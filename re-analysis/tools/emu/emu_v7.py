"""Run the engine's own per-voice state machine V7 (0xA54F1C) under Unicorn and write the golden values for WwiseVoiceStateOracleTests (B-M6b-4 batch 5k, M6-022, C41):

    python re-analysis/tools/emu/emu_v7.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseVoiceStateOracle.cs

The REAL code that runs: V7 itself (0xA54F1C..0xA55750 with its literals), 0xA4C584 (bit 2 of every connection), 0xA0228C / 0xA022E8 with its tail 0xA370E4 ([0x108DE78] -1; the virtual-voice acquire / release, bit 5 of [pbi+0x1BE] and the u16 counters of the PBI's list array
and the global at GOT 0x1040144's target), 0xA56650 (the source start latch, [src+0x10] bit 0), the PBI vtable slot +0x3C of the three classes (0x103B768 / 0x103D3B0: 0x9FF544; 0x1039D98: 0x9882E0) and the PBI context call vt+0x28 (0x9FF414 -> 0x9FF368).

The stand-ins (each logs its call and returns what the case says; every one is a callee outside V7 that C41 does not give or that has its own oracle):
  0xA4BC58   the per-connection gain update: logs (id, gain, arg5, arg12, whether the four float outputs share one buffer) and writes S2E, S2F and the four floats of the case (the callee's body is the C# UpdateConnectionGains, tested separately)
  0xA4B4B0   the ducking apply
  0xA54A30   the voice start / FX build: logs, returns the case's result and may store a new [voice+0xF0]
  0x99CC40   the unread callee of 0x9882E0 (the 0x9883AC class's vt+0x3C)
  0x9FFAD4   CalcEffectiveParams (the PBI context's vt+0x24 thunk 0xA000E0 reaches it)
  the voice vt+0x48 (0xA533FC) and vt+0x58 (0xA53698), the filter holder's vt+0xC / vt+0x10 / vt+0x14 / vt+0x18 and the source's vt+0x28 and vt+0x4C (the pure getter, bit 6 of byte [[src+0xC]+0x1BE]: it is the engine's own 0xA566C8 body written
  here as the getter: the case's source vtable points at the real 0xA566C8, so that slot also runs for real).

One line per run: the text before => is the input, the text after it is the engine's result (fields: see run_case / Case.parse in WwiseVoiceStateOracleTests). Floats are hex bits. The inputs are drawn from random.Random(seed).
"""
import random
import struct
import sys

sys.dont_write_bytecode = True
from emu_common import *
from unicorn import UcError
from unicorn.arm_const import *

M32 = 0xFFFFFFFF

VOICE = BASE + 0x10000
SRC = BASE + 0x20000
PBI = BASE + 0x30000
S = BASE + 0x40000
CONN = BASE + 0x50000          # connections 0x100 apart
LISTS = BASE + 0x60000         # the PBI's list objects 0x40 apart
LARR = BASE + 0x61000          # [pbi+0x1EC]: the array of pointers
GAINPAIR = BASE + 0x62000
NODE = BASE + 0x63000          # [voice+0x1C4] (not read: the holder is a stand-in)
STUB = BASE + 0x80000          # stub addresses (hooked), 0x10 apart
SRCVT = BASE + 0x70000
VOICEVT = BASE + 0x70100
HOLDVT = BASE + 0x70200
FLOATS = BASE + 0x62100        # the four float outputs' buffers' words are written to the stack frame by V7 itself

PBI_VPTR = {0: 0x103B768, 1: 0x1039D98, 2: 0x103D3B0}
CTX_VPTR = 0x103B7DC

assert True


def fb(x):
    return struct.unpack('<I', struct.pack('<f', x))[0]


FLOATS_POOL = [0.0, -0.0, 1.0, -1.0, 0.5, 0.25, 2.0, 100.0, 101.0, -37.0, 50.0, 1e-6, 1e30, float('inf'), float('-inf'), float('nan'), 0.125, 3.5, -3.5, 1023.5, 2.5, 1.5, -0.5, 0.75, 1e9, -1e9, 36.9, -37.1, 740.0, -740.0]


