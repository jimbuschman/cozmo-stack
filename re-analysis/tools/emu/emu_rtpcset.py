"""Run the engine's own RTPC value-change delivery and the voice pre-pass under Unicorn and write the golden values for WwiseRtpcDeliveryTests (B-M6b-4 batch 5f, M6-009 / M6-010 / M6-022, C37.1):

    python re-analysis/tools/emu/emu_rtpcset.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseRtpcSetOracle.cs

The REAL functions run (nothing is a Python stand-in except where the list below says so):

  * the array A walk 0xA114D8, the per-subscription dispatch 0xA10C84 (type 2: the sums of 0xA14E28 at the old and the new value, the delta, the vt+0 call; type 0: the filter chain and 0xA0E81C), the node subscriber's vt+0 thunk 0x9868E4 ->
    0x9868B0 (parameter 0xD: 0x97E570), the fan-out 0xA1B254 with the keyed scan 0xA1A6A4 (both its variants) and the all-wild loop 0xA1B308, the PBI context's vt+8 = 0xA02EC0 -> 0xA02CE4 -> 0x9BDDE0 on PBIs built in emulated memory
    (the ctx vptr is the .so's own 0x103B7DC; the holder's vptr is the .so's own 0x1039824, whose slot 0 is the thunk), with the real 0xA14E28 on curves in memory;
  * the voice pre-pass step 0xA55750 with the real 0x9FF414 -> 0x9FF368 (the context's vt+0x28, from the .so's vtable) and the real 0xA4B93C gain store.

Stand-ins (bodies C37 does not adopt, named in the C# as required seams): 0x9BE28C (returns 0), 0x9BDA88 (returns 0) and the context's vt+0x24 (CalcEffectiveParams) inside 0xA4B93C / 0xA55750. The group functor G is never
built here (the byte argument of 0xA114D8 is 0): its vt+0 body is unread.

The expected values in WwiseRtpcDeliveryTests are this script's output (the engine's), never the C#'s. Floats are bit patterns.
"""
import random
import struct
import sys
from unicorn import UC_HOOK_CODE
from unicorn.arm_const import *

sys.dont_write_bytecode = True
from emu_common import *
from so import u32

M32 = 0xFFFFFFFF
HOLDER_VPTR = 0x1039824            # a node's subscriber sub-object vtable: slot 0 is the thunk 0x9868E4 (checked in main)
CTX_VPTR = 0x103B7DC               # the PBI context's vtable: +8 = 0xA02EC0, +0x24 = vt+0x24, +0x28 = 0x9FF414 (checked in main)
FULL_MASK = 0x3FE3FFFE67BD         # {0xFFFE67BD, 0x3FE3}: the listener mask 0x9BC5DC..0x9BC5F0 passes

# the PBI float fields the cases observe (pbi offsets): ctx+0x8C, +0x38, +0x90, +0x94, +0x3C, +0x98, +0x9C, +0x40, +0x58, +0x48
FIELDS = [0x98, 0x44, 0x9C, 0xA0, 0x48, 0xA4, 0xA8, 0x4C, 0x64, 0x54]


def fb(x):
    return bits(x)


def h(v):
    return '0x%08XU' % (v & M32)


class Rd:
    pass


def init_fields(rnd, hard=False):
    vals = []
    for _ in FIELDS:
        if hard and rnd.random() < 0.15:
            vals.append(rnd.choice([0x00000000, 0x80000000, 0x7F800000, 0xFF800000, 0x7FC00000, 0x3F800000, 0x00000001]))
        else:
            vals.append(fb(rnd.uniform(-60.0, 40.0)))
    return vals


class World:
    def __init__(self):
        e = Emu()
        e.std_hooks()

        def memcpy(em):
            d, s, n = em.reg(0), em.reg(1), em.reg(2)
            em.uc.mem_write(d, bytes(em.uc.mem_read(s, n)))
            return d
        e.hook(0x4D37F0, memcpy)
        self.e = e
        self.curve_log = []

        def curve_log(uc, address, size, _):                # the REAL 0xA14E28 runs; the hook only records x
            self.curve_log.append(uc.reg_read(UC_ARM_REG_R1) & M32)
        e.uc.hook_add(UC_HOOK_CODE, curve_log, begin=0xA14E28, end=0xA14E28)

    def curve_slots(self, curves):
        e = self.e
        slots = e.alloc(0x14 * max(1, len(curves)))
        for i, (rid, scaling, pts) in enumerate(curves):
            p = e.alloc(12 * len(pts))
            for k, (x, y, interp) in enumerate(pts):
                e.w32(p + 12 * k, fb(x)); e.w32(p + 12 * k + 4, fb(y)); e.w32(p + 12 * k + 8, interp)
            e.w32(slots + 0x14 * i + 4, rid)
            e.w32(slots + 0x14 * i + 8, p)
            e.w32(slots + 0x14 * i + 0xC, len(pts))
            e.w32(slots + 0x14 * i + 0x10, scaling)
        return slots

    def pbi(self, init, e9):
        e = self.e
        p = e.alloc(0x400)
        e.uc.mem_write(p, bytes(0x400))
        e.w32(p + 0xC, CTX_VPTR)
        for off, v in zip(FIELDS, init):
            e.w32(p + off, v)
        e.w8(p + 0xE9, e9)
        return p

    def read_pbi(self, p):
        return [self.e.r32(p + off) for off in FIELDS], self.e.r8(p + 0xE9)


