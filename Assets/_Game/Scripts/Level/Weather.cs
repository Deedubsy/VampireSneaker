using UnityEngine;
using Vespertine.Core;
using Vespertine.Stealth;

namespace Vespertine.Level
{
    /// <summary>
    /// Rain (header <c>rain = 1</c>, script <c>weather rain|clear</c>). Rain is a stealth state, not decoration:
    /// it drowns scent (hounds and trackers smell her at half range, trail drops wash away after
    /// <see cref="TrailWashSeconds"/>) and covers footfalls. Squalls that come and go give a hunt its rhythm.
    /// </summary>
    public class Weather : MonoBehaviour
    {
        public static bool Raining { get; private set; }
        public const float TrailWashSeconds = 30f;
        public const float SmellMul = 0.5f;      // nose range in rain
        public const float FootfallMul = 0.6f;   // footstep noise radius in rain

        static Weather _inst;
        static string _ambA = "amb_night", _ambB = "amb_drip";
        ParticleSystem _ps;
        float _washT;

        /// <summary>Mission start: remember the level's ambience so a clearing sky can restore it.</summary>
        public static void Begin(Transform parent, bool rain, string ambA, string ambB)
        {
            _ambA = ambA; _ambB = ambB;
            if (_inst == null)
            {
                var go = new GameObject("Weather");
                go.transform.SetParent(parent, false);
                _inst = go.AddComponent<Weather>();
            }
            Raining = !rain;   // force the change through
            Set(rain);
        }

        public static void Set(bool rain)
        {
            if (Raining == rain && _inst != null) return;
            Raining = rain;
            if (_inst != null) _inst.Apply();
            Game.Audio?.SetAmbience(_ambA, rain ? "amb_rain" : _ambB);
        }

        /// <summary>Mission teardown: the sky clears for the menus.</summary>
        public static void End() { Raining = false; }

        void Apply()
        {
            if (Raining && _ps == null) _ps = BuildRain(transform);
            if (_ps == null) return;
            if (Raining) _ps.Play(); else _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void OnDestroy() { if (_inst == this) { _inst = null; Raining = false; } }

        void LateUpdate()
        {
            if (_ps != null && Game.Cam != null)
                _ps.transform.position = Game.Cam.Pivot + Vector3.up * 14f;
            if (!Raining) return;
            _washT -= Time.deltaTime;
            if (_washT > 0f) return;
            _washT = 1f;
            for (int i = Evidence.Stains.Count - 1; i >= 0; i--)
            {
                var s = Evidence.Stains[i];
                if (s && s.IsTrail && Time.time - s.Born > TrailWashSeconds) Destroy(s.gameObject);
            }
        }

        static ParticleSystem BuildRain(Transform parent)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // emit downwards
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 0.75f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(20f, 26f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startColor = new Color(0.62f, 0.68f, 0.78f, 0.32f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4000;
            main.gravityModifier = 0.4f;

            var em = ps.emission;
            em.rateOverTime = 3600f;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(46f, 46f, 1f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);   // a west wind
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f;
            r.lengthScale = 1f;
            r.sharedMaterial = Visual.Mats.Overlay("fx_rain", Color.white, null, false, true);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.layer = Layers.Overlay;
            return ps;
        }
    }
}
