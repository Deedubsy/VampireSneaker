using NUnit.Framework;
using UnityEngine;
using Vespertine.Controls;
using Vespertine.Level;
using Vespertine.Save;

namespace Vespertine.Tests
{
    /// <summary>D119: her footsteps by gait (sneak / walk / run), shut doors stopping her steps, and the key remap migration.</summary>
    public class SneakAndDoorTests
    {
        [Test]
        public void SneakingIsSilentOnDryStoneAndWalkingIsHeardClose()
        {
            Assert.AreEqual(0f, MoveMath.StepNoise(Gait.Sneak, false, false));
            float walk = MoveMath.StepNoise(Gait.Walk, false, false), run = MoveMath.StepNoise(Gait.Run, false, false);
            Assert.Greater(walk, 2f, "a walk is heard by someone at arm's length and a little more");
            Assert.Less(walk, 5f, "but not across a room");
            Assert.Greater(run, walk * 2f, "a run carries across a room");
        }

        [Test]
        public void WaterAndABodyMakeEvenASneakAudible()
        {
            Assert.Greater(MoveMath.StepNoise(Gait.Sneak, true, false), 0f, "wading splashes");
            Assert.Greater(MoveMath.StepNoise(Gait.Sneak, false, true), 0f, "a body over the shoulder thumps");
            Assert.Less(MoveMath.StepNoise(Gait.Sneak, true, false), MoveMath.StepNoise(Gait.Walk, false, false), "a careful wade is still quieter than a walk");
            Assert.GreaterOrEqual(MoveMath.StepNoise(Gait.Walk, true, false), MoveMath.StepNoise(Gait.Walk, false, false));
            Assert.GreaterOrEqual(MoveMath.StepNoise(Gait.Run, false, true), MoveMath.StepNoise(Gait.Run, false, false), "carrying never quiets a run");
        }

        [Test]
        public void SlowerGaitsStepLessOften()
        {
            Assert.Greater(MoveMath.StepInterval(Gait.Sneak), MoveMath.StepInterval(Gait.Walk));
            Assert.Greater(MoveMath.StepInterval(Gait.Walk), MoveMath.StepInterval(Gait.Run));
        }

        // a door in a N-S wall: the way through runs east-west, the panel spans north-south
        static readonly Vector3 Pass = Vector3.right, Span = Vector3.forward;

        [Test]
        public void AShutDoorStopsAStepIntoIt()
        {
            var step = Door.BlockStep(new Vector3(-0.6f, 0, 0), new Vector3(0.3f, 0, 0), Pass, Span);
            Assert.AreEqual(0f, step.x, 1e-5f);
        }

        [Test]
        public void SheSlidesAlongTheDoorAndCanBackAway()
        {
            var step = Door.BlockStep(new Vector3(-0.5f, 0, 0), new Vector3(0.2f, 0, 0.1f), Pass, Span);
            Assert.AreEqual(0f, step.x, 1e-5f);
            Assert.AreEqual(0.1f, step.z, 1e-5f, "the part along the panel is kept");
            var back = new Vector3(-0.2f, 0, 0);
            Assert.AreEqual(back, Door.BlockStep(new Vector3(-0.3f, 0, 0), back, Pass, Span), "inside the slab (shut on her) she can still step out");
        }

        [Test]
        public void StepsWellClearOfTheDoorAreUntouched()
        {
            var step = new Vector3(0.1f, 0, 0);
            Assert.AreEqual(step, Door.BlockStep(new Vector3(-1.2f, 0, 0), step, Pass, Span), "a pace short of it");
            Assert.AreEqual(step, Door.BlockStep(new Vector3(-0.5f, 0, 1.6f), step, Pass, Span), "beside the frame, in the wall's own run");
            Assert.AreEqual(step, Door.BlockStep(new Vector3(-0.5f, 3f, 0), step, Pass, Span), "a floor above");
            var along = new Vector3(0, 0, 0.2f);
            Assert.AreEqual(along, Door.BlockStep(new Vector3(-0.6f, 0, -0.4f), along, Pass, Span), "walking past it along the wall");
        }

        [Test]
        public void SettingsFromBeforeTheRemapDropTheirBindings()
        {
            var s = new SettingsData { Version = 1, BindingOverrides = "{\"Interact\":\"<Keyboard>/g\"}" };
            s.Migrate();
            Assert.AreEqual("", s.BindingOverrides, "G was Interact before D119; E is now, and Ctrl sneaks");
            Assert.AreEqual(SettingsData.CurrentVersion, s.Version);
            s.BindingOverrides = "{\"Sneak\":\"<Keyboard>/c\"}";
            s.Migrate();
            Assert.AreNotEqual("", s.BindingOverrides, "a rebinding made since is kept");
        }
    }
}
