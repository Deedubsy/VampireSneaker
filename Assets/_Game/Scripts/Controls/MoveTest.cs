using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Vespertine.Controls
{
    /// <summary>
    /// The WASD Movement Test (GAMEPLAY_REDESIGN §44): its log, what counts as a wall-stick, and the pass lines
    /// (under 1 stick a minute; at least 80% of players say movement "felt right"). Pure; <c>Core.MoveTestRunner</c>
    /// records play and asks the two questions when a map ends.
    /// </summary>
    public static class MoveTest
    {
        /// <summary>A stick: pushing for at least <see cref="MinWant"/> m/s, she moves at under <see cref="StickShare"/>
        /// of it for <see cref="StickAfter"/> s. Sliding along a wall faster than that is not a stick.</summary>
        public const float MinWant = 0.5f, StickShare = 0.35f, StickAfter = 0.3f, Recover = 0.6f;
        public const float SticksPass = 1f, FeltPass = 0.8f;
        /// <summary>A detection is "at a cone edge" if his fringe (D138) held her within this many seconds before it.</summary>
        public const float EdgeWithin = 2f;
        /// <summary>A camera turn or zoom is a new one after this long at rest.</summary>
        public const float TurnRest = 0.3f;

        public enum Push { None, Stick, Door }

        /// <summary>Counts each stick once: it stays latched until she moves freely again or lets go.</summary>
        public class StickClock
        {
            float _t;
            bool _latched;

            /// <param name="want">The speed the input asks for.</param>
            /// <param name="actual">The speed she makes.</param>
            /// <param name="exempt">Not her walking (a dash, a climb push, a feed, a hiding spot).</param>
            /// <param name="door">A shut door is what holds her.</param>
            public Push Tick(float dt, float want, float actual, bool exempt, bool door)
            {
                if (want < MinWant || exempt || actual >= want * Recover) { _t = 0f; _latched = false; return Push.None; }
                if (actual >= want * StickShare) { _t = 0f; return Push.None; }
                if (_latched) return Push.None;
                _t += dt;
                if (_t < StickAfter) return Push.None;
                _latched = true;
                return door ? Push.Door : Push.Stick;
            }
        }

        /// <summary>Counts camera turns (or zooms): true on the frame one starts after <see cref="TurnRest"/> s still.</summary>
        public class TurnClock
        {
            float _rest = TurnRest;

            public bool Tick(float dt, float change)
            {
                if (Mathf.Abs(change) < 0.01f) { _rest += dt; return false; }
                bool fresh = _rest >= TurnRest;
                _rest = 0f;
                return fresh;
            }
        }

        [Serializable]
        public class Detection
        {
            public float At;
            public string Guard, Caption;
            /// <summary>His cone's fringe held her within <see cref="EdgeWithin"/> s before.</summary>
            public bool Edge;
        }

        [Serializable]
        public class Log
        {
            public string Mission, Started;
            /// <summary>Seconds of play, of pad input, and of pushing the move keys.</summary>
            public float Play, Pad, Pushing;
            public int Sticks, DoorStops, Dashes, CamTurns, Zooms;
            public float CamDegrees;
            /// <summary>Where each stick happened, for finding the corner that caught her.</summary>
            public List<Vector3> StickAt = new List<Vector3>();
            public List<Detection> Detections = new List<Detection>();
            /// <summary>The end-of-map answers: 1 yes, 0 no, -1 not answered.</summary>
            public int FeltRight = -1, CameraTrouble = -1;
            public bool PadPlayer => Play > 0f && Pad / Play > 0.5f;
        }

        public struct Line
        {
            public string Name;
            public bool Pass, Measured, Info;
            public float Value, Need;
            public int Count;
            public override string ToString() =>
                !Measured ? $"----  {Name}: no data"
                : Info ? $"info  {Name}: {Value:0.###} (n={Count})"
                : $"{(Pass ? "PASS" : "FAIL")}  {Name}: {Value:0.###} (need {Need:0.###}, n={Count})";
        }

        /// <summary>The pass lines over a set of sessions, then the other measures as information.</summary>
        public static List<Line> Verdict(IReadOnlyList<Log> logs)
        {
            float play = 0f;
            int sticks = 0, doors = 0, asked = 0, felt = 0, camAsked = 0, camTrouble = 0, det = 0, edge = 0, turns = 0;
            foreach (var l in logs)
            {
                play += l.Play; sticks += l.Sticks; doors += l.DoorStops; turns += l.CamTurns;
                if (l.FeltRight >= 0) { asked++; felt += l.FeltRight; }
                if (l.CameraTrouble >= 0) { camAsked++; camTrouble += l.CameraTrouble; }
                foreach (var d in l.Detections) { det++; if (d.Edge) edge++; }
            }
            float min = play / 60f;
            var lines = new List<Line>
            {
                new Line { Name = "Wall-sticks per minute", Measured = play > 0f, Value = min > 0f ? sticks / min : 0f, Need = SticksPass, Pass = min <= 0f || sticks / min < SticksPass, Count = sticks },
                new Line { Name = "Said movement \"felt right\"", Measured = asked > 0, Value = asked > 0 ? felt / (float)asked : 0f, Need = FeltPass, Pass = asked == 0 || felt / (float)asked >= FeltPass - 1e-6f, Count = asked },
                new Line { Name = "Door stops per minute", Info = true, Measured = play > 0f, Value = min > 0f ? doors / min : 0f, Count = doors },
                new Line { Name = "Detections at a cone edge (share)", Info = true, Measured = det > 0, Value = det > 0 ? edge / (float)det : 0f, Count = det },
                new Line { Name = "Said the camera got in the way", Info = true, Measured = camAsked > 0, Value = camAsked > 0 ? camTrouble / (float)camAsked : 0f, Count = camAsked },
                new Line { Name = "Camera turns per minute", Info = true, Measured = play > 0f, Value = min > 0f ? turns / min : 0f, Count = turns },
            };
            return lines;
        }

        public static string Report(IReadOnlyList<Log> logs)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"WASD Movement Test: {logs.Count} session(s)");
            bool pass = true, any = false;
            foreach (var l in Verdict(logs))
            {
                sb.AppendLine(l.ToString());
                if (l.Measured && !l.Info) { any = true; pass &= l.Pass; }
            }
            sb.AppendLine(!any ? "No data yet." : pass ? "PASS: every measured line holds." : "Movement doesn't feel right yet.");
            return sb.ToString();
        }
    }
}
