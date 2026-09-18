"""Build the canonical Cozmo robot protocol definition (protocol/cozmo_robot_protocol.json).

Evidence priority (M2):
  1. official decompiled C# CLAD definitions -> field NAMES, array-ness, enum types
  2. native libcozmoEngine Unpack/Size       -> AUTHORITATIVE widths, order, total size
  3. real hardware captures                  -> observed lengths, direction, reliability
  4. PyCozmo                                 -> fallback names only, never over native

usage: python build_protocol_definition.py <scratch-with-intermediates> <re-analysis-dir>
Intermediates: robot_tags_official_named.json, native_layouts.json, csharp_twins.json,
csharp_enums.json, pycozmo_decl.json, capture_observed.json.
"""
import json, sys, os, re, collections

SP, RA = sys.argv[1], sys.argv[2]
tags  = json.load(open(os.path.join(SP, "robot_tags_official_named.json")))
nat   = json.load(open(os.path.join(SP, "native_layouts.json")))
tw    = json.load(open(os.path.join(SP, "csharp_twins.json")))
enums = json.load(open(os.path.join(SP, "csharp_enums.json")))
pyc   = json.load(open(os.path.join(SP, "pycozmo_decl.json")))


def scan_captures(ra):
    """Observed message tags, body lengths and directions, read straight out of the frame logs in
    captures/ so the definition is reproducible from the repository alone."""
    import glob
    import struct as _s
    out = {}
    for path in sorted(glob.glob(os.path.join(ra, "captures", "*.log"))):
        for line in open(path, encoding="utf-8-sig"):
            m = re.match(r"\S+ (TX|RX) ((?:[0-9a-f]{2} ?)+)", line.strip())
            if not m:
                continue
            d, raw = m.group(1), bytes.fromhex(m.group(2).replace(" ", ""))
            if len(raw) < 14 or raw[:7] != b"COZ\x03RE\x01":
                continue
            t, body = raw[7], raw[14:]
            subs = []
            if t in (7, 8, 9):
                o = 0
                while o + 3 <= len(body):
                    st = body[o]
                    sz = _s.unpack_from("<H", body, o + 1)[0]
                    o += 3
                    subs.append((st, body[o:o + sz]))
                    o += sz
            else:
                subs.append((t, body))
            for st, pl in subs:
                if st in (4, 5) and pl:
                    e = out.setdefault("0x%02x" % pl[0], {"count": 0, "lengths": {}, "dirs": set()})
                    e["count"] += 1
                    e["lengths"][str(len(pl) - 1)] = e["lengths"].get(str(len(pl) - 1), 0) + 1
                    e["dirs"].add(d)
    for e in out.values():
        e["dirs"] = sorted(e["dirs"])
    return out


def scan_probe_results(ra):
    """Per-message verdicts written by `cozmo-conformance probe`: a message is hardware verified when a
    real robot sent it and our generated codec re-encoded it byte-identically."""
    import glob
    verified, conflicts = {}, {}
    for path in sorted(glob.glob(os.path.join(ra, "captures", "*probe-results*.json"))):
        p = json.load(open(path))
        fw = (p.get("robot") or {}).get("firmware")
        for m in p.get("messages", []):
            tag = int(m["tag"], 16)
            if m.get("status") == "hardware_verified":
                verified[tag] = {"firmware": fw, "count": m.get("count"),
                                 "byteIdentical": m.get("byteIdenticalRoundTrips")}
            else:
                conflicts[tag] = m.get("problem")
    return verified, conflicts


obs = scan_captures(RA)
if not obs:
    obs = json.load(open(os.path.join(SP, "capture_observed.json")))
PROBED, PROBE_CONFLICTS = scan_probe_results(RA)