# ------------------------------------------------------------------------------------------------ delivery

def build_delivery(spec):
    """spec: nodes, subs [(node, param, type, accum, [(rid, scaling, pts)])], children [(node, A, B, mask, init, e9)], rtpc, old, new, key (A, B). Returns (world, pbis, entry, key pointer)."""
    w = World()
    e = w.e
    holders = []
    registries = []
    for n in range(spec['nodes']):
        reg = e.alloc(0x20)
        hold = e.alloc(0x10)
        e.w32(hold, HOLDER_VPTR)
        e.w32(hold + 4, reg)
        holders.append(hold)
        registries.append(reg)
    pbis = []
    per_node = {n: [] for n in range(spec['nodes'])}
    for ci, (node, a, b, mask, init, e9) in enumerate(spec['children']):
        p = w.pbi(init, e9)
        pbis.append(p)
        for nd in (node if isinstance(node, tuple) else (node,)):
            per_node[nd].append((a, b, mask, p))
    for n in range(spec['nodes']):
        kids = sorted(per_node[n], key=lambda t: (t[0], t[1]))             # the registry is kept sorted by key (stable for equal keys)
        arr = e.alloc(0x28 * max(1, len(kids)))
        maskb = (1 << 64) - 1
        for i, (a, b, mask, p) in enumerate(kids):
            r = arr + 0x28 * i
            e.w32(r, a); e.w32(r + 4, b); e.w32(r + 8, 0); e.w8(r + 0xC, 0xFF); e.w8(r + 0x10, 0xFF); e.w32(r + 0x14, 0)
            e.w32(r + 0x18, mask & M32); e.w32(r + 0x1C, (mask >> 32) & M32)
            e.w32(r + 0x20, p + 0xC)
            maskb &= mask
        if not kids:
            maskb = 0
        reg = registries[n]
        e.w32(reg + 8, maskb & M32); e.w32(reg + 0xC, (maskb >> 32) & M32)
        e.w32(reg + 0x10, arr); e.w32(reg + 0x14, len(kids))
    subs = []
    for sd in spec['subs']:
        node, param, typ, accum, curves = sd[:5]
        sub = e.alloc(0x60)
        slots = w.curve_slots(curves)
        if typ == 0:                                           # a context-level subscription: [e] is the context of child 0, the scope key sits at [e+0xC..0x23]
            e.w32(sub, pbis[0] + 0xC)
            a, b, c_, d, e_, f = sd[5]
            e.w32(sub + 0xC, a); e.w32(sub + 0x10, b); e.w32(sub + 0x14, c_); e.w8(sub + 0x18, d); e.w8(sub + 0x1C, e_); e.w32(sub + 0x20, f)
        else:
            e.w32(sub, holders[node])
        e.w32(sub + 4, param)
        e.w32(sub + 0x24, typ); e.w32(sub + 0x28, accum)
        e.w32(sub + 0x2C, slots); e.w32(sub + 0x30, len(curves))
        subs.append(sub)
    entry = e.alloc(0x50)
    arr_a = e.alloc(4 * max(1, len(subs)))
    for i, s_ in enumerate(subs):
        e.w32(arr_a + 4 * i, s_)
    e.w32(entry, spec['rtpc'])
    e.w32(entry + 0x34, arr_a); e.w32(entry + 0x38, len(subs))
    key = e.alloc(0x20)
    e.w32(key, spec['key'][0]); e.w32(key + 4, spec['key'][1]); e.w32(key + 8, 0)
    e.w8(key + 0xC, 0xFF); e.w8(key + 0x10, 0xFF); e.w32(key + 0x14, 0)
    e.w8(0x108D7D8, 0xEE); e.w8(0x108D7D8 + 0xC, 0xEE)
    return w, pbis, entry, key


def run_delivery(spec, w, entry, key):
    e = w.e
    e.call(0xA114D8, entry, fb(spec['old']) if isinstance(spec['old'], float) else spec['old'],
           fb(spec['new']) if isinstance(spec['new'], float) else spec['new'], key, stack=(0,))


def delivery_case(spec):
    w, pbis, entry, key = build_delivery(spec)
    run_delivery(spec, w, entry, key)
    e = w.e
    after = [w.read_pbi(p) for p in pbis]
    return after, list(w.curve_log), e.r8(0x108D7D8), e.r8(0x108D7D8 + 0xC)


EV_CURVE = (0.0, [(0.0, -1.0, 1), (1.0, 0.0, 4)])                          # event_volume 0xD2687048 on ActorMixers 62050212 / 682998829 / 121198006 (C37.5): scaling 2
EVENT = (7001, 2, [(0.0, -1.0, 1), (1.0, 0.0, 4)])
ROBOT = (7002, 0, [(0.0, -200.0, 6), (1.0, 0.0, 4)])                       # robot_volume 0x637C1240 on Bus 1723505802 (C37.5): scaling 0
LINE = (7003, 0, [(0.0, -96.0, 4), (100.0, 0.0, 4)])
STEEP = (7004, 0, [(-5.0, 10.0, 4), (0.0, -20.0, 9), (5.0, 30.0, 4), (50.0, 6.0, 4)])
OTHER = (7099, 0, [(0.0, 3.0, 4), (1.0, 4.0, 4)])


