using System.Collections.Generic;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Stealth;

namespace Vespertine.Visual
{
    /// <summary>
    /// Who watches this spot (SR.5, cone ownership): with the cursor on bare ground, every guard whose cone covers that
    /// point gets a ring at his feet in his cone's colour. Covers means what the cone draws there: in his range and view,
    /// with line of sight, at full light (the dark far band is still his cone). Rechecked when the cursor moves 0.25 m or
    /// every 0.2 s. Not while the cursor is on a guard, light or usable, nor while a menu holds the game.
    /// </summary>
    public class HoverWatchers : MonoBehaviour
    {
        public const float Moved = 0.25f, Every = 0.2f;
        const float R = 0.6f, W = 0.08f, Lift = 0.06f;
        const int Segs = 24;

        readonly List<Npc> _watchers = new List<Npc>(8);
        readonly List<Vector3> _v = new List<Vector3>(512);
        readonly List<Color> _c = new List<Color>(512);
        readonly List<int> _t = new List<int>(1024);
        Vector3 _at;
        float _checkedAt = -1f;
        Mesh _mesh;
        MeshRenderer _mr;

        /// <summary>The guards ringed now (gym checks).</summary>
        public IReadOnlyList<Npc> Watchers => _watchers;

        void Awake()
        {
            gameObject.layer = Layers.Overlay;
            _mesh = new Mesh { name = "hover_watchers" };
            _mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _mr = gameObject.AddComponent<MeshRenderer>();
            _mr.sharedMaterial = Mats.Overlay("hover_watchers", Color.white);
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.sortingOrder = 3;
            _mr.enabled = false;
        }

        /// <summary>Whether <paramref name="n"/>'s drawn cone covers the standing point <paramref name="p"/>.</summary>
        public static bool Covers(Npc n, Vector3 p) => ConeRenderer.Seeing(n) && n.SeenBand(p, 1f) != DetectionMath.Band.None;

        void LateUpdate()
        {
            var p = Game.Player;
            bool on = Game.InMission && Game.AI != null && p != null && !p.Dead && p.HoverValid
                      && !p.HoverNpc && p.HoverLight == null && p.HoverUse == null
                      && (Game.UI == null || !Game.UI.BlocksGameplay);
            if (!on) { _watchers.Clear(); _checkedAt = -1f; }
            else if (_checkedAt < 0f || Time.unscaledTime - _checkedAt > Every || Util.FlatDistance(p.HoverPoint, _at) > Moved)
            {
                _at = p.HoverPoint; _checkedAt = Time.unscaledTime;
                _watchers.Clear();
                foreach (var n in Game.AI.Npcs)
                    if (n && Util.FlatDistance(n.transform.position, _at) <= n.Vision.FarRange + 0.5f && Covers(n, _at)) _watchers.Add(n);
            }

            _v.Clear(); _c.Clear(); _t.Clear();
            _watchers.RemoveAll(n => !ConeRenderer.Seeing(n));
            foreach (var n in _watchers)
            {
                var col = ConeRenderer.StateColor(n);
                col.a = 0.9f;
                Ring(n.transform.position + Vector3.up * Lift, col);
            }
            _mesh.Clear();
            if (_v.Count == 0) { _mr.enabled = false; return; }
            _mesh.SetVertices(_v);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
            _mr.enabled = true;
        }

        void Ring(Vector3 at, Color col)
        {
            int b = _v.Count;
            for (int i = 0; i <= Segs; i++)
            {
                float a = i * Mathf.PI * 2f / Segs;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                _v.Add(at + d * (R - W * 0.5f)); _v.Add(at + d * (R + W * 0.5f));
                _c.Add(col); _c.Add(col);
                if (i == Segs) break;
                int j = b + i * 2;
                _t.Add(j); _t.Add(j + 3); _t.Add(j + 1);
                _t.Add(j); _t.Add(j + 2); _t.Add(j + 3);
            }
        }
    }
}
