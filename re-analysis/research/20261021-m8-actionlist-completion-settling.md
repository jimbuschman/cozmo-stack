# R-M8-ACTIONLIST — action foundation production comparison

Request: `requests/20261021-m8-actionlist-completion-settling.md`. Examined HEAD c419483d. Independent extraction/read-only comparison, pending manager checking. Coverage table opened before tracing.

| Record / step | Native behavior and instruction citation | Production file/line and caller | Test / reach | Disposition |
| --- | --- | --- | --- | --- |
| 015 admission | Null returns API1; disabled external-tag ranges/BAD_TAG discard through Delete and return0; six positions, position1 Cancel(-1) then NEXT (0x0053D93C..0x0053DB70, OV18 check). No synchronous Init. | `Actions/ActionList.cs`:155–209,258–351; head/lift `Motion.cs`:869–876; driving and dock C5 submit actual runners. | M8ActionCheckedRowsTests external ranges/null/BAD_TAG: unit; ReportOrderingRegressionTests.Start: actual queue/tick. | Supported checked front end; disabled-byte production writer remains M8-012 input gap. Diagnostic wording gaps alone do not block. |
| 015 tick | First-full-state/vision gates before ActionList; signed queue order, first nonzero failure retained, erase only empty queue, watcher after queues, streamer later (0x005140BC,0x0053F580..0x0053F708,0x0051411E). | `CozmoEngine.cs`:1265–1335; `ActionList.cs`:118–145; `ActionQueue.cs`:74–94. Live successor lookup after callbacks. | ReportOrderingRegressionTests real RobotState/queue/tick; M8CompoundCheckedRowsTests.RealEngineTick… uses real tick but scripted concrete child. | Supported queue/tick join; AI/BehaviorManager recipient completeness is separate. |
| 015 runner | Clock stamped first internal tick; f32 timeout equality before Init, pre/post wait includes unordered, Init→Check same tick if initialized, category4 retry resets start/init without re-lock; RUNNING has no completion callbacks (0x00540370..0x0054063A,0x00540D1C..0x00540F9C). | `IActionRunner.cs`:292–420. Actual EngineRobot supplies timer/robotID through watcher (`CozmoEngine.cs`:797). | M8ActionCheckedRowsTests 109–150,196–215: unit branch oracles. Real animation action initialized by queue/tick in report-order regression. | Supported checked runner rows; concrete Init/check/profile/track recipients remain their own obligations. |
| 015 cancel / deletion | Current Cancel then Delete; pending direct Delete preserves N; tag reentry guard; Prep→game snapshot→virtual destructor→null/unlink→E2G→guard erase (0x0053E690..0x0053E702,0x0053F9E4..0x0053FB06). Cancel itself never invokes UpdateInternal callbacks. | `ActionQueue.cs`:177–264; `IActionRunner.cs`:449–546; list218–255. Destroyed tracks stop head/lift/body before unlock and watcher enqueue. | M8ActionCheckedRowsTests snapshot/order/requeue; M8TeardownCheckedRowsTests current-vs-pending and same-tag reentry: unit. D4 production cancellation in ReportOrderingRegressionTests. | Supported mechanics; ownership/profile and full teardown ordering not settled (B1/B4). |
| 015 completion ownership | Game reads subresults then virtual union before destruction (0x00540AA8..0x00540B64); base destructor watcher uses Prep cache, union then descendants (0x00541238,0x00541BB4..0x00541C04). Play concrete getter produces variant1 animation-name string (0x005441DE..0x0054420E,0x0075C544..0x0075C572). | `ActionWatcher.cs`:110–152,227–235; `IActionRunner.cs`:267,432–442; `RobotCompletedAction.cs`:25–53; no concrete union overrides found outside CompoundActions in robot source. | GameSnapshotUsesTheDerivedGetterButWatcherDestructionUsesPrepCache: unit counting getter. Host regression checks completion presence/order, not native union payload. | Generic cache flow supported; concrete union UNBUILT, including animation (B2). |
| 015 delivery | Native Broadcast buffers app first, local emit synchronous (0x006625B0..0x0066262A;0x0065D8B8). Deletion's local5A can queue before Cancel returns; watcher callbacks run later only in Update (0x0054187E..0x005418DE). | `ActionCompletionReport.cs`:7–12 copies results before `AnimationMessages.Deliver`; `AppDelivery.cs`:38–92 FIFO/bounded batch outside mutation lock; animation GameSend bound `CozmoAnimations.cs`:226. | ReportOrderingRegressionTests 40–64: real queue/tick/received D4 yields [5F,completed,stopped], CA/D5 then tap yields [60,tap]. | Supported current ordinary-tick publication order; shutdown host delivery remains explicit limitation, B4. |
| 017 sequential | Hook first; copy suppression before deadline; callback-before-ignore; retry resets state before children; zero-delay may tick exactly one next child, whose terminal path bypasses ignore/retry (0x0054F70C..0x0054F992,0x0054F5A0..0x0054F696). NaN deadline waits. | `CompoundActions.cs`:295–394; captured clock reused through Failure/MoveToNext. | M8CompoundCheckedRowsTests NaN, captured-clock, retry: unit; CompoundActionTests scripted child tests are unit. | Supported checked scheduling, not every concrete caller's async-to-native binding. |
| 017 parallel | Ordered child updates; failure callback before ignore, retry before ignore; unignored failure stops later children; ignored failure explicit Prep then Store (0x0054FAB0..0x0054FBCC); >4 re-updates same child. | `CompoundActions.cs`:416–467; no generic sibling Cancel inserted. | CompletedChildrenGetTheExactAlreadyPreppedCount/LockFailedChild…/InvalidCategory…: unit, finite scripted invalid-category input. | Supported dispatch; no proof that an endlessly invalid shipped child is reachable or that such a hang should be reproduced. |
| 017 store / lifetime / proxy | Child union before Prep, tag-keyed union/type cache, erase shared child only destroys at last release; retained child unlock; teardown retakes tracks then Prep/erase/release, no generic Cancel (0x0054F050..0x0054F130,0x0054EB78..0x0054EC2A,0x0054FC2C..0x0054FC66). | `CompoundActions.cs`:154–241 caches uint only and directly calls WatcherEnding after removal. Proxy158–188. | ListOwnershipIsRemovedBeforeChildDestructor unit; counting union/proxy tests exercise cache flow. | Partial: delayed destruction effects and full payload UNBUILT/UNKNOWN (B2/B3), not solved by managed memory safety. |
| 020 Init / events | Init resets flags, preserves loops/cache, calls streamer including null; tag0 fails03000001; replace CA/CB/D5/5F subscriptions. CA match starts; CB requires start/tag and decrements finite u32; D5 forwards timestamp/event+5;5F full-u32 match no start gate; ended wins (0x00543F20..0x00544146,0x005452F0..0x00545852,0x005441C0..DC). | `Animation/TriggerLiftSafeAnimationAction.cs`:42–111. Real PlayTracked streamer, retained slot replacement. CheckIfDone uses flags, not ticket completion. | Production CA/D5/D4 ordering tests; adopted B-M2M15-2 check. Direct-Init handler tests stay unit. | Checked event slice supported. General cached-pointer/re-Init and plain PlayAnimationAction not represented by this bridge, B2. |
| 020 destruction | Compare streamer Animation pointer with cached pointer, including null==null; on equality SetStreamingAnimation(null,1,true,false), then release5F,D5,CB,CA, base destructor (0x00543DAC..0x00543E6A). | TriggerLiftSafeAnimationAction112–131 instead checks ticket unfinished/generation, calls StopIfCurrent; null emits gap. | D4 regression covers a running-ticket abort; does not cover idle-null or ended-yet-same-pointer. | UNBUILT exact pointer/effect contract; source already known, B2. |
| 021 choice / DAS | Has exactly1 before Get; empty group or selected name returns0300000C, only empty stored group warns; base Init success plus blacklist absence emits robot.play_animation (0x0054432C..D4,0x0054443C..0x00544638). Retry reselects name while preserving cached pointer. | TriggerLiftSafeAnimationAction28–65 uses actual map/library and strict argument; no blacklist/event. | M5X20 OnlyEmptyStoredGroup… checks earlier warning correction; production order fixture resolves real group. | Group handoff built; cached-pointer/lifecycle UNBUILT. DAS log-only gap is queued cleanup by itself. |
| 021 sleep producer | Factory constructs plain TriggerAnimationAction D2,D5,D4, each1,true,mask0,60f,false, sequence inside parallel with lift preset0/tolerance5 (0x0052CEAA..0x0052CFBE; first ctor0x0052CEDE). | `Animation/GoToSleepAnimation.cs`:16–34; `CozmoAnimations.cs`:182,623. SleepTriggerFactory has declaration/read but no production assignment found by source search. | Existing sleep gap behavior/shape fixtures cannot establish native concrete child reach. | Concrete producer UNBUILT. Do not bind the lift-safe subtype blindly; native constructor is plain trigger. |
| 024 cancellation survivors | Current deletion callback precedes pending-front capture (0x0053E6B6→0x0053E6C0); last pending successor captured before local broadcast (0x0053FA86..FAB2). Only latter proves mechanism for new pending survivor. OV111 FeedingEat result-insensitive TransitionToEating; M2-022 subscriber list. | Live list/queue implements mechanism; `SteppedBehavior.Actions.cs`:45 explicitly lacks completion subscription/lifetime owner. RobotLifetime clears manager/BSM before ActionList.Clear. | PendingUnlinks… synthetic callback proves mechanism, not shipped survivor census. | Shipped survival/owner closure remains UNKNOWN; bounded remaining census B5. |
| 015 / 024 Clear | Robot AbortAll→~BehaviorManager→~BSM→Clear→~ActionList (0x00511120..0x00511156). Clear lock-out rejects new NOW/parallel; list destroys left/right/node, queue current Cancel then pending direct-delete (0x0053D916..D93A,0x0053FB5C..FB82,0x0053F96A..F9AC). No watcher Update during Clear. | `RobotLifetime.cs`:77–85; `ActionList.cs`:86–104 ascending managed cleanup; `ActionQueue.cs`:43–60. Manager/BSM native owners explicitly missing. | M8TeardownCheckedRowsTests: unit fake recipients, not complete Robot lifetime/subscriber census. | Bounded mechanics built; multi-slot observable order and native owner/callback joins still gaps, B1/B5. |
| 015 timer handoff | removeInstance at0x004EC6B4 precedes context/RemoveRobots; getInstance allocates zero time fields at0x0084BBA8..BBD6; seconds getter loads+0x10 (0x0084BCA8). | `CozmoEngine.cs`:2459–2484 retains Timer; clock delegates still read it. | No zero-clock teardown consumer test found in inspected regressions. | Removal mismatch confirmed; concrete transitive destructor-reader effects UNKNOWN here, B4. |

