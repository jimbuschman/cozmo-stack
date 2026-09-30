# B-CORE status

CLAIMED opencode (cozmo-manager) 2026-09-29 13:05

## Scope

Rebuild the connection and device core (M1, M2, M3, M4) from the audit rows. Batches:

1. the NV queue (M3-025, M3-026, M3-027, M3-029, M3-030, M3-031, M3-035, then M3-022 and M3-023);
2. readiness (M1-041, M1-028, M3-033, M3-034);
3. head and lift actions (M4-001, M4-003, M4-016, M4-020, M4-011, M4-025, M4-008, M4-010);
4. the small ones (M2-003, M1-015, M1-024, M1-025, M1-031);
5. R-DEV's own M3/M1/M2 remainder.

Records stay IMPLEMENTATION_GAP; `unresolved` starts "built, awaiting strong verification:".

## Progress log

- 2026-09-29 13:05 CLAIMED.
- 2026-09-29 **Batch 1 (NV queue) done and pushed: dbdc39d.** Read only validates+enqueues; `NvStorage::Update`
  (state 0) pops, sends and arms; a completion sets state 0 only; the deadline uses the synchronised clock
  (`Robot.StoredState`); GetBaseEntryTag's negative branch is `!= 0xC0000000` and positive tags take the size-table
  floor below 0x198000; the header cap is one-shot; the timeout runs the callback only and the final broadcast chunk
  carries the actual result; M3-022 queues the calibration read before SetCameraParams. Verifier PASS (3 queued
  non-blocking nits: one citation fixed, two test-coverage notes). Full suite 1996/1996 (2113/2113 after the rebase).
  Records M3-022/025/026/027/029/030/031/035 remain IMPLEMENTATION_GAP with `unresolved` "built, awaiting strong
  verification".
  - **MISSING (M3-027):** what populates the READ command's data vector `+0xE8` (0x00645386) and its initial value —
    the row names `+0xE8` but not its writer. The command still sends empty data; the test asserts that.
  - **MISSING (M3-023):** what the stack-only `CozmoRobot.StartCamera` must send — the rows settle the engine's
    `EnableColorImages` and the SyncTime `ImageRequest`, but no row defines `StartCamera`. Code unchanged.
- Next: Batch 2 (readiness: M1-041, M1-028, M3-033, M3-034).