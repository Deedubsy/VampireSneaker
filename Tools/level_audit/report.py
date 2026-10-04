"""Run the level analysis for each mission and render top-down evidence images.

python3 report.py            # all missions
python3 report.py m02 m07    # some
Writes Docs/level_audit/<id>_layout.png, <id>_analysis.png and metrics.json.
"""
import sys, os, json, math
from collections import Counter, defaultdict
from PIL import Image, ImageDraw, ImageFont
from analyse import *

PX = 16
FONT = ImageFont.load_default(size=11)
FONT_B = ImageFont.load_default(size=14)

COL = {
    'void': (10, 10, 14), 'street': (58, 61, 70), 'dirt': (70, 62, 50), 'grass': (48, 62, 42), 'wood': (72, 56, 40),
    'tile': (60, 64, 72), 'carpet': (74, 42, 50), 'shallow': (40, 58, 76), 'canal': (24, 70, 140),
    'bridge': (100, 78, 48), 'doorway': (120, 90, 50), 'stairs': (150, 140, 90), 'pipe': (90, 150, 110),
    'hedge': (36, 86, 40), 'wall': (112, 108, 100), 'pipeup': (120, 160, 120), 'stairsup': (160, 150, 100),
    'gallery': (110, 92, 72), 'house': (138, 116, 98), 'building': (160, 136, 112), 'tower': (200, 176, 144),
    'bars': (80, 90, 106), 'vent': (90, 90, 90), 'crates': (106, 90, 58),
}
FACTION = {'civilian': (200, 200, 200), 'watch': (90, 130, 230), 'vigil': (235, 235, 245), 'inst': (230, 230, 150),
           'church': (240, 210, 120), 'guild': (230, 160, 60)}


def fcol(t):
    if t in ('watchman', 'sergeant', 'soldier', 'sentry'): return FACTION['watch']
    if t in VIGIL: return FACTION['vigil']
    if t in ('orderly', 'alchemist', 'scholar', 'engineer', 'saule'): return FACTION['inst']
    if t in ('priest', 'acolyte'): return FACTION['church']
    if t in ('lamplighter', 'worker'): return FACTION['guild']
    return FACTION['civilian']


