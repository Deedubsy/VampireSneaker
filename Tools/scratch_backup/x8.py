import sys
H="#! ---- the Dossier's answers: dormant until the Vigil answers one of her habits\n"
B={
'm07':"""group cm_rooftop
route r_wwMill pingpong | 22 12 wait=4 look=S | 28 12 wait=4 look=E |
npc ww1 hunter 22 12 route=r_wwMill nolantern name=Roof_sentry
route r_wwHut pingpong | 54 14 wait=4 look=W | 54 22 wait=4 look=S |
npc ww2 hunter 54 14 route=r_wwHut nolantern name=Roof_sentry
endgroup
group cm_paired
npc bp1 hunter 25 22 route=r_isl name=Vigil_hunter
npc bp2 hunter 50 10 route=r_ecp name=Vigil_hunter
npc bp3 hunter 66 29 route=r_dyke name=Vigil_hunter
endgroup
group cm_inquest
route r_inq loop | 26 24 wait=4 look=S | 38 24 wait=4 look=E | 40 32 wait=4 look=S | 24 32 wait=4 look=W |
npc inq1 inquisitor 26 24 route=r_inq name=Vigil_inquisitor
endgroup
group cm_censer
npc cen1 alchemist 49 12 face=W post name=Vigil_alchemist
npc cen2 alchemist 58 32 face=E post name=Vigil_alchemist
endgroup
""",
'm08':"""group cm_rooftop
route r_wwHall pingpong | 22 7 wait=4 look=N | 34 7 wait=4 look=E |
npc ww1 hunter 22 7 route=r_wwHall nolantern name=Wall_sentry
route r_wwCamp pingpong | 49 13 wait=4 look=W | 58 13 wait=4 look=N |
npc ww2 hunter 49 13 route=r_wwCamp nolantern name=Wall_sentry
endgroup
group cm_paired
npc bp1 hunter 24 2 route=r_dyke name=Vigil_hunter
npc bp2 hunter 24 16 route=r_yard name=Vigil_hunter
npc bp3 hunter 48 30 route=r_hum name=Vigil_hunter
endgroup
group cm_inquest
route r_inq loop | 8 13 wait=4 look=W | 8 33 wait=3 | 15 40 wait=4 look=E | 2 40 wait=4 look=S | 2 33 wait=3 look=N |
npc inq1 inquisitor 8 13 route=r_inq name=Vigil_inquisitor
endgroup
group cm_censer
npc cen1 alchemist 15 27 face=E post name=Vigil_alchemist
npc cen2 alchemist 46 34 face=W post name=Vigil_alchemist
endgroup
""",
'm09':"""group cm_rooftop
route r_wwS1 pingpong | 14 35 wait=4 look=S | 38 35 wait=4 look=N |
npc ww1 hunter 14 35 route=r_wwS1 nolantern name=Wall_sentry
route r_wwS2 pingpong | 43 35 wait=4 look=N | 61 35 wait=4 look=S |
npc ww2 hunter 61 35 route=r_wwS2 nolantern name=Wall_sentry
endgroup
group cm_paired
npc bp1 watchman 20 39 route=r_lane
npc bp2 hunter 22 32 route=r_garden name=Vigil_hunter
endgroup
group cm_inquest
route r_inq pingpong | 2 24 wait=4 look=W | 31 24 wait=3 look=N | 60 24 wait=4 look=E |
npc inq1 inquisitor 31 24 route=r_inq name=Vigil_inquisitor
endgroup
group cm_censer
npc cen1 alchemist 19 16 face=N post name=Vigil_alchemist
npc cen2 alchemist 46 15 face=W post name=Vigil_alchemist
endgroup
""",
'm10':"""group cm_rooftop
route r_wwN pingpong | 2 5 wait=4 look=N | 28 5 wait=4 look=S |
npc ww1 hunter 2 5 route=r_wwN nolantern name=Wall_sentry
route r_wwRoof pingpong | 45 8 wait=4 look=S | 54 8 wait=4 look=E |
npc ww2 hunter 54 8 route=r_wwRoof nolantern name=Roof_sentry
endgroup
group cm_paired
npc bp1 hunter 11 15 route=r_manu name=Vigil_hunter
npc bp2 hunter 47 14 route=r_holder name=Vigil_hunter
npc bp3 watchman 36 12 route=r_yard lantern
endgroup
group cm_inquest
route r_inq loop | 44 12 wait=4 look=N | 62 12 wait=3 | 62 39 wait=4 look=S | 44 34 wait=4 look=W |
npc inq1 inquisitor 44 12 route=r_inq name=Vigil_inquisitor
endgroup
group cm_censer
npc cen1 alchemist 43 26 face=W post name=Vigil_alchemist
npc cen2 alchemist 19 28 face=E post name=Vigil_alchemist
endgroup
""",
'm11':"""group cm_rooftop
route r_wwE pingpong | 47 38 wait=4 look=N | 62 38 wait=4 look=S |
npc ww1 hunter 62 38 route=r_wwE nolantern name=Roof_sentry
route r_wwW pingpong | 1 4 wait=4 look=E | 1 44 wait=4 look=E |
npc ww2 hunter 1 4 route=r_wwW nolantern name=Roof_sentry
endgroup
group cm_paired
npc bp1 hunter 50 16 route=r_yard name=Vigil_hunter
npc bp2 watchman 43 22 route=r_street lantern
npc bp3 watchman 7 28 route=r_corW lantern
endgroup
group cm_inquest
route r_inq2 loop | 14 34 wait=5 look=S | 34 34 wait=5 look=N | 34 39 wait=4 look=E | 14 39 wait=4 look=W |
npc inq2 inquisitor 34 34 route=r_inq2 name=Vigil_inquisitor
endgroup
group cm_censer
npc cen1 alchemist 47 22 face=W post name=Vigil_alchemist
npc cen2 alchemist 5 36 face=E post name=Vigil_alchemist
endgroup
""",
}
for m,b in B.items():
    p=m+'.txt'; s=open(p).read()
    assert 'group cm_' not in s, m
    k=s.index('#! ---- zones')
    s=s[:k]+H+b+'\n'+s[k:]
    open(p,'w').write(s); print(m,'ok')
