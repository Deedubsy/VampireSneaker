from grid import G
g = G(64, 52)
# ---------------------------------------------------------------- the river Vesper all round St Calder's island
g.rect(0, 0, 63, 41, '~')
# ---- the mainland (south): the river path, the Vigil's bridgehead, the jetty, fishermen's houses
g.rect(0, 42, 63, 51, '.')
g.rect(0, 42, 63, 43, ',')                     # the towpath along the bank
g.rect(0, 48, 63, 51, 'h')                     # the waterfront houses
g.put(9, 47, 'p'); g.put(44, 47, 'p')          # pipes up to their roofs
g.rect(18, 48, 20, 51, '.'); g.rect(42, 48, 43, 51, '.')   # lanes between them
g.rect(0, 44, 6, 47, 'b')                      # reeds at the foot of the weir
g.box(25, 44, 29, 47, 'H', '_'); g.put(27, 44, '+') # the bridgehead toll-house
g.rect(55, 39, 56, 43, '=')                    # the Vigil boatman's jetty
# ---------------------------------------------------------------- the island: ledges below the walls
g.rect(4, 8, 5, 30, ',')                       # the west ledge (below the prison: the outfall, the postern)
g.rect(4, 31, 4, 41, '#')                      # the old weir: a wall across the west channel to the bank
g.rect(58, 22, 59, 32, ',')                    # the east ledge (the water-stair, the landing)
# ---- the curtain wall (3 m; a wall-walk all round)
g.box(6, 4, 57, 35, '#', '.')
# ---------------------------------------------------------------- the prison (west)
g.box(7, 5, 19, 24, 'H', ':')
for (y0, y1) in ((6, 9), (11, 14), (16, 19)):  # three cells along the west wall, barred to the corridor
    g.rect(8, y0, 10, y1, ':')
    g.rect(11, y0, 11, y1, '|'); g.put(11, (y0 + y1) // 2, '+')
g.rect(8, 10, 11, 10, 'H'); g.rect(8, 15, 11, 15, 'H'); g.rect(8, 20, 11, 20, 'H')
g.rect(8, 21, 11, 23, ':')                     # the warders' store (the postern opens from it)
g.put(11, 21, '#'); g.put(11, 23, '#'); g.put(11, 22, '+')
g.rect(15, 6, 15, 23, '#')                     # the corridor (x 12..14) and the rooms east of it
g.rect(16, 13, 18, 13, '#')
g.put(15, 9, '+'); g.put(15, 18, '+')          # the guardroom (north) and the question room (south)
g.put(19, 9, '+'); g.put(19, 20, '+')          # doors out to the alley and the bailey
g.put(13, 24, '+')                             # the prison gate (south) onto the bailey
g.put(6, 12, '+'); g.put(7, 12, '+')           # the sewer outfall: a grate in the curtain, a culvert into cell 2
g.put(6, 22, '+'); g.put(7, 22, '+')           # the postern: from the warders' store out onto the west ledge
# the west watchtower (searchlight over the weir) between the prison and the barracks
g.rect(7, 25, 8, 26, 'T'); g.put(9, 26, 's')
# ---------------------------------------------------------------- the keep (north centre)
g.box(22, 5, 41, 19, 'H', ':')
g.rect(23, 6, 40, 10, 'u')                     # the upper floor: Vane's study (west) and the archive (east)
g.rect(30, 6, 30, 10, 'H'); g.put(30, 8, 'u')  # the study door
g.rect(23, 11, 40, 11, 'H'); g.put(31, 11, 's')# the stair from the great hall up to the archive
g.put(35, 5, 'u')                              # the river door: the archive opens onto the north wall-walk
g.rect(28, 12, 28, 18, '#'); g.put(28, 15, '+')# the lamp room (the sunstone generator) west of the hall
g.rect(33, 12, 33, 18, '#'); g.put(33, 15, '+')# the chapel east of the hall
g.rect(34, 12, 40, 18, 'c'); g.rect(36, 12, 38, 12, '_')
g.rect(30, 19, 31, 19, '+'); g.put(37, 19, '+'); g.put(25, 19, '+')
g.rect(40, 5, 41, 6, 'T')                      # the keep tower (searchlight over the bailey and the landing)
# ---------------------------------------------------------------- the cloister (east)
g.box(43, 5, 56, 19, 'H', '_')
g.rect(47, 9, 52, 15, 'g')                     # the garth
g.rect(44, 8, 46, 8, '#'); g.rect(53, 8, 55, 8, '#')   # the scriptorium (north range) behind the arcade
g.rect(44, 16, 55, 16, '#'); g.put(46, 16, '+'); g.put(53, 16, '+')   # the dormitory (south range)
g.put(49, 19, '+'); g.put(43, 12, '+')
# ---------------------------------------------------------------- the bailey
g.rect(20, 5, 21, 19, ',')                     # the alley between the prison and the keep
g.rect(42, 5, 42, 19, ',')                     # the alley between the keep and the cloister
g.box(9, 27, 19, 34, 'H', '_')                 # the barracks
g.put(19, 30, '+'); g.put(14, 27, '+')
g.rect(13, 27, 13, 34, 'H'); g.rect(10, 31, 12, 31, 'H'); g.put(13, 29, '+')   # the sergeant's room (west)
g.box(44, 22, 49, 27, 'H', '_'); g.put(46, 27, '+'); g.put(49, 24, '+')   # the stores
g.box(51, 29, 56, 34, 'H', ','); g.put(51, 31, '+')                   # the kennels
g.put(57, 27, '+')                             # the water-stair gate onto the east ledge
g.rect(52, 22, 56, 22, 'x')                    # stacked casks by the water-stair
for (x, y) in ((24, 23), (39, 23)): g.put(x, y, 'x')
g.put(7, 34, 's'); g.put(56, 21, 's'); g.put(21, 34, 's'); g.put(42, 34, 's')   # stairs up to the wall-walk
# ---------------------------------------------------------------- the gatehouse and the causeway (south)
g.box(27, 31, 36, 37, 'H', '_')
g.rect(30, 32, 30, 36, '#'); g.rect(32, 32, 32, 36, '#')
g.rect(31, 31, 31, 37, ':'); g.put(31, 31, '+'); g.put(31, 37, '+')
g.put(30, 34, '+'); g.put(32, 34, '+')
g.rect(31, 38, 31, 41, '=')                    # the causeway
g.rect(30, 42, 32, 42, '=')
if __name__ == '__main__':
    g.show()
    open('m12.grid', 'w').write(g.text())
