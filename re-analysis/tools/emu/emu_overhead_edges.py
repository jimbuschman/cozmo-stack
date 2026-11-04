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

Stand-ins and boundaries, all explicit (everything else is the shipped code; the shared builders and shims are emu_engine_objects.py):
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
import os
import struct
import sys
import math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from emu_elf import Machine, Abort, CxxThrow
from emu_engine_objects import (PINNED_SHA256, library_hashes, f32bits, bitsf32, install_auxv, install_logging, install_profiler_stubs,
                                install_parallel_for, install_libm_float, install_aeabi_mem, run_inits, new_image, write_pixels, Snapshot)
from unicorn import UC_HOOK_CODE
from unicorn.arm_const import UC_ARM_REG_R0, UC_ARM_REG_R8

ROWS, COLS = 240, 320
DETECT = 0x006ABE34
HIT_RESULT = 0x006AD218          # cmp r0,#1 after the image-to-ground call for a hit (row 25)
VISIBLE_CLAMP = 0x004F7812       # cmp.w r8,#1 in GetVisibleGroundQuad: the ClampQuad result (row 28)


class Oracle:
    def __init__(self, poison=0x00, neon=True):
        library_hashes()
        self.m = m = Machine(poison=poison)
        self.neon = neon
        self.probe = {'hit_failed': 0, 'hits': 0, 'clamp': []}
        self._install()
        m.relocate()
        run_inits(m)
        self.B = m.libs['libcozmoEngine.so'].base
        self._build()
        self._hooks()
        self._warm_up()
        m.mark_heap_base()
        self.snap = Snapshot(m)

    # ---- shims (emu_engine_objects; the stand-ins are the module docstring's)
    def _install(self):
        m = self.m
        install_auxv(m, self.neon)
        install_logging(m)
        install_profiler_stubs(m)
        self._img = [0]
        m.overrides['_ZN4Anki6Vision10ImageCache7GetGrayENS1_4SizeEPNS1_7GetTypeE'] = lambda mm: self._img[0]
        install_parallel_for(m)
        install_libm_float(m, {'cosf': math.cos, 'sinf': math.sin, 'sqrtf': math.sqrt, 'acosf': math.acos, 'asinf': math.asin,
                               'tanf': math.tan, 'fabsf': abs})
        m.overrides['atan2f'] = lambda mm: f32bits(math.atan2(bitsf32(mm.reg(0)), bitsf32(mm.reg(1))))
        install_aeabi_mem(m)

    # ---- the fixed objects (built once, before the snapshot)
    def _build(self):
        m = self.m
        img, self.data, self.step = new_image(m, ROWS, COLS)
        self.img = img
        self._img[0] = img
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

    def _restore(self):
        self.snap.restore()

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
        write_pixels(m, self.data, self.step, pixels, ROWS, COLS)
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