def cc(x, y):
    return (x * PX + PX // 2, y * PX + PX // 2)


def base_image(m, dim=1.0):
    img = Image.new('RGB', (m.W * PX, m.H * PX + 34), (0, 0, 0))
    d = ImageDraw.Draw(img)
    for y in range(m.H):
        for x in range(m.W):
            c = COL.get(m.k(x, y), (255, 0, 255))
            c = tuple(int(v * dim) for v in c)
            d.rectangle([x * PX, y * PX, x * PX + PX - 1, y * PX + PX - 1], fill=c)
            if m.raised(x, y):
                # bevel so height reads
                d.line([x * PX, y * PX, x * PX + PX - 1, y * PX], fill=tuple(min(255, int(v * 1.25)) for v in c))
    return img


def overlay(img, cells, rgba):
    ov = Image.new('RGBA', img.size, (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
    for (x, y) in cells:
        a = rgba if not callable(rgba) else rgba((x, y))
        if a: d.rectangle([x * PX, y * PX, x * PX + PX - 1, y * PX + PX - 1], fill=a)
    return Image.alpha_composite(img.convert('RGBA'), ov).convert('RGB')


def sector(d, x, y, face, half, r_cells, fill=None, outline=None, width=1):
    cx, cy = cc(x, y)
    rr = r_cells * PX
    a0 = face - 90 - half; a1 = face - 90 + half
    if half >= 180:
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=fill, outline=outline, width=width); return
    d.pieslice([cx - rr, cy - rr, cx + rr, cy + rr], a0, a1, fill=fill, outline=outline, width=width)


def draw_entities(m, img, cones=True, routes=True, labels=True):
    ov = Image.new('RGBA', img.size, (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
    # routes
    if routes:
        for e in m.npcs():
            rid = e.opts.get('route')
            if rid and rid in m.routes:
                pts = [cc(p['x'], p['y']) for p in m.routes[rid]['pts']]
                if m.routes[rid]['mode'] == 'loop': pts = pts + [pts[0]]
                if len(pts) > 1: d.line(pts, fill=fcol(e.type) + (150,), width=2)
    # static cones
    if cones:
        for e in m.npcs():
            if 'friendly' in e.flags or 'prisoner' in e.flags or 'asleep' in e.flags: continue
            if e.opts.get('route'): continue
            half, near, far, lu = npc_vision(m, e)
            face = FACING.get(e.opts.get('face', 'S').upper(), 180)
            sector(d, e.x, e.y, face, half, far / CS, outline=(255, 90, 90, 140), width=1)
            sector(d, e.x, e.y, face, half, near / CS, fill=(255, 40, 40, 70))
    # doors
    for (x, y), e in m.doors.items():
        col = (200, 140, 60, 255)
        if 'locked' in e.flags: col = (255, 40, 40, 255)
        if 'threshold' in e.flags or 'home' in e.opts: col = (190, 90, 255, 255)
        d.rectangle([x * PX + 3, y * PX + 3, x * PX + PX - 4, y * PX + PX - 4], outline=col, width=2)
    # lights
    for e in m.lights():
        k, r, inten, h = m.light_params(e)
        cx, cy = cc(e.x, e.y)
        if k == 'moon':
            rr = rendered_radius(m, e) / CS * PX
            d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=(120, 170, 255, 220), width=2)
            d.text((cx - 3, cy - 6), 'm', fill=(120, 170, 255, 255), font=FONT)
            continue
        col = (255, 220, 120, 255) if 'off' not in e.flags else (120, 100, 60, 255)
        if k in BURNS: col = (255, 255, 200, 255)
        d.ellipse([cx - 3, cy - 3, cx + 3, cy + 3], fill=col)
        if k == 'searchlight':
            d.text((cx + 4, cy - 6), 'SL', fill=(220, 230, 255, 255), font=FONT)
    # props that hide
    for e in m.ents:
        if e.kind == 'prop' and 'hide' in e.flags and m.active(e):
            cx, cy = cc(e.x, e.y); d.rectangle([cx - 2, cy - 2, cx + 2, cy + 2], outline=(80, 220, 220, 200))
        if e.kind in ('secret',) and m.active(e):
            cx, cy = cc(e.x, e.y); d.polygon([(cx, cy - 5), (cx + 5, cy), (cx, cy + 5), (cx - 5, cy)], outline=(255, 215, 0, 255))
        if e.kind in ('valve', 'lever', 'generator', 'bell', 'boat', 'window', 'use', 'trap') and m.active(e):
            cx, cy = cc(e.x, e.y); d.rectangle([cx - 4, cy - 4, cx + 4, cy + 4], outline=(60, 230, 200, 255), width=2)
            if labels: d.text((cx + 5, cy - 5), e.kind[0].upper(), fill=(60, 230, 200, 255), font=FONT)
        if e.kind == 'spawn':
            cx, cy = cc(e.x, e.y); d.text((cx - 3, cy - 6), 'R', fill=(255, 80, 80, 255), font=FONT_B)
        if e.kind == 'gate' and m.active(e):
            cx, cy = cc(e.x, e.y); d.line([cx - 6, cy, cx + 6, cy], fill=(255, 80, 80, 255), width=3)
    # npcs
    for e in m.npcs():
        cx, cy = cc(e.x, e.y)
        col = fcol(e.type) + (255,)
        if 'friendly' in e.flags or 'prisoner' in e.flags: col = (120, 255, 120, 255)
        r = 4
        if e.opts.get('route'): d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col, outline=(0, 0, 0, 255))
        else: d.rectangle([cx - r, cy - r, cx + r, cy + r], fill=col, outline=(0, 0, 0, 255))
        if 'notable' in e.flags or e.type in ('tracker', 'vane', 'saule', 'notable'):
            d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], outline=(255, 60, 200, 255), width=2)
        if e.type in ('hunter', 'tracker', 'inquisitor', 'vane', 'sentry'):
            d.text((cx + 4, cy - 12), '^', fill=(255, 255, 255, 255), font=FONT_B)
    # dormant
    for e in m.npcs(True):
        if e.group:
            cx, cy = cc(e.x, e.y); d.ellipse([cx - 3, cy - 3, cx + 3, cy + 3], outline=(255, 120, 120, 160))
    # player + objectives
    p = m.player; cx, cy = cc(p.x, p.y)
    d.polygon([(cx, cy - 8), (cx + 7, cy + 6), (cx - 7, cy + 6)], fill=(60, 255, 90, 255), outline=(0, 0, 0, 255))
    for oid, (ox, oy), t in objective_points(m):
        cx, cy = cc(ox, oy)
        d.regular_polygon((cx, cy, 8), 5, fill=(255, 40, 220, 230), outline=(0, 0, 0, 255))
        if labels: d.text((cx + 9, cy - 6), oid, fill=(255, 140, 240, 255), font=FONT_B)
    return Image.alpha_composite(img.convert('RGBA'), ov).convert('RGB')


