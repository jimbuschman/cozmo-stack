import sys,struct,hashlib,json,pathlib,platform
sys.path[:0]=['.scratch/bouncer-native-reference/deps','re-analysis/tools/emu']
from emu_elf import Machine,ORDER,LIBDIR
import unicorn
P=pathlib.Path('.scratch/bouncer-native-reference');m=Machine();m.relocate()
def obj(data):
 a=m.malloc(len(data));m.wr(a,data);return a
def pair(x,y):return obj(struct.pack('<2i',x,y))
def draw(case):
 buf=obj(bytes(8192));mat=obj(bytes(64));m.call(m.sym('_ZN2cv3MatC1EiiiPvj'),mat,64,128,0,stack=(buf,0));ia=obj(struct.pack('<4I',0x02010000,mat,0,0));col=obj(struct.pack('<4d',255,0,0,0))
 for x,y,w,h in case.get('rect',[]):
  r=obj(struct.pack('<4i',x,y,w,h));m.call(m.sym('_ZN2cv9rectangleERNS_3MatENS_5Rect_IiEERKNS_7Scalar_IdEEiii'),mat,r,col,0xffffffff,stack=(8,0),count=3000000)
 for x,y in case.get('circle',[]):
  m.call(m.sym('_ZN2cv6circleERKNS_17_InputOutputArrayENS_6Point_IiEEiRKNS_7Scalar_IdEEiii'),ia,pair(x,y),3,col,stack=(0xffffffff,8,0),count=3000000)
 for text,x,y in case.get('text',[]):
  c=obj(text.encode()+b'\0');s=obj(bytes(8));m.call(m.sym('_ZN2cv6StringC1EPKc'),s,c)
  m.call(m.sym('_ZN2cv7putTextERKNS_17_InputOutputArrayERKNS_6StringENS_6Point_IiEEidNS_7Scalar_IdEEiib'),ia,s,pair(x,y),0x32,stack=(0,0x3fe00000,col,1,8,0),count=5000000)
 return bytes(m.uc.mem_read(buf,8192))
cases={}
for d in range(10):cases[f'digit_{d}']={'text':[(str(d),4,12)]}
cases.update({'multi':{'text':[('4294967295',44,12)]},'circle_center':{'circle':[(64,32)]},'circle_top_left':{'circle':[(0,0)]},'circle_bottom_right':{'circle':[(128,64)]},'rect_standard':{'rect':[(48,60,32,4)]},'rect_clip':{'rect':[(-2,62,8,4)]},'rect_empty':{'rect':[(5,5,0,4)]},'frame_initial':{'rect':[(48,60,32,4)],'circle':[(64,64)],'text':[('0',4,12),('0',116,12)]},'frame_edges':{'rect':[(0,60,16,4)],'circle':[(128,0)],'text':[('42',4,12),('1234',92,12)]},'frame_multidigit':{'rect':[(96,60,32,4)],'circle':[(1,63)],'text':[('123456',4,12),('99999',84,12)]}})
out={}
for name,c in cases.items():
 b=draw(c);(P/(name+'.gray')).write_bytes(b);out[name]={'input':c,'sha256':hashlib.sha256(b).hexdigest(),'white_pixels':sum(v==255 for v in b),'nonbinary':sum(v not in (0,255) for v in b)}
libs={n:hashlib.sha256((pathlib.Path(LIBDIR)/n).read_bytes()).hexdigest() for n in ORDER}
(P/'manifest.json').write_text(json.dumps({'unicorn':unicorn.__version__,'libraries':libs,'wheel_sha256':hashlib.sha256(next(P.glob('*.whl')).read_bytes()).hexdigest(),'cases':out},indent=2))
print(json.dumps(out,indent=2))
# Execute the engine adapter's actual VCVTR instruction for representative half ties.
from unicorn.arm_const import UC_ARM_REG_S0,UC_ARM_REG_FPSCR
rounding={}
for v in [0.5,1.5,2.5,3.5,15.5,16.5,63.5,64.5,127.5,128.5,-0.5,-1.5]:
 m.uc.reg_write(UC_ARM_REG_FPSCR,0);m.uc.reg_write(UC_ARM_REG_S0,struct.unpack('<I',struct.pack('<f',v))[0]);m.uc.emu_start(m.libs['libcozmoEngine.so'].base+0x86e611,m.libs['libcozmoEngine.so'].base+0x86e614,count=1)
 raw=m.uc.reg_read(UC_ARM_REG_S0);rounding[str(v)]=struct.unpack('<i',struct.pack('<I',raw))[0]
manifest=json.loads((P/'manifest.json').read_text());manifest['rounding_vcvtr_engine_0x86e610']=rounding
(P/'manifest.json').write_text(json.dumps(manifest,indent=2));print(rounding)
