"""Run the engine's own bus-line Init 0xA4F0EC under Unicorn, for WwiseVoiceLinkerTests.

0xA4F0EC(line, r1 = the line again, cfgA, cfgB, [sp] u16 frames, [sp+4..+0xC] ctx {bus, key2, byte}, [sp+0x10] device id u64) (M6-wwise-bank.md C24.3).
Its callees are stood in for where their bodies are unread (0x9C8108, 0x9C817C, 0xA19ECC, 0xA68A44, 0xA68B28, 0xA68B38, the pool
allocators 0xA7A894 / 0xA7A7F4 and memset); the bus's vt+8 and vt+0x98 are stubs. The script prints, for each case, the result and
the stores the C# must reproduce: [line+0x30], +0x44, +0x64, +0x6C, +0x28/+0x2C, +0x1A8 and the allocation sizes.

    python re-analysis/tools/emu/emu_line_init.py
"""
import struct
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED
from unicorn.arm_const import *
from so import RAW, LOADS

ENTRY = 0xA4F0EC
BASE = 0x02000000
LINE = BASE + 0x1000
BUS = BASE + 0x3000
VT = BASE + 0x3100
STUB_ADDREF = BASE + 0x3200
STUB_VT98 = BASE + 0x3210
HEAP = BASE + 0x20000
STACK = 0x04000000
RET = 0x05000000
STACK_TOP = STACK + 0xF0000


def run(self_bus, cfg_a, cfg_b, frames, key2, byte, dev_lo, dev_hi, vt98, buffer_ok=True, holder_ok=True, holder_gate=True):
    uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
    end = max(s.virtual_address + s.virtual_size for s in LOADS)
    uc.mem_map(0, (end + 0xFFFF) & ~0xFFFF)
    for s in LOADS:
        uc.mem_write(s.virtual_address, RAW[s.file_offset:s.file_offset + s.physical_size])
    for b in (BASE, STACK, RET):
        uc.mem_map(b, 0x100000)
    uc.reg_write(UC_ARM_REG_C1_C0_2, uc.reg_read(UC_ARM_REG_C1_C0_2) | (0xF << 20))
    uc.reg_write(UC_ARM_REG_FPEXC, 0x40000000)
    w32 = lambda a, v: uc.mem_write(a, struct.pack('<I', v & 0xFFFFFFFF))

    # the bus object: [bus+0] vtable, [bus+8] its id (0xA68A2C reads it)
    w32(BUS, VT)
    w32(BUS + 8, 0xCAFE0001)
    w32(VT + 8, STUB_ADDREF)
    w32(VT + 0x98, STUB_VT98)
    uc.mem_write(STUB_ADDREF, struct.pack('<I', 0xE12FFF1E))                       # bx lr
    uc.mem_write(STUB_VT98, struct.pack('<II', 0xE3A00000 | vt98, 0xE12FFF1E))     # mov r0,#vt98; bx lr

    # the caller's stack: [sp] frames, [sp+4] ctx.bus, [sp+8] ctx.key2, [sp+0xC] ctx.byte, [sp+0x10] dev lo, [sp+0x14] dev hi
    sp = STACK_TOP
    w32(sp + 0x00, frames)
    w32(sp + 0x04, BUS if self_bus else 0)
    w32(sp + 0x08, key2)
    w32(sp + 0x0C, byte)
    w32(sp + 0x10, dev_lo)
    w32(sp + 0x14, dev_hi)

    log = []

    def hook(u, addr, size, _):
        lr = u.reg_read(UC_ARM_REG_LR)
        if addr == 0x9C8108:
            log.append(('9C8108',)); u.reg_write(UC_ARM_REG_R0, 0); u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0x9C817C:
            log.append(('9C817C',)); u.reg_write(UC_ARM_REG_R0, 0); u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0xA19ECC:
            log.append(('A19ECC',)); u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0xA68A44:
            log.append(('A68A44',)); u.reg_write(UC_ARM_REG_R0, 0); u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0xA68B28:
            log.append(('A68B28',)); u.reg_write(UC_ARM_REG_R0, 0); u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0xA68B38:
            log.append(('A68B38',)); u.reg_write(UC_ARM_REG_R0, 1 if holder_gate else 0); u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0xA7A894:           # buffer allocation (pool, size, align)
            log.append(('alloc_buffer', u.reg_read(UC_ARM_REG_R1), u.reg_read(UC_ARM_REG_R2)))
            u.reg_write(UC_ARM_REG_R0, HEAP if buffer_ok else 0); u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0xA7A7F4:           # holder allocation (pool, size)
            log.append(('alloc_holder', u.reg_read(UC_ARM_REG_R1)))
            u.reg_write(UC_ARM_REG_R0, HEAP + 0x1000 if holder_ok else 0); u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0x4D36DC:           # memset
            log.append(('memset', u.reg_read(UC_ARM_REG_R2))); u.reg_write(UC_ARM_REG_PC, lr)
    for a in (0x9C8108, 0x9C817C, 0xA19ECC, 0xA68A44, 0xA68B28, 0xA68B38, 0xA7A894, 0xA7A7F4, 0x4D36DC):
        uc.hook_add(UC_HOOK_CODE, hook, begin=a, end=a)
    uc.hook_add(UC_HOOK_MEM_UNMAPPED, lambda u, a, addr, s, v, _: print(f'unmapped {addr:#x} pc {u.reg_read(UC_ARM_REG_PC):#x}') or False)

    uc.reg_write(UC_ARM_REG_SP, sp)
    uc.reg_write(UC_ARM_REG_LR, RET)
    uc.reg_write(UC_ARM_REG_R0, LINE)
    uc.reg_write(UC_ARM_REG_R1, LINE)                    # callers 0xA423AC, 0xA42470 pass r0 = r1 = the new line; the bus is ctx word 0
    uc.reg_write(UC_ARM_REG_R2, cfg_a)
    uc.reg_write(UC_ARM_REG_R3, cfg_b)
    uc.emu_start(ENTRY, RET)
    rd = lambda off: struct.unpack('<I', bytes(uc.mem_read(LINE + off, 4)))[0]
    return {
        'result': uc.reg_read(UC_ARM_REG_R0),
        'line+0x30': 'line' if rd(0x30) == LINE else hex(rd(0x30)),
        'line+0x44': hex(rd(0x44)),
        'line+0x64': hex(rd(0x64)),
        'line+0x6C u16': hex(rd(0x6C) & 0xFFFF),
        'line+0x28/2C': (hex(rd(0x28)), hex(rd(0x2C))),
        'line+0x48': hex(rd(0x48)),
        'line+0x1A8': 'null' if rd(0x1A8) == 0 else 'holder',
        'calls': log,
    }


