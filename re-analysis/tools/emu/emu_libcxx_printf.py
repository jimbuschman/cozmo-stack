"""Shipped libc++_shared.so printf oracle for M3-041 L30 (Unicorn 2.1.4, lief).

Runs the packaged vsnprintf wrapper 0x82528 (Thumb), which calls the printf core 0x813B0, the formatter the IMU
logger's float insertion ("%.*g", libc++ num_put 0x4A210), integer insertion ("%ld", num_put 0x499D8) and
to_string(unsigned) ("%u", 0x660E8 -> 0x7E270 -> 0x82528) reach.

  python emu_libcxx_printf.py --selftest
  python emu_libcxx_printf.py --write <fixture.json>      regenerate the checked-in fixture
  python emu_libcxx_printf.py g 6 3fe0000000000000        one %.*g case: precision, double bits (hex)
  python emu_libcxx_printf.py ld -5                       one %ld case

Argument layout: AAPCS softfp variadic stack, exactly what __libcpp_snprintf_l hands to vsnprintf: the va_list word
points at the argument area; an int precision at +0, a double 8-byte aligned at +8, a long at +0.
Fixture boundaries (not claims about the phone): FPSCR round-to-nearest (the Android default, the M1-029 decision),
successful memcpy/memset, and the leaf imports __signbit/__isfinite/strlen/memchr with their IEEE/libc meaning.
Stack memory is zeroed before each call (see vsnprintf). Any other import, instruction-budget exhaustion or an
unmapped access aborts; nothing is fabricated.
"""
import sys, struct, hashlib, json, random, argparse
from pathlib import Path
import lief
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE
from unicorn.arm_const import *

PATH = str(Path(__file__).resolve().parents[3] / 'resources/lib/armeabi-v7a/libc++_shared.so')
EXPECTED_SHA256 = '8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a'
raw = open(PATH, 'rb').read()
if hashlib.sha256(raw).hexdigest() != EXPECTED_SHA256:
    raise RuntimeError('unsupported libc++ artifact: address map must be rechecked')
elf = lief.parse(PATH)

VSNPRINTF = 0x82528          # Thumb; call with bit 0 set
STACK = 0x4000000; RET = 0x5000000; STUB = 0x6000000; DATA = 0x3000000
LIMIT = 0xB0000
uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
uc.mem_map(0, LIMIT)
for s in elf.segments:
    if s.type == lief.ELF.Segment.TYPE.LOAD:
        if s.virtual_address + s.virtual_size > LIMIT:
            raise RuntimeError('segment beyond mapped image')
        uc.mem_write(s.virtual_address, raw[s.file_offset:s.file_offset + s.physical_size])
for a, n in [(DATA, 0x100000), (STACK, 0x100000), (RET, 0x1000), (STUB, 0x100000)]:
    uc.mem_map(a, n)
uc.reg_write(UC_ARM_REG_C1_C0_2, 0xF << 20)
uc.reg_write(UC_ARM_REG_FPEXC, 0x40000000)


def w32(a, v): uc.mem_write(a, struct.pack('<I', v & 0xFFFFFFFF))
def r32(a): return int.from_bytes(uc.mem_read(a, 4), 'little')
def rg(n): return uc.reg_read([UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2, UC_ARM_REG_R3][n])


hooks = {}; names = {}
for rel in elf.relocations:
    if not rel.has_symbol or not rel.symbol.name:
        continue
    s = rel.symbol
    if s.value:
        w32(rel.address, s.value + (r32(rel.address) if 'ABS32' in str(rel.type) else 0))
        continue
    if s.name not in names:
        names[s.name] = STUB + len(names) * 0x100
    w32(rel.address, names[s.name])
ERRNO = DATA + 0x80000
for n, a in names.items():
    if n == '__stack_chk_guard':
        w32(a, 0x12345678)
    else:
        hooks[a] = n
IMPORTS_USED = set()


