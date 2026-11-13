# HANDOFF CHARGER-BOUNDING-QUAD-1 (Claude worker, 2026-11-13)

My proposal from RETIRE-LEGACY-DRIVE-API-1; released as job 16. Census (with a GetObstacles section G1-G11) and
implementation reports are worker-local: `.scratch/CHARGER-BOUNDING-QUAD-1-census.md`, `.scratch/CHARGER-BOUNDING-QUAD-1-impl.md`.

## The defect

With a charger located, the stack threw wherever the engine computes a non-cube object's footprint, so on the robot
runtime the native roll was never chosen (and stack-blocks and bring-to-beacon failed the same way). The engine applies
no class check in either place: it calls each object's own GetBoundingQuadXY (slot +0x50) and GetSize (slot +0x2C).
On our system the located classes are cubes, the charger (base ObservableObject::GetBoundingQuadXY 0x0087713A, corners
0x004E9A50, size (96,80,31) at 0x004EA672) and markerless cliffs/obstacles (0x00502C32, GetSizeByType 0x005026EC).

## The change

- **The on-top neighbour search** (NativeConfigurationGeometry.Neighbor; CanInteract 0x0063C654 -> helper 0x0062601C ->
  lambda 0x0062B9FC): candidate quad from the per-class slot +0x50 with padding 0 (0x0062BA7C/0x0062BA86); the per-class
  size read only after the quads intersect (0x0062BA8C..0x0062BAC2; GetDimInParentFrame<Z> 0x00557794); height test
  unchanged. StackBlocks is fixed through it.
- **Bring-to-beacon** (NativeBringBeaconGeometry): obstacle quad via slot +0x50, own pose, padding 10 (predicate
  0x0062750E; FindFreePoseInBeacon passes 10.0f at 0x005E0C16..0x005E0C22).
- **The obstacle list** (PreActionObstacle.For and the Z filter in PreActionValidity.GetObstacles; GetObstacles
  0x00626D44 -> GetLocatedObjectBoundingBoxesXY 0x00626BB4 -> predicate 0x0062C1D4): per-class size (0x0062C1F0); rz
  from the own pose's f32 rotation of the full size vector (0x0062C1FC); centre z re-rooted (0x0062C20C..0x0062C216);
  inclusive exclusion, NaN never excluded (0x0062C220..0x0062C27E); quad from the own pose with the caller's padding
  (0x0062C280..0x0062C294).
- One shared per-class size, `ObservableObject.ObservableSize2C`. `CubeGeometry.SizeOf` is unchanged (it still returns
  44 for markerless types; recorded as open with its callers).
- A class with no slot (only SDK custom objects, which our system cannot create) stays a visible MISSING stand-in that
  the native path refuses.
- Untouched, for MIN-AREA-RECT-FOOTPRINT-1: the PlanarStandIn sites (BlockWorld.cs:3039-3057, BlockConfigurations.cs
  344/349/376-403, DriveActions.cs:599).

## Tests

- New `ChargerBoundingQuadNeighborTests` and `ChargerObstacleListTests` (reach: unit), expected values hand-derived
  from the cited corners and sizes with the arithmetic in comments.
- `HikingBringBeaconCandidatesTests` (two tests) and `PickupChildrenNativeTests` (one test) used the charger as the
  "unavailable footprint" object. The charger is now built, so they use `CustomType00` instead (labelled); the charger
  case is covered by the new unit tests.
- `FreeplayTests.TheWholeStackDrivesOffTheChargerAndPlaysWithTheCubeItSees` (production): **RollBlockOnSide is chosen
  again** with the charger located. The DockWithObject assertion is pinned to its absence by a new gap (below).

## The new gap: the roll stalls after its drive (proposed ROLL-AFTER-DRIVE-STANDALONE-1)

The DriveTo helper's DriveToObject completes with SUCCESS, but the completion never reaches BehaviorHelperComponent
(no OnDelegateSuccess / UpdateActive.Complete), so the roll never starts and the behaviour stays RollBlockOnSide.
Either NativeDriveToHelper.Respond (NativeDriveToHelper.cs:61-67) never runs or UpdateActiveHelper
(BehaviorHelperComponent.cs:413-452) stops updating. Stack defect or rig/scheduler artifact is open. It was
unreachable in this test before this job. Recorded in M8-011; the LocalizeToObject.CouldNotFindHistoricalPose lines
start at t=0 and are a separate rig issue.

## Verifier: FAIL on two visibility items, both fixed; every code hunk supported in the .so

| Finding | Class | Resolution |
| --- | --- | --- |
| F1: M12-036's test field named the whole-stack test as production coverage, but that test can't observe the obstacle list | blocking (visibility) | Removed from M12-036's test field; its unresolved says no production test asserts it yet |
| F2: the after-drive stop was recorded only in a test comment, which misattributed it to the roll's success handler | blocking until recorded | Comment corrected; recorded in M8-011; QUEUE proposal added |
| Q1: the per-class GetSize step has no record of its own | queued | Noted OPEN in M13-025/M13-007/M12-036 (manager) |
| Q2: M13-025's note lists RollBlockHelper 0x005BA136 as a live caller; its filter wasn't re-read and the stack passes families {1,2} | queued | Left for the next batch |

## For the manager

- The per-class GetSize step (slot +0x2C: 0x005577B4, 0x004EA672, 0x00502DD0 -> 0x005026EC) needs a record or an
  extension of M13-025.
- M13-007's authority text says refPose is a copy; it is re-rooted (0x00626048 bl 0x004DF628).
- Release ROLL-AFTER-DRIVE-STANDALONE-1.

## Records

`unresolved`/`test` only on M13-025, M13-007, M12-036 and M8-011. No status changed. No record settled.
