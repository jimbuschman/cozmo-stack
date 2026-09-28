"""Run the engine's own Vorbis inverse MDCT, mdct_backward (0x00AB4E34), from libcozmoEngine.so under Unicorn.

It captures the in-place buffer and the work buffer at each phase boundary, and writes the regression fixtures
the C# transliteration is checked against bit for bit (WwiseVorbisImdctNativeTests).

    python re-analysis/tools/emu/emu_imdct.py --fixtures      # writes the test fixtures
    python re-analysis/tools/emu/emu_imdct.py 256 2048        # prints a summary per size

Needs: pip install unicorn capstone lief. The engine's work-buffer pointer (BSS 0x0108E648, whose writer is
not recovered, X5-I2) is set to a buffer here, as the C# models it.
"""
import json, os, struct, sys, random
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED
from unicorn.arm_const import *
from so import RAW, LOADS

ENTRY = 0x00AB4E34
WORK_PTR_SLOT = 0x0108E648
PHASES = {  # the address reached -> the phase that has just finished
    0x00AB4EBC: 'after_presymmetry',
    0x00AB4ED0: 'after_butterfly',
    0x00AB5248: 'after_stages',
    0x00AB5A20: 'after_terminal',
}
IN_BUF, WORK_BUF, STACK, RET = 0x03000000, 0x02000000, 0x04000000, 0x05000000
FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..',
                        'cozmo-stack', 'tests', 'Cozmo.Protocol.Tests', 'Fixtures')


def run(n, seed=1):
    rnd = random.Random(seed)
    data = [struct.unpack('<f', struct.pack('<f', rnd.uniform(-1.0, 1.0)))[0] for _ in range(n)]

    uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
    end = max(s.virtual_address + s.virtual_size for s in LOADS)
    uc.mem_map(0, (end + 0xFFFF) & ~0xFFFF)
    for s in LOADS:
        uc.mem_write(s.virtual_address, RAW[s.file_offset:s.file_offset + s.physical_size])
    for base in (IN_BUF, WORK_BUF, STACK, RET):
        uc.mem_map(base, 0x100000)
    uc.mem_write(WORK_PTR_SLOT, struct.pack('<I', WORK_BUF))
    uc.mem_write(IN_BUF, struct.pack(f'<{n}f', *data))

    # VFP/NEON on: CPACR cp10/cp11 full access, FPEXC.EN
    uc.reg_write(UC_ARM_REG_C1_C0_2, uc.reg_read(UC_ARM_REG_C1_C0_2) | (0xF << 20))
    uc.reg_write(UC_ARM_REG_FPEXC, 0x40000000)
    uc.reg_write(UC_ARM_REG_SP, STACK + 0xF0000)
    uc.reg_write(UC_ARM_REG_LR, RET)
    uc.reg_write(UC_ARM_REG_R0, n)
    uc.reg_write(UC_ARM_REG_R1, IN_BUF)

    dumps = {'input': data}

    def grab(tag):
        dumps[tag] = {
            'in': list(struct.unpack(f'<{n}f', uc.mem_read(IN_BUF, 4 * n))),
            'work': list(struct.unpack(f'<{n}f', uc.mem_read(WORK_BUF, 4 * n))),
        }

    seen = set()

    def on_code(uc_, addr, size, _):
        tag = PHASES.get(addr)
        if tag and tag not in seen:
            seen.add(tag)
            grab(tag)

    def on_unmapped(uc_, access, addr, size, value, _):
        print(f'unmapped access {access} at {addr:#x} (pc {uc_.reg_read(UC_ARM_REG_PC):#x})')
        return False

    for a in PHASES:
        uc.hook_add(UC_HOOK_CODE, on_code, begin=a, end=a)
    uc.hook_add(UC_HOOK_MEM_UNMAPPED, on_unmapped)
    uc.emu_start(ENTRY, RET, count=50_000_000)
    grab('final')
    dumps['phases_seen'] = sorted(seen)
    return dumps


def write_fixture(n):
    """Fixtures/imdct_native_<n>.bin: u32 n, then n binary32 inputs, then the engine's n binary32 outputs."""
    d = run(n)
    path = os.path.join(FIXTURES, f'imdct_native_{n}.bin')
    with open(path, 'wb') as f:
        f.write(struct.pack('<I', n))
        f.write(struct.pack(f'<{n}f', *d['input']))
        f.write(struct.pack(f'<{n}f', *d['final']['in']))
    print('wrote', os.path.normpath(path))


if __name__ == '__main__':
    if '--fixtures' in sys.argv:
        for n in (256, 512, 1024, 2048):      # every block size the shipped media declare (256/2048, 512/1024)
            write_fixture(n)
    else:
        for n in [int(x) for x in sys.argv[1:]] or [256, 2048]:
            d = run(n)
            json.dump(d, open(f'native_{n}.json', 'w'))
            print(n, 'phases', d['phases_seen'])
