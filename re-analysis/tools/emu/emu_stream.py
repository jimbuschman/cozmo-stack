"""Run the engine's own streaming layer under Unicorn, for WwiseStreamManagerTests and WwiseStreamSourceTests.

The real code runs: the stream manager (CreateDevice 0x960F34, CreateAuto 0x95F334 / 0x95F0E8), the device, the auto
stream (0x9664B8 creation, 0x964730, 0x964590, the deferred open 0x96382C / 0x963890 / 0x960B04 / 0x960BDC, Start,
Stop, SetPosition, GetBuffer, ReleaseBuffer, Query, SetHeuristics, SetMinimalBufferSize, Destroy, the completion
0x966644 / 0x964914 / 0x964A20) and the source classes (the constructors, 0xA74564, 0xA7482C, 0xA746A8, 0xA74970,
0xA74C80, 0xA74E00, 0xA7538C, 0xAB22D4, 0xAB2088, 0xAB1C04, 0xAB2BFC, 0xAB12B4, the walker 0x9CD340).

What is a stand-in (the I/O thread, C33.4): the resolver (a stub object whose vt+8 / vt+0xC fill the file descriptor),
the low-level hook (Read fills the buffer with a known pattern), the pool allocator, the mutex calls, and the transfers
(a transfer record and buffer are built here and completed through the real 0x966644). The emulated I/O thread runs only
when a scenario says so.

    python re-analysis/tools/emu/emu_stream.py [scenario ...]

The expected values in the C# tests are this script's output (M6-wwise-bank.md C33.2, C33.3), not the C#'s.
"""
import json
import struct
import sys

from emu_common import BASE, HEAP
from emu_svc import SvcEmu
from so import RAW, off
from unicorn.arm_const import *

G = 0x108D798
MGR_SLOT = 0x108D7A8
BASE_PBI = BASE + 0x5000
BASE_SRC = BASE + 0x6000
BASE_SO = BASE + 0x7000
BASE_SET = BASE + 0x8000          # device settings
BASE_RES = BASE + 0x8100          # the stub resolver
BASE_HOOK = BASE + 0x8200         # the stub low-level I/O hook
BASE_VT = BASE + 0x8300           # stub vtables (the stub code sits at +0x100.. +0x1A0, clear of the manager at 0x8400)
BASE_MGR = BASE + 0x8800          # the stream manager object (vptr 0x101BE18)
BASE_TMP = BASE + 0x9000
BASE_FD = BASE + 0xA000           # file descriptor scratch (0x20 each)
BASE_FLAGS = BASE + 0xB000
LOG = []

FILE_BYTE = lambda pos: (pos * 7 + 3) & 0xFF


