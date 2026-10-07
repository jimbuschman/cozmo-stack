import importlib.util
from pathlib import Path
sp=importlib.util.spec_from_file_location('n',Path('re-analysis/research/20261007-queue5-native.py'));n=importlib.util.module_from_spec(sp);sp.loader.exec_module(n)
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB);md.detail=True;v={}
R=n.capstone.arm
print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
for i in md.disasm(n.read(0x4D86A0,0x640),0x4D86A0):
 o=i.operands
 def val(x):return x.imm if x.type==R.ARM_OP_IMM else v.get(x.reg) if x.type==R.ARM_OP_REG else None
 if i.mnemonic.startswith('ldr') and len(o)>1 and o[1].type==R.ARM_OP_MEM and o[1].mem.base==R.ARM_REG_PC:
  v[o[0].reg]=n.word(((i.address+4)&~3)+o[1].mem.disp)
 elif i.mnemonic.split('.')[0] in ('mov','movs') and len(o)==2:
  x=val(o[1]);v[o[0].reg]=x
 elif i.mnemonic.split('.')[0] in ('add','adds') and len(o)>=2:
  dst=o[0].reg
  if len(o)==2 and o[1].type==R.ARM_OP_REG and o[1].reg==R.ARM_REG_PC:x=(i.address+4)+v[dst] if v.get(dst) is not None else None
  else:
   a=v.get(dst) if len(o)==2 else val(o[1]);b=val(o[-1]);x=a+b if a is not None and b is not None else None
  v[dst]=x
 elif i.mnemonic=='blx':
  if i.op_str=='#0x4a42ac':
   a=v.get(R.ARM_REG_R1);l=v.get(R.ARM_REG_R2);dest=v.get(R.ARM_REG_R6)
   if a and l and 0<l<100:
    print(hex(i.address),'global',hex(dest) if dest else '?','source',hex(a),'len',l,n.read(a,l))
  for r in [R.ARM_REG_R0,R.ARM_REG_R1,R.ARM_REG_R2,R.ARM_REG_R3]:v[r]=None
