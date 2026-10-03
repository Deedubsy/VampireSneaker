S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
st='var s=""; foreach (var n in Vespertine.Core.Game.AI.Npcs) if (n.Prisoner) s+=n.Id+":"+n.State+(n.Escaped?"(out)":"")+" hp="+n.HP+" @"+L.Data.WorldToCell(n.transform.position)+"; "; s+=" P@"+L.Data.WorldToCell(P.Feet)+" | "; foreach (var o in M.Objectives) s+=o.Id+"="+o.State+" "; return s+" ended="+M.Ended;'
bash $S/ev.sh 'foreach (var id in new[]{"o_ward","eng","o_coal","o_gal","w_lane","h_gate1","h_gate2","h_garden","o_st1","o_st2","a_patrol"}) L.Get<Vespertine.AI.Npc>(id).gameObject.SetActive(false); L.Get<Vespertine.Level.Generator>("gen").Use(true); foreach (var id in new[]{"f2","f3"}) { var f=L.Get<Vespertine.AI.Npc>(id); P.Teleport(f.transform.position+Vector3.right*1.2f); f.Shackles.Use(true);} return "";'
for wp in "11 11" "20 12" "20 18" "20 23" "20 26" "9 27" "9 30" "12 35" "12 39" "6 40" "3 40"; do
  bash $S/ev.sh "P.Teleport(L.Data.CellToWorld(${wp/ /,},0)); return \"\";" >/dev/null
  for i in 1 2 3 4 5 6 7; do sleep 2; r=$(bash $S/ev.sh 'bool ok=true; foreach (var id in new[]{"f2","f3"}) { var f=L.Get<Vespertine.AI.Npc>(id); if (!(Vespertine.Core.Util.FlatDistance(f.transform.position,P.Feet)<4f||f.Escaped||!f.IsAlive)) ok=false; } return ok?"y":"n";'); [ "$r" = y ] && break; done
  echo "wp $wp: $(bash $S/ev.sh "$st")"
done
