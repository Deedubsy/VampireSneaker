using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Data;
using Vespertine.Stealth;
using Show = Vespertine.Stealth.ConeContext.Show;
using Cand = Vespertine.Stealth.ConeContext.Candidate;

namespace Vespertine.Tests
{
    /// <summary>The readability quick wins (QW15, QW16, QW18): exposure contours, cone distances, the contextual pick and
    /// the Spotted caption.</summary>
    public class ReadabilityTests
    {
        const float Ambient = 0.06f;

        // ---- exposure radius (SR.4 table) ----

        [TestCase(1f, 7f, 3.2f, 5.5f, TestName = "GasLamp")]
        [TestCase(1f, 6f, 2.6f, 4.8f, TestName = "WallLamp")]
        [TestCase(0.9f, 4.5f, 1.1f, 3.7f, TestName = "Lantern")]
        public void ExposureRadiusMatchesTable(float intensity, float radius, float height, float expected)
        {
            Assert.AreEqual(expected, DetectionMath.ExposureRadius(intensity, radius, height - 1f, Ambient), 0.06f);
        }

        [Test]
        public void ExposureRadiusIsWhereLightCrossesThreshold()
        {
            // the light at the contour, measured as LightSystem does (chest height, 3D distance), is the threshold
            float I = 1f, R = 7f, H = 3.2f;
            float c = DetectionMath.ExposureRadius(I, R, H - 1f, Ambient);
            float d = Mathf.Sqrt(c * c + (H - 1f) * (H - 1f));
            Assert.AreEqual(DetectionMath.ExposedAt, Ambient + DetectionMath.LightFalloff(I, R, d), 0.002f);
            Assert.Greater(Ambient + DetectionMath.LightFalloff(I, R, d - 0.2f), DetectionMath.ExposedAt);
            Assert.Less(Ambient + DetectionMath.LightFalloff(I, R, d + 0.2f), DetectionMath.ExposedAt);
        }

        [Test]
        public void ExposureRadiusEdgeCases()
        {
            // a high, weak moon never exposes her on the ground
            Assert.AreEqual(0f, DetectionMath.ExposureRadius(0.5f, 5f, 11f, Ambient));
            // too dim to ever reach the threshold
            Assert.AreEqual(0f, DetectionMath.ExposureRadius(0.2f, 6f, 0.5f, Ambient));
            // a blackout (scale 0) removes it; a bright night (ambient over threshold) exposes the whole reach
            Assert.AreEqual(0f, DetectionMath.ExposureRadius(1f, 7f, 2.2f, Ambient, 0f));
            Assert.AreEqual(Mathf.Sqrt(49f - 2.2f * 2.2f), DetectionMath.ExposureRadius(1f, 7f, 2.2f, 0.4f), 0.001f);
            // brighter nights widen it
            Assert.Greater(DetectionMath.ExposureRadius(1f, 7f, 2.2f, 0.15f), DetectionMath.ExposureRadius(1f, 7f, 2.2f, Ambient));
        }

        // ---- distance to a cone sector ----

        [Test]
        public void DistanceToSectorInsideIsZero()
        {
            Assert.AreEqual(0f, DetectionMath.DistanceToSector(Vector3.zero, Vector3.forward, 45f, 5.5f, new Vector3(0, 0, 3f)));
            Assert.AreEqual(0f, DetectionMath.DistanceToSector(Vector3.zero, Vector3.forward, 45f, 5.5f, new Vector3(1f, 0, 2f)));
        }

        [Test]
        public void DistanceToSectorBeyondArc()
        {
            Assert.AreEqual(2.5f, DetectionMath.DistanceToSector(Vector3.zero, Vector3.forward, 45f, 5.5f, new Vector3(0, 0, 8f)), 0.001f);
            // height is ignored
            Assert.AreEqual(2.5f, DetectionMath.DistanceToSector(Vector3.zero, Vector3.forward, 45f, 5.5f, new Vector3(0, 3f, 8f)), 0.001f);
        }