def rfloat(rnd):
    r = rnd.random()
    if r < 0.5:
        return fb(rnd.choice(FLOATS_POOL))
    if r < 0.75:
        return fb(rnd.uniform(-120.0, 120.0))
    if r < 0.9:
        return fb(rnd.uniform(-2.0, 2.0))
    return rfinite_bits(rnd)


def rfinite_bits(rnd):
    """A random binary32 that is not a NaN (the engine's NaN payload / sign rules and the host's differ for the invalid operations; one canonical NaN 0x7FC00000 is drawn explicitly) and not an infinity."""
    while True:
        b = rnd.getrandbits(32)
        if (b >> 23) & 0xFF != 0xFF:
            return b


def rfinite(rnd):
    r = rnd.random()
    if r < 0.5:
        return fb(rnd.choice([0.0, 1.0, -1.0, 0.5, 10.0, 50.0, 100.0, -5.0, 150.0, 3.25]))
    if r < 0.85:
        return fb(rnd.uniform(-30.0, 130.0))
    return rfinite_bits(rnd)


def rtarget(rnd):
    r = rnd.random()
    if r < 0.35:
        return fb(rnd.choice([0.0, 100.0, 50.0, 10.0, 150.0, -5.0, 1.0, 99.5, float('nan')]))
    return fb(rnd.uniform(-20.0, 140.0))


class World(Emu):
    def __init__(self):
        super().__init__()
        self.std_hooks()
        self.events = []
        self.case = None
        self.nstub = 0
        self.setup_stubs()

    def stub(self, fn):
        addr = STUB + 0x10 * self.nstub
        self.nstub += 1
        self.hook(addr, fn)
        return addr

    def setup_stubs(self):
        e = self

        def a4bc58(em):
            c = em.case
            sp = em.reg_sp()
            arg5 = em.r32(sp)
            pe, pf = em.r32(sp + 4), em.r32(sp + 8)
            fl = [em.r32(sp + 12 + 4 * k) for k in range(4)]
            a12 = em.r32(sp + 0x1C)
            voice, ctx, rid, gain = em.reg(0), em.reg(1), em.reg(2), em.reg(3)
            n = c['gcalls']
            c['gcalls'] += 1
            gc = c['gains'][min(n, len(c['gains']) - 1)]
            share = 1 if len(set(fl)) == 1 else 0
            a12s = '-'
            if a12:
                a12s = '%x.%x' % (em.r32(a12), em.r32(a12 + 4))
            em.events.append('G %x %x %x %x %s %d' % (ctx - PBI, rid, gain, arg5 & 0xFF, a12s, share))
            em.w8(pe, gc[0])
            em.w8(pf, gc[1])
            for k in range(4):
                em.w32(fl[k], gc[2 + k])
            return None
        self.hook(0xA4BC58, a4bc58)

        def a4b4b0(em):
            em.events.append('D')
        self.hook(0xA4B4B0, a4b4b0)

        def a54a30(em):
            c = em.case
            em.events.append('B')
            if c['newf0'] is not None:
                em.w32(VOICE + 0xF0, c['newf0'])
            return c['a54a30']
        self.hook(0xA54A30, a54a30)

        def a99cc40(em):
            c = em.case
            em.events.append('X')
            em.w32(em.reg(2), c['x_a'])
            em.w32(em.reg(3), c['x_b'])
            return c['x_res']
        self.hook(0x99CC40, a99cc40)
        self.hook(0x9FFAD4, lambda em: em.events.append('C'))

        # source vtable: +0x28 StartStream stub, +0x4C the real getter 0xA566C8
        self.w32(SRCVT + 0x28, self.stub(self.src28))
        self.w32(SRCVT + 0x4C, 0xA566C8)
        # voice vtable
        self.w32(VOICEVT + 0x48, self.stub(lambda em: em.events.append('S')))
        self.w32(VOICEVT + 0x58, self.stub(lambda em: em.events.append('V')))
        # holder vtable
        self.w32(HOLDVT + 0x0C, self.stub(lambda em: em.events.append('H0C')))
        self.w32(HOLDVT + 0x10, self.stub(self.h10))
        self.w32(HOLDVT + 0x14, self.stub(lambda em: em.events.append('H14 %x' % em.reg(1))))
        self.w32(HOLDVT + 0x18, self.stub(self.h18))

    def src28(self, em):
        em.events.append('R %x %x' % (em.reg(1), em.reg(2)))
        return em.case['src28']

    def h10(self, em):
        s = em.r32(em.reg(1))
        em.events.append('H10 %x' % s)
        return em.case['h10']

    def h18(self, em):
        em.events.append('H18 %x %x' % (em.reg(1), em.reg(2)))
        return em.case['h18']


