"""Specific Q14-024 4C/50 call navigation, wide Thumb loads/tails and register clobber checks. Not receiver identity or absence proof."""
from pathlib import Path
import sys,struct,json,hashlib,importlib.util
sys.argv.append('--light')
s=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'));n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
from capstone.arm import *
import lief
elf=lief.parse(str(n.path))
# Restrict encoding navigation to primary ELF executable sections.
n.loads=[(x.virtual_address,x.size,x.offset) for x in elf.sections if x.has(lief.ELF.Section.FLAGS.EXECINSTR)]
result=[];seeds=0
for thumb,step in ((False,4),(True,2)):
 md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB if thumb else n.capstone.CS_MODE_ARM);md.detail=True
 for va,size,off in n.loads:
  if va>=0xF00000 or va+size<=0x4D6800:continue
  b=n.read(va,size);start=max(0,0x4D6800-va)
  for pos in range((start+step-1)//step*step,size-80,step):
   w=struct.unpack_from('<I',b,pos)[0]
   if not thumb:
    if w&0x0F700000!=0x05100000 or w&0xfff not in (0x4c,0x50):continue
   else:
    h=w&0xffff;hi=w>>16
    narrow=h&0xf800==0x6800 and ((h>>6)&31)*4 in (0x4c,0x50)
    wide=h&0xfff0==0xf8d0 and hi&0xfff in (0x4c,0x50)
    if not(narrow or wide):continue
   ins=list(md.disasm(b[pos:pos+80],va+pos));seeds+=1
   if not ins or ins[0].id!=ARM_INS_LDR or len(ins[0].operands)<2:continue
   first=ins[0];o=first.operands
   if o[0].type!=ARM_OP_REG or o[1].type!=ARM_OP_MEM or o[1].mem.disp not in (0x4c,0x50) or o[1].mem.index:continue
   aliases={o[0].reg}
   for i in ins[1:]:
    ops=i.operands
    if i.id in (ARM_INS_BLX,ARM_INS_BX) and ops and ops[0].type==ARM_OP_REG:
     if ops[0].reg in aliases:result.append(dict(load=hex(first.address),call=hex(i.address),mode='Thumb' if thumb else 'ARM',slot=hex(o[1].mem.disp),loadInstruction=first.mnemonic+' '+first.op_str,callInstruction=i.mnemonic+' '+i.op_str))
     break
    if i.id in (ARM_INS_BL,ARM_INS_BLX):break
    if i.id in (ARM_INS_B,ARM_INS_POP) or i.id==ARM_INS_LDR and ops and ops[0].type==ARM_OP_REG and ops[0].reg==ARM_REG_PC:break
    before=set(aliases)
    try:_,written=i.regs_access()
    except Exception:written=[]
    aliases.difference_update(written)
    if i.id==ARM_INS_MOV and len(ops)==2 and ops[0].type==ARM_OP_REG and ops[1].type==ARM_OP_REG and ops[1].reg in before:aliases.add(ops[0].reg)
    if not aliases:break
out=dict(engineSha256=hashlib.sha256(n.raw).hexdigest(),limit='Encoded immediate word loads4C/50 in both alignments, 80-byte linear window, move aliases/clobbers, BX/BLX tails. Mixed-mode/data false candidates remain; no receiver identity, complete path merge, indirect absence or unreachability proof.',seedCount=seeds,candidates=result)
Path(__file__).with_suffix('.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print('seeds',seeds,'call candidates',len(result));print(json.dumps(result,indent=2))
