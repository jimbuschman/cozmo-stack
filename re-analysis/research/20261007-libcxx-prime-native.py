"""Read-only shipped libc++ prime-size dependency evidence for Q14."""
from pathlib import Path
import hashlib
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 're-analysis/research/_m5013_dependencies'))
import lief
import capstone
from capstone.arm import ARM_OP_MEM, ARM_REG_PC

path = ROOT / 'resources/lib/armeabi-v7a/libc++_shared.so'
raw = path.read_bytes()
elf = lief.parse(str(path))
loads = [s for s in elf.segments if s.type == lief.ELF.Segment.TYPE.LOAD]

def read(address, size):
    for segment in loads:
        if segment.virtual_address <= address and address + size <= segment.virtual_address + segment.physical_size:
            offset = address - segment.virtual_address + segment.file_offset
            return raw[offset:offset + size]
    raise ValueError(f'unmapped {address:08X}+{size:X}')

symbol = next(s for s in elf.symbols if s.name == '_ZNSt6__ndk112__next_primeEj' and s.value)
start = symbol.value & ~1
print('LIBRARY', path.name, 'SHA256', hashlib.sha256(raw).hexdigest())
print('SYMBOL', symbol.name, f'{symbol.value:08X}', 'SIZE', symbol.size)
decoder = capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_THUMB)
decoder.detail = True
print('CODE RANGE', f'{start:08X}..0003B65C exclusive; remaining symbol bytes are literal data')
for instruction in decoder.disasm(read(start, 0x3B65C - start), start):
    annotations = []
    for operand in instruction.operands:
        if operand.type == ARM_OP_MEM and operand.mem.base == ARM_REG_PC and not operand.mem.index:
            location = ((instruction.address + 4) & ~3) + operand.mem.disp
            value = int.from_bytes(read(location, 4), 'little')
            annotations.append(f'word[{location:08X}]={value:08X}')
    print(f'{instruction.address:08X}: {instruction.mnemonic:12} {instruction.op_str}' + (' ; ' + '; '.join(annotations) if annotations else ''))
for label, address in [('SMALL_PRIMES', 0x9B480), ('WHEEL210', 0x9B540)]:
    print(label, f'{address:08X}', ' '.join(f'{int.from_bytes(read(address + 4*i, 4), "little"):08X}' for i in range(48)))
print('OVERFLOW TEXT', repr(read(0x3B66C, 24).split(b'\0', 1)[0]))
division = capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_ARM)
print('ARM DIVISION RANGE 0008675C..00086804 exclusive')
for instruction in division.disasm(read(0x8675C, 0xA8), 0x8675C):
    print(f'{instruction.address:08X}: {instruction.mnemonic:12} {instruction.op_str}')
for relocation in elf.relocations:
    if 0xA4D1C <= relocation.address <= 0xA4D24 and relocation.has_symbol:
        print('EXCEPTION RELOCATION', f'{relocation.address:08X}', relocation.symbol.name, f'{relocation.symbol.value:08X}')
for name in ['_ZNSt6__ndk114__shared_count16__release_sharedEv', '_ZNSt6__ndk119__shared_weak_count16__release_sharedEv']:
    dependency = next(s for s in elf.symbols if s.name == name and s.value)
    print('SHARED RELEASE SYMBOL', name, f'{dependency.value:08X}', dependency.size)
    for instruction in decoder.disasm(read(dependency.value & ~1, dependency.size), dependency.value & ~1):
        print(f'{instruction.address:08X}: {instruction.mnemonic:12} {instruction.op_str}')
