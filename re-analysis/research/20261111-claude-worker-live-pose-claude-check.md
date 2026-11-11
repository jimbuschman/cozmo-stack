# Claude worker: LIVE-OBJECT-POSE-NATIVE-1 and LOCALIZE-PRODUCTION-PATH-TEST-1: Claude check (manager, 2026-11-11)

Merged from `claude-worker` at a4364c63; it is the worker's first handoff. The worker ran its own extractor
census, built the change and had an independent verifier check it. The verifier passed a full Cozmo.Protocol.Tests
run, 8347/8347. The handoffs are HANDOFF-LIVE-OBJECT-POSE-NATIVE-1.md and HANDOFF-LOCALIZE-PRODUCTION-PATH-TEST-1.md.

## LIVE-OBJECT-POSE-NATIVE-1: PASS

**The producer of the flag was misnamed in the guard (a2ccc26a).** A live cube's stored pose picked up the legacy
frame flag in the stack's `ClampPoseToFlat`, which built a new pose. It did not come from
`Pose3d.FromObservedPoseInCamera`.

**I re-disassembled:**
- **The clamp writes in place.** At 0x008774D0 it gets the input pose's own transform, then copies the new rotation
  into it (0x008774D6..0x008774E8). No new pose is made and the parent is untouched.
- **The creation path:** root conversion at 0x00625544, then the clamp on the same pose (0x00625578), then InitPose
  (0x00625582). So the stored pose is parented to the current origin.

**The stack fix:** the clamp result keeps its input's frame context. The numerics are unchanged; their own gap is
queued as CLAMP-FLAT-NUMERICS-1 and recorded on M11-006. Automatic localization S2..S15 now runs on a live robot.
The guard stays for genuinely flat host or offline inputs.

## LOCALIZE-PRODUCTION-PATH-TEST-1: PASS

A reach-production test drives rendered frames through the queued worker. It checks the native localization on
sighting 2:
- S10's frame and vision-only state;
- exactly one 0x45 with that frame, its time and the current origin;
- the robot localized to the cube.

Sighting 1 sends no 0x45. This closes the coverage gap recorded on M11-044.

## Also in this merge (manager)

A shutdown unlink-order test now includes the vision 0x8A erase-all handler with its 0x89/0x8C/0x8F siblings. The
gate on a7154ebf had failed only on that. The worker's five proposals are released to it.

**No record settled.**
- M11-044 keeps the cross-origin branch, LocalizeToMat, OnRobotRelocalized and the NaN order.
- M11-006 keeps the clamp numerics.