def children_for(rnd, keys, mask=FULL_MASK, node=0, hard=False):
    return [(node, a, b, mask, init_fields(rnd, hard), rnd.choice([0, 1, 4, 5, 0x20])) for (a, b) in keys]


def delivery_cases():
    cases = []
    rnd = random.Random(20261004)

    def add(name, **kw):
        kw.setdefault('nodes', 1)
        kw['name'] = name
        cases.append(kw)

    # the shipped shape: event_volume 0xD2687048 on an ActorMixer (type 2, parameter 0, accumulate 1, scaling 2), the PBI keyed {object, playing id}
    add('event_volume_down', subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100)]), rtpc=7001, old=1.0, new=0.5, key=(7, 0x100))
    add('event_volume_up', subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100)]), rtpc=7001, old=0.25, new=0.75, key=(7, 0x100))
    add('event_volume_to_zero', subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100)]), rtpc=7001, old=1.0, new=0.0, key=(7, 0x100))
    add('event_volume_unchanged_curve_value', subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100)]), rtpc=7001, old=1.5, new=2.5, key=(7, 0x100))
    add('keyed_playing_id_picks_one_child', subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100), (7, 0x101), (8, 0x100), (7, 0x102)]), rtpc=7001, old=1.0, new=0.4, key=(7, 0x101))
    add('keyed_object_only_picks_the_run_of_that_object', subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100), (7, 0x101), (8, 0x100), (6, 0x7), (7, 0x5)]), rtpc=7001, old=1.0, new=0.3, key=(7, 0))
    add('keyed_no_such_object', subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100), (9, 0x100)]), rtpc=7001, old=1.0, new=0.3, key=(8, 0x100))
    add('keyed_no_such_playing_id', subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100), (7, 0x102)]), rtpc=7001, old=1.0, new=0.3, key=(7, 0x101))
    add('all_wild_reaches_every_child', subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100), (7, 0x101), (8, 0x100), (0, 0)]), rtpc=7001, old=0.9, new=0.1, key=(0, 0))
    add('all_wild_bus_robot_volume', subs=[(0, 5, 2, 1, [ROBOT])], children=children_for(rnd, [(0, 0), (3, 4)]), rtpc=7002, old=0.0, new=0.5, key=(0, 0))
    add('empty_registry', subs=[(0, 0, 2, 1, [EVENT])], children=[], rtpc=7001, old=1.0, new=0.5, key=(7, 0x100))
    # a slot of another RTPC id does not contribute (a subscription is in the array A of each RTPC id one of its curves names, so the cases always have a matching slot)
    add('mixed_slots_only_the_matching_one_counts', subs=[(0, 0, 2, 1, [OTHER, EVENT, STEEP])], children=children_for(rnd, [(7, 0x100)]), rtpc=7001, old=0.2, new=0.9, key=(7, 0x100))
    add('two_matching_slots_sum', subs=[(0, 0, 2, 1, [(7001, 0, [(0.0, 1.0, 4), (1.0, 3.0, 4)]), (7001, 0, [(0.0, -2.0, 4), (1.0, 5.0, 4)])])], children=children_for(rnd, [(7, 0x100)]), rtpc=7001, old=0.0, new=1.0, key=(7, 0x100))
    # parameter ids of the 0x9BDDF0 first-level table C37.1 adopts
    for pid in (0, 2, 3, 4, 5, 7):
        add('param_%d' % pid, subs=[(0, pid, 2, 1, [LINE])], children=children_for(rnd, [(7, 0x100)]), rtpc=7003, old=10.0, new=60.0, key=(7, 0x100))
    # mask: bit paramId of each child's mask decides; the registry's AND mask shortcut and the per-child test agree
    lo = FULL_MASK & ~1
    add('mask_lacks_the_bit_of_one_child', subs=[(0, 0, 2, 1, [EVENT])], children=[(0, 7, 0x100, FULL_MASK, init_fields(rnd), 1), (0, 7, 0x101, lo, init_fields(rnd), 1), (0, 7, 0x102, FULL_MASK, init_fields(rnd), 0)], rtpc=7001, old=1.0, new=0.5, key=(7, 0))
    add('mask_lacks_the_bit_of_every_child', subs=[(0, 0, 2, 1, [EVENT])], children=[(0, 7, 0x100, lo, init_fields(rnd), 1), (0, 7, 0x101, lo, init_fields(rnd), 1)], rtpc=7001, old=1.0, new=0.5, key=(7, 0))
    add('mask_lacks_the_bit_all_wild', subs=[(0, 3, 2, 1, [LINE])], children=[(0, 7, 0x100, FULL_MASK & ~8, init_fields(rnd), 1), (0, 7, 0x101, FULL_MASK, init_fields(rnd), 1)], rtpc=7003, old=0.0, new=5.0, key=(0, 0))
    add('param_0x40_is_outside_the_mask_word', subs=[(0, 0x40, 2, 1, [LINE])], children=children_for(rnd, [(7, 0x100)], mask=(1 << 64) - 1), rtpc=7003, old=1.0, new=5.0, key=(7, 0x100))
    add('param_0x80_shifts_right', subs=[(0, 0x80, 2, 1, [LINE])], children=children_for(rnd, [(7, 0x100)], mask=(1 << 64) - 1), rtpc=7003, old=1.0, new=5.0, key=(7, 0x100))
    # parameter 0xD: 0x97E570 only
    add('param_0xd_sets_the_two_bytes', subs=[(0, 0xD, 2, 1, [LINE])], children=children_for(rnd, [(7, 0x100)]), rtpc=7003, old=1.0, new=5.0, key=(7, 0x100))
    # two subscriptions, one holder, one PBI: the order of the adds
    add('two_subs_same_param_order', nodes=2, subs=[(0, 0, 2, 1, [EVENT]), (1, 0, 2, 1, [(7001, 0, [(0.0, -3.0, 4), (1.0, 2.0, 4)])])], children=[((0, 1), 7, 0x100, FULL_MASK, init_fields(rnd), 1)], rtpc=7001, old=0.0, new=1.0, key=(7, 0x100))
    add('two_subs_one_pbi_two_holders_param_3', nodes=2, subs=[(0, 3, 2, 1, [LINE]), (1, 3, 2, 1, [(7003, 0, [(0.0, 0.5, 4), (100.0, -7.25, 4)])])], children=[((0, 1), 7, 0x100, FULL_MASK, init_fields(rnd, True), 0)], rtpc=7003, old=3.0, new=77.0, key=(7, 0x100))
    # two holders: only the PBI registered at the subscription's holder is reached
    add('two_holders_only_its_own_children', nodes=2, subs=[(1, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100)], node=0) + children_for(rnd, [(7, 0x100)], node=1), rtpc=7001, old=1.0, new=0.5, key=(7, 0x100))
    add('two_holders_both_subscribed', nodes=2, subs=[(0, 0, 2, 1, [EVENT]), (1, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100)], node=0) + children_for(rnd, [(7, 0x100)], node=1), rtpc=7001, old=1.0, new=0.5, key=(7, 0x100))
    # special float values
    for nm, old, new in (('nan_new', 0.5, float('nan')), ('nan_old', float('nan'), 0.5), ('inf_new', 0.5, float('inf')), ('huge', 1e30, -1e30)):
        add('values_' + nm, subs=[(0, 0, 2, 1, [EVENT])], children=children_for(rnd, [(7, 0x100)]), rtpc=7001, old=old, new=new, key=(7, 0x100))
    # type 0 (context level): the filter chain on the subscription's scope key and 0xA0E81C; 0x9BD100 with [ctx+0xD0] == 0 stores nothing: what shows is whether 0xA14E28 ran (the curve log). [e] is the context of child 0.
    for nm, scope, key in (('match', (7, 0x100, 0, 0xFF, 0xFF, 0), (7, 0x100)), ('all_wild_key', (7, 0x100, 0, 0xFF, 0xFF, 0), (0, 0)), ('object_mismatch', (7, 0x100, 0, 0xFF, 0xFF, 0), (8, 0x100)),
                           ('playing_id_mismatch', (7, 0x100, 0, 0xFF, 0xFF, 0), (7, 0x101)), ('object_only_key', (7, 0x100, 0, 0xFF, 0xFF, 0), (7, 0)), ('wild_scope_keyed_set', (0, 0, 0, 0xFF, 0xFF, 0), (7, 0x100)),
                           ('wild_scope_wild_set', (0, 0, 0, 0xFF, 0xFF, 0), (0, 0)), ('scope_without_playing_id', (7, 0, 0, 0xFF, 0xFF, 0), (7, 0x100))):
        add('type0_' + nm, subs=[(0, 0x1B, 0, 1, [EVENT], scope)], children=[((), 7, 0x100, FULL_MASK, init_fields(rnd), 1)], rtpc=7001, old=1.0, new=0.5, key=key)
    add('type0_two_matching_slots_and_another_id', subs=[(0, 0x1C, 0, 2, [OTHER, EVENT, (7001, 0, [(0.0, 1.0, 4), (1.0, 3.0, 4)])], (7, 0x100, 0, 0xFF, 0xFF, 0))], children=[((), 7, 0x100, FULL_MASK, init_fields(rnd), 1)], rtpc=7001, old=0.2, new=0.9, key=(7, 0x100))
    # random fuzz of the shipped shape
    for i in range(60):
        nk = rnd.randint(1, 5)
        keys = [(rnd.choice([0, 3, 7, 7, 7, 9]), rnd.choice([0, 0x100, 0x101, 0x102, 5])) for _ in range(nk)]
        masks = [rnd.choice([FULL_MASK, FULL_MASK, FULL_MASK & ~1, FULL_MASK & ~(1 << rnd.randint(0, 10)), (1 << 64) - 1, 0, 1 << rnd.randint(0, 7)]) for _ in range(nk)]
        pid = rnd.choice([0, 0, 0, 2, 3, 4, 5, 7])
        curve = rnd.choice([EVENT, ROBOT, LINE, STEEP])
        extra = [OTHER] if rnd.random() < 0.3 else []
        subs = [(0, pid, 2, rnd.choice([1, 2]), extra + [curve])]
        two = rnd.random() < 0.3
        if two:
            subs.append((1, rnd.choice([0, 2, 3]), 2, 1, [curve]))
        skey = (rnd.choice([0, 7, 7, 7, 3, 9]), 0)
        skey = (skey[0], rnd.choice([0, 0x100, 0x101, 5]) if skey[0] != 0 else 0)
        x0 = rnd.uniform(-1.0, 3.0) if curve is not STEEP else rnd.uniform(-8.0, 60.0)
        x1 = rnd.uniform(-1.0, 3.0) if curve is not STEEP else rnd.uniform(-8.0, 60.0)
        if curve is LINE:
            x0, x1 = rnd.uniform(-10, 110), rnd.uniform(-10, 110)
        x0 = struct.unpack('<f', struct.pack('<f', x0))[0]
        x1 = struct.unpack('<f', struct.pack('<f', x1))[0]
        if x0 == x1:
            x1 = struct.unpack('<f', struct.pack('<f', x1 + 0.5))[0]
        add('fuzz_%02d' % i, nodes=2 if two else 1, subs=subs,
            children=[((0, 1) if two else 0, a, b, m, init_fields(rnd, True), rnd.choice([0, 1, 4, 5])) for (a, b), m in zip(keys, masks)],
            rtpc=curve[0], old=x0, new=x1, key=skey)
    return cases


