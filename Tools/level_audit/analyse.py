"""Level-design analysis of Vespertine missions (offline, from the map files).

Usage: python3 analyse.py [m01 m02 ...]   -> writes PNGs and metrics JSON to Docs/level_audit/
See mapmodel.py for the game rules mirrored here.
"""
import sys, os, json, math, heapq, glob
from collections import defaultdict, deque
import networkx as nx
from mapmodel import *

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MISSIONS = os.path.join(ROOT, 'Assets', '_Game', 'Resources', 'Missions')
OUT = os.path.join(ROOT, 'Docs', 'level_audit')


# ====================================================================== light
def seg_blocked(m, x0, y0, z0, x1, y1, z1, block_vision=False, doors=False):
    """March a 3D segment in cell units (z in metres). Blocked by raised tiles whose top is above the line
    (LightBlockMask = walls). With block_vision, hedges and shut doors also block (SightMask / VisionBlockMask)."""
    dx, dy = x1 - x0, y1 - y0
    dist = math.hypot(dx, dy)
    n = max(1, int(dist / 0.2))
    c0 = (int(math.floor(x0 + 0.5)), int(math.floor(y0 + 0.5)))
    c1 = (int(math.floor(x1 + 0.5)), int(math.floor(y1 + 0.5)))
    for i in range(1, n):
        t = i / n
        x, y, z = x0 + dx * t, y0 + dy * t, z0 + (z1 - z0) * t
        c = (int(math.floor(x + 0.5)), int(math.floor(y + 0.5)))
        if c == c0 or c == c1: continue
        if m.raised(*c) and m.top(*c) > z: return True
        if block_vision:
            if m.k(*c) == 'hedge' or m.k(*c) == 'void': return True
            if doors and c in m.doors and 'open' not in m.doors[c].flags: return True
    return False


def light_field(m, include_off=False):
    """Exposure at every walk top, 1 m above it: ambient + brightest unblocked source (LightSystem.LightAt)."""
    lights = []
    for e in m.lights():
        k, r, inten, h = m.light_params(e)
        if k in ('searchlight', 'sunbeam'): continue          # moving pools, evaluated separately
        if 'off' in e.flags and not include_off: continue
        cx, cy = e.cell
        base = m.top(cx, cy)
        lights.append((e, k, r, inten, base + h))
    field = {}; src = {}
    amb = m.ambient
    for y in range(m.H):
        for x in range(m.W):
            if not m.walktop(x, y): continue
            tz = m.top(x, y) + 1.0
            best = 0.0; bs = None
            for (e, k, r, inten, sz) in lights:
                ddx = (e.x - x) * CS; ddy = (e.y - y) * CS; dz = sz - tz
                d = math.sqrt(ddx * ddx + ddy * ddy + dz * dz)
                if d >= r: continue
                v = inten * (1 - (d / r) ** 2)
                if v <= best: continue
                if k != 'moon' and seg_blocked(m, e.x, e.y, sz, x, y, tz): continue
                best = v; bs = e
            field[(x, y)] = min(1.0, amb + best); src[(x, y)] = bs
    return field, src


def exposure_radius(m, e):
    k, r, inten, h = m.light_params(e)
    need = EXPOSED - m.ambient
    if inten <= need: return 0.0
    d = r * math.sqrt(1 - need / inten)
    dy = abs(h - 1.0)
    return 0.0 if d <= dy else math.sqrt(d * d - dy * dy)


def rendered_radius(m, e):
    """Ground radius of the authored Unity spot for a light that is NOT fitted (moon pools keep the authored look)."""
    k, r, inten, h = m.light_params(e)
    ang = math.degrees(math.atan2(r, h)) * 2 + 10
    ang = max(30, min(170, ang))
    return h * math.tan(math.radians(ang / 2))


# ====================================================================== navigation
def human_graph(m):
    """Humans: floors and doorways, plus stairs up to same-height raised tops (ladders), walking along raised tops."""
    G = nx.Graph()
    def ok(x, y):
        return m.walktop(x, y) and not m.blocksmove(x, y)
    for y in range(m.H):
        for x in range(m.W):
            if not ok(x, y): continue
            G.add_node((x, y))
            for dx, dy in ((1, 0), (0, 1)):
                nx_, ny_ = x + dx, y + dy
                if ok(nx_, ny_) and abs(m.top(x, y) - m.top(nx_, ny_)) < 1.5:
                    G.add_edge((x, y), (nx_, ny_))
    for y in range(m.H):
        for x in range(m.W):
            if m.k(x, y) in ('stairs', 'stairsup'):
                up = up_neighbour(m, x, y)
                if up: G.add_edge((x, y), up)
    return G


