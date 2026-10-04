"""The scenario groups of emu_decode.py (B-M6b-4 batch 5e): each returns {name: {'params': ..., 'steps': [str, ...]}}; every step string is formatted the same way the C# test (WwiseVorbisEngineTests) formats its own result.

    frame     the frame loop 0xAB7E40 (0xAB3780, 0xAB3520, 0xAB3978) on calls the scenario scripts (packet counts, ready byte, max packet, allocation failure, channel mask, loops, skip)
    dsp       0xAB3264 / 0xAB3428 and the process-wide record 0x108E648
    cache     0xAB2D74 / 0xAB3120 (the parse stubbed: the cache is the subject)
    handoff   0xA73490 on a hand-built source object (the loop count, the loop wrap, vt+0x74, the markers)
    walker    0x9CD340 on hand-built RIFF buffers
"""
import hashlib
import struct
import sys

sys.dont_write_bytecode = True
import emu_decode as d
from emu_common import Emu, BASE
from emu_svc import SvcEmu
from unicorn.arm_const import *


def sha(b):
    return hashlib.sha256(b).hexdigest()


def load_fmt(name):
    b = d.zip_bytes(name)
    fmt, data = d.wem_chunks(b)
    return d.Fmt(fmt, data)


# ------------------------------------------------------------------------------------------------ frame

FRAME_FILES = {
    'mono': '289339243.wem',          # 1 channel, blocks 256 / 2048, 3941 samples
    'stereo': '692717205.wem',        # 2 channels, 256 / 2048, 7008 samples
    'small_blocks': '541179753.wem',  # 1 channel, 512 / 1024, 51288 samples (only its first packets are driven)
}


def frame_step(frames, status, consumed, state, copy, out):
    return 'f=%d s=%d c=%d st=%d cp=%d h=%s' % (frames, status, consumed, state, copy, sha(out)[:16] if out else '-')


def run_frame_case(w, f, loops, skip, cfg, calls):
    """calls: [(start byte offset in the audio, avail, ready, max packet or -1, fail_at or 0)]."""
    e = w.e
    F = w.new_frame(f, loops=loops, skip=skip, cfg=None if cfg < 0 else cfg)
    buf = e.alloc(len(f.audio) + 64)
    e.uc.mem_write(buf, f.audio)
    mark = e.brk
    steps = []
    for (start, avail, ready, maxpkt, fail_at) in calls:
        e.brk = mark
        e.reset_counts(fail_at if fail_at else None)
        r = w.call_frame(F, f, buf + start, avail, ready, None if maxpkt < 0 else maxpkt)
        steps.append(frame_step(*r))
    return steps


def frame_cases():
    cases = {}
    for key, fname in FRAME_FILES.items():
        f = load_fmt(fname)
        pk = d.packets(f.audio)
        n = len(pk)
        per = [(off, size + 2, 0, -1, 0) for (off, size) in pk[:6]]
        cases['f_%s_per_packet_first6' % key] = (fname, 1, 0, -1, per)
    f = load_fmt(FRAME_FILES['mono'])
    pk = d.packets(f.audio)
    total = len(f.audio)
    s = [size + 2 for (_, size) in pk]
    cases['f_mono_two_packets_per_call'] = (FRAME_FILES['mono'], 1, 0, -1, [(pk[0][0], s[0] + s[1], 0, -1, 0), (pk[2][0], s[2] + s[3], 0, -1, 0), (pk[4][0], s[4] + s[5], 0, -1, 0)])
    # a call returns at the first packet that yields frames: the whole media is offered at each call, advancing by the bytes consumed (the drive of the in-memory class)
    cases['f_mono_whole_ready1'] = (FRAME_FILES['mono'], 1, 0, -1, [(0, total, 1, -1, 0), (80 + 3, total - 83, 1, -1, 0), (83 + 88, total - 171, 1, -1, 0)])
    cases['f_mono_whole_ready0'] = (FRAME_FILES['mono'], 1, 0, -1, [(0, total, 0, -1, 0), (83, total - 83, 0, -1, 0)])
    cases['f_mono_whole_loops3'] = (FRAME_FILES['mono'], 3, 0, -1, [(0, total, 1, -1, 0), (83, total - 83, 1, -1, 0)])
    cases['f_mono_cut_packet'] = (FRAME_FILES['mono'], 1, 0, -1, [(pk[0][0], s[0], 0, -1, 0), (pk[1][0], s[1] - 1, 0, -1, 0), (pk[1][0], s[1], 0, -1, 0), (pk[2][0], s[2], 0, -1, 0), (pk[3][0], 1, 0, -1, 0)])
    cases['f_mono_avail_one_first'] = (FRAME_FILES['mono'], 1, 0, -1, [(0, 1, 0, -1, 0), (0, 0, 0, -1, 0), (0, s[0], 0, -1, 0)])
    cases['f_mono_max_packet_small'] = (FRAME_FILES['mono'], 1, 0, -1, [(pk[0][0], s[0], 0, -1, 0), (pk[1][0], s[1], 0, -1, 0), (pk[2][0], s[2], 0, pk[2][1] - 1, 0), (pk[2][0], s[2], 0, pk[2][1], 0)])
    last = pk[-1]
    end = last[0] + last[1] + 2
    cases['f_mono_after_last_packet'] = (FRAME_FILES['mono'], 1, 0, -1,
                                         [(off, size + 2, 1 if i == len(pk) - 1 else 0, -1, 0) for i, (off, size) in enumerate(pk)] + [(end, 0, 1, -1, 0), (end, 0, 0, -1, 0), (last[0], last[1] + 2, 1, -1, 0)])
    cases['f_mono_alloc_fail'] = (FRAME_FILES['mono'], 1, 0, -1, [(pk[0][0], s[0], 0, -1, 0), (pk[1][0], s[1], 0, -1, 1), (pk[1][0], s[1], 0, -1, 0)])
    cases['f_mono_skip_100'] = (FRAME_FILES['mono'], 1, 100, -1, [(off, size + 2, 0, -1, 0) for (off, size) in pk[:5]])
    cases['f_mono_skip_1500'] = (FRAME_FILES['mono'], 1, 1500, -1, [(off, size + 2, 0, -1, 0) for (off, size) in pk[:6]])
    cases['f_mono_skip_5000'] = (FRAME_FILES['mono'], 1, 5000, -1, [(off, size + 2, 0, -1, 0) for (off, size) in pk[:8]])
    f2 = load_fmt(FRAME_FILES['stereo'])
    pk2 = d.packets(f2.audio)
    cases['f_stereo_lfe_mask'] = (FRAME_FILES['stereo'], 1, 0, 0x8002, [(off, size + 2, 0, -1, 0) for (off, size) in pk2[:5]])
    cases['f_stereo_lfe_mask_1'] = (FRAME_FILES['stereo'], 1, 0, 0x9002, [(off, size + 2, 0, -1, 0) for (off, size) in pk2[:5]])
    cases['f_stereo_mask_no_lfe'] = (FRAME_FILES['stereo'], 1, 0, 0x3102, [(off, size + 2, 0, -1, 0) for (off, size) in pk2[:5]])
    return cases