# Engine->robot messages the robot demonstrably acted on during the 2026-09-18 runs. We generate these
# bytes ourselves, so appearing in a capture proves nothing on its own; what promotes them is the robot's
# observed response, recorded here.
ROBOT_ACCEPTED = {
    0x25: "robot answered with ManufacturingID (0xED)",
    0x37: "robot answered MotorActionAck (0xC4) and RobotState head angle reached the commanded 0.4 rad",
    0x4A: "robot answered with a 264-sample IMURawDataChunk (0xC7) burst",
    0x4B: "robot answered SyncTimeAck (0xC2) and began streaming RobotState",
    0x4C: "robot answered with 28 images as ImageChunk (0xF2) + ImageImuData (0xF4)",
    0x66: "sent before the image request; the frames that arrived were grayscale (encoding 8) as asked",
    0x80: "robot answered with CrashReport (0xCF)",
    0x0A: "robot reported cube advertisements as ObjectAvailable (0xF3) afterwards",
    0x9F: "robot began streaming AnimationState (0xF1)",
    0x45: "accepted without error; RobotState kept streaming with the requested pose origin",
}

# Semantics confirmed by decoding the probe capture (no byte-layout change; these record what the
# fields actually mean on firmware 2457).
HARDWARE_NOTES = {
    0xF2: "probe 2026-09-18: 28 images, 7 chunks each, ~6.6 kB per image, encoding 8 (JPEGMinimizedGray), "
          "resolution 4 (QVGA); chunkId counts 0..6 and imageChunkCount carries the total only in the final chunk; "
          "status was 2 throughout",
    0xC7: "probe 2026-09-18: 264 samples in one burst; the first i16 triple is the gyro (near zero at rest), the "
          "second the accelerometer (Z about 9.7k at rest); order is 0 on the first sample, 1 in the middle, 2 on the last",
    0xD1: "probe 2026-09-18: observed as motor 2 then 3 with calibStarted true, then both again with false, "
          "confirming the MotorID enum (2 = lift, 3 = head) and the start/finish pairing",
    0xF1: "probe 2026-09-18: streamed at ~30 Hz with enabledAnimTracks 0xFF and zero counters while idle",
    0xCF: "probe 2026-09-18: answered with an all-zero header and an empty array when the robot holds no crash reports",
    0xF4: "probe 2026-09-18: one per camera frame, carrying the gyro rates sampled with the image",
}

W = {"u8": 1, "i8": 1, "bool": 1, "u16": 2, "i16": 2, "u32": 4, "i32": 4,
     "f32": 4, "f64": 8, "u64": 8, "i64": 8}
CS = {"byte": "u8", "sbyte": "i8", "bool": "bool", "ushort": "u16", "short": "i16",
      "uint": "u32", "int": "i32", "ulong": "u64", "long": "i64", "float": "f32", "double": "f64"}

# Nested structs. Widths confirmed against native Size() or by arithmetic on their containers.
STRUCTS = {
    "RobotPose":        [("x", "f32"), ("y", "f32"), ("z", "f32"), ("angle", "f32"), ("pitch", "f32")],
    "AccelData":        [("x", "f32"), ("y", "f32"), ("z", "f32")],
    "GyroData":         [("x", "f32"), ("y", "f32"), ("z", "f32")],
    "ActiveAccel":      [("x", "f32"), ("y", "f32"), ("z", "f32")],
    "LightState":       [("onColor", "u16"), ("offColor", "u16"), ("onFrames", "u8"), ("offFrames", "u8"),
                         ("transitionOnFrames", "u8"), ("transitionOffFrames", "u8"), ("offset", "i16")],
    "PathSegmentSpeed": [("speed_mmps", "f32"), ("accel_mmps2", "f32"), ("decel_mmps2", "f32")],
    "Frame":            [("flags", "u8"), ("data", "u8[16]")],
}


def struct_size(n):
    t = 0
    for _, ty in STRUCTS[n]:
        m = re.match(r"(\w+)\[(\d+)\]$", ty)
        t += W[m.group(1)] * int(m.group(2)) if m else W[ty]
    return t


