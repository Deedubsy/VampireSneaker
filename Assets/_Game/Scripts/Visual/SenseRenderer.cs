using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Stealth;

namespace Vespertine.Visual
{
    /// <summary>
    /// Heartbeats drawn through walls (x-ray overlay).
    ///  - Blood Sense (held): every living creature within 30 m, coloured by state, pulsing at their heart rate,
    ///    with the path they are about to walk.
    ///  - Scent of Blood (passive): the dazed, the wounded, hounds and blood stains within 30 m, always.
    ///  - Dread Feast: witnesses stay revealed while <see cref="Npc.RevealedUntil"/> runs.
    /// </summary>
    public class SenseRenderer : MonoBehaviour
    {
        public const float Range = 30f;
        const int PathMax = 12;

        class Marker
        {
            public GameObject Root;
            public Transform Heart, Ring;
            public MeshRenderer HeartR, RingR;
            public LineRenderer Path;
            public float Phase;
        }

        readonly List<Marker> _pool = new List<Marker>();
        readonly List<Transform> _stainPool = new List<Transform>();
        readonly Vector3[] _corners = new Vector3[PathMax];
        MaterialPropertyBlock _mpb;
        Material _heartMat, _ringMat, _pathMat, _stainMat;
        int _used, _stainsUsed;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _heartMat = Mats.Overlay("sense_heart", Color.white, "radial", true);
            _ringMat = Mats.Overlay("sense_ring", Color.white, "ring", true);
            _pathMat = Mats.Overlay("sense_path", Color.white, null, true);
            _stainMat = Mats.Overlay("sense_stain", new Color(0.9f, 0.08f, 0.1f, 0.55f), "splat", true);
        }

        void LateUpdate()
        {
            _used = 0;
            _stainsUsed = 0;
            var p = Game.Player;
            var ai = Game.AI;
            if (p != null && !p.Dead && ai != null && Game.InMission && Game.Level != null)
            {
                bool full = p.Sensing;
                bool scent = Game.Campaign != null && Game.Campaign.Has("predator.scent");
                var from = p.Feet;
                foreach (var n in ai.Npcs)
                {
                    if (!n || n.State == NpcState.Dead || n.Disposed || !n.gameObject.activeInHierarchy) continue;
                    if (Util.FlatDistance(n.transform.position, from) > Range) continue;
                    bool revealed = full || Time.time < n.RevealedUntil
                        || scent && (n.State == NpcState.Dazed || n.HP < n.Arch.HP || n.Arch.Has(ArchFlags.Smell) || n.Arch.Has(ArchFlags.Quadruped));
                    if (!revealed) continue;
                    Draw(n, full);
                }
                if (scent || full)
                    foreach (var s in Evidence.Stains)
                        if (s && Util.FlatDistance(s.transform.position, from) <= Range) DrawStain(s);
            }
            for (int i = _used; i < _pool.Count; i++) if (_pool[i].Root.activeSelf) _pool[i].Root.SetActive(false);
            for (int i = _stainsUsed; i < _stainPool.Count; i++) if (_stainPool[i].gameObject.activeSelf) _stainPool[i].gameObject.SetActive(false);
        }

        static Color ColorFor(Npc n)
        {
            switch (n.State)
            {
                case NpcState.Thrall: return Mats.Pal.Dominion;
                case NpcState.Dazed: case NpcState.Mesmerised: case NpcState.Victim: return new Color(0.55f, 0.12f, 0.16f);
                case NpcState.Alerted: case NpcState.Panicked: return new Color(1f, 0.18f, 0.12f);
                case NpcState.Suspicious: case NpcState.Investigating: case NpcState.Searching: return new Color(1f, 0.55f, 0.15f);
            }
            if (n.Friendly) return new Color(0.95f, 0.85f, 0.7f);
            return n.Notable ? new Color(1f, 0.82f, 0.3f) : new Color(0.85f, 0.1f, 0.14f);
        }

        /// <summary>Beats per second: frightened hearts race, sleepers' slow.</summary>
        static float Rate(Npc n)
        {
            switch (n.State)
            {
                case NpcState.Alerted: case NpcState.Panicked: case NpcState.Victim: return 2.4f;
                case NpcState.Suspicious: case NpcState.Investigating: case NpcState.Searching: return 1.7f;
                case NpcState.Dazed: case NpcState.Mesmerised: return 0.75f;
            }
            return n.Asleep ? 0.8f : 1.15f;
        }

