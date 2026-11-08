"""Fixtures for NativeSolvePnPOracleTests (M11-054): runs each case through the shipped cv::solvePnP (emu_solvepnp.py) and writes
cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/solvepnp/solvepnp_oracle.json. Inputs and outputs are hex bit patterns.

    python re-analysis/tools/emu/gen_solvepnp_fixtures.py

Every case runs under heap/stack poison 0x00 and 0xFF, and with NEON present and absent. A poison difference stops the generator (an
uninitialised read would make the expected value a lie); a NEON difference is recorded in `neon_differences`. The image points are made by a
plain pinhole/distortion model here, which is only a way to get plausible corners: the expected values come from the shipped solver alone.
"""
import json
import math
import os
import random
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from emu_solvepnp import Oracle, library_hashes, f32bits

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'cozmo-stack', 'tests', 'Cozmo.Protocol.Tests', 'Fixtures', 'solvepnp', 'solvepnp_oracle.json')

# (fx, fy, cx, cy, skew), distortion
REAL = ((294.7, 294.9, 143.2, 117.5, 0.0), [0.0] * 8)                                    # the operator's robot, hardware bundles (dist all zero: body hw <= 6 path)
NOMINAL = ((290.0, 290.0, 160.0, 120.0, 0.0), [0.0] * 8)                                  # CameraCalibration.Nominal
TESTCAL = ((290.5, 291.2, 158.7, 121.3, 0.0), [-0.05, 0.01, 0, 0, 0, 0, 0, 0])            # VisionTests calibration struct
DISTORT = ((290.0, 290.0, 160.0, 120.0, 0.0), [-0.1, 0.02, 0.001, -0.001, 0.0, 0, 0, 0])  # VisionTests.DistortionRoundTrips
RATIONAL = ((294.7, 294.9, 143.2, 117.5, 0.0), [-0.2, 0.05, 0.002, -0.003, 0.01, 0.04, -0.02, 0.005])
FIVE = ((300.0, 299.0, 155.5, 118.25, 0.0), [-0.08, 0.03, 0.001, 0.002, -0.01])
FOUR = ((300.0, 299.0, 155.5, 118.25, 0.0), [-0.08, 0.03, 0.001, 0.002])
EMPTY = ((294.7, 294.9, 143.2, 117.5, 0.0), [])                                           # no distortion vector: one undistort iteration, null dist
SKEW = ((294.7, 294.9, 143.2, 117.5, 3.5), [0.0] * 8)                                     # K has a skew; neither undistort nor projection reads it

# The engine's corner order: canonical (-0.5,0,+0.5), (-0.5,0,-0.5), (+0.5,0,+0.5), (+0.5,0,-0.5) scaled by the marker size (M11-003).
CUBE_MARKER = [(-12.5, 0, 12.5), (-12.5, 0, -12.5), (12.5, 0, 12.5), (12.5, 0, -12.5)]
CHARGER = [(-10, 0, 13.5), (-10, 0, -13.5), (10, 0, 13.5), (10, 0, -13.5)]
FACE = [(-12.5, -22, 12.5), (-12.5, -22, -12.5), (12.5, -22, 12.5), (12.5, -22, -12.5)]   # a cube face, the plane off the object origin
TILTED = [(-12.5, -3, 12.5), (-11.0, 4, -13.0), (13.0, -4, 11.5), (12.0, 3, -12.0)]       # nearly planar and not axis aligned
XY_PLANE = [(-20, -15, 0), (-20, 15, 0), (20, -15, 0), (20, 15, 0)]                       # the basis-identity gate: third basis column on Z
FACE_R = [(22, -12.5, 12.5), (22, -12.5, -12.5), (22, 12.5, 12.5), (22, 12.5, -12.5)]
FACE_T = [(-12.5, 12.5, 22), (-12.5, -12.5, 22), (12.5, 12.5, 22), (12.5, -12.5, 22)]
TWO_FACES = FACE + FACE_R
THREE_FACES = FACE + FACE_R + FACE_T
SIX_POINTS = [(-22, -22, -22), (22, -22, -22), (-22, 22, -22), (22, 22, 22), (-22, -22, 22), (22, -22, 22)]
COLLINEAR = [(-30, 0, 0), (-10, 0, 0), (10, 0, 0), (30, 0, 0)]
NEAR_COLLINEAR = [(-30, 0, 0.01), (-10, 0.02, 0), (10, -0.01, 0.015), (30, 0.01, -0.01)]


