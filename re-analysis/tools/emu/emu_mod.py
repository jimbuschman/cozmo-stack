"""Run the engine's own modulator-list bodies under Unicorn, for WwiseModulatorListTests (M6-025 / M6-009, C34.2 R6, R7, 0xA01918 and verification corrections 4, 5, 15).

0x9E8224 (the list reset), 0x9E62AC (the out-list walker), 0x9E61B4 (its recursion) and 0xA01918 (the consumer the Play path calls at 0xA38044) run on a manager, an out list, a context record and a PBI built in emulated
memory. The one stand-in is 0x9DCE44 (the per-id body; not adopted): it records its five arguments and returns a scripted value per id. The expected values in WwiseModulatorListTests are this script's output, not
the C#'s:

    python re-analysis/tools/emu/emu_mod.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseModulatorOracle.cs
"""
import sys
from emu_common import *

MGR_VAR = 0x108D8DC                    # *[GOT 0x10400E8]: the manager pointer


class World:
    def __init__(self, table=(), word10=0x10101010, results=None):
        e = Emu()
        e.std_hooks()
        e.log = []
        self.e = e
        self.results = results or {}
        self.mgr = e.alloc(0x40)
        e.w32(MGR_VAR, self.mgr)
        buckets = e.alloc(8)
        e.w32(self.mgr, buckets)
        e.w32(self.mgr + 4, 1 if table else 0)
        e.w32(self.mgr + 0x10, word10)
        for key, ids in table:
            arr = e.alloc(4 * max(len(ids), 1) + 4)
            for i, v in enumerate(ids):
                e.w32(arr + 4 * i, v)
            ent = e.alloc(0x20)
            e.w32(ent, arr)
            e.w32(ent + 4, len(ids))
            e.w32(ent + 0xC, key)
            e.w32(ent + 0x10, e.r32(buckets))
            e.w32(buckets, ent)

        def callee(emu):               # 0x9DCE44(id, rec, ctxrec, [mgr+0x10], list)
            sp = emu.reg_sp()
            rec, ctx = emu.reg(1), emu.reg(2)
            e.log.append(('9DCE44', emu.reg(0), e.r32(rec), e.r32(rec + 4), e.r32(rec + 8), e.r32(rec + 0xC), tuple(e.r32(ctx + 4 * i) for i in range(8)), emu.reg(3), e.r32(sp)))
            return self.results.get(emu.reg(0), 1)
        e.hook(0x9DCE44, callee)

    def outvec(self, records):
        """records: [(w4, wC, [ids])] -> the {data, count} vector 0x9E62AC walks."""
        e = self.e
        data = e.alloc(20 * max(len(records), 1) + 20)
        for i, (w4, wc, ids) in enumerate(records):
            arr = e.alloc(4 * max(len(ids), 1) + 4)
            for j, v in enumerate(ids):
                e.w32(arr + 4 * j, v)
            hold = e.alloc(8)
            e.w32(hold, arr)
            e.w32(hold + 4, len(ids))
            e.w32(data + 20 * i + 4, w4)
            e.w32(data + 20 * i + 8, 0x80 + i)
            e.w32(data + 20 * i + 0xC, wc)
            e.w32(data + 20 * i + 0x10, hold)
        vec = e.alloc(16)
        e.w32(vec, data)
        e.w32(vec + 4, len(records))
        return vec


def ctx_words(e):
    ctx = e.alloc(0x20)
    for i in range(8):
        e.w32(ctx + 4 * i, 0xC0DE0000 + i)
    return ctx


def calls_of(e):
    return [l[1:] for l in e.log if l[0] == '9DCE44']


def run_62ac(table, records, results=None):
    w = World(table, results=results)
    e = w.e
    vec = w.outvec(records)
    ctx = ctx_words(e)
    r = e.call(0x9E62AC, w.mgr, vec, ctx, 0x77770000)
    return dict(ret=r, calls=calls_of(e))


def run_61b4(table, rec, results=None):
    w = World(table, results=results)
    e = w.e
    r_ = e.alloc(0x10)
    e.w32(r_, rec[0]); e.w32(r_ + 4, rec[1]); e.w32(r_ + 8, rec[2]); e.w32(r_ + 0xC, rec[3])
    ctx = ctx_words(e)
    r = e.call(0x9E61B4, w.mgr, r_, ctx, 0x77770000)
    return dict(ret=r, calls=calls_of(e))


def run_8224(n_items, null_head=False):
    w = World()
    e = w.e
    holder = e.alloc(8)
    head = 0
    items = []
    for i in range(n_items):
        it = e.alloc(0x20)
        e.w32(it, head)
        e.w32(it + 0xC, 0x70 + i)
        items.append(it)
        head = it
    e.w32(holder, 0 if null_head else head)
    e.call(0x9E8224, holder)
    return [e.r32(it + 0xC) for it in reversed(items)]


