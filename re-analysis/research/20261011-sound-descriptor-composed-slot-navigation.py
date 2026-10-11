"""Q14-024 composed ARM slot addresses. Navigation only, no receiver or absence proof."""
from pathlib import Path
import struct,json,hashlib,lief,capstone
from capstone.arm import *
p=Path(__file__).resolve().parents[2]/'resources/lib/armeabi-v7a/libcozmoEngine.so'
e=lief.parse(str(p));md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM);md.detail=True
seeds=[];hits=[];caps=[]
for sec in e.sections:
 if not sec.has(lief.ELF.Section.FLAGS.EXECINSTR):continue
 b=bytes(sec.content);v=sec.virtual_address
 # Actual audio ARM region; mixed embedded words remain navigation candidates.
 for pos in range(max(0,0x97d000-v+3)//4*4,min(len(b)-4,0xabc000-v),4):
  w=struct.unpack_from('<I',b,pos)[0]
  if w&0x0fe00000!=0x02800000:continue
  imm=w&255;r=((w>>8)&15)*2;imm=((imm>>r)|(imm<<(32-r)))&0xffffffff if r else imm
  if imm not in (0x4c,0x50):continue
  ins=list(md.disasm(b[pos:pos+128],v+pos))
  if not ins or ins[0].id!=ARM_INS_ADD:continue
  first=ins[0];o=first.operands
  if len(o)!=3 or o[0].type!=ARM_OP_REG or o[1].type!=ARM_OP_REG or o[2].type!=ARM_OP_IMM:continue
  seed=dict(address=hex(first.address),instruction=first.mnemonic+' '+first.op_str,slot=hex(imm));seeds.append(seed)
  ptr={o[0].reg:imm};targets={};ended=False
  for i in ins[1:]:
   o=i.operands
   if i.id in (ARM_INS_BX,ARM_INS_BLX):
    if o and o[0].type==ARM_OP_REG and o[0].reg in targets:hits.append(dict(seed=seed,load=hex(targets[o[0].reg]),call=hex(i.address),instruction=i.mnemonic+' '+i.op_str))
    ended=True;break
   if i.id in (ARM_INS_B,ARM_INS_BL,ARM_INS_POP) or i.id==ARM_INS_LDR and o and o[0].type==ARM_OP_REG and o[0].reg==ARM_REG_PC:ended=True;break
   oldptr=dict(ptr);oldtargets=dict(targets)
   try:wr=i.regs_access()[1]
   except Exception:wr=[]
   for reg in wr:ptr.pop(reg,None);targets.pop(reg,None)
   if i.id==ARM_INS_MOV and len(o)==2 and o[0].type==ARM_OP_REG and o[1].type==ARM_OP_REG:
    if o[1].reg in oldptr:ptr[o[0].reg]=oldptr[o[1].reg]
    if o[1].reg in oldtargets:targets[o[0].reg]=oldtargets[o[1].reg]
   if i.id==ARM_INS_LDR and len(o)==2 and o[0].type==ARM_OP_REG and o[1].type==ARM_OP_MEM and o[1].mem.base in oldptr and not o[1].mem.index and o[1].mem.disp==0:targets[o[0].reg]=i.address
   if not ptr and not targets:ended=True;break
  if not ended:caps.append(seed)
out=dict(engine_sha256=hashlib.sha256(p.read_bytes()).hexdigest(),limit='Aligned ARM immediate ADD4C/50 seeds in97D000..ABC000,128-byte straight windows, simpleMOV aliases, zero-displacementLDR and firstBX/BLX. No conditional merge, shifted index, block load, Thumb, receiver binding or absence proof.',seeds=seeds,candidates=hits,window_caps=caps)
Path(__file__).with_suffix('.json').write_bytes((json.dumps(out,indent=2)+'\n').encode())
print('seeds',len(seeds),'candidates',len(hits),'caps',len(caps));print(json.dumps(hits,indent=2));print('caps',json.dumps(caps))
