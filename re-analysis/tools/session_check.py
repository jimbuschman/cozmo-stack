from __future__ import annotations

import argparse
import base64
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from datetime import datetime
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "cozmo-stack/src"
ROUNDS = SOURCE / "Cozmo.Conformance"
BEHAVIOR = SOURCE / "Cozmo.Robot/Behavior"
PASSED = "PASSED"
SEEN = "SEEN-NOT-PASSED"
NOT_EXERCISED = "NOT EXERCISED"
COMPLETION = "M8-015, E2G 0x5A at 0x0053FAAA–0x0053FAB2"
BEHAVIOR_REPORT = "M8-012, E2G 0x87 at 0x005A22B4–0x005A24AC"
REACTION_REPORT = "M8-012, E2G 0x88 at 0x005A20AA–0x005A20D6 and 0x005A2EE8"
EDGE_TERMINAL_SIGNALS = {
    "Hiking_VisitInterestingEdge.S3": "observe from close",
    "Hiking_VisitInterestingEdge.GatheringAccurateEdge.Done": "edge lost during gathering",
}
MISSING = re.compile(r"\bMISSING(?:\b|:)")
RECORD = re.compile(r"\bM\d+-\d{3}\b")
ADDRESS = re.compile(r"0x[0-9a-fA-F]{6,8}\b")
Q = r'"([^"\n]+)"'


def source(path):
    return path.read_text(encoding="utf-8-sig")


def enum_members(name):
    body = source(BEHAVIOR / f"{name}.g.cs").split(f"enum {name} : byte", 1)[1]
    members = re.findall(r"^\s*(\w+),\s*$", body, re.M)
    return {name: ordinal for ordinal, name in enumerate(members)}


def load_rounds():
    # These declarations are the round data. Fail loudly if their shape changes
    # instead of silently dropping acceptance criteria.
    commands = {}
    for name, plan, _, record, address, expected in re.findall(
        rf'new\({Q}, {Q}, {Q}, {Q}, {Q}(?:, (0x[0-9A-Fa-f]+|null))?\)',
        source(ROUNDS / "CommandsHw.cs")
    ):
        commands[name] = (plan, f"{record}, {address}", None if expected == "null" else int(expected or "0", 0))
    signals = {}
    for name, plan, tag, report, record, address, seconds in re.findall(
        rf'new\({Q}, {Q}, (0x\w+),\s*{Q}, {Q}, {Q}(?:, (\d+))?\)',
        source(ROUNDS / "CommandEventsHw.cs")
    ):
        signals[name] = (plan, int(tag, 16), report, f"{record}, {address}", int(seconds or 20))
    personality = {}
    data = source(ROUNDS / "PersonalityHw.cs").split("Step[] Steps", 1)[1].split("SelectSteps", 1)[0]
    for name, plan, check in re.findall(
        rf'^        new\({Q},\s*{Q},(?:(?!^        new\(").)*?\n\s*new\(BehaviorID\.(.*?)(?=^        new\("|^    \}};)',
        data, re.S | re.M
    ):
        identity = re.match(r"\w+", check)[0]
        citation = re.search(r',\s*' + Q, check)
        if citation:
            evidence = citation[1]
        else:
            assert "PhysicalReactionSource" in check, name
            evidence = "config/engine/behaviorSystem/reactionTrigger_behavior_map.json"
        trigger = re.search(r"Trigger: ReactionTrigger\.(\w+)", check)
        window = re.search(r"WindowSeconds: (\d+)", check)
        personality[name] = (plan, identity, trigger[1] if trigger else None,
                             int(window[1]) if window else 45, evidence)
    if len(commands) != 13 or len(signals) != 7 or len(personality) != 23:
        raise ValueError("The hardware round declarations changed. Review the session criteria.")
    return commands, signals, personality


@dataclass
class Call:
    tool: str
    args: dict
    start: float
    end: float | None = None
    reply: dict = field(default_factory=dict)
    cancelled_at: float | None = None


@dataclass
class Verdict:
    status: str
    reason: str
    citation: str
    prompt: str
    window: tuple[float, float] | None = None
    aliases: tuple[str, ...] = ()
    missing: list[int] = field(default_factory=list)
    other_missing: list[int] = field(default_factory=list)
    blocked_by_missing: bool = False


def read_jsonl(path):
    with path.open(encoding="utf-8-sig") as lines:
        for number, line in enumerate(lines, 1):
            if line.strip():
                try:
                    yield json.loads(line)
                except json.JSONDecodeError as error:
                    raise ValueError(f"{path.name}:{number}: {error.msg}") from error


def timestamp(value):
    return datetime.fromisoformat(value.replace("Z", "+00:00")).timestamp() * 1000


def add_image_chunk(chunk, colour_ids, image_times):
    # M2-012, ImageChunk 0xF2. 0x004F1D7A–0x004F1D9C reads the first
    # chunk's encoding and data byte. Firmware may timestamp only the last
    # chunk, so join the two fields by the frame's ImageId.
    if len(chunk) < 22 or chunk[0] != 0xF2:
        return
    stamp, image_id = struct.unpack_from("<II", chunk, 1)
    if chunk[16] == 0 and (chunk[13] == 9 or (chunk[13] == 8 and chunk[21] != 0)):
        colour_ids.add(image_id)
    if stamp:
        image_times.setdefault(image_id, set()).add(stamp)


def robot_payloads(raw):
    # M1-002 and M1-003, FrameCodec.TryDecode. This read-only fallback
    # needs only application payloads, with no reassembly or sequence state.
    if len(raw) < 14 or raw[:7] != b"COZ\x03RE\x01":
        return ()
    kind = raw[7]
    if kind in (4, 5):
        return (raw[14:],)
    if kind not in (7, 8, 9):
        return ()
    payloads = []
    offset = 14
    while offset + 3 <= len(raw):
        subtype, size = struct.unpack_from("<BH", raw, offset)
        offset += 3
        if not 1 <= subtype <= 11 or offset + size > len(raw):
            return ()
        if subtype in (4, 5):
            payloads.append(raw[offset:offset + size])
        offset += size
    return payloads if offset == len(raw) else ()