def up_neighbour(m, x, y):
    """LevelBuilder.FindUpNeighbour: the adjacent raised tile above this one (highest first)."""
    best = None; bh = m.top(x, y) + 1.0
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        nx_, ny_ = x + dx, y + dy
        if m.raised(nx_, ny_) and m.top(nx_, ny_) > bh:
            if best is None or m.top(nx_, ny_) < m.top(*best): best = (nx_, ny_)
    return best


def vamp_graph(m, awaken, doors_blocked=True, mist=False, invited=False):
    """Directed graph of the vampire's moves at an Awakening level. Edge attr 'how': walk, climb, ladder, drop,
    crawl (ClimbAny, A4), leap (A3)."""
    G = nx.DiGraph()
    def ok(x, y):
        if not m.walktop(x, y) or m.blocksmove(x, y): return False
        if doors_blocked:
            d = m.doors.get((x, y))
            if d is not None and 'locked' in d.flags: return False
            if not invited:
                if d is not None and ('threshold' in d.flags or 'home' in d.opts): return False
                if (x, y) in m.home_cells: return False   # the zone blocker is 20 m tall: wall tops too
        return True
    for y in range(m.H):
        for x in range(m.W):
            if not ok(x, y): continue
            G.add_node((x, y))
            for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1)):
                a, b = x + dx, y + dy
                if not ok(a, b): continue
                ha, hb = m.top(x, y), m.top(a, b)
                if abs(ha - hb) < 1.5: G.add_edge((x, y), (a, b), how='walk')
                elif ha > hb: G.add_edge((x, y), (a, b), how='drop')
                elif awaken >= 4: G.add_edge((x, y), (a, b), how='crawl')
    for y in range(m.H):
        for x in range(m.W):
            kk = m.k(x, y)
            if kk in ('pipe', 'pipeup', 'stairs', 'stairsup') and ok(x, y):
                up = up_neighbour(m, x, y)
                if up and ok(*up):
                    how = 'ladder' if kk.startswith('stairs') else 'climb'
                    G.add_edge((x, y), up, how=how); G.add_edge(up, (x, y), how=how)
    if awaken >= 3:
        for y in range(m.H):
            for x in range(m.W):
                if not m.raised(x, y) or not ok(x, y): continue
                ha = m.top(x, y)
                for dx, dy in ((1, 0), (0, 1)):
                    for gap in (1, 2):
                        bx, by = x + dx * (gap + 1), y + dy * (gap + 1)
                        if not m.raised(bx, by) or not ok(bx, by): continue
                        hb = m.top(bx, by)
                        good = abs(ha - hb) <= 3.1
                        for kk in range(1, gap + 1):
                            gx, gy = x + dx * kk, y + dy * kk
                            if m.k(gx, gy) in ('canal', 'void'): good = False
                            elif m.top(gx, gy) > min(ha, hb) - 1.5: good = False
                        if not good: continue
                        if abs(ha - hb) <= 1.6:
                            G.add_edge((x, y), (bx, by), how='leap'); G.add_edge((bx, by), (x, y), how='leap')
                        elif ha > hb: G.add_edge((x, y), (bx, by), how='leap')
                        else: G.add_edge((bx, by), (x, y), how='leap')
                        break
    if mist:
        for y in range(m.H):
            for x in range(m.W):
                if m.k(x, y) in ('bars', 'vent'):
                    for dx, dy in ((1, 0), (0, 1)):
                        a, b = (x - dx, y - dy), (x + dx, y + dy)
                        if m.floor(*a) and m.floor(*b):
                            G.add_edge(a, b, how='mist'); G.add_edge(b, a, how='mist')
    return G


# ====================================================================== vision
def npc_vision(m, e):
    a = ARCH.get(e.type, ARCH['civilian'])
    fov, near, far, fl = a
    return fov / 2.0, near, far, 'U' in fl


def route_poses(m, e, HG):
    """(cell_x, cell_y, facing_deg, weight) samples along a patrol (shortest human path between waypoints), or the
    post. Facing: travel direction; waits use look= or the arrival direction."""
    rid = e.opts.get('route')
    fac = FACING.get(e.opts.get('face', 'S').upper(), 180)
    if not rid or rid not in m.routes:
        return [(e.x, e.y, fac, 1.0)], True
    r = m.routes[rid]
    pts = [(p['x'], p['y'], p['wait'], p['look']) for p in r['pts']]
    if len(pts) == 1:
        p = pts[0]
        return [(p[0], p[1], p[3] if p[3] is not None else fac, 1.0)], True
    seq = list(pts)
    if r['mode'] == 'loop': seq = seq + [seq[0]]
    elif r['mode'] == 'pingpong': seq = seq + seq[-2::-1]
    poses = []
    for i in range(len(seq) - 1):
        a, b = seq[i], seq[i + 1]
        ca = (int(math.floor(a[0] + 0.5)), int(math.floor(a[1] + 0.5)))
        cb = (int(math.floor(b[0] + 0.5)), int(math.floor(b[1] + 0.5)))
        try:
            path = nx.shortest_path(HG, ca, cb)
        except Exception:
            path = [ca, cb]
        for j in range(len(path) - 1):
            (x0, y0), (x1, y1) = path[j], path[j + 1]
            ang = math.degrees(math.atan2(x1 - x0, -(y1 - y0))) % 360
            poses.append((x0, y0, ang, CS / 1.6))       # seconds spent per cell at walk speed
        if b[2] > 0:
            look = b[3]
            if look is None and len(path) > 1:
                (x0, y0), (x1, y1) = path[-2], path[-1]
                look = math.degrees(math.atan2(x1 - x0, -(y1 - y0))) % 360
            poses.append((b[0], b[1], look if look is not None else fac, b[2]))
    return poses, False


