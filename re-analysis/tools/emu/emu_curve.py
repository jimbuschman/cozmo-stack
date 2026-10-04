"""Run the engine's own RTPC curve evaluation 0xA14E28 under Unicorn and write the golden values for WwiseRtpcCurveTests (M6-009, C35.1).

    python re-analysis/tools/emu/emu_curve.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseCurveOracle.cs

The real function 0xA14E28(curve, x, hint, &idx) runs (no stand-in: it is a leaf with no calls) on curves built in emulated memory: {points*, count, scaling}, a point is {f32 x, f32 y, u32 interp}. The cases:

  * every interp code 0..9, 10, 11, 15, 100, 0x80000000, 0xFFFFFFFF x every scaling 0..5 and 7 x two ranges of y x eleven x values (below, at, between, at the right end, above, NaN, +-inf);
  * the scaling stages on a one-point curve for ~100 y values (+-0, +-inf, quiet NaN of three payloads, +-1, +-0.99999994, +-37, +-740, +-800, the neighbours of 38.83, denormals, 1e30, random binary32 patterns) x scalings 0..5, 7;
  * random curves of 1..6 points (sorted, unsorted, duplicate abscissae), random interp codes and scalings, x on a point / between / random / NaN, hint 0, 1, 2, 3, 7, count-1, 100, 0xFFFFFFFF;
  * every one of the 64 RTPC curves of the six shipped banks (re-analysis/obb/sound_meta, parsed here from the HIRC chunks) at 50 x values each;
  * the literal pool 0xA15248..0xA152C0 read from the .so;
  * the live entry: the real 0xA11590 (with 0xA17878 / 0xA17724 / 0xA17280) over an RTPC manager built with a subscription per curve, root value set to x (via emu_bus.World), for the shipped curves, the product path and
    a selection of the random and matrix curves.

The expected values in WwiseRtpcCurveTests are this script's output (the engine's), never the C#'s. Floats are bit patterns; a NaN result is compared as NaN only.
"""
import math
import os
import random
import struct
import sys

from emu_common import *
from emu_bus import World, Node, Sub
from so import u32

SOUND_META = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'obb', 'sound_meta')
BANKS = ['Init.bnk', 'SFX.bnk', 'UI.bnk', 'Dev_Debug.bnk', 'Music.bnk', os.path.join('English(US)', 'Cozmo.bnk')]

M32 = 0xFFFFFFFF


def fbits(x):
    return struct.unpack('<I', struct.pack('<f', x))[0]


def ff(b):
    return struct.unpack('<f', struct.pack('<I', b & M32))[0]


def f32r(x):
    """Round a Python double to binary32 (finite or not)."""
    try:
        return ff(fbits(x))
    except OverflowError:
        return math.copysign(math.inf, x)


# ------------------------------------------------------------------------------------------------ the engine

class Engine:
    def __init__(self):
        self.e = Emu()
        self.pts = self.e.alloc(12 * 16)
        self.cur = self.e.alloc(16)
        self.idx = self.e.alloc(8)

    def run(self, points, scaling, x_bits, hint=0):
        """points: [(x_bits, y_bits, interp)] -> (result bits, idx)."""
        e = self.e
        for i, (px, py, pi_) in enumerate(points):
            e.w32(self.pts + 12 * i, px)
            e.w32(self.pts + 12 * i + 4, py)
            e.w32(self.pts + 12 * i + 8, pi_)
        e.w32(self.cur, self.pts)
        e.w32(self.cur + 4, len(points))
        e.w32(self.cur + 8, scaling)
        e.w32(self.idx, 0xDEADBEEF)
        r = e.call(0xA14E28, self.cur, x_bits, hint, self.idx)
        return r, e.r32(self.idx)


# ------------------------------------------------------------------------------------------------ the shipped banks

class Rd:
    def __init__(self, b):
        self.b, self.p = b, 0

    def u8(self):
        v = self.b[self.p]; self.p += 1; return v

    def u16(self):
        v = struct.unpack_from('<H', self.b, self.p)[0]; self.p += 2; return v

    def u32(self):
        v = struct.unpack_from('<I', self.b, self.p)[0]; self.p += 4; return v

    def skip(self, n):
        assert self.p + n <= len(self.b)
        self.p += n


