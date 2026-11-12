# Claude worker: PATH-ORIGIN-CHANGE-REPLAN-1: Claude check (manager, 2026-11-12)

Merged from `claude-worker` at cc95bde9. The worker ran its own census, built the change and had it verified; its
full gate passed 8403/8403 on 011b0354. The handoff is HANDOFF-PATH-ORIGIN-CHANGE-REPLAN-1.md.

## PASS

PathComponent's reaction to an origin change (M12-032) is now the engine's for native DriveToPose paths:
- **HandlePossibleOriginChanges 0x0064980C:** H1, H5, H6, H7.
- **RejiggerTargetsAndReplan 0x006499D0..0x00649B44:** R1–R8.
- **RestartPlannerIfNeeded:** S1.

I re-disassembled H1. HandlePossibleOriginChanges first tests pc+0x20, the selected planner (0x00649812..0x00649816),
and returns when it is null. The stack's old dispatch tested the wrong field, so it returned on every native drive, and
it also had the abort/rejigger condition inverted. The origin inputs are now bound in production.

**The manager's guard in BlockWorld.LocalizeToObject is narrowed:**
- **Native drives:** they localize and rejigger the engine's way.
- **Legacy-entry drives:** a cross-origin pair is still held back while one is in flight. That entry
  (`StartDrivingToPose(int, ...)`, used by DriveToObjectAction and ExplorerBehaviors) plans from goals that
  ComputeDriveCenterPose has not converted, so the engine's drive-center rejigger cannot be applied to it.
  - It aborts on delocalization (H5), as the engine does.
  - On H6/H7 it reports a visible MISSING.
  - Follow-up: LEGACY-DRIVE-TO-POSE-NATIVE-1.

**The 0x45 still goes out before the rejiggered ClearPath and segments**, which the production theory checks.

**No record settled.**
