"""Native checks for MOUNT-CHARGER-LLM-1, using selected captured pose/state inputs.

Runs real Charger ctor/generator (4E9CB0/4E9FB0), PreActionPose copy-with-pose
(50DF00), tilt validity with no other obstacle (4DF2C0), distance threshold (550098), GetPreActionPoses final checks
(550FD2..551196, stopped at 550ED0 before unrelated cleanup), DriveToObject caller
(559880, with the already executed pose-function result), backup pitch check
(54E7A8) and mount retry state machine (54E2D0).

Boundaries: one already selected goal; planar offsets supplied as binary32
subtractions of the native goal and captured MAIN translation; MAIN/world poses
supplied at the recorded E2G/F0 boundary, not a camera/localisation replay.
Robot::GetPose returns that supplied MAIN. Successful retry drive / failed mount
compound inputs are the recorded results. Only child deletion and retry child
construction are replaced for the retry branch; no physical backup is emulated.
Unshipped bionic/libm and logging use the existing emulator shims. No budgets,
acceptance thresholds, angle extraction, pitch cutoff or retry result are replaced.
Run --check to compare with the committed native-generated fixture. No robot I/O.
"""
import sys,struct,math,json,argparse,hashlib
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--fixture',type=Path,default=Path(__file__).resolve().parents[3]/'cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/mount_charger_llm_native.json')
parser.add_argument('--check',action='store_true',help='Compare the actual native results with the committed fixture')
parser.add_argument('--poison',type=lambda value:int(value,0),default=0,help='Fresh emulator heap/stack fill byte')
args=parser.parse_args()
fixture=json.loads(args.fixture.read_text(encoding='utf-8'))
assert hashlib.sha256((Path(__file__).resolve().parents[3]/'resources/lib/armeabi-v7a/libcozmoEngine.so').read_bytes()).hexdigest()==fixture['binary_sha256']
results=[]
from emu_elf import Machine,RETADDR,NORET
from emu_engine_objects import install_logging,install_libm_float,install_aeabi_mem,f32bits,bitsf32
from emu_solvepnp import LIBS, Oracle
from unicorn.arm_const import *
oracle=Oracle.__new__(Oracle)
m=oracle.m=Machine(libs=LIBS+['libcozmoEngine.so'],poison=args.poison)
oracle.neon=True
oracle._install()
install_logging(m,channeled=True)
install_libm_float(m,{'acosf':math.acos,'cosf':math.cos,'sinf':math.sin,'sqrtf':math.sqrt,'atanf':math.atan,'asinf':math.asin})
install_aeabi_mem(m,memset=True)
m.overrides['atan2f']=lambda mm:f32bits(math.atan2(bitsf32(mm.reg(0)),bitsf32(mm.reg(1))))
m.relocate();oracle._run_inits();b=m.libs['libcozmoEngine.so'].base
def call(a,*args,stack=()):return m.call(b+(a|1),*args,stack=stack,count=5000000)
def buf(n,data=b''):
 p=m.malloc(n);m.wr(p,data.ljust(n,b'\0'));return p
def tr(p):return struct.unpack('<4d3f',m.rd(m.r32(p+4),44))
name=buf(16);root=buf(0x40);call(0x84683c,root,name)
charger=buf(0x400);call(0x4e9cb0,charger,0xd)
# Real Charger ctor, marker, static predock Pose2d and GeneratePreActionPoses.
call(0x4d6bc4)
vec=buf(12);call(0x4e9fb0,charger,0,vec)
elem=m.r32(vec);assert m.r32(vec+4)-elem==28

ctx={}
ctx['compound_update']=lambda mm:0
m.hook_addr(b+0x4aa984,lambda mm:ctx['compound_update'](mm))
m.hook_addr(b+0x4a7114,lambda mm:charger+0x104)
def pose_function_result(mm):
 mm.wr(mm.reg(3),bytes([ctx['in_position']]))
 return ctx['pose_result']
m.hook_addr(b+0x4abe18,pose_function_result)
def begin_tail(mm):
 sp=mm.uc.reg_read(UC_ARM_REG_SP)
 mm.wr(sp,b'\0'*0xa0)
 mm.w32(sp+0x98,f32bits(0.13089969754219055))
 for reg,val in [(UC_ARM_REG_R6,ctx['robot']),(UC_ARM_REG_R7,charger),(UC_ARM_REG_R8,0),(UC_ARM_REG_R9,ctx['output'])]:
  mm.uc.reg_write(reg,val)
 return NORET