## Findings and recommendations

No primary record can be recommended for whole-record settlement at this HEAD. Several core rows are built and should not be rewritten from stale “no ActionList”, “no host completion” or “ticket completes animation action” notes. The remaining gaps below are narrower than all of M8. No production code, manifest, inventory, test or acceptance state was changed; no tests, build, emulator or hardware ran.

Evidence precedence: use OV18's adopted check over the original action-list descriptions, OV20/21 over the older AddAction/name claims, OV56's adopted mechanics over its original incomplete gates, and M2-022/OV111 over early callback guesses. The later B-M2M15-2 and B-M1-5/6 delivery checks establish repaired joins, not all concrete completions. Existing pending-lifetime and missing-recipient statements are retained only where current code still shows the boundary.

### B1 — complete M8-015 is larger than the working queue

The traced ordinary production slice is received full state/vision gate → Robot.Update → ActionList sorted-slot tick → pending promotion → watcher nesting → runner lock/profile/start clock → concrete Init/check → terminal Prep → pre-destruction game snapshot → concrete/base destructor → unlink → app enqueue then synchronous local5A → watcher drain after all queues. Immediate Cancel/NOW/Clear share deletion without an invented UpdateInternal callback. Concrete motor/animation sends and action-specific unions are separately owned.

Known behavior gaps: ActionList.Clear currently visits ascending keys, while the checked native routine visits the actual balanced tree postorder. Internal container choice is free, but when multiple queues survive, destructor traffic and local/API completion order must match or have a recorded scope decision. The current code visibly names this gap at :95–96. A narrow job should first establish which supported multi-slot survivors exist and the corresponding observable sequence; it need not clone a C++ tree for its own sake.