def cone_cells(m, x, y, face, half, near, far, looksup, ez, losc):
    """Cells a guard at (x,y) facing `face` covers: returns dicts cell->band ('near' sees in any light, 'far' only if
    lit). Applies the 1.4 m touch zone, the 2.5 m height rule and line of sight over the 2D grid with heights."""
    out = {}
    R = int(far / CS) + 1
    fx, fy = math.sin(math.radians(face)), -math.cos(math.radians(face))
    for cy in range(int(y) - R, int(y) + R + 2):
        for cx in range(int(x) - R, int(x) + R + 2):
            if not m.walktop(cx, cy): continue
            ddx, ddy = (cx - x) * CS, (cy - y) * CS
            d = math.hypot(ddx, ddy)
            dh = m.top(cx, cy) - ez
            if d <= 1.4 and abs(dh) < 1.5:
                band = 'near'
            else:
                if d > far or d < 1e-3: continue
                cosang = (ddx * fx + ddy * fy) / d
                ang = math.degrees(math.acos(max(-1, min(1, cosang))))
                if ang > half: continue
                high = dh > 2.5 and not looksup
                if d <= near: band = 'near'
                elif high: continue
                else: band = 'far'
            key = (int(math.floor(x + 0.5)), int(math.floor(y + 0.5)), cx, cy)
            vis = losc.get(key)
            if vis is None:
                vis = not seg_blocked(m, x, y, ez + 1.6, cx, cy, m.top(cx, cy) + 1.2, block_vision=True, doors=True)
                losc[key] = vis
            if vis: out[(cx, cy)] = band
    return out


def coverage(m, field, HG):
    """Per cell: static detection (posts, every pose permanent), patrol time-fraction, and who covers it."""
    losc = {}
    static_near, static_lit, patrol = set(), set(), defaultdict(float)
    watchers = defaultdict(set)
    per_npc = {}
    roof_watch = set()
    for e in m.npcs():
        if 'friendly' in e.flags or 'prisoner' in e.flags or 'asleep' in e.flags: continue
        if e.type == 'hound' and 'follow' in e.opts: pass
        half, near, far, lu = npc_vision(m, e)
        poses, static = route_poses(m, e, HG)
        tot = sum(p[3] for p in poses) or 1
        cov = defaultdict(float)
        for (px, py, face, wt) in poses:
            ez = m.top(int(math.floor(px + 0.5)), int(math.floor(py + 0.5)))
            if 'sit' in e.flags: pass
            cs = cone_cells(m, px, py, face, half, near, far, lu, ez, losc)
            for c, band in cs.items():
                seen = band == 'near' or field.get(c, 0) >= EXPOSED
                if seen:
                    cov[c] += wt / tot
                    if lu and m.raised(*c): roof_watch.add(c)
                    if static:
                        (static_near if band == 'near' else static_lit).add(c)
        for c, f in cov.items():
            if not static: patrol[c] = max(patrol[c], min(1.0, f))
            watchers[c].add(e.id)
        per_npc[e.id] = {'static': static, 'cells': len(cov), 'poses': len(poses),
                         'cycle_s': round(sum(p[3] for p in poses), 1) if not static else 0}
    return static_near, static_lit, patrol, watchers, per_npc, roof_watch


