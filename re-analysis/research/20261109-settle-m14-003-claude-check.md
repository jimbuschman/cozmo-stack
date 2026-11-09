# M14-003 (TrackFaceAction) settled EQUIVALENT_IMPLEMENTATION; M11-038 stays open: Claude check (manager, 2026-11-09)

The work is from Codex's TRACK-FACE-LIFECYCLE-SETTLEMENT-1 and AI-BODY-CUBE-WORLD-STATE-1, merged in abcda91f (the
gate passed 8094/8094).

## M14-003: what was re-read in the shipped .so against `NativeTrackFaceRunner.cs`

- **UpdateTracking 0x00565DBC:**
  - A missing face, or one not newer than the stored stamp (+0x118), means no target.
  - The stamp is updated before the pose conversion.
  - The face pose is taken relative to the robot; on failure the code logs `TrackFaceAction.UpdateTracking.PoseOriginError` and sets the error flag.
  - Tilt is atanf((z − 49) / √(x²+y²)), skipped unless x²+y² > 0. Pan is atan2(y,x) + the MAIN yaw.
  - The runner matches line for line. The face position input itself is M14-001's.
- **ITrackAction::CheckIfDone 0x00564F08:**
  - **The tilt clamp:** the maximum-tilt clamp uses +0x94 (0x3F46D3F2).
  - **The small-angle clamp:** it sets |d| to the tolerance with d's sign, measured from the head angle (robot+0x2FC). Pan does the same from the body yaw.
  - **The head move:** it happens when |d| ≥ the tolerance − 1e-5, in mode < 2. Disassembly at 0x005650E2..0x00565104 confirms the arguments: angle; speed |d| / [+0xD0]; accel 0x461C4000; both stack arguments 0. The runner passes the same values.
  - **The pan:**
    - It is gated by |d| ≥ tolerance − 1e-5, because TrackFace leaves the distance out-parameter at 0.
    - If the robot is off its treads (robot+0x355), the result is 0x0300000A.
    - Otherwise it turns in place with speed min(|d| / [+0xD4], 5.2359877) and accel 0x436DFFDB.
    - A failure returns 0x03000016, since the driving animation is off.
- **UpdateSmallAngleClamping 0x0056584C:**
  - No clamp unless +0xE0 is set.
  - With a period > 0 and a pending next time still in the future, there is no clamp.
  - Otherwise the next time is set to now + the RNG range and the clamp applies.
  - The runner's `_nextClamp` logic is equivalent.
- **The callers:**
  - All seven TrackFace sites now construct `NativeTrackFaceRunner`: FaceBehaviors ×2, enrollment, and Meet/Interact ×2. The RequestGameSimple and PeekABoo owners are not built.
  - The wall-clock `Task.Delay(60)` stand-in that the record's old `unresolved` named is gone from production.
- **The lifecycle:**
  - Codex's census covers the companion tag gate, the 0x47 re-Init subscription, the teardown order and completion variant 3 with the signed face ID.
  - Its tests drive the actual action list. I spot-checked the Init order against the handoff ranges.

**Why EQUIVALENT_IMPLEMENTATION:** atanf, atan2f and sqrtf are the host's libm (the M13-026 rule). Everything else
on this path is exact.

**Branches not built:**
- The eye shift (+0xA1), the driving animation (+0xA8), the sound, the duration criteria and the update timeout.
- No TrackFace constructor or setter in the census enables them. They are reachable only from other ITrackAction
  subclasses, which this record does not claim.

**Separate records:**
- M14-001: the face's camera-frame position.
- M12-024: the driving-animation handler.
- The explicit TrackToFace request ingress is the queued AI-BODY-TRACK-FACE-REQUEST-1, not part of this record.

## M11-038: not settled

- The snapshot builders (0x53 / 0x54) and their public requests are built.
- Its own handoff lists what is still open: RobotMarkedObjectPoseUnknown, the CheckMailbox producer, and the other
  producers. Its status stays IMPLEMENTATION_GAP.
- M11-063 is BLOCKWORLD-POSE-QUEUE-SETTLEMENT-1, already proposed to Codex.
