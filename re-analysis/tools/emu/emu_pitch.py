"""Run the engine's own pitch node, resampler, voice mix and Hijack code under Unicorn and write the golden values for the B-M6b-4 batch 5g tests (C38).

    python re-analysis/tools/emu/emu_pitch.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwisePitchOracle.cs      (about 4 minutes)
    python re-analysis/tools/emu/emu_pitch.py --node SEED       (the engine's long per-pass form of one voice scenario, for a failing WwisePitchNodeTests row)

The engine code that runs (no stand-in for it): the resampler constructor 0xA46D70, Init 0xA47038, SetPitch 0xA47384, the sibling 0xA4776C, the format change 0xA47528, Execute 0xA47178 with every kernel of the table 0x103C0B8
(0xA479F4, 0xA48F5C, 0xA48C98, 0xA4913C, 0xA49634, 0xA49E40, 0xA4A03C, 0xA4A2D8, 0xA4A59C, 0xA4A958, 0xA4AC04); the voice order 0xA44630 with the pitch node 0xA5321C / 0xA53134 / 0xA52D4C / 0xA52800 (the consumption 0xA52DA8,
the marker carry 0xA5268C, the buffer helpers 0xA69A70 / 0xA69AC8 / 0xA69A38, the pending-source arm 0xA52B90) and 0xA548C0, and the voice-to-bus mix 0xA4FBEC with the matrix mixer 0xA45E9C and the ramped accumulate 0xA46668; and the
Hijack's Thumb Init 0x8DBF76 / Execute 0x8DBFE8 with its core constructor 0x8DBF44. The stand-ins are libc (memcpy, memset), the pool allocators, powf (the phone's libm is not shipped: float32 correctly rounded, which is what the C#
host seam WwiseHostMath.Powf computes), and the callees outside the scope (the source's vt+0xC release / vt+0x28 / vt+0x30, 0xA549A0, the pbi context calls vt+0x24 / vt+0x28 of the pending-source arm, the slot bodies, filter A and B
0xA4C60C, 0xA56E00, the notify 0xA03E8C and the not-ready handler 0xA55C14), each of which logs its call.

The groups written: Init (300), SetPitch (3000 sequences), FormatChange (400), Kernels (400 + 1600 + 1600 and the float 3-channel mode 0), KernelHangs (Execute runs that never end), PowfTable and PowfSensitivity, Node (1600 voice
scenarios of 2..8 passes), Ramp (400), Matrix (300), Mix (300), Hijack (400 lives).

The inputs are drawn from the xorshift32 generator XS below, whose three-line definition is repeated in the C# tests (Xs in WwiseResamplerTests.cs); the oracle stores the seeds, the parameters and the engine's results (state words, a
sha256 prefix of every output buffer). Floats are bit patterns.
"""
import hashlib
import math
import random
import struct
import sys

import numpy as np

sys.dont_write_bytecode = True
from emu_common import Emu, BASE, HEAP, RET, f32, bits
from emu_svc import SvcEmu
from unicorn.arm_const import *

M32 = 0xFFFFFFFF


def fb(x):
    return struct.unpack('<I', struct.pack('<f', np.float32(x)))[0]


def ff(b):
    return struct.unpack('<f', struct.pack('<I', b & M32))[0]


def powf_ref(a, b):
    """powf of the phone's libm (not shipped): float32 correctly rounded, which is what WwiseHostMath.Powf computes (double pow, then rounded to binary32)."""
    if math.isnan(a) or math.isnan(b):
        return float('nan')
    try:
        return float(np.float32(math.pow(a, b)))
    except OverflowError:
        return float('inf')
    except ValueError:
        return float('nan')


class XS:
    """xorshift32, the test generator (WwisePitchTests.Xs): x ^= x << 13; x ^= x >> 17; x ^= x << 5 (32-bit)."""
    def __init__(self, seed):
        self.x = (seed * 2654435761 + 12345) & M32 or 1

    def u32(self):
        x = self.x
        x ^= (x << 13) & M32
        x ^= x >> 17
        x ^= (x << 5) & M32
        self.x = x
        return x

    def f32(self):
        """[-1, 1): int32 / 2^31, exactly representable after the (round-to-nearest) int32 -> float32 conversion."""
        v = self.u32()
        if v >= 0x80000000:
            v -= 1 << 32
        return float(np.float32(np.float32(v) / np.float32(2147483648.0)))

    def s16(self):
        v = self.u32() >> 16
        return v - 65536 if v >= 32768 else v

    def below(self, n):
        return self.u32() % n


class PEmu(SvcEmu):
    """The engine with the stand-ins every oracle of this file needs."""

    def __init__(self):
        super().__init__()
        self.std_hooks()
        self.alloc_calls = 0
        self.fail_at = None
        self.fail_always = False
        self.freed = []
        self.w16(0x1052440, 1024)

        self.powf_ulps = 0

        def powf(em):
            r = fb(powf_ref(ff(em.reg(0)), ff(em.reg(1))))
            return (r + self.powf_ulps) & M32 if self.powf_ulps else r
        self.hook(0x4D6778, powf)

        def memcpy(em):
            d, s, n = em.reg(0), em.reg(1), em.reg(2)
            if n:
                em.uc.mem_write(d, bytes(em.uc.mem_read(s, n)))
            return d
        self.hook(0x4D37F0, memcpy)

        def memset(em):
            d, v, n = em.reg(0), em.reg(1), em.reg(2)
            if n:
                em.uc.mem_write(d, bytes([v & 0xFF]) * n)
            return d
        self.hook(0x4D36DC, memset)

        def pool_alloc(em):            # 0xA7A7F4(pool, size) and 0xA7A894(pool, size, align): the engine's pool allocators
            self.alloc_calls += 1
            if self.fail_always or (self.fail_at is not None and self.alloc_calls == self.fail_at):
                return 0
            return self.alloc(em.reg(1))
        self.hook(0xA7A7F4, pool_alloc)
        self.hook(0xA7A894, pool_alloc)

        def pool_free(em):             # 0xA7A988(pool, ptr) and 0xA7A914(pool, ptr)
            self.freed.append(em.reg(1))
            return None
        self.hook(0xA7A988, pool_free)
        self.hook(0xA7A914, pool_free)

    def rbytes(self, a, n):
        return bytes(self.uc.mem_read(a, n))

    def wbytes(self, a, b):
        self.uc.mem_write(a, bytes(b))

    def wfs(self, a, xs):
        self.wbytes(a, struct.pack('<%df' % len(xs), *[float(np.float32(x)) for x in xs]))

    def sha(self, b):
        return hashlib.sha256(b).hexdigest()[:16]


_E = None


def shared():
    """One emulator for the oracle groups that need no private state (a case allocates from the bump heap and gives it back)."""
    global _E
    if _E is None:
        _E = PEmu()
    return _E


# ------------------------------------------------------------------------------------------------ the resampler object

R_FIELDS = [0x24, 0x28, 0x2C, 0x30, 0x34, 0x38, 0x3C, 0x40, 0x44, 0x48, 0x4C, 0x50]