The destructor custom-motion-profile byte+0x58/ClearCustomMotionProfile recipient remains unbound in ActionRunner.WatcherEnding. Existing path-level HasCustomMotionProfile is a different input. Before settling the whole runner claim, map actual live profile owners/callers and account for downstream motion state. ExternalActionsDisabled's producer is a separate M8-012/behavior input obligation; a settable unit property does not establish that production writer. Queue Viz/SDK-status recipients and missing result/name strings should receive a narrow observable-effect/scope disposition; phone presentation cannot silently become a behavior exclusion. Wrong log keys/formatting alone are queued cleanup, including CompoundActions' proxy warning label.

The complex ActionWatcher tree bodies (0x00541974..0x00541B9C,0x00541C08..0x00541F68) were compared to the original instruction report, but their entire changed-production proof was not newly re-verified here. OV18 explicitly did not re-verify D7–D11 and those bodies. Their complete validation remains UNKNOWN in this answer; do not transform original checked-looking table labels into fresh adoption. Known nesting, enqueue versus drain and independent Prep-cache boundaries are supported by the separately adopted cited slices.

### B2 — concrete payload and cached animation identity

Source search of all robot C# found only the base uint getter and compound proxy override. TriggerLiftSafeAnimationAction does not override GetCompletionUnion. Native animation completion is variant1 plus the **current animationName string**; it is neither a four-byte cache nor the trigger/group/debug name. Native runner construction also establishes type-specific default payloads (for type1A, empty animationCompleted string at0x0053FF36..FF56). Current base cache starts0, RobotCompletedAction.PackBody appends U32(cache), and the public ActionCompletionReport accurately calls it NativeCompletionCache. This exposes a partial result, not recovered concrete completionInfo. No claim that this four-byte body is a faithful native union serializer is justified. The current public API misses the native animation name payload; this is an observable API gap.