def run_frame_cases(cases=None):
    out = {}
    worlds = {}
    for name, (fname, loops, skip, cfg, calls) in (cases or frame_cases()).items():
        w = worlds.setdefault(fname, d.World(full=True))
        f = load_fmt(fname)
        steps = run_frame_case(w, f, loops, skip, cfg, calls)
        out[name] = {'file': fname, 'loops': loops, 'skip': skip, 'cfg': cfg, 'calls': calls, 'steps': steps}
        print('frame', name, len(steps), file=sys.stderr, flush=True)
    return out


# ------------------------------------------------------------------------------------------------ interleave (the process-shared work buffer)


def interleave_cases():
    """Two decoder states of one engine process share the work buffer of the record 0x108E648 (0xAB3780 sets work[ch] = [R+8] + ch * slice on every packet; the tail 0xAB7EF4..0xAB7F14 calls 0xAB3978 whenever
    [work[0]] != 0, and it copies work[ch] + size into THIS state's overlap with no flag test). Calls: (who, packet index or -1 for a call with no bytes: avail 0, ready 0). Each case: (file A, file B, calls, skip A, skip B,
    sentinel): with the sentinel the shared buffer is filled with 0xA5A5A5A5 after both states exist and every step carries a hash of the whole buffer (a dropped packet must leave it untouched)."""
    mono, stereo = FRAME_FILES['mono'], FRAME_FILES['stereo']
    st_small, mono_small = '1018707468.wem', '1026267421.wem'          # stereo / mono with the block sizes 512 / 1024
    cases = {}

    def add(name, fa, fb, calls, sa=0, sb=0, sentinel=False):
        cases[name] = (fa, fb, calls, sa, sb, sentinel)
    add('i_mono_stereo_nodata', mono, stereo, [(0, i) for i in range(6)] + [(1, i) for i in range(3)] + [(0, -1), (0, 6), (0, 7)])
    add('i_mono_stereo_no_nodata_call', mono, stereo, [(0, i) for i in range(6)] + [(1, i) for i in range(3)] + [(0, 6), (0, 7)])
    add('i_stereo_mono_nodata', stereo, mono, [(0, i) for i in range(5)] + [(1, i) for i in range(4)] + [(0, -1), (0, 5), (0, 6)])
    add('i_stereo_stereo_other_blocks', stereo, st_small, [(0, i) for i in range(4)] + [(1, i) for i in range(4)] + [(0, -1), (0, 4), (1, -1), (1, 4), (0, 5), (1, 5)])
    add('i_mono_small_stereo', mono_small, stereo, [(0, i) for i in range(5)] + [(1, i) for i in range(3)] + [(0, -1), (0, 5), (0, 6)])
    add('i_alternate_every_packet', mono, stereo, [x for i in range(8) for x in ((0, i), (1, i))])
    # dropped packets (the initial skip at or above bs1 / 2): the engine returns from 0xAB3780 without the inverse, the shared buffer is untouched and [D+0x30] stays 1
    add('i_skip_1100_mono_sentinel', mono, stereo, [(0, i) for i in range(6)] + [(1, i) for i in range(3)] + [(0, -1), (0, 6)], sa=1100, sentinel=True)
    add('i_skip_1500_stereo_sentinel', stereo, st_small, [(0, i) for i in range(5)] + [(1, i) for i in range(4)] + [(0, -1), (0, 5), (1, -1), (1, 5)], sa=1500, sb=0, sentinel=True)
    add('i_skip_3000_small_sentinel', st_small, mono, [(0, i) for i in range(8)] + [(1, i) for i in range(4)] + [(0, -1), (0, 8)], sa=3000, sentinel=True)
    add('i_skip_both_sentinel', mono, stereo, [x for i in range(8) for x in ((0, i), (1, i))] + [(0, -1), (1, -1)], sa=1100, sb=1500, sentinel=True)
    return cases