def rodrigues(r):
    th = math.sqrt(r[0] ** 2 + r[1] ** 2 + r[2] ** 2)
    if th < 1e-12:
        return [1, 0, 0, 0, 1, 0, 0, 0, 1]
    x, y, z = r[0] / th, r[1] / th, r[2] / th
    c, s = math.cos(th), math.sin(th)
    q = 1 - c
    return [c + q * x * x, q * x * y - s * z, q * x * z + s * y,
            q * x * y + s * z, c + q * y * y, q * y * z - s * x,
            q * x * z - s * y, q * y * z + s * x, c + q * z * z]


def project(points, rvec, tvec, cal, rng=None, noise=0.0):
    (fx, fy, cx, cy, _), d = cal
    d = list(d) + [0.0] * (8 - len(d))
    R = rodrigues(rvec)
    out = []
    for p in points:
        X = R[0] * p[0] + R[1] * p[1] + R[2] * p[2] + tvec[0]
        Y = R[3] * p[0] + R[4] * p[1] + R[5] * p[2] + tvec[1]
        Z = R[6] * p[0] + R[7] * p[1] + R[8] * p[2] + tvec[2]
        x, y = X / Z, Y / Z
        r2 = x * x + y * y
        rad = (1 + d[0] * r2 + d[1] * r2 * r2 + d[4] * r2 ** 3) / (1 + d[5] * r2 + d[6] * r2 * r2 + d[7] * r2 ** 3)
        xd = x * rad + 2 * d[2] * x * y + d[3] * (r2 + 2 * x * x)
        yd = y * rad + d[2] * (r2 + 2 * y * y) + 2 * d[3] * x * y
        u, v = fx * xd + cx, fy * yd + cy
        if rng:
            u += rng.uniform(-noise, noise)
            v += rng.uniform(-noise, noise)
        out.append((u, v))
    return out


def case(name, obj, img, cal):
    (fx, fy, cx, cy, skew), d = cal
    k = (fx, skew, cx, 0.0, fy, cy, 0.0, 0.0, 1.0)
    return {'name': name, 'obj': [f32bits(c) for p in obj for c in p], 'img': [f32bits(c) for p in img for c in p],
            'k': [f32bits(c) for c in k], 'dist': [f32bits(c) for c in d]}


