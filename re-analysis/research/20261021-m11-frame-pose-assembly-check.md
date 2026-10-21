# M11 frame-pose assembly review

**Codex review; Claude spot-check pending**

Row30, bounded review at6a8d507f. No builder release or settlement. Manager
personally reopened the instructions below in the shipped engine; the report's
larger carrier/processor/consumer claims remain pending where explicitly noted.

Current manifest reread: M11-033 IMPLEMENTATION_GAP, "The image hand-off and
the two VisionSystem::Update overloads", evidence SetNextImage652B04,
Processor651F08, UpdateVisionSystem653D30 and the pre-item8/9 checks;
M11-040 IMPLEMENTATION_GAP, "The VisionComponent mailbox and processor thread",
evidence the same two entry bodies and pre-item9 check. M11-049
IMPLEMENTATION_GAP, "The enabled flag: images are dropped until the NV
calibration read callback has run", evidence65AE80/65AB68/6583A4..658414/6500EE,
explicitly retains an unbuilt post652CFC assembly. M11-017 IMPLEMENTATION_GAP,
"The memory map's vision-derived content: the overhead edges", evidence the
pre-item6 and gap2 checks; its existing unresolved contradicts the earlier
polygon-equivalence claim. None changes status here.

## Confirmed slices

-652C04..652D5E: IMU calculation precedes query. The query passes &time=sp+11C,
  &HistState=sp+120; success calls GetLastStateWithFrameID with current
  robot+2B0 into a different temporarysp+B4, then ignores its result/output
  for assembly. Do not reject a frame on that return or replace the computed
  state with the unused output. The call can still log/set error on failure.
-513AB0..513B66: historical camera copies headcam, toggles the input head
  angle sign bit, rotates about Y, precomposes neck, parents to the computed
  pose, then names using the returned timestamp. The current CameraPose getter
  and flattened host PoseData are not this parented carrier.
-6AF330..6AF396: Set copies time, pose,0x5B RobotState bytes, camera, reparents
  camera to stored robot, copies visibility and nine f32 H words, assigns IMU
  deque, then clears stored robot's parent. Independently resolved ARM veneers
  8CDC2C ->4BED18/53055C ->8CB36C ->4A9E8C/8449BC..49D8; this detaches without
  flattening or changing the transform. No write to member+A0 occurs in Set.
-6533C4..6534FA: lookup empty/max-key gates, lower_bound and its successor,
  f32 distance comparison with strict greater replacement, nine-word copy.
  This is not nearest-bracketing interpolation. Failed lookup does not prevent
  Set; caller zeroed H before lookup and passes its false visibility result.
-6516FC..651784: changed calibration invokes the guarded update/start and LUT
  population with bits3B8EFA35. Tail651778..17E additionally copies byte
  robot+14 to VisionSystem+50 even on unchanged calibration. This tail was
  outside the answer's shortened table range; preserve it in any future full
  calibration build, with its owner/meaning checked rather than inferred.
-651998..651C58: population startsBEDF66F3, uses f32 increments and inclusive
  max3F46D3F2 (literal651D60); obtains robot/camera relative pose, forms columns
  R0/R1/t, multiplies K*M, tests ROI corners0/1, writes nine matrix words to
  keyed entry, stops on failed visibility. No clear appears in this body.
  Called K/ROI/FOV/matrix internals are not freshly approved by this read.
-535C20..535C4C and538B24..538B58: raw imageGyro forwarding;24-byte entries,
  computed timestamp initially0, pop-front only at count20, then push-back.
  Current ImuData's missing computed time and2000ms window differ.
-538C00..538D7E: signed divisor ->f32 scale, same-key timestamp computation,
  previous-key zero-time fill only with an earlier nonzero predecessor,
  separate f32 arithmetic then unsigned VFP conversion agree with the answer.
  Deque assignment6AF4AC..6AF68C confirms element assignment/append/erase
  structure; inner copy helpers still need checking before a full snapshot
  implementation claim.

## Open questions / release boundary

Current QueryFramePose calls the real history but returns the host PoseData;
RobotStateHistory.cs has no full serialized state/camera-parent/H/IMU snapshot.
Do not call this complete because frames reach vision. The image metadata
producers for native+18/+1C, ROI state and VisionSystem's target parent remain
unknown in this answer. No replacement metadata or identity/current-origin
parent is authorized. Full carrier copy/swap, processor ordering, consumer
instructions and helper producers in the rest of the answer remain pending
review; no claim is made that they have all been reread. Those boundaries
prevent release of full frame assembly now. No new research queued under HOLD.

No tests, production changes, inventory edits or hardware work from this read.
