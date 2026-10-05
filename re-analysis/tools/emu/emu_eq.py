"""Run the engine's own Parametric EQ plug-in code under Unicorn and write the golden values for the B-M6b-4 batch 6d tests (C45; research 20261005-B-M6b-4-bus-fx-17.md, sections 2.1..2.5 and 3 with the Verification's build spec).

    python re-analysis/tools/emu/emu_eq.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseEqOracle.cs      (a few minutes)
    python re-analysis/tools/emu/emu_eq.py --case N        (the long form of life N)

The engine code that runs (no stand-in for it): the parameter object creator 0xAA3178 and its vtable (wrapper 0xAA30CC -> SetParamsBlock 0xAA2E8C or the defaults, SetParam 0xAA2F44, Clone 0xAA2DF4), the plug-in creator 0xAA257C, Init 0xAA24B8, Reset 0xAA2488, Term 0xAA23F0, GetPluginInfo 0xAA244C, Execute
0xAA2A84 with the biquad 0xAA2324, the coefficient routine 0xAA25E0 and the output-gain stage with its NEON lane code. The stand-ins are the plug-in allocator (poisoned with 0xCD) and the phone's libm (float32 correctly rounded, emu_fxlib.py).

A life is one object: rate, the channel word of the Init format, the channel word of the audio state, the failing allocation, whether the instance gets a Clone of the parameter object, the 56-byte block (or the defaults), then ops: R (Reset), E (Execute on a generated buffer), P (SetParam), N (SetParam with a
null value pointer), B (a new block), D (defaults), I (GetPluginInfo), T (Term). The audio comes from the xorshift32 generator of emu_comp.py (Xs in WwiseResamplerTests.cs). A second table, the coefficient routine on its own: "band type gain freq Q rate | the band's five stored words" for all seven types, the cap, and many rates.
"""
import math
import random
import struct
import sys

import numpy as np

sys.dont_write_bytecode = True
import emu_comp
import emu_fxlib
from emu_fxlib import FxEngine, SCRATCH, ALLOC, TANF_ARGS, SINF_ARGS, COSF_ARGS
from emu_comp import fb, ff, sha, cn, cs_strings, RATES, POWF_ARGS, powf, expf, EXPF_ARGS

M32 = 0xFFFFFFFF
NAN = 0x7FC00000


def f32(x):
    return np.float32(x)


def fbits(x):
    return struct.unpack('<I', struct.pack('<f', float(np.float32(x))))[0]


# the field offsets of the parameter object that the C# snapshot prints: (offset, size)
PARAM_FIELDS = [(4, 4), (8, 4), (0xC, 4), (0x10, 4), (0x14, 1), (0x18, 4), (0x1C, 4), (0x20, 4), (0x24, 4), (0x28, 1), (0x2C, 4), (0x30, 4), (0x34, 4), (0x38, 4), (0x3C, 1), (0x40, 4), (0x44, 1), (0x48, 1), (0x49, 1), (0x4A, 1)]


def band(typ, gain, freq, q, on):
    return struct.pack('<I3fB', typ, f32(gain), f32(freq), f32(q), on)


def block(bands, out_db, plfe):
    return b''.join(band(*b) for b in bands) + struct.pack('<fB', f32(out_db), plfe)


def block_words(typ, gain, freq, q, on):
    return struct.pack('<IIIIB', typ, gain, freq, q, on)


# the two shipped blocks (Init.bnk HIRC 0x6767FC1F and 0x174901C6; research section 1)
EQ1 = (band(4, 2.0, 835.0, 2.1, 1) + band(6, -2.5, 1359.0, 4.2, 1) + band(6, -4.0, 5091.0, 1.5, 1)) + struct.pack('<fB', f32(1.5), 0)
EQ2 = (band(1, 0.0, 333.0, 1.0, 1) + band(6, -4.5, 1000.0, 0.5, 0) + band(0, 0.0, 14298.0, 1.0, 1)) + struct.pack('<fB', f32(0.0), 0)
assert len(EQ1) == 56 and len(EQ2) == 56
assert EQ1[0x33:0x37] == bytes.fromhex('0000c03f') and EQ1[0x11:0x15] == bytes.fromhex('06000000')


