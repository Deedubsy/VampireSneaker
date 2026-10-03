using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Vespertine.AI;
using Vespertine.Level;
using Vespertine.Save;
using Vespertine.Stealth;
using Vespertine.Visual;
using Q = Vespertine.Stealth.ReadTest.Q;
using Cause = Vespertine.Stealth.ReadTest.Cause;

namespace Vespertine.Core
{
    /// <summary>
    /// Runs the WASD Stealth Readability Test (§44.1) while Settings → Gameplay → <i>Readability test mode</i> is on.
    /// <list type="bullet">
    /// <item>Records free play: play time, Alt time, pause time, pad share, entries into a guard's detecting region (and
    /// whether his cone had been shown for 1.5 s), stops of 2 s or more near a guard, every detection and its cause.</item>
    /// <item>Twelve freeze probes per map: the world stops for 2 s with the overlays as they were, the question showing
    /// and its subject marked; then the screen goes dark and the player answers (Y/N, a point, or a traced route).
    /// The truth is taken from the rules that judge her, at the frozen frame.</item>
    /// <item>After each real detection, Q7: what caused it (or "I don't know why").</item>
    /// <item>Route choices offered by the mission script (<c>routechoice id safeZone otherZone</c>).</item>
    /// </list>
    /// Each map's log is saved as JSON under <c>readability/</c> in the save folder when the mission ends, and
    /// <c>report.txt</c> beside it holds the verdict over every log there.
    /// </summary>
    public class ReadTestRunner : MonoBehaviour
    {
        public static ReadTestRunner Instance { get; private set; }
        const float PadCursor = 700f, MarkSize = 16f, SpottedDelay = 0.6f;

        ReadTest.Log _log;
        readonly ReadTest.StopClock _stops = new ReadTest.StopClock();
        readonly Dictionary<Npc, float> _shown = new Dictionary<Npc, float>();
        readonly HashSet<Npc> _inSight = new HashSet<Npc>();
        readonly List<(string Id, string Safe, string Other)> _offers = new List<(string, string, string)>();
        int _asked, _turn;
        float _retryAt, _lastProbe, _lastCause;
        bool _lastPad;
        Vector3 _lastFeet;
        bool _haveFeet;
        string _lastTarget;
        float _spottedAt = -1f;
        Npc _spottedBy;
        Cause _spottedCause;

        enum Phase { Off, Freeze, Ask }
        Phase _phase;
        Q _q;
        float _phaseAt;
        // the frozen frame's truth
        string _target, _truthText;
        bool _truthYes;
        Vector3 _anchor, _her, _mark;
        float _plane;
        Func<Vector3, float> _error;
        Func<Vector3, bool> _safe;
        readonly List<Vector3> _trace = new List<Vector3>();
        bool _tracing;
        Vector2 _cursor;
        int _choice;

        // the screen
        VisualElement _scr, _shade, _marks, _cursorEl;
        Label _title, _help;
        readonly List<VisualElement> _traceEls = new List<VisualElement>();

        static readonly Cause[] Causes = { Cause.Near, Cause.Light, Cause.Touch, Cause.Noise, Cause.Searchlight, Cause.Smell, Cause.Unknown };
        static readonly string[] CauseText =
        {
            "1  Close: the near band (darkness doesn't hide you there)", "2  Light: you were lit in his far band",
            "3  Touch: right beside or behind him", "4  A noise", "5  A searchlight", "6  Smell", "0  I don't know why",
        };

        public static bool On => Game.Settings != null && Game.Settings.ReadabilityTest;
        /// <summary>A probe is up (dev checks).</summary>
        public bool Asking => _phase != Phase.Off;
        public Q Question => _q;
        public ReadTest.Log Current => _log;

        void Awake() => Instance = this;
        void OnEnable() => GameEvents.SpottedCaption += OnSpotted;
        void OnDisable() => GameEvents.SpottedCaption -= OnSpotted;

        void OnApplicationQuit() { if (_log != null) Close(); }

