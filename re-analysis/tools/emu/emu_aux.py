"""Run the engine's own aux-send route and PostEvent entry writers under Unicorn and write the golden values for WwiseAuxRouteTests (B-M6b-4 batch 5i, M6-010 / M6-006 / M6-022 / M6-025, C40):

    python re-analysis/tools/emu/emu_aux.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseAuxOracle.cs

The REAL functions run, on hand-built engine objects in emulated memory:

  B  0x9BD368   the send builder (ctx, out): game-defined sends (kind 1) and user-defined sends (kind 2), the two threshold globals [0x1052454] / [0x1052450], the fast power, the terminator rule
  M  0x9D4228   the merge of the sends into the voice's 0x14-byte entries (voice+0x2C), with the stack-fill dependence of the appended entries' flag bytes made explicit: every case runs with a given 8-byte fill of
                the flag area sp+0..7 and records whether the engine READ a flag byte it had not written (an uninitialised read)
  D  0x9D4108   the per-entry dispatch: the bus lookup 0x9A7EB0 (stand-in), the device list [0x108DAE8]+8, bit 6 of [bus+0xCC], the call 0xA43434 (stand-in: logged)
  L  0xA43434   the line find-or-create and the connection call: the real scan with the real 0xA68A2C; 0xA429F0 (create) and 0xA4C280 (connect) are stand-ins that log their arguments, and the Sound's bus
                is returned by a stand-in for the vtable slot +0x88
  W  0xA447D8..0xA44938   the aux walk and the dry walk of the voice pass (entered at 0xA447D8, left at the epilogue 0xA44714): 0xA4FBEC (the mix) and 0xA4C60C (filter B) are stand-ins that log their arguments,
                0xA68A2C is the real one
  E  0xA03108   the playing-id entry writer (the real body; the pool allocator and 0xA1C63C are stand-ins), with a bucket table that links without growing
  P  0x9A0EF8   the AK PostEvent core, as far as its arguments to 0xA03108 (a stand-in that logs them)
  G  0xA0BA3C   the game object's aux values store (compaction), H 0xA0BA28 its listener mask store
  S  0x9A080C   the send-threshold setter (powf hooked correctly rounded): [0x1052454] = max(powf(10, 0.05 v), fast power), [0x1052450] = v
  T  0x8D8CE4   the Anki PostEvent wrapper (Thumb): the flags and callback it passes to 0x9A6704 for every context word
  N  0xA0428C   the node notification of a playing id: the entry lookup, the callback(0x20, {cookie, game object, id, event id}) and its lock protocol
  K  0xA68A2C   the line key: bus ? [bus+8] : -(u8)byte
  C  0x9BC90C   the PBI context init: the node chain test of [x+0x40] & 0xE0000, [GO+0x7C]++ and the node AddRef (stand-in: logged)

The expected values in WwiseAuxRouteTests are this script's output, never the C#'s. Floats are compared as bits.
"""
import base64
import math
import gzip
import random
import struct
import sys

sys.dont_write_bytecode = True
from emu_common import *
from unicorn import UcError, UC_HOOK_MEM_READ, UC_HOOK_MEM_WRITE

M32 = 0xFFFFFFFF
SENTINEL = 0xAA


def fbits(x):
    return struct.unpack('<I', struct.pack('<f', x))[0]


FLOAT_POOL = [0.0, -0.0, 1.0, -1.0, 0.5, 0.25, 2.0, 100.0, 1e-6, 1e-30, 1e30, float('inf'), float('-inf'), float('nan'), 0.0001, 0.99903899, 3.4e38, -3.4e38]


def rfloat(rnd):
    r = rnd.random()
    if r < 0.45:
        return rnd.choice(FLOAT_POOL)
    if r < 0.7:
        return rnd.uniform(-2.0, 2.0)
    if r < 0.85:
        return rnd.uniform(0.0, 1.0)
    return struct.unpack('<f', struct.pack('<I', rnd.getrandbits(32)))[0]


def rdb(rnd):
    """A dB value: around 0, around the -740 / -80 boundaries, large and special."""
    r = rnd.random()
    if r < 0.25:
        return rnd.choice([0.0, -6.0206, -20.0, -80.0, -79.99, -80.01, -740.0, -739.99, -740.01, -741.0, -800.0, 6.0, 100.0, 2000.0, 4000.0, 5000.0, float('nan'), float('inf'), float('-inf')])
    if r < 0.6:
        return rnd.uniform(-100.0, 10.0)
    if r < 0.8:
        return rnd.uniform(-900.0, -600.0)
    return struct.unpack('<f', struct.pack('<I', rnd.getrandbits(32)))[0]


GLOBAL_PAIRS = [(0x38D1B717, fbits(-80.0)), (0x37800000, 0xC2C0999A)]


# ------------------------------------------------------------------------------------------------ B: 0x9BD368

CTX = BASE + 0x1000
GO = BASE + 0x2000
OUT = BASE + 0x3000


def run_builder(e, flag, db84, go_pairs, uids, udbs, tlin, traw):
    e.uc.mem_write(CTX, bytes(0x100))
    e.uc.mem_write(GO, bytes(0x100))
    e.w8(CTX + 0x88, flag)
    e.w32(CTX + 8, GO)
    e.w32(CTX + 0x84, db84)
    for i in range(4):
        e.w32(GO + 0x24 + 8 * i, go_pairs[i][0])
        e.w32(GO + 0x28 + 8 * i, go_pairs[i][1])
        e.w32(CTX + 0x74 + 4 * i, uids[i])
        e.w32(CTX + 0x64 + 4 * i, udbs[i])
    e.w32(0x1052454, tlin)
    e.w32(0x1052450, traw)
    e.uc.mem_write(OUT, bytes([SENTINEL]) * 0x70)
    e.call(0x9BD368, CTX, OUT)
    data = bytes(e.uc.mem_read(OUT, 0x70))
    words = [struct.unpack_from('<III', data, 12 * k) for k in range(9)]
    n = 0
    while n < 8 and words[n][0] != 0 and not (words[n][0] == 0xAAAAAAAA and words[n][1] == 0xAAAAAAAA):
        n += 1
    # the terminator is the id word 0 right after the entries; anything past it must still be the sentinel
    term = 1 if n < 8 and words[n][0] == 0 else 0
    used = 12 * n + (4 if term else 0)
    assert data[used:] == bytes([SENTINEL]) * (0x70 - used), 'the builder wrote past the entries and the terminator'
    return n, term, [words[k] for k in range(n)]