class Engine(FxEngine):
    def snap_param(self, p):
        e = self.e
        out = []
        for off, size in PARAM_FIELDS:
            out.append('%08X' % e.r32(p + off) if size == 4 else '%02X' % e.r8(p + off))
        return ','.join(out)

    def set_block(self, p, blk):
        e = self.e
        if blk is None:
            return e.call(0xAA30CC, p, ALLOC, 0, 0)
        a = SCRATCH + 0x80
        e.uc.mem_write(a, bytes(blk))
        return e.call(0xAA30CC, p, ALLOC, a, 56)

    def snap_init(self, eq, rc):
        e = self.e
        coefs = ','.join('%08X' % cn(e.r32(eq + 4 + 4 * i)) for i in range(15))
        return '%d %X %X %d %s %s' % (rc, e.r32(eq + 0x44), e.r32(eq + 0x48), 1 if e.r32(eq + 0x4C) else 0, '%08X' % cn(e.r32(eq + 0x50)) if rc == 1 else '0', coefs if rc == 1 else '0')

    def run(self, c):
        e = self.e
        self.begin(c['fail'])
        p0 = e.call(0xAA3178, ALLOC)                       # the parameter object creator
        assert p0
        self.set_block(p0, c['block'])
        if c['clone']:
            assert self.slot(p0, 3) == 0xAA2DF4
            p = e.call(0xAA2DF4, p0, ALLOC)
        else:
            p = p0
        assert p
        eq = e.call(0xAA257C, ALLOC)
        assert eq
        e.uc.mem_write(eq + 4, b'\xcd' * (0x54 - 4))       # nothing but the creator's stores is initialised; poison the rest (the creator wrote [+0x40], [+0x4C] and the vptr)
        e.w32(eq + 0x40, 0)
        e.w32(eq + 0x4C, 0)
        fmt = SCRATCH
        e.uc.mem_write(fmt, struct.pack('<III', c['rate'], c['fmt'], 0x1A2B3C4D))
        rc = e.call(0xAA24B8, eq, ALLOC, 0xCAFE0000, p, stack=(fmt,))
        init = self.snap_init(eq, rc)
        res = []
        for op in c['ops']:
            k = op[0]
            if k == 'R':
                res.append('R:%d' % e.call(0xAA2488, eq))
            elif k == 'E':
                res.append(self.execute(eq, p, c, op))
            elif k == 'P':
                _, pid, val = op
                a = SCRATCH + 0x40
                e.w32(a, val)
                r = e.call(0xAA2F44, p, pid, a)
                res.append('P:%d:%s' % (r, self.snap_param(p)))
            elif k == 'N':
                r = e.call(0xAA2F44, p, op[1], 0)
                res.append('N:%d:%s' % (r, self.snap_param(p)))
            elif k == 'B':
                r = self.set_block(p, op[1])
                res.append('B:%d:%s' % (r, self.snap_param(p)))
            elif k == 'D':
                r = self.set_block(p, None)
                res.append('D:%d:%s' % (r, self.snap_param(p)))
            elif k == 'I':
                info = SCRATCH + 0x300
                e.uc.mem_write(info, b'\xcd' * 16)
                r = e.call(0xAA244C, eq, info)
                res.append('I:%d:%08X,%08X,%02X,%02X' % (r, e.r32(info), e.r32(info + 4), e.r8(info + 8), e.r8(info + 0xA)))
            elif k == 'T':
                n0 = len(self.frees)
                r = e.call(0xAA23F0, eq, ALLOC)
                res.append('T:%d:%d:%d' % (r, len(self.frees) - n0, 1 if e.r32(eq + 0x4C) == 0 else 0))
        return init, ' ; '.join(res)

    def execute(self, eq, p, c, op):
        e = self.e
        _, frames, stride, kind, seed = op
        planes = max(c['fmt'] & 0xFF, c['cfg'] & 0xFF, 1)
        d, n = self.put_audio(kind, seed, planes, stride)
        s = SCRATCH + 0x100
        e.uc.mem_write(s, b'\xcd' * 0x40)
        e.w32(s, d)
        e.w32(s + 4, c['cfg'])
        e.w16(s + 0xC, stride)
        e.w16(s + 0xE, frames)
        estate_before = e.r32(s + 8)
        e.call(0xAA2A84, eq, s)
        out = self.read_audio(d, n)
        coefs = ','.join('%08X' % cn(e.r32(eq + 4 + 4 * i)) for i in range(15))
        st = e.r32(eq + 0x4C)
        nch = e.r32(eq + 0x44)
        if st and nch < 64:
            st_words = [cn(e.r32(st + 4 * i)) for i in range(12 * nch)]
            st_hash = sha(st_words)
        else:
            st_hash = '-'
        dirty = '%02X%02X%02X' % (e.r8(p + 0x48), e.r8(p + 0x49), e.r8(p + 0x4A))
        return 'E:%s:%08X:%s:%s:%s:%d' % (sha(out), cn(e.r32(eq + 0x50)), coefs, st_hash, dirty, 1 if e.r32(s + 8) != estate_before else 0)

    def coef(self, band_i, typ, gain, freq, q, rate):
        """The coefficient routine 0xAA25E0 on its own: the object holds only the rate at +0x48 (the rest zero), the record in the scratch."""
        e = self.e
        eq = e.alloc(0x60)
        e.uc.mem_write(eq, b'\x00' * 0x60)
        e.w32(eq + 0x48, rate)
        rec = SCRATCH + 0x400
        e.uc.mem_write(rec, struct.pack('<IIII', typ, gain, freq, q))
        e.call(0xAA25E0, eq, band_i, rec)
        return ','.join('%08X' % cn(e.r32(eq + 4 + 0x14 * band_i + 4 * i)) for i in range(5)), [e.r32(eq + 4 + 4 * i) for i in range(15)]


