"""Positive segment marker counts and saved navigation counter dispositions."""
from pathlib import Path
import collections
import hashlib
import json
import struct
import zipfile

r = Path(__file__).resolve().parent
nav = json.loads((r / '20261007-sound-reachability-assets.json').read_text(encoding='utf-8-sig'))
music = json.loads((r / '20261011-sound-music-child-cap-witness.json').read_text())
objects = {(x['bank'], x['id']): x for x in nav['objects']}
archive = r.parents[1].parent / 'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == music['archiveSha256']
rows = []
with zipfile.ZipFile(archive) as z:
    banks = {name: z.read(name) for name in {x['bank'] for x in music['rows']}}
    for x in music['rows']:
        if x['type'] != 10:
            continue
        o = objects[x['bank'], x['id']]
        b = banks[x['bank']]
        end = o['payloadOffset'] + o['length']
        assert hashlib.sha256(b[o['payloadOffset']:end]).hexdigest() == x['payloadSha256']
        p = x['meterOffset']
        assert b[p:p+23].hex() == x['meterHex']
        p += 23
        assert struct.unpack_from('<I', b, p)[0] == 0  # MPAY4: no stinger records.
        p += 4
        duration = b[p:p+8].hex()
        count = struct.unpack_from('<I', b, p+8)[0]
        count_offset = p+8
        p += 12
        markers = []
        for _ in range(count):
            q = p
            marker_id = struct.unpack_from('<I', b, p)[0]
            time = b[p+4:p+12].hex()
            length = struct.unpack_from('<I', b, p+12)[0]
            p += 16 + length
            assert p <= end
            markers.append(dict(id=marker_id, offset=q, timeHex=time,
                                nameLength=length, nameHex=b[q+16:p].hex()))
        assert p == end
        rows.append(dict(id=x['id'], bank=x['bank'], payloadSha256=x['payloadSha256'],
                         stingerCount=0, durationHex=duration, markerCountOffset=count_offset,
                         markerCount=count, markers=markers))
assert len(rows) == 209
assert max(x['markerCount'] for x in rows) == 7
caps = json.loads((r / '20261011-sound-rate-frame-cap-frontiers.json').read_text())['frontiers']
runtime = next(x for x in caps if x['seed'] == '00989FF4')
assert len(runtime['pending']) == 4
assert all(int(x['known_registers']['ip'], 16) > 7 for x in runtime['pending'])
parser = next(x for x in caps if x['seed'] == '0098A190')
classified = []
for x in parser['pending']:
    # R5 is a name length in parse entries, and a counter only in cleanup.
    addr = int(x['address'], 16)
    r5 = x['known_registers'].get('r5')
    beyond = 0x98A2F0 <= addr <= 0x98A310 and r5 is not None and int(r5, 16) > 7
    classified.append(dict(**x, disposition=('outside authored cleanup count bound' if beyond
                                             else 'existing MK2-4 contract; no admission conclusion')))
out = dict(objectCount=len(rows), maxMarkerCount=7,
           countHistogram=dict(sorted(collections.Counter(x['markerCount'] for x in rows).items())),
           runtimePending=runtime['pending'], parserPending=classified,
           parserBeyondAuthoredCleanupCount=sum(x['disposition'].startswith('outside') for x in classified),
           rows=rows,
           boundary='Actual segment payload count bound only. Other setters/mutable old arrays/callback inputs are not excluded; saved parser states without a proven counter contradiction remain explicit.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print(json.dumps({k:v for k,v in out.items() if k not in ('rows', 'runtimePending', 'parserPending')}, indent=2))
