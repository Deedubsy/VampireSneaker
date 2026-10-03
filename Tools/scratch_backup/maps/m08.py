from grid import G
g = G(64, 44)
g.rect(0, 0, 63, 43, 'g')                    # fen meadow
# ---------------------------------------------------------------- the Mere all round (running water: no way out)
g.rect(0, 0, 63, 0, '~'); g.rect(0, 43, 63, 43, '~'); g.rect(0, 0, 0, 43, '~'); g.rect(63, 0, 63, 43, '~')

# ================================================================ NORTH — the flood dyke
g.rect(1, 1, 43, 3, ',')                     # the dyke path
g.rect(1, 4, 43, 5, 'b')                     # reeds below the bank
g.rect(8, 4, 8, 5, ','); g.rect(30, 4, 31, 5, ',')   # ways down
g.put(19, 2, 'x'); g.put(20, 2, 'x')         # a fallen sluice-keeper's shed

# ================================================================ WEST — reed beds and peat cuttings
g.rect(1, 6, 16, 32, 'b')
g.rect(8, 6, 8, 34, ',')                     # the eel path, north-south
g.rect(1, 13, 17, 13, ',')                   # the peat path, east to the village
g.rect(8, 27, 17, 27, ',')                   # the reed path
g.rect(3, 9, 5, 11, 'w'); g.rect(3, 19, 5, 22, 'w'); g.rect(12, 8, 14, 10, 'w')   # peat pools
g.box(11, 16, 15, 21, '#', '_'); g.put(11, 19, '+')   # the peat store (lamp oil)
g.rect(16, 6, 16, 32, '~')                   # the peat drain: it runs fast after rain
g.put(16, 27, '=')                            # the plank bridge (the peat path ends at the drain since the flood)
g.rect(10, 15, 10, 22, ',')                  # a beaten way round it

# ================================================================ SOUTH-WEST — the causeway in, Tamsin's hut
g.rect(1, 33, 16, 42, ',')
g.box(3, 35, 7, 39, '#', '_'); g.put(7, 37, '+')      # Old Tamsin's hut
g.put(10, 36, 'x'); g.put(11, 36, 'x'); g.put(13, 39, 'x'); g.put(14, 34, 'x')   # peat stacks
g.rect(1, 33, 6, 33, 'b')

# ================================================================ THE DROWNED VILLAGE
g.rect(17, 6, 43, 42, 'w')
# St Aud's: the nave and its tower
g.box(22, 7, 34, 14, '#', '_'); g.put(28, 14, '+'); g.put(34, 10, '+')
g.rect(19, 8, 21, 11, 'T'); g.put(18, 10, 'p')   # ivy on the tower's west face
g.rect(22, 15, 34, 17, 'g')                  # the churchyard, barely above water
# cottages, roofs a leap apart (ivy on the corners)
for (x0, y0, x1, y1) in ((19, 19, 21, 21), (23, 19, 25, 21), (27, 19, 29, 21),
                         (19, 24, 21, 26), (23, 24, 26, 26),
                         (36, 8, 38, 10), (39, 12, 41, 14),
                         (36, 28, 38, 30), (39, 32, 41, 34),
                         (19, 30, 21, 32), (23, 31, 26, 33)):
    g.rect(x0, y0, x1, y1, 'h')
for (x, y) in ((18, 21), (22, 26), (35, 10), (42, 13), (35, 30), (42, 34), (18, 31), (27, 33)): g.put(x, y, 'p')
# the Marrow house: the one with a candle in the window
g.box(29, 25, 34, 30, '#', '_'); g.put(31, 30, '+'); g.rect(30, 26, 31, 27, 'u'); g.put(32, 27, 's')
# the green and its market cross
g.rect(35, 18, 42, 24, 'g')
g.rect(17, 36, 43, 42, 'g')                  # the drowned orchard (dry again, mostly)
g.rect(17, 36, 26, 37, 'w')

# ================================================================ THE MARROW CUT (running water)
g.rect(44, 1, 45, 42, '~')
g.rect(44, 9, 45, 10, '=')                   # the footbridge
g.rect(44, 21, 45, 21, '#')                  # the old weir wall: a vampire's crossing
g.rect(44, 33, 45, 34, '=')                  # the sluice bridge
g.put(43, 32, 'x')                           # the sluice gear

# ================================================================ EAST — Hollin's camp on the hummock
g.box(49, 13, 58, 20, '#', '_'); g.put(49, 16, '+'); g.put(58, 17, '+')   # the tithe barn
g.rect(53, 14, 53, 15, '#')                  # a stall partition
for (x0, y0) in ((51, 8), (55, 8), (59, 10)): g.rect(x0, y0, x0 + 1, y0 + 1, 'h')   # tents
g.box(51, 24, 56, 28, '|', ','); g.put(56, 26, ',')   # the kennels
g.rect(47, 5, 48, 6, 'u'); g.put(47, 7, 's')          # the watch platform
g.rect(59, 17, 61, 17, ','); g.rect(61, 1, 61, 17, ',')   # the drove road
g.rect(46, 29, 62, 42, 'g')
g.rect(52, 33, 55, 36, 'b'); g.rect(57, 38, 61, 41, 'b')
g.put(48, 38, 'x'); g.put(49, 38, 'x')
if __name__ == '__main__':
    g.show()
    open('m08.grid', 'w').write(g.text())
