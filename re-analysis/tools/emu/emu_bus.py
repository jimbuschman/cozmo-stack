"""Run the engine's own bus-walk and RTPC-evaluation bodies under Unicorn, for WwiseBusWalkTests (M6-025 / M6-010 / M6-009, C34.1 and C34.2).

0x9F4BB8, 0x9BDA6C, 0x9C54E8 (with the real vt+0x44 slots of the shipped vtables), 0x9F9CDC, 0x9C39DC, 0xA11590 with 0xA17878 / 0xA17724 / 0xA17280, and the bus constructor 0x9C3620 run on nodes, bundles and
an RTPC manager built in emulated memory. The curve evaluation 0xA14E28 is the REAL engine code since batch 5d (C35; only a logging code hook records each call). Python stand-ins (bodies the inventory does not adopt): 0x9E6748 (the not-in-store
branch of 0xA17280; its result is chosen per case and logged), and for the constructor 0x9F402C / 0xA19F94 / 0x9F40F4. The expected values in WwiseBusWalkTests are this script's output, not the C#'s:

    python re-analysis/tools/emu/emu_bus.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseBusWalkOracle.cs

(the generated file is written to stdout; with --text the cases are printed in a readable form instead).
"""
import struct
import sys
from unicorn import UC_HOOK_CODE
from emu_common import *

MGR_PTR_VAR = 0x108D908              # *[GOT 0x1040088]: the RTPC manager pointer
VPTR = {0: 0x103ACE0, 0xC: 0x103D100, 1: 0x103CF88, 3: 0x103BAD0, 5: 0x103B050, 2: 0x103B860, 4: 0x103BDC8}


class Node:
    """A node spec: the C# test builds the same object from the same fields."""
    FIELDS = dict(parent=-1, bus=-1, cat=0, fx=None, b68=0, b46=0, w40=0, w54=0, base=None, ranged=None, mask=None, duck8c=(), ducka8=(), maxduck=0xC2C0999A, states=(), key=0)

    def __init__(self, **kw):
        for k, v in self.FIELDS.items():
            setattr(self, k, kw.pop(k, v))
        assert not kw, kw


def lin(rid, a, b):
    """A curve (rid, scaling 0, points) that is the straight line y = a * x + b between x = 0 and x = 100 (interp 4 linear on both points)."""
    y1 = struct.unpack('<f', struct.pack('<f', struct.unpack('<f', struct.pack('<f', a * 100.0))[0] + b))[0]
    return (rid, 0, [(0.0, struct.unpack('<f', struct.pack('<f', b))[0], 4), (100.0, y1, 4)])


class Sub:
    def __init__(self, node, param, typ, accum, curves):
        # curves: [(rtpc id, scaling, [(x, y, interp)])]; a legacy (rtpc id, a, b) triple is the line of lin()
        self.node, self.param, self.typ, self.accum = node, param, typ, accum
        self.curves = [lin(*c) if not isinstance(c[2], list) else c for c in curves]


class World:
    """Emulated memory for one case."""

    def __init__(self, nodes, values=(), subs=(), e6748=(0, 0.0)):
        e = Emu()
        e.std_hooks()
        e.log = []
        self.e = e
        self.addr = []
        self.e6748 = e6748
        for n in nodes:
            self.addr.append(e.alloc(0x100))
        for i, n in enumerate(nodes):
            self.build(i, n)
        self.mgr = e.alloc(0x80)
        e.w32(MGR_PTR_VAR, self.mgr)
        self.vbuckets = e.alloc(8)
        self.sbuckets = e.alloc(8)
        e.w32(self.mgr + 0, self.vbuckets)
        e.w32(self.mgr + 4, 1)
        e.w32(self.mgr + 0x10, self.sbuckets)
        e.w32(self.mgr + 0x14, 1 if subs else 0)
        self.vaddr = {}
        for (rid, default, root_valid, root) in values:
            v = e.alloc(0x4C)
            self.vaddr[rid] = v
            e.w32(v, rid)
            e.w32(v + 8, bits(default))
            e.w32(v + 0x1C, bits(root))
            e.w8(v + 0x20, 1 if root_valid else 0)
            e.w32(v + 4, e.r32(self.vbuckets))
            e.w32(self.vbuckets, v)
        for s in subs:
            en = e.alloc(0x40)
            e.w32(en, self.addr[s.node] + 0x10)
            e.w32(en + 4, s.param)
            e.w32(en + 0x24, s.typ)
            e.w32(en + 0x28, s.accum)
            cv = e.alloc(20 * max(len(s.curves), 1))
            for j, (rid, scaling, pts) in enumerate(s.curves):
                pb = e.alloc(12 * max(len(pts), 1))
                for k, (px, py, pi_) in enumerate(pts):
                    e.wf(pb + 12 * k, px)
                    e.wf(pb + 12 * k + 4, py)
                    e.w32(pb + 12 * k + 8, pi_)
                e.w32(cv + 20 * j + 4, rid)          # [c+4] the RTPC id
                e.w32(cv + 20 * j + 8, pb)           # [c+8] the curve object {points*, count, scaling} that 0xA14E28 takes
                e.w32(cv + 20 * j + 12, len(pts))
                e.w32(cv + 20 * j + 16, scaling)
            e.w32(en + 0x2C, cv)
            e.w32(en + 0x30, len(s.curves))
            e.w32(en + 8, e.r32(self.sbuckets))
            e.w32(self.sbuckets, en)

        def curve_log(uc, address, size, _):          # the REAL 0xA14E28 runs; this hook only records that it was called (and with which x)
            e.log.append(('A14E28', uc.reg_read(UC_ARM_REG_R1)))
        e.uc.hook_add(UC_HOOK_CODE, curve_log, begin=0xA14E28, end=0xA14E28)

        def e6748(emu):                   # 0x9E6748(mgr, id, key, &out)
            ret, x = self.e6748
            e.log.append(('9E6748', emu.reg(1)))
            emu.w32(emu.reg(3), bits(x))
            return ret
        e.hook(0x9E6748, e6748)

    def bundle(self, entries, stride):
        e = self.e
        if entries is None:
            return 0
        n = len(entries)
        base = e.alloc(4 + n + stride * n + 8)
        e.w8(base, n)
        for i, (k, v) in enumerate(entries):
            e.w8(base + 1 + i, k)
        off = (n + 4) & ~3
        for i, (k, v) in enumerate(entries):
            e.w32(base + off + stride * i, v)
            if stride == 8:
                e.w32(base + off + stride * i + 4, 0x55555555)
        return base

    def build(self, i, n):
        e = self.e
        a = self.addr[i]
        e.w32(a, VPTR[n.cat])
        e.w32(a + 8, 100 + i)
        e.w32(a + 0x34, self.addr[n.parent] if n.parent >= 0 else 0)
        e.w32(a + 0x38, self.addr[n.bus] if n.bus >= 0 else 0)
        if n.fx is not None:
            c = e.alloc(0x28)
            for s in range(4):
                e.w32(c + 4 + 8 * s, n.fx[s])
            e.w32(a + 0x28, c)
        e.w32(a + 0x3C, self.bundle(n.base, 4))
        if n.mask is not None:
            m = e.alloc(8)
            e.w32(m, n.mask & 0xFFFFFFFF)
            e.w32(m + 4, n.mask >> 32)
            e.w32(a + 0x14, m)
        if n.ranged is not None:
            h = e.alloc(0x10)
            e.w32(h + 0xC, self.bundle(n.ranged, 8))
            e.w32(a + 0x24, h)
        if n.states:
            head = 0
            for bnd in reversed(n.states):
                it = e.alloc(0x20)
                e.w32(it + 8, head)
                e.w32(it + 0x10, self.bundle(bnd, 8))
                head = it
            e.w32(a + 0x18, head)
        e.w8(a + 0x46, n.b46)
        e.w32(a + 0x40, n.w40)
        e.w32(a + 0x54, n.w54)
        e.w8(a + 0x68, n.b68)
        e.w32(a + 0x6C, n.maxduck)
        for off, lst in ((0x8C, n.duck8c), (0xA8, n.ducka8)):
            head = 0
            for fl in reversed(lst):
                it = e.alloc(0x20)
                e.w32(it, head)
                e.w32(it + 0x14, fl)
                head = it
            e.w32(a + off, head)


