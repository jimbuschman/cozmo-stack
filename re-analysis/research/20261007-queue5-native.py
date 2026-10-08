"""Read-only native evidence for queue 5. Navigation index never substitutes for bytes."""
from pathlib import Path
import sys, struct, re, json, hashlib, bisect
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'re-analysis/research/_m5013_dependencies'))
import capstone
from capstone.arm import ARM_OP_MEM, ARM_REG_PC
path=ROOT/'resources/lib/armeabi-v7a/libcozmoEngine.so'
light='--light' in sys.argv
if light:
    import mmap
    sys.argv.remove('--light')
    source=path.open('rb')
    raw=mmap.mmap(source.fileno(),0,access=mmap.ACCESS_READ)
    assert raw[:7]==b'\x7fELF\x01\x01\x01', 'Expected ELF32 little endian'
    phoff=struct.unpack_from('<I',raw,28)[0]
    phsize,phcount=struct.unpack_from('<HH',raw,42)
    loads=[]
    for n in range(phcount):
        kind,offset,va,pa,filesize,memsize,flags,align=struct.unpack_from('<8I',raw,phoff+n*phsize)
        if kind==1: loads.append((va,filesize,offset))
else:
    import lief
    elf=lief.parse(str(path)); raw=path.read_bytes()
    loads=[(s.virtual_address,s.physical_size,s.file_offset) for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
def read(a,n):
    for va,size,offset in loads:
        if va<=a and a+n<=va+size:
            o=a-va+offset
            return raw[o:o+n]
    raise ValueError(f'unmapped {a:08X}+{n:X}')
def word(a): return struct.unpack('<I',read(a,4))[0]
symbols={} if light else {s.value&~1:s.name for s in elf.symbols if s.value}
rel={} if light else {r.address:r for r in elf.relocations}
index=[]
for line in (() if light else (ROOT/'re-analysis/decomp/libcozmoEngine/index.tsv').open(encoding='utf-8')):
    p=line.split('\t')
    try:index.append((int(p[0],16),int(p[1]),p[3],p[4]))
    except (ValueError,IndexError):pass
index.sort(); starts=[x[0] for x in index]
def enclosing(a):
    k=bisect.bisect_right(starts,a)-1
    return index[k] if k>=0 and a<index[k][0]+index[k][1] else None
def end(f):
    with (ROOT/'re-analysis/decomp/libcozmoEngine'/f[2]).open(encoding='utf-8') as h:
        first=''.join(next(h,'') for _ in range(4))
    bound=re.search(r'body [0-9a-f]+\.\.([0-9a-f]+)',first)
    return int(bound[1],16)+1 if bound else f[0]+f[1]
def dump(a,z,thumb=False):
    print(f'RANGE 0x{a:08X}..0x{z:08X} exclusive; {"Thumb" if thumb else "ARM"}; {symbols.get(a,"")}'.rstrip())
    md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB if thumb else capstone.CS_MODE_ARM);md.detail=True
    for i in md.disasm(read(a,z-a),a):
        notes=[]
        for o in i.operands:
            if o.type==ARM_OP_MEM and o.mem.base==ARM_REG_PC and not o.mem.index:
                loc=(((i.address+4)&~3) if thumb else i.address+8)+o.mem.disp
                try:notes.append(f'word[{loc:08X}]={word(loc):08X}')
                except ValueError:pass
        if i.mnemonic in ('bl','blx','b','b.w') and i.op_str.startswith('#'):
            target=int(i.op_str[1:],16)&~1
            info=enclosing(target)
            notes.append(symbols.get(target,'') or (info[3] if info and info[0]==target else ''))
        print(f'{i.address:08X}: {i.mnemonic:12} {i.op_str}'+(' ; '+'; '.join(x for x in notes if x) if any(notes) else ''))
if __name__=='__main__':
    print('ENGINE SHA256',hashlib.sha256(raw).hexdigest())
    if light: print('LIGHT: explicit ELF32 LOAD ranges; no symbol/index/relocation navigation; words are raw ELF values')
    for arg in sys.argv[1:]:
        if arg=='--triage':
            for line in (ROOT/'re-analysis/research/20261005-adp1-triage.md').read_text(encoding='utf-8').splitlines():
                if line.startswith('|') and re.search(r'\b(KEEP|VERIFY)\b',line):
                    print('ITEM',line)
                    seen=set()
                    for match in re.finditer(r'0x([0-9A-Fa-f]{8})',line):
                        a=int(match[1],16);f=enclosing(a)
                        if f and f[0] not in seen:
                            seen.add(f[0]);dump(f[0],end(f),f[0]<0x95E540)
        elif arg.startswith('ascii:'):
            a,n=[int(x,16) for x in arg[6:].split(':')]
            print(f'{a:08X}: bytes={read(a,n).split(bytes([0]),1)[0]!r}')
        elif arg.startswith('words:'):
            a,z=[int(x,16) for x in arg[6:].split(':')]
            for loc in range(a,z,4):
                v=word(loc);r=rel.get(loc)
                if r and r.has_symbol and r.type.name=='ARM_ABS32':v=(v+r.symbol.value)&0xffffffff
                elif r and r.has_symbol and r.type.name in ('ARM_GLOB_DAT','ARM_JUMP_SLOT'):v=r.symbol.value
                name = symbols.get(v&~1,'') or (r.symbol.name if r and r.has_symbol else '')
                print(f'{loc:08X}: raw={word(loc):08X} relocated={v:08X} {name}')
        else:
            thumb=arg.startswith('thumb:');arg=arg[6:] if thumb else arg
            p=arg.split(':');a=int(p[0],16)
            f=enclosing(a)
            if len(p)>1:
                z=int(p[1],16)
            else:
                if not f:
                    raise ValueError(f'No navigation extent for {a:08X}; supply an explicit instruction-aligned end')
                z=end(f)
                if z-a>0x10000:
                    raise ValueError(f'Fragmented navigation extent {a:08X}..{z:08X}; reopen blocks with explicit ends')
            dump(a,z,thumb)
