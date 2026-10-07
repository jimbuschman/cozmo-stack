"""Produce Q19's evidence-led report; writes research files only."""
from pathlib import Path
import json, importlib.util, struct
root=Path('re-analysis/research')
s=importlib.util.spec_from_file_location('native',root/'20261007-queue5-native.py')
m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
tables={}
for name,a,n in [('ProbePoints_X',0xDC7F74,5),('ProbePoints_Y',0xDC7F7E,5),('ThresholdDarkProbe_X',0xDC7F8C,12),('ThresholdDarkProbe_Y',0xDC7FA4,12),('ThresholdBrightProbe_X',0xDC7FBC,12),('ThresholdBrightProbe_Y',0xDC7FD4,12),('ProbeCenters_X',0xDC7FEC,1024),('ProbeCenters_Y',0xDC87EC,1024)]:
 tables[name]={'address':f'0x{a:08X}','values_i16':list(struct.unpack('<'+'h'*n,m.read(a,n*2)))}
tables['neighbor_table']={'address':'0x01051064','pairs_u8':[list(m.read(0x1051064+2*i,2)) for i in range(16)]}
(root/'20261007-q19-marker-tables.json').write_text(json.dumps(tables,indent=2)+'\n',encoding='utf-8')
quotes='\n\n'.join('```json\n'+json.dumps({k:r[k] for k in ('id','title','status','authority','evidence','unresolved') if k in r},indent=2)+'\n```' for r in json.loads((root/'20261007-q19-current-records.json').read_text(encoding='utf-8')))
body=(root/'20261007-q19-report-body.md').read_text(encoding='utf-8')
body=body.replace('<!-- CURRENT_RECORD_QUOTES -->',quotes)
(root/'20261007-M11-M14-rows.md').write_text(body,encoding='utf-8')