def run_interleave_cases():
    import hashlib as _h
    out = {}
    for name, (fa, fb, calls, sa, sb, sentinel) in interleave_cases().items():
        w = d.World(full=True)
        e = w.e
        fs = [load_fmt(fa), load_fmt(fb)]
        Fs = [w.new_frame(fs[0], skip=sa), w.new_frame(fs[1], skip=sb)]
        R = d.SHARED
        if sentinel:
            e.uc.mem_write(e.u32(R + 8), struct.pack('<I', 0xA5A5A5A5) * (e.u32(R + 4) // 4))
        bufs = []
        for f in fs:
            b = e.alloc(len(f.audio) + 64)
            e.uc.mem_write(b, f.audio)
            bufs.append(b)
        pks = [d.packets(f.audio) for f in fs]
        mark = e.brk
        steps = []
        for who, pkt in calls:
            e.brk = mark
            e.reset_counts()
            if pkt < 0:
                r = w.call_frame(Fs[who], fs[who], bufs[who], 0, 0)
            else:
                off, size = pks[who][pkt]
                r = w.call_frame(Fs[who], fs[who], bufs[who] + off, size + 2, 1 if pkt == len(pks[who]) - 1 else 0)
            step = '%d | %s' % (who, frame_step(*r))
            if sentinel:
                step += ' sh=%s ws=%d/%d' % (_h.sha256(bytes(e.uc.mem_read(e.u32(R + 8), e.u32(R + 4)))).hexdigest()[:12], e.uc.mem_read(Fs[0] + 0x40, 1)[0], e.uc.mem_read(Fs[1] + 0x40, 1)[0])
            steps.append(step)
        out[name] = {'files': [fa, fb], 'calls': calls, 'skips': [sa, sb], 'sentinel': sentinel, 'steps': steps}
        print('interleave', name, len(steps), file=sys.stderr, flush=True)
    return out


# ------------------------------------------------------------------------------------------------ the voice's render order 0xA44630 (insert-FX walks, pitch pass, the source loop 0xA4475C..0xA44788)


def pull_cases():
    """name -> spec. slots: {index: (vt+0x38 results, vt+0x3C results)} (consumed one per call, the last repeated); node: u16[node+0x6E]; src: [(result, valid frames)] per vt+0x30 call; s548: the result 0xA548C0 leaves."""
    D, B, C = 0x2D, 0x2B, 0x2E
    cases = {}
    cases['p_loop_2d_none_then_frames'] = dict(slots={}, node=0, src=[(D, 0), (D, 0), (D, 128)], s548=D)
    cases['p_loop_2e_calls_the_handler_and_returns'] = dict(slots={}, node=0, src=[(C, 0)], s548=D)
    cases['p_loop_other_result_returns'] = dict(slots={}, node=0, src=[(2, 0)], s548=D)
    cases['p_loop_2d_none_then_2e'] = dict(slots={}, node=0, src=[(D, 0), (C, 0)], s548=D)
    cases['p_node_holds_frames_the_source_is_not_called'] = dict(slots={}, node=50, src=[(D, 128)], s548=D)
    cases['p_all_slots_2b_down_then_the_loop'] = dict(slots={i: ([B], [D]) for i in range(4)}, node=0, src=[(D, 128)], s548=D)
    cases['p_slot3_ok_goes_to_filter_a'] = dict(slots={i: ([D], [D]) for i in range(4)}, node=0, src=[(D, 128)], s548=D)
    cases['p_slot2_ok_then_the_up_walk'] = dict(slots={3: ([B], [D]), 2: ([D], [D]), 1: ([D], [D]), 0: ([D], [D])}, node=0, src=[(D, 128)], s548=D)
    cases['p_slot1_other_result_returns'] = dict(slots={3: ([B], [D]), 2: ([B], [D]), 1: ([5], [D]), 0: ([D], [D])}, node=0, src=[(D, 128)], s548=D)
    cases['p_up_walk_2b_goes_back_down'] = dict(slots={3: ([B], [D]), 2: ([B], [B, D]), 1: ([D, B], [D]), 0: ([B], [D])}, node=0, src=[(D, 128)], s548=D)
    cases['p_up_walk_other_result_returns'] = dict(slots={3: ([B], [5]), 2: ([D], [D]), 1: ([B], [D]), 0: ([B], [D])}, node=0, src=[(D, 128)], s548=D)
    cases['p_after_the_loop_the_up_walk_runs_from_slot_0'] = dict(slots={i: ([B], [D]) for i in range(4)}, node=0, src=[(D, 128)], s548=D)
    cases['p_pitch_args_1200_cents_interp_on'] = dict(slots={}, node=0, src=[(D, 128)], s548=D, pitch=1200.0)
    cases['p_pitch_args_bit7_set_interp_off'] = dict(slots={}, node=0, src=[(D, 128)], s548=D, pitch=-350.5, f1be=0x80)
    cases['p_empty_node_last_buffer_byte_gives_0x11'] = dict(slots={}, node=0, src=[(D, 128)], s548=D, b8=1)
    cases['p_held_frames_with_last_buffer_byte'] = dict(slots={}, node=50, src=[(D, 128)], s548=D, b8=1, pitch=24.0)
    cases['p_548_result_ok_notifies'] = dict(slots={3: ([D], [D])}, node=0, src=[], s548=D)
    cases['p_548_result_0x11_notifies'] = dict(slots={3: ([D], [D])}, node=0, src=[], s548=0x11)
    cases['p_548_result_other_returns'] = dict(slots={3: ([D], [D])}, node=0, src=[], s548=C)
    return cases


def run_pull_cases():
    """The engine's own 0xA44630 on a hand-built voice (the slot, source and callee bodies named below are the harness's: they log the call and leave the scripted result). 0xA53134 and 0xA52D4C are the engine's code; a
    consumption reached (0xA52DA8, unread) ends the run."""
    from unicorn import UC_HOOK_CODE
    out = {}
    for name, spec in pull_cases().items():
        e = SvcEmu()
        e.std_hooks()
        state = BASE + 0x1000
        voice = BASE + 0x2000
        src = BASE + 0x6000
        vt = BASE + 0x6100
        e.uc.mem_write(state, bytes(0x40))
        e.uc.mem_write(voice, bytes(0x600))
        e.w32(state + 0x30, voice)
        e.w32(state + 0x28, 0x2B)                                  # the result the voice pass starts a voice with
        e.w32(voice + 0xD4, src)
        e.w32(voice + 0x104, src)                                  # [node+4]: the node's source
        e.w16(voice + 0x100 + 0x6E, spec['node'])
        pbi = BASE + 0xA000
        e.w32(src, vt)
        e.w32(src + 0xC, pbi)                                      # [src+0xC]: the owner PBI
        e.wf(pbi + 0x44, spec.get('pitch', 0.0))
        e.w16(pbi + 0x1BE, spec.get('f1be', 0))
        e.w32(voice + 0x1B4, pbi)                                  # [node+0xB4]
        e.uc.mem_write(voice + 0x100 + 0xB8, bytes([spec.get('b8', 0)]))
        e.uc.mem_write(voice + 0x100 + 0xB9, bytes([0xEE]))
        e.w32(voice + 0x100 + 0x48, 0x7777)
        e.w16(state + 0xC, 0x1234)
        FAKE = {'s30': BASE + 0x8000, 's20': BASE + 0x8010, 'slot38': BASE + 0x8020, 'slot3c': BASE + 0x8030}
        e.w32(vt + 0x30, FAKE['s30'])
        e.w32(vt + 0x20, 0xA5668C)                                 # the engine's own vt+0x20
        slots = {}
        for i, (r38, r3c) in spec['slots'].items():
            obj = BASE + 0x9000 + 0x100 * i
            svt = BASE + 0x9800 + 0x100 * i
            e.w32(obj, svt)
            e.w32(svt + 0x38, FAKE['slot38'])
            e.w32(svt + 0x3C, FAKE['slot3c'])
            e.w32(voice + 0x370 + 4 * i, obj)
            slots[obj] = (i, list(r38), list(r3c))
        events = []
        script = list(spec['src'])

        def next_of(lst):
            return lst.pop(0) if len(lst) > 1 else lst[0]

        def slot_hook(kind):
            def f(em):
                i, r38, r3c = slots[em.reg(0)]
                events.append('s%s(%d)' % (kind, i))
                em.w32(state + 0x28, next_of(r38 if kind == '38' else r3c))
                return None
            return f

        def s30(em):
            events.append('src(%d)' % em.r16(state + 0xC))
            code, valid = script.pop(0)
            em.w32(state + 0x28, code)
            em.w16(state + 0xE, valid)
            return None

        def handler(em):
            events.append('handler')
            return None

        def s548(em):
            events.append('548')
            em.w32(state + 0x28, spec['s548'])
            return None
        e.svc_hook(FAKE['s30'], s30)
        e.svc_hook(FAKE['slot38'], slot_hook('38'))
        e.svc_hook(FAKE['slot3c'], slot_hook('3C'))
        e.svc_hook(0xA55C14, handler)
        e.svc_hook(0xA548C0, s548)
        e.svc_hook(0xA03E8C, lambda em: events.append('notify'))
        e.svc_hook(0xA4C60C, lambda em: None)
        e.svc_hook(0xA56E00, lambda em: None)

        def setpitch(em):
            import struct as _s
            events.append('pitch(%s,%d)' % (repr(_s.unpack('<f', _s.pack('<I', em.reg(1)))[0]), em.reg(2) & 0xFF))
            return None
        e.svc_hook(0xA47384, setpitch)
        ended = []

        def consume(uc, a, s_, _):
            events.append('consume')
            ended.append('stop')
            uc.emu_stop()
        e.uc.hook_add(UC_HOOK_CODE, consume, begin=0xA52DA8, end=0xA52DA8)
        e.call(0xA44630, state)
        out[name] = {'spec': {'slots': {str(k): v for k, v in spec['slots'].items()}, 'node': spec['node'], 'src': spec['src'], 's548': spec['s548'], 'pitch': spec.get('pitch', 0.0), 'f1be': spec.get('f1be', 0), 'b8': spec.get('b8', 0)},
                     'steps': ['%s | end=%s res=%x n48=%d b9=%d' % (','.join(events), ended[0] if ended else 'ret', e.r32(state + 0x28), e.r32(voice + 0x100 + 0x48), e.uc.mem_read(voice + 0x100 + 0xB9, 1)[0])]}
        print('pull', name, out[name]['steps'][0], file=sys.stderr, flush=True)
    return out


# ------------------------------------------------------------------------------------------------ dsp


def dsp_ops():
    """(name, ops): an op is ('alloc', slot, channels, fail_at) or ('free', slot)."""
    cases = {}
    cases['d_mono_then_stereo'] = [('alloc', 0, 1, 0), ('alloc', 1, 2, 0), ('free', 0), ('free', 1)]
    cases['d_stereo_then_mono'] = [('alloc', 0, 2, 0), ('alloc', 1, 1, 0), ('free', 1), ('free', 0)]
    cases['d_free_without_alloc'] = [('free', 0), ('alloc', 0, 1, 0), ('free', 0), ('free', 0)]
    cases['d_twice_on_the_same_state'] = [('alloc', 0, 2, 0), ('alloc', 0, 2, 0), ('free', 0)]
    for k in (1, 2, 3, 4):
        cases['d_stereo_fail_at_%d' % k] = [('alloc', 0, 2, k), ('free', 0), ('alloc', 0, 2, 0), ('free', 0)]
    cases['d_second_fails_at_work'] = [('alloc', 0, 1, 0), ('alloc', 1, 2, 3), ('free', 1), ('free', 0)]
    return cases


def run_dsp_cases():
    out = {}
    for name, ops in dsp_ops().items():
        w = d.World(full=True)
        e = w.e
        setup = e.alloc(0x100)
        e.w32(setup, 256)
        e.w32(setup + 4, 2048)
        ds = []
        for i in range(4):
            F = e.alloc(0x100)
            e.w32(F + 0x20, setup)                               # [D+0x10] = F+0x20 is D+0x10: D = F+0x10
            ds.append(F + 0x10)
            e.w32(ds[-1] + 0x10, setup)
        steps = []
        for op in ops:
            if op[0] == 'alloc':
                _, slot, ch, fail_at = op
                e.reset_counts(fail_at if fail_at else None)
                ret = e.call(ev.STATE_INIT, ds[slot], ch)
                ret = ret - (1 << 32) if ret & 0x80000000 else ret
            else:
                slot = op[1]
                e.reset_counts()
                e.call(0x00AB3428, ds[slot])
                ret = 0
            D = ds[op[1]]
            a14, a18 = e.u32(D + 0x14), e.u32(D + 0x18)
            ovl = 1 if (a18 and e.u32(a18)) else 0
            steps.append('ret=%d arr=%d ovl=%d ch=%d ws=%d pf=%d cf=%d | blk=%d sz=%d work=%d users=%d | allocs=%d' % (
                ret, 1 if a14 else 0, ovl, e.u32(D + 0xC), e.u8(D + 0x30) if hasattr(e, 'u8') else e.uc.mem_read(D + 0x30, 1)[0], e.u32(D + 0x24), e.u32(D + 0x28),
                1 if e.u32(SHARED_ADDR) else 0, struct.unpack('<i', struct.pack('<I', e.u32(SHARED_ADDR + 4)))[0], 1 if e.u32(SHARED_ADDR + 8) else 0, e.u32(SHARED_ADDR + 0xC), e.allocs))
        out[name] = {'ops': ops, 'steps': steps}
        print('dsp', name, file=sys.stderr, flush=True)
    return out


SHARED_ADDR = d.SHARED


# ------------------------------------------------------------------------------------------------ cache


def cache_scripts():
    """('acq', key, fail_at, parse_fail) / ('rel', key)."""
    cases = {}
    cases['c_one_key_refcounts'] = [('acq', 7, 0, 0), ('acq', 7, 0, 0), ('acq', 7, 0, 0), ('rel', 7), ('rel', 7), ('rel', 7), ('rel', 7), ('acq', 7, 0, 0), ('rel', 7)]
    cases['c_collisions_and_unlink'] = [('acq', 1, 0, 0), ('acq', 30, 0, 0), ('acq', 59, 0, 0), ('acq', 2, 0, 0), ('rel', 30), ('rel', 1), ('acq', 88, 0, 0), ('rel', 59), ('rel', 2), ('rel', 88), ('rel', 5)]
    cases['c_grow_through_the_primes'] = [('acq', 1000 + 7 * i, 0, 0) for i in range(60)] + [('acq', 1000, 0, 0), ('acq', 1000 + 7 * 59, 0, 0)] + [('rel', 1000 + 7 * i) for i in range(60)] + [('acq', 5, 0, 0)]
    cases['c_failures'] = [
        ('acq', 11, 1, 0),            # the node allocation fails: null
        ('acq', 11, 2, 0),            # the setup block allocation fails: null
        ('acq', 11, 0, 1),            # the parse fails: null
        ('acq', 11, 3, 0),            # first insertion: the bucket array allocation fails with no buckets: null
        ('acq', 11, 0, 0),
        ('acq', 12, 0, 0),
    ] + [('acq', 100 + i, 0, 0) for i in range(26)] + [
        ('acq', 500, 3, 0),           # the table is full (27 of 29): the bucket array allocation fails: the old table keeps the node
        ('acq', 501, 0, 0),
        ('rel', 500), ('rel', 501), ('rel', 11), ('rel', 12),
    ]
    cases['c_release_unknown'] = [('rel', 3), ('acq', 3, 0, 0), ('rel', 4), ('rel', 3), ('rel', 3)]
    return cases


def table_dump(e):
    rd = e.u32 if hasattr(e, 'u32') else e.r32
    n = rd(d.CACHE + 4)
    arr = rd(d.CACHE)
    chains = []
    for b in range(n):
        node = rd(arr + 4 * b)
        c = []
        while node:
            c.append('%d:%d' % (rd(node), rd(node + 0x18)))
            node = rd(node + 4)
        chains.append(','.join(c))
    return chains


def run_cache_cases():
    out = {}
    for name, ops in cache_scripts().items():
        w = d.World(full=True)
        e = w.e
        e.hook_parse = None
        parse_fail = [False]
        # the parse is stubbed: the cache is the subject (the parse is M6-002's, emu_vorbis.py)
        e.uc.hook_add(ev.UC_HOOK_CODE, lambda uc, a, s, _: (uc.reg_write(UC_ARM_REG_R0, 0), uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))), begin=0xAB6380, end=0xAB6380)
        e.uc.hook_add(ev.UC_HOOK_CODE, lambda uc, a, s, _: (uc.reg_write(UC_ARM_REG_R0, 1 if parse_fail[0] else 0), uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))), begin=0xAB63E0, end=0xAB63E0)
        F = e.alloc(0x100)
        e.w32(F + 0x70, 0x1000)
        e.uc.mem_write(F + 0x7C, bytes([8, 11]))
        e.uc.mem_write(F + 0x48, bytes([1]))
        blk = e.alloc(16)
        sp = e.alloc(64)
        e.w32(blk, sp)
        e.w32(blk + 4, 8)
        e.uc.mem_write(blk + 8, b'\0')
        steps = []
        for op in ops:
            if op[0] == 'acq':
                _, key, fail_at, pf = op
                e.w32(F + 0x78, key)
                parse_fail[0] = bool(pf)
                e.reset_counts(fail_at if fail_at else None)
                r = e.call(0x00AB2D74, CACHE_ADDR, F, 0, blk)
                rc = e.u32(r - 8 + 0x18) if r else -1
                head = 'acq %d ret=%d rc=%d' % (key, 1 if r else 0, rc)
            else:
                _, key = op
                e.w32(F + 0x78, key)
                e.reset_counts()
                e.call(0x00AB3120, CACHE_ADDR, F)
                head = 'rel %d' % key
            steps.append('%s | bc=%d cap=%d n=%d arr=%d | allocs=%d' % (head, e.u32(d.CACHE + 4), e.u32(d.CACHE + 8), e.u32(d.CACHE + 0xC), 1 if e.u32(d.CACHE) else 0, e.allocs))
        steps.append('dump ' + '|'.join(table_dump(e)))
        out[name] = {'ops': ops, 'steps': steps}
        print('cache', name, file=sys.stderr, flush=True)
    return out


