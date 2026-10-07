"""Relocated shipped behavior vtable census; no runtime oracles/model values."""
import importlib.util, contextlib
from pathlib import Path
h=Path(__file__).resolve().parent
s=importlib.util.spec_from_file_location('native',h/'20261007-queue5-native.py');n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
def relocated(a):
 v=n.word(a);r=n.rel.get(a)
 if r and r.has_symbol and r.type.name=='ARM_ABS32':v=(v+r.symbol.value)&0xffffffff
 elif r and r.has_symbol and r.type.name in ('ARM_GLOB_DAT','ARM_JUMP_SLOT'):v=r.symbol.value
 return v
with (h/'20261007-q17-vtables-native.txt').open('w',encoding='utf-8',newline='\n') as o,contextlib.redirect_stdout(o):
 print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
 seen=set()
 for sym in sorted(n.elf.symbols,key=lambda x:x.value):
  if not sym.value or not sym.name.startswith('_ZTV') or 'Behavior' not in sym.name or sym.value in seen:continue
  seen.add(sym.value)
  if sym.size<0x94:continue
  a=sym.value+8;vals=[relocated(a+k) for k in (0x20,0x24,0x28,0x84,0x88)]
  cells=[]
  for k,v in zip((0x20,0x24,0x28,0x84,0x88),vals):
   r=n.rel.get(a+k)
   binding=(' ['+r.symbol.name+']') if r and r.has_symbol else ''
   cells.append(f'+{k:02X}={v:08X}'+binding)
  print(f'VTABLE {sym.value:08X} {sym.name} '+ ' '.join(cells))
  for v in vals[3:]:
   f=n.enclosing(v&~1)
   if f and f[0]==(v&~1) and 'IBehavior::' not in f[3]:
    print('OVERRIDE',f[3]);n.dump(f[0],n.end(f),True)
 print('SIX HELPER TABLES')
 for a in (0x1025A84,0x1025D08,0x1025EF8,0x1025F8C,0x10260EC,0x10263C0):
  for k in (0x20,0x24,0x28,0x2C,0x30):
   v=relocated(a+8+k);print(f'{a:08X}+vptr{k:02X}: {v:08X}')
print('Saved primary vtable census.')