def cases():
    rng = random.Random(0x5EED)
    out = []
    # the marker frame: a marker in front of the camera. r=(pi/2,0,0) makes the marker's +z point down the image, near frontal.
    poses = {
        'frontal_near': ((1.5708, 0.0, 0.0), (0.0, 0.0, 120.0)),
        'frontal_far': ((1.5708, 0.0, 0.0), (5.0, -3.0, 400.0)),
        'offset_near': ((1.5708, 0.1, -0.05), (35.0, 20.0, 100.0)),
        'oblique_yaw': ((1.45, 0.75, 0.2), (-12.0, 6.0, 150.0)),
        'oblique_pitch': ((2.1, 0.1, -0.1), (8.0, 25.0, 180.0)),
        'oblique_far': ((1.2, -0.7, 0.3), (40.0, -15.0, 320.0)),
        'very_near': ((1.62, 0.02, 0.01), (2.0, 1.0, 60.0)),
    }
    for sname, shape in (('cube', CUBE_MARKER), ('charger', CHARGER), ('face', FACE), ('tilted', TILTED)):
        for pname, (r, t) in poses.items():
            for cname, cal in (('real', REAL), ('testcal', TESTCAL)):
                out.append(case(f'{sname}_{pname}_{cname}', shape, project(shape, r, t, cal), cal))
                out.append(case(f'{sname}_{pname}_{cname}_noise', shape, project(shape, r, t, cal, rng, 0.35), cal))
    for cname, cal in (('nominal', NOMINAL), ('distort', DISTORT), ('rational', RATIONAL), ('five', FIVE), ('four', FOUR), ('empty', EMPTY), ('skew', SKEW)):
        for pname in ('frontal_near', 'oblique_yaw', 'oblique_far'):
            r, t = poses[pname]
            out.append(case(f'cube_{pname}_{cname}', CUBE_MARKER, project(CUBE_MARKER, r, t, cal, rng, 0.2), cal))
    # joint sets: nonplanar, so the DLT branch runs
    joint_poses = {'corner': ((2.0, 0.4, -0.3), (5.0, 10.0, 180.0)), 'far': ((2.2, -0.5, 0.4), (-15.0, 5.0, 320.0)), 'near': ((1.9, 0.5, 0.3), (2.0, 4.0, 130.0))}
    for sname, shape in (('joint8', TWO_FACES), ('joint12', THREE_FACES), ('six', SIX_POINTS)):
        for pname, (r, t) in joint_poses.items():
            for cname, cal in (('real', REAL), ('distort', DISTORT), ('empty', EMPTY)):
                out.append(case(f'{sname}_{pname}_{cname}', shape, project(shape, r, t, cal), cal))
                out.append(case(f'{sname}_{pname}_{cname}_noise', shape, project(shape, r, t, cal, rng, 0.3), cal))
    # the basis branches and the near-pi rotation
    for r, t, nm in (((0.2, -0.1, 0.05), (0.0, 0.0, 150.0), 'xy_small'), ((3.05, 0.1, 0.0), (0.0, 0.0, 150.0), 'xy_flip'), ((0.0, 0.0, 3.14159), (1.0, 2.0, 160.0), 'xy_nearpi'),
                     ((0.0, 3.14159, 0.0), (1.0, 2.0, 160.0), 'xy_nearpi_y'), ((1e-7, 0, 0), (0.0, 0.0, 200.0), 'xy_tiny')):
        out.append(case(nm, XY_PLANE, project(XY_PLANE, r, t, REAL), REAL))
    # degenerate sets: planar test sees 0/0 or near zero
    r, t = poses['oblique_yaw']
    out.append(case('near_collinear', NEAR_COLLINEAR, project(NEAR_COLLINEAR, r, t, REAL), REAL))
    out.append(case('near_collinear_noise', NEAR_COLLINEAR, project(NEAR_COLLINEAR, r, t, REAL, rng, 0.3), REAL))
    out.append(case('collinear', COLLINEAR, project(COLLINEAR, r, t, REAL), REAL))
    out.append(case('same_image_point', CUBE_MARKER, [(100.0, 100.0)] * 4, REAL))                     # H05: homography kernel returns empty
    out.append(case('image_on_a_line', CUBE_MARKER, [(100.0, 80.0), (120.0, 80.0), (140.0, 80.0), (160.0, 80.0)], REAL))
    out.append(case('crossed_corners', CUBE_MARKER, [(130.0, 90.0), (190.0, 150.0), (130.0, 150.0), (190.0, 90.0)], REAL))
    out.append(case('same_object_point', [(5.0, 5.0, 5.0)] * 4, [(100.0, 90.0), (120.0, 90.0), (100.0, 110.0), (120.0, 110.0)], REAL))
    out.append(case('huge_depth', CUBE_MARKER, project(CUBE_MARKER, (1.5708, 0, 0), (0.0, 0.0, 30000.0), REAL), REAL))
    # I03: a plane on Z whose in-plane axes are not X and Y, so the V the basis gate replaces is not already the identity
    xy_turned = [(p[0] * 0.8 - p[1] * 0.6 + 3.0, p[0] * 0.6 + p[1] * 0.8 - 2.0, 0.0) for p in XY_PLANE]
    for nm, (r, t) in (('turned_a', ((0.3, -0.2, 0.4), (4.0, -6.0, 170.0))), ('turned_b', ((-0.4, 0.5, 2.0), (-9.0, 3.0, 240.0)))):
        out.append(case('xy_' + nm, xy_turned, project(xy_turned, r, t, REAL), REAL))
    # B01: every object point at the origin makes the rotation columns of J exactly zero, so the damped matrix has zero singular values
    out.append(case('origin_object', [(0.0, 0.0, 0.0)] * 4, [(100.0, 90.0), (120.0, 90.0), (100.0, 110.0), (120.0, 110.0)], REAL))
    out.append(case('origin_object_nonplanar_count', [(0.0, 0.0, 0.0)] * 8, [(100.0 + 5 * i, 90.0 + 3 * i) for i in range(8)], REAL))
    # L06: images that fit no pose, to drive the damping exponent
    for i in range(40):
        pts = THREE_FACES if i % 3 == 0 else (TWO_FACES if i % 3 == 1 else TILTED)
        img = [(rng.uniform(-2000, 2000), rng.uniform(-2000, 2000)) for _ in pts]
        out.append(case('wild_%d' % i, pts, img, RATIONAL if i % 2 else REAL))
    for i in range(10):
        out.append(case('wild_behind_%d' % i, CUBE_MARKER, project(CUBE_MARKER, (1.5708, 0.2 * i, 0.1), (5.0, -3.0, -150.0 - 10 * i), REAL), REAL))
    return out


