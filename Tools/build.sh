#!/bin/bash
# Windows player build through the live Editor (Pipeline `build` command). Usage: Tools/build.sh [outdir=Builds/Win64]
# Ships only Main.unity; all content loads from Resources. Waits for the build and prints the summary.
cd "$(dirname "$0")/.."
OUT=${1:-Builds/Win64}
./Tools/u.sh eval 'UnityEditor.EditorBuildSettings.scenes = new[]{ new UnityEditor.EditorBuildSettingsScene("Assets/_Game/Scenes/Main.unity", true) }; UnityEditor.PlayerSettings.productName="Vespertine"; UnityEditor.PlayerSettings.companyName="Vespertine"; return "ok";' >/dev/null || exit 1
./Tools/u.sh build --target StandaloneWindows64 --outputPath "$OUT/Vespertine.exe" --scenes '["Assets/_Game/Scenes/Main.unity"]' --confirm true >/dev/null || exit 1
for i in $(seq 1 360); do
  r=$(timeout 30 ./Tools/u.sh build_status --format json 2>&1)
  echo "$r" | grep -q '"status":\s*"completed"' && break
  sleep 10
done
echo "$r" | python3 -c "
import sys,re
t=sys.stdin.read()
for k in ['result','totalErrors','totalWarnings','totalSize','totalTime','outputPath']:
    m=re.search(r'\"%s\":\s*(\"[^\"]*\"|[0-9.]+)'%k,t); print(k, m.group(1) if m else '?')
"
echo "$r" | grep -q '"result":\s*"Succeeded"'
