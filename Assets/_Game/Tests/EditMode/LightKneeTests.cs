using NUnit.Framework;
using UnityEngine;
using Vespertine.Stealth;

namespace Vespertine.Tests
{
    /// <summary>The light pool's visible knee (SR.4, K34): flat inside the exposure contour, gone a feather past it.</summary>
    public class LightKneeTests
    {
        [Test]
        public void TheTargetIsFlatInsideAndEndsAFeatherPast()
        {
            const float c = 5f;
            Assert.AreEqual(LightKnee.AtContour, LightKnee.Target(c, c, DetectionMath.ExposedAt), 1e-5f, "at the contour");
            Assert.AreEqual(1f, LightKnee.Target(1f, c, 0.9f), 1e-5f, "blazing");
            Assert.GreaterOrEqual(LightKnee.Target(3f, c, 0.5f), LightKnee.AtContour);
            Assert.AreEqual(0f, LightKnee.Target(c + LightKnee.Feather, c, 0.2f), 1e-5f, "gone a feather past");
            float prev = 1f;
            for (float r = c; r <= c + LightKnee.Feather; r += 0.02f)
            {
                float v = LightKnee.Target(r, c, 0.2f);
                Assert.LessOrEqual(v, prev + 1e-6f, "falls without a bump");
                prev = v;
            }
        }

        [Test]
        public void GroundIsInverseSquareAndLambert()
        {
            const float h = 3f, far = 1e4f;
            Assert.AreEqual(1f / 9f, LightKnee.Ground(0f, h, far), 1e-5f);
            // at 45° the ground is √2·h away and lit at cos 45°
            Assert.AreEqual(Mathf.Cos(Mathf.PI / 4f) / 18f, LightKnee.Ground(h, h, far), 1e-5f);
            Assert.AreEqual(0f, LightKnee.Ground(10f, h, 5f), 1e-6f, "past the range");
        }

        [Test]
        public void TheCookieCancelsTheFallOff()
        {
            const int n = 64;
            const float c = 5.47f, h = 3.2f;
            LightKnee.Shape(c, h, out float inner, out float outer, out float range);
            Assert.Greater(inner, c + LightKnee.Feather, "the spot's own edge is past the feather");
            Assert.Greater(outer, inner);
            var v = new float[n * n];
            float k = LightKnee.Cookie(v, n, c, h, outer, range, r => 0.06f + DetectionMath.LightFalloff(1f, 7f, Mathf.Sqrt(r * r + 4.84f)));
            Assert.Greater(k, 0f);
            float max = 0f;
            for (int i = 0; i < v.Length; i++) { Assert.GreaterOrEqual(v[i], 0f); max = Mathf.Max(max, v[i]); }
            Assert.AreEqual(1f, max, 1e-5f, "normalised");
            // the ground the spot then lays down, per unit intensity, is k × Target at every texel
            for (int y = 0; y < n; y += 7)
            for (int x = 0; x < n; x += 5)
            {
                float r = LightKnee.RadiusAt(x, y, n, outer);
                float lit = v[y * n + x] * LightKnee.Ground(r, h, range);
                float want = k * LightKnee.Target(r, c, 0.06f + DetectionMath.LightFalloff(1f, 7f, Mathf.Sqrt(r * r + 4.84f)));
                Assert.AreEqual(want, lit, 1e-4f * k + 1e-7f, $"texel {x},{y} r={r:0.00}");
            }
        }

        [Test]
        public void TexelRadiiSpanTheOuterCone()
        {
            Assert.Less(LightKnee.RadiusAt(32, 32, 64, 6f), 0.15f, "centre");
            Assert.AreEqual(6f, LightKnee.RadiusAt(63, 32, 64, 6f), 0.15f, "edge of the square is the outer radius");
        }
    }
}
