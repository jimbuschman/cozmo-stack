"""Original Q14-104/131 bounded shipped bus/ancestor/attachment input census; raw bytes."""
from pathlib import Path
import struct,zipfile,json,hashlib,collections
ROOT=Path(__file__).resolve().parents[2]
ARCHIVE=ROOT.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
class Cursor:
 def __init__(self,b,p,end):self.b=b;self.p=p;self.end=end
 def n(self,k):assert self.p+k<=self.end;v=int.from_bytes(self.b[self.p:self.p+k],'little');self.p+=k;return v
 def skip(self,k):assert self.p+k<=self.end;self.p+=k
 def header(self):
  start=self.p;override=self.n(1);fx=self.n(1)
  if fx:self.n(1);self.skip(fx*7)
  extra=self.n(1);boff=self.p;bus=self.n(4);poff=self.p;parent=self.n(4);flags=self.n(1)
  return dict(commonOffset=start,commonEnd=self.p,overrideByte=override,fxCount=fx,extraByte=extra,outputBusOffset=boff,outputBusId=bus,parentOffset=poff,parentId=parent,flags=flags,rawHex=self.b[start:self.p].hex())
def run():
 rows=[];banks=[]
 with zipfile.ZipFile(ARCHIVE) as z:
  for name in sorted(n for n in z.namelist() if n.endswith('.bnk')):
   b=z.read(name);p=0;hi=None
   while p<len(b):
    assert p+8<=len(b);tag=b[p:p+4];n=int.from_bytes(b[p+4:p+8],'little');end=p+8+n;assert end<=len(b)
    if tag==b'HIRC':assert hi is None;hi=(p+8,end)
    p=end
   assert p==len(b) and hi;c=Cursor(b,*[hi[0],hi[1]]);count=c.n(4);before=len(rows)
   for ordinal in range(count):
    typ=c.n(1);length=c.n(4);start=c.p;end=start+length;assert end<=hi[1];oid=c.n(4);s=Cursor(b,c.p,end);r=dict(bank=name,ordinal=ordinal,objectId=oid,objectType=typ,payloadOffset=start,payloadLength=length,payloadSha256=hashlib.sha256(b[start:end]).hexdigest())
    if typ==8:r.update(parentOffset=s.p,parentId=s.n(4),rawParentHex=b[start+4:start+8].hex());r['outputBusId']=r['parentId'];r['routeKind']='bus-parent'
    elif typ in (2,5,6,7,9,10,11,12,13):
     if typ==2:
      plugin=s.n(4);s.skip(10)
      if plugin&15 in (2,5):s.skip(s.n(4))
     elif typ in (10,12,13):s.n(1)
     elif typ==11:
      s.n(1);ns=s.n(4);s.skip(14*ns);nc=s.n(4);s.skip(40*nc)
      if nc:s.n(4)
      na=s.n(4)
      for _ in range(na):s.n(4);s.n(4);np=s.n(4);s.skip(np*12)
     r.update(s.header());r['routeKind']='node-common'
    else:c.p=end;continue
    rows.append(r);c.p=end
   assert c.p==hi[1];banks.append(dict(bank=name,sha256=hashlib.sha256(b).hexdigest(),objects=count,routePrefixes=len(rows)-before))
 buses={}
 for r in rows:
  if r['objectType']==8:
   ident=r['objectId'];parent=r['parentId'];assert ident not in buses or buses[ident]==parent;buses[ident]=parent
 paths=[]
 for ident in sorted(buses):
  seen=[];cur=ident
  while cur:
   assert cur not in seen,('bus cycle',ident,seen,cur);seen.append(cur)
   if cur not in buses:break
   cur=buses[cur]
  paths.append(dict(busId=ident,ancestors=seen,terminalId=cur,terminal='root' if cur==0 else 'missing'))
 nodes={}
 for r in rows:
  if r['routeKind']=='node-common':assert r['objectId'] not in nodes;nodes[r['objectId']]=r
 nodepaths=[]
 for ident in sorted(nodes):
  seen=[];cur=ident;chosen=0
  while cur and cur in nodes:
   assert cur not in seen,('node cycle',ident,seen,cur);seen.append(cur);r=nodes[cur]
   if r['outputBusId']:chosen=r['outputBusId'];break
   cur=r['parentId']
  nodepaths.append(dict(nodeId=ident,nodeAncestors=seen,firstExplicitBusId=chosen,terminalNodeId=cur,terminal='bus' if chosen else ('root' if cur==0 else 'missing')))
 missingparents=sorted({r['parentId'] for r in nodes.values() if r['parentId'] and r['parentId'] not in nodes})
 result=dict(archiveSha256=hashlib.sha256(ARCHIVE.read_bytes()).hexdigest(),banks=banks,rows=rows,busPaths=paths,nodePaths=nodepaths,missingNodeParentIds=missingparents,missingOutputBusIds=sorted({r['outputBusId'] for r in rows if r['outputBusId'] and r['outputBusId'] not in buses}),counts=dict(routePrefixes=len(rows),busObjects=sum(r['objectType']==8 for r in rows),uniqueBusIds=len(buses),nodePrefixes=sum(r['routeKind']=='node-common' for r in rows),nodeParentNonzero=sum(r['routeKind']=='node-common' and r['parentId']!=0 for r in rows),nodeOutputBusNonzero=sum(r['routeKind']=='node-common' and r['outputBusId']!=0 for r in rows)))
 out=Path(__file__).with_name('20261010-sound-bus-ancestor-input-witness.json');out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
 print(result['counts']);print('Missing bus IDs',result['missingOutputBusIds']);print('Bus paths',paths)
if __name__=='__main__':run()