def run_a01918(e8, count, records, table, field34_items, results=None, bf=0, extra=None):
    w = World(table, results=results)
    e = w.e
    pbi = e.alloc(0x240)
    e.w8(pbi + 0xE8, e8)
    e.w32(pbi + 0x14, 0xAA14)
    e.w32(pbi + 0x1E4, 0xAA1E4)
    e.w32(pbi + 0x1E8, 0xAA1E8)
    e.w32(pbi + 0x1C, 0xAA1C)
    e.w32(pbi + 0x140, 0xAA140)
    e.w32(pbi + 0x1D8, 0xAA1D8)
    e.w8(pbi + 0x1BF, bf)
    items = []
    if field34_items is not None:
        holder = e.alloc(8)
        head = 0
        for i in range(field34_items):
            it = e.alloc(0x20)
            e.w32(it, head)
            e.w32(it + 0xC, 0x70 + i)
            items.append(it)
            head = it
        e.w32(holder, head)
        e.w32(pbi + 0x34, holder)
    vec = w.outvec(records)
    e.w32(vec + 4, count)
    e.call(0xA01918, pbi, vec, 1)
    calls = calls_of(e)
    return dict(e8=e.r8(pbi + 0xE8), calls=calls, items=[e.r32(it + 0xC) for it in reversed(items)], pbi=pbi, vec=vec)


def cs_u(v):
    return '0x%XU' % (v & 0xFFFFFFFF)


def cs_calls(calls, pbi=None):
    out = []
    for (cid, w0, w4, w8, wc, ctx, r3, lst) in calls:
        if pbi is not None and lst == pbi + 0x34:
            lst = 0xFFFFFFFE                         # the list word of 0xA01918 is pbi+0x34: printed 0xFFFFFFFE
        ctx_s = ', '.join(('0xFFFFFFFFU' if (pbi is not None and v == pbi) else cs_u(v)) for v in ctx)
        out.append('(%s, %s, %s, %s, %s, new uint[] { %s }, %s, %s)' % (cs_u(cid), cs_u(w0), cs_u(w4), cs_u(w8), cs_u(wc), ctx_s, cs_u(r3), cs_u(lst)))
    return 'new (uint Id, uint W0, uint W4, uint W8, uint WC, uint[] Ctx, uint W10, uint List)[] { %s }' % ', '.join(out) if out else 'Array.Empty<(uint Id, uint W0, uint W4, uint W8, uint WC, uint[] Ctx, uint W10, uint List)>()'


def cs_records(records):
    return 'new (uint W4, uint WC, uint[] Ids)[] { %s }' % ', '.join('(%s, %s, new uint[] { %s })' % (cs_u(a), cs_u(b), ', '.join(cs_u(x) for x in ids)) for a, b, ids in records) if records else 'Array.Empty<(uint W4, uint WC, uint[] Ids)>()'


def cs_table(table):
    return 'new (uint Key, uint[] Ids)[] { %s }' % ', '.join('(%s, new uint[] { %s })' % (cs_u(k), ', '.join(cs_u(x) for x in ids)) for k, ids in table) if table else 'Array.Empty<(uint Key, uint[] Ids)>()'


def cs_results(results):
    return 'new Dictionary<uint, int> { %s }' % ', '.join('[%d] = %d' % (k, v) for k, v in sorted((results or {}).items()))


C62AC = {
    'empty': ([], [], None),
    'one_record_two_ids': ([], [(5, 6, [41, 42])], None),
    'two_records': ([], [(5, 6, [41]), (7, 8, [42, 43])], None),
    'record_without_ids': ([], [(5, 6, []), (7, 8, [43])], None),
    'callee_returns_2_sticks': ([], [(5, 6, [41, 42, 43])], {42: 2}),
    'callee_returns_3': ([], [(5, 6, [41])], {41: 3}),
    'recursion_one_level': ([(41, [51, 52])], [(5, 6, [41])], None),
    'recursion_two_levels': ([(41, [51]), (51, [61, 62]), (42, [71])], [(5, 6, [41, 42])], None),
    'recursion_callee_2_inside_does_not_change_result': ([(41, [51])], [(5, 6, [41])], {51: 2}),
    'table_without_the_key': ([(99, [51])], [(5, 6, [41])], None),
}

C61B4 = {
    'empty_table': ([], (41, 3, 3, 9), None),
    'miss': ([(99, [51])], (41, 3, 3, 9), None),
    'hit_two_ids': ([(41, [51, 52])], (41, 3, 3, 9), None),
    'hit_callee_2': ([(41, [51, 52])], (41, 3, 3, 9), {52: 2}),
    'hit_empty_ids': ([(41, [])], (41, 3, 3, 9), None),
    'chain': ([(41, [51]), (51, [61])], (41, 3, 3, 9), None),
}

