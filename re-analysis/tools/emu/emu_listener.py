"""Run the engine's own RTPC listener registration and removal under Unicorn and write the golden values for WwiseListenerTests (B-M6b-4 batch 5h, M6-009, C39):

    python re-analysis/tools/emu/emu_listener.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseListenerOracle.cs

The REAL functions run: 0xA19ECC -> 0x9F7390 (both loops, holders 1/2 and 1/3/2) with 0xA1973C, 0xA19778 and 0xA198A4, the removals 0xA19F60 -> 0x9F9064, the destructor step 0xA19D44 and the node destructor step 0xA1A0B4 -> 0x9F9064,
on hand-built engine registries (0x20-byte registry, 0x28-byte entries) in emulated memory.

Stand-ins (only these): the pool allocator 0xA7A7F4 / 0xA7A988 (the harness' bump allocator; the n-th allocation can be made to fail), the two RTPC-manager calls 0xA1008C and 0xA10220 (they only log: holder address and mask),
and 0x9C54E8 (the collapse predicate, evaluated by the rules of C34.1 B7 from the bus fields the case writes: [bus+0x68] byte, [bus+0x46] & 0x80, [bus+0x38] == 0, [bus+0x40] & 0xE0000, [bus+0x54]; the FX chunk and vt+0x44 tests are never true here). The
call order of 0x9C54E8 is logged and compared.

A case = a node/bus graph, up to four contexts, one registration (0xA19ECC), one removal (0xA19F60 / 0xA19D44 / 0xA1A0B4) and optionally a failing allocation. The expected values are the registries (mask A, cache, count, capacity, byte
+0x1C, entries in array order), [ctx+0x20] of every context, the manager call log, the 0x9C54E8 call log and the allocation count after the registration and after the removal. The C# test builds the same graph and compares the same text.
"""
import base64
import gzip
import random
import struct
import sys

sys.dont_write_bytecode = True
from emu_common import *

M64 = (1 << 64) - 1
L_FULL = 0x3FE3FFFE67BD
MASK_BITS = [0, 2, 3, 4, 5, 7, 8, 9, 10, 13, 14, 32, 33, 37, 38, 39, 40, 41, 42, 43, 44, 45]
OTHER_BITS = [1, 6, 11, 12, 15, 16, 17, 20, 31, 34, 35, 36, 46, 47]
NODE_SIZE = 0x120
KEY_W0 = [1, 2, 3]
KEY_W4 = [0, 1, 5]
KEY_W8 = [0, 1]
KEY_B0C = [0xFF, 0x1F, 0x3F, 0x00, 0x1E, 0x7F]
KEY_B10 = [0xFF, 0x00, 0x7F, 0x80, 0x01]
KEY_W14 = [0, 1]


def less(e, k):
    """The ordering of 0xA19778 (used only to build valid sorted starting arrays)."""
    if e[0] != k[0]: return e[0] < k[0]
    if e[1] != k[1]: return e[1] < k[1]
    if e[2] != k[2]: return e[2] < k[2]
    if ((e[3] + 1) & 0x1F) < ((k[3] + 1) & 0x1F): return True
    if e[3] != k[3]: return False
    if ((e[4] + 1) & 0xFF) < ((k[4] + 1) & 0xFF): return True
    if e[4] != k[4]: return False
    return e[5] < k[5]


def rkey(rnd):
    return (rnd.choice(KEY_W0), rnd.choice(KEY_W4), rnd.choice(KEY_W8), rnd.choice(KEY_B0C), rnd.choice(KEY_B10), rnd.choice(KEY_W14))


def rmask(rnd):
    r = rnd.random()
    if r < 0.12:
        return rnd.getrandbits(64)
    n = rnd.choice([1, 1, 2, 2, 3, 4])
    m = 0
    for _ in range(n):
        m |= 1 << (rnd.choice(MASK_BITS) if rnd.random() < 0.85 else rnd.choice(OTHER_BITS))
    return m


