"""Run the shipped startup rounding slice and scalar timing setters; explicit external-input fixtures only."""
from pathlib import Path
import sys,json,struct,hashlib,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE
from unicorn.arm_const import *
ROOT=Path('resources/lib/armeabi-v7a');engine=ROOT/'libcozmoEngine.so';provider=ROOT/'libacattsandroid.so'
images=[]
for file,base in ((engine,0),(provider,0x20000000)):
 raw=file.read_bytes();elf=lief.parse(str(file));loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD];images.append((file,base,raw,loads,elf))

def machine():
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM)
 for file,base,raw,loads,elf in images:
  end=max(s.virtual_address+s.virtual_size for s in loads);u.mem_map(base,(end+4095)&~4095)
  for s in loads:u.mem_write(base+s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 u.mem_map(0x40000000,0x20000);u.mem_map(0x50000000,0x20000);u.mem_map(0x60000000,4096)
 u.reg_write(UC_ARM_REG_SP,0x50010000);u.reg_write(UC_ARM_REG_LR,0x60000000)
 u.reg_write(UC_ARM_REG_C1_C0_2,u.reg_read(UC_ARM_REG_C1_C0_2)|(15<<20));u.reg_write(UC_ARM_REG_FPEXC,0x40000000);u.reg_write(UC_ARM_REG_FPSCR,0)
 return u

def w(u,a,v):u.mem_write(a,struct.pack('<I',v&0xffffffff))
def r(u,a):return struct.unpack('<I',u.mem_read(a,4))[0]
rounding=[]
for quantum in (0,1,128,256,512,1024,2048,2049,4096,65535,0xffffffff):
 u=machine();w(u,0x108d92c,1024);w(u,0x108df94,quantum);u.mem_write(0x40000044,b'\x01');u.reg_write(UC_ARM_REG_R4,0x40000000);u.reg_write(UC_ARM_REG_R7,0x104028c);calls=[];stop=[]
 def hook(u,a,size,user):
  if a in (0x4a6694,0x4be310):
   calls.append({'site':f'{u.reg_read(UC_ARM_REG_LR)-4:08X}','interface':'uidivmod' if a==0x4a6694 else 'uidiv','r0':u.reg_read(UC_ARM_REG_R0),'r1':u.reg_read(UC_ARM_REG_R1)})
   # Harness chooses this positively shipped implementation; no runtime binding claim.
   u.reg_write(UC_ARM_REG_PC,0x2003ff75 if a==0x4a6694 else 0x2003fee5)
  elif a in (0xa57834,0x2003ff90):stop.append(f'{a:08X}');u.emu_stop()
 u.hook_add(UC_HOOK_CODE,hook);u.emu_start(0xa577b8,0x60000000,count=20000)
 assert stop,'instruction cap, not a settled path'
 rounding.append({'external_quantum_fixture':quantum,'initial_request':1024,'rounding_enabled':1,'final_request':r(u,0x108d92c),'final_query_quantum':r(u,0x108df94),'stop':stop,'helper_calls':calls})
setters=[]
for rate,frames in ((48000,0),(48000,1024),(22320,1024),(1,1024),(1022,1024),(1023,1024),(1024,1024),(48000,2048)):
 u=machine();u.reg_write(UC_ARM_REG_R0,rate);u.emu_start(0xa1c75c,0x60000000,count=200);u.reg_write(UC_ARM_REG_R0,frames);u.reg_write(UC_ARM_REG_LR,0x60000000);u.emu_start(0xa1c7d4,0x60000000,count=200)
 assert u.reg_read(UC_ARM_REG_PC)==0x60000000
 words=[r(u,0x105243c+4*k) for k in range(5)];assert words[:2]==[rate,frames]
 w(u,0x108d870,0x40000100);w(u,0x4000014c,37);u.reg_write(UC_ARM_REG_R0,0);u.reg_write(UC_ARM_REG_SP,0x50010000);u.reg_write(UC_ARM_REG_LR,0x60000000);query_calls=[];query_stop=[]
 def query_hook(u,a,size,user):
  if a==0x4b3e70:
   query_calls.append({'interface':'idiv','r0':u.reg_read(UC_ARM_REG_R0),'r1':u.reg_read(UC_ARM_REG_R1)})
   u.reg_write(UC_ARM_REG_PC,0x200f4409)
  elif a==0x9ebeac:
   query_calls.append({'interface':'uidivmod','r0':u.reg_read(UC_ARM_REG_R0),'r1':u.reg_read(UC_ARM_REG_R1)})
   query_stop.append('uidivmod call frontier');u.emu_stop()
  elif a==0x2003ff90:query_stop.append('zero-handler entry frontier');u.emu_stop()
 u.hook_add(UC_HOOK_CODE,query_hook);u.emu_start(0x9ebe6c,0x60000000,count=20000);assert query_stop
 setters.append({'rate_fixture':rate,'frame_fixture':frames,'native_setting_words_hex':[f'{x:08X}' for x in words],'duration_word':words[2],'queue_count_fixture':37,'query_calls':query_calls,'query_stop':query_stop})
assert next(x for x in rounding if x['external_quantum_fixture']==2048)['final_request']==2048
assert next(x for x in rounding if x['external_quantum_fixture']==2049)['final_request']==0
assert next(x for x in rounding if x['external_quantum_fixture']==4096)['final_request']==0
assert next(x for x in rounding if x['external_quantum_fixture']==0)['stop']==['2003FF90']
assert [x['duration_word'] for x in setters]==[0,21,45,1023999,1001,1000,999,42]
assert setters[0]['query_stop']==['zero-handler entry frontier']
assert [x['query_calls'][-1]['r1'] for x in setters[1:]]==[47,22,0,0,1,1,23]
result={'scope':'Native startup rounding and timing-parameter stores only. External return fixtures are not proof of actual Android values or shipped request admission. Selected acatts helper is a harness binding, not the runtime winner. No zero-handler/raise behavior is simulated.','initial_fpscr_hex':'00000000','unicorn_version':'2.1.4','engine_sha256':hashlib.sha256(engine.read_bytes()).hexdigest(),'provider_sha256':hashlib.sha256(provider.read_bytes()).hexdigest(),'rounding_cases':rounding,'setter_cases':setters}
Path(__file__).with_suffix('.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
for x in rounding:print('quantum',x['external_quantum_fixture'],'-> frame',x['final_request'],'stop',x['stop'])
for x in setters:print('timing',x['rate_fixture'],x['frame_fixture'],'-> duration',x['duration_word'])
