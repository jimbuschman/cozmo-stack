"""Run the engine's own device-table find-or-insert 0x9EA23C under Unicorn, for WwiseVoiceLinkerTests.

0x9EA23C(E, key) (M6-wwise-bank.md C24.5, C25.6): the table is base [E+0x58], count [E+0x5C], capacity [E+0x60] of 8-byte {key, value}
entries. It scans for the key; a miss grows the capacity by exactly 1 when the count has reached it and appends {key, 0}; the value is
zeroed and rebuilt through 0xA22A3C(key, [E+0x20], [E+0x2C], &value); a non-zero value returns 1, else the first entry holding the key
is removed and 2 is returned. The build body 0xA22A3C, the allocator 0xA7A7F4, the free 0xA7A988 and memmove are stood in for. Each
case prints the result, the final count and capacity, the entries and the allocator/build calls.

    python re-analysis/tools/emu/emu_device_table.py
"""
import struct
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED
from unicorn.arm_const import *
from so import RAW, LOADS

ENTRY = 0x9EA23C
BASE = 0x02000000
E = BASE + 0x100
HEAP = BASE + 0x10000
STACK = 0x04000000
RET = 0x05000000


def run(entries, capacity, key, build_value, alloc_ok):
    """entries = [(key, value)] in the table, capacity = [E+0x60]; build_value = what 0xA22A3C stores (0 = zero)."""
    uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
    end = max(s.virtual_address + s.virtual_size for s in LOADS)
    uc.mem_map(0, (end + 0xFFFF) & ~0xFFFF)
    for s in LOADS:
        uc.mem_write(s.virtual_address, RAW[s.file_offset:s.file_offset + s.physical_size])
    for b in (BASE, STACK, RET):
        uc.mem_map(b, 0x100000)
    w32 = lambda a, v: uc.mem_write(a, struct.pack('<I', v & 0xFFFFFFFF))
    table = HEAP
    for i, (k, v) in enumerate(entries):
        uc.mem_write(table + 8 * i, struct.pack('<II', k, v))
    w32(E + 0x20, 0x20202020)
    w32(E + 0x2C, 0x2C2C2C2C)
    w32(E + 0x58, table if capacity else 0)
    w32(E + 0x5C, len(entries))
    w32(E + 0x60, capacity)
    next_heap = [HEAP + 0x1000]
    log = []

    def hook(u, addr, size, _):
        lr = u.reg_read(UC_ARM_REG_LR)
        if addr == 0xA22A3C:             # build(key, [E+0x20], [E+0x2C], &value)
            log.append(('build', hex(u.reg_read(UC_ARM_REG_R0)), hex(u.reg_read(UC_ARM_REG_R1)), hex(u.reg_read(UC_ARM_REG_R2))))
            w32(u.reg_read(UC_ARM_REG_R3), build_value)
            u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0xA7A7F4:           # alloc(pool, size)
            size_ = u.reg_read(UC_ARM_REG_R1)
            log.append(('alloc', size_))
            if alloc_ok:
                p = next_heap[0]; next_heap[0] += (size_ + 15) & ~15
                u.reg_write(UC_ARM_REG_R0, p)
            else:
                u.reg_write(UC_ARM_REG_R0, 0)
            u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0xA7A988:           # free(pool, ptr)
            log.append(('free',)); u.reg_write(UC_ARM_REG_PC, lr)
        elif addr == 0x4D667C:           # memmove(dst, src, n)
            d, s_, n = u.reg_read(UC_ARM_REG_R0), u.reg_read(UC_ARM_REG_R1), u.reg_read(UC_ARM_REG_R2)
            u.mem_write(d, bytes(u.mem_read(s_, n))); log.append(('memmove', n)); u.reg_write(UC_ARM_REG_PC, lr)
    for a in (0xA22A3C, 0xA7A7F4, 0xA7A988, 0x4D667C):
        uc.hook_add(UC_HOOK_CODE, hook, begin=a, end=a)
    uc.hook_add(UC_HOOK_MEM_UNMAPPED, lambda u, a, addr, s, v, _: print(f'unmapped {addr:#x} pc {u.reg_read(UC_ARM_REG_PC):#x}') or False)
    uc.reg_write(UC_ARM_REG_SP, STACK + 0xF0000)
    uc.reg_write(UC_ARM_REG_LR, RET)
    uc.reg_write(UC_ARM_REG_R0, E)
    uc.reg_write(UC_ARM_REG_R1, key)
    uc.emu_start(ENTRY, RET)
    rd = lambda a: struct.unpack('<I', bytes(uc.mem_read(a, 4)))[0]
    base, count, cap = rd(E + 0x58), rd(E + 0x5C), rd(E + 0x60)
    final = [(rd(base + 8 * i), rd(base + 8 * i + 4) != 0) for i in range(count)] if base else []
    return {'result': uc.reg_read(UC_ARM_REG_R0), 'count': count, 'capacity': cap, 'entries': final, 'calls': log}


def main():
    V = 0x7777
    cases = [
        ('empty table, miss, build ok: capacity 0 -> 1, count 1', [], 0, 3, V, True),
        ('empty table, miss, growth allocation fails: result 2, table unchanged, no build', [], 0, 3, V, False),
        ('empty table, miss, build returns 0: appended then removed, result 2, capacity stays 1', [], 0, 3, 0, True),
        ('{4} with capacity 1, miss 0: grows to 2, appends, build ok', [(4, V)], 1, 0, V, True),
        ('{4} with capacity 2 (room): no allocation', [(4, V)], 2, 0, V, True),
        ('{4} with capacity 1, miss, growth fails: result 2, {4} untouched, no build', [(4, V)], 1, 0, V, False),
        ('hit on key 4: value zeroed and rebuilt, no allocation, result 1', [(4, V)], 1, 4, V, True),
        ('hit on key 4 with a zero build: the existing entry is removed, result 2', [(4, V), (0, V)], 2, 4, 0, True),
        ('miss, zero build, other entries stay in order: {4, 0} + key 9', [(4, V), (0, V)], 3, 9, 0, True),
        ('hit on the second of {4, 0}: key 0 rebuilt in place', [(4, V), (0, V)], 2, 0, V, True),
        ('hit on the second of {4, 0} with a zero build: {4} remains', [(4, V), (0, V)], 2, 0, 0, True),
        ('hit on key 4 of {4, 0}, zero build: {0} remains (memmove)', [(4, V), (0, V)], 2, 4, 0, True),
    ]
    for label, entries, cap, key, val, ok in cases:
        print(f'{label}\n    {run(entries, cap, key, val, ok)}')


if __name__ == '__main__':
    main()