def execute(uc, a, size, data):
    if a == RET:
        uc.emu_stop(); return
    n = hooks.get(a)
    if n is None:
        return
    IMPORTS_USED.add(n)
    v = 0
    if n == '__errno': v = ERRNO
    elif n == '__signbit': v = 1 if (rg(1) >> 31) & 1 else 0
    elif n == '__isfinite': v = 0 if ((rg(1) >> 20) & 0x7FF) == 0x7FF else 1
    elif n == 'strlen':
        p = rg(0)
        while uc.mem_read(p + v, 1)[0]: v += 1
    elif n == 'memchr':
        p, c, cnt = rg(0), rg(1) & 255, rg(2)
        v = 0
        for i in range(cnt):
            if uc.mem_read(p + i, 1)[0] == c: v = p + i; break
    elif n in ('memcpy', 'memmove', '__aeabi_memcpy', '__aeabi_memcpy4', '__aeabi_memcpy8'):
        uc.mem_write(rg(0), bytes(uc.mem_read(rg(1), rg(2)))); v = rg(0)
    elif n.startswith('__aeabi_memclr'):
        uc.mem_write(rg(0), bytes(rg(1)))
    elif n in ('memset', '__aeabi_memset', '__aeabi_memset4', '__aeabi_memset8'):
        length, value = (rg(2), rg(1)) if n == 'memset' else (rg(1), rg(2))
        uc.mem_write(rg(0), bytes([value & 255]) * length); v = rg(0)
    else:
        raise RuntimeError(f'unmodeled import {n} at {a:x}, LR={uc.reg_read(UC_ARM_REG_LR):x}')
    uc.reg_write(UC_ARM_REG_R0, v)
    uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))


for ha in [RET, *hooks]:
    uc.hook_add(UC_HOOK_CODE, execute, begin=ha, end=ha)

IMAGE = bytes(uc.mem_read(0, LIMIT))
FMT = DATA; BUF = DATA + 0x1000; ARGS = DATA + 0x20000


def vsnprintf(fmt, args, n=512):
    """Returns (result, text) of 0x82528(buf, n, fmt, va_list)."""
    uc.mem_write(0, IMAGE)
    # Fixture boundary: the printf core reads the limb below its leading limb when a rounding carry leaves the leading
    # limb (0x821E4: no zero is stored there first). On a device that word is stack residue; here it is zero, so the
    # corpus is deterministic. Cases that reach it need a 9-digit leading limb of 999999xxx (values near 1e8..1e9).
    # (Carry loop 0x821E4..0x821F0: the limb below `a` is uninitialised stack on the phone. This zeroed stack matches the
    # C# port, which uses 0; with residue there, e.g. f32 999999552 prints differently.)
    uc.mem_write(STACK + 0xE0000, bytes(0x10000))
    w32(ERRNO, 77)
    uc.mem_write(FMT, fmt.encode() + b'\0')
    uc.mem_write(BUF, b'\xAA' * (n + 16))
    uc.mem_write(ARGS, args)
    for k in range(UC_ARM_REG_R0, UC_ARM_REG_R12 + 1): uc.reg_write(k, 0)
    for k in range(UC_ARM_REG_D0, UC_ARM_REG_D31 + 1): uc.reg_write(k, 0)
    for k, v in zip([UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2, UC_ARM_REG_R3], [BUF, n, FMT, ARGS]):
        uc.reg_write(k, v)
    for k, v in [(UC_ARM_REG_SP, STACK + 0xF0000), (UC_ARM_REG_LR, RET | 1), (UC_ARM_REG_FPSCR, 0)]:
        uc.reg_write(k, v)
    uc.emu_start(VSNPRINTF | 1, RET, count=5000000)
    if uc.reg_read(UC_ARM_REG_PC) != RET:
        raise RuntimeError('instruction budget exhausted')
    res = rg(0)
    if res >= 0x80000000: res -= 1 << 32
    out = bytes(uc.mem_read(BUF, n + 16))
    if res >= 0 and n > 0:
        end = min(res, n - 1)
        assert out[end] == 0, 'NUL position'
        assert out[n:n + 16] == b'\xAA' * 16, 'wrote past n'
        return res, out[:end].decode('ascii')
    return res, ''


