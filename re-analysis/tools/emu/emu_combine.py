"""Run the engine's own Vorbis window combine (0x00AB5A94) under Unicorn for every long/short combination.

Called by the window driver 0x00AB3520 as
    combine(bs0, bs1, prevFlag, curFlag, work[ch], overlap[ch], W0, W1, out, channels, start, end)
with W0/W1 the engine's window tables for bs0/bs1 (0x01054490.. by blocksize/2). Writes combine_cases.json:
each case's inputs and the engine's output, for the C# comparison.

    python re-analysis/tools/emu/emu_combine.py
"""
import json, struct, random
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_MEM_UNMAPPED
from unicorn.arm_const import *
from so import RAW, LOADS

COMBINE = 0x00AB5A94
WINDOWS = {128: 0x01054490, 256: 0x01054690, 512: 0x01054A90, 1024: 0x01055290, 2048: 0x01056290}  # by blocksize/2
WORK, OVER, OUT, STACK, RET = 0x02000000, 0x02100000, 0x02200000, 0x04000000, 0x05000000
LEN = 4096


def f32(x):
    return struct.unpack('<f', struct.pack('<f', x))[0]


def machine():
    uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
    end = max(s.virtual_address + s.virtual_size for s in LOADS)
    uc.mem_map(0, (end + 0xFFFF) & ~0xFFFF)
    for s in LOADS:
        uc.mem_write(s.virtual_address, RAW[s.file_offset:s.file_offset + s.physical_size])
    for base in (WORK, OVER, OUT, STACK, RET):
        uc.mem_map(base, 0x100000)
    uc.reg_write(UC_ARM_REG_C1_C0_2, uc.reg_read(UC_ARM_REG_C1_C0_2) | (0xF << 20))
    uc.reg_write(UC_ARM_REG_FPEXC, 0x40000000)
    uc.hook_add(UC_HOOK_MEM_UNMAPPED, lambda u, a, addr, s, v, _: print(f'unmapped {addr:#x} pc {u.reg_read(UC_ARM_REG_PC):#x}') or False)
    return uc


def lcg_floats(seed, count):
    """The inputs, shared with WwiseVorbisCombineNativeTests: x = x*1103515245 + 12345 mod 2^31, value
    = x/2^31*2 - 1 rounded to binary32. Both sides regenerate them, so the fixture holds only outputs."""
    x, out = seed, []
    for _ in range(count):
        x = (x * 1103515245 + 12345) % 2147483648
        out.append(f32(x / 2147483648.0 * 2.0 - 1.0))
    return out


def run_case(bs0, bs1, prev, cur, start, end, seed):
    work = lcg_floats(seed, LEN)
    over = lcg_floats(seed + 1000, LEN)
    uc = machine()
    uc.mem_write(WORK, struct.pack(f'<{LEN}f', *work))
    uc.mem_write(OVER, struct.pack(f'<{LEN}f', *over))
    uc.mem_write(OUT, b'\x7f\xc0\x00\x00' * LEN)            # fill the output with a marker NaN to see what's written
    sp = STACK + 0xF0000
    stack = [WORK, OVER, WINDOWS[bs0 // 2], WINDOWS[bs1 // 2], OUT, 1, start, end]
    uc.mem_write(sp, struct.pack('<8I', *stack))
    for reg, v in ((UC_ARM_REG_R0, bs0), (UC_ARM_REG_R1, bs1), (UC_ARM_REG_R2, prev), (UC_ARM_REG_R3, cur),
                   (UC_ARM_REG_SP, sp), (UC_ARM_REG_LR, RET)):
        uc.reg_write(reg, v)
    uc.emu_start(COMBINE, RET, count=20_000_000)
    raw = uc.mem_read(OUT, 4 * LEN)
    out = list(struct.unpack(f'<{LEN}f', raw))
    written = [i for i in range(LEN) if raw[4 * i:4 * i + 4] != b'\x7f\xc0\x00\x00']
    return {'bs0': bs0, 'bs1': bs1, 'prev': prev, 'cur': cur, 'start': start, 'end': end,
            'work': work, 'over': over, 'written': written, 'out': out}


def cases_list():
    out, seed = [], 1
    for bs0, bs1 in ((256, 2048), (512, 1024)):
        for prev in (0, 1):
            for cur in (0, 1):
                pw = bs1 if prev else bs0
                cw = bs1 if cur else bs0
                full = pw // 4 + cw // 4
                for start, end in ((0, full), (3, full), (0, full // 2), (full // 3, full)):
                    out.append((bs0, bs1, prev, cur, start, end, seed))
                    seed += 1
    return out


def write_fixture():
    """Fixtures/combine_native.bin: u32 count; per case six i32 (bs0, bs1, prev, cur, start, end), i32 seed,
    then the engine's end-start binary32 outputs."""
    import os
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..',
                        'cozmo-stack', 'tests', 'Cozmo.Protocol.Tests', 'Fixtures', 'combine_native.bin')
    cs = cases_list()
    with open(path, 'wb') as f:
        f.write(struct.pack('<I', len(cs)))
        for bs0, bs1, prev, cur, start, end, seed in cs:
            c = run_case(bs0, bs1, prev, cur, start, end, seed)
            n = end - start
            f.write(struct.pack('<7i', bs0, bs1, prev, cur, start, end, seed))
            f.write(struct.pack(f'<{n}f', *c['out'][:n]))
    print('wrote', os.path.normpath(path), len(cs), 'cases')


if __name__ == '__main__':
    import sys
    if '--fixtures' in sys.argv:
        write_fixture()
        raise SystemExit
    cases = []
    seed = 1
    for bs0, bs1 in ((256, 2048), (512, 1024)):
        for prev in (0, 1):
            for cur in (0, 1):
                pw = bs1 if prev else bs0
                cw = bs1 if cur else bs0
                full = pw // 4 + cw // 4
                for start, end in ((0, full), (3, full), (0, full // 2), (full // 3, full)):
                    c = run_case(bs0, bs1, prev, cur, start, end, seed)
                    seed += 1
                    cases.append(c)
                    w = c['written']
                    print(f'bs {bs0}/{bs1} prev {prev} cur {cur} start {start:4d} end {end:4d}: wrote {len(w)} floats'
                          + (f' [{w[0]}..{w[-1]}]' if w else ''))
    json.dump(cases, open('combine_cases.json', 'w'))
