from pathlib import Path
import sys,hashlib,struct
sys.path.insert(0,str(Path('re-analysis/research/_m5013_dependencies').resolve()))
import lief,capstone
from capstone.arm import ARM_OP_MEM,ARM_REG_PC
name=sys.argv[1];p=Path('resources/lib/armeabi-v7a')/name
raw=p.read_bytes();elf=lief.parse(str(p));loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
symbols={s.value&~1:s.name for s in elf.symbols if s.value}
def read(a,n):
 for s in loads:
  if s.virtual_address<=a and a+n<=s.virtual_address+s.physical_size:return raw[a-s.virtual_address+s.file_offset:a-s.virtual_address+s.file_offset+n]
 raise ValueError(hex(a))
print(name,'SHA256',hashlib.sha256(raw).hexdigest())
for arg in sys.argv[2:]:
 if arg.startswith('words:'):
  a,z=(int(v,16) for v in arg[6:].split(':'))
  rel={r.address:r for r in elf.relocations}
  for loc in range(a,z,4):
   value=struct.unpack('<I',read(loc,4))[0];r=rel.get(loc)
   if r and r.has_symbol and r.type.name=='ARM_ABS32':value=(value+r.symbol.value)&0xffffffff
   print(f'{loc:08X}: {value:08X} {r.symbol.name if r and r.has_symbol else symbols.get(value&~1, "")}')
  continue
 if arg=='symbols':
  for s in elf.dynamic_symbols:
   if s.value:print(f'{s.value:08X} {s.size:X} {s.name}')
  continue
 thumb=not arg.startswith('arm:')
 if not thumb:arg=arg[4:]
 a,z=(int(v,16) for v in arg.split(':'))
 print('RANGE',hex(a),hex(z),'exclusive', 'Thumb' if thumb else 'ARM',symbols.get(a,''))
 md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB if thumb else capstone.CS_MODE_ARM);md.detail=True
 for i in md.disasm(read(a,z-a),a):
  notes=[]
  for o in i.operands:
   if o.type==ARM_OP_MEM and o.mem.base==ARM_REG_PC and not o.mem.index:
    loc=(((i.address+4)&~3) if thumb else i.address+8)+o.mem.disp
    try:notes.append(f'word[{loc:08X}]={struct.unpack("<I",read(loc,4))[0]:08X}')
    except ValueError:pass
  if i.mnemonic in ('bl','blx','b','b.w') and i.op_str.startswith('#'):notes.append(symbols.get(int(i.op_str[1:],16)&~1,''))
  print(f'{i.address:08X}: {i.mnemonic:12} {i.op_str}'+(' ; '+'; '.join(x for x in notes if x) if any(notes) else ''))