        /// <summary>The folder logs go to (inside the dev sandbox when it is on).</summary>
        public static string Folder => Path.Combine(SaveSystem.BaseDir, "readability");

        /// <summary>The verdict over every log in <see cref="Folder"/>.</summary>
        public static string ReportAll()
        {
            var logs = new List<ReadTest.Log>();
            if (Directory.Exists(Folder))
                foreach (var f in Directory.GetFiles(Folder, "*.json"))
                {
                    try { var l = JsonUtility.FromJson<ReadTest.Log>(File.ReadAllText(f)); if (l != null) logs.Add(l); }
                    catch (Exception e) { Debug.LogWarning($"[ReadTest] {f}: {e.Message}"); }
                }
            return ReadTest.Report(logs);
        }

        /// <summary>The mission script's <c>routechoice</c>: the first of the two zones she enters is her choice.</summary>
        public void OfferRoute(string id, string safeZone, string otherZone)
        {
            // kept even before a log opens: the script's "on start" runs before the first Update
            if (string.IsNullOrEmpty(safeZone) || string.IsNullOrEmpty(otherZone)) return;
            _offers.RemoveAll(o => o.Id == id);
            _offers.Add((id, safeZone, otherZone));
        }

        void OnSpotted(Npc n)
        {
            if (!On || _log == null || n == null) return;
            var cause = ReadTest.CauseOf(n.LastCause);
            _log.Detections.Add(new ReadTest.Detection { At = _log.Play, Guard = n.Id, Caption = DetectionMath.Explain(n.LastCause), Cause = cause.ToString() });
            // every detection is logged; the question comes at most once per MinGap of play (an alarm spots her over and over)
            if (_spottedAt >= 0f || _log.Play < _lastCause + ReadTest.MinGap) return;
            _spottedAt = Time.unscaledTime; _spottedBy = n; _spottedCause = cause; _lastCause = _log.Play;
        }

        void Update()
        {
            if (!On || !Game.InMission || Game.Mission.Ended)
            {
                if (!Game.InMission) _offers.Clear();
                if (_phase != Phase.Off) EndProbe();
                if (_log != null) Close();
                return;
            }
            if (_log == null) Open();
            if (_phase != Phase.Off) { TickProbe(); return; }
            Record();
            if (Game.AnyPause || Game.UI != null && Game.UI.BlocksGameplay || Game.Player == null || Game.Player.Dead) return;
            if (_spottedAt >= 0f && Time.unscaledTime >= _spottedAt + SpottedDelay) { _spottedAt = -1f; AskCause(); return; }
            if (ReadTest.ProbeDue(_log.Play, _asked, _lastProbe) && Time.unscaledTime >= _retryAt)
            {
                var q = ReadTest.Next(ref _turn, Prepare);
                if (q == Q.None) _retryAt = Time.unscaledTime + ReadTest.RetryEvery;
                else { _asked++; _lastProbe = _log.Play; StartProbe(q); }
            }
        }

        // ------------------------------------------------------------------ the log