A bounded job can implement the already recovered animation union/default and carry a copied variant payload through Prep, game snapshot, watcher snapshot and compound cache/proxy, then the typed API. Other supported concrete action completions require their own getter/default ownership and source rows. Do not infer a global union port from animation alone or call a self-roundtrip serializer test an oracle.

The actual event callbacks and result priority now exist in the live bridge. Old ticket-success diagnoses are stale. Destruction is still a ticket/generation approximation rather than cached Animation-pointer equality. A completed clip whose pointer remains current, a never-initialized action with null==null, and a missing-name lookup while another clip plays require the exact known SetStreamingAnimation(null,1,true,false) call effects. Accepted M5 streamer evidence supplies those effects; no new streamer audit is needed. The bridge also performs name lookup/playback rather than retaining the cached Animation pointer across repeated Init. Its loop argument currently uses the request, not the decremented counter; the adopted B-M2M15-2 check says current callers use retries0, so that isolated branch is queued/unreachable for those callers, not a newly proved live defect.

M8-020: retain IMPLEMENTATION_GAP for these concrete payload/cache/destructor joins. M8-021: retain IMPLEMENTATION_GAP for the sleep producer and inherited live lifecycle. The blacklist/event is logging; exact text/event absence alone is not a settlement blocker under current policy. Preserve its source evidence and queue it. General C++ declaration defaults remain UNKNOWN but the explicit supported call-site arguments are known; do not block those callers on reconstructing an unused header default.

### B3 — compound scheduling versus child lifetime

The corrected sequential/parallel tables, NaN waiting, retry order, status propagation, callback-before-ignore and one-immediate-next-child asymmetry are represented. In particular, ignored failure can call the parent's callbacks and later success can call them again; imposing once-only completion would change defined behavior. A parallel failure does not generically Cancel siblings. The finite category>4 test checks dispatch; it establishes no reachable infinite-input policy. Any actual proven hang follows M1-034.

Store/Delete currently removes the child then directly calls WatcherEnding, even when another external native owner would retain it. Defined delayed destructor effects include track stops/unlock, streamer abort and watcher completion timing. Managed reachability of the object alone does not model those semantic releases. Conversely, C++ shared-control internals need not be copied. Next narrow job: identify the actual supported child retention graph/last-release boundaries, account for these effects using suitable C# ownership, and join the real completion payload cache. Arbitrary predicate callable bodies remain owned by their concrete producers. M8-017 stays IMPLEMENTATION_GAP until those live cases are resolved; the checked scheduling core needs no wholesale replacement.

### B4 — timer and shutdown boundary