def read_rtpcs(r, out):
    n = r.u16()
    for _ in range(n):
        src = r.u32(); typ = r.u8(); acc = r.u8()
        param = 0
        while True:                                     # the varint: 7 bits per byte, 0x80 continues, high group first
            b = r.u8(); param = (param << 7) | (b & 0x7F)
            if not b & 0x80:
                break
        curve = r.u32(); scaling = r.u8(); npts = r.u16()
        pts = []
        for _ in range(npts):
            pts.append((r.u32(), r.u32(), r.u32()))
        out.append(dict(src=src, type=typ, acc=acc, param=param, curve=curve, scaling=scaling, pts=pts))


def read_node_params(r, out):
    r.u8()
    nfx = r.u8()
    if nfx:
        r.u8(); r.skip(7 * nfx)
    r.u8(); r.u32(); r.u32(); r.u8()
    for _ in range(2):
        n = r.u8(); r.skip(n)
        r.skip(4 * n if _ == 0 else 8 * n)
    pos = r.u8()
    assert not (pos & 1 and pos & 8)
    aux = r.u8()
    if aux & 8:
        r.skip(16)
    r.skip(6)
    groups = r.u32()
    for _ in range(groups):
        r.u32(); r.u8(); ns = r.u16(); r.skip(8 * ns)
    read_rtpcs(r, out)


def hirc_curves(path):
    data = open(path, 'rb').read()
    off, hirc, feedback = 0, None, 0
    while off + 8 <= len(data):
        tag, size = data[off:off + 4], struct.unpack_from('<I', data, off + 4)[0]
        body = off + 8
        if tag == b'BKHD':
            assert struct.unpack_from('<I', data, body + 12)[0] == 0, 'feedback flag'
        if tag == b'HIRC':
            hirc = data[body:body + size]
        off = body + size
    assert off == len(data) and hirc is not None
    count = struct.unpack_from('<I', hirc, 0)[0]
    p = 4
    found = []
    for _ in range(count):
        typ, size = hirc[p], struct.unpack_from('<I', hirc, p + 1)[0]
        body = hirc[p + 5:p + 5 + size]
        p += 5 + size
        r = Rd(body)
        oid = r.u32()
        out = []
        if typ == 2:                                     # Sound
            plugin = r.u32(); r.u8(); r.u32(); r.u32(); r.u8()
            if plugin & 0xF in (2, 5):
                r.skip(r.u32())
            read_node_params(r, out)
        elif typ in (5, 6, 7, 9):                        # RanSeq, Switch, ActorMixer, LayerCntr
            read_node_params(r, out)
        elif typ in (10, 12, 13):                        # music segment / switch / playlist
            r.u8(); read_node_params(r, out)
        elif typ == 11:                                  # music track
            r.u8(); ns = r.u32(); r.skip(14 * ns)
            nc = r.u32(); r.skip(40 * nc)
            if nc:
                r.u32()
            na = r.u32()
            for _ in range(na):
                r.u32(); r.u32(); npts = r.u32(); r.skip(12 * npts)
            read_node_params(r, out)
        elif typ in (21, 22):                            # LFO / envelope modulator
            n = r.u8(); r.skip(n); r.skip(4 * n)
            n = r.u8(); r.skip(n); r.skip(8 * n)
            read_rtpcs(r, out)
        elif typ == 8:                                   # audio bus
            r.u32(); n = r.u8(); r.skip(n); r.skip(4 * n)
            r.u8(); b = r.u8(); assert b & 0xF == 0
            r.u16(); r.u32(); r.u8(); r.u32(); r.u32()
            ducks = r.u32(); r.skip(18 * ducks)
            nfx = r.u8()
            if nfx:
                r.u8(); r.skip(7 * nfx)
            r.u32(); r.u8(); r.u8()
            read_rtpcs(r, out)
        elif typ in (18, 19):                            # effect share set / custom
            r.u32(); r.skip(r.u32())
            n = r.u8(); r.skip(5 * n)
            read_rtpcs(r, out)
        else:
            continue
        for e in out:
            e.update(bank=os.path.basename(path), objtype=typ, obj=oid)
            found.append(e)
    return found


