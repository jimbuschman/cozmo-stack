"""Validate re-analysis/fidelity_manifest.json and regenerate the human-readable gap report.

The manifest is the authoritative statement of what rests on primary source. This script is what
keeps a hand-maintained document from drifting away from it:

    python re-analysis/tools/fidelity.py            # validate and rewrite re-analysis/FIDELITY_GAPS.md
    python re-analysis/tools/fidelity.py --check    # validate only; fail if the report is out of date
    python re-analysis/tools/fidelity.py --approve M1-transport   # the operator approved that inventory

It enforces two separate gates, and neither may be answered by the other:

    a subsystem's SOURCE INVESTIGATION is exhausted only when it holds no RECOVERABLE_GAP
    on a live execution path - nothing left that reading the original would settle;

    a subsystem's IMPLEMENTATION FIDELITY is complete only when it holds no IMPLEMENTATION_GAP
    there - nothing the original is known to do that this stack knowingly does not.

Neither means the behaviour is faithfully reproduced. BLOCKED_EXTERNAL and HARDWARE_ONLY items can
remain in both cases, so they are counted per subsystem rather than folded into a single word.

The same rules are asserted from the test suite (FidelityManifestTests), so a `dotnet test` run fails
if the manifest and the code claims disagree.

It also enforces the evidence process in AGENTS.md, which is separate from both gates:

    every subsystem carries a review state. UNREVIEWED means its records were written before the
    process and nothing here vouches for them. INVENTORY_APPROVED and ACCEPTED subsystems must have an
    inventory that names every record, a snapshot taken when the operator approved it, a concrete
    citation (an address or a file) behind every settled record, and a `// fidelity: <id>` tag in the
    file each record points at. The snapshot freezes the evidence: after approval the only status change
    allowed is an IMPLEMENTATION_GAP being built; anything else goes back through the Extractor and a
    new approval (`--approve <subsystem>` rewrites the snapshot, and only the operator authorises that);

    verification (capture or hardware) is a separate field and never changes a status;

    every `// fidelity:` tag anywhere in cozmo-stack names a record that exists.
"""

import hashlib
import json
import os
import re
import sys
from collections import Counter, defaultdict
from datetime import date

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MANIFEST = os.path.join(ROOT, "re-analysis", "fidelity_manifest.json")
REPORT = os.path.join(ROOT, "re-analysis", "FIDELITY_GAPS.md")
CODE_ROOT = os.path.join(ROOT, "cozmo-stack")

UNREVIEWED, APPROVED, ACCEPTED = "UNREVIEWED", "INVENTORY_APPROVED", "ACCEPTED"
REVIEW_STATES = (UNREVIEWED, APPROVED, ACCEPTED)
#: Statuses that settle a behaviour as reproduced, and so need a citation someone can open.
SETTLED = ("EXACT_SOURCE", "EQUIVALENT_IMPLEMENTATION")
#: Record fields the operator approves with the inventory; none may change until the next approval.
FROZEN = ("title", "authority", "evidence", "live_path", "hardware_required")
TAG = re.compile(r"//\s*fidelity:\s*([A-Za-z0-9-]+(?:\s*,\s*[A-Za-z0-9-]+)*)")
RECORD_ID = re.compile(r"\b(?:M\d{1,2}|TOOL)-\d{3}\b")
ADDRESS = re.compile(r"0x[0-9A-Fa-f]{4,}")
#: Gitignored primary sources: present on a working machine, never in a clone, so not checked for existence.
LOCAL_ONLY = ("resources/", "sources/", "smali/", "unity/", "re-analysis/obb/")

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


def concrete(entry):
    """An evidence entry someone can open: a native address, or a file. A bare symbol name or the whole .so is not."""
    if ADDRESS.search(entry):
        return True
    path = entry.split(":", 1)[0].split(" ", 1)[0].replace("\\", "/")
    if "/" not in path or path.endswith(".so"):
        return False
    return path.startswith(LOCAL_ONLY) or os.path.exists(os.path.join(ROOT, path))


def cited(r):
    return any(concrete(e) for e in r.get("evidence", []))