# CLAD variable-array helper routines recovered from libcozmoEngine: (count width, element width, semantic)
HELPERS = {
    "local_0x71a8f6": ("u16", "u8", "bytes"),    # vector<uint8_t>,  u16 count
    "local_0x7a17d8": ("u8", "u8", "bytes"),     # vector<uint8_t>,  u8 count
    "local_0x6c235c": ("u8", "i8", "string"),    # string,           u8 count
    "local_0x71283c": ("u8", "u32", "array"),    # vector<uint32_t>, u8 count
    "local_0x73923a": ("u8", "i32", "array"),    # vector<int32_t>,  u8 count
}

# Fixed arrays of a nested struct compile to "read one element, memcpy the rest", so they carry a bare
# loop bound instead of the usual (cmp #1, cmp #N) pair. Lengths below are confirmed three ways:
# native bound, C# twin size (CubeLights = LightState[4] = 40 B) and hardware capture body lengths
# (0x03 = 31 B = 3*10+1, 0x11 = 21 B = 2*10+1).
STRUCT_ARRAY = {"BackpackLightsMiddle": ("LightState", 3), "BackpackLightsTurnSignals": ("LightState", 2),
                "CubeLights": ("LightState", 4)}

SUBSYSTEM = {}


def S(name, *tagsl):
    for t in tagsl:
        SUBSYSTEM[t] = name


S("identity_version_logging", 0x01, 0x24, 0x25, 0x4B, 0x80, 0x82, 0x89, 0xA0, 0xA5, 0xB0, 0xB1,
  0xB2, 0xB7, 0xBE, 0xC2, 0xC9, 0xCF, 0xD2, 0xEC, 0xED, 0xEE)
S("robot_state_sensors", 0x4A, 0x4F, 0x52, 0x53, 0x54, 0x60, 0x63, 0xBF, 0xC0, 0xC1, 0xC3, 0xC7,
  0xD4, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xF0)
S("motors", 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x46, 0x47, 0x50, 0x51,
  0x58, 0x59, 0xC4, 0xD1, 0xD8)
S("leds_display", 0x02, 0x03, 0x0B, 0x11, 0x97, 0x98, 0xA3)
S("camera", 0x4C, 0x55, 0x57, 0x5A, 0x66, 0xC8, 0xF2, 0xF4)
S("audio", 0x64, 0x65, 0x8E, 0x8F)
S("animation", 0x8D, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x99, 0x9A, 0x9B, 0x9D, 0x9E, 0x9F,
  0xCA, 0xCB, 0xD5, 0xF1)
S("cubes_ble", 0x04, 0x05, 0x07, 0x08, 0x0A, 0x0C, 0x10, 0x12, 0x26, 0x27, 0x4D, 0x4E, 0x62, 0x86,
  0x87, 0xB4, 0xB5, 0xB6, 0xB9, 0xCE, 0xD0, 0xD7, 0xF3, 0xF5)
S("localization_navigation", 0x3C, 0x3D, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x48,
  0x49, 0x61, 0xB3, 0xB8, 0xBA, 0xBB, 0xBC, 0xBD, 0xC5, 0xC6, 0xD3)
S("firmware_update_recovery", 0x06, 0x0D, 0x30, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF, 0xEF)
S("factory_debug_storage", 0x0E, 0x0F, 0x81, 0xA1, 0xA2, 0xA4, 0xCD, 0xD6)