        void Draw(Npc n, bool withPath)
        {
            var m = Get();
            var cam = Game.Cam != null ? Game.Cam.Cam : Camera.main;
            var col = ColorFor(n);
            var pos = n.transform.position;
            bool lying = n.State == NpcState.Dazed || n.Asleep && !n.Sitting;
            m.Heart.position = pos + Vector3.up * (lying ? 0.35f : n.Arch.Has(ArchFlags.Quadruped) ? 0.6f : 1.25f);
            if (cam) m.Heart.rotation = Quaternion.LookRotation(m.Heart.position - cam.transform.position, cam.transform.up);
            // lub-dub: two quick swells per beat
            m.Phase += Time.deltaTime * Rate(n);
            float t = m.Phase - Mathf.Floor(m.Phase);
            float beat = Mathf.Max(Pulse(t, 0.0f), 0.7f * Pulse(t, 0.18f));
            m.Heart.localScale = Vector3.one * (0.95f + 0.55f * beat) * (n.Arch.Has(ArchFlags.Quadruped) ? 0.8f : 1f);
            Tint(m.HeartR, col, 0.75f + 0.25f * beat);
            m.Ring.position = pos + Vector3.up * 0.06f;
            m.Ring.localScale = Vector3.one * (1.1f + 0.5f * beat);
            Tint(m.RingR, col, 0.35f + 0.3f * beat);

            int count = 0;
            if (withPath && n.Agent && n.Agent.enabled && n.Agent.hasPath && n.State != NpcState.Thrall)
            {
                count = n.Agent.path.GetCornersNonAlloc(_corners);
                if (count > PathMax) count = PathMax;
            }
            if (count >= 2)
            {
                m.Path.enabled = true;
                m.Path.positionCount = count;
                for (int i = 0; i < count; i++) m.Path.SetPosition(i, _corners[i] + Vector3.up * 0.12f);
                var c = col; c.a = 0.55f;
                m.Path.startColor = c;
                c.a = 0.08f;
                m.Path.endColor = c;
            }
            else m.Path.enabled = false;
        }

        static float Pulse(float t, float at)
        {
            float d = (t - at) / 0.09f;
            return d < 0f || d > 1f ? 0f : Mathf.Sin(d * Mathf.PI);
        }

        void Tint(Renderer r, Color c, float a)
        {
            c.a = a;
            _mpb.SetColor("_Color", c);
            r.SetPropertyBlock(_mpb);
        }

        void DrawStain(BloodStain s)
        {
            Transform t;
            if (_stainsUsed < _stainPool.Count) t = _stainPool[_stainsUsed];
            else
            {
                t = Quad("SenseStain", _stainMat).transform;
                _stainPool.Add(t);
            }
            _stainsUsed++;
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t.position = s.transform.position + Vector3.up * 0.08f;
            t.rotation = Quaternion.Euler(90f, 0f, 0f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            t.localScale = Vector3.one * (s.Huge ? 3.2f : 1.6f) * pulse;
        }

        Marker Get()
        {
            Marker m;
            if (_used < _pool.Count) m = _pool[_used];
            else
            {
                m = new Marker { Root = new GameObject("Heartbeat"), Phase = Random.value };
                m.Root.transform.SetParent(transform, false);
                var h = Quad("Heart", _heartMat);
                h.transform.SetParent(m.Root.transform, true);
                m.Heart = h.transform;
                m.HeartR = h.GetComponent<MeshRenderer>();
                var r = Quad("Ring", _ringMat);
                r.transform.SetParent(m.Root.transform, true);
                r.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                m.Ring = r.transform;
                m.RingR = r.GetComponent<MeshRenderer>();
                var lr = new GameObject("Path").AddComponent<LineRenderer>();
                lr.transform.SetParent(m.Root.transform, false);
                lr.gameObject.layer = Layers.Overlay;
                lr.sharedMaterial = _pathMat;
                lr.widthMultiplier = 0.12f;
                lr.numCornerVertices = 2;
                lr.alignment = LineAlignment.View;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.useWorldSpace = true;
                m.Path = lr;
                _pool.Add(m);
            }
            _used++;
            if (!m.Root.activeSelf) m.Root.SetActive(true);
            return m;
        }

        GameObject Quad(string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.layer = Layers.Overlay;
            go.transform.SetParent(transform, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }
    }
}
