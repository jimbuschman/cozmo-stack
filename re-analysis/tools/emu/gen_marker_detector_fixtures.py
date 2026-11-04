"""Fixtures for NativeMarkerDetectorOracleTests: runs each case through the engine's own ApplyCLAHE + MarkerDetector::Detect
(emu_marker_detector.py) and writes cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/marker_detector/marker_detector_oracle.json.

    python re-analysis/tools/emu/gen_marker_detector_fixtures.py [--render-dir DIR]

Every case runs under heap/stack/scratch poison 0x00 and 0xFF (a difference stops the generator: an uninitialised read would make the
expected value a lie) and with NEON present and absent (a difference is recorded in `neon_differences`). Expected values come from the
oracle only.

Inputs (only inputs; nothing here decides an expected value):
  * real camera frames: every grey camera JPEG of the hardware bundles (component count 1), decoded by the shipped cv::imdecode
    (emu_jpeg_decode.py, IMREAD_GRAYSCALE, as the stack's GrayImage.FromJpeg); the test checks the stack's decoder gives the same pixels;
  * synthetic markers: frames rendered by the stack's own MarkerRenderer (cozmo-conformance `vision --render-corpus DIR`, which writes
    PGMs; the generator runs it unless --render-dir names a directory that already holds them);
  * patterns made here: grids of hollow squares (more than 100 and more than 500 quads), noise, checkers, non-convex and strongly
    foreshortened hollow shapes, each dark (the CLAHE path) and bright (the raw path);
  * homography cases: s16 quads written over the engine's own quad list just before its homography loop (0x00898E38), so
    ComputeHomographyFromQuad, IsConvex, the reprojection check and the validity-6 gate (rows 19-22) see chosen corners.
"""
import base64
import collections
import glob
import hashlib
import json
import math
import os
import random
import subprocess
import sys
import zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from emu_marker_detector import Oracle, library_hashes, f32bits, ROWS, COLS

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, '..', '..', '..'))
OUT = os.path.join(REPO, 'cozmo-stack', 'tests', 'Cozmo.Protocol.Tests', 'Fixtures', 'marker_detector', 'marker_detector_oracle.json')


# ---- inputs
def grey_jpegs():
    paths = sorted(glob.glob(os.path.join(REPO, 're-analysis', 'acceptance', 'hardware', '**', '*.jpg'), recursive=True))
    out = []
    for p in paths:
        data = open(p, 'rb').read()
        i, comps = 2, None
        while i + 9 < len(data) and data[i] == 0xFF:
            mk = data[i + 1]
            ln = (data[i + 2] << 8) | data[i + 3]
            if mk in (0xC0, 0xC1, 0xC2):
                comps = data[i + 9]
                break
            i += 2 + ln
        if comps == 1:
            out.append(p)
    return out


def real_images():
    from emu_jpeg_decode import Oracle as Jpeg
    j = Jpeg()
    out = {}
    for p in grey_jpegs():
        r = j.imdecode(open(p, 'rb').read(), 0)
        if r.get('exception') or r.get('rows') != ROWS or r.get('cols') != COLS or r.get('channels') != 1:
            raise SystemExit('decode of %s failed: %r' % (p, {k: r.get(k) for k in ('exception', 'rows', 'cols', 'channels')}))
        rel = os.path.relpath(p, REPO).replace(os.sep, '/')
        out['jpeg:' + rel] = (bytes(r['data']), rel)
    return out


def read_pgm(path):
    data = open(path, 'rb').read()
    parts, pos = [], 0
    while len(parts) < 4:
        while data[pos:pos + 1].isspace():
            pos += 1
        if data[pos:pos + 1] == b'#':
            while data[pos:pos + 1] not in (b'\n', b''):
                pos += 1
            continue
        start = pos
        while not data[pos:pos + 1].isspace():
            pos += 1
        parts.append(data[start:pos])
    pos += 1
    assert parts[0] == b'P5' and int(parts[1]) == COLS and int(parts[2]) == ROWS and int(parts[3]) == 255, path
    pix = data[pos:pos + ROWS * COLS]
    assert len(pix) == ROWS * COLS
    return pix


