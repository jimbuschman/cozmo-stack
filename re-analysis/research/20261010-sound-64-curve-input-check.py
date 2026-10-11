"""Q14-115 explicitly requested 64-entry input check. Raw ZIP/HIRC bytes; no C# or exporter parser."""
from pathlib import Path
import struct,zipfile,json,hashlib,collections
ROOT=Path(__file__).resolve().parents[2]
ARCHIVE=ROOT.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
class Cursor:
 def __init__(self,b,offset):self.b=b;self.p=offset
 def number(self,n):
  assert self.p+n<=len(self.b)
  value=int.from_bytes(self.b[self.p:self.p+n],'little');self.p+=n;return value
 def skip(self,n):assert self.p+n<=len(self.b);self.p+=n
 def bindings(self,bank,oid,typ,rows):
  count=self.number(2)
  for ordinal in range(count):
   start=self.p;source=self.number(4);kind=self.number(1);accumulate=self.number(1);parameter=0
   while True:
    q=self.number(1);parameter=(parameter<<7)|(q&127)
    if q<128:break
   curve=self.number(4);scaling=self.number(1);points=self.number(2);raw=[]
   for i in range(points):raw.append([f'{self.number(4):08X}',f'{self.number(4):08X}',self.number(4)])
   rows.append(dict(bank=bank,objectId=oid,objectType=typ,ordinal=ordinal,offset=start,end=self.p,sourceId=source,sourceType=kind,accumulate=accumulate,parameter=parameter,curveId=curve,scaling=scaling,points=raw,rawHex=self.b[start:self.p].hex(),sha256=hashlib.sha256(self.b[start:self.p]).hexdigest()))
 def properties(self,ranged):
  n=self.number(1);self.skip(n);self.skip(n*(8 if ranged else 4))
 def node(self):
  self.number(1);fx=self.number(1)
  if fx:self.number(1);self.skip(fx*7)
  self.number(1);self.number(4);self.number(4);self.number(1)
  self.properties(False);self.properties(True)
  positioning=self.number(1);assert not (positioning&1 and positioning&8),'excluded nonempty positioning payload'
  aux=self.number(1)
  if aux&8:self.skip(16)
  self.skip(6);groups=self.number(4)
  for _ in range(groups):self.number(4);self.number(1);states=self.number(2);self.skip(8*states)
def run():
 rows=[];banks=[]
 with zipfile.ZipFile(ARCHIVE) as z:
  for name in sorted(n for n in z.namelist() if n.endswith('.bnk')):
   b=z.read(name);off=0;hirc=None
   while off<len(b):
    tag=b[off:off+4];size=int.from_bytes(b[off+4:off+8],'little');end=off+8+size;assert end<=len(b)
    if tag==b'BKHD':assert int.from_bytes(b[off+20:off+24],'little')==0
    if tag==b'HIRC':hirc=(off+8,end)
    off=end
   assert hirc and off==len(b)
   cur=Cursor(b,hirc[0]);total=cur.number(4);before=len(rows)
   for i in range(total):
    typ=cur.number(1);length=cur.number(4);start=cur.p;end=start+length;assert end<=hirc[1];oid=cur.number(4)
    if typ==2:
     plugin=cur.number(4);cur.skip(10)
     if plugin&15 in (2,5):cur.skip(cur.number(4))
     cur.node()
    elif typ in (5,6,7,9):cur.node()
    elif typ in (10,12,13):cur.number(1);cur.node()
    elif typ==11:
     cur.number(1);ns=cur.number(4);cur.skip(14*ns);nc=cur.number(4);cur.skip(40*nc)
     if nc:cur.number(4)
     na=cur.number(4)
     for j in range(na):cur.number(4);cur.number(4);np=cur.number(4);cur.skip(np*12)
     cur.node()
    elif typ in (21,22):cur.properties(False);cur.properties(True)
    elif typ==8:
     cur.number(4);cur.properties(False);cur.number(1);channel=cur.number(1);assert channel&15==0;cur.skip(15);ducks=cur.number(4);cur.skip(18*ducks);fx=cur.number(1)
     if fx:cur.number(1);cur.skip(7*fx)
     cur.skip(6)
    elif typ in (18,19):cur.number(4);cur.skip(cur.number(4));n=cur.number(1);cur.skip(n*5)
    else:cur.p=end;continue
    cur.bindings(name,oid,typ,rows);assert cur.p<=end;cur.p=end
   assert cur.p==hirc[1];banks.append(dict(bank=name,sha256=hashlib.sha256(b).hexdigest(),objects=total,curves=len(rows)-before))
 assert len(banks)==6 and len(rows)==64,(len(banks),len(rows))
 out=Path(__file__).with_name('20261010-sound-64-curve-input-witness.json')
 result=dict(archiveSha256=hashlib.sha256(ARCHIVE.read_bytes()).hexdigest(),banks=banks,entries=rows)
 out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
 print('banks',len(banks),'entries',len(rows),'scaling',dict(collections.Counter(x['scaling'] for x in rows)),'accumulate',dict(collections.Counter(x['accumulate'] for x in rows)),'sourceType',dict(collections.Counter(x['sourceType'] for x in rows)),'pointCounts',dict(collections.Counter(len(x['points']) for x in rows)),'shapes',dict(collections.Counter(p[2] for x in rows for p in x['points'])))
 print('curveId duplicates within object',[(x['bank'],x['objectId']) for x in rows if sum(y['bank']==x['bank'] and y['objectId']==x['objectId'] and y['curveId']==x['curveId'] for y in rows)>1])
if __name__=='__main__':run()
