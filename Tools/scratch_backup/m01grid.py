W,H=52,34
g=[[' ']*W for _ in range(H)]
def rect(x0,y0,x1,y1,c):
    for y in range(y0,y1+1):
        for x in range(x0,x1+1): g[y][x]=c
def room(x0,y0,x1,y1,floor,wall='#'):
    rect(x0-1,y0-1,x1+1,y1+1,wall); rect(x0,y0,x1,y1,floor)
def put(x,y,c): g[y][x]=c
# records room
room(16,2,26,5,'_')
# corridor
room(2,7,45,9,':')
# middle band
room(2,11,13,21,':')      # morgue
room(15,11,22,21,',')     # store / linen
room(24,11,35,21,':')     # boiler
room(37,11,45,21,':')     # culvert antechamber
# ward
room(2,23,45,31,'w')
# culvert (masonry, too tall to climb at Awakening 1)
rect(46,10,51,32,'H'); rect(47,11,48,31,','); rect(49,11,50,31,'~')
# dry islands in the ward (raised boardwalk around beds)
rect(2,23,9,25,'_'); rect(38,29,45,31,'_')
# puddles leaking into morgue / boiler
rect(2,20,6,21,'w'); rect(30,20,35,21,'w')
# doorways
for x,y in [(21,6),(8,10),(14,16),(23,16),(30,10),(33,22),(41,10),(41,22),(5,22),(36,16)]: put(x,y,'+')
put(46,17,'+')            # culvert gate
# boiler: furnace block and catwalk
rect(26,12,28,14,'h')     # furnace (4.5 m)
rect(31,11,35,11,'u')     # catwalk along the north wall
put(31,12,'p')            # pipe up to the catwalk
# ward partitions (force a zig-zag through the dark)
rect(16,26,16,31,'#'); rect(30,23,30,28,'#')
# crate stacks
rect(16,19,17,20,'x'); rect(43,12,44,12,'x')
print('\n'.join(''.join(r) for r in g))
