S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
st='var s=""; foreach (var id in new[]{"f1","f2","clem","o_ward","o_st1","o_st2","o_gal","eng"}) { var n=L.Get<Vespertine.AI.Npc>(id); s+=id+":"+n.State+(n.IsAlive?"":"/"+n.KilledBy)+" @"+L.Data.WorldToCell(n.transform.position)+"; "; } return s;'
bash $S/ev.sh 'L.Get<Vespertine.AI.Npc>("eng").gameObject.SetActive(false); L.Get<Vespertine.Level.Generator>("gen").Use(true); var f=L.Get<Vespertine.AI.Npc>("f1"); P.Teleport(f.transform.position+Vector3.right*1.2f); f.Shackles.Use(true); f.TurnLoose(); P.Teleport(L.Data.CellToWorld(10,6,0)); return "";' >/dev/null
for i in 1 2 3 4 5 6; do sleep 5; echo "t$i dark: $(bash $S/ev.sh "$st")"; done
