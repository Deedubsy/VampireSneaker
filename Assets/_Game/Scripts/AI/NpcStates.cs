using UnityEngine;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Stealth;

namespace Vespertine.AI
{
    public partial class Npc
    {
        bool _investigateAfter, _becomeWary, _run, _looking;
        float _lookT;
        bool _returning;

        // ------------------------------------------------------------------ transitions
        void SetState(NpcState s)
        {
            if (State == NpcState.Mesmerised && s != NpcState.Mesmerised || State == NpcState.Thrall && s != NpcState.Thrall) Rig?.ClearGlow();
            State = s;
            _t = 0f;
            _looking = false;
            _lookT = 0f;
        }

        public void ResumeRoutine()
        {
            SetState(NpcState.Relaxed);
            _returning = true;
            _wakeTarget = null;
            _relightTarget = null;
            _bellTarget = null;
            _lookAt = null;
            Halt();
        }

        public void EnterSuspicious(Vector3 at, float duration = 2.5f, bool alwaysInvestigate = false)
        {
            if (Routed || State == NpcState.Alerted || State == NpcState.Searching || State == NpcState.Panicked || Incapacitated || State == NpcState.Thrall || State == NpcState.Mesmerised || Rescue) return;
            if (State == NpcState.Investigating) { _target = at; return; }
            bool wasSus = State == NpcState.Suspicious;
            SetState(NpcState.Suspicious);
            _target = at;
            _stateDuration = duration;
            _investigateAfter = alwaysInvestigate || duration < 2f;
            Halt();
            if (!wasSus)
            {
                Say(BarkKind.Suspicious);
                Game.Audio?.PlayAt("sting_suspicious", Eye, 0.5f, 1f, 30f);
            }
        }

        public void EnterInvestigating(Vector3 at, bool run, bool becomeWary)
        {
            if (Routed || State == NpcState.Alerted || State == NpcState.Panicked || Incapacitated || State == NpcState.Thrall || State == NpcState.Mesmerised || Rescue) return;
            if (State == NpcState.Searching) { _target = at; return; }
            if (ForwardToLeader(at, false, run)) return;
            if (State != NpcState.Investigating) Say(BarkKind.Investigate);
            SetState(NpcState.Investigating);
            _target = at;
            _run = run;
            _becomeWary = becomeWary;
            _stateDuration = Random.Range(4f, 6f);
        }

        /// <summary>Player fully detected.</summary>
        public void Spot(Vector3 lkp)
        {
            if (Routed || Incapacitated || State == NpcState.Thrall || Rescue) return;
            LastKnown = lkp;
            Detection = 1f;
            Wary = true;
            Asleep = false;
            Sitting = false;
            if (Arch.Morale == Morale.Civilian || (Arch.Morale == Morale.Low && !Arch.Armed))
            {
                // Dread presence (Awakening 9): a citizen who sees her runs without a sound
                bool dread = DreadSilences;
                if (!dread) Say(BarkKind.Spotted, true);
                EnterPanic(lkp, dread);
                return;
            }
            SetState(NpcState.Alerted);
            Say(BarkKind.Spotted, true);
            Game.Audio?.PlayAt("sting_alert", Eye, 0.7f, 1f, 60f);
            if (_shoutCd <= 0f)
            {
                _shoutCd = 6f;
                Game.AI?.Shout(this, lkp);
            }
            _aim = 0f;
            // unarmed responders run for the alarm bell
            if (!Arch.Armed || Arch.Id == "priest") TryClaimBell();
        }

        /// <summary>Called by others' shouts: join the hunt.</summary>
        public void Alert(Vector3 lkp, Npc by)
        {
            if (Routed || Incapacitated || State == NpcState.Thrall || State == NpcState.Mesmerised || !IsAlive || Rescue) return;
            if (Asleep) WakeFromSleep();
            Wary = true;
            LastKnown = lkp;
            if (Arch.Morale == Morale.Civilian) { EnterPanic(lkp); return; }
            if (State == NpcState.Alerted) return;
            if (State == NpcState.Panicked) return;
            EnterSearching(lkp, true);
        }