CACHE_ADDR = d.CACHE
ev = d.ev


# ------------------------------------------------------------------------------------------------ handoff


def handoff_cases():
    """(name, S fields, call, vt74 script, markers, pbi flags4)."""
    C = {}
    base = dict(total=1000, pos=0, l0=0, l1=0, loops=1, frames=128, rate=22050, cc=0x4101)
    C['h_zero_frames'] = dict(base, frames=0)
    C['h_loop1_more_data'] = dict(base)
    C['h_loop1_exact_end'] = dict(base, total=128, vt=0x11)
    C['h_loop1_past_end'] = dict(base, total=100, pos=64, vt=0x11)
    C['h_loop1_end_slot_returns_0x2d'] = dict(base, total=128, vt=0x2D)
    C['h_loop_infinite_inside'] = dict(base, loops=0, l0=10, l1=500, pos=100)
    C['h_loop_infinite_wrap'] = dict(base, loops=0, l0=10, l1=500, pos=400, vt=0x2D)
    C['h_loop_three_wrap_end_slot_0x11'] = dict(base, loops=3, l0=10, l1=500, pos=400, vt=0x11)
    C['h_loop_exactly_at_the_loop_end'] = dict(base, loops=0, l0=10, l1=528, pos=400, vt=0x2D)
    C['h_frames_above_u16'] = dict(base, frames=70000, total=900000)
    C['h_stereo_config'] = dict(base, cc=0x3102, rate=48000, pos=77)
    C['h_markers_window'] = dict(base, pos=100, frames=256, flags4=4, markers=[(1, 99), (2, 100), (3, 355), (4, 356), (5, 200), (6, 150)])
    C['h_markers_no_flag'] = dict(base, pos=100, frames=256, flags4=0, markers=[(1, 150)])
    C['h_markers_none_in_window'] = dict(base, pos=100, frames=256, flags4=4, markers=[(1, 50), (2, 900)])
    C['h_markers_alloc_fail'] = dict(base, pos=100, frames=256, flags4=4, markers=[(1, 150)], fail_alloc=True)
    C['h_markers_empty_container'] = dict(base, pos=100, frames=256, flags4=4, markers=[])
    C['h_markers_null_array'] = dict(base, pos=100, frames=256, flags4=4, markers=None)
    return C


