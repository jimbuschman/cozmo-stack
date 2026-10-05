"""Run the engine's own HIRC walker and node/bus loaders under Unicorn, for WwiseRuntimeGraphTests (M6-001 / M6-009 / M6-025, B-M6b-4 batch 6c, C44.1).

The REAL engine code runs over the shipped banks (HIRC and BKHD chunks only; DIDX/DATA do not touch the graph): the chunk loop 0x9B74D8 and the HIRC walker 0x9B3260 with every handler it dispatches to (Sound 0x9B3DF4 and
0xA1D814 / 0xA1DA08 / 0x9B9C90 / 0xA1EA68 / 0xA1EB58, ActorMixer 0x9B3B00 / 0xA66950 / 0xA669EC / 0x981658 / 0x981940, Bus 0x9B2CE8 / 0x9C3620 / 0x9C3FFC / 0x9C6420 / 0x9C0D08 / 0x9C3E94 / 0x9C581C / 0x9C181C, Event 0x9B3100 /
0x9CC96C / 0x9CD01C, Action 0x9B2F64 / 0xA60C1C / 0xA613B0 / 0xA62984, FxShareSet, RanSeq, Switch, Layer, State, LFO ...), the NodeBase 0x9F6EF8 with its readers 0x9ECAD8, 0x9ED51C, 0x9ECF44, 0x9ED84C, 0x9ED730 and the FX
setters 0x9F5760 / 0x9F5B24 / 0x9F5C30, the registry 0x9F40F4 / 0x9A7EB0, the RTPC subscription 0xA1A338 / 0xA1A160, the base constructor 0x9F402C and the bus callbacks (vt+0xC4 = 0x9C3D04 -> 0x9C39DC ...). Python stand-ins
(bodies the inventory does not read, or host memory): the pool allocator 0xA7A7F4 (it fills every block it hands out with the pass's pool byte, so the engine's uninitialised bits are observable), the state manager
0xA27B20 / 0xA28198 / 0x9F24C8, the subscription table 0xA11F98 (it records its arguments), the bus list callbacks 0xA40E6C / 0xA4131C / 0xA40FF8 / 0xA4454C / 0xA443E0 (they return: the global lists they read are empty), the
signed 64-bit divide, and the host globals ([0x105243C] rate, [0x1052440] floor, the RTPC manager pointer).

The oracle is the dump of the engine's registry after each pass: one text row per object (a routing node of table A or B, an Event, an Action) with the fields C44.1 names. Rows for the shipped chain (Event 188399711, Action
859129412, Sound 957475640, ActorMixer 13023553, root 62050212, the 15 buses) are emitted in full; every other row is emitted as a hash of the same text (FNV-1a 32). The expected values in WwiseRuntimeGraphTests are this
script's output, not the C#'s:

    python re-analysis/tools/emu/emu_graph.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseRuntimeGraphOracle.cs

(--text prints the pass rows in a readable form).
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unicorn.arm_const import UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2, UC_ARM_REG_R3  # noqa: E402
from emu_bankload import World, BM, DATA_AT, OUTBANK  # noqa: E402

META = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'obb', 'sound_meta')
BANKS = ['Init.bnk', os.path.join('English(US)', 'Cozmo.bnk'), 'SFX.bnk', 'UI.bnk', 'Dev_Debug.bnk', 'Music.bnk']
REG_PTR = 0x108D8E0            # [GOT 0x104006C]: the variable that holds the registry REG
MGR_PTR = 0x108D908            # the RTPC manager pointer
KEY = {                        # the shipped chain whose rows are emitted in full
    ('E', 188399711), ('X', 859129412), (2, 957475640), (7, 13023553), (7, 62050212),
}
TABLE_OFF = {'A': 0x00, 'B': 0x14, 'EVENT': 0x3C, 'ACTION': 0x50}


def fnv(text):
    h = 0x811C9DC5
    for b in text.encode('ascii'):
        h = ((h ^ b) * 0x01000193) & 0xFFFFFFFF
    return h


def hirc_only(path):
    """BKHD + HIRC of a bank file; also returns the (type, id) of every HIRC object in file order."""
    d = open(path, 'rb').read()
    off = 0
    out = b''
    objs = []
    while off + 8 <= len(d):
        t = d[off:off + 4]
        sz = struct.unpack_from('<I', d, off + 4)[0]
        if t in (b'BKHD', b'HIRC'):
            out += d[off:off + 8 + sz]
        if t == b'HIRC':
            n = struct.unpack_from('<I', d, off + 8)[0]
            p = off + 12
            for _ in range(n):
                typ = d[p]
                osz = struct.unpack_from('<I', d, p + 1)[0]
                objs.append((typ, struct.unpack_from('<I', d, p + 5)[0]))
                p += 5 + osz
        off += 8 + sz
    return out, objs


class Pass:
    """One emulator life: the registry REG, the host globals and the banks loaded in order. files: [(name, bank bytes holding BKHD and HIRC)]; types: {object id: HIRC type} for the row labels."""

    def __init__(self, name, files, types, fill, rate, floor, mgr=True):
        self.name, self.fill, self.rate, self.floor = name, fill, rate, floor
        self.log = []
        self.types = types
        w = World(dict(file=files[0][1], type=3, mode=0, id=0x1000))
        self.w = w
        e = w.e
        self.e = e
        fill_byte = bytes([fill])

        def alloc(emu):                 # 0xA7A7F4(pool, size): the engine's pool block, pre-filled with the pass's byte
            size = emu.reg(1)
            if size == 0:
                return 0
            a = e.alloc(size)
            e.uc.mem_write(a, fill_byte * ((size + 15) & ~15))
            return a
        e.hook(0xA7A7F4, alloc)
        reg = e.alloc(0x200)
        e.uc.mem_write(reg, bytes(0x200))
        e.w32(REG_PTR, reg)
        self.reg = reg
        if mgr:
            m = e.alloc(0x100)
            e.uc.mem_write(m, bytes(0x100))
            e.w32(MGR_PTR, m)
        else:
            e.w32(MGR_PTR, 0)
        e.w32(0x105243C, rate)
        e.w16(0x1052440, floor)

        def ldivmod(emu):               # __aeabi_ldivmod (the PLT stub 0x4A685C)
            n = (emu.reg(1) << 32) | emu.reg(0)
            d = (emu.reg(3) << 32) | emu.reg(2)
            if n >= 1 << 63:
                n -= 1 << 64
            if d >= 1 << 63:
                d -= 1 << 64
            q = abs(n) // abs(d)
            q = q if (n < 0) == (d < 0) else -q
            r = n - q * d
            q &= (1 << 64) - 1
            r &= (1 << 64) - 1
            emu.uc.reg_write(UC_ARM_REG_R1, q >> 32)
            emu.uc.reg_write(UC_ARM_REG_R2, r & 0xFFFFFFFF)
            emu.uc.reg_write(UC_ARM_REG_R3, r >> 32)
            return q & 0xFFFFFFFF
        e.hook(0x4A685C, ldivmod)
        e.hook(0xA27B20, lambda emu: 1)          # the state manager registration (not read)
        e.hook(0xA28198, lambda emu: 0)          # the current state (not read)
        e.hook(0x9F24C8, lambda emu: 1)          # the state / instance pair of a group item (not read)
        # the subscription table 0xA11F98 -> 0xA117C8 (L5-03/04): the engine's code goes on into the RTPC value store (0xA0F07C, 0x9E6EDC ...), which the inventory does not read; this stand-in records the call
        e.hook(0xA11F98, lambda emu: self.log.append(('rtpc', emu.reg(2), emu.reg(3))) or 1)
        e.hook(0xA2BCC8, lambda emu: 1)          # the Switch container's switch-group registration with the switch manager (not read)
        for a in (0xA40E6C, 0xA4131C, 0xA40FF8, 0xA4454C, 0xA443E0):
            e.hook(a, lambda emu: None)
        self.results = []
        for bi, (bname, data) in enumerate(files):
            self.results.append((bname, self.load_bank(data, 0x1000 + bi)))

    def load_bank(self, data, bank_id):
        e = self.e
        e.uc.mem_write(DATA_AT, data)

        def open_reader(emu):
            R = BM + 4
            e.w32(R + 0x18, DATA_AT)
            e.w32(R + 8, len(data))
            e.w32(R + 0x1C, 0)
            return 1
        e.hook(0x9BC17C, open_reader)
        e.hook(0x9BC348, open_reader)
        s = [0] * 13
        s[2] = 0x10
        s[3] = 3
        s[4] = 0xFFFFFFFF
        s[5] = DATA_AT
        s[6] = len(data)
        s[9] = OUTBANK
        s[10] = 0
        e.w32(OUTBANK, 0xDEADBEEF)
        return e.call(0x9B74D8, BM, 0, bank_id, 0, stack=s)

    # -- dumps
    def table(self, which):
        e = self.e
        base = self.reg + TABLE_OFF[which]
        nb = e.r32(base + 8)
        bk = e.r32(base + 4)
        out = []
        for i in range(nb):
            p = e.r32(bk + 4 * i)
            while p:
                out.append(p)
                p = e.r32(p + 4)
        return out

    def bundle(self, p, stride):
        e = self.e
        if not p:
            return '-'
        n = e.r8(p)
        ids = [e.r8(p + 1 + i) for i in range(n)]
        off = (n + 4) & ~3
        ents = []
        for i in range(n):
            a = p + off + stride * i
            if stride == 8:
                ents.append('%02X=%08X/%08X' % (ids[i], e.r32(a), e.r32(a + 4)))
            else:
                ents.append('%02X=%08X' % (ids[i], e.r32(a)))
        return '[' + ','.join(ents) + ']'

    def regstr(self, a):
        e = self.e
        if not a:
            return '-'
        return '%08X%08X/%08X%08X/%d' % (e.r32(a + 4), e.r32(a), e.r32(a + 0xC), e.r32(a + 8), e.r8(a + 0x1C))

    def fxstr(self, c):
        e = self.e
        if not c:
            return '-'
        return 'v%d i%s r%s s%s y%02X' % (e.r32(c), ','.join('%08X' % e.r32(c + 4 + 8 * s) for s in range(4)), ','.join('%02X' % e.r8(c + 8 + 8 * s) for s in range(4)),
                                        ','.join('%02X' % e.r8(c + 9 + 8 * s) for s in range(4)), e.r8(c + 0x24))

    def ptr_id(self, p):
        return '%d' % self.e.r32(p + 8) if p else '-'

    def node_row(self, p, typ):
        e = self.e
        ident = e.r32(p + 8)
        # a bus keeps arrays at +0x48 / +0x58 where a node keeps a ranged bundle, the aux block and the pooled word 58, and its holder 3 registry at +0xC8
        bus = typ == 8
        row = '%d %d r%d p%s o%s x%08X d%08X s%s b%s g%s a%s f%s h%s j%s k%s' % (
            typ, ident, e.r32(p + 0xC), self.ptr_id(e.r32(p + 0x34)), self.ptr_id(e.r32(p + 0x38)), e.r32(p + 0x40), e.r32(p + 0x44), '-' if bus else '%04X' % e.r16(p + 0x58),
            self.bundle(e.r32(p + 0x3C), 4), '-' if bus else self.bundle(e.r32(p + 0x4C), 8),
            '-' if bus or not e.r32(p + 0x54) else ','.join('%08X' % e.r32(e.r32(p + 0x54) + 4 * i) for i in range(4)),
            self.fxstr(e.r32(p + 0x28)), self.regstr(e.r32(p + 0x14)), '-' if bus else self.regstr(e.r32(p + 0x20)), self.regstr(e.r32(p + 0xC8)) if bus else '-')
        if typ == 2:
            row += ' S%08X,%08X,%08X,%08X,%08X,%08X' % tuple(e.r32(p + o) for o in (0x5C, 0x60, 0x64, 0x68, 0x6C, 0x70))
        elif typ == 7:
            n = e.r32(p + 0x60)
            arr = e.r32(p + 0x5C)
            row += ' C%d/%d:%s' % (n, e.r32(p + 0x64), ','.join(self.ptr_id(e.r32(arr + 4 * i)) for i in range(n)))
        elif typ == 8:
            n48, a48 = e.r32(p + 0x4C), e.r32(p + 0x48)
            n58, a58 = e.r32(p + 0x5C), e.r32(p + 0x58)
            ducks = []
            d = e.r32(p + 0x70)
            while d:
                ducks.append('%d:%08X:%08X:%08X:%08X:%08X' % (e.r32(d + 4), e.r32(d + 8), e.r32(d + 0xC), e.r32(d + 0x10), e.r32(d + 0x14), e.r32(d + 0x18)))
                d = e.r32(d)
            row += ' U%08X,%08X,%d,%02X B%s/%s D%s' % (e.r32(p + 0x68), e.r32(p + 0x6C), e.r32(p + 0x64), e.r8(p + 0xCC),
                                                      ','.join(self.ptr_id(e.r32(a48 + 4 * i)) for i in range(n48)) or '-', ','.join(self.ptr_id(e.r32(a58 + 4 * i)) for i in range(n58)) or '-',
                                                      ';'.join(ducks) or '-')
        return row

    def event_row(self, p):
        e = self.e
        chain = []
        a = e.r32(p + 0x10)
        while a:
            chain.append('%d' % e.r32(a + 8))
            a = e.r32(a + 0x10)
        return '4 %d r%d A%s' % (e.r32(p + 8), e.r32(p + 0xC), ','.join(chain) or '-')

    def action_row(self, p):
        e = self.e
        # the fade-curve byte and the bank id are the Play class's; the other classes' init (unread) writes them its own way
        play = (e.r16(p + 0x20) >> 8) == 4
        return '3 %d r%d t%04X f%s k%s c%08X b%s g%s' % (e.r32(p + 8), e.r32(p + 0xC), e.r16(p + 0x20), '%02X' % e.r8(p + 0x22) if play else '-', '%08X' % e.r32(p + 0x24) if play else '-',
                                                        e.r32(p + 0x1C), self.bundle(e.r32(p + 0x14), 4), self.bundle(e.r32(p + 0x18), 8))

    def master_row(self):
        """The global slots 0x108D9B0 {+4 master, +8, +0x10 second, +0x14} after the loads."""
        e = self.e
        g = 0x108D9B0
        return 'master=%s f8=%08X second=%s f14=%08X' % (self.ptr_id(e.r32(g + 4)), e.r32(g + 8), self.ptr_id(e.r32(g + 0x10)), e.r32(g + 0x14))

    def rows(self):
        """(sort key, kind, id, text) for every object of the four tables; the text starts with the HIRC type."""
        out = []
        for p in self.table('A'):
            i = self.e.r32(p + 8)
            out.append((0, i, self.node_row(p, self.types.get(i, 2))))
        for p in self.table('B'):
            out.append((1, self.e.r32(p + 8), self.node_row(p, 8)))
        for p in self.table('EVENT'):
            out.append((2, self.e.r32(p + 8), self.event_row(p)))
        for p in self.table('ACTION'):
            out.append((3, self.e.r32(p + 8), self.action_row(p)))
        out.sort(key=lambda r: (r[0], r[1]))
        return out


PASSES = [
    ('full', BANKS, 0x00, 48000, 0),
    ('pool', BANKS, 0xFF, 48000, 0),
    ('init44100', ['Init.bnk'], 0x00, 44100, 50000),
    ('init22050', ['Init.bnk'], 0xFF, 22050, 0),
]


def shipped_pass(name, banks, fill, rate, floor):
    files = []
    types = {}
    for b in banks:
        data, objs = hirc_only(os.path.join(META, b))
        files.append((b, data))
        for t, i in objs:
            types.setdefault(i, t)
    return Pass(name, files, types, fill, rate, floor)


# ---------------------------------------------------------------------------------------------------------------------------------- synthetic banks (the engine decides what they do)

def u32(x):
    return struct.pack('<I', x & 0xFFFFFFFF)


def u16(x):
    return struct.pack('<H', x & 0xFFFF)


def f32(x):
    return struct.pack('<f', x)


def varint(v):
    groups = []
    while True:
        groups.append(v & 0x7F)
        v >>= 7
        if not v:
            break
    groups.reverse()
    return bytes([g | 0x80 for g in groups[:-1]] + [groups[-1]])


def rtpc_entry(rid, typ=0, acc=1, param=0, curve=1, scaling=0, pts=((0.0, 0.0, 4), (1.0, 1.0, 4))):
    return u32(rid) + bytes([typ, acc]) + varint(param) + u32(curve) + bytes([scaling]) + u16(len(pts)) + b''.join(f32(x) + f32(y) + u32(i) for x, y, i in pts)


def state_block(states):
    return u32(len(states)) + b''.join(u32(g) + bytes([sync]) + u16(len(prs)) + b''.join(u32(a) + u32(c) for a, c in prs) for g, sync, prs in states)


def node_params(fx_override=0, fx=(), fx_bypass=0, attach=0, bus=0, parent=0, flags=0, props=(), ranged=(), pos=0xC0, aux=0, aux_ids=(0, 0, 0, 0), adv=(0, 1, 0, 0, 0), states=(), rtpcs=()):
    """The NodeBaseParams block in the runtime's order. adv is (b0, b1, u16, b4, b5)."""
    b = bytes([fx_override, len(fx)])
    if fx:
        b += bytes([fx_bypass]) + b''.join(bytes([slot]) + u32(i) + bytes([share, rendered]) for slot, i, share, rendered in fx)
    b += bytes([attach]) + u32(bus) + u32(parent) + bytes([flags])
    b += bytes([len(props)]) + bytes(k for k, v in props) + b''.join(u32(v) for k, v in props)
    b += bytes([len(ranged)]) + bytes(k for k, lo, hi in ranged) + b''.join(f32(lo) + f32(hi) for k, lo, hi in ranged)
    b += bytes([pos, aux])
    if aux & 8:
        b += b''.join(u32(x) for x in aux_ids)
    b += bytes([adv[0], adv[1]]) + u16(adv[2]) + bytes([adv[3], adv[4]])
    b += state_block(states)
    b += u16(len(rtpcs)) + b''.join(rtpcs)
    return b


