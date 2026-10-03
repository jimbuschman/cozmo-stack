"""Run the engine's own Play-path bodies under Unicorn, for WwisePlayPathTests (M6-025 / M6-026 C31.3, C32.1).

0x9BEB30 (the PBI context init), CalcEffectiveParams 0x9FFAD4, 0x9FF368, 0x9BCA68, 0x9FB9B8 / 0x9FAEE8 / 0x9FBE74, 0x9BDA6C / 0x9F4BB8, 0x9F6B94, 0x9EEDA4, 0x9F1F80, 0xA1E280, 0xA00618, 0xA36268 and
0xA358EC run on a PBI, nodes, a bus and a params block built in emulated memory. Python stand-ins (all of them for bodies the inventory does not adopt): the mutexes, memset/memcpy/expf, the allocator, node
vt+0xAC (GetAudioParameters: it writes values the scenario chooses), 0x9C54E8, 0x9C39DC, 0x9F36A0 (recorded), 0x9E8224, 0x9E62AC, 0xA11590, 0x9BE898, 0xA35D44.
The expected values in WwisePlayPathTests are this script's output, not the C#'s.

    python re-analysis/tools/emu/emu_play.py
"""
import math
import struct
import sys
from emu_common import *

PBI = BASE + 0x20000
PARAMS = BASE + 0x30000
VT = BASE + 0x40000          # a node vtable: +0xAC is the GetAudioParameters stand-in
STUB_AC = BASE + 0x40100
OUTCODE = BASE + 0x41000
BELOW = BASE + 0x41010
NODE0 = BASE + 0x50000


def setup():
    e = Emu()
    e.std_hooks()
    e.call(0x4DDFF0)                                   # the static constructor that stores the priority defaults 50.0f / -10.0f
    e.log = []
    e.cfg = {'bus_flag': 1, 'bus_vol': 0.0, 'ac': None, 'rtpc': 0.0}
    e.hook(0x4D36DC, lambda emu: emu.uc.mem_write(emu.reg(0), bytes([emu.reg(1) & 0xFF]) * emu.reg(2)) or emu.reg(0))     # memset
    e.hook(0x4D37F0, lambda emu: emu.uc.mem_write(emu.reg(0), bytes(emu.uc.mem_read(emu.reg(1), emu.reg(2)))) or emu.reg(0))   # memcpy

    def bus_flag(emu):
        e.log.append(('9C54E8', emu.reg(0)))
        return e.cfg['bus_flag']
    e.hook(0x9C54E8, bus_flag)

    def bus_vol(emu):
        e.log.append(('9C39DC', emu.reg(0), emu.reg(1), emu.reg(2)))
        return bits(e.cfg['bus_vol'])
    e.hook(0x9C39DC, bus_vol)
    e.hook(0x9F36A0, lambda emu: e.log.append(('9F36A0', emu.reg(0), emu.reg(1), emu.reg(2))))
    e.hook(0x9E8224, lambda emu: e.log.append(('9E8224', emu.reg(0))))
    e.hook(0x9E62AC, lambda emu: e.log.append(('9E62AC',)))
    e.hook(0x9BE898, lambda emu: e.log.append(('9BE898',)) or 1)
    e.hook(0xA11590, lambda emu: e.log.append(('A11590', emu.reg(2))) or bits(e.cfg['rtpc']))
    e.hook(0x9FF4D0, lambda emu: None)

    def ac(emu):                                       # node vt+0xAC(node, pbi+0x3C, mask, pbi+0x10C, [sp]=key, [sp+4]=ranges, [sp+8]=block, [sp+0xC]=1, [sp+0x10]=bus)
        sp = emu.reg_sp()
        e.log.append(('AC', emu.reg(0), emu.reg(1), emu.reg(2), emu.reg(3), e.r32(sp), e.r32(sp + 4), e.r32(sp + 8), e.r32(sp + 0xC), e.r32(sp + 0x10)))
        if e.cfg['ac']:
            e.cfg['ac'](emu)
        if e.cfg.get('ac_block_word4'):
            e.w32(e.r32(sp + 8) + 4, e.cfg['ac_block_word4'])     # the block word the first-time block tests (0x9FFF34)
        if e.cfg.get('ac_block_word0'):
            e.w32(e.r32(sp + 8), e.cfg['ac_block_word0'])
        return 0
    e.hook(STUB_AC, ac)
    e.w32(VT + 0xAC, STUB_AC)
    return e


