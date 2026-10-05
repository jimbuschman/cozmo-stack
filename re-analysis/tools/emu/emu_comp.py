"""Run the engine's own Compressor plug-in code under Unicorn and write the golden values for the B-M6b-4 batch 5j tests (C40.6; research live-bodies-10 T-F1, T-F4..T-F7).

    python re-analysis/tools/emu/emu_comp.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseCompressorOracle.cs      (about a minute)
    python re-analysis/tools/emu/emu_comp.py --check                                                                  (the independent float32 model against the emulator, no output file)
    python re-analysis/tools/emu/emu_comp.py --case N                                                                 (the long form of case N)

The engine code that runs (no stand-in for it): the creators 0xAA0538 (the Compressor, 0x3C bytes) and 0xAA0808 (the parameter object, 0x1C bytes), the parameter object's block parse 0xAA07A0 -> 0xAA0734 (the defaults for size 0) and SetParam 0xAA0660, Init
0xA9FB28, Reset 0xA9FA68 and the Execute wrapper 0xA9FC70 with its worker 0xAA0298 (the per-channel worker: mono, and unlinked stereo). The stand-ins are the plug-in allocator (vt+8 / vt+0xC of an object in emulated memory: a bump allocator whose
blocks are poisoned with 0xCD, so a read of uninitialised memory shows) and the phone's libm: expf (PLT 0x4D0058) and powf (PLT 0x4D6778) are not shipped, so both are float32 CORRECTLY ROUNDED here (200-bit mpmath arithmetic, rounded once to 24 bits by
integer arithmetic: independent of any double precision exp), which is what the C# host seam WwiseHostMath.Expf (double Math.Exp rounded once) is proved equal to for every argument this oracle produces (the ExpfProof / PowfProof tables).

A case is one Compressor life: rate, channels, the channel word of the audio state, the 22-byte block (or the defaults), the allocation to fail, then ops: R (Reset), E (Execute on a generated buffer), P (SetParam), B (a new block), D (defaults). The audio inputs are drawn from
the xorshift32 generator XS below (the C# test has the same three lines, Xs in WwiseResamplerTests.cs); the oracle stores the seeds and the engine's results (the output buffer's hash with every NaN canonicalised, the object's coefficients, make-up state and the state pairs).

--check runs a second implementation of the same arithmetic (numpy float32 scalars, written from the disassembly, not from the C#) over every case and compares it with the emulator: every case, so the NaN and denormal groups are included.
"""
import hashlib
import math
import random
import struct
import sys

import numpy as np
from mpmath import mp, mpf, exp as mp_exp, power as mp_power

sys.dont_write_bytecode = True
from emu_common import Emu, BASE, HEAP, RET
from unicorn.arm_const import *

mp.prec = 200
M32 = 0xFFFFFFFF
NAN = 0x7FC00000


def fb(x):
    return struct.unpack('<I', struct.pack('<f', float(np.float32(x))))[0]


def ff(b):
    return struct.unpack('<f', struct.pack('<I', b & M32))[0]


def f32(x):
    return np.float32(x)


def F(b):
    return np.float32(ff(b))


# ------------------------------------------------------------------------------------------------ the correctly rounded libm stand-ins

def round_mpf_to_f32(x):
    """The binary32 bits nearest to the exact mpf x (round half to even, subnormals and overflow handled); integer arithmetic only."""
    if x == 0:
        return 0
    sign = 0x80000000 if x < 0 else 0
    x = abs(x)
    man, exp = int(x.man), int(x.exp)
    e2 = exp + man.bit_length() - 1
    if e2 > 127:
        return sign | 0x7F800000
    unit = -149 if e2 < -126 else e2 - 23
    sh = exp - unit
    if sh >= 0:
        q = man << sh
    else:
        s = -sh
        q = man >> s
        r = man & ((1 << s) - 1)
        half = 1 << (s - 1)
        if r > half or (r == half and (q & 1)):
            q += 1
    if e2 < -126:
        return sign | q                       # subnormal bits (q == 2^23 is the smallest normal)
    if q == 1 << 24:
        q >>= 1
        e2 += 1
        if e2 > 127:
            return sign | 0x7F800000
    return sign | ((e2 + 127) << 23) | (q & 0x7FFFFF)


def midpoint_margin(x):
    """exact value x (> 0, in the normal binary32 range): the distance to the nearest binary32 rounding midpoint, in units of 2^-53 * x (about one double-precision half ulp)."""
    man, exp = int(x.man), int(x.exp)
    e2 = exp + man.bit_length() - 1
    if e2 < -126 or e2 > 127:
        return None
    ulp = mpf(2) ** (e2 - 23)
    k = x / ulp
    frac = k - int(k)
    d = abs(frac - mpf(1) / 2) * ulp
    return float(d / (x * mpf(2) ** -53))