def u(x):
    return x & 0xFFFFFFFF


# ---------------------------------------------------------------------------------------------------------------- the cases

def b(x):
    return bits(x)


CASES = {}      # name -> dict(kind=..., nodes=[Node], ..., expected=...)


def case_9f4bb8():
    out = {}
    chain = [Node(parent=1), Node(parent=2), Node(bus=3, parent=-1), Node()]
    for name, nodes, start in (('leaf_to_bus', chain, 0), ('self_has_bus', [Node(bus=1), Node()], 0), ('no_bus', [Node(parent=1), Node()], 0), ('nearest_wins', [Node(parent=1, bus=2), Node(bus=3), Node(), Node()], 0)):
        w = World(nodes)
        r = w.e.call(0x9F4BB8, w.addr[start])
        idx = -1 if r == 0 else w.addr.index(r)
        out[name] = dict(nodes=nodes, start=start, expected=idx)
    return out


def case_9bda6c():
    out = {}
    nodes = [Node(parent=1), Node(bus=2), Node()]
    for e9 in (0, 4, 5, 1):
        w = World(nodes)
        pbi = w.e.alloc(0x240)
        w.e.w8(pbi + 0xE9, e9)
        w.e.w32(pbi + 0xE0, w.addr[0])
        r = w.e.call(0x9BDA6C, pbi + 0xC)
        out['e9_%d' % e9] = dict(nodes=nodes, start=0, e9=e9, expected=(-1 if r == 0 else w.addr.index(r)))
    return out


def case_9c54e8():
    out = {}
    master = Node()
    variants = {
        'all_clear': Node(bus=0),
        'fx_slot0': Node(bus=0, fx=(5, 0, 0, 0)),
        'fx_slot3': Node(bus=0, fx=(0, 0, 0, 9)),
        'fx_chunk_all_zero': Node(bus=0, fx=(0, 0, 0, 0)),
        'audio_device_0c': Node(bus=0, cat=0xC),
        'byte68': Node(bus=0, b68=1),
        'b46_bit7': Node(bus=0, b46=0x80),
        'b46_bit6_only': Node(bus=0, b46=0x40),
        'no_output_bus': Node(),
        'w40_e0000': Node(bus=0, w40=0x20000),
        'w40_c0000': Node(bus=0, w40=0xC0000),
        'w40_other': Node(bus=0, w40=0x1F000),
        'w54': Node(bus=0, w54=0xDEAD),
        'actor_mixer_cat1': Node(bus=0, cat=1),
    }
    for name, node in variants.items():
        nodes = [master, node]
        w = World(nodes)
        r = w.e.call(0x9C54E8, w.addr[1])
        out[name] = dict(nodes=nodes, start=1, expected=r)
    return out


def case_9f9cdc():
    out = {}
    mk = lambda *ids: [(k, v) for k, v in ids]
    items = (mk((0, b(1.5)), (6, b(0.25)), (2, b(10.0))), mk((3, b(2.0)), (4, b(3.0)), (5, b(-4.0)), (0, b(0.5))), None, mk((7, b(9.0))))
    for name, mask, b46 in (('mask1', 1, 1), ('mask2', 2, 1), ('mask4', 4, 1), ('mask8', 8, 1), ('mask10', 0x10, 1), ('mask1f', 0x1F, 1), ('gate_off', 0x1F, 0), ('mask0', 0, 1)):
        nodes = [Node(b46=b46, states=items)]
        w = World(nodes)
        blk = w.e.alloc(0x40)
        for i in range(7):
            w.e.w32(blk + 4 * i, 0)
        w.e.call(0x9F9CDC, w.addr[0], blk, mask)
        res = [w.e.r32(blk + 4 * i) for i in range(7)]
        out[name] = dict(nodes=nodes, mask=mask, expected=res)
    nodes = [Node(b46=1, states=())]
    w = World(nodes)
    blk = w.e.alloc(0x40)
    w.e.call(0x9F9CDC, w.addr[0], blk, 0x1F)
    out['no_list'] = dict(nodes=nodes, mask=0x1F, expected=[w.e.r32(blk + 4 * i) for i in range(7)])
    # a duplicate id: the first entry is the one read
    nodes = [Node(b46=1, states=(mk((0, b(1.0)), (0, b(2.0))),))]
    w = World(nodes)
    blk = w.e.alloc(0x40)
    w.e.call(0x9F9CDC, w.addr[0], blk, 1)
    out['duplicate_id_first_wins'] = dict(nodes=nodes, mask=1, expected=[w.e.r32(blk + 4 * i) for i in range(7)])
    return out


