# Codex marker key carrier, face lifecycle closure, album file request, tracker identity, object turn: Claude check (manager, 2026-11-10)

Merged from `codex-m13-manager-2` at 4100cf6c. Codex's gate on 524f8f96 passed 8290/8290.

**The merge:** Codex rebased its branch onto 87d3fb97, replaying the ten jobs already merged in 970f80c5 and adding
five more. Main's `cozmo-stack` was identical to the old tip 0d6e86c0, so the code conflicts were resolved to Codex's
side and `cozmo-stack` now equals 4100cf6c. QUEUE.md kept both logs.

## M11-050-MARKER-KEY-CARRIER-1: PASS; M11-050 → EXACT_SOURCE

**UpdateVisionMarkers, re-disassembled:**

| Step | Address | Behaviour |
| --- | --- | --- |
| ComputeAndInsertStateAt | 0x00654D88..0x00654D92 | Takes the key out-parameter at sp+0xB4; 0 continues to 0x00654E50 |
| IsPoseInWorldOrigin | 0x00654E56 | False goes to 0x00655028 |
| WasRotatingTooFast | 0x00654E60..0x00654E7E | Called with 0x3F060A92 (30°/s) and 0x3E32B8C2 (10°/s); true drops the frame (0x006550A0) |
| Per marker: timestamp test | 0x00654F0C | Mismatch is checked first |
| Per marker: camera ID compare | 0x00654F10..0x00654F18 | A different ID skips the key check |
| Per marker: IsValidKey | 0x00654F24 | Called with the same key |
| Per marker: invalid key | 0x00654F2A → 0x00654FDA..0x00654FEC | Warns InvalidHistStateKey and continues to the next marker (0x00655012) |

**The stack:**
- **The live path:** frames come only through the queued camera worker (`queueForEngine: true`). The synchronous
  `ProcessFrame` entry has no production caller.
- **The key gate:** the marker loop follows the table in order, using the captured owner's `NativeRobotId` as the
  outer camera ID and the inner detector camera's ID 0 carried with each marker.
- **The earlier stages:** HISTORICAL-MARKER-CAMERA-SETTLEMENT-1 (checked 2026-11-09) covered the stages before the
  marker loop, and the key gap it left open is now closed.

**Status:** there are no transcendentals on this path, so it is EXACT_SOURCE.

## M14-008-FACE-LIFECYCLE-CLOSURE-1: PASS; M14-008 → EXACT_SOURCE

**AddOrUpdateFace, re-disassembled:**
- The counts (0x004F47C2..0x004F47D8): the total count goes up by one, and the frontal count (+0x114) only when TrackedFace+0x11 is set.
- immediate_recognition (0x004F47EE): fires when the old count is 0 and the face has a name.
- persistent_session_only (0x004F48DA..0x004F48E8): for an unnamed face, needs ID ≥ 1 and frontal count == 30 (0x1E).
- re_recognized (0x004F485C..0x004F4862): fires when the delta is ≥ 10001 (0x2711).

**ChangeFaceID's update_face_id gate (0x004F4074..0x004F409C):** both IDs must be positive and the entry ID ≥ 1, and
the entry must have a name or a frontal count ≥ 30.

**The expiry event's gate (0x004F53F0..0x004F5410):** ID ≥ 1, and a name or a frontal count ≥ 30.

**These all match Codex's claims:** the 0x46/0x47/0x4B order and recipients checked on 2026-11-09, and the narrow-name
pack.

**IsRecognitionSupported returns true (0x0086B244),** so the overlapping RemoveFaceByID calls are unreachable. That
closes the last gap the 2026-11-09 check left open on this record. The other gap, the expiry telemetry, is now built.

**Outside this record's path:**
- Supplied, non-stock detectors are a stack input.
- Detector modes are M14-010, recognition M14-011 and geometry M14-001.

## The other three: PASS, no settlement

- **FACE-ALBUM-FILE-REQUEST-1:** the 0x8F album file request goes load/save/replay through 0x0065827C and 0x00657924. M14-011 and M14-012 stay open.
- **FACE-TRACKER-IDENTITY-TRANSITIONS-1:** the tracker's identity transitions (0x0086DBCE..0x0086DC3C) feed the 0x47 remap. M14-010 and M14-011 stay open.
- **AI-BODY-TURN-TO-OBJECT-1:** the outside AI can queue the native TurnTowardsObject (Init 0x0054A1F0, CheckIfDone 0x0054ADD4), owner-bound, with defined completion. A capability; no record proposed.

**Records settled in this check: 2. Total: 34.**