MARGINS = []


def expf_cr(xb):
    """expf of the phone's libm (not shipped): the float32 correctly rounded result of exp(x)."""
    x = ff(xb)
    if math.isnan(x):
        return NAN
    if math.isinf(x):
        return 0x7F800000 if x > 0 else 0
    if x == 0:
        return 0x3F800000
    if x > 90:
        return 0x7F800000
    if x < -110:
        return 0
    v = mp_exp(mpf(x))
    m = midpoint_margin(v)
    if m is not None:
        MARGINS.append((m, xb))
    return round_mpf_to_f32(v)


def powf_cr(ab, bb):
    a, b = ff(ab), ff(bb)
    if math.isnan(a) or math.isnan(b):
        return NAN
    if a == 10.0 and b == 0:
        return 0x3F800000
    if a != 10.0:
        raise AssertionError('the Compressor only calls powf(10, y)')
    if math.isinf(b):
        return 0x7F800000 if b > 0 else 0
    if b > 40:
        return 0x7F800000
    if b < -50:
        return 0
    v = mp_power(mpf(10), mpf(b))
    m = midpoint_margin(v)
    if m is not None:
        MARGINS.append((m, bb))
    return round_mpf_to_f32(v)


EXPF_ARGS = set()
POWF_ARGS = set()

_expf_cache = {}


def expf(xb):
    EXPF_ARGS.add(xb)
    if xb not in _expf_cache:
        _expf_cache[xb] = expf_cr(xb)
    return _expf_cache[xb]


_powf_cache = {}


def powf(ab, bb):
    POWF_ARGS.add(bb)
    if (ab, bb) not in _powf_cache:
        _powf_cache[(ab, bb)] = powf_cr(ab, bb)
    return _powf_cache[(ab, bb)]


# ------------------------------------------------------------------------------------------------ the generator of the inputs

class XS:
    """xorshift32, the test generator (Xs in WwiseResamplerTests.cs): x ^= x << 13; x ^= x >> 17; x ^= x << 5 (32-bit)."""
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
        """int32 / 2^31 (exact after the round-to-nearest int32 -> float32 conversion)."""
        v = self.u32()
        if v >= 0x80000000:
            v -= 1 << 32
        return np.float32(np.float32(v) / np.float32(2147483648.0))


SPECIALS = [0x7FC00000, 0xFFC12345, 0x7F800000, 0xFF800000, 0x00000000, 0x80000000, 0x00000001, 0x80400000, 0x7F7FFFFF, 0x00800000]


def gen_input(kind, seed, count):
    """The float bit patterns of an input buffer of `count` floats. kind 0 audio [-1,1); 1 loud (x8); 2 quiet (x 2^-66, so x*x is denormal-scale); 3 denormal patterns; 4 random bit patterns (NaN, inf, everything); 5 audio with specials; 6 zeros."""
    xs = XS(seed)
    out = []
    for _ in range(count):
        if kind == 0:
            out.append(fb(xs.f32()))
        elif kind == 1:
            out.append(fb(xs.f32() * np.float32(8.0)))
        elif kind == 2:
            out.append(fb(xs.f32() * np.float32(2.0 ** -66)))
        elif kind == 3:
            out.append(xs.u32() & 0x807FFFFF)
        elif kind == 4:
            out.append(xs.u32())
        elif kind == 5:
            u = xs.u32()
            x = fb(xs.f32())
            out.append(SPECIALS[(u >> 8) % len(SPECIALS)] if u % 13 == 0 else x)
        else:
            out.append(0)
    return out


def canon(bits_list):
    return [NAN if (b & 0x7F800000) == 0x7F800000 and (b & 0x7FFFFF) else b for b in bits_list]


def sha(bits_list):
    return hashlib.sha256(struct.pack('<%dI' % len(bits_list), *canon(bits_list))).hexdigest()[:16]


def cn(b):
    """A coefficient bit pattern with a NaN canonicalised (a NaN's payload is not modelled)."""
    return NAN if (b & 0x7F800000) == 0x7F800000 and (b & 0x7FFFFF) else b


# ------------------------------------------------------------------------------------------------ the engine

ALLOC = BASE + 0x00
ALLOC_VT = BASE + 0x40
H_ALLOC = RET + 0x100
H_FREE = RET + 0x104
SCRATCH = BASE + 0x200