# Refinements derived from the 2026-09-18 firmware-2457 captures. Each is byte-compatible with the
# native Unpack (it only splits or renames fields the engine reads as one block) and is marked as
# hardware evidence in the output.
REFINEMENTS = {
    0xB0: {"note": "capture 2026-09-18: decoded against the OBB AnkiLogStringTables; the engine reads "
                   "formatId+unused as one 4-byte block",
           "fields": [{"name": "formatId", "kind": "scalar", "type": "u16", "name_source": "hardware"},
                      {"name": "unused", "kind": "scalar", "type": "u16", "name_source": "hardware"},
                      {"name": "nameId", "kind": "scalar", "type": "u16", "name_source": "hardware"},
                      {"name": "level", "kind": "scalar", "type": "i8", "name_source": "hardware"},
                      {"name": "args", "kind": "varray", "type": "i32", "count": "u8", "elem": "i32",
                       "name_source": "hardware"}]},
    0xEE: {"note": "capture 2026-09-18: leading u16 is the low half of the head serial number; the "
                   "byte array is the cozmo.safe JSON signature header",
           "fields": [{"name": "robotId", "kind": "scalar", "type": "u16", "name_source": "hardware"},
                      {"name": "signature", "kind": "varray", "type": "u8", "count": "u16", "elem": "u8",
                       "name_source": "hardware", "note": "UTF-8 JSON"}]},
    0xC9: {"note": "capture 2026-09-18: u32 head serial (0x41d04d9d) then hardware revision 5, "
                   "matching the robot's own hardware.revision trace and MfgId.body_hw_version",
           "fields": [{"name": "serialNumberHead", "kind": "scalar", "type": "u32", "name_source": "hardware"},
                      {"name": "hwVersion", "kind": "scalar", "type": "u16", "name_source": "hardware"}]},
}

SAFETY = {}


def F(level, *tagsl):
    for t in tagsl:
        SAFETY[t] = level


F("read_only", 0x24, 0x25, 0x4A, 0x4C, 0x4F, 0x80, 0x5A)
F("safe_visible", 0x02, 0x03, 0x0B, 0x11, 0x65, 0x97, 0x98, 0xA3, 0x04, 0x10, 0x4D)
F("motion", 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3D, 0x3E, 0x3F, 0x40,
  0x41, 0x42, 0x43, 0x44, 0x58, 0x8D, 0x91, 0x92, 0x93, 0x94, 0x99, 0x9A, 0x9B)
F("state_change", 0x01, 0x05, 0x08, 0x0A, 0x0C, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4B, 0x4E, 0x50,
  0x51, 0x54, 0x55, 0x57, 0x59, 0x60, 0x62, 0x63, 0x64, 0x66, 0x82, 0x8E, 0x8F, 0x95, 0x96, 0x9D,
  0x9E, 0x9F, 0xA0, 0x26, 0x27)
F("destructive", 0x06, 0x07, 0x0D, 0x0E, 0x0F, 0x12, 0x30, 0x52, 0x53, 0x61, 0x81, 0x89, 0xA1,
  0xA2, 0xA4, 0xA5, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF)


def cs_ops(cls):
    """C# fields -> (op sequence comparable with native, field descriptors)."""
    ops, fields = [], []
    for f in cls["fields"]:
        cs = f["cs"]
        arr = cs.endswith("[]")
        base = (cs[:-2] if arr else cs).split(".")[-1]
        if base in STRUCTS:
            kind, ty, w = "struct", base, struct_size(base)
        elif base in CS:
            kind, ty, w = "scalar", CS[base], W[CS[base]]
        elif base in enums:
            kind, ty, w = "enum", base, W[enums[base]["base"]]
        elif base == "string":
            kind, ty, w = "string", "string", None
        else:
            kind, ty, w = "unknown", base, None
        d = {"name": f["name"], "kind": kind, "type": ty}
        if arr:
            if "fixed" in f:
                d["kind"] = "farray"; d["length"] = f["fixed"]; d["elem"] = ty
            else:
                d["kind"] = "varray"; d["count"] = f.get("count_type", "u16"); d["elem"] = ty
        if kind == "string":
            d["count"] = f.get("count_type", "u8")
        fields.append(d)
        if kind == "struct":
            ops.append(("struct", ty))
        elif d["kind"] in ("varray", "string"):
            ops.append(("var", None))
        else:
            ops.append(("w", w))
    return ops, fields


