using System.Collections.Generic;
using UnityEngine;
using Vespertine.Core;
using Vespertine.Level;

namespace Vespertine.Stealth
{
    /// <summary>Pure grid math for the <see cref="ExposureField"/> (unit-tested).</summary>
    public static class ExposureGrid
    {
        /// <summary>Cell size in metres: four cells to a 2 m tile, so a cell never straddles two tiles.</summary>
        public const float Cell = 0.5f;
        public const int PerTile = 4;
        /// <summary>Lights a cell can list before it falls back to the full calculation.</summary>
        public const int Slots = 6;

        public static int CellIndex(float world) => Mathf.FloorToInt(world / Cell);
        /// <summary>The tile column holding cell column <paramref name="i"/> (i ≥ 0).</summary>
        public static int TileX(int i) => i / PerTile;
        /// <summary>The tile row holding cell row <paramref name="j"/> (j ≥ 0): cell rows grow north with world z, tile rows grow south.</summary>
        public static int TileY(int j, int tileRows) => tileRows - 1 - j / PerTile;

        /// <summary>Flat reach of a light's sphere at a sample <paramref name="dy"/> above or below its source; 0 if it doesn't reach.</summary>
        public static float FlatReach(float radius, float dy)
        {
            dy = Mathf.Abs(dy);
            return dy >= radius ? 0f : Mathf.Sqrt(radius * radius - dy * dy);
        }

        /// <summary>Squared flat distance from (x, z) to the nearest point of cell (i, j); 0 inside it.</summary>
        public static float NearestSq(float x, float z, int i, int j)
        {
            float x0 = i * Cell, z0 = j * Cell;
            float dx = x < x0 ? x0 - x : x > x0 + Cell ? x - x0 - Cell : 0f;
            float dz = z < z0 ? z0 - z : z > z0 + Cell ? z - z0 - Cell : 0f;
            return dx * dx + dz * dz;
        }

        /// <summary>A slot entry: light id + 1 (so 0 is empty), and whether walls block it for part of the cell.</summary>
        public static ushort Pack(int light, bool partial) => (ushort)(((light + 1) << 1) | (partial ? 1 : 0));
        public static int Light(ushort e) => (e >> 1) - 1;
        public static bool Partial(ushort e) => (e & 1) != 0;

        /// <summary>Puts an entry in the cell's first free slot. False if the cell is full.</summary>
        public static bool Insert(ushort[] slots, int cell, ushort entry)
        {
            int o = cell * Slots;
            for (int k = 0; k < Slots; k++)
                if (slots[o + k] == 0) { slots[o + k] = entry; return true; }
            return false;
        }

        /// <summary>Removes a light's entry from the cell, keeping the filled slots packed at the front.</summary>
        public static void Remove(ushort[] slots, int cell, int light)
        {
            int o = cell * Slots, w = 0;
            for (int k = 0; k < Slots; k++)
            {
                var e = slots[o + k];
                if (e == 0) break;
                if (Light(e) != light) slots[o + w++] = e;
            }
            for (int k = w; k < Slots; k++) slots[o + k] = 0;
        }
    }

    /// <summary>
    /// The Exposure Field (SR.3, D137): one structure for every read of the light, computed with
    /// <see cref="LightSystem.LightAt"/>'s own rules. A 0.5 m grid over the level's walkable tops lists, per cell, the
    /// static lights that reach it and whether walls block them for the whole cell, none of it, or part of it (tested at
    /// its centre 1 m up and the eight corners of the box a read may sample: just outside the cell, ±0.2 m, against <see cref="Layers.LightBlockMask"/>).
    /// <list type="bullet">
    /// <item>A read evaluates the falloff exactly at the point for the listed lights (Linecasting only where the cell is
    /// part-blocked), then max-combines moving lights (lanterns, searchlights, sunbeams) analytically, then applies
    /// Gloom, <see cref="LightSystem.GlobalScale"/> and the ambient: so it equals <c>LightAt</c> to float precision.</item>
    /// <item>Snuffing, relighting, gas valves and lamp groups only flip <see cref="GameLight.On"/>, which is read live:
    /// nothing re-bakes. A light that is added, removed, moved or resized re-bakes only its own cells; one that keeps
    /// moving is treated as moving from then on.</item>
    /// <item>Points off the grid, off a tile's walkable top (a canal, a ledge, mid-climb), or in a cell listing more
    /// than <see cref="ExposureGrid.Slots"/> lights, fall back to <c>LightAt</c>.</item>
    /// </list>
    /// Foliage (×0.25) is the reader's to apply, as <c>Npc.Perceive</c> does.
    /// </summary>
    public class ExposureField
    {
        // a cell is tested at the eight corners of the box a read may sample (just outside the cell, so its border is
        // never an untested strip, and from the lowest to the highest feet the cell answers for) plus its centre
        const float HeightTolerance = 0.2f, Outset = 0.03f, MoveSlack = 0.02f;
        const int Samples = 9;
        const int PromoteAfterMoves = 3;

