"""An Emu (emu_common.py) whose stand-ins for engine and libc functions are real stubs `svc #n; bx lr`.

Emu.hook redirects PC from a code hook, which works for ARM callers but not for Thumb callers (the T bit is not switched
from inside a hook). A stub written over the function's first words returns through the real `bx lr`, so ARM and Thumb
callers both come back in their own mode. Used by the B-M6b-4 batch 5b oracles (emu_zip.py, emu_stream.py).
"""
import struct
from emu_common import Emu
from unicorn import UC_HOOK_INTR
from unicorn.arm_const import *


class SvcEmu(Emu):
    def __init__(self):
        super().__init__()
        self._svc = {}
        self.uc.hook_add(UC_HOOK_INTR, self._on_intr)

    def svc_hook(self, addr, fn, thumb=False):
        """Overwrite the code at addr with `svc #n; bx lr` (ARM) or `svc #n; bx lr` (Thumb, 4 bytes) and run fn(emu) for it."""
        n = len(self._svc) + 1
        self._svc[n] = fn
        if thumb:
            self.uc.mem_write(addr, struct.pack('<HH', 0xDF00 | n, 0x4770))
        else:
            self.uc.mem_write(addr, struct.pack('<II', 0xEF000000 | n, 0xE12FFF1E))

    def hook(self, addr, fn):
        self.svc_hook(addr, fn)

    def _on_intr(self, uc, intno, _):
        pc = uc.reg_read(UC_ARM_REG_PC)
        cpsr = uc.reg_read(UC_ARM_REG_CPSR)
        if cpsr & 0x20:
            n = struct.unpack('<H', bytes(uc.mem_read(pc - 2, 2)))[0] & 0xFF
        else:
            n = struct.unpack('<I', bytes(uc.mem_read(pc - 4, 4)))[0] & 0xFFFFFF
        r = self._svc[n](self)
        if r is not None:
            uc.reg_write(UC_ARM_REG_R0, r & 0xFFFFFFFF)
