# HANDOFF LIVE-OBJECT-POSE-NATIVE-1 (Claude worker, 2026-11-11)

Census: `.scratch/LIVE-OBJECT-POSE-NATIVE-1-census.md` (extractor; sections 1-8, including the C4 consumer audit).

## What changed

- **The producer was misnamed.** The legacy frame flag on a live cube's stored pose did not come from
  `Pose3d.FromObservedPoseInCamera`. That flag is dropped twice before storage:
  - by the camera child (BlockWorld.cs `CreateObservedCameraChild`, Transform3d ctor);
  - by the root conversion (`GetWithRespectToRoot`, Assign).

  The flag was set by the stack's `ClampPoseToFlat` (`new Pose3d(Mat3, Vec3)`). That clamp runs on every
  localizable cube, because a localizable cube is flat within 5°.
- **The engine clamps in place** (0x00877330..0x008774EC). It writes the new rotation into the input pose's own
  transform at 0x008774D0..0x008774E8, with no SetParent, ctor or translation write.
  - **Creation:** the root conversion at 0x00625544, the clamp on the same pose at 0x00625576..0x00625578, then
    InitPose at 0x00625582 and SetPose's operator= at 0x004EF5A2. So the stored pose is parented to the current origin.
  - **Docking:** a parent-preserving copy (0x00505F02), then the clamp in place (0x00505F22).
- **C1 (built):** the stack clamp now keeps its input's parent and frame context, via
  `Pose3d.KeepInputFrameContextOf` (Geometry.cs) and BlockWorld.cs `ClampPoseToFlat`.
  - The numerics are unchanged.
  - A live stored cube pose is now a native world pose, so automatic LocalizeToObject S2..S15 runs on a live robot,
    including the 0x45.
- **The guard stays** (BlockWorld.cs `NativeSetLocalizedToRouteUsable`), for genuinely flagged host or offline poses
  and the no-current-camera case.
  - Its MISSING text now says so.
  - The comments that named the wrong producer are corrected.
- **Offline engines:** clamped objects now take the native route there too (manager decision). No offline test
  changed outcome.

## Tests

- **`LocalizeToObjectSameOriginTests.M11_044_ALiveSightingOfAConnectedCubeLocalizesThroughTheNativePath`**
  (production). This is the LOCALIZE-PRODUCTION-PATH-TEST-1 test; see its own handoff.
- **`LocalizeToObjectSameOriginTests.M11_044_AFlaggedHostPoseLocalizesByFieldsAndSendsNoUpdate`** (unit). It is the
  guard regression and replaces the removed live fields-only test.
- **`VisionTests.M11_006_TheClampKeepsItsInputsParentAndFrameContext`** (unit), with two cases:
  - a parented, unflagged input stays parented and unflagged;
  - a parentless, flagged input stays flagged.
- **Results:**
  - affected classes (VisionTests, M11RVisBuildTests, M12RFix2StreamATests, RFixBatch3Tests,
    LocalizeToObjectSameOriginTests): 206/206 passed;
  - the verifier's full Cozmo.Protocol.Tests run: 8347/8347 passed;
  - the full gate: see the commit.

## Verifier findings

| Finding | Classification | Resolution |
| --- | --- | --- |
| **V1:** the clamp's arithmetic is not the engine's (f32 acosf, 1e-5f early-outs, strict <, quaternion product, root-relative test), and after C1 it reaches the 0x45 | BLOCKING until recorded | Recorded in M11-006 `unresolved` (the evidence line "the shape this stack already had" is too strong). Queued as CLAMP-FLAT-NUMERICS-1. |
| **Q1:** M11-044 `unresolved` named the wrong producer and said the path doesn't run | queued | Fixed now; the old paragraph is kept, marked superseded. |
| **Q2:** the production test's reach label omitted the sim firmware and the outer calibration | queued | Fixed now. Sim gates only the rolling-shutter shift. |
| **Q3:** the test did not pin which sighting localizes | queued | Fixed now. It pins sighting 2 (0x0050689A..0x00506982, 0x00506AC2..0x00506ACC) and checks that sighting 1 sends no 0x45. |
| **Q4:** the public Pose snapshot cache ignores the flag | queued | No production effect found after C1. Queued as POSE-SNAPSHOT-FLAG-CACHE-1. |
| **Q5:** the guard's MISSING text | queued | Fixed now. |

The verifier confirmed C1 against the binary, and that no flag consumer changes on the live path except the
SetLocalizedTo route (census section 8, re-walked independently). It found no races.

## What stays open

| Item | Owner |
| --- | --- |
| Clamp numerics | CLAMP-FLAT-NUMERICS-1 (M11-006) |
| InitPose's obj+0x50 write (0x004EF578..0x004EF57A), not made for vision-created objects | INITPOSE-OBSERVED-FLAG-1 (owner M11-013 or M11-062, the manager's call) |
| Pre-existing, not caused by C1: DockActions.cs:426 throws where the engine skips the marker | DOCK-MARKER-WRT-SKIP-1 |
| Pre-existing, not caused by C1: LiftGeometry.cs:148's parentless relative observation | LIFT-RELATIVE-OBSERVATION-PARENT-1 |
| Cross-origin, LocalizeToMat, OnRobotRelocalized | LOCALIZE-CROSS-ORIGIN-1, LOCALIZE-TO-MAT-HISTORY-1, M11-044 |

All the new rows are PROPOSED; the manager releases them.

## Records

- **M11-044:** no settlement. Still open: the cross-origin branch, LocalizeToMat, OnRobotRelocalized and the
  LocalizeRobot NaN order. `unresolved` is updated; status and frozen fields are untouched.
- **M11-006:** no settlement. The clamp numerics are a named gap (CLAMP-FLAT-NUMERICS-1). `unresolved` is updated;
  status and evidence are untouched.
- **M11-050:** no change. The production test now checks the stored pose's root (the history drop at
  0x00654E56..0x00654E5C); the rest of M11-050's path is not re-traced here.
