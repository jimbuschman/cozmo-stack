"""Oracle for marker pose (M11-054, the solver behind Camera::ComputeObjectPoseHelper): the shipped cv::solvePnP run under Unicorn
over the shipped libopencv_calib3d / imgproc / core / flann / features2d and libc++_shared (loader: emu_elf.py).

    from emu_solvepnp import Oracle
    o = Oracle()                                    # poison=0x00, NEON on
    r = o.solve(obj_bits, img_bits, k_bits, dist_bits)
    # obj_bits: flat list of u32 binary32 patterns (x,y,z per point), img_bits (u,v per point), k_bits (9), dist_bits (n)
    # -> dict(rvec=[u64,u64,u64], tvec=[u64,u64,u64], ret=int, exception=None or text, instructions=int)

The call is the engine's own (0x0085D77E..0x0085D8A2, rows E04/E05): object vector<Point3f> wrapper 0x81030015, image vector<Point2f>
wrapper 0x8103000D, K Matx33f wrapper 0xC1020005 size (3,3), distortion Mat wrapper 0x81010005 over a 1 x n CV_32F Mat, rvec/tvec Matx31d
wrappers 0xC2020006 size (1,3) over zeroed doubles, useExtrinsicGuess = 0, flags = 0.

Boundaries, all explicit (the rest of the path is the shipped code):
  * libm (sqrt, sin, cos, acos, exp) is the host's through Python: the phone's bionic is not shipped. This is the same EQUIVALENT_IMPLEMENTATION
    boundary NativeRodrigues.cs names (M13-023(e)). sqrt is IEEE-exact.
  * cv::parallel_for_ runs the body once over the whole range and OpenCL is off, as in emu_jpeg_decode.py.
  * heap and stack are poisoned; gen_solvepnp_fixtures.py runs each case with 0x00 and 0xFF and refuses a difference.
  * NEON is decided by the served auxv; the generator runs every case with it present and absent and records any difference.
  * FPSCR is the reset value (round to nearest, no flush to zero).
  * a C++ throw ends the run (the unwinder is not shipped); `exception` carries it.
"""
import os
import struct
import sys

import lief

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emu_elf
from emu_elf import Machine, Abort, CxxThrow, NORET, LIBDIR
from unicorn.arm_const import UC_ARM_REG_PC

emu_elf.BASES.update({
    'libopencv_flann.so': 0x28000000,
    'libopencv_features2d.so': 0x2C000000,
    'libopencv_calib3d.so': 0x30000000,
})
LIBS = ['libc++_shared.so', 'libopencv_core.so', 'libopencv_imgproc.so', 'libopencv_flann.so', 'libopencv_features2d.so', 'libopencv_calib3d.so']
for _n in LIBS:
    if _n not in emu_elf.ORDER:
        emu_elf.ORDER.append(_n)

PINNED_SHA256 = {
    'libopencv_core.so': '8b8c9fcdff9028e5c1b58578d6c20040bebfc2f16c2d2d4bf885f79da56cba8d',
    'libopencv_imgproc.so': '3c4e3ff7e639c61e21c5ef91ea2cc4125bb9bd259ff53f5c0b1eeb9a95054830',
    'libc++_shared.so': '8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a',
}

AUXV_NEON = struct.pack('<IIII', 16, 0x1000 | 0x80 | 0x40 | 0x8, 0, 0)       # AT_HWCAP: NEON | VFPv3 | VFP | EDSP
AUXV_NONE = struct.pack('<IIII', 16, 0x80 | 0x40 | 0x8, 0, 0)

SOLVEPNP = '_ZN2cv8solvePnPERKNS_11_InputArrayES2_S2_S2_RKNS_12_OutputArrayES5_bi'


def library_hashes():
    import hashlib
    out = {}
    for name in LIBS:
        out[name] = hashlib.sha256(open(os.path.join(LIBDIR, name), 'rb').read()).hexdigest()
    for name, want in PINNED_SHA256.items():
        if out[name] != want:
            raise SystemExit('%s is not the pinned build: sha256 %s, expected %s' % (name, out[name], want))
    return out


def f32bits(x):
    return struct.unpack('<I', struct.pack('<f', x))[0]