def build_case(rnd, w):
    c = {}
    c['cls'] = rnd.choice([0, 0, 0, 0, 1, 2])
    c['e4'] = rnd.choice([0, 0, 1, 2, 2, 2, 3])
    c['e0'] = rnd.choice([0, 1, 1, 2, 3])
    c['cd'] = rnd.getrandbits(8)
    if rnd.random() < 0.5:
        c['cd'] &= ~8
    c['e8'] = rnd.choice([0, 1, 1, 0xA, 0x21, 0x20, 0x2C, 0x5D, 0x7D])
    c['ve8'] = rnd.choice([0, 1, 1, 0xE, 0xF])
    c['f0'] = rnd.choice([0, 0x4101, 0x3102, rnd.getrandbits(32)])
    c['has1b4'] = rnd.choice([0, 0, 1])
    c['nconn'] = rnd.choice([0, 1, 1, 2, 3])
    c['connflags'] = [rnd.getrandbits(8) for _ in range(c['nconn'])]
    c['ramps'] = [(rfinite(rnd), rfinite(rnd), rnd.choice([0, 1, 8, 0xFFFF, rnd.getrandbits(16)]), rnd.choice([0, 1])) for _ in range(4)]
    c['src10'] = rnd.choice([0, 1])
    c['gp'] = None if rnd.random() < 0.4 else (rfloat(rnd), rfloat(rnd))
    c['f1f8'] = rnd.choice([M32, M32, M32, 0, 5, 0x80000000])
    c['f54'] = rfloat(rnd)
    c['f11c'] = rfloat(rnd)
    if rnd.random() < 0.5:
        c['f54'] = fb(rnd.uniform(-60.0, 10.0))
        c['f11c'] = fb(rnd.uniform(-20.0, 20.0))
    c['b58'] = rnd.getrandbits(8)
    c['f68'] = rtarget(rnd)
    c['f6c'] = rtarget(rnd)
    c['f164'] = rfloat(rnd) if rnd.random() < 0.5 else fb(rnd.choice([1.0, 1.0, 0.5, 2.0, 0.0, -1.0, 0.25, 1.5, 0.001]))
    c['pe9'] = rnd.choice([0, 1, 1, 4, 5])
    c['b1be'] = rnd.choice([0, 0x40, 0x04, 0x14, 0x20, 0x60, rnd.getrandbits(8)])
    c['f1d8'] = rnd.choice([M32, M32, M32, 0x80000000, 0, 3, 100, 1023, 1024, 1025, 0x7FFFFFFF, 5000, rnd.getrandbits(32)])
    c['f1dc'] = rnd.getrandbits(32)
    c['f1e0'] = rnd.getrandbits(32)
    c['f4'] = rnd.choice([0, 0x10, 0x100000, 0x110000, rnd.getrandbits(32)])
    c['f140'] = rnd.getrandbits(32)
    c['c4'] = rfloat(rnd)
    c['f168'] = fb(rnd.choice([1.0, 0.5, 0.0, 2.0]))
    c['f16c'] = fb(rnd.choice([1.0, 0.25, 0.0, 3.0]))
    c['f98'] = rfinite(rnd)
    c['f118'] = rfinite(rnd)
    c['nlists'] = rnd.choice([0, 1, 2])
    c['lists'] = [rnd.getrandbits(16) for _ in range(c['nlists'])]
    c['maxf'] = rnd.choice([1024, 1024, 1024, 0, 1, 7, 512, 0xFFFF, rnd.getrandbits(16)])
    c['chan'] = rnd.getrandbits(32)
    c['code28'] = rnd.choice([0x2B, 0x2D, 0x11, 2])
    c['hasbus'] = rnd.choice([0, 0, 1])
    c['gcalls'] = 0
    c['gains'] = [(rnd.choice([0, 1, 1]), rnd.choice([0, 1, 1, 1]), rtarget(rnd), rtarget(rnd), rtarget(rnd), rtarget(rnd)) for _ in range(2)]
    c['x_res'] = rnd.choice([0, 1, 7])
    c['x_a'] = rnd.getrandbits(32)
    c['x_b'] = rnd.getrandbits(32)
    c['h10'] = rnd.choice([0, 1, 2, 0x11, 0x2D, 0x2B])
    c['h18'] = rnd.choice([0, 1, 1, 2, 5])
    c['src28'] = rnd.choice([1, 1, 0x3F, 2, 0, 7])
    c['a54a30'] = rnd.choice([1, 1, 1, 2, 0x34]) if c['cls'] == 0 else rnd.choice([2, 0x34, 0, 5])
    c['newf0'] = rnd.choice([None, None, 0x4101, rnd.getrandbits(32)])
    return c