def read_text(path):
    with open(path, encoding="utf-8") as f:
        return f.read().replace("\r\n", "\n")


def sha256_text(path):
    return hashlib.sha256(read_text(path).encode("utf-8")).hexdigest()


def review(sub):
    return sub.get("review", {}).get("state", UNREVIEWED)


def code_tags():
    """Every `// fidelity:` tag under cozmo-stack: (file, id) pairs."""
    tags = []
    for dirpath, dirnames, filenames in os.walk(CODE_ROOT):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "third-party", ".vs")]
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            for match in TAG.finditer(read_text(path)):
                for rid in match.group(1).split(","):
                    tags.append((os.path.relpath(path, ROOT).replace("\\", "/"), rid.strip()))
    return tags


def snapshot(m, sid):
    """What the operator approves: the inventory's hash and every record's status and frozen fields."""
    sub = next(s for s in m["subsystems"] if s["id"] == sid)
    inventory = sub["review"]["inventory"]
    return {
        "subsystem": sid,
        "approved": sub["review"]["approved"],
        "inventory": inventory,
        "inventory_sha256": sha256_text(os.path.join(ROOT, inventory)),
        "records": {r["id"]: {"status": r["status"], **{f: r.get(f) for f in FROZEN}}
                    for r in sorted(m["records"], key=lambda r: r["id"]) if r["subsystem"] == sid},
    }


