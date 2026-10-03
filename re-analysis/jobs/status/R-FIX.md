CLAIMED claude-sonnet 2026-10-03

Job R-FIX: fix the defects recorded in the `unresolved` of the M11, M13 and M14 records demoted by corrections A3, A4 and A6 (31 records: M11-002, 003, 006, 008, 010, 015, 019, 020, 022, 023, 029, 031, 036, 039; M13-002, 003, 004, 005, 008, 009, 011, 012, 013, 015, 016, 017, 018; M14-001, 003, 004, 005). Rows: those texts plus research/20260930-reaudit-M11.md, 20261001-reaudit-M7-M8-M9-M13.md, 20260930-reaudit-sonnet-layers.md. Records stay IMPLEMENTATION_GAP, "built, awaiting strong verification:" (CHECKLIST 6).

## Plan
- Batch 1: float-bit and width fixes (M11-002/003/006/008/010/015/020/022, M13-002, M14-001/003/004/005).
- Batch 2: the f32 lattice planner and path defects in M13 (M13-003/004/005/009/011/018 and the charger/behaviour path defects 008/012/013/015/016/017).
- Batch 3: the M11 path defects (M11-019/023/029/031/036/039).

## Progress (2026-10-03)
Batches 1, 2 and 3 were built in parallel in one tree, so they are one commit (their hunks overlap in VisionSystem.cs). Verifier passes (read-only Claude `cozmo-verifier`s; the reports are the agents' messages, not files):
- Batch 1 (float-bit/width fixes: M11-002/003/006/008/010/015/020/022, M13-002, M14-001/003/004/005): round 1 FAIL (the FOV test, the BehindCamera stand-in, the cube-moved minimum size, the deferred TrackFaceAction), round 2 FAIL (the pan-speed claim), then fixed. The last hunk (the pan speed cap 0x40A78D36, accel 0x436DFFDB, tolerance [+0x84], 0x00565672..0x005656B4) had no separate verifier pass: the implementer and the round-2 verifier read the same addresses.
- Batch 2 (M13 lattice/charger/path: M13-003/004/005/008/009/011/012/013/015/016/017/018 and the Robot::Update charger-platform writer): round 1 FAIL (4 blocking: goal-id hash order, VerifyResult byte+4, per-action IAction timeouts, a local LastDriveOffChargerSec writer), round 2 FAIL (1 blocking: the nested mount timeouts for align/head/lift), then fixed; the final hunk (RunStage) had no separate verifier pass.
- Batch 3 (M11 path defects: M11-019/023/029/031/036/039): round 1 FAIL (2 blocking: the rotation gate dropped every marker frame; the 101-mismatch Delocalize was missing), round 2 PASS.
- Full suite 2892/2892 twice in a row, `fidelity.py --check` clean, `vision --synthetic` 5/5. The intermittent test-host crashes during the work were parallel runs plus a `taskkill` of testhost.exe by one worker; the clean runs had no other test host.

Records: all 31 stay IMPLEMENTATION_GAP, unresolved "built, awaiting strong verification (R-FIX, 2026-10-03): ..." with what remains. Nothing settled (CHECKLIST 6).

## Open items for other owners / the manager (in each record's `unresolved`)
- M11-019's frozen title ("A new pose origin in RobotState delocalizes") and evidence still cite the removed origin comparison: needs a correction and `--approve` by the manager.
- M11-036/050: the WasRotatingTooFast gate is not built (no runtime MISSING report, only a comment and a test): the engine's IMU keys need the image timestamp (0 on fw2457) and line2Number.
- M13-003/018: the obstacle source is a stand-in (BlockWorld, includes the charger); M13-011 GetCompletePath and StraightLinePlanner; M13-017 live the charger-platform step clears the flag as soon as the contacts drop because this stack never creates the located charger on SetOnCharger's rising edge (needs the M12 object creation); SetPoseOnCharger/robot+0x334; DriveStraightAction Init/CheckIfDone; the memory map.
- M14-003: TrackFaceAction runs as a Task.Delay(60) loop (no ActionList; Engine.ActionRunnerUpdate hosts head/lift moves only).
- Pose3d/CameraModel/OccluderList are double stack-wide (the engine builds poses in float); libm stand-ins (MathF acos/cos/atan2/sin/sqrt) are a policy question for later.
- Queued: OldOrigin log prints ids (engine prints pose names); Core008's wording; ClearCliffRunningStats hook on Delocalize; kmeans (double port) and ComputeClockwiseCorners (M11-030) not compared; DriveAndFlipBlockAction.GetPossiblePoses (M12-035) casts a double sum; the unused Footprint (M13-007 Block) centroid order; HeadGeometry.MinHeadAngleRad/MaxHeadAngleRad (CameraModel.cs) and ChargerActions.HeadToleranceRad/LiftSpeedRadPerSec, SearchActions.HeadToleranceRad, M11-013's DegToRadDecimal are rounded decimals (the engine's bits are 0xBEDF66F3 / 0x3F46D3F2 / 0x3D0EFA35 / 0x3C8EFA35).

## DONE 2026-10-03
