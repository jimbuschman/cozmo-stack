"""Run the engine's bank loader 0x9B74D8 (type 0, mode 0: the LoadSoundbank path) under Unicorn, for WwiseMediaTableTests (M6-025 C32.2 M2..M5).

The loader runs on a BM object and a bank file held in Python; the stream functions (0x9BBC14, 0x9BBF64, 0x9BBF9C, 0x9BBB04, 0x9BBBA4, 0x9BC17C), BKHD (0x9B21F4), the bank registry lookup (0xA68804), the chunk
handlers the loader does not own (0x9B3260, 0x9B4048, 0x9B0B14, 0x9B2988, 0x9B2B08, 0x9B2410), the allocator and the pool functions (0xA7AC98, 0xA7AAE8, 0xA7A7C8, 0xA7AA9C, 0xA7A9FC) are Python stand-ins.
Arguments are laid out as the wrapper 0x9B7CD4 does (stack words s0..s12, s9 = &out, mode = 0, type = s3 = 0, pool id = s4 = -1 as LoadSoundbank passes).

    python re-analysis/tools/emu/emu_loader.py
"""
import struct
from emu_common import *
from emu_media import BM, BMVAR, node, table

OUTBANK = BASE + 0x3000
STREAM = BASE + 0x4000


def fourcc(s):
    return s.encode('ascii')


def chunk(tag, body):
    return fourcc(tag) + struct.pack('<I', len(body)) + body


def run(file_bytes, *, pool_attrs=0, pool_block=0, pool_create=0x77, pool_check=1, alloc_fail=None, handler_ret=1, bkhd_ret=1, label=''):
    e = Emu()
    e.std_hooks()
    e.set_got(0x1040078, BMVAR)
    e.w32(BMVAR, BM)
    state = {'pos': 0, 'closed': 0, 'handled': [], 'pool_calls': [], 'alloc_sizes': []}

    def rd(emu, n):
        got = file_bytes[state['pos']:state['pos'] + n]
        state['pos'] += len(got)
        return got

    def f_read(emu):           # 0x9BBC14(stream, buf, n, &read) -> 1
        buf, n, pr = emu.reg(1), emu.reg(2), emu.reg(3)
        data = rd(emu, n)
        emu.uc.mem_write(buf, data) if data else None
        emu.w32(pr, len(data))
        return 1
    e.hook(0x9BBC14, f_read)

    def f_raw(emu):            # 0x9BBF64(stream, buf, n) -> 1, or 0x38 on a short read
        buf, n = emu.reg(1), emu.reg(2)
        data = rd(emu, n)
        emu.uc.mem_write(buf, data) if data else None
        return 1 if len(data) == n else 0x38
    e.hook(0x9BBF64, f_raw)

    def f_skip(emu):           # 0x9BBF9C(stream, n, &skipped)
        n, pr = emu.reg(1), emu.reg(2)
        got = rd(emu, n)
        emu.w32(pr, len(got))
        return 1
    e.hook(0x9BBF9C, f_skip)

    def f_close(emu):
        state['closed'] += 1
    e.hook(0x9BBBA4, f_close)
    e.hook(0x9BBB04, lambda emu: 0)
    e.hook(0x9BC17C, lambda emu: 1)           # open by name
    e.hook(0x9B21F4, lambda emu: bkhd_ret)    # BKHD
    e.hook(0xA68804, lambda emu: 0)           # bank registry: not found
    def handler(emu, name):      # a chunk handler: records the call and consumes the chunk body (the loader's [sp+0x40] is the chunk size)
        state['handled'].append(name)
        rd(emu, emu.r32(emu.reg_sp() + 0x40))
        return handler_ret
    for addr, name in ((0x9B3260, 'HIRC'), (0x9B4048, 'INIT'), (0x9B0B14, 'STMG'), (0x9B2988, 'ENVS'), (0x9B2B08, 'PLAT'), (0x9B2410, 'STID')):
        e.hook(addr, (lambda nm: lambda emu: handler(emu, nm))(name))

    def alloc(emu):            # 0xA7A7F4(pool, size)
        e.alloc_calls += 1
        state['alloc_sizes'].append((emu.reg(0), emu.reg(1)))
        if alloc_fail is not None and e.alloc_calls == alloc_fail:
            return 0
        return e.alloc(emu.reg(1))
    e.hook(0xA7A7F4, alloc)
    e.hook(0xA7AC98, lambda emu: state['pool_calls'].append(('create', emu.reg(0), emu.reg(1), emu.reg(2), emu.reg(3))) or pool_create)
    e.hook(0xA7AAE8, lambda emu: state['pool_calls'].append(('check', emu.reg(0))) or pool_check)
    e.hook(0xA7A7C8, lambda emu: state['pool_calls'].append(('attrs', emu.reg(0))) or pool_attrs)
    e.hook(0xA7AA9C, lambda emu: state['pool_calls'].append(('blocksize', emu.reg(0))) or pool_block)
    e.hook(0xA7A9FC, lambda emu: state['pool_calls'].append(('block', emu.reg(0))) or e.alloc(max(pool_block, 16)))
    e.hook(0x9B45D8, lambda emu: state.setdefault('cleanup', []).append(1) or 0)

    # stack words s0..s12 as the wrapper 0x9B7CD4 builds them; the loader's [sp+0x98] = s0 at entry sp
    s = [0] * 13
    s[2] = 0x10          # [sp+0xa0]: the platform word (read only by paths this oracle does not take)
    s[3] = 0             # type
    s[4] = 0xFFFFFFFF    # pool id (-1)
    s[5] = 0
    s[9] = OUTBANK
    s[10] = 0            # mode
    e.w32(OUTBANK, 0)
    e.alloc_calls = 0
    r = e.call(0x9B74D8, BM, 0, 0x1234, 0xDEAD, stack=s)
    bank = e.r32(OUTBANK)
    res = {'ret': r, 'closed': state['closed'], 'handled': state['handled'], 'pool_calls': state['pool_calls'], 'table': table(e), 'cleanup': len(state.get('cleanup', []))}
    if bank:
        res['bank'] = {'id08': e.r32(bank + 8), 'flags4': e.r8(bank + 4), 'data14': e.r32(bank + 0x14) != 0, 'didx18': e.r32(bank + 0x18) != 0, 'size1c': e.r32(bank + 0x1C),
                       'pool24': e.r32(bank + 0x24), 'flag28': e.r8(bank + 0x28), 'ctr2c': e.r32(bank + 0x2C), 'cnt30': e.r32(bank + 0x30), 'ref48': e.r32(bank + 0x48), 'flags50': e.r8(bank + 0x50)}
        res['bank_addr'] = bank
        res['data_base'] = e.r32(bank + 0x14)
    res['e'] = e
    return res


