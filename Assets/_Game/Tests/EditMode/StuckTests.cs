using NUnit.Framework;
using Vespertine.AI;

namespace Vespertine.Tests
{
    /// <summary>Stuck detection must not depend on the frame rate (a walking lamplighter gave up at 144 fps).</summary>
    public class StuckTests
    {
        [TestCase(30f), TestCase(60f), TestCase(144f), TestCase(500f)]
        public void AWalkIsNeverStuck(float fps)
        {
            float dt = 1f / fps;
            Assert.IsFalse(Npc.Crawling(1.1f * dt, dt), "slowest walk");
            Assert.IsFalse(Npc.Crawling(1.1f * 0.35f * 0.85f * dt, dt), "slowest walk, slowed by Dread, at a squad leader's pace");
        }

        [TestCase(30f), TestCase(60f), TestCase(144f), TestCase(500f)]
        public void StandingStillIsStuck(float fps)
        {
            float dt = 1f / fps;
            Assert.IsTrue(Npc.Crawling(0f, dt));
            Assert.IsTrue(Npc.Crawling(0.05f * dt, dt), "jittering against a wall");
        }
    }
}
