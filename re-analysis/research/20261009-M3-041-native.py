from pathlib import Path
import lief, capstone, struct, hashlib, sys
from capstone.arm import *
P=Path(sys.argv[1] if len(sys.argv)>1 else 'resources/lib/armeabi-v7a/libcozmoEngine.so'); raw=P.read_bytes(); elf=lief.parse(str(P)); loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]; rel={r.address:r for r in elf.relocations}; syms={s.name:s for s in elf.symbols if s.value}; byva={s.value&~1:s for s in syms.values()}; md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB); md.detail=True

def read(a,n):
 for s in loads:
  if s.virtual_address<=a and a+n<=s.virtual_address+s.physical_size: return raw[s.file_offset+a-s.virtual_address:s.file_offset+a-s.virtual_address+n]
 return b''
def word(a): return int.from_bytes(read(a,4),'little')
def string(a):
 b=bytearray()
 for k in range(256):
  c=read(a+k,1)
  if not c or c==b'\0':break
  b+=c
 return bytes(b)
def plt(a):
 b=read(a,12)
 if len(b)!=12:return None
 w=struct.unpack('<3I',b)
 if (w[0]&0xfffff000)!=0xe28fc000 or (w[1]&0xfffff000)!=0xe28cc000 or (w[2]&0xfffff000)!=0xe5bcf000:return None
 def imm(x):
  v=x&255; r=((x>>8)&15)*2
  return ((v>>r)|(v<<(32-r)))&0xffffffff if r else v
 got=a+8+imm(w[0])+imm(w[1])+(w[2]&4095); r=rel.get(got)
 return (got,r.symbol if r and r.has_symbol else None)
def dump(a,z):
 print(f'\nRANGE {a:08X}..{z:08X} '+(byva[a].name if a in byva else ''))
 for i in md.disasm(read(a,z-a),a):
  ann=[]
  for o in i.operands:
   if o.type==ARM_OP_MEM and o.mem.base==ARM_REG_PC:
    at=((i.address+4)&~3)+o.mem.disp; ann.append(f'literal[{at:08X}]={word(at):08X}')
   if o.type==ARM_OP_IMM and i.mnemonic in ('bl','blx'):
    p=plt(o.imm); s=p[1] if p else byva.get(o.imm&~1)
    if s:ann.append(f'{s.name} -> {s.value&~1:08X} size={s.size:X}' if s.value else s.name+' IMPORT (resolve packaged dependencies before calling external)')
   if o.type==ARM_OP_IMM and i.mnemonic=='adr':
    at=((i.address+4)&~3)+o.imm;ann.append(f'ADR[{at:08X}]={string(at)!r}')
   if o.type==ARM_OP_IMM and i.mnemonic=='addw' and any(x.type==ARM_OP_REG and x.reg==ARM_REG_PC for x in i.operands):
    at=((i.address+4)&~3)+o.imm;ann.append(f'ADDW[{at:08X}]={string(at)!r}')
  print(f'{i.address:08X}: {i.mnemonic:10s} {i.op_str}'+(' ; '+'; '.join(ann) if ann else ''))
if __name__=='__main__':
 import sys
 print('Primary',P,'SHA256',hashlib.sha256(raw).hexdigest());print('Arguments',repr(sys.argv[1:]))
 if len(sys.argv)>1:
  for arg in sys.argv[2:]:
   arm=arg.startswith('A');arg=arg[1:] if arm else arg
   md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM if arm else capstone.CS_MODE_THUMB);md.detail=True
   a,z=[int(v,16) for v in arg.split(':')];dump(a,z)
  sys.exit(0)
 for a,z in [(0x535c50,0x536114),(0x536114,0x536678),(0x532944,0x532a7c),(0x53779e,0x5377d0),(0x538a3c,0x538b22),(0x5010b4,0x501156),(0x501398,0x501568),(0x802824,0x80284e),(0x537e48,0x537f18),(0x4e437c,0x4e44be),(0x501684,0x5017b0),(0x501210,0x5012a8),(0x4e3f06,0x4e4020)]:dump(a,z)
 print('\nREOPENED CALLEE CENSUS')
 seen=set()
 if len(sys.argv)>1:
  for arg in sys.argv[2:]:
   arm=arg.startswith('A');arg=arg[1:] if arm else arg
   md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM if arm else capstone.CS_MODE_THUMB);md.detail=True
   a,z=[int(v,16) for v in arg.split(':')];dump(a,z)
  sys.exit(0)
 for a,z in [(0x535c50,0x535f7a),(0x536114,0x5364cc)]:
  for i in md.disasm(read(a,z-a),a):
   if i.mnemonic in ('bl','blx') and i.operands[0].type==ARM_OP_IMM:
    t=i.operands[0].imm;p=plt(t);s=p[1] if p else byva.get(t&~1)
    if s and s.value and (s.value&~1) not in seen:
     seen.add(s.value&~1);dump(s.value&~1,(s.value&~1)+s.size)
 print('\nVTABLE BINDINGS')
 for a in range(0x101f2bc,0x101f2fc,4):
  r=rel.get(a);t=r.symbol.value if r and r.has_symbol and r.symbol.name and r.symbol.value else word(a)
  print(f'{a:08X} -> {t&~1:08X} '+(r.symbol.name if r and r.has_symbol else 'relative/local'))
 print('\nLITERAL BYTES')
 for a,n in [(0x536094,8),(0x5360cc,18),(0x5360e0,1),(0x5360e4,1),(0x5365f4,11),(0x53662c,28),(0x53664c,1),(0x536650,1),(0xbe3f00,1),(0xbe940e,5),(0xc00d9e,9)]:print(f'{a:08X}',read(a,n).hex(),repr(read(a,n)))
