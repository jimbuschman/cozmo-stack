"""Two original mode4 nodes: primary serialized state/RTPC registration inputs.

The successful native reader order, not a generic zero-byte search, supplies
the offsets. No runtime graph immutability or inherited subscription is claimed.
"""
from pathlib import Path
import hashlib, json, struct, zipfile

r = Path(__file__).resolve().parent
root = r.parents[1]
nav = json.loads((r / '20261007-sound-reachability-assets.json').read_text(encoding='utf-8-sig'))
archive = root.parent / 'cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == nav['archiveSha256']
rows = []
with zipfile.ZipFile(archive) as z:
    for ident in (725225627, 196431345):
        o = next(o for o in nav['objects'] if o['id'] == ident)
        assert o['type'] == 5
        bank = z.read(o['bank'])
        p = bank[o['payloadOffset']:o['payloadOffset'] + o['length']]
        assert hashlib.sha256(p).hexdigest() == o['sha256']
        assert struct.unpack_from('<I', p)[0] == ident
        # CF3: zero FX count consumes only inheritance/count.
        assert p[4:6] == bytes(2)
        # NB2/3: flag, first parent, second parent, common flag.
        first, second = struct.unpack_from('<II', p, 7)
        assert p[6] == p[15] == 0 and first == 0
        # Actual table103B860 slots104/108/10C/110/114:
        # CF1/2 two zero counts; positioning bit0 clear (CF8);
        # aux flags0 consumes one byte (CF9); advanced reader six bytes.
        assert p[16:18] == bytes(2) and p[18] == 0xC0 and p[19] == 0
        advanced = p[20:26]
        assert advanced == bytes.fromhex('000100000000')
        groups = struct.unpack_from('<I', p, 26)[0]
        rtpcs = struct.unpack_from('<H', p, 30)[0]
        assert groups == rtpcs == 0
        assert o['payloadOffset'] + 32 == o['childrenOffset'] - 24
        rows.append(dict(id=ident, bank=o['bank'], payloadOffset=o['payloadOffset'],
                         payloadSha256=o['sha256'], prefixHex=p[:32].hex(),
                         secondParentID=second, advancedHex=advanced.hex(),
                         stateGroupCountOffset=o['payloadOffset']+26, stateGroupCount=groups,
                         rtpcCountOffset=o['payloadOffset']+30, rtpcCount=rtpcs,
                         ranseqHeaderOffset=o['payloadOffset']+32))
out = dict(archiveSha256=nav['archiveSha256'], rows=rows,
           boundary='Successful bank reader creates no own state-group or RTPC registrations from these two payload sections. Ancestor/inherited and later runtime registrations, callbacks and direct-node admission remain unproved.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print(json.dumps(out, indent=2))
