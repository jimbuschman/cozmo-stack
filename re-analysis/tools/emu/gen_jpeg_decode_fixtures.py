"""Generate the JPEG decode fixtures (M3-001 / M3-018 decode path) from the shipped libraries run under Unicorn.

    python re-analysis/tools/emu/gen_jpeg_decode_fixtures.py [--out DIR] [--quick] [--no-coverage]

Every expected value is what the shipped code returns, never what the C# port returns:
  * `imdecode` cases run cv::imdecode(buf, flags, Mat*) from libopencv_imgcodecs.so exactly as EncodedImage::DecodeImageHelper calls it;
  * `resize` cases run cv::resize(..., INTER_LINEAR) from libopencv_imgproc.so;
  * `engine` cases run EncodedImage::DecodeImageGray / DecodeImageRGB from libcozmoEngine.so (MiniToJpegHelper, imdecode, cvtColor,
    copyMakeBorder, Resize, the size check and its log lines).
Each case is run with the heap and stack poisoned with 0x00 and with 0xFF (bytes that differ are bytes the shipped code never writes: they
are recorded in `excluded` and the tests skip them) and with NEON reported present and absent (a difference would be listed in
`neon_differences`; the run must find none). Libraries, stand-ins and boundaries: emu_elf.py, emu_jpeg_decode.py.

Output: index.json (the cases and expected values), data.zip (inputs, and outputs of the imdecode and resize cases), coverage.json
(executed instructions per J row), README.md (what is in the corpus).
"""
import argparse
import hashlib
import io
import json
import os
import random
import re
import struct
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, HERE)
from emu_jpeg_decode import Oracle, verify_library_hashes  # noqa: E402
import jpegcraft as jc  # noqa: E402

DEFAULT_OUT = os.path.join(ROOT, 'cozmo-stack', 'tests', 'Cozmo.Protocol.Tests', 'Fixtures', 'jpeg_decode')


def ordered_logs(r0):
    """The log lines a decode wrote, in order, in the stack's level-prefix form: sErrorF / sWarningF as "error: Key: text" / "warning: Key: text", and the
    line cv::error() writes to logcat (tag cv::error(), priority ERROR) as "error: cv::error(): OpenCV Error: ..."."""
    out = []
    for l in r0['logs']:
        if l[0] in ('warning', 'error'):
            out.append('%s: %s: %s' % (l[0], l[1], l[2]))
        elif l[0] == 'android_log' and l[1] == 'cv::error()':
            out.append('error: cv::error(): ' + l[2])
    return out


class Runs:
    """The oracle instances a case is run under."""

    def __init__(self):
        self.p0 = Oracle(poison=0x00, neon=True)
        self.pff = Oracle(poison=0xFF, neon=True)
        self.nn = Oracle(poison=0x00, neon=False)

    def all(self):
        return (self.p0, self.pff, self.nn)


def diff_ranges(a, b):
    out = []
    n = min(len(a), len(b))
    i = 0
    while i < n:
        if a[i] != b[i]:
            j = i
            while j < n and a[j] != b[j]:
                j += 1
            out.append([i, j])
            i = j
        else:
            i += 1
    return out


def sha(b):
    return hashlib.sha256(b).hexdigest()


class Corpus:
    def __init__(self, runs):
        self.runs = runs
        self.cases = []
        self.files = {}          # path in zip -> bytes
        self.neon_differences = []
        self.oracle_aborts = []
        self.ids = set()

    def _put(self, name, data):
        self.files[name] = bytes(data)

    def _result(self, fn, store_data):
        """Run fn(oracle) under the three oracles and merge them into one expected record."""
        r0, rff, rnn = (fn(o) for o in self.runs.all())
        exc = r0.get('exception')
        if exc and exc.startswith('abort'):
            return None, exc
        rec = {}
        if exc:
            rec['exception'] = 'cxx_throw'
            rec['exception_type'] = exc.split(' ', 1)[1]
            rec['logs'] = ordered_logs(r0)
            return rec, None
        for k in ('rows', 'cols', 'channels', 'result', 'err_flag'):
            if k in r0:
                rec[k] = r0[k]
        data = r0.get('data', b'')
        excluded = []
        if rff.get('exception') is None and len(rff.get('data', b'')) == len(data):
            excluded = diff_ranges(data, rff['data'])
        elif data:
            excluded = [[0, len(data)]]
        rec['excluded'] = excluded
        if rnn.get('exception') is None and len(rnn.get('data', b'')) == len(data):
            nd = diff_ranges(data, rnn['data'])
            ex_set = set()
            for a, b in excluded:
                ex_set.update(range(a, b))
            nd = [d for d in nd if not all(i in ex_set for i in range(d[0], d[1]))]
            if nd:
                rec['neon_differences'] = nd
        else:
            if data or rnn.get('data'):
                rec['neon_differences'] = [[0, len(data)]]
        # the engine's own log lines (warnings and errors it raised)
        rec['logs'] = ordered_logs(r0)
        # libjpeg's standard error manager prints the first warning of a decode to stderr (fprintf); cv::error also prints its text there
        rec['stderr'] = [l[1].rstrip('\n') for l in r0['logs'] if l[0] == 'fprintf' and not l[1].startswith('OpenCV Error')]
        mask = bytearray(data)
        for a, b in excluded:
            for i in range(a, b):
                mask[i] = 0
        rec['sha256'] = sha(bytes(mask))
        rec['_data'] = data
        return rec, None

    def add_imdecode(self, cid, data, flags, note=''):
        if cid in self.ids:
            raise ValueError('duplicate id ' + cid)
        rec, abort = self._result(lambda o: o.imdecode(data, flags), True)
        if rec is None:
            self.oracle_aborts.append((cid, abort))
            return
        case = {'id': cid, 'kind': 'imdecode', 'flags': flags, 'input': 'in/%s.bin' % cid, 'note': note}
        self._finish(case, rec, data, store_out=True)

    def add_resize(self, cid, rows, cols, ch, data, drows, dcols, note=''):
        rec, abort = self._result(lambda o: o.resize(rows, cols, ch, data, dcols, drows), True)
        if rec is None:
            self.oracle_aborts.append((cid, abort))
            return
        case = {'id': cid, 'kind': 'resize', 'srows': rows, 'scols': cols, 'schannels': ch, 'drows': drows, 'dcols': dcols,
                'input': 'in/%s.bin' % cid, 'note': note}
        self._finish(case, rec, data, store_out=True)

    def add_engine(self, cid, encoding, gray, payload, note='', store_out=False):
        rec, abort = self._result(lambda o: o.decode(encoding, payload, gray), store_out)
        if rec is None:
            self.oracle_aborts.append((cid, abort))
            return
        case = {'id': cid, 'kind': 'engine', 'encoding': encoding, 'gray': gray, 'input': 'in/%s.bin' % cid, 'note': note}
        self._finish(case, rec, payload, store_out=store_out)

    def _finish(self, case, rec, input_bytes, store_out):
        self.ids.add(case['id'])
        data = rec.pop('_data', b'')
        self._put(case['input'], input_bytes)
        if store_out and data:
            case['output'] = 'out/%s.bin' % case['id']
            self._put(case['output'], data)
        case['expected'] = rec
        if rec.get('neon_differences'):
            self.neon_differences.append(case['id'])
        self.cases.append(case)


