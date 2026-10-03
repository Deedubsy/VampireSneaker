using System.Collections.Generic;
using UnityEngine;
using Vespertine.Core;
using Vespertine.Visual;

namespace Vespertine.Level
{
    /// <summary>Builds level geometry (merged meshes + colliders) from a LevelGrid.</summary>
    public static class LevelBuilder
    {
        const float CS = LevelData.CellSize;
        public const float CanalDepth = 1.4f;
        public const float WaterLevel = -0.7f;

        public static GameObject Build(LevelGrid g, Transform parent)
        {
            var root = new GameObject("LevelGeometry");
            root.transform.SetParent(parent, false);
            var mb = new MeshBuilder();
            var rng = new System.Random(g.Data.Id.GetHashCode());
            // header `nowindows = x y w h; ...`: raised blocks that aren't buildings (a gas holder's casing, a bastion)
            var noWin = new List<RectInt>();
            foreach (var part in g.Data.Get("nowindows").Split(';'))
            {
                var n = part.Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (n.Length == 4 && int.TryParse(n[0], out var rx) && int.TryParse(n[1], out var ry) && int.TryParse(n[2], out var rw) && int.TryParse(n[3], out var rh))
                    noWin.Add(new RectInt(rx, ry, rw, rh));
            }
            bool Windowless(int x, int y) { foreach (var r in noWin) if (x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax) return true; return false; }

            // ---- floors (top faces per kind) ----
            var floors = g.Merge((x, y) => g.Def(x, y).Floor || g.Def(x, y).BlocksMove, (x, y) => (int)FloorKindForVisual(g, x, y));
            foreach (var r in floors)
            {
                var k = FloorKindForVisual(g, r.x, r.y);
                var b = g.RectBounds(r, 0, 0);
                float y = k == TileKind.Shallow ? -0.04f : 0f;
                mb.Top(Mats.ForTile(k, false), b.min.x, b.min.z, b.max.x, b.max.z, y);
            }

            // ---- canals ----
            var canals = g.Merge((x, y) => g.Kind(x, y) == TileKind.Canal);
            foreach (var r in canals)
            {
                var b = g.RectBounds(r, 0, 0);
                mb.Top(Mats.ForTile(TileKind.Canal, false), b.min.x, b.min.z, b.max.x, b.max.z, WaterLevel);
            }
            // canal banks: vertical stone faces where a canal cell borders a non-canal cell
            var bankMat = Mats.Lit("t_bank", Util.Hex("#3a3a42"), "brick", 0.3f);
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    if (g.Kind(x, y) != TileKind.Canal) continue;
                    foreach (var d in LevelGrid.Dirs4)
                    {
                        var nk = g.Kind(x + d.x, y + d.y);
                        if (nk == TileKind.Canal || nk == TileKind.Void) continue;
                        EdgeFace(g, mb, bankMat, x, y, d, -CanalDepth, 0f, inward: true);
                    }
                }

            // ---- raised blocks: tops and sides ----
            var raised = g.Merge((x, y) => g.Def(x, y).Raised, (x, y) => (int)g.Kind(x, y));
            foreach (var r in raised)
            {
                var k = g.Kind(r.x, r.y);
                var def = TileDefs.Get(k);
                var b = g.RectBounds(r, 0, def.Height);
                mb.Top(Mats.ForTile(k, false), b.min.x, b.min.z, b.max.x, b.max.z, def.Height);
            }
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    var def = g.Def(x, y);
                    if (!def.Raised) continue;
                    foreach (var d in LevelGrid.Dirs4)
                    {
                        int nx = x + d.x, ny = y + d.y;
                        var nd = g.Def(nx, ny);
                        float bottom = nd.Raised ? nd.Height : (nd.Kind == TileKind.Canal ? -CanalDepth : 0f);
                        if (!g.In(nx, ny)) bottom = 0f;
                        if (bottom >= def.Height - 0.01f) continue;
                        EdgeFace(g, mb, Mats.ForTile(def.Kind, true), x, y, d, bottom, def.Height, inward: false);
                        // windows on tall buildings
                        if ((def.Kind == TileKind.Building || def.Kind == TileKind.House || def.Kind == TileKind.Tower) && nd.Kind != def.Kind && !Windowless(x, y))
                            AddWindows(g, mb, x, y, d, def.Height, rng);
                        if (def.Kind == TileKind.Vent && nd.Floor)
                            AddGrate(g, mb, x, y, d);
                    }
                    // roof details
                    if ((def.Kind == TileKind.Building || def.Kind == TileKind.House) && rng.NextDouble() < 0.06)
                    {
                        var c = g.CellCenter(x, y);
                        mb.Box(Mats.Lit("chimney", Util.Hex("#3b3238"), "darkbrick", 0.1f), c + new Vector3((float)rng.NextDouble() - 0.5f, 0.6f, (float)rng.NextDouble() - 0.5f), new Vector3(0.6f, 1.2f, 0.6f));
                    }
                    // parapet lip on walls
                }

