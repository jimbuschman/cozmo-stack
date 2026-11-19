"""Extract numeric event evidence from a private LLM-session capture.

This is a capture cross-check, not a native-engine oracle. Producer rules remain
those of the shipped engine; raw images, frames, names and hardware bundles are
never written to the output. Engine addresses are documented in the handoff.
"""
import argparse
import collections
import hashlib
import json
import struct
from pathlib import Path

TAGS = {0x46: 'face-seen', 0x47: 'face-id-changed', 0x4B: 'face-lost',
        0x44: 'object-seen', 0x0F: 'cube-moved', 0x10: 'cube-moved',
        0x13: 'cube-tapped', 0x52: 'object-pose-lost'}


def decode(tag, data):
    """CLAD field order, with float32 payloads retained as numeric bit patterns."""
    def words(offset, count):
        return list(struct.unpack_from('<' + 'I' * count, data, offset))
    if tag == 0x46:
        face, timestamp = struct.unpack_from('<iI', data)
        at = 56
        size = data[at]
        at += 1
        name = data[at:at + size]
        at += size
        expression = struct.unpack_from('<b', data, at)[0]
        at += 1
        attrs = []
        for _ in range(3):
            attrs.append([data[at], *words(at + 1, 2)])
            at += 9
        scores = list(data[at:at + 5])
        at += 5
        parts = []
        for _ in range(4):
            count = data[at]
            at += 1
            parts.append(words(at, 2 * count))
            at += 8 * count
        assert at == len(data), (tag, at, len(data))
        return dict(faceId=face, timestamp=timestamp, poseBits=words(8, 7),
                    originId=words(36, 1)[0], rectBits=words(40, 4),
                    nameLength=size, nameSha256=hashlib.sha256(name).hexdigest(),
                    expression=expression, attributes=attrs, expressionScores=scores,
                    partsBits=parts)
    if tag == 0x47:
        assert len(data) == 8
        old, new = struct.unpack('<ii', data)
        return dict(oldId=old, newId=new)
    if tag == 0x4B:
        assert len(data) == 4
        return dict(faceId=struct.unpack('<i', data)[0])
    if tag == 0x44:
        assert len(data) == 69
        timestamp, family, kind, obj = struct.unpack_from('<4I', data)
        return dict(timestamp=timestamp, family=family, type=kind, objectId=obj,
                    rectBits=words(16, 4), poseBits=words(32, 7),
                    originId=words(60, 1)[0], topMarkerAngleBits=words(64, 1)[0],
                    isActive=data[68])
    if tag in (0x0F, 0x10):
        assert len(data) == (21 if tag == 0x0F else 8)
        timestamp, obj = struct.unpack_from('<II', data)
        result = dict(timestamp=timestamp, objectId=obj, moving=tag == 0x0F)
        if tag == 0x0F:
            result.update(accelBits=words(8, 3), upAxis=data[20])
        return result
    if tag == 0x13:
        assert len(data) == 12
        timestamp, obj, taps, time, neg, pos = struct.unpack('<IIBBbb', data)
        return dict(timestamp=timestamp, objectId=obj, numTaps=taps,
                    tapTime=time, tapNeg=neg, tapPos=pos)
    if tag == 0x52:
        assert len(data) == 4
        return dict(objectId=struct.unpack('<I', data)[0])
    raise ValueError(tag)


def pack_live(entry):
    """Independent CLAD body projection of a polled JSON event (not its producer)."""
    kind, p = entry['type'], entry['payload']
    data = bytearray()
    missing = []
    def put(fmt, *values):
        data.extend(struct.pack('<' + fmt, *values))
    def floats(obj, *keys):
        put('f' * len(keys), *(float(obj[k]) for k in keys))
    def pose(obj):
        floats(obj, 'x', 'y', 'z', 'q0', 'q1', 'q2', 'q3')
        put('I', obj['originId'])
    if kind in ('face-seen', 'face-recognised'):
        put('iI', p['faceId'], p['timestamp'])
        pose(p['pose'])
        floats(p['rect'], 'x', 'y', 'width', 'height')
        name = p['name'].encode('utf-8')
        put('B', len(name))
        data.extend(name)
        put('b', p['expression'])
        for key in ('smile', 'gaze', 'blink'):
            attr = p[key]
            put('Bff', attr['valid'], attr['first'], attr['second'])
        data.extend(p['expressionScores'])
        for key in ('leftEye', 'rightEye', 'nose', 'mouth'):
            put('B', len(p[key]))
            for point in p[key]:
                floats(point, 'x', 'y')
        tag = 0x46
    elif kind == 'face-id-changed':
        put('ii', p['oldId'], p['newId'])
        tag = 0x47
    elif kind == 'face-lost':
        put('i', p['faceId'])
        tag = 0x4B
    elif kind in ('cube-seen', 'charger-seen'):
        put('IIII', *(p[k] for k in ('timestamp', 'family', 'type', 'objectId')))
        floats(p, 'imageX', 'imageY', 'imageWidth', 'imageHeight')
        pose(p['pose'])
        floats(p, 'topOrientationRad')
        put('B', p['isActive'])
        tag = 0x44
    elif kind in ('cube-moved', 'cube-tapped'):
        cube = p['cube']
        put('II', p['timestamp'], cube['worldObjectId'])
        if kind == 'cube-tapped':
            tap = cube['lastTap']
            put('BBbb', *(tap[k] for k in ('numTaps', 'tapTime', 'tapNeg', 'tapPos')))
            tag = 0x13
        elif cube['moving']:
            floats(cube['accel'], 'x', 'y', 'z')
            if 'axisOfAccel' not in p:
                missing.append('axisOfAccel')
            put('B', p.get('axisOfAccel', 0))
            tag = 0x0F
        else:
            tag = 0x10
    elif kind == 'object-pose-lost':
        put('I', p['objectId'])
        tag = 0x52
    else:
        return None
    return tag, bytes(data), missing