def sound(i, plugin=0x00040001, stream=1, media=None, mem=0x1234, bits=0, params=b'', **kw):
    body = u32(i) + u32(plugin) + bytes([stream]) + u32(i + 5 if media is None else media) + u32(mem) + bytes([bits])
    if plugin & 0xF in (2, 5):
        body += u32(len(params)) + params
    return (2, body + node_params(**kw))


def actor_mixer(i, children=(), **kw):
    return (7, u32(i) + node_params(**kw) + u32(len(children)) + b''.join(u32(c) for c in children))


def bus(i, parent=0, props=(), a=0, b=0, maxinst=0, cfg=0x4101, c=2, recovery=1000, maxduck=-96.0, ducks=(), fx=(), fx_bypass=0, mixer=(0, 0), attach=0, rtpcs=(), states=()):
    body = u32(i) + u32(parent) + bytes([len(props)]) + bytes(k for k, v in props) + b''.join(u32(v) for k, v in props)
    body += bytes([a, b]) + u16(maxinst) + u32(cfg) + bytes([c]) + u32(recovery) + f32(maxduck)
    body += u32(len(ducks)) + b''.join(u32(t) + f32(v) + u32(fo) + u32(fi) + bytes([cv, tp]) for t, v, fo, fi, cv, tp in ducks)
    body += bytes([len(fx)])
    if fx:
        body += bytes([fx_bypass]) + b''.join(bytes([slot]) + u32(fid) + bytes([share, rend]) for slot, fid, share, rend in fx)
    body += u32(mixer[0]) + bytes([mixer[1], attach]) + u16(len(rtpcs)) + b''.join(rtpcs)
    body += state_block(states)
    return (8, body)


