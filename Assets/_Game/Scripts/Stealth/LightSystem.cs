using System.Collections.Generic;
using UnityEngine;
using Vespertine.Core;

namespace Vespertine.Stealth
{
    /// <summary>Gameplay light field: ambient + brightest occlusion-tested source.</summary>
    public class LightSystem
    {
        public readonly List<GameLight> All = new List<GameLight>();
        public float Ambient = 0.06f;
        public float GlobalScale = 1f; // blackout etc.

        struct DarkZone { public Vector3 C; public float R, Until; }
        readonly List<DarkZone> _dark = new List<DarkZone>();

        /// <summary>Magical darkness (Gloom): points inside read as near-black regardless of lamps.</summary>
        public void AddDarkness(Vector3 c, float r, float duration) => _dark.Add(new DarkZone { C = c, R = r, Until = Time.time + duration });
        public bool InDarkness(Vector3 p)
        {
            for (int i = _dark.Count - 1; i >= 0; i--)
            {
                if (Time.time > _dark[i].Until) { _dark.RemoveAt(i); continue; }
                if ((p - _dark[i].C).sqrMagnitude < _dark[i].R * _dark[i].R) return true;
            }
            return false;
        }

        float _budgetAt;
        readonly List<GameLight> _shadowSort = new List<GameLight>();

        /// <summary>Only the lit lamps nearest the camera cast real-time shadows: with every lamp casting, URP shrinks
        /// each map until none of them read. Re-ranked a few times a second.</summary>
        public void BudgetShadows(Vector3 focus, int max)
        {
            if (Time.unscaledTime < _budgetAt) return;
            _budgetAt = Time.unscaledTime + 0.3f;
            _shadowSort.Clear();
            foreach (var l in All)
            {
                if (!l) continue;
                if (l.WantsShadow) _shadowSort.Add(l); else l.AllowShadow(false);
            }
            _sortFocus = focus;
            _shadowSort.Sort(ByFocusDistance);
            for (int i = 0; i < _shadowSort.Count; i++) _shadowSort[i].AllowShadow(i < max);
        }

        static Vector3 _sortFocus;
        static readonly System.Comparison<GameLight> ByFocusDistance =
            (a, b) => (a.transform.position - _sortFocus).sqrMagnitude.CompareTo((b.transform.position - _sortFocus).sqrMagnitude);

        ExposureField _field;

        /// <summary>The Exposure Field (SR.3) for the level in play, baked on first use; null outside a level.</summary>
        public ExposureField Field
        {
            get
            {
                var lv = Game.Level;
                if (lv == null || lv.Grid == null) return _field = null;
                if (_field == null || _field.Level != lv || _field.Grid != lv.Grid) _field = new ExposureField(this, lv);
                return _field;
            }
        }

        public void Register(GameLight l) { if (!All.Contains(l)) All.Add(l); }
        public void Unregister(GameLight l) { All.Remove(l); }

        public float LightAt(Vector3 feet, float heightOffset = 1.0f)
        {
            var p = feet + Vector3.up * heightOffset;
            if (_dark.Count > 0 && InDarkness(p)) return Ambient * 0.5f;
            float best = 0f;
            for (int i = 0; i < All.Count; i++)
            {
                var l = All[i];
                if (!l.On || !l.isActiveAndEnabled) continue;
                var src = l.SourcePos;
                float d = Vector3.Distance(src, p);
                if (d >= l.Radius) continue;
                float v = DetectionMath.LightFalloff(l.Intensity, l.Radius, d);
                if (v <= best) continue;
                if (l.Kind != LightKind.Moon && Physics.Linecast(src, p, Layers.LightBlockMask, QueryTriggerInteraction.Ignore)) continue;
                best = v;
            }
            return Mathf.Clamp01(Ambient + best * GlobalScale);
        }

        /// <summary>The light that decides <see cref="LightAt"/> at a point (the brightest unblocked source), or null.</summary>
        public GameLight Brightest(Vector3 feet, float heightOffset = 1.0f)
        {
            var p = feet + Vector3.up * heightOffset;
            float best = 0f; GameLight bl = null;
            for (int i = 0; i < All.Count; i++)
            {
                var l = All[i];
                if (!l.On || !l.isActiveAndEnabled) continue;
                float d = Vector3.Distance(l.SourcePos, p);
                if (d >= l.Radius) continue;
                float v = DetectionMath.LightFalloff(l.Intensity, l.Radius, d);
                if (v <= best) continue;
                if (l.Kind != LightKind.Moon && Physics.Linecast(l.SourcePos, p, Layers.LightBlockMask, QueryTriggerInteraction.Ignore)) continue;
                best = v; bl = l;
            }
            return bl;
        }

        /// <summary>Flat radius around a light's base inside which it exposes her on the current night (ambient and
        /// blackout scale included). See <see cref="DetectionMath.ExposureRadius"/>.</summary>
        public float ExposureRadius(GameLight l, float threshold = DetectionMath.ExposedAt) =>
            DetectionMath.ExposureRadius(l.Intensity, l.Radius, l.Height - 1f, Ambient, GlobalScale, threshold);

        /// <summary>Strongest burning source affecting a point (sunstone/holy), 0..1.</summary>
        public float BurnAt(Vector3 feet)
        {
            var p = feet + Vector3.up;
            float best = 0f;
            foreach (var l in All)
            {
                if (!l.On || !l.Burns || !l.isActiveAndEnabled) continue;
                float d = Vector3.Distance(l.SourcePos, p);
                if (d >= l.Radius * 0.85f) continue;
                if (Physics.Linecast(l.SourcePos, p, Layers.LightBlockMask, QueryTriggerInteraction.Ignore)) continue;
                best = Mathf.Max(best, 1f - d / (l.Radius * 0.85f));
            }
            return best;
        }

        public GameLight Nearest(Vector3 p, float maxDist, System.Func<GameLight, bool> pred)
        {
            GameLight best = null; float bd = maxDist;
            foreach (var l in All)
            {
                if (!l.isActiveAndEnabled || !pred(l)) continue;
                float d = Util.FlatDistance(l.transform.position, p);
                if (d < bd) { bd = d; best = l; }
            }
            return best;
        }
    }
}
