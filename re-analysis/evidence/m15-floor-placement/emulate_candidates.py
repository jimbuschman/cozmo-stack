"""Independent binary32 emulation of BehaviorExploreBringCubeToBeacon::FindFreePoseInBeacon's candidate arithmetic.

Written from the disassembly of libcozmoEngine.so 3.4.0-1204 (not from the C# port), to give the unit tests their expected bit patterns:

  Rotation3d(Radians, Point3f)          0x0084A506  half = angle * 0.5f; q = (cosf(half), axis * sinf(half)) as doubles, UnitQuaternion_<double>::Normalize 0x00849DB8
  Rotation3d::operator*(Point3f)        0x0084ACA6 -> UnitQuaternion_<float>::operator* 0x00849C1C (every op below is one vmul/vadd/vsub.f32 in that order)
  candidate (FindFreePoseInBeacon's per-candidate test, 0x005E09E0..0x005E0A66):
        off = (S*(float)i, S*(float)j, 0);  p = rot * off;  t = p + B   (S = 44.0f + 10.0f)
  frame from the robot (0x005E03D0..0x005E0512): v = B - R, v.z = 0, MakeUnitLength (0x0050E0C0), acosf(dot(v, X)), negated when dot(v, -Y) >= 0

cosf/sinf/acosf are taken from Python's libm in double and rounded to binary32 (the correctly rounded result for the arguments used).
Run: python emulate_candidates.py
"""
import math
import struct
import numpy as np

f32 = np.float32


def bits(x):
    return "0x%08X" % struct.unpack('<I', struct.pack('<f', float(x)))[0]


def rescale(v):
    """Radians::rescale 0x0084C87C for |v| < 10 (constants 0xC0490FDB, 0x40490FDB, 0x40C90FDB, 0xC0C90FDB)."""
    pi = f32(struct.unpack('<f', struct.pack('<I', 0x40490FDB))[0])
    two_pi = f32(struct.unpack('<f', struct.pack('<I', 0x40C90FDB))[0])
    while not (v > -pi):
        v = v + two_pi
    while v > pi:
        v = v + (-two_pi)
    return v


def rotation_about_z(angle):
    half = f32(angle) * f32(0.5)
    c = f32(math.cos(float(half)))
    s = f32(math.sin(float(half)))
    w = float(c)
    x = float(s * f32(0.0))
    y = float(s * f32(0.0))
    z = float(s * f32(1.0))
    n2 = w * w
    n2 += x * x
    n2 += y * y
    n2 += z * z
    tol = struct.unpack('<d', struct.pack('<Q', 0x3E56A09E19D672B6))[0]
    if abs(1.0 - n2) < tol:
        sc = 2.0 / (n2 + 1.0)
        return (sc * w, sc * x, sc * y, sc * z)
    ln = math.sqrt(n2)
    return (w / ln, x / ln, y / ln, z / ln)


def rotate(q, p):
    a, b, c, d = [f32(v) for v in q]
    px, py, pz = [f32(v) for v in p]
    aa = a * a; bb = b * b; ab = a * b; ac = a * c; ad = a * d; bd = b * d; bc = b * c; cd = c * d
    aa_bb = aa - bb
    aa_pl_bb = aa + bb
    cc = c * c
    ad2 = ad + ad
    dd = d * d
    bc2 = bc + bc
    cd2 = cd + cd
    ab2 = ab + ab
    ac2 = ac + ac
    bd2 = bd + bd
    m3 = aa_bb + cc
    m8 = aa_pl_bb - cc
    m4 = aa_bb - cc
    s5 = bc2 + ad2
    s0 = bc2 - ad2
    s7 = ab2 + cd2
    s9 = bd2 - ac2
    m3 = m3 - dd
    s2 = m8 - dd
    m4 = m4 + dd
    s6 = cd2 - ab2
    t10 = px * s5
    t8 = py * s7
    t7 = px * s9
    t12 = m3 * py
    t14 = ac2 + bd2
    t0 = py * s0
    t2 = px * s2
    t4 = m4 * pz
    t6 = s6 * pz
    t8 = t7 + t8
    t10 = t12 + t10
    t12 = t14 * pz
    t0 = t2 + t0
    t2 = t4 + t8
    t4 = t6 + t10
    t0 = t12 + t0
    return (t0, t4, t2)


def candidate(q, spacing, i, j, B):
    off = (f32(spacing) * f32(i), f32(spacing) * f32(j), f32(0.0))
    p = rotate(q, off)
    return tuple(f32(p[k] + f32(B[k])) for k in range(3)), p


def frame_from_robot(B, R):
    v = [f32(B[k]) - f32(R[k]) for k in range(3)]
    v[2] = f32(0.0)
    s2 = v[0] * v[0]
    s2 = s2 + v[1] * v[1]
    s2 = s2 + v[2] * v[2]
    if s2 > 0:
        ln = f32(math.sqrt(float(s2)))
        inv = f32(1.0) / ln
        v = [inv * c for c in v]
    else:
        ln = f32(0.0)
    if abs(ln) < f32(struct.unpack('<f', struct.pack('<I', 0x3727C5AC))[0]):
        return rotation_about_z(0.0)
    dotx = v[0] * f32(1.0)
    dotx = dotx + v[1] * f32(0.0)
    dotx = dotx + v[2] * f32(0.0)
    dotny = v[0] * f32(-0.0)
    dotny = dotny + v[1] * f32(-1.0)
    dotny = dotny + v[2] * f32(-0.0)
    a = f32(math.acos(float(dotx)))
    if dotny >= 0:
        a = -a
    return rotation_about_z(float(rescale(a)))


if __name__ == "__main__":
    S = 54.0
    B = (100.0, 50.0, 0.0)
    for name, R in (("beacon +x of robot", (0.0, 50.0, 0.0)), ("beacon +y of robot", (100.0, -50.0, 0.0)), ("beacon -x of robot", (300.0, 50.0, 0.0))):
        q = frame_from_robot(B, R)
        print(name, "quat", q)
        for (i, j) in ((0, 0), (0, -1), (0, 1), (1, 0), (-1, 1), (2, -1)):
            t, p = candidate(q, S, i, j, B)
            print("  i=%d j=%d  t=(%s, %s, %s)  p=(%s, %s, %s)" % (i, j, bits(t[0]), bits(t[1]), bits(t[2]), bits(p[0]), bits(p[1]), bits(p[2])))