class Session:
    def __init__(self, path):
        self.path = path
        self.result = json.loads(source(path / "result.json"))
        if self.result.get("testId") != "LLM-SESSION":
            raise ValueError("result.json does not describe an LLM-SESSION bundle")
        self.calls = defaultdict(list)
        self.reports = defaultdict(list)
        self.public = defaultdict(dict)
        self.missing = []
        self.edge_terminal_signals = []
        self.activity_changes = []
        self.resolved_markers = []
        self.end = 0.0
        self.startup = None
        self.offset = None
        self.read_events()
        self.read_missing()
        messages = path / "messages.jsonl"
        frames = path / "frames.jsonl"
        self.wire_counts = Counter()
        colour_ids = set()
        image_times = {}
        self.last_message = None
        # Engine-to-game tags are captured in events.jsonl. The two wire files
        # carry transport and firmware evidence with independent tag spaces.
        for message in read_jsonl(messages):
            tag = int(message["tag"], 16)
            self.wire_counts[(message["outbound"], tag)] += 1
            self.last_message = message["atMs"]
            if not message["outbound"] and tag == 0xF2:
                chunk = bytes.fromhex(message["payloadHex"][:44])
                add_image_chunk(chunk, colour_ids, image_times)
        self.frame_count = 0
        self.last_frame = None
        self.raw_tail = False
        for frame in read_jsonl(frames):
            self.frame_count += 1
            self.last_frame = frame["atMs"]
            if frame.get("outbound") or (self.last_message is not None and self.last_frame <= self.last_message):
                continue
            self.raw_tail = True
            if frame.get("error") or not frame.get("rawBase64"):
                continue
            for payload in robot_payloads(base64.b64decode(frame["rawBase64"], validate=True)):
                add_image_chunk(payload, colour_ids, image_times)
        self.wire_image_times = {stamp for image_id in colour_ids for stamp in image_times.get(image_id, ())}

    def read_events(self):
        pending = {}
        self.timed_missing = defaultdict(list)
        for event in read_jsonl(self.path / "events.jsonl"):
            at = float(event["atMs"])
            self.end = max(at, self.end)
            kind = event["kind"]
            if self.offset is None and event.get("timestampUtc"):
                self.offset = timestamp(event["timestampUtc"]) - at
            if kind == "startup-gate":
                self.startup = at
            if kind in {"engine-log", "stack-log"} and MISSING.search(event.get("line", "")):
                self.timed_missing[event["line"]].append(at)
            if kind == "engine-log" and event.get("name") in EDGE_TERMINAL_SIGNALS:
                self.edge_terminal_signals.append((at, EDGE_TERMINAL_SIGNALS[event["name"]]))
            if kind == "engine-log" and event.get("name") == "RobotAudioClient.UpdateActivityMusicState.NoFreeplayMusic":
                activity = re.fullmatch(r"No freeplay music for ActivityName '(Selection|Freeplay)'", event.get("text", ""))
                if activity:
                    self.activity_changes.append((at, activity[1]))
            if kind == "engine-to-game":
                self.reports[int(event["tag"], 16)].append((at, bytes.fromhex(event["bodyHex"])))
            elif kind == "mcp-input":
                method = event.get("method")
                params = event.get("params") or {}
                if method == "tools/call" and "id" in event:
                    call = Call(params.get("name", ""), params.get("arguments") or {}, at)
                    self.calls[call.tool].append(call)
                    pending[event["id"]] = call
                elif method == "notifications/cancelled":
                    call = pending.get(params.get("requestId"))
                    if call:
                        call.cancelled_at = at
            elif kind == "mcp-output":
                structured = (event.get("result") or {}).get("structuredContent") or {}
                call = pending.pop(event.get("id"), None)
                if call:
                    call.end = at
                    call.reply = structured or {"status": "rpc-error", "detail": event.get("error")}
                self.add_public(structured, at)
                self.add_public(structured.get("during") or {}, at)
                if event.get("method") == "notifications/claude/channel":
                    content = (event.get("params") or {}).get("content")
                    if isinstance(content, str):
                        self.add_public(json.loads(content), at)

    def add_public(self, page, at):
        for event in page.get("events", []):
            if not isinstance(event, dict) or "type" not in event:
                continue
            event_at = at
            if self.offset is not None and event.get("timestampUtc"):
                event_at = timestamp(event["timestampUtc"]) - self.offset
            key = event.get("cursor", (event_at, len(self.public[event["type"]])))
            self.public[event["type"]].setdefault(key, (event_at, event))

    def read_missing(self):
        # Keep the original line numbers for review. Match the exact old diagnostics
        # so a different M8-015 or M12-052 gap still blocks a verdict.
        resolved = {
            "MISSING: M1-034 M8-012: skip absent external interface for ReactionTriggerTransition at 0x005A2EFA":
                "M1-034 skips the unchecked external-interface dereference from 0x005A20BA..0x005A20D6. The policy skip is logged",
            "MISSING: M12-043 pyramid container 0x006168EC..0x006169C4 / builder 0x0061830A..0x006183B4 / "
            "cache 0x0062C5A2..0x0062C616: native current-origin family query, predicate and pose-root/distance "
            "joins are unbuilt; existing all-pairs snapshot is a candidate, not the processed-ID/old-cache source path":
                "The diagnostic was emitted by the independent host snapshot. The activity path uses the native configuration manager updated at 0x0062520C",
            "MISSING: M13-038 BehaviorStageTag EnumToString at 0x0064DA72/0x0064DA7A/0x0064DAF6: "
            "stage Info name input absent; checked field store/emission proceed":
                "The robot's broadcaster now uses the 0x0078FB14 stage-name switch and table at 0x01033FF0",
            **{f"MISSING: M13-039 client needs cache +0x{offset:X} read at 0x{address:08X}: "
               "checked ctor0x00591E50..0x00591E78 does not supply this value; initial cache input remains unknown, not zero":
                   "M1-034 skips the undefined first comparison and uses the first public state as the next baseline"
               for offset, address in ((0x2C, 0x5924C4), (0x30, 0x592514), (0x34, 0x592560))},
            "MISSING: M7-019 RobotCliffEventFinished tag0x36 native local subscriber continuity after "
            "E2G emit0x00662590 remains unverified; typed bus delivery is built, concrete native recipients are not certified":
                "The empty 0x36 report and synchronous emit are built. No 0x36 registration was found in the checked "
                "E2G fixed or factory tag sets, direct calls or immediate vtable +0x28 sites",
            "MISSING: M1-034 proposed Roll own-path GetCompletionUnion005567E0..EE/setter0075C400..410 "
            "reads four undefined unused IDs: skip full public payload; defined target/count1/false and scalar/local callback retained":
                "2026-11-22 operator decision zero-fills roll ID words 1–4 and publishes the 0x5A",
            "MISSING: M1-034/M8-015 TurnTowardsObjectAction.GetCompletionUnion 0x0054B020–0x0054B036: "
            "native interaction words are undefined; retain defined metadata only.":
                "2026-11-22 operator decision zero-fills turn-to-cube ID words 1–4 and publishes the 0x5A",
            "MISSING: M13-033 Replanning State/plan text, PrepareForPlanning and conditional EnvDump "
            "recipient at 0x004FF052..0x004FF30A remain incomplete":
                "0x004FF0C6 now prepares obstacle bounds and edge order. The remaining output is diagnostic",
            "MISSING: M8-015 Queue.Update RUNNING VizManager.SetText0x0053F65C and "
            "CozmoContext.SetSdkStatus(type1,name)0x0053F6F2 recipients are not bound; "
            "normal queue path is not classified SDK-only": "M8-015 Viz label discarded, external SDK mode excluded",
            "MISSING: M8-015 Queue.Update terminal VizManager.SetText(empty)0x0053F696 / "
            "CozmoContext.SetSdkStatus(type1,empty)0x0053F6B4 recipients before Delete are not bound":
                "M8-015 Viz label discarded, external SDK mode excluded",
            "MISSING: M1-034 M12-052 Ground completion 0x00554AE0..00554AFA: four uninitialized "
            "objectInteractionCompleted ID slots; full raw/API payload unavailable, defined saved-carry/count1/false "
            "and scalar result retained; PlaceRel normalization policy does not apply":
                "M12-052 existing M1-034 policy for undefined ID words",
            "MISSING: M12-002 PathSegment.Print 0x00507F36 -> 0x0085C034 text/body has no checked row; "
            "segment print is unbuilt": "0x0085C138 calls CoreTechPrint before the path wire send",
            "MISSING: M13-033 ComputeClosestGoalPose Debug fields/rotation-axis formatting "
            "0x00508870..0x0050890A not represented":
                "0x00508870–0x0050890A formats candidate pose and axis for Debug",
            "MISSING: M13-034 IPathPlanner.PrintPath 0x005090A0 output formatting remains unbuilt":
                "0x00509072 calls Path::PrintPath for diagnostics",
            "MISSING: M13-034 ApplyMotionProfile two-pass length/end-pose/split calls require "
            "source-backed M13-011 PathSegment helpers (0x0085BC00..0x0085C034); only checked profile-field "
            "assignment built, continuity/deceleration/splitting remain unbuilt":
                "SHORT-PLANNER-1 binds the two-pass profile at 0x00508AE4; the recorded stack and roll "
                "segments are compared in the offline replay",
            "MISSING: M13-034 slot 0x18 -> 0x1C PathIsSafe body needs separately-owned PathSegment "
            "geometry helpers; candidate short path safety is not claimed source-backed":
                "SHORT-PLANNER-1 binds the safety dispatch at 0x00649FB6; this marker records the older build",
            "MISSING: M13-033 Path::PrintPath call 0x004FFC76 output remains unbuilt":
                "0x0085C7C8 prints segments after AppendToPath",
            "MISSING: M8-021 trigger DAS blacklist/event diagnostic coverage remains incomplete; concrete CA/CB/D5/5F callbacks, selected completion name and retained playback clip are independent inputs":
                "0x005445BC is the DAS event; the existing action callbacks and completion name have separate owners",
            "MISSING: M11-033/M11-078 native ImageCache ResetHelper size-tree and image shared-control ownership 0x008744E6..0x0087465C are not replaced by host buffer assignment; bounded decoded image/valid flags only":
                "0x008744E6–0x0087465C resets prior cache validity; the captured path already retained the decoded frame and its valid flags",
            "MISSING: M11-058/M11-055 DecodeJPEG Tic/Toc 0x006B4B76/0x006B4C6C native timing producer unbuilt; no host timer substituted":
                "0x0087D048 and 0x0087D250 update diagnostics; VisionSystem checks the independent decode result at 0x006B4C70",
            "MISSING: M13-033 LatticePlanner.PlanMismatch sError stream 0x005003D0 and its error side effects remain unported":
                "0x005003D0 forwards the mismatch text to the DAS error logger via 0x0080D29C",
            "MISSING: M13-033 RobotPositionError event/format at 0x004FFDCC..0x004FFE2A remains unported":
                "0x004FFDE4 emits a DAS event before the existing plan clear and failure status",
            **{f"MISSING: M13-033 PrintPlan{label} {address} -> 0x00855E7C output remains unbuilt":
                "0x00855E7C formats the plan for debug logging" for label, address in
                (("(total)", "0x0050048E"), ("(new)", "0x005004D8"), ("", "0x00500532"), ("", "0x0050058A"))},
            "MISSING: M8-007/M8-015 TracksLockedBecause Warning0x00540530: MovementComponent.WhoIsLocking "
            "native string recipient0x00540506 is not bound; no invented owner-description text":
                "0x00540506 supplies warning text to 0x00540530; the existing TRACKS_LOCKED result is retained",
            "MISSING: M12-032 VizManager.DrawPath 0x0064A474 recipient/color handoff is unbuilt":
                "0x006BFB1A writes only the visualizer path",
            **{f"MISSING: M12-024 | EraseAllPlannerObstacles({mode}) 0x0055A626/0x0055A632 visualizer handoff absent":
                "0x006BFCCA reaches the Viz ErasePath(-1) at 0x006BFC7C" for mode in (0, 1)},
        }
        counts = Counter()
        for index, line in enumerate(source(self.path / "stack-log.txt").splitlines(), 1):
            if line in resolved:
                self.resolved_markers.append((index, line, resolved[line]))
            elif re.fullmatch(
                r"MISSING: M13-033 stdout: FaceAndApproachPlanner: deltaTheta of -?[0-9]+\.[0-9]{6} above threshold, doing backup!",
                line,
            ):
                self.resolved_markers.append((index, line, "0x004F37A8 prints before the existing backup geometry"))
            elif re.fullmatch(
                r"MISSING: M8-015 ActionCompleted full ActionResult EnumToString recipient0x005405CA missing for0x[0-9A-F]{8}"
                r"|MISSING: M8-017 sequential failure full EnumToString result at 0x0054F8C4 is not recovered for 0x[0-9A-F]{8}",
                line,
            ):
                self.resolved_markers.append((index, line, "0x005405CA and 0x0054F8C4 use the name only in the diagnostic"))
            elif MISSING.search(line):
                occurrences = self.timed_missing[line]
                occurrence = counts[line]
                counts[line] += 1
                at = occurrences[occurrence] if occurrence < len(occurrences) else None
                self.missing.append((index, at, line))

    def in_window(self, tag, first=0, last=None):
        return ((at, body) for at, body in self.reports[tag]
                if at >= first and (last is None or at <= last))

    def apply_missing(self, verdict):
        if verdict.window is None:
            return verdict
        start, end = verdict.window
        records = set(RECORD.findall(verdict.citation))
        addresses = set(ADDRESS.findall(verdict.citation))
        for index, at, line in self.missing:
            matches_source = bool(records.intersection(RECORD.findall(line)) or
                                  addresses.intersection(ADDRESS.findall(line)))
            matches_name = any(re.search(rf"(?<![\w-])(?:Behavior)?{re.escape(name)}(?:Behavior)?(?![\w-])", line, re.I)
                               for name in verdict.aliases)
            constructor_gap = (matches_name and at is not None and self.startup is not None and at < self.startup
                               and ("constructor" in line.lower() or "factory" in line.lower()))
            in_window = at is None or start <= at <= end
            if constructor_gap or (in_window and (matches_source or matches_name)):
                verdict.missing.append(index)
        if verdict.missing and verdict.status == PASSED:
            verdict.status = SEEN
            verdict.blocked_by_missing = True
            verdict.reason += " A matching MISSING marker is unresolved in this capture."
        return verdict


