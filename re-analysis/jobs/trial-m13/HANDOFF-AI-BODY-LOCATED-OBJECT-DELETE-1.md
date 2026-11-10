# AI-BODY-LOCATED-OBJECT-DELETE-1

## Claims and evidence

- DeleteLocatedObjects at 0x0061D038 prunes selected entries before the report loop (0x0061D58E–0x0061D598). Only a selected nonnull current-origin object gets a notification copy (0x0061D1C6–0x0061D1CE). Old-origin and null erasures at 0x0061D3F4/0x0061D4BE do not report.
- In the report loop the confirmer entry is erased at 0x0061D5B8 and BroadcastObjectPoseChanged runs at 0x0061D5C8. Its configuration/map effects at 0x00506F98/0x00506FA8 precede the report constructed at 0x0061D5DC and sent at 0x0061D5E4. The report uses the actual deleted object's u32 ID (tag 0x4C at 0x007250F0, payload at 0x007250F6 and Pack 0x00714396–0x007143A6). The distinct 0x52 report goes before deletion and may repeat the original ID.
- Robot::Broadcast at 0x00511DE8 requires the external interface for both recipients. The UI receiver queues app delivery at 0x00662526 and then sends synchronously to its local signal at 0x00662552. The local value remains a uint for the dock subscriber. IDockAction's 0x005524C6–0x0055250C invalidates the matching target and does not synchronously abort.
- The retained path snapshots owner and origin, checks the selected object's identity around removal, and stops a stale tail after synchronous callbacks. Existing standalone world deletion notifications remain available. The new engine event provides the native typed delivery alongside raw 0x4C.

## Files and tests

- LocatedObjectDeletedReport.cs packs and delivers the report. Vision/BlockWorld.cs binds it to the shared deletion tail after pose effects and before the existing public world event.
- LocatedObjectDeletedTests uses the attached world to check both IDs after all erasures, 0x52 then 0x4C, raw/typed bytes and the real dock subscription's matching-target invalidation. It covers external-interface suppression with standalone delivery, non-current origin erasure and replacement during the first local report. The dock test installs the real subscription directly. The retained dock next-tick result and same-key replacement identity guard have no dedicated regression in this job. SparksStackNativeTests reaches the real camera-miss invalidation and asserts the 0x4C report. Affected 155/155 and final focused 5/5 passed after fixture fixes for app draining outside the engine owner lock and selecting all origins. The batch gate is recorded in QUEUE.md.

## MISSING

- Factory/dev/custom-object producer paths and broader deletion/map geometry remain with their owners. The M11-038 VisionSystem mailbox is separate.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager re-opened the native deletion notification loop and checked the current dock consumer. Review checked current-origin suppression, exact identity removal and original-owner reentry.

## Records

- M11-038 owns the native report and recipient plumbing. M12-034 owns the dock handler that now receives the live report. M11-007, M11-042, M11-062 and M11-088 own the related deletion, geometry and map effects. No whole record settlement is proposed.
