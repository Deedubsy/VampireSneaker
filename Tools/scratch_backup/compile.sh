#!/bin/bash
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
# never recompile in play mode: the domain reload nulls non-serialised state (Npc.Arch) and floods the console
timeout 30 ./Tools/u.sh editor_stop >/dev/null 2>&1
timeout 120 ./Tools/u.sh recompile >/dev/null 2>&1
sleep 3
for i in $(seq 1 60); do
  timeout 30 ./Tools/u.sh recompile_status --format json > $S/rs.json 2>&1
  grep -q '"compiling"' $S/rs.json || break
  sleep 3
done
python3 - <<PY
import json,re
t=open("$S/rs.json").read()
m=re.search(r'"failed":\s*(true|false)',t)
print("failed:", m.group(1) if m else "?")
errs=re.findall(r'((?:Scripts|Tests|Editor)[^"]*?error CS\d+: [^"]*)',t)
errs=sorted(set(re.sub(r'\\\\+','/',e) for e in errs))
print(len(errs),"errors")
for e in errs[:int("${1:-80}")]: print(e[:260])
PY
