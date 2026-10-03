using System.Collections.Generic;
using NUnit.Framework;
using Vespertine.Controls;

namespace Vespertine.Tests
{
    /// <summary>The WASD Movement Test's scoring (§44): what a wall-stick is, camera turns, and the pass lines.</summary>
    public class MoveTestTests
    {
        static int Run(MoveTest.StickClock c, float secs, float want, float actual, bool exempt = false, bool door = false, MoveTest.Push kind = MoveTest.Push.Stick)
        {
            int n = 0;
            for (float t = 0f; t < secs; t += 0.02f)
                if (c.Tick(0.02f, want, actual, exempt, door) == kind) n++;
            return n;
        }

        [Test]
        public void AStickIsPushingWithoutMovingAndCountsOnce()
        {
            var c = new MoveTest.StickClock();
            Assert.AreEqual(0, Run(c, 0.25f, 3f, 0f), "under 0.3 s is not a stick yet");
            Assert.AreEqual(1, Run(c, 3f, 3f, 0f), "held against the wall for 3 s is one stick");
            Assert.AreEqual(0, Run(c, 1f, 3f, 2.5f), "moving freely again");
            Assert.AreEqual(1, Run(c, 1f, 3f, 0.2f), "caught again is a second stick");
            Assert.AreEqual(0, Run(c, 0.1f, 0f, 0f), "letting go");
            Assert.AreEqual(1, Run(c, 1f, 3f, 0.2f), "and caught once more");
        }

        [Test]
        public void SlidingAlongAWallAndStandingStillAreNotSticks()
        {
            var c = new MoveTest.StickClock();
            Assert.AreEqual(0, Run(c, 3f, 3f, 1.5f), "half speed along a wall");
            Assert.AreEqual(0, Run(c, 3f, 0f, 0f), "no input");
            Assert.AreEqual(0, Run(c, 3f, 0.3f, 0f), "a stick nudged too gently to count");
            Assert.AreEqual(0, Run(c, 3f, 3f, 0f, exempt: true), "pushing into a climb, a dash, a feed");
        }

        [Test]
        public void AShutDoorIsADoorStopNotAStick()
        {
            var c = new MoveTest.StickClock();
            Assert.AreEqual(1, Run(c, 1f, 3f, 0f, door: true, kind: MoveTest.Push.Door));
            c = new MoveTest.StickClock();
            Assert.AreEqual(0, Run(c, 1f, 3f, 0f, door: true, kind: MoveTest.Push.Stick));
        }

        [Test]
        public void ACameraTurnCountsOnceUntilItRests()
        {
            var t = new MoveTest.TurnClock();
            int n = 0;
            for (int i = 0; i < 20; i++) if (t.Tick(0.02f, 2f)) n++;
            Assert.AreEqual(1, n, "one hold");
            for (int i = 0; i < 5; i++) t.Tick(0.02f, 0f);
            if (t.Tick(0.02f, 2f)) n++;
            Assert.AreEqual(1, n, "a pause of 0.1 s is the same turn");
            for (int i = 0; i < 20; i++) t.Tick(0.02f, 0f);
            if (t.Tick(0.02f, -2f)) n++;
            Assert.AreEqual(2, n, "after 0.4 s at rest, a new one");
        }

        [Test]
        public void ThePassLinesAreUnderOneStickAMinuteAndEightyPercentFeltRight()
        {
            MoveTest.Log L(float play, int sticks, int felt) => new MoveTest.Log { Play = play, Sticks = sticks, FeltRight = felt };
            var logs = new List<MoveTest.Log> { L(600f, 4, 1), L(600f, 5, 1), L(600f, 3, 1), L(600f, 2, 1), L(600f, 6, 0) };
            var v = MoveTest.Verdict(logs);
            Assert.AreEqual(0.4f, v[0].Value, 1e-4f); Assert.IsTrue(v[0].Pass);
            Assert.AreEqual(0.8f, v[1].Value, 1e-4f); Assert.IsTrue(v[1].Pass);
            StringAssert.Contains("PASS", MoveTest.Report(logs));

            logs.Add(L(60f, 1, 0));
            v = MoveTest.Verdict(logs);
            Assert.IsFalse(v[1].Pass, "4 of 6 felt right");
            StringAssert.Contains("Movement doesn't feel right yet.", MoveTest.Report(logs));

            v = MoveTest.Verdict(new List<MoveTest.Log> { L(60f, 1, 1) });
            Assert.IsFalse(v[0].Pass, "exactly one a minute is not under one");
            StringAssert.Contains("No data yet.", MoveTest.Report(new List<MoveTest.Log>()));
        }

        [Test]
        public void UnansweredQuestionsAndInfoLinesDontDecide()
        {
            var logs = new List<MoveTest.Log> { new MoveTest.Log { Play = 600f, Sticks = 1, DoorStops = 30, CameraTrouble = 1 } };
            logs[0].Detections.Add(new MoveTest.Detection { Edge = true });
            var v = MoveTest.Verdict(logs);
            Assert.IsFalse(v[1].Measured, "no one answered");
            Assert.IsTrue(v[2].Info && v[3].Info && v[4].Info);
            Assert.AreEqual(1f, v[3].Value, 1e-4f);
            StringAssert.Contains("PASS", MoveTest.Report(logs));
        }
    }
}
