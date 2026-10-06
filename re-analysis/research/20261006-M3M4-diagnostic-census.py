"""Read-only literal/string census for the Q1 cube diagnostic ranges."""
from pathlib import Path
import capstone,lief,struct
from capstone.arm import *
e=lief.parse('resources/lib/armeabi-v7a/libcozmoEngine.so')
def read(a,n):
    return bytes(e.get_content_from_virtual_address(a,n))
def word(a):return struct.unpack('<I',read(a,4))[0]
def string(a):
    try:
        b=read(a,600).split(b'\0')[0]
        if len(b)>3 and all(32<=x<127 for x in b):return b.decode()
    except Exception:pass
m=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB);m.detail=True
for start,end in [(0x514a70,0x514c8e),(0x5179c0,0x517b0a),(0x517bbc,0x517d1e),(0x61a7ec,0x61a9d2),(0x61aa8c,0x61ab9a)]:
    pending={}
    print(f'RANGE {start:08X}..{end:08X}')
    for i in m.disasm(read(start,end-start),start):
        ops=i.operands
        if i.mnemonic=='adr' and len(ops)==2:
            addr=((i.address+4)&~3)+ops[1].imm
            s=string(addr)
            if s:print(f'{i.address:08X} {i.mnemonic} {i.op_str} -> {addr:08X} {s!r}')
        if i.mnemonic.startswith('ldr') and len(ops)==2 and ops[1].type==ARM_OP_MEM and ops[1].mem.base==ARM_REG_PC:
            addr=((i.address+4)&~3)+ops[1].mem.disp
            pending[ops[0].reg]=word(addr)
        if i.mnemonic=='add' and len(ops)==2 and ops[1].type==ARM_OP_REG and ops[1].reg==ARM_REG_PC and ops[0].reg in pending:
            addr=(pending.pop(ops[0].reg)+i.address+4)&0xffffffff
            s=string(addr)
            if s:print(f'{i.address:08X} {i.mnemonic} {i.op_str} -> {addr:08X} {s!r}')
        if i.mnemonic in ['bl','blx']:
            print(f'{i.address:08X} {i.mnemonic} {i.op_str}')