Fresh native reads confirm ~CozmoEngine removeInstance0x004EC6B4, context release0x004EC702, zero-initializing getInstance0x0084BBA8..BBD6 and seconds load0x0084BCA8. Current Dispose keeps the same nonzero Timer through Robots.RemoveRobots; the mismatch is real. This is an **engine-destruction** lead, not a claim that ordinary robot disconnect resets the timer.

However, the lead's phrase “action-destructor clock reads” is not yet an established complete consumer path. A bounded direct-call scan of exported Action destructors found no direct calls to getInstance/GetCurrentTimeInSeconds; the freshly read PlayAnimationAction destructor itself contains none, and the selected AnimationStreamer.Abort main body has none. This does not exclude inlined or transitive reads through profile/world/behavior/audio/local5A recipients. Their reachable reader and state/API/traffic consequence remain **UNKNOWN**. Finish that narrow transitive closure before prescribing a blanket Timer reset or claiming no effect. A fake destructor that reads a test clock would only test an implementation choice, not establish the native consumer.

Current Dispose also sets _disposed before RemoveRobots; AppDelivery drops queued host callbacks while synchronous local delivery still runs. The adopted B-DELIV-1 check already flags this as a scope/decision obligation. This pass makes no new exclusion. Ordinary-tick FIFO fixes are supported and do not settle shutdown host-report disposition.

### B5 — remaining callback census, not a manufactured survivor

The adopted M2-022 answer identifies local5A recipients: RobotEventHandler<90>, DrivingAnimationHandler, IBehavior ctor subscribers, VisitInterestingEdge and DockingTestSimple. ActionWatcher/Mood callbacks are **not** synchronous5A callbacks: destruction only enqueues them and Clear does not drain. That older mood-at-Clear concern is resolved. The SDK-only FacePlant +C4 dock-lock null read is not a supported survivor path; the OV18 check withdraws it.

OV111 establishes FeedingEat state2 → result-insensitive member callback → TransitionToEating → new PlayAnimationAction; ~Robot AbortAll occurs before either behavior owner is destroyed. Thus cancellation-time requeue is a supported lead. But a requeue caused by deleting the current action happens **before** Cancel loads pending.front at0x0053E6C0, so that same-slot new pending action can still be deleted in that Cancel. A new action appended by the final pending deletion occurs after saved successor reached the sentinel and can survive. New lower-key concurrent slots and QueueNow insertion before an already-captured pending iterator are the other checked mechanisms. FeedingEat's requeue alone is not proof of survivor-at-Clear.

Remaining relevant questions, each explicitly UNKNOWN rather than silently discharged:

| Remaining census item | Address / checked lead | Missing closure |
| --- | --- | --- |
| Which shipped pending-action completion tags still match an acting IBehavior and requeue at the final pending deletion? | six callback PLTs from M8-024 evidence; StartActing0x005BDB40/46, completion0x005BE1EC..224; OV111 state2 member wrapper0x005BF8F4 | Actual tag/queue position, +A0/+A1/+A2/+88 state and successor/slot after that callback at disconnect. |
| FeedingEat at engine/robot teardown | AbortAll0x00511120; OV111 F12/F13/F20 | Whether a suitable pending completion, rather than a current deletion consumed in the pending pass, produces a survivor; listener/cube preconditions. Same R2E drain after cube disconnect is M15-069, not proof for RCM removal before PopData. |
| ExploreLookAround and callable replacement | OV56 chain0x005E2932..0x005E2CD2; invocation0x005BE220 replaces+88 via0x005BDB46 | Selectability/retained-owner gates and RNG/side effects at actual removal; any post-replacement capture use. |
| Remaining synchronous5A recipients | Driving0x004F0742, RobotEventHandler0x005277C6, VisitInterestingEdge0x005E7B5E, DockingTestSimple per M2-022 | Which live slots survive owners, whether they queue, and queue keys/positions. Current driving recipient is bound; IBehavior subscription lifetime is visibly unbound in the owned adapter. |
| Manager/BSM and removal/path callouts | manager Dtor0x005A0D22, BSM0x005A5886; RemoveRobot0x0052F2AC/CC; Path Abort0x00649152/188 | Actual retained behaviors/captures during Clear and any requeue from recipient callbacks. Later path implementation must not be relabelled wholly unread; exact census closure remains missing. |