def rendered_images(render_dir):
    if render_dir is None:
        render_dir = os.path.join(REPO, '.scratch', 'marker_render_corpus')
        if not glob.glob(os.path.join(render_dir, '*.pgm')):
            proj = os.path.join(REPO, 'cozmo-stack', 'src', 'Cozmo.Conformance')
            subprocess.run(['dotnet', 'run', '--project', proj, '--', 'vision', '--render-corpus', render_dir], check=True)
    out = {}
    for p in sorted(glob.glob(os.path.join(render_dir, '*.pgm'))):
        out['render:' + os.path.splitext(os.path.basename(p))[0]] = read_pgm(p)
    if not out:
        raise SystemExit('no rendered frames in ' + render_dir)
    return out


def raster(fn, ss=4):
    """A grey image from a coverage function fn(x, y) -> value, sampled ss x ss per pixel (pixel centres at integers)."""
    pix = bytearray(ROWS * COLS)
    for y in range(ROWS):
        for x in range(COLS):
            s = 0.0
            for j in range(ss):
                for i in range(ss):
                    s += fn(x - 0.5 + (i + 0.5) / ss, y - 0.5 + (j + 0.5) / ss)
            pix[y * COLS + x] = max(0, min(255, int(round(s / (ss * ss)))))
    return bytes(pix)


def inside(poly, x, y):
    c = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            c = not c
    return c


def hollow_scene(shapes, dark, light, ss=2):
    """shapes: list of (outer polygon, inner polygon); dark inside the ring, light elsewhere; ss x ss samples per pixel."""
    acc = [float(light)] * (ROWS * COLS)
    for outer, inner in shapes:
        xs = [p[0] for p in outer]
        ys = [p[1] for p in outer]
        for y in range(max(0, int(math.floor(min(ys))) - 1), min(ROWS, int(math.ceil(max(ys))) + 2)):
            for x in range(max(0, int(math.floor(min(xs))) - 1), min(COLS, int(math.ceil(max(xs))) + 2)):
                hit = 0
                for j in range(ss):
                    for i in range(ss):
                        px, py = x - 0.5 + (i + 0.5) / ss, y - 0.5 + (j + 0.5) / ss
                        if inside(outer, px, py) and not inside(inner, px, py):
                            hit += 1
                if hit:
                    k = y * COLS + x
                    acc[k] = acc[k] + (dark - light) * hit / (ss * ss)
    return bytes(max(0, min(255, int(round(v)))) for v in acc)


def square(cx, cy, half, angle=0.0):
    c, s = math.cos(angle), math.sin(angle)
    return [(cx + c * dx - s * dy, cy + s * dx + c * dy) for dx, dy in ((-half, -half), (half, -half), (half, half), (-half, half))]


def ring(cx, cy, half, width, angle=0.0):
    return (square(cx, cy, half, angle), square(cx, cy, half - width, angle))


def scale_poly(poly, k):
    cx = sum(p[0] for p in poly) / len(poly)
    cy = sum(p[1] for p in poly) / len(poly)
    return [(cx + k * (x - cx), cy + k * (y - cy)) for x, y in poly]


