"""Oracle for the overhead-edge detector (M11-017): the engine's own OverheadEdgesDetector::Detect 0x006ABE34 run under Unicorn over the
shipped libcozmoEngine.so, libopencv_core / imgproc / imgcodecs and libc++_shared (loader: emu_elf.py). Started from the extractor's probe
(research/20261103-visit-edge-detector-extraction.md, "Emulator"); grey path only (ImageCache+8 = 0, row 3).

    from emu_overhead_edges import Oracle
    o = Oracle()                                          # poison=0x00, NEON on
    r = o.detect(pixels, h_bits, lift_bits, status, visible, timestamp)
    # pixels: bytes, ROWS*COLS (320 x 240, row-major); h_bits: 9 u32 binary32 patterns (VisionPoseData+0x7C, row-major);
    # lift_bits: u32 binary32 (VisionPoseData+0x3C); status: u32 (VisionPoseData+0x5C); visible: byte (VisionPoseData+0x78)
    # -> dict(ret, frames=[dict(ts, flag, quad=[8 x u32], chains=[dict(kind, points=[[x,y,c0,c1,c2] u32 bits])])],
    #         logs, hit_project_failed=int, hits=int, clamp=[r8 at 0x004F7812 per GetVisibleGroundQuad call])

The call is Detect(detector, ImageCache, VisionPoseData, VisionProcessingResult) with the structures of the extraction's emulator section:
  * detector (0x14 bytes): Camera* +0, VizManager* +4 (null; Detect never reads it), Profiler* +8, f32 50.0 at +0xC, u32 3 at +0x10
    (the VisionSystem construction site 0x006B011E..0x006B012E, row 1);
  * ImageCache: byte +8 = 0 (grey); GetGray is overridden to return one Anki::Vision::Image built by the engine's Image::Image(int,int),
    its pixels written through the cv::Mat at Image+4, its timestamp at Image+0x3C;
  * VisionPoseData: robot Pose3d at +4 (engine ctor Pose3d(const string&)), lift angle +0x3C, status +0x5C, camera Pose3d at +0x6C made by
    the engine ctor Pose3d(Radians, axis, translation, parent = +4, name), visible byte +0x78, H at +0x7C;
  * VisionProcessingResult: the frame std::list sentinel at +0x48/+0x4C, size +0x50.

Stand-ins and boundaries, all explicit (everything else is the shipped code):
  * The Camera has a NULL calibration pointer (Camera+4 = 0). Project3dPoint then warns Camera.Project3dPoint.NoCalibration and fails for
    both lift points, so the lift gate always proceeds (row 9: neither projects). The lift gate is therefore OUTSIDE this proof; the stack's
    gate (OverheadEdgesDetector.LiftIsOutOfTheWay) is not checked by these fixtures.
  * The pose tree is the engine's own; the camera pose is parented on the robot pose as VisionPoseData::Set does (0x006AF330..0x006AF38A),
    so the lift-versus-camera GetWithRespectTo succeeds and Detect's return-1 path (row 9) is not exercised.
  * Profiler::Tic/Toc return 0 (Detect never uses their result); sWarningF/sErrorF are recorded in `logs`; sDebugBreakOnError returns 0.
  * libm: cosf/sinf/sqrtf/atan2f... are the host's through Python (bionic is not shipped); they feed only the lift pose here.
  * __aeabi_memcpy*/memclr* where not already modelled are Python copies.
  * cv::parallel_for_ runs the body once over the whole range and OpenCL is off, as in emu_jpeg_decode.py.
  * NEON is decided by the served auxv (/proc/self/auxv), as in emu_solvepnp.py.
  * FPSCR is the reset value (round to nearest, no flush to zero).
  * Heap and stack are poisoned. The whole machine state (writable library segments, the static area and the heap below the mark) is
    snapshotted after one warm-up Detect and restored before each case, so a case cannot see an earlier case; gen_overhead_edges_fixtures.py
    runs every case with poison 0x00 and 0xFF and refuses a difference.
  * A C++ throw or abort ends the run (`exception`).
"""
import hashlib
import os
import struct
import sys
import math

import lief

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from emu_elf import Machine, Abort, CxxThrow, NORET, LIBDIR, HEAP, STATIC, STATIC_SIZE
from unicorn import UC_HOOK_CODE
from unicorn.arm_const import UC_ARM_REG_PC, UC_ARM_REG_R0, UC_ARM_REG_R8