def case_9c39dc():
    out = {}
    mk = lambda *ids: [(k, v) for k, v in ids]
    parent_fx = Node(fx=(7, 0, 0, 0))                           # a parent that is not collapsed (FX id)
    collapsed = lambda **kw: Node(bus=0, **kw)                  # parent 0 is the first node of each case: see below

    def run(name, nodes, p, flag=0, start=None, values=(), subs=(), e6748=(0, 0.0)):
        w = World(nodes, values, subs, e6748)
        s = len(nodes) - 1 if start is None else start
        r = w.e.call(0x9C39DC, w.addr[s], flag, p)
        out[name] = dict(nodes=nodes, start=s, p=p, flag=flag, values=values, subs=subs, e6748=e6748, expected=r, log=[l[0] for l in w.e.log])

    base5 = mk((5, b(-3.5)))
    base0 = mk((0, b(-2.0)))
    run('p5_base', [Node(base=base5)], 5)
    run('p0_base', [Node(base=base0)], 0)
    run('p5_base_absent_id', [Node(base=mk((0, b(1.0)), (2, b(7.0))))], 5)
    run('p5_no_base', [Node()], 5)
    run('p5_base_plus_ranged_first_float', [Node(base=base5, ranged=mk((5, b(0.25))))], 5)
    run('p5_ranged_absent', [Node(base=base5, ranged=mk((0, b(0.25))))], 5)
    run('p5_ranged_only', [Node(ranged=mk((5, b(2.5))))], 5)
    run('p0_state_list', [Node(b46=1, states=(mk((0, b(1.5)), (6, b(9.0))), mk((0, b(0.5)))))], 0)
    run('p5_state_list', [Node(b46=1, states=(mk((5, b(-1.5)), (6, b(9.0))), mk((5, b(-0.5)))))], 5)
    run('p5_state_list_gate_off', [Node(b46=0, states=(mk((5, b(-1.5))),))], 5)
    run('p5_duck_max_wins', [Node(ducka8=(b(-18.0),))], 5)
    run('p5_duck_sum_above_floor', [Node(ducka8=(b(-18.0), b(-6.0)))], 5)
    run('p5_duck_sum_positive', [Node(ducka8=(b(2.0), b(3.0)))], 5)
    run('p5_duck_floor_above_sum', [Node(ducka8=(b(-18.0),), maxduck=b(-10.0))], 5)
    run('p5_duck_equal', [Node(ducka8=(b(-10.0),), maxduck=b(-10.0))], 5)
    run('p0_duck8c', [Node(duck8c=(b(-3.0), b(-1.0)), ducka8=(b(-50.0),))], 0)
    run('p5_duck_nan_sum', [Node(ducka8=(0x7FC00000,))], 5)
    run('p5_all_terms', [Node(base=base5, ranged=mk((5, b(0.25))), b46=1, states=(mk((5, b(1.0))),), ducka8=(b(-1.0),), maxduck=b(-96.3))], 5)
    # recursion: flag 0 stops at a parent that 0x9C54E8 says is not collapsed, continues through a collapsed one
    top = Node(base=mk((5, b(-10.0))), fx=(1, 0, 0, 0))                      # not collapsed (FX id)
    run('p5_flag0_stops_at_noncollapsed_parent', [top, Node(bus=0, base=mk((5, b(-1.0))))], 5)
    top_c = Node(bus=0, base=mk((5, b(-10.0))))                              # collapsed: has an output bus of its own, nothing else
    run('p5_flag0_continues_through_collapsed_parent', [Node(base=mk((5, b(-20.0))), fx=(1, 0, 0, 0)), top_c, Node(bus=1, base=mk((5, b(-1.0))))], 5, start=2)
    run('p5_flag1_always_recurses', [top, Node(bus=0, base=mk((5, b(-1.0))))], 5, flag=1)
    run('p5_flag2_is_not_1', [top, Node(bus=0, base=mk((5, b(-1.0))))], 5, flag=2)
    run('p5_flag1_chain_of_three', [Node(base=mk((5, b(-4.0)))), Node(bus=0, base=mk((5, b(-2.0)))), Node(bus=1, base=mk((5, b(-1.0))))], 5, flag=1)
    # the RTPC bit
    sub = lambda node, param, typ, accum, curves: Sub(node, param, typ, accum, curves)
    vals = ((900, 0.0, True, 50.0),)
    run('p5_rtpc_sum', [Node(base=base5, mask=1 << 5)], 5, values=vals, subs=[sub(0, 5, 0, 1, [(900, 2.0, 0.5)])])
    run('p5_rtpc_bit_clear_no_call', [Node(base=base5, mask=1 << 4)], 5, values=vals, subs=[sub(0, 5, 0, 1, [(900, 2.0, 0.5)])])
    run('p0_rtpc_bit0', [Node(mask=1)], 0, values=vals, subs=[sub(0, 0, 0, 1, [(900, 2.0, 0.5)])])
    run('p5_rtpc_no_subscription_gives_zero', [Node(mask=1 << 5)], 5, values=vals, subs=[])
    run('p5_rtpc_two_curves_sum', [Node(mask=1 << 5)], 5, values=((900, 0.0, True, 50.0), (901, 0.0, True, 1.0)), subs=[sub(0, 5, 0, 1, [(900, 2.0, 0.5), (901, 1.0, 0.0)])])
    run('p5_rtpc_two_curves_product', [Node(mask=1 << 5)], 5, values=((900, 0.0, True, 50.0), (901, 0.0, True, 1.0)), subs=[sub(0, 5, 0, 2, [(900, 2.0, 0.5), (901, 1.0, 0.0)])])
    run('p5_rtpc_stmg_default', [Node(mask=1 << 5)], 5, values=((900, 4.0, False, 0.0),), subs=[sub(0, 5, 0, 1, [(900, 2.0, 0.5)])])
    run('p5_rtpc_real_event_volume_curve', [Node(mask=1 << 5)], 5, values=((900, 0.0, True, 0.5),), subs=[sub(0, 5, 0, 1, [(900, 2, [(0.0, -1.0, 1), (1.0, 0.0, 4)])])])
    run('p5_rtpc_absent_id_param5_calls_9e6748', [Node(mask=1 << 5)], 5, subs=[sub(0, 5, 0, 1, [(900, 2.0, 0.5)])], e6748=(1, 7.0))
    run('p5_rtpc_absent_id_9e6748_zero_result', [Node(mask=1 << 5)], 5, subs=[sub(0, 5, 0, 1, [(900, 2.0, 0.5)])], e6748=(0, 7.0))
    run('p0_rtpc_absent_id_param0_skips', [Node(mask=1)], 0, subs=[sub(0, 0, 0, 1, [(900, 2.0, 0.5)])], e6748=(1, 7.0))
    run('p0_rtpc_absent_id_type1_calls_9e6748', [Node(mask=1)], 0, subs=[sub(0, 0, 1, 1, [(900, 2.0, 0.5)])], e6748=(1, 7.0))
    run('p0_rtpc_absent_id_param0_skips_product', [Node(mask=1)], 0, values=((901, 0.0, True, 3.0),), subs=[sub(0, 0, 0, 2, [(900, 2.0, 0.5), (901, 1.0, 0.0)])], e6748=(1, 7.0))
    return out