def show(label, res):
    e = res.pop('e')
    keys = [k for k in res if k not in ('bank_addr',)]
    print(label)
    for k in keys:
        print('   ', k, res[k] if k != 'data_base' else hex(res[k]))
    return e


BKHD = chunk('BKHD', b'\x00' * 16)


def main():
    didx3 = b''.join(struct.pack('<III', i, o, sz) for i, o, sz in ((1001, 0, 10), (1002, 10, 20), (1003, 30, 5)))
    data = bytes(range(35))

    f = BKHD + chunk('DIDX', didx3) + chunk('DATA', data)
    e = show('L1 BKHD DIDX DATA, pool attributes bit3 clear', run(f, pool_attrs=0))
    f = BKHD + chunk('DIDX', didx3) + chunk('DATA', data)
    r = run(f, pool_attrs=8, pool_block=64)
    e = show('L2 pool attributes bit3 set, block size 64 (>= 35)', r)
    f = BKHD + chunk('DIDX', didx3) + chunk('DATA', data)
    show('L3 pool attributes bit3 set, block size 16 (< 35): [bank+0x14] stays 0', run(f, pool_attrs=8, pool_block=16))
    show('L4 DATA before DIDX: the writer is not run (count is 0)', run(BKHD + chunk('DATA', data) + chunk('DIDX', didx3), pool_attrs=0))
    show('L5 two DIDX chunks: the second is skipped (counter is non-zero only after DATA, so the second DIDX replaces)', run(BKHD + chunk('DIDX', didx3) + chunk('DIDX', didx3[:12]) + chunk('DATA', data[:35]), pool_attrs=0))
    show('L6 unknown chunk then HIRC and INIT', run(BKHD + chunk('ABCD', b'xyz') + chunk('HIRC', b'1234') + chunk('INIT', b'12') + chunk('DIDX', didx3) + chunk('DATA', data), pool_attrs=0))
    show('L7 STMG with size 0 is skipped (no handler call)', run(BKHD + chunk('STMG', b'') + chunk('DIDX', didx3) + chunk('DATA', data), pool_attrs=0))
    show('L8 handler returns 9', run(BKHD + chunk('HIRC', b'1234') + chunk('DIDX', didx3), handler_ret=9))
    show('L9 pool create fails (-1)', run(BKHD + chunk('DIDX', didx3) + chunk('DATA', data), pool_create=0xFFFFFFFF))
    show('L10 pool check returns 5', run(BKHD + chunk('DIDX', didx3) + chunk('DATA', data), pool_check=5))
    show('L11 BKHD fails with 9', run(BKHD + chunk('DIDX', didx3), bkhd_ret=9))
    show('L12 truncated chunk header (5 bytes) after BKHD', run(BKHD + b'DIDX1'))
    show('L13 DATA chunk shorter than its size (read 20 of 35)', run(BKHD + chunk('DIDX', didx3) + fourcc('DATA') + struct.pack('<I', 35) + bytes(20), pool_attrs=0))
    show('L14 DATA size 0', run(BKHD + chunk('DIDX', didx3) + chunk('DATA', b''), pool_attrs=0))
    show('L15 DIDX size 30 (2 entries + 6 bytes left unread: the next header read starts inside the chunk)', run(BKHD + chunk('DIDX', didx3[:24] + b'\x00' * 6) + chunk('DATA', data), pool_attrs=0))
    for n in (1, 2, 3, 4):
        show(f'L16 allocation #{n} fails', run(BKHD + chunk('DIDX', didx3) + chunk('DATA', data), pool_attrs=0, alloc_fail=n))
    show('L17 the file ends right after BKHD', run(BKHD))
    show('L18 DIDX of 1 entry whose id is 0, then DATA', run(BKHD + chunk('DIDX', struct.pack('<III', 0, 0, 4)) + chunk('DATA', b'abcd'), pool_attrs=0))


if __name__ == '__main__':
    main()
