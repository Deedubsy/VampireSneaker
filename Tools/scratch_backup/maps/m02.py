from grid import G
g = G(62, 40)
g.rect(1, 1, 60, 38, '.')
g.rect(1, 0, 3, 39, '~')                 # the Weir canal (west)
g.rect(4, 1, 60, 1, '#')                 # north boundary wall
g.rect(60, 1, 60, 38, '#')               # east boundary
g.rect(4, 38, 41, 38, '#')               # south boundary
g.rect(6, 2, 59, 3, ',')                 # back court
# north blocks
g.rect(6, 4, 15, 15, 'H')                # A
g.rect(16, 4, 18, 15, ',')               # alley AB
g.rect(19, 4, 30, 15, 'H')               # B
g.rect(31, 4, 42, 15, 'h')               # C (drop from B)
g.rect(43, 4, 46, 7, '#')                # wash-house lean-to
g.rect(43, 8, 46, 15, ',')               # side yard
g.rect(47, 4, 59, 16, 'h')               # D lodging-house
g.rect(50, 12, 55, 15, '_')              # hall (open-top)
g.put(52, 16, '+')                       # front door
g.put(44, 8, 'p')                        # pipe side yard -> lean-to
g.put(45, 3, 'p')                        # pipe back court -> lean-to
g.put(18, 6, 'p')                        # pipe alley -> B roof
g.put(16, 13, 'p')                       # pipe alley -> A roof
g.put(9, 3, 'p')                         # pipe back court -> A roof
# south side
g.box(6, 23, 22, 37, '#', ',')           # coach-house yard
g.box(6, 23, 12, 30, '#', '_')           # stable (interior)
g.put(12, 27, '+')                       # stable door
g.rect(16, 24, 21, 25, 'h')              # coach-house loft
g.put(14, 23, '+'); g.put(15, 23, '+')   # yard gate
g.put(5, 32, 'p')                        # pipe quay -> yard wall
g.rect(23, 22, 25, 37, ',')              # Tanner's Row
g.rect(26, 23, 34, 34, 'h')              # slum block
g.put(25, 28, 'p')                       # pipe Tanner's Row -> slum roof
g.rect(23, 35, 41, 37, ',')              # south lane
g.box(35, 23, 50, 33, '#', '_')          # The Drowned Man
g.put(42, 23, '+')                       # tavern front door
g.put(50, 30, '+')                       # tavern back door
g.rect(51, 23, 59, 33, ',')              # tavern yard
g.rect(35, 34, 41, 34, ',')
g.rect(42, 34, 59, 35, '=')              # dock
g.rect(42, 36, 59, 39, '~')              # the Weir cut
g.rect(4, 39, 41, 39, ' ')
g.show() if __name__ == '__main__' and False else None
open('m02.grid', 'w').write(g.text())
g.show()
