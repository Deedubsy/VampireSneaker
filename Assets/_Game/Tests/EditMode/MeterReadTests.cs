using NUnit.Framework;
using UnityEngine;
using Vespertine.Stealth;
using Band = Vespertine.Stealth.DetectionMath.Band;
using Feed = Vespertine.Stealth.MeterRead.Feed;
using Glint = Vespertine.Stealth.MeterRead.Glint;

namespace Vespertine.Tests
{
    /// <summary>How a detection meter reads (SR.9, SR.11): its pattern, the glint when it starts, and the edge pip.</summary>
    public class MeterReadTests
    {
        static DetectionMath.SightCause Cause(Band b, bool smell = false, bool searchlight = false) =>
            new DetectionMath.SightCause { Band = b, Smell = smell, Searchlight = searchlight };

        [TestCase(Band.Near, Feed.Near)]
        [TestCase(Band.Peripheral, Feed.Near)]
        [TestCase(Band.Far, Feed.Far)]
        [TestCase(Band.None, Feed.None)]
        public void TheFillShowsTheBandThatSawHer(Band b, Feed expected)
        {
            Assert.AreEqual(expected, MeterRead.FeedOf(Cause(b), 0.1f, float.PositiveInfinity));
        }

        [Test]
        public void SmellAndSearchlightOutrankTheBand()
        {
            Assert.AreEqual(Feed.Smelled, MeterRead.FeedOf(Cause(Band.None, smell: true), 0.1f, float.PositiveInfinity));
            Assert.AreEqual(Feed.Far, MeterRead.FeedOf(Cause(Band.Near, searchlight: true), 0.1f, float.PositiveInfinity), "a searchlight reads as the far band");
        }

        [Test]
        public void TheLatestSenseWins()
        {
            Assert.AreEqual(Feed.Heard, MeterRead.FeedOf(Cause(Band.Near), 2f, 0.5f), "heard since he last saw her");
            Assert.AreEqual(Feed.Near, MeterRead.FeedOf(Cause(Band.Near), 0.5f, 2f), "seen since he last heard her");
            Assert.AreEqual(Feed.Heard, MeterRead.FeedOf(Cause(Band.None), float.PositiveInfinity, 3f), "only ever heard");
            Assert.AreEqual(Feed.None, MeterRead.FeedOf(Cause(Band.Far), float.PositiveInfinity, float.PositiveInfinity), "never sensed");
        }

        [TestCase(Band.Near, Glint.NearArc)]
        [TestCase(Band.Peripheral, Glint.TouchCircle)]
        [TestCase(Band.Far, Glint.LightRim)]
        [TestCase(Band.None, Glint.None)]
        public void TheGlintIsTheEdgeSheIsPast(Band b, Glint expected)
        {
            Assert.AreEqual(expected, MeterRead.GlintOf(Cause(b)));
        }

        [Test]
        public void NoEdgeGlintsForSensesWithoutOne()
        {
            Assert.AreEqual(Glint.None, MeterRead.GlintOf(Cause(Band.Far, smell: true)));
            Assert.AreEqual(Glint.None, MeterRead.GlintOf(Cause(Band.Far, searchlight: true)));
        }

        [Test]
        public void AMeterStartsOnceWhereTheHuhPlays()
        {
            Assert.IsTrue(MeterRead.Started(0f, MeterRead.StartAt));
            Assert.IsTrue(MeterRead.Started(0.04f, 0.3f));
            Assert.IsFalse(MeterRead.Started(MeterRead.StartAt, 0.3f), "already started");
            Assert.IsFalse(MeterRead.Started(0f, 0.04f));
            Assert.IsFalse(MeterRead.Started(0.5f, 0.2f), "falling");
        }

        [Test]
        public void EdgePipsForAwareGuardsNearbyPinnedOrTactical()
        {
            Assert.IsTrue(MeterRead.EdgePip(MeterRead.PipAware, true, false, false));
            Assert.IsFalse(MeterRead.EdgePip(MeterRead.PipAware + 1f, true, false, false), "aware but far");
            Assert.IsFalse(MeterRead.EdgePip(5f, false, false, false), "relaxed with no meter: not without Alt");
            Assert.IsTrue(MeterRead.EdgePip(MeterRead.PipAll, false, false, true), "Alt shows everyone in range");
            Assert.IsFalse(MeterRead.EdgePip(MeterRead.PipAll + 1f, true, false, true));
            Assert.IsTrue(MeterRead.EdgePip(200f, false, true, false), "pinned at any range");
            Assert.IsTrue(MeterRead.EdgePip(60f, false, false, false, true), "a cone reaching her within 2.5 s");
        }

        [Test]
        public void TheEdgePointSitsOnTheMarginTowardTheGuard()
        {
            const float W = 1920, H = 1080, M = 50;
            // off the right edge, level with the centre
            var p = MeterRead.EdgePoint(new Vector3(3000, 540, 10), W, H, M, out var dir);
            Assert.AreEqual(W - M, p.x, 1e-3f); Assert.AreEqual(540f, p.y, 1e-3f);
            Assert.AreEqual(1f, dir.x, 1e-4f);
            // off a corner: it stays on the line from the centre and inside the margin
            p = MeterRead.EdgePoint(new Vector3(-4000, -4000, 10), W, H, M, out dir);
            Assert.IsTrue(p.x >= M - 1e-3f && p.y >= M - 1e-3f);
            Assert.IsTrue(Mathf.Approximately(p.x, M) || Mathf.Approximately(p.y, M), "on the margin");
            var c = new Vector2(W / 2, H / 2);
            Assert.AreEqual(0f, Vector2.SignedAngle(p - c, new Vector2(-4000, -4000) - c), 0.01f);
        }

        [Test]
        public void ABehindTheCameraPointIsMirrored()
        {
            // behind the camera the projection flips: a guard behind and to the left projects to the right
            var p = MeterRead.EdgePoint(new Vector3(1500, 540, -5), 1920, 1080, 50, out var dir);
            Assert.Less(p.x, 960f);
            Assert.Less(dir.x, 0f);
        }
    }
}
