#!/bin/bash
# ui.sh "Label" ["Label2" ...]: press buttons in order (0.6 s apart), then list visible buttons and screenshot to $S/shot.png
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
for l in "$@"; do
  ./Tools/u.sh eval "return Vespertine.Core.DevUi.Click(\"$l\");" 2>&1 | grep -o '"result":"[^"]*"'
  sleep 0.6
done
./Tools/u.sh eval 'return (Vespertine.Core.Game.UI.TopScreen ?? "-") + " :: " + Vespertine.Core.DevUi.Buttons();' 2>&1 | grep -o '"result":"[^"]*"' | cut -c1-900
$S/shot.sh >/dev/null