def run_handoff_cases():
    out = {}
    for name, c in handoff_cases().items():
        e = SvcEmu()
        e.std_hooks()
        pbi = BASE + 0x1000
        S = BASE + 0x2000
        vt = BASE + 0x3000
        fn = BASE + 0x3800
        state = BASE + 0x4000
        buf = BASE + 0x5000
        marks = BASE + 0x6000
        e.uc.mem_write(pbi, bytes(0x200))
        e.uc.mem_write(S, bytes(0x100))
        e.uc.mem_write(state, bytes(range(0x40)))               # a distinctive pattern: untouched fields show
        e.w32(pbi + 4, c.get('flags4', 0))
        e.w32(S, vt + 8)                                       # the vptr points 8 bytes into the vtable: slot 0x74 is at vptr + 0x74 -> vt + 0x7C? (read as in-place word [vptr+0x74])
        e.w32(S + 0xC, pbi)
        e.w32(S + 0x14, c['total'])
        e.w32(S + 0x18, c['pos'])
        e.w32(S + 0x24, c['l0'])
        e.w32(S + 0x28, c['l1'])
        e.w16(S + 0x38, c['loops'])
        markers = c.get('markers', [])
        if markers is None:
            e.w32(S + 0x2C, 0)
            e.w32(S + 0x30, 0)
        else:
            e.w32(S + 0x2C, len(markers))
            if markers:
                e.w32(S + 0x30, marks)
                for i, (mid, mpos) in enumerate(markers):
                    e.w32(marks + 12 * i, mid)
                    e.w32(marks + 12 * i + 4, mpos)
                    e.w32(marks + 12 * i + 8, 0xBEEF0000 + i)
            else:
                e.w32(S + 0x30, marks)
        e.w32(vt + 8 + 0x74, fn)                               # the slot read by `ldr r3,[r3,#0x74]` with r3 = [S] = vt + 8
        calls = []

        def vt74(em):
            calls.append(em.reg(1))
            return c.get('vt', 0x2D)
        e.svc_hook(fn, vt74)
        if c.get('fail_alloc'):
            e.hook(0xA7A7F4, lambda em: 0)
        e.call(0xA73490, S, buf, c['frames'], c['rate'], stack=(c['cc'], state))
        g = lambda o: e.r32(state + o)
        marks_out = []
        mc = e.r16(state + 0x10)
        arr = e.r32(state + 0x14)
        for i in range(mc if (0x03000000 <= arr < 0x03400000) else 0):
            w5 = [e.r32(arr + 20 * i + 4 * k) for k in range(5)]
            marks_out.append('%s/%d/%d/%d/%x' % ('pbi' if w5[0] == pbi else str(w5[0]), w5[1], w5[2], w5[3], w5[4]))
        dstr = 'buf' if g(0) == buf else ('untouched' if g(0) == 0x03020100 else '%x' % g(0))
        step = 'd=%s cc=%x max=%d valid=%d mc=%d arr=%d pos=%d tot=%d rate=%d res=%x | S18=%d | vt=[%s] | marks=[%s]' % (
            dstr, g(4), e.r16(state + 0xC), e.r16(state + 0xE), mc, 1 if arr else 0, g(0x18), g(0x20), g(0x24), g(0x28), e.r32(S + 0x18), ','.join(map(str, calls)), ';'.join(marks_out))
        out[name] = {'case': {k: v for k, v in c.items()}, 'steps': [step]}
        print('handoff', name, file=sys.stderr, flush=True)
    return out


