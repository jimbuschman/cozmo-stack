"""A small multi-library ARM (Thumb) loader over Unicorn for the packaged .so files (B-M3M4 JPEG decode oracle).

Maps the shipped libraries side by side (libc++_shared, libopencv_core/imgproc/imgcodecs, libcozmoEngine), applies their
R_ARM_RELATIVE / ABS32 / GLOB_DAT / JUMP_SLOT relocations, and binds each import to the first loaded library that defines
it. Everything else (bionic libc, libm, pthread, libz, liblog) is a Python stand-in registered by name in `Machine.libc`;
an import with no definition and no stand-in becomes a stub that aborts the run with its name, so nothing is silently faked.

Fixture boundaries, all explicit:
  * the heap allocator (malloc/free/realloc): first-fit with 16-byte alignment; fresh memory is filled with `poison`
    (default 0x00) so a run with two different poison values shows which output bytes depend on uninitialised memory;
  * the stack is filled with `poison` before each call;
  * setjmp/longjmp, pthread_once/mutex/key, libm, string.h and printf are Python (the phone's bionic is not shipped);
  * cv::parallel_for_ is replaced by one call of the loop body over the whole range (partitioning does not change pixels,
    J44), and cv::ocl::useOpenCL by 0 (no OpenCL on the decode path).
"""
import math
import os
import struct
import lief
from unicorn import UC_HOOK_BLOCK, Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, UC_HOOK_MEM_READ_UNMAPPED, UC_HOOK_MEM_WRITE_UNMAPPED, UC_HOOK_MEM_FETCH_UNMAPPED, UcError
from unicorn.arm_const import *

LIBDIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'resources', 'lib', 'armeabi-v7a')

STUB = 0x7F000000
RETADDR = 0x7F100000
HEAP = 0x40000000
HEAP_SIZE = 0x08000000
STACK = 0x50000000
STACK_SIZE = 0x00200000
STATIC = 0x60000000        # data objects for imports (stack guard, ctype tables, __sF)
STATIC_SIZE = 0x00100000

BASES = {
    'libc++_shared.so': 0x10000000,
    'libopencv_core.so': 0x14000000,
    'libopencv_imgproc.so': 0x18000000,
    'libopencv_imgcodecs.so': 0x1C000000,
    'libcozmoEngine.so': 0x20000000,
}
ORDER = ['libc++_shared.so', 'libopencv_core.so', 'libopencv_imgproc.so', 'libopencv_imgcodecs.so', 'libcozmoEngine.so']

M32 = 0xFFFFFFFF


class Abort(Exception):
    """The emulated code called abort / __stack_chk_fail / an unmodelled import."""


class CxxThrow(Exception):
    """The emulated code called __cxa_throw."""

    def __init__(self, typename, message=''):
        super().__init__(f'{typename}: {message}')
        self.typename = typename
        self.message = message


class Lib:
    def __init__(self, name, base):
        self.name = name
        self.base = base
        path = os.path.join(LIBDIR, name)
        self.raw = open(path, 'rb').read()
        self.elf = lief.parse(path)
        self.loads = [s for s in self.elf.segments if s.type == lief.ELF.Segment.TYPE.LOAD]
        self.end = max(s.virtual_address + s.virtual_size for s in self.loads)
        self.syms = {}
        for s in self.elf.dynamic_symbols:
            if s.shndx != 0 and s.name:
                self.syms[s.name] = base + s.value

    def va(self, v):
        return self.base + v