class Engine:
    def __init__(self):
        e = self.e = Emu()
        e.std_hooks()

        def cb_expf(em):
            return expf(em.reg(0))
        e.hook(0x4D0058, cb_expf)

        def cb_powf(em):
            return powf(em.reg(0), em.reg(1))
        e.hook(0x4D6778, cb_powf)

        self.calls = 0
        self.fail_at = None

        def cb_alloc(em):
            self.calls += 1
            if self.fail_at == self.calls:
                return 0
            size = em.reg(1)
            a = em.alloc(size)
            em.uc.mem_write(a, b'\xcd' * max(size, 1))
            return a
        e.hook(H_ALLOC, cb_alloc)
        e.hook(H_FREE, lambda em: 1)
        e.w32(ALLOC, ALLOC_VT)
        e.w32(ALLOC_VT + 8, H_ALLOC)
        e.w32(ALLOC_VT + 12, H_FREE)

    def run(self, c):
        """One case -> the result row. Returns (row, model_inputs)."""
        e = self.e
        e.heap = HEAP
        self.calls = 0
        self.fail_at = c['fail']
        params = e.call(0xAA0808, ALLOC)
        assert params
        res = []
        # the block: defaults for the empty block, else the 22 bytes through 0xAA07A0 (size 22) -> vt+0x18 = 0xAA0734
        self.set_block(params, c['block'], res=None)
        comp = e.call(0xAA0538, ALLOC)
        assert comp
        e.uc.mem_write(comp + 4, b'\xcd' * (0x3C - 4))      # nothing but the creator's stores is initialised; poison the rest (the creator wrote only +4, +8, +0xC, +0x24)
        e.w32(comp + 4, 0)
        e.w32(comp + 8, 0)
        e.w32(comp + 0xC, 0)
        e.w32(comp + 0x24, 0)
        fmt = SCRATCH
        e.uc.mem_write(fmt, struct.pack('<III', c['rate'], c['ch'], 0x1A2B3C4D))
        rc = e.call(0xA9FB28, comp, ALLOC, 0xCAFE0000, params, stack=(fmt,))
        init = self.snap_init(comp, rc)
        if rc != 1:
            return init, ''
        for op in c['ops']:
            k = op[0]
            if k == 'R':
                res.append('R:%d' % e.call(0xA9FA68, comp))
            elif k == 'E':
                res.append(self.execute(comp, c, op))
            elif k == 'P':
                _, pid, val = op
                a = SCRATCH + 0x40
                e.w32(a, val)
                r = e.call(0xAA0660, params, pid, a, 4)
                res.append('P:%d:%s' % (r, self.params_snap(params)))
            elif k == 'B':
                r = self.set_block(params, op[1], res=None)
                res.append('B:%d:%s' % (r, self.params_snap(params)))
            elif k == 'D':
                r = self.set_block(params, None, res=None)
                res.append('D:%d:%s' % (r, self.params_snap(params)))
        return init, ' ; '.join(res)

    def set_block(self, params, block, res):
        e = self.e
        if block is None:
            return e.call(0xAA07A0, params, 0, 0, 0)
        a = SCRATCH + 0x80
        e.uc.mem_write(a, bytes(block))
        return e.call(0xAA07A0, params, 0, a, 22)

    def params_snap(self, params):
        e = self.e
        return '%08X,%08X,%08X,%08X,%08X,%02X%02X' % tuple([cn(e.r32(params + 4 + 4 * k)) if k == 4 else e.r32(params + 4 + 4 * k) for k in range(5)] + [e.r8(params + 0x18), e.r8(params + 0x19)])

    def snap_init(self, comp, rc):
        e = self.e
        worker = e.r32(comp + 8)
        st = e.r32(comp + 0x24)
        return '%d %X %X %X %X %X %X %X %X %X %X %X %X %d' % (rc, worker, e.r32(comp + 0xC), e.r32(comp + 0x10) if rc == 1 else 0, e.r32(comp + 0x14), e.r32(comp + 0x18), e.r32(comp + 0x1C),
                                                              e.r32(comp + 0x20) if rc == 1 else 0, e.r32(comp + 0x28), e.r32(comp + 0x2C), e.r32(comp + 0x30), e.r32(comp + 0x34), e.r8(comp + 0x38), 1 if st else 0)

    def execute(self, comp, c, op):
        e = self.e
        _, frames, stride, kind, seed = op
        planes = max(c['ch'], c['cfg'] & 0xFF, 1)
        n = planes * stride
        data = gen_input(kind, seed, n)
        d = e.alloc(4 * n + 16)
        e.uc.mem_write(d, struct.pack('<%dI' % n, *data))
        s = SCRATCH + 0x100
        e.uc.mem_write(s, b'\xcd' * 0x40)
        e.w32(s, d)
        e.w32(s + 4, c['cfg'])
        e.w16(s + 0xC, stride)
        e.w16(s + 0xE, frames)
        e.call(0xA9FC70, comp, s)
        out = list(struct.unpack('<%dI' % n, bytes(e.uc.mem_read(d, 4 * n))))
        cnt = e.r32(comp + 0x1C)
        stp = e.r32(comp + 0x24)
        pairs = ','.join('%08X' % cn(e.r32(stp + 4 * i)) for i in range(2 * cnt))
        return 'E:%s:%08X:%08X:%08X:%08X:%08X:%s' % (sha(out), cn(e.r32(comp + 0x10)), cn(e.r32(comp + 0x28)), cn(e.r32(comp + 0x2C)), cn(e.r32(comp + 0x30)), cn(e.r32(comp + 0x34)), pairs)


