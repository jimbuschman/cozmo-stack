"""Read packaged codec tables and ARM allocation veneer; no binary writes."""
from pathlib import Path
import struct, lief, capstone
b = lief.parse('resources/lib/armeabi-v7a/libopencv_imgcodecs.so')
read = lambda a,n: bytes(b.get_content_from_virtual_address(a,n))
u32 = lambda a: struct.unpack('<I',read(a,4))[0]
out=[]
# PC-relative table pointers in Huffman start_pass, with their instruction offsets.
for n,pc,lit,offset in [(2,0x2BD32,0x2BED0,0x34),(3,0x2BD32,0x2BED0,0x4C),
                         (4,0x2BD1C,0x2BEC0,0x64),(5,0x2BD20,0x2BEC4,0xB4),
                         (6,0x2BD34,0x2BED4,0x11C),(7,0x2BD3A,0x2BED8,0x1AC),
                         (8,0x2BD26,0x2BEC8,0x254)]:
    # n2 actually ip, n3 r8: fix explicit PC associations.
    if n==2: pc,lit=0x2BD2E,0x2BECC
    a=(pc+4+u32(lit)+offset)&0xffffffff
    # n2/4/8 index h*n+v; n3/5/6/7 index (h-1)*n+v.
    first=a+(n+1)*4 if n in (2,4,8) else a+4
    vals=struct.unpack('<'+'i'*(n*n),read(first,n*n*4))
    out.append(f'LIMIT n={n} firstVA={first:08X}; rows vertical size, columns horizontal size; entries before +1')
    out += [' '.join(str(x) for x in vals[j*n:(j+1)*n]) for j in range(n)]
md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM)
out.append('ARM malloc veneer')
out += [f'{i.address:08X}: {i.mnemonic} {i.op_str}' for i in md.disasm(read(0xB6F0C,12),0xB6F0C)]
dest=(0xB6F10+8+u32(0xB6F14))&0xffffffff
out.append(f'ARM veneer target {dest:08X}')
out += [f'{i.address:08X}: {i.mnemonic} {i.op_str}' for i in md.disasm(read(dest,12),dest)]
for pc,lit in [(0xF6F8,0xFA98),(0xF70E,0xFA9C),(0xF710,0xFAA0)]:
    a=(pc+4+u32(lit))&0xffffffff
    out.append(f'STRING {a:08X} '+repr(read(a,300).split(b'\0')[0]))
p=lief.parse('resources/lib/armeabi-v7a/libopencv_imgproc.so')
out.append('RGB-gray coefficients E2AB0 data: '+bytes(p.get_content_from_virtual_address(0xE2AB0,12)).hex())
Path(__file__).with_suffix('.txt').write_text('\n'.join(out)+'\n',encoding='utf-8')
print('\n'.join(out))
