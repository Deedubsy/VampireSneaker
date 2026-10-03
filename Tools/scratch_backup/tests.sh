#!/bin/bash
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
timeout 30 ./Tools/u.sh editor_stop >/dev/null 2>&1
C=$(bash $S/compile.sh 40); echo "$C" | grep -v "^failed: false$" | grep -v "^0 errors"
echo "$C" | grep -q "^0 errors" || { echo "compile failed: tests not run"; exit 1; }
# the performance-testing package saves TestResults.xml into the real persistentDataPath after every run; Editor/QuietTestResults strips it after each reload. Strip again (covers a pending reload) and show it is gone
./Tools/u.sh eval 'int n = Vespertine.EditorTools.QuietTestResults.Strip(); var h = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.TestTools.TestRunner.Api.CallbacksHolder")).First(t => t != null); var all = (System.Array)h.GetMethod("GetAll").Invoke(h.BaseType.GetProperty("instance").GetValue(null), null); return "perf saver stripped now " + n + ", remaining " + all.Cast<object>().Count(x => x.GetType().Name == "PerformanceTestRunSaver");' 2>&1 | grep -o '"result":"[^"]*"\|rror.*' 
timeout 300 ./Tools/u.sh run_tests --mode EditMode --timeout 240 --format json > $S/tests.json 2>&1
python3 - <<PY
import json
d=json.load(open("$S/tests.json")); r=d['data']['result']
print(r['Summary'])
for t in r['Results']:
  if t['Status']!='Passed': print('FAIL',t['FullName'],'\n ',(t['Message'] or '')[:1500])
PY