def shipped():
    out = []
    for b in BANKS:
        out += hirc_curves(os.path.join(SOUND_META, b))
    assert len(out) == 64, len(out)                       # the Opus verifier's census (C35.4): 64 RTPC entries
    assert {e['scaling'] for e in out} == {0, 2}
    assert min(len(e['pts']) for e in out) == 2 and max(len(e['pts']) for e in out) == 6
    return out


# ------------------------------------------------------------------------------------------------ the cases

class Oracle:
    def __init__(self):
        self.eng = Engine()
        self.curves = []        # point lists (bits)
        self.curve_ix = {}
        self.cases = []         # (curve, scaling, hint, x_bits, result, idx, tag)

    def curve(self, pts):
        key = tuple(pts)
        if key not in self.curve_ix:
            self.curve_ix[key] = len(self.curves)
            self.curves.append(list(pts))
        return self.curve_ix[key]

    def add(self, pts, scaling, x_bits, hint=0):
        r, idx = self.eng.run(pts, scaling, x_bits, hint)
        self.cases.append((self.curve(pts), scaling, hint, x_bits, r, idx))
        return r


def P(x, y, interp):
    return (fbits(x), fbits(y), interp)


NAN = 0x7FC00000
INTERPS = list(range(10)) + [10, 11, 0xFFFFFFFF]
SCALINGS = [0, 1, 2, 3, 4, 5, 7]
XS = [-1.0, 0.0, 0.1, 0.25, 0.5, 0.75, 1.0, 1.5]


def matrix(o):
    for interp in INTERPS:
        for scaling in SCALINGS:
            for (y0, y1) in ((0.0, 1.0), (-1.0, 0.0), (-60.0, 5.0)):
                if scaling == 2 and y0 < -1.0:
                    continue
                pts = [P(0.0, y0, interp), P(1.0, y1, 4)]
                for x in XS:
                    o.add(pts, scaling, fbits(x))
                o.add(pts, scaling, NAN)


def y_edges(o):
    ys = [0.0, -0.0, 1.0, -1.0, 0.5, -0.5, 0.25, 0.75, 0.999, -0.999, 0.001, 0.005, 6.0, -6.0, -20.0, -60.0, 1.5, -1.5, 2.0, 37.0, -37.0, 36.99999, -36.99999, 37.00001, -37.00001, 740.0, -740.0, 800.0, -800.0,
          38.0, 38.5, 38.8, 38.82, 38.83, 38.84, 38.9, 39.0, 40.0, 100.0, -100.0, 200.0, 1e30, -1e30, 3.4e38, -3.4e38]
    yb = [fbits(y) for y in ys]
    yb += [0x7F800000, 0xFF800000, 0x7FC00000, 0xFFC00000, 0x7FC00001, 0x3F7FFFFF, 0xBF7FFFFF, 0x3F800001, 0xBF800001, 0x00000001, 0x80000001, 0x007FFFFF, 0x807FFFFF, 0x00800000, 0x80800000,
           0x0DA22560, 0xC2140001, 0xC213FFFF, 0x421B0000, 0x421B5C29]
    rnd = random.Random(20261004)
    yb += [rnd.getrandbits(32) for _ in range(30)]
    for b in yb:
        for scaling in SCALINGS:
            o.add([(0, b, 4)], scaling, fbits(0.5))
    # two-point constant segments too: the y of the interpolated segment goes through the same stage
    for b in yb[:30]:
        for scaling in (0, 2, 3, 4):
            o.add([(0, b, 4), (fbits(1.0), b, 4)], scaling, fbits(0.5))


