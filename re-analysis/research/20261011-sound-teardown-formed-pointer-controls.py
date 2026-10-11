"""Positive navigation controls: only existing761 and760 primary pointer seams."""
from pathlib import Path
import json,hashlib
source=Path(__file__).with_name('20261010-sound-weight-formed-pointer-navigation.py')
ns={'__file__':str(source.resolve())}
exec(compile(source.read_text(encoding='utf-8').split('for va,size,off in exec_sections:')[0],str(source),'exec'),ns)
controls=[]
def capture(label,target,start,initial=None):
 ns['targets']=(target,);ns['events']={}
 ns['execute'](ns['n'].read(start,768),0,start,False,initial_env=initial)
 events=sorted(ns['events'].values(),key=lambda e:(e['address'],e['kind']))
 controls.append({'label':label,'target':f'{target:08X}','seed':f'{start:08X}','input_provenance':'Existing761 R0 argument handoff' if initial else 'Existing native return/load instructions','events':events})
 return events
x=capture('761 existing music-hook load/call',0x984930,0x97D778)
assert any(e['address']=='0097D77C' and e['kind']=='formed-r0' for e in x)
assert any(e['address']=='0097D780' and e['kind']=='live-r0-at-call' for e in x)
x=capture('761 existing supplied hook pointer publication',0x984930,0x9A5E64,{ns['ARM_REG_R0']:0x984930})
assert any(e['address']=='009A5E6C' and e['kind']=='possible-stored-pointer-value' for e in x)
x=capture('760 initialized shared-context return arm',0x108D8D0,0x99F164)
assert any(e['address']=='0099F170' and e['kind']=='possible-returned-pointer' for e in x)
result={'scope':'Existing760/761 positive controls only;not a teardown caller, input or Q15 completion.','source_model':source.name,'source_model_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'controls':controls}
Path(__file__).with_suffix('.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('All3 primary pointer-observation controls passed.')
