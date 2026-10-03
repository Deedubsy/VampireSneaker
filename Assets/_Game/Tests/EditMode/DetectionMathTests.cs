using NUnit.Framework;
using UnityEngine;
using Vespertine.Stealth;

namespace Vespertine.Tests
{
    public class DetectionMathTests
    {
        static readonly VisionParams V = VisionParams.Default;
        static readonly Vector3 Eye = Vector3.zero;
        static readonly Vector3 Fwd = Vector3.forward;

        [Test]
        public void NearBandSeesInDarkness()
        {
            Assert.AreEqual(DetectionMath.Band.Near, DetectionMath.Classify(V, Eye, Fwd, new Vector3(0, 0, 4f), 0f));
            Assert.Greater(DetectionMath.Rate(V, Eye, Fwd, new Vector3(0, 0, 4f), 0f), 0f);
        }

        [Test]
        public void FarBandNeedsLight()
        {
            var far = new Vector3(0, 0, 10f);
            Assert.AreEqual(DetectionMath.Band.None, DetectionMath.Classify(V, Eye, Fwd, far, 0.1f));
            Assert.AreEqual(DetectionMath.Band.Far, DetectionMath.Classify(V, Eye, Fwd, far, 0.8f));
            Assert.AreEqual(0f, DetectionMath.Rate(V, Eye, Fwd, far, 0.1f));
        }

        [Test]
        public void BeyondFarRangeIsUnseen()
        {
            Assert.AreEqual(DetectionMath.Band.None, DetectionMath.Classify(V, Eye, Fwd, new Vector3(0, 0, 16f), 1f));
        }

        [Test]
        public void OutsideConeIsUnseenButPeripheralCatchesBumps()
        {
            Assert.AreEqual(DetectionMath.Band.None, DetectionMath.Classify(V, Eye, Fwd, new Vector3(0, 0, -4f), 1f));
            Assert.AreEqual(DetectionMath.Band.Peripheral, DetectionMath.Classify(V, Eye, Fwd, new Vector3(0, 0, -1f), 0f));
        }

        [Test]
        public void HighTargetsHiddenFromFarBandUnlessLookingUp()
        {
            var roof = new Vector3(0, 4f, 9f);
            Assert.AreEqual(DetectionMath.Band.None, DetectionMath.Classify(V, Eye, Fwd, roof, 1f));
            var up = V; up.LooksUp = true;
            Assert.AreEqual(DetectionMath.Band.Far, DetectionMath.Classify(up, Eye, Fwd, roof, 1f));
            // inside the near band height does not protect
            Assert.AreEqual(DetectionMath.Band.Near, DetectionMath.Classify(V, Eye, Fwd, new Vector3(0, 4f, 3f), 0f));
        }

        [Test]
        public void CloserAndBrighterFillsFaster()
        {
            float close = DetectionMath.Rate(V, Eye, Fwd, new Vector3(0, 0, 2f), 0.2f);
            float edge = DetectionMath.Rate(V, Eye, Fwd, new Vector3(0, 0, 5f), 0.2f);
            Assert.Greater(close, edge);
            float dim = DetectionMath.Rate(V, Eye, Fwd, new Vector3(0, 0, 9f), 0.5f);
            float lit = DetectionMath.Rate(V, Eye, Fwd, new Vector3(0, 0, 9f), 1f);
            Assert.Greater(lit, dim);
            Assert.AreEqual(2f * close, DetectionMath.Rate(V, Eye, Fwd, new Vector3(0, 0, 2f), 0.2f, 2f), 1e-4f);
        }

        [Test]
        public void MeterFillsHoldsThenDecays()
        {
            float m = DetectionMath.Step(0f, 2f, 0.25f, 0f);
            Assert.AreEqual(0.5f, m, 1e-5f);
            Assert.AreEqual(1f, DetectionMath.Step(0.9f, 5f, 1f, 0f), "clamped at 1");
            Assert.AreEqual(0.5f, DetectionMath.Step(0.5f, 0f, 1f, 0.5f), "held during the hold window");
            Assert.Less(DetectionMath.Step(0.5f, 0f, 1f, 2f), 0.5f, "decays after the hold");
            Assert.AreEqual(0f, DetectionMath.Step(0.05f, 0f, 10f, 5f), "never negative");
        }

        [Test]
        public void LightFalloffIsZeroAtRadius()
        {
            Assert.AreEqual(1f, DetectionMath.LightFalloff(1f, 5f, 0f), 1e-5f);
            Assert.AreEqual(0f, DetectionMath.LightFalloff(1f, 5f, 5f));
            Assert.Greater(DetectionMath.LightFalloff(1f, 5f, 2f), DetectionMath.LightFalloff(1f, 5f, 4f));
        }

        [Test]
        public void HitChanceFallsWithDistanceAndDarkness()
        {
            float near = DetectionMath.HitChance(2f, 1f);
            float far = DetectionMath.HitChance(16f, 1f);
            Assert.Greater(near, far);
            Assert.Less(DetectionMath.HitChance(8f, 0.1f), DetectionMath.HitChance(8f, 0.9f));
            Assert.GreaterOrEqual(DetectionMath.HitChance(100f, 0f), 0.05f);
        }

        [Test]
        public void WallsMuffleNoise()
        {
            Assert.IsTrue(DetectionMath.Hears(7f, 8f, false));
            Assert.IsFalse(DetectionMath.Hears(7f, 8f, true));
            Assert.IsTrue(DetectionMath.Hears(4f, 8f, true));
            Assert.IsTrue(DetectionMath.Hears(9f, 8f, false, 1.25f));
        }
    }
}
