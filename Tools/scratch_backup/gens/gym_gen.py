W, H = 34, 24
g = [['.' for _ in range(W)] for _ in range(H)]
def rect(x0, y0, x1, y1, ch):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1): g[y][x] = ch
def ring(x0, y0, x1, y1, ch):
    for x in range(x0, x1 + 1): g[y0][x] = ch; g[y1][x] = ch
    for y in range(y0, y1 + 1): g[y][x0] = ch; g[y][x1] = ch
ring(0, 0, W - 1, H - 1, '#')
# west: the lamp row, dirt
rect(1, 1, 11, 22, ',')
# middle: a room with a doorway (light spills out of it only through the door)
ring(14, 2, 21, 9, '#'); rect(15, 3, 20, 8, '_'); g[9][17] = '+'
# a short wall south of the square, for a lamp's shadow
for x in range(13, 17): g[13][x] = '#'
# east: a house roof (4.5 m) with a drainpipe, and the hunter's street
rect(25, 2, 32, 10, 'h'); g[11][27] = 'p'
# exit
g[22][32] = '.'
for row in g: print(' ' + ''.join(row) + ' ')
