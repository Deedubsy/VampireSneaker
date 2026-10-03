# tiny helper for authoring mission grids
class G:
    def __init__(s, w, h, fill=' '):
        s.w, s.h = w, h
        s.c = [[fill]*w for _ in range(h)]
    def rect(s, x0, y0, x1, y1, ch):          # inclusive
        for y in range(y0, y1+1):
            for x in range(x0, x1+1):
                if 0 <= x < s.w and 0 <= y < s.h: s.c[y][x] = ch
    def box(s, x0, y0, x1, y1, wall, floor):  # walls on the border
        s.rect(x0, y0, x1, y1, wall); s.rect(x0+1, y0+1, x1-1, y1-1, floor)
    def put(s, x, y, ch): s.c[y][x] = ch
    def at(s, x, y): return s.c[y][x]
    def text(s): return "\n".join("".join(r) for r in s.c)
    def show(s):
        print("    " + "".join(str(x//10) for x in range(s.w)))
        print("    " + "".join(str(x%10) for x in range(s.w)))
        for y, r in enumerate(s.c): print(f"{y:3d} " + "".join(r))
