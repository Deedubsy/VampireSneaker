using System;
using System.Collections.Generic;

namespace Vespertine.Audio
{
    /// <summary>
    /// The mission scores: one synthesised theme per `music =` key, each in its own key, mode, metre and instruments.
    /// Every theme renders three looping layers that share one beat, so they stay in time when they play together:
    ///   - Calm: the theme itself.
    ///   - Tension: drum and tremolo in the theme's root, four bars long.
    ///   - Alert: driving hits and stabs, two bars long.
    /// Rendering is pure arithmetic (no Unity API), so the AudioManager runs it on a worker thread and swaps the clips
    /// in when they are ready. Placeholder for a recorded score (KNOWN_ISSUES K15).
    /// </summary>
    public static class Score
    {
        public const int Rate = 22050;
        const double TargetRms = 0.21;
        public static readonly string[] Keys = { "drowned", "lantern", "docks", "cathedral", "gasworks", "masque", "bridges", "hunt", "opera", "bastion" };
        public static bool Has(string key) => key != null && Array.IndexOf(Keys, key) >= 0;

        public sealed class Layers
        {
            public string Key;
            public float Bpm;
            public float[] Calm, Tension, Alert;
        }

        sealed class Theme
        {
            public double Root, Bpm;
            public int Beats, Bars;
            public Func<double, float> Calm;
            public double Beat => 60.0 / Bpm;
            public double BarLen => Beat * Beats;
        }

        public static Layers Render(string key)
        {
            var th = Build(key);
            if (th == null) return null;
            return new Layers
            {
                Key = key,
                Bpm = (float)th.Bpm,
                Calm = RenderLoop(th.BarLen * th.Bars, th.Calm),
                Tension = RenderLoop(th.BarLen * 4, TensionOf(th)),
                Alert = RenderLoop(th.BarLen * 2, AlertOf(th)),
            };
        }

        /// <summary>Renders one loop of <paramref name="seconds"/>, plus a half-second run-on that is blended into the
        /// head. The loop point then carries the tails of the last notes and the warmed-up filters.</summary>
        static float[] RenderLoop(double seconds, Func<double, float> f)
        {
            int n = (int)Math.Round(seconds * Rate);
            int xf = Math.Min(Rate / 2, n / 8);
            var d = new float[n + xf];
            for (int i = 0; i < d.Length; i++) d[i] = f(i / (double)Rate);
            for (int i = 0; i < xf; i++)
            {
                float a = i / (float)xf;
                d[i] = d[i] * a + d[n + i] * (1 - a);
            }
            Array.Resize(ref d, n);
            // match loudness rather than peaks, so a theme with a few loud plucks isn't left quiet; tanh softens
            // the peaks this pushes over, then a final trim keeps everything under 0.95
            double e = 0;
            for (int i = 0; i < n; i++) e += d[i] * d[i];
            double rms = Math.Sqrt(e / Math.Max(1, n));
            if (rms < 1e-6) return d;
            float k = (float)(TargetRms / rms), m = 0;
            for (int i = 0; i < n; i++) { d[i] = (float)Math.Tanh(d[i] * k); m = Math.Max(m, Math.Abs(d[i])); }
            if (m > 0.95f) { float q = 0.95f / m; for (int i = 0; i < n; i++) d[i] *= q; }
            return d;
        }

        // ================================================================ building blocks
        const int TableSize = 4096;
        static readonly float[] Table = BuildTable();
        static float[] BuildTable()
        {
            var t = new float[TableSize + 1];
            for (int i = 0; i <= TableSize; i++) t[i] = (float)Math.Sin(2 * Math.PI * i / TableSize);
            return t;
        }

        /// <summary>Table sine with a double phase, so a long loop doesn't smear high notes.</summary>
        static float S(double f, double t) { double p = f * t; p -= Math.Floor(p); return Table[(int)(p * TableSize)]; }
        static float Ex(double x) => (float)Math.Exp(x);
        static float Env(double t, double a, double d) => t < 0 ? 0f : t < a ? (float)(t / a) : (float)Math.Exp(-(t - a) / d);
        static float SawA(double f, double t, int h) { float s = 0; for (int i = 1; i <= h; i++) s += S(i * f, t) / i; return s; }
        static float Smooth(double x) { if (x <= 0) return 0; if (x >= 1) return 1; return (float)(x * x * (3 - 2 * x)); }
        static float Noise(double t, int seed)
        {
            unchecked
            {
                ulong x = (ulong)((long)(t * Rate + 0.5) * 2654435761L + seed * 40503L);
                x ^= x >> 31; x *= 0xBF58476D1CE4E5B9UL; x ^= x >> 29; x *= 0x94D049BB133111EBUL; x ^= x >> 32;
                return (x & 0xFFFFFF) / (float)0x800000 - 1f;
            }
        }
        static double Mod(double a, double m) { double r = a % m; return r < 0 ? r + m : r; }