def fmt_g(precision, bits):
    """%.*g of the double with these bits."""
    res, text = vsnprintf('%.*g', struct.pack('<iiQ', precision, 0, bits))
    assert res == len(text)
    return text


def fmt_ld(v): return vsnprintf('%ld', struct.pack('<i', v))[1]
def fmt_u(v): return vsnprintf('%u', struct.pack('<I', v))[1]


def f32_bits_to_f64_bits(b32):
    return struct.unpack('<Q', struct.pack('<d', struct.unpack('<f', struct.pack('<I', b32))[0]))[0]


def corpus():
    """Deterministic corpus. Each g case: (label, precision, f64 bits, f32 bits or None)."""
    g = []
    def f32(label, b32, prec=6):
        g.append((label, prec, f32_bits_to_f64_bits(b32 & 0xFFFFFFFF), b32 & 0xFFFFFFFF))
    def f64(label, v, prec):
        g.append((label, prec, struct.unpack('<Q', struct.pack('<d', v))[0], None))
    def f64b(label, b, prec): g.append((label, prec, b, None))
    F = lambda x: struct.unpack('<I', struct.pack('<f', x))[0]
    # signed zero, denormals, extremes of f32
    for lab, b in [('+0', 0), ('-0', 0x80000000), ('denorm_min', 1), ('-denorm_min', 0x80000001),
                   ('denorm_max', 0x007FFFFF), ('norm_min', 0x00800000), ('max', 0x7F7FFFFF), ('-max', 0xFF7FFFFF),
                   ('+inf', 0x7F800000), ('-inf', 0xFF800000), ('qnan', 0x7FC00000), ('-qnan', 0xFFC00000),
                   ('snan', 0x7F800001), ('-nan_payload', 0xFFA5A5A5), ('one', 0x3F800000), ('-one', 0xBF800000)]:
        f32(lab, b)
    # powers of ten and the %g exponent switch (P > e >= -4) for P=6
    for e in range(-12, 14):
        for m in (1.0, 9.99999, 9.999995, 5.0, 1.5):
            f32(f'{m}e{e}', F(float(f'{m}e{e}')))
    # halfway cases on the exact binary value (round-half-even), %.6g
    for x in (100000.5, 100001.5, 123456.5, 123457.5, 999999.5, 1000000.5, 99999.95, 0.5, 1.5, 2.5, 8388607.5,
              16777215.0, 16777216.0, 0.000123456, 0.0001, 0.00009999995, 99999.5, 999999.0, 9999995.0, 5e-5, 1e-4,
              1e-5, 123456.0, 1234565.0, 12345.65, 0.1, 0.2, 0.3, 1.0 / 3.0, 2.0 / 3.0, 9.80665, -9.80665, 3.14159274,
              6.02214e23, 1.17549435e-38, 3.40282347e38):
        f32(repr(x), F(x))
    # IMU-like values: gaussians near rest gravity, small gyro noise, large dynamic range
    rnd = random.Random(0x3041)
    for i in range(300): f32(f'accel{i}', F(rnd.gauss(0, 9.8)))
    for i in range(300): f32(f'gyro{i}', F(rnd.gauss(0, 0.02)))
    for i in range(200): f32(f'tiny{i}', F(rnd.gauss(0, 1e-5)))
    for i in range(200): f32(f'wide{i}', F(rnd.choice([-1, 1]) * 10 ** rnd.uniform(-45, 38)))
    # random f32 bit patterns across all exponents (including denormals, inf and NaN patterns)
    for i in range(1200): f32(f'rand{i}', rnd.getrandbits(32))
    # integer-valued and exact decimal-fraction patterns near rounding carries
    for i in range(100): f32(f'int{i}', F(float(rnd.randint(-2**24, 2**24))))
    for i in range(100): f32(f'carry{i}', F(float(rnd.randint(99990, 99999)) * 10 ** rnd.randint(-6, 6)))
    # other precisions (the logger itself only uses the stream default 6)
    for p in (0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 12, 17, 20, 30, 40):
        for v in (0.0, -0.0, 0.5, 0.125, 0.375, 2.5, 1.0, 0.1, 1.0 / 3.0, 9.80665, 1e-5, 1e-4, 123456789.0,
                  1e21, 1e-300, 5e-324, 1.7976931348623157e308, 100.0, 99.5, 0.00001234, 4.35, 1.005):
            f64(f'p{p}', v, p)
    for p in (1, 2, 6, 17):
        for i in range(200): f64b(f'p{p}rand{i}', rnd.getrandbits(64), p)
    # negative precision selects the default 6 (0x816F2..0x816FA)
    f64('negprec', 3.14159, -1)
    ld = [0, 1, -1, 9, 10, 99, 100, 12345, -12345, 2**31 - 1, -2**31, -2**31 + 1, 1000000, -1000000, 255, -32768, 32767]
    ld += [rnd.randint(-2**31, 2**31 - 1) for _ in range(300)]
    ld += [rnd.randint(-32768, 32767) for _ in range(100)]
    u = [0, 1, 9, 10, 99, 100, 2**31, 2**32 - 1, 4294967290, 65535, 65536] + [rnd.getrandbits(32) for _ in range(150)]
    return g, ld, u


