"""Primary byte-string census for PerfMetric; navigation text is not evidence."""
import importlib.util
from pathlib import Path
s=importlib.util.spec_from_file_location('n',Path(__file__).with_name('20261007-queue5-native.py'))
n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
for needle in (b'%5i %8.3f',b'%5i,%8.3f',b'Summary:  ',b' %8.3f %8.3f',b',%8.3f,%8.3f',b'EngineTick',b'engine ticks'):
 start=0
 while True:
  off=n.raw.find(needle,start)
  if off<0:break
  start=off+1
  for seg in n.loads:
   if seg.file_offset<=off<seg.file_offset+seg.physical_size:
    a=seg.virtual_address+off-seg.file_offset
    end=n.raw.find(b'\0',off)
    print(f'{a:08X}: {n.raw[off:end]!r}')
    break
