#!/bin/bash
# usage: toe.sh '<setup C#>' x y  — run setup (teleport/DebugMove), slow time, print toe state, capture at cell
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
bash $S/ev.sh "UnityEngine.Time.timeScale=1f; $1 UnityEngine.Time.timeScale=0.002f; return \"ok\";" >/dev/null
sleep 1.5
bash $S/ev.sh 'var d=Vespertine.Visual.IlseDisc.Instance; var s=""; foreach(var n in Vespertine.Core.Game.AI.Npcs) if(n.gameObject.activeSelf) s+=n.name+" in="+n.InSight+" det="+n.Detection.ToString("F2")+"; "; return "sl="+P.SightLight.ToString("F2")+" toe="+d.Toe+" by="+d.ToeBy+" "+s;'
bash $S/look.sh $2 $3 ${4:-10} 60 0
