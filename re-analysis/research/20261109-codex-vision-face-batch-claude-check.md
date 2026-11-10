# Codex historical marker camera, vision mailbox, computed history keys, observed-face and face-ID reports: Claude check (manager, 2026-11-09)

Merged from `codex-m13-manager-2` at efc0649b. Codex's batch gate on 1ea06955 passed 8167/8167. The first gate run
failed 12 rendered-scene tests, which lacked historical IMU brackets. The fixtures were corrected, not the engine path.

## What I re-disassembled

**Marker loop 0x00654EE0..0x00654F24:**
- A timestamp mismatch branches away at 0x00654F0C.
- The Camera ID compare (0x00654F10..0x00654F18) comes before IsValidKey at 0x00654F24.
- So a stock marker (inner camera ID 0) against the outer camera (robot ID 1) bypasses the key check, as
  COMPUTED-HISTORY-KEY-SETTLEMENT-1 says.

**IsValidKey 0x0053214C..0x00532186:** a lower-bound search of the reverse map at +0x28, returning membership only.

**FaceWorld expiry 0x004F5380..0x004F53AC:**
- It applies only to unnamed faces (the name-length test at 0x004F538E..0x004F53A0).
- The limit is 15000 (0x3A98).
- A face expires when the prior timestamp is unsigned-greater than last-observed + 15000 (`bls` skips it).

**0x47 Pack 0x00713A80:** two 4-byte writes, old ID then new ID, and no name.

The rest of each handoff is consistent with these points and with the earlier checked recipient boundary:
Robot::Broadcast 0x00511DE8, then the app queue at 0x00662526, then the local signal at 0x00662552.

## Verdicts

| Job | Verdict | Records |
| --- | --- | --- |
| HISTORICAL-MARKER-CAMERA-SETTLEMENT-1 | PASS | M11-050 stays open: the same-calibration key carrier is MISSING |
| VISION-MAILBOX-1 | PASS | M11-040 and M11-038 stay open: input statistics, and the tool-code, laser and debug consumers |
| COMPUTED-HISTORY-KEY-SETTLEMENT-1 | PASS | M11-044, M11-051 and M11-097 stay open: the alternate-camera identity and the other callers |
| AI-BODY-OBSERVED-FACE-REPORT-1 | PASS, capability | M14-008 stays open: optional expression and gaze modes, and the wider lifecycle |
| AI-BODY-FACE-ID-REPORTS-1 | PASS, capability | M14-008 stays open: the expiry telemetry and the RemoveFaceByID overlap |

**No record is settled.** Each record named keeps a MISSING the handoff states, so none owns its whole path yet.

**What the outside AI gains:**
- the full stock 0x46 RobotObservedFace report;
- the 0x47 face-ID change report;
- the 0x4B face-deleted report;
- unnamed-face expiry, which now runs on the engine's prior-frame clock.

## Next

Codex's REVIEW note asks for jobs that close the named MISSING items, so that M11-050, M11-044 and M14-008 can settle.