        public void EnterSearching(Vector3 at, bool run)
        {
            if (Routed || Incapacitated || State == NpcState.Thrall || State == NpcState.Mesmerised || !IsAlive || Rescue) return;
            if (Arch.Morale == Morale.Civilian) { EnterPanic(at); return; }
            if (ForwardToLeader(at, true, run)) return;
            bool was = State == NpcState.Searching;
            SetState(NpcState.Searching);
            LastKnown = at;
            _target = at;
            _run = run;
            _stateDuration = Random.Range(30f, 60f) * Difficulties.Current.Search;
            _searchPts.Clear();
            _searchPts.Add(at);
            if (Game.AI != null) _searchPts.AddRange(Game.AI.SearchPoints(at, Random.Range(3, 6), 7f));
            _searchIdx = 0;
            if (!was) Say(BarkKind.Search);
            if (Arch.Has(ArchFlags.Officer)) Game.AI?.AssignSearchers(this, at);
            if (Arch.Has(ArchFlags.Flares)) _flareCd = 0.5f;
        }

        /// <summary>Awakening 9: civilians who see Ilse flee silently instead of screaming.</summary>
        bool DreadSilences => Arch.Morale == Morale.Civilian && Game.Player != null && Game.Player.Awakening >= 9;

        public void EnterPanic(Vector3 from, bool silent = false)
        {
            if (Incapacitated || State == NpcState.Thrall || !IsAlive || Rescue) return;
            bool was = State == NpcState.Panicked;
            SetState(NpcState.Panicked);
            _target = from;
            _stateDuration = 20f;
            Asleep = false; Sitting = false;
            if (!was && !silent)
            {
                Say(BarkKind.Panic, true);
                Game.Noise?.Emit(transform.position, 18f, NoiseKind.Scream, this);
            }
            PickFleePoint();
        }

        public void EnterDistracted(Vector3 at, float lookTime, bool becomeWary)
        {
            if (Routed || Incapacitated || State == NpcState.Thrall || !IsAlive || State == NpcState.Alerted || State == NpcState.Panicked || Rescue) return;
            SetState(NpcState.Distracted);
            _target = at;
            _stateDuration = lookTime;
            _becomeWary = becomeWary;
        }

        public void HoldAt(Vector3 at, float duration)
        {
            if (Incapacitated || !IsAlive) return;
            SetState(NpcState.Holding);
            _target = at;
            _stateDuration = duration;
        }

        public void BeginRelight(GameLight l)
        {
            if (Incapacitated || !IsAlive || State == NpcState.Alerted || State == NpcState.Panicked) return;
            SetState(NpcState.Relighting);
            _relightTarget = l;
        }

        public void WakeFromSleep()
        {
            if (!Asleep) return;
            Asleep = false;
            Game.Audio?.PlayAt("huh", Eye, 0.4f);
        }

        // ------------------------------------------------------------------ per-state tick
        void Tick(float dt)
        {
            if (Carried) return;
            if (_reload > 0f) _reload -= dt * (Game.AI != null ? Game.AI.TimeScaleFor(this) : 1f);
            if (SquadRingTick(dt)) return;
            switch (State)
            {
                case NpcState.Relaxed: TickRelaxed(dt); break;
                case NpcState.Suspicious: TickSuspicious(dt); break;
                case NpcState.Investigating: TickInvestigating(dt); break;
                case NpcState.Alerted: TickAlerted(dt); break;
                case NpcState.Searching: TickSearching(dt); break;
                case NpcState.Panicked: TickPanicked(dt); break;
                case NpcState.Dazed: if (_t >= _stateDuration) WakeUp(true); break;
                case NpcState.Mesmerised: TickMesmerised(dt); break;
                case NpcState.Thrall: TickThrall(dt); break;
                case NpcState.Distracted: TickDistracted(dt); break;
                case NpcState.Relighting: TickRelight(dt); break;
                case NpcState.Holding: TickHolding(dt); break;
                case NpcState.Captive: TickCaptive(dt); break;
                case NpcState.Escort: TickEscort(dt); break;
                case NpcState.Amok: TickAmok(dt); break;
            }
        }

