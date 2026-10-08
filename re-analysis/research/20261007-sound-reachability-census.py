"""Local shipped-asset census. Raw envelopes independent of the candidate C# reader.

Hierarchy export is navigation; raw ID/offset/payload and serialized references are
checked here. Does not emulate playback, prove an app caller, or settle a record.
"""
from pathlib import Path
import argparse, collections, hashlib, json, struct, zipfile

def u32(b, p): return struct.unpack_from('<I', b, p)[0]
def census(archive, export):
    candidate = {(r['bank'], r['Id']): r for r in
                 map(json.loads, Path(export).read_text(encoding='utf-8').splitlines())
                 if r['kind'] == 'object'}
    objects, banks, media, names = [], [], [], {}
    with zipfile.ZipFile(archive) as z:
        for name in sorted(z.namelist()):
            if name.endswith('.txt'):
                for line in z.read(name).decode('utf-8-sig').splitlines():
                    cols=line.split('\t')
                    if len(cols)>2 and cols[1].isdigit(): names[int(cols[1])]=cols[2]
            if name.endswith('.wem'):
                b=z.read(name); p=12
                while p+8<=len(b):
                    tag=b[p:p+4]; n=u32(b,p+4)
                    if tag==b'fmt ':
                        media.append(dict(name=name,size=len(b),fmtOffset=p+8,
                            format=struct.unpack_from('<H',b,p+8)[0],
                            channels=struct.unpack_from('<H',b,p+10)[0],rate=u32(b,p+12),
                            blockAlign=struct.unpack_from('<H',b,p+20)[0],
                            bits=struct.unpack_from('<H',b,p+22)[0]))
                        break
                    p+=8+n+(n&1)
            if not name.endswith('.bnk'): continue
            b=z.read(name); p=0; chunks=[]; counts=collections.Counter(); didx=[]; data=None
            while p<len(b):
                assert p+8<=len(b)
                tag=b[p:p+4].decode('ascii'); size=u32(b,p+4); a=p+8; end=a+size
                assert end<=len(b)
                chunks.append(dict(tag=tag,offset=p,size=size))
                if tag=='DIDX':
                    assert size%12==0
                    didx=[struct.unpack_from('<III',b,j) for j in range(a,end,12)]
                if tag=='DATA': data=a
                if tag=='HIRC':
                    n=u32(b,a); q=a+4
                    for i in range(n):
                        typ=b[q]; length=u32(b,q+1); start=q+5; finish=start+length
                        assert finish<=end and length>=4
                        ident=u32(b,start); counts[typ]+=1
                        r=candidate[(name,ident)]; assert r['type']==typ
                        row=dict(bank=name,id=ident,type=typ,offset=q,payloadOffset=start,
                                 length=length,sha256=hashlib.sha256(b[start:finish]).hexdigest())
                        if typ==4:
                            count=u32(b,start+4); assert 8+count*4==length
                            row['actions']=[u32(b,start+8+j*4) for j in range(count)]
                            assert row['actions']==r['actions']
                        if typ==3:
                            row['actionType']=struct.unpack_from('<H',b,start+4)[0]
                            row['target']=u32(b,start+6)
                            assert row['target']==r['actionTarget'] and row['actionType']==r['actionType']
                        node=r.get('node') or {}; children=node.get('Children',[])
                        if children:
                            seq=struct.pack('<I',len(children))+struct.pack('<'+'I'*len(children),*children)
                            found=b[start:finish].find(seq)
                            assert found>=0,(name,ident,'children not serialized')
                            row['childrenOffset']=start+found; row['children']=children
                        objects.append(row); q=finish
                    assert q==end
                p=end
            embedded=[]
            for ident,off,size in didx:
                assert data is not None
                payload=b[data+off:data+off+size]; assert len(payload)==size
                item=dict(id=ident,offset=data+off,size=size,magic=payload[:4].hex())
                if payload[:4]==b'MThd':
                    item['midiHeader']=struct.unpack_from('>HHH',payload,8)
                if payload[:4]==b'RIFF':
                    q=12
                    while q+8<=len(payload):
                        n=u32(payload,q+4)
                        if payload[q:q+4]==b'fmt ':
                            item.update(format=struct.unpack_from('<H',payload,q+8)[0],
                                channels=struct.unpack_from('<H',payload,q+10)[0],rate=u32(payload,q+12))
                            break
                        q+=8+n+(n&1)
                embedded.append(item)
            banks.append(dict(bank=name,size=len(b),sha256=hashlib.sha256(b).hexdigest(),media=embedded,
                              chunks=chunks,counts=dict(counts)))
    assert len(candidate)==len(objects)
    for row in objects: row['name']=names.get(row['id'],'')
    return dict(archiveSha256=hashlib.sha256(Path(archive).read_bytes()).hexdigest(),
                banks=banks,objects=objects,media=media)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('archive');p.add_argument('export');p.add_argument('output')
    a=p.parse_args(); out=census(a.archive,a.export)
    # One record per line keeps the metadata diff reviewable without a binary asset.
    lines=['{', '"archiveSha256":'+json.dumps(out['archiveSha256'])+',']
    for key in ('banks','objects','media'):
        lines.append(json.dumps(key)+':[')
        lines.extend(json.dumps(row,separators=(',',':'))+(',' if i+1<len(out[key]) else '')
                     for i,row in enumerate(out[key]))
        lines.append(']'+(',' if key!='media' else ''))
    lines.append('}')
    Path(a.output).write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print('Raw objects',len(out['objects']),'media headers',len(out['media']))
    print('Types',dict(collections.Counter(o['type'] for o in out['objects'])))
    print('Formats',dict(collections.Counter((m['format'],m['channels'],m['rate']) for m in out['media'])))
