using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.AI
{
    /// <summary>Coordinates NPCs: noise, shouts, alarm level, lockdown, bells, search allocation and lamp bookkeeping.</summary>
    public class AIDirector : MonoBehaviour
    {
        public readonly List<Npc> Npcs = new List<Npc>();
        /// <summary>A freed prisoner or loose fledgling is up and about (M09): guards look for them.</summary>
        public bool RescuesLoose;
        public int Alarm;                      // 0 calm, 1 suspicious/searching, 2 alerted, 3 lockdown
        public bool Lockdown;
        public int BodiesFound;
        public int LockdownThreshold = 3;
        public bool EveryoneLooksUp;
        public float MaxDetection;
        public Npc MostAware;
        public int TimesSpotted;
        public int WitnessReports;

        readonly HashSet<GameLight> _noticedLamps = new HashSet<GameLight>();
        readonly Dictionary<Bell, Npc> _bellClaims = new Dictionary<Bell, Npc>();
        readonly HashSet<Npc> _foundBodies = new HashSet<Npc>();
        float _calmTimer;
        float _lastAlert = -999f;

        // ability effects
        struct BlindZone { public Vector3 Center; public float Radius, Until; }
        readonly List<BlindZone> _blind = new List<BlindZone>();
        float _slowUntil = -1f, _slowScale = 1f;

        void Awake() { Game.AI = this; }
        void OnDestroy() { if (Game.AI == this) Game.AI = null; }

        public void Setup(LevelData data)
        {
            Npcs.Clear();
            _noticedLamps.Clear();
            _bellClaims.Clear();
            _foundBodies.Clear();
            _blind.Clear();
            Alarm = 0;
            Lockdown = false;
            BodiesFound = 0;
            TimesSpotted = 0;
            WitnessReports = 0;
            LockdownThreshold = data.GetInt("lockdown_bodies", Game.Countermeasure("cm_paired") ? 2 : 3);
            EveryoneLooksUp = Game.Countermeasure("cm_rooftop");
        }

        public void Register(Npc n) { if (!Npcs.Contains(n)) Npcs.Add(n); }
        public void Unregister(Npc n) { Npcs.Remove(n); }

        // ------------------------------------------------------------------ noise
        public void HearNoise(Vector3 pos, float radius, NoiseKind kind, object source)
        {
            float r = NoiseRange(radius, kind, Difficulties.Current.ShoutRadius);
            foreach (var n in Npcs.ToArray())
            {
                if (!n || !n.gameObject.activeInHierarchy || !n.IsAlive) continue;
                if (source is Npc s && s == n) continue;
                float d = Vector3.Distance(n.transform.position, pos);
                if (d > r * n.Arch.Hear + 0.1f) continue;
                bool occl = kind != NoiseKind.Bell && kind != NoiseKind.Gunshot && NoiseSystem.Occluded(pos, n.transform.position);
                // vertical separation of more than one storey muffles small sounds
                if (Mathf.Abs(pos.y - n.transform.position.y) > 3.5f && radius < 10f) occl = true;
                if (!DetectionMath.Hears(d, r, occl, n.Arch.Hear)) continue;
                n.Hear(pos, radius, kind, source);
            }
            if (kind == NoiseKind.Bell) RaiseLockdown("bell");
        }

        /// <summary>The Hunter difficulty's shout radius, in metres. Other difficulties scale voices and screams against it.</summary>
        public const float BaseShoutRadius = 15f;

        /// <summary>
        /// How far a shout carries. <see cref="DifficultyDef.ShoutRadius"/> is already in metres (10 / 15 / 22).
        /// It used to be multiplied by 15 again, so one sighting woke the whole map (I-01).
        /// </summary>
        public static float ShoutRange(float shoutRadius) => shoutRadius;

        /// <summary>How far a noise carries on the current difficulty: voices and screams scale with the shout radius relative to Hunter.</summary>
        public static float NoiseRange(float radius, NoiseKind kind, float shoutRadius)
        {
            bool voice = kind == NoiseKind.Scream || kind == NoiseKind.Voice;
            return voice ? radius * shoutRadius / BaseShoutRadius : radius;
        }

        /// <summary>An alerted NPC shouts: armed allies in range join the hunt.</summary>
        public void Shout(Npc from, Vector3 lkp)
        {
            float r = ShoutRange(Difficulties.Current.ShoutRadius);
            Game.Noise?.Emit(from.transform.position, 0f, NoiseKind.Voice, from);
            GameEvents.RaiseNoise(from.transform.position, r, "Shout");
            // the shout's reach on the ground (QW7, SR.7): everyone inside the red ring and not behind a wall comes
            var red = Mats.Pal.Alerted; red.a = 0.55f;
            Fx.Ring(from.transform.position, r, red, 1.1f);
            foreach (var n in Npcs)
            {
                if (n == from || !n || !n.IsAlive || !n.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(n.transform.position, from.transform.position);
                if (d > r) continue;
                if (d > r * 0.6f && NoiseSystem.Occluded(n.transform.position, from.transform.position)) continue;
                if (Split(from, n)) continue;
                n.Alert(lkp, from);
            }
            _lastAlert = Time.time;
        }

        /// <summary>
        /// After the Opera (campaign flag `watch_vigil_split`, set by framing the Vigil for a Council death), the Watch
        /// and the Vigil stop answering each other's shouts for the rest of Act III.
        /// </summary>
        public static bool Split(Npc a, Npc b)
        {
            bool flag = Game.Campaign != null && Game.Campaign.Flag("watch_vigil_split");
            int act = Game.Mission != null && Game.Mission.Info != null ? Game.Mission.Info.Act : 0;
            return Split(flag, act, a.Arch.Faction, b.Arch.Faction);
        }

        /// <summary>Pure form of <see cref="Split(Npc, Npc)"/> (unit-tested).</summary>
        public static bool Split(bool splitFlag, int act, Faction fa, Faction fb)
        {
            if (!splitFlag || act != 3) return false;
            return (fa == Faction.Watch && fb == Faction.Vigil) || (fa == Faction.Vigil && fb == Faction.Watch);
        }

        // ------------------------------------------------------------------ evidence & escalation
        public void ReportBody(Npc finder, Npc body)
        {
            if (body != null)
            {
                if (_foundBodies.Contains(body)) return;
                _foundBodies.Add(body);
            }
            BodiesFound++;
            GameEvents.RaiseToast(body != null ? "A body has been found." : "Blood has been found.");
            Game.Campaign?.AddHabit(Progression.Habits.BodiesFound, body != null ? 1f : 0.5f);
            // everyone within earshot becomes wary
            foreach (var n in Npcs)
                if (n && n.IsAlive && Vector3.Distance(n.transform.position, finder.transform.position) < 20f) n.Wary = true;
            Escalate(1);
            if (BodiesFound >= LockdownThreshold) RaiseLockdown("bodies");
        }

        /// <summary>A survivor reports what they saw (woken sip victim, exposer).</summary>
        public void ReportWitness(Npc witness)
        {
            WitnessReports++;
            Game.Campaign?.AddHabit(Progression.Habits.Sightings, 0.5f);
            Escalate(1);
            foreach (var n in Npcs)
                if (n && n.IsAlive && Vector3.Distance(n.transform.position, witness.transform.position) < 15f) n.Wary = true;
        }

        public void RaiseLockdown(string reason)
        {
            if (Lockdown) return;
            Lockdown = true;
            Debug.Log("[AI] lockdown: " + reason);
            GameEvents.RaiseToast("LOCKDOWN - the alarm is raised.");
            foreach (var n in Npcs) if (n && n.IsAlive) { n.Wary = true; if (n.State == NpcState.Relaxed) n.Say(BarkKind.Lockdown); }
            var lvl = Game.Level;
            if (lvl != null)
            {
                foreach (var g in lvl.All<Gate>()) if (g.Spec != null && g.Spec.Has("lockdown")) g.SetOpen(false, false);
                foreach (var d in lvl.All<Door>()) if (d.Spec != null && d.Spec.Has("lockdown")) d.SetLocked(true);
                lvl.ActivateGroup("lockdown", true);
                // lamplighters relight everything
                foreach (var l in Game.Lights.All)
                    if (l && !l.On && !l.Portable && l.Snuffable && l.Kind != LightKind.Moon) RequestRelight(l);
            }
            Game.Mission?.OnLockdown();
            SetAlarm(3);
            EnsureReinforcements(true);
        }

        // ------------------------------------------------------------------ reinforcements
        /// <summary>
        /// Who a lockdown brings in at the mission's `spawn` points: `count` (default 2) of `type` (default watchman)
        /// per point, one fewer on Merciful and one more on Apex. Once the Vigil hunts her (M07 on, or header `vigil = 1`)
        /// a hunter and a hound also arrive at the point flagged `vigil` (else the first point). Ids are deterministic so a save made after
        /// the lockdown restores them by id.
        /// </summary>
        public static List<EntitySpec> ReinforcementSpecs(IList<EntitySpec> points, bool vigil, Difficulty diff)
        {
            var list = new List<EntitySpec>();
            EntitySpec vigilAt = null;
            foreach (var sp in points) if (sp.Has("vigil")) { vigilAt = sp; break; }
            if (vigilAt == null && points.Count > 0) vigilAt = points[0];
            foreach (var sp in points)
            {
                int n = Mathf.Max(1, sp.OptInt("count", 2) + (diff == Difficulty.Merciful ? -1 : diff == Difficulty.Apex ? 1 : 0));
                string type = sp.Opt("type", "watchman");
                for (int i = 0; i < n; i++) list.Add(Reinforcement(sp, $"rf_{sp.Id}_{i}", i == 0 && n > 2 && type == "watchman" ? "sergeant" : type));
                if (vigil && sp == vigilAt && !sp.Has("novigil"))
                {
                    list.Add(Reinforcement(sp, $"rf_{sp.Id}_v0", "hunter"));
                    list.Add(Reinforcement(sp, $"rf_{sp.Id}_v1", "hound"));
                }
            }
            return list;
        }

        static EntitySpec Reinforcement(EntitySpec sp, string id, string type)
        {
            var e = new EntitySpec { Kind = "npc", Id = id, Type = type, X = sp.X, Y = sp.Y, Line = sp.Line };
            e.Flags.Add("wary");
            if (sp.Opts.TryGetValue("face", out var f)) e.Opts["face"] = f;
            return e;
        }

        /// <summary>Spawn any reinforcement not already present. dispatch: send them to search where she was last seen.</summary>
        public void EnsureReinforcements(bool dispatch)
        {
            var lvl = Game.Level;
            if (lvl == null) return;
            var points = new List<EntitySpec>();
            foreach (var sp in lvl.All<SpawnPoint>()) if (sp && sp.Spec != null) points.Add(sp.Spec);
            if (points.Count == 0) return;
            bool vigil = lvl.Data.GetInt("vigil", Game.Mission != null && Game.Mission.Index >= 6 ? 1 : 0) == 1;
            var diff = Game.Campaign != null ? Game.Campaign.Difficulty : Difficulty.Hunter;
            Vector3 lkp = MostAware && MostAware.LastKnown != Vector3.zero ? MostAware.LastKnown
                : Game.Player ? Game.Player.transform.position : Vector3.zero;
            foreach (var spec in ReinforcementSpecs(points, vigil, diff))
            {
                if (lvl.Get(spec.Id)) continue;
                var n = Npc.Spawn(spec);
                if (dispatch) n.EnterSearching(lkp + new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f)), true);
            }
            if (dispatch) GameEvents.RaiseToast("Reinforcements are coming.");
        }

        void Escalate(int level)
        {
            if (Alarm < level) SetAlarm(level);
            _calmTimer = 0f;
        }

        void SetAlarm(int a)
        {
            if (Alarm == a) return;
            Alarm = a;
            GameEvents.RaiseAlarm(a);
        }

        // ------------------------------------------------------------------ bells
        public Bell NearestBell(Vector3 p, float max)
        {
            Bell best = null;
            float bd = max;
            if (Game.Level == null) return null;
            foreach (var b in Game.Level.All<Bell>())
            {
                if (!b || !b.gameObject.activeInHierarchy || b.Silenced || Time.time - b.LastRung < 30f) continue;
                if (_bellClaims.TryGetValue(b, out var who) && who && who.IsAlive && who.State != NpcState.Dazed) continue;
                float d = Vector3.Distance(b.transform.position, p);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        public bool ClaimBell(Bell b, Npc n)
        {
            if (b == null) return false;
            if (_bellClaims.TryGetValue(b, out var who) && who && who != n && who.IsAlive) return false;
            _bellClaims[b] = n;
            GameEvents.RaiseBark(n.Id, "To the bell!");
            return true;
        }

        public void ReleaseBell(Bell b, Npc n)
        {
            if (b == null) return;
            if (_bellClaims.TryGetValue(b, out var who) && who == n) _bellClaims.Remove(b);
        }

        /// <summary>Who is running for a bell right now (for UI warnings).</summary>
        public IEnumerable<KeyValuePair<Bell, Npc>> BellRunners => _bellClaims;

        // ------------------------------------------------------------------ searching
        static NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavAreas.HumanMask };

        public List<Vector3> SearchPoints(Vector3 around, int count, float radius)
        {
            var list = new List<Vector3>();
            for (int i = 0; i < count * 4 && list.Count < count; i++)
            {
                var r = Random.insideUnitCircle * radius;
                var p = around + new Vector3(r.x, 0, r.y);
                if (!NavMesh.SamplePosition(p, out var hit, 2.5f, Filter)) continue;
                // prefer dark places: the Vigil learned the vampire hides there
                if (Game.Lights != null && Game.Lights.LightAt(hit.position) > 0.6f && Random.value < 0.5f) continue;
                bool dup = false;
                foreach (var q in list) if ((q - hit.position).sqrMagnitude < 4f) { dup = true; break; }
                if (!dup) list.Add(hit.position);
            }
            return list;
        }

        public void AssignSearchers(Npc officer, Vector3 at)
        {
            int sent = 0;
            foreach (var n in Npcs)
            {
                if (sent >= 3) break;
                // a squad man hunts with his own leader, never on another officer's word
                if (n == officer || n.Squad != null || n.Friendly || !n.IsAlive || !n.Armed || n.State == NpcState.Alerted || n.State == NpcState.Searching || n.Incapacitated || n.IsThrall) continue;
                if (Vector3.Distance(n.transform.position, officer.transform.position) > 25f) continue;
                n.EnterSearching(at + Random.insideUnitSphere.Flat() * 4f, true);
                sent++;
            }
            if (sent > 0) GameEvents.RaiseBark(officer.Id, "You three - with me! Search every shadow!");
        }

        public void ThrowFlare(Npc by, Vector3 at)
        {
            var from = by.Eye + by.Forward * 0.4f;
            Visual.Fx.Flare(from, at, 25f);
            GameEvents.RaiseBark(by.Id, "Flare!");
        }

        public Vector3 FleePoint(Npc n, Vector3 from)
        {
            var away = (n.transform.position - from).Flat();
            if (away.sqrMagnitude < 0.01f) away = -n.Forward;
            away.Normalize();
            // prefer a lit place away from the threat, or an armed ally
            Vector3 best = n.transform.position + away * 12f;
            float bestScore = float.MinValue;
            for (int i = 0; i < 10; i++)
            {
                var dir = Quaternion.Euler(0, Random.Range(-70f, 70f), 0) * away;
                var p = n.transform.position + dir * Random.Range(8f, 18f);
                if (!NavMesh.SamplePosition(p, out var hit, 3f, Filter)) continue;
                float score = Vector3.Distance(hit.position, from) + (Game.Lights != null ? Game.Lights.LightAt(hit.position) * 6f : 0f);
                if (score > bestScore) { bestScore = score; best = hit.position; }
            }
            return best;
        }

        // ------------------------------------------------------------------ lamps
        public bool LampNoticed(GameLight l) => _noticedLamps.Contains(l);

        public void NoticeLamp(GameLight l, Npc by)
        {
            _noticedLamps.Add(l);
            if (!by.Arch.Has(ArchFlags.Relights)) RequestRelight(l);
        }

        public void LampRelit(GameLight l) { if (l) _noticedLamps.Remove(l); }

        /// <summary>Send the nearest free lamplighter (or, failing that during lockdown, any armed npc) to relight.</summary>
        public void RequestRelight(GameLight l)
        {
            Npc best = null;
            float bd = 60f;
            foreach (var n in Npcs)
            {
                if (!n.IsAlive || !n.Arch.Has(ArchFlags.Relights) || n.Incapacitated || n.IsThrall) continue;
                if (n.State != NpcState.Relaxed && n.State != NpcState.Suspicious) continue;
                float d = Vector3.Distance(n.transform.position, l.transform.position);
                if (d < bd) { bd = d; best = n; }
            }
            if (best == null && Lockdown)
            {
                bd = 25f;
                foreach (var n in Npcs)
                {
                    if (!n.IsAlive || n.Incapacitated || n.IsThrall || n.State != NpcState.Relaxed) continue;
                    float d = Vector3.Distance(n.transform.position, l.transform.position);
                    if (d < bd) { bd = d; best = n; }
                }
            }
            best?.BeginRelight(l);
        }

        // ------------------------------------------------------------------ ability support
        public void AddBlindZone(Vector3 c, float r, float duration) => _blind.Add(new BlindZone { Center = c, Radius = r, Until = Time.time + duration });
        public bool Blinded(Npc n)
        {
            for (int i = 0; i < _blind.Count; i++)
                if (Util.FlatDistance(_blind[i].Center, n.transform.position) < _blind[i].Radius) return true;
            return false;
        }
        public bool InBlindZone(Vector3 p)
        {
            for (int i = 0; i < _blind.Count; i++)
                if (Util.FlatDistance(_blind[i].Center, p) < _blind[i].Radius) return true;
            return false;
        }

        /// <summary>Slow every human (Eclipse capstone).</summary>
        public void SlowHumans(float scale, float duration) { _slowScale = scale; _slowUntil = Time.time + duration; }
        public float TimeScaleFor(Npc n) => Time.time < _slowUntil ? _slowScale : 1f;

        // ------------------------------------------------------------------ queries
        /// <summary>Every NPC alive and in the world. A struct sequence, so a plain <c>foreach</c> allocates nothing;
        /// LINQ still works through <see cref="IEnumerable{T}"/>.</summary>
        public LivingNpcs Living() => new LivingNpcs(Npcs);

        public readonly struct LivingNpcs : IEnumerable<Npc>
        {
            readonly List<Npc> _list;
            public LivingNpcs(List<Npc> list) { _list = list; }
            public Enumerator GetEnumerator() => new Enumerator(_list);
            IEnumerator<Npc> IEnumerable<Npc>.GetEnumerator() => GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public struct Enumerator : IEnumerator<Npc>
            {
                readonly List<Npc> _list;
                int _i;
                public Enumerator(List<Npc> list) { _list = list; _i = -1; }
                public Npc Current => _list[_i];
                object System.Collections.IEnumerator.Current => Current;
                public bool MoveNext()
                {
                    while (++_i < _list.Count)
                    {
                        var n = _list[_i];
                        if (n && n.IsAlive && n.gameObject.activeInHierarchy) return true;
                    }
                    return false;
                }
                public void Reset() { _i = -1; }
                public void Dispose() { }
            }
        }

        public Npc NearestNpc(Vector3 p, float max, System.Func<Npc, bool> pred = null)
        {
            Npc best = null;
            float bd = max;
            foreach (var n in Npcs)
            {
                if (!n || !n.gameObject.activeInHierarchy) continue;
                if (pred != null && !pred(n)) continue;
                float d = Vector3.Distance(n.transform.position, p);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float now = Time.time;
            for (int i = _blind.Count - 1; i >= 0; i--) if (_blind[i].Until < now) _blind.RemoveAt(i);

            MaxDetection = 0f;
            MostAware = null;
            bool anyAlerted = false, anySearching = false, anySus = false, loose = false;
            foreach (var n in Npcs)
            {
                if (!n || !n.IsAlive || !n.gameObject.activeInHierarchy) continue;
                if (n.State == NpcState.Escort || n.State == NpcState.Amok) loose = true;
                if (n.Detection > MaxDetection) { MaxDetection = n.Detection; MostAware = n; }
                switch (n.State)
                {
                    case NpcState.Alerted: anyAlerted = true; break;
                    case NpcState.Searching: case NpcState.Panicked: anySearching = true; break;
                    case NpcState.Suspicious: case NpcState.Investigating: anySus = true; break;
                }
            }
            RescuesLoose = loose;
            if (anyAlerted) { _lastAlert = now; Escalate(Lockdown ? 3 : 2); }
            else if (anySearching || anySus) { if (Alarm < 1) SetAlarm(1); _calmTimer = 0f; }
            else if (!Lockdown)
            {
                _calmTimer += Time.deltaTime;
                if (_calmTimer > 6f && Alarm > 0 && now - _lastAlert > 30f) SetAlarm(0);
            }
            else if (Alarm < 3) SetAlarm(3);

            // music + heartbeat
            var au = Game.Audio;
            if (au != null && Game.InMission)
            {
                au.Mood = anyAlerted ? Audio.MusicMood.Alert : (anySearching || anySus || Lockdown || MaxDetection > 0.2f) ? Audio.MusicMood.Tension : Audio.MusicMood.Calm;
                float danger = Mathf.Clamp01(MaxDetection);
                au.HeartRate = danger > 0.05f ? Mathf.Lerp(1.1f, 2.6f, danger) : 0f;
                au.HeartVolume = Mathf.Lerp(0f, 0.9f, danger);
            }
        }

        // ------------------------------------------------------------------ save
        public void Capture(Save.MissionSave s)
        {
            s.Alarm = Alarm;
            s.Lockdown = Lockdown;
            s.BodiesFound = BodiesFound;
            s.Npcs.Clear();
            foreach (var n in Game.Level.All<Npc>()) if (n) s.Npcs.Add(n.Capture());
        }

        public void Restore(Save.MissionSave s)
        {
            Lockdown = s.Lockdown;
            BodiesFound = s.BodiesFound;
            SetAlarm(s.Alarm);
            _noticedLamps.Clear();
            _bellClaims.Clear();
            _blind.Clear();
            if (Lockdown) EnsureReinforcements(false);
            var byId = new Dictionary<string, NpcSave>();
            foreach (var ns in s.Npcs) byId[ns.Id] = ns;
            foreach (var n in new List<Npc>(Game.Level.All<Npc>()))
            {
                if (!n) continue;
                if (byId.TryGetValue(n.Id, out var ns))
                {
                    n.gameObject.SetActive(true);
                    n.Restore(ns);
                    if (!ns.Active) n.gameObject.SetActive(false);
                }
                else { Game.Level.Unregister(n); Destroy(n.gameObject); }
            }
        }
    }
}
