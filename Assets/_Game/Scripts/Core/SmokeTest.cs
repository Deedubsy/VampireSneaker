using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Vespertine.Data;
using Vespertine.Save;

namespace Vespertine.Core
{
    /// <summary>
    /// Player smoke test (P5): launch the built game with <c>-smoke [-smokeout file]</c> and it starts a throwaway
    /// campaign, loads every mission in turn, lets each run for a few seconds, then writes a report and quits with
    /// exit code 0 (clean) or 1 (errors, missing player, or a mission that never started). Saves go to a scratch
    /// folder next to the exe (smoke_saves), so a smoke run never touches the player's profiles. Add <c>-uncapped</c> to turn off
    /// vsync and the frame cap, so the frame times measure the game rather than the monitor. Each mission is sampled
    /// twice: calm (as it opens) and hunted (lockdown, every NPC sent searching at Ilse, who is in god mode), the
    /// worst case for perception, pathing and lights.
    /// </summary>
    public class SmokeTest : MonoBehaviour
    {
        const float StartTimeout = 30f, Settle = 4f, Sample = 4f;

        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-smoke") >= 0;

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        readonly List<string> _errors = new List<string>();
        readonly HashSet<string> _warnings = new HashSet<string>();
        readonly StringBuilder _report = new StringBuilder();
        string _mission = "boot";

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (_errors.Count < 40) _errors.Add($"[{_mission}] {type}: {msg}\n{FirstLines(stack, 4)}");
            }
            else if (type == LogType.Warning && _warnings.Count < 40) _warnings.Add($"[{_mission}] {msg}");
        }

        static string FirstLines(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var lines = s.Split('\n');
            return string.Join("\n", lines, 0, Mathf.Min(n, lines.Length));
        }

        static IEnumerator Measure(List<float> ms)
        {
            float s = 0f;
            while (s < Sample)
            {
                yield return null;
                s += Time.unscaledDeltaTime;
                ms.Add(Time.unscaledDeltaTime * 1000f);
            }
            ms.Sort();
        }

        static string Stats(List<float> ms)
        {
            if (ms.Count == 0) return "(not measured)";
            float total = 0f;
            foreach (var x in ms) total += x;
            int n = ms.Count;
            return $"fps={n * 1000f / total:0} median={ms[n / 2]:0.0}ms p95={ms[n * 95 / 100]:0.0}ms worst={ms[n - 1]:0.0}ms";
        }

        IEnumerator Start()
        {
            // next to the exe (inside the project for dev builds), never in the player's own save folder
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "smoke_saves");
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            SaveSystem.RootOverride = dir;
            bool uncapped = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-uncapped") >= 0;
            _report.AppendLine($"Vespertine smoke {Application.version} {SystemInfo.graphicsDeviceName} {Screen.width}x{Screen.height}");

            yield return new WaitForSecondsRealtime(2f);
            Game.Root.NewGame(0, Difficulty.Hunter, false, false);
            if (uncapped) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            yield return new WaitForSecondsRealtime(2f);

            int failed = 0;
            foreach (var m in Missions.All)
            {
                _mission = m.Id;
                int errs0 = _errors.Count;
                Game.Root.StartMission(m.Id);
                float t = 0f;
                // the old mission may still be running for a frame or two while the transition fades out
                yield return new WaitForSecondsRealtime(1f);
                while (!(Game.InMission && Game.Mission.Info != null && Game.Mission.Info.Id == m.Id) && t < StartTimeout)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                bool started = t < StartTimeout;
                yield return new WaitForSecondsRealtime(Settle);
                Game.UI?.PopScreen("intro");

                var calm = new List<float>();
                yield return Measure(calm);
                int npcs = Game.AI != null ? Game.AI.Npcs.Count : 0;

                var hunted = new List<float>();
                if (Game.InMission && Game.AI != null && Game.Player != null)
                {
                    Player.Vampire.GodMode = true;
                    Game.AI.RaiseLockdown("smoke");
                    var at = Game.Player.transform.position;
                    foreach (var n in Game.AI.Npcs.ToArray())
                        if (n && n.IsAlive && !n.IsThrall && !n.Friendly) n.EnterSearching(at + new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f)), true);
                    yield return new WaitForSecondsRealtime(1f);
                    yield return Measure(hunted);
                    Player.Vampire.GodMode = false;
                }
                int npcsHunted = Game.AI != null ? Game.AI.Npcs.Count : 0;

                bool ok = started && Game.Player != null && npcs > 0 && _errors.Count == errs0;
                if (!ok) failed++;
                _report.AppendLine($"{m.Id} {(ok ? "ok" : "FAIL")} started={started} player={Game.Player != null} errors={_errors.Count - errs0}");
                _report.AppendLine($"    calm   npcs={npcs,-3} {Stats(calm)}");
                _report.AppendLine($"    hunted npcs={npcsHunted,-3} {Stats(hunted)}");
            }

            _mission = "end";
            _report.AppendLine(failed == 0 && _errors.Count == 0 ? "RESULT PASS" : $"RESULT FAIL missions={failed} errors={_errors.Count}");
            if (_errors.Count > 0) { _report.AppendLine("-- errors"); foreach (var e in _errors) _report.AppendLine(e); }
            if (_warnings.Count > 0) { _report.AppendLine("-- warnings"); foreach (var w in _warnings) _report.AppendLine(w); }

            var outPath = Arg("-smokeout") ?? Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "smoke.txt");
            try { File.WriteAllText(outPath, _report.ToString()); } catch (System.Exception e) { Debug.Log("[Smoke] cannot write " + outPath + ": " + e.Message); }
            Debug.Log("[Smoke]\n" + _report);
            Application.Quit(failed == 0 && _errors.Count == 0 ? 0 : 1);
        }
    }
}