class Res:
    """The CAkResampler R at a fixed address in emulated memory."""

    def __init__(self, e, addr=None, fresh=True):
        self.e = e
        self.R = addr or e.alloc(0x80)
        self.fmt = e.alloc(0x10)
        if fresh:
            e.uc.mem_write(self.R, bytes(0x80))
            e.call(0xA46D70, self.R)

    def init(self, rate, ch, fmtword, out_rate):
        e = self.e
        e.w32(self.fmt, rate)
        e.w32(self.fmt + 4, ch)
        e.w32(self.fmt + 8, fmtword)
        return e.call(0xA47038, self.R, self.fmt, out_rate) & M32

    def setpitch(self, cbits, flag):
        return self.e.call(0xA47384, self.R, cbits, flag)

    def sibling(self, cbits):
        return self.e.call(0xA4776C, self.R, cbits)

    def snap(self):
        e, R = self.e, self.R
        return [e.r32(R + o) for o in (0x24, 0x28, 0x2C, 0x30, 0x34, 0x38, 0x3C, 0x40, 0x48, 0x4C, 0x50)] + [e.r8(R + 0x44), e.r8(R + 0x54), e.r8(R + 0x55), e.r8(R + 0x56), e.r8(R + 0x57)]

    def hist_len(self):
        ch = self.e.r8(self.R + 0x55)
        return 0x20 if ch <= 8 else ch * (4 if self.e.r8(self.R + 0x54) >= 4 else 2)

    def hist(self):
        return self.e.rbytes(self.e.r32(self.R + 0x20), self.hist_len())

    def set_hist(self, b):
        self.e.wbytes(self.e.r32(self.R + 0x20), b[:self.hist_len()])


def hx(v, n=8):
    return '%0*X' % (n, v & ((1 << (4 * n)) - 1))


def fmtword(is_float, ch):
    """The format word the engine's callers build: 0x10 / 0x20 in the low 6 bits and the block alignment (channels * sample bytes) above them."""
    return (0x20 if is_float else 0x10) | ((ch * (4 if is_float else 2)) << 6)


# ------------------------------------------------------------------------------------------------ Init and SetPitch

RATES = [48000, 48000, 44100, 32000, 24000, 22050, 11025, 8000, 96000, 22320]
OUT_RATES = [48000, 48000, 48000, 22320, 44100, 24000]


def rand_cents(x, last):
    t = x.below(20)
    if last and t < 6:
        return last[x.below(len(last))]
    if t == 0:
        return float(x.below(4801) - 2400)
    if t == 1:
        return float(x.below(4801) - 2400)
    if t == 2:
        return float(x.below(200) - 100) + (0.5 if x.below(2) else 0.0)
    if t == 3:
        return [0.0, -0.0, 1200.0, -1200.0, 100.0, -100.0][x.below(6)]
    if t == 4:
        return [float('inf'), float('-inf'), 1e30, -1e30, 1e-30, -1e-30, 20000.0, -20000.0, 3.4e38][x.below(9)]
    if t == 5:
        return float(np.float32(x.f32() * 2400.0))
    return float(x.below(1601) - 800)


