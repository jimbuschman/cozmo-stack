"""The source-class scenario groups of emu_decode.py that run the engine's own source classes under Unicorn (B-M6b-4 batch 5e): the streamed Vorbis class's decode (0xAB1550 through the real stream layer,
0xAB7E40, 0xA73490), its vt+0x74 (0xAB1138) and seek lookup (0xAB1020), the in-memory Vorbis class (0xAB0B20, 0xAB0A30, 0xAB04F0, 0xAB0448, 0xAB0374, 0xAB032C, 0xAB0FC0), the start position 0xA736D4, the ADPCM stream class
(0xA73ABC is covered by emu_stream.py's sc_adpcm, now with the real parse; here 0xA73D34, 0xA739E8, 0xA742C8, 0xA73A14) and the output hand-off on the sources' own states.

Every step string is formatted by this module and, identically, by WwiseVorbisEngineTests.
"""
import hashlib
import struct
import sys

sys.dont_write_bytecode = True
import emu_stream as ms
import emu_decode as d
from emu_common import BASE
from unicorn.arm_const import *

STATE = BASE + 0x9800          # the voice's io state (0x40 bytes)


def sha16(b):
    return hashlib.sha256(b).hexdigest()[:16]


def io_string(e, state, channels_from_cfg=True, adpcm=False):
    code = e.r32(state + 0x28)
    frames = e.r16(state + 0xE)
    ptr = e.r32(state)
    cc = e.r32(state + 4)
    h = '-'
    if ptr and frames:
        if adpcm:
            n = frames * ((e.r16(ms.BASE_PBI + 0x160) >> 6) & 0x3FF)
        else:
            n = frames * 4 * (cc & 0xFF)
        h = sha16(bytes(e.uc.mem_read(ptr, n)))
    return 'code=%d f=%d mx=%d cc=%x pos=%d tot=%d rate=%d mc=%d h=%s' % (code, frames, e.r16(state + 0xC), cc, e.r32(state + 0x18), e.r32(state + 0x20), e.r32(state + 0x24), e.r16(state + 0x10), h)


def signed(v):
    return v - (1 << 32) if v & 0x80000000 else v


# ------------------------------------------------------------------------------------------------ the streamed Vorbis class


def stream_src_string(w):
    e, so = w.e, w.so
    return 'S18=%d h38=%d h5c=%d b5e=%d L44=%d f48=%d | st=%d fr=%d fs=%d fc=%d sk=%d tr=%d se=%d/%d | a4=%d' % (
        e.r32(so + 0x18), e.r16(so + 0x38), e.r16(so + 0x5C), e.r8(so + 0x5E) & 0x1F, e.r32(so + 0x44), e.r32(so + 0x48),
        e.r32(so + 0x68), e.r32(so + 0x60), e.r32(so + 0x64), e.r32(so + 0x6C), e.r16(so + 0x70 + 0x2C), e.r16(so + 0x70 + 0x2E),
        signed(e.r32(so + 0x70 + 0x1C)), signed(e.r32(so + 0x70 + 0x20)), 1 if e.r32(so + 0xA4) else 0)


def new_stream_world(wem, loops=1, flags_1bd=0x04, prefix_len=0, flags4=0):
    w = ms.SrcWorld(wem, prefix_len, flags_1bd=flags_1bd, loop_count=loops, flags4=flags4, real_decode=True)
    e = w.e
    e.uc.mem_write(STATE, bytes(0x40))
    return w


def decode_call(w):
    e = w.e
    e.w32(STATE + 0x28, 0xABCD)
    e.w16(STATE + 0xC, 1024)
    e.call(0xAB1550, w.so, STATE)
    return io_string(e, STATE) + ' | ' + stream_src_string(w)


