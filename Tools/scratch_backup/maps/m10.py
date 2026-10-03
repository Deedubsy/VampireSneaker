from grid import G
g = G(64, 48)
# ---------------------------------------------------------------- Gasworks Road (north): the city side
g.rect(0, 0, 63, 0, 'H')
g.rect(0, 1, 63, 4, '.')
g.rect(0, 5, 63, 5, '#')
g.rect(30, 5, 31, 5, '+')                    # the main gate
g.put(56, 5, '+')                            # the side gate
# ---------------------------------------------------------------- the works: cinders everywhere
g.rect(0, 6, 63, 40, ',')
g.rect(0, 6, 0, 40, '#'); g.rect(63, 6, 63, 40, '#')
# the yard road from the gate down to the wharf
g.rect(29, 6, 32, 19, '.'); g.rect(20, 35, 44, 38, '.')
# ---- the lamp manufactory (west)
g.box(2, 8, 18, 22, '#', ':')
g.put(18, 12, '+'); g.put(10, 22, '+'); g.put(18, 19, '+')
g.rect(3, 9, 17, 10, 'u'); g.put(3, 11, 's'); g.put(17, 11, 's')   # the drying gallery
g.rect(8, 13, 8, 21, '#'); g.put(8, 17, '+')                   # the office partition
g.rect(15, 19, 16, 20, 'x')                                    # consignment one, crated
g.rect(11, 14, 13, 14, 'x'); g.rect(11, 17, 13, 17, 'x')       # the benches
# ---- the loading yard (north-centre)
g.rect(21, 7, 40, 17, '.')
g.rect(24, 8, 25, 9, 'T'); g.put(26, 9, 's')                  # searchlight tower one
g.rect(34, 9, 36, 10, 'x')                                     # consignment two, on the dray
g.rect(38, 13, 40, 14, 'x')
# the workers' mess and coal sheds (north-east): low roofs to run along
g.rect(45, 7, 54, 10, 'h'); g.put(44, 9, 'p')
g.rect(57, 7, 62, 9, 'h'); g.put(58, 10, 'p')
# ---- the retort house (centre)
g.box(20, 20, 42, 33, '#', ':')
g.rect(30, 20, 31, 20, '+'); g.put(20, 27, '+'); g.put(42, 27, '+'); g.rect(30, 33, 31, 33, '+')
g.rect(21, 21, 41, 21, 'u'); g.put(21, 22, 's'); g.put(41, 22, 's')   # the charging stage
for x0 in (23, 33):
    g.rect(x0, 24, x0 + 6, 25, 'x')                            # retort benches
    g.rect(x0, 29, x0 + 6, 30, 'x')
# ---- the great gas holder (east): a brick-cased tank, nine metres of it
cx, cy, R = 53, 22, 5.3
for y in range(48):
    for x in range(64):
        if (x - cx) ** 2 + (y - cy) ** 2 <= R * R: g.put(x, y, 'T')
g.put(53, 28, 's')                                             # the inspection ladder
g.box(49, 29, 57, 33, '#', '_'); g.put(53, 29, ':'); g.put(53, 29, '+'); g.put(49, 31, '+')  # the valve house
g.rect(60, 14, 61, 15, 'T'); g.put(60, 16, 's')               # searchlight tower two
# ---- the coke yard (south-west)
g.rect(2, 26, 17, 38, ',')
for (x0, y0, x1, y1) in ((3, 27, 6, 29), (11, 27, 14, 28), (4, 33, 7, 35), (13, 32, 15, 34)):
    g.rect(x0, y0, x1, y1, 'x')
g.rect(9, 37, 12, 39, 'h'); g.put(13, 38, 'p')                # the weighbridge office
# ---- the engine house (south-east): power for the searchlights
g.box(46, 35, 56, 40, '#', '_'); g.put(51, 35, '+'); g.put(46, 38, '+')
g.rect(28, 37, 29, 38, 'T'); g.put(30, 37, 's')               # searchlight tower three (the wharf)
# ---------------------------------------------------------------- the wharf and the river
g.rect(0, 41, 63, 43, '=')
g.rect(0, 44, 63, 47, '~')
g.rect(36, 41, 38, 42, 'x')                                    # consignment three, waiting for the barge
g.rect(18, 41, 19, 42, 'x'); g.rect(50, 42, 52, 43, 'x')
if __name__ == '__main__':
    g.show()
    open('m10.grid', 'w').write(g.text())
