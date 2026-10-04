"""Run the engine's own bank loader under Unicorn, for WwiseBankLoaderTests (M6-025 / M6-024 / M6-001, C34.4 K1..K19).

The real functions: the loader 0x9B74D8, BKHD 0x9B21F4, DIDX / DATA (inline), INIT 0x9B4048, ENVS 0x9B2988 and 0x9CC5A0, PLAT 0x9B2B08 and 0x9A6518, HIRC 0x9B3260 (the hook path 0x9B3EBC), the writer 0x9B49A4 and its cleanup
0x9B45D8, the bank release 0x9B47D8, the registry 0xA68804, the pool functions 0xA7AC98 / 0xA7AAE8 / 0xA7A7C8 / 0xA7AA9C / 0xA7A9FC / 0xA7AA48 and the reader 0x9BBB04 / 0x9BBB90 / 0x9BBBA4 / 0x9BBBE0 / 0x9BBC14 / 0x9BBE5C /
0x9BBF64 / 0x9BBF9C (memory mode, and stream mode over a stub stream). Python stand-ins (bodies C34 does not adopt): the allocator 0xA7A7F4 / 0xA7A894 / 0xA7A988 / 0xA7A914 (the heap), the external allocator thunk 0x8D7FFC, STMG 0x9B0B14,
STID 0x9B2410, the streamed DATA load 0x9B6C34, the file opens 0x9BC17C / 0x9BC348, 0xA57238, 0xA68150, DestroyPool 0xA7AEC4, the music hook, and libc (memcpy, strncpy, strlen, strcmp, strcpy). The expected values in
WwiseBankLoaderTests are this script's output, not the C#'s:

    python re-analysis/tools/emu/emu_bankload.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseBankLoaderOracle.cs
"""
import struct
import sys
from emu_common import *
from unicorn.arm_const import *

BM = BASE + 0x1000
BMVAR = BASE + 0x0F00
OUTBANK = BASE + 0x3000
DATA_AT = BASE + 0x20000          # the bank file bytes (memory mode)
STUB_HOOK = BASE + 0x40300        # the music hook function address
STREAM_VT = BASE + 0x41000
STREAM_OBJ = BASE + 0x42000


def fourcc(s):
    return s.encode('ascii')


def chunk(tag, body):
    return fourcc(tag) + struct.pack('<I', len(body)) + body


def bkhd(version=0x78, bank_id=1234, lang=0, align=0, devalloc=0, proj=77, extra=b'', xor=None):
    body = struct.pack('<IIIHHI', version, bank_id, lang, align, devalloc, proj) + extra
    if xor:
        words = [struct.unpack_from('<I', body, 4 * i)[0] for i in range(4)]
        k = xor
        w = [words[0] ^ k[0], words[1] ^ k[1], words[2] ^ k[2], words[3] ^ k[3]]
        body = struct.pack('<IIII', *w) + body[16:]
    return chunk('BKHD', body)


