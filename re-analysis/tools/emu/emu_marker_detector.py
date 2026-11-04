"""Oracle for the cube and charger marker detector: the engine's own VisionSystem::ApplyCLAHE 0x006B44E0 (the enum-4 dark test and the
CLAHE front end), DetectMarkersWithCLAHE's image pick (0x006B47A8..0x006B47B4) and MarkerDetector::Detect 0x008753B6, run under Unicorn over
the shipped libcozmoEngine.so, libopencv_core / imgproc / imgcodecs and libc++_shared (loader: emu_elf.py). Started from the extractor's probe
(research/20261103-marker-detector-extraction.md, "Emulator"; .scratch/marker_probe.py).

    from emu_marker_detector import Oracle
    o = Oracle()                                     # poison=0x00, NEON on
    r = o.detect(pixels, timestamp, clahe=True)
    # pixels: bytes, ROWS*COLS (320 x 240, row-major); clahe=True takes the engine's path (ApplyCLAHE, then the dark flag at
    # VisionSystem+0x358 picks the CLAHE output or the original); clahe=False runs Detect on the given image directly.
    # -> dict(ret, exception, dark, image_sha256, markers=[dict(type, ts, corners=[8 x u32 binary32 bits])] in list order,
    #         quads=[[8 x s16]] (the quad list after ComputeQuadrilateralsFromConnectedComponents, 0x00898E38),
    #         slots=[dict(validity, corners, h=[9 x u32])] after the homography loop (0x00898F1C),
    #         final=[dict(validity, type, corners, h)] after the refine/decode loop (0x00899668), logs, libm={name: calls})

The objects (all built by the engine's own constructors, once, before the snapshot):
  * Camera: Camera::Camera() 0x0085DD14 (it loads the nearest-neighbour library itself);
  * MarkerDetector(const Camera&) 0x0087509C, then MarkerDetector::Init(320, 240) 0x008752E0 (Parameters::Initialize 0x008752F8);
  * the input and the CLAHE output: Image::Image(240, 320) 0x00871CAC (emu_engine_objects.new_image); the timestamp is Image+0x3C;
  * the CLAHE object: cv::createCLAHE(40.0, Size(8, 8)) (libopencv_imgproc), stored at VisionSystem+0x34C of a zeroed 0x800-byte
    VisionSystem stand-in with +0x350/+0x354 = 0, as the VisionSystem ctor leaves them; ApplyCLAHE sets the 4x4 tiles and clip 32 itself;
  * the output std::list<ObservedMarker>: a 12-byte sentinel (emu_engine_objects.new_list). A node's value (node + 8) has the s16 type at
    +0, the u32 timestamp at +4 and the four corners as binary32 (x, y) at +8..+0x27.

Stand-ins and boundaries, all explicit (everything else is the shipped code; the shared builders and shims are emu_engine_objects.py):
  * Logging: sWarningF / sErrorF and the channelled info/debug/warning calls are recorded in `logs`; sDebugBreakOnError returns 0.
  * Profiler::Tic/Toc and Embedded Begin/EndBenchmark return 0 (nothing reads their result).
  * cv::parallel_for_ runs the body once over the whole range (the PLT import and the definition inside libopencv_core); OpenCL is off.
  * NEON is decided by the served auxv (/proc/self/auxv); FPSCR is the reset value (round to nearest, no flush to zero).
  * libm (bionic is not shipped), the boundary of this proof:
      - floorf, ceilf, roundf, __signbit, __signbitf: exact functions, computed exactly here (no rounding is involved);
      - sqrt (double) and sqrtf: IEEE-754 square root is correctly rounded, so the host value is the only correct one;
      - exp (double) and atan2f: the host's (Python's math.exp / math.atan2, then rounded to the return type). Bionic's results are
        not reproduced by this oracle; the count of calls per case is recorded in `libm` (and `libm_sites` names the callers).
    expf, logf, cosf, sinf, fabsf are installed too so that a call would be visible in `libm`; the shipped path calls none of them.
  * __aeabi_memcpy*/memmove*/memclr*/memset* where emu_elf has none are Python copies.
  * The three scratch buffers MarkerDetector::Memory::ResetBuffers 0x00874E20 resizes (4,000,000 / 600,000 / 200,000 bytes) keep their
    contents between frames in the engine. Here they are filled with the poison byte before every case, so a case can never read an
    earlier case's scratch, and gen_marker_detector_fixtures.py's two poisons expose any read of scratch the case did not write.
  * Heap and stack are poisoned. The whole machine state (writable library segments, the static area and the heap below the mark) is
    snapshotted after one warm-up Detect and restored before each case.
  * A C++ throw or abort ends the run (`exception`).
"""
import hashlib
import math
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from emu_elf import Machine, Abort, CxxThrow
from emu_engine_objects import (library_hashes, f32bits, bitsf32, install_auxv, install_logging, install_profiler_stubs,
                                install_parallel_for, hook_core_parallel_for, install_aeabi_mem, run_inits, new_image, write_pixels,
                                read_pixels, new_list, reset_list, list_values, roundf_host, Snapshot)
