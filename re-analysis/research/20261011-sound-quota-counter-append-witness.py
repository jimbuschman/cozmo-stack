"""Original037 native append/order decisions; explicit PBI/Play boundary fixtures."""
from pathlib import Path
import hashlib,json,struct,sys,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE
from unicorn.arm_const import *
p=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=p.read_bytes();e=lief.parse(str(p));loads=[s for s in e.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
G=0x108de78;P=0x20001000;OLD=0x20002000;HOLDER=0x20003000;SP=0x30008000;STOP=0x20009000
def machine(nonempty,count):
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 u.mem_map(0x20000000,0x10000);u.mem_map(0x30000000,0x10000);u.reg_write(UC_ARM_REG_SP,SP);u.reg_write(UC_ARM_REG_LR,STOP);u.reg_write(UC_ARM_REG_R0,P);u.reg_write(UC_ARM_REG_R1,0x11223344)
 u.mem_write(G+0x48,struct.pack('<3I',count,OLD if nonempty else 0,OLD if nonempty else 0));u.mem_write(P+0x104,struct.pack('<I',0xa5a5a5a5));return u
def state(u):return [f'{x:08X}' for x in struct.unpack('<3I',u.mem_read(G+0x48,12))]+[f'{struct.unpack("<I",u.mem_read(P+0x104,4))[0]:08X}',f'{struct.unpack("<I",u.mem_read(OLD+0x104,4))[0]:08X}']
cases=[]
for entry in (0xa36fe0,0x97dea4,0x97de3c):
 for nonempty,count in ((False,0),(True,0xffffffff)):
  for result in (1,2,0x50):
   for third in ((0,1,2,0xffffffff) if entry==0xa36fe0 else (0,)):
    u=machine(nonempty,count);u.reg_write(UC_ARM_REG_R2,third);calls=[]
    if entry==0x97de3c:
     u.mem_write(HOLDER,struct.pack('<I',P));u.mem_write(SP+0x1f4,struct.pack('<I',HOLDER));u.mem_write(SP+0x1d8,struct.pack('<I',0x11223344));u.reg_write(UC_ARM_REG_R11,0);u.reg_write(UC_ARM_REG_R7,3)
    def hook(u,a,size,ignored):
     if a not in (0xa00618,0xa0067c):return
     args=[u.reg_read(x) for x in (UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3)];assert args[0]==P
     snapshot=state(u);calls.append(dict(address=f'{a:08X}',args=[f'{v:08X}' for v in args],counter_list_before_boundary=snapshot))
     if entry!=0xa36fe0:assert snapshot[0]==f'{(count+1)&0xffffffff:08X}' and snapshot[1]==f'{P:08X}' and snapshot[3]=='00000000'
     if a==0xa0067c:
      assert args[1]==0x11223344
      assert args[2:]==([int(third==1),0] if entry==0xa36fe0 else [0,1] if entry==0x97dea4 else [0,3])
     lr=u.reg_read(UC_ARM_REG_LR)
     for reg in (UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3,UC_ARM_REG_R12):u.reg_write(reg,0x5a5a5a5a)
     u.reg_write(UC_ARM_REG_R0,result if a==0xa0067c else 0xdeadbeef);u.reg_write(UC_ARM_REG_PC,lr)
    u.hook_add(UC_HOOK_CODE,hook);end=0x97de74 if entry==0x97de3c else STOP;u.emu_start(entry,end,count=120)
    assert u.reg_read(UC_ARM_REG_PC)==end and u.reg_read(UC_ARM_REG_SP)==SP and len(calls)==2
    appended=entry!=0xa36fe0 or result==1
    final=state(u);assert int(final[0],16)==((count+int(appended))&0xffffffff)
    assert int(final[1],16)==(P if appended else OLD if nonempty else 0)
    assert int(final[2],16)==(OLD if nonempty else P if appended else 0)
    assert int(final[3],16)==(0 if appended else 0xa5a5a5a5);assert int(final[4],16)==(P if appended and nonempty else 0)
    if entry!=0x97de3c:assert u.reg_read(UC_ARM_REG_R0)==result
    cases.append(dict(entry=f'{entry:08X}',nonempty=nonempty,initial_count=f'{count:08X}',incoming_third=f'{third:08X}',Play_fixture_result=f'{result:08X}',calls=calls,final_counter_tail_head_Pnext_oldnext=final,appended=appended))
out=dict(engine_sha256=hashlib.sha256(raw).hexdigest(),cases=cases,boundary='ActualA36FE0/97DEA4 and97DE3C..74 instructions, including actualA3709C. A00618/A0067C are opaque ABI fixtures, not complete Init/Play execution or a claim about callback mutations;97DE3C starts after live virtualA8, not whole creator. Empty/nonempty acyclic lists and explicit PBI/arguments only. No admitted complete supplier, duplicate/cycle/concurrency or all-writer closure.')
Path(__file__).with_suffix('.json').write_bytes((json.dumps(out,indent=2)+'\n').encode());print(len(cases),'actual append/order fixtures passed')
