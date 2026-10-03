using System.Collections.Generic;
using UnityEngine;
using Vespertine.Core;
using Vespertine.Level;

namespace Vespertine.Visual
{
    /// <summary>Palette and runtime material cache. Base materials live in Resources/Materials (created by the editor setup).</summary>
    public static class Mats
    {
        public static class Pal
        {
            public static readonly Color Ink = Util.Hex("#0b0d14");
            public static readonly Color Ember = Util.Hex("#ffb35c");
            public static readonly Color Blood = Util.Hex("#9e1022");
            public static readonly Color BloodBright = Util.Hex("#e0283c");
            public static readonly Color Bone = Util.Hex("#e8dcc2");
            public static readonly Color Moon = Util.Hex("#8fa8d8");
            public static readonly Color Relaxed = Util.Hex("#d8dce6");
            public static readonly Color Suspicious = Util.Hex("#f2b233");
            public static readonly Color Alerted = Util.Hex("#e8324a");
            public static readonly Color Holy = Util.Hex("#fff1b8");
            public static readonly Color Sunstone = Util.Hex("#ffe58a");
            public static readonly Color Thrall = Util.Hex("#b04ae0");
            public static readonly Color Dominion = Util.Hex("#9a6bff");
            public static readonly Color Shade = Util.Hex("#4a6ad8");
            public static readonly Color Interact = Util.Hex("#7fd6c8");
        }

        static Material _lit, _overlay, _overlayXray, _overlayAdd;
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> Tex = new Dictionary<string, Texture2D>();

        public static Material LitBase => _lit ? _lit : _lit = LoadBase("Materials/Lit", "Universal Render Pipeline/Lit");
        public static Material OverlayBase => _overlay ? _overlay : _overlay = LoadBase("Materials/Overlay", "Vespertine/Overlay");
        public static Material OverlayXrayBase => _overlayXray ? _overlayXray : _overlayXray = LoadBase("Materials/OverlayXray", "Vespertine/Overlay");
        public static Material OverlayAddBase => _overlayAdd ? _overlayAdd : _overlayAdd = LoadBase("Materials/OverlayAdd", "Vespertine/Overlay");

        static Material LoadBase(string path, string shader)
        {
            var m = Resources.Load<Material>(path);
            if (m) return m;
            Debug.LogWarning($"[Mats] Missing {path}; falling back to Shader.Find({shader})");
            return new Material(Shader.Find(shader));
        }

        public static Texture2D GetTex(string key)
        {
            if (Tex.TryGetValue(key, out var t) && t) return t;
            switch (key)
            {
                case "cobble": t = ProcTex.Cobble(Util.Hex("#8a8f9c")); break;
                case "planks": t = ProcTex.Planks(Util.Hex("#9a7454")); break;
                case "tiles": t = ProcTex.Tiles(Util.Hex("#8a8a94"), Util.Hex("#5c5c66")); break;
                case "brick": t = ProcTex.Brick(Util.Hex("#8c7f7a")); break;
                case "darkbrick": t = ProcTex.Brick(Util.Hex("#7a6060"), 10, 5, 9); break;
                case "slate": t = ProcTex.Slate(Util.Hex("#7c8496")); break;
                case "dirt": t = ProcTex.Noise(Util.Hex("#8f806a"), 0.5f, 6, 3); break;
                case "grass": t = ProcTex.Noise(Util.Hex("#6f8a66"), 0.6f, 10, 5); break;
                case "water": t = ProcTex.Noise(Util.Hex("#8aa0b4"), 0.3f, 4, 8); break;
                case "carpet": t = ProcTex.Noise(Util.Hex("#b08080"), 0.25f, 16, 12); break;
                case "stone": t = ProcTex.Noise(Util.Hex("#9a98a0"), 0.35f, 8, 13); break;
                case "radial": t = ProcTex.Radial("radial", 0.0f); break;
                case "splat": t = ProcTex.Radial("splat", 0.5f, true); break;
                case "ring": t = ProcTex.Ring(); break;
                case "thinring": t = ProcTex.Ring(0.955f); break;
                default: t = Texture2D.whiteTexture; break;
            }
            Tex[key] = t;
            return t;
        }

