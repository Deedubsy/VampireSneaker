using System.Collections.Generic;
using UnityEngine;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;

namespace Vespertine.AI
{
    /// <summary>A hunter squad (M13 on): `squad &lt;id&gt; x y [name=…] [nerve=N]` with members `npc … squad=&lt;id&gt;`
    /// (the one marked `lead`, else the first listed, leads; the others walk a wedge behind him, so the squad
    /// moves, rings and hunts as one: what any man hears or finds becomes the leader's search). The squad shares its
    /// nerve. Every man it loses, every feed it watches and every lamp that dies near it costs nerve, and after each
    /// shock it closes into a ring, back to back, before it hunts again as one. At zero nerve it breaks: the survivors
    /// run for the squad's rally point (x y) and leave the streets. Fearless men run too: it is the squad that breaks,
    /// not the man.</summary>
    public class Squad : Entity
    {
        public readonly List<Npc> Members = new List<Npc>();   // slot order; [0] leads
        public string DisplayName;
        public float Nerve, StartNerve;
        public bool Broken, Routed;
        public float RingT;
        public Vector3 RingCentre, ShockAt;
        public Vector3 Rally => transform.position;
        float _sinceShock = 99f, _dreadCd, _musterT;
        readonly HashSet<Npc> _lost = new HashSet<Npc>();   // each man costs nerve once, however he goes

        /// <summary>Set while a save is applied: restoring dazed men must not shake anyone.</summary>
        public static bool Restoring;
        static readonly List<Squad> _all = new List<Squad>();
        public static IReadOnlyList<Squad> All => _all;

        public Npc Leader => Members.Count > 0 ? Members[0] : null;
        public bool Ringing => RingT > 0f && !Broken;

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            DisplayName = spec.Opt("name", "The squad").Replace('_', ' ');
        }

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() { _all.Remove(this); }

        public override void Link() => Muster();

        /// <summary>Collect the men. Men in a group not yet active (a squad the script sends in later, Vane's men when
        /// he lives) are left out until it is: an empty squad keeps trying.</summary>
        void Muster()
        {
            Members.Clear();
            Npc lead = null;
            foreach (var n in Game.Level.All<Npc>())
            {
                if (!n || n.Spec == null || n.Spec.Opt("squad") != Id || !n.gameObject.activeInHierarchy) continue;
                if (n.Spec.Has("lead") && lead == null) lead = n;
                else Members.Add(n);
            }
            Members.Sort((a, b) => a.Spec.Line.CompareTo(b.Spec.Line));
            if (lead) Members.Insert(0, lead);
            if (Members.Count == 0) return;
            Reslot();
            var c = Game.Campaign;
            StartNerve = Spec.Has("nerve") ? Spec.OptFloat("nerve", 80f)
                : SquadMath.StartNerve(Leader ? Leader.Arch.Faction : Faction.Vigil, c != null ? c.Terror : 0, c != null ? c.Rumour : 0);
            // Vane at their head: his men will stand a long time
            if (!Spec.Has("nerve") && Members.Exists(m => m.Arch.Id == "vane")) StartNerve += SquadMath.VaneBonus;
            Nerve = StartNerve;
        }

        /// <summary>A group switched on: men it brought who belong to this squad join it. Before anything has shaken
        /// the squad they muster afresh (a `lead` among them takes the head); afterwards they fall in at the back.</summary>
        public void Remuster()
        {
            if (Broken || Restoring) return;
            if (Members.Count == 0 || Nerve >= StartNerve) { Muster(); return; }
            bool any = false;
            foreach (var n in Game.Level.All<Npc>())
                if (n && n.Spec != null && n.Spec.Opt("squad") == Id && n.gameObject.activeInHierarchy && !Members.Contains(n))
                { Members.Add(n); any = true; }
            if (any) Reslot();
        }

        void Reslot()
        {
            for (int i = 0; i < Members.Count; i++)
            {
                var n = Members[i];
                n.Squad = this;
                n.Slot = i;
            }
        }