        void Open()
        {
            _log = new ReadTest.Log { Mission = Game.Mission.Info != null ? Game.Mission.Info.Id : "?", Started = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            _asked = 0; _turn = 0; _retryAt = 0f; _lastProbe = _lastCause = float.NegativeInfinity; _spottedAt = -1f; _haveFeet = false;
            _shown.Clear(); _inSight.Clear(); _stops.Flush();
        }

        void Close()
        {
            float s = _stops.Flush();
            if (s > 0f) { _log.Stops++; _log.StopTime += s; }
            try
            {
                Directory.CreateDirectory(Folder);
                var file = Path.Combine(Folder, $"{_log.Mission}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(file, JsonUtility.ToJson(_log, true));
                File.WriteAllText(Path.Combine(Folder, "report.txt"), ReportAll());
                Debug.Log($"[ReadTest] saved {file}");
            }
            catch (Exception e) { Debug.LogWarning($"[ReadTest] could not save: {e.Message}"); }
            _log = null;
            _offers.Clear();
        }

        void Record()
        {
            var p = Game.Player;
            if (p == null) return;
            float dt = Time.deltaTime;
            if (Game.TacticalPaused) _log.Paused += Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            _log.Play += dt;
            var cones = ConeRenderer.Instance;
            if (cones != null && cones.AllShown) _log.Alt += dt;
            UpdateDevice();
            if (_lastPad) _log.Pad += dt;

            var feet = p.Feet;
            float speed = _haveFeet ? Util.FlatDistance(feet, _lastFeet) / dt : 0f;
            _lastFeet = feet; _haveFeet = true;
            bool near = false;
            foreach (var n in Game.AI.Npcs)
            {
                if (!n || !n.IsAlive || n.Friendly || !n.CanSee) { if (n) { _shown.Remove(n); _inSight.Remove(n); } continue; }
                if (Util.FlatDistance(n.transform.position, feet) <= ReadTest.StopNear) near = true;
                bool shown = cones != null && cones.Showing(n) != ConeContext.Show.None;
                _shown[n] = shown ? (_shown.TryGetValue(n, out var t) ? t : 0f) + dt : 0f;
                if (n.InSight && _inSight.Add(n))
                {
                    _log.Entries++;
                    if (_shown[n] - dt >= ReadTest.Timely) _log.Timely++;
                }
                else if (!n.InSight) _inSight.Remove(n);
            }
            float stop = _stops.Tick(dt, speed, near);
            if (stop > 0f) { _log.Stops++; _log.StopTime += stop; }

            for (int i = _offers.Count - 1; i >= 0; i--)
            {
                var o = _offers[i];
                var safe = Game.Level != null ? Game.Level.Get<Zone>(o.Safe) : null;
                var other = Game.Level != null ? Game.Level.Get<Zone>(o.Other) : null;
                bool inSafe = safe && safe.Contains(feet), inOther = other && other.Contains(feet);
                if (!inSafe && !inOther) continue;
                _log.Routes.Add(new ReadTest.Route { Id = o.Id, Chose = inSafe ? o.Safe : o.Other, Safe = inSafe });
                _offers.RemoveAt(i);
            }
        }

        void UpdateDevice() => _lastPad = PadInput(_lastPad);

        /// <summary>Whether the pad is the device in use: true while any pad input is held, false on keyboard or mouse
        /// input, else <paramref name="last"/>. Shared with the Movement Test.</summary>
        public static bool PadInput(bool last)
        {
            var g = Gamepad.current;
            if (g != null && (g.leftStick.ReadValue().sqrMagnitude > 0.04f || g.rightStick.ReadValue().sqrMagnitude > 0.04f
                              || g.buttonSouth.isPressed || g.buttonEast.isPressed || g.buttonWest.isPressed || g.buttonNorth.isPressed
                              || g.leftTrigger.isPressed || g.rightTrigger.isPressed || g.leftShoulder.isPressed || g.rightShoulder.isPressed))
                return true;
            else if (Keyboard.current != null && Keyboard.current.anyKey.isPressed
                     || Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.delta.ReadValue().sqrMagnitude > 4f))
                return false;
            return last;
        }

        // ------------------------------------------------------------------ the probes' truth

        bool OnScreen(Vector3 w)
        {
            var cam = Game.Cam != null ? Game.Cam.Cam : null;
            if (!cam) return false;
            var v = cam.WorldToViewportPoint(w);
            return v.z > 0f && v.x > 0.05f && v.x < 0.95f && v.y > 0.08f && v.y < 0.9f;
        }

        /// <summary>Sets up question <paramref name="q"/> at this frame if it has a subject; false if it doesn't.</summary>
        bool Prepare(Q q)
        {
            var p = Game.Player;
            if (p == null || Game.AI == null || Game.Lights == null) return false;
            var feet = p.Feet;
            _her = feet; _plane = feet.y; _error = null; _safe = null; _truthText = null;
            switch (q)
            {
                case Q.Q1:
                {
                    var n = PickGuard(18f, null);
                    if (!n) return false;
                    var band = n.SeenBand(feet, p.SightLight);
                    _truthYes = band != DetectionMath.Band.None && !n.Overlooks(p, feet, Util.FlatDistance(n.transform.position, feet));
                    _truthText = _truthYes ? $"yes ({band})" : "no";
                    Subject(n.Id, n.transform.position);
                    return true;
                }
                case Q.Q2:
                {
                    var n = PickGuard(25f, x => ConeRenderer.Instance != null && ConeRenderer.Instance.Showing(x) == ConeContext.Show.Full);
                    if (!n) return false;
                    var v = n.Vision;
                    var apex = n.transform.position; var fwd = n.Forward;
                    _error = c => ReadTest.ArcError(apex, fwd, v.HalfAngle, off => WallReach(apex, fwd, off, v.FarRange), c);
                    _truthText = $"far range {v.FarRange:0.0} m, wall-clipped";
                    Subject(n.Id, apex); _plane = apex.y;
                    return true;
                }
                case Q.Q3:
                {
                    var n = PickGuard(15f, null);
                    if (!n) return false;
                    var v = n.Vision;
                    var apex = n.transform.position; var fwd = n.Forward;
                    _error = c => ReadTest.NearError(apex, fwd, v.HalfAngle, off => WallReach(apex, fwd, off, v.NearRange), v.Peripheral, c);
                    _truthText = $"near {v.NearRange:0.0} m, touch {v.Peripheral:0.0} m";
                    Subject(n.Id, apex); _plane = apex.y;
                    return true;
                }
                case Q.Q4:
                {
                    GameLight best = null; float bestD = float.MaxValue;
                    foreach (var l in Game.Lights.All)
                    {
                        if (!l || !l.On || l.Portable || l.Kind == LightKind.Searchlight || l.Kind == LightKind.Sunbeam || l.GetComponent<FxLife>()) continue;   // not flares
                        float d = Util.FlatDistance(l.transform.position, feet);
                        if (d > 15f || Game.Lights.ExposureRadius(l) < 1f || !OnScreen(l.transform.position)) continue;
                        if (l.Id == _lastTarget) d += 6f;
                        if (d < bestD) { best = l; bestD = d; }
                    }
                    if (!best) return false;
                    var lamp = best.transform.position;
                    _error = c => ReadTest.ContourError(lamp, c, w => Game.Lights.LightAt(w));
                    _truthText = $"contour {ReadTest.ContourError(lamp, lamp, w => Game.Lights.LightAt(new Vector3(w.x, feet.y, w.z))):0.0} m along the line";
                    Subject(best.Id, lamp);
                    return true;
                }
                case Q.Q6:
                {
                    var dir = p.MoveIntent; dir.y = 0f;
                    if (dir.sqrMagnitude < 0.04f || p.Feeding != null) return false;
                    var next = feet + dir.normalized * StepRead.Reach(p.IntentSpeed);
                    if (UnityEngine.AI.NavMesh.Raycast(feet, next, out var hit, UnityEngine.AI.NavMesh.AllAreas)) next = hit.position;
                    next.y = feet.y;
                    if (Util.FlatDistance(next, feet) < 0.2f) return false;
                    _truthYes = !Safe(next, out var why);
                    _truthText = _truthYes ? "yes: " + why : "no";
                    Subject("step", next);
                    return true;
                }
                case Q.Q5:
                {
                    if (!Safe(feet, out _)) return false;
                    if (!FindRoute(feet, out var mark)) return false;
                    _mark = mark;
                    _safe = Corridor;
                    _truthText = "a dark route exists";
                    Subject("route", mark);
                    return true;
                }
            }
            return false;
        }

        void Subject(string id, Vector3 at) { _target = id; _anchor = at; _lastTarget = id; }

        Npc PickGuard(float within, Func<Npc, bool> also)
        {
            var feet = Game.Player.Feet;
            Npc best = null; float bestD = float.MaxValue;
            foreach (var n in Game.AI.Npcs)
            {
                if (!n || !n.IsAlive || n.Friendly || !n.CanSee || !n.gameObject.activeInHierarchy) continue;
                float d = Util.FlatDistance(n.transform.position, feet);
                if (d > within || !OnScreen(n.transform.position) || also != null && !also(n)) continue;
                if (n.Id == _lastTarget) d += 6f;   // vary the subject
                if (d < bestD) { best = n; bestD = d; }
            }
            return best;
        }

        /// <summary>A guard's sight's reach from <paramref name="apex"/> along a ray <paramref name="off"/> degrees off
        /// <paramref name="fwd"/>, as frozen at the probe: walls stop it.</summary>
        static float WallReach(Vector3 apex, Vector3 fwd, float off, float range)
        {
            fwd.y = 0f; fwd.Normalize();
            var dir = Quaternion.Euler(0f, off, 0f) * fwd;
            return Physics.Raycast(apex + Vector3.up * 1.1f, dir, out var hit, range, Layers.VisionBlockMask, QueryTriggerInteraction.Ignore) ? hit.distance : range;
        }

        /// <summary>Would she be unexposed standing at <paramref name="at"/>: below the light threshold and in no
        /// guard's band with his eye on her?</summary>
        static bool Safe(Vector3 at, out string why)
        {
            var p = Game.Player;
            bool leaves = Game.Level != null && Game.Level.InFoliage(at);
            float light = Player.Vampire.JudgedLight(Game.Lights.LightAt(at) * (leaves ? 0.6f : 1f), leaves);
            if (light >= DetectionMath.ExposedAt) { why = $"lit ({light:0.00})"; return false; }
            foreach (var n in Game.AI.Npcs)
            {
                if (!n || !n.IsAlive || n.Friendly || !n.CanSee) continue;
                if (Util.FlatDistance(n.transform.position, at) > n.Vision.FarRange + 1f) continue;
                var b = n.SeenBand(at, light);
                if (b != DetectionMath.Band.None && !n.Overlooks(p, at, Util.FlatDistance(n.transform.position, at))) { why = $"{n.Id}'s {b} band"; return false; }
            }
            why = null;
            return true;
        }

        /// <summary>Q5's rule: the dark corridor is at least 0.5 m wide (her point and a quarter metre each way are safe).</summary>
        static bool Corridor(Vector3 w)
        {
            if (!Safe(w, out _)) return false;
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (!Safe(w + d * 0.25f, out _)) return false;
            return true;
        }

        /// <summary>A dark point 8–14 m away, on screen, on her level, whose straight line from her is not safe but
        /// which a dark walk (0.5 m grid, 4-connected) reaches. Q5's mark.</summary>
        bool FindRoute(Vector3 her, out Vector3 mark)
        {
            mark = default;
            const float cell = 0.5f; const int half = 30;   // a 30 m square
            int size = half * 2 + 1;
            var ok = new sbyte[size * size];   // 0 unknown, 1 safe and walkable, -1 not
            Func<int, int, bool> walk = (x, z) =>
            {
                int k = z * size + x;
                if (ok[k] != 0) return ok[k] > 0;
                var w = new Vector3(her.x + (x - half) * cell, her.y, her.z + (z - half) * cell);
                bool good = UnityEngine.AI.NavMesh.SamplePosition(w, out var h, 0.3f, UnityEngine.AI.NavMesh.AllAreas)
                            && Mathf.Abs(h.position.y - her.y) < 0.5f && Safe(new Vector3(w.x, her.y, w.z), out _);
                ok[k] = (sbyte)(good ? 1 : -1);
                return good;
            };
            // flood the dark from her
            var seen = new bool[size * size];
            var queue = new Queue<int>();
            queue.Enqueue(half * size + half); seen[half * size + half] = true;
            var reached = new List<Vector3>();
            int budget = 2500;
            while (queue.Count > 0 && budget-- > 0)
            {
                int k = queue.Dequeue(); int x = k % size, z = k / size;
                var w = new Vector3(her.x + (x - half) * cell, her.y, her.z + (z - half) * cell);
                float d = Util.FlatDistance(w, her);
                if (d >= 8f && d <= 14f) reached.Add(w);
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                    int nk = nz * size + nx;
                    if (seen[nk]) continue;
                    seen[nk] = true;
                    if (walk(nx, nz)) queue.Enqueue(nk);
                }
            }
            // a reachable dark point whose straight line crosses danger: the question has a wrong answer
            for (int i = 0; i < reached.Count; i++)
            {
                var w = reached[(i * 7919) % reached.Count];
                if (!OnScreen(w)) continue;
                bool straight = true;
                for (float t = 0.5f; t < Util.FlatDistance(w, her); t += 0.5f)
                    if (!Safe(Vector3.MoveTowards(her, w, t), out _)) { straight = false; break; }
                if (straight) continue;
                mark = w;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the probe screen

        void StartProbe(Q q)
        {
            _q = q; _phase = Phase.Freeze; _phaseAt = Time.unscaledTime;
            _trace.Clear(); _tracing = false;
            Build();
            _shade.style.display = DisplayStyle.None;
            _title.text = $"Freeze. {Ask(q)}";
            _help.text = "Look now: the screen goes dark in 2 seconds.";
            Mark(_anchor, Color.yellow, Label(q));
        }

        void AskCause()
        {
            _q = Q.Q7; _phase = Phase.Ask; _phaseAt = Time.unscaledTime; _choice = 0;
            _target = _spottedBy ? _spottedBy.Id : "?";
            _truthText = _spottedCause.ToString();
            Build();
            _shade.style.display = DisplayStyle.Flex;
            ShowCauses();
        }

        void ShowCauses()
        {
            var t = "What just caused that detection?\n\n";
            for (int i = 0; i < CauseText.Length; i++) t += (i == _choice ? "▶ " : "   ") + CauseText[i] + "\n";
            _title.text = t;
            _help.text = "Keys 1–6 or 0.  Pad: d-pad up/down, A to answer.";
        }

        static string Ask(Q q)
        {
            switch (q)
            {
                case Q.Q1: return "Can this guard see you here?";
                case Q.Q2: return "Where does his cone end? (point)";
                case Q.Q3: return "How close can you get to him in darkness? (point)";
                case Q.Q4: return "Where does this lamp stop exposing you? (point)";
                case Q.Q5: return "Trace a safe route from you to the mark.";
                case Q.Q6: return "Will your next step, the way you are pressing, expose you?";
                default: return "";
            }
        }

        static string Label(Q q) => q == Q.Q4 ? "lamp" : q == Q.Q6 ? "step" : q == Q.Q5 ? "mark" : "him";

        void TickProbe()
        {
            if (_phase == Phase.Freeze)
            {
                if (Time.unscaledTime < _phaseAt + ReadTest.Freeze) return;
                _phase = Phase.Ask; _phaseAt = Time.unscaledTime;
                _shade.style.display = DisplayStyle.Flex;
                _title.text = Ask(_q);
                bool yn = _q == Q.Q1 || _q == Q.Q6;
                _help.text = yn ? "Y = yes, N = no.   Pad: A = yes, B = no."
                    : _q == Q.Q5 ? "Hold the left button and draw the route.   Pad: hold A and steer with the left stick."
                    : "Click the point.   Pad: steer with the left stick, A to place.";
                ClearMarks();
                Mark(_her, Color.white, "you");
                if (_q != Q.Q6) Mark(_anchor, Color.yellow, Label(_q));
                var c = Game.Cam != null ? Game.Cam.Cam : null;
                _cursor = c ? (Vector2)c.WorldToScreenPoint(_anchor) : new Vector2(Screen.width / 2f, Screen.height / 2f);
                return;
            }
            float t = Time.unscaledTime - _phaseAt;
            var kb = Keyboard.current; var ms = Mouse.current; var pad = Gamepad.current;
            if (pad != null)
            {
                var s = pad.leftStick.ReadValue();
                if (s.sqrMagnitude > 0.02f) _cursor += s * PadCursor * Time.unscaledDeltaTime;
            }
            if (ms != null && (ms.delta.ReadValue().sqrMagnitude > 0f || ms.leftButton.isPressed || ms.leftButton.wasReleasedThisFrame)) _cursor = ms.position.ReadValue();
            _cursor.x = Mathf.Clamp(_cursor.x, 0, Screen.width); _cursor.y = Mathf.Clamp(_cursor.y, 0, Screen.height);
            Place(_cursorEl, _cursor);
            _cursorEl.style.display = _q == Q.Q1 || _q == Q.Q6 || _q == Q.Q7 ? DisplayStyle.None : DisplayStyle.Flex;

            switch (_q)
            {
                case Q.Q1: case Q.Q6:
                {
                    bool yes = kb != null && kb.yKey.wasPressedThisFrame || pad != null && pad.buttonSouth.wasPressedThisFrame;
                    bool no = kb != null && kb.nKey.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame;
                    if (yes || no) Answer(yes == _truthYes, yes ? "yes" : "no", t, -1f, pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame));
                    break;
                }
                case Q.Q2: case Q.Q3: case Q.Q4:
                {
                    bool click = ms != null && ms.leftButton.wasPressedThisFrame, a = pad != null && pad.buttonSouth.wasPressedThisFrame;
                    if (!click && !a) break;
                    if (!Ground(_cursor, out var w)) break;
                    float err = _error(w);
                    Answer(err <= ReadTest.PointWithin, $"{w.x:0.0},{w.z:0.0}", t, err, a);
                    break;
                }
                case Q.Q5:
                {
                    bool held = ms != null && ms.leftButton.isPressed || pad != null && pad.buttonSouth.isPressed;
                    if (held && Ground(_cursor, out var w))
                    {
                        if (_trace.Count == 0 || Util.FlatDistance(_trace[_trace.Count - 1], w) > 0.3f) { _trace.Add(w); Dot(_cursor); }
                        _tracing = true;
                    }
                    else if (!held && _tracing)
                    {
                        _tracing = false;
                        if (_trace.Count < 2) { _trace.Clear(); ClearTrace(); break; }
                        bool safe = ReadTest.TraceSafe(_trace, _her, _mark, _safe);
                        Answer(safe, $"{_trace.Count} points", t, -1f, pad != null && pad.buttonSouth.wasReleasedThisFrame);
                    }
                    break;
                }
                case Q.Q7:
                {
                    int pick = -1;
                    if (kb != null)
                    {
                        if (kb.digit1Key.wasPressedThisFrame) pick = 0; else if (kb.digit2Key.wasPressedThisFrame) pick = 1;
                        else if (kb.digit3Key.wasPressedThisFrame) pick = 2; else if (kb.digit4Key.wasPressedThisFrame) pick = 3;
                        else if (kb.digit5Key.wasPressedThisFrame) pick = 4; else if (kb.digit6Key.wasPressedThisFrame) pick = 5;
                        else if (kb.digit0Key.wasPressedThisFrame) pick = 6;
                    }
                    bool padPick = false;
                    if (pad != null)
                    {
                        if (pad.dpad.down.wasPressedThisFrame) { _choice = (_choice + 1) % Causes.Length; ShowCauses(); }
                        if (pad.dpad.up.wasPressedThisFrame) { _choice = (_choice + Causes.Length - 1) % Causes.Length; ShowCauses(); }
                        if (pad.buttonSouth.wasPressedThisFrame) { pick = _choice; padPick = true; }
                    }
                    if (pick >= 0) Answer(ReadTest.CauseCorrect(_spottedCause, Causes[pick]), Causes[pick].ToString(), t, -1f, padPick);
                    break;
                }
            }
        }

        /// <summary>Where a screen point lands on the frozen frame's ground plane.</summary>
        bool Ground(Vector2 screen, out Vector3 w)
        {
            w = default;
            var c = Game.Cam != null ? Game.Cam.Cam : null;
            if (!c) return false;
            var ray = c.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, _plane, 0f));
            if (!plane.Raycast(ray, out float e)) return false;
            w = ray.GetPoint(e);
            return true;
        }