        [Test]
        public void DistanceToSectorBesideAndBehind()
        {
            // directly to his right at 3 m: the 45° edge passes 3·sin45 away
            Assert.AreEqual(3f * Mathf.Sin(45f * Mathf.Deg2Rad), DetectionMath.DistanceToSector(Vector3.zero, Vector3.forward, 45f, 5.5f, new Vector3(3f, 0, 0)), 0.001f);
            Assert.AreEqual(3f * Mathf.Sin(45f * Mathf.Deg2Rad), DetectionMath.DistanceToSector(Vector3.zero, Vector3.forward, 45f, 5.5f, new Vector3(-3f, 0, 0)), 0.001f);
            // directly behind: nearest point is the apex
            Assert.AreEqual(4f, DetectionMath.DistanceToSector(Vector3.zero, Vector3.forward, 45f, 5.5f, new Vector3(0, 0, -4f)), 0.001f);
            // a rotated guard: facing +x, a point at +z is beside him
            Assert.Greater(DetectionMath.DistanceToSector(Vector3.zero, Vector3.right, 45f, 5.5f, new Vector3(0, 0, 3f)), 1f);
            Assert.AreEqual(0f, DetectionMath.DistanceToSector(Vector3.zero, Vector3.right, 45f, 5.5f, new Vector3(3f, 0, 0)));
        }

        // ---- contextual pick ----

        static Show[] Pick(params Cand[] c)
        {
            var r = new Show[c.Length];
            ConeContext.Pick(c, r);
            return r;
        }

        [Test]
        public void ContextualQualification()
        {
            var r = Pick(
                new Cand { Gap = 3.9f, Distance = 8f },                       // near danger
                new Cand { Gap = 4.5f, Distance = 12f },                      // too far, unaware
                new Cand { Gap = 20f, Distance = 24f, Aware = true },         // aware within 25 m
                new Cand { Gap = 20f, Distance = 26f, Aware = true },         // aware beyond 25 m
                new Cand { Gap = 9f, Distance = 12f, Held = true });          // held by hysteresis
            Assert.AreEqual(new[] { Show.Full, Show.None, Show.Full, Show.None, Show.Full }, r);
        }

        [Test]
        public void ContextualBudgetRanksAwareFirstThenNearest()
        {
            var r = Pick(
                new Cand { Gap = 3f, Distance = 6f },
                new Cand { Gap = 0f, Distance = 4f },
                new Cand { Gap = 2f, Distance = 5f },
                new Cand { Gap = 1f, Distance = 5f },
                new Cand { Gap = 3.5f, Distance = 7f },
                new Cand { Gap = 15f, Distance = 20f, Aware = true });
            // aware one plus the three nearest unaware ones: the two furthest drop to their near sector
            Assert.AreEqual(new[] { Show.NearOnly, Show.Full, Show.Full, Show.Full, Show.NearOnly, Show.Full }, r);
        }

        [Test]
        public void TimeToContactQualifiesAndRanksAheadOfNearness()
        {
            var r = Pick(
                new Cand { Gap = 1f, Distance = 5f },
                new Cand { Gap = 2f, Distance = 5f },
                new Cand { Gap = 3f, Distance = 6f },
                new Cand { Gap = 3.5f, Distance = 7f },
                new Cand { Gap = 9f, Distance = 14f, Soon = true, ContactIn = 2f });
            // the far guard about to reach her qualifies on contact alone and takes a place ahead of the nearest
            Assert.AreEqual(new[] { Show.Full, Show.Full, Show.Full, Show.NearOnly, Show.Full }, r);
        }

