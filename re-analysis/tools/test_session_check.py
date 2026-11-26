"""Offline tests for the session checker. Run with python -m unittest discover -s re-analysis/tools -p test_session_check.py."""

import base64
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import struct
import tempfile
import unittest

from session_check import NOT_EXERCISED, PASSED, SEEN, Session, action_check, enum_members, evaluate, render


class SessionCheckTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.bundle = Path(directory.name)
        self.events = []
        self.epoch = datetime(2026, 11, 20, tzinfo=timezone.utc)
        (self.bundle / "result.json").write_text('{"testId":"LLM-SESSION","status":"finished"}', encoding="utf-8")
        (self.bundle / "frames.jsonl").write_text("", encoding="utf-8")
        (self.bundle / "messages.jsonl").write_text("", encoding="utf-8")

    def event(self, at, kind, **fields):
        self.events.append(dict(fields, kind=kind, atMs=at,
                                timestampUtc=(self.epoch + timedelta(milliseconds=at)).isoformat()))

    def raw(self, at, tag, body):
        self.event(at, "engine-to-game", tag=f"0x{tag:02X}", bodyHex=body.hex())

    def request(self, at, identity, tool, **args):
        self.event(at, "mcp-input", id=identity, method="tools/call", params=dict(name=tool, arguments=args))

    def reply(self, at, identity, **payload):
        self.event(at, "mcp-output", id=identity, result={"structuredContent": payload})

    def completion(self, at, tag, action_type, result=0):
        self.raw(at, 0x5A, struct.pack("<IiIBB", tag, action_type, result, 0, 6))

    def check(self, missing=()):
        for source_name, lines in (("events.jsonl", [json.dumps(e) for e in sorted(self.events, key=lambda e: e["atMs"])]),
                                   ("stack-log.txt", list(missing))):
            (self.bundle / source_name).write_text("".join(line + "\n" for line in lines), encoding="utf-8")
        session = Session(self.bundle)
        entries = list(evaluate(session))
        return {name: verdict for _, name, verdict in entries}, render(session, entries)

    def test_submitted_tracking_requires_exact_later_cancel_and_variant_three(self):
        self.request(100, 1, "track-face", faceId=-10)
        self.reply(105, 1, status="submitted", admitted=True,
                   report={"actionTag": 7, "requestedFaceId": -10})
        self.request(10200, 2, "cancel-action", actionTag=8)
        self.reply(10201, 2, status="submitted", report={"actionTag": 8})
        self.raw(10250, 0x5A, struct.pack("<IiIBBi", 7, 0x23, 0x02000000, 0, 3, 1))
        self.check()
        def verdict():
            return action_check(Session(self.bundle), "track-face", "", "", 0x02000000, 20)
        self.assertEqual(SEEN, verdict().status)
        self.request(101, 3, "cancel-action", actionTag=7)
        self.reply(102, 3, status="submitted", report={"actionTag": 7})
        self.check()
        self.assertEqual(SEEN, verdict().status)
        self.request(10210, 4, "cancel-action", actionTag=7)
        self.reply(10211, 4, status="submitted", report={"actionTag": 7})
        self.check()
        self.assertEqual(PASSED, verdict().status)

    def test_stacked_ghost_missing_downgrades_acknowledge_and_cube_moved(self):
        ids, classes = enum_members("BehaviorID"), enum_members("BehaviorClass")
        cube_moved = enum_members("ReactionTrigger")["CubeMoved"]
        self.raw(90, 0x0F, bytes(4))
        self.raw(100, 0x88, bytes((0, cube_moved)))
        self.raw(110, 0x87, bytes((ids["Wait"], ids["AcknowledgeObject"],
                                  classes["Wait"], 12, classes["AcknowledgeObject"], 12, 0)))
        self.raw(180, 0x87, bytes((ids["AcknowledgeObject"], ids["Wait"],
                                  classes["AcknowledgeObject"], 12, classes["Wait"], 12, 0)))
        self.raw(190, 0x88, bytes((cube_moved, 0)))
        marker = ("MISSING: M15-081 M11-010/M12-033/M13-026 stacked-cube look unavailable: "
                  "M11-010 ghost visibility reason")
        checks, _ = self.check()
        for name in ("AcknowledgeObject", "ReactToCubeMoved"):
            self.assertEqual(PASSED, checks[name].status)
        self.event(150, "stack-log", line=marker)
        checks, _ = self.check([marker])
        for name in ("AcknowledgeObject", "ReactToCubeMoved"):
            self.assertEqual(SEEN, checks[name].status)
            self.assertTrue(checks[name].blocked_by_missing)

    def test_cube_freeplay_rounds_use_the_native_transition_and_result(self):
        ids, classes = enum_members("BehaviorID"), enum_members("BehaviorClass")
        self.request(10, 1, "freeplay", enabled=True)
        self.reply(20, 1, status="submitted")
        cases = [
            ("PickUpCube", "SparksPickupSingleCubeForPyramid", None, True),
            ("PutDownBlock", "PutDownBlockNothingToDo", None, False),
            ("StackBlocks", "StackBlocks", 40, None),
            ("DriveOffCharger", "Hiking_DriveOffCharger", 4, None),
        ]

        def behavior(at, old_id, new_id, old_cls, new_cls):
            self.raw(at, 0x87, bytes((ids[old_id], ids[new_id], classes[old_cls], 12, classes[new_cls], 12, 0)))

        for i, (cls, name, objective, carrying) in enumerate(cases, 1):
            at = i * 200
            behavior(at, "Wait", name, "Wait", cls)
            behavior(at + 50, name, "Wait", cls, "Wait")
        checks, _ = self.check()
        for cls, *_ in cases:
            self.assertEqual(SEEN, checks[cls].status, cls)

        for i, (cls, name, objective, carrying) in enumerate(cases, 1):
            at = i * 200
            if objective is not None:
                self.raw(at + 30, 0x8B, struct.pack("<I", objective))
            else:
                self.request(at - 40, at, "get_state")
                self.reply(at - 30, at, isCarrying=not carrying)
                self.request(at + 60, at + 60, "get_state")
                self.reply(at + 65, at + 60, isCarrying=carrying)
        checks, _ = self.check()
        for cls, *_ in cases:
            self.assertEqual(PASSED, checks[cls].status, cls)

    def test_interesting_edge_round_uses_the_configured_engine_entry_and_exit(self):
        ids, classes = enum_members("BehaviorID"), enum_members("BehaviorClass")
        wait, edge = ids["Wait"], ids["Hiking_VisitInterestingEdge"]
        wait_class, edge_class = classes["Wait"], classes["VisitInterestingEdge"]
        self.request(10, 1, "freeplay", enabled=True)
        self.reply(20, 1, status="submitted")
        self.request(30, 2, "get_map")
        self.reply(40, 2, available=True, regions=[{"type": "InterestingEdge"}])
        checks, _ = self.check()
        self.assertEqual(NOT_EXERCISED, checks["VisitInterestingEdge"].status)

        self.raw(100, 0x87, bytes((wait, edge, wait_class, 12, edge_class, 12, 0)))
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["VisitInterestingEdge"].status)

        self.raw(150, 0x87, bytes((edge, wait, edge_class, 12, wait_class, 12, 0)))
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["VisitInterestingEdge"].status)

        self.event(130, "engine-log", name="Hiking_VisitInterestingEdge.S3")
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["VisitInterestingEdge"].status)
        self.assertIn("0x005E729C", checks["VisitInterestingEdge"].citation)

        self.event(120, "engine-log", line="MISSING: M11-092 VisitInterestingEdge test gap")
        checks, _ = self.check(["MISSING: M11-092 VisitInterestingEdge test gap"])
        self.assertEqual(SEEN, checks["VisitInterestingEdge"].status)

    def test_freeplay_return_uses_the_head_completion_and_automatic_activity_bracket(self):
        ids, classes = enum_members("BehaviorID"), enum_members("BehaviorClass")
        self.request(10, 1, "get_state")
        self.reply(20, 1, highLevelActivity="Freeplay", activity="Socialize")
        self.request(30, 2, "head", radians=-.15)
        self.event(35, "engine-log", name="RobotAudioClient.UpdateActivityMusicState.NoFreeplayMusic",
                   text="No freeplay music for ActivityName 'Selection'")
        self.raw(40, 0x87, bytes((ids["InteractWithFaces"], ids["Wait"], classes["InteractWithFaces"], 12,
                                  classes["Wait"], 11, 0)))
        self.completion(90, 7, 0x12)
        self.reply(95, 2, engineResult=0, admitted=True)
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["freeplay"].status)

        self.event(92, "engine-log", name="RobotAudioClient.UpdateActivityMusicState.NoFreeplayMusic",
                   text="No freeplay music for ActivityName 'Freeplay'")
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["freeplay"].status)

        self.request(91, 3, "freeplay", enabled=True)
        self.reply(94, 3, status="submitted")
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["freeplay"].status)

    def test_rename_response_needs_the_identity_report_and_owned_speech(self):
        ids, classes = enum_members("BehaviorID"), enum_members("BehaviorClass")
        rename, wait = ids["RespondToRenameFace"], ids["Wait"]
        rc, wc = classes["RespondToRenameFace"], classes["Wait"]
        self.request(30, 1, "rename-face", faceId=7, oldName="Drawn", newName="Ada")
        self.raw(35, 0x1D, struct.pack("<i", 7) + b"\x03Ada")
        self.reply(40, 1, faceOperation={"outcome": "renamed", "faceId": 7, "name": "Ada"})
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["RespondToRenameFace"].status)

        self.raw(50, 0x87, bytes((wait, rename, wc, 11, rc, 12, 0)))
        self.completion(70, 42, 0x20)
        self.raw(90, 0x87, bytes((rename, wait, rc, 12, wc, 11, 0)))
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["RespondToRenameFace"].status)

        self.completion(80, 3000101, 0x20)
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["RespondToRenameFace"].status)

    def test_physical_rounds_require_both_native_reaction_edges(self):
        triggers = enum_members("ReactionTrigger")
        pairs = {
            "ReactToRobotOnFace": "RobotOnFace",
            "ReactToRobotOnSide": "RobotOnSide",
            "ReactToRobotShaken": "RobotShaken",
            "ReactToPlacedOnSlope": "RobotPlacedOnSlope",
            "ReactToFrustration": "Frustration",
        }
        native_none = 0x16  # ReactionTransitionReport.cs and E2G 0x88 at 0x005A2EE8.
        for i, reaction in enumerate(pairs.values(), 1):
            self.raw(100 * i, 0x88, bytes((native_none, triggers[reaction])))
        checks, _ = self.check()
        for behavior in pairs:
            self.assertEqual(SEEN, checks[behavior].status, behavior)

        for i, reaction in enumerate(pairs.values(), 1):
            self.raw(100 * i + 50, 0x88, bytes((triggers[reaction], native_none)))
        checks, _ = self.check()
        for behavior in pairs:
            self.assertEqual(PASSED, checks[behavior].status, behavior)
            self.assertIn("0x88", checks[behavior].citation)

    def test_command_needs_matching_type_and_result_within_its_own_window(self):
        self.request(100, 1, "head")
        self.completion(110, 7, 0x13)
        self.reply(120, 1, engineResult=0, admitted=True)
        self.completion(130, 8, 0x12)
        self.request(200, "1", "lift")
        self.completion(210, 9, 0x13)
        self.reply(220, "1", engineResult=0, admitted=True)
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["head"].status)
        self.assertEqual(PASSED, checks["lift"].status)
        self.assertEqual(NOT_EXERCISED, checks["roll"].status)

    def test_roll_completion_has_the_proxied_child_type_and_external_tag(self):
        self.request(100, 1, "roll", objectId=4)
        self.completion(110, 7, -2)
        self.completion(112, 3_000_040, 0x1F)
        self.reply(120, 1, engineResult=0, admitted=True)
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["roll"].status)
        self.raw(114, 0x5A, struct.pack("<IiIBB5IBB", 7, 0x1F, 0, 0, 0, 4, 0, 0, 0, 0, 1, 0))
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["roll"].status)

    def test_two_unattributed_overlapping_calls_cannot_share_completion(self):
        self.request(100, 1, "head")
        self.request(105, 2, "head")
        self.completion(110, 7, 0x12)
        self.reply(120, 1, engineResult=0, admitted=True)
        self.reply(125, 2, engineResult=0, admitted=True)
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["head"].status)

    def test_path_missing_prevents_pass_and_unrelated_marker_stays_visible(self):
        related = "MISSING: M4-016 SetHeadAngleAction readiness"
        unrelated = "MISSING: FactoryExample"
        self.event(10, "stack-log", line=unrelated)
        self.event(20, "startup-gate")
        self.request(100, 1, "head")
        self.event(105, "stack-log", line=related)
        self.completion(110, 7, 0x12)
        self.reply(120, 1, engineResult=0, admitted=True)
        checks, report = self.check([unrelated, related])
        self.assertEqual(SEEN, checks["head"].status)
        self.assertEqual([2], checks["head"].missing)
        self.assertIn("M1 (startup)", report)
        self.assertIn("M2 (head)", report)

    def test_resolved_queue_and_ground_markers_keep_capture_lines_without_blocking(self):
        running = ("MISSING: M8-015 Queue.Update RUNNING VizManager.SetText0x0053F65C and "
                   "CozmoContext.SetSdkStatus(type1,name)0x0053F6F2 recipients are not bound; "
                   "normal queue path is not classified SDK-only")
        terminal = ("MISSING: M8-015 Queue.Update terminal VizManager.SetText(empty)0x0053F696 / "
                    "CozmoContext.SetSdkStatus(type1,empty)0x0053F6B4 recipients before Delete are not bound")
        ground = ("MISSING: M1-034 M12-052 Ground completion 0x00554AE0..00554AFA: four uninitialized "
                  "objectInteractionCompleted ID slots; full raw/API payload unavailable, defined saved-carry/count1/false "
                  "and scalar result retained; PlaceRel normalization policy does not apply")
        other_queue_gap = "MISSING: M8-015 QueueNow Debug0x0053E2A6: native action names not supplied"
        self.request(100, 1, "head")
        self.completion(108, 1, 0x12)
        self.reply(110, 1, engineResult=0, admitted=True)
        self.request(200, 2, "put-down")
        self.reply(220, 2, engineResult=0, admitted=True, during={"events": [{
            "type": "action-completed",
            "payload": {"completion": {"tag": 3_000_384, "actionType": 0x18, "result": 0}},
        }]})
        self.request(300, 3, "lift")
        self.completion(308, 3, 0x13)
        self.reply(310, 3, engineResult=0, admitted=True)
        lines = (running, terminal, ground, other_queue_gap)
        for at, line in zip((102, 105, 204, 305), lines):
            self.event(at, "stack-log", line=line)

        checks, report = self.check(lines)
        self.assertEqual(PASSED, checks["head"].status)
        self.assertEqual(PASSED, checks["put-down"].status)
        self.assertEqual(SEEN, checks["lift"].status)
        self.assertEqual([4], checks["lift"].missing)
        self.assertIn("M4 (lift)", report)
        for marker in ("M1 (M8-015", "M2 (M8-015", "M3 (M12-052"):
            self.assertIn(marker, report)

    def test_planner_diagnostics_resolve_without_masking_other_planner_gaps(self):
        lines = (
            "MISSING: M13-033 stdout: FaceAndApproachPlanner: deltaTheta of -2.498092 above threshold, doing backup!",
            "MISSING: M13-033 LatticePlanner.PlanMismatch sError stream 0x005003D0 and its error side effects remain unported",
            "MISSING: M13-033 PrintPlan(total) 0x0050048E -> 0x00855E7C output remains unbuilt",
            "MISSING: M13-033 PrintPlan(new) 0x005004D8 -> 0x00855E7C output remains unbuilt",
            "MISSING: M13-033 RobotPositionError event/format at 0x004FFDCC..0x004FFE2A remains unported",
            "MISSING: M13-033 lattice asset/context input is absent",
        )
        self.request(100, 1, "go-to-pose")
        self.completion(108, 1, 12)
        self.reply(110, 1, engineResult=0, admitted=True)
        for at, line in enumerate(lines, 101):
            self.event(at, "stack-log", line=line)
        checks, report = self.check(lines)
        self.assertEqual(SEEN, checks["go-to-pose"].status)
        self.assertEqual([6], checks["go-to-pose"].missing)
        for index in range(1, 6):
            self.assertIn(f"- M{index} (0x", report)

    def test_play_area_telemetry_resolves_without_masking_map_producer_gaps(self):
        lines = (
            "MISSING: M11-045 robot.play_area_size at 0x00624F32: tree does not yet contain all map content (B-M3M4-20); event value unavailable",
            "MISSING: M11-045 explored-area virtual +0x1C (0x006816E8, MemoryMap+0x40): tree does not yet contain all map content (B-M3M4-20)",
            "MISSING: M11-074 ObstacleUnrecognized (type 7) producer UNKNOWN",
        )
        for at, line in enumerate(lines, 101):
            self.event(at, "stack-log", line=line)
        _, report = self.check(lines)
        self.assertIn("0x00624F32..0x00624F4E now emits", report)
        self.assertIn("0x006816E8 returns the cached double", report)
        self.assertIn("ObstacleUnrecognized (type 7) producer UNKNOWN", report)

    def test_shared_action_logging_resolves_while_a_carry_pose_gap_stays_visible(self):
        lines = (
            "MISSING: M8-015 ActionCompleted full ActionResult EnumToString recipient0x005405CA missing for0x03000005",
            "MISSING: M8-017 sequential failure full EnumToString result at 0x0054F8C4 is not recovered for 0x03000001",
            "MISSING: M8-021 trigger DAS blacklist/event diagnostic coverage remains incomplete; concrete CA/CB/D5/5F callbacks, selected completion name and retained playback clip are independent inputs",
            "MISSING: M11-066 shared main pose still receives the existing history raw-pose candidate; complete native raw-pose writer/input binding is not built here",
        )
        self.request(100, 1, "head")
        self.completion(108, 1, 0x12)
        self.reply(110, 1, engineResult=0, admitted=True)
        for at, line in enumerate(lines, 101):
            self.event(at, "stack-log", line=line)
        checks, report = self.check(lines)
        self.assertEqual(PASSED, checks["head"].status)
        self.assertIn("- M1 (0x005405CA", report)
        self.assertIn("- M2 (0x005405CA", report)
        self.assertIn("- M3 (0x005445BC", report)
        self.assertIn("- M4 (unattributed)", report)

    def test_recognition_requires_the_taught_identity_name_and_window(self):
        self.request(100, 1, "teach-face", name="Guest")
        self.raw(200, 0x56, struct.pack("<BiB", 0, 21, 5) + b"Guest")
        self.reply(210, 1, engineResult=0, faceOperation={"outcome": "enrolled", "faceId": 21, "name": "Guest"})
        for at, identity in ((300, 20), (400, 21), (20400, 21)):
            self.raw(at, 0x46, struct.pack("<iI", identity, at) + bytes(48) + b"\x05Guest" + bytes(37))
            payload = {"faceId": identity, "name": "Guest"}
            event = {"cursor": at, "type": "face-recognised", "payload": payload, "robotTimestamp": at,
                     "timestampUtc": (self.epoch + timedelta(milliseconds=at)).isoformat()}
            self.event(at + 10, "mcp-output", method="notifications/claude/channel",
                       params={"content": json.dumps({"events": [event]})})
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["teach-face"].status)
        self.assertEqual(PASSED, checks["face-recognised"].status)
        self.assertIn("400 ms", checks["face-recognised"].reason)
        self.events = [row for row in self.events if row["atMs"] != 400]
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["face-recognised"].status)

    def test_named_state_requires_matching_fresh_visual_timestamp(self):
        self.request(100, 1, "teach-face", name="Guest")
        self.raw(200, 0x56, struct.pack("<BiB", 0, 21, 5) + b"Guest")
        self.reply(210, 1, engineResult=0, faceOperation={"outcome": "enrolled", "faceId": 21, "name": "Guest"})
        self.raw(400, 0x46, struct.pack("<iI", 21, 301) + bytes(48) + b"\x05Guest" + bytes(37))
        self.request(450, 2, "get_state")
        self.reply(460, 2, knownFaces=[{"faceId": 21, "name": "Guest", "lastObservedTimestamp": None, "enrolled": True}])
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["face-recognised"].status)

        self.request(590, 3, "get_state")
        self.reply(600, 3, knownFaces=[{"faceId": 21, "name": "Guest", "lastObservedTimestamp": 301, "enrolled": True}])
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["face-recognised"].status)
        self.assertIn("fresh get_state", checks["face-recognised"].reason)

    def test_reaction_requires_entering_the_expected_trigger(self):
        trigger = enum_members("ReactionTrigger")["UnexpectedMovement"]
        self.raw(250, 0x88, bytes((trigger, trigger)))
        self.raw(270, 0x88, bytes((trigger, 0x16)))
        checks, _ = self.check()
        self.assertEqual(NOT_EXERCISED, checks["ReactToUnexpectedMovement"].status)
        self.raw(300, 0x88, bytes((0x16, trigger)))
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["ReactToUnexpectedMovement"].status)

    def test_colour_processing_uses_raw_tail_after_decoded_messages_end(self):
        self.request(90, 1, "get_camera_image")
        self.raw(105, 0x43, struct.pack("<IBB", 125, 0, 0))
        self.reply(110, 1, isColor=True, encoding=9, robotTimestamp=125)
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["colour camera"].status)

        header = b"COZ\x03RE\x01" + bytes([8]) + bytes(6)
        first = bytes([0xF2]) + struct.pack("<II", 0, 51) + bytes(4) + bytes([8]) + bytes(5) + bytes((1, 0, 1))
        final = bytearray(first)
        struct.pack_into("<I", final, 1, 125)
        final[16] = 1
        packet = header + b"".join(struct.pack("<BH", 5, len(chunk)) + chunk for chunk in (first, final))
        path = self.bundle / "frames.jsonl"
        path.write_text(json.dumps({"outbound": False, "atMs": 100,
                                    "rawBase64": base64.b64encode(packet).decode()}) + "\n", encoding="utf-8")
        checks, report = self.check()
        self.assertEqual(PASSED, checks["colour camera"].status)
        self.assertIn("reads the raw frame tail", report)

    def test_stack_tracks_warning_text_does_not_hide_the_successful_root_result(self):
        self.request(100, 1, "stack", objectId=3)
        message = ("MISSING: M8-007/M8-015 TracksLockedBecause Warning0x00540530: MovementComponent.WhoIsLocking "
                   "native string recipient0x00540506 is not bound; no invented owner-description text")
        self.event(120, "engine-log", line=message)
        self.completion(150, 15, 0x19)
        self.reply(155, 1, admitted=True, engineResult=0)
        checks, report = self.check([message])
        self.assertEqual(PASSED, checks["stack"].status)
        self.assertIn("0x00540506 supplies warning text", report)

        other = "MISSING: M13-034 Native path profile unavailable"
        self.event(130, "engine-log", line=other)
        checks, _ = self.check([message, other])
        self.assertEqual(SEEN, checks["stack"].status)

    def test_replayed_short_planner_markers_remain_in_the_historical_notes(self):
        self.request(100, 1, "stack", objectId=3)
        profile = ("MISSING: M13-034 ApplyMotionProfile two-pass length/end-pose/split calls require "
                   "source-backed M13-011 PathSegment helpers (0x0085BC00..0x0085C034); only checked profile-field "
                   "assignment built, continuity/deceleration/splitting remain unbuilt")
        safety = ("MISSING: M13-034 slot 0x18 -> 0x1C PathIsSafe body needs separately-owned PathSegment "
                  "geometry helpers; candidate short path safety is not claimed source-backed")
        self.event(120, "engine-log", line=profile)
        self.event(130, "engine-log", line=safety)
        self.completion(150, 15, 0x19)
        self.reply(155, 1, admitted=True, engineResult=0)
        checks, report = self.check([profile, safety])
        self.assertEqual(PASSED, checks["stack"].status)
        self.assertIn("SHORT-PLANNER-1 binds the two-pass profile", report)
        self.assertIn("this marker records the older build", report)

    def test_turn_to_cube_requires_the_matching_zero_filled_wire_body(self):
        self.request(100, 1, "turn-to-cube", objectId=1)
        message = ("MISSING: M1-034/M8-015 TurnTowardsObjectAction.GetCompletionUnion 0x0054B020–0x0054B036: "
                   "native interaction words are undefined; retain defined metadata only.")
        self.event(130, "engine-log", line=message)
        self.reply(140, 1, admitted=True, engineResult=0)
        checks, _ = self.check([message])
        self.assertEqual(SEEN, checks["turn-to-cube"].status)
        self.assertIn("0 attributable 0x5A", checks["turn-to-cube"].reason)
        self.raw(135, 0x5A, struct.pack("<IiIBB5IBB", 9, 0x2C, 0, 0, 0, 2, 0, 0, 0, 0, 1, 0))
        checks, _ = self.check([message])
        self.assertEqual(SEEN, checks["turn-to-cube"].status)
        self.raw(136, 0x5A, struct.pack("<IiIBB5IBB", 9, 0x2C, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0))
        checks, _ = self.check([message])
        self.assertEqual(PASSED, checks["turn-to-cube"].status)

    def test_say_text_checks_default_completed_after_the_subaction_results(self):
        self.request(100, 1, "say-text", text="Hello")
        self.reply(150, 1, admitted=True, engineResult=0)
        body = struct.pack("<IiIBI", 7, 0x20, 0, 1, 0)
        self.raw(140, 0x5A, body + bytes((0,)))
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["say-text"].status)
        self.assertIn("defaultCompleted (6)", checks["say-text"].reason)

        self.events.pop()
        self.raw(140, 0x5A, body + bytes((6,)))
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["say-text"].status)

    def test_rename_requires_requested_id_and_name_in_the_exact_rename_report(self):
        self.request(100, 1, "rename-face", faceId=7, oldName="Drawn", newName="Ada")
        for tag, face, name, tail in ((0x1D, 8, "Ada", b""), (0x1D, 7, "Other", b""),
                                      (0x57, 7, "Ada", b""), (0x1D, 7, "Ada", b"x"),
                                      (0x1D, 7, "Ada", b"")):
            self.raw(140, tag, struct.pack("<iB", face, len(name)) + name.encode() + tail)
            self.reply(150, 1, faceOperation={"outcome": "renamed", "faceId": face, "name": name})
            checks, _ = self.check()
            expected = PASSED if (tag, face, name, tail) == (0x1D, 7, "Ada", b"") else SEEN
            self.assertEqual(expected, checks["rename-face"].status)
            del self.events[-2:]

    def test_erase_requires_the_requested_id_and_empty_native_report_name(self):
        self.request(100, 1, "erase-face", faceId=7)
        self.reply(150, 1, faceOperation={"outcome": "erased", "faceId": 7, "lastKnownName": "Ada"})
        self.raw(140, 0x57, struct.pack("<iB", 7, 3) + b"Ada")
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["erase-face"].status)

        self.events.pop()
        self.raw(140, 0x57, struct.pack("<iB", 8, 0))
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["erase-face"].status)

        self.raw(141, 0x57, struct.pack("<iB", 7, 0))
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["erase-face"].status)

    def test_mount_needs_contacts_and_the_requested_charger_sighting(self):
        self.request(100, 1, "mount-charger", chargerId=4)
        self.completion(200, 7, -2)
        self.reply(210, 1, engineResult=0, admitted=True)
        checks, _ = self.check()
        self.assertEqual(SEEN, checks["mount-charger"].status)
        self.raw(90, 0x44, struct.pack("<IIII", 15, 4, 13, 4) + bytes(53))
        self.raw(190, 0x39, bytes([1]))
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["mount-charger"].status)

    def test_ready_wait_checks_the_real_mcp_admission_fields(self):
        self.request(100, 1, "head", waitAdmittedMs=8000)
        self.reply(500, 1, engineResult=0, admitted=True,
                   admissionWait={"waitedMs": 300, "reactions": ["PetInitialDetection"], "timedOut": False})
        checks, _ = self.check()
        self.assertEqual(PASSED, checks["wait until accepted"].status)
        self.assertEqual(SEEN, checks["head"].status)


if __name__ == "__main__":
    unittest.main()
