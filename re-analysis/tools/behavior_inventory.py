"""Inventory the shipped Cozmo behaviour system and classify what each behaviour needs.

Joins three sources, all of them Anki's:

  * the 178 behaviour configs under config/engine/behaviorSystem/behaviors/ (behaviorClass, behaviorID,
    and whatever parameters each carries)
  * the decompiled BehaviorClass and BehaviorID enums
  * the Behavior* classes the engine exports

and classifies each behaviour by what it would need before this stack could run it.

Classification is by **stated rules over evidence**, never by impression. Each behaviour records which
rule fired and what triggered it, so a classification can be argued with. A behaviour that matches no
rule is 'unclear' rather than being pushed into a plausible bucket.

    python re-analysis/tools/behavior_inventory.py            # write the report
    python re-analysis/tools/behavior_inventory.py --check    # verify it is current
"""
import collections
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
BEHAVIOR_DIR = (ROOT / "re-analysis" / "obb" / "assets" / "cozmo_resources" / "config" / "engine"
                / "behaviorSystem" / "behaviors")
CSHARP = ROOT / "unity" / "scripts" / "csharp" / "Anki.Cozmo"
EXPORTS = ROOT / "re-analysis" / "symbols" / "exports_demangled.txt"
OUT_MD = ROOT / "re-analysis" / "BEHAVIOR_INVENTORY.md"
OUT_JSON = ROOT / "re-analysis" / "behavior_inventory.json"

# Each rule is (category, reason, regex over the config's text). Order matters: the first match wins,
# so the most specific capability requirement is listed before the more general ones.
#
# Deliberately NOT anchored on word boundaries. Behaviour and id names are CamelCase — BuildPyramid,
# KnockOverCubes, InteractWithFaces — so a \b before the keyword never matches, because the transition is
# letter-to-letter. An earlier version of this file had that bug and filed 89 behaviours under the
# directory they happened to live in instead of what they need.
#
# Patterns that would fire on unrelated English are avoided rather than anchored: no bare `pose` (matches
# "purpose"), no bare `map` (matches "mapping"), no bare `mount` (matches "amount").
RULES = [
    ("requires cubes", "names cubes, blocks or objects",
     r"(?i)(cube|block|pyramid|stack|carryObject|beacon)"),
    ("requires vision/person detection", "names faces, people, pets or motion sensing",
     r"(?i)(face|person|people|pet\b|smile|eyeContact|pounce)"),
    ("requires charger/docking", "names the charger or docking",
     r"(?i)(charger|docking|dockTo)"),
    ("requires navigation/path planning", "names driving, paths or planning",
     r"(?i)(driveTo|drivePath|pathPlan|planner|goTo[A-Z]|waypoint|navigat)"),
    ("requires localization/world model", "names the world model or localization",
     r"(?i)(worldOrigin|localiz|memoryMap|worldState)"),
    # M6 resolved Wwise *events*; these behaviours select audio by switch state. M9 (2026-09-19) built the
    # switch-state path - music switch containers, the MIDI songs and the per-note vocal sampler - and the
    # Singing class runs through it (SingingBehavior), so the 39 Singing behaviours are implementable.
    # Hardware acceptance for M9 is pending; the classification is of what the stack can run.
    ("implementable with M9 (switch-state audio)", "selects audio by switch state, which M9 implements",
     r"(?i)(audioSwitchGroup|audioSwitch)"),
]

# Directory-based categories, applied when no capability rule fires. These say what a behaviour is for
# rather than what it needs.
DIR_CATEGORIES = {
    "devBehaviors": ("developer-only", "ships under devBehaviors/"),
    "freeplay": ("freeplay/explorer-specific", "ships under freeplay/"),
    "onboarding": ("game-specific", "ships under onboarding/"),
    "meetCozmo": ("game-specific", "ships under meetCozmo/"),
    "voiceCommands": ("game-specific", "ships under voiceCommands/"),
    "feeding": ("game-specific", "ships under feeding/"),
}

# Behaviour classes that do nothing themselves but run other behaviours.
COMPOSITE = re.compile(r"(?i)(dispatcher|chooser|composite|wrapper|sequence|container|activity)")

# What M1-M7 can actually do: animation, face, head, lift, wheels, lights, sensors, sound.
IMPLEMENTABLE = "implementable with M1-M7 now"

