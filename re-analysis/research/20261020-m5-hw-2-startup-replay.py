"""Read-only extraction of the second M5 run's startup facts; no acceptance oracle.

Usage: python <this-file> <bundle-directory>
Prints a compact witness, including hashes of the original evidence. Does not
change the bundle, connect to hardware, or infer the cause of physical appearance.
"""
import hashlib
import json
from pathlib import Path
import sys


def main():
    bundle = Path(sys.argv[1])
    result = json.loads((bundle / "result.json").read_text(encoding="utf-8-sig"))
    events = []
    for line_number, line in enumerate(
        (bundle / "events.jsonl").open(encoding="utf-8-sig"), 1
    ):
        event = json.loads(line)
        name, text = event.get("name", ""), event.get("text", "")
        if (
            event.get("kind") in ("connection-response", "check-readiness")
            or event.get("level") == "error"
            or "NeutralFace" in text
            or "anim_neutral_eyes_01" in text
            or "ag_neutral_face" in text
        ):
            events.append({
                "line": line_number,
                **{key: event[key] for key in (
                    "atMs", "kind", "level", "name", "text", "result",
                    "assets", "calibrated"
                ) if key in event},
            })
    checks = []
    for check in result["checks"]:
        keys = ["id", "verdict", "resultKind", "missing"]
        if check["id"] == "ABORT":
            keys += ["neutralReference", "observationWindow"]
        if check["id"] == "DRIVING":
            keys += ["result", "successfulTraversalCoverage", "traversalJudgement"]
        checks.append({key: check[key] for key in keys if key in check})
    hashes = {}
    for name in ("result.json", "events.jsonl", "SUMMARY.txt"):
        hashes[name] = hashlib.sha256((bundle / name).read_bytes()).hexdigest()
    provenance = []
    for entry in result["provenance"]:
        saved = bundle / "provenance" / entry["path"]
        actual = hashlib.sha256(saved.read_bytes()).hexdigest() if saved.is_file() else None
        provenance.append({**entry, "savedCopyMatches": actual == entry["sha256"]})
    print(json.dumps({
        "bundle": bundle.name,
        "commit": result["commit"],
        "createdUtc": result["createdUtc"],
        "argv": result["argv"],
        "hashes": hashes,
        "provenance": provenance,
        "reportedRunErrors": result["errors"],
        "events": events,
        "checks": checks,
        "neutralReferenceFilePresent": (bundle / "neutral-reference.svg").is_file(),
        "scope": "Observed evidence only; native/app path and judgement are in the companion reports.",
    }, indent=2, ensure_ascii=True))


if __name__ == "__main__":
    main()
