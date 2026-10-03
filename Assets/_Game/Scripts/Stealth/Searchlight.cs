using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Visual;

namespace Vespertine.Stealth
{
    /// <summary>
    /// A searchlight (M10+): an arc lamp on a tower throwing a moving pool of light.
    /// <c>light id x y searchlight from=x,y[,h] sweep=x1,y1,x2,y2,... speed=2.5 op=npcId [group=g]</c>.
    /// The <see cref="GameLight"/> sits at the pool and moves with it, so the light field and every guard's
    /// perception use it like any other lamp. While its operator mans it, the pool sweeps the listed cells
    /// (ping-pong, with a short dwell at each end) and follows whatever the operator is looking at: a noise,
    /// a suspicion, Ilse herself. The operator never leaves the lamp, and sees anything the pool falls on at any
    /// distance. Kill, daze or turn the operator and the beam stops where it is; cut its <c>group=</c> at a
    /// generator and it goes dark.
    /// </summary>
    public class Searchlight : MonoBehaviour
    {
        public GameLight Light;
        public Vector3 Mount;
        public string OperatorId;
        public float Speed = 2.5f, TrackSpeed = 5.5f;
        public int Next = 1, Dir = 1;
        readonly List<Vector3> _path = new List<Vector3>();
        float _dwell;
        Npc _op;
        bool _resolved;
        Transform _lampLight, _beam, _ring;
        Light _unity;

        public Npc Operator
        {
            get
            {
                if (!_resolved && Game.Level != null)
                {
                    _resolved = true;
                    _op = OperatorId != null ? Game.Level.Get<Npc>(OperatorId) : null;
                    if (_op) _op.Beam = this;
                }
                return _op;
            }
        }

        /// <summary>The operator is at the lamp and able to aim it.</summary>
        public bool Manned
        {
            get
            {
                var o = Operator;
                return o && o.IsAlive && !o.Incapacitated && o.State != NpcState.Thrall && o.State != NpcState.Mesmerised
                       && !o.Rescue && !o.Asleep && o.gameObject.activeInHierarchy && Util.FlatDistance(o.transform.position, Mount) < 3f;
            }
        }

        public bool Lit => Light && Light.On && Light.isActiveAndEnabled;

        /// <summary>Is a point inside the bright part of the pool?</summary>
        public bool Covers(Vector3 feet)
        {
            if (!Lit) return false;
            var p = transform.position;
            return Mathf.Abs(feet.y - p.y) < 2.5f && Util.FlatDistance(feet, p) < Light.Radius * 0.72f;
        }

