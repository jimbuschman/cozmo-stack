"""Actual9CCD20 action-list decisions, explicit index/mutex/division ABI fixtures."""
from pathlib import Path
import sys,json,struct,hashlib,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE
from unicorn.arm_const import *
engine=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=engine.read_bytes();elf=lief.parse(str(engine));loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
E=0x20001000;A=0x20002000;B=0x20003000;I=0x20004000;BUCKETS=0x20005000;STOP=0x20009000;SP=0x30008000;ID=42
def run(kind):
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 u.mem_map(0x20000000,0x10000);u.mem_map(0x30000000,0x10000)
 def put(a,v):u.mem_write(a,struct.pack('<I',v&0xffffffff))
 def word(a):return struct.unpack('<I',u.mem_read(a,4))[0]
 put(0x108d8e0,I);put(I+0x54,BUCKETS);put(I+0x58,0 if kind=='empty index' else 1)
 put(A+8,ID if kind!='missing key' else ID+1);put(A+0xc,0xffffffff);put(A+4,B if kind=='first duplicate key' else 0);put(A+0x10,0x20007000)
 put(B+8,ID);put(B+0xc,17);put(B+0x10,0)
 put(BUCKETS,0 if kind=='empty bucket' else A)
 head=B if kind=='existing different tail' else A if kind=='already same action' else 0
 put(E+0x10,head);put(E+0x14,0xa5a5a5a5)
 if kind=='already same action':put(A+0x10,0)
 u.reg_write(UC_ARM_REG_SP,SP);u.reg_write(UC_ARM_REG_LR,STOP);u.reg_write(UC_ARM_REG_R0,E);u.reg_write(UC_ARM_REG_R1,0 if kind=='zero ID' else ID);calls=[]
 def hook(u,a,size,ignored):
  if a not in (0x4d3064,0x4d3070,0x4a6694):return
  lr=u.reg_read(UC_ARM_REG_LR);r0=u.reg_read(UC_ARM_REG_R0);r1=u.reg_read(UC_ARM_REG_R1)
  if a==0x4a6694:assert r0==ID and r1==1
  else:assert r0==I+0x50
  calls.append(dict(boundary=f'{a:08X}',R0=f'{r0:08X}',R1=f'{r1:08X}'))
  for reg in (UC_ARM_REG_R2,UC_ARM_REG_R3,UC_ARM_REG_R12):u.reg_write(reg,0x5a5a5a5a)
  # Fixture supplies bucket index0, not a native numerical-provider claim.
  u.reg_write(UC_ARM_REG_R0,0xcafebabe if a==0x4a6694 else 0);u.reg_write(UC_ARM_REG_R1,0);u.reg_write(UC_ARM_REG_PC,lr)
 u.hook_add(UC_HOOK_CODE,hook);u.emu_start(0x9ccd20,STOP,count=200)
 assert u.reg_read(UC_ARM_REG_PC)==STOP and u.reg_read(UC_ARM_REG_SP)==SP
 success=kind in ('empty event','existing different tail','already same action','first duplicate key');expected=1 if success else 0xe if kind=='zero ID' else 2
 assert u.reg_read(UC_ARM_REG_R0)==expected and word(E+0x14)==0xa5a5a5a5
 if success:
  assert word(A+0xc)==0 and word(B+0xc)==17
  assert word(E+0x10)==(head if head else A)
  assert word(A+0x10)==(A if kind=='already same action' else 0)
  if kind=='existing different tail':assert word(B+0x10)==A
 else:assert word(E+0x10)==head and word(A+0xc)==0xffffffff
 return dict(case=kind,returnR0=expected,event_head=f'{word(E+0x10):08X}',event14=f'{word(E+0x14):08X}',action_refC=f'{word(A+0xc):08X}',action_next10=f'{word(A+0x10):08X}',second_action_refC=f'{word(B+0xc):08X}',second_action_next10=f'{word(B+0x10):08X}',interfaces=calls)
cases=[run(k) for k in ('zero ID','empty index','empty bucket','missing key','empty event','existing different tail','already same action','first duplicate key')]
result=dict(engine_sha256=hashlib.sha256(raw).hexdigest(),boundary='Actual9CCD20 body with explicit supplied Event/Action/index shapes. Mutex and UIDIVMOD interfaces are fixture boundaries; numerical provider/signal bodies not executed. No actual caller/ID/Event admission, publication, later mutation, concurrency or cycle reachability is certified.',cases=cases)
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode());print(len(cases),'native append cases passed; same-action fixture forms self-link')
