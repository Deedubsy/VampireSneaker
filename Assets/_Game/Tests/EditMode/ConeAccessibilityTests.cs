using NUnit.Framework;
using Vespertine.AI;
using Vespertine.Visual;
using Shape = Vespertine.Visual.ConeRenderer.OriginShape;

namespace Vespertine.Tests
{
    /// <summary>Accessible cones (SR.12): the cone key held or toggled, shape-coded states, doubled edges.</summary>
    public class ConeAccessibilityTests
    {
        [Test]
        public void HeldKeyShowsOnlyWhileHeld()
        {
            bool l = false;
            Assert.IsTrue(ConeRenderer.ConeKey(ref l, true, false, true, true, true));
            Assert.IsFalse(ConeRenderer.ConeKey(ref l, true, false, true, false, false), "released");
            Assert.IsFalse(ConeRenderer.ConeKey(ref l, false, false, true, true, true), "hotkey disabled");
            Assert.IsFalse(ConeRenderer.ConeKey(ref l, true, false, false, true, false), "a menu has the input");
        }

        [Test]
        public void ToggledKeyLatchesOnEachPress()
        {
            bool l = false;
            Assert.IsTrue(ConeRenderer.ConeKey(ref l, true, true, true, true, true), "press: on");
            Assert.IsTrue(ConeRenderer.ConeKey(ref l, true, true, true, false, false), "released: still on");
            Assert.IsFalse(ConeRenderer.ConeKey(ref l, true, true, false, false, false), "hidden under a menu");
            Assert.IsTrue(ConeRenderer.ConeKey(ref l, true, true, true, false, false), "back after the menu");
            Assert.IsFalse(ConeRenderer.ConeKey(ref l, true, true, true, true, true), "press again: off");
            Assert.IsTrue(ConeRenderer.ConeKey(ref l, true, true, true, true, true));
            Assert.IsFalse(ConeRenderer.ConeKey(ref l, true, false, true, false, false), "switching to hold drops the latch");
            Assert.IsFalse(l);
        }

        [Test]
        public void ShapesSeparateUnawareSuspiciousAndKnowing()
        {
            Assert.AreEqual(Shape.Plain, ConeRenderer.OriginShapeOf(NpcState.Relaxed));
            Assert.AreEqual(Shape.Dashed, ConeRenderer.OriginShapeOf(NpcState.Suspicious));
            Assert.AreEqual(Shape.Dashed, ConeRenderer.OriginShapeOf(NpcState.Investigating));
            Assert.AreEqual(Shape.Wide, ConeRenderer.OriginShapeOf(NpcState.Alerted));
            Assert.AreEqual(Shape.Wide, ConeRenderer.OriginShapeOf(NpcState.Searching));
            Assert.AreEqual(Shape.Wide, ConeRenderer.OriginShapeOf(NpcState.Panicked));
        }

        [Test]
        public void HighContrastDoublesEdgesAndThePulseStaysVisible()
        {
            Assert.AreEqual(0.24f, ConeRenderer.EdgeWidth(0.12f, true), 1e-6f);
            Assert.AreEqual(0.12f, ConeRenderer.EdgeWidth(0.12f, false), 1e-6f);
            for (float t = 0f; t < 2f; t += 0.05f)
            {
                float p = ConeRenderer.Pulse(t);
                Assert.GreaterOrEqual(p, 0.7f - 1e-5f); Assert.LessOrEqual(p, 1f + 1e-5f);
            }
        }
    }
}
