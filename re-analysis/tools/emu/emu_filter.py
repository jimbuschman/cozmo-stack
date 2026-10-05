"""Run the engine's own voice filter code under Unicorn and write the golden values for the B-M6b-4 batch 6a tests (C43; research 20261005-B-M6b-4-voice-filter-13.md).

    python re-analysis/tools/emu/emu_filter.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseVoiceFilterOracle.cs      (a few minutes)
    python re-analysis/tools/emu/emu_filter.py --case N        (the long form of scenario N)

The engine code that runs, with no stand-in for it: the filter ctor 0xA76280, the init 0xA764D4 (the node's two allocations go through the allocator object at 0x108DA00, whose vt+8 / vt+0xC are the hooks below), the Reset
0xA7666C, the E1 entry 0xA766B8 (which calls the LPF 0xA766F0 and the HPF 0xA77480 on the same pass block S: the design, the ramp, the NEON block kernel, the scalar head and tail and the bypass history copy) and the two
cutoff maps 0xA7A3D8 (LPF) and 0xA7A4AC (HPF). The stand-ins are the pool allocator (a bump allocator whose blocks are poisoned with 0xCD) and the phone's libm: tanf (PLT 0x4AB038) is not shipped, so it is the float32
CORRECTLY ROUNDED tan here (200-bit mpmath, rounded once to 24 bits by integer arithmetic: independent of any double precision tan), which is what the C# host seam WwiseHostMath.Tanf (double Math.Tan rounded once) is proved
equal to for every argument this oracle produces (the TanfProof table). memset (PLT 0x4D36DC), memmove (0x4D667C) and memcpy (0x4D37F0) are Python hooks. NEON runs flush-to-zero with default NaN (Unicorn's own semantics: the engine
never writes FPSCR); the VFP scalar code runs with FPSCR = 0 (FZ = 0), the Android default.

A scenario is one node life: the globals (mix rate u32[0x105243C], ramp chunk u32[0x105244C]), the channel count of the init, the pass block S (channel word, plane stride u16[S+0xC], buffer alignment in bytes), the two band blocks
(cur, target, steps, countdown, dirty, first, bypass), the stored coefficient blocks F (words from the xorshift32 generator) and histories, then ops: P (0xA766B8 on a generated buffer), B (new band blocks), R (the Reset 0xA7666C).
The audio, F and history words come from the xorshift32 generator XS below (Xs in WwiseResamplerTests.cs, gen_input kinds as in emu_comp.py); the oracle stores seeds and the engine's results (the buffer hash with every NaN
canonicalised, the two band blocks, the F words and the histories).
"""
import hashlib
import math
import random
import struct
import sys

from mpmath import mp, mpf, tan as mp_tan

sys.dont_write_bytecode = True
from emu_common import Emu, BASE, HEAP, RET
from emu_comp import XS, gen_input, fb, ff, canon, sha, round_mpf_to_f32, midpoint_margin, cs_strings, NAN
from unicorn.arm_const import *

mp.prec = 200
M32 = 0xFFFFFFFF

NODE = BASE + 0x1000          # 16-byte aligned node (voice+0x1D0 is aligned in the engine: the F blocks use vst1.64 with a 128-bit alignment hint)
SBLK = BASE + 0x2000
H_ALLOC = RET + 0x100
H_FREE = RET + 0x104
ALLOC_VT = BASE + 0x40
G_RATE, G_CHUNK, G_U16 = 0x105243C, 0x105244C, 0x1052440
ALLOCATOR = 0x108DA00
TANF_ARGS = set()
MARGINS = []
_tan_cache = {}


def tanf_cr(xb):
    """tanf of the phone's libm (not shipped): the float32 correctly rounded result."""
    x = ff(xb)
    if math.isnan(x) or math.isinf(x):
        return NAN
    if x == 0:
        return xb
    if xb not in _tan_cache:
        v = mp_tan(mpf(x))
        m = midpoint_margin(abs(v))
        if m is not None:
            MARGINS.append((m, xb))
        _tan_cache[xb] = round_mpf_to_f32(v)
    return _tan_cache[xb]


def tanf(xb):
    TANF_ARGS.add(xb)
    return tanf_cr(xb)


