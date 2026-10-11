"""Original factory/key inputs; new paired/block constant-load navigation only.
No actual admission or absence certification. Do not repeat paid scalar-only scans.
"""
from pathlib import Path
import importlib.util,hashlib
path=Path(__file__).with_name('20261011-sound-paired-block-constant-model.py')
spec=importlib.util.spec_from_file_location('paired_model',path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
targets=(0xA3B684,0xA3B694,*range(0x108D9A0,0x108D9B0))
print('PAIRED/BLOCK MODEL SHA256',hashlib.sha256(path.read_bytes()).hexdigest())
print('BASELINE MODEL SHA256',m.BASELINE_SHA)
print('TARGETS',','.join(f'{x:08X}' for x in targets))
print('EXTENSION: unindexed/no-writeback LDRD and LDM IA/IB/DB; constant GOT/literal words only; mutable data stays unknown; DA and unpredictable/user-bank forms unsupported. One native instruction controls separate.')
exec(compile(m.build_source(targets),str(path),'exec'),{'__file__':str(path.resolve())})
