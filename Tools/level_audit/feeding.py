"""Feeding geometry: drag pockets near posts, and drop-feed ledges along patrols."""
import os, json, math
from analyse import *
from report import MISSIONS, OUT, analyse_mission
out = {}
for i in [f'm{n:02d}' for n in range(1, 15)]:
    m, met, D = analyse_mission(os.path.join(MISSIONS, i + '.txt'))
    field, static, patrol = D['field'], D['static'], D['patrol']
    HG = human_graph(m)
    posts = [e for e in m.npcs() if not e.opts.get('route') and 'friendly' not in e.flags and 'prisoner' not in e.flags
             and e.type not in ('hound', 'fledgling')]
    drag_ok = 0; post_lit = 0
    for e in posts:
        cx, cy = e.cell
        if field.get((cx, cy), 0) >= EXPOSED: post_lit += 1
        found = False
        for dx in range(-3, 4):
            for dy in range(-3, 4):
                c = (cx + dx, cy + dy)
                d = math.hypot(dx, dy) * CS
                if 2 <= d <= 6 and m.floor(*c) and field.get(c, 0) < EXPOSED and c not in static and patrol.get(c, 0) < 0.1:
                    found = True
        drag_ok += found
    # ledges along patrol routes (cells a patroller walks that touch a 3-4.5 m raised walk top)
    walk = 0; ledge = 0; dark_ledge = 0
    for e in m.npcs():
        if not e.opts.get('route') or 'friendly' in e.flags: continue
        poses, st = route_poses(m, e, HG)
        for (px, py, f, w) in poses:
            c = (int(math.floor(px + .5)), int(math.floor(py + .5)))
            walk += 1
            if any(m.raised(c[0] + dx, c[1] + dy) and 2.9 <= m.top(c[0] + dx, c[1] + dy) <= 6.1 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                ledge += 1
                if field.get(c, 0) < EXPOSED: dark_ledge += 1
    out[i] = {'posts': len(posts), 'posts_lit_pct': round(100 * post_lit / max(1, len(posts))),
              'posts_with_drag_pocket_pct': round(100 * drag_ok / max(1, len(posts))),
              'patrol_cells_under_ledge_pct': round(100 * ledge / max(1, walk)),
              'patrol_cells_under_dark_ledge_pct': round(100 * dark_ledge / max(1, walk))}
    print(i, out[i])
json.dump(out, open(os.path.join(OUT, 'feeding.json'), 'w'), indent=1)
