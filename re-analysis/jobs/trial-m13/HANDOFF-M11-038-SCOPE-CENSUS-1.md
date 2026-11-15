# HANDOFF M11-038-SCOPE-CENSUS-1 (Claude worker, 2026-11-14)

Job 27. Census (worker-local): `.scratch/M11-038-SCOPE-CENSUS-1-census.md`. Read against the current code and the binary
(capstone/lief; the decomp tree is empty).

## Built and matching the binary (nothing to build)

| Step | Engine | Stack |
| --- | --- | --- |
| 0x53 builder | filter 0x00628406 (ID, ts, family, type, pose with origin, poseState, isConnected); mode 0 = current origin (0x0061E742, 0x0061EC22) | NativeObjectStateReports.cs:7-19 |
| 0x53 Pack | LocatedObjectStates 0x00714EE2 (count u8 = n/52), entry 0x00714A10, PoseStruct3d 0x00797EB6; Size 50/entry | ObjectStateReports.cs:16-27 (tests pin 51 and 151 bytes) |
| 0x54 builder / Pack | 0x0062868E; 0x007153A4 / 0x00715090, 12/entry | NativeObjectStateReports.cs:22-28; ObjectStateReports.cs:30-35 |
| Robot::Broadcast | 0x00511DE8 (null interface -> nothing delivered) | Mood.cs:550, Cubes.cs:835 |
| 0x44 for light cubes | 0x0061FED8; Pack 0x007131E2, 69 bytes | NativeCubeObservation.cs; RobotObservedObjectReport.cs:11-14 |
| CheckMailbox | 0x006B2AD4..0x006B2CC4 | VisionResultQueue.cs:35-56 (untagged) |
| 0x52 | 0x005073B8..C0, Pack 0x007148CC | ObjectPoseUnknownReport.cs; code under M11-007 |

## Contradicted

M11-038's note B-M1-7 says PoseStateChanged is missing a host/local delivery split at 0x0061D5C8. 0x0061D5C8 is
`blx BroadcastObjectPoseChanged` in DeleteLocatedObjects, and no pose-state or pose-changed path sends any host message
(0x00506288..DA, 0x00506F88..B4, 0x00624808). Nothing to build there.

## Remaining, proposed as jobs

- **CHARGER-OBSERVED-OBJECT-REPORT-1:** the engine sends an app 0x44 for the charger (it reaches 0x0061FED8 with
  isActive 1, family 4, top 0); the stack sends none. Hand-derived expected rect and bytes are in the QUEUE row. Waits on
  M11-065's ApplyTo arithmetic.
- **OBSERVED-OBJECT-LOCAL-PAYLOAD-1:** the engine delivers the whole 0x44 synchronously to its local readers, two of which
  read the pose; the stack's local copy has four fields and its pose readers use the deferred, stack-only ObjectObserved
  event (which also reads the live pose rather than the report's snapshot).

## For the manager

1. Is the charger's 0x44 in scope now? (The engine sends it whenever the charger is seen.)
2. Do the stack-only BlockWorld.ObjectObserved and PoseStateChanged events stay as stack API (a COMPATIBILITY_POLICY), or
   are they retired once the local payload is built? This may be the operator's (C# API).
3. Should the 0x52 citation move from M11-038 to M11-007?
4. Citation cleanup: M11-038's tags carry no Pack addresses; CheckMailbox has no tag.

`unresolved` of M11-038 updated (stale notes, the contradiction, the remaining gaps). No status changed. No record settled.
