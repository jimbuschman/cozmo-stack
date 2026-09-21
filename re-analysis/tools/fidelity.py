"""Validate re-analysis/fidelity_manifest.json and regenerate the human-readable gap report.

The manifest is the authoritative statement of what rests on primary source. This script is what
keeps a hand-maintained document from drifting away from it:

    python re-analysis/tools/fidelity.py            # validate and rewrite re-analysis/FIDELITY_GAPS.md
    python re-analysis/tools/fidelity.py --check    # validate only; fail if the report is out of date

It enforces two separate gates, and neither may be answered by the other:

    a subsystem's SOURCE INVESTIGATION is exhausted only when it holds no RECOVERABLE_GAP
    on a live execution path - nothing left that reading the original would settle;

    a subsystem's IMPLEMENTATION FIDELITY is complete only when it holds no IMPLEMENTATION_GAP
    there - nothing the original is known to do that this stack knowingly does not.

Neither means the behaviour is faithfully reproduced. BLOCKED_EXTERNAL and HARDWARE_ONLY items can
remain in both cases, so they are counted per subsystem rather than folded into a single word.

The same rules are asserted from the test suite (FidelityManifestTests), so a `dotnet test` run fails
if the manifest and the code claims disagree.
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

RECOVERABLE = "RECOVERABLE_GAP"
IMPLEMENTATION = "IMPLEMENTATION_GAP"
#: Statuses that are outstanding work rather than a settled answer. Both block their own gate.
OPEN = (RECOVERABLE, IMPLEMENTATION)


def load():
    with open(MANIFEST, encoding="utf-8") as f:
        return json.load(f)


def live(records, subsystem, status):
    return [r["id"] for r in records
            if r["subsystem"] == subsystem and r["status"] == status and r.get("live_path")]


def validate(m):
    problems = []
    statuses = set(m["statuses"])
    subsystems = {s["id"]: s for s in m["subsystems"]}
    records = m["records"]
    seen = set()

    for r in records:
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
        if r["status"] in OPEN and not r.get("unresolved"):
            problems.append(f"{rid}: a {r['status']} has to say what is still outstanding")
        if r["status"] == RECOVERABLE and r.get("hardware_required"):
            problems.append(f"{rid}: a gap that needs hardware is HARDWARE_ONLY, not RECOVERABLE_GAP")
        if r["status"] == IMPLEMENTATION and r.get("hardware_required"):
            problems.append(f"{rid}: an IMPLEMENTATION_GAP is work to do here, so it cannot need hardware")
        loc = r.get("location", "")
        path = loc.split(":", 1)[0]
        if path and not os.path.exists(os.path.join(ROOT, path)):
            problems.append(f"{rid}: location {path} does not exist")

    # The two gates, each checked in both directions so a flag cannot be set early or left stale.
    for sid, sub in subsystems.items():
        for flag, status, what in (("source_investigation_exhausted", RECOVERABLE, "reverse engineering"),
                                   ("implementation_fidelity_complete", IMPLEMENTATION, "implementation")):
            blocking = live(records, sid, status)
            if flag not in sub:
                problems.append(f"{sid}: missing flag {flag!r}")
                continue
            if sub[flag] and blocking:
                problems.append(f"{sid}: claims {flag} while holding live-path {status} {', '.join(blocking)}")
            if not sub[flag] and not blocking:
                problems.append(f"{sid}: has no live-path {status} left, so {flag} should be true "
                                f"(outstanding {what} is what that flag tracks, and nothing else)")
    return problems


def render(m):
    counts = Counter(r["status"] for r in m["records"])
    records = m["records"]
    by_sub = defaultdict(list)
    for r in records:
        by_sub[r["subsystem"]].append(r)

    out = []
    w = out.append
    w("# Open fidelity gaps")
    w("")
    w("Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.")
    w("Do not edit by hand: edit the manifest and regenerate, or the two will disagree.")
    w("")
    w(f"Manifest of **{len(records)} records** over {len(m['subsystems'])} subsystems.")
    w("")
    w("| status | records | meaning |")
    w("| --- | ---: | --- |")
    for status, meaning in m["statuses"].items():
        w(f"| {status} | {counts.get(status, 0)} | {meaning} |")
    w("")
    w("## Where each subsystem stands")
    w("")
    w("Four separate things, because one word cannot carry them. **Source read** means nothing is left that")
    w("reading the original would settle. **Built** means nothing the original is known to do is knowingly")
    w("not done here. Neither says the behaviour is faithfully reproduced: the last two columns are what")
    w("remains after both, and they do not go away by working harder on this repository.")
    w("")
    w("| subsystem | records | to read | to build | blocked externally | needs hardware | source read | built |")
    w("| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |")
    for s in m["subsystems"]:
        rs = by_sub.get(s["id"], [])
        def n(status):
            return len(live(records, s["id"], status))
        w(f"| {s['id']} — {s['name']} | {len(rs)} | {n(RECOVERABLE)} | {n(IMPLEMENTATION)} | "
          f"{n('BLOCKED_EXTERNAL')} | {n('HARDWARE_ONLY')} | "
          f"{'yes' if s['source_investigation_exhausted'] else 'no'} | "
          f"{'yes' if s['implementation_fidelity_complete'] else 'no'} |")
    w("")

    for status, heading, lead in (
        (RECOVERABLE, "Still to read: every RECOVERABLE_GAP",
         "Each of these is a question the original can answer and nobody has asked it yet."),
        (IMPLEMENTATION, "Still to build: every IMPLEMENTATION_GAP",
         "Each of these is a question already answered. The original's behaviour is established and this "
         "stack knowingly does something else, so the work outstanding is writing it, not reading."),
    ):
        w(f"## {heading}")
        w("")
        w(lead)
        w("")
        for s in m["subsystems"]:
            rs = [r for r in by_sub.get(s["id"], []) if r["status"] == status]
            if not rs:
                continue
            w(f"### {s['id']} — {s['name']}")
            w("")
            for r in rs:
                live_note = "live path" if r.get("live_path") else "not on the live path"
                w(f"**{r['id']} — {r['title']}** ({live_note})")
                w("")
                w(f"* where: `{r['location']}`")
                w(f"* effect: {r['effect']}")
                w(f"* rests on: {r['provenance']}")
                w(f"* best authority: {r['authority']}")
                if r.get("evidence"):
                    w(f"* evidence: {'; '.join(r['evidence'])}")
                w(f"* outstanding: {r['unresolved']}")
                w("")

    w("## What remains after both: blocked externally, or needing hardware")
    w("")
    w("| id | subsystem | status | what | why it cannot be settled here |")
    w("| --- | --- | --- | --- | --- |")
    for r in records:
        if r["status"] not in ("BLOCKED_EXTERNAL", "HARDWARE_ONLY"):
            continue
        w(f"| {r['id']} | {r['subsystem']} | {r['status']} | {r['title']} | {r['unresolved']} |")
    w("")
    w("## Settled differences: equivalent implementations and kept policies")
    w("")
    w("| id | subsystem | status | what | why it is settled |")
    w("| --- | --- | --- | --- | --- |")
    for r in records:
        if r["status"] not in ("EQUIVALENT_IMPLEMENTATION", "COMPATIBILITY_POLICY"):
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
    for status, label in ((RECOVERABLE, "to read"), (IMPLEMENTATION, "to build")):
        n = sum(1 for r in m["records"] if r["status"] == status and r.get("live_path"))
        print("  live-path %s (%s): %d" % (status, label, n))
    return 0


if __name__ == "__main__":
    sys.exit(main())