        public void Setup(Level.EntitySpec spec, Level.LevelData data, System.Func<float, float, float> surface)
        {
            Light = GetComponent<GameLight>();
            OperatorId = spec.Opt("op");
            Speed = spec.OptFloat("speed", Speed);
            var from = Floats(spec.Opt("from"));
            float fx = from.Count > 0 ? from[0] : spec.X, fy = from.Count > 1 ? from[1] : spec.Y;
            float fh = from.Count > 2 ? from[2] : 7f;
            Mount = data.CellToWorld(fx, fy, surface(fx, fy) + fh);
            _path.Add(transform.position);
            var sw = Floats(spec.Opt("sweep"));
            for (int i = 0; i + 1 < sw.Count; i += 2) _path.Add(data.CellToWorld(sw[i], sw[i + 1], surface(sw[i], sw[i + 1])));
            BuildVisual();
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
            // the lamp housing on its tower, aimed every frame
            var lamp = new GameObject(name + ".lamp");
            lamp.transform.position = Mount;
            lamp.transform.SetParent(Game.Level != null ? Game.Level.EntityRoot : null, true);
            var mb = new MeshBuilder();
            var iron = Mats.Lit("lamp_iron", Util.Hex("#17181c"), null, 0.5f, 0.6f);
            mb.Box(iron, new Vector3(0, -0.15f, 0), new Vector3(0.7f, 0.3f, 0.7f));
            mb.Build(lamp.transform, "base", true, Layers.Prop);
            _lampLight = new GameObject("head").transform;
            _lampLight.SetParent(lamp.transform, false);
            var hb = new MeshBuilder();
            hb.Cylinder(iron, new Vector3(0, -0.35f, 0), 0.38f, 0.7f, 10);
            hb.Build(_lampLight, "drum", true, Layers.Prop);
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(lens.GetComponent<Collider>());
            lens.transform.SetParent(_lampLight, false);
            lens.transform.localPosition = new Vector3(0, 0, 0.36f);
            lens.transform.localRotation = Quaternion.Euler(90, 0, 0);
            lens.transform.localScale = new Vector3(0.66f, 0.02f, 0.66f);
            var lr = lens.GetComponent<Renderer>();
            lr.sharedMaterial = Mats.Lit("searchlight_lens", Light.Color * 0.5f, null, 0.9f, 0, Light.Color * 3f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            _unity = _lampLight.gameObject.AddComponent<Light>();
            _unity.type = LightType.Spot;
            _unity.color = Light.Color;
            _unity.intensity = 9f * Light.Intensity;
            _unity.shadows = LightShadows.None;

            // a faint shaft of light from the lens to the pool
            _beam = new GameObject("shaft").transform;
            _beam.SetParent(lamp.transform, false);
            _beam.gameObject.layer = Layers.Overlay;
            var mesh = new Mesh { name = "shaft" };
            const int N = 16;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                v.Add(new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f, 0f));
                v.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1f));
                if (i == 0) continue;
                int o = v.Count - 4;
                t.AddRange(new[] { o, o + 1, o + 2, o + 2, o + 1, o + 3 });   // the overlay shader draws both faces
            }
            // fade from the lens to the pool: bright at the lamp, nearly nothing on the ground
            var vc = new List<Color>();
            for (int i = 0; i < v.Count; i++) vc.Add(new Color(1f, 1f, 1f, v[i].z < 0.5f ? 1f : 0.1f));
            mesh.SetVertices(v); mesh.SetColors(vc); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            _beam.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = _beam.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Overlay("searchlight_shaft", new Color(0.85f, 0.9f, 1f, 0.045f), null, false, true);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // the pool's edge on the ground, so its reach reads at a glance
            _ring = new GameObject("poolring").transform;
            _ring.SetParent(transform, false);
            _ring.gameObject.layer = Layers.Overlay;
            var rm = new Mesh { name = "poolring" };
            var rv = new List<Vector3>(); var rt = new List<int>();
            float r = Light.Radius * 0.72f;
            for (int i = 0; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                rv.Add(d * r + Vector3.up * 0.07f);
                rv.Add(d * (r - 0.12f) + Vector3.up * 0.07f);
                if (i == 0) continue;
                int o = rv.Count - 4;
                rt.AddRange(new[] { o, o + 2, o + 1, o + 1, o + 2, o + 3 });
            }
            var rc = new List<Color>();
            for (int i = 0; i < rv.Count; i++) rc.Add(Color.white);
            rm.SetVertices(rv); rm.SetColors(rc); rm.SetTriangles(rt, 0); rm.RecalculateBounds();
            _ring.gameObject.AddComponent<MeshFilter>().sharedMesh = rm;
            var rr = _ring.gameObject.AddComponent<MeshRenderer>();
            rr.sharedMaterial = Mats.Overlay("searchlight_ring", new Color(0.85f, 0.92f, 1f, 0.35f), null, false, true);
            rr.shadowCastingMode = ShadowCastingMode.Off;
        }

        Vector3 Aim()
        {
            var o = Operator;
            switch (o.State)
            {
                case NpcState.Alerted:
                case NpcState.Searching:
                    return o.LastKnown;
                case NpcState.Suspicious:
                case NpcState.Investigating:
                case NpcState.Distracted:
                    return o.FocusPoint;
            }
            return _path.Count > 1 ? _path[Mathf.Clamp(Next, 0, _path.Count - 1)] : transform.position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Lit && Manned)
            {
                var o = Operator;
                bool tracking = o.State != NpcState.Relaxed && o.State != NpcState.Holding && o.State != NpcState.Relighting;
                var aim = Aim();
                var p = transform.position;
                if (tracking) p = Vector3.MoveTowards(p, aim, TrackSpeed * dt);
                else if (_dwell > 0f) _dwell -= dt;
                else
                {
                    p = Vector3.MoveTowards(p, aim, Speed * dt);
                    if ((p - aim).sqrMagnitude < 0.01f && _path.Count > 1)
                    {
                        _dwell = 1.2f;
                        if (Next + Dir >= _path.Count || Next + Dir < 0) Dir = -Dir;
                        Next += Dir;
                    }
                }
                transform.position = p;
                // the operator turns with the lamp
                var face = (p - o.transform.position); face.y = 0f;
                if (face.sqrMagnitude > 0.5f && !tracking) o.transform.rotation = Quaternion.RotateTowards(o.transform.rotation, Quaternion.LookRotation(face), 180f * dt);
            }
            Aim(Lit);
        }

        void Aim(bool lit)
        {
            if (!_lampLight) return;
            var to = transform.position - _lampLight.position;
            if (to.sqrMagnitude > 0.01f) _lampLight.rotation = Quaternion.LookRotation(to);
            _unity.enabled = lit;
            _beam.gameObject.SetActive(lit);
            _ring.gameObject.SetActive(lit);
            if (!lit) return;
            float dist = to.magnitude;
            float r = Light.Radius * 0.75f;
            _unity.range = dist + 4f;
            // light falls off with the square of the throw: keep the pool as bright as a lamp's from any tower
            _unity.intensity = 1.8f * Light.Intensity * dist * dist;
            _unity.spotAngle = Mathf.Clamp(Mathf.Atan2(r, dist) * Mathf.Rad2Deg * 2f, 4f, 80f);
            _unity.innerSpotAngle = _unity.spotAngle * 0.7f;
            _beam.rotation = _lampLight.rotation;
            _beam.localScale = new Vector3(r, r, dist);
        }

        public void SaveState(Save.EntityState s) { s.P = transform.position; s.I0 = Next; s.I1 = Dir; }
        public void LoadState(Save.EntityState s)
        {
            if (s.P != Vector3.zero) transform.position = s.P;
            Next = Mathf.Clamp(s.I0, 0, Mathf.Max(0, _path.Count - 1));
            Dir = s.I1 == 0 ? 1 : s.I1;
        }
    }
}
