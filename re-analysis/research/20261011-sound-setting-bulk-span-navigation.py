"""Original092/099/120/124/130 setting spans only; no new body family."""
from pathlib import Path
import json,hashlib,sys
original=Path(__file__).with_name('20261011-sound-header-key-bulk-span-navigation.py')
prefix=original.read_text(encoding='utf-8').split('events=[];ns=')[0]
ns={'__file__':str(original.resolve())};exec(compile(prefix,str(original),'exec'),ns)
intervals=((0x105243c,0x1052450),(0x108df90,0x108df99))
targets=tuple(a for lo,hi in intervals for a in range(lo,hi))
source=ns['source'];old='targets='+repr(tuple(range(0x108d9a0,0x108d9b0)));assert source.count(old)==1
source=source.replace(old,'targets='+repr(targets))
marker="  elif mn in ('bl','blx'):\n   callee=";assert source.count(marker)==1
source=source.replace(marker,"  elif mn in ('b','bx','bl','blx'):\n   callee=")
legacy="   for reg in (ARM_REG_R0,ARM_REG_R1,ARM_REG_R2,ARM_REG_R3):\n    if R(env.get(reg,0)):emit(ins,'live-'+ins.reg_name(reg)+'-at-call',env[reg])"
assert source.count(legacy)==1
source=source.replace(legacy,"   if mn in ('bl','blx'):\n    for reg in (ARM_REG_R0,ARM_REG_R1,ARM_REG_R2,ARM_REG_R3):\n     if R(env.get(reg,0)):emit(ins,'live-'+ins.reg_name(reg)+'-at-call',env[reg])")
def overlap(dest,count):
 if not count:return False
 end=dest+count
 return any(dest<hi and end>lo or end>0x100000000 and end-0x100000000>lo for lo,hi in intervals)
events=[];ns.update(bulk_stubs=ns['stubs'],bulk_count_register=ns['count_register'],bulk_overlap=overlap,bulk_events=events,__file__=str(ns['path'].resolve()))
print('EXPLICIT SETTING INTERVALS',intervals)
exec(compile(source,str(Path(__file__)), 'exec'),ns)
if '--controls' in sys.argv:
 sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
 from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM
 from unicorn.arm_const import UC_ARM_REG_PC,UC_ARM_REG_R0,UC_ARM_REG_R2
 from capstone.arm import ARM_REG_R0,ARM_REG_R2
 raw=ns['raw'];elf=ns['elf'];loads=[s for s in elf.segments if s.type==ns['lief'].ELF.Segment.TYPE.LOAD];controls=[]
 for a in (0x8fd678,0x9096f0):
  for lo,hi in intervals:
   for dest,count,expected in ((lo-8,hi-lo+8,True),(lo-8,8,False),(lo,0,False)):
    u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
    for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
    u.reg_write(UC_ARM_REG_R0,dest);u.reg_write(UC_ARM_REG_R2,count);u.emu_start(a,0x4d36dc,count=1)
    assert u.reg_read(UC_ARM_REG_PC)==0x4d36dc and u.reg_read(UC_ARM_REG_R0)==dest and u.reg_read(UC_ARM_REG_R2)==count
    events.clear();ns['execute'](ns['n'].read(a,4),0,a,False,{ARM_REG_R0:dest,ARM_REG_R2:count});assert bool(events)==expected
    controls.append(dict(instruction=f'{a:08X}',stub='004D36DC',interval=[f'{lo:08X}',f'{hi:08X}'],destination=f'{dest:08X}',count=count,overlap=expected,stopped_before_primitive=True))
 result={'engine_sha256':hashlib.sha256(raw).hexdigest(),'controls':controls,'boundary':'Actual memset BL/B instructions with explicit setting-address/count fixtures; no actual admitted destination or performed writes.883 certifies imported length-register distinctions; unknown/computed targets remain unknown.'}
 Path(__file__).with_name('20261011-sound-setting-bulk-span-controls.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
 print('NATIVE SETTING CONTROLS',len(controls),'PASSED')
else:
 result={'engine_sha256':hashlib.sha256(ns['raw']).hexdigest(),'intervals':intervals,'bulk_events':events,'navigation_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'reused_interface_sha256':hashlib.sha256(original.read_bytes()).hexdigest(),'boundary':'Only exact original renderer words and Android rate/frame/route byte; B/BX/BL/BLX primitive spans with known arguments. No mutable loads, all-alias absence, host policy change or helper body execution.'}
 Path(__file__).with_name('20261011-sound-setting-bulk-span-summary.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
 print('SETTING BULK EVENTS',len(events))