M8-024 stays RECOVERABLE_GAP. Manager/BSM bodies, concrete activity algorithms and the entire behavior census are not re-extracted here. A small follow-up should start with one concrete supported survivor candidate and trace its tag/slot/owner to Clear, then repeat only for unresolved recipient classes. Known crash/double-delete paths use existing M1-034 skips with record/address; no operator departure is requested.

## Regression reach and proposed tests

Read current M8ActionCheckedRowsTests, M8TeardownCheckedRowsTests, the action portion of M8CompoundCheckedRowsTests, ReportOrderingRegressionTests, and selected CompoundActionTests cache/predicate fixtures. The probe scripts and literal result/bit/sequence assertions are useful independent branch oracles. A fake Init, counting getter or scripted child is a unit input even when the real queue handles it; it cannot prove concrete action wire/payload/lifetime recovery. No circular expectation was found in the inspected assertions. No tests were run.

Preserve the actual received-message tests: the D4 case now asserts host order [5F,ActionCompleted,RobotStopped], and the CA/D5/tap case exercises action subscriptions after queue/tick initialization. Earlier check notes that the completion order was not asserted are superseded. They still do not validate full completion payload. Missing precise tests for a separate builder:

1. Real queue/tick animation start/end, terminal payload name and union variant1 against a native literal oracle, including cancellation before Init (empty/current name as the source requires), missing selected clip and remapped supported name. Check API value copies survive subsequent reuse/disposal.
2. Cache-pointer destruction with completed-yet-current clip, idle never-Init/null==null, different pointer, and retry re-Init when reachable. Assert ordered streamer abort/local5F/5A/watcher effects; do not use ticket completion as the expected native predicate.
3. Three-child sequential zero-delay case with immediate second failure/retry and a nontrivial ignore predicate: no predicate/retry on that immediate path; third stays unticked. Existing branch fixtures should remain units until real concrete children produce those results.
4. Retain a supported compound child beyond list erase, then release its final semantic owner: verify exact stop/unlock/abort/watcher timing and union preservation. A CLR reference held by the test is insufficient unless it corresponds to the recovered owner graph.
5. Actual Robot removal with a checked shipped callback/tag/queue state that survives Cancel, observing Clear effects without any watcher drain. Synthetic GameSend requeues remain mechanism-only units. Include current-deletion requeue consumed in pending traversal as the negative control.
6. Multi-slot Clear observable destructor/publication sequence from a sourced insertion history; never assert ascending order just because SortedDictionary uses it.
7. Timer-removal regression only after a real transitive native reader is identified: engine Dispose at nonzero time versus ordinary robot disconnect, with its downstream consequence. Keep shutdown API drop as an explicit separate disposition, not an invented expected “no callbacks”.

## Record decisions and handoffs

| Record | Recommendation | Bounded next work |
| --- | --- | --- |
| M8-015 | Core queue/runner/cancel/delivery rows supported; whole record stays gap. | B1/B2/B4; watcher validation limits explicit, supported profile input and teardown order/clock closure. |
| M8-017 | Scheduling core supported; whole record stays gap. | B3 and real typed completion cache; supported external child ownership effects. |
| M8-020 | Events/result/ordinary host order now built; whole record stays gap. | Concrete completion payload/default, cached pointer/re-Init/destructor joins B2. |
| M8-021 | Mapping/choice/error handoff partial; stays gap. | Plain trigger factory producer for sleep0x0052CEDE and inherited B2 lifecycle. DAS log cleanup queued. |
| M8-024 | Keep RECOVERABLE_GAP. | B5; a requeue or synthetic survivor is not shipped survivor closure. |

M5 owns accepted streamer output/abort/choice; M5-038 owns factory shape, M8-021 its concrete child producer, M1-045 submission. M8-001/011/012/016/023 own behavior/helper/manager lifecycle and native retained owners; this pass only identifies missing joins. M10-007 and M12/M13 own detector/movement/world action bodies, not the generic runtime. M2-022/023 and M8-027 own shared subscription ordering/retirement; no alternate signal mechanism is invented. All statuses remain unchanged. These are source-backed slices plus explicit UNKNOWNs, not a statement that all supported actions have been fully recovered.

## Current primary-record snapshot

Quoted from the current manifest at examined HEAD; no status changes proposed by these quotations.

### M8-015

