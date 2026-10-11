"""Q14-104/131 concrete candidate initializer caller navigation; not indirect absence proof."""
from pathlib import Path
import lief,capstone,struct,json,hashlib
ROOT=Path(__file__).resolve().parents[2];path=ROOT/'resources/lib/armeabi-v7a/libcozmoEngine.so';elf=lief.parse(str(path));targets={0x9b1aac,0x9b1830};out=[]
arm=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM);th=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB)
def signed(x,bits):return x-(1<<bits) if x&(1<<(bits-1)) else x
for sec in elf.sections:
 if not sec.has(lief.ELF.Section.FLAGS.EXECINSTR):continue
 data=bytes(sec.content);va=sec.virtual_address
 for pos in range(0,len(data)-4,2):
  a=va+pos;w=struct.unpack_from('<I',data,pos)[0];h=w&65535;hi=w>>16;tests=[]
  if a%4==0 and w&0x0e000000==0x0a000000:
   off=signed(w&0xffffff,24)<<2
   if w>>28==15:off|=((w>>24)&1)<<1
   tests.append(((a+8+off)&0xffffffff,arm))
  if h&0xf800==0xe000:tests.append(((a+4+(signed(h&0x7ff,11)<<1))&0xffffffff,th))
  if h&0xf000==0xd000 and (h>>8)&15<14:tests.append(((a+4+(signed(h&0xff,8)<<1))&0xffffffff,th))
  if h&0xf800==0xf000:
   sign=(h>>10)&1;j1=(hi>>13)&1;j2=(hi>>11)&1
   if hi&0xd000 in (0x9000,0xc000,0xd000):
    i1=1^(j1^sign);i2=1^(j2^sign);off=signed((sign<<24)|(i1<<23)|(i2<<22)|((h&0x3ff)<<12)|((hi&0x7ff)<<1),25)
    base=(a+4)&~3 if hi&0xd000==0xc000 else a+4;tests.append(((base+off)&0xffffffff,th))
   elif hi&0xd000==0x8000:
    off=signed((sign<<20)|(j2<<19)|(j1<<18)|((h&0x3f)<<12)|((hi&0x7ff)<<1),21);tests.append(((a+4+off)&0xffffffff,th))
  for dest,md in tests:
   if dest&~1 not in targets:continue
   i=next(md.disasm(data[pos:pos+4],a),None)
   if i and i.mnemonic.startswith(('b','cb')) and i.op_str.startswith('#') and int(i.op_str[1:],16)&~1 in targets:out.append(dict(address=hex(a),target=i.op_str,mode='Thumb' if md is th else 'ARM',instruction=i.mnemonic+' '+i.op_str,section=sec.name))
result=dict(sha256=hashlib.sha256(path.read_bytes()).hexdigest(),targets=[hex(x) for x in sorted(targets)],limit='Dual-alignment immediate ARM/Thumb branch encoding navigation only; mixed-mode/literal false candidates, alternate entry/computed/copied/indirect inputs remain UNKNOWN.',candidates=out)
Path(__file__).with_suffix('.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(out,indent=2))