class Engine:
    def __init__(self):
        e = self.e = Emu()
        e.std_hooks()
        e.hook(0x4AB038, lambda em: tanf(em.reg(0)))

        def memset(em):
            d, v, n = em.reg(0), em.reg(1), em.reg(2)
            em.uc.mem_write(d, bytes([v & 0xFF]) * n)
            return d
        e.hook(0x4D36DC, memset)

        def memcpy(em):
            d, s, n = em.reg(0), em.reg(1), em.reg(2)
            em.uc.mem_write(d, bytes(em.uc.mem_read(s, n)))
            return d
        e.hook(0x4D37F0, memcpy)
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
        e.w32(ALLOCATOR, ALLOC_VT)
        e.w32(ALLOC_VT + 8, H_ALLOC)
        e.w32(ALLOC_VT + 12, H_FREE)

    # ------------------------------------------------------------------ helpers
    def node_ctor(self):
        e = self.e
        e.heap = HEAP
        e.uc.mem_write(NODE, b'\xcd' * 0x200)
        e.call(0xA76280, NODE)

    def init(self, word, flag, fail=None):
        self.calls = 0
        self.fail_at = fail
        return self.e.call(0xA764D4, NODE, word, flag)

    def band_set(self, off, b):
        e = self.e
        cur, tgt, steps, cd, dirty, first, byp = b
        e.w32(NODE + off, cur)
        e.w32(NODE + off + 4, tgt)
        e.w16(NODE + off + 8, steps)
        e.w8(NODE + off + 0xA, cd)
        e.w8(NODE + off + 0xB, dirty)
        e.w8(NODE + off + 0xC, first)
        e.w8(NODE + off + 0xD, byp)

    def band_get(self, off):
        e = self.e
        return '%08X:%08X:%X:%X:%X:%X:%X' % (cn(e.r32(NODE + off)), cn(e.r32(NODE + off + 4)), e.r16(NODE + off + 8), e.r8(NODE + off + 0xA), e.r8(NODE + off + 0xB), e.r8(NODE + off + 0xC), e.r8(NODE + off + 0xD))

    def f_words(self, foff):
        e = self.e
        return [e.r32(NODE + foff + 4 * i) for i in range(37)]

    def f_set(self, foff, words):
        for i, w in enumerate(words):
            self.e.w32(NODE + foff + 4 * i, w)

    def hist_words(self, ptr_off, ch):
        e = self.e
        p = e.r32(NODE + ptr_off)
        if p == 0:
            return []
        return [e.r32(p + 4 * i) for i in range(4 * ch)]

    def hist_set(self, ptr_off, words):
        e = self.e
        p = e.r32(NODE + ptr_off)
        for i, w in enumerate(words):
            e.w32(p + 4 * i, w)

    def snap(self, ch, with_data=None):
        parts = []
        if with_data is not None:
            parts.append(sha(with_data))
        parts.append(self.band_get(0x170))
        parts.append(self.band_get(0x180))
        parts.append(sha(self.f_words(0x10) + self.f_words(0xC0)))
        parts.append(sha(self.hist_words(0xB0, ch) + self.hist_words(0x160, ch)))
        return ':'.join(parts)

    # ------------------------------------------------------------------ one scenario
    def run(self, c):
        e = self.e
        ch = c['ch']
        self.node_ctor()
        e.w32(G_RATE, c['rate'])
        e.w32(G_CHUNK, c['chunk'])
        rc = self.init(ch | (c.get('wordhi', 0) << 8), 0)
        assert rc == 1
        out = []
        self.f_set(0x10, gen_input(0, c['fseed'], 37))
        self.f_set(0xC0, gen_input(0, c['fseed'] + 1, 37))
        hk, hs = c['hist']
        if ch:
            self.hist_set(0xB0, gen_input(hk, hs, 4 * ch))
            self.hist_set(0x160, gen_input(hk, hs + 1, 4 * ch))
        self.band_set(0x170, c['lb'])
        self.band_set(0x180, c['hb'])
        for op in c['ops']:
            k = op[0]
            if k == 'P':
                out.append(self.process(c, op))
            elif k == 'B':
                self.band_set(0x170, op[1])
                self.band_set(0x180, op[2])
                out.append('B')
            elif k == 'R':
                e.call(0xA7666C, NODE)
                out.append('R:' + self.snap(ch))
        return ' ; '.join(out)

    def process(self, c, op):
        e = self.e
        _, frames, kind, seed = op
        ch, stride, align = c['ch'], c['stride'], c['align']
        n = ch * stride
        data = gen_input(kind, seed, n)
        d = e.alloc(4 * n + 64)
        e.uc.mem_write(d, b'\xcd' * (4 * n + 64))
        base = d + align
        e.uc.mem_write(base, struct.pack('<%dI' % n, *data))
        e.uc.mem_write(SBLK, b'\xcd' * 0x40)
        e.w32(SBLK, base)
        e.w32(SBLK + 4, ch | (c['cfghi'] << 8))
        e.w16(SBLK + 0xC, stride)
        e.w16(SBLK + 0xE, frames)
        e.call(0xA766B8, NODE, SBLK)
        res = list(struct.unpack('<%dI' % n, bytes(e.uc.mem_read(base, 4 * n)))) if n else []
        return 'P:' + self.snap(ch, res)


