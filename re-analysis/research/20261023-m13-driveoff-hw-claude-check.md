# Claude check: M13-DRIVEOFF-HW robot check (2026-10-23)

Read-only cozmo-verifier pass on the new harness (`cozmo-stack/src/Cozmo.Conformance/M13DriveOffHw.cs`, tests
`M13DriveOffHwTests.cs`, guide `acceptance/hardware/scripts/m13-driveoff-hw-1.md`). Instructions read in
libcozmoEngine.so. **First pass FAIL (1 blocking), fix re-verified PASS.** Harness only; no record settles.

## Confirmed against the binary

- Travel rule: GetPose 0x004EA398 -> MoveRobotPoseForward(d = -20.0f, 0x00512D4E; 0.0 only when carrying,
  0x00512D14) -> UpdateCurrPoseFromHistory 0x00512D74 -> again 0x00512DD4..0x00512DEC; f32 norm 0x00512E2A..0x00512E4C
  (z = 0 from 0x00517EC0..0x00517F3E); +0x528 sum; literal 0x00512D18 = 50.0; `ble` 0x00512E96; one-shot +0x52C. +0x528
  and +0x52C are written only in the ctor (0x0051023C/0x00510240) and UpdateFullRobotState. The harness's `Travel`
  matches it step for step.
- Robot pose writers other than UpdateCurrPoseFromHistory go through SetNewPose -> AddVisionOnlyStateToHistory
  (++frame 0x00515152), so the constant-frame/origin requirement catches them. The contact-true observation writes only
  the charger.
- Before the drive exactly [50, 50]: senders are SetOnChargerPlatform (on a change only, 0x00511D66..0x00511D70; the
  rising edge 0x00511BA8..0x00511BB6 is a change), the first-state 50 (0x00512DA2), the distance 400 (needs travel),
  the increment (cache above 150, 0x00634514) and the Delocalize restore (logged, excluded).
- After the crossing state, exactly two 400s with the first message 400: the increment only sends 150 from 400.
- Tests are not circular; all labelled `reach: unit`.

## Blocking finding and fix

The grading window ran to the end of the capture, including the question phase: a re-dock and a lift while answering
gave a false PLATFORM_EXIT PROBLEM, and a single lift turned HOLD_50/DISTANCE_400 into COULD NOT CHECK. Fixed:
`Capture.DriveEndMs` (behaviour end + 1.5 s, or Ctrl+C) bounds the wire, the platform events and the restore log.
Tests `EvidenceAfterTheDriveWindowIsNotGraded` and `IncrementBetweenTheTwoFourHundredsStillMatches`; 15/15 pass.
Operator text: don't touch until TEST FINISHED; Ctrl+C if he heads for an edge. Ctrl+C now also ends the window
(verifier queued item 2, done by the manager).

## Queued (non-blocking)

1. The exit time comes from the API event stamp; a broadcast stall over 1.5 s could put the exit 400 inside the
   window and the event outside (false DISTANCE_400 PROBLEM). Unlikely; could derive the exit from the wire 400.
2. HOLD_50 and DISTANCE_400 disagree on an exit before 50 mm (unreachable by geometry; exit is about 85.9 mm).
3. A RobotStopped during the drive ends the behaviour in the engine (H4 0x00535432..0x00535480); the harness-ticked
   behaviour keeps re-driving. Noted in the guide's Limits.
4. No test covers a platform that never clears after a long drive beyond the existing INCONCLUSIVE case.
