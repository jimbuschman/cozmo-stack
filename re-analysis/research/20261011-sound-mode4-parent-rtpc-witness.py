"""Positive authored RTPC on the original mode4 outer node's parent only."""
from pathlib import Path
import hashlib, json, struct, zipfile
r=Path(__file__).resolve().parent
nav=json.loads((r/'20261007-sound-reachability-assets.json').read_text(encoding='utf-8-sig'))
archive=r.parents[1].parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest()==nav['archiveSha256']
o=next(o for o in nav['objects'] if o['id']==62050212)
assert o['type']==7
with zipfile.ZipFile(archive) as z:b=z.read(o['bank'])
p=b[o['payloadOffset']:o['payloadOffset']+o['length']]
assert hashlib.sha256(p).hexdigest()==o['sha256']
assert struct.unpack_from('<I',p)[0]==62050212
# CF3 inheritance0, count1, bypass0, seven-byte slot/ID/share/rendered record.
assert p[4:8]==bytes.fromhex('00010000') and p[12:14]==bytes([1,0])
fx=struct.unpack_from('<I',p,8)[0]
assert p[14]==0 and struct.unpack_from('<I',p,19)[0]==0 and p[23]==0
bus=struct.unpack_from('<I',p,15)[0]
# CF1 count2, ID bytes, raw values; CF2 range count0.
assert p[24]==2 and p[25:27]==bytes([0,3]) and p[35]==0
props=[dict(id=p[25+i],raw=f'{struct.unpack_from("<I",p,27+4*i)[0]:08X}') for i in range(2)]
# Positioning C3: bit0/1 set, bit3 clear; no positioning payload consumed.
# Aux02: bit3 clear, no four-ID or gain trailer; advanced six-byte section.
assert p[36:38]==bytes.fromhex('c302') and p[38:44]==bytes.fromhex('040101000000')
assert struct.unpack_from('<I',p,44)[0]==0
assert struct.unpack_from('<H',p,48)[0]==1
source=struct.unpack_from('<I',p,50)[0]
assert p[54:57]==bytes([0,1,0]) # source type, accumulation, property ID
curve=struct.unpack_from('<I',p,57)[0]
assert p[61]==2 and struct.unpack_from('<H',p,62)[0]==2
points=[list(struct.unpack_from('<III',p,64+12*i)) for i in range(2)]
assert points==[[0,0xBF800000,1],[0x3F800000,0,4]]
assert o['payloadOffset']+88==o['childrenOffset']
n=struct.unpack_from('<I',p,88)[0]
children=list(struct.unpack_from('<'+'I'*n,p,92))
assert children==o['children'] and 725225627 in children and 92+4*n==len(p)
out=dict(id=o['id'],bank=o['bank'],payloadOffset=o['payloadOffset'],payloadSha256=o['sha256'],
         commonPrefixHex=p[:88].hex(),fxID=fx,firstParentBusID=bus,secondParentID=0,
         properties=props,stateGroupCount=0,rtpcCount=1,rtpc=dict(sourceID=source,sourceHex=f'{source:08X}',
         type=0,accumulation=1,propertyID=0,curveID=curve,scaling=2,
         pointWords=[[f'{v:08X}' for v in q] for q in points]),childCount=n,
         outerMode4ChildIndex=children.index(725225627),
         boundary='Positive authored parent binding only; no assertion of actual RTPC request, inherited recipient admission or inability of all runtime callbacks to start playback.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(json.dumps(out,indent=2))
