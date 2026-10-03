using NUnit.Framework;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Level;

namespace Vespertine.Tests
{
    /// <summary>C18: NPC footfalls pick a real clip family for each surface, at a believable cadence.</summary>
    public class FootstepTests
    {
        [Test]
        public void EverySurfaceHasAClipFamilyForHumans()
        {
            Assert.AreEqual("step_stone", Npc.StepClip(Surface.Stone, false));
            Assert.AreEqual("step_wood", Npc.StepClip(Surface.Wood, false));
            Assert.AreEqual("step_water", Npc.StepClip(Surface.Water, false));
            Assert.AreEqual("step_dirt", Npc.StepClip(Surface.Dirt, false));
            Assert.AreEqual("step_dirt", Npc.StepClip(Surface.Carpet, false));
        }

        [Test]
        public void HoundsPadSoftlyAndAreSilentOnCarpet()
        {
            Assert.AreEqual("step_dirt", Npc.StepClip(Surface.Stone, true));
            Assert.IsNull(Npc.StepClip(Surface.Carpet, true));
        }

        [Test]
        public void RunnersStepFasterThanWalkersAndHoundsFastest()
        {
            float walk = Npc.StepInterval(1.2f, false), run = Npc.StepInterval(3.5f, false), hound = Npc.StepInterval(1.2f, true);
            Assert.Less(run, walk);
            Assert.Less(hound, walk);
            Assert.That(walk, Is.InRange(0.4f, 0.7f));
            Assert.That(run, Is.InRange(0.25f, 0.4f));
        }

        [Test]
        public void EveryClipTheGameAsksForExists()
        {
            // the clips are synthesised as <family>0..2; a bare family name is not a clip (the old step_water bug)
            var bank = Vespertine.Audio.Synth.BuildAll();
            foreach (Surface s in System.Enum.GetValues(typeof(Surface)))
                foreach (bool quad in new[] { false, true })
                {
                    var f = Npc.StepClip(s, quad);
                    if (f == null) continue;
                    for (int v = 0; v < 3; v++) Assert.IsTrue(bank.ContainsKey(f + v), f + v);
                }
            foreach (var k in new[] { "amb_fire", "amb_night", "amb_drip", "ignite", "chain", "step_water0" })
                Assert.IsTrue(bank.ContainsKey(k), k);
            foreach (var c in bank.Values) Object.DestroyImmediate(c);
        }
    }
}