        Vector3 WaypointWorld(int i)
        {
            var w = _route.Points[i];
            var lvl = Game.Level;
            return lvl.Data.CellToWorld(w.X, w.Y, lvl.SurfaceHeightCell(w.X, w.Y));
        }

        void TickRelaxed(float dt)
        {
            if (Asleep) { Halt(); return; }
            // a paired patrol notices its partner is missing
            if (Partner != null && !_returning)
            {
                bool missing = !Partner || Partner.Incapacitated || Partner.Hidden || Partner.Disposed || !Partner.gameObject.activeInHierarchy
                               || Util.FlatDistance(Partner.transform.position, transform.position) > 14f;
                _partnerMissingT = missing ? _partnerMissingT + dt : 0f;
                if (_partnerMissingT > 6f)
                {
                    _partnerMissingT = -30f;
                    Say(BarkKind.Partner, true);
                    var where = Partner ? Partner.transform.position : transform.position + Forward * 6f;
                    if (Partner && Partner.IsBody && !Partner.Hidden && !Partner.Disposed) EnterInvestigating(where, true, true);
                    else EnterSearching(where, false);
                    return;
                }
            }
            RoutineTick(dt);
        }

        bool _heeling, _casting;
        Vector3 _castPt;

        // ------------------------------------------------------------------ the hunt
        /// <summary>hunts[=s] (script `hunt id on|off`): a tracker who needs no alarm to find her. Every few seconds
        /// she takes a fresh fix on Ilse's scent, a point somewhere near her, and walks there with her hounds at heel.
        /// Successive fixes tighten; mist or a hiding place breaks the scent.</summary>
        public bool Hunting;
        float _huntT = 4f, _huntLook;
        Vector3 _huntPt;
        bool _hasHunt;
        int _huntFixes;

        /// <summary>Arrived at a fix and searching: her hounds range out from her (and come back to heel after a while).</summary>
        public bool CastingAbout => Hunting && _hasHunt && _huntLook > HuntMath.CastDelay && _huntLook < HuntMath.CastDelay + HuntMath.CastTime;
        public Vector3 HuntPoint => _huntPt;

        /// <summary>Script `hunt id now`: she knows where Ilse went (a scripted give-away). An immediate, tight fix.</summary>
        public void HuntNow() { Hunting = true; _huntT = 0f; _huntFixes += 3; }

        bool HuntTick(float dt)
        {
            var p = Game.Player;
            if (p == null || p.Dead || Game.AI == null) return false;
            _huntT -= dt;
            if (_huntT <= 0f)
            {
                float every = Spec != null ? Spec.OptFloat("hunts", 0f) : 0f;
                _huntT = every > 0f ? every : HuntMath.DefaultInterval;
                if (p.Concealed || p.InMist)
                {
                    // the scent is gone: she gives up this trail and goes back to her post until the next fix
                    if (_huntFixes > 0) { Say(BarkKind.HuntLost, true); _hasHunt = false; }
                    _huntFixes = 0;
                }
                else
                {
                    float r = HuntMath.Radius(Util.FlatDistance(p.Feet, transform.position), p.Bleeding, Weather.Raining, _huntFixes);
                    var pts = Game.AI.SearchPoints(p.Feet, 1, r);
                    if (pts.Count > 0)
                    {
                        _huntPt = pts[0];
                        _hasHunt = true;
                        _huntLook = 0f;
                        _huntFixes++;
                        Say(BarkKind.Hunt, _huntFixes == 1);
                    }
                }
            }
            if (!_hasHunt) return false;
            if (_huntLook <= 0f && Util.FlatDistance(_huntPt, transform.position) > 1.2f)
            {
                if (MoveTo(_huntPt, false) && !Stuck(dt)) return true;
            }
            // there, or as close as the ground allows: cast about until the next fix
            if (_huntLook <= 0f) Halt();
            _huntLook += dt;
            FaceYaw(transform.eulerAngles.y + 40f, 45f);
            return true;
        }

