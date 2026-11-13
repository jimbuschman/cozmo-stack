# HANDOFF RETIRE-LEGACY-DRIVE-API-1 (Claude worker, 2026-11-13)

Operator decision (BLOCKED.md, 2026-11-13): option 2, **retire and rebind**. Manager answers Q3-Q6:
`research/20261113-worker-jobs-10-13-m11-009-claude-check.md`. Census and implementation reports are worker-local:
`.scratch/RETIRE-LEGACY-DRIVE-API-1-census.md`, `.scratch/RETIRE-LEGACY-DRIVE-API-1-impl.md`.

## Before you merge: one manager action (verifier blocking finding)

**M12-004 is still EXACT_SOURCE but has no built form.** Its only built form was the legacy DockHelper's
`DriveToObjectAction.ExcludePoses` (UseSecondClosestPreActionPose 0x005BEE56), and no native runner or helper sets an
exclusion (Q4). Per your Q4 answer it becomes IMPLEMENTATION_GAP; I may not change a status. Its `unresolved` says so.
The engine caller of 0x005BEE56 is not in the inventory (MISSING), so the job that would build it still needs a read.
The robot runtime never set the exclusion before either, so robot traffic is unchanged; only the status claim is wrong.

## What changed

**Removed (the legacy public drive types):**
- `DriveToPoseAction`: the public `(ManipulationSystem)` constructor (now private), `_native`, the legacy Init and
  CheckIfDone branch, `Profile`, `IgnoreObstacleIds` (Q5); goals enter only through `SetNativeGoals`.
- `DriveToObjectAction`: the legacy flow (RunAsync, InitHelperAsync, `Chosen`, `Profile`), type 6 (Q3: the 4-arg ctor,
  FromGotoObject, the object-delta goal, the type-6 CheckIfDone branch), `ExcludePoses` (Q4), the non-native
  GetPossiblePoses branch and `NativeInputs` (Q6: nothing outside the retired types reached it).
- `DockHelper`, `DriveAndFlipBlockAction.RunAsync`, `DriveToFlipBlockPoseAction`, `DriveToPlaceCarriedObjectAction.RunAsync`.
- The legacy behaviour classes PickUpCube, RollBlock, StackBlocks, PickUpAndPutDownCube, KnockOverCubes, PopAWheelie,
  CubeLiftWorkout, BuildPyramidBase and the Explorer DriveInDesperation (with FreeplayTool.WarnStandaloneEnergy).
  The legacy BringCubeToBeaconBehavior stays (NativeBringCubeToBeaconBehavior's `_floor` uses FindFreePoseInBeacon, and
  M15-019..M15-026 name it); its two DockHelper transitions now report MISSING and start nothing. It is shadowed in
  production and standalone.
- The holds and guard: NativePathComponent's legacy entry `StartDrivingToPose(int,...)`, `_legacyEntry` /
  `LegacyEntryPathInFlight`, H6/H7, the legacy restart tail, the planner-model fields, the legacy origin inputs;
  `NativePlannerSet.Configure` and its borrowed arms; `BlockWorld.PathInFlight` and the LocalizeToObject legacy-path
  guard; the CozmoRobot binding. OriginOwnerInput and ObjectsChangedInput stay.

**Rebound:**
- `FreeplayStack.Create` passes `NativeRobot` **only for the ten retired drive classes** through a new
  `BehaviorFactoryContext.NativeRobotClasses` filter (PickUpCube, RollBlock, StackBlocks, PopAWheelie, KnockOverCubes,
  CubeLiftWorkout, BuildPyramid, BuildPyramidBase, BringCubeToBeacon, DriveInDesperation). The filter is
  CreateBehavior's first statement; every other class binds exactly as before. The canonical runtime sets no filter.
  (Passing NativeRobot unfiltered also rebound faces, needs, AcknowledgeObject and CantHandleTallStack, which the
  decision did not cover.)
