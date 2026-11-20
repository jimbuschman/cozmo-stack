"""Offline tests for the session checker. Run with python -m unittest discover -s re-analysis/tools -p test_session_check.py."""

import base64
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import struct
import tempfile
import unittest

from session_check import NOT_EXERCISED, PASSED, SEEN, Session, enum_members, evaluate, render


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
        self.completion(114, 7, 0x1F)
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
