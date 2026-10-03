#if UNITY_EDITOR || DEBUG
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Vespertine.AI;
using Vespertine.Level;

namespace Vespertine.Core
{
    /// <summary>
    /// Development check (E1): stands archetypes in rows on the most open, flat, roofless ground of the loaded map,
    /// frozen and facing the camera, so their silhouettes can be judged at play zoom. <c>Show(ids)</c> returns the
    /// layout, front row first; <c>Clear()</c> removes them.
    /// </summary>
    public static class DevLineup
    {
        static readonly List<Npc> _spawned = new List<Npc>();

        public static string Show(string ids = null, int perRow = 7, float spacing = 2.4f, float rowGap = 3f)
        {
            Clear();
            var list = string.IsNullOrEmpty(ids) ? Data.Codex.Listed.Select(a => a.Id).ToList() : ids.Split(',').Select(s => s.Trim()).ToList();
            var cam = Game.Cam;
            float yaw = cam ? cam.Yaw : 45f;
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            int rows = (list.Count + perRow - 1) / perRow;
            float w = (perRow - 1) * spacing, d = (rows - 1) * rowGap;

            // the slot offsets from the block centre; row 0 nearest the camera
            var slots = new List<Vector3>();
            for (int i = 0; i < list.Count; i++)
            {
                int r = i / perRow, c = i % perRow;
                slots.Add(right * (c * spacing - w * 0.5f) + fwd * (r * rowGap - d * 0.5f));
            }

            var lvl = Game.Level;
            var toCam = Vector3.up * 1f - Quaternion.Euler(cam ? cam.Pitch : 52f, yaw, 0) * Vector3.forward * (cam ? cam.Distance : 24f);
            var b = cam ? cam.Limits : new Bounds(Vector3.zero, Vector3.one * 200f);
            Vector3 best = Vector3.zero; int bestScore = -1;
            for (float x = b.min.x + 6f; x <= b.max.x - 6f; x += 3f)
                for (float z = b.min.z + 6f; z <= b.max.z - 6f; z += 3f)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, 0f, z), out var h0, 1f, NavMesh.AllAreas) || h0.position.y > 0.5f) continue;   // street level
                    int score = 0;
                    foreach (var s in slots)
                    {
                        var p = h0.position + s;
                        if (NavMesh.SamplePosition(p, out var h, 0.6f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - h0.position.y) < 0.3f
                            && Visible(lvl, h.position, toCam)) score++;
                    }
                    if (score > bestScore) { bestScore = score; best = h0.position; }
                }

            var spot = lvl.Data.WorldToCell(best);
            for (int i = 0; i < list.Count; i++)
            {
                if (Data.Archetypes.Get(list[i]) == null) continue;
                var spec = new EntitySpec { Kind = "npc", Id = "lineup_" + list[i], Type = list[i], X = spot.x, Y = spot.y };
                var n = Npc.Spawn(spec);
                var p = best + slots[i];
                if (NavMesh.SamplePosition(p, out var h, 1f, NavMesh.AllAreas)) p = h.position;
                if (n.Agent) n.Agent.enabled = false;
                n.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw + 180f + 35f, 0));
                n.enabled = false;
                _spawned.Add(n);
            }
            if (cam) { cam.Follow = null; cam.SnapTo(best); }
            return $"{_spawned.Count} at {best} ({bestScore}/{list.Count} slots clear): " + string.Join(" ", list.Select((id, i) => (i % perRow == 0 ? "| " : "") + id));
        }

        /// <summary>Open ground (the grid's top here is the street, not a building) with no taller cell on the line to the camera.
        /// Buildings are grid geometry without physics colliders, so this reads the grid rather than raycasting.</summary>
        static bool Visible(LevelRuntime lvl, Vector3 p, Vector3 toCam)
        {
            var c = lvl.CellOf(p);
            if (!lvl.Grid.In(c.x, c.y) || lvl.Grid.Top(c.x, c.y) > p.y + 0.3f) return false;
            for (float t = 0.05f; t <= 1f; t += 0.025f)
            {
                var q = p + Vector3.up * 1f + toCam * t;
                var qc = lvl.CellOf(q);
                if (lvl.Grid.In(qc.x, qc.y) && lvl.Grid.Top(qc.x, qc.y) > q.y) return false;
            }
            return true;
        }

        /// <summary>Close-up of one row (0 = front), with the follow released.</summary>
        public static string Frame(int row, float distance = 9f, int perRow = 7)
        {
            var cam = Game.Cam;
            int a = row * perRow, b = Mathf.Min(_spawned.Count, a + perRow);
            if (!cam || a >= b) return "no such row";
            var c = Vector3.zero;
            for (int i = a; i < b; i++) c += _spawned[i].transform.position;
            c /= b - a;
            cam.Follow = null;
            cam.SnapTo(c);
            cam.Distance = distance;
            typeof(View.TacticalCamera).GetField("_tDist", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(cam, distance);
            return string.Join(" ", _spawned.Skip(a).Take(b - a).Select(n => n.Arch.Id));
        }

        public static void Clear()
        {
            foreach (var n in _spawned) if (n) Object.Destroy(n.gameObject);
            _spawned.Clear();
        }
    }
}
#endif
