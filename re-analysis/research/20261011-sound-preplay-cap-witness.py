"""Actual pre-play delay property/range inputs; live device states stay explicit."""
from pathlib import Path
import collections
import hashlib
import json
import zipfile

r = Path(__file__).resolve().parent
nav = json.loads((r / '20261010-sound-bus-ancestor-input-witness.json').read_text())
xs = json.loads((r / '20261010-sound-play-control-input-witness.json').read_text())['rows']
objects = {(x['bank'], x['objectId']): x for x in nav['rows']}
archive = r.parents[1].parent / 'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == nav['archiveSha256']
ranges, delays = [], []
counts = collections.Counter()
with zipfile.ZipFile(archive) as z:
    banks = {name: z.read(name) for name in {x['bank'] for x in xs}}
    for x in xs:
        b = banks[x['bank']]
        o = objects[x['bank'], x['id']]
        start, end = o['payloadOffset'], o['payloadOffset'] + o['payloadLength']
        assert hashlib.sha256(b[start:end]).hexdigest() == x['payloadSha256']
        p = x['commonEnd']
        for width, expected in ((4, x['properties']), (8, x['ranges'])):
            count = b[p]
            p += 1
            ids = list(b[p:p+count])
            p += count
            assert ids == [v['id'] for v in expected]
            if width == 8:
                counts[count] += 1
            for v in expected:
                assert p == v['offset'] and b[p:p+width].hex() == v['rawHex']
                if width == 8:
                    ranges.append(dict(objectID=x['id'], bank=x['bank'], **v))
                elif v['id'] == 59:
                    delays.append(dict(objectID=x['id'], bank=x['bank'], type=x['type'], **v))
                p += width
        assert p == x['positionFlagOffset']
assert len(xs) == 3490 and counts == {0:3478, 1:12}
assert len(ranges) == 12 and all(x['id'] == 2 for x in ranges)
assert len(delays) == 2 and all(x['type'] == 2 and x['rawHex'] == 'cdcccc3d' for x in delays)
caps = json.loads((r / '20261011-sound-rate-frame-cap-frontiers.json').read_text())['frontiers']
pending = []
for x in next(f for f in caps if f['seed'] == '009F12E8')['pending']:
    a = int(x['address'], 16)
    reason = 'existing ID contract; live input or prior register role remains explicit'
    if a in (0x9F143C, 0x9F1458, 0x9F1464, 0x9F1450) and int(x['known_registers'].get('lr', '0'), 16) > 1:
        reason = 'outside authored ranged-property next-index bound'
    elif a == 0x9F1448 and int(x['known_registers'].get('r3', '0'), 16) >= 1:
        reason = 'outside authored ranged-property repeated-index bound'
    elif 0x9F1468 <= a <= 0x9F14E0 or a == 0x9F1554:
        reason = 'matched3B range body has no such authored range input'
    pending.append(dict(**x, disposition=reason))
device = next(f for f in caps if f['seed'] == '009EBE6C')
assert len(pending) == 46 and len(device['pending']) == 33
out = dict(nodeCount=3490, rangeHistogram=dict(counts), ranges=ranges, authoredDelay3B=delays,
           preplayPending=pending, preplayDispositionCounts=dict(collections.Counter(x['disposition'] for x in pending)),
           devicePending=device['pending'],
           boundary='No universal live range/blob mutation absence. Device states have no recovered finite bank bound;cadence/provider and sink interfaces remain their existing contracts.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print(json.dumps(out['preplayDispositionCounts'], indent=2))