def builder_cases(e, rnd, count):
    out = []
    # the shapes the inventory names first
    shapes = []
    for k in range(count):
        flag = rnd.choice([0, 1, 1, 1, 2, 0x80])
        db84 = fbits(rdb(rnd)) if rnd.random() < 0.7 else fbits(rnd.choice([0.0, -6.0206, -20.0, 0.0]))
        pairs = []
        for i in range(4):
            if rnd.random() < 0.22:
                pid = 0
            else:
                pid = rnd.choice([0x1111, 0x2222, 0x3333, 0x4444, rnd.getrandbits(32) or 1])
            pairs.append((pid, fbits(rfloat(rnd))))
        if rnd.random() < 0.3:
            pairs[0] = (pairs[0][0] or 0x77, pairs[0][1])           # make the first id non-zero often
        if rnd.random() < 0.12:
            pairs[0] = (0, pairs[0][1])                             # first id zero with later pairs non-zero
        uids = [0 if rnd.random() < 0.25 else rnd.choice([0x5555, 0x6666, 0x7777, rnd.getrandbits(32) or 1]) for _ in range(4)]
        udbs = [fbits(rdb(rnd)) for _ in range(4)]
        if rnd.random() < 0.25:                                     # make the all-stored count-8 shape common
            flag = 1
            db84 = fbits(rnd.uniform(-10.0, 5.0))
            pairs = [(0x100 + i, fbits(rnd.uniform(0.2, 1.5))) for i in range(4)]
            uids = [0x200 + i for i in range(4)]
            udbs = [fbits(rnd.uniform(-70.0, 3.0)) for _ in range(4)]
        tlin, traw = rnd.choice(GLOBAL_PAIRS) if rnd.random() < 0.85 else (fbits(rfloat(rnd)), fbits(rdb(rnd)))
        n, term, items = run_builder(e, flag, db84, pairs, uids, udbs, tlin, traw)
        line = 'B %x %x %s %s %s %x %x => %d %d %s' % (
            flag, db84,
            ' '.join('%x.%x' % p for p in pairs),
            ' '.join('%x' % u for u in uids),
            ' '.join('%x' % d for d in udbs), tlin, traw, n, term,
            ' '.join('%x.%x.%x' % it for it in items) or '-')
        out.append(line)
    return out


# ------------------------------------------------------------------------------------------------ M: 0x9D4228

BLK = BASE + 0x4000
ARR = BASE + 0x5000
CNT = BASE + 0x5100
VOICE = BASE + 0x6000
CTXP = BASE + 0x6100
GO2 = BASE + 0x6200
SP0 = (STACK + 0xE0000 - 4) & ~7               # e.call(): one stack argument
FLAGS = SP0 - 0x90                              # push of 9 registers (36) and sub sp,#0x6c: sp+0..7 of 0x9D4228


class MergeRig:
    def __init__(self, e):
        self.e = e
        self.dispatches = []
        self.written = set()
        self.uninit = False
        e.hook(0x9D4108, self.on_dispatch)
        e.uc.hook_add(UC_HOOK_MEM_WRITE, self.on_write, begin=FLAGS, end=FLAGS + 7)
        e.uc.hook_add(UC_HOOK_MEM_READ, self.on_read, begin=FLAGS, end=FLAGS + 7)

    def on_dispatch(self, e):
        entry = bytes(e.uc.mem_read(e.reg(1), 0x14))
        self.dispatches.append((e.reg(0), struct.unpack('<5I', entry), e.reg(2) & 0xFF))
        return None

    def on_write(self, uc, access, address, size, value, _):
        for a in range(address, address + size):
            self.written.add(a - FLAGS)

    def on_read(self, uc, access, address, size, value, _):
        for a in range(address, address + size):
            if (a - FLAGS) not in self.written:
                self.uninit = True

    def run(self, flag, fill, old, block, mask):
        e = self.e
        e.uc.mem_write(FLAGS, bytes(fill))
        self.written = set()
        self.uninit = False
        self.dispatches = []
        e.uc.mem_write(VOICE, bytes(0x100))
        e.w32(VOICE + 8, CTXP)
        e.w32(CTXP + 8, GO2)
        e.uc.mem_write(GO2, bytes(0x40))
        e.w8(GO2 + 0x22, mask)
        e.uc.mem_write(ARR, bytes(0xA0))
        for i, en in enumerate(old):
            e.uc.mem_write(ARR + 0x14 * i, struct.pack('<5I', *en))
        e.w8(CNT, len(old))
        e.uc.mem_write(BLK, bytes([SENTINEL]) * 0x70)
        for k, (bid, gain, kind) in enumerate(block):
            e.uc.mem_write(BLK + 12 * k, struct.pack('<III', bid, gain, kind))
        if len(block) < 8:
            e.w32(BLK + 12 * len(block), 0)
        e.call(0x9D4228, BLK, ARR, flag, CNT, stack=[VOICE])
        n = e.r8(CNT)
        arr = [struct.unpack('<5I', bytes(e.uc.mem_read(ARR + 0x14 * i, 0x14))) for i in range(8)]
        return n, arr, list(self.dispatches), self.uninit


def merge_cases(e, rnd, count):
    rig = MergeRig(e)
    out = []
    for _ in range(count):
        flag = rnd.choice([0, 1])
        nold = rnd.choice([0, 0, 1, 2, 3, 4, 5, 6, 7, 8])
        ids = rnd.choice([[1, 2], [1, 2, 3], [1, 2, 3, 4, 5], [7]])
        old = []
        for _ in range(nold):
            tgt = rnd.choice([fbits(rnd.uniform(0.1, 1.0)), fbits(rnd.uniform(0.1, 1.0)), 0, fbits(-0.5), 0x7FC00000, fbits(1e-30), 0x80000000])
            old.append((tgt, fbits(rnd.uniform(0.0, 1.0)), 0xFFFFFFFF, rnd.choice(ids), rnd.choice([1, 2])))
        k = rnd.choice([0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 8])
        bids = rnd.choice([[1, 2], [1, 2, 3], [1, 2, 3, 4, 5], [7], [9, 10, 11]])
        block = [(rnd.choice(bids), fbits(rnd.uniform(0.0, 1.0)), rnd.choice([1, 2])) for _ in range(k)]
        if rnd.random() < 0.3:
            block = [(100 + i, fbits(rnd.uniform(0.0, 1.0)), 1) for i in range(k)]      # distinct ids: nothing matches
        fill = [rnd.choice([0, 0, 1, 0xFF, rnd.getrandbits(8)]) for _ in range(8)]
        mask = rnd.getrandbits(8)
        n, arr, disp, uninit = rig.run(flag, fill, old, block, mask)
        assert len(disp) == n, (len(disp), n)
        for i, (v, ent, m) in enumerate(disp):
            assert v == VOICE and ent == arr[i] and m == mask
        line = 'M %d %s %d %s %d %s %x => %d %d %s' % (
            flag, ''.join('%02x' % b for b in fill),
            len(old), ' '.join('.'.join('%x' % w for w in en) for en in old) or '-',
            len(block), ' '.join('%x.%x.%x' % b for b in block) or '-', mask,
            n, 1 if uninit else 0, ' '.join('.'.join('%x' % w for w in en) for en in arr))
        out.append(line)
    return out




# ------------------------------------------------------------------------------------------------ D: 0x9D4108

DEVLIST = 0x108DAFC                              # the device list struct the GOT word 0x10400B8 points at (0x9D4118..0x9D4158; field +0x14 of the output-device state 0x108DAE8): [list+8] = the head device; a device: +4 next, +0x10 lo, +0x14 hi, +0x18 listener mask
DEVS = BASE + 0xA000
BUSD = BASE + 0xA800
VTD = BASE + 0xA900
RELA = RET + 0x100                               # the stand-in for bus->vt+0xC (the release)
ENT = BASE + 0xAA00
VOICEX = BASE + 0xAB00