        static readonly int[] Aeolian = { 0, 2, 3, 5, 7, 8, 10 }, Dorian = { 0, 2, 3, 5, 7, 9, 10 },
                              Phrygian = { 0, 1, 3, 5, 7, 8, 10 }, Harmonic = { 0, 2, 3, 5, 7, 8, 11 };

        static double Hz(double root, int[] sc, int deg)
        {
            int n = sc.Length, o = (int)Math.Floor(deg / (double)n), i = deg - o * n;
            return root * Math.Pow(2, (sc[i] + 12 * o) / 12.0);
        }

        sealed class LP { float y; readonly float a; public LP(double c) { a = 1f - (float)Math.Exp(-2 * Math.PI * c / Rate); } public float F(float x) { y += a * (x - y); return y; } }
        sealed class BP { readonly LP l, h; public BP(double lo, double hi) { h = new LP(lo); l = new LP(hi); } public float F(float x) => l.F(x - h.F(x)); }

        delegate float Voice(double f, double lt, double dur);

        /// <summary>A melody in scale degrees, one token per step: a number is a note, "_" holds the note before, "."
        /// is a rest, and "|" is ignored (bar lines for the reader).</summary>
        sealed class Pat
        {
            public readonly double Step, Len;
            readonly double[] _f, _start, _dur;
            readonly int[] _owner;
            public Pat(string s, double root, int[] sc, double step)
            {
                Step = step;
                var f = new List<double>(); var st = new List<double>(); var du = new List<double>(); var own = new List<int>();
                int k = 0;
                foreach (var tok in s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (tok == "|") continue;
                    if (tok == "_") { if (du.Count > 0) du[du.Count - 1] += step; }
                    else if (tok != ".") { f.Add(Hz(root, sc, int.Parse(tok))); st.Add(k * step); du.Add(step); }
                    own.Add(f.Count - 1);
                    k++;
                }
                _f = f.ToArray(); _start = st.ToArray(); _dur = du.ToArray(); _owner = own.ToArray();
                Len = k * step;
            }

            public float Play(double t, Voice v, int ring = 3)
            {
                if (_f.Length == 0) return 0;
                double lt0 = Mod(t, Len);
                int k = Math.Min((int)(lt0 / Step), _owner.Length - 1);
                int e = _owner[k];
                float s = 0;
                for (int b = 0; b < ring && b < _f.Length; b++)
                {
                    int idx = e - b; double off = 0;
                    while (idx < 0) { idx += _f.Length; off += Len; }
                    s += v(_f[idx], lt0 - _start[idx] + off, _dur[idx]);
                }
                return s;
            }
        }

        /// <summary>A chord progression, one chord (scale degrees) per <c>bar</c> seconds, crossfading into each
        /// new chord. The voice is called with absolute time, so sustained tones keep their phase.</summary>
        sealed class Prog
        {
            readonly double[][] _f; readonly double _bar, _xf;
            public Prog(double root, int[] sc, double bar, double xf, params int[][] chords)
            {
                _bar = bar; _xf = xf;
                _f = new double[chords.Length][];
                for (int i = 0; i < chords.Length; i++) { _f[i] = new double[chords[i].Length]; for (int j = 0; j < chords[i].Length; j++) _f[i][j] = Hz(root, sc, chords[i][j]); }
            }
            public double Root(double t) => _f[(int)Mod(Math.Floor(t / _bar), _f.Length)][0];
            public float Play(double t, Func<double, double, float> v)
            {
                long k = (long)Math.Floor(t / _bar);
                double lt = t - k * _bar;
                float a = Smooth(lt / _xf), s = 0;
                foreach (var f in _f[(int)Mod(k, _f.Length)]) s += v(f, t) * a;
                if (a < 1) foreach (var f in _f[(int)Mod(k - 1, _f.Length)]) s += v(f, t) * (1 - a);
                return s;
            }
        }

