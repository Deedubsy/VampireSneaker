var ag = P.GetComponent<UnityEngine.AI.NavMeshAgent>();
var f = new UnityEngine.AI.NavMeshQueryFilter{agentTypeID=ag.agentTypeID, areaMask=UnityEngine.AI.NavMesh.AllAreas};
var sb = new System.Text.StringBuilder();
int[][] q = { new[]{25,44,49,44}, new[]{40,11,49,11}, new[]{3,44,73,31}, new[]{3,44,21,5}, new[]{49,25,64,10} };
foreach (var a in q) {
  var p = new UnityEngine.AI.NavMeshPath();
  UnityEngine.AI.NavMesh.SamplePosition(L.Data.CellToWorld(a[0],a[1]), out var h1, 3f, f);
  UnityEngine.AI.NavMesh.SamplePosition(L.Data.CellToWorld(a[2],a[3]), out var h2, 3f, f);
  UnityEngine.AI.NavMesh.CalculatePath(h1.position, h2.position, f, p);
  sb.Append($"{a[0]},{a[1]}->{a[2]},{a[3]} {p.status}: ");
  foreach (var c in p.corners) { var cc = L.Data.WorldToCellInt(c); sb.Append($"({cc.x},{cc.y},{c.y:0.#}) "); }
  sb.Append("\n");
}
return sb.ToString();
