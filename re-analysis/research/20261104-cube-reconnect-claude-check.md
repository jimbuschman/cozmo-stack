# CUBE-RECONNECT-1: Claude check (manager, 2026-11-04)

**Verdict: the reconnect behaviour is the engine's; nothing to change there. One host bug is fixed: offline and test robots used
the user's real block pool file. No record changes status.**

The failure is in robot run 4 (`research/20261103-idle-robot-run-4-claude-check.md`):
- cube 0x241a8eec dropped at 95.8 s;
- its slot was reset at 98.2 s ("Resetting slot 0 ... connection state 4", after the 2.0 s delay);
- the robot reconnected that slot by itself at 101.4 s;
- every later report logged "Ignoring connection ... because expecting connection to 0x0".

## Production path traced in the engine

- **CheckDisconnectedObjects 0x00514924..0x00514A1A:** a slot in state 4 whose disconnect time + 2.0 has passed is cleared in
  place: factory 0, type -1, state 0 (0x005149F0..0x00514A00). **No message is sent to the robot.** The stack matches
  (CubeConnections.cs, CheckDisconnectedObjects).
- **HandleActiveObjectConnectionState 0x00533B3C** passes a connected report to HandleConnectedToObject.
- **HandleConnectedToObject 0x005179C0:** if the slot's factory id is not the reported one (0x005179D6..0x005179DC) it logs
  "Ignoring connection" and returns (0x00517AA8). There is no adoption of an empty slot. The stack matches.
- **BlockFilter::UpdateConnecting 0x0061AA8C..0x0061AB96:** for a pooled cube that no slot holds, the closest advertiser of its
  type is taken only if it is a **different** cube (`cmp r5, r0; beq` at 0x0061AB32..0x0061AB34). The same cube is never
  re-requested. The stack matches.
- **Every caller of Robot::ConnectToObjects** (thunk 0x004B7AE8): BlockFilter::HandleGameEvents (0x00619FB4, 0x0061A0A0,
  0x0061A102, 0x0061A150; the app's messages), Init (0x0061A278), BlockFilter::ConnectToObjects (0x0061A792), UpdateDiscovering
  (0x0061A9BA; only types not yet pooled) and UpdateConnecting (0x0061AB88). None of them re-requests the same cube after a reset.
- **The app** (`unity/scripts/csharp/Cozmo.BlockPool/BlockPoolTracker.cs:236-238`): HandleObjectConnectionState only updates its
  own block data. It does not ask for a reconnect.

**So a cube that stays disconnected for more than 2 s is lost to the original engine too,** until a game event such as the app's
cube-connection screen re-requests it. This is engine behaviour; changing it would be a departure that needs the operator. The
drops themselves are the cube radio's: hardware, not established here.

## Host bug fixed: the pool file

- **The bug:** CreateOffline and CreateForTest robots with no explicit BlockPoolPath used `CozmoRobot.DefaultBlockPoolPath`, which
  is `%LOCALAPPDATA%\cozmo-stack\blockPool.txt`, the operator's real pool. 61 of 73 test constructions give no path. The
  operator's pool held `0xaabbccdd,1`, the test id and type of `ControlTests.ACubeIsTrackedFromItsAdvertisementAndThenItsConnection`.
  How it was written there is not established, and this PC has no pool file.
- **The fix:** such robots now get the no-path case (""): Save at 0x0061B014 writes nothing. An explicit path is still used.
  Tests: `OfflineBlockPoolPathTests`.
- **For the operator:** delete the `0xaabbccdd` line from that file.

## Options for the operator (not built)

If Cozmo should get a dropped cube back by himself, the stack could, as the app's cube screen would, re-request the pooled cube
when its slot is reset. That is a departure from the engine-only behaviour and needs a decision.