        /// <summary>A drum line: "X" accent, "x" soft, "." nothing. Returns the time since the latest hit and its weight.</summary>
        sealed class Beat
        {
            readonly float[] _amp; readonly double _step;
            public Beat(string s, double step)
            {
                _step = step;
                var a = new List<float>();
                foreach (var c in s) if (c == 'X') a.Add(1f); else if (c == 'x') a.Add(0.55f); else if (c == '.') a.Add(0f);
                _amp = a.ToArray();
            }
            public float Hit(double t, out double lt)
            {
                long k = (long)Math.Floor(t / _step);
                for (int b = 0; b < _amp.Length; b++)
                {
                    float a = _amp[(int)Mod(k - b, _amp.Length)];
                    if (a > 0) { lt = t - (k - b) * _step; return a; }
                }
                lt = 99; return 0;
            }
        }

        // ---------------------------------------------------------------- instruments
        static float Pluck(double f, double lt, double dur) => lt > 1.6 ? 0 :
            (S(f, lt) + 0.5f * S(2 * f, lt) * Ex(-lt * 8) + 0.3f * S(3 * f, lt) * Ex(-lt * 14)) * Env(lt, 0.004, 0.45);
        static float Harpsi(double f, double lt, double dur) => lt > 1.4 ? 0 :
            (S(f, lt) + 0.7f * S(2 * f, lt) + 0.5f * S(3 * f, lt) * Ex(-lt * 4) + 0.35f * S(4 * f, lt) * Ex(-lt * 6) + 0.2f * S(6 * f, lt) * Ex(-lt * 10)) * Env(lt, 0.002, 0.32);
        static float Bell(double f, double lt, double dur) => lt > 5 ? 0 :
            (S(f, lt) + 0.55f * S(2.76 * f, lt) * Ex(-lt * 2) + 0.35f * S(5.4 * f, lt) * Ex(-lt * 4) + 0.2f * S(8.93 * f, lt) * Ex(-lt * 7)) * Env(lt, 0.002, 1.6);
        static float MusicBox(double f, double lt, double dur) => lt > 3 ? 0 :
            (S(f, lt) + 0.3f * S(4 * f, lt) * Ex(-lt * 10) + 0.12f * S(5.95 * f, lt) * Ex(-lt * 16)) * Env(lt, 0.002, 0.8);
        static float Drip(double f, double lt, double dur) => lt > 1.5 ? 0 :
            (S(f, lt) + 0.4f * S(2.76 * f, lt) * Ex(-lt * 6)) * Env(lt, 0.002, 0.55);

        /// <summary>A bowed or blown note: attack, sustain for its duration, then release.</summary>
        static float Held(double lt, double dur, double attack, double release)
        {
            if (lt < 0) return 0;
            float a = Smooth(lt / Math.Min(attack, dur * 0.6 + 0.02));
            return lt <= dur ? a : a * Ex(-(lt - dur) / release);
        }
        static float Strings(double f, double t) { double tt = t + 0.0007 * Math.Sin(2 * Math.PI * 5.1 * t); return SawA(f, tt, 6); }
        static float Bowed(double f, double lt, double dur)
        {
            if (lt > dur + 1.5) return 0;
            return Strings(f, lt) * Held(lt, dur, 0.25, 0.3);
        }
        static float Brass(double f, double t)
        {
            return S(f, t) + 0.7f * S(2 * f, t) + 0.5f * S(3 * f, t) + 0.32f * S(4 * f, t) + 0.18f * S(5 * f, t) + 0.1f * S(6 * f, t);
        }
        static float Horn(double f, double lt, double dur)
        {
            if (lt > dur + 1.2) return 0;
            float e = Held(lt, dur, 0.18, 0.25);
            // a horn brightens as it swells
            return (S(f, lt) + 0.6f * e * S(2 * f, lt) + 0.35f * e * S(3 * f, lt) + 0.18f * e * e * S(4 * f, lt)) * e;
        }
        static float Organ(double f, double t) => S(f, t) + 0.55f * S(2 * f, t) + 0.3f * S(3 * f, t) + 0.22f * S(4 * f, t) + 0.1f * S(8 * f, t);
        static float Sq(double x) => (float)(x * x);

