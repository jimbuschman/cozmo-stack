CLAIMED claude-sonnet 2026-10-03

Job R-FIX2: fix the defects in the `unresolved` of the records demoted by Corrections A2 (M12-001/002/007/010/012/017/019, M5-005/017), A3 (M10-002/009/010/011, M15-004/008/009/011/012/013/015), A5 (M3-024) and A7 (M6-003): 22 records. Rows: those texts plus research/20260929-audit-calibration.md, 20260930-reaudit-sonnet-layers.md, 20261001-reaudit-M3-M4.md, 20261001-reaudit-M5B-M6.md. Records stay IMPLEMENTATION_GAP, "built, awaiting strong verification" (CHECKLIST 6).

## Plan
- Stream A (M12 + M5 + the M15-012 hunk): M12-001/002/007/010/012/017/019, M5-005/017, M15-012.
- Stream B (M10/M15/M3/M6): M10-002/009/010/011, M15-004/008/009/011/013/015, M3-024, M6-003.
Both streams run in parallel in one tree (disjoint files); verifier passes per stream; one commit when both pass (hunks in shared files like CozmoEngine.cs may overlap).

## Progress (2026-10-03/04)
Streams A and B built in parallel in one tree (disjoint files), so one commit. Read-only Claude `cozmo-verifier` passes (the reports are the agents' messages):
- Stream A (M12-001/002/007/010/012/017/019, M5-005/017, M15-012): round 1 FAIL (5 blocking), round 2 FAIL (2 blocking + 1 narrow: the look-down carried-id release order, PickupObjectAction.Verify's order/results, the squint latch), round 3: no source-fidelity objections, 1 flaky test (fixed: the stamp comes from PickupObjectAction.VerifyStartedAt; the class ran 12 times clean). The final comment/MISSING-report hunks had no separate verifier pass.
- Stream B (M10-002/009/010/011, M15-004/008/009/011/013/015, M3-024, M6-003): round 1 FAIL (the TryToStackOn callback categories; the missing records), round 2 PASS.
- Seven new records M15-019..M15-025 (the floor-placement path M15-008/009 depended on; research/20261003-R-FIX2-M15-gap1-extraction.md; inventory Correction A4, M15-freeplay re-approved).
- Full suite 3273/3273 (several clean runs; two earlier failures happened with a stray test host running), `fidelity.py --check` clean (425 records).
All 22 records stay IMPLEMENTATION_GAP, unresolved "built, awaiting strong verification (R-FIX2, 2026-10-03): ..." with what remains. Nothing settled (CHECKLIST 6).

## Batch C: the floor-placement path (2026-10-04)
Built from the extractor's report (research/20261003-R-FIX2-M15-gap1-extraction.md) and the verifiers' reads: FindFreePoseInBeacon, the candidate filter (IsRunnableInternal's {PickUpObject, RollOrPopAWheelie} set with DidFailToUse by candidate id, 20.0f, pi/8), FindUsableCubesOutOfBeacons, AreAllCubesInBeacons, CanPickUpObject with the f32 IsPoseTooHigh, the whiteboard failure memory, TryToPlaceAt and its callback, PlaceObjectOnGroundAction as the engine's one class (verify child TurnTowardsObjectAction, the reaction lock for every caller), the NoFreePoses branch, InitInternal/TransitionToPickUpObject and the pick-up completion lambda, Robot::ComputeHeadAngleToSeePose and GetAbsoluteHeadAngleToLookAtPose (Unicorn-confirmed).
Records: M15-019..025 built, M15-008/009 updated; new records M15-026 (pick-up phase) and M11-054 (the head camera z: the engine's RobotHeadCam is (17.52, 0, -8.0), the stack's vision model uses 17.52 and cannot absorb the change until M11's exact solvePnP exists: three marker-pose tests miss by 10-19 mm with the corrected camera; the engine's value is used only inside ComputeHeadAngleToSeePose).
Verifier rounds: 4 (round 1 FAIL: the candidate filter; round 2 FAIL: the f32 IsPoseTooHigh, the carrying gate/InitInternal, the verify-child comments; round 3 FAIL: the pick-up substitution visibility and an invented head-angle fallback trigger; round 4 FAIL: the head-cam z). The last hunks (reverting the vision model's camera constant and scoping the engine's value to ComputeHeadAngleToSeePose; the pick-up lambda re-edits after round 4's findings) had no separate verifier pass. Full suite 3308/3308 twice, `fidelity.py --check` clean (427 records). Records stay IMPLEMENTATION_GAP, "built, awaiting strong verification".

## DONE 2026-10-04
Commits: 0422dd7 (streams A+B), and the floor-placement commit below. Open items for other owners are in each record's `unresolved`; the main ones: M11-054 (exact solvePnP and corner pipeline), DriveToPickupObjectAction's body and the stack-on DriveToPlaceOnObjectAction (M15-026/M15-008), TrackObjectAction in TurnTowardsObjectAction, the BlockWorld map +0x48 for AreAllCubesInBeacons, the engine's rotation matrices (Renormalize 0x008494E0), libm stand-ins, and HARDWARE_ONLY firmware behaviour for IS_PICKING_OR_PLACING.
