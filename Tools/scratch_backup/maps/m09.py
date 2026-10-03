from grid import G
g = G(64, 44)
# ---------------------------------------------------------------- Coldwater Lane and the canal (south)
g.rect(0, 43, 63, 43, '~')
g.rect(1, 38, 62, 42, '.')
g.rect(0, 29, 0, 42, 'H'); g.rect(63, 29, 63, 42, 'H')       # the neighbours: blind warehouse walls
g.rect(1, 37, 62, 37, '#')                    # the Institute's railings-on-stone
g.put(12, 37, '+')                            # the coal-yard gate
g.rect(40, 37, 41, 37, '+')                   # the main gate
g.rect(26, 41, 28, 42, 'x'); g.put(52, 41, 'x')   # barrows, a dray

# ---------------------------------------------------------------- the grounds
g.rect(1, 29, 62, 36, 'g')
# the coal yard (west): the subjects' way out, past the chute
g.rect(1, 29, 17, 36, ',')
g.rect(18, 29, 18, 36, '#'); g.put(18, 33, '+')
g.rect(3, 30, 5, 31, 'x'); g.rect(14, 34, 16, 35, 'x')
g.rect(7, 30, 10, 32, 'h'); g.put(11, 32, 'p')            # the coal shed
# the physic garden
g.rect(40, 29, 41, 36, ','); g.rect(19, 32, 49, 32, ',')
for (x0, y0, x1, y1) in ((22, 30, 27, 30), (22, 34, 27, 35), (31, 34, 36, 35), (44, 30, 47, 30), (44, 34, 47, 35), (31, 29, 36, 29)):
    g.rect(x0, y0, x1, y1, 'b')
# the generator house (east)
g.box(50, 29, 60, 36, '#', '_'); g.put(50, 32, '+')
g.rect(61, 29, 62, 36, 'g')

# ---------------------------------------------------------------- the Institute, upper floor
g.box(1, 2, 62, 28, '#', ':')
# the long gallery (south)
g.rect(2, 24, 61, 24, '#')
g.put(9, 28, '+'); g.rect(34, 28, 35, 28, '+'); g.put(48, 28, '+')

# ---- the west wing: Ward C, the subjects' cells
g.rect(2, 9, 21, 9, '#')
g.rect(8, 3, 8, 8, '#'); g.rect(14, 3, 14, 8, '#')
for x in (5, 11, 17): g.put(x, 9, '+')
g.rect(2, 3, 7, 8, '_'); g.rect(9, 3, 13, 8, '_'); g.rect(15, 3, 21, 8, '_')
g.rect(2, 14, 18, 14, '#'); g.rect(18, 15, 18, 23, '#')
g.put(18, 19, '+'); g.put(9, 24, '+'); g.put(20, 24, '+')
g.rect(2, 15, 17, 23, '_')                    # the orderlies' station
g.rect(12, 15, 12, 23, '#'); g.put(12, 19, '+')   # and the restraint room
# ---- Saule's laboratory (centre)
g.rect(22, 3, 22, 23, '#'); g.put(22, 11, '+')
g.rect(41, 3, 41, 23, '#'); g.put(41, 11, '+')
g.rect(23, 3, 40, 5, 'u'); g.put(24, 6, 's'); g.put(39, 6, 's')   # the observation gallery
g.put(31, 24, '+')
# ---- the east wing: the scholars
g.rect(42, 9, 61, 9, '#'); g.put(47, 9, '+'); g.put(57, 9, '+')
g.rect(53, 3, 53, 8, '#')
g.rect(42, 3, 52, 8, 'c')                     # the library
g.rect(54, 3, 61, 8, '_')                     # the quiet room (Clement)
g.rect(42, 13, 61, 13, '#'); g.put(46, 13, '+'); g.put(56, 13, '+')
g.rect(51, 14, 51, 23, '#')
g.put(45, 24, '+')
g.rect(52, 14, 61, 23, 'c')                   # Saule's study
if __name__ == '__main__':
    g.show()
    open('m09.grid', 'w').write(g.text())
