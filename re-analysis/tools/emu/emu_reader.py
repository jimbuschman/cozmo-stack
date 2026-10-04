"""Run the engine's bank reader under Unicorn, for WwiseBankReaderTests (M6-025 K17, K18, K19, C34.4).

The real functions: reset 0x9BBB04, heuristics 0x9BBB5C, memory setup 0x9BBB90, release 0x9BBBA4, temporary free 0x9BBBE0, read 0x9BBC14, raw read 0x9BBF64, window pointer 0x9BBE5C and skip 0x9BBF9C on the reader R = BM+4,
in memory mode and in stream mode. Python stand-ins (not in the C34.4 slice): the stream object's vtable (+0x1C read, +0x34 wait, +0x28 skip, +0x24 status, +8 release), the aligned allocator 0xA7A894 and the free 0xA7A914, memcpy.
The expected values in WwiseBankReaderTests are this script's output, not the C#'s:

    python re-analysis/tools/emu/emu_reader.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseBankReaderOracle.cs
"""
import struct
from emu_common import *
from unicorn.arm_const import *

R = BASE + 0x1004                 # R = BM + 4
WINDOW = BASE + 0x8000
MEMORY = BASE + 0x9000
STREAM_VT = BASE + 0x41000
STREAM_OBJ = BASE + 0x42000
VT_READ, VT_WAIT, VT_SKIP, VT_STATUS, VT_REL = [BASE + 0x43000 + 0x10 * i for i in range(5)]
DST = BASE + 0x20000


def pattern(n, seed=3):
    return bytes((i * 7 + seed) & 0xFF for i in range(n))


class Rig:
    def __init__(self, cfg):
        self.cfg = cfg
        e = Emu()
        e.std_hooks()
        self.e = e
        e.uc.mem_write(R, bytes(0x40))
        e.w32(R + 0, WINDOW)
        e.w32(R + 0xC, cfg.get('mindirect', 0x40))
        e.w32(R + 0x10, cfg.get('block', 0x10))
        e.w32(R + 0x20, bits(cfg.get('throughput', 2.0)))
        e.w8(R + 0x24, cfg.get('priority', 50) & 0xFF)
        e.hook(0x4D37F0, lambda emu: emu.uc.mem_write(emu.reg(0), bytes(emu.uc.mem_read(emu.reg(1), emu.reg(2)))) or emu.reg(0))
        self.log = []
        self.frees = []
        self.alloc_calls = 0
        self.temp = 0

        def alloc_aligned(emu):
            self.alloc_calls += 1
            self.log.append('alloc')
            if cfg.get('alloc_fail') == self.alloc_calls:
                return 0
            a = e.alloc(emu.reg(1) + 0x80)
            self.temp = (a + 0x1F) & ~0x1F
            return self.temp
        e.hook(0xA7A894, alloc_aligned)
        e.hook(0xA7A914, lambda emu: self.log.append('free') or 1)
        if cfg.get('memory') is not None:
            e.uc.mem_write(MEMORY, cfg['memory'])
            e.call(0x9BBB90, R, MEMORY, len(cfg['memory']))
        else:
            self.stream_data = pattern(cfg.get('stream_len', 300))
            self.pos = 0
            self.reads = 0
            self.waits = 0
            self.skips = 0
            e.w32(STREAM_OBJ, STREAM_VT)
            for slot, fn in ((0x1C, VT_READ), (0x34, VT_WAIT), (0x28, VT_SKIP), (0x24, VT_STATUS), (8, VT_REL)):
                e.w32(STREAM_VT + slot, fn)
            e.w32(R + 0x1C, STREAM_OBJ)
            e.hook(VT_READ, self.vt_read)
            e.hook(VT_WAIT, self.vt_wait)
            e.hook(VT_SKIP, self.vt_skip)
            e.hook(VT_STATUS, self.vt_status)
            e.hook(VT_REL, lambda emu: self.log.append('rel') or 0)
            e.call(0x9BBB04, R)

    def vt_read(self, emu):
        sp = emu.reg_sp()
        dst, nbytes = emu.reg(1), emu.reg(2)
        prio = struct.unpack('<b', bytes([self.e.r32(sp) & 0xFF]))[0]
        deadline = self.e.r32(sp + 4)
        actual_ptr = self.e.r32(sp + 8)
        self.reads += 1
        self.log.append('r1C:%d:%08x:%d:f%d' % (nbytes, deadline, prio, emu.reg(3)))
        if self.cfg.get('read_fail') == self.reads:
            return 9
        n = min(nbytes, len(self.stream_data) - self.pos)
        cap = self.cfg.get('read_cap')
        if cap is not None:
            n = min(n, cap)
        self.e.uc.mem_write(dst, self.stream_data[self.pos:self.pos + n])
        self.pos += n
        self.e.w32(actual_ptr, n)
        return 1

    def vt_wait(self, emu):
        self.waits += 1
        self.log.append('wait')
        return 5 if self.cfg.get('wait_fail') == self.waits else 1

    def vt_skip(self, emu):
        sp = emu.reg_sp()
        n = emu.reg(2)
        actual_ptr = self.e.r32(sp + 4)
        self.skips += 1
        self.log.append('s28:%d' % n)
        if self.cfg.get('skip_fail') == self.skips:
            return 9
        got = min(n, len(self.stream_data) - self.pos)
        cap = self.cfg.get('skip_cap')
        if cap is not None:
            got = min(got, cap)
        self.pos += got
        self.e.w32(actual_ptr, got)
        return 1

    def vt_status(self, emu):
        self.log.append('st')
        self.e.w8(emu.reg(1), self.cfg.get('status', 0))
        return 0

    def state(self):
        e = self.e
        left = e.r32(R + 8)
        cur = e.r32(R + 4) - WINDOW
        mem = e.r32(R + 0x18)
        stream = e.r32(R + 0x1C)
        temp = e.r32(R + 0x14)
        return 'left=%d cur=%d mem=%s stream=%d temp=%d' % (left, cur if self.cfg.get('memory') is None else 0, (mem - MEMORY) if mem else -1, 1 if stream else 0, 1 if temp else 0)