def rw40(rnd):
    r = rnd.random()
    if r < 0.4:
        return 0
    if r < 0.55:
        return rnd.choice([1, 2, 0x20, 0x400, 0xFFFFFFFF, 0x1FFFFF])
    w = 0
    for _ in range(rnd.choice([1, 2, 3, 5])):
        w |= 1 << (rnd.choice(range(15, 29)) if rnd.random() < 0.7 else rnd.randrange(32))
    return w


def make_registry(rnd, nctx, ctx_keys):
    mask_a = rmask(rnd)
    count = rnd.choice([0, 0, 1, 1, 2, 3, 4])
    entries = []
    for _ in range(count):
        ci = rnd.randrange(nctx)
        key = ctx_keys[ci] if rnd.random() < 0.7 else rkey(rnd)
        m = rmask(rnd)
        # insert at the lower_bound slot (ties go before equal-key entries) so the array is a valid sorted array
        lo, hi = 0, len(entries)
        while lo < hi:
            mid = (lo + hi) >> 1
            if less(entries[mid][0], key): lo = mid + 1
            else: hi = mid
        entries.append(None)
        entries[lo + 1:] = entries[lo:-1]
        entries[lo] = (key, m, ci)
    if rnd.random() < 0.08 and len(entries) > 1:
        rnd.shuffle(entries)                      # an unsorted array: the search is deterministic anyway
    cap = len(entries) + rnd.choice([0, 0, 0, 1, 2])
    anded = M64
    for e in entries:
        anded &= e[1]
    cache = anded if rnd.random() < 0.6 else (M64 if rnd.random() < 0.5 else rnd.getrandbits(64))
    return dict(a=mask_a, cache=cache, cap=cap, b1c=rnd.choice([0, 0, 1]), entries=entries)


def make_case(rnd, cid):
    nctx = rnd.choice([2, 3, 4])
    ctx_keys = [rkey(rnd) for _ in range(nctx)]
    if rnd.random() < 0.5:
        for i in range(1, nctx):
            if rnd.random() < 0.5:
                ctx_keys[i] = ctx_keys[0]
    nn = rnd.choice([1, 1, 2, 3, 4])
    nb = rnd.choice([0, 1, 1, 2, 3, 3])
    start_bus = nb > 0 and rnd.random() < 0.15
    nodes = []
    for i in range(nn):
        nodes.append(dict(bus=0, b46=rnd.randrange(256) & ~4, w40=rw40(rnd), parent=(i + 1 if i + 1 < nn else -1), out=-1, b68=0, w54=0))
    for j in range(nb):
        i = nn + j
        nodes.append(dict(bus=1, b46=rnd.randrange(256) if rnd.random() < 0.5 else rnd.choice([0, 0, 0x80, 0x01]), w40=rw40(rnd),
                          parent=-1, out=(i + 1 if j + 1 < nb else -1), b68=rnd.choice([0, 0, 1, 2]), w54=rnd.choice([0, 0, 0, 1])))
        if rnd.random() < 0.2:
            nodes[-1]['w40'] |= 0xE0000 if rnd.random() < 0.5 else 0
    if nb:
        for n in nodes[:nn]:
            r = rnd.random()
            n['out'] = -1 if r < 0.45 else nn + rnd.randrange(nb)
    for n in nodes:
        pbus = n['bus']
        n['r1'] = make_registry(rnd, nctx, ctx_keys) if rnd.random() < 0.6 else None
        n['r2'] = make_registry(rnd, nctx, ctx_keys) if rnd.random() < 0.25 else None
        n['r3'] = make_registry(rnd, nctx, ctx_keys) if (pbus and rnd.random() < 0.3) else None
    if start_bus:
        start = nn + rnd.randrange(nb)
        nodes[start]['b46'] |= 4
    else:
        start = 0
    reg_ctx = 0 if rnd.random() < 0.8 else rnd.randrange(nctx)
    r = rnd.random()
    mask = L_FULL if r < 0.7 else (0 if r < 0.8 else rmask(rnd) | rmask(rnd))
    flag = rnd.choice([1, 1, 1, 1, 0, 2, 3])
    ctx20 = [-1] * nctx
    for i in range(nctx):
        if rnd.random() < (0.1 if i == reg_ctx else 0.5):
            ctx20[i] = rnd.randrange(len(nodes))
    fail = rnd.choice([1, 2, 3]) if rnd.random() < 0.12 else 0
    k = rnd.random()
    if k < 0.45:
        unreg = (1, reg_ctx, 0, L_FULL if rnd.random() < 0.7 else (mask if rnd.random() < 0.5 else rmask(rnd)))
    elif k < 0.7:
        unreg = (2, reg_ctx, 0, 0)
    else:
        holder_node = rnd.randrange(len(nodes)) if rnd.random() < 0.85 else -1
        unreg = (3, reg_ctx, holder_node, 0)
    glist = list(range(nctx))
    rnd.shuffle(glist)
    return dict(id=cid, nctx=nctx, ctx_keys=ctx_keys, ctx20=ctx20, nodes=nodes, start=start, reg=(reg_ctx, start, mask, flag), unreg=unreg, fail=fail, glist=glist)


