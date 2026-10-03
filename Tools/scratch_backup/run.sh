#!/bin/bash
# usage: run.sh [mission=m01]  — stop, recompile, play, start mission, dismiss intro
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
./Tools/u.sh editor_stop >/dev/null 2>&1
bash $S/compile.sh 20 || exit 1
grep -q '"failed":\s*true' $S/rs.json && exit 1
./Tools/u.sh editor_play >/dev/null 2>&1
for i in $(seq 1 30); do sleep 2; ./Tools/u.sh eval 'return Vespertine.Core.Game.Root != null ? "y" : "n";' 2>&1 | grep -q '"result":"y"' && break; done
sleep 2
M=${1:-m01}
./Tools/u.sh eval "var G=Vespertine.Core.Game.Root; if (Vespertine.Core.Game.Campaign==null) G.NewGame(0, Vespertine.Data.Difficulty.Hunter, false, false); G.StartMission(\"$M\"); return \"ok\";" 2>&1 | tail -1 | cut -c1-200
sleep 8
./Tools/u.sh eval 'Vespertine.Core.Game.UI.PopScreen("intro"); return "ok";' >/dev/null 2>&1
sleep 2
$S/con.sh warn 30
