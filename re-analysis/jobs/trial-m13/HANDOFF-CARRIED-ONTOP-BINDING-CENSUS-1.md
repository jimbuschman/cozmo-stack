# HANDOFF CARRIED-ONTOP-BINDING-CENSUS-1 (Claude worker, 2026-11-12)

This handoff carries the census summary, because the census itself (`.scratch/CARRIED-ONTOP-BINDING-CENSUS-1-census.md`)
is local and gitignored. The manager's `CENSUS-M11-043.md` (row L2) started it.

## The census, in short

**CarryingComponent::IsCarryingObject (0x00633F88..0x00633F9E)** is `id == carried [+8] || id == on-top [+0x14]`.
- It is reached only through PLT 0x004A6FAC, from 15 call sites in 13 functions:

| Site | Function |
| --- | --- |
| 0x00505E0A | UpdatePoseInInstance |
| 0x00507270 | MarkObjectUnknown |
| 0x00507676 | MarkObjectDirty |
| 0x0053411E / 0x0053417C | HandleActiveObjectMoved |
| 0x0053492C / 0x0053498C | HandleActiveObjectStopped |
| 0x0055937A | DriveToObjectAction::InitHelper |
| 0x0056D142 | FindCubesInBeacon |
| 0x0061DF18 / 0x0061DF8A | CycleSelectedObject |
| 0x00620F06 | AddAndUpdateObjects |
| 0x00621926 | UpdatePoseOfStackedObjects |
| 0x0062597A | CheckForCollisionWithRobot |
| 0x00628E60 | UpdateObjectOrigins lambda1 |
| 0x0063A3D0 | CubeLight RobotDelocalized |

- **Carried-only inline reads of [+8]**, a different test:
  - FindObjectMatchForObservation's predicate, 0x00507CF6..0x00507D06;
  - CheckForUnobservedObjects, 0x00621D70..0x00621D78;
  - ClearLocatedObjectHelper, 0x00625CF0..0x00625CF8.

**CarryingComponent::UnSetCarryObject (0x00633F6C..0x00633F86):**
- carried ID: UnSetCarryingObjects(false);
- on-top ID: UnSetCarryingObjects(true).

**UnSetCarryingObjects (0x00633C00):**
- When not top-only and something is carried, it sends SetCarryState(0) (0x00633D6A).
- It clears [+8] (0x00633D78, not top-only) and [+0x14] (0x00633D80), synchronously.

**Carried-stack origin move (lambda1, then the erase at 0x0062064C):**
- The top cube's pose is copied with its parent kept: the old carried cube's node.
- ~PoseBase (0x008450F4) logs "PoseBase.Destructor.NotLastOwner" through sVerifyFailedReturnFalse (0x00845156 → 0x0080D9A4) and releases only its own reference.
- So the node stays alive, and the top cube keeps following the lift.
- This is defined behaviour (OV121 A8-15 confirmed), not a crash.

## What changed

