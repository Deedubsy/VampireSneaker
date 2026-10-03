using System.Collections.Generic;
using UnityEngine;

namespace Vespertine.Level
{
    /// <summary>Resolved tile grid (pure C#). x = column (east), y = row (south). Row 0 is north.</summary>
    public class LevelGrid
    {
        public readonly int W, H;
        public readonly TileKind[,] Kinds;
        public readonly LevelData Data;

        public LevelGrid(LevelData data)
        {
            Data = data;
            W = data.Width; H = data.Height;
            Kinds = new TileKind[W, H];
            var legend = new Dictionary<char, TileKind>(TileDefs.DefaultLegend);
            foreach (var kv in data.Legend)
            {
                if (TileDefs.TryKindByName(kv.Value, out var k)) legend[kv.Key] = k;
                else data.Errors.Add($"legend: unknown tile kind '{kv.Value}' for '{kv.Key}'");
            }
            var unknown = new HashSet<char>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char c = data.CellAt(x, y);
                    if (!legend.TryGetValue(c, out var k)) { if (unknown.Add(c)) data.Errors.Add($"map: unknown tile char '{c}'"); k = TileKind.Street; }
                    Kinds[x, y] = k;
                }
        }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public TileKind Kind(int x, int y) => In(x, y) ? Kinds[x, y] : TileKind.Void;
        public TileDef Def(int x, int y) => TileDefs.Get(Kind(x, y));
        public float Top(int x, int y) => Def(x, y).TopHeight;
        public bool WalkTop(int x, int y) => In(x, y) && Def(x, y).WalkTop;
        public bool Raised(int x, int y) => In(x, y) && Def(x, y).Raised;
        public bool Floor(int x, int y) => In(x, y) && Def(x, y).Floor;

        public Vector3 CellCenter(int x, int y) => Data.CellToWorld(x, y, Top(x, y));

        /// <summary>Unit step in world space for a grid step (dx, dy) — note rows grow southward (−Z).</summary>
        public static Vector3 StepDir(int dx, int dy) => new Vector3(dx, 0, -dy);

        public static readonly Vector2Int[] Dirs4 = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };

        /// <summary>Greedy rectangle merge over cells matching a predicate. Returns rects (x, y, w, h) in cells.</summary>
        public List<RectInt> Merge(System.Func<int, int, bool> pred, System.Func<int, int, int> key = null)
        {
            var used = new bool[W, H];
            var res = new List<RectInt>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (used[x, y] || !pred(x, y)) continue;
                    int k = key?.Invoke(x, y) ?? 0;
                    int w = 1;
                    while (x + w < W && !used[x + w, y] && pred(x + w, y) && (key?.Invoke(x + w, y) ?? 0) == k) w++;
                    int h = 1;
                    bool grow = true;
                    while (grow && y + h < H)
                    {
                        for (int i = 0; i < w; i++)
                            if (used[x + i, y + h] || !pred(x + i, y + h) || (key?.Invoke(x + i, y + h) ?? 0) != k) { grow = false; break; }
                        if (grow) h++;
                    }
                    for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) used[xx, yy] = true;
                    res.Add(new RectInt(x, y, w, h));
                }
            return res;
        }

        /// <summary>World-space bounds of a cell rect between heights y0..y1.</summary>
        public Bounds RectBounds(RectInt r, float y0, float y1)
        {
            float cs = LevelData.CellSize;
            float minX = r.x * cs, maxX = (r.x + r.width) * cs;
            float maxZ = (H - r.y) * cs, minZ = (H - r.y - r.height) * cs;
            var b = new Bounds();
            b.SetMinMax(new Vector3(minX, y0, minZ), new Vector3(maxX, y1, maxZ));
            return b;
        }
    }
}
