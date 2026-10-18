# Opus fix review #2 of B-M1-6 (84a6430b, 86d287d6): readiness side, 2026-10-18

- **Checked:** the readiness verifier, resumed, scoped to R1–R4 of `20261018-b-m1-5-check-readiness.md` (B-M1-6 items
  1, 3, 4) and readiness/startup/animation-library changes.
- **Verdict: FAIL.** Production fixes for R1, R2, R4 correct; shipped-tree startup works. The two new ConnectAsync ready
  tests are flaky (item 1 required determinism) and the ready/failure handoff lacks a store-load fence.
- **Gates:** `fidelity.py --check` passed (694); focused 20/21 (the :99 test failed). Manifest append-only.

## Findings

**S1 — BLOCKING (test defect; item 1 not met).** `StartupDeliveryRegressionTests.cs`:130
`ConnectAsyncCompletesWhenSubscriptionFallsBetweenReadyCaptureAndDelivery` and :99
`ConnectAsyncDoesNotMissReadyPublishedWhileCreateIsDeliveringAnEarlierReport` assume the 0→1 host report is dispatched
on the Create thread by `DrainAppDelivery()` at the end of StartProduction (`CozmoEngine.cs`:1976). But StartProduction
starts the runner first (:1975), and the runner's first tick can drain 0→1 itself (TickAt finally); the blocking handler
then stops the engine thread. :130 then waits 10 s on `sourceReadyPublished` and fails; :99 spins on the engine it is
blocking. Measured: :130 alone 3/6 failed; :99 1/3 failed and failed in the focused batch (`Assert.True` :117); scratch
repro 7/20 with handler thread ≠ caller, each ending `seen=False state=2 conn=False`. No production effect.

**S2 — BLOCKING under the races rule (theoretical; hang).** Dispatch side `EngineReadiness.cs`:74: `_hostReady=state==3`
(volatile write, release) then `ReadyDeliveredInternal?.Invoke()` (plain read). Waiter side `CozmoRobot.cs`:520,525:
event add (Interlocked CAS, full fence) then volatile read of `_hostReady`. A later load may pass an earlier volatile
store (.NET model; x86/x64 store buffer): the dispatcher reads the event empty while `_hostReady=true` is not yet
visible to the waiter, which reads false. Neither signals; ConnectAsync waits for its token. Failure path has the same
shape (`FailStartup`: volatile `_startupFailed`, then plain capture of EngineStartupFailed; ConnectAsync subscribes, then
checks StartupFailed). Fix: a full fence on the publishing side (Interlocked.Exchange on an int, or MemoryBarrier
between write and invoke), or both sides under one lock. The macroscopic R1 window is closed.

**S3 — QUEUED (unreachable).** Duplicate name whose later JSON definition fails: native has already Clear()ed
(0x0057687A) and keeps a cleared/partial animation; the stack keeps the prior content (null LoadDocument never
Registers). No duplicates in the shipped tree.

**S4 — QUEUED (test coverage).** `RelativeDefinitionOrderIsTheSameForResourcesAndAssetsRoots` has no nested
subdirectory, so it cannot detect a `\` vs `/` difference. The Replace itself is correct.

## Checked and supported

- R1 code: IsReady engine-side; IsReadyDelivered = IsReady && `_hostReady`; ReadyDeliveredInternal read in
  beforeDispatch after `_hostReady`; ConnectAsync subscribe-then-check. The :130 test's reflection only observes and
  fetches the real transport. Only consumer of IsReady/IsReadyDelivered in src is ConnectAsync.
- R2 `AnimationLibrary.cs`:611-616: first position; clip, `_clipFiles`, `_jsonClipFiles` replaced together; native
  0x00588322 → 0x00588350 (AddAnimation 0x005883C8 keeps node, GetAnimation 0x005883D0) → 0x00588338 → Clear 0x00575734;
  JSON 0x0057687A. Rollback correct. Expected 99 from input `z.json`.
- R4: definitionRoot `configAnimations/../../..` is the resources root for both inputs.
- Shipped tree at HEAD: resources root → state 3, ready and delivered, 997 names, tail `… end 2->3 p1`; nested assets
  root the same in 1.2 s.
- AnimationAborted internal. Manifest append-only.
