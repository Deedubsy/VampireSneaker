using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Mission;
using Vespertine.Progression;
using Vespertine.Save;
using Vespertine.UI;

namespace Vespertine.Core
{
    /// <summary>
    /// Development check (item 6): plays a throwaway campaign from the first night to an ending through the real flow
    /// (mission start, script rules, the debrief's Continue, blood-dreams, the ending). It does not sneak: each night's
    /// objectives are driven one at a time in the order the mission reveals them. Interactions are raised as events,
    /// reach and escape areas are entered by teleporting Ilse into them, and everything else is completed directly.
    /// Scripted choices take the first preferred key offered, else the first option.
    /// Start with <c>DevCampaignRun.Run("free")</c> from an editor eval; the report is in <see cref="Report"/>.
    /// Saves go to a scratch folder, so the run never touches a real profile.
    /// </summary>
    public class DevCampaignRun : MonoBehaviour
    {
        public static string Report = "";
        public static bool Running;
        const float StartTimeout = 30f, NightTimeout = 150f, HiddenGrace = 25f;

        string[] _prefer;
        bool _optionals;
        readonly StringBuilder _out = new StringBuilder();
        readonly List<string> _errors = new List<string>();
        readonly List<string> _picks = new List<string>();
        string _night = "boot";
        int _failed;

