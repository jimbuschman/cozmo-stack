"""Raw bus helper input prefix, native9C6420/NB17-18/9C0D08; no inferred full schema."""
from pathlib import Path
import collections,hashlib,json,struct,zipfile

def main():
    index=json.loads(Path('re-analysis/research/20261007-sound-reachability-assets.json').read_text(encoding='utf-8-sig'))
    archive=Path('../cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip')
    assert hashlib.sha256(archive.read_bytes()).hexdigest()==index['archiveSha256']
    records=[];seen=[]
    with zipfile.ZipFile(archive) as z:
        for bank in index['banks']:
            b=z.read(bank['bank']);assert hashlib.sha256(b).hexdigest()==bank['sha256']
            p=0
            while p<len(b):
                assert p+8<=len(b)
                tag=b[p:p+4];size=struct.unpack_from('<I',b,p+4)[0];end=p+8+size;assert end<=len(b)
                if tag==b'HIRC':
                    q=p+8;n=struct.unpack_from('<I',b,q)[0];q+=4
                    for _ in range(n):
                        kind=b[q];length=struct.unpack_from('<I',b,q+1)[0];start=q+5;stop=start+length
                        assert stop<=end and length>=4
                        if kind in (8,20):seen.append((bank['bank'],kind,start,length))
                        q=stop
                    assert q==end
                p=end
            assert p==len(b)
        expected=[(o['bank'],o['type'],o['payloadOffset'],o['length']) for o in index['objects'] if o['type'] in (8,20)]
        assert sorted(seen)==sorted(expected)
        # No type20 envelope in this shipped corpus; no claim of global runtime absence.
        assert all(k==8 for _,k,_,_ in seen)
        for o in index['objects']:
            if o['type']!=8:continue
            b=z.read(o['bank']);start=o['payloadOffset'];end=start+o['length'];raw=b[start:end]
            assert hashlib.sha256(raw).hexdigest()==o['sha256']
            assert struct.unpack_from('<I',b,start)[0]==o['id']
            q=start+8
            def take(n):
                nonlocal q
                assert q+n<=end,(o['id'],q,end)
                v=q;q+=n;return v
            count=b[take(1)];take(5*count)
            common=take(9)
            words=take(12);duration,other,n=struct.unpack_from('<III',b,words);duck=take(18*n)
            fx=b[take(1)];bypass=None;fxstart=q
            if fx:bypass=b[take(1)];fxstart=q;take(7*fx)
            pos=take(5);ident=struct.unpack_from('<I',b,pos)[0];share=b[pos+4]
            records.append(dict(bank=o['bank'],id=o['id'],payloadOffset=start,payloadLength=o['length'],
                payloadSha256=o['sha256'],parentID=struct.unpack_from('<I',b,start+4)[0],propertyCount=count,
                commonHeaderOffset=common,commonHeaderHex=b[common:common+9].hex(),duckCount=n,duckOffset=duck,
                fxCount=fx,bypassRaw=bypass,fxOffset=fxstart,fxHex=b[fxstart:fxstart+7*fx].hex(),
                helperOffset=pos,helperID=ident,helperSharedRaw=share,helperHex=b[pos:pos+5].hex(),
                prefixHex=b[start:q].hex(),nextOffset=q,end=end))
    Path('re-analysis/research/20261009-sound-bus-helper-input-joins.jsonl').write_text(
        ''.join(json.dumps(r,separators=(',',':'))+'\n' for r in records),encoding='utf-8')
    print('Validated HIRC bus envelope census and native prefixes:',len(records),
          'helper IDs',dict(collections.Counter(r['helperID'] for r in records)),
          'shared raw',dict(collections.Counter(r['helperSharedRaw'] for r in records)))
    assert len(records)==15 and all(r['helperID']==0 and r['helperSharedRaw']==0 for r in records)

if __name__=='__main__':main()
