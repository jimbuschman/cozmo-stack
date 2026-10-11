"""Original9F2F88 tail, seven already-recovered table products only.
Actual instructions and static table bytes; explicit receiver/frame fixtures, no callee execution.
"""
from pathlib import Path
import sys,struct,hashlib,json,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,__version__
from unicorn.arm_const import *
p=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=p.read_bytes();e=lief.parse(str(p));loads=[s for s in e.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
def read(a,size):
 for s in loads:
  if s.virtual_address<=a and a+size<=s.virtual_address+s.physical_size:return raw[s.file_offset+a-s.virtual_address:s.file_offset+a-s.virtual_address+size]
 raise ValueError(hex(a))
products=[('RanSeq',0x103b860,0x98154c),('Sound',0x103bad0,0xa1dfe0),('Switch',0x103bdc8,0x98154c),('ActorMixer',0x103cf88,0x98154c),('Layer',0x103b050,0x98154c),('bus base',0x103ace0,0x9c3d04),('bus second',0x103d100,0x9c3d04)]
obj=0x20000000;frame=0x30004000;trap=0x40000000;cases=[]
for label,table,target in products:
 assert struct.unpack('<I',read(table+0xcc,4))[0]==0x9f2e34
 assert struct.unpack('<I',read(table+0xc4,4))[0]==target and target!=0x9f319c
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);end=max(s.virtual_address+s.virtual_size for s in loads);u.mem_map(0,(end+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 for base,size in ((obj,4096),(0x30000000,0x10000),(trap,4096)):u.mem_map(base,size)
 u.mem_write(obj,struct.pack('<I',table));u.mem_write(frame+0x2c,struct.pack('<I',obj));u.mem_write(frame+0x34,struct.pack('<9I',*range(0x100,0x108),trap))
 u.reg_write(UC_ARM_REG_SP,frame);u.reg_write(UC_ARM_REG_R1,0xdeadbeef)
 # Stop before selected C4 callee; its already-paid body is not re-executed.
 u.emu_start(0x9f2f70,target,count=100)
 assert u.reg_read(UC_ARM_REG_PC)==target and u.reg_read(UC_ARM_REG_R0)==obj and u.reg_read(UC_ARM_REG_R1)==0
 assert u.reg_read(UC_ARM_REG_SP)==frame+0x58 and u.reg_read(UC_ARM_REG_LR)==trap
 cases.append({'established_product':label,'vtable_fixture':f'{table:08X}','slotCC_address':f'{table+0xcc:08X}','slotCC_value':'009F2E34','slotC4_address':f'{table+0xc4:08X}','selected_C4_target':f'{target:08X}','receiver_at_tail':f'{obj:08X}','argumentR1':0,'stack_delta':0x58,'stopped_before_callee':True})
refs=[]
for s in e.sections:
 b=bytes(s.content)
 for value in (0x9f2e34,0x9f2e35):
  needle=struct.pack('<I',value);j=b.find(needle)
  while j>=0:
   if (s.virtual_address+j)%4==0:refs.append({'section':s.name,'address':f'{s.virtual_address+j:08X}','value':f'{value:08X}'})
   j=b.find(needle,j+1)
known={x['slotCC_address'] for x in cases};others=[x for x in refs if x['address'] not in known]
assert len(refs)==22 and len(others)==15
result={'engine_sha256':hashlib.sha256(raw).hexdigest(),'unicorn_version':__version__,'boundary':'Actual9F2F70..88 and original static C4/CC bytes for seven previously established BI378A/NR146B/NT201A/SI283A/BH549A products. Explicit receiver/table/frame fixtures, not a fresh class construction or actual caller/input membership proof. Stop before C4 callee. Other15 exact pointer candidates are navigation only, not fifteen admitted products or a new family census. Alternate vtables, mutations and arbitrary aliases stay UNKNOWN.','tail_instruction_hex':read(0x9f2f70,0x1c).hex(),'cases':cases,'exact_method_pointer_candidates':refs,'other_unbound_pointer_candidates':others}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
print('Seven original table/C4 tail controls passed;15 other raw pointer candidates remain unbound.')
