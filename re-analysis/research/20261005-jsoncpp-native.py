"""Read-only extraction helper; ELF VAs, relocation-aware literals/PLT annotations."""
import sys, struct, lief, capstone
from capstone.arm import *
path = sys.argv[1]
elf = lief.parse(path)
raw = open(path, 'rb').read()
loads = [s for s in elf.segments if s.type == lief.ELF.Segment.TYPE.LOAD]
def read(a,n):
    for s in loads:
        if s.virtual_address <= a < s.virtual_address+s.physical_size:
            o=a-s.virtual_address+s.file_offset
            return raw[o:o+n]
    return b''
def word(a):
    b=read(a,4)
    return int.from_bytes(b,'little') if len(b)==4 else 0
rel={r.address:r for r in elf.relocations}
symbols={s.value & ~1:s.name for s in elf.symbols if s.value}
def plt(a):
    ins=list(capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM).disasm(read(a,12),a))
    if len(ins)!=3 or ins[0].mnemonic!='add' or ins[2].mnemonic!='ldr': return ''
    import re
    nums=lambda i: [int(x,0) for x in re.findall(r'#(0x[0-9a-f]+|[0-9]+)',i.op_str)]
    x=nums(ins[0]); y=nums(ins[1]);z=nums(ins[2])
    if not x or not y or not z:return ''
    got=a+8+(x[0]<<(x[1] if len(x)>1 else 0))+y[0]+z[0]
    r=rel.get(got)
    return f'GOT {got:08X} '+(r.symbol.name if r and r.has_symbol else '')
md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB);md.detail=True
for arg in sys.argv[2:]:
    a,z=[int(v,16) for v in arg.split(':')]
    print('\nFILE',path,'RANGE',hex(a),hex(z), symbols.get(a,''))
    for i in md.disasm(read(a,z-a),a):
        ann=[]
        for o in i.operands:
            if o.type==ARM_OP_MEM and o.mem.base==ARM_REG_PC:
                at=((i.address+4)&~3)+o.mem.disp
                ann.append(f'literal[{at:08X}]={word(at):08X}')
            if o.type==ARM_OP_IMM and i.mnemonic in ('bl','blx','b.w','b'):
                ann.append(symbols.get(o.imm&~1,'') or plt(o.imm))
        print(f'{i.address:08X}: {i.mnemonic:10s} {i.op_str}'+' ; '+ ' '.join(x for x in ann if x))