# ------------------------------------------------------------------------------------------------ the independent float32 model

class Model:
    """The same arithmetic in numpy float32 scalars, written from the disassembly (0xA9FB28, 0xA9FC70, 0xAA0298, 0xAA0660, 0xAA0734)."""
    def __init__(self):
        self.err = np.seterr(all='ignore')

    def block_words(self, block):
        if block is None:
            return [0xC1400000, 0x40800000, 0x3C23D70A, 0x3DCCCCCD, 0x3F800000], 1, 1
        w = list(struct.unpack('<5I', bytes(block[:20])))
        w[4] = powf(0x41200000, fb(F(w[4]) * F(0x3D4CCCCD)))
        return w, block[0x14], block[0x15]

    def run(self, c):
        w, b18, b19 = self.block_words(c['block'])
        P = {'w': w, 'b18': b18, 'b19': b19}
        rate, ch = c['rate'], c['ch']
        o = {}
        o['rate'] = rate
        o['ch'] = ch
        o['lfe'] = b18
        att, rel = F(w[2]), F(w[3])
        o['aset'] = att
        o['acoef'] = F(expf(fb(F(0xC00CCCCD) / (np.float32(rate) * att))))
        o['rset'] = rel
        o['rcoef'] = F(expf(fb(F(0xC00CCCCD) / (np.float32(rate) * rel))))
        m = 1 if ch == 1 else 0
        if m < b19:
            worker, size, count = 'L', 8, 1
        else:
            worker = 'P'
            if b19 == 0:
                size, count = ch * 8, ch
            else:
                size, count = 8, 1
        o['worker'] = worker
        o['count'] = count
        if c['fail'] == 3:
            o['fail'] = True
            return P, o, None
        o['fail'] = False
        o['pcoef'] = F(expf(fb(np.float32(-1.0) / (np.float32(rate) * F(0x3CBE37DF)))))
        o['prev'] = F(w[4])
        o['state'] = [np.float32(0.0)] * (2 * count)
        return P, o, None

    def cn(self, x):
        b = fb(x)
        return cn(b)

    def execute(self, P, o, c, op):
        _, frames, stride, kind, seed = op
        planes = max(c['ch'], c['cfg'] & 0xFF, 1)
        n = planes * stride
        data = [F(b) for b in gen_input(kind, seed, n)]
        if frames:
            w = P['w']
            thr, ratio, att, rel = F(w[0]), F(w[1]), F(w[2]), F(w[3])
            g1 = F(w[4])
            self.worker(o, data, c, frames, stride, thr, ratio, att, rel)
            g0 = o['prev']
            cfg = c['cfg']
            chs = cfg & 0xFF
            if o['lfe'] == 0 and (cfg & 0x8000):
                chs -= 1
            if g0 == g1:
                if chs != 0 and g0 != np.float32(1.0):
                    lr = frames >> 2
                    for ch in range(chs):
                        b = ch * stride
                        for i in range(frames):
                            if i < 4 * lr:
                                data[b + i] = self.z(self.z(data[b + i]) * self.z(g0))
                            else:
                                data[b + i] = data[b + i] * g0
            elif chs != 0:
                lr = frames >> 2
                tail = (g1 - g0) / np.float32(frames)
                for ch in range(chs):
                    b = ch * stride
                    i = 0
                    if lr:
                        d = (g1 - g0) / np.float32(4 * lr)
                        lane = [g0, g0 + d, None, None]
                        lane[2] = d + lane[1]
                        lane[3] = d + lane[2]
                        step = d * np.float32(4.0)
                        for _blk in range(lr):
                            for k in range(4):
                                data[b + i] = self.z(self.z(data[b + i]) * self.z(lane[k]))
                                i += 1
                            for k in range(4):
                                lane[k] = self.z(self.z(lane[k]) + self.z(step))
                    gain = g0
                    while i < frames:
                        data[b + i] = data[b + i] * gain
                        gain = gain + tail
                        i += 1
            o['prev'] = g1
        out = [fb(x) for x in data]
        st = ','.join('%08X' % self.cn(x) for x in o['state'])
        return 'E:%s:%08X:%08X:%08X:%08X:%08X:%s' % (sha(out), self.cn(o['prev']), self.cn(o['aset']), self.cn(o['acoef']), self.cn(o['rset']), self.cn(o['rcoef']), st)

    @staticmethod
    def z(x):
        x = np.float32(x)
        if x != 0 and abs(x) < np.float32(1.1754944e-38) and not np.isnan(x):
            return np.float32(-0.0) if np.signbit(x) else np.float32(0.0)
        return x

    def worker(self, o, data, c, frames, stride, thr, ratio, att, rel):
        one = np.float32(1.0)
        slope = np.float32(one / ratio) - one
        if not (att == o['aset']):
            o['aset'] = att
            o['acoef'] = F(expf(fb(F(0xC00CCCCD) / (att * np.float32(o['rate'])))))
        if not (rel == o['rset']):
            o['rset'] = rel
            o['rcoef'] = F(expf(fb(F(0xC00CCCCD) / (rel * np.float32(o['rate'])))))
        nch = o['ch']
        if (c['cfg'] & 0x8000) and o['lfe'] == 0:
            nch -= 1
        tiny, third, c127, ln2, l10 = F(0x15F79688), F(0x3EAAAAAB), np.float32(127.0), F(0x3F317218), F(0x3EDE5BD9)
        p05, m37, sc, base, c6, c7, c8 = F(0x3D4CCCCD), F(0xC2140000), F(0x4BD49A78), F(0x4E7E0000), F(0x3EA67F46), F(0x3CAA70DE), F(0x3F272DDB)
        for ch in range(nch):
            g = o['state'][2 * ch]
            p = o['state'][2 * ch + 1]
            b = ch * stride
            for i in range(frames):
                x = data[b + i]
                q = tiny + np.float32(x * x)
                p = q + np.float32((p - q) * o['pcoef'])
                bb = fb(p)
                e = (bb >> 23) & 0xFF
                m = F((bb & 0x7FFFFF) + 0x3F800000)
                t = (m - one) / (m + one)
                t2 = t * t
                ef = np.float32(e) - c127
                poly = one + np.float32(t2 * third)
                s_ = np.float32(ef * ln2) + np.float32(np.float32(t + t) * poly)
                lg = s_ * l10
                over = np.float32(lg * np.float32(10.0)) - thr
                if not (over > 0):
                    over = np.float32(0.0)
                rise = over - g
                fall = g - over
                a = o['acoef'] if rise >= 0 else o['rcoef']
                g = over + np.float32(a * fall)
                y = np.float32(g * slope) * p05
                if y < m37:
                    lin = np.float32(0.0)
                else:
                    word = base + np.float32(y * sc)
                    if np.isnan(word) or word <= 0:
                        u = 0
                    elif word >= np.float32(4294967296.0):
                        u = M32
                    else:
                        u = int(word)
                    m2 = F((u & 0x7FFFFF) + 0x3F800000)
                    f7 = c7 + np.float32(m2 * c6)
                    f8 = c8 + np.float32(m2 * f7)
                    lin = f8 * F((u >> 23) << 23)
                data[b + i] = x * lin
            o['state'][2 * ch] = g
            o['state'][2 * ch + 1] = p


