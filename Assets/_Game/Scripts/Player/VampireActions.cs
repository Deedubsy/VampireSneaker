using System.Collections.Generic;
using UnityEngine;
using Pose = Vespertine.Visual.Pose;
using UnityEngine.AI;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.Player
{
    public partial class Vampire
    {
        // ---------------------------------------------------------------- hover
        public Npc HoverNpc;
        public Interactable HoverUse;
        public GameLight HoverLight;
        public Vector3 HoverPoint;
        public bool HoverValid;

        public void UpdateHover()
        {
            HoverNpc = null; HoverUse = null; HoverLight = null; HoverValid = false;
            if (Game.Cam == null || Game.Cam.Cam == null) return;
            if (Game.UI != null && Game.UI.PointerOverUI) return;
            var ray = Game.Cam.MouseRay();
            if (Physics.Raycast(ray, out var ground, 400f, Layers.WalkClickMask, QueryTriggerInteraction.Ignore))
            {
                HoverPoint = ground.point;
                HoverValid = true;
            }
            if (Physics.Raycast(ray, out var hit, 400f, Layers.ClickMask, QueryTriggerInteraction.Collide))
            {
                var n = hit.collider.GetComponentInParent<Npc>();
                if (n != null && !n.Hidden && !n.Disposed) HoverNpc = n;
                var u = hit.collider.GetComponentInParent<Interactable>();
                if (u != null && u.Enabled && u.ShowMarker) HoverUse = u;
                if (!HoverValid) { HoverPoint = hit.point; HoverValid = true; }
                // a prisoner is handled through their shackles (free / wait / follow)
                if (HoverNpc != null && HoverNpc.Shackles != null && HoverNpc.Shackles.ShowMarker) { HoverUse = HoverNpc.Shackles; HoverNpc = null; }
            }
            if (HoverNpc == null && HoverUse == null && Game.Lights != null)
            {
                var mouse = Game.Input != null ? Game.Input.MousePos : Vector2.zero;
                float best = 30f;
                foreach (var l in Game.Lights.All)
                {
                    if (!l || !l.On || l.Portable || l.Kind == LightKind.Moon || l.Kind == LightKind.Window) continue;
                    if (!Game.Cam.WorldToScreen(l.SourcePos, out var s)) continue;
                    float d = (s - mouse).magnitude;
                    if (d < best) { best = d; HoverLight = l; }
                }
            }
        }

        // ---------------------------------------------------------------- actions
        float _toastT;
        void Say(string msg)
        {
            if (Time.unscaledTime - _toastT < 0.6f) return;
            _toastT = Time.unscaledTime;
            Game.UI?.Toast(msg);
        }

        public void CancelAll()
        {
            _pending = null;
            if (Feeding != null) EndFeed(true);
            _useTarget = null;
            if (_channel > 0f) { _channel = 0f; _channelDone = null; }
            Stop();
        }

        /// <summary>Act at once (Gameplay Redesign P7: she moves only when the player moves her). Feed, carry, use,
        /// snuff and abilities happen if she is within reach, else she says how far it is and does nothing. Move is
        /// no longer bound to the mouse (D114) but stays for scripted runs and the dev tools. A feed pressed in
        /// mid-leap waits for the landing (drop-feed).</summary>
        public bool Act(PlayerAction a)
        {
            if (Dead || a == null) return false;
            if (Feeding != null) return false;
            if (_link != null)
            {
                if (a.Kind != ActionKind.Feed) return false;
                _pending = a;
                return true;
            }
            _useTarget = null;
            if (_channel > 0f) { _channel = 0f; _channelDone = null; }
            _pending = null;
            switch (a.Kind)
            {
                case ActionKind.Move:
                    if (!GoTo(a.Point, a.Rush)) { Say("Ilse cannot reach that place."); return false; }
                    if (a.Rush) Fx.Ring(a.Point, 0.8f, Mats.Pal.BloodBright, 0.4f); else Fx.Ring(a.Point, 0.6f, Mats.Pal.Bone, 0.35f);
                    return true;
                case ActionKind.Feed:
                    if (InMist) { Say("Mist cannot feed."); return false; }
                    if (Carrying != null) { Say("Her hands are full."); return false; }
                    break;
                case ActionKind.Carry:
                    if (InMist) { Say("Mist cannot carry."); return false; }
                    break;
                case ActionKind.Interact:
                    if (InMist && !(a.Use is HideSpot)) { Say("Mist cannot touch anything."); return false; }
                    if (a.Use == null || !a.Use.PlayerCan) { Say(a.Use != null && a.Use.Unavailable != null ? a.Use.Unavailable : "Not now."); return false; }
                    break;
                case ActionKind.Snuff:
                    if (InMist) { Say("Mist cannot touch anything."); return false; }
                    if (a.Light == null || !a.Light.CanSnuffByHand) { Say(a.Light != null && a.Light.Caged ? "Caged - it cannot be snuffed by hand." : "It cannot be put out."); return false; }
                    break;
            }
            return Resolve(a);
        }

        /// <summary>A feed held over from a leap is tried once she lands; nothing else waits.</summary>
        void TickPending(float dt)
        {
            var a = _pending;
            if (a == null || Busy) return;
            _pending = null;
            Resolve(a);
        }

        // ---------------------------------------------------------------- reach (no auto-walk, P7)
        /// <summary>Reach for lifting a body, and for a lamp snuffed by hand.</summary>
        public const float CarryReach = 1.8f, SnuffReach = 1.8f;
        /// <summary>Beyond <see cref="FeedRange"/> and up to this, a feed key lunges the last step (0.25 s).</summary>
        public const float LungeRange = 2.6f, LungeTime = 0.25f;

        public static string TooFar(float d) => $"Too far ({d:0.0} m)";

        /// <summary>How far past her reach the feed target is (0 = she can take them now, with a lunge if need be).</summary>
        public float FeedGap(Npc n)
        {
            if (!n) return float.MaxValue;
            float d = Util.FlatDistance(n.transform.position, Feet);
            if (Mathf.Abs(n.transform.position.y - Feet.y) >= 1.2f) return Mathf.Max(d, 0.01f);
            return Mathf.Max(0f, d - LungeRange);
        }

        public float CarryGap(Npc n) => !n ? float.MaxValue
            : Mathf.Abs(n.transform.position.y - Feet.y) >= 1.2f ? Mathf.Max(0.01f, Util.FlatDistance(n.transform.position, Feet))
            : Mathf.Max(0f, Util.FlatDistance(n.transform.position, Feet) - CarryReach);

        public bool InUseReach(Interactable u)
        {
            if (!u) return false;
            var up = u.UsePoint(Feet, NavAreas.VampireAgent);
            return Util.FlatDistance(up, Feet) < 0.45f || (Util.FlatDistance(u.transform.position, Feet) <= u.UseRange + UseSlack && Mathf.Abs(up.y - Feet.y) < 1f);
        }

        public bool InSnuffReach(GameLight l) => l && Util.FlatDistance(l.transform.position, Feet) <= SnuffReach && Mathf.Abs(l.transform.position.y - Feet.y) < 2.5f;

        /// <summary>Does the action now, if it is within reach; otherwise says why not.</summary>
        bool Resolve(PlayerAction a)
        {
            switch (a.Kind)
            {
                case ActionKind.Feed:
                {
                    var n = a.Npc;
                    if (!n || !n.IsAlive || n.State == NpcState.Victim) return false;
                    float d = Util.FlatDistance(n.transform.position, Feet);
                    if (FeedGap(n) > 0f) { Say(TooFar(d)); return false; }
                    if (d <= FeedRange) return BeginFeed(n, a.Drain);
                    if (!n.CanBeFedBy(this, out var why)) { Say(why); return false; }
                    if (!Lunge(n, a.Drain)) { Say(TooFar(d)); return false; }
                    return true;
                }
                case ActionKind.Carry:
                {
                    var n = a.Npc;
                    if (!n || !n.IsBody || n.Carried || n.Disposed) return false;
                    if (CarryGap(n) > 0f) { Say(TooFar(Util.FlatDistance(n.transform.position, Feet))); return false; }
                    return StartCarry(n);
                }
                case ActionKind.Interact:
                {
                    var u = a.Use;
                    if (!u || !u.Enabled) return false;
                    if (!InUseReach(u)) { Say(TooFar(Util.FlatDistance(u.transform.position, Feet))); return false; }
                    StartUse(u);
                    return true;
                }
                case ActionKind.Snuff:
                {
                    var l = a.Light;
                    if (!l || !l.On) return false;
                    if (!InSnuffReach(l)) { Say(TooFar(Util.FlatDistance(l.transform.position, Feet))); return false; }
                    FaceTowards(l.transform.position);
                    Channel(0.45f, Pose.Interact, () => SnuffByHand(l));
                    return true;
                }
                case ActionKind.Ability:
                    return ResolveAbility(a);
            }
            return false;
        }

        /// <summary>The last step to a feed: a short, straight lunge to arm's length, then the bite. Refused through walls.</summary>
        bool Lunge(Npc n, bool drain)
        {
            var np = n.transform.position;
            var dir = (np - Feet).Flat();
            if (dir.sqrMagnitude < 0.0001f) return false;
            var to = np - dir.normalized * (FeedRange * 0.7f);
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            if (NavMesh.Raycast(Feet, to, out _, filter)) return false;
            if (!NavMesh.SamplePosition(to, out var hit, 0.5f, filter)) return false;
            ManualLeap(hit.position, LungeTime, 0.1f, () => { if (n && n.IsAlive && n.State != NpcState.Victim) BeginFeed(n, drain); });
            return true;
        }

        public void FaceTowards(Vector3 p)
        {
            var d = (p - transform.position).Flat();
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }

        // ---------------------------------------------------------------- interaction
        void StartUse(Interactable u)
        {
            if (!u.PlayerCan) { Say(u.Unavailable ?? "Not now."); return; }
            Stop();
            FaceTowards(u.transform.position);
            _useTarget = u;
            _useT = 0f;
        }

        void TickUse(float dt)
        {
            if (_useTarget == null) return;
            if (!_useTarget.Enabled) { _useTarget = null; return; }
            _useT += dt;
            if (_useT >= _useTarget.Duration)
            {
                var u = _useTarget;
                _useTarget = null;
                u.Use(true);
            }
        }

        void SnuffByHand(GameLight l)
        {
            if (!l || !l.CanSnuffByHand) return;
            l.SetOn(false, true);
            Game.Campaign?.AddHabit(Progression.Habits.Snuff, 1f);
            Fx.Smoke(l.SourcePos, new Color(0.3f, 0.3f, 0.32f, 0.6f), 0.6f, 1.5f);
        }

        // ---------------------------------------------------------------- feeding
        float _feedT, _feedDur, _feedFxT;
        bool _feedDrain, _feedVeiled;
        public float FeedProgress => Feeding != null ? Mathf.Clamp01(_feedT / _feedDur) : 0f;
        public bool FeedDrain => _feedDrain;

        public static float BloodValue(BloodType t)
        {
            switch (t)
            {
                case BloodType.Drunk: return 25f;
                case BloodType.Fevered: return 15f;
                case BloodType.Soldier: return 40f;
                case BloodType.Priest: return 30f;
                case BloodType.Occult: return 30f;
                case BloodType.Notable: return 60f;
                case BloodType.Animal: return 10f;
            }
            return 30f;
        }

        public bool BeginFeed(Npc n, bool drain, bool force = false)
        {
            if (n == null || Feeding != null) return false;
            if (InMist) { Say("Mist cannot feed."); return false; }
            if (Carrying != null) { Say("Her hands are full."); return false; }
            if (!force && !n.CanBeFedBy(this, out var reason)) { Say(reason); return false; }
            if (force && (!n.IsAlive || n.State == NpcState.Victim)) return false;
            Stop();
            float dur = drain ? 3.2f : 1.6f;
            if (Has("predator.gorge")) dur *= 0.6f;
            if (Has("predator.stalker") && n.AngleTo(transform.position) > 100f) dur *= 0.7f;
            if (Time.time < _apexUntil) dur = 0.25f;
            _feedVeiled = Awakening >= 5 && n.State == NpcState.Mesmerised;
            Feeding = n;
            _feedDrain = drain;
            _feedT = 0f;
            _feedDur = dur;
            n.BeginVictim(this);
            // settle behind the victim
            var behind = n.transform.position - n.Forward * 0.55f;
            if (NavMesh.SamplePosition(behind, out var hit, 0.6f, new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas }))
                Agent.Warp(hit.position);
            FaceTowards(n.transform.position);
            Game.Audio?.PlayAt("gasp", n.transform.position, 0.5f, Random.Range(0.9f, 1.15f));
            Game.Audio?.PlayAt("feed", Feet, 0.7f);
            return true;
        }

        void TickFeeding(float dt)
        {
            var n = Feeding;
            if (n == null) return;
            if (!n || n.State != NpcState.Victim) { Feeding = null; return; }
            _feedT += dt;
            _feedFxT -= dt;
            if (_feedFxT <= 0f)
            {
                _feedFxT = 0.05f;
                Fx.BloodStream(n.transform.position + Vector3.up * 1.45f, transform.position + Vector3.up * 1.5f);
            }
            // Terror: those who witness the feed flee rather than raise the alarm.
            if (Has("predator.terror") && Game.AI != null)
                foreach (var w in Game.AI.Living())
                    if (w != n && w.SeesPlayer && w.Detection > 0.4f && w.Arch.Faction != Faction.Vigil && w.State != NpcState.Panicked)
                        w.Terrify(Feet);
            if (_feedT >= _feedDur) CompleteFeed();
        }

        void CompleteFeed()
        {
            var n = Feeding;
            bool drain = _feedDrain;
            Feeding = null;
            var diff = Difficulties.Current;
            float value = BloodValue(n.Arch.Blood);
            float gain = value * (drain ? 1f : 0.6f) * diff.BloodGain;
            if (drain && Has("predator.gorge")) gain *= 1.25f;
            AddBlood(gain);
            var c = Game.Campaign;
            if (c != null)
            {
                int lv = c.AddVitae(Mathf.RoundToInt(gain));
                if (lv > 0) { Game.UI?.Toast($"Awakening {c.Awakening}. Something in her uncoils."); Game.Audio?.Play2D("objective"); }
                if (drain) { c.TotalDrains++; c.AddHabit(Progression.Habits.Lethal, 1f); }
                else { c.TotalSips++; c.AddHabit(Progression.Habits.Sips, 1f); }
                if (drain && n.Arch.Blood == BloodType.Notable && Game.Mission != null && Game.Mission.Info != null)
                {
                    // once per notable per campaign: replaying a night cannot farm the same throat twice
                    var rec = c.Record(Game.Mission.Info.Id);
                    if (rec.Notables == null) rec.Notables = new List<string>();   // saves from before X6
                    if (!rec.Notables.Contains(n.Id)) { rec.Notables.Add(n.Id); c.Marks++; Game.UI?.Toast("+1 Mark (a notable's blood)"); }
                }
            }
            ApplyHumour(n.Arch.Blood);
            if (drain && Time.time < _apexUntil) AddBlood(15f);
            var pos = n.transform.position;
            n.EndVictim(drain);
            if (drain && !Has("sanguis.clean") && Game.Level != null) Evidence.SpawnStain(pos, Game.Level.DynamicRoot, false, 0.8f);
            if (drain && Has("predator.dread_feast") && Game.AI != null)
                foreach (var w in Game.AI.Living())
                    if (Util.FlatDistance(w.transform.position, pos) < 18f && w.CouldSee(Feet, Mathf.Max(Light, 0.5f))) { w.Terrify(Feet); w.RevealedUntil = Time.time + 20f; }
            GameEvents.RaiseNpcFed(n, drain);
            Game.Audio?.PlayAt("swish", Feet, 0.3f, 0.6f);
        }

        void EndFeed(bool interrupted)
        {
            var n = Feeding;
            Feeding = null;
            if (!n || n.State != NpcState.Victim) return;
            float frac = Mathf.Clamp01(_feedT / Mathf.Max(0.01f, _feedDur));
            AddBlood(BloodValue(n.Arch.Blood) * 0.5f * frac * Difficulties.Current.BloodGain);
            n.EndVictim(false, interrupted);
        }

        void ApplyHumour(BloodType t)
        {
            Humour = null;
            switch (t)
            {
                case BloodType.Drunk: Humour = "languid"; HumourUntil = float.MaxValue; break;
                case BloodType.Fevered: Humour = "fever"; HumourUntil = Time.time + 60f; break;
                case BloodType.Soldier: Humour = "iron"; HumourUntil = float.MaxValue; break;
                case BloodType.Priest: Humour = "scalding"; HumourUntil = Time.time + 60f; Damage(10f, false, null, true, true); break;
                case BloodType.Occult: Humour = "lucid"; HumourUntil = Time.time + 60f; break;
            }
            if (Humour != null) Game.UI?.Toast("Humour: " + HumourName(Humour));
        }

        public static string HumourName(string h)
        {
            switch (h)
            {
                case "languid": return "Languid (quieter, slower)";
                case "fever": return "Fever-sight (Blood Sense free)";
                case "iron": return "Iron (+25% max health)";
                case "scalding": return "Scalding (Sanguis half cost)";
                case "lucid": return "Lucid (Dominion half cost)";
            }
            return h;
        }

        // ---------------------------------------------------------------- bodies
        public bool StartCarry(Npc n)
        {
            if (n == null || Carrying != null || InMist) return false;
            if (!n.IsBody || n.Carried || n.Disposed) return false;
            if (n.Hidden)
            {
                var spot = Game.Level != null && n.HideSpotId != null ? Game.Level.Get<HideSpot>(n.HideSpotId) : null;
                spot?.RemoveBody(n);
                n.Hidden = false;
            }
            Stop();
            n.SetCarried(true, transform);
            Carrying = n;
            Game.Audio?.PlayAt("thud", Feet, 0.25f, 1.3f);
            return true;
        }

        public void DropBody(bool allowCanal = true)
        {
            var n = Carrying;
            if (n == null) return;
            Carrying = null;
            if (Game.Mission != null && Game.Mission.IsDeliveryDrop(n, Feet)) allowCanal = false;
            if (allowCanal && Game.Level != null && Game.Level.CanalNear(Feet, 2.6f, out var water))
            {
                n.SetCarried(false, transform);
                n.transform.position = water;
                n.Dispose("canal");
                Game.Audio?.PlayAt("splash", water, 0.8f);
                Game.Noise?.Emit(water, 7f, NoiseKind.Splash, this);
                Fx.Ring(water, 2.5f, new Color(0.5f, 0.6f, 0.7f, 0.6f), 0.9f);
                Game.UI?.Toast("The canal takes the body.");
                return;
            }
            n.SetCarried(false, transform);
            Game.Audio?.PlayAt("thud", Feet, 0.45f);
            Game.Noise?.Emit(Feet, 2.5f, NoiseKind.Body, this);
        }

        // ---------------------------------------------------------------- hiding spots
        public void UseHideSpot(HideSpot h)
        {
            if (h == null) return;
            if (Carrying != null)
            {
                var n = Carrying;
                Carrying = null;
                n.SetCarried(false, transform);
                if (h.AddBody(n)) GameEvents.RaiseBodyDisposed("hide");
                return;
            }
            if (InsideSpot == h) LeaveHideSpot();
            else EnterHideSpot(h);
        }

        void EnterHideSpot(HideSpot h)
        {
            if (InMist) SetMist(false);
            Stop();
            Concealed = true;
            InsideSpot = h;
            h.PlayerInside = true;
            Rig?.SetVisible(false);
            Game.Audio?.PlayAt("door", h.transform.position, 0.3f, 1.4f);
        }

        public void LeaveHideSpot()
        {
            if (InsideSpot != null) InsideSpot.PlayerInside = false;
            InsideSpot = null;
            Concealed = false;
            Rig?.SetVisible(!InMist);
        }

        // ---------------------------------------------------------------- input
        void HandleInput()
        {
            var inp = Game.Input;
            if (inp == null) return;
            if (Game.UI != null && Game.UI.BlocksGameplay) return;
            UpdateHover();

            if (TickAimInput()) return;

            if (inp.Cancel.WasPressedThisFrame())
            {
                if (SelectedThrall != null) { SelectThrall(null); return; }
                CancelAll();
                return;
            }

            if (inp.CycleThrall.WasPressedThisFrame()) CycleThrall();
            if (inp.Center.WasPressedThisFrame()) Game.Cam?.Recentre();

            if (SelectedThrall != null)
            {
                HandleThrallInput();
                return;
            }
            if (inp.ThrallFollow.WasPressedThisFrame()) ToggleFollowAll();

            // the mouse never walks her: a click uses what is under it, or snuffs a lamp, if she is within reach
            if (inp.Click.WasPressedThisFrame() && (Game.UI == null || !Game.UI.PointerOverUI))
            {
                if (HoverUse != null && HoverUse.PlayerCan && HoverNpc == null)
                    Act(new PlayerAction { Kind = ActionKind.Interact, Use = HoverUse });
                else if (HoverNpc == null && HoverLight != null && HoverLight.CanSnuffByHand)
                    Act(new PlayerAction { Kind = ActionKind.Snuff, Light = HoverLight });
            }

            if (inp.Sip.WasPressedThisFrame()) TryFeedKey(false);
            if (inp.Drain.WasPressedThisFrame()) TryFeedKey(true);
            if (inp.Carry.WasPressedThisFrame()) TryCarryKey();
            if (inp.Interact.WasPressedThisFrame()) TryInteractKey();
            for (int i = 0; i < inp.Abilities.Length; i++)
                if (inp.Abilities[i].WasPressedThisFrame()) PressAbilitySlot(i);
        }

        bool Rushy => Game.Input != null && Game.Input.Run.IsPressed();

        /// <summary>Who the feed keys take: whoever is within reach (she moves with the keys, so what is beside her
        /// comes first), else the one under the cursor; she never walks to them (P7), so a far one is "Too far".</summary>
        public Npc FeedTarget(bool hoverFirst = true)
        {
            var near = Game.AI?.NearestNpc(Feet, LungeRange, n => n.IsAlive && !n.Rescue && n.State != NpcState.Thrall && n.State != NpcState.Victim && n.CanBeFedBy(this, out _));
            if (near != null) return near;
            if (hoverFirst && HoverNpc != null && HoverNpc.IsAlive && HoverNpc.State != NpcState.Thrall) return HoverNpc;
            return null;
        }

        void TryFeedKey(bool drain)
        {
            // Drain on a fledgling she has freed (or could free): turn it loose on the staff
            if (drain && HoverUse is Shackles sh && sh.CanLoose)
            {
                if (Vector3.Distance(sh.transform.position, Feet) > 8f) { Say("Too far to be heard."); return; }
                if (sh.Owner.State == NpcState.Captive) { Say("Break its shackles first."); return; }
                sh.Owner.TurnLoose();
                return;
            }
            var n = FeedTarget();
            if (n == null) { Say("No one within reach to feed on."); return; }
            Act(new PlayerAction { Kind = ActionKind.Feed, Npc = n, Drain = drain, Rush = Rushy });
        }

        public Npc BodyTarget()
        {
            Npc best = null; float bd = 2.5f;
            if (Game.Level != null)
                foreach (var n in Game.Level.All<Npc>())
                {
                    if (!n.IsBody || n.Carried || n.Disposed || n.Hidden || !n.gameObject.activeInHierarchy) continue;
                    float d = Vector3.Distance(n.transform.position, Feet);
                    if (d < bd) { bd = d; best = n; }
                }
            if (best == null && HoverNpc != null && HoverNpc.IsBody && !HoverNpc.Carried) best = HoverNpc;
            return best;
        }

        void TryCarryKey()
        {
            if (Carrying != null)
            {
                if (_pending != null && _pending.Kind == ActionKind.Interact) _pending = null;
                DropBody();
                return;
            }
            var b = BodyTarget();
            if (b == null) { Say("No body within reach."); return; }
            Act(new PlayerAction { Kind = ActionKind.Carry, Npc = b, Rush = Rushy });
        }

        Interactable _nearUse;
        float _nearUseAt = -1f;

        /// <summary>
        /// What the interact key would use now (D119): the nearest thing beside her, favouring what she faces, and a
        /// hiding place while she carries a body. Things she cannot use are offered too (behind usable ones) so the key
        /// can say why. Recomputed at most ten times a second; drives the verb bar and the prompt beside her.
        /// </summary>
        public Interactable NearUse
        {
            get
            {
                if (Time.time - _nearUseAt < 0.1f && (_nearUse == null || _nearUse)) return _nearUse;
                _nearUseAt = Time.time;
                _nearUse = null;
                if (Game.Level == null || Dead) return null;
                float best = 2.6f;
                var feet = Feet;
                var fwd = transform.forward;
                foreach (var u in Game.Level.All<Interactable>())
                {
                    if (!u || !u.Enabled || !u.ShowMarker || !u.gameObject.activeInHierarchy) continue;
                    var to = u.transform.position - feet;
                    if (Mathf.Abs(to.y) > 1.6f) continue;
                    to.y = 0f;
                    float d = to.magnitude;
                    if (d > 2.6f) continue;
                    float score = d - (d > 0.05f ? 0.5f * Vector3.Dot(fwd, to / d) : 0.5f);
                    if (!u.PlayerCan) score += 1.2f;
                    if (Carrying != null && u is HideSpot) score -= 1.5f;
                    if (score < best) { best = score; _nearUse = u; }
                }
                return _nearUse;
            }
        }

        void TryInteractKey()
        {
            // what is beside her and within reach first (she moves only with the keys, P7); then whatever the cursor
            // rests on; anything usable but out of reach just says how far it is
            var best = NearUse;
            if (best != null && best.PlayerCan && InUseReach(best)) { Act(new PlayerAction { Kind = ActionKind.Interact, Use = best }); return; }
            var l = Game.Lights?.Nearest(Feet, SnuffReach, x => x.CanSnuffByHand && !x.Portable && InSnuffReach(x));
            if (l != null) { Act(new PlayerAction { Kind = ActionKind.Snuff, Light = l }); return; }
            if (HoverUse != null && HoverUse.PlayerCan && InUseReach(HoverUse)) { Act(new PlayerAction { Kind = ActionKind.Interact, Use = HoverUse }); return; }
            if (HoverLight != null && HoverLight.CanSnuffByHand && InSnuffReach(HoverLight)) { Act(new PlayerAction { Kind = ActionKind.Snuff, Light = HoverLight }); return; }
            if (best != null && best.PlayerCan) { Say(TooFar(Util.FlatDistance(best.transform.position, Feet))); return; }
            if (HoverUse != null && HoverUse.PlayerCan) { Say(TooFar(Util.FlatDistance(HoverUse.transform.position, Feet))); return; }
            if (HoverLight != null && HoverLight.CanSnuffByHand) { Say(TooFar(Util.FlatDistance(HoverLight.transform.position, Feet))); return; }
            if (best != null) { Say(best.Unavailable ?? "Cannot be used."); return; }
            Say("Nothing to use here.");
        }
    }
}