class World:
    def __init__(self, file_size=1000, defer=True, device_id=0):
        e = SvcEmu()
        self.e = e
        e.std_hooks()
        self.file_size = file_size
        self.content = None             # the file's bytes (default: the pattern FILE_BYTE)
        self.defer = defer
        self.device_id = device_id
        self.callbacks = []
        self.opens = []
        for a in (0x4D6694, 0x4D66A0, 0x4D66AC, 0x4D66B8, 0x4D66C4, 0x4D6730, 0x4D6754, 0x4D66D0, 0x97CB7C):
            e.hook(a, lambda em: 0)
        e.hook(0x97CB7C, self._base_ctor_97cb7c)                       # the stream's base constructor: [S+4] = 0
        e.hook(0x4D36DC, lambda em: (em.uc.mem_write(em.reg(0), bytes([em.reg(1) & 0xFF]) * em.reg(2)), em.reg(0))[1])   # memset
        e.hook(0x4A48AC, lambda em: em.uc.mem_write(em.reg(0), bytes(em.reg(1))) or None)   # __aeabi_memclr8
        e.hook(0x4A403C, lambda em: em.uc.mem_write(em.reg(0), bytes(em.reg(1))) or None)   # __aeabi_memclr4
        e.hook(0x4D37F0, lambda em: (em.uc.mem_write(em.reg(0), bytes(em.uc.mem_read(em.reg(1), em.reg(2)))), em.reg(0))[1])   # memcpy
        e.hook(0x4A685C, self._ldivmod)
        e.hook(0x4A44E0, self._strlen)
        e.hook(0x4BB694, self._strncpy)
        e.hook(0x969E8C, lambda em: 1)                                 # the I/O memory manager's init
        e.hook(0x97CC80, lambda em: 1)                                 # the I/O thread's start
        e.hook(0x96FE70, lambda em: None)                              # return a buffer to the memory manager
        e.hook(0xA059D8, self._callback_a059d8)
        e.hook(0x4CD124, lambda em: 0)                                 # sched_get_priority_max
        e.hook(0x4CD118, lambda em: 0)                                 # sched_get_priority_min
        self.setup_globals()

    def _base_ctor_97cb7c(self, em):
        em.w32(em.reg(0) + 4, 0)
        return None

    def _strlen(self, em):
        p, n = em.reg(0), 0
        while em.r8(p + n):
            n += 1
        return n

    def _strncpy(self, em):
        d, sp, n = em.reg(0), em.reg(1), em.reg(2)
        data = bytearray()
        for i in range(n):
            b = em.r8(sp + i)
            data.append(b)
            if b == 0:
                break
        data.extend(bytes(n - len(data)))
        em.uc.mem_write(d, bytes(data))
        return d

    def _ldivmod(self, em):
        n = em.reg(0) | (em.reg(1) << 32)
        d = em.reg(2) | (em.reg(3) << 32)
        if n >= 1 << 63:
            n -= 1 << 64
        if d >= 1 << 63:
            d -= 1 << 64
        if d == 0:
            q, r = 0, 0
        else:
            q = abs(n) // abs(d) * (1 if (n < 0) == (d < 0) else -1)
            r = n - q * d
        em.uc.reg_write(UC_ARM_REG_R1, (q >> 32) & 0xFFFFFFFF)
        em.uc.reg_write(UC_ARM_REG_R2, r & 0xFFFFFFFF)
        em.uc.reg_write(UC_ARM_REG_R3, (r >> 32) & 0xFFFFFFFF)
        return q & 0xFFFFFFFF

    def _hook_libc_thunks(self):
        """Every PLT thunk of the libc / AEABI helpers the real decode code calls (memcpy / memset / memclr families and the integer divides), by name from the Ghidra index (as emu_vorbis.py does)."""
        import os
        index = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'decomp', 'libcozmoEngine', 'index.tsv')
        e = self.e
        s32 = lambda v: v - (1 << 32) if v & 0x80000000 else v

        def memcpy(em):
            if em.reg(2):
                em.uc.mem_write(em.reg(0), bytes(em.uc.mem_read(em.reg(1), em.reg(2))))
            return em.reg(0)

        def memset(em):
            if em.reg(2):
                em.uc.mem_write(em.reg(0), bytes([em.reg(1) & 0xFF]) * em.reg(2))
            return em.reg(0)

        def aeabi_memset(em):                                   # (dest, n, c)
            if em.reg(1):
                em.uc.mem_write(em.reg(0), bytes([em.reg(2) & 0xFF]) * em.reg(1))
            return None

        def memclr(em):                                         # (dest, n)
            if em.reg(1):
                em.uc.mem_write(em.reg(0), bytes(em.reg(1)))
            return None

        def idiv(em):
            a, b = s32(em.reg(0)), s32(em.reg(1))
            return 0 if b == 0 else (abs(a) // abs(b) * (1 if (a < 0) == (b < 0) else -1)) & 0xFFFFFFFF

        def idivmod(em):
            a, b = s32(em.reg(0)), s32(em.reg(1))
            if b == 0:
                return 0
            q = abs(a) // abs(b) * (1 if (a < 0) == (b < 0) else -1)
            em.uc.reg_write(UC_ARM_REG_R1, (a - q * b) & 0xFFFFFFFF)
            return q & 0xFFFFFFFF

        table = {}
        for n in ('memcpy', 'memmove', '__aeabi_memcpy', '__aeabi_memcpy4', '__aeabi_memcpy8', '__aeabi_memmove', '__aeabi_memmove4'):
            table[n] = memcpy
        table['memset'] = memset
        for n in ('__aeabi_memset', '__aeabi_memset4', '__aeabi_memset8'):
            table[n] = aeabi_memset
        for n in ('__aeabi_memclr', '__aeabi_memclr4', '__aeabi_memclr8'):
            table[n] = memclr
        table['__aeabi_idiv'] = idiv
        table['__aeabi_idivmod'] = idivmod
        for line in open(index):
            f = line.rstrip(chr(10)).split(chr(9))
            if len(f) >= 5 and f[2] == 'THUNK' and f[4].startswith('<EXTERNAL>::') and f[4][12:] in table and int(f[0], 16) < 0x1000000:
                e.hook(int(f[0], 16), table[f[4][12:]])

    def arm_fail(self, n):
        """Fail the n-th (1-based) pool allocation from now on (0xA7A7F4 or 0xA7A894)."""
        self.fail_n = n
        self.alloc_seen = 0

    def _idiv(self, em):
        a, b = em.reg(0), em.reg(1)
        a = a - (1 << 32) if a & 0x80000000 else a
        b = b - (1 << 32) if b & 0x80000000 else b
        if b == 0:
            return 0
        q = abs(a) // abs(b) * (1 if (a < 0) == (b < 0) else -1)
        return q & 0xFFFFFFFF

    def _callback_a059d8(self, em):
        info = em.reg(3)
        self.callbacks.append({'playing_id': em.reg(1), 'estimate': em.r32(info), 'flag': em.r32(info + 4)})
        return None

    # ------------------------------------------------------------------ globals, resolver, hook, device

    def setup_globals(self):
        e = self.e
        # the stub resolver: vptr -> BASE_VT, vt+8 / vt+0xC
        e.w32(BASE_RES, BASE_VT)
        e.svc_hook(BASE_VT + 0x100, self._resolver_open)
        e.w32(BASE_VT + 8, BASE_VT + 0x100)
        e.w32(BASE_VT + 0xC, BASE_VT + 0x100)
        # the stub hook: +8 Close, +0xC GetBlockSize, +0x18 Read
        e.w32(BASE_HOOK, BASE_VT + 0x40)
        e.svc_hook(BASE_VT + 0x180, self._hook_close)
        e.svc_hook(BASE_VT + 0x190, lambda em: 1)
        e.svc_hook(BASE_VT + 0x1A0, self._hook_read)
        e.w32(BASE_VT + 0x40 + 8, BASE_VT + 0x180)
        e.w32(BASE_VT + 0x40 + 0xC, BASE_VT + 0x190)
        e.w32(BASE_VT + 0x40 + 0x18, BASE_VT + 0x1A0)
        e.w32(G + 0, BASE_RES)
        e.w32(BASE_MGR, 0x101BE18)
        e.w32(G + 0x10, BASE_MGR)
        e.w32(MGR_SLOT, BASE_MGR)
        # the device
        e.call(0x96060C, BASE_SET)                                      # the real defaults
        e.w32(BASE_SET + 4, 0x180000)
        e.w8(BASE_SET + 0x30, 1)
        self.slot = e.call(0x960F34, BASE_MGR, BASE_SET, BASE_HOOK)
        self.dev = e.r32(e.r32(G + 4) + 4 * self.slot) if self.slot != 0xFFFFFFFF else 0

    def _resolver_open(self, em):
        # (this, id-or-name, mode, flags, &bSyncOpen, fileDesc): the stack arguments are at [sp] and [sp+4]
        sp = em.reg_sp()
        bsync_p = em.r32(sp)
        fd = em.r32(sp + 4)
        em.uc.mem_write(fd, bytes(0x20))
        em.w32(fd + 0x18, self.device_id)
        if em.r8(bsync_p) == 0 and self.defer:
            return 1
        em.w8(bsync_p, 1)
        em.w32(fd, self.file_size)
        self.opens.append(em.reg(1))
        return 1

    def _hook_close(self, em):
        return 1

    def _hook_read(self, em):
        # (hook, fd, heur, buf, info): info[0] = position (low), info[0xC] = size
        info = em.reg(3)
        buf = em.r32(info + 0x10)
        pos = em.r32(info)
        size = em.r32(info + 0xC)
        em.uc.mem_write(buf, self.bytes_at(pos, size))
        return 1

    # ------------------------------------------------------------------ helpers

    def alloc_zero(self, n):
        a = self.e.alloc(n)
        self.e.uc.mem_write(a, bytes(n))
        return a

    def heuristics(self, throughput_bits, loop_start=0, loop_end=0, min_bufs=0, priority=0):
        a = self.alloc_zero(0x10)
        self.e.w32(a, throughput_bits)
        self.e.w32(a + 4, loop_start)
        self.e.w32(a + 8, loop_end)
        self.e.w8(a + 0xC, min_bufs)
        self.e.w8(a + 0xD, priority)
        return a

    def flags(self, company=0, codec=0, lang=0, b11=0, cache=0xFFFFFFFF):
        a = self.alloc_zero(0x1C)
        self.e.w32(a, company)
        self.e.w32(a + 4, codec)
        self.e.w8(a + 0x10, lang)
        self.e.w8(a + 0x11, b11)
        self.e.w32(a + 0x14, cache)
        return a

    def create_auto_by_id(self, file_id, flags_ptr, heur_ptr, bs_ptr=0, sync=0):
        out = self.alloc_zero(4)
        r = self.e.call(0x95F334, BASE_MGR, file_id, flags_ptr, heur_ptr, stack=(bs_ptr, out, sync))
        iface_ptr = self.e.r32(out)
        return r, (iface_ptr - 0x30 if iface_ptr else 0)           # *out is the IAkAutoStream subobject at S+0x30

    def stream_state(self, s):
        e = self.e
        d = {
            'f10': e.r32(s + 0x10), 'f28': e.r32(s + 0x28), 'b2c': e.r8(s + 0x2C), 'b2d': e.r8(s + 0x2D),
            'p38': e.r32(s + 0x38) | (e.r32(s + 0x3C) << 32), 'f40': e.r32(s + 0x40), 'f44': e.r32(s + 0x44),
            'f48': e.r32(s + 0x48), 'f4c': e.r32(s + 0x4C), 'f50': e.r32(s + 0x50), 'f54': e.r32(s + 0x54),
            'f58': e.r32(s + 0x58), 'f5c': e.r32(s + 0x5C), 'n60': e.r32(s + 0x60), 'b6c': e.r8(s + 0x6C),
            'b6d': e.r8(s + 0x6D), 'b6e': e.r8(s + 0x6E), 'f70': e.r32(s + 0x70), 'b74': e.r8(s + 0x74),
            'rec': e.r32(s + 0xC),
        }
        # the node list
        nodes = []
        n = e.r32(s + 0x68)
        while n:
            buf = e.r32(n + 4)
            nodes.append({'start': e.r32(buf) | (e.r32(buf + 4) << 32), 'size': e.r32(buf + 0x10), 'offset': e.r32(n + 8), 'state': e.r8(n + 0xC)})
            n = e.r32(n)
        d['nodes'] = nodes
        return d

    def bytes_at(self, pos, size):
        if self.content is not None:
            return self.content[pos:pos + size].ljust(size, bytes(1))
        return bytes(FILE_BYTE(pos + i) for i in range(size))

    def new_buffer(self, start, size):
        e = self.e
        buf = self.alloc_zero(0x20)
        data = self.alloc_zero(size)
        e.uc.mem_write(data, self.bytes_at(start, size))
        e.w32(buf, start & 0xFFFFFFFF)
        e.w32(buf + 4, start >> 32)
        e.w32(buf + 8, data)
        e.w32(buf + 0x10, size)
        e.w32(buf + 0x18, 0xFFFFFFFF)
        node = self.alloc_zero(0x10)
        e.w32(node + 4, buf)
        e.w8(node + 0xC, 1)
        return node, buf, data

    def deliver(self, s, start, size):
        """What the I/O thread does for one transfer: the record goes in flight, its accounted bytes are added to [S+0x54], and the real 0x966644 completes it."""
        e = self.e
        node, buf, data = self.new_buffer(start, size)
        loop_end = e.r32(s + 0x4C)
        acc = size if (start >= loop_end) else (loop_end - start if start + size > loop_end else size)
        e.w32(s + 0x54, (e.r32(s + 0x54) + acc) & 0xFFFFFFFF)
        e.w32(s + 0x70, node)
        r = e.call(0x966644, s, node, 1, 1)
        return r, data


def out(case, **kw):
    kw['case'] = case
    print(json.dumps(kw, sort_keys=True))


# ---------------------------------------------------------------------- scenarios

def iface(s):
    return s + 0x30


def call_if(w, s, slot_impl, *args, stack=()):
    return w.e.call(slot_impl, iface(s), *args, stack=stack)


def sc_create():
    """CreateAuto by id, deferred open: the stream, its geometry and its open record (D3, D5, D6, D7)."""
    w = World(file_size=1000)
    for name, thr, ls, le, minb, prio, flags in (
        ('plain', 0x3F800000, 0, 0, 0, 0, dict(codec=1, lang=1, b11=1)),
        ('throughput_below_one', 0x3F000000, 0, 0, 0, 50, dict(codec=1)),
        ('loop_range', 0x40000000, 123, 600, 3, 0x64, dict(codec=1)),
        ('loop_end_past_file', 0x40400000, 0, 5000, 0, 1, dict(codec=0)),
        ('priority_too_high', 0x3F800000, 0, 0, 0, 0x65, dict(codec=1)),
        ('negative_throughput', 0xBF800000, 0, 0, 0, 0, dict(codec=1)),
    ):
        h = w.heuristics(thr, ls, le, minb, prio)
        f = w.flags(**flags)
        r, s = w.create_auto_by_id(77, f, h)
        d = w.stream_state(s) if s else None
        rec = None
        if s and w.e.r32(s + 0xC):
            rec_p = w.e.r32(s + 0xC)
            rec = {'id': w.e.r32(rec_p), 'mode': w.e.r32(rec_p + 0x20), 'b24': w.e.r8(rec_p + 0x24),
                   'codec': w.e.r32(rec_p + 8), 'lang': w.e.r8(rec_p + 0x14)}
        out('create_' + name, result=r, stream=d, record=rec, flag12=w.e.r8(f + 0x12))
    # a stream with an explicit buffer geometry and a missing resolver result
    w = World(file_size=1000)
    h = w.heuristics(0x3F800000)
    f = w.flags(codec=1)
    for name, bs in (('buffer_0x800', (0, 0x800, 0)), ('buffer_explicit_4000', (0x4000, 0, 0)), ('buffer_explicit_not_multiple', (0x4001, 0, 0)), ('buffer_explicit_too_large', (0x8000, 0, 0))):
        b = w.alloc_zero(0xC)
        w.e.w32(b, bs[0]); w.e.w32(b + 4, bs[1]); w.e.w32(b + 8, bs[2])
        r, s = w.create_auto_by_id(78, f, h, b)
        out('create_' + name, result=r, stream=w.stream_state(s) if s else None)
    w2 = World(file_size=0)
    r, s = w2.create_auto_by_id(79, w2.flags(codec=1), w2.heuristics(0x3F800000))
    out('create_zero_size_file', result=r, stream=w2.stream_state(s) if s else None)
    w3 = World(file_size=1000, defer=False)
    r, s = w3.create_auto_by_id(80, w3.flags(codec=1), w3.heuristics(0x3F800000))
    out('create_sync_open', result=r, stream=w3.stream_state(s) if s else None, opens=w3.opens)


def mk_stream(w, file_size=1000, loop_start=0, loop_end=0, thr=0x3F800000, bs=(0, 0, 0), open_it=True, start=True):
    h = w.heuristics(thr, loop_start, loop_end)
    f = w.flags(codec=1)
    b = 0
    if bs != (0, 0, 0):
        b = w.alloc_zero(0xC)
        w.e.w32(b, bs[0]); w.e.w32(b + 4, bs[1]); w.e.w32(b + 8, bs[2])
    r, s = w.create_auto_by_id(5, f, h, b)
    assert r == 1, r
    if start:
        w.e.call(0x964CB8, iface(s) - 0x30 + 0x30 - 0x30 + 0x30) if False else None
    return s


def start(w, s):
    return w.e.call(0x964CB8, s)


def run_open(w, s):
    """The I/O thread's open (the real 0x963890, with the stub resolver)."""
    return w.e.call(0x963890, s)


def sc_stream():
    """Start, the open, Query and GetBuffer before and after data, ReleaseBuffer, SetPosition, Stop, Destroy."""
    w = World(file_size=1000)
    e = w.e
    s = mk_stream(w)
    out('s0_created', stream=w.stream_state(s))
    # Query and GetBuffer before the stream is opened: 0x2E with the out word untouched
    q = w.alloc_zero(4); e.w32(q, 0xCAFEF00D)
    r = e.call(0x961C1C, s, q)
    out('s1_query_unopened', result=r, out_word=e.r32(q))
    bp = w.alloc_zero(4); sp_ = w.alloc_zero(4)
    r = e.call(0x965B1C, s, bp, sp_, 0)
    out('s1_getbuffer_unopened', result=r, buf=e.r32(bp), size=e.r32(sp_), stream=w.stream_state(s))
    r = start(w, s)
    out('s2_start', result=r, stream=w.stream_state(s))
    r2 = start(w, s)
    out('s2_start_again', result=r2)
    r = run_open(w, s)
    out('s3_open', result=r, stream=w.stream_state(s))
    e.w32(q, 0xCAFEF00D)
    r = e.call(0x961C1C, s, q)
    out('s4_query_opened_empty', result=r, out_word=e.r32(q))
    r = e.call(0x965B1C, s, bp, sp_, 0)
    out('s4_getbuffer_opened_empty', result=r, buf=e.r32(bp), size=e.r32(sp_))
    # deliver two buffers: [0,300) and [300,700)
    r, d1 = w.deliver(s, 0, 300)
    out('s5_deliver_0_300', result=r, stream=w.stream_state(s))
    r, d2 = w.deliver(s, 300, 400)
    out('s5_deliver_300_400', result=r, stream=w.stream_state(s))
    r = e.call(0x961C1C, s, q)
    out('s6_query_two_buffers', result=r, out_word=e.r32(q), stream=w.stream_state(s))
    r = e.call(0x965B1C, s, bp, sp_, 0)
    out('s7_getbuffer_first', result=r, buf_offset=e.r32(bp) - d1, size=e.r32(sp_), stream=w.stream_state(s))
    r = e.call(0x965B1C, s, bp, sp_, 0)
    out('s7_getbuffer_second', result=r, buf_offset=e.r32(bp) - d2, size=e.r32(sp_), stream=w.stream_state(s))
    r = e.call(0x965B1C, s, bp, sp_, 0)
    out('s7_getbuffer_none_left', result=r, buf=e.r32(bp), size=e.r32(sp_), stream=w.stream_state(s))
    r = e.call(0x964D64, s)
    out('s8_release_first', result=r, stream=w.stream_state(s))
    r = e.call(0x964D64, s)
    out('s8_release_second', result=r, stream=w.stream_state(s))
    r = e.call(0x964D64, s)
    out('s8_release_none', result=r, stream=w.stream_state(s))
    # the end of the file: deliver [700, 1000) and read it
    r, d3 = w.deliver(s, 700, 300)
    r = e.call(0x965B1C, s, bp, sp_, 0)
    out('s9_getbuffer_last_with_eof_flag', result=r, size=e.r32(sp_), stream=w.stream_state(s))
    r = e.call(0x965B1C, s, bp, sp_, 0)
    out('s9_getbuffer_after_end', result=r, stream=w.stream_state(s))
    # Stop, then Destroy
    r = e.call(0x9655B0, s)
    out('s10_stop', result=r, stream=w.stream_state(s))
    r = e.call(0x9654E4, s)
    out('s10_destroy', result=r, stream=w.stream_state(s))


def sc_position():
    """SetPosition (begin, current, end), alignment with a block size of 1, SetHeuristics (fast and heavy path), SetMinimalBufferSize."""
    w = World(file_size=1000)
    e = w.e
    s = mk_stream(w)
    start(w, s)
    run_open(w, s)
    w.deliver(s, 0, 300)
    w.deliver(s, 300, 300)
    out('p0', stream=w.stream_state(s))
    real = w.alloc_zero(8)
    for name, off_, method in (('begin_300', 300, 0), ('begin_450', 450, 0), ('current_plus_10', 10, 1), ('end_minus_0', 0, 2), ('bad_method', 0, 7), ('negative', -5 & 0xFFFFFFFF, 0)):
        ww = World(file_size=1000)
        ee = ww.e
        ss = mk_stream(ww)
        start(ww, ss)
        run_open(ww, ss)
        ww.deliver(ss, 0, 300)
        ww.deliver(ss, 300, 300)
        rp = ww.alloc_zero(8)
        hi = 0xFFFFFFFF if name == 'negative' else 0
        r = ee.call(0x9657E0, ss, 0, off_, hi, stack=(method, rp))   # r0 = S, r1 unused, r2:r3 = the 64-bit offset, [sp] = method, [sp+4] = &real
        out('pos_' + name, result=r, real=ee.r32(rp) | (ee.r32(rp + 4) << 32), stream=ww.stream_state(ss))


def sc_heur():
    """SetHeuristics on a stream with buffers: priority, throughput only (fast path), a loop range (heavy path), the loop end past the file size."""
    for name, fn in (
        ('priority_only', lambda w, s, h: None),
    ):
        pass
    results = []

    def fresh(buffers=True, granted=0):
        w = World(file_size=1000)
        s = mk_stream(w)
        start(w, s)
        run_open(w, s)
        if buffers:
            w.deliver(s, 0, 300)
            w.deliver(s, 300, 300)
            w.deliver(s, 600, 300)
        bp = w.alloc_zero(4); sp_ = w.alloc_zero(4)
        for _ in range(granted):
            w.e.call(0x965B1C, s, bp, sp_, 0)
        return w, s

    def sethf(w, s, thr, ls, le, minb=0, prio=0):
        h = w.heuristics(thr, ls, le, minb, prio)
        r = w.e.call(0x964E4C, s, h)
        return r

    for name, args, granted in (
        ('throughput_only', (0x40000000, 0, 0), 0),
        ('throughput_same_min_changed', (0x3F800000, 0, 0, 5), 0),
        ('priority_too_high', (0x3F800000, 0, 0, 0, 0x65), 0),
        ('loop_range_no_granted', (0x3F800000, 100, 500), 0),
        ('loop_range_one_granted', (0x3F800000, 100, 500), 1),
        ('loop_range_two_granted', (0x3F800000, 450, 500), 2),
        ('loop_end_past_file', (0x3F800000, 0, 5000), 1),
        ('loop_end_cleared_again', (0x3F800000, 0, 0), 1),
    ):
        w, s = fresh(granted=granted)
        before = w.stream_state(s)
        r = sethf(w, s, *args)
        out('heur_' + name, result=r, before=before, after=w.stream_state(s))
    # set a loop range, then clear it
    w, s = fresh(granted=1)
    sethf(w, s, 0x3F800000, 100, 500)
    r = sethf(w, s, 0x3F800000, 0, 0)
    out('heur_set_then_clear', result=r, after=w.stream_state(s))
    # SetMinimalBufferSize
    for name, size, gran_after in (('zero', 0, None), ('one', 1, None), ('equal_block', 1, None), ('bigger_fits', 0x1000, None), ('bigger_than_buffer', 0x5000, None)):
        w, s = fresh(buffers=False)
        try:
            r = w.e.call(0x965244, s, size)
            out('minbuf_' + name, result=r, stream=w.stream_state(s))
        except Exception as ex:
            out('minbuf_' + name, error=str(ex)[:60], stream=w.stream_state(s))


def sc_nominal():
    w = World(file_size=1000)
    e = w.e
    s = mk_stream(w, thr=0x40490FDB)       # 3.14159
    out('nominal_normal', value=e.call(0x961AD8, s), tl=e.r32(w.dev + 0x224))
    e.w8(s + 0x2D, e.r8(s + 0x2D) | 0x40)
    out('nominal_caching_stream', value=e.call(0x961AD8, s))


def run_vorbis_first(name, prefix_bytes, has_prefetch=True, loop_count=1, prio_bits=0, flags_1bd=0x44, pbi_flags4=0, src_bits0c=0x2, src_bits0d=0x02, extra=None, steps=1):
    pass


SCENARIOS = {'create': sc_create, 'stream': sc_stream, 'position': sc_position, 'heur': sc_heur, 'nominal': sc_nominal}



# ---------------------------------------------------------------------- the WAVE walker 0x9CD340 (for validating the test double of the seam)

ZIP = None


def zip_entry(name):
    """The bytes of a stored entry of the shipped AudioAssets.zip (re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip)."""
    import zipfile, os
    global ZIP
    p = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'obb', 'assets', 'cozmo_resources', 'sound', 'AudioAssets.zip')
    if ZIP is None:
        ZIP = zipfile.ZipFile(p)
    return ZIP.read(name)