def model_row(m, c):
    """The model's init and op results in the emulator's format (None for an op the model does not predict)."""
    P, o, _ = m.run(c)
    res = []
    if o['fail']:
        return None, None
    for op in c['ops']:
        if op[0] == 'E':
            res.append(m.execute(P, o, c, op))
        elif op[0] == 'R':
            o['state'] = [np.float32(0.0)] * len(o['state'])
            res.append('R:1')
        elif op[0] == 'P':
            _, pid, val = op
            if pid <= 3:
                P['w'][pid] = val
            elif pid == 4:
                P['w'][4] = powf(0x41200000, fb(F(val) * F(0x3D4CCCCD)))
            elif pid == 5:
                P['b18'] = val & 0xFF
            else:
                P['b19'] = val & 0xFF
            res.append('P')
        elif op[0] == 'B':
            P['w'] = m.block_words(op[1])[0]
            res.append('B')
        elif op[0] == 'D':
            P['w'] = m.block_words(None)[0]
            res.append('D')
        else:
            res.append(op[0])
    return o, res


# ------------------------------------------------------------------------------------------------ the cases

RATES = [8000, 11025, 12000, 16000, 22050, 22320, 24000, 32000, 44100, 48000, 88200, 96000]
TIMES = [0.0001, 0.0002, 0.0005, 0.001, 0.002, 0.003, 0.005, 0.0075, 0.01, 0.015, 0.02, 0.03, 0.04, 0.05, 0.06, 0.075, 0.08, 0.1, 0.12, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0]
SHIPPED_BLOCKS = [bytes.fromhex(h) for h in (
    '6666bac1000020406f12833a3d0a573e0000e0400101',   # Init.bnk / Cozmo.bnk 0x89DDC03B: -23.3, 2.5, 0.001, 0.21, 7.0, 1, 1
    '33336bc133335340f853e33d3108ac3c000040400101',   # SFX.bnk 0xA43A6A24
    '3333f3c133331340cdcccc3e5c8f423e9a9959400001',   # Cozmo.bnk 0xC596ED5 and four more
    '000018c2000090400ad7a33c0ad7233d0000c0400000',   # 0x33F0F738
    '9a99a9c100009040cdcc4c3d9a99993e000020410000',   # 0x6F61AFF3
    '00001ac2cdcc4c40e17a943e52b89e3e000090400000',   # 0xA7BB7E79
    '00000ac2000090400ad7a33c9a99193e0000f0400000',   # 0xE39FC759
)]
SHIPPED_BLOCKS_NOTE = 'the 12 Compressor ShareSet objects of the shipped banks (Init.bnk 1, SFX.bnk 1, Cozmo.bnk 10; 7 distinct blocks)'
FRAMES = [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63, 64, 65, 100, 127, 128, 200, 256, 513, 1000, 1024]


