"""Actual empty-event factory plus paid dispatcher empty branch, ABI fixtures only."""
from pathlib import Path
import sys,struct,json,hashlib,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE
from unicorn.arm_const import *
engine=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=engine.read_bytes();elf=lief.parse(str(engine));loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
OBJ=0x20001000;STOP=0x20009000;SP=0x30008000
def machine():
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 u.mem_map(0x20000000,0x10000);u.mem_map(0x30000000,0x10000);u.reg_write(UC_ARM_REG_SP,SP);u.reg_write(UC_ARM_REG_LR,STOP);return u
cases=[]
for ident in (0,725225627,196431345):
 for allocated in (False,True):
  u=machine();u.mem_write(OBJ,b'\xa5'*24);u.reg_write(UC_ARM_REG_R0,ident);calls=[]
  def hook(u,a,size,ignored):
   if a not in (0xa7a7f4,0x9cc9c4):return
   lr=u.reg_read(UC_ARM_REG_LR)
   if a==0xa7a7f4:
    assert u.reg_read(UC_ARM_REG_R1)==24;ret=OBJ if allocated else 0
    calls.append(dict(boundary='allocator, opaque fixture',address=f'{a:08X}',size=24,fixture_return=f'{ret:08X}'))
   else:
    assert u.reg_read(UC_ARM_REG_R0)==OBJ
    words=struct.unpack('<6I',u.mem_read(OBJ,24));assert words==(0x103af78,0xa5a5a5a5,ident,1,0,0)
    calls.append(dict(boundary='publication, opaque fixture',address=f'{a:08X}',object_words=[f'{v:08X}' for v in words],fixture_return='DEADBEEF'));ret=0xdeadbeef
   for reg in (UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3,UC_ARM_REG_R12):u.reg_write(reg,0x5a5a5a5a)
   u.reg_write(UC_ARM_REG_R0,ret);u.reg_write(UC_ARM_REG_PC,lr)
  u.hook_add(UC_HOOK_CODE,hook);u.emu_start(0x9ccc14,STOP,count=100)
  assert u.reg_read(UC_ARM_REG_PC)==STOP and u.reg_read(UC_ARM_REG_SP)==SP and u.reg_read(UC_ARM_REG_R0)==(OBJ if allocated else 0)
  assert len(calls)==(2 if allocated else 1)
  result=dict(ID=ident,allocation_fixture=allocated,returnR0=f'{u.reg_read(UC_ARM_REG_R0):08X}',calls=calls)
  if allocated:
   # Isolate E22's existing empty-list branch; no actual publication or post/queue claim.
   v=machine();v.mem_write(OBJ,bytes(u.mem_read(OBJ,24)));v.reg_write(UC_ARM_REG_R0,OBJ);executed=[]
   v.hook_add(UC_HOOK_CODE,lambda u,a,size,ignored:executed.append(a));v.emu_start(0x9aa3dc,STOP,count=30)
   assert v.reg_read(UC_ARM_REG_PC)==STOP and v.reg_read(UC_ARM_REG_SP)==SP
   assert executed==[0x9aa3dc,0x9aa3e0,0x9aa3e4,0x9aa3e8,0x9aa3ec,0x9aa3f0,0x9aa3f4,0x9aa3f8,0x9aa560,0x9aa564]
   result['dispatcher_empty_branch']=[f'{a:08X}' for a in executed];result['action_or_scheduler_calls']=0
  cases.append(result)
result=dict(engine_sha256=hashlib.sha256(raw).hexdigest(),boundary='Actual9CCC14+9D0418 constructor instructions, opaque allocator/publication ABI fixtures, then actual already-paid E22 empty-list branch. No real allocation/registry publication, admitted caller, post/queue, later action-list mutation or all-alias exclusion certified.',cases=cases)
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode());print(len(cases),'factory cases and3 exact empty-dispatch branches passed')
