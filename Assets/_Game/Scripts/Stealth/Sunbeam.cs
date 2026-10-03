using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vespertine.Core;
using Vespertine.Visual;

namespace Vespertine.Stealth
{
    /// <summary>Where a sunbeam's pool is at a given moment. Pure; unit-tested.</summary>
    public static class SunbeamMath
    {
        public const float FadeIn = 20f;   // the first light takes this long to whiten

        /// <summary>How far along its path the pool is, 0..1 (0 before <paramref name="start"/>).</summary>
        public static float Travel(float t, float start, float over)
        {
            if (t <= start) return 0f;
            if (over <= 0f) return 1f;
            return Mathf.Clamp01((t - start) / over);
        }

        /// <summary>The beam is up from <paramref name="start"/> on. Before that it's still night.</summary>
        public static bool Up(float t, float start) => t >= start;

        /// <summary>Brightness 0..1: the light thickens from nothing over <see cref="FadeIn"/> seconds.</summary>
        public static float Strength(float t, float start) => t < start ? 0f : Mathf.Clamp01((t - start) / FadeIn);

        /// <summary>The pool's position along a path of cells (piecewise linear) at travel <paramref name="f"/>.</summary>
        public static Vector2 Along(IList<Vector2> path, float f)
        {
            if (path == null || path.Count == 0) return Vector2.zero;
            if (path.Count == 1 || f <= 0f) return path[0];
            if (f >= 1f) return path[path.Count - 1];
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector2.Distance(path[i - 1], path[i]);
            float want = f * total;
            for (int i = 1; i < path.Count; i++)
            {
                float d = Vector2.Distance(path[i - 1], path[i]);
                if (want <= d && d > 0f) return Vector2.Lerp(path[i - 1], path[i], want / d);
                want -= d;
            }
            return path[path.Count - 1];
        }
    }

    /// <summary>
    /// Dawn coming in through a high window (M14): a slanting shaft of real sunlight whose pool creeps across the floor.
    /// <c>light id x y sunbeam from=x,y,h path=x1,y1,x2,y2,... start=s over=s [radius=r]</c>. Nothing before
    /// <c>start</c> (mission seconds); then the pool fades in at (x,y) and slides along <c>path</c> over <c>over</c>
    /// seconds. It burns like a sunstone, can't be snuffed, smashed or cut, and its place follows from the mission
    /// clock alone, so it needs no save state.
    /// </summary>
    public class Sunbeam : MonoBehaviour
    {
        public GameLight Light;
        public Vector3 Window;
        public float Start, Over = 300f;
        readonly List<Vector2> _cells = new List<Vector2>();
        Level.LevelData _data;
        System.Func<float, float, float> _surface;
        Transform _shaft, _ring, _wash;
        Light _unity;
        Material _shaftMat;

        public void Setup(Level.EntitySpec spec, Level.LevelData data, System.Func<float, float, float> surface)
        {
            Light = GetComponent<GameLight>();
            _data = data; _surface = surface;
            Start = spec.OptFloat("start", 0f);
            Over = spec.OptFloat("over", Over);
            var from = Floats(spec.Opt("from"));
            float fx = from.Count > 0 ? from[0] : spec.X, fy = from.Count > 1 ? from[1] : spec.Y;
            float fh = from.Count > 2 ? from[2] : 9f;
            Window = data.CellToWorld(fx, fy, surface(fx, fy) + fh);
            _cells.Add(new Vector2(spec.X, spec.Y));
            var p = Floats(spec.Opt("path"));
            for (int i = 0; i + 1 < p.Count; i += 2) _cells.Add(new Vector2(p[i], p[i + 1]));
            BuildVisual();
            Tick();
        }

        static List<float> Floats(string s)
        {
            var l = new List<float>();
            if (string.IsNullOrEmpty(s)) return l;
            foreach (var t in s.Split(','))
                if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) l.Add(v);
            return l;
        }

