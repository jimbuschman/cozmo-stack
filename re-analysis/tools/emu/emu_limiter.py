"""Run the engine's own Peak Limiter plug-in code under Unicorn and write the golden values for the B-M6b-4 batch 6d tests (C45; research 20261005-B-M6b-4-bus-fx-17.md, sections 2.6..2.8 and 4 with the Verification's build spec).

    python re-analysis/tools/emu/emu_limiter.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseLimiterOracle.cs      (a few minutes)
    python re-analysis/tools/emu/emu_limiter.py --case N        (the long form of life N)

The engine code that runs (no stand-in for it): the parameter object creator 0xAA2280 and its vtable (wrapper 0xAA20FC -> SetParamsBlock 0xAA2084 or the defaults, SetParam 0xAA216C, Clone 0xAA1FC4), the plug-in creator 0xAA18F4, Init 0xAA1B98, Setup 0xAA19CC, Reset 0xAA0940, Term 0xAA0894, GetPluginInfo
0xAA0904 and Execute 0xAA1BD8 with the unlinked / mono process P2 0xAA0EB4, the NoMoreData flush path and the output-gain stage with its NEON lane code. The stand-ins are the plug-in allocator (poisoned with 0xCD) and the phone's libm (expf, powf: float32 correctly rounded, emu_fxlib.py).
Not run: the linked processes P1 0xAA09B8 / P3 0xAA1464 and P2's LFE swap (required stops in the C#); no life selects them (a linked block only with one processed channel; the LFE flag of the buffer only with ProcessLFE 1).

A life is one object: rate, the channel word of the Init format, the channel word of the audio state, the failing allocation, whether the instance gets a Clone of the parameter object, the 22-byte block (or the defaults), then ops: R (Reset), E (Execute on a generated buffer with the input status in [S+8]), P (SetParam), B (a new
block), D (defaults), I (GetPluginInfo), T (Term). The audio comes from the xorshift32 generator of emu_comp.py.
"""
import math
import random
import struct
import sys

import numpy as np

sys.dont_write_bytecode = True
import emu_comp
import emu_fxlib
from emu_fxlib import FxEngine, SCRATCH, ALLOC
from emu_comp import fb, ff, sha, cn, cs_strings, RATES, POWF_ARGS, powf, expf, EXPF_ARGS

M32 = 0xFFFFFFFF
POISON = 0xCDCDCDCD
P2, P1, P3 = 0xAA0EB4, 0xAA09B8, 0xAA1464


def f32(x):
    return np.float32(x)


def fbits(x):
    return struct.unpack('<I', struct.pack('<f', float(np.float32(x))))[0]


def block(thr, ratio, look, rel, out_db, plfe, link):
    return struct.pack('<5f2B', f32(thr), f32(ratio), f32(look), f32(rel), f32(out_db), plfe, link)


# the shipped blocks (Init.bnk HIRC 0xDF2230FF Robot_Bus_Peak_Limiter and 0x3ABE7001 Cozmo_SFX_Bus_Limiter; research section 1)
LIM_ROBOT = block(-1.0, 10.8, 0.009, 0.041, 0.0, 0, 0)
LIM_SFX = block(-0.5, 10.0, 0.01, 0.1, 0.0, 1, 1)
assert struct.unpack('<4I', LIM_ROBOT[:16]) == (0xBF800000, 0x412CCCCD, 0x3C1374BC, 0x3D27EF9E)
assert struct.unpack('<4I', LIM_SFX[:16]) == (0xBF000000, 0x41200000, 0x3C23D70A, 0x3DCCCCCD)

PARAM_FIELDS = [(4, 4), (8, 4), (0xC, 4), (0x10, 4), (0x14, 1), (0x18, 4), (0x1C, 1), (0x1D, 1), (0x1E, 1)]


def lookahead_frames(rate, look):
    v = np.float32(np.float32(rate) * np.float32(look))
    if not (v > 0):
        return 0
    return int(v) if v < np.float32(4294967296.0) else M32