        /// <summary>Still standing with the squad: alive, awake, his own man, not running.</summary>
        public static bool Capable(Npc n) => n && n.gameObject.activeInHierarchy && n.IsAlive && !n.Incapacitated && !n.Hidden
                                             && n.State != NpcState.Thrall && n.State != NpcState.Mesmerised && !n.Rescue && !n.Routed;

        public int Standing { get { int k = 0; foreach (var n in Members) if (Capable(n)) k++; return k; } }

        void Update()
        {
            if (Broken || !Game.InMission) return;
            float dt = Time.deltaTime;
            if (Members.Count == 0)
            {
                _musterT -= dt;
                if (_musterT <= 0f) { _musterT = 1f; Muster(); }
                return;
            }
            if (!Capable(Leader)) Promote();
            if (Broken) return;
            if (_dreadCd > 0f) _dreadCd -= dt;
            _sinceShock += dt;
            Nerve = SquadMath.Recover(Nerve, StartNerve, _sinceShock, dt);
            if (RingT > 0f)
            {
                RingT -= dt;
                if (RingT <= 0f && Capable(Leader))
                {
                    // the ring opens: hunt as one, from where it happened
                    GameEvents.RaiseBark(Leader.Id, Nerve < StartNerve * 0.4f ? "Stay together. Whatever happens, stay together." : "On me. We find it.");
                    Leader.EnterSearching(ShockAt, false);
                }
            }
        }

        /// <summary>The leader is down: the next man standing takes his place and his beat.</summary>
        void Promote()
        {
            int i = Members.FindIndex(Capable);
            if (i < 0) { Break(false, transform.position); return; }
            var old = Members[0];
            var next = Members[i];
            Members.RemoveAt(i);
            Members.Insert(0, next);
            Reslot();
            next.TakeBeatFrom(old);
            GameEvents.RaiseBark(next.Id, "I have the squad! Close up!");
        }

        // ------------------------------------------------------------------ nerve
        public void Shock(float amount, Vector3 at, string why = null)
        {
            if (Broken || Restoring || amount <= 0f) return;
            Nerve -= amount;
            _sinceShock = 0f;
            if (SquadMath.Breaks(Nerve)) { Break(true, at); return; }
            if (!Capable(Leader)) Promote();
            if (Broken) return;
            // back to back where they stand
            var c = Vector3.zero; int k = 0;
            foreach (var n in Members) if (Capable(n)) { c += n.transform.position; k++; }
            RingCentre = k > 0 ? c / k : Leader.transform.position;
            ShockAt = at;
            bool fresh = RingT <= 0f;
            RingT = SquadMath.RingTime;
            if (fresh)
                GameEvents.RaiseBark(Leader.Id, why ?? (Nerve < StartNerve * 0.4f ? "It's taking us one at a time!" : "Backs together! Watch the dark!"));
        }

        /// <summary>The squad breaks. Routed: the men left standing run for the rally point and leave the streets.
        /// Not routed: there is nobody left to run.</summary>
        public void Break(bool routed, Vector3 from)
        {
            if (Broken) return;
            Broken = true;
            Routed = routed;
            RingT = 0f;
            if (routed)
            {
                bool first = true;
                foreach (var n in Members)
                {
                    if (!Capable(n)) continue;
                    if (n.Arch.Id == "vane")
                    {
                        // the Captain does not run: he finishes it alone
                        GameEvents.RaiseBark(n.Id, "Go, then. Run. I'll do it myself.");
                        n.EnterSearching(from, true);
                        continue;
                    }
                    n.Rout(Rally, from, first);
                    first = false;
                }
                GameEvents.RaiseToast($"{DisplayName} breaks and runs.");
                // the sight of it reaches any squad close enough to see them go
                foreach (var q in new List<Squad>(_all))
                    if (q != this && !q.Broken && q.Leader && Util.FlatDistance(q.Leader.transform.position, from) < 30f)
                        q.Shock(SquadMath.RoutNearby, from, "They're running! Hold! HOLD!");
            }
            else GameEvents.RaiseToast($"{DisplayName} is finished.");
            Game.Mission?.OnSquadBroken(this);
        }

