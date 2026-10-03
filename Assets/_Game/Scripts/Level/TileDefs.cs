using System.Collections.Generic;

namespace Vespertine.Level
{
    public enum TileKind
    {
        Void, Street, Dirt, Wood, Tile, Shallow, Canal, Bridge, Doorway, Stairs, Pipe, Grass, Carpet,
        Wall, Gallery, House, Building, Tower, PipeUp, StairsUp,
        Bars, Vent, Hedge, Crates
    }

    public enum Surface { Stone, Dirt, Wood, Water, Carpet }

    public class TileDef
    {
        public TileKind Kind;
        public string Name;
        public float Height;          // top height for raised tiles; 0 for floors
        public bool Floor;            // walkable ground-level floor
        public bool Raised;           // solid block with walkable top
        public bool BlocksVision;
        public bool BlocksMove;       // ground-level obstacle without a walkable top (bars, crates, vent)
        public bool Water;            // canal (running water)
        public Surface Surface;

        public bool HasGround => Kind != TileKind.Void && Kind != TileKind.Canal;
        public bool WalkTop => Floor || Raised;
        public float TopHeight => Raised ? Height : 0f;
    }

    public static class TileDefs
    {
        static readonly Dictionary<TileKind, TileDef> ByKind = new Dictionary<TileKind, TileDef>();
        static readonly Dictionary<string, TileKind> ByName = new Dictionary<string, TileKind>();
        public static readonly Dictionary<char, TileKind> DefaultLegend = new Dictionary<char, TileKind>
        {
            { ' ', TileKind.Void }, { '.', TileKind.Street }, { ',', TileKind.Dirt }, { '_', TileKind.Wood },
            { ':', TileKind.Tile }, { 'w', TileKind.Shallow }, { '~', TileKind.Canal }, { '=', TileKind.Bridge },
            { '#', TileKind.Wall }, { 'u', TileKind.Gallery }, { 'h', TileKind.House }, { 'H', TileKind.Building },
            { 'T', TileKind.Tower }, { '|', TileKind.Bars }, { 'v', TileKind.Vent }, { 'b', TileKind.Hedge },
            { 'x', TileKind.Crates }, { '+', TileKind.Doorway }, { 's', TileKind.Stairs }, { 'p', TileKind.Pipe },
            { 'P', TileKind.PipeUp }, { 'S', TileKind.StairsUp }, { 'g', TileKind.Grass }, { 'c', TileKind.Carpet },
        };

        static TileDefs()
        {
            Add(TileKind.Void, "void", 0, floor: false, vision: true);
            Add(TileKind.Street, "street", 0, floor: true, surface: Surface.Stone);
            Add(TileKind.Dirt, "dirt", 0, floor: true, surface: Surface.Dirt);
            Add(TileKind.Grass, "grass", 0, floor: true, surface: Surface.Dirt);
            Add(TileKind.Wood, "wood", 0, floor: true, surface: Surface.Wood);
            Add(TileKind.Tile, "tile", 0, floor: true, surface: Surface.Stone);
            Add(TileKind.Carpet, "carpet", 0, floor: true, surface: Surface.Carpet);
            Add(TileKind.Shallow, "shallow", 0, floor: true, surface: Surface.Water);
            Add(TileKind.Canal, "canal", 0, floor: false, water: true);
            Add(TileKind.Bridge, "bridge", 0, floor: true, surface: Surface.Wood);
            Add(TileKind.Doorway, "doorway", 0, floor: true, surface: Surface.Wood);
            Add(TileKind.Stairs, "stairs", 0, floor: true, surface: Surface.Wood);
            Add(TileKind.Pipe, "pipe", 0, floor: true, surface: Surface.Stone);
            Add(TileKind.Hedge, "hedge", 0, floor: true, vision: true, surface: Surface.Dirt);
            Add(TileKind.Wall, "wall", 3, raised: true, vision: true);
            Add(TileKind.PipeUp, "pipeup", 3, raised: true, vision: true);
            Add(TileKind.StairsUp, "stairsup", 3, raised: true, vision: true);
            Add(TileKind.Gallery, "gallery", 3, raised: true, vision: true, surface: Surface.Wood);
            Add(TileKind.House, "house", 4.5f, raised: true, vision: true);
            Add(TileKind.Building, "building", 6, raised: true, vision: true);
            Add(TileKind.Tower, "tower", 9, raised: true, vision: true);
            Add(TileKind.Bars, "bars", 2.5f, block: true);
            Add(TileKind.Vent, "vent", 3, raised: true, vision: true);
            Add(TileKind.Crates, "crates", 1.2f, block: true);
        }

        static void Add(TileKind k, string name, float h, bool floor = false, bool raised = false, bool vision = false,
            bool block = false, bool water = false, Surface surface = Surface.Stone)
        {
            ByKind[k] = new TileDef
            {
                Kind = k, Name = name, Height = h, Floor = floor, Raised = raised, BlocksVision = vision,
                BlocksMove = block, Water = water, Surface = surface
            };
            ByName[name] = k;
        }

        public static TileDef Get(TileKind k) => ByKind[k];
        public static bool TryKindByName(string name, out TileKind k) => ByName.TryGetValue(name, out k);
    }
}
