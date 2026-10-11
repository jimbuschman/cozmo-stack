"""One actual engine instruction per explicit fixture; native vs navigation model.
This does not certify caller admission or mutable key/object values.
"""
from pathlib import Path
import importlib.util,sys,struct,json,hashlib,contextlib,io,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,__version__
from unicorn.arm_const import *
from capstone.arm import *
modelpath=Path(__file__).with_name('20261011-sound-paired-block-constant-model.py')
spec=importlib.util.spec_from_file_location('paired_model',modelpath);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
namespace={'__file__':str(modelpath)}
with contextlib.redirect_stdout(io.StringIO()):exec(compile(m.build_source((0x108d9a0,),controls=True),str(modelpath),'exec'),namespace)
n=namespace['n'];engine=n.path;raw=engine.read_bytes();elf=lief.parse(str(engine));loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
got=0x10400e8
cases=[]
registers={'r0':(ARM_REG_R0,UC_ARM_REG_R0),'r1':(ARM_REG_R1,UC_ARM_REG_R1),'r2':(ARM_REG_R2,UC_ARM_REG_R2),'r3':(ARM_REG_R3,UC_ARM_REG_R3),'sp':(ARM_REG_SP,UC_ARM_REG_SP),'lr':(ARM_REG_LR,UC_ARM_REG_LR)}
fixtures=[
 (0x9af358,'sp',got-0x48,['r2','r3'],'paired GOT fixture'),
 (0x9b227c,'r2',got-4,['r0','r1'],'IB GOT fixture'),
 (0x9a5658,'lr',got,['r0','r1','r2','r3','lr'],'IA/writeback GOT fixture'),
 (0x9b7ee8,'r2',got+8,['r1','r2'],'DB/base-in-list without writeback GOT fixture'),
 (0x9b227c,'r2',0x108d9a0,['r0','r1'],'mutable keys must stay unknown'),
]
for address,base,value,outputs,label in fixtures:
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);end=max(s.virtual_address+s.virtual_size for s in loads);u.mem_map(0,(end+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 # Nonzero explicit key fixture demonstrates why initial BSS is not a runtime value.
 if value==0x108d9a0:u.mem_write(value,struct.pack('<4I',0x11223344,0x55667788,0x99aabbcc,0xddeeff00))
 u.reg_write(registers[base][1],value);u.emu_start(address,0,count=1)
 observed=[]
 with contextlib.redirect_stdout(io.StringIO()):namespace['execute'](n.read(address,4),0,address,False,{registers[base][0]:value},lambda ins,env:observed.append(env))
 assert len(observed)==1
 model=observed[0];native={r:u.reg_read(registers[r][1]) for r in outputs}
 if value==0x108d9a0:
  assert all(registers[r][0] not in model for r in outputs)
  assert native=={'r0':0x55667788,'r1':0x99aabbcc}
 else:
  assert all(model.get(registers[r][0])==native[r] for r in outputs),(label,model,native)
 cases.append({'instruction_address':f'{address:08X}','instruction_hex':n.read(address,4).hex(),'fixture':label,'base_register':base,'base_value':f'{value:08X}','native':{r:f'{v:08X}' for r,v in native.items()},'model':{r:f'{model[registers[r][0]]:08X}' if registers[r][0] in model else 'UNKNOWN' for r in outputs}})
regressions=[]
for label,target,start,initial,at,kind in (
 ('761 existing hook load/call',0x984930,0x97d778,None,'0097D780','live-r0-at-call'),
 ('761 supplied hook publication',0x984930,0x9a5e64,{ARM_REG_R0:0x984930},'009A5E6C','possible-stored-pointer-value'),
 ('760 initialized context return',0x108d8d0,0x99f164,None,'0099F170','possible-returned-pointer'),
):
 namespace['targets']=(target,);namespace['events']={}
 with contextlib.redirect_stdout(io.StringIO()):namespace['execute'](n.read(start,768),0,start,False,initial)
 events=sorted(namespace['events'].values(),key=lambda e:(e['address'],e['kind']))
 assert any(e['address']==at and e['kind']==kind for e in events)
 regressions.append({'label':label,'target':f'{target:08X}','seed':f'{start:08X}','events':events})
result={'engine_sha256':hashlib.sha256(raw).hexdigest(),'model_sha256':hashlib.sha256(modelpath.read_bytes()).hexdigest(),'baseline_sha256':m.BASELINE_SHA,'unicorn_version':__version__,'GOT_words':{f'{got+4*j:08X}':f'{n.word(got+4*j):08X}' for j in range(4)},'boundary':'One shipped ARM instruction per case; explicit base fixtures and raw mapped ELF GOT. Native values checked independently by Unicorn. No caller admission, full alias completeness, key initialization or atomic snapshot claim. Mutable key loads emit navigation reads but never constant values.','cases':cases,'existing_primary_pointer_regressions':regressions}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
print(len(cases),'native paired/block instruction controls and',len(regressions),'existing primary pointer regressions passed')