Title: "ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "0x0053E6A8..0x0053E6E0; 0x005409E2..0x00540A40; 0x00541146..0x00541238; 0x0053FAB2; Current Cancel then delete; pending direct deletion preserves existing result. Cancelled0x02000000 except NotStarted0x02000001; stop before unlock, watcher then broadcast. Existing B-ACTIONS candidate must be checked for full teardown effects and concrete descendants.",
  "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
  "P3/P4 full ActionList/ActionQueue/IAction lifecycle: ActionList 0x0053F580..0x0053F5F2, ActionQueue 0x0053F5F4..0x0053F70C, IActionRunner 0x00540370..0x0054063E, clock/timeout 0x00540D4A..0x00540E80, CheckIfDone 0x005549B8..0x005549F4 (20261006-M1M2-missing-triage.md P3/P4).",
  "re-analysis/research/20261012-m8-play-animation-actions.md rows C2..C7, L14..L23, D5, S2..S4 and re-analysis/research/20261012-m8-play-animation-actions-check.md (the +0x57 gate on L17/L18/L20, the DeleteActionAndIter guard set at queue+0x10, ActionWatcher Start/EndUpdating 0x00540386/0x0054057E/0x00540630, the UpdateInternal status string 0x00540D46..0x00540F1C).",
  "re-analysis/research/overnight/20261014-ov18-m8-015-check.md: L1-L9 0x00540370..0x0054063A, 0x00540D1C..0x00540F9C, 0x00540750..0x005407C0, 0x005409DC..0x00540A44; Q1-Q7 0x0053F580..0x0053F708, 0x0053DCE4..0x0053DD92, 0x0053E24C..0x0053E2F6, 0x0053DF2C..0x0053DFC8, 0x0053DE10..0x0053DE5C, 0x0053E690..0x0053E700; D1-D6, D12 0x0053F9E4..0x0053FB06, 0x00541084..0x00541286, 0x00541BB4..0x00541C04, 0x0054187E..0x005418DE; T2 List Clear 0x0053D916..0x0053D93A, 0x0053F938..0x0053F950, 0x0053FB5C..0x0053FB82; QueueAction front end 0x0053D93C..0x0053DAC8; position table 0x0053DA24..0x0053DB70.",
  "re-analysis/research/overnight/20261015-ov56-58-check.md: teardown mechanics from OV56: ~Robot order 0x005110EE..0x00511156; AbortAll 0x00511952..0x0051198C (0x0063BE2A..0x0063BE36, 0x00517DFE..0x00517E0A); ActionList::Cancel clearing gate and slot order 0x0053DE18..0x0053DE54; ActionQueue::Cancel 0x0053E69A..0x0053E6FA (early exit 0x0053E6E4); QueueNow/QueueAtEnd 0x0053E25A..0x0053E36E; DeleteActionAndIter 0x0053F9E4..0x0053FAB2 (re-entrancy set, INTERRUPTED/no-interface gates); result codes 0x0053FDBA..0x0053FDD4, 0x005409E2..0x00540A40; Clear lock-out 0x0053D91A..0x0053D938, 0x0053DCEA..0x0053DD00, 0x0053DCAA, 0x0053DF40, 0x0053F93C, 0x0053F970..0x0053F9AC; ~ActionList double Clear 0x0053D8F6..0x0053D90E; IBehavior StartActing reject-delete 0x005BDC04, Init failure 0x005BCCC6..0x005BCD06."
]
```

### M8-017

Title: "The compound action runtime: ICompoundAction, CompoundActionSequential and CompoundActionParallel (construction, AddAction naming, update/result tables, retry, completion store, teardown), as used by the go-to-sleep sequence and other compounds"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "0x0052CEA2..0x0052CFC0; Parallel/sequential child execution, ignore-failure predicate and completion/track interactions remain higher framework obligations; M1 owns only factory handoff and NOW/retries0 submission.",
  "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
  "re-analysis/research/20261012-m8-play-animation-actions-check.md addition 4..5: AddAction(IActionRunner*, bool, bool) 0x0054EC7C -> AddAction(function, bool) 0x0054ED90 (empty predicate; child inherits +0x57; compound name appended); the trailing boolean is never loaded.",
  "re-analysis/research/overnight/20261014-ov20-21-check.md: C1 0x0052CECA..0x0052CFA2; C2 0x0054E990..0x0054EA86; C3 0x0053FDB8..0x0053FE1C; C4 0x0054EC7C..0x0054EEAC; C5 0x0054F4F8..0x0054F54C; S1-S8 0x0054F70C..0x0054F992, 0x0054F5A0..0x0054F696, 0x0054F560..0x0054F59E; P1-P5 0x0054FAB0..0x0054FBCC, 0x0054EC56..0x0054EC7A; D1-D4 0x0054F050..0x0054F130, 0x0054F170..0x0054F1BC, 0x0054F270..0x0054F3EA, 0x0054EB78..0x0054EC2A, 0x0054FC2C..0x0054FC66; the seven log sites; AlreadyPrepped call sites 0x0054F0C0, 0x0054FB3C."
]
```

