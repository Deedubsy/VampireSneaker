# shared: parse an @map section exactly like MapParser (blank rows count; trailing blanks trimmed; "#!" lines skipped)
def grid(f):
    lines=open(f).read().replace('\r\n','\n').replace('\r','\n').split('\n'); g=[]; on=False
    for l in lines:
        if l.startswith('#!'): continue
        if l.strip().startswith('@'):
            if on: break
            on = l.strip()=='@map'; continue
        if on: g.append(l.split('#!')[0].rstrip() if '#!' in l else l.rstrip())
    while g and g[-1].strip()=='': g.pop()
    return g
