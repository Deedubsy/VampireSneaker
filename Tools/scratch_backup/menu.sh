#!/bin/bash
# menu.sh: compile, enter play mode, wait for the main menu
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
$S/compile.sh | tail -1
./Tools/u.sh eval 'UnityEditor.AssetDatabase.Refresh(); return "ok";' >/dev/null 2>&1
./Tools/u.sh editor_play >/dev/null 2>&1
for i in $(seq 1 60); do ./Tools/u.sh eval 'return Vespertine.Core.Game.Root!=null && Vespertine.Core.Game.Root.State==Vespertine.Core.RootState.MainMenu ? "up" : "no";' 2>&1 | grep -q '"up"' && break; sleep 2; done
sleep 1
