"""Shared engine-object builders and stand-ins for the vision oracles that run libcozmoEngine.so under Unicorn (emu_elf.py):
emu_overhead_edges.py (OverheadEdgesDetector::Detect) and emu_marker_detector.py (MarkerDetector::Detect).

Everything here is either a way to build an engine object with the engine's own constructor, or an explicit stand-in that a caller
lists in its own docstring:
  * library_hashes(): the shipped libraries, pinned by SHA-256; a different build refuses to run.
  * install_auxv(): /proc/self/auxv is served with or without NEON (OpenCV decides its SIMD paths from it).
  * install_logging(): sWarningF / sErrorF (and, when asked, the channelled info/debug/warning calls, _Anki_Log) are recorded in
    m.log; sDebugBreakOnError returns 0.
  * install_profiler_stubs(): Profiler::Tic/Toc return 0, the Embedded Begin/EndBenchmark return 0 (when asked), OpenCL is off,
    cv::setNumThreads is a no-op.
  * install_parallel_for(): cv::parallel_for_ runs the body once over the whole range (the PLT import, and, after relocation, the
    definition inside libopencv_core for its own internal callers).
  * install_libm_float(): float libm functions taken from the host through Python doubles, rounded to binary32 (bionic is not
    shipped). Which ones a caller installs, and what that means for its proof, is the caller's boundary.
  * install_aeabi_mem(): __aeabi_memcpy*/memmove*/memclr* (and memset* when asked) as Python copies where emu_elf has none.
  * run_inits(): the OpenCV libraries' INIT_ARRAY.
  * new_image() / write_pixels(): Anki::Vision::Image::Image(int rows, int cols) 0x00871CAC on a zeroed 0x80-byte block; pixels are
    written row by row through the cv::Mat at Image+4 (data +0x10, step[0] via +0x2c); the timestamp is Image+0x3C.
  * new_list() / reset_list() / list_values(): a 12-byte std::list sentinel (prev, next, size) and its node values (node + 8).
  * Snapshot: the writable library segments, the static area and the heap below the mark, restored before each case.
"""
import hashlib
import math
import os
import struct
import sys

import lief

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from emu_elf import NORET, LIBDIR, HEAP, STATIC, STATIC_SIZE
from unicorn.arm_const import UC_ARM_REG_PC

# The libraries the oracles load (emu_elf's default set), pinned by SHA-256.
PINNED_SHA256 = {
    'libcozmoEngine.so': '02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1',
    'libopencv_core.so': '8b8c9fcdff9028e5c1b58578d6c20040bebfc2f16c2d2d4bf885f79da56cba8d',
    'libopencv_imgproc.so': '3c4e3ff7e639c61e21c5ef91ea2cc4125bb9bd259ff53f5c0b1eeb9a95054830',
    'libopencv_imgcodecs.so': '4bdf5a45023c7938924f6c0018bf20774ebb5f7c0b2a637c93fe9e61b1f87218',
    'libc++_shared.so': '8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a',
}

AUXV_NEON = struct.pack('<IIII', 16, 0x1000 | 0x80 | 0x40 | 0x8, 0, 0)       # AT_HWCAP: NEON | VFPv3 | VFP | EDSP
AUXV_NONE = struct.pack('<IIII', 16, 0x80 | 0x40 | 0x8, 0, 0)

PARALLEL_FOR = '_ZN2cv13parallel_for_ERKNS_5RangeERKNS_16ParallelLoopBodyEd'
IMAGE_CTOR = '_ZN4Anki6Vision5ImageC1Eii'


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


# ---- shims (install before m.relocate())
def install_auxv(m, neon=True):
    auxv = AUXV_NEON if neon else AUXV_NONE
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


def install_logging(m, channeled=False):
    def logger(kind, idx):
        def h(mm):
            try:
                s = mm.cstr(mm.reg(idx)).decode('latin1')
            except Exception:
                s = '?'
            mm.log.append((kind, s))
            return 0
        return h
    sig = 'PKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z'
    m.overrides['_ZN4Anki4Util9sWarningFE' + sig] = logger('warning', 0)
    m.overrides['_ZN4Anki4Util7sErrorFE' + sig] = logger('error', 0)
    m.overrides['_ZN4Anki4Util18sDebugBreakOnErrorEv'] = lambda mm: 0
    if channeled:
        csig = 'PKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z'
        for nm in ('sChanneledInfoF', 'sChanneledDebugF', 'sChanneledWarningF'):
            m.overrides['_ZN4Anki4Util%d%sE%s' % (len(nm), nm, csig)] = logger(nm, 1)

        def anki_log(mm):
            mm.log.append(('Anki_Log', mm.cstr(mm.reg(1)).decode('latin1'), mm.cstr(mm.reg(2)).decode('latin1')))
            return 0
        m.overrides['_Anki_Log'] = anki_log


