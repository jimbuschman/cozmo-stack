"""Oracle for ObservableObject::ClampPoseToFlat 0x00877330 (M11-006, CLAMP-FLAT-NUMERICS-1): the shipped function run under Unicorn
over libcozmoEngine.so and libc++_shared.so (loader: emu_elf.py). It prints, per case, the input quaternion words, the tolerance word, the
returned bool and the pose's rotation words after the call, for VisionTests.M11_006_* to embed.

    python re-analysis/tools/emu/emu_clamp_flat.py

Inputs are independent of the C# stack: quaternions from Python math (double), written straight into the engine node
(Pose3d +4 -> node; Rotation3d w,x,y,z doubles at node+0, translation f32 x3 at node+0x20; this layout was checked during
CLAMP-FLAT-NUMERICS-1 against the binary, not by this script: GetTransform 0x0084499A/0x008449A0 is ldr r0,[r0,#4], and the Rotation3d
ctor 0x0084A518 stores w=1.0 at node+0). A parented case is a chain root -> mid -> pose: the
root by Pose3d(const string&) 0x0084683C, mid and pose by Pose3d(Radians, axis, translation, parent, name) 0x00846A84; then mid's rotation
is overwritten with the 'parent' quaternion (translation zero) and the pose's with the 'local' one, so the pose's root-relative rotation
is parent * local (GetWithRespectToRoot 0x004DF628 runs inside the clamp). The tolerance is built by the engine's Radians(float) 0x0084C832.

Stand-ins, all explicit: acosf/cosf/sinf/sqrtf are the host's through Python doubles rounded to binary32 (bionic is not shipped:
M13-023(e)); sWarningF/sErrorF are recorded (none is expected); __aeabi_mem* are Python copies; heap and stack poison 0x00 and 0xFF are
both run and must agree.
"""
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'research', '_m5013_dependencies'))
sys.path.insert(0, HERE)
from emu_elf import Machine
from emu_engine_objects import install_logging, install_libm_float, install_aeabi_mem, f32bits

CLAMP = 0x00877331
NAME_CTOR = '_ZN4Anki6Pose3dC1ERKNSt6__ndk112basic_stringIcNS1_11char_traitsIcEENS1_9allocatorIcEEEE'
CHILD_CTOR = ('_ZN4Anki6Pose3dC1ERKNS_7RadiansERKNS_5PointILj3EfEES7_RKS0_RKNSt6__ndk112basic_stringIcNSA_11char_traitsIcEENSA_'
              '9allocatorIcEEEE')
RADIANS_CTOR = '_ZN4Anki7RadiansC1Ef'


def qmul(a, b):
    aw, ax, ay, az = a
    bw, bx, by, bz = b
    return (aw * bw - ax * bx - ay * by - az * bz, aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw)


def q(deg, axis):
    h = math.radians(deg) / 2
    c, s = math.cos(h), math.sin(h)
    return (c, s, 0.0, 0.0) if axis == 0 else (c, 0.0, s, 0.0) if axis == 1 else (c, 0.0, 0.0, s)


def qrad(rad, axis):
    c, s = math.cos(rad / 2), math.sin(rad / 2)
    return (c, s, 0.0, 0.0) if axis == 0 else (c, 0.0, s, 0.0) if axis == 1 else (c, 0.0, 0.0, s)