        /// <summary>Walk at the leader's heel (follow=), else follow the route, or stand at the post.</summary>
        void RoutineTick(float dt)
        {
            if (Hunting && HuntTick(dt)) return;
            if (SquadFollowTick(dt)) return;
            if (Leader && Leader.IsAlive && !Leader.Incapacitated && !Leader.Carried && Leader.gameObject.activeInHierarchy)
            {
                // the handler has stopped to search: range out and sniff, leaving her alone for a while
                if (Leader.CastingAbout && Game.AI != null)
                {
                    if (!_casting)
                    {
                        _casting = true;
                        var pts = Game.AI.SearchPoints(Leader.HuntPoint, 1, HuntMath.CastRadius);
                        _castPt = pts.Count > 0 ? pts[0] : transform.position;
                    }
                    if (Util.FlatDistance(_castPt, transform.position) > 0.8f && MoveTo(_castPt, false) && !Stuck(dt)) return;
                    Halt();
                    FaceYaw(transform.eulerAngles.y - 60f, 90f);
                    return;
                }
                _casting = false;
                var lf = Leader.Forward;
                var heel = Leader.transform.position - lf * 1.4f + Vector3.Cross(Vector3.up, lf) * 0.9f;
                float d = Util.FlatDistance(heel, transform.position);
                // hysteresis: settle at heel, set off again once the handler has walked on a little
                if (d > (_heeling ? 0.7f : 2f) && MoveTo(heel, d > 6f, d > 3f ? 1.25f : 1f)) { _heeling = true; return; }
                if (_heeling) Halt();
                _heeling = false;
                FaceYaw(Util.DirToFacing(lf), 240f);
                return;
            }
            if (_route != null && _route.Points.Count > 0 && !_routeDone)
            {
                var wp = _route.Points[_wp];
                var pos = WaypointWorld(_wp);
                if (_returning || _wpWait <= 0f)
                {
                    if (Util.FlatDistance(pos, transform.position) > 0.45f || Mathf.Abs(pos.y - transform.position.y) > 1f)
                    {
                        if (!MoveTo(pos, false) || Stuck(dt)) AdvanceWaypoint();
                        return;
                    }
                    _returning = false;
                    if (_wpWait <= 0f)
                    {
                        _wpWait = Mathf.Max(0.01f, wp.Wait);
                        Halt();
                    }
                }
                if (_wpWait > 0f)
                {
                    if (wp.Look.HasValue) FaceYaw(wp.Look.Value, 180f);
                    _wpWait -= dt;
                    if (_wpWait <= 0f) AdvanceWaypoint();
                }
                return;
            }
            // stationary post
            if (Util.FlatDistance(_post, transform.position) > 0.5f || Mathf.Abs(_post.y - transform.position.y) > 1f)
            {
                if (MoveTo(_post, false) && !Stuck(dt)) return;
            }
            _returning = false;
            Halt();
            if (Spec != null && Spec.Has("scan"))
                FaceYaw(_postYaw + Mathf.Sin(Time.time * 0.35f + _seedCounter) * 60f, 60f);
            else FaceYaw(_postYaw, 180f);
        }

        void AdvanceWaypoint()
        {
            _wpWait = 0f;
            int n = _route.Points.Count;
            if (n <= 1) { _routeDone = true; return; }
            switch (_route.Mode)
            {
                case RouteMode.Loop: _wp = (_wp + 1) % n; break;
                case RouteMode.PingPong:
                    if (_wp + _wpDir >= n || _wp + _wpDir < 0) _wpDir = -_wpDir;
                    _wp += _wpDir;
                    break;
                case RouteMode.Once:
                    if (_wp + 1 >= n) { _routeDone = true; _post = transform.position; _postYaw = transform.eulerAngles.y; }
                    else _wp++;
                    break;
            }
        }