def step(rig, op):
    e = rig.e
    kind, *args = op.split(':')
    rig.log.clear()
    out = ''
    if kind == 'read':
        n = int(args[0])
        e.uc.mem_write(DST, bytes([0xEE]) * (n + 4))
        e.w32(DST + 0x800, 0xAAAAAAAA)
        r = e.call(0x9BBC14, R, DST, n, DST + 0x800)
        got = e.r32(DST + 0x800)
        out = 'r=%d got=%d data=%s' % (r, got, bytes(e.uc.mem_read(DST, got)).hex() if got else '')
    elif kind == 'raw':
        n = int(args[0])
        e.uc.mem_write(DST, bytes([0xEE]) * (n + 4))
        r = e.call(0x9BBF64, R, DST, n)
        out = 'r=%d data=%s' % (r, bytes(e.uc.mem_read(DST, n)).hex())
    elif kind == 'skip':
        n = int(args[0])
        e.w32(DST + 0x800, 0xAAAAAAAA)
        r = e.call(0x9BBF9C, R, n, DST + 0x800)
        out = 'r=%d skipped=%d' % (r, e.r32(DST + 0x800))
    elif kind == 'window':
        n = int(args[0])
        left_before = e.r32(R + 8)
        p = e.call(0x9BBE5C, R, n)
        if p == 0:
            where = 'null'
        elif rig.cfg.get('memory') is not None:
            where = 'mem+%d' % (p - MEMORY)
        elif WINDOW <= p < WINDOW + 0x1000:
            where = 'win+%d' % (p - WINDOW)
        else:
            where = 'tmp+%d' % (p - e.r32(R + 0x14))
        take = min(n, left_before) if rig.cfg.get('memory') is not None else n
        data = bytes(e.uc.mem_read(p, take)).hex() if p and take and take <= 64 else ''
        out = 'p=%s data=%s' % (where, data)
    elif kind == 'freetemp':
        e.call(0x9BBBE0, R)
    elif kind == 'release':
        out = 'r=%d' % e.call(0x9BBBA4, R)
    elif kind == 'reset':
        e.call(0x9BBB04, R)
    elif kind == 'heur':
        f = float(args[0])
        r = e.call(0x9BBB5C, R, bits(f), int(args[1]))
        out = 'r=%d thr=%08x prio=%d' % (r, e.r32(R + 0x20), e.r8(R + 0x24))
    else:
        raise SystemExit('unknown op ' + op)
    return '%s | %s | %s' % (out, ' '.join(rig.log), rig.state())


