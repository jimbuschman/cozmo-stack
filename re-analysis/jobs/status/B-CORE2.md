# B-CORE2 status

CLAIMED 2026-09-30 06:38 (DeepSeek, window 3)

## Progress log

- 06:38 Claimed. Read the job, the three Opus verification reports and the Codex request.
- 07:2x **Batch 1 DONE and pushed: aeaf977.** On-idle tick: completion only sets state 0; on-idle runs from
  `NVStorageComponent::Update` state 0 and `AddOneShotOnIdleCallback`; streaming opens the next `Robot::Update`.
  Records M1-041, M3-026, M3-030, M3-033 stay IMPLEMENTATION_GAP, "built, awaiting strong verification: B-CORE2
  batch 1 (on-idle tick)". Verifier PASS; fidelity check clean; full suite 2605/2605.
- 08:xx **Batch 2 DONE and pushed: <hash>.** Connection reads: one calibration read (removed the parallel
  `VisionSystem.ReadCalibrationAsync`; the six Conformance tools wait on the engine's read); the Needs read 0x194000
  queued from `HandleMfgId` after the Lab read with a buffered result a late NeedsManager adopts; the calibration log
  order fixed. Records M3-022, M3-033 (and M15-014's note) stay IMPLEMENTATION_GAP. Verifier PASS; full suite
  2610/2610.