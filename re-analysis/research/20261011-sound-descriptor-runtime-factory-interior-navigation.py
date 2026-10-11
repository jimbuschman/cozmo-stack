"""Original A3B684/A3B694 factory interior inputs, immediate/raw navigation only."""
from pathlib import Path
import hashlib
baseline=Path(__file__).with_name('20261011-sound-shared-context-provider-callers.py')
source=baseline.read_text(encoding='utf-8')
assert source.count('targets={0x99f148,0xa7900c,0xa79010}')==1
source=source.replace('targets={0x99f148,0xa7900c,0xa79010}','targets=set(range(0xa3b684,0xa3b734,4))')
source=source.replace('[0x9ec634,0xa51710,0xa6c2c0,0xa6c2c4]','sorted(targets)')
source=source.replace('print(json.dumps(out,indent=2))',"print('branches',len(out),'external sources',sum(not 0xa3b684<=int(x['address'],16)<0xa3b734 for x in out),'raw pointers',len(refs))")
print('DECODER SHA256',hashlib.sha256(baseline.read_bytes()).hexdigest())
exec(compile(source,str(baseline),'exec'),{'__file__':str(Path(__file__).resolve())})
