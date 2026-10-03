from grid import G
g = G(64, 64)
# ================================================================ St Vesper's: the nave (north)
g.box(1, 0, 62, 22, 'T', ':')                  # nine metres of black stone; interior x2-61, y1-21
g.rect(6, 1, 47, 2, 'u')                       # the north triforium gallery
g.rect(26, 20, 47, 21, 'u')                    # the south gallery (the west half fell in the flood of '09)
g.put(5, 2, 's'); g.put(48, 2, 's')            # gallery stairs
g.put(25, 20, "s"); g.put(48, 20, "s")
g.put(30, 3, 'p'); g.put(36, 19, 'p')          # carved tracery Ilse can climb
g.put(1, 11, '+')                              # the west doors (barred behind her)
# ---- the rood screen and the chancel
g.rect(50, 1, 50, 21, '#')
g.put(50, 4, '+'); g.put(50, 11, '+'); g.put(50, 18, '+')
g.rect(51, 1, 61, 3, 'H'); g.box(51, 1, 58, 4, 'H', '_'); g.put(55, 4, '+')   # the vestry
g.put(56, 22, '+')                             # the bricked stair, under the chancel floor
g.put(12, 22, '+')                             # the sexton's stair, under the south aisle
# ================================================================ the undercroft (middle)
g.rect(1, 23, 62, 43, 'H')
g.rect(56, 23, 56, 25, ',')                    # the bricked stair down
g.rect(44, 26, 61, 33, ',')                    # the old crypt: the Sisters' ossuary, untouched since they bricked it
g.rect(12, 23, 12, 26, ',')                    # the sexton's stair down
g.rect(4, 27, 16, 33, ',')                     # the charnel (Saule's people use it as a store)
g.put(8, 34, '+'); g.put(8, 35, ',')           # ...to the boiler room
g.rect(4, 36, 14, 40, ':')                     # the boiler room: the generator for his sunstones
g.rect(17, 30, 19, 31, ',')                    # passage, charnel to laboratory
g.rect(20, 26, 40, 37, ':')                    # Saule's laboratory
g.rect(41, 29, 43, 30, ',')                    # passage, laboratory to the old crypt
g.rect(41, 33, 43, 33, ','); g.put(42, 33, 'v')# a flue: mist only
g.put(27, 38, '+'); g.rect(24, 39, 30, 41, ':')# the purge chamber
g.rect(34, 38, 34, 44, ',')                    # the vault stair, behind its gate
# ================================================================ the flooded vault (south)
g.rect(1, 44, 62, 63, 'H')
g.rect(34, 44, 34, 44, ',')
g.rect(8, 45, 55, 61, 'w')                     # black water to the shin
g.rect(8, 45, 55, 46, '=')                     # the gantry along the vats
g.rect(28, 51, 36, 57, ':')                    # the dais where she kneels
g.rect(3, 49, 7, 55, ',')                      # Saule's pens (west alcove)
g.rect(2, 59, 7, 61, ',')                      # the Council's river door: their boat, the drain to the Ost
g.rect(56, 49, 60, 53, '_')                    # the Council's gallery: chairs to watch the harvest
if __name__ == '__main__':
    g.show()
    open('m14.grid', 'w').write(g.text())