ACTION_TYPES = {
    "head": (0x12,), "lift": (0x13,), "drive-straight": (8,), "turn-in-place": (0x28,),
    "turn-to-face": (0x29,), "turn-to-cube": (0x2C,), "track-face": (0x23,), "animation": (0x1A,),
    "say-text": (0x20,), "go-to-pose": (12,), "pickup": (0x16, -2), "put-down": (0x18,),
    # DriveToRollObjectAction sets the RollObject child's proxy at 0x0055CAA6.
    # It copies the child type, 0x1F at 0x0055656E, to the compound.
    "roll": (0x1F,), "stack": (0x19,), "mount-charger": (-2,),
}
ACTION_ALIASES = {
    "head": ("SetHeadAngleAction",), "lift": ("SetLiftHeightAction",),
    "drive-straight": ("DriveStraightAction",), "turn-in-place": ("TurnInPlaceAction",),
    "turn-to-face": ("TurnTowardsLastFacePoseAction",), "turn-to-cube": ("TurnTowardsObjectAction",),
    "track-face": ("TrackFaceAction",), "animation": ("PlayAnimationAction",),
    "say-text": ("SayTextAction",), "go-to-pose": ("DriveToPoseAction",),
    "pickup": ("PickupObjectAction", "DriveToPickupObjectAction"),
    "put-down": ("PlaceRelObjectAction", "PlaceObjectOnGroundAction"),
    "roll": ("DriveToRollObjectAction",), "stack": ("DriveToPlaceOnObjectAction",),
    "mount-charger": ("MountChargerAction", "DriveToAndMountChargerAction"),
}
SKILLS = {
    "pickup": ("Ask Cozmo to pick up the cube.",
               "M15CubeHw.RunPair; M12-015, G2E pickup 0x0052A384–0x0052A3F0", 90),
    "put-down": ("Ask Cozmo to set the carried cube on the ground.",
                 "M15CubeHw.RunPair; M12-052, local E2G 0x5A, 0x00554AE0–0x00554AFA", 60),
    "mount-charger": ("Ask Cozmo to find and mount his charger.",
                      "M15CubeHw.RunCharger; 0x0054E7F8–0x0054E88A, charger report 0x007131E2", 120),
    "roll": ("Ask Cozmo to roll the visible light cube.",
             "M15CubeHw.RunSkills; M15-077, 0x0052AA14–0x0052AA52, completion proxy 0x0055CAA6", 120),
    "stack": ("Ask Cozmo to pick up one cube and stack it on the other.",
              "M15CubeHw.RunSkills; M15-008, 0x0052A5B2–0x0052A5F2, DriveToPlaceOnObjectAction", 90),
}
FACE_TOOLS = {
    "teach-face": (("teach-face", "enroll-face"), 0x56, 120, "enrolled"),
    "rename-face": (("rename-face",), 0x1D, 10, "renamed"),
    "erase-face": (("erase-face",), 0x57, 10, "erased"),
}
FACE_ACTION_WINDOW = 20
TRACK_SECONDS = 10


def action_tags(call, action_types, result):
    # Action reports captured under the originating MCP command carry the
    # strongest correlation signal available in older bundles.
    during = (call.reply.get("during") or {}).get("events", [])
    return {completion["tag"]
            for entry in during if entry.get("type") == "action-completed"
            for completion in [entry.get("payload", {}).get("completion", {})]
            if completion.get("actionType") in action_types and completion.get("result") == result
            and "tag" in completion}


def action_completion_variant(body):
    offset = 13 + 4 * body[12]
    return body[offset] if offset < len(body) else None


def matches_interaction_completion(body, object_id):
    offset = 13 + 4 * body[12]
    return len(body) == offset + 23 and struct.unpack_from("<B5IBB", body, offset) == (
        0, object_id, 0, 0, 0, 0, 1, 0)