def build_fixture():
    g, ld, u = corpus()
    rows = []
    for label, prec, b64, b32 in g:
        rows.append({'fmt': '%.*g', 'label': label, 'precision': prec, 'double_bits': f'{b64:016X}',
                     'f32_bits': None if b32 is None else f'{b32:08X}', 'expected': fmt_g(prec, b64)})
    for v in ld: rows.append({'fmt': '%ld', 'value': v, 'expected': fmt_ld(v)})
    for v in u: rows.append({'fmt': '%u', 'value': v, 'expected': fmt_u(v)})
    return {'libcxx_sha256': EXPECTED_SHA256, 'entry': '0x82528 (vsnprintf) -> 0x813B0 (printf core)',
            'boundary': 'FPSCR round-to-nearest; leaf imports __signbit/__isfinite/strlen/memchr/memcpy/memset only; the limb below the leading limb at the carry loop 0x821E4..0x821F0 is uninitialised stack on the phone and zero here',
            'imports_used': sorted(IMPORTS_USED), 'rows': rows}


def selftest():
    # a few values that follow directly from the printf contract; the corpus itself is the real check.
    assert fmt_g(6, 0x3FE0000000000000) == '0.5'
    assert fmt_g(6, 0x8000000000000000) == '-0'
    assert fmt_ld(-5) == '-5' and fmt_u(4294967295) == '4294967295'
    print('selftest ok')


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('args', nargs='*'); ap.add_argument('--selftest', action='store_true')
    ap.add_argument('--write')
    a = ap.parse_args()
    if a.selftest: selftest()
    elif a.write:
        fx = build_fixture()
        head = {k: v for k, v in fx.items() if k != 'rows'}
        lines = ',\n'.join(json.dumps(r, separators=(',', ':')) for r in fx['rows'])
        Path(a.write).write_text(json.dumps(head, separators=(',', ':'))[:-1] + ',"rows":[\n' + lines + '\n]}\n',
                                 encoding='utf-8', newline='\n')
        print('wrote', a.write, len(fx['rows']), 'rows; imports', fx['imports_used'])
    elif a.args[:1] == ['g']: print(fmt_g(int(a.args[1]), int(a.args[2], 16)))
    elif a.args[:1] == ['ld']: print(fmt_ld(int(a.args[1])))
    else: ap.print_help()
