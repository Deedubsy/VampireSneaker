#!/bin/bash
# camprun.sh [prefer] [optionals=true]: play a throwaway campaign M01->ending (DevCampaignRun) and print the report
cd /mnt/e/ShadowTactics/StealthVampire
timeout 30 ./Tools/u.sh editor_play >/dev/null 2>&1
for i in $(seq 1 40); do r=$(timeout 20 ./Tools/u.sh eval 'return Vespertine.Core.Game.Root!=null && Vespertine.Core.Game.Root.State!=Vespertine.Core.RootState.Boot ? "up" : "no";' 2>&1 | grep -o '"result":"[^"]*"'); [ "$r" = '"result":"up"' ] && break; sleep 2; done
timeout 30 ./Tools/u.sh eval "Vespertine.Core.DevCampaignRun.Run(\"$1\", ${2:-true}); return \"started\";" 2>&1 | grep -o '"result":"[^"]*"'
for i in $(seq 1 300); do
  sleep 5
  r=$(timeout 20 ./Tools/u.sh eval 'return Vespertine.Core.DevCampaignRun.Running ? "run" : "done";' 2>&1 | grep -o '"result":"[^"]*"')
  [ "$r" = '"result":"done"' ] && break
done
timeout 30 ./Tools/u.sh eval 'return Vespertine.Core.DevCampaignRun.Report;' --format json 2>&1 | python3 -c 'import sys,json; t=sys.stdin.read(); print(json.loads(t)["data"]["result"]["result"])'
