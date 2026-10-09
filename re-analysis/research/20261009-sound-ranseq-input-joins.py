"""Verify the serialized RanSeq inputs joining native TR11-17 to C1-25.

Read shipped banks locally. Candidate export supplies expected values, never byte
values. Native TR11-17 specify the fixed header, child and playlist layouts. The
prior raw census locates nonempty child arrays; empty payloads use the complete
zero-count trailer. This is a bounded asset join, not an engine call-site census.
"""
from pathlib import Path
import argparse, collections, hashlib, json, struct, zipfile

ROOT = Path(__file__).resolve().parent

def collect(archive):
    raw = json.loads((ROOT / "20261007-sound-reachability-assets.json").read_text(encoding="utf-8-sig"))
    nav = {(d["bank"], d["Id"]): d for d in map(json.loads,
        (ROOT / "20261007-sound-census-export.jsonl").read_text(encoding="utf-8-sig").splitlines())
        if d.get("kind") == "object"}
    archive_hash = hashlib.sha256(Path(archive).read_bytes()).hexdigest()
    assert archive_hash == raw["archiveSha256"]
    objects = {(d["bank"], d["id"]): d for d in raw["objects"]}
    bank_hash = {d["bank"]: d["sha256"] for d in raw["banks"]}
    result = []
    with zipfile.ZipFile(archive) as z:
        banks = {}
        for d in raw["objects"]:
            if d["type"] != 5:
                continue
            name = d["bank"]
            if name not in banks:
                banks[name] = z.read(name)
                assert hashlib.sha256(banks[name]).hexdigest() == bank_hash[name]
            b = banks[name]
            a, end = d["payloadOffset"], d["payloadOffset"] + d["length"]
            assert hashlib.sha256(b[a:end]).hexdigest() == d["sha256"]
            expected = nav[(name, d["id"])]["node"]
            p = d.get("childrenOffset")
            if p is None:
                assert expected["Children"] == [] and expected["Playlist"] == []
                p = end - 6
            h = p - 24
            assert a + 4 <= h < p < end
            loop, minimum, maximum, f0, f1, f2, avoid, transition, random, mode, flags = struct.unpack_from("<HHHIIIHBBBB", b, h)
            assert (loop, avoid, transition, random, mode, flags) == tuple(expected[k] for k in
                ["LoopCount", "AvoidRepeatCount", "TransitionMode", "RandomMode", "Mode", "Flags"])
            n = struct.unpack_from("<I", b, p)[0]
            assert p + 4 + n * 4 + 2 <= end
            children = list(struct.unpack_from("<" + "I" * n, b, p + 4))
            assert children == expected["Children"] == d.get("children", [])
            q = p + 4 + n * 4
            m = struct.unpack_from("<H", b, q)[0]
            assert q + 2 + m * 8 == end
            playlist = [struct.unpack_from("<II", b, q + 2 + i * 8) for i in range(m)]
            assert playlist == [(x["Item1"], x["Item2"]) for x in expected["Playlist"]]
            joined = []
            for child in children:
                target = objects[(name, child)]
                ta, te = target["payloadOffset"], target["payloadOffset"] + target["length"]
                assert b[target["offset"]] == target["type"]
                assert struct.unpack_from("<I", b, ta)[0] == child
                assert hashlib.sha256(b[ta:te]).hexdigest() == target["sha256"]
                joined.append({"id": child, "type": target["type"], "objectOffset": target["offset"]})
            result.append({"bank": name, "id": d["id"], "objectOffset": d["offset"],
                "payloadSha256": d["sha256"], "headerOffset": h, "headerHex": b[h:p].hex(),
                "loop": loop, "loopMin": minimum, "loopMax": maximum,
                "rawFloatWords": [f"{x:08X}" for x in [f0, f1, f2]], "avoid": avoid,
                "transitionMode": transition, "randomMode": random, "selectionKind": mode,
                "flags": flags, "childrenOffset": p, "children": joined,
                "playlistOffset": q, "playlist": [{"id": i, "weight": w} for i, w in playlist]})
    witnesses = []
    for ident in [296758057, 777177819, 460890181]:
        selected = next(d for d in result if d["id"] == ident)
        name = selected["bank"]
        action = next(d for d in raw["objects"] if d["bank"] == name and d["type"] == 3
            and d.get("actionType") == 0x403 and d.get("target") == ident)
        event = next(d for d in raw["objects"] if d["bank"] == name and d["type"] == 4
            and action["id"] in d.get("actions", []))
        b = banks[name]
        for d in [action, event]:
            a, end = d["payloadOffset"], d["payloadOffset"] + d["length"]
            assert hashlib.sha256(b[a:end]).hexdigest() == d["sha256"]
        assert struct.unpack_from("<HI", b, action["payloadOffset"] + 4) == (0x403, ident)
        a = event["payloadOffset"]
        n = struct.unpack_from("<I", b, a + 4)[0]
        assert list(struct.unpack_from("<" + "I" * n, b, a + 8)) == event["actions"]
        witnesses.append({"bank": name, "event": event["id"], "eventOffset": event["offset"],
            "action": action["id"], "actionOffset": action["offset"], "fullType": "0403",
            "target": ident, "targetOffset": selected["objectOffset"]})
    summary = {"kind": "summary", "archiveSha256": archive_hash,
        "nativeLayouts": "TR11-17 / A0828C..A084C8, A080CC..A0819C",
        "records": len(result), "directPlayWitnesses": witnesses, "childReferences": sum(len(d["children"]) for d in result),
        "childTypes": dict(collections.Counter(c["type"] for d in result for c in d["children"])),
        "selectionKinds": dict(collections.Counter(d["selectionKind"] for d in result)),
        "randomModes": dict(collections.Counter(d["randomMode"] for d in result)),
        "flags": dict(collections.Counter(d["flags"] for d in result)),
        "playlistEntries": sum(len(d["playlist"]) for d in result),
        "weights": dict(collections.Counter(e["weight"] for d in result for e in d["playlist"])),
        "boundary": "Serialized input/type joins only; no indirect-writer, app-post or unreachable proof."}
    return summary, result

if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("archive")
    p.add_argument("output")
    args = p.parse_args()
    summary, result = collect(args.archive)
    Path(args.output).write_text("\n".join(json.dumps(x, separators=(",", ":")) for x in
        [summary, *result]) + "\n", encoding="utf-8")
    print(json.dumps(summary))