def extract(bundle):
    frames = bundle / 'frames.jsonl'
    frame_hash = hashlib.sha256()
    frame_lines = frame_bytes = 0
    end = None
    with frames.open('rb') as source:
        for line in source:
            frame_hash.update(line)
            frame_bytes += len(line)
            frame_lines += 1
            row = json.loads(line)
            end = max(end or 0, row['atMs'])
    assert end is not None
    rows, clocks, feeds = [], [], {}
    prefix_hash = hashlib.sha256()
    prefix_bytes = prefix_lines = 0
    seed = None
    tools = []
    # The live server can keep appending idle output after the finite frame file.
    # Snapshot its current length and retain only the recorded-frame time range.
    event_path = bundle / 'events.jsonl'
    with event_path.open('rb') as source:
        remaining = event_path.stat().st_size
        while remaining:
            line = source.readline(remaining)
            remaining -= len(line)
            if not line:
                break
            if not line.endswith(b'\n'):
                raise ValueError('snapshot ends in an incomplete event row')
            row = json.loads(line)
            if row['atMs'] > end:
                continue
            prefix_hash.update(line)
            prefix_lines += 1
            prefix_bytes += len(line)
            kind = row['kind']
            if kind == 'engine-log' and row['name'] == 'app.random_seed':
                seed = int(row['text'])
            if kind == 'mcp-input' and row.get('method') == 'tools/call':
                tools.append(dict(atMs=row['atMs'], id=row['id'],
                                  name=row['params']['name']))
            if kind == 'mcp-output':
                for event in row.get('result', {}).get('structuredContent', {}).get('events', []):
                    feeds[event['cursor']] = event
            if kind != 'engine-to-game':
                continue
            tag = int(row['tag'], 16)
            if tag == 0x61:
                clocks.append(row['atMs'])
            if tag not in TAGS:
                continue
            data = bytes.fromhex(row['bodyHex'])
            fields = decode(tag, data)
            name = TAGS[tag]
            if tag == 0x44:
                name = 'cube-seen' if fields['family'] == 2 else 'charger-seen'
            rows.append(dict(atMs=row['atMs'], tag=tag, kind=name,
                             bodySha256=hashlib.sha256(data).hexdigest(), **fields))
    # Check only events actually polled live, preserving their cursor order.
    # Engine rows outside the two poll windows are not missing deliveries.
    delivered = []
    previous_index = -1
    for cursor, event in sorted(feeds.items()):
        projected = pack_live(event)
        if projected is None:
            continue
        tag, body, missing = projected
        fields = decode(tag, body)
        candidates = [(i, row) for i, row in enumerate(rows) if row['tag'] == tag
                      and all(row.get(k) == fields[k] for k in
                              ('timestamp', 'faceId', 'oldId', 'newId', 'objectId') if k in fields)]
        digest = hashlib.sha256(body).hexdigest()
        later = [(i, row) for i, row in candidates if i > previous_index]
        exact = [(i, row) for i, row in later if row['bodySha256'] == digest]
        if not later:
            raise ValueError(('live event has no later engine occurrence', cursor, len(candidates)))
        index, expected = (exact or later)[0]
        # The old bridge omits the axis. Do not substitute a default and call
        # the payload correct, even when its value happens to be zero.
        delivered.append(dict(cursor=cursor, kind=event['type'], engineIndex=index,
                              orderPreserved=index > previous_index,
                              missingFields=missing, bodySha256=digest,
                              matchesPackedBody=not missing and digest == expected['bodySha256']))
        previous_index = index
    counts = collections.Counter(row['kind'] for row in rows)
    counts['face-recognised'] = sum(row.get('nameLength', 0) != 0 for row in rows if row['tag'] == 0x46)
    return dict(schema=1, authority='recorded-stack cross-check only; not native source authority',
                framesSha256=frame_hash.hexdigest(), framesBytes=frame_bytes,
                framesLines=frame_lines, frameEndMs=end,
                eventsWithinFramesSha256=prefix_hash.hexdigest(),
                eventsWithinFramesBytes=prefix_bytes, eventsWithinFramesLines=prefix_lines,
                seed=seed, recordedMoodTicksMs=clocks, counts=dict(counts),
                livePolledCounts=dict(collections.Counter(event['type'] for event in feeds.values())),
                liveDelivery=delivered, tools=tools, engineEvents=rows)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('bundle', type=Path)
    parser.add_argument('--out', required=True, type=Path)
    args = parser.parse_args()
    result = extract(args.bundle)
    lines = ['{']
    entries = list(result.items())
    for index, (key, value) in enumerate(entries):
        comma = ',' if index + 1 < len(entries) else ''
        if key in ('engineEvents', 'liveDelivery'):
            lines.append('  ' + json.dumps(key) + ': [')
            lines.extend('    ' + json.dumps(row, separators=(',', ':')) + (',' if i + 1 < len(value) else '') for i, row in enumerate(value))
            lines.append('  ]' + comma)
        elif key == 'recordedMoodTicksMs':
            lines.append('  ' + json.dumps(key) + ': [')
            for start in range(0, len(value), 16):
                chunk = value[start:start + 16]
                lines.append('    ' + ','.join(json.dumps(v) for v in chunk) + (',' if start + 16 < len(value) else ''))
            lines.append('  ]' + comma)
        else:
            lines.append('  ' + json.dumps(key) + ': ' + json.dumps(value, separators=(',', ':')) + comma)
    lines.append('}')
    args.out.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(json.dumps({k: result[k] for k in ('frameEndMs', 'framesLines', 'counts', 'livePolledCounts')}))