def native_ops(t):
    v = nat.get(t, {})
    out = []
    for o in v.get("ops", []):
        if o["op"] == "bytes":
            out.append(("w", o["size"]))
        elif o["op"] == "read":
            out.append(("w", W.get(o["type"], 1)))
        elif o["op"] == "struct":
            out.append(("struct", o["name"]))
        elif o["op"] == "call":
            n = o["name"]
            if n in HELPERS:
                out.append(("var", n))
            elif "memcpy" in n:
                pass  # trailing copy of a fixed array, not an independent read
            else:
                out.append(("call", n))
    return out, v.get("size"), v.get("loops", 0)


def py_names(tag, nops):
    """PyCozmo names for a tag, but only when its widths agree with the native Unpack.
    Returns [(name, type)] where type carries PyCozmo's float/signedness (widths already match)."""
    p = pyc["packets"].get("0x%02x" % tag)
    if not p:
        return None
    names = []
    for a in p["args"]:
        t = a["type"]
        if t == "farray":
            names.append((a["name"], a.get("elem"), ("w", W.get(a.get("elem"), 1))))
        elif t in ("varray", "string"):
            names.append((a["name"], a.get("elem", "u8"), ("var", None)))
        else:
            names.append((a["name"], t, ("w", W.get(t))))
    if len(names) != len(nops):
        return None
    for (n, ty, o), no in zip(names, nops):
        if o[0] != no[0]:
            return None
        if o[0] == "w" and o[1] != no[1]:
            return None
    return [(n, ty) for n, ty, _ in names]


def pick_twin(t, nops):
    """Best C# candidate: exact op match beats prefix match beats nothing.

    A candidate with no fields of its own can only ever be an exact match, against a native layout that is
    itself empty. The empty op list is a prefix of every layout, so without the guard below a zero-field C#
    class silently "prefix matches" any message and claims a name and a statically_verified status on the
    strength of no field evidence at all. 0xD4 RobotStopped and 0xDD FallingStarted were both recorded that
    way before this check existed.
    """
    best = None
    for c in tw.get(t, []):
        if c["kind"] != "class":
            continue
        ops, fields = cs_ops(c)
        if ops == nops:
            return ("exact", c, fields)      # includes both-empty, which genuinely confirms an empty message
        if 0 < len(ops) < len(nops) and ops == nops[:len(ops)] and best is None:
            best = ("prefix", c, fields)
    return best


SCALAR_WIDTHS = {1: "u8", 2: "u16", 4: "u32", 8: "u64"}
FIELD_KINDS = {"scalar", "enum", "struct", "farray", "varray", "string", "raw", "unresolved"}


def norm_name(n):
    """Names compared for duplication ignoring case and underscores: impactIntensity == impact_intensity."""
    return n.replace("_", "").lower()


def width_name(v):
    """Name of a scalar of this byte width. Unknown widths are an error, not a u8."""
    if v not in SCALAR_WIDTHS:
        raise ValueError("no scalar type is %r bytes wide; the native read was not understood" % (v,))
    return SCALAR_WIDTHS[v]


def validate(tag, ctype, fields):
    """Refuse to emit a field we cannot represent, rather than truncating it to something plausible."""
    seen = set()
    for i, f in enumerate(fields):
        where = "0x%02X %s field %d (%s)" % (tag, ctype, i, f.get("name"))
        if f["kind"] not in FIELD_KINDS:
            raise ValueError("%s: unsupported kind %r" % (where, f["kind"]))
        if f["kind"] in ("varray", "string"):
            cw = f.get("count")
            if cw not in ("u8", "u16", "u32"):
                raise ValueError("%s: count width %r is not one the codec can emit" % (where, cw))
        if f["kind"] == "farray" and not isinstance(f.get("length"), int):
            raise ValueError("%s: fixed array has no resolved length" % where)
        key = norm_name(f["name"])
        if key in seen:
            raise ValueError("%s: duplicate field name" % where)
        seen.add(key)
    return fields


