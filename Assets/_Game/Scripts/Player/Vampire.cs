using System.Collections.Generic;
using UnityEngine;
using Pose = Vespertine.Visual.Pose;
using UnityEngine.AI;
using Vespertine.AI;
using Vespertine.Controls;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.Player
{
    public enum ActionKind { None, Move, Feed, Carry, Interact, Snuff, Ability }

    /// <summary>One queued intention for Ilse (the keys, the cursor and dev scripts share this).</summary>
    public class PlayerAction
    {
        public ActionKind Kind;
        public Vector3 Point;
        public bool Rush;
        public Npc Npc;
        public Interactable Use;
        public GameLight Light;
        public string Ability;
        public bool Drain;
        public Vector3 Point2;

        public string Describe()
        {
            switch (Kind)
            {
                case ActionKind.Move: return Rush ? "Rush" : "Glide";
                case ActionKind.Feed: return (Drain ? "Drain " : "Sip ") + (Npc ? Npc.DisplayName : "");
                case ActionKind.Carry: return "Carry body";
                case ActionKind.Interact: return Use ? Use.Verb : "Use";
                case ActionKind.Snuff: return "Snuff light";
                case ActionKind.Ability: return Skills.Ability(Ability)?.Name ?? Ability;
            }
            return "";
        }
    }

    /// <summary>Ilse: the player character. Movement (with climb / leap / mist link traversal), blood and health,
    /// feeding, bodies, hiding, interaction, abilities and thralls. Split across partial files.</summary>
    public partial class Vampire : Entity
    {
        public const float GlideSpeed = 3.4f, RushSpeed = 6.8f, SneakSpeed = 2.2f, FeedRange = 1.35f, UseSlack = 0.35f;

        public NavMeshAgent Agent;
        public CharacterRig Rig;

        // ---------------------------------------------------------------- state visible to the simulation
        public float HP, Blood, BonusHP;
        public bool Dead;
        public bool Concealed;            // inside a hiding spot
        public float Light;               // light level at feet (0..1+)
        /// <summary>The light a guard's far band judges her by: <see cref="Light"/>, and a quarter of it among leaves.
        /// Her ground disc reads exposed exactly when this reaches <see cref="DetectionMath.ExposedAt"/> (SR.7).</summary>
        public float SightLight;
        public float VisibilityMul = 1f;  // motion / form multiplier used by NPC perception
        public bool Rushing;
        public bool InMist;
        public Npc Carrying;
        public Npc Feeding;
        public HideSpot InsideSpot;
        public readonly List<string> Items = new List<string>();

        // ---------------------------------------------------------------- humours (blood quality buffs)
        public string Humour;             // languid, fever, iron, scalding, lucid
        public float HumourUntil;

        public float MaxHP => (Game.Campaign != null ? Game.Campaign.MaxHP : 40f) * (Humour == "iron" ? 1.25f : 1f);
        public float MaxBlood => Game.Campaign != null ? Game.Campaign.MaxBlood : 60f;
        public int Awakening => Game.Campaign != null ? Game.Campaign.Awakening : 1;
        public bool Starving => Blood < MaxBlood * 0.15f;
        public bool Has(string node) => Game.Campaign != null && Game.Campaign.Has(node);
        public bool InDark => Light < DetectionMath.SuspiciousAt;
        /// <summary>The light guards judge from her light at a spot (<see cref="Light"/>, already dimmed among leaves): a
        /// quarter of it among leaves. Pure.</summary>
        public static float JudgedLight(float light, bool leaves) => leaves ? light * 0.25f : light;
        public bool Busy => Feeding != null || _useTarget != null || _link != null || _channel > 0f;
        public bool Moving => DirectMoving || Agent && Agent.enabled && Agent.isOnNavMesh && Agent.hasPath && Agent.remainingDistance > Agent.stoppingDistance + 0.05f;
        public Vector3 Feet => transform.position;
        public PlayerAction Pending => _pending;

        // ---------------------------------------------------------------- the masque disguise (item "mask")
        public bool Masked => Items.Contains("mask");
        /// <summary>Masked and behaving like a guest: civilians and the Watch take her for one.</summary>
        public bool MaskHolds => Masked && MaskFault == null;
        /// <summary>What gives a masked guest away right now (null while she passes).</summary>
        public string MaskFault
        {
            get
            {
                if (_maskFrame == Time.frameCount) return _maskFault;
                _maskFrame = Time.frameCount;
                _maskFault = ComputeMaskFault();
                return _maskFault;
            }
        }
        int _maskFrame = -1; string _maskFault;

        /// <summary>Standing on a gallery, terrace or landing (where guests walk too), not on a wall top or a roof.</summary>
        bool OnUpperFloor()
        {
            var lvl = Game.Level;
            if (lvl == null) return false;
            var c = lvl.Data.WorldToCellInt(Feet);
            var k = lvl.Grid.Kind(c.x, c.y);
            return k == TileKind.Gallery || k == TileKind.StairsUp;
        }

        string ComputeMaskFault()
        {
            if (!Masked) return "unmasked";
            if (Feeding != null) return "feeding";
            if (Carrying != null) return "carrying a body";
            if (Rushing) return "running";
            if (InMist) return "in mist";
            if (_link != null || (Feet.y > 1.5f && !OnUpperFloor())) return "climbing";
            if (Game.Level != null && Game.Level.ZoneAt(Feet, true) != null) return "trespassing";
            return null;
        }

        PlayerAction _pending;
        float _lastHurt = -99f, _bleedUntil, _bleedDist, _noRegenUntil, _stepT, _heartT, _roofT, _mistFxT, _burnT;
        float _channel; System.Action _channelDone; Pose _channelPose;
        Interactable _useTarget; float _useT;

        // ---------------------------------------------------------------- spawn
        public static Vampire Spawn(Vector3 pos, float yaw, Transform parent)
        {
            var go = new GameObject("Ilse");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.layer = Layers.Character;
            var v = go.AddComponent<Vampire>();
            v.Id = "ilse";
            v.Build();
            return v;
        }

        void Build()
        {
            Rig = CharacterRig.BuildVampire(transform);
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.35f;
            col.isTrigger = true;

            HP = MaxHP;
            var d = Game.Level != null ? Game.Level.Data : null;
            float startFrac = d != null ? d.GetFloat("start_blood", 0.5f) : 0.5f;
            Blood = Mathf.Round(MaxBlood * startFrac);
            // "blood = N" in the map header is an absolute starting amount (M01's starving start)
            if (d != null && d.GetFloat("blood", -1f) >= 0f) Blood = Mathf.Min(MaxBlood, d.GetFloat("blood", 0f));
            Game.Player = this;
        }

        void Start()
        {
            Agent = gameObject.AddComponent<NavMeshAgent>();
            Agent.agentTypeID = NavAreas.VampireAgent;
            Agent.radius = 0.3f;
            Agent.height = 1.8f;
            Agent.speed = GlideSpeed;
            Agent.acceleration = 40f;
            Agent.angularSpeed = 900f;
            Agent.stoppingDistance = 0.05f;
            Agent.autoBraking = true;
            Agent.autoTraverseOffMeshLink = false;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            Agent.areaMask = AreaMask();
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas }))
                Agent.Warp(hit.position);
        }

        void OnDestroy()
        {
            if (Game.Player == this) Game.Player = null;
            if (_controlRing) Destroy(_controlRing);
        }

        public int AreaMask()
        {
            int m = NavAreas.VampireBaseMask;
            if (Awakening >= 4) m |= 1 << NavAreas.ClimbAny;
            if (Awakening >= 3 || Has("predator.bound")) m |= 1 << NavAreas.Leap;
            if (InMist) m |= 1 << NavAreas.Mist;
            if (Carrying != null) m &= ~((1 << NavAreas.Climb) | (1 << NavAreas.ClimbAny) | (1 << NavAreas.Leap));
            return m;
        }

        // ---------------------------------------------------------------- frame
        void Update()
        {
            float dt = Time.deltaTime;
            if (Dead) return;
            if (dt <= 0f) return;   // paused: real time only, no orders while the world is frozen

            UpdateStats(dt);
            if (Dead) return;
            ReadMoveInput();
            TickDashClock(dt);
            if (_link != null) { TickLink(dt); UpdatePose(); return; }
            if (Agent && Agent.enabled && Agent.isOnOffMeshLink) { BeginLink(); UpdatePose(); return; }

            TickFeeding(dt);
            TickChannel(dt);
            TickUse(dt);
            TickDirect(dt);
            TickPending(dt);
            TickMovement(dt);
            TickAbilities(dt);
            TickThralls(dt);
            HandleInput();
            UpdatePose();
        }

        void UpdateStats(float dt)
        {
            var feet = Feet;
            Light = Concealed ? 0f : (Game.Lights != null ? Game.Lights.LightAt(feet) : 0f);
            bool leaves = Game.Level != null && Game.Level.InFoliage(feet);
            if (leaves) Light *= 0.6f;
            SightLight = JudgedLight(Light, leaves);

            if (Humour != null && Time.time > HumourUntil) { Humour = null; HP = Mathf.Min(HP, MaxHP); }

            // burning light (sunstone / holy)
            float burn = Game.Lights != null && !Concealed ? Game.Lights.BurnAt(feet) : 0f;
            if (burn > 0.01f)
            {
                float mul = Has("sanguis.silverblood") ? 0.5f : 1f;
                Damage(14f * burn * mul * dt, false, null, holy: true, quiet: true);
                _burnT -= dt;
                if (_burnT <= 0f) { _burnT = 0.25f; Fx.Smoke(feet + Vector3.up * Random.Range(0.4f, 1.6f), new Color(0.9f, 0.6f, 0.3f, 0.6f), 0.5f, 0.8f); Rig.SetGlow(new Color(0.8f, 0.35f, 0.05f)); }
                if (Dead) return;
            }
            else if (_burnT < 0f || _burnT > 0f) { _burnT = 0f; if (Rig && !InMist) Rig.ClearGlow(); }

            // regeneration: only in darkness, never in combat, never under silver
            bool calm = Time.time - _lastHurt > 4f && Time.time > _noRegenUntil;
            if (calm && InDark && HP < MaxHP)
            {
                if (Has("shade.nightblood")) HP = Mathf.Min(MaxHP, HP + 1f * dt);
                float rate = 4f * (Awakening >= 6 ? 2f : 1f) * dt;
                float spend = Mathf.Min(rate, MaxHP - HP, Mathf.Max(0f, Blood - MaxBlood * 0.15f));
                if (spend > 0f) { HP += spend; Blood -= spend; }
            }

            // starving: faster, but the hunger is audible
            if (Starving && !Concealed)
            {
                _heartT -= dt;
                if (_heartT <= 0f)
                {
                    _heartT = 1.3f;
                    Game.Noise?.Emit(feet, 3f, NoiseKind.Heartbeat, this);
                }
            }
            if (Game.Audio != null) Game.Audio.HeartVolume = Starving ? 0.6f : 0f;

            // blood trail: fresh wounds (and grave ones) drip; hounds follow the drops
            if (Bleeding && !Concealed && !InMist && Game.Level != null)
            {
                _bleedDist += Mathf.Min(1f, Util.FlatDistance(feet, _bleedLast));
                if (_bleedDist > 1.8f)
                {
                    _bleedDist = 0f;
                    Evidence.DropTrail(feet + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f)), Game.Level.DynamicRoot);
                    if (!_bledHint && Game.AI != null && Game.AI.Npcs.Exists(n => n.IsAlive && n.Arch.Has(ArchFlags.Smell)))
                    {
                        _bledHint = true;
                        Game.UI?.Hint("You are bleeding. Hounds follow blood trails. Rest in the dark to close the wound.", null);
                    }
                }
            }
            _bleedLast = feet;

            // rooftop habit (the Dossier watches where she walks)
            if (feet.y > 2.8f && Moving)
            {
                _roofT += dt;
                if (_roofT > 10f) { _roofT = 0f; Game.Campaign?.AddHabit(Progression.Habits.Rooftops, 1f); }
            }

            // perception multiplier
            float vis = 1f;
            if (Rushing) vis *= 2f;
            else if (!Moving && !Busy) vis *= 0.6f;
            if (Carrying != null) vis *= 1.2f;
            if (Feeding != null) vis *= Feeding.State == NpcState.Victim && _feedVeiled ? 0.4f : 1.3f;
            if (InMist) vis *= Light > 0.5f ? 0.7f : 0.25f;
            if (Humour == "languid") vis *= 0.9f;
            if (DashVeiled) vis = 0f;
            VisibilityMul = vis;
        }

        // ---------------------------------------------------------------- movement
        public float SpeedMul
        {
            get
            {
                float s = 1f;
                if (Carrying != null) s *= Awakening >= 2 ? 0.9f : 0.6f;
                if (InMist) s *= 0.7f;
                if (Starving) s *= 1.1f;
                if (Humour == "languid") s *= 0.9f;
                return s;
            }
        }

        public bool GoTo(Vector3 p, bool rush)
        {
            if (!Agent || !Agent.enabled || !Agent.isOnNavMesh) return false;
            if (Concealed) LeaveHideSpot();
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = Agent.areaMask };
            if (!NavMesh.SamplePosition(p, out var hit, 1.6f, filter)) return false;
            var path = new NavMeshPath();
            if (!Agent.CalculatePath(hit.position, path) || path.status == NavMeshPathStatus.PathInvalid) return false;
            Agent.SetPath(path);
            Rushing = rush && Carrying == null && !InMist;
            return true;
        }

        public bool CanReach(Vector3 p, out Vector3 snapped)
        {
            snapped = p;
            if (!Agent || !Agent.isOnNavMesh) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = Agent.areaMask };
            if (!NavMesh.SamplePosition(p, out var hit, 1.6f, filter)) return false;
            snapped = hit.position;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(transform.position, hit.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>Scripted relocation (a boat crossing): snaps to the nearest vampire navmesh point.</summary>
        public void Teleport(Vector3 p)
        {
            Stop();
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            if (Agent && Agent.enabled && NavMesh.SamplePosition(p, out var hit, 2f, filter)) Agent.Warp(hit.position);
            else transform.position = p;
        }

        public void Stop()
        {
            if (Agent && Agent.enabled && Agent.isOnNavMesh) Agent.ResetPath();
            Rushing = false;
        }

        void TickMovement(float dt)
        {
            if (!Agent || !Agent.enabled) return;
            Agent.areaMask = AreaMask();
            Agent.speed = (Rushing ? RushSpeed : Sneaking ? SneakSpeed : GlideSpeed) * SpeedMul;
            if (!Moving) Rushing = false;

            if (Moving)
            {
                _stepT -= dt;
                if (_stepT <= 0f)
                {
                    var gait = Gait;
                    _stepT = MoveMath.StepInterval(gait);
                    var surf = Game.Level != null ? Game.Level.SurfaceAt(Feet) : Surface.Stone;
                    float radius = MoveMath.StepNoise(gait, surf == Surface.Water, Carrying != null);
                    if (InMist || Dashing) radius = 0f;
                    if (Humour == "languid") radius *= 0.85f;
                    if (Weather.Raining) radius *= Weather.FootfallMul;
                    if (radius > 0f)
                    {
                        Game.Noise?.Emit(Feet, radius, Rushing ? NoiseKind.Rush : NoiseKind.Footstep, this);
                        // what she can hear of herself: a faint ring the size of the step
                        Fx.Ring(Feet, radius, Rushing ? new Color(0.85f, 0.25f, 0.2f, 0.35f) : new Color(0.85f, 0.8f, 0.7f, 0.22f), 0.55f);
                    }
                    if (!InMist && !Dashing && Game.Audio != null)
                    {
                        if (surf == Surface.Water) Game.Audio.PlayVariant("step_water", 3, Feet, Rushing ? 0.5f : gait == Gait.Sneak ? 0.12f : 0.25f);
                        else if (gait != Gait.Sneak) Game.Audio.PlayVariant(surf == Surface.Wood ? "step_wood" : "step_stone", 3, Feet, Rushing ? 0.35f : 0.16f);
                        else Game.Audio.PlayAt("step_vamp", Feet, 0.08f, Random.Range(0.9f, 1.1f));
                    }
                }
            }

            if (InMist)
            {
                _mistFxT -= dt;
                if (_mistFxT <= 0f)
                {
                    _mistFxT = 0.07f;
                    Fx.Smoke(Feet + Random.insideUnitSphere * 0.4f + Vector3.up * Random.Range(0.2f, 1.2f), new Color(0.55f, 0.55f, 0.65f, 0.35f), 1.4f, 1.1f);
                }
            }
        }

        void UpdatePose()
        {
            if (!Rig) return;
            if (Dead) { Rig.Pose = Pose.Lying; return; }
            if (_link != null) { Rig.Pose = _linkPose; Rig.Speed = 1f; return; }
            if (Feeding != null) { Rig.Pose = Pose.Feed; return; }
            if (_channel > 0f) { Rig.Pose = _channelPose; return; }
            if (_useTarget != null) { Rig.Pose = Pose.Interact; return; }
            float sp = Mathf.Max(Agent && Agent.enabled ? Agent.velocity.magnitude : 0f, _actualSpeed);
            Rig.Speed = sp;
            Rig.Pose = sp < 0.15f ? Pose.Stand : Rushing ? Pose.Run : Sneaking ? Pose.Sneak : Pose.Walk;
        }

        // ---------------------------------------------------------------- off-mesh link traversal
        NavLink _link;
        LinkKind _linkKind;
        Vector3[] _linkPts;
        float[] _linkTimes;
        float _linkT, _linkDur, _linkArc;
        Pose _linkPose;

        void BeginLink()
        {
            var data = Agent.currentOffMeshLinkData;
            Vector3 from = transform.position, to = data.endPos;
            NavLink link = null;
            if (Game.Level != null && Game.Level.Nav != null && Game.Level.Nav.Resolve(NavAreas.VampireAgent, transform.position, data.startPos, data.endPos, out link, out var f, out var t))
            {
                from = transform.position;
                to = t;
            }
            SetupLink(link ?? new NavLink { Kind = LinkKind.Jump, Start = from, End = to }, from, to);
        }

        /// <summary>Lays out the traversal of <paramref name="link"/> from where she stands to <paramref name="to"/>.</summary>
        void SetupLink(NavLink link, Vector3 from, Vector3 to)
        {
            _link = link;
            _linkKind = _link.Kind;
            _linkT = 0f;
            _linkArc = 0f;
            var flat = (to - from).Flat();
            if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat);
            float climbSpeed = Has("predator.bound") ? 5f : 2.6f;
            switch (_linkKind)
            {
                case LinkKind.Climb:
                case LinkKind.ClimbAny:
                case LinkKind.Ladder:
                {
                    var mid = from + flat * 0.5f;
                    var a = new Vector3(mid.x, from.y, mid.z);
                    var b = new Vector3(mid.x, to.y, mid.z);
                    _linkPts = new[] { from, a, b, to };
                    float h = Mathf.Abs(to.y - from.y);
                    bool stairs = _linkKind == LinkKind.Ladder && flat.magnitude > 2.5f;
                    if (stairs) { _linkPts = new[] { from, to }; _linkTimes = new[] { 0f, (to - from).magnitude / (GlideSpeed * 0.8f * SpeedMul) }; _linkPose = Pose.Walk; }
                    else
                    {
                        float hz = flat.magnitude * 0.5f / GlideSpeed;
                        _linkTimes = new[] { 0f, hz, hz + h / climbSpeed, hz + h / climbSpeed + hz };
                        _linkPose = Pose.Climb;
                        Game.Audio?.PlayAt("climb", from, 0.3f);
                    }
                    break;
                }
                case LinkKind.Leap:
                    _linkPts = new[] { from, to };
                    _linkTimes = new[] { 0f, 0.5f + (to - from).magnitude * 0.03f };
                    _linkArc = 1.0f + flat.magnitude * 0.15f;
                    _linkPose = Pose.Run;
                    Game.Audio?.PlayAt("swish", from, 0.4f);
                    Game.Campaign?.AddHabit(Progression.Habits.Rooftops, 0.5f);
                    break;
                case LinkKind.Mist:
                    // as mist she pours through; out of it, it is a dash through the bars (Between Bars)
                    _linkPts = new[] { from, to };
                    _linkTimes = new[] { 0f, (to - from).magnitude / (InMist ? 2.2f : 15f) };
                    _linkPose = Pose.Stand;
                    Game.Audio?.PlayAt("mist", from, 0.3f);
                    break;
                default: // Jump (drop)
                    _linkPts = new[] { from, to };
                    _linkTimes = new[] { 0f, 0.3f + Mathf.Abs(from.y - to.y) * 0.05f + flat.magnitude * 0.05f };
                    _linkArc = 0.35f;
                    _linkPose = Pose.Run;
                    break;
            }
            _linkDur = _linkTimes[_linkTimes.Length - 1];
        }

        void TickLink(float dt)
        {
            _linkT += dt;
            float t = Mathf.Min(_linkT, _linkDur);
            int seg = 1;
            while (seg < _linkTimes.Length - 1 && t > _linkTimes[seg]) seg++;
            float t0 = _linkTimes[seg - 1], t1 = _linkTimes[seg];
            float k = t1 > t0 ? Mathf.Clamp01((t - t0) / (t1 - t0)) : 1f;
            var p = Vector3.Lerp(_linkPts[seg - 1], _linkPts[seg], k);
            if (_linkArc > 0f) p.y += Mathf.Sin(Mathf.Clamp01(t / _linkDur) * Mathf.PI) * _linkArc;
            transform.position = p;
            if (_linkT >= _linkDur) EndLink();
        }

        void EndLink()
        {
            var kind = _linkKind;
            var end = _linkPts[_linkPts.Length - 1];
            _link = null;
            transform.position = end;
            // a Pounce hands over to its own follow-up; a pushed climb or drop (D114) just puts the agent back under her
            bool pounce = _afterLeap != null;
            if (pounce || (Agent && !Agent.updatePosition)) { AfterManualLeap(end); if (pounce) return; }
            if (Agent && Agent.enabled && Agent.isOnOffMeshLink) Agent.CompleteOffMeshLink();
            if (kind == LinkKind.Jump || kind == LinkKind.Leap)
            {
                Game.Audio?.PlayAt("step_vamp", end, 0.3f, 0.7f);
                // Drop-feed (Awakening 7): landing next to the intended victim starts a Drain at once.
                if (Awakening >= 7 && _pending != null && _pending.Kind == ActionKind.Feed && _pending.Npc && Util.FlatDistance(_pending.Npc.transform.position, end) < 2.2f
                    && _pending.Npc.IsAlive && !_pending.Npc.Aware)
                {
                    var n = _pending.Npc;
                    _pending = null;
                    BeginFeed(n, true, true);
                }
            }
        }

        // ---------------------------------------------------------------- channelled actions (snuff, hide, cast wind-up)
        void Channel(float duration, Pose pose, System.Action done)
        {
            Stop();
            _channel = Mathf.Max(0.01f, duration);
            _channelPose = pose;
            _channelDone = done;
        }

        void TickChannel(float dt)
        {
            if (_channel <= 0f) return;
            _channel -= dt;
            if (_channel <= 0f)
            {
                var d = _channelDone;
                _channelDone = null;
                _channel = 0f;
                d?.Invoke();
            }
        }

        public float ChannelProgress => _useTarget != null ? Mathf.Clamp01(_useT / Mathf.Max(0.01f, _useTarget.Duration)) : 0f;

        // ---------------------------------------------------------------- damage & death
        public void Damage(float amount, bool silver, Npc source) => Damage(amount, silver, source, false, false);

        /// <summary>Debug console: no damage.</summary>
        public static bool GodMode;
        public static bool NoTarget;

        /// <summary>A fresh wound drips for a while; below a third of her health she drips until she heals.</summary>
        public bool Bleeding => !Dead && !GodMode && (Time.time < _bleedUntil || HP < MaxHP * 0.33f);
        bool _bledHint;
        Vector3 _bleedLast;      // debug: NPCs neither see nor hear her

        public void Damage(float amount, bool silver, Npc source, bool holy, bool quiet)
        {
            if (Dead || amount <= 0f || GodMode) return;
            if (Concealed && !holy) return;
            if ((silver || holy) && Has("sanguis.silverblood")) amount *= 0.5f;
            if (silver) _noRegenUntil = Time.time + 10f;
            _lastHurt = Time.time;
            if (BonusHP > 0f) { float a = Mathf.Min(BonusHP, amount); BonusHP -= a; amount -= a; }
            HP -= amount;
            if (!quiet)
            {
                _bleedUntil = Mathf.Max(_bleedUntil, Time.time + 15f + amount);
                Fx.BloodBurst(Feet + Vector3.up * 1.2f, 0.8f);
                Game.Audio?.PlayAt("hit", Feet, 0.8f);
                Game.Cam?.Shake(0.35f);
                InterruptActions();
            }
            if (HP <= 0f) Die(source != null ? source.DisplayName : holy ? "light" : "wounds");
        }

        void InterruptActions()
        {
            if (Feeding != null) EndFeed(true);
            _useTarget = null;
            if (_channel > 0f) { _channel = 0f; _channelDone = null; }
        }

        public string DeathCause;

        void Die(string cause)
        {
            if (Dead) return;
            Dead = true;
            HP = 0f;
            DeathCause = cause;
            InterruptActions();
            if (Carrying != null) DropBody(false);
            if (InMist) SetMist(false);
            Stop();
            if (Agent) Agent.enabled = false;
            if (Rig) { Rig.Pose = Pose.Lying; Rig.SetGlow(new Color(0.6f, 0.2f, 0.05f)); }
            Game.Audio?.Play2D("fail");
            Game.Mission?.OnPlayerDied(cause);
        }

        public void Heal(float amount) { HP = Mathf.Min(MaxHP, HP + amount); }

        public void AddBlood(float amount)
        {
            float max = MaxBlood;
            float nb = Blood + amount;
            if (nb > max && Has("sanguis.vessel")) BonusHP = Mathf.Min(30f, BonusHP + (nb - max));
            Blood = Mathf.Clamp(nb, 0f, max);
        }

        // ---------------------------------------------------------------- items
        public bool HasItem(string id) => id != null && Items.Contains(id);
        public void AddItem(string id)
        {
            if (id == null || Items.Contains(id)) return;
            Items.Add(id);
            // a key, however it was come by (taken, or given by a script), opens the way for her pathing
            if (Game.Level != null) foreach (var d in Game.Level.All<Door>()) if (d.KeyId == id) d.OnKeyAcquired();
        }

        // ---------------------------------------------------------------- save
        public PlayerSave Capture()
        {
            return new PlayerSave
            {
                P = transform.position, Yaw = transform.eulerAngles.y, HP = HP, Blood = Blood, BonusHP = BonusHP,
                Humour = Humour, HumourLeft = Humour != null ? HumourUntil - Time.time : 0f,
                Items = new List<string>(Items), Carrying = Carrying ? Carrying.Id : null,
                HideSpot = InsideSpot ? InsideSpot.Id : null, InMist = InMist,
                Thralls = Thralls.ConvertAll(t => t.Id),
                Cooldowns = CaptureCooldowns(),
                Runes = CaptureRunes(),
            };
        }

        public void Restore(PlayerSave s)
        {
            InterruptActions();
            _pending = null;
            _link = null;
            if (Agent && Agent.enabled) Agent.Warp(s.P); else transform.position = s.P;
            transform.rotation = Quaternion.Euler(0, s.Yaw, 0);
            HP = s.HP; Blood = s.Blood; BonusHP = s.BonusHP;
            Humour = string.IsNullOrEmpty(s.Humour) ? null : s.Humour;
            HumourUntil = Time.time + s.HumourLeft;
            Items.Clear(); Items.AddRange(s.Items);
            Carrying = null;
            if (!string.IsNullOrEmpty(s.Carrying))
            {
                var n = Game.Level.Get<Npc>(s.Carrying);
                if (n) { Carrying = n; n.SetCarried(true, transform); }
            }
            Concealed = false; InsideSpot = null; Rig.SetVisible(true);
            if (!string.IsNullOrEmpty(s.HideSpot)) { var h = Game.Level.Get<HideSpot>(s.HideSpot); if (h) EnterHideSpot(h); }
            InMist = false;
            if (s.InMist) SetMist(true);
            Thralls.Clear();
            foreach (var id in s.Thralls) { var n = Game.Level.Get<Npc>(id); if (n && (n.State == NpcState.Thrall || n.MakeThrall())) Thralls.Add(n); }
            SelectedThrall = null;
            RestoreCooldowns(s.Cooldowns);
            RestoreRunes(s.Runes);
        }
    }

    [System.Serializable]
    public class PlayerSave
    {
        public Vector3 P;
        public float Yaw, HP, Blood, BonusHP, HumourLeft;
        public string Humour, Carrying, HideSpot;
        public bool InMist;
        public List<string> Items = new List<string>();
        public List<string> Thralls = new List<string>();
        public List<Progression.Counter> Cooldowns = new List<Progression.Counter>();
        public List<Vector3> Runes = new List<Vector3>();
    }
}
