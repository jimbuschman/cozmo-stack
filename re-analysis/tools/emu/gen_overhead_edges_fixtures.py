"""Fixtures for NativeOverheadEdgesOracleTests (M11-017): runs each case through the engine's own OverheadEdgesDetector::Detect
(emu_overhead_edges.py) and writes cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/overhead_edges/overhead_edges_oracle.json.

    python re-analysis/tools/emu/gen_overhead_edges_fixtures.py

Every case runs under heap/stack poison 0x00 and 0xFF (a difference stops the generator: an uninitialised read would make the expected
value a lie) and with NEON present and absent (a difference is recorded in `neon_differences`). Expected values come from the oracle only.
The homographies are made here by a plain pinhole model K*[r0 r1 t] (the shape of the engine's LUT entries, inventory row 6) or written
directly; they are only inputs. The real camera images are the grey JPEGs of the hardware bundles, decoded by the shipped cv::imdecode
(emu_jpeg_decode.py, IMREAD_GRAYSCALE as the stack's GrayImage.FromJpeg); the test checks the stack's decoder gives the same pixels.

Each case records the oracle's coverage probes (hits, hits whose image-to-ground step failed at 0x006AD218, the ClampQuad result read by
GetVisibleGroundQuad at 0x004F7812) so the test can show the corpus reaches the rows that DIFFER.
"""
import base64
import glob
import hashlib
import json
import math
import os
import random
import sys
import zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from emu_overhead_edges import Oracle, library_hashes, f32bits, bitsf32, ROWS, COLS

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, '..', '..', '..'))
OUT = os.path.join(REPO, 'cozmo-stack', 'tests', 'Cozmo.Protocol.Tests', 'Fixtures', 'overhead_edges', 'overhead_edges_oracle.json')

NOMINAL = (290.0, 290.0, 160.0, 120.0)        # CameraCalibration.Nominal
REAL = (294.7, 294.9, 143.2, 117.5)           # the operator's robot (hardware bundles)


def pinhole(cam, pitch, K, yaw=0.0, roll=0.0, mirror=False):
    """H = K*[r0 r1 t] for a camera at `cam` (robot mm) pitched down by `pitch`, yawed left by `yaw`, rolled by `roll` (radians)."""
    fx, fy, cx, cy = K
    # camera axes in robot coordinates before yaw: right = -y, down, forward
    cp, sp = math.cos(pitch), math.sin(pitch)
    fwd = (cp, 0.0, -sp)
    down = (-sp, 0.0, -cp)
    right = (0.0, -1.0, 0.0)
    if mirror:
        right = (0.0, 1.0, 0.0)
    cr, sr = math.cos(roll), math.sin(roll)
    r2 = tuple(cr * right[i] + sr * down[i] for i in range(3))
    d2 = tuple(-sr * right[i] + cr * down[i] for i in range(3))
    cyw, syw = math.cos(yaw), math.sin(yaw)

    def rz(v):
        return (cyw * v[0] - syw * v[1], syw * v[0] + cyw * v[1], v[2])
    R = [rz(r2), rz(d2), rz(fwd)]                  # rows: camera x, y, z in robot coordinates
    t = [-sum(R[i][j] * cam[j] for j in range(3)) for i in range(3)]
    M = [[R[i][0], R[i][1], t[i]] for i in range(3)]
    Kmat = [[fx, 0, cx], [0, fy, cy], [0, 0, 1]]
    return [sum(Kmat[i][k] * M[k][j] for k in range(3)) for i in range(3) for j in range(3)]


def fit_quad(image_corners):
    """The homography taking the ground ROI corners TL (190,75), BL (40,20), TR (190,-75), BR (40,-20) to the given image corners, h22 = 1
    (a plain 8x8 linear solve; only a way to make an input)."""
    g = [(190, 75), (40, 20), (190, -75), (40, -20)]
    A, b = [], []
    for (X, Y), (u, v) in zip(g, image_corners):
        A.append([X, Y, 1, 0, 0, 0, -u * X, -u * Y])
        b.append(u)
        A.append([0, 0, 0, X, Y, 1, -v * X, -v * Y])
        b.append(v)
    n = len(b)
    M = [row[:] + [b[i]] for i, row in enumerate(A)]
    for c in range(n):
        piv = max(range(c, n), key=lambda r: abs(M[r][c]))
        M[c], M[piv] = M[piv], M[c]
        for r in range(n):
            if r != c:
                f = M[r][c] / M[c][c]
                for k in range(c, n + 1):
                    M[r][k] -= f * M[c][k]
    return [M[i][n] / M[i][i] for i in range(n)] + [1.0]