class Engine(FxEngine):
    def snap_param(self, p):
        e = self.e
        return ','.join('%08X' % e.r32(p + off) if size == 4 else '%02X' % e.r8(p + off) for off, size in PARAM_FIELDS)

    def set_block(self, p, blk):
        e = self.e
        if blk is None:
            return e.call(0xAA20FC, p, ALLOC, 0, 0)
        a = SCRATCH + 0x80
        e.uc.mem_write(a, bytes(blk))
        return e.call(0xAA20FC, p, ALLOC, a, 22)

    def word(self, a):
        w = self.e.r32(a)
        return '-' if w == POISON else '%08X' % cn(w)

    def snap_init(self, lim, rc):
        e = self.e
        proc = e.r32(lim + 4)
        pname = {P2: 'P2', P1: 'P1', P3: 'P3'}.get(proc, '%X' % proc)
        if rc != 1 and rc != 0x34:
            return '%d' % rc
        return '%d %s %s %X %X %X %X %X %X %s %s %d %d %s %s' % (rc, '%08X' % e.r32(lim + 0x14), pname, e.r32(lim + 0x18), e.r32(lim + 0x1C), e.r32(lim + 0x24), e.r32(lim + 0x28), e.r32(lim + 0x2C), e.r32(lim + 0x3C), self.word(lim + 0x40),
                                                                  self.word(lim + 0x48), 1 if e.r32(lim + 0x34) else 0, 1 if e.r32(lim + 0x30) else 0, self.word(lim + 0x38), self.word(lim + 0x44))

    def run(self, c):
        e = self.e
        self.begin(c['fail'])
        p0 = e.call(0xAA2280, ALLOC)
        assert p0
        self.set_block(p0, c['block'])
        if c['clone']:
            assert self.slot(p0, 3) == 0xAA1FC4
            p = e.call(0xAA1FC4, p0, ALLOC)
        else:
            p = p0
        assert p
        lim = e.call(0xAA18F4, ALLOC)
        assert lim
        fmt = SCRATCH
        e.uc.mem_write(fmt, struct.pack('<III', c['rate'], c['fmt'], 0x1A2B3C4D))
        rc = e.call(0xAA1B98, lim, ALLOC, 0xCAFE0000, p, stack=(fmt,))
        init = self.snap_init(lim, rc)
        res = []
        for op in c['ops']:
            k = op[0]
            if k == 'R':
                res.append('R:%d' % e.call(0xAA0940, lim))
            elif k == 'E':
                res.append(self.execute(lim, p, c, op))
            elif k == 'P':
                _, pid, val = op
                a = SCRATCH + 0x40
                e.w32(a, val)
                r = e.call(0xAA216C, p, pid, a)
                res.append('P:%d:%s' % (r, self.snap_param(p)))
            elif k == 'B':
                r = self.set_block(p, op[1])
                res.append('B:%d:%s' % (r, self.snap_param(p)))
            elif k == 'D':
                r = self.set_block(p, None)
                res.append('D:%d:%s' % (r, self.snap_param(p)))
            elif k == 'I':
                info = SCRATCH + 0x300
                e.uc.mem_write(info, b'\xcd' * 16)
                r = e.call(0xAA0904, lim, info)
                res.append('I:%d:%08X,%08X,%02X,%02X' % (r, e.r32(info), e.r32(info + 4), e.r8(info + 8), e.r8(info + 0xA)))
            elif k == 'T':
                n0 = len(self.frees)
                r = e.call(0xAA0894, lim, ALLOC)
                res.append('T:%d:%d' % (r, len(self.frees) - n0))
        return init, ' ; '.join(res)

    def execute(self, lim, p, c, op):
        e = self.e
        _, frames, stride, kind, seed, estate = op
        planes = max(c['fmt'] & 0xFF, c['cfg'] & 0xFF, 1)
        d, n = self.put_audio(kind, seed, planes, stride)
        s = SCRATCH + 0x100
        e.uc.mem_write(s, b'\xcd' * 0x40)
        e.w32(s, d)
        e.w32(s + 4, c['cfg'])
        e.w32(s + 8, estate)
        e.w16(s + 0xC, stride)
        e.w16(s + 0xE, frames)
        e.call(0xAA1BD8, lim, s)
        out = self.read_audio(d, n)
        slots = e.r32(lim + 0x28)
        det = e.r32(lim + 0x30)
        if det and slots < 64:
            words = []
            for i in range(slots):
                words += [cn(e.r32(det + 12 * i)), cn(e.r32(det + 12 * i + 4)), e.r32(det + 12 * i + 8)]
            det_hash = sha(words)
        else:
            det_hash = '-'
        ring = e.r32(lim + 0x34)
        L = e.r32(lim + 0x2C)
        nb = e.r8(lim + 0x1C)
        if ring and L * nb < 1 << 20:
            ring_hash = sha([cn(e.r32(ring + 4 * i)) for i in range(L * nb)])
        else:
            ring_hash = '-'
        return 'E:%s:%08X:%s:%08X:%s:%s:%s:%s:%02X:%08X:%d:%02X:%02X' % (
            sha(out), cn(e.r32(lim + 0x14)), self.word(lim + 0x38), e.r32(lim + 0x3C), self.word(lim + 0x40), self.word(lim + 0x44), det_hash, ring_hash,
            e.r8(lim + 0x4C), e.r32(s + 8), e.r16(s + 0xE), e.r8(p + 0x14), e.r8(p + 0x1E))


