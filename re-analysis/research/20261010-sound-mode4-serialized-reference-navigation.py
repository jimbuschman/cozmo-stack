"""Two original Q14 mode4 nodes only; raw HIRC byte hits are navigation, not runtime admission."""
from pathlib import Path
import zipfile,struct,json,hashlib
root=Path(__file__).resolve().parents[2];r=Path(__file__).parent
archive=root.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
lead=json.loads((r/'20261007-sound-reachability-assets.json').read_text(encoding='utf-8'))
nav={(x['bank'],x['id']):x for x in lead['objects']};targets=(725225627,196431345);out=[];banks=[];total=0
with zipfile.ZipFile(archive) as z:
 for name in sorted(n for n in z.namelist() if n.endswith('.bnk')):
  b=z.read(name);p=0;count=0
  while p<len(b):
   assert p+8<=len(b);length=struct.unpack_from('<I',b,p+4)[0];end=p+8+length;assert end<=len(b)
   if b[p:p+4]==b'HIRC':
    count=struct.unpack_from('<I',b,p+8)[0];q=p+12
    for ordinal in range(count):
     typ=b[q];n=struct.unpack_from('<I',b,q+1)[0];start=q+5;finish=start+n;assert finish<=end and n>=4
     oid=struct.unpack_from('<I',b,start)[0];payload=b[start:finish];old=nav[(name,oid)]
     digest=hashlib.sha256(payload).hexdigest();assert old['type']==typ and old['payloadOffset']==start and old['length']==n and old['sha256']==digest
     for target in targets:
      at=payload.find(struct.pack('<I',target))
      while at>=0:
       role='raw unmatched field';absolute=start+at
       if at==0:role='own object ID'
       childbase=old.get('childrenOffset');children=old.get('children',[])
       if childbase is not None and childbase+4<=absolute<childbase+4+4*len(children) and (absolute-childbase-4)%4==0:
        index=(absolute-childbase-4)//4;assert children[index]==target;role='previously checked child-vector ID'
       if typ==3 and at==6:role='action target'
       out.append(dict(bank=name,object_id=oid,type=typ,payload_sha256=digest,target=target,relative_offset=at,absolute_offset=absolute,role=role,context_offset=max(0,at-12),context_hex=payload[max(0,at-12):at+16].hex()))
       at=payload.find(struct.pack('<I',target),at+1)
     total+=1;q=finish
    assert q==end
   p=end
  assert p==len(b);banks.append(dict(bank=name,size=len(b),sha256=hashlib.sha256(b).hexdigest(),hirc_count=count))
assert total==len(nav)
result=dict(scope='All six banks, all HIRC payload bytes at every alignment, exactly two original mode4 IDs. No payload exclusion is decoded or expanded; hits require primary reader-field attribution. No runtime mutable/indirect admission or unreachability proof.',objects=total,banks=banks,hits=out)
p=Path(__file__).with_suffix('.json');p.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print('objects',total,'hits',len(out));print(json.dumps(out,indent=2))