def event(i, actions):
    return (4, u32(i) + u32(len(actions)) + b''.join(u32(a) for a in actions))


def action(i, typ=0x0403, target=1, is_bus=0, props=(), ranged=(), fade=4, bank_id=0x8E39A50B):
    body = u32(i) + u16(typ) + u32(target) + bytes([is_bus, len(props)]) + bytes(k for k, v in props) + b''.join(u32(v) for k, v in props)
    body += bytes([len(ranged)]) + bytes(k for k, lo, hi in ranged) + b''.join(f32(lo) + f32(hi) for k, lo, hi in ranged)
    body += bytes([fade]) + u32(bank_id)
    return (3, body)


def bank_file(bank_id, objs):
    body = u32(len(objs)) + b''.join(bytes([t]) + u32(len(b)) + b for t, b in objs)
    hirc = b'HIRC' + u32(len(body)) + body
    bkhd = struct.pack('<IIIHHI', 0x78, bank_id, 0, 0, 0, 77)
    return b'BKHD' + u32(len(bkhd)) + bkhd + hirc


class Scenario:
    def __init__(self, name, banks, fill=0, rate=48000, floor=0, mgr=True):
        self.name, self.banks, self.fill, self.rate, self.floor, self.mgr = name, banks, fill, rate, floor, mgr


