#!/bin/bash
# usage: knee.sh light_name [before=false] — rendered pool edge vs the exposure contour
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
bash $S/ev.sh "$(sed -e "s/__LIGHT__/$1/" -e "s/__BEFORE__/${2:-false}/" $S/knee.cs | tr '\n' ' ')"
