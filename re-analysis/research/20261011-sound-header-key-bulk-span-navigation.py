"""Original key block: bulk-call byte spans; navigation, never absence proof.
No memory-helper body is executed. Mutable values remain unknown.
"""
from pathlib import Path
import importlib.util,hashlib,json,struct,sys
import lief,capstone
from capstone.arm import *

engine=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=engine.read_bytes();elf=lief.parse(str(engine))
path=Path(__file__).with_name('20261011-sound-paired-block-constant-model.py')
spec=importlib.util.spec_from_file_location('paired',path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
assert hashlib.sha256(path.read_bytes()).hexdigest()=='ca0f5ef8e468c6263e70c338a2545193991c4a15cbde3f7da0b601eb22219882'
names={'memcpy','memmove','memset','bzero',*[f'__aeabi_mem{x}{suffix}' for x in ('cpy','move','set','clr') for suffix in ('','4','8')]}
slots={r.address:r.symbol.name for r in elf.relocations if r.has_symbol and r.symbol.name in names and 'JUMP_SLOT' in str(r.type)}
plt=elf.get_section('.plt');b=bytes(plt.content);stubs={}
def imm(w):
 rot=((w>>8)&15)*2;v=w&255
 return ((v>>rot)|(v<<(32-rot)))&0xffffffff if rot else v
for j in range(0,len(b)-11,4):
 a=plt.virtual_address+j;w0,w1,w2=struct.unpack_from('<3I',b,j)
 if w0&0xfffff000!=0xe28fc000 or w1&0xfffff000!=0xe28cc000 or w2&0xfffff000!=0xe5bcf000:continue
 got=(a+8+imm(w0)+imm(w1)+(w2&0xfff))&0xffffffff
 if got in slots:stubs[a]=slots[got]
assert len(stubs)==len(slots)==14
def count_register(name):return ARM_REG_R1 if name=='bzero' or name.startswith(('__aeabi_memset','__aeabi_memclr')) else ARM_REG_R2
def overlap(dest,count):
 # Unsigned count; retain wrap as a candidate, never simulate an invalid write.
 if not count:return False
 end=dest+count
 return dest<0x108d9b0 and end>0x108d9a0 or end>0x100000000 and end-0x100000000>0x108d9a0
source=m.build_source(tuple(range(0x108d9a0,0x108d9b0)),controls='--controls' in sys.argv)
marker="  elif mn in ('bl','blx'):\n   for reg in (ARM_REG_R0,ARM_REG_R1,ARM_REG_R2,ARM_REG_R3):"
assert source.count(marker)==1
source=source.replace(marker,"""  elif mn in ('bl','blx'):
   callee=val(ops[0],ins) if ops else None
   name=bulk_stubs.get(callee & ~1) if callee is not None else None
   if name:
    dest=env.get(ARM_REG_R0);count=env.get(bulk_count_register(name))
    if dest is not None and count is not None and bulk_overlap(dest,count):
     bulk_events.append({'call':f'{ins.address:08X}','mode':'Thumb' if thumb else 'ARM','stub':f'{callee:08X}','primitive':name,'destination':f'{dest:08X}','count':count,'unsigned_end':dest+count,'wrap':dest+count>0x100000000,'seed':f'{a:08X}'})
   for reg in (ARM_REG_R0,ARM_REG_R1,ARM_REG_R2,ARM_REG_R3):""")
events=[];ns={'__file__':str(path.resolve()),'bulk_stubs':stubs,'bulk_count_register':count_register,'bulk_overlap':overlap,'bulk_events':events}
print('BULK STUBS',json.dumps({f'{a:08X}':n for a,n in stubs.items()},sort_keys=True))
exec(compile(source,str(Path(__file__)), 'exec'),ns)
if '--controls' in sys.argv:
 sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
 from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM
 from unicorn.arm_const import UC_ARM_REG_PC,UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2
 loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
 # Independent raw ARM BL decoder locates a real call to each primitive stub.
 calls={}
 for s in elf.sections:
  if not s.has(lief.ELF.Section.FLAGS.EXECINSTR):continue
  data=bytes(s.content)
  for j in range(0,len(data)-3,4):
   w=struct.unpack_from('<I',data,j)[0]
   if w&0xff000000!=0xeb000000:continue
   d=w&0xffffff;d=d-0x1000000 if d&0x800000 else d
   a=s.virtual_address+j;t=(a+8+4*d)&0xffffffff
   if t in stubs:calls.setdefault(t,a)
 controls=[]
 for stub in sorted(stubs):
  if stub not in calls:continue
  a=calls[stub];name=stubs[stub]
  for dest,count,expected in ((0x108d998,24,True),(0x108d998,8,False),(0x108d9a0,0,False)):
   u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
   for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
   u.reg_write(UC_ARM_REG_R0,dest)
   r=UC_ARM_REG_R1 if count_register(name)==ARM_REG_R1 else UC_ARM_REG_R2
   u.reg_write(r,count);u.emu_start(a,stub,count=1)
   assert u.reg_read(UC_ARM_REG_PC)==stub and u.reg_read(UC_ARM_REG_R0)==dest and u.reg_read(r)==count
   events.clear();env={ARM_REG_R0:dest,count_register(name):count};ns['execute'](ns['n'].read(a,4),0,a,False,env)
   assert bool(events)==expected
   controls.append({'actual_BL':f'{a:08X}','stub':f'{stub:08X}','name':name,'fixture_destination':f'{dest:08X}','fixture_count':count,'overlap':expected,'stopped_before_primitive':True})
 assert controls and any(c['name']=='memcpy' for c in controls) and any(c['name'].startswith('__aeabi_memset') for c in controls)
 result={'engine_sha256':hashlib.sha256(raw).hexdigest(),'boundary':'Real ARM BL instructions, explicit argument fixtures; no actual caller admission or helper writes certified. Length R2 for memcpy/memmove including AEABI; R1 for AEABI memset/memclr and bzero.','stubs':{f'{a:08X}':n for a,n in stubs.items()},'no_ARM_BL_control_found':[stubs[a] for a in stubs if a not in calls],'controls':controls}
 Path(__file__).with_name('20261011-sound-header-key-bulk-span-controls.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
 print('NATIVE CALL-BOUNDARY CONTROLS',len(controls),'PASSED')
else:
 result={'engine_sha256':hashlib.sha256(raw).hexdigest(),'paired_model_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'navigation_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'bulk_events':events,'boundary':'Finite original key seed windows only; unknown arguments, unsupported aliases and capped windows remain unknown. No native helper bodies or mutable runtime constants.'}
 Path(__file__).with_name('20261011-sound-header-key-bulk-span-summary.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
 print('BULK SPAN EVENTS',len(events))
