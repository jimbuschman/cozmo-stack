"""Q14-104/131 exact A0423C ARM PC-immediate navigation; not reachability proof."""
from pathlib import Path
import lief,struct,json,capstone,hashlib
root=Path(__file__).resolve().parents[2]
path=root/'resources/lib/armeabi-v7a/libcozmoEngine.so'
elf=lief.parse(str(path));out=[]
arm=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM)
for section in elf.sections:
 if not section.has(lief.ELF.Section.FLAGS.EXECINSTR):continue
 data=bytes(section.content)
 for offset in range((-section.virtual_address)%4,len(data)-3,4):
  word=struct.unpack_from('<I',data,offset)[0];address=section.virtual_address+offset;opcode=(word>>21)&15
  if (word>>25)&7!=1 or (word>>16)&15!=15 or opcode not in [2,4]:continue
  value=word&255;rotation=2*((word>>8)&15)
  immediate=((value>>rotation)|(value<<(32-rotation)))&0xffffffff if rotation else value
  target=(address+8+(immediate if opcode==4 else -immediate))&0xffffffff
  if target&~1==0xa0423c:
   instruction=next(arm.disasm(data[offset:offset+4],address),None)
   out.append(dict(address=hex(address),instruction=instruction.mnemonic+' '+instruction.op_str if instruction else None,value=hex(target),rotation=rotation))
Path(__file__).with_suffix('.json').write_text(json.dumps(out,indent=2)+'\n')
print(hashlib.sha256(path.read_bytes()).hexdigest(),len(out),'aligned ARM ADD/SUB PC-immediate candidates; no computed/indirect absence proof')