# A ReactToX behaviour is only runnable if something can tell it to run. These are the reaction causes
# M4 actually reports - the same four ReactionTable maps - so only these ReactToX classes count as
# implementable. The rest are blocked on robot state this stack does not yet derive, which is a real
# blocker and not a thin config.
#
# An earlier version of this file counted "no parameters beyond class and id" as implementable, which
# quietly promoted ReactToRobotOnBack, ReactToImpact, ReactToSparked and friends. A thin config means the
# behaviour is native-driven, not that it is easy.
DETECTABLE_REACTIONS = {
    "ReactToCliff",       # RobotStatusFlag.CliffDetected
    "ReactToPickup",      # RobotStatusFlag.IsPickedUp
    "ReactToOnCharger",   # RobotStatusFlag.IsOnCharger
}

UNDETECTED_STATE = "requires robot state not yet derived"


def strip_comments(text):
    return re.sub(r"//[^\n\r]*", "", text)


def load_enum(name):
    path = CSHARP / f"{name}.cs"
    if not path.exists():
        return []
    body = path.read_text(encoding="utf-8").split("{", 1)[1].rsplit("}", 1)[0]
    out = []
    for line in body.splitlines():
        line = line.strip().rstrip(",").strip()
        if line and not line.startswith("//") and "=" not in line:
            out.append(line)
    return [n for n in out if n not in ("Count", "NoneBehavior", "Invalid")]


def native_classes():
    if not EXPORTS.exists():
        return set()
    text = EXPORTS.read_text(encoding="utf-8", errors="replace")
    return set(re.findall(r"Anki::Cozmo::(Behavior[A-Za-z0-9_]+)", text))


def classify(entry):
    """Returns (category, reason). Rules are tried in order; the first to fire wins.

    The text searched is the class name, the id and the config body together. Most shipped configs are
    thin - a reaction config carries only behaviorClass and behaviorID - so the class name Anki chose is
    usually the only evidence there is about what a behaviour needs. Ignoring it would push most of the
    system into whichever directory it happens to live in, which says nothing about dependencies.
    """
    blob = entry["behaviorClass"] + " " + entry["behaviorID"] + "\n" + entry["raw"]
    if COMPOSITE.search(entry["behaviorClass"]):
        return "wrapper/composite behavior", f"class name '{entry['behaviorClass']}' names a composite"
    for category, reason, pattern in RULES:
        m = re.search(pattern, blob)
        if m:
            return category, f"{reason} ('{m.group(0)}')"
    for part in entry["path"].split("/"):
        if part in DIR_CATEGORIES:
            return DIR_CATEGORIES[part]
    cls = entry["behaviorClass"]
    if cls.startswith("ReactTo"):
        return (IMPLEMENTABLE, "its cause is reported by M4 sensors") if cls in DETECTABLE_REACTIONS             else (UNDETECTED_STATE, f"nothing yet derives the state '{cls}' reacts to")
    if entry["animTriggers"] or cls in ("PlayAnimWithFace", "PlayAnim", "PlayArbitraryAnim"):
        return IMPLEMENTABLE, "plays an animation and asks for nothing else"
    if not entry["extraKeys"]:
        return "unclear", "a thin config: the behaviour is native-driven, so what it needs is not stated"
    return "unclear", f"no rule matched; carries {sorted(entry['extraKeys'])[:4]}"


def collect():
    entries = []
    for path in sorted(BEHAVIOR_DIR.rglob("*.json")):
        raw = path.read_text(encoding="utf-8", errors="replace")
        try:
            doc = json.loads(strip_comments(raw))
        except json.JSONDecodeError:
            continue
        rel = path.relative_to(BEHAVIOR_DIR).as_posix()
        keys = set(doc) - {"behaviorClass", "behaviorID", "displayNameKey"}
        entry = {
            "path": rel,
            "behaviorID": doc.get("behaviorID", "?"),
            "behaviorClass": doc.get("behaviorClass", "?"),
            "animTriggers": doc.get("animTriggers", []),
            "extraKeys": keys,
            "raw": raw,
        }
        entry["category"], entry["reason"] = classify(entry)
        entries.append(entry)
    return entries


