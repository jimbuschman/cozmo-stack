"""Original009/034 alternate Event append: all shipped Actions, not only Event references."""
from pathlib import Path
import json,zipfile,struct,hashlib,collections
r=Path(__file__).parent;root=r.resolve().parents[1];archive=root.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
nav=json.loads((r/'20261007-sound-reachability-assets.json').read_text(encoding='utf-8'));lookup={(x['bank'],x['id']):x for x in nav['objects']};old=json.loads((r/'20261010-sound-play-branch-input-witness.json').read_text(encoding='utf-8'))
rows=[];banks=[];objects=0;events=[];mode4={725225627,196431345}
with zipfile.ZipFile(archive) as z:
 for name in sorted(n for n in z.namelist() if n.endswith('.bnk')):
  b=z.read(name);p=0
  while p<len(b):
   assert p+8<=len(b);size=struct.unpack_from('<I',b,p+4)[0];end=p+8+size;assert end<=len(b)
   if b[p:p+4]==b'HIRC':
    count=struct.unpack_from('<I',b,p+8)[0];q=p+12
    for ordinal in range(count):
     typ=b[q];length=struct.unpack_from('<I',b,q+1)[0];at=q+5;finish=at+length;assert length>=4 and finish<=end
     payload=b[at:finish];oid=struct.unpack_from('<I',payload)[0];lead=lookup[name,oid];digest=hashlib.sha256(payload).hexdigest();assert lead['type']==typ and lead['payloadOffset']==at and lead['length']==length and lead['sha256']==digest
     if typ==3:
      assert length>=11;fulltype=struct.unpack_from('<H',payload,4)[0];target=struct.unpack_from('<I',payload,6)[0]
      rows.append(dict(bank=name,id=oid,fulltype=f'{fulltype:04X}',target=target,raw_target_selector=payload[10],normalized_target_selector=int(payload[10]!=0),payloadOffset=at,bytes=length,payloadSHA256=digest))
     if typ==4:
      number=struct.unpack_from('<I',payload,4)[0];assert length==8+4*number;events.append(dict(id=oid,actions=list(struct.unpack_from('<'+'I'*number,payload,8))))
     objects+=1;q=finish
    assert q==end
   p=end
  assert p==len(b);banks.append(dict(bank=name,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
assert objects==len(lookup)==5350 and len(rows)==906 and len({x['id'] for x in rows})==906
plays={x['id']:x['target'] for x in rows if x['fulltype']=='0403'};previous={x['action']:x['target'] for x in old['playActions']};assert len(plays)==723 and plays==previous
assert not any(x['target'] in mode4 for x in rows)
action_ids={x['id'] for x in rows};assert all(set(x['actions'])<=action_ids for x in events)
out=dict(archive_sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),banks=banks,objects=objects,action_count=len(rows),event_count=len(events),fulltype_counts=dict(collections.Counter(x['fulltype'] for x in rows)),raw_target_selector_counts=dict(collections.Counter(str(x['raw_target_selector']) for x in rows)),all_play_actions=len(plays),prior_Event_Play_actions=len(previous),additional_Play_actions=[],matching_mode4_action_targets=[],actions=rows,boundary='All six bank HIRC payloads/extents independently verified. All723 fully authored Play actions match PB647 Event-root ID/target map. Under canonical fully read bank-only Action population and unchanged node graph, appending one of these Actions introduces no additional Play root. Not actual append admission, publication success, complete branch/music execution, immutability, partial/concurrent read, malformed alias/cycle or nonbank/alternate-action exclusion.')
Path(__file__).with_suffix('.json').write_bytes((json.dumps(out,indent=2)+'\n').encode());print('5350 objects,906 Actions,723 Play actions; no additional Play root or mode4 action target')
