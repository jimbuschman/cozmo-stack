"""Q14-109/134 exact four-word bank-header key range, repaired finite pointer model.
No new DSP, backing or helper-body extraction; no caller absence proof.
"""
from pathlib import Path
import hashlib
baseline=Path(__file__).with_name('20261010-sound-weight-formed-pointer-navigation.py')
source=baseline.read_text(encoding='utf-8')
assert source.count('targets=(0xA08024,)')==1
source=source.replace('targets=(0xA08024,)','targets=tuple(range(0x108D9A0,0x108D9B0))')
source=source.replace('Q14-008 original weight-setter input','Q14-109/134 original bank-header key inputs').replace('one original weight-setter input','original bank-header key bytes')
print('BASELINE MODEL SHA256',hashlib.sha256(baseline.read_bytes()).hexdigest())
print('TARGET BYTE RANGE 108D9A0..108D9B0 exclusive; four header-key words')
exec(compile(source,str(baseline),'exec'),{'__file__':str(Path(__file__).resolve())})
