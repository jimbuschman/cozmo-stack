"""Bounded ARM PC-relative candidate census for shared seek-rate input; no absence proof."""
from pathlib import Path
import sys,struct,hashlib,importlib.util
sys.argv.append('--light')
spec=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'));n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
print('ENGINE SHA256',hashlib.sha256(n.raw).hexdigest())
print('Targets: rate GOT1040068; shared rate/frame/timing block105243C..105244C')
targets={0x1040068,*range(0x105243c,0x1052450,4)}
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_ARM);md.detail=True
for va,size,off in n.loads:
 if va>0x95e540 or va+size<=0x95e540:continue
 data=n.read(va,size)
 for j in range(max(0,0x95e540-va),size-68,4):
  w=struct.unpack_from('<I',data,j)[0];a=va+j
  if w&0x0f7f0000!=0x051f0000:continue
  rd=(w>>12)&15;loc=a+8+(w&0xfff)*(1 if w&(1<<23) else -1)
  try:literal=n.word(loc)
  except ValueError:continue
  for delta in range(4,65,4):
   a2=a+delta;endpoint=(a2+8+literal)&0xffffffff
   if endpoint not in targets:continue
   w2=struct.unpack_from('<I',data,j+delta)[0]
   # ADD register with pc and literal register, or LDR register [pc,literal register]; no shifts.
   add=(w2&0x0fe00ff0)==0x00800000 and (((w2>>16)&15)==15 and (w2&15)==rd or ((w2>>16)&15)==rd and (w2&15)==15)
   ldr=(w2&0x0f7f0ff0)==0x071f0000 and w2&15==rd
   if not (add or ldr):continue
   regid=getattr(n.capstone.arm,'ARM_REG_R'+str(rd)) if rd<13 else {13:n.capstone.arm.ARM_REG_SP,14:n.capstone.arm.ARM_REG_LR,15:n.capstone.arm.ARM_REG_PC}[rd]
   middle=list(md.disasm(data[j+4:j+delta],a+4))
   if sum(i.size for i in middle)!=delta-4:continue
   if any(regid in i.regs_access()[1] or i.mnemonic in ('bl','blx') and rd in (0,1,2,3,12,14) for i in middle):continue
   print(f'candidate literal-load {a:08X} reg{rd} word[{loc:08X}]={literal:08X}; join{a2:08X} raw={w2:08X} -> {endpoint:08X}')
   dest=(w2>>12)&15
   destid=getattr(n.capstone.arm,'ARM_REG_R'+str(dest)) if dest<13 else {13:n.capstone.arm.ARM_REG_SP,14:n.capstone.arm.ARM_REG_LR,15:n.capstone.arm.ARM_REG_PC}[dest]
   for ins in md.disasm(data[j+delta+4:j+delta+68],a2+4):
    for operand in ins.operands:
     if operand.type==n.capstone.arm.ARM_OP_MEM and operand.mem.base==destid:
      print(f'  local-memory {ins.address:08X}: {ins.mnemonic} {ins.op_str}')
    if destid in ins.regs_access()[1] or ins.mnemonic in ('b','bx','bl','blx'):break

print('LIMIT: ARM candidate pattern only, 64-byte join window and register-kill filtering; no indirect/Thumb/MOVW/MOVT/other-base absence claim.')
