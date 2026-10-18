# Opus fix review #3 of B-M1-7 (add63a25): readiness side, 2026-10-18

- **Checked:** the readiness verifier, resumed, scoped to S1–S4 (B-M1-7 items 1, 2, 6, 7) and readiness/startup hunks.
- **Verdict: PASS with one queued item.**
- **Gates:** both ready tests 35/35 each in separate processes (before: 1/3 and 3/6 failed); StartupDelivery +
  EngineReadiness 18/18; `fidelity.py --check` passed (697). add63a25 manifest append-only.

## Queued

**T1 (test harness, failure path only).** `StartupDeliveryRegressionTests.cs`:112 and :153 install the seam with
`initialHostEntered.Wait(cancel.Token)`, which runs on the engine runner thread inside the tick callback
(`CozmoEngine.cs`:1976-1980); `EngineTickRunner.Run` (~:222) has no try/catch. If the 0→1 handler never ran, the token
timeout (15 s / 30 s) would throw on a background thread and crash the test host instead of failing one test. Bounded;
success path unaffected (the seam is exchanged to null first). Catch in the seam and fail the test instead.

## Checked and supported

- **S1 seam:** `internal Action? BeforeFirstProductionTickForTest` (`CozmoEngine.cs`:1969), invoked via
  `Interlocked.Exchange(ref …, null)?.Invoke()` before the first TickAt; at most once; null in production (only the two
  tests write it); returns nothing, touches no engine state. While the runner is held, the only drain is
  StartProduction's `DrainAppDelivery()` (:1982) (others: :1996 offline start, :2046 offline Kick, :2063 TickAt finally),
  so 0→1 is always delivered on the creating thread. All waits bounded.
- **S2 fences:** ready `EngineReadiness.cs`:74-81 store → `Thread.MemoryBarrier()` → read ReadyDeliveredInternal;
  waiter `CozmoRobot.cs`:520,525 Interlocked add → volatile read. Failure :150-154: reason (plain) → `_startupFailed`
  (volatile release) → MemoryBarrier → subscriber capture; reason visible to a waiter that sees the flag (acquire).
  Store-load window closed on both paths.
- **S3/S6:** M1-055 note appended, matching 0x00576868..0x0057687A.
- **S4/S7:** `a/clip.json` vs `a0.json` discriminates `/` (0x2F) from `\` (0x5C) around `0` (0x30); same order from the
  resources and nested-assets roots.
- **Shipped tree at HEAD:** state 3, ready and delivered, 997 names in 1.2 s, tail `… end 2->3 p1`.
