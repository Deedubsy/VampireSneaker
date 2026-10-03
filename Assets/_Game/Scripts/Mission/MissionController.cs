using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Player;
using Vespertine.Progression;
using Vespertine.Save;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.Mission
{
    /// <summary>
    /// Owns one mission: builds the level, spawns characters, tracks objectives, runs the map script,
    /// handles saves/loads, death, dawn and the end-of-mission tally (Marks, Dossier, Terror/Rumour).
    /// </summary>
    public class MissionController : MonoBehaviour
    {
        public MissionInfo Info;
        public LevelData Data;
        public int Index;
        public bool Running, Ended, Won;
        public float MissionTime;
        /// <summary><see cref="MissionTime"/> at the last save or load, or -1 (the HUD's save age, QW8).</summary>
        public float SavedAt = -1f;
        /// <summary>Spotted events this run, for the debrief's Detections page (SR.10). Not saved: a load starts it empty.</summary>
        public readonly List<SpottedRecord> Detections = new List<SpottedRecord>();
        public float DawnLeft = -1f;            // seconds; < 0 = no dawn timer
        /// <summary>The script timer shown on the HUD chip ("hud" unless a `countdown` names another) and its label.</summary>
        public string HudTimer = "hud", HudLabel;
        public readonly List<Objective> Objectives = new List<Objective>();
        public MissionState Stats = new MissionState();
        public MissionResult Result;
        public string DeathCause;

        readonly List<ScriptRule> _rules = new List<ScriptRule>();
        readonly HashSet<int> _fired = new HashSet<int>();
        readonly Dictionary<string, float> _timers = new Dictionary<string, float>();
        readonly HashSet<string> _flags = new HashSet<string>();
        string _campaignAtStart;
        int _vitaeAtStart, _awakeningAtStart;
        float _autosaveT, _escapeToastT = -99f;
        bool _applyingSave;
        int _loadsThisRun;

        void Awake() { Game.Mission = this; }
        // Static GameEvents outlive the scene when play mode is entered without a domain reload: never leave a
        // destroyed controller subscribed.
        void OnDestroy() { Running = false; Unsubscribe(); if (Game.Mission == this) Game.Mission = null; }

        // ================================================================== lifecycle
        /// <summary>Builds the mission from its map file. If a save is given, its state is applied on top.</summary>
        public bool Begin(string missionId, MissionSave save = null)
        {
            Teardown();
            var text = Resources.Load<TextAsset>("Missions/" + missionId);
            if (text == null) { Debug.LogError("[Mission] missing map " + missionId); Game.UI?.Toast("Mission file missing: " + missionId); return false; }
            LevelData data;
            try { data = MapParser.Parse(text.text); }
            catch (System.Exception e) { Debug.LogError($"[Mission] parse error in {missionId}: {e.Message}"); Game.UI?.Toast("Mission file error: " + e.Message); return false; }

            Info = Missions.Get(missionId) ?? new MissionInfo { Id = missionId, Title = data.Title };
            Index = Missions.IndexOf(missionId);
            Data = data;

            var c = Game.Campaign;
            if (save != null && !string.IsNullOrEmpty(save.CampaignJson))
            {
                var restored = JsonUtility.FromJson<CampaignState>(save.CampaignJson);
                if (restored != null) { Game.Campaign = c = restored; c.ClampLoadout(); }
            }
            if (save == null)
            {
                _campaignAtStart = c != null ? JsonUtility.ToJson(c) : null;
                _loadsThisRun = 0;
            }
            // a load (from the menu, or after another mission) restarts to its own night's start, not whatever ran last
            else _campaignAtStart = CampaignAtStart(save);
            _vitaeAtStart = c != null ? (save != null ? _vitaeAtStartFromSave(save) : c.Vitae) : 0;
            _awakeningAtStart = CampaignState.AwakeningFor(_vitaeAtStart);

            Game.ResetPauses();
            Fx.Clear();
            Weather.End();
            Evidence.Clear();

            Game.Level.Load(data);
            // Dossier countermeasures and always-on groups
            // `dossier = all` (Vane's Bastion): every habit she has shown is countered here, for this night only
            Game.MissionCounters.Clear();
            if (c != null && data.Get("dossier", "") == "all") Game.MissionCounters.AddRange(c.FullDossier());
            foreach (var cm in Game.ActiveCountermeasures) Game.Level.ActivateGroup(cm, true);
            // Caged lamps: half of every lamp, brazier and candle she could put out by hand (the same ones every load)
            if (Game.Countermeasure("cm_caged"))
            {
                var open = Game.Lights.All.Where(l => l && !l.Portable && !l.Caged && l.Snuffable && GameLight.CageEligible(l.Kind)).ToList();
                var cells = open.Select(l => new Vector2Int(Mathf.RoundToInt(l.transform.position.x), Mathf.RoundToInt(l.transform.position.z))).ToList();
                foreach (int i in GameLight.PickCaged(cells)) open[i].SetCaged(true);
            }
            foreach (var g in data.Get("groups", "").Split(',')) if (!string.IsNullOrWhiteSpace(g)) Game.Level.ActivateGroup(g.Trim(), true);
            if (c != null) foreach (var f in c.Flags) Game.Level.ActivateGroup("flag_" + f, true);
            // world flags: the campaign's state, visible to `if` conditions on rules and objectives
            var world = new List<string>();
            if (c != null && Index >= 0)
            {
                if (c.Terror > c.Rumour + 2) { Game.Level.ActivateGroup("terror", true); world.Add("terror"); }
                if (c.Rumour > c.Terror + 2) { Game.Level.ActivateGroup("rumour", true); world.Add("rumour"); }
            }
            if (c != null)
            {
                world.AddRange(Game.ActiveCountermeasures);
                foreach (var f in c.Flags) world.Add("cf_" + f);
                world.Add("tobias_" + (string.IsNullOrEmpty(c.Tobias) ? "alive" : c.Tobias));   // tobias_alive | tobias_thrall | tobias_lost
            }

            Game.AI.Setup(data);
            Vampire player = null;
            foreach (var spec in data.Entities)
            {
                try
                {
                    if (spec.Kind == "npc") Npc.Spawn(spec);
                    else if (spec.Kind == "corpse") Npc.Spawn(spec, true);
                    else if (spec.Kind == "player" && player == null)
                    {
                        var pos = data.CellToWorld(spec.X, spec.Y, Game.Level.SurfaceHeightCell(spec.X, spec.Y));
                        float yaw = spec.Opts.TryGetValue("face", out var f) ? MapParser.ParseFacing(f) : 0f;
                        player = Vampire.Spawn(pos, yaw, Game.Level.EntityRoot);
                    }
                }
                catch (System.Exception e) { Debug.LogError($"[Mission] spawn {spec.Kind} {spec.Id}: {e.Message}\n{e.StackTrace}"); }
            }
            // cross references between entities (paired=, follow=) once everything exists, group members included
            foreach (var e in Game.Level.All<Entity>()) e.Link();
            if (player == null)
            {
                Debug.LogError("[Mission] map has no player start");
                player = Vampire.Spawn(Game.Level.WorldBounds.center, 0f, Game.Level.EntityRoot);
            }

            // objectives & script
            Objectives.Clear();
            _rules.Clear(); _rules.AddRange(data.Script);
            _fired.Clear(); _timers.Clear(); _flags.Clear(); _downed.Clear();
            HudTimer = "hud"; HudLabel = null;
            foreach (var w in world) _flags.Add(w);
            // `if=a,!b` on an objective: it only exists in this run when the world agrees (e.g. the Faithful only wait
            // at the checkpoint once Rumour runs ahead of Terror)
            foreach (var o in data.Objectives)
                if (!o.Opts.TryGetValue("if", out var cond) || ConditionsHold(new List<string>(cond.Split(',')), n => _flags.Contains(n)))
                    Objectives.Add(new Objective(o));
            Stats = new MissionState();
            Detections.Clear();
            MissionTime = 0f;
            SavedAt = -1f;
            DawnLeft = data.GetFloat("dawn", 0f) > 0f ? data.GetFloat("dawn", 0f) : -1f;
            Result = null;
            DeathCause = null;
            Ended = false; Won = false;
            _autosaveT = 0f;

            Subscribe();

            var cam = Game.Cam;
            if (cam != null)
            {
                cam.SetLimits(Game.Level.WorldBounds);
                float startYaw = data.GetFloat("camyaw", 45f);
                cam.SnapTo(player.transform.position, startYaw);
                cam.FollowTarget(player.transform);
                cam.Locked = false;
            }
            Game.Audio?.SetAmbience(data.Get("ambience", "amb_night"), data.Get("ambience2", "amb_drip"));
            Game.Audio?.SetTheme(data.Get("music", null));
            Weather.Begin(Game.Level.transform, data.GetFloat("rain", 0f) > 0f, data.Get("ambience", "amb_night"), data.Get("ambience2", "amb_drip"));
            Running = true;

            if (save != null)
            {
                _applyingSave = true;
                Squad.Restoring = true;
                try { Apply(save); }
                finally { Squad.Restoring = false; _applyingSave = false; }
                foreach (var q in Squad.All) q.AfterRestore();
            }
            else
            {
                Fire("start", null);
                Game.UI?.MissionIntro(Info, data);
                StartCoroutineSafe(AutosaveNextFrame("Mission start"));
            }
            return true;
        }

        /// <summary>The campaign a loaded save's mission began with. Old saves never stored it: their own snapshot is
        /// the nearest thing, so a restart then keeps what was earned before the save rather than rolling back too far.</summary>
        public static string CampaignAtStart(MissionSave s) => !string.IsNullOrEmpty(s.CampaignAtStartJson) ? s.CampaignAtStartJson : s.CampaignJson;

        int _vitaeAtStartFromSave(MissionSave s)
        {
            // the vitae the campaign had when the mission began is kept in the mission state
            foreach (var t in s.Mission.Timers) if (t.Key == "#vitae0") return Mathf.RoundToInt(t.Value);
            return Game.Campaign != null ? Game.Campaign.Vitae : 0;
        }

        void StartCoroutineSafe(System.Collections.IEnumerator e) { if (isActiveAndEnabled) StartCoroutine(e); }

        System.Collections.IEnumerator AutosaveNextFrame(string label)
        {
            yield return null;
            yield return null;
            if (Running && !Ended) Autosave(label, true);
        }

        /// <summary>Destroys the current mission world.</summary>
        public void Teardown()
        {
            StopAllCoroutines();
            Running = false;
            Unsubscribe();
            GameEvents.ClearMissionSubscribers();
            if (Game.Player) { Destroy(Game.Player.gameObject); Game.Player = null; }
            if (Game.Level != null) Game.Level.Unload();
            Fx.Clear();
            Evidence.Clear();
            Objectives.Clear();
            Game.ResetPauses();
        }

        /// <summary>Restart from the beginning; the campaign rolls back to its state when the mission began.</summary>
        public void Restart()
        {
            var id = Info != null ? Info.Id : null;
            if (id == null) return;
            if (!string.IsNullOrEmpty(_campaignAtStart))
            {
                var c = JsonUtility.FromJson<CampaignState>(_campaignAtStart);
                if (c != null) Game.Campaign = c;
            }
            Begin(id);
        }

        /// <summary>Leave the mission without completing it; progress during it is discarded.</summary>
        public void Abandon()
        {
            if (!string.IsNullOrEmpty(_campaignAtStart))
            {
                var c = JsonUtility.FromJson<CampaignState>(_campaignAtStart);
                if (c != null) Game.Campaign = c;
            }
            Teardown();
        }

        // ================================================================== events
        bool _subscribed;
        void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            GameEvents.NpcKilled += OnKilled;
            GameEvents.NpcFed += OnFed;
            GameEvents.NpcDowned += OnDowned;
            GameEvents.NpcSpottedPlayer += OnSpotted;
            GameEvents.NpcFoundEvidence += OnEvidence;
            GameEvents.Interacted += OnInteracted;
            GameEvents.ZoneEntered += OnZone;
            GameEvents.AlarmChanged += OnAlarm;
            GameEvents.FlagSet += OnFlag;
            GameEvents.SecretFound += OnSecret;
            GameEvents.BodyDisposed += OnBodyDisposed;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            GameEvents.NpcKilled -= OnKilled;
            GameEvents.NpcFed -= OnFed;
            GameEvents.NpcDowned -= OnDowned;
            GameEvents.NpcSpottedPlayer -= OnSpotted;
            GameEvents.NpcFoundEvidence -= OnEvidence;
            GameEvents.Interacted -= OnInteracted;
            GameEvents.ZoneEntered -= OnZone;
            GameEvents.AlarmChanged -= OnAlarm;
            GameEvents.FlagSet -= OnFlag;
            GameEvents.SecretFound -= OnSecret;
            GameEvents.BodyDisposed -= OnBodyDisposed;
        }

        public static bool IsPlayerKill(Npc n)
        {
            if (n == null || n.Preplaced) return false;
            var k = n.KilledBy ?? "";
            return k != "combat" && k != "unknown" && k != "friendly" && k != "accident" && k != "";
        }

        void OnKilled(Npc n)
        {
            if (Running && !Ended) Squad.NotifyDowned(n);
            if (!Running || Ended || _applyingSave) return;
            bool byPlayer = IsPlayerKill(n);
            if (byPlayer)
            {
                Stats.Kills++;
                if (Game.Campaign != null) Game.Campaign.TotalKills++;
                foreach (var o in Objectives)
                    if (o.Active && (o.Type == "nokill" || (o.Type == "nokill_type" && o.Spec.Args.Contains(n.Arch.Id)))) FailObjective(o.Id);
            }
            foreach (var o in Objectives)
            {
                if (!o.Active) continue;
                if (o.Type == "kill" && o.Spec.Args.Count > 0 && o.Spec.Args[0] == n.Id)
                {
                    // `kill <id> accident`: only an arranged accident will do
                    if (o.Spec.Args.Contains("accident") && n.KilledBy != "accident") FailObjective(o.Id, "It had to look like an accident.");
                    else CompleteObjective(o.Id);
                }
                else if (o.Type == "take" && o.Spec.Args.Count > 0 && o.Spec.Args[0] == n.Id) CompleteOrHold(o);
                else if (o.Type == "kill_all" && o.Spec.Args.Contains(n.Id)) { o.Done.Add(n.Id); o.Progress = o.Done.Count / (float)o.PartsTotal; o.FlashT = 2f; if (o.Done.Count >= o.PartsTotal) CompleteObjective(o.Id); }
                else if ((o.Type == "deliver" || o.Type == "protect") && o.Spec.Args.Count > 0 && o.Spec.Args[0] == n.Id && !o.Spec.Opts.ContainsKey("dead")) FailObjective(o.Id, "They are dead.");
                else if (o.Type == "evacuate" && o.Spec.Args.Contains(n.Id)) FailObjective(o.Id, $"{n.DisplayName} is dead.");
            }
            Fire("kill", n.Id);
            if (byPlayer) Fire("kill", "any");
            if (byPlayer && n.Arch.Faction != Faction.Vigil && VigilAt(n.transform.position) is Npc v)
            {
                // a Vigil man standing over the body takes the blame: `on framed <npc>`
                Fire("framed", n.Id);
                Fire("framed", "any");
                GameEvents.RaiseToast($"{v.DisplayName} was found standing over {n.DisplayName}.");
            }
        }

        /// <summary>A living, standing Vigil NPC within 6 m (and the same floor) of a death, or null.</summary>
        static Npc VigilAt(Vector3 p)
        {
            if (Game.AI == null) return null;
            foreach (var m in Game.AI.Npcs)
                if (m && m.IsAlive && !m.Incapacitated && m.gameObject.activeInHierarchy && m.Arch.Faction == Faction.Vigil
                    && StandsOver(m.transform.position, p)) return m;
            return null;
        }

        /// <summary>Close enough to a death, on the same floor, to be blamed for it (pure; unit-tested).</summary>
        public static bool StandsOver(Vector3 witness, Vector3 death) =>
            Util.FlatDistance(witness, death) < 6f && Mathf.Abs(witness.y - death.y) < 2.5f;

        void OnFed(Npc n, bool drained)
        {
            if (!Running || Ended || _applyingSave) return;
            Squad.NotifyFed(n, drained);
            if (drained) Stats.Drains++; else Stats.Sips++;
            var type = n.Arch.Blood.ToString().ToLowerInvariant();
            if (!Stats.FedTypes.Contains(type)) Stats.FedTypes.Add(type);
            foreach (var o in Objectives)
            {
                if (!o.Active) continue;
                if (o.Type == "feed_types" && o.Spec.Args.Contains(type) && o.Done.Add(type))
                {
                    o.Progress = o.Done.Count / (float)o.PartsTotal;
                    o.FlashT = 2f;
                    if (o.Done.Count >= o.PartsTotal) CompleteObjective(o.Id);
                }
                else if (o.Type == "feed" && o.Spec.Args.Count > 0 && (o.Spec.Args[0] == n.Id || o.Spec.Args[0] == n.Arch.Id)) CompleteObjective(o.Id);
                else if (o.Type == "nofeed") FailObjective(o.Id);
                else if (o.Type == "feedonly" && !o.Spec.Args.Contains(type)) FailObjective(o.Id);
            }
            Fire("feed", n.Id);
            Fire("feed", n.Arch.Id);
            Fire("feed", "any");
        }

        void OnSpotted(Npc n)
        {
            if (!Running || Ended || _applyingSave) return;
            Stats.TimesSpotted++;
            if (Game.Player != null) SpottedRecord.Keep(Detections, SpottedRecord.Take(n, Game.Player, MissionTime));
            if (n.Squad != null) n.Squad.NotifySeen(Game.Player != null ? Game.Player.Feet : n.transform.position);
            foreach (var o in Objectives) if (o.Active && o.Type == "nodetect") FailObjective(o.Id);
            Fire("spotted", n.Id);
            Fire("spotted", "any");
        }

        void OnEvidence(Npc n, string kind)
        {
            if (!Running || Ended || _applyingSave) return;
            if (kind == "body") { Stats.BodiesFound++; foreach (var o in Objectives) if (o.Active && o.Type == "nobodies") FailObjective(o.Id); }
            Fire("evidence", kind);
        }

        void OnInteracted(string id)
        {
            if (!Running || Ended || _applyingSave) return;
            foreach (var o in Objectives)
            {
                if (!o.Active) continue;
                if (o.Type == "interact" && o.Spec.Args.Count > 0 && o.Spec.Args[0] == id) CompleteOrHold(o);
                else if ((o.Type == "interact_all" && o.Spec.Args.Contains(id) && o.Done.Add(id))
                         || (o.Type == "evacuate" && id.EndsWith(".evac") && o.Spec.Args.Contains(id.Substring(0, id.Length - 5)) && o.Done.Add(id)))
                {
                    o.Progress = o.Done.Count / (float)o.PartsTotal;
                    o.FlashT = 2f;
                    if (o.Done.Count >= o.PartsTotal) CompleteObjective(o.Id);
                    else Game.UI?.Toast($"{o.Spec.Text}{o.ProgressText}");
                }
            }
            Fire("interact", id);
        }

        /// <summary>"on down &lt;id&gt;": the NPC is out of the way, however it happened (dazed, enthralled, killed).</summary>
        void OnDowned(Npc n)
        {
            if (Running && !Ended) Squad.NotifyDowned(n);
            if (!Running || Ended || _applyingSave || n == null) return;
            if (n.State == NpcState.Thrall)
            {
                foreach (var o in Objectives)
                    if (o.Active && o.Type == "take" && o.Spec.Args.Count > 0 && o.Spec.Args[0] == n.Id) CompleteOrHold(o);
                Fire("thrall", n.Id);
            }
            if (!_downed.Add(n.Id)) return;
            Fire("down", n.Id);
        }
        readonly HashSet<string> _downed = new HashSet<string>();

        /// <summary>A squad broke (routed: its men ran; else it was taken apart). `break sq… [count=N] [how=rout]`
        /// counts it; the script hears `broken &lt;id&gt;` (and `routed &lt;id&gt;` when they ran).</summary>
        public void OnSquadBroken(Squad q)
        {
            if (!Running || Ended || _applyingSave || q == null) return;
            foreach (var o in Objectives)
            {
                if (!o.Active || o.Type != "break" || !o.Spec.Args.Contains(q.Id)) continue;
                if (o.Spec.Opts.TryGetValue("how", out var how) && how == "rout" && !q.Routed) { FailObjective(o.Id, $"{q.DisplayName} died where it stood. Nobody was left to tell of it."); continue; }
                if (!o.Done.Add(q.Id)) continue;
                o.Progress = o.Done.Count / (float)o.PartsTotal;
                o.FlashT = 2f;
                if (o.Done.Count >= o.PartsTotal) CompleteObjective(o.Id);
                else Game.UI?.Toast($"{o.Spec.Text}{o.ProgressText}");
            }
            Fire("broken", q.Id);
            Fire("broken", "any");
            if (q.Routed) { Fire("routed", q.Id); Fire("routed", "any"); }
        }

        void OnZone(string id)
        {
            if (!Running || Ended) return;
            Fire("enter", id);
        }

        void OnAlarm(int level)
        {
            if (!Running || Ended || _applyingSave) return;
            if (level >= 2)
            {
                Stats.Alarms++;
                foreach (var o in Objectives) if (o.Active && o.Type == "noalert") FailObjective(o.Id);
                Fire("alert", null);
            }
        }

        void OnFlag(string flag)
        {
            if (!Running || Ended) return;
            _flags.Add(flag);
            Fire("flag", flag);
        }

        void OnSecret(string id)
        {
            if (!Running || Ended || _applyingSave) return;
            if (!Stats.Secrets.Contains(id)) Stats.Secrets.Add(id);
            Game.UI?.Toast("Secret found");
            Game.Audio?.Play2D("secret", 0.7f);
            Fire("secret", id);
        }

        void OnBodyDisposed(string how)
        {
            if (!Running || Ended) return;
            Stats.Disposed++;
            foreach (var o in Objectives)
            {
                if (!o.Active || o.Type != "dispose") continue;
                string want = o.Spec.Args.Count > 0 ? o.Spec.Args[0] : "any";
                if (want != "any" && want != how) continue;
                o.Done.Add("b" + o.Done.Count);
                o.Progress = o.Done.Count / (float)o.PartsTotal;
                o.FlashT = 2f;
                if (o.Done.Count >= o.PartsTotal) CompleteObjective(o.Id);
            }
            Fire("dispose", how);
        }

        public void OnLockdown()
        {
            if (!Running || Ended || _applyingSave) return;
            foreach (var o in Objectives) if (o.Active && (o.Type == "noalert" || o.Type == "nolockdown")) FailObjective(o.Id);
            Fire("lockdown", null);
        }

        public void OnPlayerDied(string cause)
        {
            if (!Running || Ended) return;
            Ended = true;
            Won = false;
            DeathCause = cause;
            Game.SlowMo = 0.35f;
            Game.ApplyTime();
            Game.Audio?.Play2D("death", 1f);
            Fire("death", null);
            StartCoroutine(ShowDeathLater(cause));
        }

        System.Collections.IEnumerator ShowDeathLater(string cause)
        {
            yield return new WaitForSecondsRealtime(1.6f);
            Game.SlowMo = 1f;
            Game.MenuPaused = true;
            Game.UI?.ShowDeath(cause);
        }

        // ================================================================== frame
        void Update()
        {
            if (!Running || Ended) return;
            float dt = Time.deltaTime;
            var input = Game.Input;
            if (input != null && Game.UI != null && !Game.UI.ModalOpen)
            {
                if (input.QuickSave.WasPressedThisFrame()) QuickSave();
                if (input.QuickLoad.WasPressedThisFrame()) { QuickLoad(); return; }
            }
            if (dt <= 0f) return;
            MissionTime += dt;
            if (Game.Campaign != null) Game.Campaign.PlayTime += dt;

            foreach (var o in Objectives) if (o.FlashT > 0f) o.FlashT -= dt;
            TickObjectives();
            TickTimers(dt);
            TickDawn(dt);
            TickCodex(dt);

            if (Difficulties.Current.AutosaveMode == 0)
            {
                _autosaveT += dt;
                if (_autosaveT > 180f && CanAutosaveNow()) { _autosaveT = 0f; Autosave("Timed autosave", false); }
            }
        }

        float _codexT;
        const float CodexRange = 18f;

        /// <summary>First sightings for the Codex: a human within reach of Ilse's eyes with no wall between them.</summary>
        void TickCodex(float dt)
        {
            _codexT -= dt;
            if (_codexT > 0f) return;
            _codexT = 0.5f;
            var c = Game.Campaign; var p = Game.Player;
            if (c == null || p == null || p.Dead || Game.AI == null) return;
            var eye = p.transform.position + Vector3.up * 1.5f;
            foreach (var n in Game.AI.Npcs)
            {
                if (!n || n.Arch == null || c.Bestiary.Contains(n.Arch.Id) || n.Arch.Id == "notable") continue;
                if ((n.transform.position - p.transform.position).sqrMagnitude > CodexRange * CodexRange) continue;
                if (Physics.Linecast(eye, n.Eye, Layers.SightMask, QueryTriggerInteraction.Ignore)) continue;
                if (c.MarkSeen(n.Arch.Id)) Game.UI?.Toast($"Codex: {n.Arch.Name}");
            }
        }

        bool CanAutosaveNow()
        {
            var p = Game.Player;
            if (p == null || p.Dead || p.Feeding != null) return false;
            if (Game.AI != null && (Game.AI.Alarm >= 2 || Game.AI.MaxDetection > 0.3f)) return false;
            return true;
        }

        bool PlayerInRect(Objective o)
        {
            var p = Game.Player;
            if (p == null || p.Dead || !o.TryRect(out var r)) return false;
            var c = Data.WorldToCell(p.transform.position);
            return r.Contains(c);
        }

        /// <summary>`needs=a[,b…]`: each is a completed objective or a set mission flag (M14's `flee` waits on the
        /// `abbess.destroy` choice: a hidden objective is still live, so walking out first would win the night).</summary>
        bool NeedsMet(Objective o)
        {
            if (!o.Spec.Opts.TryGetValue("needs", out var need)) return true;
            foreach (var n in need.Split(','))
                if (!IsObjectiveComplete(n) && !HasFlag(n)) return false;
            return true;
        }

        /// <summary>A `take` or `interact` done before its `needs=` are met is held (as a saved flag) and completes the
        /// moment they are, so a mis-ordered objective can neither win early nor be lost (K22).</summary>
        void CompleteOrHold(Objective o)
        {
            if (NeedsMet(o)) CompleteObjective(o.Id);
            else _flags.Add(o.HeldFlag);
        }

        void TickObjectives()
        {
            var p = Game.Player;
            if (p == null) return;
            for (int i = 0; i < Objectives.Count; i++)
            {
                var o = Objectives[i];
                if (!o.Active) continue;
                switch (o.Type)
                {
                    case "reach":
                        if (PlayerInRect(o) && NeedsMet(o)) CompleteObjective(o.Id);
                        break;
                    case "take":
                    case "interact":
                        if (_flags.Contains(o.HeldFlag) && NeedsMet(o)) CompleteObjective(o.Id);
                        break;
                    case "escape":
                        if (!PlayerInRect(o) || !NeedsMet(o)) break;
                        if (OtherPrimariesComplete(o)) CompleteObjective(o.Id);
                        else if (o.Discovered && Time.unscaledTime - _escapeToastT > 6f)
                        {
                            _escapeToastT = Time.unscaledTime;
                            Game.UI?.Toast("There is still work to do here.");
                        }
                        break;
                    case "deliver":
                    {
                        if (o.Spec.Args.Count == 0) break;
                        var n = Game.Level.Get<Npc>(o.Spec.Args[0]);
                        if (!n || !o.TryRect(out var r)) break;
                        if (n.Carried) break;
                        bool alive = n.IsAlive;
                        if (!alive && !o.Spec.Opts.ContainsKey("dead")) break;
                        if (r.Contains(Data.WorldToCell(n.transform.position)))
                        {
                            CompleteObjective(o.Id);
                            if (o.Spec.Opts.ContainsKey("consume")) { n.Dispose("delivered"); }
                        }
                        break;
                    }
                    case "escort":
                    {
                        // freed prisoners led into the area slip away; with loose=1 a fledgling turned loose also counts
                        if (!o.TryRect(out var er)) break;
                        bool loose = o.Spec.Opts.ContainsKey("loose");
                        int lost = 0, before = o.Done.Count;
                        var ids = o.EscortIds;
                        foreach (var id in ids)
                        {
                            if (o.Done.Contains(id)) continue;
                            var n = Game.Level.Get<Npc>(id);
                            if (!n) { lost++; continue; }
                            if (n.Disposed && n.State == NpcState.Escort) { o.Done.Add(id); continue; }
                            if (loose && n.Loose) { o.Done.Add(id); continue; }
                            if (!n.IsAlive || (n.Loose && !loose)) { lost++; continue; }
                            if (n.State == NpcState.Escort && er.Contains(Data.WorldToCell(n.transform.position))) { n.Escape(); o.Done.Add(id); }
                        }
                        if (o.Done.Count != before) o.FlashT = 2f;
                        o.Progress = o.Done.Count / (float)o.PartsTotal;
                        if (o.Done.Count >= o.PartsTotal) CompleteObjective(o.Id);
                        else if (ids.Count - lost < o.PartsTotal) FailObjective(o.Id, ids.Count == 1 ? "They are lost." : "Too many of them are lost.");
                        break;
                    }
                    case "valves":
                    {
                        // every listed gas main shut at once (a lamplighter reopening one undoes the progress)
                        int before = o.Done.Count;
                        o.Done.Clear();
                        foreach (var id in o.Spec.Args) { var v = Game.Level.Get<Valve>(id); if (v && v.State) o.Done.Add(id); }
                        if (o.Done.Count != before) o.FlashT = 2f;
                        o.Progress = o.Done.Count / (float)o.PartsTotal;
                        if (o.Done.Count >= o.PartsTotal) CompleteObjective(o.Id);
                        break;
                    }
                    case "survive":
                        if (o.Spec.Args.Count > 0 && float.TryParse(o.Spec.Args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var secs))
                        {
                            o.Progress = Mathf.Clamp01(MissionTime / secs);
                            if (MissionTime >= secs) CompleteObjective(o.Id);
                        }
                        break;
                    case "flag":
                        if (o.Spec.Args.Count > 0 && _flags.Contains(o.Spec.Args[0])) CompleteObjective(o.Id);
                        break;
                    case "hpfloor":
                    {
                        // conduct: her vitality never fell below N% of its maximum
                        float pct = o.Spec.Args.Count > 0 && float.TryParse(o.Spec.Args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var hp) ? hp : 50f;
                        if (!Vampire.GodMode && p.HP < p.MaxHP * pct / 100f) FailObjective(o.Id, "She bled too much.");
                        break;
                    }
                    case "noholy":
                        if (Game.Lights != null && Game.Lights.BurnAt(p.Feet) > 0f) FailObjective(o.Id, "Holy light touched her.");
                        break;
                    case "item":
                        if (o.Spec.Args.Count > 0 && p.HasItem(o.Spec.Args[0])) CompleteObjective(o.Id);
                        break;
                }
            }
        }

        bool OtherPrimariesComplete(Objective except)
        {
            foreach (var o in Objectives)
                if (o != except && o.Primary && !o.IsConduct && o.State != ObjectiveState.Complete) return false;
            return true;
        }

        public bool IsObjectiveComplete(string id) { var o = Find(id); return o != null && o.Complete; }
        public Objective Find(string id) => Objectives.Find(x => x.Id == id);

        /// <summary>True when setting <paramref name="n"/> down at <paramref name="at"/> would deliver them (an active
        /// deliver objective's target rect): the drop must place them there, never tip them into nearby water.</summary>
        public bool IsDeliveryDrop(Npc n, Vector3 at)
        {
            if (!n) return false;
            foreach (var o in Objectives)
                if (o.Active && o.Type == "deliver" && o.Spec.Args.Count > 0 && o.Spec.Args[0] == n.Id && o.TryRect(out var r)
                    && r.Contains(Data.WorldToCell(at))) return true;
            return false;
        }

        void TickTimers(float dt)
        {
            if (_timers.Count == 0) return;
            List<string> done = null;
            var keys = _timerKeys;
            keys.Clear(); keys.AddRange(_timers.Keys);
            foreach (var k in keys)
            {
                _timers[k] -= dt;
                if (_timers[k] <= 0f) (done ??= new List<string>()).Add(k);
            }
            if (done == null) return;
            foreach (var k in done) { _timers.Remove(k); Fire("timer", k); }
        }

        readonly List<string> _timerKeys = new List<string>();

        public float TimerLeft(string name) => _timers.TryGetValue(name, out var t) ? t : -1f;

        /// <summary>`crescendo <secs>`: the orchestra drowns the house; every noise but a bell or a lure carries a quarter as far.</summary>
        public bool Hushed => TimerLeft("#hush") > 0f;
        public const float HushFactor = 0.25f;

        /// <summary>
        /// `blast x y radius ["Death text"]`: an explosion. Everyone inside the radius dies (cause "blast": her doing),
        /// everyone out to 2.5× panics, and Ilse dies if she is inside it. Bodies read as a disaster, not a murder.
        /// </summary>
        public void Blast(Vector3 at, float radius, string deathText = null)
        {
            Game.Audio?.Play2D("explosion", 1f);
            Game.Cam?.Shake(1.6f);
            Fx.Ring(at + Vector3.up * 0.2f, radius, new Color(1f, 0.55f, 0.2f, 0.8f), 1.2f);
            Fx.Fireball(at + Vector3.up * 1.5f, Mathf.Clamp(radius * 0.5f, 3f, 14f));
            for (int i = 0; i < 9; i++)
            {
                var off = Quaternion.Euler(0, i * 40f, 0) * Vector3.forward * Random.Range(0f, radius * 0.5f);
                Fx.Smoke(at + off + Vector3.up * Random.Range(1f, 6f), new Color(0.12f, 0.1f, 0.09f, 0.9f), 4f + Random.value * 3f, 6f);
                Fx.Sparks(at + off + Vector3.up * Random.Range(0.5f, 4f), new Color(1f, 0.6f, 0.2f));
            }
            Game.Noise?.Emit(at, radius * 4f, Stealth.NoiseKind.Gunshot, null);
            if (Game.AI != null)
                foreach (var n in Game.AI.Npcs.ToArray())
                {
                    if (!n || !n.IsAlive || !n.gameObject.activeInHierarchy || n.Friendly) continue;
                    float d = Util.FlatDistance(n.transform.position, at);
                    if (d < radius) n.Die("blast");
                    else if (d < radius * 2.5f && !n.Rescue) n.EnterPanic(at);
                }
            var p = Game.Player;
            if (p != null && !p.Dead && Util.FlatDistance(p.transform.position, at) < radius)
                Lose(deathText ?? "Ilse was too close when it went up.");
        }

        void TickDawn(float dt)
        {
            if (DawnLeft < 0f) return;
            float before = DawnLeft;
            DawnLeft -= dt;
            if (before > 60f && DawnLeft <= 60f) { Game.UI?.Toast("The sky is paling. One minute until dawn."); Game.Audio?.Play2D("bell", 0.6f, 0.7f); }
            if (before > 10f && DawnLeft <= 10f) Game.UI?.Toast("Dawn!");
            if (DawnLeft <= 0f)
            {
                DawnLeft = 0f;
                var p = Game.Player;
                if (p != null && !p.Dead && !p.Concealed) { p.Damage(20f * dt, false, null, true, true); if (Random.value < dt * 6f) Fx.Sparks(p.transform.position + Vector3.up, Mats.Pal.Ember); }
                if (p != null && !p.Dead && p.Concealed) { DawnLeft = -1f; Lose("Dawn came. Ilse slept where she hid, and they found her there."); }
            }
        }

        // ================================================================== objectives
        public void CompleteObjective(string id)
        {
            var o = Find(id);
            if (o == null || !o.Active || Ended) return;
            // K18 tripwire: an escape should only complete with Ilse inside its area; the stack names the caller
            if (o.Type == "escape" && !_applyingSave && !PlayerInRect(o))
                Debug.LogError($"[K18] escape objective '{id}' completed with Ilse at cell {Data.WorldToCell(Game.Player ? Game.Player.transform.position : Vector3.zero)}\n{System.Environment.StackTrace}");
            o.State = ObjectiveState.Complete;
            o.Progress = 1f;
            o.FlashT = 3f;
            bool wasHidden = !o.Discovered;
            o.Discovered = true;
            Game.UI?.ObjectiveToast(o, wasHidden);
            Game.Audio?.Play2D("objective", 0.8f);
            GameEvents.RaiseObjectiveCompleted(id);
            Fire("complete", id);
            Fire("objective", id);
            if (!_applyingSave && Difficulties.Current.AutosaveMode <= 1 && !o.IsConduct) StartCoroutineSafe(AutosaveNextFrame("Objective: " + o.Spec.Text));
            CheckWin(o);
        }

        public void FailObjective(string id, string why = null)
        {
            var o = Find(id);
            if (o == null || !o.Active || Ended) return;
            o.State = ObjectiveState.Failed;
            o.FlashT = 3f;
            Game.UI?.ObjectiveToast(o, false);
            Game.Audio?.Play2D("ui_error", 0.6f);
            GameEvents.RaiseObjectiveFailed(id);
            Fire("fail", id);
            if (o.Primary && !o.Spec.Opts.ContainsKey("soft")) Lose(why ?? ("Objective failed: " + o.Spec.Text));
        }

        public void Reveal(string id)
        {
            var o = Find(id);
            if (o == null || o.Discovered) return;
            o.Discovered = true;
            o.FlashT = 3f;
            Game.UI?.ObjectiveToast(o, true);
            if (!Stats.Discovered.Contains(id)) Stats.Discovered.Add(id);
            Fire("discover", id);
        }

        void CheckWin(Objective last)
        {
            if (Data.GetInt("manual_win", 0) == 1) return;
            bool hasEscape = Objectives.Exists(x => x.Type == "escape" && x.Primary);
            if (hasEscape && !(last.Type == "escape" && last.Primary)) return;
            foreach (var o in Objectives)
                if (o.Primary && !o.IsConduct && !o.Complete) return;
            // let the "complete" script rules (dialogue, choices) run first
            StartCoroutineSafe(WinSoon());
        }

        System.Collections.IEnumerator WinSoon()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            while (Game.UI != null && Game.UI.ModalOpen) yield return null;
            // the closing lines play out over a frozen world before the debrief replaces them
            float cap = Time.unscaledTime + 40f;
            if (Game.UI != null && Game.UI.SubtitlesBusy) Game.ClosingPaused = true;
            while (Game.UI != null && (Game.UI.SubtitlesBusy || Game.UI.ModalOpen) && Time.unscaledTime < cap && Running && !Ended) yield return null;
            Game.ClosingPaused = false;
            if (Running && !Ended) Win();
        }

        // ================================================================== end of mission
        public void Win()
        {
            if (Ended) return;
            Ended = true;
            Won = true;
            // conduct objectives succeed if never broken
            foreach (var o in Objectives)
                if (o.Active && o.IsConduct) { o.State = ObjectiveState.Complete; o.Discovered = true; }
            Result = BuildResult(true, null);
            Game.Audio?.Play2D("victory", 0.9f);
            SaveSystem.ClearMissionSaves();
            if (Game.Campaign != null) SaveSystem.SaveCampaign(Game.Campaign);
            Game.MenuPaused = true;
            Game.UI?.ShowDebrief(Result);
        }

        public void Lose(string reason)
        {
            if (Ended) return;
            Ended = true;
            Won = false;
            DeathCause = reason;
            Game.Audio?.Play2D("death", 0.8f);
            StartCoroutine(ShowFailLater(reason));
        }

        System.Collections.IEnumerator ShowFailLater(string reason)
        {
            yield return new WaitForSecondsRealtime(1.0f);
            Game.MenuPaused = true;
            Game.UI?.ShowMissionFailed(reason);
        }

        MissionResult BuildResult(bool won, string cause)
        {
            var c = Game.Campaign;
            var r = new MissionResult
            {
                MissionId = Info.Id, Title = Info.Title, Won = won, Cause = cause, Time = MissionTime,
                Kills = Stats.Kills, Sips = Stats.Sips, Drains = Stats.Drains, TimesSpotted = Stats.TimesSpotted,
                Alarms = Stats.Alarms, BodiesFound = Stats.BodiesFound, Disposed = Stats.Disposed, Loads = _loadsThisRun,
                NeverSpotted = Stats.TimesSpotted == 0, AwakeningBefore = _awakeningAtStart
            };
            r.Detections.AddRange(Detections);
            if (c == null) return r;

            var rec = c.Record(Info.Id);
            r.FirstClear = !rec.Completed;
            int marks = 0, vitae = 0;   // objective rewards: paid on first completion only
            if (!rec.Completed) { marks++; vitae += CampaignEconomy.PrimaryVitae; rec.Completed = true; }
            foreach (var o in Objectives)
            {
                if (o.Primary) continue;
                if (o.Complete)
                {
                    r.Optionals.Add(o.Spec.Text);
                    if (!rec.Optionals.Contains(o.Id)) { rec.Optionals.Add(o.Id); vitae += CampaignEconomy.OptionalVitae; }   // Vitae only: Marks come from challenges (P8)
                }
                else if (o.Visible) r.OptionalsMissed.Add(o.Spec.Text);
                else r.OptionalsMissed.Add("???");
            }
            foreach (var s in Stats.Secrets)
            {
                var sec = Game.Level != null ? Game.Level.Get<Secret>(s) : null;
                r.Secrets.Add(sec != null ? sec.Title : s);
                if (!rec.Secrets.Contains(s))
                {
                    rec.Secrets.Add(s);
                    vitae += CampaignEconomy.SecretVitae;
                    if (sec == null || !sec.LoreOnly) marks++;
                }
            }
            r.Par = Info.Par;
            if (rec.Challenges == null) rec.Challenges = new List<string>();   // saves from before challenges
            foreach (var ch in Progression.Challenges.Earned(r, Info.Par, c.Difficulty))
            {
                r.Challenges.Add(ch);
                if (!rec.Challenges.Contains(ch)) { rec.Challenges.Add(ch); r.NewChallenges.Add(ch); marks += Progression.Challenges.MarkReward; }
            }
            // notable drains already granted their Mark when fed
            c.Marks += marks;
            r.MarksEarned = marks;
            c.AddVitae(vitae);
            rec.BestTime = rec.BestTime <= 0f ? MissionTime : Mathf.Min(rec.BestTime, MissionTime);
            r.BestTime = rec.BestTime;
            rec.Kills = Stats.Kills; rec.Feeds = Stats.Sips + Stats.Drains; rec.TimesSpotted = Stats.TimesSpotted;
            rec.NeverSpotted = rec.NeverSpotted || Stats.TimesSpotted == 0;

            // world consequence: Terror (killing, being seen as a monster) vs Rumour (sparing, being glimpsed as a kindness)
            int civKills = 0;
            if (Game.Level != null)
                foreach (var n in Game.Level.All<Npc>())
                    if (n && !n.IsAlive && IsPlayerKill(n) && (n.Arch.Faction == Faction.None || n.Arch.Morale == Morale.Civilian)) civKills++;
            int terror = civKills + (Stats.Kills >= 6 ? 1 : 0) + (Stats.BodiesFound >= 3 ? 1 : 0);
            int rumour = Stats.Sips / 3 + (Stats.Kills == 0 ? 1 : 0);
            terror += Data.GetInt("terror", 0);
            rumour += Data.GetInt("rumour", 0);
            c.Terror += terror; c.Rumour += rumour;
            r.TerrorDelta = terror; r.RumourDelta = rumour;

            if (Index >= 0 && c.MissionIndex <= Index) c.MissionIndex = Index + 1;
            r.VitaeEarned = c.Vitae - _vitaeAtStart;
            r.AwakeningAfter = c.Awakening;

            // The Dossier: the Vigil studies her habits and answers them (unless she has just burned everything it knew)
            bool burned = c.BurnArchive();
            if (!burned) r.NewCountermeasures.AddRange(c.UpdateCountermeasures(Index));
            c.ClampLoadout();
            return r;
        }

        // ================================================================== saves
        public MissionSave Capture(string label)
        {
            var s = new MissionSave
            {
                MissionId = Info.Id, MissionIndex = Index, Label = label,
                Difficulty = Game.Campaign != null ? (int)Game.Campaign.Difficulty : 1,
                PlayTime = Game.Campaign != null ? Game.Campaign.PlayTime : MissionTime,
                CampaignJson = Game.Campaign != null ? JsonUtility.ToJson(Game.Campaign) : null,
                CampaignAtStartJson = _campaignAtStart
            };
            Game.AI.Capture(s);
            s.Player = Game.Player.Capture();
            Game.Level.SaveState(s.Entities, s.Groups);
            foreach (var st in Evidence.Stains)
                if (st && !st.Preexisting) s.Stains.Add(new StainSave { P = st.transform.position, Size = st.Size, Found = st.Found, Huge = st.Huge, Seq = st.Seq });

            var m = s.Mission;
            m.Time = MissionTime;
            foreach (var o in Objectives)
            {
                var os = new ObjectiveSave { Id = o.Id, State = (int)o.State, Progress = o.Progress };
                os.Done.AddRange(o.Done);
                if (o.Discovered && o.Hidden) m.Discovered.Add(o.Id);
                m.Objectives.Add(os);
            }
            m.FiredRules.AddRange(_fired);
            foreach (var kv in _timers) m.Timers.Add(new Counter { Key = kv.Key, Value = kv.Value });
            m.Timers.Add(new Counter { Key = "#vitae0", Value = _vitaeAtStart });
            m.Timers.Add(new Counter { Key = "#dawn", Value = DawnLeft });
            m.Timers.Add(new Counter { Key = "#hud|" + HudTimer + "|" + (HudLabel ?? ""), Value = 0f });
            m.Timers.Add(new Counter { Key = "#rain", Value = Weather.Raining ? 1f : 0f });
            m.Flags.AddRange(_flags);
            m.Secrets.AddRange(Stats.Secrets);
            m.Kills = Stats.Kills; m.Sips = Stats.Sips; m.Drains = Stats.Drains; m.TimesSpotted = Stats.TimesSpotted;
            m.Alarms = Stats.Alarms; m.BodiesFound = Stats.BodiesFound; m.Disposed = Stats.Disposed; m.Loads = _loadsThisRun;
            m.FedTypes.AddRange(Stats.FedTypes);

            if (Game.Cam != null) { s.CamPivot = Game.Cam.Pivot; s.CamYaw = Game.Cam.Yaw; s.CamDistance = Game.Cam.Distance; }
            return s;
        }

        void Apply(MissionSave s)
        {
            Game.Level.LoadState(s.Entities, s.Groups);
            Game.AI.Restore(s);
            foreach (var st in s.Stains)
            {
                var b = st.Seq >= 0 ? Evidence.DropTrail(st.P, Game.Level.DynamicRoot, st.Seq) : Evidence.SpawnStain(st.P, Game.Level.DynamicRoot, false, 1f, st.Huge);
                b.transform.localScale = new Vector3(st.Size, st.Size, 1f);
                b.Size = st.Size;
                b.Found = st.Found;
            }
            if (s.Player != null) Game.Player.Restore(s.Player);

            var m = s.Mission;
            MissionTime = m.Time;
            SavedAt = MissionTime;
            foreach (var os in m.Objectives)
            {
                var o = Find(os.Id);
                if (o == null) continue;
                o.State = (ObjectiveState)os.State;
                o.Progress = os.Progress;
                o.Done.Clear();
                foreach (var d in os.Done) o.Done.Add(d);
                o.Discovered = !o.Hidden || m.Discovered.Contains(o.Id) || o.State != ObjectiveState.Active;
            }
            _fired.Clear(); foreach (var f in m.FiredRules) _fired.Add(f);
            _timers.Clear();
            foreach (var t in m.Timers)
            {
                if (t.Key == "#vitae0") continue;
                if (t.Key == "#dawn") { DawnLeft = t.Value; continue; }
                if (t.Key.StartsWith("#hud|"))
                {
                    var parts = t.Key.Split('|');
                    HudTimer = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : "hud";
                    HudLabel = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null;
                    continue;
                }
                if (t.Key == "#rain") { Weather.Set(t.Value > 0.5f); continue; }
                _timers[t.Key] = t.Value;
            }
            _flags.Clear(); foreach (var f in m.Flags) _flags.Add(f);
            foreach (var f in m.Flags) if (f.StartsWith("#swap|")) { var sp = f.Split('|'); if (sp.Length == 4) SwapProp(float.Parse(sp[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(sp[2], System.Globalization.CultureInfo.InvariantCulture), sp[3]); }
            Stats.Secrets.Clear(); Stats.Secrets.AddRange(m.Secrets);
            Stats.Kills = m.Kills; Stats.Sips = m.Sips; Stats.Drains = m.Drains; Stats.TimesSpotted = m.TimesSpotted;
            Stats.Alarms = m.Alarms; Stats.BodiesFound = m.BodiesFound; Stats.Disposed = m.Disposed;
            Stats.FedTypes.Clear(); Stats.FedTypes.AddRange(m.FedTypes);
            _loadsThisRun = m.Loads + 1;

            if (Game.Cam != null && s.CamDistance > 0f)
            {
                Game.Cam.SnapTo(Game.Player ? Game.Player.transform.position : s.CamPivot, s.CamYaw);
                Game.Cam.Distance = s.CamDistance;
                if (Game.Player) Game.Cam.FollowTarget(Game.Player.transform);
            }
        }

        public bool CanSave(out string why)
        {
            why = null;
            if (!Running || Ended) { why = "Not in a mission"; return false; }
            var p = Game.Player;
            if (p == null || p.Dead) { why = "Dead"; return false; }
            if (Game.Campaign != null && Game.Campaign.Ironblood) { why = "Ironblood: no saving"; return false; }
            if (Game.AI != null && WatchedBy(Game.AI.Npcs) != null) { why = "you are being watched"; return false; }
            return true;
        }

        /// <summary>Someone whose meter is filling on her this tick (QW8, P11): no save while she is seen, so a save is
        /// never made in a lost position. Being overlooked (the masque) doesn't count.</summary>
        public static Npc WatchedBy(IEnumerable<Npc> npcs)
        {
            foreach (var n in npcs)
                if (n && n.IsAlive && n.SeesPlayer && !n.Friendly && !n.IsThrall) return n;
            return null;
        }

        public bool SaveTo(string slot, string label)
        {
            if (!CanSave(out var why)) { Game.UI?.Toast("Cannot save: " + why); return false; }
            var s = Capture(label);
            bool ok = SaveSystem.WriteMission(s, slot);
            if (ok) { Stats.Saves++; SavedAt = MissionTime; }
            return ok;
        }

        public void QuickSave()
        {
            if (SaveTo(SaveSystem.NextQuickSlot(), "Quick save")) Game.UI?.Toast("Quick saved");
            else if (SaveSystem.LastError != null) Game.UI?.Toast("Save failed: " + SaveSystem.LastError);
        }

        public void QuickLoad()
        {
            var slot = SaveSystem.NewestQuickSlot();
            var s = slot != null ? SaveSystem.ReadMission(slot) : null;
            if (s == null) { Game.UI?.Toast("No quick save."); return; }
            LoadSave(s);
        }

        public void Autosave(string label, bool force)
        {
            if (!CanSave(out _)) return;
            if (!force && !CanAutosaveNow()) return;
            if (SaveTo(SaveSystem.NextAutoSlot(), label)) Game.UI?.SaveIndicator();
        }

        public void LoadSave(MissionSave s)
        {
            if (s == null) return;
            Game.UI?.HideModals();
            Begin(s.MissionId, s);
            Game.UI?.Toast("Loaded: " + SaveSystem.SlotName(s.Slot));
        }

        // ================================================================== script
        void Fire(string ev, string arg)
        {
            if (_applyingSave) return;
            for (int i = 0; i < _rules.Count; i++)
            {
                var r = _rules[i];
                if (r.Event != ev) continue;
                if (!string.IsNullOrEmpty(r.Arg) && r.Arg != "*" && !string.Equals(r.Arg, arg, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(r.Arg) && arg == "any") continue; // plain "on kill:" matches each kill once, not the duplicate "any"
                if (!r.Repeat && _fired.Contains(i)) continue;
                if (!ConditionsHold(r)) continue;
                _fired.Add(i);
                foreach (var a in r.Actions)
                {
                    try { Run(a, r); }
                    catch (System.Exception e) { Debug.LogError($"[Script] line {r.Line} '{string.Join(" ", a)}': {e.Message}"); }
                    if (Ended) return;
                }
            }
        }

        bool ConditionsHold(ScriptRule r) => ConditionsHold(r.Conditions, n => _flags.Contains(n) || IsObjectiveComplete(n));

        /// <summary>True when every condition holds; "!name" requires the name not to hold.</summary>
        public static bool ConditionsHold(List<string> conditions, System.Func<string, bool> holds)
        {
            foreach (var c in conditions)
            {
                bool neg = c.StartsWith("!");
                if (holds(neg ? c.Substring(1) : c) == neg) return false;
            }
            return true;
        }

        static string A(List<string> a, int i, string fallback = null) => a.Count > i ? a[i] : fallback;
        static float F(List<string> a, int i, float fallback) => a.Count > i && float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        void Run(List<string> a, ScriptRule rule)
        {
            if (a.Count == 0) return;
            var lvl = Game.Level;
            switch (a[0].ToLowerInvariant())
            {
                case "say": Game.UI?.Subtitle(A(a, 1, "Ilse"), A(a, 2, ""), F(a, 3, 0f)); break;
                case "bark":
                {
                    var n = lvl.Get<Npc>(A(a, 1));
                    if (n && n.IsAlive && n.gameObject.activeInHierarchy) GameEvents.RaiseBark(n.Id, A(a, 2, ""));
                    break;
                }
                case "toast": Game.UI?.Toast(A(a, 1, "")); break;
                case "hint": Game.UI?.Hint(A(a, 1, ""), A(a, 2, null)); break;
                case "document": Game.UI?.ShowDocument(A(a, 1, ""), A(a, 2, "")); break;
                case "reveal": case "objective": Reveal(A(a, 1)); break;
                case "complete": CompleteObjective(A(a, 1)); break;
                case "fail": FailObjective(A(a, 1), A(a, 2)); break;
                case "flag": _flags.Add(A(a, 1)); Fire("flag", A(a, 1)); break;
                case "unflag": _flags.Remove(A(a, 1)); break;
                case "weather": Weather.Set(A(a, 1, "rain") == "rain"); break;
                case "hunt": { var n = lvl.Get<Npc>(A(a, 1)); if (!n) break; if (A(a, 2, "on") == "now") n.HuntNow(); else n.Hunting = A(a, 2, "on") != "off"; break; }
                case "campaign_flag": Game.Campaign?.SetFlag(A(a, 1), A(a, 2, "on") != "off"); break;
                case "lore": Game.Campaign?.DiscoverLore(A(a, 1), A(a, 2, ""), A(a, 3, ""), Info.Id); Game.UI?.Toast("Journal: " + A(a, 2, "")); break;
                case "group": lvl.ActivateGroup(A(a, 1), true); break;
                case "ungroup": lvl.ActivateGroup(A(a, 1), false); break;
                case "spawn": { var e = lvl.Get(A(a, 1)); if (e) e.gameObject.SetActive(true); break; }
                case "remove": { var e = lvl.Get(A(a, 1)); if (e) e.gameObject.SetActive(false); break; }
                case "unlock": { var d = lvl.Get<Door>(A(a, 1)); if (d) d.SetLocked(false); break; }
                case "lock": { var d = lvl.Get<Door>(A(a, 1)); if (d) d.SetLocked(true); break; }
                case "route":
                {
                    var n = lvl.Get<Npc>(A(a, 1));
                    if (n && n.IsAlive && lvl.Data.Routes.TryGetValue(A(a, 2, ""), out var r)) n.SetRoute(r);
                    break;
                }
                case "unseal": { var u = lvl.Get<Interactable>(A(a, 1)); if (u) u.Sealed = false; break; }
                case "seal": { var u = lvl.Get<Interactable>(A(a, 1)); if (u) u.Sealed = true; break; }
                case "invite": { var d = lvl.Get<Door>(A(a, 1)); if (d) d.Invite(); break; }
                case "open": { var g = lvl.Get<Gate>(A(a, 1)); if (g) g.SetOpen(true, false); break; }
                case "close": { var g = lvl.Get<Gate>(A(a, 1)); if (g) g.SetOpen(false, false); break; }
                case "light":
                {
                    var l = lvl.Get<GameLight>(A(a, 1));
                    if (l) l.SetOn(A(a, 2, "on") != "off");
                    break;
                }
                case "lightgroup":
                {
                    bool on = A(a, 2, "on") != "off";
                    foreach (var l in Game.Lights.All.ToArray()) if (l && l.LightGroup == A(a, 1)) l.SetOn(on);
                    break;
                }
                case "alarm":
                {
                    int lv = (int)F(a, 1, 2);
                    if (lv >= 3) Game.AI.RaiseLockdown("script");
                    else GameEvents.RaiseAlarm(lv);
                    break;
                }
                case "lockdown": Game.AI.RaiseLockdown(A(a, 1, "script")); break;
                case "timer": _timers[A(a, 1, "t")] = F(a, 2, 10f); break;
                case "canceltimer": _timers.Remove(A(a, 1, "t")); break;
                case "dawn": DawnLeft = F(a, 1, 300f); break;
                case "countdown":
                    // countdown <timer> <seconds> ["Label"]: a script timer shown on the HUD chip
                    HudTimer = A(a, 1, "hud");
                    HudLabel = A(a, 3, null);
                    _timers[HudTimer] = F(a, 2, 60f);
                    break;
                case "crescendo": _timers["#hush"] = F(a, 1, 8f); Fire("crescendo", "any"); break;
                case "blast": Blast(Data.CellToWorld(F(a, 1, 0), F(a, 2, 0), lvl.SurfaceHeightCell(F(a, 1, 0), F(a, 2, 0))), F(a, 3, 12f), A(a, 4, null)); break;
                case "swapprop":
                {
                    // replace a static prop for good (a blown gas holder): remembered as a flag so loads rebuild it
                    float sx = F(a, 1, 0), sy = F(a, 2, 0);
                    var ty = A(a, 3, "crate");
                    if (SwapProp(sx, sy, ty)) _flags.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "#swap|{0}|{1}|{2}", sx, sy, ty));
                    break;
                }
                case "kill": { var n = lvl.Get<Npc>(A(a, 1)); if (n && n.IsAlive) n.Die(A(a, 2, "accident")); break; }
                case "rout":
                {
                    // rout <squad>: the squad breaks and runs (a scripted horror, a bell, a fire)
                    var q = lvl.Get<Squad>(A(a, 1));
                    if (q && !q.Broken) q.Break(true, q.Leader ? q.Leader.transform.position : q.transform.position);
                    break;
                }
                case "evacuate":
                    // evacuate [id...]: the named evac NPCs (all of them if none named) run for the exit
                    if (Game.AI == null) break;
                    foreach (var n in Game.AI.Npcs.ToArray())
                        if (n && n.EvacTo.HasValue && (a.Count < 2 || a.Contains(n.Id))) n.Evacuate();
                    break;
                case "wake": { var n = lvl.Get<Npc>(A(a, 1)); if (n && n.IsAlive && n.Asleep) n.WakeFromSleep(); break; }
                // investigate <npc> <x> <y>: he walks to look at a cell (the readability gym keeps one guard investigating)
                case "investigate":
                {
                    var n = lvl.Get<Npc>(A(a, 1));
                    if (n && n.IsAlive) n.EnterInvestigating(Data.CellToWorld(F(a, 2, 0), F(a, 3, 0), lvl.SurfaceHeightCell(F(a, 2, 0), F(a, 3, 0))), false, false);
                    break;
                }
                case "routechoice": ReadTestRunner.Instance?.OfferRoute(A(a, 1), A(a, 2), A(a, 3)); break;
                case "give": Game.Player?.AddItem(A(a, 1)); break;
                case "grant":
                {
                    var c = Game.Campaign; var node = Skills.Get(A(a, 1));
                    if (c == null || node == null || c.Has(node.Id)) break;
                    c.Grant(node.Id);
                    Game.UI?.Toast("New gift: " + node.Name);
                    Game.Audio?.Play2D("secret", 0.8f, 0.8f);
                    break;
                }
                case "vitae": Game.Campaign?.AddVitae((int)F(a, 1, 0)); break;
                case "mark": if (Game.Campaign != null) Game.Campaign.Marks += (int)F(a, 1, 1); break;
                case "terror": if (Game.Campaign != null) Game.Campaign.Terror += (int)F(a, 1, 1); break;
                case "rumour": if (Game.Campaign != null) Game.Campaign.Rumour += (int)F(a, 1, 1); break;
                case "tobias": if (Game.Campaign != null) Game.Campaign.Tobias = A(a, 1, "alive"); break;
                case "camera":
                {
                    var p = Data.CellToWorld(F(a, 1, 0), F(a, 2, 0), lvl.SurfaceHeightCell(F(a, 1, 0), F(a, 2, 0)));
                    Game.Cam?.FocusOn(p);
                    break;
                }
                case "ferry":
                {
                    // ferry x y [yaw]: a boat crossing. Fade, row across, fade back in.
                    var pl = Game.Player;
                    if (pl == null || pl.Dead) break;
                    var to = Data.CellToWorld(F(a, 1, 0), F(a, 2, 0), lvl.SurfaceHeightCell(F(a, 1, 0), F(a, 2, 0)));
                    float? yaw = a.Count > 3 ? F(a, 3, 0) : (float?)null;
                    Game.Audio?.Play2D("step_water0", 0.6f);
                    if (Game.UI == null) { pl.Teleport(to); break; }
                    Game.UI.FadeOut(0.35f, () =>
                    {
                        if (pl && !pl.Dead) pl.Teleport(to);
                        Game.Cam?.SnapTo(to, yaw);
                        Game.UI.FadeIn(0.6f);
                    });
                    break;
                }
                case "music": Game.Audio?.Play2D(A(a, 1, "sting"), F(a, 2, 0.8f)); break;
                case "sound": Game.Audio?.Play2D(A(a, 1, "bell"), F(a, 2, 0.8f)); break;
                case "checkpoint": StartCoroutineSafe(AutosaveNextFrame(A(a, 1, "Checkpoint"))); break;
                case "choice":
                {
                    // choice <id> "prompt" key "text" key "text" ...  → flag, campaign flag and `choice` event "<id>.<key>"
                    var id = A(a, 1, "choice");
                    var prompt = A(a, 2, "");
                    var opts = new List<KeyValuePair<string, string>>();
                    for (int i = 3; i + 1 < a.Count; i += 2) opts.Add(new KeyValuePair<string, string>(a[i], a[i + 1]));
                    if (Game.UI == null || opts.Count == 0) break;
                    Game.UI.ShowChoice(prompt, opts, key =>
                    {
                        Game.Campaign?.SetFlag(id + "." + key);
                        _flags.Add(id + "." + key);
                        Fire("choice", id + "." + key);
                    });
                    break;
                }
                case "win": Win(); break;
                case "lose": Lose(A(a, 1, "The mission failed.")); break;
                default: Debug.LogWarning($"[Script] line {rule.Line}: unknown action '{a[0]}'"); break;
            }
        }

        public bool HasFlag(string f) => _flags.Contains(f);

        /// <summary>Swap the static prop standing on a cell for another type (keeping its yaw).</summary>
        public bool SwapProp(float x, float y, string type)
        {
            var lvl = Game.Level;
            if (lvl == null) return false;
            var at = Data.CellToWorld(x, y, lvl.SurfaceHeightCell(x, y));
            foreach (Transform t in lvl.EntityRoot)
            {
                if (!t.name.StartsWith("prop_") || Util.FlatDistance(t.position, at) > 1.2f) continue;
                if (t.name == "prop_" + type) return true;
                var yaw = t.eulerAngles.y;
                Destroy(t.gameObject);
                Visual.PropFactory.Build(type, lvl.EntityRoot, t.position, yaw, Mathf.RoundToInt(x * 131 + y));
                return true;
            }
            Debug.LogWarning($"[Mission] swapprop: no prop at {x},{y}");
            return false;
        }
    }
}