        void Answer(bool correct, string given, float time, float error, bool pad)
        {
            _log?.Answers.Add(new ReadTest.Answer { Q = (int)_q, Target = _target, Truth = _truthText, Given = given, Correct = correct, Time = time, Error = error, Pad = pad });
            EndProbe();
        }

        void EndProbe()
        {
            _phase = Phase.Off; _q = Q.None;
            if (_scr != null && Game.UI != null) Game.UI.PopScreen("readtest");
            _scr = null;
        }

        void Build()
        {
            _scr = new VisualElement { pickingMode = PickingMode.Position };
            _scr.style.position = Position.Absolute; _scr.style.left = 0; _scr.style.right = 0; _scr.style.top = 0; _scr.style.bottom = 0;
            _shade = new VisualElement { pickingMode = PickingMode.Ignore };
            _shade.style.position = Position.Absolute; _shade.style.left = 0; _shade.style.right = 0; _shade.style.top = 0; _shade.style.bottom = 0;
            _shade.style.backgroundColor = Color.black;
            _scr.Add(_shade);
            _marks = new VisualElement { pickingMode = PickingMode.Ignore };
            _marks.style.position = Position.Absolute; _marks.style.left = 0; _marks.style.right = 0; _marks.style.top = 0; _marks.style.bottom = 0;
            _scr.Add(_marks);
            _title = Text(26, 60);
            _help = Text(16, -1);
            _help.style.top = StyleKeyword.Auto; _help.style.bottom = 40;
            _scr.Add(_title); _scr.Add(_help);
            _cursorEl = Ring(Color.cyan, 22f);
            _cursorEl.style.display = DisplayStyle.None;
            _scr.Add(_cursorEl);
            _traceEls.Clear();
            Game.UI?.PushScreen("readtest", _scr, null, pausesGame: true, hideBelow: false);
        }

