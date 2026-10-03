"""Run the engine's own 0xA544BC (the pending-voice "not ready" check) under Unicorn, for WwiseVoiceLinkerTests.

0xA544BC(voice, pbi) re-runs 0xA56650(source, [owner+0x1DC], [owner+0x1E0]) and, on a result of 1, compares
[pbi+0x1D8] with trunc_s32((float)(u32)(([0x108D90C+0x1C]+1) * u16[0x1052440]) * [pbi+0x164] +- 0.5f)
(M6-wwise-bank.md C30.1). This script builds a fake voice/source/PBI in emulated memory, stands in for the source's
vt+0x28 with a stub that returns a chosen code, and prints, for each case, the engine's own result and what the
callees it reaches (0xA56650's vt+0x28 arguments, 0xA0428C) were given.

    python re-analysis/tools/emu/emu_notready.py

The expected values in WwiseVoiceLinkerTests (NotReady*) are this script's output, not the C#'s.
"""
import struct
import sys
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED
from unicorn.arm_const import *
from so import RAW, LOADS

ENTRY = 0xA544BC
GOT = 0xA544DC + 0x5EBDB0            # add r5, pc, r5 at 0xA544D4 (pc reads +8)
SLOT_L = GOT - 0x1F4                  # [pc->0xA545E4] = 0xFFFFFE0C -> pointer to 0x108D90C
SLOT_F = GOT - 0x234                  # [pc->0xA545E8] = 0xFFFFFDCC -> pointer to u16 0x1052440
SLOT_MGR = GOT - 0x22C                # [pc->0xA545EC] = 0xFFFFFDD4 -> pointer to the manager pointer

BASE = 0x02000000
LSTRUCT = BASE + 0x000               # +0x1C = look-ahead L
FVAR = BASE + 0x100                  # u16 frames
MGRPTR = BASE + 0x110                # pointer to manager
MGR = BASE + 0x120
VOICE = BASE + 0x1000
SOURCE = BASE + 0x2000
VTABLE = BASE + 0x3000
STUB = BASE + 0x4000                 # mov r0,#code (patched) ; bx lr  -- also records r1,r2
PBI = BASE + 0x5000                  # the pbi argument (r1)
OWNER = BASE + 0x6000                # [source+0xC]: the owner PBI whose +0x1DC/+0x1E0 feed vt+0x28
NODE = BASE + 0x7000
STACK = 0x04000000
RET = 0x05000000
LOG = BASE + 0x8000


def f32bits(x):
    return struct.unpack('<I', struct.pack('<f', x))[0]


