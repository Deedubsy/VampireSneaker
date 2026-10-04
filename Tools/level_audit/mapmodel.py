"""Offline model of a Vespertine mission map, built from the same rules the game uses.

Sources mirrored here (keep in step if they change):
  Level/MapParser.cs      - file format, tokenising, entity schema, routes
  Level/TileDefs.cs       - tile kinds, heights, floors, raised tops, vision blockers
  Level/NavBuilder.cs     - vampire links: pipes/stairs, drops, ClimbAny (A4), leaps (A3, 1-2 cell gaps), mist
  Player/Vampire.cs       - area mask by Awakening (ClimbAny A4, Leap A3)
  Stealth/GameLight.cs    - light kinds: radius, intensity, height
  Stealth/LightSystem.cs  - ambient + brightest source, wall Linecast (moon unoccluded), sampled 1 m above the feet
  Stealth/DetectionMath   - near/far bands, 2.5 m height rule, LooksUp, 1.4 m touch zone, 0.35 threshold
  Data/Archetypes.cs      - fov / near / far / flags per archetype
Cells are 2 m. Coordinates are entity coordinates: x = column, y = row (row 0 = north). A cell's centre is (x, y).
"""
import math, re, os
from collections import defaultdict, deque

CS = 2.0

# ------------------------------------------------------------------ tiles
TILES = {
    # kind: (height, floor, raised, blocks_vision, blocks_move, water)
    'void': (0, False, False, True, False, False),
    'street': (0, True, False, False, False, False),
    'dirt': (0, True, False, False, False, False),
    'grass': (0, True, False, False, False, False),
    'wood': (0, True, False, False, False, False),
    'tile': (0, True, False, False, False, False),
    'carpet': (0, True, False, False, False, False),
    'shallow': (0, True, False, False, False, False),
    'canal': (0, False, False, False, False, True),
    'bridge': (0, True, False, False, False, False),
    'doorway': (0, True, False, False, False, False),
    'stairs': (0, True, False, False, False, False),
    'pipe': (0, True, False, False, False, False),
    'hedge': (0, True, False, True, False, False),
    'wall': (3, False, True, True, False, False),
    'pipeup': (3, False, True, True, False, False),
    'stairsup': (3, False, True, True, False, False),
    'gallery': (3, False, True, True, False, False),
    'house': (4.5, False, True, True, False, False),
    'building': (6, False, True, True, False, False),
    'tower': (9, False, True, True, False, False),
    'bars': (2.5, False, False, False, True, False),
    'vent': (3, False, True, True, False, False),
    'crates': (1.2, False, False, False, True, False),
}
LEGEND = {' ': 'void', '.': 'street', ',': 'dirt', '_': 'wood', ':': 'tile', 'w': 'shallow', '~': 'canal',
          '=': 'bridge', '#': 'wall', 'u': 'gallery', 'h': 'house', 'H': 'building', 'T': 'tower', '|': 'bars',
          'v': 'vent', 'b': 'hedge', 'x': 'crates', '+': 'doorway', 's': 'stairs', 'p': 'pipe', 'P': 'pipeup',
          'S': 'stairsup', 'g': 'grass', 'c': 'carpet'}

# ------------------------------------------------------------------ lights (GameLight.Setup)
LIGHTS = {
    'gaslamp': (7, 1.0, 3.2), 'walllamp': (6, 1.0, 2.6), 'lantern': (4.5, 0.9, 1.1), 'brazier': (6.5, 1.1, 1.3),
    'candle': (3.5, 0.8, 1.2), 'chandelier': (9, 1.0, 4.5), 'sunstone': (8, 1.3, 3.4), 'window': (4, 0.7, 2.2),
    'moon': (5, 0.55, 12.0), 'holy': (5, 1.0, 1.4), 'fire': (8, 1.2, 1.0), 'searchlight': (4.6, 1.5, 2.4),
    'sunbeam': (3.4, 1.6, 1.6),
}
LIGHT_ALIAS = {'lamp': 'gaslamp', 'sconce': 'walllamp', 'candles': 'candle', 'moonbeam': 'moon', 'votive': 'holy',
               'bonfire': 'fire', 'dawn': 'sunbeam'}