# ------------------------------------------------------------------------------------------------ the world

class World:
    def __init__(self):
        e = Emu()
        e.std_hooks()
        self.e = e
        self.mlog = []
        self.clog = []
        e.hook(0xA1008C, lambda em: (self.mlog.append(('A', em.reg(1), em.reg(2) | (em.reg(3) << 32))), 0)[1])
        e.hook(0xA10220, lambda em: (self.mlog.append(('B', em.reg(1), em.reg(2) | (em.reg(3) << 32))), 0)[1])

        def c54(em):
            bus = em.reg(0)
            self.clog.append(bus)
            r = (em.r8(bus + 0x68) != 0 or (em.r8(bus + 0x46) & 0x80) != 0 or em.r32(bus + 0x38) == 0
                 or (em.r32(bus + 0x40) & 0xE0000) != 0 or em.r32(bus + 0x54) != 0)
            return 1 if r else 0
        e.hook(0x9C54E8, c54)

        def memcpy(em):
            d, s_, n = em.reg(0), em.reg(1), em.reg(2)
            em.uc.mem_write(d, bytes(em.uc.mem_read(s_, n)))
            return d
        e.hook(0x4D37F0, memcpy)                  # the PBI ctor's 0x44-byte block copy
        e.hook(0xA149C8, lambda em: 0)            # reached by [pbi+0x1E4] == 0x90 with [pbi+0x1E7] != 0 (an RTPC set; not part of the key)
        e.hook(RET + 0x100, lambda em: 0)         # the ctx node's vt+8 called by 0x9BC90C

    def build(self, c):
        e = self.e
        e.heap = HEAP
        e.alloc_calls = 0
        e.fail_alloc_at = c['fail'] or None
        self.mlog.clear()
        self.clog.clear()
        e.w32(0x108DC44, 0)
        self.ctx = []
        for i in range(c['nctx']):
            a = e.alloc(0x40)
            e.uc.mem_write(a, bytes(0x40))
            w0, w4, w8, b0c, b10, w14 = c['ctx_keys'][i]
            e.w32(a + 8, w0); e.w32(a + 0xC, w4); e.w32(a + 0x10, w8); e.w8(a + 0x14, b0c); e.w8(a + 0x18, b10); e.w32(a + 0x1C, w14)
            self.ctx.append(a)
        self.node = []
        for n in c['nodes']:
            a = e.alloc(NODE_SIZE)
            e.uc.mem_write(a, bytes(NODE_SIZE))
            self.node.append(a)
        self.regs = {}
        for i, n in enumerate(c['nodes']):
            a = self.node[i]
            e.w32(a + 0x34, self.node[n['parent']] if n['parent'] >= 0 else 0)
            e.w32(a + 0x38, self.node[n['out']] if n['out'] >= 0 else 0)
            e.w32(a + 0x40, n['w40'])
            e.w8(a + 0x46, n['b46'])
            e.w8(a + 0x68, n['b68'])
            e.w32(a + 0x54, n['w54'])
            for key, (poff, hoff) in (('r1', (0x14, 0x10)), ('r2', (0x20, 0x1C)), ('r3', (0xC8, 0xC4))):
                r = n[key]
                if r is None:
                    continue
                reg = e.alloc(0x20)
                e.uc.mem_write(reg, bytes(0x20))
                arr = e.alloc(max(r['cap'], 1) * 0x28)
                e.uc.mem_write(arr, bytes(max(r['cap'], 1) * 0x28))
                e.w32(reg, r['a'] & 0xFFFFFFFF); e.w32(reg + 4, r['a'] >> 32)
                e.w32(reg + 8, r['cache'] & 0xFFFFFFFF); e.w32(reg + 12, r['cache'] >> 32)
                e.w32(reg + 0x10, arr if r['cap'] else 0)
                e.w32(reg + 0x14, len(r['entries']))
                e.w32(reg + 0x18, r['cap'])
                e.w8(reg + 0x1C, r['b1c'])
                for j, (k, m, ci) in enumerate(r['entries']):
                    p = arr + 0x28 * j
                    e.w32(p, k[0]); e.w32(p + 4, k[1]); e.w32(p + 8, k[2]); e.w8(p + 0xC, k[3]); e.w8(p + 0x10, k[4]); e.w32(p + 0x14, k[5])
                    e.w32(p + 0x18, m & 0xFFFFFFFF); e.w32(p + 0x1C, m >> 32); e.w32(p + 0x20, self.ctx[ci])
                e.w32(a + poff, reg)
                self.regs[(i, key)] = reg
        for i, a in enumerate(self.ctx):
            e.w32(a + 0x20, self.node[c['ctx20'][i]] if c['ctx20'][i] >= 0 else 0)

    # -- the observable state, as text
    def snap(self, c):
        e = self.e
        out = []
        ctxname = {a: i for i, a in enumerate(self.ctx)}
        for i, n in enumerate(c['nodes']):
            a = self.node[i]
            for key, poff, h in (('r1', 0x14, 1), ('r2', 0x20, 2), ('r3', 0xC8, 3)):
                reg = e.r32(a + poff)
                if not reg:
                    continue
                am = e.r32(reg) | (e.r32(reg + 4) << 32)
                cache = e.r32(reg + 8) | (e.r32(reg + 12) << 32)
                cnt = e.r32(reg + 0x14)
                cap = e.r32(reg + 0x18)
                arr = e.r32(reg + 0x10)
                ents = []
                for j in range(cnt):
                    p = arr + 0x28 * j
                    ents.append('%x.%x.%x.%x.%x.%x.%x.%s' % (e.r32(p), e.r32(p + 4), e.r32(p + 8), e.r8(p + 0xC), e.r8(p + 0x10), e.r32(p + 0x14),
                                                           e.r32(p + 0x18) | (e.r32(p + 0x1C) << 32), ctxname.get(e.r32(p + 0x20), 'x')))
                out.append('N%dH%d:%x/%x/%d/%d/%x[%s]' % (i, h, am, cache, cnt, cap, e.r8(reg + 0x1C), ';'.join(ents)))
        for i, a in enumerate(self.ctx):
            v = e.r32(a + 0x20)
            out.append('X%d:%s' % (i, ('n%d' % self.node.index(v)) if v in self.node else ('-' if v == 0 else '?')))
        hold = {}
        for i, a in enumerate(self.node):
            hold[a + 0x10] = 'n%dh1' % i
            hold[a + 0x1C] = 'n%dh2' % i
            hold[a + 0xC4] = 'n%dh3' % i
        for k, h, m in self.mlog:
            out.append('M%s:%s:%x' % (k, hold.get(h, '?%x' % h), m))
        nodename = {a: i for i, a in enumerate(self.node)}
        for b in self.clog:
            out.append('K:%s' % nodename.get(b, '?'))
        out.append('Z:%d' % e.alloc_calls)
        return ' '.join(out)

    def run(self, c):
        e = self.e
        ctx_i, start, mask, flag = c['reg']
        mp = e.alloc(8)
        e.w32(mp, mask & 0xFFFFFFFF); e.w32(mp + 4, mask >> 32)
        e.call(0xA19ECC, self.ctx[ctx_i], self.node[start], mp, flag)
        s1 = self.snap(c)
        self.mlog.clear(); self.clog.clear()
        kind, uctx, arg, umask = c['unreg']
        if kind == 1:
            up = e.alloc(8)
            e.w32(up, umask & 0xFFFFFFFF); e.w32(up + 4, umask >> 32)
            e.call(0xA19F60, self.ctx[uctx], up, 1)
        elif kind == 2:
            e.call(0xA19D44, self.ctx[uctx])
        else:
            prev = 0
            for ci in reversed(c['glist']):
                e.w32(self.ctx[ci] + 4, prev)
                prev = self.ctx[ci]
            e.w32(0x108DC44, prev)
            holder = (self.node[arg] + 0x10) if arg >= 0 else 0
            e.call(0xA1A0B4, holder)
        s2 = self.snap(c)
        return s1, s2


