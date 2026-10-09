"""Raw shipped FX-settings control joins; native FS432 layout, no sample processing."""
from pathlib import Path
import argparse, collections, hashlib, json, struct, zipfile

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--archive', default='../cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip')
    ap.add_argument('--output', default='re-analysis/research/20261009-sound-fx-settings-input-joins.jsonl')
    args = ap.parse_args()
    index = json.loads(Path('re-analysis/research/20261007-sound-reachability-assets.json').read_text(encoding='utf-8-sig'))
    archive = Path(args.archive)
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == index['archiveSha256']
    rows = []
    with zipfile.ZipFile(archive) as z:
        for bank in index['banks']:
            assert hashlib.sha256(z.read(bank['bank'])).hexdigest() == bank['sha256']
        for obj in index['objects']:
            if obj['type'] not in (18, 19):
                continue
            b = z.read(obj['bank'])
            start = obj['payloadOffset']; end = start + obj['length']
            raw = b[start:end]
            assert len(raw) == obj['length'] and hashlib.sha256(raw).hexdigest() == obj['sha256']
            assert b[obj['offset']] == obj['type']
            assert struct.unpack_from('<I', b, obj['offset'] + 1)[0] == obj['length']
            ident, plugin, n = struct.unpack_from('<III', b, start)
            assert ident == obj['id'] and start + 12 + n <= end
            row = dict(bank=obj['bank'], id=ident, type=obj['type'], objectOffset=obj['offset'], payloadOffset=start,
                       length=obj['length'], payloadSha256=obj['sha256'], pluginID=plugin,
                       parameterOffset=start + 12, parameterLength=n, parameterHex=b[start+12:start+12+n].hex())
            p = start + 12 + n
            def take(n):
                nonlocal p
                assert p + n <= end
                pos = p; p += n
                return pos
            def varint():
                nonlocal p
                v = 0; pos = p
                while True:
                    a = b[take(1)]; v = ((v << 7) | (a & 127)) & 0xffffffff
                    if a < 128:
                        return v, pos, b[pos:p].hex()
            count = b[take(1)]; row['properties'] = []
            for _ in range(count):
                q = take(5)
                row['properties'].append(dict(offset=q, key=b[q], rawWord=f'{struct.unpack_from("<I", b, q+1)[0]:08X}'))
            count = struct.unpack_from('<H', b, take(2))[0]; row['bindings'] = []
            for _ in range(count):
                q = take(6); rtpc = struct.unpack_from('<I', b, q)[0]
                param, vp, vb = varint(); k = take(7)
                curve = struct.unpack_from('<I', b, k)[0]; pts = struct.unpack_from('<H', b, k+5)[0]
                points = take(pts*12)
                row['bindings'].append(dict(offset=q, rtpcID=rtpc, kind=b[q+4], mode=b[q+5], parameterID=param,
                                            parameterVarintOffset=vp, parameterVarintHex=vb, curveID=curve,
                                            scaling=b[k+4], pointCount=pts, pointsOffset=points,
                                            pointsHex=b[points:points+pts*12].hex()))
            count = struct.unpack_from('<H', b, take(2))[0]; row['associations'] = []
            for _ in range(count):
                key, q, vb = varint(); v = take(4)
                row['associations'].append(dict(offset=q, key=key, varintHex=vb,
                                                rawWord=f'{struct.unpack_from("<I", b, v)[0]:08X}'))
            assert p == end, (obj['bank'], ident, p, end)
            if plugin == 0x6e0003:
                assert n == 22
                row['nativeParameterReader'] = 'AA2084'
            elif plugin == 0x690003:
                assert n == 56
                row['nativeParameterReader'] = 'AA2E8C'
            rows.append(row)
    Path(args.output).write_text(''.join(json.dumps(x, separators=(',', ':')) + '\n' for x in rows), encoding='utf-8')
    print('Validated', len(rows), 'raw settings; bindings', sum(len(x['bindings']) for x in rows),
          'associations', sum(len(x['associations']) for x in rows))
    print('Known limiter/EQ readers:', dict(collections.Counter(x.get('nativeParameterReader') for x in rows if 'nativeParameterReader' in x)))

if __name__ == '__main__':
    main()
