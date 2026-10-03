#!/bin/bash
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
timeout 60 ./Tools/u.sh capture_game_view --format json "$@" > $S/cap.json 2>&1
python3 - <<PY
import json,base64,re
t=open("$S/cap.json").read()
m=re.search(r'"base64"\s*:\s*"([^"]+)"',t)
if not m: print(t[:400])
else:
    open("$S/shot.png","wb").write(base64.b64decode(m.group(1))); print("saved $S/shot.png")
PY
