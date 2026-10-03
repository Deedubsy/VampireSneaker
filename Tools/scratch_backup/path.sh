#!/bin/bash
# usage: path.sh "fx,fy" "x,y x,y ..."  — vampire path status between cells (player's area mask)
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
pts=""; for c in $2; do pts="$pts new int[]{${c}},"; done
bash $S/ev.sh "var sb=new System.Text.StringBuilder(); var f=new UnityEngine.AI.NavMeshQueryFilter{agentTypeID=Vespertine.Core.NavAreas.VampireAgent, areaMask=P.AreaMask()}; UnityEngine.AI.NavMeshHit o; UnityEngine.AI.NavMesh.SamplePosition(L.Grid.CellCenter($1), out o, 1.2f, f); foreach (var c in new int[][]{ $pts }) { UnityEngine.AI.NavMeshHit h; bool s=UnityEngine.AI.NavMesh.SamplePosition(L.Grid.CellCenter(c[0],c[1]), out h, 1.2f, f); var path=new UnityEngine.AI.NavMeshPath(); if (s) UnityEngine.AI.NavMesh.CalculatePath(o.position, h.position, f, path); sb.Append(c[0]+\",\"+c[1]+\"=\"+(s? path.status.ToString():\"nomesh\")+\" \"); } return sb.ToString();"