def cn(b):
    return NAN if (b & 0x7F800000) == 0x7F800000 and (b & 0x7FFFFF) else b


# ------------------------------------------------------------------------------------------------ the scenarios

F0 = fb(0.0)
BAND_VALUES = [0.0, 0.05, 0.1, 0.100000024, 0.099999994, 1.0, 15.0, 15.0, 30.0, 49.0, 50.0, 99.9, 100.0]


def rand_value(rng, edge=False):
    r = rng.random()
    if r < 0.5:
        return fb(rng.choice(BAND_VALUES))
    if r < 0.9:
        return fb(round(rng.uniform(0, 100), rng.choice([0, 1, 3, 6])))
    if edge or r > 0.97:
        return rng.choice([0x7FC00000, 0xFF800000, 0x7F800000, 0x80000000, 0xC1200000, 0x42C80001, 0x43FA0000, 0x00000001, 0x7F7FFFFF, 0xBF800000])
    return fb(rng.uniform(-5, 110))


def rand_band(rng, edge=False):
    cur = rand_value(rng, edge)
    tgt = rand_value(rng, edge)
    steps = rng.choice([0, 1, 2, 3, 4, 5, 6, 7, 8, 8, 8, 8, 9, 15, 200, 0xFFFF]) if rng.random() < 0.9 else rng.randrange(0, 65536)
    cd = rng.choice([0, 0, 1, 2, 3, 4, 5, 0x7F, 0x80, 0xFF, 0xFE])
    dirty = rng.choice([0, 1, 1, 1, 2, 0xFF])
    first = rng.choice([0, 0, 1])
    byp = rng.choice([0, 0, 1, 1, 7])
    return (cur, tgt, steps, cd, dirty, first, byp)


FRAMES = [0, 1, 2, 3, 4, 5, 7, 8, 31, 32, 33, 100, 127, 128, 129, 200, 255, 256, 257, 511, 512, 777, 1000, 1023, 1024]