class World:
    def __init__(self, spec):
        self.spec = spec
        e = Emu()
        e.std_hooks()
        self.e = e
        e.log = []
        e.set_got(0x1040078, BMVAR)
        e.w32(BMVAR, BM)
        e.uc.mem_write(BM, bytes(0x100))
        e.alloc_calls = 0
        e.fail_alloc_at = spec.get('alloc_fail')
        # libc stand-ins
        e.hook(0x4D37F0, lambda emu: emu.uc.mem_write(emu.reg(0), bytes(emu.uc.mem_read(emu.reg(1), emu.reg(2)))) or emu.reg(0))
        e.hook(0x4D36DC, lambda emu: emu.uc.mem_write(emu.reg(0), bytes([emu.reg(1) & 0xFF]) * emu.reg(2)) or emu.reg(0))

        def strncpy(emu):
            dst, src, n = emu.reg(0), emu.reg(1), emu.reg(2)
            data = bytes(emu.uc.mem_read(src, n))
            z = data.find(b'\0')
            data = data[:z] + bytes(n - z) if z >= 0 else data
            emu.uc.mem_write(dst, data)
            return dst
        e.hook(0x4BB694, strncpy)

        def cstr(a):
            out = b''
            while True:
                c = bytes(e.uc.mem_read(a, 1))
                if c == b'\0':
                    return out
                out += c
                a += 1
        self.cstr = cstr
        e.hook(0x4A44E0, lambda emu: len(cstr(emu.reg(0))))
        e.hook(0x4AAE04, lambda emu: (lambda a, b: (a > b) - (a < b))(cstr(emu.reg(0)), cstr(emu.reg(1))) & 0xFFFFFFFF)
        e.hook(0x4A7FB4, lambda emu: emu.uc.mem_write(emu.reg(0), cstr(emu.reg(1)) + b'\0') or emu.reg(0))
        # allocator (the heap): records every call
        self.allocs = []

        def alloc(emu):
            if emu.reg(1) == 0:
                return 0
            e.alloc_calls += 1
            self.allocs.append(emu.reg(1))
            if e.fail_alloc_at is not None and e.alloc_calls == e.fail_alloc_at:
                return 0
            return e.alloc(emu.reg(1))
        e.hook(0xA7A7F4, alloc)
        e.hook(0xA7A894, lambda emu: 0 if emu.reg(1) == 0 else (alloc_aligned(emu)))

        def alloc_aligned(emu):
            e.alloc_calls += 1
            self.allocs.append(('aligned', emu.reg(1), emu.reg(2)))
            if e.fail_alloc_at is not None and e.alloc_calls == e.fail_alloc_at:
                return 0
            a = e.alloc(emu.reg(1) + 0x40)
            return (a + 0x1F) & ~0x1F
        e.hook(0xA7A894, lambda emu: 0 if emu.reg(1) == 0 else alloc_aligned(emu))
        self.frees = []
        e.hook(0xA7A988, lambda emu: self.frees.append(emu.reg(1)) or 1)
        e.hook(0xA7A914, lambda emu: self.frees.append(emu.reg(1)) or 1)
        # the thunk to the external allocator (pool memory)
        def thunk_alloc(emu):
            e.alloc_calls += 1
            self.allocs.append(('thunk', emu.reg(0)))
            if e.fail_alloc_at is not None and e.alloc_calls == e.fail_alloc_at:
                return 0
            return e.alloc(emu.reg(0) + 0x40)
        e.hook(0x8D7FFC, thunk_alloc)
        # the pool table
        self.max_pools = spec.get('max_pools', 8)
        tbl = e.alloc(0x34 * 16)
        e.uc.mem_write(tbl, bytes(0x34 * 16))
        for i in range(16):
            e.w32(tbl + 0x34 * i + 0x18, 1)   # 0xA7A694: init 0xA7A5D8 sets [desc+0x18] = 1
        e.w32(0x108E354, tbl)
        e.w32(0x108E358, self.max_pools)
        e.w32(0x108E35C, 0)
        self.tbl = tbl
        # globals
        for i, w in enumerate(spec.get('xor', [0, 0, 0, 0])):
            e.w32(0x108D9A0 + 4 * i, w)
        for base, ids in ((0x108D9E8, spec.get('plugins_e8', [])), (0x108D9DC, spec.get('plugins_dc', []))):
            data = e.alloc(12 * max(len(ids), 1))
            for i, pid in enumerate(ids):
                e.w32(data + 12 * i, pid)
            e.w32(base, data)
            e.w32(base + 4, len(ids))
        e.w32(0x108D904, 1 if spec.get('envs') else 0)
        self.envs_table = e.alloc(0x100)
        e.uc.mem_write(self.envs_table, bytes(0x100))
        e.w32(0x108D8FC, self.envs_table)
        plat = spec.get('platform')
        if plat is not None:
            p = e.alloc(len(plat) + 1)
            e.uc.mem_write(p, plat.encode() + b'\0')
            e.w32(0x108D868 + 0x7C, p)
        else:
            e.w32(0x108D868 + 0x7C, 0)
        e.w32(0x108D90C + 0x14, spec.get('media_pool', 0x55) & 0xFFFFFFFF)
        def consume(n):                  # a handler that reads its chunk body: the memory reader advances by n
            R = BM + 4
            take = min(n, e.r32(R + 8))
            e.w32(R + 0x18, e.r32(R + 0x18) + take)
            e.w32(R + 8, e.r32(R + 8) - take)
        # the music hook
        hook = spec.get('hook')
        if hook is None:
            e.w32(0x108D968, 0)
        else:
            e.w32(0x108D968, STUB_HOOK)
            seq = list(hook)

            def music_hook(emu):
                hdr = emu.reg(0)
                typ = e.r8(hdr)
                size = struct.unpack('<I', bytes(e.uc.mem_read(hdr + 1, 4)))[0]
                e.log.append(('hook', typ, size, emu.reg(2)))
                res = seq.pop(0) if seq else 1
                if res == 1:             # a hook that continues has read the object itself (its body is not in the binary slice): the stand-in consumes the object body
                    consume(size)
                return res
            e.hook(STUB_HOOK, music_hook)
        # unread functions
        def stmg(emu):                   # 0x9B0B14(BM): the chunk size is the loader's [sp+0x40]
            consume(e.r32(emu.reg_sp() + 0x40))
            e.log.append(('stmg',))
            return spec.get('stmg_ret', 1)

        def stid(emu):                   # 0x9B2410(BM, size, bank)
            consume(emu.reg(1))
            e.log.append(('stid', emu.reg(1)))
            return spec.get('stid_ret', 1)

        def data_streamed(emu):          # 0x9B6C34(BM, bank, size, flag)
            consume(emu.reg(2))
            e.log.append(('data9b6c34', emu.reg(2), emu.reg(3)))
            return spec.get('data_stream_ret', 1)
        e.hook(0x9B0B14, stmg)
        e.hook(0x9B2410, stid)
        e.hook(0x9B6C34, data_streamed)

        def plugin(emu):
            a = emu.reg(0)
            e.log.append(('plugin', cstr(a).decode() if a else None))
        e.hook(0xA57238, plugin)
        e.hook(0xA68150, lambda emu: e.log.append(('a68150', emu.reg(1), emu.reg(2), emu.reg(3), e.r32(emu.reg_sp()), e.r32(emu.reg_sp() + 4), e.r32(emu.reg_sp() + 8))))
        if not spec.get('real_destroy'):
            e.hook(0xA7AEC4, lambda emu: e.log.append(('destroy_pool', emu.reg(0))) or 1)
        # the file opens (0x9BC17C by name, 0x9BC348 by id): set R to memory mode over the file
        file_bytes = spec['file']
        e.uc.mem_write(DATA_AT, file_bytes)

        def open_reader(emu, kind):
            e.log.append((kind,))
            R = BM + 4
            e.w32(R + 0x18, DATA_AT)
            e.w32(R + 8, len(file_bytes))
            e.w32(R + 0x1C, 0)
            return spec.get('open_ret', 1)
        e.hook(0x9BC17C, lambda emu: open_reader(emu, 'open_name'))
        e.hook(0x9BC348, lambda emu: open_reader(emu, 'open_id'))
        # the registry: BM+0x44 {buckets, count}
        regs = spec.get('registry', [])
        self.reg_banks = []
        buckets = e.alloc(8)
        e.w32(buckets, 0)
        e.w32(BM + 0x44, buckets)
        e.w32(BM + 0x48, 1 if regs else 0)
        for (rid, rlang, flags50, w54) in regs:
            b = e.alloc(0x60)
            e.uc.mem_write(b, bytes(0x60))
            e.w32(b + 8, rid)
            e.w32(b + 0xC, rlang)
            e.w8(b + 0x50, flags50)
            e.w32(b + 0x54, w54)
            e.w32(b + 0x48, 1)
            e.w32(b + 0x10, e.r32(buckets))
            e.w32(buckets, b)
            self.reg_banks.append(b)

    def load(self):
        e, spec = self.e, self.spec
        s = [0] * 13
        s[2] = spec.get('open_flags', 0x10)
        s[3] = spec.get('type', 0)
        s[4] = spec.get('pool_arg', 0xFFFFFFFF)
        s[5] = spec.get('lang', DATA_AT)
        s[6] = len(spec['file'])
        s[9] = OUTBANK
        s[10] = spec.get('mode', 0)
        s[11] = 1 if spec.get('add_ref') else 0
        s[12] = spec.get('flag12', 0)
        e.w32(OUTBANK, 0xDEADBEEF)
        nameptr = 0xDEAD if spec.get('has_name') else 0
        r = e.call(0x9B74D8, BM, 0, spec.get('id', 4242), nameptr, stack=s)
        return r


