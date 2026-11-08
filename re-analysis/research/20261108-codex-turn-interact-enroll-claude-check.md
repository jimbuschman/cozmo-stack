# Codex turn, Meet interaction and named enrollment batch: Claude check (manager, 2026-11-08)

**Verdict: all three PASS and merge (branch tip 52aece9f). No record settled, none added.**

Codex gate: `scripts/test-full.sh` on 969b3dee, 7963/7963, no skips.

## AI-BODY-TURN-CONTROL-1: PASS

- **Public API:** `CozmoMotion.QueueAiTurnInPlaceAsync` (angle, speed, acceleration, tolerance, relative or absolute),
  through the explicit native factory 0x0052AE74 with AtEnd and zero retries.
- **Spot-checked:** the completion getter at 0x005469A8..0x005469BE writes the u32 at +0xD4 (pose-frame change count)
  as variant 5, as built.

## MEET-COZMO-INTERACT-1: PASS

- **Spot-checked:** 0x005C2562..0x005C2572. The drive distance is +40 mm (0x42200000) when the 40 mm forward ray is
  clear and -15 mm when blocked, as built.
- **Built:** the stock Meet winner verifies the face (named F8 or unnamed F9), drives, tracks, and plays F7 for the
  configured 8–15 s, then the objective, emotion and Needs tail. The null map path is the M1-034 skip.
- **The test clock change (M4ControlTests)** is fixture-only; production timeouts are unchanged.

## MEET-COZMO-NAMED-ENROLLMENT-1: PASS

- **Public API:** `CozmoRobot.SetFaceToEnroll(name, saveId = 0)`, `CozmoRobot.CancelFaceEnrollment()` and
  `CozmoEngine.FaceEnrollmentCompleted` (result, ID, name). They post the shipped stock 0x87/0x88 inputs (all three
  options true); the Meet activity must be active, as in the app.
- **Spot-checked:** 0x005FFC76..0x005FFCA0. The scan deadline adds 8 s and is capped at 35 s (0x420C0000), as built.
- **Not exercised:** recognition from a physical face through the whole public route, and the robot. The music barrier
  can hold a stuck worker for up to two seconds, logged.

## Next jobs (manager order)

From Codex's BLOCKED.md: AI-BODY-CUBE-PICKUP-1, then VOICE-PICKUP-REACTION-1, then MEET-COZMO-RENAME-1.

## Progress

22 records settled (unchanged), 2 of 178 finished (unchanged), 761 records (unchanged). The capability jobs since
2026-11-07 have added public functions but settled no record: settling waits on the per-record Claude check, which
these partial jobs have not asked for.
