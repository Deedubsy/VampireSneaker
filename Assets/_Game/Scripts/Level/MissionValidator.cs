using System.Collections.Generic;
using UnityEngine;
using Vespertine.Data;

namespace Vespertine.Level
{
    /// <summary>
    /// Static content checks for a parsed mission file: references, ids, placement and vocabulary.
    /// Used by the EditMode tests (every Resources/Missions file) and the debug console ("validate").
    /// Returns human-readable problems; an empty list means the file is structurally sound.
    /// </summary>
    public static class MissionValidator
    {
        public static readonly HashSet<string> EntityKinds = new HashSet<string>
        {
            "player", "npc", "corpse", "light", "prop", "hide", "door", "gate", "valve", "generator", "lever", "note", "secret",
            "item", "bell", "window", "boat", "use", "spawn", "listen", "trap", "zone", "stain", "squad"
        };

        public static readonly HashSet<string> ObjectiveTypes = new HashSet<string>
        {
            "reach", "escape", "deliver", "escort", "survive", "flag", "item", "interact", "interact_all", "kill", "kill_all",
            "feed", "feed_types", "dispose", "protect", "valves", "nokill", "nokill_type", "noalert", "nodetect", "nobodies", "nofeed", "feedonly", "nolockdown", "noholy",
            "take", "hpfloor", "evacuate", "break"
        };
        static readonly HashSet<string> BloodTypes = new HashSet<string> { "common", "drunk", "fevered", "soldier", "priest", "occult", "notable", "animal" };

        public static readonly HashSet<string> ScriptEvents = new HashSet<string>
        {
            "start", "enter", "complete", "objective", "fail", "kill", "feed", "down", "interact", "timer", "alert", "lockdown",
            "flag", "discover", "spotted", "secret", "evidence", "dispose", "death", "choice", "thrall", "framed", "crescendo", "broken", "routed"
        };

        public static readonly HashSet<string> ScriptActions = new HashSet<string>
        {
            "say", "bark", "toast", "hint", "document", "reveal", "objective", "complete", "fail", "flag", "unflag",
            "campaign_flag", "grant", "ferry", "lore", "group", "ungroup", "spawn", "remove", "unlock", "lock", "seal", "unseal", "route", "invite", "open", "close",
            "light", "lightgroup", "alarm", "lockdown", "timer", "canceltimer", "dawn", "kill", "wake", "investigate", "give", "vitae",
            "mark", "terror", "rumour", "tobias", "camera", "music", "sound", "checkpoint", "choice", "win", "lose", "weather", "hunt",
            "countdown", "blast", "evacuate", "swapprop", "crescendo", "rout", "routechoice"
        };

        public static readonly HashSet<string> LightKinds = new HashSet<string>
        {
            "gaslamp", "lamp", "walllamp", "sconce", "lantern", "brazier", "candle", "candles", "chandelier", "sunstone",
            "window", "moon", "moonbeam", "holy", "votive", "fire", "bonfire", "searchlight", "sunbeam", "dawn"
        };

        public static readonly HashSet<string> PropTypes = new HashSet<string>
        {
            "crate", "barrel", "well", "bed", "slab", "gurney", "bed_shroud", "slab_shroud", "slab_open", "gurney_shroud", "abbess", "vat", "still", "bricks", "table", "desk", "shelf", "bookshelf", "cabinet", "wardrobe",
            "cart", "privy", "coffin", "tub", "pew", "bench", "altar", "pillar", "statue", "tree", "sacks", "cage", "chest",
            "gravestone", "fountain", "boat", "washline", "chair", "body_pile", "rug", "lampstand", "pipes", "machine", "gasholder", "gasholder_wreck", "seats", "curtain", "scenery", "balustrade"
        };

        // script actions whose first argument names an entity id
        static readonly HashSet<string> EntityArgActions = new HashSet<string> { "bark", "unlock", "lock", "seal", "unseal", "route", "open", "close", "kill", "wake", "investigate", "remove", "invite", "hunt", "rout" };

        /// <summary>Flags the mission controller sets at start from the campaign: rumour / terror (whichever runs two
        /// ahead), every active Dossier countermeasure (cm_*), every campaign flag as cf_&lt;flag&gt;, and Tobias's fate
        /// (tobias_alive / tobias_thrall / tobias_lost).</summary>
        public static bool IsWorldFlag(string n) => n == "rumour" || n == "terror" || n.StartsWith("cm_") || n.StartsWith("cf_") || n.StartsWith("tobias_");

