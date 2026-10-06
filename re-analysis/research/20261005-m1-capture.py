"""Read-only Thumb evidence capture for the 2026-10-05 M1 research reports."""
import argparse, hashlib
from pathlib import Path
import capstone, lief
p=argparse.ArgumentParser();p.add_argument('output');p.add_argument('ranges',nargs='+');a=p.parse_args()
path=Path(r'C:/Users/JimBu/Downloads/com.anki.cozmo_3.4.0-1204_minAPI21(armeabi-v7a)(nodpi)_apkmirror.com.apk_Decompiler.com/resources/lib/armeabi-v7a/libcozmoEngine.so')
raw=path.read_bytes();elf=lief.ELF.parse(str(path));md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB)
plt=elf.get_section('.plt');pm={plt.virtual_address+20+12*i:r.symbol.name for i,r in enumerate(elf.pltgot_relocations) if r.has_symbol};sm={s.value&~1:s.name for s in elf.dynamic_symbols if s.shndx and s.value}
lines=['SHA256 '+hashlib.sha256(raw).hexdigest()]
for r in a.ranges:
 start,end=[int(v,16) for v in r.split(':')];seg=next(s for s in elf.segments if s.virtual_address<=start<s.virtual_address+s.physical_size);off=start-seg.virtual_address+seg.file_offset
 lines.append('\nFUNCTION '+hex(start)+' '+sm.get(start,''))
 for ins in md.disasm(raw[off:off+end-start],start):
  note=''
  if ins.mnemonic.startswith(('bl','b.w')) and ins.op_str.startswith('#'):
   target=int(ins.op_str[1:],0);note=' ; '+pm.get(target,sm.get(target&~1,''))
  lines.append(f'{ins.address:08x}: {ins.mnemonic} {ins.op_str}{note}')
Path(a.output).write_text('\n'.join(lines),encoding='utf-8')
print('Saved',a.output,'with',len(lines),'lines')
