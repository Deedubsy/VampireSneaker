using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Vespertine.Stealth
{
    /// <summary>
    /// The WASD Stealth Readability Test (§44.1): its log, how each answer is scored against the rules that judge her,
    /// and the pass lines. Pure; <c>Core.ReadTestRunner</c> records play, freezes the probes and asks the questions.
    /// </summary>
    public static class ReadTest
    {
        /// <summary>Q1 can he see you, Q2 where his cone ends, Q3 how close in darkness, Q4 where a lamp stops exposing
        /// you, Q5 a safe route, Q6 will your next step expose you, Q7 what caused that detection.</summary>
        public enum Q { None, Q1, Q2, Q3, Q4, Q5, Q6, Q7 }

        /// <summary>What a detection was caused by, as Q7 offers it.</summary>
        public enum Cause { Unknown, Near, Light, Touch, Noise, Searchlight, Smell }

        public const float Freeze = 2f, AnswerWithin = 2f, PointWithin = 1f, Timely = 1.5f;
        public const float StopAfter = 2f, StopNear = 10f, StopSpeed = 0.15f;
        public const float YesNoPass = 0.9f, PointPass = 0.85f, RoutePass = 0.9f, AltMax = 0.1f, UnknownMax = 0.1f,
                           TimelyPass = 0.95f, PadGap = 0.1f;
        /// <summary>Freeze probes per map, the first one's time and the gap between them (12 over ~9 min of a 10 min
        /// free play).</summary>
        public const int ProbesPerMap = 12;
        public const float FirstProbe = 30f, ProbeEvery = 45f, RetryEvery = 2f, MinGap = 20f;
        /// <summary>The order probes take, skipping any with no fitting target at the moment.</summary>
        public static readonly Q[] Rotation = { Q.Q1, Q.Q2, Q.Q3, Q.Q4, Q.Q6, Q.Q5 };

        [Serializable]
        public class Answer
        {
            public int Q;
            public string Target, Truth, Given;
            public bool Correct;
            /// <summary>Seconds from the screen going dark to the answer.</summary>
            public float Time;
            /// <summary>Metres from the true boundary (Q2–Q4); -1 otherwise.</summary>
            public float Error = -1f;
            public bool Pad;
        }

        [Serializable]
        public class Detection
        {
            public float At;
            public string Guard, Caption, Cause;
        }

        [Serializable]
        public class Route
        {
            public string Id, Chose;
            public bool Safe;
        }

        [Serializable]
        public class Log
        {
            public string Mission, Started;
            public float Play, Alt, Paused, Pad;
            public int Entries, Timely, Stops;
            public float StopTime;
            public List<Answer> Answers = new List<Answer>();
            public List<Detection> Detections = new List<Detection>();
            public List<Route> Routes = new List<Route>();
            /// <summary>The player mostly used a pad (over half of the play time with pad input last).</summary>
            public bool PadPlayer => Play > 0f && Pad / Play > 0.5f;
        }

        /// <summary>Q7's answer is right when it names the logged cause; "don't know" is never right.</summary>
        public static bool CauseCorrect(Cause truth, Cause given) => given != Cause.Unknown && given == truth;

        /// <summary>The cause a Spotted caption came from (SR.10's <see cref="DetectionMath.SightCause"/>).</summary>
        public static Cause CauseOf(in DetectionMath.SightCause c)
        {
            if (c.Smell) return Cause.Smell;
            if (c.Searchlight) return Cause.Searchlight;
            switch (c.Band)
            {
                case DetectionMath.Band.Peripheral: return Cause.Touch;
                case DetectionMath.Band.Near: return Cause.Near;
                case DetectionMath.Band.Far: return Cause.Light;
                default: return Cause.Unknown;
            }
        }

        // ------------------------------------------------------------------ point answers (Q2–Q4)

        /// <summary>Flat distance from <paramref name="p"/> to the end of a cone: radius <paramref name="reachAt"/>(off)
        /// at each angle off his facing (walls clip it), and straight sides at ±<paramref name="half"/>. Q2.</summary>
        public static float ArcError(Vector3 apex, Vector3 fwd, float half, Func<float, float> reachAt, Vector3 p)
        {
            Polar(apex, fwd, p, out float d, out float off);
            float sides = SideDistance(apex, fwd, half, reachAt, p);
            return Mathf.Abs(off) <= half ? Mathf.Min(Mathf.Abs(d - reachAt(off)), sides) : sides;
        }

        /// <summary>Flat distance from <paramref name="p"/> to the edge of where darkness stops hiding her: his near
        /// sector (radius <paramref name="nearAt"/>(off), walls clip it) joined with his touch circle. Q3.</summary>
        public static float NearError(Vector3 apex, Vector3 fwd, float half, Func<float, float> nearAt, float touch, Vector3 p)
        {
            Polar(apex, fwd, p, out float d, out float off);
            bool inWedge = Mathf.Abs(off) <= half;
            float sector = inWedge ? Mathf.Min(Mathf.Abs(d - nearAt(off)), SideDistance(apex, fwd, half, nearAt, p))
                                   : SideDistance(apex, fwd, half, nearAt, p);
            float circle = Mathf.Abs(d - touch);
            bool inSector = inWedge && d <= nearAt(off), inCircle = d <= touch;
            // inside one shape the edge is that shape's (inside both, the farther one's); outside, the nearer shape's
            if (inSector && inCircle) return Mathf.Max(sector, circle);
            if (inSector) return sector;
            if (inCircle) return circle;
            return Mathf.Min(sector, circle);
        }

        /// <summary>How far <paramref name="p"/> is from where the light around <paramref name="lamp"/> stops exposing
        /// her, along the line from the lamp through <paramref name="p"/>: marches out until
        /// <paramref name="lightAt"/> drops under the threshold. Q4.</summary>
        public static float ContourError(Vector3 lamp, Vector3 p, Func<Vector3, float> lightAt, float maxR = 30f,
                                         float threshold = DetectionMath.ExposedAt, float step = 0.05f)
        {
            var v = p - lamp; v.y = 0f;
            float d = v.magnitude;
            var dir = d > 1e-4f ? v / d : Vector3.forward;
            float r = 0f;
            for (; r <= maxR; r += step)
            {
                var q = new Vector3(lamp.x + dir.x * r, p.y, lamp.z + dir.z * r);
                if (lightAt(q) < threshold) break;
            }
            return Mathf.Abs(d - Mathf.Min(r, maxR));
        }

        /// <summary>Is a traced route safe (Q5)? It must start near her (<paramref name="from"/>), end near the mark
        /// (<paramref name="to"/>), and every 0.25 m along it be <paramref name="safe"/>.</summary>
        public static bool TraceSafe(IReadOnlyList<Vector3> trace, Vector3 from, Vector3 to, Func<Vector3, bool> safe,
                                     float ends = 2f, float step = 0.25f)
        {
            if (trace == null || trace.Count < 2) return false;
            if (Flat(trace[0], from) > ends || Flat(trace[trace.Count - 1], to) > ends) return false;
            for (int i = 1; i < trace.Count; i++)
            {
                var a = trace[i - 1]; var b = trace[i];
                float len = Flat(a, b);
                int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
                for (int s = 0; s <= n; s++)
                    if (!safe(Vector3.Lerp(a, b, s / (float)n))) return false;
            }
            return true;
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        static void Polar(Vector3 apex, Vector3 fwd, Vector3 p, out float d, out float off)
        {
            var v = p - apex; v.y = 0f; fwd.y = 0f;
            d = v.magnitude;
            off = d < 1e-4f ? 0f : Vector3.SignedAngle(fwd, v, Vector3.up);
        }

        static float SideDistance(Vector3 apex, Vector3 fwd, float half, Func<float, float> reachAt, Vector3 p)
        {
            var v = p - apex; v.y = 0f; fwd.y = 0f; fwd.Normalize();
            float best = float.MaxValue;
            for (int s = -1; s <= 1; s += 2)
            {
                var e = Quaternion.Euler(0f, s * half, 0f) * fwd;
                float along = Mathf.Clamp(Vector3.Dot(v, e), 0f, reachAt(s * half));
                best = Mathf.Min(best, (v - e * along).magnitude);
            }
            return best;
        }

        // ------------------------------------------------------------------ play

        /// <summary>Stops of <see cref="StopAfter"/> s or more near a guard (hesitation, §44.1 measures).</summary>
        public class StopClock
        {
            float _still;
            /// <summary>Feeds one frame; returns the length of a stop that just ended (0 if none did).</summary>
            public float Tick(float dt, float speed, bool nearGuard)
            {
                if (speed < StopSpeed && nearGuard) { _still += dt; return 0f; }
                float s = _still >= StopAfter ? _still : 0f;
                _still = 0f;
                return s;
            }
            /// <summary>Ends any stop in progress (the session is closing).</summary>
            public float Flush() { float s = _still >= StopAfter ? _still : 0f; _still = 0f; return s; }
        }

        /// <summary>Is a freeze probe due? <paramref name="asked"/> is how many this map has had, the last at
        /// <paramref name="lastAt"/> s of play; after a stretch with nothing to ask about, the overdue ones still come at
        /// least <see cref="MinGap"/> apart.</summary>
        public static bool ProbeDue(float play, int asked, float lastAt = float.NegativeInfinity) =>
            asked < ProbesPerMap && play >= FirstProbe + ProbeEvery * asked && play >= lastAt + MinGap;

        /// <summary>The next question in <see cref="Rotation"/> from <paramref name="turn"/> that has a target now;
        /// <see cref="Q.None"/> if none does. <paramref name="turn"/> moves past the one picked.</summary>
        public static Q Next(ref int turn, Func<Q, bool> available)
        {
            for (int k = 0; k < Rotation.Length; k++)
            {
                var q = Rotation[(turn + k) % Rotation.Length];
                if (!available(q)) continue;
                turn = (turn + k + 1) % Rotation.Length;
                return q;
            }
            return Q.None;
        }

        // ------------------------------------------------------------------ the verdict

        public struct Line
        {
            public string Name;
            public bool Pass, Measured;
            public float Value, Need;
            public int Count;
            public override string ToString() =>
                Measured ? $"{(Pass ? "PASS" : "FAIL")}  {Name}: {Value:0.###} (need {Need:0.###}, n={Count})" : $"----  {Name}: no data";
        }

        /// <summary>The §44.1 pass lines over a set of sessions, plus the acceptance timeliness line (≥ 95% of entries
        /// into a detecting region with that guard's cone shown ≥ 1.5 s before).</summary>
        public static List<Line> Verdict(IReadOnlyList<Log> logs)
        {
            var all = new List<Answer>();
            float play = 0f, alt = 0f;
            int entries = 0, timely = 0, routes = 0, safe = 0;
            foreach (var l in logs)
            {
                all.AddRange(l.Answers);
                play += l.Play; alt += l.Alt; entries += l.Entries; timely += l.Timely;
                foreach (var r in l.Routes) { routes++; if (r.Safe) safe++; }
            }
            var lines = new List<Line>();
            foreach (var q in new[] { Q.Q1, Q.Q6, Q.Q7 })
            {
                int n = 0, ok = 0;
                foreach (var a in all) if (a.Q == (int)q) { n++; if (a.Correct && a.Time <= AnswerWithin) ok++; }
                lines.Add(Ratio($"{q} correct within {AnswerWithin:0} s", ok, n, YesNoPass, true));
            }
            foreach (var q in new[] { Q.Q2, Q.Q3, Q.Q4 })
            {
                int n = 0, ok = 0;
                foreach (var a in all) if (a.Q == (int)q) { n++; if (a.Error >= 0f && a.Error <= PointWithin) ok++; }
                lines.Add(Ratio($"{q} within {PointWithin:0} m", ok, n, PointPass, true));
            }
            {
                int n = routes, ok = safe;
                foreach (var a in all) if (a.Q == (int)Q.Q5) { n++; if (a.Correct) ok++; }
                lines.Add(Ratio("Q5 and route choice: safe route", ok, n, RoutePass, true));
            }
            lines.Add(new Line { Name = "Alt share of play", Measured = play > 0f, Value = play > 0f ? alt / play : 0f, Need = AltMax, Pass = play <= 0f || alt / play <= AltMax, Count = logs.Count });
            {
                int n = 0, unknown = 0;
                foreach (var a in all) if (a.Q == (int)Q.Q7) { n++; if (a.Given == Cause.Unknown.ToString()) unknown++; }
                lines.Add(new Line { Name = "Detections rated \"I don't know why\"", Measured = n > 0, Value = n > 0 ? unknown / (float)n : 0f, Need = UnknownMax, Pass = n == 0 || unknown / (float)n <= UnknownMax, Count = n });
            }
            lines.Add(Ratio("Timeliness: cone shown 1.5 s before entry (acceptance)", timely, entries, TimelyPass, true));
            // pad and KB/M players: no question's accuracy more than 10 points apart
            {
                float worst = -1f;
                for (var q = Q.Q1; q <= Q.Q7; q++)
                {
                    float pad = Accuracy(logs, q, true), key = Accuracy(logs, q, false);
                    if (pad >= 0f && key >= 0f) worst = Mathf.Max(worst, Mathf.Abs(pad - key));
                }
                lines.Add(new Line { Name = "Pad vs KB/M accuracy gap", Measured = worst >= 0f, Value = Mathf.Max(0f, worst), Need = PadGap, Pass = worst <= PadGap, Count = logs.Count });
            }
            return lines;
        }

        static Line Ratio(string name, int ok, int n, float need, bool atLeast) => new Line
        {
            Name = name, Measured = n > 0, Count = n, Need = need,
            Value = n > 0 ? ok / (float)n : 0f,
            Pass = n == 0 || (atLeast ? ok / (float)n >= need - 1e-6f : ok / (float)n <= need + 1e-6f),
        };

        /// <summary>Share of right answers to <paramref name="q"/> among pad (or KB/M) players' sessions; -1 if none.</summary>
        static float Accuracy(IReadOnlyList<Log> logs, Q q, bool pad)
        {
            int n = 0, ok = 0;
            foreach (var l in logs)
            {
                if (l.PadPlayer != pad) continue;
                foreach (var a in l.Answers)
                {
                    if (a.Q != (int)q) continue;
                    n++;
                    bool right = q == Q.Q2 || q == Q.Q3 || q == Q.Q4 ? a.Error >= 0f && a.Error <= PointWithin : a.Correct;
                    if (right) ok++;
                }
            }
            return n > 0 ? ok / (float)n : -1f;
        }

        /// <summary>The verdict as text: one line each, then the overall result in §44.1's words.</summary>
        public static string Report(IReadOnlyList<Log> logs)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"WASD Stealth Readability Test: {logs.Count} session(s)");
            bool pass = true, any = false;
            foreach (var l in Verdict(logs))
            {
                sb.AppendLine(l.ToString());
                if (l.Measured) { any = true; pass &= l.Pass; }
            }
            sb.AppendLine(!any ? "No data yet." : pass ? "PASS: every measured line holds." : "The stealth information is not readable enough yet.");
            return sb.ToString();
        }
    }
}
