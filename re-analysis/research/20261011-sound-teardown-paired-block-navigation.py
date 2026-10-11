"""Original084/118 five-entry input: omitted paired/block constant transfers only."""
from pathlib import Path
import importlib.util,hashlib,json
p=Path(__file__).with_name('20261011-sound-paired-block-constant-model.py');s=importlib.util.spec_from_file_location('paired',p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
targets=(0xa0228c,0xa022e8,0xa370e4,0xa55a84,0xa55d04);ns={'__file__':str(p.resolve())}
print('ORIGINAL084/118',','.join(f'{x:08X}' for x in targets));print('MODEL SHA256',hashlib.sha256(p.read_bytes()).hexdigest());print('Extension over846: selected LDRD/LDM constant transfers. Paid scalar events are comparison baseline, not new recovery.')
exec(compile(m.build_source(targets),str(p),'exec'),ns)
result=dict(engine_sha256=hashlib.sha256(ns['n'].raw).hexdigest(),model_sha256=hashlib.sha256(p.read_bytes()).hexdigest(),targets=[f'{x:08X}' for x in targets],windows=ns['seeds'],events=list(ns['events'].values()),boundary='Original084/118 selected paired/block constant transfer extension over846. Mutable values remain unknown. No stack/copy/computed/foreign/capped input exclusion, real callback admission, or new teardown/sample/storage body execution.')
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
