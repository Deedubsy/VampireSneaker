using UnityEngine;

namespace Vespertine.Controls
{
    /// <summary>
    /// Direct (WASD) movement maths, kept pure so it can be tested: turning stick/key input into a world direction
    /// relative to the camera, easing speed, and deciding when a push into an off-mesh link starts a climb or drop.
    /// </summary>
    /// <summary>How she is moving: sneaking (Ctrl), walking, or running (Shift). D119.</summary>
    public enum Gait { Sneak, Walk, Run }

    public static class MoveMath
    {
        /// <summary>
        /// How far one of her footsteps carries (D119): a sneak is silent on stone, a walk is heard by anyone close,
        /// a run is heard across a room. Water splashes whatever the gait; a body over the shoulder thumps. Pure; unit-tested.
        /// </summary>
        public static float StepNoise(Gait g, bool water, bool carrying)
        {
            float r = g == Gait.Run ? 8f : g == Gait.Walk ? 3.2f : 0f;
            if (water) r = Mathf.Max(r * 1.25f, g == Gait.Sneak ? 1.5f : 4.5f);
            if (carrying) r = Mathf.Max(r, g == Gait.Sneak ? 1.2f : 3.5f);
            return r;
        }

        /// <summary>Seconds between footsteps at a gait.</summary>
        public static float StepInterval(Gait g) => g == Gait.Run ? 0.3f : g == Gait.Walk ? 0.45f : 0.6f;

        /// <summary>Key/stick input (x = right, y = forward) as a flat world direction seen from a camera at <paramref name="yaw"/>.
        /// Magnitude is kept (0..1) so a gentle stick walks slowly; diagonals on keys are not faster than straight lines.</summary>
        public static Vector3 CameraRelative(Vector2 input, float yaw)
        {
            if (input.sqrMagnitude < 0.0004f) return Vector3.zero;
            if (input.sqrMagnitude > 1f) input.Normalize();
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var v = rot * Vector3.right * input.x + rot * Vector3.forward * input.y;
            v.y = 0f;
            return v;
        }

        /// <summary>QW11, §10: the move keys keep the camera yaw they started with. A new yaw is taken only when the input
        /// is released or turns more than this many degrees, so rotating the camera mid-move doesn't swing her path.</summary>
        public const float FrameRelock = 15f;

        /// <summary>The yaw to read <paramref name="input"/> against: the locked one while the same input is held, else the
        /// camera's (which becomes the new lock).</summary>
        public static float FrameYaw(ref bool locked, ref Vector2 lockInput, ref float lockYaw, Vector2 input, float camYaw)
        {
            if (input.sqrMagnitude < 0.0004f) { locked = false; return camYaw; }
            if (!locked || Vector2.Angle(input, lockInput) > FrameRelock) { locked = true; lockInput = input; lockYaw = camYaw; }
            return lockYaw;
        }

        /// <summary>Speed eased toward <paramref name="target"/>: quick to start, quicker to stop (stealth wants crisp halts).</summary>
        public static float Ease(float current, float target, float dt, float accel = 28f, float decel = 40f)
            => Mathf.MoveTowards(current, target, (target > current ? accel : decel) * dt);

        /// <summary>
        /// Whether a character at <paramref name="feet"/> pushing in <paramref name="dir"/> is pushing into a link that runs
        /// from <paramref name="start"/> to <paramref name="end"/> (a wide link of <paramref name="width"/> is entered anywhere
        /// along its edge). On success gives the actual entry and exit points for this character.
        /// </summary>
        public static bool PushesInto(Vector3 feet, Vector3 dir, Vector3 start, Vector3 end, float width, out Vector3 from, out Vector3 to,
            float reach = 1.1f, float minDot = 0.55f)
        {
            from = start; to = end;
            var flatDir = new Vector3(dir.x, 0f, dir.z);
            if (flatDir.sqrMagnitude < 0.01f) return false;
            flatDir.Normalize();
            var span = new Vector3(end.x - start.x, 0f, end.z - start.z);
            if (width > 0f && span.sqrMagnitude > 0.0001f)
            {
                var axis = Vector3.Cross(Vector3.up, span.normalized);
                float off = Mathf.Clamp(Vector3.Dot(feet - start, axis), -width * 0.5f, width * 0.5f);
                from += axis * off; to += axis * off;
            }
            if (Mathf.Abs(feet.y - from.y) > 0.9f) return false;
            var toStart = new Vector3(from.x - feet.x, 0f, from.z - feet.z);
            if (toStart.magnitude > reach) return false;
            // the link's own heading; a straight-up pipe falls back to "towards the entry point"
            var heading = span.sqrMagnitude > 0.04f ? span.normalized : toStart.sqrMagnitude > 0.01f ? toStart.normalized : flatDir;
            return Vector3.Dot(flatDir, heading) >= minDot;
        }

        // ---------------------------------------------------------------- Shadow Dash (D155)
        public const float DashDistance = 6f, DashTime = 0.2f, DashRechargeTime = 5f, DashBlur = 0.3f, RiseHeight = 4f, UmbralTime = 1f;
        public const int DashCharges = 2;

        /// <summary>Light is the cooldown: one charge refills every <see cref="DashRechargeTime"/> s, and only while she is
        /// in the dark (the clock holds in light, it doesn't reset). Returns the progress toward the next charge. Pure; unit-tested.</summary>
        public static float DashRecharge(ref int charges, int max, float progress, float dt, bool dark)
        {
            if (charges >= max) { charges = max; return 0f; }
            if (!dark) return progress;
            progress += dt;
            while (progress >= DashRechargeTime && charges < max) { progress -= DashRechargeTime; charges++; }
            return charges >= max ? 0f : progress;
        }

        /// <summary>Why she can't dash now, or null. Pure; unit-tested.</summary>
        public static string DashProblem(int charges, bool busy, bool carrying, bool mist, bool censer)
        {
            if (carrying) return "Not while carrying";
            if (mist) return "Not as mist";
            if (busy) return "Busy";
            if (censer) return "Garlic smoke";
            if (charges <= 0) return "No dash left - find the dark";
            return null;
        }

        /// <summary>Whether a dash into a climb carries her up (the Rise): only in the dark, only one storey (up to
        /// <see cref="RiseHeight"/> m), only upward.</summary>
        public static bool DashRises(float fromY, float toY, bool dark) => dark && toY - fromY > 0.5f && toY - fromY <= RiseHeight;
    }
}
