"""Load libcozmoEngine.so for emulation and disassembly: its LOAD segments, a byte reader and capstone (ARM)."""
import os
import struct
import lief
from capstone import Cs, CS_ARCH_ARM, CS_MODE_ARM

SO = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..',
                  'resources', 'lib', 'armeabi-v7a', 'libcozmoEngine.so')
ELF = lief.parse(SO)
RAW = open(SO, 'rb').read()
LOADS = [s for s in ELF.segments if s.type == lief.ELF.Segment.TYPE.LOAD]


def off(va):
    """File offset of a virtual address (the ELF's own addresses, base 0, as the repo cites them)."""
    for s in LOADS:
        if s.virtual_address <= va < s.virtual_address + s.physical_size:
            return va - s.virtual_address + s.file_offset
    raise ValueError(hex(va))


def u32(va):
    return struct.unpack_from('<I', RAW, off(va))[0]


MD = Cs(CS_ARCH_ARM, CS_MODE_ARM)


def dis(start, end):
    """ARM-mode disassembly of [start, end). Capstone stops at the first word it can't decode (a literal pool)."""
    code = RAW[off(start):off(start) + (end - start)]
    return [f'{i.address:#010x}: {i.mnemonic:8s} {i.op_str}' for i in MD.disasm(code, start)]
