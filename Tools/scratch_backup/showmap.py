import sys
f=sys.argv[1]; import os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); from mapgrid import grid
g=grid(f)
W=max(len(r) for r in g)
print('    '+''.join(str(x//10) for x in range(W)))
print('    '+''.join(str(x%10) for x in range(W)))
for y,r in enumerate(g): print(f'{y:3} {r}')