# ------------------------------------------------------------------------------------------------ the child call on its own

def receive_cases():
    rnd = random.Random(42)
    cases = []
    vals = [0.0, -0.0, 1.0, -1.0, 0.5, -37.5, 12.25, 100.0, 1e-30, 3.4e38, float('inf'), float('-inf'), float('nan')]
    for pid in (0, 2, 3, 4, 5, 7):
        for v in vals:
            for d in rnd.sample(vals, 4):
                cases.append((pid, v, d, init_fields(rnd, True), rnd.choice([0, 1, 4, 5, 0xFE, 0xFF])))
    return cases


def run_receive(cases):
    out = []
    for (pid, value, delta, init, e9) in cases:
        w = World()
        p = w.pbi(init, e9)
        w.e.call(0xA02EC0, p + 0xC, pid, fb(value), fb(delta))
        out.append((w.read_pbi(p)))
    return out


# ------------------------------------------------------------------------------------------------ the pre-pass step 0xA55750

def prepass_cases():
    rnd = random.Random(55750)
    cases = []

    def add(name, e8, e9, b1be, vol, mute, f98, f118, f168, f16c, calc_vol=None, calc_mute=None):
        cases.append(dict(name=name, e8=e8, e9=e9, b1be=b1be, vol=vol, mute=mute, f98=f98, f118=f118, f168=f168, f16c=f16c, calc_vol=calc_vol, calc_mute=calc_mute))
    # E8 bit 5 set, E9 bit 0 set: the context's vt+0x28 = 0x9FF414 -> 0x9FF368 recomputes +0x3C / +0x40, then the gain store
    add('dirty_recompute_then_gain', 0x7D, 0x01, 0x00, -3.0, 0.5, -6.0, 1.5, 1.0, 1.0)
    add('dirty_recompute_volume_floor', 0x7D, 0x01, 0x00, 0.0, 1.0, -800.0, 0.0, 1.0, 1.0)
    add('dirty_recompute_mute_product', 0x7D, 0x01, 0x00, 0.0, 1.0, -1.0, 0.25, 0.5, 0.25)
    add('dirty_recompute_negative_product_clamps', 0x7D, 0x01, 0x00, 0.0, 1.0, -1.0, 0.0, -2.0, 1.0)
    add('dirty_recompute_nan_factor', 0x7D, 0x01, 0x00, 0.0, 1.0, -1.0, 0.0, float('nan'), 1.0)
    add('clean_only_the_gain', 0x7D, 0x00, 0x00, -12.0, 0.75, -6.0, 1.5, 1.0, 1.0)
    add('clean_gain_zero_db', 0x7D, 0x00, 0x00, 0.0, 1.0, 0.0, 0.0, 1.0, 1.0)
    add('clean_gain_below_threshold', 0x7D, 0x00, 0x00, -741.0, 1.0, 0.0, 0.0, 1.0, 1.0)
    add('clean_gain_at_threshold', 0x7D, 0x00, 0x00, -740.0, 1.0, 0.0, 0.0, 1.0, 1.0)
    add('clean_gain_loud', 0x7D, 0x00, 0x00, 24.0, 2.0, 0.0, 0.0, 1.0, 1.0)
    add('clean_gain_nan_volume', 0x7D, 0x00, 0x00, float('nan'), 1.0, 0.0, 0.0, 1.0, 1.0)
    add('unfiltered_1be_bits_do_not_matter', 0x7D, 0x01, 0xEB, -3.0, 0.5, -6.0, 1.5, 1.0, 1.0)
    # E8 bit 5 clear: the context's vt+0x24 (stubbed here: it leaves the scripted +0x3C / +0x40), then the gain store
    add('calc_effective_params_then_gain', 0x5D, 0x01, 0x00, -3.0, 0.5, -6.0, 1.5, 1.0, 1.0, calc_vol=-9.0, calc_mute=0.5)
    for i in range(30):
        add('fuzz_%02d' % i, rnd.choice([0x7D, 0x7D, 0x5D]), rnd.choice([0, 1]), rnd.choice([0, 1, 2, 0xE3, 0xC1]),
            rnd.uniform(-900, 40), rnd.uniform(-0.5, 1.5), rnd.uniform(-100, 20), rnd.uniform(-20, 20), rnd.uniform(-1, 2), rnd.uniform(-1, 2),
            calc_vol=rnd.uniform(-100, 20), calc_mute=rnd.uniform(0, 1))
    return cases


