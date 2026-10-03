using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Vespertine.Core;

namespace Vespertine.Level
{
    public enum LinkKind { Jump, ClimbAny, Climb, Ladder, Leap, Mist }

    public class NavLink
    {
        public LinkKind Kind;
        public Vector3 Start, End;
        public float Width;
        public int Agent;
        public bool Bidirectional;
        public NavMeshLinkInstance Instance;
        public Vector3 Axis => Width > 0 ? Vector3.Cross(Vector3.up, (End - Start).Flat().normalized) : Vector3.zero;
    }

    /// <summary>Builds runtime NavMesh data for both agent types directly from the tile grid, plus off-mesh links.</summary>
    public class NavBuilder
    {
        const float CS = LevelData.CellSize;
        readonly LevelGrid g;
        public readonly List<NavLink> Links = new List<NavLink>();
        NavMeshDataInstance _human, _vampire;
        NavMeshData _vampireData;
        Bounds _bounds;
        public readonly List<Bounds> VampireBlockers = new List<Bounds>(); // thresholds / locked doors
        public readonly List<Bounds> ExtraBlockers = new List<Bounds>();   // props (both agents)
        public bool Busy => _op != null && !_op.isDone;
        AsyncOperation _op;

        public NavBuilder(LevelGrid grid) { g = grid; }

        public void BuildAll()
        {
            _bounds = new Bounds(new Vector3(g.W * CS * 0.5f, 4f, g.H * CS * 0.5f), new Vector3(g.W * CS + 4, 20f, g.H * CS + 4));
            var t0 = Time.realtimeSinceStartup;
            _human = Add(Build(NavAreas.HumanAgent, Sources(false)));
            _vampireData = Build(NavAreas.VampireAgent, Sources(true));
            _vampire = NavMesh.AddNavMeshData(_vampireData);
            BuildLinks();
            Debug.Log($"[Nav] built in {(Time.realtimeSinceStartup - t0) * 1000f:0} ms, links={Links.Count}");
        }

        public void RebuildVampire()
        {
            _op = NavMeshBuilder.UpdateNavMeshDataAsync(_vampireData, Settings(NavAreas.VampireAgent), Sources(true), _bounds);
            _relink = true;
        }

        bool _relink;

        /// <summary>Off-mesh links lose their connection when the tiles under them are rebuilt: re-register the vampire's once the async update lands.</summary>
        public void Tick()
        {
            if (!_relink || Busy) return;
            _relink = false;
            foreach (var l in Links)
            {
                if (l.Agent != NavAreas.VampireAgent) continue;
                if (NavMesh.IsLinkValid(l.Instance)) NavMesh.RemoveLink(l.Instance);
                l.Instance = NavMesh.AddLink(LinkData(l.Kind, l.Start, l.End, l.Width, l.Agent, l.Bidirectional));
            }
        }

        public void Clear()
        {
            foreach (var l in Links) if (NavMesh.IsLinkValid(l.Instance)) NavMesh.RemoveLink(l.Instance);
            Links.Clear();
            if (_human.valid) _human.Remove();
            if (_vampire.valid) _vampire.Remove();
        }

        static NavMeshDataInstance Add(NavMeshData d) => NavMesh.AddNavMeshData(d);

        // the async rebuild must use the same settings as the first build, or the tiles won't match
        static NavMeshBuildSettings Settings(int agent)
        {
            var settings = NavMesh.GetSettingsByID(agent);
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.1f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            settings.minRegionArea = 0.4f;   // a lone 2 m wall top (a bridge arch, a gatepost) erodes to ~1 m², still standable
            return settings;
        }

        NavMeshData Build(int agent, List<NavMeshBuildSource> src) =>
            NavMeshBuilder.BuildNavMeshData(Settings(agent), src, _bounds, Vector3.zero, Quaternion.identity);