def action_check(session, name, prompt, citation, expected, seconds, calls=None):
    citation += "; " + COMPLETION
    candidates = []
    for call in session.calls[name] if calls is None else calls:
        end = min(call.end if call.end is not None else session.end, call.start + 1000 * seconds)
        window = (call.start, end)
        result = call.reply.get("engineResult")
        aliases = (name, *ACTION_ALIASES[name])
        reason = call.reply.get("engineResultName") or call.reply.get("status") or "MCP reply absent"
        passed = False
        if result == expected and call.reply.get("admitted") and not call.reply.get("ownerRetired"):
            types = ACTION_TYPES[name]
            tags = action_tags(call, types, result)
            active = (other for tool, calls in session.calls.items()
                      if set(types).intersection(ACTION_TYPES.get(tool, ())) for other in calls
                      if other is not call and other.start <= end and
                      (other.end if other.end is not None else session.end) >= call.start)
            reports = [(at, tag, action_completion_variant(body)) for at, body in session.in_window(0x5A, *window) if len(body) >= 14
                       for tag, action_type, code in [struct.unpack_from("<IiI", body)]
                       if action_type in types and code == result and
                       (tag in tags if tags else 0 < tag <= 1_000_000) and
                       (name not in {"roll", "turn-to-cube"} or
                        matches_interaction_completion(body, call.args.get("objectId")))]
            if not tags and next(active, None):
                reason = "Overlapping commands could own the same completion."
            elif len(reports) == 1:
                passed, reason = True, f"Matching 0x5A action tag {reports[0][1]} at {reports[0][0]:.0f} ms."
            elif name == "put-down" and len(tags) == 1 and not reports:
                passed, reason = True, f"Matching local 0x5A action tag {next(iter(tags))}."
            else:
                reason = f"Native reply matched, with {len(reports)} attributable 0x5A reports in the round window."
            if passed and name == "track-face":
                elapsed = (call.cancelled_at - call.start) if call.cancelled_at is not None else 0.0
                if call.cancelled_at is None:
                    passed, reason = False, "The engine cancelled tracking. There is no caller cancellation in the recording."
                elif elapsed < TRACK_SECONDS * 1000 or not (call.reply.get("report") or {}).get("callerCancellationRequested"):
                    passed, reason = False, "The round requires caller cancellation after 10 s of tracking."
            if passed and name == "say-text":
                _, matched_tag, union = reports[0]
                if union != 6:
                    passed, reason = False, f"Matching SayText tag {matched_tag} carried completion variant {union}; expected defaultCompleted (6)."
            if passed and name == "animation" and call.reply.get("truncatedByRobot"):
                passed, reason = False, "The robot cut off the animation."
            if passed and name == "mount-charger":
                before = any(len(body) == 69 and struct.unpack_from("<I", body, 4)[0] == 4 and
                             struct.unpack_from("<I", body, 12)[0] == call.args.get("chargerId")
                             for _, body in session.in_window(0x44, max(0, call.start - 30_000), call.start))
                contacts = any(len(body) == 1 and body[0] == 1 for _, body in session.in_window(0x39, *window))
                if not (before and contacts):
                    passed, reason = False, "The round also requires a fresh charger report and charger contacts."
        candidates.append(session.apply_missing(Verdict(PASSED if passed else SEEN, reason, citation, prompt,
                                                       window, aliases)))
    return choose(candidates, citation, prompt)


def choose(candidates, citation, prompt):
    if not candidates:
        return Verdict(NOT_EXERCISED, "No matching request or engine report.", citation, prompt)
    winner = next((v for v in candidates if v.status == PASSED), None)
    if winner is None:
        winner = next((v for v in candidates if v.blocked_by_missing), candidates[0])
    winner.other_missing = sorted({marker for v in candidates if v is not winner for marker in v.missing}
                                  .difference(winner.missing))
    return winner


def text8(body, offset):
    if offset >= len(body) or offset + 1 + body[offset] > len(body):
        return None
    return body[offset + 1:offset + 1 + body[offset]].decode("utf-8", errors="replace")


def face_operation(session, name, step):
    prompt, citation, expected = step
    tools, tag, seconds, outcome = FACE_TOOLS[name]
    citation += f"; E2G 0x{tag:02X}"
    if tag == 0x57:
        citation += "; G2E 0x89, remove and attempt album save at 0x00657A60–0x00657A98 before the empty-name report"
    candidates = []
    enrolled = set()
    for tool in tools:
        for call in session.calls[tool]:
            end = min(call.end if call.end is not None else session.end, call.start + seconds * 1000)
            operation = call.reply.get("faceOperation") or {}
            wanted = operation.get("faceId")
            wanted_name = operation.get("name")
            reports = []
            requested_id_matches = tag != 0x57 or wanted == call.args.get("faceId")
            if wanted is not None and requested_id_matches and operation.get("outcome") == outcome and call.reply.get("engineResult") == expected:
                for at, body in session.in_window(tag, call.start, end):
                    offset = int(tag == 0x56)
                    if len(body) < offset + 5 or struct.unpack_from("<i", body, offset)[0] != wanted:
                        continue
                    if tag == 0x56 and body[0] != expected:
                        continue
                    if text8(body, offset + 4) != ("" if tag == 0x57 else wanted_name):
                        continue
                    reports.append(at)
            passed = len(reports) == 1
            reason = f"Matching face ID {wanted} at {reports[0]:.0f} ms." if passed else "Expected matching face operation and E2G report."
            v = session.apply_missing(Verdict(PASSED if passed else SEEN, reason, citation, prompt,
                                              (call.start, end), tools + ("EnrollFace", "FaceWorld")))
            if v.status == PASSED and tag == 0x56:
                enrolled.add((wanted, wanted_name, reports[0]))
            candidates.append(v)
    return choose(candidates, citation, prompt), enrolled


def signal_key(tag, body):
    if tag == 0x46 and len(body) >= 94 and len(body) >= 94 + body[56]:
        return (*struct.unpack_from("<iI", body), text8(body, 56))
    if tag == 0x4B and len(body) == 4:
        return struct.unpack("<i", body)
    if tag == 0x44 and len(body) == 69:
        at, family, _, identity = struct.unpack_from("<IIII", body)
        return identity, at, family
    if tag in (0x0F, 0x13) and len(body) == {0x0F: 21, 0x13: 12}[tag]:
        at, identity = struct.unpack_from("<II", body)
        return identity, at
    if tag == 0x52 and len(body) == 4:
        return struct.unpack("<I", body)
    return None


def public_key(name, item):
    payload = item.get("payload") or {}
    robot_time = item.get("robotTimestamp")
    if name in {"face-seen", "face-recognised"}:
        return payload.get("faceId"), robot_time, payload.get("name")
    if name == "face-lost":
        return (payload.get("faceId"),)
    if name in {"cube-tapped", "cube-moved"}:
        return (payload.get("cube") or {}).get("worldObjectId"), robot_time
    if name == "object-pose-lost":
        return (payload.get("objectId"),)
    return payload.get("objectId"), robot_time, payload.get("family")


def signal_check(session, name, step, enrolled=()):
    prompt, tag, report, citation, seconds = step
    raw = defaultdict(list)
    family = 2 if name == "cube-seen" else 4
    for at, body in session.in_window(tag):
        key = signal_key(tag, body)
        if key is None:
            continue
        if name in {"cube-seen", "charger-seen"} and key[2] != family:
            continue
        if name == "face-seen" and key[2] != "":
            continue
        if name == "face-recognised" and not key[2]:
            continue
        raw[key].append(at)
    candidates = []
    for public_at, item in session.public[name].values():
        key = public_key(name, item)
        for raw_at in raw.get(key, ()):
            if abs(raw_at - public_at) > seconds * 1000:
                continue
            start = min(raw_at, public_at)
            if name == "face-recognised":
                teach = next((at for identity, label, at in enrolled
                              if (identity, label) == (key[0], key[2]) and 0 <= raw_at - at <= seconds * 1000), None)
                if teach is None:
                    continue
                start = teach
            v = Verdict(PASSED, f"Matched {report} 0x{tag:02X} to MCP at {raw_at:.0f} ms.",
                        citation, prompt, (start, max(raw_at, public_at)), (name, report.split()[0]))
            candidates.append(session.apply_missing(v))
    if name == "face-recognised":
        # In ordinary sessions the caller can read state without draining events.
        # A named album entry alone does not prove a sighting. Require FaceWorld's
        # last visual timestamp to match the raw named 0x46 for that same face.
        for call in session.calls["get_state"]:
            if call.end is None:
                continue
            for face in call.reply.get("knownFaces", []):
                key = face.get("faceId"), face.get("lastObservedTimestamp"), face.get("name")
                for identity, label, taught_at in enrolled:
                    if (identity, label) != (key[0], key[2]) or not taught_at <= call.end <= taught_at + seconds * 1000:
                        continue
                    raw_at = next((at for at in raw.get(key, ()) if taught_at <= at <= call.end), None)
                    if raw_at is None:
                        continue
                    v = Verdict(PASSED, f"Named 0x46 at {raw_at:.0f} ms matches the fresh get_state sighting "
                                f"at {call.end:.0f} ms.", citation, prompt,
                                (taught_at, call.end), (name, "RobotObservedFace"))
                    candidates.append(session.apply_missing(v))
    if raw and not candidates:
        times = [at for values in raw.values() for at in values]
        reason = "Named face seen, with no matching public sighting within 20 s of the teach." if name == "face-recognised" else "Engine report present without a matching public MCP payload."
        candidates.append(session.apply_missing(Verdict(SEEN, reason, citation, prompt,
                                                        (min(times), max(times)), (name, report.split()[0]))))
    return choose(candidates, citation, prompt)


