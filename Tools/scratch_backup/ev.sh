#!/bin/bash
# usage: ev.sh '<C# body>'  — vars: P (player), L (level), M (mission); prints result or diagnostics
cd /mnt/e/ShadowTactics/StealthVampire
code="var P=Vespertine.Core.Game.Player; var L=Vespertine.Core.Game.Level; var M=Vespertine.Core.Game.Mission; $1"
./Tools/u.sh eval "$code" --format json 2>&1 | python3 -c '
import sys,json
t=sys.stdin.read()
try:
  d=json.loads(t); r=(d.get("data") or {}).get("result") or {}
  if r.get("success"): print(r.get("result"))
  else: print("ERR", json.dumps(r.get("diagnostics") or d.get("errors") or d)[:2000])
except Exception: print(t[:2000])'
