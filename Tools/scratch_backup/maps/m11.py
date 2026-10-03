from grid import G
g = G(64, 48)
# ---------------------------------------------------------------- the city around it: dark since the gasworks
g.rect(0, 0, 63, 0, 'H')
g.rect(0, 1, 63, 47, '.')
# Stage Door Lane (west), houses on its far side
g.rect(0, 1, 1, 40, 'h'); g.put(2, 12, 'p'); g.put(2, 30, 'p')
g.rect(2, 1, 5, 40, ',')
# ---------------------------------------------------------------- the opera house: x 6..41, y 2..40
g.box(6, 2, 41, 40, 'H', '_')
# ---- the stage house (north): wings, the stage, the flies
g.rect(7, 3, 40, 10, '_')
g.rect(7, 3, 11, 10, '_')                      # west wing (scene dock)
g.rect(36, 3, 40, 10, '_')                     # east wing (dressing rooms)
g.rect(36, 6, 40, 6, '#'); g.put(38, 6, '+')   # dressing-room wall
g.rect(12, 3, 35, 3, 'u'); g.put(12, 4, 's')   # the fly gallery along the back wall
g.put(6, 7, '+')                               # the stage door
g.put(6, 36, '+')                              # the cloakroom's lane door
g.put(41, 4, '+')                              # the scene-dock door (east lane)
# ---- the proscenium (y 11): the stage opening x 12..35
g.rect(7, 11, 11, 11, 'H'); g.rect(36, 11, 40, 11, 'H')
g.rect(12, 11, 35, 11, '#'); g.rect(21, 11, 26, 11, '_')   # the proscenium wall and its 12 m arch
g.rect(7, 11, 8, 11, 'u'); g.rect(39, 11, 40, 11, 'u')   # the box corridors run up to the proscenium
g.put(8, 10, 's'); g.put(39, 10, 's')          # pass doors: stairs up from the wings to the box corridors
# ---- the orchestra pit and the stalls
g.rect(12, 12, 35, 27, 'c')
g.rect(12, 12, 35, 13, '_')                    # the pit
g.rect(14, 14, 21, 14, 'x'); g.rect(26, 14, 33, 14, 'x')   # the pit rail (gaps at the aisles)
# ---- the side tiers: corridor (x 7..8 / 39..40), partition (9 / 38), boxes (10..11 / 36..37)
for (c0, c1, part, b0, b1) in ((7, 8, 9, 10, 11), (39, 40, 38, 36, 37)):
    g.rect(c0, 12, c1, 32, 'u')
    g.rect(part, 12, part, 29, 'H')
    g.rect(b0, 12, b1, 27, 'u')
    g.rect(b0, 16, b1, 16, 'H'); g.rect(b0, 21, b1, 21, 'H')   # box partitions
    for y in (14, 19, 24): g.put(part, y, 'u')                   # box doors
# ---- the grand tier (south, facing the stage): boxes y 28..29, partition y 30, corridor y 31..32
g.rect(10, 28, 37, 29, 'u')
g.rect(9, 30, 38, 30, 'H')
g.rect(7, 31, 40, 32, 'u')
g.rect(17, 28, 17, 29, 'H'); g.rect(30, 28, 30, 29, 'H')     # the royal box x 18..29 between them
for x in (13, 23, 24, 34): g.put(x, 30, 'u')                  # grand-tier box doors
# ---- the foyer (ground floor, y 33..39)
g.rect(7, 33, 40, 39, ':')
g.rect(22, 33, 25, 33, 's')                    # the grand staircase up to the grand-tier corridor
g.rect(12, 33, 12, 39, '#'); g.put(12, 36, '+') # the cloakroom (west)
g.rect(35, 33, 35, 39, '#'); g.put(35, 36, '+') # the bar (east)
g.rect(22, 40, 25, 40, '+')                    # the front doors
# ---------------------------------------------------------------- east: Lantern Street and the Vigil's yard
g.rect(42, 1, 44, 47, '.')
g.put(41, 22, 'u'); g.put(42, 22, 's')         # the Council's private stair: a landing in the east wall, steps down to the street
g.box(46, 2, 62, 13, 'H', '_')                 # the Vigil's coach house
g.put(46, 8, '+'); g.rect(53, 13, 55, 13, '+')
g.rect(45, 14, 63, 34, ',')                    # the coach yard
g.rect(45, 14, 45, 34, '#'); g.rect(45, 21, 45, 23, '+')   # the yard wall and its gate
g.rect(46, 35, 63, 36, '#')
g.rect(56, 22, 57, 23, 'T'); g.put(56, 24, 's') # the Vigil's searchlight tower (over the yard and the street)
# ---------------------------------------------------------------- the square (south)
g.rect(2, 41, 63, 47, '.')
g.rect(0, 41, 1, 47, 'h'); g.put(2, 44, '.')
g.rect(30, 43, 31, 44, 'T'); g.put(32, 44, 's') # the Watch's searchlight tower in the square
g.rect(46, 37, 62, 40, 'h'); g.put(47, 40, 'p') # the arcade roofs
if __name__ == '__main__':
    g.show()
    open('m11.grid', 'w').write(g.text())