def stream_cases():
    wem = ms.zip_entry('English(US)/99908739.wem')
    C = {}
    out = {}

    def header_from_first_buffer(w, size):
        w.start()                                       # 0x3F: no prefetch
        w.io_open_and_deliver(0, size)
        return w.start()                                # the header, from the first buffer

    def scenario(name, buffers, calls, wem_bytes=None, setup=None, extra_deliver=None, fail_alloc=0, corrupt=0):
        w = new_stream_world(wem_bytes or wem)
        r = header_from_first_buffer(w, buffers[0][1])
        steps = ['start=%d | %s' % (r, stream_src_string(w))]
        s3c = w.e.r32(w.so + 0x3C) - 0x30
        for st, sz in buffers[1:]:
            w.deliver(s3c, st, sz)
        if fail_alloc:
            w.arm_fail(fail_alloc)
        for i in range(calls):
            if extra_deliver and i in extra_deliver:
                for st, sz in extra_deliver[i]:
                    w.deliver(s3c, st, sz)
            steps.append(decode_call(w))
        out[name] = {'file': 'English(US)/99908739.wem', 'buffers': buffers, 'calls': calls, 'steps': steps, 'extra': extra_deliver or {}, 'fail_alloc': fail_alloc, 'corrupt': corrupt}
        print('stream', name, file=sys.stderr, flush=True)

    scenario('sv_first_calls', [(0, 4096)], 6)
    scenario('sv_whole_file_in_three_buffers', [(0, 4096), (4096, 4096), (8192, 1097)], 70)
    scenario('sv_packet_spans_the_buffer_end', [(0, 380)], 2, extra_deliver={1: [(380, 4096)]})
    scenario('sv_size_prefix_split', [(0, 348)], 3, extra_deliver={1: [(348, 4096)]})

    bad = bytearray(wem)
    # the first audio packet's u16 size is set above wMaxPacketSize: 0xAB7E40 gives status 2
    f = d.Fmt(*d.wem_chunks(wem))
    data_off = 94
    bad[data_off + f.audio_off] = 0xFF
    bad[data_off + f.audio_off + 1] = 0xFF
    scenario('sv_corrupt_packet_size', [(0, 4096)], 2, wem_bytes=bytes(bad), corrupt=data_off + f.audio_off)

    scenario('sv_output_alloc_fails', [(0, 4096)], 3, fail_alloc=1)
    return out


def stream_start_offset_cases():
    """The start-offset tail of the streamed class: bit 7 of [pbi+0x1BD] with an offset; the second StartStream (the header from the first buffer) runs 0xA74BA0 -> 0xA736D4 -> 0xAB1020 -> SetPosition."""
    wem = ms.zip_entry('English(US)/99908739.wem')
    out = {}
    for name, w1b4, f1be in (('sv_start_offset_zero', 0, 0), ('sv_start_offset_first_entry', 100, 0), ('sv_start_offset_mid', 15000, 0), ('sv_start_offset_last_entry', 33000, 0),
                             ('sv_start_offset_beyond_total', 40000, 0), ('sv_start_offset_float_quarter', 0x3E800000, 1), ('sv_start_offset_float_beyond', 0x40000000, 1)):
        w = new_stream_world(wem, flags_1bd=0xC4)
        w.e.w32(ms.BASE_PBI + 0x1B4, w1b4)
        w.e.w8(ms.BASE_PBI + 0x1BE, f1be)
        w.e.hook(0x4BAA4C, _uldivmod)
        w.start()
        w.io_open_and_deliver(0, 4096)
        r = w.start()
        s3c = w.e.r32(w.so + 0x3C) - 0x30
        sp = w.e.r32(s3c + 0x38) | (w.e.r32(s3c + 0x3C) << 32)
        steps = ['start=%d | %s | pbi1b4=%d pbi1bd=%d pbi1be=%d sp=%d' % (r, stream_src_string(w), w.e.r32(ms.BASE_PBI + 0x1B4), w.e.r8(ms.BASE_PBI + 0x1BD), w.e.r8(ms.BASE_PBI + 0x1BE), sp)]
        out[name] = {'file': 'English(US)/99908739.wem', 'w1b4': w1b4, 'f1be': f1be, 'steps': steps}
        print('startoffset', name, file=sys.stderr, flush=True)
    return out


