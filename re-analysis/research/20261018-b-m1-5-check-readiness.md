# Opus fix review of B-M1-5 (fd73bc88, 06b86940): readiness side, 2026-10-18

- **Checked:** the verifier that failed B-M1-4 (`research/20261017-b-m1-4-check.md`), resumed and scoped to F1–F6 and
  B-M1-5 items 1–4, 7, 9–12.
- **Verdict: FAIL.** One blocking finding (R1, a race introduced by item 7). F1–F6 fixed. Three queued.
- **Gates:** `fidelity.py --check` passed (694); focused 26/26. Manifest append-only.

## Findings

**R1 — BLOCKING (race, ends in a hang). ConnectAsync can miss its own ready wake-up.**
- `EngineReadiness.cs`:23 `IsReady => EngineState==3 && _hostReady && !StartupFailed`; :66,70 (`_hostReady=false`,
  then `beforeDispatch: () => _hostReady = state==3`); `CozmoRobot.cs`:520-527 (ConnectAsync subscribes after Create,
  checks IsReady, awaits).
- SetEngineState(3) captures the EngineStateChanged subscribers at queue time (`AppDelivery.cs`:14-18); `_hostReady`
  becomes true only at the drain, after the 997 names and End are handed out. If ConnectAsync subscribes between the
  capture and the drain, its handler is not in the captured list, IsReady still reads false, and no later state change
  comes: `await ready.Task` never completes (only a cancellation ends it).
- New in this diff: before, IsReady was EngineState==3, stored before the capture.
- Likelihood low (the window opens ≥ ~120 ms after Create returns; ~3 s with the shipped tree), but a race that hangs
  is blocking.
- Fix directions: ConnectAsync attaches its handlers through BeforeStart (chaining the caller's), or waits on a
  delivered-ready signal set where `_hostReady` is set.

**R2 — QUEUED (contradicted; unreachable with shipped assets). Duplicate name: content.**
- `AnimationLibrary.cs`:607 `if(_clipCache.ContainsKey(clip.Name))return;` keeps the first definition's content;
  `StartupDeliveryRegressionTests.cs`:91 `DuplicateNameKeepsItsFirstDefinitionAndItsSingleListPosition` asserts it.
- Native: CannedAnimationContainer::DefineFromFlatBuf (0x00588322) → GetAnimationWrapper (0x00588350) → AddAnimation
  (0x005883C8; find-first keeps the existing node) → GetAnimation (0x005883D0) → Animation::DefineFromFlatBuf on that
  existing object (0x00588338), which calls Animation::Clear() first (0x00575734); JSON the same (0x0057687A).
- So: list position from the first insertion, **content from the last definition**. The manager clarification
  ("first definition wins … 0x00587E56/0x0058802A") misread 0x0058802A, which only skips node creation; the test's
  expected value came from that clarification.
- Shipped tree: 997 definitions, 997 distinct names, no duplicates. Fix: first position, last content, in stack order.

**R3 — QUEUED (visibility; out of the job's scope).** `Behavior/CliffPickupBehaviors.cs`:313-314 now queues a
RobotCliffEventFinished host view and removed the ReportMissing; the clarification (which landed after the build) said
to keep it MISSING since its host/local split is unsettled. No internal subscribers (only `M7BatchThreeATests.cs`:566),
so nothing observable lost; restore the visible gap or settle the join.

**R4 — QUEUED (note).** `AnimationLibrary.cs`:321 sorts full paths ordinally with the OS separator (`\` vs `/`). With
nested subdirectories the order would differ by platform. The shipped tree has none, and its two roots differ at
"assets" vs "config", so the shipped order is the same. Pin "relative path" (e.g. `/` separators).

## Checked and holds

- **F1 / items 1–2:** a public Create with BeforeStart and the shipped tree reached `state=3 ready=True` in 3.4 s with
  997 names: `0->1 1->2 p0 … p0.69999886 … end 2->3 p1`. Tail names → End → Set3 → final progress 1.0 (0x004ED5FA..
  0x004ED618; poll join at ≥ 1.0, 0x005235A2/0x005235AA). `ShippedStartupPublishesTheWholeListBeforeReadyAndThen
  ProcessesRobotTraffic` sends ConnectionResponse and RobotAvailable through the real transport and tick. Files sorted
  ordinally, loaded serially; clips in flatbuffer table order; failed files never register; malformed binary rolls
  back `_definitionOrder`. `_PROCEDURAL_` excluded (0x005881E6..0x00588222). Independent Python census: 997 names;
  first five and last three match the test. M1-055 decision text appended verbatim.
- **Tools / item 3:** all 16 listed callers plus ControlCheck.cs:526 and M3M4Hw.cs:244 pass
  `EngineStartupResources.Options/OptionsForRoot`. Missing root: ArgumentException naming `--resources`, and the
  engine reports "ResourcesPath is missing…".
- **F2 / item 4:** `CozmoRobot.Create` (:478-488) runs BeforeStart after construction, before StartProduction;
  ConnectAsync forwards it; `ConnectAsyncCallerCanObserveNamesAndEveryStartupTransition` shows the full sequence.
  Nothing publishes before BeforeStart.
- **Item 7:** store first; state-3 gate engine-side; IsReady false in name/End handlers, true in the ready handler;
  failure flag set before its event; Dispose leaves it false; volatile, monotonic. Only R1.
- **Item 9:** M1-024 notes; M2-021 correction; `ConnectAsyncWithoutResourcesFailsStartupBeforeCancellation` with a
  live 10 s token.
- **Item 10:** text = old state name, `$data` = new (EnumToString 0x0077527C, table 0x01033930: Stopped,
  WaitingForUIDevices, LoadingData, Running, UpdatingFirmware).
- **Items 11, 12:** Posted reports the absent output owner once; InitAppAudioInput after UI init.
- **Other effects:** every internal subscriber of the queued Sensors/Cubes/OffTreads/UnexpectedMovement events moved to
  an `*Internal` hook; the only remaining public `+=` is `Sensors.WaitForCliffAsync` (host helper).
