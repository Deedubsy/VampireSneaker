using UnityEngine;
using UnityEngine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.AI
{
    /// <summary>
    /// M09 rescue: prisoners (<c>npc ... prisoner</c>) start shackled (Captive). Ilse or a thrall breaks the shackles;
    /// the prisoner then follows her on foot (Escort): they cannot climb, wait below when she takes to the roofs, and
    /// guards who see them give chase and drag them back to their chains. A fledgling (Undead) refuses to walk into
    /// burning light, smoulders inside it, and can instead be turned loose (Amok) to hunt the staff.
    /// The mission's <c>escort</c> objective slips them away when they reach its area.
    /// </summary>
    public partial class Npc
    {
        public bool Prisoner;
        /// <summary>Escort told to stay put (toggled through the shackles' verb).</summary>
        public bool EscortWait;
        /// <summary>A fledgling she turned loose: no longer hers to lead out.</summary>
        public bool Loose;
        public Shackles Shackles;

        /// <summary>Captive, following, or running amok: not a guard, not a meal, not a target.</summary>
        public bool Rescue => State == NpcState.Captive || State == NpcState.Escort || State == NpcState.Amok;
        public bool Escaped => Prisoner && Disposed && State == NpcState.Escort;
        public bool IsUndead => Arch.Has(ArchFlags.Undead);

        float _escortScan, _burnAcc, _balkCd, _roofCd, _balkHold;
        Vector3 _safePos; bool _hasSafe;
        bool _sitsInCell, _toCell; // how the prisoner waits in its cell; on the way back to it after a recapture
        Npc _amokVictim;
        float _amokScan, _amokBite;

        static readonly string[] FreedLines = { "You... came for us?", "The chains - God, the chains -", "Lead. I'll follow.", "Quietly. They're everywhere." };
        static readonly string[] FledglingFreed = { "Mistress... the hunger...", "Out. Please. Out.", "I can smell them. All of them." };

        void InitPrisoner(EntitySpec spec)
        {
            Prisoner = true;
            State = NpcState.Captive;
            Sitting = _sitsInCell = !spec.Has("stand");
            var ss = new EntitySpec { Kind = "shackles", Id = Id + ".free", X = spec.X, Y = spec.Y, Group = spec.Group, Line = spec.Line };
            var go = new GameObject("shackles_" + Id);
            go.transform.SetParent(Game.Level.EntityRoot, false);
            go.transform.position = transform.position;
            Shackles = go.AddComponent<Shackles>();
            Shackles.Owner = this;
            Shackles.Init(ss);
            Game.Level.Register(Shackles);
        }

        // ------------------------------------------------------------------ transitions
        /// <summary>The shackles break: the prisoner rises and follows Ilse.</summary>
        public void Free(bool byThrall)
        {
            if (State != NpcState.Captive) return;
            Sitting = false;
            EscortWait = false;
            SetState(NpcState.Escort);
            Game.Audio?.PlayAt("chain", transform.position + Vector3.up, 0.9f);
            var lines = IsUndead ? FledglingFreed : FreedLines;
            GameEvents.RaiseBark(Id, Spec.Opt("freed", lines[Random.Range(0, lines.Length)]).Replace('_', ' '));
            if (IsUndead && Game.AI != null && !Game.AI.Npcs.Exists(o => o && o != this && o.Prisoner && o.IsUndead && o.State != NpcState.Captive))
                Game.UI?.Toast("Fledglings cannot cross sunstone light. Lead them out, or turn them loose on the staff.");
        }

        public void SetEscortWait(bool wait)
        {
            if (State != NpcState.Escort) return;
            EscortWait = wait;
            Halt();
            GameEvents.RaiseBark(Id, wait ? "I'll wait here." : "Right behind you.");
        }

        /// <summary>Turn a freed fledgling loose: it hunts the nearest of the living until someone puts it down.</summary>
        public bool TurnLoose()
        {
            if (!IsUndead || (State != NpcState.Escort && State != NpcState.Captive)) return false;
            Sitting = false;
            Loose = true;
            SetState(NpcState.Amok);
            Rig?.SetGlow(new Color(0.35f, 0.02f, 0.02f));
            Game.Audio?.PlayAt("growl", transform.position, 0.9f, 1.3f);
            GameEvents.RaiseBark(Id, "Feed... FEED...");
            GameEvents.RaiseInteracted(Id + ".loose");
            return true;
        }

        /// <summary>A guard reaches a fleeing prisoner and puts them back in irons; they are marched back to their cell
        /// (out of any sunstone light they were caught in) and sit down there (K10).</summary>
        public void Recapture(Npc by)
        {
            if (State != NpcState.Escort) return;
            Halt();
            SetState(NpcState.Captive);
            Sitting = false;
            _toCell = true;
            EscortWait = false;
            Game.Audio?.PlayAt("chain", transform.position + Vector3.up, 0.8f, 0.8f);
            if (by) { GameEvents.RaiseBark(by.Id, "Back in irons, you."); by.Wary = true; by.EnterSearching(transform.position, false); }
            Game.UI?.Toast(DisplayName + " has been recaptured.");
        }

        /// <summary>The escort objective's area: they slip away into the night.</summary>
        public void Escape()
        {
            if (State != NpcState.Escort || Disposed) return;
            Halt();
            Fx.Smoke(transform.position + Vector3.up * 0.8f, new Color(0.1f, 0.1f, 0.14f, 0.6f), 0.9f, 1.4f);
            Disposed = true;
            Hidden = true;
            gameObject.SetActive(false);
            Game.UI?.Toast(DisplayName + " slips away into the night.");
            GameEvents.RaiseInteracted(Id + ".out");
        }

        // ------------------------------------------------------------------ ticks
        void TickCaptive(float dt)
        {
            // away from the cell (just recaptured, or loaded mid-march): walk back to it, then settle as before
            if (!_toCell && Util.FlatDistance(_post, transform.position) > 1.5f) _toCell = true;
            if (_toCell)
            {
                if (Util.FlatDistance(_post, transform.position) > 0.6f && _t < 60f && MoveTo(_post, false, 0.8f) && !Stuck(dt)) return;
                _toCell = false;
                Sitting = _sitsInCell;
                transform.rotation = Quaternion.Euler(0f, _postYaw, 0f);
            }
            Halt();
        }

        void TickEscort(float dt)
        {
            var p = Game.Player;
            if (EscortGuardsCheck(dt) || p == null) return;
            if (IsUndead)
            {
                if (SunstoneBurn(dt) || FleeBurn()) return;
            }
            if (EscortWait) { Halt(); return; }

            var feet = p.transform.position;
            float d = Util.FlatDistance(feet, transform.position);
            // several followers string out behind her in a line instead of all crowding the same spot
            float keep = 2.2f + EscortSlot() * 1.1f;
            if (d <= keep && Mathf.Abs(feet.y - transform.position.y) < 1.5f) { Halt(); FaceTowards(feet, 180f); return; }
            // she is above (roofs, ledges): they can only wait at the nearest street point beneath her
            if (!NavMesh.SamplePosition(feet, out var hit, 1.6f, Filter))
            {
                _roofCd -= dt;
                if (!NavMesh.SamplePosition(feet, out hit, 6f, Filter)) { Halt(); return; }
                if (_roofCd <= 0f && Util.FlatDistance(hit.position, transform.position) < 2f)
                {
                    _roofCd = 25f;
                    GameEvents.RaiseBark(Id, IsUndead ? "We cannot climb, mistress." : "I can't follow you up there.");
                }
            }
            var to = hit.position;
            if (Util.FlatDistance(to, transform.position) < keep - 0.4f) { Halt(); return; }
            _balkCd -= dt;
            if (_balkHold > 0f) { _balkHold -= dt; Halt(); return; }
            if (!MoveTo(to, d > 6f, 0.95f)) { Halt(); return; }
            // a fledgling balks at the edge of burning light rather than walk into it; it holds there and re-tries now and then
            if (IsUndead && Game.Lights != null && Agent && Agent.enabled)
            {
                var dir = (Agent.hasPath ? Agent.steeringTarget : to) - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f && Game.Lights.BurnAt(transform.position + dir.normalized * 1.2f) > 0f)
                {
                    Halt();
                    _balkHold = 1.5f;
                    FaceTowards(transform.position + dir, 180f);
                    if (_balkCd <= 0f) { _balkCd = 12f; GameEvents.RaiseBark(Id, "The light - I can't pass it!"); }
                }
            }
        }

        /// <summary>This follower's place in the line behind her: how many following prisoners come before it.</summary>
        int EscortSlot()
        {
            int slot = 0;
            foreach (var o in Game.AI.Npcs)
            {
                if (o == this) break;
                if (o && o.State == NpcState.Escort && !o.EscortWait && !o.Escaped && o.IsAlive && o.gameObject.activeInHierarchy) slot++;
            }
            return slot;
        }

        /// <summary>Caught in burning light (a lamp relit on it, or it overran the edge): it bolts back to the last dark
        /// spot it stood on. True while it is fleeing.</summary>
        bool FleeBurn()
        {
            if (Game.Lights == null) return false;
            if (Game.Lights.BurnAt(transform.position) <= 0f) { _safePos = transform.position; _hasSafe = true; return false; }
            if (!_hasSafe) return false;
            var away = _safePos - transform.position; away.y = 0f;
            return MoveTo(_safePos + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.zero), true);
        }

        /// <summary>Burning light (sunstone, holy) eats a fledgling caught inside it. True when it died.</summary>
        bool SunstoneBurn(float dt)
        {
            float b = Game.Lights != null ? Game.Lights.BurnAt(transform.position) : 0f;
            if (b <= 0f) { _burnAcc = Mathf.Max(0f, _burnAcc - dt * 0.2f); return false; }
            _burnAcc += dt * (0.6f + b);
            if (Random.value < dt * 4f) Fx.Smoke(transform.position + Vector3.up * 1.2f, new Color(0.3f, 0.25f, 0.2f, 0.5f), 0.5f, 0.8f);
            if (_burnAcc < 1f) return false;
            _burnAcc = 0f;
            HP--;
            Game.Audio?.PlayAt("burn", transform.position + Vector3.up, 0.8f);
            if (HP > 0) { GameEvents.RaiseBark(Id, "It BURNS!"); return false; }
            Fx.Smoke(transform.position + Vector3.up, new Color(0.2f, 0.18f, 0.16f, 0.8f), 1.4f, 2.5f);
            Die("combat");
            Game.UI?.Toast(DisplayName + " burned in the sunstone light.");
            return true;
        }

        /// <summary>A guard within arm's reach takes a freed prisoner back. True when that happened.</summary>
        bool EscortGuardsCheck(float dt)
        {
            _escortScan -= dt;
            if (_escortScan > 0f) return false;
            _escortScan = 0.25f;
            if (Game.AI == null) return false;
            foreach (var o in Game.AI.Npcs)
            {
                if (!o || o == this || !o.IsAlive || !o.CanSee || o.Rescue || o.Friendly || o.IsThrall || o.State == NpcState.Panicked || !o.gameObject.activeInHierarchy) continue;
                if (o.Arch.Faction == Faction.None || o.Arch.Morale == Morale.Civilian) continue;
                if (Util.FlatDistance(o.transform.position, transform.position) > 1.6f || Mathf.Abs(o.transform.position.y - transform.position.y) > 1.5f) continue;
                Recapture(o);
                return true;
            }
            return false;
        }

        Npc _amokShun; float _amokShunT;

        void TickAmok(float dt)
        {
            _amokShunT -= dt;
            if (SunstoneBurn(dt) || FleeBurn()) { _amokVictim = null; return; }
            _amokScan -= dt;
            if (_amokScan <= 0f || !_amokVictim || !_amokVictim.IsAlive || _amokVictim.Incapacitated)
            {
                _amokScan = 1f;
                var prev = _amokVictim;
                _amokVictim = NearestPrey(30f);
                // the unarmed see it coming and run; the armed stand and fight
                if (_amokVictim && _amokVictim != prev && !_amokVictim.Armed && _amokVictim.CanSee
                    && Util.FlatDistance(_amokVictim.transform.position, transform.position) < 10f)
                    _amokVictim.EnterPanic(transform.position);
            }
            var v = _amokVictim;
            if (!v) { Halt(); return; }
            var vp = v.transform.position;
            if (Util.FlatDistance(vp, transform.position) > 1.2f)
            {
                _amokBite = 0f;
                if (!MoveTo(vp, true, 1.2f)) { _amokVictim = null; return; }
                // even in a frenzy it will not run into burning light: it gives that one up and looks for another
                if (Agent && Agent.hasPath && Game.Lights != null)
                {
                    var dir = Agent.steeringTarget - transform.position; dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f && Game.Lights.BurnAt(transform.position + dir.normalized * 1.2f) > 0f)
                    { Halt(); _amokShun = v; _amokShunT = 8f; _amokVictim = null; _amokScan = 0.5f; }
                }
                return;
            }
            Halt();
            FaceTowards(vp, 720f);
            if (v.Armed && (v.State == NpcState.Alerted || v.State == NpcState.Searching))
            {
                // it throws itself at a man with a weapon who is ready for it, and dies on it; the unready ones it takes
                Game.Audio?.PlayAt("hit", Eye, 0.9f);
                GameEvents.RaiseBark(v.Id, "Get off me - DIE!");
                v.Wary = true;
                v.EnterSearching(transform.position, true);
                Fx.BloodBurst(Eye, 0.6f);
                Die("combat");
                return;
            }
            _amokBite += dt;
            if (_amokBite < 1.4f) return;
            _amokBite = 0f;
            Fx.BloodBurst(v.Eye, 1f);
            Game.Audio?.PlayAt("scream", v.Eye, 1f);
            Game.Noise?.Emit(vp, 16f, NoiseKind.Scream, v);
            Evidence.SpawnStain(vp, Game.Level.DynamicRoot, false);
            v.Die("fledgling");
            _amokVictim = null;
        }

        Npc NearestPrey(float range)
        {
            if (Game.AI == null) return null;
            Npc best = null; float bd = range;
            foreach (var o in Game.AI.Npcs)
            {
                if (!o || o == this || !o.IsAlive || o.Incapacitated || o.Rescue || o.Friendly || o.IsThrall || o.Hidden || o == _amokShun && _amokShunT > 0f) continue;
                if (o.Arch.Has(ArchFlags.Quadruped) || o.IsUndead || !o.gameObject.activeInHierarchy) continue;
                float d = Util.FlatDistance(o.transform.position, transform.position);
                if (d < bd && Mathf.Abs(o.transform.position.y - transform.position.y) < 2.5f) { bd = d; best = o; }
            }
            return best;
        }

        // ------------------------------------------------------------------ what the guards make of it
        /// <summary>
        /// ScanEvidence: a guard who sees a freed prisoner gives chase (searching around them, so a careless escort
        /// leads the hunt to Ilse); one who sees a loose fledgling shoots it if he can, and an unarmed witness flees.
        /// True when something was seen.
        /// </summary>
        bool ScanRescues()
        {
            var ai = Game.AI;
            var v = Vision;
            foreach (var o in ai.Npcs)
            {
                if (!o || o == this || (o.State != NpcState.Escort && o.State != NpcState.Amok) || o.Disposed || !o.gameObject.activeInHierarchy) continue;
                var op = o.transform.position;
                float d = Util.FlatDistance(op, transform.position);
                if (d > v.FarRange) continue;
                float light = Game.Lights != null ? Game.Lights.LightAt(op + Vector3.up * 0.9f, 0.3f) : 0.5f;
                if (Game.Level != null && Game.Level.InFoliage(op)) light *= 0.25f;
                float range = o.State == NpcState.Amok ? v.FarRange : light >= v.LitThreshold ? v.FarRange : v.NearRange;
                if (d > range) continue;
                if (d > v.Peripheral && Vector3.Angle(Forward, (op - transform.position).Flat()) > v.HalfAngle) continue;
                if (!LineOfSight(o.Eye)) continue;
                if (o.State == NpcState.Amok)
                {
                    if (Ranged && d < 16f)
                    {
                        FaceTowards(op, 9999f);
                        Fx.MuzzleFlash(Eye + Forward * 0.5f, (o.Eye - Eye).normalized);
                        Game.Audio?.PlayAt("gunshot", Eye, 1f);
                        Game.Noise?.Emit(transform.position, 30f, NoiseKind.Gunshot, this);
                        GameEvents.RaiseBark(Id, "Put it down!");
                        o.Die("combat");
                        Wary = true;
                        EnterSearching(op, false);
                        return true;
                    }
                    if (Armed) { GameEvents.RaiseBark(Id, "One of the subjects is loose!"); Wary = true; EnterInvestigating(op, true, true); return true; }
                    EnterPanic(op);
                    return true;
                }
                if (Arch.Faction == Faction.None || Arch.Morale == Morale.Civilian)
                {
                    // staff who are no use in a fight run for the guards (and draw them)
                    if (State == NpcState.Relaxed) { GameEvents.RaiseBark(Id, "The prisoners are out! Guards!"); EnterPanic(op); return true; }
                    continue;
                }
                if (State == NpcState.Searching) { _target = op; return true; }
                Say(BarkKind.Spotted, true);
                GameEvents.RaiseBark(Id, o.IsUndead ? "A subject's out of its cell!" : "Prisoner loose! Stop there!");
                GameEvents.RaiseEvidence(this, "prisoner");
                Wary = true;
                EnterSearching(op, true);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The prisoner's handle: "Break the shackles" (Ilse or a thrall), then "Tell to wait / Tell to follow".
    /// Not clickable on its own (the hover goes through the prisoner); follows them around. Id: <c>&lt;npc&gt;.free</c>.
    /// </summary>
    public class Shackles : Interactable
    {
        public Npc Owner;
        bool Captive => Owner && Owner.State == NpcState.Captive;
        bool Escorting => Owner && Owner.State == NpcState.Escort && !Owner.Disposed;

        public override string Verb => Captive ? "Break the shackles" : Owner && Owner.EscortWait ? "Tell to follow" : "Tell to wait";
        public override string DisplayName => Owner ? Owner.DisplayName : "Prisoner";
        public override float Duration => Captive ? 2.2f : 0.1f;
        public override float NoiseRadius => Captive ? 5f : 0f;
        public override bool Repeatable => true;
        public override bool PlayerCan => Enabled && !Sealed && (Captive || Escorting);
        public override bool ThrallCan => Enabled && Captive;
        public override bool ShowMarker => Enabled && (Captive || Escorting) && Owner.gameObject.activeInHierarchy;
        /// <summary>A freed fledgling can be turned loose instead of led out.</summary>
        public bool CanLoose => Owner && Owner.IsUndead && (Captive || Escorting);

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            Click.enabled = false;   // hovering the prisoner selects this
            UseRange = 1.6f;
        }

        void LateUpdate() { if (Owner) transform.position = Owner.transform.position; }

        protected override void OnUse(bool byPlayer)
        {
            if (!Owner) return;
            if (Captive) Owner.Free(!byPlayer);
            else if (Escorting) Owner.SetEscortWait(!Owner.EscortWait);
        }
    }
}