PHYSICAL = {
    "ReactToCliff": "cliff", "ReactToPickup": "pickup", "ReactToRobotOnBack": "on-back",
    "ReactToRobotOnFace": "on-face", "ReactToRobotOnSide": "on-side", "ReactToRobotShaken": "shaken",
    "ReactToReturnedToTreads": "putdown", "ReactToImpact": "react-to-impact",
    "ReactToPlacedOnSlope": "react-to-placed-on-slope", "ReactToUnexpectedMovement": "react-to-unexpected-movement",
    "ReactToMotorCalibration": "react-to-motor-calibration", "ReactToOnCharger": "react-to-on-charger",
    "ReactToFrustration": "react-to-frustration",
}
REACTION_EXIT_ROUNDS = {"on-face", "on-side", "shaken", "react-to-placed-on-slope", "react-to-frustration"}
NEEDS = {
    "ExpressNeeds": "express-needs", "PlayAnimOnNeedsChange": "needs-change-animation",
    "DriveInDesperation": "drive-in-desperation", "EarnedSparks": "earned-sparks",
}
IDLE = (
    "PlayAnim", "PlayAnimWithFace", "PlayArbitraryAnim", "Wait", "ExploreLookAroundInPlace",
    "FindFaces", "LookForFaceAndCube", "VisitInterestingEdge", "PounceOnMotion", "ThinkAboutBeacons",
)
FACES = {
    "AcknowledgeFace": "acknowledge-face", "InteractWithFaces": "interact-with-faces",
    "SearchForFace": "search-for-face", "DriveToFace": "drive-to-face", "EnrollFace": None,
    "RespondToRenameFace": None, "ReactToPet": "react-to-pet",
}
CUBES = ("AcknowledgeObject", "ReactToCubeMoved", "PickUpCube", "PutDownBlock", "RollBlock", "StackBlocks", "DriveOffCharger")
CUBE_FREEPLAY_ROUNDS = {
    "PickUpCube": (
        ("SparksPickupSingleCubeForPyramid", "SparksPickupSingleCubeToStack"),
        "Observe the configured pickup in the pyramid build phase. Keep one eligible cube in view and the lift empty.",
        "buildPyramid.json and 0x005A888C–0x005A88B2 select the build chooser. "
        "Pickup selects a located eligible cube at 0x005C6628–0x005C6644 and 0x005C6868–0x005C6A88. It delegates at 0x005C67AE–0x005C67BE.",
        None, True,
    ),
    "PutDownBlock": (
        ("PutDownBlock", "PutDownBlockNothingToDo", "PyramidPutDownBlock"),
        "Start Freeplay holding one cube. Keep faces and other cubes out of view so the NothingToDo fallback can choose PutDownBlockNothingToDo.",
        "nothingToDo.json scores the carry-only fallback at 1.1. The carried ID gate and final release are "
        "at 0x005C7FB0–0x005C7FCA and 0x005C8480–0x005C84D4.",
        None, False,
    ),
    "StackBlocks": (
        ("StackBlocks",),
        "Use two upright, distinct cubes and start Freeplay with stacking unlocked. Keep people out of view and let the cube-only scene select PlayAlone.",
        "playAlone.json scores StackBlocks at 0.8. Native targets are refreshed at 0x005C94A6–0x005C9500. "
        "Objective 40 is emitted on placement success at 0x005C9EA8.",
        40, None,
    ),
    "DriveOffCharger": (
        ("DriveOffCharger", "Hiking_DriveOffCharger"),
        "Start on the charger and enable Freeplay. Leave a clear path straight ahead and wait until the behavior leaves the platform.",
        "nothingToDo.json scores DriveOffCharger at 1000. Platform gate 0x005C0B10, departure update "
        "0x005C0DA8–0x005C0E18 and success objective 4 at 0x005C0E9C–0x005C0EAE.",
        4, None,
    ),
}


def behavior_checks(session, personality):
    names = enum_members("BehaviorID")
    classes = enum_members("BehaviorClass")
    triggers = enum_members("ReactionTrigger")
    for group, members in (("Physical reactions", PHYSICAL), ("Needs", NEEDS),
                           ("Idle life", dict.fromkeys(IDLE)), ("Faces", FACES), ("Cubes", dict.fromkeys(CUBES))):
        for name, step in members.items():
            if name == "VisitInterestingEdge":
                yield group, name, visit_interesting_edge_check(session, names)
                continue
            if name == "RespondToRenameFace":
                yield group, name, rename_response_check(session, names)
                continue
            citation = BEHAVIOR_REPORT + "; M7-001, shipped BehaviorID and BehaviorClass tables"
            prompt = f"Leave Freeplay on and watch for the {name} behavior."
            trigger = identity = None
            seconds = 45
            if step:
                prompt, identifier, trigger, seconds, evidence = personality[step]
                identity = names[identifier]
                citation += "; " + evidence
            cube_round = CUBE_FREEPLAY_ROUNDS.get(name)
            if cube_round:
                configured, prompt, evidence, objective, carrying = cube_round
                cube_ids = {names[behavior_id] for behavior_id in configured}
                citation += "; " + evidence
                if objective is not None:
                    citation += "; E2G 0x8B, objective at 0x005BE228–0x005BE308"
                else:
                    citation += "; get_state projects the retained Carrying owner"
            if name == "ReactToCubeMoved":
                citation += "; " + REACTION_REPORT + "; PersonalityHw cube-move, M4-009, 0x00534190–0x005341B0"
                trigger = "CubeMoved"
                prompt = "Move the cube to a different spot within Cozmo’s view, keeping it upright."
            if name in {"AcknowledgeObject", "ReactToCubeMoved"}:
                citation += "; M15-081, stacked-cube look 0x006038DA–0x00603B50 and FinishIteration 0x00603C74"
            candidates = []
            starts = []
            if trigger is not None:
                citation += "; " + REACTION_REPORT
                reaction = triggers[trigger]
                starts.extend((at, "0x88", reaction) for at, body in session.in_window(0x88)
                              if len(body) == 2 and body[1] == reaction != body[0])
            else:
                for at, body in session.in_window(0x87):
                    if len(body) < 7 or body[1] == body[0]:
                        continue
                    if cube_round and body[1] not in cube_ids:
                        continue
                    if identity is not None and body[1] != identity:
                        continue
                    if identity is None and (body[4] != classes[name] or body[1] not in names.values()):
                        continue
                    # Selection's ordinary idle Wait is a separate scope item from Freeplay Wait.
                    if name == "Wait" and not freeplay_running_at(session, at):
                        continue
                    starts.append((at, "0x87", body[1]))
            for at, tag, key in starts:
                end = session.end if cube_round else min(at + seconds * 1000, session.end)
                if tag == "0x87":
                    stopped = (t for t, body in session.in_window(0x87, at, end)
                               if len(body) >= 7 and body[0] == key != body[1])
                else:
                    stopped = (t for t, body in session.in_window(0x88, at, end)
                               if len(body) == 2 and body[0] == key != body[1])
                stopped_at = next(stopped, None)
                end = stopped_at if stopped_at is not None else end
                if cube_round:
                    selected = next(behavior_id for behavior_id in configured if names[behavior_id] == key)
                    reason = cube_round_gap(session, at, stopped_at, objective, carrying)
                    if reason:
                        candidates.append(session.apply_missing(Verdict(
                            SEEN, reason, citation, prompt, (at, end), (name, selected))))
                        continue
                if step in REACTION_EXIT_ROUNDS and stopped_at is None:
                    candidates.append(session.apply_missing(Verdict(
                        SEEN, f"Engine {tag} entry arrived without an exit within {seconds} s.",
                        citation, prompt, (at, end), (name, trigger))))
                    continue
                if name == "ReactToCubeMoved" and not any(session.in_window(0x0F, max(0, at - seconds * 1000), at)):
                    candidates.append(Verdict(SEEN, "Reaction entered without the round’s ObjectMoved report.",
                                              citation, prompt, (at, end), (name,)))
                    continue
                reason = (f"Engine {tag} entry at {at:.0f} ms and exit at {end:.0f} ms."
                          if cube_round or step in REACTION_EXIT_ROUNDS else f"Engine {tag} entry at {at:.0f} ms.")
                aliases = (name, selected) if cube_round else (name, trigger) if step in REACTION_EXIT_ROUNDS else (name,)
                candidates.append(session.apply_missing(Verdict(PASSED, reason, citation, prompt, (at, end), aliases)))
            if name == "ReactToCubeMoved" and not candidates and session.reports[0x0F]:
                candidates.append(Verdict(SEEN, "ObjectMoved arrived without a CubeMoved reaction entry.",
                                          citation, prompt))
            yield group, name, choose(candidates, citation, prompt)


