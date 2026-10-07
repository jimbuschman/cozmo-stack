"""ARM-state packaged Mono disassembly with PC-relative literal annotations."""
import sys,struct,lief,capstone
b=lief.parse(sys.argv[1])
r=lambda a,n:bytes(b.get_content_from_virtual_address(a,n))
c=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM)
for arg in sys.argv[2:]:
 a,z=map(lambda s:int(s,16),arg.split(':'))
 print('FILE',sys.argv[1],hex(a),hex(z))
 for i in c.disasm(r(a,z-a),a):
  suffix=''
  if '[pc, #' in i.op_str:
   off=int(i.op_str.split('#')[-1].split(']')[0],0);loc=i.address+8+off
   suffix=f' ; literal[{loc:08X}]={struct.unpack("<I",r(loc,4))[0]:08X}'
  print(f'{i.address:08X}: {i.mnemonic:12} {i.op_str}{suffix}')
