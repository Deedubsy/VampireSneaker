using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Vespertine.AI;
using Vespertine.Controls;
using Vespertine.Save;
using Vespertine.Stealth;

namespace Vespertine.Core
{
    /// <summary>
    /// Runs the WASD Movement Test (GAMEPLAY_REDESIGN §44) while Settings → Gameplay → <i>Movement test mode</i> is on.
    /// Records free play without interrupting it: wall-sticks (where each happened), door stops, dashes, camera turns
    /// and zooms, and every detection with whether a cone's fringe had her just before. When the map ends it asks two
    /// questions: did moving feel right, and did the camera get in the way. Each map's log is saved as JSON under
    /// <c>movement/</c> in the save folder, with <c>report.txt</c> beside it holding the verdict over every log there.
    /// </summary>
    public class MoveTestRunner : MonoBehaviour
    {
        public static MoveTestRunner Instance { get; private set; }

        MoveTest.Log _log;
        readonly MoveTest.StickClock _sticks = new MoveTest.StickClock();
        readonly MoveTest.TurnClock _turns = new MoveTest.TurnClock(), _zooms = new MoveTest.TurnClock();
        readonly Dictionary<Npc, float> _graceAt = new Dictionary<Npc, float>();
        float _lastYaw, _lastDist;
        bool _haveCam, _lastPad, _wasDashing;

        enum Phase { Off, Felt, Camera, Done }
        Phase _phase;
        VisualElement _scr;
        Label _title, _help;

        public static bool On => Game.Settings != null && Game.Settings.MovementTest;
        /// <summary>The end-of-map questions are up (dev checks).</summary>
        public bool Asking => _phase == Phase.Felt || _phase == Phase.Camera;
        /// <summary>The log being recorded (dev checks).</summary>
        public MoveTest.Log Current => _log;

        void Awake() => Instance = this;
        void OnEnable() => GameEvents.SpottedCaption += OnSpotted;
        void OnDisable() => GameEvents.SpottedCaption -= OnSpotted;
        void OnApplicationQuit() { if (_log != null) Close(); }

        /// <summary>The folder logs go to (inside the dev sandbox when it is on).</summary>
        public static string Folder => Path.Combine(SaveSystem.BaseDir, "movement");

        /// <summary>The verdict over every log in <see cref="Folder"/>.</summary>
        public static string ReportAll()
        {
            var logs = new List<MoveTest.Log>();
            if (Directory.Exists(Folder))
                foreach (var f in Directory.GetFiles(Folder, "*.json"))
                {
                    try { var l = JsonUtility.FromJson<MoveTest.Log>(File.ReadAllText(f)); if (l != null) logs.Add(l); }
                    catch (Exception e) { Debug.LogWarning($"[MoveTest] {f}: {e.Message}"); }
                }
            return MoveTest.Report(logs);
        }

        void Update()
        {
            if (!On || !Game.InMission)
            {
                if (_phase != Phase.Off) Hide();
                if (_log != null) Close();
                _phase = Phase.Off;
                return;
            }
            if (Game.Mission.Ended)
            {
                // the map is over: ask, once, then save
                if (_log == null) return;
                if (_phase == Phase.Off) Ask(Phase.Felt);
                TickAsk();
                return;
            }
            if (_log == null) Open();
            Record();
        }

        // ------------------------------------------------------------------ the log

        void Open()
        {
            _log = new MoveTest.Log { Mission = Game.Mission.Info != null ? Game.Mission.Info.Id : "?", Started = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            _graceAt.Clear();
            _haveCam = false; _wasDashing = false;
            _phase = Phase.Off;
        }

        void Close()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var file = Path.Combine(Folder, $"{_log.Mission}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(file, JsonUtility.ToJson(_log, true));
                File.WriteAllText(Path.Combine(Folder, "report.txt"), ReportAll());
                Debug.Log($"[MoveTest] saved {file}");
            }
            catch (Exception e) { Debug.LogWarning($"[MoveTest] could not save: {e.Message}"); }
            _log = null;
        }

