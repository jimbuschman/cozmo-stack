"""Original four-key ELF image declaration only, before program initialization.
Raw ELF32 headers cross-checked against LIEF; no loader/constructor implementation or runtime-zero proof.
"""
from pathlib import Path
import struct,json,hashlib,lief
p=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=p.read_bytes();e=lief.parse(str(p));lo=0x108d9a0;hi=lo+16
h=struct.unpack_from('<16sHHIIIIIHHHHHH',raw,0)
assert h[0][:7]==b'\x7fELF\x01\x01\x01' and h[2]==40 and h[9]==32 and h[11]==40
programs=[]
for i in range(h[10]):
 off=h[5]+32*i;kind,offset,va,pa,filesz,memsz,flags,align=struct.unpack_from('<8I',raw,off)
 if kind==1 and va<=lo and hi<=va+memsz:programs.append({'header_file_offset':off,'virtual_start':va,'file_offset':offset,'file_size':filesz,'memory_size':memsz,'flags':flags,'file_backed_end':va+filesz,'memory_end':va+memsz})
assert len(programs)==1;seg=programs[0];assert seg['file_backed_end']<=lo and hi<=seg['memory_end'] and seg['flags']&2
lh=next(s for s in e.segments if s.type==lief.ELF.Segment.TYPE.LOAD and s.virtual_address==seg['virtual_start'])
assert lh.file_offset==seg['file_offset'] and lh.physical_size==seg['file_size'] and lh.virtual_size==seg['memory_size']
sections=[struct.unpack_from('<10I',raw,h[6]+40*i) for i in range(h[12])];strings=sections[h[13]];names=raw[strings[4]:strings[4]+strings[5]]
def name(s):return names[s[0]:].split(b'\0',1)[0].decode('utf-8')
owners=[s for s in sections if s[3]<=lo and hi<=s[3]+s[5] and s[2]&2];assert len(owners)==1
s=owners[0];assert name(s)=='.bss' and s[1]==8 and s[2]&1
near=[];copies=[];raw_relocations=0;rel_sections=[]
for s in sections:
 if s[1]!=9:continue
 assert s[9]==8 and s[5]%8==0
 rel_sections.append({'name':name(s),'address':s[3],'file_offset':s[4],'bytes':s[5],'records':s[5]//8})
 for off in range(s[4],s[4]+s[5],8):
  addr,info=struct.unpack_from('<II',raw,off);raw_relocations+=1
  if lo-64<=addr<hi+64:near.append({'address':addr,'raw_info':info})
  if info&255==20:copies.append({'address':addr,'raw_info':info})
assert raw_relocations==len(list(e.relocations))==52521 and not near and not copies
dyn_symbols=[]
for s in sections:
 if s[1]!=11:continue
 strings=sections[s[6]];stringsraw=raw[strings[4]:strings[4]+strings[5]];assert s[9]==16
 for off in range(s[4],s[4]+s[5],16):
  no,va,size,info,other,index=struct.unpack_from('<IIIBBH',raw,off)
  if index and (lo<=va<hi or size and va<hi and lo<va+size):dyn_symbols.append({'name':stringsraw[no:].split(b'\0',1)[0].decode('utf-8'),'value':va,'size':size,'binding':info>>4,'visibility':other&3})
init=next(s for s in sections if name(s)=='.init_array');assert init[5]==0x1f4 and init[5]%4==0
entries=list(struct.unpack_from('<'+'I'*(init[5]//4),raw,init[4]));assert len(entries)==125
result={'engine_sha256':hashlib.sha256(raw).hexdigest(),'scope':'Four-key mapping declaration before any initialization, raw ELF32 program/section/symbol/REL headers cross-checked against LIEF. No constructor/JNI/Init/caller/mutation execution, all writer absence, post-init runtime-zero or encrypted-input exclusion proof.','key_range':['0108D9A0','0108D9B0'],'containing_LOAD':seg,'section':{'name':'.bss','type_raw':8,'flags_raw':owners[0][2],'virtual_start':owners[0][3],'size':owners[0][5],'file_offset_field':owners[0][4]},'declared_zero_fill_key_words_before_initialization':['00000000']*4,'format_definition':'https://gabi.xinuos.com/elf/07-pheader.html#segment-types','relocation_sections':rel_sections,'raw_REL_count':raw_relocations,'relocation_offsets_within64_bytes':near,'R_ARM_COPY_records':copies,'defined_dynamic_symbols_overlapping_key_bytes':dyn_symbols,'initialization_array':{'address':init[3],'file_offset':init[4],'byte_size':init[5],'raw_entries':entries,'entry_count':len(entries),'metadata_only_not_executed':True}}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
print('Key bytes in declared zero-fill tail; writable.bss; no nearby REL/COPY.125 initializer metadata entries retained, not executed.')
print('Dynamic symbol overlap',dyn_symbols)