def elem_width(f, enums_):
    """Byte width of one element of a field, or None if unknown."""
    t = f.get("elem") or f.get("type")
    if t in W:
        return W[t]
    if t in STRUCTS:
        return struct_size(t)
    if t in enums_:
        return W[enums_[t]["base"]]
    return None


def solve_arrays(fields, bounds, target, enums_):
    """Assign the native loop bounds (in field order) to a subset of fields so the total equals the
    official Size(). Returns the unique solution or None when ambiguous/impossible."""
    idx = [i for i, f in enumerate(fields) if f["kind"] in ("scalar", "enum", "struct")]
    widths = {i: elem_width(fields[i], enums_) for i in idx}
    if any(widths[i] is None for i in idx) or len(bounds) > len(idx):
        return None
    base = sum(widths[i] for i in idx)
    sols = []

    def walk(pos, k, extra, chosen):
        if len(sols) > 1:
            return
        if k == len(bounds):
            if base + extra == target:
                sols.append(list(chosen))
            return
        for j in range(pos, len(idx)):
            i = idx[j]
            add = widths[i] * (bounds[k] - 1)
            if base + extra + add > target:
                continue
            chosen.append((i, bounds[k]))
            walk(j + 1, k + 1, extra + add, chosen)
            chosen.pop()

    walk(0, 0, 0, [])
    return sols[0] if len(sols) == 1 else None


