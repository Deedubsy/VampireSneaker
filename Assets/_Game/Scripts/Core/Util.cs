using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Vespertine.Core
{
    public static class Util
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);
        public static float FlatDistance(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        /// <summary>Facing in degrees (0 = north/+Z, clockwise) to a world direction.</summary>
        public static Vector3 FacingToDir(float deg) => Quaternion.Euler(0, deg, 0) * Vector3.forward;
        public static float DirToFacing(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        public static Color Hex(string hex, Color fallback = default)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        public static float ParseF(string s, float fallback = 0f) =>
            float.TryParse(s, NumberStyles.Float, Inv, out var f) ? f : fallback;

        public static int ParseI(string s, int fallback = 0) =>
            int.TryParse(s, NumberStyles.Integer, Inv, out var i) ? i : fallback;

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c ? c : go.AddComponent<T>();
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        public static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>Deterministic hash noise in [0,1).</summary>
        public static float Hash(int x, int y, int seed = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        public static string Wrap(string s, int max = 60) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        public static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        }
    }

    /// <summary>Physics layers configured by Tools/setup.cs.</summary>
    public static class Layers
    {
        public const int Ground = 8, Wall = 9, Bars = 10, Foliage = 11, Character = 12, Corpse = 13,
            Interactable = 14, Vent = 15, Water = 16, Overlay = 17, Prop = 18, Door = 19;

        public static readonly int GroundMask = 1 << Ground;
        public static readonly int WallMask = 1 << Wall;
        /// <summary>What stops sight and muffles sound: walls, and doors while they are shut (D119). Light is not stopped by a door.</summary>
        public static readonly int SightMask = (1 << Wall) | (1 << Door);
        public static readonly int VisionBlockMask = (1 << Wall) | (1 << Foliage) | (1 << Door);
        public static readonly int LightBlockMask = 1 << Wall;
        public static readonly int ClickMask = (1 << Ground) | (1 << Wall) | (1 << Character) | (1 << Corpse) | (1 << Interactable) | (1 << Prop);
        public static readonly int WalkClickMask = (1 << Ground) | (1 << Wall);
    }

    /// <summary>NavMesh areas configured by Tools/setup.cs.</summary>
    public static class NavAreas
    {
        public const int Walkable = 0, NotWalkable = 1, Jump = 2, Climb = 3, ClimbAny = 4, Leap = 5, Ladder = 6, Mist = 7, Threshold = 8;
        public const int HumanMask = (1 << Walkable) | (1 << Ladder);
        public const int VampireBaseMask = (1 << Walkable) | (1 << Jump) | (1 << Climb) | (1 << Ladder);
        public const int HumanAgent = 0, VampireAgent = 7777;
    }
}
