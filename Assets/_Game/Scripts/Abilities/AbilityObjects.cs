using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.Abilities
{
    static class AbilityVisual
    {
        public static GameObject Quad(string name, Transform parent, Vector3 pos, float size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.layer = Layers.Overlay;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * size;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        public static Transform Root => Game.Level != null && Game.Level.DynamicRoot ? Game.Level.DynamicRoot : null;
    }

    /// <summary>
    /// Gloom: a sphere of magical darkness. A solid collider on the Foliage layer blocks human sight lines into it
    /// (not navigation, not clicks), lamps read as dark inside it, and humans inside are blinded.
    /// With Shroud of Sleep, humans who stay inside for 3 s fall asleep.
    /// </summary>
    public class GloomSphere : MonoBehaviour
    {
        public float Radius, Until;
        public bool Shroud;
        readonly Dictionary<Npc, float> _inside = new Dictionary<Npc, float>();
        Renderer _cloud, _disc;
        float _tick;

        public static GloomSphere Create(Vector3 p, float radius, float duration, bool shroud)
        {
            var go = new GameObject("Gloom");
            go.transform.SetParent(AbilityVisual.Root, false);
            go.transform.position = p;
            go.layer = Layers.Foliage;
            var col = go.AddComponent<SphereCollider>();
            col.radius = radius * 0.85f;
            col.center = Vector3.up * 0.5f;
            var g = go.AddComponent<GloomSphere>();
            g.Radius = radius;
            g.Until = Time.time + duration;
            g.Shroud = shroud;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Cloud";
            Destroy(sphere.GetComponent<Collider>());
            sphere.layer = Layers.Overlay;
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localPosition = Vector3.up * 0.5f;
            sphere.transform.localScale = Vector3.one * radius * 2f;
            var mr = sphere.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Overlay("gloom_cloud", new Color(0.02f, 0.02f, 0.05f, 0.62f), "radial");
            mr.shadowCastingMode = ShadowCastingMode.Off;
            g._cloud = mr;
            g._disc = AbilityVisual.Quad("Disc", go.transform, p + Vector3.up * 0.06f, radius * 2.2f,
                Mats.Overlay("gloom_disc", new Color(0f, 0f, 0.02f, 0.85f), "radial")).GetComponent<Renderer>();

            Game.Lights?.AddDarkness(p, radius, duration);
            Game.AI?.AddBlindZone(p, radius, duration);
            return g;
        }

        void Update()
        {
            float left = Until - Time.time;
            if (left <= 0f) { Destroy(gameObject); return; }
            float fade = Mathf.Clamp01(left / 1.5f) * Mathf.Clamp01((Time.time - (Until - 15f)) / 0.5f + 0.2f);
            if (_cloud) _cloud.transform.localScale = Vector3.one * Radius * 2f * (0.85f + 0.15f * fade);
            transform.Rotate(0f, 12f * Time.deltaTime, 0f);

            if (!Shroud || Game.AI == null) return;
            _tick -= Time.deltaTime;
            if (_tick > 0f) return;
            _tick = 0.25f;
            var seen = new List<Npc>();
            foreach (var n in Game.AI.Living())
            {
                if (n.Asleep || n.Incapacitated) continue;
                if ((n.transform.position - transform.position).sqrMagnitude > Radius * Radius) continue;
                seen.Add(n);
                _inside.TryGetValue(n, out var t);
                t += 0.25f;
                _inside[n] = t;
            }
            foreach (var n in seen)
                if (_inside[n] >= 3f && n.FallAsleep())
                    Fx.Ring(n.transform.position, 1.2f, Mats.Pal.Shade, 0.8f);
            var gone = new List<Npc>();
            foreach (var kv in _inside) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var n in gone) _inside.Remove(n);
        }
    }

    /// <summary>Eclipse: relights the lamps it killed after a delay (lamplighters may get there first).</summary>
    public class EclipseTimer : MonoBehaviour
    {
        List<GameLight> _lights;
        float _at;

        public static void Create(List<GameLight> lights, float delay)
        {
            var go = new GameObject("EclipseTimer");
            go.transform.SetParent(AbilityVisual.Root, false);
            var t = go.AddComponent<EclipseTimer>();
            t._lights = lights;
            t._at = Time.time + delay;
            if (Game.Lights != null) Game.Lights.GlobalScale = 0.25f;
        }

        void Update()
        {
            if (Game.Lights != null) Game.Lights.GlobalScale = Mathf.MoveTowards(Game.Lights.GlobalScale, Time.time < _at - 22f ? 0.25f : 1f, Time.deltaTime * 0.25f);
            if (Time.time < _at) return;
            foreach (var l in _lights) if (l && !l.On) l.SetOn(true);
            if (Game.Lights != null) Game.Lights.GlobalScale = 1f;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Game.Lights != null) Game.Lights.GlobalScale = 1f;
        }
    }

    /// <summary>Snare rune: a blood sigil on the ground. The first human to step on it is dazed.</summary>
    public class SnareRune : MonoBehaviour
    {
        public const float Radius = 0.9f;
        public const float LureRange = 20f;
        float _tick, _lure = 1f;
        Transform _glyph;

        public static SnareRune Create(Vector3 p)
        {
            var go = new GameObject("SnareRune");
            go.transform.SetParent(AbilityVisual.Root, false);
            go.transform.position = p;
            var r = go.AddComponent<SnareRune>();
            r._glyph = AbilityVisual.Quad("Glyph", go.transform, p + Vector3.up * 0.05f, Radius * 2.4f,
                Mats.Overlay("snare_rune", new Color(0.75f, 0.05f, 0.1f, 0.8f), "ring", false, true)).transform;
            AbilityVisual.Quad("Core", go.transform, p + Vector3.up * 0.04f, Radius * 1.2f,
                Mats.Overlay("snare_core", new Color(0.45f, 0.02f, 0.05f, 0.7f), "radial"));
            return r;
        }

        void Update()
        {
            if (_glyph) _glyph.Rotate(0f, 0f, 40f * Time.deltaTime, Space.Self);
            _tick -= Time.deltaTime;
            if (_tick > 0f || Game.AI == null) return;
            _tick = 0.1f;
            _lure -= 0.1f;
            if (_lure <= 0f) { _lure = 2f; Lure(); }
            foreach (var n in Game.AI.Living())
            {
                if (n.Incapacitated || n.State == NpcState.Thrall || n.Arch.Has(ArchFlags.Quadruped) && n.Arch.Has(ArchFlags.Armored)) continue;
                if (Util.FlatDistance(n.transform.position, transform.position) > Radius || Mathf.Abs(n.transform.position.y - transform.position.y) > 1f) continue;
                if (n.CarriesSalt)
                {
                    // salt breaks the sigil, and the man who broke it knows what it was
                    Fx.Smoke(transform.position + Vector3.up * 0.2f, new Color(0.9f, 0.9f, 0.88f), 0.8f, 1f);
                    Game.Audio?.PlayAt("snuff", transform.position, 0.6f, 0.7f);
                    GameEvents.RaiseBark(n.Id, "Blood-craft. Salt it.");
                    n.EnterInvestigating(transform.position, false, true);
                    Destroy(gameObject);
                    return;
                }
                n.Daze(12f, false);
                Fx.BloodBurst(transform.position + Vector3.up * 0.3f, 0.8f);
                Fx.Ring(transform.position, 1.5f, Mats.Pal.BloodBright, 0.5f);
                Game.Audio?.PlayAt("cast", transform.position, 0.5f, 0.6f);
                Game.Noise?.Emit(transform.position, 3f, NoiseKind.Body, null);
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>False Trail: calm hounds and trackers in range walk over to sniff the rune (and step on it).</summary>
        void Lure()
        {
            if (Game.Campaign == null || !Game.Campaign.Has("sanguis.false_trail")) return;
            foreach (var n in Game.AI.Living())
            {
                if (!n.Arch.Has(ArchFlags.Smell) || n.Incapacitated || n.Friendly) continue;
                if (n.State != NpcState.Relaxed && n.State != NpcState.Holding && n.State != NpcState.Searching) continue;
                if (Vector3.Distance(n.transform.position, transform.position) > LureRange) continue;
                n.EnterDistracted(transform.position, 4f, false);
                n.Say(BarkKind.Smell, true);
            }
        }
    }
}