def block(thr, ratio, att, rel, mk, b18, b19):
    return struct.pack('<5f2B', f32(thr), f32(ratio), f32(att), f32(rel), f32(mk), b18, b19)


def rand_block(rng, edge=False):
    if edge:
        att = rng.choice([0.0, -0.001, 100.0, 1e-9, 1e9, float('nan'), float('inf')])
        rel = rng.choice([0.0, 0.2, -0.5, 50.0, 1e-9])
        ratio = rng.choice([0.0, 1.0, 0.5, 1e-6, 100.0, float('nan'), -2.0])
        thr = rng.choice([-96.0, 0.0, 12.0, -0.0, float('nan'), 1e30])
        mk = rng.choice([0.0, -96.0, 96.0, float('nan'), 1e30, -1e30])
    else:
        att, rel = rng.choice(TIMES), rng.choice(TIMES)
        ratio = rng.choice([1.0, 1.5, 2.0, 2.5, 3.2, 4.0, 4.5, 6.0, 10.0, 20.0, 0.8])
        thr = rng.choice([-60.0, -48.0, -38.5, -30.4, -23.3, -18.0, -12.0, -6.0, -3.0, 0.0])
        mk = rng.choice([0.0, 1.5, 3.0, 3.4, 4.5, 6.0, 7.0, 10.0, 12.0, 24.0, -3.0, -6.0, -12.0])
    return block(thr, ratio, att, rel, mk, rng.choice([0, 1]), rng.choice([0, 1]))


