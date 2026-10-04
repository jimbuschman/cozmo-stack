"""The Vorbis / ADPCM stream integration under Unicorn (B-M6b-4 batch 5e; M6-wwise-bank.md C36).

The engine's own functions run: the frame loop 0xAB7E40 with the packet entry 0xAB3780 and the window driver 0xAB3520, the setup cache 0xAB2D74 / 0xAB3120, the decoder-state allocation 0xAB3264 / 0xAB3428, the output
hand-off 0xA73490, the WAVE walker 0x9CD340, the in-memory class (0xAB0B20, 0xAB0A30, 0xAB04F0, 0xAB0448, 0xAB0374), the streamed class (0xAB22D4 ... 0xAB1550 through the stream layer of emu_stream.py) and the ADPCM stream
class (0xA73ABC, 0xA73D34, 0xA739E8). Stand-ins are the harness's only: the pool allocator (0xA7A7F4 / 0xA7A894, with failure injection; frees are no-ops), the libc divides and memcpy / memset, the stream layer's I/O thread
(emu_stream.py, C33.4) and, where a scenario says so, the setup parse 0xAB6380 / 0xAB63E0 (a cache scenario is about the cache; the parse is M6-002's, whole-decode oracle emu_vorbis.py).

    python re-analysis/tools/emu/emu_decode.py census [--limit N] [--shard k/n] [--full] --out file.json
    python re-analysis/tools/emu/emu_decode.py emit-cs <census json> ... > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseVorbisEngineOracle.cs

The census drives every shipped Vorbis medium one packet per call (the copy-buffer case of 0xAB1550: F+0x50 = size + 2, the ready byte 1 only on the last packet) and summarises the per-call frames; the per-packet inverse
0xAB6B14 is a no-op there unless --full (it cannot change the frames, the status, the bytes consumed or the state: they come from 0xAB3780's skip / trim machine and 0xAB3520's start / end). Every other scenario runs the whole
decode. The expected values in WwiseVorbisEngineTests are this script's output, never the C#'s.
"""
import hashlib
import json
import os
import struct
import sys
import zipfile
import zlib

sys.dont_write_bytecode = True
import emu_vorbis as ev
from emu_vorbis import Engine, ROOT, SETUP_INIT, SETUP_PARSE, STATE_INIT, FRAME_LOOP
from so import u32 as so_u32, RAW, off as so_off

INVERSE = 0x00AB6B14            # the per-packet inverse (the tail call of 0xAB3780)
DRIVER = 0x00AB3520
SHARED = 0x108E648              # the process-wide record: +0 block, +4 size, +8 work buffer, +0xC users
CACHE = 0x108E638               # the setup cache table: +0 buckets, +4 bucket count, +8 capacity, +0xC nodes
ZIP = os.path.join(ROOT, 're-analysis', 'obb', 'assets', 'cozmo_resources', 'sound', 'AudioAssets.zip')


def sha(b):
    return hashlib.sha256(b).hexdigest()


# ------------------------------------------------------------------------------------------------ media


def wem_chunks(b):
    off, fmt, data = 12, None, None
    while off + 8 <= len(b):
        tag, size = b[off:off + 4], struct.unpack_from('<I', b, off + 4)[0]
        if tag == b'fmt ':
            fmt = b[off + 8:off + 8 + size]
        elif tag == b'data':
            data = b[off + 8:off + 8 + size]
            break
        off += 8 + size + (size & 1)
    return fmt, data


def vorbis_media():
    """Every Vorbis medium of AudioAssets.zip: (name, fmt chunk, data chunk)."""
    z = zipfile.ZipFile(ZIP)
    out = []
    for n in sorted(z.namelist()):
        if not n.endswith('.wem'):
            continue
        b = z.read(n)
        fmt, data = wem_chunks(b)
        if fmt is None or struct.unpack_from('<H', fmt, 0)[0] != 0xFFFF:
            continue
        out.append((n, fmt, data))
    return out


def zip_bytes(name):
    return zipfile.ZipFile(ZIP).read(name)


