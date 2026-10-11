"""Actual action property inputs; no blanket exclusion of callback mutation."""
from pathlib import Path
import collections
import hashlib
import json
import struct
import zipfile

r = Path(__file__).resolve().parent
nav = json.loads((r / '20261007-sound-reachability-assets.json').read_text(encoding='utf-8-sig'))
xs = [x for x in nav['objects'] if x['type'] == 3]
archive = r.parents[1].parent / 'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == nav['archiveSha256']
ordinary, ranged = collections.Counter(), collections.Counter()
rows, delays = [], []
with zipfile.ZipFile(archive) as z:
    banks = {name: z.read(name) for name in {x['bank'] for x in xs}}
    for x in xs:
        b = banks[x['bank']][x['payloadOffset']:x['payloadOffset']+x['length']]
        assert hashlib.sha256(b).hexdigest() == x['sha256']
        assert struct.unpack_from('<I', b)[0] == x['id']
        p, blocks = 11, []
        for width, histogram in ((4, ordinary), (8, ranged)):
            q = p
            count = b[p]
            histogram[count] += 1
            p += 1
            ids = list(b[p:p+count])
            p += count
            values = [b[p+width*i:p+width*(i+1)].hex() for i in range(count)]
            p += width*count
            assert p <= len(b)
            blocks.append(dict(count=count, offset=x['payloadOffset']+q, ids=ids, rawValues=values))
            if 15 in ids:
                index = ids.index(15)
                delays.append(dict(id=x['id'], width=width, rawHex=values[index],
                                   signedMilliseconds=int.from_bytes(bytes.fromhex(values[index])[:4], 'little', signed=True)))
        if blocks[0]['count'] or blocks[1]['count']:
            rows.append(dict(bank=x['bank'], id=x['id'], payloadSha256=x['sha256'], blocks=blocks))
assert len(xs) == 906 and max(ordinary) == 2 and ranged == {0: 906}
cap = next(x for x in json.loads((r / '20261011-sound-rate-frame-cap-frontiers.json').read_text())['frontiers'] if x['seed'] == '00A613F8')
pending = []
for x in cap['pending']:
    outside = int(x['address'], 16) in (0xA61584, 0xA61588, 0xA615A0, 0xA615A4) and int(x['known_registers'].get('r0', '0'), 16) > 2
    pending.append(dict(**x, disposition=('outside authored ordinary property index bound' if outside
                                         else 'existing AH413D boundary; register origin or callback/live blob input remains explicit')))
assert sum(x['disposition'].startswith('outside') for x in pending) == 4
out = dict(actionCount=906, ordinaryHistogram=dict(ordinary), rangeHistogram=dict(ranged),
           actionsWithProperties=rows, delayInputs=delays, savedPending=pending,
           boundary='Positive serialized Action inputs only. Derived callback writes/later mutation and arbitrary alternate inputs are not excluded; no generic zero-range unreachable claim.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print('Actions906;ordinary counts', dict(ordinary), ';range counts', dict(ranged), ';delay15 inputs', len(delays))
