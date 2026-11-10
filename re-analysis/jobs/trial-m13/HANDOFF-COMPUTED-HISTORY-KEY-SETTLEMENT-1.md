# COMPUTED-HISTORY-KEY-SETTLEMENT-1

## Claims and evidence

- ComputeAndInsertStateAt calls the history query at 0x00531BA6, propagates nonzero result with null borrowed state and untouched optional key at 0x00531BAA–0x00531BB4, and inserts or updates by the resolved timestamp at 0x00531BB6–0x00531BCE. The existing node keeps its address and key and receives the updated pose and state at 0x00531BD0–0x00531BF6. Its optional timestamp-key subscript at 0x00531C0E–0x00531C14 inserts zero when absent without repairing the reverse index.
- A new state is stored at 0x00531C22–0x00531C36, with owned pose and payload copy at 0x00532664–0x005326AA. The ELF process-global u32 counter at 0x01059ED0 increments modulo 2^32 at 0x00531C42–0x00531C46. The two independent unique insertions at 0x00531C54 and 0x00531C70 keep existing mapped values (0x005326FE–0x00532746). The optional key receives the incremented value at 0x00531C82. Constructor and Clear at 0x0053088C–0x00530996 clear the local maps and preserve that process sequence.
- GetComputedStateAt matches only an exact timestamp (0x00531CCC–0x00531D06). On hit, borrowed state is written at 0x00531D12 before optional key lookup. Absent key warns at 0x00531D42 and returns failure with the state already selected and key untouched. IsValidKey at 0x0053214C–0x00532186 checks reverse-map membership alone, including zero. The existing native culler at 0x005309D0–0x00530C38 keeps its distinct timestamp and generated-key prefix rules.
- Real marker, face and overhead consumers request keyed history at 0x00654D92, 0x004F42B4 and 0x0067F8A2. The marker compares Camera ID at 0x00654F10–0x00654F18 before key validation. The stock inner camera is ID 0 (0x006B0016 to 0x0085DD20, copied into markers at 0x0087554C–0x0087555C). The outer camera is built with the robot ID at 0x006500DA–0x006500E0 and 0x0085DD96, from Robot+0x10 at 0x0050FC38 and AddRobot(1) at 0x004ED07A–0x004ED07C. The stock marker therefore bypasses 0x00654F24 even when the calibration is shared.
- The live docking and BlockWorld pose readers use the original history's computed state (0x0063C000 and 0x0050CEB4–0x0050CF84). The offline seeded pose facade retains its public behavior without producing a native key or replacing a live full state.

## Files and tests

- NativeRobotStateHistory.cs owns state storage and the indices. VisionSystem.cs and OverheadMapWriter.cs join the live producers. BlockWorld.cs and Docking.cs keep source-owned computed pose lookup separate from offline seeds.
- RobotStateHistoryAdmissionTests checks resolved noninterpolated time, stable state and pose aliases, optional outputs and exact warning, zero/wrap and duplicate maps, cull from live writes, and the offline seed. DriveOffPilotVisionTests checks actual worker marker delivery with a keyed history state. FaceIdentityTests checks stock camera recognition populates a native key, and the overhead history fixture calls the production reader with real history. NativeDockingCorrectionTests injects a conflicting host seed into a real native marker frame and checks the 0x48 distances still come from native history. NativeMotionIntegrationTests uses the source movement override so its intentional shutter-correction IMU still reaches world delivery. The first 62/64 run exposed that fixture dependency. Affected 476/476 passed, final history 29/29, and manager integration 109/109.

## MISSING

- Generic alternate marker camera identity and its equal-ID validation recipient remain open. Other native callers at 0x005667F4/0x00566BA4/0x00566C1E/0x0056768C were outside the checked joins. No supported route was established for the state emplace-false race or a nonlocal counter writer. No unsupported allocator failure or race behavior is inferred.

## Workers

- Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager reopened the native allocator, face/overhead key requests and distinct camera constructors. One behavioral review round separated live lookups from the host seed. Builder raised the initial rendered-marker failures, resolved against the already checked historical gate and existing movement override.

## Records

- M11-044 owns the existing computed-cache and localization join. M11-050 owns the marker handoff. M11-051 owns culling, M11-097 the query, and M11-017 and M14-008 the overhead and face consumers. No whole record settlement is proposed.