        void TickSuspicious(float dt)
        {
            if (SeesPlayer) _target = LastKnown;
            FaceTowards(_target, 240f);
            if (Detection <= 0.01f && _t > 1.5f && !_investigateAfter)
            {
                Say(BarkKind.GiveUp);
                ResumeRoutine();
                return;
            }
            if (_t >= _stateDuration && (_investigateAfter || Detection >= 0.2f))
                EnterInvestigating(_target, false, true);
            else if (_t >= _stateDuration + 2f)
            {
                Say(BarkKind.GiveUp);
                ResumeRoutine();
            }
        }

        void TickInvestigating(float dt)
        {
            if (!_looking)
            {
                if (_wakeTarget != null && _wakeTarget && Util.FlatDistance(_wakeTarget.transform.position, transform.position) < 1.6f)
                {
                    Halt();
                    var t = _wakeTarget;
                    _wakeTarget = null;
                    if (t.State == NpcState.Dazed && !t.Carried && !t.Hidden) t.WakeUp(true, this);
                    EnterSearching(t.transform.position, false);
                    return;
                }
                bool ok = MoveTo(_target, _run);
                if (!ok || Arrived || Stuck(dt) || _t > 25f)
                {
                    Halt();
                    _looking = true;
                    _lookT = 0f;
                    _lookBase = transform.eulerAngles.y;
                }
                return;
            }
            _lookT += dt;
            FaceYaw(_lookBase + Mathf.Sin(_lookT * 1.3f) * 75f, 150f);
            if (_lookT >= _stateDuration)
            {
                if (_becomeWary) Wary = true;
                Say(BarkKind.GiveUp);
                ResumeRoutine();
            }
        }

        // ------------------------------------------------------------------ combat
        void TickAlerted(float dt)
        {
            var p = Game.Player;
            if (p == null || p.Dead) { EnterSearching(LastKnown, false); return; }

            if (_bellTarget != null)
            {
                if (TickBell(dt)) return;
            }

            float tscale = Game.AI != null ? Game.AI.TimeScaleFor(this) : 1f;
            var pp = p.transform.position;
            float d = Vector3.Distance(pp, transform.position);
            bool clear = SeesPlayer || (_sinceSeen < 0.6f && LineOfSight(pp + Vector3.up * 1.2f, false));

            if (Arch.Ranged)
            {
                float range = Arch.Weapon == Weapon.Pistol ? 12f : 20f;
                if (clear && d <= range && !p.Concealed)
                {
                    Halt();
                    FaceTowards(pp, 540f);
                    if (_reload <= 0f)
                    {
                        _aim += dt * tscale;
                        if (_aim >= 0.8f) Fire(p, d);
                    }
                    else _aim = 0f;
                }
                else
                {
                    _aim = 0f;
                    MoveTo(LastKnown, true);
                }
            }
            else if (Arch.Armed)
            {
                _aim = 0f;
                if (d > 1.5f || !clear) MoveTo(clear ? pp : LastKnown, true);
                else
                {
                    Halt();
                    FaceTowards(pp, 720f);
                    _melee -= dt * tscale;
                    if (_melee <= 0f)
                    {
                        _melee = 1.2f;
                        float dmg = Arch.Weapon == Weapon.Bite ? 8f : Arch.Weapon == Weapon.Halberd ? 14f : Arch.Weapon == Weapon.Cudgel ? 6f : 10f;
                        Game.Audio?.PlayAt(Arch.Weapon == Weapon.Bite ? "growl" : "hit", pp + Vector3.up, 0.9f);
                        p.Damage(dmg * Difficulties.Current.DamageTaken, Arch.Has(ArchFlags.Silver), this);
                    }
                }
            }
            else
            {
                // unarmed non-civilians (priest, lamplighter): keep distance, point and shout
                FaceTowards(pp, 360f);
                if (d < 5f) MoveTo(transform.position + (transform.position - pp).Flat().normalized * 4f, true);
                else Halt();
                if (Arch.Has(ArchFlags.HolyAura) && d < 8f) { /* holy aura burns via LightSystem / Vampire */ }
            }

            if (_sinceSeen > 4f)
            {
                Detection = Mathf.Min(Detection, 0.9f);
                if (Arch.Armed && _bellTarget == null) TryClaimBell();
                if (_bellTarget == null) EnterSearching(LastKnown, true);
            }
        }

