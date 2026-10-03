using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Stealth;
using Q = Vespertine.Stealth.ReadTest.Q;
using Cause = Vespertine.Stealth.ReadTest.Cause;

namespace Vespertine.Tests
{
    /// <summary>The WASD Stealth Readability Test's scoring (§44.1): answers are judged by the same geometry and rules as
    /// play, and the verdict holds the pass lines.</summary>
    public class ReadTestTests
    {
        static readonly Vector3 Apex = Vector3.zero, North = Vector3.forward;

        [Test]
        public void ArcErrorMeasuresToTheEndOrTheSides()
        {
            // a 30° half-angle cone, 10 m deep
            Assert.AreEqual(0f, ReadTest.ArcError(Apex, North, 30f, _ => 10f, new Vector3(0, 0, 10)), 1e-3f, "on the arc");
            Assert.AreEqual(1f, ReadTest.ArcError(Apex, North, 30f, _ => 10f, new Vector3(0, 0, 11)), 1e-3f, "a metre past");
            Assert.AreEqual(0.5f, ReadTest.ArcError(Apex, North, 30f, _ => 10f, new Vector3(0, 0, 9.5f)), 1e-3f, "inside, half a metre short");
            // a wall at 4 m straight ahead (from -5° to 5°) moves the end there
            float Walled(float off) => Mathf.Abs(off) < 5f ? 4f : 10f;
            Assert.AreEqual(0f, ReadTest.ArcError(Apex, North, 30f, Walled, new Vector3(0, 0, 4)), 1e-3f, "the wall is the end");
            Assert.Greater(ReadTest.ArcError(Apex, North, 30f, Walled, new Vector3(0, 0, 10)), 3f, "past the wall is wrong");
            // beside the cone: the distance to its side
            var side = Quaternion.Euler(0, 30, 0) * North * 5f;
            var outside = side + Quaternion.Euler(0, 120, 0) * North * 2f;   // 2 m out, square to the side
            Assert.AreEqual(2f, ReadTest.ArcError(Apex, North, 30f, _ => 10f, outside), 0.05f, "off the side");
        }

        [Test]
        public void NearErrorJoinsTheSectorAndTheTouchCircle()
        {
            // near sector 5 m, half 45°; touch circle 1.5 m all round
            Assert.AreEqual(0f, ReadTest.NearError(Apex, North, 45f, _ => 5f, 1.5f, new Vector3(0, 0, 5)), 1e-3f, "the near arc");
            Assert.AreEqual(0f, ReadTest.NearError(Apex, North, 45f, _ => 5f, 1.5f, new Vector3(0, 0, -1.5f)), 1e-3f, "behind him, the touch circle");
            Assert.AreEqual(1f, ReadTest.NearError(Apex, North, 45f, _ => 5f, 1.5f, new Vector3(0, 0, -2.5f)), 1e-3f, "a metre outside the circle");
            Assert.AreEqual(1f, ReadTest.NearError(Apex, North, 45f, _ => 5f, 1.5f, new Vector3(0, 0, 4f)), 1e-3f, "a metre inside the near arc");
            Assert.AreEqual(2f, ReadTest.NearError(Apex, North, 45f, _ => 3f, 1.5f, new Vector3(0, 0, 5f)), 1e-3f, "a wall at 3 m");
        }

        [Test]
        public void ContourErrorMarchesToWhereTheLightDrops()
        {
            // light falls linearly from 1 at the lamp to 0 at 8 m; the threshold is crossed at 8 × (1 − t)
            float t = DetectionMath.ExposedAt, edge = 8f * (1f - t);
            float L(Vector3 w) => Mathf.Clamp01(1f - new Vector2(w.x, w.z).magnitude / 8f);
            Assert.AreEqual(0f, ReadTest.ContourError(Apex, new Vector3(edge, 0, 0), L), 0.06f, "on the contour");
            Assert.AreEqual(1f, ReadTest.ContourError(Apex, new Vector3(0, 0, edge + 1f), L), 0.06f, "a metre out");
            Assert.AreEqual(edge, ReadTest.ContourError(Apex, Apex, L), 0.06f, "at the lamp");
            Assert.AreEqual(30f, ReadTest.ContourError(Apex, Vector3.zero, _ => 1f), 0.06f, "never drops: capped");
        }