def build_voice(w, pbi):
    """A voice owning `pbi` (memory already built): [voice+0xD4] -> source -> [source+0xC] = pbi, [voice+8] = the context, a send table of one entry."""
    e = w.e
    voice = e.alloc(0x600)
    src = e.alloc(0x40)
    obj = e.alloc(0x100)
    tbl = e.alloc(0x100)
    e.uc.mem_write(voice, bytes(0x600)); e.uc.mem_write(src, bytes(0x40))
    e.w32(voice + 0xD4, src); e.w32(src + 0xC, pbi)
    e.w32(voice + 8, pbi + 0xC)                                    # [voice+8]: the context of the owner PBI
    e.w32(voice + 0x10, tbl); e.w32(voice + 0x14, 1)               # the send table exists (one entry): 0xA4B9C8 takes the 0xA4B9D4 path
    e.w32(pbi + 0x14, obj)                                         # [ctx+8]: the registered object 0xA4B9D4 reads ([obj+0x22], [obj+0x60])
    return voice


def run_prepass(c):
    w = World()
    e = w.e
    pbi = e.alloc(0x400)
    e.uc.mem_write(pbi, bytes(0x400))
    e.w32(pbi + 0xC, CTX_VPTR)
    voice = build_voice(w, pbi)
    e.w8(pbi + 0xE8, c['e8']); e.w8(pbi + 0xE9, c['e9']); e.w8(pbi + 0x1BE, c['b1be'])
    e.wf(pbi + 0x3C, c['vol']); e.wf(pbi + 0x40, c['mute'])
    e.wf(pbi + 0x98, c['f98']); e.wf(pbi + 0x118, c['f118']); e.wf(pbi + 0x168, c['f168']); e.wf(pbi + 0x16C, c['f16c'])
    events = []
    e.hook(0x9BE28C, lambda em: (events.append('9BE28C'), 0)[1])
    e.hook(0x9BDA88, lambda em: (events.append('9BDA88'), 0)[1])
    vt24 = u32(CTX_VPTR + 0x24)

    def calc(em):
        events.append('calc')
        em.wf(pbi + 0x3C, c['calc_vol'])
        em.wf(pbi + 0x40, c['calc_mute'])
        return None
    e.hook(vt24, calc)
    e.call(0xA55750, voice)
    return dict(gain=e.r32(voice + 0x1C), vol=e.r32(pbi + 0x3C), mute=e.r32(pbi + 0x40), e9=e.r8(pbi + 0xE9), events=events)


