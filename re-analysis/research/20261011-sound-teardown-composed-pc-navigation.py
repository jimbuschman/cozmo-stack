"""Q14-084/118 composed nearby-PC address seeds; unchanged812 finite propagation, no admission/absence proof."""
from pathlib import Path
import struct,json,hashlib,lief,capstone
path=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=path.read_bytes();elf=lief.parse(str(path))
targets=(0xA0228C,0xA022E8,0xA370E4,0xA55A84,0xA55D04);hits=[];counts={"ARM immediate ADD/SUB PC":0,"Thumb ADR16":0,"Thumb ADDW/SUBW PC":0}
for sec in elf.sections:
 if not sec.has(lief.ELF.Section.FLAGS.EXECINSTR):continue
 data=raw[sec.offset:sec.offset+sec.size];base=sec.virtual_address
 for j in range(0,len(data)-3,4):
  w=struct.unpack_from('<I',data,j)[0];a=base+j
  if w&0x0E000000!=0x02000000 or (w>>16)&15!=15 or (w>>21)&15 not in (2,4):continue
  counts["ARM immediate ADD/SUB PC"]+=1
  imm=w&255;rot=((w>>8)&15)*2
  if rot:imm=((imm>>rot)|(imm<<(32-rot)))&0xffffffff
  op=(w>>21)&15;value=(a+8+(imm if op==4 else -imm))&0xffffffff
  if any(t-512 <= (value&~1) <= t+512 for t in targets):hits.append({"address":f"{a:08X}","mode":"ARM","bytes":data[j:j+4].hex(),"value":f"{value:08X}","form":"ARM immediate ADD/SUB PC","section":sec.name})
 for j in range(0,len(data)-3,2):
  h=struct.unpack_from('<H',data,j)[0];a=base+j;value=None;form=None;size=None
  if h&0xF800==0xA000:
   form="Thumb ADR16";size=2;value=((a+4)&~3)+((h&255)<<2)
  elif h&0xFBFF in (0xF20F,0xF2AF):
   hh=struct.unpack_from('<H',data,j+2)[0]
   if hh&0x8000:continue
   form="Thumb ADDW/SUBW PC";size=4
   imm=((h&0x400)<<1)|((hh&0x7000)>>4)|(hh&255)
   value=(((a+4)&~3)+(imm if h&0xFBFF==0xF20F else -imm))&0xffffffff
  if form:
   counts[form]+=1
   if any(t-512 <= (value&~1) <= t+512 for t in targets):hits.append({"address":f"{a:08X}","mode":"Thumb","bytes":data[j:j+size].hex(),"value":f"{value:08X}","form":form,"section":sec.name})

# Read only existing model functions; do not rerun full GOT/MOVW seed census.
import contextlib,io
model=Path(__file__).with_name('20261010-sound-weight-formed-pointer-navigation.py')
model_source=model.read_text(encoding='utf-8').split('for va,size,off in exec_sections:')[0]
ns={'__file__':str(model.resolve())}
with contextlib.redirect_stdout(io.StringIO()):exec(compile(model_source,str(model),'exec'),ns)
ns['targets']=targets;ns['events']={};log=io.StringIO();seeds=[]
for hit in hits:
 hit['seed_target_match']=(int(hit['value'],16)&~1) in targets
 md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM if hit['mode']=='ARM' else capstone.CS_MODE_THUMB);md.detail=True
 decoded=list(md.disasm(bytes.fromhex(hit['bytes']),int(hit['address'],16)))
 hit['decoded']=[i.mnemonic+' '+i.op_str for i in decoded]
 if len(decoded)!=1 or not decoded[0].operands or decoded[0].operands[0].type!=ns['ARM_OP_REG']:
  hit['propagation']='No decoded scalar destination; encoding-only candidate';continue
 ins=decoded[0];reg=ins.operands[0].reg
 if reg==ns['ARM_REG_PC']:
  hit['propagation']='PC destination is not a pointer register seed; direct seed target remains separately checked by810';continue
 # Explicit raw ADR arithmetic supplies only the first instruction destination.
 # Conditional or mixed-mode candidates are possible navigation, not admission.
 start=ins.address+ins.size
 hit['propagation_start']=f'{start:08X}';hit['destination']=ins.reg_name(reg)
 seeds.append(hit)
 with contextlib.redirect_stdout(log):ns['execute'](ns['n'].read(start,768),0,start,hit['mode']=='Thumb',{reg:int(hit['value'],16)})
for e in ns['events'].values():e['seeds']=sorted(set(e['seeds']))
r={'engine_sha256':hashlib.sha256(raw).hexdigest(),'targets':[f'{t:08X}' for t in targets],'formsExamined':counts,'nearby_seed_count':len(hits),'propagation_count':len(seeds),'seeds':hits,'events':list(ns['events'].values()),'cap_log':log.getvalue().splitlines(),'model_sha256':hashlib.sha256(model.read_bytes()).hexdigest(),'limit':'Only direct-PC seed values within512 bytes either side of original teardown inputs then unchanged812 local propagation; explicit destination seed, possible modes/conditions, distant address construction, stack/copy/foreign inputs and caps are not certified.'}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(r,indent=2)+'\n').encode())
print(json.dumps({'nearby_seed_count':len(hits),'propagation_count':len(seeds),'events':r['events'],'cap_log':r['cap_log']},indent=2))
