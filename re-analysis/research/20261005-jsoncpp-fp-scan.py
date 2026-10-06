import lief,capstone,struct
from pathlib import Path
csT=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB)
csA=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM)
for path in Path('resources/lib/armeabi-v7a').glob('*.so'):
 elf=lief.parse(str(path)); raw=path.read_bytes(); found=[]
 for seg in elf.segments:
  if not (int(seg.flags)&1): continue
  b=raw[seg.file_offset:seg.file_offset+seg.physical_size]
  for off in range(0,len(b)-3,2):
   # VMSR FPSCR,Rt Thumb starts EEE1, ARM ends EEE1.
   if b[off:off+2]==b'\xe1\xee':
    ins=next(csT.disasm(b[off:off+4],seg.virtual_address+off),None)
    if ins and ins.mnemonic=='vmsr': found.append((ins.address,ins.mnemonic,ins.op_str,'Thumb'))
   if off%4==0 and b[off+2:off+4]==b'\xe1\xee':
    ins=next(csA.disasm(b[off:off+4],seg.virtual_address+off),None)
    if ins and ins.mnemonic=='vmsr': found.append((ins.address,ins.mnemonic,ins.op_str,'ARM'))
 print(path, 'VMSR candidates',found)
 if path.name=='libc++_shared.so':
  for address,n in [(0x9fd68,5),(0x9fd40,5)]:
   seg=next(s for s in elf.segments if s.virtual_address<=address<s.virtual_address+s.physical_size)
   o=seg.file_offset+address-seg.virtual_address
   print('table',hex(address),[f'{struct.unpack_from("<Q",raw,o+i*8)[0]:016X}' for i in range(n)])
