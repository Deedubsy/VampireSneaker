using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vespertine.Core;
using Vespertine.Stealth;

namespace Vespertine.Visual
{
    /// <summary>Short-lived visual effects built from primitives and overlay materials (no particle assets).</summary>
    public static class Fx
    {
        static Transform _root;
        static Transform Root
        {
            get
            {
                if (_root) return _root;
                var go = new GameObject("Fx");
                _root = go.transform;
                return _root;
            }
        }

        static Mesh _quad;
        static Mesh Quad => _quad ? _quad : _quad = Evidence.QuadMesh;

        public static void Clear()
        {
            if (_root) Object.Destroy(_root.gameObject);
            _root = null;
        }

        static GameObject Billboard(string name, Vector3 pos, float size, Material m, float life, bool faceUp = false)
        {
            var go = new GameObject(name);
            go.layer = Layers.Overlay;
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            if (faceUp) go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var f = go.AddComponent<FxLife>();
            f.Life = life;
            f.Billboard = !faceUp;
            f.Renderer = mr;
            return go;
        }

        public static void MuzzleFlash(Vector3 pos, Vector3 dir)
        {
            var m = Mats.Overlay("fx_flash", new Color(1f, 0.85f, 0.5f, 1f), "radial", false, true);
            var go = Billboard("flash", pos + dir * 0.2f, 0.9f, m, 0.08f);
            go.GetComponent<FxLife>().Grow = 2f;
            var lgo = new GameObject("flashlight");
            lgo.transform.SetParent(go.transform, false);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 8f;
            l.intensity = 6f;
            l.color = new Color(1f, 0.8f, 0.5f);
            l.shadows = LightShadows.None;
        }

        /// <summary>A fading line from a to b (bullet / bolt trail).</summary>
        public static void Tracer(Vector3 a, Vector3 b, bool silver)
        {
            var go = new GameObject("tracer");
            go.transform.SetParent(Root, false);
            go.layer = Layers.Overlay;
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = 0.05f;
            lr.endWidth = 0.02f;
            lr.sharedMaterial = Mats.Overlay(silver ? "fx_tracer_silver" : "fx_tracer", silver ? new Color(0.85f, 0.9f, 1f, 0.9f) : new Color(1f, 0.85f, 0.55f, 0.8f), null, false, true);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            var f = go.AddComponent<FxLife>();
            f.Life = 0.15f;
            f.Line = lr;
        }

