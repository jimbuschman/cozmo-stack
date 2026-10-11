"""Primary bank byte attribution for exactly13 mode4 ID hits, using TR6/8, CF3/NB2, TR16 and PB647 checked vectors."""
from pathlib import Path
import zipfile,struct,json,hashlib,collections
r=Path(__file__).parent;root=r.parents[1];archive=root.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
nav=json.loads((r/'20261007-sound-reachability-assets.json').read_text(encoding='utf-8'));objects={(x['bank'],x['id']):x for x in nav['objects']}
hits=json.loads((r/'20261010-sound-mode4-serialized-reference-navigation.json').read_text())['hits'];out=[]
with zipfile.ZipFile(archive) as z:
 banks={name:z.read(name) for name in {x['bank'] for x in hits}}
 for hit in hits:
  obj=objects[(hit['bank'],hit['object_id'])];b=banks[hit['bank']];base=obj['payloadOffset'];payload=b[base:base+obj['length']];assert hashlib.sha256(payload).hexdigest()==hit['payload_sha256']
  rel=hit['relative_offset'];target=hit['target'];assert struct.unpack_from('<I',payload,rel)[0]==target
  role=hit['role'];entry=dict(hit)
  if role=='raw unmatched field':
   common=18 if obj['type']==2 else 4
   if obj['type'] in (2,5) and rel==common+7:
    # TR6/8 Sound prefix14; CF3 count0 consumes2; NB2 flag+two U32 IDs consumes9.
    assert payload[common:common+3]==bytes(3)
    assert struct.unpack_from('<I',payload,common+3)[0]==0
    role='NB2 second parent ID (selector0)';entry.update(common_offset=common,fx_inheritance=0,fx_count=0,link_flag=0,first_parent_id=0)
   elif obj['id']==725225627 and rel==78:
    at=obj['childrenOffset'];children=obj['children'];assert struct.unpack_from('<I',b,at)[0]==len(children)
    assert list(struct.unpack_from('<'+'I'*len(children),b,at+4))==children
    playlist=at+4+4*len(children);count=struct.unpack_from('<H',b,playlist)[0];assert count==2
    pairs=[struct.unpack_from('<II',b,playlist+2+8*i) for i in range(count)]
    assert pairs==[(990835622,50000),(196431345,50000)]
    assert base+rel==playlist+2+8
    role='TR16 playlist second entry ID';entry.update(playlist_absolute_offset=playlist,playlist_count=count,playlist_pairs=pairs)
   else:raise AssertionError(hit)
  entry['primary_role']=role;out.append(entry)
counts=dict(collections.Counter(x['primary_role'] for x in out));assert counts=={'NB2 second parent ID (selector0)':8,'own object ID':2,'previously checked child-vector ID':2,'TR16 playlist second entry ID':1}
Path(__file__).with_suffix('.json').write_text(json.dumps(dict(counts=counts,rows=out,limit='All13 finite serialized matches attributed. Runtime mutation/admission and external inputs remain UNKNOWN; no unreachability proof.'),indent=2)+'\n')
print(counts)