def render(entries, ids, classes, native):
    by_cat = collections.Counter(e["category"] for e in entries)
    config_classes = {e["behaviorClass"] for e in entries}

    lines = [
        "# M8 — the shipped Cozmo behaviour system, inventoried",
        "",
        "Generated by `re-analysis/tools/behavior_inventory.py`. Do not edit by hand.",
        "",
        "## Sources",
        "",
        f"* **{len(entries)}** behaviour configs under "
        "`config/engine/behaviorSystem/behaviors/`, each naming a `behaviorClass` and a `behaviorID`",
        f"* **{len(ids)}** `BehaviorID` and **{len(classes)}** `BehaviorClass` values, from the decompiled enums",
        f"* **{len(native)}** `Behavior*` classes visible in the engine's exports",
        "",
        "## How each behaviour was classified",
        "",
        "By stated rules over the config's own text, in this order, first match winning. Every row records",
        "which rule fired and what triggered it, so a classification can be argued with rather than taken",
        "on trust. A behaviour matching no rule is **unclear**, not pushed into a plausible bucket.",
        "",
        "1. class name names a composite → wrapper/composite",
        "2. text names cubes, blocks or objects → requires cubes",
        "3. text names faces, people or pets → requires vision",
        "4. text names the charger or docking → requires charger/docking",
        "5. text names driving, paths or poses → requires navigation",
        "6. text names the world, map or origin → requires localization",
        "7. text selects audio by switch state → implementable with M9 (switch-state audio)",
        "8. otherwise, the directory it ships in gives its purpose",
        "9. plays animations and asks for nothing else → implementable now",
        "",
        "A rule firing on a *mention* is deliberately cautious: a behaviour that merely refers to cubes is",
        "counted as needing them. That overstates the blocked count rather than the implementable one.",
        "",
        "## Totals",
        "",
        "| category | behaviours |",
        "| --- | ---: |",
    ]
    for cat, n in by_cat.most_common():
        lines.append(f"| {cat} | {n} |")
    lines += [
        f"| **total** | **{len(entries)}** |",
        "",
        "## Coverage of the enums",
        "",
        f"* configs cover {len(config_classes)} of the {len(classes)} `BehaviorClass` values",
        f"* {len(set(e['behaviorID'] for e in entries))} distinct `behaviorID`s appear in configs, "
        f"against {len(ids)} in the enum",
        "",
        "## Every behaviour",
        "",
        "| behaviourID | class | category | why | config |",
        "| --- | --- | --- | --- | --- |",
    ]
    for e in sorted(entries, key=lambda x: (x["category"], x["behaviorID"])):
        reason = e["reason"].replace("|", "\\|")
        lines.append(f"| {e['behaviorID']} | {e['behaviorClass']} | {e['category']} | {reason} | `{e['path']}` |")

    missing = sorted(set(classes) - config_classes)
    if missing:
        lines += [
            "",
            "## BehaviorClass values with no shipped config",
            "",
            "These exist in the enum but no config names them, so nothing in the shipped data would",
            "instantiate them. They are listed for completeness rather than counted as blocked work.",
            "",
            ", ".join(f"`{m}`" for m in missing),
        ]
    lines.append("")
    return "\n".join(lines)


def main():
    if not BEHAVIOR_DIR.exists():
        print(f"no behaviour configs at {BEHAVIOR_DIR}; is the OBB unpacked?")
        return 1
    entries = collect()
    ids = load_enum("BehaviorID")
    classes = load_enum("BehaviorClass")
    native = native_classes()
    md = render(entries, ids, classes, native)
    payload = json.dumps(
        [{k: (sorted(v) if isinstance(v, set) else v) for k, v in e.items() if k != "raw"}
         for e in entries], indent=1)

    if "--check" in sys.argv:
        bad = 0
        for path, want in ((OUT_MD, md), (OUT_JSON, payload)):
            current = path.read_text(encoding="utf-8") if path.exists() else ""
            if current.replace("\r\n", "\n") != want:
                print(f"OUT OF DATE: {path}")
                bad = 1
        if not bad:
            print(f"up to date: {len(entries)} behaviours")
        return bad

    OUT_MD.write_text(md, encoding="utf-8", newline="\n")
    OUT_JSON.write_text(payload, encoding="utf-8", newline="\n")
    counts = collections.Counter(e["category"] for e in entries)
    print(f"wrote {OUT_MD} and {OUT_JSON}: {len(entries)} behaviours")
    for cat, n in counts.most_common():
        print(f"  {n:4}  {cat}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
