"""Primary prefix witnesses for existing admitted music-track population."""
from pathlib import Path
import hashlib,json,struct,zipfile,collections
root=Path(__file__).resolve().parents[2];out=root/'re-analysis/research'
meta=json.loads((out/'20261007-sound-reachability-assets.json').read_text(encoding='utf-8'))
objects={(o['bank'],o['id']):o for o in meta['objects']};banks={};rows={}
archive=zipfile.ZipFile(root.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip')
def parse(o):
    key=(o['bank'],o['id'])
    if key in rows:return rows[key]
    if o['bank'] not in banks:banks[o['bank']]=archive.read(o['bank'])
    bank=banks[o['bank']]
    raw=bank[o['payloadOffset']:o['payloadOffset']+o['length']]
    assert hashlib.sha256(raw).hexdigest()==o['sha256']
    assert struct.unpack_from('<I',raw)[0]==o['id']
    pos=5;sources=[]
    def u8():
        nonlocal pos
        v=raw[pos];pos+=1;return v
    def u32():
        nonlocal pos
        v=struct.unpack_from('<I',raw,pos)[0];pos+=4;return v
    if o['type']==11:
        for _ in range(u32()):
            at=pos;plugin=u32();mode=u8();media=u32();size=u32();flags=u8()
            if plugin&15 in (2,5):
                inlineLength=u32();pos+=inlineLength
            else:assert plugin&15 in (0,1)
            sources.append(dict(offset=at,plugin=plugin,mode=mode,media=media,size=size,flags=flags))
        n=u32();pos+=40*n
        if n:subtrackCount=u32()
        for _ in range(u32()):
            u32();u32();n=u32();pos+=12*n
    else:assert o['type'] in (10,12,13)
    common=pos;fxOverride=u8();fxCount=u8()
    if fxCount:u8();pos+=7*fxCount
    priorityApply=u8();bus=u32();parent=u32();priorityFlags=u8()
    count=u8();ids=list(raw[pos:pos+count]);pos+=count
    properties=[dict(id=i,bits=f'{u32():08X}') for i in ids]
    count=u8();ids=list(raw[pos:pos+count]);pos+=count
    ranges=[dict(id=i,low=f'{u32():08X}',high=f'{u32():08X}') for i in ids]
    positionOffset=pos;positionFlags=u8()
    assert positionFlags in (0,1,2,3,0xC0,0xC1,0xC2,0xC3), (o['id'],pos,positionFlags)
    row=dict(bank=o['bank'],id=o['id'],type=o['type'],payloadOffset=o['payloadOffset'],length=o['length'],sha256=o['sha256'],bankSha256=hashlib.sha256(bank).hexdigest(),prefixHex=raw[:pos].hex(),commonOffset=common,priorityApply=priorityApply,bus=bus,parent=parent,priorityFlags=priorityFlags,properties=properties,ranges=ranges,positionOffset=positionOffset,positionFlags=positionFlags,sources=sources)
    rows[key]=row
    if parent:parse(objects[(o['bank'],parent)])
    return row
for o in meta['objects']:
    if o['type']==11:parse(o)
(out/'20261010-sound-track-priority-witness.jsonl').write_text(''.join(json.dumps(r,separators=(',',':'))+'\n' for r in rows.values()),encoding='utf-8')
print('Primary music prefixes:',dict(collections.Counter(r['type'] for r in rows.values())))
print('Position flags:',dict(collections.Counter(r['positionFlags'] for r in rows.values())))
print('Priority flags:',dict(collections.Counter(r['priorityFlags'] for r in rows.values())))
print('Priority7/8:',[(r['id'],v) for r in rows.values() for v in r['properties'] if v['id'] in (7,8)])
