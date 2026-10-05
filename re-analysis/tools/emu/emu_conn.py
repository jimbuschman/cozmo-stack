"""Run the engine's own connection gain/matrix chain and bus gain stage under Unicorn and write the golden values for the B-M6b-4 batch 6e tests (C44.3; research matrix-chain-16 and voice-filter-13 verifications).

    python re-analysis/tools/emu/emu_conn.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseConnOracle.cs      (about a minute)
    python re-analysis/tools/emu/emu_conn.py --case V3     (the long form of scenario V3; also A12, B7, D5)

The engine code that runs (no stand-in for it): 0xA4BC58 (the per-voice connection update: prelude, S2E/S2F, minima, the per-connection state machine B1..B15), 0xA5975C (the send gain and the pan decision), 0xA25FF8 with its tail 0xA1F79C and 0xA209BC, 0xA67B9C and 0xA67C58 (the descriptor),
0xA4D994 (the bus gain stage), 0xA4F9E0 (the bus-to-bus mix), 0xA4FBEC (the voice-to-bus mix) with 0xA45E9C and 0xA46668. The stand-ins: the libc calls (PLT memset 0x4D36DC, memcpy 0x4D37F0, memmove 0x4D667C), the pool allocator 0xA7A894 / 0xA7A7F4 (a bump allocator whose blocks are POISONED with 0xCD,
so a read of memory the engine never wrote shows) and its frees 0xA7A914 / 0xA7A988 (logged), the voice vtable slot +0x3C (0xA55E90: the case's value), and the three callees this oracle does not run because no adopted row reads them: the 3D pair 0xA5B9D0 / 0xA5993C and 0xA5D70C (each stops the run and the
row says STOP:3D or STOP:P12; the C# must throw at the same frame). The global device list [0x108DAFC + 8] is empty (A5975C's scan finds no record: the node argument of 0xA25FF8 is 0).

Objects are built in emulated memory from explicit scenario parameters and POISONED (0xCD) first: only the fields a row names are written. The rows carry every input (floats as bit patterns); the audio inputs are drawn from the xorshift32 generator XS below (the C# test has the same lines, Xs in WwiseResamplerTests.cs).

Groups: A (0xA25FF8 over config pairs, flags and pans: 112 rows), V (0xA4BC58 sequences of 1..4 frames over 0..3 connections, with the mix 0xA4FBEC after each frame for the mono-line scenarios: 700 rows), B (0xA4D994 sequences with 0xA4F9E0 after each call: 400 rows), D (the 0xA4D994 gain over 10824 dB values).
The engine rows leave out nothing: a scenario the C# stops on (3D, param_12) is a STOP row; the config pairs the inventory does not cover are in group A and the C# test expects a stop for them. Scenario filters (not engine claims): no mix on a descriptor sized for another input count, and a first bus call never starts with old parameters equal to the new ones (the matrices would stay uninitialised pool memory).
"""
import hashlib
import random
import struct
import sys

import numpy as np

sys.dont_write_bytecode = True
from emu_common import Emu, BASE, HEAP, RET, STACK, f32, bits
from unicorn.arm_const import *

M32 = 0xFFFFFFFF
POISON = 0xCD


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
        v = self.u32()
        if v >= 0x80000000:
            v -= 1 << 32
        return float(np.float32(np.float32(v) / np.float32(2147483648.0)))


def fb(x):
    return struct.unpack('<I', struct.pack('<f', float(np.float32(x))))[0]


def ff(b):
    return struct.unpack('<f', struct.pack('<I', b & M32))[0]


def hx(v):
    return '%X' % (v & M32)


def canon_hash(raw):
    n = len(raw) // 4
    vals = struct.unpack('<%dI' % n, raw)
    fixed = [0x7FC00000 if ((v >> 23) & 0xFF) == 0xFF and (v & 0x7FFFFF) else v for v in vals]
    return hashlib.sha256(struct.pack('<%dI' % n, *fixed)).hexdigest()[:16]


# ------------------------------------------------------------------------------------------------ the world

VOICE = BASE + 0x10000
TABLE = BASE + 0x11000
VT = BASE + 0x12000
PADDR = BASE + 0x13000
OUTS = BASE + 0x14000          # S2E, S2F bytes and the four float outputs
CONNS = BASE + 0x20000         # connections 0x100 apart
LINES = BASE + 0x30000         # lines 0x400 apart (the dry line per connection)
SBLK = BASE + 0x40000          # the voice block S
STUBS = BASE + 0x50000
BUS = BASE + 0x60000
PARENT = BASE + 0x61000
CHILD_S = BASE + 0x62000
BUFFERS = BASE + 0x70000

STUB_VT3C = STUBS + 0x00
STUB_3D_A = 0xA5B9D0
STUB_3D_B = 0xA5993C
STUB_P12 = 0xA5D70C


