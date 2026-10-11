"""Original021 exact Remove-prefix PC-literal suppliers; no generator family."""
from pathlib import Path
import importlib.util,hashlib,json,struct,sys
path=Path(__file__).with_name('20261011-sound-paired-block-constant-model.py');spec=importlib.util.spec_from_file_location('paired',path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
ns={'__file__':str(path.resolve())};exec(compile(m.build_source((0xbf0000,),controls=True),str(path),'exec'),ns)
n=ns['n'];target=0xbf0000;assert n.read(target,7)==b'Remove\x00'
# Independent actual known producer control: only two Thumb instructions.
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM
from unicorn.arm_const import UC_ARM_REG_R1,UC_ARM_REG_PC
u=Uc(UC_ARCH_ARM,UC_MODE_ARM);elf=ns['elf'];loads=[s for s in elf.segments if s.type==ns['lief'].ELF.Segment.TYPE.LOAD];u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
for s in loads:u.mem_write(s.virtual_address,n.raw[s.file_offset:s.file_offset+s.physical_size])
u.emu_start(0x58ef37,0x58ef3a,count=2);assert u.reg_read(UC_ARM_REG_PC)==0x58ef3a and u.reg_read(UC_ARM_REG_R1)==target
ns['execute'](n.read(0x58ef36,4),0,0x58ef36,True);assert any(e['address']=='0058EF38' and e['kind']=='formed-r1' and e['effective']=='00BF0000' for e in ns['events'].values())
ns['events'].clear();ns['seeds']=0;seeds=[]
for va,size,off in ns['exec_sections']:
 if va>=0xf00000 or va+size<=0x4a0000:continue
 data=n.read(va,size);start=max(0,0x4a0000-va)
 for thumb,step in ((False,4),(True,2)):
  for j in range((start+step-1)//step*step,size-4,step):
   a=va+j;loc=None
   if not thumb:
    w=struct.unpack_from('<I',data,j)[0]
    if w&0x0f7f0000==0x051f0000:loc=a+8+(w&0xfff)*(1 if w&(1<<23) else -1)
   else:
    h=struct.unpack_from('<H',data,j)[0]
    if h&0xf800==0x4800:loc=((a+4)&~3)+((h&255)<<2)
    elif h in (0xf8df,0xf85f):loc=((a+4)&~3)+(struct.unpack_from('<H',data,j+2)[0]&0xfff)*(1 if h==0xf8df else -1)
   if loc is None:continue
   try:value=n.word(loc)
   except ValueError:continue
   base=a+(4 if thumb else 8)
   if not any(abs(candidate-target)<=512 for candidate in (value,(base+value)&0xffffffff)) :continue
   seeds.append(dict(address=f'{a:08X}',mode='Thumb' if thumb else 'ARM',literal_address=f'{loc:08X}',literal_word=f'{value:08X}'))
   ns['execute'](data,j,a,thumb)
result=dict(engine_sha256=hashlib.sha256(n.raw).hexdigest(),model_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),target='00BF0000',literal_hex=n.read(target,7).hex(),native_control=dict(start='0058EF36 Thumb',stop='0058EF3A',R1='00BF0000',instructions=2),seeds=seeds,events=list(ns['events'].values()),limit='Exact original Remove-prefix PC-literal/composed-PC supplier navigation only. MOVW/MOVT, distant/computed/copied/GOT/string-content aliases and capped windows are not excluded. No callee bodies or actual manager/tag/control admission certified.')
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode());print(json.dumps(result,indent=2))