def stream_vt74_cases():
    """0xAB1138 (vt+0x74) and 0xAB1020 (vt+0x7C) on a started source (the 600-byte prefix covers the seek table and the setup packet)."""
    wem = ms.zip_entry('English(US)/99908739.wem')
    out = {}
    for loops in (1, 3, 0, 2):
        steps = []
        for r1 in (1, 0, 0, 1, 0):
            w = ms.SrcWorld(wem, 600, loop_count=loops, flags_1bd=0x04, real_decode=True)
            assert w.start() == 1
            e = w.e
            so = w.so
            e.w16(so + 0x5C, 0)
            ret = e.call(0xAB1138, so, r1)
            steps.append('r1=%d ret=%x h38=%d h5c=%d st=%d fs=%d sk=%d tr=%d se=%d/%d' % (
                r1, ret, e.r16(so + 0x38), e.r16(so + 0x5C), e.r32(so + 0x68), e.r32(so + 0x64), e.r16(so + 0x70 + 0x2C), e.r16(so + 0x70 + 0x2E),
                signed(e.r32(so + 0x70 + 0x1C)), signed(e.r32(so + 0x70 + 0x20))))
        # a repeat sequence on one object: the counters accumulate
        w = ms.SrcWorld(wem, 600, loop_count=loops, flags_1bd=0x04, real_decode=True)
        assert w.start() == 1
        e, so = w.e, w.so
        seq = []
        for r1 in (0, 0, 0, 1, 0):
            ret = e.call(0xAB1138, so, r1)
            seq.append('%d:%x/%d/%d' % (r1, ret, e.r16(so + 0x38), e.r16(so + 0x5C)))
        steps.append('seq ' + ' '.join(seq))
        out['sv_vt74_loops%d' % loops] = {'file': 'English(US)/99908739.wem', 'loops': loops, 'steps': steps}
    # the seek lookup
    w = ms.SrcWorld(wem, 600, loop_count=1, flags_1bd=0x04, real_decode=True)
    assert w.start() == 1
    e, so = w.e, w.so
    outs = e.alloc(8)
    steps = []
    for pos in (0, 1, 500, 4095, 4096, 9000, 20000, 33905, 33906, 40000):
        e.w32(outs, 0xAAAA)
        e.w32(outs + 4, 0xBBBB)
        ret = e.call(0xAB1020, so, pos, outs, outs + 4)
        steps.append('pos=%d ret=%d s=%d b=%d' % (pos, ret, e.r32(outs), e.r32(outs + 4)))
    # no table: [S+0xE4] cleared; no entries: [S+0xC4] = 0
    e.w32(so + 0xE4, 0)
    e.w32(outs, 0xAAAA)
    e.w32(outs + 4, 0xBBBB)
    ret = e.call(0xAB1020, so, 5, outs, outs + 4)
    steps.append('notable ret=%d s=%d b=%d' % (ret, e.r32(outs), e.r32(outs + 4)))
    out['sv_seek_lookup'] = {'file': 'English(US)/99908739.wem', 'steps': steps}
    return out


# ------------------------------------------------------------------------------------------------ 0xA736D4 on a synthetic source