# ====================================================================== objectives
def objective_points(m):
    pts = []
    for o in m.objectives:
        if o['cat'] == 'optional': continue
        t = o['type']; a = o['args']
        if t in ('reach', 'escape') and len(a) >= 2 and isnum(a[0]):
            x, y = float(a[0]), float(a[1]); w = float(a[2]) if len(a) > 2 else 1; h = float(a[3]) if len(a) > 3 else 1
            pts.append((o['id'], (x + w / 2 - 0.5, y + h / 2 - 0.5), t))
            continue
        if t == 'deliver' and len(a) >= 3:
            pts.append((o['id'], (float(a[1]) + float(a[3] if len(a) > 3 else 1) / 2 - 0.5,
                                  float(a[2]) + float(a[4] if len(a) > 4 else 1) / 2 - 0.5), t))
            e = m.ent(a[0])
            if e: pts.append((o['id'] + ':target', (e.x, e.y), 'target'))
            continue
        if t == 'escort' and len(a) >= 5:
            nums = [x for x in a if isnum(x)]
            if len(nums) >= 4:
                pts.append((o['id'], (float(nums[0]) + float(nums[2]) / 2 - 0.5, float(nums[1]) + float(nums[3]) / 2 - 0.5), t))
        for x in a:
            e = m.ent(x)
            if e is None or e.kind == 'zone': continue
            if e.kind == 'squad':
                for n in m.npcs():
                    if n.opts.get('squad') == e.id and 'lead' in n.flags:
                        pts.append((o['id'] + ':' + n.id, (n.x, n.y), t))
                continue
            pts.append((o['id'] + ':' + e.id, (e.x, e.y), t))
    return pts


# ====================================================================== topology
def ring_class(m, UG, cell, R=6):
    """Local spatial type at a ground cell: count the connected components of the BFS ring at path distance R..R+1.
    1 small ring = dead end; 2 = corridor; 3+ = junction; 1 large ring = open space."""
    dist = {cell: 0}; q = deque([cell])
    while q:
        c = q.popleft()
        if dist[c] >= R + 1: continue
        for n in UG.neighbors(c):
            if n not in dist:
                dist[n] = dist[c] + 1; q.append(n)
    ring = [c for c, d in dist.items() if d >= R]
    if not ring: return 'pocket', 0
    rs = set(ring); comps = 0; seen = set()
    for c in ring:
        if c in seen: continue
        comps += 1; st = [c]; seen.add(c)
        while st:
            u = st.pop()
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    v = (u[0] + dx, u[1] + dy)
                    if v in rs and v not in seen: seen.add(v); st.append(v)
    if comps == 1:
        return ('open' if len(ring) > 2.2 * R else 'deadend'), comps
    return ('corridor' if comps == 2 else 'junction'), comps


def cross_width(m, S, c):
    """Narrowest straight cross-section through a cell (horizontal or vertical run of walkable cells), in cells."""
    best = 99
    for dx, dy in ((1, 0), (0, 1)):
        n = 1
        x, y = c
        while (x + dx * n, y + dy * n) in S: n += 1
        k = 1
        while (x - dx * k, y - dy * k) in S: k += 1
        best = min(best, n + k - 1)
    return best


def regions(m, UG, S):
    """Split ground into spaces: remove doorways and 1-cell passages (the connectors), take connected components.
    Returns cell->region id, region->cells, region->number of separate connector clusters touching it (its exits)."""
    conn = {c for c in UG if m.k(*c) == 'doorway' or cross_width(m, S, c) == 1}
    reg = {}; cells = {}
    rid = 0
    for c in UG:
        if c in conn or c in reg: continue
        st = [c]; reg[c] = rid; cells[rid] = [c]
        while st:
            u = st.pop()
            for v in UG.neighbors(u):
                if v not in conn and v not in reg:
                    reg[v] = rid; cells[rid].append(v); st.append(v)
        rid += 1
    # connector clusters
    seen = set(); clusters = []
    for c in conn:
        if c in seen: continue
        cl = [c]; seen.add(c); st = [c]
        while st:
            u = st.pop()
            for v in UG.neighbors(u):
                if v in conn and v not in seen: seen.add(v); cl.append(v); st.append(v)
        clusters.append(cl)
    exits = defaultdict(int); nbrs = defaultdict(set)
    for cl in clusters:
        touch = set()
        for u in cl:
            for v in UG.neighbors(u):
                if v in reg: touch.add(reg[v])
        for r in touch:
            exits[r] += 1
            nbrs[r] |= (touch - {r})
    return reg, cells, exits, nbrs, conn


def optimise(m, VG, start, goals, cost_fn):
    """Dijkstra from start to the nearest goal cell; returns path."""
    if start not in VG: return None
    gs = set(goals)
    dist = {start: 0}; prev = {}; pq = [(0, start)]
    while pq:
        d, u = heapq.heappop(pq)
        if u in gs:
            path = [u]
            while u in prev: u = prev[u]; path.append(u)
            return path[::-1], d
        if d > dist[u]: continue
        for v in VG.successors(u):
            nd = d + cost_fn(u, v, VG[u][v].get('how'))
            if nd < dist.get(v, 1e18):
                dist[v] = nd; prev[v] = u; heapq.heappush(pq, (nd, v))
    return None
