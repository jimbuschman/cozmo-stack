"""Original037 concrete ordinary-creator callback input and postcallback reload."""
from pathlib import Path
import hashlib,json,struct,sys,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE
from unicorn.arm_const import *
p=Path('resources/lib/armeabi-v7a/libcozmoEngine.so'); raw=p.read_bytes(); e=lief.parse(str(p))
loads=[s for s in e.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
G=0x108de78; N=0x20000100; P=0x20001000; OTHER=0x20002000; HOLDER=0x20003000; OLD=0x20004000; SP=0x30008000
def word(u,a):return struct.unpack('<I',u.mem_read(a,4))[0]
def put(u,a,v):u.mem_write(a,struct.pack('<I',v))
cases=[]
for nonempty in (False,True):
 for replace in (False,True):
  for callback_result in (0,1,0xffffffff):
   u=Uc(UC_ARCH_ARM,UC_MODE_ARM); u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
   for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
   u.mem_map(0x20000000,0x10000);u.mem_map(0x30000000,0x10000);u.mem_write(SP,b'\xa5'*0x220)
   u.reg_write(UC_ARM_REG_SP,SP);u.reg_write(UC_ARM_REG_R4,N);u.reg_write(UC_ARM_REG_R6,0x12345678)
   u.reg_write(UC_ARM_REG_R7,1);u.reg_write(UC_ARM_REG_R10,SP+0x64)
   put(u,N,0x103bad0);put(u,HOLDER,P);put(u,SP+0x1f4,HOLDER);put(u,SP+0x1ec,0x87654321);put(u,SP+0x1d8,0x11223344)
   put(u,P+0x140,0x24681357);put(u,G+0x48,0xffffffff if nonempty else 0);put(u,G+0x4c,OLD if nonempty else 0);put(u,G+0x50,OLD if nonempty else 0)
   put(u,P+0x104,0xcafef00d);put(u,OTHER+0x104,0xcafef00d)
   chosen=OTHER if replace else P;calls=[]
   def hook(u,a,size,ignored):
    if a not in (0x9f1be8,0xa00618,0xa0067c):return
    args=[u.reg_read(x) for x in (UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3)]
    rec={'address':f'{a:08X}','args':[f'{v:08X}' for v in args],'count_tail_head':[f'{word(u,G+x):08X}' for x in (0x48,0x4c,0x50)]}
    if a==0x9f1be8:
     assert args==[N,SP+0x64,P+0x34,1]
     expected=struct.pack('<8I',0x12345678,0xa5a5ff00,0,0,0x24681357,0x87654321,0,P)
     descriptor=bytes(u.mem_read(SP+0x64,32));assert descriptor==expected
     rec['descriptor_hex']=descriptor.hex();assert word(u,G+0x48)==(0xffffffff if nonempty else 0)
     if replace:put(u,HOLDER,OTHER)
    else:
     assert args[0]==chosen and word(u,G+0x48)==(0 if nonempty else 1)
     assert word(u,G+0x4c)==chosen and word(u,G+0x50)==(OLD if nonempty else chosen)
     assert word(u,chosen+0x104)==0
     if a==0xa0067c:assert args[1:]==[0x11223344,0,1]
    calls.append(rec);lr=u.reg_read(UC_ARM_REG_LR)
    for reg in (UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3,UC_ARM_REG_R12):u.reg_write(reg,0x5a5a5a5a)
    u.reg_write(UC_ARM_REG_R0,callback_result if a==0x9f1be8 else 0x50);u.reg_write(UC_ARM_REG_PC,lr)
   u.hook_add(UC_HOOK_CODE,hook);u.emu_start(0x97dddc,0x97de74,count=150)
   assert u.reg_read(UC_ARM_REG_PC)==0x97de74 and u.reg_read(UC_ARM_REG_SP)==SP
   assert [x['address'] for x in calls]==['009F1BE8','00A00618','00A0067C']
   assert word(u,OLD+0x104)==(chosen if nonempty else 0)
   assert word(u,(P if replace else OTHER)+0x104)==0xcafef00d
   cases.append({'nonempty':nonempty,'holder_replaced_at_callback_fixture':replace,'callback_fixture_result':f'{callback_result:08X}','calls':calls,'appended_pointer':f'{chosen:08X}'})
out={'engine_sha256':hashlib.sha256(raw).hexdigest(),'cases':cases,'boundary':'Actual97DDDC..97DE70 instructions and actualA3709C append; explicit Sound table103BAD0 and PBI/frame fixtures. Paid9F1BE8 and Init/Play are opaque ABI boundaries. Callback holder mutation is a deliberate fixture, not an admitted real mutation. Stops before97DE74 outward branch; no whole creator/preparation/modulation body, actual concurrency, arbitrary alias or all-writer proof.'}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(out,indent=2)+'\n').encode('utf-8'))
print(len(cases),'native creator modulation/append handoff fixtures passed')