def main():
    hashes = library_hashes()
    runs = {(p, n): Oracle(poison=p, neon=n) for p, n in ((0x00, True), (0xFF, True), (0x00, False))}
    base = runs[(0x00, True)]
    out, neon_diff = [], []
    for c in cases():
        args = (c['obj'], c['img'], c['k'], c['dist'])
        r = base.solve(*args)
        p = runs[(0xFF, True)].solve(*args)
        if (r['rvec'], r['tvec'], r['exception']) != (p['rvec'], p['tvec'], p['exception']):
            raise SystemExit('poison changes the result of ' + c['name'])
        n = runs[(0x00, False)].solve(*args)
        if (r['rvec'], r['tvec'], r['exception']) != (n['rvec'], n['tvec'], n['exception']):
            neon_diff.append(c['name'])
        item = {'name': c['name']}
        for key in ('obj', 'img', 'k', 'dist'):
            item[key] = ['%08X' % v for v in c[key]]
        if r['exception']:
            item['exception'] = r['exception']
        else:
            item['rvec'] = ['%016X' % v for v in r['rvec']]
            item['tvec'] = ['%016X' % v for v in r['tvec']]
        # The host-libm results this case used (sqrt is IEEE-exact and not recorded), deduplicated: the test replays them so its expected
        # values do not depend on the test host's libm (the M11-099 boundary).
        libm = sorted({(nm, x, y) for nm, x, y in r['libm'] if nm != 'sqrt'})
        if libm:
            item['libm'] = [[nm, '%016X' % x, '%016X' % y] for nm, x, y in libm]
        out.append(item)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    doc = {'generator': 're-analysis/tools/emu/gen_solvepnp_fixtures.py', 'oracle': 're-analysis/tools/emu/emu_solvepnp.py',
           'boundary': 'libm (sin, cos, acos, exp) is the host Python stand-in for bionic, recorded per case under libm for replay; sqrt is IEEE; everything else is the shipped calib3d/imgproc/core code',
           'library_sha256': hashes, 'neon_differences': neon_diff, 'cases': out}
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(doc, f, indent=0)
        f.write('\n')
    print('%d cases, %d exceptions, %d NEON differences, %s' % (len(out), sum('exception' in c for c in out), len(neon_diff), os.path.normpath(OUT)))
    for c in out:
        if 'exception' in c:
            print('  exception:', c['name'], c['exception'])
    if neon_diff:
        print('  neon differs:', neon_diff[:20])


if __name__ == '__main__':
    main()