        public readonly LightSystem Sys;
        public readonly LevelRuntime Level;
        public readonly LevelGrid Grid;
        /// <summary>Cells east (x) and north (z).</summary>
        public readonly int W, D;

        readonly ushort[] _slots;
        readonly bool[] _full;

        class Baked
        {
            public GameLight L;
            public int Id, Seen, Moves;
            public Vector3 Pos;
            public float Radius, Height;
            public readonly List<int> Cells = new List<int>();
        }

        readonly Dictionary<GameLight, Baked> _baked = new Dictionary<GameLight, Baked>();
        readonly List<Baked> _byId = new List<Baked>();
        readonly Stack<int> _freeIds = new Stack<int>();
        readonly HashSet<GameLight> _promoted = new HashSet<GameLight>();
        readonly List<GameLight> _moving = new List<GameLight>();
        readonly List<Baked> _gone = new List<Baked>();
        int _frame = -1, _stamp;

        // ---- stats for the equality check
        public int Linecasts, FullCells, Rebakes, Fallbacks;
        public float BakeMs;

        public ExposureField(LightSystem sys, LevelRuntime level)
        {
            Sys = sys; Level = level; Grid = level.Grid;
            W = Grid.W * ExposureGrid.PerTile; D = Grid.H * ExposureGrid.PerTile;
            _slots = new ushort[W * D * ExposureGrid.Slots];
            _full = new bool[W * D];
            Physics.SyncTransforms();
            var t = System.Diagnostics.Stopwatch.StartNew();
            Sync(true);
            BakeMs = (float)t.Elapsed.TotalMilliseconds;
        }

        public static bool Moves(GameLight l) => l.Portable || l.Kind == LightKind.Searchlight || l.Kind == LightKind.Sunbeam;

        /// <summary>Lights moving from the field's point of view (portable, beams, and static ones that kept moving).</summary>
        public IReadOnlyList<GameLight> Moving { get { Sync(); return _moving; } }

        /// <summary>Re-checks the light list now, even if it already did this frame (dev checks that move a light mid-frame).</summary>
        public void Resync() => Sync(true);

        /// <summary>Brings the field up to date with the light list: at most once a frame.</summary>
        void Sync(bool force = false)
        {
            if (!force && _frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _stamp++;
            _moving.Clear();
            foreach (var l in Sys.All)
            {
                if (!l) continue;
                if (Moves(l) || _promoted.Contains(l)) { _moving.Add(l); continue; }
                if (!_baked.TryGetValue(l, out var b)) { Bake(l); _baked[l].Seen = _stamp; continue; }
                b.Seen = _stamp;
                if ((l.transform.position - b.Pos).sqrMagnitude > MoveSlack * MoveSlack || l.Radius != b.Radius || l.Height != b.Height)
                {
                    int moves = b.Moves + 1;
                    Unbake(b);
                    if (moves >= PromoteAfterMoves) { _promoted.Add(l); _moving.Add(l); continue; }
                    Bake(l);
                    _baked[l].Moves = moves;
                    _baked[l].Seen = _stamp;
                    Rebakes++;
                }
            }
            _gone.Clear();
            foreach (var b in _baked.Values) if (b.Seen != _stamp) _gone.Add(b);
            foreach (var b in _gone) Unbake(b);
        }

        void Bake(GameLight l)
        {
            int id = _freeIds.Count > 0 ? _freeIds.Pop() : _byId.Count;
            var b = new Baked { L = l, Id = id, Pos = l.transform.position, Radius = l.Radius, Height = l.Height };
            if (id == _byId.Count) _byId.Add(b); else _byId[id] = b;
            _baked[l] = b;
            var src = l.SourcePos;
            float r = l.Radius;
            bool walls = l.Kind != LightKind.Moon;
            int i0 = Mathf.Max(0, ExposureGrid.CellIndex(src.x - r)), i1 = Mathf.Min(W - 1, ExposureGrid.CellIndex(src.x + r));
            int j0 = Mathf.Max(0, ExposureGrid.CellIndex(src.z - r)), j1 = Mathf.Min(D - 1, ExposureGrid.CellIndex(src.z + r));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    if (!Top(i, j, out float top)) continue;
                    float sy = top + 1f;
                    float reach = ExposureGrid.FlatReach(r, src.y - sy);
                    if (reach <= 0f || ExposureGrid.NearestSq(src.x, src.z, i, j) >= reach * reach) continue;
                    int lit = Samples;
                    if (walls)
                    {
                        lit = 0;
                        float x0 = i * ExposureGrid.Cell - Outset, z0 = j * ExposureGrid.Cell - Outset;
                        float x1 = x0 + ExposureGrid.Cell + 2f * Outset, z1 = z0 + ExposureGrid.Cell + 2f * Outset;
                        if (Clear(src, (x0 + x1) * 0.5f, sy, (z0 + z1) * 0.5f)) lit++;
                        for (int h = -1; h <= 1; h += 2)
                        {
                            float y = sy + h * HeightTolerance;
                            if (Clear(src, x0, y, z0)) lit++;
                            if (Clear(src, x1, y, z0)) lit++;
                            if (Clear(src, x0, y, z1)) lit++;
                            if (Clear(src, x1, y, z1)) lit++;
                        }
                        if (lit == 0) continue;
                    }
                    int c = j * W + i;
                    if (!ExposureGrid.Insert(_slots, c, ExposureGrid.Pack(id, lit < Samples)))
                    {
                        if (!_full[c]) FullCells++;
                        _full[c] = true;
                    }
                    b.Cells.Add(c);
                }
        }