def sp_cases(n_seq=3000):
    """name -> (config tokens, ops tokens, results). op: kind (s SetPitch, b sibling) cents bits flag."""
    out = []
    for seq in range(n_seq):
        x = XS(0x5000 + seq)
        rate, orate = RATES[x.below(len(RATES))], OUT_RATES[x.below(len(OUT_RATES))]
        ch = 1 + x.below(2)
        fl = x.below(2)
        e = shared()
        mark = e.heap
        r = Res(e)
        rc = r.init(rate, ch, fmtword(fl, ch), orate)
        n = 4 + x.below(9)
        last = []
        ops, res = [], []
        for k in range(n):
            if seq % 50 == 7 and k == (seq // 50) % n:
                c = float('nan')
            else:
                c = rand_cents(x, last)
            last.append(c)
            kind = 'b' if x.below(8) == 0 else 's'
            flag = x.below(2)
            cb = fb(c) if not math.isnan(c) else 0x7FC00000
            if kind == 's':
                r.setpitch(cb, flag)
            else:
                r.sibling(cb)
            s = r.snap()
            ops.append('%s%s%d' % (kind, hx(cb), flag))
            # cur tgt cnt mode pitch57
            res.append('%s%s%s%X%s%d' % (hx(s[3]), hx(s[4]), hx(s[5], 3), s[8], hx(s[10]), s[15]))
        out.append(((rate, orate, ch, fl), ' '.join(ops), ' '.join(res), 'rc=%d' % rc))
        e.heap = mark
    return out


def run_limited(e, addr, *args, stack=(), limit=3_000_000):
    """Emu.call with an instruction limit: returns (r0, finished)."""
    uc = e.uc
    from emu_common import STACK, RET
    sp = STACK + 0xE0000
    sp -= 4 * len(stack)
    sp &= ~7
    for i, v in enumerate(stack):
        uc.mem_write(sp + 4 * i, struct.pack('<I', v & M32))
    for i, v in enumerate(args[:4]):
        uc.reg_write(UC_ARM_REG_R0 + i, v & M32)
    uc.reg_write(UC_ARM_REG_SP, sp)
    uc.reg_write(UC_ARM_REG_LR, RET)
    uc.emu_start(addr, RET, count=limit)
    return uc.reg_read(UC_ARM_REG_R0), uc.reg_read(UC_ARM_REG_PC) == RET


def init_cases(n=300):
    """Init 0xA47038 on every channel count 0..12, the formats 0x10, 0x20 and others, with the pool allocation failing or not."""
    out = []
    for k in range(n):
        x = XS(0x9000 + k)
        e = shared()
        mark = e.heap
        rate = [48000, 44100, 32000, 24000, 22050, 1, 8000][x.below(7)]
        orate = [48000, 22320, 44100, 96000, 2000000000, 16000][x.below(6)]
        ch = x.below(13)
        word = [0x10, 0x20, 0x10, 0x20, 0x00, 0x30, 0x3F][x.below(7)] | (x.below(1024) << 6)
        word |= 0xABCD0000 if x.below(2) else 0
        fail = 1 if (ch > 8 and x.below(3) == 0) else 0
        e.fail_always = bool(fail)
        e.alloc_calls = 0
        r = Res(e)
        rc = r.init(rate, ch, word, orate)
        e.fail_always = False
        s = r.snap()
        out.append(((rate, ch, word, orate, fail), 'rc=%d' % rc, ' '.join(hx(v) for v in s)))
        e.heap = mark
    return out


def fmtchange_cases(n=400):
    """0xA47528: the history conversions, the ratio change and the type from the new format."""
    out = []
    for k in range(n):
        x = XS(0xA000 + k)
        e = shared()
        mark = e.heap
        rate, orate = RATES[x.below(len(RATES))], [48000, 48000, 22320, 44100][x.below(4)]
        ch = 1 + x.below(4) if x.below(6) else x.below(9)
        fl = x.below(2)
        r = Res(e)
        r.init(rate, ch, fmtword(fl, ch), orate)
        c0 = rand_cents(x, [])
        flag0 = x.below(2)
        if not math.isnan(c0):
            r.setpitch(fb(c0), flag0)
        # history: random words (floats for the float kernels, int16 pairs for the int16 kernels), the sentinel above the live part
        hist = bytearray()
        for i in range(8):
            if fl:
                hist += struct.pack('<f', float(np.float32(x.f32() * [1.0, 1.0, 1.0, 2.5, 100.0][x.below(5)])))
            else:
                hist += struct.pack('<I', x.u32())
        r.set_hist(bytes(hist))
        rate2 = RATES[x.below(len(RATES))]
        fl2 = [0, 1, 2][x.below(3)]
        word2 = ([0x10, 0x20, 0x00][fl2]) | (x.below(1024) << 6)
        ch2 = ch if x.below(2) else 1 + x.below(2)
        c1 = rand_cents(x, [c0])
        r.e.w32(r.fmt, rate2)
        e.w32(r.fmt + 4, ch2)
        e.w32(r.fmt + 8, word2)
        before = r.snap(), bytes(r.hist())
        rr, fin = run_limited(e, 0xA47528, r.R, r.fmt, fb(c1) if not math.isnan(c1) else 0x7FC00000, 0, stack=(orate,))
        assert fin
        s = r.snap()
        out.append(((rate, orate, ch, fl, ('%X' % fb(c0)) if not math.isnan(c0) else 'N', flag0, bytes(hist).hex(), rate2, word2, ch2, fb(c1) if not math.isnan(c1) else 0x7FC00000),
                    ' '.join(hx(v) for v in s) + ' ' + bytes(r.hist()).hex()))
        e.heap = mark
    return out


STEPS = [0x10000, 0xEB33, 0xAAAB, 0x8000, 0x2268A, 0x1, 0x400, 0x20000, 0x180000, 0x10001, 0xFFFF, 0x5555, 0x30000]
SENTINEL = 0x7FC01234


def kernel_params(mode, kind, seed):
    """kind: i1 (int16 mono), i2 (int16 stereo), f1, f2, f3 (float, 3 channels: mode 0 only). The parameters of one Execute run (xorshift draws, in this order)."""
    x = XS(seed)
    isf = kind[0] == 'f'
    ch = int(kind[1])
    p = {'mode': mode, 'kind': kind, 'seed': seed}
    p['V'] = 1 + x.below(48 if x.below(4) else 8)
    p['A'] = x.below(5)
    p['inMax'] = p['A'] + p['V'] + x.below(3)
    p['L'] = 1 + x.below(64 if x.below(4) else 12)
    p['B'] = x.below(p['L'])
    p['outMax'] = p['L'] + x.below(3)
    p['outV'] = x.below(p['L'])
    p['P'] = [0x10000, 0x10000, x.below(0x30000), x.below(0x10000), 0x20000 + x.below(0x8000)][x.below(5)]
    p['S'] = STEPS[x.below(len(STEPS))] if x.below(2) else 1 + x.below(0x28000)
    p['T'] = STEPS[x.below(len(STEPS))] if x.below(2) else 1 + x.below(0x28000)
    p['C'] = x.below(0x400) if x.below(8) else 0x400 + x.below(0x10)
    p['fp'] = 1 + x.below(2)
    p['cfg'] = ((x.u32() & 0xFFFFFF00) | ch) if x.below(2) else (0x4100 | ch)
    p['dseed'] = x.u32()
    return p


def kernel_run(p):
    """The engine's Execute 0xA47178 for p -> (ret, state tokens); None when the instruction limit is hit (the engine loops forever)."""
    e = shared()
    mark = e.heap
    isf = p['kind'][0] == 'f'
    ch = int(p['kind'][1])
    r = Res(e)
    r.init(48000, ch, fmtword(isf, ch), 48000)
    d = XS(p['dseed'])
    # the history, then the input data
    hist = bytearray()
    for i in range(8):
        hist += struct.pack('<f', d.f32()) if isf else struct.pack('<I', d.u32())
    r.set_hist(bytes(hist))
    n_in = p['inMax'] * ch
    if isf:
        data = b''.join(struct.pack('<f', d.f32()) for _ in range(n_in))
    else:
        data = b''.join(struct.pack('<h', d.s16()) for _ in range(n_in))
    din = e.alloc(len(data) + 16)
    e.wbytes(din, data)
    out_ch = ch
    dout = e.alloc(4 * p['outMax'] * out_ch + 16)
    e.wbytes(dout, struct.pack('<I', SENTINEL) * (p['outMax'] * out_ch))
    IN, OUT = e.alloc(0x30), e.alloc(0x30)
    for buf, dat, cfg, mx, vl in ((IN, din, p['cfg'], p['inMax'], p['V']), (OUT, dout, ch, p['outMax'], p['outV'])):
        e.uc.mem_write(buf, bytes(0x30))
        e.w32(buf, dat); e.w32(buf + 4, cfg); e.w32(buf + 8, 0x2D); e.w16(buf + 0xC, mx); e.w16(buf + 0xE, vl)
    R = r.R
    e.w32(R + 0x24, p['A']); e.w32(R + 0x28, p['B']); e.w32(R + 0x2C, p['P'])
    e.w32(R + 0x30, p['S'] if p['mode'] == 1 else p['S']); e.w32(R + 0x34, p['T'] if p['mode'] == 2 else p['S'])
    e.w32(R + 0x38, p['C']); e.w32(R + 0x3C, p['fp']); e.w32(R + 0x40, p['L']); e.w32(R + 0x48, p['mode'])
    # the output frames already delivered before B keep a recognisable pattern (the kernels write only from B on)
    ret, fin = run_limited(e, 0xA47178, R, IN, OUT)
    if not fin:
        e.heap = mark
        return None
    s = r.snap()
    outb = e.rbytes(dout, 4 * p['outMax'] * out_ch)
    res = ' '.join([hx(ret, 2), hx(s[0]), hx(s[1]), hx(s[2]), hx(s[3]), hx(s[4]), hx(s[5]), '%X' % s[8], hx(e.r16(IN + 0xE), 4), hx(e.r16(OUT + 0xE), 4), r.hist().hex(), hashlib.sha256(outb).hexdigest()[:16]])
    e.heap = mark
    return res


HANGS = []


def kernel_cases(per_kind=400):
    out = []
    kinds0 = ['i1', 'i2', 'f1', 'f2', 'f3']
    kinds12 = ['i1', 'i2', 'f1', 'f2']
    seed = 0xB000
    for mode, kinds, count in ((0, kinds0, per_kind), (1, kinds12, per_kind), (2, kinds12, per_kind)):
        for kind in kinds:
            made = 0
            while made < count:
                seed += 1
                p = kernel_params(mode, kind, seed)
                if mode == 2 and p['fp'] > 2:
                    continue
                res = kernel_run(p)
                if res is None:
                    HANGS.append(p)
                    continue
                out.append((p, res))
                made += 1
    return out




# ------------------------------------------------------------------------------------------------ the pitch node through the voice order 0xA44630

STATE = BASE + 0x1000
VOICE = BASE + 0x2000
SRC1, VT1, PBI1 = BASE + 0x6000, BASE + 0x6100, BASE + 0xA000
SRC2, VT2, PBI2 = BASE + 0x6400, BASE + 0x6500, BASE + 0xB000
FAKE = BASE + 0x8000                       # fake function slots: 8 bytes each (svc stubs)
NODE = VOICE + 0x100
MARKER_PBI = 0x7B000


def node_blocks(spec):
    """spec: 'res/valid/pos/dseed/nmark/mseed,...' -> list of dicts."""
    out = []
    if not spec or spec == '-':
        return out
    for b in spec.split(','):
        res, valid, pos, dseed, nmark, mseed = b.split('/')
        out.append(dict(res=int(res, 16), valid=int(valid), pos=int(pos, 16), dseed=int(dseed, 16), nmark=int(nmark), mseed=int(mseed, 16)))
    return out


def block_data(blk, ch, is_float):
    d = XS(blk['dseed'])
    n = blk['valid'] * ch
    if is_float:
        return b''.join(struct.pack('<f', d.f32()) for _ in range(n))
    return b''.join(struct.pack('<h', d.s16()) for _ in range(n))


def block_markers(blk):
    m = XS(blk['mseed'])
    out = []
    for k in range(blk['nmark']):
        span = max(blk['valid'], 1) + 8
        out.append((MARKER_PBI + k, m.below(span), m.below(100), m.below(1000), m.below(100)))
    return out


class NodeWorld:
    """The engine's voice order 0xA44630 on a hand-built voice with the real pitch node, resampler and 0xA548C0; the source classes, the pbi context calls and 0xA549A0 are logged stand-ins."""

    def __init__(self, p):
        self.p = p
        e = self.e = PEmu()
        self.events = []
        self.next = {SRC1: 0, SRC2: 0}
        self.scripts = {SRC1: node_blocks(p['s1']), SRC2: node_blocks(p['s2'])}
        ch, is_float = p['ch'], p['float']
        for obj, size in ((STATE, 0x40), (VOICE, 0x600), (PBI1, 0x400), (PBI2, 0x400), (SRC1, 0x40), (SRC2, 0x40), (VT1, 0x80), (VT2, 0x80)):
            e.uc.mem_write(obj, bytes(size))
        self.slots = {}

        def fake(name, fn):
            a = FAKE + 8 * len(self.slots)
            self.slots[name] = a
            e.svc_hook(a, fn)
            return a
        self.fake = fake
        # voice, node
        e.w32(STATE + 0x30, VOICE)
        e.w32(VOICE + 0xD4, SRC1)
        e.w32(VOICE + 0xD8, SRC2 if p['pend'] else 0)
        e.w32(VOICE + 0xEC, 48000)
        e.w32(NODE + 4, SRC1)
        e.w32(NODE + 0xB0, VOICE)
        e.call(0xA46D70, NODE + 8)
        e.w32(NODE + 0x68, 0x2B); e.w32(NODE + 0x78, M32); e.w32(NODE + 0x80, M32); e.w32(NODE + 0x84, 1); e.wf(NODE + 0x7C, 1.0)
        e.w32(NODE + 0x90, 0x2B); e.w32(NODE + 0xA0, M32); e.wf(NODE + 0xA4, 1.0); e.w32(NODE + 0xA8, M32); e.w32(NODE + 0xAC, 1)
        # sources
        for src, vt, pbi, nm in ((SRC1, VT1, PBI1, 1), (SRC2, VT2, PBI2, 2)):
            e.w32(src, vt)
            e.w32(src + 0xC, pbi)
            e.w32(vt + 0xC, fake('rel%d' % nm, self.make_rel(nm)))
            e.w32(vt + 0x20, 0xA5668C)
            e.w32(vt + 0x28, fake('start%d' % nm, self.make_start(nm)))
            e.w32(vt + 0x30, fake('s30_%d' % nm, self.make_s30(src, nm)))
            e.w32(pbi, 0x103B768)
            e.w32(pbi + 0x1F8, M32)
            e.w32(pbi + 0x140, 0x4321)
        # the owner PBI of the current source
        e.w32(PBI1 + 0x44, fb(p['pitch']) if not math.isnan(p['pitch']) else 0x7FC00000)
        e.w16(PBI1 + 0x1BE, p['h1be'])
        e.w8(PBI1 + 0x1BD, p['b1bd'])
        e.w32(PBI1 + 0x1B4, p['s1b4'])
        e.w32(PBI1 + 0x1D8, p['d1d8'])
        e.w32(PBI1 + 0x164, p['p164'])
        e.w32(PBI1 + 0x15C, p['w15c'])
        e.w32(VOICE + 0x1B4, PBI1)
        # the node init 0xA5321C(N, F2, pbi, outRate)
        self.fmt = e.alloc(0x10)
        e.w32(self.fmt, p['rate']); e.w32(self.fmt + 4, p['w15c']); e.w32(self.fmt + 8, fmtword(is_float, ch))
        e.call(0xA5321C, NODE, self.fmt, PBI1, 48000)
        self.start_result = 1
        if p['pend']:
            q = p['pend']
            e.w32(PBI2 + 0x44, fb(q['pitch']))
            e.w32(PBI2 + 0x1D8, q['d2']); e.w32(PBI2 + 0x164, q['p2']); e.w32(PBI2 + 0x1DC, 0x11); e.w32(PBI2 + 0x1E0, 0x22)
            e.w32(PBI2 + 0x158, q['rate2']); e.w32(PBI2 + 0x15C, q['w15c2']); e.w32(PBI2 + 0x160, q['fmt2'])
            e.w8(PBI2 + 0xE8, q['e8']); e.w8(PBI2 + 0xE9, q['e9'])
            ctx_vt = e.alloc(0x40)
            e.w32(PBI2 + 0xC, ctx_vt)
            e.w32(ctx_vt + 0x24, fake('ctx24', lambda em: self.events.append('ctx24') or None))
            e.w32(ctx_vt + 0x28, fake('ctx28', lambda em: self.events.append('ctx28') or None))
            self.start_result = q['start']
        self.slot_state = {}
        for i, (r38, r3c) in p['slots'].items():
            obj = BASE + 0x9000 + 0x100 * i
            svt = BASE + 0x9800 + 0x100 * i
            e.w32(obj, svt)
            e.w32(svt + 0x38, fake('slot38_%d' % i, self.make_slot(i, '38', obj)))
            e.w32(svt + 0x3C, fake('slot3c_%d' % i, self.make_slot(i, '3C', obj)))
            e.w32(VOICE + 0x370 + 4 * i, obj)
            self.slot_state[obj] = (i, list(r38), list(r3c))
        self.bus = None
        if p.get('conn'):
            q = p['conn']
            bus = self.bus = e.alloc(0x200)
            e.uc.mem_write(bus, bytes(0x200))
            dd = XS(q['dseed'])
            ddata = self.busdata = e.alloc(4 * 1024 + 64)
            e.wbytes(ddata, b''.join(struct.pack('<f', dd.f32()) for _ in range(1024)))
            e.w16(bus + 0x58, 1024); e.wf(bus + 0x5C, 1.0 / 1024)
            e.w32(bus + 0x60, ddata); e.w32(bus + 0x64, 0x4101); e.w16(bus + 0x6C, 1024); e.w16(bus + 0x6E, 0); e.w32(bus + 0x68, 0x11); e.w32(bus + 0x1BC, q['state'])
            conn = e.alloc(0x80)
            e.uc.mem_write(conn, bytes(0x80))
            for off, g in zip((8, 0xC, 0x10, 0x14), q['gains']):
                e.wf(conn + off, g)
            e.w32(conn + 0x18, 0x1234)             # the descriptor is allocated
            e.w32(conn + 0x30, bus)
            mm = XS(q['mseed'])
            A, B = e.alloc(0x40 * 4), e.alloc(0x40 * 4)
            for mat in (A, B):
                e.uc.mem_write(mat, bytes(0x40 * 4))
                for s_ in range(ch):
                    e.wf(mat + 16 * s_, gain_value(mm))      # the rows are padded to 4 floats (one destination channel)
            e.w32(conn + 0x20, B); e.w32(conn + 0x24, A)
            e.w32(VOICE + 0x28, conn)
        e.svc_hook(0xA549A0, self.switch)
        # the voice-order callees outside the scope
        e.svc_hook(0xA4C60C, lambda em: None)
        e.svc_hook(0xA56E00, lambda em: None)
        e.svc_hook(0xA03E8C, lambda em: self.events.append('notify'))
        e.svc_hook(0xA55C14, lambda em: self.events.append('handler'))
        e.fail_at = p['failat'] or None
        e.alloc_calls = 0

    def make_slot(self, i, kind, obj):
        def f(em):
            _, r38, r3c = self.slot_state[obj]
            lst = r38 if kind == '38' else r3c
            self.events.append('s%s(%d)' % (kind, i))
            self.e.w32(STATE + 0x28, lst.pop(0) if len(lst) > 1 else lst[0])
        return f

    def make_rel(self, nm):
        def f(em):
            self.events.append('rel%d' % nm)
        return f

    def make_start(self, nm):
        def f(em):
            self.events.append('start%d(%d,%d)' % (nm, em.reg(1), em.reg(2)))
            return self.start_result
        return f

    def switch(self, em):
        self.events.append('switch')
        self.e.w32(VOICE + 0xD4, self.e.r32(VOICE + 0xD8))
        self.e.w32(VOICE + 0xD8, 0)                # 0xA549E8..0xA549F8: [voice+0xD4] = [voice+0xD8]; [voice+0xD8] = 0

    def make_s30(self, src, nm):
        def f(em):
            e = self.e
            S = em.reg(1)
            k = self.next[src]
            self.next[src] += 1
            self.events.append('s30_%d(%d)' % (nm, e.r16(S + 0xC)))
            script = self.scripts[src]
            if k >= len(script):
                e.w16(S + 0xE, 0)
                e.w32(S + 0x28, 0x2E)
                return None
            blk = script[k]
            ch, is_float = self.p['ch'], (self.p['float'] if src == SRC1 else self.p['pend']['float2'])
            data = block_data(blk, ch, is_float)
            buf = e.alloc(len(data) + 16)
            e.wbytes(buf, data)
            e.w32(S, buf); e.w32(S + 4, self.p['w15c']); e.w32(S + 8, 0x2D)
            e.w16(S + 0xC, blk['valid']); e.w16(S + 0xE, blk['valid'])
            marks = block_markers(blk)
            e.w16(S + 0x10, len(marks))
            if marks:
                arr = e.alloc(0x14 * len(marks))
                for i, m in enumerate(marks):
                    for j, v in enumerate(m):
                        e.w32(arr + 0x14 * i + 4 * j, v)
                e.w32(S + 0x14, arr)
            else:
                e.w32(S + 0x14, 0)
            e.w32(S + 0x18, blk['pos']); e.w32(S + 0x20, 100000 + k); e.w32(S + 0x24, self.p['rate'])
            e.w32(S + 0x28, blk['res'])
            return None
        return f

    def snapshot(self):
        e = self.e
        S = STATE
        res = e.r32(S + 0x28)
        valid, mx, ch = e.r16(S + 0xE), e.r16(S + 0xC), e.r8(S + 4)
        dptr = e.r32(S)
        dh = '-'
        if dptr and res in (0x2D, 0x11) and valid:
            parts = [e.rbytes(dptr + 4 * mx * c, 4 * valid) for c in range(ch)]
            dh = e.sha(b''.join(parts))
        mc = e.r16(S + 0x10)
        marr = e.r32(S + 0x14)
        marks = ''
        if mc and marr:
            marks = ';'.join('%X.%X.%X.%X.%X' % tuple(e.r32(marr + 0x14 * i + 4 * j) for j in range(5)) for i in range(mc))
        st = ' '.join(hx(v) for v in (res, e.r32(S + 8), e.r32(S + 4), mx, valid, mc, e.r32(S + 0x18), e.r32(S + 0x1C), e.r32(S + 0x20), e.r32(S + 0x24))) + ' ' + dh + ' ' + (marks or '-')
        R = Res(e, NODE + 8, fresh=False)
        held = ' '.join(hx(v) for v in (e.r16(NODE + 0x6E), e.r16(NODE + 0x6C), e.r16(NODE + 0x70), e.r32(NODE + 0x78), e.r32(NODE + 0x68))) + ' d%d' % (1 if e.r32(NODE + 0x60) else 0)
        outs = ' '.join(hx(v) for v in (e.r16(NODE + 0x96), e.r16(NODE + 0x94), e.r16(NODE + 0x98), e.r32(NODE + 0xA0), e.r32(NODE + 0xA4), e.r32(NODE + 0xA8), e.r32(NODE + 0xAC))) + ' d%d' % (1 if e.r32(NODE + 0x88) else 0)
        flags = ' '.join(hx(v, 2) for v in (e.r8(NODE + 0xB8), e.r8(NODE + 0xB9), e.r8(NODE + 0xBA)))
        pbi = ' '.join(hx(v) for v in (e.r32(PBI1 + 0x1B4), e.r8(PBI1 + 0x1BD), e.r8(PBI1 + 0x1BE), e.r32(PBI1 + 0x1D8), e.r32(PBI2 + 0x1D8)))
        links = '%d %d' % (1 if e.r32(NODE + 0xB4) == PBI2 else 0, 2 if e.r32(NODE + 4) == SRC2 else 1)
        busx = ''
        if self.bus:
            b = self.bus
            busx = '%X %X %s %s' % (e.r32(b + 0x68), e.r32(b + 0x1BC), hx(e.r16(b + 0x6E), 4), canon_hash(e, e.rbytes(self.busdata, 4 * 1024)))
        self.long_tail = ' | '.join((held, outs, flags, pbi, links, ' '.join(hx(v) for v in R.snap()), R.hist().hex(), busx))
        return '%s | %s' % (' '.join(self.events), st)

    def run_pass(self):
        e = self.e
        S = STATE
        e.uc.mem_write(S, bytes(0x40))
        e.w32(S + 0x30, VOICE)
        e.w32(S + 8, 0x2B)                         # 0xA44A48 / 0xA44A44: the voice pass block 0xA44A00..0xA44A48
        e.w16(S + 0xC, 1024)                       # 0xA44A40 strh r3,[sp,#0x18]: u16[0x1052440]
        e.w32(S + 0x28, 0x2B)
        self.events = []
        rr, fin = run_limited(e, 0xA44630, S, limit=3_000_000)
        assert fin
        snap = self.snapshot()
        # the release chain after the pull: the node's ReleaseBuffer 0xA52800 (the voice pass calls 0xA5495C)
        e.call(0xA52800, NODE)
        e.w32(S, 0)
        rel = ' '.join(hx(v) for v in (e.r16(NODE + 0x96), e.r16(NODE + 0x94), e.r32(NODE + 0x88), e.r32(NODE + 0x30)))
        self.last_long = snap + ' | ' + self.long_tail + ' | ' + rel
        return snap + ' | ' + hashlib.sha256((self.long_tail + ' | ' + rel).encode()).hexdigest()[:16]


def gen_node(seed):
    x = XS(0xC000 + seed)
    rate = [48000, 48000, 44100, 32000, 24000, 22050][x.below(6)]
    ch = 1 + x.below(2)
    is_float = x.below(5) != 0
    cents = [0.0, 0.0, 0.0, float(x.below(2401) - 1200), 100.0, -50.5, 1200.0][x.below(7)]
    h1be = [0, 0, 0, 0x380, 0x100, 0x80, 0x200][x.below(7)]
    s1b4 = [0, 0, 0, 0, x.below(3000) + 1, x.below(50) + 1][x.below(6)]
    b1bd = (0x80 if x.below(5) == 0 else 0) | 0x44
    p164 = fb([1.0, 1.0, 1.0, 0.5, 2.0, 0.0208333, 3.0][x.below(7)])
    pf = float(np.float32(np.float32(1024.0) * ff(p164)))
    frac = [1.0, 0.5, 0.25, 0.999, 0.1][x.below(5)]
    d1d8 = [-max(1, int(pf * frac)), -max(1, int(pf * frac)), -(x.below(1000) + 1), -(x.below(2200) + 1), -(x.below(40) + 1)][x.below(5)] & M32     # the voice pass keeps D in [-F*P, 0) (0xA55228..0xA55244)
    w15c = (0x4100 | ch) if ch == 1 else 0x3102
    failat = [0, 0, 0, 0, 0, 0, 0, 0, 0, 1 + x.below(6)][x.below(10)]
    npass = 2 + x.below(7)

    def blocks(n):
        out = []
        for k in range(n):
            last = k == n - 1
            valid = [1024, 1024, 1024, 576, 128, 1 + x.below(1024), 0][x.below(7)]
            if last:
                res = 0x11 if x.below(8) else 0x2D
            else:
                res = [0x2D, 0x2D, 0x2D, 0x2E][x.below(4)]
            pos = [0, k * 1024, M32][x.below(3)]
            out.append('%X/%d/%X/%X/%d/%X' % (res, valid, pos, x.u32(), [0, 0, 0, 1, 2, 3][x.below(6)], x.u32()))
        return ','.join(out) or '-'
    s1 = blocks(2 + x.below(6))
    pend = None
    s2 = '-'
    if x.below(2) == 0:
        w15c2 = w15c if x.below(6) else [0x4101, 0x3102, 0x4102, 0x3101, 0x5101][x.below(5)]
        float2 = x.below(2)
        fmt2 = fmtword(float2, ch) | (0 if x.below(2) else (x.below(1024) << 6))
        pend = dict(float2=float2, start=[1, 1, 1, 1, 1, 0x3F, 2, 7][x.below(8)], d2=[0, 0, 0, 0, 0, 0, x.below(2000) + 1][x.below(7)], p2=fb([1.0, 1.0, 0.5, 2.0][x.below(4)]), rate2=[48000, 44100, 32000, 22050][x.below(4)],
                    w15c2=w15c2, fmt2=fmt2, e8=[0, 0x20][x.below(2)], e9=x.below(2), pitch=[0.0, 0.0, float(x.below(1201) - 600)][x.below(3)])
        s2 = blocks(1 + x.below(4))
    slots = {}
    if x.below(4) == 0:
        for i in range(4):
            if x.below(2):
                r3c = [[0x2B, 0x2D, 0x11, 5][x.below(4)] for _ in range(1 + x.below(3))]
                if r3c[-1] == 0x2B:                  # a vt+0x3C that answers 0x2B for ever sends the walk back down for ever (the engine loops too)
                    r3c[-1] = 0x2D
                slots[i] = ([[0x2B, 0x2D, 0x11, 5][x.below(4)] for _ in range(1 + x.below(3))], r3c)
    conn = None
    if x.below(5) == 0:
        g = XS(x.u32())
        conn = dict(state=[1, 4][x.below(2)], gains=[gain_value(g) for _ in range(4)], dseed=x.u32(), mseed=x.u32())
    return dict(seed=seed, rate=rate, ch=ch, float=is_float, pitch=cents, h1be=h1be, s1b4=s1b4, b1bd=b1bd, p164=p164, d1d8=d1d8, w15c=w15c, failat=failat, npass=npass, s1=s1, s2=s2, pend=pend, slots=slots, conn=conn)


def node_row(p):
    q = p['pend']
    head = '%d %d %d %X %X %X %X %X %X %X %d %d %d' % (p['rate'], p['ch'], 1 if p['float'] else 0, fb(p['pitch']), p['h1be'], p['b1bd'], p['s1b4'], p['d1d8'], p['p164'], p['w15c'], p['failat'], p['npass'], 1 if q else 0)
    if q:
        head += ' %X %X %X %X %X %X %X %X %X %d' % (q['start'], q['d2'], q['p2'], q['rate2'], q['w15c2'], q['fmt2'], q['e8'], q['e9'], fb(q['pitch']), q['float2'])
    sl = ';'.join('%d/%s/%s' % (i, '.'.join('%X' % v for v in r38), '.'.join('%X' % v for v in r3c)) for i, (r38, r3c) in sorted(p['slots'].items())) or '-'
    q = p['conn']
    cn = ('%d %X %X %X %X %X %X' % (q['state'], fb(q['gains'][0]), fb(q['gains'][1]), fb(q['gains'][2]), fb(q['gains'][3]), q['dseed'], q['mseed'])) if q else '-'
    return head + ' ~ ' + p['s1'] + ' ~ ' + p['s2'] + ' ~ ' + sl + ' ~ ' + cn


def node_cases(n=1600):
    out = []
    for seed in range(n):
        p = gen_node(seed)
        w = NodeWorld(p)
        passes = []
        for _ in range(p['npass']):
            passes.append(w.run_pass())
        out.append(node_row(p) + ' ## ' + ' ;; '.join(passes))
    return out




# ------------------------------------------------------------------------------------------------ the voice mix 0xA4FBEC, the matrix mixer 0xA45E9C and the ramped accumulate 0xA46668

def canon_hash(e, raw):
    """sha256 prefix of a float32 byte block with every NaN replaced by 0x7FC00000 (the NEON and SSE units may differ in the payload only)."""
    n = len(raw) // 4
    vals = struct.unpack('<%dI' % n, raw)
    fixed = [0x7FC00000 if ((v >> 23) & 0xFF) == 0xFF and (v & 0x7FFFFF) else v for v in vals]
    return hashlib.sha256(struct.pack('<%dI' % n, *fixed)).hexdigest()[:16]


def gain_value(x):
    t = x.below(12)
    if t == 0:
        return 0.0
    if t == 1:
        return -0.0
    if t == 2:
        return 1.0
    if t == 3:
        return 0.5
    if t == 4:
        return float(np.float32(x.f32() * 8.0))
    return float(np.float32(x.f32() * 2.0))


DEN = [0x000116C2, 0x80208F63, 0x00000001, 0x007FFFFF]      # denormal binary32 patterns (NEON flushes them to zero)


def denf(b):
    return ff(b)


def den_sample(d, i, den, phase):
    v = d.f32()
    if den and i % (3 if phase == 0 else 4) == 0:
        return denf(DEN[(i + phase) % 4])
    return v


def ramp_cases(n=400):
    """0xA46668(src, dst, start, inc, frames): the lane-accumulated gain kernel on random sources and destinations. Row: 'frames startBits incBits seed | hash'."""
    out = []
    e = shared()
    for k in range(n):
        x = XS(0xD000 + k)
        frames = [8, 8, 16, 24, 32, 64, 96, 128, 1024][x.below(9)]
        start = gain_value(x)
        inc = gain_value(x) if x.below(4) else 0.0
        if x.below(40) == 0:
            start = float('nan')
        sb = fb(start) if not math.isnan(start) else 0x7FC00000
        ib = fb(inc)
        seed = x.u32()
        den = x.below(2)
        if den:
            if x.below(2):
                sb = DEN[x.below(4)]
            if x.below(2):
                ib = DEN[x.below(4)]
        d = XS(seed)
        mark = e.heap
        src = e.alloc(4 * frames + 64)
        dst = e.alloc(4 * frames + 64)
        e.wbytes(src, b''.join(struct.pack('<f', den_sample(d, i, den, 0)) for i in range(frames)))
        e.wbytes(dst, b''.join(struct.pack('<f', den_sample(d, i, den, 1)) for i in range(frames)))
        rr, fin = run_limited(e, 0xA46668, src, dst, sb, ib, stack=(frames,))
        assert fin
        out.append('%d %X %X %X %d | %s' % (frames, sb, ib, seed, den, canon_hash(e, e.rbytes(dst, 4 * frames))))
        e.heap = mark
    return out


def matrix_params(x, max_ch):
    sch = 1 + x.below(max_ch)
    dch = 1 + x.below(max_ch)
    frames = [8, 16, 24, 64, 128][x.below(5)]
    g0, g1 = gain_value(x), gain_value(x)
    return sch, dch, frames, g0, g1


def build_world_mix(e, p):
    """Buffers and matrices of one mixer scenario: returns the addresses."""
    d = XS(p['seed'])
    sch, dch, frames = p['sch'], p['dch'], p['frames']
    smax = p['smax']
    row = ((dch + 3) >> 2) * 4
    S, D = e.alloc(0x30), e.alloc(0x30)
    sdata = e.alloc(4 * smax * sch + 64)
    ddata = e.alloc(4 * frames * dch + 64)
    den = p.get('den', 0)
    e.wbytes(sdata, b''.join(struct.pack('<f', den_sample(d, i, den, 0)) for i in range(smax * sch)))
    e.wbytes(ddata, b''.join(struct.pack('<f', den_sample(d, i, den, 1)) for i in range(frames * dch)))
    scfg = (0x4100 | sch) if sch == 1 else 0x3102
    dcfg = (0x4100 | dch) if dch == 1 else 0x3102
    e.uc.mem_write(S, bytes(0x30)); e.uc.mem_write(D, bytes(0x30))
    e.w32(S, sdata); e.w32(S + 4, scfg); e.w16(S + 0xC, smax); e.w16(S + 0xE, p['valid'])
    e.w32(D, ddata); e.w32(D + 4, dcfg); e.w16(D + 0xC, frames); e.w16(D + 0xE, 0)
    A = e.alloc(4 * row * sch + 16)
    B = e.alloc(4 * row * sch + 16)
    mats = []
    for mat in (A, B):
        vals = []
        for s_ in range(sch):
            for dd in range(dch):
                vals.append(gain_value(d))
        if den:
            vals[0] = denf(DEN[1])
        mats.append(vals)
        buf = bytearray(4 * row * sch)
        for s_ in range(sch):
            for dd in range(dch):
                struct.pack_into('<f', buf, 4 * (s_ * row + dd), vals[s_ * dch + dd])
        e.wbytes(mat, bytes(buf))
    return S, D, sdata, ddata, A, B, mats


def matrix_cases(n=300):
    """0xA45E9C(S, D, {g0, g1}, A, B, 1/frames, frames) direct. Row: 'seed sch dch frames g0 g1 | D hash'."""
    out = []
    e = shared()
    for k in range(n):
        x = XS(0xE000 + k)
        sch, dch, frames, g0, g1 = matrix_params(x, 2)
        den = x.below(2)
        if den and x.below(2):
            g0 = denf(DEN[x.below(4)])
        p = dict(seed=x.u32(), sch=sch, dch=dch, frames=frames, smax=frames, valid=frames, den=den)
        mark = e.heap
        S, D, sdata, ddata, A, B, mats = build_world_mix(e, p)
        gp = e.alloc(8)
        e.wbytes(gp, struct.pack('<ff', g0, g1))
        rr, fin = run_limited(e, 0xA45E9C, S, D, gp, A, stack=(B, fb(1.0 / frames), frames))
        assert fin
        out.append('%X %d %d %d %X %X %d | %s %s' % (p['seed'], sch, dch, frames, fb(g0), fb(g1), den, canon_hash(e, e.rbytes(ddata, 4 * frames * dch)), hx(e.r16(D + 0xE), 4)))
        e.heap = mark
    return out


def mix_cases(n=300):
    """0xA4FBEC(bus, S, conn, {g0, g1}) with a mono or stereo voice block into a mono bus. Row: 'seed sch frames smax valid state c8 cC c10 c14 g0 g1 | bus68 bus1BC bus6E busHash Svalid Shash'."""
    out = []
    e = shared()
    for k in range(n):
        x = XS(0xF000 + k)
        sch = 1 + x.below(2)
        frames = [8, 16, 24, 64, 1024][x.below(5)]
        smax = [frames, frames, frames + 8 * x.below(3), frames + 8][x.below(4)]
        valid = [0, smax, smax, 1 + x.below(smax), max(1, smax - 8)][x.below(5)]
        state = [1, 4][x.below(2)]
        gains = [gain_value(x) for _ in range(4)]
        g0, g1 = gain_value(x), gain_value(x)
        den = x.below(2)
        if den and x.below(2):
            gains[0] = denf(DEN[x.below(4)])
        p = dict(seed=x.u32(), sch=sch, dch=1, frames=frames, smax=smax, valid=valid, den=den)
        mark = e.heap
        S, D, sdata, ddata, A, B, mats = build_world_mix(e, p)
        bus = e.alloc(0x200)
        e.uc.mem_write(bus, bytes(0x200))
        e.w16(bus + 0x58, frames); e.wf(bus + 0x5C, 1.0 / frames)
        # the bus buffer is the struct at bus+0x60 (D of the matrix mixer): data +0x60, cfg +0x64, max +0x6C
        e.w32(bus + 0x60, ddata); e.w32(bus + 0x64, 0x4101); e.w16(bus + 0x6C, frames); e.w16(bus + 0x6E, 0); e.w32(bus + 0x68, 0x11); e.w32(bus + 0x1BC, state)
        conn = e.alloc(0x80)
        e.uc.mem_write(conn, bytes(0x80))
        for off, g in zip((8, 0xC, 0x10, 0x14), gains):
            e.wf(conn + off, g)
        e.w32(conn + 0x20, B); e.w32(conn + 0x24, A)
        gp = e.alloc(8)
        e.wbytes(gp, struct.pack('<ff', g0, g1))
        rr, fin = run_limited(e, 0xA4FBEC, bus, S, conn, gp)
        assert fin
        out.append('%X %d %d %d %d %d %X %X %X %X %X %X %d | %X %X %s %s %s %s' % (p['seed'], sch, frames, smax, valid, state, fb(gains[0]), fb(gains[1]), fb(gains[2]), fb(gains[3]), fb(g0), fb(g1), den,
                                                                                  e.r32(bus + 0x68), e.r32(bus + 0x1BC), hx(e.r16(bus + 0x6E), 4), canon_hash(e, e.rbytes(ddata, 4 * frames)), hx(e.r16(S + 0xE), 4),
                                                                                  canon_hash(e, e.rbytes(sdata, 4 * smax * sch))))
        e.heap = mark
    return out


# ------------------------------------------------------------------------------------------------ the Hijack's Thumb Init 0x8DBF76 and Execute 0x8DBFE8

def run_thumb(e, addr, *args, limit=4_000_000):
    from emu_common import STACK, RET
    uc = e.uc
    sp = (STACK + 0xE0000) & ~7
    for i, v in enumerate(args[:4]):
        uc.reg_write(UC_ARM_REG_R0 + i, v & M32)
    uc.reg_write(UC_ARM_REG_SP, sp)
    uc.reg_write(UC_ARM_REG_LR, RET)
    uc.emu_start(addr | 1, RET, count=limit)
    return uc.reg_read(UC_ARM_REG_R0), uc.reg_read(UC_ARM_REG_PC) == RET


def hijack_run(seed):
    """One Hijack life: the core ctor 0x8DBF44, Init 0x8DBF76 (22320 Hz, chunk 744) and several Execute 0x8DBFE8 calls on a bus buffer.
    Row: 'rate ch gate | n | (valid, seed)... | init=rc | per call: deliveries "count:hash,..." inValid outValid R'."""
    x = XS(0x1A000 + seed)
    e = PEmu()
    rate = [48000, 48000, 44100, 32000][x.below(4)]
    ch = [1, 1, 1, 2, 0][x.below(5)]
    gate = 0 if x.below(12) == 0 else 1
    nexec = 1 + x.below(8)
    core = e.alloc(0xE0)
    e.uc.mem_write(core, bytes(0xE0))
    events = []

    def process(em):
        # 0x8DC040(functor = core + 0x80, id, data, count)
        data, count = em.reg(2), em.reg(3)
        events.append((count, e.sha(e.rbytes(data, 4 * count)) if count else '-'))
    e.svc_hook(0x8DC040, process, thumb=True)

    def alloc(em):
        return e.alloc(em.reg(1))
    allocator = e.alloc(0x10)
    avt = e.alloc(0x20)
    e.w32(allocator, avt)
    afn = FAKE + 0x400
    e.svc_hook(afn, alloc)
    e.w32(avt + 8, afn)
    # ctor 0x8DBF44(core, id): the id is the object itself; SetupEnginePlugInFx stores the rate and the chunk size
    run_thumb(e, 0x8DBF44, core, core)
    e.w32(core + 4, 22320)
    e.w16(core + 8, 744)
    fmt = e.alloc(0x10)
    e.w32(fmt, rate)
    e.w32(fmt + 4, 0x4100 | ch if ch != 2 else 0x3102)
    e.w32(fmt + 8, fmtword(True, max(ch, 1)))
    if ch == 0:
        e.w32(fmt + 4, 0)
    e.w32(core + 0x90, 1 if gate else 0)
    rc, fin = run_thumb(e, 0x8DBF76, core, allocator, fmt)
    assert fin
    res = ['init=%d' % rc]
    calls = []
    R = Res(e, core + 0x14, fresh=False)
    if rc != 1:
        return '%d %d %d %d | %s' % (rate, ch, gate, 0, res[0]) + ' | ' + R_snap_str(R)
    for k in range(nexec):
        seedk = x.u32()
        valid = [0, 1, 12, 40, 100, 300, 744, 1024, x.below(1025)][x.below(9)]
        d = XS(seedk)
        mark = e.heap
        data = e.alloc(4 * 1024 * ch + 64)
        e.wbytes(data, b''.join(struct.pack('<f', d.f32()) for _ in range(1024 * ch)))
        buf = e.alloc(0x30)
        e.uc.mem_write(buf, bytes(0x30))
        e.w32(buf, data); e.w32(buf + 4, 0x4100 | ch if ch != 2 else 0x3102); e.w32(buf + 8, 0x2D); e.w16(buf + 0xC, 1024); e.w16(buf + 0xE, valid)
        events.clear()
        rc2, fin = run_thumb(e, 0x8DBFE8, core, buf)
        assert fin
        dl = ','.join('%d:%s' % t for t in events) or '-'
        calls.append('%d.%d %s %s %s %s' % (valid, seedk, dl, hx(e.r16(buf + 0xE), 4), hx(e.r16(core + 0x7A), 4), ' '.join(hx(v) for v in R.snap())))
        e.heap = mark
    return '%d %d %d %d | %s | %s' % (rate, ch, gate, nexec, res[0], ' ;; '.join(calls))


def R_snap_str(R):
    return ' '.join(hx(v) for v in R.snap())


def hijack_cases(n=400):
    return [hijack_run(k) for k in range(n)]


# ------------------------------------------------------------------------------------------------ the C# emitter

def cs_strings(name, doc, rows):
    lines = ['    /// <summary>%s</summary>' % doc, '    public static readonly string[] %s =' % name, '    {']
    for r in rows:
        lines.append('        "%s",' % r)
    lines.append('    };')
    return '\n'.join(lines)


def kernel_row(p, res):
    return '%d %s %d %d %d %d %d %d %d %X %X %X %X %d %X %X | %s' % (p['mode'], p['kind'], p['V'], p['A'], p['inMax'], p['L'], p['B'], p['outMax'], p['outV'], p['P'], p['S'] if p['mode'] != 2 else p['S'],
                                                                      p['T'], p['C'], p['fp'], p['cfg'], p['dseed'], res)


SENSITIVE = [-795, -759, -149, -94, -80, 16, 20, 172, 218, 316, 411, 455, 486, 507, 525]
SHIPPED = [-800, -750, -600, -500, -230]


def powf_cases():
    """The powf table of the integer cents -2400..2400 (numpy float32 power, independent of the C# host function) and the engine's step for every sensitive and shipped cents value with the powf result exact, one ulp up and one ulp down (ratio 1.0)."""
    table = []
    for c in range(-2400, 2401):
        q = float(np.float32(np.float32(c) / np.float32(1200.0)))
        table.append('%X %X' % (fb(q), fb(float(np.power(np.float32(2.0), np.float32(q))))))
    e = shared()
    rows = []
    for c in SENSITIVE + SHIPPED:
        steps = []
        for ulps in (0, 1, -1):
            mark = e.heap
            e.powf_ulps = ulps
            r = Res(e)
            r.init(48000, 1, fmtword(0, 1), 48000)
            r.setpitch(fb(float(c)), 0)
            steps.append(r.snap()[3])
            e.heap = mark
        e.powf_ulps = 0
        rows.append('%d %X %X %X' % (c, steps[0], steps[1], steps[2]))
    return table, rows


def emit_resampler(parts):
    table, rows = powf_cases()
    parts.append(cs_strings('PowfTable', 'powf(2, c/1200): "argument bits result bits" for the integer cents -2400..2400 (numpy float32 power).', table))
    parts.append(cs_strings('PowfSensitivity', 'The engine step (SetPitch, flag 0, ratio 1.0) with the powf result exact, +1 ulp and -1 ulp: "cents step step+ step-".', rows))
    parts.append(cs_strings('Init', 'Init 0xA47038: "rate ch fmtword outRate failAlloc | rc=result | R fields (A24 B28 P2C S30 T34 C38 SC3C L40 M48 R4C P50 F44 T54 C55 B56 F57)".',
                            ['%d %d %X %d %d | %s | %s' % (c[0], c[1], c[2], c[3], c[4], r, s) for c, r, s in init_cases()]))
    rows = []
    for (rate, orate, ch, fl), ops, res, rc in sp_cases():
        rows.append('%d %d %d %d | %s | %s | %s' % (rate, orate, ch, fl, rc, ops, res))
    parts.append(cs_strings('SetPitch', 'SetPitch 0xA47384 / sibling 0xA4776C sequences: "rate outRate ch float | rc | ops (s|b, cents bits, flag) | results (cur tgt cnt(3) mode pitch50 f57) per op".', rows))
    rows = []
    for c, res in fmtchange_cases():
        rows.append('%d %d %d %d %s %d %s %d %X %d %X | %s' % (c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7], c[8], c[9], c[10], res))
    parts.append(cs_strings('FormatChange', 'Format change 0xA47528: "rate outRate ch float cents0bits-or-N flag0 hist0 rate2 fmtword2 ch2 cents1bits | R fields | history after".', rows))
    rows = [kernel_row(p, r) for p, r in kernel_cases()]
    parts.append(cs_strings('KernelHangs', 'Execute runs the engine never leaves (its instruction limit was hit): same fields as Kernels, no result.', [kernel_row(p, '') .rstrip(' |').rstrip() for p in HANGS]))
    parts.append(cs_strings('Kernels', 'Execute 0xA47178 with every kernel: "mode kind V A inMax L B outMax outV P S T C fp cfg dseed | ret A B P cur tgt cnt mode inV outV hist outHash".', rows))