def run_walker(data, length=None):
    w = World(file_size=len(data))
    e = w.e
    length = len(data) if length is None else length
    mem = w.alloc_zero(len(data) + 16)
    e.uc.mem_write(mem, data)
    so = w.alloc_zero(0x100)
    fmtinfo = w.alloc_zero(8)
    extra = w.alloc_zero(8)
    # 0x9CD340(data, len, &fmtinfo, S+0x2C, [sp]=S+0x24, [sp+4]=S+0x28, [sp+8]=S+0x1C, [sp+0xC]=S+0x20, [sp+0x10]=&extra, [sp+0x14]=0)
    r = e.call(0x9CD340, mem, length, fmtinfo, so + 0x2C, stack=(so + 0x24, so + 0x28, so + 0x1C, so + 0x20, extra, 0))
    return {
        'result': r, 'fmt_size': e.r32(fmtinfo), 'fmt_off': (e.r32(fmtinfo + 4) - mem) if e.r32(fmtinfo + 4) else None,
        'w24': e.r32(so + 0x24), 'w28': e.r32(so + 0x28), 'data_size': e.r32(so + 0x1C), 'data_off': e.r32(so + 0x20),
        'extra': e.r32(extra), 'w2c': [e.r32(so + 0x2C + 4 * i) for i in range(4)],
    }