# ------------------------------------------------------------------------------------------------ the cases

FRAMES = [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63, 64, 65, 100, 127, 128, 200, 256, 431, 432, 513, 700, 1000, 1024]
THRS = [-40.0, -24.0, -12.0, -6.0, -3.0, -1.0, -0.5, 0.0, 3.0]
RATIOS = [1.0, 1.5, 2.0, 4.0, 6.0, 10.0, 10.8, 20.0, 0.5, 100.0]
LOOKS = [0.002, 0.004, 0.005, 0.009, 0.01, 0.015, 0.02, 0.03]
RELS = [0.001, 0.005, 0.02, 0.041, 0.1, 0.2, 0.5, 1.0]
OUTS = [-12.0, -6.0, -1.0, 0.0, 0.5, 1.5, 3.0, 6.0, 12.0]
EDGE_T = [float('nan'), float('inf'), float('-inf'), 400.0, -400.0, 1e30]
EDGE_R = [0.0, -1.0, float('nan'), float('inf'), 1e-30, 1e30]
EDGE_REL = [0.0, -0.5, float('nan'), float('inf'), 1e-9, 1e9]
SHIPPED_RATES = [48000, 22320, 44100, 32000, 24000, 16000, 8000, 96000, 22050]
# (channel count, format word, buffer word, link allowed, ProcessLFE): the format's LFE flag needs ProcessLFE 1 for the buffer to carry it (P2's LFE swap is a required stop)
LAYOUTS = [(1, 0x4101), (2, 0x3102), (3, 0x7103), (4, 0x3104), (1, 0x01), (1, 0x4101 | 0x8000), (2, 0x3102 | 0x8000), (6, 0x3F06 | 0x8000)]


def rand_block(rng, edge=False, link=None, plfe=None):
    return block(rng.choice(THRS + (EDGE_T if edge else [])), rng.choice(RATIOS + (EDGE_R if edge else [])), rng.choice(LOOKS), rng.choice(RELS + (EDGE_REL if edge else [])), rng.choice(OUTS),
                 rng.choice([0, 1]) if plfe is None else plfe, rng.choice([0, 0, 1, 2]) if link is None else link)