def main():
    cases = [
        ('master line: cfgA = cfgB = 0x3102, frames 0x400, self non-null, vt+0x98 non-zero -> 1', True, 0x3102, 0x3102, 0x400, 7, 0, 2, 0, 1),
        ('child line: cfgA = 1, cfgB = 0x3102, frames 0x400 -> buffer 1*0x400*4 = 4096', True, 1, 0x3102, 0x400, 7, 0, 2, 0, 1),
        ('stereo 0x3102 buffer (cfgA & 0xFF) = 2: 2*0x400*4 = 8192', True, 0x3102, 0x3102, 0x400, 7, 0, 2, 0, 1),
        ('frames 3, cfgA 0x4101: buffer 1*3*4 = 12', True, 0x4101, 0x4101, 3, 7, 0, 2, 0, 1),
        ('default context (self null): no vt calls, no 0xA19ECC', False, 0x3102, 0x3102, 0x400, 0xFFFFFFFF, 1, 2, 0, 1),
        ('vt+0x98(3) returns 0 -> result 2, before the buffer allocation', True, 0x3102, 0x3102, 0x400, 7, 0, 2, 0, 0),
        ('buffer allocation fails -> 0x34', True, 0x3102, 0x3102, 0x400, 7, 0, 2, 0, 1, False, True),
        ('holder allocation fails (holder step reached) -> 0x34, [line+0x1A8] = 0', True, 0x3102, 0x3102, 0x400, 7, 0, 2, 0, 1, True, False),
        ('holder step gated off (0xA68B38 returns 0) with a failing holder allocation -> 1', True, 0x3102, 0x3102, 0x400, 7, 0, 2, 0, 1, True, False, False),
        ('device lo 4: the holder step is skipped ([line+0x28] == 4) -> 1', True, 0x3102, 0x3102, 0x400, 7, 0, 4, 0, 1, True, False),
        ('64-bit device id (lo 3, hi 5)', True, 0x3102, 0x3102, 0x400, 7, 0, 3, 5, 1),
    ]
    for label, self_bus, a, b, frames, key2, byte, lo, hi, vt98, *rest in cases:
        r = run(self_bus, a, b, frames, key2, byte, lo, hi, vt98, *rest)
        print(f'{label}\n    {r}')


if __name__ == '__main__':
    main()