        /// <summary>A sung "ah": harmonics weighted by the vowel's formants, with vibrato that grows into the note.</summary>
        static float Sung(double f, double lt, double dur, double f1, double f2)
        {
            if (lt > dur + 1.2) return 0;
            double vib = 0.0011 * Math.Min(1, lt / 0.6) * Math.Sin(2 * Math.PI * 5.4 * lt);
            double tt = lt + vib;
            float s = 0;
            for (int h = 1; h <= 9; h++)
            {
                double hf = h * f;
                float a = Ex(-Sq((hf - f1) / 260)) + 0.55f * Ex(-Sq((hf - f2) / 320)) + 0.12f * Ex(-Sq((hf - 2900) / 450));
                if (h == 1) a += 0.35f;
                if (a > 0.01f) s += a * S(hf, tt);
            }
            return s * Held(lt, dur, 0.22, 0.35);
        }

        static float Tom(double lt, double f) => lt > 1 ? 0 : S(f * (1 + 0.9 * Math.Exp(-lt * 28)), lt) * Env(lt, 0.002, 0.22);
        static float Metal(double lt, double f) => lt > 0.5 ? 0 :
            (S(f, lt) + 0.8f * S(f * 1.47, lt) + 0.6f * S(f * 2.09, lt) + 0.45f * S(f * 2.56, lt)) * Env(lt, 0.001, 0.07);

        // ================================================================ the themes
        static Theme Build(string key)
        {
            switch (key)
            {
                case "drowned":   return Drowned();
                case "lantern":   return Lantern();
                case "docks":     return Docks();
                case "cathedral": return Cathedral();
                case "gasworks":  return Gasworks();
                case "masque":    return Masque();
                case "bridges":   return Bridges();
                case "hunt":      return Hunt();
                case "opera":     return Opera();
                case "bastion":   return Bastion();
            }
            return null;
        }

        /// <summary>M01, M09. A minor, slow. Pale chords under water, a low swell, drops falling in the dark.</summary>
        static Theme Drowned()
        {
            var th = new Theme { Root = 55, Bpm = 60, Beats = 4, Bars = 8 };
            var pad = new Prog(110, Aeolian, th.BarLen * 2, 1.4, new[] { 0, 2, 4 }, new[] { -2, 0, 2 }, new[] { -4, -2, 0 }, new[] { -3, -1, 1 });
            var drips = new Pat("7 . . . . . . . | . . . 9 . . . . | . 11 . . . . . . | . . . . . . 8 . | " +
                                "7 . . . . . . . | . . . 6 . . . . | . 4 . . . . . . | . . . . . . . .", 220, Aeolian, th.Beat / 2);
            th.Calm = t =>
                pad.Play(t, (f, tt) => (S(f, tt) + 0.25f * S(2 * f, tt)) * (0.8f + 0.2f * S(0.23, tt))) * 0.16f
                + S(55, t) * 0.35f * (0.65f + 0.35f * S(0.05, t))
                + drips.Play(t, Drip) * 0.22f;
            return th;
        }

        /// <summary>M02. D Dorian. A street-organ drone and a music-box tune, a slum lullaby.</summary>
        static Theme Lantern()
        {
            var th = new Theme { Root = 73.42, Bpm = 72, Beats = 4, Bars = 8 };
            var tune = new Pat("4 . 3 . 2 . 0 . | 1 . 2 . 4 . . . | 5 . 4 . 3 . 2 . | 3 . . . . . . . | " +
                               "4 . 3 . 2 . 0 . | 1 . 2 . 3 . 4 . | 2 . 1 . -1 . 1 . | 0 . . . . . . .", 293.66, Dorian, th.Beat / 2);
            var bass = new Pat("0 . 0 . | -3 . -3 . | -2 . -2 . | -3 . -3 . | 0 . 0 . | -2 . -3 . | -4 . -3 . | 0 . . .", 73.42, Dorian, th.Beat);
            th.Calm = t =>
            {
                float chien = 0.8f + 0.2f * Ex(-Mod(t, th.Beat / 2) * 10);
                return (SawA(146.83, t, 6) + 0.6f * SawA(220, t, 5)) * 0.07f * chien
                     + tune.Play(t, MusicBox) * 0.24f
                     + bass.Play(t, Pluck) * 0.3f;
            };
            return th;
        }