| Where | Change |
| --- | --- |
| Docking.cs | The shared BlockWorld.IsCarryingObject binding is now the engine body (`Carrying.IsCarryingObjectId`). Seven readers follow it: UpdatePoseInInstance, MarkObjectDirty, MarkObjectUnknown, the Moved/Stopped dirty guard, AddAndUpdateObjects, UpdatePoseOfStackedObjects and UpdateObjectOrigins lambda1. |
| BlockWorld.cs | The three inline readers use a new carried-only `CarriedOnly` (0x00507CF6, 0x00621D70, 0x00625CF0). A NotLastOwner Error log at the old carried cube's erase in a carried-stack origin move. Moved objects keep the stack-only NativeCarryingTree flag in lambda1's carried branch. A comment says CheckForCollisionWithRobot must use the engine body when it is built. |
| ManipulationSystem.cs | The ObjectMoved/ObjectStoppedMoving E2G exclusion uses the engine body (0x0053417C, 0x0053498C). This brings M4-009's code back to its own evidence. |
| DriveActions.cs | Legacy DriveToObjectAction uses the engine body (0x0055937A). |
| Docking.cs + ManipulationSystem.cs | **UnSetCarryObject is wired in production** (0x00633F6C body; the call is at 0x00505FBC in UpdatePoseInInstance's carried branch). A sighting of the top cube clears only the top, and the observation proceeds. A sighting of the carried cube clears both and sends CARRY_NONE. Before, the unset was only counted. |
| Comments | Stale comments fixed in Docking.cs, VisionSystem.cs, BlockWorld.cs and ManipulationSystem.cs. |

**Not built:**
- The engine's `_errG`, the call-stack dump and the logger provider's vt+0x14 (error machinery).
- The follow-on UnownedParent logs.
- CycleSelectedObject: no sender.
- The about 60 behaviour sites that read [+8] inline. They are outside this binding.

## Tests

| Test | Reach |
| --- | --- |
| `CarriedOnTopBindingTests.TheCubeOnTopIsExcludedFromTheMovedBroadcastAndIsNotDirtiedByItsOwnMotion` | production for the broadcast exclusion; unit for the dirty part (PoseState substituted Known) |
| `CarriedOnTopBindingTests.ASightingOfTheCubeOnTopUnsetsItAndIsNotDropped` | production (the attach goes through the public SetObjectAsAttachedToLift; the scene keeps the cubes where they stood) |
| `CarriedOnTopBindingTests.ASightingOfTheCarriedCubeUnsetsBothAndSendsCarryNone` | production (same caveats) |
| `M11RVisBuildTests.M11_043_TheCarriedOnlyReadersStillTreatTheCubeOnTopAsNotCarried` | unit |
| `M11RVisBuildTests.M11_043_ACarriedStackKeepsTheTopCubeOnTheOldCarriedNodeWhichFollowsTheLift` | unit (hand-worked poses (1035,0,74), then (1055,0,94)) |
| `M11_037_CheckForUnobservedObjects…` | changed: its carried case uses CarriedPrimaryId, per 0x00621D70 |

**Results:**
- implementer: 1880/1880 on the affected classes;
- verifier: 516/516;
- full gate: see the commit.

## Verifier: FAIL, then PASS

| Round | Finding | Resolution |
| --- | --- | --- |
| 1 | BLOCKING: the sighting test pinned "dropped, still carried", which the binary contradicts. The engine unsets inside UpdatePoseInInstance (0x00505FBC) before the 0x00620F06 test | UnSetCarryObject wired; test rewritten to the engine's outcome; M12-008 text corrected |
| 1 | Queued: a wrong address 0x00625D32 (correct: 0x00625D4E) in a test doc and in ClearLocatedObjectHelper's MISSING text; the manifest cited the gitignored census; reach label caveats | Fixed |
| 2 | PASS. Observation: UnSetCarryObject reads both IDs under one lock and calls outside it; callers hold FrameGate, so there is no observable difference | — |
| 2 | Log only: NotLastOwner doesn't set `_errG`, which has no stack reader; its use_count text is approximate | Left |

## For the manager

- **M4-009 (EXACT_SOURCE):** its implementation contradicted its evidence (carried-only, where 0x0053417C is the two-ID body). Now corrected; noted in its `unresolved`.
- **M11-009 (EXACT_SOURCE):** its evidence cites only the message read at 0x00533E54, not the guards 0x0053411E/0x0053492C. Too weak as written; noted in its `unresolved` (evidence is frozen).
- **M11-007's `unresolved`:** it also says `BlockWorld.DockingObjectId` is not wired, but Docking.cs binds it now. Stale, outside this job.

## Records

| Record | Status | What stays open |
| --- | --- | --- |
| M11-043 | No settlement | L2 is now built. The census corrections in CENSUS-M11-043.md still need applying at settlement. |
| M12-008 | No settlement | Its other items in `unresolved`. |
| M11-084 | No settlement | A8-15 is built as above; the rest is unchanged. |
| M11-009, M4-009 | Unchanged | The notes above. |