def run_case(w, c):
    e = w
    e.case = c
    e.events = []
    e.uc.mem_write(VOICE, bytes(0x600))
    e.uc.mem_write(SRC, bytes(0x40))
    e.uc.mem_write(PBI, bytes(0x240))
    e.uc.mem_write(S, bytes(0x40))
    e.uc.mem_write(CONN, bytes(0x400))
    e.uc.mem_write(LISTS, bytes(0x100))
    e.uc.mem_write(LARR, bytes(0x20))
    e.uc.mem_write(GAINPAIR, bytes(0x10))
    # voice
    e.w32(VOICE, VOICEVT)
    e.w32(VOICE + 0xD4, SRC)
    e.w8(VOICE + 0xCD, c['cd'])
    e.w32(VOICE + 0xE0, c['e0'])
    e.w32(VOICE + 0xE4, c['e4'])
    e.w8(VOICE + 0xE8, c['ve8'])
    e.w32(VOICE + 0xF0, c['f0'])
    e.w32(VOICE + 0x1B4, PBI if c['has1b4'] else 0)
    e.w32(VOICE + 0x1C0, HOLDVT)
    e.w32(VOICE + 0x1C4, NODE)
    for k in range(c['nconn']):
        a = CONN + 0x100 * k
        e.w8(a + 0x6C, c['connflags'][k])
        e.w32(a + 0x28, CONN + 0x100 * (k + 1) if k + 1 < c['nconn'] else 0)
    e.w32(VOICE + 0x28, CONN if c['nconn'] else 0)
    for (base, tgt_off), r in zip(((0x340, 0x344), (0x510, 0x514), (0x350, 0x354), (0x520, 0x524)), c['ramps']):
        e.w32(VOICE + base, r[0])
        e.w32(VOICE + base + 4, r[1])
        e.w16(VOICE + base + 8, r[2])
        e.w8(VOICE + base + 0xB, r[3])
    # source
    e.w32(SRC, SRCVT)
    e.w32(SRC + 8, GAINPAIR if c['gp'] else 0)
    if c['gp']:
        e.w32(GAINPAIR, c['gp'][0])
        e.w32(GAINPAIR + 4, c['gp'][1])
    e.w32(SRC + 0xC, PBI)
    e.w8(SRC + 0x10, c['src10'])
    # pbi
    e.w32(PBI, PBI_VPTR[c['cls']])
    e.w32(PBI + 4, c['f4'])
    e.w32(PBI + 0xC, CTX_VPTR)
    e.w32(PBI + 0x3C, 0)
    e.w32(PBI + 0x54, c['f54'])
    e.w8(PBI + 0x58, c['b58'])
    e.w32(PBI + 0x68, c['f68'])
    e.w32(PBI + 0x6C, c['f6c'])
    e.w32(PBI + 0x98, c['f98'])
    e.w32(PBI + 0x118, c['f118'])
    e.w32(PBI + 0x11C, c['f11c'])
    e.w8(PBI + 0xE8, c['e8'])
    e.w8(PBI + 0xE9, c['pe9'])
    e.w32(PBI + 0xC4, c['c4'])
    e.w32(PBI + 0x140, c['f140'])
    e.w32(PBI + 0x164, c['f164'])
    e.w32(PBI + 0x168, c['f168'])
    e.w32(PBI + 0x16C, c['f16c'])
    e.w8(PBI + 0x1BE, c['b1be'])
    e.w32(PBI + 0x1D8, c['f1d8'])
    e.w32(PBI + 0x1DC, c['f1dc'])
    e.w32(PBI + 0x1E0, c['f1e0'])
    e.w32(PBI + 0x1F8, c['f1f8'])
    e.w32(PBI + 0x1EC, LARR if c['nlists'] else 0)
    e.w32(PBI + 0x1F0, c['nlists'])
    e.w32(PBI + 0x1F4, 4)
    for k in range(c['nlists']):
        e.w32(LARR + 4 * k, LISTS + 0x40 * k)
        e.w16(LISTS + 0x40 * k + 0x22, c['lists'][k])
    e.w32(PBI + 0x10C, 0)
    e.w32(PBI + 0x110, 0)
    # S
    e.w32(S + 4, c['chan'])
    e.w16(S + 0xC, c['maxf'])
    e.w32(S + 0x28, c['code28'])
    e.w8(S + 0x2C, c['hasbus'])
    g0 = e.r32(0x108DE78)
    ret = None
    try:
        ret = e.call(0xA54F1C, VOICE, S)
    except UcError as ex:
        return None, 'ERR %s' % ex
    # outputs
    out = []
    out.append('r=%d' % (ret & 0xFFFFFFFF))
    out.append('S=%x.%x.%d' % (e.r32(S + 4), e.r32(S + 0x28), e.r8(S + 0x2C)))
    out.append('V=%x.%x.%x.%x' % (e.r8(VOICE + 0xCD), e.r8(VOICE + 0xE8) & 1, e.r32(VOICE + 0xF0), 1 if e.r32(VOICE + 0x1B4) else 0))
    out.append('Rm=' + '.'.join('%x:%x:%x:%x' % (e.r32(VOICE + b), e.r32(VOICE + b + 4), e.r16(VOICE + b + 8), e.r8(VOICE + b + 0xB)) for b in (0x340, 0x510, 0x350, 0x520)))
    out.append('P=%x.%x.%x.%x.%x.%x.%x' % (e.r32(PBI + 0x1D8), e.r32(PBI + 0xC4), e.r8(PBI + 0x1BE), e.r8(PBI + 0xE9), e.r32(PBI + 0x3C), e.r32(PBI + 0x40), e.r8(PBI + 0xE8)))
    out.append('Q=%x.%x.%x' % (e.r32(PBI + 0x1B4), e.r8(PBI + 0x1BD), e.r8(SRC + 0x10)))
    out.append('C=' + ('.'.join('%x' % e.r8(CONN + 0x100 * k + 0x6C) for k in range(c['nconn'])) or '-'))
    out.append('L=' + ('.'.join('%x' % e.r16(LISTS + 0x40 * k + 0x22) for k in range(c['nlists'])) or '-') + '.%x' % ((e.r32(0x108DE78) - g0) & M32))
    evs = [x for x in e.events if x != 'H0C']
    out.append('E=' + (','.join(evs) or '-').replace(' ', '_'))
    out.append('Z=%d' % (1 if 'H0C' in e.events else 0))
    return ret, ' '.join(out)