        void Fire(Player.Vampire p, float d)
        {
            _aim = 0f;
            bool silver = Arch.Has(ArchFlags.Silver);
            _reload = Arch.Weapon == Weapon.Musket ? 5f : Arch.Weapon == Weapon.Pistol ? 3.5f : 4f;
            float chance = DetectionMath.HitChance(d, p.Light, Arch.Weapon == Weapon.Pistol ? 12f : 20f);
            if (p.Rushing) chance -= 0.2f;
            if (Wary) chance += 0.05f;
            bool hit = Random.value < chance;
            var muzzle = Eye + Forward * 0.6f - Vector3.up * 0.2f;
            if (Arch.Weapon == Weapon.Crossbow)
            {
                Game.Audio?.PlayAt("swish", muzzle, 0.9f);
                Game.Noise?.Emit(transform.position, 6f, NoiseKind.Object, this);
            }
            else
            {
                Game.Audio?.PlayAt("gunshot", muzzle, 1f, Random.Range(0.92f, 1.06f), 90f);
                Game.Noise?.Emit(transform.position, 30f, NoiseKind.Gunshot, this);
                Visual.Fx.MuzzleFlash(muzzle, Forward);
            }
            Visual.Fx.Tracer(muzzle, p.transform.position + Vector3.up * (hit ? 1.1f : 1.1f + Random.Range(-0.8f, 0.8f)) + (hit ? Vector3.zero : Random.insideUnitSphere * 1.2f), silver);
            if (hit)
            {
                p.Damage((silver ? 22f : 18f) * Difficulties.Current.DamageTaken, silver, this);
                if (Random.value < 0.4f) Say(BarkKind.Hit);
            }
            else Game.Audio?.PlayAt("swish", p.transform.position + Vector3.up, 0.5f, 1.4f);
            Game.Audio?.PlayAt("reload", transform.position, 0.3f, 1f, 15f);
        }

        void TryClaimBell()
        {
            var b = Game.AI?.NearestBell(transform.position, 20f);
            if (b != null && Game.AI.ClaimBell(b, this)) { _bellTarget = b; _bellT = 0f; }
        }

        float _bellT;
        /// <summary>Run to the bell and ring it. Returns true while busy.</summary>
        bool TickBell(float dt)
        {
            var b = _bellTarget;
            if (!b || b.Silenced || Time.time - b.LastRung < 30f) { Game.AI?.ReleaseBell(b, this); _bellTarget = null; return false; }
            var up = b.UsePoint(transform.position, NavAreas.HumanAgent);
            if (Util.FlatDistance(up, transform.position) > 1.2f)
            {
                if (!MoveTo(up, true) || Stuck(dt)) { Game.AI?.ReleaseBell(b, this); _bellTarget = null; return false; }
                return true;
            }
            Halt();
            FaceTowards(b.transform.position, 540f);
            _bellT += dt;
            if (_bellT > 1.2f)
            {
                b.Ring(transform.position);
                Game.AI?.ReleaseBell(b, this);
                _bellTarget = null;
            }
            return true;
        }

