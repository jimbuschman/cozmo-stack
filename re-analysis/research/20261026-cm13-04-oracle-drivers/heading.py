import sys,math,struct,json,pathlib
sys.dont_write_bytecode=True
sys.path.insert(0,'C:/Users/JimBu/source/repos/jimbuschman/cozmo-stack-codex2/re-analysis/research/_m5013_dependencies')
sys.path.insert(0,'re-analysis/tools/emu')
from emu_elf import Machine
m=Machine(libs=['libc++_shared.so','libcozmoEngine.so'])
def f(b):return struct.unpack('<f',struct.pack('<I',b))[0]
def b(x):return struct.unpack('<I',struct.pack('<f',x))[0]
for name,fn in [('cosf',math.cos),('sinf',math.sin),('ceilf',math.ceil)]:
 m.libc[name]=(lambda fn:lambda mm:b(fn(f(mm.reg(0)))))(fn)
m.libc['atan2f']=lambda mm:b(math.atan2(f(mm.reg(0)),f(mm.reg(1))))
m.relocate(); base=m.libs['libcozmoEngine.so'].base
rad=m.malloc(8);axis=m.malloc(12);trans=m.malloc(12);name=m.malloc(12);pose=m.malloc(12);out=m.malloc(12);robot=m.malloc(0x300);carry=m.malloc(32);quad=m.malloc(32)
m.wr(axis,struct.pack('<3f',0,0,1));m.wr(trans,bytes(12));m.wr(name,bytes(12));m.w32(robot+0x284,carry);m.w32(carry+8,0xffffffff)

import hashlib
asset=pathlib.Path('re-analysis/obb/assets/cozmo_resources/config/engine/cozmo_mprim.json')
angles=json.loads(asset.read_text())['angle_definitions']
heads=[('shipped_'+str(i),b(x)) for i,x in enumerate(angles)]
heads += [('non_axis_0_7',b(0.7)),('near_pi_below',0x40490FDA),('pi',0x40490FDB),('near_pi_above',0x40490FDC),('minus_pi',0xC0490FDB),('minus_zero',0x80000000)]
yaw=m.malloc(8)
rows=[]
def F(x):return f(b(x))
def oldquad(h,p):
 c=F(math.cos(h));ss=F(math.sin(h));out=[]
 for x,y,px,py in [(f(0x41B0CCCC),-f(0x41D8CCCD),p,-p),(f(0x41B0CCCC),f(0x41D8CCCD),p,p),(f(0xC25F999A),-f(0x41D8CCCD),-p,-p),(f(0xC25F999A),f(0x41D8CCCD),-p,p)]:
  if p!=0:x=F(x+px);y=F(y+py)
  out.extend([b(F(F(x*c)+F(y*-ss))),b(F(F(x*ss)+F(y*c)))])
 return out
for label,h in heads:
 for raw in [0xffffffff,0,0x80000000]:
  for pad in [2.0,7.0]:
   m.w32(carry+8,raw)
   m.call(base+0x84c833,rad,h,count=1000000)
   m.call(base+0x84686d,pose,rad,axis,trans,stack=(name,),count=1000000)
   m.call(base+0x84683d,out,name,count=1000000)
   m.call(base+0x517f6d,robot,pose,out,count=1000000)
   node=m.r32(out+4)
   m.call(base+0x84aa1d,yaw,node,count=1000000)
   m.call(base+0x514d75,quad,robot,out,b(pad),count=1000000)
   words=struct.unpack('<8I',m.rd(quad,32))
   row={'case':label,'heading_bits':f'{h:08X}','carried_raw':f'{raw:08X}','padding_bits':f'{b(pad):08X}','rescaled_heading_bits':f'{m.r32(rad):08X}','pose_yaw_bits':f'{m.r32(yaw):08X}','quaternion_f64_bits':[f'{x:016X}' for x in struct.unpack('<4Q',m.rd(node,32))],'origin_xyz_bits':[f'{x:08X}' for x in struct.unpack('<3I',m.rd(node+0x20,12))],'quad_xy_bits':[f'{x:08X}' for x in words]}
   if raw!=0xffffffff:row['direct_heading_different_quad_words']=sum(x!=y for x,y in zip(words,oldquad(f(h),pad)))
   rows.append(row)
   m.call(base+0x8450f5,out,count=1000000);m.call(base+0x8450f5,pose,count=1000000)
metadata={'reference_kind':'engine ARM instructions under existing emu_elf loader with explicit host libm hooks; not phone libm bit identity','engine_sha256':hashlib.sha256(pathlib.Path('resources/lib/armeabi-v7a/libcozmoEngine.so').read_bytes()).hexdigest(),'angle_asset_sha256':hashlib.sha256(asset.read_bytes()).hexdigest(),'fpscr':0,'libm_hooks':'Python math cos/sin/atan2/ceil on widened f32 arguments, results narrowed to f32; existing loader math.sqrt for fallback; existing heap/guard/libc fixtures','native_functions':['0084C832','0084686C','0084683C','00517F6C','00517EC0','0084AA1C','00514D74','008450F4'],'corner_order':'c0x,c0y,c1x,c1y,c2x,c2y,c3x,c3y','rows':rows}
pathlib.Path('.scratch/cm13-04/origin-quad-corpus.json').write_text(json.dumps(metadata,indent=2)+'\n')
print('rows',len(rows),'carry quad differences',sum(r.get('direct_heading_different_quad_words',0)>0 for r in rows))
for wanted in ['shipped_0','non_axis_0_7','near_pi_below','minus_pi']:
 print(json.dumps(next(r for r in rows if r['case']==wanted and r['carried_raw']=='00000000' and r['padding_bits']=='40E00000')))
