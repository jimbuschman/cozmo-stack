"""Run the engine's own media-table functions under Unicorn, for WwiseMediaTableTests (M6-025 C31.1, C32.2).

0x9B49A4 (the writer that fills the BM hash), 0xA1EC54 / 0x9BB320 / 0x9BB1F8 (the lookup) and 0xA1ECBC / 0x9B65A8 (the release) run on a BM object, bank objects and DIDX arrays built in emulated memory.
Only the allocator (0xA7A7F4, 0xA7A988, 0xA7A914), the mutexes, __aeabi_uidivmod and memmove are Python stand-ins. The expected values in WwiseMediaTableTests are this script's output, not the C#'s.

    python re-analysis/tools/emu/emu_media.py
"""
import struct
from emu_common import *

BM = BASE + 0x1000
BMVAR = BASE + 0x0F00
OUT = BASE + 0x2000


def setup():
    e = Emu()
    e.std_hooks()
    e.set_got(0x1040078, BMVAR)
    e.w32(BMVAR, BM)
    e.cleanup_called = 0

    def cleanup(emu):          # 0x9B45D8: not run, only recorded (the C# throws there)
        emu.cleanup_called += 1
        return 0
    e.hook(0x9B45D8, cleanup)
    return e


def new_bank(e, entries, flags4=2, refcount=1):
    """A bank object with a DIDX array of (id, offset, size) entries (raw u32 triples), +0x2C = 0, +0x30 = len(entries)."""
    bank = e.alloc(0x60)
    didx = e.alloc(12 * max(len(entries), 1))
    for i, (mid, off, size) in enumerate(entries):
        e.w32(didx + 12 * i, mid)
        e.w32(didx + 12 * i + 4, off)
        e.w32(didx + 12 * i + 8, size)
    e.w32(bank + 0x18, didx)
    e.w32(bank + 0x30, len(entries))
    e.w8(bank + 4, flags4)
    e.w32(bank + 0x48, refcount)
    return bank


def write(e, bank, data_base):
    e.alloc_calls = 0
    r = e.call(0x9B49A4, BM, data_base, bank)
    return r


def lookup(e, key, size8=777):
    obj = e.alloc(0x20)
    e.w32(obj, key)
    e.w32(obj + 8, size8)
    e.w32(obj + 0x10, 0)
    o1, o2, o3 = OUT, OUT + 4, OUT + 8
    e.w32(o1, 0xAAAA0001)
    e.w32(o2, 0xAAAA0002)
    e.w32(o3, 0xAAAA0003)
    e.call(0xA1EC54, obj, o1, o2, o3)
    return {'data': e.r32(o1), 'size': e.r32(o2), 'bank': e.r32(o3)}


def release(e, key):
    obj = e.alloc(0x20)
    e.w32(obj, key)
    e.w32(obj + 0x10, 0)
    e.call(0xA1ECBC, obj)


def node(e, key):
    """(refcount, item count, capacity, [(bank, data, size)...]) of the node for key, or None."""
    n = e.r32(BM + 0x38)
    if n == 0:
        return None
    p = e.r32(e.r32(BM + 0x34) + 4 * (key % n))
    while p:
        if e.r32(p + 4) == key:
            cnt = e.r32(p + 0x14)
            arr = e.r32(p + 0x10)
            items = [(e.r32(arr + 12 * i), e.r32(arr + 12 * i + 4), e.r32(arr + 12 * i + 8)) for i in range(cnt)]
            return {'ref': e.r32(p + 0x1C), 'count': cnt, 'cap': e.r32(p + 0x18), 'items': items, 'id20': e.r32(p + 0x20)}
        p = e.r32(p)
    return None


def table(e):
    return {'buckets': e.r32(BM + 0x38), 'cap': e.r32(BM + 0x3C), 'nodes': e.r32(BM + 0x40)}