from unicorn import UC_HOOK_CODE
from unicorn.arm_const import UC_ARM_REG_SP, UC_ARM_REG_LR

ROWS, COLS = 240, 320
CAMERA_CTOR = '_ZN4Anki6Vision6CameraC1Ev'
DETECTOR_CTOR = '_ZN4Anki6Vision14MarkerDetectorC1ERKNS0_6CameraE'
DETECTOR_INIT = '_ZN4Anki6Vision14MarkerDetector4InitEii'
DETECT = '_ZN4Anki6Vision14MarkerDetector6DetectERKNS0_5ImageERNSt6__ndk14listINS0_14ObservedMarkerENS5_9allocatorIS7_EEEE'
APPLY_CLAHE = '_ZN4Anki5Cozmo12VisionSystem10ApplyCLAHEERKNS_6Vision5ImageENS1_20MarkerDetectionCLAHEERS3_'
CREATE_CLAHE = '_ZN2cv11createCLAHEEdNS_5Size_IiEE'

QUADS_DONE = 0x00898E38      # DetectFiducialMarkers: the quad count is [sp+0x354], the s16 quads at [sp+0x378] (16 bytes each)
HOMOGRAPHY_DONE = 0x00898F1C  # after the homography loop 0x00898E78..0x00898F1A
DECODE_DONE = 0x00899668     # after the refine/decode loop, before the erase of validity >= 2
MARKERS_OBJ = 0x78           # [sp+0x78]: the VisionMarker FixedLengthList (+0xc count, +0x1c capacity, +0x30 data; slots 0x40 bytes)


