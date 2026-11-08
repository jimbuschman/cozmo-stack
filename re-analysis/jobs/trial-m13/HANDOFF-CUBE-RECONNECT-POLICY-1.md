# CUBE-RECONNECT-POLICY-1

## Read and decision

- The shipped handler dispatches game tag E6 to BlockSelectedMessage at 0x00619EBA,
  E9 to BlockPoolResetMessage at 0x00619FD8, EA to DisconnectFromSpecificCube at
  0x0061A0B8, and EB to ConnectToSpecificCube at 0x00619F5E.
- Unity BlockPoolTracker.cs 141–148 finds the factory ID, saves its ID in robot NV, and
  sends EB with the factory ID and object type. At 0x00619F78–0x00619F84 EB requires a
  zero persistent factory ID with that type. On success it copies the pool and calls
  ConnectToObjects at 0x00619FB4, sends pool data, saves and stamps the connection time.
- Unity BlockPoolTracker.cs 169–190 sends E6 with factory ID, object type and selected.
  Selected true calls AddObjectToPersistentPool at 0x00619ECE and stops at 0x00619ED4
  if it rejects an existing ID or type. Selected false removes the entry.
- At 0x005149F0 the elapsed disconnect check clears the active robot slot alone.
  At 0x00517AA8 a mismatching factory ID is ignored. At 0x0061AB32 the replacement
  path rejects the same factory ID. Neither EB nor E6(true) reconnects this cube.
- The app's BlockPoolPane.cs 146 toggles E6 off and back on. This changes the persistent
  pool twice and can move the cube to a different free slot. EA then EB is a separate
  manual path. The app's EA producer requires an entry in its current LightCubes, and
  its EB producer writes the NV saved ID.
- Synthetic E9 with enable and maintainPersistentPool true reloads the whole persistent
  pool and calls ConnectToObjects at 0x0061A0A0, saves and stamps. The shipped
  BlockPoolTracker.cs 106–116 and DataPersistencePane.cs 70–72 construct E9 with
  maintainPersistentPool false (BlockPoolResetMessage.cs 8–10).

## Approved input and fidelity record

On 2026-11-07 the operator selected the direct request through the existing engine connection routine.
The request supplies the expired runtime-pooled factory ID to Robot::ConnectToObjects
(0x00517150) after the native slot-reset step. It is enabled by default, can be disabled,
and preserves the saved pool. The engine's advertisement, wire and reply handling follow.

Proposed M4-053, COMPATIBILITY_POLICY, live path. The approved M4-control inventory is
frozen. Claude can register the policy with the inventory update at integration.
Existing records and approvals stay as they are in this branch.

## Files and tests

CozmoEngine.cs, CozmoRobot.cs and Cubes.cs carry the option to CubeConnections.cs. The retry selects only the expired runtime-pooled ID and leaves competing or unrelated requests in the native queue. Existing advertisement and wire admission (0x00514A70) and matching reply (0x005179C0) drive the retry.

EngineAppLayerTests.cs drives the persisted test pool, actual transport, clock, reset, unsolicited report, fresh advertisement, SetPropSlot and matching reply. BackupTestResources.cs copies the option. The affected classes passed 465/465 before the final test-only ordering refinement, and the final reconnect tests passed 4/4. Independent review passed after one returned round to cover the enabled early report.

The reader was requested as gpt-6.1-sol medium.
The builder and reviewer use the configured default. Actual backends are unobserved.

## Remaining hardware uncertainty

The approved engine route still requires an advertisement. In the previous idle run,
20261103-204052-M13-IDLE-HW-dfa2e5/messages.jsonl has a single ObjectAvailable for this
factory ID at line 35 (31,485.6688 ms), before its initial connection, and an unsolicited
connected report at line 20690 (101,367.2805 ms), after the reset. No matching advertisement
followed in that capture. It predates this policy. The fake robot establishes recovery
when an advertisement arrives. Recovery for a radio that only sends a connected report
remains unverified.

Batch full gate on 4487b1fa failed (7801/7803, zero skipped). The old stock-detector availability fixture was corrected and its follow-up passed 61/61. The identical SolvePnP oracle mismatch also reproduces on origin/main a9dbec14. The corrected gate ran on 601b4475 after rebasing onto 1771234b. All 7803 tests passed with zero failures or skips. This bounded handoff is READY FOR CLAUDE.