def start_position_cases():
    C = {}
    base = dict(total=100000, l0=0, l1=0, loops38=1, loops=1, rate=48000, f1be=0, w1b4=2000, markers=None)
    C['p_int_no_fold'] = dict(base)
    C['p_int_rate_44100'] = dict(base, rate=44100, w1b4=12345)
    C['p_int_zero'] = dict(base, w1b4=0)
    C['p_fold_loops3_inside_second_pass'] = dict(base, loops=3, loops38=3, l0=1000, l1=4999, w1b4=7000)
    C['p_fold_loops3_past_all_passes'] = dict(base, loops=3, loops38=3, l0=1000, l1=4999, w1b4=60000)
    C['p_fold_infinite'] = dict(base, loops=0, loops38=0, l0=1000, l1=4999, w1b4=60000)
    C['p_fold_not_applied_loop1'] = dict(base, loops=1, l0=1000, l1=4999, w1b4=60000)
    C['p_fold_not_applied_inside'] = dict(base, loops=3, loops38=3, l0=1000, l1=4999, w1b4=3000)
    C['p_fold_not_applied_l0_ge_l1'] = dict(base, loops=3, loops38=3, l0=5000, l1=5000, w1b4=9000)
    C['p_float_half_loops1'] = dict(base, f1be=1, w1b4=0x3F000000, loops=1, loops38=1, total=48000)
    C['p_float_quarter_loops_0'] = dict(base, f1be=1, w1b4=0x3E800000, loops=0, loops38=0, total=48000)
    C['p_float_loops3'] = dict(base, f1be=1, w1b4=0x3F800000, loops=3, loops38=3, l0=1000, l1=4999, total=48000)
    C['p_float_negative'] = dict(base, f1be=1, w1b4=0xBF000000, loops=1, total=48000)
    C['p_float_nan'] = dict(base, f1be=1, w1b4=0x7FC00000, loops=1, total=48000)
    C['p_float_huge'] = dict(base, f1be=1, w1b4=0x7F000000, loops=1, total=48000)
    C['p_snap_nearest'] = dict(base, f1be=2, w1b4=2200, markers=[(1, 1000), (2, 2000), (3, 3000)])
    C['p_snap_tie_first'] = dict(base, f1be=2, w1b4=2500, markers=[(1, 2000), (2, 3000)], rate=48000)
    C['p_snap_then_fold'] = dict(base, f1be=2, w1b4=3100, markers=[(1, 6000)], loops=3, loops38=3, l0=1000, l1=4999)
    C['p_snap_no_markers'] = dict(base, f1be=2, w1b4=2200, markers=[])
    C['p_float_and_snap'] = dict(base, f1be=3, w1b4=0x3F000000, markers=[(1, 100), (2, 24000), (3, 30000)], total=48000)
    return C


def run_start_position_cases():
    out = {}
    for name, c in start_position_cases().items():
        w = ms.World(file_size=10)
        e = w.e
        pbi = BASE + 0x1000
        S = BASE + 0x2000
        marks = BASE + 0x6000
        e.uc.mem_write(pbi, bytes(0x200))
        e.uc.mem_write(S, bytes(0x100))
        e.w32(S, 0x103E138)                                    # the streamed Vorbis class: vt+0x34 = 0xA72F5C, vt+0x70 = 0xA72F50
        e.w32(S + 0xC, pbi)
        e.w32(S + 0x14, c['total'])
        e.w32(S + 0x24, c['l0'])
        e.w32(S + 0x28, c['l1'])
        e.w16(S + 0x38, c['loops38'])
        e.w32(pbi + 0x158, c['rate'])
        e.w32(pbi + 0x1B4, c['w1b4'])
        e.w16(pbi + 0x1B8, c['loops'])
        e.w8(pbi + 0x1BE, c['f1be'])
        markers = c['markers']
        if markers:
            e.w32(S + 0x2C, len(markers))
            e.w32(S + 0x30, marks)
            for i, (mid, mp) in enumerate(markers):
                e.w32(marks + 12 * i, mid)
                e.w32(marks + 12 * i + 4, mp)
                e.w32(marks + 12 * i + 8, 0)
        e.hook(0x4BAA4C, _uldivmod)
        r = e.call(0xA736D4, S)
        out[name] = {'case': c, 'steps': ['pos=%d h38=%d' % (r, e.r16(S + 0x38))]}
        print('startpos', name, file=sys.stderr, flush=True)
    return out


