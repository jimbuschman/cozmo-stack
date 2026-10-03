DONE 2026-09-30 (DeepSeek, window 3) - all five batches built and pushed; verifier PASS on every batch; full suite 2623/2623; every touched record left IMPLEMENTATION_GAP awaiting strong verification.

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
- 10:xx **Batch 4 DONE and pushed: e0f4333** (rebased on Codex's `5a1fa5b`; one full-suite flake on the push gate, green on re-run). Head clip uses Radians operator</> with the 1e-5 IsNear; RescaleRadians keeps the
  vcvt round trip (saturating); the action teardown stops before unlocking, gated on AreAllTracksLockedBy; the lock
  owner is the per-action tag; the timeout is on the engine clock with 0x03000018, tested before CheckIfDone; the
  invented FailedToSend log is gone (Robot::SendMessage's own warning). Collateral: M13-028's 5 s LiftWaitStandIn
  removed (the M4-016 engine-clock timeout covers it), its 5 tests rebuilt. Records M4-001/M4-003/M4-016/M4-020 stay
  IMPLEMENTATION_GAP. Verifier PASS; full suite 2619/2619.
- 11:xx **Batch 5 DONE: caaef86.** Needs tick float (+0x3AC stored, f32 decay), SyncTime-ack f32,
  PlaceObjectOnGround status-bit gate, the IsColor(0) `VERIFY(false): NoneImageEncoding` log and the empty-decode
  rejection, PopFrame live M3-010 test. Records M1-024/M1-041/M2-002/M3-018/M3-010 stay IMPLEMENTATION_GAP. Verifier
  FAILed two circular clock assertions; fixed to test-derived literals (0.36f, 120.0f), re-verified PASS. Full suite
  2623/2623.

## MISSING (all honest, left IMPLEMENTATION_GAP; the first seven are Codex extraction, out of scope)

- M3-027 the READ command's Data vector +0xE8.
- M3-023 `StartCamera`.
- M3-033/M3-034 the FaceAlbum-read gate (VisionSystem::Init 0x6B0658 == 0) and the four missing NV sinks.
- M3-037 the image buffer.
- M1-029 the jsoncpp reader.
- M1-044/M1-045 Robot::~Robot teardown and CreateGoToSleepAnimSequence.
- M4-003 the `ActionQueue::QueueNow` semantics.
- M3-001/M3-018 the codec (StbImageSharp vs libjpeg 9).
- M2-002 the engine keeps PlaceObjectOnGroundAction RUNNING forever if the status gate never opens; the stack
  completes with the dock result (a local bridge, recorded).
- M3-018 the OpenCV `cv::cvtColor` empty-assert text is not in the inventory; the stack rejects an empty decode
  explicitly (recorded).
- M3-010 the PCM seam is `short[]`, so the engine's `encodeMuLaw(float)` NaN warning is unreachable through PopFrame.
- M4-016 the engine's second time gate (precondition delay) is not reproduced (not named in the verification).
- M4-003 the engine's per-action lock-owner tag counter seed is a global with no established value; the stack starts
  at 1 (no wire effect).

## Summary (the DONE line at the top is authoritative)

Commits pushed: 218f940 (claim), aeaf977 (batch 1), bd12929 (batch 2), 4d960de (batch 3), e0f4333 (batch 4),
caaef86 (batch 5). Every record this job touched stays IMPLEMENTATION_GAP with `unresolved` starting
"built, awaiting strong verification: B-CORE2 batch N ...". Verifier PASS on every batch (batch 5 after the
circular-test fix); full suite 2623/2623 at the end. Nothing is settled: an Opus verifier settles later.