"""Q14-008 paired-PC literal first seeds only; reuse876, no scalar rescan."""
from pathlib import Path
import hashlib
baseline=Path(__file__).with_name('20261011-sound-factory-key-paired-literal-seeds.py')
source=baseline.read_text(encoding='utf-8')
old='targets=(0xa3b684,0xa3b694,*range(0x108d9a0,0x108d9b0))'
assert source.count(old)==1
source=source.replace(old,'targets=(0xa08024,)')
print('FIRST-SEED ENUMERATOR SHA256',hashlib.sha256(baseline.read_bytes()).hexdigest())
exec(compile(source,str(baseline),'exec'),{'__file__':str(Path(__file__).resolve())})