# ------------------------------------------------------------------------------------------------ the chain: the set, then the next voice pre-pass

def chain_cases():
    """The shipped event_volume shape followed by the pre-pass step 0xA55750 on the same PBI: (name, delivery spec, the PBI's E8, +0x3C, +0x40, +0x118, +0x168, +0x16C)."""
    rnd = random.Random(4242)
    out = []
    for i, (old, new, vol, mute, f118, f168, f16c) in enumerate([
            (1.0, 0.5, -3.0, 1.0, 0.0, 1.0, 1.0), (0.25, 0.75, -9.5, 0.5, 2.0, 1.0, 0.5), (1.0, 0.0, 0.0, 1.0, 0.0, 1.0, 1.0),
            (0.9, 0.1, -20.0, 0.8, -1.5, 0.75, 1.0), (0.5, 1.0, 6.0, 1.0, 0.0, 1.0, 1.0)]):
        init = init_fields(rnd)
        spec = dict(name='chain_%d' % i, nodes=1, subs=[(0, 0, 2, 1, [EVENT])], children=[(0, 7, 0x100, FULL_MASK, init, 0)], rtpc=7001, old=old, new=new, key=(7, 0x100))
        out.append((spec, dict(e8=0x7D, vol=vol, mute=mute, f118=f118, f168=f168, f16c=f16c)))
    return out


