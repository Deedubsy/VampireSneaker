#!/bin/bash
# usage: look.sh x y [dist=26] [pitch=60] [yaw=35] — lock the camera on a cell and capture
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
bash $S/ev.sh "var c=Vespertine.Core.Game.Cam; c.Follow=null; c.Locked=true; c.Pitch=${4:-60}; c.SnapTo(L.Grid.CellCenter($1,$2), ${5:-35}f); var f=typeof(Vespertine.View.TacticalCamera).GetField(\"_tDist\", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance); f.SetValue(c, ${3:-26}f); c.Distance=${3:-26}f; return \"ok\";" >/dev/null
sleep 1.5; bash $S/shot.sh >/dev/null 2>&1
