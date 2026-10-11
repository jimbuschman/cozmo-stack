"""Q14-084/118 five exact teardown entry targets, repaired finite pointer model.
No new DSP, backing or helper-body extraction; no caller absence proof.
"""
from pathlib import Path
import hashlib
baseline=Path(__file__).with_name('20261010-sound-weight-formed-pointer-navigation.py')
source=baseline.read_text(encoding='utf-8')
assert source.count('targets=(0xA08024,)')==1
source=source.replace('targets=(0xA08024,)','targets=(0xA0228C,0xA022E8,0xA370E4,0xA55A84,0xA55D04)')
source=source.replace('Q14-008 original weight-setter input','Q14-084/118 original teardown inputs').replace('one original weight-setter input','five original teardown inputs')
print('BASELINE MODEL SHA256',hashlib.sha256(baseline.read_bytes()).hexdigest())
print('TARGETS A0228C A022E8 A370E4 A55A84 A55D04; ARM/Thumb mode-bit variants')
exec(compile(source,str(baseline),'exec'),{'__file__':str(Path(__file__).resolve())})