def visit_interesting_edge_check(session, names):
    identifier = "Hiking_VisitInterestingEdge"
    native_id = names[identifier]
    citation = (BEHAVIOR_REPORT + "; M11-079/M11-089/M11-092, hiking.json and Hiking_visitInterestingEdge.json. "
                "Process 1 reads the border sets at 0x00574FCC–0x00575054. Goal gate at 0x005E6520–0x005E6824 "
                "and stop at 0x005E729C–0x005E7302. Terminal branches log at "
                "0x005E7564–0x005E7598 and 0x005E7DC8–0x005E7E0E.")
    prompt = ("Start Freeplay with Cozmo on his treads, off the charger and carrying nothing. "
              "Use a flat surface with a safe contrast boundary and clear room to turn and approach. "
              "Give Hiking time to finish its intro and beacon check. Capture get_map around the attempt.")
    candidates = []
    transitions = [(at, body[0], body[1]) for at, body in session.in_window(0x87)
                   if len(body) >= 7 and body[0] != body[1]]
    for index, (at, _, new) in enumerate(transitions):
        if new != native_id:
            continue
        stopped = next((time for time, previous, _ in transitions[index + 1:] if previous == native_id), None)
        end = stopped if stopped is not None else session.end
        if not freeplay_running_at(session, at):
            status, reason = SEEN, "Hiking_VisitInterestingEdge entered without an active Freeplay request."
        elif stopped is None:
            status, reason = SEEN, "Hiking_VisitInterestingEdge entered without a matching engine exit."
        else:
            terminal = next((branch for time, branch in session.edge_terminal_signals if at <= time <= stopped), None)
            if terminal is None:
                status, reason = SEEN, "Hiking_VisitInterestingEdge exited before a terminal branch was recorded."
            else:
                status = PASSED
                reason = (f"Engine 0x87 entered at {at:.0f} ms, reached the {terminal} branch "
                          f"and exited at {end:.0f} ms.")
        candidates.append(session.apply_missing(Verdict(
            status, reason, citation, prompt, (at, end), ("VisitInterestingEdge", identifier))))
    return choose(candidates, citation, prompt)


def rename_response_check(session, names):
    identifier = "RespondToRenameFace"
    native_id = names[identifier]
    citation = (BEHAVIOR_REPORT + "; M14-011, G2E 0x8C and E2G 0x1D at 0x00657B40–0x00657B82; "
                "M7-019, 0x0060080C–0x00600844 stages the name. M14-006, 0x00600884–0x006008F0 "
                "starts Name_Normal with the authored trigger. 0x00600A50–0x00600A5C waits for the action; " + COMPLETION)
    prompt = ("Teach Cozmo a face, then rename that enrolled ID while he is still in Meet Cozmo. "
              "Let him finish saying the new name before leaving Meet Cozmo.")
    candidates = []
    requests = session.calls["rename-face"]
    for index, call in enumerate(requests):
        end = requests[index + 1].start if index + 1 < len(requests) else session.end
        entered = next((at for at, body in session.in_window(0x87, call.start, end)
                        if len(body) >= 7 and body[0] != body[1] == native_id), None)
        if entered is None:
            candidates.append(Verdict(SEEN, "A rename was requested; the Meet response did not enter.", citation, prompt))
            continue
        stopped = next((at for at, body in session.in_window(0x87, entered, end)
                        if len(body) >= 7 and body[0] == native_id != body[1]), None)
        changed = any(len(body) >= 5 and struct.unpack_from("<i", body)[0] == call.args.get("faceId")
                      and text8(body, 4) == call.args.get("newName")
                      for _, body in session.in_window(0x1D, call.start, min(end, call.end or end)))
        speech = [(at, tag) for at, body in session.in_window(0x5A, entered, stopped or end)
                  if len(body) >= 14 and (tag := struct.unpack_from("<I", body)[0]) > 3_000_000
                  and struct.unpack_from("<iI", body, 4) == (0x20, 0)]
        if changed and stopped is not None and len(speech) == 1:
            status = PASSED
            reason = f"The rename report matched, and the Meet response exited after successful SayText tag {speech[0][1]}."
        else:
            status = SEEN
            reason = "The response entered; a matching rename report, owned SayText success and exit are required."
        candidates.append(session.apply_missing(Verdict(
            status, reason, citation, prompt, (call.start, stopped or end), (identifier, "SayTextAction"))))
    return choose(candidates, citation, prompt)


def cube_round_gap(session, at, stopped_at, objective, carrying):
    if not freeplay_running_at(session, at):
        return "The configured behavior entered without an active Freeplay request."
    if stopped_at is None:
        return "The configured behavior entered without a matching exit."
    if objective is not None:
        found = any(body == struct.pack("<I", objective) for _, body in session.in_window(0x8B, at, stopped_at))
        return None if found else f"The behavior exited without engine objective {objective}."
    states = sorted((call for call in session.calls["get_state"] if call.end is not None
                     and isinstance(call.reply.get("isCarrying"), bool)), key=lambda call: call.end)
    before = next((call for call in reversed(states) if call.end <= at), None)
    after = next((call for call in states if call.start >= stopped_at), None)
    if before is None or after is None or before.reply["isCarrying"] == carrying or after.reply["isCarrying"] != carrying:
        return "The behavior exited without the matching carry-state change in the surrounding get_state reports."
    return None


def freeplay_running_at(session, at):
    calls = sorted((c for c in session.calls["freeplay"] if c.end is not None and c.end < at
                    and c.reply.get("status") == "submitted"), key=lambda c: c.end)
    return bool(calls and calls[-1].args.get("enabled") is True)