def case_a11590():
    """0xA11590 directly: key1 = node+0x10 of node 0."""
    out = {}

    def run(name, nodes, key_node, param, values=(), subs=(), e6748=(0, 0.0), use_key1=None):
        w = World(nodes, values, subs, e6748)
        key = w.e.alloc(0x20)
        w.e.w32(key, 0); w.e.w32(key + 4, 0); w.e.w32(key + 8, 0); w.e.w8(key + 0xC, 0xFF); w.e.w8(key + 0x10, 0xFF); w.e.w32(key + 0x14, 0)
        k1 = w.addr[key_node] + 0x10 if use_key1 is None else use_key1
        r = w.e.call(0xA11590, w.mgr, k1, param, key)
        out[name] = dict(nodes=nodes, param=param, key_node=key_node, values=values, subs=subs, e6748=e6748, expected=r, log=[l[0] for l in w.e.log])
    n = [Node()]
    sub = lambda param, typ, accum, curves: Sub(0, param, typ, accum, curves)
    run('empty_table', n, 0, 3)
    run('not_found_wrong_param', n, 0, 4, values=((900, 0.0, True, 1.0),), subs=[sub(3, 0, 1, [(900, 2.0, 0.5)])])
    run('sum_one_curve', n, 0, 3, values=((900, 0.0, True, 1.0),), subs=[sub(3, 0, 1, [(900, 2.0, 0.5)])])
    run('accumulate_0_is_sum', n, 0, 3, values=((900, 0.0, True, 1.0),), subs=[sub(3, 0, 0, [(900, 2.0, 0.5)])])
    run('accumulate_3_is_sum', n, 0, 3, values=((900, 0.0, True, 1.0),), subs=[sub(3, 0, 3, [(900, 2.0, 0.5)])])
    run('product_two_curves', n, 0, 3, values=((900, 0.0, True, 1.0), (901, 0.0, True, 4.0)), subs=[sub(3, 0, 2, [(900, 2.0, 0.5), (901, 0.5, 1.0)])])
    run('sum_three_curves_order', n, 0, 3, values=((900, 0.0, True, 1.0), (901, 0.0, True, 3.0), (902, 0.0, True, 5.0)), subs=[sub(3, 0, 1, [(900, 1.0, 1e-8), (901, 1.0, 1.0), (902, 1.0, -1.0)])])
    run('empty_curve_list_sum', n, 0, 3, subs=[sub(3, 0, 1, [])])
    run('empty_curve_list_product', n, 0, 3, subs=[sub(3, 0, 2, [])])
    run('stmg_default_when_root_invalid', n, 0, 3, values=((900, 6.0, False, 99.0),), subs=[sub(3, 0, 1, [(900, 2.0, 0.5)])])
    run('absent_param0_skip', n, 0, 0, subs=[sub(0, 0, 0, [(900, 2.0, 0.5)])], e6748=(1, 9.0))
    run('absent_param7_skip', n, 0, 7, subs=[sub(7, 0, 0, [(900, 2.0, 0.5)])], e6748=(1, 9.0))
    run('absent_param7_type2_skip', n, 0, 7, subs=[sub(7, 2, 0, [(900, 2.0, 0.5)])], e6748=(1, 9.0))
    run('absent_param2_calls_9e6748_result1', n, 0, 2, subs=[sub(2, 0, 0, [(900, 2.0, 0.5)])], e6748=(1, 9.0))
    run('absent_param2_calls_9e6748_result0', n, 0, 2, subs=[sub(2, 0, 0, [(900, 2.0, 0.5)])], e6748=(0, 9.0))
    run('absent_type1_param0_calls_9e6748', n, 0, 0, subs=[sub(0, 1, 0, [(900, 2.0, 0.5)])], e6748=(1, 9.0))
    # real shipped-shape curves (batch 5d, C35): event_volume (scaling 2, (0,-1,interp 1)->(1,0,interp 4)) and others, the engine's own 0xA14E28 inside the accumulators
    ev = (900, 2, [(0.0, -1.0, 1), (1.0, 0.0, 4)])
    scurve = (901, 0, [(0.0, 0.0, 5), (1.0, 1.0, 4)])
    pw = (902, 3, [(0.0, -60.0, 4), (100.0, 0.0, 4)])
    run('real_event_volume_half', n, 0, 3, values=((900, 0.0, True, 0.5),), subs=[sub(3, 0, 1, [ev])])
    run('real_event_volume_stmg_default_zero', n, 0, 3, values=((900, 0.0, False, 0.0),), subs=[sub(3, 0, 1, [ev])])
    run('real_sum_two_curves_scaling2_and_scurve', n, 0, 3, values=((900, 0.0, True, 0.75), (901, 0.0, True, 0.3)), subs=[sub(3, 0, 1, [ev, scurve])])
    run('real_product_scaling3', n, 0, 3, values=((902, 0.0, True, 50.0), (901, 0.0, True, 0.999)), subs=[sub(3, 0, 2, [pw, scurve])])
    run('real_sum_three_curves_float_order', n, 0, 3, values=((900, 0.0, True, 0.995), (901, 0.0, True, 0.5), (902, 0.0, True, 10.0)), subs=[sub(3, 0, 1, [ev, scurve, pw])])
    run('real_nan_value_gives_last_point', n, 0, 3, values=((901, 0.0, True, float('nan')),), subs=[sub(3, 0, 1, [scurve])])
    return out


