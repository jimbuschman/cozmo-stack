from pathlib import Path
import lief,capstone,struct
b=lief.parse('resources/lib/armeabi-v7a/libopencv_imgcodecs.so')
read=lambda a,n: bytes(b.get_content_from_virtual_address(a,n))
u32=lambda a: struct.unpack('<I',read(a,4))[0]
c=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB)
# Selector r6 is PC 2A62C+4 plus literal 2A914.
base=(0x2A630+u32(0x2A914))&0xffffffff
cases={0x204:0x2A91C,0x404:0x2A920,0x603:0x2A924,0x70E:0x2A928,0x909:0x2A92C,0xC06:0x2A930,0xE0E:0x2A934,0x1010:0x2A938}
keys=[0x202,0x303,0x505,0x606,0x707,0xA0A,0xB0B,0xC0C,0xD0D,0xF0F,0x1008,0xE07,0xA05,0x804,0x402,0x201,0x810,0x60C,0x50A,0x408,0x306,0x102]
cases.update(dict(zip(keys,range(0x2A93C,0x2A994,4))));cases[0x101]=0x2A99C;cases[0x808]=0x2A9A0
out=[]
for key,lit in sorted(cases.items()):
 got=(base+u32(lit))&0xffffffff;start=u32(got)&~1;todo=[start];seen={};calls=set();unknown=[]
 while todo:
  a=todo.pop()
  while a not in seen:
   ii=list(c.disasm(read(a,4),a,count=1))
   if not ii:unknown.append(hex(a));break
   i=ii[0];seen[a]=i;a2=a+i.size;m=i.mnemonic;op=i.op_str
   if m.startswith('bl'):
    calls.add(op);a=a2;continue
   if m in ('bx','pop','pop.w') and ('lr' in op if m=='bx' else 'pc' in op):break
   if m.startswith('b') and op.startswith('#'):
    target=int(op[1:],0)
    if m in ('b','b.w'):a=target;continue
    todo.append(target)
   elif m in ('cbz','cbnz'):
    todo.append(int(op.split('#')[-1],0))
   a=a2
   if not start<=a<start+0x3000:unknown.append('unexpected extent '+hex(a));break
 out.append(f'KEY {key:04X} GOT {got:08X} ENTRY {start:08X} COUNT {len(seen)} CALLS {sorted(calls)} UNKNOWN {unknown}')
 for a,i in sorted(seen.items()):
  suffix=''
  if '[pc,' in i.op_str:
   off=int(i.op_str.split('#')[-1].split(']')[0],0);la=((a+4)&~3)+off;suffix=f' ; literal[{la:08X}]={u32(la):08X}'
  out.append(f'{a:08X}: {i.mnemonic:12} {i.op_str}{suffix}')
 out.append('')
Path('re-analysis/research/20261006-M3M4-jpeg-idct-cfg.txt').write_text('\n'.join(out),encoding='utf-8')
print('\n'.join(x for x in out if x.startswith('KEY')))
