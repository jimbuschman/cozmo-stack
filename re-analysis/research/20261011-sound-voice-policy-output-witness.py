"""Original033 actual call-mediated output pointer formation/branch controls only."""
from pathlib import Path
import hashlib,json,struct,sys,lief,capstone
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM
from unicorn.arm_const import *
from capstone.arm import *
p=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=p.read_bytes();e=lief.parse(str(p));loads=[s for s in e.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
SP=0x30008000;V=0x20001000;md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM);md.detail=True
def read(a,z):
 for s in loads:
  if s.virtual_address<=a and a+z<=s.virtual_address+s.physical_size:return raw[s.file_offset+a-s.virtual_address:s.file_offset+a-s.virtual_address+z]
 raise ValueError(hex(a))
def machine():
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 u.mem_map(0x20000000,0x10000);u.mem_map(0x30000000,0x10000);u.reg_write(UC_ARM_REG_SP,SP);u.reg_write(UC_ARM_REG_R4,V);return u
rows=[]
for form,stop,handoff,hstop,call,out in ((0xa371e4,0xa371f0,0xa37234,0xa3723c,0xa37258,SP+0x14),(0xa37358,0xa3735c,0xa37390,0xa37398,0xa373b4,SP+0x14),(0xa3751c,0xa37520,0xa37560,0xa37568,0xa37578,SP+0xc),(0xa37634,0xa37638,0xa37644,0xa3764c,0xa37650,SP+0xc),(0xa378e4,0xa378e8,0xa37930,0xa37934,0xa37944,SP+0xc),(0xa559c0,0xa559c4,None,None,0xa559c4,V+0xe0),(0xa55b50,0xa55b54,None,None,0xa55b54,V+0xe0)):
 u=machine();u.emu_start(form,stop,count=3);assert u.reg_read(UC_ARM_REG_PC)==stop
 if handoff is not None:u.emu_start(handoff,hstop,count=2);assert u.reg_read(UC_ARM_REG_PC)==hstop
 assert u.reg_read(UC_ARM_REG_R1)==out and u.reg_read(UC_ARM_REG_SP)==SP
 u.emu_start(call,0xa01768,count=1);assert u.reg_read(UC_ARM_REG_PC)==0xa01768 and u.reg_read(UC_ARM_REG_LR)==call+4 and u.reg_read(UC_ARM_REG_R1)==out
 rows.append(dict(formation=f'{form:08X}..{stop:08X}',handoff=None if handoff is None else f'{handoff:08X}..{hstop:08X}',call=f'{call:08X}',R1=f'{out:08X}',destination='currentSP+14' if out==SP+0x14 else 'currentSP+C' if out==SP+0xc else 'suppliedV+E0',scope='Actual small instruction slices and one BL; stops before paid query body, not complete caller/admission or later local copy/escape proof.'))
retained=[]
for a,b,reg in ((0xa37358,0xa373b4,ARM_REG_R6),(0xa3751c,0xa37578,ARM_REG_R8),(0xa37634,0xa37650,ARM_REG_R8),(0xa378e4,0xa37944,ARM_REG_R9)):
 ins=list(md.disasm(read(a,b-a),a));assert ins and ins[-1].address+ins[-1].size==b
 writes=[i.address for i in ins if reg in i.regs_access()[1]];assert writes==[a];assert all(ARM_REG_SP not in i.regs_access()[1] for i in ins)
 retained.append(dict(interval=f'{a:08X}..{b:08X}',carrier=ins[0].reg_name(reg),writes=[f'{x:08X}' for x in writes],SP_writes=0,boundary='Static primary interval; no alternate incoming branch or complete path feasibility claim.'))
ins=list(md.disasm(read(0xa371e4,0xa37258-0xa371e4),0xa371e4));slotwrites=[i.address for i in ins if i.mnemonic.startswith('str') and any(o.type==ARM_OP_MEM and o.mem.base==ARM_REG_SP and o.mem.disp==0 for o in i.operands)];assert slotwrites==[0xa371ec]
out=dict(engine_sha256=hashlib.sha256(raw).hexdigest(),pointer_controls=rows,retained_carrier_intervals=retained,first_stack_pointer_slot_writes=[f'{x:08X}' for x in slotwrites],boundary='Seven actual immediate call/pointer controls, primary retained-carrier intervals. No query-body reexecution, complete caller/runtime admission, unrestricted alias or later stack-value copy/escape exclusion.')
Path(__file__).with_suffix('.json').write_bytes((json.dumps(out,indent=2)+'\n').encode());print('seven native output-pointer/BL controls and five retained-carrier intervals passed')
