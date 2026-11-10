# HISTORICAL-MARKER-CAMERA-SETTLEMENT-1

## Claims and evidence

- Nonempty native marker lists call ComputeAndInsertStateAt at 0x00654D92. Status 0x06000000 takes the silent marker-only exit, and other failures log history range and drop that handler (0x00654D9A–0x00654DE6). The retained status overload exposes the existing native history result and resolved time. Empty lists continue directly to the world update. Other frame modes continue after a marker-only failure.
- The original compares the computed pose root against the current world allocation (0x00654E56, 0x0051290C–0x00512916), then checks live IMU at the resolved history timestamp (0x00654E60–0x00654E7E). Head and body limits are 10 and 30 degrees/second. The zero-count native checks use ordered greater-than on each rate (0x006562DA–0x00656304, 0x006563FE–0x00656428). The separate world 10/10 gate remains unchanged.
- Historical marker camera comes from the original component's outer calibration (0x00513A66–0x00513A74) and the computed image-time pose and head (0x00513A80–0x00513A8A). The result binds that owner and preserves the actual calibration identity. It retains original frame time for world/dock and the distinct resolved time for rotation.
- The native loop drops timestamp mismatches before its calibration/key branch (0x00654EE0–0x00654F24), with an error and global flag (0x00654FA2–0x00654FD0). Kept markers retain their order. The queued path isolates the diagnostics outside its vision and frame locks, and checks the original owner before further delivery. The direct fixture adapter keeps its existing behavior.
- Same-calibration IsValidKey at 0x00654F24 remains a visible MISSING. The timestamp cache does not provide the native key index. The stock detector's frame calibration can identify its own common input, while the generic per-marker camera carrier is still absent. The code preserves the existing timestamp-compatible marker handoff with no invented key, validity result or invalid-key rejection.

## Files and tests

- Vision/NativeRobotStateHistory.cs exposes marker status/resolved time without changing callers using the existing bool/cache. Vision/VisionSystem.cs adds the captured-owner gates, outer historical camera and diagnostics.
- PickupPyramidNativeExecutionTests and M3M4ReviewFixTests use the actual queued camera worker, source-shaped IMU inputs and the original owner. They cover 20°/s body allowing the marker stage, 31°/s body, 11°/s head and a missing bracket dropping only that stage, face/frame continuation, image-time camera with distinct current state and inner calibration, and silent frame-ID mismatch. Additional supplied boundary fixtures cover distinct root allocations with the same numeric ID, timestamp mismatch and callback-driven owner replacement. RobotStateHistoryAdmissionTests checks the status/time overload. Final 11/11 focused and 262/262 affected passed. The batch gate is recorded in QUEUE.md.

## MISSING

- Complete computed key/index ownership and per-marker calibration carrier remain open under M11-044/M11-097 and this record. The VisionSystem mailbox, world consumers, solve and broader camera geometry remain separate. No whole record settlement or hardware acceptance is proposed.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager re-opened the ARM comparisons, including unordered semantics. Review moved public diagnostics outside the held locks and added original-owner continuation checks.

## Records

- M11-050 owns the supported historical marker stages checked here and remains open for the key/calibration carrier. M11-044/M11-097 own computed history index and lifecycle. M11-036/M11-037/M11-064 own the downstream world/tail, and M11-038 the separate mailbox.
