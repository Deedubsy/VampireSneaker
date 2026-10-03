using NUnit.Framework;
using UnityEngine;
using Vespertine.Core;
using Vespertine.Stealth;
using Vespertine.Visual;
using V = Vespertine.Core.DevConeCheck.Verdict;

namespace Vespertine.Tests
{
    /// <summary>The cone truth sweep's reading of a built cone (SR.14): what the drawing says at a point, and where it is
    /// too close to call.</summary>
    public class ConeTruthTests
    {
        const int Rays = ConeRenderer.Drawn.RayCount, N = ConeRenderer.Drawn.Samples;

        /// <summary>A 45° half-angle cone facing north: near fill to 4 m, ending at 10 m, its far band lit wherever
        /// <paramref name="lit"/> says (by distance).</summary>
        static ConeRenderer.Drawn Cone(System.Func<float, bool> lit)
        {
            var d = new ConeRenderer.Drawn { Origin = Vector3.zero, Fwd = Vector3.forward, Half = 45f };
            for (int i = 0; i <= Rays; i++)
            {
                d.Near[i] = 4f; d.Reach[i] = 10f; d.FarEnd[i] = 9.94f; d.FarOn[i] = true;
                for (int s = 0; s < N; s++) d.Lit[i * N + s] = lit(4f + (9.94f - 4f) * s / (N - 1));
            }
            return d;
        }

        static Vector3 At(float deg, float m) => Quaternion.Euler(0f, deg, 0f) * Vector3.forward * m;

        [Test]
        public void TheNearFillTheLitFarBandAndNothingElse()
        {
            var d = Cone(t => t < 7f);
            Assert.AreEqual(V.Near, DevConeCheck.Read(d, At(0f, 2f)));
            Assert.AreEqual(V.Near, DevConeCheck.Read(d, At(-30f, 3.9f)));
            Assert.AreEqual(V.Far, DevConeCheck.Read(d, At(0f, 4.05f)), "just past the near edge, lit: the far band");
            Assert.AreEqual(V.Far, DevConeCheck.Read(d, At(10f, 5.5f)), "lit far ground");
            Assert.AreEqual(V.Off, DevConeCheck.Read(d, At(10f, 8.5f)), "dark far ground hides her");
            Assert.AreEqual(V.Edge, DevConeCheck.Read(d, At(10f, 7.0f)), "between a lit and a dark sample");
            Assert.AreEqual(V.Edge, DevConeCheck.Read(d, At(0f, 9.97f)), "on the end arc");
            Assert.AreEqual(V.Off, DevConeCheck.Read(d, At(0f, 10.5f)), "past the end");
            Assert.AreEqual(V.Off, DevConeCheck.Read(d, At(50f, 2f)), "outside the sides");
            Assert.AreEqual(V.Off, DevConeCheck.Read(d, At(-50f, 2f)), "outside the other side");
        }

        [Test]
        public void AWallBetweenTwoRaysIsTooCloseToCall()
        {
            var d = Cone(t => true);
            // the straight-ahead ray (18 of 36) stops at a wall 3 m out
            d.Reach[Rays / 2] = 3f; d.Near[Rays / 2] = 3f; d.FarEnd[Rays / 2] = 3f; d.FarOn[Rays / 2] = false;
            float step = 90f / Rays;
            Assert.AreEqual(V.Near, DevConeCheck.Read(d, At(step * 0.5f, 2.5f)), "short of the wall");
            Assert.AreEqual(V.Edge, DevConeCheck.Read(d, At(step * 0.5f, 5f)), "between the walled ray and the open one");
            Assert.AreEqual(V.Off, DevConeCheck.Read(d, At(step * 0.5f, 10.5f)), "past both");
            Assert.AreEqual(V.Far, DevConeCheck.Read(d, At(step * 2.5f, 5f)), "two rays over, the cone is open");
        }

        [Test]
        public void TheRuleReadsAsTheDrawingWould()
        {
            Assert.AreEqual(V.Near, DevConeCheck.Of(DetectionMath.Band.Peripheral));
            Assert.AreEqual(V.Near, DevConeCheck.Of(DetectionMath.Band.Near));
            Assert.AreEqual(V.Far, DevConeCheck.Of(DetectionMath.Band.Far));
            Assert.AreEqual(V.Off, DevConeCheck.Of(DetectionMath.Band.None));
        }
    }
}
