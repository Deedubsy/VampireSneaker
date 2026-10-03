using System.Collections.Generic;
using UnityEngine;
using Pose = Vespertine.Visual.Pose;
using UnityEngine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.AI
{
    public enum NpcState
    {
        Relaxed, Suspicious, Investigating, Alerted, Searching, Panicked,
        Dazed, Mesmerised, Thrall, Dead, Victim, Distracted, Relighting, Holding,
        // M09 rescue: a shackled prisoner, a freed prisoner following Ilse, a fledgling turned loose (append only: saved as int)
        Captive, Escort, Amok
    }

    /// <summary>A human (or hound). Perception, state machine, combat and body handling. Split across partial files.</summary>
    public partial class Npc : Entity
    {
        public Archetype Arch;
        public NpcState State = NpcState.Relaxed;
        public NavMeshAgent Agent;
        public CharacterRig Rig;
        public GameLight Lantern;
        public string DisplayName;
        public bool Notable;

        // perception
        public float Detection;            // 0..1 towards the player
        public bool Wary;
        /// <summary>Story allies (Tobias): never perceive Ilse or evidence, cannot be fed upon.</summary>
        public bool Friendly;
        public bool SeesPlayer;            // this tick
        /// <summary>Dread Feast: heartbeat shown through walls until this time.</summary>
        public float RevealedUntil = -1f;
        public float LastSawPlayer = -999f;
        /// <summary>When he last heard a noise he acted on (a meter fed by it fills with waves, SR.9).</summary>
        public float HeardAt = -999f;
        /// <summary>The sense behind his meter, for its fill pattern (SR.9).</summary>
        public MeterRead.Feed MeterFeed => MeterRead.FeedOf(LastCause,
            LastSawPlayer < -900f ? float.PositiveInfinity : Time.time - LastSawPlayer,
            HeardAt < -900f ? float.PositiveInfinity : Time.time - HeardAt);
        /// <summary>A meter on her has just started (crossed <see cref="MeterRead.StartAt"/>): the edge she is past
        /// glints (SR.9).</summary>
        public static event System.Action<Npc> MeterStarted;
        public Vector3 LastKnown;          // last known player position
        public bool Asleep, Sitting;

        // health / body
        public int HP;
        public bool Hidden, Disposed, Found, Carried, Drained, Preplaced;
        public string KilledBy;            // drain / combat / hemorrhage / strike / accident
        public bool IsBody => State == NpcState.Dead || State == NpcState.Dazed;
        public bool IsAlive => State != NpcState.Dead;
        public bool Incapacitated => State == NpcState.Dead || State == NpcState.Dazed || State == NpcState.Victim || Carried || IsCorpsePuppet;
        public bool Aware => State == NpcState.Alerted || State == NpcState.Panicked;
        public bool Armed => Arch.Armed;
        public bool Ranged => Arch.Ranged;

        // routine
        RouteSpec _route;
        int _wp, _wpDir = 1;
        float _wpWait;
        bool _routeDone;
        Vector3 _post;
        float _postYaw;
        public Npc Partner;
        /// <summary>The searchlight this NPC mans (M10): he stays at it and sees whatever its pool falls on.</summary>
        public Stealth.Searchlight Beam;
        /// <summary>Where a suspicious / investigating / distracted NPC is looking.</summary>
        public Vector3 FocusPoint => _target;
        public Npc Leader;
        float _partnerMissingT;

        // state machine
        float _t;                           // time in state
        float _stateDuration;
        Vector3 _target;
        float _lookBase;
        readonly List<Vector3> _searchPts = new List<Vector3>();
        int _searchIdx;
        float _percAcc, _evidAcc;
        float _sinceSeen = 99f;
        Npc _wakeTarget;
        GameLight _relightTarget;
        Bell _bellTarget;
        float _reload, _aim, _melee;
        float _barkCd;
        float _shoutCd;
        Vector3 _dest = new Vector3(9999, 0, 9999);
        Vector3 _lastPos;
        float _stuckT;
        static int _seedCounter;

        public float StateTime => _t;

        // ------------------------------------------------------------------ spawning
        public static Npc Spawn(EntitySpec spec, bool dead = false)
        {
            var lvl = Game.Level;
            var go = new GameObject("npc_" + spec.Id);
            go.transform.SetParent(lvl.EntityRoot, false);
            go.transform.position = lvl.Data.CellToWorld(spec.X, spec.Y, lvl.SurfaceHeightCell(spec.X, spec.Y));
            float yaw = spec.Opts.TryGetValue("face", out var f) ? MapParser.ParseFacing(f) : 180f;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var n = go.AddComponent<Npc>();
            n.Preplaced = dead;
            n.Init(spec);
            lvl.Register(n);
            return n;
        }

        /// <summary>Script `route`: walk a new route from its first point (null: hold the current spot as a post).</summary>
        public void SetRoute(RouteSpec r)
        {
            _route = r; _wp = 0; _wpDir = 1; _wpWait = 0f; _routeDone = false;
            Sitting = false; Asleep = false;
            _post = transform.position; _postYaw = transform.eulerAngles.y;
        }

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            Arch = Archetypes.Get(spec.Type ?? spec.Opt("type", "civilian"));
            HP = spec.OptInt("hp", Arch.HP);
            Notable = spec.Has("notable") || Arch.Blood == BloodType.Notable;
            DisplayName = spec.Opt("name", Arch.Name)?.Replace('_', ' ');
            Wary = spec.Has("wary") || Arch.Id == "tracker";
            Friendly = spec.Has("friendly");
            Hunting = spec.Has("hunts") || spec.Opts.ContainsKey("hunts");
            Asleep = spec.Has("asleep") || spec.Has("sleep");
            Sitting = spec.Has("sit");
            _post = transform.position;
            _postYaw = transform.eulerAngles.y;
            if (spec.Opts.TryGetValue("route", out var rid) && Game.Level.Data.Routes.TryGetValue(rid, out var r)) _route = r;
            else if (spec.Opts.TryGetValue("circuit", out var cid) && Game.Level.Data.Routes.TryGetValue(cid, out var c)) _route = c;

            gameObject.layer = Layers.Character;
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = Arch.Has(ArchFlags.Quadruped) ? 0.45f : 0.38f;
            if (Arch.Has(ArchFlags.Quadruped)) { col.direction = 2; col.center = new Vector3(0, 0.5f, 0); col.height = 1.3f; }

            Rig = Arch.Has(ArchFlags.Quadruped)
                ? CharacterRig.BuildHound(transform, Arch, ++_seedCounter * 7919 + spec.Line)
                : CharacterRig.BuildHuman(transform, Arch, ++_seedCounter * 7919 + spec.Line);

            bool lantern = (Arch.Has(ArchFlags.Lantern) || spec.Has("lantern")) && !spec.Has("nolantern") && !Asleep && !Sitting;
            if (lantern) GiveLantern();

            if (Preplaced)
            {
                State = NpcState.Dead;
                Found = !spec.Has("fresh");
                KilledBy = spec.Opt("cause", "unknown");
                Drained = KilledBy == "drain";
            }
            else if (spec.Has("prisoner")) InitPrisoner(spec);
            // evac=x,y: a worker who, once frightened (or told to by a whistle), runs off-site there and is gone
            var ev = spec.Opt("evac");
            if (!string.IsNullOrEmpty(ev))
            {
                var c = ev.Split(',');
                if (c.Length >= 2 && float.TryParse(c[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ex)
                    && float.TryParse(c[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ey))
                    EvacTo = Game.Level.Data.CellToWorld(ex, ey, Game.Level.SurfaceHeightCell(ex, ey));
            }
        }

        /// <summary>Where this NPC runs to leave the level when panicked (null = flees as usual and comes back).</summary>
        public Vector3? EvacTo;

        /// <summary>Send an evac NPC running for the exit (a works whistle, a script) without a scream.</summary>
        public void Evacuate()
        {
            if (!EvacTo.HasValue || !IsAlive || Incapacitated || State == NpcState.Thrall || Rescue || !gameObject.activeInHierarchy) return;
            EnterPanic(transform.position, true);
        }

        void GiveLantern()
        {
            var lgo = new GameObject("lantern");
            lgo.transform.SetParent(transform, false);
            lgo.transform.localPosition = new Vector3(0.32f, 0, 0.32f);
            Lantern = lgo.AddComponent<GameLight>();
            Lantern.Id = Id + "_lantern";
            Lantern.Setup(LightKind.Lantern);
            Lantern.Snuffable = false;
            Lantern.BuildVisual(Vector3.zero);
        }

        public override void Link()
        {
            var p = Spec.Opt("paired");
            if (p != null) Partner = Game.Level.Get<Npc>(p);
            // follow=<npc>: walks at heel (a hound and its handler). The leader going missing is noticed like a partner.
            var f = Spec.Opt("follow");
            if (f != null) { Leader = Game.Level.Get<Npc>(f); if (Partner == null) Partner = Leader; }
        }

        void Start()
        {
            Agent = gameObject.AddComponent<NavMeshAgent>();
            Agent.agentTypeID = NavAreas.HumanAgent;
            Agent.areaMask = NavAreas.HumanMask;
            Agent.radius = 0.35f;
            Agent.height = 1.8f;
            Agent.speed = Arch.WalkSpeed;
            Agent.acceleration = 14f;
            Agent.angularSpeed = 420f;
            Agent.stoppingDistance = 0.1f;
            Agent.autoBraking = true;
            Agent.autoTraverseOffMeshLink = true;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            Agent.avoidancePriority = 30 + Random.Range(0, 40);
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, filter)) Agent.Warp(hit.position);
            else Debug.LogWarning($"[Npc] {Id} not on navmesh at {transform.position}");
            _post = transform.position;
            _lastPos = transform.position;
            _percAcc = Random.value * 0.1f;
            _evidAcc = Random.value * 0.5f;
            if (Game.AI != null) Game.AI.Register(this);
            if (State == NpcState.Dead) EnterDeadVisuals();
            else if (Spec.Has("dazed")) { State = NpcState.Relaxed; Daze(Spec.OptFloat("dazed", 60f), false); }
        }

        void OnDestroy() { Game.AI?.Unregister(this); }

        // ------------------------------------------------------------------ vision
        /// <summary>Current vision parameters (archetype, difficulty, wariness, state and countermeasures).</summary>
        public VisionParams Vision
        {
            get
            {
                var v = VisionParams.Default;
                v.HalfAngle = Arch.Fov * 0.5f;
                v.NearRange = Arch.Near;
                v.FarRange = Arch.Far;
                v.LooksUp = Arch.Has(ArchFlags.LooksUp) || (Game.AI != null && Game.AI.EveryoneLooksUp && Arch.Faction != Faction.None);
                if (Wary) { v.NearRange *= 1.15f; v.FarRange *= 1.1f; }
                if (State == NpcState.Alerted || State == NpcState.Searching) { v.NearRange *= 1.2f; v.HalfAngle = Mathf.Min(180f, v.HalfAngle + 10f); }
                // back to back: each man watches a wide arc, and a scared squad sees further into the dark
                if (Squad != null && Squad.Ringing && !Routed) { v.HalfAngle = Mathf.Min(180f, v.HalfAngle + 35f); v.FarRange *= 1.1f; }
                if (Game.AI != null && Game.AI.Blinded(this)) { v.NearRange = 0.8f; v.FarRange = 0.8f; }
                return v;
            }
        }

        /// <summary>Guests, servants and the Watch take a masked Ilse for a guest, until the house is roused. The Vigil, the
        /// Church and anyone with a Ward look closer.</summary>
        public bool FooledByMask => (Arch.Faction == Faction.None || Arch.Faction == Faction.Watch) && !Arch.Has(ArchFlags.Ward)
                                    && State != NpcState.Alerted && State != NpcState.Searching;

        public bool CanSee => State != NpcState.Dead && State != NpcState.Dazed && State != NpcState.Victim && State != NpcState.Mesmerised
                              && State != NpcState.Thrall && !Rescue && !Carried && !Asleep && !Hidden && !IsCorpsePuppet;

        public Vector3 Eye => transform.position + Vector3.up * (Arch.Has(ArchFlags.Quadruped) ? 0.7f : 1.6f);
        public Vector3 Forward => transform.forward;

        /// <summary>Line of sight from this NPC's eye to a point (walls and foliage block).</summary>
        public bool LineOfSight(Vector3 point, bool foliageBlocks = true)
        {
            int mask = foliageBlocks ? Layers.VisionBlockMask : Layers.SightMask;
            return !Physics.Linecast(Eye, point, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// The band he has a target at <paramref name="feet"/> in, judged by <paramref name="light"/> (the far band's
        /// light: <see cref="Player.Vampire.SightLight"/>), with line of sight to her body at 1.0 or 1.6 m (leaves don't
        /// hide her at touch range). None if he can't see her there. <see cref="Perceive"/> and the toe of her disc (SR.8)
        /// both ask this, so what the toe predicts is what he judges.
        /// </summary>
        public DetectionMath.Band SeenBand(Vector3 feet, float light)
        {
            if (!CanSee) return DetectionMath.Band.None;
            var band = DetectionMath.Classify(Vision, transform.position, Forward, feet, light);
            if (band == DetectionMath.Band.None) return band;
            bool leaves = band != DetectionMath.Band.Peripheral;
            return LineOfSight(feet + Vector3.up * 1.0f, leaves) || LineOfSight(feet + Vector3.up * 1.6f, leaves) ? band : DetectionMath.Band.None;
        }

        /// <summary>Takes her for someone else, or misses her: a masked guest to a guard the masque fools, or Stalker behind
        /// his back. He has her in a band but his meter doesn't move.</summary>
        public bool Overlooks(Player.Vampire p, Vector3 feet, float d) =>
            p.MaskHolds && FooledByMask
            || !p.Rushing && d < 4f && Vector3.Angle(Forward, (feet - transform.position).Flat()) > 100f && Game.Campaign != null && Game.Campaign.Has("predator.stalker");

        /// <summary>Would this NPC see a target at feet position with given light (cone + LOS), ignoring the meter.</summary>
        public bool CouldSee(Vector3 feet, float light)
        {
            if (!CanSee) return false;
            var band = DetectionMath.Classify(Vision, transform.position, Forward, feet, light);
            if (band == DetectionMath.Band.None) return false;
            return LineOfSight(feet + Vector3.up * 0.9f, band != DetectionMath.Band.Peripheral);
        }

        /// <summary>What filled the meter last (band, distance, light, modifiers): the Spotted caption reads it (SR.10).</summary>
        public DetectionMath.SightCause LastCause;
        /// <summary>She is in his cone's grace fringe this tick (D138): the cone and her watcher tick draw it dashed.</summary>
        public bool InGrace;
        /// <summary>He has her in a band with line of sight this tick, the sighting onset included (her watcher tick, SR.7).
        /// <see cref="SeesPlayer"/> waits for the onset.</summary>
        public bool InSight;
        float _onset, _unseenFor;

        void Perceive(float dt)
        {
            SeesPlayer = false;
            InGrace = false;
            InSight = false;
            var p = Game.Player;
            // a hound whose handler is Ilse's thrall answers to her now
            if (p == null || p.Dead || !CanSee || Friendly || Vespertine.Player.Vampire.NoTarget || (Leader && Leader.IsThrall))
            {
                Detection = DetectionMath.Step(Detection, 0, dt, _sinceSeen += dt);
                DetectionMath.Onset(ref _onset, ref _unseenFor, 0f, dt);
                return;
            }
            float rate = 0f, sight = 0f, sightLight = 0f;
            var sightBand = DetectionMath.Band.None;
            var feet = p.transform.position;
            float d = Util.FlatDistance(feet, transform.position);
            if (!p.Concealed && d < Arch.Far * 1.6f + 2f)
            {
                var v = Vision;
                float light = p.SightLight;
                var band = SeenBand(feet, light);
                if (band != DetectionMath.Band.None)
                {
                    float mul = Difficulties.Current.Detection * p.VisibilityMul;
                    if (Wary) mul *= 1.25f;
                    if (State == NpcState.Alerted || State == NpcState.Searching) mul *= 1.6f;
                    if (State == NpcState.Panicked) mul *= 0.5f;
                    // the masque (a masked woman behaving like a guest is just another guest), and Stalker
                    if (Overlooks(p, feet, d)) mul = 0f;
                    sight = DetectionMath.Rate(v, transform.position, Forward, feet, light, mul);
                    sightBand = band;
                    sightLight = light;
                    InSight = sight > 0f;
                    // the cone's outer edge forgives (D138): half rate in the fringe
                    if (sight > 0f && DetectionMath.InGrace(v, transform.position, Forward, feet, light, Difficulties.Current.Grace))
                    {
                        sight *= DetectionMath.GraceRate;
                        InGrace = true;
                    }
                }
            }
            // the first quarter second of a sighting doesn't count; touch range always does
            float counted = DetectionMath.Onset(ref _onset, ref _unseenFor, sight, dt);
            rate = sightBand == DetectionMath.Band.Peripheral ? sight : counted;
            if (rate > 0f) Cause(sightBand, d, sightLight, Vision, false, false);
            // a searchlight operator sees whatever his pool falls on, at any range
            if (Beam != null && Beam.Covers(feet) && Beam.Manned && !p.Concealed && !p.InMist && !(p.MaskHolds && FooledByMask)
                && (LineOfSight(feet + Vector3.up * 1.0f, false) || LineOfSight(feet + Vector3.up * 1.6f, false)))
            {
                float beam = 1.6f * Difficulties.Current.Detection * p.VisibilityMul;
                if (beam > rate) Cause(DetectionMath.Band.Far, d, p.Light, Vision, true, false);
                rate = Mathf.Max(rate, beam);
            }
            // smell: hounds / trackers notice the vampire within 5 m regardless of light or angle (half that in rain)
            if (Arch.Has(ArchFlags.Smell) && d < 5f * (Weather.Raining ? Weather.SmellMul : 1f) && Mathf.Abs(feet.y - transform.position.y) < 2.5f && !p.InMist && !p.Concealed)
            {
                float smell = 1.2f * Difficulties.Current.Detection;
                if (smell > rate) Cause(DetectionMath.Band.None, d, 0f, Vision, false, true);
                rate = Mathf.Max(rate, smell);
                if (rate > 0 && Random.value < 0.05f) Say(BarkKind.Smell);
            }
            if (rate > 0f)
            {
                SeesPlayer = true;
                _sinceSeen = 0f;
                LastSawPlayer = Time.time;
                LastKnown = feet;
            }
            else _sinceSeen += dt;
            float before = Detection;
            Detection = DetectionMath.Step(Detection, rate, dt, _sinceSeen, Wary ? 0.15f : 0.22f);
            if (rate > 0f && MeterRead.Started(before, Detection)) MeterStarted?.Invoke(this);
            if (rate > 0f) OnSeePlayer(before);
        }

        void Cause(DetectionMath.Band band, float d, float light, in VisionParams v, bool beam, bool smell)
        {
            var p = Game.Player;
            LastCause = new DetectionMath.SightCause
            {
                Band = band, Distance = d, Light = light, NearRange = v.NearRange, Threshold = v.LitThreshold,
                Searchlight = beam, Smell = smell,
                Running = p.Rushing, Carrying = p.Carrying != null, Feeding = p.Feeding != null,
                Wary = Wary, Hunting = State == NpcState.Alerted || State == NpcState.Searching,
            };
        }

        /// <summary>The Spotted caption for the last thing that filled this meter, naming the lamp that lit her.</summary>
        public string ExplainSpotted()
        {
            var c = LastCause;
            if (c.Band == DetectionMath.Band.Far && !c.Searchlight && !c.Smell && Game.Lights != null && Game.Player != null)
            {
                var l = Game.Lights.Brightest(Game.Player.transform.position);
                if (l) c.Lamp = GameLight.KindName(l.Kind);
            }
            return DetectionMath.Explain(c);
        }

        /// <summary>A blur in the light (Shadow Dash, D155): his meter jumps by <paramref name="bump"/>, and he knows where.</summary>
        public void Glimpse(Vector3 feet, float bump)
        {
            float before = Detection;
            Detection = Mathf.Min(1f, Detection + bump);
            LastKnown = feet;
            LastSawPlayer = Time.time;
            if (MeterRead.Started(before, Detection)) MeterStarted?.Invoke(this);
            OnSeePlayer(before);
        }

        void OnSeePlayer(float before)
        {
            if (Detection >= DetectionMath.SpottedAt)
            {
                if (State != NpcState.Alerted && State != NpcState.Panicked)
                {
                    GameEvents.RaiseSpotted(this);
                    Spot(LastKnown);
                }
                return;
            }
            if (Detection >= DetectionMath.SuspiciousAt && (State == NpcState.Relaxed || State == NpcState.Distracted || State == NpcState.Relighting || State == NpcState.Holding))
                EnterSuspicious(LastKnown);
            else if (before < 0.05f && Detection >= 0.05f && Game.Audio != null && State == NpcState.Relaxed)
                Game.Audio.PlayAt("huh", Eye, 0.25f, Random.Range(0.9f, 1.15f), 14f);
        }

        // ------------------------------------------------------------------ blood trails
        static readonly List<Vector3> _trailPos = new List<Vector3>();
        static readonly List<int> _trailSeq = new List<int>();
        static readonly List<BloodStain> _trail = new List<BloodStain>();

        /// <summary>Hound on a trail: start from the freshest drop it can smell, follow the drops forward a few steps
        /// and go there. Drops it passes are spent, so it never doubles back; at the next stop it smells on.</summary>
        void TrackBloodTrail()
        {
            _trail.Clear(); _trailPos.Clear(); _trailSeq.Clear();
            int start = -1;
            foreach (var x in Evidence.Stains)
            {
                if (!x || !x.IsTrail || x.Found) continue;
                _trail.Add(x); _trailPos.Add(x.transform.position); _trailSeq.Add(x.Seq);
                int i = _trail.Count - 1;
                if (Util.FlatDistance(_trailPos[i], transform.position) <= 8f && (start < 0 || _trailSeq[i] > _trailSeq[start])) start = i;
            }
            if (start < 0) return;
            int end = Evidence.FollowTrail(_trailPos, _trailSeq, start, 4.5f, 5);
            int endSeq = _trailSeq[end];
            for (int i = 0; i < _trail.Count; i++)
                if (_trailSeq[i] <= endSeq && Util.FlatDistance(_trailPos[i], transform.position) < 24f) _trail[i].Found = true;
            bool fresh = Time.time - _trail[end].Born < 40f;
            if (State != NpcState.Investigating && State != NpcState.Searching)
            {
                Say(BarkKind.Smell, true);
                GameEvents.RaiseEvidence(this, "trail");
            }
            Wary = true;
            if (State == NpcState.Searching) { _target = _trailPos[end]; return; }
            EnterInvestigating(_trailPos[end], fresh, true);
        }

        // ------------------------------------------------------------------ evidence
        void ScanEvidence()
        {
            if (!CanSee || Friendly || State == NpcState.Alerted || State == NpcState.Panicked) return;
            var ai = Game.AI;
            if (ai == null) return;
            var v = Vision;
            if (ai.RescuesLoose && !Rescue && !IsThrall && ScanRescues()) return;
            // bodies
            foreach (var o in ai.Npcs)
            {
                if (o == this || !o.IsBody || o.Found || o.Hidden || o.Disposed || o.Carried || !o.gameObject.activeInHierarchy) continue;
                var bp = o.transform.position;
                float d = Util.FlatDistance(bp, transform.position);
                if (d > v.FarRange) continue;
                float light = Game.Lights != null ? Game.Lights.LightAt(bp, 0.3f) : 0.5f;
                if (Game.Level != null && Game.Level.InFoliage(bp)) light *= 0.2f;
                float range = light >= v.LitThreshold ? v.FarRange : v.NearRange * 1.2f;
                if (d > range) continue;
                if (d > v.Peripheral && Vector3.Angle(Forward, (bp - transform.position).Flat()) > v.HalfAngle) continue;
                if (bp.y - transform.position.y > DetectionMath.HighTarget && !v.LooksUp) continue;
                if (!LineOfSight(bp + Vector3.up * 0.3f)) continue;
                FindBody(o);
                return;
            }
            // thralls exposed by inquisitors / Vane
            if (Arch.Has(ArchFlags.Ward) && Arch.Has(ArchFlags.Officer))
                foreach (var o in ai.Npcs)
                {
                    bool puppetPasses = Game.Campaign != null && Game.Campaign.Has("dominion.living_lie");
                    if (!(o.State == NpcState.Thrall && !o.IsCorpsePuppet) && !(o.IsCorpsePuppet && !puppetPasses)) continue;
                    float d = Util.FlatDistance(o.transform.position, transform.position);
                    if (d < v.NearRange && Vector3.Angle(Forward, (o.transform.position - transform.position).Flat()) < v.HalfAngle && LineOfSight(o.Eye))
                    {
                        ExposeThrall(o);
                        return;
                    }
                }
            // stains
            bool nose = Arch.Has(ArchFlags.Smell);
            foreach (var s in Evidence.Stains)
            {
                if (!s || s.Found) continue;
                var sp = s.transform.position;
                float d = Util.FlatDistance(sp, transform.position);
                if (s.IsTrail && nose)
                {
                    // a hound smells a trail drop in any light, any direction, through a fence
                    if (d > (Weather.Raining ? 5f : 8f) || Mathf.Abs(sp.y - transform.position.y) > 3f) continue;
                    TrackBloodTrail();
                    return;
                }
                float light = Game.Lights != null ? Game.Lights.LightAt(sp, 0.2f) : 0.5f;
                float range = light >= v.LitThreshold ? Mathf.Min(v.FarRange, s.Huge ? 16f : 9f) : (s.Huge ? 6f : 3f);
                if (s.IsTrail) range *= 0.6f;   // a drop, not a pool
                if (d > range) continue;
                if (Vector3.Angle(Forward, (sp - transform.position).Flat()) > v.HalfAngle) continue;
                if (!LineOfSight(sp + Vector3.up * 0.1f)) continue;
                s.Found = true;
                if (s.Huge) { Say(BarkKind.Body); GameEvents.RaiseEvidence(this, "pool"); EnterSearching(sp, true); ai.ReportBody(this, null); }
                else { Say(BarkKind.Stain); GameEvents.RaiseEvidence(this, "stain"); EnterInvestigating(sp, false, true); }
                return;
            }
            // lamps put out by the player
            if (Game.Lights == null) return;
            foreach (var l in Game.Lights.All)
            {
                if (!l || l.On || !l.WasSnuffedByPlayer || l.Portable || ai.LampNoticed(l)) continue;
                var lp = l.transform.position;
                float d = Util.FlatDistance(lp, transform.position);
                if (d > Mathf.Min(v.FarRange, 16f)) continue;
                if (Vector3.Angle(Forward, (lp - transform.position).Flat()) > v.HalfAngle + 15f) continue;
                if (!LineOfSight(l.SourcePos)) continue;
                ai.NoticeLamp(l, this);
                Say(BarkKind.Lamp);
                GameEvents.RaiseEvidence(this, "lamp");
                if (Arch.Has(ArchFlags.Relights) || CarriesTaper) BeginRelight(l);
                else EnterSuspicious(lp, 3f);
                return;
            }
        }

        static readonly string[] AccidentLines =
        {
            "Dear God. He's fallen.", "Someone fetch a doctor. No... too late.", "The rail. I always said that rail would go.",
            "Drunk as a lord, and now look.", "Don't touch him. Fetch the steward.",
        };

        /// <summary>Inquisitors, Hollin and Vane (warded officers) read what she leaves behind.</summary>
        public bool Examines => Arch.Has(ArchFlags.Ward) && Arch.Has(ArchFlags.Officer);

        /// <summary>What an examiner learns from a body (E5): the Dossier habit it feeds, or null when it tells them
        /// nothing of her (a fledgling's work, a debug kill). A staged accident does not fool them. Pure; unit-tested.</summary>
        public static string ExamineHabit(string cause, bool dazed)
        {
            if (dazed) return Progression.Habits.Sips;
            switch (cause)
            {
                case "hemorrhage": case "snare": return Progression.Habits.Blood;
                case "drain": case "combat": case "strike": case "rend": case "accident": case "blast": case "drowned": return Progression.Habits.Lethal;
            }
            return null;
        }

        static readonly string[] ExamineLines =
        {
            "Two punctures. Clean. She took her time.", "No struggle. She had him calm before she bit.",
            "Look at the throat. Write it down.", "Pale as wax. That was no fall.",
        };

        void Examine(Npc body)
        {
            var habit = ExamineHabit(body.KilledBy, body.State == NpcState.Dazed);
            if (habit == null) return;
            Game.Campaign?.AddHabit(habit, 1f);
            GameEvents.RaiseBark(Id, ExamineLines[Random.Range(0, ExamineLines.Length)]);
            GameEvents.RaiseToast("The Vigil has read the body. The Dossier grows.");
        }

        void FindBody(Npc body)
        {
            body.Found = true;
            bool reads = Examines;
            if (reads) Examine(body);
            if (body.State == NpcState.Dazed)
            {
                Say(BarkKind.Dazed);
                GameEvents.RaiseEvidence(this, "dazed");
                _wakeTarget = body;
                EnterInvestigating(body.transform.position, true, false);
            }
            else if ((body.KilledBy == "accident" || body.KilledBy == "blast") && !reads)
            {
                // a fall, a slip, too much wine: grief and gossip, not an alarm
                GameEvents.RaiseBark(Id, AccidentLines[Random.Range(0, AccidentLines.Length)]);
                GameEvents.RaiseEvidence(this, "accident");
                Game.Audio?.PlayAt("gasp", Eye, 0.8f);
                EnterInvestigating(body.transform.position, false, false);
            }
            else
            {
                Say(BarkKind.Body);
                GameEvents.RaiseEvidence(this, "corpse");
                Game.Audio?.PlayAt("gasp", Eye, 0.8f);
                Wary = true;
                Game.AI?.ReportBody(this, body);
                if (Arch.Morale == Morale.Civilian) EnterPanic(body.transform.position);
                else EnterSearching(body.transform.position, true);
            }
        }

        // ------------------------------------------------------------------ hearing (called by AIDirector)
        public void Hear(Vector3 pos, float radius, NoiseKind kind, object source)
        {
            if (!IsAlive || Rescue || State == NpcState.Victim || State == NpcState.Thrall || State == NpcState.Mesmerised || State == NpcState.Dazed || Carried || IsCorpsePuppet) return;
            if (source is Npc sn && sn == this) return;
            if (Friendly) return;
            if (Asleep)
            {
                if (kind == NoiseKind.Footstep || kind == NoiseKind.Heartbeat) return;
                WakeFromSleep();
            }
            HeardAt = Time.time;
            switch (kind)
            {
                case NoiseKind.Gunshot:
                case NoiseKind.Scream:
                case NoiseKind.Bell:
                    if (State == NpcState.Alerted || State == NpcState.Panicked) return;
                    if (Arch.Morale == Morale.Civilian && kind != NoiseKind.Bell) { EnterPanic(pos); return; }
                    Wary = true;
                    EnterSearching(pos, true);
                    return;
                case NoiseKind.Lure:
                    if (State == NpcState.Relaxed || State == NpcState.Suspicious || State == NpcState.Distracted || State == NpcState.Holding)
                        EnterDistracted(pos, 6f, source as Npc == null);
                    return;
                case NoiseKind.Footstep:
                case NoiseKind.Rush:
                case NoiseKind.Heartbeat:
                    if (State == NpcState.Alerted || State == NpcState.Searching)
                    {
                        if (State == NpcState.Searching) { LastKnown = pos; _target = pos; }
                        return;
                    }
                    Detection = Mathf.Max(Detection, DetectionMath.SuspiciousAt + 0.05f);
                    EnterSuspicious(pos, kind == NoiseKind.Rush ? 1.2f : 2.5f);
                    return;
                default: // body, object, splash, door, voice
                    if (State == NpcState.Alerted || State == NpcState.Panicked) return;
                    if (State == NpcState.Searching) { _target = pos; return; }
                    if (kind == NoiseKind.Voice && State == NpcState.Relaxed) { EnterSuspicious(pos, 2f); return; }
                    EnterInvestigating(pos, false, true);
                    return;
            }
        }

        // ------------------------------------------------------------------ main loop
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _t += dt;
            _barkCd -= dt;
            _shoutCd -= dt;

            if (State != NpcState.Dead && !Carried)
            {
                _percAcc += dt;
                if (_percAcc >= 0.1f) { Perceive(_percAcc); _percAcc = 0f; }
                _evidAcc += dt;
                if (_evidAcc >= 0.5f) { _evidAcc = 0f; ScanEvidence(); }
            }

            Tick(dt);
            EndGlanceIfIdle();
            UpdatePose();
            TickSteps(dt);

            // censer smoke: the counter has to be visible before it bites
            _censerT -= dt;
            if (_censerT <= 0f)
            {
                _censerT = 0.7f;
                if (CarriesCenser && gameObject.activeInHierarchy)
                    Fx.Smoke(transform.position + Vector3.up * 0.9f + transform.right * 0.35f, new Color(0.62f, 0.66f, 0.55f, 0.5f), 1.1f, 2f);
            }
        }
        float _censerT;

        // ------------------------------------------------------------------ footsteps
        /// <summary>How far from the camera's focus a footfall is still worth playing.</summary>
        public const float StepHearing = 20f;
        float _stepT;
        int _stepN;

        /// <summary>The footstep clip family for a surface (three variants each), or null for a silent one. Pure; unit-tested.</summary>
        public static string StepClip(Surface s, bool quadruped)
        {
            if (quadruped) return s == Surface.Water ? "step_water" : s == Surface.Carpet ? null : "step_dirt";
            switch (s)
            {
                case Surface.Wood: return "step_wood";
                case Surface.Water: return "step_water";
                case Surface.Dirt: case Surface.Carpet: return "step_dirt";
            }
            return "step_stone";
        }

        /// <summary>Seconds between footfalls at a speed: a walking man about two a second, a runner three, a hound faster. Pure; unit-tested.</summary>
        public static float StepInterval(float speed, bool quadruped)
        {
            float t = speed > 2.4f ? 0.32f : Mathf.Lerp(0.62f, 0.46f, Mathf.InverseLerp(0.3f, 2.4f, speed));
            return quadruped ? t * 0.6f : t;
        }

        /// <summary>A patrol is heard before it is seen: walkers near the camera's focus make footsteps by surface,
        /// louder when running, and a Bulwark's armour clinks.</summary>
        void TickSteps(float dt)
        {
            var au = Game.Audio;
            float spd = Speed;
            if (au == null || spd < 0.3f || Carried || !IsAlive || Incapacitated) { _stepT = Mathf.Min(_stepT, 0.15f); return; }
            _stepT -= dt;
            if (_stepT > 0f) return;
            bool quad = Arch.Has(ArchFlags.Quadruped);
            _stepT = StepInterval(spd, quad);
            var feet = transform.position;
            if (Game.Cam && Util.FlatDistance(feet, Game.Cam.Pivot) > StepHearing) return;
            var clip = StepClip(Game.Level != null ? Game.Level.SurfaceAt(feet) : Surface.Stone, quad);
            if (clip == null) return;
            bool armoured = Arch.Has(ArchFlags.Armored);
            float vol = (spd > 2.4f ? 0.3f : 0.17f) * (quad ? 0.55f : 1f) * (armoured ? 1.5f : 1f);
            au.PlayVariant(clip, 3, feet, vol, 18f);
            if (armoured && (_stepN++ & 1) == 0) au.PlayAt("chain", feet + Vector3.up, 0.07f, 0.65f, 16f);
        }

        void UpdatePose()
        {
            if (!Rig) return;
            float spd = Speed;
            Rig.Speed = spd;
            Pose pose;
            switch (State)
            {
                case NpcState.Dead: pose = Carried ? Pose.Carried : Pose.Lying; break;
                case NpcState.Dazed: pose = Carried ? Pose.Carried : Pose.Lying; break;
                case NpcState.Victim: pose = Pose.Victim; break;
                case NpcState.Mesmerised: pose = Pose.Mesmerised; break;
                case NpcState.Relighting: pose = spd > 0.2f ? Pose.Walk : Pose.Interact; break;
                case NpcState.Panicked: pose = spd > 0.2f ? Pose.Panic : Pose.Stand; break;
                case NpcState.Captive: pose = Sitting ? Pose.Sit : Pose.Stand; break;
                case NpcState.Amok: pose = spd > 0.2f ? Pose.Run : Pose.Feed; break;
                case NpcState.Alerted: pose = _aim > 0f ? Pose.Aim : spd > 2.4f ? Pose.Run : spd > 0.2f ? Pose.Walk : Pose.Stand; break;
                default:
                    if (Asleep) pose = Pose.Sleep;
                    else if (Sitting && spd < 0.2f && State == NpcState.Relaxed) pose = Pose.Sit;
                    else pose = spd > 2.4f ? Pose.Run : spd > 0.2f ? Pose.Walk : Pose.Stand;
                    break;
            }
            Rig.Pose = pose;
        }

        // ------------------------------------------------------------------ movement helpers
        NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavAreas.HumanMask };

        public bool MoveTo(Vector3 p, bool run, float speedMul = 1f)
        {
            if (!Agent || !Agent.enabled || !Agent.isOnNavMesh) return false;
            // a searchlight operator keeps to his lamp while it burns (he turns it on things instead)
            if (Beam != null && Beam.Lit && Beam.Manned && State != NpcState.Panicked) { Halt(); return false; }
            float spd = run ? Arch.RunSpeed : Arch.WalkSpeed * (Wary ? 1.1f : 1f);
            // a squad leader walks a little slow so his men can hold the wedge
            if (!run && Squad != null && Slot == 0 && !Squad.Broken) speedMul *= 0.85f;
            Agent.speed = spd * speedMul * (Game.AI != null ? Game.AI.TimeScaleFor(this) : 1f);
            if ((p - _dest).sqrMagnitude > 0.09f || (!Agent.hasPath && !Agent.pathPending))
            {
                if (!NavMesh.SamplePosition(p, out var hit, 3f, Filter)) return false;
                _dest = p;
                Agent.SetDestination(hit.position);
            }
            Agent.isStopped = false;
            return true;
        }

        public bool Arrived
        {
            get
            {
                if (!Agent || !Agent.enabled || !Agent.isOnNavMesh) return true;
                if (Agent.pathPending) return false;
                if (!Agent.hasPath) return true;
                return Agent.remainingDistance <= Agent.stoppingDistance + 0.3f;
            }
        }

        public void Halt()
        {
            _dest = new Vector3(9999, 0, 9999);
            if (Agent && Agent.enabled && Agent.isOnNavMesh) { Agent.isStopped = true; Agent.ResetPath(); }
        }

        void FaceTowards(Vector3 worldPoint, float degPerSec = 360f)
        {
            var dir = (worldPoint - transform.position).Flat();
            if (dir.sqrMagnitude < 0.01f) return;
            FaceYaw(Util.DirToFacing(dir), degPerSec);
        }

        void FaceYaw(float yaw, float degPerSec = 360f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, yaw, 0), degPerSec * Time.deltaTime);
        }

        /// <summary>Below this an agent with a path is not really moving. The slowest honest walk (1.1 m/s, slowed to 35%
        /// by Dread, at a squad leader's pace) is about 0.33 m/s.</summary>
        public const float StuckSpeed = 0.12f;

        /// <summary>
        /// Whether a frame's movement counts toward being stuck. A speed, not a distance per frame: a fixed 2 cm per frame
        /// meant every walking npc was "stuck" above ~65 fps, and gave up relighting, investigating, etc. after 3 s.
        /// </summary>
        public static bool Crawling(float moved, float dt) => moved < StuckSpeed * dt;

        bool Stuck(float dt)
        {
            if (Crawling((transform.position - _lastPos).magnitude, dt) && Agent && Agent.hasPath && !Agent.isStopped) _stuckT += dt;
            else _stuckT = 0f;
            _lastPos = transform.position;
            return _stuckT > 3f;
        }

        public void Say(BarkKind k, bool force = false)
        {
            if (!force && _barkCd > 0f) return;
            _barkCd = 4f;
            GameEvents.RaiseBark(Id, Barks.Get(k, Arch));
            if (Game.Audio != null && !Arch.Has(ArchFlags.Quadruped))
                Game.Audio.PlayAt(k == BarkKind.Spotted || k == BarkKind.Body || k == BarkKind.Lockdown ? "shout" : k == BarkKind.Panic ? "scream" : "whisper",
                    Eye, k == BarkKind.Panic ? 0.7f : 0.45f, Random.Range(0.85f, 1.15f), 22f);
            else if (Game.Audio != null) Game.Audio.PlayAt(k == BarkKind.Spotted ? "bark" : "growl", Eye, 0.6f, Random.Range(0.9f, 1.1f), 25f);
        }
    }
}
