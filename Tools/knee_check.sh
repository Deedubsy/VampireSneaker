#!/bin/bash
# usage: Tools/knee_check.sh light_object_name [before=false]
# Play mode, mission loaded. Renders the light's pool top-down and reports where the drawn edge is against the exposure
# contour (D145): rHalf is where ground brightness falls halfway from just inside the contour to outside; err is rHalf
# minus (contour + half the feather). before=true measures the D133 fit (no cookie) for comparison.
cd "$(dirname "$0")/.."
code="$(sed -e "s/__LIGHT__/$1/" -e "s/__BEFORE__/${2:-false}/" Tools/knee_check.cs.txt | tr '\n' ' ')"
./Tools/u.sh eval "$code" 2>&1 | tail -3
