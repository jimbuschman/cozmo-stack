# AI-BODY-CUBE-WORLD-STATE-1

## Source path

- G2E 0xA7 RequestLocatedObjectStates and 0xA8 RequestConnectedObjects are empty requests. The native BlockWorld handlers reach the two snapshot builders (0x61C48A–0x61C490, 0x61E6BC, 0x61E918, 0x627A2A–0x627AA8). The C# requests use the game FIFO and the captured original owner. The source requires the external interface for these request subscriptions and has no camera, Freeplay or firmware-state gate.
- Located 0x53 traverses the current-origin map in family, type and object-key order with no pose-state or cube-only filter (0x61E6C0–0x61E78E, 0x61EB78–0x61ECE8). Its row uses the located object's world ID, observation timestamp, family, type, root-relative pose, state and the sign of its own active ID for the connection byte (0x628406–0x628496). The shared pose conversion verifies the actual root owner and flattened parent and serializes WXYZ quaternion order (0x846D38–0x846DEE).
- Connected 0x54 traverses the connected map in family, type and object-key order with no origin or pose dependency (0x61E91C–0x61E9E2, 0x61F078–0x61F130). The row has world ID, family and type (0x62868E–0x6286CC).
- Both lists emit even when empty. The count is a low byte followed by every native row, including when the located count wraps. Located rows are 50 bytes and connected rows are 12 bytes (0x714A10–0x714A7E, 0x714EE2–0x714F2A, 0x715090–0x7150CA, 0x7153A4–0x7153EC).
- Automatic 0x53 follows zombie cleanup, map creation and selection reset at delocalization, before later component callbacks and 0x32. This includes construction of the original robot before publication in the manager (0x510324, 0x6249C4–0x6249EC, 0x510D62–0x510D94). The existing UpdateObjectOrigins body publishes after refreshing the current-origin objects and before forcing configurations (0x6206E0–0x6206EA). The connected snapshot has only the request caller.
- Robot::Broadcast at 0x511DE8 checks the external interface. UI slot +0x18 resolves to the const E2G implementation at 0x662508, with app enqueue at 0x662526 before synchronous local emit at 0x662552. Raw and typed host deliveries share one enqueue. Local probes verify the same order without assuming a stock typed recipient.

## Files and tests

- `ObjectStateReports.cs` holds the immutable report values and typed host request wrappers. The wrappers dispatch the checked recipient on the original robot through the game FIFO. They use a direct host call to that recipient, since each native request has one checked handler.
- `NativeObjectStateReports.cs`, `BlockWorld.cs`, `CozmoRobot.cs`, `NativeDelocalizationWorld.cs` and `NativeDelocalization.cs` supply the original owner and the three checked publication entries. The automatic paths recheck owner lifetime after synchronous local delivery before later owner or configuration work.
- `NativeCubeObservation.cs` shares the native pose conversion with 0x44 and reads the root ID from the flattened result at 0x846D44–0x846D4C. The original root input is copied into a new root whose ID starts at zero (0x4DF65C–0x4DF66A, 0x8459D0–0x845A2E).
- `ObjectStateReportTests.cs` drives both public requests with actual connection-state ingress and explicit located world fixtures, including current and old origins, markerless entries and distinct connected-only and located-only objects. It checks order, raw offsets, immutable typed delivery, repeated empty snapshots, count wrap, app/local order, host callback reentry and old-owner replacement at both automatic tails.
- `M11DelocalizationBuilderTests.cs` and `CoreReviewTests.cs` exercise actual 0x53 before 0x32, including the original carried object in the new origin. The constructor test verifies empty 0x53 before the initial 0x32. The affected run passed 121/121. Independent rebuilt snapshot, delocalization, vision and fidelity tests passed 167/167.

## MISSING

- The higher original LocalizeToObject route into UpdateObjectOrigins is still incomplete under M11-044. The existing body now publishes at its checked tail. No new producer was inferred from camera observations or connection changes.
- The null pose node at 0x846DB6 is an undefined native read. M1-034 skips that row with a visible diagnostic.
- Markerless ingress can still produce a detached native pose under M11-062. These snapshots report the actual stored native pose and its verified root rather than substitute the map key.
- Pose-unknown and mailbox report paths remain outside these snapshots.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and reviewer used the configured default request. Runtime model identity was not observed. Review corrected the flattened-root source, capture and post-publication owner fences, and the process-global error-flag fixture. The reader distinguished the live delocalize path from the absent higher origin-merger ingress.

## Records

- M11-038 now covers both snapshot builders, their live public requests, the live delocalization tail and the checked origin-update body. Pose-unknown, mailbox and other producers still keep the whole record open.
- M11-063 remains open. The snapshot paths do not write its pose-change queue flag.
- M11-044 supplies delocalization and localization with its separate origin-merger gap. M11-065 and M11-066 supply native pose conversion and algebra. M2-022 supplies the UI app/local boundary.