        [Test]
        public void TimeToContactFollowsHisWalkHerWalkAndHisTurn()
        {
            var at = Vector3.zero;
            // she stands 8 m ahead of a 5 m sector: he walks at 1.5 m/s and reaches her in 2 s
            Assert.AreEqual(2f, ConeContext.TimeToContact(at, Vector3.forward * 1.5f, Vector3.forward, 0f, 45f, 5f, new Vector3(0, 0, 8), Vector3.zero), 0.11f);
            // too slow to reach her within 2.5 s
            Assert.IsTrue(float.IsPositiveInfinity(ConeContext.TimeToContact(at, Vector3.forward * 1f, Vector3.forward, 0f, 45f, 5f, new Vector3(0, 0, 8), Vector3.zero)));
            // she walks into a still guard's cone
            Assert.AreEqual(1f, ConeContext.TimeToContact(at, Vector3.zero, Vector3.forward, 0f, 45f, 5f, new Vector3(-6, 0, 3), Vector3.right * 3f), 0.11f);
            // she is beside him, 90° off his facing; he turns toward her at 90°/s and the 45° edge reaches her at 0.5 s
            Assert.AreEqual(0.5f, ConeContext.TimeToContact(at, Vector3.zero, Vector3.forward, 90f, 45f, 5f, new Vector3(3, 0, 0), Vector3.zero), 0.11f);
            // a turn is only carried on for a second: at 20°/s he never swings the 45° to her
            Assert.IsTrue(float.IsPositiveInfinity(ConeContext.TimeToContact(at, Vector3.zero, Vector3.forward, 20f, 45f, 5f, new Vector3(3, 0, 0), Vector3.zero)));
            Assert.AreEqual(0f, ConeContext.TimeToContact(at, Vector3.zero, Vector3.forward, 0f, 45f, 5f, new Vector3(0, 0, 3), Vector3.zero), "already inside");
        }

        [Test]
        public void ContextualAwareAlwaysShowEvenPastBudget()
        {
            var c = new Cand[6];
            for (int i = 0; i < 6; i++) c[i] = new Cand { Gap = 10f, Distance = 12f, Aware = true };
            foreach (var s in Pick(c)) Assert.AreEqual(Show.Full, s);
        }

        [Test]
        public void ContextualNearSectorAloneWhenClose()
        {
            var r = Pick(new Cand { Gap = 6f, Distance = 7f, NearClose = true }, new Cand { Gap = 6f, Distance = 7f });
            Assert.AreEqual(new[] { Show.NearOnly, Show.None }, r);
        }

        // ---- Spotted caption ----

        static DetectionMath.SightCause Cause(DetectionMath.Band b, float d, float light = 0f) => new DetectionMath.SightCause
        {
            Band = b, Distance = d, Light = light, NearRange = 5.5f, Threshold = 0.35f,
        };

        [Test]
        public void ExplainNamesTheBand()
        {
            Assert.AreEqual("Touch range: 1.2 m.", DetectionMath.Explain(Cause(DetectionMath.Band.Peripheral, 1.2f)));
            StringAssert.StartsWith("Near band: 4.1 m (limit 5.5 m).", DetectionMath.Explain(Cause(DetectionMath.Band.Near, 4.1f)));
            var far = Cause(DetectionMath.Band.Far, 9.1f, 0.52f); far.Lamp = "gas lamp";
            Assert.AreEqual("Lit by the gas lamp: light 0.52 (exposed above 0.35), 9.1 m away.", DetectionMath.Explain(far));
            StringAssert.StartsWith("Lit: light", DetectionMath.Explain(Cause(DetectionMath.Band.Far, 9f, 0.4f)));
        }

        [Test]
        public void ExplainOtherSensesAndModifiers()
        {
            var smell = Cause(DetectionMath.Band.None, 3.2f); smell.Smell = true; smell.Running = true;
            Assert.AreEqual("Smelled you: 3.2 m.", DetectionMath.Explain(smell));
            var beam = Cause(DetectionMath.Band.Far, 12f); beam.Searchlight = true;
            StringAssert.StartsWith("Searchlight", DetectionMath.Explain(beam));
            var near = Cause(DetectionMath.Band.Near, 3f); near.Running = true; near.Wary = true;
            StringAssert.EndsWith("Running ×2, wary ×1.25.", DetectionMath.Explain(near));
            near.Hunting = true;
            StringAssert.EndsWith("Running ×2, hunting ×1.6.", DetectionMath.Explain(near));
        }

