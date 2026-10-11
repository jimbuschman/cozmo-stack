"""Q14-033 related voice-family near-field writer/address navigation; not global alias proof."""
from pathlib import Path
import lief,capstone,hashlib,json
from capstone.arm import *
p=Path(__file__).resolve().parents[2]/'resources/lib/armeabi-v7a/libcozmoEngine.so';e=lief.parse(str(p));md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM);md.detail=True;md.skipdata=True;out=[]
lo,hi=0xa53000,0xa56600
sec=next(s for s in e.sections if s.virtual_address<=lo and hi<=s.virtual_address+s.size)
for i in md.disasm(bytes(sec.content)[lo-sec.virtual_address:hi-sec.virtual_address],lo):
 if i.id==0:continue
 mn=i.mnemonic.split('.')[0];kind=None
 if mn.startswith(('str','vstr','stm','vstm')):
  for o in i.operands:
   if o.type==ARM_OP_MEM and 0xd8<=o.mem.disp<=0xe4:kind='near-field-store'
 if mn.startswith(('add','sub')) and any(o.type==ARM_OP_IMM and o.imm in (0xe0,0xe4) for o in i.operands):kind='near-field-address'
 if kind:out.append(dict(address=hex(i.address),instruction=i.mnemonic+' '+i.op_str,bytes=i.bytes.hex(),kind=kind))
r=dict(sha256=hashlib.sha256(p.read_bytes()).hexdigest(),range=[hex(lo),hex(hi)],limit='Aligned ARM related-method-region near-field navigation only. Literal/mode/receiver identity, composed offsets, arbitrary register block stores, copies, computed callers and escaped aliases remain unproven.',candidates=out)
with Path(__file__).with_suffix('.json').open('w',encoding='utf-8') as f:json.dump(r,f,indent=2)
print(json.dumps(out,indent=2))