        /// <summary>A flat ring on the ground that stays until hidden or destroyed by its owner (the ring under a controlled thrall).</summary>
        public static GameObject GroundMarker(string name, Color c)
        {
            var go = new GameObject(name);
            go.layer = Layers.Overlay;
            go.transform.SetParent(Root, false);
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Overlay("fx_mark_" + ColorUtility.ToHtmlStringRGB(c), c, "thinring", false, true);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        /// <summary>Expanding ground ring (noise, ability pulse, ping).</summary>
        public static void Ring(Vector3 pos, float radius, Color c, float life = 0.6f)
        {
            var m = Mats.Overlay("fx_ring_" + ColorUtility.ToHtmlStringRGB(c), c, "ring", false, true);
            var go = Billboard("ring", pos + Vector3.up * 0.08f, 0.2f, m, life, true);
            var f = go.GetComponent<FxLife>();
            f.GrowTo = radius * 2f;
        }

        public static void BloodBurst(Vector3 pos, float size = 1f)
        {
            var m = Mats.Overlay("fx_blood", new Color(0.55f, 0.02f, 0.05f, 0.95f), "radial", false, false);
            for (int i = 0; i < 6; i++)
            {
                var go = Billboard("blood", pos + Random.insideUnitSphere * 0.2f * size, 0.18f * size, m, 0.6f);
                var f = go.GetComponent<FxLife>();
                f.Velocity = (Random.insideUnitSphere + Vector3.up) * 2.2f * size;
                f.Gravity = 9f;
            }
        }

        /// <summary>Swirling red motes flowing from a to b (feeding).</summary>
        public static void BloodStream(Vector3 from, Vector3 to)
        {
            var m = Mats.Overlay("fx_mote", new Color(0.9f, 0.1f, 0.15f, 0.9f), "radial", false, true);
            var go = Billboard("mote", from + Random.insideUnitSphere * 0.15f, 0.08f, m, 0.4f);
            go.GetComponent<FxLife>().Velocity = (to - from) / 0.4f;
        }

        public static void Smoke(Vector3 pos, Color c, float size = 1f, float life = 1.2f)
        {
            var m = Mats.Overlay("fx_smoke_" + ColorUtility.ToHtmlStringRGB(c), c, "radial", false, false);
            var go = Billboard("smoke", pos, 0.5f * size, m, life);
            var f = go.GetComponent<FxLife>();
            f.Velocity = Vector3.up * 0.6f + Random.insideUnitSphere * 0.2f;
            f.Grow = 1.2f;
        }

        /// <summary>A gas explosion: a rising, swelling ball of flame that lights the whole district, then fades.</summary>
        public static void Fireball(Vector3 pos, float size)
        {
            var m = Mats.Overlay("fx_fireball", new Color(1f, 0.55f, 0.18f, 0.9f), "radial", false, true);
            var core = Mats.Overlay("fx_fireball_core", new Color(1f, 0.9f, 0.6f, 1f), "radial", false, true);
            for (int i = 0; i < 7; i++)
            {
                var go = Billboard("fireball", pos + Random.insideUnitSphere * size * 0.25f + Vector3.up * size * 0.2f, size * 0.3f, i == 0 ? core : m, 1.4f + i * 0.25f);
                var f = go.GetComponent<FxLife>();
                f.GrowTo = size * Random.Range(0.9f, 1.4f);
                f.Velocity = Vector3.up * Random.Range(2f, 5f) + Random.insideUnitSphere * 1.5f;
            }
            var anchor = Billboard("fireglow", pos + Vector3.up * size * 0.5f, 0.01f, core, 3.5f);
            var l = anchor.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = size * 4f;
            l.intensity = 60f;
            l.color = new Color(1f, 0.6f, 0.3f);
            l.shadows = LightShadows.None;
            anchor.GetComponent<FxLife>().Glow = l;
        }

        public static void Sparks(Vector3 pos, Color c)
        {
            var m = Mats.Overlay("fx_spark_" + ColorUtility.ToHtmlStringRGB(c), c, "radial", false, true);
            for (int i = 0; i < 8; i++)
            {
                var go = Billboard("spark", pos, 0.07f, m, 0.5f);
                var f = go.GetComponent<FxLife>();
                f.Velocity = Random.insideUnitSphere * 3f + Vector3.up;
                f.Gravity = 6f;
            }
        }

        /// <summary>A burning flare lobbed in an arc that lands and lights the area for a while.</summary>
        public static GameLight Flare(Vector3 from, Vector3 to, float duration = 25f)
        {
            var go = new GameObject("flare");
            go.transform.SetParent(Root, false);
            go.transform.position = from;
            var l = go.AddComponent<GameLight>();
            l.Id = "flare_" + Time.frameCount;
            l.Setup(LightKind.Fire);
            l.Radius = 6.5f;
            l.Intensity = 1.1f;
            l.Height = 0.4f;
            l.Color = Util.Hex("#ff5a3a");
            l.Snuffable = true;
            l.Caged = false;
            l.BuildVisual(Vector3.zero);
            var arc = go.AddComponent<FxArc>();
            arc.From = from;
            arc.To = to;
            arc.Duration = 0.9f;
            var life = go.AddComponent<FxLife>();
            life.Life = duration;
            life.KeepScale = true;
            Game.Audio?.PlayAt("flare", from, 0.7f);
            return l;
        }
    }

    public class FxLife : MonoBehaviour
    {
        public float Life = 1f, Grow, GrowTo, Gravity;
        public Light Glow;                 // optional light that fades out with the effect
        float _glow0;
        public Vector3 Velocity;
        public bool Billboard, KeepScale;
        public Renderer Renderer;
        public LineRenderer Line;
        float _t;
        Vector3 _scale0;
        MaterialPropertyBlock _mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        Color _c0;

        void Start()
        {
            _scale0 = transform.localScale;
            if (Glow) _glow0 = Glow.intensity;
            if (Renderer) _c0 = Renderer.sharedMaterial.HasProperty(ColorId) ? Renderer.sharedMaterial.GetColor(ColorId) : Color.white;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Life);
            if (Velocity != Vector3.zero || Gravity != 0f)
            {
                Velocity += Vector3.down * Gravity * Time.deltaTime;
                transform.position += Velocity * Time.deltaTime;
            }
            if (!KeepScale)
            {
                if (GrowTo > 0f) transform.localScale = Vector3.one * Mathf.Lerp(_scale0.x, GrowTo, Mathf.Sqrt(k));
                else if (Grow != 0f) transform.localScale = _scale0 * (1f + Grow * k);
            }
            if (Billboard && Game.Cam != null && Game.Cam.Cam) transform.rotation = Game.Cam.Cam.transform.rotation;
            if (Renderer)
            {
                _mpb ??= new MaterialPropertyBlock();
                var c = _c0;
                c.a *= 1f - k;
                _mpb.SetColor(ColorId, c);
                Renderer.SetPropertyBlock(_mpb);
            }
            if (Glow) Glow.intensity = _glow0 * (1f - k) * (1f - k);
            if (Line)
            {
                var c = Line.startColor;
                c.a = 1f - k;
                Line.startColor = c;
                Line.endColor = c;
            }
            if (_t >= Life) Destroy(gameObject);
        }
    }

    public class FxArc : MonoBehaviour
    {
        public Vector3 From, To;
        public float Duration = 1f, Height = 3f;
        float _t;

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Duration);
            var p = Vector3.Lerp(From, To, k);
            p.y += Mathf.Sin(k * Mathf.PI) * Height;
            transform.position = p;
            if (k >= 1f) Destroy(this);
        }
    }
}
