using System.Collections.Generic;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Stealth;

namespace Vespertine.Visual
{
    /// <summary>
    /// Where a suspicious guard is going (SR.5, SR.11): a dotted line on the ground along his walk, ending in a small
    /// ring. Amber for Investigating, red at 60% for Searching (his next search point). Dots march toward the end, so
    /// the line says which way he is coming. Shown for guards within <see cref="ConeContext.AwareRange"/> of Ilse, pinned
    /// guards at any range, and every one in range while Alt is held. Only while he walks; a guard looking round draws
    /// nothing.
    /// </summary>
    public class IntentPaths : MonoBehaviour
    {
        public const float Spacing = 0.45f, Skip = 0.8f, MaxLength = 30f, March = 0.6f;
        const float Dot = 0.07f, Lift = 0.06f, PinR = 0.35f, PinW = 0.06f;
        const int PinSegs = 16, CornerMax = 24;

        readonly List<Vector3> _v = new List<Vector3>(1024);
        readonly List<Color> _c = new List<Color>(1024);
        readonly List<int> _t = new List<int>(2048);
        readonly List<Vector3> _dots = new List<Vector3>(128);
        readonly Vector3[] _corners = new Vector3[CornerMax];
        Mesh _mesh;
        MeshRenderer _mr;

        /// <summary>How many guards' paths are drawn now (gym checks).</summary>
        public int Drawn { get; private set; }

        void Awake()
        {
            gameObject.layer = Layers.Overlay;
            _mesh = new Mesh { name = "intent_paths" };
            _mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _mr = gameObject.AddComponent<MeshRenderer>();
            _mr.sharedMaterial = Mats.Overlay("intent_paths", Color.white);
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.sortingOrder = 2;
            _mr.enabled = false;
        }

        /// <summary>Dots along the polyline <paramref name="pts"/> (the first <paramref name="count"/>), every
        /// <paramref name="spacing"/> m, starting <paramref name="start"/> m from its first point and stopping at the end or
        /// at <paramref name="maxLen"/> m along it. Returns whether the line reached its end within the cap.</summary>
        public static bool Dots(IReadOnlyList<Vector3> pts, int count, float spacing, float start, float maxLen, List<Vector3> into)
        {
            into.Clear();
            if (count < 2 || spacing <= 0f) return count == 1;
            float along = 0f, next = start;
            for (int i = 1; i < count; i++)
            {
                var a = pts[i - 1];
                var seg = pts[i] - a;
                float len = seg.magnitude;
                if (len < 1e-5f) continue;
                while (next <= along + len)
                {
                    if (next > maxLen) return false;
                    into.Add(a + seg * ((next - along) / len));
                    next += spacing;
                }
                along += len;
                if (along > maxLen) return false;
            }
            return true;
        }

        void LateUpdate()
        {
            _v.Clear(); _c.Clear(); _t.Clear();
            Drawn = 0;
            var p = Game.Player;
            if (Game.InMission && Game.AI != null && p != null)
            {
                var cones = ConeRenderer.Instance;
                bool all = cones != null && cones.AllShown;
                float phase = Mathf.Repeat(Time.time * March, Spacing);
                foreach (var n in Game.AI.Npcs)
                {
                    if (!n || !n.IsAlive || n.Incapacitated || n.IsThrall || !n.gameObject.activeInHierarchy) continue;
                    bool lost = n.State == NpcState.Searching;
                    if (n.State != NpcState.Investigating && !lost) continue;
                    var ag = n.Agent;
                    if (!ag || !ag.enabled || !ag.isOnNavMesh || !ag.hasPath || ag.isStopped || ag.pathPending) continue;
                    bool pinned = cones != null && cones.IsInspected(n);
                    if (!pinned && !all && Util.FlatDistance(n.transform.position, p.Feet) > ConeContext.AwareRange) continue;
                    int count = ag.path.GetCornersNonAlloc(_corners);
                    if (count < 2) continue;
                    _corners[0] = n.transform.position;
                    var col = ConeRenderer.StateColor(n);
                    col.a = lost ? 0.6f : 0.9f;
                    bool ends = Dots(_corners, count, Spacing, Skip + phase, MaxLength, _dots);
                    foreach (var d in _dots) Diamond(d + Vector3.up * Lift, col);
                    if (ends) Ring(_corners[count - 1] + Vector3.up * Lift, col);
                    Drawn++;
                }
            }
            _mesh.Clear();
            if (_v.Count == 0) { _mr.enabled = false; return; }
            _mesh.SetVertices(_v);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
            _mr.enabled = true;
        }

        void Diamond(Vector3 at, Color col)
        {
            int b = _v.Count;
            _v.Add(at + new Vector3(Dot, 0, 0)); _v.Add(at + new Vector3(0, 0, -Dot));
            _v.Add(at + new Vector3(-Dot, 0, 0)); _v.Add(at + new Vector3(0, 0, Dot));
            for (int i = 0; i < 4; i++) _c.Add(col);
            _t.Add(b); _t.Add(b + 1); _t.Add(b + 2);
            _t.Add(b); _t.Add(b + 2); _t.Add(b + 3);
        }

        void Ring(Vector3 at, Color col)
        {
            int b = _v.Count;
            for (int i = 0; i <= PinSegs; i++)
            {
                float a = i * Mathf.PI * 2f / PinSegs;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                _v.Add(at + d * (PinR - PinW * 0.5f)); _v.Add(at + d * (PinR + PinW * 0.5f));
                _c.Add(col); _c.Add(col);
                if (i == PinSegs) break;
                int j = b + i * 2;
                _t.Add(j); _t.Add(j + 3); _t.Add(j + 1);
                _t.Add(j); _t.Add(j + 2); _t.Add(j + 3);
            }
        }
    }
}
