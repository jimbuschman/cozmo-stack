"""Original descriptor population input pointers A3B684/A3B694 only.
No registry/hash/DSP bodies or all-caller absence claim.
"""
from pathlib import Path
import hashlib
baseline=Path(__file__).with_name('20261010-sound-weight-formed-pointer-navigation.py')
source=baseline.read_text(encoding='utf-8')
assert source.count('targets=(0xA08024,)')==1
source=source.replace('targets=(0xA08024,)','targets=(0xA3B684,0xA3B694)')
source=source.replace('one original weight-setter input','original descriptor registration/factory inputs')
print('BASELINE MODEL SHA256',hashlib.sha256(baseline.read_bytes()).hexdigest())
print('TARGETS A3B684 explicit registration, A3B694 ID/type factory')
exec(compile(source,str(baseline),'exec'),{'__file__':str(Path(__file__).resolve())})
