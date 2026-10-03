#!/bin/bash
# usage: flagrun.sh <mission> <timer> <seconds> <flag>...  — play the mission with campaign flags set; report if the timer's rule fired
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd /mnt/e/ShadowTactics/StealthVampire
M=$1; T=$2; W=$3; shift 3
FL=""; for f in "$@"; do FL="$FL c.SetFlag(\"$f\");"; done
./Tools/u.sh editor_stop >/dev/null 2>&1; sleep 2
./Tools/u.sh editor_play >/dev/null 2>&1
for i in $(seq 1 30); do sleep 2; ./Tools/u.sh eval 'return Vespertine.Core.Game.Root != null ? "y" : "n";' 2>&1 | grep -q '"result":"y"' && break; done
sleep 2
./Tools/u.sh eval "var G=Vespertine.Core.Game.Root; G.NewGame(0, Vespertine.Data.Difficulty.Hunter, false, false); var c=Vespertine.Core.Game.Campaign; $FL G.StartMission(\"$M\"); return \"ok\";" 2>&1 | tail -1 | cut -c1-200
sleep 8
./Tools/u.sh eval 'Vespertine.Core.Game.UI.PopScreen("intro"); Vespertine.Player.Vampire.GodMode = true; return "ok";' >/dev/null 2>&1
sleep $W
./Tools/u.sh eval "var mc=Vespertine.Core.Game.Mission; var t=mc.GetType(); var rules=(System.Collections.Generic.List<Vespertine.Level.ScriptRule>)t.GetField(\"_rules\",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(mc); var fired=(System.Collections.Generic.HashSet<int>)t.GetField(\"_fired\",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(mc); var o=\"\"; for(int i=0;i<rules.Count;i++) if(rules[i].Event==\"timer\"&&rules[i].Arg==\"$T\") o+=\"$M $T fired=\"+fired.Contains(i)+\" \"; return o==\"\"?\"no rule\":o;" 2>&1 | grep -o '"result":"[^"]*"'