# ------------------------------------------------------------------------------------------------ walker


def riff(chunks, form=b'WAVE', size_override=None):
    body = form
    for tag, payload, declared in chunks:
        body += tag + struct.pack('<I', len(payload) if declared is None else declared) + payload
        if len(payload) & 1 and declared is None:
            body += b'\0'
    return b'RIFF' + struct.pack('<I', len(body) if size_override is None else size_override) + body


def fmt_chunk(extra=b''):
    return b'fmt ', struct.pack('<HHIIHH', 0xFFFF, 1, 48000, 100, 36, 0) + bytes(0x10) + extra, None


def walker_cases():
    C = {}
    fm = fmt_chunk()
    data = (b'data', bytes(range(32)), None)
    C['w_minimal'] = riff([fm, data])
    C['w_xwma_form'] = riff([fm, data], form=b'XWMA')
    C['w_bad_form'] = riff([fm, data], form=b'AVI ')
    bad_tag = bytearray(riff([fm, data]))
    bad_tag[0:4] = b'RIFX'
    C['w_bad_tag'] = bytes(bad_tag)
    C['w_data_before_fmt'] = riff([data, fm])
    C['w_two_fmt'] = riff([fm, (b'fmt ', b'\x01' * 24, None), data])
    C['w_junk_even'] = riff([fm, (b'JUNK', bytes(8), None), data])
    C['w_junk_odd_pad_zero'] = riff([fm, (b'JUNK', bytes(7), None), data])
    pad_nonzero = riff([fm, (b'JUNK', bytes(7), None), data])
    ofs = pad_nonzero.index(b'JUNK') + 8 + 7
    pn = bytearray(pad_nonzero)
    pn[ofs] = 0x55                                              # a non-zero pad byte: the walker reads the next header at the pad byte
    C['w_junk_odd_pad_nonzero_misaligns'] = bytes(pn)
    C['w_list_nested'] = riff([fm, (b'LIST', b'adtl' + b'abcd' + struct.pack('<I', 4) + b'zzzz', None), data])
    C['w_list_then_odd'] = riff([fm, (b'LIST', b'INFO', None), (b'JUNK', bytes(3), None), data])
    smpl = struct.pack('<IIIIIIIII', 0, 0, 0, 60, 0, 0, 0, 1, 0) + struct.pack('<IIIIII', 0, 0, 24302, 220574, 0, 0)
    C['w_smpl_one_loop'] = riff([fm, (b'smpl', smpl, None), data])
    smpl0 = struct.pack('<IIIIIIIII', 0, 0, 0, 60, 0, 0, 0, 0, 0) + bytes(24)
    C['w_smpl_zero_loops'] = riff([fm, (b'smpl', smpl0, None), data])
    smpl_cb = struct.pack('<IIIIIIIII', 0, 0, 0, 60, 0, 0, 0, 1, 4) + b'\xaa' * 4 + struct.pack('<IIIIII', 0, 0, 11, 222, 0, 0)
    C['w_smpl_with_sampler_data'] = riff([fm, (b'smpl', smpl_cb, None), data])
    C['w_akd'] = riff([fm, (b'akd ', b'AKD!' * 3, None), data])
    C['w_akd_empty'] = riff([fm, (b'akd ', b'', None), data])
    C['w_seek'] = riff([fm, (b'seek', b'SEEK' * 2, None), data])
    cue = struct.pack('<I', 3) + b''.join(struct.pack('<IIIIII', i + 1, 100 * (i + 1), 0x61746164, 0, 0, 0) for i in range(3))
    C['w_cue_three'] = riff([fm, (b'cue ', cue, None), data])
    C['w_cue_zero_count'] = riff([fm, (b'cue ', struct.pack('<I', 0), None), data])
    C['w_cue_before_fmt'] = riff([(b'cue ', cue, None), fm, data])
    C['w_cue_twice'] = riff([fm, (b'cue ', cue, None), (b'cue ', struct.pack('<I', 1) + struct.pack('<IIIIII', 9, 99, 0, 0, 0, 0), None), data])
    C['w_labl_without_cue'] = riff([fm, (b'LIST', b'adtl', None), (b'labl', struct.pack('<I', 1) + b'hi\0\0', None), data])
    C['w_labl_no_match'] = riff([fm, (b'cue ', cue, None), (b'LIST', b'adtl', None), (b'labl', struct.pack('<I', 77) + b'hi\0\0', None), data])
    C['w_truncated_chunk'] = riff([fm, (b'JUNK', bytes(40), 400), data])
    C['w_truncated_data_ok'] = riff([fm, (b'data', bytes(16), 4000)])
    C['w_only_fmt'] = riff([fm])
    C['w_short_tail'] = riff([fm]) + b'abc'
    C['w_too_short'] = b'RIFF\0\0\0\0WAV'
    C['w_chunk_after_data_ignored'] = riff([fm, data, (b'JUNK', b'xx', None)])
    return C


