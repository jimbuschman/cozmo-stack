"""Key-base R2 carrier boundaries only; no repeated read/decrypt/I-O body.
Six actual prefix shapes plus explicit edge/caller fixtures; not global key-write absence.
"""
from pathlib import Path
import sys,struct,json,hashlib,zipfile,lief
sys.path.insert(0,str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,__version__
from unicorn.arm_const import *
engine=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=engine.read_bytes();e=lief.parse(str(engine));loads=[s for s in e.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
archive=Path('../cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip')
witness=json.loads(Path('re-analysis/research/20261010-sound-bank-platform-input-witness.json').read_text(encoding='utf-8'));expected={Path(b['path']).name:b['sha256'] for b in witness['banks']}
frame=0x30004000;header=frame+0x74;manager=0x20001000;trap=0x40000000;key=0x108d9a0
def machine():
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM);end=max(s.virtual_address+s.virtual_size for s in loads);u.mem_map(0,(end+4095)&~4095)
 for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
 for base,size in ((0x20000000,0x10000),(0x30000000,0x10000),(trap,4096)):u.mem_map(base,size)
 u.reg_write(UC_ARM_REG_SP,frame);return u
def zero_case(payload,chunk):
 u=machine();u.mem_write(header,payload);u.mem_write(frame+0xc,struct.pack('<I',chunk));u.mem_write(frame+0x10,struct.pack('<6I',*range(0x100,0x105),trap))
 for reg,val in ((UC_ARM_REG_R2,key),(UC_ARM_REG_R3,0),(UC_ARM_REG_R4,manager),(UC_ARM_REG_R5,header),(UC_ARM_REG_R6,manager+4)):u.reg_write(reg,val)
 end=trap if chunk==20 else 0x9bbf9c;u.emu_start(0x9b2274,end,count=100)
 assert u.reg_read(UC_ARM_REG_PC)==end
 if chunk==20:
  assert u.reg_read(UC_ARM_REG_R2)==key and u.reg_read(UC_ARM_REG_R0)==1
 else:
  assert u.reg_read(UC_ARM_REG_R2)==frame+4 and u.reg_read(UC_ARM_REG_R0)==manager+4 and u.reg_read(UC_ARM_REG_R1)==chunk-20
  assert bytes(u.mem_read(frame+4,4))==b'\x00'*4
 return {'declared_chunk_size':chunk,'fixture_K0_loaded_value':0,'R2_before_gate':f'{key:08X}','stop_PC':f'{end:08X}','R2_at_stop':f'{u.reg_read(UC_ARM_REG_R2):08X}','R0_at_stop':u.reg_read(UC_ARM_REG_R0),'R1_at_stop':u.reg_read(UC_ARM_REG_R1),'stop_before_skip_body':chunk!=20,'stack_header_fixture':f'{header:08X}'}
cases=[];first=None
with zipfile.ZipFile(archive) as z:
 for name in sorted(n for n in z.namelist() if n.endswith('.bnk')):
  b=z.read(name);h=hashlib.sha256(b).hexdigest();assert h==expected[Path(name).name] and b[:4]==b'BKHD'
  chunk=struct.unpack_from('<I',b,4)[0];payload=b[8:28];assert chunk in (24,32) and struct.unpack_from('<I',payload)[0]==120
  if first is None:first=payload
  c=zero_case(payload,chunk);c.update({'case':'actual shipped prefix, explicit zero-key loaded fixture','bank':name,'bank_sha256':h,'payload_hex':payload.hex()});cases.append(c)
assert len(cases)==6
c=zero_case(first,20);c['case']='explicit20-byte edge, not a shipped declared chunk size';cases.append(c)
for half in (0,0xffff):
 u=machine();u.mem_write(header+0xe,struct.pack('<H',half));u.reg_write(UC_ARM_REG_R5,header);u.reg_write(UC_ARM_REG_R2,key)
 u.emu_start(0x9b2294,0x9b2298,count=1);assert u.reg_read(UC_ARM_REG_R2)==half and half!=key
 cases.append({'case':'nonzero-key branch actual LDRH pointer kill, explicit header-half fixture','header_half_fixture':half,'R2_before':f'{key:08X}','R2_after':f'{half:08X}','instruction_address':'009B2294'})
# Actual success caller's first R2 definition, without repeating the header reader.
u=machine();u.mem_write(frame+0x10,struct.pack('<I',1));u.reg_write(UC_ARM_REG_R0,1);u.reg_write(UC_ARM_REG_R2,key)
u.emu_start(0x9b7770,0x9b778c,count=100);assert u.reg_read(UC_ARM_REG_PC)==0x9b778c and u.reg_read(UC_ARM_REG_R2)==0x4e49
cases.append({'case':'actual caller success branch, explicit returned-key R2 fixture','returnR0_fixture':1,'R2_before':f'{key:08X}','first_R2_definition':'009B7788 MOVW R2,4E49','R2_after':'00004E49','stopped_before_next_instruction':True})
# Explicit error-carrier boundary: retain the pointer live, never invent cleanup behavior.
u=machine();u.reg_write(UC_ARM_REG_R0,0x40);u.reg_write(UC_ARM_REG_R2,key);u.reg_write(UC_ARM_REG_R4,manager+4)
u.emu_start(0x9b7770,0x9bbba4,count=100);assert u.reg_read(UC_ARM_REG_PC)==0x9bbba4 and u.reg_read(UC_ARM_REG_R2)==key
cases.append({'case':'explicit error+key carrier edge; not proof of shipped admission','returnR0_fixture':0x40,'R2_at_cleanup_boundary':f'{key:08X}','stop_PC':'009BBBA4','cleanup_body_not_executed':True})
result={'engine_sha256':hashlib.sha256(raw).hexdigest(),'archive_sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'unicorn_version':__version__,'boundary':'Only key-base carrier control slices9B2274 onward/9B2294 and caller9B7770. Six actual hashed bank-prefix shapes, explicit K0/header/frame/caller-result fixtures. No read/decrypt/skip/cleanup implementations, runtime key values, buffer immutability, actual extra caller/admission, or all writer/foreign alias absence proof. Only the originally formed R2 address is tracked; coincidental encrypted scalar values are not address provenance.','cases':cases}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
print(len(cases),'native carrier cases passed; six shipped sizes24/32, explicit20-byte/error carrier limits retained.')