def case_ctor():
    """0x9C3620: the bus constructor with the three unread callees stood in."""
    out = {}
    for cat in (0, 0xC, 0xA, 1, 3, 5):
        w = None
        e = Emu()
        e.std_hooks()
        e.log = []
        mem = {}
        # the constructor allocates 0xD0 bytes through 0xA7A7F4 (the std hook) and calls 0x9F402C(obj, id), 0xA19F94(obj+0xC4), 0x9F40F4(obj)
        e.hook(0x9F402C, lambda emu: (e.log.append(('9F402C', emu.reg(1))), emu.w32(emu.reg(0) + 8, emu.reg(1)))[0] or emu.reg(0))
        e.hook(0xA19F94, lambda emu: e.log.append(('A19F94', emu.reg(0))))
        e.hook(0x9F40F4, lambda emu: e.log.append(('9F40F4', emu.reg(0))))
        vt = e.alloc(0x200)
        # the real vptr 0x103ACE0 has category 0; for the other categories a private vtable whose +0x44 slot is a stub
        stub = BASE + 0x40100
        e.hook(stub, lambda emu: cat)
        # the constructor writes the real vptr itself ([bus] = 0x103ACE0 + 8 relative); the category comes from vt+0x44 of that vptr, so patch the image word for this run
        slot = 0x103ACE0 + 0x44
        e.w32(slot, stub)
        # object pre-filled with 0xFF garbage (the allocator's memory is not zero)
        orig_alloc = e.alloc
        got = {}

        def alloc_hook(emu):
            a = e.alloc(emu.reg(1))
            e.uc.mem_write(a, bytes([0xAA]) * emu.reg(1))
            got['a'] = a
            return a
        e.hook(0xA7A7F4, alloc_hook)
        r = e.call(0x9C3620, 0x1234ABCD)
        a = got['a']
        res = dict(word68=e.r32(a + 0x68), maxduck=e.r32(a + 0x6C), w54=e.r32(a + 0x54), b46=e.r8(a + 0x46), bcc=e.r8(a + 0xCC), id8=e.r32(a + 8), ret_is_obj=(r == a),
                   log=[l[0] for l in e.log], w48_64=[e.r32(a + o) for o in range(0x48, 0x68, 4)], w7c=[e.r32(a + o) for o in (0x7C, 0x80, 0x84, 0x98, 0x9C, 0xA0, 0xB4, 0xB8, 0xBC)],
                   l70=[e.r32(a + o) for o in (0x70, 0x74, 0x78, 0x88, 0x8C, 0x90, 0x94, 0xA4, 0xA8, 0xAC, 0xB0, 0xC0)])
        out['category_%X' % cat] = dict(cat=cat, expected=res)
        e.w32(slot, 0x9C07BC)
    return out


# ---------------------------------------------------------------------------------------------------------------- the bus reader pieces (B6)

STUB_BASE = BASE + 0x48000


def bus_with_vtable(e, log):
    """A bus object whose vptr is a private vtable: +0x44 = category stub, +0xC4 / +0x8C / +0xE0 stubs that log (and +0xE0 returns the scripted mixer result)."""
    vt = e.alloc(0x200)
    stubs = {}
    for off, name in ((0x44, 'vt44'), (0xC4, 'vtC4'), (0x8C, 'vt8C'), (0xE0, 'vtE0')):
        a = STUB_BASE + off * 4
        stubs[name] = a
        e.w32(vt + off, a)
    e.hook(stubs['vt44'], lambda emu: 0)
    e.hook(stubs['vtC4'], lambda emu: log.append(('vtC4', emu.reg(1))))
    e.hook(stubs['vt8C'], lambda emu: log.append(('vt8C', emu.reg(1))))
    bus = e.alloc(0x100)
    e.uc.mem_write(bus, bytes(0x100))
    e.w32(bus, vt)
    e.w32(bus + 8, 0x1234)
    return bus, stubs


def case_fx9f5760():
    out = {}
    scenarios = {
        'new_chunk_slot0': dict(chunk=None, slot=0, id=7, share=1, version=0),
        'new_chunk_slot3': dict(chunk=None, slot=3, id=9, share=0, version=5),
        'slot4_rejected': dict(chunk=None, slot=4, id=7, share=1, version=0),
        'alloc_fails': dict(chunk=None, slot=0, id=7, share=1, version=0, fail=True),
        'same_values_no_callbacks': dict(chunk=(0, [7, 0, 0, 0], [1, 0, 0, 0]), slot=0, id=7, share=1, version=0),
        'id_changes': dict(chunk=(0, [7, 0, 0, 0], [1, 0, 0, 0]), slot=0, id=8, share=1, version=0),
        'share_changes': dict(chunk=(0, [7, 0, 0, 0], [1, 0, 0, 0]), slot=0, id=7, share=0, version=0),
        'older_version_ignored': dict(chunk=(3, [7, 0, 0, 0], [1, 0, 0, 0]), slot=0, id=9, share=0, version=2),
        'equal_version_applies': dict(chunk=(3, [7, 0, 0, 0], [1, 0, 0, 0]), slot=1, id=9, share=1, version=3),
        'newer_version_stores_it': dict(chunk=(3, [7, 0, 0, 0], [1, 0, 0, 0]), slot=1, id=9, share=1, version=4),
        'negative_version_chunk': dict(chunk=(-1, [0, 0, 0, 0], [0, 0, 0, 0]), slot=2, id=4, share=1, version=0),
    }
    for name, sc in scenarios.items():
        e = Emu()
        e.std_hooks()
        log = []
        bus, stubs = bus_with_vtable(e, log)
        if sc['chunk'] is not None:
            ver, ids, shares = sc['chunk']
            c = e.alloc(0x28)
            e.uc.mem_write(c, bytes(0x28))
            e.w32(c, ver)
            for i in range(4):
                e.w32(c + 4 + 8 * i, ids[i])
                e.w8(c + 9 + 8 * i, shares[i])
            e.w32(bus + 0x28, c)
        if sc.get('fail'):
            e.fail_alloc_at = 1
        r = e.call(0x9F5760, bus, sc['slot'], sc['id'], sc['share'], stack=[sc['version']])
        c = e.r32(bus + 0x28)
        ver_after = None
        if c:
            ver_after = e.r32(c)
            if ver_after >= 1 << 31:
                ver_after -= 1 << 32
        res = dict(ret=r, chunk=None if c == 0 else (ver_after, [e.r32(c + 4 + 8 * i) for i in range(4)], [e.r8(c + 9 + 8 * i) for i in range(4)]),
                   log=[l[0] + (':%d' % l[1] if l[0] == 'vt8C' else '') for l in log])
        out[name] = dict(sc=sc, expected=res)
    return out