def sc_walker():
    import struct as st
    for name in ('998061257.wem', '1000323750.wem', 'English(US)/99908739.wem', 'English(US)/1000221881.wem', 'English(US)/1013342711.wem'):
        d = zip_entry(name)
        tag = st.unpack_from('<H', d, 20)[0]
        for n in (len(d), 600, 200, 90):
            out('walker', name=name, length=n, tag=hex(tag), **run_walker(d[:n], n))


def sc_walker_degenerate():
    """The walker on the inputs of a stream that has no data (the second StartStream call of a stream whose first call failed before Start)."""
    w = World(file_size=10)
    e = w.e
    so = w.alloc_zero(0x100)
    fmtinfo = w.alloc_zero(8)
    extra = w.alloc_zero(8)
    zeros16 = w.alloc_zero(16)
    for name, ptr, ln in (('null_pointer_length_0', 0, 0), ('null_pointer_length_50', 0, 50), ('valid_pointer_length_0', zeros16, 0), ('zeros_length_11', zeros16, 11)):
        r = e.call(0x9CD340, ptr, ln, fmtinfo, so + 0x2C, stack=(so + 0x24, so + 0x28, so + 0x1C, so + 0x20, extra, 0))
        out('walker_degenerate', name=name, result=r)


SCENARIOS['walker'] = sc_walker
SCENARIOS['walker_degenerate'] = sc_walker_degenerate


