"""Execute BKHD decisions with shipped bytes and explicit reader ABI fixtures.
No storage-body emulation or runtime key/binding/admission assertion.
"""
from pathlib import Path
import sys, json, struct, hashlib, zipfile, lief
sys.path.insert(0, str(Path('.scratch/q14818deps').resolve()))
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, __version__
from unicorn.arm_const import *
engine=Path('resources/lib/armeabi-v7a/libcozmoEngine.so')
raw=engine.read_bytes(); elf=lief.parse(str(engine))
loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
archive=Path('../cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip')
witness=json.loads(Path('re-analysis/research/20261010-sound-bank-platform-input-witness.json').read_text())
expected={Path(b['path']).name:b['sha256'] for b in witness['banks']}
cases=[]
with zipfile.ZipFile(archive) as z:
 for name in sorted(n for n in z.namelist() if n.endswith('.bnk')):
  bank=z.read(name); assert hashlib.sha256(bank).hexdigest()==expected[Path(name).name]
  assert bank[:4]==b'BKHD'
  chunk=struct.unpack_from('<I',bank,4)[0]; payload=bank[8:28]
  assert struct.unpack_from('<I',payload)[0]==120 and chunk>=20
  for keys in ((0,0,0,0),(0,0xffffffff,0x12345678,0xffffffff),(1,0x89abcdef,0x76543210,0x12345678),(0x78,1,2,0x00010000),(0xffffffff,0,0,0),(0x80000000,0,0,1),(0x10000,0,0,0)):
   u=Uc(UC_ARCH_ARM,UC_MODE_ARM)
   end=max(s.virtual_address+s.virtual_size for s in loads)
   u.mem_map(0,(end+4095)&~4095)
   for s in loads:u.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
   u.mem_map(0x20000000,0x10000); u.mem_map(0x30000000,0x10000); u.mem_map(0x40000000,4096)
   manager=0x20001000; header=0x20002000; initial_sp=0x30008000
   u.mem_write(header,b'\xa5'*20); u.mem_write(manager+0x64,b'\xa5')
   u.mem_write(0x108d9a0,struct.pack('<4I',*keys))
   for reg,val in ((UC_ARM_REG_R0,manager),(UC_ARM_REG_R1,header),(UC_ARM_REG_SP,initial_sp),(UC_ARM_REG_LR,0x40000000)):u.reg_write(reg,val)
   calls=[]; reads=[]
   def hook(u,a,size,user):
    if a==0x9bbf64:
     r0=u.reg_read(UC_ARM_REG_R0); dest=u.reg_read(UC_ARM_REG_R1); length=u.reg_read(UC_ARM_REG_R2)
     phase=len(reads); assert r0==manager+4 and phase<2
     assert length==(8 if phase==0 else 20)
     assert dest==(initial_sp-40+8 if phase==0 else header)
     data=bank[:8] if phase==0 else payload
     u.mem_write(dest,data); reads.append(length); calls.append({'interface':'read','length':length,'return':1})
     u.reg_write(UC_ARM_REG_R0,1);u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
    elif a==0x9bbf9c:
     assert reads==[8,20] and u.reg_read(UC_ARM_REG_R0)==manager+4
     amount=u.reg_read(UC_ARM_REG_R1); count=u.reg_read(UC_ARM_REG_R2)
     assert amount==chunk-20 and count==initial_sp-40+4
     assert len(bank)>=8+chunk
     u.mem_write(count,struct.pack('<I',amount));calls.append({'interface':'skip','requested':amount,'actual_fixture':amount,'return':1,'header_unmodified_by_fixture':True})
     u.reg_write(UC_ARM_REG_R0,1);u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
   u.hook_add(UC_HOOK_CODE,hook);u.emu_start(0x9b21f4,0x40000000,count=1000)
   assert u.reg_read(UC_ARM_REG_PC)==0x40000000 and u.reg_read(UC_ARM_REG_SP)==initial_sp
   out=bytes(u.mem_read(header,20)); result=u.reg_read(UC_ARM_REG_R0); flag=bytes(u.mem_read(manager+0x64,1))[0]
   exp=bytearray(payload)
   if keys[0]:
    for i,k in enumerate(keys):struct.pack_into('<I',exp,4*i,struct.unpack_from('<I',payload,4*i)[0]^k)
   assert out==exp and result==(1 if keys[0]==0 else 0x40)
   assert flag==int(struct.unpack_from('<H',out,12)[0]!=0)
   cases.append({'bank':name,'bank_sha256':hashlib.sha256(bank).hexdigest(),'chunk_size':chunk,'keys_hex':[f'{k:08X}' for k in keys],'header_before_hex':payload.hex(),'header_after_hex':out.hex(),'return':result,'manager64':flag,'interface_fixtures':calls})
result={'boundary':'Actual 9B21F4 instructions; six shipped headers, controlled key words. Reader and skip are ABI fixtures returning exact bytes/count and preserving header during skip. No runtime key, buffer immutability, encrypted-input admission, file-loader or all-caller proof. K3 exit flag already paid; this is a reference witness, not a new obligation.','engine_sha256':hashlib.sha256(raw).hexdigest(),'archive_sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'unicorn_version':__version__,'cases':cases}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
print('Native header decision cases:',len(cases),'all passed; explicit ABI fixtures, no admission proof.')