class Oracle:
    def __init__(self, poison=0x00, neon=True):
        library_hashes()
        self.poison = poison
        self.m = m = Machine(poison=poison)
        self.neon = neon
        self.libm = {}
        self.libm_sites = {}
        self.probe = {}
        self._install()
        m.relocate()
        hook_core_parallel_for(m)
        run_inits(m)
        self.B = m.libs['libcozmoEngine.so'].base
        self._build()
        self._hooks()
        self._warm_up()
        m.mark_heap_base()
        self.snap = Snapshot(m)

    # ---- shims
    def _install(self):
        m = self.m
        install_auxv(m, self.neon)
        install_logging(m, channeled=True)
        install_profiler_stubs(m, benchmarks=True)
        install_parallel_for(m)
        install_aeabi_mem(m, memset=True)

        def counted(name, fn):
            def h(mm):
                self.libm[name] = self.libm.get(name, 0) + 1
                lr = (mm.uc.reg_read(UC_ARM_REG_LR) & ~1)
                for lib in mm.libs.values():
                    if lib.base <= lr < lib.base + lib.end:
                        key = '%s %s+%#x' % (name, lib.name, lr - lib.base)
                        self.libm_sites[key] = self.libm_sites.get(key, 0) + 1
                return fn(mm)
            return h

        def f1(fn):
            return lambda mm: f32bits(fn(bitsf32(mm.reg(0))))

        def d1(fn):
            def h(mm):
                mm.set_dbl(fn(mm.dbl(mm.reg(0), mm.reg(1))))
            return h
        for nm, fn in (('floorf', lambda x: float(math.floor(x))), ('ceilf', lambda x: float(math.ceil(x))), ('roundf', roundf_host),
                       ('sqrtf', math.sqrt), ('expf', math.exp), ('logf', math.log), ('cosf', math.cos), ('sinf', math.sin),
                       ('fabsf', abs)):
            m.overrides[nm] = counted(nm, f1(fn))
        m.overrides['atan2f'] = counted('atan2f', lambda mm: f32bits(math.atan2(bitsf32(mm.reg(0)), bitsf32(mm.reg(1)))))
        m.overrides['exp'] = counted('exp', d1(math.exp))
        m.overrides['sqrt'] = counted('sqrt', d1(math.sqrt))
        m.overrides['__signbit'] = counted('__signbit', lambda mm: (mm.reg(1) >> 31) & 1)
        m.overrides['__signbitf'] = counted('__signbitf', lambda mm: (mm.reg(0) >> 31) & 1)

    # ---- the fixed objects (built once, before the snapshot)
    def _build(self):
        m = self.m
        cam = m.malloc(0x100)
        m.wr(cam, bytes(0x100))
        m.call(m.sym(CAMERA_CTOR), cam)
        det = m.malloc(0x40)
        m.wr(det, bytes(0x40))
        m.call(m.sym(DETECTOR_CTOR), det, cam, count=2_000_000_000)
        m.call(m.sym(DETECTOR_INIT), det, COLS, ROWS)
        self.cam, self.det = cam, det
        self.img, self.data, self.step = new_image(m, ROWS, COLS)
        self.out, self.out_data, self.out_step = new_image(m, ROWS, COLS)
        sret = m.malloc(16)
        m.wr(sret, bytes(16))
        lo, hi = struct.unpack('<II', struct.pack('<d', 40.0))
        size = m.malloc(16)
        m.wr(size, struct.pack('<ii', 8, 8))
        m.call(m.libs['libopencv_imgproc.so'].syms[CREATE_CLAHE], sret, 0, lo, hi, stack=(size,))
        clahe = m.r32(sret + 4)                    # cv::Ptr<CLAHE> is {owner, stored}: the object is the second word
        if not clahe:
            raise SystemExit('cv::createCLAHE returned no object')
        vs = m.malloc(0x800)
        m.wr(vs, bytes(0x800))
        m.w32(vs + 0x34c, clahe)
        m.w32(vs + 0x350, 0)
        m.w32(vs + 0x354, 0)
        self.vs = vs
        self.lst = new_list(m)
        mem = m.r32(det + 8)
        self.scratch = [(m.r32(mem + o), m.r32(mem + o + 4)) for o in (0, 0xc, 0x18)]

    def _hooks(self):
        m, B, p = self.m, self.B, self.probe

        def sp():
            return m.uc.reg_read(UC_ARM_REG_SP)

        def slots(full):
            lst = m.r32(sp() + MARKERS_OBJ)
            n, base = m.r32(lst + 0xc), m.r32(lst + 0x30)
            out = []
            for i in range(n):
                s = base + 0x40 * i
                hdata, hstride = m.r32(s + 0x2c + 0x10), m.r32(s + 0x2c + 0x8)
                h = [m.r32(hdata + r * hstride + 4 * c) for r in range(3) for c in range(3)]
                e = {'validity': m.r32(s + 0x28), 'corners': list(struct.unpack('<8I', m.rd(s, 32))), 'h': h}
                if full:
                    e['type'] = m.r32(s + 0x20)
                out.append(e)
            return out

        def quads(uc, a, size, ud):
            n = m.r32(sp() + 0x354)
            q = m.r32(sp() + 0x378)
            p['quads'] = [list(struct.unpack('<8h', m.rd(q + 16 * i, 16))) for i in range(max(0, n))]
            lst = m.r32(sp() + MARKERS_OBJ)
            p['capacity'] = m.r32(lst + 0x1c)

        def homographies(uc, a, size, ud):
            p['slots'] = slots(False)

        def decoded(uc, a, size, ud):
            p['final'] = slots(True)
        m.uc.hook_add(UC_HOOK_CODE, quads, begin=B + QUADS_DONE, end=B + QUADS_DONE)
        m.uc.hook_add(UC_HOOK_CODE, homographies, begin=B + HOMOGRAPHY_DONE, end=B + HOMOGRAPHY_DONE)
        m.uc.hook_add(UC_HOOK_CODE, decoded, begin=B + DECODE_DONE, end=B + DECODE_DONE)

    def _warm_up(self):
        """One ApplyCLAHE and Detect over a dark checker, so the function-local and library statics exist before the snapshot."""
        pix = bytes(20 if ((x // 40) + (y // 40)) % 2 else 70 for y in range(ROWS) for x in range(COLS))
        r = self._run(pix, 1, True)
        if r['exception'] or r['ret'] != 0:
            raise SystemExit('warm-up Detect failed: %r' % ({k: r[k] for k in ('exception', 'ret')},))

    # ---- one case
    def detect(self, pixels, timestamp, clahe=True, count=6_000_000_000):
        self.snap.restore()
        return self._run(pixels, timestamp, clahe, count)

    def _run(self, pixels, timestamp, clahe, count=6_000_000_000):
        m = self.m
        assert len(pixels) == ROWS * COLS
        m.poison_stack()
        for a, e in self.scratch:
            if e > a:
                m.wr(a, bytes([self.poison]) * (e - a))
        m.log.clear()
        self.libm.clear()
        self.libm_sites.clear()
        self.probe.clear()
        write_pixels(m, self.data, self.step, pixels, ROWS, COLS)
        m.w32(self.img + 0x3c, timestamp)
        reset_list(m, self.lst)
        out = {'exception': None, 'ret': None, 'dark': None, 'markers': []}
        use = self.img
        try:
            if clahe:
                m.call(m.sym(APPLY_CLAHE), self.vs, self.img, 4, self.out, count=count)
                out['dark'] = m.r8(self.vs + 0x358)
                if out['dark']:
                    use = self.out
            data, step = (self.out_data, self.out_step) if use == self.out else (self.data, self.step)
            if use == self.out:
                data = m.r32(self.out + 4 + 0x10)
                step = m.r32(m.r32(self.out + 4 + 0x2c))
            out['image_sha256'] = hashlib.sha256(read_pixels(m, data, step, ROWS, COLS)).hexdigest()
            out['ret'] = m.call(m.sym(DETECT), self.det, use, self.lst, count=count)
        except CxxThrow as e:
            out['exception'] = 'cxx_throw %s' % e.typename
        except Abort as e:
            out['exception'] = 'abort %s' % e
        if out['exception'] is None:
            for v in list_values(m, self.lst):
                out['markers'].append({'type': struct.unpack('<h', m.rd(v, 2))[0], 'ts': m.r32(v + 4),
                                       'corners': list(struct.unpack('<8I', m.rd(v + 8, 32)))})
        out['quads'] = self.probe.get('quads', [])
        out['capacity'] = self.probe.get('capacity')
        out['slots'] = self.probe.get('slots', [])
        out['final'] = self.probe.get('final', [])
        out['logs'] = list(m.log)
        out['libm'] = dict(self.libm)
        out['libm_sites'] = dict(self.libm_sites)
        return out

    # ---- unit entries (the same machine, the same snapshot)
    def homographies(self, quads, count=2_000_000_000):
        """ComputeHomographyFromQuad 0x008C9036 and the validity-6 gate (0x00898E68..0x00898F12) on chosen s16 quads: a flat image is run
        through Detect, and at 0x00898E38 (after ComputeQuadrilateralsFromConnectedComponents) the engine's own quad list is overwritten
        with `quads` (at most 100, so every write stays inside the 100-slot marker list) and its size set; the run is stopped at 0x00898F1C,
        after the homography loop. Returns [dict(validity, h=[9 x u32])] in quad order."""
        assert 0 < len(quads) <= 100
        m, B = self.m, self.B
        self.snap.restore()
        m.poison_stack()
        for a, e in self.scratch:
            if e > a:
                m.wr(a, bytes([self.poison]) * (e - a))
        write_pixels(m, self.data, self.step, bytes([128]) * (ROWS * COLS), ROWS, COLS)
        reset_list(m, self.lst)
        got = {}

        def inject(uc, a, size, ud):
            sp = uc.reg_read(UC_ARM_REG_SP)
            q = m.r32(sp + 0x378)
            for i, quad in enumerate(quads):
                m.wr(q + 16 * i, struct.pack('<8h', *quad))
            m.w32(sp + 0x354, len(quads))

        def done(uc, a, size, ud):
            sp = uc.reg_read(UC_ARM_REG_SP)
            lst = m.r32(sp + MARKERS_OBJ)
            n, base = m.r32(lst + 0xc), m.r32(lst + 0x30)
            out = []
            for i in range(n):
                s = base + 0x40 * i
                hdata, hstride = m.r32(s + 0x2c + 0x10), m.r32(s + 0x2c + 0x8)
                out.append({'validity': m.r32(s + 0x28), 'h': [m.r32(hdata + r * hstride + 4 * c) for r in range(3) for c in range(3)]})
            got['slots'] = out
            uc.emu_stop()
        cause = {}
        cur = [None]

        def call_site(uc, a, size, ud):                       # 0x00898E98: the ComputeHomographyFromQuad call, r7 = quad index
            from unicorn.arm_const import UC_ARM_REG_R7
            cur[0] = uc.reg_read(UC_ARM_REG_R7)

        def mark(kind):
            def h(uc, a, size, ud):
                cause.setdefault(cur[0], kind)
            return h

        def cholesky(uc, a, size, ud):                        # 0x008C95EC: bne on the Cholesky failure flag just loaded into r0
            from unicorn.arm_const import UC_ARM_REG_R0
            if uc.reg_read(UC_ARM_REG_R0) & 0xFF:
                cause.setdefault(cur[0], 'cholesky')
        hooks = [m.uc.hook_add(UC_HOOK_CODE, inject, begin=B + QUADS_DONE, end=B + QUADS_DONE),
                 m.uc.hook_add(UC_HOOK_CODE, done, begin=B + HOMOGRAPHY_DONE, end=B + HOMOGRAPHY_DONE),
                 m.uc.hook_add(UC_HOOK_CODE, call_site, begin=B + 0x00898E98, end=B + 0x00898E98),
                 m.uc.hook_add(UC_HOOK_CODE, mark('not_convex'), begin=B + 0x008C91D0, end=B + 0x008C91D0),
                 m.uc.hook_add(UC_HOOK_CODE, mark('reprojection'), begin=B + 0x008C9774, end=B + 0x008C9774),
                 m.uc.hook_add(UC_HOOK_CODE, cholesky, begin=B + 0x008C95EC, end=B + 0x008C95EC)]
        try:
            m.call(m.sym(DETECT), self.det, self.img, self.lst, count=count)
        except Abort:
            pass
        finally:
            for h in hooks:
                m.uc.hook_del(h)
        if 'slots' not in got:
            raise SystemExit('the homography loop was not reached')
        for i, sl in enumerate(got['slots']):
            sl['cause'] = cause.get(i)
        return got['slots']

    def probe_values(self, pixels, h_bits):
        """VisionMarker::GetProbeValues(image, H, false, out) 0x0089EF40 called directly: the image is the input Image's pixels wrapped by
        the engine's Array<u8>(rows, cols, void*, length, Flags) 0x008A1120, H an Array<f32> 3x3 header written in the same layout
        (+0 rows, +4 cols, +8 stride 16, +0xc flags, +0x10 data), out a cv::Mat(32, 32, CV_8U). Returns the 1024 probe bytes, or None
        when the call fails. The caller keeps every probe inside the image buffer (outside it the engine reads foreign memory)."""
        m = self.m
        self.snap.restore()
        m.poison_stack()
        write_pixels(m, self.data, self.step, pixels, ROWS, COLS)
        flags = m.malloc(16)
        m.wr(flags, bytes(16))
        m.call(m.sym('_ZN4Anki8Embedded5Flags6BufferC1Ebbb'), flags, 0, 0, 0)
        arr = m.malloc(0x20)
        m.wr(arr, bytes(0x20))
        m.call(m.sym('_ZN4Anki8Embedded5ArrayIhEC2EiiPviNS0_5Flags6BufferE'), arr, ROWS, COLS, self.data,
               stack=(self.step * ROWS, m.r32(flags)))
        if m.r32(arr + 0x10) != self.data or m.r32(arr + 8) != self.step:
            raise SystemExit('Array<u8> wrap failed: %r' % ([hex(m.r32(arr + 4 * i)) for i in range(5)],))
        hdata = m.malloc(64)
        m.wr(hdata, bytes(64))
        for r in range(3):
            m.wr(hdata + 16 * r, struct.pack('<3I', *h_bits[3 * r:3 * r + 3]))
        harr = m.malloc(0x20)
        m.wr(harr, struct.pack('<IIII', 3, 3, 16, m.r32(arr + 0xc)) + struct.pack('<I', hdata) + bytes(12))
        mat = m.malloc(0x40)
        m.wr(mat, bytes(0x40))
        m.call(m.libs['libopencv_core.so'].syms['_ZN2cv3MatC1Eiii'], mat, 32, 32, 0)
        r = m.call(self.B + 0x0089EF40 + 1, arr, harr, 0, mat, count=200_000_000)
        if r != 0:
            return None
        data = m.r32(mat + 0x10)
        return m.rd(data, 1024)

    def marker_image(self, pixels, timestamp=1):
        """The pixels Detect receives on the engine's path (the CLAHE output when dark, else the input), and the dark flag."""
        m = self.m
        self.snap.restore()
        write_pixels(m, self.data, self.step, pixels, ROWS, COLS)
        m.w32(self.img + 0x3c, timestamp)
        m.call(m.sym(APPLY_CLAHE), self.vs, self.img, 4, self.out, count=2_000_000_000)
        dark = m.r8(self.vs + 0x358)
        if not dark:
            return bytes(pixels), 0
        data = m.r32(self.out + 4 + 0x10)
        step = m.r32(m.r32(self.out + 4 + 0x2c))
        return read_pixels(m, data, step, ROWS, COLS), dark


def main():
    import time
    o = Oracle()
    pix = bytes(20 if ((x // 40) + (y // 40)) % 2 else 70 for y in range(ROWS) for x in range(COLS))
    for _ in range(2):
        t = time.time()
        r = o.detect(pix, 12345)
        print('ret', r['ret'], 'dark', r['dark'], 'markers', len(r['markers']), 'quads', len(r['quads']), 'slots',
              [s['validity'] for s in r['slots']], 'final', [(s['validity'], s['type']) for s in r['final']], 'libm', r['libm'],
              r['libm_sites'], '%.1fs' % (time.time() - t))


if __name__ == '__main__':
    main()
