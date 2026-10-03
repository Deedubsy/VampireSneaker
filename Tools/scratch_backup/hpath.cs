var f = new UnityEngine.AI.NavMeshQueryFilter{agentTypeID=0, areaMask=UnityEngine.AI.NavMesh.AllAreas};
var p = new UnityEngine.AI.NavMeshPath();
UnityEngine.AI.NavMesh.SamplePosition(L.Data.CellToWorld(40,11), out var h1, 3f, f);
UnityEngine.AI.NavMesh.SamplePosition(L.Data.CellToWorld(49,11), out var h2, 3f, f);
UnityEngine.AI.NavMesh.CalculatePath(h1.position, h2.position, f, p);
return "human toll path " + p.status;