        /// <summary>Can anyone standing in the squad see this point?</summary>
        public bool Sees(Vector3 p, Npc except = null)
        {
            foreach (var n in Members)
            {
                if (n == except || !Capable(n) || !n.CanSee) continue;
                if (Util.FlatDistance(n.transform.position, p) > SquadMath.WitnessRange) continue;
                if (n.LineOfSight(p + Vector3.up * 1.0f)) return true;
            }
            return false;
        }

        bool Near(Vector3 p, float r)
        {
            foreach (var n in Members) if (Capable(n) && Util.FlatDistance(n.transform.position, p) < r) return true;
            return false;
        }

        // ------------------------------------------------------------------ what shakes them (called by the mission)
        /// <summary>Someone was dazed, enthralled or killed.</summary>
        public static void NotifyDowned(Npc victim)
        {
            if (!victim) return;
            foreach (var q in new List<Squad>(_all))
            {
                if (!q.Members.Contains(victim) || !q._lost.Add(victim) || q.Broken || Restoring) continue;
                q.Shock(q.Sees(victim.transform.position, victim) ? SquadMath.DownSeen : SquadMath.DownUnseen, victim.transform.position,
                        victim.State == NpcState.Thrall ? "He's turned! Get away from him!" : null);
            }
        }

        public static void NotifyFed(Npc victim, bool drained)
        {
            if (Restoring || !victim) return;
            var p = Game.Player;
            bool terror = p != null && p.Has("predator.terror");
            bool dread = p != null && p.Has("predator.dread_feast");
            foreach (var q in new List<Squad>(_all))
                if (!q.Broken)
                    q.Shock(SquadMath.FedShock(q.Sees(victim.transform.position, victim), drained, terror, dread), victim.transform.position,
                        drained ? "It's drinking him dry. God. God." : null);
        }

        /// <summary>A lamp went out by her hand near a squad.</summary>
        public static void NotifyDark(Vector3 at)
        {
            if (Restoring) return;
            foreach (var q in new List<Squad>(_all))
                if (!q.Broken && q.Near(at, 10f)) q.Shock(SquadMath.LightOut, at, "The lamps! Who's putting out the lamps?");
        }

        public static void NotifyApex(Vector3 at)
        {
            foreach (var q in new List<Squad>(_all))
                if (!q.Broken && q.Near(at, 20f)) q.Shock(SquadMath.Apex, at, "Everything's slow, I can't, I can't move!");
        }

        /// <summary>A squad man saw her outright. At Awakening 9 the sight of her alone shakes them.</summary>
        public void NotifySeen(Vector3 at)
        {
            if (Broken || _dreadCd > 0f || Game.Player == null || Game.Player.Awakening < 9) return;
            _dreadCd = SquadMath.DreadCooldown;
            Shock(SquadMath.Dread, at, "That's her. That's HER.");
        }

        // ------------------------------------------------------------------ save
        public void SaveState(Save.EntityState s)
        {
            s.F0 = Nerve; s.F1 = RingT; s.F2 = StartNerve; s.B0 = Broken; s.B1 = Routed; s.P = ShockAt;
            var ids = new List<string>();
            foreach (var n in Members) if (n) ids.Add(n.Id);
            s.S0 = string.Join(",", ids);
        }

        public void LoadState(Save.EntityState s)
        {
            Nerve = s.F0; RingT = s.F1; StartNerve = s.F2 > 0f ? s.F2 : StartNerve; Broken = s.B0; Routed = s.B1; ShockAt = s.P;
            _sinceShock = 0f;
            if (!string.IsNullOrEmpty(s.S0))
            {
                var order = new List<Npc>();
                foreach (var id in s.S0.Split(',')) { var n = Game.Level.Get<Npc>(id); if (n) order.Add(n); }
                if (order.Count > 0) { Members.Clear(); Members.AddRange(order); Reslot(); }
            }
            RingCentre = Leader ? Leader.transform.position : transform.position;
        }

        /// <summary>After the NPCs are restored: a routed squad's men are still running.</summary>
        public void AfterRestore()
        {
            if (!Broken || !Routed) return;
            foreach (var n in Members)
                if (n && n.gameObject.activeInHierarchy && n.IsAlive && !n.Incapacitated && n.State != NpcState.Thrall)
                    n.Rout(Rally, n.transform.position, false);
        }
    }
}
