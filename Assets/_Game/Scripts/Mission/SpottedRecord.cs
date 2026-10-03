using System.Collections.Generic;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Stealth;

namespace Vespertine.Mission
{
    /// <summary>
    /// One Spotted event, kept for the debrief's Detections page (SR.10): where she and the spotter stood, his cone as
    /// walls cut it, the light that lit her and the caption. Flat coordinates are world (x, z).
    /// </summary>
    public class SpottedRecord
    {
        public const int Rays = 24, Max = 12;
        public float Time;
        public string Who, Caption;
        public Vector2 Her, Guard, Facing;
        public float Half, Near, Far, Touch;
        /// <summary>His far reach along each of <see cref="Rays"/>+1 rays across the cone, cut where a wall stops the eye.</summary>
        public float[] ConeR;
        public bool HasLamp;
        public Vector2 Lamp;
        public float LampR;
        public DetectionMath.Band Band;
        public bool Smell, Searchlight;

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>The ray directions (flat, unit) the cone's reaches are measured along.</summary>
        public Vector2 RayDir(int i)
        {
            float a0 = Mathf.Atan2(Facing.y, Facing.x);
            float a = a0 + (-Half + 2f * Half * i / Rays) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// <summary>Takes the picture at the moment he spots her.</summary>
        public static SpottedRecord Take(Npc n, Player.Vampire p, float time)
        {
            var v = n.Vision;
            var c = n.LastCause;
            var r = new SpottedRecord
            {
                Time = time, Who = string.IsNullOrEmpty(n.DisplayName) ? "A guard" : n.DisplayName, Caption = n.ExplainSpotted(),
                Her = Flat(p.Feet), Guard = Flat(n.transform.position), Facing = Flat(n.Forward).normalized,
                Half = v.HalfAngle, Near = v.NearRange, Far = v.FarRange, Touch = v.Peripheral,
                Band = c.Band, Smell = c.Smell, Searchlight = c.Searchlight,
                ConeR = new float[Rays + 1],
            };
            if (r.Facing.sqrMagnitude < 0.5f) r.Facing = Vector2.up;
            var eye = n.Eye;
            for (int i = 0; i <= Rays; i++)
            {
                var d = r.RayDir(i);
                var end = n.transform.position + new Vector3(d.x, 0f, d.y) * r.Far + Vector3.up;
                r.ConeR[i] = Physics.Linecast(eye, end, out var hit, Layers.LightBlockMask, QueryTriggerInteraction.Ignore)
                    ? Mathf.Max(0.3f, Vector2.Distance(Flat(hit.point), r.Guard)) : r.Far;
            }
            if (c.Band == DetectionMath.Band.Far && !c.Smell && !c.Searchlight && Game.Lights != null)
            {
                var l = Game.Lights.Brightest(p.Feet);
                if (l != null && l.Kind != LightKind.Moon)
                {
                    r.HasLamp = true; r.Lamp = Flat(l.transform.position); r.LampR = Game.Lights.ExposureRadius(l);
                }
            }
            return r;
        }

        /// <summary>The world area the sketch must show: her, him, his near sector and touch circle, his cone as cut, and
        /// the lamp's rim.</summary>
        public Rect Bounds()
        {
            var min = Vector2.Min(Her, Guard); var max = Vector2.Max(Her, Guard);
            void Add(Vector2 q) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
            Add(Guard - Vector2.one * Touch); Add(Guard + Vector2.one * Touch);
            for (int i = 0; i <= Rays; i++) Add(Guard + RayDir(i) * (ConeR != null ? ConeR[i] : Far));
            if (HasLamp) { Add(Lamp - Vector2.one * LampR); Add(Lamp + Vector2.one * LampR); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>Pixels per metre to fit <paramref name="world"/> in a w×h box with a margin, never closer than
        /// <paramref name="maxScale"/>. Pure.</summary>
        public static float Fit(Rect world, float w, float h, float margin, float maxScale)
        {
            float sx = (w - 2f * margin) / Mathf.Max(0.5f, world.width), sy = (h - 2f * margin) / Mathf.Max(0.5f, world.height);
            return Mathf.Min(maxScale, Mathf.Min(sx, sy));
        }

        /// <summary>World (x, z) to sketch pixels (y down, north up), centred on <paramref name="centre"/>. Pure.</summary>
        public static Vector2 Map(Vector2 world, Vector2 centre, float scale, float w, float h) =>
            new Vector2(w * 0.5f + (world.x - centre.x) * scale, h * 0.5f - (world.y - centre.y) * scale);

        public static void Keep(List<SpottedRecord> list, SpottedRecord r)
        {
            if (list.Count >= Max) return;   // the first few explain the night; later ones repeat the lesson
            list.Add(r);
        }
    }
}
