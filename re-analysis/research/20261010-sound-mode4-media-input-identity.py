"""Two original mode4 nodes: shipped media metadata identity, not admission/unreachability."""
from pathlib import Path
import zipfile,struct,hashlib,json
root=Path(__file__).resolve().parents[2];b=Path(__file__).parent
archive=root.parent/'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
nav=json.loads((b/'20261007-sound-reachability-assets.json').read_text(encoding='utf-8'))
lookup={(o['bank'],o['id']):o for o in nav['objects']};name='English(US)/Cozmo.bnk'
outer=lookup[name,725225627];inner=lookup[name,196431345];sibling=lookup[name,990835622]
assert outer['children']==[196431345,990835622]
rows=[]
with zipfile.ZipFile(archive) as z:
 data=z.read(name);txt=z.read('English(US)/Cozmo.txt');xml=z.read('SoundbanksInfo.xml');lines=txt.decode('utf-8-sig').splitlines()
 for parent in (inner,sibling):
  for oid in parent['children']:
   o=lookup[name,oid];assert o['type']==2;at=o['payloadOffset'];raw=data[at:at+o['length']];assert hashlib.sha256(raw).hexdigest()==o['sha256']
   # TR6/8 native9B9CC4 reads header+5; Sound ID is payload0 and source header starts payload4.
   media=struct.unpack_from('<I',raw,9)[0];matches=[]
   for i,l in enumerate(lines):
    f=l.split('\t')
    if len(f)>5 and f[1].strip()==str(media):matches.append({'line':i+1,'text':l,'shortName':f[2],'objectPath':f[5]})
   assert matches and all('Robot_VO__Shared_Upgrade_Align_Frustrated_Loop' in m['objectPath'] for m in matches)
   rows.append({'parentID':parent['id'],'soundID':oid,'payloadOffset':at,'payloadSHA256':o['sha256'],'mediaIDOffset':at+9,'mediaID':media,'sourceHeaderHex':raw[4:18].hex(),'metadata':matches})
 r={'scope':'Exactly original outer725225627, inner196431345 and its already-authored sibling990835622; native source media-ID geometry joined to shipped text metadata. No Play/caller proof or SDK-only inference.','bank':name,'bankSHA256':hashlib.sha256(data).hexdigest(),'textMember':'English(US)/Cozmo.txt','textSHA256':hashlib.sha256(txt).hexdigest(),'xmlMember':'SoundbanksInfo.xml','xmlSHA256':hashlib.sha256(xml).hexdigest(),'sounds':rows,'metadataLinesContainingSDK':[{'line':i+1,'text':l} for i,l in enumerate(lines) if 'SDK' in l and 'Align_Frustrated' in l]}
(b/'20261010-sound-mode4-media-input-identity.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8',newline='\n')
print('sounds',len(rows),'inner',len(inner['children']),'sibling',len(sibling['children']))
for r in rows:print(r['soundID'],r['mediaID'],r['metadata'][0]['shortName'],r['metadata'][0]['line'])
