import sys
# usage: tiles.py mission.txt "x,y" "x1,y1-x2,y2" ... : prints the tiles at points / along straight segments
f=sys.argv[1]; import os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); from mapgrid import grid
g=grid(f)
def t(x,y):
    return g[y][x] if 0<=y<len(g) and 0<=x<len(g[y]) else ' '
for a in sys.argv[2:]:
    if '-' in a:
        p,q=a.split('-'); x1,y1=map(int,p.split(',')); x2,y2=map(int,q.split(','))
        n=max(abs(x2-x1),abs(y2-y1)); s=''
        for k in range(n+1):
            x=x1+round((x2-x1)*k/max(1,n)); y=y1+round((y2-y1)*k/max(1,n)); s+=t(x,y)
        print(a, repr(s))
    else:
        x,y=map(int,a.split(',')); print(a, repr(t(x,y)))
