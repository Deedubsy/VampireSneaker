using UnityEngine;

namespace Vespertine.Stealth
{
    /// <summary>
    /// The visible knee of a light pool (SR.4, "truthful light"; K34). A down-facing Unity spot lights the ground with
    /// cos³θ / h², so a lamp's pool fades smoothly to an eighth of its centre by the exposure contour and never says where
    /// exposure ends. A radial cookie cancels that: the ground is flat and bright where the light exposes her, falls off
    /// over <see cref="Feather"/> past the contour, then leaves only ambient. Pure; <c>GameLight</c> builds the cookie
    /// and sets the spot from it.
    /// </summary>
    public static class LightKnee
    {
        /// <summary>How far past the exposure contour the drawn light runs before it ends (D132).</summary>
        public const float Feather = 0.3f;
        /// <summary>Ground brightness, relative to full, at the contour and where the light is blazing (0.7).</summary>
        public const float AtContour = 0.75f, Blazing = 0.7f;
        /// <summary>Spot reach past the feather, so the cookie, not the spot's own edge, shapes the fall-off.</summary>
        public const float Margin = 0.35f;

        /// <summary>The brightness the ground should show at flat distance <paramref name="r"/>, 0..1: 0.75..1 inside
        /// the contour <paramref name="contour"/> by how bright the light is there, then a smooth fall to 0 over the
        /// feather. <paramref name="light"/> is the gameplay light level at chest height over that point.</summary>
        public static float Target(float r, float contour, float light, float threshold = DetectionMath.ExposedAt)
        {
            if (r <= contour) return AtContour + (1f - AtContour) * Mathf.Clamp01((light - threshold) / (Blazing - threshold));
            float t = Mathf.Clamp01((r - contour) / Feather);
            return AtContour * (1f - t * t * (3f - 2f * t));
        }

        /// <summary>What a spot at height <paramref name="h"/> with range <paramref name="range"/> lays on flat ground at
        /// flat distance <paramref name="r"/> per unit intensity (URP: inverse square, Lambert, smooth range fade).</summary>
        public static float Ground(float r, float h, float range)
        {
            float d2 = r * r + h * h;
            float cos = h / Mathf.Sqrt(d2);
            float f = d2 / (range * range);
            float smooth = Mathf.Clamp01(1f - f * f);
            return cos / d2 * smooth * smooth;
        }

        /// <summary>The spot's shape for a contour: the ground radius it covers at full strength (its inner angle)
        /// and in all (outer angle), and its range.</summary>
        public static void Shape(float contour, float h, out float inner, out float outer, out float range)
        {
            inner = contour + Feather + Margin;
            outer = inner + 0.3f;
            range = Mathf.Sqrt(outer * outer + h * h) * 2.5f;
        }

        /// <summary>
        /// Fills a <paramref name="size"/>² radial cookie for a spot whose outer half-angle reaches ground radius
        /// <paramref name="outer"/>. <paramref name="lightAt"/> gives the gameplay light at flat distance r. Returns the
        /// scale k: the ground then shows <c>intensity · k · Target</c>, so intensity = full / k.
        /// </summary>
        public static float Cookie(float[] into, int size, float contour, float h, float outer, float range, System.Func<float, float> lightAt)
        {
            float max = 0f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = RadiusAt(x, y, size, outer);
                float g = Ground(r, h, range);
                float v = g > 1e-6f ? Target(r, contour, lightAt(r)) / g : 0f;
                into[y * size + x] = v;
                if (v > max) max = v;
            }
            if (max <= 0f) return 0f;
            for (int i = 0; i < into.Length; i++) into[i] /= max;
            return 1f / max;
        }

        /// <summary>Flat ground radius under a cookie texel's centre. URP maps a spot cookie across the outer cone's
        /// square: the texel at the edge looks along the outer half-angle.</summary>
        public static float RadiusAt(int x, int y, int size, float outer)
        {
            float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
            return Mathf.Sqrt(u * u + v * v) * outer;
        }
    }
}
