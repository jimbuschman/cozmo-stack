"""Original021 prefix suppliers: primary template identities; no other track bodies."""
from pathlib import Path
import hashlib,json,struct,sys,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM
from unicorn.arm_const import UC_ARM_REG_R1,UC_ARM_REG_PC
p=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=p.read_bytes();e=lief.parse(str(p));loads=[s for s in e.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
def read(a,n):
 for s in loads:
  if s.virtual_address<=a and a+n<=s.virtual_address+s.physical_size:return raw[s.file_offset+a-s.virtual_address:s.file_offset+a-s.virtual_address+n]
 raise ValueError(hex(a))
def ror(v,n):return ((v>>n)|(v<<(32-n)))&0xffffffff if n else v
def imm(w):return ror(w&255,((w>>8)&15)*2)
rel={r.address:r for r in e.pltgot_relocations};rows=[]
for entry,formation,call,plt in ((0x58e391,0x58e4a2,0x58e4b6,0x4aeac4),(0x58eb01,0x58ec24,0x58ec38,0x4ae9f8),(0x58ee4d,0x58ef36,0x58ef4a,0x4aeb30)):
 symbols=[s.name for s in e.dynamic_symbols if s.value==entry and 'RemovePersistentLayer' in s.name];assert len(symbols)==1
 words=struct.unpack('<3I',read(plt,12));assert words[0]&0xfffff000==0xe28fc000 and words[1]&0xfffff000==0xe28cc000 and words[2]&0xfffff000==0xe5bcf000
 got=plt+8+imm(words[0])+imm(words[1])+(words[2]&0xfff);r=rel[got];name=r.symbol.name
 assert 'AddLayer' in name
 family=symbols[0].split('ITrackLayerManager',1)[1].split('21RemovePersistentLayer',1)[0]
 assert family in name
 definitions=[dict(value=f'{s.value:08X}',name=s.name) for s in e.dynamic_symbols if s.name==name and s.value]
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);u.mem_map(0,(max(s.virtual_address+s.virtual_size for s in loads)+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 u.emu_start(formation|1,formation+4,count=2);assert u.reg_read(UC_ARM_REG_PC)==formation+4 and u.reg_read(UC_ARM_REG_R1)==0xbf0000
 rows.append(dict(remove_entry=f'{entry:08X}',remove_dynamic_symbol=symbols[0],prefix_control=dict(start=f'{formation:08X} Thumb',stop=f'{formation+4:08X}',R1='00BF0000',instruction_count=2),add_call=f'{call:08X}',plt=f'{plt:08X}',plt_words=[f'{w:08X}' for w in words],got=f'{got:08X}',relocation_type=str(r.type),add_import_symbol=name,add_defined_symbols=definitions))
 result=None
j=json.loads(Path(__file__).with_name('20261011-sound-layer-remove-prefix-navigation.json').read_text());log=Path(__file__).with_name('20261011-sound-layer-remove-prefix-navigation.txt').read_text()
caps=[x for x in log.splitlines() if x.startswith('LIMIT local state cap')];assert len(caps)==5
result=dict(engine_sha256=hashlib.sha256(raw).hexdigest(),navigation_seed_count=len(j['seeds']),navigation_event_count=len(j['events']),cap_diagnostics=caps,attributions=rows,boundary='Primary dynamic symbols and actual caller PLT relocation bind BackpackLights, ProceduralFace and AudioSample template identities. Three two-instruction native prefix controls only; no complete removal or AddLayer body, real manager/tag/control input, or all-alias/capped-window exclusion.')
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode());print(json.dumps(result,indent=2))