class Fmt:
    """The fields of a Vorbis fmt chunk the engine copies (0xAB12B4 / 0xAB0B20): fmt + 0x1C .. 0x42 is the vorb block."""

    def __init__(self, fmt, data):
        self.fmt, self.data = fmt, data
        self.channels = struct.unpack_from('<H', fmt, 2)[0]
        self.rate = struct.unpack_from('<I', fmt, 4)[0]
        self.cfg = struct.unpack_from('<I', fmt, 0x14)[0]
        self.samples = struct.unpack_from('<I', fmt, 0x18)[0]
        v = fmt[0x1C:0x1C + 0x26]
        self.loop_start_off, self.loop_end_off = struct.unpack_from('<II', v, 0)
        self.loop_begin_extra, self.loop_end_extra = struct.unpack_from('<HH', v, 8)
        self.seek_size, self.audio_off = struct.unpack_from('<II', v, 0xC)
        self.max_packet, self.last_granule_extra = struct.unpack_from('<HH', v, 0x14)
        self.alloc = struct.unpack_from('<I', v, 0x18)[0]
        self.hash = struct.unpack_from('<I', v, 0x20)[0]
        self.bs0, self.bs1 = v[0x24], v[0x25]
        self.setup_off = self.seek_size
        self.setup_size = struct.unpack_from('<H', data, self.setup_off)[0]
        self.audio = data[self.audio_off:]


def packets(audio):
    """The (offset, size) of every audio packet: u16 size then the body."""
    off, out = 0, []
    while off + 2 <= len(audio):
        size = struct.unpack_from('<H', audio, off)[0]
        out.append((off, size))
        off += 2 + size
    return out


# ------------------------------------------------------------------------------------------------ the frame-loop world


class Eng(Engine):
    """emu_vorbis.Engine with allocation failure injection and counting (0xA7A7F4 plain, 0xA7A894 aligned)."""

    def __init__(self):
        super().__init__()
        self.fail_at = None             # fail the n-th (1-based) allocation call since reset_counts()
        self.allocs = 0
        self.log = []

    def reset_counts(self, fail_at=None):
        self.fail_at = fail_at
        self.allocs = 0
        self.log = []

    def _stub(self, uc, addr, size, _):
        if addr in (ev.ALLOC, ev.ALLOC_ALIGN):
            r = [uc.reg_read(getattr(__import__('unicorn.arm_const', fromlist=['x']), f'UC_ARM_REG_R{i}')) for i in range(3)]
            self.allocs += 1
            self.log.append(('aligned' if addr == ev.ALLOC_ALIGN else 'plain', r[1]))
            if self.fail_at is not None and self.allocs == self.fail_at:
                self._ret(0)
                return
        super()._stub(uc, addr, size, _)