BURNS = {'sunstone', 'holy', 'sunbeam'}
EXPOSED = 0.35

# ------------------------------------------------------------------ archetypes (fov, near, far, flags)
ARCH = {
    'civilian': (100, 4, 10, ''), 'drunk': (80, 3, 7, ''), 'sailor': (90, 4, 9, ''), 'beggar': (90, 4, 9, ''),
    'orderly': (90, 5, 13, 'L'), 'watchman': (90, 6, 15, 'L'), 'sergeant': (100, 6, 16, 'LO'),
    'lamplighter': (90, 5, 12, 'LR'), 'priest': (100, 6, 14, 'A'), 'acolyte': (90, 5, 12, 'L'),
    'guest': (100, 4, 11, ''), 'servant': (100, 5, 12, ''), 'soldier': (90, 7, 17, ''),
    'hunter': (90, 7, 18, 'U'), 'hound': (360, 5, 5, 'M'), 'tracker': (100, 8, 20, 'UMO'),
    'alchemist': (90, 6, 14, 'C'), 'inquisitor': (100, 7, 18, 'UOW'), 'bulwark': (80, 6, 14, 'L'),
    'sentry': (100, 7, 18, 'UL'), 'notable': (90, 6, 14, ''), 'scholar': (90, 4, 11, ''),
    'engineer': (90, 5, 13, 'LR'), 'worker': (90, 4, 11, ''), 'fledgling': (60, 3, 6, ''),
    'saule': (100, 7, 16, 'WCLO'), 'vane': (110, 8, 22, 'UOWA'),
}
CIVILIAN = {'civilian', 'drunk', 'sailor', 'beggar', 'guest', 'servant', 'notable', 'scholar', 'worker'}
VIGIL = {'hunter', 'hound', 'tracker', 'inquisitor', 'bulwark', 'vane'}
FACING = {'N': 0, 'NE': 45, 'E': 90, 'SE': 135, 'S': 180, 'SW': 225, 'W': 270, 'NW': 315}

# Expected Awakening at the start of each night (PROGRESSION.md, CampaignEconomy simulation)
AWAKEN_TYPICAL = {'m01': 1, 'm02': 2, 'm03': 3, 'm04': 4, 'm05': 5, 'm06': 5, 'm07': 6, 'm08': 7, 'm09': 8,
                  'm10': 8, 'm11': 8, 'm12': 9, 'm13': 9, 'm14': 10}
AWAKEN_GHOST = {'m01': 1, 'm02': 2, 'm03': 2, 'm04': 3, 'm05': 3, 'm06': 4, 'm07': 5, 'm08': 5, 'm09': 6,
                'm10': 6, 'm11': 7, 'm12': 7, 'm13': 8, 'm14': 8}


def tokenize(s):
    out, cur, q = [], '', False
    for ch in s:
        if ch == '"':
            q = not q; cur += ch; continue
        if not q and ch.isspace():
            if cur: out.append(cur); cur = ''
            continue
        cur += ch
    if cur: out.append(cur)
    return out


def unq(s):
    return s[1:-1] if len(s) >= 2 and s[0] == '"' and s[-1] == '"' else s


def isnum(s):
    try:
        float(s); return True
    except ValueError:
        return False


SCHEMA = {'player': ['x', 'y'], 'npc': ['id', 'type', 'x', 'y'], 'light': ['id', 'type', 'x', 'y'],
          'prop': ['type', 'x', 'y'], 'hide': ['id', 'x', 'y'], 'door': ['id', 'x', 'y'], 'valve': ['id', 'x', 'y'],
          'generator': ['id', 'x', 'y'], 'use': ['id', 'x', 'y'], 'lever': ['id', 'x', 'y'], 'gate': ['id', 'x', 'y'],
          'note': ['id', 'x', 'y'], 'secret': ['id', 'x', 'y'], 'item': ['id', 'type', 'x', 'y'],
          'bell': ['id', 'x', 'y'], 'spawn': ['id', 'x', 'y'], 'zone': ['id', 'x', 'y'], 'boat': ['id', 'x', 'y'],
          'window': ['id', 'x', 'y'], 'listen': ['id', 'x', 'y'], 'trap': ['id', 'x', 'y'],
          'squad': ['id', 'x', 'y'], 'stain': ['x', 'y'], 'corpse': ['id', 'x', 'y']}

