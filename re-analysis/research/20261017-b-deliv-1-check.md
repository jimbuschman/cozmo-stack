# Opus check of B-DELIV-1 (33b80553), 2026-10-17

- **Checked:** read-only cozmo-verifier (Opus) under AGENTS.md "What we reproduce" and Process step 4. HEAD 0a140c45
  differs only in the status file.
- **Verdict: FAIL.** Two blocking (1, 2), four queued (3–6).
- **Gates:** `fidelity.py --check` exit 0 (694); focused 90/90 and 244/244. Manifest: 19 records, strict prefix appends
  to `unresolved`/`test` only.

## Findings

1. **BLOCKING (host order changed; census misses Broadcast sites).** The job forbids any change to message order and
   requires every inline site. Listed sites now defer to the end-of-tick drain while other public E2G broadcasts still
   run inline under `_tickLock` inside `Handler.ProcessMessages`, so a deferred report published before an inline one
   in the same tick now reaches the host after it.
   - **Case A (production, cliff stop during an animation action):** `Sensors.cs`:895 `ActionList.Cancel(-1)` →
     `ActionQueue.cs`:258 → `TriggerLiftSafeAnimationAction.cs`:119 → `AnimationScheduler.cs`:799/803 `AbortLocked` →
     `CozmoAnimations.cs`:195 queues 0x5F; `Sensors.cs`:896 raises `RobotStopped` inline. Native: ActionList::Cancel at
     0x0053546E precedes Robot::Broadcast(RobotStopped&&) at 0x00535480; Robot::Broadcast 0x00511DE8 → context+4 vtable
     +0x18 → UiMessageHandler Broadcast → DeliverToGame (0x00661594) → PushToGameMessage (0x0065D8C8 lock, 0x0065D944
     memmove). App buffer holds 0x5F first; the stack host now gets RobotStopped first.
   - **Case B (production):** a robot AnimationEvent → `TriggerLiftSafeAnimationAction.cs`:103 queued 0x60; a following
     ObjectTapped raises `Cubes.cs`:778 inline; host sees the tap first.
   - **Missed inline sites** (their own docs call them game broadcasts): `Sensors.cs`:896 RobotStopped, :904
     FallingStarted, :919 FallingStopped; `Cubes.cs`:778 TapBroadcast, :781 MovementBroadcast, :782 UpAxisBroadcast,
     :783 PowerLevelBroadcast (doc :134-136), :796 ConnectionStateBroadcast (doc :786, E2G 0x0D); `NvStorage.cs`:1331/
     :1352 NVStorageOpResultBroadcast (doc :1404-1407).
   - Leaving these out as "raw device/component observer APIs" is an exclusion of API delivery with no recorded
     decision, and they keep the original hazard (host code under `_tickLock`).

2. **BLOCKING (production label on an input production cannot produce).** `AppDeliveryTests.cs`:44-59
   `AWaitingHostDoesNotHoldTheSchedulerOrTickLock` (`reach: production`) has its host start a worker that calls
   `f.Engine.Tick()` (:53). In production the only ticker is `EngineTickRunner` (`CozmoEngine.cs`:1972), the same
   thread running the drain (`TickAt` finally, :2052-2056); `Tick()` is an offline seam. A production host blocking in
   a callback stalls the engine tick. The test does show the drain holds neither `_tickLock` nor `_gate`, and the
   worker's Play is valid; "completes an engine tick" is not. M1-024 cites it as its only production coverage; M5-023
   and M8-020 cite it as production too.

3. **QUEUED (readiness property vs delivery).** `EngineReadiness.cs`:64 stores state 3 before the queued names, End
   and 7B reach the host; `IsReady` (:21) can be true while they wait for the drain. Host order (names → end → 2->3,
   `EngineReadinessTests`:59) holds; ConnectAsync is unaffected. Only a `Create()` host polling `IsReady` sees it.

4. **QUEUED (reports dropped at Dispose, no recorded decision).** `CozmoEngine.cs`:2390 sets `_disposed` first; then
   `DeliverAnimationReportToApp`/`QueueAppDelivery` drop everything, including 0x5F from DestroyRobot's abort-all in
   RemoveRobots (:2400) and the rest of an in-flight batch (`AppDelivery.cs`:43). Before, these were delivered inline.
   Plausibly the same observable result (the app has shut down); record it as a decision.

5. **QUEUED (offline stranding; scheduling note).** With `CozmoRobot.CreateOffline` (autoTick ticker,
   `CozmoAnimations.cs`:637-645), ticker-thread publications wait for the next Kick/Tick, which may never come; a drain
   refused by the CAS or lock check (`AppDelivery.cs`:31-32) has the same effect offline. Production drains every tick.
   A blocking host lengthens the runner's tick period (robot-traffic timing); allowed by the job and pre-existing for
   tick-originated reports; record it as a COMPATIBILITY note.

6. **QUEUED (pre-existing).** `ActionWatcher.cs`:241-245 / `CozmoAnimations.cs`:224: RobotCompletedAction
   (Robot::Broadcast 0x0053FAAA) has no host view, only the local hub.

## Checked and found supported

- Broadcast 0x006625B0..0x0066262A: slot +0x48 at 0x006625CA, call 0x006625CE (r2=-1); local emitter 0x00662590 at
  0x006625FA. Enqueue before local emit, as built.
- DeliverToGame: +0xFC increment 0x006615C4..0x006615CA before Pack/Size (0x006615EE/0x006615F4); destinations via
  +0x28 (0x0066165E..0x00661662). One count per publication, raw/typed counted once.
- PushToGameMessage 0x0065D8B8: lock, memmove, unlock; the stack's publication-time copies match.
- Synchronous effects unchanged: reliable 0x8D and local 0x5F in AbortLocked (`CozmoAnimations.cs`:195-198), local
  0x60/D5, Delocalize local 0x32 and stages, nurture hub, ConnectionResponse subscribers, SyncTime/vision/TracePrinter,
  defaults Post, 7B store before enqueue.
- No recursion (fixed batch, CAS guard); drain fencing takes `_tickLock` then `_gate` sequentially, host code with no
  engine lock; no deadlock found; TickAt finally covers early returns; no double delivery.
- No internal engine subscriber depends on deferred events.
- Tests not circular; bounded waits.

## Per record

| Record | Production tests | Unit-only |
| --- | --- | --- |
| M5-023, M8-020 | AnAbortHost… | AWaitingHost… mislabelled (finding 2); D5…QueuesApp… |
| M1-024 | none valid (finding 2) | n/a |
| M1-055/056 | EngineReadinessTests:53/59 | Interleaved…; EngineInitAnnouncementCarryTests |
| M11-070 | M11DelocalizationB8Tests | DelocalizationQueuesApp…, M11SmallReads…, M11_053 |
| M15-034/035/043 | existing unlock-handler tests | M15RestorationCheckedRowsTests |
| M15-014, M1-015/028/041 | EngineAppLayerTests G_M15_014, CheckedRemoval_* | none |
| M2-019 | none new | M3M4X18 ImplVersion… |
| M4-035 | EngineAppLayerTests M4_010_P14_P27 | none |
| M5-033 | none | TheStackStringObserver… |
| M1-031/045, M1-053 | existing EngineAppLayerTests | none |
