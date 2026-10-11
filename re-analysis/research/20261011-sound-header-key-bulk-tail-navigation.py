"""Original keys, B/BX bulk tails only;883 BL/BLX stays paid. No helper bodies."""
from pathlib import Path
import sys,json,hashlib,struct
original=Path(__file__).with_name('20261011-sound-header-key-bulk-span-navigation.py')
text=original.read_text(encoding='utf-8')
# Reuse interface/ABI and controlled constant-transfer model, without launching883.
prefix=text.split("events=[];ns=")[0]
ns={'__file__':str(original.resolve())};exec(compile(prefix,str(original),'exec'),ns)
source=ns['source'];marker="  elif mn in ('bl','blx'):\n   callee="
assert source.count(marker)==1
source=source.replace(marker,"  elif mn in ('b','bx'):\n   callee=")
events=[];ns.update(bulk_stubs=ns['stubs'],bulk_count_register=ns['count_register'],bulk_overlap=ns['overlap'],bulk_events=events,__file__=str(ns['path'].resolve()))
exec(compile(source,str(Path(__file__)), 'exec'),ns)
if '--controls' in sys.argv:
 sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
 from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM
 from unicorn.arm_const import UC_ARM_REG_PC,UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2
 from capstone.arm import ARM_REG_R0,ARM_REG_R1
 elf=ns['elf'];raw=ns['raw'];stubs=ns['stubs'];calls={}
 for s in elf.sections:
  if not s.has(ns['lief'].ELF.Section.FLAGS.EXECINSTR):continue
  data=bytes(s.content)
  for j in range(0,len(data)-3,4):
   w=struct.unpack_from('<I',data,j)[0]
   if w&0xff000000!=0xea000000:continue
   d=w&0xffffff;d=d-0x1000000 if d&0x800000 else d
   a=s.virtual_address+j;t=(a+8+4*d)&0xffffffff
   if t in stubs:calls.setdefault(t,a)
 loads=[s for s in elf.segments if s.type==ns['lief'].ELF.Segment.TYPE.LOAD];controls=[]
 for stub,a in sorted(calls.items()):
  name=stubs[stub]
  for dest,count,expected in ((0x108d998,24,True),(0x108d998,8,False),(0x108d9a0,0,False)):
   u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
   for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
   reg=ns['count_register'](name);ur=UC_ARM_REG_R1 if reg==ARM_REG_R1 else UC_ARM_REG_R2
   u.reg_write(UC_ARM_REG_R0,dest);u.reg_write(ur,count);u.emu_start(a,stub,count=1)
   assert u.reg_read(UC_ARM_REG_PC)==stub and u.reg_read(UC_ARM_REG_R0)==dest and u.reg_read(ur)==count
   events.clear();ns['execute'](ns['n'].read(a,4),0,a,False,{ARM_REG_R0:dest,reg:count});assert bool(events)==expected
   controls.append(dict(actual_B=f'{a:08X}',stub=f'{stub:08X}',name=name,destination=f'{dest:08X}',count=count,overlap=expected,stopped_before_primitive=True))
 assert controls
 result={'engine_sha256':hashlib.sha256(raw).hexdigest(),'controls':controls,'boundary':'Actual unconditional ARM B instructions with explicit argument fixtures; no helper execution, actual caller input or performed write certification. BX/computed targets remain navigation only.'}
 Path(__file__).with_name('20261011-sound-header-key-bulk-tail-controls.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
 print('NATIVE BOUNDARY CONTROLS',len(controls),'PASSED')
else:
 result={'engine_sha256':hashlib.sha256(ns['raw']).hexdigest(),'bulk_events':events,'navigation_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'reused_span_sha256':hashlib.sha256(original.read_bytes()).hexdigest(),'boundary':'B/BX bulk-tail spans only, original key seeds; unknown values/aliases and capped paths remain unknown. BL/BLX883 not new observations.'}
 Path(__file__).with_name('20261011-sound-header-key-bulk-tail-summary.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
 print('BULK TAIL EVENTS',len(events))