def random_curves(o, n=170):
    rnd = random.Random(777)
    for _ in range(n):
        count = rnd.choice([1, 2, 2, 3, 3, 4, 5, 6])
        scaling = rnd.choice(SCALINGS)
        kind = rnd.choice(['sorted', 'sorted', 'sorted', 'unsorted', 'dups'])
        xs = sorted(f32r(rnd.uniform(-2, 5)) for _ in range(count))
        if kind == 'unsorted':
            rnd.shuffle(xs)
        elif kind == 'dups' and count >= 2:
            xs[rnd.randrange(count)] = xs[rnd.randrange(count)]
        pts = []
        for k in range(count):
            if scaling == 2:
                y = rnd.choice([rnd.uniform(-1.2, 1.2), rnd.uniform(-1, 0), rnd.uniform(0, 1), 0.0, -1.0, 1.0])
            elif scaling in (3, 4):
                y = rnd.choice([rnd.uniform(-60, 45), rnd.uniform(-45, 0), rnd.uniform(-1, 3), -37.0 * (20 if scaling == 4 else 1)])
            else:
                y = rnd.uniform(-200, 200)
            interp = rnd.choice([4, 4, 0, 1, 2, 3, 5, 6, 7, 8, 9, 9, 10, 12])
            pts.append(P(xs[k], f32r(y), interp))
        px = [ff(p[0]) for p in pts]
        xl = list(px)
        for k in range(count - 1):
            xl.append(f32r(px[k] + (px[k + 1] - px[k]) * rnd.choice([0.3, 0.5, 0.7])))
        xl += [min(px) - 1, max(px) + 1, f32r(rnd.uniform(-3, 6))]
        for x in xl:
            o.add(pts, scaling, fbits(x))
        o.add(pts, scaling, NAN)
        for h in (1, 2, count - 1, count, 100, 0xFFFFFFFF):
            o.add(pts, scaling, fbits(f32r(rnd.uniform(-1, 6))), h)
            o.add(pts, scaling, fbits(px[rnd.randrange(count)]), h)


def shipped_cases(o, ship):
    rnd = random.Random(64)
    names = []
    for e in ship:
        pts = [(x, y, i) for (x, y, i) in e['pts']]
        px = [ff(p[0]) for p in pts]
        xl = list(px)
        for k in range(len(px) - 1):
            a, b = px[k], px[k + 1]
            for t in (0.001, 0.1, 0.5, 0.9, 0.999):
                xl.append(f32r(a + (b - a) * t))
        for x in px:                                             # one ulp either side of every abscissa
            xl.append(ff(fbits(x) + 1))
            xl.append(ff(fbits(x) - 1) if x != 0 else -1e-45)
        xl += [px[0] - 1, px[0] - 0.5, px[-1] + 1, px[-1] + 10]
        seen = []
        for x in xl:
            if fbits(x) not in seen:
                seen.append(fbits(x))
        while len(seen) < 50:
            b = fbits(f32r(rnd.uniform(px[0] - 0.2 * abs(px[-1] - px[0]), px[-1] + 0.2 * abs(px[-1] - px[0]))))
            if b not in seen:
                seen.append(b)
        seen = seen[:max(50, len(seen))]
        first = len(o.cases)
        for xb in seen:
            o.add(pts, e['scaling'], xb)
        names.append((e, o.curve(pts), len(seen)))
    return names


# ------------------------------------------------------------------------------------------------ the live entry

def live(o, ship, names):
    """0xA11590 over a manager with one subscription per (curve, scaling, accumulate) and the root value set to x."""
    wanted = []              # (curve index, scaling, acc, [x bits])
    seen = {}

    def want(ci, scaling, acc, xs):
        key = (ci, scaling, acc)
        if key not in seen:
            seen[key] = len(wanted)
            wanted.append((ci, scaling, acc, []))
        wanted[seen[key]][3].extend(xs)
    by_curve = {}
    for c in o.cases:
        by_curve.setdefault((c[0], c[1]), []).append(c)
    for (e, ci, n) in names:
        xs = [c[3] for c in by_curve[(ci, e['scaling'])]][:16]
        want(ci, e['scaling'], 1, xs)
        want(ci, e['scaling'], 2, xs[:4])
    rnd = random.Random(5)
    picks = [k for k in by_curve if k[1] <= 255 and k not in {(ci, e['scaling']) for (e, ci, n) in names}]
    rnd.shuffle(picks)
    for (ci, scaling) in picks[:120]:
        cs = [c for c in by_curve[(ci, scaling)] if c[2] == 0]
        if not cs:
            continue
        xs = [c[3] for c in cs][:8]
        want(ci, scaling, 1, xs)
        want(ci, scaling, 2, xs[:3])
    subs, values = [], []
    for k, (ci, scaling, acc, xs) in enumerate(wanted):
        rid = 5000 + k
        values.append((rid, 0.0, True, 0.0))
        subs.append(Sub(0, k, 0, acc, [(rid, scaling, [(ff(x), ff(y), i) for (x, y, i) in o.curves[ci]])]))
    w = World([Node()], values, subs)
    key = w.e.alloc(0x20)
    w.e.w8(key + 0xC, 0xFF); w.e.w8(key + 0x10, 0xFF)
    res = []
    for k, (ci, scaling, acc, xs) in enumerate(wanted):
        for xb in xs:
            w.e.w32(w.vaddr[5000 + k] + 0x1C, xb)
            r = w.e.call(0xA11590, w.mgr, w.addr[0] + 0x10, k, key)
            res.append((ci, scaling, acc, xb, r))
    return res