def run_dispatch(e, bit6, mask, devices, bus_found, ent_id, calls, rel):
    e.uc.mem_write(BUSD, bytes(0x100))
    e.w32(BUSD, VTD)
    e.w32(VTD + 0xC, RELA)
    e.w8(BUSD + 0xCC, 0x40 if bit6 else 0x00)
    e.uc.mem_write(DEVS, bytes(0x40 * 8))
    head = 0
    for i in reversed(range(len(devices))):
        a = DEVS + 0x40 * i
        e.w32(a + 4, head)
        e.w32(a + 0x10, devices[i][0])
        e.w32(a + 0x14, devices[i][1])
        e.w32(a + 0x18, devices[i][2])
        head = a
    e.w32(DEVLIST + 8, head)
    e.uc.mem_write(ENT, bytes(0x20))
    e.w32(ENT + 0xC, ent_id)
    calls.clear()
    rel.clear()

    def lookup(emu):
        calls.append(('lookup', emu.reg(1), emu.reg(2)))
        return BUSD if bus_found else 0

    def link(emu):
        arg5 = struct.unpack('<I', bytes(emu.uc.mem_read(emu.reg_sp(), 4)))[0]
        calls.append(('link', emu.reg(0) == BUSD, emu.reg(1) == ENT, emu.reg(2), emu.reg(3), arg5 == VOICEX))
        return None

    e.hook(0x9A7EB0, lookup)
    e.hook(0xA43434, link)
    e.hook(RELA, lambda emu: (rel.append(emu.reg(0) == BUSD) or None))
    try:
        e.call(0x9D4108, VOICEX, ENT, mask)
        return 0
    except UcError:
        return 1


def dispatch_cases(e, rnd, count):
    out = []
    calls, rel = [], []
    ids = [(2, 0), (3, 0), (2, 1), (1, 0), (0, 0), (5, 7), (2, 0)]
    for _ in range(count):
        nd = rnd.choice([0, 1, 2, 3, 4, 5])
        devices = [(*rnd.choice(ids), rnd.choice([0, 1, 2, 3, 0xFF, rnd.getrandbits(8)])) for _ in range(nd)]
        bit6 = rnd.choice([0, 1])
        mask = rnd.choice([1, 2, 3, 0xFF, 0x10, rnd.getrandbits(8)])
        found = rnd.random() < 0.9
        eid = rnd.getrandbits(32)
        crash = run_dispatch(e, bit6, mask, devices, found, eid, calls, rel)
        res = []
        for c in calls:
            if c[0] == 'lookup':
                assert c[1] == eid and c[2] == 1
            else:
                assert c[1] and c[2] and c[5]
                res.append('%x.%x' % (c[3], c[4]))
        line = 'D %d %x %d %s => %s %d %d' % (
            bit6, mask, 1 if found else 0, ' '.join('%x.%x.%x' % d for d in devices) or '-',
            ' '.join(res) or '-', len(rel), crash)
        out.append(line)
    return out


# ------------------------------------------------------------------------------------------------ L: 0xA43434

LINEARR = 0x108DF54                              # {data pointer [0], count [4]}
LNARR = BASE + 0xB000
LINES = BASE + 0x10000
BUSES = BASE + 0xC000
LENT = BASE + 0xD000
LVOICE = BASE + 0xD100
LCTX = BASE + 0xD200
LNODE = BASE + 0xD300
LVT = BASE + 0xD400
CBUSM = BASE + 0xD700
NEWLINE = BASE + 0xE000
CONNS = BASE + 0xD800
FN88 = RET + 0x200
RELB = RET + 0x300