def make_cases():
    rng = random.Random(20261004)
    cases = []

    def case(rate, ch, cfg, blk, ops, fail=None):
        cases.append(dict(rate=rate, ch=ch, cfg=cfg, block=blk, ops=ops, fail=fail))

    def E(frames=None, kind=0, seed=None, stride=None):
        frames = frames if frames is not None else rng.choice(FRAMES)
        stride = stride if stride is not None else frames + rng.choice([0, 0, 0, 1, 7, 64])
        return ('E', frames, stride, kind, seed if seed is not None else rng.randrange(1, 1 << 30))

    def P(pid, v):
        return ('P', pid, v)

    # A. mono, the shipped blocks, every rate in turn, plain audio (the main path: 1872 + 5 Sounds)
    for blk in SHIPPED_BLOCKS:
        for rate in RATES:
            for _ in range(2):
                case(rate, 1, 1, blk, [('R',), E(kind=0), E(kind=0), E(kind=1)])
    # B. mono grid blocks, audio / loud
    for _ in range(700):
        blk = rand_block(rng)
        case(rng.choice(RATES), 1, 1, blk, [('R',), E(kind=rng.choice([0, 0, 1])), E(kind=0), E(kind=rng.choice([0, 1]))])
    # C. make-up changes between Executes: the ramp (lr == 0 and lr > 0), the equal path, attack/release cache refresh, ratio, threshold
    for _ in range(500):
        blk = rand_block(rng)
        pid = rng.choice([0, 1, 2, 3, 4, 4, 4])
        if pid <= 3:
            newv = fb(f32(rng.choice(TIMES + [-30.0, -12.0, 2.0, 8.0]))) if pid != 1 else fb(f32(rng.choice([1.0, 2.0, 4.0, 8.0])))
        else:
            newv = fb(f32(rng.choice([0.0, 3.0, 6.0, 12.0, -6.0, 24.0])))
        case(rng.choice(RATES), 1, 1, blk, [('R',), E(), P(pid, newv), E(), E(), P(pid, newv), E()])
    # D. denormal-scale inputs (x*x denormal; denormal samples; zeros) with a make-up change so the NEON ramp and the equal multiply both see them
    for _ in range(600):
        blk = rand_block(rng)
        kind = rng.choice([2, 2, 3, 3, 6])
        case(rng.choice(RATES), 1, 1, blk, [('R',), E(kind=kind), P(4, fb(f32(rng.choice([3.0, 9.0, -9.0])))), E(kind=kind), E(kind=0), E(kind=kind)])
    # E. NaN / inf / random bit patterns
    for _ in range(300):
        blk = rand_block(rng)
        kind = rng.choice([4, 5, 5])
        case(rng.choice(RATES), 1, 1, blk, [('R',), E(kind=kind), E(kind=0), P(4, fb(f32(rng.choice([2.0, 5.0])))), E(kind=kind)])
    # F. per-channel stereo and 3 channels (link byte 0), the LFE flag in the channel word
    for _ in range(300):
        ch = rng.choice([2, 2, 3])
        b = bytearray(rand_block(rng))
        b[0x15] = 0
        b[0x14] = rng.choice([0, 1])
        cfg = ch | rng.choice([0, 0, 0x8000])
        case(rng.choice(RATES), ch, cfg, bytes(b), [('R',), E(kind=rng.choice([0, 0, 2, 5])), P(4, fb(f32(6.0))), E(kind=rng.choice([0, 1]))])
    # G. extremes of the coefficients (zero, negative, huge, NaN times; zero and huge rates)
    for _ in range(120):
        blk = rand_block(rng, edge=True)
        case(rng.choice(RATES + [0, 1, 1000000]), 1, 1, blk, [('R',), E(kind=rng.choice([0, 1, 5])), E(kind=0)])
    # H. Init: the mono link byte 1 and 0, channels 0, 2, 3 with the link byte, the failing state allocation (the third allocation of the case)
    for ch in (0, 1, 2, 3):
        for link in (0, 1, 2):
            for lfe in (0, 1):
                b = block(-23.3, 2.5, 0.001, 0.21, 7.0, lfe, link)
                case(48000, ch, ch, b, [], fail=None)
                case(48000, ch, ch, b, [], fail=3)
    # I. the defaults and the block ops between executes
    for _ in range(60):
        case(rng.choice(RATES), 1, 1, None, [('R',), E(), ('B', rand_block(rng)), E(), ('D',), E()])
    return cases


# ------------------------------------------------------------------------------------------------ output

def cs_strings(name, doc, rows):
    lines = ['    /// <summary>%s</summary>' % doc, '    public static readonly string[] %s =' % name, '    {']
    for r in rows:
        lines.append('        "%s",' % r)
    lines.append('    };')
    return '\n'.join(lines)


def op_str(op):
    if op[0] == 'E':
        return 'E.%d.%d.%d.%d' % op[1:]
    if op[0] == 'P':
        return 'P.%d.%X' % (op[1], op[2])
    if op[0] == 'B':
        return 'B.' + bytes(op[1]).hex()
    return op[0]


def case_str(c):
    return '%d %d %X %d | %s | %s' % (c['rate'], c['ch'], c['cfg'], c['fail'] or 0, bytes(c['block']).hex() if c['block'] is not None else '-', ' '.join(op_str(o) for o in c['ops']) or '-')


def proof_grid():
    """Every expf argument the Compressor can form for the realistic rates, the shipped / default / grid times and the power smoothing constant; powf arguments of the shipped and gridded make-up values."""
    args = set()
    for rate in RATES:
        for t in TIMES + [0.001, 0.21, 0.4, 0.19, 0.02, 0.04, 0.05, 0.3, 0.29, 0.31, 0.15, 0.111, 0.021, 0.01, 0.1]:
            tt = f32(t)
            args.add(fb(F(0xC00CCCCD) / (np.float32(rate) * tt)))
        args.add(fb(np.float32(-1.0) / (np.float32(rate) * F(0x3CBE37DF))))
    for blk in SHIPPED_BLOCKS:
        _, _, att, rel, _ = struct.unpack('<5f', blk[:20])
        for rate in RATES:
            args.add(fb(F(0xC00CCCCD) / (np.float32(rate) * np.float32(att))))
            args.add(fb(F(0xC00CCCCD) / (np.float32(rate) * np.float32(rel))))
    pw = set()
    for db in [0.0, 1.5, 3.0, 3.4, 4.5, 6.0, 7.0, 7.5, 10.0, 12.0, 24.0, -3.0, -6.0, -12.0, 9.0, -9.0, 5.0, 2.0, -96.0, 96.0]:
        pw.add(fb(f32(db) * F(0x3D4CCCCD)))
    for blk in SHIPPED_BLOCKS:
        pw.add(fb(np.float32(struct.unpack('<f', blk[16:20])[0]) * F(0x3D4CCCCD)))
    return args, pw