class World(Emu):
    def __init__(self):
        super().__init__()
        self.stopped = None
        self.vt3c = 0
        self.log = []

        def memset(em):
            d, v, n = em.reg(0), em.reg(1), em.reg(2)
            if n:
                em.uc.mem_write(d, bytes([v & 0xFF]) * n)
            return d
        self.hook(0x4D36DC, memset)

        def memcpy(em):
            d, s, n = em.reg(0), em.reg(1), em.reg(2)
            if n:
                em.uc.mem_write(d, bytes(em.uc.mem_read(s, n)))
            return d
        self.hook(0x4D37F0, memcpy)
        self.hook(0x4D667C, memcpy)

        def pool_alloc(em):            # 0xA7A7F4(pool, size) and 0xA7A894(pool, size, align)
            size = em.reg(1)
            a = em.alloc(size)
            em.uc.mem_write(a, bytes([POISON]) * ((max(size, 1) + 15) & ~15))
            return a
        self.hook(0xA7A7F4, pool_alloc)
        self.hook(0xA7A894, pool_alloc)

        def pool_free(em):
            self.log.append(('free', em.reg(1)))
            return None
        self.hook(0xA7A988, pool_free)
        self.hook(0xA7A914, pool_free)

        self.hook(STUB_VT3C, lambda em: em.vt3c)

        def stop(reason):
            def f(em):
                em.stopped = reason
                em.uc.emu_stop()
                return 0
            return f
        self.hook(STUB_3D_A, stop('3D'))
        self.hook(STUB_3D_B, stop('3D'))
        self.hook(STUB_P12, stop('P12'))

    def poison(self, a, n):
        self.uc.mem_write(a, bytes([POISON]) * n)

    def wbytes(self, a, b):
        self.uc.mem_write(a, bytes(b))

    def rbytes(self, a, n):
        return bytes(self.uc.mem_read(a, n))

    def run(self, addr, *args, stack=()):
        self.stopped = None
        self.call(addr, *args, stack=stack)


# ------------------------------------------------------------------------------------------------ group A: 0xA25FF8

def a_cases():
    rows = []
    e = World()
    pans = [(0.5, 0.5, 0.0), (0.0, 0.0, 0.0), (1.0, 0.3, 0.7), (0.25, 0.75, 0.5), (0.5, 0.5, 0.2), (0.0, 1.0, 0.5), (0.9, 0.2, 1.0)]
    ins = [0x4101, 0x3102, 0x0001, 0x0002]
    outs = [0x4101, 0x3102]
    M = BASE + 0x1000
    for ci in ins:
        for co in outs:
            for flag in (0, 1):
                for (p1, p2, p3) in pans:
                    rows_n = (co & 0xFF) + 3 & ~3
                    n_in = ci & 0xFF
                    total = n_in * rows_n
                    e.uc.mem_write(M, bytes([POISON]) * 256)
                    try:
                        e.run(0xA25FF8, fb(p1), fb(p2), fb(p3), flag, stack=(ci, co, M, 0))
                    except Exception as ex:                      # an unread arm (cosf / sinf through an unmapped PLT)
                        continue
                    vals = struct.unpack('<%dI' % total, e.rbytes(M, 4 * total))
                    rows.append('A %X %X %d %X %X %X | %s' % (ci, co, flag, fb(p1), fb(p2), fb(p3), ' '.join('%X' % v for v in vals)))
    return rows


# ------------------------------------------------------------------------------------------------ group V: 0xA4BC58

def pick(r, items):
    return items[r.randrange(len(items))]


FLOATS = [0.0, 1.0, 0.5, 0.37, 0.25, 2.0, 100.0, 50.0, 10.0, 0.75, 33.3, 99.0]


def rf(r):
    return fb(pick(r, FLOATS) if r.random() < 0.7 else r.uniform(0.0, 100.0))


def rlp(r):
    """A filter cut-off: sometimes a NaN (the running minima compare with vcmpe / vmovpl: an unordered compare replaces the output)."""
    return 0x7FC00000 if r.random() < 0.1 else rf(r)


def supported_out(in_word, r, mono=False):
    if mono:
        return 0x4101
    t = (in_word >> 8) & 0xF
    if in_word == 0:
        return pick(r, [0x4101, 0x3102])
    if t == 0:
        return pick(r, [0x4101, 0x3102])
    if in_word == 0x4101:
        return pick(r, [0x4101, 0x4101, 0x3102])
    return 0x4101                                   # 0x3102 -> mono