# ------------------------------------------------------------------------------------------------ the cases

FRAMES = [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63, 64, 65, 100, 127, 128, 200, 256, 513, 1000, 1024]
GAINS = [-24.0, -12.0, -6.0, -4.5, -4.0, -3.0, -2.5, -1.0, 0.0, 1.0, 2.0, 3.0, 6.0, 9.0, 12.0, 18.0, 24.0]
FREQS = [20.0, 50.0, 100.0, 333.0, 835.0, 1000.0, 1359.0, 2000.0, 3000.0, 5091.0, 8000.0, 10044.0, 14298.0, 16000.0, 20000.0, 21600.0, 22000.0, 24000.0, 30000.0]
QS = [0.1, 0.3, 0.5, 0.7071, 1.0, 1.5, 2.1, 3.0, 4.2, 6.0, 10.0]
OUTS = [-12.0, -6.0, -1.0, 0.0, 0.5, 1.5, 3.0, 6.0, 12.0]
EDGE_F = [0.0, -0.0, -50.0, float('nan'), float('inf'), float('-inf'), 1e-30, 1e10]
EDGE_G = [float('nan'), float('inf'), float('-inf'), 400.0, -400.0, 1e30]
EDGE_Q = [0.0, -1.0, float('nan'), float('inf'), 1e-30, 1e30]
SHIPPED_RATES = [48000, 22320, 44100, 32000, 24000, 16000, 8000, 96000, 22050]
FMTS = [(1, 0x4101), (2, 0x3102), (3, 0x7103), (2, 0x3102 | 0x8000), (1, 0x4101 | 0x8000), (4, 0x3104), (6, 0x3F06 | 0x8000), (1, 0x01)]


def rand_block(rng, edge=False):
    bands = []
    for _ in range(3):
        typ = rng.choice([0, 1, 2, 3, 4, 5, 6, 6, 4, 5])
        gain = rng.choice(GAINS + (EDGE_G if edge else []))
        freq = rng.choice(FREQS + (EDGE_F if edge else []))
        q = rng.choice(QS + (EDGE_Q if edge else []))
        bands.append((typ, gain, freq, q, rng.choice([0, 1, 1, 1, 2])))
    return block(bands, rng.choice(OUTS), rng.choice([0, 0, 1]))


