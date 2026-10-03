using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Vespertine.Level
{
    /// <summary>Parses the text mission format described in Docs/LEVEL_DESIGN.md. Pure C#; never throws on content
    /// errors — problems are collected in <see cref="LevelData.Errors"/>.</summary>
    public static class MapParser
    {
        // Positional schema per entity kind: names of positional args before free args/flags.
        static readonly Dictionary<string, string[]> Schema = new Dictionary<string, string[]>
        {
            { "player", new[] { "x", "y" } },
            { "npc", new[] { "id", "type", "x", "y" } },
            { "light", new[] { "id", "type", "x", "y" } },
            { "prop", new[] { "type", "x", "y" } },
            { "hide", new[] { "id", "x", "y" } },
            { "door", new[] { "id", "x", "y" } },
            { "valve", new[] { "id", "x", "y" } },
            { "generator", new[] { "id", "x", "y" } },
            { "use", new[] { "id", "x", "y" } },
            { "lever", new[] { "id", "x", "y" } },
            { "gate", new[] { "id", "x", "y" } },
            { "note", new[] { "id", "x", "y" } },
            { "secret", new[] { "id", "x", "y" } },
            { "item", new[] { "id", "type", "x", "y" } },
            { "bell", new[] { "id", "x", "y" } },
            { "spawn", new[] { "id", "x", "y" } },
            { "zone", new[] { "id", "x", "y" } },
            { "boat", new[] { "id", "x", "y" } },
            { "window", new[] { "id", "x", "y" } },
            { "listen", new[] { "id", "x", "y" } },
            { "trap", new[] { "id", "x", "y" } },
            { "squad", new[] { "id", "x", "y" } },
            { "stain", new[] { "x", "y" } },
            { "corpse", new[] { "id", "x", "y" } },
        };

        public static LevelData Parse(string text)
        {
            var data = new LevelData();
            if (string.IsNullOrEmpty(text)) { data.Errors.Add("empty map text"); return data; }

            string section = null;
            string group = null;
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                int lineNo = i + 1;
                if (raw.StartsWith("#!")) continue;
                var trimmed = raw.Trim();
                if (trimmed.StartsWith("@"))
                {
                    section = trimmed.Substring(1).Trim().ToLowerInvariant();
                    continue;
                }
                if (section == "map")
                {
                    // Map rows are taken verbatim (spaces are meaningful). Blank rows are void rows,
                    // but trailing blank lines at the end of the section are trimmed later.
                    data.Rows.Add(StripInlineComment(raw).TrimEnd());
                    continue;
                }
                var line = StripInlineComment(raw).Trim();
                if (line.Length == 0) continue;
                try
                {
                    switch (section)
                    {
                        case "mission": ParseHeader(data, line, lines, ref i); break;
                        case "legend": ParseLegend(data, line, lineNo); break;
                        case "entities": ParseEntityLine(data, line, lineNo, ref group); break;
                        case "objectives": ParseObjective(data, line, lineNo); break;
                        case "script": ParseScript(data, line, lineNo); break;
                        default: data.Errors.Add($"line {lineNo}: content outside a section"); break;
                    }
                }
                catch (Exception ex)
                {
                    data.Errors.Add($"line {lineNo}: {ex.Message}");
                }
            }

            while (data.Rows.Count > 0 && data.Rows[data.Rows.Count - 1].Length == 0) data.Rows.RemoveAt(data.Rows.Count - 1);
            int w = 0;
            foreach (var r in data.Rows) w = Math.Max(w, r.Length);
            data.Width = w;
            for (int r = 0; r < data.Rows.Count; r++) data.Rows[r] = data.Rows[r].PadRight(w);
            if (data.Rows.Count == 0) data.Errors.Add("no @map section");
            Validate(data);
            return data;
        }

        static string StripInlineComment(string s)
        {
            // "#!" starts a comment anywhere (never valid map/entity content).
            int idx = s.IndexOf("#!", StringComparison.Ordinal);
            return idx >= 0 ? s.Substring(0, idx) : s;
        }

        static void ParseHeader(LevelData data, string line, string[] lines, ref int i)
        {
            int eq = line.IndexOf('=');
            if (eq < 0) throw new FormatException($"expected key = value, got '{line}'");
            var key = line.Substring(0, eq).Trim().ToLowerInvariant();
            var value = line.Substring(eq + 1).Trim();
            if (value == "|")
            {
                // Multi-line block: continues until a line that is exactly "|".
                var sb = new StringBuilder();
                while (++i < lines.Length)
                {
                    var l = lines[i].TrimEnd();
                    if (l.Trim() == "|") break;
                    // a section header means the closing "|" is missing: report it and let the section parse
                    if (l.StartsWith("@")) { data.Errors.Add($"header '{key}': multi-line block is not closed with '|'"); i--; break; }
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(l.Trim());
                }
                value = sb.ToString();
            }
            data.Header[key] = value;
        }

        static void ParseLegend(LevelData data, string line, int lineNo)
        {
            int eq = line.IndexOf('=');
            if (eq != 1) throw new FormatException("legend entries must be 'c = kind'");
            data.Legend[line[0]] = line.Substring(eq + 1).Trim().ToLowerInvariant();
        }

        static void ParseEntityLine(LevelData data, string line, int lineNo, ref string group)
        {
            var tokens = Tokenize(line);
            if (tokens.Count == 0) return;
            var kind = tokens[0].ToLowerInvariant();
            if (kind == "group") { group = tokens.Count > 1 ? tokens[1] : null; return; }
            if (kind == "endgroup") { group = null; return; }
            if (kind == "route") { ParseRoute(data, line, lineNo); return; }

            if (!Schema.TryGetValue(kind, out var schema))
                throw new FormatException($"unknown entity kind '{kind}'");

            var e = new EntitySpec { Kind = kind, Line = lineNo, Group = group };
            int pos = 0;
            for (int t = 1; t < tokens.Count; t++)
            {
                var tok = tokens[t];
                int eq = tok.IndexOf('=');
                if (eq > 0 && !tok.StartsWith("\"")) { e.Opts[tok.Substring(0, eq).ToLowerInvariant()] = Unquote(tok.Substring(eq + 1)); continue; }
                if (pos < schema.Length)
                {
                    switch (schema[pos])
                    {
                        case "id": e.Id = tok; break;
                        case "type": e.Type = tok.ToLowerInvariant(); break;
                        case "x": e.X = ParseFloat(tok); break;
                        case "y": e.Y = ParseFloat(tok); break;
                    }
                    pos++;
                    continue;
                }
                if (tok.StartsWith("\"")) e.Args.Add(Unquote(tok));
                else if (IsNumber(tok)) e.Args.Add(tok);
                else e.Flags.Add(tok.ToLowerInvariant());
            }
            if (pos < schema.Length) throw new FormatException($"'{kind}' needs {string.Join(" ", schema)}");
            if (string.IsNullOrEmpty(e.Id))
                e.Id = kind == "player" ? "player" : $"{kind}_{e.Type ?? "x"}@{e.X.ToString(CultureInfo.InvariantCulture)},{e.Y.ToString(CultureInfo.InvariantCulture)}";
            if (data.Entities.Exists(o => o.Id == e.Id)) throw new FormatException($"duplicate entity id '{e.Id}'");
            data.Entities.Add(e);
        }

        static void ParseRoute(LevelData data, string line, int lineNo)
        {
            var parts = line.Split('|');
            var head = Tokenize(parts[0]);
            if (head.Count < 2) throw new FormatException("route needs an id");
            var route = new RouteSpec { Id = head[1], Mode = RouteMode.Loop };
            if (head.Count > 2)
            {
                switch (head[2].ToLowerInvariant())
                {
                    case "loop": route.Mode = RouteMode.Loop; break;
                    case "pingpong": route.Mode = RouteMode.PingPong; break;
                    case "once": route.Mode = RouteMode.Once; break;
                    default: throw new FormatException($"unknown route mode '{head[2]}'");
                }
            }
            for (int p = 1; p < parts.Length; p++)
            {
                var tk = Tokenize(parts[p]);
                if (tk.Count == 0) continue;
                if (tk.Count < 2) throw new FormatException($"waypoint '{parts[p].Trim()}' needs x y");
                var wp = new WaypointSpec { X = ParseFloat(tk[0]), Y = ParseFloat(tk[1]) };
                for (int t = 2; t < tk.Count; t++)
                {
                    var kv = tk[t].Split('=');
                    if (kv.Length != 2) throw new FormatException($"bad waypoint option '{tk[t]}'");
                    switch (kv[0].ToLowerInvariant())
                    {
                        case "wait": wp.Wait = ParseFloat(kv[1]); break;
                        case "look": wp.Look = ParseFacing(kv[1]); break;
                        case "act": wp.Act = kv[1].ToLowerInvariant(); break;
                        default: throw new FormatException($"unknown waypoint option '{kv[0]}'");
                    }
                }
                route.Points.Add(wp);
            }
            if (route.Points.Count == 0) throw new FormatException($"route '{route.Id}' has no points");
            data.Routes[route.Id] = route;
        }

        static void ParseObjective(LevelData data, string line, int lineNo)
        {
            var tk = Tokenize(line);
            if (tk.Count < 4) throw new FormatException("objective needs: category id \"text\" type [args]");
            var o = new ObjectiveSpec
            {
                Category = tk[0].ToLowerInvariant(),
                Id = tk[1],
                Text = Unquote(tk[2]),
                Type = tk[3].ToLowerInvariant()
            };
            if (o.Category != "primary" && o.Category != "optional" && o.Category != "hidden")
                throw new FormatException($"objective category must be primary/optional/hidden, got '{tk[0]}'");
            for (int t = 4; t < tk.Count; t++)
            {
                int eq = tk[t].IndexOf('=');
                if (eq > 0 && !tk[t].StartsWith("\"")) o.Opts[tk[t].Substring(0, eq).ToLowerInvariant()] = Unquote(tk[t].Substring(eq + 1));
                else o.Args.Add(Unquote(tk[t]));
            }
            data.Objectives.Add(o);
        }

        static void ParseScript(LevelData data, string line, int lineNo)
        {
            // on <event> [arg]: action args ; action args
            // every <event> [arg]: ...   (repeats)
            int colon = IndexOfOutsideQuotes(line, ':');
            if (colon < 0) throw new FormatException("script rule needs ':'");
            var head = Tokenize(line.Substring(0, colon));
            if (head.Count < 2 || (head[0] != "on" && head[0] != "every")) throw new FormatException("script rule must start with 'on' or 'every'");
            int ifAt = head.IndexOf("if");
            var rule = new ScriptRule { Event = head[1].ToLowerInvariant(), Arg = head.Count > 2 && ifAt != 2 ? head[2] : null, Repeat = head[0] == "every", Line = lineNo };
            if (ifAt >= 2) rule.Conditions.AddRange(head.GetRange(ifAt + 1, head.Count - ifAt - 1));
            foreach (var actText in SplitOutsideQuotes(line.Substring(colon + 1), ';'))
            {
                var at = Tokenize(actText);
                if (at.Count == 0) continue;
                for (int k = 0; k < at.Count; k++) at[k] = Unquote(at[k]);
                at[0] = at[0].ToLowerInvariant();
                rule.Actions.Add(at);
            }
            data.Script.Add(rule);
        }

        static void Validate(LevelData data)
        {
            if (!data.Entities.Exists(e => e.Kind == "player")) data.Errors.Add("no player start");
            foreach (var e in data.Entities)
            {
                if (e.Kind == "npc" && e.Opts.TryGetValue("route", out var r) && !data.Routes.ContainsKey(r))
                    data.Errors.Add($"line {e.Line}: npc '{e.Id}' references missing route '{r}'");
                if (e.X < 0 || e.Y < 0 || e.X > data.Width || e.Y > data.Height)
                    data.Errors.Add($"line {e.Line}: '{e.Id}' is outside the map ({e.X},{e.Y})");
            }
            foreach (var route in data.Routes.Values)
                foreach (var p in route.Points)
                    if (p.X < 0 || p.Y < 0 || p.X > data.Width || p.Y > data.Height)
                        data.Errors.Add($"route '{route.Id}' point ({p.X},{p.Y}) outside map");
        }

        // ---------- helpers ----------
        public static List<string> Tokenize(string s)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') { inQuote = !inQuote; sb.Append(c); continue; }
                if (!inQuote && char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list;
        }

        static int IndexOfOutsideQuotes(string s, char ch)
        {
            bool q = false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '"') q = !q;
                else if (!q && s[i] == ch) return i;
            }
            return -1;
        }

        static List<string> SplitOutsideQuotes(string s, char ch)
        {
            var res = new List<string>();
            bool q = false; int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '"') q = !q;
                else if (!q && s[i] == ch) { res.Add(s.Substring(start, i - start)); start = i + 1; }
            }
            res.Add(s.Substring(start));
            return res;
        }

        public static string Unquote(string s)
        {
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"') s = s.Substring(1, s.Length - 2);
            return s.Replace("\\n", "\n");
        }

        static bool IsNumber(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        static float ParseFloat(string s)
        {
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                throw new FormatException($"expected number, got '{s}'");
            return f;
        }

        /// <summary>Facing: N/NE/E/SE/S/SW/W/NW or degrees (0 = north, clockwise).</summary>
        public static float ParseFacing(string s)
        {
            switch (s.ToUpperInvariant())
            {
                case "N": return 0; case "NE": return 45; case "E": return 90; case "SE": return 135;
                case "S": return 180; case "SW": return 225; case "W": return 270; case "NW": return 315;
            }
            return ParseFloat(s);
        }
    }
}