def make_cases():
    rng = random.Random(0x43F1)
    cases = []

    def case(**kw):
        c = dict(rate=48000, chunk=128, ch=1, cfghi=0, wordhi=0, stride=1024, align=0, fseed=1, hist=(0, 1), lb=None, hb=None, ops=[])
        c.update(kw)
        # the pass block's valid frames never exceed its plane stride (u16[S+0xE] <= u16[S+0xC]) in the engine
        c['ops'] = [(o[0], min(o[1], c['stride'])) + o[2:] if o[0] == 'P' else o for o in c['ops']]
        cases.append(c)

    def P(frames=None, kind=None, seed=None):
        return ('P', rng.choice(FRAMES) if frames is None else frames, rng.choice([0, 0, 0, 0, 0, 1, 2, 3, 3, 6]) if kind is None else kind, rng.randrange(1, 1 << 20) if seed is None else seed)

    def random_case(i, edge=False):
        ch = rng.choice([0, 1, 1, 2, 2, 3, 4])
        frames_first = rng.choice(FRAMES + [rng.randrange(1, 1025) for _ in range(6)])
        stride = rng.choice([1024, 1024, 1024, 1024, 512, 1000, 1001, 1002, 1003, 2048]) if rng.random() < 0.7 else max(frames_first, rng.randrange(1, 1100))
        stride = max(stride, 1)
        ops = []
        for j in range(rng.choice([1, 1, 2, 2, 3, 4])):
            fr = frames_first if j == 0 else rng.choice(FRAMES + [rng.randrange(1, 1025)])
            fr = min(fr, stride)
            kind = rng.choice([0, 0, 0, 0, 0, 1, 2, 3, 3, 6]) if not (edge or rng.random() < 0.06) else rng.choice([4, 5, 5])
            ops.append(('P', fr, kind, rng.randrange(1, 1 << 20)))
            r = rng.random()
            if r < 0.15:
                ops.append(('B', rand_band(rng), rand_band(rng)))
            elif r < 0.25:
                ops.append(('R',))
        case(rate=rng.choice([48000] * 7 + [44100, 32000, 22050, 8000, 96000]), chunk=rng.choice([128] * 7 + [100, 32, 1, 127, 129, 300]), ch=ch,
             cfghi=rng.choice([0, 0, 0, 0, 0x81, 0xFF]) if rng.random() < 0.1 else 0, stride=stride, align=rng.choice([0] * 7 + [4, 8, 12]),
             fseed=rng.randrange(1, 1 << 16), hist=(rng.choice([0, 0, 3, 6, 0]), rng.randrange(1, 1 << 16)),
             lb=rand_band(rng, edge), hb=rand_band(rng, edge), ops=ops)

    # A. the shipped Sound: target 15.0 -> LPF engaged at the first apply (steps 8, dirty/first set, cur = target = 15), HPF target 0 bypassed, 1024 frames, 1..2 channels, aligned
    for ch in (1, 2):
        for _ in range(4):
            fb15 = fb(15.0)
            case(ch=ch, lb=(0, fb15, 8, 0, 1, 1, 1), hb=(0, 0, 8, 0, 1, 1, 1), fseed=rng.randrange(1, 1 << 16), ops=[('P', 1024, 0, rng.randrange(1, 1 << 20)), ('P', 1024, 0, rng.randrange(1, 1 << 20)), ('P', 1024, 0, rng.randrange(1, 1 << 20))])
    # B. the 0 -> 15 ramp (the setter ran with the init state: cur 0, target 15, steps 8, dirty 1, first 0), then steady, with the chunks 128 / 100 / 32 and frames around the chunk edge
    for chunk in (128, 100, 32, 128, 128):
        for fr in (1024, 128, 127, 129, 300, 1000, 33, 7, 1):
            case(ch=rng.choice([1, 2]), chunk=chunk, lb=(0, fb(15.0), 0, 0, 1, 0, 0), hb=(0, 0, 8, 0, 1, 0, 0), ops=[('P', fr, 0, rng.randrange(1, 1 << 20))] + [('P', fr, 0, rng.randrange(1, 1 << 20)) for _ in range(3)])
    # C. LPF and HPF ramps over many targets and stride / alignment combinations (both bands engaged)
    for _ in range(300):
        cur = fb(rng.choice([0.0, 5.0, 30.0, 50.0, 80.0, 100.0, rng.uniform(0, 100)]))
        tgt = fb(rng.choice([0.0, 0.05, 15.0, 29.0, 30.0, 31.0, 60.0, 100.0, rng.uniform(0, 100)]))
        case(ch=rng.choice([1, 2, 3]), chunk=rng.choice([128, 128, 100, 32, 64]), stride=rng.choice([1024, 1000, 1001]), align=rng.choice([0, 0, 4, 8, 12]),
             rate=rng.choice([48000, 48000, 44100, 8000]), fseed=rng.randrange(1, 1 << 16),
             lb=(cur, tgt, 0, 0, 1, 0, 0), hb=(cur, tgt, 0, 0, 1, 0, 0),
             ops=[('P', rng.choice([1024, 300, 129, 1000, 64]), 0, rng.randrange(1, 1 << 20)) for _ in range(rng.choice([2, 3, 4]))])
    # D. denormal-scale inputs and zeros through the block kernel (the NEON flush-to-zero), steady state with random stored F
    for _ in range(300):
        case(ch=rng.choice([1, 2, 4]), hist=(rng.choice([2, 3, 6]), rng.randrange(1, 1 << 16)), fseed=rng.randrange(1, 1 << 16), stride=rng.choice([1024, 512]),
             lb=(fb(30.0), fb(30.0), 8, 0, 0, 0, 0), hb=(fb(50.0), fb(50.0), 8, 0, 0, 0, 0),
             ops=[('P', rng.choice([1024, 777, 130, 7, 4]), rng.choice([2, 3, 3, 6, 2, 0]), rng.randrange(1, 1 << 20)) for _ in range(rng.choice([1, 2]))])
    # E. the bypass countdown and the steady-state tail: bands with countdown 1..4, bypass copies of the history
    for _ in range(200):
        case(ch=rng.choice([0, 1, 2, 3]), lb=(fb(0.05), fb(0.05), 8, rng.choice([1, 2, 3, 4]), 0, 0, 0), hb=(fb(0.0), fb(0.0), 8, 0, 0, 0, 1),
             ops=[('P', rng.choice([1024, 5, 2, 1, 0]), 0, rng.randrange(1, 1 << 20)) for _ in range(6)])
    # F. random scenarios
    for i in range(1800):
        random_case(i)
    # G. NaN / inf / extreme band values and random bit patterns in the audio
    for i in range(300):
        random_case(i, edge=True)
    # H. NaN / inf cur and target in the ramp and first-apply states (the compares bls / bhi on unordered flags; the map and the design on a NaN value)
    nanv = [0x7FC00000, 0xFFC12345, 0x7F800000, 0xFF800000, 0x7F800001]
    for _ in range(240):
        cur = rng.choice(nanv + [fb(0.0), fb(0.05), fb(15.0), fb(50.0)])
        tgt = rng.choice(nanv + [fb(0.0), fb(0.05), fb(15.0), fb(100.0)])
        steps = rng.choice([0, 0, 3, 8])
        first = rng.choice([0, 0, 1])
        case(ch=rng.choice([1, 2]), chunk=rng.choice([128, 128, 100]), lb=(cur, tgt, steps, rng.choice([0, 2]), 1, first, rng.choice([0, 1])), hb=(cur, tgt, steps, 0, 1, first, 0),
             fseed=rng.randrange(1, 1 << 16), ops=[('P', rng.choice([1024, 300, 129]), 0, rng.randrange(1, 1 << 20)) for _ in range(rng.choice([2, 3]))])
    return cases


