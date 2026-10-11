"""Q14-088/121 repeat finite Play-input census from primary bank bytes.
Candidate child offsets are navigation from the earlier census; full hashes and exact
serialized vectors are independently verified. Closure is structural, not gate success.
"""
from pathlib import Path
import zipfile,struct,json,hashlib,collections
root=Path(__file__).resolve().parents[2];r=Path(__file__).parent
archive=root.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
lead=json.loads((r/'20261007-sound-reachability-assets.json').read_text(encoding='utf-8'))
nav={(x['bank'],x['id']):x for x in lead['objects']};objects=[];banks=[]
with zipfile.ZipFile(archive) as z:
 for name in sorted(n for n in z.namelist() if n.endswith('.bnk')):
  b=z.read(name);p=0;count=0
  while p<len(b):
   assert p+8<=len(b);length=struct.unpack_from('<I',b,p+4)[0];end=p+8+length;assert end<=len(b)
   if b[p:p+4]==b'HIRC':
    count=struct.unpack_from('<I',b,p+8)[0];q=p+12
    for ordinal in range(count):
     typ=b[q];n=struct.unpack_from('<I',b,q+1)[0];start=q+5;finish=start+n;assert finish<=end and n>=4
     oid=struct.unpack_from('<I',b,start)[0];old=nav[(name,oid)];payload=b[start:finish]
     assert old['type']==typ and old['payloadOffset']==start and old['length']==n and old['sha256']==hashlib.sha256(payload).hexdigest()
     item=dict(bank=name,id=oid,type=typ,offset=q,payloadOffset=start,length=n,sha256=hashlib.sha256(payload).hexdigest())
     if typ==4:
      k=struct.unpack_from('<I',payload,4)[0];assert 8+4*k==n;item['actions']=list(struct.unpack_from('<'+'I'*k,payload,8))
     elif typ==3:item.update(actionType=struct.unpack_from('<H',payload,4)[0],target=struct.unpack_from('<I',payload,6)[0])
     if old.get('children'):
      children=old['children'];at=old['childrenOffset'];vec=struct.pack('<I',len(children))+struct.pack('<'+'I'*len(children),*children)
      assert b[at:at+len(vec)]==vec and start<=at and at+len(vec)<=finish
      item.update(children=children,childrenOffset=at)
     objects.append(item);q=finish
    assert q==end
   p=end
  assert p==len(b);banks.append(dict(bank=name,size=len(b),sha256=hashlib.sha256(b).hexdigest(),hircCount=count))
assert len(objects)==len(nav)
index=collections.defaultdict(list)
for x in objects:index[x['id']].append(x)
assert all(len({x['sha256'] for x in v})==1 for v in index.values()), "different payload for shared ID requires explicit variant graph"
ix={k:v[0] for k,v in index.items()};roots=[];missing=[]
for event in (x for x in objects if x['type']==4):
 for ordinal,aid in enumerate(event['actions']):
  action=ix.get(aid)
  if action is None:missing.append(dict(event=event['id'],action=aid));continue
  assert action['type']==3
  if action['actionType']==0x403:roots.append(dict(bank=event['bank'],event=event['id'],actionOrdinal=ordinal,action=aid,target=action['target']))
# Child expansion is only the four known executable ordinary container types.
# ActorMixer Play is no-op; music entries retain opaque higher-layer interfaces.
paths={};queue=[(x['target'],[x['event'],x['action'],x['target']]) for x in roots]
while queue:
 oid,path=queue.pop(0)
 if oid in paths:continue
 paths[oid]=path
 obj=ix.get(oid)
 if obj is not None and obj['type'] in (5,6,9):
  queue.extend((cid,path+[cid]) for cid in obj.get('children',[]))
out=dict(scope='Finite supplied Event/action graph plus verified serialized child vectors. Candidate paths preserve runtime selection/filter/registry uncertainty; ActorMixer/no-op and music boundaries are not expanded. MIDI direct submission is separately MP646/TI590, not inferred from these Play roots.',banks=banks,objectCount=len(objects),duplicateIdenticalIds=sorted(k for k,v in index.items() if len(v)>1),playActions=roots,missingActions=missing,candidatePaths={str(k):v for k,v in paths.items()},candidateTypes=dict(collections.Counter(ix[k]['type'] for k in paths if k in ix)),unresolvedTargets=sorted(k for k in paths if k not in ix),mode4Candidates={str(k):paths.get(k) for k in (725225627,196431345)})
(r/'20261010-sound-play-branch-input-witness.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in out.items() if k not in ('playActions','candidatePaths','banks')},indent=2));print('Play roots',len(roots),'candidate nodes',len(paths))
