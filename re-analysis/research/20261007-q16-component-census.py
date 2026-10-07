"""Queue 5 candidate census, not a proof of pointer alias absence."""
import importlib.util,sys
from pathlib import Path
spec=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'));n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB);md.detail=True
print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
print('Indexed Thumb extents; pools may decode as instructions. Whole-function candidates, no 45-instruction cutoff. Not general alias proof.')
count=0
for f in n.index:
 a,z=f[0],f[0]+f[1]
 if not 0x4e0000<=a<0x8ca000:continue
 try:ins=list(md.disasm(n.read(a,z-a),a))
 except ValueError:continue
 fetch=[i for i in ins if i.mnemonic.startswith('ldr') and any(o.type==n.ARM_OP_MEM and o.mem.disp==0x264 and o.mem.base not in (n.capstone.arm.ARM_REG_PC,n.capstone.arm.ARM_REG_SP) for o in i.operands)]
 stores70=[i for i in ins if i.mnemonic.startswith('str') and any(o.type==n.ARM_OP_MEM and o.mem.disp==0x70 and o.mem.base not in (n.capstone.arm.ARM_REG_PC,n.capstone.arm.ARM_REG_SP) for o in i.operands)]
 # Fetch component within entire function plus any byte store to +4, or any word write to +70 and AI fetch.
 stores4=[i for i in ins if i.mnemonic.startswith('strb') and any(o.type==n.ARM_OP_MEM and o.mem.disp==4 and o.mem.base not in (n.capstone.arm.ARM_REG_PC,n.capstone.arm.ARM_REG_SP) for o in i.operands)]
 if fetch:
  print('FETCH264',hex(a),hex(z),f[3],','.join(hex(i.address) for i in fetch))
 if fetch and (stores4 or stores70):
  print('CANDIDATE',hex(a),f[3])
  for i in fetch+stores4+stores70:print(hex(i.address),i.mnemonic,i.op_str)
  count+=1
print('CANDIDATE COUNT',count)