        /// <summary>M03. E Phrygian. A cello line over a low drone, a foghorn every four bars, ropes creaking.</summary>
        static Theme Docks()
        {
            var th = new Theme { Root = 82.41, Bpm = 56, Beats = 4, Bars = 8 };
            var cello = new Pat("0 _ 1 0 | -1 _ -2 _ | 0 _ 1 3 | 1 _ 0 _", 82.41, Phrygian, th.Beat * 2);
            var creak = new BP(400, 1400);
            double four = th.BarLen * 4;
            th.Calm = t =>
            {
                double h = Mod(t, four);
                float horn = (S(41.2, t) + 0.6f * S(82.4, t) + 0.3f * S(123.6, t)) * Smooth(h / 1.5) * (h < 3.5 ? 1f : Ex(-(h - 3.5) / 0.8));
                double c = Mod(t - th.BarLen * 2.5, four);
                float cr = c < 1.2 ? creak.F(Noise(t, 3)) * (0.5f + 0.5f * S(27, t)) * Ex(-c * 2.5) : creak.F(0);
                return cello.Play(t, Bowed) * 0.22f + horn * 0.28f
                     + (S(82.41, t) + 0.5f * S(123.47, t)) * 0.12f * (0.7f + 0.3f * S(0.07, t))
                     + cr * 0.5f;
            };
            return th;
        }

        /// <summary>M04, M14. C harmonic minor, very slow. Organ chords, a chant above them, a deep bell every four bars.</summary>
        static Theme Cathedral()
        {
            var th = new Theme { Root = 65.41, Bpm = 50, Beats = 4, Bars = 8 };
            var organ = new Prog(130.81, Harmonic, th.BarLen, 0.9,
                new[] { 0, 2, 4 }, new[] { -4, -2, 0 }, new[] { -2, 0, 2 }, new[] { -3, -1, 1 },
                new[] { 0, 2, 4 }, new[] { -2, 0, 2 }, new[] { -4, -2, 0 }, new[] { -3, -1, 1 });
            var chant = new Pat("4 5 5 4 | 4 5 3 4", 261.63, Harmonic, th.BarLen);
            double four = th.BarLen * 4;
            th.Calm = t =>
                organ.Play(t, Organ) * 0.1f
                + S(organ.Root(t) / 2, t) * 0.18f
                + chant.Play(t, (f, lt, d) => Sung(f, lt, d, 700, 1100), 2) * 0.12f
                + Bell(130.81, Mod(t, four), 0) * 0.3f;
            return th;
        }

        /// <summary>M05, M10. F minor in 5/4. A pumping throb, iron clanks, steam, a grinding semitone.</summary>
        static Theme Gasworks()
        {
            var th = new Theme { Root = 87.31, Bpm = 96, Beats = 5, Bars = 8 };
            var clank = new Beat("X.xx.x..x.", th.Beat / 2);
            var bass = new Pat("0 . 0 2 . | -1 . -1 1 .", 87.31, Aeolian, th.Beat);
            var hiss = new BP(2500, 7000);
            var click = new BP(1800, 6000);
            th.Calm = t =>
            {
                double lb = Mod(t, th.Beat);
                float throb = SawA(43.65, t, 8) * (0.55f + 0.45f * Ex(-lb * 5)) * 0.2f;
                float a = clank.Hit(t, out double lc);
                float metal = a > 0 ? (Metal(lc, 310) * 0.6f + click.F(Noise(t, 5)) * Env(lc, 0.001, 0.02)) * a : click.F(0);
                double ls = Mod(t, th.BarLen * 2);
                float steam = hiss.F(Noise(t, 9)) * Smooth(ls / 0.3) * Ex(-ls / 1.4);
                double lg = Mod(t, th.BarLen * 4);
                float grind = (S(174.61, t) + S(185.0, t)) * 0.05f * Smooth(lg / 3) * Ex(-Math.Max(0, lg - 4) / 2);
                return throb + metal * 0.28f + steam * 0.25f + grind + bass.Play(t, Pluck) * 0.3f;
            };
            return th;
        }