def fmt_input(c):
    """key=value fields separated by spaces; the lists inside a value are separated by dots (colons inside an item)."""
    p = []
    p.append('cls=%d e4=%d e0=%d cd=%x ve8=%x f0=%x h1b4=%d' % (c['cls'], c['e4'], c['e0'], c['cd'], c['ve8'], c['f0'], c['has1b4']))
    p.append('conn=' + ('.'.join('%x' % f for f in c['connflags']) or '-'))
    p.append('ramps=' + '.'.join('%x:%x:%x:%x' % r for r in c['ramps']))
    p.append('src10=%d' % c['src10'])
    p.append('gp=' + ('-' if not c['gp'] else '%x:%x' % c['gp']))
    for k in ('f1f8', 'f54', 'f11c', 'b58', 'f68', 'f6c', 'f164', 'pe9', 'b1be', 'f1d8', 'f1dc', 'f1e0', 'f4', 'f140', 'c4', 'f168', 'f16c', 'e8', 'f98', 'f118'):
        p.append('%s=%x' % (k, c[k]))
    p.append('lists=' + ('.'.join('%x' % l for l in c['lists']) or '-'))
    p.append('S=%x:%x:%x:%d' % (c['maxf'], c['chan'], c['code28'], c['hasbus']))
    p.append('gains=' + '.'.join('%d:%d:%x:%x:%x:%x' % g for g in c['gains']))
    p.append('x=%x:%x:%x' % (c['x_res'], c['x_a'], c['x_b']))
    p.append('h10=%x h18=%x src28=%x a54a30=%x nf0=%s' % (c['h10'], c['h18'], c['src28'], c['a54a30'], '-' if c['newf0'] is None else '%x' % c['newf0']))
    return ' '.join(p)


