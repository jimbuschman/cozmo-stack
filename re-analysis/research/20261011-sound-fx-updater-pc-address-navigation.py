"""Q14-016 exact direct PC-relative address construction; no admission/absence proof."""
from pathlib import Path
import struct,json,hashlib,lief,capstone
path=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=path.read_bytes();elf=lief.parse(str(path))
target=0x9CDFE4;hits=[];counts={"ARM immediate ADD/SUB PC":0,"Thumb ADR16":0,"Thumb ADDW/SUBW PC":0}
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
  if value&~1==target:hits.append({"address":f"{a:08X}","mode":"ARM","bytes":data[j:j+4].hex(),"value":f"{value:08X}","form":"ARM immediate ADD/SUB PC","section":sec.name})
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
   if value&~1==target:hits.append({"address":f"{a:08X}","mode":"Thumb","bytes":data[j:j+size].hex(),"value":f"{value:08X}","form":form,"section":sec.name})
for hit in hits:
 md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM if hit['mode']=='ARM' else capstone.CS_MODE_THUMB)
 hit['decoded']=[i.mnemonic+' '+i.op_str for i in md.disasm(bytes.fromhex(hit['bytes']),int(hit['address'],16))]
r={'engine_sha256':hashlib.sha256(raw).hexdigest(),'target':'009CDFE4','formsExamined':counts,'candidates':hits,'limit':'Encoding and arithmetic candidates only; modes, input bindings, computed/table-relative/copy/foreign call edges are not certified.'}
Path(__file__).with_suffix('.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8')
print(json.dumps(r,indent=2))