def homographies():
    hs = {
        # the probe's camera: the near corners fall below the image, both side edges cross row 239 (clamp fires, rows 8 and 28)
        'probe_clamp': pinhole((20, 0, 40), 0.5, NOMINAL),
        'real_clamp': pinhole((17.5, 0, 36), 0.45, REAL),
        'real_shallow_clamp': pinhole((10, 0, 38), 0.30, REAL),
        'nominal_steep_clamp': pinhole((20, 0, 40), 0.70, NOMINAL),
        # the whole ROI inside the image: no clamp (rows 8, 28)
        'noclamp_back': pinhole((-10, 0, 40), 0.44, NOMINAL),
        'noclamp_real': pinhole((-20, 0, 45), 0.42, REAL),
        # yawed: one side edge misses the bottom segment, so the clamp does not fire and the near corners stay below the image (row 11 clip)
        'yaw_one_side': pinhole((15, 0, 38), 0.5, NOMINAL, yaw=0.35),
        'yaw_other_side': pinhole((15, 0, 38), 0.5, REAL, yaw=-0.30),
        # rolled: slanted quad edges for the mask rasteriser (row 18)
        'roll_left': pinhole((5, 0, 40), 0.48, NOMINAL, roll=0.25),
        'roll_right': pinhole((0, 0, 42), 0.46, REAL, roll=-0.4),
        'roll_yaw': pinhole((-5, 0, 40), 0.42, NOMINAL, yaw=0.2, roll=0.3),
        # wide and low: the ROI runs past the left and right image edges, so the rect touches the image borders (row 16)
        'wide_low': pinhole((25, 0, 22), 0.25, NOMINAL),
        'wide_low_real': pinhole((30, 0, 18), 0.18, REAL),
        # mirrored: quad[0].x > quad[2].x, so no column passes the clear-report test (row 26)
        'mirrored': pinhole((10, 0, 40), 0.45, NOMINAL, mirror=True),
        # the ROI entirely outside the image: the clipped rect is empty (row 11)
        'looking_up': pinhole((20, 0, 40), -0.6, NOMINAL),
        # yawed far round: the ROI seen side-on
        'yaw_away': pinhole((20, 0, 40), 0.5, NOMINAL, yaw=1.6),
        # the camera inside the ROI: part of the ROI is behind it, so some hits fail the image-to-ground step (row 25)
        'inside_roi_a': pinhole((100, 0, 30), 0.35, NOMINAL),
        'inside_roi_b': pinhole((70, 0, 25), 0.20, NOMINAL),
        'inside_roi_c': pinhole((120, 10, 40), 0.55, REAL, roll=0.2),
        'inside_roi_d': pinhole((60, 0, 20), 0.05, NOMINAL),
        # a negated H: the image quad is the same, every image-to-ground step has q.z < 0 (rows 24, 25)
        'negated': [-v for v in pinhole((20, 0, 40), 0.5, NOMINAL)],
        # near corners swapped in the image (a crossed quad): the projective map sends a line through the ROI to infinity, so part of the
        # filled polygon has q.z <= 0. Negated, the bottom hits fail while the top row projects: the row-25 fallback to the clear report.
        'bowtie_a': fit_quad([(60, 40), (250, 200), (260, 40), (70, 200)]),
        'bowtie_a_neg': [-v for v in fit_quad([(60, 40), (250, 200), (260, 40), (70, 200)])],
        'bowtie_b_neg': [-v for v in fit_quad([(40, 30), (280, 230), (280, 30), (40, 230)])],
        'bowtie_d': fit_quad([(30, 60), (290, 236), (290, 60), (30, 236)]),
        'bowtie_d_neg': [-v for v in fit_quad([(30, 60), (290, 236), (290, 60), (30, 236)])],
    }
    # rank 2: the cofactor inverse sees det 0 and gives the zero matrix (row 20)
    base = pinhole((20, 0, 40), 0.5, NOMINAL)
    hs['rank2'] = base[0:3] + [2.0 * v for v in base[0:3]] + base[6:9]
    return {k: [f32bits(v) for v in h] for k, h in hs.items()}


