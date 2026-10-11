"""ActualA57604 decision body, explicit VM/query ABI fixtures; no phone bodies."""
from pathlib import Path
import sys,struct,json,hashlib,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE
from unicorn.arm_const import *
engine=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=engine.read_bytes();elf=lief.parse(str(engine));loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
E=0x20001000;P=0x20002000;VM=0x20003000;TABLE=0x20004000;ENV=0x20005000;ACTIVITY=0x20006000;GET=0x20008000;ATTACH=GET+4;DETACH=GET+8;STOP=0x20009000;Q=0x108df90
def run(name,vm,activity,env_first,env_attached,rate,frames):
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 u.mem_map(0x20000000,0x10000);u.mem_map(0x30000000,0x10000)
 def put(a,v):u.mem_write(a,struct.pack('<I',v&0xffffffff))
 def word(a):return struct.unpack('<I',u.mem_read(a,4))[0]
 put(VM,TABLE);put(TABLE+0x18,GET);put(TABLE+0x10,ATTACH);put(TABLE+0x14,DETACH)
 put(P+0x4c,VM if vm else 0);put(P+0x50,ACTIVITY if activity else 0);put(P+0x38,0x11111111);put(E+0x20,0x22222222);put(Q,0x33333333);put(Q+4,0x44444444)
 u.reg_write(UC_ARM_REG_SP,0x30008000);u.reg_write(UC_ARM_REG_LR,STOP);u.reg_write(UC_ARM_REG_R0,E);u.reg_write(UC_ARM_REG_R1,P)
 trace=[]
 def hook(u,a,size,ignored):
  if a in (0xa57680,0xa57684,0xa576fc,0xa57700):trace.append(dict(step='before native setting store',pc=f'{a:08X}',platform38=f'{word(P+0x38):08X}',engine20=f'{word(E+0x20):08X}'))
  if a not in (GET,ATTACH,DETACH,0xa56e20):return
  lr=u.reg_read(UC_ARM_REG_LR);r0=u.reg_read(UC_ARM_REG_R0);r1=u.reg_read(UC_ARM_REG_R1);r2=u.reg_read(UC_ARM_REG_R2)
  if a==GET:
   assert r0==VM and r2==0x10006;put(r1,ENV if env_first else 0);label='GetEnv';ret=0xffffffff
  elif a==ATTACH:
   assert r0==VM and word(r2)==0x10006 and word(r2+8)==0
   nameptr=word(r2+4);assert bytes(u.mem_read(nameptr,13))==b'NativeThread\x00'
   put(r1,ENV if env_attached else 0);label='AttachCurrentThread';ret=0
  elif a==0xa56e20:
   assert r0==ENV and r1==ACTIVITY;put(Q,rate);put(Q+4,frames);label='query ABI fixture';ret=0xdeadbeef
  else:
   assert r0==VM;label='DetachCurrentThread';ret=0xffffffff
  trace.append(dict(step=label,callee=f'{a:08X}',R0=f'{r0:08X}',R1=f'{r1:08X}',platform38=f'{word(P+0x38):08X}',engine20=f'{word(E+0x20):08X}',fixture_return=f'{ret:08X}'))
  for reg in (UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3,UC_ARM_REG_R12):u.reg_write(reg,0xa5a5a5a5)
  u.reg_write(UC_ARM_REG_R0,ret);u.reg_write(UC_ARM_REG_PC,lr)
 u.hook_add(UC_HOOK_CODE,hook);u.emu_start(0xa57604,STOP,count=300)
 assert u.reg_read(UC_ARM_REG_PC)==STOP and u.reg_read(UC_ARM_REG_SP)==0x30008000
 success=vm and activity and (env_first or env_attached)
 expected=1 if success else 0x1f if not vm or not activity else 2
 assert u.reg_read(UC_ARM_REG_R0)==expected
 assert word(P+0x38)==(rate if success else 0x11111111) and word(E+0x20)==(frames if success else 0x22222222)
 labels=[s['step'] for s in trace]
 if success:
  assert labels.count('query ABI fixture')==1
  stores=[s for s in trace if s['step']=='before native setting store'];assert len(stores)==2 and stores[0]['platform38']=='11111111' and stores[1]['platform38']==f'{rate:08X}' and stores[1]['engine20']=='22222222'
  if not env_first:
   d=next(s for s in trace if s['step']=='DetachCurrentThread');assert d['platform38']==f'{rate:08X}' and d['engine20']==f'{frames:08X}'
 else:assert 'query ABI fixture' not in labels and 'DetachCurrentThread' not in labels
 return dict(case=name,fixtures=dict(vm=vm,activity=activity,env_first=env_first,env_attached=env_attached,rate=rate,frames=frames),returnR0=expected,platform38=f'{word(P+0x38):08X}',engine20=f'{word(E+0x20):08X}',trace=trace)
cases=[run('missing VM',False,True,False,False,0,0),run('missing activity',True,False,False,False,0,0),run('attachment returns0 but env remains null',True,True,False,False,0,0)]
for pair in ((48000,1024),(0,0),(22320,4096)):
 for existing in (True,False):cases.append(run('existing env' if existing else 'attached env',True,True,existing,not existing,*pair))
result=dict(engine_sha256=hashlib.sha256(raw).hexdigest(),boundary='NativeA57604 gates, VM/query ABI arguments, raw copy/store/detach order and results. VM/query callbacks are explicit fixtures, not phone implementations or admitted shipped caller/settings invariants. No numerical helper orA57724 startup body rerun.',cases=cases)
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode());print(len(cases),'native query-pair decision cases passed')
