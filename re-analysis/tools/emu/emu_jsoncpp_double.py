"""Shipped ARMv7 libc++ converter oracle (Unicorn 2.1.4, lief).

Run from any directory: python emu_jsoncpp_double.py "0.1" "1e309"
Or feed JSONL objects with an input string. Returns raw converter bits, errno,
UTF-8 byte end offset, FPSCR, plus the normalized num_get wrapper bits/state.
The raw entry is 0x7E570; wrapper is 0x5EB98. Neither executes the Reader lexer
or num_get locale normalization. Controlled ASCII tables, successful allocation,
serial mutexes and FPSCR are explicit fixture boundaries, not phone defaults.
Unknown imports and instruction/heap exhaustion abort, never fabricate a value.
Empty CLI strings work; no positional strings selects JSONL stdin.
"""
import sys, struct, lief
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE
from unicorn.arm_const import *
from pathlib import Path
import json, argparse, hashlib
PATH=str(Path(__file__).resolve().parents[3]/'resources/lib/armeabi-v7a/libc++_shared.so')
elf=lief.parse(PATH);raw=open(PATH,'rb').read()
EXPECTED_SHA256='8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a'
if hashlib.sha256(raw).hexdigest()!=EXPECTED_SHA256:raise RuntimeError('unsupported libc++ artifact: address map must be rechecked')
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
    a=heap;heap+=(max(n,1)+15)&~15
    if heap>HEAP+0x400000:raise MemoryError('fixture heap exhausted; not a simulated native allocation failure')
    return a
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
    n=hooks.get(a)
    if n is None:return
    v=0
    if n=='__errno':v=BASE+0x90000
    elif n in ['malloc','calloc','_Znwj','_Znaj']:
        count=reg(0)*(reg(1) if n=='calloc' else 1)
        v=alloc(count)
        if n=='calloc':uc.mem_write(v,bytes(count))
    elif n in ['memcpy','memmove','__aeabi_memcpy','__aeabi_memcpy4','__aeabi_memcpy8']:
        uc.mem_write(reg(0),bytes(uc.mem_read(reg(1),reg(2))));v=reg(0)
    elif n=='strncasecmp':
        def lower(x):return x+32 if 65<=x<=90 else x
        a0,a1,count=reg(0),reg(1),reg(2)
        for i in range(count):
            x,y=uc.mem_read(a0+i,1)[0],uc.mem_read(a1+i,1)[0]
            v=lower(x)-lower(y)
            if v or not x or not y:break
        v &= 0xFFFFFFFF
    elif n.startswith('__aeabi_memclr'):
        uc.mem_write(reg(0),bytes(reg(1)))
    elif n in ['memset','__aeabi_memset','__aeabi_memset4','__aeabi_memset8']:
        length,value=(reg(2),reg(1)) if n=='memset' else (reg(1),reg(2))
        uc.mem_write(reg(0),bytes([value&255])*length);v=reg(0)
    elif n in ['free','_ZdlPv','_ZdaPv','pthread_mutex_lock','pthread_mutex_unlock','unused_C_locale_handle']:pass
    else:raise RuntimeError(f'unmodeled import {n} at {a:x}, LR={uc.reg_read(UC_ARM_REG_LR):x}')
    uc.reg_write(UC_ARM_REG_R0,v);uc.reg_write(UC_ARM_REG_PC,uc.reg_read(UC_ARM_REG_LR))
for hook_address in [RET,*hooks]:
    uc.hook_add(UC_HOOK_CODE,execute,begin=hook_address,end=hook_address)

# Restore module globals before each call: decimal bigint freelists/cache must not
# point into a heap that has been reset. Both calls independently start identical.
initial_module = bytes(uc.mem_read(0,0xB0000))
def call(entry, data, fpscr=0, initial_errno=77):
    global heap
    if len(data)>=0x70000: raise ValueError("input too long")
    uc.mem_write(0,initial_module); heap=HEAP
    uc.mem_write(BASE,data+b"\0");w(BASE+0x71000,0);w(BASE+0x72000,0);w(BASE+0x90000,initial_errno)
    for n in range(UC_ARM_REG_R0,UC_ARM_REG_R12+1): uc.reg_write(n,0)
    for n in range(UC_ARM_REG_D0,UC_ARM_REG_D31+1): uc.reg_write(n,0)
    args=[BASE,BASE+0x71000,0] if entry==0x7E571 else [BASE,BASE+len(data),BASE+0x72000]
    for n,v in zip([UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2],args):uc.reg_write(n,v)
    for n,v in [(UC_ARM_REG_SP,STACK+0xF0000),(UC_ARM_REG_LR,RET),(UC_ARM_REG_FPSCR,fpscr)]:uc.reg_write(n,v)
    uc.emu_start(entry,RET,count=2000000)
    if uc.reg_read(UC_ARM_REG_PC)!=RET:raise RuntimeError("instruction budget exhausted")
    result={"bits":f"{(reg(1)<<32)|reg(0):016X}","errno":r(BASE+0x90000),"fpscr_out":f"{uc.reg_read(UC_ARM_REG_FPSCR):08X}"}
    if entry==0x7E571:result["end_offset"]=r(BASE+0x71000)-BASE
    else:result["state"]=r(BASE+0x72000)
    return result

def convert(text,fpscr=0):
    data=text.encode("utf-8")
    return {"input":text,"fpscr_in":f"{fpscr:08X}","raw":call(0x7E571,data,fpscr),"num_get_normalized":call(0x5EB99,data,fpscr)}

if __name__=="__main__":
    parser=argparse.ArgumentParser(description="Shipped libc++ converter and normalized num_get wrapper; JSONL stdin/stdout")
    parser.add_argument("strings",nargs="*");parser.add_argument("--fpscr",type=lambda x:int(x,0),default=0)
    args=parser.parse_args()
    for text in args.strings or (json.loads(line)["input"] for line in sys.stdin if line.strip()):
        try:print(json.dumps(convert(text,args.fpscr),ensure_ascii=True),flush=True)
        except Exception as error:raise RuntimeError(f"oracle failed for {text!r}") from error