        /// <summary><paramref name="prefer"/>: comma-separated choice keys to take when offered (e.g. "free" or
        /// "destroy,spare"). <paramref name="optionals"/>: also complete every optional objective that is revealed.</summary>
        public static void Run(string prefer = "", bool optionals = true)
        {
            if (Running) return;
            var r = new GameObject("DevCampaignRun").AddComponent<DevCampaignRun>();
            r._prefer = string.IsNullOrEmpty(prefer) ? new string[0] : prefer.Split(',');
            r._optionals = optionals;
            DontDestroyOnLoad(r.gameObject);
        }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && _errors.Count < 60)
                _errors.Add($"[{_night}] {type}: {msg.Split('\n')[0]} @ {FirstFrame(stack)}");
        }

        static string FirstFrame(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return "";
            foreach (var l in stack.Split('\n'))
                if (l.Contains("Vespertine.")) return l.Trim();
            return stack.Split('\n')[0].Trim();
        }

        string Pick(List<KeyValuePair<string, string>> opts)
        {
            string key = opts[0].Key;
            foreach (var p in _prefer)
                if (opts.Exists(o => o.Key == p)) { key = p; break; }
            var all = new List<string>();
            foreach (var o in opts) all.Add(o.Key);
            _picks.Add($"{_night}: {key} of [{string.Join("/", all)}]");
            return key;
        }

        void Start() { StartCoroutine(Go()); }

        IEnumerator Go()
        {
            Running = true;
            Report = "running";
            var dir = Path.Combine(SaveSystem.BaseDir, "campaignrun");
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            var oldRoot = SaveSystem.RootOverride;
            int oldProfile = SaveSystem.Profile;
            SaveSystem.RootOverride = dir;
            UIManager.AutoPick = Pick;
            _out.AppendLine($"Campaign run: prefer=[{string.Join(",", _prefer)}] optionals={_optionals}");

            Game.Root.NewGame(0, Difficulty.Hunter, false, false);
            yield return new WaitForSecondsRealtime(1.5f);

            foreach (var m in Missions.All)
            {
                _night = m.Id;
                var c = Game.Campaign;
                var flags0 = new HashSet<string>(c.Flags);
                int vitae0 = c.Vitae, errs0 = _errors.Count, cm0 = c.Countermeasures.Count;
                float t0 = Time.realtimeSinceStartup;
                Game.Root.StartMission(m.Id);
                yield return new WaitForSecondsRealtime(1f);
                float wait = 0f;
                while (!(Game.InMission && Game.Mission.Running && Game.Mission.Info != null && Game.Mission.Info.Id == m.Id) && wait < StartTimeout)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (wait >= StartTimeout) { Line($"{m.Id} FAIL: never started"); _failed++; break; }
                yield return new WaitForSecondsRealtime(1f);
                Game.UI.PopScreen("intro");
                Player.Vampire.GodMode = Player.Vampire.NoTarget = true;

                var mc = Game.Mission;
                _raised.Clear();
                float t = 0f;
                var forced = new List<string>();
                while (!mc.Ended && t < NightTimeout)
                {
                    Game.UI.ClearSubtitles();
                    var note = Step(mc, t);
                    if (note != null) forced.Add(note);
                    yield return new WaitForSecondsRealtime(0.5f);
                    t += 0.5f;
                }
                // let the debrief land
                float w2 = 0f;
                while (mc.Ended && mc.Won && mc.Result == null && w2 < 5f) { w2 += 0.1f; yield return new WaitForSecondsRealtime(0.1f); }
                Player.Vampire.GodMode = Player.Vampire.NoTarget = false;

                if (!mc.Ended || !mc.Won)
                {
                    _failed++;
                    var open = new List<string>();
                    foreach (var o in mc.Objectives) if (o.Active) open.Add($"{o.Id}({o.Type}{(o.Primary ? "" : ",opt")}{(o.Discovered ? "" : ",hidden")})");
                    Line($"{m.Id} FAIL: {(mc.Ended ? "lost: " + mc.DeathCause : "stalled")}; open: {string.Join(" ", open)}; steps: {string.Join(" ", forced)}");
                    break;
                }

                var r = mc.Result;
                var added = new List<string>();
                foreach (var f in c.Flags) if (!flags0.Contains(f)) added.Add(f);
                Line($"{m.Id} won in {Time.realtimeSinceStartup - t0:0}s: Vitae {vitae0}->{c.Vitae} (Awakening {CampaignState.AwakeningFor(c.Vitae)}), Marks {c.Marks}, " +
                     $"countermeasures +{c.Countermeasures.Count - cm0}, errors {_errors.Count - errs0}, index {c.MissionIndex}");
                Line($"    steps: {string.Join(" ", forced)}");
                if (added.Count > 0) Line($"    flags: {string.Join(" ", added)}");

                Game.Root.AfterDebrief(r);
                yield return new WaitForSecondsRealtime(1.5f);
                if (Game.UI.HasScreen("dream")) Line("    blood-dream shown");
                if (Game.Root.State == RootState.Ending) Line($"    ENDING: {Game.Root.EndingId()} (Terror {c.Terror}, Rumour {c.Rumour}, Tobias {c.Tobias})");
            }

            if (_failed == 0 && Game.Root.State != RootState.Ending) { Line("FAIL: the last night did not lead to an ending"); _failed++; }
            if (_picks.Count > 0) { Line("-- choices"); foreach (var p in _picks) Line("    " + p); }
            if (_errors.Count > 0) { Line("-- errors"); foreach (var e in _errors) Line("    " + e); }
            Line(_failed == 0 && _errors.Count == 0 ? "RESULT PASS" : $"RESULT FAIL nights={_failed} errors={_errors.Count}");

            UIManager.AutoPick = null;
            // leave nothing behind that a later save would write into a real profile: the throwaway campaign stays in the scratch folder
            Game.Root.GoToMainMenu();
            Game.Campaign = null;
            SaveSystem.RootOverride = oldRoot;
            SaveSystem.Profile = oldProfile;
            Game.UI.ShowMainMenu(); // again, now over the real profiles
            Report = _out.ToString();
            Debug.Log("[CampaignRun]\n" + Report);
            Running = false;
            Destroy(gameObject);
        }

        void Line(string s) => _out.AppendLine(s);

        /// <summary>Drives one objective. Returns a short note of what was done, or null.</summary>
        string Step(MissionController mc, float t)
        {
            // as in the game (OtherPrimariesComplete), an escape waits for every other primary, hidden ones too (M01's wheel)
            bool othersDone = true;
            foreach (var o in mc.Objectives)
                if (o.Active && o.Primary && !o.IsConduct && o.Type != "escape") othersDone = false;

            // revealed optionals first (the last primary ends the night), then primaries in the mission's order
            Objective pick = null;
            foreach (var o in mc.Objectives)
            {
                if (!o.Active || o.IsConduct || _raised.Contains(o.Id + "!")) continue;
                if (!o.Primary && !_optionals) continue;
                if (!o.Visible && t < HiddenGrace) continue;
                if (o.Type == "escape" && !othersDone) continue;
                if (!o.Primary) { pick = o; break; }
                if (pick == null) pick = o;
            }
            if (pick == null) return null;
            var tag = (pick.Primary ? "" : "+") + (pick.Visible ? "" : "?") + pick.Id;

            switch (pick.Type)
            {
                case "interact":
                case "interact_all":
                case "item":
                    if (pick.Spec.Args.Exists(Sealed)) return null; // not yet: the mission unseals it
                    if (!_raised.Contains(pick.Id))
                    {
                        _raised.Add(pick.Id);
                        foreach (var a in pick.Spec.Args)
                            if (pick.Type == "interact_all" || a == pick.Spec.Args[0]) Use(a);
                        Game.UI.HideModals(); // the documents a use opens
                        return tag + ":use";
                    }
                    break;
                case "escape":
                case "reach":
                    if (!_raised.Contains(pick.Id) && pick.TryRect(out var rect))
                    {
                        _raised.Add(pick.Id);
                        Enter(mc, rect);
                        return tag + ":enter";
                    }
                    break;
            }
            mc.CompleteObjective(pick.Id);
            if (!pick.Complete && !pick.Primary) _raised.Add(pick.Id + "!"); // an optional that cannot complete yet (needs=): leave it
            return tag + (pick.Complete ? "" : ":held");
        }

        readonly HashSet<string> _raised = new HashSet<string>();

        /// <summary>Uses the entity the way Ilse would (its own effects: secrets, documents, levers), else raises the bare event.</summary>
        static void Use(string id)
        {
            var it = Game.Level != null ? Game.Level.Get<Interactable>(id) : null;
            if (it != null && it.Enabled) it.Use(true);
            else GameEvents.RaiseInteracted(id);
        }

        static bool Sealed(string id)
        {
            var it = Game.Level != null ? Game.Level.Get<Interactable>(id) : null;
            return it != null && it.Sealed;
        }

        static void Enter(MissionController mc, Rect r)
        {
            var p = Game.Player;
            if (p == null) return;
            var at = mc.Data.CellToWorld(r.center.x, r.center.y);
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            // the area may be on a roof: take the nearest walkable point at any height inside it
            for (float h = 0f; h <= 24f; h += 3f)
                if (NavMesh.SamplePosition(at + Vector3.up * h, out var hit, 2.5f, filter) && r.Contains(mc.Data.WorldToCell(hit.position)))
                {
                    p.Teleport(hit.position);
                    return;
                }
            p.Teleport(at);
        }
    }
}