# ---------------------------------------------------------------------- the source classes

import zlib

CODEBOOK_RECORD = BASE + 0xC000


class SrcWorld(World):
    """A World with a PBI, a source block and a source object built by the engine's own constructor."""

    def __init__(self, wem, prefix_len, cls='vorbis', file_size=None, loop_count=1, flags_1bd=0x44, flags4=0, priority_bits=0x3F800000,
                 src_bits0c=0x03, src_bits0d=0x02, plugin=0x00040001, name_ptr=0, source_id=99908739, real_decode=False, parse_setup=True):
        super().__init__(file_size=len(wem) if file_size is None else file_size)
        e = self.e
        self.content = wem
        self.cls = cls
        self.pbi = BASE_PBI
        self.src = BASE_SRC
        self.so = BASE_SO
        e.uc.mem_write(BASE_PBI, bytes(0x200))
        e.uc.mem_write(BASE_SRC, bytes(0x40))
        e.uc.mem_write(BASE_SO, bytes(0x100))
        e.w32(self.pbi + 4, flags4)
        e.w32(self.pbi + 0x140, 0x1234)
        e.w32(self.pbi + 0x150, self.src)
        e.w16(self.pbi + 0x1B8, loop_count)
        e.w8(self.pbi + 0x1BD, flags_1bd)
        e.w32(self.pbi + 0x1C0, priority_bits)
        e.w32(self.src + 4, source_id)
        e.w8(self.src + 0xC, src_bits0c)
        e.w8(self.src + 0xD, src_bits0d)
        e.w32(self.src + 0x10, name_ptr)
        e.w32(self.src + 0x14, plugin)
        self.prefix_mem = 0
        self.prefix_end = 0
        self.out_fail = False
        if prefix_len:
            self.prefix_mem = self.alloc_zero(prefix_len + 16)
            self.prefix_end = self.prefix_mem + prefix_len + 16
            e.uc.mem_write(self.prefix_mem, wem[:prefix_len])
            e.w32(self.pbi + 0x1DC, self.prefix_mem)
            e.w32(self.pbi + 0x1E0, prefix_len)
        self.setups = []
        self.out_buf_calls = []
        self.real_decode = real_decode
        if real_decode:
            # batch 5e: the engine's own setup cache 0xAB2D74, decoder-state allocation 0xAB3264, frame loop 0xAB7E40 and output hand-off 0xA73490 run (only the pool allocator and the divide stubs are the harness's);
            # the setup parse inside the cache runs for real unless parse_setup is False (0xAB6380 / 0xAB63E0 then return 0: the cache logic is what the scenario is about)
            self._hook_libc_thunks()
            self.fail_n = 0
            self.alloc_seen = 0
            self.alloc_log = []

            def maybe_fail():
                if not self.fail_n:
                    return False
                self.alloc_seen += 1
                if self.alloc_seen == self.fail_n:
                    self.fail_n = 0
                    return True
                return False

            def aligned(em):
                self.alloc_log.append(('aligned', em.reg(1)))
                return 0 if maybe_fail() else em.alloc(em.reg(1))

            def plain(em):
                self.alloc_log.append(('plain', em.reg(1)))
                return 0 if maybe_fail() else em.alloc(em.reg(1))
            e.hook(0xA7A894, aligned)
            e.hook(0xA7A7F4, plain)
            if not parse_setup:
                e.hook(0xAB6380, lambda em: 0)
                e.hook(0xAB63E0, lambda em: 0)
            # log the setup cache's and the decoder state allocation's arguments without replacing them
            from unicorn import UC_HOOK_CODE

            def log_cache(uc, addr, size, _):
                blk = e.reg(3)
                ptr, sz, flag = e.r32(blk), e.r32(blk + 4), e.r8(blk + 8)
                data = bytes(uc.mem_read(ptr, sz))
                self.setups.append({'size': sz, 'flag': flag, 'crc': zlib.crc32(data), 'in_prefix': self.prefix_mem != 0 and self.prefix_mem <= ptr < self.prefix_end})
            e.uc.hook_add(UC_HOOK_CODE, log_cache, begin=0xAB2D74, end=0xAB2D74)
            e.uc.hook_add(UC_HOOK_CODE, lambda uc, a, s_, _: self.out_buf_calls.append(e.reg(1) & 0xFF), begin=0xAB3264, end=0xAB3264)
        else:
            e.hook(0xAB2D74, self._codebook_cache)
            e.hook(0xAB3264, self._out_buffers)
        e.w32(CODEBOOK_RECORD, 0xC0DE)
        ctor = {'vorbis': 0xAB1B38, 'inmem': 0xAB0930, 'pcm': 0xA76140}.get(cls, 0xA74244)
        e.call(ctor, self.so, self.pbi)
        self.start_fn = 0xAB22D4 if cls == 'vorbis' else 0xA7538C

    def _codebook_cache(self, em):
        blk = em.reg(3)
        ptr, size, flag = em.r32(blk), em.r32(blk + 4), em.r8(blk + 8)
        data = bytes(em.uc.mem_read(ptr, size))
        self.setups.append({'size': size, 'flag': flag, 'crc': zlib.crc32(data), 'in_prefix': self.prefix_mem != 0 and self.prefix_mem <= ptr < self.prefix_end})
        return CODEBOOK_RECORD

    def _out_buffers(self, em):
        self.out_buf_calls.append(em.reg(1) & 0xFF)
        return 0xFFFFFFFF if self.out_fail else 0

    def start(self):
        return self.e.call(self.start_fn, self.so)

    def source_state(self):
        e = self.e
        so = self.so

        def ptr_off(a):
            if not a:
                return 0
            if self.prefix_mem and self.prefix_mem <= a < self.prefix_end:
                return a - self.prefix_mem
            return 'buf'
        d = {
            'f10': e.r8(so + 0x10), 'f14': e.r32(so + 0x14), 'f18': e.r32(so + 0x18), 'f1c': e.r32(so + 0x1C), 'f20': e.r32(so + 0x20),
            'f24': e.r32(so + 0x24), 'f28': e.r32(so + 0x28), 'h38': e.r16(so + 0x38), 'stream': 1 if e.r32(so + 0x3C) else 0,
            'f40': ptr_off(e.r32(so + 0x40)),
            'f44': e.r32(so + 0x44), 'f48': e.r32(so + 0x48), 'f4c': e.r32(so + 0x4C), 'f50': e.r32(so + 0x50), 'f54': e.r32(so + 0x54), 'f58': e.r32(so + 0x58),
            'h5c': e.r16(so + 0x5C), 'b5e': e.r8(so + 0x5E) & 0x1F,
            'pbi158': e.r32(self.pbi + 0x158), 'pbi15c': e.r32(self.pbi + 0x15C), 'pbi160': e.r8(self.pbi + 0x160), 'pbi161': e.r8(self.pbi + 0x161),
            'pbi162': e.r8(self.pbi + 0x162), 'pbi1b4': e.r32(self.pbi + 0x1B4), 'pbi1bd': e.r8(self.pbi + 0x1BD), 'pbi1be': e.r8(self.pbi + 0x1BE),
        }
        if self.cls == 'vorbis':
            d.update({'v68': e.r32(so + 0x68), 'v80': e.r32(so + 0x80), 'va8': e.r32(so + 0xA8), 've0': e.r32(so + 0xE0), 've8': e.r32(so + 0xE8),
                      'vf0': e.r32(so + 0xF0), 'vf4': e.r32(so + 0xF4), 'vf8': e.r8(so + 0xF8), 'l2c': e.r16(so + 0x70 + 0x2C), 'l2e': e.r16(so + 0x70 + 0x2E),
                      'vorb': bytes(e.uc.mem_read(so + 0xB8, 0x26)).hex()})
        d['setups'] = list(self.setups)
        d['callbacks'] = list(self.callbacks)
        d['out_buffers'] = list(self.out_buf_calls)
        s3c = e.r32(so + 0x3C)
        d['str'] = self.stream_state(s3c - 0x30) if s3c else None
        return d

    def io_open_and_deliver(self, start, size):
        s3c = self.e.r32(self.so + 0x3C)
        s = s3c - 0x30
        self.e.call(0x963890, s)
        return self.deliver(s, start, size)


