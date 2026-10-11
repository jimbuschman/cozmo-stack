"""Actual descriptor factory slice; explicit allocation/insertion ABI fixtures.
No runtime admission, allocator, registry implementation or arbitrary field-value claim.
"""
from pathlib import Path
import sys, struct, hashlib, json, lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, __version__
from unicorn.arm_const import *
engine=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=engine.read_bytes();elf=lief.parse(str(engine))
loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
obj=0x20001000;stack=0x30008000;trap=0x40000000;ident=0x12345678
def machine():
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);end=max(s.virtual_address+s.virtual_size for s in loads)
 u.mem_map(0,(end+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 for base,size in ((0x20000000,0x10000),(0x30000000,0x10000),(trap,4096)):u.mem_map(base,size)
 # Explicit pool-ID pointer fixture at the actual common GOT address.
 u.mem_write(0x1040048,struct.pack('<I',0x20000080));u.mem_write(0x20000080,struct.pack('<I',42))
 u.mem_write(obj,b'\xa5'*0x80);u.reg_write(UC_ARM_REG_SP,stack);u.reg_write(UC_ARM_REG_LR,trap)
 return u
cases=[]
for kind,nonnull in ((0,True),(0,False),(1,True),(1,False),(2,True),(0xffffffff,True)):
 u=machine();u.reg_write(UC_ARM_REG_R0,ident);u.reg_write(UC_ARM_REG_R1,kind);calls=[]
 def hook(u,a,size,user):
  if a==0xa7a7f4:
   pool=u.reg_read(UC_ARM_REG_R0);length=u.reg_read(UC_ARM_REG_R1)
   assert kind in (0,1) and pool==42 and length==(0x48 if kind==0 else 0x38)
   calls.append({'interface':'allocate','pool_fixture':pool,'size':length,'returned_pointer_fixture':obj if nonnull else 0})
   u.reg_write(UC_ARM_REG_R0,obj if nonnull else 0);u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
  elif a==0xa3b434:
   assert nonnull and kind in (0,1) and u.reg_read(UC_ARM_REG_R0)==obj
   b=bytes(u.mem_read(obj,0x48 if kind==0 else 0x38));words=struct.unpack_from('<4I',b)
   assert words[0]==(0x103bfa0 if kind==0 else 0x103bfc8) and words[2:]==(ident,1)
   assert b[0x10:]==b'\xa5'*(len(b)-0x10)
   calls.append({'interface':'insert-request','object':obj,'snapshot_before_interface_hex':b.hex(),'unmodified_payload_pattern_hex':'A5'})
   # Insertion effects are not simulated; its unspecified R0 is intentionally clobbered.
   u.reg_write(UC_ARM_REG_R0,0xdead0000);u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
 u.hook_add(UC_HOOK_CODE,hook);u.emu_start(0xa3b694,trap,count=1000)
 assert u.reg_read(UC_ARM_REG_PC)==trap and u.reg_read(UC_ARM_REG_SP)==stack
 ret=u.reg_read(UC_ARM_REG_R0);assert ret==(obj if kind in (0,1) and nonnull else 0)
 assert len(calls)==(2 if kind in (0,1) and nonnull else 1 if kind in (0,1) else 0)
 if ret:assert bytes(u.mem_read(obj+0x10,(0x48 if kind==0 else 0x38)-0x10))==b'\xa5'*((0x48 if kind==0 else 0x38)-0x10)
 cases.append({'id_fixture_hex':f'{ident:08X}','type_fixture_hex':f'{kind:08X}','allocation_nonnull_fixture':nonnull,'return':ret,'calls':calls})
result={'boundary':'Actual A3B694 and9D0418 instructions. Explicit GOT pool-ID42, allocation pointer/null and A5-filled object fixtures; A3B434 is an insertion-request ABI boundary with no registry effects simulated. No actual shipped caller, field-value, allocator initialization, global binding, insertion success or supported-input exclusion proof.','engine_sha256':hashlib.sha256(raw).hexdigest(),'unicorn_version':__version__,'cases':cases}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
print('Native factory cases',len(cases),'passed; descriptor bytes unchanged at insertion boundary under explicit fixtures.')
