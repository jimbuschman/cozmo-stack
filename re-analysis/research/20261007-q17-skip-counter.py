"""Find native PC-relative materializations of the manager tick-skip word."""
from pathlib import Path
import importlib.util,struct,contextlib
h=Path(__file__).resolve().parent
s=importlib.util.spec_from_file_location('n',h/'20261007-queue5-native.py');n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
sec=n.elf.get_section('.text');data=bytes(sec.content);base=sec.virtual_address;hits=set()
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB);md.detail=True
for off in range(32,len(data)-2,2):
 half=struct.unpack_from('<H',data,off)[0]
 if half&0xff78!=0x4478:continue
 addr=base+off;reg=(half&7)+((half>>4)&8)
 if reg>12:continue
 for begin in range(off-30,off,2):
  ins=list(md.disasm(data[begin:off+2],base+begin))
  if not ins or ins[-1].address!=addr:continue
  for i in ins[:-1]:
   if i.mnemonic not in ('ldr','ldr.w') or len(i.operands)<2:continue
   if i.op_str.split(',')[0].strip()!=('r'+str(reg) if reg<8 else {8:'r8',9:'sb',10:'sl',11:'fp',12:'ip'}[reg]):continue
   o=i.operands[1]
   if o.type!=n.ARM_OP_MEM or o.mem.base!=n.ARM_REG_PC:continue
   loc=((i.address+4)&~3)+o.mem.disp
   try:value=(n.word(loc)+addr+4)&0xffffffff
   except ValueError:continue
   if value==0x1051010:hits.add((i.address,addr,loc))
with (h/'20261007-q17-skip-counter-native.txt').open('w',encoding='utf-8') as out,contextlib.redirect_stdout(out):
 print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
 print('INITIAL DATA 01051010',n.read(0x1051010,4).hex(),'signed little-endian',struct.unpack('<i',n.read(0x1051010,4))[0])
 print('CANDIDATE PC-relative references; reopen enclosing bodies before accepting stores')
 for a,b,loc in sorted(hits):
  f=n.enclosing(a);print('REFERENCE',hex(a),hex(b),hex(loc),f[3] if f else '')
  n.dump(a,b+24,True)
print('Found',len(hits),'reference candidates')