        /// <summary>M06. G harmonic minor, a waltz. Pizzicato on one, harpsichord chords on two and three, a sad tune.</summary>
        static Theme Masque()
        {
            var th = new Theme { Root = 98, Bpm = 84, Beats = 3, Bars = 16 };
            int[] I = { 0, 2, 4 }, IV = { 3, 5, 7 }, V = { 4, 6, 8 }, VI = { 5, 7, 9 };
            var chords = new[] { I, I, IV, IV, V, V, I, I, VI, VI, IV, IV, V, V, I, I };
            var tune = new Pat("4 _ 2 | 0 _ _ | 3 _ 5 | 4 3 2 | 1 _ -1 | 1 2 3 | 2 _ 1 | 0 _ _ | " +
                               "2 _ 4 | 5 _ 4 | 3 _ 2 | 3 4 5 | 6 _ 4 | 1 _ 3 | 2 1 -1 | 0 _ _", 392, Harmonic, th.Beat);
            var bassF = new double[16]; var chordF = new double[16][];
            for (int i = 0; i < 16; i++)
            {
                bassF[i] = Hz(49, Harmonic, chords[i][0]);
                chordF[i] = new double[3];
                for (int j = 0; j < 3; j++) chordF[i][j] = Hz(196, Harmonic, chords[i][j] > 4 ? chords[i][j] - 7 : chords[i][j]);
            }
            th.Calm = t =>
            {
                float s = 0;
                // this beat and the one before it, so the plucks ring across the bar line
                for (int back = 0; back < 2; back++)
                {
                    long k = (long)Math.Floor(t / th.Beat) - back;
                    double lt = t - k * th.Beat;
                    int bar = (int)Mod(Math.Floor(k / 3.0), 16), beat = (int)Mod(k, 3);
                    if (beat == 0) s += Pluck(bassF[bar], lt, 0) * 0.45f;
                    else foreach (var f in chordF[bar]) s += Harpsi(f, lt, 0) * 0.1f;
                }
                return s + tune.Play(t, Harpsi, 2) * 0.2f;
            };
            return th;
        }

        /// <summary>M07. B-flat minor, quick. A running string ostinato (the water) under a long, high line.</summary>
        static Theme Bridges()
        {
            var th = new Theme { Root = 58.27, Bpm = 100, Beats = 4, Bars = 8 };
            int[] shift = { 0, 0, -2, -2, -3, -3, -1, -1 };
            int[] figure = { 0, 0, 2, 0, 3, 0, 2, 0 };
            var sb = new System.Text.StringBuilder();
            foreach (var o in shift) { foreach (var d in figure) sb.Append(d + o).Append(' '); sb.Append("| "); }
            var ost = new Pat(sb.ToString(), 116.54, Aeolian, th.Beat / 2);
            var line = new Pat("4 _ _ 3 | 2 _ _ _ | 0 _ 1 2 | 1 _ _ _", 466.16, Aeolian, th.Beat * 2);
            th.Calm = t =>
                ost.Play(t, (f, lt, d) => lt > 0.6 ? 0 : Strings(f, lt) * Held(lt, d * 0.6, 0.02, 0.06), 2) * 0.16f
                + line.Play(t, Bowed) * 0.12f
                + (S(58.27, t) + 0.4f * S(87.31, t)) * 0.22f;
            return th;
        }

        /// <summary>M08, M13. D minor. Toms like a heartbeat, horns in fifths, a hunting call and its answer.</summary>
        static Theme Hunt()
        {
            var th = new Theme { Root = 73.42, Bpm = 66, Beats = 4, Bars = 8 };
            var drum = new Beat("X..xX...X..xX.xx", th.Beat / 2);
            var horns = new Prog(146.83, Aeolian, th.BarLen * 2, 1.0, new[] { 0, 4 }, new[] { -2, 2 }, new[] { -4, 0 }, new[] { -3, 1 });
            var call = new Pat("0 _ 4 _ | 7 _ _ _ | . . . . | . . . . | 7 _ 5 _ | 4 _ _ _ | . . . . | . . . .", 293.66, Aeolian, th.Beat);
            th.Calm = t =>
            {
                float a = drum.Hit(t, out double lt);
                return Tom(lt, 55) * a * 0.55f
                     + horns.Play(t, Brass) * 0.07f * (0.75f + 0.25f * S(0.12, t))
                     + call.Play(t, Horn, 2) * 0.22f;
            };
            return th;
        }