def scenarios():
    import random
    out = []

    def sc(name, banks, **kw):
        out.append(Scenario(name, banks, **kw))
    B1, B2 = 0x2001, 0x2002
    # --- the chain and its links
    sc('chain_ok', [bank_file(B1, [sound(10), sound(11), actor_mixer(20, [10, 11]), actor_mixer(30, [20])])])
    sc('child_absent_returns_0xF', [bank_file(B1, [sound(10), actor_mixer(20, [10, 99, 11])])])
    sc('child_id_zero_returns_0xE', [bank_file(B1, [sound(10), actor_mixer(20, [10, 0])])])
    sc('child_with_a_parent_already', [bank_file(B1, [sound(10), actor_mixer(20, [10]), actor_mixer(21, [10])])])
    sc('children_sorted_by_id', [bank_file(B1, [sound(40), sound(10), sound(30), sound(20), actor_mixer(50, [40, 10, 30, 20])])])
    sc('direct_parent_present_and_absent', [bank_file(B1, [actor_mixer(20, []), sound(10, parent=20), sound(11, parent=99), sound(12, parent=20)])])
    sc('direct_parent_after_children', [bank_file(B1, [sound(10), actor_mixer(20, [10]), sound(11, parent=20), sound(12, parent=20), sound(13, parent=20)])])
    sc('found_again_in_a_second_bank', [bank_file(B1, [sound(10), actor_mixer(20, [10])]), bank_file(B2, [sound(10), actor_mixer(20, [10]), sound(11)])])
    sc('source_plugin_sound_twice', [bank_file(B1, [sound(10, plugin=0x00640002, stream=0, params=b'')]), bank_file(B2, [sound(10, plugin=0x00640002, stream=0, params=b'')])])
    sc('source_plugin_sound_with_params', [bank_file(B1, [sound(10, plugin=0x00650005, stream=2, params=b'abcdefgh')])])
    sc('sound_plugin_nibble_0_and_other', [bank_file(B1, [sound(10, plugin=0x00000000, stream=0), sound(11, plugin=0x00040003, stream=1)])])
    sc('sound_streams', [bank_file(B1, [sound(10 + k, stream=k % 3, bits=k * 7 & 0xFF) for k in range(9)])])
    # --- buses
    top = [bus(100, parent=0), bus(101, parent=100), bus(102, parent=100), bus(103, parent=101), bus(104, parent=0), bus(105, parent=104)]
    sc('bus_tree', [bank_file(B1, top)])
    sc('bus_parent_absent_returns_2', [bank_file(B1, [bus(100), bus(101, parent=999)])])
    sc('bus_found_again', [bank_file(B1, top), bank_file(B2, [bus(100), bus(106, parent=100)])])
    sc('bus_third_parentless', [bank_file(B1, [bus(100), bus(101), bus(102), bus(103, parent=102)])])
    sc('bus_ducks', [bank_file(B1, [bus(100), bus(101, parent=100), bus(102, parent=100, ducks=[(101, -12.0, 100, 200, 1, 0), (999, -3.0, 5, 6, 2, 5), (100, -6.0, 7, 8, 3, 7)]), bus(103, parent=100, ducks=[(104, -1.0, 1, 2, 0, 0)]), bus(104, parent=100)])])
    sc('bus_children_by_override_bus', [bank_file(B1, [bus(100), bus(101, parent=100), sound(50, bus=101), sound(51, bus=100), sound(52, bus=101), actor_mixer(60, [], bus=100), sound(40, bus=100)])])
    sc('override_bus_absent_returns_2', [bank_file(B1, [bus(100), sound(10, bus=77)])])
    sc('override_bus_node_chain', [bank_file(B1, [bus(100), bus(101, parent=100), sound(10, bus=101), sound(11), actor_mixer(20, [10, 11], bus=100)])])
    sc('bus_fx_and_mixer', [bank_file(B1, [bus(100), bus(101, parent=100, fx=[(0, 0x1111, 1, 0), (1, 0, 0, 1), (2, 0x3333, 0, 1), (3, 0x4444, 1, 0)], fx_bypass=5)])])
    sc('bus_fx_slot_above_3_fails', [bank_file(B1, [bus(100), bus(101, parent=100, fx=[(0, 0x1111, 1, 0), (4, 0x2222, 1, 0)], fx_bypass=1)])])
    sc('bus_all_zero_fx_ids', [bank_file(B1, [bus(100), bus(101, parent=100, fx=[(0, 0, 1, 0)], fx_bypass=1)])])
    sc('bus_rtpc', [bank_file(B1, [bus(100), bus(101, parent=100, rtpcs=[rtpc_entry(0xAAAA, 0, 1, 5), rtpc_entry(0xBBBB, 2, 0, 0), rtpc_entry(0xCCCC, 0, 1, 5, curve=9)])])])
    sc('bus_rtpc_manager_null_returns_2', [bank_file(B1, [bus(100), bus(101, parent=100, rtpcs=[rtpc_entry(0xAAAA)])])], mgr=False)
    sc('bus_states', [bank_file(B1, [bus(100), bus(101, parent=100, states=[(0x1000, 1, [(1, 2), (3, 4)]), (0x2000, 0, [])])])])
    # --- node FX / RTPC / states
    sc('sound_fx_rendered', [bank_file(B1, [bus(100), sound(10, fx=[(0, 0x5555, 1, 0), (1, 0x6666, 0, 1), (2, 0, 1, 0)], fx_bypass=3, fx_override=1), sound(11, fx=[(1, 0x7777, 1, 1)], fx_bypass=0)])])
    sc('sound_fx_twice_partial', [bank_file(B1, [sound(10, fx=[(0, 0x5555, 1, 0), (1, 0x6666, 0, 1)], fx_bypass=3, plugin=0x00640002, stream=0)]), bank_file(B2, [sound(10, fx=[(0, 0x5555, 1, 1), (3, 0x6666, 0, 0)], fx_bypass=1, plugin=0x00640002, stream=0)])])
    sc('sound_rtpc', [bank_file(B1, [sound(10, rtpcs=[rtpc_entry(0xAAAA, 0, 1, 0), rtpc_entry(0xBBBB, 2, 0, 3), rtpc_entry(0xCCCC, 0, 1, 0, curve=2), rtpc_entry(0xDDDD, 0, 1, 40)])])])
    sc('sound_rtpc_manager_null_returns_2', [bank_file(B1, [sound(10, rtpcs=[rtpc_entry(0xAAAA)])])], mgr=False)
    sc('sound_states', [bank_file(B1, [sound(10, states=[(0x1000, 1, [(1, 2), (3, 4)]), (0x2000, 0, [])])])])
    # --- events and actions
    sc('event_ok', [bank_file(B1, [action(501), action(502), action(503), event(600, [501, 502, 503])])])
    sc('event_action_absent_returns_2', [bank_file(B1, [action(501), event(600, [501, 999])])])
    sc('event_zero_action_returns_0xE', [bank_file(B1, [action(501), event(600, [501, 0, 502])])])
    sc('event_empty', [bank_file(B1, [event(600, [])])])
    sc('event_twice', [bank_file(B1, [action(501), event(600, [501])]), bank_file(B2, [action(501), event(600, [501]), event(601, [501])])])
    sc('action_found_again', [bank_file(B1, [action(501)]), bank_file(B2, [action(501), action(501)])])
    sc('action_kinds', [bank_file(B1, [action(510, typ=0x0403), action(511, typ=0x0404, fade=9, bank_id=7), action(512, typ=0x0402, fade=31, bank_id=0xFFFFFFFF), action(513, typ=0x0401, target=0x80000000, is_bus=1)])])
    sc('action_delay', [bank_file(B1, [action(520, props=[(0x0F, 200)]), action(521, props=[(0x0F, 0), (0, 0x3F800000)]), action(522, props=[(0x0F, 1000000)]), action(523, props=[(0x0F, 0xFFFFFFF0)])])], rate=44100)
    sc('action_delay_rate_48000', [bank_file(B1, [action(520, props=[(0x0F, 200), (1, 5), (0x0F, 7)]), action(521, props=[(2, 9)], ranged=[(1, 0.5, 1.5)])])])
    # --- batch 6c verifier findings: the paths the first oracle never reached
    sc('fx_zero_id_bypass_0_sound', [bank_file(B1, [sound(10, fx=[(0, 0, 1, 0)], fx_bypass=0), sound(11, fx=[(0, 0, 1, 0)], fx_bypass=1), sound(12, fx=[(0, 0, 1, 0), (1, 0, 0, 0)], fx_bypass=0, fx_override=1)])])
    sc('fx_zero_id_bypass_0_bus', [bank_file(B1, [bus(100), bus(101, parent=100, fx=[(0, 0, 1, 0)], fx_bypass=0), bus(102, parent=100, fx=[(0, 0, 1, 0)], fx_bypass=1)])])
    sc('duck_duplicate_target', [bank_file(B1, [bus(100), bus(101, parent=100), bus(102, parent=100, ducks=[(101, -1.0, 1, 2, 1, 0), (100, -2.0, 3, 4, 2, 3), (101, -5.0, 9, 8, 3, 5)])])])
    sc('duck_capacity_0x64', [bank_file(B1, [bus(100), bus(101, parent=100, ducks=[(5000 + k, -1.0, k, k, 1, 0) for k in range(101)])])])
    sc('duck_capacity_with_duplicate_at_the_end', [bank_file(B1, [bus(100), bus(101, parent=100, ducks=[(5000 + k, -1.0, k, k, 1, 0) for k in range(100)] + [(5003, -9.0, 7, 7, 2, 2)])])])
    sc('aux_ids_all_zero_or_not', [bank_file(B1, [sound(10, aux=8, aux_ids=(0, 0, 0, 0)), sound(11, aux=8, aux_ids=(0, 5, 0, 0)), sound(12, aux=0), sound(13, aux=9, aux_ids=(1, 2, 3, 4))])])
    sc('sound_stream_type_3_returns_2', [bank_file(B1, [sound(10, stream=0), sound(11, stream=3)])])
    sc('sound_stream_type_3_other_plugin', [bank_file(B1, [sound(10, plugin=0x00000000, stream=3), sound(11, plugin=0x00640002, stream=3)])])
    sc('action_ranged_delay', [bank_file(B1, [action(530, ranged=[(0x0F, 200.0, 400.0)]), action(531, ranged=[(1, 1.0, 2.0), (0x0F, 0.5, -3.0), (0x0F, 9.0, 9.0)], props=[(0x0F, 100)])])], rate=44100)
    # --- the bit-level sweeps: random NodeBase blocks and bus blocks, the engine decides
    rnd = random.Random(0x6C44)
    nodes = []
    for k in range(240):
        props = [(rnd.randrange(0, 60), rnd.getrandbits(32)) for _ in range(rnd.choice([0, 0, 1, 2, 5]))]
        ranged = [(rnd.randrange(0, 60), rnd.uniform(-5, 5), rnd.uniform(0, 9)) for _ in range(rnd.choice([0, 0, 1, 3]))]
        pos = rnd.choice([0xC0, 0xC3, 0xC1, 0xC7, 0xC5, 0x00, 0x03, 0x02, 0x07, 0xD7, 0xE3, rnd.getrandbits(8)])
        if pos & 1 and pos & 8:
            pos &= ~8
        aux = rnd.choice([0, 0, 1, 2, 4, 8, 15, rnd.getrandbits(8) & 0x0F])
        nodes.append(sound(1000 + k, flags=rnd.choice([0, 1, 0x24, rnd.getrandbits(8) & 0x3F]), fx_override=rnd.choice([0, 0, 1, 2]), attach=rnd.choice([0, 1, 2]),
                           props=props, ranged=ranged, pos=pos, aux=aux, aux_ids=[rnd.getrandbits(32) for _ in range(4)],
                           adv=(rnd.getrandbits(8) & 0x1F if rnd.random() < 0.8 else rnd.getrandbits(8), rnd.getrandbits(8), rnd.getrandbits(16), rnd.choice([0, 1, 2, 3, 4, 15, rnd.getrandbits(8)]), rnd.getrandbits(8)),
                           plugin=rnd.choice([0x00040001, 0x00040001, 0x00640002, 0x00000000, 0x00650005]), stream=rnd.choice([0, 1, 2]), bits=rnd.getrandbits(8), mem=rnd.getrandbits(16)))
    sc('node_bit_sweep', [bank_file(B1, nodes)])
    sc('node_bit_sweep_pool_ff', [bank_file(B1, nodes)], fill=0xFF)
    buses = [bus(2000)]
    for k in range(120):
        parent = rnd.choice([0, 2000, 2000, 2000 + rnd.randrange(0, k + 1)]) if k else 2000
        fx = []
        if rnd.random() < 0.5:
            for slot in rnd.sample(range(4), rnd.choice([1, 2, 4])):
                fx.append((slot, rnd.choice([0, 0x1000 + k, 0x2000 + slot]), rnd.choice([0, 1, 7]), rnd.choice([0, 1, 5])))
        cfg_kind = rnd.choice([1, 1, 2, 3, 0])
        cfg = rnd.getrandbits(8) | (cfg_kind << 8) | (rnd.getrandbits(20) << 12)
        buses.append(bus(2001 + k, parent=parent, a=rnd.getrandbits(2), b=rnd.getrandbits(3), maxinst=rnd.getrandbits(16), cfg=cfg, c=rnd.getrandbits(2),
                         recovery=rnd.choice([0, 1, 500, 1000, 4000, 100000, rnd.getrandbits(20)]), maxduck=rnd.choice([-96.0, -12.5, 0.0, 3.5]),
                         fx=fx, fx_bypass=rnd.getrandbits(8), attach=rnd.getrandbits(2), props=[(rnd.randrange(0, 40), rnd.getrandbits(32)) for _ in range(rnd.choice([0, 1, 4]))],
                         ducks=[(2000 + rnd.randrange(0, k + 2), rnd.uniform(-20, 0), rnd.getrandbits(10), rnd.getrandbits(10), rnd.getrandbits(3), rnd.randrange(0, 14)) for _ in range(rnd.choice([0, 0, 1, 2]))]))
    sc('bus_bit_sweep', [bank_file(B1, buses)], rate=44100, floor=100)
    sc('bus_bit_sweep_pool_ff', [bank_file(B1, buses)], fill=0xFF, rate=22050, floor=0)
    plays = []
    for k in range(80):
        props = [(rnd.choice([0x0F, 0x0F, 1, 2, 3, 0x10]), rnd.choice([0, 1, 200, 1000, 123456, rnd.getrandbits(32)])) for _ in range(rnd.choice([0, 1, 2, 3]))]
        plays.append(action(3000 + k, typ=rnd.choice([0x0403, 0x0403, 0x0404, 0x0401, 0x0402]), target=rnd.getrandbits(32), is_bus=rnd.getrandbits(1), props=props,
                            ranged=[(rnd.choice([1, 2, 5]), rnd.uniform(0, 3), rnd.uniform(3, 9)) for _ in range(rnd.choice([0, 0, 1]))], fade=rnd.getrandbits(8), bank_id=rnd.getrandbits(32)))
    sc('action_sweep', [bank_file(B1, plays)], rate=48000)
    sc('action_sweep_rate_44100', [bank_file(B1, plays)], rate=44100, fill=0xFF)
    return out


