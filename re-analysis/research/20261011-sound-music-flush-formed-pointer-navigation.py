"""Q14-119 two exact music-flush entry targets, repaired finite pointer model.
No new DSP, backing or helper-body extraction; no caller absence proof.
"""
from pathlib import Path
import hashlib
baseline=Path(__file__).with_name('20261010-sound-weight-formed-pointer-navigation.py')
source=baseline.read_text(encoding='utf-8')
assert source.count('targets=(0xA08024,)')==1
source=source.replace('targets=(0xA08024,)','targets=(0xA71634,0xA716BC)')
source=source.replace('Q14-008 original weight-setter input','Q14-119 original teardown inputs').replace('one original weight-setter input','original music-flush inputs')
print('BASELINE MODEL SHA256',hashlib.sha256(baseline.read_bytes()).hexdigest())
print('TARGETS A71634 A716BC; ARM/Thumb mode-bit variants')
exec(compile(source,str(baseline),'exec'),{'__file__':str(Path(__file__).resolve())})
