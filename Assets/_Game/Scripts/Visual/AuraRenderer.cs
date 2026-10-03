using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Player;

namespace Vespertine.Visual
{
    /// <summary>
    /// Holy ground made visible: a pale gold ring at the edge of every waking priest's aura
    /// (<see cref="Vampire.HolyAuraRadius"/>), the line inside which Shade, Dominion and Sanguis will not answer her.
    /// Faint at a distance, brighter as she nears it, and strongest while she stands inside. Drawn with depth, so a
    /// priest behind a wall or under a roof gives nothing away.
    /// </summary>
    public class AuraRenderer : MonoBehaviour
    {
        public const float ShowRange = 26f;

        class Aura { public Transform Ring, Fill; public MeshRenderer RingR, FillR; }

        static readonly Color Gold = Mats.Pal.Holy;
        readonly List<Aura> _pool = new List<Aura>();
        MaterialPropertyBlock _mpb;
        Material _ringMat, _fillMat;
        int _used;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _ringMat = Mats.Overlay("aura_ring", Gold, "thinring");
            _fillMat = Mats.Overlay("aura_fill", Gold, "radial");
        }

        void LateUpdate()
        {
            _used = 0;
            var p = Game.Player;
            var ai = Game.AI;
            if (p != null && !p.Dead && ai != null && Game.InMission)
            {
                var feet = p.Feet;
                float r = Vampire.HolyAuraRadius;
                foreach (var n in ai.Living())
                {
                    if (!n.Arch.Has(ArchFlags.HolyAura) || n.Incapacitated || !n.gameObject.activeInHierarchy) continue;
                    float d = Util.FlatDistance(n.transform.position, feet);
                    if (d > ShowRange || Mathf.Abs(n.transform.position.y - feet.y) > 4f && d > r) continue;
                    // 0.18 at the edge of view, rising to 0.55 at the ring, 0.85 inside it
                    float a = d < r ? 0.85f : Mathf.Lerp(0.55f, 0.18f, Mathf.InverseLerp(r, ShowRange, d));
                    a *= 0.9f + 0.1f * Mathf.Sin(Time.time * 1.6f + n.Id.GetHashCode() % 7);
                    var au = Get();
                    var pos = n.transform.position + Vector3.up * 0.07f;
                    au.Ring.position = pos;
                    au.Ring.localScale = Vector3.one * r * 2f;
                    au.Fill.position = pos - Vector3.up * 0.01f;
                    au.Fill.localScale = Vector3.one * r * 2f;
                    Tint(au.RingR, a);
                    Tint(au.FillR, a * 0.16f);
                }
            }
            for (int i = _used; i < _pool.Count; i++)
                if (_pool[i].Ring.gameObject.activeSelf) { _pool[i].Ring.gameObject.SetActive(false); _pool[i].Fill.gameObject.SetActive(false); }
        }

        void Tint(Renderer rr, float a)
        {
            var c = Gold; c.a = a;
            _mpb.SetColor("_Color", c);
            rr.SetPropertyBlock(_mpb);
        }

        Aura Get()
        {
            Aura au;
            if (_used < _pool.Count) au = _pool[_used];
            else
            {
                au = new Aura();
                au.Fill = Quad("AuraFill", _fillMat, out au.FillR);
                au.Ring = Quad("AuraRing", _ringMat, out au.RingR);
                _pool.Add(au);
            }
            _used++;
            if (!au.Ring.gameObject.activeSelf) { au.Ring.gameObject.SetActive(true); au.Fill.gameObject.SetActive(true); }
            return au;
        }

        Transform Quad(string name, Material mat, out MeshRenderer mr)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.layer = Layers.Overlay;
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }
    }
}
