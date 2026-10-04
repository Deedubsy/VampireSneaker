"""Targeted checks: M05 carry route vs gas mains; M09 escort route vs sunstones; perimeter highway use."""
import os, json, math
import networkx as nx
from analyse import *
from report import MISSIONS, OUT, analyse_mission

def carry_route(m, field, static, patrol, src, dst_cells):
    G = vamp_graph(m, 1, invited=True)
    G = G.copy(); G.remove_nodes_from([n for n in list(G) if m.raised(*n)])
    def risk(c):
        r = 0.0
        if c in static: r += 40
        r += 30 * patrol.get(c, 0)
        if field.get(c, 0) >= EXPOSED: r += 4       # carrying in light: seen at range (x1.2)
        return r
    best = None
    for d in dst_cells:
        try:
            p = nx.shortest_path(G, src, d, weight=lambda u, v, a: 1 + risk(v))
        except Exception:
            continue
        cost = sum(1 + risk(c) for c in p)
        if best is None or cost < best[0]: best = (cost, p)
    if not best: return None
    p = best[1]
    return {'len_m': 2 * len(p), 'lit_cells': sum(1 for c in p if field.get(c, 0) >= EXPOSED),
            'watched_cells': sum(1 for c in p if c in static or patrol.get(c, 0) > 0.15)}

out = {}
# M05: Penrose -> boat, with every light, and with each main shut
from report import coverage, human_graph, light_field
m = Mission(os.path.join(MISSIONS, 'm05.txt'))
HG = human_graph(m)
dst = [(x, y) for x in range(43, 49) for y in range(37, 39) if m.floor(x, y)]
for tag, off in (('all lit', set()), ('works off', {'works'}), ('hall off', {'hall'}), ('wharf off', {'wharf'}), ('all three off', {'works', 'hall', 'wharf'})):
    for e in m.lights():
        e.flags.discard('off')
        if e.opts.get('group') in off: e.flags.add('off')
    field, src = light_field(m)
    sn, sl, patrol, *_ = coverage(m, field, HG)
    out['m05 ' + tag] = carry_route(m, field, sn | sl, patrol, m.ent('penrose').cell, dst)
# M09: escort from Ward C cells to the lane exit, ground only, counting sunstone-burn cells (fledglings burn)
m = Mission(os.path.join(MISSIONS, 'm09.txt'))
G = vamp_graph(m, 1, invited=True); G.remove_nodes_from([n for n in list(G) if m.raised(*n)])
burn = set()
for e in m.lights():
    k, r, inten, h = m.light_params(e)
    if k == 'sunstone':
        for y in range(m.H):
            for x in range(m.W):
                if m.floor(x, y) and math.hypot((x - e.x) * CS, (y - e.y) * CS) < r * 0.85 and not seg_blocked(m, e.x, e.y, h, x, y, 1.0):
                    burn.add((x, y))
for f in ('f1', 'f2', 'f3', 'clem'):
    s = m.ent(f).cell
    try:
        p = nx.shortest_path(G, s, (3, 40))
        q = nx.shortest_path(G, s, (3, 40), weight=lambda u, v, a: 1 + (100 if v in burn else 0))
        out['m09 ' + f] = {'shortest_m': 2 * len(p), 'burn_cells_shortest': sum(1 for c in p if c in burn),
                           'min_burn_route_m': 2 * len(q), 'burn_cells_min': sum(1 for c in q if c in burn)}
    except Exception as ex:
        out['m09 ' + f] = str(ex)
# perimeter highway: share of each mission's safest typical route on the outer 2 rings of the map, and raised
mets = json.load(open(os.path.join(OUT, 'metrics.json')))
for i in [f'm{n:02d}' for n in range(1, 15)]:
    m, met, D = analyse_mission(os.path.join(MISSIONS, i + '.txt'))
    border = 0; tot = 0
    for oid, r in D['routes']['typical'].items():
        if not r: continue
        for (x, y) in r['path']:
            tot += 1
            if (x <= 1 or y <= 1 or x >= m.W - 2 or y >= m.H - 2) and m.raised(x, y): border += 1
    ring = sum(1 for x in range(m.W) for y in range(m.H) if (x == 0 or y == 0 or x == m.W - 1 or y == m.H - 1) and m.raised(x, y))
    per = 2 * (m.W + m.H) - 4
    out['border ' + i] = {'raised_border_pct': round(100 * ring / per), 'safest_route_on_raised_border_pct': round(100 * border / max(1, tot), 1)}
json.dump(out, open(os.path.join(OUT, 'special.json'), 'w'), indent=1)
for k, v in out.items(): print(k, v)
