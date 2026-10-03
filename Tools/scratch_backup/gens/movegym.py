W,H=38,25
g=[['#']*W for _ in range(H)]
def rect(c0,r0,c1,r1,ch='.'):
    for r in range(r0,r1+1):
        for c in range(c0,c1+1): g[r][c]=ch
def put(c,r,ch): g[r][c]=ch
# start pocket (SW)
rect(1,20,6,23,',')
# jagged diagonal corridor NE: 3-wide steps
for k in range(9):
    rect(1+k,19-k,3+k,19-k,',')
# junction
rect(8,9,12,12,'.')
# narrow 1-wide corridor with 90-degree turns, north-west
rect(9,5,9,8,'.')
rect(3,5,9,5,'.')
rect(3,1,3,5,'.')
rect(3,1,15,1,'.')
put(15,2,'+')            # a door straight after the turn
# three rooms in a row, doors offset
rect(13,3,17,8,'_'); rect(19,3,23,8,':'); rect(25,3,29,8,'c')
put(18,7,'+'); put(24,3,'+'); put(30,8,'+')
# clutter yard
rect(13,11,28,22,'.')
rect(12,10,12,12,'.')
for (c,r) in [(15,13),(16,13),(19,15),(22,12),(23,12),(25,16),(17,19),(18,19),(21,20),(26,20),(14,17),(27,13)]:
    put(c,r,'x')
for (c,r) in [(20,18),(24,18),(16,16),(22,15)]:
    put(c,r,'#')      # pillars
# the east hall with an S-bend
rect(31,2,36,22,'.')
rect(29,18,30,19,'.')   # yard into the hall
rect(33,12,36,12,'#')   # bend 1: gap at 31-32
rect(31,16,34,16,'#')   # bend 2: gap at 35-36
for r in g: print(''.join(r)+' ')
