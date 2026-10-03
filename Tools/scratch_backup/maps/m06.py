from grid import G
W, H = 64, 46
g = G(W, H, 'g')
# estate wall
g.rect(0, 0, W-1, 0, '#'); g.rect(0, H-1, W-1, H-1, '#'); g.rect(0, 0, 0, H-1, '#'); g.rect(W-1, 0, W-1, H-1, '#')

# ---------------------------------------------------------------- SW hedge garden (start) and summerhouse
for y in (29, 33, 37, 41):
    g.rect(2, y, 13, y, 'b')
g.put(3, 29, 'g'); g.put(12, 33, 'g'); g.put(4, 37, 'g'); g.put(11, 41, 'g'); g.put(7, 37, 'g')
g.rect(1, 43, 14, 44, ',')                 # gravel walk along the south wall
g.box(4, 30, 9, 32, '#', '_')               # summerhouse (frozen) ... rows 30..32 inside 31
g.put(6, 32, '+')
g.rect(15, 28, 15, 44, 'b')                 # tall hedge between garden and drive
g.put(15, 43, ',')                          # gap to the drive

# ---------------------------------------------------------------- kitchen yard (west of the house, restricted)
g.rect(1, 18, 15, 27, ',')
g.rect(1, 17, 15, 17, '#')                  # yard wall north
g.rect(1, 27, 14, 27, '#')                  # yard wall south (to the garden)
g.put(8, 27, '+')                           # gate to the hedge garden
g.rect(3, 19, 5, 21, 'x')                   # woodpile
# ---------------------------------------------------------------- north lawn & ice house (NW)
g.rect(1, 1, 15, 16, 'g')
g.box(3, 3, 8, 7, '#', ':')                 # ice house
g.put(8, 5, '+')
for x in range(10, 15, 2): g.put(x, 10, 'b')

# ---------------------------------------------------------------- the house
g.box(16, 4, 52, 33, '#', '_')
# north range: library | long gallery | study
g.rect(17, 5, 27, 12, 'c')                  # library
g.rect(28, 5, 28, 12, '#'); g.put(28, 9, '+')
g.rect(29, 5, 41, 12, '_')                  # long gallery
g.rect(42, 5, 42, 12, '#')                  # study wall (door entity at 42 9)
g.put(42, 9, '+')
g.rect(43, 5, 51, 12, 'c')                  # study
g.rect(17, 13, 51, 13, '#')                 # wall under the north range
g.put(18, 13, '+')                          # servants' passage -> library (jib door)
g.put(31, 13, '+')                          # long gallery -> ballroom
# west wing: servants' passage | card room / butler's pantry
g.rect(17, 14, 18, 25, '_')                 # servants' passage
g.rect(19, 14, 19, 25, '#'); g.put(19, 19, '+')      # passage -> card room (jib door)
g.rect(20, 14, 23, 21, 'c')                 # card room
g.rect(20, 22, 23, 22, '#')
g.rect(20, 23, 23, 25, ':')                 # butler's pantry
g.put(19, 24, '+')                          # passage -> pantry
g.rect(24, 14, 24, 25, '#'); g.put(24, 17, '+')      # card room -> ballroom
# ballroom
g.rect(25, 14, 43, 25, '_')
g.rect(28, 17, 40, 23, 'c')                 # the dance floor carpet
g.rect(37, 14, 43, 15, 'u')                 # musicians' gallery (NE corner)
g.put(36, 15, 's')
g.rect(44, 14, 44, 25, '#'); g.put(44, 17, '+'); g.put(44, 22, '+')
# drawing room (east)
g.rect(45, 14, 51, 25, 'c')
g.put(52, 20, '+')                          # door to the terrace
# south range: kitchen | hall | cloakroom
g.rect(17, 26, 51, 26, '#')
g.rect(17, 27, 26, 32, ':')                 # kitchen
g.put(18, 26, '+')                          # kitchen -> passage
g.rect(27, 27, 27, 32, '#')
g.rect(28, 27, 40, 32, ':')                 # entrance hall
g.put(33, 26, '+'); g.put(34, 26, '+'); g.put(35, 26, '+')   # hall -> ballroom
g.rect(41, 27, 41, 32, '#'); g.put(41, 30, '+')
g.rect(42, 27, 51, 32, 'c')                 # cloakroom / retiring room
g.put(48, 26, '+')                          # cloakroom -> drawing room
g.put(16, 27, '+')                          # kitchen door (threshold), onto the yard
g.put(34, 33, '+')                          # front door (threshold)
g.rect(20, 28, 22, 28, 'x')                 # kitchen range / tables
g.rect(24, 31, 25, 31, 'x')

# ---------------------------------------------------------------- terrace (east, raised 3 m) and east garden
g.rect(53, 14, 58, 26, 'u')
g.put(53, 20, 's')
g.rect(59, 1, 62, 44, 'g')
g.rect(53, 1, 58, 13, 'g')
g.rect(53, 27, 58, 33, 'g')
for y in range(19, 25): g.put(60, y, 'b')
g.put(61, 21, 'b'); g.put(61, 22, 'b')
g.rect(55, 4, 60, 8, 'w')                   # frozen pond
g.rect(57, 5, 58, 7, '~')
for y in range(10, 14): g.put(61, y, 'b')
g.rect(54, 29, 57, 29, 'b')

# ---------------------------------------------------------------- forecourt and carriage drive (south)
g.rect(16, 34, 62, 44, '.')
g.rect(17, 34, 51, 35, ':')                 # front steps / terrace stones
g.rect(16, 36, 62, 36, '.')
g.rect(22, 38, 26, 39, 'x')                 # a coach
g.rect(40, 38, 44, 39, 'x')                 # a coach
g.rect(50, 41, 54, 42, 'x')                 # a coach
g.rect(28, 41, 29, 42, 'b')                 # clipped yews
g.rect(37, 41, 38, 42, 'b')
g.rect(31, 45, 35, 45, '.')                 # the gates (open)
g.rect(53, 34, 62, 35, 'g')
g.rect(55, 37, 61, 44, 'g')                 # paddock
for x in range(55, 62, 3): g.put(x, 37, 'b')

if __name__ == "__main__":
    g.show()
open("m06.grid", "w").write(g.text())
