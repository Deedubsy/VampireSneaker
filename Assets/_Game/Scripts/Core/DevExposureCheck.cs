using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Vespertine.Stealth;

namespace Vespertine.Core
{
    /// <summary>
    /// SR.3's honesty test, run in a loaded mission (dev): the Exposure Field must equal <see cref="LightSystem.LightAt"/>
    /// within 0.02 at random standing points, as lit now, with a third of the lamps snuffed, and after a lamp moves.
    /// Also reports what the field didn't answer itself (fallbacks) and what the bake cost.
    /// </summary>
    public static class DevExposureCheck
    {
        public const float Tolerance = 0.02f;

        public static string Run(int points = 10000, int seed = 1)
        {
            var lights = Game.Lights;
            if (lights == null || Game.Level == null) return "no level";
            var field = lights.Field;
            var sb = new StringBuilder();
            sb.AppendLine($"field {field.W}x{field.D} cells, {field.BakedCount} static lights, {field.Moving.Count} moving; bake {field.BakeMs:0} ms, {field.Linecasts} linecasts, {field.FullCells} full cells");
            var rng = new System.Random(seed);
            var pts = Sample(field, points, rng);
            bool ok = Compare("as lit", field, pts, sb);

            // a third of the static lamps off (Smother, gas valves): read live, nothing re-bakes
            var off = new List<GameLight>();
            foreach (var l in lights.All) if (l && l.On && !ExposureField.Moves(l) && rng.NextDouble() < 0.33) { l.On = false; off.Add(l); }
            ok &= Compare($"{off.Count} lamps off", field, pts, sb);
            foreach (var l in off) l.On = true;

            // a lamp moves 1.5 m: only its cells re-bake
            GameLight moved = null;
            foreach (var l in lights.All) if (l && l.On && !ExposureField.Moves(l) && l.Kind != LightKind.Moon) { moved = l; break; }
            if (moved != null)
            {
                var was = moved.transform.position;
                moved.transform.position = was + new Vector3(1.5f, 0f, 0.5f);
                Physics.SyncTransforms();
                int before = field.Rebakes;
                // a fresh frame for the field: Sync runs at most once a frame
                ok &= CompareNear($"{moved.name} moved", field, moved, rng, sb, forceSync: true);
                sb.AppendLine($"  re-bakes {field.Rebakes - before}");
                moved.transform.position = was;
                Physics.SyncTransforms();
            }
            sb.AppendLine(ok ? "RESULT PASS" : "RESULT FAIL");
            return sb.ToString();
        }

        static List<Vector3> Sample(ExposureField field, int n, System.Random rng)
        {
            var b = Game.Level.WorldBounds;
            var res = new List<Vector3>(n);
            for (int tries = 0; res.Count < n && tries < n * 20; tries++)
            {
                var want = new Vector3(b.min.x + (float)rng.NextDouble() * b.size.x, 0f, b.min.z + (float)rng.NextDouble() * b.size.z);
                if (!OnTop(field, ref want) || !NavMesh.SamplePosition(want, out var hit, 0.6f, NavMesh.AllAreas)) continue;
                res.Add(hit.position);
            }
            return res;
        }

        /// <summary>Lifts a point to its tile's walkable top, so sampling finds the street or the roof and not the navmesh
        /// sealed inside a building's block (an unreachable island).</summary>
        static bool OnTop(ExposureField field, ref Vector3 p)
        {
            int i = ExposureGrid.CellIndex(p.x), j = ExposureGrid.CellIndex(p.z);
            if (i < 0 || j < 0) return false;
            int tx = ExposureGrid.TileX(i), ty = ExposureGrid.TileY(j, field.Grid.H);
            if (!field.Grid.In(tx, ty)) return false;
            p.y = field.Grid.Top(tx, ty);
            return true;
        }

        static bool Compare(string label, ExposureField field, List<Vector3> pts, StringBuilder sb)
        {
            var lights = Game.Lights;
            int bad = 0, fb0 = field.Fallbacks, covered = 0, lit = 0;
            float worst = 0f, worstF = 0f, worstA = 0f; Vector3 worstAt = default;
            var tf = new System.Diagnostics.Stopwatch();
            var ta = new System.Diagnostics.Stopwatch();
            foreach (var p in pts)
            {
                if (field.Covers(p)) covered++;
                tf.Start(); float f = field.LightAt(p); tf.Stop();
                ta.Start(); float a = lights.LightAt(p); ta.Stop();
                if (a >= DetectionMath.ExposedAt) lit++;
                float e = Mathf.Abs(f - a);
                if (e > Tolerance) bad++;
                if (e > worst) { worst = e; worstAt = p; worstF = f; worstA = a; }
            }
            int n = Mathf.Max(1, pts.Count);
            sb.AppendLine($"{label}: {pts.Count} points ({lit} exposed), covered {covered * 100f / n:0.0}%, fallbacks {field.Fallbacks - fb0}, " +
                          $"over {Tolerance}: {bad}, worst {worst:0.0000} at {worstAt} (field {worstF:0.000}, LightAt {worstA:0.000}); read {tf.Elapsed.TotalMilliseconds * 1000 / n:0.0} us vs LightAt {ta.Elapsed.TotalMilliseconds * 1000 / n:0.0} us");
            return bad == 0;
        }

        static bool CompareNear(string label, ExposureField field, GameLight l, System.Random rng, StringBuilder sb, bool forceSync)
        {
            if (forceSync) ForceNextFrame(field);
            var pts = new List<Vector3>();
            var c = l.transform.position;
            for (int tries = 0; pts.Count < 2000 && tries < 20000; tries++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble()) * (l.Radius + 1f);
                var want = c + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (OnTop(field, ref want) && NavMesh.SamplePosition(want, out var hit, 0.6f, NavMesh.AllAreas)) pts.Add(hit.position);
            }
            return Compare(label, field, pts, sb);
        }

        /// <summary>Sync runs once a frame; the check runs inside one frame, so it lets the field look again.</summary>
        static void ForceNextFrame(ExposureField field) => field.Resync();
    }
}