        public static List<string> Validate(LevelData d)
        {
            var p = new List<string>();
            foreach (var e in d.Errors) p.Add("parse: " + e);
            if (d.Width == 0 || d.Height == 0) { p.Add("map is empty"); return p; }
            if (string.IsNullOrEmpty(d.Get("id"))) p.Add("header: missing id");
            if (string.IsNullOrEmpty(d.Get("title"))) p.Add("header: missing title");

            var grid = new LevelGrid(d);
            var ids = new HashSet<string>();
            var npcIds = new HashSet<string>();
            var prisonerIds = new HashSet<string>();
            foreach (var e in d.Entities) if (e.Kind == "npc" && !string.IsNullOrEmpty(e.Id)) { npcIds.Add(e.Id); if (e.Has("prisoner")) prisonerIds.Add(e.Id); }
            var groups = new HashSet<string>();
            int players = 0;

            foreach (var e in d.Entities)
            {
                string at = $"line {e.Line} ({e.Kind} {e.Id})";
                if (!EntityKinds.Contains(e.Kind)) { p.Add($"{at}: unknown entity kind '{e.Kind}'"); continue; }
                if (!string.IsNullOrEmpty(e.Id))
                {
                    if (!ids.Add(e.Id)) p.Add($"{at}: duplicate id '{e.Id}'");
                }
                if (e.Group != null) groups.Add(e.Group);
                int x = Mathf.RoundToInt(e.X), y = Mathf.RoundToInt(e.Y);
                if (e.Kind != "zone" && (x < 0 || y < 0 || x >= d.Width || y >= d.Height)) p.Add($"{at}: out of bounds ({x},{y})");

                switch (e.Kind)
                {
                    case "player":
                        players++;
                        if (!grid.WalkTop(x, y)) p.Add($"{at}: player starts on an unwalkable cell '{d.CellAt(x, y)}'");
                        break;
                    case "npc":
                    case "corpse":
                    {
                        string type = e.Type ?? e.Opt("type");
                        if (e.Kind == "npc" && !Archetypes.Exists(type)) p.Add($"{at}: unknown archetype '{type}'");
                        if (e.Kind == "corpse" && type != null && !Archetypes.Exists(type)) p.Add($"{at}: unknown corpse type '{type}'");
                        if (!grid.WalkTop(x, y)) p.Add($"{at}: placed on an unwalkable cell '{d.CellAt(x, y)}'");
                        var route = e.Opt("route");
                        if (route != null && !d.Routes.ContainsKey(route)) p.Add($"{at}: unknown route '{route}'");
                        break;
                    }
                    case "light":
                        if (e.Type != null && !LightKinds.Contains(e.Type)) p.Add($"{at}: unknown light kind '{e.Type}'");
                        break;
                    case "prop":
                        if (e.Type != null && !PropTypes.Contains(e.Type)) p.Add($"{at}: unknown prop type '{e.Type}'");
                        break;
                    case "use":
                        if (e.Opt("prop") != null && !PropTypes.Contains(e.Opt("prop"))) p.Add($"{at}: unknown prop type '{e.Opt("prop")}'");
                        break;
                    case "hide":
                        if (!PropTypes.Contains(e.Opt("type", "crate"))) p.Add($"{at}: unknown hide type '{e.Opt("type")}'");
                        break;
                    case "spawn":
                        if (!grid.WalkTop(x, y)) p.Add($"{at}: reinforcement point on an unwalkable cell '{d.CellAt(x, y)}'");
                        if (e.Opt("type") != null && !Archetypes.Exists(e.Opt("type"))) p.Add($"{at}: unknown archetype '{e.Opt("type")}'");
                        break;
                    case "listen":
                        if (e.Args.Count < 2) p.Add($"{at}: a listen point needs a title and at least one line");
                        break;
                    case "zone":
                        if (e.OptInt("w", 1) <= 0 || e.OptInt("h", 1) <= 0) p.Add($"{at}: zone has no area");
                        break;
                }
            }
            if (players != 1) p.Add($"expected exactly one player, found {players}");
            // ids the runtime derives: a prisoner's shackles and what becomes of them, a sunstone lamp's smash point
            foreach (var e in d.Entities)
            {
                if (string.IsNullOrEmpty(e.Id)) continue;
                if (e.Kind == "npc" && e.Has("prisoner")) { ids.Add(e.Id + ".free"); ids.Add(e.Id + ".out"); ids.Add(e.Id + ".loose"); }
                if (e.Kind == "npc" && e.Opt("evac") != null) ids.Add(e.Id + ".evac");
                if (e.Kind == "light" && e.Type == "sunstone" && !e.Has("nosmash")) ids.Add(e.Id + ".smash");
            }
            // a gas main or generator must feed some lamps
            var lightGroups = new HashSet<string>();
            foreach (var e in d.Entities) if (e.Kind == "light" && e.Opt("group") != null) lightGroups.Add(e.Opt("group"));
            foreach (var e in d.Entities)
                if ((e.Kind == "valve" || e.Kind == "generator") && e.Opt("group") != null && !lightGroups.Contains(e.Opt("group")))
                    p.Add($"line {e.Line} ({e.Kind} {e.Id}): no light has group={e.Opt("group")}");

            // cross references from entity options
            foreach (var e in d.Entities)
            {
                string at = $"line {e.Line} ({e.Kind} {e.Id})";
                // a searchlight needs a living operator and cells to sweep
                if (e.Kind == "light" && e.Type == "searchlight")
                {
                    var op = e.Opt("op");
                    if (op != null && !npcIds.Contains(op)) p.Add($"{at}: op= references unknown npc '{op}'");
                    var sw = e.Opt("sweep", "").Split(',');
                    if (sw.Length < 2 || sw.Length % 2 != 0) p.Add($"{at}: searchlight needs sweep=x1,y1[,x2,y2...]");
                    if (e.Opt("from") == null) p.Add($"{at}: searchlight needs from=x,y[,h] (its tower)");
                }
                // squad=: a squad entity; a squad man walks the squad's wedge, not a follow= heel or a paired= beat
                if (e.Kind == "npc" && e.Opt("squad") != null)
                {
                    if (!d.Entities.Exists(x => x.Kind == "squad" && x.Id == e.Opt("squad"))) p.Add($"{at}: squad= references unknown squad '{e.Opt("squad")}'");
                    if (e.Opt("follow") != null || e.Opt("paired") != null) p.Add($"{at}: a squad man can't also have follow= or paired=");
                }
                if (e.Kind == "squad")
                {
                    int men = d.Entities.FindAll(x => x.Kind == "npc" && x.Opt("squad") == e.Id).Count;
                    if (men < 2) p.Add($"{at}: a squad needs at least two men (npc … squad={e.Id})");
                    if (d.Entities.FindAll(x => x.Kind == "npc" && x.Opt("squad") == e.Id && x.Has("lead")).Count > 1) p.Add($"{at}: more than one man marked lead");
                    if (!grid.WalkTop(Mathf.RoundToInt(e.X), Mathf.RoundToInt(e.Y))) p.Add($"{at}: rally point on an unwalkable cell");
                }
                foreach (var key in new[] { "gate", "door", "target", "home", "paired", "follow" })
                {
                    var r = e.Opt(key);
                    if (r == null) continue;
                    foreach (var one in key == "home" ? r.Split(',') : new[] { r })
                        if (!ids.Contains(one)) p.Add($"{at}: {key}= references unknown id '{one}'");
                }
            }

            // conversations: every speaker (speakers= and each "who: line" prefix) must be an npc
            foreach (var e in d.Entities)
            {
                if (e.Kind != "listen") continue;
                var speakers = new HashSet<string>(e.Opt("speakers", "").Split(','));
                speakers.Remove("");
                string first = e.Opt("speakers", "").Split(',')[0];
                for (int i = 1; i < e.Args.Count; i++) speakers.Add(Eavesdrop.ParseLine(e.Args[i], first).Who ?? "");
                foreach (var s in speakers)
                    if (!d.Entities.Exists(x => x.Kind == "npc" && x.Id == s)) p.Add($"line {e.Line} (listen {e.Id}): speaker '{s}' is not an npc");
            }

            // routes
            foreach (var r in d.Routes.Values)
                foreach (var w in r.Points)
                {
                    int x = Mathf.RoundToInt(w.X), y = Mathf.RoundToInt(w.Y);
                    if (!grid.WalkTop(x, y)) p.Add($"route {r.Id}: waypoint ({x},{y}) is not walkable '{d.CellAt(x, y)}'");
                }

            // objectives
            var objIds = new HashSet<string>();
            bool anyPrimary = false;
            foreach (var o in d.Objectives)
            {
                if (!objIds.Add(o.Id)) p.Add($"objective {o.Id}: duplicate id");
                if (!ObjectiveTypes.Contains(o.Type)) p.Add($"objective {o.Id}: unknown type '{o.Type}'");
                if (o.Category == "primary") anyPrimary = true;
                if (o.Type == "break")
                    foreach (var a in o.Args)
                        if (!d.Entities.Exists(x => x.Kind == "squad" && x.Id == a)) p.Add($"objective {o.Id}: '{a}' is not a squad");
                if (o.Type == "interact" || o.Type == "interact_all" || o.Type == "kill" || o.Type == "kill_all" || o.Type == "protect" || o.Type == "valves" || o.Type == "take")
                    foreach (var a in o.Args)
                        if (a != "any" && !(o.Type == "kill" && a == "accident") && !ids.Contains(a)) p.Add($"objective {o.Id}: references unknown id '{a}'");
                // a deliver/feed objective names a person: a typo would leave it impossible to complete
                if (o.Type == "deliver" && (o.Args.Count < 3 || !npcIds.Contains(o.Args[0])))
                    p.Add($"objective {o.Id}: deliver needs an npc id and an area (deliver <npc> x y [w h])" + (o.Args.Count > 0 && !npcIds.Contains(o.Args[0]) ? $"; '{o.Args[0]}' is not an npc" : ""));
                if (o.Type == "feed" && o.Args.Count > 0 && o.Args[0] != "any" && !npcIds.Contains(o.Args[0]) && !Archetypes.Exists(o.Args[0]))
                    p.Add($"objective {o.Id}: feed references '{o.Args[0]}', which is neither an npc nor an archetype");
                if (o.Type == "evacuate")
                    foreach (var a in o.Args)
                        if (!d.Entities.Exists(x => x.Kind == "npc" && x.Id == a && x.Opt("evac") != null)) p.Add($"objective {o.Id}: evacuate '{a}' is not an npc with evac=x,y");
                if (o.Type == "escort")
                {
                    int k = 0;
                    for (; k < o.Args.Count && !float.TryParse(o.Args[k], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _); k++)
                        if (!prisonerIds.Contains(o.Args[k])) p.Add($"objective {o.Id}: escort '{o.Args[k]}' is not a prisoner npc");
                    if (k == 0) p.Add($"objective {o.Id}: escort needs at least one prisoner id");
                    if (o.Args.Count < k + 2) p.Add($"objective {o.Id}: escort needs an area (escort <npc...> x y [w h])");
                }
                if (o.Type == "reach" || o.Type == "escape" || o.Type == "deliver")
                {
                    int first = o.Type == "deliver" ? 1 : 0;
                    if (o.Args.Count < first + 2) p.Add($"objective {o.Id}: {o.Type} needs an area (x y [w h])");
                    for (int i = first; i < o.Args.Count && i < first + 4; i++)
                        if (!float.TryParse(o.Args[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                            p.Add($"objective {o.Id}: {o.Type} area value '{o.Args[i]}' is not a number");
                }
                if (o.Type == "feedonly" || o.Type == "feed_types")
                {
                    if (o.Args.Count == 0) p.Add($"objective {o.Id}: {o.Type} needs at least one blood type");
                    foreach (var a in o.Args) if (!BloodTypes.Contains(a)) p.Add($"objective {o.Id}: unknown blood type '{a}'");
                }
            }
            if (!anyPrimary) p.Add("no primary objective");

            // script
            var flags = new HashSet<string>();
            foreach (var r in d.Script)
                foreach (var a in r.Actions)
                {
                    if (a.Count > 1 && a[0].ToLowerInvariant() == "flag") flags.Add(a[1]);
                    // choice <id> "prompt" key "text" …: each answer sets the flag <id>.<key>
                    if (a.Count > 1 && a[0].ToLowerInvariant() == "choice")
                        for (int i = 3; i + 1 < a.Count; i += 2) flags.Add(a[1] + "." + a[i]);
                }
            foreach (var o in d.Objectives)
                if (o.Opts.TryGetValue("if", out var oc))
                    foreach (var c in oc.Split(','))
                        if (!IsWorldFlag(c.TrimStart('!'))) p.Add($"objective {o.Id}: if='{c}' is not a world flag (rumour, terror, cm_*, cf_*)");
            // a hidden objective stays off the list until a rule reveals it: without one the player is never told
            foreach (var o in d.Objectives)
                if (o.Hidden && !d.Script.Exists(r => r.Actions.Exists(a => a.Count > 1 && (a[0] == "reveal" || a[0] == "objective") && a[1] == o.Id)))
                    p.Add($"objective {o.Id}: hidden, but no script rule reveals it");
            foreach (var r in d.Script)
            {
                foreach (var c in r.Conditions)
                {
                    var n = c.TrimStart('!');
                    if (!objIds.Contains(n) && !flags.Contains(n) && !IsWorldFlag(n)) p.Add($"script line {r.Line}: condition '{c}' is neither an objective nor a flag the script sets");
                }
                if (!ScriptEvents.Contains(r.Event)) p.Add($"script line {r.Line}: unknown event '{r.Event}'");
                if ((r.Event == "interact" || r.Event == "secret") && !string.IsNullOrEmpty(r.Arg) && r.Arg != "*" && r.Arg != "any" && !ids.Contains(r.Arg)
                    && !(r.Arg.EndsWith(".sprung") && d.Entities.Exists(e => e.Kind == "trap" && e.Id + ".sprung" == r.Arg)))
                    p.Add($"script line {r.Line}: event references unknown id '{r.Arg}'");
                if (r.Event == "feed" && !string.IsNullOrEmpty(r.Arg) && r.Arg != "any" && !npcIds.Contains(r.Arg) && !Archetypes.Exists(r.Arg))
                    p.Add($"script line {r.Line}: feed references '{r.Arg}', which is neither an npc nor an archetype");
                if ((r.Event == "down" || r.Event == "thrall") && !string.IsNullOrEmpty(r.Arg) && !ids.Contains(r.Arg))
                    p.Add($"script line {r.Line}: {r.Event} references unknown npc '{r.Arg}'");
                if (r.Event == "choice" && !string.IsNullOrEmpty(r.Arg) && !flags.Contains(r.Arg))
                    p.Add($"script line {r.Line}: choice references '{r.Arg}', which no choice action offers (<id>.<key>)");
                if (r.Event == "enter" && !string.IsNullOrEmpty(r.Arg) && !ids.Contains(r.Arg))
                    p.Add($"script line {r.Line}: enter references unknown zone '{r.Arg}'");
                if ((r.Event == "complete" || r.Event == "objective") && !string.IsNullOrEmpty(r.Arg) && !objIds.Contains(r.Arg))
                    p.Add($"script line {r.Line}: references unknown objective '{r.Arg}'");
                foreach (var a in r.Actions)
                {
                    if (a.Count == 0) continue;
                    var act = a[0].ToLowerInvariant();
                    if (!ScriptActions.Contains(act)) { p.Add($"script line {r.Line}: unknown action '{a[0]}'"); continue; }
                    if (EntityArgActions.Contains(act) && a.Count > 1 && !ids.Contains(a[1]) && !groups.Contains(a[1]))
                        p.Add($"script line {r.Line}: {act} references unknown id '{a[1]}'");
                    if (act == "choice" && a.Count < 5)
                        p.Add($"script line {r.Line}: choice needs <id> \"prompt\" and at least one key \"text\" pair");
                    if (act == "grant" && a.Count > 1 && Skills.Get(a[1]) == null)
                        p.Add($"script line {r.Line}: grant references unknown skill '{a[1]}'");
                    if (act == "route" && (a.Count < 3 || !d.Routes.ContainsKey(a[2])))
                        p.Add($"script line {r.Line}: route references unknown route '{(a.Count > 2 ? a[2] : "")}'");
                    if (act == "routechoice" && (a.Count < 4 || !ids.Contains(a[2]) || !ids.Contains(a[3])))
                        p.Add($"script line {r.Line}: routechoice needs <id> <safe zone> <other zone>");
                    if ((act == "group" || act == "ungroup") && a.Count > 1 && !groups.Contains(a[1]))
                        p.Add($"script line {r.Line}: {act} references unknown group '{a[1]}'");
                    if (act == "swapprop")
                    {
                        if (a.Count < 4 || !PropTypes.Contains(a[3])) p.Add($"script line {r.Line}: swapprop needs x y <prop type>");
                        else if (!float.TryParse(a[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sx)
                              || !float.TryParse(a[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sy)
                              || !d.Entities.Exists(e => e.Kind == "prop" && Mathf.Abs(e.X - sx) < 0.6f && Mathf.Abs(e.Y - sy) < 0.6f))
                            p.Add($"script line {r.Line}: swapprop: no prop at {a[1]},{a[2]}");
                    }
                    if ((act == "complete" || act == "fail" || act == "reveal" || act == "objective") && a.Count > 1 && !objIds.Contains(a[1]))
                        p.Add($"script line {r.Line}: {act} references unknown objective '{a[1]}'");
                }
            }
            return p;
        }
    }
}