def _uldivmod(em):
    n = em.reg(0) | (em.reg(1) << 32)
    dv = em.reg(2) | (em.reg(3) << 32)
    q, r = (n // dv, n % dv) if dv else (0, 0)
    em.uc.reg_write(UC_ARM_REG_R1, (q >> 32) & 0xFFFFFFFF)
    em.uc.reg_write(UC_ARM_REG_R2, r & 0xFFFFFFFF)
    em.uc.reg_write(UC_ARM_REG_R3, (r >> 32) & 0xFFFFFFFF)
    return q & 0xFFFFFFFF


# ------------------------------------------------------------------------------------------------ the in-memory Vorbis class


def mem_src_string(e, so):
    return 'S18=%d h38=%d | st=%d fs=%d fr=%d lf=%d c8=%s sk=%d tr=%d se=%d/%d | o80=%d c0=%d' % (
        e.r32(so + 0x18), e.r16(so + 0x38), e.r32(so + 0x44), e.r32(so + 0x40), e.r32(so + 0x3C), e.r32(so + 0x88), ('x' if e.r32(so + 0xC8) == 0 else (e.r32(so + 0xC8) - e.r32(so + 0xCC)) & 0xFFFFFFFF),
        e.r16(so + 0x4C + 0x2C), e.r16(so + 0x4C + 0x2E), signed(e.r32(so + 0x4C + 0x1C)), signed(e.r32(so + 0x4C + 0x20)), 1 if e.r32(so + 0x80) else 0, 1 if e.r32(so + 0xC0) else 0)


def pbi_string(e):
    p = ms.BASE_PBI
    return 'pbi158=%d pbi15c=%d pbi160=%d pbi161=%d pbi162=%d pbi1b4=%d pbi1bd=%d pbi1be=%d' % (
        e.r32(p + 0x158), e.r32(p + 0x15C), e.r8(p + 0x160), e.r8(p + 0x161), e.r8(p + 0x162), e.r32(p + 0x1B4), e.r8(p + 0x1BD), e.r8(p + 0x1BE))


class MemWorld(ms.SrcWorld):
    def __init__(self, wem, loops=1, flags_1bd=0x44, w1b4=0, f1be=0):
        super().__init__(wem, 0, cls='inmem', loop_count=loops, flags_1bd=flags_1bd, real_decode=True)
        e = self.e
        e.w32(ms.BASE_PBI + 0x1B4, w1b4)
        e.w8(ms.BASE_PBI + 0x1BE, f1be)
        e.hook(0x4BAA4C, _uldivmod)
        self.mem = self.alloc_zero(len(wem) + 16)
        e.uc.mem_write(self.mem, wem)
        self.size = len(wem)
        e.uc.mem_write(STATE, bytes(0x40))

    def begin(self, size=None, data=True):
        return self.e.call(0xAB0B20, self.so, self.mem if data else 0, self.size if size is None else size)

    def decode(self):
        e = self.e
        e.w32(STATE + 0x28, 0xABCD)
        e.w16(STATE + 0xC, 1024)
        e.call(0xAB0448, self.so, STATE)
        return io_string(e, STATE) + ' | ' + mem_src_string(e, self.so)


def inmem_cases():
    out = {}
    small = '289339243.wem'
    stereo = '692717205.wem'
    long_ = 'English(US)/99908739.wem'

    def start_only(name, fname, **kw):
        wem = ms.zip_entry(fname)
        w = MemWorld(wem, **{k: v for k, v in kw.items() if k in ('loops', 'flags_1bd', 'w1b4', 'f1be')})
        if kw.get('fail_alloc'):
            w.arm_fail(kw['fail_alloc'])
        r = w.begin(size=kw.get('size'), data=kw.get('data', True))
        steps = ['start=%d | %s | %s' % (r, mem_src_string(w.e, w.so), pbi_string(w.e))]
        n = kw.get('calls', 0)
        for _ in range(n):
            steps.append(w.decode())
        out[name] = {'file': fname, 'params': dict(kw), 'steps': steps}
        print('inmem', name, file=sys.stderr, flush=True)

    start_only('im_single_play', small, loops=1, calls=14)
    start_only('im_stereo_single_play', stereo, loops=1, calls=22)
    start_only('im_loops3', small, loops=3, calls=40)
    start_only('im_loops_infinite', small, loops=0, calls=40)
    start_only('im_loops2_end', small, loops=2, calls=30)
    start_only('im_start_offset_zero', long_, loops=1, flags_1bd=0xC4, w1b4=0, calls=2)
    start_only('im_start_offset_first_entry', long_, loops=1, flags_1bd=0xC4, w1b4=100, calls=2)
    start_only('im_start_offset_mid_table', long_, loops=1, flags_1bd=0xC4, w1b4=15000, calls=3)
    start_only('im_start_offset_last_entry', long_, loops=1, flags_1bd=0xC4, w1b4=33000, calls=2)
    start_only('im_start_offset_beyond_total', long_, loops=1, flags_1bd=0xC4, w1b4=40000, calls=1)
    start_only('im_start_offset_loops3_fold', small, loops=3, flags_1bd=0xC4, w1b4=5000, calls=3)
    start_only('im_start_offset_float', long_, loops=1, flags_1bd=0xC4, w1b4=0x3E800000, f1be=1, calls=2)
    start_only('im_no_offset_bit_clear_with_value', long_, loops=1, flags_1bd=0x44, w1b4=15000, calls=1)
    start_only('im_size_mismatch', small, loops=1, size=len(ms.zip_entry(small)) + 1)
    start_only('im_null_data', small, loops=1, data=False)
    start_only('im_wrong_tag', '998061257.wem', loops=1)

    start_only('im_seek_copy_alloc_fails', long_, loops=1, fail_alloc=1)
    return out


def inmem_vt74_cases():
    out = {}
    small = ms.zip_entry('289339243.wem')
    steps = []
    for loops in (1, 2, 3, 0):
        for r1 in (1, 0):
            w = MemWorld(small, loops=loops)
            assert w.begin() == 1
            e, so = w.e, w.so
            ret = e.call(0xAB0374, so, r1)
            steps.append('loops=%d r1=%d ret=%x | %s' % (loops, r1, ret, mem_src_string(e, so)))
    out['im_vt74'] = {'file': '289339243.wem', 'steps': steps}
    # the close slot and the release slot
    w = MemWorld(small, loops=1)
    assert w.begin() == 1
    e, so = w.e, w.so
    w.decode()
    steps = ['before | %s' % mem_src_string(e, so)]
    e.call(0xAB032C, so)
    steps.append('after_release | %s' % mem_src_string(e, so))
    e.call(0xAB032C, so)
    steps.append('release_again | %s' % mem_src_string(e, so))
    out['im_release'] = {'file': '289339243.wem', 'steps': steps}
    return out


# ------------------------------------------------------------------------------------------------ the ADPCM stream class


def adpcm_src_string(w):
    e, so = w.e, w.so
    return 'S18=%d h38=%d h5c=%d b5e=%d L44=%d f48=%d f10=%d | pb=%d bk=%d o64=%d' % (
        e.r32(so + 0x18), e.r16(so + 0x38), e.r16(so + 0x5C), e.r8(so + 0x5E) & 0x1F, e.r32(so + 0x44), e.r32(so + 0x48), e.r8(so + 0x10), e.r16(so + 0x6C), e.r32(so + 0x60), 1 if e.r32(so + 0x64) else 0)


def adpcm_decode_call(w):
    e = w.e
    e.w32(STATE + 0x28, 0xABCD)
    e.w16(STATE + 0xC, 1024)
    e.call(0xA73D34, w.so, STATE)
    return io_string(e, STATE, adpcm=True) + ' | ' + adpcm_src_string(w)


def adpcm_cases():
    out = {}
    wem = ms.zip_entry('998061257.wem')

    def scenario(name, buffers, calls, flags_1bd=0x04, flags4=0, fail_alloc=0, extra=None):
        w = ms.SrcWorld(wem, 0, cls='adpcm', plugin=0x00020001, source_id=998061257, src_bits0c=0x02, flags_1bd=flags_1bd, flags4=flags4, real_decode=True)
        e = w.e
        e.uc.mem_write(STATE, bytes(0x40))
        r = [w.start()]
        w.io_open_and_deliver(buffers[0][0], buffers[0][1])
        r.append(w.start())
        s3c = e.r32(w.so + 0x3C) - 0x30
        for st, sz in buffers[1:]:
            w.deliver(s3c, st, sz)
        steps = ['start=%s | %s | %s' % (','.join(map(str, r)), adpcm_src_string(w), pbi_string(e))]
        if fail_alloc:
            w.arm_fail(fail_alloc)
        for i in range(calls):
            if extra and i in extra:
                for st, sz in extra[i]:
                    w.deliver(s3c, st, sz)
            steps.append(adpcm_decode_call(w))
        out[name] = {'file': '998061257.wem', 'buffers': buffers, 'calls': calls, 'steps': steps, 'extra': extra or {}, 'flags_1bd': flags_1bd, 'flags4': flags4, 'fail_alloc': fail_alloc}
        print('adpcm', name, file=sys.stderr, flush=True)

    scenario('ad_first_calls', [(0, 4096)], 6)
    scenario('ad_two_buffers_partial_blocks', [(0, 4096), (4096, 4096), (8192, 2024)], 14)
    scenario('ad_small_buffer_partial_carry', [(0, 100)], 5, extra={3: [(100, 4096)]})
    scenario('ad_gate_on', [(0, 4096)], 3, flags_1bd=0x44)

    scenario('ad_output_alloc_fails', [(0, 4096)], 2, fail_alloc=1)
    scenario('ad_carry_alloc_fails', [(0, 4096)], 2, fail_alloc=2)

    # vt+0x7C (0xA739E8), vt+0x74 (0xA742C8) and vt+0xC (0xA73A14)
    w = ms.SrcWorld(wem, 600, cls='adpcm', plugin=0x00020001, source_id=998061257, src_bits0c=0x02, real_decode=True)
    assert w.start() == 1
    e, so = w.e, w.so
    outs = e.alloc(8)
    steps = []
    for pos in (0, 1, 63, 64, 65, 1000, 10000, 20000):
        e.w32(outs, 0xAAAA)
        e.w32(outs + 4, 0xBBBB)
        ret = e.call(0xA739E8, so, pos, outs, outs + 4)
        steps.append('pos=%d ret=%d s=%d b=%d' % (pos, ret, e.r32(outs), e.r32(outs + 4)))
    out['ad_seek_lookup'] = {'file': '998061257.wem', 'steps': steps}
    steps = []
    for loops in (1, 2, 3, 0):
        for r1 in (1, 0, 0, 0):
            w = ms.SrcWorld(wem, 600, cls='adpcm', plugin=0x00020001, source_id=998061257, src_bits0c=0x02, loop_count=loops, real_decode=True)
            assert w.start() == 1
            e, so = w.e, w.so
            e.w16(so + 0x5C, 0)
            ret = e.call(0xA742C8, so, r1)
            steps.append('loops=%d r1=%d ret=%x h38=%d h5c=%d' % (loops, r1, ret, e.r16(so + 0x38), e.r16(so + 0x5C)))
    out['ad_vt74'] = {'file': '998061257.wem', 'steps': steps}
    return out


# ------------------------------------------------------------------------------------------------ the destructors (V22)


def cache_state(e):
    import emu_decode_cases as cases
    return 'bc=%d n=%d arr=%d dump=%s' % (e.r32(d.CACHE + 4), e.r32(d.CACHE + 0xC), 1 if e.r32(d.CACHE) else 0, '|'.join(cases.table_dump(e)))


def destroy_cases():
    """The class destructors (0xAB11BC streamed, 0xAB02E0 in-memory; the base destructors 0xA75A0C / 0xA73304 run for real in the emulator and are a seam in the C#): the output block is released and the setup record's count goes 1 -> 0, which frees the
    node and the bucket array."""
    out = {}
    wem = ms.zip_entry('English(US)/99908739.wem')
    w = new_stream_world(wem)
    w.start()
    w.io_open_and_deliver(0, 4096)
    w.start()
    decode_call(w)
    e = w.e
    steps = ['before | %s | a4=%d a60=%d s80=%d' % (cache_state(e), 1 if e.r32(w.so + 0xA4) else 0, e.r32(w.so + 0x60), 1 if e.r32(w.so + 0x80) else 0)]
    e.call(0xAB11BC, w.so)
    steps.append('after | %s | a4=%d a60=%d' % (cache_state(e), 1 if e.r32(w.so + 0xA4) else 0, e.r32(w.so + 0x60)))
    out['dt_streamed'] = {'file': 'English(US)/99908739.wem', 'steps': steps}
    small = ms.zip_entry('289339243.wem')
    m = MemWorld(small, loops=1)
    assert m.begin() == 1
    m.decode()
    e = m.e
    steps = ['before | %s | o80=%d s5c=%d' % (cache_state(e), 1 if e.r32(m.so + 0x80) else 0, 1 if e.r32(m.so + 0x5C) else 0)]
    e.call(0xAB02E0, m.so)
    steps.append('after | %s | o80=%d' % (cache_state(e), 1 if e.r32(m.so + 0x80) else 0))
    out['dt_inmem'] = {'file': '289339243.wem', 'steps': steps}
    return out