def props_list(e, props):
    """[count][ids...] then the values at ((count+4)&~3)."""
    if not props:
        return 0
    ids = list(props)
    n = len(ids)
    base = e.alloc(4 + n + 4 * n + 8)
    e.w8(base, n)
    for i, k in enumerate(ids):
        e.w8(base + 1 + i, k)
    off = (n + 4) & ~3
    for i, k in enumerate(ids):
        e.w32(base + off + 4 * i, props[k])
    return base


def ranged_list(e, ranged):
    if not ranged:
        return 0
    ids = list(ranged)
    n = len(ids)
    base = e.alloc(4 + n + 8 * n + 8)
    e.w8(base, n)
    for i, k in enumerate(ids):
        e.w8(base + 1 + i, k)
    off = (n + 4) & ~3
    for i, k in enumerate(ids):
        e.w32(base + off + 8 * i, ranged[k][0])
        e.w32(base + off + 8 * i + 4, ranged[k][1])
    return base


def make_node(e, parent=0, bus=0, props=None, ranged=None, rtpc=(), pos=0, bits_=0, advanced=(0, 1, 0, 0, 0), node_id=1):
    n = e.alloc(0x100)
    e.w32(n, VT)
    e.w32(n + 8, node_id)
    e.w32(n + 0x34, parent)
    e.w32(n + 0x38, bus)
    e.w32(n + 0x3C, props_list(e, props or {}))
    e.w32(n + 0x4C, ranged_list(e, ranged or {}))
    if rtpc:
        mask = 0
        for r in rtpc:
            mask |= 1 << r
        m = e.alloc(8)
        e.w32(m, mask & 0xFFFFFFFF)
        e.w32(m + 4, mask >> 32)
        e.w32(n + 0x14, m)
    e.w32(n + 0x40, (bits_ & 1) | (0xFFE if pos & 1 else 0))
    e.w8(n + 0x45, 0x40 | ((bits_ >> 1 & 1) << 7))
    e.w8(n + 0x46, 0x21)
    e.w8(n + 0x47, 1 if (pos & 3) == 3 and (pos >> 2) & 1 else 0)
    e.w8(n + 0x58, ((advanced[1] & 7) << 3) | (advanced[1] & 7))
    e.w8(n + 0x59, advanced[4] & 0xF)
    e.w32(n + 0x2C, 0)
    return n


def make_pbi(e, node, e8=0x5D, e9=0x01):
    p = PBI
    e.uc.mem_write(p, bytes(0x240))
    e.w32(p + 0xC, 0x103B7DC)                          # ctx vtable
    e.w32(p + 0xE0, node)
    e.w8(p + 0xE8, e8)
    e.w8(p + 0xE9, e9)
    e.wf(p + 0x168, 1.0)
    e.wf(p + 0x16C, 1.0)
    e.w32(p + 0x1F8, 0xFFFFFFFF)
    return p


def make_params(e):
    e.uc.mem_write(PARAMS, bytes(0x150))
    return PARAMS + 0x8C                               # r1 of CalcEffectiveParams: params+0x8C; [r1+0x90] = params+0x11C = 0


def set_list(e, pbi, records):
    """pbi+0x10C data, +0x110 count; records are (word0, flags4, value8)."""
    base = e.alloc(12 * max(len(records), 1) + 12)
    for i, (w0, f4, v8) in enumerate(records):
        e.w32(base + 12 * i, w0)
        e.w8(base + 12 * i + 4, f4)
        e.wf(base + 12 * i + 8, v8)
    e.w32(pbi + 0x10C, base)
    e.w32(pbi + 0x110, len(records))


def get_list(e, pbi):
    base, n = e.r32(pbi + 0x10C), e.r32(pbi + 0x110)
    return [(e.r32(base + 12 * i), e.r8(base + 12 * i + 4), e.rf(base + 12 * i + 8)) for i in range(n)]


def dump(e, pbi):
    h = lambda o: hex(e.r32(pbi + o))
    f = lambda o: (e.rf(pbi + o), hex(e.r32(pbi + o)))
    d = {
        '3C': f(0x3C), '40': f(0x40), '44': f(0x44), '48': f(0x48), '4C': f(0x4C), '64': f(0x64), '98': f(0x98), '9C': f(0x9C), 'A4': f(0xA4),
        'B4': h(0xB4), 'B8': h(0xB8), 'BC': h(0xBC), 'C0': hex(e.r8(pbi + 0xC0)), 'C4': h(0xC4),
        'E4': h(0xE4), 'E8': hex(e.r8(pbi + 0xE8)), 'E9': hex(e.r8(pbi + 0xE9)), '1BC': hex(e.r8(pbi + 0x1BC)),
        '1CC': h(0x1CC), '1D0': h(0x1D0), '1C0': h(0x1C0), 'list': [(hex(a), b, hex(bits(c))) for a, b, c in get_list(e, pbi)],
    }
    return d