def stop_tail(mm):
 mm.uc.reg_write(UC_ARM_REG_PC,RETADDR)
 return NORET
m.hook_addr(b+0x550fd2,begin_tail)
m.hook_addr(b+0x550ed0,stop_tail)
# Robot::GetPose helper is bounded to the supplied MAIN pose, not a complete Robot.
m.hook_addr(b+0x4ea398,lambda mm:ctx['main'])
call(0x844a14,charger+0x108,root)
for row in fixture['rows']:
 st,ob=row['state'],row['charger']
 m.wr(m.r32(charger+0x108+4),struct.pack('<4d3f',*ob['q'],*ob['xyz']))
 goal=buf(28);call(0x50df00,goal,elem,charger+0x108,f32bits(0),stack=(f32bits(0),))
 robot=buf(0x500)
 angle=buf(12);call(0x84c832,angle,f32bits(st['yaw']))
 call(0x846a84,robot+0x298,angle,buf(12,struct.pack('<3f',0,0,1)),buf(12,struct.pack('<3f',st['x'],st['y'],st['z'])),stack=(root,name))
 assert call(0x4df2c0,charger,goal,robot+0x298,buf(12))&255==1
 output=buf(0x30);m.w32(output+4,goal);m.w32(output+0x10,0)
 g=tr(goal+8)
 f=lambda v:struct.unpack('<f',struct.pack('<f',v))[0]
 dx,dy=abs(f(st['x']-g[4])),abs(f(st['y']-g[5]))
 m.wr(output+0x14,struct.pack('<2f',dx,dy))
 if row['target']==612214.56: backup_context=(row,robot,robot+0x298)
 ctx.update(robot=robot,main=robot+0x298,output=output)
 result=call(0x550fd2)
 in_position=m.rd(output+0x1c,1)[0]
 # Run the real DriveToObject caller around the already executed pose function.
 # Only its completed inner compound and function-call boundary are substituted.
 ctx.update(in_position=in_position,pose_result=result)
 drive=buf(0x180);m.w32(drive+4,robot);m.wr(drive+0x148,b'\x01')
 assert call(0x559880,drive)==(0 if in_position else 0x04000001)
 thresholds=struct.unpack('<2f',m.rd(output+0x20,8))
 rel=buf(0x40);call(0x84683c,rel,name);assert m.call(b+0x4a40fc,goal+8,robot+0x298,rel)&255
 yaw=buf(4);m.call(b+0x4a4150,yaw,m.r32(rel+4))
 results.append(dict(at=row['target'],goal=list(g),offset=[dx,dy],threshold=list(thresholds),yaw=struct.unpack('<f',m.rd(yaw,4))[0],result=result,inPosition=m.rd(output+0x1c,1)[0]))

row,robot,main=backup_context
m.wr(m.r32(charger+0x108+4),struct.pack('<4d3f',*row['charger']['q'],*row['charger']['xyz']))
m.wr(robot+0x304,struct.pack('<f',row['state']['pitch']))
m.wr(robot+0x338,b'\0')
backup=buf(0x100);m.w32(backup+4,robot)
base_calls=[]
m.hook_addr(b+0x4ab590,lambda mm:base_calls.append(1) or 0x01000000)
backup_result=call(0x54e7a8,backup)
assert backup_result==0x0400000a and not base_calls
mount=buf(0x100);m.w32(mount+4,robot)
child,retry=buf(32),buf(32)
vt=buf(16);nop=buf(16,b'\x70\x47');m.w32(vt+4,nop|1);m.w32(child,vt)
m.w32(mount+0x88,child)
updates=[];configured=[]
ctx['compound_update']=lambda mm: updates.append(mm.reg(0)) or (backup_result if mm.reg(0)==child else 0)
def configure(mm):
 configured.append(1);mm.w32(mount+0x8c,retry);return 0
m.hook_addr(b+0x4ab71c,configure)
ctx.update(main=main)
retry_result=call(0x54e2d0,mount)
backup_expected=dict(result=backup_result,baseDriveCalls=len(base_calls),retryResult=retry_result,retryConfigurations=len(configured),childUpdates=len(updates))
assert retry_result==0x04000006 and len(configured)==1 and updates==[child,retry]
if args.check:
 assert results==[row['expected'] for row in fixture['rows']],results
 assert backup_expected==fixture['backupExpected'],backup_expected
print(json.dumps(dict(binary_sha256=fixture['binary_sha256'],rows=results,backupExpected=backup_expected),indent=2))


