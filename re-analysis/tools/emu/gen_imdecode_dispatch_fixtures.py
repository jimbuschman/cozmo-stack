"""Generate M3-045 probe fixtures from shipped ARM code, run from the repository root.

Inputs and branch statuses are from manager-adopted Part C.3 of
re-analysis/research/20261010-m3045-m4033-rows.md. The engine's own probe
at libopencv_imgcodecs.so 0x375A6 supplies the recorded expected statuses.
Requires the same lief/unicorn dependencies as emu_jpeg_decode.py.
"""
import sys,struct,json
from pathlib import Path
sys.path.insert(0,str(Path('re-analysis/tools/emu').resolve()))
from emu_jpeg_decode import Oracle,verify_library_hashes
def put(b,p,v): struct.pack_into('<I',b,p,v)
def tag(b,p,s): b[p:p+4]=s.encode('ascii')
def chunk(s,n):
 b=bytearray(32);tag(b,0,s);put(b,4,n);return b
def riff(s,n,rs):
 b=chunk('RIFF',rs);tag(b,8,'WEBP');tag(b,12,s);put(b,16,n);return b
def stream(route,lossless):
 s='VP8L' if lossless else 'VP8 '
 b=riff(s,12,24) if route=='riff' else chunk(s,12) if route=='chunk' else chunk('ALPH',0) if route=='alph' else bytearray(32)
 p={'raw':0,'riff':20,'chunk':8,'alph':16}[route]
 if route=='alph':tag(b,8,s);put(b,12,12)
 if lossless:b[p]=0x2f
 else:b[p]=0x10;b[p+3:p+6]=bytes.fromhex('9d012a')
 return b
cases=[]
def add(name,b,want,version=0x201,features=True,data=True):cases.append((name,bytes(b),want,version,features,data))
for route in ('raw','riff','chunk','alph'):
 for lossless in (False,True):add(route+('-lossless' if lossless else '-lossy'),stream(route,lossless),0)
b=riff('VP8X',10,0xfffffff6);put(b,20,0xffffffff);add('extended-flags-large-riff',b,0)
for size in (1,2,31):add('padded-short-lossless-'+str(size),bytes([0x2f])+bytes(size-1)+b' '*(32-size),0)
for name,want in [('riff-not-webp',3),('riff-too-small',3),('riff-too-large',3),('vp8x-size',3),('vp8x-product',3),('vp8x-no-riff',3),('alph-cap',3),('alph-incomplete-chunk',7),('alph-next-header-too-short',7),('riff-chunk-exceeds-riff',3),('bitstream-size-cap',3),('lossless-too-short',7),('lossless-marker',3),('lossy-too-short',7),('lossy-key-marker',3),('lossy-not-key-frame',3),('lossy-version',3),('lossy-not-shown',3),('lossy-partition-equal-size',3)]:
 if name.startswith('riff-'):b=riff('VP8L',5,20)
 elif name in ('vp8x-size','vp8x-product'):b=riff('VP8X',10,22)
 elif name=='vp8x-no-riff':b=chunk('VP8X',10)
 elif name.startswith('alph-') or name.endswith('-too-short'):b=chunk('ALPH',0)
 elif name in ('bitstream-size-cap','lossless-marker'):b=stream('chunk',True)
 else:b=stream('raw',False)
 if name=='riff-not-webp':b[8]=0
 elif name=='riff-too-small':put(b,4,11)
 elif name=='riff-too-large':put(b,4,0xfffffff7)
 elif name=='vp8x-size':put(b,16,9)
 elif name=='vp8x-product':b[24:30]=b'\xff'*6
 elif name=='alph-cap':put(b,4,0xfffffff7)
 elif name=='alph-incomplete-chunk':put(b,4,25)
 elif name=='alph-next-header-too-short':put(b,4,17)
 elif name=='riff-chunk-exceeds-riff':put(b,16,9)
 elif name=='bitstream-size-cap':put(b,4,0xfffffff7)
 elif name=='lossless-too-short':put(b,4,12);tag(b,20,'VP8L')
 elif name=='lossless-marker':b[8]=0
 elif name=='lossy-too-short':put(b,4,8);tag(b,16,'VP8 ')
 elif name=='lossy-key-marker':b[3]=0
 elif name=='lossy-not-key-frame':b[0]=0x11
 elif name=='lossy-version':b[0]=0x18
 elif name=='lossy-not-shown':b[0]=0
 elif name=='lossy-partition-equal-size':b[1]=4
 add(name,b,want)
for version,want in [(0x200,0),(0x2ff,0),(0x300,2),(-1,2)]:add('version-'+str(version),stream('raw',True),want,version)
add('null-features',stream('raw',True),2,features=False)
add('null-data',stream('raw',True),2,data=False)
o=Oracle();m=o.m;fn=m.libs['libopencv_imgcodecs.so'].base+0x375a7
out=[]
for name,b,want,version,features,data in cases:
 m.reset_heap();m.poison_stack();buf=m.malloc(32);m.wr(buf,b);feat=m.malloc(0x28)
 got=m.call(fn,buf if data else 0,32,feat if features else 0,version,count=100000)
 if got!=want:raise SystemExit(f'MISSING: {name}: adopted row expected {want}, native probe returned {got}')
 out.append(dict(id=name,window_hex=b.hex(),version=version,features_present=features,data_present=data,status=got))
folder=Path('cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/imdecode_dispatch');folder.mkdir(exist_ok=True)
(folder/'webp-probe.json').write_text(json.dumps(dict(source='shipped libopencv_imgcodecs.so 0x375A6, Unicorn via emu_jpeg_decode.Oracle; inputs from adopted Part C.3 rows; size always 32',library_sha256=verify_library_hashes(),cases=out),indent=2)+'\n',encoding='utf-8')
print(f'{len(out)} probe cases match the adopted rows and the shipped native code')
