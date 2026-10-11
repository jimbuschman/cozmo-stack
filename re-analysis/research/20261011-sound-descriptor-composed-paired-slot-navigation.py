"""Q14-024 ADD-composed ARM paired slot loads; navigation only, no receiver/absence proof."""
from pathlib import Path
import struct,json,hashlib,lief,capstone
from capstone.arm import *
p=Path(__file__).resolve().parents[2]/'resources/lib/armeabi-v7a/libcozmoEngine.so'
e=lief.parse(str(p));md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM);md.detail=True
seeds=[];hits=[];caps=[];loads=[]
for sec in e.sections:
 if not sec.has(lief.ELF.Section.FLAGS.EXECINSTR):continue
 b=bytes(sec.content);v=sec.virtual_address
 for pos in range(max(0,0x97d000-v+3)//4*4,min(len(b)-4,0xabc000-v),4):
  w=struct.unpack_from('<I',b,pos)[0]
  if w&0x0fe00000!=0x02800000:continue
  imm=w&255;r=((w>>8)&15)*2;imm=((imm>>r)|(imm<<(32-r)))&0xffffffff if r else imm
  if imm not in (0x48,0x4c,0x50):continue
  seq=list(md.disasm(b[pos:pos+128],v+pos))
  if not seq or seq[0].id!=ARM_INS_ADD:continue
  first=seq[0];o=first.operands
  if len(o)!=3 or o[0].type!=ARM_OP_REG or o[1].type!=ARM_OP_REG or o[2].type!=ARM_OP_IMM:continue
  seed={'address':f'{first.address:08X}','instruction':first.mnemonic+' '+first.op_str,'displacement':imm};seeds.append(seed)
  ptr={o[0].reg:imm};targets={};ended=False
  for ins in seq[1:]:
   ops=ins.operands
   if ins.id in (ARM_INS_BX,ARM_INS_BLX):
    if ops and ops[0].type==ARM_OP_REG and ops[0].reg in targets:hits.append({'seed':seed,'call':f'{ins.address:08X}','instruction':ins.mnemonic+' '+ins.op_str,'selected':targets[ops[0].reg]})
    ended=True;break
   if ins.id in (ARM_INS_B,ARM_INS_BL,ARM_INS_POP) or ins.id==ARM_INS_LDR and ops and ops[0].type==ARM_OP_REG and ops[0].reg==ARM_REG_PC:ended=True;break
   oldptr=dict(ptr);oldtargets=dict(targets)
   for reg in ins.regs_access()[1]:ptr.pop(reg,None);targets.pop(reg,None)
   if ins.id==ARM_INS_MOV and len(ops)==2 and all(x.type==ARM_OP_REG for x in ops):
    if ops[1].reg in oldptr:ptr[ops[0].reg]=oldptr[ops[1].reg]
    if ops[1].reg in oldtargets:targets[ops[0].reg]=oldtargets[ops[1].reg]
   if ins.id==ARM_INS_LDRD and len(ops)==3 and all(x.type==ARM_OP_REG for x in ops[:2]) and ops[2].type==ARM_OP_MEM and not ops[2].mem.index and ops[2].mem.base in oldptr:
    base=oldptr[ops[2].mem.base];offset=ops[2].mem.disp
    slots=[{'register':ins.reg_name(ops[k].reg),'offset':base+offset+4*k} for k in range(2)]
    selected=[x for x in slots if x['offset'] in (0x4c,0x50)]
    loads.append({'seed':seed['address'],'load':f'{ins.address:08X}','instruction':ins.mnemonic+' '+ins.op_str,'relative_base':base,'slots':slots,'selected_slots':selected})
    for k in range(2):
     slot=base+offset+4*k
     if slot in (0x4c,0x50):targets[ops[k].reg]={'load':f'{ins.address:08X}','slot':slot,'register':ins.reg_name(ops[k].reg)}
    if ins.writeback:ptr[ops[2].mem.base]=base+offset
   if not ptr and not targets:ended=True;break
  if not ended:caps.append(seed)
r={'engine_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'seeds':seeds,'candidates':hits,'window_caps':caps,'selected_pair_loads':loads,'limit':'Aligned ARM ADD48/4C/50 in97D000..ABC000;128-byte straight windows,MOV aliases,unindexed immediate LDRD pairs and first branch/call. Conditional alternatives,mixed modes,Thumb,shifted bases,receiver identity and runtime absence not certified.'}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(r,indent=2)+'\n').encode())
print('seeds',len(seeds),'candidates',len(hits),'caps',len(caps));print(json.dumps(hits,indent=2));print('caps',json.dumps(caps))
