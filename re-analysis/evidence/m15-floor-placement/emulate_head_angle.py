"""Independent binary32 emulation of TurnTowardsPoseAction::GetAbsoluteHeadAngleToLookAtPose 0x0054B428..0x0054B564 (libcozmoEngine.so 3.4.0-1204).

Literals read from the .so: -49.0f (0x0054B568), 150.0f (0x0054B574), 0.0f (0x0054B570), 300.0f (0x0054B56C), 0.0872665f 0x3DB2B8C2 (0x0054B578), 0.1308997f 0x3E060A92 (0x0054B57C),
0.0698132f 0x3D8EFA35 (0x0054B580); immediates 13.0, 1.0, -10.0. Each `it pl/gt` pair uses the latest vcmpe before its vmrs. atan2f is Python's libm rounded to binary32.
"""
import math, struct
import numpy as np
f = np.float32

def bits(x): return "0x%08X" % struct.unpack('<I', struct.pack('<f', float(x)))[0]
def fb(u): return f(struct.unpack('<f', struct.pack('<I', u))[0])

def head(x, y, z):
    x, y, z = f(x), f(y), f(z)
    L5, L6, L7 = fb(0x3DB2B8C2), fb(0x3E060A92), fb(0x3D8EFA35)
    s16 = z + f(-49.0)
    d = f(math.sqrt(float(x * x + y * y)))
    s8 = f(0.0) - s16
    dd = d + f(13.0)
    u = s8 / f(-10.0)
    t = (f(300.0) - dd) / f(150.0)
    pl = lambda a, b: not (a < b)
    s6 = f(0.0); s0 = f(0.0); s8b = f(0.0); s18 = f(0.0)
    if pl(t, f(1.0)): s6 = L5
    s4 = t * L5
    if t > 0: s0 = s6
    s6b = L6
    s16b = s0
    if pl(u, f(1.0)): s8b = s6b
    if u > 0: s18 = s8b
    s24 = s18
    if t > 0: s16b = s4
    u6 = u * s6b
    if u > 0: s24 = u6
    if pl(t, f(1.0)): s16b = s0
    a = f(math.atan2(float(s16), float(dd)))
    r = a + s16b
    if pl(u, f(1.0)): s24 = s18
    r = s24 + r
    r = r + L7
    return r

for p in ((100, 0, 22), (200, 30, 80), (50, 0, 0), (400, 0, -10), (280, 0, 49)):
    print(p, bits(head(*p)))