            // ---- ground-level obstacles ----
            var iron = Mats.ForTile(TileKind.Bars, false);
            var crateMat = Mats.ForTile(TileKind.Crates, false);
            var hedgeMat = Mats.Lit("hedge", Util.Hex("#1d2e22"), "grass", 0.05f);
            var stairMat = Mats.Lit("stairs", Util.Hex("#4a3828"), "planks", 0.15f);
            var pipeMat = Mats.Lit("pipe", Util.Hex("#2a2c30"), null, 0.6f, 0.8f);
            var ivyMat = Mats.Lit("ivy", Util.Hex("#1f3a26"), "grass", 0.05f);
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    var k = g.Kind(x, y);
                    var c = g.CellCenter(x, y);
                    switch (k)
                    {
                        case TileKind.Bars:
                        {
                            bool ew = g.Kind(x - 1, y) == TileKind.Bars || g.Kind(x + 1, y) == TileKind.Bars || g.Raised(x - 1, y) || g.Raised(x + 1, y);
                            bool ns = g.Kind(x, y - 1) == TileKind.Bars || g.Kind(x, y + 1) == TileKind.Bars || g.Raised(x, y - 1) || g.Raised(x, y + 1);
                            if (!ew && !ns) ew = true;
                            if (ew && ns) { BarsLine(mb, iron, c, true); BarsLine(mb, iron, c, false); }
                            else BarsLine(mb, iron, c, ew);
                            break;
                        }
                        case TileKind.Crates:
                        {
                            int n = 1 + rng.Next(3);
                            for (int i = 0; i < n; i++)
                            {
                                float s = 0.7f + (float)rng.NextDouble() * 0.5f;
                                var off = new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 0.6f;
                                float h = i == 0 ? 1.2f : s;
                                mb.Box(crateMat, c + off + Vector3.up * h * 0.5f, new Vector3(s, h, s), (float)rng.NextDouble() * 30f);
                            }
                            break;
                        }
                        case TileKind.Hedge:
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                var off = new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 0.8f;
                                float h = 1.4f + (float)rng.NextDouble() * 0.5f;
                                mb.Box(hedgeMat, c + off + Vector3.up * h * 0.5f, new Vector3(1.3f, h, 1.3f), (float)rng.NextDouble() * 90f);
                            }
                            break;
                        }
                        case TileKind.Stairs:
                        case TileKind.StairsUp:
                        {
                            if (FindUpNeighbour(g, x, y, out var d, out float top))
                                Stairs(mb, stairMat, g, x, y, d, g.Top(x, y), top);
                            break;
                        }
                        case TileKind.Pipe:
                        case TileKind.PipeUp:
                        {
                            if (FindUpNeighbour(g, x, y, out var d, out float top))
                            {
                                var dir = LevelGrid.StepDir(d.x, d.y);
                                var basePos = c + dir * (CS * 0.5f - 0.12f);
                                mb.Cylinder(pipeMat, basePos, 0.08f, top - g.Top(x, y) + 0.3f, 6);
                                // ivy patch around the pipe
                                var side = Vector3.Cross(Vector3.up, dir);
                                for (int i = 0; i < 4; i++)
                                {
                                    var p = basePos - dir * 0.05f + side * ((float)rng.NextDouble() - 0.5f) * 1.2f + Vector3.up * (g.Top(x, y) + (float)rng.NextDouble() * (top - g.Top(x, y)));
                                    mb.Box(ivyMat, p, new Vector3(0.5f, 0.6f, 0.5f) * (0.6f + (float)rng.NextDouble() * 0.6f), (float)rng.NextDouble() * 90);
                                }
                            }
                            break;
                        }
                    }
                }

            mb.Build(root.transform, "geo", true, Layers.Ground);
            BuildColliders(g, root.transform);
            return root;
        }

        static TileKind FloorKindForVisual(LevelGrid g, int x, int y)
        {
            var k = g.Kind(x, y);
            switch (k)
            {
                case TileKind.Bars: case TileKind.Crates:
                    // inherit floor from a neighbour floor tile
                    foreach (var d in LevelGrid.Dirs4) { var nk = g.Kind(x + d.x, y + d.y); if (g.Def(x + d.x, y + d.y).Floor && nk != TileKind.Hedge) return nk == TileKind.Doorway || nk == TileKind.Stairs || nk == TileKind.Pipe ? TileKind.Street : nk; }
                    return TileKind.Street;
                case TileKind.Hedge: return TileKind.Grass;
                case TileKind.Pipe: return TileKind.Street;
                case TileKind.Stairs: case TileKind.Doorway:
                    foreach (var d in LevelGrid.Dirs4) { var nk = g.Kind(x + d.x, y + d.y); if (nk == TileKind.Wood || nk == TileKind.Tile || nk == TileKind.Carpet) return nk; }
                    foreach (var d in LevelGrid.Dirs4) { var nk = g.Kind(x + d.x, y + d.y); if (g.Def(x + d.x, y + d.y).Floor && nk != TileKind.Stairs && nk != TileKind.Doorway && nk != TileKind.Hedge) return nk == TileKind.Pipe ? TileKind.Street : nk; }
                    return TileKind.Street;
            }
            return k;
        }

        /// <summary>Find the highest 4-neighbour that is higher than this tile's top.</summary>
        public static bool FindUpNeighbour(LevelGrid g, int x, int y, out Vector2Int dir, out float top)
        {
            dir = default; top = g.Top(x, y);
            float best = top + 0.5f; bool found = false;
            // a flight runs straight through: prefer a raised neighbour with open floor behind the stair (the
            // approach), so stairs between two tall walls climb to the floor ahead, not onto a wall top beside them
            for (int pass = 0; pass < 2 && !found; pass++)
                foreach (var d in LevelGrid.Dirs4)
                {
                    if (!g.Raised(x + d.x, y + d.y)) continue;
                    if (pass == 0 && g.Raised(x - d.x, y - d.y)) continue;
                    float t = g.Top(x + d.x, y + d.y);
                    if (t > best) { best = t; dir = d; found = true; }
                }
            if (found) top = best;
            return found;
        }

        /// <summary>Emit a vertical face on the edge of cell (x,y) in direction d. inward = face points into the cell.</summary>
        static void EdgeFace(LevelGrid g, MeshBuilder mb, Material m, int x, int y, Vector2Int d, float y0, float y1, bool inward)
        {
            var c = g.Data.CellToWorld(x, y);
            var dir = LevelGrid.StepDir(d.x, d.y);
            var edgeMid = c + dir * (CS * 0.5f);
            var normal = inward ? -dir : dir;
            var tangent = Vector3.Cross(normal, Vector3.up); // p1 - p0 must satisfy cross(up, p1-p0) = normal → p1-p0 = cross(normal, up)
            var p0 = edgeMid - tangent * (CS * 0.5f);
            var p1 = edgeMid + tangent * (CS * 0.5f);
            mb.Side(m, p0, p1, y0, y1);
        }

        static void AddWindows(LevelGrid g, MeshBuilder mb, int x, int y, Vector2Int d, float height, System.Random rng)
        {
            var c = g.Data.CellToWorld(x, y);
            var dir = LevelGrid.StepDir(d.x, d.y);
            var tangent = Vector3.Cross(dir, Vector3.up);
            for (float wy = 2.2f; wy < height - 0.8f; wy += 2.6f)
            {
                if (rng.NextDouble() < 0.35) continue;
                bool lit = rng.NextDouble() < 0.3;
                var mat = lit ? Mats.Lit("win_lit", Util.Hex("#2a1a08"), null, 0.6f, 0, Util.Hex("#ffae4a") * 1.6f)
                              : Mats.Lit("win_dark", Util.Hex("#0d1018"), null, 0.85f);
                var center = c + dir * (CS * 0.5f + 0.02f) + Vector3.up * wy;
                var p0 = center - tangent * 0.35f; var p1 = center + tangent * 0.35f;
                mb.Side(mat, p0, p1, wy - 0.55f, wy + 0.55f);
                // mullion frame
                var frame = Mats.Lit("win_frame", Util.Hex("#1a1416"), null, 0.2f);
                mb.Side(frame, center - tangent * 0.03f + dir * 0.01f, center + tangent * 0.03f + dir * 0.01f, wy - 0.55f, wy + 0.55f);
            }
        }

        static void AddGrate(LevelGrid g, MeshBuilder mb, int x, int y, Vector2Int d)
        {
            var c = g.Data.CellToWorld(x, y);
            var dir = LevelGrid.StepDir(d.x, d.y);
            var tangent = Vector3.Cross(dir, Vector3.up);
            var center = c + dir * (CS * 0.5f + 0.02f);
            var dark = Mats.Lit("grate_hole", Util.Hex("#050608"), null, 0f);
            mb.Side(dark, center - tangent * 0.55f, center + tangent * 0.55f, 0.05f, 0.9f);
            var iron = Mats.ForTile(TileKind.Bars, false);
            for (int i = -2; i <= 2; i++)
            {
                var p = center + dir * 0.03f + tangent * (i * 0.22f);
                mb.Box(iron, p + Vector3.up * 0.47f, new Vector3(0.05f, 0.85f, 0.05f));
            }
        }

        static void BarsLine(MeshBuilder mb, Material iron, Vector3 c, bool eastWest)
        {
            var axis = eastWest ? Vector3.right : Vector3.forward;
            for (float t = -0.9f; t <= 0.91f; t += 0.2f)
                mb.Box(iron, c + axis * t + Vector3.up * 1.25f, new Vector3(0.06f, 2.5f, 0.06f));
            mb.Box(iron, c + Vector3.up * 2.35f, eastWest ? new Vector3(2f, 0.08f, 0.1f) : new Vector3(0.1f, 0.08f, 2f));
            mb.Box(iron, c + Vector3.up * 0.3f, eastWest ? new Vector3(2f, 0.06f, 0.08f) : new Vector3(0.08f, 0.06f, 2f));
        }

        static void Stairs(MeshBuilder mb, Material m, LevelGrid g, int x, int y, Vector2Int d, float from, float to)
        {
            // a ladder-like steep stair hugging the higher neighbour's face
            var c = g.Data.CellToWorld(x, y);
            var dir = LevelGrid.StepDir(d.x, d.y);
            int steps = Mathf.Max(3, Mathf.RoundToInt((to - from) / 0.35f));
            float run = CS * 0.9f;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                float h = from + (to - from) * (i + 1f) / steps;
                var p = c - dir * (run * 0.5f) + dir * (run * t);
                var size = Mathf.Abs(dir.x) > 0.5f ? new Vector3(run / steps, h - from, 1.3f) : new Vector3(1.3f, h - from, run / steps);
                mb.Box(m, new Vector3(p.x, from + (h - from) * 0.5f, p.z), size);
            }
        }

        static void BuildColliders(LevelGrid g, Transform parent)
        {
            var colRoot = new GameObject("Colliders");
            colRoot.transform.SetParent(parent, false);

            // ground slabs (top at y=0) for everything with ground
            foreach (var r in g.Merge((x, y) => g.Def(x, y).HasGround))
                AddBox(colRoot.transform, "ground", g.RectBounds(r, -0.5f, 0f), Layers.Ground, false);
            // canal water surface (not walkable; used for click feedback and body dumping)
            foreach (var r in g.Merge((x, y) => g.Kind(x, y) == TileKind.Canal))
                AddBox(colRoot.transform, "water", g.RectBounds(r, WaterLevel - 0.5f, WaterLevel), Layers.Water, false);
            // raised blocks
            foreach (var r in g.Merge((x, y) => g.Def(x, y).Raised, (x, y) => Mathf.RoundToInt(g.Def(x, y).Height * 10)))
                AddBox(colRoot.transform, "raised", g.RectBounds(r, 0f, g.Def(r.x, r.y).Height), Layers.Wall, false);
            foreach (var r in g.Merge((x, y) => g.Kind(x, y) == TileKind.Bars))
                AddBox(colRoot.transform, "bars", g.RectBounds(r, 0f, 2.5f), Layers.Bars, false);
            foreach (var r in g.Merge((x, y) => g.Kind(x, y) == TileKind.Crates))
                AddBox(colRoot.transform, "crates", g.RectBounds(r, 0f, 1.2f), Layers.Prop, false);
            foreach (var r in g.Merge((x, y) => g.Kind(x, y) == TileKind.Hedge))
                AddBox(colRoot.transform, "hedge", g.RectBounds(r, 0f, 1.9f), Layers.Foliage, true);
        }

        static void AddBox(Transform parent, string name, Bounds b, int layer, bool trigger)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.position = b.center;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = b.size;
            bc.isTrigger = trigger;
            go.isStatic = true;
        }
    }
}
