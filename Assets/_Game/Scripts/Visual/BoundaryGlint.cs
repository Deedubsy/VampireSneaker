using System.Collections.Generic;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Stealth;

namespace Vespertine.Visual
{
    /// <summary>
    /// When a meter on Ilse starts, the edge she is past brightens for 0.3 s (SR.9): his near arc, his touch circle, or
    /// the exposure rim of the light that lit her (<see cref="MeterRead.GlintOf"/>). Shapes are taken when the meter
    /// starts and then held, so the glint marks where the line was, not where he has turned since.
    /// <para>The Spotted picture (SR.10) uses the same shapes: when a guard spots her, the band she crossed is held for
    /// <see cref="MeterRead.PictureTime"/> (with a ring round the lamp that lit her), a ring marks the spotter and a
    /// sight-line runs from his eye to her; <see cref="ConeRenderer.Spotter"/> draws his cone full and on top.</para>
    /// </summary>
    public class BoundaryGlint : MonoBehaviour
    {
        const float W = 0.14f, Lift = 0.08f;

        struct Shot
        {
            public Vector3 Centre, Forward;
            public float Radius, Half, Born, Life;
            public bool Held;   // the Spotted picture: steady, fading only at the end
            public Color Col;
            public float[] R;   // per-vertex radius, cut where a wall stops the eye or the light
        }

        readonly List<Shot> _shots = new List<Shot>();
        readonly List<Vector3> _v = new List<Vector3>(512);
        readonly List<Color> _c = new List<Color>(512);
        readonly List<int> _t = new List<int>(1024);
        Mesh _mesh;
        MeshRenderer _mr;

        /// <summary>How many glints are showing now (gym checks).</summary>
        public int Showing => _shots.Count;
        public static BoundaryGlint Instance;
        /// <summary>Dev: hold each glint this many extra seconds (screenshots in the gym). 0 in play.</summary>
        public static float Hold;

        void Awake()
        {
            Instance = this;
            gameObject.layer = Layers.Overlay;
            _mesh = new Mesh { name = "boundary_glint" };
            _mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _mr = gameObject.AddComponent<MeshRenderer>();
            _mr.sharedMaterial = Mats.Overlay("boundary_glint", Color.white, xray: true);
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.sortingOrder = 4;
            _mr.enabled = false;
            Npc.MeterStarted += OnStarted;
            GameEvents.SpottedCaption += OnSpotted;
        }

        void OnDestroy()
        {
            Npc.MeterStarted -= OnStarted;
            GameEvents.SpottedCaption -= OnSpotted;
            if (Instance == this) Instance = null;
        }

        void OnStarted(Npc n)
        {
            if (!Game.InMission || n == null) return;
            Shape(n, MeterRead.GlintTime, false);
        }

        Npc _spotter;
        float _spotUntil;

        void OnSpotted(Npc n)
        {
            if (!Game.InMission || n == null) return;
            // the first sighting explains the mistake, as the caption does: others joining in while it shows add nothing
            if (_spotter && Time.unscaledTime < _spotUntil) return;
            _spotter = n;
            _spotUntil = Time.unscaledTime + MeterRead.PictureTime;
            if (ConeRenderer.Instance != null) { ConeRenderer.Instance.Spotter = n; ConeRenderer.Instance.SpotUntil = _spotUntil; }
            Shape(n, MeterRead.PictureTime, true);
        }

        /// <summary>Adds the edge for the band that fed his meter; for the Spotted picture also a ring round the lamp.</summary>
        void Shape(Npc n, float life, bool held)
        {
            var c = n.LastCause;
            var v = n.Vision;
            var s = new Shot { Born = Time.unscaledTime, Life = life, Held = held, Half = 180f, Forward = n.Forward };
            switch (MeterRead.GlintOf(c))
            {
                case MeterRead.Glint.NearArc:
                    s.Centre = n.transform.position; s.Radius = v.NearRange; s.Half = v.HalfAngle;
                    s.Col = Bright(ConeRenderer.StateColor(n));
                    break;
                case MeterRead.Glint.TouchCircle:
                    s.Centre = n.transform.position; s.Radius = v.Peripheral;
                    s.Col = Bright(ConeRenderer.StateColor(n));
                    break;
                case MeterRead.Glint.LightRim:
                    var p = Game.Player;
                    var l = Game.Lights != null && p != null ? Game.Lights.Brightest(p.Feet) : null;
                    if (l == null) return;
                    s.Centre = l.transform.position; s.Radius = Game.Lights.ExposureRadius(l);
                    if (s.Radius < 0.3f) return;
                    s.Col = Bright(l.Color);
                    Clip(ref s, l.Kind == LightKind.Moon ? (Vector3?)null : l.SourcePos);
                    _shots.Add(s);
                    if (held && l.Kind != LightKind.Moon)
                    {
                        // "the lamp that lit her": a small ring at its foot
                        var r = s; r.Radius = 0.6f; r.R = null; r.Centre = l.transform.position;
                        Clip(ref r, null);
                        _shots.Add(r);
                    }
                    return;
                default: return;
            }
            Clip(ref s, n.Eye);
            _shots.Add(s);
        }