A01918 = {
    'bit6_set_count0': (0x5D, 0, [], [], 2, None, 0),
    'bit6_set_count1': (0x5D, 1, [(5, 6, [41, 42])], [], 2, None, 0),
    'bit6_clear_count1': (0x1D, 1, [(5, 6, [41])], [], 0, None, 0),
    'no_list_holder': (0x5D, 1, [(5, 6, [41])], [], None, None, 0),
    'bit1bf2_set': (0x5D, 1, [(5, 6, [41])], [], None, None, 4),
    'two_records_recursion': (0x5D, 2, [(5, 6, [41]), (7, 8, [42])], [(41, [51])], 3, {51: 2}, 4),
    'count_zero_with_records': (0x5D, 0, [(5, 6, [41])], [], 1, None, 0),
}


def main():
    print('// <auto-generated> by re-analysis/tools/emu/emu_mod.py from the engine\'s own output (the real 0x9E8224, 0x9E62AC, 0x9E61B4 and 0xA01918 under Unicorn, 0x9DCE44 stood in): do not edit. </auto-generated>')
    print('namespace Cozmo.Protocol.Tests;')
    print('')
    print('internal static class ModulatorOracle')
    print('{')
    print('    /// <summary>0x9E8224: the +0xC words of the list items after the call, head first (items start 0x70, 0x71, ...; the list is built head = last).</summary>')
    print('    public static readonly Dictionary<string, (int Items, bool NullHead, uint[] After)> Reset = new()')
    print('    {')
    for name, n, nh in (('three_items', 3, False), ('no_items', 0, False), ('null_head', 2, True)):
        print('        ["%s"] = (%d, %s, new uint[] { %s }),' % (name, n, 'true' if nh else 'false', ', '.join(cs_u(x) for x in run_8224(n, nh))))
    print('    };')
    print('')
    print('    /// <summary>0x9E62AC: (table, records, scripted results of 0x9DCE44, return value, 0x9DCE44 calls in order). The ctx words are 0xC0DE0000 + i; the list word 0x77770000; [mgr+0x10] 0x10101010.</summary>')
    print('    public static readonly Dictionary<string, ((uint Key, uint[] Ids)[] Table, (uint W4, uint WC, uint[] Ids)[] Records, Dictionary<uint, int> Results, int Ret, (uint Id, uint W0, uint W4, uint W8, uint WC, uint[] Ctx, uint W10, uint List)[] Calls)> Walk = new()')
    print('    {')
    for name, (table, records, results) in C62AC.items():
        r = run_62ac(table, records, results)
        print('        ["%s"] = (%s, %s, %s, %d, %s),' % (name, cs_table(table), cs_records(records), cs_results(results), r['ret'], cs_calls(r['calls'])))
    print('    };')
    print('')
    print('    /// <summary>0x9E61B4: (table, rec {id, w4, w8, wC}, results, return value, calls).</summary>')
    print('    public static readonly Dictionary<string, ((uint Key, uint[] Ids)[] Table, (uint Id, uint W4, uint W8, uint WC) Rec, Dictionary<uint, int> Results, int Ret, (uint Id, uint W0, uint W4, uint W8, uint WC, uint[] Ctx, uint W10, uint List)[] Calls)> Rec = new()')
    print('    {')
    for name, (table, rec, results) in C61B4.items():
        r = run_61b4(table, rec, results)
        print('        ["%s"] = (%s, (%s, %s, %s, %s), %s, %d, %s),' % (name, cs_table(table), cs_u(rec[0]), cs_u(rec[1]), cs_u(rec[2]), cs_u(rec[3]), cs_results(results), r['ret'], cs_calls(r['calls'])))
    print('    };')
    print('')
    print('    /// <summary>0xA01918: (E8 in, count, records, table, list items, results, [pbi+0x1BF], E8 out, 9DCE44 calls (the PBI word is printed 0xFFFFFFFF), items after). PBI words: +0x14 0xAA14, +0x1E4 0xAA1E4, +0x1E8 0xAA1E8, +0x1C 0xAA1C, +0x140 0xAA140, +0x1D8 0xAA1D8.</summary>')
    print('    public static readonly Dictionary<string, (byte E8In, uint Count, (uint W4, uint WC, uint[] Ids)[] Records, (uint Key, uint[] Ids)[] Table, int ListItems, Dictionary<uint, int> Results, byte Bf, byte E8Out, (uint Id, uint W0, uint W4, uint W8, uint WC, uint[] Ctx, uint W10, uint List)[] Calls, uint[] ItemsAfter)> Consume = new()')
    print('    {')
    for name, (e8, count, records, table, items, results, bf) in A01918.items():
        r = run_a01918(e8, count, records, table, items, results, bf)
        calls = r['calls']
        print('        ["%s"] = (0x%02X, %d, %s, %s, %d, %s, %d, 0x%02X, %s, new uint[] { %s }),' % (name, e8, count, cs_records(records), cs_table(table), -1 if items is None else items, cs_results(results), bf, r['e8'], cs_calls(calls, r['pbi']), ', '.join(cs_u(x) for x in r['items'])))
    print('    };')
    print('}')


if __name__ == '__main__':
    main()