def freeplay_return_check(session):
    citation = ("M15-031, M15-058; Unity NeedsHub_2018.cs:376–390,486–490; G2E 0x92 at 0x005A4FA2, "
                "SetCurrentActivity at 0x005A11E8–0x005A122A; Selection Wait at 0x0060AAAE–0x0060AAC8; "
                "activity public-state subscriber at 0x00592B1E and activity log at 0x00592010–0x00592190; " + BEHAVIOR_REPORT + "; " + COMPLETION)
    prompt = ("While Cozmo is active in Freeplay, ask him to move his head or turn in place. Check that he "
              "returns to Freeplay after the command, then ask him to turn Freeplay off.")
    candidates = []
    wait = enum_members("BehaviorID")["Wait"]
    observed = sorted(((call.end, call.reply) for call in session.calls["get_state"] if call.end is not None),
                      key=lambda sample: sample[0])
    for tool, types in ACTION_TYPES.items():
        for call in session.calls[tool]:
            if call.end is None:
                continue
            before = next((state for at, state in reversed(observed) if at < call.start), {})
            was_active = (before.get("freeplay") or {}).get("active", before.get("highLevelActivity") == "Freeplay")
            if not was_active:
                continue
            aliases = ("Freeplay", *ACTION_ALIASES[tool])
            window = (call.start, call.end)
            native = action_check(session, tool, "", citation, 0, (call.end - call.start) / 1000 + 1, (call,))
            if native.status != PASSED:
                candidates.append(session.apply_missing(Verdict(SEEN, native.reason, citation, prompt, window, aliases)))
                continue
            tags = action_tags(call, types, 0)
            completion = next((at for at, body in session.in_window(0x5A, *window)
                               if len(body) >= 14 and (data := struct.unpack_from("<IiI", body))[1] in types
                               and data[2] == 0 and (data[0] in tags if tags else 0 < data[0] <= 1_000_000)), None)
            if completion is None:
                candidates.append(session.apply_missing(Verdict(
                    SEEN, f"{tool} returned successfully; a timestamped engine completion was unavailable.",
                    citation, prompt, window, aliases)))
                continue
            selection = next((at for at, name in session.activity_changes if call.start <= at <= completion and name == "Selection"), None)
            interrupted_wait = any(len(body) >= 7 and body[1] == wait for _, body in session.in_window(0x87, call.start, completion))
            toggled = next((toggle.start for toggle in session.calls["freeplay"] if toggle.start >= call.start), session.end)
            if selection is not None and interrupted_wait:
                resumed = next((at for at, name in session.activity_changes if completion <= at <= toggled and name == "Freeplay"), None)
                if resumed is not None:
                    candidates.append(session.apply_missing(Verdict(
                        PASSED, f"Selection and Wait bracketed {tool}; 0x5A completed at {completion:.0f} ms "
                        f"and Freeplay resumed at {resumed:.0f} ms.", citation, prompt, (call.start, resumed), aliases)))
                    continue
            snapshots = [(call.end, call.reply), *observed]
            previous_resume = (before.get("freeplay") or {}).get("lastResumedAtEngineSeconds")
            resumed = next((at for at, response in snapshots if completion <= at <= toggled
                            and (state := response.get("freeplay") or {}).get("active")
                            and state.get("lastResumedAtEngineSeconds") is not None
                            and (previous_resume is None or state["lastResumedAtEngineSeconds"] > previous_resume)), None)
            paused = interrupted_wait or any(call.start <= at <= completion and (state.get("freeplay") or {}).get("pausedForCommand")
                                            for at, state in observed)
            if paused and resumed is not None:
                candidates.append(session.apply_missing(Verdict(
                    PASSED, f"{tool} completed at {completion:.0f} ms; a newer native Freeplay resume was "
                    f"reported at {resumed:.0f} ms.", citation, prompt, (call.start, resumed), aliases)))
            else:
                candidates.append(session.apply_missing(Verdict(
                    SEEN, f"{tool} completed during the round; the Selection pause and automatic return were not both observed.",
                    citation, prompt, window, aliases)))
    return choose(candidates, citation, prompt)


def freeplay_check(session, command_verdicts, require_resume):
    seconds = int(re.search(r"FreeplaySeconds = (\d+)", source(ROUNDS / "M15CubeHw.cs"))[1])
    citation = ("M15CubeHw.RunFreeplayControl; M15-031, M15-058; "
                "SetCurrentActivity 0x005A11E8–0x005A122A; " + BEHAVIOR_REPORT)
    prompt = ("Ask Cozmo to play on his own, then play one animation and let him return to Freeplay. "
              "Give him a minute on each side, then turn Freeplay off.")
    calls = session.calls["freeplay"]
    if not calls:
        return choose([], citation, prompt)
    samples = []
    for family in session.calls.values():
        for sample in family:
            if sample.end is None:
                continue
            state = sample.reply.get("freeplay")
            if state is None and sample.tool == "get_state" and sample.reply.get("highLevelActivity") == "Freeplay":
                # Older bundles expose SetCurrentActivity through get_state's high-level and child activity names.
                state = {"active": bool(sample.reply.get("activity"))}
            if state is not None:
                samples.append((sample.end, state))
    samples.sort(key=lambda sample: sample[0])
    candidates = []
    for call in calls:
        if call.args.get("enabled") is not True:
            continue
        disabled = next((c for c in calls if c.start > call.start and c.args.get("enabled") is False), None)
        end = disabled.end if disabled and disabled.end is not None else session.end
        before = next(((t, state) for t, state in samples if call.start <= t <= end and state.get("active")), None)
        reason = "Freeplay submission observed without the sourced activity sequence."
        passed = False
        if before and not require_resume:
            passed, reason = True, f"Native Freeplay activity active at {before[0]:.0f} ms."
        if before and require_resume:
            animation = command_verdicts["animation"]
            if animation.status == PASSED:
                start, finish = animation.window
                after = next((state for t, state in samples if finish <= t <= end and state.get("active")
                              and (state.get("lastResumedAtEngineSeconds") or -1) >
                              (before[1].get("lastResumedAtEngineSeconds") or -1)), None)
                off = disabled and any(t >= disabled.start and state.get("enabled") is False
                                       and state.get("active") is False for t, state in samples)
                if before[0] <= start and start - call.start >= seconds * 1000 and after and off and end - finish >= seconds * 1000:
                    passed, reason = True, "An animation completed between the two observed Freeplay windows, then Freeplay turned off."
        candidates.append(session.apply_missing(Verdict(PASSED if passed else SEEN, reason, citation, prompt,
                                                        (call.start, end), ("Freeplay",))))
    if not candidates:
        candidates.append(Verdict(SEEN, "Only a Freeplay off call was recorded.", citation, prompt))
    return choose(candidates, citation, prompt)


def bridge_check(session, command):
    requirements = {
        "connection": (("get_state",), "CozmoBridge.GetState; Cozmo.Mcp startup gate", "Ask Cozmo for his current state."),
        "colour camera": (("get_camera_image",), "COLOR-CAMERA-1; M2-012, R2E 0xF2 at 0x004F1D7A–0x004F1D9C; M11-035, E2G 0x43 at 0x00654A14–0x00654A1C",
                          "Ask for a processed colour camera image."),
        "reaction locks": (("lock-reactions", "unlock-reactions"), "G2E 0x9F/0xA0/0xA1, 0x005A4D2A/0x005A4EA6/0x005A4F20",
                           "Ask Cozmo to temporarily lock cube reactions while doing a task, then unlock them."),
        "wait until accepted": (tuple(ACTION_TYPES), "ActionList::QueueAction 0x0053D94E, GetCurrentReactionTrigger 0x005A19EC",
                                "While Cozmo is reacting, ask him to wait until he can accept the next command."),
    }
    tools, citation, prompt = requirements[command]
    candidates = []
    for tool in tools:
        for call in session.calls[tool]:
            reply = call.reply
            end = call.end if call.end is not None else session.end
            if command == "connection":
                link = reply.get("connection") or {}
                passed = bool(session.startup is not None and end >= session.startup and link.get("ready")
                              and link.get("connected"))
                reason = "MCP returned engine state after the startup gate." if passed else "The startup gate or state report is absent."
            elif command == "colour camera":
                frame = reply.get("robotTimestamp")
                matched = any(len(body) >= 4 and struct.unpack_from("<I", body)[0] == frame
                              for _, body in session.in_window(0x43, 0, end))
                passed = (reply.get("isColor") is True and reply.get("encoding") == 9 and
                          frame in session.wire_image_times and matched)
                reason = ("The colour image and its 0x43 processing report share the MCP image timestamp."
                          if passed else "No matching colour frame and processing report for this MCP reply.")
            elif command == "reaction locks":
                report = reply.get("report") or {}
                identifier = call.args.get("id")
                entries = report.get("reactionLocks", [])
                targets = [set(entry.get("triggers", [])) for entry in entries if entry.get("id") == identifier]
                if tool == "unlock-reactions":
                    if not session.calls["lock-reactions"]:
                        candidates.append(Verdict(SEEN, "Only an unlock request was recorded.", citation, prompt))
                    continue
                unlocked = any(other.start >= end and other.args.get("id") == identifier
                               and (other.reply.get("report") or {}).get("id") == identifier
                               and not any(entry.get("id") == identifier for entry in other.reply["report"].get("reactionLocks", []))
                               for other in session.calls["unlock-reactions"])
                passed = bool(identifier and targets and set(call.args.get("triggers", ())).issubset(targets[0]) and unlocked)
                reason = "Requested lock later removed under the same ID." if passed else "The matching lock and unlock reports are incomplete."
            else:
                if not call.args.get("waitAdmittedMs"):
                    continue
                wait = reply.get("admissionWait") or {}
                # An immediate admission only exercises the ordinary no-wait path.
                passed = bool(reply.get("admitted") and wait.get("waitedMs", 0) > 0 and wait.get("reactions") and not wait.get("timedOut"))
                reason = "Action admitted after retrying within the requested wait." if passed else "The reply does not show a wait followed by admission."
            candidates.append(session.apply_missing(Verdict(PASSED if passed else SEEN, reason, citation, prompt,
                                                            (call.start, end), (tool,))))
    return choose(candidates, citation, prompt)