def case_fxlist():
    out = {}

    def entry(slot, id_, share, extra=0):
        return bytes([slot]) + id_.to_bytes(4, 'little') + bytes([share, extra])
    mixer = lambda mid, flag: mid.to_bytes(4, 'little') + bytes([flag])
    scenarios = {
        'count0_mixer': (bytes([0]) + mixer(0, 0), 1),
        'count0_mixer_id_flag': (bytes([0]) + mixer(0x55, 1), 1),
        'one_fx': (bytes([1, 9]) + entry(0, 7, 1) + mixer(0, 0), 1),
        'two_fx_one_zero_id': (bytes([2, 9]) + entry(0, 0, 1) + entry(2, 8, 0) + mixer(0, 0), 1),
        'fx_slot_out_of_range': (bytes([2, 9]) + entry(5, 7, 1) + entry(1, 8, 0) + mixer(0, 0), 1),
        'mixer_result_2': (bytes([0]) + mixer(0x55, 0), 2),
        'three_fx': (bytes([3, 4]) + entry(0, 1, 1) + entry(1, 2, 0) + entry(3, 3, 1) + mixer(0x66, 1), 1),
    }
    for name, (data, mixer_ret) in scenarios.items():
        e = Emu()
        e.std_hooks()
        log = []
        bus, stubs = bus_with_vtable(e, log)
        e.hook(stubs['vtE0'], lambda emu, log=log, mr=mixer_ret: (log.append(('vtE0', emu.reg(1), emu.reg(2), emu.reg(3))), mr)[1])
        e.hook(0x9F5C30, lambda emu, log=log: log.append(('9F5C30', emu.reg(1), emu.reg(2))))
        buf = e.alloc(len(data) + 8)
        e.uc.mem_write(buf, data)
        cur = e.alloc(8)
        e.w32(cur, buf)
        e.w32(bus + 0x40, 0x100)
        r = e.call(0x9C0D08, bus, cur)
        names = []
        for l in log:
            if l[0] == 'vtE0':
                names.append('mixer:%d/%d/%d' % (l[1], l[2], l[3]))
            elif l[0] == '9F5C30':
                names.append('9F5C30:%d/%d' % (l[1], l[2]))
            elif l[0] == 'vt8C':
                names.append('vt8C:%d' % l[1])
            else:
                names.append(l[0])
        c = e.r32(bus + 0x28)
        res = dict(ret=r, consumed=e.r32(cur) - buf, w40=e.r32(bus + 0x40), log=names,
                   chunk=None if c == 0 else ([e.r32(c + 4 + 8 * i) for i in range(4)], [e.r8(c + 9 + 8 * i) for i in range(4)]))
        out[name] = dict(data=data, mixer_ret=mixer_ret, expected=res)
    return out


def case_reader():
    """The whole bus reader function 0x9C6420 over a stream: count 0, byte A 0, byte F, u16, the channel config word, byte C. 0x9F627C / 0x9F68D8 / 0x9F6DB4 / 0xA4454C / 0x9C62AC are recorded."""
    out = {}
    cfgs = {
        'mono_4101': (0x4101, 0x00000000),
        'unchanged_4101': (0x4101, 0x4101),
        'byte_changed': (0x4101, (0x4201 & ~0xFF) | 2),
        'nibble_changed': (0x4101, 0x4001),
        'hi_changed': (0x4101, 0x8101),
        'stereo_3102': (0x3102, 0),
        'kind0_byte': (0x00000007, 0),
        'kind0_hi': (0x12345603, 0),
        'kind1_masked_zero': (0x00040100, 0),
        'kind1_mask_bits': (0xFFFFF100, 0),
        'kind1_three_bits': (0x0001D100, 0xFFFFFFFF),
        'kind2_same_as_old': (0x00005202, 0x00005202),
    }
    for name, (cfg, old) in cfgs.items():
        for f, c in ((0, 0), (8, 3), (0xF, 1), (4, 2)):
            e = Emu()
            e.std_hooks()
            log = []
            bus = e.alloc(0x100)
            e.uc.mem_write(bus, bytes(0x100))
            e.w32(bus + 0x68, old)
            e.w8(bus + 0x46, 0x20)
            w40_in = 0x00000001 | (0xE0000 if c == 0 and f == 8 else 0)
            e.w32(bus + 0x40, w40_in)
            e.w8(bus + 0xCC, 0x30)
            e.w32(bus + 8, 0x1234)
            for a in (0x9F627C, 0x9F68D8, 0x9F6DB4, 0xA4454C, 0x9C62AC):
                e.hook(a, lambda emu, a=a: log.append(a) or 0)
            data = bytes([0, 0, f]) + (0x0123).to_bytes(2, 'little') + cfg.to_bytes(4, 'little') + bytes([c])
            buf = e.alloc(len(data) + 8)
            e.uc.mem_write(buf, data)
            cur = e.alloc(8)
            e.w32(cur, buf)
            r = e.call(0x9C6420, bus, cur)
            out['%s_f%X_c%d' % (name, f, c)] = dict(cfg=cfg, old=old, f=f, c=c, w40_in=w40_in, expected=dict(
                ret=r, word68=e.r32(bus + 0x68), b46=e.r8(bus + 0x46), w40=e.r32(bus + 0x40), bcc=e.r8(bus + 0xCC), a4454c=(0xA4454C in log), c62ac=(0x9C62AC in log), consumed=e.r32(cur) - buf))
    return out


def case_be898():
    import emu_play as P
    out = {}
    scenarios = {
        'e9_bit2_clears': dict(e8=0x5D, e9=5, pan=(9.0, 8.0, 7.0), c0=9),
        'e9_bit2_e8_low_bits': dict(e8=0x5F, e9=4, pan=(1.0, 2.0, 3.0), c0=1),
        'bits01_eq1_runs_9fb9b8': dict(e8=0x5D, e9=1, pan=(0.0, 0.0, 0.0), c0=0),
        'bits01_eq1_other_e8': dict(e8=0xA1, e9=0, pan=(0.0, 0.0, 0.0), c0=0),
        'bits01_not1_nothing': dict(e8=0x5E, e9=1, pan=(4.0, 5.0, 6.0), c0=3),
        'bits01_zero_nothing': dict(e8=0x5C, e9=1, pan=(4.0, 5.0, 6.0), c0=3),
        'bits01_eq3_nothing': dict(e8=0x5F, e9=1, pan=(4.0, 5.0, 6.0), c0=3),
    }
    for name, sc in scenarios.items():
        e = Emu()                                   # not P.setup(): its stand-in for 0x9BE898 would replace the function under test
        e.std_hooks()
        e.log = []
        root = P.make_node(e, props={0xC: bits(0.5), 0xD: bits(-0.25), 0xE: bits(0.75)}, node_id=10)
        leaf = P.make_node(e, parent=root, node_id=11)
        pbi = P.make_pbi(e, leaf, sc['e8'], sc['e9'])
        e.wf(pbi + 0xB4, sc['pan'][0]); e.wf(pbi + 0xB8, sc['pan'][1]); e.wf(pbi + 0xBC, sc['pan'][2]); e.w8(pbi + 0xC0, sc['c0'])
        outp = BASE + 0x61000
        e.w32(outp, 0xDEADBEEF)
        r = e.call(0x9BE898, pbi + 0xC, outp)
        out[name] = dict(sc=sc, expected=dict(ret=r, e8=e.r8(pbi + 0xE8), pan=[e.r32(pbi + o) for o in (0xB4, 0xB8, 0xBC)], c0=e.r8(pbi + 0xC0), outp=e.r32(outp)))
    return out


# ---------------------------------------------------------------------------------------------------------------- the generated C# file

def cs_bits(v):
    return '0x%08XU' % (v & 0xFFFFFFFF)


def cs_pairs(pairs):
    if pairs is None:
        return 'null'
    return 'new (byte, uint)[] { ' + ', '.join('(%d, %s)' % (k, cs_bits(v)) for k, v in pairs) + ' }'