def reader_dump(e):
    R = BM + 4
    return dict(left=e.r32(R + 8), mem=e.r32(R + 0x18), stream=e.r32(R + 0x1C), temp=e.r32(R + 0x14))


def bank_dump(e, b):
    if b == 0:
        return None
    return dict(id=e.r32(b + 8), lang=e.r32(b + 0xC), flags4=e.r8(b + 4), data14=e.r32(b + 0x14), didx18=e.r32(b + 0x18), size1c=e.r32(b + 0x1C), pool24=e.r32(b + 0x24), flag28=e.r8(b + 0x28),
                ctr2c=e.r32(b + 0x2C), cnt30=e.r32(b + 0x30), w34=e.r32(b + 0x34), ref48=e.r32(b + 0x48), w4c=e.r32(b + 0x4C), flags50=e.r8(b + 0x50), w54=e.r32(b + 0x54), cap44=e.r32(b + 0x44), cnt40=e.r32(b + 0x40))


def table_dump(e):
    n = e.r32(BM + 0x38)
    out = {'buckets': n, 'count': e.r32(BM + 0x40)}
    nodes = []
    if n:
        base = e.r32(BM + 0x34)
        for i in range(n):
            p = e.r32(base + 4 * i)
            while p:
                cnt = e.r32(p + 0x14)
                arr = e.r32(p + 0x10)
                nodes.append((e.r32(p + 4), e.r32(p + 0x1C), cnt, e.r32(p + 0x18), e.r32(p + 0x20)))
                p = e.r32(p)
    out['nodes'] = sorted(nodes)
    return out


def extra_dump(w):
    e = w.e
    t = e.r32(0x108D8FC)
    parts = ['en=' + ','.join(str(e.r8(t + i)) for i in range(6))]
    for k in range(6):
        ent = t + 8 + 12 * k
        ptr, cnt, sc = e.r32(ent), e.r32(ent + 4), e.r32(ent + 8)
        pts = bytes(e.uc.mem_read(ptr, 12 * cnt)).hex() if ptr and cnt else ''
        parts.append('%d:%d/%d/%s' % (k, cnt, sc, pts))
    p = e.r32(0x108D868 + 0x7C)
    parts.append('plat=' + (w.cstr(p).decode() if p else '-'))
    return ' '.join(parts)


def run(spec):
    w = World(spec)
    e = w.e
    r = w.load()
    b = e.r32(OUTBANK)
    res = dict(ret=r, out_is_set=(b != 0xDEADBEEF), bank=bank_dump(e, b) if b not in (0, 0xDEADBEEF) else None, out_zero=(b == 0), reader=reader_dump(e), flag64=e.r8(BM + 0x64),
               log=[l for l in e.log], allocs=len(w.allocs), table=table_dump(e))
    res['b'] = b
    res['extra'] = extra_dump(w)
    res['world'] = w
    return res


def basic_file(didx_n=3, data=None, extra=b''):
    didx = b''.join(struct.pack('<III', 1001 + i, 10 * i, 10) for i in range(didx_n))
    data = bytes(range(10 * didx_n)) if data is None else data
    return bkhd() + extra + chunk('DIDX', didx) + chunk('DATA', data)


SCENARIOS = {}


def sc(name, **kw):
    SCENARIOS[name] = kw


