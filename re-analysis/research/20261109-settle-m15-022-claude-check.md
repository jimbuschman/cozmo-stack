# GROUND-AT-POSE-SETTLEMENT-1 and M15-022: Claude check (manager, 2026-11-09)

**Verdict: PASS and merge (branch tip 7aa64214). M15-022 settled EQUIVALENT_IMPLEMENTATION. 27 records settled.**

Codex gate: `scripts/test-full.sh` on 327a3b34, 8069/8069, no skips. The first run on a4151786 failed two fixtures:
one missing the shipped planner, and one whose diagnostic was deduplicated across the process. Both were fixed.

## The engine path, checked

- **Re-disassembled:** PlaceObjectOnGroundAction::CheckIfDone 0x005549B0..0x005549E6.
  - When DockingComponent(+0x280)+4 (busy) is set, it latches +0x84 and returns RUNNING.
  - With the latch set and busy clear, it keeps returning RUNNING while MovementComponent(+0x254)+9 is set.
  - Only then does it update the verify child at +0x80 (0x004AA984).

  The stack's runner does the same, with the movement byte written at the engine's own update point.
- **Placement-at-pose:** the compound at 0x00554AFC..0x00554BAC is a sequence of DriveToPlaceCarriedObject (type 2,
  goal check on, 10 mm padding) and Ground on Drive success. It proxies the Ground tag and completion. Both live
  Bring callers, the manual standalone binding and the canonical behaviour, now admit that compound on the original
  robot.
- **The old open items on the record are closed:**
  - The 0x03000005 marker-transform failure is built (GROUND-VERIFY-MARKER-1, `research/20261109-...` batch).
  - The verify child's single RUNNING tick (0x0054AE40..0x0054AEB8) and its visual child are built.
  - The TrackObject branch (+0x192, 0x0054AE08) cannot be reached, because Ground constructs the child with
    track = false.
  - The head-angle solve now uses the native M11-065/M11-066 algebra. The old double-precision note belonged to a
    flat adapter outside this path.
  - The reaction lock: every Ground runner now requires the original robot's reaction manager, which the ordinary
    CozmoRobot lifetime supplies before Freeplay.

## Why EQUIVALENT_IMPLEMENTATION, not EXACT_SOURCE

The verify child's head-angle solve is M13-026 (EQUIVALENT_IMPLEMENTATION). It uses host atan2f and the sqrtf NaN
fallback in place of the phone's libm.

## Whole path

Each part of the path has a record of its own:
- the 0x44 wire is M12-015;
- the completion projection is M12-052 (the four undefined ID words are skipped under M1-034, 0x00554AE0..0x00554AFA);
- the head-camera solve is M13-026;
- the drive and footprint leaves are M12-030, M11-088, M11-091 and M13-024;
- choosing the candidate pose, upstream of this action, is M15-019.

## Hardware check (separate; does not change provenance)

Whether the firmware raises and clears IS_PICKING_OR_PLACING for 0x44, and in what order relative to
PickAndPlaceResult, is the robot's behaviour. The engine's response to either order is defined and built, and fake
firmware tests cover it. A hardware run should record that order. Verification stays NONE until it does.

## Next

Codex's proposals are approved in its order:
1. AI-BODY-CUBE-WORLD-STATE-1
2. TRACK-FACE-LIFECYCLE-SETTLEMENT-1 (M14-003)

## Progress

27 records settled (was 26). Since 2026-11-08: M15-080, M6-005, M14-009, M6-007, M15-022.
