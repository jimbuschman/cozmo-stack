"""Pilot (manager, 2026-10-16): check engine-log keys in the stack against the strings in libcozmoEngine.so.

What it can establish: that an identifier-like literal passed to an engine-log call (event key, channel) exists as a
NUL-terminated string in the binary (.rodata, or inline in .text after Thumb code). It cannot establish level, branch,
order, arguments or whether the log should happen at all, so it is a pre-check, never a verdict.

Scope: identifier-like string literals (Name or Name.Part[.Part...], no spaces) inside the argument list of any call
whose arguments name an EngineLogLevel - the signature every engine-log path and wrapper in the stack carries.
Each is classified:
  EXACT    - a whole NUL-terminated string in the binary
  COMPOSED - absent as a whole, but a prefix ending in '.' is a string (runtime-composed key; review by hand)
  PART     - occurs only inside a longer printable string (usually inline .text after printable code bytes)
  ABSENT   - occurs nowhere in the binary: a likely wrong or invented key

Usage: python -I re-analysis/tools/logkey_check.py [--rev <git-rev>] [--all] [path-prefix...]
  --rev  read the C# files from a git revision instead of the working tree (for tests on historical commits)
  --all  also list EXACT keys
Exit code 1 if any key is ABSENT.
"""
import os
import re
import subprocess
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
SO = os.path.join(ROOT, 'resources', 'lib', 'armeabi-v7a', 'libcozmoEngine.so')
CALL = re.compile(r'\b([A-Za-z_]\w*)\s*(\?\.\s*Invoke\s*)?\(')
LEVEL = 'EngineLogLevel.'
LIT = re.compile(r'(?<![@$])"((?:[^"\\]|\\.)*)"')
KEY = re.compile(r'^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*$')
NOT_KEY = re.compile(r'\.(json|bin|bnk|wem|txt|png|jpg|cs|md|so|xml|pck)$', re.I)
NUL = bytes([0])


def binary():
    data = open(SO, 'rb').read()
    whole = set(s.decode('ascii') for s in data.split(NUL) if len(s) >= 3 and all(32 <= c < 127 for c in s))
    return whole, data


def classify(s, whole, data):
    if s in whole:
        return 'EXACT'
    k = s.encode()
    for m in re.finditer(re.escape(k) + NUL, data):
        i = m.start()
        if i == 0 or not (32 <= data[i - 1] < 127):
            return 'EXACT'
    if k in data:
        return 'PART'
    for i, c in enumerate(s):
        if c == '.' and s[:i + 1] in whole:
            return 'COMPOSED'
    return 'ABSENT'


def call_args(text, start):
    depth, i, n = 1, start, len(text)
    while i < n and depth:
        c = text[i]
        if c == '"':
            j = i + 1
            while j < n and text[j] != '"':
                j += 2 if text[j] == '\\' else 1
            i = j + 1
            continue
        if c == '(':
            depth += 1
        elif c == ')':
            depth -= 1
        i += 1
    return text[start:i - 1]


def sources(rev, prefixes):
    def wanted(rel):
        return rel.endswith('.cs') and (not prefixes or any(rel.startswith(p) for p in prefixes))
    if rev:
        names = subprocess.run(['git', '-C', ROOT, 'ls-tree', '-r', '--name-only', rev, 'cozmo-stack/src'],
                               capture_output=True, text=True, check=True).stdout.split()
        for f in names:
            if wanted(f):
                yield f, subprocess.run(['git', '-C', ROOT, 'show', f'{rev}:{f}'], capture_output=True,
                                        check=True).stdout.decode('utf-8', 'replace')
    else:
        base = os.path.join(ROOT, 'cozmo-stack', 'src')
        for d, _, fs in os.walk(base):
            for f in fs:
                p = os.path.join(d, f)
                rel = os.path.relpath(p, ROOT).replace(os.sep, '/')
                if wanted(rel):
                    yield rel, open(p, encoding='utf-8', errors='replace').read()


def main():
    args = sys.argv[1:]
    rev = None
    if '--rev' in args:
        k = args.index('--rev')
        rev = args[k + 1]
        del args[k:k + 2]
    show_all = '--all' in args
    prefixes = [a for a in args if a != '--all']
    whole, data = binary()
    counts = {'EXACT': 0, 'COMPOSED': 0, 'PART': 0, 'ABSENT': 0}
    rows = []
    for rel, text in sources(rev, prefixes):
        # blank out // comments (same length, so line numbers hold); doc comments name members with cref="..."
        text = re.sub(r'//[^\n]*', lambda c: ' ' * len(c.group()), text)
        seen = set()
        for m in CALL.finditer(text):
            argtext = call_args(text, m.end())
            if LEVEL not in argtext:
                continue
            for lm in LIT.finditer(argtext):
                pos = m.end() + lm.start()
                if pos in seen:
                    continue
                seen.add(pos)
                s = lm.group(1)
                if not KEY.match(s) or NOT_KEY.search(s) or len(s) < 3:
                    continue
                cls = classify(s, whole, data)
                counts[cls] += 1
                rows.append((cls, rel, text.count('\n', 0, pos) + 1, s))
    order = {'ABSENT': 0, 'COMPOSED': 1, 'PART': 2, 'EXACT': 3}
    for cls, rel, line, s in sorted(rows, key=lambda r: (order[r[0]], r[1], r[2])):
        if cls != 'EXACT' or show_all:
            print(f'{cls:8} {rel}:{line}  {s}')
    print(f'keys: {sum(counts.values())}  EXACT {counts["EXACT"]}  COMPOSED {counts["COMPOSED"]}  '
          f'PART {counts["PART"]}  ABSENT {counts["ABSENT"]}')
    return 1 if counts['ABSENT'] else 0


if __name__ == '__main__':
    sys.exit(main())
