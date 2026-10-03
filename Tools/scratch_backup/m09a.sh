S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
st='var s=""; foreach (var n in Vespertine.Core.Game.AI.Npcs) if (n.Prisoner) s+=n.Id+":"+n.State+(n.EscortWait?"(w)":"")+" hp="+n.HP+" @"+L.Data.WorldToCell(n.transform.position)+"; "; s+=" P@"+L.Data.WorldToCell(P.Feet)+" hp="+P.HP; return s;'
# keep the ward orderly out of it
bash $S/ev.sh 'L.Get<Vespertine.AI.Npc>("o_ward").gameObject.SetActive(false); var f=L.Get<Vespertine.AI.Npc>("f1"); P.Teleport(f.transform.position+Vector3.right*1.2f); return "tp";'
sleep 1
bash $S/ev.sh 'var f=L.Get<Vespertine.AI.Npc>("f1"); f.Shackles.Use(true); return f.State.ToString()+" verb="+f.Shackles.Verb;'
# walk Ilse down the passage to the gallery: f1 should balk at the sunstone corridor
bash $S/ev.sh 'P.Teleport(L.Data.CellToWorld(20,20,0)); return "tp2";'
sleep 6
bash $S/ev.sh "$st"
# throw the breaker
bash $S/ev.sh 'var g=L.Get<Vespertine.Level.Generator>("gen"); g.Use(true); return "gen state="+g.State+" sun_w1 on="+L.Get<Vespertine.Stealth.GameLight>("sun_w1").On;'
sleep 8
bash $S/ev.sh "$st"