def pattern_images():
    """Hollow shapes the front end turns into quads: thin dark rings (fill under one half, so InvalidateSolidOrSparse keeps them)."""
    rng = random.Random(0x3A12)
    ims = {}
    for tone, (dark, light) in (('dark', (5, 60)), ('bright', (20, 230))):
        # rows 15/17/18: more than 100 quads (12 x 9 and 16 x 12 grids of rings)
        ims['grid108_' + tone] = hollow_scene([ring(13 + 26.5 * i, 13 + 26.5 * j, 10, 2) for i in range(12) for j in range(9)], dark, light)
        ims['grid192_' + tone] = hollow_scene([ring(10 + 20 * i, 10 + 20 * j, 8, 1.6) for i in range(16) for j in range(12)], dark, light)
        # just under and at the 100-slot list: 10 x 10 and 11 x 9 rings
        ims['grid100_' + tone] = hollow_scene([ring(16 + 29 * i, 13 + 23.5 * j, 10, 2) for i in range(10) for j in range(10)], dark, light)
        ims['grid99_' + tone] = hollow_scene([ring(15 + 29 * i, 14 + 26.5 * j, 10, 2) for i in range(11) for j in range(9)], dark, light)
        # rotated rings of several sizes
        shapes = []
        for k in range(30):
            cx, cy = 30 + (k % 6) * 52, 30 + (k // 6) * 46
            half = 12 + (k % 5) * 2.5
            shapes.append(ring(cx, cy, half, 0.18 * half, angle=rng.uniform(-0.8, 0.8)))
        ims['rotated30_' + tone] = hollow_scene(shapes, dark, light)
        # foreshortened hollow quads (rows 20-22): the far side shorter and shorter
        shapes = []
        for k in range(12):
            cx, cy = 40 + (k % 4) * 80, 40 + (k // 4) * 80
            near, far = 30, 30 / (1.2 + k * 0.5)
            outer = [(cx - near, cy - 32), (cx + far, cy - 32 * far / near - 4), (cx + far, cy + 32 * far / near + 4), (cx - near, cy + 32)]
            shapes.append((outer, scale_poly(outer, 0.82)))
        ims['trapezoids_' + tone] = hollow_scene(shapes, dark, light)
        # non-convex hollow shapes (row 19): a notch pushed into one side
        shapes = []
        for k in range(12):
            cx, cy = 40 + (k % 4) * 80, 40 + (k // 4) * 80
            d = 2 + 2.5 * k
            outer = [(cx - 28, cy - 28), (cx, cy - 28 + d), (cx + 28, cy - 28), (cx + 28, cy + 28), (cx - 28, cy + 28)]
            shapes.append((outer, scale_poly(outer, 0.82)))
        ims['chevrons_' + tone] = hollow_scene(shapes, dark, light)
        # skewed, thin and near-degenerate rings
        shapes = []
        for k in range(16):
            cx, cy = 40 + (k % 4) * 80, 30 + (k // 4) * 60
            sk = 0.2 + 0.45 * k
            outer = [(cx - 24, cy - 20), (cx + 24 + sk * 6, cy - 20 + sk), (cx + 24, cy + 20), (cx - 24 - sk * 4, cy + 20 - sk * 2)]
            shapes.append((outer, scale_poly(outer, 0.8 + 0.01 * (k % 5))))
        ims['skewed_' + tone] = hollow_scene(shapes, dark, light)
        lo, hi = (0, 75) if tone == 'dark' else (0, 255)
        ims['noise_' + tone] = bytes(rng.randrange(lo, hi + 1) for _ in range(ROWS * COLS))
        ims['checker7_' + tone] = bytes(hi if ((x // 7) + (y // 7)) % 2 else lo for y in range(ROWS) for x in range(COLS))
    ims['flat0'] = bytes(ROWS * COLS)
    ims['flat255'] = bytes([255]) * (ROWS * COLS)
    # the dark-test boundary (row 1): every 3rd pixel of every 3rd row against 80 * 107 * 80
    ims['mean79'] = bytes(79 for _ in range(ROWS * COLS))
    ims['mean80'] = bytes(80 for _ in range(ROWS * COLS))
    return ims


# ---- cases
def cases(images):
    out = []
    ts = 1000
    for name in sorted(images):
        ts += 33
        out.append({'name': name + '__engine', 'image': name, 'ts': ts, 'clahe': True})
        if name.startswith('jpeg:') or name.startswith('render:'):
            out.append({'name': name + '__raw', 'image': name, 'ts': ts + 1, 'clahe': False})
    return out


def homography_quads(seen):
    """s16 quads for the homography unit cases (rows 19-22): every quad the end-to-end cases produced, then random convex quads,
    random corner orders (crossed and mirrored quads), strongly foreshortened quads (H[2][0] or H[2][1] above 8), near-degenerate and
    tiny quads, and the s16 extremes."""
    rng = random.Random(0x40C0)
    out = [list(q) for q in seen]
    for _ in range(220):
        cx, cy = rng.uniform(30, 290), rng.uniform(30, 210)
        w, h = rng.uniform(6, 120), rng.uniform(6, 120)
        a = rng.uniform(-3.2, 3.2)
        pts = []
        for u, v in ((0, 0), (0, 1), (1, 0), (1, 1)):
            x, y = (u - 0.5) * w + rng.uniform(-0.15, 0.15) * w, (v - 0.5) * h + rng.uniform(-0.15, 0.15) * h
            pts.append((cx + math.cos(a) * x - math.sin(a) * y, cy + math.sin(a) * x + math.cos(a) * y))
        out.append([int(round(c)) for p_ in pts for c in p_])
    for _ in range(80):
        q = out[rng.randrange(len(out))]
        order = list(range(4))
        rng.shuffle(order)
        out.append([q[2 * i + j] for i in order for j in (0, 1)])
    for k in range(80):
        x0, y0, L = rng.uniform(5, 150), rng.uniform(5, 100), rng.uniform(20, 130)
        W = rng.uniform(4, 160)
        l = rng.choice([0, 1, 2, 3, 4, 6, 8]) + rng.random() * 2
        ym = y0 + L / 2 + rng.uniform(-L / 3, L / 3)
        q = [(x0, y0), (x0, y0 + L), (x0 + W, ym - l / 2), (x0 + W, ym + l / 2)]
        if k % 2:
            q = [(p_[1], p_[0]) for p_ in q]
        if k % 3 == 2:
            q = [(319 - p_[0], p_[1]) for p_ in q]
        out.append([max(-32768, min(32767, int(round(c)))) for p_ in q for c in p_])
    for k in range(60):
        x0, y0 = rng.randrange(0, 300), rng.randrange(0, 220)
        d = rng.choice([0, 1, 2, 3])
        kind = k % 4
        if kind == 0:
            q = [x0, y0, x0 + d, y0 + 40, x0 + 1, y0 + 20, x0 + 30, y0 + 30]
        elif kind == 1:
            q = [x0, y0, x0, y0, x0 + d, y0 + d, x0 + 2 * d, y0 + 3 * d]
        elif kind == 2:
            q = [x0, y0, x0, y0 + 1 + d, x0 + 1 + d, y0, x0 + 1 + d, y0 + 1 + d]
        else:
            q = [x0, y0, x0 + 200, y0 + d, x0 + 100, y0 + 2 * d, x0 + 300, y0 + 3 * d]
        out.append(q)
    out += [[0, 0, 0, 0, 0, 0, 0, 0], [-30000, -30000, -30000, 30000, 30000, -30000, 30000, 30000],
            [32767, 32767, 32767, -32768, -32768, 32767, -32768, -32768], [5, 5, 5, 6, 6, 5, 6, 6]]
    for k in range(60):                                        # the short side along the source's v axis (BL, BR): H[2][1] above 8
        x0, y0, W = rng.uniform(5, 150), rng.uniform(5, 100), rng.uniform(20, 160)
        L = rng.uniform(4, 130)
        l = rng.choice([0, 1, 2, 3, 4, 6, 8]) + rng.random() * 2
        xm = x0 + W / 2 + rng.uniform(-W / 3, W / 3)
        q = [(x0, y0), (xm - l / 2, y0 + L), (x0 + W, y0), (xm + l / 2, y0 + L)]
        if k % 3 == 2:
            q = [(p_[0], 239 - p_[1]) for p_ in q]
        out.append([int(round(c)) for p_ in q for c in p_])
    return out


def probe_cases(images):
    """GetProbeValues unit cases (row 31): an H per case and an image; the H puts probes on exact .5 coordinates, mixes in
    perspective (so 1/w then multiply differs from a divide) and moves probes off the left and right edges into the neighbouring rows
    (no clamp). Every probe stays inside the image buffer (rows 1..238)."""
    rng = random.Random(0x9B0)
    hs = []
    for sx in (64.0, 100.0, 128.0, 150.5, 200.0, 256.0):
        for frac in (0.0, 0.25, 0.5, 0.75):
            tx = 60 + rng.randrange(40) + frac
            ty = 10 + rng.randrange(20) + (0.5 if frac in (0.5, 0.75) else frac)
            hs.append([sx, 0.0, tx, 0.0, sx * 0.75, ty, 0.0, 0.0, 1.0])
    for _ in range(16):
        a = rng.uniform(-1, 1)
        sc = rng.uniform(40, 180)
        hs.append([sc * math.cos(a), -sc * math.sin(a), rng.uniform(80, 160), sc * math.sin(a), sc * math.cos(a), rng.uniform(40, 80),
                   rng.uniform(-0.004, 0.004), rng.uniform(-0.004, 0.004), 1.0])
    hs.append([200.0, 0.0, -20.0, 0.0, 150.0, 30.5, 0.0, 0.0, 1.0])
    hs.append([200.0, 0.0, 140.5, 0.0, 150.0, 30.0, 0.0, 0.0, 1.0])
    hs.append([-150.0, 0.0, 230.5, 0.0, -120.0, 200.25, 0.0, 0.0, 1.0])
    out = []
    names = [k for k in sorted(images) if k.startswith('noise_') or k.startswith('jpeg:')][:6]
    for i, h in enumerate(hs):
        out.append({'name': 'probe_%02d' % i, 'image': names[i % len(names)], 'h': [f32bits(v) for v in h]})
    return out


def pack(pix):
    return base64.b64encode(zlib.compress(pix, 9)).decode('ascii')


def main():
    render_dir = None
    if '--render-dir' in sys.argv:
        render_dir = sys.argv[sys.argv.index('--render-dir') + 1]
    only = None
    if '--only' in sys.argv:
        only = sys.argv[sys.argv.index('--only') + 1]
    hashes = library_hashes()
    reals = real_images()
    images = {k: v[0] for k, v in reals.items()}
    if '--no-render' not in sys.argv:
        images.update(rendered_images(render_dir))
    images.update(pattern_images())
    runs = {(p, n): Oracle(poison=p, neon=n) for p, n in ((0x00, True), (0xFF, True), (0x00, False))}
    base = runs[(0x00, True)]
    out, neon_diff = [], []

    def key(r):
        return json.dumps({k: r[k] for k in ('ret', 'exception', 'dark', 'image_sha256', 'markers', 'quads', 'slots', 'final')})
    # --units-only: keep the end-to-end cases of the existing fixture and recompute only the homography and probe cases
    units_only = '--units-only' in sys.argv
    if units_only:
        prev = json.load(open(OUT, encoding='utf-8'))
        out = prev['cases']
        neon_diff = [n for n in prev['neon_differences'] if not n.startswith('homography') and not n.startswith('probe_')]
    for c in ([] if units_only else cases(images)):
        if only and only not in c['name']:
            continue
        args = (images[c['image']], c['ts'], c['clahe'])
        r = base.detect(*args)
        if r['exception']:
            raise SystemExit('%s: %s' % (c['name'], r['exception']))
        p = runs[(0xFF, True)].detect(*args)
        if key(r) != key(p):
            raise SystemExit('poison changes the result of ' + c['name'])
        n = runs[(0x00, False)].detect(*args)
        if key(r) != key(n):
            neon_diff.append(c['name'])
        hx = lambda v: '%08X' % v
        item = {'name': c['name'], 'image': c['image'], 'ts': c['ts'], 'clahe': c['clahe'], 'ret': r['ret'], 'dark': r['dark'],
                'marker_image_sha256': r['image_sha256'], 'capacity': r['capacity'],
                'markers': [{'type': mk['type'], 'ts': mk['ts'], 'corners': [hx(v) for v in mk['corners']]} for mk in r['markers']],
                'quads': r['quads'],
                'slots': [{'validity': s['validity'], 'corners': [hx(v) for v in s['corners']], 'h': [hx(v) for v in s['h']]} for s in r['slots']],
                'final': [{'validity': s['validity'], 'type': s['type'], 'corners': [hx(v) for v in s['corners']], 'h': [hx(v) for v in s['h']]}
                          for s in r['final']],
                'libm': r['libm'],
                'logs': sorted({l[1] for l in r['logs'] if l[0] != 'sChanneledInfoF'})}
        out.append(item)
        print('%-70s dark %s quads %4d slots %s markers %s libm %s' % (
            c['name'], r['dark'], len(r['quads']), ''.join(str(s['validity']) for s in r['slots'])[:40],
            [mk['type'] for mk in r['markers']], r['libm']), flush=True)
    # homography unit cases (rows 19-22), in batches of up to 100 (the slot list), under both poisons
    hquads = homography_quads(sorted({tuple(q) for c in out for q in c['quads']}))
    hcases = []
    for i in range(0, len(hquads), 100):
        batch = hquads[i:i + 100]
        r = base.homographies(batch)
        if json.dumps(r) != json.dumps(runs[(0xFF, True)].homographies(batch)):
            raise SystemExit('poison changes the homographies of batch %d' % i)
        if json.dumps(r) != json.dumps(runs[(0x00, False)].homographies(batch)):
            neon_diff.append('homography batch %d' % i)
        for q, sl in zip(batch, r):
            hcases.append({'quad': q, 'validity': sl['validity'], 'cause': sl['cause'], 'h': ['%08X' % v for v in sl['h']]})
    print('%d homography cases: %s' % (len(hcases), dict(collections.Counter((h['validity'], h['cause']) for h in hcases))))
    # GetProbeValues unit cases (row 31)
    pcases = []
    for pc in probe_cases(images):
        v = base.probe_values(images[pc['image']], pc['h'])
        if v is None:
            raise SystemExit('GetProbeValues failed for ' + pc['name'])
        if v != runs[(0xFF, True)].probe_values(images[pc['image']], pc['h']):
            raise SystemExit('poison changes the probe values of ' + pc['name'])
        if v != runs[(0x00, False)].probe_values(images[pc['image']], pc['h']):
            neon_diff.append(pc['name'])
        pcases.append({'name': pc['name'], 'image': pc['image'], 'h': ['%08X' % b for b in pc['h']], 'probes': v.hex()})
    print('%d probe cases' % len(pcases))
    used = sorted({c['image'] for c in out} | {p['image'] for p in pcases})
    doc = {'generator': 're-analysis/tools/emu/gen_marker_detector_fixtures.py', 'oracle': 're-analysis/tools/emu/emu_marker_detector.py',
           'entry': 'VisionSystem::ApplyCLAHE 0x006B44E0 + DetectMarkersWithCLAHE pick 0x006B47A8 + MarkerDetector::Detect 0x008753B6, libcozmoEngine.so',
           'boundary': 'host exp (double) and atan2f; Profiler/benchmark/logging stubbed; scratch buffers poisoned per case; everything else is '
                       'the shipped engine/OpenCV code',
           'rows': ROWS, 'cols': COLS, 'library_sha256': hashes, 'neon_differences': neon_diff,
           'images': {k: {'zlib_b64': pack(images[k]), 'sha256': hashlib.sha256(images[k]).hexdigest(),
                          'jpeg': reals[k][1] if k in reals else None} for k in used},
           'cases': out, 'homography_cases': hcases, 'probe_cases': pcases}
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(doc, f, indent=0)
        f.write('\n')
    print('%d cases, %d images (%d real), %d NEON differences, %s' % (len(out), len(used), sum(1 for k in used if k in reals), len(neon_diff),
                                                                     os.path.normpath(OUT)))


if __name__ == '__main__':
    main()
