"""Shared stand-ins of the bus-FX oracles emu_eq.py and emu_limiter.py (batch 6d of B-M6b-4; research 20261005-B-M6b-4-bus-fx-17.md, correction C45).

The engine's libm is the phone's and does not ship: tanf (PLT 0x4AB038), sinf (0x4A4168), cosf (0x4A415C), sqrtf (0x4A4078), powf (0x4D6778) and expf (0x4D0058) are float32 CORRECTLY ROUNDED here (200-bit mpmath, then integer rounding to 24 bits: independent of any double precision
function), which is what the C# host seams WwiseHostMath.Tanf / Sinf / Cosf / Sqrtf / Powf / Expf (double precision, rounded once) are proved equal to for every argument an oracle produces. memset (PLT 0x4D36DC), memcpy (0x4D37F0) and memmove (0x4D667C) are Python hooks; the plug-in allocator
(vt+8 / vt+0xC of an object in emulated memory) is a bump allocator whose blocks are poisoned with 0xCD, so a read of uninitialised memory shows.
"""
import math
import struct
import sys

from mpmath import mp, mpf, tan as mp_tan, sin as mp_sin, cos as mp_cos

sys.dont_write_bytecode = True
import emu_comp
from emu_comp import fb, ff, round_mpf_to_f32, midpoint_margin, NAN, expf, powf, EXPF_ARGS, POWF_ARGS, XS, gen_input, sha, canon, cn, cs_strings
from emu_common import Emu, BASE, HEAP, RET

mp.prec = 200
M32 = 0xFFFFFFFF

TANF_ARGS, SINF_ARGS, COSF_ARGS = set(), set(), set()
_cache = {}


def _trig(name, fn, xb, record):
    record.add(xb)
    x = ff(xb)
    if math.isnan(x) or math.isinf(x):
        return NAN
    if x == 0:
        return xb if name != 'cos' else 0x3F800000      # tan(+-0) = +-0, sin(+-0) = +-0, cos(0) = 1
    key = (name, xb)
    if key not in _cache:
        v = fn(mpf(x))
        m = midpoint_margin(abs(v))
        if m is not None:
            emu_comp.MARGINS.append((m, xb))
        _cache[key] = round_mpf_to_f32(v)
    return _cache[key]


def tanf(xb):
    return _trig('tan', mp_tan, xb, TANF_ARGS)


def sinf(xb):
    return _trig('sin', mp_sin, xb, SINF_ARGS)


def cosf(xb):
    return _trig('cos', mp_cos, xb, COSF_ARGS)


ALLOC = BASE + 0x00
ALLOC_VT = BASE + 0x40
H_ALLOC = RET + 0x100
H_FREE = RET + 0x104
SCRATCH = BASE + 0x200


class FxEngine:
    """The emulator with the libm / memory hooks and the plug-in allocator installed; `heap` restarts per life."""

    def __init__(self):
        e = self.e = Emu()
        e.std_hooks()
        e.hook(0x4D0058, lambda em: expf(em.reg(0)))
        e.hook(0x4D6778, lambda em: powf(em.reg(0), em.reg(1)))
        e.hook(0x4AB038, lambda em: tanf(em.reg(0)))
        e.hook(0x4A4168, lambda em: sinf(em.reg(0)))
        e.hook(0x4A415C, lambda em: cosf(em.reg(0)))

        def sqrtf_hook(em):                     # only called when vsqrt.f32 produced a NaN: the result is a NaN
            x = ff(em.reg(0))
            return NAN if math.isnan(x) or x < 0 else fb(math.sqrt(x))
        e.hook(0x4A4078, sqrtf_hook)

        def memset(em):
            d, v, n = em.reg(0), em.reg(1) & 0xFF, em.reg(2)
            if n:
                em.uc.mem_write(d, bytes([v]) * n)
            return d
        e.hook(0x4D36DC, memset)

        def memcpy(em):
            d, s, n = em.reg(0), em.reg(1), em.reg(2)
            if n:
                em.uc.mem_write(d, bytes(em.uc.mem_read(s, n)))
            return d
        e.hook(0x4D37F0, memcpy)

        self.calls = 0
        self.fail_at = None
        self.sizes = []
        self.frees = []

        def cb_alloc(em):
            self.calls += 1
            size = em.reg(1)
            self.sizes.append(size)
            if self.fail_at == self.calls:
                return 0
            a = em.alloc(size)
            em.uc.mem_write(a, b'\xcd' * max(size, 1))
            return a
        e.hook(H_ALLOC, cb_alloc)

        def cb_free(em):
            self.frees.append(em.reg(1))
            return 1
        e.hook(H_FREE, cb_free)
        e.w32(ALLOC, ALLOC_VT)
        e.w32(ALLOC_VT + 8, H_ALLOC)
        e.w32(ALLOC_VT + 12, H_FREE)

    def begin(self, fail=None):
        self.e.heap = HEAP
        self.calls = 0
        self.fail_at = fail
        self.sizes = []
        self.frees = []

    def slot(self, obj, k):
        """The code address in slot k of the object's vtable (the vptr points at slot 0)."""
        return self.e.r32(self.e.r32(obj) + 4 * k)

    def put_audio(self, kind, seed, planes, stride):
        e = self.e
        n = planes * stride
        data = gen_input(kind, seed, n)
        d = e.alloc(4 * n + 16)
        e.uc.mem_write(d, struct.pack('<%dI' % n, *data))
        return d, n

    def read_audio(self, d, n):
        return list(struct.unpack('<%dI' % n, bytes(self.e.uc.mem_read(d, 4 * n))))


def proof_header(worst):
    return worst


def worst_margin():
    return min(emu_comp.MARGINS) if emu_comp.MARGINS else None
