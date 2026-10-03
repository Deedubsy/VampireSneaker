using NUnit.Framework;
using UnityEngine;
using Vespertine.Player;
using Vespertine.Stealth;
using Band = Vespertine.Stealth.DetectionMath.Band;
using Toe = Vespertine.Stealth.StepRead.Toe;

namespace Vespertine.Tests
{
    /// <summary>Ilse's ground disc and its toe (SR.7, SR.8): the rules <c>Visual.IlseDisc</c> draws.</summary>
    public class StepReadTests
    {
        [Test]
        public void TheDiscIsExposedAtTheFarBandsThreshold()
        {
            Assert.IsFalse(StepRead.Exposed(DetectionMath.ExposedAt - 0.001f));
            Assert.IsTrue(StepRead.Exposed(DetectionMath.ExposedAt));
            // one threshold: exposed on the disc is lit enough for a far band
            Assert.AreEqual(VisionParams.Default.LitThreshold, DetectionMath.ExposedAt, 1e-5f);
        }

        [Test]
        public void AmongLeavesTheDiscJudgesTheLightTheGuardsDo()
        {
            // a lamp's full light among leaves: dimmed to 0.6 for her, and a guard judges a quarter of that
            Assert.AreEqual(0.6f * 0.25f, Vampire.JudgedLight(0.6f, true), 1e-5f);
            Assert.AreEqual(0.6f, Vampire.JudgedLight(0.6f, false), 1e-5f);
            Assert.IsFalse(StepRead.Exposed(Vampire.JudgedLight(0.9f, true)), "a lit thicket still hides her from the far band");
        }

        [TestCase(Band.None, Band.Far, Toe.Seen)]
        [TestCase(Band.None, Band.Near, Toe.Flash)]
        [TestCase(Band.None, Band.Peripheral, Toe.Flash)]
        [TestCase(Band.Far, Band.Near, Toe.Flash)]
        [TestCase(Band.Far, Band.Far, Toe.None)]
        [TestCase(Band.Near, Band.Near, Toe.None)]
        [TestCase(Band.Near, Band.Far, Toe.None)]
        [TestCase(Band.Far, Band.None, Toe.None)]
        [TestCase(Band.None, Band.None, Toe.None)]
        public void TheToeWarnsOnlyWhenTheStepMakesThingsWorse(Band now, Band next, Toe expected)
        {
            Assert.AreEqual(expected, StepRead.ForGuard(now, next));
        }

        [Test]
        public void TheLightsToeIsWarmOnlyFromHiddenToExposed()
        {
            Assert.AreEqual(Toe.Warm, StepRead.ForLight(0.2f, 0.5f));
            Assert.AreEqual(Toe.None, StepRead.ForLight(0.5f, 0.8f), "already exposed");
            Assert.AreEqual(Toe.None, StepRead.ForLight(0.5f, 0.1f), "stepping out of the light is no warning");
            Assert.AreEqual(Toe.None, StepRead.ForLight(0.1f, 0.3f));
        }

        [Test]
        public void TheWorstToeWins()
        {
            Assert.AreEqual(Toe.Flash, StepRead.Worse(Toe.Seen, Toe.Flash));
            Assert.AreEqual(Toe.Seen, StepRead.Worse(Toe.Seen, Toe.Warm));
            Assert.AreEqual(Toe.Warm, StepRead.Worse(Toe.None, Toe.Warm));
        }

        [Test]
        public void TheToeLooksFarEnoughToMatterAndGrowsWithSpeed()
        {
            Assert.AreEqual(StepRead.MinAhead, StepRead.Reach(0f), 1e-5f, "from a standstill it still looks half a metre");
            Assert.AreEqual(3.4f * StepRead.Ahead, StepRead.Reach(3.4f), 1e-5f);
            Assert.Greater(StepRead.Reach(6f), StepRead.Reach(3.4f));
        }

        [Test]
        public void WatcherTicksGrowWithTheMeter()
        {
            Assert.AreEqual(StepRead.TickMin, StepRead.TickLength(0f), 1e-5f);
            Assert.AreEqual(StepRead.TickMax, StepRead.TickLength(1f), 1e-5f);
            Assert.AreEqual(StepRead.TickMax, StepRead.TickLength(3f), 1e-5f);
            Assert.Greater(StepRead.TickLength(0.6f), StepRead.TickLength(0.3f));
        }

        [Test]
        public void TheNearWarningIsTheMetreOutsideHisNearSector()
        {
            var v = VisionParams.Default;
            var g = Vector3.zero; var f = Vector3.forward;
            Assert.IsFalse(StepRead.NearWarning(v, g, f, new Vector3(0, 0, v.NearRange - 0.2f)), "inside: the cone says it already");
            Assert.IsTrue(StepRead.NearWarning(v, g, f, new Vector3(0, 0, v.NearRange + 0.5f)));
            Assert.IsFalse(StepRead.NearWarning(v, g, f, new Vector3(0, 0, v.NearRange + 1.5f)));
            // beside the sector's edge, not just ahead of it
            var side = Quaternion.Euler(0, v.HalfAngle + 6f, 0) * f * (v.NearRange * 0.6f);
            Assert.IsTrue(StepRead.NearWarning(v, g, f, side));
            Assert.IsFalse(StepRead.NearWarning(v, g, f, -f * 4f), "well behind him");
        }

        [Test]
        public void TheToeAgreesWithTheBandsAcrossAStepIntoTheCone()
        {
            // walk her toward a guard in the dark from far outside his cone: the toe must turn Flash exactly where the next
            // step's band turns Near, never earlier, and never stay silent across that edge
            var v = VisionParams.Default;
            var g = Vector3.zero; var f = Vector3.forward;
            float step = StepRead.Reach(2.5f);
            bool flashed = false;
            for (float z = v.FarRange + 4f; z > v.NearRange; z -= 0.1f)
            {
                var here = new Vector3(0, 0, z); var next = new Vector3(0, 0, z - step);
                var toe = StepRead.ForGuard(DetectionMath.Classify(v, g, f, here, 0.1f), DetectionMath.Classify(v, g, f, next, 0.1f));
                bool shouldFlash = z - step <= v.NearRange && z > v.NearRange;
                Assert.AreEqual(shouldFlash ? Toe.Flash : Toe.None, toe, $"at {z:F1} m");
                flashed |= toe == Toe.Flash;
            }
            Assert.IsTrue(flashed);
        }
    }
}
