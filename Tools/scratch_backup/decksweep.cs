Time.timeScale = 0f;
var h = GameObject.Find("npc_hunter").GetComponent<Vespertine.AI.Npc>();
var lvl = Vespertine.Core.Game.Level; var LS = Vespertine.Core.Game.Lights;
MeshFilter mf = null; foreach (var m in Vespertine.Visual.ConeRenderer.Instance.GetComponentsInChildren<MeshFilter>()) if (m.name == "ConeDeck" && m.GetComponent<MeshRenderer>().enabled) mf = m;
if (mf == null) return "no deck";
var vs = mf.sharedMesh.vertices; var cs = mf.sharedMesh.colors; var tr = mf.sharedMesh.triangles;
int both = 0, onlyRule = 0, onlyDrawn = 0, neither = 0; string ex = "";
var o = h.transform.position;
for (float x = 50f; x <= 66f; x += 0.25f) for (float z = 26.1f; z <= 45.9f; z += 0.25f)
{
  var q = new Vector3(x, 0, z); var c = lvl.CellOf(q); if (!lvl.Grid.WalkTop(c.x, c.y) || lvl.Grid.Top(c.x, c.y) < 3f) continue; q.y = lvl.Grid.Top(c.x, c.y);
  var band = Vespertine.Stealth.DetectionMath.Classify(h.Vision, o, h.Forward, q, LS.LightAt(q));
  bool rule = band != Vespertine.Stealth.DetectionMath.Band.None && (h.LineOfSight(q + Vector3.up) || h.LineOfSight(q + Vector3.up * 1.6f));
  float a = 0f;
  for (int t = 0; t < tr.Length; t += 3) {
    Vector2 A = new Vector2(vs[tr[t]].x, vs[tr[t]].z), B = new Vector2(vs[tr[t+1]].x, vs[tr[t+1]].z), C = new Vector2(vs[tr[t+2]].x, vs[tr[t+2]].z), PP = new Vector2(x, z);
    float d = (B.y - C.y) * (A.x - C.x) + (C.x - B.x) * (A.y - C.y); if (Mathf.Abs(d) < 1e-6f) continue;
    float l1 = ((B.y - C.y) * (PP.x - C.x) + (C.x - B.x) * (PP.y - C.y)) / d, l2 = ((C.y - A.y) * (PP.x - C.x) + (A.x - C.x) * (PP.y - C.y)) / d, l3 = 1 - l1 - l2;
    if (l1 < 0 || l2 < 0 || l3 < 0) continue;
    a = Mathf.Max(a, l1 * cs[tr[t]].a + l2 * cs[tr[t+1]].a + l3 * cs[tr[t+2]].a);
  }
  bool drawn = a > 0.004f;
  // distance into the roof from its south face (z = 26)
  float depth = z - 26f;
  if (rule && drawn) both++; else if (rule) { onlyRule++; if (ex.Length < 600) ex += "R(" + x.ToString("F2") + "," + z.ToString("F2") + ") "; } else if (drawn) { onlyDrawn++; if (ex.Length < 600) ex += "D(" + x.ToString("F2") + "," + z.ToString("F2") + " a" + a.ToString("F3") + ") "; } else neither++;
}
return "hunter " + o.ToString("F1") + " fwd " + h.Forward.ToString("F2") + " | seen+drawn " + both + ", seen not drawn " + onlyRule + ", drawn not seen " + onlyDrawn + ", neither " + neither + "\n" + ex;
