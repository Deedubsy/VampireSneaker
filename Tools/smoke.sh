#!/bin/bash
# Player smoke test: runs the built exe with -smoke (loads all 14 missions in a throwaway campaign, then quits).
# Usage: [OUT=dir] [W=1920 H=1080] Tools/smoke.sh [-uncapped]   (defaults: Builds/Win64, 1280x720)
# -uncapped turns vsync and the frame cap off, for real frame times (P3). Prints the report; exit code 0 = PASS.
cd "$(dirname "$0")/.."
OUT=${OUT:-Builds/Win64}
REP="$OUT/smoke.txt"; LOG="$OUT/smoke_player.log"
rm -f "$REP" "$LOG"
timeout 600 "$OUT/Vespertine.exe" -smoke -smokeout "$(wslpath -w "$REP")" -logFile "$(wslpath -w "$LOG")" \
  -screen-fullscreen 0 -screen-width ${W:-1280} -screen-height ${H:-720} "$@" >/dev/null 2>&1
code=$?
cat "$REP" 2>/dev/null || { echo "no report (exit $code); last log lines:"; tail -30 "$LOG"; }
exit $code