def main():
    cases = make_cases()
    eng = Engine()
    rows = []
    mism = 0
    model = Model()
    for i, c in enumerate(cases):
        init, res = eng.run(c)
        row = '%s | %s | %s' % (case_str(c), init, res)
        rows.append(row)
    # the proof tables: the grid plus every argument the emulator actually passed
    g_exp, g_pow = proof_grid()
    all_exp = sorted(set(EXPF_ARGS) | g_exp)
    all_pow = sorted(set(POWF_ARGS) | g_pow)
    exp_rows = ['%08X %08X' % (a, expf(a)) for a in all_exp]
    pow_rows = ['%08X %08X' % (a, powf(0x41200000, a)) for a in all_pow]
    worst = min(MARGINS) if MARGINS else None
    sys.stderr.write('cases %d; expf arguments %d; powf arguments %d; the smallest distance of an exact result to a binary32 midpoint: %s (units of 2^-53 * result; the argument bits %s)\n' % (
        len(cases), len(all_exp), len(all_pow), '%.3f' % worst[0] if worst else None, '%08X' % worst[1] if worst else None))
    print('// <auto-generated> by re-analysis/tools/emu/emu_comp.py from the engine\'s own output (the real Compressor Init 0xA9FB28, Reset 0xA9FA68, Execute wrapper 0xA9FC70, worker 0xAA0298, creators 0xAA0538 / 0xAA0808 and the parameter object 0xAA0660 / 0xAA07A0 / 0xAA0734 under Unicorn): do not edit.')
    print('// The phone\'s expf / powf are float32 CORRECTLY ROUNDED in the emulator run (200-bit mpmath, then integer rounding to 24 bits).')
    print('// Smallest distance of any exact exp / pow result the oracle used to a binary32 rounding midpoint: %s units of 2^-53 * result.' % ('%.3f' % worst[0] if worst else 'n/a'))
    print('namespace Cozmo.Protocol.Tests;')
    print()
    print('internal static class WwiseCompressorOracle')
    print('{')
    print(cs_strings('Cases', 'One Compressor life per row: "rate ch channelWord failAlloc | block | ops | init | results". ops: R reset, E.frames.stride.kind.seed execute on a generated buffer (kinds 0 audio, 1 loud, 2 quiet (x*x denormal-scale), 3 denormal patterns, 4 random bit patterns, 5 audio with specials, 6 zeros; the generator is the xorshift32 Xs), P.id.valueBits SetParam, B.block a new block, D the defaults. init: "rc worker +C +10 +14 +18 +1C +20 +28 +2C +30 +34 +38 stateAllocated". results per op: E:outputHash:+10:+28:+2C:+30:+34:statePairs, R:rc, P/B/D:rc:parameterWords.', rows))
    print()
    print(cs_strings('ExpfProof', 'expf: "argument bits correctly rounded result bits" for every argument the Compressor forms (the grid of realistic rates and times, the shipped blocks, and every argument the emulator run passed).', exp_rows))
    print()
    print(cs_strings('PowfProof', 'powf(10.0f, y): "y bits correctly rounded result bits" for the make-up values of the shipped blocks and a grid (y = dB * 0.05f).', pow_rows))
    print('}')
    return cases, rows


def check():
    """The independent model against the emulator on every case that has Executes."""
    cases = make_cases()
    eng = Engine()
    model = Model()
    bad = 0
    n = 0
    by_kind = {}
    for i, c in enumerate(cases):
        init, res = eng.run(c)
        if not c['ops'] or init.split()[0] != '1':
            continue
        o, mres = model_row(model, c)
        got = res.split(' ; ')
        for g, m, op in zip(got, mres, c['ops']):
            if op[0] != 'E':
                continue
            n += 1
            by_kind.setdefault(op[3], [0, 0])
            by_kind[op[3]][0] += 1
            if g != m:
                bad += 1
                by_kind[op[3]][1] += 1
                if bad <= 5:
                    print('MISMATCH case %d op %s\n  engine %s\n  model  %s' % (i, op, g, m))
    print('executes compared %d, mismatches %d; by input kind (count, bad): %s' % (n, bad, by_kind))
    return bad


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == '--check':
        sys.exit(1 if check() else 0)
    elif len(sys.argv) == 3 and sys.argv[1] == '--case':
        cs = make_cases()
        c = cs[int(sys.argv[2])]
        eng = Engine()
        print(case_str(c))
        print(eng.run(c))
    else:
        main()
