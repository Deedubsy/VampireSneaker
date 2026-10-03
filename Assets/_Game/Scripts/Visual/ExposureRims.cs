using System.Collections.Generic;
using UnityEngine;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Stealth;

namespace Vespertine.Visual
{
    /// <summary>
    /// The exposure rim (QW16, SR.4): a thin line on the ground in the light's own hue where its light stops exposing
    /// her (<see cref="LightSystem.ExposureRadius"/>, 0.35), pulled in where walls block the light, as the burn ring
    /// is. Only lights near Ilse draw one: it fades in from 10 m from the line to full at 2 m (12 m for carried
    /// lanterns, whose edge moves). Rebuilt when the night's light changes (a blackout) and, for lanterns, 5 times a
    /// second.
    /// </summary>
    public class ExposureRims : MonoBehaviour
    {
        const float ShowWithin = 10f, MovingWithin = 12f, FullWithin = 2f, MaxAlpha = 0.5f, Width = 0.07f, Lift = 0.065f;
        const int N = 56;

        class Rim
        {
            public GameLight L;
            public GameObject Go;
            public Mesh Mesh;
            public readonly Vector3[] V = new Vector3[(N + 1) * 2];
            public readonly Color[] C = new Color[(N + 1) * 2];
            public float Radius = -1f, BuiltAt, Alpha = -1f;
            public Color Hue;
        }

        readonly Dictionary<GameLight, Rim> _rims = new Dictionary<GameLight, Rim>();
        readonly List<GameLight> _dead = new List<GameLight>();
        Material _mat;
        int[] _tris;

        void LateUpdate()
        {
            var lights = Game.Lights;
            var p = Game.Player;
            if (!Game.InMission || lights == null || p == null || Game.Settings != null && !Game.Settings.ExposureRims)
            {
                if (_rims.Count > 0) Clear();
                return;
            }
            var feet = p.Feet;
            foreach (var l in lights.All)
            {
                if (!l) continue;
                _rims.TryGetValue(l, out var rim);
                float a = 0f, r = 0f;
                if (l.On && l.isActiveAndEnabled && l.Kind != LightKind.Searchlight && l.Kind != LightKind.Sunbeam
                    && Mathf.Abs(feet.y - l.transform.position.y) < 3f)
                {
                    float within = Mathf.Min(l.Portable ? MovingWithin : ShowWithin, Mathf.Max(FullWithin + 1f, Difficulties.Current.RimWithin));
                    float dl = Util.FlatDistance(l.transform.position, feet);
                    if (dl < l.Radius + within)
                    {
                        r = lights.ExposureRadius(l);
                        if (r > 0.1f) a = Mathf.Clamp01((within - Mathf.Abs(dl - r)) / (within - FullWithin));
                    }
                }
                if (a <= 0.01f) { if (rim != null && rim.Go.activeSelf) rim.Go.SetActive(false); continue; }
                if (rim == null) _rims[l] = rim = NewRim(l);
                if (!rim.Go.activeSelf) rim.Go.SetActive(true);
                rim.Go.transform.position = l.transform.position;
                if (Mathf.Abs(rim.Radius - r) > 0.05f || l.Portable && Time.time - rim.BuiltAt > 0.2f) Build(rim, r);
                if (Mathf.Abs(rim.Alpha - a) > 0.02f) Tint(rim, a);
            }
            // lights that were destroyed (a lantern carrier's, at the end of a mission)
            _dead.Clear();
            foreach (var kv in _rims) if (!kv.Key) _dead.Add(kv.Key);
            foreach (var k in _dead) { if (_rims[k].Go) Destroy(_rims[k].Go); _rims.Remove(k); }
        }

        void Clear()
        {
            foreach (var r in _rims.Values) if (r.Go) Destroy(r.Go);
            _rims.Clear();
        }

        Rim NewRim(GameLight l)
        {
            var go = new GameObject("rim");
            go.layer = Layers.Overlay;
            go.transform.SetParent(transform, false);
            var rim = new Rim { L = l, Go = go, Mesh = new Mesh { name = "rim" } };
            rim.Mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = rim.Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            _mat ??= Mats.Overlay("exposure_rim", Color.white);
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (_tris == null)
            {
                _tris = new int[N * 6];
                for (int i = 0; i < N; i++)
                {
                    int o = i * 2, t = i * 6;
                    _tris[t] = o; _tris[t + 1] = o + 2; _tris[t + 2] = o + 1;
                    _tris[t + 3] = o + 1; _tris[t + 4] = o + 2; _tris[t + 5] = o + 3;
                }
            }
            // the light's own hue: lifted toward white so it reads on dark ground; burning lights in the burn ring's gold
            rim.Hue = l.Burns ? new Color(1f, 0.86f, 0.5f) : l.Kind == LightKind.Moon || l.Kind == LightKind.Window ? Color.Lerp(l.Color, new Color(0.7f, 0.8f, 1f), 0.5f) : Color.Lerp(l.Color, Color.white, 0.1f);
            return rim;
        }

        /// <summary>The ring at <paramref name="r"/>, each spoke cut where a wall blocks the light from her chest.</summary>
        void Build(Rim rim, float r)
        {
            var l = rim.L;
            rim.Radius = r;
            rim.BuiltAt = Time.time;
            var src = l.SourcePos;
            var basePos = l.transform.position;
            for (int i = 0; i <= N; i++)
            {
                float ang = i * Mathf.PI * 2f / N;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float ri = r;
                if (l.Kind != LightKind.Moon && Physics.Linecast(src, basePos + dir * r + Vector3.up, out var hit, Layers.LightBlockMask, QueryTriggerInteraction.Ignore))
                    ri = Mathf.Max(0.3f, Util.FlatDistance(hit.point, basePos) - 0.05f);
                rim.V[i * 2] = dir * ri + Vector3.up * Lift;
                rim.V[i * 2 + 1] = dir * Mathf.Max(0f, ri - Width) + Vector3.up * Lift;
            }
            rim.Mesh.vertices = rim.V;
            if (rim.Mesh.triangles.Length == 0)
            {
                rim.Mesh.colors = rim.C;
                rim.Mesh.triangles = _tris;
            }
            rim.Mesh.RecalculateBounds();
            rim.Alpha = -1f;
        }

        void Tint(Rim rim, float a)
        {
            rim.Alpha = a;
            var c = rim.Hue; c.a = a * MaxAlpha;
            for (int i = 0; i < rim.C.Length; i++) rim.C[i] = c;
            rim.Mesh.colors = rim.C;
        }

        void OnDisable() => Clear();
    }
}