        [Test]
        public void ATraceMustJoinHerToTheMarkThroughTheDark()
        {
            var her = Vector3.zero; var mark = new Vector3(10, 0, 0);
            bool Dark(Vector3 w) => !(w.x > 4f && w.x < 6f && Mathf.Abs(w.z) < 2f);   // a lit square in the middle
            var straight = new List<Vector3> { her, mark };
            var round = new List<Vector3> { her, new Vector3(3, 0, 3), new Vector3(7, 0, 3), mark };
            Assert.IsFalse(ReadTest.TraceSafe(straight, her, mark, Dark), "through the light");
            Assert.IsTrue(ReadTest.TraceSafe(round, her, mark, Dark), "round it");
            Assert.IsFalse(ReadTest.TraceSafe(new List<Vector3> { her, new Vector3(3, 0, 3) }, her, mark, Dark), "stops short");
            Assert.IsFalse(ReadTest.TraceSafe(new List<Vector3> { new Vector3(0, 0, 5), mark }, her, mark, Dark), "doesn't start at her");
            Assert.IsFalse(ReadTest.TraceSafe(new List<Vector3> { her }, her, mark, Dark), "one point");
        }

        [Test]
        public void StopsCountOnlyWhenLongAndNearAGuard()
        {
            var c = new ReadTest.StopClock();
            for (int i = 0; i < 25; i++) Assert.AreEqual(0f, c.Tick(0.1f, 0f, true));
            Assert.AreEqual(2.5f, c.Tick(0.1f, 3f, true), 1e-4f, "a 2.5 s stop ends when she moves");
            for (int i = 0; i < 10; i++) c.Tick(0.1f, 0f, true);
            Assert.AreEqual(0f, c.Tick(0.1f, 3f, true), "a 1 s pause isn't a stop");
            for (int i = 0; i < 30; i++) Assert.AreEqual(0f, c.Tick(0.1f, 0f, false), "far from guards");
            for (int i = 0; i < 30; i++) c.Tick(0.1f, 0f, true);
            Assert.AreEqual(3f, c.Flush(), 1e-4f, "flushed at the end");
            Assert.AreEqual(0f, c.Flush());
        }

        [Test]
        public void ProbesComeTwelveTimesAndRotateOverAvailableQuestions()
        {
            Assert.IsFalse(ReadTest.ProbeDue(29f, 0));
            Assert.IsTrue(ReadTest.ProbeDue(30f, 0));
            Assert.IsFalse(ReadTest.ProbeDue(60f, 1));
            Assert.IsTrue(ReadTest.ProbeDue(75f, 1));
            Assert.IsFalse(ReadTest.ProbeDue(10000f, ReadTest.ProbesPerMap), "twelve and no more");
            Assert.IsFalse(ReadTest.ProbeDue(200f, 2, 190f), "overdue, but only 10 s since the last");
            Assert.IsTrue(ReadTest.ProbeDue(210f, 2, 190f));

            int turn = 0;
            Assert.AreEqual(Q.Q1, ReadTest.Next(ref turn, _ => true));
            Assert.AreEqual(Q.Q2, ReadTest.Next(ref turn, _ => true));
            Assert.AreEqual(Q.Q4, ReadTest.Next(ref turn, q => q != Q.Q3), "skips one with no subject");
            Assert.AreEqual(Q.Q6, ReadTest.Next(ref turn, _ => true));
            Assert.AreEqual(Q.Q5, ReadTest.Next(ref turn, _ => true));
            Assert.AreEqual(Q.Q1, ReadTest.Next(ref turn, _ => true), "wraps");
            int before = turn;
            Assert.AreEqual(Q.None, ReadTest.Next(ref turn, _ => false));
            Assert.AreEqual(before, turn, "nothing asked, the turn stays");
        }

        [Test]
        public void CausesComeFromTheSpottedCaption()
        {
            Assert.AreEqual(Cause.Near, ReadTest.CauseOf(new DetectionMath.SightCause { Band = DetectionMath.Band.Near }));
            Assert.AreEqual(Cause.Light, ReadTest.CauseOf(new DetectionMath.SightCause { Band = DetectionMath.Band.Far }));
            Assert.AreEqual(Cause.Touch, ReadTest.CauseOf(new DetectionMath.SightCause { Band = DetectionMath.Band.Peripheral }));
            Assert.AreEqual(Cause.Searchlight, ReadTest.CauseOf(new DetectionMath.SightCause { Band = DetectionMath.Band.Far, Searchlight = true }));
            Assert.AreEqual(Cause.Smell, ReadTest.CauseOf(new DetectionMath.SightCause { Smell = true }));
            Assert.IsTrue(ReadTest.CauseCorrect(Cause.Light, Cause.Light));
            Assert.IsFalse(ReadTest.CauseCorrect(Cause.Light, Cause.Near));
            Assert.IsFalse(ReadTest.CauseCorrect(Cause.Unknown, Cause.Unknown), "\"I don't know\" is never right");
        }