class Machine:
    def __init__(self, libs=ORDER, poison=0x00):
        self.poison = poison & 0xFF
        uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
        self.uc = uc
        uc.mem_map(STUB, 0x100000)
        uc.mem_map(RETADDR, 0x1000)
        uc.mem_map(HEAP, HEAP_SIZE)
        uc.mem_map(STACK, STACK_SIZE)
        uc.mem_map(STATIC, STATIC_SIZE)
        uc.reg_write(UC_ARM_REG_C1_C0_2, uc.reg_read(UC_ARM_REG_C1_C0_2) | (0xF << 20))
        uc.reg_write(UC_ARM_REG_FPEXC, 0x40000000)
        self.libs = {}
        for name in libs:
            lib = Lib(name, BASES[name])
            self.libs[name] = lib
            size = (lib.end + 0xFFF) & ~0xFFF
            uc.mem_map(lib.base, size)
            for s in lib.loads:
                uc.mem_write(lib.base + s.virtual_address, lib.raw[s.file_offset:s.file_offset + s.physical_size])
        self.hooks = {}            # absolute address -> fn(machine) -> r0 or None
        self.stub_names = {}       # stub address -> name
        self.stub_addr = {}        # name -> stub address
        self.next_stub = STUB
        self.static_top = STATIC
        self.heap_top = HEAP
        self.free_list = []        # (addr, size)
        self.blocks = {}           # addr -> size
        self.tls = {}
        self.next_key = 1
        self.jmpbufs = {}
        self.log = []              # captured log / printf output
        self.calls = []            # trace of hooked calls, when tracing
        self.trace_names = None
        self.libc = {}
        self._install_libc()
        self.overrides = {}        # symbol name -> python hook that wins over any definition
        self.name_hooks = {}
        self.pending = None
        self.stub_fill = struct.pack('<I', 0xE12FFF1E) * 0x40000
        uc.mem_write(STUB, self.stub_fill)
        uc.hook_add(UC_HOOK_CODE, self._stub_hook, begin=STUB, end=STUB + 0xFFFFF)
        uc.hook_add(UC_HOOK_MEM_READ_UNMAPPED | UC_HOOK_MEM_WRITE_UNMAPPED | UC_HOOK_MEM_FETCH_UNMAPPED, self._unmapped)
        self._relocated = False

    # ---- memory helpers
    def w32(self, a, v):
        self.uc.mem_write(a, struct.pack('<I', v & M32))

    def r32(self, a):
        return struct.unpack('<I', bytes(self.uc.mem_read(a, 4)))[0]

    def r8(self, a):
        return bytes(self.uc.mem_read(a, 1))[0]

    def w8(self, a, v):
        self.uc.mem_write(a, bytes([v & 0xFF]))

    def r16(self, a):
        return struct.unpack('<H', bytes(self.uc.mem_read(a, 2)))[0]

    def w16(self, a, v):
        self.uc.mem_write(a, struct.pack('<H', v & 0xFFFF))

    def rd(self, a, n):
        return bytes(self.uc.mem_read(a, n)) if n else b''

    def wr(self, a, data):
        if data:
            self.uc.mem_write(a, bytes(data))

    def cstr(self, a, limit=4096):
        out = bytearray()
        while len(out) < limit:
            b = self.r8(a + len(out))
            if b == 0:
                break
            out.append(b)
        return bytes(out)

    def reg(self, n):
        return self.uc.reg_read(UC_ARM_REG_R0 + n)

    def setreg(self, n, v):
        self.uc.reg_write(UC_ARM_REG_R0 + n, v & M32)

    def sp(self):
        return self.uc.reg_read(UC_ARM_REG_SP)

    def arg(self, n):
        """n-th 32-bit argument of a hooked call (r0..r3 then the stack)."""
        if n < 4:
            return self.reg(n)
        return self.r32(self.sp() + 4 * (n - 4))

    def dbl(self, lo, hi):
        return struct.unpack('<d', struct.pack('<II', lo & M32, hi & M32))[0]

    def set_dbl(self, v):
        lo, hi = struct.unpack('<II', struct.pack('<d', v))
        self.setreg(0, lo)
        self.setreg(1, hi)

    def static_alloc(self, size, align=16):
        a = (self.static_top + align - 1) & ~(align - 1)
        self.static_top = a + size
        assert self.static_top <= STATIC + STATIC_SIZE
        return a

    # ---- heap (fixture boundary; see module docstring)
    def malloc(self, size):
        size = max((size + 15) & ~15, 16)
        for i, (a, s) in enumerate(self.free_list):
            if s >= size:
                del self.free_list[i]
                if s > size:
                    self.free_list.append((a + size, s - size))
                self.blocks[a] = size
                return a
        a = self.heap_top
        if a + size > HEAP + HEAP_SIZE:
            return 0
        self.heap_top += size
        self.uc.mem_write(a, bytes([self.poison]) * size)
        self.blocks[a] = size
        return a

    def mark_heap_base(self):
        """Remember the heap state after library initialisation; reset_heap() returns to it."""
        self.heap_base = self.heap_top
        self.free_base = list(self.free_list)
        self.blocks_base = dict(self.blocks)

    def reset_heap(self):
        """Forget every allocation made since mark_heap_base() and refill that memory with the poison byte, so an uninitialised read
        returns the poison and nothing from an earlier call (deterministic per run; two poison values expose such reads)."""
        if self.heap_top > self.heap_base:
            self.uc.mem_write(self.heap_base, bytes([self.poison]) * (self.heap_top - self.heap_base))
        for a, size in self.free_base:
            self.uc.mem_write(a, bytes([self.poison]) * size)
        self.heap_top = self.heap_base
        self.free_list = list(self.free_base)
        self.blocks = dict(self.blocks_base)

    def free(self, a):
        if a and a in self.blocks:
            self.free_list.append((a, self.blocks.pop(a)))

    # ---- relocation / symbol binding
    def relocate(self):
        order = [self.libs[n] for n in ORDER if n in self.libs]
        for lib in order:
            for rel in lib.elf.relocations:
                a = lib.base + rel.address
                t = str(rel.type)
                if t.endswith('ARM_RELATIVE'):
                    self.w32(a, self.r32(a) + lib.base)
                    continue
                if not rel.has_symbol:
                    raise RuntimeError(f'{lib.name}: relocation {t} without a symbol at {rel.address:#x}')
                name = rel.symbol.name
                target = self.resolve(name, lib)
                if t.endswith('ABS32'):
                    self.w32(a, target + self.r32(a))
                elif t.endswith('GLOB_DAT') or t.endswith('JUMP_SLOT'):
                    self.w32(a, target)
                else:
                    raise RuntimeError(f'{lib.name}: unsupported relocation {t}')
        self._relocated = True

    def resolve(self, name, owner):
        if name in self.overrides or name in PREFER_HOOK:
            return self.stub(name)
        if owner.syms.get(name):
            return owner.syms[name]
        for n in ORDER:
            if n in self.libs and name in self.libs[n].syms:
                return self.libs[n].syms[name]
        if name in self.libc:
            return self.stub(name)
        if name in DATA_SYMBOLS:
            return self.data_symbol(name)
        return self.stub(name)

    def stub(self, name):
        if name not in self.stub_addr:
            a = self.next_stub
            self.next_stub += 4
            self.stub_addr[name] = a
            self.stub_names[a] = name
        return self.stub_addr[name]

    def data_symbol(self, name):
        key = ('data', name)
        if key in self.stub_addr:
            return self.stub_addr[key]
        a = DATA_SYMBOLS[name](self)
        self.stub_addr[key] = a
        return a

    def sym(self, name):
        for n in ORDER:
            if n in self.libs and name in self.libs[n].syms:
                return self.libs[n].syms[name]
        raise KeyError(name)

    # ---- hook dispatch
    def _stub_hook(self, uc, address, size, ud):
        name = self.stub_names.get(address)
        if name is None:
            return
        fn = self.overrides.get(name) or self.libc.get(name)
        if fn is None and (name.startswith('_ZN3tbb') or name.startswith('_ZNK3tbb')):
            fn = TBB.get(name, lambda m: 0)      # tbb is not run: cv::parallel_for_ is replaced, its static initialisers only configure it
        if self.trace_names is not None and (self.trace_names is True or name in self.trace_names):
            self.calls.append((name, self.reg(0), self.reg(1), self.reg(2), self.reg(3)))
        if fn is None:
            raise Abort(f'unmodelled import {name} (lr {self.uc.reg_read(UC_ARM_REG_LR):#x})')
        r = fn(self)
        if r is NORET:
            return
        if r is not None:
            uc.reg_write(UC_ARM_REG_R0, r & M32)
        uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))

    def hook_addr(self, address, fn):
        """Run fn(machine) instead of the code at the absolute address (returns to lr)."""
        def cb(uc, a, size, ud):
            r = fn(self)
            if r is NORET:
                return
            if r is not None:
                uc.reg_write(UC_ARM_REG_R0, r & M32)
            uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))
        self.uc.hook_add(UC_HOOK_CODE, cb, begin=address & ~1, end=address & ~1)

    def _unmapped(self, uc, access, addr, size, value, ud):
        pc = uc.reg_read(UC_ARM_REG_PC)
        self.fault = f'unmapped access {addr:#x} size {size} access {access} at pc {pc:#x} lr {uc.reg_read(UC_ARM_REG_LR):#x}'
        return False

    # ---- running
    def call(self, addr, *args, stack=(), count=0, fpscr=0):
        """Call addr (include the Thumb bit for Thumb functions); returns r0. Registers other than the arguments are zeroed."""
        uc = self.uc
        for n in range(UC_ARM_REG_R0, UC_ARM_REG_R12 + 1):
            uc.reg_write(n, 0)
        sp = STACK + STACK_SIZE - 0x100
        sp -= 4 * len(stack)
        sp &= ~7
        for i, v in enumerate(stack):
            self.w32(sp + 4 * i, v)
        for i, v in enumerate(args[:4]):
            uc.reg_write(UC_ARM_REG_R0 + i, v & M32)
        if len(args) > 4:
            raise ValueError('pass arguments beyond r3 with stack=')
        uc.reg_write(UC_ARM_REG_SP, sp)
        uc.reg_write(UC_ARM_REG_LR, RETADDR)
        uc.reg_write(UC_ARM_REG_FPSCR, fpscr)
        try:
            uc.emu_start(addr, RETADDR, count=count)
        except UcError as e:
            raise Abort(f'unicorn: {e} {getattr(self, "fault", "")} at pc {uc.reg_read(UC_ARM_REG_PC):#x} lr {uc.reg_read(UC_ARM_REG_LR):#x}')
        if uc.reg_read(UC_ARM_REG_PC) != RETADDR:
            raise Abort(f'call to {addr:#x} stopped at pc {uc.reg_read(UC_ARM_REG_PC):#x} (instruction budget?)')
        return uc.reg_read(UC_ARM_REG_R0)

    def call_sret(self, addr, *args, count=0):
        """Call with up to four arguments in r0..r3 (r0 is the hidden return pointer for a struct return)."""
        return self.call(addr, *args, count=count)

    def start_coverage(self, *libnames):
        """Record every executed translation block (absolute start -> size) inside the named libraries; the dict is `self.cov`."""
        self.cov = {}

        def cb(uc, address, size, ud):
            self.cov[address & ~1] = max(self.cov.get(address & ~1, 0), size)
        for libname in libnames:
            lib = self.libs[libname]
            self.uc.hook_add(UC_HOOK_BLOCK, cb, begin=lib.base, end=lib.base + lib.end)

    def covered_bytes(self, libname, start, end):
        """Bytes of [start, end) (library virtual addresses) that lie inside an executed block."""
        base = self.libs[libname].base
        n = 0
        for a, size in self.cov.items():
            lo, hi = max(a, base + start), min(a + size, base + end)
            if hi > lo:
                n += hi - lo
        return n

    def poison_stack(self):
        self.uc.mem_write(STACK, bytes([self.poison]) * STACK_SIZE)

    # ---- libc
    def _install_libc(self):
        L = self.libc

        def reg_(name):
            def deco(fn):
                L[name] = fn
                return fn
            return deco

        for nm in ('pthread_mutex_lock', 'pthread_mutex_unlock', 'pthread_mutex_init', 'pthread_mutex_destroy',
                   'pthread_mutex_trylock', 'pthread_mutexattr_init', 'pthread_mutexattr_destroy',
                   'pthread_mutexattr_settype', 'pthread_cond_init', 'pthread_cond_destroy', 'pthread_cond_signal',
                   'pthread_cond_broadcast', 'pthread_cond_wait', 'sched_yield', '__cxa_atexit', '__aeabi_atexit',
                   '__cxa_finalize', 'fflush', 'pthread_setspecific_dummy'):
            L[nm] = lambda m: 0

        @reg_('malloc')
        def _(m):
            return m.malloc(m.reg(0))

        @reg_('calloc')
        def _(m):
            n = m.reg(0) * m.reg(1)
            a = m.malloc(n)
            if a:
                m.wr(a, bytes(n))
            return a

        @reg_('free')
        def _(m):
            m.free(m.reg(0))
            return 0

        @reg_('realloc')
        def _(m):
            p, n = m.reg(0), m.reg(1)
            if p == 0:
                return m.malloc(n)
            old = m.blocks.get(p, 0)
            a = m.malloc(n)
            if a:
                m.wr(a, m.rd(p, min(old, n)))
                m.free(p)
            return a

        @reg_('memcpy')
        def _(m):
            d, s, n = m.reg(0), m.reg(1), m.reg(2)
            m.wr(d, m.rd(s, n))
            return d
        L['memmove'] = L['memcpy']
        L['__aeabi_memcpy'] = lambda m: (m.wr(m.reg(0), m.rd(m.reg(1), m.reg(2))), None)[1]
        L['__aeabi_memmove'] = L['__aeabi_memcpy']

        @reg_('memset')
        def _(m):
            d, v, n = m.reg(0), m.reg(1), m.reg(2)
            m.wr(d, bytes([v & 0xFF]) * n)
            return d

        @reg_('__aeabi_memclr8')
        def _(m):
            m.wr(m.reg(0), bytes(m.reg(1)))
        L['__aeabi_memclr'] = L['__aeabi_memclr4'] = L['__aeabi_memclr8']

        @reg_('__aeabi_memset')
        def _(m):
            m.wr(m.reg(0), bytes([m.reg(2) & 0xFF]) * m.reg(1))
        L['__aeabi_memset8'] = L['__aeabi_memset4'] = L['__aeabi_memset']

        @reg_('memcmp')
        def _(m):
            a, b = m.rd(m.reg(0), m.reg(2)), m.rd(m.reg(1), m.reg(2))
            return 0 if a == b else (-1 if a < b else 1)

        @reg_('memchr')
        def _(m):
            p, c, n = m.reg(0), m.reg(1) & 0xFF, m.reg(2)
            i = m.rd(p, n).find(bytes([c]))
            return 0 if i < 0 else p + i

        @reg_('strlen')
        def _(m):
            return len(m.cstr(m.reg(0), 1 << 20))

        @reg_('strcmp')
        def _(m):
            a, b = m.cstr(m.reg(0)), m.cstr(m.reg(1))
            return 0 if a == b else (-1 if a < b else 1)

        @reg_('strncmp')
        def _(m):
            n = m.reg(2)
            a, b = m.cstr(m.reg(0))[:n], m.cstr(m.reg(1))[:n]
            return 0 if a == b else (-1 if a < b else 1)

        @reg_('strchr')
        def _(m):
            p, c = m.reg(0), m.reg(1) & 0xFF
            s = m.cstr(p)
            if c == 0:
                return p + len(s)
            i = s.find(bytes([c]))
            return 0 if i < 0 else p + i

        @reg_('strrchr')
        def _(m):
            p, c = m.reg(0), m.reg(1) & 0xFF
            s = m.cstr(p)
            if c == 0:
                return p + len(s)
            i = s.rfind(bytes([c]))
            return 0 if i < 0 else p + i

        @reg_('strcpy')
        def _(m):
            m.wr(m.reg(0), m.cstr(m.reg(1)) + b'\0')
            return m.reg(0)

        @reg_('strncpy')
        def _(m):
            n = m.reg(2)
            s = m.cstr(m.reg(1))[:n]
            m.wr(m.reg(0), s + bytes(n - len(s)))
            return m.reg(0)

        @reg_('strstr')
        def _(m):
            h = m.cstr(m.reg(0), 1 << 20)
            i = h.find(m.cstr(m.reg(1)))
            return 0 if i < 0 else m.reg(0) + i

        for nm in ('open', 'opendir', 'fopen', 'stat', 'fstat', 'readdir', 'mkstemp', 'getenv', 'dlopen', 'dlsym', 'dladdr', 'closedir', 'close', 'read'):
            L[nm] = lambda m: 0xFFFFFFFF if True else 0
        L['getenv'] = lambda m: 0
        L['opendir'] = L['fopen'] = L['dlopen'] = L['dlsym'] = L['readdir'] = lambda m: 0

        @reg_('abort')
        def _(m):
            raise Abort('abort() called')

        @reg_('__stack_chk_fail')
        def _(m):
            raise Abort('__stack_chk_fail')

        @reg_('__errno')
        def _(m):
            return m.errno_addr

        self.errno_addr = self.static_alloc(16)

        @reg_('__cxa_throw')
        def _(m):
            tinfo = m.reg(1)
            name = ''
            try:
                name = m.cstr(m.r32(tinfo + 4)).decode('latin1')
            except Exception:
                pass
            raise CxxThrow(name)

        @reg_('__cxa_allocate_exception')
        def _(m):
            return m.malloc(m.reg(0) + 128) + 128

        @reg_('__android_log_print')
        def _(m):
            m.log.append(('android_log', m.cstr(m.reg(1)).decode('latin1'), m.fmt(m.cstr(m.reg(2)), 3)))
            return 0

        @reg_('fprintf')
        def _(m):
            m.log.append(('fprintf', m.fmt(m.cstr(m.reg(1)), 2)))
            return 0

        @reg_('printf')
        def _(m):
            m.log.append(('printf', m.fmt(m.cstr(m.reg(0)), 1)))
            return 0

        @reg_('snprintf')
        def _(m):
            s = m.fmt(m.cstr(m.reg(2)), 3).encode('latin1')
            n = m.reg(1)
            if n:
                m.wr(m.reg(0), s[:n - 1] + b'\0')
            return len(s)

        @reg_('vsnprintf')
        def _(m):
            s = m.fmt(m.cstr(m.reg(2)), 0, ap=m.reg(3)).encode('latin1')
            n = m.reg(1)
            if n:
                m.wr(m.reg(0), s[:n - 1] + b'\0')
            return len(s)

        @reg_('vfprintf')
        def _(m):
            m.log.append(('vfprintf', m.fmt(m.cstr(m.reg(1)), 0, ap=m.reg(2))))
            return 0

        @reg_('sprintf')
        def _(m):
            s = m.fmt(m.cstr(m.reg(1)), 2).encode('latin1')
            m.wr(m.reg(0), s + b'\0')
            return len(s)

        # ---- setjmp / longjmp: r4-r11, sp, lr and d8-d15 into a Python side table keyed by the buffer address
        @reg_('setjmp')
        def _(m):
            buf = m.reg(0)
            saved = [m.uc.reg_read(UC_ARM_REG_R4 + i) for i in range(8)]
            saved += [m.uc.reg_read(UC_ARM_REG_SP), m.uc.reg_read(UC_ARM_REG_LR)]
            dregs = [m.uc.reg_read(UC_ARM_REG_D8 + i) for i in range(8)]
            m.jmpbufs[buf] = (saved, dregs)
            return 0
        L['_setjmp'] = L['setjmp']

        @reg_('longjmp')
        def _(m):
            buf, val = m.reg(0), m.reg(1)
            saved, dregs = m.jmpbufs[buf]
            for i in range(8):
                m.uc.reg_write(UC_ARM_REG_R4 + i, saved[i])
            for i in range(8):
                m.uc.reg_write(UC_ARM_REG_D8 + i, dregs[i])
            m.uc.reg_write(UC_ARM_REG_SP, saved[8])
            m.uc.reg_write(UC_ARM_REG_R0, val or 1)
            m.uc.reg_write(UC_ARM_REG_PC, saved[9])
            return NORET
        L['_longjmp'] = L['longjmp']

        # ---- pthread
        @reg_('pthread_key_create')
        def _(m):
            k = m.next_key
            m.next_key += 1
            m.w32(m.reg(0), k)
            return 0

        @reg_('pthread_key_delete')
        def _(m):
            return 0

        @reg_('pthread_getspecific')
        def _(m):
            return m.tls.get(m.reg(0), 0)

        @reg_('pthread_setspecific')
        def _(m):
            m.tls[m.reg(0)] = m.reg(1)
            return 0

        @reg_('pthread_once')
        def _(m):
            ctl, fn = m.reg(0), m.reg(1)
            if m.r32(ctl) == 0:
                m.w32(ctl, 1)
                m.uc.reg_write(UC_ARM_REG_PC, fn)      # run the init routine, which returns to our caller
                return NORET
            return 0

        # ---- integer helpers from libgcc
        def s32(v):
            v &= M32
            return v - (1 << 32) if v & 0x80000000 else v

        def tdiv(a, b):
            if b == 0:
                return 0
            q = abs(a) // abs(b)
            return q if (a < 0) == (b < 0) else -q

        @reg_('__aeabi_idiv')
        def _(m):
            return tdiv(s32(m.reg(0)), s32(m.reg(1)))

        @reg_('__aeabi_uidiv')
        def _(m):
            b = m.reg(1)
            return m.reg(0) // b if b else 0

        @reg_('__aeabi_idivmod')
        def _(m):
            a, b = s32(m.reg(0)), s32(m.reg(1))
            q = tdiv(a, b)
            m.setreg(1, a - q * b)
            return q

        @reg_('__aeabi_uidivmod')
        def _(m):
            a, b = m.reg(0), m.reg(1)
            if b == 0:
                m.setreg(1, 0)
                return 0
            m.setreg(1, a % b)
            return a // b

        def s64(lo, hi):
            v = (hi << 32) | lo
            return v - (1 << 64) if v & (1 << 63) else v

        @reg_('__aeabi_ldivmod')
        def _(m):
            a, b = s64(m.reg(0), m.reg(1)), s64(m.reg(2), m.reg(3))
            q = tdiv(a, b)
            r = a - q * b
            m.setreg(1, (q >> 32) & M32)
            m.setreg(2, r & M32)
            m.setreg(3, (r >> 32) & M32)
            return q & M32

        @reg_('__aeabi_uldivmod')
        def _(m):
            a, b = (m.reg(1) << 32) | m.reg(0), (m.reg(3) << 32) | m.reg(2)
            q, r = (a // b, a % b) if b else (0, 0)
            m.setreg(1, (q >> 32) & M32)
            m.setreg(2, r & M32)
            m.setreg(3, (r >> 32) & M32)
            return q & M32

        @reg_('__aeabi_l2d')
        def _(m):
            m.set_dbl(float(s64(m.reg(0), m.reg(1))))
            return NORET_R0R1

        @reg_('__aeabi_ul2d')
        def _(m):
            m.set_dbl(float((m.reg(1) << 32) | m.reg(0)))

        @reg_('__aeabi_ui2d')
        def _(m):
            m.set_dbl(float(m.reg(0)))

        @reg_('__aeabi_dsub')
        def _(m):
            m.set_dbl(m.dbl(m.reg(0), m.reg(1)) - m.dbl(m.reg(2), m.reg(3)))

        # ---- libm (softfp: doubles in r0:r1, r2:r3; floats in r0, r1)
        def m1(fn):
            def h(m):
                m.set_dbl(fn(m.dbl(m.reg(0), m.reg(1))))
            return h

        for nm, fn in (('sqrt', math.sqrt), ('floor', math.floor), ('ceil', math.ceil), ('exp', math.exp), ('log', math.log),
                       ('log10', math.log10), ('sin', math.sin), ('cos', math.cos), ('acos', math.acos), ('asin', math.asin)):
            L[nm] = m1((lambda f: (lambda x: float(f(x))))(fn))

        def pow_(m):
            m.set_dbl(math.pow(m.dbl(m.reg(0), m.reg(1)), m.dbl(m.reg(2), m.reg(3))))
        L['pow'] = pow_

        def f32(bits):
            return struct.unpack('<f', struct.pack('<I', bits & M32))[0]

        def bits32(x):
            return struct.unpack('<I', struct.pack('<f', x))[0]

        L['sqrtf'] = lambda m: bits32(math.sqrt(f32(m.reg(0))))

    # ---- printf subset
    def fmt(self, fmt, argn, ap=None):
        """Render a printf format; arguments come from r0..r3/stack from index argn, or from a va_list pointer `ap`."""
        import re

        def nxt():
            nonlocal argn, ap
            if ap is not None:
                v = self.r32(ap)
                ap += 4
                return v
            v = self.arg(argn)
            argn += 1
            return v
        s = fmt.decode('latin1')
        res = ''
        pos = 0
        for mt in re.finditer(r'%([-+ #0]*)(\*|\d+)?(?:\.(\*|\d+))?(hh|h|ll|l|z|j|t)?([diuxXcsfgeEp%])', s):
            res += s[pos:mt.start()]
            pos = mt.end()
            flags, width, prec, _, conv = mt.groups()
            if conv == '%':
                res += '%'
                continue
            if width == '*':
                width = str(nxt())
            if prec == '*':
                prec = str(nxt())
            if conv in 'di':
                v = nxt()
                v = v - (1 << 32) if v & 0x80000000 else v
                res += ('%' + flags + (width or '') + 'd') % v
            elif conv in 'uxX':
                res += ('%' + flags + (width or '') + conv) % nxt()
            elif conv == 'c':
                res += chr(nxt() & 0xFF)
            elif conv == 's':
                p = nxt()
                res += self.cstr(p).decode('latin1') if p else '(null)'
            elif conv == 'p':
                res += '0x%x' % nxt()
            else:
                if ap is not None:
                    ap = (ap + 7) & ~7
                elif argn % 2:
                    argn += 1
                lo, hi = nxt(), nxt()
                d = self.dbl(lo, hi)
                res += ('%' + flags + (width or '') + ('.' + prec if prec else '') + conv) % d
        res += s[pos:]
        return res


NORET = object()
PREFER_HOOK = {'__cxa_throw'}      # stop the run at a C++ throw instead of unwinding (the unwinder is not shipped here)
TBB = {'_ZN3tbb8internal12NFS_AllocateEjjPv': lambda m: m.malloc(m.reg(0) * m.reg(1))}
NORET_R0R1 = None


def _stack_guard(m):
    a = m.static_alloc(16)
    m.w32(a, 0x12345678)
    return a


def _ctype(m):
    table = m.static_alloc(1024)
    m.wr(table, bytes([0]) + bytes(_ctype_flag(x) for x in range(256)))
    a = m.static_alloc(16)
    m.w32(a, table)
    return a


def _ctype_flag(c):
    # bionic _ctype_ flags: _U 1, _L 2, _N 4, _S 8, _P 0x10, _C 0x20, _X 0x40, _B 0x80
    f = 0
    ch = chr(c)
    if 'A' <= ch <= 'Z':
        f |= 1
    if 'a' <= ch <= 'z':
        f |= 2
    if '0' <= ch <= '9':
        f |= 4
    if c in (9, 10, 11, 12, 13, 32):
        f |= 8
    if 33 <= c <= 126 and not ch.isalnum():
        f |= 0x10
    if c < 32 or c == 127:
        f |= 0x20
    if ch in '0123456789abcdefABCDEF':
        f |= 0x40
    if c == 32:
        f |= 0x80
    return f


def _tolower(m):
    table = m.static_alloc(1024)
    m.wr(table, b''.join(struct.pack('<h', x + 32 if 65 <= x <= 90 else x) for x in [0] + list(range(256))))
    a = m.static_alloc(16)
    m.w32(a, table)
    return a


def _toupper(m):
    table = m.static_alloc(1024)
    m.wr(table, b''.join(struct.pack('<h', x - 32 if 97 <= x <= 122 else x) for x in [0] + list(range(256))))
    a = m.static_alloc(16)
    m.w32(a, table)
    return a


def _sf(m):
    return m.static_alloc(0x400)


DATA_SYMBOLS = {
    '__stack_chk_guard': _stack_guard,
    '_ctype_': _ctype,
    '_tolower_tab_': _tolower,
    '_toupper_tab_': _toupper,
    '__sF': _sf,
}
