using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vespertine.Audio
{
    /// <summary>Procedurally synthesised placeholder sound effects (see KNOWN_ISSUES: replace with recorded audio).</summary>
    public static class Synth
    {
        public const int Rate = 44100;
        static System.Random _rng = new System.Random(1871);
        static float N() => (float)(_rng.NextDouble() * 2 - 1);

        static AudioClip Make(string name, float seconds, Func<float, float, Fx, float> f, bool loop = false)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var fx = new Fx();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = f(t, t / seconds, fx);
            }
            Normalize(data, 0.9f);
            if (loop) { data = Crossfade(data, Mathf.Min(n / 8, Rate / 4)); n = data.Length; }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            if (m < 1e-5f) return;
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static float[] Crossfade(float[] d, int len)
        {
            int n = d.Length;
            for (int i = 0; i < len; i++)
            {
                float a = i / (float)len;
                d[i] = d[i] * a + d[n - len + i] * (1 - a);
            }
            Array.Resize(ref d, n - len);
            return d;
        }

        /// <summary>Per-clip filter bank so lambdas can use stateful filters by slot.</summary>
        class Fx
        {
            readonly Dictionary<int, BP> _b = new Dictionary<int, BP>();
            readonly Dictionary<int, LP> _l = new Dictionary<int, LP>();
            public float B(int slot, float lo, float hi, float x) { if (!_b.TryGetValue(slot, out var f)) _b[slot] = f = new BP(lo, hi); return f.F(x); }
            public float L(int slot, float c, float x) { if (!_l.TryGetValue(slot, out var f)) _l[slot] = f = new LP(c); return f.F(x); }
        }

        // simple one-pole filters as closures
        class LP { float y; public float a; public LP(float cutoff) { a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate); } public float F(float x) { y += a * (x - y); return y; } }
        class HP { LP lp; public HP(float c) { lp = new LP(c); } public float F(float x) => x - lp.F(x); }
        class BP { LP l; HP h; public BP(float lo, float hi) { h = new HP(lo); l = new LP(hi); } public float F(float x) => l.F(h.F(x)); }

        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
        static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

        public static Dictionary<string, AudioClip> BuildAll()
        {
            var d = new Dictionary<string, AudioClip>();
            void Add(AudioClip c) => d[c.name] = c;

            // footsteps (stone, wood, water, dirt) — 3 variations each
            for (int v = 0; v < 3; v++)
            {
                var lp = new LP(900 + v * 200); var bp = new BP(300, 2500 + v * 400);
                Add(Make("step_stone" + v, 0.12f, (t, u, fx) => bp.F(N()) * Env(t, 0.002f, 0.025f) + Sin(90 + v * 10, t) * Env(t, 0.001f, 0.02f) * 0.4f));
                var lp2 = new LP(500 + v * 80);
                Add(Make("step_wood" + v, 0.16f, (t, u, fx) => lp2.F(N()) * Env(t, 0.002f, 0.03f) + Sin(140 + v * 15, t) * Env(t, 0.001f, 0.05f) * 0.6f));
                var bpw = new BP(400, 3000);
                Add(Make("step_water" + v, 0.3f, (t, u, fx) => bpw.F(N()) * Env(t, 0.01f, 0.08f) * (1 + 0.5f * Sin(30 + v * 7, t))));
                var lpd = new LP(700);
                Add(Make("step_dirt" + v, 0.12f, (t, u, fx) => lpd.F(N()) * Env(t, 0.004f, 0.03f)));
            }
            // heartbeat (lub-dub)
            Add(Make("heartbeat", 0.7f, (t, u, fx) =>
            {
                float a = Sin(52, t) * Env(t, 0.005f, 0.06f);
                float t2 = t - 0.24f;
                float b = t2 > 0 ? Sin(46, t2) * Env(t2, 0.005f, 0.07f) * 0.7f : 0;
                return Mathf.Clamp(a + b, -1, 1);
            }));
            // alert sting: dissonant stab
            Add(Make("sting_alert", 1.4f, (t, u, fx) => (Saw(220, t) * 0.4f + Saw(233, t) * 0.4f + Sin(110, t) * 0.6f + Saw(330, t) * 0.25f) * Env(t, 0.01f, 0.45f)));
            // suspicion: rising thin tone
            Add(Make("sting_suspicious", 0.9f, (t, u, fx) => Sin(600 + 500 * u, t) * Env(t, 0.05f, 0.35f) * 0.5f + Sin(1200 + 1000 * u, t) * Env(t, 0.05f, 0.25f) * 0.15f));
            Add(Make("sting_calm", 1.2f, (t, u, fx) => (Sin(330, t) + Sin(440, t) * 0.5f) * Env(t, 0.15f, 0.4f) * 0.4f));
            // bell: inharmonic partials
            Add(Make("bell", 4f, (t, u, fx) =>
            {
                float s = 0;
                float[] p = { 1f, 2.0f, 2.4f, 3.0f, 4.2f, 5.4f };
                float[] a = { 1f, 0.6f, 0.5f, 0.35f, 0.25f, 0.15f };
                for (int i = 0; i < p.Length; i++) s += Sin(196 * p[i], t) * a[i] * Mathf.Exp(-t * (0.7f + i * 0.5f));
                return s * Mathf.Min(1, t / 0.003f);
            }));
            // gunshot
            var gl = new LP(2500); var gl2 = new LP(200);
            Add(Make("gunshot", 1.2f, (t, u, fx) => gl.F(N()) * Env(t, 0.001f, 0.08f) + gl2.F(N()) * Env(t, 0.002f, 0.35f) * 2.5f));
            var rl = new BP(800, 4000);
            Add(Make("reload", 0.6f, (t, u, fx) => rl.F(N()) * (Env(t, 0.001f, 0.02f) + (t > 0.3f ? Env(t - 0.3f, 0.001f, 0.03f) : 0))));
            // snuff / ignite
            var sh = new BP(1500, 7000);
            Add(Make("snuff", 0.5f, (t, u, fx) => sh.F(N()) * Env(t, 0.01f, 0.12f)));
            var ig = new BP(300, 3000);
            Add(Make("ignite", 0.8f, (t, u, fx) => ig.F(N()) * Env(t, 0.15f, 0.25f) * (1 + 0.3f * Sin(8, t))));
            // feed — wet low pulses
            var fl = new LP(400);
            Add(Make("feed", 1.6f, (t, u, fx) => fl.F(N()) * (0.5f + 0.5f * Sin(3.2f, t)) * Env(t, 0.1f, 1.2f) + Sin(48, t) * Env(t % 0.55f, 0.005f, 0.06f) * (1 - u) * 0.8f));
            Add(Make("gasp", 0.5f, (t, u, fx) => fx.B(1, 500, 2500, N()) * Env(t, 0.02f, 0.1f)));
            // scream (formant-ish)
            Add(Make("scream", 1.2f, (t, u, fx) =>
            {
                float f0 = 500 + 140 * Sin(5, t) + 200 * (1 - u);
                return (Saw(f0, t) * 0.5f + Sin(f0 * 2.1f, t) * 0.3f) * Env(t, 0.03f, 0.6f);
            }));
            // shout (lower)
            Add(Make("shout", 0.6f, (t, u, fx) => (Saw(180 + 40 * u, t) * 0.6f + Sin(540, t) * 0.2f) * Env(t, 0.02f, 0.25f)));
            Add(Make("huh", 0.35f, (t, u, fx) => (Saw(160 + 60 * u, t) * 0.5f) * Env(t, 0.02f, 0.12f)));
            // body drop / thud
            var tl = new LP(250);
            Add(Make("thud", 0.5f, (t, u, fx) => tl.F(N()) * Env(t, 0.002f, 0.08f) * 2 + Sin(60, t) * Env(t, 0.002f, 0.1f)));
            var spl = new BP(300, 5000);
            Add(Make("splash", 1.2f, (t, u, fx) => spl.F(N()) * Env(t, 0.01f, 0.35f) * (1 + 0.5f * Sin(14, t))));
            // door creak
            Add(Make("door", 0.9f, (t, u, fx) => Saw(90 + 60 * Mathf.Sin(u * 7), t) * Env(t, 0.05f, 0.4f) * 0.4f + fx.B(2, 1000, 3000, N()) * 0.05f));
            Add(Make("valve", 1.4f, (t, u, fx) => Saw(70 + 30 * u, t) * 0.4f * (0.6f + 0.4f * Sin(9, t)) * Env(t, 0.05f, 0.8f) + fx.B(3, 2000, 6000, N()) * 0.08f * Env(t, 0.3f, 0.6f)));
            Add(Make("gate", 2f, (t, u, fx) => (Saw(55, t) * 0.5f + fx.L(15, 600, N()) * 0.6f) * Env(t, 0.1f, 1f)));
            // M09: smashed sunstone glass, struck shackles, the generator breaker
            Add(Make("glass", 0.9f, (t, u, fx) => fx.B(30, 3000, 9000, N()) * Env(t, 0.001f, 0.25f) * (1 + 0.8f * Sin(47 + 30 * u, t)) + Sin(2400 - 900 * u, t) * 0.15f * Env(t, 0.001f, 0.15f)));
            Add(Make("chain", 0.7f, (t, u, fx) => (Sin(1700 + 300 * Mathf.Sin(u * 40), t) * 0.25f + fx.B(31, 2500, 7000, N()) * 0.4f) * (0.5f + 0.5f * Sin(14, t)) * Env(t, 0.005f, 0.3f)));
            Add(Make("breaker", 1.6f, (t, u, fx) => fx.L(32, 900, N()) * Env(t, 0.001f, 0.06f) * 2.5f + Saw(60 * (1 - 0.6f * u), t) * 0.35f * Env(t, 0.05f, 1.2f)));
            // M10: the gas holder going up, the works' steam whistle, a lit fuse
            Add(Make("explosion", 5f, (t, u, fx) => fx.L(40, 2200, N()) * Env(t, 0.001f, 0.12f) * 2.5f + fx.L(41, 160, N()) * Env(t, 0.01f, 1.4f) * 5f
                                                    + Sin(32 + 10 * (1 - u), t) * Env(t, 0.02f, 1.8f) * 0.9f + fx.L(42, 600, N()) * Env(t, 0.3f, 2.2f) * 0.8f * (0.6f + 0.4f * Sin(3, t))));
            Add(Make("whistle", 3f, (t, u, fx) => (Sin(740, t) * 0.5f + Sin(1110, t) * 0.3f + Sin(1480, t) * 0.12f + fx.B(43, 1500, 6000, N()) * 0.15f)
                                                  * Mathf.Min(1f, t / 0.15f) * (t > 2.4f ? Mathf.Max(0f, 1f - (t - 2.4f) / 0.6f) : 1f) * (1f + 0.04f * Sin(6, t))));
            Add(Make("fuse", 1.5f, (t, u, fx) => fx.B(44, 2500, 9000, N()) * 0.5f * (0.7f + 0.3f * Sin(23, t)) * Mathf.Min(1f, t / 0.05f) * Mathf.Min(1f, (1.5f - t) / 0.2f)
                                                + (N() > 0.997f ? 0.8f : 0f)));
            // UI
            Add(Make("ui_click", 0.06f, (t, u, fx) => Sin(1800, t) * Env(t, 0.001f, 0.012f)));
            Add(Make("ui_hover", 0.04f, (t, u, fx) => Sin(2600, t) * Env(t, 0.001f, 0.008f) * 0.4f));
            Add(Make("ui_confirm", 0.35f, (t, u, fx) => (Sin(660, t) + Sin(990, t) * 0.5f) * Env(t, 0.005f, 0.12f)));
            Add(Make("ui_error", 0.25f, (t, u, fx) => Saw(140, t) * Env(t, 0.005f, 0.1f) * 0.5f));
            Add(Make("objective", 1.6f, (t, u, fx) => (Sin(392, t) * Env(t, 0.01f, 0.5f) + (t > 0.18f ? Sin(587, t) * Env(t - 0.18f, 0.01f, 0.6f) : 0) + (t > 0.36f ? Sin(784, t) * Env(t - 0.36f, 0.01f, 0.7f) * 0.6f : 0)) * 0.6f));
            Add(Make("fail", 2f, (t, u, fx) => (Saw(110, t) * 0.3f + Saw(116.5f, t) * 0.3f + Sin(55, t) * 0.6f) * Env(t, 0.05f, 0.9f)));
            Add(Make("ui_back", 0.08f, (t, u, fx) => Sin(1100, t) * Env(t, 0.001f, 0.02f)));
            Add(Make("secret", 1.8f, (t, u, fx) => (Sin(523, t) * Env(t, 0.01f, 0.6f) + (t > 0.12f ? Sin(659, t) * Env(t - 0.12f, 0.01f, 0.6f) : 0) + (t > 0.24f ? Sin(988, t) * Env(t - 0.24f, 0.01f, 0.9f) * 0.5f : 0)) * 0.45f));
            Add(Make("victory", 3f, (t, u, fx) => (Sin(220, t) * 0.4f + Sin(277, t) * 0.3f * Env(t, 0.4f, 1.2f) + Sin(330, t) * 0.3f * Env(t, 0.8f, 1.4f) + Sin(55, t) * 0.4f) * Env(t, 0.05f, 1.5f)));
            Add(Make("death", 3f, (t, u, fx) => (Saw(73, t) * 0.25f + Sin(36.7f, t) * 0.6f + Sin(77.8f, t) * 0.2f) * Env(t, 0.02f, 1.4f) + fx.L(20, 300, N()) * Env(t, 0.01f, 0.5f) * 0.5f));
            Add(Make("plan_in", 0.6f, (t, u, fx) => (Sin(180 - 80 * u, t) * 0.5f + fx.B(15, 300, 1500, N()) * 0.3f) * Env(t, 0.02f, 0.25f)));
            Add(Make("plan_out", 0.5f, (t, u, fx) => (Sin(100 + 80 * u, t) * 0.5f + fx.B(16, 300, 1500, N()) * 0.2f) * Env(t, 0.02f, 0.2f)));
            Add(Make("plan_go", 0.9f, (t, u, fx) => (Saw(110, t) * 0.2f + Sin(220, t) * 0.4f + fx.B(17, 600, 4000, N()) * 0.3f * (1 - u)) * Env(t, 0.005f, 0.35f)));
            Add(Make("page", 0.4f, (t, u, fx) => fx.B(18, 1500, 7000, N()) * Mathf.Sin(u * Mathf.PI) * 0.6f));
            Add(Make("awaken", 2.5f, (t, u, fx) => (Sin(110, t) * 0.5f + Sin(165, t) * 0.4f * Env(t, 0.3f, 1f) + Sin(220 * (1 + 0.5f * u), t) * 0.2f) * Env(t, 0.3f, 1f)));
            // abilities
            Add(Make("whisper", 1.0f, (t, u, fx) => fx.B(4, 1800, 5000, N()) * (0.5f + 0.5f * Sin(6, t)) * Env(t, 0.1f, 0.4f)));
            Add(Make("cast", 0.8f, (t, u, fx) => (Sin(220 * (1 + u), t) * 0.4f + fx.B(5, 800, 4000, N()) * 0.4f) * Env(t, 0.02f, 0.3f)));
            Add(Make("mist", 1.2f, (t, u, fx) => fx.B(6, 200, 1500, N()) * Env(t, 0.3f, 0.5f)));
            Add(Make("step_vamp", 0.5f, (t, u, fx) => (Sin(80 * (1 - u * 0.5f), t) + fx.L(16, 300, N())) * Env(t, 0.005f, 0.15f)));
            Add(Make("hit", 0.4f, (t, u, fx) => (fx.L(17, 1200, N()) * 0.8f + Sin(90, t)) * Env(t, 0.001f, 0.08f)));
            Add(Make("burn", 0.8f, (t, u, fx) => fx.B(7, 1500, 8000, N()) * Env(t, 0.05f, 0.4f) * (1 + 0.6f * Sin(23, t))));
            Add(Make("bark", 0.35f, (t, u, fx) => (Saw(320 - 120 * u, t) * 0.6f + fx.B(8, 500, 2000, N()) * 0.3f) * Env(t, 0.005f, 0.09f)));
            Add(Make("growl", 1.2f, (t, u, fx) => Saw(85 + 15 * Sin(11, t), t) * Env(t, 0.1f, 0.6f) * 0.5f));
            Add(Make("flare", 1.5f, (t, u, fx) => fx.B(9, 800, 6000, N()) * Env(t, 0.02f, 0.9f) * (1 + 0.4f * Sin(31, t))));
            Add(Make("pickup", 0.3f, (t, u, fx) => (Sin(880, t) + Sin(1320, t)) * Env(t, 0.003f, 0.08f) * 0.5f));
            Add(Make("climb", 0.4f, (t, u, fx) => fx.B(10, 400, 2500, N()) * Env(t, 0.01f, 0.05f) * (1 + Sin(12, t))));
            Add(Make("swish", 0.35f, (t, u, fx) => fx.B(11, 800 + 2000 * u, 5000, N()) * Mathf.Sin(u * Mathf.PI)));

            // loops
            Add(Make("amb_night", 8f, (t, u, fx) => fx.L(18, 300, N()) * 0.6f * (0.7f + 0.3f * Sin(0.25f, t)) + fx.B(12, 2000, 6000, N()) * 0.03f, true));
            Add(Make("amb_drip", 8f, (t, u, fx) =>
            {
                float s = fx.L(19, 220, N()) * 0.4f;
                float[] drops = { 0.7f, 2.3f, 3.1f, 4.9f, 6.2f, 7.4f };
                foreach (var dt in drops) { float x = t - dt; if (x > 0 && x < 0.2f) s += Sin(1400 + dt * 120, x) * Mathf.Exp(-x * 40) * 0.5f; }
                return s;
            }, true));
            Add(Make("amb_rain", 8f, (t, u, fx) =>
            {
                // steady hiss, a low roof drum, and the odd heavy drop off the eaves
                float s = fx.B(20, 2500, 9000, N()) * 0.32f * (0.85f + 0.15f * Sin(0.31f, t)) + fx.L(21, 500, N()) * 0.35f;
                float[] drops = { 0.4f, 1.9f, 2.6f, 4.1f, 5.5f, 6.8f, 7.6f };
                foreach (var dt in drops) { float x = t - dt; if (x > 0 && x < 0.12f) s += fx.B(22, 300, 1800, N()) * Mathf.Exp(-x * 45) * 0.6f; }
                return s;
            }, true));
            // a brazier or bonfire: a low roar of flame with irregular pops and the odd split of wood
            Add(Make("amb_fire", 6f, (t, u, fx) =>
            {
                float s = fx.L(23, 380, N()) * 0.45f * (0.8f + 0.2f * Sin(0.7f, t) * Sin(1.9f, t));
                float[] pops = { 0.21f, 0.55f, 0.62f, 1.3f, 1.84f, 2.2f, 2.95f, 3.1f, 3.66f, 4.4f, 4.47f, 5.2f, 5.71f };
                for (int i = 0; i < pops.Length; i++) { float x = t - pops[i]; if (x > 0 && x < 0.05f) s += fx.B(24, 1500, 7000, N()) * Mathf.Exp(-x * (i % 3 == 0 ? 60 : 140)) * (i % 4 == 0 ? 1.1f : 0.55f); }
                return s;
            }, true));
            Add(Make("amb_water", 8f, (t, u, fx) => fx.B(13, 150, 900, N()) * (0.6f + 0.4f * Sin(0.4f, t) * Sin(0.17f, t)), true));
            Add(Make("music_drone", 16f, (t, u, fx) =>
            {
                float s = Sin(55, t) * 0.5f + Sin(82.4f, t) * 0.25f * (0.6f + 0.4f * Sin(0.06f, t)) + Sin(110, t) * 0.12f + Sin(130.8f, t) * 0.08f * (0.5f + 0.5f * Sin(0.11f, t));
                return s * 0.8f;
            }, true));
            Add(Make("music_tension", 8f, (t, u, fx) =>
            {
                float pulse = Env(t % 0.75f, 0.005f, 0.12f);
                return Sin(55, t) * 0.4f + Saw(110, t) * 0.08f + Sin(46, t) * pulse * 0.8f + Sin(233, t) * 0.05f * (0.5f + 0.5f * Sin(0.5f, t));
            }, true));
            Add(Make("music_alert", 4f, (t, u, fx) =>
            {
                float pulse = Env(t % 0.333f, 0.003f, 0.08f);
                return Saw(55, t) * 0.25f + Sin(41.2f, t) * pulse + Saw(116.5f, t) * 0.12f * pulse + fx.B(14, 3000, 8000, N()) * 0.04f * Env(t % 0.1665f, 0.001f, 0.01f);
            }, true));
            Add(Make("music_menu", 16f, (t, u, fx) =>
            {
                float s = 0;
                float[] notes = { 220f, 261.6f, 329.6f, 293.7f };
                int idx = (int)(t / 4f) % 4;
                float local = t % 4f;
                s += Sin(notes[idx], t) * Env(local, 0.6f, 2.5f) * 0.3f;
                s += Sin(notes[idx] * 0.5f, t) * 0.25f;
                s += Sin(55, t) * 0.3f;
                return s;
            }, true));
            return d;
        }

        static float Saw(float f, float t) { float p = f * t; return 2f * (p - Mathf.Floor(p + 0.5f)); }
    }
}