def case_str(c):
    def band(b):
        return '%X:%X:%X:%X:%X:%X:%X' % b

    def op(o):
        if o[0] == 'P':
            return 'P.%d.%d.%d' % o[1:]
        if o[0] == 'B':
            return 'B.' + band(o[1]) + '.' + band(o[2])
        return 'R'
    return '%d %d %d %X %d %d %d | %s | %s | %d:%d:%d | %s' % (c['rate'], c['chunk'], c['ch'], c['cfghi'], c['stride'], c['align'], c['fseed'],
                                                               band(c['lb']), band(c['hb']), c['hist'][0], c['hist'][1], 0, ' '.join(op(o) for o in c['ops']))


# ------------------------------------------------------------------------------------------------ ctor / init / map rows

def ctor_row(eng):
    e = eng.e
    eng.node_ctor()
    f_l = eng.f_words(0x10)
    f_h = eng.f_words(0xC0)
    w = [e.r32(NODE + o) for o in (0xB0, 0xB4, 0x160, 0x164)]
    untouched = all(e.r8(NODE + o) == 0xCD for o in range(0x170, 0x190))
    return 'C | %s | %s | %s | %d | %08X | %08X' % (' '.join('%08X' % x for x in f_l), ' '.join('%08X' % x for x in f_h), ' '.join('%08X' % x for x in w), 1 if untouched else 0, e.r32(NODE), e.r32(NODE + 0x190))