def make_cases():
    rng = random.Random(20261005)
    cases = []

    def case(rate, fmt, cfg, blk, ops, fail=None, clone=1):
        cases.append(dict(rate=rate, fmt=fmt, cfg=cfg, block=blk, ops=ops, fail=fail, clone=clone))

    def E(frames=None, kind=0, seed=None, stride=None):
        frames = frames if frames is not None else rng.choice(FRAMES)
        stride = stride if stride is not None else frames + rng.choice([0, 0, 0, 1, 7, 64])
        return ('E', frames, stride, kind, seed if seed is not None else rng.randrange(1, 1 << 30))

    def P(pid, v):
        return ('P', pid, v)

    # A. the two shipped blocks: every rate, mono / stereo / 3 channels, a gain change in the middle (the NEON ramp), the equal multiply
    for blk in (EQ1, EQ2):
        for rate in SHIPPED_RATES:
            for ch, word in FMTS[:3]:
                case(rate, word, word, blk, [('R',), E(kind=0), E(kind=0), P(15, fbits(3.0)), E(kind=0), E(kind=1), P(15, fbits(0.0)), E(frames=3), E(frames=4)], clone=rng.choice([0, 1]))
    # B. random blocks, random layouts, plain audio / loud, SetParam / block / default ops between Executes
    for _ in range(900):
        ch, word = rng.choice(FMTS)
        cfg = word if rng.random() < 0.85 else rng.choice(FMTS)[1]
        blk = rand_block(rng)
        ops = [('R',), E(kind=rng.choice([0, 0, 1])), E(kind=0)]
        for _ in range(rng.choice([1, 2, 3])):
            kind = rng.choice(['P', 'P', 'B', 'D', 'N'])
            if kind == 'P':
                pid = rng.choice(list(range(17)) + [17, 40])
                if pid in (0, 5, 10):
                    ops.append(P(pid, fbits(rng.choice([0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 4.9, 6.99, 0.5, float('nan')]))))
                elif pid in (1, 6, 11):
                    ops.append(P(pid, fbits(rng.choice(GAINS))))
                elif pid in (2, 7, 12):
                    ops.append(P(pid, fbits(rng.choice(FREQS))))
                elif pid in (3, 8, 13):
                    ops.append(P(pid, fbits(rng.choice(QS))))
                elif pid in (4, 9, 14):
                    ops.append(P(pid, fbits(rng.choice([0.0, 1.0, -0.0, 0.5, float('nan')]))))
                elif pid == 15:
                    ops.append(P(pid, fbits(rng.choice(OUTS))))
                else:
                    ops.append(P(pid, rng.choice([0, 1, 2])))
            elif kind == 'B':
                ops.append(('B', rand_block(rng)))
            elif kind == 'D':
                ops.append(('D',))
            else:
                ops.append(('N', rng.choice([0, 4, 15, 16, 17])))
            ops.append(E(kind=rng.choice([0, 0, 1])))
        case(rng.choice(RATES), word, cfg, blk, ops, clone=rng.choice([0, 1]))
    # C. denormal-scale, denormal, NaN / inf / random patterns, zeros; with an output-level change so the NEON ramp and the equal multiply both see them
    for _ in range(500):
        ch, word = rng.choice(FMTS)
        blk = rand_block(rng)
        kind = rng.choice([2, 2, 3, 3, 4, 5, 5, 6])
        case(rng.choice(RATES), word, word, blk, [('R',), E(kind=kind), P(15, fbits(rng.choice([3.0, 9.0, -9.0]))), E(kind=kind), E(kind=0), E(kind=kind)], clone=rng.choice([0, 1]))
    # D. Init: channel counts 0..6, the LFE flag with ProcessLFE 0 / 1, the failing state allocation, the instance on the creator's object (no Clone)
    for word in (0x00, 0x01, 0x02, 0x8001, 0x8002, 0x8003, 0x4101, 0x3102, 0x7103, 0x3104, 0xBF06):
        for plfe in (0, 1):
            b = bytearray(EQ1)
            b[0x37] = plfe
            for clone in (0, 1):
                if (word & 0xFF) == 0 and (word & 0x8000):
                    continue           # a channel count of 0 with the flag wraps: the engine allocates 2^32 * 0x30 bytes
                case(48000, word, word, bytes(b), [('R',), E(), P(15, fbits(6.0)), E(), ('I',), ('T',)], clone=clone)
                case(48000, word, word, bytes(b), [('R',), ('T',)], fail=(4 if clone else 3), clone=clone)
    # E. the coefficient extremes: zero / negative / NaN / inf frequency, gain and Q; zero and odd rates
    for _ in range(300):
        ch, word = rng.choice(FMTS[:3])
        blk = rand_block(rng, edge=True)
        case(rng.choice(RATES + [0, 1, 1000000]), word, word, blk, [('R',), E(kind=rng.choice([0, 1, 5])), E(kind=0)], clone=rng.choice([0, 1]))
    # F. the defaults (size 0) and the block ops
    for _ in range(60):
        ch, word = rng.choice(FMTS[:3])
        case(rng.choice(RATES), word, word, None, [('R',), E(), ('B', rand_block(rng)), E(), ('D',), E(), ('I',), ('T',)], clone=rng.choice([0, 1]))
    return cases


