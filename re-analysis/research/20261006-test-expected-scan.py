"""Candidate discovery only: expected-expression provenance still needs manual review."""
import json,re
from pathlib import Path
rs=json.loads(Path('re-analysis/research/20261006-M3-M5-test-records.json').read_text(encoding='utf-8'))
files={Path(r['location']) for r in rs};types={};methods={}
for p in files:
 if not p.exists():continue
 s=p.read_text(encoding='utf-8')
 for typ in re.findall(r'\b(?:class|struct|enum)\s+(\w+)',s): types[typ]=str(p)
root=Path('cozmo-stack/tests/Cozmo.Protocol.Tests')
candidates=[];stats=[]
for p in root.glob('*.cs'):
 if p.name.endswith('Oracle.cs'):continue
 s=p.read_text(encoding='utf-8')
 if not (re.search(r'M[345][-_]\d{3}',s) or any(re.search(r'\b'+re.escape(t)+r'\b',s) for t in types)):continue
 eq=list(re.finditer(r'Assert\.(Equal|Same|InRange)\s*\(',s));stats.append((p.name,len(eq)))
 for match in eq:
  start=match.end();depth=0;quote=None;esc=False;j=start
  while j<len(s):
   ch=s[j]
   if quote:
    if esc:esc=False
    elif ch=='\\':esc=True
    elif ch==quote:quote=None
   elif ch in '\"\'':quote=ch
   elif ch in '([{':depth+=1
   elif ch in ')]}':
    if depth==0:break
    depth-=1
   elif ch==',' and depth==0:break
   j+=1
  expr=s[start:j].strip();line=s.count('\n',0,match.start())+1
  hits=[t for t in types if re.search(r'\b'+re.escape(t)+r'\s*\.',expr)]
  if hits:candidates.append({'file':str(p).replace('\\','/'),'line':line,'expected':expr,'types':hits})
Path('re-analysis/research/20261006-Q7-candidates.json').write_text(json.dumps(candidates,indent=2),encoding='utf-8')
Path('re-analysis/research/20261006-Q7-scan.json').write_text(json.dumps({'types':types,'files':stats,'assertions':sum(n for _,n in stats)},indent=2),encoding='utf-8')
for row in candidates:print(Path(row['file']).name+':'+str(row['line']),row['expected'].replace('\n',' '))
print('FILES',len(stats),'ASSERTIONS',sum(n for _,n in stats),'CANDIDATES',len(candidates))