def vorbis_wem(name='English(US)/99908739.wem'):
    return zip_entry(name)


def sc_vorbis():
    wem = vorbis_wem()
    need = 347
    for name, plen, kw in (
        ('prefix_covers', 600, {}),
        ('prefix_exact', need, {}),
        ('prefix_one_short', need - 1, {}),
        ('prefix_header_only', 94, {}),
        ('prefix_tiny', 40, {}),
        ('loop_infinite', 600, dict(loop_count=0)),
        ('loop_three', 600, dict(loop_count=3)),
        ('priority_7_5', 600, dict(priority_bits=0x40F00000)),
        ('callback_bit', 600, dict(flags4=0x400000)),
        ('prefetch_bit_clear', 600, dict(src_bits0c=0x01)),
    ):
        w = SrcWorld(wem, plen, **kw)
        r = w.start()
        out('vorbis_' + name, result=r, state=w.source_state())
    # a second call after the first returned 0x3F (the voice pass retries): no I/O has run
    for name, plen in (('retry_no_io_one_short', need - 1), ('retry_no_io_tiny', 40)):
        w = SrcWorld(wem, plen)
        r1 = w.start()
        r2 = w.start()
        r3 = w.start()
        out('vorbis_' + name, results=[r1, r2, r3], state=w.source_state())
    # the I/O thread runs between the calls: the open, then the data from the end of the prefix
    for name, plen in (('retry_after_io_one_short', need - 1), ('retry_after_io_tiny', 40)):
        w = SrcWorld(wem, plen)
        r1 = w.start()
        w.io_open_and_deliver(plen, 4096)
        r2 = w.start()
        r3 = w.start()
        out('vorbis_' + name, results=[r1, r2, r3], state=w.source_state())
    # no prefetch: 0x3F until the data arrives, then the whole header from the first buffer
    for name, kw in (('noprefetch', {}), ('noprefetch_loop_infinite', dict(loop_count=0)), ('noprefetch_gate_off', dict(flags_1bd=0x04))):
        w = SrcWorld(wem, 0, **kw)
        r1 = w.start()
        st1 = w.source_state()
        r1b = w.start()
        w.io_open_and_deliver(0, 4096)
        r2 = w.start()
        st2 = w.source_state()
        r3 = w.start()
        r4 = w.start()
        out('vorbis_' + name, results=[r1, r1b, r2, r3, r4], state_after_first=st1, state=st2, state_end=w.source_state())
    # the third call of the no-prefetch path with the gate: data below / above the nominal buffering
    w = SrcWorld(wem, 0)
    w.start()
    w.io_open_and_deliver(0, 400)
    r2 = w.start()
    out('vorbis_noprefetch_small_buffer', results=[r2], state=w.source_state())
    for k in range(3):
        out('vorbis_noprefetch_small_buffer_call%d' % (k + 3), result=w.start(), state=w.source_state())


SCENARIOS['vorbis'] = sc_vorbis


# ---------------------------------------------------------------------- the PCM / ADPCM stream class (F7)

def sc_adpcm():
    wem = zip_entry('998061257.wem')
    for name, plen, kw in (
        ('prefix', 600, {}),
        ('prefix_loop_infinite', 600, dict(loop_count=0)),
        ('noprefetch', 0, {}),
        ('noprefetch_gate_off', 0, dict(flags_1bd=0x04)),
        ('prefetch_bit_clear', 600, dict(src_bits0c=0x01)),
        ('start_offset_flag', 600, dict(flags_1bd=0xC4)),
    ):
        w = SrcWorld(wem, plen, cls='adpcm', plugin=0x00020001, source_id=998061257, src_bits0c=kw.pop('src_bits0c', 0x02), **kw)
        r1 = w.start()
        st1 = w.source_state()
        results = [r1]
        if plen == 0 or name.startswith('prefetch_bit'):
            w.io_open_and_deliver(0, 4096)
            results.append(w.start())
            results.append(w.start())
            results.append(w.start())
        out('adpcm_' + name, results=results, state_after_first=st1, state=w.source_state())
    # the callback block and the bit-1 gate with a small buffer
    w = SrcWorld(wem, 0, cls='adpcm', plugin=0x00020001, source_id=998061257, src_bits0c=0x02, flags4=0x400000)
    r = [w.start()]
    w.io_open_and_deliver(0, 400)
    r += [w.start(), w.start()]
    out('adpcm_callback', results=r, state=w.source_state())


