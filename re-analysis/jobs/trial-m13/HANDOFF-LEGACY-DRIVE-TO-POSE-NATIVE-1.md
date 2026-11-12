# HANDOFF LEGACY-DRIVE-TO-POSE-NATIVE-1 (Claude worker, 2026-11-12): partly blocked on the operator

The census is the worker's local `.scratch/LEGACY-DRIVE-TO-POSE-NATIVE-1-census.md`; its summary is below.

## What the census found

**No production path reaches the legacy entry `StartDrivingToPose(int, ...)`.**
- **The robot runtime** builds only native classes: CozmoRobot.cs:227 → NativeBehaviorRuntime.
- **Native drives** go through StartNativeDrivingToPose / DriveToPoseAction.InitNative. Those match the engine's
  DriveToPoseAction::Init (0x0055A86C..0x0055AA66) and StartDrivingToPose (0x0064ACC4..0x0064AF30) in behaviour,
  including:
  - the drive-center conversion: ComputeDriveCenterPose 0x005135D0, offset -20 or 0 (literal 0x005135F4), then
    MoveRobotPoseForward 0x00517EC0;
  - the differences are log lines only.
- **The legacy public drive types** are the `DriveToPoseAction(ManipulationSystem)` constructor and its legacy Init, the
  legacy DriveToObjectAction flow, DockHelper and the standalone Explorer driver. They are reached only through:
  - the public C# constructors;
  - the stack's own tools (FreeplayStack.CreateStandalone, ManipTool, M5Hw.cs:838);
  - the tests.
- **The legacy DriveToObject flow contradicts the engine** (DriveToObjectAction::InitHelper 0x00558FB4):
  - it drives to one reduced goal, where the engine sets all poses (SetGoals 0x00559302..0x0055935A);
  - it sets an ignore-obstacle ID, where the engine sets none;
  - it has no head move;
  - its manual speed is always false;
  - it plans from the main-pose start toward unconverted, borrowed goals;
  - its type-6 goal has no parent (the engine's has one: 0x00559252).
- **The Explorer driver** is not the engine's BehaviorDriveInDesperation (M15-091, built natively).

**So the remaining legacy holds never fire in production:** the H6/H7 MISSING in NativePathComponent,
`LegacyEntryPathInFlight` and the BlockWorld.LocalizeToObject guard.

## Blocked on the operator

`BLOCKED.md`: is the legacy public drive API a supported C# feature? The three options are to retire it, rebuild it on
the native entry, or keep it as is. Removing the holds and the guard depends on that decision. Nothing legacy was
changed.

## What was built (it does not depend on the decision)

| Where | Change |
| --- | --- |
| NativeDriveToObjectRunner.cs:75-76 | The inner DriveToPoseAction's manual flag is now the action's +0x8C (`UseManualSpeed`) instead of a hard-coded false (0x005592E8..0x005592FE: r2 = 1 head, r3 = [this+0x8C]) |
| DriveActions.cs | `UseManualSpeed` gets an internal setter (a test seam; nothing in src writes it) |

**Production behaviour is unchanged today.** The runner builds its action with the 7-argument-form constructor, which in
the stack takes no manual argument, so +0x8C stays false. The engine's 7-arg constructor (0x0055850C) writes +0x8C from
its last argument (0x005585A8). The verifier found two engine callers that forward their own value:
- IDriveToInteractWithObject (0x0055B2C8), the base of the pickup/place/roll drive-to actions;
- DriveToAndMountChargerAction (0x0054E836), app/SDK path only.

That plumbing and the runner's own track mask (`useManualSpeed ? 0 : 4`, 0x00558544..0x0055854C) are MISSING. They are
recorded in M12-022 and proposed as **DRIVE-TO-OBJECT-MANUAL-SPEED-1**.

**P13:** the native entry passes IgnoreObstacleIds into the planner, which the engine has no input for. No native caller
sets it. Recorded in M12-032 as stack-only, with no production effect.

## Tests

**`NativeDriveToObjectManualSpeedTests`** (new, `reach: unit`):
- With +0x8C set, the inner drive's track mask is 0 (0x0055A25A..0x0055A264) and Path +0x46 is true (0x0064AD44).
- The default is mask 4 and +0x46 false.
- The first test fails on the old code.

**Results:**
- implementer: 762/762, plus 183/183 for the runner's other users;
- verifier: 193/193;
- full gate: see the commit.

## Verifier: PASS

Two notes were queued, both visible and neither blocking:
- **The MISSING plumbing:** the callers above.
- **"Unreachable in production":** this means the robot runtime; the types stay reachable through the public C# API.
  That is exactly the BLOCKED.md question.

## Records

`unresolved` notes only, plus `test` on M12-022:

| Record | Note |
| --- | --- |
| M12-032 | P9/P10 citations; P13 |
| M12-022 | D4 built; the legacy contradictions; the MISSING items |
| M12-023, M12-024 | The legacy Init contradicts I1..I6 |
| M12-011 | D9 confirmed |
| M15-091 | The Explorer driver is not the engine flow |
| M11-044 | The guard covers only legacy paths |

- The QUEUE row's M12-033 was the wrong ID; the owning records are listed above.
- No settlement.
