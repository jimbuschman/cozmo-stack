"""Q14-024 omitted LDR-PC transfer form only, no new object family or absence proof."""
from pathlib import Path
import lief, struct, json, hashlib, capstone
from capstone.arm import *
p=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');elf=lief.parse(str(p))
md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM);md.detail=True
direct=[]; composed=[]
for sec in elf.sections:
 if not sec.has(lief.ELF.Section.FLAGS.EXECINSTR):continue
 b=bytes(sec.content);v=sec.virtual_address
 for pos in range(max(0,0x97d000-v+3)//4*4,min(len(b)-3,0xabc000-v),4):
  w=struct.unpack_from('<I',b,pos)[0]
  if w&0x0c100000!=0x04100000 or (w>>12)&15!=15:continue
  ins=next(md.disasm(b[pos:pos+4],v+pos),None)
  if not ins or ins.id!=ARM_INS_LDR or len(ins.operands)!=2:continue
  dst,src=ins.operands
  if dst.type!=ARM_OP_REG or dst.reg!=ARM_REG_PC or src.type!=ARM_OP_MEM or src.mem.index:continue
  mem=src.mem
  if mem.disp in (0x4c,0x50):direct.append({'address':f'{ins.address:08X}','instruction':ins.mnemonic+' '+ins.op_str,'base':ins.reg_name(mem.base),'displacement':mem.disp})
  if mem.disp==0:composed.append({'address':f'{ins.address:08X}','instruction':ins.mnemonic+' '+ins.op_str,'base':ins.reg_name(mem.base)})
result={'engine_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'direct_slot_pc_loads':direct,'zero_displacement_pc_loads':composed,'boundary':'Aligned ARM97D000..ABC000 LDR-PC immediate unindexed4C/50 and zero-displacement forms only; raw candidates can be mixed words. Direct PC transfer was a stop in842/861. No actual receiver, preceding composed ADD, branch alternatives, Thumb or all-input exclusion proof.'}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
print('direct slot LDR-PC',len(direct),'zero-displacement LDR-PC',len(composed));print(json.dumps(result,indent=2))