        float _flareCd;
        void TickSearching(float dt)
        {
            if (_bellTarget != null && TickBell(dt)) return;
            if (_t >= _stateDuration)
            {
                Say(BarkKind.SearchEnd);
                Wary = true;
                Detection = 0f;
                ResumeRoutine();
                return;
            }
            // hunters throw flares into suspicious darkness
            if (Arch.Has(ArchFlags.Flares))
            {
                _flareCd -= dt;
                if (_flareCd <= 0f)
                {
                    _flareCd = 14f;
                    var at = _searchPts.Count > 0 ? _searchPts[Mathf.Min(_searchIdx, _searchPts.Count - 1)] : _target;
                    if (Game.Lights != null && Game.Lights.LightAt(at) < 0.3f && Util.FlatDistance(at, transform.position) < 16f)
                    {
                        Game.AI?.ThrowFlare(this, at);
                    }
                }
            }
            if (_searchPts.Count == 0) { _searchPts.Add(_target); }
            if (!_looking)
            {
                var pt = _searchPts[_searchIdx];
                bool ok = MoveTo(pt, _run && _searchIdx == 0);
                if (!ok || Arrived || Stuck(dt))
                {
                    Halt();
                    _looking = true;
                    _lookT = 0f;
                    _lookBase = transform.eulerAngles.y;
                }
                return;
            }
            _lookT += dt;
            FaceYaw(_lookBase + Mathf.Sin(_lookT * 1.6f) * 90f, 200f);
            if (_lookT > 2.6f)
            {
                _looking = false;
                _searchIdx = (_searchIdx + 1) % _searchPts.Count;
                if (_searchIdx == 0 && Game.AI != null)
                {
                    // widen the search around the last known position
                    _searchPts.Clear();
                    _searchPts.AddRange(Game.AI.SearchPoints(LastKnown, Random.Range(3, 6), 11f));
                    if (_searchPts.Count == 0) _searchPts.Add(LastKnown);
                }
            }
        }

        Vector3 _flee;
        void PickFleePoint()
        {
            _flee = Game.AI != null ? Game.AI.FleePoint(this, _target) : transform.position + (transform.position - _target).Flat().normalized * 12f;
        }

        void TickPanicked(float dt)
        {
            if (EvacTo.HasValue)
            {
                // off-site: run for the exit and leave the level for good
                var to = EvacTo.Value;
                if (Util.FlatDistance(transform.position, to) < 1.6f)
                {
                    Halt();
                    gameObject.SetActive(false);
                    GameEvents.RaiseInteracted(Id + ".evac");
                    return;
                }
                if (!MoveTo(to, true) || Stuck(dt)) { Halt(); FaceTowards(_target, 360f); }
                return;
            }
            if (_t >= _stateDuration)
            {
                Wary = true;
                Detection = 0f;
                if (Arch.Armed) EnterSearching(_target, false);
                else ResumeRoutine();
                return;
            }
            if (!MoveTo(_flee, true) || Arrived || Stuck(dt))
            {
                Halt();
                FaceTowards(_target, 360f);
                if (_t > 3f && Random.value < dt * 0.3f) PickFleePoint();
            }
            // herding: flee directly away from the vampire
            if (Game.Campaign != null && Game.Campaign.Has("predator.herd") && Game.Player != null && Random.value < dt)
            {
                _target = Game.Player.transform.position;
                PickFleePoint();
            }
        }

        void TickDistracted(float dt)
        {
            if (!_looking)
            {
                if (!MoveTo(_target, false) || Arrived || Stuck(dt) || Util.FlatDistance(_target, transform.position) < 1.2f)
                {
                    Halt();
                    _looking = true;
                    _lookT = 0f;
                    _lookBase = transform.eulerAngles.y;
                }
                return;
            }
            _lookT += dt;
            FaceYaw(_lookBase + Mathf.Sin(_lookT * 1.1f) * 60f, 120f);
            if (_lookT > _stateDuration)
            {
                if (_becomeWary) Wary = true;
                ResumeRoutine();
            }
        }

