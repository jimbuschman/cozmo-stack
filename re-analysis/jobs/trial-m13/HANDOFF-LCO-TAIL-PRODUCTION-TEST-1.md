# HANDOFF LCO-TAIL-PRODUCTION-TEST-1 (Claude worker, 2026-11-12)

The inventory is the manager's `CENSUS-M11-043.md` (rows U7, U8) and the QUEUE row. This is a test job: the stack
already produces the tail in the engine's order, so no production code changed.

## The test

`LocalizeCrossOriginTests.LCO_Production_ADelocalizedRobotThatSeesAKnownCubeRelocalizesIntoItsOrigin` is extended. It
checks paths 0 and 1; path 2 returns at the legacy guard.
- **Path 0** is `reach: production`: real frames through the worker, with Delocalize as a direct call. This is labelled.
- **Path 1** is `reach: unit`, because its path start is a direct entry call.

On the relocalizing frame it checks:
- **Order:** the 0x53 comes first (0x006206E6 → BroadcastLocatedObjectStates 0x0061E6C0), then the configuration force
  flag (0x006206EA..0x006206F0).
- **Before X11:** both steps happen while the map is still in the old origin, before UpdateMapOrigins (0x0051599E).
- **The flag:** clear at the 0x53 and set at the force.
- **One 0x53:** exactly one app 0x53 on that frame, the same object as the local emit.
- **Its list:** only the current origin's objects (origin compare 0x0061EC22), which here is the one cube. For that
  cube it checks:
  - the moved object's ID, family and type;
  - origin o1;
  - x, y and z within 1 mm (S9 blends only z);
  - PoseState Known;
  - Connected (1 ^ ([obj+0x40]>>31), 0x0062845C..0x00628462).
- **The checks run:** the implementer forced one to fail, saw it fail, then restored it.

**Results:** the affected classes passed 131/131 for both the implementer and the verifier. For the full gate, see the
commit.

## Records

| Record | Change |
| --- | --- |
| M11-043 | `unresolved`: the stale OLD note on the broadcast and force hook is superseded; the tail is built and tested. L9 is recorded as "not supported: SDK mode (custom objects), operator 2026-10-09"; U1 as a branch our system never produces (IDs start at 1, 0x00847934); the log formats are best effort. L2, the carried test, stays open under CARRIED-ONTOP-BINDING-CENSUS-1. `test`: the LCO test. |
| M11-038 | `unresolved`: 0x53 and 0x54 are built since 0d22601b. The still-true parts of the old line are kept. `// fidelity: M11-038` tags are on PublishLocatedObjectStates and PublishConnectedObjects (ObjectStateReports.cs) and on BroadcastLocatedObjectStates and BroadcastConnectedObjects (NativeObjectStateReports.cs). |

Neither record is settled. M11-043 is blocked by L2 (CARRIED-ONTOP-BINDING-CENSUS-1). M11-038 still has
RobotObservedObject and RobotMarkedObjectPoseUnknown without message types, CheckMailbox (M11-040) and the unread Pack
bodies.

## Verifier: PASS (no blocking findings)

| # | Finding | Class | Resolution |
| --- | --- | --- | --- |
| Q1 | The flag-clear expectation did not cite the clear: BlockConfigurationManager::Update 0x00616DC6..0x00616DCC, called at 0x0062520C on every frame | queued | The citation is in the test comment, with the condition it rests on. |
| Q2 | The comment claimed the pose check shows the CopyWithNewPose copy; it cannot tell that copy from E's pose simply kept | queued | Comment reworded. |
| Q3 | The manifest said "tested through production (paths 0 and 1)" | queued | Now says path 0 is production and path 1 is unit. |
| Q4 | Path 0's production label rests on a direct Delocalize | observation | Already labelled; Robot::Delocalize is reachable in production. |
| O1 | A TryConvert failure drops a row, where the engine always pushes one (existing code; failure only for a null pose, logged M1-034) | observation | For the manager. |
| O2–O4 | The map origin stands in for "before X11" (the order is confirmed in the binary); Ramp/MatPiece taken from the census; the expected values are read from engine-derived world state | observation | — |

One more item: the U1 guard's existing `ReportMissing` text in BlockWorld.cs, "VERIFY key and format are not in the
inventory", differs from the new "never produces" note. It was left unchanged (no production change in this job) and
can be reworded later.
