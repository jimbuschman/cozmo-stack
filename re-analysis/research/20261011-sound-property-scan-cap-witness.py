"""Hash-checked ordinary property count bound; no mutable-blob absence proof."""
from pathlib import Path
import collections
import hashlib
import json
import zipfile

r = Path(__file__).resolve().parent
source = r / '20261010-sound-play-control-input-witness.json'
xs = json.loads(source.read_text())['rows']
archive = r.parents[1].parent / 'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == 'd889b6e82606be8e284fedde44daecdbd5406f72bf5c68a1396f319eeecf6145'
counts = collections.Counter()
property46 = []
with zipfile.ZipFile(archive) as z:
    banks = {name: z.read(name) for name in {x['bank'] for x in xs}}
    nav = json.loads((r / '20261010-sound-bus-ancestor-input-witness.json').read_text())
    objects = {(x['bank'], x['objectId']):x for x in nav['rows']}
    for x in xs:
        b = banks[x['bank']]
        o = objects[x['bank'], x['id']]
        start, end = o['payloadOffset'], o['payloadOffset'] + o['payloadLength']
        assert hashlib.sha256(b[start:end]).hexdigest() == x['payloadSha256']
        p = x['commonEnd']
        count = b[p]
        p += 1
        ids = list(b[p:p+count])
        p += count
        assert ids == [v['id'] for v in x['properties']]
        for v in x['properties']:
            assert p == v['offset'] and b[p:p+4].hex() == v['rawHex']
            if v['id'] == 46:
                property46.append(dict(bank=x['bank'], objectID=x['id'], type=x['type'], **v))
            p += 4
        assert p <= end
        counts[count] += 1
assert len(xs) == 3490 and max(counts) == 4
cap = next(x for x in json.loads((r / '20261011-sound-rate-frame-cap-frontiers.json').read_text())['frontiers'] if x['seed'] == '009EDF5C')
assert len(cap['pending']) == 6
assert all(int(x['known_registers']['r0'], 16) > 4 for x in cap['pending'])
out = dict(objectCount=3490, maxPropertyCount=4, histogram=dict(sorted(counts.items())),
           navigationSha256=hashlib.sha256(source.read_bytes()).hexdigest(), property46=property46,
           savedPending=cap['pending'],
           boundary='Actual node property blocks only; arbitrary later property replacement/mutation or pointer identity not excluded. All source rows independently checked, navigation retained in cited hashed input file.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print(json.dumps({k:v for k,v in out.items() if k != 'savedPending'}, indent=2))
