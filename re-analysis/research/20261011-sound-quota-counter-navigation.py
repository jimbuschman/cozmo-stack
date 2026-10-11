"""Original037 exact process counters/quota/priority: selected constant-origin transfers."""
from pathlib import Path
import importlib.util,hashlib,json
p=Path(__file__).with_name('20261011-sound-paired-block-constant-model.py');s=importlib.util.spec_from_file_location('paired',p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
fields={'global_virtual':(0x108de78,4),'global_PBI_count':(0x108dec0,4),'default_quota':(0x108dea4,2),'default_active':(0x108deb8,2),'default_virtual':(0x108deba,2),'quota_priority':(0x1052420,4)}
targets=tuple(a+i for a,size in fields.values() for i in range(size))
ns={'__file__':str(p.resolve())}
print('ORIGINAL037 FIELDS',json.dumps(fields));print('MODEL SHA256',hashlib.sha256(p.read_bytes()).hexdigest())
exec(compile(m.build_source(targets),str(p),'exec'),ns)
result=dict(engine_sha256=hashlib.sha256(ns['n'].raw).hexdigest(),model_sha256=hashlib.sha256(p.read_bytes()).hexdigest(),fields={k:dict(address=f'{a:08X}',size=z) for k,(a,z) in fields.items()},windows=ns['seeds'],events=list(ns['events'].values()),boundary='Original037 selected scalar/paired/block/NEON constant-origin read/write/address navigation. Mutable values remain unknown. No all-writer/copy/stack/computed/foreign/capped-window exclusion, actual quota setter admission, or policy/collection recipient census.')
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
