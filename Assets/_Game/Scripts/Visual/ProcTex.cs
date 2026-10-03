using UnityEngine;
using Vespertine.Core;

namespace Vespertine.Visual
{
    /// <summary>Procedurally generated tileable textures (placeholder art, "Ink &amp; Ember" palette).</summary>
    public static class ProcTex
    {
        const int N = 128;

        static Texture2D New(string name)
        {
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            return t;
        }

        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Util.Hash(Mod(x0, period), Mod(y0, period), seed), b = Util.Hash(Mod(x0 + 1, period), Mod(y0, period), seed);
            float c = Util.Hash(Mod(x0, period), Mod(y0 + 1, period), seed), d = Util.Hash(Mod(x0 + 1, period), Mod(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        /// <summary>Tileable fractal noise in [0,1].</summary>
        public static float Fbm(float u, float v, int baseFreq, int seed, int oct = 4)
        {
            float sum = 0, amp = 0.5f, norm = 0; int f = baseFreq;
            for (int o = 0; o < oct; o++)
            {
                sum += ValueNoise(u * f, v * f, f, seed + o * 17) * amp;
                norm += amp; amp *= 0.5f; f *= 2;
            }
            return sum / norm;
        }

        static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1);

        public static Texture2D Cobble(Color baseCol, int seed = 1)
        {
            var t = New("cobble");
            const int cells = 6;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (float)x / N * cells, v = (float)y / N * cells;
                    int cx = Mathf.FloorToInt(u), cy = Mathf.FloorToInt(v);
                    float d1 = 9, d2 = 9; float id = 0;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int gx = cx + ox, gy = cy + oy;
                            float jx = gx + 0.2f + 0.6f * Util.Hash(Mod(gx, cells), Mod(gy, cells), seed);
                            float jy = gy + 0.2f + 0.6f * Util.Hash(Mod(gx, cells), Mod(gy, cells), seed + 5);
                            float d = (u - jx) * (u - jx) + (v - jy) * (v - jy);
                            if (d < d1) { d2 = d1; d1 = d; id = Util.Hash(Mod(gx, cells), Mod(gy, cells), seed + 9); }
                            else if (d < d2) d2 = d;
                        }
                    float edge = Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
                    float gap = Mathf.SmoothStep(0, 1, edge * 6f);
                    float n = Fbm((float)x / N, (float)y / N, 8, seed);
                    float k = Mathf.Lerp(0.35f, 0.85f + id * 0.35f, gap) * (0.85f + n * 0.3f);
                    px[y * N + x] = Shade(baseCol, k);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        public static Texture2D Planks(Color baseCol, int planks = 5, int seed = 2)
        {
            var t = New("planks");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = (float)y / N * planks; int row = Mathf.FloorToInt(v); float fv = v - row;
                    float offset = Util.Hash(row, 0, seed) ;
                    float u = ((float)x / N + offset) % 1f;
                    float seam = (fv < 0.06f || fv > 0.94f) ? 0.45f : 1f;
                    float endSeam = Mathf.Abs(u - 0.5f) < 0.008f && Util.Hash(row, 1, seed) > 0.4f ? 0.5f : 1f;
                    float grain = 0.8f + 0.25f * Mathf.Sin((u * 40f + Fbm(u, fv, 4, seed + row) * 6f));
                    float tone = 0.8f + Util.Hash(row, 3, seed) * 0.35f;
                    px[y * N + x] = Shade(baseCol, seam * endSeam * grain * tone);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        public static Texture2D Tiles(Color a, Color b, int count = 2)
        {
            var t = New("tiles");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (float)x / N * count, v = (float)y / N * count;
                    int cx = Mathf.FloorToInt(u), cy = Mathf.FloorToInt(v);
                    float fu = u - cx, fv = v - cy;
                    bool grout = fu < 0.03f || fv < 0.03f || fu > 0.97f || fv > 0.97f;
                    var c = ((cx + cy) & 1) == 0 ? a : b;
                    float n = 0.9f + Fbm((float)x / N, (float)y / N, 8, 3) * 0.2f;
                    px[y * N + x] = grout ? Shade(c, 0.45f) : Shade(c, n);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        public static Texture2D Brick(Color baseCol, int rows = 8, int cols = 4, int seed = 4)
        {
            var t = New("brick");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = (float)y / N * rows; int row = Mathf.FloorToInt(v); float fv = v - row;
                    float u = (float)x / N * cols + ((row & 1) == 0 ? 0f : 0.5f); int col = Mathf.FloorToInt(u); float fu = u - col;
                    bool mortar = fv < 0.08f || fu < 0.04f;
                    float tone = 0.75f + Util.Hash(Mod(col, cols), row, seed) * 0.4f;
                    float n = 0.85f + Fbm((float)x / N, (float)y / N, 8, seed) * 0.3f;
                    px[y * N + x] = mortar ? Shade(baseCol, 0.5f) : Shade(baseCol, tone * n);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        public static Texture2D Slate(Color baseCol, int seed = 6)
        {
            var t = New("slate");
            var px = new Color[N * N];
            const int rows = 6, cols = 5;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = (float)y / N * rows; int row = Mathf.FloorToInt(v); float fv = v - row;
                    float u = (float)x / N * cols + ((row & 1) == 0 ? 0f : 0.5f); int col = Mathf.FloorToInt(u); float fu = u - col;
                    float k = Mathf.Lerp(0.55f, 1f, fv) * (fu < 0.04f ? 0.6f : 1f);
                    k *= 0.8f + Util.Hash(Mod(col, cols), row, seed) * 0.3f;
                    px[y * N + x] = Shade(baseCol, k);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        public static Texture2D Noise(Color baseCol, float contrast = 0.4f, int freq = 6, int seed = 7)
        {
            var t = New("noise");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float n = Fbm((float)x / N, (float)y / N, freq, seed, 5);
                    px[y * N + x] = Shade(baseCol, 1f - contrast * 0.5f + n * contrast);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>Soft radial blob (alpha) for stains, glows and ground markers.</summary>
        public static Texture2D Radial(string name, float hardness = 0.3f, bool splotchy = false)
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (splotchy) d += (Fbm(x / 64f, y / 64f, 4, 11, 3) - 0.5f) * 0.7f;
                    float a = Mathf.Clamp01((1f - d) / Mathf.Max(0.01f, 1f - hardness));
                    px[y * 64 + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>Ring texture (alpha) for selection and noise rings.</summary>
        public static Texture2D Ring(float inner = 0.82f)
        {
            var t = new Texture2D(128, 128, TextureFormat.RGBA32, true) { name = "ring", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - inner) * 20f) * Mathf.Clamp01((1f - d) * 20f);
                    px[y * 128 + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }
    }
}
