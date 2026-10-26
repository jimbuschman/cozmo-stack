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
heading=0x4A001476
second=m.malloc(8);yaw=m.malloc(8)
m.call(base+0x84c833,rad,heading,count=1000000)
once=m.r32(rad)
m.call(base+0x84c833,second,once,count=1000000)
twice=m.r32(second)
print('input/once/twice',f'{heading:08X}',f'{once:08X}',f'{twice:08X}',f(heading),f(once),f(twice))
rows=[]
for constructions,angle in [(1,rad),(2,second)]:
 for raw in [0xffffffff,0]:
  for pad in [2.0,7.0]:
   m.w32(carry+8,raw)
   m.call(base+0x84686d,pose,angle,axis,trans,stack=(name,),count=1000000)
   m.call(base+0x84683d,out,name,count=1000000)
   m.call(base+0x517f6d,robot,pose,out,count=1000000)
   node=m.r32(out+4)
   m.call(base+0x84aa1d,yaw,node,count=1000000)
   m.call(base+0x514d75,quad,robot,out,b(pad),count=1000000)
   row={'radians_constructions':constructions,'heading_bits':f'{heading:08X}','rescaled_once_bits':f'{once:08X}','rescaled_twice_bits':f'{twice:08X}','carried_raw':f'{raw:08X}','padding_bits':f'{b(pad):08X}','pose_yaw_bits':f'{m.r32(yaw):08X}','origin_xyz_bits':[f'{x:08X}' for x in struct.unpack('<3I',m.rd(node+0x20,12))],'quad_xy_bits':[f'{x:08X}' for x in struct.unpack('<8I',m.rd(quad,32))]}
   rows.append(row);print(json.dumps(row))
   m.call(base+0x8450f5,out,count=1000000);m.call(base+0x8450f5,pose,count=1000000)
meta={'engine_sha256':hashlib.sha256(pathlib.Path('resources/lib/armeabi-v7a/libcozmoEngine.so').read_bytes()).hexdigest(),'reference_kind':'actual engine ARM Radians/pose/origin/quad with existing emu_elf loader and explicit host libm hooks; no phone or arbitrary .NET-host bit identity claim','fpscr':0,'host_hooks':'Python math cos/sin/atan2/ceil, float arguments widened and return rounded to f32; existing loader heap/guard/libc/sqrt fixtures','prescreen':{'input_float':f(heading),'float_div_ceil_minus_half_quotient':333980,'int_conversion_in_range':True},'rows':rows}
pathlib.Path('.scratch/cm13-04/origin-large-corpus.json').write_text(json.dumps(meta,indent=2)+'\n')