def run_link(e, buses, lines, aux_bus, key2, kind, lo, hi, conns, ctx_bus, owner_null, create_ok):
    """buses: (id, bit6). lines: (bus index or -1, key2, byte, lo, hi, state). conns: the line index of each connection of the voice, head first (-1 = the line this call creates)."""
    for i, (bid, b6) in enumerate(buses):
        a = BUSES + 0x100 * i
        e.uc.mem_write(a, bytes(0x100))
        e.w32(a, LVT)
        e.w32(a + 8, bid)
        e.w8(a + 0xCC, 0x40 if b6 else 0)
    e.w32(LVT + 0xC, RELB)
    for i, (bi, k2, byte, llo, lhi, state) in enumerate(lines):
        a = LINES + 0x400 * i
        e.uc.mem_write(a, bytes(0x400))
        e.w32(a + 0x4C, BUSES + 0x100 * bi if bi >= 0 else 0)
        e.w32(a + 0x50, k2)
        e.w8(a + 0x54, byte)
        e.w32(a + 0x28, llo)
        e.w32(a + 0x2C, lhi)
        e.w32(a + 0x1BC, state)
        e.w32(LNARR + 4 * i, a)
    e.w32(LINEARR, LNARR)
    e.w32(LINEARR + 4, len(lines))
    e.uc.mem_write(NEWLINE, bytes(0x400))
    e.uc.mem_write(LENT, bytes(0x20))
    e.w32(LENT + 8, key2)
    e.w32(LENT + 0x10, kind)
    e.uc.mem_write(LVOICE, bytes(0x100))
    prev = 0
    for ci in reversed(range(len(conns))):
        a = CONNS + 0x100 * ci
        e.uc.mem_write(a, bytes(0x100))
        e.w32(a + 0x28, prev)
        ln = conns[ci]
        e.w32(a + 0x30, NEWLINE if ln == -1 else LINES + 0x400 * ln)
        prev = a
    e.w32(LVOICE + 0x28, prev)
    e.w32(LVOICE + 8, 0 if owner_null else LCTX)
    e.w32(LCTX + 0xD4, LNODE)
    e.w32(LNODE, LVT + 0x80)
    e.w32(LVT + 0x80 + 0x88, FN88)
    e.uc.mem_write(CBUSM, bytes(0x100))
    if ctx_bus is not None:
        e.w8(CBUSM + 0xCC, 0x40 if ctx_bus else 0)
    log = []
    e.hook(FN88, lambda emu: (CBUSM if ctx_bus is not None else 0))
    e.hook(RELB, lambda emu: None)

    def create(emu):
        sp = emu.reg_sp()
        ctx = struct.unpack('<IIB', bytes(emu.uc.mem_read(emu.reg(0), 9)))
        flag = struct.unpack('<I', bytes(emu.uc.mem_read(sp, 4)))[0]
        busidx = -1 if ctx[0] == 0 else (ctx[0] - BUSES) // 0x100
        log.append('C %d.%x.%x.%x.%x.%x' % (busidx, ctx[1], ctx[2], emu.reg(2), emu.reg(3), flag & 0xFF))
        return NEWLINE if create_ok else 0

    def connect(emu):
        sp = emu.reg_sp()
        arg5 = struct.unpack('<I', bytes(emu.uc.mem_read(sp, 4)))[0]
        l = emu.reg(1)
        li = 'N' if l == NEWLINE else str((l - LINES) // 0x400)
        log.append('K %s.%x.%x.%x' % (li, emu.reg(2), emu.reg(3), arg5))
        return None

    e.hook(0xA429F0, create)
    e.hook(0xA4C280, connect)
    try:
        e.call(0xA43434, BUSES + 0x100 * aux_bus, LENT, lo, hi, stack=[LVOICE])
        crash = 0
    except UcError:
        crash = 1
    flags = [e.r8(LINES + 0x400 * i + 0x1CC) for i in range(len(lines))] + [e.r8(NEWLINE + 0x1CC)]
    return log, flags, crash


def link_cases(e, rnd, count):
    out = []
    for _ in range(count):
        nb = rnd.choice([1, 2, 3])
        buses = [(rnd.choice([0x1000, 0x1001, 0x1002, 0x2000]) + i, rnd.choice([0, 1])) for i in range(nb)]
        nl = rnd.choice([0, 1, 2, 3, 4])
        lines = []
        for _ in range(nl):
            bi = rnd.choice([-1] + list(range(nb)))
            lines.append((bi, rnd.choice([0xFFFFFFFF, 0xFFFFFFFF, 0, 5]), rnd.choice([0, 1, 1, 2]), rnd.choice([2, 3, 2]), rnd.choice([0, 0, 1]), rnd.choice([1, 4, 2, 4])))
        aux = rnd.randrange(nb)
        if nl and rnd.random() < 0.6:                          # often make one line match
            j = rnd.randrange(nl)
            lines[j] = (aux, 0xFFFFFFFF, lines[j][2], 2, 0, rnd.choice([4, 1, 4, 2]))
        key2 = rnd.choice([0xFFFFFFFF, 0xFFFFFFFF, 5])
        kind = rnd.choice([1, 2])
        lo, hi = rnd.choice([(2, 0), (3, 0), (2, 0), (1, 0)])
        nc = rnd.choice([0, 0, 1, 2])
        conns = [rnd.choice(list(range(nl)) + [-1]) for _ in range(nc)] if nl else [-1] * nc
        ctx_bus = rnd.choice([None, 0, 1])
        owner_null = rnd.random() < 0.05
        create_ok = rnd.random() < 0.85
        log, flags, crash = run_link(e, buses, lines, aux, key2, kind, lo, hi, conns, ctx_bus, owner_null, create_ok)
        line = 'L %s | %s | %d %x %d %x.%x %s | %s %d %d => %s | %s %d' % (
            ' '.join('%x.%d' % b for b in buses), ' '.join('%d.%x.%x.%x.%x.%x' % l for l in lines) or '-',
            aux, key2, kind, lo, hi, ' '.join(str(c) for c in conns) or '-',
            '-' if ctx_bus is None else str(ctx_bus), 1 if owner_null else 0, 1 if create_ok else 0,
            ' '.join(log) or '-', ' '.join('%x' % f for f in flags), crash)
        out.append(line)
    return out




# ------------------------------------------------------------------------------------------------ W: 0xA447D8..0xA44938

WV = BASE + 0x12000                              # the voice
WS = BASE + 0x13000                              # the pass block S (r5): only passed on
WCONN = BASE + 0x14000
WLINE = BASE + 0x16000
WBUS = BASE + 0x18000
SPW = STACK + 0xC0000


def run_walk(e, buses, lines, conns, entries, count, events):
    """buses: ids. lines: (bus index or -1, byte). conns: (aux, desc, b6c, line index). entries: (target, current, handle, id, kind)."""
    e.uc.mem_write(WV, bytes(0x400))
    for i, bid in enumerate(buses):
        e.uc.mem_write(WBUS + 0x100 * i, bytes(0x100))
        e.w32(WBUS + 0x100 * i + 8, bid)
    for i, (bi, byte) in enumerate(lines):
        a = WLINE + 0x100 * i
        e.uc.mem_write(a, bytes(0x100))
        e.w32(a + 0x4C, WBUS + 0x100 * bi if bi >= 0 else 0)
        e.w8(a + 0x54, byte)
    prev = 0
    for ci in reversed(range(len(conns))):
        aux, desc, b6c, li = conns[ci]
        a = WCONN + 0x100 * ci
        e.uc.mem_write(a, bytes(0x100))
        e.w32(a + 0x28, prev)
        e.w32(a + 0x68, aux)
        e.w32(a + 0x18, desc)
        e.w8(a + 0x6C, b6c)
        e.w32(a + 0x30, WLINE + 0x100 * li)
        prev = a
    e.w32(WV + 0x28, prev)
    e.w8(WV + 0xCC, count)
    for i, en in enumerate(entries):
        e.uc.mem_write(WV + 0x2C + 0x14 * i, struct.pack('<5I', *en))
    events.clear()

    def mix(emu):
        g = struct.unpack('<II', bytes(emu.uc.mem_read(emu.reg(3), 8)))
        ci = (emu.reg(2) - WCONN) // 0x100
        assert emu.reg(0) == e_line_of(conns, ci) and emu.reg(1) == WS
        events.append('M %d %x %x' % (ci, g[0], g[1]))
        return None

    def e_line_of(conns_, ci):
        return WLINE + 0x100 * conns_[ci][3]

    e.hook(0xA4FBEC, mix)
    e.hook(0xA4C60C, lambda emu: (events.append('F') or None))
    sp = SPW
    e.uc.mem_write(sp, bytes(0x80))
    e.w32(sp + 0x14 + 0x20, RET)                 # the epilogue add sp,sp,#0x14; pop {r4..fp,pc} returns to RET
    uc = e.uc
    uc.reg_write(UC_ARM_REG_R7, WV)
    uc.reg_write(UC_ARM_REG_R5, WS)
    uc.reg_write(UC_ARM_REG_SP, sp)
    uc.reg_write(UC_ARM_REG_LR, RET)
    uc.emu_start(0xA447D8, RET)


def walk_cases(e, rnd, count):
    out = []
    events = []
    for _ in range(count):
        nb = rnd.choice([1, 2, 3])
        buses = [rnd.choice([0x1000, 0x1001, 0x2000, 0xFFFFFFF0]) + i for i in range(nb)]
        nl = rnd.choice([1, 2, 3, 4])
        lines = [(rnd.choice([-1] + list(range(nb))), rnd.choice([0, 1, 1, 2])) for _ in range(nl)]
        keys = []
        for bi, byte in lines:
            keys.append(buses[bi] if bi >= 0 else (-byte) & M32)
        nc = rnd.choice([0, 1, 2, 3, 4, 5])
        conns = [(rnd.choice([0, 1, 1, 7]), rnd.choice([0, 1, 1, 1]), rnd.choice([0, 1, 2, 4, 6, 7, 5]), rnd.randrange(nl)) for _ in range(nc)]
        cnt = rnd.choice([0, 1, 2, 3, 4, 5, 6, 7, 8])
        entries = []
        for _ in range(8):
            entries.append((fbits(rfloat(rnd)) if rnd.random() < 0.5 else fbits(rnd.uniform(0.0, 1.0)), fbits(rfloat(rnd)) if rnd.random() < 0.3 else fbits(rnd.uniform(0.0, 1.0)),
                            0xFFFFFFFF, rnd.choice(keys + [0x5151, 0]), rnd.choice([1, 2])))
        run_walk(e, buses, lines, conns, entries, cnt, events)
        line = 'W %s | %s | %s | %d %s => %s' % (
            ' '.join('%x' % b for b in buses), ' '.join('%d.%x' % l for l in lines),
            ' '.join('%x.%x.%x.%d' % c for c in conns) or '-', cnt,
            ' '.join('.'.join('%x' % w for w in en) for en in entries), ' '.join(events) or '-')
        out.append(line)
    return out


# ------------------------------------------------------------------------------------------------ E: 0xA03108

MGR = BASE + 0x20000
BUCKETS = BASE + 0x20100
ITEM = BASE + 0x20400
HOLDER = BASE + 0x20800
PBLK = BASE + 0x20900


def run_entry(e, pid, eid, go, callback, cookie, flags, holder_ref, ext, alloc_ok):
    e.uc.mem_write(MGR, bytes(0x40))
    e.w32(MGR, BUCKETS)
    e.w32(MGR + 4, 7)                            # 7 buckets, none yet: the ratio count / buckets stays below 0.9 for the first entries, so no growth
    e.w32(MGR + 0xC, 0)
    e.uc.mem_write(BUCKETS, bytes(7 * 4))
    e.uc.mem_write(ITEM, bytes([0xCC]) * 0x50)   # what the pool hands out: 0xA03108 zero-fills it
    e.uc.mem_write(PBLK, bytes(0x40))
    e.w32(PBLK + 0, go)
    e.w32(PBLK + 8, pid)
    e.w32(PBLK + 0x10, HOLDER if holder_ref is not None else 0)
    e.w32(PBLK + 0x18, ext[0])
    e.w32(PBLK + 0x1C, ext[1])
    e.w32(PBLK + 0x20, ext[2])
    if holder_ref is not None:
        e.w32(HOLDER, holder_ref)
    calls = []

    def alloc(emu):
        calls.append(('alloc', emu.reg(1)))
        return ITEM if alloc_ok else 0

    def memset(emu):
        emu.uc.mem_write(emu.reg(0), bytes([emu.reg(1) & 0xFF]) * emu.reg(2))
        return emu.reg(0)

    e.hook(0x4D36DC, memset)
    e.hook(0xA7A7F4, alloc)
    e.hook(0xA1C63C, lambda emu: (calls.append(('A1C63C', emu.reg(0) == ITEM)) or None))
    res = e.call(0xA03108, MGR, PBLK, callback, cookie, stack=[flags, eid])
    item = bytes(e.uc.mem_read(ITEM, 0x50)) if alloc_ok else b''
    return res, item, (e.r32(HOLDER) if holder_ref is not None else None), e.r32(MGR + 0xC), calls


def entry_cases(e, rnd, count):
    out = []
    for _ in range(count):
        pid = rnd.getrandbits(32) | 1
        eid = rnd.getrandbits(32)
        go = rnd.getrandbits(32)
        cb = rnd.choice([0, 0, 0x8D8D41, rnd.getrandbits(32) | 1])
        cookie = rnd.getrandbits(32)
        flags = rnd.choice([0, 1, 5, 9, 13, 0xFFFFFFFF, rnd.getrandbits(32), 0x400001, 0x100001, 0xFD000 | 0xFF0 | 0xB | 1])
        holder = rnd.choice([None, None, 0, 1, rnd.getrandbits(31)])
        ext = (rnd.getrandbits(32), rnd.getrandbits(32), rnd.getrandbits(32)) if holder is not None or rnd.random() < 0.2 else (0, 0, 0)
        alloc_ok = rnd.random() < 0.95
        res, item, href, cnt, calls = run_entry(e, pid, eid, go, cb, cookie, flags, holder, ext, alloc_ok)
        if alloc_ok:
            assert ('A1C63C', True) in calls and cnt == 1 and res == 1
            w = struct.unpack('<20I', item)
            f = lambda i: w[i // 4]
            line = 'E %x %x %x %x %x %x %s %x.%x.%x %d => %d %x.%x.%x.%x.%x.%x.%x.%x.%x.%x.%x.%x %s' % (
                pid, eid, go, cb, cookie, flags, '-' if holder is None else '%x' % holder, ext[0], ext[1], ext[2], 1,
                res, f(0x18), f(0x1C), f(0x20), f(0x24), f(0x28), f(0x30), f(0x34), f(0x38), f(0x3C), f(0x40), f(0x44), f(0x48),
                '-' if href is None else '%x' % href)
        else:
            assert res == 2 and cnt == 0
            line = 'E %x %x %x %x %x %x %s %x.%x.%x %d => %d' % (pid, eid, go, cb, cookie, flags, '-' if holder is None else '%x' % holder, ext[0], ext[1], ext[2], 0, res)
        out.append(line)
    return out


# ------------------------------------------------------------------------------------------------ P: 0x9A0EF8

G0 = 0x108D868                                   # the same global block as G1: [G+0x78] = the event table, [G+8] the message queue, [G+0x8C] the playing-id manager, [G+0xF8] the playing-id counter
G1 = G0
EVT = BASE + 0x30000
EBUCK = BASE + 0x30100
ENODE = BASE + 0x30200
EVT_VT = BASE + 0x30300
QUEUE = BASE + 0x30400
MSG = BASE + 0x30500
MGRP = 0x0ABC1230
EREL = RET + 0x400


def run_post(e, eid, go, flags, cb, cookie, pidarg, counter, found, a03108_result, calls):
    e.uc.mem_write(EVT, bytes(0x80))
    e.w32(G0 + 0x78, EVT)
    e.w32(EVT + 0x40, EBUCK)
    e.w32(EVT + 0x44, 5)
    e.uc.mem_write(EBUCK, bytes(5 * 4))
    e.uc.mem_write(ENODE, bytes(0x40))
    if found:
        e.w32(ENODE, EVT_VT)
        e.w32(ENODE + 8, eid)
        e.w32(ENODE + 0xC, 3)
        e.w32(EVT_VT + 0xC, EREL)
        e.w32(EBUCK + 4 * (eid % 5), ENODE)
    e.w32(G1 + 8, QUEUE)
    e.w32(QUEUE + 0x6C, 5)
    e.w32(G1 + 0x8C, MGRP)
    e.w32(G1 + 0xF8, counter)
    e.uc.mem_write(MSG, bytes([0xCC]) * 0x80)
    calls.clear()
    e.hook(0x9A962C, lambda emu: 0x40)
    e.hook(0x9AF778, lambda emu: (calls.append(('msg', emu.reg(0) == QUEUE, emu.reg(1), emu.reg(2))) or MSG))
    e.hook(EREL, lambda emu: (calls.append(('release', emu.reg(0) == ENODE)) or None))

    def post(emu):
        sp = emu.reg_sp()
        flags_, eid_ = struct.unpack('<II', bytes(emu.uc.mem_read(sp, 8)))
        p = bytes(emu.uc.mem_read(emu.reg(1), 0x24))
        calls.append(('A03108', emu.reg(0) == MGRP, emu.reg(1) == MSG + 4, emu.reg(2), emu.reg(3), flags_, eid_, struct.unpack('<9I', p)))
        return a03108_result

    e.hook(0xA03108, post)
    ret = e.call(0x9A0EF8, eid, go, flags, cb, stack=[cookie, 0, pidarg])
    msg = struct.unpack('<20I', bytes(e.uc.mem_read(MSG, 0x50)))
    return ret, e.r32(G1 + 0xF8), msg, e.r32(QUEUE + 0x6C)


def post_cases(e, rnd, count):
    out = []
    calls = []
    for _ in range(count):
        eid = rnd.getrandbits(32)
        go = rnd.getrandbits(32)
        flags = rnd.choice([0, 1, 5, 9, 13, rnd.getrandbits(32)])
        cb = rnd.choice([0, 0x8D8D41, rnd.getrandbits(32) | 1])
        cookie = rnd.getrandbits(32)
        pidarg = rnd.choice([0, 0, rnd.getrandbits(32)])
        counter = rnd.choice([0, 1, 0xFFFFFFFE, 0xFFFFFFFF, rnd.getrandbits(32)])
        found = rnd.random() < 0.8
        res = rnd.choice([1, 1, 1, 2])
        ret, counter_after, msg, qcount = run_post(e, eid, go, flags, cb, cookie, pidarg, counter, found, res, calls)
        if not found:
            assert ret == 0 and not calls and counter_after == counter
            line = 'P %x %x %x %x %x %x %x 0 %d => 0 %x' % (eid, go, flags, cb, cookie, pidarg, counter, res, counter_after)
        else:
            kinds = [c[0] for c in calls]
            assert kinds[:2] == ['msg', 'A03108'], kinds
            m = calls[0]
            assert m[1] and m[2] == 1 and m[3] == 0x40
            a = calls[1]
            assert a[1] and a[2]
            p = a[7]
            newid = (counter + 1) & M32
            assert p[0] == go and p[2] == newid and msg[3] == newid and msg[4] == pidarg and msg[0xA] == ENODE and msg[0xC] == eid
            assert p[4] == 0 and p[6] == 0 and p[7] == 0 and p[8] == 0                # no externals: the block is zeroed
            if res == 1:
                assert ret == newid and kinds == ['msg', 'A03108']
            else:
                assert ret == 0 and kinds == ['msg', 'A03108', 'release'] and (msg[0] >> 16) == 0x38
            line = 'P %x %x %x %x %x %x %x 1 %d => %x %x | %x %x %x %x %x %x %x %x' % (
                eid, go, flags, cb, cookie, pidarg, counter, res, ret, counter_after,
                a[3], a[4], a[5], a[6], p[0], p[2], msg[4], len(kinds))
        out.append(line)
    return out




# ------------------------------------------------------------------------------------------------ G, H: 0xA0BA3C, 0xA0BA28 (the game object's aux values and listener mask stores)

GOA = BASE + 0x40000
GVALS = BASE + 0x40200


def go_cases(e, rnd, count):
    out = []
    for _ in range(count):
        e.uc.mem_write(GOA, bytes([0xCC]) * 0x100)
        n = rnd.choice([0, 1, 2, 3, 4, 4, 5, 6])
        vals = []
        for _ in range(4 if n <= 4 else 8):
            vid = rnd.choice([0, 0x1111, 0x2222, 0x3333, rnd.getrandbits(32) or 5])
            vals.append((vid, fbits(rfloat(rnd))))
        null = rnd.random() < 0.1
        for i, (vid, g) in enumerate(vals):
            e.uc.mem_write(GVALS + 8 * i, struct.pack('<II', vid, g))
        r = e.call(0xA0BA3C, GOA, 0 if null else GVALS, n)
        words = struct.unpack('<8I', bytes(e.uc.mem_read(GOA + 0x24, 0x20)))
        assert bytes(e.uc.mem_read(GOA + 0x44, 0xBC)) == bytes([0xCC]) * 0xBC and bytes(e.uc.mem_read(GOA, 0x24)) == bytes([0xCC]) * 0x24
        line = 'G %d %d %s => %d %s' % (n, 1 if null else 0, ' '.join('%x.%x' % v for v in vals[:max(n, 0) if n <= 4 else 4]) or '-', r, ' '.join('%x' % w for w in words))
        out.append(line)
    for _ in range(count // 4):
        w7c = rnd.choice([0xC0000001, 0, 0x80000005, rnd.getrandbits(32)])
        mask = rnd.getrandbits(8)
        e.uc.mem_write(GOA, bytes(0x100))
        e.w32(GOA + 0x7C, w7c)
        e.w8(GOA + 0x22, 0x55)
        e.call(0xA0BA28, GOA, mask)
        out.append('H %x %x => %x %x' % (w7c, mask, e.r32(GOA + 0x7C), e.r8(GOA + 0x22)))
    return out


# ------------------------------------------------------------------------------------------------ C: 0x9BC90C (the PBI context init: the chain test, [GO+0x7C]++ and the node AddRef)

CCTX = BASE + 0x42000
CGO = BASE + 0x42400
CNODES = BASE + 0x42800
CVT = BASE + 0x43000
CADD = RET + 0x500


def run_ctx(e, nodes, w7c, flag4):
    """nodes: (out index or -1, parent index or -1, word40). Node 0 is the PBI's node."""
    e.uc.mem_write(CCTX, bytes(0x100))
    e.uc.mem_write(CGO, bytes(0x100))
    e.w32(CGO + 0x7C, w7c)
    for i, (oi, pi, w40) in enumerate(nodes):
        a = CNODES + 0x100 * i
        e.uc.mem_write(a, bytes(0x100))
        e.w32(a, CVT)
        e.w32(a + 0x38, CNODES + 0x100 * oi if oi >= 0 else 0)
        e.w32(a + 0x34, CNODES + 0x100 * pi if pi >= 0 else 0)
        e.w32(a + 0x40, w40)
    e.w32(CVT + 8, CADD)
    adds = []
    e.hook(CADD, lambda emu: (adds.append((emu.reg(0) - CNODES) // 0x100) or None))
    e.hook(0x9E8728, lambda emu: None)
    e.call(0x9BC90C, CCTX, CGO, CNODES, flag4)
    return e.r8(CCTX + 0xDD), e.r8(CCTX + 0xDC), e.r32(CGO + 0x7C), adds, e.r32(CCTX + 0xD4) == CNODES, e.r32(CCTX + 8) == CGO


def ctx_cases(e, rnd, count):
    out = []
    for _ in range(count):
        n = rnd.choice([1, 2, 3, 4])
        nodes = []
        for i in range(n):
            oi = (i + 1) if (i + 1 < n and rnd.random() < 0.5) else -1
            pi = (i + 1) if (oi < 0 and i + 1 < n and rnd.random() < 0.85) else (i + 1 if oi >= 0 and rnd.random() < 0.3 else -1)
            w40 = rnd.choice([0, 0, 0x2, 0xFFE, 0x20000, 0x40000, 0x80000, 0xE0000, 0x100000, 0x1E0000, 0xFFFFFFFF, rnd.getrandbits(32)])
            nodes.append((oi, pi, w40))
        w7c = rnd.choice([0xC0000001, 0, 0x80000005, 0x3FFFFFFF, 0xFFFFFFFF, rnd.getrandbits(32)])
        flag4 = rnd.choice([0, 1])
        dd, dc, after, adds, nodeok, gook = run_ctx(e, nodes, w7c, flag4)
        assert nodeok and gook
        out.append('C %s %x %d => %x %x %x %s' % (' '.join('%d.%d.%x' % nd for nd in nodes), w7c, flag4, dd, dc, after, ' '.join(str(a) for a in adds) or '-'))
    return out




# ------------------------------------------------------------------------------------------------ K: 0xA68A2C

KCTX = BASE + 0x44000
KBUS = BASE + 0x44100


def key_cases(e, rnd, count):
    out = []
    for _ in range(count):
        e.uc.mem_write(KCTX, bytes([0xCC]) * 0x20)
        e.uc.mem_write(KBUS, bytes([0xCC]) * 0x20)
        bus = rnd.random() < 0.5
        bid = rnd.getrandbits(32)
        byte = rnd.choice([0, 1, 2, 0x7F, 0x80, 0xFF, rnd.getrandbits(8)])
        e.w32(KCTX, KBUS if bus else 0)
        e.w32(KCTX + 4, rnd.getrandbits(32))
        e.w8(KCTX + 8, byte)
        e.w32(KBUS + 8, bid)
        r = e.call(0xA68A2C, KCTX)
        out.append('K %d %x %x => %x' % (1 if bus else 0, bid, byte, r))
    return out




# ------------------------------------------------------------------------------------------------ N: 0xA0428C (the node notification of a playing id)

NMGR = BASE + 0x50000
NBUCK = BASE + 0x50100
NITEM = BASE + 0x50400
NCB = RET + 0x600


def run_notify(e, pid, present, other_in_chain, callback, flags, go, cookie, eid):
    e.uc.mem_write(NMGR, bytes(0x40))
    e.w32(NMGR, NBUCK)
    e.w32(NMGR + 4, 5)
    e.w8(NMGR + 0x1C, 0x77)
    e.uc.mem_write(NBUCK, bytes(5 * 4))
    e.uc.mem_write(NITEM, bytes(0x100))
    chain = []
    if other_in_chain:                                  # an item with another id in the same bucket, first in the chain
        o = NITEM + 0x80
        e.w32(o + 0x3C, pid + 5)
        chain.append(o)
    if present:
        a = NITEM
        e.w32(a + 0x3C, pid)
        e.w32(a + 0x40, NCB if callback else 0)
        e.w32(a + 0x44, cookie)
        e.w32(a + 0x48, flags)
        e.w32(a + 0x24, go)
        e.w32(a + 0x20, eid)
        chain.append(a)
    prev = 0
    for a in reversed(chain):
        e.w32(a + 0x4C, prev)
        prev = a
    e.w32(NBUCK + 4 * (pid % 5), prev)
    log = []
    bc = []

    def cb(emu):
        info = struct.unpack('<4I', bytes(emu.uc.mem_read(emu.reg(1), 16)))
        log.append((emu.reg(0), info, e.r8(NMGR + 0x1C)))
        return None

    e.hook(NCB, cb)
    e.hook(0x4D6754, lambda emu: (bc.append(1) or 0))
    e.call(0xA0428C, NMGR, pid)
    return log, len(bc), e.r8(NMGR + 0x1C)


def notify_cases(e, rnd, count):
    out = []
    for _ in range(count):
        pid = rnd.getrandbits(32)
        present = rnd.random() < 0.8
        other = rnd.random() < 0.3
        callback = rnd.random() < 0.7
        flags = rnd.choice([0, 0x20, 0x21, 0x1F, 0xFFFFFFFF, 0x100000, rnd.getrandbits(32)])
        go = rnd.getrandbits(32)
        cookie = rnd.getrandbits(32)
        eid = rnd.getrandbits(32)
        log, nbc, flag1c = run_notify(e, pid, present, other, callback, flags, go, cookie, eid)
        out.append('N %x %d %d %d %x %x %x %x => %s %d %x' % (
            pid, 1 if present else 0, 1 if other else 0, 1 if callback else 0, flags, go, cookie, eid,
            ' '.join('%x.%x.%x.%x.%x.%x' % ((l[0],) + l[1] + (l[2],)) for l in log) or '-', nbc, flag1c))
    return out




# ------------------------------------------------------------------------------------------------ T: 0x8D8CE4 (the Anki PostEvent wrapper, Thumb)

TTHIS = BASE + 0x60000
TCTX = BASE + 0x60100


def run_wrapper(e, engine_flag, ctx_word, with_ctx, calls):
    e.uc.mem_write(TTHIS, bytes(0x10))
    e.w8(TTHIS, engine_flag)
    e.uc.mem_write(TCTX, bytes(0x20))
    e.w32(TCTX, ctx_word)
    calls.clear()

    def post(emu):
        sp = emu.reg_sp()
        stk = struct.unpack('<4I', bytes(emu.uc.mem_read(sp, 16)))
        calls.append((emu.reg(0), emu.reg(1), emu.reg(2), emu.reg(3), stk))
        return 0x1234

    e.hook(0x9A6704, post)
    uc = e.uc
    sp = STACK + 0xE0000
    uc.reg_write(UC_ARM_REG_SP, sp)
    uc.reg_write(UC_ARM_REG_R0, TTHIS)
    uc.reg_write(UC_ARM_REG_R1, 0xE1E1E1E1)
    uc.reg_write(UC_ARM_REG_R2, 0x60B0B0B0)
    uc.reg_write(UC_ARM_REG_R3, TCTX if with_ctx else 0)
    uc.reg_write(UC_ARM_REG_LR, RET | 1)
    uc.emu_start(0x8D8CE4 | 1, RET)
    return uc.reg_read(UC_ARM_REG_R0)


def wrapper_cases(e, rnd, count):
    out = []
    calls = []
    for b in range(256):
        w = b | (rnd.getrandbits(24) << 8)
        r = run_wrapper(e, 1, w, True, calls)
        c = calls[0]
        assert c[0] == 0xE1E1E1E1 and c[1] == 0x60B0B0B0 and c[4][0] == TCTX and c[4][1:] == (0, 0, 0) and r == 0x1234
        out.append('T %x 1 1 => %x %x %x' % (w, c[2], c[3], 1 if c[4][0] == TCTX else 0))
    r = run_wrapper(e, 1, 0, False, calls)
    c = calls[0]
    assert c[4] == (0, 0, 0, 0)
    out.append('T 0 1 0 => %x %x 0' % (c[2], c[3]))
    r = run_wrapper(e, 0, 5, True, calls)
    assert not calls and r == 0
    out.append('T 5 0 1 => none')
    return out




# ------------------------------------------------------------------------------------------------ S: 0x9A080C (the send-threshold setter)

def run_setter(e, vbits, typ, gate):
    e.w32(0x105241C, gate)
    e.w32(0x1052454, 0x37800000)
    e.w32(0x1052450, 0xC2C0999A)

    def powf(emu):
        x = struct.unpack('<f', struct.pack('<I', emu.reg(1)))[0]
        try:
            r = math.pow(10.0, x)
        except OverflowError:
            r = float('inf')
        if r != r:
            return 0x7FC00000
        return struct.unpack('<I', struct.pack('<f', r))[0] if abs(r) < 3.4028235e38 else 0x7F800000

    e.hook(0x4D6778, powf)
    r = e.call(0x9A080C, vbits, typ)
    return r, e.r32(0x1052454), e.r32(0x1052450), e.r32(0x105241C)


def setter_cases(e, rnd, count):
    out = []
    for v in (-80.0, -6.0, -37 / 0.05, -90.0):
        r, g, u, ga = run_setter(e, fbits(v), 0, 3)
        out.append('S %x %d %d => %d %x %x %d' % (fbits(v), 0, 3, r, g, u, ga))
    specials = [fbits(-80.0), fbits(0.0), fbits(-0.0), fbits(-96.3), 0xC2C0999A, 0xC2C0999B, 0xC2C09999, fbits(-1.0), fbits(-6.0), fbits(-37 / 0.05), fbits(-740.01), fbits(1e-3), 0x7FC00000, 0xFF800000, 0x7F800000, fbits(-95.0)]
    for i in range(count):
        v = rnd.choice(specials) if rnd.random() < 0.4 else fbits(rnd.uniform(-100.0, 1.0))
        typ = rnd.choice([0, 1, 2, 3, 4, -1])
        gate = rnd.choice([3, 3, 2, 0, 1])
        r, g, u, ga = run_setter(e, v, typ & M32, gate)
        out.append('S %x %d %d => %d %x %x %d' % (v, typ, gate, r, g, u, ga if ga < 0x80000000 else ga - (1 << 32)))
    return out


def special_builder_cases(e):
    """The shapes the inventory names, as explicit cases (the random ones cover them too, the test checks the counts)."""
    out = []
    one = fbits(1.0)
    post = GLOBAL_PAIRS[0]
    for flag, db, pairs, uids, udbs in (
        (1, 0.0, [(0x1111, one), (0, 0), (0, 0), (0, 0)], [0, 0, 0, 0], [0.0] * 4),                                  # 0 dB: the fast power 0.99903899
        (1, 0.0, [(0x1111, one), (0, one), (0x3333, one), (0x4444, one)], [0, 0, 0, 0], [0.0] * 4),                  # the game pairs stop at the first zero id
        (1, 0.0, [(0x1111, one)] * 4, [0x5555, 0, 0x6666, 0], [-10.0, 0.0, -20.0, 0.0]),                              # the user entries are independent
        (1, 0.0, [(0x1111 + i, one) for i in range(4)], [0x5555 + i for i in range(4)], [-10.0] * 4),               # 4 + 4 = 8 entries, no terminator
        (0, 0.0, [(0x1111, one)] * 4, [0x5555, 0, 0, 0], [-10.0, 0, 0, 0]),                                          # the use flag clear: no game sends
        (1, -741.0, [(0x1111, one)] * 4, [0, 0, 0, 0], [0.0] * 4),                                                    # y below -37.0: lin 0, below the threshold
    ):
        gp = [(a, b) for a, b in pairs]
        n, term, items = run_builder(e, flag, fbits(db), gp, uids, [fbits(d) for d in udbs], post[0], post[1])
        out.append('B %x %x %s %s %s %x %x => %d %d %s' % (
            flag, fbits(db), ' '.join('%x.%x' % p for p in gp), ' '.join('%x' % u for u in uids), ' '.join('%x' % fbits(d) for d in udbs), post[0], post[1], n, term,
            ' '.join('%x.%x.%x' % it for it in items) or '-'))
    return out


def main():
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 20261004
    rnd = random.Random(seed)
    e = Emu()
    e.std_hooks()
    lines = special_builder_cases(e)
    lines += builder_cases(e, rnd, 4000)
    em = Emu()
    em.std_hooks()
    lines += merge_cases(em, rnd, 1500)
    ed = Emu()
    ed.std_hooks()
    lines += dispatch_cases(ed, rnd, 600)
    el = Emu()
    el.std_hooks()
    lines += link_cases(el, rnd, 900)
    ew = Emu()
    ew.std_hooks()
    lines += walk_cases(ew, rnd, 800)
    ee = Emu()
    ee.std_hooks()
    lines += entry_cases(ee, rnd, 600)
    ep = Emu()
    ep.std_hooks()
    lines += post_cases(ep, rnd, 400)
    eg = Emu()
    eg.std_hooks()
    lines += go_cases(eg, rnd, 400)
    lines += key_cases(eg, rnd, 300)
    en = Emu()
    en.std_hooks()
    lines += notify_cases(en, rnd, 400)
    et = Emu()
    et.std_hooks()
    lines += wrapper_cases(et, rnd, 0)
    es = Emu()
    es.std_hooks()
    lines += setter_cases(es, rnd, 400)
    ec = Emu()
    ec.std_hooks()
    lines += ctx_cases(ec, rnd, 500)
    blob = base64.b64encode(gzip.compress(chr(10).join(lines).encode('utf-8'), 9, mtime=0)).decode('ascii')
    out = []
    o = out.append
    o("// <auto-generated> by re-analysis/tools/emu/emu_aux.py from the engine's own output (the real 0x9BD368 / 0x9D4228 / 0x9D4108 / 0xA43434 / 0xA447D8.. / 0xA03108 / 0x9A0EF8 under Unicorn, seed %d, %d cases): do not edit. </auto-generated>" % (seed, len(lines)))
    o('using System.IO.Compression;')
    o('using System.Text;')
    o('')
    o('namespace Cozmo.Protocol.Tests;')
    o('')
    o('internal static class WwiseAuxOracle')
    o('{')
    o("    /// <summary>One engine run per line: B 0x9BD368, M 0x9D4228, D 0x9D4108, L 0xA43434, W 0xA447D8.., E 0xA03108, P 0x9A0EF8, G 0xA0BA3C, H 0xA0BA28, C 0x9BC90C, K 0xA68A2C, N 0xA0428C, T 0x8D8CE4, S 0x9A080C; the text before <c>=&gt;</c> is the input and the text after it is the engine's result (see emu_aux.py for the fields). Floats are hex bits.")
    o("    /// The lines are stored gzip-compressed (base64) so the test assembly's user-string heap is not spent on the text.</summary>")
    o('    public static readonly string[] Lines = Decode(Data);')
    o('')
    o('    private static string[] Decode(string base64)')
    o('    {')
    o('        using var raw = new MemoryStream(Convert.FromBase64String(base64));')
    o('        using var gz = new GZipStream(raw, CompressionMode.Decompress);')
    o('        using var reader = new StreamReader(gz, Encoding.UTF8);')
    o("        return reader.ReadToEnd().Split('\\n');")
    o('    }')
    o('')
    o('    private const string Data =')
    step = 120
    chunks = [blob[i:i + step] for i in range(0, len(blob), step)]
    for i, ch in enumerate(chunks):
        o('        "%s"%s' % (ch, ' +' if i + 1 < len(chunks) else ';'))
    o('}')
    sys.stdout.write(chr(10).join(out) + chr(10))
    for kind in 'BMDLWEPGHCKNTS':
        sys.stderr.write('%s: %d lines\n' % (kind, sum(1 for l in lines if l.startswith(kind + ' '))))


if __name__ == '__main__':
    main()
