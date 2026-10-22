#!/usr/bin/env python3
"""Queue and settling rules the push gate enforces (operator, 2026-10-21).

    python re-analysis/tools/queue_gate.py <base-sha> <pushed-sha>

Exit 1 with the reasons when the pushed commit breaks a rule; exit 0 otherwise. Rules:
  1. The research lane of jobs/QUEUE.md has at most 4 rows whose status is `open`.
  2. While QUEUE.md carries "HOLD ON NEW READING", no research row is added.
  3. A record leaving IMPLEMENTATION_GAP/RECOVERABLE_GAP for a settled status needs a Claude check: a file
     re-analysis/research/*claude-check*.md that names the record id.
  4. A subsystem set to ACCEPTED needs the same: a *claude-check*.md naming the subsystem id.
A rule cannot be argued with; change the queue or add the check, never bypass the gate.
"""
import json
import re
import subprocess
import sys

QUEUE = "re-analysis/jobs/QUEUE.md"
MANIFEST = "re-analysis/fidelity_manifest.json"
MAX_OPEN_RESEARCH = 4
GAPS = {"IMPLEMENTATION_GAP", "RECOVERABLE_GAP"}
SETTLED = {"EXACT_SOURCE", "EQUIVALENT_IMPLEMENTATION", "COMPATIBILITY_POLICY"}


def show(sha, path):
    r = subprocess.run(["git", "show", f"{sha}:{path}"], capture_output=True)
    return r.stdout.decode("utf-8", "replace") if r.returncode == 0 else None


def research_rows(text):
    """{row number: status} for the research lane table."""
    if text is None:
        return {}
    lane = text.split("## Research lane", 1)
    if len(lane) < 2:
        return {}
    rows = {}
    for line in lane[1].splitlines():
        m = re.match(r"\|\s*(\d+)\s*\|\s*\[[^\]]+\][^|]*\|\s*([^|]*)\|", line)
        if m:
            rows[m.group(1)] = m.group(2).strip()
    return rows


def claude_check_text(sha):
    r = subprocess.run(["git", "ls-tree", "-r", "--name-only", sha, "re-analysis/research"], capture_output=True, text=True)
    names = [n for n in r.stdout.splitlines() if "claude-check" in n and n.endswith(".md")]
    return "\n".join(show(sha, n) or "" for n in names)


def main(base, head):
    problems = []
    old_q, new_q = show(base, QUEUE), show(head, QUEUE)
    if new_q is not None and new_q != old_q:
        old_rows, new_rows = research_rows(old_q), research_rows(new_q)
        opened = [k for k, v in new_rows.items() if v.lower().startswith("open")]
        if len(opened) > MAX_OPEN_RESEARCH:
            problems.append(f"research lane has {len(opened)} open rows ({', '.join(opened)}); the limit is {MAX_OPEN_RESEARCH}")
        added = [k for k in new_rows if k not in old_rows]
        if added and "HOLD ON NEW READING" in new_q:
            problems.append(f"research rows {', '.join(added)} added while QUEUE.md holds new reading")

    old_m, new_m = show(base, MANIFEST), show(head, MANIFEST)
    if new_m is not None and old_m is not None and new_m != old_m:
        old_j, new_j = json.loads(old_m), json.loads(new_m)
        before = {r["id"]: r["status"] for r in old_j["records"]}
        settled = [r["id"] for r in new_j["records"] if before.get(r["id"]) in GAPS and r["status"] in SETTLED]
        old_state = {s["id"]: s.get("review", {}).get("state") for s in old_j.get("subsystems", [])}
        accepted = [s["id"] for s in new_j.get("subsystems", [])
                    if s.get("review", {}).get("state") == "ACCEPTED" and old_state.get(s["id"]) != "ACCEPTED"]
        if settled or accepted:
            checks = claude_check_text(head)
            for rid in settled + accepted:
                if not re.search(rf"(?<![\w-]){re.escape(rid)}(?![\w-])", checks):
                    what = "subsystem" if rid in accepted else "record"
                    problems.append(f"{what} {rid} settled/accepted with no re-analysis/research/*claude-check*.md naming it")

    for p in problems:
        print("queue gate: " + p)
    return 1 if problems else 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1], sys.argv[2]))