def make_coef_cases():
    rng = random.Random(20261006)
    rows = []
    rates = SHIPPED_RATES + [11025, 12000, 88200, 192000, 8001, 0, 1, 1000000]
    for typ in range(7):
        for rate in rates:
            for _ in range(24):
                rows.append((rng.randrange(3), typ, fbits(rng.choice(GAINS)), fbits(rng.choice(FREQS)), fbits(rng.choice(QS)), rate))
    # the cap: frequencies at, just under and above 0.45 * fs
    for typ in range(7):
        for rate in SHIPPED_RATES:
            lim = float(np.float32(np.float32(np.float32(rate) * np.float32(0.5)) * np.float32(0.9)))
            for f in (lim, np.nextafter(np.float32(lim), np.float32(0)), np.nextafter(np.float32(lim), np.float32(1e9)), lim * 0.5, lim * 2.0):
                rows.append((rng.randrange(3), typ, fbits(-4.5), fbits(f), fbits(1.5), rate))
    # the shipped bands
    for blk in (EQ1, EQ2):
        for b in range(3):
            typ, gain, freq, q = struct.unpack('<IfffB'[:5], blk[0x11 * b:0x11 * b + 16])
            for rate in SHIPPED_RATES:
                rows.append((b, typ, fbits(gain), fbits(freq), fbits(q), rate))
    # extremes
    for typ in range(7):
        for _ in range(40):
            rows.append((rng.randrange(3), typ, fbits(rng.choice(GAINS + EDGE_G)), fbits(rng.choice(FREQS + EDGE_F)), fbits(rng.choice(QS + EDGE_Q)), rng.choice(rates)))
    return rows


# ------------------------------------------------------------------------------------------------ output

def op_str(op):
    if op[0] == 'E':
        return 'E.%d.%d.%d.%d' % op[1:]
    if op[0] == 'P':
        return 'P.%d.%X' % (op[1], op[2])
    if op[0] == 'N':
        return 'N.%d' % op[1]
    if op[0] == 'B':
        return 'B.' + bytes(op[1]).hex()
    return op[0]


def case_str(c):
    return '%d %X %X %d %d | %s | %s' % (c['rate'], c['fmt'], c['cfg'], c['fail'] or 0, c['clone'], bytes(c['block']).hex() if c['block'] is not None else '-', ' '.join(op_str(o) for o in c['ops']) or '-')


def proof_grid():
    """Every libm argument the EQ can form for the shipped blocks at the rates the stack runs (48000, 22320) and the other realistic rates, with every type at the shipped frequencies."""
    tan, sin, cos, pw = set(), set(), set(), set()
    pi, tpi, k = np.float32(struct.unpack('<f', struct.pack('<I', 0x40490FDB))[0]), np.float32(struct.unpack('<f', struct.pack('<I', 0x40C90FDB))[0]), np.float32(struct.unpack('<f', struct.pack('<I', 0x3CCCCCCD))[0])
    blocks = [EQ1, EQ2]
    freqs = set()
    for blk in blocks:
        for b in range(3):
            typ, gain, freq, q = struct.unpack('<IfffB'[:5], blk[0x11 * b:0x11 * b + 16])
            freqs.add(np.float32(freq))
            pw.add(fbits(np.float32(gain) * k))
    for rate in SHIPPED_RATES + [11025, 12000, 88200]:
        fs = np.float32(rate)
        lim = np.float32(np.float32(fs * np.float32(0.5)) * np.float32(0.9))
        for f in sorted(freqs | {np.float32(x) for x in FREQS}):
            f = min(f, lim) if f < lim else lim
            tan.add(fbits(np.float32(f * pi) / fs))
            w = np.float32(f * tpi) / fs
            sin.add(fbits(w))
            cos.add(fbits(w))
    for g in GAINS:
        pw.add(fbits(np.float32(g) * k))
    for o in OUTS + [1.5]:
        pw.add(fbits(np.float32(o) * np.float32(struct.unpack('<f', struct.pack('<I', 0x3D4CCCCD))[0])))
    return tan, sin, cos, pw


