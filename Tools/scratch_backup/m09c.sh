S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
st='var s=""; foreach (var n in Vespertine.Core.Game.AI.Npcs) if (n.Prisoner) s+=n.Id+":"+n.State+(n.EscortWait?"(w)":"")+(n.Escaped?"(out)":"")+" hp="+n.HP+" @"+L.Data.WorldToCell(n.transform.position)+"; "; s+=" P@"+L.Data.WorldToCell(P.Feet)+" | "; foreach (var o in M.Objectives) s+=o.Id+"="+o.State+":"+o.Progress+" "; return s;'
bash $S/ev.sh 'var f=L.Get<Vespertine.AI.Npc>("f1"); P.Teleport(f.transform.position+Vector3.right*1.2f); f.Shackles.Use(true); return "";' >/dev/null
sleep 1
bash $S/ev.sh 'P.Teleport(L.Data.CellToWorld(10,12,0)); return "";' >/dev/null
sleep 12
echo "-- balk: $(bash $S/ev.sh "$st")"
bash $S/con.sh all 40 | grep -i "light\|burn\|bark" | tail -5
# turn f3 loose (amok); put o_ward back so it has prey
bash $S/ev.sh 'L.Get<Vespertine.AI.Npc>("f1").TurnLoose(); return "";' >/dev/null
sleep 10
echo "-- amok: $(bash $S/ev.sh "$st") ward=$(bash $S/ev.sh 'var o=L.Get<Vespertine.AI.Npc>("o_ward"); return o.State+" alive="+o.IsAlive+" by="+o.KilledBy;')"
