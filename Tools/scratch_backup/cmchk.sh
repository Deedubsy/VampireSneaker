#!/bin/bash
# usage: cmchk.sh id1,id2,...  — per group NPC: active, on navmesh, cell, agent path status
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
$S/ev.sh "var sb=new System.Text.StringBuilder(); foreach (var id in \"$1\".Split(',')) { var n=Vespertine.Core.Game.Level.Get<Vespertine.AI.Npc>(id); if (n==null) { sb.Append(id+\":null; \"); continue; } var p=n.transform.position; sb.Append(id+\":\"+(n.gameObject.activeInHierarchy?\"on\":\"off\")+\" nav=\"+(n.Agent!=null&&n.Agent.isOnNavMesh)+\" (\"+p.x.ToString(\"0.0\")+\",\"+p.y.ToString(\"0.0\")+\",\"+p.z.ToString(\"0.0\")+\") \"+(n.Agent!=null&&n.Agent.isOnNavMesh?n.Agent.pathStatus.ToString():\"\")+\"; \"); } return sb.ToString();"
