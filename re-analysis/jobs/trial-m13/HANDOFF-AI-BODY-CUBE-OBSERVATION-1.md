# AI-BODY-CUBE-OBSERVATION-1

## Source path

- The canonical marker result uses computed history and a current-root check (0x654D92, 0x654E56). It constructs a temporary historical camera for marker localization (0x654E8E, 0x513A5E–0x513A8A) and destroys it before handing the marker list to BlockWorld (0x655022, 0x654DF4).
- The existing BlockWorld commit selects the located current-origin object and applies confirmation, carrying and per-origin duplicate rules (0x620C4E–0x620C5A, 0x620EDE–0x621142). It writes the located timestamp and marker times, updates occluders, and broadcasts before setting DidObjectsChange and updating map time (0x621144–0x621214). There is no extra report-level deduplication or +0x74 queue write.
- The report projects the located cube through VisionComponent's retained camera (0x61FEF6–0x61FF14), using its current calibration (0x651704–0x65171C). The robot initializes that camera to the minimum head angle (0x51007C–0x510086, 0x51039A–0x5103B0) and updates it from the accepted or clamped current angle (0x513358–0x513404). The camera stays parented to the neck under actual MAIN (0x510FFC–0x511036).
- The active cube contributes its eight ordered 44 mm corners (0x4E60DA–0x4E610E, 0x4E61FC–0x4E6270, 0x4E4C8C). The native f32 projection keeps every result, including the positive NaN behind-camera values, and uses strict extrema seeded from the first point (0x85E264–0x85E41C, 0x85E48C–0x85E4CE, 0x4E81B4–0x4E825A). Unrelated roots give an empty projection and zero rectangle. Missing calibration gives positive quiet NaNs for projected points (0x85E26A–0x85E274, 0x85E3CA–0x85E418). The original error diagnostic and flag precede publication (0x85E58A–0x85E636).
- Top orientation is the angle of the first marker with the greatest root-space normal dot against +Z (0x4E69B8–0x4E6A66, 0x4E6AB8–0x4E6AC6). The native quaternion operand path ends at the existing host atan2f boundary (0x84AA1C–0x84AAB8). Pose serialization flattens the located object against its own native root, verifies the origin and flattened parent when applicable, and narrows its quaternion in WXYZ order (0x846D38–0x846DEE).
- The 69-byte report includes timestamp, family, type, ID, rectangle, pose, top angle and active byte (0x61FF5E–0x61FF9E, 0x72496E–0x724982, 0x7131E2–0x71325E). The original robot's external-interface gate precedes app enqueue and synchronous local emit (0x511DE8–0x511DF8, 0x662508–0x662552 via UI vtable relocation 0x102FE34). The typed and raw host views share the existing FIFO. Existing local prefix consumers and the separate legacy observation event retain their delivery contracts.

## Files and tests

- `BlockWorld.cs` and `VisionSystem.cs` attach the full report at the canonical cube commit and carry the original owner and removal generation. The vision worker queues a prepared result. The engine consumes it under the same tick lock as firmware state routing, so `Motion.cs` can snapshot the current head angle without taking its lock under FrameGate.
- `NativeCubeObservation.cs` builds the camera-relative rectangle, flattened native pose and top orientation. `RobotObservedObjectReport.cs` packs the 69-byte payload and feeds the existing app FIFO before the local prefix.
- `M3M4ReviewFixTests.cs` drives actual camera packets through marker confirmation, proves app enqueue before the synchronous local prefix, and exercises repeated sightings, interface-off suppression and public callback execution outside FrameGate. Its camera-choice check contrasts finite retained and historical results with the shared projector. Its owner replacement control pauses before result preparation.
- `NativeCubeObservationTests.cs` checks all 69 bytes in a fixed pose fixture. The manager separately ran the original ARM projection at 0x85E264 and rectangle constructor at 0x4E81B4. The front, mixed-depth and zero-depth cases match the managed rectangle bit for bit. The affected run passed 19/19. The manager run passed 188/188 across cube observation, pickup, docking, vision and fidelity.

## MISSING

- Full host report producers for non-cubes and other contexts remain outside the bounded canonical cube report. The legacy world event remains available separately.
- Physical projection and live firmware timing require operator evidence. Existing history and localization producer boundaries remain independent of the report pack.
- The unshipped phone atan2f remains under the existing numerical equivalence boundary.
- For a relative transform that succeeds, the undefined short distortion-vector read at 0x85E2AE and following coefficient accesses is skipped with an M1-034 diagnostic. A null located pose similarly skips the report at the fatal 0x846DB6 read.
- The live camera test does not use an independent retained-camera numeric oracle. Replacement after a confirmed old-owner result has already been prepared was not exercised.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and reviewer used the configured default request. Runtime model identity was not observed. Review corrected the ordinary projection error log and avoided an extra VERIFY. The reader resolved the retained camera owner, default head pose and both native pose diagnostics. The first builder run did not contain the tracked integration edits and was rerun after the changes were present.

## Records

- M11-038 now has the complete scoped 0x44 report for a committed active light-cube observation, from the canonical call through native rectangle and pose, pack, and app/local delivery. Other producer classes, located and connected states, unknown-pose reports and the result mailbox remain open.
- M11-063 remains open. The report call occurs before the normal change stamp and does not write the pose-change queue flag.
- M11-036, M11-037, M11-044 and M11-050 supply upstream history, marker and localization gates with their own coverage. M4-052, M11-065 and M11-066 supply the checked marker-normal and native pose algebra. This job does not settle their broader paths.