        public static Material Lit(string key, Color color, string tex = null, float smooth = 0.15f, float metallic = 0f, Color? emission = null)
        {
            if (Cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(LitBase) { name = key };
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", GetTex(tex));
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            Cache[key] = m;
            return m;
        }

        public static Material Overlay(string key, Color color, string tex = null, bool xray = false, bool additive = false)
        {
            if (Cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(xray ? OverlayXrayBase : additive ? OverlayAddBase : OverlayBase) { name = key };
            m.SetColor("_Color", color);
            if (tex != null) m.SetTexture("_MainTex", GetTex(tex));
            Cache[key] = m;
            return m;
        }

        /// <summary>Material for a tile kind. side = vertical face material.</summary>
        public static Material ForTile(TileKind k, bool side)
        {
            switch (k)
            {
                case TileKind.Street: case TileKind.Pipe: return Lit("t_street", Util.Hex("#4a4f5e"), "cobble", 0.35f);
                case TileKind.Dirt: return Lit("t_dirt", Util.Hex("#4c4334"), "dirt", 0.05f);
                case TileKind.Grass: case TileKind.Hedge: return Lit("t_grass", Util.Hex("#33402f"), "grass", 0.05f);
                case TileKind.Wood: case TileKind.Doorway: case TileKind.Stairs: return Lit("t_wood", Util.Hex("#5a4030"), "planks", 0.2f);
                case TileKind.Bridge: return Lit("t_bridge", Util.Hex("#4e3b2b"), "planks", 0.15f);
                case TileKind.Tile: return Lit("t_tile", Util.Hex("#55565f"), "tiles", 0.45f);
                case TileKind.Carpet: return Lit("t_carpet", Util.Hex("#5a1e26"), "carpet", 0.05f);
                case TileKind.Shallow: return Lit("t_shallow", Util.Hex("#1d2a35"), "water", 0.85f);
                case TileKind.Canal: return Lit("t_canal", Util.Hex("#0e1b26"), "water", 0.92f);
                case TileKind.Wall: case TileKind.PipeUp: case TileKind.StairsUp: case TileKind.Vent:
                    return side ? Lit("t_wall_s", Util.Hex("#5a5560"), "brick", 0.1f) : Lit("t_wall_t", Util.Hex("#4a4852"), "stone", 0.1f);
                case TileKind.Gallery: return side ? Lit("t_gal_s", Util.Hex("#4a3a30"), "planks", 0.1f) : Lit("t_wood", Util.Hex("#5a4030"), "planks", 0.2f);
                case TileKind.House: return side ? Lit("t_house_s", Util.Hex("#5b4c4a"), "darkbrick", 0.1f) : Lit("t_roof2", Util.Hex("#3d3236"), "slate", 0.3f);
                case TileKind.Building: return side ? Lit("t_bld_s", Util.Hex("#4c4048"), "darkbrick", 0.1f) : Lit("t_roof", Util.Hex("#343846"), "slate", 0.3f);
                case TileKind.Tower: return side ? Lit("t_tower_s", Util.Hex("#55535c"), "brick", 0.1f) : Lit("t_tower_t", Util.Hex("#3e3c46"), "stone", 0.1f);
                case TileKind.Crates: return Lit("t_crates", Util.Hex("#6a4c30"), "planks", 0.1f);
                case TileKind.Bars: return Lit("t_iron", Util.Hex("#1e1f24"), null, 0.5f, 0.7f);
            }
            return Lit("t_default", Color.gray);
        }

        public static void ClearCache()
        {
            foreach (var m in Cache.Values) if (m) Object.Destroy(m);
            Cache.Clear();
        }
    }
}