SCENARIOS['adpcm'] = sc_adpcm


def pcm_wem(tag=0xFFFE, channels=2, rate=22050, block_align=4, bits=16, data_bytes=4000, smpl=None, mask=0x3, extra_chunks=()):
    """A hand-built PCM RIFF (no shipped bank has a PCM source): WAVE_FORMAT_EXTENSIBLE fmt (40 bytes: +0xC block align, +0xE bits, +0x14 the channel mask), optional smpl loop, data."""
    fmt = struct.pack('<HHIIHHHHI', tag, channels, rate, rate * block_align, block_align, bits, 22, bits, mask) + bytes(16)
    chunks = [(b'fmt ', fmt)]
    if smpl:
        chunks.append((b'smpl', struct.pack('<IIIIIIIII', 0, 0, 0, 60, 0, 0, 0, 1, 0) + struct.pack('<IIIIII', 0, 0, smpl[0], smpl[1], 0, 0)))
    for c in extra_chunks:
        chunks.append(c)
    chunks.append((b'data', bytes((i * 3) & 0xFF for i in range(data_bytes))))
    body = b'WAVE'
    for tg, payload in chunks:
        body += tg + struct.pack('<I', len(payload)) + payload + (b'\0' if len(payload) & 1 else b'')
    return b'RIFF' + struct.pack('<I', len(body)) + body


def sc_pcm():
    """The PCM stream class (0xA76140; vt+0x78 = 0xA75BC4): the header parse and StartStream with a prefix that covers the whole hand-built file (C36.2 A06)."""
    for name, wem, kw in (
        ('basic', pcm_wem(), {}),
        ('smpl_loop_three_passes', pcm_wem(smpl=(100, 899)), dict(loop_count=3)),
        ('smpl_loop_infinite', pcm_wem(smpl=(100, 899)), dict(loop_count=0)),
        ('smpl_loop_but_single_pass', pcm_wem(smpl=(100, 899)), dict(loop_count=1)),
        ('no_smpl_three_passes', pcm_wem(), dict(loop_count=3)),
        ('wrong_tag', pcm_wem(tag=2), {}),
        ('loop_end_beyond_data', pcm_wem(smpl=(100, 5000)), dict(loop_count=3)),
        ('loop_end_below_start', pcm_wem(smpl=(800, 700)), dict(loop_count=3)),
        ('mono_8bit', pcm_wem(channels=1, block_align=1, bits=8, data_bytes=1001, mask=0x4), {}),
        ('stereo_24bit_block_align_6', pcm_wem(block_align=6, bits=24, data_bytes=3000), {}),
        ('block_align_1024', pcm_wem(block_align=1024, bits=16, data_bytes=8192), {}),
    ):
        w = SrcWorld(wem, len(wem), cls='pcm', plugin=0x00010001, source_id=998061257, src_bits0c=0x02, **kw)
        r = w.start()
        out('pcm_' + name, results=[r], state=w.source_state(), blob=wem.hex(), loop=kw.get('loop_count', 1))


SCENARIOS['pcm'] = sc_pcm


def sc_vorbis_gate():
    wem = vorbis_wem()
    # the gate after the header: the buffered amount against the nominal buffering (12.971 bytes/ms * 380 ms = 4928)
    for name, size1, extra in (('below', 4000, []), ('above', 6000, []), ('two_buffers', 4000, [(4000, 4000)])):
        w = SrcWorld(wem, 0, flags4=0)
        w.start()
        w.io_open_and_deliver(0, size1)
        for (st, sz) in extra:
            s = w.e.r32(w.so + 0x3C) - 0x30
            w.deliver(s, st, sz)
        results = [w.start(), w.start(), w.start()]
        out('vorbis_gate_' + name, results=results, state=w.source_state())
    w = SrcWorld(wem, 0, flags4=0x400000)
    w.start()
    w.io_open_and_deliver(0, 6000)
    results = [w.start(), w.start()]
    out('vorbis_gate_callback', results=results, state=w.source_state())
    # the voice pass retry with the data arriving in two reads: the first call returns 0x3F (the prefix is one byte short), the I/O thread delivers 2 x 4096 bytes from the end of the prefix, the second call passes
    w = SrcWorld(wem, 346)
    r1 = w.start()
    s = w.e.r32(w.so + 0x3C) - 0x30
    w.io_open_and_deliver(346, 4096)
    w.deliver(s, 346 + 4096, 4096)
    r2 = w.start()
    out('vorbis_retry_after_io_two_buffers', results=[r1, r2], state=w.source_state())
    # a cleared output-buffer allocation fails the state-3 action
    w = SrcWorld(wem, 600)
    w.out_fail = True
    out('vorbis_output_buffers_fail', result=w.start(), state=w.source_state())


SCENARIOS['vorbis_gate'] = sc_vorbis_gate


def sc_vorbis_gate7():
    """The buffering gate at the head of the decode body 0xAB1550(S, state): whether the decode is reached and the code stored in [state+0x28] when it is not."""
    wem = vorbis_wem()
    for name, size, kw, mutate in (
        ('below_nominal', 4000, {}, None),
        ('above_nominal', 6000, {}, None),
        ('below_nominal_callback', 4000, dict(flags4=0x400000), None),
        ('above_nominal_callback', 6000, dict(flags4=0x400000), None),
        ('gate_clear', 4000, dict(flags_1bd=0x04), None),
        ('gate_clear_callback', 4000, dict(flags_1bd=0x04, flags4=0x400000), None),
        ('error_status', 4000, {}, 'kill'),
        ('error_status_callback', 4000, dict(flags4=0x400000), 'kill'),
        ('unopened', 0, {}, 'unopened'),
    ):
        w = SrcWorld(wem, 0, **kw)
        w.start()
        if mutate != 'unopened':
            w.io_open_and_deliver(0, size)
            w.start()
        s = w.e.r32(w.so + 0x3C) - 0x30
        if mutate == 'kill':
            w.e.call(0x962028, s)
        reached = []
        w.e.hook(0xAB7E40, lambda em: reached.append(1) or None)
        w.e.hook(0xA73490, lambda em: None)
        state = w.alloc_zero(0x40)
        w.e.w32(state + 0x28, 0xABCD)
        # the decode loop of 0xAB1550 reads [S+0x64] after 0xAB7E40: 0x2D ends it
        w.e.w32(w.so + 0x64, 0x2D)
        before_cb = len(w.callbacks)
        w.e.call(0xAB1550, w.so, state)
        out('vorbis_gate7_' + name, reached=1 if reached else 0, code=w.e.r32(state + 0x28), f10=w.e.r8(w.so + 0x10), callbacks=[(c['playing_id'], c['estimate'], c['flag']) for c in w.callbacks[before_cb:]])


SCENARIOS['vorbis_gate7'] = sc_vorbis_gate7


