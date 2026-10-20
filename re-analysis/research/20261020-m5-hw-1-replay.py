"""Read-only bundle analysis. No robot, stack execution, or acceptance updates.

Run from repo root: python -I re-analysis/research/20261020-m5-hw-1-replay.py
Pixel counts are an engine-format diagnostic, not a firmware decoder oracle.
"""
import base64
import collections
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BUNDLE = ROOT / "re-analysis/acceptance/hardware/20261020-074223-M5-HW-2cc1cf"


def read_lines(name):
    return [json.loads(line) for line in (BUNDLE / name).open(encoding="utf-8")]


def wire():
    seen = {False: set(), True: set()}
    rows = []
    for line, frame in enumerate(read_lines("frames.jsonl"), 1):
        data = base64.b64decode(frame["rawBase64"])
        assert data[:7] == b"COZ\x03RE\x01"
        typ, seq, last = data[7], *struct.unpack_from("<HH", data, 8)
        parts = []
        if typ in (7, 8, 9):
            pos = 14
            while pos < len(data):
                kind, size = struct.unpack_from("<BH", data, pos)
                pos += 3
                assert pos + size <= len(data)
                parts.append((kind, data[pos:pos + size]))
                pos += size
        else:
            parts = [(typ, data[14:])]
        for ordinal, (kind, payload) in enumerate(parts):
            reliable = bool(seq or last) and kind not in (5, 7, 8, 9, 10, 11)
            number = seq if reliable else 0
            if reliable:
                seq = 1 if seq == 65534 else seq + 1
            previous = seen[frame["outbound"]]
            if kind in (1, 2):
                previous.clear()
                continue
            if kind not in (4, 5) or not payload:
                continue
            if reliable:
                if number in previous:
                    continue
                previous.add(number)
                if len(previous) > 8192:
                    previous.difference_update(
                        [old for old in previous if ((number - old) & 65535) > 4096])
            rows.append(dict(atMs=frame["atMs"], outbound=frame["outbound"],
                             payloadHex=payload.hex().upper(), line=line,
                             ordinal=ordinal, seq=number))
    return rows


def pixel_count(payload):
    """Diagnostic translation of Display.cs DecodeCanvas, engine column-mask format."""
    if len(payload) == 1024:
        return sum(int.from_bytes(payload[i:i + 8], "little").bit_count()
                   for i in range(0, 1024, 8))
    masks, pos = [], 0
    while len(masks) < 128 and pos < len(payload):
        value = payload[pos]
        if value < 64:
            masks.extend([0] * (value + 1))
            pos += 1
        elif value < 128:
            masks.extend([masks[-1] if masks else 0] * ((value & 63) + 1))
            pos += 1
        else:
            mask = row = 0
            while row < 64 and pos < len(payload) and payload[pos] & 128:
                count, pair = ((payload[pos] >> 2) & 31) + 1, payload[pos] & 3
                for _ in range(count):
                    if row < 64:
                        mask |= pair << row
                        row += 2
                pos += 1
            masks.append(mask)
    return sum(mask.bit_count() for mask in masks)


def main():
    rows, events = wire(), read_lines("events.jsonl")
    key = lambda r: (r["atMs"], r["outbound"], r["payloadHex"])
    assert collections.Counter(map(key, rows)) == collections.Counter(
        map(key, read_lines("messages.jsonl")))
    print("Raw-frame replay matches all", len(rows), "deduplicated messages.")
    for name, start, end in [
            ("ABORT", 7715.5276, 8115.5276),
            ("LOCK commands", 47490, 49510),
            ("Dispose", 172516.4529, 172549.2551)]:
        tags = ("9D", "9E", "93") if name == "LOCK commands" else ("8D", "8F", "97")
        selected = [r for r in rows if r["outbound"] and start <= r["atMs"] <= end
                    and (r["payloadHex"][:2] in tags or name == "Dispose")]
        print(name, [{**r, "payloadHex": r["payloadHex"][:16]} for r in selected])
    states = [e["headRad"] for e in events if e["kind"] == "state"
              and 47538 <= e["atMs"] <= 49508.412]
    print("Locked head samples/min/max:", len(states), min(states), max(states))
    faces = [(r["atMs"], pixel_count(bytes.fromhex(r["payloadHex"])[3:]), r["line"])
             for r in rows if r["outbound"] and r["payloadHex"].startswith("97")
             and 90000 < r["atMs"] < 167884]
    groups = []
    for face in (f for f in faces if f[1] < 250):
        if not groups or face[0] - groups[-1][-1][0] > 1000:
            groups.append([])
        groups[-1].append(face)
    print("Low-lit-pixel diagnostic clusters (not blink acceptance):", groups)
    samples = [e for e in events if e["kind"] == "animation-state"
               and 60198.0338 < e["atMs"] < 65981.5542]
    batches = []
    for sample in samples:
        if not batches or sample["atMs"] - batches[-1][-1]["atMs"] > 2:
            batches.append([])
        batches[-1].append(sample)
    print("Delivery-cluster final max frames/bytes:",
          max(g[-1]["streamFrames"] - g[-1]["audioPlayed"] for g in batches),
          max(g[-1]["streamBytes"] - g[-1]["bytesPlayed"] for g in batches))
    print("Cluster grouping is diagnostic, not an atomic send-budget oracle.")
    print("Drops:", dict(collections.Counter(bytes.fromhex(r["payloadHex"])[15]
          for r in rows if not r["outbound"] and r["payloadHex"].startswith("F1"))))


if __name__ == "__main__":
    main()
