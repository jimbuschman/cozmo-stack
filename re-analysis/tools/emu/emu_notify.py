"""Run the engine's PBI-notification push 0xA38600 and flush 0xA38420 under Unicorn, for WwiseNotificationQueueTests (M6-025 / M6-026, C34.3 S8).

The real functions: the push 0xA38600 and the flush 0xA38420 (with its pop 0xA38518..0xA3856C) on the queue Q at 0x108DE78. Python stand-ins: the allocator 0xA7A7F4 / 0xA7A988 (the heap: every call logged, failures
injectable) and the per-item handler 0xA0188C (logged); items queued for a flush carry codes other than 4, so the code-4 teardown (0xA384C8) is not reached. The queue's init is unread: each scenario writes the array,
its free-list order, the limit and the count directly. The expected values in WwiseNotificationQueueTests are this script's output, not the C#'s:

    python re-analysis/tools/emu/emu_notify.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseNotificationQueueOracle.cs
"""
from emu_common import *
from unicorn import UcError

Q = 0x108DE78
ARRAY = BASE + 0x10000
POOLVAR = 0x1052418


class Rig:
    def __init__(self, cap, limit, free_order, alloc_fail=None):
        e = Emu()
        e.std_hooks()
        self.e = e
        self.cap = cap
        self.log = []
        self.heap = {}            # address -> sequence id
        self.alloc_calls = 0
        self.alloc_fail = set(alloc_fail or ())
        e.uc.mem_write(Q, bytes(0x20))
        e.uc.mem_write(ARRAY, bytes(20 * max(cap, 1)))
        e.w32(Q + 0x10, cap)
        e.w32(Q + 0x14, limit)
        e.w32(Q + 0x1C, ARRAY)
        prev = 0
        for idx in reversed(free_order):
            a = ARRAY + 20 * idx
            e.w32(a, prev)
            prev = a
        e.w32(Q + 0xC, prev)

        def alloc(emu):
            self.alloc_calls += 1
            self.log.append('alloc')
            if self.alloc_calls in self.alloc_fail:
                return 0
            a = e.alloc(emu.reg(1) + 0x20)
            self.heap[a] = cap + len(self.heap)
            return a
        e.hook(0xA7A7F4, alloc)
        e.hook(0xA7A988, lambda emu: self.log.append('free') or 1)
        e.hook(0xA0188C, lambda emu: self.log.append('h:%d/%d/%d/%d' % (emu.reg(0), emu.reg(1), emu.reg(2), emu.reg(3))) or 0)

    def ident(self, a):
        if a == 0:
            return '-'
        if ARRAY <= a < ARRAY + 20 * self.cap:
            return str((a - ARRAY) // 20)
        return str(self.heap[a])

    def dump(self):
        e = self.e
        items = []
        p = e.r32(Q + 4)
        guard = 0
        while p and guard < 40:
            items.append('%s:%d/%d/%d/%d' % (self.ident(p), e.r32(p + 4), e.r32(p + 8), e.r32(p + 0xC), e.r32(p + 0x10)))
            p = e.r32(p)
            guard += 1
        free = []
        p = e.r32(Q + 0xC)
        while p and len(free) < 40:
            free.append(self.ident(p))
            p = e.r32(p)
        return 'head=%s tail=%s free=%s count=%d queue=%s' % (self.ident(e.r32(Q + 4)), self.ident(e.r32(Q + 8)), ','.join(free), e.r32(Q + 0x18), ','.join(items))

    def push(self, pbi, code, r2, r3):
        self.log.clear()
        try:
            self.e.call(0xA38600, pbi, code, r2, r3)
            res = 'ok'
        except UcError:
            res = 'crash'
        return '%s | %s | %s' % (res, ' '.join(self.log), self.dump())

    def flush(self):
        self.log.clear()
        self.e.call(0xA38420)
        return 'flush | %s | %s' % (' '.join(self.log), self.dump())


SCEN = {}


def sc(name, cap, limit, free_order, ops, alloc_fail=None):
    SCEN[name] = dict(cap=cap, limit=limit, free_order=free_order, ops=ops, alloc_fail=alloc_fail)


sc('free_item_empty_queue', 3, 8, [0, 1, 2], [('push', 100, 3, 0, 0x3F800000)])
sc('free_list_order_is_kept', 3, 8, [2, 0, 1], [('push', 100, 3, 0, 1), ('push', 101, 3, 1, 2), ('push', 102, 4, 1, 0)])
sc('four_pushes_third_array_then_heap', 2, 8, [0, 1], [('push', 1, 3, 0, 0), ('push', 2, 3, 0, 0), ('push', 3, 3, 0, 0), ('push', 4, 4, 1, 0)])
sc('no_array_allocates_each_item', 0, 4, [], [('push', 1, 3, 0, 0), ('push', 2, 3, 0, 0)])
sc('limit_reached_flush_drains_then_reuses', 2, 2, [0, 1], [('push', 1, 3, 0, 0), ('push', 2, 3, 0, 0), ('push', 3, 3, 0, 0), ('push', 4, 3, 0, 0)])
sc('heap_items_full_flush_frees_then_allocates', 0, 1, [], [('push', 1, 3, 0, 0), ('push', 2, 3, 0, 0), ('push', 3, 3, 0, 0)])
sc('alloc_fails_below_limit_flushes_then_allocates', 0, 4, [], [('push', 1, 3, 0, 0), ('push', 2, 3, 0, 0)], alloc_fail=[2])
sc('alloc_fails_twice_crashes', 0, 4, [], [('push', 1, 3, 0, 0), ('push', 2, 3, 0, 0), ('push', 3, 3, 0, 0)], alloc_fail=[2, 3])
sc('limit_zero_crashes_when_empty', 0, 0, [], [('push', 1, 3, 0, 0)])
sc('limit_zero_with_free_array', 1, 0, [0], [('push', 1, 3, 0, 0), ('push', 2, 3, 0, 0)])
sc('flush_pops_in_order', 3, 8, [0, 1, 2], [('push', 1, 3, 0, 0), ('push', 2, 5, 1, 0), ('push', 3, 7, 2, 9), ('flush',), ('push', 4, 3, 0, 0), ('flush',)])
sc('flush_empty', 2, 8, [0, 1], [('flush',), ('push', 1, 3, 0, 0), ('flush',), ('flush',)])
sc('flush_mixed_array_and_heap_items', 1, 8, [0], [('push', 1, 3, 0, 0), ('push', 2, 3, 0, 0), ('push', 3, 3, 0, 0), ('flush',), ('push', 4, 3, 0, 0), ('push', 5, 3, 0, 0), ('flush',)])
sc('free_list_after_flush_is_lifo', 3, 8, [0, 1, 2], [('push', 1, 3, 0, 0), ('push', 2, 3, 0, 0), ('push', 3, 3, 0, 0), ('flush',), ('push', 4, 3, 0, 0), ('push', 5, 3, 0, 0)])
sc('wrap_negative_values', 2, 8, [0, 1], [('push', 0xFFFFFFFF, 0xFFFFFFFE, 0x80000000, 0x7FFFFFFF)])


def run(sc_):
    rig = Rig(sc_['cap'], sc_['limit'], sc_['free_order'], sc_['alloc_fail'])
    out = []
    alloc_seq = 0
    for op in sc_['ops']:
        if op[0] == 'push':
            out.append(rig.push(*op[1:]))
        else:
            out.append(rig.flush())
        if out[-1].startswith('crash'):
            break
    return out


def cs_str(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def main():
    print('// <auto-generated> by re-analysis/tools/emu/emu_notify.py from the engine\'s own output (the real push 0xA38600 and flush 0xA38420 under Unicorn): do not edit. </auto-generated>')
    print('namespace Cozmo.Protocol.Tests;')
    print('')
    print('internal static class NotificationQueueOracle')
    print('{')
    print('    /// <summary>name -> (array capacity, limit, free-list order, allocations that fail, the operations, the engine\'s result of each: "result | calls | queue state").</summary>')
    print('    public static readonly Dictionary<string, (int Capacity, uint Limit, int[] FreeOrder, int[] AllocFails, string[] Ops, string[] Steps)> Scenarios = new()')
    print('    {')
    for name, sc_ in SCEN.items():
        steps = run(sc_)
        ops = []
        for op in sc_['ops'][:len(steps)]:
            ops.append('push:%d:%d:%d:%d' % tuple(x if x < 0x80000000 else x - (1 << 32) for x in op[1:]) if op[0] == 'push' else 'flush')
        print('        ["%s"] = (%d, %du, new int[] { %s }, new int[] { %s }, new string[] { %s }, new string[] {\n            %s }),' % (name, sc_['cap'], sc_['limit'], ', '.join(str(x) for x in sc_['free_order']), ', '.join(str(x) for x in sorted(sc_['alloc_fail'] or ())),
              ', '.join(cs_str(o) for o in ops), ',\n            '.join(cs_str(s) for s in steps)))
    print('    };')
    print('}')


if __name__ == '__main__':
    main()