messages = {}
for union, dirname in (("EngineToRobot", "engine_to_robot"), ("RobotToEngine", "robot_to_engine")):
    for stag, (member, ctype) in tags[union].items():
        tag = int(stag)
        ctype = ctype.replace("&&", "")
        nops, nsize, nloops = native_ops(ctype)
        twin = pick_twin(ctype, nops)
        fields, notes = [], []
        if twin:
            how, c, cf = twin
            fields = [dict(f) for f in cf]
            for f in fields:
                f["name_source"] = "csharp"
            confidence = "exact" if how == "exact" else "prefix"
            notes.append("C# twin %s.%s (%s match)" % (c["ns"], ctype, how))
            if how == "prefix":
                pn = py_names(tag, nops)
                taken = {norm_name(f["name"]) for f in fields}
                for i in range(len(cf), len(nops)):
                    k, v = nops[i]
                    # PyCozmo's field list can be shorter or shifted relative to the native layout, so a
                    # borrowed name may collide with one the C# twin already supplied (0xDE FallingStopped
                    # produced impactIntensity and impact_intensity side by side). Fall back to a positional
                    # name rather than emit two fields that read as the same thing.
                    nm = pn[i][0] if pn and i < len(pn) else None
                    src = "pycozmo"
                    if nm is None or norm_name(nm) in taken:
                        nm, src = "field%d" % i, "generated"
                    taken.add(norm_name(nm))
                    fields.append({"name": nm,
                                   "kind": "scalar" if k == "w" else k,
                                   "type": ((pn[i][1] or width_name(v)) if pn and i < len(pn) else width_name(v)) if k == "w" else v,
                                   "name_source": src,
                                   "uncertain": True})
        else:
            pn = py_names(tag, nops)
            confidence = "native_named" if pn else ("native_only" if nops else "empty")
            for i, (k, v) in enumerate(nops):
                nm = pn[i][0] if pn else "field%d" % i
                src = "pycozmo" if pn else "generated"
                if k == "w":
                    fields.append({"name": nm, "kind": "scalar",
                                   "type": (pn[i][1] or width_name(v)) if pn else width_name(v),
                                   "name_source": src, "uncertain": not pn})
                elif k == "struct":
                    fields.append({"name": nm, "kind": "struct", "type": v,
                                   "name_source": src, "uncertain": not pn})
                elif k == "var":
                    cnt, elem, sem = HELPERS.get(v, ("u16", "u8", "bytes"))
                    fields.append({"name": nm, "kind": "string" if sem == "string" else "varray",
                                   "type": "string" if sem == "string" else elem,
                                   "count": cnt, "elem": elem, "name_source": src, "uncertain": not pn})
                else:
                    fields.append({"name": "unresolved%d" % i, "kind": "unresolved", "type": v, "uncertain": True})
            if pn:
                notes.append("field names from PyCozmo (widths agree with native)")
            elif nops:
                notes.append("names generated; widths from native Unpack")
        # varray count widths always come from the native helper, never from C#/PyCozmo guesses
        for i, (k, v) in enumerate(nops):
            if k == "var" and v in HELPERS and i < len(fields) and fields[i]["kind"] in ("varray", "string"):
                cnt, elem, sem = HELPERS[v]
                fields[i]["count"] = cnt
                if fields[i]["kind"] == "varray" and not fields[i].get("elem"):
                    fields[i]["elem"] = elem

        def fsize(f):
            if f["kind"] == "scalar":
                return W.get(f["type"])
            if f["kind"] == "enum":
                return W.get(enums.get(f["type"], {}).get("base", "u8"))
            if f["kind"] == "struct":
                return struct_size(f["type"]) if f["type"] in STRUCTS else None
            if f["kind"] == "farray":
                e = f["elem"]
                ew = W.get(e) or (struct_size(e) if e in STRUCTS else None) or \
                     (W.get(enums[e]["base"]) if e in enums else None)
                return ew * f["length"] if ew else None
            return None

        # Resolve fixed arrays the C# twin did not supply, using the native loop bounds + official size.
        bounds = nat.get(ctype, {}).get("loop_bounds", [])
        if ctype in STRUCT_ARRAY and not any(f["kind"] == "farray" for f in fields):
            sname, slen = STRUCT_ARRAY[ctype]
            for f in fields:
                if f["kind"] == "struct" and f["type"] == sname:
                    f["kind"] = "farray"; f["elem"] = sname; f["length"] = slen
                    f.setdefault("name_source", "generated")
                    notes.append("%s[%d]: native loop bound, C# twin size and capture body length agree"
                                 % (sname, slen))
                    break
        elif bounds and nsize not in (None, "variable") and not any(f["kind"] == "farray" for f in fields):
            parts0 = [fsize(f) for f in fields]
            if all(p is not None for p in parts0) and sum(parts0) != nsize:
                sol = solve_arrays(fields, bounds, nsize, enums)
                if sol:
                    for i, ln in sol:
                        f = fields[i]
                        f["elem"] = f.get("type"); f["kind"] = "farray"; f["length"] = ln
                    notes.append("fixed array lengths %s solved from native loop bounds; total matches official Size() %d B"
                                 % ([ln for _, ln in sol], nsize))
                    if confidence in ("native_only", "native_named"):
                        confidence = confidence  # names unchanged, layout now complete
                else:
                    notes.append("native loop bounds %s could not be attributed uniquely" % bounds)

        if tag in REFINEMENTS:
            r = REFINEMENTS[tag]
            fields = [dict(f) for f in r["fields"]]
            notes.append(r["note"])
            if confidence in ("native_only", "native_named"):
                confidence = "hardware_refined"

        parts = [fsize(f) for f in fields]
        declared = sum(p for p in parts if p) if all(p is not None for p in parts) else None
        if nsize not in (None, "variable") and declared is not None and declared != nsize:
            confidence = "partial"
            notes.append("declared %d B != official Size() %d B: unresolved fixed array(s); tail kept as raw"
                         % (declared, nsize))
            fields.append({"name": "unknownTail", "kind": "raw", "type": "u8", "uncertain": True,
                           "note": "%d B unaccounted" % (nsize - declared)})
        # A message is only variable-length if it actually carries a counted array, a string or an
        # unresolved raw tail. Computed after the tail is appended, or a message that just grew one would
        # still be reported as fixed. Some Size() implementations are not constant-folded even though the
        # struct is fixed (RobotState is the notable case: native Size() is a call, every field is fixed).
        variable = any(f["kind"] in ("varray", "string", "raw") for f in fields)
        effective = nsize if isinstance(nsize, int) else (declared if not variable else None)
        if nloops and confidence in ("native_only", "native_named") and not any(f["kind"] == "farray" for f in fields):
            notes.append("native Unpack has %d loop(s): one or more fields are fixed arrays not yet attributed" % nloops)
            confidence = "partial"
        o = obs.get("0x%02x" % tag)
        if confidence in ("exact", "prefix", "empty", "hardware_refined"):
            ver = "statically_verified"
        elif confidence in ("native_named", "native_only"):
            ver = "layout_known_semantics_uncertain"
        else:
            ver = "unresolved"
        if o:
            lens = set(int(k) for k in o["lengths"])
            fits = (effective is None) or (lens == {effective}) or variable
            ver = "capture_verified" if fits else "capture_conflict"
        if tag in PROBED:
            ver = "hardware_verified"
            p = PROBED[tag]
            notes.append("hardware verified on firmware %s: %d received, %d re-encoded byte-identically"
                         % (p["firmware"], p["count"], p["byteIdentical"]))
        elif tag in PROBE_CONFLICTS:
            ver = "capture_conflict"
            notes.append("probe conflict: %s" % PROBE_CONFLICTS[tag])
        if tag in ROBOT_ACCEPTED:
            ver = "hardware_verified"
            notes.append("hardware verified: %s" % ROBOT_ACCEPTED[tag])
        if tag in HARDWARE_NOTES:
            notes.append(HARDWARE_NOTES[tag])
        messages["0x%02X" % tag] = {
            "tag": tag, "member": member, "clad_type": ctype, "direction": dirname,
            "subsystem": SUBSYSTEM.get(tag, "unclassified"), "safety": SAFETY.get(tag, "state_change"),
            "official_size": effective, "native_size": nsize, "variable_length": variable,
            "declared_size": declared,
            "fields": [{k: v for k, v in f.items() if v is not None} for f in validate(tag, ctype, fields)],
            "confidence": confidence, "verification": ver,
            "evidence": {"native_unpack": nat.get(ctype, {}).get("addr"),
                         "native_namespace": nat.get(ctype, {}).get("ns"),
                         "csharp": bool(twin),
                         "pycozmo_name": pyc["packets"].get("0x%02x" % tag, {}).get("name"),
                         "capture": (o and {"count": o["count"], "lengths": o["lengths"], "dirs": o["dirs"]})},
            "notes": notes}

