# Codex turn-to-face, dock clamp, pose-unknown, located-object delete, calibration install: Claude check (manager, 2026-11-09)

Merged from `codex-m13-manager-2` at afc11304. Codex's batch gate on b0a83526 passed 8133/8133.

## DOCK-CORRECTION-CLAMP-SETTLEMENT-1: PASS; M12-019 → EQUIVALENT_IMPLEMENTATION

**I re-disassembled the call site in UpdateDockingErrorSignal:**
- Relative XYZ is stored at 0x0063C14A..0x0063C180.
- The limit 0x3F32B8C2 is built at 0x0063C182 and 0x0063C188 and passed to ClampPoseToFlat at 0x0063C194.
- The yaw is widened to f64, pi/2 and the angle slot are added, and it is narrowed once at 0x0063C1A2..0x0063C1CE.

**I re-disassembled ClampPoseToFlat (0x00877330..0x008774EC):**
- the root pose's rotation matrix, its dominant Z-row column with sign (0x00877346..0x0087736C);
- acos of |entry|, then the Radians compare against the limit (0x00877370..0x00877384);
- the local column, negated for a negative axis (0x008773A6..0x008773D0);
- the f32 dot, acos and |angle| < epsilon early exit (0x008773DE..0x00877440);
- the cross product and its length checked against the same epsilon (0x00877442..0x008774AA);
- the angle-axis rotation left-multiplied into the local rotation (0x008774BA..0x008774CC).

`NativeDockingGeometry.Clamp` matches step for step. Both live paths, the 0x48 recipient and legacy DockAsync
OnFrame, now gate on the marker's root (world) rotation and correct the relative one. The legacy test shows the old
local-only gate gave a different angle (1.81459 against 1.95477 rad). The expected values come from the shipped ARM
code under the emulator, not from the C#.

**Why EQUIVALENT_IMPLEMENTATION:** acosf and the length's sqrtf are the host's libm (M13-023(e)). The pose inputs, the
marker choice and M11-006's 20° confirm clamp are separate records.

## CAMERA-CALIBRATION-INSTALL-SETTLEMENT-1: PASS; M11-039 → EXACT_SOURCE

**I re-disassembled Camera::SetCalibration 0x0085DEDC:**

| Case | Result |
| --- | --- |
| No calibration stored, null passed | Return 0 |
| Same pointer as stored | Return 0 |
| Distinct non-null pointer | Install (0x0085DEF0..0x0085DF08), return 1 |
| Null passed, one already stored | VERIFY at 0x0085DF12, return 0 |

**UpdateCameraCalibration 0x006B1E3E** calls MarkerDetector::Init with rows and columns (0x006B1E6C..0x006B1E78) only on
a 1.

**The stack:** it reproduces each branch. The VERIFY effect is the error log, the error flag and the flush. The NV
recipient now installs and resets under the captured owner, so a retired owner can't reset its successor.

**Constructor calibration:** the stock constructor starts with no calibration. A calibration supplied to the
constructor is a stack input and does not come from the engine.

**Separate records:** the NV read (M3-022) and the image drop (M11-049).

## AI-BODY-TURN-TO-FACE-1: PASS (capability, no settlement)

- **Route:** the public request goes through the existing AI queue (AtEnd, zero retries, original-owner cancellation).
- **Native factory:** 0x0052AECA with zero motor overrides: pan 5.2359877/10, tilt 15/20, 5° tolerances, no speech.
- **Target:** a cached-pose fallback for ID 0, a deleted reference, or a null reference.
- **Completion:** variant 6.

The records involved (M14-002, M13-014, M14-001, M8-015) stay as they are. Arbitrary motor overrides and the speech
variants are not exposed.

## AI-BODY-OBJECT-POSE-UNKNOWN-1 and AI-BODY-LOCATED-OBJECT-DELETE-1: PASS (capability, no settlement)

**0x52 RobotMarkedObjectPoseUnknown:**
- It is sent once per distinct collected ID, carrying the original ID, before deletion (0x0050739C..0x005073C6).
- Producers: the camera miss, WritePoseHelper state 0 and the Flip terminal.

**0x4C (the deleted object's actual ID):**
- It is sent after the pose-change effects, in the report loop at 0x0061D5B8..0x0061D5E4.
- Only current-origin, non-null victims are reported.

**Both reports:**
- Both are gated on the external interface (Robot::Broadcast 0x00511DE8).
- The app queue comes before local delivery.
- The dock subscriber invalidates its matching target.

**Test change:** the Flip terminal now deletes the object for real, which is the native behaviour, instead of leaving
a host tombstone. Three tests were updated to match.

**Records:** M11-038 stays IMPLEMENTATION_GAP. Its VisionSystem CheckMailbox producer (M11-040) is still unbuilt and
is released as the next job. M12-034, M11-007, M11-042 and M13-028 don't change.

## Next jobs

- HISTORICAL-MARKER-CAMERA-SETTLEMENT-1 (M11-050), already RELEASED by Codex.
- VISION-MAILBOX-1 (M11-040, the remaining M11-038 producer) is released after it.
