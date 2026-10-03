#!/bin/bash
# usage: cmstate.sh id1,id2,... — NPC state + cell (and the player's cell)
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
$S/ev.sh "var sb=new System.Text.StringBuilder(\"pl \"+L.Data.WorldToCellInt(P.transform.position)+\"; \"); foreach (var id in \"$1\".Split(',')) { var n=L.Get<Vespertine.AI.Npc>(id); if (n==null) { sb.Append(id+\":null; \"); continue; } sb.Append(id+\":\"+n.State+\" \"+L.Data.WorldToCellInt(n.transform.position)+\" y\"+n.transform.position.y.ToString(\"0\")+(n.Agent!=null&&n.Agent.isOnNavMesh?\"\":\" OFFNAV\")+\"; \"); } return sb.ToString();"
