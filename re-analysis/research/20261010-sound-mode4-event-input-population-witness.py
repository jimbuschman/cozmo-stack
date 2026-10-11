"""Two original mode4 IDs versus every shipped Event own-ID; no arbitrary registry/input absence."""
from pathlib import Path
import json,zipfile,struct,hashlib,collections
b=Path(__file__).resolve().parent;root=b.parents[1]
nav=json.loads((b/'20261007-sound-reachability-assets.json').read_text(encoding='utf-8'))
lookup={(o['bank'],o['id']):o for o in nav['objects']};targets={725225627,196431345};rows=[];events=[];totals={};banks=[];objects=0
with zipfile.ZipFile(root.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip') as z:
 for name in sorted(n for n in z.namelist() if n.endswith('.bnk')):
  data=z.read(name);p=0;totals[name]=0
  while p<len(data):
   assert p+8<=len(data);size=struct.unpack_from('<I',data,p+4)[0];end=p+8+size;assert end<=len(data)
   if data[p:p+4]==b'HIRC':
    count=struct.unpack_from('<I',data,p+8)[0];q=p+12
    for k in range(count):
     typ=data[q];length=struct.unpack_from('<I',data,q+1)[0];at=q+5;finish=at+length;assert finish<=end and length>=4
     payload=data[at:finish];oid=struct.unpack_from('<I',payload)[0];old=lookup[name,oid];digest=hashlib.sha256(payload).hexdigest()
     assert old['type']==typ and old['payloadOffset']==at and old['length']==length and old['sha256']==digest
     if typ==4 or oid in targets:
      r={'bank':name,'type':typ,'id':oid,'payloadOffset':at,'payloadSHA256':digest}
      if typ==4:
       number=struct.unpack_from('<I',payload,4)[0];assert len(payload)==8+4*number
       r['orderedActionIDs']=[struct.unpack_from('<I',payload,8+4*k)[0] for k in range(number)];events.append(r);totals[name]+=1
      else:rows.append(r)
     objects+=1;q=finish
    assert q==end
   p=end
  assert p==len(data);banks.append({'bank':name,'SHA256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})
assert objects==len(lookup)==5350
assert {o['id'] for o in rows}==targets and all(o['type']==5 for o in rows)
assert not targets&{o['id'] for o in events}
r={'scope':'Exactly two original mode4 IDs versus every authored type4 Event own-ID in six banks. All5350 payload hashes/extents and Event/action order bytes verified; no runtime mutation/alternate registry publication or unreachability proof.','banks':banks,'objects':objects,'targets':sorted(targets),'targetObjects':rows,'eventCount':len(events),'countsByBank':totals,'events':events,'matchingEventOwnIDs':[]}
(b/'20261010-sound-mode4-event-input-population-witness.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8',newline='\n')
print('objects',objects,'events',len(events),'bybank',totals,'mode4matches',r['matchingEventOwnIDs'])
