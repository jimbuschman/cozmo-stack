# BLOCKWORLD-POSE-QUEUE-SETTLEMENT-1

## Claims and evidence

- BlockWorld constructs an empty pose-change list and a clear flag at 0x0061BD7C–0x0061BD8A. UpdateObservedMarkers clears the previous list at 0x00624F78 and sets the flag at 0x00624F7E, before branching on marker count. A successful marker frame runs CheckForUnobservedObjects and the stacked-pose recipient while the flag is set (0x006250CA–0x006250D0). An empty frame skips the latter (0x00625052). Both success branches clear only the flag at 0x006250D8 before the configuration update at 0x0062520C. List entries survive until the next frame or teardown.
- The nonzero AddAndUpdate result goes through 0x006250B6–0x006250C2 and returns at 0x00625220 with the window and entries retained. The next frame clears those entries in its prologue. The wrapper is checked here with the localization result supplied at the M11-044 boundary.
- OnObjectPoseChanged tests the flag at 0x00624814. With the flag set, the ID scan (0x00624820–0x0062483A) precedes the first-state check (0x0062487C). Duplicate and first-state-zero paths log without appending. An eligible node copies the ID, old pose and old state and links at the tail (0x00624884, 0x0062496E–0x006249A8). The retained managed queue owns an independent pose wrapper. Both native and managed clear release their own old-pose copies. The managed gate hides internal list removal order from callers.
- All normal branches call the configuration setter with the object's current state (0x00624888–0x00624892, 0x00616D54–0x00616D7A) before the downstream map recipient at 0x00506FA8. The separate origin-rewrite force setter at 0x006206EA–0x006206F0 sets the configuration force bit. It does not clear the queue or run the update.
- Normal flag-clear cliff ingress goes through the robot message drain before the world update (0x004ED62E before 0x004ED648). The handler computes the pose and adds the cliff (0x005355B4–0x005355C0), reaching the zero-old-state broadcast at 0x00622E4A–0x00622E4E. It skips the pose queue and guard logs while still notifying the configuration recipient. A retained open flag after an earlier failed frame has its own defined behavior.

## Files and tests

- M3M4X16BuilderTests.RenderedFrameKeepsFirstOldPoseOnDuplicateAndFailureUntilNextFrame runs real marker detection and the frame writer through ProcessImage with a supplied camera pose and schedule. It verifies the confirmed first pose at the live configuration owner before injecting the repeated producer calls and failure at the localization result boundary. It checks copied pose coordinates, parent and old state, then recovers via the next empty frame.
- RobotStateHistoryAdmissionTests.DetectedCliffUsesInterpolatedRobotHistoryAndIsPlacedInTheWorld sends actual fake-transport F0 and C0 messages through the history and cliff handler. It checks that the closed flag stays clear and neither guard log nor queue entry is produced.
- Builder focused 2/2 and related 167/167 passed. Independent integration passed 131/131 with public TrackFace, BlockWorld/history, world reports and the existing native configuration force checks. The batch gate is recorded in QUEUE.md.

## MISSING

- The localization failure producer and complete stack-pose consumer geometry are M11-044 and M11-037. M11-064 owns the wider failure chain and play-area diagnostic. Full pose-tree/shared-control lifetime remains M11-077.
- Configuration rebuild outcomes and the broader cliff/map routes remain under their existing records. No hardware run or new engine behavior is claimed.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager re-opened the exact flag and append branches in the shipped ELF and checked the live setter and pose copy. The test distinguishes actual frame handling from the supplied repeated producer calls and localization result.

## Records

- **M11-063.** The implemented writer, flag gates, owned old-pose queue and defined configuration notification are complete on the checked production paths. Proposed for Claude settlement.
- **M11-037, M11-044, M11-064 and M11-077.** These own the separate consumers, failure producer/tail and wider pose lifetimes. M12-049/M13-014 own configuration rebuild behavior. Their statuses remain separate.
