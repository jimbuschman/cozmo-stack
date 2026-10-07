import lief, struct
from pathlib import Path
b=lief.parse('resources/lib/armeabi-v7a/libcozmoEngine.so')
w=bytes(b.get_content_from_virtual_address(0xC530C0,88))
o=['GetNextDistortionFrame table, engine VA 0x00C530C0, eleven (binary32 hold probability, signed integer degree multiplier) pairs:']
for i in range(11):
    probability,multiplier=struct.unpack_from('<Ii',w,8*i)
    o.append(f'{0xC530C0+8*i:08X}: probability={probability:08X} multiplier={multiplier}')
Path('re-analysis/research/20261006-M5-distortion-table.txt').write_text('\n'.join(o))
print('\n'.join(o))
