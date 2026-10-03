from grid import G
g = G(64, 46)
g.rect(0, 0, 63, 45, '.')
g.rect(0, 0, 63, 0, 'H')                 # north: the old city wall
g.rect(0, 0, 0, 41, 'H')                 # west
g.rect(63, 0, 63, 41, 'H')               # east
g.rect(0, 42, 63, 45, '~')               # the Weir
# ---------------- WEST: Saint Brigid's almshouse yard and the Pilgrims' Lane
g.rect(1, 1, 19, 14, ',')                # almshouse yard
g.rect(1, 1, 6, 14, 'h')                 # the almshouse range (low roofs)
g.put(7, 11, 'p')                        # pipe yard -> almshouse roof
g.rect(7, 3, 10, 3, '#')                 # yard wall: almshouse roof -> tower walls
g.box(11, 2, 17, 8, '#', ':')            # Saint Brigid's bell tower
g.put(11, 2, 'T'); g.put(17, 2, 'T')     # spire corners
g.put(14, 8, '+')                        # tower door (votive lights)
g.rect(1, 15, 19, 37, '.')               # the Pilgrims' Lane quarter
g.rect(2, 17, 7, 22, 'h')                # lodging house
g.put(8, 19, 'p')
g.rect(2, 26, 7, 31, 'h')                # lodging house
g.rect(12, 18, 16, 23, 'h')              # the pilgrims' hostel
g.put(14, 24, 'p')
g.rect(12, 28, 16, 33, 'h')              # chandler's
g.rect(9, 34, 10, 34, 'x')
g.rect(17, 15, 18, 16, 'x')
# ---------------- CENTRE: the Cathedral of Saint Corvin
g.box(27, 2, 39, 8, '#', 'c')            # the deacon's archive (chapter house)
g.put(33, 8, '+')                        # archive -> nave
g.put(39, 5, '+')                        # archive -> the close passage
g.rect(20, 1, 26, 7, ',')                # the north alley (behind the archive)
g.rect(40, 1, 44, 8, ',')                # the close passage
g.box(24, 8, 42, 28, '#', ':')           # the nave
g.rect(27, 2, 39, 2, '#')
for y in (12, 16, 20, 24):               # columns
    g.put(28, y, '#'); g.put(38, y, '#')
g.put(33, 28, '+')                       # great south door
g.put(33, 8, '+')                        # archive -> nave (re-cut after the nave box)
g.box(22, 3, 26, 7, '#', ',')            # the anchoress's cell (walled; only from the wall tops)
g.put(42, 13, '+')                       # east door -> close
g.rect(22, 9, 23, 21, 'h')               # west aisle roof
g.put(21, 13, 'p')                       # pipe lane -> west aisle roof
g.rect(43, 16, 44, 27, 'h')              # east aisle roof
g.box(18, 22, 24, 28, '#', ':')          # the Great Bell tower (west front)
g.put(18, 22, 'T'); g.put(18, 28, 'T')
g.put(21, 28, '+')                       # tower door -> square
g.put(24, 25, '+')                       # tower door -> nave
g.rect(19, 29, 48, 37, '.')              # the Cathedral Square
g.rect(32, 32, 34, 34, 'x')              # the fountain base
# ---------------- EAST: the Bishop's Close
g.rect(45, 1, 62, 16, 'g')               # the close garden
g.rect(54, 1, 62, 8, 'H')                # the Bishop's palace
g.rect(46, 4, 46, 12, 'b'); g.rect(50, 10, 53, 10, 'b'); g.rect(48, 14, 58, 14, 'b')   # hedges
g.rect(57, 10, 57, 12, 'b')
# ---------------- EAST: the Watch-house and its bell
g.rect(45, 17, 62, 41, '.')
g.put(45, 24, 'p')                       # pipe -> east aisle roof
g.box(47, 18, 56, 27, '#', '_')          # the Watch-house
g.put(47, 22, '+')                       # west door
g.put(53, 27, '+')                       # south door
g.rect(47, 23, 51, 23, '#'); g.rect(51, 23, 51, 27, '#')   # the cells
g.put(49, 23, '+')
g.box(56, 17, 62, 23, '#', ':')          # the Watch bell tower
g.put(62, 17, 'T'); g.put(56, 17, 'T')
g.put(56, 20, '+')                       # tower <- watch-house
g.put(59, 23, '+')                       # tower <- watch yard
g.rect(57, 24, 62, 36, ',')              # the Watch yard
g.rect(56, 28, 56, 37, '#')              # yard wall west
g.rect(56, 37, 62, 37, '#')              # yard wall south
g.put(59, 37, '+')                       # the yard gate
g.rect(51, 30, 55, 34, 'h')              # the stable (roof onto the yard wall)
g.put(50, 32, 'p')
g.rect(60, 25, 61, 26, 'x')
# ---------------- the Weirside quay
g.rect(1, 38, 62, 41, '.')
g.rect(3, 40, 5, 41, '=')                # the ferry steps (start)
g.rect(58, 39, 61, 41, '=')              # the Weir steps (escape)
g.rect(26, 39, 27, 41, '=')

if __name__ == '__main__':
    g.show()
    open('m04.grid', 'w').write(g.text())
