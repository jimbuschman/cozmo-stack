"""Original033 five local output recipients; actual bounded selection bodies, ABI fixtures."""
from pathlib import Path
import hashlib,json,struct,sys,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE,UC_HOOK_MEM_READ
from unicorn.arm_const import *
p=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=p.read_bytes();e=lief.parse(str(p));loads=[s for s in e.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
P=0x20001000;DESC=0x20002000;ARRAY=0x20003000;OUT=0x20004000;NODE=0x20005000;STOP=0x20009000;SP=0x30008000;G=0x108de78
def put(u,a,x):u.mem_write(a,struct.pack('<I',x))
def machine():
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 u.mem_map(0x20000000,0x10000);u.mem_map(0x30000000,0x10000);u.reg_write(UC_ARM_REG_SP,SP);u.reg_write(UC_ARM_REG_LR,STOP)
 u.reg_write(UC_ARM_REG_C1_C0_2,u.reg_read(UC_ARM_REG_C1_C0_2)|(15<<20));u.reg_write(UC_ARM_REG_FPEXC,0x40000000);u.reg_write(UC_ARM_REG_FPSCR,0)
 put(u,DESC,ARRAY);put(u,DESC+4,1);put(u,ARRAY,P);put(u,P+0x14,777);put(u,P+0xe0,NODE);put(u,P+0x1c0,0x3f800000);put(u,G+0x50,P);put(u,OUT,0xa5a5a5a5);return u
cases=[]
for entry,filtered,expectedcall in ((0xa37100,True,0xa37258),(0xa37100,False,0xa373b4),(0xa373f4,True,0xa37578),(0xa373f4,False,0xa37650),(0xa37880,False,0xa37944)):
 for policy in (0,1,2):
  for modeword in (0,7):
   u=machine();calls=[];local=[];reads=[]
   if entry==0xa37100:
    for reg,value in ((UC_ARM_REG_R0,DESC),(UC_ARM_REG_R1,1),(UC_ARM_REG_R2,0x40000000),(UC_ARM_REG_R3,777 if filtered else 0)):u.reg_write(reg,value)
    for off,value in ((0,0),(4,1),(8,OUT),(12,2)):put(u,SP+off,value)
   elif entry==0xa373f4:
    for reg,value in ((UC_ARM_REG_R0,0x40000000),(UC_ARM_REG_R1,777 if filtered else 0),(UC_ARM_REG_R2,0),(UC_ARM_REG_R3,1)):u.reg_write(reg,value)
    put(u,SP,OUT);put(u,SP+4,2)
   else:
    u.reg_write(UC_ARM_REG_R0,0x40000000);put(u,G+0x20,ARRAY);put(u,G+0x24,1);u.mem_write(G+0x2c,struct.pack('<H',1));put(u,G+0x48,1)
   def hook(u,a,size,ignored):
    if a not in (0xa01768,0xa01ca4):return
    args=[u.reg_read(x) for x in (UC_ARM_REG_R0,UC_ARM_REG_R1)];assert args[0]==P;lr=u.reg_read(UC_ARM_REG_LR)
    if a==0xa01768:
     assert lr==expectedcall+4 and 0x30000000<=args[1]<SP;local.append(args[1]);put(u,args[1],modeword);ret=policy
    else:assert args[1]==2;ret=0xdeadbeef
    calls.append(dict(address=f'{a:08X}',LR=f'{lr:08X}',R0=f'{args[0]:08X}',R1=f'{args[1]:08X}',fixture_return=f'{ret:08X}'))
    for reg in (UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3,UC_ARM_REG_R12):u.reg_write(reg,0x5a5a5a5a)
    u.reg_write(UC_ARM_REG_R0,ret);u.reg_write(UC_ARM_REG_PC,lr)
   def memread(u,access,a,size,value,ignored):
    if local and a<local[0]+4 and local[0]<a+size:reads.append(dict(PC=f'{u.reg_read(UC_ARM_REG_PC):08X}',address=f'{a:08X}',size=size))
   u.hook_add(UC_HOOK_CODE,hook);u.hook_add(UC_HOOK_MEM_READ,memread);u.emu_start(entry,STOP,count=400)
   assert u.reg_read(UC_ARM_REG_PC)==STOP and u.reg_read(UC_ARM_REG_SP)==SP and u.reg_read(UC_ARM_REG_R0)==(0x50 if entry==0xa373f4 and policy==0 else 1)
   assert len(local)==1 and not reads and sum(x['address']=='00A01CA4' for x in calls)==int(policy==1)
   victim=struct.unpack('<I',u.mem_read(OUT,4))[0];assert victim==(NODE if entry!=0xa37880 and policy else 0xa5a5a5a5)
   cases.append(dict(entry=f'{entry:08X}',object_filter=filtered,query_call=f'{expectedcall:08X}',fixture_policy=policy,fixture_modeword=modeword,query_local=f'{local[0]:08X}',local_word_reads_after_query=reads,returnR0=f'{u.reg_read(UC_ARM_REG_R0):08X}',victim_output=f'{victim:08X}',calls=calls))
for j in range(0,len(cases),2):
 a,b=cases[j:j+2];assert a['returnR0']==b['returnR0'] and a['victim_output']==b['victim_output'] and a['calls']==b['calls']
out=dict(engine_sha256=hashlib.sha256(raw).hexdigest(),cases=cases,boundary='Actual three selection bodies/five local query sites, one explicit eligible PBI, incoming priority2 vs candidate1, acyclic list. Query/output and Stop are opaque ABI fixtures. No query/cache/callback execution or actual runtime admission. Output mode0/7 varies independently of fixture policy0/1/2. Read watch and matching results apply only to these cases, not all aliases/branches/inputs.')
Path(__file__).with_suffix('.json').write_bytes((json.dumps(out,indent=2)+'\n').encode());print(len(cases),'native local-output selection fixtures passed; no caller local-word read')
