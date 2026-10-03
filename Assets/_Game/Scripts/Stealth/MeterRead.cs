using UnityEngine;

namespace Vespertine.Stealth
{
    /// <summary>
    /// How a detection meter reads (SR.9, SR.11). Pure; the HUD and <c>Visual.BoundaryGlint</c> draw it.
    /// <list type="bullet">
    /// <item>Its fill shows the sense that is feeding it: <see cref="Feed.Near"/> solid (near band or touch),
    /// <see cref="Feed.Far"/> striped (lit far band or a searchlight), <see cref="Feed.Heard"/> waves,
    /// <see cref="Feed.Smelled"/> dots. The player learns why while it fills.</item>
    /// <item>When a meter starts (crosses <see cref="StartAt"/>, where the "huh" plays) the edge she is past glints:
    /// <see cref="GlintOf"/>.</item>
    /// <item>A guard off screen gets an edge pip: <see cref="EdgePip"/>.</item>
    /// </list>
    /// </summary>
    public static class MeterRead
    {
        public const float StartAt = 0.05f, GlintTime = 0.3f, PipAware = 25f, PipAll = 40f;
        /// <summary>How long the Spotted picture and caption stay (SR.10), unscaled seconds.</summary>
        public const float PictureTime = 4f;

        public enum Feed { None, Near, Far, Heard, Smelled }

        /// <summary>The sense behind the meter: the last thing that raised it. A noise counts when it came after the last
        /// sighting (or there was none); <paramref name="sinceHeard"/> and <paramref name="sinceSeen"/> are seconds.</summary>
        public static Feed FeedOf(in DetectionMath.SightCause last, float sinceSeen, float sinceHeard)
        {
            if (sinceHeard < sinceSeen) return Feed.Heard;
            if (sinceSeen > 1e5f) return Feed.None;
            if (last.Smell) return Feed.Smelled;
            if (last.Searchlight) return Feed.Far;
            switch (last.Band)
            {
                case DetectionMath.Band.Near: case DetectionMath.Band.Peripheral: return Feed.Near;
                case DetectionMath.Band.Far: return Feed.Far;
                default: return Feed.None;
            }
        }

        /// <summary>The edge that glints when a meter starts.</summary>
        public enum Glint { None, NearArc, TouchCircle, LightRim }

        public static Glint GlintOf(in DetectionMath.SightCause c)
        {
            if (c.Smell || c.Searchlight) return Glint.None;
            switch (c.Band)
            {
                case DetectionMath.Band.Near: return Glint.NearArc;
                case DetectionMath.Band.Peripheral: return Glint.TouchCircle;
                case DetectionMath.Band.Far: return Glint.LightRim;
                default: return Glint.None;
            }
        }

        public static bool Started(float before, float after) => before < StartAt && after >= StartAt;

        /// <summary>Whether a guard off screen gets an edge pip: aware of her (a meter, or not relaxed) within
        /// <see cref="PipAware"/> m, his cone reaching her within 2.5 s (<paramref name="soon"/>,
        /// <see cref="ConeContext.TimeToContact"/>), pinned at any range, or anyone within <see cref="PipAll"/> m while
        /// Alt is held.</summary>
        public static bool EdgePip(float distance, bool aware, bool pinned, bool tactical, bool soon = false) =>
            pinned || soon || aware && distance <= PipAware || tactical && distance <= PipAll;

        /// <summary>Where an off-screen point's pip sits: the screen point pushed along the line from the screen centre
        /// until it is inside the margin. A point behind the camera is mirrored first. Returns the pip position and the
        /// unit direction it points (screen space, y up).</summary>
        public static Vector2 EdgePoint(Vector3 screen, float width, float height, float margin, out Vector2 dir)
        {
            var c = new Vector2(width * 0.5f, height * 0.5f);
            var d = new Vector2(screen.x, screen.y) - c;
            if (screen.z < 0f) d = -d;
            if (d.sqrMagnitude < 1e-4f) d = Vector2.down;
            dir = d.normalized;
            float hx = Mathf.Max(1f, c.x - margin), hy = Mathf.Max(1f, c.y - margin);
            float k = Mathf.Min(hx / Mathf.Max(1e-4f, Mathf.Abs(d.x)), hy / Mathf.Max(1e-4f, Mathf.Abs(d.y)));
            return c + d * k;
        }
    }
}
