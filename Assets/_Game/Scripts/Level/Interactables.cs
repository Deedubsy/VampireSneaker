using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Vespertine.Core;
using Vespertine.Visual;

namespace Vespertine.Level
{
    /// <summary>Something Ilse (or a thrall) can walk to and use.</summary>
    public abstract class Interactable : Entity
    {
        public bool Enabled = true;
        public bool Used;
        /// <summary>Visible and hoverable but refused until a script `unseal`s it (`sealed=Reason_shown`).</summary>
        public bool Sealed;
        public string SealedWhy;
        public float UseRange = 1.5f;
        protected BoxCollider Click;

        public abstract string Verb { get; }
        public virtual string DisplayName => Spec != null && Spec.Args.Count > 0 ? Spec.Arg(0) : Verb;
        public virtual float Duration => 0.4f;
        public virtual float NoiseRadius => 0f;
        public virtual bool PlayerCan => Enabled && !Sealed;
        public virtual bool ThrallCan => false;
        public virtual bool Repeatable => false;
        public virtual string Unavailable => Sealed ? SealedWhy ?? "Sealed." : null;   // reason shown when PlayerCan is false
        public virtual bool ShowMarker => Enabled && (!Used || Repeatable);

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            Sealed = spec.Has("sealed") || spec.Opts.ContainsKey("sealed");
            var why = spec.Opt("sealed");
            SealedWhy = string.IsNullOrEmpty(why) ? null : why.Replace('_', ' ');
            gameObject.layer = Layers.Interactable;
            Click = gameObject.AddComponent<BoxCollider>();
            Click.isTrigger = true;
            Click.center = new Vector3(0, 0.9f, 0);
            Click.size = new Vector3(1.2f, 1.8f, 1.2f);
        }

        /// <summary>Navmesh point from which the actor uses this.</summary>
        public virtual Vector3 UsePoint(Vector3 from, int agentType)
        {
            var p = transform.position;
            var dir = (from - p).Flat();
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            for (int i = 0; i < 8; i++)
            {
                var cand = p + Quaternion.Euler(0, i * 45f * (i % 2 == 0 ? 1 : -1), 0) * dir.normalized * 0.9f;
                if (NavMesh.SamplePosition(cand, out var hit, 0.8f, filter) && Util.FlatDistance(hit.position, p) <= UseRange) return hit.position;
            }
            if (NavMesh.SamplePosition(p, out var h2, 2f, filter)) return h2.position;
            return p;
        }

        public void Use(bool byPlayer)
        {
            if (!Enabled) return;
            if (NoiseRadius > 0 && Game.Noise != null) Game.Noise.Emit(transform.position, NoiseRadius, Stealth.NoiseKind.Object, this);
            OnUse(byPlayer);
            Used = true;
            GameEvents.RaiseInteracted(Id);
        }

        protected abstract void OnUse(bool byPlayer);