def main():
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 20261005
    count = int(sys.argv[2]) if len(sys.argv) > 2 else 6000
    rnd = random.Random(seed)
    w = World()
    lines = []
    for k in range(count):
        c = build_case(rnd, w)
        ret, out = run_case(w, c)
        if ret is None:
            sys.stderr.write('case %d: %s\n' % (k, out))
            continue
        lines.append('V %s => %s' % (fmt_input(c), out))
    out = []
    o = out.append
    o("// <auto-generated> by re-analysis/tools/emu/emu_v7.py from the engine's own output (the real V7 0xA54F1C with 0xA4C584, 0xA0228C / 0xA022E8, 0xA56650 and the PBI vt+0x3C / vt+0x28 slots under Unicorn, seed %d, %d cases): do not edit. </auto-generated>" % (seed, len(lines)))
    o('namespace Cozmo.Protocol.Tests;')
    o('')
    o('internal static class WwiseVoiceStateOracle')
    o('{')
    o('    private static void Add(List<string> l, ReadOnlySpan<byte> chunk)')
    o('    {')
    o("        foreach (var row in System.Text.Encoding.UTF8.GetString(chunk).Split('\\n', StringSplitOptions.RemoveEmptyEntries)) l.Add(row);")
    o('    }')
    o('')
    o("    /// <summary>One engine run of V7 per line: the text before <c>=&gt;</c> is the input, the text after it the engine's result (see emu_v7.py). Floats are hex bits. The rows are UTF-8 data-section literals (CS8103: the user-string budget of the test assembly).</summary>")
    o('    public static readonly string[] Lines = MakeLines();')
    o('')
    o('    private static string[] MakeLines()')
    o('    {')
    o('        var l = new List<string>();')
    for i in range(0, len(lines), 100):
        part = lines[i:i + 100]
        o('        Add(l,')
        for j, r in enumerate(part):
            o('            "%s\\n"u8%s' % (r, ' +' if j < len(part) - 1 else ');'))
    o('        return l.ToArray();')
    o('    }')
    o('}')
    sys.stdout.write(chr(10).join(out) + chr(10))
    sys.stderr.write('V: %d lines\n' % len(lines))


if __name__ == '__main__':
    main()