class Oracle:
    def __init__(self, poison=0x00, neon=True):
        library_hashes()
        self.m = m = Machine(libs=LIBS, poison=poison)
        self.neon = neon
        self._ready = False
        self._install()
        m.relocate()
        self._run_inits()
        self._warm_up()
        m.mark_heap_base()
        self._ready = True

    def _install(self):
        m = self.m
        auxv = AUXV_NEON if self.neon else AUXV_NONE
        fds = {}

        def open_(mm):
            path = mm.cstr(mm.reg(0)).decode('latin1')
            if path == '/proc/self/auxv':
                fds[100] = [auxv, 0]
                return 100
            return 0xFFFFFFFF

        def read_(mm):
            fd, buf, n = mm.reg(0), mm.reg(1), mm.reg(2)
            if fd not in fds:
                return 0xFFFFFFFF
            data, pos = fds[fd]
            chunk = data[pos:pos + n]
            fds[fd][1] += len(chunk)
            mm.wr(buf, chunk)
            return len(chunk)
        m.libc['open'] = open_
        m.libc['read'] = read_
        m.libc['close'] = lambda mm: 0

        def parallel_for(mm):
            rng, body = mm.reg(0), mm.reg(1)
            fn = mm.r32(mm.r32(body) + 8)
            mm.setreg(0, body)
            mm.setreg(1, rng)
            mm.uc.reg_write(UC_ARM_REG_PC, fn)
            return NORET
        m.overrides['_ZN2cv13parallel_for_ERKNS_5RangeERKNS_16ParallelLoopBodyEd'] = parallel_for
        m.overrides['_ZN2cv3ocl9useOpenCLEv'] = lambda mm: 0
        m.overrides['_ZN2cv3ocl10haveOpenCLEv'] = lambda mm: 0
        m.overrides['_ZN2cv14setNumThreadsEi'] = lambda mm: 0

    def _run_inits(self):
        m = self.m
        for n in LIBS:
            if n == 'libc++_shared.so':
                continue
            lib = m.libs[n]
            tag = lib.elf.get(lief.ELF.DynamicEntry.TAG.INIT_ARRAY)
            if tag is None:
                continue
            for f in tag.array:
                if f in (0, 0xFFFFFFFF):
                    continue
                m.call(lib.base + f)

    def _warm_up(self):
        """One solve of each branch, so the lazily created library statics exist before the heap snapshot."""
        k = [f32bits(v) for v in (290, 0, 160, 0, 290, 120, 0, 0, 1)]
        dist = [0] * 8
        planar = [(-12.5, 0, 12.5), (-12.5, 0, -12.5), (12.5, 0, 12.5), (12.5, 0, -12.5)]
        img = [(130, 90), (130, 150), (190, 90), (190, 150)]
        self.solve([f32bits(c) for p in planar for c in p], [f32bits(c) for p in img for c in p], k, dist)
        obj = [(-22, -22, -22), (22, -22, -22), (-22, 22, -22), (22, 22, -22), (-22, -22, 22), (22, -22, 22), (-22, 22, 22), (22, 22, 22)]
        im2 = [(100 + 50 * ((i >> 0) & 1) + 5 * ((i >> 2) & 1), 80 + 50 * ((i >> 1) & 1) + 4 * ((i >> 2) & 1)) for i in range(8)]
        self.solve([f32bits(c) for p in obj for c in p], [f32bits(c) for p in im2 for c in p], k, dist)

    def _vector(self, bits):
        m = self.m
        buf = m.malloc(max(4 * len(bits), 4))
        m.wr(buf, struct.pack('<%dI' % len(bits), *bits))
        vec = m.malloc(16)
        end = buf + 4 * len(bits)
        m.wr(vec, struct.pack('<III', buf, end, end))
        return vec

    def solve(self, obj_bits, img_bits, k_bits, dist_bits, count=400_000_000):
        m = self.m
        if self._ready:
            m.reset_heap()
        m.poison_stack()
        m.log.clear()
        m.libm_log = []
        res = {'exception': None, 'ret': None, 'rvec': None, 'tvec': None}
        try:
            vobj = self._vector(obj_bits)
            vimg = self._vector(img_bits)
            kbuf = m.malloc(36)
            m.wr(kbuf, struct.pack('<9I', *k_bits))
            dbuf = m.malloc(max(4 * len(dist_bits), 4))
            if dist_bits:
                m.wr(dbuf, struct.pack('<%dI' % len(dist_bits), *dist_bits))
            dmat = m.malloc(0x40)
            m.wr(dmat, bytes(0x40))
            # Mat::Mat(int rows, int cols, int type, void* data, size_t step = AUTO_STEP): the 1 x n CV_32F view of 0x0085D78A..0x0085D848
            m.call(m.sym('_ZN2cv3MatC1EiiiPvj'), dmat, 1, len(dist_bits), 5, stack=(dbuf if dist_bits else 0, 0))
            rbuf, tbuf = m.malloc(24), m.malloc(24)
            m.wr(rbuf, bytes(24))
            m.wr(tbuf, bytes(24))

            def wrap(flags, obj, w=0, h=0):
                a = m.malloc(16)
                m.wr(a, struct.pack('<IIii', flags, obj, w, h))
                return a
            a_obj = wrap(0x81030015, vobj)
            a_img = wrap(0x8103000D, vimg)
            a_k = wrap(0xC1020005, kbuf, 3, 3)
            a_d = wrap(0x81010005, dmat)
            a_r = wrap(0xC2020006, rbuf, 1, 3)
            a_t = wrap(0xC2020006, tbuf, 1, 3)
            res['ret'] = m.call(m.sym(SOLVEPNP), a_obj, a_img, a_k, a_d, stack=(a_r, a_t, 0, 0), count=count)
            res['rvec'] = list(struct.unpack('<3Q', m.rd(rbuf, 24)))
            res['tvec'] = list(struct.unpack('<3Q', m.rd(tbuf, 24)))
        except CxxThrow as e:
            res['exception'] = 'cxx_throw %s' % e.typename
        except Abort as e:
            res['exception'] = 'abort %s' % e
        res['logs'] = list(m.log)
        res['libm'] = list(m.libm_log)
        m.libm_log = None
        return res


def main():
    o = Oracle()
    k = [f32bits(v) for v in (290, 0, 160, 0, 290, 120, 0, 0, 1)]
    planar = [(-12.5, 0, 12.5), (-12.5, 0, -12.5), (12.5, 0, 12.5), (12.5, 0, -12.5)]
    img = [(130, 90), (131, 150), (190, 90), (189, 150)]
    r = o.solve([f32bits(c) for p in planar for c in p], [f32bits(c) for p in img for c in p], k, [0] * 8)
    print(r)
    for name in ('rvec', 'tvec'):
        if r[name]:
            print(name, [struct.unpack('<d', struct.pack('<Q', v))[0] for v in r[name]])


if __name__ == '__main__':
    main()
