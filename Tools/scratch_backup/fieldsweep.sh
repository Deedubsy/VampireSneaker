#!/bin/bash
T=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
for m in m01 m02 m03 m04 m05 m06 m07 m08 m09 m10 m11 m12 m13 m14; do
  echo "=== $m"
  $T/run.sh $m >/dev/null 2>&1
  $T/ev.sh 'return Vespertine.Core.DevExposureCheck.Run();' 2>&1 | grep -v '^warn' | tail -7
done