def show(label, e, pbi, r=None):
    print(label)
    if r is not None:
        print('   result', r)
    print('  ', dump(e, pbi))
    print('   log', [tuple(hex(x) if isinstance(x, int) else x for x in l) for l in e.log])


def call9beb30(e, pbi, priority, flag, params=None):
    ctx = pbi + 0xC
    e.w32(OUTCODE, 0xAAAA0001)
    e.w8(BELOW, 0xEE)
    r = e.call(0x9BEB30, ctx, bits(priority), 0, flag, stack=[OUTCODE, params if params is not None else 0, BELOW])
    return r, e.r32(OUTCODE), e.r8(BELOW)


def main():
    nan = struct.unpack('<f', struct.pack('<I', 0x7FC00000))[0]

    # ---- the node walks
    e = setup()
    bus = make_node(e, node_id=99)
    root = make_node(e, bus=bus, props={0xC: bits(0.5), 0xD: bits(-0.25), 0xE: bits(0.75)}, node_id=10)
    mid = make_node(e, parent=root, node_id=11)
    leaf = make_node(e, parent=mid, node_id=12)
    pbi = make_pbi(e, leaf)
    print('--- W1 0x9F4BB8 / 0x9BDA6C: the first output bus of a leaf under a root with a bus; and with E9 bit 2')
    print('   0x9F4BB8(leaf) == bus', e.call(0x9F4BB8, leaf) == bus, ' 0x9BDA6C(ctx) == bus', e.call(0x9BDA6C, pbi + 0xC) == bus)
    e.w8(pbi + 0xE9, 5)
    print('   0x9BDA6C with E9 bit2 ->', e.call(0x9BDA6C, pbi + 0xC))
    e.w8(pbi + 0xE9, 1)
    print('   0x9F4BB8(bus) ->', e.call(0x9F4BB8, bus))

    print('--- W2 0x9EEDA4 (code and index)')
    for label, adv, parent_adv in (('leaf byte4=0 byte1=1', (0, 1, 0, 0, 0), None), ('leaf byte4=3 byte1=5', (0, 5, 0, 0, 3), None), ('leaf byte4=3, parent is the flagged node (45 bit 4)', (0, 1, 0, 0, 3), 'p')):
        e = setup()
        root_n = make_node(e, advanced=(0, 2, 0, 0, 9))
        leaf_n = make_node(e, parent=root_n, advanced=adv)
        if parent_adv:
            e.w8(leaf_n + 0x45, 0x40)      # flag bit 4 clear on the leaf
            e.w8(root_n + 0x45, 0x50)      # bit 4 set on the root
        out = BASE + 0x60000
        code = e.call(0x9EEDA4, leaf_n, out)
        print('  ', label, 'code', code, 'index', e.r32(out))
    e = setup()
    mid_n = make_node(e, advanced=(0x10, 4, 0, 0, 7))
    e.w8(mid_n + 0x45, 0x50)
    leaf_n = make_node(e, parent=mid_n, advanced=(0, 1, 0, 0, 2))
    top_n = make_node(e, advanced=(0, 3, 0, 0, 0xC))
    e.w32(mid_n + 0x34, top_n)
    out = BASE + 0x60000
    print('   leaf -> mid (45 bit4 set) -> top: code', e.call(0x9EEDA4, leaf_n, out), 'index', e.r32(out))

    print('--- W3 0x9F1F80 (node vt+0x84) for nodes without a positioning object')
    e = setup()
    root_n = make_node(e)
    leaf_n = make_node(e, parent=root_n, pos=0xC0)
    out = BASE + 0x60000
    e.w32(out, 0x12345678)
    print('   result', e.call(0x9F1F80, leaf_n, out), 'out', hex(e.r32(out)))
    e.w32(out, 0x12345678)
    leaf2 = make_node(e, parent=root_n, pos=0xC1)
    print('   positioning bit0 on the leaf: result', e.call(0x9F1F80, leaf2, out), 'out', hex(e.r32(out)))

    print('--- W4 0x9F6B94 (priority): defaults, property 7, property 8 gated by [node+0x45] bit 7, the parent for a node without override bit 0')
    for label, kw in (('defaults', {}), ('prop7=12.5 only', {'props': {7: bits(12.5)}}), ('prop7=12.5 prop8=-3.0, bit1 (offset flag) set', {'props': {7: bits(12.5), 8: bits(-3.0)}, 'bits_': 3}),
                      ('prop8 only, offset flag set', {'props': {8: bits(-3.0)}, 'bits_': 2}), ('offset flag set, no props', {'bits_': 2})):
        e = setup()
        n = make_node(e, **kw)
        out = BASE + 0x60000
        e.call(0x9F6B94, out, n, 0x77)
        print('  ', label, 'priority', e.rf(out), hex(e.r32(out)), 'offset', e.rf(out + 4), hex(e.r32(out + 4)))
    e = setup()
    par = make_node(e, props={7: bits(77.0)})
    child = make_node(e, parent=par, props={7: bits(5.0)})
    child2 = make_node(e, parent=par, props={7: bits(5.0)}, bits_=1)
    out = BASE + 0x60000
    e.call(0x9F6B94, out, child, 0)
    print('   child without override takes the parent:', e.rf(out))
    e.call(0x9F6B94, out, child2, 0)
    print('   child with override (bit 0) keeps its own:', e.rf(out))

    # ---- 0x9BEB30 end to end
    def rig(e8=0x5D, e9=0x01, bus_present=True, props=None, pos=0xC0, ac=None, bus_flag=1, bus_vol=0.0, records=None, params=True, lpf=None):
        e = setup()
        e.cfg.update({'ac': ac, 'bus_flag': bus_flag, 'bus_vol': bus_vol})
        b = make_node(e, node_id=99) if bus_present else 0
        root_n = make_node(e, bus=b, props=props, pos=pos, node_id=10)
        leaf_n = make_node(e, parent=root_n, pos=pos, node_id=11)
        pbi = make_pbi(e, leaf_n, e8, e9)
        if records is not None:
            set_list(e, pbi, records)
        else:
            set_list(e, pbi, [])
        p8c = make_params(e) if params else 0
        return e, pbi, p8c, leaf_n, root_n

    def ac_fill(vol=0.0, pitch=0.0, lpf=0.0, hpf=0.0):
        def f(emu):
            io = emu.reg(1)
            e_ = ac_fill.e
            e_.wf(io + 0, vol)           # pbi+0x3C
            e_.wf(io + 8, pitch)         # pbi+0x44
            e_.wf(io + 0xC, lpf)         # pbi+0x48
            e_.wf(io + 0x10, hpf)        # pbi+0x4C
        return f

    print('--- B1 0x9BEB30 on the shipped path: node chain with a bus, E8 = 0x5D, E9 = 1; vt+0xAC writes volume -6, pitch 100, lpf 10, hpf 20; flag 1 and flag 0')
    for flag in (1, 0):
        e, pbi, p8c, leaf_n, root_n = rig(ac=None)
        ac_fill.e = e
        e.cfg['ac'] = ac_fill(-6.0, 100.0, 10.0, 20.0)
        e.wf(pbi + 0xA0, 1.5)
        e.wf(pbi + 0xA8, 2.5)
        e.wf(pbi + 0x118, 0.125)
        e.wf(pbi + 0x120, 7.0)
        e.wf(pbi + 0x124, 0.5)
        e.wf(pbi + 0x128, 0.25)
        r = call9beb30(e, pbi, 50.0, flag, p8c)
        show(f'  B1 flag={flag}', e, pbi, r)

    print('--- B2 below audibility: volume -100 dB (lin 0): flag 0 -> returns 3 with code 0x29; flag 1 -> 1; with E9 bit 2 -> 1')
    for flag, e9 in ((0, 1), (1, 1), (0, 5)):
        e, pbi, p8c, leaf_n, root_n = rig(e9=e9)
        ac_fill.e = e
        e.cfg['ac'] = ac_fill(-100.0, 0.0, 0.0, 0.0)
        r = call9beb30(e, pbi, 50.0, flag, p8c)
        print(f'  flag={flag} E9={e9:#x}: result {r[0]} code {r[1]:#x} below {r[2]} E8 {e.r8(pbi + 0xE8):#x} E9 {e.r8(pbi + 0xE9):#x} 64 {e.rf(pbi + 0x64)} 3C {e.rf(pbi + 0x3C)} 40 {e.rf(pbi + 0x40)}')

    print('--- B3 E9 bit 2 (the action is a bus play): the BA8 path zeroes the pan values and CalcEffectiveParams ends at 0x9FFC30')
    e, pbi, p8c, leaf_n, root_n = rig(e9=0x05)
    e.wf(pbi + 0xB4, 9.0); e.wf(pbi + 0xB8, 9.0); e.wf(pbi + 0xBC, 9.0); e.w8(pbi + 0xC0, 9)
    r = call9beb30(e, pbi, 33.0, 1, p8c)
    show('  B3', e, pbi, r)

    print('--- B4 E8 bit 5 already set: no CalcEffectiveParams; E9 bit 0 set runs 0x9FF368 over the records (1.0, 2.0, NaN-free) and 0x98 + 0x118')
    for e9 in (0x01, 0x00):
        e, pbi, p8c, leaf_n, root_n = rig(e8=0x7D, e9=e9, records=[(1, 2, 0.5), (2, 2, 4.0)])
        e.wf(pbi + 0x98, 3.0)
        e.wf(pbi + 0x118, 0.25)
        e.wf(pbi + 0x168, 0.5)
        e.wf(pbi + 0x16C, 2.0)
        r = call9beb30(e, pbi, 20.0, 1, p8c)
        show(f'  B4 E9={e9:#x}', e, pbi, r)

    print('--- B5 bus present: 0x9C54E8 returns 0 -> 0x9C39DC(bus, 0, 5) is added to pbi+0x64; returns 1 -> nothing is added')
    for flag_ret, vol in ((0, -3.5), (1, -3.5)):
        e, pbi, p8c, leaf_n, root_n = rig(bus_flag=flag_ret, bus_vol=vol)
        ac_fill.e = e
        e.cfg['ac'] = ac_fill(0.0, 0.0, 0.0, 0.0)
        r = call9beb30(e, pbi, 20.0, 1, p8c)
        show(f'  B5 0x9C54E8 -> {flag_ret}', e, pbi, r)

    print('--- B6 no output bus (r6 == 0) with E9 bit 2 clear: [params+0x11C] == 0 == r6 selects the cached path (unread); with a non-zero [params+0x11C] the normal path runs')
    e, pbi, p8c, leaf_n, root_n = rig(bus_present=False)
    e.cfg['ac'] = ac_fill(0, 0, 0, 0)
    ac_fill.e = e
    try:
        r = call9beb30(e, pbi, 20.0, 1, p8c)
        show('  B6 (the cached path ran)', e, pbi, r)
    except Exception as ex:
        print('   fault', type(ex).__name__)
    e, pbi, p8c, leaf_n, root_n = rig(bus_present=False)
    e.cfg['ac'] = ac_fill(0, 0, 0, 0)
    ac_fill.e = e
    e.w32(PARAMS + 0x11C, 0x1234)
    r = call9beb30(e, pbi, 20.0, 1, p8c)
    show('  B6b [params+0x11C] != 0', e, pbi, r)

    print('--- B7 the pan values come from the top node (positioning bit 0 on the root): props 0xC, 0xD, 0xE; [node+0x47] bit 0 from positioning byte 7')
    for pos in (0xC0, 0xC1, 0xC3, 0xC7):
        e, pbi, p8c, leaf_n, root_n = rig(props={0xC: bits(0.5), 0xD: bits(-0.25), 0xE: bits(0.75)}, pos=pos)
        e.w8(leaf_n + 0x45, 0x40)
        # leaf has pos bit0 only when pos & 1; make the leaf carry no positioning of its own
        e.w32(leaf_n + 0x40, 0)
        e.w8(leaf_n + 0x47, 0)
        e.cfg['ac'] = ac_fill(0, 0, 0, 0)
        ac_fill.e = e
        r = call9beb30(e, pbi, 20.0, 1, p8c)
        print(f'  pos {pos:#x} (root): B4 {e.r32(pbi + 0xB4):#x} B8 {e.r32(pbi + 0xB8):#x} BC {e.r32(pbi + 0xBC):#x} C0 {e.r8(pbi + 0xC0):#x} E8 {e.r8(pbi + 0xE8):#x}')

    print('--- B8 RTPC: node with parameter 0x17 / 0x18 / 0x12 is routed through 0xA11590 (not modelled: recorded)')
    for rtpc in ((0x17,), (0x18,), (0x12,), (0x13,)):
        e = setup()
        b = make_node(e, node_id=99)
        root_n = make_node(e, bus=b, props={0xC: bits(1.0)}, rtpc=rtpc, node_id=10)
        pbi = make_pbi(e, root_n)
        set_list(e, pbi, [])
        p8c = make_params(e)
        e.cfg['ac'] = ac_fill(0, 0, 0, 0)
        ac_fill.e = e
        e.cfg['rtpc'] = 3.0
        r = call9beb30(e, pbi, 20.0, 1, p8c)
        print(f'  rtpc {rtpc}: result {r[0]} E8 {e.r8(pbi + 0xE8):#x} B4 {e.r32(pbi + 0xB4):#x} B8 {e.r32(pbi + 0xB8):#x} BC {e.r32(pbi + 0xBC):#x} C0 {e.r8(pbi + 0xC0):#x} log {[l[0] + ":" + hex(l[1]) for l in e.log if l[0] == "A11590"]}')

    print('--- B9 CalcEffectiveParams alone (via ctx vt+0x24), r1 = 0 (AddSrc): reset stage, prune, compose')
    for label, recs in (('records keep/drop', [(1, 0, 9.0), (2, 2, 0.5), (3, 0, 7.0), (4, 2, 4.0)]), ('one record dropped', [(1, 0, 9.0)]), ('two kept', [(1, 2, 3.0), (2, 2, 5.0)])):
        e, pbi, p8c, leaf_n, root_n = rig(records=recs)
        for off, v in ((0x3C, 9.0), (0x44, 9.0), (0x48, 9.0), (0x4C, 9.0), (0x50, 9.0), (0x54, 9.0), (0x5C, 9.0), (0x64, 9.0), (0x68, 9.0), (0x6C, 9.0)):
            e.wf(pbi + off, v)
        e.w8(pbi + 0x58, 0xFF); e.w8(pbi + 0x60, 0xFF)
        e.uc.mem_write(pbi + 0x70, b'\xFF' * 32)
        e.w32(pbi + 0x90, 0xFFFFFFFF); e.uc.mem_write(pbi + 0x94, b'\xFF' * 4)
        e.cfg['ac'] = ac_fill(-3.0, 50.0, 4.0, 5.0)
        ac_fill.e = e
        e.call(0xA000E0 if False else 0x9FFAD4, pbi, 0)
        print('  ', label, dump(e, pbi), 'x58', hex(e.r8(pbi + 0x58)), 'x60', hex(e.r8(pbi + 0x60)), 'x70', bytes(e.uc.mem_read(pbi + 0x70, 32)).hex(), 'x90', hex(e.r32(pbi + 0x90)), 'x94', bytes(e.uc.mem_read(pbi + 0x94, 4)).hex(),
              'log', [l[0] for l in e.log])

    print('--- B10 the priority block: new priority 50.0f (default); [pbi+0x1CC] / [0x1D0] / [0x1C0] set to compare equal / different; two limiter lists in the array')
    for label, c1cc, c1d0, c1c0 in (('all equal to (50, 0)', 50.0, 0.0, 50.0), ('1CC differs', 7.0, 0.0, 50.0), ('1D0 differs only', 50.0, 3.0, 50.0), ('1C0 differs', 50.0, 3.0, 20.0), ('NaN 1CC', nan, 0.0, 50.0)):
        e, pbi, p8c, leaf_n, root_n = rig()
        e.cfg['ac'] = ac_fill(0, 0, 0, 0)
        ac_fill.e = e
        e.wf(pbi + 0x1CC, c1cc); e.wf(pbi + 0x1D0, c1d0); e.wf(pbi + 0x1C0, c1c0)
        arr = e.alloc(16)
        e.w32(arr, 0xA1A1A1A1); e.w32(arr + 4, 0xB2B2B2B2)
        e.w32(pbi + 0x1EC, arr); e.w32(pbi + 0x1F0, 2); e.w32(pbi + 0x1F4, 2)
        call9beb30(e, pbi, 20.0, 1, p8c)
        print(f'  {label}: 1CC {e.r32(pbi + 0x1CC):#x} 1D0 {e.r32(pbi + 0x1D0):#x} 1C0 {e.r32(pbi + 0x1C0):#x} log {[tuple(hex(x) if isinstance(x, int) else x for x in l) for l in e.log if l[0] == "9F36A0"]}')

    print('--- B11 the first-time block (E8 bit 6 clear): 0x9E8224 when [pbi+0x34] != 0; 0x9E62AC when the block word is set; E8 |= 0x40')
    for f34, blkword in ((0, 0), (0x1111, 0), (0x1111, 1), (0, 1)):
        e, pbi, p8c, leaf_n, root_n = rig(e8=0x1D)
        e.cfg['ac'] = ac_fill(0, 0, 0, 0)
        ac_fill.e = e
        e.w32(pbi + 0x34, f34)
        e.cfg['ac_block_word4'] = blkword
        call9beb30(e, pbi, 20.0, 1, p8c)
        print(f'  [pbi+0x34]={f34:#x} word the vt+0xAC stand-in stores at block+4={blkword}: E8 {e.r8(pbi + 0xE8):#x} log {[l[0] for l in e.log if l[0] != "AC"]}  ac block arg {[hex(l[7]) for l in e.log if l[0] == "AC"]} (the local block is on the stack, not params+0x108 = {p8c + 0x7C:#x})')

    print('--- B12 0x9BCA68 (AddSrc): E8 bit 5 clear -> CalcEffectiveParams(r1 = 0) then below; bit 5 set + E9 bit 0 -> 0x9FF368; else below only')
    for e8, e9, vol in ((0x5D, 1, -100.0), (0x7D, 1, -100.0), (0x7D, 0, -100.0), (0x7D, 0, 0.0), (0x7D, 1, 0.0)):
        e, pbi, p8c, leaf_n, root_n = rig(e8=e8, e9=e9, records=[(1, 2, 0.5)])
        e.cfg['ac'] = ac_fill(vol, 0, 0, 0)
        ac_fill.e = e
        e.wf(pbi + 0x98, vol)
        e.wf(pbi + 0x3C, vol); e.wf(pbi + 0x40, 1.0)
        r = e.call(0x9BCA68, pbi + 0xC, 0)
        print(f'  E8={e8:#x} E9={e9:#x} vol {vol}: result {r} 3C {e.rf(pbi + 0x3C)} 40 {e.rf(pbi + 0x40)} 64 {e.rf(pbi + 0x64)} E8 {e.r8(pbi + 0xE8):#x} E9 {e.r8(pbi + 0xE9):#x} ac calls {len([l for l in e.log if l[0] == "AC"])}')
    print('   the below test over (3C, 40, 64): products around 2^-16 (0x37800000)')
    for v3c, f40, v64 in ((-96.0, 1.0, 0.0), (-79.5, 1.0, 0.0), (-80.0, 1.0, 0.0), (0.0, 0.0, 0.0), (0.0, 1.0, -100.0), (nan, 1.0, 0.0), (0.0, nan, 0.0), (-37.0 / 0.05, 1.0, 0.0), (-740.1, 1.0, 0.0), (-739.9, 1.0, 0.0), (0.0, 1.0, -739.9), (-30.0, 0.5, -30.0), (-20.0, 0.00001, -20.0)):
        e, pbi, p8c, leaf_n, root_n = rig(e8=0x7D, e9=0)
        e.wf(pbi + 0x3C, v3c); e.wf(pbi + 0x40, f40); e.wf(pbi + 0x64, v64)
        r = e.call(0x9BCA68, pbi + 0xC, 0)
        print(f'  3C {v3c!r} 40 {f40!r} 64 {v64!r}: {r}')

    print('--- L1 0xA1E280 loop count')
    for label, props, ranged, seed in (('none', {}, {}, (0, 0)), ('prop 0x3A = 0', {0x3A: 0}, {}, (0, 0)), ('prop 0x3A = 2', {0x3A: 2}, {}, (0, 0)), ('range 3..3 (no draw)', {}, {0x3A: (3, 3)}, (0, 0)),
                                       ('base 1 range 2..10, seed 0', {0x3A: 1}, {0x3A: (2, 10)}, (0, 0)), ('base 1 range 2..10, seed 12345', {0x3A: 1}, {0x3A: (2, 10)}, (12345, 0)), ('range -5..5', {}, {0x3A: (0xFFFFFFFB, 5)}, (0x89ABCDEF, 0x01234567)),
                                       ('base 0x7FFF + range to overflow s16', {0x3A: 0x7FFF}, {0x3A: (1, 3)}, (7, 7)), ('base 0xFFFFFFFF', {0x3A: 0xFFFFFFFF}, {}, (0, 0))):
        e = setup()
        n = make_node(e, props=props, ranged=ranged)
        e.w32(0x108D868, seed[0]); e.w32(0x108D86C, seed[1])
        r = e.call(0xA1E280, n)
        print(f'  {label}: result {r & 0xFFFF:#x} (sxth {struct.unpack("<h", struct.pack("<H", r & 0xFFFF))[0]}), lcg {e.r32(0x108D86C):#x}:{e.r32(0x108D868):#x}')

    print('--- P9 0xA00618: [pbi+0x1B8] stored every call; 1BD bit 0 set once; [pbi+0xAC] != 0 reaches 0x9FF0D8 (stubbed)')
    e = setup()
    e.hook(0x9FF0D8, lambda emu: e.log.append(('9FF0D8', emu.reg(1), emu.reg(2))))
    n = make_node(e, props={0x3A: 5})
    pbi = make_pbi(e, n)
    e.w8(pbi + 0x1BD, 0x44)
    e.call(0xA00618, pbi)
    print('   first call: 1B8', hex(e.r16(pbi + 0x1B8)), '1BD', hex(e.r8(pbi + 0x1BD)), e.log)
    e.w32(n + 0x3C, props_list(e, {0x3A: 9}))
    e.call(0xA00618, pbi)
    print('   second call: 1B8', hex(e.r16(pbi + 0x1B8)), '1BD', hex(e.r8(pbi + 0x1BD)), e.log)
    e.w8(pbi + 0x1BD, 0x44)
    e.w32(pbi + 0xAC, 0x9999)
    e.call(0xA00618, pbi)
    print('   with [pbi+0xAC] set: 1BD', hex(e.r8(pbi + 0x1BD)), e.log)

    print('--- T1 0xA358EC (the transition factor): item flag byte bit 2, time, frames')
    e = setup()
    item = e.alloc(0x40)
    for flags, t, frames in ((0x04, 500, 0x400), (0x04, 0, 0x400), (0x00, 500, 0x400), (0x04, 1, 1), (0x04, 100, 0x400), (0x04, 1500, 0x400), (0x04, -500 & 0xFFFFFFFF, 0x400), (0x04, 500, 0), (0x04, 500, 0xFFFF)):
        e.w8(item + 0x34, flags)
        e.w32(item + 0x38, 0x11111111)
        e.uc.mem_write(0x1052440, struct.pack('<H', frames))
        # expf is the phone's libm: stand-in with the correctly rounded float32 result
        import numpy as np
        e.log.clear()
        e.hook(0x4D0058, lambda emu: e.log.append(('expf', emu.reg(0))) or bits(float(np.exp(np.float32(f32(emu.reg(0)))))))
        e.call(0xA358EC, item, t)
        print(f'  flags {flags:#x} t {t:#x} frames {frames:#x}: expf argument bits {[hex(l[1]) for l in e.log]} ({[f32(l[1]) for l in e.log]}), [0x38] = {e.r32(item + 0x38):#x} ({f32(e.r32(item + 0x38))})')

    print('--- T2 0xA366AC / 0xA366D0 state toggles')
    e = setup()
    item = e.alloc(0x40)
    for start in range(0, 6):
        row = []
        for fn in (0xA366AC, 0xA366D0):
            e.w32(item + 0x30, start)
            e.call(fn, 0, item)
            row.append(e.r32(item + 0x30))
        print(f'  state {start}: 0xA366AC -> {row[0]}, 0xA366D0 -> {row[1]}')

    print('--- T3 0xA36268 (create): allocation, ctor, 0xA35D44 stand-in, vector append and growth, start flag, failure exit')
    for label, init_ret, start, sel, alloc_fail, count0, cap0 in (('ok, start, list A', 1, 1, 0, None, 0, 4), ('ok, no start, list B', 1, 0, 1, None, 0, 4), ('init returns 2', 2, 1, 0, None, 0, 4), ('item alloc fails', 1, 1, 0, 1, 0, 4),
                                                                  ('vector full: grows by 0x80', 1, 1, 0, None, 4, 4), ('vector full: growth alloc fails', 1, 1, 0, 2, 4, 4), ('count 3 of 4', 1, 1, 0, None, 3, 4)):
        e = setup()
        mgr = e.alloc(0x80)
        data = e.alloc(4 * 0x100)
        lst = mgr + (0xC if sel == 1 else 0)
        e.w32(lst, data); e.w32(lst + 4, count0); e.w32(lst + 8, cap0)
        e.w32(mgr + 0x4C, 0x5555)
        # [GOT 0x609e1c-based] -> pointer to the manager: the instruction at 0xA362A0 loads [pc + 0x609e1c] with pc = 0xA362AC
        slot = 0xA362B4 + 0x609E1C
        mgrvar = e.alloc(8)
        e.w32(mgrvar, mgr)
        e.set_got(slot, mgrvar)
        e.alloc_calls = 0
        e.fail_alloc_at = alloc_fail
        calls = []
        e.hook(0xA35D44, lambda emu: calls.append(('A35D44', emu.reg(1), emu.reg(2))) or init_ret)
        e.hook(0xA3587C, lambda emu: calls.append(('A3587C',)))
        info = e.alloc(0x40)
        target = e.alloc(0x20)
        vt = e.alloc(0x20)
        e.w32(target, vt)
        e.w32(vt, 0x7000)                       # vt+0 of the target
        e.hook(0x7000, lambda emu: calls.append(('target vt+0', emu.reg(1), emu.reg(2), emu.reg(3))))
        e.w32(info, target); e.w32(info + 4, 0x1000000); e.wf(info + 8, 0.0); e.wf(info + 0xC, 1.0)
        r = e.call(0xA36268, mgr, info, start, sel)
        created = r != 0
        item_state = e.r32(r + 0x30) if created else None
        item_w0 = e.r32(r + 0) if created else None
        print(f'  {label}: result {"item" if created else 0}, state {item_state}, word0 {hex(item_w0) if item_w0 else None}, count {e.r32(lst + 4)} cap {e.r32(lst + 8)}, last entry == item {e.r32(e.r32(lst) + 4 * (e.r32(lst + 4) - 1)) == r if created else None}, calls {calls}, alloc calls {e.alloc_calls}')


if __name__ == '__main__':
    main()
