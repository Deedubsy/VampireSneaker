#!/bin/bash
# sweep.sh <mission> <part>: restart the mission, run DevAbilitySweep part N, print non-PASS lines
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
$S/run.sh $1 | grep -v CS0618 | tail -1
./Tools/u.sh eval "Vespertine.Core.DevAbilitySweep.Run($2); return \"started\";" 2>&1 | grep -o '"result":"[^"]*"'
sleep 5
for i in $(seq 1 55); do r=$(timeout 15 ./Tools/u.sh eval 'return (Vespertine.Core.DevAbilitySweep.Running ? "run" : "done") + " ts=" + UnityEngine.Time.timeScale;' 2>&1 | grep -o '"result":"[^"]*"'); case "$r" in *done*) break;; *"ts=0"*) echo "STALL"; break;; esac; sleep 10; done
timeout 20 ./Tools/u.sh eval 'return Vespertine.Core.DevAbilitySweep.Report;' 2>&1 | grep -o '"result":"[^"]*' | sed 's/\\n/\n/g; s/"result":"//' | grep -v "^PASS"