def run_walker_cases():
    from emu_stream import World as StreamWorld
    out = {}
    for name, blob in walker_cases().items():
        cuts = [('whole', len(blob))]
        if name in ('w_minimal', 'w_cue_three', 'w_smpl_one_loop'):
            cuts += [('cut_in_data', len(blob) - 8), ('cut_in_fmt', 40), ('cut_header', 11)]
        steps = []
        for label, n in cuts:
            w = StreamWorld(file_size=len(blob))
            e = w.e
            mem = w.alloc_zero(len(blob) + 16)
            e.uc.mem_write(mem, blob)
            so = w.alloc_zero(0x100)
            fmtinfo = w.alloc_zero(8)
            akd = w.alloc_zero(8)
            seek = w.alloc_zero(8)
            for a in (so + 0x24, so + 0x28, so + 0x1C, so + 0x20):
                e.w32(a, 0xDEAD0000 + (a - so))                # untouched outputs are visible
            e.w32(fmtinfo, 0xF0F0)
            e.w32(fmtinfo + 4, 0xF1F1)
            e.w32(seek, 0)
            e.w32(seek + 4, 0)
            r = e.call(0x9CD340, mem, n, fmtinfo, so + 0x2C, stack=(so + 0x24, so + 0x28, so + 0x1C, so + 0x20, akd, seek if name == 'w_seek' else 0))
            fp = e.r32(fmtinfo + 4)
            ap = e.r32(akd + 4)
            cnt = e.r32(so + 0x2C)
            arr = e.r32(so + 0x30)
            cues = []
            for i in range(cnt if arr else 0):
                cues.append('%d/%d/%d' % (e.r32(arr + 12 * i), e.r32(arr + 12 * i + 4), e.r32(arr + 12 * i + 8)))
            sp = e.r32(seek + 4)
            steps.append('%s r=%d fmt=%d@%s lp=%d/%d data=%d@%d akd=%d@%s seek=%d@%s n=%d arr=%d cues=[%s]' % (
                label, r, e.r32(fmtinfo), ('%d' % (fp - mem)) if fp > 0x100000 else str(fp), e.r32(so + 0x24), e.r32(so + 0x28), e.r32(so + 0x1C), e.r32(so + 0x20),
                e.r32(akd), ('%d' % (ap - mem)) if ap > 0x100000 else str(ap), e.r32(seek), ('%d' % (sp - mem)) if sp > 0x100000 else str(sp), cnt, 1 if arr else 0, ';'.join(cues)))
        out[name] = {'blob': blob.hex(), 'cuts': [c for c in cuts], 'steps': steps}
        print('walker', name, file=sys.stderr, flush=True)
    return out


