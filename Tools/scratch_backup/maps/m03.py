from grid import G
g = G(60, 42)
g.rect(0, 0, 59, 41, '.')
g.rect(0, 0, 59, 0, 'H')                 # north: warehouse backs
g.rect(0, 0, 0, 35, 'H')                 # west boundary
g.rect(59, 0, 59, 35, 'H')               # east boundary
g.rect(0, 36, 59, 41, '~')               # the harbour
g.rect(29, 1, 31, 35, '~')               # the Ost (running water)
# ---------------- west bank
g.box(1, 1, 13, 11, '#', '_')            # The Herring (sailors' tavern)
g.rect(1, 1, 13, 1, '#')
g.put(13, 7, '+')                        # tavern door -> back alley
g.put(7, 11, '+')                        # tavern door -> fish lane
g.rect(14, 1, 16, 11, ',')               # back alley
g.rect(17, 1, 28, 7, 'h')                # net sheds (low roofs)
g.rect(21, 3, 23, 5, ',')                # walled pocket (the shrine), only from the roofs
g.put(23, 5, 'p')                        # pipe out of the pocket
g.put(16, 5, 'p')                        # pipe alley -> shed roofs
g.rect(29, 3, 31, 6, 'h')                # the Customs Arch spans the Ost
g.rect(32, 3, 33, 6, 'h')                # arch east abutment
g.rect(17, 8, 28, 11, ',')               # gutting yard
g.rect(1, 12, 28, 13, '.')               # fish lane
# fish market
g.rect(5, 17, 8, 17, 'x'); g.rect(13, 17, 16, 17, 'x'); g.rect(21, 17, 24, 17, 'x')   # stall rows
g.rect(5, 22, 8, 22, 'x'); g.rect(13, 22, 15, 22, 'x')
g.rect(19, 24, 26, 29, 'h')              # the ice house
g.put(18, 26, 'p')                       # pipe -> ice-house roof
g.put(27, 16, 'x')
# west quay
g.rect(8, 31, 12, 33, 'h')               # net-mending loft
g.put(13, 32, 'p')
g.rect(4, 36, 5, 40, '=')                # the old pier (start)
g.rect(18, 36, 19, 39, '=')              # fish pier
g.rect(2, 34, 3, 34, 'x')
# ---------------- the swing bridge
g.rect(29, 19, 31, 21, '=')
# ---------------- east bank
g.rect(34, 1, 45, 10, ',')               # Institute bonded yard
g.rect(36, 8, 37, 9, 'x'); g.rect(41, 3, 43, 4, 'x'); g.rect(44, 7, 45, 8, 'x')
g.put(34, 6, 'p')                        # pipe yard -> arch
g.rect(46, 1, 58, 9, 'H')                # bonded warehouse
g.rect(32, 11, 58, 12, '.')              # Customs lane
# Harbourmaster's office: upper storey (h walls, u floor) over a ground floor (# walls)
g.rect(40, 13, 54, 18, 'h')
g.rect(41, 14, 53, 17, 'u')
g.put(53, 14, 'P')                       # pipe from the upper floor back onto the parapet
g.box(40, 19, 54, 26, '#', ':')
g.put(44, 19, 'u'); g.put(44, 18, 'u')   # stair landing
g.put(44, 20, 's')                       # stairs up
g.rect(48, 20, 48, 25, '#'); g.put(48, 23, '+')   # records room partition
g.put(40, 22, '+')                       # front door (plaza)
g.put(47, 26, '+')                       # back door (quay)
g.put(50, 12, 'p')                       # pipe lane -> upper storey parapet
g.put(52, 27, 'p')                       # pipe quay -> ground-floor wall top
g.rect(56, 13, 58, 26, 'h')              # gutting sheds
g.put(55, 20, 'p')
# east quay
g.rect(36, 29, 38, 30, 'x'); g.rect(54, 31, 56, 32, 'x')
g.rect(50, 36, 51, 40, '=')              # east pier
open('m03.grid', 'w').write(g.text())
g.show()