def install_profiler_stubs(m, benchmarks=False):
    m.overrides['_ZN4Anki6Vision8Profiler3TicEPKc'] = lambda mm: 0
    m.overrides['_ZN4Anki6Vision8Profiler3TocEPKc'] = lambda mm: 0
    m.overrides['_ZN2cv3ocl9useOpenCLEv'] = lambda mm: 0
    m.overrides['_ZN2cv3ocl10haveOpenCLEv'] = lambda mm: 0
    m.overrides['_ZN2cv14setNumThreadsEi'] = lambda mm: 0
    if benchmarks:
        m.overrides['_ZN4Anki8Embedded14BeginBenchmarkEPKc'] = lambda mm: 0
        m.overrides['_ZN4Anki8Embedded12EndBenchmarkEPKc'] = lambda mm: 0


def _parallel_for(mm):
    rng, body = mm.reg(0), mm.reg(1)
    fn = mm.r32(mm.r32(body) + 8)
    mm.setreg(0, body)
    mm.setreg(1, rng)
    mm.uc.reg_write(UC_ARM_REG_PC, fn)
    return NORET


def install_parallel_for(m):
    """The PLT import (before relocation)."""
    m.overrides[PARALLEL_FOR] = _parallel_for


def hook_core_parallel_for(m):
    """The definition inside libopencv_core, for core's own callers (after relocation)."""
    m.hook_addr(m.libs['libopencv_core.so'].syms[PARALLEL_FOR], _parallel_for)


def install_libm_float(m, fns):
    """fns: {name: python function of one double}; each becomes a binary32 function (argument and result in r0)."""
    def f1(fn):
        def h(mm):
            return f32bits(fn(bitsf32(mm.reg(0))))
        return h
    for nm, fn in fns.items():
        m.overrides[nm] = f1(fn)


def roundf_host(x):
    """C roundf: half away from zero (exact for every binary32 input: x + 0.5 is exact in double)."""
    return float(math.floor(x + 0.5)) if x >= 0 else -float(math.floor(-x + 0.5))


def install_aeabi_mem(m, memset=False):
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
    if memset:
        def mset(mm):                              # __aeabi_memset(dest, n, c): note the argument order
            mm.wr(mm.reg(0), bytes([mm.reg(2) & 0xFF]) * mm.reg(1))
            return mm.reg(0)
        for nm in ('__aeabi_memset4', '__aeabi_memset8', '__aeabi_memset'):
            m.overrides[nm] = mset


def run_inits(m):
    for n in ('libopencv_core.so', 'libopencv_imgproc.so', 'libopencv_imgcodecs.so'):
        lib = m.libs[n]
        tag = lib.elf.get(lief.ELF.DynamicEntry.TAG.INIT_ARRAY)
        if tag is None:
            continue
        for f in tag.array:
            if f in (0, 0xFFFFFFFF):
                continue
            m.call(lib.base + f)


# ---- engine objects
def new_image(m, rows, cols):
    """Anki::Vision::Image::Image(rows, cols) 0x00871CAC. Returns (image, data, step)."""
    img = m.malloc(0x80)
    m.wr(img, bytes(0x80))
    m.call(m.sym(IMAGE_CTOR), img, rows, cols)
    data = m.r32(img + 4 + 0x10)
    step = m.r32(m.r32(img + 4 + 0x2c))
    assert (m.r32(img + 4 + 8), m.r32(img + 4 + 0xc)) == (rows, cols)
    return img, data, step


def write_pixels(m, data, step, pixels, rows, cols):
    for y in range(rows):
        m.wr(data + y * step, bytes(pixels[y * cols:(y + 1) * cols]))


def read_pixels(m, data, step, rows, cols):
    return b''.join(m.rd(data + y * step, cols) for y in range(rows))


def new_list(m):
    lst = m.malloc(16)
    reset_list(m, lst)
    return lst


def reset_list(m, lst):
    m.w32(lst, lst)
    m.w32(lst + 4, lst)
    m.w32(lst + 8, 0)


def list_values(m, lst):
    """The node value addresses (node + 8) from first to last; checks the size word."""
    out = []
    n = m.r32(lst + 4)
    while n != lst:
        out.append(n + 8)
        n = m.r32(n + 4)
    if m.r32(lst + 8) != len(out):
        raise SystemExit('std::list size %d != %d nodes' % (m.r32(lst + 8), len(out)))
    return out


class Snapshot:
    """The whole machine state that a case could change: writable library segments, the static area and the heap below the mark."""

    def __init__(self, m):
        self.m = m
        self.parts = []
        for lib in m.libs.values():
            for s in lib.loads:
                if int(s.flags) & 2:                       # PF_W
                    a = lib.base + s.virtual_address
                    self.parts.append((a, m.rd(a, s.virtual_size)))
        self.parts.append((STATIC, m.rd(STATIC, STATIC_SIZE)))
        self.parts.append((HEAP, m.rd(HEAP, m.heap_base - HEAP)))

    def restore(self):
        m = self.m
        for a, data in self.parts:
            m.wr(a, data)
        m.reset_heap()