        /// <summary>M11. E-flat harmonic minor. Strings and pizzicato from the pit, and a soprano aria floating over them.</summary>
        static Theme Opera()
        {
            var th = new Theme { Root = 77.78, Bpm = 60, Beats = 4, Bars = 8 };
            var strings = new Prog(155.56, Harmonic, th.BarLen, 0.8,
                new[] { 0, 2, 4 }, new[] { -2, 0, 2 }, new[] { -4, -2, 0 }, new[] { -3, -1, 1 },
                new[] { 0, 2, 4 }, new[] { -4, -2, 0 }, new[] { -3, -1, 1 }, new[] { 0, 2, 4 });
            var aria = new Pat("7 _ _ _ | 9 _ 8 7 | 5 _ _ _ | 6 _ 5 4 | 4 _ _ 2 | 3 _ 2 _ | 8 _ 6 _ | 7 _ _ _", 311.13, Harmonic, th.Beat);
            th.Calm = t =>
            {
                double lb = Mod(t, th.Beat * 2);
                return strings.Play(t, Strings) * 0.05f
                     + Pluck(strings.Root(t) / 2, lb, 0) * 0.3f
                     + aria.Play(t, (f, lt, d) => Sung(f, lt, d, 820, 1180), 2) * 0.16f;
            };
            return th;
        }

        /// <summary>M12. C-sharp Phrygian. A march: snare, timpani, low brass, the flat second of the Vigil.</summary>
        static Theme Bastion()
        {
            var th = new Theme { Root = 69.3, Bpm = 80, Beats = 4, Bars = 8 };
            var snare = new Beat("X.x.X.xxX.x.Xxxx", th.Beat / 4);
            var brass = new Prog(138.59, Phrygian, th.BarLen, 0.5,
                new[] { 0, 2, 4 }, new[] { 1, 3, 5 }, new[] { 0, 2, 4 }, new[] { -3, -1, 1 },
                new[] { 0, 2, 4 }, new[] { 1, 3, 5 }, new[] { -2, 0, 2 }, new[] { -3, -1, 1 });
            var snareF = new BP(1500, 5000);
            th.Calm = t =>
            {
                float a = snare.Hit(t, out double ls);
                float sn = snareF.F(Noise(t, 11)) * Env(ls, 0.001, 0.05) * a;
                double lb = Mod(t, th.BarLen);
                int bar = (int)Mod(Math.Floor(t / th.BarLen), 8);
                float timp = Tom(lb, 69.3) + ((bar == 3 || bar == 7) ? Tom(lb - th.Beat * 2, 69.3) : 0);
                return sn * 0.22f + timp * 0.45f + brass.Play(t, Brass) * 0.06f;
            };
            return th;
        }

        // ---------------------------------------------------------------- the shared tension and alert layers
        static double LowRoot(Theme th) { double r = th.Root; while (r > 70) r /= 2; while (r < 40) r *= 2; return r; }

        static Func<double, float> TensionOf(Theme th)
        {
            double r = LowRoot(th), beat = th.Beat;
            return t =>
            {
                double lt = Mod(t, beat);
                float drum = Tom(lt, r * 0.9);
                float trem = (S(r * 4, t) + S(r * 4 * 1.05946, t)) * (0.5f + 0.5f * S(7, t));
                return drum * 0.8f + trem * 0.07f + S(r, t) * 0.3f + S(r * 1.5, t) * 0.08f * (0.5f + 0.5f * S(0.2, t));
            };
        }

        static Func<double, float> AlertOf(Theme th)
        {
            double r = LowRoot(th), half = th.Beat / 2;
            var hat = new BP(4000, 9000);
            return t =>
            {
                double lt = Mod(t, half), lq = Mod(t, half / 2);
                float hit = Tom(lt, r * 0.75) * 1.2f;
                float stab = (SawA(r * 2, lt, 5) + SawA(r * 2 * 1.05946, lt, 5)) * Env(lt, 0.003, 0.09) * 0.35f;
                return hit + stab + SawA(r, t, 6) * 0.2f + hat.F(Noise(t, 13)) * Env(lq, 0.001, 0.015) * 0.5f;
            };
        }
    }
}
