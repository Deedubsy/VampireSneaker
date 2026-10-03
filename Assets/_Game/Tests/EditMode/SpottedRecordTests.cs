using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Mission;

namespace Vespertine.Tests
{
    /// <summary>The debrief's Detections page (SR.10): the record's rays, its bounds and the sketch mapping.</summary>
    public class SpottedRecordTests
    {
        static SpottedRecord Rec(Vector2 guard, Vector2 her, Vector2 facing, float far = 10f)
        {
            var r = new SpottedRecord { Guard = guard, Her = her, Facing = facing.normalized, Half = 45f, Near = 6f, Far = far, Touch = 1.5f, ConeR = new float[SpottedRecord.Rays + 1] };
            for (int i = 0; i <= SpottedRecord.Rays; i++) r.ConeR[i] = far;
            return r;
        }

        [Test]
        public void TheRaysSpanTheConeAboutHisFacing()
        {
            var r = Rec(Vector2.zero, Vector2.one, Vector2.right);
            Assert.AreEqual(-45f, Vector2.SignedAngle(Vector2.right, r.RayDir(0)), 1e-3f);
            Assert.AreEqual(45f, Vector2.SignedAngle(Vector2.right, r.RayDir(SpottedRecord.Rays)), 1e-3f);
            Assert.AreEqual(0f, Vector2.SignedAngle(Vector2.right, r.RayDir(SpottedRecord.Rays / 2)), 1e-3f, "the middle ray is his facing");
            Assert.AreEqual(1f, r.RayDir(7).magnitude, 1e-5f);
        }

        [Test]
        public void TheBoundsHoldHimHerTheConeAndTheLamp()
        {
            var r = Rec(new Vector2(10, 10), new Vector2(14, 10), Vector2.right);
            var b = r.Bounds();
            Assert.IsTrue(b.Contains(r.Guard) && b.Contains(r.Her));
            Assert.AreEqual(20f, b.xMax, 1e-3f, "the cone's far reach along his facing");
            Assert.AreEqual(10f - 1.5f, b.xMin, 1e-3f, "his touch circle behind him");
            r.HasLamp = true; r.Lamp = new Vector2(0, 0); r.LampR = 3f;
            b = r.Bounds();
            Assert.AreEqual(-3f, b.xMin, 1e-3f); Assert.AreEqual(-3f, b.yMin, 1e-3f);
        }

        [Test]
        public void WallsShrinkTheBounds()
        {
            var r = Rec(Vector2.zero, new Vector2(2, 0), Vector2.right);
            for (int i = 0; i <= SpottedRecord.Rays; i++) r.ConeR[i] = 3f;
            Assert.AreEqual(3f, r.Bounds().xMax, 1e-3f);
        }

        [Test]
        public void FitKeepsTheMarginAndTheCap()
        {
            var world = new Rect(0, 0, 20, 10);
            float s = SpottedRecord.Fit(world, 260, 190, 16, 100);
            Assert.AreEqual((260f - 32f) / 20f, s, 1e-4f, "width-limited");
            Assert.AreEqual(22f, SpottedRecord.Fit(new Rect(0, 0, 1, 1), 260, 190, 16, 22), 1e-4f, "never closer than the cap");
            Assert.IsFalse(float.IsInfinity(SpottedRecord.Fit(new Rect(0, 0, 0, 0), 260, 190, 16, 1000)), "a zero-size area is safe");
        }

        [Test]
        public void MapPutsNorthUpAndTheCentreInTheMiddle()
        {
            var c = new Vector2(5, 5);
            Assert.AreEqual(new Vector2(130, 95), SpottedRecord.Map(c, c, 10, 260, 190));
            var north = SpottedRecord.Map(c + Vector2.up, c, 10, 260, 190);
            Assert.AreEqual(85f, north.y, 1e-4f, "+z is up the sketch (smaller y)");
            var east = SpottedRecord.Map(c + Vector2.right, c, 10, 260, 190);
            Assert.AreEqual(140f, east.x, 1e-4f);
        }

        [Test]
        public void KeepStopsAtTheCap()
        {
            var list = new List<SpottedRecord>();
            for (int i = 0; i < SpottedRecord.Max + 5; i++) SpottedRecord.Keep(list, new SpottedRecord { Time = i });
            Assert.AreEqual(SpottedRecord.Max, list.Count);
            Assert.AreEqual(0f, list[0].Time, "the first ones are kept");
        }
    }
}
