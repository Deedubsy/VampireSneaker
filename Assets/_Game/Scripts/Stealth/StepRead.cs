using UnityEngine;

namespace Vespertine.Stealth
{
    /// <summary>
    /// Ilse's ground disc and its toe (SR.7, SR.8). Pure rules; <c>Visual.IlseDisc</c> draws them.
    /// <list type="bullet">
    /// <item>The disc is <b>exposed</b> (filled) when the light the far band judges her by reaches
    /// <see cref="DetectionMath.ExposedAt"/>, else hidden (a hollow ring). One threshold, the one regen and the far band use.</item>
    /// <item>A <b>watcher tick</b> on the rim points at each guard who has her in a band with line of sight, as long as his
    /// meter is full, and shrinks with the meter as it decays.</item>
    /// <item>The rim takes a guard's colour when she is within <see cref="NearWarn"/> of his near sector.</item>
    /// <item>The <b>toe</b> looks <see cref="Ahead"/> s along the move keys: warm when the step exposes her and no cone
    /// covers it, a guard's colour when he would see her there, flashing when it is his near sector or touch range,
    /// where there is no time to react. Nothing when the step changes nothing.</item>
    /// </list>
    /// </summary>
    public static class StepRead
    {
        public const float Ahead = 0.6f, MinAhead = 0.5f, NearWarn = 1f, TickMin = 0.12f, TickMax = 0.62f;

        /// <summary>Worst first: a later value outranks an earlier one.</summary>
        public enum Toe { None, Warm, Seen, Flash }

        public static bool Exposed(float sightLight) => sightLight >= DetectionMath.ExposedAt;

        static bool Close(DetectionMath.Band b) => b == DetectionMath.Band.Near || b == DetectionMath.Band.Peripheral;

        /// <summary>What one guard makes of her next step: <paramref name="now"/> and <paramref name="next"/> are the bands he
        /// has her in (with line of sight) where she stands and where the step lands.</summary>
        public static Toe ForGuard(DetectionMath.Band now, DetectionMath.Band next)
        {
            if (Close(next) && !Close(now)) return Toe.Flash;
            if (next != DetectionMath.Band.None && now == DetectionMath.Band.None) return Toe.Seen;
            return Toe.None;
        }

        /// <summary>The light's part of the toe: stepping from hidden to exposed.</summary>
        public static Toe ForLight(float lightNow, float lightNext) => Exposed(lightNext) && !Exposed(lightNow) ? Toe.Warm : Toe.None;

        public static Toe Worse(Toe a, Toe b) => b > a ? b : a;

        /// <summary>How far ahead the toe looks, in metres, for the speed the keys ask for.</summary>
        public static float Reach(float intentSpeed) => Mathf.Max(MinAhead, intentSpeed * Ahead);

        /// <summary>Watcher tick length (m) for a meter of 0..1.</summary>
        public static float TickLength(float meter) => Mathf.Lerp(TickMin, TickMax, Mathf.Clamp01(meter));

        /// <summary>Outside his near sector but within <see cref="NearWarn"/> of it (flat; the near band ignores height).</summary>
        public static bool NearWarning(in VisionParams v, Vector3 guardFeet, Vector3 forward, Vector3 herFeet)
        {
            float gap = DetectionMath.DistanceToSector(guardFeet, forward, v.HalfAngle, v.NearRange, herFeet);
            return gap > 0f && gap <= NearWarn;
        }
    }
}