def run(code, offset, ratio, frames, look, src_flags=0, voice_ctx=True, owner_1dc=0x11112222, owner_1e0=0x33334444):
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
    w32(SLOT_L, LSTRUCT)
    w32(SLOT_F, FVAR)
    w32(SLOT_MGR, MGRPTR)
    w32(MGRPTR, MGR)
    w32(LSTRUCT + 0x1C, look)
    uc.mem_write(FVAR, struct.pack('<H', frames))

    # voice: [+0xD4] source, [+8] ctx = pbi+0xC context (0x9BD138 reads [ctx+0xD4] then [..+8]), [+0xE8] flags
    w32(VOICE + 0xD4, SOURCE)
    ctx = OWNER + 0xC
    w32(VOICE + 8, ctx if voice_ctx else 0)
    uc.mem_write(VOICE + 0xE8, b'\x00')
    w32(ctx + 0xD4, NODE)               # [ctx+0xD4] = the node; 0x9BD138 returns [node+8]
    w32(NODE + 8, 0xAABBCCDD)
    w32(ctx + 0x134, 0x00C0FFEE)        # [ctx+0x134] = pbi+0x140 = the playing id (the 2nd argument of 0xA0428C)
    # source: [+0] vtable, [+0xC] owner, [+0x10] flags (bit0 = latch, bit1 = the 0xA54580 gate)
    w32(SOURCE, VTABLE)
    w32(SOURCE + 0xC, OWNER)
    uc.mem_write(SOURCE + 0x10, bytes([src_flags]))
    w32(VTABLE + 0x28, STUB)
    # stub: mov r0,#code; str r1,[LOG]; str r2,[LOG+4]; bx lr  -> assembled by hand
    ldr_log = 0xE59FC00C                 # ldr ip,[pc,#12]  (pc = STUB+8 -> STUB+0x14)
    mov_r0 = 0xE3A00000 | code
    str_r1 = 0xE58C1000                  # str r1,[ip]
    str_r2 = 0xE58C2004                  # str r2,[ip,#4]
    bx_lr = 0xE12FFF1E
    insns = [ldr_log, mov_r0, str_r1, str_r2, bx_lr, LOG]
    # layout: STUB+0 ldr ip,=LOG ; +4 mov r0 ; +8 str r1 ; +0xC str r2 ; +0x10 bx lr ; +0x14 .word LOG
    uc.mem_write(STUB, b''.join(struct.pack('<I', i) for i in insns))
    # owner PBI (the source's [+0xC]) and the pbi argument
    w32(OWNER + 0x1DC, owner_1dc)
    w32(OWNER + 0x1E0, owner_1e0)
    w32(PBI + 0x1D8, offset)
    w32(PBI + 0x164, f32bits(ratio))

    calls = []

    def hook(u, addr, size, _):
        if addr == 0xA0428C:             # the unread body: record the arguments and return
            calls.append(('A0428C', u.reg_read(UC_ARM_REG_R0), u.reg_read(UC_ARM_REG_R1), u.reg_read(UC_ARM_REG_R2)))
            u.reg_write(UC_ARM_REG_PC, u.reg_read(UC_ARM_REG_LR))
    uc.hook_add(UC_HOOK_CODE, hook, begin=0xA0428C, end=0xA0428C)
    uc.hook_add(UC_HOOK_MEM_UNMAPPED, lambda u, a, addr, s, v, _: print(f'unmapped {addr:#x} pc {u.reg_read(UC_ARM_REG_PC):#x}') or False)

    uc.reg_write(UC_ARM_REG_SP, STACK + 0xF0000)
    uc.reg_write(UC_ARM_REG_LR, RET)
    uc.reg_write(UC_ARM_REG_R0, VOICE)
    uc.reg_write(UC_ARM_REG_R1, PBI)
    try:
        uc.emu_start(ENTRY, RET)
        fault = None
    except Exception as e:             # the deliberate udf of a null [voice+8]
        fault = f'{type(e).__name__} at pc {uc.reg_read(UC_ARM_REG_PC):#x}'
    r0 = uc.reg_read(UC_ARM_REG_R0)
    args = struct.unpack('<II', bytes(uc.mem_read(LOG, 8)))
    return {
        'result': r0 if fault is None else None,
        'fault': fault,
        'latch': bool(bytes(uc.mem_read(SOURCE + 0x10, 1))[0] & 1),
        'voiceE8': bytes(uc.mem_read(VOICE + 0xE8, 1))[0],
        'vt28_args': [hex(a) for a in args],
        'calls': [(c[0], hex(c[1]), hex(c[2]), hex(c[3])) for c in calls],
    }


