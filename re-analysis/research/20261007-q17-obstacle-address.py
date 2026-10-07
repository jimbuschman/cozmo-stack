"""Candidate PC-relative references to the docking obstacle dimension vector."""
from pathlib import Path
import importlib.util,struct,contextlib
h=Path(__file__).resolve().parent
s=importlib.util.spec_from_file_location('n',h/'20261007-queue5-native.py');n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
sec=n.elf.get_section('.text');data=bytes(sec.content);base=sec.virtual_address;hits=set()
for off in range(32,len(data)-2,2):
 half=struct.unpack_from('<H',data,off)[0]
 if half&0xff78!=0x4478:continue
 addr=base+off;reg=(half&7)+((half>>4)&8)
 for prev in range(off-30,off,2):
  a=base+prev;w=struct.unpack_from('<H',data,prev)[0];loc=None
  if w&0xf800==0x4800 and (w>>8)&7==reg:loc=((a+4)&~3)+4*(w&255)
  if w in (0xf8df,0xf85f):
   w2=struct.unpack_from('<H',data,prev+2)[0]
   if w2>>12==reg:loc=((a+4)&~3)+(w2&4095)*(1 if w==0xf8df else -1)
  if loc is None:continue
  try:value=(n.word(loc)+addr+4)&0xffffffff
  except ValueError:continue
  if value==0x105ae90:hits.add((a,addr,loc))
with (h/'20261007-q17-obstacle-address-native.txt').open('w',encoding='utf-8') as out,contextlib.redirect_stdout(out):
 print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
 for a,b,loc in sorted(hits):
  f=n.enclosing(a);print('REFERENCE',hex(a),hex(b),hex(loc),f[3] if f else '')
  n.dump(max(a-24,base),b+48,True)
print('Found',len(hits),'candidates')