def make_cases():
    rng = random.Random(20261007)
    cases = []

    def case(rate, fmt, cfg, blk, ops, fail=None, clone=1):
        cases.append(dict(rate=rate, fmt=fmt, cfg=cfg, block=blk, ops=ops, fail=fail, clone=clone))

    def E(frames=None, kind=0, seed=None, stride=None, estate=0x2B):
        frames = frames if frames is not None else rng.choice(FRAMES)
        stride = stride if stride is not None else frames + rng.choice([0, 0, 0, 1, 7, 64])
        return ('E', frames, stride, kind, seed if seed is not None else rng.randrange(1, 1 << 30), estate)

    def P(pid, v):
        return ('P', pid, v)

    def fits(rate, blk):
        look = struct.unpack('<5f', blk[:20])[2]
        return lookahead_frames(rate, look) >= 1

    def pick_layout(plfe, link):
        """A layout the built processes cover: P2 only (no linked block with more than one processed channel; the LFE flag in the buffer only with ProcessLFE 1; in the format only with ProcessLFE 1 or a channel count above 1 with a clean buffer word)."""
        while True:
            ch, word = rng.choice(LAYOUTS)
            processed = ch - 1 if (word & 0x8000) and not plfe else ch
            cfg = word
            if (word & 0x8000) and not plfe:
                cfg = word & ~0x8000          # the buffer carries no flag: P2 has no swap to do
            if link and processed != 1:
                continue
            return ch, word, cfg

    # A. the shipped blocks: every rate, mono; plain, loud, a gain change (the NEON ramp), the equal multiply, the first-buffer prescan and the ring wrap
    for blk in (LIM_ROBOT, LIM_SFX):
        for rate in SHIPPED_RATES:
            for _ in range(2):
                case(rate, 0x4101, 0x4101, blk, [('R',), E(kind=0), E(kind=0), E(kind=1), P(4, fbits(3.0)), E(kind=0), E(kind=1), P(3, fbits(0.1)), E(frames=3), E(frames=4)], clone=rng.choice([0, 1]))
    # B. random lives on the built processes, plain / loud audio, SetParam / block / default ops (the ids that re-run Setup: 2, 5, 6; the ones that refresh the release: 0, 1, 3, 4)
    for _ in range(1500):
        link = rng.choice([0, 0, 1, 2])
        plfe = rng.choice([0, 1])
        blk = rand_block(rng, link=link, plfe=plfe)
        rate = rng.choice(RATES)
        if not fits(rate, blk):
            continue
        ch, word, cfg = pick_layout(plfe, link)
        ops = [('R',), E(kind=rng.choice([0, 0, 1])), E(kind=0)]
        for _ in range(rng.choice([1, 2, 3])):
            kind = rng.choice(['P', 'P', 'P', 'B', 'D'])
            if kind == 'P':
                pid = rng.choice([0, 1, 2, 3, 4, 4, 5, 6, 7, 9])
                processed = ch - 1 if (word & 0x8000) and not plfe else ch
                if pid == 0:
                    ops.append(P(pid, fbits(rng.choice(THRS))))
                elif pid == 1:
                    ops.append(P(pid, fbits(rng.choice(RATIOS))))
                elif pid == 2:
                    nv = rng.choice(LOOKS)
                    if lookahead_frames(rate, nv) >= 1:
                        ops.append(P(pid, fbits(nv)))
                elif pid == 3:
                    ops.append(P(pid, fbits(rng.choice(RELS))))
                elif pid == 4:
                    ops.append(P(pid, fbits(rng.choice(OUTS))))
                elif pid == 5:
                    if not (word & 0x8000):
                        ops.append(P(pid, rng.choice([0, 1])))        # no flag anywhere: ProcessLFE does not change the process
                    else:
                        ops.append(P(pid, plfe))
                elif pid == 6:
                    ops.append(P(pid, rng.choice([0, 1, 2]) if processed == 1 else 0))
                else:
                    ops.append(P(pid, 0))                              # ids above 6: 0x1F
            elif kind == 'B':
                nb = rand_block(rng, link=link if processed_ok(ch, word, plfe) else 0, plfe=plfe)
                if fits(rate, nb):
                    ops.append(('B', nb))
            elif ch == 1:
                ops.append(('D',))          # the defaults (ChannelLink 1, ProcessLFE 1) only on one channel: P2 whatever the flags
            ops.append(E(kind=rng.choice([0, 0, 1])))
        case(rate, word, cfg, blk, ops, clone=rng.choice([0, 1]))
    # C. the NoMoreData flush path: establish a tail, then flush calls of every shape (V = 0, V small, V = L, V > L; M = V and M much larger)
    for blk in (LIM_ROBOT, LIM_SFX):
        for rate in (48000, 22320, 44100):
            for _ in range(40):
                ops = [('R',), E(frames=rng.choice([1, 100, 431, 700]), kind=0)]
                for _ in range(rng.choice([2, 3, 4, 6, 8])):
                    v = rng.choice([0, 0, 0, 1, 5, 100, 431, 432, 700])
                    m = v + rng.choice([0, 0, 1, 64, 200, 431, 432, 600, 1000])
                    m = max(m, 1)
                    if rng.random() < 0.15:
                        ops.append(E(frames=rng.choice([1, 50, 431, 1000]), kind=0))      # a normal call in between: the tail counter restarts
                    else:
                        ops.append(E(frames=v, stride=m, kind=rng.choice([0, 6, 0]), estate=0x11))
                case(rate, 0x4101, 0x4101, blk, ops, clone=rng.choice([0, 1]))
    for _ in range(300):
        link = rng.choice([0, 0, 1])
        plfe = rng.choice([0, 1])
        blk = rand_block(rng, link=link, plfe=plfe)
        rate = rng.choice(RATES)
        if not fits(rate, blk):
            continue
        ch, word, cfg = pick_layout(plfe, link)
        L = lookahead_frames(rate, struct.unpack('<5f', blk[:20])[2])
        ops = [('R',), E(frames=rng.choice([1, L, L + 5, 2 * L + 3]), kind=0)]
        for _ in range(rng.choice([2, 4, 6])):
            v = rng.choice([0, 0, 1, L - 1, L, L + 1, 2 * L])
            m = max(v + rng.choice([0, 1, L // 2, L, L + 1, 2 * L + 5]), 1)
            ops.append(E(frames=max(v, 0), stride=m, kind=rng.choice([0, 6]), estate=0x11))
        case(rate, word, cfg, blk, ops, clone=rng.choice([0, 1]))
    # D. denormal-scale, denormal, NaN / inf / random patterns, zeros; with an output-level change so the NEON ramp and the equal multiply both see them
    for _ in range(500):
        link = rng.choice([0, 0, 1])
        plfe = rng.choice([0, 1])
        blk = rand_block(rng, link=link, plfe=plfe)
        rate = rng.choice(RATES)
        if not fits(rate, blk):
            continue
        ch, word, cfg = pick_layout(plfe, link)
        kind = rng.choice([2, 2, 3, 3, 4, 5, 5, 6])
        case(rate, word, cfg, blk, [('R',), E(kind=kind), P(4, fbits(rng.choice([3.0, 9.0, -9.0]))), E(kind=kind), E(kind=0), E(kind=kind)], clone=rng.choice([0, 1]))
    # E. Init: channel counts 0..6, the LFE flag with ProcessLFE 0 / 1, the failing ring and detector allocations, the instance on the creator's object (no Clone)
    for word in (0x00, 0x01, 0x02, 0x8001, 0x8002, 0x4101, 0x3102, 0x3104, 0xBF06, 0x7103):
        for plfe in (0, 1):
            for link in (0, 1):
                for clone in (0, 1):
                    if (word & 0xFF) == 0 and (word & 0x8000):
                        continue
                    b = block(-1.0, 10.8, 0.009, 0.041, 0.0, plfe, link)
                    processed = (word & 0xFF) - 1 if (word & 0x8000) and not plfe else (word & 0xFF)
                    cfg = word if (plfe or not (word & 0x8000)) else word & ~0x8000
                    ok = (link == 0 or processed == 1) and processed >= 1
                    ops = [('R',), E(), ('I',), ('T',)] if ok else [('R',), ('T',)]
                    case(48000, word, cfg, b, ops, clone=clone)
                    case(48000, word, cfg, b, [('R',), ('T',)], fail=(4 if clone else 3), clone=clone)      # the ring allocation
                    case(48000, word, cfg, b, [('R',), ('T',)], fail=(5 if clone else 4), clone=clone)      # the detector allocation
    # F. the coefficient extremes: threshold / ratio / release NaN / inf / zero / negative (the detector must reach the same non-numbers)
    for _ in range(300):
        blk = rand_block(rng, edge=True, link=rng.choice([0, 1]), plfe=rng.choice([0, 1]))
        rate = rng.choice(RATES + [1000000])
        if not fits(rate, blk):
            continue
        case(rate, 0x4101, 0x4101, blk, [('R',), E(kind=rng.choice([0, 1, 5])), E(kind=0)], clone=rng.choice([0, 1]))
    # G. the defaults (size 0), the block ops and a reset in the middle
    for _ in range(60):
        rate = rng.choice(RATES)
        nb = rand_block(rng, link=0, plfe=0)
        if not fits(rate, nb):
            continue
        case(rate, 0x4101, 0x4101, None, [('R',), E(), ('B', nb), E(), ('D',), E(), ('R',), E(), ('I',), ('T',)], clone=rng.choice([0, 1]))
    return cases


def processed_ok(ch, word, plfe):
    processed = ch - 1 if (word & 0x8000) and not plfe else ch
    return processed == 1


# ------------------------------------------------------------------------------------------------ output

def op_str(op):
    if op[0] == 'E':
        return 'E.%d.%d.%d.%d.%X' % op[1:]
    if op[0] == 'P':
        return 'P.%d.%X' % (op[1], op[2])
    if op[0] == 'B':
        return 'B.' + bytes(op[1]).hex()
    return op[0]


def case_str(c):
    return '%d %X %X %d %d | %s | %s' % (c['rate'], c['fmt'], c['cfg'], c['fail'] or 0, c['clone'], bytes(c['block']).hex() if c['block'] is not None else '-', ' '.join(op_str(o) for o in c['ops']) or '-')


def proof_grid():
    """The libm arguments of the limiter for the shipped blocks at the realistic rates: expf(-2.2f / (L / 2)), expf(-2.2f / (rate * release)), powf(10, outDb * 0.05f)."""
    k = np.float32(struct.unpack('<f', struct.pack('<I', 0x3D4CCCCD))[0])
    m22 = np.float32(struct.unpack('<f', struct.pack('<I', 0xC00CCCCD))[0])
    ex, pw = set(), set()
    for rate in SHIPPED_RATES + [11025, 12000, 88200]:
        for blk in (LIM_ROBOT, LIM_SFX):
            _, _, look, rel, _ = struct.unpack('<5f', blk[:20])
            L = lookahead_frames(rate, look)
            ex.add(fbits(m22 / (np.float32(L) * np.float32(0.5))))
            ex.add(fbits(m22 / (np.float32(rate) * np.float32(rel))))
        for look in LOOKS:
            L = lookahead_frames(rate, look)
            if L:
                ex.add(fbits(m22 / (np.float32(L) * np.float32(0.5))))
        for rel in RELS:
            ex.add(fbits(m22 / (np.float32(rate) * np.float32(rel))))
    for o in OUTS + [0.0]:
        pw.add(fbits(np.float32(o) * k))
    return ex, pw


def main():
    cases = make_cases()
    eng = Engine()
    rows = []
    for c in cases:
        init, res = eng.run(c)
        rows.append('%s | %s | %s' % (case_str(c), init, res))
    ge, gp = proof_grid()
    all_e, all_p = sorted(EXPF_ARGS | ge), sorted(POWF_ARGS | gp)
    exp_rows = ['%08X %08X' % (a, expf(a)) for a in all_e]
    pow_rows = ['%08X %08X' % (a, powf(0x41200000, a)) for a in all_p]
    worst = emu_fxlib.worst_margin()
    sys.stderr.write('lives %d; expf %d powf %d arguments; the smallest distance of an exact result to a binary32 midpoint: %s (units of 2^-53 * result; the argument bits %s)\n' % (
        len(cases), len(all_e), len(all_p), '%.3f' % worst[0] if worst else None, '%08X' % worst[1] if worst else None))
    print('// <auto-generated> by re-analysis/tools/emu/emu_limiter.py from the engine\'s own output (the real Peak Limiter creator 0xAA18F4, Init 0xAA1B98, Setup 0xAA19CC, Reset 0xAA0940, Term 0xAA0894, Execute 0xAA1BD8 with P2 0xAA0EB4, and the parameter object 0xAA2280 / 0xAA20FC / 0xAA2084 / 0xAA216C / 0xAA1FC4 under Unicorn): do not edit.')
    print('// The phone\'s expf / powf are float32 CORRECTLY ROUNDED in the emulator run (200-bit mpmath, then integer rounding to 24 bits).')
    print('// Smallest distance of any exact libm result the oracle used to a binary32 rounding midpoint: %s units of 2^-53 * result.' % ('%.3f' % worst[0] if worst else 'n/a'))
    print('namespace Cozmo.Protocol.Tests;')
    print()
    print('internal static class WwiseLimiterOracle')
    print('{')
    print('    private static void Add(List<string> l, ReadOnlySpan<byte> chunk)')
    print('    {')
    print('        foreach (var row in System.Text.Encoding.UTF8.GetString(chunk).Split(\'\\n\', StringSplitOptions.RemoveEmptyEntries)) l.Add(row);')
    print('    }')
    print()
    print(cs_strings('Cases', 'One Peak Limiter life per row: "rate fmtWord cfgWord failAlloc clone | block | ops | init | results". ops: R reset, E.frames.stride.kind.seed.inputStatus execute on a generated buffer with u32 [S+8] = inputStatus (kinds as in emu_comp.py), P.id.valueBits SetParam, B.block a new block, D the defaults, I info, T term. init: "rc +0x14 process +0x18 +0x1C +0x24 +0x28 +0x2C +0x3C +0x40 +0x48 ringAllocated detectorsAllocated +0x38 +0x44" ("-" is uninitialised pool memory). results per op: E:audioHash:+0x14:+0x38:+0x3C:+0x40:+0x44:detectorHash:ringHash:justReset:eState:validFrames:dirtyA:dirtyB, R:rc, P/B/D:rc:parameterFields, I:rc:info, T:rc:frees.', rows))
    print()
    print(cs_strings('ExpfProof', 'expf: "argument bits correctly rounded result bits" for every argument the limiter forms (the attack and release coefficients of the shipped blocks and a grid at the realistic rates, and every argument the emulator run passed).', exp_rows))
    print()
    print(cs_strings('PowfProof', 'powf(10.0f, y): "y bits correctly rounded result bits" for the output levels (y = dB * 0.05f) of the shipped blocks, a grid, and every argument the emulator run passed.', pow_rows))
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
