"""Thumb literal/add-PC plug-in-list candidates only; not an absence proof."""
from pathlib import Path
import sys,struct,hashlib,importlib.util
sys.argv.append('--light')
spec=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'));n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
print('ENGINE SHA256',hashlib.sha256(n.raw).hexdigest())
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB);md.detail=True
targets={0x103ff7c,0x108d9f4}
for va,size,off in n.loads:
 if va>=0x95e540:continue
 headers=[struct.unpack_from('<8I',n.raw,n.phoff+i*n.phsize) for i in range(n.phcount)]
 if not any(h[0]==1 and h[2]==va and h[6]&1 for h in headers):continue
 data=n.read(va,size);stop=min(size-68,0x95e540-va)
 for j in range(0,stop,2):
  h=struct.unpack_from('<H',data,j)[0]
  if h&0xf800!=0x4800:continue
  a=va+j;rd=(h>>8)&7;loc=((a+4)&~3)+(h&255)*4
  try:literal=n.word(loc)
  except ValueError:continue
  for delta in range(2,65,2):
   a2=a+delta;h2=struct.unpack_from('<H',data,j+delta)[0]
   if h2&0xff00!=0x4400 or (h2>>3)&15!=15 or (h2&7)|((h2>>4)&8)!=rd:continue
   endpoint=(a2+4+literal)&0xffffffff
   if endpoint not in targets:continue
   regid=getattr(n.capstone.arm,'ARM_REG_R'+str(rd));middle=list(md.disasm(data[j+2:j+delta],a+2))
   if sum(i.size for i in middle)!=delta-2 or any(regid in i.regs_access()[1] or i.mnemonic in ('b','bx','bl','blx','b.w') for i in middle):continue
   print(f'candidate load{a:08X} r{rd} word[{loc:08X}]={literal:08X}; addPC{a2:08X} -> {endpoint:08X}')
print('LIMIT: Thumb16 literal/same-register ADD-PC only below95E540; no alias/MOVW/MOVT/other-GOT-base absence claim.')