        void Record()
        {
            var p = Game.Player;
            float dt = Time.deltaTime;
            if (p == null || dt <= 0f || Game.AnyPause || Game.UI != null && Game.UI.BlocksGameplay) return;
            _log.Play += dt;
            _lastPad = ReadTestRunner.PadInput(_lastPad);
            if (_lastPad) _log.Pad += dt;

            // her walking: only while she, not a thrall, takes the keys
            float want = p.SelectedThrall == null && !p.Dead ? p.IntentSpeed : 0f;
            if (want >= MoveTest.MinWant) _log.Pushing += dt;
            bool exempt = p.Dashing || p.Busy || p.Concealed || p.TraverseLink != null || p.Agent && p.Agent.enabled && p.Agent.hasPath;
            switch (_sticks.Tick(dt, want, p.DirectSpeed, exempt, p.DoorHeld))
            {
                case MoveTest.Push.Stick:
                    _log.Sticks++;
                    _log.StickAt.Add(p.Feet);
                    Debug.Log($"[MoveTest] stick at {p.Feet}");
                    break;
                case MoveTest.Push.Door: _log.DoorStops++; break;
            }
            if (p.Dashing && !_wasDashing) _log.Dashes++;
            _wasDashing = p.Dashing;

            var cam = Game.Cam;
            if (cam != null)
            {
                if (_haveCam)
                {
                    float turn = Mathf.DeltaAngle(_lastYaw, cam.Yaw);
                    _log.CamDegrees += Mathf.Abs(turn);
                    if (_turns.Tick(dt, turn)) _log.CamTurns++;
                    if (_zooms.Tick(dt, cam.Distance - _lastDist)) _log.Zooms++;
                }
                _lastYaw = cam.Yaw; _lastDist = cam.Distance; _haveCam = true;
            }

            if (Game.AI != null)
                foreach (var n in Game.AI.Npcs)
                    if (n && n.InGrace) _graceAt[n] = _log.Play;
        }

        void OnSpotted(Npc n)
        {
            if (!On || _log == null || n == null || Game.Mission == null || Game.Mission.Ended) return;
            bool edge = _graceAt.TryGetValue(n, out var t) && _log.Play - t <= MoveTest.EdgeWithin;
            _log.Detections.Add(new MoveTest.Detection { At = _log.Play, Guard = n.Id, Caption = DetectionMath.Explain(n.LastCause), Edge = edge });
        }

        // ------------------------------------------------------------------ the two questions

        void Ask(Phase q)
        {
            _phase = q;
            if (_scr == null) Build();
            _title.text = q == Phase.Felt
                ? "Movement test: did moving Ilse feel right?"
                : "Did the camera ever get in your way?";
            _help.text = "Y  (pad A)  yes\nN  (pad B)  no";
        }

        void TickAsk()
        {
            if (_phase != Phase.Felt && _phase != Phase.Camera) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool yes = kb != null && kb.yKey.wasPressedThisFrame || pad != null && pad.buttonSouth.wasPressedThisFrame;
            bool no = kb != null && kb.nKey.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame;
            if (!yes && !no) return;
            Answer(yes);
        }

        /// <summary>Records an answer to the question that is up (also the dev hook).</summary>
        public void Answer(bool yes)
        {
            if (_log == null) return;
            if (_phase == Phase.Felt) { _log.FeltRight = yes ? 1 : 0; Ask(Phase.Camera); return; }
            if (_phase != Phase.Camera) return;
            _log.CameraTrouble = yes ? 1 : 0;
            Hide();
            _phase = Phase.Done;
            Close();
        }

        void Build()
        {
            _scr = new VisualElement { pickingMode = PickingMode.Position };
            _scr.style.position = Position.Absolute; _scr.style.left = 0; _scr.style.right = 0; _scr.style.top = 0; _scr.style.bottom = 0;
            _scr.style.backgroundColor = Color.black;
            _title = Text(26, 220);
            _help = Text(18, 290);
            _scr.Add(_title); _scr.Add(_help);
            Game.UI?.PushScreen("movetest", _scr, null, pausesGame: true, hideBelow: false);
        }

        void Hide()
        {
            if (_scr != null && Game.UI != null) Game.UI.PopScreen("movetest");
            _scr = null;
        }

        static Label Text(int size, float top)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute; l.style.left = 0; l.style.right = 0; l.style.top = top;
            l.style.unityTextAlign = TextAnchor.UpperCenter;
            l.style.fontSize = size; l.style.color = Color.white;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }
    }
}