BLOCKING_PROPS = {'crate', 'barrel', 'well', 'bed', 'bed_shroud', 'slab', 'gurney', 'slab_shroud', 'slab_open',
                  'gurney_shroud', 'table', 'desk', 'shelf', 'bookshelf', 'cabinet', 'wardrobe', 'cart', 'privy',
                  'coffin', 'tub', 'pew', 'bench', 'altar', 'pillar', 'statue', 'tree', 'sacks', 'cage', 'chest',
                  'gravestone', 'fountain', 'body_pile', 'lampstand', 'pipes', 'machine', 'scenery', 'abbess', 'vat',
                  'still'}


class Entity:
    def __init__(self, kind):
        self.kind = kind; self.id = None; self.type = None; self.x = 0.0; self.y = 0.0
        self.opts = {}; self.flags = set(); self.args = []; self.group = None; self.line = 0

    def has(self, f):
        return f in self.flags or f in self.opts

    @property
    def cell(self):
        return (int(math.floor(self.x + 0.5)), int(math.floor(self.y + 0.5)))

    def __repr__(self):
        return f'{self.kind}:{self.id}({self.type})@{self.x},{self.y}'


class Mission:
    def __init__(self, path):
        self.path = path
        self.mid = os.path.splitext(os.path.basename(path))[0]
        self.header = {}; self.rows = []; self.legend = dict(LEGEND); self.ents = []; self.routes = {}
        self.objectives = []; self.script = []; self.comments = []
        self.active_groups = set()
        self._parse(open(path, encoding='utf-8').read())
        self._grid()

    # ---------------------------------------------------------------- parse
    def _parse(self, text):
        sec = None; group = None
        lines = text.replace('\r\n', '\n').split('\n')
        i = 0
        while i < len(lines):
            raw = lines[i]; i += 1
            if raw.startswith('#!'):
                self.comments.append((sec, raw[2:].strip())); continue
            t = raw.strip()
            if t.startswith('@'):
                sec = t[1:].strip().lower(); continue
            if sec == 'map':
                k = raw.find('#!')
                self.rows.append((raw[:k] if k >= 0 else raw).rstrip()); continue
            k = raw.find('#!')
            line = (raw[:k] if k >= 0 else raw).strip()
            if not line: continue
            if sec == 'mission':
                if '=' not in line: continue
                key, val = line.split('=', 1); key = key.strip().lower(); val = val.strip()
                if val == '|':
                    buf = []
                    while i < len(lines) and lines[i].strip() != '|':
                        buf.append(lines[i].strip()); i += 1
                    i += 1; val = '\n'.join(buf)
                self.header[key] = val
            elif sec == 'legend':
                self.legend[line[0]] = line[2:].replace('=', '').strip().lower()
            elif sec == 'entities':
                tk = tokenize(line)
                kind = tk[0].lower()
                if kind == 'group': group = tk[1] if len(tk) > 1 else None; continue
                if kind == 'endgroup': group = None; continue
                if kind == 'route': self._route(line); continue
                if kind not in SCHEMA: continue
                e = Entity(kind); e.group = group; pos = 0
                for tok in tk[1:]:
                    eq = tok.find('=')
                    if eq > 0 and not tok.startswith('"'):
                        e.opts[tok[:eq].lower()] = unq(tok[eq + 1:]); continue
                    sc = SCHEMA[kind]
                    if pos < len(sc):
                        f = sc[pos]
                        if f == 'id': e.id = tok
                        elif f == 'type': e.type = tok.lower()
                        elif f == 'x': e.x = float(tok)
                        elif f == 'y': e.y = float(tok)
                        pos += 1; continue
                    if tok.startswith('"'): e.args.append(unq(tok))
                    elif isnum(tok): e.args.append(tok)
                    else: e.flags.add(tok.lower())
                if e.id is None: e.id = f'{kind}_{e.type}@{e.x},{e.y}'
                self.ents.append(e)
            elif sec == 'objectives':
                tk = tokenize(line)
                o = {'cat': tk[0].lower(), 'id': tk[1], 'text': unq(tk[2]), 'type': tk[3].lower(), 'args': [],
                     'opts': {}}
                for t2 in tk[4:]:
                    eq = t2.find('=')
                    if eq > 0 and not t2.startswith('"'): o['opts'][t2[:eq].lower()] = unq(t2[eq + 1:])
                    else: o['args'].append(unq(t2))
                self.objectives.append(o)
            elif sec == 'script':
                self.script.append(line)

    def _route(self, line):
        parts = line.split('|')
        head = tokenize(parts[0])
        mode = head[2].lower() if len(head) > 2 else 'loop'
        pts = []
        for p in parts[1:]:
            tk = tokenize(p)
            if len(tk) < 2: continue
            wp = {'x': float(tk[0]), 'y': float(tk[1]), 'wait': 0.0, 'look': None}
            for t in tk[2:]:
                k, v = t.split('=')
                if k == 'wait': wp['wait'] = float(v)
                elif k == 'look': wp['look'] = FACING.get(v.upper())
            pts.append(wp)
        self.routes[head[1]] = {'mode': mode, 'pts': pts}

    # ---------------------------------------------------------------- grid
    def _grid(self):
        while self.rows and not self.rows[-1]: self.rows.pop()
        self.W = max(len(r) for r in self.rows); self.H = len(self.rows)
        self.kind = [[self.legend.get((r.ljust(self.W))[x], 'void') for x in range(self.W)] for r in self.rows]
        self.ch = [[(r.ljust(self.W))[x] for x in range(self.W)] for r in self.rows]
        self.doors = {}
        for e in self.ents:
            if e.kind == 'door' and not e.group:
                self.doors[e.cell] = e
        # threshold zones (home=): cells the vampire can't enter uninvited
        self.home_cells = set()
        for e in self.ents:
            if e.kind == 'zone' and 'home' in e.opts:
                w = int(float(e.opts.get('w', e.args[0] if e.args else 1))); h = int(float(e.opts.get('h', e.args[1] if len(e.args) > 1 else 1)))
                for yy in range(int(e.y), int(e.y) + h):
                    for xx in range(int(e.x), int(e.x) + w):
                        self.home_cells.add((xx, yy))
        # gates start shut (lever / valve / windlass / script raise them)
        self.gates = {e.cell for e in self.ents if e.kind == 'gate'}
        self.blockprops = defaultdict(list)
        for e in self.ents:
            if e.kind == 'prop' and e.type in BLOCKING_PROPS and not e.group:
                self.blockprops[e.cell].append(e.type)

    def inb(self, x, y): return 0 <= x < self.W and 0 <= y < self.H
    def k(self, x, y): return self.kind[y][x] if self.inb(x, y) else 'void'
    def t(self, x, y): return TILES[self.k(x, y)]
    def floor(self, x, y): return self.t(x, y)[1]
    def raised(self, x, y): return self.t(x, y)[2]
    def top(self, x, y): return self.t(x, y)[0] if self.raised(x, y) else 0.0
    def walktop(self, x, y): return self.floor(x, y) or self.raised(x, y)
    def blocksmove(self, x, y): return self.t(x, y)[4] or (x, y) in self.gates

    def active(self, e):
        """Entities in a group are dormant until a script switches the group on."""
        return e.group is None or e.group in self.active_groups

    def npcs(self, dormant=False):
        return [e for e in self.ents if e.kind == 'npc' and (dormant or self.active(e))]

    def lights(self):
        return [e for e in self.ents if e.kind == 'light' and self.active(e)]

    def light_kind(self, e):
        k = LIGHT_ALIAS.get(e.type, e.type)
        return k if k in LIGHTS else 'gaslamp'

    def light_params(self, e):
        k = self.light_kind(e)
        r, i, h = LIGHTS[k]
        r = float(e.opts.get('radius', r)); i = float(e.opts.get('intensity', i))
        return k, r, i, h

    @property
    def ambient(self):
        return float(self.header.get('ambient', 0.06))

    @property
    def player(self):
        return next(e for e in self.ents if e.kind == 'player')

    def ent(self, eid):
        for e in self.ents:
            if e.id == eid: return e
        return None