class World:
    """One engine; a parsed setup is kept per (hash, channels, block sizes, setup bytes) like the engine's cache does."""

    def __init__(self, full=False):
        self.e = Eng()
        self.full = full
        self.setups = {}
        if not full:
            self.e.uc.hook_add(ev.UC_HOOK_CODE, self._skip_inverse, begin=INVERSE, end=INVERSE)

    def _skip_inverse(self, uc, addr, size, _):
        uc.reg_write(ev.UC_ARM_REG_PC, uc.reg_read(ev.UC_ARM_REG_LR))          # a tail call: lr is the caller's return address

    def parse_setup(self, f):
        e = self.e
        key = (f.hash, f.channels, f.bs0, f.bs1, f.setup_size, hashlib.md5(f.data[f.setup_off + 2:f.setup_off + 2 + f.setup_size]).digest())
        if key in self.setups:
            return self.setups[key]
        size = max(f.alloc, 0x80000)
        base = e.alloc(size)
        assert e.call(SETUP_INIT, base, f.bs0, f.bs1) == 0, 'setup init'
        alloc = e.alloc(16)
        for i, v in enumerate((base, base + 0x30, 0x30, size)):
            e.w32(alloc + 4 * i, v)
        sp = e.alloc(f.setup_size + 16)
        e.uc.mem_write(sp, f.data[f.setup_off + 2:f.setup_off + 2 + f.setup_size])
        reader = e.alloc(16)
        for i, v in enumerate((sp, 0, f.setup_size)):
            e.w32(reader + 4 * i, v)
        rc = e.call(SETUP_PARSE, base, f.channels, reader, alloc)
        assert rc == 0, f'setup parse returned {rc:#x}'
        self.setups[key] = base
        return base

    def new_frame(self, f, loops=1, skip=0, cfg=None):
        """F and its decoder state as 0xAB0A30 / 0xAB22D4 leave them: the setup, 0xAB3264, then 0xAB3244(D, skip, trim)."""
        e = self.e
        base = self.parse_setup(f)
        F = e.alloc(0x100)
        D = F + 0x10
        e.w32(D + 0x10, base)
        rc = e.call(STATE_INIT, D, f.channels)
        assert rc == 0, f'0xAB3264 returned {rc:#x}'
        trim = f.last_granule_extra if loops == 1 else f.loop_end_extra
        e.uc.mem_write(D + 0x2C, struct.pack('<HH', skip, trim))
        e.w32(D + 0x1C, 0xFFFFFFFF)
        e.w32(D + 0x20, 0xFFFFFFFF)
        e.w32(F + 0x48, f.cfg if cfg is None else cfg)
        return F

    def call_frame(self, F, f, buf, avail, ready, max_packet=None):
        e = self.e
        e.w32(F + 0x50, avail)
        e.uc.mem_write(F + 0x54, bytes([ready]))
        outp = F + 0x44
        e.w32(outp, 0)
        e.call(FRAME_LOOP, F, f.max_packet if max_packet is None else max_packet, buf, outp)
        frames, status, consumed, state, copy = e.u32(F), e.u32(F + 4), e.u32(F + 0xC), e.u32(F + 8), e.u32(F + 0x4C)
        out = b''
        if frames and e.u32(outp):
            out = bytes(e.uc.mem_read(e.u32(outp), 4 * f.channels * frames))
        return frames, status, consumed, state, copy, out


def drive_per_packet(w, f, loops=1, keep_output=False):
    """One packet per call: F+0x50 = size + 2, the ready byte 1 only on the last packet (the copy-buffer case of 0xAB1550)."""
    e = w.e
    F = w.new_frame(f, loops)
    audio = f.audio
    buf = e.alloc(len(audio) + 16)
    e.uc.mem_write(buf, audio)
    calls, outs = [], []
    pk = packets(audio)
    mark = e.brk
    for i, (off, size) in enumerate(pk):
        e.brk = mark                                           # the previous call's output block is dead (the pitch node released it): reuse the pool
        frames, status, consumed, state, copy, out = w.call_frame(F, f, buf + off, size + 2, 1 if i == len(pk) - 1 else 0)
        calls.append((frames, status, consumed, state))
        if keep_output:
            outs.append(out)
        if status == 2:
            break
    return calls, outs


def drive_whole(w, f, loops=1):
    """The whole media offered at every call (ready = 1), advancing by the bytes consumed: the drive of emu_vorbis.py and of the in-memory class's 0xAB0448 (a call returns at the first packet that yields frames)."""
    e = w.e
    F = w.new_frame(f, loops)
    audio = f.audio
    buf = e.alloc(len(audio) + 16)
    e.uc.mem_write(buf, audio)
    mark = e.brk
    calls, outs = [], []
    off = 0
    while off + 2 <= len(audio):
        e.brk = mark
        frames, status, consumed, state, copy, out = w.call_frame(F, f, buf + off, len(audio) - off, 1)
        calls.append((frames, status, consumed, state))
        outs.append(out)
        if consumed == 0 or status == 0x11 or status == 2:
            break
        off += consumed
        if state == 4:
            break
    return calls, outs


def census_record(name, f, calls):
    frames = [c[0] for c in calls]
    per = sorted(set(x for x in frames if x))
    last = calls[-1] if calls else None
    return {'name': name, 'channels': f.channels, 'bs': [f.bs0, f.bs1], 'samples': f.samples, 'total': sum(frames), 'calls': len(calls),
            'last_frames': last[0] if last else None, 'last_status': last[1] if last else None, 'last_state': last[3] if last else None,
            'per_call': per, 'statuses': sorted(set(c[1] for c in calls))}


