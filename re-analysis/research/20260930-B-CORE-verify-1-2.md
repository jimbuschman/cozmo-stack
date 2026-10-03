# B-CORE batches 1 and 2: Opus verification

- **Date:** 2026-09-30
- **Verifies:** dbdc39d (batch 1, the NV queue) and ac62741 (batch 2, readiness); code at HEAD 6bfaec0
- **Verifier:** Opus `cozmo-verifier`, from the manager session.
- **Result:** FAIL.
  - **Settled:** M3-029 and M1-028.
  - **Not yet, log-only defects:** M3-025, M3-031 and M4-020. The manager's rule is that an invented or missing log
    blocks settling, just as it does elsewhere. M4-020 had just been settled from the batch-3/4 verification and is
    reverted.
  - **Not yet:** M3-026, M3-030, M1-041, M3-035 and M3-022.
  - **Needs extraction:** M3-027, M3-023, M3-033 and M3-034.
- **Where the detail is:** each record's `unresolved`, starting "Opus verification of B-CORE".

## The central defect

**On-idle runs one Update too early.** The NV completion only calls `SetState(0)` (0x006437EE), and `SetState`
(0x00642B0C) runs no callbacks. `ProcessOnIdleCallbacks` runs from exactly two places:
- `Update`'s state-0 path (0x006456EC);
- `AddOneShotOnIdleCallback` (0x00645C32).

`Robot::Update` checks the streamer (0x0051410C..0x0051411E) before `NVStorageComponent::Update` (0x0051416A). So
streaming opens one tick after the queue drains. The stack opens it in the same tick (`NvStorage.cs:591`,
`CozmoEngine.cs:1168/1179`). This affects M3-026, M3-030, M3-033 and M1-041.

## Other defects

- **A parallel calibration read.** `VisionSystem.ReadCalibrationAsync` sends a second 0x80000001 read with a 3 s host
  timeout, from six tool call sites. The engine reads it once.
- **The Needs read is queued late.** The engine queues it synchronously from the mfgId lambda (0x0052E3B2 ->
  0x006943F8). The stack queues it when the FreeplayStack is created.
- **A stale face album after a reconnect** (M3-034).
- **The sink after a timeout.** The engine's vector keeps the blobs already applied.
- **No loop bound.** The engine stops broadcasting at 1000 chunks (0x00643770..0x00643798); the stack has no bound.
- **The READ command's +0xE8 data vector** is not settled (M3-027).
- **The FaceAlbum-read gate** exists only in prose.

## Tests whose expected values don't come from the binary

- `EngineAppLayerTests.cs:1225`: streaming opens on the completing tick.
- `EngineAppLayerTests.cs:849`: the invented log string.
- `M3DeviceTests.cs:1554`: asserts the empty Data guess.
