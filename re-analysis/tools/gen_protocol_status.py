"""Render re-analysis/PROTOCOL_STATUS.md from protocol/cozmo_robot_protocol.json.

usage: python gen_protocol_status.py <re-analysis-dir>
"""
import json, os, sys, collections

RA = sys.argv[1]
doc = json.load(open(os.path.join(RA, "protocol", "cozmo_robot_protocol.json")))
M = doc["messages"]

SUB_TITLE = {
    "identity_version_logging": "Identity / version / logging",
    "robot_state_sensors": "Robot state and sensors",
    "motors": "Head / lift / wheels",
    "leds_display": "LEDs and display",
    "camera": "Camera",
    "audio": "Audio",
    "animation": "Animation",
    "cubes_ble": "Cubes and BLE",
    "localization_navigation": "Localization and navigation",
    "firmware_update_recovery": "Firmware / update / recovery",
    "factory_debug_storage": "Factory / debug / storage",
    "unclassified": "Unclassified",
}
VER_TITLE = {
    "hardware_verified": "hardware verified",
    "capture_verified": "capture verified",
    "statically_verified": "statically verified",
    "layout_known_semantics_uncertain": "layout known, semantics uncertain",
    "unresolved": "unresolved",
    "capture_conflict": "capture conflict",
}
ORDER = ["identity_version_logging", "robot_state_sensors", "motors", "leds_display", "camera", "audio",
         "animation", "cubes_ble", "localization_navigation", "firmware_update_recovery",
         "factory_debug_storage", "unclassified"]


def fieldstr(f):
    k = f["kind"]
    if k == "farray":
        return "%s %s[%d]" % (f["name"], f.get("elem", f["type"]), f["length"])
    if k == "varray":
        return "%s %s[%s count]" % (f["name"], f.get("elem", f["type"]), f.get("count", "u16"))
    if k == "string":
        return "%s string[%s count]" % (f["name"], f.get("count", "u8"))
    if k == "raw":
        return "%s raw tail" % f["name"]
    return "%s %s" % (f["name"], f["type"])


ver = collections.Counter(m["verification"] for m in M.values())
conf = collections.Counter(m["confidence"] for m in M.values())
nfields = sum(len(m["fields"]) for m in M.values())
unnamed = sum(1 for m in M.values() for f in m["fields"] if f.get("name_source") == "generated")
uncertain = sum(1 for m in M.values() for f in m["fields"] if f.get("uncertain"))
src = collections.Counter(f.get("name_source", "csharp") for m in M.values() for f in m["fields"])

L = ["# Robot protocol status",
     "",
     "Generated from `protocol/cozmo_robot_protocol.json` by `tools/gen_protocol_status.py`. That JSON is the",
     "canonical definition; the C# codecs in `../cozmo-stack/src/Cozmo.Protocol/Generated/` are generated from it",
     "by `tools/gen_protocol.py` and are never edited by hand.",
     "",
     "Evidence order: (1) official decompiled C# CLAD structs, (2) `libcozmoEngine.so` 3.4.0-1204 `Unpack`/`Size`",
     "(authoritative for widths, order and total size), (3) hardware captures from a firmware-2457 robot,",
     "(4) PyCozmo for names only, never over the native layout.",
     "",
     "## Totals",
     "",
     "| | count |",
     "|---|---|",
     "| official robot messages | **%d** (%d engine->robot, %d robot->engine) |" %
     (len(M), doc["meta"]["engine_to_robot"], doc["meta"]["robot_to_engine"]),
     "| fields defined | %d |" % nfields,
     "| messages with a complete byte layout | **%d** |" % (len(M) - conf["partial"]),
     "| messages with unresolved fields | %d |" % conf["partial"],
     "| nested structs | %d |" % len(doc["structs"]),
     "| enums carried over from the official C# | %d |" % len(doc["enums"]),
     "",
     "### Verification status",
     "",
     "| status | count | meaning |",
     "|---|---|---|",
     "| hardware verified | %d | seen on the firmware-2457 robot; decoded and re-encoded byte-identically |" % ver["hardware_verified"],
     "| statically verified | %d | layout matches an official C# CLAD struct field for field, or is empty |" % ver["statically_verified"],
     "| layout known, semantics uncertain | %d | widths/order from the engine binary; some field names are guesses |" % ver["layout_known_semantics_uncertain"],
     "| unresolved | %d | one or more fields not attributed; the bytes are preserved in a raw tail |" % ver["unresolved"],
     "| capture conflict | %d | observed bytes disagree with the static layout |" % ver["capture_conflict"],
     "",
     "### Where field names come from",
     "",
     "| source | fields |",
     "|---|---|"]
for k, v in src.most_common():
    L.append("| %s | %d |" % ({"csharp": "official decompiled C#", "pycozmo": "PyCozmo (widths agreed with native)",
                               "hardware": "hardware capture", "generated": "generated placeholder"}.get(k, k), v))
