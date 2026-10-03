using System;
using UnityEngine;
using UnityEngine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Stealth;

namespace Vespertine.AI
{
    public enum ThrallOrder { None, Move, Interact, Distract, Strike, FalseOrders, Follow }

    [Serializable]
    public class NpcSave
    {
        public string Id;
        public int State;
        public Vector3 P;
        public float Yaw;
        public float Detection, StateTime, StateDuration, Dur2;
        public bool Active = true, Wary, Hidden, Disposed, Found, Drained, Asleep, Sitting, Lantern, LanternOn, Puppet, WardTorn, Hunting, EscortWait, Loose;
        public int HP, Wp, WpDir;
        public string Route;
        public string KilledBy, HideSpot;
        public Vector3 LastKnown, Target;
    }

    public partial class Npc
    {
        // ------------------------------------------------------------------ ability state
        float _snareT;
        public bool Snared => _snareT > 0f;
        float _hemorrhageT = -1f;
        public bool IsCorpsePuppet;
        float _puppetT;
        public string HideSpotId;

        // thrall
        public ThrallOrder Order;
        Vector3 _orderPos;
        Interactable _orderUse;
        Npc _orderNpc;
        float _orderT;
        public bool IsThrall => State == NpcState.Thrall;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_snareT > 0f)
            {
                _snareT -= dt;
                if (State != NpcState.Dead && State != NpcState.Dazed) Halt();
                if (_snareT <= 0f) Rig?.ClearGlow();
            }
            if (_hemorrhageT >= 0f && State != NpcState.Dead)
            {
                _hemorrhageT += dt;
                if (_hemorrhageT > 2.5f)
                {
                    _hemorrhageT = -1f;
                    Evidence.SpawnStain(transform.position, Game.Level.DynamicRoot, false, 1f, true);
                    Die("hemorrhage");
                }
            }
            if (IsCorpsePuppet)
            {
                _puppetT -= dt;
                if (_puppetT <= 0f) EndPuppet();
            }
        }

        // ------------------------------------------------------------------ feeding
        /// <summary>Angle between this NPC's facing and the direction to a point (0 = in front, 180 = directly behind).</summary>
        public float AngleTo(Vector3 p) => Vector3.Angle(Forward, (p - transform.position).Flat());

        public bool Helpless => State == NpcState.Dazed || State == NpcState.Mesmerised || Asleep || Snared || State == NpcState.Thrall;

        public bool CanBeFedBy(Player.Vampire v, out string reason)
        {
            reason = null;
            if (State == NpcState.Dead) { reason = "Dead blood is useless"; return false; }
            if (Friendly) { reason = "Not him. Never him."; return false; }
            if (IsUndead) { reason = "Kindred blood is ash in her mouth"; return false; }
            if (Rescue) { reason = "She came to free them, not to feed"; return false; }
            if (Carried || Hidden || Disposed) { reason = "Unreachable"; return false; }
            if (State == NpcState.Victim) { reason = "Already feeding"; return false; }
            if (Arch.Has(ArchFlags.Quadruped) && !Helpless) { reason = "Too wary"; return false; }
            bool behind = AngleTo(v.transform.position) > 100f;
            if (Arch.Has(ArchFlags.Armored) && !behind && State != NpcState.Dazed && !Asleep) { reason = "Armoured - approach from behind"; return false; }
            if (Helpless) return true;
            if (Aware || State == NpcState.Alerted) { reason = "They see you"; return false; }
            if (!behind && Detection > 0.6f) { reason = "They see you coming"; return false; }
            if (!behind && State != NpcState.Distracted && State != NpcState.Relaxed && State != NpcState.Holding) { reason = "Approach from behind"; return false; }
            if (!behind && Detection > 0.05f) { reason = "Approach from behind"; return false; }
            return true;
        }

        NpcState _preVictim;
        public void BeginVictim(Player.Vampire v)
        {
            _preVictim = State;
            SetState(NpcState.Victim);
            Asleep = false;
            Sitting = false;
            Halt();
            if (Agent) Agent.enabled = false;
            // face away from the vampire
            var away = (transform.position - v.transform.position).Flat();
            if (away.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(away);
            Detection = 0f;
            if (WardProtected)
            {
                WardTorn = true;
                Game.UI?.Toast($"{DisplayName}'s ward charm tears loose. Dominion will take hold now.");
            }
        }

        /// <summary>Finish a feed. Drained → dead; sipped → dazed (wakes later, remembers).</summary>
        public void EndVictim(bool drained, bool interrupted = false)
        {
            if (State != NpcState.Victim) return;
            if (drained)
            {
                Drained = true;
                Die("drain");
            }
            else
            {
                float dur = Game.Countermeasure("cm_inquest") ? 25f : 45f;
                if (Game.Campaign != null && Game.Campaign.Has("sanguis.clean")) dur *= 1.6f;
                if (interrupted) dur = 3f;
                if (Agent) Agent.enabled = true;
                State = NpcState.Relaxed;
                Daze(dur, true);
            }
        }

        public void Daze(float duration, bool fromFeed)
        {
            SetState(NpcState.Dazed);
            _stateDuration = duration;
            Found = false;
            Asleep = false;
            Sitting = false;
            Detection = 0f;
            Halt();
            if (Agent) Agent.enabled = false;
            _fromFeed = fromFeed;
            gameObject.layer = Layers.Corpse;
            GameEvents.RaiseNpcDowned(this);
        }
        bool _fromFeed;

        /// <summary>Recover from dazed. If found by someone, or the victim saw anything, they report.</summary>
        public void WakeUp(bool report, Npc by = null)
        {
            if (State != NpcState.Dazed || Carried) return;
            // leave Dazed first: EnterPanic and EnterSearching refuse an incapacitated NPC, so a victim that was still
            // Dazed here stayed down and reported again every frame from the Dazed tick (I-02)
            SetState(NpcState.Relaxed);
            gameObject.layer = Layers.Character;
            ReenableAgent();
            Wary = true;
            if (report && _fromFeed && !(Game.Campaign != null && Game.Campaign.Has("dominion.mesmerize_forget") && by == null && Hidden))
            {
                Say(BarkKind.Wake, true);
                Game.AI?.ReportWitness(this);
            }
            _fromFeed = false;
            if (Hidden)
            {
                // wakes inside a hiding spot: climbs out
                Hidden = false;
                Rig?.SetVisible(true);
                var hs = HideSpotId != null ? Game.Level.Get<HideSpot>(HideSpotId) : null;
                hs?.RemoveBody(this);
                HideSpotId = null;
            }
            if (Arch.Morale == Morale.Civilian) EnterPanic(transform.position + Forward * -3f);
            else EnterSearching(transform.position, false);
        }

        void ReenableAgent()
        {
            if (!Agent) return;
            Agent.enabled = true;
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(transform.position, out var hit, 4f, filter)) Agent.Warp(hit.position);
        }

        // ------------------------------------------------------------------ bodies
        public void SetCarried(bool carried, Transform holder)
        {
            Carried = carried;
            var col = GetComponent<Collider>();
            if (carried)
            {
                if (Agent) Agent.enabled = false;
                if (col) col.enabled = false;
                transform.SetParent(holder, false);
                transform.localPosition = new Vector3(0, 1.25f, 0.1f);
                transform.localRotation = Quaternion.Euler(0, 90, 0);
                Hidden = false;
                Rig?.SetVisible(true);
            }
            else
            {
                var p = holder ? holder.position : transform.position;
                var yaw = holder ? holder.eulerAngles.y + 90f : 0f;
                transform.SetParent(Game.Level.EntityRoot, true);
                if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var h, 6f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) p = h.point;
                transform.position = p;
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                if (col) col.enabled = true;
            }
        }

        public void HideIn(HideSpot spot)
        {
            Hidden = true;
            HideSpotId = spot ? spot.Id : null;
            Rig?.SetVisible(false);
            var col = GetComponent<Collider>();
            if (col) col.enabled = false;
            if (spot) transform.position = spot.transform.position;
        }

        public void Dispose(string how)
        {
            Disposed = true;
            Hidden = true;
            GameEvents.RaiseBodyDisposed(how);
            if (State == NpcState.Dazed) { State = NpcState.Dead; KilledBy = how == "canal" ? "drowned" : how; GameEvents.RaiseNpcKilled(this); }
            gameObject.SetActive(false);
        }

        public void Die(string cause)
        {
            if (State == NpcState.Dead) return;
            bool wasThrall = State == NpcState.Thrall;
            SetState(NpcState.Dead);
            KilledBy = cause;
            HP = 0;
            GameEvents.RaiseNpcDowned(this);
            Found = false;
            Asleep = false;
            Sitting = false;
            Detection = 0f;
            _snareT = 0f;
            Halt();
            if (Agent) Agent.enabled = false;
            gameObject.layer = Layers.Corpse;
            DropLantern();
            Rig?.ClearGlow();
            if (_bellTarget) { Game.AI?.ReleaseBell(_bellTarget, this); _bellTarget = null; }
            if (cause == "combat" || cause == "rend" || cause == "strike") Evidence.SpawnStain(transform.position, Game.Level.DynamicRoot, false);
            if (wasThrall) Game.Player?.ReleaseThrall(this);
            Game.Audio?.PlayAt("thud", transform.position, 0.6f);
            Game.Noise?.Emit(transform.position, cause == "drain" ? 0f : 4f, NoiseKind.Body, this);
            GameEvents.RaiseNpcKilled(this);
        }

        void EnterDeadVisuals()
        {
            if (Agent) Agent.enabled = false;
            gameObject.layer = Layers.Corpse;
            if (Lantern) { Destroy(Lantern.gameObject); Lantern = null; }
        }

        void DropLantern()
        {
            if (!Lantern) return;
            var l = Lantern;
            Lantern = null;
            l.transform.SetParent(Game.Level.DynamicRoot, true);
            var p = l.transform.position;
            if (Physics.Raycast(p + Vector3.up, Vector3.down, out var h, 4f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) p = h.point;
            l.transform.position = p;
            l.Snuffable = true;
            l.Portable = false;
        }

        /// <summary>Combat damage (rend, puppet strike). Returns true if killed.</summary>
        public bool TakeHit(int damage, string cause, Vector3 from)
        {
            if (State == NpcState.Dead) return false;
            HP -= damage;
            Rig?.SetGlow(new Color(0.6f, 0.05f, 0.05f));
            Invoke(nameof(ClearHitGlow), 0.15f);
            if (HP <= 0) { Die(cause); return true; }
            if (State != NpcState.Dazed && State != NpcState.Victim && State != NpcState.Thrall) Spot(from);
            return false;
        }

        void ClearHitGlow() { if (State != NpcState.Thrall && State != NpcState.Mesmerised) Rig?.ClearGlow(); }

        // ------------------------------------------------------------------ ability hooks
        /// <summary>Swings a censer of garlic smoke: no mist or Shadow Dash within 4.5 m. Alchemists always; the Vigil
        /// once the Dossier calls for censer-bearers.</summary>
        public bool CarriesCenser => IsAlive && !Incapacitated && State != NpcState.Thrall
            && (Arch.Has(ArchFlags.Censer) || (Arch.Faction == Faction.Vigil && !Arch.Has(ArchFlags.Quadruped) && Game.Countermeasure("cm_censer")));
        public const float CenserRadius = 4.5f;
        /// <summary>Salt in the pockets: Vigil and Church feet break blood snares once the Dossier salts the doorways.</summary>
        /// <summary>cm_caged: every Watch, Vigil, Church, Guild and Institute man carries a taper and relights a dark lamp he finds.</summary>
        public bool CarriesTaper => Arch.Faction != Faction.None && !Arch.Has(ArchFlags.Quadruped) && !Arch.Has(ArchFlags.Undead) && Game.Countermeasure("cm_caged");

        public bool CarriesSalt => (Arch.Faction == Faction.Vigil || Arch.Faction == Faction.Church) && !Arch.Has(ArchFlags.Quadruped) && Game.Countermeasure("cm_salt");

        /// <summary>A ward guards the mind, not the throat: feeding on a warded human tears the charm away for good.</summary>
        public bool WardTorn;
        public bool WardProtected => !WardTorn && Arch.Has(ArchFlags.Ward) || (Game.Countermeasure("cm_ward") && Arch.Faction != Faction.None && Spec != null && !Spec.Has("noward"));
        public bool Immune(out string why)
        {
            why = null;
            if (Arch.Has(ArchFlags.HolyAura)) { why = "Holy aura"; return true; }
            if (WardProtected) { why = "Ward charm"; return true; }
            return false;
        }

        public bool Mesmerise(float duration)
        {
            if (!IsAlive || Incapacitated || State == NpcState.Thrall) return false;
            if (Immune(out _)) return false;
            SetState(NpcState.Mesmerised);
            _stateDuration = duration;
            Detection = 0f;
            Asleep = false;
            Halt();
            Rig?.SetGlow(new Color(0.35f, 0.1f, 0.5f));
            return true;
        }

        public bool Snare(float duration)
        {
            if (!IsAlive || Incapacitated) return false;
            _snareT = duration;
            Halt();
            Rig?.SetGlow(new Color(0.5f, 0.02f, 0.05f));
            return true;
        }

        public bool Hemorrhage()
        {
            if (!IsAlive || Incapacitated || Arch.Has(ArchFlags.HolyAura)) return false;
            _hemorrhageT = 0f;
            _snareT = 2.6f;
            Rig?.SetGlow(new Color(0.7f, 0f, 0f));
            return true;
        }

        /// <summary>Shroud of Sleep: drop off where they stand (wakes on loud noise like any sleeper).</summary>
        public bool FallAsleep()
        {
            if (!IsAlive || Incapacitated || Asleep || State == NpcState.Thrall || State == NpcState.Alerted || State == NpcState.Victim) return false;
            if (Arch.Morale == Morale.Fearless || Arch.Has(ArchFlags.Quadruped)) return false;
            SetState(NpcState.Relaxed);
            Asleep = true;
            Sitting = false;
            Detection = 0f;
            Halt();
            return true;
        }

        public void Terrify(Vector3 from)
        {
            if (!IsAlive || Incapacitated || State == NpcState.Thrall || Routed) return;
            // a squad takes the fright together; it may be the thing that breaks it
            if (Squad != null && !Squad.Broken) Squad.Shock(SquadMath.TerrorExtra, from, "Hold the line! It's only trying to scare us!");
            if (Routed) return;
            if (Arch.Morale == Morale.Fearless) { Spot(from); return; }
            EnterPanic(from, DreadSilences);
        }

        public bool Beckon(Vector3 pos)
        {
            if (!IsAlive || Incapacitated || State == NpcState.Alerted || State == NpcState.Panicked || State == NpcState.Thrall) return false;
            if (Arch.Has(ArchFlags.Quadruped)) return false;
            Asleep = false;
            bool mimic = Game.Campaign != null && Game.Campaign.Has("dominion.beckon_mimic");
            EnterDistracted(pos, 5f, !mimic);
            if (!mimic) Say(BarkKind.Suspicious, true);
            return true;
        }

        // ------------------------------------------------------------------ thrall
        public bool MakeThrall()
        {
            if (!IsAlive || State == NpcState.Victim || Carried) return false;
            if (Immune(out _)) return false;
            if (State == NpcState.Dazed) ReenableAgent();
            gameObject.layer = Layers.Character;
            SetState(NpcState.Thrall);
            Order = ThrallOrder.None;
            Detection = 0f;
            Asleep = false;
            Sitting = false;
            Halt();
            Rig?.SetGlow(new Color(0.22f, 0.06f, 0.32f));
            // her hounds follow her voice, and her voice is Ilse's now
            if (Game.AI != null)
                foreach (var h in Game.AI.Npcs)
                    if (h && h.Leader == this && h.IsAlive && !h.Incapacitated && h.State != NpcState.Thrall) h.CalmToHeel();
            GameEvents.RaiseNpcDowned(this);
            return true;
        }

        void CalmToHeel()
        {
            SetState(NpcState.Relaxed);
            Detection = 0f;
            Wary = false;
            _returning = false;
            _casting = false;
            Halt();
        }

        /// <summary>The thrall breaks free (exposed, mission end, or released by the player).</summary>
        public void ReleaseFromThrall(bool exposed)
        {
            if (State != NpcState.Thrall) return;
            Rig?.ClearGlow();
            Order = ThrallOrder.None;
            if (IsCorpsePuppet) { EndPuppet(); return; }
            if (exposed) Daze(30f, true);
            else { SetState(NpcState.Relaxed); Wary = true; _returning = true; EnterSuspicious(transform.position + Forward * 2f, 3f); }
            Game.Player?.ReleaseThrall(this);
        }

        void ExposeThrall(Npc thrall)
        {
            if (thrall.IsCorpsePuppet)
            {
                thrall.EndPuppet();
                FindBody(thrall);
                return;
            }
            Say(BarkKind.Spotted, true);
            GameEvents.RaiseBark(Id, "The mark of the leech! This one is hers!");
            GameEvents.RaiseEvidence(this, "thrall");
            thrall.ReleaseFromThrall(true);
            Game.AI?.ReportWitness(this);
            EnterSearching(thrall.transform.position, true);
        }

        public bool RaisePuppet(float duration)
        {
            if (State != NpcState.Dead || Carried || Disposed) return false;
            IsCorpsePuppet = true;
            _puppetT = duration;
            Hidden = false;
            Rig?.SetVisible(true);
            var col = GetComponent<Collider>();
            if (col) col.enabled = true;
            gameObject.layer = Layers.Character;
            ReenableAgent();
            SetState(NpcState.Relaxed);
            _returning = true;
            Rig?.SetGlow(new Color(0.25f, 0f, 0.03f));
            return true;
        }

        void EndPuppet()
        {
            if (!IsCorpsePuppet) return;
            IsCorpsePuppet = false;
            Halt();
            if (Agent) Agent.enabled = false;
            gameObject.layer = Layers.Corpse;
            SetState(NpcState.Dead);
            Found = false;
            Rig?.ClearGlow();
        }

        public void OrderMove(Vector3 p) { Order = ThrallOrder.Move; _orderPos = p; _orderT = 0f; }
        public void OrderHold() { Order = ThrallOrder.None; _orderT = 0f; Halt(); }
        public void OrderFollow() { Order = ThrallOrder.Follow; _orderT = 0f; }
        public void OrderInteract(Interactable i) { Order = ThrallOrder.Interact; _orderUse = i; _orderT = 0f; }
        public void OrderDistract(Npc target) { Order = ThrallOrder.Distract; _orderNpc = target; _orderT = 0f; }
        public void OrderStrike(Npc target) { Order = ThrallOrder.Strike; _orderNpc = target; _orderT = 0f; }
        public void OrderFalse(Npc target, Vector3 sendTo) { Order = ThrallOrder.FalseOrders; _orderNpc = target; _orderPos = sendTo; _orderT = 0f; }

        public string OrderLabel
        {
            get
            {
                switch (Order)
                {
                    case ThrallOrder.Move: return "Moving";
                    case ThrallOrder.Interact: return _orderUse ? _orderUse.Verb + " " + _orderUse.DisplayName : "Using";
                    case ThrallOrder.Distract: return _orderNpc ? "Distracting " + _orderNpc.DisplayName : "Distracting";
                    case ThrallOrder.Strike: return _orderNpc ? "Striking " + _orderNpc.DisplayName : "Striking";
                    case ThrallOrder.FalseOrders: return "Relaying false orders";
                    case ThrallOrder.Follow: return "Following";
                }
                return DirectMove.sqrMagnitude > 0.01f ? "Under your hand" : "Holding";
            }
        }

        // ------------------------------------------------------------------ direct control (D114)
        /// <summary>Set every frame by Ilse while this thrall is under her hand: a world direction (0..1) from the move keys.</summary>
        [System.NonSerialized] public Vector3 DirectMove;
        [System.NonSerialized] public bool DirectRun;
        float _directSpd, _directActual;
        Vector3 _directDir = Vector3.forward;
        bool _viaLink;

        /// <summary>How fast this human is actually going (path following or steered directly).</summary>
        public float Speed => Mathf.Max(Agent && Agent.enabled ? Agent.velocity.magnitude : 0f, _directActual);

        /// <summary>The move keys steer the thrall. Any key press drops a standing order (hold, follow, an errand);
        /// pushing into a ladder for a moment takes it. Returns true while the keys are in charge.</summary>
        bool TickDirect(float dt)
        {
            bool wants = DirectMove.sqrMagnitude > 0.0004f;
            // a ladder taken by a push runs to its end before the keys steer again
            if (_viaLink && Order == ThrallOrder.Move) return false;
            _viaLink = false;
            if (wants)
            {
                if (Order != ThrallOrder.None) { Order = ThrallOrder.None; Halt(); }
                _convoT = 0f;
                _directDir = DirectMove.normalized;
            }
            float target = wants ? (DirectRun ? Arch.RunSpeed : Arch.WalkSpeed) * Mathf.Clamp01(DirectMove.magnitude) : 0f;
            _directSpd = Controls.MoveMath.Ease(_directSpd, target, dt, 18f, 30f);
            if (_directSpd <= 0.01f || !Agent || !Agent.enabled || !Agent.isOnNavMesh)
            {
                _directActual = Mathf.Lerp(_directActual, 0f, 1f - Mathf.Exp(-18f * dt));
                if (_directActual < 0.05f) _directActual = 0f;
                return wants;
            }
            if (Agent.hasPath) Agent.ResetPath();
            Agent.isStopped = false;
            var before = Agent.nextPosition;
            Agent.Move(Door.ClampStep(transform.position, _directDir * (_directSpd * dt)));
            var moved = Agent.nextPosition - before;
            moved.y = 0f;
            _directActual = Mathf.Lerp(_directActual, moved.magnitude / dt, 1f - Mathf.Exp(-18f * dt));
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_directDir), 720f * dt);
            if (wants && TryPushLadder()) return false;
            return true;
        }

        float _ladderPushT;
        bool TryPushLadder()
        {
            var nav = Game.Level != null ? Game.Level.Nav : null;
            if (nav == null) return false;
            var feet = transform.position;
            foreach (var l in nav.Links)
            {
                if (l.Agent != NavAreas.HumanAgent) continue;
                for (int pass = 0; pass < (l.Bidirectional ? 2 : 1); pass++)
                {
                    var s = pass == 0 ? l.Start : l.End;
                    var e = pass == 0 ? l.End : l.Start;
                    if (!Controls.MoveMath.PushesInto(feet, _directDir, s, e, l.Width, out _, out var to)) continue;
                    _ladderPushT += Time.deltaTime;
                    if (_ladderPushT < 0.18f) return false;
                    _ladderPushT = 0f;
                    OrderMove(to);
                    _viaLink = true;
                    _directSpd = _directActual = 0f;
                    return true;
                }
            }
            _ladderPushT = 0f;
            return false;
        }

        void TickThrall(float dt)
        {
            _orderT += dt;
            if (TickDirect(dt)) return;
            switch (Order)
            {
                case ThrallOrder.None:
                    Halt();
                    if (_convoT > 0f)
                    {
                        _convoT -= dt;
                        if (_lookAt) FaceTowards(_lookAt.transform.position, 240f);
                    }
                    return;
                case ThrallOrder.Follow:
                    if (Game.Player == null) { Order = ThrallOrder.None; return; }
                    var pp = Game.Player.transform.position;
                    if (Util.FlatDistance(pp, transform.position) > 2.5f) MoveTo(pp, false);
                    else Halt();
                    return;
                case ThrallOrder.Move:
                    if (!MoveTo(_orderPos, false) || Arrived || Stuck(dt)) { Halt(); Order = ThrallOrder.None; }
                    return;
                case ThrallOrder.Interact:
                {
                    var u = _orderUse;
                    if (!u || !u.Enabled || (u.Used && !u.Repeatable)) { Order = ThrallOrder.None; return; }
                    var up = u.UsePoint(transform.position, NavAreas.HumanAgent);
                    if (Util.FlatDistance(up, transform.position) > u.UseRange * 0.8f && !(Arrived && _orderT > 1f))
                    {
                        if (!MoveTo(up, false) || Stuck(dt)) Order = ThrallOrder.None;
                        return;
                    }
                    Halt();
                    FaceTowards(u.transform.position, 540f);
                    if (_orderT > 0.5f)
                    {
                        if (Util.FlatDistance(up, transform.position) <= u.UseRange + 0.6f) u.Use(false);
                        else Game.UI?.Toast(DisplayName + " cannot reach " + u.DisplayName);
                        Order = ThrallOrder.None;
                    }
                    return;
                }
                case ThrallOrder.Distract:
                case ThrallOrder.FalseOrders:
                case ThrallOrder.Strike:
                {
                    var t = _orderNpc;
                    if (!t || !t.IsAlive || t.Incapacitated || t.Hidden) { Order = ThrallOrder.None; return; }
                    var tp = t.transform.position;
                    float d = Util.FlatDistance(tp, transform.position);
                    if (d > (Order == ThrallOrder.Strike ? 1.3f : 2.2f))
                    {
                        if (!MoveTo(tp, Order == ThrallOrder.Strike) || Stuck(dt) || _orderT > 40f) Order = ThrallOrder.None;
                        return;
                    }
                    Halt();
                    FaceTowards(tp, 540f);
                    if (Order == ThrallOrder.Distract)
                    {
                        t.HoldAt(t.transform.position, 12f);
                        t._lookAt = this;
                        GameEvents.RaiseBark(Id, "Have you a moment? Something I must tell you...");
                        Order = ThrallOrder.None;
                        HoldConversation(t, 12f);
                    }
                    else if (Order == ThrallOrder.FalseOrders)
                    {
                        GameEvents.RaiseBark(Id, "Captain wants you elsewhere. Now.");
                        t.HoldAt(_orderPos, 45f);
                        t.Say(BarkKind.Investigate, true);
                        Order = ThrallOrder.None;
                    }
                    else
                    {
                        Game.Audio?.PlayAt("hit", tp + Vector3.up, 0.9f);
                        bool lethalPuppet = IsCorpsePuppet || (Game.Campaign != null && Game.Campaign.Has("dominion.puppet_strike"));
                        t.TakeHit(lethalPuppet ? 99 : 3, "strike", transform.position);
                        Order = ThrallOrder.None;
                        // a living thrall who kills is seen as a murderer, a mundane threat
                        if (!IsCorpsePuppet) Game.Noise?.Emit(transform.position, 8f, NoiseKind.Voice, this);
                    }
                    return;
                }
            }
        }

        // a conversation partner (distraction) faces the speaker
        Npc _lookAt;
        float _convoT;
        void HoldConversation(Npc t, float dur) { _convoT = dur; _lookAt = t; }

        // ------------------------------------------------------------------ save / load
        public NpcSave Capture()
        {
            return new NpcSave
            {
                Id = Id, Active = gameObject.activeSelf && !Disposed, State = IsCorpsePuppet ? (int)NpcState.Dead : (int)State, P = transform.position, Yaw = transform.eulerAngles.y,
                Detection = Detection, StateTime = _t, StateDuration = _stateDuration,
                Wary = Wary, Hidden = Hidden, Disposed = Disposed, Found = Found, Drained = Drained, Asleep = Asleep, Sitting = Sitting,
                Lantern = Lantern != null, LanternOn = Lantern != null && Lantern.On, Puppet = IsCorpsePuppet,
                HP = HP, Wp = _wp, WpDir = _wpDir, Route = _route?.Id ?? "", KilledBy = KilledBy, HideSpot = HideSpotId,
                LastKnown = LastKnown, Target = _target, WardTorn = WardTorn, Hunting = Hunting, EscortWait = EscortWait, Loose = Loose
            };
        }

        public void Restore(NpcSave s)
        {
            if (Carried) SetCarried(false, null);
            var st = (NpcState)s.State;
            // transient states collapse to their stable equivalents
            if (st == NpcState.Victim) st = NpcState.Dazed;
            if (st == NpcState.Thrall || st == NpcState.Mesmerised) st = NpcState.Relaxed;
            if (st == NpcState.Alerted || st == NpcState.Panicked) st = NpcState.Searching;
            if (Agent) Agent.enabled = false;
            transform.position = s.P;
            transform.rotation = Quaternion.Euler(0, s.Yaw, 0);
            Wary = s.Wary; Found = s.Found; Drained = s.Drained; Asleep = s.Asleep; Sitting = s.Sitting;
            // "" = no route; null = a save written before routes were persisted (keep the authored route)
            if (s.Route != null && s.Route != (_route?.Id ?? "")) _route = Game.Level.Data.Routes.TryGetValue(s.Route, out var rr) ? rr : null;
            HP = s.HP; _wp = s.Wp; _wpDir = s.WpDir == 0 ? 1 : s.WpDir; KilledBy = s.KilledBy;
            LastKnown = s.LastKnown; _target = s.Target;
            WardTorn = s.WardTorn; Hunting = s.Hunting; EscortWait = s.EscortWait; Loose = s.Loose; _hasHunt = false; _huntT = 1f;
            Detection = 0f;
            if (!s.Lantern && Lantern) { Destroy(Lantern.gameObject); Lantern = null; }
            Hidden = false; Disposed = false;
            var col = GetComponent<Collider>();
            if (col) col.enabled = true;
            Rig?.SetVisible(true);
            Rig?.ClearGlow();
            switch (st)
            {
                case NpcState.Dead:
                    State = NpcState.Dead; _t = 0;
                    EnterDeadVisuals();
                    break;
                case NpcState.Dazed:
                    State = NpcState.Relaxed;
                    Daze(Mathf.Max(3f, s.StateDuration - s.StateTime), true);
                    break;
                case NpcState.Captive:
                    gameObject.layer = Layers.Character;
                    ReenableAgent();
                    State = NpcState.Captive; _t = 0;
                    Halt();
                    break;
                case NpcState.Escort:
                case NpcState.Amok:
                    gameObject.layer = Layers.Character;
                    ReenableAgent();
                    State = st; _t = 0;
                    if (st == NpcState.Amok) Rig?.SetGlow(new Color(0.35f, 0.02f, 0.02f));
                    break;
                case NpcState.Searching:
                    gameObject.layer = Layers.Character;
                    ReenableAgent();
                    State = NpcState.Relaxed;
                    EnterSearching(s.LastKnown, false);
                    break;
                default:
                    gameObject.layer = Layers.Character;
                    ReenableAgent();
                    ResumeRoutine();
                    break;
            }
            if (s.Hidden && !string.IsNullOrEmpty(s.HideSpot))
            {
                var hs = Game.Level.Get<HideSpot>(s.HideSpot);
                if (hs) hs.AddBody(this); else HideIn(null);
            }
            if (s.Disposed) { Disposed = true; Hidden = true; gameObject.SetActive(false); }
        }
    }
}
