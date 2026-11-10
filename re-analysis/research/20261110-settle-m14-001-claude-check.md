# M14-001 settlement and the legacy face-pose readers: Claude check (manager, 2026-11-10)

From the rescoped TRACKED-FACE-LIBM-PROOF-1: an extractor census of every reader of the legacy face-pose projection,
after Codex's TRACKED-FACE-SETTLEMENT-2 (checked in `20261110-codex-pet-face-localization-batch-claude-check.md`).
**This settlement was made on Fable and is marked for an Opus spot-check.**

## M14-001 → EQUIVALENT_IMPLEMENTATION

**The record's path on the stock queued route is native end to end.** What I re-disassembled:

| Step | Address | Behaviour |
| --- | --- | --- |
| Pose detach | 0x006B37BC..0x006B37EC | Composed with respect to the root, parent cleared (0x006B37E4), stored at face+0xFC |
| Cache gate | 0x004F4956..0x004F496E | Updated only when the count is above 3 or the name is non-empty |
| Same-stamp compare | 0x004F4972..0x004F4A24 | Old and new poses with respect to the robot pose, f32 lengths by inline `vsqrt.f32` (0x004F49B4, 0x004F4A02); the old one is kept when old <= new or unordered (`ble`) |
| Cache store | 0x004F4A26..0x004F4A38 | Pose at +0x14, stamp at +0x20 |
| Getter | 0x004F5AE2..0x004F5B38 | Zero stamp returns 0; a pose in the current origin is copied; otherwise bool set returns 0, bool clear flattens and reparents (`cbz r7`, 0x004F5B0A); returns the stamp |

`NativeFaceCache.cs` (`UpdateNativeLastFace`, `CopyNativeLastFaceTimestamp`) matches each step, including the
unordered case. The geometry, the match by id, the origin reparent (0x004F42B4..0x004F42F2) and the 15000 ms expiry
were checked in the earlier handoffs and checks.

**Why EQUIVALENT_IMPLEMENTATION:** GetIntraEyeDistance calls the phone's libm cosf (0x0087DCD6), M13-023(e). The
cache square roots are not libm: they are inline `vsqrt.f32`, and sqrtf is called only for a NaN result.

**Inventory correction:** M14-001's evidence did not cite the detach, the reparent, the cache or the getter that its
code tags claim. Those were added to its authority and evidence, and M14-faces re-approved under the standing
authorisation.

## The legacy readers are other records' work

None of the readers of the flat `LegacyProjection` is inside M14-001's path. Three are on the canonical production
path and drive robot behaviour:

| Reader | Native pose it should read | Owner (stays open) |
| --- | --- | --- |
| `NativeLastFaceRunner` owner-null branch (Init, Observe, FineTune) | Entry+0xF4 with respect to Robot::GetPose through the gated GetFace (0x0054BD8E, 0x0054C0D4, 0x0054C31E; gate 0x004F59BA) | M13-014 |
| `FacePositionUpdatedStrategy.HandleFaceObserved` | ComputeDistanceBetween(Robot::GetPose, entry+0xF4) (0x0060CC28..0x0060CC32), only on tag 0x46 (0x0060CF70) | M10-003 |
| FistBump: `TurnTowardsLastFacePoseAction` and the last-face stamp | As the first row; the cache stamp with bool = 1 | M13-014 / M7-018 |

The census's code reading suggests the first one is a live defect: the flat projection has no parent, so the
with-respect-to step probably fails and the face is never verified after a turn. Job **FACE-POSE-READERS-NATIVE-1**
confirms that with a test, moves the three readers to the native pose and then removes the projection. The shadowed
readers (the flat candidate selector, DriveToFaceBehavior, the legacy flip side check, manual Interact) are not on
the canonical path.

## Corrections to other records

- **M12-035:** its `unresolved` mapped GetLastObservedFace's bool to "named only". The bool is the world-origin
  requirement (0x004F5AF8, 0x004F5B0A). Corrected in the record; the legacy action still carries the wrong mapping
  and is shadowed.
- **BehaviorDriveToFace** (0x005DAA30) has no record; it is not built in the canonical runtime. Queued.

## Test-harness races fixed (found by the gate on 4564cb56)

The full gate failed 2 of 8309; both pass in isolation and neither is a product defect:
- `NativeMotionIntegrationTests.QualityInputReentryCannotCommitOldFrameToReplacement`: the shared fixture resolved
  the vision system through the current Robot after queueing a frame, while the test's worker callback replaces the
  Robot. The fixture now captures it before queueing.
- `NativeTrackedFaceGeometryTests.QueuedFramesKeepDetachedInputAndOriginOwnedFaceAcrossRelocalization`: a deadlock
  probe waited 2 s for a thread-pool task; under full-suite load the start alone can exceed that. Now 15 s.

**Records settled in this check: 1. Total: 38.**