def emit_node(parts):
    parts.append(cs_strings('Node', 'The voice order 0xA44630 with the real pitch node: "rate ch float cents h1be b1bd s1b4 d1d8 p164 w15c failAt npasses pend [startResult d2 p2 rate2 w15c2 fmt2 e8 e9 pitch2] ~ source-1 blocks ~ source-2 blocks ## per pass: events | S | sha256-prefix of (held | OUT | flags | pbi | links | R | history | after the release); python emu_pitch.py --node SEED prints the long form". A block is "res/valid/pos/dseed/nmark/mseed"; the slots are "index/results of vt+0x38/results of vt+0x3C".', node_cases()))


def emit_mix(parts):
    parts.append(cs_strings('Ramp', '0xA46668(src, dst, start, inc, frames): "frames startBits incBits seed | hash" (NaNs canonicalised).', ramp_cases()))
    parts.append(cs_strings('Matrix', '0xA45E9C(S, D, {g0, g1}, A, B, 1/frames, frames): "seed sch dch frames g0 g1 | D hash D valid".', matrix_cases()))
    parts.append(cs_strings('Mix', '0xA4FBEC(bus, S, conn, {g0, g1}): "seed sch frames smax valid state c8 cC c10 c14 g0 g1 | bus68 bus1BC bus6E busHash Svalid Shash".', mix_cases()))


def emit_hijack(parts):
    parts.append(cs_strings('Hijack', 'The Hijack core under the real Thumb Init 0x8DBF76 / Execute 0x8DBFE8: "rate ch gate nExec | init | per Execute: valid.seed deliveries(count:hash) inValid outValid R".', hijack_cases()))


def main():
    parts = []
    emit_resampler(parts)
    emit_node(parts)
    emit_mix(parts)
    emit_hijack(parts)
    print('// <auto-generated> by re-analysis/tools/emu/emu_pitch.py from the engine\'s own output (the real pitch node, resampler, mix and Hijack code under Unicorn): do not edit. </auto-generated>')
    print('namespace Cozmo.Protocol.Tests;')
    print()
    print('internal static class WwisePitchOracle')
    print('{')
    print('\n\n'.join(parts))
    print('}')


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--node':
        p = gen_node(int(sys.argv[2]))
        w = NodeWorld(p)
        print(node_row(p))
        for _ in range(p['npass']):
            w.run_pass()
            print(w.last_long)
    else:
        main()