def init_rows(eng):
    e = eng.e
    rows = []
    for word in (0, 1, 2, 3, 4, 0x3102, 0x1FF, 0x4101, 0xDEAD0204):
        for flag in (0, 1, 7):
            for fail in (None, 1, 2):
                eng.node_ctor()
                rc = eng.init(word, flag, fail)
                ptrs = (1 if e.r32(NODE + 0xB0) else 0, 1 if e.r32(NODE + 0x160) else 0)
                sizes = (e.r32(NODE + 0xB4), e.r32(NODE + 0x164) if (e.r32(NODE + 0x160)) else 0)
                rows.append('I %X %X %d | %d | %s %s | %d %d | %X %X | %X %X | %s' % (word, flag, fail or 0, rc, eng.band_get(0x170), eng.band_get(0x180), ptrs[0], ptrs[1], sizes[0], sizes[1], e.r32(NODE + 0x190), e.r8(NODE + 0x194),
                                                                                       sha(eng.hist_words(0xB0, word & 0xFF)) if ptrs[0] else '-'))
    # Reset on a node that was never initialised (histories null): only the two first bytes change
    for _ in range(1):
        eng.node_ctor()
        e.call(0xA7666C, NODE)
        rows.append('Z | %X %X | %s' % (e.r8(NODE + 0x17C), e.r8(NODE + 0x18C), ' '.join('%X' % e.r8(NODE + o) for o in (0x17A, 0x17B, 0x17D, 0x18A, 0x18B, 0x18D))))
    return rows


MAP_SPECIALS = [0x7FC00000, 0xFFC12345, 0x7F800000, 0xFF800000, 0x00000000, 0x80000000, 0x00000001, 0x80400000, 0x7F7FFFFF, 0xFF7FFFFF, 0x00800000, 0xC1200000, 0x41F00000, 0x41EFFFFF, 0x41F00001, 0x42C80000, 0x42C80001, 0x42C7FFFF, 0x43FA0000, 0x4B000000, 0x4F800000, 0xCF800000]


def map_rows(eng):
    """The two cutoff maps 0xA7A3D8 / 0xA7A4AC on the grid, the boundaries (30, 100), the special bit patterns and random values, at several u32[0x105243C]."""
    e = eng.e
    rows = []
    rng = random.Random(0x4D)
    vals = [fb(x / 20.0) for x in range(-40, 2200)]                  # -2..110 in steps of 0.05
    vals += MAP_SPECIALS
    vals += [rng.randrange(0, 1 << 32) for _ in range(300)]
    vals += [fb(rng.uniform(-10, 120)) for _ in range(600)]
    for rate in (48000, 44100, 8000, 96000, 0):
        e.w32(G_RATE, rate)
        for v in (vals if rate == 48000 else vals[::7]):
            rows.append('M %d %08X %08X %08X' % (rate, v, cn(e.call(0xA7A3D8, v, 0, 0)), cn(e.call(0xA7A4AC, v, 0, 0))))
    e.w32(G_RATE, 48000)
    return rows


def tan_grid():
    """Every tanf argument the filter can form for realistic rates: the map's cutoffs on a grid of v (LPF / HPF), the ramp values of 0 -> 15 and the shipped Sound."""
    import numpy as np
    eng = Engine()
    e = eng.e
    args = set()
    one = np.float32(1.0)
    pi = np.float32(struct.unpack('<f', struct.pack('<I', 0x40490FDB))[0])
    for rate in (48000, 44100, 32000, 22050, 16000, 8000, 96000):
        e.w32(G_RATE, rate)
        vs = [fb(x / 10.0) for x in range(0, 1001)]
        # the ramp arguments of 0 -> 15 and other ramps (v_k = cur + ((float)k * (target - cur)) * 0.125)
        for cur, tgt in ((0.0, 15.0), (15.0, 0.0), (0.0, 100.0), (100.0, 0.0), (50.0, 15.0), (30.0, 60.0)):
            c, t = np.float32(cur), np.float32(tgt)
            diff = np.float32(t - c)
            for k in range(1, 9):
                vs.append(fb(np.float32(c + np.float32(np.float32(np.float32(k) * diff) * np.float32(0.125)))))
        for v in vs:
            for fn in (0xA7A3D8, 0xA7A4AC):
                fc = ff(e.call(fn, v, 0, 0))
                a = np.float32(np.float32(np.float32(fc) / np.float32(rate)) * pi)
                args.add(fb(a))
    return args


