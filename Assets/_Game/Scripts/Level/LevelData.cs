using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Vespertine.Level
{
    /// <summary>Parsed, engine-agnostic description of a mission map (see Docs/LEVEL_DESIGN.md).</summary>
    public class LevelData
    {
        public const float CellSize = 2f;

        public readonly Dictionary<string, string> Header = new Dictionary<string, string>();
        public readonly Dictionary<char, string> Legend = new Dictionary<char, string>();
        public readonly List<string> Rows = new List<string>();
        public readonly List<EntitySpec> Entities = new List<EntitySpec>();
        public readonly Dictionary<string, RouteSpec> Routes = new Dictionary<string, RouteSpec>();
        public readonly List<ObjectiveSpec> Objectives = new List<ObjectiveSpec>();
        public readonly List<ScriptRule> Script = new List<ScriptRule>();
        public readonly List<string> Errors = new List<string>();

        public int Width { get; internal set; }
        public int Height => Rows.Count;

        public string Id => Get("id", "unknown");
        public string Title => Get("title", Id);

        public string Get(string key, string fallback = "") =>
            Header.TryGetValue(key, out var v) ? v : fallback;

        public float GetFloat(string key, float fallback) =>
            Header.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;

        public int GetInt(string key, int fallback) =>
            Header.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;

        public char CellAt(int x, int y)
        {
            if (y < 0 || y >= Rows.Count) return ' ';
            var row = Rows[y];
            return x < 0 || x >= row.Length ? ' ' : row[x];
        }

        /// <summary>Cell (column,row) centre to world XZ. Row 0 is north (max Z).</summary>
        public Vector3 CellToWorld(float x, float y, float height = 0f) =>
            new Vector3(x * CellSize + CellSize * 0.5f, height, (Height - 1 - y) * CellSize + CellSize * 0.5f);

        public Vector2 WorldToCell(Vector3 world) =>
            new Vector2(world.x / CellSize - 0.5f, Height - 1 - (world.z / CellSize - 0.5f));

        public Vector2Int WorldToCellInt(Vector3 world)
        {
            var c = WorldToCell(world);
            return new Vector2Int(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y));
        }

        public EntitySpec FindEntity(string id) => Entities.Find(e => e.Id == id);
    }

    public class EntitySpec
    {
        public string Kind;
        public string Id;
        public string Type;            // archetype / light kind / prop kind
        public float X, Y;             // cell coordinates
        public readonly List<string> Args = new List<string>();     // extra positional (text etc.)
        public readonly Dictionary<string, string> Opts = new Dictionary<string, string>();
        public readonly HashSet<string> Flags = new HashSet<string>();
        public string Group;           // dormant group (countermeasure / scripted), null = always active
        public int Line;

        public bool Has(string flag) => Flags.Contains(flag);
        public string Opt(string key, string fallback = null) => Opts.TryGetValue(key, out var v) ? v : fallback;

        public float OptFloat(string key, float fallback) =>
            Opts.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;

        public int OptInt(string key, int fallback) =>
            Opts.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;

        public string Arg(int i, string fallback = "") => i < Args.Count ? Args[i] : fallback;
        public override string ToString() => $"{Kind} {Id} {Type} ({X},{Y})";
    }

    public enum RouteMode { Loop, PingPong, Once }

    public class RouteSpec
    {
        public string Id;
        public RouteMode Mode;
        public readonly List<WaypointSpec> Points = new List<WaypointSpec>();
    }

    public class WaypointSpec
    {
        public float X, Y;
        public float Wait;
        public float? Look;            // degrees (0 = north, clockwise), null = keep walking heading
        public string Act;             // optional routine animation tag (smoke, warm, talk, lamp)
    }

    public class ObjectiveSpec
    {
        public string Id;
        /// <summary>primary | optional | hidden (a primary that stays off the list until a script "reveal").</summary>
        public string Category;
        public string Text;
        public string Type;            // reach | interact | interact_all | kill | deliver | nokill | noalert | nodetect | feed_types | escape
        public readonly List<string> Args = new List<string>();
        public readonly Dictionary<string, string> Opts = new Dictionary<string, string>();
        public bool Optional => Category == "optional";
        public bool Hidden => Category == "hidden";
    }

    public class ScriptRule
    {
        public string Event;           // start | enter | complete | fail | kill | feed | interact | timer | alert | lockdown | flag | discover
        public string Arg;
        public readonly List<List<string>> Actions = new List<List<string>>();
        public bool Repeat;
        public int Line;
        /// <summary>`if a !b`: every name must hold (a flag set or an objective complete; `!` negates). A rule whose
        /// conditions fail does not fire and is not used up.</summary>
        public readonly List<string> Conditions = new List<string>();
    }
}
