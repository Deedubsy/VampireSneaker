#!/bin/bash
# hub.sh [tab] [setup C#]: compile, play, open sandbox profile 1 in the refuge (optionally run setup on c = Game.Campaign), show tab
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
$S/menu.sh
./Tools/u.sh eval 'Vespertine.Core.Game.Root.ContinueProfile(0, true); return "ok";' 2>&1 | grep -o 'rror.*'
sleep 2
./Tools/u.sh eval "var c = Vespertine.Core.Game.Campaign; $2; Vespertine.Core.Game.UI.ShowHub(\"${1:-Next Night}\"); return \"ok\";" 2>&1 | grep -o 'rror.*'
sleep 1
$S/ui.sh