# --- K1/K12: the loader, creation, registry, the two memory types, modes
sc('type3_mode0_creates_and_loads', file=basic_file(), type=3, mode=0, id=4242)
sc('type2_window_mode', file=basic_file(), type=2, mode=0, id=4242, lang=DATA_AT)
sc('type0_mode0_open_by_name', file=basic_file(), type=0, mode=0, has_name=True)
sc('type0_mode0_open_by_id', file=basic_file(), type=0, mode=0, has_name=False)
sc('type1_mode0_open_by_id_even_with_a_name', file=basic_file(), type=1, mode=0, has_name=True)
sc('mode1_creates_other_fields', file=basic_file(), type=0, mode=1, has_name=True, add_ref=True, media_pool=0x66, data_stream_ret=1)
sc('mode1_add_ref_off', file=basic_file(), type=0, mode=1, has_name=True, add_ref=False)
sc('mode2_skips_didx_and_data', file=basic_file(), type=0, mode=2, has_name=True)
sc('mode3_didx_loads_data_streamed', file=basic_file(), type=0, mode=3, has_name=True)
sc('mode4_leaves_didx_unread', file=basic_file(), type=0, mode=4, has_name=True)
sc('open_fails_with_9', file=basic_file(), type=0, mode=0, has_name=True, open_ret=9)
sc('registry_hit_bit0_set_returns_45', file=basic_file(), type=3, mode=0, id=4242, registry=[(4242, 0, 1, 0)])
sc('registry_hit_loads_into_it_and_add_ref', file=basic_file(), type=3, mode=0, id=4242, registry=[(4242, 0, 0, 5)], add_ref=True)
sc('registry_miss_other_language', file=basic_file(), type=3, mode=0, id=4242, registry=[(4242, 9, 1, 0)])
sc('registry_type2_key_is_the_language_word', file=basic_file(), type=2, mode=0, id=4242, lang=DATA_AT, registry=[(4242, DATA_AT, 1, 0)])
sc('bank_object_allocation_fails_34', file=basic_file(), type=3, mode=0, alloc_fail=1)
# --- K2: BKHD
sc('bkhd_version_wrong_40', file=bkhd(version=0x77) + chunk('DIDX', b''), type=3)
sc('bkhd_extra_bytes_skipped', file=bkhd(extra=b'\xAA' * 8, align=16) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('bkhd_xor_decrypts', file=bkhd(xor=[0x11111111, 0x22222222, 0x33333333, 0x44444444]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, xor=[0x11111111, 0x22222222, 0x33333333, 0x44444444])
sc('bkhd_xor_enabled_but_file_plain_40', file=bkhd() + chunk('DIDX', b''), type=3, xor=[1, 0, 0, 0])
sc('bkhd_wrong_tag_7', file=chunk('XXXX', b'\0' * 20) + chunk('DIDX', b''), type=3)
sc('bkhd_short_header_38', file=fourcc('BKHD') + struct.pack('<I', 0x14) + b'\x00' * 6, type=3)
sc('bkhd_size_below_14_fails_38', file=fourcc('BKHD') + struct.pack('<I', 0x10) + struct.pack('<IIIHHI', 0x78, 1, 0, 0, 0, 0), type=3)
sc('bkhd_empty_file_7', file=b'', type=3)
# --- the chunk loop
sc('stmg_zero_size_skipped', file=bkhd() + chunk('STMG', b'') + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('stmg_nonzero_calls_the_handler', file=bkhd() + chunk('STMG', b'xxxx') + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, stmg_ret=1)
sc('stmg_handler_result_aborts', file=bkhd() + chunk('STMG', b'xxxx') + chunk('DIDX', struct.pack('<III', 1, 0, 4)), type=3, stmg_ret=9)
sc('stid_calls_the_handler', file=bkhd() + chunk('STID', b'yyyy') + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('unknown_chunk_skipped', file=bkhd() + chunk('ABCD', b'xyz') + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('unknown_chunk_truncated_7', file=bkhd() + fourcc('ABCD') + struct.pack('<I', 100) + b'xyz', type=3)
sc('chunk_header_truncated_7', file=bkhd() + b'DIDX1', type=3)
sc('file_ends_after_bkhd_1', file=bkhd(), type=3)
sc('two_didx_second_skipped_after_writer', file=bkhd() + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd') + chunk('DIDX', struct.pack('<III', 2, 0, 4)), type=3)
sc('data_before_didx_no_writer', file=bkhd() + chunk('DATA', b'abcd') + chunk('DIDX', struct.pack('<III', 1, 0, 4)), type=3)
sc('data_size_zero', file=bkhd() + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b''), type=3)
sc('data_truncated_7', file=bkhd() + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + fourcc('DATA') + struct.pack('<I', 35) + bytes(20), type=3)
sc('didx_size_not_multiple_of_12', file=bkhd() + chunk('DIDX', struct.pack('<III', 1, 0, 4) + b'\x00' * 6) + chunk('DATA', b'abcd'), type=3)
sc('didx_size_zero_34', file=bkhd() + chunk('DIDX', b'') + chunk('DATA', b'abcd'), type=3)
sc('didx_size_below_12_34', file=bkhd() + chunk('DIDX', struct.pack('<IH', 1, 0)) + chunk('DATA', b'abcd'), type=3)
sc('didx_size_below_12_window_mode', file=bkhd() + chunk('DIDX', struct.pack('<IH', 1, 0)) + chunk('DATA', b'abcd'), type=2)
sc('didx_alloc_fails_34', file=bkhd() + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, alloc_fail=2)
sc('pool_table_zero_create_fails_34', file=basic_file(), type=3, max_pools=0)
sc('data_pool_blocksize', file=bkhd() + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, max_pools=2)
# --- K6 INIT
init_body = lambda plugins: struct.pack('<I', len(plugins)) + b''.join(struct.pack('<II', pid, len(name)) + name for pid, name in plugins)
sc('init_all_known', file=bkhd() + chunk('INIT', init_body([(0x112C3, b'HijackAudio\0'), (0x640002, b'Sine\0')])) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, plugins_e8=[0x112C3], plugins_dc=[0x640002])
sc('init_unknown_ids_call_a57238', file=bkhd() + chunk('INIT', init_body([(0x112C3, b'HijackAudio\0'), (0x640002, b'Sine\0'), (0x9999, b'')])) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, plugins_e8=[0x112C3])
sc('init_count_zero', file=bkhd() + chunk('INIT', struct.pack('<I', 0)) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('init_in_second_registry', file=bkhd() + chunk('INIT', init_body([(0x77, b'X\0')])) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, plugins_dc=[0x77])
# --- K7 ENVS
def envs_body(items):
    out = b''
    for en, scaling, pts in items:
        out += bytes([en, scaling]) + struct.pack('<H', len(pts)) + b''.join(struct.pack('<ffI', x, y, i) for x, y, i in pts)
    return out
pts_a = [(0.0, 0.0, 4), (100.0, -1.0, 4)]
pts_b = [(0.0, 0.0, 4), (100.0, 100.0, 4)]
shipped = [(1, 2, pts_a), (1, 0, pts_b), (0, 0, pts_b)] * 2
sc('envs_shipped_shape', file=bkhd() + chunk('ENVS', envs_body(shipped)) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, envs=True)
sc('envs_no_manager_2', file=bkhd() + chunk('ENVS', envs_body(shipped)) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, envs=False)
sc('envs_idx1_scaling2_kept', file=bkhd() + chunk('ENVS', envs_body([(1, 0, pts_a), (1, 2, pts_a), (1, 3, pts_b)] * 2)) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, envs=True)
sc('envs_n_zero_gives_34', file=bkhd() + chunk('ENVS', envs_body([(1, 0, [])] + [(1, 0, pts_b)] * 5)) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, envs=True)
sc('envs_truncated', file=bkhd() + fourcc('ENVS') + struct.pack('<I', 40) + bytes([1, 0, 2, 0]), type=3, envs=True)
sc('envs_size_zero_2', file=bkhd() + chunk('ENVS', b'') + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, envs=True)
# --- K8 PLAT
plat = lambda s: chunk('PLAT', struct.pack('<I', len(s)) + s)
sc('plat_first_stores', file=bkhd() + plat(b'Android\0') + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('plat_equal_1', file=bkhd() + plat(b'Android\0') + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, platform='Android')
sc('plat_different_53', file=bkhd() + plat(b'Android\0') + chunk('DIDX', struct.pack('<III', 1, 0, 4)), type=3, platform='iOS')
sc('plat_len_zero', file=bkhd() + chunk('PLAT', struct.pack('<I', 0)) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('plat_size_zero', file=bkhd() + chunk('PLAT', b'') + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('plat_store_alloc_fails_34', file=bkhd() + plat(b'Android\0') + chunk('DIDX', struct.pack('<III', 1, 0, 4)), type=3, alloc_fail=2)
# --- K9/K10 HIRC (music types and the hook)
def hirc(objs):
    body = struct.pack('<I', len(objs))
    for typ, payload in objs:
        body += bytes([typ]) + struct.pack('<I', len(payload)) + payload
    return chunk('HIRC', body)
sc('hirc_count_zero', file=bkhd() + hirc([]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('hirc_no_hook_skips_music_objects', file=bkhd() + hirc([(10, b'aaaa'), (11, b'bbbbbb'), (12, b'cc'), (13, b'')]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('hirc_no_hook_type_above_23_skipped', file=bkhd() + hirc([(30, b'aaaa'), (10, b'zz')]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('hirc_no_hook_type_0_skipped', file=bkhd() + hirc([(0, b'aaaa')]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3)
sc('hirc_hook_returns_1_continues', file=bkhd() + hirc([(10, b'aaaa'), (11, b'bb')]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, hook=[1, 1])
sc('hirc_hook_returns_3_skips_then_aborts_with_3', file=bkhd() + hirc([(10, b'aaaa'), (11, b'bb')]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd'), type=3, hook=[3, 1])
sc('hirc_hook_returns_2_aborts_without_skip', file=bkhd() + hirc([(10, b'aaaa'), (11, b'bb')]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)), type=3, hook=[2, 1])
sc('hirc_hook_second_object_returns_3', file=bkhd() + hirc([(10, b'aaaa'), (11, b'bb'), (12, b'c')]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)), type=3, hook=[1, 3, 1])
sc('hirc_skip_short_gives_7', file=bkhd() + chunk('HIRC', struct.pack('<I', 1) + bytes([10]) + struct.pack('<I', 100) + b'xy'), type=3)
sc('hirc_header_truncated', file=bkhd() + chunk('HIRC', struct.pack('<I', 2) + bytes([10]) + struct.pack('<I', 2) + b'xy' + b'\x0b\x00'), type=3, hook=[1, 1])
sc('hirc_array_alloc_fails_2', file=bkhd() + hirc([(10, b'aaaa')]) + chunk('DIDX', struct.pack('<III', 1, 0, 4)), type=3, alloc_fail=2)
sc('hirc_count_fewer_than_objects_in_chunk', file=bkhd() + chunk('HIRC', struct.pack('<I', 1) + bytes([10]) + struct.pack('<I', 2) + b'xy' + bytes([11]) + struct.pack('<I', 2) + b'zz') + chunk('DIDX', struct.pack('<III', 1, 0, 4)) , type=3, hook=[1, 1])
two = bkhd() + chunk('DIDX', struct.pack('<III', 1001, 0, 4) + struct.pack('<III', 1002, 4, 4)) + chunk('DATA', b'abcdefgh')
sc('two_entries_ok', file=two, type=3)
for k in range(1, 13):
    sc('two_entries_alloc_fail_%d' % k, file=two, type=3, alloc_fail=k)
two_envs = bkhd() + chunk('ENVS', envs_body(shipped)) + chunk('ENVS', envs_body([(1, 0, pts_b)] * 6)) + chunk('DIDX', struct.pack('<III', 1, 0, 4)) + chunk('DATA', b'abcd')
for k in range(1, 31):
    sc('envs_twice_alloc_fail_%d' % k, file=two_envs, type=3, envs=True, alloc_fail=k)
# the shipped Init.bnk and Music/SFX shapes are covered by the shipped-data test: here the ordering of chunks as in a bank


def cs_bits(v):
    return '0x%08XU' % (v & 0xFFFFFFFF)


def cs_str(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def bank_str(b):
    if b is None:
        return 'null'
    keys = ('id', 'lang', 'flags4', 'data14', 'didx18', 'size1c', 'pool24', 'flag28', 'ctr2c', 'cnt30', 'w34', 'ref48', 'w4c', 'flags50', 'w54', 'cap44', 'cnt40')
    return cs_str(' '.join('%s=%d' % (k, b[k] if k not in ('data14', 'didx18') else (1 if b[k] else 0)) for k in keys))


def log_str(log):
    items = []
    for l in log:
        if l[0] == 'plugin':
            items.append('plugin:%s' % l[1])
        elif l[0] == 'hook':
            items.append('hook:%d/%d' % (l[1], l[2]))
        elif l[0] == 'data9b6c34':
            items.append('data9b6c34:%d/%d' % (l[1], l[2]))
        elif l[0] in ('stid',):
            items.append('stid:%d' % l[1])
        elif l[0] == 'a68150':
            items.append('a68150')
        else:
            items.append(l[0] if len(l) == 1 else ':'.join(str(x) for x in l))
    return 'new string[] { %s }' % ', '.join(cs_str(x) for x in items) if items else 'Array.Empty<string>()'


def table_str(t):
    return cs_str('buckets=%d count=%d nodes=%s' % (t['buckets'], t['count'], ';'.join('%d/%d/%d/%d/%d' % n for n in t['nodes'])))


# --- K15: the bank release 0x9B47D8(bank, force) on a loaded bank (DestroyPool 0xA7AEC4's tail is unread: its stand-in only logs)
RELEASES = {}


def rel(name, base, ref48=None, w34=None, w4c=None, flags50=None, force=0):
    RELEASES[name] = dict(base=base, ref48=ref48, w34=w34, w4c=w4c, flags50=flags50, force=force)


rel('ref2_drops_to_1', 'type3_mode0_creates_and_loads', ref48=2)
rel('ref1_loaded_mode0_returns_block_and_destroys_pool', 'type3_mode0_creates_and_loads', ref48=1)
rel('ref0_loaded_mode0', 'type3_mode0_creates_and_loads', ref48=0)
rel('mode1_w4c1_w34_force0_runs_a68150', 'mode1_creates_other_fields', ref48=1, w34=7, force=0)
rel('mode1_w4c1_w34_force1_skips_a68150', 'mode1_creates_other_fields', ref48=1, w34=7, force=1)
rel('mode1_w4c1_w34_zero_force0', 'mode1_creates_other_fields', ref48=1, w34=0, force=0)
rel('mode1_w4c0_force1_frees_didx_and_object', 'mode1_creates_other_fields', ref48=1, w4c=0, w34=7, force=1)
rel('mode1_w4c0_force0_a68150_then_free', 'mode1_creates_other_fields', ref48=1, w4c=0, w34=7, force=0)
rel('mode1_w4c0_didx_not_owned', 'mode1_creates_other_fields', ref48=1, w4c=0, flags50=0, force=1)
rel('mode1_w4c1_then_negative_ref', 'mode1_creates_other_fields', ref48=0, w34=3, force=0)
rel('window_mode_bank_has_no_owned_didx', 'type2_window_mode', ref48=1, w4c=0, force=1)


def run_release(name, r):
    spec = SCENARIOS[r['base']]
    w = World(spec)
    e = w.e
    w.load()
    b = e.r32(OUTBANK)
    if b in (0, 0xDEADBEEF):
        return None
    if r['ref48'] is not None:
        e.w32(b + 0x48, r['ref48'])
    if r['w34'] is not None:
        e.w32(b + 0x34, r['w34'])
    if r['w4c'] is not None:
        e.w32(b + 0x4C, r['w4c'])
    if r['flags50'] is not None:
        e.w8(b + 0x50, r['flags50'])
    didx = e.r32(b + 0x18)
    data = e.r32(b + 0x14)
    pool = e.r32(b + 0x24)
    e.log.clear()
    w.frees.clear()
    e.call(0x9B47D8, b, r['force'])
    used = e.r32(e.r32(0x108E354) + 0x34 * pool + 0x2C) if pool != 0xFFFFFFFF and pool < w.max_pools else 0
    return dict(bank=bank_dump(e, b) if b not in w.frees else None, freed_obj=b in w.frees, freed_didx=(didx != 0 and didx in w.frees), freed_data=(data != 0 and data in w.frees), used=used,
                table=table_dump(e), log=list(e.log), frees=len(w.frees), pool=pool)



# --- K3: 0x9B2314(BM, size, _, bank), the pool read the DATA handler shares
K3 = {}


def k3(name, calls, data=16, max_pools=8, alloc_fail=None, preset=None, pool_id=None):
    K3[name] = dict(calls=calls, data=data, max_pools=max_pools, alloc_fail=alloc_fail, preset=preset, pool_id=pool_id)


k3('size_zero_returns_1', [0], data=16)
k3('creates_pool_and_reads', [16], data=20)
k3('creates_pool_short_data_7', [16], data=8)
k3('pool_exists_block_fits', [16], data=20, preset=(64, 32))
k3('pool_exists_block_exact', [16], data=20, preset=(64, 16))
k3('pool_exists_block_too_small_34', [16], data=20, preset=(64, 8))
k3('pool_id_without_descriptor_0xE', [16], data=20, pool_id=5)
k3('pool_id_beyond_table_0xE', [16], data=20, pool_id=9)
k3('pool_create_fails_34', [16], data=20, max_pools=0)
k3('pool_memory_alloc_fails_34', [16], data=20, alloc_fail=1)
k3('shared_pool_exhausted_34', [16, 16, 16], data=64, preset=(32, 16))
k3('second_call_same_bank_pool_kept', [16, 16], data=40)


def run_k3(sc):
    w = World(dict(file=b'', max_pools=sc['max_pools'], real_destroy=True))
    e = w.e
    data = bytes((i * 5 + 1) & 0xFF for i in range(sc['data']))
    e.uc.mem_write(DATA_AT, data)
    e.call(0x9BBB90, BM + 4, DATA_AT, len(data))
    shared = None
    if sc['preset']:
        size, block = sc['preset']
        shared = e.call(0xA7AC98, 0, size, block, 9, stack=[0x10])
    out = []
    bank = None
    e.fail_alloc_at = sc['alloc_fail']
    e.alloc_calls = 0
    for i, size in enumerate(sc['calls']):
        if bank is None or sc['preset']:
            bank = e.alloc(0x60)
            e.uc.mem_write(bank, bytes(0x60))
            e.w32(bank + 0x24, 0xFFFFFFFF if shared is None else shared)
            if sc['pool_id'] is not None:
                e.w32(bank + 0x24, sc['pool_id'])
        before = len(w.allocs)
        r = e.call(0x9B2314, BM, size, 0, bank)
        b = bank_dump(e, bank)
        pool = e.r32(bank + 0x24)
        used = e.r32(e.r32(0x108E354) + 0x34 * pool + 0x2C) if pool < w.max_pools else 0
        d14 = e.r32(bank + 0x14)
        buf = bytes(e.uc.mem_read(d14, size)).hex() if r == 1 and d14 and size else ''
        out.append('ret=%d %s left=%d allocs=%d used=%d buf=%s' % (r, bank_str(b)[1:-1], e.r32(BM + 4 + 8), len(w.allocs) - before, used, buf))
    return out


# --- K16: the pool table functions
POOLS = {}


def pools(name, ops, max_pools=8, alloc_fail=None):
    POOLS[name] = dict(ops=ops, max_pools=max_pools, alloc_fail=alloc_fail)


pools('create_pop_push', ['create:64:16:9:16', 'check:0', 'attrs:0', 'bsize:0', 'pop:0', 'pop:0', 'pop:0', 'pop:0', 'pop:0', 'push:0:2', 'pop:0', 'push:0:0', 'push:0:3', 'pop:0', 'pop:0'])
pools('align_0x20_one_block', ['create:30:16:9:32', 'pop:0', 'pop:0', 'check:0'])
pools('align_zero', ['create:32:8:9:0', 'pop:0', 'pop:0', 'pop:0', 'pop:0', 'pop:0'])
pools('size_below_block_gives_no_blocks', ['create:8:16:9:16', 'check:0', 'pop:0', 'create:16:16:9:16', 'check:0', 'check:1'])
pools('ptr_null_attr_bit0_clear_fails', ['create:64:16:8:16', 'create:64:16:0:16', 'check:0'])
pools('table_full_fails', ['create:32:16:9:16', 'create:32:16:9:16', 'create:32:16:9:16', 'check:1', 'check:2'], max_pools=2)
pools('no_table_fails', ['create:32:16:9:16', 'check:0'], max_pools=0)
pools('thunk_alloc_fails', ['create:32:16:9:16', 'create:32:16:9:16', 'check:0', 'check:1'], alloc_fail=1)
pools('check_out_of_table_0xE', ['check:0', 'check:7', 'check:8', 'check:100', 'create:16:16:9:16', 'check:0', 'check:1'])
pools('two_pools_independent', ['create:48:16:9:16', 'create:64:32:9:16', 'pop:0', 'pop:1', 'pop:1', 'pop:1', 'push:1:1', 'pop:1', 'pop:0', 'attrs:1', 'bsize:1'])
pools('free_slot_reused_by_block_count', ['create:32:16:9:16', 'create:16:16:9:16', 'check:0', 'check:1'])


def dump_pool(e, w, p):
    d = e.r32(0x108E354) + 0x34 * p
    cnt, bs, attr, flags, align, used, cap = e.r32(d), e.r32(d + 4), e.r32(d + 0x18), e.r8(d + 0x1C), e.r32(d + 0x28), e.r32(d + 0x2C), e.r32(d + 0x30)
    if cnt == 0 and bs == 0:
        return '-'
    aligned = e.r32(d + 0xC)
    free = []
    blk = e.r32(d + 0x14)
    guard = 0
    while blk and guard < 64:
        free.append((blk - aligned) // bs if bs else 0)
        blk = e.r32(blk)
        guard += 1
    return '[%d/%d/%d/%d/%d/%d/%d free=%s]' % (cnt, bs, attr, flags, align, used, cap, ','.join(str(x) for x in free))


def run_pools(sc):
    w = World(dict(file=b'', max_pools=sc['max_pools'], real_destroy=True))
    e = w.e
    e.fail_alloc_at = sc['alloc_fail']
    e.alloc_calls = 0
    out = []
    for op in sc['ops']:
        k, *a = op.split(':')
        a = [int(x) for x in a]
        if k == 'create':
            r = e.call(0xA7AC98, 0, a[0], a[1], a[2], stack=[a[3]])
            res = 'id=%d' % (r - (1 << 32) if r >= 0x80000000 else r)
        elif k == 'check':
            res = 'r=%d' % e.call(0xA7AAE8, a[0])
        elif k == 'attrs':
            res = 'attr=%d' % e.call(0xA7A7C8, a[0])
        elif k == 'bsize':
            res = 'bsize=%d' % e.call(0xA7AA9C, a[0])
        elif k == 'pop':
            d = e.r32(0x108E354) + 0x34 * a[0]
            r = e.call(0xA7A9FC, a[0])
            res = 'blk=%s' % ('null' if r == 0 else (r - e.r32(d + 0xC)) // max(e.r32(d + 4), 1))
        elif k == 'push':
            d = e.r32(0x108E354) + 0x34 * a[0]
            e.call(0xA7AA48, a[0], e.r32(d + 0xC) + a[1] * e.r32(d + 4))
            res = 'pushed'
        out.append('%s %s %s %s' % (res, dump_pool(e, w, 0), dump_pool(e, w, 1), dump_pool(e, w, 2)))
    return out



def stmg_size(file):
    i = file.find(b'STMG', 20)
    return struct.unpack_from('<I', file, i + 4)[0] if i >= 0 else 0


def spec_str(spec):
    f = spec['file']
    reg = ', '.join('(%s, %s, (byte)%d, %s)' % (cs_bits(a), cs_bits(b), c, cs_bits(d)) for a, b, c, d in spec.get('registry', []))
    hook = spec.get('hook')
    xor = spec.get('xor', [0, 0, 0, 0])
    return ('new BankSpec { File = "%s", Type = %d, Mode = %d, Id = %s, HasName = %s, AddRef = %s, MediaPool = %s, OpenRet = %d, StmgRet = %d, StidRet = %d, DataStreamRet = %d, AllocFail = %d, MaxPools = %d, '
            'Xor = new uint[] { %s }, PluginsE8 = new uint[] { %s }, PluginsDC = new uint[] { %s }, Envs = %s, Platform = %s, Hook = %s, Registry = new (uint, uint, byte, uint)[] { %s }, Lang = %s, PoolArg = %s, StmgSize = %d }'
            % (f.hex(), spec.get('type', 0), spec.get('mode', 0), cs_bits(spec.get('id', 4242)), 'true' if spec.get('has_name') else 'false', 'true' if spec.get('add_ref') else 'false',
               cs_bits(spec.get('media_pool', 0x55)), spec.get('open_ret', 1), spec.get('stmg_ret', 1), spec.get('stid_ret', 1), spec.get('data_stream_ret', 1), spec.get('alloc_fail') or 0, spec.get('max_pools', 8),
               ', '.join(cs_bits(x) for x in xor), ', '.join(cs_bits(x) for x in spec.get('plugins_e8', [])), ', '.join(cs_bits(x) for x in spec.get('plugins_dc', [])), 'true' if spec.get('envs') else 'false',
               cs_str(spec['platform']) if spec.get('platform') is not None else 'null', ('new int[] { %s }' % ', '.join(str(x) for x in hook)) if hook is not None else 'null', reg,
               cs_bits(spec.get('lang', DATA_AT)), cs_bits(spec.get('pool_arg', 0xFFFFFFFF)), stmg_size(f)))


def main():
    print('// <auto-generated> by re-analysis/tools/emu/emu_bankload.py from the engine\'s own output (the real loader 0x9B74D8, BKHD, INIT, ENVS, PLAT, HIRC, the writer, the pool functions and the reader under Unicorn): do not edit. </auto-generated>')
    print('namespace Cozmo.Protocol.Tests;')
    print('')
    print('internal static class BankLoaderOracle')
    print('{')
    print('    /// <summary>name -> (return, bank state or null, out was written, media table, BM+0x64 flag, reader left, log of the unread callees and handlers in order, allocation calls, the ENVS table and the stored platform string).</summary>')
    print('    public static readonly Dictionary<string, (uint Ret, string? Bank, bool OutWritten, bool OutZero, string Table, byte Flag64, uint ReaderLeft, string[] Log, int Allocs, string Extra)> Loads = new()')
    print('    {')
    for name, spec in SCENARIOS.items():
        r = run(spec)
        w = r.pop('world')
        print('        ["%s"] = (%s, %s, %s, %s, %s, %d, %d, %s, %d, %s),' % (name, cs_bits(r['ret']), bank_str(r['bank']), 'true' if r['out_is_set'] else 'false', 'true' if r['out_zero'] else 'false', table_str(r['table']),
              r['flag64'], r['reader']['left'], log_str(r['log']), r['allocs'], cs_str(r['extra'])))
    print('    };')
    print('')
    print('    /// <summary>name -> (base scenario, ref48 before or -1, w34 before or -1, w4c before or -1, flags50 before or -1, force, bank after or null when freed, object freed, DIDX freed, data returned to the heap, pool used after, table, log, frees).</summary>')
    print('    public static readonly Dictionary<string, (string Base, long Ref48, long W34, long W4c, long Flags50, int Force, string? Bank, bool FreedObject, bool FreedDidx, bool FreedData, uint PoolUsed, string Table, string[] Log, int Frees)> Releases = new()')
    print('    {')
    for name, r in RELEASES.items():
        x = run_release(name, r)
        if x is None:
            raise SystemExit('release scenario %s has no bank' % name)
        print('        ["%s"] = (%s, %d, %d, %d, %d, %d, %s, %s, %s, %s, %d, %s, %s, %d),' % (name, cs_str(r['base']), -1 if r['ref48'] is None else r['ref48'], -1 if r['w34'] is None else r['w34'], -1 if r['w4c'] is None else r['w4c'],
              -1 if r['flags50'] is None else r['flags50'], r['force'], bank_str(x['bank']), 'true' if x['freed_obj'] else 'false', 'true' if x['freed_didx'] else 'false', 'true' if x['freed_data'] else 'false', x['used'],
              table_str(x['table']), log_str(x['log']), x['frees']))
    print('    };')
    print('')
    print('    /// <summary>name -> (the calls sizes, data length, max pools, alloc fail, preset pool (size, block) or null, pool id override or -1, the engine result of each call).</summary>')
    print('    public static readonly Dictionary<string, (int[] Calls, int Data, int MaxPools, int AllocFail, (uint Size, uint Block)? Preset, long PoolId, string[] Steps)> Chunk2314 = new()')
    print('    {')
    for name, sc_ in K3.items():
        steps = run_k3(sc_)
        pre = 'null' if sc_['preset'] is None else '(%du, %du)' % sc_['preset']
        print('        ["%s"] = (new int[] { %s }, %d, %d, %d, %s, %d, new string[] {\n            %s }),' % (name, ', '.join(str(x) for x in sc_['calls']), sc_['data'], sc_['max_pools'], sc_['alloc_fail'] or 0, pre,
              -1 if sc_['pool_id'] is None else sc_['pool_id'], ',\n            '.join(cs_str(x) for x in steps)))
    print('    };')
    print('')
    print('    /// <summary>name -> (max pools, alloc fail, the operations, the engine result of each: the result and the first three descriptors).</summary>')
    print('    public static readonly Dictionary<string, (int MaxPools, int AllocFail, string[] Ops, string[] Steps)> Pools = new()')
    print('    {')
    for name, sc_ in POOLS.items():
        steps = run_pools(sc_)
        print('        ["%s"] = (%d, %d, new string[] { %s }, new string[] {\n            %s }),' % (name, sc_['max_pools'], sc_['alloc_fail'] or 0, ', '.join(cs_str(o) for o in sc_['ops']), ',\n            '.join(cs_str(x) for x in steps)))
    print('    };')
    print('')
    print('    public static readonly Dictionary<string, BankSpec> Specs = new()')
    print('    {')
    for name, spec in SCENARIOS.items():
        print('        ["%s"] = %s,' % (name, spec_str(spec)))
    print('    };')
    print('}')
    print('')
    print('/// <summary>The inputs of one scenario of emu_bankload.py (the same values the engine ran with).</summary>')
    print('internal sealed class BankSpec')
    print('{')
    print('    public string File { get; init; } = "";')
    print('    public int Type { get; init; }')
    print('    public int Mode { get; init; }')
    print('    public uint Id { get; init; }')
    print('    public bool HasName { get; init; }')
    print('    public bool AddRef { get; init; }')
    print('    public uint MediaPool { get; init; }')
    print('    public int OpenRet { get; init; }')
    print('    public int StmgRet { get; init; }')
    print('    public int StidRet { get; init; }')
    print('    public int DataStreamRet { get; init; }')
    print('    public int AllocFail { get; init; }')
    print('    public int MaxPools { get; init; }')
    print('    public uint[] Xor { get; init; } = new uint[4];')
    print('    public uint[] PluginsE8 { get; init; } = Array.Empty<uint>();')
    print('    public uint[] PluginsDC { get; init; } = Array.Empty<uint>();')
    print('    public bool Envs { get; init; }')
    print('    public string? Platform { get; init; }')
    print('    public int[]? Hook { get; init; }')
    print('    public (uint Id, uint Lang, byte Flags50, uint Word54)[] Registry { get; init; } = Array.Empty<(uint, uint, byte, uint)>();')
    print('    public uint Lang { get; init; }')
    print('    public uint PoolArg { get; init; }')
    print('    public int StmgSize { get; init; }')
    print('}')


if __name__ == '__main__':
    main()