def grid_lines(img, m):
    d = ImageDraw.Draw(img)
    for x in range(0, m.W, 10):
        d.line([x * PX, 0, x * PX, m.H * PX], fill=(255, 255, 255), width=1)
        d.text((x * PX + 2, m.H * PX + 2), str(x), fill=(200, 200, 200), font=FONT)
    for y in range(0, m.H, 10):
        d.line([0, y * PX, m.W * PX, y * PX], fill=(255, 255, 255), width=1)
        d.text((2, y * PX + 2), str(y), fill=(220, 220, 220), font=FONT)
    return img


def caption(img, m, text):
    import textwrap
    lines = textwrap.wrap(text, width=max(60, int(m.W * PX / 6.2)))
    out = Image.new('RGB', (img.width, img.height + 14 * len(lines)), (0, 0, 0))
    out.paste(img, (0, 0))
    d = ImageDraw.Draw(out)
    for i, ln in enumerate(lines):
        d.text((4, m.H * PX + 18 + 14 * i), ln, fill=(240, 240, 240), font=FONT)
    return out


def analyse_mission(path, cm=False, invited=False):
    m = Mission(path)
    if cm:
        m.active_groups = {e.group for e in m.ents if e.group and e.group.startswith('cm_')}
    A = AWAKEN_TYPICAL.get(m.mid, 1); AG = AWAKEN_GHOST.get(m.mid, 1)
    field, src = light_field(m)
    HG = human_graph(m)
    sn, sl, patrol, watchers, per_npc, roof_watch = coverage(m, field, HG)
    static = sn | sl
    # searchlights: a moving pool (seen at any range by the operator) swept along its waypoints; intermittent cover
    for e in m.lights():
        if m.light_kind(e) != 'searchlight' or 'sweep' not in e.opts: continue
        k, rr, inten, h = m.light_params(e)
        pts = [float(v) for v in e.opts['sweep'].split(',')]
        pts = list(zip(pts[0::2], pts[1::2]))
        rad = rr * 0.72 / CS
        L = sum(math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]) for i in range(len(pts) - 1)) or 1
        frac = max(0.25, min(1.0, 4 * rad / L))
        cells = set()
        for i in range(len(pts) - 1):
            (x0, y0), (x1, y1) = pts[i], pts[i + 1]
            n = int(math.hypot(x1 - x0, y1 - y0) * 4) + 1
            for j in range(n + 1):
                px, py = x0 + (x1 - x0) * j / n, y0 + (y1 - y0) * j / n
                for cx in range(int(px - rad) - 1, int(px + rad) + 2):
                    for cy in range(int(py - rad) - 1, int(py + rad) + 2):
                        if math.hypot(cx - px, cy - py) <= rad and m.walktop(cx, cy): cells.add((cx, cy))
        for c in cells:
            if c not in static: patrol[c] = max(patrol[c], frac)
    ground = [c for c in field if m.floor(*c)]
    raised = [c for c in field if m.raised(*c)]
    gset = set(ground)
    lit_ground = {c for c in ground if field[c] >= EXPOSED}
    met = {'id': m.mid, 'title': m.header.get('title', ''), 'W': m.W, 'H': m.H,
           'awaken_typical': A, 'awaken_ghost': AG, 'ground_cells': len(ground), 'raised_cells': len(raised)}

    # ---- people
    act = m.npcs(); dor = [e for e in m.npcs(True) if e.group]
    hostile = [e for e in act if 'friendly' not in e.flags and 'prisoner' not in e.flags]
    met['npcs'] = len(act); met['dormant'] = len(dor)
    met['dormant_groups'] = sorted({e.group for e in dor})
    met['types'] = dict(Counter(e.type for e in act))
    met['static'] = sum(1 for e in hostile if per_npc.get(e.id, {}).get('static'))
    met['patrolling'] = sum(1 for e in hostile if e.id in per_npc and not per_npc[e.id]['static'])
    met['asleep_sit'] = sum(1 for e in hostile if 'asleep' in e.flags or 'sit' in e.flags)
    met['scan'] = sum(1 for e in hostile if 'scan' in e.flags)
    met['looksup'] = [e.id for e in hostile if ARCH.get(e.type, ARCH['civilian'])[3] and 'U' in ARCH[e.type][3]]
    cycles = {k: v['cycle_s'] for k, v in per_npc.items() if not v['static']}
    met['patrol_cycles_s'] = cycles
    met['patrol_over_40s'] = sorted([k for k, v in cycles.items() if v > 40])

    # isolation: humans with no other active hostile within 8 m (4 cells) of their post or route centroid
    def centroid(e):
        rid = e.opts.get('route')
        if rid and rid in m.routes:
            pts = m.routes[rid]['pts']; return (sum(p['x'] for p in pts) / len(pts), sum(p['y'] for p in pts) / len(pts))
        return (e.x, e.y)
    humans = [e for e in hostile if e.type not in ('hound', 'fledgling')]
    iso = []
    for e in humans:
        cx, cy = centroid(e)
        near = [o for o in hostile if o is not e and math.hypot(centroid(o)[0] - cx, centroid(o)[1] - cy) * CS < 8]
        if not near: iso.append(e.id)
    met['isolated_humans'] = len(iso); met['humans'] = len(humans)

    # ---- light
    lts = m.lights()
    kinds = Counter(m.light_kind(e) for e in lts)
    met['lights'] = dict(kinds)
    met['lights_off_at_start'] = sum(1 for e in lts if 'off' in e.flags)
    moon = [e for e in lts if m.light_kind(e) == 'moon']
    met['moon_pools'] = len(moon)
    met['moon_pools_gameplay_zero'] = sum(1 for e in moon if exposure_radius(m, e) <= 0.1)
    met['ground_lit_pct'] = round(100 * len(lit_ground) / max(1, len(ground)), 1)
    met['raised_lit_pct'] = round(100 * sum(1 for c in raised if field[c] >= EXPOSED) / max(1, len(raised)), 1)
    # exclusive coverage per light (cells that go dark without it)
    excl = Counter(); cover = Counter()
    for c in lit_ground:
        s = src[c]
        if s is None: continue
        cover[s.id] += 1
    # redundancy: recompute without each light only for lights that cover cells (cheap approximation: a light is
    # "shadowed" if every cell it is brightest at stays >= 0.35 from the second-best source)
    cover_all = Counter(src[c].id for c in field if field[c] >= EXPOSED and src[c] is not None)
    met['decorative_lights'] = sorted(e.id for e in lts if m.light_kind(e) not in ('moon', 'searchlight', 'sunbeam')
                                      and 'off' not in e.flags and cover_all.get(e.id, 0) == 0)
    met['top_lamps_by_ground_cells'] = cover.most_common(5)

    # ---- watched ground
    met['static_watch_ground_pct'] = round(100 * len([c for c in static if c in gset]) / max(1, len(ground)), 1)
    met['patrol_watch_ground_pct'] = round(100 * len([c for c in patrol if c in gset and c not in static]) / max(1, len(ground)), 1)
    met['roof_cells_watched_by_lookup'] = len(roof_watch)
    met['raised_watched_pct'] = round(100 * len([c for c in static | set(patrol) if m.raised(*c)]) / max(1, len(raised)), 1)

    # ---- topology
    start = m.player.cell
    objs = objective_points(m)
    goals = {}
    for oid, (ox, oy), t in objs:
        c = (int(math.floor(ox + 0.5)), int(math.floor(oy + 0.5)))
        # nearest walkable
        best = None
        for r in range(0, 4):
            for dx in range(-r, r + 1):
                for dy in range(-r, r + 1):
                    cc_ = (c[0] + dx, c[1] + dy)
                    if m.walktop(*cc_) and not m.blocksmove(*cc_):
                        best = cc_; break
                if best: break
            if best: break
        if best: goals.setdefault(oid, best)
    met['objectives'] = {k: list(v) for k, v in goals.items()}

    VG1 = vamp_graph(m, 1); VGA = vamp_graph(m, A, invited=invited); VGG = vamp_graph(m, AG, invited=invited)
    VGi = vamp_graph(m, 1, invited=True)
    UG = nx.Graph()      # physical ground topology (thresholds open: an invitation can come mid-mission)
    for u, v, dd in VGi.edges(data=True):
        if dd['how'] == 'walk' and m.floor(*u) and m.floor(*v): UG.add_edge(u, v)
    for c in ground:
        if c in VGi: UG.add_node(c)

    # ring classes (Hunt topology)
    rc = {}
    for c in ground:
        if c in UG: rc[c] = ring_class(m, UG, c)[0]
    cnt = Counter(rc.values()); tot = max(1, sum(cnt.values()))
    met['ring_pct'] = {k: round(100 * v / tot, 1) for k, v in cnt.items()}

    # vertical access: distance (cells, path) from a ground cell to a climb at A1 and at typical A
    def climb_cells(G):
        out = set()
        for u, v, dd in G.edges(data=True):
            if dd['how'] in ('climb', 'ladder', 'crawl') and m.floor(*u) and m.raised(*v): out.add(u)
        return out
    def bfs_dist(srcs, R=5):
        dist = {s: 0 for s in srcs if s in UG}; q = deque(dist)
        while q:
            u = q.popleft()
            if dist[u] >= R: continue
            for n in UG.neighbors(u):
                if n not in dist: dist[n] = dist[u] + 1; q.append(n)
        return dist
    vA1 = bfs_dist(climb_cells(VG1)); vAA = bfs_dist(climb_cells(VGA))
    met['vertical_within_10m_A1_pct'] = round(100 * len([c for c in ground if c in vA1]) / max(1, len(ground)), 1)
    met['vertical_within_10m_typical_pct'] = round(100 * len([c for c in ground if c in vAA]) / max(1, len(ground)), 1)
    met['climb_points_A1'] = len(climb_cells(VG1))
    dark_safe = {c for c in ground if field[c] < EXPOSED and c not in static}
    dD = bfs_dist(dark_safe, 3)

    # spaces (rooms / yards / streets) and their exits
    Sx = set(c for c in ground if c in VGi or m.k(*c) == 'doorway')
    reg, rcells, rexits, rnbrs, conn = regions(m, UG, Sx)
    small = {r for r, cl in rcells.items() if len(cl) <= 120}
    dead_regions = [r for r in rcells if rexits.get(r, 0) <= 1 and len(rcells[r]) >= 6]
    met['spaces'] = len([r for r in rcells if len(rcells[r]) >= 6])
    met['dead_end_spaces'] = len(dead_regions)
    met['dead_end_spaces_no_climb_A1'] = len([r for r in dead_regions if not any(c in vA1 for c in rcells[r])])
    met['space_exit_hist'] = dict(Counter(min(4, rexits.get(r, 0)) for r in rcells if len(rcells[r]) >= 6))

    # Hunt heat: branches + vertical + dark nearby; lit cells penalised. Small spaces score by their exits,
    # large ones (streets, yards) by the local ring topology.
    heat = {}
    for c, cls in rc.items():
        s = {'deadend': 0, 'pocket': 0, 'corridor': 1, 'open': 2, 'junction': 3}[cls]
        if c in reg and reg[c] in small:
            s = min(s, {0: 0, 1: 0, 2: 1, 3: 2}.get(rexits.get(reg[c], 0), 3))
        s += 1 if c in vAA else 0
        s += 1 if c in dD else 0
        if c in lit_ground: s -= 1
        heat[c] = 'broken' if s <= 0 else 'dangerous' if s == 1 else 'acceptable' if s <= 3 else 'excellent'
    hc = Counter(heat.values()); ht = max(1, sum(hc.values()))
    met['hunt_pct'] = {k: round(100 * hc.get(k, 0) / ht, 1) for k in ('excellent', 'acceptable', 'dangerous', 'broken')}

    # passages
    S = Sx
    narrow = [c for c in ground if cross_width(m, S, c) == 1 and m.k(*c) != 'doorway']
    met['one_cell_passage_pct'] = round(100 * len(narrow) / max(1, len(ground)), 1)
    met['doorways'] = sum(1 for c in ground if m.k(*c) == 'doorway')
    clutter = []
    for c, types in m.blockprops.items():
        if c in S and cross_width(m, S, c) <= 2:
            clutter.append((c, types))
    met['props_in_narrow_passages'] = len(clutter)

    # min vertex cut start -> each goal (ground graph, A1, locked/threshold doors shut)
    cuts = {}
    UGd = nx.Graph(UG)
    for oid, g in goals.items():
        if start in UGd and g in UGd and nx.has_path(UGd, start, g):
            try:
                cut = nx.minimum_node_cut(UGd, start, g)
                cuts[oid] = sorted(cut)
            except Exception:
                cuts[oid] = 'adjacent'
        else:
            cuts[oid] = None
    met['ground_min_cut'] = {k: (len(v) if isinstance(v, list) else v) for k, v in cuts.items()}

    # ---- optimiser: least-risk routes
    lit = lambda c: field.get(c, 0) >= EXPOSED
    def risk(c):
        r = 0.0
        if c in static: r += 40
        r += 30 * patrol.get(c, 0)
        if m.floor(*c) and lit(c): r += 2
        return r
    def cost(u, v, how):
        return 1 + risk(v)
    routes = {}
    for name, G in (('A1', VG1), ('typical', VGA), ('ghost', VGG)):
        res = {}
        for oid, g in goals.items():
            r = optimise(m, G, start, [g], cost)
            if not r: res[oid] = None; continue
            path, c = r
            on_raised = sum(1 for p in path if m.raised(*p))
            risky = sum(1 for p in path if risk(p) >= 5)
            res[oid] = {'len_m': len(path) * 2, 'raised_pct': round(100 * on_raised / len(path), 1),
                        'risky_cells': risky, 'cost': round(c, 1), 'path': path}
        routes[name] = res
    # ground-only least risk (no climbs) for comparison
    GG = VG1.copy()
    GG.remove_nodes_from([n for n in list(GG) if m.raised(*n)])
    res = {}
    for oid, g in goals.items():
        r = optimise(m, GG, start, [g], cost)
        res[oid] = None if not r else {'len_m': len(r[0]) * 2, 'risky_cells': sum(1 for p in r[0] if risk(p) >= 5),
                                       'cost': round(r[1], 1), 'path': r[0]}
    routes['ground'] = res
    met['routes'] = {k: {o: ({kk: vv for kk, vv in v.items() if kk != 'path'} if v else None) for o, v in r.items()}
                     for k, r in routes.items()}
    # roof bypass: how many ground cells must be touched if roofs are preferred (typical Awakening)
    def roofcost(u, v, how):
        return (1 if m.raised(*v) else 6) + risk(v)
    res = {}
    for oid, g in goals.items():
        r = optimise(m, VGA, start, [g], roofcost)
        if r:
            p = r[0]
            res[oid] = {'ground_cells': sum(1 for c in p if m.floor(*c)), 'risky': sum(1 for c in p if risk(c) >= 5),
                        'len_m': len(p) * 2}
    met['roof_preferred'] = res

    # ---- laws
    met['canal_cells'] = sum(1 for y in range(m.H) for x in range(m.W) if m.k(x, y) == 'canal')
    met['bridge_cells'] = sum(1 for y in range(m.H) for x in range(m.W) if m.k(x, y) == 'bridge')
    met['boats'] = sum(1 for e in m.ents if e.kind == 'boat')
    met['locked_doors'] = sum(1 for e in m.doors.values() if 'locked' in e.flags)
    met['threshold_doors'] = sum(1 for e in m.doors.values() if 'threshold' in e.flags or 'home' in e.opts)
    met['home_zone_cells'] = len(m.home_cells)
    met['burning_lights'] = sum(1 for e in lts if m.light_kind(e) in BURNS)
    met['holy_npcs'] = sum(1 for e in act if e.type in ('priest', 'vane'))
    met['censers'] = sum(1 for e in act if e.type in ('alchemist', 'saule'))
    met['searchlights'] = sum(1 for e in lts if m.light_kind(e) == 'searchlight')
    met['hides'] = sum(1 for e in m.ents if e.kind == 'prop' and 'hide' in e.flags and m.active(e)) + \
        sum(1 for e in m.ents if e.kind == 'hide' and m.active(e))
    met['interactables'] = dict(Counter(e.kind for e in m.ents if m.active(e) and e.kind in
                                        ('valve', 'lever', 'gate', 'generator', 'bell', 'boat', 'window', 'use', 'trap', 'listen')))
    met['secrets'] = sum(1 for e in m.ents if e.kind == 'secret')
    return m, met, dict(field=field, src=src, static=static, patrol=patrol, rc=rc, heat=heat, routes=routes,
                        cuts=cuts, goals=goals, narrow=narrow, clutter=clutter, roof_watch=roof_watch)


