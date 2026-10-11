"""Actual authored duck/subscription counts for two saved bus-reader caps."""
from pathlib import Path
import collections
import hashlib
import json
import struct
import zipfile

r = Path(__file__).resolve().parent
nav = json.loads((r / '20261010-sound-bus-ancestor-input-witness.json').read_text())
xs = [x for x in nav['rows'] if x['objectType'] == 8]
archive = r.parents[1].parent / 'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == nav['archiveSha256']
rows = []
with zipfile.ZipFile(archive) as z:
    banks = {name: z.read(name) for name in {x['bank'] for x in xs}}
    for x in xs:
        b = banks[x['bank']]
        start, end = x['payloadOffset'], x['payloadOffset'] + x['payloadLength']
        assert hashlib.sha256(b[start:end]).hexdigest() == x['payloadSha256']
        assert struct.unpack_from('<I', b, start)[0] == x['objectId']
        p = start + 8
        props = b[p]
        prop_hex = b[p:p+1+5*props].hex()
        p += 1 + 5*props
        controls = b[p:p+9].hex()  # BH548A: nine fixed control bytes.
        p += 9
        time, raw, count = struct.unpack_from('<iII', b, p)
        count_offset = p+8
        p += 12
        ducks = [b[p+18*i:p+18*(i+1)].hex() for i in range(count)]
        p += 18*count
        fx_start = p
        fx_count = b[p]
        p += 1
        if fx_count:
            p += 1 + 7*fx_count
        helper = struct.unpack_from('<I', b, p)[0]
        shared = b[p+4]
        p += 5
        flag = b[p]
        subs = struct.unpack_from('<H', b, p+1)[0]
        p += 3
        bindings = []
        for _ in range(subs):
            q = p
            p += 6
            prop = 0
            while True:
                v = b[p]
                p += 1
                prop = ((prop << 7) | (v & 0x7F)) & 0xFFFFFFFF
                if not v & 0x80:
                    break
            points = struct.unpack_from('<H', b, p+5)[0]
            p += 7 + 12*points
            assert p <= end
            bindings.append(dict(offset=q, propertyID=prop, pointCount=points, rawHex=b[q:p].hex()))
        rows.append(dict(id=x['objectId'], bank=x['bank'], payloadSha256=x['payloadSha256'],
                         propertyHex=prop_hex, controlsHex=controls, durationMilliseconds=time,
                         rawWord=raw, duckCountOffset=count_offset, duckCount=count, ducks=ducks,
                         fxCount=fx_count, fxAndHelperHex=b[fx_start:fx_start+1+(1+7*fx_count if fx_count else 0)+5].hex(),
                         helperID=helper, helperShared=shared, flagByte=flag,
                         subscriptionCount=subs, bindings=bindings, tailOffset=p, tailHex=b[p:end].hex()))
assert len(rows) == 15
assert max(x['duckCount'] for x in rows) == 1
assert max(x['subscriptionCount'] for x in rows) == 1
caps = json.loads((r / '20261011-sound-rate-frame-cap-frontiers.json').read_text())['frontiers']
pending = []
for seed in ('009C4094', '009C40C0'):
    for x in next(f for f in caps if f['seed'] == seed)['pending']:
        a, regs = int(x['address'], 16), x['known_registers']
        reason = 'existing NB/BH contract; counter origin or input not established by saved state'
        if a in (0x9C4100, 0x9C4140, 0x9C4148) and int(regs.get('r4', '0'), 16) > 1:
            reason = 'outside authored duck loop count bound'
        if a in (0x9C42B8, 0x9C42C0) and int(regs.get('r6', '0'), 16) > 1:
            reason = 'outside authored subscription loop count bound'
        pending.append(dict(seed=seed, **x, disposition=reason))
assert len(pending) == 14
out = dict(objectCount=15,
           duckCountHistogram=dict(collections.Counter(x['duckCount'] for x in rows)),
           subscriptionCountHistogram=dict(collections.Counter(x['subscriptionCount'] for x in rows)),
           pending=pending, outsideCount=sum(x['disposition'].startswith('outside') for x in pending), rows=rows,
           boundary='Finite bank reader input counts only; tail bytes retained without tracing. No global runtime mutation, callback success or alias absence proof.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print(json.dumps({k:v for k,v in out.items() if k not in ('rows', 'pending')}, indent=2))