def cmd_census(args):
    limit = None
    full = '--full' in args
    out = None
    shard = (0, 1)
    for i, a in enumerate(args):
        if a == '--limit':
            limit = int(args[i + 1])
        if a == '--out':
            out = args[i + 1]
        if a == '--shard':
            k, n = args[i + 1].split('/')
            shard = (int(k), int(n))
    media = vorbis_media()
    media = [m for i, m in enumerate(media) if i % shard[1] == shard[0]]
    if limit:
        media = media[:limit]
    w = World(full=full)
    records = []
    for i, (name, fmt, data) in enumerate(media):
        f = Fmt(fmt, data)
        try:
            calls, _ = drive_per_packet(w, f)
            rec = census_record(name, f, calls)
        except Exception as ex:                                  # a Unicorn failure is recorded, not hidden
            rec = {'name': name, 'error': repr(ex)}
        records.append(rec)
        if (i + 1) % 25 == 0:
            print(f'{i + 1}/{len(media)}', file=sys.stderr, flush=True)
            if out:
                json.dump(records, open(out, 'w'))
    if out:
        json.dump(records, open(out, 'w'))
    else:
        for r in records:
            print(json.dumps(r, sort_keys=True))


PARITY_FILES = ('289339243.wem', '692717205.wem', '884576857.wem', '456481546.wem', '541179753.wem', 'English(US)/99908739.wem')


def run_parity():
    """Each file driven twice with the whole decode: one packet per call and one call over the whole media; the SHA-256 of every call's output, of all the per-packet outputs together and of the whole call."""
    import emu_decode_cases as cases
    out = {}
    w = World(full=True)
    for name in PARITY_FILES:
        f = cases.load_fmt(name)
        calls, outs = drive_per_packet(w, f, keep_output=True)
        steps = []
        # the per-call strings are rebuilt from the same drive: frames, status, consumed, state, and the SHA-256 prefix of the block
        for (frames, status, consumed, state), o in zip(calls, outs):
            steps.append('f=%d s=%d c=%d st=%d h=%s' % (frames, status, consumed, state, sha(o)[:16] if o else '-'))
        wcalls, wouts = drive_whole(w, f)
        wsteps = ['f=%d s=%d c=%d st=%d h=%s' % (fr, st_, co, sta, sha(o)[:16] if o else '-') for (fr, st_, co, sta), o in zip(wcalls, wouts)]
        out[name] = {'samples': f.samples, 'channels': f.channels, 'per_packet': steps, 'all_sha': sha(b''.join(outs)), 'whole': wsteps, 'whole_sha': sha(b''.join(wouts))}
        print('parity', name, file=sys.stderr, flush=True)
    return out


