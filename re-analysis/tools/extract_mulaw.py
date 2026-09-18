#!/usr/bin/env python3
"""
Reproduce the evidence behind Cozmo.Robot.AnkiMuLaw from libcozmoEngine.so.

The robot's speaker does not take G.711. The engine's own encoder omits the 132 bias and never complements
the result, so silence is 0x00 rather than 0xFF. Sending standard mu-law is heard as a loud buzz at roughly
the right pitch. This script re-derives that from the binary so the claim is checkable rather than asserted.

Usage:
    python re-analysis/tools/extract_mulaw.py resources/lib/armeabi-v7a/libcozmoEngine.so \
        [--out re-analysis/disassembly/dis_mulaw.txt]

It prints, and optionally writes:
  * the disassembly of Anki::Cozmo::Audio::encodeMuLaw(float)
  * the 128-byte segment table it indexes, from .rodata
  * the float scale constant it multiplies by
  * a self-check that the transcribed algorithm matches the C# implementation's documented behaviour
"""
import argparse
import struct
import subprocess
import sys
from pathlib import Path

# Addresses are file virtual addresses in the armeabi-v7a libcozmoEngine.so shipped with
# com.anki.cozmo 3.4.0-1204. They are asserted below against the symbol table, so a different
# build fails loudly instead of producing wrong constants.
ENCODE_MULAW = "_ZN4Anki5Cozmo5Audio11encodeMuLawEf"
EXPECTED_VA = 0x00597AD8
SEGMENT_TABLE_VA = 0x00C5C3F0
SEGMENT_TABLE_LEN = 128
SCALE_CONST_VA = 0x00597C18


def load(path):
    import lief
    so = lief.ELF.parse(str(path))
    if so is None:
        sys.exit(f"not an ELF file: {path}")
    return so, Path(path).read_bytes()


def va_to_off(so, va):
    import lief
    for seg in so.segments:
        if seg.type == lief.ELF.Segment.TYPE.LOAD and seg.virtual_address <= va < seg.virtual_address + seg.virtual_size:
            return va - seg.virtual_address + seg.file_offset
    sys.exit(f"virtual address {va:#x} is not in any load segment")


def find_symbol(so, name):
    for s in so.dynamic_symbols:
        if s.name == name and s.value:
            return s.value & ~1
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("so", help="path to libcozmoEngine.so")
    ap.add_argument("--out", help="also write the report to this file")
    args = ap.parse_args()

    so, raw = load(args.so)

    va = find_symbol(so, ENCODE_MULAW)
    if va is None:
        sys.exit(f"{ENCODE_MULAW} is not exported by this library; it is not the build this was derived from")
    if va != EXPECTED_VA:
        sys.exit(f"{ENCODE_MULAW} is at {va:#x}, not the expected {EXPECTED_VA:#x}; "
                 "the constants below belong to a different build")

    table = raw[va_to_off(so, SEGMENT_TABLE_VA):va_to_off(so, SEGMENT_TABLE_VA) + SEGMENT_TABLE_LEN]
    scale = struct.unpack_from("<f", raw, va_to_off(so, SCALE_CONST_VA))[0]

    expected_table = bytes([0, 1, 2, 2] + [3] * 4 + [4] * 8 + [5] * 16 + [6] * 32 + [7] * 64)
    if table != expected_table:
        sys.exit("the segment table does not match the one transcribed into AnkiMuLaw")
    if scale != 32767.0:
        sys.exit(f"the scale constant is {scale}, not the 32767.0 transcribed into AnkiMuLaw")

    out = [
        f"Anki::Cozmo::Audio::encodeMuLaw(float) @ {va:#010x}",
        f"segment table @ {SEGMENT_TABLE_VA:#010x}, {SEGMENT_TABLE_LEN} bytes",
        f"scale constant @ {SCALE_CONST_VA:#010x} = {scale}",
        "",
        "segment table:",
        "  " + ", ".join(str(b) for b in table),
        "",
        "transcribed algorithm (see cozmo-stack/src/Cozmo.Robot/Audio.cs, AnkiMuLaw):",
        "  if (isnan(f)) return 0;",
        "  s    = f <= -1 ? -32767 : (int)(min(f, 1) * 32767)",
        "  mag  = s ^ (s >> 15)          // no 132 bias, unlike G.711",
        "  exp  = segment[mag >> 8]",
        "  mant = (mag >> 8) == 0 ? mag >> 4 : (mag >> (exp + 3)) & 0x0F",
        "  byte = (s < 0 ? 0x80 : 0) | (exp << 4) | mant      // NOT complemented, unlike G.711",
        "",
        "consequence: silence encodes to 0x00, where standard G.711 gives 0xFF.",
        "",
    ]

    # self-check: the transcription must agree with what the C# says it does
    def encode(sample):
        s = -32767 if sample < -32767 else sample
        mag = s ^ (s >> 15)
        hi = mag >> 8
        exp = table[hi]
        mant = (mag >> 4) if hi == 0 else ((mag >> (exp + 3)) & 0x0F)
        return ((0x80 if s < 0 else 0) | (exp << 4) | mant) & 0xFF

    checks = [(0, 0x00), (32767, 0x7F), (-32767, 0xFF), (-1, 0x80)]
    for value, expect in checks:
        got = encode(value)
        if got != expect:
            sys.exit(f"self-check failed: encode({value}) = {got:#04x}, expected {expect:#04x}")
    out.append("self-check: encode(0)=0x00 encode(32767)=0x7F encode(-32767)=0xFF encode(-1)=0x80  OK")
    out.append("")

    # the disassembly itself, via the tool already in this directory
    disarm = Path(__file__).with_name("disarm.py")
    out.append("disassembly:")
    try:
        text = subprocess.run([sys.executable, str(disarm), args.so, ENCODE_MULAW],
                              capture_output=True, text=True, check=True).stdout
        out.append(text)
    except Exception as e:                                    # pragma: no cover - diagnostic path
        out.append(f"  (could not run disarm.py: {e})")

    report = "\n".join(out)
    print(report)
    if args.out:
        Path(args.out).write_text(report, encoding="utf-8")
        print(f"written to {args.out}")


if __name__ == "__main__":
    main()