# The libraries the oracle loads (emu_elf's default set), pinned by SHA-256. A different build refuses to run.
PINNED_SHA256 = {
    'libcozmoEngine.so': '02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1',
    'libopencv_core.so': '8b8c9fcdff9028e5c1b58578d6c20040bebfc2f16c2d2d4bf885f79da56cba8d',
    'libopencv_imgproc.so': '3c4e3ff7e639c61e21c5ef91ea2cc4125bb9bd259ff53f5c0b1eeb9a95054830',
    'libopencv_imgcodecs.so': '4bdf5a45023c7938924f6c0018bf20774ebb5f7c0b2a637c93fe9e61b1f87218',
    'libc++_shared.so': '8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a',
}

AUXV_NEON = struct.pack('<IIII', 16, 0x1000 | 0x80 | 0x40 | 0x8, 0, 0)       # AT_HWCAP: NEON | VFPv3 | VFP | EDSP
AUXV_NONE = struct.pack('<IIII', 16, 0x80 | 0x40 | 0x8, 0, 0)

ROWS, COLS = 240, 320
DETECT = 0x006ABE34
HIT_RESULT = 0x006AD218          # cmp r0,#1 after the image-to-ground call for a hit (row 25)
VISIBLE_CLAMP = 0x004F7812       # cmp.w r8,#1 in GetVisibleGroundQuad: the ClampQuad result (row 28)


def library_hashes():
    out = {}
    for name, want in PINNED_SHA256.items():
        got = hashlib.sha256(open(os.path.join(LIBDIR, name), 'rb').read()).hexdigest()
        if got != want:
            raise SystemExit('%s is not the pinned build: sha256 %s, expected %s' % (name, got, want))
        out[name] = got
    return out


def f32bits(x):
    return struct.unpack('<I', struct.pack('<f', x))[0]


def bitsf32(b):
    return struct.unpack('<f', struct.pack('<I', b & 0xFFFFFFFF))[0]


