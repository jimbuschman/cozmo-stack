"""Original3 helper interfaces,10 shipped entries: bounded argument-register entry witnesses only."""
from pathlib import Path
import lief,capstone,json,hashlib
from capstone.arm import *
p=Path(__file__).parent;defs=json.loads((p/'20261011-sound-rate-frame-live-helper-definitions.json').read_text(encoding='utf-8'))['definitions']
rows=[];lines=[]
for item in defs:
 path=Path('resources/lib/armeabi-v7a')/item['library'];raw=path.read_bytes();elf=lief.parse(str(path))
 start=int(item['value'],16)&~1;thumb=bool(int(item['value'],16)&1)
 def read(a,size):
  for seg in elf.segments:
   if seg.type==lief.ELF.Segment.TYPE.LOAD and seg.virtual_address<=a and a+size<=seg.virtual_address+seg.physical_size:
    off=seg.file_offset+a-seg.virtual_address;return raw[off:off+size]
  raise ValueError(a)
 md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB if thumb else capstone.CS_MODE_ARM);md.detail=True
 killed=set();observed=[];first_reads=[];stop='64-byte prefix limit';lines.append(item['library']+' '+item['name']+f' {start:08X} '+('Thumb' if thumb else 'ARM'))
 for ins in md.disasm(read(start,64),start):
  reads,writes=ins.regs_access();newreads=set(reads)&{ARM_REG_R2,ARM_REG_R3}-killed
  if newreads:first_reads.append({'address':f'{ins.address:08X}','registers':[ins.reg_name(x) for x in sorted(newreads)],'instruction':ins.mnemonic+' '+ins.op_str})
  unconditional=ins.cc in (ARM_CC_AL,ARM_CC_INVALID)
  if unconditional:killed.update(set(writes)&{ARM_REG_R2,ARM_REG_R3})
  observed.append({'address':f'{ins.address:08X}','bytes':ins.bytes.hex(),'instruction':ins.mnemonic+' '+ins.op_str,'new_incoming_register_reads':[ins.reg_name(x) for x in sorted(newreads)],'unconditional_definitions':[ins.reg_name(x) for x in sorted(set(writes)&{ARM_REG_R2,ARM_REG_R3})] if unconditional else []})
  lines.append(f'{ins.address:08X} {ins.bytes.hex()} {ins.mnemonic} {ins.op_str}')
  if killed=={ARM_REG_R2,ARM_REG_R3}:stop='both tracked registers unconditionally defined';break
  if ins.id in (ARM_INS_B,ARM_INS_BL,ARM_INS_BLX,ARM_INS_BX,ARM_INS_CBZ,ARM_INS_CBNZ) or ins.id==ARM_INS_POP and any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in ins.operands):stop='first control frontier';break
 rows.append({'library':item['library'],'library_sha256':hashlib.sha256(raw).hexdigest(),'symbol':item['name'],'entry':f'{start:08X}','mode':'Thumb' if thumb else 'ARM','instructions':observed,'incoming_reads_before_unconditional_definition':first_reads,'defined_registers':[md.reg_name(x) for x in sorted(killed)],'stop':stop,'limit':'Straight-line entry witness only; conditional branches,tails,callee inputs,private register saves and arithmetic bodies are not settled.'})
 lines.append('STOP '+stop)
r={'scope':'Exactly10 existing helper definitions; argument-entry bytes only,not runtime binding or whole-callee alias absence.','entries':rows}
Path(__file__).with_suffix('.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8')
Path(__file__).with_suffix('.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8')
for x in rows:print(x['library'],x['symbol'],x['defined_registers'],x['incoming_reads_before_unconditional_definition'],x['stop'])
