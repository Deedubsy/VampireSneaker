using NUnit.Framework;
using UnityEngine;
using Vespertine.Controls;
using Vespertine.Save;
using Vespertine.View;

namespace Vespertine.Tests
{
    /// <summary>Direct (WASD) control, D114: camera-relative input, speed easing, pushing into climbs and drops,
    /// the follow camera's look-ahead and the settings migration that came with it.</summary>
    public class MoveMathTests
    {
        static void Near(Vector3 want, Vector3 got, string msg = null) =>
            Assert.Less(Vector3.Distance(want, got), 1e-4f, (msg ?? "") + $" want {want}, got {got}");

        [Test]
        public void ForwardFollowsTheCameraYaw()
        {
            Near(Vector3.forward, MoveMath.CameraRelative(new Vector2(0, 1), 0f));
            Near(Vector3.right, MoveMath.CameraRelative(new Vector2(0, 1), 90f));
            Near(Vector3.back, MoveMath.CameraRelative(new Vector2(1, 0), 90f), "right at yaw 90");
            Near(new Vector3(1, 0, 1).normalized, MoveMath.CameraRelative(new Vector2(0, 1), 45f));
        }

        [Test]
        public void DiagonalsAreNotFaster()
        {
            var d = MoveMath.CameraRelative(new Vector2(1, 1), 30f);
            Assert.AreEqual(1f, d.magnitude, 1e-4f);
            Assert.AreEqual(0f, d.y);
        }

        [Test]
        public void GentleStickWalksSlowlyAndDeadZoneIsStill()
        {
            Assert.AreEqual(0.5f, MoveMath.CameraRelative(new Vector2(0, 0.5f), 0f).magnitude, 1e-4f);
            Assert.AreEqual(Vector3.zero, MoveMath.CameraRelative(new Vector2(0.01f, 0.01f), 0f));
        }

        [Test]
        public void EaseStopsQuickerThanItStarts()
        {
            float up = MoveMath.Ease(0f, 5f, 0.05f);
            float down = 5f - MoveMath.Ease(5f, 0f, 0.05f);
            Assert.Greater(up, 0f);
            Assert.Greater(down, up);
            Assert.AreEqual(5f, MoveMath.Ease(4.99f, 5f, 0.1f), 1e-5f, "does not overshoot");
        }

        // a climb: start at the foot of a wall at z=1, top 3 m up and 1 m further on
        static readonly Vector3 Foot = new Vector3(0, 0, 1), Top = new Vector3(0, 3, 2);

        [Test]
        public void PushingIntoAWallTakesIt()
        {
            Assert.IsTrue(MoveMath.PushesInto(new Vector3(0, 0, 0.3f), Vector3.forward, Foot, Top, 0f, out var from, out var to));
            Near(Foot, from); Near(Top, to);
        }

        [Test]
        public void FacingAwayOrAlongDoesNot()
        {
            var at = new Vector3(0, 0, 0.3f);
            Assert.IsFalse(MoveMath.PushesInto(at, Vector3.back, Foot, Top, 0f, out _, out _), "backing away");
            Assert.IsFalse(MoveMath.PushesInto(at, Vector3.right, Foot, Top, 0f, out _, out _), "walking along the wall");
            Assert.IsTrue(MoveMath.PushesInto(at, new Vector3(0.6f, 0, 1f), Foot, Top, 0f, out _, out _), "a slanting push still climbs");
        }

        [Test]
        public void TooFarOrOnAnotherFloorDoesNot()
        {
            Assert.IsFalse(MoveMath.PushesInto(new Vector3(0, 0, -1f), Vector3.forward, Foot, Top, 0f, out _, out _), "2 m short");
            Assert.IsFalse(MoveMath.PushesInto(new Vector3(0, 3, 0.5f), Vector3.forward, Foot, Top, 0f, out _, out _), "on the roof beside it");
            Assert.IsTrue(MoveMath.PushesInto(new Vector3(0, 0, -0.9f), Vector3.forward, Foot, Top, 0f, out _, out _, reach: 2.2f), "a leap reaches further");
        }