        bool Clear(Vector3 src, float x, float y, float z)
        {
            Linecasts++;
            return !Physics.Linecast(src, new Vector3(x, y, z), Layers.LightBlockMask, QueryTriggerInteraction.Ignore);
        }

        void Unbake(Baked b)
        {
            foreach (int c in b.Cells) ExposureGrid.Remove(_slots, c, b.Id);
            _baked.Remove(b.L);
            _byId[b.Id] = null;
            _freeIds.Push(b.Id);
        }

        /// <summary>The walkable top of the tile holding cell (i, j), if it has ground.</summary>
        bool Top(int i, int j, out float top)
        {
            int tx = ExposureGrid.TileX(i), ty = ExposureGrid.TileY(j, Grid.H);
            top = 0f;
            if (!Grid.In(tx, ty)) return false;
            var def = Grid.Def(tx, ty);
            if (!def.HasGround) return false;
            top = def.TopHeight;
            return true;
        }

        /// <summary>The cell a standing point reads from, or false if the field can't answer for it.</summary>
        bool CellOf(Vector3 feet, out int c)
        {
            int i = ExposureGrid.CellIndex(feet.x), j = ExposureGrid.CellIndex(feet.z);
            c = -1;
            if (i < 0 || j < 0 || i >= W || j >= D) return false;
            if (!Top(i, j, out float top) || Mathf.Abs(feet.y - top) > HeightTolerance) return false;
            c = j * W + i;
            return !_full[c];
        }

        /// <summary>Does the field answer for this point itself (rather than falling back to <c>LightAt</c>)?</summary>
        public bool Covers(Vector3 feet) => CellOf(feet, out _);

        /// <summary><see cref="LightSystem.LightAt"/> at standing height (1 m above <paramref name="feet"/>), from the field.</summary>
        public float LightAt(Vector3 feet)
        {
            Sync();
            if (!CellOf(feet, out int c)) { Fallbacks++; return Sys.LightAt(feet); }
            var p = feet + Vector3.up;
            if (Sys.InDarkness(p)) return Sys.Ambient * 0.5f;
            return Mathf.Clamp01(Sys.Ambient + Best(c, p, out _) * Sys.GlobalScale);
        }

        /// <summary><see cref="LightSystem.Brightest"/> at standing height, from the field.</summary>
        public GameLight Brightest(Vector3 feet)
        {
            Sync();
            if (!CellOf(feet, out int c)) { Fallbacks++; return Sys.Brightest(feet); }
            Best(c, feet + Vector3.up, out var bl);
            return bl;
        }

        float Best(int c, Vector3 p, out GameLight bl)
        {
            float best = 0f; bl = null;
            int o = c * ExposureGrid.Slots;
            for (int k = 0; k < ExposureGrid.Slots; k++)
            {
                var e = _slots[o + k];
                if (e == 0) break;
                var l = _byId[ExposureGrid.Light(e)].L;
                if (!l || !l.On || !l.isActiveAndEnabled) continue;
                var src = l.SourcePos;
                float d = Vector3.Distance(src, p);
                if (d >= l.Radius) continue;
                float v = DetectionMath.LightFalloff(l.Intensity, l.Radius, d);
                if (v <= best) continue;
                if (ExposureGrid.Partial(e) && Physics.Linecast(src, p, Layers.LightBlockMask, QueryTriggerInteraction.Ignore)) continue;
                best = v; bl = l;
            }
            for (int k = 0; k < _moving.Count; k++)
            {
                var l = _moving[k];
                if (!l || !l.On || !l.isActiveAndEnabled) continue;
                var src = l.SourcePos;
                float d = Vector3.Distance(src, p);
                if (d >= l.Radius) continue;
                float v = DetectionMath.LightFalloff(l.Intensity, l.Radius, d);
                if (v <= best) continue;
                if (l.Kind != LightKind.Moon && Physics.Linecast(src, p, Layers.LightBlockMask, QueryTriggerInteraction.Ignore)) continue;
                best = v; bl = l;
            }
            return best;
        }

        /// <summary>Static lights listed in the field (for the equality check's report).</summary>
        public int BakedCount => _baked.Count;
    }
}