        // ---- cone ownership (SR.5) ----

        [Test]
        public void TheRisingGuardOwnsTheOverlap()
        {
            var rising = new[] { false, true, true, false };
            var meter = new[] { 0.9f, 0.2f, 0.5f, 0.7f };
            Assert.AreEqual(2, ConeContext.Owner(rising, meter, 4), "the fullest of those rising, not the fullest overall");
        }

        [Test]
        public void NobodyRisingMeansNoOwner()
        {
            Assert.AreEqual(-1, ConeContext.Owner(new[] { false, false }, new[] { 0.9f, 0.4f }, 2));
            Assert.AreEqual(-1, ConeContext.Owner(new bool[0], new float[0], 0));
            // only the first count entries count (the arrays are reused buffers)
            Assert.AreEqual(-1, ConeContext.Owner(new[] { false, true }, new[] { 0f, 1f }, 1));
        }

        [Test]
        public void OwnershipTieGoesToTheFirst()
        {
            Assert.AreEqual(0, ConeContext.Owner(new[] { true, true }, new[] { 0.3f, 0.3f }, 2));
        }

        // ---- pins and difficulty ranges (SR.12) ----

        [Test]
        public void PinsHoldThreeAndDropTheOldest()
        {
            var pins = new List<int>();
            Assert.IsTrue(ConeContext.TogglePin(pins, 1));
            ConeContext.TogglePin(pins, 2);
            ConeContext.TogglePin(pins, 3);
            Assert.IsTrue(ConeContext.TogglePin(pins, 4));
            Assert.AreEqual(new[] { 2, 3, 4 }, pins.ToArray());
            Assert.IsFalse(ConeContext.TogglePin(pins, 3), "clicking a pinned guard unpins him");
            Assert.AreEqual(new[] { 2, 4 }, pins.ToArray());
        }

        [Test]
        public void MercifulCountsSixMetresAsClose()
        {
            var c = new ConeContext.Candidate { Gap = 5f, Distance = 12f };
            Assert.IsFalse(ConeContext.Qualifies(c, Difficulties.Get(Difficulty.Hunter).ConeNear), "Hunter");
            Assert.IsTrue(ConeContext.Qualifies(c, Difficulties.Get(Difficulty.Merciful).ConeNear), "Merciful");
        }

        [Test]
        public void ApexShowsUnawareGuardsNearSectorOnly()
        {
            var c = new List<ConeContext.Candidate>
            {
                new ConeContext.Candidate { Gap = 1f, Distance = 6f },
                new ConeContext.Candidate { Gap = 9f, Distance = 14f, Aware = true },
            };
            var r = new ConeContext.Show[2];
            var apex = Difficulties.Get(Difficulty.Apex);
            Assert.IsTrue(apex.ConesAwareOnly);
            ConeContext.Pick(c, r, apex.ConesAwareOnly ? 0 : ConeContext.Budget, apex.ConeNear);
            Assert.AreEqual(ConeContext.Show.NearOnly, r[0]);
            Assert.AreEqual(ConeContext.Show.Full, r[1]);
        }

        [Test]
        public void OnARoofOnlyAGuardWhoLooksUpCountsHisFarBand()
        {
            Assert.IsTrue(ConeContext.FarReaches(0f, false), "same level");
            Assert.IsTrue(ConeContext.FarReaches(-4.5f, false), "she is below him");
            Assert.IsFalse(ConeContext.FarReaches(4.5f, false), "a roof above a watchman");
            Assert.IsTrue(ConeContext.FarReaches(4.5f, true), "a roof above a hunter");
            Assert.IsTrue(ConeContext.FarReaches(DetectionMath.HighTarget, false), "the threshold itself is not high");
        }
    }
}
