from grid import G
g = G(72, 62)
# ================================================================ the cathedral close (north)
g.rect(0, 0, 71, 16, ':')                      # the close: flagstones
g.rect(0, 0, 71, 0, 'H')                       # the city behind it
g.box(26, 1, 45, 9, 'T', 'T')                  # St Vesper's: nine metres of black stone
g.rect(32, 10, 39, 10, '_')                    # the west steps
g.rect(1, 1, 22, 7, 'H')                       # the Bishop's palace (west)
g.rect(49, 1, 70, 7, 'H')                      # the chapter house (east)
g.put(10, 7, '+'); g.put(60, 7, '+')
g.rect(3, 9, 21, 14, 'g')                      # the churchyards
g.rect(50, 9, 68, 14, 'g')
g.rect(0, 1, 0, 16, 'h'); g.rect(71, 1, 71, 16, 'h')
g.put(23, 4, 'P'); g.put(48, 4, 'P')           # downpipes onto the palace and chapter-house roofs
# ---- the close wall (3 m) and its gate
g.rect(0, 17, 71, 17, '#')
g.put(35, 17, '+')                             # the close gate
g.put(58, 18, 's'); g.put(13, 18, 's')         # stairs up onto the wall from Lantern Street
# ================================================================ the market district (middle)
g.rect(0, 18, 71, 19, '.')                     # Lantern Street, under the close wall
g.rect(0, 20, 71, 38, 'h')                     # the blocks
# ---- the market square
g.rect(24, 22, 47, 34, '.')
g.rect(35, 20, 36, 21, '.')                    # Bell Lane north into Lantern Street
g.rect(34, 35, 37, 38, '.')                    # Bell Lane south to the bridge
g.rect(44, 21, 45, 22, 'T'); g.put(46, 22, 's')# the searchlight tower in the square's north-east corner
g.rect(22, 20, 23, 38, ',')                    # Rope Walk (west of the square)
# ---- the west blocks: Pewter Alley, the dyer's yard and a family behind a locked door
g.rect(0, 28, 21, 28, ',')                     # Pewter Alley
g.rect(11, 20, 11, 38, ',')                    # Cutler's Passage
g.box(3, 21, 9, 26, 'h', '_'); g.put(9, 24, '+'); g.put(10, 24, ',')   # the Hallorans' house (they bar the door)
g.box(13, 30, 20, 36, 'h', ','); g.put(13, 33, '+'); g.put(12, 33, ','); g.put(17, 30, '+'); g.put(17, 29, ',')   # the dyer's yard
g.put(10, 31, 'p'); g.put(21, 25, 'p')         # pipes up onto the roofs
for x, y in ((7, 29), (19, 27), (12, 37), (66, 34)): g.put(x, y, ',')   # doorways and nooks off the alleys (crates)
# ---- the east blocks: the Watch-house and Tanner Row
g.rect(48, 20, 50, 38, '.')                    # Tanner Row
g.box(52, 21, 63, 30, 'H', ':')                # the Watch-house
g.rect(52, 25, 63, 25, 'H'); g.put(57, 25, '+')# the guardroom (north) / the cells and sergeant's room (south)
g.rect(58, 26, 58, 29, '|'); g.put(58, 27, '+')# the lock-up
g.put(52, 23, '+'); g.put(52, 28, '+'); g.put(51, 23, '.'); g.put(51, 28, '.')         # doors onto Tanner Row
g.rect(51, 33, 71, 33, ',')                    # Tallow Lane
g.rect(64, 20, 64, 38, ',')                    # Gutter Steps
g.put(51, 31, 'p'); g.put(65, 26, 'p')
# ---- Vigil barricades across the streets (crates: the militia close the ways the squads don't walk)
g.put(5, 18, 'x')                              # Lantern Street, west end (half-closed)
g.put(68, 18, 'x')                             # Lantern Street, east end
g.put(22, 32, 'x'); g.put(23, 32, 'x')         # Rope Walk
# ================================================================ the Mercy canal
g.rect(0, 38, 71, 39, '.')                     # the north quay
g.rect(0, 40, 71, 42, '~')
g.rect(0, 43, 71, 43, '.')                     # the south quay
g.rect(9, 40, 11, 42, '=')                     # Weavers' Bridge (west): a checkpoint
g.put(9, 41, 'x'); g.put(10, 41, 'x')          # ...barricaded but for one gap
g.rect(34, 40, 37, 42, '=')                    # Mercy Bridge (the main crossing)
g.rect(60, 40, 62, 42, '=')                    # Lock Bridge (east)
# ================================================================ the Wick (south)
g.rect(0, 44, 71, 61, 'h')
g.rect(0, 44, 71, 45, ',')                     # Quay Lane
g.rect(1, 55, 8, 60, ',')                      # the yard where she starts
g.rect(4, 46, 5, 54, ',')                      # Coffin Lane north to the quay
g.rect(5, 50, 30, 51, ',')                     # Sallow Street
g.rect(16, 46, 17, 61, ',')                    # Chandler's Lane
g.rect(29, 46, 30, 61, ',')
g.rect(17, 57, 44, 58, ',')                    # Low Street
g.box(19, 52, 27, 56, 'h', '_'); g.put(23, 52, '+')   # the Marrow house: her mother's, shut up since the plague
g.put(15, 53, 'p'); g.put(31, 48, 'p'); g.put(9, 54, 'p')
# ---- Candle Row: the Faithful's barricade (east)
g.rect(40, 46, 41, 56, ',')                    # Lamp Alley
g.rect(41, 46, 63, 47, ',')                    # Candle Row's mouth onto Quay Lane
g.rect(46, 48, 58, 56, '_')                    # Candle Row: the Faithful's yard
g.rect(46, 48, 58, 48, 'x'); g.put(52, 48, '+')# their barricade: carts, doors, a pew (a gap they hold with a door)
g.put(45, 52, '+')                             # the back way from Lamp Alley (they keep it shut)
g.rect(42, 52, 44, 52, ',')
g.rect(60, 48, 62, 56, ',')                    # Ferry Steps
g.put(59, 54, 'p')
if __name__ == '__main__':
    g.show()
    open('m13.grid', 'w').write(g.text())