        [Test]
        public void WideLinksAreTakenWhereSheStands()
        {
            // a 4 m wide wall: standing 1.5 m to the side she climbs straight up from there, not diagonally to the middle
            Assert.IsTrue(MoveMath.PushesInto(new Vector3(1.5f, 0, 0.4f), Vector3.forward, Foot, Top, 4f, out var from, out var to));
            Assert.AreEqual(1.5f, from.x, 1e-4f);
            Assert.AreEqual(1.5f, to.x, 1e-4f);
            // beyond the edge she is clamped to it, and then too far away
            Assert.IsFalse(MoveMath.PushesInto(new Vector3(4f, 0, 0.4f), Vector3.forward, Foot, Top, 4f, out _, out _));
        }

        [Test]
        public void StraightUpPipeUsesTheWayToIt()
        {
            var pipeFoot = new Vector3(0, 0, 1); var pipeTop = new Vector3(0, 4, 1);
            Assert.IsTrue(MoveMath.PushesInto(new Vector3(0, 0, 0.4f), Vector3.forward, pipeFoot, pipeTop, 0f, out _, out _));
            Assert.IsFalse(MoveMath.PushesInto(new Vector3(0, 0, 0.4f), Vector3.back, pipeFoot, pipeTop, 0f, out _, out _));
        }

        [Test]
        public void LookAheadHoldsWhileStillAndEasesBackOnTheMove()
        {
            var peek = new Vector3(10, 0, 0);
            Assert.AreEqual(peek, TacticalCamera.EasePeek(peek, 0f, false, 0.1f), "standing still keeps the look");
            Assert.AreEqual(peek, TacticalCamera.EasePeek(peek, 4f, true, 0.1f), "still looking");
            var p = peek;
            for (int i = 0; i < 60; i++) p = TacticalCamera.EasePeek(p, 4f, false, 1f / 30f);
            Assert.Less(p.magnitude, 1f, "two seconds of walking brings the view home");
        }

        [Test]
        public void OldSettingsLoseEdgePanAndStaleBindings()
        {
            var s = new SettingsData { Version = 0, EdgePan = true, BindingOverrides = "{\"Nightplan\":\"<Keyboard>/space\"}" };
            s.Migrate();
            Assert.IsFalse(s.EdgePan);
            Assert.AreEqual("", s.BindingOverrides);
            Assert.AreEqual(SettingsData.CurrentVersion, s.Version);
            s.EdgePan = true;
            s.Migrate();
            Assert.IsTrue(s.EdgePan, "a current file keeps the player's choice");
        }

        [Test]
        public void ATapSnapsToTheNext45()
        {
            Assert.AreEqual(90f, TacticalCamera.SnapYaw(45f, 1));
            Assert.AreEqual(90f, TacticalCamera.SnapYaw(50f, 1), "off the grid: to the next line");
            Assert.AreEqual(45f, TacticalCamera.SnapYaw(50f, -1));
            Assert.AreEqual(0f, TacticalCamera.SnapYaw(45f, -1));
            Assert.AreEqual(-45f, TacticalCamera.SnapYaw(0f, -1));
        }

        [Test]
        public void ZOrXTappedSnapsHeldTurns()
        {
            int dir = 0; float held = 0f, y = 45f, dt = 1f / 60f;
            for (int i = 0; i < 6; i++) y = TacticalCamera.RotateStep(y, ref dir, ref held, 1f, 110f, dt);
            Assert.AreEqual(45f, y, "a tap doesn't turn while down");
            y = TacticalCamera.RotateStep(y, ref dir, ref held, 0f, 110f, dt);
            Assert.AreEqual(90f, y, "released: the next 45");
            for (int i = 0; i < 60; i++) y = TacticalCamera.RotateStep(y, ref dir, ref held, -1f, 110f, dt);
            Assert.Less(y, 90f - 80f, "held a second: smooth turn past the tap window");
            float hold = y;
            y = TacticalCamera.RotateStep(y, ref dir, ref held, 0f, 110f, dt);
            Assert.AreEqual(hold, y, "a hold stops where it's let go");
        }