doc = {
    "meta": {
        "source": "libcozmoEngine.so 3.4.0-1204 (authoritative) + decompiled C# CLAD + hardware captures (fw 2457) + PyCozmo (names only)",
        "engine_to_robot": sum(1 for m in messages.values() if m["direction"] == "engine_to_robot"),
        "robot_to_engine": sum(1 for m in messages.values() if m["direction"] == "robot_to_engine"),
        "generated_by": "re-analysis/tools/build_protocol_definition.py"},
    "structs": {k: [{"name": n, "type": t} for n, t in v] for k, v in STRUCTS.items()},
    "enums": {k: {"base": v["base"], "values": [{"name": n, "value": val} for n, val in v["values"]
                                                if isinstance(val, int)]} for k, v in enums.items()},
    "helpers": {k: {"count": v[0], "elem": v[1], "semantic": v[2]} for k, v in HELPERS.items()},
    "messages": dict(sorted(messages.items(), key=lambda kv: kv[1]["tag"]))}

out = os.path.join(RA, "protocol", "cozmo_robot_protocol.json")
json.dump(doc, open(out, "w"), indent=1)
c = collections.Counter(m["confidence"] for m in messages.values())
v = collections.Counter(m["verification"] for m in messages.values())
print("wrote", out, len(messages), "messages")
print("confidence:", dict(c))
print("verification:", dict(v))
print("by subsystem:", dict(collections.Counter(m["subsystem"] for m in messages.values())))
print("partial:", [k for k, m in messages.items() if m["confidence"] == "partial"])