def ctor_key_cases(w, rnd, n):
    """The key (ctx+8..0x20) the PBI ctor 0xA000E8 leaves after 0x9BC90C and its own overwrites 0xA00384..0xA00408, for random params: [params+4] node (0 or an object with [node+8] = id), [params+8] game object,
    [params+0x24] playing id, [params+0x84..0x87]. Returns text lines: go playing target b84 b85 b86 b87 -> w0 w4 w8 b0C b10 w14IsPbi."""
    e = w.e
    out = []
    for _ in range(n):
        e.heap = HEAP
        pbi = e.alloc(0x400); e.uc.mem_write(pbi, bytes(0x400))
        params = e.alloc(0x200); e.uc.mem_write(params, bytes(0x200))
        node = e.alloc(0x40); e.uc.mem_write(node, bytes(0x40))
        tid = rnd.choice([0x1234, 7, 0xFFFFFFF0, 1])
        e.w32(node + 8, tid)
        go = e.alloc(0x100); e.uc.mem_write(go, bytes(0x100))
        cn = e.alloc(0x100); e.uc.mem_write(cn, bytes(0x100))
        vt = e.alloc(0x40); e.w32(vt + 8, RET + 0x100); e.w32(cn, vt)
        have_target = rnd.random() < 0.7
        playing = rnd.choice([0, 1, 0x55, 0xFFFFFFFF, rnd.getrandbits(32)])
        b84 = rnd.choice([0, 0, 0x80, 0x90, 0xA0, 0x10, 0x81, 0xFF, 0x55, 0xA1, 0x91])
        b85, b86, b87 = rnd.choice([0xFF, 0, 0x12, rnd.randrange(256)]), rnd.randrange(256), rnd.choice([0, 0, 3])
        e.w32(params + 4, node if have_target else 0)
        e.w32(params + 8, go)
        e.w32(params + 0x24, playing)
        e.w32(params + 0x84, b84 | (b85 << 8) | (b86 << 16) | (b87 << 24))
        blk = e.alloc(0x44)
        e.call(0xA000E8, pbi, params, cn, 0x99, stack=(blk, 0))
        k = [e.r32(pbi + 0x14), e.r32(pbi + 0x18), e.r32(pbi + 0x1C), e.r8(pbi + 0x20), e.r8(pbi + 0x24), e.r32(pbi + 0x28)]
        out.append('%x %x %x %x %x %x %x %x %x %x %x %x %d' % (go, playing, tid if have_target else 0, b84, b85, b86, b87, k[0], k[1], k[2], k[3], k[4], 1 if k[5] == pbi else 0))
    return out


