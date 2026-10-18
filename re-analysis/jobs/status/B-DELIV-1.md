DONE (3/3 items; 0 new MISSING)

Codex builder, 2026-10-17. Implementation commit: this commit; publication hash appended after push. No hardware run, no status promotion, no acceptance. Existing source gaps (including M1-055 multi-name producer588190 and M6 execution) remain unchanged.

## Primary boundary and root cause

Manager-adopted `research/20261017-b-m2m15-2-check.md` finding1, raised to blocking, and `research/20261014-b-m11-1-check.md` queued app-delivery finding. Primary ARMv7 ranges and call targets reopened in shipped libcozmoEngine.so: Broadcast6625B0..66262A, local emitter662590..6625B0, DeliverToGame661594..6616B2, Direct port buffer65D8B8..65D992, Abort57B3E0..57B426. Preflight is a census/cross-check only, not new implementation authority.

Source path: Abort -> Broadcast -> DeliverToGame counter/serialize/port buffer -> local signal -> robot reliable8D/action callbacks -> finish replacement. Delocalize uses the same app handoff before its synchronous local32 emit. Previously host delegates ran immediately, inside scheduler/tick locks and before replacement state was committed; a host Play recursively aborted the still-old animation, and a host waiting for another lock-taking thread could deadlock.

New path: enqueue a copied host publication at the original app handoff -> unchanged local emit/robot send/state mutation -> capture a fixed app batch at the safe tick/start boundary -> wait out the producing tick/scheduler mutations -> release all acquired locks -> invoke host handlers in FIFO publication order. A handler's new publications remain in the queue for the next boundary; nested drains do not recurse.

## Implementation / complete publisher census

Paths below relative to `cozmo-stack/src/Cozmo.Robot/`.

| Site | New host path | Synchronous work retained |
| --- | --- | --- |
| AppDelivery.cs:8..52 | One engine-owned FIFO, publication-time delegate snapshots, fixed batches, reentrancy guard, disposal clearing | Locks used only to snapshot/fence, never around host invocation |
| AnimationEventReport.cs:21..37; Animation/CozmoAnimations.cs:190..199 | raw5F copied into FIFO | Direct reliable AbortAnimation8D, local5F/action callback, replacement state stores |
| AnimationEventReport.cs:12..18 | raw60 then typed AnimationEvent as adjacent views of one queued/counted publication | Local60 in its original location |
| NativeDelocalization.cs:167..170 | raw32 through same helper | NativeUiDeliveryCount before synchronous local32 and remaining Delocalize stages |
| NurtureRestoration.cs:142..146 | copied raw6D/6E/6F/70 and9A via shared helper | Local hub emit stays synchronous; owning NV/backup state unchanged |
| AnimationAnnouncements.cs:76..85 | typed5D gets an independent name-byte copy, one native count | Local5D synchronous; public Init-time exclusion/internal hook retained |
| AnimationAnnouncements.cs:88..92 | typed End then raw9A remain adjacent views, one counted publication | Local9A stays in DeliverNurture |
| EngineReadiness.cs:60..69 | raw7B then typed state event in one publication | Store-before-enqueue and local7B unchanged |
| EngineReadiness.cs:126..139 | typed progress and stack failure queued | Poll cadence, failed flag/reason/state gate unchanged |
| CozmoEngine.cs:2265..2290 | ConnectionResponse, RobotDisconnected, RobotErrorPassThrough queued | Handshake fields, SyncTime/vision/TracePrinter, serial edge, defaults Post and removal bookkeeping stay synchronous |
| EngineRobotState.cs:112 | immutable RobotStatePublished snapshot queued | Field calculation/publication gate after Robot.Update unchanged |
| CheckedMessageHandlers.cs:13 | immutable CLADVersionMismatch queued | Payload/flags/copy/warnings unchanged |
| EngineLog.cs:54 | BlockPoolData queued with copied value list | Producer info and block-filter local state unchanged |
| Animation/CozmoAnimations.cs:183 | stack-only string Event observer buffered | Native robot Event95 keyframe unchanged; this observer is not claimed as a second native E2G message |
| CozmoEngine.cs:2336..2339 | stack fallback GoToSleepRequested observer buffered | Native factory/action creation/queue and disconnect order unchanged; no new native message |

