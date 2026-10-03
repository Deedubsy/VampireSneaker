#!/bin/bash
# squad + objective status
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
bash $S/ev.sh 'var s=""; foreach (var q in Vespertine.AI.Squad.All) { s+=q.Id+":"+q.Members.Count+"/"+q.Standing+" nerve="+q.Nerve.ToString("0")+"/"+q.StartNerve+" lead="+(q.Leader?q.Leader.Id:"-")+(q.Ringing?" RING":"")+(q.Broken?" BROKEN":"")+" @"+(q.Leader?L.CellOf(q.Leader.transform.position).ToString():"")+"\n"; } foreach (var o in M.Objectives) s+=o.Id+" "+o.State+" "+o.Progress.ToString("0.00")+(o.Visible?"":" (hidden)")+"\n"; return s+"dawn="+M.DawnLeft.ToString("0")+" t="+M.MissionTime.ToString("0");'
