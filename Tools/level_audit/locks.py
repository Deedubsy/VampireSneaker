"""For every locked door and gate: can the vampire reach both sides without it, at Awakening 1 and the mission's
typical Awakening? A lock whose far side is reachable anyway is not a gate."""
import sys, os, json
import networkx as nx
from analyse import *
from report import MISSIONS, OUT

def side_cells(m, c):
    x, y = c
    out = []
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        n = (x + dx, y + dy)
        if m.walktop(*n) and not m.blocksmove(*n) and m.floor(*n): out.append(n)
    return out

res = {}
for i in [f'm{n:02d}' for n in range(1, 15)]:
    m = Mission(os.path.join(MISSIONS, i + '.txt'))
    start = m.player.cell
    rows = []
    for A in (1, AWAKEN_GHOST[i], AWAKEN_TYPICAL[i]):
        G = vamp_graph(m, A, invited=True)
        reach = nx.descendants(G, start) | {start} if start in G else set()
        for e in m.ents:
            if not ((e.kind == 'door' and 'locked' in e.flags) or e.kind == 'gate'): continue
            sides = side_cells(m, e.cell)
            r = [s in reach for s in sides]
            rows.append((A, e.kind, e.id, e.cell, sum(r), len(sides)))
    res[i] = rows
    seen = {}
    for A, k, eid, c, nr, ns in rows:
        seen.setdefault((k, eid, c), []).append(f'A{A}:{nr}/{ns}')
    for key, v in seen.items():
        print(i, key, ' '.join(v))
