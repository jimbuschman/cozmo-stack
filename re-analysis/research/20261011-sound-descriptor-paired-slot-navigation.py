"""Q14-024 selected ARM LDRD slot pairs, navigation only; no receiver/absence proof."""
from pathlib import Path
import struct,json,hashlib,lief,capstone
from capstone.arm import *
p=Path(__file__).resolve().parents[2]/'resources/lib/armeabi-v7a/libcozmoEngine.so'
e=lief.parse(str(p));md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM);md.detail=True
seeds=[];hits=[];caps=[]
for sec in e.sections:
 if not sec.has(lief.ELF.Section.FLAGS.EXECINSTR):continue
 b=bytes(sec.content);v=sec.virtual_address
 for pos in range(max(0,0x97d000-v+3)//4*4,min(len(b)-4,0xabc000-v),4):
  w=struct.unpack_from('<I',b,pos)[0]
  if w&0x0e1000f0!=0x000000d0:continue
  seq=list(md.disasm(b[pos:pos+128],v+pos))
  if not seq or seq[0].id!=ARM_INS_LDRD:continue
  first=seq[0];o=first.operands
  if len(o)!=3 or any(x.type!=ARM_OP_REG for x in o[:2]) or o[2].type!=ARM_OP_MEM:continue
  mem=o[2].mem
  if mem.index or mem.disp not in (0x48,0x4c,0x50):continue
  seed={'address':f'{first.address:08X}','instruction':first.mnemonic+' '+first.op_str,'base':first.reg_name(mem.base),'displacement':mem.disp,'slots':[{'register':first.reg_name(o[k].reg),'offset':mem.disp+4*k} for k in range(2)]};seeds.append(seed)
  targets={o[k].reg:{'load':f'{first.address:08X}','slot':mem.disp+4*k} for k in range(2) if mem.disp+4*k in (0x4c,0x50)};ended=False
  for ins in seq[1:]:
   ops=ins.operands
   if ins.id in (ARM_INS_BX,ARM_INS_BLX):
    if ops and ops[0].type==ARM_OP_REG and ops[0].reg in targets:hits.append({'seed':seed,'call':f'{ins.address:08X}','instruction':ins.mnemonic+' '+ins.op_str,'selected':targets[ops[0].reg]})
    ended=True;break
   if ins.id in (ARM_INS_B,ARM_INS_BL,ARM_INS_POP) or ins.id==ARM_INS_LDR and ops and ops[0].type==ARM_OP_REG and ops[0].reg==ARM_REG_PC:ended=True;break
   old=dict(targets)
   for reg in ins.regs_access()[1]:targets.pop(reg,None)
   if ins.id==ARM_INS_MOV and len(ops)==2 and all(x.type==ARM_OP_REG for x in ops) and ops[1].reg in old:targets[ops[0].reg]=old[ops[1].reg]
   if not targets:ended=True;break
  if not ended:caps.append(seed)
r={'engine_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'seeds':seeds,'candidates':hits,'window_caps':caps,'limit':'Aligned ARM LDRD immediate48/4C/50 in97D000..ABC000;128-byte straight windows,MOV aliases,first branch/call stops. Conditional alternatives,mixed modes,Thumb,composed bases,block loads,receiver identity and runtime absence not certified.'}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(r,indent=2)+'\n').encode())
print('seeds',len(seeds),'candidates',len(hits),'caps',len(caps));print(json.dumps(hits,indent=2));print('caps',json.dumps(caps))