def run_chain(spec, pre):
    w, pbis, entry, key = build_delivery(spec)
    e = w.e
    pbi = pbis[0]
    run_delivery(spec, w, entry, key)
    delivered = w.read_pbi(pbi)
    voice = build_voice(w, pbi)
    e.w8(pbi + 0xE8, pre['e8'])
    e.wf(pbi + 0x3C, pre['vol']); e.wf(pbi + 0x40, pre['mute'])
    e.wf(pbi + 0x118, pre['f118']); e.wf(pbi + 0x168, pre['f168']); e.wf(pbi + 0x16C, pre['f16c'])
    events = []
    e.hook(0x9BE28C, lambda em: (events.append('9BE28C'), 0)[1])
    e.hook(0x9BDA88, lambda em: (events.append('9BDA88'), 0)[1])
    e.call(0xA55750, voice)
    return delivered, dict(gain=e.r32(voice + 0x1C), vol=e.r32(pbi + 0x3C), mute=e.r32(pbi + 0x40), e9=e.r8(pbi + 0xE9), f98=e.r32(pbi + 0x98))


# ------------------------------------------------------------------------------------------------ output

def cs_u32_list(xs):
    return 'new uint[] { %s }' % ', '.join(h(x) for x in xs)


def main():
    assert u32(HOLDER_VPTR) == 0x9868E4, hex(u32(HOLDER_VPTR))
    assert u32(CTX_VPTR + 8) == 0xA02EC0
    assert u32(CTX_VPTR + 0x28) == 0x9FF414, hex(u32(CTX_VPTR + 0x28))
    dc = delivery_cases()
    results = []
    for spec in dc:
        after, log, f0, fc = delivery_case(spec)
        results.append((spec, after, log, f0, fc))
        print('delivery', spec['name'], file=sys.stderr, flush=True)
    rc = receive_cases()
    rr = run_receive(rc)
    pc = prepass_cases()
    pr = [run_prepass(c) for c in pc]
    ch = [(spec, pre) + run_chain(spec, pre) for (spec, pre) in chain_cases()]
    out = []
    w = out.append
    w("// <auto-generated> by re-analysis/tools/emu/emu_rtpcset.py from the engine's own output (the real 0xA114D8 / 0xA10C84 / 0x9868B0 / 0xA1B254 / 0xA1A6A4 / 0xA02EC0 / 0xA02CE4 / 0x9BDDE0 / 0xA14E28 and the voice pre-pass step 0xA55750 / 0x9FF368 / 0xA4B93C under Unicorn): do not edit. </auto-generated>")
    w('namespace Cozmo.Protocol.Tests;')
    w('')
    w('internal static class WwiseRtpcSetOracle')
    w('{')
    w('    /// <summary>The PBI offsets the cases observe, in order: ctx+0x8C, +0x38, +0x90, +0x94, +0x3C, +0x98, +0x9C, +0x40, +0x58, +0x48.</summary>')
    w('    public static readonly int[] Fields = { %s };' % ', '.join('0x%X' % f for f in FIELDS))
    w('')
    w('    /// <summary>A curve: RTPC id, scaling, points flattened as (x bits, y bits, interp).</summary>')
    w('    public sealed record DCurve(uint Rtpc, uint Scaling, uint[] Pts);')
    w('    /// <summary>A subscription on holder <c>Node</c>: parameter, type, accumulate, curves; a type-0 subscription also carries its scope key words {A, B, C, D, E, F} and targets child 0.</summary>')
    w('    public sealed record DSub(int Node, uint Param, uint Type, uint Accum, DCurve[] Curves, uint[]? Scope = null);')
    w('    /// <summary>A registered PBI: its holders, key (A, B), listener mask, the initial and the final observed floats and dirty byte.</summary>')
    w('    public sealed record DChild(int[] Nodes, uint A, uint B, ulong Mask, uint[] Init, byte InitE9, uint[] After, byte AfterE9);')
    w('    /// <summary>One 0xA114D8 run: the subscriptions, the children, the set (RTPC id, old and new bits, key A and B), the engine\'s 0xA14E28 x log, and the two bytes 0x97E570 writes (0xEE untouched).</summary>')
    w('    public sealed record DCase(string Name, int Nodes, DSub[] Subs, DChild[] Children, uint Rtpc, uint Old, uint New, uint KeyA, uint KeyB, uint[] CurveLog, byte Byte0, byte Byte0C);')
    w('')
    w('    public static readonly DCase[] Delivery =')
    w('    {')
    for (spec, after, log, f0, fc) in results:
        subs = []
        for sd in spec['subs']:
            node, param, typ, accum, curves = sd[:5]
            cs = ', '.join('new DCurve(%d, %d, %s)' % (rid, sc, cs_u32_list([v for (x, y, k) in pts for v in (fb(x), fb(y), k)])) for (rid, sc, pts) in curves)
            scope = ', %s' % cs_u32_list(sd[5]) if typ == 0 else ''
            subs.append('new DSub(%d, %d, %d, %d, new DCurve[] { %s }%s)' % (node, param, typ, accum, cs, scope))
        kids = []
        for i, (node, a, b, mask, init, e9) in enumerate(spec['children']):
            fl, ae9 = after[i]
            nodes_ = node if isinstance(node, tuple) else (node,)
            kids.append('new DChild(new int[] { %s }, %d, %d, 0x%XUL, %s, %d, %s, %d)' % (', '.join(map(str, nodes_)), a, b, mask, cs_u32_list(init), e9, cs_u32_list(fl), ae9))
        old = spec['old'] if isinstance(spec['old'], int) else fb(spec['old'])
        new = spec['new'] if isinstance(spec['new'], int) else fb(spec['new'])
        w('        new DCase("%s", %d, new DSub[] { %s }, new DChild[] { %s }, %d, %s, %s, %d, %d, %s, %d, %d),' % (
            spec['name'], spec['nodes'], ', '.join(subs), ', '.join(kids), spec['rtpc'], h(old), h(new), spec['key'][0], spec['key'][1], cs_u32_list(log), f0, fc))
    w('    };')
    w('')
    w('    /// <summary>The PBI context vt+8 (0xA02EC0) called directly: parameter id, value bits, delta bits, initial floats, initial dirty byte, final floats, final dirty byte.</summary>')
    w('    public static readonly (uint Param, uint Value, uint Delta, uint[] Init, byte InitE9, uint[] After, byte AfterE9)[] Receive =')
    w('    {')
    for (pid, value, delta, init, e9), (fl, ae9) in zip(rc, rr):
        w('        (%d, %s, %s, %s, %d, %s, %d),' % (pid, h(fb(value)), h(fb(delta)), cs_u32_list(init), e9, cs_u32_list(fl), ae9))
    w('    };')
    w('')
    w('    /// <summary>0xA55750 for one voice (E8, E9, 1BE bytes; +0x3C, +0x40, +0x98, +0x118, +0x168, +0x16C bits; the scripted CalcEffectiveParams result +0x3C / +0x40 bits or 0): the final voice+0x1C, +0x3C, +0x40 bits, E9 byte and the events.</summary>')
    w('    public static readonly (string Name, byte E8, byte E9, byte B1be, uint[] Pbi, uint[] Calc, uint Gain, uint Vol, uint Mute, byte AfterE9, string Events)[] PrePass =')
    w('    {')
    for c, r in zip(pc, pr):
        pbi = [fb(c[k]) for k in ('vol', 'mute', 'f98', 'f118', 'f168', 'f16c')]
        calc = [fb(c['calc_vol']), fb(c['calc_mute'])] if c['calc_vol'] is not None else [0, 0]
        w('        ("%s", 0x%X, 0x%X, 0x%X, %s, %s, %s, %s, %s, %d, "%s"),' % (c['name'], c['e8'], c['e9'], c['b1be'], cs_u32_list(pbi), cs_u32_list(calc), h(r['gain']), h(r['vol']), h(r['mute']), r['e9'], ','.join(r['events'])))
    w('    };')
    w('')
    w('    /// <summary>The chain: the event_volume set (the delivery case\'s subscriptions, child, set) and then the pre-pass step 0xA55750 on the same PBI (E8 byte, +0x3C, +0x40, +0x118, +0x168, +0x16C bits): the PBI after the delivery, then the final +0x98, +0x3C, +0x40 bits, the E9 byte and voice+0x1C.</summary>')
    w('    public static readonly (DCase Delivery, byte E8, uint[] Pre, uint[] Delivered, byte DeliveredE9, uint F98, uint Vol, uint Mute, byte AfterE9, uint Gain)[] Chain =')
    w('    {')
    for (spec, pre, delivered, r) in ch:
        subs = []
        for sd in spec['subs']:
            node, param, typ, accum, curves = sd[:5]
            cs = ', '.join('new DCurve(%d, %d, %s)' % (rid, sc, cs_u32_list([v for (x, y, k) in pts for v in (fb(x), fb(y), k)])) for (rid, sc, pts) in curves)
            subs.append('new DSub(%d, %d, %d, %d, new DCurve[] { %s })' % (node, param, typ, accum, cs))
        kids = []
        for (node, a, b, mask, init, e9) in spec['children']:
            kids.append('new DChild(new[] { 0 }, %d, %d, 0x%XUL, %s, %d, %s, %d)' % (a, b, mask, cs_u32_list(init), e9, cs_u32_list(delivered[0]), delivered[1]))
        dcase = 'new DCase("%s", 1, new DSub[] { %s }, new DChild[] { %s }, %d, %s, %s, %d, %d, new uint[0], 0, 0)' % (spec['name'], ', '.join(subs), ', '.join(kids), spec['rtpc'], h(fb(spec['old'])), h(fb(spec['new'])), spec['key'][0], spec['key'][1])
        pre_bits = [fb(pre['vol']), fb(pre['mute']), fb(pre['f118']), fb(pre['f168']), fb(pre['f16c'])]
        w('        (%s, 0x%X, %s, %s, %d, %s, %s, %s, %d, %s),' % (dcase, pre['e8'], cs_u32_list(pre_bits), cs_u32_list(delivered[0]), delivered[1], h(r['f98']), h(r['vol']), h(r['mute']), r['e9'], h(r['gain'])))
    w('    };')
    w('}')
    sys.stdout.write('\n'.join(out) + '\n')
    sys.stderr.write('delivery %d, receive %d, prepass %d\n' % (len(results), len(rc), len(pc)))


if __name__ == '__main__':
    main()
