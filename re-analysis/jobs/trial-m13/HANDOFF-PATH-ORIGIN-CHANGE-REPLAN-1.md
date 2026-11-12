# HANDOFF PATH-ORIGIN-CHANGE-REPLAN-1 (Claude worker, 2026-11-12)

The census is `.scratch/PATH-ORIGIN-CHANGE-REPLAN-1-census.md`, sections 1–8. It reads HandlePossibleOriginChanges,
RejiggerTargetsAndReplan and RestartPlannerIfNeeded in full, the ParamsOrigin writers, the planners' parameter use and
the origin pollers.

## What changed

**PathComponent's reaction to an origin change (M12-032)** is now the engine's for native DriveToPose paths, in
`NativePathComponent.cs` and `NativePlannerSet.cs`:

| Step | Address | Stack |
| --- | --- | --- |
| H1: no selected planner (pc+0x20), so return; ExecuteCustomPath clears it, so custom paths never react | 0x00649812..0x00649816; 0x0064B1E6 | `PlannerSelected`; `ExecuteCustomPath` |
| H5: params ≠ current and not in the world (HasSameRootAs 0x0051290C): Abort (ClearPath, status 5), then status 6, then info "Planner"/"PathComponent.Update.Replan.NotPossible" | 0x00649870..0x00649898 | `HandlePossibleOriginChanges` |
| H6: params ≠ current and in the world, so Rejigger | 0x006498D2 | same |
| H7: same origin and DidObjectsChange()==1, so RestartPlannerIfNeeded from GetDriveCenterPose | 0x006498EA; S1 0x00649C28..0x00649C3A | same |
| R1–R8, RejiggerTargetsAndReplan: params origin := current; StopPlanning if +0x47; RejiggeringPlanner info; in-place GetWithRespectTo of every goal (ANDed); StartPlanner from GetDriveCenterPose, then status 1; fallback; warning, then status 0; VERIFY, then status 0. Nothing is sent at rejigger time | 0x006499D0..0x00649B44 | `RejiggerTargetsAndReplan`, `StartPlanner(Pose3d)` |

**What changed in the stack's dispatch:**
- Before, it returned on every native drive, because it tested the wrong field.
- It also had the abort/rejigger condition inverted.

**Production binding:** `OriginOwnerInput` in CozmoRobot.cs. No input had been bound before.

**Threading:**
- The dispatch and `UpdatePlanning` run under the world's FrameGate, the same gate LocalizeToObject holds from S0 to
  S15. So the path tick sees the whole pre-localization or the whole post-localization state.
- The lock order is _tickLock → ActionList gate → FrameGate, as InitNative and CheckIfDone already use.
- The 0x45 still goes out before the rejiggered ClearPath and segments (SendPendingVisionLocalization comes before
  PathComponentTick; ExecutePath only runs on a later update).

**The manager's guard (BlockWorld.LocalizeToObject)** is narrowed:
- It now holds back a cross-origin pair only while a legacy-entry DriveToPose path is in flight.
- Native drives localize and rejigger the engine's way.
- The end-of-branch MISSING is removed.

## The legacy DriveToPose entry: a visible gap

`StartDrivingToPose(int, ...)` is used by DriveToObjectAction (DriveActions.cs:682/740) and ExplorerBehaviors.cs:371.
- It plans from the history main pose, toward goals that ComputeDriveCenterPose has not converted (NativePlannerSet.cs:35).
- Its goals are borrowed from the caller.
- So the engine's drive-center rejigger and restart cannot be applied to it without mixing frames at the lattice's 20 mm
  ForcePlan/RobotPositionError threshold.

What it does now:
- It writes ParamsOrigin the engine's way.
- It aborts on delocalization (H5), as the engine does.
- On H6 and H7 it emits a visible MISSING and leaves the path as is, which is its pre-job behaviour.
- Its fallback keeps its pre-job start.

Follow-up proposed: **LEGACY-DRIVE-TO-POSE-NATIVE-1**.

## Tests

- **`PathOriginChangeReplanTests` (new, `reach: unit`):**
  - H5, native and legacy;
  - H6/R1–R6, with +0x47 clear and set;
  - R7;
  - H7/S1, native, from the drive-center pose;
  - the legacy restart hold (MISSING);
  - the legacy fallback start (pins pre-job stack behaviour, no engine claim);
  - H1, custom path;
  - the legacy rejigger hold.
  - It is in the "SteppedBehavior missing-report statics" collection (verifier T1, a one-off fixture
    NullReferenceException under parallel load).
- **`LocalizeCrossOriginTests.LCO_Production_…(path 0/1/2)`:**
  - **0, no path:** production.
  - **1, native drive:** the localization proceeds and the path rejiggers. The 0x45 is checked to come before ClearPath
    and ExecutePath.
  - **2, legacy drive:** still held by the narrowed guard.
  - Paths 1 and 2 are `reach: unit`, because the path start is a direct entry call.
- **Results:**
  - implementer: 857/857 on the affected classes;
  - verifier: 581/581;
  - full gate: see the commit.

## Verifier: FAIL, FAIL, then PASS

| Round | Finding | Resolution |
| --- | --- | --- |
| 1 | B1: the legacy entry newly reached H7 and restarted from the history pose | Restart moved to GetDriveCenterPose, then superseded by N1 |
| 1 | B2: a race between the vision worker's LocalizeToObject (FrameGate) and the ungated path tick | The dispatch runs under FrameGate; the 0x45 order is tested |
| 1 | Queued: the legacy reason, the M11-044 stale paragraph, the LCO reach labels | Fixed |
| 2 | N1: the legacy entry mixed drive-center and main-pose starts in one plan | Legacy is held on H6/H7 (visible MISSING); the fallback uses its pre-job start; native unchanged |
| 2 | Queued: UpdatePlanning's ungated reads (Q1); the fallback swap before a throw (Q2) | UpdatePlanning runs under FrameGate; the throw is removed |
| 3 | PASS. Queued: T1, the test collection; Q-a, "decision" reworded as a gap | Both fixed |
| 3 | Observation: the legacy fallback test pins stack behaviour by design | Labelled |

## Open

- **LEGACY-DRIVE-TO-POSE-NATIVE-1 (proposed):** rebuild DriveToObjectAction's and Explorer's drives on the native
  DriveToPose entry (drive-center start, converted goals, owned goal copies). Then drop the legacy H6/H7 holds and the
  BlockWorld guard.
- **H2/H3 VERIFYs** (log only): no producer in the stack, so not built.
- **Census U2 hop and planner internals after a replan:** M13-033.

## Records

- **M12-032:** no settlement. It is blocked by the legacy-entry gap, the unbuilt H2/H3 VERIFYs and the remaining P25
  items in its `unresolved`.
- **M11-044:** no settlement. The guard is narrowed but still present for legacy paths; the null-O body and the
  LocalizeRobot NaN order stay open.