        List<NavMeshBuildSource> Sources(bool vampire)
        {
            var list = new List<NavMeshBuildSource>();
            foreach (var r in g.Merge((x, y) => g.Def(x, y).HasGround))
                list.Add(Box(g.RectBounds(r, -0.5f, 0f), NavAreas.Walkable));
            foreach (var r in g.Merge((x, y) => g.Def(x, y).Raised, (x, y) => Mathf.RoundToInt(g.Def(x, y).Height * 10)))
                list.Add(Box(g.RectBounds(r, 0f, g.Def(r.x, r.y).Height), NavAreas.Walkable));
            foreach (var r in g.Merge((x, y) => g.Def(x, y).BlocksMove))
                list.Add(Box(g.RectBounds(r, 0f, g.Def(r.x, r.y).Height), NavAreas.NotWalkable));
            foreach (var b in ExtraBlockers)
                list.Add(Box(b, NavAreas.NotWalkable));
            if (vampire)
                foreach (var b in VampireBlockers)
                    list.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.ModifierBox, size = b.size, transform = Matrix4x4.Translate(b.center), area = NavAreas.NotWalkable });
            return list;
        }

        static NavMeshBuildSource Box(Bounds b, int area) => new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box, size = b.size, transform = Matrix4x4.Translate(b.center), area = area
        };

        // ------------------------------------------------------------------ links
        void BuildLinks()
        {
            // Pipes / stairs (explicit climb points)
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    var k = g.Kind(x, y);
                    if (k != TileKind.Pipe && k != TileKind.PipeUp && k != TileKind.Stairs && k != TileKind.StairsUp) continue;
                    if (!LevelBuilder.FindUpNeighbour(g, x, y, out var d, out float top)) continue;
                    var dir = LevelGrid.StepDir(d.x, d.y);
                    var c = g.CellCenter(x, y);
                    var start = c + dir * (CS * 0.5f - 0.75f);
                    var end = g.Data.CellToWorld(x + d.x, y + d.y, top) - dir * (CS * 0.5f - 0.55f);
                    bool ladder = k == TileKind.Stairs || k == TileKind.StairsUp;
                    AddLink(ladder ? LinkKind.Ladder : LinkKind.Climb, start, end, 0f, NavAreas.VampireAgent, true);
                    if (ladder) AddLink(LinkKind.Ladder, start, end, 0f, NavAreas.HumanAgent, true);
                }

            // Drops (Jump, down) and wall-crawl (ClimbAny, up) between adjacent walk tops of different height.
            // Runs of identical edges are merged into wide links.
            for (int di = 0; di < 4; di++)
            {
                var d = LevelGrid.Dirs4[di];
                var along = new Vector2Int(Mathf.Abs(d.y), Mathf.Abs(d.x)); // perpendicular grid step
                var done = new HashSet<Vector2Int>();
                for (int y = 0; y < g.H; y++)
                    for (int x = 0; x < g.W; x++)
                    {
                        if (done.Contains(new Vector2Int(x, y)) || !EdgeDrop(x, y, d, out float hi, out float lo)) continue;
                        int run = 1;
                        while (run < 4 && EdgeDrop(x + along.x * run, y + along.y * run, d, out float h2, out float l2) && Mathf.Approximately(h2, hi) && Mathf.Approximately(l2, lo)) run++;
                        for (int i = 0; i < run; i++) done.Add(new Vector2Int(x + along.x * i, y + along.y * i));
                        float mx = x + along.x * (run - 1) * 0.5f, my = y + along.y * (run - 1) * 0.5f;
                        var dir = LevelGrid.StepDir(d.x, d.y);
                        var top = g.Data.CellToWorld(mx, my, hi) + dir * (CS * 0.5f - 0.5f);
                        var bottom = g.Data.CellToWorld(mx + d.x, my + d.y, lo) - dir * (CS * 0.5f - 0.7f);
                        float width = run * CS - 0.9f;
                        AddLink(LinkKind.Jump, top, bottom, width, NavAreas.VampireAgent, false);
                        AddLink(LinkKind.ClimbAny, bottom, top, width, NavAreas.VampireAgent, false);
                    }
            }

            // Leaps between raised tops across a 1–2 cell gap (never across running water).
            for (int di = 0; di < 2; di++)
            {
                var d = di == 0 ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
                for (int y = 0; y < g.H; y++)
                    for (int x = 0; x < g.W; x++)
                    {
                        if (!g.Raised(x, y)) continue;
                        float ha = g.Top(x, y);
                        for (int gap = 1; gap <= 2; gap++)
                        {
                            int bx = x + d.x * (gap + 1), by = y + d.y * (gap + 1);
                            if (!g.Raised(bx, by)) continue;
                            float hb = g.Top(bx, by);
                            bool ok = Mathf.Abs(ha - hb) <= 3.1f;
                            for (int k = 1; k <= gap && ok; k++)
                            {
                                int gx = x + d.x * k, gy = y + d.y * k;
                                if (g.Kind(gx, gy) == TileKind.Canal || g.Kind(gx, gy) == TileKind.Void) ok = false;
                                else if (g.Top(gx, gy) > Mathf.Min(ha, hb) - 1.5f) ok = false;
                            }
                            if (!ok) continue;
                            // only from tiles whose own continuation is the gap (avoid duplicates along long edges: every 2nd cell)
                            if (((d.x != 0 ? y : x) & 1) == 1) continue;
                            var dir = LevelGrid.StepDir(d.x, d.y);
                            var a = g.Data.CellToWorld(x, y, ha) + dir * (CS * 0.5f - 0.5f);
                            var b = g.Data.CellToWorld(bx, by, hb) - dir * (CS * 0.5f - 0.5f);
                            if (Mathf.Abs(ha - hb) <= 1.6f) AddLink(LinkKind.Leap, a, b, 0f, NavAreas.VampireAgent, true);
                            else if (ha > hb) AddLink(LinkKind.Leap, a, b, 0f, NavAreas.VampireAgent, false);
                            else AddLink(LinkKind.Leap, b, a, 0f, NavAreas.VampireAgent, false);
                            break;
                        }
                    }
            }

            // Mist passages through bars and vents.
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    var k = g.Kind(x, y);
                    if (k != TileKind.Bars && k != TileKind.Vent) continue;
                    for (int di = 0; di < 2; di++)
                    {
                        var d = di == 0 ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
                        if (g.Floor(x - d.x, y - d.y) && g.Floor(x + d.x, y + d.y) && !g.Def(x - d.x, y - d.y).BlocksMove && !g.Def(x + d.x, y + d.y).BlocksMove)
                        {
                            var a = g.Data.CellToWorld(x - d.x, y - d.y) + LevelGrid.StepDir(d.x, d.y) * 0.3f;
                            var b = g.Data.CellToWorld(x + d.x, y + d.y) - LevelGrid.StepDir(d.x, d.y) * 0.3f;
                            AddLink(LinkKind.Mist, a, b, 0f, NavAreas.VampireAgent, true);
                        }
                    }
                }
        }

        /// <summary>True if cell (x,y) is a walkable top higher than its walkable neighbour in direction d.</summary>
        bool EdgeDrop(int x, int y, Vector2Int d, out float hi, out float lo)
        {
            hi = lo = 0;
            if (!g.WalkTop(x, y) || !g.WalkTop(x + d.x, y + d.y)) return false;
            if (g.Def(x + d.x, y + d.y).BlocksMove) return false;
            hi = g.Top(x, y); lo = g.Top(x + d.x, y + d.y);
            if (hi - lo < 1.5f) return false;
            // explicit stairs/pipes already provide a route on this edge
            var nk = g.Kind(x + d.x, y + d.y);
            return true;
        }

        public static int AreaFor(LinkKind k)
        {
            switch (k)
            {
                case LinkKind.Jump: return NavAreas.Jump;
                case LinkKind.ClimbAny: return NavAreas.ClimbAny;
                case LinkKind.Climb: return NavAreas.Climb;
                case LinkKind.Ladder: return NavAreas.Ladder;
                case LinkKind.Leap: return NavAreas.Leap;
                case LinkKind.Mist: return NavAreas.Mist;
            }
            return NavAreas.Walkable;
        }

        void AddLink(LinkKind kind, Vector3 start, Vector3 end, float width, int agent, bool bidir)
        {
            var inst = NavMesh.AddLink(LinkData(kind, start, end, width, agent, bidir));
            Links.Add(new NavLink { Kind = kind, Start = start, End = end, Width = width, Agent = agent, Bidirectional = bidir, Instance = inst });
        }

        static NavMeshLinkData LinkData(LinkKind kind, Vector3 start, Vector3 end, float width, int agent, bool bidir) => new NavMeshLinkData
        {
            startPosition = start, endPosition = end, width = Mathf.Max(0, width), costModifier = -1,
            bidirectional = bidir, area = AreaFor(kind), agentTypeID = agent
        };

        /// <summary>Identify the link an agent is about to traverse and compute its actual start/end (wide links offset along their axis).</summary>
        public bool Resolve(int agent, Vector3 agentPos, Vector3 dataStart, Vector3 dataEnd, out NavLink link, out Vector3 from, out Vector3 to)
        {
            link = null; from = dataStart; to = dataEnd;
            float best = float.MaxValue;
            foreach (var l in Links)
            {
                if (l.Agent != agent) continue;
                for (int pass = 0; pass < (l.Bidirectional ? 2 : 1); pass++)
                {
                    var s = pass == 0 ? l.Start : l.End;
                    var e = pass == 0 ? l.End : l.Start;
                    // endpoints of the data must match the link's endpoints (allow wide-link offsets along the axis)
                    var axis = l.Axis;
                    var ds = dataStart - s; var de = dataEnd - e;
                    if (l.Width > 0) { ds -= axis * Vector3.Dot(ds, axis); de -= axis * Vector3.Dot(de, axis); }
                    float score = ds.sqrMagnitude + de.sqrMagnitude;
                    if (score < best) { best = score; link = l; from = s; to = e; }
                }
            }
            if (link == null || best > 1.5f) { link = null; return false; }
            if (link.Width > 0)
            {
                var axis = link.Axis;
                float off = Mathf.Clamp(Vector3.Dot(agentPos - from, axis), -link.Width * 0.5f, link.Width * 0.5f);
                from += axis * off; to += axis * off;
            }
            return true;
        }
    }
}