# ------------------------------------------------------------------------------------------------ output

def h(v):
    return '0x%08XU' % (v & M32)


def main():
    o = Oracle()
    matrix(o)
    n_matrix = len(o.cases)
    y_edges(o)
    random_curves(o)
    ship = shipped()
    names = shipped_cases(o, ship)
    lv = live(o, ship, names)
    pool = [u32(a) for a in range(0xA15248, 0xA152C4, 4)][:31]
    assert len(o.cases) >= 1500
    out = []
    w = out.append
    w('// <auto-generated> by re-analysis/tools/emu/emu_curve.py from the engine\'s own output (the real 0xA14E28 and, for the live entry, 0xA11590 / 0xA17878 / 0xA17724 / 0xA17280 under Unicorn): do not edit. </auto-generated>')
    w('namespace Cozmo.Protocol.Tests;')
    w('')
    w('internal static class WwiseCurveOracle')
    w('{')
    w('    /// <summary>The literal pool 0xA15248..0xA152C0 (31 words) read from libcozmoEngine.so.</summary>')
    w('    public static readonly uint[] Pool = { %s };' % ', '.join(h(p) for p in pool))
    w('')
    w('    /// <summary>The curves: points flattened as (x bits, y bits, interp) triples.</summary>')
    w('    public static readonly uint[][] Curves =')
    w('    {')
    for c in o.curves:
        w('        new uint[] { %s },' % ', '.join(h(v) for t in c for v in t))
    w('    };')
    w('')
    w('    /// <summary>0xA14E28(curve, x, hint, &amp;idx) run by the engine: (curve, scaling word, hint, x bits) -> (result bits, idx).</summary>')
    w('    public static readonly (int Curve, uint Scaling, uint Hint, uint X, uint Result, uint Idx)[] Cases =')
    w('    {')
    for (ci, sc, hi, xb, r, idx) in o.cases:
        w('        (%d, %s, %s, %s, %s, %s),' % (ci, sc if sc < 256 else h(sc), h(hi) if hi > 255 else '%dU' % hi, h(xb), h(r), '%dU' % idx if idx < 1000 else h(idx)))
    w('    };')
    w('')
    w('    /// <summary>The 64 RTPC entries of the six shipped banks (HIRC walk): bank, object type, object id, RTPC id, source type, accumulate byte, parameter, curve id, scaling, curve index into <see cref="Curves"/>.</summary>')
    w('    public static readonly (string Bank, byte ObjType, uint Obj, uint Rtpc, byte Type, byte Acc, uint Param, uint CurveId, byte Scaling, int Curve)[] Shipped =')
    w('    {')
    for (e, ci, n) in names:
        w('        ("%s", %d, %s, %s, %d, %d, %d, %s, %d, %d),' % (e['bank'], e['objtype'], h(e['obj']), h(e['src']), e['type'], e['acc'], e['param'], h(e['curve']), e['scaling'], ci))
    w('    };')
    w('')
    w('    /// <summary>0xA11590 through a subscription holding one curve, root value x: (curve, scaling, accumulate byte, x bits) -> result bits (0.0f + y for the sum, 1.0f * y for the product).</summary>')
    w('    public static readonly (int Curve, uint Scaling, byte Acc, uint X, uint Result)[] Live =')
    w('    {')
    for (ci, sc, acc, xb, r) in lv:
        w('        (%d, %d, %d, %s, %s),' % (ci, sc, acc, h(xb), h(r)))
    w('    };')
    w('}')
    sys.stdout.write('\n'.join(out) + '\n')
    sys.stderr.write('curves %d, cases %d (matrix %d), shipped %d, live %d\n' % (len(o.curves), len(o.cases), n_matrix, len(names), len(lv)))


if __name__ == '__main__':
    main()
