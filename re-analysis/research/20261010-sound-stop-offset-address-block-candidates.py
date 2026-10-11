"""Q14-119 bounded address/block-write navigation for the original PBI1F8 census; never absence proof."""
from pathlib import Path
import lief,capstone,json,hashlib
from capstone.arm import *
p=Path(__file__).resolve().parents[2]/'resources/lib/armeabi-v7a/libcozmoEngine.so'
e=lief.parse(str(p));out=[]
for sec in e.sections:
 if not sec.has(lief.ELF.Section.FLAGS.EXECINSTR): continue
 va=sec.virtual_address;d=bytes(sec.content)
 # Known principal Thumb/ARM domains; mixed mode and literal islands remain navigation uncertainty.
 for lo,hi,mode,label in [(0x4a0000,0x95e540,capstone.CS_MODE_THUMB,'Thumb'),(0x95e540,0xa6cf00,capstone.CS_MODE_ARM,'ARM')]:
  start=max(lo,va);end=min(hi,va+len(d))
  if end<=start: continue
  md=capstone.Cs(capstone.CS_ARCH_ARM,mode);md.detail=True;md.skipdata=True
  for i in md.disasm(d[start-va:end-va],start):
   if i.id==0: continue
   ops=i.operands;mn=i.mnemonic.split('.')[0]
   if mn.startswith(('add','sub')) and any(o.type==ARM_OP_IMM and o.imm in (0x1f0,0x1f4,0x1f8,0x1fc) for o in ops):
    out.append(dict(address=hex(i.address),mode=label,instruction=i.mnemonic+' '+i.op_str,kind='near-field-address-arithmetic'))
   if mn.startswith(('strd','vstr','vstm','stm')):
    for o in ops:
     if o.type==ARM_OP_MEM and 0x1e0<=abs(o.mem.disp)<=0x200:
      out.append(dict(address=hex(i.address),mode=label,instruction=i.mnemonic+' '+i.op_str,kind='near-field-wide-store'))
r=dict(sha256=hashlib.sha256(p.read_bytes()).hexdigest(),target='PBI1F8',limit='Bounded near-field immediates and wide-memory encodings in principal mode regions. Navigation only; mixed-mode/literal false candidates, composed offsets, arbitrary register-index/block writes and escaped pointer mutation UNKNOWN.',candidates=out)
Path(__file__).with_suffix('.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(candidates=len(out),sha256=r['sha256'],limit=r['limit']),indent=2))