def main():
    cases = []
    # (label, code, offset(s32), ratio, frames u16, look u32, src_flags)
    f = lambda bits: struct.unpack('<f', struct.pack('<I', bits))[0]
    one = 1.0
    for label, code, offset, ratio, frames, look, flags in [
        ('latched source: 0xA56650 returns 1 without calling vt+0x28, window 2048, offset 2047', 1, 2047, one, 0x400, 1, 1),
        ('latched, offset == window 2048 -> 0x3F', 1, 2048, one, 0x400, 1, 1),
        ('latched, negative offset below the window -> side effect then 1', 1, -1, one, 0x400, 1, 1),
        ('latched, negative offset, source bit1 set -> no side effect, 1', 1, -1, one, 0x400, 1, 3),
        ('0x3F, offset 5 -> 0x3F, no side effect', 0x3F, 5, one, 0x400, 1, 0),
        ('0x3F, offset -1 -> side effect, 0x3F', 0x3F, -1, one, 0x400, 1, 0),
        ('other code 7 -> 2', 7, 0, one, 0x400, 1, 0),
        ('code 0 -> 2', 0, 0, one, 0x400, 1, 0),
        ('vt+0x28 returns 1 (latch clear) -> latch set, window compare', 1, 100, one, 0x400, 1, 0),
        ('ratio 0.49999997f, frames 1: product 0.49999997 + 0.5 = 1.0f -> window 1, offset 1 -> 0x3F', 1, 1, f(0x3EFFFFFF), 1, 0, 1),
        ('ratio 0.49999997f, frames 1: window 1, offset 0 -> 1', 1, 0, f(0x3EFFFFFF), 1, 0, 1),
        ('ratio 0.0f: product 0 -> -0.5 -> trunc 0, offset 0 -> 0x3F (0 >= 0)', 1, 0, 0.0, 0x400, 1, 1),
        ('ratio 0.0f: window 0, offset -1 -> side effect then 1', 1, -1, 0.0, 0x400, 1, 1),
        ('ratio negative -1.0f: window trunc(-2048-0.5) = -2048, offset -2048 -> 0x3F', 1, -2048, -1.0, 0x400, 1, 1),
        ('ratio negative -1.0f: window -2048, offset -2049 -> side effect then 1', 1, -2049, -1.0, 0x400, 1, 1),
        ('ratio 1.5f frames 3, L 1: product 9.0 + 0.5 -> 9, offset 9 -> 0x3F', 1, 9, 1.5, 3, 1, 1),
        ('ratio 1.5f frames 3, L 1: window 9, offset 8 -> 1', 1, 8, 1.5, 3, 1, 1),
        ('ratio 0.5f frames 5 L 1: product 5.0 -> 5.5 -> 5; offset 5 -> 0x3F', 1, 5, 0.5, 5, 1, 1),
        ('ratio 0.25f frames 2 L 1: product 1.0 -> 1.5 -> 1; offset 1 -> 0x3F', 1, 1, 0.25, 2, 1, 1),
        ('ratio 0.25f frames 2 L 1: window 1; offset 0 -> 1', 1, 0, 0.25, 2, 1, 1),
        ('L 0xFFFFFFFF wraps to 0: (L+1)*frames = 0 -> window 0 (-0.5 -> 0)', 1, 0, one, 0x400, 0xFFFFFFFF, 1),
        ('frames 0xFFFF L 1: u32 product 131070 * 1.0 -> 131070', 1, 131070, one, 0xFFFF, 1, 1),
        ('frames 0xFFFF L 1: window 131070, offset 131069 -> 1', 1, 131069, one, 0xFFFF, 1, 1),
        ('NaN ratio: product NaN, +-0.5 branch -0.5, cvt gives 0; offset 0 -> 0x3F', 1, 0, f(0x7FC00000), 0x400, 1, 1),
        ('NaN ratio: window 0; offset -1 -> side effect, 1', 1, -1, f(0x7FC00000), 0x400, 1, 1),
        ('huge ratio 3e9f saturates to 0x7FFFFFFF; offset 0x7FFFFFFE -> 1', 1, 0x7FFFFFFE, 3e9, 0x400, 1, 1),
        ('huge ratio 3e9f saturates; offset 0x7FFFFFFF -> 0x3F', 1, 0x7FFFFFFF, 3e9, 0x400, 1, 1),
        ('huge negative ratio saturates to INT_MIN; offset -2147483648 -> 0x3F', 1, -2147483648, -3e9, 0x400, 1, 1),
        ('odd float above 2^23: frames 0xFFFF * ratio 255.00002f', 1, 0, f(0x437F0001), 0xFFFF, 1, 1),
        ('product 8388609.0 (odd, 2^23+1): +0.5 ties to even, window 8388610; offset 8388609 -> 1', 1, 8388609, one, 3, 2796202, 1),
        ('product 8388609.0: offset 8388610 -> 0x3F', 1, 8388610, one, 3, 2796202, 1),
        ('product 8388608.0 (even): window 8388608; offset 8388607 -> 1', 1, 8388607, one, 2, 4194303, 1),
        ('product 8388608.0 (even): offset 8388608 -> 0x3F', 1, 8388608, one, 2, 4194303, 1),
        ('ratio 0.49999997f frames 1 L 0: product 0.49999997 + 0.5 = 1.0f, window 1, offset 0 -> 1', 1, 0, f(0x3EFFFFFF), 1, 0, 1),
        ('ratio 0.49999997f frames 1 L 0: offset 1 -> 0x3F', 1, 1, f(0x3EFFFFFF), 1, 0, 1),
    ]:
        r = run(code, offset, ratio, frames, look, flags)
        print(f'{label}\n    {r}')
    print('--- null [voice+8] (the deliberate udf at 0xA545D8..0xA545DC): E8 is stored before the fault')
    print(run(0x3F, -1, one, 0x400, 1, 0, voice_ctx=False))


if __name__ == '__main__':
    main()
