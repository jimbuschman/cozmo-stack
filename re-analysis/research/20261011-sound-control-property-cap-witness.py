"""Reconcile two paid control-property readers against existing positive bounds."""
from pathlib import Path
import hashlib
import json

r = Path(__file__).resolve().parent
ordinary_path = r / '20261011-sound-property-scan-cap-witness.json'
bus_path = r / '20261011-sound-bus-reader-cap-witness.json'
ordinary = json.loads(ordinary_path.read_text())
bus = json.loads(bus_path.read_text())
assert ordinary['objectCount'] == 3490 and ordinary['maxPropertyCount'] == 4
assert len(bus['rows']) == 15
assert all(bytes.fromhex(x['propertyHex'])[0] == 4 for x in bus['rows'])
frontiers = json.loads((r / '20261011-sound-rate-frame-cap-frontiers.json').read_text())['frontiers']
rows = []
index_addresses = {'009EEC1C': {0x9EEC6C, 0x9EEC50, 0x9EECA0},
                   '00A41E64': {0xA41F58, 0xA41F78, 0xA42134, 0xA42150}}
for seed, addresses in index_addresses.items():
    for x in next(f for f in frontiers if f['seed'] == seed)['pending']:
        index = int(x['known_registers'].get('r0', '0'), 16)
        excluded = int(x['address'], 16) in addresses and index > 4
        if x['address'] == '009EECA0':
            excluded = excluded and x['known_registers'].get('r1') == '00000001'
        rows.append(dict(seed=seed, **x, disposition=('outside authored property index bound' if excluded
                        else 'existing CW/LDRV contract; live input or prior register origin remains explicit')))
assert len(rows) == 14
assert sum(x['disposition'].startswith('outside') for x in rows) == 7
out = dict(ordinaryWitnessSha256=hashlib.sha256(ordinary_path.read_bytes()).hexdigest(),
           busWitnessSha256=hashlib.sha256(bus_path.read_bytes()).hexdigest(),
           nodeObjects=3490, busObjects=15, maxAuthoredPropertyCount=4, pending=rows,
           boundary='Only instruction-specific next-index observations are bounded. Later R0 may be an RTPC return or stale prior value; no blanket register-value exclusion, live blob immutability or all-alias claim.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print('Saved14 states:7 authored property-index contradictions,7 retained existing-contract/input states.')
