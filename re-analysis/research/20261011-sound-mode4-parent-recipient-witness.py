from pathlib import Path
import json,struct,hashlib
r=Path('re-analysis/research');p=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');data=p.read_bytes()
assert hashlib.sha256(data).hexdigest()=='02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1'
phoff=struct.unpack_from('<I',data,28)[0];entsize,n=struct.unpack_from('<HH',data,42)
loads=[]
for i in range(n):
 t,off,va,pa,fs,ms,flags,align=struct.unpack_from('<8I',data,phoff+i*entsize)
 if t==1:loads.append((va,va+fs,off))
def word(a):
 start,end,off=next(x for x in loads if x[0]<=a and a+4<=x[1]);return struct.unpack_from('<I',data,off+a-start)[0]
assert 0xA6692C+8+word(0xA6694C)+8==0x103CF88
assert 0xA6692C+8+word(0xA6694C)+0x14C==0x103D0CC
assert 0xA66988+8+word(0xA669E8)+8==0x103CF88
assert 0xA66988+8+word(0xA669E8)+0x14C==0x103D0CC
expect={0x103D0CC:0xA1B254,0x103D0D0:0xA196D0,0x103B7E4:0xA02EC0,0x103D430:0xA02EC0,0x103B7E8:0x9886E8,0x103D434:0x9886E8,0x980EE8:0xE12FFF1E,0x9BDEE0:0xE12FFF1E}
assert all(word(a)==v for a,v in expect.items())
(r/'20261011-sound-mode4-parent-recipient-witness.json').write_text(json.dumps({'engineSha256':hashlib.sha256(data).hexdigest(),'checkedWords':{f'{a:08X}':f'{v:08X}' for a,v in expect.items()},'constructorProducts':['103CF88','103D0CC'],'boundary':'Constructor/table and literal byte checks; current runtime pointers and admitted requests are not emulated or assumed.'},indent=2)+'\n',encoding='utf-8')
print('both ActorMixer publications and eight exact table/instruction words checked')