        void BuildVisual()
        {
            var col = Light.Color;
            var lgo = new GameObject("sun");
            lgo.transform.SetParent(Game.Level != null ? Game.Level.EntityRoot : null, false);
            lgo.transform.position = Window;
            _unity = lgo.AddComponent<Light>();
            _unity.type = LightType.Spot;
            _unity.color = col;
            _unity.shadows = LightShadows.None;

            // the shaft: a long, soft-edged slab of dust-lit air from the window to the pool
            _shaft = new GameObject("shaft").transform;
            _shaft.SetParent(lgo.transform, false);
            _shaft.gameObject.layer = Layers.Overlay;
            var mesh = new Mesh { name = "sunshaft" };
            const int N = 12;
            var v = new List<Vector3>(); var t = new List<int>(); var vc = new List<Color>();
            for (int i = 0; i <= N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                v.Add(new Vector3(Mathf.Cos(a) * 0.45f, Mathf.Sin(a) * 0.45f, 0f)); vc.Add(new Color(1f, 1f, 1f, 0.5f));
                v.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1f)); vc.Add(new Color(1f, 1f, 1f, 1f));
                if (i == 0) continue;
                int o = v.Count - 4;
                t.AddRange(new[] { o, o + 1, o + 2, o + 2, o + 1, o + 3 });
            }
            mesh.SetVertices(v); mesh.SetColors(vc); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            _shaft.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = _shaft.gameObject.AddComponent<MeshRenderer>();
            _shaftMat = Mats.Overlay("sunbeam_shaft", new Color(1f, 0.9f, 0.62f, 0.07f), null, false, true);
            mr.sharedMaterial = _shaftMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // the burning edge on the floor (where LightSystem.BurnAt reaches a standing body), and a warm wash inside it
            float r = BurnRadius;
            _ring = Disc("sunring", r, 0.14f, new Color(1f, 0.82f, 0.4f, 0.6f));
            _wash = Disc("sunwash", r, r, new Color(1f, 0.86f, 0.5f, 0.12f));
        }

        Transform Disc(string n, float r, float width, Color c)
        {
            var go = new GameObject(n);
            go.layer = Layers.Overlay;
            go.transform.SetParent(transform, false);
            var m = new Mesh { name = n };
            var rv = new List<Vector3>(); var rt = new List<int>(); var rc = new List<Color>();
            for (int i = 0; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                rv.Add(d * r + Vector3.up * 0.07f); rc.Add(Color.white);
                rv.Add(d * Mathf.Max(0f, r - width) + Vector3.up * 0.07f); rc.Add(Color.white);
                if (i == 0) continue;
                int o = rv.Count - 4;
                rt.AddRange(new[] { o, o + 2, o + 1, o + 1, o + 2, o + 3 });
            }
            m.SetVertices(rv); m.SetColors(rc); m.SetTriangles(rt, 0); m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Overlay(n, c, null, false, true);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        float BurnRadius { get { float reach = Light.Radius * 0.85f, dy = Light.Height - 1f; return Mathf.Sqrt(Mathf.Max(0.1f, reach * reach - dy * dy)); } }

        float Now => Game.Mission != null ? Game.Mission.MissionTime : 0f;

        void Update() => Tick();

        void Tick()
        {
            float now = Now;
            bool up = SunbeamMath.Up(now, Start);
            var c = SunbeamMath.Along(_cells, SunbeamMath.Travel(now, Start, Over));
            transform.position = _data.CellToWorld(c.x, c.y, _surface(c.x, c.y));
            if (Light.On != up) Light.SetOn(up);
            float s = SunbeamMath.Strength(now, Start);
            // the burn only counts once the light has thickened enough to see by
            Light.Intensity = Mathf.Lerp(0.3f, 1.6f, s);
            if (!_unity) return;
            _unity.enabled = up;
            _shaft.gameObject.SetActive(up);
            _ring.gameObject.SetActive(up);
            _wash.gameObject.SetActive(up);
            if (!up) return;
            var to = transform.position - Window;
            float dist = to.magnitude, r = BurnRadius * 1.15f;
            _unity.transform.rotation = Quaternion.LookRotation(to);
            _unity.range = dist + 6f;
            _unity.intensity = 1.6f * s * dist * dist / 10f + 0.5f * s;
            _unity.spotAngle = Mathf.Clamp(Mathf.Atan2(r, dist) * Mathf.Rad2Deg * 2f, 4f, 80f);
            _unity.innerSpotAngle = _unity.spotAngle * 0.6f;
            _shaft.localScale = new Vector3(r, r, dist);
        }
    }
}
