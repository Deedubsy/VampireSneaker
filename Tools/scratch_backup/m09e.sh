S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
st='var s=""; foreach (var n in Vespertine.Core.Game.AI.Npcs) if (n.Prisoner) s+=n.Id+":"+n.State+(n.EscortWait?"(w)":"")+(n.Escaped?"(out)":"")+" @"+L.Data.WorldToCell(n.transform.position)+"; "; s+=" P@"+L.Data.WorldToCell(P.Feet)+" gen="+L.Get<Vespertine.Level.Generator>("gen").State+" | "; foreach (var o in M.Objectives) s+=o.Id+"="+o.State+":"+o.Progress.ToString("0.0")+" "; return s;'
bash $S/ev.sh 'foreach (var id in new[]{"o_quiet","h_east","a_disp","o_disp","o_gal","a_patrol","h_garden","o_coal","w_lane","h_gate1","h_gate2"}) L.Get<Vespertine.AI.Npc>(id).gameObject.SetActive(false); var f=L.Get<Vespertine.AI.Npc>("clem"); P.Teleport(f.transform.position+Vector3.left*1.2f); f.Shackles.Use(true); return f.State.ToString();'
go() { for wp in "$@"; do
  bash $S/ev.sh "P.Teleport(L.Data.CellToWorld(${wp/ /,},0)); return \"\";" >/dev/null
  for i in 1 2 3 4 5 6 7; do sleep 2; r=$(bash $S/ev.sh 'var f=L.Get<Vespertine.AI.Npc>("clem"); return (Vespertine.Core.Util.FlatDistance(f.transform.position,P.Feet)<3f||f.Escaped||!f.IsAlive)?"y":"n";'); [ "$r" = y ] && break; done
  echo "wp $wp: $(bash $S/ev.sh "$st")"
done; }
go "57 11" "46 11" "46 17" "45 23" "45 26"
bash $S/ev.sh 'M.QuickSave(); var f=L.Get<Vespertine.AI.Npc>("clem"); f.Shackles.Use(true); return "saved; wait="+f.EscortWait;'
sleep 2
bash $S/ev.sh 'M.QuickLoad(); return "loaded";'
sleep 6
echo "after load: $(bash $S/ev.sh "$st")"
go "48 27" "48 30" "40 33" "22 33" "17 33" "12 35" "12 39" "3 40"
