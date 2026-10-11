"""Bounded primary tempo/meter witnesses; no asset upload or runtime changes."""
from pathlib import Path
import hashlib, json, struct, subprocess, sys, zipfile
root=Path(__file__).resolve().parents[2]
out=root/'re-analysis/research'
args=['992428:99249C','987D34:987DD8','A3C8F0:A3C9B8','987830:987878','9879FC:987AD4','words:987AE8:987AF8','A3EF80:A3EFB4','A3CD80:A3CDA0','A3C4B8:A3C53C','A3C608:A3C650','words:A3C748:A3C760','words:1040068:104006C']
p=subprocess.run([sys.executable,str(out/'20261007-queue5-native.py'),'--light',*args],capture_output=True,check=True)
(out/'20261010-sound-midi-tempo-input-native.txt').write_text('\n'.join(s.rstrip() for s in p.stdout.decode('utf-8').splitlines())+'\n',encoding='utf-8')
with zipfile.ZipFile(root.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip') as z:
    bank=z.read('English(US)/Cozmo.bnk')
assert hashlib.sha256(bank).hexdigest()=='7a2770c62f3bf31ee4f77389d3a147c85768034bb29c9c84e913a3284a5efbe8'
rows=[]
for line in (out/'20261010-sound-midi-target-input-witness.jsonl').read_text(encoding='utf-8').splitlines():
    o=json.loads(line)
    payload=bank[o['payloadOffset']:o['payloadOffset']+o['length']]
    assert hashlib.sha256(payload).hexdigest()==o['sha256']
    count=struct.unpack_from('<I',bank,o['childrenOffset'])[0]
    children=list(struct.unpack_from('<'+str(count)+'I',bank,o['childrenOffset']+4))
    assert children==o['children']
    at=o['childrenOffset']+4+4*count
    assert at+23<=o['payloadOffset']+o['length']
    raw=bank[at:at+23]
    period,offset,tempo,beats,value,override=struct.unpack('<ddfBBB',raw)
    assert o['musicFlags']==6 and [v['id'] for v in o['properties']]==[56]
    rows.append(dict(bank=o['bank'],id=o['id'],payloadOffset=o['payloadOffset'],length=o['length'],sha256=o['sha256'],bankSha256=o['bankSha256'],childrenCount=count,meterOffset=at,meterHex=raw.hex(),periodMilliseconds=period,offsetMilliseconds=offset,tempo=tempo,tempoBits=f'{struct.unpack_from("<I",raw,16)[0]:08X}',beatsPerBar=beats,beatValue=value,meterOverride=override,tempoProperty55Absent=True,tempoPropertyOverride=1))
(out/'20261010-sound-midi-tempo-input-witness.jsonl').write_text(''.join(json.dumps(o,separators=(',',':'))+'\n' for o in rows),encoding='utf-8')
print(json.dumps(rows,indent=2))