        [Test]
        public void TheCameraLeadsHerUnlessSheCreepsNearAGuard()
        {
            Assert.AreEqual(1.32f, TacticalCamera.LeadFor(new Vector3(2.2f, 0f, 0f), false).x, 1e-4f, "walking: about 1.3 m");
            Assert.AreEqual(TacticalCamera.LeadMax, TacticalCamera.LeadFor(new Vector3(0f, 0f, 20f), false).magnitude, 1e-4f, "capped");
            Assert.AreEqual(Vector3.zero, TacticalCamera.LeadFor(new Vector3(2.2f, 0f, 0f), true), "sneaking near a guard: still");
        }

        [Test]
        public void TheMoveFrameHoldsWhileTheSameKeysAreHeld()
        {
            bool locked = false; Vector2 li = default; float ly = 0f;
            Assert.AreEqual(45f, MoveMath.FrameYaw(ref locked, ref li, ref ly, Vector2.up, 45f), "W pressed: the camera's yaw");
            Assert.AreEqual(45f, MoveMath.FrameYaw(ref locked, ref li, ref ly, Vector2.up, 90f), "camera turned mid-move: W keeps its yaw");
            Assert.AreEqual(90f, MoveMath.FrameYaw(ref locked, ref li, ref ly, new Vector2(1f, 1f).normalized, 90f), "D added: a new direction takes the new yaw");
            Assert.AreEqual(90f, MoveMath.FrameYaw(ref locked, ref li, ref ly, Vector2.zero, 90f));
            Assert.AreEqual(135f, MoveMath.FrameYaw(ref locked, ref li, ref ly, Vector2.up, 135f), "released and pressed again: the camera's yaw");
        }

        [Test]
        public void ADashChargeRefillsOnlyInTheDark()
        {
            int charges = 0;
            float t = MoveMath.DashRecharge(ref charges, 2, 0f, 4f, true);
            Assert.AreEqual(0, charges);
            t = MoveMath.DashRecharge(ref charges, 2, t, 3f, false);
            Assert.AreEqual(0, charges, "light holds the clock");
            Assert.AreEqual(4f, t, 1e-4f, "and doesn't reset it");
            t = MoveMath.DashRecharge(ref charges, 2, t, 1.5f, true);
            Assert.AreEqual(1, charges);
            Assert.AreEqual(0.5f, t, 1e-4f);
            t = MoveMath.DashRecharge(ref charges, 2, t, 20f, true);
            Assert.AreEqual(2, charges, "never past the cap");
            Assert.AreEqual(0f, t);
            charges = 3;
            MoveMath.DashRecharge(ref charges, 2, 0f, 1f, true);
            Assert.AreEqual(2, charges, "a lost Umbral Step trims the third");
        }

        [Test]
        public void TheDashRefusesOnlyForAReason()
        {
            Assert.IsNull(MoveMath.DashProblem(1, false, false, false, false));
            Assert.IsNotNull(MoveMath.DashProblem(0, false, false, false, false), "no charge");
            Assert.IsNotNull(MoveMath.DashProblem(2, false, true, false, false), "carrying");
            Assert.IsNotNull(MoveMath.DashProblem(2, false, false, true, false), "as mist");
            Assert.IsNotNull(MoveMath.DashProblem(2, true, false, false, false), "feeding or climbing");
            Assert.IsNotNull(MoveMath.DashProblem(2, false, false, false, true), "garlic smoke");
        }

        [Test]
        public void TheRiseIsOneStoreyUpInTheDark()
        {
            Assert.IsTrue(MoveMath.DashRises(0f, 3.5f, true));
            Assert.IsTrue(MoveMath.DashRises(0f, MoveMath.RiseHeight, true));
            Assert.IsFalse(MoveMath.DashRises(0f, 3.5f, false), "not in light");
            Assert.IsFalse(MoveMath.DashRises(0f, 6f, true), "not two storeys");
            Assert.IsFalse(MoveMath.DashRises(3.5f, 0f, true), "never down");
        }
    }
}
