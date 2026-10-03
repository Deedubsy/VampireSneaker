using System.Collections.Generic;
using UnityEngine;
using Vespertine.Core;
using Vespertine.Visual;

namespace Vespertine.Stealth
{
    /// <summary>A blood stain on the ground. Evidence that makes guards suspicious.</summary>
    public class BloodStain : MonoBehaviour
    {
        public bool Found;
        public bool Preexisting;   // part of the level (old stains); ignored by guards
        public float Size = 1f;
        public bool Huge;          // hemorrhage pool: alerts like a corpse
        public int Seq = -1;       // blood-trail drop order (-1 = not part of a trail)
        public float Born;         // Time.time when spilled
        public bool IsTrail => Seq >= 0;

        void OnEnable() => Evidence.Stains.Add(this);
        void OnDisable() => Evidence.Stains.Remove(this);
    }

    /// <summary>Registry for evidence that is not an NPC (stains). Corpses and dazed bodies are NPC states.</summary>
    public static class Evidence
    {
        public static readonly List<BloodStain> Stains = new List<BloodStain>();

        public const int MaxTrailDrops = 90;
        public static int TrailSeq;

        /// <summary>One drop of a wounded vampire's blood trail. Hounds follow these forward, drop by drop.</summary>
        public static BloodStain DropTrail(Vector3 pos, Transform parent, int seq = -1)
        {
            var s = SpawnStain(pos, parent, false, 0.32f);
            s.Seq = seq >= 0 ? seq : ++TrailSeq;
            if (s.Seq > TrailSeq) TrailSeq = s.Seq;
            // old drops dry out of the record
            BloodStain oldest = null; int n = 0;
            foreach (var x in Stains) if (x && x.IsTrail) { n++; if (oldest == null || x.Seq < oldest.Seq) oldest = x; }
            if (n > MaxTrailDrops && oldest != null) Object.Destroy(oldest.gameObject);
            return s;
        }

        /// <summary>From drop <paramref name="from"/>, step to the next drop in spill order (the lowest later Seq within
        /// <paramref name="maxGap"/> metres) up to <paramref name="maxSteps"/> times. Returns the index reached.
        /// Pure, so the hound's tracking is unit-tested.</summary>
        public static int FollowTrail(IList<Vector3> pos, IList<int> seq, int from, float maxGap, int maxSteps)
        {
            int cur = from;
            for (int step = 0; step < maxSteps; step++)
            {
                int best = -1;
                for (int i = 0; i < pos.Count; i++)
                {
                    if (seq[i] <= seq[cur]) continue;
                    var dx = pos[i] - pos[cur]; dx.y = 0f;
                    if (dx.magnitude > maxGap) continue;
                    if (best < 0 || seq[i] < seq[best]) best = i;
                }
                if (best < 0) break;
                cur = best;
            }
            return cur;
        }

        public static BloodStain SpawnStain(Vector3 pos, Transform parent, bool preexisting, float size = 1f, bool huge = false)
        {
            var go = new GameObject(preexisting ? "stain_old" : "stain");
            go.transform.SetParent(parent, true);
            // stand on whatever is below
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, Layers.GroundMask | Layers.WallMask, QueryTriggerInteraction.Ignore))
                pos = hit.point;
            go.transform.position = pos + Vector3.up * 0.03f;
            go.transform.rotation = Quaternion.Euler(90, Random.Range(0, 360f), 0);
            float s = size * (huge ? 3.2f : 1.4f) * Random.Range(0.85f, 1.15f);
            go.transform.localScale = new Vector3(s, s, 1);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = QuadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = preexisting
                ? Mats.Overlay("stain_old", new Color(0.18f, 0.02f, 0.03f, 0.75f), "splat")
                : Mats.Overlay("stain_fresh", new Color(0.42f, 0.02f, 0.05f, 0.92f), "splat");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.layer = Layers.Overlay;
            var st = go.AddComponent<BloodStain>();
            st.Preexisting = preexisting;
            st.Found = preexisting;
            st.Size = s;
            st.Huge = huge;
            st.Born = Time.time;
            return st;
        }

        static Mesh _quad;
        public static Mesh QuadMesh
        {
            get
            {
                if (_quad) return _quad;
                _quad = new Mesh { name = "quad" };
                _quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
                _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                _quad.RecalculateNormals();
                _quad.RecalculateBounds();
                return _quad;
            }
        }

        public static void Clear() { Stains.Clear(); TrailSeq = 0; }
    }
}
