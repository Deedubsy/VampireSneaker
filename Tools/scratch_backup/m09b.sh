S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
st='var s=""; foreach (var n in Vespertine.Core.Game.AI.Npcs) if (n.Prisoner) s+=n.Id+":"+n.State+(n.EscortWait?"(w)":"")+(n.Escaped?"(out)":"")+" hp="+n.HP+" @"+L.Data.WorldToCell(n.transform.position)+"; "; s+=" P@"+L.Data.WorldToCell(P.Feet)+" | "; foreach (var o in M.Objectives) s+=o.Id+"="+o.State+" "; return s;'
bash $S/ev.sh 'foreach (var id in new[]{"o_ward","eng","o_coal","o_gal","w_lane","h_gate1","h_gate2","h_garden","o_st1","o_st2"}) L.Get<Vespertine.AI.Npc>(id).gameObject.SetActive(false); L.Get<Vespertine.Level.Generator>("gen").Use(true); var f=L.Get<Vespertine.AI.Npc>("f2"); P.Teleport(f.transform.position+Vector3.right*1.2f); f.Shackles.Use(true); return f.State.ToString();'
for wp in "11 11" "20 12" "20 18" "20 23" "20 26" "9 27" "9 30" "12 35" "12 39" "3 40"; do
  bash $S/ev.sh "P.Teleport(L.Data.CellToWorld(${wp/ /,},0)); return \"\";" >/dev/null
  for i in 1 2 3 4 5 6; do sleep 2; r=$(bash $S/ev.sh 'var f=L.Get<Vespertine.AI.Npc>("f2"); return (Vespertine.Core.Util.FlatDistance(f.transform.position,P.Feet)<3f||f.Escaped||!f.IsAlive)?"y":"n";'); [ "$r" = y ] && break; done
  echo "wp $wp: $(bash $S/ev.sh "$st")"
done