# --------------------------------------------------------------------------------------------------------------- corpus content
def natural(w, h, seed):
    from PIL import Image
    r = random.Random(seed)
    im = Image.new('RGB', (w, h))
    px = im.load()
    for y in range(h):
        for x in range(w):
            base = (x * 255 // max(w, 1), y * 255 // max(h, 1), (x + y) * 255 // max(w + h, 1))
            if (x // 7 + y // 5) % 3 == 0:
                base = (255 - base[0], base[2], base[1])
            n = r.randint(-12, 12)
            px[x, y] = tuple(max(0, min(255, v + n)) for v in base)
    return im


def pil_cases(c, quick):
    from PIL import Image
    sizes = [(16, 16), (33, 17), (64, 48), (7, 5), (1, 1), (100, 3)] + ([] if quick else [(320, 240)])
    for (w, h) in sizes:
        im = natural(w, h, w * 1000 + h)
        variants = [('420', dict(subsampling=2)), ('422', dict(subsampling=1)), ('444', dict(subsampling=0)), ('q40', dict(subsampling=0, quality=40)),
                    ('prog', dict(progressive=True, subsampling=2)), ('prog444', dict(progressive=True, subsampling=0, quality=60)),
                    ('q95', dict(quality=95, subsampling=1)), ('opt', dict(optimize=True, subsampling=2)), ('q5', dict(quality=5, subsampling=1)),
                    ('q100', dict(quality=100, subsampling=0))]
        for name, kw in variants:
            b = io.BytesIO()
            im.save(b, 'JPEG', **kw)
            for flags in (0, 1):
                c.add_imdecode('pil_%dx%d_%s_f%d' % (w, h, name, flags), b.getvalue(), flags, 'PIL-encoded natural-ish image')
        b = io.BytesIO()
        im.convert('L').save(b, 'JPEG', quality=85)
        for flags in (0, 1):
            c.add_imdecode('pil_%dx%d_gray_f%d' % (w, h, flags), b.getvalue(), flags, 'PIL-encoded gray')
        b = io.BytesIO()
        im.convert('L').save(b, 'JPEG', quality=70, progressive=True)
        for flags in (0, 1):
            c.add_imdecode('pil_%dx%d_grayprog_f%d' % (w, h, flags), b.getvalue(), flags, 'PIL-encoded progressive gray')
    # CMYK (Adobe) from PIL
    for mode in ('CMYK',):
        im = natural(24, 16, 5).convert(mode)
        b = io.BytesIO()
        im.save(b, 'JPEG', quality=90)
        for flags in (0, 1):
            c.add_imdecode('pil_cmyk_f%d' % flags, b.getvalue(), flags, 'PIL CMYK (Adobe APP14 transform 0)')


SAMPLINGS = [[(1, 1, 1)], [(1, 1, 1), (2, 1, 1), (3, 1, 1)], [(1, 2, 1), (2, 1, 1), (3, 1, 1)], [(1, 2, 2), (2, 1, 1), (3, 1, 1)],
             [(1, 1, 2), (2, 1, 1), (3, 1, 1)], [(1, 4, 1), (2, 1, 1), (3, 1, 1)], [(1, 1, 4), (2, 1, 1), (3, 1, 1)],
             [(1, 4, 2), (2, 1, 1), (3, 1, 1)], [(1, 2, 4), (2, 1, 1), (3, 1, 1)], [(1, 3, 1), (2, 1, 1), (3, 1, 1)],
             [(1, 2, 1), (2, 2, 1), (3, 1, 1)], [(1, 1, 1), (2, 2, 2), (3, 2, 2)], [(1, 2, 2), (2, 2, 1), (3, 1, 2)],
             [(1, 3, 3), (2, 1, 1), (3, 1, 1)], [(1, 4, 4), (2, 2, 2), (3, 1, 1)], [(1, 2, 2), (2, 1, 1), (3, 1, 1), (4, 2, 2)],
             [(1, 2, 1), (2, 1, 1)], [(1, 1, 3), (2, 1, 1), (3, 1, 1)], [(1, 4, 3), (2, 2, 1), (3, 1, 1)]]


def baseline_cases(c, rnd, quick):
    n = 0
    sizes = [(8, 8), (17, 13), (40, 24), (33, 65), (1, 1), (5, 70), (16, 16), (64, 8)]
    for si, comps in enumerate(SAMPLINGS):
        for (w, h) in (sizes[:4] if quick else sizes):
            ri = rnd.choice([0, 0, 1, 2, 5])
            inter = rnd.random() < 0.7
            ad = rnd.choice([None, None, 0, 1, 2])
            data = jc.build_baseline(w, h, comps, rnd, restart_interval=ri, interleaved=inter, adobe=ad, jfif=(ad is None and rnd.random() < 0.7))
            for flags in (0, 1):
                c.add_imdecode('b%d_%dx%d_ri%d_%s_ad%s_f%d' % (si, w, h, ri, 'i' if inter else 'n', ad, flags), data, flags,
                               'baseline, random coefficients, Annex K tables')
            n += 1
    return n


def random_entropy_cases(c, rnd, quick):
    comps_list = [[(1, 1, 1)], [(1, 2, 1), (2, 1, 1), (3, 1, 1)], [(1, 2, 2), (2, 1, 1), (3, 1, 1)], [(1, 1, 1), (2, 1, 1), (3, 1, 1)],
                  [(1, 3, 1), (2, 1, 1), (3, 1, 1)], [(1, 4, 4), (2, 2, 2), (3, 1, 1)]]
    n = 0
    for kind, marker in (('seq', 0xC0), ('ext', 0xC1), ('prog', 0xC2), ('arith', 0xC9), ('parith', 0xCA)):
        for ci, comps in enumerate(comps_list):
            for (w, h) in ((16, 16), (23, 19), (64, 8)):
                for trial in range(2 if quick else 4):
                    head = jc.soi() + jc.app0_jfif() + jc.dqt([(0, jc.STD_LUM_Q, 0), (1, [max(1, q // 3) for q in jc.STD_LUM_Q], 0)])
                    head += jc.sof(marker, 8, h, w, [(cid, hh, vv, 0 if i == 0 else 1) for i, (cid, hh, vv) in enumerate(comps)])
                    if marker in (0xC9, 0xCA) and trial % 2 == 1:
                        head += jc.dac([(0, rnd.choice([0x00, 0x21, 0x42, 0x13])), (1, 0x42), (16, rnd.choice([3, 9, 63])), (17, 9)])
                    else:
                        head += jc.std_dht_all()
                    if rnd.random() < 0.3:
                        head += jc.dri(rnd.choice([1, 2, 3, 7]))
                    body = b''
                    if marker in (0xC2, 0xCA):
                        first = comps[0][0]
                        script = [([cc[0] for cc in comps], 0, 0, 0, 1)]
                        for cc in comps:
                            script.append(([cc[0]], 1, 5, 0, 2))
                        for cc in comps:
                            script.append(([cc[0]], 6, 63, 0, 2))
                        script += [([first], 1, 63, 2, 1), ([first], 1, 63, 1, 0), ([cc[0] for cc in comps], 0, 0, 1, 0)]
                        if trial >= 2:
                            rnd.shuffle(script)
                        for cids, ss, se, ah, al in script:
                            body += jc.sos([(cid, 0 if cid == first else 1, 0 if cid == first else 1) for cid in cids], ss, se, ah, al)
                            body += jc.random_entropy(rnd, rnd.choice([3, 20, 120, 400]))
                    else:
                        body += jc.sos([(cid, 0 if i == 0 else 1, 0 if i == 0 else 1) for i, (cid, _, _) in enumerate(comps)])
                        body += jc.random_entropy(rnd, rnd.choice([10, 80, 300, 900, 3000]))
                    data = head + body + (jc.eoi() if rnd.random() < 0.85 else b'')
                    for flags in (0, 1):
                        c.add_imdecode('rnd_%s_%dc%d_%dx%d_t%d_f%d' % (kind, len(comps), ci, w, h, trial, flags), data, flags,
                                       'random entropy bytes under a %s frame header' % kind)
                    n += 1
    return n


def mutation_cases(c, rnd, quick):
    """Valid streams with random byte changes, truncations and inserted marker bytes."""
    n = 0
    bases = []
    for comps in ([(1, 1, 1)], [(1, 2, 1), (2, 1, 1), (3, 1, 1)], [(1, 2, 2), (2, 1, 1), (3, 1, 1)]):
        bases.append(jc.build_baseline(24, 24, comps, rnd, restart_interval=rnd.choice([0, 2, 3]), interleaved=True))
    count = 40 if quick else 220
    for i in range(count):
        d = bytearray(rnd.choice(bases))
        mode = rnd.choice(['flip', 'trunc', 'insert', 'flipmany', 'rst', 'dup'])
        if mode == 'flip':
            for _ in range(rnd.randint(1, 3)):
                d[rnd.randrange(len(d))] = rnd.randrange(256)
        elif mode == 'flipmany':
            for _ in range(rnd.randint(5, 40)):
                d[rnd.randrange(len(d))] = rnd.randrange(256)
        elif mode == 'trunc':
            d = d[:rnd.randrange(2, len(d))]
        elif mode == 'insert':
            for _ in range(rnd.randint(1, 3)):
                p = rnd.randrange(2, len(d))
                d[p:p] = bytes([0xFF, rnd.choice([0x00, 0xD0, 0xD3, 0xD7, 0xD9, 0xC4, 0xDA, 0xE1, 0xFE, 0x01, 0x02, 0xFF])])
        elif mode == 'rst':
            for _ in range(rnd.randint(1, 4)):
                p = rnd.randrange(len(d) // 2, len(d))
                d[p:p] = bytes([0xFF, 0xD0 + rnd.randrange(8)])
        else:
            p = rnd.randrange(2, len(d))
            d[p:p] = d[rnd.randrange(2, len(d)):][:rnd.randrange(1, 30)]
        for flags in (0, 1):
            c.add_imdecode('mut_%03d_%s_f%d' % (i, mode, flags), bytes(d), flags, 'mutation: ' + mode)
        n += 1
    return n


def header_cases(c, rnd):
    """One crafted file per header branch (rows J46..J51, J76, J103)."""
    std = jc.std_dht_all()
    q0 = jc.dqt([(0, jc.STD_LUM_Q, 0), (1, jc.STD_LUM_Q, 0)])
    ent = jc.random_entropy(rnd, 400)
    gray = [(1, 1, 1, 0)]
    col = [(1, 2, 1, 0), (2, 1, 1, 1), (3, 1, 1, 1)]
    sos1 = jc.sos([(1, 0, 0)])
    sos3 = jc.sos([(1, 0, 0), (2, 1, 1), (3, 1, 1)])

    def add(name, data, flags=(0, 1), note=''):
        for f in flags:
            c.add_imdecode('hdr_%s_f%d' % (name, f), bytes(data), f, note or name)

    base = jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi()
    basec = jc.soi() + jc.app0_jfif() + q0 + jc.sof(0xC0, 8, 16, 16, col) + std + sos3 + ent + jc.eoi()
    add('ok_gray', base)
    add('ok_col', basec)
    add('no_soi', base[2:])
    add('only_ffd8ff', b'\xFF\xD8\xFF')
    add('two_bytes', b'\xFF\xD8')
    add('three_bytes_bad', b'\xFF\xD8\x00')
    add('bmp_signature', b'BM' + bytes(60))
    add('soi_twice', jc.soi() + base)
    add('eoi_first', jc.soi() + jc.eoi())
    add('tables_only', jc.soi() + q0 + std + jc.eoi())
    add('empty_after_soi', jc.soi())
    for cut in (4, 10, 20, 30, 70, 100, 160, 300, 330, len(base) - 3):
        add('trunc_%d' % cut, base[:cut])
    # frame header
    add('sof_dup', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('sof_zero_h', jc.soi() + q0 + jc.sof(0xC0, 8, 0, 16, gray) + std + sos1 + ent + jc.eoi())
    add('sof_zero_w', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 0, gray) + std + sos1 + ent + jc.eoi())
    add('sof_zero_comp', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, []) + std + sos1 + ent + jc.eoi())
    add('sof_prec12', jc.soi() + q0 + jc.sof(0xC0, 12, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('sof_prec0', jc.soi() + q0 + jc.sof(0xC0, 0, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('sof_badlen', jc.soi() + q0 + b'\xFF\xC0\x00\x0C\x08\x00\x10\x00\x10\x01\x01\x11\x00\x00\x00' + std + sos1 + ent + jc.eoi())
    for m in (0xC3, 0xC5, 0xC6, 0xC7, 0xC8, 0xCB, 0xCD, 0xCE, 0xCF):
        add('sof_%02x' % m, jc.soi() + q0 + jc.sof(m, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('sof_big', jc.soi() + q0 + jc.sof(0xC0, 8, 65535, 65535, gray) + std + sos1 + ent + jc.eoi(), note='dimensions above 65500')
    add('sof_65501', jc.soi() + q0 + jc.sof(0xC0, 8, 65501, 8, gray) + std + sos1 + ent + jc.eoi())
    for comps, nm in (([(1, 1, 1, 0), (2, 1, 1, 0)], '2c'), ([(i + 1, 1, 1, 0) for i in range(5)], '5c'), ([(i + 1, 1, 1, 0) for i in range(10)], '10c'),
                      ([(i + 1, 1, 1, 0) for i in range(11)], '11c')):
        add('sof_%s' % nm, jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, comps) + std + jc.sos([(cc[0], 0, 0) for cc in comps[:4]]) + ent + jc.eoi())
    for h, v in ((0, 1), (1, 0), (5, 1), (1, 5), (15, 15)):
        add('sof_samp_%d_%d' % (h, v), jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, [(1, h, v, 0)]) + std + sos1 + ent + jc.eoi())
    add('sof_dup_ids', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, [(1, 2, 1, 0), (1, 1, 1, 1), (1, 1, 1, 1)]) + std + jc.sos([(1, 0, 0), (2, 1, 1), (3, 1, 1)]) + ent + jc.eoi())
    add('sof_dup_ids2', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, [(7, 2, 1, 0), (7, 1, 1, 1), (7, 1, 1, 1)]) + std + jc.sos([(7, 0, 0), (8, 1, 1), (9, 1, 1)]) + ent + jc.eoi())
    add('sof_ids_rgb', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, [(82, 1, 1, 0), (71, 1, 1, 1), (66, 1, 1, 1)]) + std + jc.sos([(82, 0, 0), (71, 1, 1), (66, 1, 1)]) + ent + jc.eoi())
    add('sof_ids_other', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, [(10, 1, 1, 0), (20, 1, 1, 1), (30, 1, 1, 1)]) + std + jc.sos([(10, 0, 0), (20, 1, 1), (30, 1, 1)]) + ent + jc.eoi())
    # SOS
    add('sos_no_sof', jc.soi() + q0 + std + sos1 + ent + jc.eoi())
    add('sos_badlen', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + b'\xFF\xDA\x00\x0A\x01\x01\x00\x00\x3F\x00' + ent + jc.eoi())
    add('sos_5comps', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, col) + std + jc.sos([(1, 0, 0), (2, 1, 1), (3, 1, 1), (1, 0, 0), (2, 0, 0)]) + ent + jc.eoi())
    add('sos_unknown_id', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + jc.sos([(9, 0, 0)]) + ent + jc.eoi())
    add('sos_dup_id', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, col) + std + jc.sos([(1, 0, 0), (1, 1, 1), (3, 1, 1)]) + ent + jc.eoi())
    add('sos_zero_comp_seq', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + b'\xFF\xDA\x00\x08\x00\x00\x3F\x00' + ent + jc.eoi())
    add('sos_reorder', jc.soi() + jc.app0_jfif() + q0 + jc.sof(0xC0, 8, 16, 16, col) + std + jc.sos([(3, 1, 1), (1, 0, 0), (2, 1, 1)]) + ent + jc.eoi())
    add('sos_noninterleaved', jc.soi() + jc.app0_jfif() + q0 + jc.sof(0xC0, 8, 16, 16, col) + std + jc.sos([(1, 0, 0)]) + jc.random_entropy(rnd, 100) +
        jc.sos([(2, 1, 1)]) + jc.random_entropy(rnd, 60) + jc.sos([(3, 1, 1)]) + jc.random_entropy(rnd, 60) + jc.eoi())
    add('sos_ss_se', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + jc.sos([(1, 0, 0)], 3, 20, 0, 0) + ent + jc.eoi())
    add('sos_ah_al', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + jc.sos([(1, 0, 0)], 0, 63, 2, 1) + ent + jc.eoi())
    # tables
    add('dqt_idx4', jc.soi() + jc.dqt([(4, jc.STD_LUM_Q, 0)]) + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('dqt_16bit', jc.soi() + jc.dqt([(0, jc.STD_LUM_Q, 1)]) + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('dqt_16bit_big', jc.soi() + jc.dqt([(0, [60000 - i for i in range(64)], 1)]) + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('dqt_zero', jc.soi() + jc.dqt([(0, [0] * 64, 0)]) + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('no_dqt', jc.soi() + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    for n in (4, 9, 16, 25, 36, 49, 5, 63, 100):
        add('dqt_short_%d' % n, jc.soi() + jc.dqt_raw(0, [3 + i for i in range(n)]) + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('dqt_trailing', jc.soi() + jc.seg(0xDB, bytes([0]) + bytes(range(1, 65)) + b'\x05\x06') + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('dht_idx4', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.dht([(0x04, *jc.STD_DC_LUM)]) + std + sos1 + ent + jc.eoi())
    add('dht_count_over', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.dht([(0x00, [255] * 16, list(range(12)))]) + std + sos1 + ent + jc.eoi())
    add('dht_oversub', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.dht([(0x00, [3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 1, 2])]) + jc.dht([(0x10, *jc.STD_AC_LUM)]) + sos1 + ent + jc.eoi())
    add('dht_dc_sym16', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.dht([(0x00, [0, 1, 1] + [0] * 13, [0, 16])]) + jc.dht([(0x10, *jc.STD_AC_LUM)]) + sos1 + ent + jc.eoi())
    add('dht_missing_dc', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.dht([(0x10, *jc.STD_AC_LUM)]) + sos1 + ent + jc.eoi())
    add('dht_missing_ac', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.dht([(0x00, *jc.STD_DC_LUM)]) + sos1 + ent + jc.eoi())
    add('dht_only_slot1', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.dht([(0x01, *jc.STD_DC_CHR), (0x11, *jc.STD_AC_CHR)]) + jc.sos([(1, 1, 1)]) + ent + jc.eoi())
    add('dht_slot2_only', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.dht([(0x02, *jc.STD_DC_CHR), (0x12, *jc.STD_AC_CHR)]) + jc.sos([(1, 2, 2)]) + ent + jc.eoi())
    add('dht_trailing', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + jc.seg(0xC4, bytes([0x00] + jc.STD_DC_LUM[0] + jc.STD_DC_LUM[1] + [1, 2, 3])) + std + sos1 + ent + jc.eoi())
    add('default_tables', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + sos1 + ent + jc.eoi(), note='no DHT: the default MJPEG tables')
    add('default_tables_col', jc.soi() + jc.app0_jfif() + q0 + jc.sof(0xC0, 8, 16, 16, col) + sos3 + ent + jc.eoi())
    add('default_tables_prog', jc.soi() + q0 + jc.sof(0xC2, 8, 16, 16, gray) + jc.sos([(1, 0, 0)], 0, 0, 0, 1) + ent + jc.eoi())
    add('dac_badlen', jc.soi() + q0 + jc.sof(0xC9, 8, 16, 16, gray) + jc.seg(0xCC, b'\x00\x21\x01') + sos1 + ent + jc.eoi())
    add('dac_badidx', jc.soi() + q0 + jc.sof(0xC9, 8, 16, 16, gray) + jc.dac([(32, 1)]) + sos1 + ent + jc.eoi())
    add('dac_badval', jc.soi() + q0 + jc.sof(0xC9, 8, 16, 16, gray) + jc.dac([(0, 0x20)]) + sos1 + ent + jc.eoi())
    add('dri_badlen', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + jc.seg(0xDD, b'\x00\x01\x00') + sos1 + ent + jc.eoi())
    for m in (0x02, 0x4F, 0x80, 0xBF, 0xC8, 0xF0, 0xFD, 0xDC, 0xDE, 0xDF, 0xE1, 0xEF, 0xFE, 0x01, 0xD0, 0xFF):
        add('marker_%02x' % m, jc.soi() + q0 + jc.seg(m, b'xyz') + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('com_marker', jc.soi() + jc.seg(0xFE, b'hello') + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('app_long', jc.soi() + jc.seg(0xE1, bytes(300)) + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('seg_short_len', jc.soi() + b'\xFF\xE1\x00\x01' + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('dnl_marker', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.seg(0xDC, b'\x00\x10') + jc.eoi())
    add('ff_padding', jc.soi() + b'\xFF\xFF\xFF' + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + b'junk\x00\x01' + sos1 + ent + jc.eoi())
    # colour spaces
    for ad in (None, 0, 1, 2, 3, 77):
        for nm, comps, scan in (('1', gray, sos1), ('3', col, sos3), ('4', [(1, 1, 1, 0), (2, 1, 1, 1), (3, 1, 1, 1), (4, 1, 1, 0)],
                                                                  jc.sos([(1, 0, 0), (2, 1, 1), (3, 1, 1), (4, 0, 0)]))):
            hd = jc.soi() + (jc.app14_adobe(ad) if ad is not None else b'') + q0 + jc.sof(0xC0, 8, 16, 16, comps) + std + scan + jc.random_entropy(rnd, 500) + jc.eoi()
            add('adobe_%s_%s' % (ad, nm), hd)
    for nm, comps, scan in (('4', [(1, 1, 1, 0), (2, 1, 1, 1), (3, 1, 1, 1), (4, 1, 1, 0)], jc.sos([(1, 0, 0), (2, 1, 1), (3, 1, 1), (4, 0, 0)])),):
        for jf in (True, False):
            add('jfif_4comp_%s' % jf, jc.soi() + (jc.app0_jfif() if jf else b'') + q0 + jc.sof(0xC0, 8, 16, 16, comps) + std + scan + jc.random_entropy(rnd, 500) + jc.eoi())
    add('jfif_version2', jc.soi() + jc.app0_jfif(major=2) + q0 + jc.sof(0xC0, 8, 16, 16, col) + std + sos3 + ent + jc.eoi())
    add('jfxx', jc.soi() + jc.seg(0xE0, b'JFXX\x00\x10') + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi())
    add('ycck_with_sampling', jc.soi() + jc.app14_adobe(2) + q0 + jc.sof(0xC0, 8, 16, 16, [(1, 2, 2, 0), (2, 1, 1, 1), (3, 1, 1, 1), (4, 2, 2, 0)]) + std +
        jc.sos([(1, 0, 0), (2, 1, 1), (3, 1, 1), (4, 0, 0)]) + jc.random_entropy(rnd, 800) + jc.eoi())
    # entropy segment edge cases
    for n in (0, 1, 2, 3, 5, 9, 17, 40):
        add('entropy_%d' % n, jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + jc.random_entropy(rnd, n) + jc.eoi())
        add('entropy_noeoi_%d' % n, jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + jc.random_entropy(rnd, n))
    add('entropy_marker_mid', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + jc.random_entropy(rnd, 20) + b'\xFF\xD0' + jc.random_entropy(rnd, 30) + jc.eoi())
    add('entropy_junk_after_eoi', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + jc.eoi() + bytes(50))
    add('entropy_no_eoi_after_scan', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent)
    add('multi_sos_unexpected', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + ent + sos1 + ent + jc.eoi())
    add('eoi_in_scan_start', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, gray) + std + sos1 + jc.eoi())
    # restart handling with real RSTs of the wrong number / missing
    comps = [(1, 1, 1)]
    for variant in ('ok', 'skip', 'extra', 'wrong', 'far'):
        d = jc.build_baseline(32, 16, comps, rnd, restart_interval=2, interleaved=True)
        i = d.find(b'\xFF\xD0')
        if i > 0:
            if variant == 'skip':
                d = d[:i] + d[i + 2:]
            elif variant == 'extra':
                d = d[:i] + b'\xFF\xD0\xFF\xD1' + d[i:]
            elif variant == 'wrong':
                d = d[:i + 1] + bytes([0xD3]) + d[i + 2:]
            elif variant == 'far':
                d = d[:i + 1] + bytes([0xD5]) + d[i + 2:]
        add('rst_%s' % variant, d)


def corpus_rest(c, rnd):
    # scaled non-baseline sequential (Se != 63): block sizes other than 8 (J77) - recorded so the unported branch is visible
    q0 = jc.dqt([(0, jc.STD_LUM_Q, 0)])
    std = jc.std_dht_all()
    for se in (0, 3, 8, 15, 24, 35, 48, 63, 80, 99, 120, 143, 168, 195, 224, 255, 5, 62):
        d = jc.soi() + q0 + jc.sof(0xC1, 8, 16, 16, [(1, 1, 1, 0)]) + std + jc.sos([(1, 0, 0)], 0, se, 0, 0) + jc.random_entropy(rnd, 300) + jc.eoi()
        for f in (0, 1):
            c.add_imdecode('scaled_seq_se%d_f%d' % (se, f), d, f, 'extended sequential with Se=%d (block size)' % se)
    # scaled block sizes with subsampled chroma: the rectangular and doubled transforms (rows J95..J102), with entropy data that runs out
    # inside the image (the skipped-MCU path) and with enough of it, for the Huffman and the arithmetic coder
    for n in range(1, 17):
        se = n * n - 1 if n <= 8 else (80, 99, 120, 143, 168, 195, 224, 255)[n - 9]
        for ci, comps in enumerate(([(1, 2, 1), (2, 1, 1), (3, 1, 1)], [(1, 1, 2), (2, 1, 1), (3, 1, 1)], [(1, 2, 2), (2, 1, 1), (3, 1, 1)],
                                      [(1, 1, 1), (2, 1, 1), (3, 1, 1)], [(1, 4, 1), (2, 1, 1), (3, 1, 1)])):
            for marker, nm in ((0xC1, 'h'), (0xC9, 'a')):
                for elen in (150, 1500):
                    hd = jc.soi() + jc.app0_jfif() + jc.dqt([(0, jc.STD_LUM_Q, 0), (1, jc.STD_LUM_Q, 0)]) + jc.sof(marker, 8, 24, 40, [(cid, h_, v_, 0 if i == 0 else 1) for i, (cid, h_, v_) in enumerate(comps)])
                    d = hd + (std if marker == 0xC1 else b'') + jc.sos([(cid, 0 if i == 0 else 1, 0 if i == 0 else 1) for i, (cid, _, _) in enumerate(comps)], 0, se, 0, 0) + jc.random_entropy(rnd, elen) + jc.eoi()
                    for f in (0, 1):
                        c.add_imdecode('scaled_seq3_%s_n%d_c%d_e%d_f%d' % (nm, n, ci, elen, f), d, f, 'sequential 3 components, block size %d (Se=%d), %d entropy bytes' % (n, se, elen))
    for se in (0, 15, 63):
        d = jc.soi() + q0 + jc.sof(0xC9, 8, 16, 16, [(1, 1, 1, 0)]) + jc.sos([(1, 0, 0)], 0, se, 0, 0) + jc.random_entropy(rnd, 300) + jc.eoi()
        for f in (0, 1):
            c.add_imdecode('scaled_arith_se%d_f%d' % (se, f), d, f, 'arithmetic sequential with Se=%d' % se)


def lse_cases(c, rnd):
    """The JPEG-LS preset marker 0xF8 (rows L1..L18): the valid 22-byte form, every failure variant, the colour-space combinations that reach the
    0x1C error of jinit_color_deconverter, and the rgb1 conversions through every sampling layout."""
    q0 = jc.dqt([(0, jc.STD_LUM_Q, 0), (1, jc.STD_LUM_Q, 0)])
    std = jc.std_dht_all()
    ent = jc.random_entropy(rnd, 600)
    ids = (82, 71, 66)                                   # R, G, B

    def lse_payload(a=None, ident=ids):
        a = a or ident
        return bytes([0x0D, 0x00, 0xFF, 3, a[1], a[0], a[2], 0x80, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0])

    def lse(ident=ids, length=None, payload=None):
        p = lse_payload(ident=ident) if payload is None else payload
        return b'\xFF\xF8' + struct.pack('>H', (len(p) + 2) if length is None else length) + p

    def comps(ident, hv=((1, 1), (1, 1), (1, 1))):
        return [(ident[i], hv[i][0], hv[i][1], 0 if i == 0 else 1) for i in range(3)]

    def scan(ident):
        return jc.sos([(ident[0], 0, 0), (ident[1], 1, 1), (ident[2], 1, 1)])

    def file_(ident=ids, lse_bytes=None, hv=((1, 1), (1, 1), (1, 1)), pre=b'', marker=0xC0, w=16, h=16, entropy=None, scan_=None, before_sof=False, after=b''):
        lb = lse(ident) if lse_bytes is None else lse_bytes
        head = jc.soi() + pre + q0
        sof = jc.sof(marker, 8, h, w, comps(ident, hv))
        body = (lb + sof) if before_sof else (sof + lb)
        return head + body + after + std + (scan_ or scan(ident)) + (ent if entropy is None else entropy) + jc.eoi()

    def add(name, data):
        for f in (0, 1):
            c.add_imdecode('lse_%s_f%d' % (name, f), bytes(data), f, 'LSE marker: ' + name)

    add('ok', file_())
    add('ok_ids123_adobe0', file_(ident=(1, 2, 3), pre=jc.app14_adobe(0)))
    add('ok_big', file_(w=48, h=40, entropy=jc.random_entropy(rnd, 3000)))
    add('ok_twice', file_(lse_bytes=lse() + lse()))
    add('ok_with_dri', file_(after=jc.dri(2)))
    for nm, hv in (('h2v1', ((2, 1), (1, 1), (1, 1))), ('h2v2', ((2, 2), (1, 1), (1, 1))), ('h1v2', ((1, 2), (1, 1), (1, 1))), ('h4v1', ((4, 1), (1, 1), (1, 1))),
                   ('all2', ((2, 2), (2, 2), (2, 2))), ('chroma_big', ((1, 1), (2, 2), (2, 1)))):
        add('ok_' + nm, file_(hv=hv, w=40, h=24, entropy=jc.random_entropy(rnd, 2500)))
    add('ok_noentropy', file_(entropy=b''))
    add('ok_progressive', file_(marker=0xC2, scan_=jc.sos([(ids[0], 0, 0), (ids[1], 1, 1), (ids[2], 1, 1)], 0, 0, 0, 1)))
    add('ok_arith', file_(marker=0xC9, entropy=jc.random_entropy(rnd, 500)))
    add('ok_ids_other', file_(ident=(10, 20, 30)))
    add('ok_dup_ids', file_(ident=(7, 7, 7)))
    # failures, in the order the shipped get_lse makes its tests
    add('before_sof', file_(before_sof=True))
    add('no_sof', jc.soi() + q0 + lse() + std + scan(ids) + ent + jc.eoi())
    for n_, cm in (('1comp', [(1, 1, 1, 0)]), ('2comp', [(1, 1, 1, 0), (2, 1, 1, 1)])):
        add(n_, jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, cm) + lse() + std + jc.sos([(cc[0], 0, 0) for cc in cm]) + ent + jc.eoi())
    add('4comp', jc.soi() + q0 + jc.sof(0xC0, 8, 16, 16, [(82, 1, 1, 0), (71, 1, 1, 1), (66, 1, 1, 1), (65, 1, 1, 0)]) + lse() + std +
        jc.sos([(82, 0, 0), (71, 1, 1), (66, 1, 1), (65, 0, 0)]) + ent + jc.eoi())
    for ln in (0, 1, 2, 22, 23, 25, 26, 0x118, 0xFFFF):
        add('len_%d' % ln, file_(lse_bytes=lse(length=ln)))
    for val in (0x00, 0x0C, 0x0E, 0xFF):
        p = bytearray(lse_payload()); p[0] = val
        add('idbyte_%02x' % val, file_(lse_bytes=lse(payload=bytes(p))))
    for pos in range(1, 22):
        for val in (None, 0x7F):
            p = bytearray(lse_payload())
            p[pos] = (p[pos] ^ 1) if val is None else val
            if bytes(p) == lse_payload():
                continue
            add('payload_%02d_%s' % (pos, 'x1' if val is None else '7f'), file_(lse_bytes=lse(payload=bytes(p))))
    add('ids_swapped', file_(lse_bytes=lse(ident=(71, 82, 66))))
    add('ids_wrong', file_(lse_bytes=lse(ident=(1, 2, 3))))
    # a payload cut inside the segment (the source runs dry: suspension)
    full = file_()
    k = full.index(b'\xFF\xF8')
    for cut in range(k, k + 28):
        add('trunc_%d' % (cut - k), full[:cut])
    # LSE with a colour space the deconverter refuses (L11: 0x1C for any non-RGB input, both output spaces)
    add('jfif_ycc', file_(ident=(1, 2, 3), pre=jc.app0_jfif()))
    add('ids123_ycc', file_(ident=(1, 2, 3)))
    add('adobe1', file_(ident=(82, 71, 66), pre=jc.app14_adobe(1)))
    add('adobe0_rgb', file_(ident=(10, 20, 30), pre=jc.app14_adobe(0)))
    add('adobe2', file_(ident=(82, 71, 66), pre=jc.app14_adobe(2)))
    add('jfif_rgbids', file_(pre=jc.app0_jfif()))
    add('cmyk', jc.soi() + jc.app14_adobe(0) + q0 + jc.sof(0xC0, 8, 16, 16, [(1, 1, 1, 0), (2, 1, 1, 1), (3, 1, 1, 1), (4, 1, 1, 0)]) + lse((2, 1, 3)) + std +
        jc.sos([(1, 0, 0), (2, 1, 1), (3, 1, 1), (4, 0, 0)]) + ent + jc.eoi())
    add('ycck', jc.soi() + jc.app14_adobe(2) + q0 + jc.sof(0xC0, 8, 16, 16, [(1, 1, 1, 0), (2, 1, 1, 1), (3, 1, 1, 1), (4, 1, 1, 0)]) + lse((2, 1, 3)) + std +
        jc.sos([(1, 0, 0), (2, 1, 1), (3, 1, 1), (4, 0, 0)]) + ent + jc.eoi())
    # an LSE followed by a second SOI (get_soi's duplicate check fires before its resets, row L10)
    add('then_second_soi', file_(after=jc.soi()))
    # random entropy, many seeds, so the rgb1 arithmetic sees wrapping values (c0 + c1 - 128 outside 0..255)
    for i in range(12):
        e = jc.random_entropy(rnd, 700 + 40 * i)
        add('rand_%d' % i, file_(w=24 + 8 * (i % 3), h=16 + 8 * (i % 2), hv=(((i % 2) + 1, 1), (1, 1), (1, 1)), entropy=e))


def oom_cases(c, rnd):
    """Dimensions whose Mat the shipped Mat::create cannot allocate (rows O1..O8b): the 32-bit total overflow (-211) and the failed malloc (-4)."""
    q0 = jc.dqt([(0, jc.STD_LUM_Q, 0)])
    std = jc.std_dht_all()
    gray = [(1, 1, 1, 0)]
    col = [(1, 1, 1, 0), (2, 1, 1, 1), (3, 1, 1, 1)]
    ent = jc.random_entropy(rnd, 200)
    for w, h, nm in ((65500, 65500, '65500sq'), (65500, 40000, '65500x40000'), (50000, 50000, '50000sq')):
        for comps, scan, cn in ((gray, jc.sos([(1, 0, 0)]), 'g'), (col, jc.sos([(1, 0, 0), (2, 1, 1), (3, 1, 1)]), 'c')):
            data = jc.soi() + q0 + jc.sof(0xC0, 8, h, w, comps) + std + scan + ent + jc.eoi()
            for f in (0, 1):
                if w * h * (3 if f else 1) < 1700000000:      # an allocation the emulator's stand-in heap would grant: not an OOM case
                    continue
                c.add_imdecode('oom_%s_%s_f%d' % (nm, cn, f), data, f, 'Mat::create of %dx%d channels %d' % (h, w, 3 if f else 1))


def resize_cases(c, rnd, quick):
    sizes = [(160, 240, 320, 240), (8, 6, 16, 6), (17, 9, 34, 9), (5, 4, 10, 4), (9, 7, 11, 8), (20, 20, 21, 33), (30, 10, 7, 5), (64, 48, 100, 75),
             (13, 13, 13, 40), (3, 3, 17, 17), (40, 30, 80, 60), (100, 100, 60, 40), (1, 1, 5, 5), (2, 2, 3, 3), (7, 3, 7, 3), (160, 240, 320, 241)]
    if quick:
        sizes = sizes[:6]
    for (sc, sr, dc, dr) in sizes:
        for ch in (1, 3):
            for kind in ('rand', 'ramp', 'extreme'):
                if kind == 'rand':
                    data = bytes(rnd.randrange(256) for _ in range(sc * sr * ch))
                elif kind == 'ramp':
                    data = bytes(((x * 7 + y * 13 + k * 50) % 256) for y in range(sr) for x in range(sc) for k in range(ch))
                else:
                    data = bytes(rnd.choice([0, 255]) for _ in range(sc * sr * ch))
                c.add_resize('rs_%dx%d_to_%dx%d_c%d_%s' % (sc, sr, dc, dr, ch, kind), sr, sc, ch, data, dr, dc, 'cv::resize INTER_LINEAR 8U')
    c.add_resize('rs_empty', 0, 0, 1, b'', 240, 320, 'empty source: cv::error')


def engine_cases(c, rnd, quick, frames_dir=None):
    import glob
    # real camera frames: the reconstructed gray JPEGs the hardware bundles saved, turned back into mini payloads (flag byte 0 +
    # entropy data with the FF00 stuffing removed, exactly what the engine's MiniToJpegHelper reverses)
    n = 0
    for f in sorted(glob.glob(os.path.join(ROOT, 're-analysis', 'acceptance', 'hardware', '*', 'camera-*.jpg'))):
        jpg = open(f, 'rb').read()
        if jpg[:4] != b'\xFF\xD8\xFF\xE0' or len(jpg) < 330:
            continue
        payload = b'\x00' + jpg[324:-2].replace(b'\xFF\x00', b'\xFF')
        tag = os.path.basename(os.path.dirname(f))[:15] + '_' + os.path.basename(f)[:-4]
        for gray in (True, False):
            c.add_engine('hw_%s_%s' % (tag, 'g' if gray else 'c'), 8, gray, payload, 'real robot frame (hardware acceptance bundle)')
        n += 1
        if quick and n >= 3:
            break
    if frames_dir and os.path.isdir(frames_dir):
        for f in sorted(os.listdir(frames_dir)):
            payload = open(os.path.join(frames_dir, f), 'rb').read()
            for gray in (True, False):
                c.add_engine('probe_%s_%s' % (f[:-4], 'g' if gray else 'c'), 8, gray, payload, 'real robot frame (fw2457 probe capture)')
            if quick:
                break
    # the robot's own path with damage: byte changes, truncation (the strip of trailing 0xFF included), inserted 0xFF runs and a lost leading
    # chunk (the entropy stream starts mid-symbol), on real frames
    real = sorted(glob.glob(os.path.join(ROOT, 're-analysis', 'acceptance', 'hardware', '*', 'camera-*.jpg')))
    for i in range(4 if quick else 24):
        if not real:
            break
        jpg = open(real[i % len(real)], 'rb').read()
        base = bytearray(b'\x00' + jpg[324:-2].replace(b'\xFF\x00', b'\xFF'))
        mode = ('flip', 'truncate', 'ffrun', 'dropfront', 'flipmany', 'zeroes')[i % 6]
        if mode == 'flip':
            for _ in range(rnd.randint(1, 4)):
                base[rnd.randrange(1, len(base))] = rnd.randrange(256)
        elif mode == 'flipmany':
            for _ in range(60):
                base[rnd.randrange(1, len(base))] = rnd.randrange(256)
        elif mode == 'truncate':
            base = base[:rnd.randrange(2, len(base))]
        elif mode == 'ffrun':
            p_ = rnd.randrange(1, len(base))
            base[p_:p_] = b'\xFF' * rnd.randint(1, 9)
            base += b'\xFF' * rnd.randint(0, 5)
        elif mode == 'dropfront':
            base = base[:1] + base[rnd.randint(2, 1000):]
        else:
            p_ = rnd.randrange(1, len(base))
            base[p_:p_ + rnd.randint(1, 200)] = bytes(rnd.randint(1, 200))
        for gray in (True, False):
            c.add_engine('mini_mut_%02d_%s_%s' % (i, mode, 'g' if gray else 'c'), 8, gray, bytes(base), 'real robot frame, damaged: ' + mode)
    # synthetic mini frames
    robot_q = [0x10, 0x0B, 0x0C, 0x0E, 0x0C, 0x0A, 0x10, 0x0E, 0x0D, 0x0E, 0x12, 0x11, 0x10, 0x13, 0x18, 0x28, 0x1A, 0x18, 0x16, 0x16, 0x18, 0x31, 0x23, 0x25, 0x1D, 0x28,
               0x3A, 0x33, 0x3D, 0x3C, 0x39, 0x33, 0x38, 0x37, 0x40, 0x48, 0x5C, 0x4E, 0x40, 0x44, 0x57, 0x45, 0x37, 0x38, 0x50, 0x6D, 0x51, 0x57, 0x5F, 0x62, 0x67, 0x68, 0x67,
               0x3E, 0x4D, 0x71, 0x79, 0x70, 0x64, 0x78, 0x5C, 0x65, 0x67, 0x63]
    del robot_q
    tables = {('dc', 0): jc.STD_DC_LUM, ('ac', 0): jc.STD_AC_LUM, ('dc', 1): jc.STD_DC_CHR, ('ac', 1): jc.STD_AC_CHR}
    for i in range(3 if quick else 10):
        # gray 320x240
        comps5 = [(1, 1, 1, 0, 0)]
        bw_, bh_ = 40, 30
        lst = jc.random_blocks(rnd, bw_ * bh_, 0.6 + 0.2 * i, dc_range=120)
        bd = [{(x, y): lst[y * bw_ + x] for y in range(bh_) for x in range(bw_)}]
        scans = jc.encode_baseline_scan(comps5, 320, 240, bd, tables)
        gp = b'\x00' + scans[0][1].replace(b'\xFF\x00', b'\xFF')
        # colour: JPEG width 160, Y 2x1
        compsc = [(1, 2, 1, 0, 0), (2, 1, 1, 1, 1), (3, 1, 1, 1, 1)]
        sizes = jc.blocks_per_component(compsc, 160, 240)
        bdc = []
        for (bw2, bh2) in sizes:
            lst = jc.random_blocks(rnd, bw2 * bh2, 0.5 + 0.2 * i, dc_range=100)
            bdc.append({(x, y): lst[y * bw2 + x] for y in range(bh2) for x in range(bw2)})
        scans = jc.encode_baseline_scan(compsc, 160, 240, bdc, tables)
        cp = b'\x01' + scans[0][1].replace(b'\xFF\x00', b'\xFF')
        for gray in (True, False):
            c.add_engine('syn_gray_%d_%s' % (i, 'g' if gray else 'c'), 8, gray, gp, 'synthetic mini gray frame, random coefficients')
            c.add_engine('syn_color_%d_%s' % (i, 'g' if gray else 'c'), 9, gray, cp, 'synthetic mini colour frame, random coefficients')
    # random / degenerate payloads
    lens = [0, 1, 2, 3, 10, 100, 1000, 5000] if not quick else [1, 100]
    for n_ in lens:
        for enc in (8, 9):
            pl = bytes(rnd.randrange(256) for _ in range(n_))
            for gray in (True, False):
                if n_ == 0:
                    continue          # the empty strip reads data[-1] (policy M3-020); not decodable by the shipped code
                c.add_engine('rnd_%d_%d_%s' % (enc, n_, 'g' if gray else 'c'), enc, gray, pl, 'random payload')
    for enc in (8, 9):
        for pl, nm in ((b'\x00' + b'\xFF' * 3 + b'\x01', 'trailing_ff'), (b'\x00\xFF\x00\xFF\x01', 'ff_inside'), (b'\x00\x00', 'two_zero')):
            for gray in (True, False):
                c.add_engine('deg_%d_%s_%s' % (enc, nm, 'g' if gray else 'c'), enc, gray, pl, 'degenerate payload')
    # every other encoding value, through the dispatch
    raw_gray = bytes((i * 7) & 255 for i in range(320 * 240))
    raw_rgb = bytes((i * 5) & 255 for i in range(320 * 240 * 3))
    for gray in (True, False):
        g = 'g' if gray else 'c'
        c.add_engine('enc1_full_%s' % g, 1, gray, raw_gray, 'RawGray, exact size', store_out=False)
        c.add_engine('enc2_full_%s' % g, 2, gray, raw_rgb, 'RawRGB, exact size')
        c.add_engine('enc1_long_%s' % g, 1, gray, raw_gray + bytes(10), 'RawGray, longer than needed')
        c.add_engine('enc2_long_%s' % g, 2, gray, raw_rgb + bytes(10), 'RawRGB, longer than needed')
        for e in (0, 3, 4, 10, 11, 100, 255):
            c.add_engine('enc%d_%s' % (e, g), e, gray, b'abc', 'unsupported encoding')
        # JPEG file encodings 5, 6, 7 with a real image
        from PIL import Image
        for w, h in ((320, 240), (160, 240), (100, 50), (200, 240)):
            im = natural(w, h, w + h)
            for nm, kw in (('420', dict(subsampling=2)), ('422', dict(subsampling=1)), ('gray', None)):
                b = io.BytesIO()
                if kw is None:
                    im.convert('L').save(b, 'JPEG', quality=80)
                else:
                    im.save(b, 'JPEG', quality=80, **kw)
                for e in (5, 6, 7):
                    if quick and (w, h) != (320, 240):
                        continue
                    c.add_engine('jpg_%d_%dx%d_%s_%s' % (e, w, h, nm, g), e, gray, b.getvalue(), 'JPEG file encoding %d' % e)
        c.add_engine('jpg_5_empty_%s' % g, 5, gray, b'', 'empty JPEG vector: the cv::error assertion')
        c.add_engine('jpg_5_garbage_%s' % g, 5, gray, b'\xFF\xD8\xFFgarbage', 'JPEG signature then garbage')
        c.add_engine('jpg_7_garbage_%s' % g, 7, gray, b'\xFF\xD8\xFFgarbage', 'half-width: copyMakeBorder of an empty Mat')


def idct_vectors(oracle, rnd, per):
    """Random and extreme coefficient blocks through each of the 32 shipped inverse DCT bodies (called directly)."""
    cases = []
    for (w, h) in sorted(oracle.idct_functions()):
        for t in range(per):
            kind = t % 6
            coef = [0] * 64
            if kind == 0:
                coef[0] = rnd.randint(-2000, 2000)
            elif kind == 1:
                for _ in range(rnd.randint(1, 6)):
                    coef[rnd.randrange(64)] = rnd.randint(-300, 300)
                coef[0] = rnd.randint(-1000, 1000)
            elif kind == 2:
                coef = [rnd.randint(-40, 40) for _ in range(64)]
            elif kind == 3:
                coef = [rnd.choice([-32768, 32767, 0, 1, -1]) for _ in range(64)]
            elif kind == 4:
                coef = [rnd.randint(-32768, 32767) for _ in range(64)]
            else:
                coef = [rnd.randint(-3, 3) if rnd.random() < 0.3 else 0 for _ in range(64)]
                coef[0] = rnd.randint(-500, 500)
            quant = [rnd.choice([1, 2, 3, 5, 8, 16, 40, 100, 255, 1000, 65535]) if kind in (3, 4) else rnd.randint(1, 60) for _ in range(64)]
            r = oracle.idct(w, h, coef, quant)
            if 'rows' not in r:
                raise RuntimeError('idct oracle failed for %dx%d: %s' % (w, h, r))
            cases.append({'w': w, 'h': h, 'coef': coef, 'quant': quant, 'rows': r['rows']})
    return cases


def dump_static_tables():
    """The static tables of libopencv_imgcodecs.so read from the binary (the tests compare the port's tables with these)."""
    raw = open(os.path.join(ROOT, 'resources', 'lib', 'armeabi-v7a', 'libopencv_imgcodecs.so'), 'rb').read()
    t = {}
    t['natural8'] = list(struct.unpack_from('<80i', raw, 0xCECF0))
    for n, va in ((2, 0xCE938), (3, 0xCE988), (4, 0xCE9F0), (5, 0xCEA70), (6, 0xCEB18), (7, 0xCEBE8)):
        t['natural%d' % n] = list(struct.unpack_from('<%di' % (n * n + 16), raw, va))
    t['arith'] = list(struct.unpack_from('<114I', raw, 0xCF328))
    for n, va in ((2, 0xCEFF0), (3, 0xCF000), (4, 0xCF028), (5, 0xCF068), (6, 0xCF0D0), (7, 0xCF160), (8, 0xCF228)):
        t['limit%d' % n] = list(struct.unpack_from('<%di' % (n * n), raw, va))
    t['defaultDht'] = list(raw[0x174018:0x174018 + 0x1A4])
    return t


def write_outputs(c, out, coverage, quick):
    os.makedirs(out, exist_ok=True)
    json.dump(dump_static_tables(), open(os.path.join(out, 'tables.json'), 'w'))
    zpath = os.path.join(out, 'data.zip')
    with zipfile.ZipFile(zpath, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for name in sorted(c.files):
            zi = zipfile.ZipInfo(name, date_time=(2026, 10, 10, 0, 0, 0))
            zi.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(zi, c.files[name])
    cases = sorted(c.cases, key=lambda x: x['id'])
    meta = {
        'generator': 're-analysis/tools/emu/gen_jpeg_decode_fixtures.py',
        'libraries': 'resources/lib/armeabi-v7a/{libcozmoEngine,libopencv_core,libopencv_imgproc,libopencv_imgcodecs,libc++_shared}.so under Unicorn (emu_elf.py)',
        'library_sha256': verify_library_hashes(),
        'runs': 'heap/stack poison 0x00 and 0xFF, NEON present and absent',
        'quick': quick,
        'cases': cases,
    }
    json.dump(meta, open(os.path.join(out, 'index.json'), 'w'), indent=0, sort_keys=True)
    if coverage is not None:
        json.dump(coverage, open(os.path.join(out, 'coverage.json'), 'w'), indent=1, sort_keys=True)
    write_readme(out, cases, coverage, quick)


def write_readme(out, cases, coverage, quick):
    from collections import Counter
    kinds = Counter(c['kind'] for c in cases)
    excl = sum(1 for c in cases if c['expected'].get('excluded'))
    thr = sum(1 for c in cases if c['expected'].get('exception'))
    lines = ['# JPEG decode fixtures (M3-001 / M3-018)', '',
             'Generated by `re-analysis/tools/emu/gen_jpeg_decode_fixtures.py` (%s). Every expected value is what the shipped libraries returned when run' % ('quick corpus' if quick else 'full corpus'),
             'under Unicorn (`emu_elf.py`, `emu_jpeg_decode.py`): libcozmoEngine.so, libopencv_core/imgproc/imgcodecs.so, libc++_shared.so. The phone libraries are stand-ins',
             '(bionic libc, libm, pthread; see the module docstrings): heap and stack poison, the parallel_for_ shim, OpenCL off, NEON from /proc/self/auxv.', '',
             '| kind | cases |', '|---|---:|']
    for k, n in sorted(kinds.items()):
        lines.append('| %s | %d |' % (k, n))
    lines += ['', '%d cases carry `excluded` byte ranges (bytes that differ between a 0x00-poisoned and a 0xFF-poisoned run: bytes the shipped code never writes);' % excl,
              '%d cases end in a cv::Exception inside the shipped library. No case in this corpus differs between NEON present and absent (a measurement over this corpus, not a proof for every input).' % thr, '',
              'Files: `index.json` (cases and expected values), `data.zip` (inputs; outputs of the imdecode and resize cases; `idct.json`: vectors for the 32 inverse DCTs),',
              '`tables.json` (static tables read from the .so), `coverage.json` (executed-code fraction per J row).', '']
    if coverage:
        lines += ['## Coverage of the cited ranges (fraction of bytes inside executed translation blocks; literal pools count as unexecuted)', '', '| row | % | ranges |', '|---|---:|---|']
        for k in sorted(coverage, key=lambda x: (x[0] != 'J', x[0], int(re.sub(r'\D', '', x[1:])), x)):
            v = coverage[k]
            lines.append('| %s | %s | %s |' % (k, v['percent'], '; '.join(v['ranges'])))
    open(os.path.join(out, 'README.md'), 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=DEFAULT_OUT)
    ap.add_argument('--quick', action='store_true')
    ap.add_argument('--frames-dir', default=os.path.join(HERE, 'jpeg_inputs', 'fw2457_probe_payloads'), help='mini payloads of the 27 frames in Fixtures/hw_fw2457_probe.log (export_probe_frames.cs.txt)')
    ap.add_argument('--no-coverage', action='store_true')
    args = ap.parse_args()
    rnd = random.Random(20261010)
    runs = Runs()
    c = Corpus(runs)
    pil_cases(c, args.quick)
    print('pil', len(c.cases))
    baseline_cases(c, rnd, args.quick)
    print('baseline', len(c.cases))
    random_entropy_cases(c, rnd, args.quick)
    print('random entropy', len(c.cases))
    mutation_cases(c, rnd, args.quick)
    print('mutations', len(c.cases))
    header_cases(c, rnd)
    corpus_rest(c, rnd)
    lse_cases(c, rnd)
    oom_cases(c, rnd)
    print('headers', len(c.cases))
    resize_cases(c, rnd, args.quick)
    print('resize', len(c.cases))
    engine_cases(c, rnd, args.quick, args.frames_dir)
    print('engine', len(c.cases))
    coverage = None
    if not args.no_coverage:
        import jpeg_coverage
        coverage = jpeg_coverage.measure(c.cases, c.files)
    c._put('idct.json', json.dumps(idct_vectors(runs.p0, rnd, 12 if args.quick else 30)).encode())
    write_outputs(c, args.out, coverage, args.quick)
    print('cases', len(c.cases), 'files', len(c.files), 'neon differences', c.neon_differences, 'oracle aborts', c.oracle_aborts)


if __name__ == '__main__':
    main()
