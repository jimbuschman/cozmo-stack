"""Code coverage of the JPEG decode corpus: which parts of the shipped code each J row (re-analysis/research/20261006-M3M4-rows-extraction.md,
M3-001 table J1..J115) cites were executed by the corpus's cases.

The measure is the fraction of the bytes of each cited address range that lie inside a translation block Unicorn executed (so literal
pools count as unexecuted); it is a statement about the corpus, not a proof of anything. A row with 0 % is a branch the corpus does not reach.
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
ROWS = os.path.join(ROOT, 're-analysis', 'research', '20261006-M3M4-rows-extraction.md')
ROWS2 = os.path.join(ROOT, 're-analysis', 'research', '20261010-jpeg-lse-logs-oom-rows.md')     # rows L (LSE), D (engine logs), O (out of memory)

LIBS = {'imgcodecs': 'libopencv_imgcodecs.so', 'imgproc': 'libopencv_imgproc.so', 'core': 'libopencv_core.so', 'engine': 'libcozmoEngine.so'}


# the library each row's addresses belong to (the table does not repeat the library name on every row)
FORCED = {}
for _j in list(range(1, 5)) + [8] + list(range(31, 40)):
    FORCED['J%d' % _j] = 'engine'
for _j in list(range(40, 46)) + [112]:
    FORCED['J%d' % _j] = 'imgproc'
for _j in (7, 115):
    FORCED['J%d' % _j] = 'core'


def parse_rows():
    """J row -> list of (lib, start, end) from the address column."""
    rows = {}
    for line in open(ROWS, encoding='utf-8'):
        m = re.match(r'\| (J\d+) \| (.*?) \|', line)
        if not m:
            continue
        jid, col = m.group(1), m.group(2)
        ranges = []
        for tok in re.finditer(r'0x([0-9A-Fa-f]{3,8})\.\.0x([0-9A-Fa-f]{3,8})', col):
            a, b = int(tok.group(1), 16), int(tok.group(2), 16)
            if b <= a or b - a > 0x20000:
                continue
            lib = FORCED.get(jid, 'imgcodecs')
            if jid == 'J41' and a < 0x400000 and 'imgproc' in col:
                lib = 'imgproc'
            ranges.append((lib, a, b))
        if ranges:
            rows[jid] = ranges
    return rows


def parse_rows2():
    """L / D / O rows: the citation cell (4th) names its library per range with the prefix IMG, CORE or ENG."""
    rows = {}
    names = {'IMG': 'imgcodecs', 'CORE': 'core', 'ENG': 'engine'}
    default = {'L': 'imgcodecs', 'D': 'engine', 'O': 'core'}
    for line in open(ROWS2, encoding='utf-8'):
        cells = [x.strip() for x in line.split('|')]
        if len(cells) < 6 or not re.fullmatch(r'[LDO]\d+b?', cells[1]):
            continue
        rid, cit = cells[1], cells[4]
        ranges = []
        for tok in re.finditer(r'0x([0-9A-Fa-f]{3,8})\.\.0x([0-9A-Fa-f]{3,8})', cit):
            a, b = int(tok.group(1), 16), int(tok.group(2), 16)
            if b <= a or b - a > 0x20000:
                continue
            lib = default[rid[0]]
            for m in re.finditer(r'\b(IMG|CORE|ENG)\b', cit[:tok.start()]):
                lib = names[m.group(1)]
            ranges.append((lib, a, b))
        if ranges:
            rows[rid] = ranges
    return rows


def union(intervals):
    intervals = sorted(intervals)
    out = []
    for a, b in intervals:
        if out and a <= out[-1][1]:
            out[-1][1] = max(out[-1][1], b)
        else:
            out.append([a, b])
    return out


def measure(cases, files):
    sys.path.insert(0, HERE)
    from emu_jpeg_decode import Oracle
    o = Oracle()
    m = o.m
    m.start_coverage(*LIBS.values())
    for case in cases:
        data = files[case['input']]
        k = case['kind']
        if k == 'imdecode':
            o.imdecode(data, case['flags'])
        elif k == 'resize':
            o.resize(case['srows'], case['scols'], case['schannels'], data, case['dcols'], case['drows'])
        else:
            o.decode(case['encoding'], data, case['gray'])
    per_lib = {}
    for libkey, name in LIBS.items():
        base = m.libs[name].base
        iv = [(a - base, a - base + size) for a, size in m.cov.items() if base <= a < base + m.libs[name].end]
        per_lib[libkey] = union(iv)
    result = {}
    all_rows = dict(parse_rows())
    all_rows.update(parse_rows2())
    for jid, ranges in all_rows.items():
        tot = cov = 0
        detail = []
        for lib, a, b in ranges:
            ivs = per_lib[lib]
            c = 0
            for x, y in ivs:
                lo, hi = max(a, x), min(b, y)
                if hi > lo:
                    c += hi - lo
            tot += b - a
            cov += c
            detail.append('%s %#x..%#x %d%%' % (lib, a, b, round(100 * c / (b - a))))
        result[jid] = {'percent': round(100 * cov / tot, 1) if tot else None, 'ranges': detail}
    return result


if __name__ == '__main__':
    for k, v in parse_rows().items():
        print(k, v)