def validate_review(m, sub, records):
    """The evidence process for one subsystem that has been through (or is going through) it."""
    problems = []
    sid, rev = sub["id"], sub["review"]
    mine = [r for r in records if r["subsystem"] == sid]

    for field in ("inventory", "approved", "snapshot"):
        if not rev.get(field):
            problems.append(f"{sid}: review state {rev['state']} needs {field!r}")
    if problems:
        return problems
    inv_path = os.path.join(ROOT, rev["inventory"])
    snap_path = os.path.join(ROOT, rev["snapshot"])
    if not os.path.exists(inv_path):
        return [f"{sid}: inventory {rev['inventory']} does not exist"]
    if not os.path.exists(snap_path):
        return [f"{sid}: snapshot {rev['snapshot']} does not exist; the operator approves with --approve {sid}"]

    inventory = read_text(inv_path)
    known = {r["id"] for r in records}
    for r in mine:
        if not re.search(r"\b%s\b" % re.escape(r["id"]), inventory):
            problems.append(f"{sid}: {r['id']} is not in the inventory {rev['inventory']}")
    for rid in sorted(set(RECORD_ID.findall(inventory)) - known):
        problems.append(f"{sid}: the inventory names {rid}, which is not a record")

    with open(snap_path, encoding="utf-8") as f:
        snap = json.load(f)
    if snap.get("inventory_sha256") != sha256_text(inv_path):
        problems.append(f"{sid}: the inventory changed after it was approved; it needs a new approval")
    frozen = snap.get("records", {})
    for rid in sorted(set(frozen) - {r["id"] for r in mine}):
        problems.append(f"{sid}: {rid} was in the approved inventory and has been removed")
    for r in mine:
        was = frozen.get(r["id"])
        if was is None:
            problems.append(f"{sid}: {r['id']} was added after the inventory was approved")
            continue
        for field in FROZEN:
            if r.get(field) != was.get(field):
                problems.append(f"{sid}: {r['id']} {field} changed after approval; that is Extractor work "
                                f"and needs a new approval")
        if r["status"] != was["status"] and not (was["status"] == IMPLEMENTATION and r["status"] in SETTLED):
            problems.append(f"{sid}: {r['id']} went from {was['status']} to {r['status']} after approval; only an "
                            f"IMPLEMENTATION_GAP being built may change status without a new approval")

    tagged = defaultdict(set)
    for path, rid in code_tags():
        tagged[path].add(rid)
    for r in mine:
        if r["status"] in SETTLED and not cited(r):
            problems.append(f"{sid}: {r['id']} is {r['status']} with no evidence entry that names an address or a file")
        path = r["location"].split(":", 1)[0]
        if path.endswith(".cs") and r["id"] not in tagged.get(path, set()):
            problems.append(f"{sid}: {r['id']} has no `// fidelity: {r['id']}` tag in {path}")

    if rev["state"] == ACCEPTED:
        if not rev.get("accepted_commit"):
            problems.append(f"{sid}: ACCEPTED needs the accepted_commit")
        for status in OPEN:
            blocking = live(records, sid, status)
            if blocking:
                problems.append(f"{sid}: cannot be ACCEPTED while holding live-path {status} {', '.join(blocking)}")
    return problems


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

        # Verification is its own axis: it says a capture or a robot agreed, never where the behaviour came from.
        ver = r.get("verification")
        if ver is not None:
            if ver.get("level") not in m["verification_levels"]:
                problems.append(f"{rid}: unknown verification level {ver.get('level')!r}")
            elif ver["level"] != "NONE":
                bundles = ver.get("bundles") or []
                if not bundles:
                    problems.append(f"{rid}: {ver['level']} has to name the bundle(s) it rests on")
                for b in bundles:
                    if not os.path.exists(os.path.join(ROOT, b)):
                        problems.append(f"{rid}: verification bundle {b} does not exist")

    for path, tid in code_tags():
        if tid not in seen:
            problems.append(f"{path}: `// fidelity: {tid}` names no record")

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

        state = sub.get("review", {}).get("state")
        if state not in REVIEW_STATES:
            problems.append(f"{sid}: review state {state!r} is not one of {', '.join(REVIEW_STATES)}")
        elif state != UNREVIEWED:
            problems.extend(validate_review(m, sub, records))
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

    w("## Evidence process")
    w("")
    w("Separate from both columns above (AGENTS.md, \"Process\"). An **UNREVIEWED** subsystem's records and")
    w("flags were written before the evidence process; nothing in this report vouches for them, and its")
    w("\"source read\" and \"built\" say only what those records claim. **Uncited** counts the settled records")
    w("(EXACT_SOURCE or EQUIVALENT_IMPLEMENTATION) whose evidence names no address and no file: a bare symbol")
    w("name or prose. **Verified** counts records a capture or a robot has agreed with; that never changes a")
    w("status.")
    w("")
    w("| subsystem | review | settled | uncited | capture verified | hardware verified |")
    w("| --- | --- | ---: | ---: | ---: | ---: |")
    for s in m["subsystems"]:
        rs = by_sub.get(s["id"], [])
        settled = [r for r in rs if r["status"] in SETTLED]
        level = Counter((r.get("verification") or {}).get("level", "NONE") for r in rs)
        w(f"| {s['id']} | {review(s)} | {len(settled)} | {sum(1 for r in settled if not cited(r))} | "
          f"{level.get('CAPTURE_VERIFIED', 0)} | {level.get('HARDWARE_VERIFIED', 0)} |")
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


def approve(sid):
    """Record the operator's approval of a subsystem's inventory: freeze its evidence as it stands now."""
    m = load()
    sub = next((s for s in m["subsystems"] if s["id"] == sid), None)
    if sub is None:
        print(f"no subsystem {sid}")
        return 1
    rev = sub.setdefault("review", {"state": UNREVIEWED})
    if not rev.get("inventory") or not os.path.exists(os.path.join(ROOT, rev["inventory"])):
        print(f"{sid}: set review.inventory to the inventory file before approving it")
        return 1
    rev["state"] = APPROVED
    rev["approved"] = date.today().isoformat()
    rev["snapshot"] = f"re-analysis/inventory/{sid}.approved.json"
    rev.pop("accepted_commit", None)
    os.makedirs(os.path.join(ROOT, "re-analysis", "inventory"), exist_ok=True)
    with open(os.path.join(ROOT, rev["snapshot"]), "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(snapshot(m, sid), indent=2, ensure_ascii=False) + "\n")
    with open(MANIFEST, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(m, indent=2, ensure_ascii=False) + "\n")
    print(f"{sid}: inventory approved and frozen in {rev['snapshot']}")
    return 0


def main():
    if "--approve" in sys.argv:
        return approve(sys.argv[sys.argv.index("--approve") + 1])
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
