# AI-BODY-OBJECT-POSE-UNKNOWN-1

## Claims and evidence

- ObjectPoseConfirmer::MarkObjectUnknown at 0x00507128 starts with the original object ID and, when propagating, walks upward (0x00507220–0x00507258). It skips a carried upper object in the set while continuing through it (0x00507288–0x0050728E). The report loop at 0x0050739C–0x005073C6 emits the original ID once per distinct collected ID. This is the set size before deletion, independent of which entries DeleteLocatedObjects actually removes at 0x005073F4.
- RobotMarkedObjectPoseUnknown uses tag 0x52 (0x007253CC–0x007253D4) and one little-endian u32 ID (0x007148CC–0x007148E8). Robot::Broadcast at 0x00511DE8 gates both receivers on the external interface. The UI receiver queues the app report at 0x00662526 before synchronous local delivery at 0x00662552. Both happen before world deletion. The app dispatcher retains its raw-before-typed order.
- The native calls are the second eligible miss (0x0050702E), WritePoseHelper state zero (0x0050610A–0x00506116) and terminal Flip with a located object (0x0055F186). They share the retained report. The public/manual Flip terminal now uses that helper. Generic host MarkUnknown, ordinary world deletion and Ground Clear alone are separate.
- The retained boundary captures the original owner and origin around synchronous local callbacks. The Flip callers pass the original owner through the terminal recipient. A replaced owner or changed origin cannot continue deleting or reporting through the successor. Ownerless or interface-disabled standalone deletion can still run without publishing.

## Files and tests

- ObjectPoseUnknownReport.cs adds immutable typed and raw delivery. BlockWorld.cs binds it before deletion. FlipBlockAction.cs and NativeFlipBlockRunner.cs keep the terminal call with the captured owner.
- ObjectPoseUnknownTests checks two reports with the same original ID, app queue admission before each local delivery, local delivery before deletion, raw/typed order, no-interface deletion, absent and located Flip, and origin/owner replacement during delivery.
- SparksStackNativeTests verifies a real camera-miss invalidation reaches the local 0x52 report. M12RVisBuildTests now expects actual deletion instead of a host tombstone for the Flip terminal. The earlier M11 tests cover propagation past a carried middle.
- Broad 244/244, actual-camera plus focused 6/6, and final original-owner/Flip suite 70/70 passed. Three earlier failures came from obsolete Flip tombstone assertions and a replacement fixture outside engine ownership. The batch gate is recorded in QUEUE.md.

## MISSING

- The separate VisionSystem mailbox producer and wider deletion/geometry are outside this job. The final retained Flip guard is checked in source and related integration tests. There is no separate injected Sequence.Update replacement test.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager re-opened the original loop and checked the set-count, payload and ordering. Review corrected the manual captured-null and retained Flip child-update replacement windows.

## Records

- M11-038 owns the reported pose-unknown payload and recipients. Its wider mailbox remains open. M11-007, M11-042 and M13-028 own the invalidation, deletion and complete Flip lifecycle. No record settlement is proposed.
