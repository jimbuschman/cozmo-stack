#!/usr/bin/env python3
"""ARM-mode disassembler for libcozmoEngine.so, for M6 Wwise evidence recovery.

The Wwise runtime (statically linked Wwise 2016.2) is ARM-mode, not Thumb, in the
unnamed region 0x0095E540..0x00AE2E40 of libcozmoEngine.so. This prints a VA range
with pc-relative literal loads annotated, which is how the STMG reader at 0x9B0B14
was decoded (see re-analysis/M6-STATUS.md).

Requires: pip install capstone lief

Usage:
    python re-analysis/tools/arm_disasm.py <start-hex> <end-hex> [so-path]

The default SO path is the extracted APK below; override with the third argument.
"""
import sys

import capstone
import lief

DEFAULT_SO = (
    r"C:\Users\JimBu\Downloads"
    r"\com.anki.cozmo_3.4.0-1204_minAPI21(armeabi-v7a)(nodpi)_apkmirror.com.apk_Decompiler.com"
    r"\resources\lib\armeabi-v7a\libcozmoEngine.so"
)


def main() -> None:
    start = int(sys.argv[1], 16)
    end = int(sys.argv[2], 16)
    path = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_SO

    so = lief.ELF.parse(path)
    raw = open(path, "rb").read()

    def va2off(va: int):
        for seg in so.segments:
            if seg.type == lief.ELF.Segment.TYPE.LOAD and seg.virtual_address <= va < seg.virtual_address + seg.virtual_size:
                return va - seg.virtual_address + seg.file_offset
        return None

    def rd32(va: int):
        o = va2off(va)
        return int.from_bytes(raw[o:o + 4], "little") if o is not None else None

    off = va2off(start)
    if off is None:
        raise SystemExit(f"0x{start:X} is not in a loadable segment")
    md = capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_ARM)
    md.detail = True
    for insn in md.disasm(raw[off:off + (end - start)], start):
        line = f"0x{insn.address:08X}: {insn.mnemonic:8s} {insn.op_str}"
        ann = []
        for op in insn.operands:
            if op.type == capstone.arm.ARM_OP_MEM and op.mem.base == capstone.arm.ARM_REG_PC:
                lit = insn.address + 8 + op.mem.disp
                ann.append(f"[pc->0x{lit:08X}] = 0x{(rd32(lit) or 0):08X}")
            if op.type == capstone.arm.ARM_OP_IMM and insn.mnemonic in ("bl", "b", "blx", "bx"):
                ann.append(f"->0x{op.imm:08X}")
        if ann:
            line += "   ; " + " ".join(ann)
        print(line)


if __name__ == "__main__":
    main()