        static int Segs(float half) => Mathf.Clamp(Mathf.CeilToInt(half * 2f / 6f), 6, 60);

        /// <summary>Like the exposure rims: an edge stops at the wall between it and the eye or lamp it belongs to.</summary>
        static void Clip(ref Shot s, Vector3? from)
        {
            int segs = Segs(s.Half);
            s.R = new float[segs + 1];
            float a0 = Mathf.Atan2(s.Forward.z, s.Forward.x) * Mathf.Rad2Deg;
            for (int i = 0; i <= segs; i++)
            {
                float a = (a0 - s.Half + 2f * s.Half * i / segs) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = s.Radius;
                if (from.HasValue && Physics.Linecast(from.Value, s.Centre + d * r + Vector3.up, out var hit, Layers.LightBlockMask, QueryTriggerInteraction.Ignore))
                    r = Mathf.Max(0.2f, Util.FlatDistance(hit.point, s.Centre) - 0.05f);
                s.R[i] = r;
            }
        }

        static Color Bright(Color c) { c = Color.Lerp(c, Color.white, 0.55f); c.a = 1f; return c; }

        void LateUpdate()
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
                if (Time.unscaledTime - _shots[i].Born > _shots[i].Life + Hold || !Game.InMission) _shots.RemoveAt(i);
            bool spotting = _spotter && _spotter.IsAlive && Game.InMission && Game.Player != null && Time.unscaledTime < _spotUntil + Hold;
            if (!spotting) _spotter = null;
            if (_shots.Count == 0 && !spotting) { if (_mr.enabled) _mr.enabled = false; return; }
            _v.Clear(); _c.Clear(); _t.Clear();
            float now = Time.unscaledTime;
            foreach (var s in _shots)
            {
                float age = Mathf.Max(0f, now - s.Born - Hold);
                if (s.Held) Arc(s, Mathf.Clamp01((s.Life - age) / 0.6f) * (0.85f + 0.15f * Mathf.Sin(now * 7f)), 1.25f);
                else Arc(s, 1f - age / s.Life, 1f + age / s.Life * 0.6f);
            }
            if (spotting) Picture(Mathf.Clamp01((_spotUntil + Hold - now) / 0.6f));
            _mesh.Clear();
            _mesh.SetVertices(_v);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
            _mr.enabled = true;
        }

        void Arc(in Shot s, float alpha, float widen)
        {
            var col = s.Col; col.a = 0.9f * Mathf.Clamp01(alpha);
            float w = W * widen;
            int segs = Segs(s.Half);
            float a0 = Mathf.Atan2(s.Forward.z, s.Forward.x) * Mathf.Rad2Deg;
            var y = Vector3.up * Lift;
            int o = _v.Count;
            for (int i = 0; i <= segs; i++)
            {
                float a = (a0 - s.Half + 2f * s.Half * i / segs) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = s.R[i];
                _v.Add(s.Centre + y + d * (r + w * 0.5f)); _c.Add(col);
                _v.Add(s.Centre + y + d * Mathf.Max(0f, r - w * 0.5f)); _c.Add(col);
            }
            for (int i = 0; i < segs; i++)
            {
                int k = o + i * 2;
                _t.Add(k); _t.Add(k + 2); _t.Add(k + 1);
                _t.Add(k + 1); _t.Add(k + 2); _t.Add(k + 3);
            }
        }
    

        /// <summary>The spotter's ring and the sight-line from his eye to her chest, following both.</summary>
        void Picture(float alpha)
        {
            var n = _spotter;
            var col = Bright(ConeRenderer.StateColor(n)); col.a = 0.95f * alpha;
            var ring = new Shot { Centre = n.transform.position, Forward = Vector3.forward, Radius = 0.75f, Half = 180f, Col = col };
            Clip(ref ring, null);
            Arc(ring, alpha * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 9f)), 0.8f);
            var cam = Game.Cam != null ? Game.Cam.Cam : null;
            if (cam == null) return;
            Vector3 a = n.Eye, b = Game.Player.transform.position + Vector3.up * 1.1f;
            var side = Vector3.Cross(b - a, cam.transform.forward);
            if (side.sqrMagnitude < 1e-6f) return;
            side = side.normalized * 0.035f;
            int o = _v.Count;
            var lc = col; lc.a = 0.85f * alpha;
            _v.Add(a - side); _v.Add(a + side); _v.Add(b - side); _v.Add(b + side);
            for (int i = 0; i < 4; i++) _c.Add(lc);
            _t.Add(o); _t.Add(o + 2); _t.Add(o + 1);
            _t.Add(o + 1); _t.Add(o + 2); _t.Add(o + 3);
        }
    }
}
