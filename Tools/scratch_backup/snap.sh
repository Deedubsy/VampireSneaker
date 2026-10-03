#!/bin/bash
# usage: snap.sh x z [dist=20] [pitch=60] [yaw=0] [out=shot.png] — frame a world point and capture
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
bash $S/ev.sh "var c=Vespertine.Core.Game.Cam; c.Follow=null; c.Locked=true; c.Pitch=${4:-60}; c.SnapTo(new UnityEngine.Vector3($1f,0,$2f), ${5:-0}f); typeof(Vespertine.View.TacticalCamera).GetField(\"_tDist\", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(c, ${3:-20}f); c.Distance=${3:-20}f; return \"ok\";" >/dev/null
sleep 1.5; bash $S/shot.sh >/dev/null 2>&1; [ -n "$6" ] && cp $S/shot.png $S/$6