        static ReadTest.Log Session(bool pad, int right, int wrong, Q q = Q.Q1, float time = 1f)
        {
            var l = new ReadTest.Log { Play = 600f, Alt = 30f, Pad = pad ? 500f : 0f, Entries = 20, Timely = 20 };
            for (int i = 0; i < right; i++) l.Answers.Add(new ReadTest.Answer { Q = (int)q, Correct = true, Time = time, Error = q >= Q.Q2 && q <= Q.Q4 ? 0.5f : -1f });
            for (int i = 0; i < wrong; i++) l.Answers.Add(new ReadTest.Answer { Q = (int)q, Correct = false, Time = time, Error = q >= Q.Q2 && q <= Q.Q4 ? 2f : -1f });
            return l;
        }

        static ReadTest.Line Find(List<ReadTest.Line> lines, string start) => lines.Find(l => l.Name.StartsWith(start));

        [Test]
        public void TheVerdictHoldsEachPassLine()
        {
            var good = new List<ReadTest.Log> { Session(false, 10, 0), Session(true, 10, 0) };
            var v = ReadTest.Verdict(good);
            Assert.IsTrue(Find(v, "Q1").Pass);
            Assert.IsFalse(Find(v, "Q2").Measured, "no Q2 asked");
            Assert.IsTrue(Find(v, "Alt").Pass, "5% Alt");
            Assert.IsTrue(Find(v, "Timeliness").Pass);
            Assert.IsTrue(Find(v, "Pad").Pass);
            StringAssert.Contains("PASS: every", ReadTest.Report(good));

            Assert.IsFalse(Find(ReadTest.Verdict(new List<ReadTest.Log> { Session(false, 8, 2) }), "Q1").Pass, "80% < 90%");
            Assert.IsFalse(Find(ReadTest.Verdict(new List<ReadTest.Log> { Session(false, 10, 0, Q.Q1, 3f) }), "Q1").Pass, "right but slow");
            Assert.IsTrue(Find(ReadTest.Verdict(new List<ReadTest.Log> { Session(false, 17, 3, Q.Q3) }), "Q3").Pass, "85% within a metre");
            Assert.IsFalse(Find(ReadTest.Verdict(new List<ReadTest.Log> { Session(false, 16, 4, Q.Q3) }), "Q3").Pass, "80%");

            var alt = Session(false, 10, 0); alt.Alt = 90f;
            Assert.IsFalse(Find(ReadTest.Verdict(new List<ReadTest.Log> { alt }), "Alt").Pass, "15% Alt");

            var late = Session(false, 10, 0); late.Timely = 18;
            Assert.IsFalse(Find(ReadTest.Verdict(new List<ReadTest.Log> { late }), "Timeliness").Pass, "90% timely");

            var gap = new List<ReadTest.Log> { Session(false, 10, 0), Session(true, 8, 2) };
            Assert.IsFalse(Find(ReadTest.Verdict(gap), "Pad").Pass, "a 20 point gap");
            StringAssert.Contains("not readable enough", ReadTest.Report(gap));

            var routes = Session(false, 0, 0);
            for (int i = 0; i < 9; i++) routes.Routes.Add(new ReadTest.Route { Safe = true });
            routes.Answers.Add(new ReadTest.Answer { Q = (int)Q.Q5, Correct = false });
            Assert.IsTrue(Find(ReadTest.Verdict(new List<ReadTest.Log> { routes }), "Q5").Pass, "9 of 10 safe");

            var why = Session(false, 0, 0);
            for (int i = 0; i < 8; i++) why.Answers.Add(new ReadTest.Answer { Q = (int)Q.Q7, Given = "Light", Correct = true, Time = 1f });
            for (int i = 0; i < 2; i++) why.Answers.Add(new ReadTest.Answer { Q = (int)Q.Q7, Given = "Unknown", Time = 1f });
            Assert.IsFalse(Find(ReadTest.Verdict(new List<ReadTest.Log> { why }), "Detections").Pass, "20% don't know");

            StringAssert.Contains("No data", ReadTest.Report(new List<ReadTest.Log>()));
        }

        [Test]
        public void TheLogSurvivesJson()
        {
            var l = Session(true, 2, 1);
            l.Mission = "gym";
            l.Detections.Add(new ReadTest.Detection { At = 12f, Guard = "seeker", Caption = "Seen: near", Cause = "Near" });
            l.Routes.Add(new ReadTest.Route { Id = "west", Chose = "z_top", Safe = true });
            var back = JsonUtility.FromJson<ReadTest.Log>(JsonUtility.ToJson(l));
            Assert.AreEqual("gym", back.Mission);
            Assert.AreEqual(3, back.Answers.Count);
            Assert.AreEqual("seeker", back.Detections[0].Guard);
            Assert.IsTrue(back.Routes[0].Safe);
            Assert.IsTrue(back.PadPlayer);
        }
    }
}