def cs_node(n):
    parts = []
    if n.parent >= 0:
        parts.append('Parent = %d' % n.parent)
    if n.bus >= 0:
        parts.append('Bus = %d' % n.bus)
    if n.cat:
        parts.append('Cat = %d' % n.cat)
    if n.fx is not None:
        parts.append('Fx = new uint[] { %s }' % ', '.join(str(x) for x in n.fx))
    if n.b68:
        parts.append('B68 = %d' % n.b68)
    if n.b46:
        parts.append('B46 = %d' % n.b46)
    if n.w40:
        parts.append('W40 = %s' % cs_bits(n.w40))
    if n.w54:
        parts.append('W54 = %s' % cs_bits(n.w54))
    if n.base is not None:
        parts.append('Base = %s' % cs_pairs(n.base))
    if n.ranged is not None:
        parts.append('Ranged = %s' % cs_pairs(n.ranged))
    if n.mask is not None:
        parts.append('Mask = 0x%XUL' % n.mask)
    if n.duck8c:
        parts.append('Duck8C = new uint[] { %s }' % ', '.join(cs_bits(x) for x in n.duck8c))
    if n.ducka8:
        parts.append('DuckA8 = new uint[] { %s }' % ', '.join(cs_bits(x) for x in n.ducka8))
    if n.maxduck != 0xC2C0999A:
        parts.append('MaxDuck = %s' % cs_bits(n.maxduck))
    if n.states:
        parts.append('States = new (byte, uint)[]?[] { %s }' % ', '.join(cs_pairs(x) for x in n.states))
    return 'new BusNodeSpec { %s }' % ', '.join(parts)


def cs_nodes(nodes):
    return 'new[] { ' + ', '.join(cs_node(n) for n in nodes) + ' }'


def cs_values(values):
    return 'new RtpcValueSpec[] { ' + ', '.join('new(%d, %s, %s, %s)' % (rid, cs_bits(b(d)), 'true' if rv else 'false', cs_bits(b(r))) for (rid, d, rv, r) in values) + ' }'


def cs_pts(pts):
    return 'new (uint, uint, uint)[] { ' + ', '.join('(%s, %s, %d)' % (cs_bits(b(px)), cs_bits(b(py)), pi_) for px, py, pi_ in pts) + ' }'


def cs_subs(subs):
    return 'new RtpcSubSpec[] { ' + ', '.join('new(%d, %d, %d, %d, new (uint, byte, (uint, uint, uint)[])[] { %s })' % (
        s.node, s.param, s.typ, s.accum, ', '.join('(%d, %d, %s)' % (rid, scaling, cs_pts(pts)) for rid, scaling, pts in s.curves)) for s in subs) + ' }'


def cs_logs(log):
    return 'new[] { ' + ', '.join('"%s"' % x for x in log) + ' }' if log else 'Array.Empty<string>()'