### M8-020

Title: "PlayAnimationAction: construction, Init, robot/game animation handlers, CheckIfDone, animationCompleted, destructor streamer stop"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "re-analysis/research/20261012-m8-play-animation-actions.md rows C1, C8..C11, P1..P12, D1..D3, L2..L8: ctor 0x00543C08..0x00543CBE (+0x84 loops, +0x8B 0, +0x8C interrupt, +0x90 null, +0x94 timeout; 60.0f -> FLT_MAX only when loops == 0), Init 0x00543F20 (cozmo_sings_custom remap 0x0054417C/0x00544190, lookup robot[0]->+0x20->+0x48, SetStreamingAnimation(robot+0x60, anim, loops, interrupt, flag) -> +0x8B, tag 0 -> failed, 0x03000001; R2E 0xCA/0xCB/0xD5 via 0x00519F8C and E2G 0x5F via external-interface vtable +0x28; callbacks 0x005452F1, 0x0054541D, 0x0054561D, 0x005457F5 and AnimationEvent forward 0x00545750), CheckIfDone 0x005441C0, GetCompletionUnion 0x005441DE (0x0075C544, 0x006C22DC), destructor 0x00543DAC (StillStreaming info 0x00543DF2 on robot+0x98 == +0x90; SetStreamingAnimation(null, 1, true, false) at 0x00543E26; releases +0xB4, +0xAC, +0xA4, +0x9C), vtable 0x01021584 slots +0x1C/+0x20/+0x2C (0x00545210).",
  "re-analysis/research/20261012-m8-play-animation-actions-check.md additions 2 (the null SetStreamingAnimation tail: +0x73 store and the idle Abort)."
]
```

### M8-021

Title: "TriggerAnimationAction: trigger to group, Init NO_ANIM_NAME paths, the group choice hand-off, the DAS blacklist gate and the robot.play_animation event"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "re-analysis/research/20261012-m8-play-animation-actions.md rows T1..T12, D4, L11..L13: ctor 0x00544228, SetAnimGroupFromTrigger 0x0054432C (Has == 1 before Get; empty-group warning TriggerAnimationAction.EmptyAnimGroupNameForTrigger \"Event: %s\" at 0x005443AC), Init 0x0054443C (NO_ANIM_NAME 0x0300000C; GetAnimationNameFromGroup with the robot and strictCooldown; blacklist set walk +0x8C/+0x90; sEvent robot.play_animation, value +0x78, $data trigger + \":\" + group at 0x005445BC), vtable 0x010215BC."
]
```

### M8-024

Title: "Pending actions surviving Cancel(-1) through ~Robot: which shipped completion callbacks re-queue after CANCELLED, IBehavior +0xA0..+0xA2 at teardown, other tag-0x5A subscribers, and whether ~BehaviorManager/~BSM queue actions"

Status: `RECOVERABLE_GAP`

Evidence:
```json
[
  "re-analysis/research/overnight/20261014-ov18-m8-015-check.md: mechanisms 0x0053FA86..0x0053FAB2, 0x0053E6E8..0x0053E6EE, 0x0053E2DA..0x0053E2F2, 0x0053DF48..0x0053DF8A; callback PLTs 0x004B01C8, 0x004B2184, 0x004B219C, 0x004B297C, 0x004B2B5C, 0x004B34B0; tag-0x5A subscribers 0x004F0742, 0x005277B0, 0x005E7B5E; ~BehaviorManager PLT 0x004A7978, ~BSM PLT 0x004A796C."
]
```