class Oracle:
    def __init__(self, poison=0x00, neon=True):
        library_hashes()
        self.m = m = Machine(poison=poison)
        self.neon = neon
        self.probe = {'hit_failed': 0, 'hits': 0, 'clamp': []}
        self._install()
        m.relocate()
        self._run_inits()
        self.B = m.libs['libcozmoEngine.so'].base
        self._build()
        self._hooks()
        self._warm_up()
        m.mark_heap_base()
        self._snapshot()

    # ---- shims
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

        def logger(kind):
            def h(mm):
                mm.log.append((kind, mm.cstr(mm.reg(0)).decode('latin1')))
                return 0
            return h
        sig = 'PKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z'
        m.overrides['_ZN4Anki4Util9sWarningFE' + sig] = logger('warning')
        m.overrides['_ZN4Anki4Util7sErrorFE' + sig] = logger('error')
        m.overrides['_ZN4Anki4Util18sDebugBreakOnErrorEv'] = lambda mm: 0
        m.overrides['_ZN4Anki6Vision8Profiler3TicEPKc'] = lambda mm: 0
        m.overrides['_ZN4Anki6Vision8Profiler3TocEPKc'] = lambda mm: 0
        m.overrides['_ZN2cv3ocl9useOpenCLEv'] = lambda mm: 0
        m.overrides['_ZN2cv3ocl10haveOpenCLEv'] = lambda mm: 0
        m.overrides['_ZN2cv14setNumThreadsEi'] = lambda mm: 0
        self._img = [0]
        m.overrides['_ZN4Anki6Vision10ImageCache7GetGrayENS1_4SizeEPNS1_7GetTypeE'] = lambda mm: self._img[0]

        def parallel_for(mm):
            rng, body = mm.reg(0), mm.reg(1)
            fn = mm.r32(mm.r32(body) + 8)
            mm.setreg(0, body)
            mm.setreg(1, rng)
            mm.uc.reg_write(UC_ARM_REG_PC, fn)
            return NORET
        m.overrides['_ZN2cv13parallel_for_ERKNS_5RangeERKNS_16ParallelLoopBodyEd'] = parallel_for

        def f1(fn):
            def h(mm):
                return f32bits(fn(bitsf32(mm.reg(0))))
            return h
        for nm, fn in (('cosf', math.cos), ('sinf', math.sin), ('sqrtf', math.sqrt), ('acosf', math.acos), ('asinf', math.asin),
                       ('tanf', math.tan), ('fabsf', abs)):
            m.overrides[nm] = f1(fn)
        m.overrides['atan2f'] = lambda mm: f32bits(math.atan2(bitsf32(mm.reg(0)), bitsf32(mm.reg(1))))

        def mcpy(mm):
            d, s, n = mm.reg(0), mm.reg(1), mm.reg(2)
            mm.wr(d, mm.rd(s, n))
            return d
        for nm in ('__aeabi_memcpy8', '__aeabi_memcpy4', '__aeabi_memcpy', '__aeabi_memmove', '__aeabi_memmove4', '__aeabi_memmove8'):
            if nm not in m.libc:
                m.overrides[nm] = mcpy

        def mclr(mm):
            d, n = mm.reg(0), mm.reg(1)
            mm.wr(d, bytes(n))
            return d
        for nm in ('__aeabi_memclr4', '__aeabi_memclr', '__aeabi_memclr8'):
            if nm not in m.libc:
                m.overrides[nm] = mclr

    def _run_inits(self):
        m = self.m
        for n in ('libopencv_core.so', 'libopencv_imgproc.so', 'libopencv_imgcodecs.so'):
            lib = m.libs[n]
            tag = lib.elf.get(lief.ELF.DynamicEntry.TAG.INIT_ARRAY)
            if tag is None:
                continue
            for f in tag.array:
                if f in (0, 0xFFFFFFFF):
                    continue
                m.call(lib.base + f)

    # ---- the fixed objects (built once, before the snapshot)
    def _build(self):
        m = self.m
        img = m.malloc(0x80)
        m.wr(img, bytes(0x80))
        m.call(m.sym('_ZN4Anki6Vision5ImageC1Eii'), img, ROWS, COLS)
        self.img = img
        self._img[0] = img
        self.data = m.r32(img + 4 + 0x10)
        self.step = m.r32(m.r32(img + 4 + 0x2c))
        assert (m.r32(img + 4 + 8), m.r32(img + 4 + 0xc)) == (ROWS, COLS)
        self.cache = m.malloc(0x100)
        m.wr(self.cache, bytes(0x100))                     # +8 = 0: grey
        pd = m.malloc(0x200)
        m.wr(pd, bytes(0x200))
        name = m.malloc(16)
        m.wr(name, bytes(16))
        m.call(m.sym('_ZN4Anki6Pose3dC1ERKNSt6__ndk112basic_stringIcNS1_11char_traitsIcEENS1_9allocatorIcEEEE'), pd + 4, name)
        ang = m.malloc(8)
        m.wr(ang, struct.pack('<f', 0.0))
        axis = m.malloc(16)
        m.wr(axis, struct.pack('<3f', 0, 1, 0))
        tr = m.malloc(16)
        m.wr(tr, struct.pack('<3f', 20, 0, 40))
        m.call(m.sym('_ZN4Anki6Pose3dC1ERKNS_7RadiansERKNS_5PointILj3EfEES7_RKS0_RKNSt6__ndk112basic_stringIcNSA_11char_traitsIcEENSA_9allocatorIcEEEE'),
               pd + 0x6c, ang, axis, tr, stack=(pd + 4, name))
        self.pd = pd
        cam = m.malloc(0x40)
        m.wr(cam, bytes(0x40))                             # calibration pointer at +4 is null (see the docstring)
        prof = m.malloc(0x40)
        m.wr(prof, bytes(0x40))
        det = m.malloc(0x20)
        m.wr(det, struct.pack('<IIIfI', cam, 0, prof, 50.0, 3))
        self.det = det
        res = m.malloc(0x100)
        m.wr(res, bytes(0x100))
        self.res = res

    def _hooks(self):
        m, B, p = self.m, self.B, self.probe

        def hit(uc, a, size, ud):
            p['hits'] += 1
            if uc.reg_read(UC_ARM_REG_R0) != 1:
                p['hit_failed'] += 1

        def clamp(uc, a, size, ud):
            p['clamp'].append(uc.reg_read(UC_ARM_REG_R8))
        m.uc.hook_add(UC_HOOK_CODE, hit, begin=B + HIT_RESULT, end=B + HIT_RESULT)
        m.uc.hook_add(UC_HOOK_CODE, clamp, begin=B + VISIBLE_CLAMP, end=B + VISIBLE_CLAMP)

    def _warm_up(self):
        """One Detect over a step image, so the function-local statics (kernel 0xC8E0AC, lift points) and library statics exist before the
        snapshot."""
        th = 0.5
        C = (20.0, 0.0, 40.0)
        R = [(0, -1, 0), (-math.sin(th), 0, -math.cos(th)), (math.cos(th), 0, -math.sin(th))]
        t = [-sum(R[i][j] * C[j] for j in range(3)) for i in range(3)]
        Mx = [[R[i][0], R[i][1], t[i]] for i in range(3)]
        K = [[290, 0, 160], [0, 290, 120], [0, 0, 1]]
        H = [sum(K[i][k] * Mx[k][j] for k in range(3)) for i in range(3) for j in range(3)]
        pix = bytes(200 if y >= 150 + (x // 40) else 50 for y in range(ROWS) for x in range(COLS))
        r = self._run(pix, [f32bits(v) for v in H], f32bits(0.0), 0x100, 1, 1)
        if r['exception'] or r['ret'] != 0 or not r['frames']:
            raise SystemExit('warm-up Detect failed: %r' % (r,))

    def _snapshot(self):
        m = self.m
        self.snap = []
        for lib in m.libs.values():
            for s in lib.loads:
                if int(s.flags) & 2:                       # PF_W
                    a = lib.base + s.virtual_address
                    self.snap.append((a, m.rd(a, s.virtual_size)))
        self.snap.append((STATIC, m.rd(STATIC, STATIC_SIZE)))
        self.snap.append((HEAP, m.rd(HEAP, m.heap_base - HEAP)))

    def _restore(self):
        m = self.m
        for a, data in self.snap:
            m.wr(a, data)
        m.reset_heap()

    # ---- one case
    def detect(self, pixels, h_bits, lift_bits, status, visible, timestamp, count=600_000_000):
        self._restore()
        return self._run(pixels, h_bits, lift_bits, status, visible, timestamp, count)

    def _run(self, pixels, h_bits, lift_bits, status, visible, timestamp, count=600_000_000):
        m = self.m
        assert len(pixels) == ROWS * COLS and len(h_bits) == 9
        m.poison_stack()
        m.log.clear()
        self.probe['hit_failed'] = 0
        self.probe['hits'] = 0
        self.probe['clamp'] = []
        for y in range(ROWS):
            m.wr(self.data + y * self.step, bytes(pixels[y * COLS:(y + 1) * COLS]))
        m.w32(self.img + 0x3c, timestamp)
        pd = self.pd
        m.w32(pd + 0x3c, lift_bits)
        m.w32(pd + 0x5c, status)
        m.w8(pd + 0x78, visible)
        m.wr(pd + 0x7c, struct.pack('<9I', *h_bits))
        res = self.res
        m.w32(res + 0x48, res + 0x48)
        m.w32(res + 0x4c, res + 0x48)
        m.w32(res + 0x50, 0)
        out = {'exception': None, 'ret': None, 'frames': []}
        try:
            out['ret'] = m.call(self.B + DETECT + 1, self.det, self.cache, pd, res, count=count)
        except CxxThrow as e:
            out['exception'] = 'cxx_throw %s' % e.typename
        except Abort as e:
            out['exception'] = 'abort %s' % e
        if out['exception'] is None:
            n = m.r32(res + 0x4c)
            while n != res + 0x48:
                fr = n + 8
                frame = {'ts': m.r32(fr), 'flag': m.r8(fr + 4), 'quad': list(struct.unpack('<8I', m.rd(fr + 8, 32))), 'chains': []}
                cb, ce = m.r32(fr + 0x28), m.r32(fr + 0x2c)
                for c in range(cb, ce, 16):
                    pb, pe = m.r32(c), m.r32(c + 4)
                    pts = [list(struct.unpack('<5I', m.rd(q, 20))) for q in range(pb, pe, 20)]
                    frame['chains'].append({'kind': m.r8(c + 12), 'points': pts})
                out['frames'].append(frame)
                n = m.r32(n + 4)
            if m.r32(res + 0x50) != len(out['frames']):
                raise SystemExit('result list size %d != %d frames' % (m.r32(res + 0x50), len(out['frames'])))
        out['logs'] = list(m.log)
        out['hits'] = self.probe['hits']
        out['hit_project_failed'] = self.probe['hit_failed']
        out['clamp'] = list(self.probe['clamp'])
        return out


def main():
    o = Oracle()
    th = 0.5
    C = (20.0, 0.0, 40.0)
    R = [(0, -1, 0), (-math.sin(th), 0, -math.cos(th)), (math.cos(th), 0, -math.sin(th))]
    t = [-sum(R[i][j] * C[j] for j in range(3)) for i in range(3)]
    Mx = [[R[i][0], R[i][1], t[i]] for i in range(3)]
    K = [[290, 0, 160], [0, 290, 120], [0, 0, 1]]
    H = [sum(K[i][k] * Mx[k][j] for k in range(3)) for i in range(3) for j in range(3)]
    pix = bytes(200 if y >= 150 + (x // 40) else 50 for y in range(ROWS) for x in range(COLS))
    for _ in range(2):
        r = o.detect(pix, [f32bits(v) for v in H], f32bits(0.0), 0x100, 1, 12345)
        f = r['frames'][0]
        print('ret', r['ret'], 'ts', f['ts'], 'flag', f['flag'], 'quad', [round(bitsf32(b), 3) for b in f['quad']],
              'chains', [(c['kind'], len(c['points'])) for c in f['chains']], 'hits', r['hits'], 'failed', r['hit_project_failed'],
              'clamp', r['clamp'], 'logs', len(r['logs']))


if __name__ == '__main__':
    main()
