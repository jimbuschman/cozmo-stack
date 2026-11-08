"""Run the shipped English voice effects against fixed structural and PCM inputs.

This reads the user's local engine library through emu_common and does not export banks.
Run from the repo root with the dependencies used by emu_fxlib on PYTHONPATH.
"""
import math
from struct import pack, unpack

from mpmath import mp, mpf
from emu_comp import fb, ff, round_mpf_to_f32, sha
from emu_fxlib import FxEngine, ALLOC, SCRATCH


HARMONIZER = bytes.fromhex(
    "0100007a440000000004000000000000000000fa450000803f"
    "0100007a44000080c0050000000000000000c050440000803f"
    "000000000000c0c200000000000400000000"
)
EXPANDER = bytes.fromhex("00003cc26666d6409eef273de17a943e000000000000")


def main():
    fx = FxEngine()
    e = fx.e

    def double_math(fn, machine):
        value = unpack("<d", pack("<II", machine.reg(0), machine.reg(1)))[0]
        low, high = unpack("<II", pack("<d", float(fn(mpf(value)))))
        machine.uc.reg_write(67, high)
        return low

    def divmod_hook(machine):
        quotient, remainder = divmod(machine.reg(0), machine.reg(1))
        machine.uc.reg_write(67, remainder)
        return quotient

    e.hook(0x4D6778, lambda m: round_mpf_to_f32(mp.power(mpf(ff(m.reg(0))), mpf(ff(m.reg(1))))))
    e.hook(0x4CF578, divmod_hook)
    e.hook(0x4B3E70, lambda m: m.reg(0) // m.reg(1))
    e.hook(0x4A4084, lambda m: fb(math.floor(ff(m.reg(0)))))
    e.hook(0x4A9F04, lambda m: double_math(mp.floor, m))
    e.hook(0x4D0070, lambda m: double_math(mp.cos, m))
    e.hook(0x4D007C, lambda m: double_math(mp.sin, m))

    for name, param_create, create, block in (
        ("expander", 0xABCF50, 0xABCC84, EXPANDER),
        ("harmonizer", 0xAA5BC4, 0xAA32BC, HARMONIZER),
    ):
        fx.begin()
        params = e.call(param_create, ALLOC)
        effect = e.call(create, ALLOC)
        context = SCRATCH + 0x180
        e.call(0xA6C22C, context, 0, 0)
        e.uc.mem_write(SCRATCH + 0x80, block)
        param_result = e.call(fx.slot(params, 4), params, ALLOC, SCRATCH + 0x80, len(block))
        e.uc.mem_write(SCRATCH, pack("<III", 48000, 0x4101, 0))
        init_result = e.call(fx.slot(effect, 7), effect, ALLOC, context, params, stack=(SCRATCH,))
        reset_result = e.call(fx.slot(effect, 3), effect)
        print(name, "params", param_result, "init", init_result, "reset", reset_result)
        for frame in range(6):
            data, count = fx.put_audio(0, 7 + frame, 1, 256)
            code, valid = (0x11, 199) if frame == 5 else (0x2D, 256)
            state = SCRATCH + 0x100
            e.uc.mem_write(state, pack("<IIIHH", data, 0x4101, code, 256, valid))
            e.call(fx.slot(effect, 8), effect, state)
            print(frame, "max", e.r16(state + 12), "valid", e.r16(state + 14),
                  "state", hex(e.r32(state + 8)), "sha256-prefix", sha(fx.read_audio(data, count)))
        print("term", e.call(fx.slot(effect, 2), effect, ALLOC))


if __name__ == "__main__":
    main()