- Conformance: `InternalsVisibleTo("cozmo-conformance")` (the tool's real AssemblyName). ManipTool's
  --driveto/--pickup/--roll/--flip queue the native runners on the canonical ActionList; --wheelie/--knockover/--stack
  select the configured native behaviour. M5Hw DRIVING uses `DriveToPoseAction(m,false,false)` + `SetNativeGoals`.
  ManipTool now prints only observed facts (completed actions, outbound EnableStopOnCliff frames, the objective
  broadcast). HardwareCatalog is unchanged.

**Verifier on the native path and robot runtime:** no behaviour change on any path that can run (each removed branch
was reachable only with `_legacyEntry`, a null native planner which H1 excludes, or `_ownsNativePoses` false).

## For the manager

1. **M12-004 status** (above, blocking).
2. **HardwareCatalog judges T and R can no longer pass:** `--knockover` / `--stack` no longer print
   `KnockOver success=yes` / `StackBlocks success=yes` (the native behaviours expose no such flag; printing it would be
   inferred). The judges (HardwareCatalog.cs:1022-1023, 1065) need rewriting to observed signals before the operator
   runs T or R. U still passes from observed signals.
3. **ManipTool N and P** now run the drive-and-dock actions, not the pickup/roll helpers (a helper needs a parent
   behaviour), so they no longer exercise the 2/3-attempt retries.
4. **Locations to repoint:** M13-014, M13-015, M12-012 hold no tag of theirs at their location any more.
5. **Two ids unbound standalone:** SparksPickUpCube (PickUpAndPutDownCube) and PyramidRespondPossiblyRoll
   (RespondPossiblyRoll) have no native class (BehaviorContainer.cs factory), so option 2 could not keep them. The
   robot runtime doesn't bind them either. Recorded in M8-011. Worth telling the operator.
6. **M15-051** has no test left (its role-getter test went with the legacy BuildPyramidBase).

## Tests

Class-(a) tests (only the legacy flow, type 6, the holds/guard) are deleted; class-(b) tests are ported to the native
classes. Changed expected values cite the engine, and the verifier re-checked each in the .so: 0x0300000F (I2,
0x0055A894 + 0x0055A95C), 0x03000004 (0x00559798), the -20 mm drive-center offset (P9, 0x005135D4), the M12-009
boundary (0x005B8192..0x005B8198), RestingFlat 0x3E32B8C2 (0x0063C66C). Engine facts that lost their only test (type 6,
the L3/L4 installers, M15-051's getters) are recorded as having no built form in M12-022, M12-011, M12-035 and M15-051.

Two FreeplayTests now reach recorded gaps through the native classes:
- `ASevereNeedTakesPriorityOverFreeplayAndTheGetInPlaysOnce`: the post-refill loop (IsRunnableInternal 0x005D90AC
  returns 1) is no longer asserted. NativeDriveInDesperationBehavior's Energy post-drive search is M15-091's recorded
  MISSING, so the behaviour ends. The rest of the test stands.
- `TheWholeStackDrivesOffTheChargerAndPlaysWithTheCubeItSees`: unlock RollCube (9) is now a test input (the native class
  checks it, IsRunnableBase 0x005BD810). The roll is then never chosen: the intention-7 target refresh's on-top search
  needs every located object's GetBoundingQuadXY (vtable +0x50, 0x00626048), and the charger's is unread (M13-025), so it
  throws NativeConfigurationUnavailable. **The robot runtime fails the same way whenever a charger is located.** The
  test pins the gap (no roll, no dock) and fails when it is built. Proposed: CHARGER-BOUNDING-QUAD-1.

Queued items fixed: a swallowed M13X2 accel assertion restored; a `reach:` label on HikingRollCubeRecoveryTests; stale
`test` names of the deleted BuildPyramidBase test removed from 17 records.

## Records

`unresolved`/`test` only, plus `location` and tags for M12-009 (NativePickupBlockHelper.cs, the `Attempts<=1` test,
0x005B8192..0x005B8198) and M12-016 (NativeRollBlockHelper.cs), as the manager directed. Each record the census lists
carries "legacy public drive types retired and rebound, operator 2026-11-13" with what changed for it. No status
changed; `--approve` not run; `fidelity.py --check` passes. No record settled.

## Verifier: FAIL on M12-004's status only (manager action); everything else queued and fixed, or observations.
