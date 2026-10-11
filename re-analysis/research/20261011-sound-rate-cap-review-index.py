"""Index every saved cap state against bounded source/input dispositions.

No rerun of executable navigation; no claim that retained states are unreachable.
"""
from pathlib import Path
import collections
import hashlib
import json

r = Path(__file__).resolve().parent
baseline = json.loads((r / '20261011-sound-rate-frame-cap-frontiers.json').read_text())
decisions = {}
def identity(seed, x):
    return (seed, x['address'], tuple(sorted(x['known_registers'].items())), x['nzcv_mask'])
def add(seed, x, reason, evidence):
    key = identity(seed, x)
    if key in decisions:
        assert decisions[key] == (reason, evidence)
    decisions[key] = reason, evidence

for name, field, seed, cp in (
    ('music-marker', 'runtimePending', '00989FF4', 828),
    ('music-marker', 'parserPending', '0098A190', 828),
    ('envelope-property', 'savedPending', '009D5974', 833),
    ('action-property', 'savedPending', '00A613F8', 832),
    ('preplay', 'preplayPending', '009F12E8', 834),
):
    path = r / ('20261011-sound-' + name + '-cap-witness.json')
    for x in json.loads(path.read_text())[field]:
        reason = x.get('disposition', 'outside authored incoming marker count bound')
        add(seed, x, reason, cp)
for name, cp in (('bus-reader',829), ('control-property',831)):
    for x in json.loads((r / ('20261011-sound-' + name + '-cap-witness.json')).read_text())['pending']:
        add(x['seed'], x, x['disposition'], cp)

rows = []
for f in baseline['frontiers']:
    reasons = []
    for x in f['pending']:
        key = identity(f['seed'], x)
        if f['seed'] in ('00A7255C', '00A73DBC'):
            reason, cp = 'outside retained channel branch/count bound', 826
        elif f['seed'] == '009877F8':
            reason, cp = ('outside authored music child loop count bound' if x['address'] == '009879C8'
                          else 'existing MPAY header contract; legitimate saved read'), 827
        elif f['seed'] == '009EDF5C':
            reason, cp = 'outside authored ordinary property next-index bound', 830
        elif f['seed'] == '009E6D2C':
            reason, cp = 'existing MTK contract; live registry input not excluded', 830
        elif f['seed'] == '009EBE6C':
            reason, cp = 'existing DF contract; live device/sink input not excluded', 834
        else:
            assert key in decisions, key
            reason, cp = decisions[key]
        kind = ('source-branch/count' if f['seed'] in ('00A7255C','00A73DBC') else
                'conditional-authored-count' if reason.startswith('outside') else
                'conditional-authored-ID' if reason.startswith('matched3B') else
                'retained-contract/input')
        reasons.append(dict(**x, kind=kind, disposition=reason, checkpoint=cp))
    assert len(reasons) == f['pending_count']
    rows.append(dict(seed=f['seed'], savedCount=len(reasons), states=reasons))
histogram = collections.Counter(x['kind'] for f in rows for x in f['states'])
assert len(rows) == 15 and sum(histogram.values()) == 231
assert histogram == {'source-branch/count':3, 'conditional-authored-count':59,
                     'conditional-authored-ID':13, 'retained-contract/input':156}
out = dict(windowCount=15, stateCount=231, dispositionCounts=dict(histogram), windows=rows,
           baselineSha256=hashlib.sha256((r / '20261011-sound-rate-frame-cap-frontiers.json').read_bytes()).hexdigest(),
           boundary='Index only; no changed baseline events,seeds,state bound or universal alias/admission proof.59 counts/13 IDs depend on current authored input identity;156 retained states remain explicit.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print(json.dumps({k:v for k,v in out.items() if k != 'windows'}, indent=2))
