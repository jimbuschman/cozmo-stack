"""Fixture generator for NativeOkaoOracleTests: a frame sequence (drawn faces at three scales and positions, and faceless
frames) fed in order to one emu_okao.Detector, as video-mode tracking carries state between frames. Expected values come only
from the oracle. Every frame is run with heap/stack poison 0x00 and 0xFF and a difference is refused.

The faces are drawn (no photograph of anyone): an ellipse with hair, brows, eyes, nose and mouth, blurred.

    python -I re-analysis/tools/emu/gen_okao_fixtures.py
writes cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/okao/okao_oracle.json (images inline, base64 of 320x240 grey bytes).
"""
import base64
import hashlib
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

W, H = 320, 240
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'cozmo-stack', 'tests', 'Cozmo.Protocol.Tests',
                   'Fixtures', 'okao', 'okao_oracle.json')


def face(cx=160, cy=125, s=1.0, bg=90, blur=True):
    """A drawn face, pure Python (no numpy/PIL), deterministic bytes."""
    img = [[float(bg) + 20 * math.sin(x / 37.0) for x in range(W)] for y in range(H)]
    a, b = 38 * s, 50 * s

    def ell(px, py, rx, ry, v, extra=None):
        x0, x1 = max(0, int(px - rx) - 1), min(W, int(px + rx) + 2)
        y0, y1 = max(0, int(py - ry) - 1), min(H, int(py + ry) + 2)
        for y in range(y0, y1):
            for x in range(x0, x1):
                if ((x - px) / rx) ** 2 + ((y - py) / ry) ** 2 < 1 and (extra is None or extra(x, y)):
                    img[y][x] = v(x, y) if callable(v) else v

    hair_test = lambda x, y: y < cy - 18 * s
    ell(cx, cy - 8 * s, a * 1.12, b * 1.12, 35, hair_test)
    ell(cx, cy, a, b, lambda x, y: 180 - 40 * ((x - cx) / a) ** 2 - 25 * min(max((y - cy) / b, 0), 1),
        lambda x, y: not (((x - cx) / (a * 1.12)) ** 2 + ((y - cy + 8 * s) / (b * 1.12)) ** 2 < 1 and y < cy - 18 * s))
    ex, ey = 15 * s, cy - 8 * s
    for sx in (-1, 1):
        ell(cx + sx * ex, ey - 11 * s, 11 * s, 2.5 * s, 55)
        ell(cx + sx * ex, ey, 10 * s, 5 * s, 120)
        ell(cx + sx * ex, ey, 7 * s, 3.2 * s, 225)
        ell(cx + sx * ex, ey, 3.2 * s, 3.2 * s, 25)
    ell(cx, cy + 8 * s, 4 * s, 9 * s, 150)
    ell(cx - 4 * s, cy + 16 * s, 2.2 * s, 1.6 * s, 70)
    ell(cx + 4 * s, cy + 16 * s, 2.2 * s, 1.6 * s, 70)
    ell(cx, cy + 28 * s, 13 * s, 3.2 * s, 85)
    if blur:  # 5x5 box blur, twice, integer-rounded each pass
        for _ in range(2):
            src = img
            img = [[0.0] * W for _ in range(H)]
            for y in range(H):
                for x in range(W):
                    acc, n = 0.0, 0
                    for dy in (-2, -1, 0, 1, 2):
                        yy = y + dy
                        if 0 <= yy < H:
                            row = src[yy]
                            for dx in (-2, -1, 0, 1, 2):
                                xx = x + dx
                                if 0 <= xx < W:
                                    acc += row[xx]
                                    n += 1
                    img[y][x] = float(int(acc / n + 0.5))
    return bytes(max(0, min(255, int(v + 0.5))) for row in img for v in row)


def blank(bg=90):
    return bytes(max(0, min(255, int(bg + 20 * math.sin(x / 37.0) + 0.5))) for y in range(H) for x in range(W))


def sequence():
    f0, f1, f2 = face(s=1.0), face(cx=120, cy=110, s=1.3), face(cx=200, cy=140, s=0.8)
    b = blank()
    return ([('blank_a', b)] + [('face_s10_%d' % i, f0) for i in range(4)] + [('blank_b%d' % i, b) for i in range(3)]
            + [('face_s13_%d' % i, f1) for i in range(4)] + [('face_s08_%d' % i, f2) for i in range(4)]
            + [('blank_c%d' % i, b) for i in range(3)])


def main():
    import emu_okao
    seq = sequence()
    runs = []
    for poison in (0x00, 0xFF):
        d = emu_okao.Detector(poison=poison)
        runs.append([d.detect(pix) for _, pix in seq])
    if runs[0] != runs[1]:
        raise SystemExit('poison 0x00 and 0xFF differ: an output depends on uninitialised memory')
    images = {}
    frames = []
    for (name, pix), out in zip(seq, runs[0]):
        key = hashlib.sha256(pix).hexdigest()[:16]
        images.setdefault(key, base64.b64encode(pix).decode())
        frames.append({'name': name, 'image': key, **out})
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'width': W, 'height': H, 'images': images, 'frames': frames}, f, indent=1)
    found = sum(1 for fr in frames if fr['faces'])
    print('%d frames, %d with a face, %d images -> %s' % (len(frames), found, len(images), os.path.normpath(OUT)))


if __name__ == '__main__':
    main()