        public virtual void SaveState(Save.EntityState s) { s.B0 = Used; s.B1 = Enabled; s.B3 = Sealed; }
        public virtual void LoadState(Save.EntityState s) { Used = s.B0; Enabled = s.B1; Sealed = s.B3; }
    }

    // ------------------------------------------------------------------ Door
    /// <summary>
    /// A door (D119). Shut, it stops sight, muffles sound and stops her steps; she opens and closes it with the
    /// interact key or a click. Humans open a door as they reach it and shut it behind them (a habit, whoever opened
    /// it). Mist slips under a shut door. Locked and threshold doors refuse her as before (and are cut from her navmesh).
    /// </summary>
    public class Door : Interactable
    {
        public bool Locked, Threshold, Invited;
        public string KeyId;
        public bool EastWest;      // passage runs E-W (door panel spans N-S)
        /// <summary>Wanted state; the panel swings toward it.</summary>
        public bool IsOpen;
        Transform _panel;
        float _open;
        Bounds _cell;
        GameObject _lockGlyph;
        BoxCollider _block;
        Vector3 _pass, _span;
        float _scanT, _clearT;
        bool _humanPassing;

        /// <summary>Every door in the level, for the per-step movement check.</summary>
        public static readonly List<Door> Live = new List<Door>();

        public bool BlocksVampire => (Locked && !HasKey) || (Threshold && !Invited);
        bool HasKey => KeyId != null && Game.Player != null && Game.Player.HasItem(KeyId);
        /// <summary>Shut enough to stop a body and an eye.</summary>
        public bool Shut => _open < 0.6f;

        public override string Verb => IsOpen ? "Close" : "Open";
        public override string DisplayName => Threshold ? "Front door" : "Door";
        public override float Duration => 0.25f;
        public override bool Repeatable => true;
        public override bool ThrallCan => true;
        public override bool PlayerCan => Enabled && !Sealed && !(BlocksVampire && !IsOpen);
        public override string Unavailable => Threshold && !Invited ? "She has not been invited in." : Locked && !HasKey ? "Locked." : base.Unavailable;
        // a creak; easing it open while sneaking makes none
        public override float NoiseRadius => Game.Player != null && Game.Player.Sneaking ? 0f : 2.5f;

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            Locked = spec.Has("locked");
            Threshold = spec.Has("threshold") || spec.Has("home");
            KeyId = spec.Opt("key");
            IsOpen = spec.Has("open");
            Live.Add(this);
        }

        void OnDestroy() => Live.Remove(this);

        public void Build(LevelGrid g, int x, int y)
        {
            bool wallW = g.Def(x - 1, y).Raised, wallE = g.Def(x + 1, y).Raised;
            EastWest = !(wallW || wallE);
            var c = g.Data.CellToWorld(x, y);
            transform.position = c;
            _cell = new Bounds(c + Vector3.up, new Vector3(1.6f, 2f, 1.6f));
            var frameMat = Mats.Lit("door_frame", Util.Hex("#2a2420"), "planks", 0.1f);
            var mb = new MeshBuilder();
            Vector3 span = EastWest ? Vector3.forward : Vector3.right;
            _span = span;
            _pass = EastWest ? Vector3.right : Vector3.forward;
            mb.Box(frameMat, -span * 0.95f + Vector3.up * 1.4f, Vector3.Scale(new Vector3(0.18f, 2.8f, 0.18f), Vector3.one));
            mb.Box(frameMat, span * 0.95f + Vector3.up * 1.4f, new Vector3(0.18f, 2.8f, 0.18f));
            mb.Box(frameMat, Vector3.up * 2.6f, EastWest ? new Vector3(0.22f, 0.3f, 2.1f) : new Vector3(2.1f, 0.3f, 0.22f));
            mb.Build(transform, "frame", true, Layers.Prop);
            var hinge = new GameObject("hinge").transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = -span * 0.85f;
            hinge.localRotation = Quaternion.LookRotation(span);
            _panel = hinge;
            var pm = new MeshBuilder();
            var panelMat = Threshold ? Mats.Lit("door_home", Util.Hex("#4a2a24"), "planks", 0.2f) : Mats.Lit("door_panel", Util.Hex("#3e3026"), "planks", 0.2f);
            pm.Box(panelMat, new Vector3(0, 1.2f, 0.85f), new Vector3(0.1f, 2.4f, 1.7f));
            pm.Box(Mats.Lit("p_iron", Util.Hex("#202126"), null, 0.5f, 0.7f), new Vector3(0.08f, 1.15f, 1.5f), new Vector3(0.06f, 0.1f, 0.1f));
            pm.Build(hinge, "panel", true, Layers.Prop);
            // the shut panel as an obstacle to sight and sound (its own layer: light still leaks round a door)
            var blk = new GameObject("shut");
            blk.layer = Layers.Door;
            blk.transform.SetParent(transform, false);
            _block = blk.AddComponent<BoxCollider>();
            _block.center = new Vector3(0, 1.3f, 0);
            _block.size = EastWest ? new Vector3(0.14f, 2.6f, 1.9f) : new Vector3(1.9f, 2.6f, 0.14f);
            Click.size = EastWest ? new Vector3(0.6f, 2.4f, 1.9f) : new Vector3(1.9f, 2.4f, 0.6f);
            Click.center = new Vector3(0, 1.2f, 0);
            _open = IsOpen ? 1f : 0f;
            Pose();
            RefreshGlyph();
        }

        public Bounds CellBounds => _cell;

        void RefreshGlyph()
        {
            if (_lockGlyph) Destroy(_lockGlyph);
            if (!BlocksVampire) return;
            _lockGlyph = new GameObject("lockglyph");
            _lockGlyph.transform.SetParent(transform, false);
            var mb = new MeshBuilder();
            var m = Threshold ? Mats.Overlay("glyph_threshold", new Color(1f, 0.9f, 0.6f, 0.35f), "ring", false, true) : Mats.Overlay("glyph_lock", new Color(0.8f, 0.8f, 0.9f, 0.3f), "ring", false, true);
            mb.Top(m, -0.9f, -0.9f, 0.9f, 0.9f, 0.04f);
            mb.Build(_lockGlyph.transform, "g", false, Layers.Overlay);
        }

        public void SetLocked(bool v) { Locked = v; if (v && !Occupied()) SetOpen(false); Changed(); }
        public void Invite() { if (Threshold && !Invited) { Invited = true; Changed(); Game.UI?.Toast("You have been invited in."); } }
        public void OnKeyAcquired() { if (Locked) Changed(); }
        void Changed() { RefreshGlyph(); Game.Level?.RequestVampireNavRebuild(); }

        protected override void OnUse(bool byPlayer) => SetOpen(!IsOpen);

        public void SetOpen(bool v, bool instant = false)
        {
            if (IsOpen == v && !instant) return;
            IsOpen = v;
            if (instant) { _open = v ? 1f : 0f; Pose(); }
            else if (Game.Audio) Game.Audio.PlayAt("door", transform.position, v ? 0.35f : 0.28f, v ? 1f : 0.85f);
        }

        /// <summary>The move step a body at <paramref name="feet"/> may take past the shut doors: the part heading
        /// into a shut panel is taken away, the part along it kept (she slides). Mist is not stopped.</summary>
        public static Vector3 ClampStep(Vector3 feet, Vector3 step)
        {
            for (int i = 0; i < Live.Count; i++)
            {
                var d = Live[i];
                if (d && d.Shut && d.isActiveAndEnabled) step = BlockStep(feet - d.transform.position, step, d._pass, d._span);
            }
            return step;
        }

        /// <summary>Pure core of <see cref="ClampStep"/>: <paramref name="rel"/> is the body relative to the door's
        /// centre, <paramref name="pass"/> the way through it, <paramref name="span"/> along the panel. Unit-tested.</summary>
        public static Vector3 BlockStep(Vector3 rel, Vector3 step, Vector3 pass, Vector3 span, float half = 0.4f)
        {
            if (Mathf.Abs(rel.y) > 1.5f) return step;
            var next = rel + step;
            if (Mathf.Abs(Vector3.Dot(next, span)) > 1.15f) return step;
            float a = Vector3.Dot(rel, pass), b = Vector3.Dot(next, pass);
            if (Mathf.Abs(b) >= half || Mathf.Abs(b) > Mathf.Abs(a)) return step;   // clear of it, or moving away
            return step - pass * Vector3.Dot(step, pass);
        }

        public bool Occupied()
        {
            if (Game.AI != null)
                foreach (var n in Game.AI.Npcs)
                    if (n && !n.Carried && !n.Hidden && !n.Disposed && n.gameObject.activeInHierarchy && Near(n.transform.position, 1.3f)) return true;
            var p = Game.Player;
            return p != null && !p.Dead && Near(p.Feet, 1.3f);
        }

        bool Near(Vector3 pos, float r)
        {
            var d = pos - transform.position;
            if (Mathf.Abs(d.y) > 1.5f) return false;
            d.y = 0f;
            return d.sqrMagnitude < r * r;
        }

        /// <summary>Humans open a door they reach (one under her hand does not: she opens it) and shut it once they
        /// are through. Ilse walking on her own to something (an approach to feed or use) opens it too.</summary>
        void Scan()
        {
            bool human = false, anyone = false;
            var sel = Game.Player != null ? Game.Player.SelectedThrall : null;
            if (Game.AI != null)
                foreach (var n in Game.AI.Npcs)
                {
                    if (!n || n.Carried || n.Hidden || n.Disposed || !n.gameObject.activeInHierarchy) continue;
                    if (!Near(n.transform.position, 1.45f)) continue;
                    anyone = true;
                    if (!n.IsAlive || (n == sel && n.Order == AI.ThrallOrder.None)) continue;
                    if (Heading(n.transform.position, n.Agent && n.Agent.enabled ? n.Agent.velocity : n.transform.forward * n.Speed)) human = true;
                }
            var p = Game.Player;
            if (p != null && !p.Dead && Near(p.Feet, 1.3f))
            {
                anyone = true;
                if (!IsOpen && !BlocksVampire && p.Moving && !p.DirectMoving && p.Agent && Heading(p.Feet, p.Agent.velocity)) SetOpen(true);
            }
            if (human)
            {
                _humanPassing = true;
                if (!IsOpen) SetOpen(true);
            }
            _clearT = anyone ? 0f : _clearT + 0.1f;
            if (IsOpen && _humanPassing && _clearT > 0.8f) { _humanPassing = false; SetOpen(false); }
        }

        /// <summary>Someone at <paramref name="pos"/> going at <paramref name="vel"/> is in the doorway or walking at it
        /// (not along the wall past it).</summary>
        bool Heading(Vector3 pos, Vector3 vel)
        {
            var rel = pos - transform.position;
            float side = Vector3.Dot(rel, _span);
            if (Mathf.Abs(side) > 0.95f) return false;
            float across = Vector3.Dot(rel, _pass);
            if (Mathf.Abs(across) < 0.55f) return true;
            return Vector3.Dot(vel, _pass) * -Mathf.Sign(across) > 0.25f;
        }

        void Pose()
        {
            if (_panel) _panel.localRotation = Quaternion.LookRotation(_span) * Quaternion.Euler(0, -100f * Mathf.SmoothStep(0, 1, _open), 0);
            if (_block) _block.enabled = Shut;
        }

        void Update()
        {
            if ((_scanT -= Time.deltaTime) <= 0f) { _scanT = 0.1f; Scan(); }
            float target = IsOpen ? 1f : 0f;
            if (_open == target) return;
            _open = Mathf.MoveTowards(_open, target, Time.deltaTime * 3f);
            Pose();
        }
    }

    // ------------------------------------------------------------------ Gate (blocks everyone)
    /// <summary>Iron bars raised by a valve or lever elsewhere. Touching it says what raises it; `rise=` seconds to lift
    /// (the way is open once the bars clear a head).</summary>
    public class Gate : Interactable
    {
        public bool Open;
        Transform _bars;
        NavMeshObstacle _obstacle;
        BoxCollider _col;
        float _t, _rise = 1.7f;

        public override string Verb => "Raise";
        public override string DisplayName => Spec != null && Spec.Args.Count > 0 ? Spec.Arg(0) : "Gate";
        public override bool PlayerCan => false;
        public override bool ShowMarker => !Open;
        public override string Unavailable
        {
            get
            {
                var why = Spec != null ? Spec.Opt("why") : null;
                if (!string.IsNullOrEmpty(why)) return why.Replace('_', ' ');
                if (Game.Level != null)
                {
                    foreach (var v in Game.Level.All<Valve>()) if (v.GateId == Id) return "Barred. The wheel valve nearby raises it.";
                    foreach (var l in Game.Level.All<Lever>()) if (l.GateId == Id) return "Barred. A lever somewhere raises it.";
                }
                return "Barred. It will not lift by hand.";
            }
        }

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            Open = spec.Has("open");
            _rise = Mathf.Max(0.3f, spec.OptFloat("rise", 1.7f));
        }

        protected override void OnUse(bool byPlayer) { }

        public void Build(LevelGrid g, int x, int y)
        {
            transform.position = g.Data.CellToWorld(x, y);
            bool eastWest = !(g.Def(x - 1, y).Raised || g.Def(x + 1, y).Raised);
            Pass = eastWest ? Vector3.right : Vector3.forward;
            var mb = new MeshBuilder();
            var iron = Mats.Lit("p_iron", Util.Hex("#202126"), null, 0.5f, 0.7f);
            for (int i = 0; i < 9; i++)
            {
                float o = -0.9f + i * 0.225f;
                mb.Box(iron, eastWest ? new Vector3(0, 1.4f, o) : new Vector3(o, 1.4f, 0), new Vector3(0.06f, 2.8f, 0.06f));
            }
            mb.Box(iron, new Vector3(0, 0.6f, 0), eastWest ? new Vector3(0.08f, 0.08f, 2f) : new Vector3(2f, 0.08f, 0.08f));
            mb.Box(iron, new Vector3(0, 2.2f, 0), eastWest ? new Vector3(0.08f, 0.08f, 2f) : new Vector3(2f, 0.08f, 0.08f));
            _bars = new GameObject("bars").transform;
            _bars.SetParent(transform, false);
            mb.Build(_bars, "gate", true, Layers.Bars);
            var blk = new GameObject("block");
            blk.layer = Layers.Bars;
            blk.transform.SetParent(transform, false);
            _col = blk.AddComponent<BoxCollider>();
            _col.center = new Vector3(0, 1.4f, 0);
            _col.size = eastWest ? new Vector3(0.3f, 2.8f, 2f) : new Vector3(2f, 2.8f, 0.3f);
            Click.size = eastWest ? new Vector3(0.8f, 2.6f, 2f) : new Vector3(2f, 2.6f, 0.8f);
            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
            _obstacle.shape = NavMeshObstacleShape.Box;
            _obstacle.center = _col.center;
            _obstacle.size = eastWest ? new Vector3(0.6f, 2.8f, 2f) : new Vector3(2f, 2.8f, 0.6f);
            _obstacle.carving = true;
            _obstacle.carveOnlyStationary = false;
            Apply(true);
        }

        public void SetOpen(bool v, bool instant = false)
        {
            if (Open == v && !instant) return;
            Open = v;
            if (!instant && Game.Audio) Game.Audio.PlayAt("gate", transform.position, 0.9f);
            if (!instant && Game.Noise != null) Game.Noise.Emit(transform.position, 10f, Stealth.NoiseKind.Object, this);
            Apply(instant);
        }

        /// <summary>Passable once the bars are above a head.</summary>
        public bool Clear => _t > 0.55f;

        /// <summary>The way through the gate (flat world axis).</summary>
        public Vector3 Pass { get; private set; } = Vector3.forward;

        void Apply(bool instant)
        {
            if (instant) _t = Open ? 1 : 0;
            if (_col) _col.enabled = !Clear;
            if (_obstacle) _obstacle.enabled = !Clear;
        }

        void Update()
        {
            float target = Open ? 1 : 0;
            if (_t == target) return;
            bool was = Clear;
            _t = Mathf.MoveTowards(_t, target, Time.deltaTime / _rise);
            if (_bars) _bars.localPosition = Vector3.up * 2.6f * _t;
            if (Clear != was) Apply(false);
        }
    }

    // ------------------------------------------------------------------ Valve / Lever
    public class Valve : Interactable
    {
        public string LightGroup, GateId;
        /// <summary>`wheel=<item>`: the hand-wheel has been taken off (D119). The bare spindle shows until she fits it.</summary>
        public string WheelItem;
        public bool State; // false = initial
        GameObject _wheel;
        public override string Verb => GateId != null ? (WheelItem != null && !Used ? "Fit the wheel and turn it" : "Turn the wheel") : (State ? "Open the gas valve" : "Close the gas valve");
        public override string DisplayName => Spec != null && Spec.Args.Count > 0 ? Spec.Arg(0) : GateId != null ? "Wheel valve" : "Gas valve";
        public override float Duration => 1.6f;
        public override float NoiseRadius => Spec != null ? Spec.OptFloat("noise", 8f) : 8f;
        public override bool ThrallCan => true;
        public override bool Repeatable => GateId == null;

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            LightGroup = spec.Opt("group");
            GateId = spec.Opt("gate");
            WheelItem = spec.Opt("wheel");
            BuildModel();
        }

        void Update()
        {
            if (_wheel && _wheel.activeSelf != (WheelItem == null || Used)) _wheel.SetActive(WheelItem == null || Used);
        }

        protected virtual void BuildModel()
        {
            var mb = new MeshBuilder();
            var iron = Mats.Lit("p_iron", Util.Hex("#202126"), null, 0.5f, 0.7f);
            var red = Mats.Lit("valve_wheel", Util.Hex("#7a2020"), null, 0.4f, 0.5f);
            mb.Cylinder(iron, Vector3.zero, 0.12f, 1.0f, 6);
            mb.Cylinder(iron, new Vector3(0, 1.0f, 0), 0.04f, 0.12f, 6);   // the spindle
            mb.Build(transform, "valve", true, Layers.Prop);
            var wm = new MeshBuilder();
            _wheel = new GameObject("wheel");
            _wheel.transform.SetParent(transform, false);
            wm.Cylinder(red, new Vector3(0, 1.0f, 0), 0.35f, 0.06f, 10);
            wm.Build(_wheel.transform, "w", true, Layers.Prop);
        }

        protected override void OnUse(bool byPlayer)
        {
            State = !State;
            PlaySound();
            if (LightGroup != null && Game.Lights != null)
                foreach (var l in Game.Lights.All.ToArray())
                    if (l.LightGroup == LightGroup) { l.GasCut = State; l.SetOn(!State, true); }   // a thrall at the main is still her doing
            if (GateId != null) Game.Level?.Get<Gate>(GateId)?.SetOpen(true);
        }

        protected virtual void PlaySound() { if (Game.Audio) Game.Audio.PlayAt("valve", transform.position, 0.9f); }

        public override void SaveState(Save.EntityState s) { base.SaveState(s); s.B2 = State; }
        public override void LoadState(Save.EntityState s)
        {
            base.LoadState(s);
            State = s.B2;
            if (LightGroup != null && Game.Lights != null)
                foreach (var l in Game.Lights.All) if (l && l.LightGroup == LightGroup) l.GasCut = State;
        }

        /// <summary>The gas main feeding a lamp group (lamplighters walk here to reopen it).</summary>
        public static Valve ForGroup(string group)
        {
            if (group == null || Game.Level == null) return null;
            foreach (var v in Game.Level.All<Valve>()) if (v && v.GateId == null && v.LightGroup == group) return v;
            return null;
        }
    }

    /// <summary>
    /// The sunstone generator (M09+): a breaker that cuts every lamp in its <c>group=</c>. Behaves as a gas main
    /// (an engineer who notices the dark walks here and restores the current), but sounds and looks like a machine.
    /// </summary>
    public class Generator : Valve
    {
        public override string Verb => State ? "Restore the current" : "Throw the breaker";
        public override string DisplayName => Spec != null && Spec.Args.Count > 0 ? Spec.Arg(0) : "Sunstone generator";
        public override float Duration => 1.2f;
        public override float NoiseRadius => Spec != null ? Spec.OptFloat("noise", 10f) : 10f;
        Transform _switch;
        GameObject _coilGlow;

        protected override void BuildModel()
        {
            var mb = new MeshBuilder();
            var iron = Mats.Lit("p_iron", Util.Hex("#202126"), null, 0.5f, 0.7f);
            var brass = Mats.Lit("gen_brass", Util.Hex("#8a6a30"), null, 0.6f, 0.8f);
            mb.Box(iron, new Vector3(0, 0.6f, 0), new Vector3(1.3f, 1.2f, 0.8f));
            mb.Cylinder(brass, new Vector3(-0.35f, 1.2f, 0), 0.22f, 0.5f, 10);
            mb.Cylinder(brass, new Vector3(0.35f, 1.2f, 0), 0.22f, 0.5f, 10);
            mb.Build(transform, "generator", true, Layers.Prop);
            var glow = new MeshBuilder();
            glow.Box(Mats.Lit("gen_coil", Util.Hex("#fff0b0") * 0.4f, null, 0.8f, 0, Util.Hex("#fff0b0") * 2.5f), new Vector3(0, 1.75f, 0), new Vector3(0.9f, 0.12f, 0.12f));
            _coilGlow = new GameObject("coil");
            _coilGlow.transform.SetParent(transform, false);
            glow.Build(_coilGlow.transform, "coil", false, Layers.Prop);
            _switch = new GameObject("switch").transform;
            _switch.SetParent(transform, false);
            _switch.localPosition = new Vector3(0.66f, 0.9f, 0);
            var sb = new MeshBuilder();
            sb.Box(Mats.Lit("valve_wheel", Util.Hex("#7a2020"), null, 0.4f, 0.5f), new Vector3(0, 0.25f, 0), new Vector3(0.07f, 0.5f, 0.07f));
            sb.Build(_switch, "h", true, Layers.Prop);
            ShowState();
        }

        void ShowState()
        {
            if (_switch) _switch.localRotation = Quaternion.Euler(0, 0, State ? -60f : 20f);
            if (_coilGlow) _coilGlow.SetActive(!State);
        }

        protected override void PlaySound() { Game.Audio?.PlayAt("breaker", transform.position, 1f); ShowState(); }

        public override void LoadState(Save.EntityState s) { base.LoadState(s); ShowState(); }
    }

    /// <summary>
    /// A sunstone lamp's housing: Ilse cannot touch it, but a thrall can smash it (dark for good). Spawned beside every
    /// sunstone light with an id, as <c>&lt;light&gt;.smash</c>.
    /// </summary>
    public class LampSmash : Interactable
    {
        public Stealth.GameLight Light;
        public override string Verb => "Smash the sunstone lamp";
        public override string DisplayName => "Sunstone lamp";
        public override float Duration => 1f;
        public override float NoiseRadius => 12f;
        public override bool PlayerCan => false;
        public override bool ThrallCan => Enabled && Light && !Light.Broken;
        public override string Unavailable => "It burns her kind. A thrall could smash it.";
        public override bool ShowMarker => Enabled && Light && !Light.Broken && Light.gameObject.activeInHierarchy;

        protected override void OnUse(bool byPlayer)
        {
            if (Light) Light.Smash();
            Enabled = false;
        }
    }

    public class Lever : Interactable
    {
        public string GateId;
        public override string Verb => "Pull the lever";
        public override string DisplayName => "Lever";
        public override float Duration => 0.8f;
        public override float NoiseRadius => 6f;
        public override bool ThrallCan => true;
        Transform _handle;

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            GateId = spec.Opt("gate");
            var mb = new MeshBuilder();
            mb.Box(Mats.Lit("p_iron", Util.Hex("#202126"), null, 0.5f, 0.7f), new Vector3(0, 0.5f, 0), new Vector3(0.4f, 1f, 0.3f));
            mb.Build(transform, "base", true, Layers.Prop);
            _handle = new GameObject("handle").transform;
            _handle.SetParent(transform, false);
            _handle.localPosition = new Vector3(0, 1f, 0);
            _handle.localRotation = Quaternion.Euler(-35, 0, 0);
            var hb = new MeshBuilder();
            hb.Box(Mats.Lit("valve_wheel", Util.Hex("#7a2020"), null, 0.4f, 0.5f), new Vector3(0, 0.35f, 0), new Vector3(0.08f, 0.7f, 0.08f));
            hb.Build(_handle, "h", true, Layers.Prop);
        }

        protected override void OnUse(bool byPlayer)
        {
            _handle.localRotation = Quaternion.Euler(35, 0, 0);
            if (Game.Audio) Game.Audio.PlayAt("valve", transform.position, 0.6f, 1.4f);
            if (GateId != null) Game.Level?.Get<Gate>(GateId)?.SetOpen(true);
        }
    }

    // ------------------------------------------------------------------ Documents, secrets, items
    public class Note : Interactable
    {
        public string Title => Spec.Arg(0, "Note");
        public string Body => Spec.Arg(1, "");
        public override string Verb => "Read";
        public override string DisplayName => Title;
        public override bool Repeatable => true;

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            var mb = new MeshBuilder();
            if (!spec.Has("nostand"))
                mb.Box(Mats.Lit("p_dwood", Util.Hex("#3a2a20"), "planks", 0.2f), new Vector3(0, 0.4f, 0), new Vector3(0.6f, 0.8f, 0.5f));
            mb.Box(Mats.Lit("note_paper", Util.Hex("#e8dcc0"), null, 0.05f, 0, Util.Hex("#e8dcc0") * 0.25f), new Vector3(0, spec.Has("nostand") ? 0.02f : 0.82f, 0), new Vector3(0.35f, 0.02f, 0.45f), 12f);
            mb.Build(transform, "note", false, Layers.Prop);
        }

        protected override void OnUse(bool byPlayer)
        {
            Game.Campaign?.DiscoverLore(Id, Title, Body);
            Game.UI?.ShowDocument(Title, Body);
        }
    }

    public class Secret : Interactable
    {
        public string Title => Spec.Arg(0, "Secret");
        public string Body => Spec.Arg(1, "");
        public bool LoreOnly => Spec.Has("lore");
        public override string Verb => "Examine";
        public override string DisplayName => Used ? Title : "Something hidden";

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            var mb = new MeshBuilder();
            mb.Box(Mats.Lit("secret_box", Util.Hex("#3a1a24"), null, 0.6f, 0.3f, Mats.Pal.Blood * 0.6f), new Vector3(0, 0.25f, 0), new Vector3(0.4f, 0.5f, 0.3f), 20f);
            mb.Build(transform, "secret", false, Layers.Prop);
        }

        protected override void OnUse(bool byPlayer)
        {
            if (Game.Audio) Game.Audio.Play2D("pickup");
            GameEvents.RaiseSecret(Id);
            Game.Campaign?.DiscoverLore(Id, Title, Body);
            if (!string.IsNullOrEmpty(Body)) Game.UI?.ShowDocument(Title, Body);
            else Game.UI?.Toast("Secret found: " + Title);
            foreach (Transform c in transform) Destroy(c.gameObject);
            Enabled = false;
        }
    }

    public class Item : Interactable
    {
        public string ItemType => Spec.Type;
        public override string Verb => "Take";
        public override string DisplayName => Spec.Arg(0, ItemType == "key" ? "Key" : ItemType == "wheel" ? "Valve wheel" : "Item");

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            var mb = new MeshBuilder();
            var m = ItemType == "key" ? Mats.Lit("item_key", Util.Hex("#b08a40"), null, 0.8f, 1f, Util.Hex("#b08a40") * 0.3f)
                  : ItemType == "mask" ? Mats.Lit("item_mask", Util.Hex("#ece6f0"), null, 0.6f, 0.2f, Util.Hex("#c8a8e0") * 0.35f)
                                      : Mats.Lit("item_doc", Util.Hex("#e0d4b0"), null, 0.1f, 0, Util.Hex("#e0d4b0") * 0.25f);
            mb.Box(Mats.Lit("p_dwood", Util.Hex("#3a2a20"), "planks", 0.2f), new Vector3(0, 0.4f, 0), new Vector3(0.6f, 0.8f, 0.5f));
            if (ItemType == "wheel")
            {
                // a valve's iron hand-wheel lying flat on the box: a rim of eight bars, two spokes and a hub
                var iron = Mats.Lit("item_wheel", Util.Hex("#3a3c44"), null, 0.55f, 0.8f, Util.Hex("#8a6040") * 0.12f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    mb.Box(iron, new Vector3(Mathf.Sin(a) * 0.24f, 0.84f, Mathf.Cos(a) * 0.24f), new Vector3(0.2f, 0.05f, 0.05f), i * 45f);
                }
                mb.Box(iron, new Vector3(0, 0.84f, 0), new Vector3(0.46f, 0.04f, 0.04f), 20f);
                mb.Box(iron, new Vector3(0, 0.84f, 0), new Vector3(0.46f, 0.04f, 0.04f), 110f);
                mb.Box(iron, new Vector3(0, 0.86f, 0), new Vector3(0.09f, 0.08f, 0.09f));
            }
            else mb.Box(m, new Vector3(0, 0.84f, 0), ItemType == "key" ? new Vector3(0.25f, 0.05f, 0.08f) : new Vector3(0.3f, 0.06f, 0.4f), 30f);
            mb.Build(transform, "item", false, Layers.Prop);
        }

        protected override void OnUse(bool byPlayer)
        {
            if (Game.Audio) Game.Audio.Play2D("pickup");
            Game.Player?.AddItem(Id);
            Game.UI?.Toast("Taken: " + DisplayName);
            if (!string.IsNullOrEmpty(Spec.Arg(1))) Game.UI?.ShowDocument(DisplayName, Spec.Arg(1));
            foreach (Transform c in transform) if (c.name == "item") Destroy(c.gameObject);
            Enabled = false;
        }
    }

    // ------------------------------------------------------------------ Bell
    public class Bell : Interactable
    {
        public bool Silenced;
        public float LastRung = -999f;
        public override string Verb => "Cut the bell rope";
        public override string DisplayName => "Alarm bell";
        public override float Duration => 2f;
        public override bool PlayerCan => !Silenced;
        public override bool ShowMarker => !Silenced;

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            var mb = new MeshBuilder();
            var wood = Mats.Lit("p_dwood", Util.Hex("#3a2a20"), "planks", 0.2f);
            var bronze = Mats.Lit("bell_bronze", Util.Hex("#8a6a30"), null, 0.7f, 0.9f);
            mb.Box(wood, new Vector3(-0.6f, 1.3f, 0), new Vector3(0.15f, 2.6f, 0.15f));
            mb.Box(wood, new Vector3(0.6f, 1.3f, 0), new Vector3(0.15f, 2.6f, 0.15f));
            mb.Box(wood, new Vector3(0, 2.55f, 0), new Vector3(1.4f, 0.15f, 0.2f));
            mb.Cylinder(bronze, new Vector3(0, 1.75f, 0), 0.38f, 0.7f, 10);
            mb.Build(transform, "bell", true, Layers.Prop);
        }

        public void Ring(Vector3 by)
        {
            if (Silenced) return;
            LastRung = Time.time;
            if (Game.Audio) Game.Audio.PlayAt("bell", transform.position, 1f, 1f, 120f);
            Game.Noise?.Emit(transform.position, 60f, Stealth.NoiseKind.Bell, this);
            Game.AI?.RaiseLockdown("bell " + Id);
        }

        protected override void OnUse(bool byPlayer)
        {
            Silenced = true;
            Game.UI?.Toast("The bell is silenced.");
        }

        public override void SaveState(Save.EntityState s) { base.SaveState(s); s.B2 = Silenced; }
        public override void LoadState(Save.EntityState s) { base.LoadState(s); Silenced = s.B2; }
    }

    // ------------------------------------------------------------------ Generic scripted use (window, boat, mechanism)
    public class ScriptedUse : Interactable
    {
        public override string Verb => Spec.Opt("verb", Spec.Kind == "window" ? "Knock" : Spec.Kind == "boat" ? "Take the boat" : "Use").Replace('_', ' ');
        public override string DisplayName => Spec.Arg(0, Spec.Kind == "window" ? "Window" : Spec.Kind == "boat" ? "Boat" : "Mechanism").Replace('_', ' ');
        public override float Duration => Spec.OptFloat("time", 0.8f);
        public override float NoiseRadius => Spec.OptFloat("noise", 0f);
        public override bool Repeatable => Spec.Has("repeat");
        public override bool ThrallCan => Spec.Has("thrall");

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            if (spec.Kind == "boat") PropFactory.Build("boat", transform, transform.position, spec.OptFloat("yaw", 0), 7);
            // use id x y "Name" verb=... [prop=desk yaw=90] [time= noise= thrall repeat]: a scripted mechanism or deed
            if (spec.Kind == "use" && spec.Opt("prop") != null) PropFactory.Build(spec.Opt("prop"), transform, transform.position, spec.OptFloat("yaw", 0), spec.Line);
            if (spec.Has("disabled")) Enabled = false;
        }

        protected override void OnUse(bool byPlayer) { }
    }

    // ------------------------------------------------------------------ Trap: an arranged accident
    /// <summary>
    /// `trap id x y radius=1.2 victims=ashcombe drop=x,y [sweep] "Loose rail" verb=Loosen`. Ilse prepares it; afterwards the next
    /// living human (of the listed victims, if any) who stands within the radius dies by accident, and the body may fall
    /// to the drop cell. An accident is not her kill: it never counts against nokill, and whoever finds the body
    /// mourns rather than raising the alarm.
    /// </summary>
    public class Trap : Interactable
    {
        public bool Armed => Used && !Sprung;
        public bool Sprung;
        public float Radius = 1.2f;
        public string Victim;          // id of whoever it took
        string[] _victims;

        public override string Verb => Spec.Opt("verb", "Tamper with").Replace('_', ' ');
        public override string DisplayName => Spec.Arg(0, "Trap").Replace('_', ' ');
        public override float Duration => Spec.OptFloat("time", 2.5f);
        public override float NoiseRadius => Spec.OptFloat("noise", 2f);
        public override bool ShowMarker => Enabled && !Used;

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            Radius = spec.OptFloat("radius", 1.2f);
            var v = spec.Opt("victims");
            _victims = string.IsNullOrEmpty(v) ? null : v.Split(',');
        }

        /// <summary>Would this trap take an NPC with this id/archetype standing here? (Pure; unit-tested.)</summary>
        public static bool Takes(string[] victims, string id, string archId, Vector3 trapPos, float radius, Vector3 at)
        {
            if (victims != null && System.Array.IndexOf(victims, id) < 0 && System.Array.IndexOf(victims, archId) < 0) return false;
            return Util.FlatDistance(trapPos, at) <= radius && Mathf.Abs(at.y - trapPos.y) < 1.6f;
        }

        protected override void OnUse(bool byPlayer)
        {
            Game.Audio?.PlayAt("door", transform.position, 0.5f);
            Game.UI?.Toast(Spec.Opt("armed", "It will hold for no one now.").Replace('_', ' '));
        }

        void Update()
        {
            if (!Armed || Game.AI == null) return;
            foreach (var n in Game.AI.Npcs)
            {
                if (!n || !n.IsAlive || n.Carried || n.IsThrall || n.IsCorpsePuppet || n.Arch.Has(Data.ArchFlags.Quadruped)) continue;
                if (!Takes(_victims, n.Id, n.Arch.Id, transform.position, Radius, n.transform.position)) continue;
                Spring(n);
                break;
            }
        }

        void Spring(AI.Npc n)
        {
            Sprung = true;
            Victim = n.Id;
            n.Die("accident");
            var drop = Spec.Opt("drop");
            if (!string.IsNullOrEmpty(drop) && Game.Level != null)
            {
                var xy = drop.Split(',');
                if (xy.Length == 2 && float.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cx)
                    && float.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cy))
                    n.transform.position = Game.Level.Data.CellToWorld(cx, cy, Game.Level.SurfaceHeightCell(cx, cy));
            }
            bool sweep = Spec.Has("sweep");
            Game.Audio?.PlayAt(sweep ? "splash" : "thud", n.transform.position, 1f);
            Game.Noise?.Emit(n.transform.position, Spec.OptFloat("fallnoise", 10f), Stealth.NoiseKind.Scream, n);
            // `sweep`: running water takes the body; nobody will find it
            if (sweep) n.Dispose("canal");
            GameEvents.RaiseInteracted(Id + ".sprung");
        }

        public override void SaveState(Save.EntityState s) { base.SaveState(s); s.B2 = Sprung; s.S0 = Victim; }
        public override void LoadState(Save.EntityState s) { base.LoadState(s); Sprung = s.B2; Victim = s.S0; }
    }

    // ------------------------------------------------------------------ Hide spot (prop flagged 'hide')
    public class HideSpot : Interactable
    {
        public readonly List<string> Bodies = new List<string>();
        public int Capacity = 2;
        public string PropType;
        public bool PlayerInside;

        public override string Verb => Game.Player != null && Game.Player.Carrying != null ? "Hide the body" : PlayerInside ? "Leave" : "Hide inside";
        public override string DisplayName => char.ToUpper(PropType[0]) + PropType.Substring(1);
        public override float Duration => Game.Player != null && Game.Player.Carrying != null ? 1.2f : 0.5f;
        public override bool Repeatable => true;
        public bool Full => Bodies.Count >= Capacity;
        public override bool PlayerCan => Enabled && !(Game.Player != null && Game.Player.Carrying != null && Full);
        public override string Unavailable => Full ? "Full" : null;

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            PropType = spec.Type ?? "crate";
            Capacity = spec.OptInt("capacity", PropType == "well" ? 4 : 2);
            Click.size = new Vector3(1.8f, 2f, 1.8f);
        }

        protected override void OnUse(bool byPlayer)
        {
            Game.Player?.UseHideSpot(this);
        }

        public bool AddBody(AI.Npc n)
        {
            if (n == null || (Full && !Bodies.Contains(n.Id))) return false;
            if (!Bodies.Contains(n.Id)) Bodies.Add(n.Id);
            n.HideIn(this);
            Game.Audio?.PlayAt("thud", transform.position, 0.5f, 0.8f);
            return true;
        }

        public void RemoveBody(AI.Npc n) { if (n != null) Bodies.Remove(n.Id); }

        public override void SaveState(Save.EntityState s) { base.SaveState(s); s.S0 = string.Join(",", Bodies); s.B2 = PlayerInside; }
        public override void LoadState(Save.EntityState s)
        {
            base.LoadState(s);
            Bodies.Clear();
            if (!string.IsNullOrEmpty(s.S0)) Bodies.AddRange(s.S0.Split(','));
            PlayerInside = s.B2;
        }
    }

    // ------------------------------------------------------------------ Zones, spawns, listen points
    public class Zone : Entity
    {
        public Rect CellRect;
        public Bounds WorldBounds;
        public bool Restricted;
        public bool PlayerInside;
        /// <summary>"home=doorId[,doorId…]": the zone is a dwelling the vampire may not enter until one of its threshold doors
        /// invites her (which invites her through all of them).</summary>
        public string[] HomeDoors = new string[0];

        public void Setup(LevelData d)
        {
            float w = Spec.OptFloat("w", Util.ParseF(Spec.Arg(0), 1)), h = Spec.OptFloat("h", Util.ParseF(Spec.Arg(1), 1));
            CellRect = new Rect(Spec.X, Spec.Y, w, h);
            var a = d.CellToWorld(Spec.X, Spec.Y);
            var b = d.CellToWorld(Spec.X + w - 1, Spec.Y + h - 1);
            var min = Vector3.Min(a, b) - new Vector3(1, 0, 1);
            var max = Vector3.Max(a, b) + new Vector3(1, 0, 1);
            WorldBounds = new Bounds((min + max) * 0.5f + Vector3.up * 5f, new Vector3(max.x - min.x, 20f, max.z - min.z));
            Restricted = Spec.Has("restricted");
            var home = Spec.Opt("home");
            HomeDoors = string.IsNullOrEmpty(home) ? new string[0] : home.Split(',');
            transform.position = WorldBounds.center;
        }

        public bool Contains(Vector3 p) => p.x >= WorldBounds.min.x && p.x <= WorldBounds.max.x && p.z >= WorldBounds.min.z && p.z <= WorldBounds.max.z;
    }

    public class SpawnPoint : Entity { }

    /// <summary>
    /// `listen id x y radius=4 speakers=a,b "Title" "a: line" "b: line" …` — a conversation Ilse can overhear.
    /// While every speaker is alive, at ease and near the point they talk in rounds; staying inside the ring for a
    /// whole round counts as overheard and raises an interact event with the point's id (so interact / interact_all
    /// objectives and `on interact` rules work unchanged). A faint ring marks the spot until it is heard.
    /// </summary>
    public class ListenPoint : Entity
    {
        public float Radius = 5f;
        public bool Triggered;
        public string Title;
        public readonly List<AI.Npc> Speakers = new List<AI.Npc>();
        public Eavesdrop Talk;
        GameObject _ring;

        public bool Listening => !Triggered && Talk != null && Talk.Listening && Talk.Index >= 0;
        public float Progress => Talk != null ? Talk.Progress : 0f;

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            Radius = spec.OptFloat("radius", 5f);
            Title = spec.Arg(0, "A conversation").Replace('_', ' ');
            var ids = (spec.Opt("speakers", "") ?? "").Split(',');
            Talk = new Eavesdrop(spec.OptFloat("delay", 2f)) { Rest = spec.OptFloat("rest", 8f) };
            for (int i = 1; i < spec.Args.Count; i++) Talk.Lines.Add(Eavesdrop.ParseLine(spec.Args[i], ids[0]));
        }

        bool _linked;
        static AI.Npc FindNpc(string id)
        {
            var n = Game.Level != null ? Game.Level.Get<AI.Npc>(id) : null;
            if (!n && Game.AI != null) n = Game.AI.Npcs.Find(x => x && x.Id == id);
            return n;
        }

        /// <summary>Speakers resolve lazily: NPCs spawn after the level geometry.</summary>
        void Resolve()
        {
            _linked = true;
            Speakers.Clear();
            var ids = new HashSet<string>(Spec.Opt("speakers", "").Split(','));
            foreach (var l in Talk.Lines) ids.Add(l.Who);
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var n = FindNpc(id);
                if (n && !Speakers.Contains(n)) Speakers.Add(n);
            }
            if (!_ring) BuildRing();
        }

        void BuildRing()
        {
            if (_ring) Destroy(_ring);
            if (Triggered) return;
            _ring = new GameObject("listenring");
            _ring.transform.SetParent(transform, false);
            var mb = new MeshBuilder();
            float r = Radius;
            mb.Top(Mats.Overlay("glyph_listen", new Color(0.75f, 0.7f, 0.95f, 0.16f), "ring", false, true), -r, -r, r, r, 0.05f);
            mb.Build(_ring.transform, "g", false, Layers.Overlay);
        }

        public void SetTriggered(bool v)
        {
            Triggered = v;
            if (v) Talk?.MarkHeard();
            if (_ring) _ring.SetActive(!v);
            if (!v && !_ring && Talk != null) BuildRing();
        }

        bool SpeakersReady()
        {
            if (Speakers.Count == 0) return false;
            foreach (var n in Speakers)
            {
                if (!n || !n.gameObject.activeInHierarchy || !n.IsAlive || n.Asleep || n.Carried) return false;
                if (n.State != AI.NpcState.Relaxed && n.State != AI.NpcState.Holding) return false;
                if (Util.FlatDistance(n.transform.position, transform.position) > Radius + 3f) return false;
            }
            return true;
        }

        bool PlayerInRange()
        {
            var p = Game.Player;
            if (!p || p.Dead) return false;
            var d = p.Feet - transform.position;
            return new Vector2(d.x, d.z).magnitude <= Radius && Mathf.Abs(d.y) < 4.5f;
        }

        void Update()
        {
            if (Triggered || Talk == null || !Game.InMission || Time.deltaTime <= 0f) return;
            if (!_linked) Resolve();
            bool inRange = PlayerInRange();
            int line = Talk.Tick(Time.deltaTime, SpeakersReady(), inRange);
            if (line >= 0)
            {
                var l = Talk.Lines[line];
                GameEvents.RaiseBark(l.Who, l.Text);
                if (Talk.Listening)
                {
                    var n = FindNpc(l.Who);
                    Game.UI?.Subtitle(n ? n.DisplayName : l.Who, l.Text, l.Dur);
                }
            }
            if (_ring) _ring.SetActive(!Triggered);
            if (Talk.Heard)
            {
                SetTriggered(true);
                Game.UI?.Toast("Overheard: " + Title);
                GameEvents.RaiseInteracted(Id);
            }
        }
    }
}
