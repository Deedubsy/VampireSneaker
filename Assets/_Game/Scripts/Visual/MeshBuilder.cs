using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vespertine.Visual
{
    /// <summary>Accumulates quads/boxes per material and emits merged meshes.</summary>
    public class MeshBuilder
    {
        class Part
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();
        }

        readonly Dictionary<Material, Part> _parts = new Dictionary<Material, Part>();
        public float UvScale = 0.5f; // 1 texture repeat per 2 m

        Part Get(Material m)
        {
            if (!_parts.TryGetValue(m, out var p)) { p = new Part(); _parts[m] = p; }
            return p;
        }

        /// <summary>Quad from 4 corners in perimeter order. Winding is chosen so the face is visible from the side
        /// <paramref name="normal"/> points to (defaults to the geometric normal of a→b→c).</summary>
        public void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Color? col = null, Vector3? normal = null)
        {
            var p = Get(m);
            int i = p.V.Count;
            var g = Vector3.Cross(b - a, c - a).normalized;   // normal for a→b→c ordering (unity: clockwise = front means normal = cross(b-a,c-a))
            var n = normal ?? g;
            p.V.Add(a); p.V.Add(b); p.V.Add(c); p.V.Add(d);
            p.N.Add(n); p.N.Add(n); p.N.Add(n); p.N.Add(n);
            p.UV.Add(ua); p.UV.Add(ub); p.UV.Add(uc); p.UV.Add(ud);
            var cc = col ?? Color.white;
            p.C.Add(cc); p.C.Add(cc); p.C.Add(cc); p.C.Add(cc);
            if (Vector3.Dot(g, n) >= 0)
            {
                p.T.Add(i); p.T.Add(i + 1); p.T.Add(i + 2);
                p.T.Add(i); p.T.Add(i + 2); p.T.Add(i + 3);
            }
            else
            {
                p.T.Add(i); p.T.Add(i + 2); p.T.Add(i + 1);
                p.T.Add(i); p.T.Add(i + 3); p.T.Add(i + 2);
            }
        }

        public void Tri(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color? col = null)
        {
            var p = Get(m);
            int i = p.V.Count;
            p.V.Add(a); p.V.Add(b); p.V.Add(c);
            for (int k = 0; k < 3; k++) { p.N.Add(normal); p.C.Add(col ?? Color.white); }
            p.UV.Add(new Vector2(a.x, a.z) * UvScale); p.UV.Add(new Vector2(b.x, b.z) * UvScale); p.UV.Add(new Vector2(c.x, c.z) * UvScale);
            var g = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(g, normal) >= 0) { p.T.Add(i); p.T.Add(i + 1); p.T.Add(i + 2); }
            else { p.T.Add(i); p.T.Add(i + 2); p.T.Add(i + 1); }
        }

        /// <summary>Horizontal top face at height y, facing up. World-space UVs.</summary>
        public void Top(Material m, float minX, float minZ, float maxX, float maxZ, float y)
        {
            float s = UvScale;
            Quad(m, new Vector3(minX, y, minZ), new Vector3(maxX, y, minZ), new Vector3(maxX, y, maxZ), new Vector3(minX, y, maxZ),
                new Vector2(minX * s, minZ * s), new Vector2(maxX * s, minZ * s), new Vector2(maxX * s, maxZ * s), new Vector2(minX * s, maxZ * s), null, Vector3.up);
        }

        /// <summary>Vertical face between p0 and p1 (horizontal edge) from y0 to y1; normal points to the right of p0→p1 when seen from above.</summary>
        public void Side(Material m, Vector3 p0, Vector3 p1, float y0, float y1)
        {
            float s = UvScale;
            float len = Vector3.Distance(p0, p1);
            float u0 = (p0.x + p0.z) * s, u1 = u0 + len * s;
            var a = new Vector3(p0.x, y0, p0.z); var b = new Vector3(p1.x, y0, p1.z);
            var c = new Vector3(p1.x, y1, p1.z); var d = new Vector3(p0.x, y1, p0.z);
            var n = Vector3.Cross(Vector3.up, p1 - p0).normalized;
            Quad(m, a, b, c, d, new Vector2(u0, y0 * s), new Vector2(u1, y0 * s), new Vector2(u1, y1 * s), new Vector2(u0, y1 * s), null, n);
        }

        /// <summary>Axis-aligned (optionally rotated about Y) box.</summary>
        public void Box(Material m, Vector3 center, Vector3 size, float yaw = 0f, bool bottom = false, Color? col = null)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + r * new Vector3(x * h.x, y * h.y, z * h.z);
            float s = UvScale;
            var sx = size.x * s; var sy = size.y * s; var sz = size.z * s;
            Quad(m, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), new Vector2(0, 0), new Vector2(sx, 0), new Vector2(sx, sz), new Vector2(0, sz), col, r * Vector3.up);
            Quad(m, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), new Vector2(0, 0), new Vector2(sx, 0), new Vector2(sx, sy), new Vector2(0, sy), col, r * Vector3.back);
            Quad(m, P(1, -1, 1), P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), new Vector2(0, 0), new Vector2(sx, 0), new Vector2(sx, sy), new Vector2(0, sy), col, r * Vector3.forward);
            Quad(m, P(-1, -1, 1), P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), new Vector2(0, 0), new Vector2(sz, 0), new Vector2(sz, sy), new Vector2(0, sy), col, r * Vector3.left);
            Quad(m, P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), new Vector2(0, 0), new Vector2(sz, 0), new Vector2(sz, sy), new Vector2(0, sy), col, r * Vector3.right);
            if (bottom)
                Quad(m, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, col, r * Vector3.down);
        }

        /// <summary>A square-section beam from a to b, at any angle (wreckage, struts, fallen spars).</summary>
        public void Strut(Material m, Vector3 a, Vector3 b, float width, float roll = 0f)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-4f) return;
            var r = Quaternion.LookRotation(d, Mathf.Abs(d.normalized.y) > 0.98f ? Vector3.forward : Vector3.up) * Quaternion.Euler(0, 0, roll);
            float hw = width * 0.5f, len = d.magnitude;
            Vector3 P(float x, float y, float z) => a + r * new Vector3(x * hw, y * hw, z * len);
            float s = UvScale, w = width * s, l = len * s;
            Quad(m, P(-1, 1, 0), P(1, 1, 0), P(1, 1, 1), P(-1, 1, 1), new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, l), new Vector2(0, l), null, r * Vector3.up);
            Quad(m, P(1, -1, 0), P(-1, -1, 0), P(-1, -1, 1), P(1, -1, 1), new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, l), new Vector2(0, l), null, r * Vector3.down);
            Quad(m, P(-1, -1, 0), P(-1, 1, 0), P(-1, 1, 1), P(-1, -1, 1), new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, l), new Vector2(0, l), null, r * Vector3.left);
            Quad(m, P(1, 1, 0), P(1, -1, 0), P(1, -1, 1), P(1, 1, 1), new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, l), new Vector2(0, l), null, r * Vector3.right);
            Quad(m, P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, null, r * Vector3.forward);
            Quad(m, P(1, -1, 0), P(1, 1, 0), P(-1, 1, 0), P(-1, -1, 0), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, null, r * Vector3.back);
        }

        /// <summary>Simple vertical cylinder approximation (n-gon prism).</summary>
        public void Cylinder(Material m, Vector3 baseCenter, float radius, float height, int sides = 8, bool cap = true)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                var p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                var p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                Side(m, p0, p1, baseCenter.y, baseCenter.y + height);
                if (cap)
                {
                    var top = baseCenter + Vector3.up * height;
                    Tri(m, top, p0 + Vector3.up * height, p1 + Vector3.up * height, Vector3.up);
                }
            }
        }

        public int VertexCount { get { int n = 0; foreach (var p in _parts.Values) n += p.V.Count; return n; } }

        /// <summary>Emit one child GameObject per material under parent.</summary>
        public List<GameObject> Build(Transform parent, string name, bool castShadows = true, int layer = 0)
        {
            var res = new List<GameObject>();
            foreach (var kv in _parts)
            {
                var p = kv.Value;
                if (p.V.Count == 0) continue;
                var mesh = new Mesh { name = name + "_" + kv.Key.name };
                if (p.V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(p.V); mesh.SetNormals(p.N); mesh.SetUVs(0, p.UV); mesh.SetColors(p.C); mesh.SetTriangles(p.T, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                var go = new GameObject(name + "_" + kv.Key.name) { layer = layer };
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key;
                mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                res.Add(go);
            }
            _parts.Clear();
            return res;
        }

        public Mesh BuildSingle(string name)
        {
            var mesh = new Mesh { name = name };
            var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var C = new List<Color>();
            var subs = new List<List<int>>();
            foreach (var p in _parts.Values)
            {
                int off = V.Count;
                V.AddRange(p.V); N.AddRange(p.N); UV.AddRange(p.UV); C.AddRange(p.C);
                var t = new List<int>(p.T.Count);
                foreach (var i in p.T) t.Add(i + off);
                subs.Add(t);
            }
            if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV); mesh.SetColors(C);
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        public Material[] Materials() { var l = new List<Material>(_parts.Keys); return l.ToArray(); }
    }
}