def main_cs():
    print('// <auto-generated> by re-analysis/tools/emu/emu_bus.py from the engine\'s own output (the real 0x9F4BB8, 0x9BDA6C, 0x9C54E8, 0x9F9CDC, 0x9C39DC, 0xA11590 / 0xA17878 / 0xA17724 / 0xA17280 and 0x9C3620 under Unicorn): do not edit. </auto-generated>')
    print('namespace Cozmo.Protocol.Tests;')
    print('')
    print('internal static class BusWalkOracle')
    print('{')
    print('    public static readonly Dictionary<string, (BusNodeSpec[] Nodes, int Start, int Expected)> F4BB8 = new()')
    print('    {')
    for k, v in case_9f4bb8().items():
        print('        ["%s"] = (%s, %d, %d),' % (k, cs_nodes(v['nodes']), v['start'], v['expected']))
    print('    };')
    print('')
    print('    public static readonly Dictionary<string, (BusNodeSpec[] Nodes, byte E9, int Expected)> BDA6C = new()')
    print('    {')
    for k, v in case_9bda6c().items():
        print('        ["%s"] = (%s, %d, %d),' % (k, cs_nodes(v['nodes']), v['e9'], v['expected']))
    print('    };')
    print('')
    print('    public static readonly Dictionary<string, (BusNodeSpec[] Nodes, int Start, int Expected)> C54E8 = new()')
    print('    {')
    for k, v in case_9c54e8().items():
        print('        ["%s"] = (%s, %d, %d),' % (k, cs_nodes(v['nodes']), v['start'], v['expected']))
    print('    };')
    print('')
    print('    public static readonly Dictionary<string, (BusNodeSpec[] Nodes, uint Mask, uint[] Expected)> F9CDC = new()')
    print('    {')
    for k, v in case_9f9cdc().items():
        print('        ["%s"] = (%s, %d, new uint[] { %s }),' % (k, cs_nodes(v['nodes']), v['mask'], ', '.join(cs_bits(x) for x in v['expected'])))
    print('    };')
    print('')
    print('    public static readonly Dictionary<string, (BusNodeSpec[] Nodes, int Start, int Flag, uint P, RtpcValueSpec[] Values, RtpcSubSpec[] Subs, uint Expected, string[] Log)> C39DC = new()')
    print('    {')
    for k, v in case_9c39dc().items():
        print('        ["%s"] = (%s, %d, %d, %d, %s, %s, %s, %s),' % (k, cs_nodes(v['nodes']), v['start'], v['flag'], v['p'], cs_values(v['values']), cs_subs(v['subs']), cs_bits(v['expected']), cs_logs(v['log'])))
    print('    };')
    print('')
    print('    public static readonly Dictionary<string, (BusNodeSpec[] Nodes, uint Param, RtpcValueSpec[] Values, RtpcSubSpec[] Subs, uint Expected, string[] Log)> A11590 = new()')
    print('    {')
    for k, v in case_a11590().items():
        print('        ["%s"] = (%s, %d, %s, %s, %s, %s),' % (k, cs_nodes(v['nodes']), v['param'], cs_values(v['values']), cs_subs(v['subs']), cs_bits(v['expected']), cs_logs(v['log'])))
    print('    };')
    print('')
    print('    /// <summary>vt+0x44 of the shipped vtables, read from the .so: the slot [vptr + 0x44] points at a body whose first instruction is mov r0,#n.</summary>')
    print('    public static readonly Dictionary<string, int> Vt44 = new()')
    print('    {')
    from so import u32, MD, RAW, off
    for name, vptr in (('Bus', 0x103ACE0), ('AudioDeviceBus', 0x103D100), ('Class1039EA8', 0x1039EA8), ('Class103A018', 0x103A018), ('Class103A198', 0x103A198), ('Class103A470', 0x103A470),
                       ('Layer', 0x103B050), ('RandomSequence', 0x103B860), ('Sound', 0x103BAD0), ('Switch', 0x103BDC8), ('ActorMixer', 0x103CF88)):
        f = u32(vptr + 0x44)
        ins = list(MD.disasm(RAW[off(f):off(f) + 8], f))
        assert ins[0].mnemonic == 'mov' and ins[0].op_str.startswith('r0, #') and ins[1].mnemonic == 'bx', (name, ins)
        print('        ["%s"] = %d,' % (name, int(ins[0].op_str.split('#')[1], 0)))
    print('    };')
    print('')
    print('    /// <summary>The constructor 0x9C3620 on memory pre-filled with 0xAA: (category, word68, maxDuckBits, word54, byte46, byteCC, id, callee log).</summary>')
    print('    public static readonly Dictionary<int, (uint Word68, uint MaxDuck, uint Word54, byte B46, byte BCC, uint Id, string[] Log)> Ctor = new()')
    print('    {')
    for k, v in case_ctor().items():
        r = v['expected']
        print('        [%d] = (%s, %s, %s, %d, %d, %s, %s),' % (v['cat'], cs_bits(r['word68']), cs_bits(r['maxduck']), cs_bits(r['w54']), r['b46'], r['bcc'], cs_bits(r['id8']), cs_logs(r['log'])))
    print('    };')
    print('')
    print('    /// <summary>0x9F5760: (slot, id, share, version, existing chunk (version, ids, shares) or null, allocation fails) -> (return, chunk after, callbacks).</summary>')
    print('    public static readonly Dictionary<string, (int Slot, uint Id, byte Share, int Version, (int Version, uint[] Ids, byte[] Share)? Chunk, bool Fail, uint Ret, (int Version, uint[] Ids, byte[] Share)? After, string[] Log)> Fx = new()')
    print('    {')
    for k, v in case_fx9f5760().items():
        sc, r = v['sc'], v['expected']
        ch = sc['chunk']
        print('        ["%s"] = (%d, %d, %d, %d, %s, %s, %d, %s, %s),' % (k, sc['slot'], sc['id'], sc['share'], sc['version'],
              'null' if ch is None else '(%d, new uint[] { %s }, new byte[] { %s })' % (ch[0], ', '.join(str(x) for x in ch[1]), ', '.join(str(x) for x in ch[2])),
              'true' if sc.get('fail') else 'false', r['ret'],
              'null' if r['chunk'] is None else '(%d, new uint[] { %s }, new byte[] { %s })' % (r['chunk'][0], ', '.join(str(x) for x in r['chunk'][1]), ', '.join(str(x) for x in r['chunk'][2])),
              'new string[] { %s }' % ', '.join('"%s"' % x for x in r['log']) if r['log'] else 'Array.Empty<string>()'))
    print('    };')
    print('')
    print('    /// <summary>0x9C0D08: (bytes, mixer result) -> (return, bytes consumed, [bus+0x40] after (starts 0x100), calls in order, chunk ids / shares or null).</summary>')
    print('    public static readonly Dictionary<string, (byte[] Data, int MixerRet, uint Ret, int Consumed, uint W40, string[] Log, (uint[] Ids, byte[] Share)? Chunk)> FxList = new()')
    print('    {')
    for k, v in case_fxlist().items():
        r = v['expected']
        print('        ["%s"] = (new byte[] { %s }, %d, %d, %d, %s, %s, %s),' % (k, ', '.join(str(x) for x in v['data']), v['mixer_ret'], r['ret'], r['consumed'], cs_bits(r['w40']),
              'new string[] { %s }' % ', '.join('"%s"' % x for x in r['log']) if r['log'] else 'Array.Empty<string>()',
              'null' if r['chunk'] is None else '(new uint[] { %s }, new byte[] { %s })' % (', '.join(str(x) for x in r['chunk'][0]), ', '.join(str(x) for x in r['chunk'][1]))))
    print('    };')
    print('')
    print('    /// <summary>The bus reader 0x9C6420 (count 0, A 0, F, u16, config word, C): (cfg, old [+0x68], F, C, [+0x40] in) -> ([+0x68], [+0x46] (starts 0x20), [+0x40], [+0xCC] (starts 0x30), 0xA4454C called, 0x9C62AC called).</summary>')
    print('    public static readonly Dictionary<string, (uint Cfg, uint Old, byte F, byte C, uint W40In, uint Word68, byte B46, uint W40, byte BCC, bool Changed, bool Bit3Call)> Reader = new()')
    print('    {')
    for k, v in case_reader().items():
        r = v['expected']
        assert r['ret'] == 1 and r['consumed'] == 3 + 2 + 4 + 1, (k, r)
        print('        ["%s"] = (%s, %s, %d, %d, %s, %s, %d, %s, %d, %s, %s),' % (k, cs_bits(v['cfg']), cs_bits(v['old']), v['f'], v['c'], cs_bits(v['w40_in']), cs_bits(r['word68']), r['b46'], cs_bits(r['w40']), r['bcc'],
              'true' if r['a4454c'] else 'false', 'true' if r['c62ac'] else 'false'))
    print('    };')
    print('')
    print('    /// <summary>0x9BE898 with [ctx+0xD0] == 0 on a leaf under a root with pan properties 0.5 / -0.25 / 0.75: (E8, E9, pan floats in, [ctx+0xB4] in) -> (return, E8, pan bits, [ctx+0xB4], *out unchanged).</summary>')
    print('    public static readonly Dictionary<string, (byte E8, byte E9, uint[] PanIn, byte C0In, uint Ret, byte E8Out, uint[] PanOut, byte C0Out, bool OutUnchanged)> Be898 = new()')
    print('    {')
    for k, v in case_be898().items():
        sc, r = v['sc'], v['expected']
        print('        ["%s"] = (0x%02X, %d, new uint[] { %s }, %d, %d, 0x%02X, new uint[] { %s }, %d, %s),' % (k, sc['e8'], sc['e9'], ', '.join(cs_bits(bits(x)) for x in sc['pan']), sc['c0'], r['ret'], r['e8'], ', '.join(cs_bits(x) for x in r['pan']), r['c0'], 'true' if r['outp'] == 0xDEADBEEF else 'false'))
    print('    };')
    print('}')


def main_text():
    for fn in (case_9f4bb8, case_9bda6c, case_9c54e8, case_9f9cdc, case_9c39dc, case_a11590, case_ctor):
        print('---', fn.__name__)
        for k, v in fn().items():
            print('  ', k, {kk: vv for kk, vv in v.items() if kk not in ('nodes', 'subs', 'values')})


if __name__ == '__main__':
    if '--text' in sys.argv:
        main_text()
    else:
        main_cs()