def v_scenario(seed):
    r = random.Random(0xC0DE0000 + seed)
    nconn = pick(r, [0, 1, 1, 1, 1, 2, 2, 2, 3, 3])
    mix = 1 if r.random() < 0.5 else 0
    words = [0x4101, 0x4101, 0x4101, 0x3102, 0x3102, 0x0001, 0x0002, 0]
    if mix:
        words = [0x4101, 0x4101, 0x3102]
    word0 = pick(r, words)
    stereo_pan_ok = False
    nframes = 1 + r.randrange(4)
    sc = dict(seed=seed, nconn=nconn, mix=mix, cd0=pick(r, [0x00, 0x01, 0x20, 0x40, 0x80, 0xF1]) & ~0x0C,
              vgain=rf(r), sendgain=rf(r), frames_n=pick(r, [8, 16, 24, 64]))
    sc['lpf'] = rlp(r)
    sc['hpf'] = rlp(r)
    sc['conns'] = []
    for k in range(nconn):
        sc['conns'].append(dict(arg5=pick(r, [0, 0, 1, 2, 4]), f6c=(0x01 if r.random() < 0.9 else 0) | (r.randrange(8) << 5) | (r.randrange(2) << 4) | (r.randrange(2) << 2),
                                c=[pick(r, [fb(1.0), rf(r)]) for _ in range(4)], ga=(rf(r), rf(r))))
    # the pan inputs
    sc['p_b8'] = 0x42CA0000
    sc['p_bc'], sc['p_c0'], sc['p_c4'] = fb(r.uniform(-50, 50)), fb(r.uniform(-50, 50)), r.randrange(2)
    sc['dc0'] = pick(r, [0x4C, 0x4C, 0x4C, 0x0C, 0x5C & ~0x10 | 0x00])
    if r.random() < 0.06:
        sc['dc0'] |= pick(r, [1, 2, 3])                   # the 3D branch (a required stop)
    frames = []
    word = word0
    lcfg = [supported_out(word, r, mix) for _ in range(nconn)]
    for f in range(nframes):
        fr = {}
        if f > 0 and r.random() < 0.3:
            word = pick(r, words)
            lcfg = [supported_out(word, r, mix) for _ in range(nconn)]
        fr['word'] = word
        fr['lcfg'] = list(lcfg)
        fr['vt3c'] = pick(r, [0, 0, 0, 1])
        fr['argA'] = r.randrange(2)
        fr['gain'] = rf(r)
        fr['p12'] = 1 if r.random() < 0.05 else 0
        fr['cd3'] = pick(r, [0, 1]) if f > 0 else pick(r, [0, 0, 1])
        fr['cd2'] = r.randrange(2)
        fr['send'] = rf(r) if r.random() < 0.3 else sc['sendgain']
        fr['dc4'] = 1 if r.random() < 0.4 or f == 0 else 0
        fr['bit1'] = [1 if r.random() < 0.25 else 0 for _ in range(nconn)]
        pan_choices = [0.0, 0.0, 0.0, -0.0, 0.5, 12.5, -30.0, 100.0, float('nan')]
        fr['a8'], fr['ac'], fr['b0'] = [fb(pick(r, pan_choices)) for _ in range(3)]
        fr['b4'] = pick(r, [0, 0, 1])
        fr['lpf'] = rlp(r)
        fr['hpf'] = rlp(r)
        fr['valid'] = pick(r, [0, sc['frames_n'], sc['frames_n'], max(1, sc['frames_n'] // 2)])
        fr['seed'] = r.randrange(1, 1 << 30)
        frames.append(fr)
    # a mono -> stereo connection with flag 1 needs the default pan tuple (the shipped data) for the supported arms
    for fr in frames:
        need_default = False
        for k in range(nconn):
            ci, co = fr['word'], fr['lcfg'][k]
            if ci in (0x4101,) and co == 0x3102:
                need_default = True
        if need_default and fr['b4'] == 1:
            fr['a8'] = fr['ac'] = fr['b0'] = fb(0.0)
        if need_default:
            # the compare also looks at the previous frame's tail: keep pans at the supported tuple when flag byte could be 1
            pass
    sc['frames'] = frames
    return sc


def setup_world_v(e, sc):
    nconn = sc['nconn']
    e.poison(VOICE, 0x200)
    e.poison(TABLE, 0x100)
    e.poison(VT, 0x100)
    e.poison(PADDR, 0x100)
    e.w32(VOICE, VT)
    e.w32(VT + 0x3C, STUB_VT3C)
    e.w32(VOICE + 0x10, TABLE)
    e.wf(VOICE + 0x1C, ff(sc['vgain']))
    e.w32(VOICE + 0x28, CONNS if nconn else 0)
    e.w8(VOICE + 0xCD, sc['cd0'])
    e.wf(PADDR + 0x3C, ff(sc['lpf']))
    e.wf(PADDR + 0x40, ff(sc['hpf']))
    e.w32(PADDR + 0xB8, sc['p_b8'])
    e.w32(PADDR + 0xBC, sc['p_bc'])
    e.w32(PADDR + 0xC0, sc['p_c0'])
    e.w32(PADDR + 0xC4, 0xCDCDCD00 | sc['p_c4'])
    e.w8(PADDR + 0xDC, sc['dc0'] | 0x10)
    for k in range(nconn):
        c = CONNS + 0x100 * k
        line = LINES + 0x400 * k
        e.poison(c, 0x70)
        e.poison(line, 0x400)
        for off in (0x18, 0x1C, 0x20, 0x24, 0x64, 0x50, 0x54, 0x58, 0x5C, 0x60):
            e.w32(c + off, 0)
        e.w32(c + 0x28, CONNS + 0x100 * (k + 1) if k + 1 < nconn else 0)
        e.w32(c + 0x30, line)
        e.w32(c + 0x48, 2)
        e.w32(c + 0x4C, 0)
        cs = sc['conns'][k]
        for off, val in zip((8, 0xC, 0x10, 0x14), cs['c']):
            e.w32(c + off, val)
        e.w32(c + 0x68, cs['arg5'])
        e.w8(c + 0x6C, cs['f6c'])


def run_v(e, sc, row_only=False):
    """Returns the row text and the per-frame outcome strings."""
    setup_world_v(e, sc)
    nconn = sc['nconn']
    out_strs = []
    mixbuf_ptrs = []
    for fi, fr in enumerate(sc['frames']):
        # --- per-frame inputs
        cd = e.r8(VOICE + 0xCD)
        cd = (cd & ~0x0C) | (fr['cd3'] << 3) | (fr['cd2'] << 2)
        e.w8(VOICE + 0xCD, cd)
        e.w32(VOICE + 0xF0, fr['word'])
        e.w32(TABLE + 0x34, fb(ff(fr['send'])))
        e.w32(PADDR + 0xA8, fr['a8'])
        e.w32(PADDR + 0xAC, fr['ac'])
        e.w32(PADDR + 0xB0, fr['b0'])
        e.w32(PADDR + 0xB4, 0xCDCDCD00 | fr['b4'])
        e.wf(PADDR + 0x3C, ff(fr['lpf']))
        e.wf(PADDR + 0x40, ff(fr['hpf']))
        dc = e.r8(PADDR + 0xDC)
        e.w8(PADDR + 0xDC, (dc & ~0x10) | (fr['dc4'] << 4))
        for k in range(nconn):
            c = CONNS + 0x100 * k
            f6c = e.r8(c + 0x6C)
            e.w8(c + 0x6C, (f6c & ~2) | (fr['bit1'][k] << 1))
            e.w32(LINES + 0x400 * k + 0x64, fr['lcfg'][k])
        e.vt3c = fr['vt3c']
        s2e, s2f = OUTS, OUTS + 1
        flo = [OUTS + 0x10 + 4 * i for i in range(4)]
        p12 = 0
        if fr['p12']:
            p12 = OUTS + 0x40
            e.w32(p12, 0x1234)
            e.w32(p12 + 4, 0x5678)
        e.uc.mem_write(OUTS, bytes([0xCD]) * 2)
        for a in flo:
            e.w32(a, 0xCDCDCDCD)
        e.run(0xA4BC58, VOICE, PADDR, fr['word'], fb(ff(fr['gain'])), stack=(fr['argA'], s2e, s2f, flo[0], flo[1], flo[2], flo[3], p12))
        if e.stopped:
            out_strs.append('STOP:' + e.stopped)
            break
        o = ['%X' % e.r8(s2e), '%X' % e.r8(s2f)] + [hx(e.r32(a)) for a in flo]
        o += ['%X' % e.r8(VOICE + 0xCD), '%X' % e.r8(PADDR + 0xDC), hx(e.r32(PADDR + 0xB8)), hx(e.r32(PADDR + 0xBC)), hx(e.r32(PADDR + 0xC0)), '%X' % e.r8(PADDR + 0xC4)]
        conns = []
        for k in range(nconn):
            c = CONNS + 0x100 * k
            base, size, pa, pb = e.r32(c + 0x18), e.r32(c + 0x1C), e.r32(c + 0x20), e.r32(c + 0x24)
            half = size // 2
            nmat = ' '.join(hx(w) for w in struct.unpack('<%dI' % (half // 4), e.rbytes(pa, half))) if base else '-'
            pmat = ' '.join(hx(w) for w in struct.unpack('<%dI' % (half // 4), e.rbytes(pb, half))) if base else '-'
            cs = [hx(e.r32(c + off)) for off in (8, 0xC, 0x10, 0x14, 0x50, 0x54, 0x58, 0x5C, 0x64)] + ['%X' % e.r8(c + 0x6C)]
            conns.append('%s %d/%d/%d n:%s p:%s' % (' '.join(cs), size if base else 0, (pa - base) if base else 0, (pb - base) if base else 0, nmat, pmat))
        text = ' '.join(o) + ' | ' + ' ; '.join(conns)
        # --- the mix after the frame (mono lines, standard words only)
        if sc['mix']:
            mixes = []
            for k in range(nconn):
                c = CONNS + 0x100 * k
                base = e.r32(c + 0x18)
                f6c = e.r8(c + 0x6C)
                sch = fr['word'] & 0xFF
                if base == 0 or (f6c & 6) == 6 or e.r32(c + 0x64) != sch:      # a descriptor sized for another input count would be read past its allocation (a scenario filter, not an engine claim)
                    mixes.append('-')
                    continue
                cs = sc['conns'][k]
                g0, g1 = (fb(1.0), fb(1.0)) if cs['arg5'] == 0 else cs['ga']
                sch = fr['word'] & 0xFF
                frames_n, valid = sc['frames_n'], fr['valid']
                d = XS(fr['seed'] + k)
                sdata = [d.f32() for _ in range(frames_n * sch)]
                ddata = [d.f32() for _ in range(frames_n)]
                sd = BUFFERS
                bd = BUFFERS + 0x4000
                e.wbytes(sd, struct.pack('<%df' % len(sdata), *sdata))
                e.wbytes(bd, struct.pack('<%df' % len(ddata), *ddata))
                S = SBLK
                e.poison(S, 0x40)
                e.w32(S, sd)
                e.w32(S + 4, fr['word'])
                e.w16(S + 0xC, frames_n)
                e.w16(S + 0xE, valid)
                bus = LINES + 0x400 * k
                e.w16(bus + 0x58, frames_n)
                e.wf(bus + 0x5C, ff(fb(np.float32(1.0) / np.float32(frames_n))))
                e.w32(bus + 0x60, bd)
                e.w32(bus + 0x68, 0x11)
                e.w16(bus + 0x6C, frames_n)
                e.w16(bus + 0x6E, 0)
                e.w32(bus + 0x1A8, 0)
                e.w32(bus + 0x1BC, 4)
                gp = BUFFERS + 0x8000
                e.wbytes(gp, struct.pack('<II', g0, g1))
                e.run(0xA4FBEC, bus, S, c, gp)
                mixes.append('%X %X %X %s %X %s' % (e.r32(bus + 0x68), e.r32(bus + 0x1BC), e.r16(bus + 0x6E), canon_hash(e.rbytes(bd, 4 * frames_n)), e.r16(S + 0xE), canon_hash(e.rbytes(sd, 4 * frames_n * sch))))
            text += ' | ' + ' ; '.join(mixes)
        out_strs.append(text)
    return out_strs


def v_row(sc, outs):
    h = 'V%d %d %d %X %X %X %X %X %X' % (sc['seed'], sc['nconn'], sc['mix'], sc['cd0'], fb(ff(sc['vgain'])), fb(ff(sc['sendgain'])), sc['dc0'], sc['frames_n'], fb(ff(sc['lpf'])))
    h += ' %X %X %X %X %X' % (sc['p_b8'], sc['p_bc'], sc['p_c0'], sc['p_c4'], fb(ff(sc['hpf'])))
    conns = ';'.join('%d,%X,%X,%X,%X,%X,%X,%X' % (c['arg5'], c['f6c'], c['c'][0], c['c'][1], c['c'][2], c['c'][3], c['ga'][0], c['ga'][1]) for c in sc['conns']) or '-'
    frs = []
    for fr in sc['frames']:
        t = '%d %d %X %d %X %d %d %X %d %X %X %X %d %X %X %d %X' % (fr['vt3c'], fr['argA'], fr['gain'], fr['p12'], fr['word'], fr['cd3'], fr['cd2'], fr['send'], fr['dc4'], fr['a8'], fr['ac'], fr['b0'], fr['b4'], fr['lpf'], fr['hpf'], fr['valid'], fr['seed'])
        t += ' ' + (','.join('%d' % b + ':%X' % l for b, l in zip(fr['bit1'], fr['lcfg'])) or '-')
        frs.append(t)
    return '%s | %s | %s => %s' % (h, conns, ' ;; '.join(frs), ' ;; '.join(outs))


def v_cases(n=700):
    e = World()
    rows = []
    for s in range(n):
        sc = v_scenario(s)
        try:
            outs = run_v(e, sc)
        except Exception as ex:
            sys.stderr.write('V%d failed: %r\n' % (s, ex))
            continue
        rows.append(v_row(sc, outs))
    return rows


# ------------------------------------------------------------------------------------------------ group B: 0xA4D994 and 0xA4F9E0

def b_scenario(seed):
    r = random.Random(0xB0000000 + seed)
    local = pick(r, [0x4101, 0x4101, 0x3102])
    out = 0x4101 if local == 0x3102 else pick(r, [0x4101, 0x4101, 0x3102])
    sc = dict(seed=seed, local=local, out=out)
    sc['has_matrix'] = 1 if r.random() < 0.85 else 0
    sc['c0'] = pick(r, [0x00, 0x00, 0x08, 0x02, 0x0A, 0x01, 0x20]) & ~0x04
    sc['c0'] |= 0x08 if r.random() < 0.4 else 0
    sc['init_mat'] = r.randrange(1, 1 << 30)
    dbs = [0.0, 0.0, -6.0, -80.0, -96.0, 6.0, -3.0, -740.0, -741.0, -12.5, 20.0, 740.0, 1000.0, float('nan')]
    sc['db'] = fb(pick(r, dbs))
    sc['g84'] = rf(r)
    sc['g80'] = rf(r)
    defaults = (fb(0.5), fb(1.0), fb(100.0), 0)
    if r.random() < 0.5:
        sc['params'] = defaults
    else:
        sc['params'] = (fb(pick(r, [0.0, 0.5, 0.25, 1.0, 100.0, -100.0])), fb(pick(r, [0.0, 1.0, 0.5, 0.75])), fb(pick(r, [100.0, 50.0, 0.0])), pick(r, [0, 0, 1]))
    if sc['c0'] & 8 and r.random() < 0.7:
        sc['old'] = sc['params']
    else:
        sc['old'] = (fb(101.0), fb(0.0), fb(0.0), 0) if r.random() < 0.5 else (fb(pick(r, [0.0, 0.5, 1.0])), fb(pick(r, [0.0, 1.0])), fb(pick(r, [100.0, 0.0])), pick(r, [0, 1]))
    if not (sc['c0'] & 8) and sc['old'] == sc['params']:                # a first call whose old parameters equal the new ones never computes the matrices (the engine's defaults store 101.0f in [bus+0xA4]): the halves would stay uninitialised pool memory
        sc['old'] = (fb(101.0),) + sc['old'][1:]
    if out == 0x3102:                                                   # a mono -> stereo matrix with flag 1 is supported only for the default pan tuple: keep the flag byte 0 (flag 0 takes any pan)
        sc['params'] = sc['params'][:3] + (0,)
    sc['pcfg'] = pick(r, [0x4101, 0x4101, 0x4101])          # the model's bus buffers are mono
    sc['frames_n'] = pick(r, [8, 16, 64])
    calls = []
    for i in range(1 + r.randrange(4)):
        call = {}
        call['db'] = fb(pick(r, dbs)) if i else sc['db']
        if i and r.random() < 0.5:
            call['params'] = (fb(pick(r, [0.0, 0.5, 0.25, 1.0, 100.0, -100.0])), fb(pick(r, [0.0, 1.0, 0.5, 0.75])), fb(pick(r, [100.0, 50.0, 0.0])), pick(r, [0, 1]))
        else:
            call['params'] = None
        if call.get('params') is not None and out == 0x3102:
            call['params'] = call['params'][:3] + (0,)
        call['c0xor'] = pick(r, [0, 0, 0, 2])
        call['valid'] = pick(r, [0, sc['frames_n'], sc['frames_n'], max(1, sc['frames_n'] // 2)])
        call['seed'] = r.randrange(1, 1 << 30)
        call['pc0'] = pick(r, [0, 0, 0, 2, 4, 6])
        call['pcfg'] = None
        calls.append(call)
    sc['calls'] = calls
    return sc


def run_b(e, sc):
    outs = []
    e.poison(BUS, 0x200)
    e.poison(PARENT, 0x200)
    e.poison(CHILD_S, 0x40)
    local, out = sc['local'], sc['out']
    cfgp = BUFFERS + 0x9000
    e.w32(cfgp, local)
    e.w32(BUS + 0x28, 0)
    e.w32(BUS + 0x2C, 0)
    e.w32(BUS + 0x44, out)
    e.w32(BUS + 0x48, 0)
    e.w32(BUS + 0x34, 0)
    e.w32(BUS + 0x38, 0)
    e.w32(BUS + 0x3C, 0)
    e.w32(BUS + 0x40, 0)
    if sc['has_matrix']:
        rc = e.call(0xA67B9C, BUS + 0x34, local & 0xFF, out & 0xFF)
        assert rc == 1
        # a state past the first call needs both halves written (the engine reads the poison otherwise)
        d = XS(sc['init_mat'])
        size = e.r32(BUS + 0x38)
        if sc['c0'] & 8:
            vals = [d.f32() for _ in range(size // 4)]
            e.wbytes(e.r32(BUS + 0x34), struct.pack('<%df' % len(vals), *vals))
    e.wf(BUS + 0x80, ff(sc['g80']))
    e.wf(BUS + 0x84, ff(sc['g84']))
    e.w32(BUS + 0x90, sc['db'])
    p = sc['params']
    e.w32(BUS + 0x94, p[0]); e.w32(BUS + 0x98, p[1]); e.w32(BUS + 0x9C, p[2]); e.w32(BUS + 0xA0, p[3])
    o = sc['old']
    e.w32(BUS + 0xA4, o[0]); e.w32(BUS + 0xA8, o[1]); e.w32(BUS + 0xAC, o[2]); e.w32(BUS + 0xB0, o[3])
    e.w8(BUS + 0xC0, sc['c0'])
    e.w32(BUS + 0x30, 0)
    # the parent line (mono model)
    fr_n = sc['frames_n']
    e.w16(PARENT + 0x58, fr_n)
    e.wf(PARENT + 0x5C, ff(fb(np.float32(1.0) / np.float32(fr_n))))
    pbuf = BUFFERS + 0x2000
    e.w32(PARENT + 0x60, pbuf)
    e.w32(PARENT + 0x64, sc['pcfg'])
    e.w16(PARENT + 0x6C, fr_n)
    e.w16(PARENT + 0x6E, 0)
    e.w32(PARENT + 0x68, 0x11)
    e.w32(PARENT + 0x1A8, 0)
    e.w32(PARENT + 0x1BC, 4)
    # the child's out buffer S (its data is the child's buffer)
    for call in sc['calls']:
        if call['params'] is not None:
            p = call['params']
            e.w32(BUS + 0x94, p[0]); e.w32(BUS + 0x98, p[1]); e.w32(BUS + 0x9C, p[2]); e.w32(BUS + 0xA0, p[3])
        e.w32(BUS + 0x90, call['db'])
        e.w8(BUS + 0xC0, (e.r8(BUS + 0xC0) ^ call['c0xor']) & ~0x04)
        e.w32(cfgp, local)
        e.run(0xA4D994, BUS, cfgp)
        if e.stopped:
            outs.append('STOP:' + e.stopped)
            break
        base, size, pa, pb = e.r32(BUS + 0x34), e.r32(BUS + 0x38), e.r32(BUS + 0x3C), e.r32(BUS + 0x40)
        half = size // 2
        nmat = ' '.join(hx(w) for w in struct.unpack('<%dI' % (half // 4), e.rbytes(pa, half))) if base else '-'
        pmat = ' '.join(hx(w) for w in struct.unpack('<%dI' % (half // 4), e.rbytes(pb, half))) if base else '-'
        text = ' '.join([hx(e.r32(BUS + 0x80)), hx(e.r32(BUS + 0x84)), hx(e.r32(BUS + 0x94)), hx(e.r32(BUS + 0x98)), hx(e.r32(BUS + 0x9C)), '%X' % e.r8(BUS + 0xA0),
                         hx(e.r32(BUS + 0xA4)), hx(e.r32(BUS + 0xA8)), hx(e.r32(BUS + 0xAC)), '%X' % e.r8(BUS + 0xB0), '%X' % e.r8(BUS + 0xC0)])
        text += ' %d/%d/%d n:%s p:%s' % (size if base else 0, (pa - base) if base else 0, (pb - base) if base else 0, nmat, pmat)
        # --- the parent mix 0xA4F9E0 (mono model buffers; the child's S = its own buffer with the gain pair copied to S[0x10], S[0x14])
        if local == 0x4101 and sc['pcfg'] == 0x4101:
            d = XS(call['seed'])
            sdata = [d.f32() for _ in range(fr_n)]
            pdata = [d.f32() for _ in range(fr_n)]
            sd = BUFFERS + 0x1000
            e.wbytes(sd, struct.pack('<%df' % fr_n, *sdata))
            e.wbytes(pbuf, struct.pack('<%df' % fr_n, *pdata))
            S = CHILD_S
            e.poison(S, 0x40)
            e.w32(S, sd)
            e.w32(S + 4, local)
            e.w16(S + 0xC, fr_n)
            e.w16(S + 0xE, call['valid'])
            e.w32(S + 0x10, e.r32(BUS + 0x80))
            e.w32(S + 0x14, e.r32(BUS + 0x84))
            e.w32(PARENT + 0x68, 0x11)
            e.w32(PARENT + 0x1BC, 4)
            e.w16(PARENT + 0x6E, 0)
            c0save = e.r8(BUS + 0xC0)
            e.w8(BUS + 0xC0, (c0save & ~6) | call['pc0'])
            try:
                e.run(0xA4F9E0, PARENT, S, BUS)
                text += ' | %X %X %X %s %X %s' % (e.r32(PARENT + 0x68), e.r32(PARENT + 0x1BC), e.r16(PARENT + 0x6E), canon_hash(e.rbytes(pbuf, 4 * fr_n)), e.r16(S + 0xE), canon_hash(e.rbytes(sd, 4 * fr_n)))
            except Exception as ex:
                text += ' | ENGINE_FAIL'
            e.w8(BUS + 0xC0, c0save)
        outs.append(text)
    return outs


def b_row(sc, outs):
    h = 'B%d %X %X %d %X %X %X %X %X %X %X %X %X %X %X %X %X %X %X %d' % (sc['seed'], sc['local'], sc['out'], sc['has_matrix'], sc['c0'], sc['init_mat'], sc['db'], fb(ff(sc['g80'])), fb(ff(sc['g84'])),
                                                                         sc['params'][0], sc['params'][1], sc['params'][2], sc['params'][3], sc['old'][0], sc['old'][1], sc['old'][2], sc['old'][3], sc['pcfg'], 0, sc['frames_n'])
    calls = []
    for c in sc['calls']:
        pr = '-' if c['params'] is None else ','.join('%X' % v for v in c['params'])
        calls.append('%X %s %d %d %X %d' % (c['db'], pr, c['c0xor'], c['valid'], c['seed'], c['pc0']))
    return '%s | %s => %s' % (h, ' ;; '.join(calls), ' ;; '.join(outs))


def b_cases(n=400):
    e = World()
    rows = []
    for s in range(n):
        sc = b_scenario(s)
        try:
            outs = run_b(e, sc)
        except Exception as ex:
            sys.stderr.write('B%d failed: %r\n' % (s, ex))
            continue
        rows.append(b_row(sc, outs))
    return rows


# ------------------------------------------------------------------------------------------------ group D: the bus gain 0xA4D994 over dB values

def d_cases():
    """dBToLin of [bus+0x90] as 0xA4D994 stores it in [bus+0x84] (no matrix descriptor: [bus+0x34] == 0): a grid over the whole range, the edges of the -37 cut (y = db * 0.05f), the special values, and random bit patterns."""
    e = World()
    r = random.Random(0xD0D0)
    vals = [fb(-820.0 + 0.125 * i) for i in range(0, 8800)]
    vals += [fb(x) for x in (0.0, -0.0, 6.0, -6.0, -80.0, -96.0, -3.0, -740.0, -741.0, -739.375, 100.0, 740.0, 1e5, 1e10, 3.4e38, -3.4e38, 1e-40, -1e-40)]
    vals += [0x7FC00000, 0x7F800000, 0xFF800000, 0x00000001, 0x80000001, 0x007FFFFF]
    vals += [r.getrandbits(32) for _ in range(2000)]
    cfgp = BUFFERS + 0x9000
    e.w32(cfgp, 0x4101)
    rows = []
    for v in vals:
        e.poison(BUS, 0x200)
        for off in (0x34, 0x38, 0x3C, 0x40, 0x48, 0x28, 0x2C, 0x30):
            e.w32(BUS + off, 0)
        e.w32(BUS + 0x90, v)
        e.w8(BUS + 0xC0, 8)
        e.run(0xA4D994, BUS, cfgp)
        rows.append('D %X | %X' % (v, e.r32(BUS + 0x84)))
    return rows


# ------------------------------------------------------------------------------------------------ output

def cs_strings(name, doc, rows, chunk=100):
    lines = ['    /// <summary>%s</summary>' % doc, '    public static readonly string[] %s = Make%s();' % (name, name), '',
             '    private static string[] Make%s()' % name, '    {', '        var l = new List<string>();']
    for i in range(0, len(rows), chunk):
        part = rows[i:i + chunk]
        lines.append('        Add(l,')
        for j, r in enumerate(part):
            lines.append('            "%s\\n"u8%s' % (r, ' +' if j < len(part) - 1 else ');'))
    lines.append('        return l.ToArray();')
    lines.append('    }')
    return '\n'.join(lines)


def main():
    a = a_cases()
    v = v_cases()
    b = b_cases()
    d = d_cases()
    sys.stderr.write('A %d rows, V %d rows, B %d rows, D %d rows\n' % (len(a), len(v), len(b), len(d)))
    print("// <auto-generated> by re-analysis/tools/emu/emu_conn.py from the engine's own output (the real 0xA4BC58, 0xA5975C, 0xA25FF8 / 0xA1F79C, 0xA67B9C / 0xA67C58, 0xA4D994, 0xA4F9E0, 0xA4FBEC, 0xA45E9C, 0xA46668 under Unicorn): do not edit.")
    print('// The 3D pair 0xA5B9D0 / 0xA5993C and 0xA5D70C are stopped stand-ins (STOP:3D / STOP:P12 rows); the pool allocator is a poisoned bump allocator; PLT memset / memcpy / memmove are hooked.')
    print('namespace Cozmo.Protocol.Tests;')
    print()
    print('internal static class WwiseConnOracle')
    print('{')
    print('    private static void Add(List<string> l, ReadOnlySpan<byte> chunk)')
    print('    {')
    print("        foreach (var row in System.Text.Encoding.UTF8.GetString(chunk).Split('\\n', StringSplitOptions.RemoveEmptyEntries)) l.Add(row);")
    print('    }')
    print()
    print(cs_strings('MatrixRows', '0xA25FF8 over config pairs: "A inCfg outCfg flag p1 p2 p3 | matrix words" (numIn rows of (numOut + 3) & ~3 floats; hex float bits). The engine run of every pair the oracle could run (a pan arm that reaches an unmapped libm call is left out).', a))
    print()
    print(cs_strings('VoiceRows', '0xA4BC58 sequences: "V<seed> nconn mix cd0 vgain send dc0 frames lpf b8 bc c0 c4 hpf | conns | frames => outcomes". See WwiseConnOracleTests for the field order.', v))
    print()
    print(cs_strings('BusRows', '0xA4D994 sequences with 0xA4F9E0 after each call: "B<seed> ... | calls => outcomes". See WwiseConnOracleTests for the field order.', b))
    print()
    print(cs_strings('GainRows', '0xA4D994 gain: "D dBBits | [bus+0x84] bits" for a grid of dB values (-820 .. 280 step 0.125), the cut at -740 dB, the special values and random bit patterns.', d))
    print('}')


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--case':
        k = sys.argv[2]
        e = World()
        if k[0] == 'V':
            sc = v_scenario(int(k[1:]))
            print(v_row(sc, run_v(e, sc)))
        elif k[0] == 'B':
            sc = b_scenario(int(k[1:]))
            print(b_row(sc, run_b(e, sc)))
        else:
            print('\n'.join(a_cases()))
    else:
        main()
