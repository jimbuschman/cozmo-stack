"""Q16 shipped libDAS evidence; addresses are library relative, no guessed engine index extents."""
import importlib.util,sys,hashlib
from pathlib import Path
sp=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'));n=importlib.util.module_from_spec(sp);sp.loader.exec_module(n)
p=n.ROOT/'resources/lib/armeabi-v7a/libDAS.so'
n.elf=n.lief.parse(str(p));n.raw=p.read_bytes();n.loads=[s for s in n.elf.segments if s.type==n.lief.ELF.Segment.TYPE.LOAD]
n.symbols={s.value&~1:s.name for s in n.elf.symbols if s.value};n.rel={r.address:r for r in n.elf.relocations};n.index=[];n.starts=[]
print('libDAS.so SHA256',hashlib.sha256(n.raw).hexdigest())
for arg in sys.argv[1:]:
 if arg.startswith('words:'):
  a,z=[int(x,16) for x in arg[6:].split(':')]
  for loc in range(a,z,4):
   v=n.word(loc);r=n.rel.get(loc)
   if r and r.has_symbol and r.symbol.value:
    if r.type.name=='ARM_ABS32':v=(v+r.symbol.value)&0xffffffff
    elif r.type.name in ('ARM_GLOB_DAT','ARM_JUMP_SLOT'):v=r.symbol.value
   label=n.symbols.get(v&~1,'')
   if r and r.has_symbol and r.symbol.name:label=r.symbol.name
   print(f'{loc:08X}: raw={n.word(loc):08X} relocated={v:08X} {label}')
 else:
  thumb=arg.startswith('thumb:');arg=arg[6:] if thumb else arg;a,z=[int(x,16) for x in arg.split(':')];n.dump(a,z,thumb)
