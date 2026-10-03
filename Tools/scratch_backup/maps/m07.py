from grid import G
g = G(76, 48)
# ---------------------------------------------------------------- bases
g.rect(0, 0, 17, 47, ',')                # Tanners' Row (west bank): beaten dirt
g.rect(20, 0, 41, 47, '.')               # Chandlers' Island: cobbles
g.rect(49, 0, 58, 47, '.')               # Ostbank wharf (east bank)
g.rect(62, 0, 75, 47, 'g')               # the fens
g.rect(18, 0, 19, 47, '~')               # the Mill Leat
g.rect(42, 0, 48, 47, '~')               # the River Ost
g.rect(59, 0, 61, 47, '~')               # the Fen Cut
# boundaries (never across water)
g.rect(0, 0, 0, 47, 'H')
for x0, x1 in ((0, 17), (20, 41), (49, 58)):
    g.rect(x0, 0, x1, 0, 'H'); g.rect(x0, 47, x1, 47, 'H')
g.rect(62, 0, 75, 1, 'b'); g.rect(62, 46, 75, 47, 'b')

# ================================================================ WEST BANK — Tanners' Row
g.rect(1, 1, 7, 7, 'H')                  # tannery (north)
g.rect(9, 1, 11, 46, '.')                # Tanners' Row (the street)
# the mill straddles the leat: a ground-floor crossing through the wheel room
g.box(13, 2, 23, 9, '#', '_')
g.rect(17, 3, 17, 8, '#'); g.put(17, 5, '+')       # partition: grinding floor | wheel room
g.rect(20, 3, 20, 8, '#'); g.put(20, 7, '+')       # wheel room | sack store
g.put(13, 6, '+')                        # west door (Tanners' Row)
g.put(23, 4, '+')                        # east door (the island)
g.put(15, 3, 'x'); g.put(22, 8, 'x')
# tanning pits
g.rect(1, 9, 7, 17, ',')
for (x, y) in ((2, 10), (5, 10), (2, 13), (5, 13)): g.rect(x, y, x + 1, y + 1, 'w')
g.rect(1, 16, 3, 17, 'x')
# drying sheds: low roofs, the start of the roof road across the leat
g.rect(12, 11, 16, 15, 'h')
g.put(11, 14, 'p')                       # downpipe from the street
# leat houses: their upper storeys span the leat on both sides of the bridge
g.rect(15, 17, 22, 20, 'h')
g.rect(15, 24, 22, 27, 'h')
g.put(14, 26, 'p')                       # downpipe in the back lane (south houses)
# the road and the Leat Bridge
g.rect(1, 21, 17, 23, '.')
g.rect(18, 21, 19, 23, '=')
g.put(15, 21, 'x'); g.put(15, 23, 'x')   # Watch barricade, one gap
# south of the road
g.rect(1, 25, 7, 32, 'H')                # the limeyard works
g.rect(12, 29, 17, 35, 'H')              # Tanners' Guildhall
g.put(11, 31, 'p')
g.rect(1, 35, 7, 39, 'h')                # tanners' cottages
g.rect(2, 41, 4, 41, 'x'); g.put(7, 44, 'x'); g.put(6, 45, 'x')   # start yard
g.rect(13, 38, 16, 42, 'h')              # skin store
g.rect(14, 44, 16, 45, 'x')

# ================================================================ CHANDLERS' ISLAND
g.rect(25, 1, 31, 6, 'H')                # the chandlery
g.rect(32, 1, 41, 3, ',')                # tallow yard
g.rect(33, 1, 34, 2, 'x'); g.put(39, 1, 'x')
g.box(35, 4, 41, 8, '#', ':')            # the tollhouse
g.put(38, 8, '+')
g.put(32, 6, 'p')                        # downpipe -> chandlery roof
g.rect(22, 11, 28, 14, 'h')              # Wick Lane houses
g.rect(22, 16, 28, 19, 'h')
g.put(29, 18, 'p')
# the chapel of Saint Ide (holy light; its walls make a second climb)
g.box(31, 15, 38, 19, '#', ':')
g.put(34, 15, '+')
g.rect(35, 16, 35, 16, ':')
# the Wick (a tavern, shut for the curfew, not empty)
g.box(23, 25, 31, 30, '#', '_')
g.put(27, 25, '+'); g.put(31, 28, '+')
g.rect(28, 26, 30, 26, 'x')
g.rect(34, 25, 41, 29, 'h')              # quay houses
g.put(33, 27, 'p')
g.rect(21, 34, 29, 40, 'H')              # salt warehouse
g.put(30, 37, 'p')
# the ferry stage
g.rect(36, 34, 41, 39, ',')
g.rect(42, 35, 43, 37, '=')              # jetty (island)
g.rect(47, 35, 48, 37, '=')              # jetty (wharf)
g.rect(37, 34, 38, 34, 'x')
g.box(31, 41, 36, 45, '#', '_')          # Watch house
g.put(33, 41, '+')
g.rect(38, 42, 41, 45, 'h')

# ================================================================ THE TOLL BRIDGE
g.rect(42, 10, 48, 12, '=')
g.put(43, 10, '#'); g.put(43, 12, '#')   # the toll arch (gate between)

# ================================================================ OSTBANK WHARF
g.rect(50, 1, 57, 3, 'H')                # cooperage
g.rect(49, 4, 58, 6, '.')                # Lock Lane
g.rect(59, 5, 61, 5, '=')                # lock-gate catwalk
g.put(51, 10, 'x'); g.put(51, 12, 'x')   # Vigil barricade at the bridge foot
g.rect(50, 8, 51, 8, 'x')
g.rect(53, 7, 58, 8, 'H')                # salt store
g.rect(53, 14, 58, 22, 'H')              # bonded warehouse
g.put(52, 18, 'p')
g.box(52, 24, 57, 28, '#', '_')          # Vigil guard hut
g.put(55, 28, '+')
g.rect(49, 30, 58, 32, '.')              # the causeway road
g.rect(59, 30, 61, 32, '=')              # the Fen Cut causeway
# the Faithful's pen (bars)
g.box(51, 34, 56, 37, '|', ',')
g.put(51, 35, ',')                       # the pen gate
g.rect(50, 40, 58, 46, 'H')

# ================================================================ THE FENS
g.rect(62, 30, 75, 32, ',')              # the fen road
g.rect(62, 2, 65, 4, 'h')                # lock-keeper's cottage
g.rect(62, 5, 66, 6, ',')
g.rect(66, 5, 66, 29, ',')               # the dyke path
g.put(64, 30, 'x'); g.put(64, 32, 'x')   # Vigil barricade at the causeway foot
for (x0, y0, x1, y1) in ((67, 9, 71, 17), (62, 12, 64, 26), (69, 20, 74, 27), (67, 35, 73, 42), (62, 36, 65, 44), (70, 3, 74, 7)):
    g.rect(x0, y0, x1, y1, 'b')
for (x0, y0, x1, y1) in ((68, 4, 69, 6), (70, 12, 71, 14), (63, 40, 64, 42), (71, 22, 72, 24)):
    g.rect(x0, y0, x1, y1, 'w')
g.rect(67, 22, 75, 22, 'g')              # a gap through the reeds
open('m07.grid', 'w').write(g.text())
g.show()