def main():
    cases = make_cases()
    eng = Engine()
    rows = []
    for i, c in enumerate(cases):
        rows.append('%s | %s' % (case_str(c), eng.run(c)))
    ctor = ctor_row(eng)
    inits = init_rows(eng)
    maps = map_rows(eng)
    g = tan_grid()
    all_args = sorted(set(TANF_ARGS) | g)
    tan_rows = ['%08X %08X' % (a, tanf(a)) for a in all_args]
    worst = min(MARGINS) if MARGINS else None
    sys.stderr.write('scenarios %d; map rows %d; tanf arguments %d; the smallest distance of an exact tan to a binary32 midpoint: %s (units of 2^-53 * result; the argument bits %s)\n' % (
        len(cases), len(maps), len(all_args), '%.3f' % worst[0] if worst else None, '%08X' % worst[1] if worst else None))
    print('// <auto-generated> by re-analysis/tools/emu/emu_filter.py from the engine\'s own output (the real filter ctor 0xA76280, init 0xA764D4, Reset 0xA7666C, E1 entry 0xA766B8 with the LPF 0xA766F0 and HPF 0xA77480, and the cutoff maps 0xA7A3D8 / 0xA7A4AC under Unicorn): do not edit.')
    print('// The phone\'s tanf is float32 CORRECTLY ROUNDED in the emulator run (200-bit mpmath, then integer rounding to 24 bits).')
    print('// Smallest distance of any exact tan result the oracle used to a binary32 rounding midpoint: %s units of 2^-53 * result.' % ('%.3f' % worst[0] if worst else 'n/a'))
    print('namespace Cozmo.Protocol.Tests;')
    print()
    print('internal static class WwiseVoiceFilterOracle')
    print('{')
    print('    private static void Add(List<string> l, ReadOnlySpan<byte> chunk)')
    print('    {')
    print('        foreach (var row in System.Text.Encoding.UTF8.GetString(chunk).Split(\'\\n\', StringSplitOptions.RemoveEmptyEntries)) l.Add(row);')
    print('    }')
    print()
    print(cs_strings('Cases', 'One filter-node life per row: "rate chunk ch cfgHigh stride align fseed | LPF band | HPF band | histKind:histSeed:0 | ops | results". A band is cur:target:steps:countdown:dirty:first:bypass (floats as bits, hex). Stored F words (LPF seed, HPF seed+1) and histories come from the xorshift32 generator (kind 0 audio, histKind as emu_comp gen_input). ops: P.frames.kind.seed = 0xA766B8 on a generated buffer (gen_input kinds as emu_comp.py), B.lband.hband = new band blocks, R = Reset 0xA7666C. results: P:dataHash:LPFband:HPFband:Fhash:historyHash, R:LPFband:HPFband:Fhash:historyHash, B.', rows))
    print()
    print(cs_strings('Ctor', 'The ctor 0xA76280 on a node poisoned with 0xCD: "C | F_L words | F_H words | [0xB0] [0xB4] [0x160] [0x164] | band blocks 0x170..0x18F untouched | vtable word | [0x190]".', [ctor]))
    print()
    print(cs_strings('Init', 'The init 0xA764D4(node, word, flag) with the 1st / 2nd allocation failing ("I word flag fail | rc | LPF band HPF band | ptr[0xB0]!=0 ptr[0x160]!=0 | [0xB4] [0x164] | [0x190] byte[0x194] | history hash"); and the Reset 0xA7666C of a never-initialised node ("Z | byte[0x17C] byte[0x18C] | other flag bytes").', inits))
    print()
    print(cs_strings('Map', 'The cutoff maps: "M rate vBits LPF-map bits HPF-map bits" (0xA7A3D8 and 0xA7A4AC at u32[0x105243C] = rate; NaN canonicalised).', maps))
    print()
    print(cs_strings('TanfProof', 'tanf: "argument bits correctly rounded result bits" for every argument the filter forms (the cutoff grid of both maps at seven rates, the ramps 0 -> 15 and others, and every argument the emulator run passed).', tan_rows))
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
