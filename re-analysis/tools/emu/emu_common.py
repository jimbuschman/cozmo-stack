"""A small Unicorn harness over libcozmoEngine.so for the B-M6b-4 oracles (emu_media.py, emu_play.py, ...).

The .so is mapped as it is on disk; every GOT word is pointed at its own zeroed 0x40-byte block (so a `ldr rX,[got]; ldr rY,[rX]` finds 0), then a script sets the few slots whose value it
cares about. PLT stubs are hooked in Python (mutex lock/unlock, __aeabi_uidivmod, memmove, powf, expf ...); engine functions the oracle does not run are hooked the same way.
"""
import math
import struct
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED
from unicorn.arm_const import *
from so import RAW, LOADS, ELF, off

BASE = 0x02000000          # scratch memory for objects
HEAP = 0x03000000          # bump allocator
STACK = 0x04000000
RET = 0x05000000
DUMMY = 0x06000000         # GOT dummy blocks
GOT_START, GOT_END = 0x103E78C, 0x103E78C + 0x12874


def f32(bits):
    return struct.unpack('<f', struct.pack('<I', bits & 0xFFFFFFFF))[0]


def bits(x):
    return struct.unpack('<I', struct.pack('<f', x))[0]


class Emu:
    def __init__(self):
        uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
        end = max(s.virtual_address + s.virtual_size for s in LOADS)
        uc.mem_map(0, (end + 0xFFFF) & ~0xFFFF)
        for s in LOADS:
            uc.mem_write(s.virtual_address, RAW[s.file_offset:s.file_offset + s.physical_size])
        for b, size in ((BASE, 0x100000), (HEAP, 0x400000), (STACK, 0x100000), (RET, 0x1000), (DUMMY, 0x200000)):
            uc.mem_map(b, size)
        uc.reg_write(UC_ARM_REG_C1_C0_2, uc.reg_read(UC_ARM_REG_C1_C0_2) | (0xF << 20))
        uc.reg_write(UC_ARM_REG_FPEXC, 0x40000000)
        self.uc = uc
        # R_ARM_RELATIVE words keep their in-place addend (an address inside the image: e.g. GOT 0x10400AC -> 0x1052454); words the loader would fill from a symbol (zero on disk) point at a zeroed block.
        for i, a in enumerate(range(GOT_START, GOT_END, 4)):
            if struct.unpack_from('<I', RAW, off(a))[0] == 0:
                uc.mem_write(a, struct.pack('<I', DUMMY + 0x40 * i))
        self.heap = HEAP
        self.fail_alloc_at = None      # fail the n-th (1-based) allocation
        self.alloc_calls = 0
        self.log = []
        uc.hook_add(UC_HOOK_MEM_UNMAPPED, self._unmapped)

    def _unmapped(self, uc, access, addr, size, value, _):
        print(f'unmapped {addr:#x} pc {uc.reg_read(UC_ARM_REG_PC):#x}')
        return False

    # -- memory helpers
    def w32(self, a, v):
        self.uc.mem_write(a, struct.pack('<I', v & 0xFFFFFFFF))

    def r32(self, a):
        return struct.unpack('<I', bytes(self.uc.mem_read(a, 4)))[0]

    def w16(self, a, v):
        self.uc.mem_write(a, struct.pack('<H', v & 0xFFFF))

    def r16(self, a):
        return struct.unpack('<H', bytes(self.uc.mem_read(a, 2)))[0]

    def w8(self, a, v):
        self.uc.mem_write(a, bytes([v & 0xFF]))

    def r8(self, a):
        return bytes(self.uc.mem_read(a, 1))[0]

    def wf(self, a, x):
        self.w32(a, bits(x))

    def rf(self, a):
        return f32(self.r32(a))

    def set_got(self, slot, value):
        """Store `value` in the GOT word at `slot`."""
        self.w32(slot, value)

    def alloc(self, size):
        a = self.heap
        self.heap += (max(size, 1) + 15) & ~15
        return a

    # -- hooks
    def hook(self, addr, fn):
        """Run fn(emu) instead of the code at addr; fn returns the value for r0 (or None to leave it) and execution returns to lr. A second hook on the same address replaces the first."""
        if not hasattr(self, '_hooks'):
            self._hooks = {}
        first = addr not in self._hooks
        self._hooks[addr] = fn
        if not first:
            return

        def cb(uc, address, size, _):
            r = self._hooks[address](self)
            if r is not None:
                uc.reg_write(UC_ARM_REG_R0, r & 0xFFFFFFFF)
            uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))
        self.uc.hook_add(UC_HOOK_CODE, cb, begin=addr, end=addr)

    def std_hooks(self):
        """mutex lock/unlock, __aeabi_uidivmod, memmove, and the pool allocator."""
        self.hook(0x4D3064, lambda e: 0)
        self.hook(0x4D3070, lambda e: 0)
        self.hook(0x4D6688, lambda e: 0)      # __cxa_atexit (the tail call of the static constructors)

        def uidivmod(e):
            n, d = e.reg(0), e.reg(1)
            e.uc.reg_write(UC_ARM_REG_R1, n % d if d else 0)
            return n // d if d else 0
        self.hook(0x4A6694, uidivmod)
        self.hook(0x4BE310, lambda e: e.reg(0) // e.reg(1) if e.reg(1) else 0)

        def memmove(e):
            d, s, n = e.reg(0), e.reg(1), e.reg(2)
            e.uc.mem_write(d, bytes(e.uc.mem_read(s, n)))
            return d
        self.hook(0x4D667C, memmove)

        def alloc(e):          # 0xA7A7F4(pool, size)
            self.alloc_calls += 1
            if self.fail_alloc_at is not None and self.alloc_calls == self.fail_alloc_at:
                return 0
            return self.alloc(e.reg(1))
        self.hook(0xA7A7F4, alloc)
        self.hook(0xA7A988, lambda e: None)
        self.hook(0xA7A914, lambda e: None)

    def reg_sp(self):
        return self.uc.reg_read(UC_ARM_REG_SP)

    def reg(self, n):
        return self.uc.reg_read(UC_ARM_REG_R0 + n)

    def call(self, addr, *args, stack=(), until=RET):
        uc = self.uc
        sp = STACK + 0xE0000
        for i, v in enumerate(reversed(stack)):
            pass
        sp -= 4 * len(stack)
        sp &= ~7  # AAPCS: sp is 8-aligned at the call (the engine's 0x9B2B08 stack buffer relies on it)
        for i, v in enumerate(stack):
            uc.mem_write(sp + 4 * i, struct.pack('<I', v & 0xFFFFFFFF))
        for i, v in enumerate(args[:4]):
            uc.reg_write(UC_ARM_REG_R0 + i, v & 0xFFFFFFFF)
        uc.reg_write(UC_ARM_REG_SP, sp)
        uc.reg_write(UC_ARM_REG_LR, until)
        uc.emu_start(addr, until)
        return uc.reg_read(UC_ARM_REG_R0)
