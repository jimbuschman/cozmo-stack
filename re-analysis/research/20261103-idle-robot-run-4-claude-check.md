# Fourth left-alone robot run: Claude check (manager, 2026-11-03)

Bundle `20261103-204052-M13-IDLE-HW-dfa2e5` (kept by the operator, not in git), commit `3a0a78ff`. That commit is the first robot
run with the engine's overhead-edge detector (M11-101) and marker detector (M11-103/104).

The run lasted 240 s. Startup was ready after 3961 ms, with 0 error-level engine logs, no run errors and no exceptions. The
operator noted nothing odd.

**Verdict: idle 1.0 of 240 s. The marker detector located the charger on real frames, and VisitInterestingEdge took its close
branch 6 times. A cube-connection problem was found (below). No record settled: a hardware run does not raise provenance.**

## Behaviours

| Behaviour | Time | Starts |
| --- | --- | --- |
| VisitInterestingEdge | 90.8 s | 7 |
| FindFaces_socialize | 61.6 s | 3 |
| PounceOnMotion | 39.6 s | 4 |
| LookInPlaceForUnknown | 39.2 s | 4 |
| FirstLookIntro | 4.2 s | |
| ThinkAboutBeacons | 2.8 s | |
| ReactToUnexpectedMovement | 0.7 s | |

There was no ReactToCliff: the run was on the floor.

## Vision on the new detectors

- **Charger located:** `BlockWorld.AddLocatedObject` found the Charger_Basic, ID 2, at (-115.4, 159.8, -2.9) at 122.1 s, with
  `robot.object_located`.
- **No cube seen:** no cube was located by sight. Whether a cube was in view is not recorded.
- **Edge visits:** `GatheringAccurateEdge.Close` then S3 appeared six times, with "moving closer" values of 0.43, -3.07, -0.74,
  -0.50 mm and others, all near zero.
- **No DetectMarkersFailed** (the over-100-quads path).

## Cube connection (finding)

- **Startup:** at 31.3 s the block pool loads `0xaabbccdd,1` beside the real cube `0x241a8eec`. 0xAABBCCDD is a placeholder ID
  used in the stack's tests (ControlTests.cs:487). The operator's `%LOCALAPPDATA%\cozmo-stack\blockPool.txt`
  (CozmoRobot.DefaultBlockPoolPath) appears to contain it. How it got there is not established.
- **Connection drops** (cube 0x241a8eec):
  - It connects at 34.6 s, drops at 44.0 s and reconnects at 44.6 s (connection state 4).
  - It drops again at 95.8 s.
  - From 101.4 s every reconnect logs "Ignoring connection to object 0x241a8eec ... because expecting connection to 0x0 of type
    InvalidObject". The stack never accepts the cube again, and it flaps about 10 times.
- **Next:** trace how the engine clears and restores the expected slot after a disconnect (HandleDisconnectedFromObject, the
  BlockFilter connecting state, ConnectToObjects), compare it with `CubeConnections.cs`, then fix. This is the outbound
  cube-connection lifecycle named in AGENTS.md's current warning.

## Other

- `CubeLightComponent.PlayLightAnim.InvalidObjectID` for the charger ID 2, twice. It may be engine-faithful; not checked.
- 146 MISSING texts reported (run 3: 170).