def main():
    cases = make_cases()
    coefs = make_coef_cases()
    eng = Engine()
    rows = []
    for c in cases:
        init, res = eng.run(c)
        rows.append('%s | %s | %s' % (case_str(c), init, res))
    coef_rows = []
    for (b, typ, g, f, q, rate) in coefs:
        words, _ = eng.coef(b, typ, g, f, q, rate)
        coef_rows.append('%d %d %08X %08X %08X %d | %s' % (b, typ, g, f, q, rate, words))
    gt, gs, gc, gp = proof_grid()
    all_t, all_s, all_c, all_p = sorted(TANF_ARGS | gt), sorted(SINF_ARGS | gs), sorted(COSF_ARGS | gc), sorted(POWF_ARGS | gp)
    tan_rows = ['%08X %08X' % (a, emu_fxlib.tanf(a)) for a in all_t]
    sin_rows = ['%08X %08X' % (a, emu_fxlib.sinf(a)) for a in all_s]
    cos_rows = ['%08X %08X' % (a, emu_fxlib.cosf(a)) for a in all_c]
    pow_rows = ['%08X %08X' % (a, powf(0x41200000, a)) for a in all_p]
    worst = emu_fxlib.worst_margin()
    sys.stderr.write('lives %d; coefficient rows %d; tanf %d sinf %d cosf %d powf %d arguments; the smallest distance of an exact result to a binary32 midpoint: %s (units of 2^-53 * result; the argument bits %s)\n' % (
        len(cases), len(coef_rows), len(all_t), len(all_s), len(all_c), len(all_p), '%.3f' % worst[0] if worst else None, '%08X' % worst[1] if worst else None))
    print('// <auto-generated> by re-analysis/tools/emu/emu_eq.py from the engine\'s own output (the real Parametric EQ creator 0xAA257C, Init 0xAA24B8, Reset 0xAA2488, Term 0xAA23F0, Execute 0xAA2A84 with the biquad 0xAA2324 and the coefficient routine 0xAA25E0, and the parameter object 0xAA3178 / 0xAA30CC / 0xAA2E8C / 0xAA2F44 / 0xAA2DF4 under Unicorn): do not edit.')
    print('// The phone\'s tanf / sinf / cosf / powf are float32 CORRECTLY ROUNDED in the emulator run (200-bit mpmath, then integer rounding to 24 bits).')
    print('// Smallest distance of any exact libm result the oracle used to a binary32 rounding midpoint: %s units of 2^-53 * result.' % ('%.3f' % worst[0] if worst else 'n/a'))
    print('namespace Cozmo.Protocol.Tests;')
    print()
    print('internal static class WwiseEqOracle')
    print('{')
    print('    private static void Add(List<string> l, ReadOnlySpan<byte> chunk)')
    print('    {')
    print('        foreach (var row in System.Text.Encoding.UTF8.GetString(chunk).Split(\'\\n\', StringSplitOptions.RemoveEmptyEntries)) l.Add(row);')
    print('    }')
    print()
    print(cs_strings('Cases', 'One EQ life per row: "rate fmtWord cfgWord failAlloc clone | block | ops | init | results". ops: R reset, E.frames.stride.kind.seed execute on a generated buffer (kinds as in emu_comp.py), P.id.valueBits SetParam, N.id SetParam with a null value pointer, B.block a new block, D the defaults, I info, T term. init: "rc +0x44 +0x48 stateAllocated +0x50 coefficients". results per op: E:audioHash:+0x50:coefficients:stateHash:dirtyBytes:estateChanged, R:rc, P/N/B/D:rc:parameterFields, I:rc:info, T:rc:frees:statePointerCleared.', rows))
    print()
    print(cs_strings('Coefficients', 'The coefficient routine 0xAA25E0 on its own: "band type gainBits freqBits qBits rate | the five words the engine stored at this+4+0x14*band".', coef_rows))
    print()
    print(cs_strings('TanfProof', 'tanf: "argument bits correctly rounded result bits" for every argument the EQ forms (the shipped frequencies and a grid at the realistic rates, and every argument the emulator run passed).', tan_rows))
    print()
    print(cs_strings('SinfProof', 'sinf: "argument bits correctly rounded result bits" for every argument the EQ forms.', sin_rows))
    print()
    print(cs_strings('CosfProof', 'cosf: "argument bits correctly rounded result bits" for every argument the EQ forms.', cos_rows))
    print()
    print(cs_strings('PowfProof', 'powf(10.0f, y): "y bits correctly rounded result bits" for the gains (y = dB * 0.025f) and output levels (y = dB * 0.05f) of the shipped blocks, a grid, and every argument the emulator run passed.', pow_rows))
    print('}')
    return cases, rows


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--case':
        cs = make_cases()
        c = cs[int(sys.argv[2])]
        eng = Engine()
        print(case_str(c))
        print(eng.run(c))
    else:
        main()