# ------------------------------------------------------------------------------------------------ the text of a case (what the C# test parses)

def reg_text(r):
    if r is None:
        return '-'
    return '%x,%x,%d,%d,%s' % (r['a'], r['cache'], r['cap'], r['b1c'], ';'.join('%x.%x.%x.%x.%x.%x.%x.%d' % (k + (m, ci)) for (k, m, ci) in r['entries']) or '_')


def case_text(c):
    lines = []
    ctx_i, start, mask, flag = c['reg']
    lines.append('F %d' % c['fail'])
    lines.append('R %d %d %x %d' % (ctx_i, start, mask, flag))
    kind, uctx, arg, umask = c['unreg']
    lines.append('U %d %d %d %x' % (kind, uctx, arg, umask))
    lines.append('G ' + ' '.join(map(str, c['glist'])))
    for i in range(c['nctx']):
        lines.append('C %x.%x.%x.%x.%x.%x %d' % (c['ctx_keys'][i] + (c['ctx20'][i],)))
    for n in c['nodes']:
        lines.append('N %d %x %x %d %d %x %x %s %s %s' % (n['bus'], n['b46'], n['w40'], n['parent'], n['out'], n['b68'], n['w54'], reg_text(n['r1']), reg_text(n['r2']), reg_text(n['r3'])))
    return lines


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 3200
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261004
    rnd = random.Random(seed)
    w = World()
    cases = []
    for i in range(n):
        c = make_case(rnd, i)
        w.build(c)
        s1, s2 = w.run(c)
        cases.append((c, s1, s2))
        if (i + 1) % 200 == 0:
            print('cases', i + 1, file=sys.stderr, flush=True)
    keycases = ctor_key_cases(w, random.Random(seed + 1), 400)
    texts = []
    for c, s1, s2 in cases:
        lines = case_text(c)
        lines.append('E1 ' + s1)
        lines.append('E2 ' + s2)
        texts.append('\n'.join(lines))
    blob = base64.b64encode(gzip.compress('\n\n'.join(texts).encode('utf-8'), 9, mtime=0)).decode('ascii')
    out = []
    o = out.append
    o("// <auto-generated> by re-analysis/tools/emu/emu_listener.py from the engine's own output (the real 0xA19ECC / 0x9F7390 / 0xA1973C / 0xA19778 / 0xA198A4 / 0xA19F60 / 0x9F9064 / 0xA19D44 / 0xA1A0B4 under Unicorn, seed %d, %d cases): do not edit. </auto-generated>" % (seed, len(cases)))
    o('using System.IO.Compression;')
    o('using System.Text;')
    o('')
    o('namespace Cozmo.Protocol.Tests;')
    o('')
    o('internal static class WwiseListenerOracle')
    o('{')
    o("    /// <summary>Each case is its graph (lines F, R, U, G, C, N, see emu_listener.py) followed by the engine's registries, [ctx+0x20], manager call log, 0x9C54E8 call log and allocation count after the registration (E1) and after the removal (E2).")
    o("    /// The cases are stored gzip-compressed (base64) so the test assembly's user-string heap is not spent on 3 MB of text; they are separated by a blank line.</summary>")
    o('    public static readonly string[] Cases = Decode(Data);')
    o('')
    o('    private static string[] Decode(string base64)')
    o('    {')
    o('        using var raw = new MemoryStream(Convert.FromBase64String(base64));')
    o('        using var gz = new GZipStream(raw, CompressionMode.Decompress);')
    o('        using var reader = new StreamReader(gz, Encoding.UTF8);')
    o('        return reader.ReadToEnd().Split("\\n\\n");')
    o('    }')
    o('')
    o('    /// <summary>The PBI ctor 0xA000E8 run under Unicorn (the real 0x9BC90C key writer and the ctor overwrites 0xA00384..0xA00408): game object, playing id, target node id (0 = no node), params+0x84..0x87, then the key words w0, w4, w8, byte@0xC, byte@0x10 and whether word@0x14 is the PBI address (1).</summary>')
    o('    public static readonly string[] KeyCases =')
    o('    {')
    for kc in keycases:
        o('        "%s",' % kc)
    o('    };')
    o('')
    o('    private const string Data =')
    step = 120
    chunks = [blob[i:i + step] for i in range(0, len(blob), step)]
    for i, ch in enumerate(chunks):
        o('        "%s"%s' % (ch, ' +' if i + 1 < len(chunks) else ';'))
    o('}')
    sys.stdout.write('\n'.join(out) + '\n')
    sys.stderr.write('%d cases\n' % len(cases))


if __name__ == '__main__':
    main()
