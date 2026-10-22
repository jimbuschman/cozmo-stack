#!/usr/bin/env python3
"""What do we already know about an engine address or function?

    python re-analysis/tools/known.py 0x00511990          # an address (any address inside a function works)
    python re-analysis/tools/known.py SetOnCharger        # a function name (substring, case-insensitive)
    python re-analysis/tools/known.py 0x00511990 --all    # list every matching file, not just the first 40

Prints the containing function (from the Ghidra index), then every manifest record, inventory and research file that
cites an address inside that function, newest research first. Run it before tracing anything: earlier passes are
evidence leads to reuse after checking their cited instructions, not to repeat.
"""
import bisect
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RA = ROOT / "re-analysis"
INDEX = RA / "decomp" / "libcozmoEngine" / "index.tsv"
ADDR = re.compile(r"0x([0-9A-Fa-f]{6,8})(?:\s*\.\.\s*(?:0x)?([0-9A-Fa-f]{2,8}))?")


def load_functions():
    rows = []
    for line in INDEX.read_text(encoding="utf-8").splitlines()[1:]:
        entry, size, _status, path, name = line.split("\t", 4)
        rows.append((int(entry, 16), int(size), path, name))
    rows.sort()
    return rows


def containing(funcs, starts, addr):
    i = bisect.bisect_right(starts, addr) - 1
    if i >= 0:
        entry, size, path, name = funcs[i]
        if entry <= addr < entry + max(size, 1):
            return funcs[i]
    return None


def cited_ranges(text):
    """Every cited address or range in text, as (lo, hi)."""
    for m in ADDR.finditer(text):
        lo = int(m.group(1), 16)
        if m.group(2):
            tail = m.group(2)
            hi = int(tail, 16) if len(tail) >= 6 else int(m.group(1)[: len(m.group(1)) - len(tail)] + tail, 16)
            if hi < lo:
                hi = lo
        else:
            hi = lo
        yield lo, hi


def main(argv):
    if len(argv) < 2 or argv[1] in ("-h", "--help"):
        print(__doc__)
        return 2
    query, show_all = argv[1], "--all" in argv
    funcs = load_functions()
    starts = [f[0] for f in funcs]

    if re.fullmatch(r"(0x)?[0-9A-Fa-f]{6,8}", query):
        addr = int(query, 16)
        fn = containing(funcs, starts, addr)
        targets = [fn] if fn else []
        if not fn:
            print(f"0x{addr:08X}: not inside any function in the Ghidra index (data or a gap); matching the address only.")
            lo, hi = addr, addr
    else:
        q = query.lower()
        targets = [f for f in funcs if q in f[3].lower()]
        if not targets:
            print(f"no function name contains {query!r}")
            return 1
        if len(targets) > 12:
            print(f"{len(targets)} functions match {query!r}; showing the first 12. Narrow the name.")
        targets = targets[:12]

    spans = [(f[0], f[0] + max(f[1], 1) - 1) for f in targets] or [(lo, hi)]
    for f in targets:
        print(f"0x{f[0]:08X}  {f[1]} bytes  {f[3]}\n  decomp: re-analysis/decomp/libcozmoEngine/{f[2]} (navigation aid; cite the .so)")

    def hits(text):
        return any(a <= h and l <= b for l, h in cited_ranges(text) for a, b in spans)

    manifest = json.loads((RA / "fidelity_manifest.json").read_text(encoding="utf-8"))
    recs = [r for r in manifest["records"] if hits(json.dumps(r.get("evidence", "")) + " " + str(r.get("unresolved", "")))]
    print(f"\nRecords citing it ({len(recs)}):")
    for r in recs:
        print(f"  {r['id']}  {r['status']}  {r['title'][:100]}")

    files = []
    for folder in ("inventory", "research", "acceptance"):
        for p in (RA / folder).rglob("*.md"):
            try:
                if hits(p.read_text(encoding="utf-8", errors="replace")):
                    files.append(p)
            except OSError:
                pass
    files.sort(key=lambda p: p.name, reverse=True)
    print(f"\nFiles citing it ({len(files)}, newest first):")
    for p in files if show_all else files[:40]:
        print("  " + p.relative_to(ROOT).as_posix())
    if not show_all and len(files) > 40:
        print(f"  ... {len(files) - 40} more (--all)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
