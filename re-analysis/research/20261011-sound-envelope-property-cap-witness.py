"""Actual type22 envelope properties and instruction-specific saved index bounds."""
from pathlib import Path
import collections
import hashlib
import json
import struct
import zipfile

r = Path(__file__).resolve().parent
nav = json.loads((r / '20261007-sound-reachability-assets.json').read_text(encoding='utf-8-sig'))
xs = [x for x in nav['objects'] if x['type'] == 22]
archive = r.parents[1].parent / 'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == nav['archiveSha256']
rows = []
with zipfile.ZipFile(archive) as z:
    banks = {name: z.read(name) for name in {x['bank'] for x in xs}}
    for x in xs:
        b = banks[x['bank']][x['payloadOffset']:x['payloadOffset']+x['length']]
        assert hashlib.sha256(b).hexdigest() == x['sha256']
        assert struct.unpack_from('<I', b)[0] == x['id']
        p = 4
        count = b[p]
        p += 1
        ids = list(b[p:p+count])
        p += count
        values = [b[p+4*i:p+4*(i+1)].hex() for i in range(count)]
        p += 4*count
        assert b[p] == 0
        p += 1
        assert struct.unpack_from('<H', b, p)[0] == 0 and p+2 == len(b)
        rows.append(dict(id=x['id'], bank=x['bank'], payloadSha256=x['sha256'],
                         count=count, ids=ids, rawValues=values, rangeCount=0, rtpcCount=0,
                         firstProperty9NextIndex=ids.index(9)+1, secondProperty11NextIndex=ids.index(11)+1))
assert len(rows) == 7 and max(x['count'] for x in rows) == 7
assert all(x['firstProperty9NextIndex'] == 2 and x['secondProperty11NextIndex'] == 4 for x in rows)
cap = next(x for x in json.loads((r / '20261011-sound-rate-frame-cap-frontiers.json').read_text())['frontiers'] if x['seed'] == '009D5974')
registers = {0x9D59F8:'ip', 0x9D59DC:'ip', 0x9D59C0:'ip',
             0x9D5BF0:'lr', 0x9D5BFC:'lr', 0x9D5BE0:'lr', 0x9D5C0C:'lr'}
pending = []
for x in cap['pending']:
    register = registers.get(int(x['address'], 16))
    index = int(x['known_registers'].get(register, '0'), 16) if register else 0
    outside = index > 7
    pending.append(dict(**x, disposition=('outside authored envelope property index bound' if outside
                        else 'existing EP/804 contract; live callback/table or prior register origin remains explicit')))
assert len(pending) == 32 and sum(x['disposition'].startswith('outside') for x in pending) == 7
out = dict(envelopeCount=7, countHistogram=dict(collections.Counter(x['count'] for x in rows)),
           rows=rows, savedPending=pending,
           boundary='Type22 authored payloads only; not Action or node blocks. RTPC callbacks may reload changed tables; zero authored ranges/subscriptions do not establish global runtime absence.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print('Seven Envelope payloads:counts6/7,first9 atindex2,second11 atindex4;7 of32 saved explicit indices exceed7.')
