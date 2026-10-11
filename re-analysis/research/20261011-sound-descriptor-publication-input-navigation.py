"""Original descriptor registry population frontier, not another context family.
Direct factory and insertion inputs only; indirect/copy/mutation absence unproved.
"""
from pathlib import Path
import hashlib
baseline=Path(__file__).with_name('20261011-sound-shared-context-provider-callers.py')
source=baseline.read_text(encoding='utf-8')
assert source.count('targets={0x99f148,0xa7900c,0xa79010}')==1
source=source.replace('targets={0x99f148,0xa7900c,0xa79010}','targets={0xa3b84c,0xa3ba44,0xa3b434}')
source=source.replace('[0x9ec634,0xa51710,0xa6c2c0,0xa6c2c4]','[0xa3b84c,0xa3ba44,0xa3b434]')
print('DECODER SHA256',hashlib.sha256(baseline.read_bytes()).hexdigest())
exec(compile(source,str(baseline),'exec'),{'__file__':str(Path(__file__).resolve())})
