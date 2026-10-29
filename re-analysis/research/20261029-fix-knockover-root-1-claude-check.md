# FIX-KNOCKOVER-ROOT-1 — Claude check (manager, 2026-10-29)

**Verdict: PASS. The intermittent KnockOver fault is found and fixed. Nothing settled.**

## The fault

FIX-KNOCKOVER-RACE-1 added full fault capture, and it caught the fault in Codex's DOCKING-TEST-SIMPLE-1 full gate
(`research/20261029-docking-test-simple-1-knockover-failure.txt`, branch codex-m13-manager-2 e0d7545d).
- `NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates` failed with InvalidOperationException
  "MISSING: M11-066 caller has not handled unrelated pose roots".
- Stack: `Pose3d.WithRespectTo` ← `FlipBlockAction.CheckIfDoneTick` ← DriveAndFlipBlockAction ← legacy KnockOverCubes.
- The cube's pose and the robot's pose occasionally sit under different roots on a RUNNING tick.

## Engine behaviour (checked by Claude)

FlipBlockAction::CheckIfDone, RUNNING with a located object:
- 0x0055F0A0..0x0055F0BA constructs a local Pose3d named "": identity rotation, zero translation.
- 0x0055F0E0 calls GetWithRespectTo(objectPose, robotPose, local) and ignores the bool. 0x0055F0E4 reads the local
  unconditionally.
- GetWithRespectTo 0x00845A34 (`research/20261014-pose-rows-recheck.md` R35 step 3): on different roots it returns false
  with no log and leaves `out` untouched.
- So the norm is 0, below [this+0x138]. With [this+0x13C] == -1 the carry lift is queued and the tick stays RUNNING.

This is defined behaviour, so "What we reproduce" governs it. The stack threw instead: an implementation bug.

## Change (cozmo-implementer; checked by Claude)

- **`FlipBlockAction.CheckIfDoneTick`:** `TryWithRespectTo` into a fresh `Pose3d("")`, result ignored. This is the same
  pattern as NativeDriveAndFlipRunner.cs:52. The ported GetWithRespectTo returns false without writing on
  HasSameRoot false (Geometry.cs:354).
- **Regression test `M13_028_ARootMismatchLeavesTheLocalAtZeroAndQueuesTheCarryLift`:**
  - Setup: the cube is 201 mm away under an unrelated root, so a matching-root case would not lift.
  - Asserts: no throw, RUNNING, LiftRaised, and one SetLiftHeight to the carry height.
  - Expected values come from the cited addresses. It fails on the old code with the captured exception.
- **Results:** M12RVisBuildTests 142/142; the KnockOver test 5/5.

M13-028 stays IMPLEMENTATION_GAP: its other open items (the destructor's lift cancel and reaction-lock removal) are
unchanged. Why the roots differ in the legacy test (an origin change mid-flip) is not investigated. The engine handles
it the same way.