TOL20, TOL5, TOL40 = 0x3EB2B8C2, 0x3DB2B8C2, 0x3F32B8C2
T5 = struct.unpack('<f', struct.pack('<I', TOL5))[0]
# (label, tolerance word, mid-level parent quaternion under a root, or None for a root pose, local quaternion)
CASES = [
    ('yaw0.3_tiltX15_tol20', TOL20, None, qmul(q(15, 0), qrad(0.3, 2))),
    ('yaw0.3_tiltX25_tol20', TOL20, None, qmul(q(25, 0), qrad(0.3, 2))),
    ('tiltY3_tol5', TOL5, None, q(3, 1)),
    ('tiltY6_tol5', TOL5, None, q(6, 1)),
    ('tiltY_tol5_minus_5e-6', TOL5, None, qrad(T5 - 5e-6, 1)),
    ('tiltY_tol5_minus_3e-5', TOL5, None, qrad(T5 - 3e-5, 1)),
    ('tiltY_1e-6_tol20', TOL20, None, qrad(1e-6, 1)),
    ('upside_down_tiltX170_tol20', TOL20, None, q(170, 0)),
    ('side_Y90_tiltX10_tol20', TOL20, None, qmul(q(10, 0), q(90, 1))),
    ('parentX-40_localX52_tol20', TOL20, q(-40, 0), q(52, 0)),
    ('parentX40_localX7_tol20', TOL20, q(40, 0), q(7, 0)),
    ('parentZ30_localX10Y5_tol20', TOL20, q(30, 2), qmul(q(10, 0), q(5, 1))),
]


def dbits(v):
    return struct.unpack('<Q', struct.pack('<d', v))[0]


def run(poison):
    m = Machine(libs=['libc++_shared.so', 'libcozmoEngine.so'], poison=poison)
    install_logging(m)
    install_libm_float(m, {'acosf': math.acos, 'cosf': math.cos, 'sinf': math.sin, 'sqrtf': math.sqrt})
    install_aeabi_mem(m)
    m.relocate()
    out = []
    for label, tol, parent, local in CASES:
        name = m.malloc(16)
        m.wr(name, bytes(16))
        zero = m.malloc(8)
        m.call(m.sym(RADIANS_CTOR), zero, 0)
        axis = m.malloc(16)
        m.wr(axis, struct.pack('<3f', 0, 0, 1))
        tr = m.malloc(16)
        m.wr(tr, struct.pack('<3f', 100, 0, 22))
        pose = m.malloc(0x40)
        m.wr(pose, bytes(0x40))
        if parent is None:
            m.call(m.sym(NAME_CTOR), pose, name)
        else:
            root = m.malloc(0x40)
            m.wr(root, bytes(0x40))
            m.call(m.sym(NAME_CTOR), root, name)
            mid = m.malloc(0x40)
            m.wr(mid, bytes(0x40))
            m.call(m.sym(CHILD_CTOR), mid, zero, axis, tr, stack=(root, name))
            m.wr(m.r32(mid + 4), struct.pack('<4d', *parent))
            m.wr(m.r32(mid + 4) + 0x20, struct.pack('<3f', 0, 0, 0))
            m.call(m.sym(CHILD_CTOR), pose, zero, axis, tr, stack=(mid, name))
        node = m.r32(pose + 4)
        m.wr(node, struct.pack('<4d', *local))
        m.wr(node + 0x20, struct.pack('<3f', 100, 0, 22))
        rad = m.malloc(8)
        m.call(m.sym(RADIANS_CTOR), rad, tol)
        before = m.log[:]
        ret = m.call(m.libs["libcozmoEngine.so"].base + CLAMP, pose, rad) & 0xFF
        after = struct.unpack('<4d', m.rd(node, 32))
        trans = struct.unpack('<3I', m.rd(node + 0x20, 12))
        out.append((label, tol, parent, local, ret, after, trans, m.log[len(before):]))
    return out


def main():
    a, b = run(0x00), run(0xFF)
    for x, y in zip(a, b):
        if x[4:7] != y[4:7]:
            raise SystemExit('poison-dependent result in ' + x[0])
    for label, tol, parent, local, ret, after, trans, logs in a:
        p = 'null' if parent is None else ','.join('0x%016X' % dbits(v) for v in parent)
        print('%s tol=0x%08X parent=%s local=%s -> ret=%d rot=%s trans=%s logs=%s' % (
            label, tol, p, ','.join('0x%016X' % dbits(v) for v in local), ret,
            ','.join('0x%016X' % dbits(v) for v in after), ','.join('0x%08X' % t for t in trans), logs))


if __name__ == '__main__':
    main()