def render(m, met, D):
    os.makedirs(OUT, exist_ok=True)
    field = D['field']
    # ---- layout: tiles + lit cells + entities
    img = base_image(m)
    img = overlay(img, [c for c, v in field.items() if v >= EXPOSED],
                  lambda c: (255, 190, 80, int(60 + 120 * min(1, (field[c] - EXPOSED) / 0.5))))
    img = draw_entities(m, img)
    img = grid_lines(img, m)
    img = caption(img, m, f"{m.mid.upper()} {m.header.get('title','')} - layout. Amber = gameplay-lit (>=0.35). Blue rings = moon pools "
                    f"(rendered, 0 gameplay light). Squares = posts (red wedge = near band), dots = patrollers + routes. "
                    f"Green = start, pink stars = objectives.")
    img.save(os.path.join(OUT, f'{m.mid}_layout.png'))

    # ---- analysis: hunt heat + watched + routes + cuts
    img = base_image(m, 0.55)
    heatcol = {'excellent': (60, 200, 90, 110), 'acceptable': (200, 200, 60, 90), 'dangerous': (240, 130, 40, 120),
               'broken': (230, 30, 30, 150)}
    img = overlay(img, D['heat'].keys(), lambda c: heatcol[D['heat'][c]])
    # watched: static = red hatch; patrol = magenta dots
    ov = Image.new('RGBA', img.size, (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
    for (x, y) in D['static']:
        d.line([x * PX, y * PX + PX - 1, x * PX + PX - 1, y * PX], fill=(255, 30, 30, 200), width=2)
    for (x, y), f in D['patrol'].items():
        if (x, y) in D['static']: continue
        cx, cy = cc(x, y); r = 1 + int(3 * f)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 60, 255, 170))
    for (x, y) in D['roof_watch']:
        d.rectangle([x * PX + 1, y * PX + 1, x * PX + PX - 2, y * PX + PX - 2], outline=(255, 255, 255, 200))
    # routes
    def drawpath(p, col, w, off):
        if p and len(p) > 1:
            d.line([(cc(*c)[0] + off, cc(*c)[1] + off) for c in p], fill=col, width=w)
    for oid in D['goals']:
        r = D['routes']['ground'].get(oid)
        drawpath(r and r['path'], (0, 230, 255, 230), 3, -2)
        r = D['routes']['typical'].get(oid)
        drawpath(r and r['path'], (255, 255, 255, 240), 3, 2)
    for oid, cut in D['cuts'].items():
        if isinstance(cut, list) and len(cut) <= 3:
            for (x, y) in cut:
                d.rectangle([x * PX - 2, y * PX - 2, x * PX + PX + 1, y * PX + PX + 1], outline=(255, 255, 0, 255), width=3)
    for c, types in D['clutter']:
        x, y = c; d.text((x * PX + 3, y * PX + 1), 'x', fill=(255, 255, 0, 255), font=FONT_B)
    img = Image.alpha_composite(img.convert('RGBA'), ov).convert('RGB')
    img = draw_entities(m, img, cones=False, routes=False, labels=False)
    img = grid_lines(img, m)
    img = caption(img, m, f"{m.mid.upper()} analysis. Hunt heat: green excellent / yellow ok / orange dangerous / red broken. "
                    f"Red hatch = permanently watched, magenta = patrol-watched, white box = roof seen by look-up. "
                    f"Cyan = safest ground route, white = safest route at Awakening {met['awaken_typical']}. Yellow box = min cut.")
    img.save(os.path.join(OUT, f'{m.mid}_analysis.png'))


