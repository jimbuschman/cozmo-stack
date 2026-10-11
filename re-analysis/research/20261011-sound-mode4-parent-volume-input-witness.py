from pathlib import Path
import json,zipfile,struct,hashlib
r=Path('re-analysis/research');nav=json.loads((r/'20261007-sound-reachability-assets.json').read_text(encoding='utf-8-sig'))
a=Path('../cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip')
assert hashlib.sha256(a.read_bytes()).hexdigest()==nav['archiveSha256']
with zipfile.ZipFile(a) as z:b=z.read('Init.bnk')
assert hashlib.sha256(b).hexdigest()==next(x['sha256'] for x in nav['banks'] if x['bank']=='Init.bnk')
p=0;found=[]
while p<len(b):
 tag=b[p:p+4];n=struct.unpack_from('<I',b,p+4)[0];end=p+8+n;assert end<=len(b)
 if tag==b'STMG':found.append((p+8,b[p+8:end]))
 p=end
assert p==len(b) and len(found)==1
start,body=found[0];assert len(body)==1095
count=struct.unpack_from('<I',body,306)[0];assert count==37
# U5's exact 21-byte entries begin after the count; zero-based ordinal30.
off=310+30*21
record=struct.unpack_from('<IIIIIB',body,off)
assert record==(0xD2687048,0x3F800000,0,0,0,0)
out=dict(archiveSha256=nav['archiveSha256'],bankSha256=hashlib.sha256(b).hexdigest(),stmgBodyOffset=start,
         stmgBodyLength=len(body),parameterCount=count,parameterOrdinal=30,parameterBodyOffset=off,
         absoluteOffset=start+off,rawHex=body[off:off+21].hex(),
         id='D2687048',defaultBits='3F800000',rampType=0,rampUpBits='00000000',rampDownBits='00000000',builtIn=0,
         boundary='Primary authored default only; later scoped values and runtime publisher admission are not inferred from it.')
(r/'20261011-sound-mode4-parent-volume-input-witness.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(json.dumps(out,indent=2))
