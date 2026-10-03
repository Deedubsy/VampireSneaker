#!/bin/bash
# usage: Tools/cone_check.sh [points per guard=1500] [seed=1]
# Play mode, mission loaded. The cone truth sweep (SR.14): every seeing guard's full cone is built and read back, and at
# random standing points the drawn answer (near fill, lit far fill, nothing) is compared with SeenBand plus line of sight.
# Prints RESULT PASS or RESULT FAIL with the disagreements. Big maps (M11) need ~700 points to stay inside the eval timeout.
cd "$(dirname "$0")/.."
./Tools/u.sh eval "return Vespertine.Core.DevConeCheck.Run(${1:-1500}, ${2:-1});" 2>&1 | tail -3
