"""Research-only execution of shipped libc++ double conversion. No host decimal parsing.
Controlled ASCII C tables, FPSCR and allocator/errno/thread primitives; NOT production-locale proof.
"""
import sys, struct, lief
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE
from unicorn.arm_const import *
PATH='resources/lib/armeabi-v7a/libc++_shared.so'
elf=lief.parse(PATH);raw=open(PATH,'rb').read()
BASE=0x2000000;HEAP=0x3000000;STACK=0x4000000;RET=0x5000000;STUB=0x6000000
uc=Uc(UC_ARCH_ARM,UC_MODE_ARM)
uc.mem_map(0,0xB0000)
for s in elf.segments:
    if s.type==lief.ELF.Segment.TYPE.LOAD:uc.mem_write(s.virtual_address,raw[s.file_offset:s.file_offset+s.physical_size])
for a,n in [(BASE,0x100000),(HEAP,0x400000),(STACK,0x100000),(RET,0x1000),(STUB,0x100000)]:uc.mem_map(a,n)
uc.reg_write(UC_ARM_REG_C1_C0_2,0xF<<20);uc.reg_write(UC_ARM_REG_FPEXC,0x40000000)
def w(a,v):uc.mem_write(a,struct.pack('<I',v&0xFFFFFFFF))
def r(a):return int.from_bytes(uc.mem_read(a,4),'little')
def reg(n):return uc.reg_read([UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3][n])
heap=HEAP
def alloc(n):
    global heap
    a=heap;heap+=(max(n,1)+15)&~15;return a
hooks={};names={}
for i,rel in enumerate(elf.relocations):
    if not rel.has_symbol or not rel.symbol.name:continue
    s=rel.symbol
    if s.value:w(rel.address,s.value+(r(rel.address) if 'ABS32' in str(rel.type) else 0));continue
    if s.name not in names:names[s.name]=STUB+len(names)*0x100
    w(rel.address,names[s.name])
for n,a in names.items():
    if n=='__stack_chk_guard':w(a,0x12345678)
    elif n=='_ctype_':
        table=BASE+0x80000;w(a,table)
        uc.mem_write(table,bytes([0]+[8 if x in [9,10,11,12,13,32] else 0 for x in range(256)]))
    elif n=='_tolower_tab_':
        table=BASE+0x81000;w(a,table)
        uc.mem_write(table,b''.join(struct.pack('<H',x+32 if 65<=x<=90 else x) for x in [0]+list(range(256))))
    else:hooks[a]=n
# __cloc returns an unused opaque handle: converter 7E570 does not read r2.
hooks[0x473A8]='unused_C_locale_handle'
def execute(uc,a,size,data):
    if a==RET:uc.emu_stop();return
    if '--trace' in sys.argv and a in [0x7EC62,0x7EE54,0x7EE58,0x7EE86,0x7EE96,0x7EECA,0x7EF12,0x7EF42,0x7EF5A,0x7EFEC,0x7F026,0x7F0D8,0x7F20A]:
        sp=uc.reg_read(UC_ARM_REG_SP)
        print(f'TRACE pc={a:08X} candidate={r(sp+0x2c):08X}{r(sp+0x30):08X} '
              f'd0={uc.reg_read(UC_ARM_REG_D0):016X} d13={uc.reg_read(UC_ARM_REG_D13):016X} '
              f'd15={uc.reg_read(UC_ARM_REG_D15):016X} r0={reg(0):08X}')
        if a==0x7EE54:
            for q in [reg(0),reg(1)]:
                length=r(q+0x10)
                value=sum(r(q+0x14+i*4)<<(32*i) for i in range(length))
                print(f'BIGINT pointer={q:08X} limbs={length} magnitude={value}')
    n=hooks.get(a)
    if n is None:return
    v=0
    if n=='__errno':v=BASE+0x90000
    elif n in ['malloc','calloc','_Znwj','_Znaj']:
        v=alloc(reg(0)*(reg(1) if n=='calloc' else 1))
    elif n in ['memcpy','memmove','__aeabi_memcpy','__aeabi_memcpy4','__aeabi_memcpy8']:
        uc.mem_write(reg(0),bytes(uc.mem_read(reg(1),reg(2))));v=reg(0)
    elif n.startswith('__aeabi_memclr'):
        uc.mem_write(reg(0),bytes(reg(1)))
    elif n in ['memset','__aeabi_memset','__aeabi_memset4','__aeabi_memset8']:
        length,value=(reg(2),reg(1)) if n=='memset' else (reg(1),reg(2))
        uc.mem_write(reg(0),bytes([value&255])*length);v=reg(0)
    elif n in ['free','_ZdlPv','_ZdaPv','pthread_mutex_lock','pthread_mutex_unlock','unused_C_locale_handle']:pass
    else:raise RuntimeError(f'unmodeled import {n} at {a:x}, LR={uc.reg_read(UC_ARM_REG_LR):x}')
    uc.reg_write(UC_ARM_REG_R0,v);uc.reg_write(UC_ARM_REG_PC,uc.reg_read(UC_ARM_REG_LR))
uc.hook_add(UC_HOOK_CODE,execute)
cases=['1.5','0.0','-0.0','1.','1e','1e+','18446744073709551616','-9223372036854775809','1e309','-1e309','1e-400','-1e-400','1e-324','2e-324','3e-324','4e-324','2.2250738585072013e-308','1.7976931348623157e308','1.7976931348623159e308','5e-324','2.2250738585072014e-308','0.1','9007199254740993.0','1.00000000000000011102230246251565404236316680908203125']
if '--modes' in sys.argv: cases=['0.1','3e-324','2.2250738585072014e-308','1.7976931348623157e308','1.00000000000000011102230246251565404236316680908203125']
if '--trace' in sys.argv: cases=['1.7976931348623157e308']
mode_values=[0,0x400000,0x800000,0xc00000,0x1000000] if '--modes' in sys.argv else [0]
for mode_value,text in [(m,t) for m in mode_values for t in cases]:
    uc.mem_write(BASE,text.encode('ascii')+b'\0');w(BASE+0x1000,0);w(BASE+0x90000,77)
    for n,v in [(UC_ARM_REG_R0,BASE),(UC_ARM_REG_R1,BASE+len(text)),(UC_ARM_REG_R2,BASE+0x1000),(UC_ARM_REG_SP,STACK+0xF0000),(UC_ARM_REG_LR,RET),(UC_ARM_REG_FPSCR,mode_value)]:uc.reg_write(n,v)
    try:
        uc.emu_start(0x5EB99,RET,count=1000000)
        assert uc.reg_read(UC_ARM_REG_PC)==RET, 'instruction budget exhausted before return'
        bits=(reg(1)<<32)|reg(0)
        print(f'FPSCRin={mode_value:08X} {text!r}\tbits={bits:016X}\tfail={r(BASE+0x1000)}\terrno={r(BASE+0x90000)}')
    except Exception as e:print(f'{text!r}\tUNVERIFIABLE {e} PC={uc.reg_read(UC_ARM_REG_PC):x}');break