def evaluate(session):
    command_steps, signals, personality = load_rounds()
    command_verdicts = {}
    for name, (prompt, citation, expected) in command_steps.items():
        if name == "face-setup":
            continue
        tool = "say-text" if name == "sentence" else name
        if name in FACE_TOOLS:
            command_verdicts[name], enrolled = face_operation(session, name, command_steps[name])
            if name == "teach-face":
                taught = enrolled
            continue
        seconds = FACE_ACTION_WINDOW if name in {"track-face", "turn-to-face"} else 65 if name in {"animation", "sentence"} else 45
        if tool == "say-text":
            citation += "; SayText type 0x20 at 0x00560B74, defaultCompleted (6) at 0x0053FF0E–0x0053FF14"
        if tool == "go-to-pose":
            prompt = ("Read the world pose with get_map, choose a clear target about 200 mm ahead, "
                      "ask him to drive there and face left, then read his world pose again.")
            citation += ("; M12-002, M12-024, M12-032, M13-033, M13-034; G2E handler 0x0052A11C–0x0052A20C; "
                         "Ready arrival branch at 0x0055ACA2–0x0055B0A6")
        command_verdicts[tool] = action_check(session, tool, prompt, citation, expected, seconds)
    for tool, (prompt, citation, seconds) in SKILLS.items():
        if tool in {"roll", "stack", "mount-charger"}:
            citation += "; M13-033, M13-034; pre-dock planner starts at 0x0064ACC4, recheck at 0x00559A86–0x00559AE4"
        command_verdicts[tool] = action_check(session, tool, prompt, citation, 0, seconds)
    for name, step in signals.items():
        command_verdicts[name] = signal_check(session, name, step, taught)
    # RunCharger waits 30 s for a confirmed, current-origin charger observation.
    command_verdicts["charger-seen"] = signal_check(session, "charger-seen",
        ("Show Cozmo the charger marker in good light.", 0x44, "RobotObservedObject for the charger",
         "M15CubeHw.RunCharger; E2G 0x44 at 0x007131E2, M11-038", 30))
    for name in ("connection", "colour camera", "reaction locks", "wait until accepted"):
        command_verdicts[name] = bridge_check(session, name)
    command_verdicts["freeplay"] = freeplay_return_check(session)
    if command_verdicts["freeplay"].status != PASSED:
        conformance_round = freeplay_check(session, command_verdicts, True)
        if conformance_round.status == PASSED or command_verdicts["freeplay"].status == NOT_EXERCISED:
            command_verdicts["freeplay"] = conformance_round
    scope_a = {
        "Head, lift, drive straight, turn in place": "head,lift,drive-straight,turn-in-place",
        "Turn to a face, turn to a cube, track a face": "turn-to-face,turn-to-cube,track-face",
        "Play an authored animation": "animation",
        "Say a sentence in his voice": "say-text",
        "Pick up a cube, put it down": "pickup,put-down",
        "Events": "face-seen,face-recognised,face-lost,cube-seen,cube-moved,cube-tapped,object-pose-lost",
        "Teach, rename and erase a face": "teach-face,rename-face,erase-face",
        "Go to a place": "go-to-pose",
        "Return to and mount the charger": "mount-charger",
        "Roll a cube": "roll",
        "Stack a cube": "stack",
        "Charger seen report": "charger-seen",
        "Freeplay on/off and returning after a command": "freeplay",
        "The connection for an LLM": "connection",
        "Colour camera": "colour camera",
        "Reaction locks and wait until accepted": "reaction locks,wait until accepted",
    }
    for item, names in scope_a.items():
        for name in names.split(","):
            yield "A / " + item, name, command_verdicts[name]
    yield "B / Freeplay loop, activities, needs, mood", "Freeplay loop", freeplay_check(session, command_verdicts, False)
    for group, name, verdict in behavior_checks(session, personality):
        yield "B / " + group, name, verdict


def markdown_cell(value):
    return " ".join(str(value).split()).replace("|", "\\|").replace("<", "&lt;").replace(">", "&gt;")


def render(session, entries):
    rows = [
        f"# Session check for {session.path.name}",
        "",
        f"Capture status {session.result.get('status', 'unknown')}. "
        f"{session.frame_count:,} frame rows and {sum(session.wire_counts.values()):,} decoded wire messages. "
        "Passes are evidence for review against SCOPE.md. Timing windows come from the hardware rounds.",
        "",
    ]
    if session.raw_tail:
        rows.extend(("The decoded message stream ends before the raw frame capture. The colour check also reads the raw frame tail.", ""))
    scope_a = defaultdict(Counter)
    for group, _, verdict in entries:
        if group.startswith("A / "):
            scope_a[group[4:]][verdict.status] += 1
    rows.extend(("## Scope A summary", "", "| Scope item | Verdict | Checks passed |", "| --- | --- | --- |"))
    for group, counts in scope_a.items():
        total = counts.total()
        status = PASSED if counts[PASSED] == total else NOT_EXERCISED if counts[NOT_EXERCISED] == total else SEEN
        rows.append(f"| {group} | {status} | {counts[PASSED]}/{total} |")
    rows.extend(("", "## Scope A checks and Scope B classes", "",
        "| Scope item | Check | Verdict | Evidence or reason | Source |",
        "| --- | --- | --- | --- | --- |"))
    for group, name, verdict in entries:
        markers = ", ".join(f"M{n}" for n in verdict.missing)
        reason = verdict.reason + (f" MISSING markers {markers}." if markers else "")
        if verdict.other_missing:
            reason += " Other attempts hit " + ", ".join(f"M{n}" for n in verdict.other_missing) + "."
        rows.append("| " + " | ".join(map(markdown_cell, (group, name, verdict.status, reason, verdict.citation))) + " |")
    rows.extend(("", "## Try these next session", ""))
    prompts = []
    groups = set()
    for group, _, verdict in entries:
        if verdict.status == NOT_EXERCISED and group not in groups:
            prompts.append(verdict.prompt)
            groups.add(group)
    rows.extend("- " + prompt for prompt in dict.fromkeys(prompts))
    if not prompts:
        rows.append("All scope groups were exercised at least once.")
    if sum(v.status == NOT_EXERCISED for _, _, v in entries) > len(prompts):
        rows.append("\nThe table lists the remaining checks within each group.")
    rows.extend(("", "## MISSING markers", ""))
    by_marker = defaultdict(list)
    for _, name, verdict in entries:
        for marker in verdict.missing + verdict.other_missing:
            by_marker[marker].append(name)
    if not session.missing:
        rows.append("No unresolved MISSING lines in stack-log.txt.")
    lines = defaultdict(list)
    for marker, at, line in session.missing:
        # Keep every line number and compress repeated messages from the same path.
        owner = ", ".join(by_marker[marker]) or (
            "startup" if at is not None and session.startup is not None and at < session.startup else "unattributed")
        lines[line, owner].append(marker)
    for (line, owner), markers in lines.items():
        rows.append(f"- {', '.join(f'M{i}' for i in markers)} ({owner}) — {markdown_cell(line)}")
    if session.resolved_markers:
        rows.extend(("", "## Resolved markers in the original capture", ""))
        for index, line, resolution in session.resolved_markers:
            rows.append(f"- M{index} ({resolution}) — {markdown_cell(line)}")
    return "\n".join(rows) + "\n"


def main():
    parser = argparse.ArgumentParser(description="Check a recorded Cozmo MCP session against the sourced hardware rounds.")
    parser.add_argument("bundle", type=Path)
    parser.add_argument("--output", "-o", type=Path, help="Write Markdown to this file.")
    args = parser.parse_args()
    try:
        session = Session(args.bundle)
        report = render(session, list(evaluate(session)))
        if args.output:
            args.output.write_text(report, encoding="utf-8")
        else:
            print(report, end="")
    except (OSError, ValueError, KeyError) as error:
        parser.exit(1, f"session_check: {error}\n")


if __name__ == "__main__":
    main()
