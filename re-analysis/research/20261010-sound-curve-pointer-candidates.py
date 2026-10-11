"""Q14-114 bounded evaluator pointer navigation; no indirect absence proof."""
from pathlib import Path
import sys, struct, hashlib, json
root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(root/'re-analysis/research/_m5013_dependencies'))
import lief
path=root/'resources/lib/armeabi-v7a/libcozmoEngine.so'
raw=path.read_bytes(); elf=lief.parse(str(path)); target=0xA14E28
out={'engineSha256':hashlib.sha256(raw).hexdigest(),'target':f'{target:08X}','scope':'All file-backed PT_LOAD bytes for exact entry/entry+1 words; symbol values; relocation values resolved for ARM_ABS32, GLOB_DAT, JUMP_SLOT and RELATIVE. Navigation only: computed relative pointers, interior entries, stack aliases and external mutation are not excluded.','rawCandidates':[],'symbolCandidates':[],'relocationCandidates':[]}
for seg in elf.segments:
 if seg.type!=lief.ELF.Segment.TYPE.LOAD: continue
 start=seg.file_offset; data=raw[start:start+seg.physical_size]
 for value in (target,target|1):
  needle=struct.pack('<I',value); at=data.find(needle)
  while at>=0:
   out['rawCandidates'].append({'address':f'{seg.virtual_address+at:08X}','fileOffset':start+at,'aligned4':(seg.virtual_address+at)%4==0,'word':f'{value:08X}'})
   at=data.find(needle,at+1)
for sym in elf.symbols:
 if sym.value&~1==target:out['symbolCandidates'].append({'name':sym.name,'value':f'{sym.value:08X}'})
def word(a):
 for seg in elf.segments:
  if seg.type==lief.ELF.Segment.TYPE.LOAD and seg.virtual_address<=a and a+4<=seg.virtual_address+seg.physical_size:
   return struct.unpack_from('<I',raw,seg.file_offset+a-seg.virtual_address)[0]
 return None
for rel in elf.relocations:
 value=word(rel.address); name=rel.type.name
 if value is None:continue
 resolved=None
 if name=='ARM_ABS32' and rel.has_symbol:resolved=(value+rel.symbol.value)&0xffffffff
 elif name in ('ARM_GLOB_DAT','ARM_JUMP_SLOT') and rel.has_symbol:resolved=rel.symbol.value
 elif name=='ARM_RELATIVE':resolved=value
 if (resolved is not None and resolved&~1==target) or (rel.has_symbol and rel.symbol.value&~1==target):
  out['relocationCandidates'].append({'address':f'{rel.address:08X}','type':name,'raw':f'{value:08X}','resolved':None if resolved is None else f'{resolved:08X}','symbol':rel.symbol.name if rel.has_symbol else None})
result=Path(__file__).with_suffix('.json');result.write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in out.items() if k!='scope'},indent=2))