The three original raw invocation sites are unified; a source search now finds raw EngineToGameMessage invocation only inside the queued closure. All supported Engine app-report event sites above use the same FIFO. Internal AnimationMessages subscribers, InitTimeAnimationAnnouncementsInternal and serial/Needs/constructor hooks remain synchronous. Diagnostic callbacks (logs/NotImplemented/fault reporting), raw device/component observer APIs and general scheduler test/sink hooks are not reclassified as native Broadcast here. This is a bounded app-report repair, not a claim that every delegate in every subsystem has a recovered app contract.

## Drain/lifetime contract and calls accounted for

- CozmoEngine.cs:2052..2060 wraps TickAtCore, so loading/state1/failure early returns also drain after their locks unwind. Existing running tick order is unchanged. StartProduction/StartOffline drain their startup publications; offline Kick drains after its outer tick lock, rather than from the nested TickAt while that lock is still held.
- AppDelivery.cs:29..48 refuses a drain while this thread holds tick/scheduler mutation locks, serializes drains, snapshots the batch first, then waits for tick and scheduler mutation completion. AnimationScheduler.cs:430..431 exposes only the existing mutation gate; CozmoAnimations constructor binds those checks to the real scheduler. Gate acquisition is sequential, never nested during fencing; callbacks hold neither gate nor the app-buffer lock.
- Callback-created publications are not appended to the active batch. Their FIFO position remains after already-published messages. No native wire/local callback was postponed with host delivery. Host callbacks are isolated individually under M1-034.
- Raw payloads, typed names and block lists retain publication-time values. Names given to local subscribers use a different byte array from the host snapshot. End/state/event raw+typed views preserve their pre-existing relative view order and count only once; dispatch itself never increments the native publication counter.
- Broadcast6625CA/CE handoff -> local6625FA remains enqueue-before-local. DeliverToGame6615C4/CA counter stays at publication. Pack/size and Direct65D944 copy are represented by managed retained payloads. Managed lifetime handles native temporaries/destruction. Original phone buffer capacity/overflow/drop machinery is replaced PC plumbing (AGENTS); no C# dropping policy invented. Native best-effort log/error machinery unchanged.
- Dispose clears queued host callbacks. Existing subscriptions/local teardown still run at their source sites; disposed app callbacks do not execute later.
- **Scheduling limit:** dispatch is synchronous on the thread reaching the safe boundary. A blocking host can delay that thread's next tick; it cannot hold the reviewed engine/scheduler locks through dispatch. No native C# thread affinity, callback latency or promise of an independently running app thread is claimed. Offline direct-unit calls without a tick require the explicit safe drain in unit tests; they are not new production owner seams.

## Regression reach / fidelity impact

New tests all in AppDeliveryTests, with per-test reach comments:

- `AnAbortHostCanReplaceTheAnimationWithoutRecursiveDeliveryOrAnIncompleteMutation`: **production** public Play -> actual AbortLocked/robot8D/local5F -> real engine tick -> host Play. Checks replaced completions, exact tag FIFO, immediate local/robot effects, max callback depth1 and one generation's host batch per tick, including an unconditional replacing host.
- `AWaitingHostDoesNotHoldTheSchedulerOrTickLock`: **production**, same real abort; host waits at most2s for another thread that plays an animation and completes an engine tick. No helper-only lock assertion.
- `InterleavedViewsKeepPublicationOrderAndBytesWhileLocalDeliveryStaysSynchronous`: **unit**, direct checked32/60/6D/5D/9A/7B entries and substituted announcement name owner. Checks local work before host consumption, native count+6 once, raw/typed FIFO, raw caller mutation and local name-array mutation isolation.
- `TheStackStringObserverUsesTheSameSafeAppBoundary`: **unit**, manual scheduler clock substitutes the ticker/engine clock; no native string-event contract claimed.
- `DisposalDropsPendingHostCallbacks`: **unit**, direct publication/disposal; no native callback timing claimed for disposal.

