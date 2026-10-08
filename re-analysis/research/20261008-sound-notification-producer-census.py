"""Primary-byte branch census, not an indirect-call absence proof."""
from pathlib import Path
import struct, sys, importlib.util
sys.argv.append('--light')
spec=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'))
n=importlib.util.module_from_spec(spec); spec.loader.exec_module(n)
targets={0x9D3558}
targets.update(int(arg,16) for arg in sys.argv[1:] if arg.startswith('0x'))
print('ENGINE SHA256',__import__('hashlib').sha256(n.raw).hexdigest())
print('Direct branch targets',','.join(f'{x:08X}' for x in sorted(targets)))
for va,size,off in n.loads:
    # Read p_flags independently; scan only executable LOAD segments.
    headers=[struct.unpack_from('<8I',n.raw,n.phoff+i*n.phsize) for i in range(n.phcount)]
    if not any(h[0]==1 and h[2]==va and h[6]&1 for h in headers): continue
    data=n.read(va,size)
    for j in range(0,size-4,4):
        w=struct.unpack_from('<I',data,j)[0]; a=va+j
        if (w&0x0E000000)==0x0A000000:
            imm=w&0xffffff; imm=imm-(1<<24) if imm&(1<<23) else imm
            t=(a+8+(imm<<2))&0xffffffff
            if w>>28==15:t=(t+((w>>23)&2))&~1
            if t in targets:print(f'ARM candidate {a:08X} -> {t:08X} raw={w:08X}')
    md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB)
    for j in range(0,size-4,2):
        h1,h2=struct.unpack_from('<HH',data,j)
        if h1&0xf800!=0xf000 or h2&0x8000==0:continue
        s=(h1>>10)&1; i1=1^((h2>>13)&1)^s;i2=1^((h2>>11)&1)^s
        imm=(s<<24)|(i1<<23)|(i2<<22)|((h1&0x3ff)<<12)|((h2&0x7ff)<<1)
        if s:imm-=1<<25
        a=va+j;t=(a+4+imm)&0xffffffff
        if h2&0x1000==0:t=((a+4)&~3)+imm;t&=~3
        if t not in targets:continue
        for ins in md.disasm(data[j:j+4],a):
            if ins.mnemonic in ('bl','blx','b.w'):print(f'Thumb candidate {a:08X}: {ins.mnemonic} {ins.op_str}')
print('Raw aligned address-valued words across file-backed LOADs (relocations/PC-relative references not excluded):')
for va,size,off in n.loads:
    data=n.read(va,size)
    for j in range(0,size-4,4):
        w=struct.unpack_from('<I',data,j)[0]
        if w&~1 in targets:print(f'{va+j:08X}: {w:08X}')
print('Dynamic symbol records at target addresses:')
shoff=struct.unpack_from('<I',n.raw,32)[0]
shsize,shcount=struct.unpack_from('<HH',n.raw,46)
sections=[struct.unpack_from('<10I',n.raw,shoff+i*shsize) for i in range(shcount)]
shnames_index=struct.unpack_from('<H',n.raw,50)[0]
shnames_section=sections[shnames_index]
shnames=n.raw[shnames_section[4]:shnames_section[4]+shnames_section[5]]
for sh in sections:
    if sh[1]!=11:continue
    strings=sections[sh[6]]; names=n.raw[strings[4]:strings[4]+strings[5]]
    for j in range(sh[4],sh[4]+sh[5],sh[9]):
        name,value,size,info,other,index=struct.unpack_from('<IIIBBH',n.raw,j)
        if value&~1 in targets:
            end=names.find(b'\0',name)
            print(f'{value:08X} size={size:X} info={info:02X} section={index}: {names[name:end].decode("utf-8")}')
print('LIMIT: candidates require reopening; no claim excluding indirect/computed/exported calls.')
