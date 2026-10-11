"""Bound one saved Q14 navigation loop with actual music bank child counts.

Reuses hash-checked prefix navigation, then reads remaining fields independently.
No mutable-input, callback-result, or all-runtime alias absence claim.
"""
from pathlib import Path
import collections
import hashlib
import json
import struct
import zipfile

r = Path(__file__).resolve().parent
nav = json.loads((r / '20261007-sound-reachability-assets.json').read_text(encoding='utf-8-sig'))
prefixes = json.loads((r / '20261010-sound-play-control-input-witness.json').read_text())['rows']
archive = r.parents[1].parent / 'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == nav['archiveSha256']
objects = {(x['bank'], x['id']): x for x in nav['objects']}
rows = []
with zipfile.ZipFile(archive) as z:
    banks = {name: z.read(name) for name in sorted({x['bank'] for x in prefixes})}
    for x in prefixes:
        if x['type'] not in (10, 12, 13):
            continue
        o = objects[x['bank'], x['id']]
        b = banks[x['bank']]
        start, end = o['payloadOffset'], o['payloadOffset'] + o['length']
        assert hashlib.sha256(b[start:end]).hexdigest() == x['payloadSha256'] == o['sha256']
        assert struct.unpack_from('<I', b, start)[0] == x['id']
        p = x['commonEnd']
        for width, expected in ((4, x['properties']), (8, x['ranges'])):
            count = b[p]
            p += 1
            ids = list(b[p:p+count])
            p += count
            assert ids == [v['id'] for v in expected]
            for v in expected:
                assert p == v['offset'] and b[p:p+width].hex() == v['rawHex']
                p += width
        assert p == x['positionFlagOffset'] and b[p] in (0xC0, 0xC3)
        assert b[p+1] == 0  # CF9-11: no aux ID trailer.
        advanced = b[p+2:p+8].hex()  # CF12 six raw bytes.
        p += 8
        assert struct.unpack_from('<I', b, p)[0] == 0  # No state entries.
        p += 4
        rtpc_count = struct.unpack_from('<H', b, p)[0]
        p += 2
        bindings = []
        for _ in range(rtpc_count):
            q = p
            source = struct.unpack_from('<I', b, p)[0]
            p += 6  # Source ID, source type, accumulation.
            prop = 0
            while True:
                v = b[p]
                p += 1
                prop = (prop << 7) | (v & 0x7F)
                if not v & 0x80:
                    break
            curve = struct.unpack_from('<I', b, p)[0]
            count = struct.unpack_from('<H', b, p+5)[0]
            p += 7 + 12 * count
            assert p <= end
            bindings.append(dict(sourceID=source, propertyID=prop, curveID=curve,
                                 pointCount=count, offset=q, rawHex=b[q:p].hex()))
        child_offset = p
        count = struct.unpack_from('<I', b, p)[0]
        p += 4
        assert p + count * 4 + 23 <= end
        children = list(struct.unpack_from('<' + 'I' * count, b, p))
        p += count * 4
        assert children == o.get('children', [])
        if count:
            assert child_offset == o['childrenOffset']
        meter = b[p:p+23]
        rows.append(dict(bank=x['bank'], id=x['id'], type=x['type'],
                         payloadSha256=o['sha256'], childCountOffset=child_offset,
                         childCount=count, children=children, advancedHex=advanced,
                         stateCount=0, rtpcBindings=bindings, meterOffset=p,
                         meterHex=meter.hex(), meterEnabled=meter[22]))
assert len(rows) == 346
assert max(x['childCount'] for x in rows) == 84
out = dict(archiveSha256=nav['archiveSha256'], objectCount=len(rows),
           typeCounts=dict(collections.Counter(x['type'] for x in rows)),
           maxChildCount=max(x['childCount'] for x in rows),
           childCountHistogram=dict(sorted(collections.Counter(x['childCount'] for x in rows).items())),
           meterEnabledCounts=dict(collections.Counter(x['meterEnabled'] for x in rows)),
           rows=rows,
           boundary='Actual six-bank music type10/12/13 child counts only. Conditional successful reader loop bound; no runtime input admission, callback success, debug-skip configuration, or universal alias absence claim.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k:v for k,v in out.items() if k != 'rows'}, indent=2))
