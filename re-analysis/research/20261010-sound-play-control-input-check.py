"""Q14-088/121 independent repeated Play candidates joined to raw common control prefixes. Stop before positioning payloads."""
from pathlib import Path
import json,zipfile,struct,hashlib,collections
r=Path(__file__).parent
nav=json.loads((r/'20261010-sound-bus-ancestor-input-witness.json').read_text(encoding='utf-8'))
play=json.loads((r/'20261010-sound-play-branch-input-witness.json').read_text(encoding='utf-8'))
paths=play['candidatePaths'];midi=json.loads((r/'20261010-sound-midi-branch-target-witness.json').read_text(encoding='utf-8'))['candidatePaths'];rows=[]
with zipfile.ZipFile(r.parents[2]/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip') as z:
 banks={x['bank']:z.read(x['bank']) for x in nav['banks']}
 for x in nav['rows']:
  if x['routeKind']!='node-common':continue
  b=banks[x['bank']];start=x['payloadOffset'];end=start+x['payloadLength'];raw=b[start:end]
  assert hashlib.sha256(raw).hexdigest()==x['payloadSha256']
  assert b[x['commonOffset']:x['commonEnd']].hex()==x['rawHex']
  p=x['commonEnd'];count=b[p];p+=1;ids=list(b[p:p+count]);p+=count;properties=[]
  for key in ids:
   assert p+4<=end;properties.append(dict(id=key,offset=p,rawHex=b[p:p+4].hex()));p+=4
  assert p<end;count=b[p];p+=1;ids=list(b[p:p+count]);p+=count;ranges=[]
  for key in ids:
   assert p+8<=end;ranges.append(dict(id=key,offset=p,rawHex=b[p:p+8].hex()));p+=8
  assert p<end;flags=b[p]
  rows.append(dict(bank=x['bank'],id=x['objectId'],type=x['objectType'],payloadOffset=start,payloadSha256=x['payloadSha256'],commonOffset=x['commonOffset'],commonEnd=x['commonEnd'],priorityApply=x['extraByte'],priorityFlags=x['flags'],properties=properties,ranges=ranges,positionFlagOffset=p,positionFlags=flags,candidatePath=paths.get(str(x['objectId'])),midiCandidatePath=midi.get(str(x['objectId']))))
counts=lambda xs:dict(collections.Counter(xs))
candidates=[x for x in rows if x['candidatePath']]
out=dict(scope='All3490 primary node prefixes; repeated finite ordinary Play and authored MIDI-target candidate paths. Raw control flags/properties/ranges read before positioning payload. No runtime property/attachment immutability or gate-success claim.',nodeCount=len(rows),candidateNodeCount=len(candidates),candidateTypes=counts(x['type'] for x in candidates),candidatePriorityApply=counts(x['priorityApply'] for x in candidates),candidatePriorityFlags=counts(x['priorityFlags'] for x in candidates),candidatePositionFlags=counts(x['positionFlags'] for x in candidates),candidatePriorityProperties=counts(v['id'] for x in candidates for v in x['properties'] if v['id'] in (7,8)),candidatePriorityRanges=counts(v['id'] for x in candidates for v in x['ranges'] if v['id'] in (7,8)),rows=rows)
union=[x for x in rows if x['candidatePath'] or x['midiCandidatePath']]
assert sum(bool(x['midiCandidatePath']) for x in rows)==200
out.update(unionNodeCount=len(union),midiCandidateCount=200,unionPriorityFlags=counts(x['priorityFlags'] for x in union),unionPositionFlags=counts(x['positionFlags'] for x in union),distancePriorityEnabled=[x['id'] for x in union if x['priorityFlags']&2])
(r/'20261010-sound-play-control-input-witness.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in out.items() if k!='rows'},indent=2))
