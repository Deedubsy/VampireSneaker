from grid import G
g = G(64, 44)
g.rect(0, 0, 63, 43, '.')
g.rect(0, 0, 63, 0, 'H'); g.rect(0, 0, 0, 39, 'H'); g.rect(63, 0, 63, 39, 'H')
g.rect(0, 40, 63, 43, '~')               # the Gasworks Cut
# ---------------- WEST: the gasworks yard
g.rect(1, 1, 19, 29, ',')
g.rect(2, 3, 8, 9, 'T')                  # gasholder No. 1
g.put(9, 6, 'p')
g.rect(2, 14, 8, 20, 'T')                # gasholder No. 2
g.rect(11, 2, 18, 8, 'H')                # the retort house
g.rect(11, 14, 13, 15, 'x'); g.rect(15, 18, 17, 19, 'x')   # coal heaps
g.rect(3, 23, 4, 24, 'x')
g.rect(11, 23, 15, 27, 'h')              # the governor house
g.put(16, 25, 'p')
g.rect(1, 30, 19, 30, '#'); g.put(10, 30, '+')             # yard wall + gate
g.rect(20, 1, 20, 30, '#'); g.put(20, 8, '+')              # east yard wall + back gate
# ---------------- CENTRE: the Guildhall of Lamps
g.rect(21, 1, 44, 3, ',')                # the north alley
g.rect(21, 4, 21, 30, ',')               # Retort Lane
g.box(22, 4, 42, 26, '#', '_')
g.rect(23, 5, 32, 13, ':')               # clerks' office
g.rect(33, 5, 33, 13, '#')
g.rect(34, 5, 41, 13, 'c')               # Penrose's study
g.rect(22, 14, 42, 14, '#')
for x, y in ((28, 4), (33, 9), (27, 14), (38, 14), (32, 26), (42, 20)): g.put(x, y, '+')
g.rect(25, 16, 25, 24, 'x')              # the burner-testing counter
g.put(25, 20, '_')
g.rect(22, 27, 42, 30, '.')              # Guild Square
# ---------------- EAST: the lamplighters' depot
g.rect(43, 4, 44, 30, ',')               # Depot Lane
g.box(46, 2, 56, 10, '#', '_')           # the apprentices' bunkhouse
g.put(51, 10, '+')
g.rect(58, 2, 62, 8, 'h'); g.put(57, 5, 'p')               # the oil store
g.rect(45, 11, 62, 30, '.')
g.rect(45, 12, 45, 28, '|'); g.put(45, 20, '.')            # depot railings
g.rect(48, 14, 50, 15, 'x'); g.rect(55, 13, 57, 14, 'x')    # ladder racks, oil drums
g.rect(52, 24, 53, 26, 'x')
# ---------------- Guild Street, the terraces and the Coal Wharf
g.rect(1, 31, 62, 32, '.')
for x0, x1 in ((5, 13), (22, 29), (50, 57)):
    g.rect(x0, 33, x1, 35, 'h')
g.put(14, 34, 'p'); g.put(30, 34, 'p'); g.put(49, 34, 'p')
g.rect(1, 36, 62, 39, '.')
g.rect(2, 40, 4, 41, '=')                # the start steps
g.rect(44, 40, 47, 41, '=')              # the coal jetty: the boat
g.rect(26, 40, 28, 40, '=')
if __name__ == '__main__':
    g.show()
    open('m05.grid', 'w').write(g.text())
