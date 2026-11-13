# Operator decisions

## RETIRE-LEGACY-DRIVE-API-1: the retirement reaches more than the decision below assumed (Claude worker, 2026-11-13)

**The question:** please confirm or narrow the "retire" decision below. When I wrote it up I said the legacy types were
used only by "the stack's own tools and the tests". The retirement census (`.scratch/RETIRE-LEGACY-DRIVE-API-1-census.md`,
worker-local) found that this undersold it. Nothing has been removed yet.

**1. The standalone stack's behaviours drive through the legacy types.**
- `FreeplayStack.Create`, used by FreeplayTool and CoreChecks, binds the shipped behaviours through
  `ShippedBehaviors.Manipulation`/`Navigation` (Behaviors.cs:609/632) and `ExplorerBehaviors.LoadShipped`.
- Its factory context sets no `NativeRobot` (FreeplayStack.cs:99-118, deliberately per 64-65). So the factory refuses
  the native classes and binds the legacy ones.
- The behaviours affected are about 12: PickUpCube, RollBlock, StackBlocks, PickUpAndPutDownCube, KnockOverCubes,
  PopAWheelie, CubeLiftWorkout, BuildPyramidBase / BuildPyramid, RespondPossiblyRoll, BringCubeToBeacon and the Explorer
  driver. Their drive step is DockHelper, `DriveToObjectAction.RunAsync` or `DriveAndFlipBlockAction`.
- The robot runtime (NativeBehaviorRuntime) is not affected: it binds only the native classes.

**2. The Conformance hardware tools can't reach the native classes.** All native runners are `internal`, as are
`DriveToPoseAction(m, head, manual)` and `SetNativeGoals`; `InternalsVisibleTo` names only the test project.
- **ManipTool** drives HardwareCatalog checks N, P, Q, R, S, T, U and X (pinned in HardwareCatalogFidelityTests and
  HardwareRunnerTests).
- **M5Hw.cs's DRIVING check** (834-865) uses the legacy constructor. M5Hw is the operator-run M5-HW harness.

**3. Records:**
- M12-009 and M12-016 (EXACT_SOURCE) carry their only `// fidelity:` tag on DockHelper, so the tags must move to the
  native helpers in the same commit.
- M12-004 (UseSecondClosestPreActionPose 0x005BEE56) loses its only built form: no native counterpart sets ExcludePoses.

**Choices:**

1. **Retire fully.** The standalone stack loses those behaviours: their ids become unbound standalone, or fail with a
   visible MISSING. ManipTool's N/P/Q/R/S/T/U/X modes and M5Hw's DRIVING check are deleted or rewritten.
2. **Retire, but rebind.** The standalone stack passes `NativeRobot` so it binds the native classes, which then run on
   the canonical robot's ActionList and PathComponent. The tools get a public or InternalsVisibleTo entry to the native
   runners. This is more work, and keeps the features.
3. **Retire only what nothing else uses.** Delete the legacy DriveToPoseAction(m) constructor path, type 6 and the
   unused DriveToFlipBlockPose and DriveToPlaceCarried RunAsync. Keep DockHelper and the legacy DriveToObject flow for
   the standalone stack and tools, documented as non-engine. In that case the legacy entry, its holds and the BlockWorld
   guard stay. They don't fire on the robot runtime.

**Meanwhile:** job 14 is paused. The census also leaves four smaller questions, which the manager can answer:
- Q3: retire type 6;
- Q4: M12-004 — keep ExcludePoses as a unit, or record the gap;
- Q5: drop the P13 IgnoreObstacleIds pass-through;
- Q6: the non-native branch of GetPossiblePoses.

## LEGACY-DRIVE-TO-POSE-NATIVE-1: is the legacy public drive API a supported C# feature? (Claude worker, 2026-11-12)

**DECIDED (operator, 2026-11-12): option 1, retire.** The legacy public drive types are retired as stack machinery. Job
RETIRE-LEGACY-DRIVE-API-1 does it.

**The decision:** retire or rebuild the stack's legacy public drive types. AGENTS.md: "Excluding ... a C# API feature
needs the operator."

**The types:**
- the `DriveToPoseAction(ManipulationSystem m, …)` constructor and its legacy Init;
- the legacy `DriveToObjectAction`;
- `DockHelper`;
- the standalone Explorer driver (`ExplorerBehaviors.cs:314`).

**The census** (worker's `.scratch/LEGACY-DRIVE-TO-POSE-NATIVE-1-census.md`, summarised in the job's handoff) found:
- **No production path reaches them.** The robot's behaviours run through `NativeBehaviorRuntime`, which builds only the
  native classes. They drive through the native, engine-matching DriveToPose entry (StartDrivingToPose 0x0064ACC4 with
  the drive-center conversion 0x005135D0). Their only users are:
  - the stack's own tools (`FreeplayStack.Create` for FreeplayTool/CoreChecks, ManipTool, M5Hw.cs:838);
  - the tests;
  - the public constructors.
- **The legacy flow contradicts the engine** (DriveToObjectAction::InitHelper 0x00558FB4):
  - it drives to one reduced goal, where the engine passes all pre-action poses to SetGoals at 0x00559302..0x0055935A;
  - it sets an ignore-obstacle ID, where the engine sets none;
  - it never moves the head;
  - manual speed is always false, where the engine passes +0x8C;
  - it plans from the history main pose toward unconverted, borrowed goals.

  The Explorer driver is not the engine's BehaviorDriveInDesperation flow, which is already built natively as M15-091.
- **What the guard covers:** while these types exist, `NativePathComponent` holds a legacy path on origin-change
  replan/restart (visible MISSING). `BlockWorld.LocalizeToObject` keeps the manager's path-in-flight guard for legacy
  paths. Neither fires in production, because no production path is legacy.

**Options:**
1. **Retire** the legacy public drive types as stack machinery. Move the tools and tests to the native classes; then
   remove the legacy entry, the H6/H7 holds, `LegacyEntryPathInFlight` and the BlockWorld guard. This is a C# API
   removal.
2. **Rebuild** them on the native entry as supported API: all goals, no ignore ID, the head move, manual speed and
   owned goal copies; the type-6 and Explorer goals would need a parent. Then remove the same holds.
3. **Keep** them as they are, documented as non-engine stack tools, with the holds and the guard left in place.

**Meanwhile:** the worker builds only what doesn't depend on this decision (see the job's handoff).

Capability and settlement proposals don't belong here, and they don't wait for approval. Add them to QUEUE.md as rows
and start them, per AUTONOMY.md ("Empty queue: scope the next job yourself"). This file is only for decisions
AGENTS.md reserves to the operator.