SCEN = {}


def sc(name, ops, **cfg):
    SCEN[name] = (cfg, ops)


# memory mode
sc('mem_read_all', ['read:4', 'read:100', 'read:1'], memory=pattern(20))
sc('mem_read_exact_left', ['read:20', 'read:0'], memory=pattern(20))
sc('mem_skip_then_read', ['skip:5', 'read:6', 'skip:100', 'read:1'], memory=pattern(20))
sc('mem_window', ['window:4', 'window:30', 'window:1'], memory=pattern(20))
sc('mem_raw_short_0x38', ['raw:8', 'raw:8', 'raw:8'], memory=pattern(20))
sc('mem_release', ['read:3', 'release', 'read:3'], memory=pattern(20))
sc('mem_heur', ['heur:1.5:50', 'heur:-1.0:50', 'heur:1.0:101', 'heur:0.0:100', 'heur:2.0:200'], memory=pattern(20))
# stream mode: window refill (n < min direct) and direct reads
sc('stream_small_reads_refill_window', ['read:8', 'read:8', 'read:20', 'read:40'], stream_len=300, mindirect=0x40, block=0x10)
sc('stream_direct_read_multiple_of_block', ['read:128', 'read:5'], stream_len=300, mindirect=0x40, block=0x10)
sc('stream_direct_read_with_remainder', ['read:100', 'read:3'], stream_len=300, mindirect=0x40, block=0x10)
sc('stream_direct_short_returns_2', ['read:200'], stream_len=150, mindirect=0x40, block=0x10)
sc('stream_direct_short_below_min', ['read:100'], stream_len=90, mindirect=0x40, block=0x10)
sc('stream_read_zero', ['read:0'], stream_len=300)
sc('stream_eof_returns_short', ['read:8', 'read:50', 'read:50'], stream_len=60, mindirect=0x20, block=0x10)
sc('stream_read_fail_9', ['read:8', 'read:8'], stream_len=300, read_fail=1)
sc('stream_direct_read_fail', ['read:100'], stream_len=300, read_fail=1)
sc('stream_wait_fail_5', ['read:8'], stream_len=300, wait_fail=1)
sc('stream_direct_wait_fail', ['read:100'], stream_len=300, wait_fail=1)
sc('stream_read_cap', ['read:8', 'read:100'], stream_len=300, read_cap=24)
sc('stream_raw_short_0x38', ['raw:200', 'raw:100'], stream_len=250, mindirect=0x20, block=0x10)
sc('stream_priority_and_throughput', ['read:100'], stream_len=300, priority=0xF0, throughput=4.0, mindirect=0x20, block=0x10)
# skip
sc('stream_skip_window_then_direct_skip', ['read:4', 'skip:3', 'skip:200', 'read:4'], stream_len=400, mindirect=0x40, block=0x10)
sc('stream_skip_small_refills', ['skip:10', 'skip:10', 'read:4'], stream_len=400, mindirect=0x40, block=0x10)
sc('stream_skip_small_status_nonzero_short_2', ['skip:30'], stream_len=10, mindirect=0x40, block=0x10, status=1)
sc('stream_skip_small_status_nonzero_enough', ['skip:5'], stream_len=100, mindirect=0x40, block=0x10, status=1)
sc('stream_skip_fail', ['skip:200'], stream_len=400, skip_fail=1)
sc('stream_skip_partial_cap', ['skip:200', 'read:4'], stream_len=400, skip_cap=50, mindirect=0x40, block=0x10)
sc('stream_skip_zero', ['skip:0'], stream_len=400)
sc('stream_skip_refill_read_fail', ['skip:10'], stream_len=400, read_fail=1)
sc('stream_skip_refill_wait_fail', ['skip:10'], stream_len=400, wait_fail=1)
# window pointer
sc('stream_window_in_window', ['read:2', 'window:6', 'window:6'], stream_len=300, mindirect=0x40, block=0x10)
sc('stream_window_alloc_copy', ['window:100', 'freetemp', 'freetemp'], stream_len=300, mindirect=0x40, block=0x10)
sc('stream_window_alloc_pad', ['read:3', 'window:100'], stream_len=300, mindirect=0x40, block=0x10)
sc('stream_window_alloc_fails', ['window:100'], stream_len=300, mindirect=0x40, block=0x10, alloc_fail=1)
sc('stream_window_short_read', ['window:100'], stream_len=50, mindirect=0x20, block=0x10)
sc('stream_release_closes_stream', ['read:4', 'release', 'read:4'], stream_len=300)


