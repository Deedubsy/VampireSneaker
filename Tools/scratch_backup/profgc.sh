#!/bin/bash
# prof.sh <mission> [hunted] [filter-regex]: plays the mission, captures ~90 profiler frames, prints top self-time markers (ms/frame)
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
bash $S/run.sh $1 >/dev/null 2>&1; sleep 6
H='Vespertine.Player.Vampire.GodMode = true; Vespertine.Core.Game.AI.RaiseLockdown("prof"); var at = Vespertine.Core.Game.Player.transform.position; foreach (var n in Vespertine.Core.Game.AI.Npcs.ToArray()) if (n && n.IsAlive && !n.IsThrall && !n.Friendly) n.EnterSearching(at + new UnityEngine.Vector3(UnityEngine.Random.Range(-5f,5f),0,UnityEngine.Random.Range(-5f,5f)), true);'
[ "$2" = hunted ] || H=''
./Tools/u.sh eval "$H UnityEditorInternal.ProfilerDriver.ClearAllFrames(); UnityEditorInternal.ProfilerDriver.enabled = true; UnityEngine.Profiling.Profiler.enabled = true; return \"on\";" 2>&1 | grep -o 'rror.\{0,600\}'
sleep 4
./Tools/u.sh eval "$(cat $S/profgc.cs)" 2>&1 | grep -o '"result":"[^"]*"\|rror.\{0,900\}' | sed 's/ ; /\n/g' | grep -E "${3:-.}"
