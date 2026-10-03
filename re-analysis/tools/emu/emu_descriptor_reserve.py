"""Run the engine's own 0xA67B9C (the connection descriptor reserve) under Unicorn, for WwiseVoiceLinkerTests.

0xA67B9C(desc, inCh, outCh): size = ((outCh+3)>>2) * (inCh<<5); 1 at once when equal to [desc+4]; else free any old
block, zero the four words, allocate through 0xA7A894 (align 0x10) and store {data, size, data, data+size/2}, return 1;
an allocation that returns null leaves the four words zero and returns 2 (M6-wwise-bank.md C24.4).
The allocator 0xA7A894 and the free 0xA7A914 are stood in for (the pool is not modelled); this prints the descriptor
words and the result for each case.

    python re-analysis/tools/emu/emu_descriptor_reserve.py
"""
import struct
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED
from unicorn.arm_const import *
from so import RAW, LOADS

ENTRY = 0xA67B9C
BASE = 0x02000000
DESC = BASE + 0x100
HEAP = BASE + 0x10000
STACK = 0x04000000
RET = 0x05000000


def run(prev, in_ch, out_ch, alloc_ok):
    """prev = (data, size, ptrA, ptrB) before the call."""
    uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
    end = max(s.virtual_address + s.virtual_size for s in LOADS)
    uc.mem_map(0, (end + 0xFFFF) & ~0xFFFF)
    for s in LOADS:
        uc.mem_write(s.virtual_address, RAW[s.file_offset:s.file_offset + s.physical_size])
    for b in (BASE, STACK, RET):
        uc.mem_map(b, 0x100000)
    uc.mem_write(DESC, struct.pack('<IIII', *prev))
    log = []

    def hook(u, addr, size, _):
        if addr == 0xA7A894:             # alloc(pool, size, align)
            log.append(('alloc', u.reg_read(UC_ARM_REG_R1), u.reg_read(UC_ARM_REG_R2)))
            u.reg_write(UC_ARM_REG_R0, HEAP if alloc_ok else 0)
            u.reg_write(UC_ARM_REG_PC, u.reg_read(UC_ARM_REG_LR))
        elif addr == 0xA7A914:           # free(pool, ptr)
            log.append(('free', u.reg_read(UC_ARM_REG_R1)))
            u.reg_write(UC_ARM_REG_PC, u.reg_read(UC_ARM_REG_LR))
    uc.hook_add(UC_HOOK_CODE, hook, begin=0xA7A894, end=0xA7A914)
    uc.hook_add(UC_HOOK_MEM_UNMAPPED, lambda u, a, addr, s, v, _: print(f'unmapped {addr:#x}') or False)
    uc.reg_write(UC_ARM_REG_SP, STACK + 0xF0000)
    uc.reg_write(UC_ARM_REG_LR, RET)
    uc.reg_write(UC_ARM_REG_R0, DESC)
    uc.reg_write(UC_ARM_REG_R1, in_ch)
    uc.reg_write(UC_ARM_REG_R2, out_ch)
    uc.emu_start(ENTRY, RET)
    words = struct.unpack('<IIII', bytes(uc.mem_read(DESC, 16)))
    rel = [(hex(w - HEAP) if i in (0, 2, 3) and w else hex(w)) for i, w in enumerate(words)]
    return uc.reg_read(UC_ARM_REG_R0), rel, log


def main():
    cases = [
        ('fresh descriptor, in 2 out 1: size ((1+3)>>2)*(2<<5) = 64', (0, 0, 0, 0), 2, 1, True),
        ('fresh descriptor, allocation fails', (0, 0, 0, 0), 2, 1, False),
        ('same size already: returns 1 with no allocation', (HEAP, 64, HEAP, HEAP + 32), 2, 1, False),
        ('different size, allocation fails: old block freed, words zeroed, returns 2', (HEAP, 64, HEAP, HEAP + 32), 1, 9, False),
        ('different size, allocation ok: reallocated to 96', (HEAP, 64, HEAP, HEAP + 32), 1, 9, True),
        ('size 0 equal to the zero descriptor: returns 1 at once', (0, 0, 0, 0), 0, 5, False),
    ]
    for label, prev, i, o, ok in cases:
        print(f'{label}\n    result={run(prev, i, o, ok)}')


if __name__ == '__main__':
    main()