def hirc_objs(d):
    """[(type, id)] of the HIRC objects of a bank file in memory."""
    off = 0
    objs = []
    while off + 8 <= len(d):
        t = d[off:off + 4]
        sz = struct.unpack_from('<I', d, off + 4)[0]
        if t == b'HIRC':
            n = struct.unpack_from('<I', d, off + 8)[0]
            p = off + 12
            for _ in range(n):
                osz = struct.unpack_from('<I', d, p + 1)[0]
                objs.append((d[p], struct.unpack_from('<I', d, p + 5)[0]))
                p += 5 + osz
        off += 8 + sz
    return objs


def scenario_pass(sc):
    types = {}
    files = []
    for bi, data in enumerate(sc.banks):
        for t, i in hirc_objs(data):
            types.setdefault(i, t)
        files.append(('b%d' % bi, data))
    return Pass(sc.name, files, types, sc.fill, sc.rate, sc.floor, mgr=sc.mgr)


def is_key(r):
    kind, ident, text = r
    typ = text.split(' ')[0]
    t = {'4': 'E', '3': 'X'}.get(typ)
    if t:
        return (t, ident) in KEY
    return typ == '8' or (int(typ), ident) in KEY


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
    text_mode = '--text' in sys.argv
    out_rows = []
    scen_rows = []
    summary = []
    for (name, banks, fill, rate, floor) in PASSES:
        p = shipped_pass(name, banks, fill, rate, floor)
        rows = p.rows()
        summary.append('pass %s: %d objects, results %s' % (name, len(rows), p.results))
        for kind, ident, text in rows:
            if text_mode:
                print(name, text)
                continue
            if is_key((kind, ident, text)):
                out_rows.append('%s F %s' % (name, text))
            else:
                out_rows.append('%s H %s %s %08X' % (name, text.split(' ')[0], ident, fnv(text)))
        out_rows.append('%s R %s' % (name, ' '.join('%s=%d' % (n.replace('\\', '/'), r) for n, r in p.results)))
        out_rows.append('%s M %s' % (name, p.master_row()))
    for sc in scenarios():
        p = scenario_pass(sc)
        rows = p.rows()
        summary.append('scenario %s: %d objects, results %s' % (sc.name, len(rows), [r for _, r in p.results]))
        if text_mode:
            for kind, ident, text in rows:
                print(sc.name, text)
            continue
        scen_rows.append('%s P %d %d %d %d' % (sc.name, sc.fill, sc.rate, sc.floor, 1 if sc.mgr else 0))
        for bi, b in enumerate(sc.banks):
            scen_rows.append('%s B %d %s' % (sc.name, bi, b.hex()))
        scen_rows.append('%s R %s' % (sc.name, ' '.join(str(r) for _, r in p.results)))
        scen_rows.append('%s M %s' % (sc.name, p.master_row()))
        for kind, ident, text in rows:
            scen_rows.append('%s F %s' % (sc.name, text))
    sys.stderr.write('\n'.join(summary) + '\n')
    if text_mode:
        return
    print("// <auto-generated> by re-analysis/tools/emu/emu_graph.py from the engine's own output (the real HIRC walker 0x9B3260 and the Sound / ActorMixer / Bus / Event / Action loaders under Unicorn): do not edit.")
    print('namespace Cozmo.Protocol.Tests;')
    print()
    print('internal static class WwiseRuntimeGraphOracle')
    print('{')
    print('    private static void Add(List<string> l, ReadOnlySpan<byte> chunk)')
    print('    {')
    print("        foreach (var row in System.Text.Encoding.UTF8.GetString(chunk).Split('\\n', StringSplitOptions.RemoveEmptyEntries)) l.Add(row);")
    print('    }')
    print()
    print(cs_strings('Rows', 'One row per engine object and pass: "pass F <full row>" for the shipped chain, "pass H type id hash" (FNV-1a 32 of the same row text) for every other object, "pass R bank=result ..." for the walker results. Passes: full (pool byte 00, rate 48000, floor 0), pool (pool byte FF), init44100 (Init.bnk only, rate 44100, floor 50000), init22050 (Init.bnk, pool byte FF, rate 22050).', out_rows))
    print()
    print(cs_strings('Scenarios', 'Synthetic banks the engine loaded: "name P fill rate floor manager", "name B index hex" (the bank file), "name R result ..." (one per bank) and "name F row" (every object of the four tables, full text).', scen_rows))
    print('}')


if __name__ == '__main__':
    main()