def main():
    D = 0x08100000
    print('--- S1 one bank, 3 entries (1001,0,10) (1002,10,20) (1003,30,5), DATA base 0x08100000')
    e = setup()
    a = new_bank(e, [(1001, 0, 10), (1002, 10, 20), (1003, 30, 5)])
    print('ret', write(e, a, D), 'counter2c', e.r32(a + 0x2C), table(e))
    for k in (1001, 1002, 1003, 9999):
        r = lookup(e, k)
        print('lookup', k, {x: hex(v) for x, v in r.items()}, 'bank==a', r['bank'] == a, 'node', node(e, k) and node(e, k)['ref'])
    print('bank48 after 3 lookups', e.r32(a + 0x48))

    print('--- S2 two banks write id 5: A (offset 0, size 100) then B (offset 8, size 300); a third C (offset 4, size 300)')
    e = setup()
    a = new_bank(e, [(5, 0, 100)])
    b = new_bank(e, [(5, 8, 300)])
    c = new_bank(e, [(5, 4, 300)])
    print('write A', write(e, a, 0x1000), 'write B', write(e, b, 0x2000), 'write C', write(e, c, 0x3000))
    n5 = node(e, 5)
    print('node', n5, 'banks order A,B,C =', [a, b, c])
    r = lookup(e, 5)
    print('lookup 5 ->', {x: hex(v) for x, v in r.items()}, 'picked B?', r['bank'] == b, 'picked C?', r['bank'] == c, 'refs', node(e, 5)['ref'])
    print('b48', e.r32(b + 0x48), 'c48', e.r32(c + 0x48))
    e.w8(b + 4, e.r8(b + 4) | 1)
    e.w8(c + 4, e.r8(c + 4) | 1)
    r = lookup(e, 5)
    print('lookup 5 with B and C flagged bit0 ->', {x: hex(v) for x, v in r.items()}, 'picked A?', r['bank'] == a, 'refs', node(e, 5)['ref'])
    e.w8(a + 4, e.r8(a + 4) | 1)
    r = lookup(e, 5)
    print('lookup 5 with all flagged ->', {x: hex(v) for x, v in r.items()}, 'refs (restored)', node(e, 5)['ref'])
    e2 = setup()
    a = new_bank(e2, [(5, 0, 100)], flags4=0)
    write(e2, a, 0x1000)
    r = lookup(e2, 5)
    print('bank with [+4] bit1 clear: lookup', {x: hex(v) for x, v in r.items()}, 'a48', e2.r32(a + 0x48))

    print('--- S3 same bank rewrites its entry: (7,0,10) then DATA moved, written again')
    e = setup()
    a = new_bank(e, [(7, 0, 10)])
    print('write', write(e, a, 0x1000), node(e, 7))
    e.w32(a + 0x2C, 0)
    print('rewrite', write(e, a, 0x5000), node(e, 7))

    print('--- S4 id 0 skipped and counted: (0,0,4) (11,4,4)')
    e = setup()
    a = new_bank(e, [(0, 0, 4), (11, 4, 4)])
    print('ret', write(e, a, 0x1000), 'counter', e.r32(a + 0x2C), table(e), node(e, 11))

    print('--- S5 growth: bank with 60 entries ids 100..159 (size 1 each)')
    e = setup()
    a = new_bank(e, [(100 + i, i, 1) for i in range(60)])
    print('ret', write(e, a, 0x1000), table(e), 'counter', e.r32(a + 0x2C))
    for n in (27, 28):
        e = setup()
        a = new_bank(e, [(100 + i, i, 1) for i in range(n)])
        write(e, a, 0x1000)
        print(f'after {n} entries', table(e))
    e = setup()
    a = new_bank(e, [(100 + i, i, 1) for i in range(28)])
    write(e, a, 0x1000)
    print('28 entries: lookups', [hex(lookup(e, 100 + i)['data']) for i in (0, 13, 27)])

    print('--- S6 failures (allocation n fails)')
    for n in (1, 2, 3, 4, 5):
        e = setup()
        e.fail_alloc_at = n
        a = new_bank(e, [(1001, 0, 10), (1002, 10, 20)])
        e.alloc_calls = 0
        r = write(e, a, 0x1000)
        print(f'alloc #{n} fails: ret {r:#x} counter {e.r32(a + 0x2C)} table {table(e)} cleanup {e.cleanup_called} node1001 {node(e, 1001) and node(e, 1001)["items"]} node1002 {node(e, 1002) and node(e, 1002)["items"]}')
    print('failure on the second entry with a first entry done')
    for n in (4, 5, 6):
        e = setup()
        a = new_bank(e, [(1001, 0, 10), (1002, 10, 20)])
        e.alloc_calls = 0
        e.fail_alloc_at = n
        r = write(e, a, 0x1000)
        print(f'alloc #{n} fails: ret {r:#x} counter {e.r32(a + 0x2C)} table {table(e)} cleanup {e.cleanup_called}')
    print('growth failure on an existing node: bank A writes 5, bank B (new node alloc ok, items alloc fails)')
    e = setup()
    a = new_bank(e, [(5, 0, 10)])
    write(e, a, 0x1000)
    b = new_bank(e, [(5, 4, 20)])
    e.alloc_calls = 0
    e.fail_alloc_at = 1
    r = write(e, b, 0x2000)
    print('ret', hex(r), 'table', table(e), 'cleanup', e.cleanup_called, 'node5', node(e, 5))

    print('--- S7 release')
    e = setup()
    a = new_bank(e, [(21, 0, 10)])
    write(e, a, 0x1000)
    lookup(e, 21)
    print('after write+lookup', node(e, 21)['ref'], table(e))
    release(e, 21)
    print('after 1 release', node(e, 21) and node(e, 21)['ref'], table(e))
    release(e, 21)
    print('after 2 releases', node(e, 21), table(e))
    print('lookup after', lookup(e, 21))
    release(e, 21)
    print('release of an absent key leaves', table(e))
    print('empty table lookup', lookup(setup(), 1))


if __name__ == '__main__':
    main()
