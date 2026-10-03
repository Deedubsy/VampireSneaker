using NUnit.Framework;
using UnityEngine;
using Vespertine.Stealth;

namespace Vespertine.Tests
{
    /// <summary>The cone-edge grace (SR.5, D138): the outer 10° and outer metre fill at half rate, the first quarter
    /// second of a sighting doesn't count, and stepping in and out of an edge doesn't buy the allowance back.</summary>
    public class GraceTests
    {
        static readonly VisionParams V = VisionParams.Default; // half-angle 45, near 5.5, far 15, lit 0.35
        static readonly Vector3 Eye = Vector3.zero;
        static readonly Vector3 Fwd = Vector3.forward;

        static Vector3 At(float deg, float dist) => Quaternion.Euler(0f, deg, 0f) * Vector3.forward * dist;

        [Test]
        public void CentreOfTheConeHasNoGrace()
        {
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(0f, 3f), 0f));
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(20f, 10f), 1f));
        }

        [Test]
        public void OuterTenDegreesIsGrace()
        {
            Assert.IsTrue(DetectionMath.InGrace(V, Eye, Fwd, At(40f, 3f), 0f));
            Assert.IsTrue(DetectionMath.InGrace(V, Eye, Fwd, At(-38f, 10f), 1f));
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(33f, 3f), 0f));
        }

        [Test]
        public void OuterMetreFollowsWhatSeesHer()
        {
            // dark: the near edge (5.5 m) is the limit, so 4.5..5.5 m is the fringe
            Assert.IsTrue(DetectionMath.InGrace(V, Eye, Fwd, At(0f, 5f), 0f));
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(0f, 4f), 0f));
            // lit: the near edge isn't a limit any more (it stays solid), the far end is
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(0f, 5f), 1f));
            Assert.IsTrue(DetectionMath.InGrace(V, Eye, Fwd, At(0f, 14.5f), 1f));
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(0f, 13.5f), 1f));
        }

        [Test]
        public void HighAndLitAboveAGuardWhoDoesntLookUpIsJudgedByTheNearEdge()
        {
            var roof = At(0f, 5f) + Vector3.up * 3f;
            Assert.IsTrue(DetectionMath.InGrace(V, Eye, Fwd, roof, 1f));
            var up = V; up.LooksUp = true;
            Assert.IsFalse(DetectionMath.InGrace(up, Eye, Fwd, roof, 1f));
        }

        [Test]
        public void TouchRangeAndUnseenHaveNoGrace()
        {
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(0f, 1f), 0f), "peripheral");
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(0f, 8f), 0f), "dark far: unseen");
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(50f, 3f), 1f), "outside the cone");
        }

        [Test]
        public void MercifulWidensTheFringe()
        {
            Assert.IsFalse(DetectionMath.InGrace(V, Eye, Fwd, At(33f, 3f), 0f));
            Assert.IsTrue(DetectionMath.InGrace(V, Eye, Fwd, At(33f, 3f), 0f, 1.5f));
            Assert.IsTrue(DetectionMath.InGrace(V, Eye, Fwd, At(0f, 4.2f), 0f, 1.5f));
        }

        [Test]
        public void TheFirstQuarterSecondDoesntCount()
        {
            float onset = 0f, unseen = 0f, counted = 0f;
            for (int i = 0; i < 10; i++) counted += DetectionMath.Onset(ref onset, ref unseen, 1f, 0.05f) * 0.05f;
            // 0.5 s in view: 0.25 s counts (the tick that crosses the threshold counts whole)
            Assert.AreEqual(0.25f, counted, 0.051f);
        }

        [Test]
        public void DitheringAtTheEdgeDoesntBuyTheAllowanceBack()
        {
            float onset = 0f, unseen = 0f, counted = 0f;
            const float dt = 0.05f;
            // 0.2 s in, 0.3 s out, repeated: the allowance is spent once, then every second in view counts
            for (int cycle = 0; cycle < 6; cycle++)
            {
                for (int i = 0; i < 4; i++) counted += DetectionMath.Onset(ref onset, ref unseen, 1f, dt) * dt;
                for (int i = 0; i < 6; i++) DetectionMath.Onset(ref onset, ref unseen, 0f, dt);
            }
            // 1.2 s in view in total, 0.25 s forgiven
            Assert.AreEqual(0.95f, counted, 0.051f);
        }

        [Test]
        public void TheAllowanceReturnsAfterHeLosesHer()
        {
            float onset = 0f, unseen = 0f;
            for (int i = 0; i < 10; i++) DetectionMath.Onset(ref onset, ref unseen, 1f, 0.05f);
            Assert.Greater(DetectionMath.Onset(ref onset, ref unseen, 1f, 0.05f), 0f);
            // out of sight for the meter's hold (1.2 s): a fresh sighting
            for (int i = 0; i < 25; i++) DetectionMath.Onset(ref onset, ref unseen, 0f, 0.05f);
            Assert.AreEqual(0f, DetectionMath.Onset(ref onset, ref unseen, 1f, 0.05f));
        }
    }
}
