"""Q14-008 original weight input only: new874 constant transfer forms.
No additional helper/body or global writer/caller absence claim.
"""
from pathlib import Path
import importlib.util,hashlib
path=Path(__file__).with_name('20261011-sound-paired-block-constant-model.py')
spec=importlib.util.spec_from_file_location('paired_model',path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
print('MODEL SHA256',hashlib.sha256(path.read_bytes()).hexdigest())
print('BASELINE SHA256',m.BASELINE_SHA)
print('TARGET original A08024 node/childID/weight input; newly controlled constant LDRD/LDM IA/IB/DB only; original scalar seeds and finite bounds retained. Mutable data never becomes a constant.')
exec(compile(m.build_source((0xa08024,)),str(path),'exec'),{'__file__':str(path.resolve())})