def sc_create_args():
    """0xA74564's heuristics and flags as the open record carries them, for a by-id and a by-name source and the cache-id rule."""
    wem = vorbis_wem()
    for name, kw in (
        ('by_id', dict(src_bits0d=0x02)),
        ('by_id_cache_minus_one_bit0', dict(src_bits0d=0x03)),
        ('by_id_cache_minus_one_bit3', dict(src_bits0d=0x0A)),
        ('by_id_lang_clear', dict(src_bits0c=0x02, src_bits0d=0x02)),
        ('by_name', dict(src_bits0d=0x06)),
        ('priority_255', dict(priority_bits=0x437F0000)),
        ('priority_negative', dict(priority_bits=0xC0A00000)),
    ):
        name_ptr = 0
        w = None
        kw2 = dict(kw)
        if name == 'by_name':
            tmp = World()
        w = SrcWorld(wem, 600, **kw2)
        if name == 'by_name':
            np_ = w.alloc_zero(16)
            w.e.uc.mem_write(np_, b'snd/test.wem\x00')
            w.e.w32(w.src + 0x10, np_)
        r = w.start()
        s3c = w.e.r32(w.so + 0x3C)
        if not s3c:
            out('create_args_' + name, result=r, stream=None)
            continue
        s = s3c - 0x30
        e = w.e
        rec = e.r32(s + 0xC)
        flags = {'company': e.r32(rec + 4), 'codec': e.r32(rec + 8), 'lang': e.r8(rec + 0x14), 'b11': e.r8(rec + 0x15), 'b12': e.r8(rec + 0x16), 'cache': e.r32(rec + 0x18), 'prefetch': e.r32(rec + 0x1C)}
        nm = None
        if name == 'by_name':
            nm = bytes(e.uc.mem_read(e.r32(rec), 16)).split(b'\0')[0].decode() if e.r8(rec + 0x24) & 1 else None
        out('create_args_' + name, result=r, prio=e.r8(s + 0x2C), cache40=e.r32(s + 0x40), b24=e.r8(rec + 0x24), flags=flags, mode=e.r32(rec + 0x20), rec0=e.r32(rec) if name != 'by_name' else 0, name=nm,
            heur_b6d=e.r8(s + 0x6D), f44=e.r32(s + 0x44), f58=e.r32(s + 0x58))


SCENARIOS['create_args'] = sc_create_args


def sc_inflight():
    """A transfer in flight: cancellation (vt+0x78), the seek (vt+0x7C), the next position (vt+0x74), vt+8, and the completion of a failed transfer."""
    def fresh(loop_start=0, loop_end=0):
        w = World(file_size=1000)
        s = mk_stream(w, loop_start=loop_start, loop_end=loop_end)
        start(w, s)
        run_open(w, s)
        node, buf, data = w.new_buffer(200, 300)
        acc = 300
        loop = w.e.r32(s + 0x4C)
        if loop:
            acc = 300 if 200 >= loop else (loop - 200 if 200 + 300 > loop else 300)
        w.e.w32(s + 0x54, w.e.r32(s + 0x54) + acc)
        w.e.w32(s + 0x70, node)
        return w, s, node

    def node_part(w, node):
        return {'state': w.e.r8(node + 0xC) & 7, 'offset': w.e.r32(node + 8)}

    for name, ls, le in (('no_loop', 0, 0), ('loop_end_400', 0, 400), ('loop_end_100', 0, 100)):
        w, s, node = fresh(ls, le)
        before = w.stream_state(s)
        nxt = w.e.call(0x9666E8, s) | (w.e.r32(0) * 0)
        r1 = w.e.call(0x965F5C, s)
        w.e.call(0x96672C, s)
        out('inflight_cancel_' + name, next_position=nxt, destroyable_while_in_flight=r1, node=node_part(w, node), after=w.stream_state(s), destroyable_after=w.e.call(0x965F5C, s))
    for name, pos in (('equal_to_the_buffer_end', 500), ('equal_to_the_buffer_start', 200), ('elsewhere', 100)):
        w, s, node = fresh()
        w.e.call(0x9667C0, s, 0, pos, 0)
        out('inflight_seek_' + name, node=node_part(w, node), after=w.stream_state(s))
    for name, result in (('success', 1), ('error', 2), ('other', 0x35)):
        w, s, node = fresh()
        r = w.e.call(0x966644, s, node, result, 1)
        out('inflight_complete_' + name, result=r, node=node_part(w, node), after=w.stream_state(s))
    w, s, node = fresh()
    w.e.w8(node + 0xC, 2)                                         # a cancelled record completing with success is released, not appended
    r = w.e.call(0x966644, s, node, 1, 1)
    out('inflight_complete_cancelled_state', result=r, after=w.stream_state(s))
    w, s, node = fresh()
    w.e.w32(s + 0x70, 0)                                          # no transfer in flight when the completion arrives
    r = w.e.call(0x966644, s, node, 1, 1)
    out('inflight_complete_not_in_flight', result=r, after=w.stream_state(s))
    # a flagged stream cannot be destroyed while a transfer is in flight, and [S+0x74] keeps it alive after a cancellation
    w, s, node = fresh()
    w.e.call(0x9654E4, s)
    out('inflight_destroy_flagged', can_destroy=w.e.call(0x965F5C, s), after=w.stream_state(s))


SCENARIOS['inflight'] = sc_inflight


def sc_prepare():
    """The loop wrap of the transfer preparation 0x966BCC (0x966C4C..0x966C74): the real function runs up to 0x966F04 (the buffer allocation after the cache branch, unread), where the position (r8:sb) and the size ([sp+0x38]) are recorded."""
    from unicorn import UC_HOOK_CODE
    for name, ls, le, delivered_end in (('no_loop', 0, 0, 500), ('below_loop_end', 100, 400, 300), ('at_loop_end', 100, 400, 400), ('past_loop_end', 100, 400, 450),
                                        ('loop_start_zero', 0, 400, 400), ('at_file_end_no_loop', 0, 0, 1000)):
        w = World(file_size=1000)
        s = mk_stream(w, loop_start=ls, loop_end=le)
        start(w, s)
        run_open(w, s)
        e = w.e
        if le:
            e.call(0x964E4C, s, w.heuristics(0x3F800000, ls, le))     # the loop range (the heavy path of SetHeuristics) sets [S+0x48] / [S+0x4C]
        w.deliver(s, 0, delivered_end)
        rec = {}

        def stop_hook(uc, address, size, _, rec=rec):
            sp = uc.reg_read(UC_ARM_REG_SP)
            rec['pos'] = uc.reg_read(UC_ARM_REG_R8) | (uc.reg_read(UC_ARM_REG_SB) << 32)
            rec['size'] = struct.unpack('<I', bytes(uc.mem_read(sp + 0x38, 4)))[0]
            uc.emu_stop()
        h = e.uc.hook_add(UC_HOOK_CODE, stop_hook, begin=0x966F04, end=0x966F04)
        out_fd = w.alloc_zero(4); out_a = w.alloc_zero(4); out_b = w.alloc_zero(4)
        nxt = e.call(0x9666E8, s)
        r = e.call(0x966BCC, s, out_fd, out_a, out_b, stack=(0,) * 0 + (0,) * 0 + tuple([0] * 0))
        e.uc.hook_del(h)
        out('prepare_' + name, next_position=nxt, loop_start=e.r32(s + 0x48), loop_end=e.r32(s + 0x4C), buffer_size=e.r32(s + 0x50), reached=bool(rec), result=r, **rec)


SCENARIOS['prepare'] = sc_prepare


if __name__ == '__main__':
    names = sys.argv[1:] or list(SCENARIOS)
    for n in names:
        SCENARIOS[n]()