def images():
    rng = random.Random(0x0E06E)
    ims = {}

    def make(fn):
        return bytes(max(0, min(255, int(fn(x, y)))) for y in range(ROWS) for x in range(COLS))
    ims['flat120'] = make(lambda x, y: 120)
    ims['step_probe'] = make(lambda x, y: 200 if y >= 150 + (x // 40) else 50)
    ims['step_row170'] = make(lambda x, y: 30 if y > 170 else 150)
    ims['step_row200_up'] = make(lambda x, y: 220 if y > 200 else 90)
    ims['diagonal'] = make(lambda x, y: 20 if y > 120 + 0.4 * x else 180)
    ims['diagonal_steep'] = make(lambda x, y: 230 if y > 260 - 1.6 * x else 40)
    ims['bands16'] = make(lambda x, y: 0 if (y // 16) % 2 == 0 else 255)
    ims['checker'] = make(lambda x, y: 255 if ((x // 12) + (y // 9)) % 2 else 0)
    ims['noise'] = bytes(rng.randrange(256) for _ in range(ROWS * COLS))
    ims['noise_soft'] = bytes(100 + rng.randrange(60) for _ in range(ROWS * COLS))
    # threshold: a single step of height d, s = d * 1.0002 just above 50 (d = 50: rounds to 50, no edge) and 51 (edge); mixed outer/inner
    # rows and per-column variation put sums into (50, 50.5] and (50.5, 51)
    ims['thresh_50'] = make(lambda x, y: 150 if y > 160 else 100)
    ims['thresh_51'] = make(lambda x, y: 151 if y > 160 else 100)
    ims['thresh_cols'] = make(lambda x, y: (100 + 49 + (x % 4)) if y > 150 + (x // 64) * 9 else 100)
    ims['thresh_mixed'] = make(lambda x, y: 100 if y < 140 else (100 + 48 + (x % 5) if y == 140 else (100 + 50 + ((x // 5) % 3))))
    ims['thresh_ramp'] = make(lambda x, y: 100 + (50.4 + (x % 7) * 0.17) * min(1.0, max(0.0, (y - 150) / 1.0)) if y >= 150 else 100)
    # strong texture against the image borders (reflect-101 and the parent-image border fill, row 16)
    ims['borders'] = make(lambda x, y: 255 if (x < 6 or x > 313 or y > 232 or y < 4) and ((x + y) % 3 == 0) else (40 if y > 180 else 160))
    ims['border_bottom_rows'] = make(lambda x, y: (250 if (x // 3) % 2 else 0) if y >= 234 else 128)
    return ims


def real_images():
    """The grey camera JPEGs of the hardware bundles (component count 1), decoded by the shipped cv::imdecode."""
    from emu_jpeg_decode import Oracle as Jpeg
    j = Jpeg()
    out = {}
    paths = sorted(glob.glob(os.path.join(REPO, 're-analysis', 'acceptance', 'hardware', '**', '*.jpg'), recursive=True))
    grey = []
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
        if comps != 1:
            continue
        grey.append(p)
    # one frame in three keeps the fixture small while still spanning every bundle
    for p in grey[::3]:
        data = open(p, 'rb').read()
        r = j.imdecode(data, 0)
        if r.get('exception') or r.get('rows') != ROWS or r.get('cols') != COLS:
            raise SystemExit('decode of %s failed: %r' % (p, {k: r.get(k) for k in ('exception', 'rows', 'cols')}))
        rel = os.path.relpath(p, REPO).replace(os.sep, '/')
        pix = bytes(r['data'])                     # read_mat packs the rows (cols * channels each)
        if len(pix) != ROWS * COLS or r.get('channels') != 1:
            raise SystemExit('%s: not a %dx%d grey Mat' % (p, COLS, ROWS))
        out['jpeg:' + rel] = (pix, rel)
    return out


def cases(hs, ims, reals):
    out = []
    zero = f32bits(0.0)

    def add(name, image, h, status=0x100, visible=1, ts=1000, lift=zero):
        out.append({'name': name, 'image': image, 'h': hs[h] if isinstance(h, str) else h, 'h_name': h if isinstance(h, str) else 'custom',
                    'status': status, 'visible': visible, 'ts': ts, 'lift': lift})
    ts = 1000
    for hn in hs:
        for im in ('flat120', 'step_probe', 'diagonal', 'noise', 'checker', 'bands16'):
            ts += 7
            add(f'{hn}__{im}', im, hn, ts=ts)
    for hn in ('probe_clamp', 'real_clamp', 'noclamp_back', 'roll_left', 'wide_low', 'inside_roi_a'):
        for im in ('step_row170', 'step_row200_up', 'diagonal_steep', 'bands16', 'noise_soft', 'borders', 'border_bottom_rows'):
            ts += 7
            add(f'{hn}__{im}', im, hn, ts=ts)
    for hn in ('probe_clamp', 'real_clamp', 'noclamp_back', 'noclamp_real', 'roll_right'):
        for im in ('thresh_50', 'thresh_51', 'thresh_cols', 'thresh_mixed', 'thresh_ramp'):
            ts += 7
            add(f'{hn}__{im}', im, hn, ts=ts)
    # row 4: the ground plane not visible
    add('not_visible__step_probe', 'step_probe', 'probe_clamp', visible=0, ts=4242)
    add('not_visible_and_status_fails', 'step_probe', 'probe_clamp', visible=0, status=0x000, ts=4243)
    # row 5: the status gate (LIFT_IN_POS 0x100, IS_CARRYING_BLOCK 0x2)
    for st in (0x000, 0x002, 0x102, 0x103, 0x001, 0x200, 0x101, 0x300, 0x8100, 0xFFFFFEFD, 0xFFFFFFFF, 0x1100):
        add('status_%08X' % st, 'step_probe', 'probe_clamp', status=st, ts=5000 + (st & 0xFFF))
    # the lift angle input is read (ComputeLiftPose) but with no calibration the gate proceeds whatever it is
    for la in (-0.2, 0.4, 0.9):
        add('lift_%g' % la, 'step_probe', 'real_clamp', lift=f32bits(la), ts=6000)
    # real camera frames
    for i, key in enumerate(sorted(reals)):
        for hn in ('real_clamp', 'real_shallow_clamp', 'noclamp_real', 'roll_right'):
            add(f'{key}__{hn}', key, hn, ts=7000 + i)
    return out


def pack(pix):
    return base64.b64encode(zlib.compress(pix, 9)).decode('ascii')


def main():
    hashes = library_hashes()
    hs = homographies()
    ims = images()
    reals = real_images()
    allims = dict(ims)
    for k, (pix, rel) in reals.items():
        allims[k] = pix
    runs = {(p, n): Oracle(poison=p, neon=n) for p, n in ((0x00, True), (0xFF, True), (0x00, False))}
    base = runs[(0x00, True)]
    out, neon_diff = [], []

    def key(r):
        return (r['ret'], r['exception'], json.dumps(r['frames']))
    for c in cases(hs, ims, reals):
        args = (allims[c['image']], c['h'], c['lift'], c['status'], c['visible'], c['ts'])
        r = base.detect(*args)
        p = runs[(0xFF, True)].detect(*args)
        if key(r) != key(p):
            raise SystemExit('poison changes the result of ' + c['name'])
        n = runs[(0x00, False)].detect(*args)
        if key(r) != key(n):
            neon_diff.append(c['name'])
        if r['exception']:
            raise SystemExit('%s: %s' % (c['name'], r['exception']))
        item = {'name': c['name'], 'image': c['image'], 'h_name': c['h_name'], 'h': ['%08X' % v for v in c['h']],
                'lift': '%08X' % c['lift'], 'status': c['status'], 'visible': c['visible'], 'ts': c['ts'], 'ret': r['ret'],
                'probe': {'hits': r['hits'], 'hit_project_failed': r['hit_project_failed'], 'clamp': r['clamp'],
                          'bad_projected_z_logs': sum(1 for l in r['logs'] if l[1] == 'VisionSystem.SetEdgePositionHelper.BadProjectedZ')},
                'frames': [{'ts': f['ts'], 'flag': f['flag'], 'quad': ['%08X' % v for v in f['quad']],
                            'chains': [{'kind': ch['kind'], 'points': [''.join('%08X' % v for v in pt) for pt in ch['points']]}
                                       for ch in f['chains']]} for f in r['frames']]}
        out.append(item)
        print('%-60s frames %d chains %s hits %d failed %d clamp %s' % (
            c['name'], len(r['frames']), [len(ch['points']) for f in r['frames'] for ch in f['chains']][:6], r['hits'], r['hit_project_failed'], r['clamp']))
    used = sorted({c['image'] for c in out})
    doc = {'generator': 're-analysis/tools/emu/gen_overhead_edges_fixtures.py', 'oracle': 're-analysis/tools/emu/emu_overhead_edges.py',
           'entry': 'OverheadEdgesDetector::Detect 0x006ABE34 (grey path), libcozmoEngine.so',
           'boundary': 'null camera calibration (lift gate always proceeds); Profiler Tic/Toc stubbed; host libm for the lift pose; '
                       'everything else is the shipped engine/OpenCV code',
           'rows': ROWS, 'cols': COLS, 'library_sha256': hashes, 'neon_differences': neon_diff,
           'images': {k: {'zlib_b64': pack(allims[k]), 'sha256': hashlib.sha256(allims[k]).hexdigest(),
                          'jpeg': reals[k][1] if k in reals else None} for k in used},
           'cases': out}
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(doc, f, indent=0)
        f.write('\n')
    print('%d cases, %d images (%d real), %d NEON differences, %s' % (len(out), len(used), sum(1 for k in used if k in reals), len(neon_diff),
                                                                     os.path.normpath(OUT)))


if __name__ == '__main__':
    main()
