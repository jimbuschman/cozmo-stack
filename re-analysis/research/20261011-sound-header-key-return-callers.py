"""Original key-address carrier at9B21F4 return; direct/raw input navigation only.
No caller absence, runtime key-value or arbitrary escaped-alias certification.
"""
from pathlib import Path
import hashlib
baseline=Path(__file__).with_name('20261011-sound-shared-context-provider-callers.py')
assert hashlib.sha256(baseline.read_bytes()).hexdigest()=='cc16862765c8337dd7f86682fe0ce5d5cf7a9954f56ccd6907152633ce9a4b55'
source=baseline.read_text(encoding='utf-8')
source=source.replace('targets={0x99f148,0xa7900c,0xa79010}','targets={0x9b21f4}')
source=source.replace('[0x9ec634,0xa51710,0xa6c2c0,0xa6c2c4]','[0x9b21f4,0x9b21f5]')
assert 'targets={0x9b21f4}' in source
exec(compile(source,str(baseline),'exec'),{'__file__':str(Path(__file__).resolve())})
