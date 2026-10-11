"""Approved no-JNI source branch; native parameter stores only,not a full-init or global-invariance proof."""
from pathlib import Path
import sys,struct,json,hashlib,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE
from unicorn.arm_const import *
file=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=file.read_bytes();elf=lief.parse(str(file));loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
u=Uc(UC_ARCH_ARM,UC_MODE_ARM);end=max(s.virtual_address+s.virtual_size for s in loads);u.mem_map(0,(end+4095)&~4095)
for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
u.mem_map(0x50000000,0x20000);u.mem_map(0x60000000,4096)
u.reg_write(UC_ARM_REG_SP,0x50010000);u.reg_write(UC_ARM_REG_LR,0x60000000)
u.reg_write(UC_ARM_REG_C1_C0_2,u.reg_read(UC_ARM_REG_C1_C0_2)|(15<<20));u.reg_write(UC_ARM_REG_FPEXC,0x40000000);u.reg_write(UC_ARM_REG_FPSCR,0)
def w(a,v):u.mem_write(a,struct.pack('<I',v&0xffffffff))
def r(a):return struct.unpack('<I',u.mem_read(a,4))[0]
assert r(0x1040128)==0x108da38 and r(0x1040098)==0x108d90c
# Only required native-default input fields are fixtures;no constructor/backing/syscall stand-ins.
w(0x108d92c,1024);w(0x108da84,0);w(0x108da70,0)
seen=[];stop=[];branches=[]
def hook(u,a,size,user):
 if a in (0xa1c75c,0xa1c7d4):seen.append({'entry':f'{a:08X}','argument_r0':u.reg_read(UC_ARM_REG_R0)})
 if a in (0xa578a0,0xa578b4,0xa57848,0xa57850):branches.append(f'{a:08X}')
 if a==0xa40bc0:stop.append('final setup interface');u.emu_stop()
 if a in (0x4a6694,0x4be310,0x4b3e70):raise AssertionError('no-JNI startup unexpectedly invoked numerical import')
u.hook_add(UC_HOOK_CODE,hook);u.emu_start(0xa57724,0x60000000,count=2000)
words=[r(0x105243c+4*k) for k in range(5)]
assert seen==[{'entry':'00A1C75C','argument_r0':48000},{'entry':'00A1C7D4','argument_r0':1024}] and words==[48000,1024,21,5,128] and stop==['final setup interface']
result={'scope':'Approved no-JNI input branch only;required fields supplied from native constructor values;stops at existing final setup interface,no full-init/implementation result or all-writer claim.','engine_sha256':hashlib.sha256(raw).hexdigest(),'native_calls':seen,'branch_entries':branches,'setting_words_hex':[f'{x:08X}' for x in words],'query_block_words_hex':[f'{r(0x108df90+4*k):08X}' for k in range(3)],'stop':stop,'fixture_platform_address':'0108DA38','fixture_engine_config_address':'0108D90C','initial_platform_vm':0,'initial_platform_rate':0,'initial_engine_frame_request':1024,'initial_fpscr_hex':'00000000','unicorn_version':'2.1.4'}
Path(__file__).with_suffix('.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('no-JNI calls',seen,'setting words',words,'stop',stop)