L += ["",
      "%d of %d fields still carry a generated placeholder name; %d fields are flagged uncertain." % (unnamed, nfields, uncertain),
      "Unknown bytes are never invented: a message whose layout does not add up keeps an explicit `unknownTail`",
      "raw field, and placeholder names are `field0`, `field1`, ... so they cannot be mistaken for official ones.",
      "",
      "## By subsystem",
      ""]

for sub in ORDER:
    rows = [m for m in M.values() if m["subsystem"] == sub]
    if not rows:
        continue
    v = collections.Counter(m["verification"] for m in rows)
    L += ["### %s (%d messages)" % (SUB_TITLE[sub], len(rows)), "",
          "%s" % ", ".join("%d %s" % (c, VER_TITLE[k]) for k, c in v.most_common()), "",
          "| tag | dir | CLAD type | size | layout | verification | probe safety |",
          "|---|---|---|---|---|---|---|"]
    for m in sorted(rows, key=lambda x: x["tag"]):
        size = "var" if m["variable_length"] else (m["official_size"] if m["official_size"] is not None else "?")
        L.append("| `0x%02X` | %s | %s | %s | %s | %s | %s |" %
                 (m["tag"], "E->R" if m["direction"] == "engine_to_robot" else "R->E", m["clad_type"], size,
                  m["confidence"], VER_TITLE[m["verification"]], m["safety"]))
    L.append("")

L += ["## What remains unknown", "",
      "### Messages with unresolved fields", ""]
part = [m for m in M.values() if m["confidence"] == "partial"]
if part:
    for m in sorted(part, key=lambda x: x["tag"]):
        L += ["* **0x%02X %s** (%s, %s B). %s" %
              (m["tag"], m["clad_type"], m["subsystem"], m["official_size"], " ".join(m["notes"])),
              "  Fields: %s" % ", ".join(fieldstr(f) for f in m["fields"])]
else:
    L.append("* none")

L += ["", "### Messages whose names are still guesses", "",
      "These have the right widths and order (from the engine's own `Unpack`) but no official C# struct to name",
      "them, so some field names are placeholders. Sending them is safe; interpreting the fields is not yet.", ""]
nm = [m for m in M.values() if m["confidence"] in ("native_only",) and m["fields"]]
L += ["| tag | CLAD type | subsystem | fields |", "|---|---|---|---|"]
for m in sorted(nm, key=lambda x: x["tag"]):
    L.append("| `0x%02X` | %s | %s | %s |" % (m["tag"], m["clad_type"], m["subsystem"],
                                              ", ".join(fieldstr(f) for f in m["fields"])))

L += ["", "### Not yet seen on hardware", "",
      "Every message below has a static layout but has not been exercised on a robot. The `probe` command covers",
      "the safe ones; motion, state-change and destructive messages need an explicit opt-in or stay out of scope.", ""]
unseen = [m for m in M.values() if m["verification"] != "hardware_verified"]
bysub = collections.Counter(m["subsystem"] for m in unseen)
L += ["| subsystem | not yet hardware verified | of which safe to probe |", "|---|---|---|"]
for sub in ORDER:
    if bysub[sub]:
        safe = sum(1 for m in unseen if m["subsystem"] == sub and m["safety"] in ("read_only", "safe_visible"))
        L.append("| %s | %d | %d |" % (SUB_TITLE[sub], bysub[sub], safe))

L += ["", "## Firmware compatibility", "",
      "The reference engine is Anki 3.4.0-1204, whose shipped firmware is 2381. The robot used for verification",
      "runs **2457** (a 2025 Digital Dream Labs build). Its CLAD hashes differ from 2381:",
      "",
      "| | engine->robot hash | robot->engine hash |",
      "|---|---|---|",
      "| firmware 2381 (shipped in this APK) | `9e4a965ace4e09d86997b87ba14235d5` | `a259247f16231db440957215baba12ab` |",
      "| firmware 2457 (robot under test) | `fedb4b12f1b5b45456aec1629cd0b8cc` | `5a7211095fc4961407e96b9126658317` |",
      "",
      "Every message exercised so far is **layout-compatible across both**, so the stack does not reject a robot",
      "on a hash mismatch: it records both hashes, reports them, and relies on observed per-message compatibility.",
      "If a future firmware does change a layout, the per-message verification status above is where it will show",
      "up, and the definition file can carry firmware-specific variants at that point.",
      "",
      "## Reproducing",
      "",
      "```",
      "python tools/build_protocol_definition.py <scratch> .        # rebuild the canonical JSON from all evidence",
      "python tools/gen_protocol.py . ../cozmo-stack                # regenerate the C# codecs and catalog",
      "python tools/gen_protocol_status.py .                        # regenerate this document",
      "cd ../cozmo-stack && dotnet test Cozmo.sln                   # sizes, round trips, capture replay",
      "```", ""]

out = os.path.join(RA, "PROTOCOL_STATUS.md")
open(out, "w", encoding="utf-8").write("\n".join(L))
print("wrote", out, len(L), "lines")