def cs_str(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def main():
    print('// <auto-generated> by re-analysis/tools/emu/emu_reader.py from the engine\'s own output (the real reader 0x9BBB04..0x9BBF9C under Unicorn): do not edit. </auto-generated>')
    print('namespace Cozmo.Protocol.Tests;')
    print('')
    print('internal static class BankReaderOracle')
    print('{')
    print('    /// <summary>name -> (config, the operations, the engine\'s result of each: "result | stream calls | reader state").</summary>')
    print('    public static readonly Dictionary<string, (ReaderConfig Config, string[] Ops, string[] Steps)> Scenarios = new()')
    print('    {')
    for name, (cfg, ops) in SCEN.items():
        rig = Rig(cfg)
        steps = [step(rig, op) for op in ops]
        c = ('new ReaderConfig { Memory = %s, StreamLength = %d, MinDirect = %d, Block = %d, Throughput = %s, Priority = %d, ReadFail = %d, WaitFail = %d, SkipFail = %d, ReadCap = %d, SkipCap = %d, Status = %d, AllocFail = %d }'
             % ('null' if cfg.get('memory') is None else '"%s"' % cfg['memory'].hex(), cfg.get('stream_len', 300), cfg.get('mindirect', 0x40), cfg.get('block', 0x10), 'BitConverter.UInt32BitsToSingle(0x%08XU)' % bits(cfg.get('throughput', 2.0)),
                cfg.get('priority', 50), cfg.get('read_fail', 0), cfg.get('wait_fail', 0), cfg.get('skip_fail', 0), cfg.get('read_cap', -1), cfg.get('skip_cap', -1), cfg.get('status', 0), cfg.get('alloc_fail', 0)))
        print('        ["%s"] = (%s, new string[] { %s }, new string[] {\n            %s }),' % (name, c, ', '.join(cs_str(o) for o in ops), ',\n            '.join(cs_str(s) for s in steps)))
    print('    };')
    print('}')
    print('')
    print('internal sealed class ReaderConfig')
    print('{')
    print('    public string? Memory { get; init; }')
    print('    public int StreamLength { get; init; }')
    print('    public uint MinDirect { get; init; }')
    print('    public uint Block { get; init; }')
    print('    public float Throughput { get; init; }')
    print('    public int Priority { get; init; }')
    print('    public int ReadFail { get; init; }')
    print('    public int WaitFail { get; init; }')
    print('    public int SkipFail { get; init; }')
    print('    public int ReadCap { get; init; }')
    print('    public int SkipCap { get; init; }')
    print('    public int Status { get; init; }')
    print('    public int AllocFail { get; init; }')
    print('}')


if __name__ == '__main__':
    main()