Append-only unresolved/test notes on **19 records**: M1-015/024/028/031/041/045/053/055/056, M2-019, M4-035, M5-023/033, M8-020, M11-070, M15-014/034/035/043. No frozen field or classification changes. FIDELITY_GAPS regenerated.

| Record / group | Production regression reach | Unit-only boundary |
| --- | --- | --- |
| M5-023, M8-020, M1-024 | New public Play/real scheduler/tick reentrancy and cross-thread tests above | D5 source-field test remains direct action/dispatch; its assertion now distinguishes synchronous local emit from deferred raw/typed host consumption |
| M1-015/028/041, M15-014 | EngineAppLayerTests connection/removal packet/request tests; changed serial/removal tests now check local source order and then deferred host reports | No new handshake owner substitution |
| M1-031/045 | EngineAppLayerTests.M1_031_CC6_SleepFiresBeforeDisconnectInOneUpdate | Fallback observer is stack plumbing; no native producer claimed |
| M1-053 | EngineAppLayerTests.M1_053_PublicationGateAndOwningComponents and existing publication regressions | None newly introduced |
| M1-055/056 | EngineReadinessTests real one-file startup/failure path still checks states/names/end/progress through ticks | Mixed-publication/name-mutation test substitutes the name owner; successful multi-name startup remains MISSING588190 from B-M1-4 |
| M4-035 | EngineAppLayerTests.M4_010_P14_P27_TheDiscoveryDiagnosticsFollowTheTypeOrder | Value-list copy is buffer implementation plumbing |
| M11-070 | Existing M11DelocalizationB8Tests live RobotState tread-boundary path still runs local recipients | Direct32 app-order assertion in M11DelocalizationBuilderTests is unit, explicitly drains; mixed32 FIFO test also unit. No new missing join; these tests do not independently establish the whole host32 wire path |
| M2-019 | Existing actual packet handlers unchanged | M3M4X18BuilderTests version-report test is direct-dispatch unit and now drains explicitly; not reported as whole transport coverage |
| M15-034/035/043 | Existing production unlock handler regressions retained | M15RestorationCheckedRowsTests direct NV sinks/dispatch/drain and mixed6D FIFO test are unit, explicitly labelled; whole NV transport path is not newly certified |
| M5-033 | Robot keyframe behavior unchanged | Stack string observer test uses manual Advance; no native E2G contract invented |

Changed tests retain their real reach labels. Direct Delocalize/NV/CLAD tests explicitly drain at a safe unit boundary; their original local ordering remains asserted. EngineInitAnnouncementCarryTests' injected state delegates/log observations now precede actual host callbacks; the real EngineReadiness production test still asserts names/end before the API ready notification.

Validation: broad focused **454/454 passed**, then final affected AppDelivery/EngineAppLayer **181/181 passed** including the additional string-observer test. fidelity.py --check passed. Full suite result appended after completion.

Log-key precheck (all changed production files):119 keys,116 EXACT,2 PART,1 ABSENT. ABSENT `AnimationStreamer.BufferFaceToSend` in AnimationScheduler.cs:1656 is **pre-existing and unchanged** (this diff adds only two gate-access lines to that file); it is a best-effort log cleanup, outside this delivery repair. PART keys are existing version classification fragments. No new log keys added; no MISSING in the engine log.

Full pre-commit suite: **5713/5713 passed** (3.7831 minutes). Append-only frozen-field comparison:19 records, only unresolved/test appended. fidelity.py --check passed; git diff --check clean. Normal push gate follows.
