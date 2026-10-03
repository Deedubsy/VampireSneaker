using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.Core
{
    /// <summary>
    /// The cone truth sweep (SR.14), run in a loaded mission (dev): every seeing guard's full cone is built off screen and
    /// read back (<see cref="ConeRenderer.DrawFor"/>), and at random standing points on his ground the drawn answer (near
    /// fill, lit far fill, nothing) is compared with the rule that judges her there (<see cref="AI.Npc.SeenBand"/>, with
    /// line of sight, at the light guards judge). Points the drawing can't answer cleanly (between two rays that disagree,
    /// between far samples that disagree, on the edge and end arcs) and points within <see cref="Tol"/> of a true boundary
    /// are skipped and counted; any other disagreement is a lie and fails the sweep.
    /// </summary>
    public static class DevConeCheck
    {
        /// <summary>How near a true boundary a point may be before it is not held against the drawing: the light cells
        /// are 0.5 m, so a lamp's threshold can sit 0.35 m from where the cone draws it.</summary>
        public const float Tol = 0.4f;
        /// <summary>The origin arc and the touch circle are drawn apart from the fill: points this close aren't checked.</summary>
        public const float Skip = 0.5f;

        public enum Verdict { Off, Near, Far, Edge }

        /// <summary>What a built cone draws at a ground point. Pure: the ray fan is read as drawn, rays and far samples
        /// interpolated, and wherever neighbouring rays or samples disagree the answer is <see cref="Verdict.Edge"/>.</summary>
        public static Verdict Read(ConeRenderer.Drawn d, Vector3 p)
        {
            var to = p - d.Origin; to.y = 0f;
            float t = to.magnitude;
            if (t < 0.001f) return Verdict.Edge;
            float off = Vector3.SignedAngle(d.Fwd, to, Vector3.up);
            if (Mathf.Abs(off) > d.Half) return Verdict.Off;
            int rays = ConeRenderer.Drawn.RayCount, n = ConeRenderer.Drawn.Samples;
            float u = (off + d.Half) / (2f * d.Half) * rays;
            int i0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, rays), i1 = Mathf.Min(i0 + 1, rays);
            float reachLo = Mathf.Min(d.Reach[i0], d.Reach[i1]), reachHi = Mathf.Max(d.Reach[i0], d.Reach[i1]);
            float nearLo = Mathf.Min(d.Near[i0], d.Near[i1]), nearHi = Mathf.Max(d.Near[i0], d.Near[i1]);
            if (t > reachHi) return Verdict.Off;
            if (t > reachLo) return Verdict.Edge;
            if (t <= nearLo) return Verdict.Near;
            if (t <= nearHi) return Verdict.Edge;
            if (!d.FarOn[i0] || !d.FarOn[i1] || t > Mathf.Min(d.FarEnd[i0], d.FarEnd[i1])) return Verdict.Edge;
            int lit = 0, dark = 0;
            foreach (int i in new[] { i0, i1 })
            {
                float span = Mathf.Max(0.001f, d.FarEnd[i] - d.Near[i]);
                float fs = Mathf.Clamp((t - d.Near[i]) / span * (n - 1), 0f, n - 1);
                int s0 = Mathf.FloorToInt(fs), s1 = Mathf.Min(s0 + 1, n - 1);
                foreach (int s in new[] { s0, s1 })
                    if (d.Lit[i * n + s]) lit++; else dark++;
            }
            return dark == 0 ? Verdict.Far : lit == 0 ? Verdict.Off : Verdict.Edge;
        }

        /// <summary>The rule's answer as a drawing would give it: the peripheral and near bands are the near fill.</summary>
        public static Verdict Of(DetectionMath.Band b) =>
            b == DetectionMath.Band.Far ? Verdict.Far : b == DetectionMath.Band.None ? Verdict.Off : Verdict.Near;

        /// <param name="tamper">Alters each read-back before it is judged: the sweep's own check that it catches a lie.</param>
        public static string Run(int perGuard = 2000, int seed = 1, System.Action<ConeRenderer.Drawn> tamper = null)
        {
            if (Game.AI == null || Game.Lights == null || Game.Level == null || ConeRenderer.Instance == null) return "no level";
            var rng = new System.Random(seed);
            var sb = new StringBuilder();
            int guards = 0, checkedPts = 0, edge = 0, unstable = 0, lies = 0, leafLies = 0;
            var kinds = new Dictionary<string, int>();
            var agree = new int[4];
            foreach (var n in Game.AI.Npcs)
            {
                if (!n || n.Friendly) continue;
                var d = ConeRenderer.Instance.DrawFor(n);
                if (d == null) continue;
                tamper?.Invoke(d);
                guards++;
                int gl = 0, gc = 0;
                float r = n.Vision.FarRange + 1f;
                for (int tries = 0, got = 0; got < perGuard && tries < perGuard * 6; tries++)
                {
                    // three in four inside his view (and 15° past its sides), where the drawing has something to say
                    float yaw = Mathf.Atan2(d.Fwd.x, d.Fwd.z), wedge = (d.Half + 15f) * Mathf.Deg2Rad;
                    float ang = tries % 4 == 3 ? (float)rng.NextDouble() * Mathf.PI * 2f
                              : Mathf.PI * 0.5f - (yaw + ((float)rng.NextDouble() * 2f - 1f) * wedge);
                    float rr = Mathf.Sqrt((float)rng.NextDouble()) * r;
                    var want = d.Origin + new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr);
                    if (!NavMesh.SamplePosition(want, out var hit, 0.3f, NavMesh.AllAreas)) continue;
                    var p = hit.position;
                    // the ground he stands on: raised surfaces are the deck's, checked by eye
                    if (Mathf.Abs(p.y - d.Origin.y) > 0.5f || Util.FlatDistance(p, d.Origin) < n.Vision.Peripheral + Skip) continue;
                    got++;
                    var drawn = Read(d, p);
                    if (drawn == Verdict.Edge) { edge++; continue; }
                    var truth = Truth(n, p, out bool leaves, out float light);
                    if (!Stable(n, p, truth)) { unstable++; continue; }
                    checkedPts++; gc++;
                    if (drawn == truth) { agree[(int)drawn]++; continue; }
                    lies++; gl++;
                    if (leaves) leafLies++;
                    string kind = $"drawn {drawn} / judged {truth}{(leaves ? " (leaves)" : "")}";
                    kinds.TryGetValue(kind, out int k); kinds[kind] = k + 1;
                    if (gl <= 3) sb.AppendLine($"  {n.name}: at ({p.x:0.00}, {p.z:0.00}) {kind}, light {light:0.00}, {Util.FlatDistance(p, d.Origin):0.0} m");
                }
                if (gl > 0) sb.AppendLine($"  {n.name}: {gl} of {gc} wrong");
            }
            var head = new StringBuilder();
            head.AppendLine($"{guards} guards, {checkedPts} points checked, {edge} on a drawn edge, {unstable} within {Tol} m of a true boundary; wrong {lies} ({leafLies} among leaves)");
            head.AppendLine($"  agreed: near {agree[(int)Verdict.Near]}, lit far {agree[(int)Verdict.Far]}, unseen {agree[(int)Verdict.Off]}");
            foreach (var kv in kinds) head.AppendLine($"  {kv.Key}: {kv.Value}");
            head.Append(sb);
            head.AppendLine(lies == 0 ? "RESULT PASS" : "RESULT FAIL");
            return head.ToString();
        }

        /// <summary>The rule at a standing point, at the light guards judge her there (foliage dims and hides).</summary>
        static Verdict Truth(AI.Npc n, Vector3 p, out bool leaves, out float light)
        {
            leaves = Game.Level.InFoliage(p);
            light = Player.Vampire.JudgedLight(Game.Lights.LightAt(p) * (leaves ? 0.6f : 1f), leaves);
            return Of(n.SeenBand(p, light));
        }

        /// <summary>True when the rule gives the same answer <see cref="Tol"/> to every side.</summary>
        static bool Stable(AI.Npc n, Vector3 p, Verdict at)
        {
            foreach (var o in new[] { new Vector3(Tol, 0, 0), new Vector3(-Tol, 0, 0), new Vector3(0, 0, Tol), new Vector3(0, 0, -Tol) })
                if (Truth(n, p + o, out _, out _) != at) return false;
            return true;
        }
    }
}