def cs_str(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def cs_arr(items):
    return 'new[] { ' + ', '.join(cs_str(x) for x in items) + ' }'


def params_string(c):
    parts = []
    for k, v in c.items():
        if k == 'markers':
            parts.append('markers=' + ('none' if v is None else ','.join('%d/%d' % tuple(m) for m in v)))
        elif v is True:
            parts.append('%s=1' % k)
        else:
            parts.append('%s=%s' % (k, v))
    return ';'.join(parts)


def run_all_scenarios():
    """Every scenario group except the census: the slow part (about half an hour); saved by the `scenarios` command so emit-cs is quick."""
    import emu_decode_cases as cases
    import emu_decode_stream as stream
    out = {}
    out['frame'] = cases.run_frame_cases()
    out['dsp'] = cases.run_dsp_cases()
    out['il'] = cases.run_interleave_cases()
    out['pull'] = cases.run_pull_cases()
    out['cache'] = cases.run_cache_cases()
    out['handoff'] = cases.run_handoff_cases()
    out['intake'] = cases.run_intake_cases()
    walker = cases.run_walker_cases()
    walker.update(cases.run_walker_alloc_fail())
    out['walker'] = walker
    light = bool(os.environ.get('EMIT_LIGHT'))                     # development only: skips the slow whole-decode groups
    out['parity'] = {} if light else run_parity()
    sv = {} if light else stream.stream_cases()
    if not light:
        sv.update(stream.stream_vt74_cases())
        sv.update(stream.stream_start_offset_cases())
    out['sv'] = sv
    out['startpos'] = stream.run_start_position_cases()
    im = {} if light else stream.inmem_cases()
    if not light:
        im.update(stream.inmem_vt74_cases())
    out['im'] = im
    out['ad'] = {} if light else stream.adpcm_cases()
    out['dt'] = {} if light else stream.destroy_cases()
    return out


def cmd_scenarios(args):
    json.dump(run_all_scenarios(), open(args[0], 'w'))


def pull_block(pull):
    """The `Pull` member of WwiseVorbisEngineOracle (the voice render order 0xA44630 with the real 0xA548C0), as source lines."""
    o = []
    o.append('    /// <summary>The voice render order 0xA44630 on a hand-built voice with the REAL 0xA548C0 (slots: index:vt+0x38 results:vt+0x3C results, results joined by /, slots joined by ;; node frames; source script result:valid joined by ;; the owner PBI pitch +0x44, its u16 +0x1BE, the node byte +0xB8; extra: valid, f1f8, flags4, pos, w1c, total, rate, pend = (vt+0x28 result/latch/+0x1DC/+0x1E0), repo = the stamp of a record already in the repository), steps.</summary>')
    o.append('    public static readonly Dictionary<string, (string Slots, int Node, string Src, double Pitch, int F1be, int B8, string Extra, string Step)> Pull = new()')
    o.append('    {')
    for k, v in pull.items():
        sp = v['spec']
        slots = ';'.join('%s:%s:%s' % (i, '/'.join(str(x) for x in a), '/'.join(str(x) for x in b)) for i, (a, b) in sorted(sp['slots'].items()))
        src = ';'.join('%d:%d' % tuple(x) for x in sp['src'])
        o.append('        [%s] = (%s, %d, %s, %r, %d, %d, %s, %s),' % (cs_str(k), cs_str(slots), sp['node'], cs_str(src), float(sp['pitch']), sp['f1be'], sp['b8'], cs_str(sp['extra']), cs_str(v['steps'][0])))
    o.append('    };')
    return o


def cmd_patch_pull(args):
    """patch-pull <WwiseVorbisEngineOracle.cs>: re-runs only the pull group and rewrites the `Pull` member of an existing generated file (the rest of the file is unchanged; a full emit-cs gives the same text)."""
    import emu_decode_cases as cases
    path = args[0]
    text = open(path, encoding='utf-8').read()
    start = text.index('    /// <summary>The voice render order 0xA44630')
    end = text.index('    };\n', start) + len('    };\n')
    block = '\n'.join(pull_block(cases.run_pull_cases())) + '\n'
    open(path, 'w', encoding='utf-8').write(text[:start] + block + text[end:])


def cmd_emit(args):
    """emit-cs [--scenarios file.json] <census json> ...: writes WwiseVorbisEngineOracle.cs to stdout (running the scenario groups itself unless a saved file is given)."""
    saved = None
    if args and args[0] == '--scenarios':
        saved = json.load(open(args[1]))
        args = args[2:]
    census = []
    for a in args:
        census += json.load(open(a))
    bad = [r for r in census if 'error' in r]
    assert not bad, bad[:3]
    if os.environ.get('EMIT_PARTIAL_CENSUS'):
        print('warning: a partial census (development only)', file=sys.stderr)
    else:
        assert len(census) == 1987, len(census)
    census.sort(key=lambda r: r['name'])
    sc = saved if saved is not None else run_all_scenarios()
    frame, dsp, cache, handoff, intake, walker = sc['frame'], sc['dsp'], sc['cache'], sc['handoff'], sc['intake'], sc['walker']
    parity, sv, startpos, im, ad = sc['parity'], sc['sv'], sc['startpos'], sc['im'], sc['ad']
    il = sc['il']
    pull = sc['pull']
    sv.update(sc.get('dt', {}))
    o = []
    p = o.append
    p("// <auto-generated> by re-analysis/tools/emu/emu_decode.py emit-cs from the engine's own output (Unicorn): do not edit. </auto-generated>")
    p('namespace Cozmo.Protocol.Tests;')
    p('')
    p("/// <summary>The engine's values for the Vorbis / ADPCM stream integration (B-M6b-4 batch 5e, C36): the real 0xAB7E40, 0xAB3520, 0xAB3780, 0xAB2D74, 0xAB3120, 0xAB3264, 0xAB3428, 0xA73490, 0x9CD340, the in-memory and streamed Vorbis classes and the ADPCM stream class under Unicorn.</summary>")
    p('internal static class WwiseVorbisEngineOracle')
    p('{')
    p('    /// <summary>The prime list at 0x10045D0 (28 words: 27 primes then the 0 the scan never reads).</summary>')
    p('    public static readonly uint[] Primes = { %s };' % ', '.join(str(so_u32(0x10045D0 + 4 * i)) + 'u' for i in range(28)))
    p('')
    p('    /// <summary>The census: every shipped Vorbis medium (name, channels, header SampleCount, total frames, calls, last call frames / status / state, the sorted set of non-zero frames per call), one packet per call.</summary>')
    p('    public static readonly (string Name, int Channels, uint Samples, long Total, int Calls, int LastFrames, int LastStatus, int LastState, int[] PerCall)[] Census =')
    p('    {')
    for r in census:
        p('        (%s, %d, %du, %d, %d, %d, %d, %d, new[] { %s }),' % (cs_str(r['name']), r['channels'], r['samples'], r['total'], r['calls'], r['last_frames'], r['last_status'], r['last_state'], ', '.join(map(str, r['per_call']))))
    p('    };')
    p('')
    p('    /// <summary>Whole-decode parity (file: channels, samples, one step per packet, the SHA-256 of all per-packet outputs, the whole-buffer step and its SHA-256).</summary>')
    p('    public static readonly Dictionary<string, (int Channels, uint Samples, string[] PerPacket, string AllSha, string[] Whole, string WholeSha)> Parity = new()')
    p('    {')
    for k, v in parity.items():
        p('        [%s] = (%d, %du, %s, %s, %s, %s),' % (cs_str(k), v['channels'], v['samples'], cs_arr(v['per_packet']), cs_str(v['all_sha']), cs_arr(v['whole']), cs_str(v['whole_sha'])))
    p('    };')
    p('')
    p("    /// <summary>The frame loop on scripted calls: file, loop count, initial skip, channel word (-1: the file's), calls (start offset, bytes available, ready byte, max packet or -1, failing allocation or 0), steps.</summary>")
    p('    public static readonly Dictionary<string, (string File, int Loops, int Skip, int Cfg, (int Start, int Avail, int Ready, int Max, int FailAt)[] Calls, string[] Steps)> Frame = new()')
    p('    {')
    for k, v in frame.items():
        calls = ', '.join('(%d, %d, %d, %d, %d)' % tuple(c) for c in v['calls'])
        p('        [%s] = (%s, %d, %d, %d, new (int, int, int, int, int)[] { %s }, %s),' % (cs_str(k), cs_str(v['file']), v['loops'], v['skip'], v['cfg'], calls, cs_arr(v['steps'])))
    p('    };')
    p('')
    p('    /// <summary>Two decoder states of one engine process sharing the work buffer of 0x108E648: file A, file B, calls (who:packet, packet -1 = a call with no bytes), steps (who | the frame step).</summary>')
    p('    public static readonly Dictionary<string, (string FileA, string FileB, string Calls, int SkipA, int SkipB, bool Sentinel, string[] Steps)> Interleave = new()')
    p('    {')
    for k, v in il.items():
        p('        [%s] = (%s, %s, %s, %d, %d, %s, %s),' % (cs_str(k), cs_str(v['files'][0]), cs_str(v['files'][1]), cs_str(';'.join('%d:%d' % tuple(c) for c in v['calls'])), v['skips'][0], v['skips'][1], 'true' if v['sentinel'] else 'false', cs_arr(v['steps'])))
    p('    };')
    p('')
    for line in pull_block(pull):
        p(line)
    for name, group, doc in (('Dsp', dsp, 'The decoder-state allocation and teardown (ops: alloc:slot:channels:failAt, free:slot).'), ('Cache', cache, 'The setup cache (ops: acq:key:failAt:parseFail, rel:key).')):
        p('')
        p('    /// <summary>%s</summary>' % doc)
        p('    public static readonly Dictionary<string, (string Ops, string[] Steps)> %s = new()' % name)
        p('    {')
        for k, v in group.items():
            ops = ';'.join(':'.join(str(x) for x in op) for op in v['ops'])
            p('        [%s] = (%s, %s),' % (cs_str(k), cs_str(ops), cs_arr(v['steps'])))
        p('    };')
    p('')
    p('    /// <summary>The output hand-off 0xA73490 on a hand-built source (parameters as key=value;...).</summary>')
    p('    public static readonly Dictionary<string, (string Params, string Step)> Handoff = new()')
    p('    {')
    for k, v in handoff.items():
        p('        [%s] = (%s, %s),' % (cs_str(k), cs_str(params_string(v['case'])), cs_str(v['steps'][0])))
    p('    };')
    p('')
    p('    /// <summary>The pitch node\'s intake 0xA52D4C (held frames, valid frames, result): where it stops (ret, consume = 0xA52DA8, end = 0xA52EBC), the result left, the node\'s copy of the state.</summary>')
    p('    public static readonly Dictionary<string, (string Params, string Step)> Intake = new()')
    p('    {')
    for k, v in intake.items():
        p('        [%s] = (%s, %s),' % (cs_str(k), cs_str(','.join(map(str, v['case']))), cs_str(v['steps'][0])))
    p('    };')
    p('')
    p('    /// <summary>The WAVE walker 0x9CD340 on hand-built RIFF buffers (hex, the lengths walked, one step per length).</summary>')
    p('    public static readonly Dictionary<string, (string Blob, (string Label, int Len)[] Cuts, string[] Steps)> Walker = new()')
    p('    {')
    for k, v in walker.items():
        cuts = v.get('cuts', [('whole', len(bytes.fromhex(v['blob'])))])
        p('        [%s] = (%s, new (string, int)[] { %s }, %s),' % (cs_str(k), cs_str(v['blob']), ', '.join('(%s, %d)' % (cs_str(c[0]), c[1]) for c in cuts), cs_arr(v['steps'])))
    p('    };')
    p('')
    p('    /// <summary>The start position 0xA736D4 on a hand-built source.</summary>')
    p('    public static readonly Dictionary<string, (string Params, string Step)> StartPosition = new()')
    p('    {')
    for k, v in startpos.items():
        p('        [%s] = (%s, %s),' % (cs_str(k), cs_str(params_string(v['case'])), cs_str(v['steps'][0])))
    p('    };')
    for name, group, doc in (('Stream', sv, 'The streamed Vorbis class through the stream layer (buffers, calls) and its vt+0x74 / vt+0x7C.'), ('InMemory', im, 'The in-memory Vorbis class (parameters as JSON).'), ('Adpcm', ad, 'The ADPCM stream class (buffers, calls).')):
        p('')
        p('    /// <summary>%s</summary>' % doc)
        p('    public static readonly Dictionary<string, (string Params, string[] Steps)> %s = new()' % name)
        p('    {')
        for k, v in group.items():
            prm = json.dumps({kk: vv for kk, vv in v.items() if kk != 'steps'}, sort_keys=True)
            p('        [%s] = (%s, %s),' % (cs_str(k), cs_str(prm), cs_arr(v['steps'])))
        p('    };')
    p('}')
    print('\n'.join(o))


COMMANDS = {'census': cmd_census, 'scenarios': cmd_scenarios, 'emit-cs': cmd_emit, 'patch-pull': cmd_patch_pull}

if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'census'
    COMMANDS[cmd](sys.argv[2:])
