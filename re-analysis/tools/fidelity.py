"""Validate re-analysis/fidelity_manifest.json and regenerate the human-readable gap report.

The manifest is the authoritative statement of what rests on primary source. This script is what
keeps a hand-maintained document from drifting away from it:

    python re-analysis/tools/fidelity.py            # validate and rewrite re-analysis/FIDELITY_GAPS.md
    python re-analysis/tools/fidelity.py --check    # validate only; fail if the report is out of date

The gate it enforces is the one the cleanup was set up around:

    a subsystem may assert source_complete only when it holds no RECOVERABLE_GAP with live_path true.

The same rule is asserted from the test suite (FidelityManifestTests), so a `dotnet test` run fails if
the manifest and the code claims disagree.
"""

import json
import os
import sys
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MANIFEST = os.path.join(ROOT, "re-analysis", "fidelity_manifest.json")
REPORT = os.path.join(ROOT, "re-analysis", "FIDELITY_GAPS.md")

# A gap record has to say enough to be worked on; an EXACT_SOURCE record only has to say what it was read from.
FULL_FIELDS = ("id", "subsystem", "title", "location", "effect", "provenance", "authority",
               "evidence", "status", "unresolved", "hardware_required", "live_path", "test")
SHORT_FIELDS = ("id", "subsystem", "title", "location", "status", "authority", "evidence", "live_path")
GAP = "RECOVERABLE_GAP"


def load():
    with open(MANIFEST, encoding="utf-8") as f:
        return json.load(f)


def validate(m):
    problems = []
    statuses = set(m["statuses"])
    subsystems = {s["id"]: s for s in m["subsystems"]}
    seen = set()

    for r in m["records"]:
        rid = r.get("id", "<no id>")
        if rid in seen:
            problems.append(f"{rid}: duplicate id")
        seen.add(rid)
        if r.get("status") not in statuses:
            problems.append(f"{rid}: unknown status {r.get('status')!r}")
            continue
        if r.get("subsystem") not in subsystems:
            problems.append(f"{rid}: unknown subsystem {r.get('subsystem')!r}")
        required = SHORT_FIELDS if r["status"] == "EXACT_SOURCE" else FULL_FIELDS
        for field in required:
            if field not in r:
                problems.append(f"{rid}: missing field {field!r} (status {r['status']})")
        if r["status"] == GAP and not r.get("unresolved"):
            problems.append(f"{rid}: a RECOVERABLE_GAP has to say what is still unresolved")
        if r["status"] == GAP and r.get("hardware_required"):
            problems.append(f"{rid}: a gap that needs hardware is HARDWARE_ONLY, not RECOVERABLE_GAP")
        loc = r.get("location", "")
        path = loc.split(":", 1)[0]
        if path and not os.path.exists(os.path.join(ROOT, path)):
            problems.append(f"{rid}: location {path} does not exist")

    # The gate.
    open_gaps = defaultdict(list)
    for r in m["records"]:
        if r["status"] == GAP and r.get("live_path"):
            open_gaps[r["subsystem"]].append(r["id"])
    for sid, sub in subsystems.items():
        blocked = open_gaps.get(sid, [])
        if sub.get("source_complete") and blocked:
            problems.append(
                f"{sid}: asserts source_complete while holding live-path gaps {', '.join(blocked)}")
        if not sub.get("source_complete") and not blocked:
            problems.append(
                f"{sid}: has no live-path RECOVERABLE_GAP left and should now assert source_complete")
    return problems


def render(m):
    counts = Counter(r["status"] for r in m["records"])
    subsystems = {s["id"]: s for s in m["subsystems"]}
    by_sub = defaultdict(list)
    for r in m["records"]:
        by_sub[r["subsystem"]].append(r)

    out = []
    w = out.append
    w("# Open fidelity gaps")
    w("")
    w("Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.")
    w("Do not edit by hand: edit the manifest and regenerate, or the two will disagree.")
    w("")
    w(f"Manifest of **{len(m['records'])} records** over {len(m['subsystems'])} subsystems.")
    w("")
    w("| status | records | meaning |")
    w("| --- | ---: | --- |")
    for status, meaning in m["statuses"].items():
        w(f"| {status} | {counts.get(status, 0)} | {meaning} |")
    w("")
    w("## Source-completeness by subsystem")
    w("")
    w("A subsystem is source-complete when nothing on its normal live execution path is a RECOVERABLE_GAP.")
    w("")
    w("| subsystem | records | live-path gaps | source-complete |")
    w("| --- | ---: | ---: | --- |")
    for s in m["subsystems"]:
        rs = by_sub.get(s["id"], [])
        gaps = [r for r in rs if r["status"] == GAP and r.get("live_path")]
        w(f"| {s['id']} — {s['name']} | {len(rs)} | {len(gaps)} | {'yes' if s.get('source_complete') else 'no'} |")
    w("")
    w("## Every RECOVERABLE_GAP")
    w("")
    for s in m["subsystems"]:
        rs = [r for r in by_sub.get(s["id"], []) if r["status"] == GAP]
        if not rs:
            continue
        w(f"### {s['id']} — {s['name']}")
        w("")
        for r in rs:
            live = "live path" if r.get("live_path") else "not on the live path"
            w(f"**{r['id']} — {r['title']}** ({live})")
            w("")
            w(f"* where: `{r['location']}`")
            w(f"* effect: {r['effect']}")
            w(f"* rests on: {r['provenance']}")
            w(f"* best authority: {r['authority']}")
            if r.get("evidence"):
                w(f"* evidence: {'; '.join(r['evidence'])}")
            w(f"* unresolved: {r['unresolved']}")
            w("")
    w("## Items that are not gaps but are not the engine either")
    w("")
    w("| id | subsystem | status | what | why it is not a gap |")
    w("| --- | --- | --- | --- | --- |")
    for r in m["records"]:
        if r["status"] in (GAP, "EXACT_SOURCE"):
            continue
        w(f"| {r['id']} | {r['subsystem']} | {r['status']} | {r['title']} | {r['authority']} |")
    w("")
    return "\n".join(out) + "\n"


def main():
    check_only = "--check" in sys.argv
    m = load()
    problems = validate(m)
    text = render(m)
    if check_only:
        current = open(REPORT, encoding="utf-8").read() if os.path.exists(REPORT) else ""
        if current != text:
            problems.append("FIDELITY_GAPS.md is out of date; run re-analysis/tools/fidelity.py")
    else:
        with open(REPORT, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    if problems:
        print("fidelity manifest: %d problem(s)" % len(problems))
        for p in problems:
            print("  " + p)
        return 1
    counts = Counter(r["status"] for r in m["records"])
    print("fidelity manifest: %d records, %s" % (len(m["records"]), dict(counts)))
    print("open RECOVERABLE_GAP on a live path: %d" %
          sum(1 for r in m["records"] if r["status"] == GAP and r.get("live_path")))
    return 0


if __name__ == "__main__":
    sys.exit(main())
