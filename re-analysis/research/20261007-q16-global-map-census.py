import importlib.util
from pathlib import Path
sp=importlib.util.spec_from_file_location('n',Path('re-analysis/research/20261007-queue5-native.py'));n=importlib.util.module_from_spec(sp);sp.loader.exec_module(n)
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB);md.detail=True
print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
for f in n.index:
 if not 0x4C0000 <= f[0] <0x8CA000:continue
 vals={}
 try:ins=md.disasm(n.read(f[0],f[1]),f[0])
 except ValueError:continue
 for i in ins:
  ops=i.operands
  if i.mnemonic.startswith('ldr') and len(ops)>1 and ops[0].type==n.capstone.arm.ARM_OP_REG and ops[1].type==n.ARM_OP_MEM and ops[1].mem.base==n.ARM_REG_PC:
   loc=((i.address+4)&~3)+ops[1].mem.disp
   try:vals[ops[0].reg]=n.word(loc)
   except ValueError:pass
  elif i.mnemonic=='add' and len(ops)==2 and ops[1].type==n.capstone.arm.ARM_OP_REG and ops[1].reg==n.ARM_REG_PC:
   r=ops[0].reg
   if r in vals:
    v=(vals[r]+i.address+4)&0xffffffff
    if 0x105B360<=v<=0x105B384:print(hex(f[0]),f[3],hex(i.address),hex(v))
    vals[r]=v
  else:
   try:
    for r in i.regs_access()[1]:vals.pop(r,None)
   except Exception:pass