        static Label Text(int size, float top)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute; l.style.left = 0; l.style.right = 0;
            if (top >= 0) l.style.top = top;
            l.style.unityTextAlign = TextAnchor.UpperCenter;
            l.style.fontSize = size; l.style.color = Color.white;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        static VisualElement Ring(Color c, float size)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute; e.style.width = size; e.style.height = size;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = size / 2f;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 3f;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
            return e;
        }

        void Mark(Vector3 world, Color c, string text)
        {
            var cam = Game.Cam != null ? Game.Cam.Cam : null;
            if (!cam || _marks == null) return;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            var r = Ring(c, MarkSize);
            _marks.Add(r);
            Place(r, sp);
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute; l.style.color = c; l.style.fontSize = 14;
            _marks.Add(l);
            var pp = Panel(sp);
            l.style.left = pp.x + MarkSize; l.style.top = pp.y - MarkSize;
        }

        void Dot(Vector2 screen)
        {
            var d = Ring(Color.cyan, 6f);
            _marks.Add(d); _traceEls.Add(d);
            Place(d, screen);
        }

        void ClearTrace() { foreach (var e in _traceEls) e.RemoveFromHierarchy(); _traceEls.Clear(); }
        void ClearMarks() { _marks?.Clear(); _traceEls.Clear(); }

        Vector2 Panel(Vector2 screen) =>
            _scr != null && _scr.panel != null ? RuntimePanelUtils.ScreenToPanel(_scr.panel, new Vector2(screen.x, Screen.height - screen.y)) : new Vector2(screen.x, Screen.height - screen.y);

        void Place(VisualElement e, Vector2 screen)
        {
            var pp = Panel(screen);
            float w = e.resolvedStyle.width > 0f ? e.resolvedStyle.width : e.style.width.value.value;
            e.style.left = pp.x - w / 2f; e.style.top = pp.y - w / 2f;
        }
    }
}
