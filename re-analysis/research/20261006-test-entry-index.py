from pathlib import Path
import re,json
rs=json.loads(Path('re-analysis/research/20261006-M3-M5-test-records.json').read_text(encoding='utf-8'))
all={}
for p in Path('cozmo-stack/tests/Cozmo.Protocol.Tests').glob('*.cs'):
 s=p.read_text(encoding='utf-8');clean=re.sub(r'"""[\s\S]*?"""|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/',lambda m:''.join('\n' if c=='\n' else ' ' for c in m.group()),s)
 for m in re.finditer(r'public\s+(?:async\s+)?(?:void|Task(?:<[^>]+>)?)\s+(\w+)\s*\([^;{}]*?\)\s*(\{|=>)',clean):
  pos=m.end();end=clean.find(';',pos)+1
  if m.group(2)=='{':
   d=1;j=pos
   while j<len(clean) and d:
    if clean[j]=='{':d+=1
    elif clean[j]=='}':d-=1
    j+=1
   end=j
  all.setdefault(m.group(1),[]).append({'file':str(p).replace('\\','/'),'line':s.count('\n',0,m.start())+1,'body':s[m.start():end]})
out=[]
for r in rs:
 names=set(n for _,n in re.findall(r'(\w+Tests)\.(\w+)',r.get('test') or ''))
 names.update(n for n in all if r['id'].replace('-','_') in n)
 found=[x|{'method':n} for n in sorted(names) for x in all.get(n,[])]
 routes=[]
 for x in found:
  b=x['body'];routes.append({k:x[k] for k in ['file','line','method']}|{'rig':bool(re.search(r'(new Rig\(|new AppRig\(|CreateForTest\(|rig\.)',b)),'entry_tokens':[t for t in ['rig.Data','rig.Send','rig.Tick','Engine.Tick','OnMessage','HandleMessage','EngineUpdate','.Advance(','.Render(','.OnChunk(','.ToFrames(','.Update('] if t in b]})
 out.append({'id':r['id'],'title':r['title'],'location':r['location'],'evidence':r['evidence'],'declared_test':r.get('test'),'tests':routes})
Path('re-analysis/research/20261006-test-entry-index.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
for r in out:
 print(r['id'],len(r['tests']),'RIG',sum(x['rig'] for x in r['tests']),' '.join(f"{Path(x['file']).name}:{x['line']}" for x in r['tests'][:3]))
