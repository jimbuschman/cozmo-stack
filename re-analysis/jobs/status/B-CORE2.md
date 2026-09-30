# B-CORE2 status

CLAIMED 2026-09-30 06:38 (DeepSeek, window 3)

## Progress log

- 06:38 Claimed. Read the job, the three Opus verification reports and the Codex request.
- 07:2x **Batch 1 DONE and pushed: aeaf977.** On-idle tick: completion only sets state 0; on-idle runs from
  `NVStorageComponent::Update` state 0 and `AddOneShotOnIdleCallback`; streaming opens the next `Robot::Update`.
  Records M1-041, M3-026, M3-030, M3-033 stay IMPLEMENTATION_GAP, "built, awaiting strong verification: B-CORE2
  batch 1 (on-idle tick)". Verifier PASS; fidelity check clean; full suite 2605/2605.
- 08:xx **Batch 2 DONE and pushed: bd12929** (rebased on Codex's `48f756a`). Connection reads: one calibration read (removed the parallel
  `VisionSystem.ReadCalibrationAsync`; the six Conformance tools wait on the engine's read); the Needs read 0x194000
  queued from `HandleMfgId` after the Lab read with a buffered result a late NeedsManager adopts; the calibration log
  order fixed. Records M3-022, M3-033 (and M15-014's note) stay IMPLEMENTATION_GAP. Verifier PASS; full suite
  2610/2610.
- 09:xx **Batch 3 DONE and pushed: 4d960de.** NV sink filled as blobs arrive (timeout keeps them); the 1000-chunk broadcast bound
  with LoopBoundOverflow; the missing logs (TagIsTooSmall/FactoryTagNotFound, Retry/NumRetriesExceeded, ReadOpFailed
  on every negative, ReadSuccess/ReadEntryNotFound/ReadFailed, the enroll NotFound/Fail); ConnectionFaceAlbumResult
  cleared on removal; the M3-035 test drives removal -> ResetDevices. Records M3-025/M3-030/M3-031/M3-034/M3-035 stay
  IMPLEMENTATION_GAP. Verifier PASS (citation-only notes fixed); full suite 2617/2617.
- 10:xx **Batch 4 DONE: <hash>.** Head clip uses Radians operator</> with the 1e-5 IsNear; RescaleRadians keeps the
  vcvt round trip (saturating); the action teardown stops before unlocking, gated on AreAllTracksLockedBy; the lock
  owner is the per-action tag; the timeout is on the engine clock with 0x03000018, tested before CheckIfDone; the
  invented FailedToSend log is gone (Robot::SendMessage's own warning). Collateral: M13-028's 5 s LiftWaitStandIn
  removed (the M4-016 engine-clock timeout covers it), its 5 tests rebuilt. Records M4-001/M4-003/M4-016/M4-020 stay
  IMPLEMENTATION_GAP. Verifier PASS; full suite <n>/<n>.