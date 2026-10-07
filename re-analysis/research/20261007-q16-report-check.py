"""Report consistency only; not a behavioral fidelity test or expected-output oracle."""
from pathlib import Path
import re,json
p=Path(__file__).with_name('20261007-M10-M15-rows.md')
s=p.read_text(encoding='utf-8')
assert s.startswith('| Q16 item |')
coverage=s.split('\n\n',1)[0]
rows=re.findall(r'^\| (M(?:10|15)-\d+) \| (CHECKED|PARTIAL|NOT DONE) \|',coverage,re.M)
manifest=json.loads((p.parent.parent/'fidelity_manifest.json').read_text(encoding='utf-8'))
expected={r['id'] for r in manifest['records'] if r['subsystem'] in ('M10-derived','M15-freeplay') and r['status']!='EXACT_SOURCE'}
assert len(rows)==len({r[0] for r in rows}) and {r[0] for r in rows}==expected,(expected-{r[0] for r in rows},{r[0] for r in rows}-expected)
assert all(r[1]!='NOT DONE' for r in rows)
quoted=set(re.findall(r'^> \{"id": "(M\d+-\d+)"',s,re.M))
assert {r[0] for r in rows}<=quoted
current={r['id']:r for r in manifest['records']}
for line in s.splitlines():
 if line.startswith('> {'):
  q=json.loads(line[2:])
  assert all(current[q['id']][k]==v for k,v in q.items()),q['id']
files=set(re.findall(r'`(20261007-[^`]+\.(?:txt|py))`',s))
missing=[name for name in sorted(files) if not p.with_name(name).exists()]
assert not missing,missing
print(f'{len(rows)} unique coverage records; match current non-EXACT M10/M15 manifest; all quoted; no NOT DONE; explicit companion names exist.')
print('Coverage:', {v:sum(x[1]==v for x in rows) for v in ('CHECKED','PARTIAL','NOT DONE')})
print('This checks report structure only. It does not validate native behavior or approve a manifest.')