        void TickHolding(float dt)
        {
            if (Util.FlatDistance(_target, transform.position) > 0.8f && _t < 20f)
            {
                if (MoveTo(_target, false) && !Stuck(dt)) return;
            }
            Halt();
            if (_lookAt && _lookAt.IsAlive) FaceTowards(_lookAt.transform.position, 240f);
            else FaceYaw(_postYaw + Mathf.Sin(_t * 0.5f) * 45f, 60f);
            if (_t > _stateDuration) ResumeRoutine();
        }

        void TickRelight(float dt)
        {
            var l = _relightTarget;
            if (!l || l.On) { Game.AI?.LampRelit(l); ResumeRoutine(); return; }
            // a cut gas main: walk to the valve and reopen it, which relights the whole group
            var valve = l.GasCut ? Valve.ForGroup(l.LightGroup) : null;
            if (l.GasCut && (valve == null || !valve.Enabled)) { Game.AI?.LampRelit(null); ResumeRoutine(); return; }
            // smashed: nothing to relight (the lamp stays noticed, so no one else is sent)
            if (l.Broken && !l.GasCut) { GameEvents.RaiseBark(Id, "Smashed! Someone's been at the lamps."); Wary = true; ResumeRoutine(); return; }
            var lp = valve ? valve.transform.position : l.transform.position;
            if (!_looking)
            {
                if (Util.FlatDistance(lp, transform.position) > 1.6f)
                {
                    if (!MoveTo(lp, Game.AI != null && Game.AI.Lockdown) || Stuck(dt) || (Arrived && _t > 1f))
                    {
                        if (Util.FlatDistance(lp, transform.position) > 3f) { Game.AI?.LampRelit(null); ResumeRoutine(); return; }
                    }
                    else return;
                }
                Halt();
                _looking = true;
                _lookT = 0f;
            }
            FaceTowards(lp, 360f);
            _lookT += dt;
            if (_lookT >= (valve ? 2.5f : 5f))
            {
                bool lingering = Game.Campaign != null && Game.Campaign.Has("shade.smother_lingering") && l.WasSnuffedByPlayer && Time.time - l.SnuffedAt < 60f;
                if (valve)
                {
                    if (valve.State) valve.Use(false);
                    GameEvents.RaiseBark(Id, "Who shut the main?");
                }
                else if (lingering) Game.UI?.Toast(DisplayName + " cannot relight the lamp.");
                else
                {
                    l.SetOn(true);
                    Game.Audio?.PlayAt("ignite", l.transform.position, 0.5f, 1f, 22f);
                    Say(BarkKind.Relight);
                }
                Game.AI?.LampRelit(l);
                ResumeRoutine();
            }
        }

        void TickMesmerised(float dt)
        {
            Halt();
            if (_t >= _stateDuration)
            {
                Rig?.ClearGlow();
                bool forget = Game.Campaign != null && Game.Campaign.Has("dominion.mesmerize_forget");
                SetState(NpcState.Relaxed);
                _returning = true;
                if (!forget) { Wary = true; EnterSuspicious(transform.position + Forward * 3f, 3f); }
            }
        }
    }

    /// <summary>How close a hunting tracker's scent fix lands. Pure, so the tightening is unit-tested.</summary>
    public static class HuntMath
    {
        public const float DefaultInterval = 16f;
        public const float MinRadius = 3f, MaxRadius = 22f;
        /// <summary>At a fix the hounds wait this long at heel, then range this far for this long.</summary>
        public const float CastDelay = 1.5f, CastTime = 9f, CastRadius = 8f;

        /// <param name="dist">tracker to Ilse, metres</param>
        /// <param name="fixes">fixes taken in a row without losing her</param>
        public static float Radius(float dist, bool bleeding, bool raining, int fixes)
        {
            float r = Mathf.Clamp(dist * 0.4f, 6f, MaxRadius);
            if (bleeding) r *= 0.4f;          // fresh blood is a lantern to her
            if (raining) r *= 1.5f;           // rain drowns the scent
            r *= Mathf.Max(0.45f, 1f - 0.12f * fixes);
            return Mathf.Clamp(r, MinRadius, MaxRadius);
        }
    }
}
