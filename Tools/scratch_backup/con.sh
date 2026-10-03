#!/bin/bash
cd /mnt/e/ShadowTactics/StealthVampire
./Tools/u.sh console --level ${1:-warn} --tail ${2:-30} --format json 2>&1 | python3 -c "
import sys,json
t=sys.stdin.read()
def walk(o):
  if isinstance(o,dict):
    if 'entries' in o: return o
    for v in o.values():
      r=walk(v)
      if r: return r
  if isinstance(o,list):
    for v in o:
      r=walk(v)
      if r: return r
  if isinstance(o,str) and o.startswith('{'):
    try: return walk(json.loads(o))
    except: pass
  return None
try:
  d=walk(json.loads(t))
  for e in d['entries']:
    m=e.get('message','')
    if 'UAC1001' in m: continue
    print(e.get('level'),'|',m[:400].replace('\n',' / '))
    st=e.get('stackTrace','')
    if e.get('level')=='error' and st: print('   ', st[:600].replace('\n',' / '))
except Exception as ex: print('parse fail',ex,t[:1500])
"