def run_walker_alloc_fail():
    from emu_stream import World as StreamWorld
    blob = walker_cases()['w_cue_three']
    w = StreamWorld(file_size=len(blob))
    e = w.e
    mem = w.alloc_zero(len(blob) + 16)
    e.uc.mem_write(mem, blob)
    so = w.alloc_zero(0x100)
    fmtinfo = w.alloc_zero(8)
    akd = w.alloc_zero(8)
    e.hook(0xA7A7F4, lambda em: 0)
    r = e.call(0x9CD340, mem, len(blob), fmtinfo, so + 0x2C, stack=(so + 0x24, so + 0x28, so + 0x1C, so + 0x20, akd, 0))
    return {'w_cue_three_alloc_fail': {'blob': blob.hex(), 'steps': ['r=%d n=%d arr=%d data=%d' % (r, e.r32(so + 0x2C), 1 if e.r32(so + 0x30) else 0, e.r32(so + 0x1C))]}}


# ------------------------------------------------------------------------------------------------ the pitch node's intake 0xA52D4C


def intake_cases():
    """(name, held frames u16[node+0x6E], valid frames u16[state+0xE], result [state+0x28])."""
    C = {}
    C['i_result_0x2d_no_frames_becomes_0x2b'] = (0, 0, 0x2D)
    C['i_result_0x2e_no_frames_is_copied'] = (0, 0, 0x2E)
    C['i_result_0x2d_with_frames_is_copied'] = (0, 128, 0x2D)
    C['i_frames_held_nothing_is_copied'] = (50, 128, 0x2D)
    C['i_frames_held_no_valid_frames'] = (50, 0, 0x2D)
    C['i_result_0x11_goes_to_the_last_buffer_path'] = (0, 77, 0x11)
    C['i_result_0x11_with_frames_held'] = (9, 77, 0x11)
    C['i_result_2_with_frames_is_copied'] = (0, 5, 2)
    return C


def run_intake_cases():
    out = {}
    for name, (held, valid, result) in intake_cases().items():
        e = SvcEmu()
        e.std_hooks()
        node = BASE + 0x1000
        state = BASE + 0x2000
        e.uc.mem_write(node, bytes([0xEE]) * 0x100)              # the node's old words (0xEEEEEEEE): a copy shows
        e.uc.mem_write(state, bytes(0x40))
        e.w16(node + 0x6E, held)
        for off_, v in ((0, 0x1234), (4, 0x4101), (8, 0x55), (0x14, 0x6789), (0x18, 7000), (0x1C, 0x3F800000), (0x20, 9000), (0x24, 22050), (0x28, result)):
            e.w32(state + off_, v)
        e.w16(state + 0xC, 1024)
        e.w16(state + 0xE, valid)
        e.w16(state + 0x10, 3)
        reached = []

        def stop(label):
            def f(uc, a, s_, _):
                reached.append(label)
                uc.emu_stop()
            return f
        from unicorn import UC_HOOK_CODE
        e.uc.hook_add(UC_HOOK_CODE, stop('consume'), begin=0xA52DA8, end=0xA52DA8)
        e.uc.hook_add(UC_HOOK_CODE, stop('end'), begin=0xA52EBC, end=0xA52EBC)
        e.call(0xA52D4C, node, state)
        w = lambda o: e.r32(node + 0x60 + o)
        step = 'reached=%s res=%x held=d:%d/cc:%x/mx:%d/v:%d/mc:%d/pos:%d/tot:%d/rate:%d/w8:%x/w1c:%x' % (
            reached[0] if reached else 'ret', e.r32(state + 0x28), 1 if w(0) not in (0, 0xEEEEEEEE) else (0 if w(0) == 0 else -1), w(4), e.r16(node + 0x60 + 0xC), e.r16(node + 0x60 + 0xE),
            e.r16(node + 0x60 + 0x10), w(0x18), w(0x20), w(0x24), w(8), w(0x1C))
        out[name] = {'case': [held, valid, result], 'steps': [step]}
        print('intake', name, file=sys.stderr, flush=True)
    return out
