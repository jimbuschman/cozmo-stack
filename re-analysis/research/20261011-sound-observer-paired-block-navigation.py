"""Original028/029 observer inputs: paired/block transfers omitted by848."""
from pathlib import Path
import importlib.util,hashlib,json
p=Path(__file__).with_name('20261011-sound-paired-block-constant-model.py');s=importlib.util.spec_from_file_location('paired',p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
targets=(0x9a03dc,0x9a040c);ns={'__file__':str(p.resolve())}
print('ORIGINAL028/029',','.join(f'{x:08X}' for x in targets));print('MODEL SHA256',hashlib.sha256(p.read_bytes()).hexdigest());print('Extension over848: selected LDRD/LDM constant transfers. Paid scalar scans/registration/filter bodies are not new recovery.')
exec(compile(m.build_source(targets),str(p),'exec'),ns)
result=dict(engine_sha256=hashlib.sha256(ns['n'].raw).hexdigest(),model_sha256=hashlib.sha256(p.read_bytes()).hexdigest(),targets=[f'{x:08X}' for x in targets],windows=ns['seeds'],events=list(ns['events'].values()),boundary='Original028/029 selected paired/block constant transfer extension over848. Mutable values remain unknown. No stack/copy/computed/foreign/capped input exclusion, actual key/callback/rawthirdword admission or callback/registration/sample/storage body execution.')
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