def variants(ids):
    """Roof dominance with and without the Dossier's dormant bodies, and with thresholds opened (invited)."""
    out = {}
    for i in ids:
        row = {}
        for tag, kw in (('base', {}), ('cm', {'cm': True}), ('invited', {'invited': True}), ('cm_invited', {'cm': True, 'invited': True})):
            m, met, D = analyse_mission(os.path.join(MISSIONS, i + '.txt'), **kw)
            row[tag] = {'roof_preferred': met['roof_preferred'],
                        'typical': met['routes']['typical'], 'ground': met['routes']['ground'],
                        'raised_watched_pct': met['raised_watched_pct'], 'lookup_roof': met['roof_cells_watched_by_lookup'],
                        'npcs': met['npcs']}
        out[i] = row
        print(i, 'variants done')
    json.dump(out, open(os.path.join(OUT, 'variants.json'), 'w'), indent=1, default=str)


def main(ids):
    os.makedirs(OUT, exist_ok=True)
    mp = os.path.join(OUT, 'metrics.json')
    allm = json.load(open(mp)) if os.path.exists(mp) else {}
    for i in ids:
        m, met, D = analyse_mission(os.path.join(MISSIONS, i + '.txt'))
        render(m, met, D)
        allm[i] = met
        print(i, 'done')
    json.dump(allm, open(mp, 'w'), indent=1, default=str)


if __name__ == '__main__':
    args = sys.argv[1:]
    if args and args[0] == 'variants':
        variants(args[1:] or [f'm{n:02d}' for n in range(1, 15)])
    else:
        main(args or [f'm{n:02d}' for n in range(1, 15)])
