| Requested item | Coverage | Rows |
|---|---|---|
| ActionList / ActionQueue construction | CHECKED | A |
| Per-tick Update and Robot component order | CHECKED | T |
| NOW | CHECKED | Q4-Q8 |
| NOW_AND_CLEAR_REMAINING | CHECKED | Q9 |
| NEXT | CHECKED | Q10 |
| AT_END | CHECKED | Q11 |
| IN_PARALLEL | CHECKED | Q12 |
| Shipped additional position NOW_AND_RESUME | CHECKED | Q13-Q15 |
| QueueNow running deletion 0x0053E24C..0x0053E35E | CHECKED | Q5-Q8, D |
| Cancel by id/tag/type | CHECKED | C |
| RobotCompletedAction fields, order, result, game send | CHECKED | D, W |
| IActionRunner / IAction Init, timeout, CheckIfDone | CHECKED | L1-L13 |
| Track take/release, stop-before-unlock, +0x56 | CHECKED | L5-L6, L14-L17 |
| ActionWatcher / M7-020 action-ended callbacks | CHECKED | W |
| CompoundActionSequential order/failure/ignore | CHECKED | P, S |
| CompoundActionParallel order/failure/ignore | CHECKED | P, R |
| C# hosts in Motion.cs, Behavior/, Manipulation/ | CHECKED | H |

# ActionList / ActionQueue independent extraction — 2026-10-04

Request: operator's message in this chat beginning “One task, research lane only”. No separate request file specified. Read PROJECT_STATE.md, re-analysis/research/README.md and .opencode/agent/cozmo-extractor.md. Writes confined to research. No code, inventory, manifest or state edits; no branches, worktrees, commits, tests or hardware.

CHECKED means the requested surface was examined against instructions, not fidelity acceptance. Explicit UNKNOWNs concern dynamic implementations / boundaries which this surface does not settle. None is filled with a stand-in.

Primary: resources/lib/armeabi-v7a/libcozmoEngine.so, 17,139,336 bytes, SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. All addresses are hexadecimal ELF virtual addresses. Thumb pointers have bit0 set; instruction addresses clear it. Calls resolved through PLT, veneers and relocations. Companion 20261004-actionlist-native.txt is the working instruction extract. Linear dumps can include table bytes, literal pools and neighboring instructions; these are not all code. Ghidra output is navigation only. TBB tables at 0053DA2E, 0054F802 and 0054FB12 were read as bytes.

Rows combine behavior, gates, order and failure/result. Float column gives raw binary32 patterns; “none” means no fixed float in that step. Native offsets are identification, not proposed C# layout. Results: SUCCESS=00000000, RUNNING=01000000, CANCELLED=02000000, NOT_STARTED=02000001, bad tag=03000006, interrupted=03000009, timeout=03000018, tracks locked=03000019. Category means full result shifted24; do not replace full result with category. `f32` denotes each single-precision operation.

## Current manifest quotations, before findings

The following current title/status/authority/evidence/unresolved fields are quoted verbatim from re-analysis/fidelity_manifest.json. All four records are already IMPLEMENTATION_GAP; no finding assumes an earlier status.

### M4-003

```json
{
  "id": "M4-003",
  "title": "The head and lift API follows the game-message path: caller speed/accel/duration; the original app passes head 10/20 and lift 10/20, duration 0",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "MA10 game SetHeadAngle overwrites +0x90..+0x98 with the message values (0x0052ABE0..0x0052AC24)",
    "MA11 unity/scripts/csharp/Robot.cs:1443-1450 (head 10, 20, 0) and 1638-1646 (lift 10, 20, 0)",
    "MA12 game SetLiftHeight: 32.0 while carrying -> PlaceObjectOnGroundAction (0x0052AC40..0x0052AC9E)",
    "MA9, MA13 action ctor defaults (head 15/20 at 0x00547F14..0x00547F28; lift 10/20 at 0x00548A68..0x00548A78)"
  ],
  "unresolved": "Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT. Stop-before-unlock and the AreAllTracksLockedBy gate hold (Motion.cs:904-919); the game path's ActionQueue::QueueNow replacement (0x0053E24C..0x0053E35E) is absent, so a repeated game move fails 0x03000019 (Motion.cs:831-844). The queue semantics are in Codex's 20260930-bcore-extractions.md (unchecked). Before: built, awaiting strong verification: B-CORE2 batch 4 (head/lift/action timeout). ~IActionRunner's teardown now stops the track before the lock release: RunAsync/UpdateActions stop (gated on AreAllTracksLockedBy(mask, owner) 0x00541138/0x0054115E) then unlock (0x0054120C..0x0054122A), and the lock owner is a per-action tag (the engine's to_string(+0x60), counter 0x0053FE54..0x0053FE68 store 0x0053FEC6) instead of the old constant. Citation corrected: 0x005408EC is IActionRunner::UnlockTracks, called only from the IAction constructor (0x00540CB0) and IAction::Reset (0x00540D02); the action's end release is inline in ~IActionRunner. Still out of scope: the QueueNow part (a repeated game move never gets 0x03000019) awaits Codex extraction. Tests: M4ControlTests.M4_003_MA_ALockedTrackFailsTheHeadMoveWith03000019, M4ControlTests.M4_003_MA_TheHeadMoveLocksThenUnlocksItsTrack, M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds."
}
```

### M4-016

```json
{
  "id": "M4-016",
  "title": "Head and lift move semantics: no send when in position, ack matched by id, completion in position and stopped, tolerances and error codes",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "MA9, MA13, MA15..MA17, C1, C6, C10.1 as before",
    "C11.3 the lift CheckIfDone body 0x005493F6..0x00549508 contains no eye-shift removal; the head +0xA8 has only three writers, all zero (0x00547F3C, 0x005484B0, 0x00548724), so H4..H6 never execute"
  ],
  "unresolved": "Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT, confirmed by the Opus verifier. +0x74 starts negative and is set from the tick clock at the first UpdateInternal (0x00540D52..0x00540D64); Motion.cs:828 stamps it at RunAsync. Codex's \"second precondition-time gate\" (0x00540DAC..0x00540DC0) cannot fire for these actions: slots 0x24/0x28 return 0.0 (0x0052B0BA/0x0052B0BE), slot 0x2C returns 30.0f (0x0052B0C2). Before: built, awaiting strong verification: B-CORE2 batch 4 (head/lift/action timeout). The timeout is now tested on the engine clock (CozmoEngine.Timer.Seconds) from the action's Init/first UpdateInternal (IAction::UpdateInternal 0x00540D4A..0x00540D64) before CheckIfDone, run by Robot::Update's ActionList step (CozmoMotion.UpdateActions, hook 0x00540370/CD12), and fails with EngineResult 0x03000018 (0x00540E80); the wall-clock Task.Delay is gone. Strict IsNear (0x0084CC0A) and the 30.0 s default hold. Tests: M4ControlTests.M4_016_MA15_NothingIsSentWhenAlreadyInPosition, M4ControlTests.M4_016_MA16_MA17_CompletionIsTheAckThenInPositionAndStopped, M4ControlTests.M4_016_MA17_StoppingOutOfPositionIsStoppedMakingProgress, M4ControlTests.M4_016_C1_TheHeadInPositionIsLatched, M4ControlTests.M4_016_R1_PassingTheTargetBeforeTheAckDoesNotLatch, M4ControlTests.M4_016_C6_TheLiftCompletion, M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds, ControlTests.AnUnacknowledgedActionTimesOutRatherThanReportingSuccess."
}
```

### M7-020

```json
{
  "id": "M7-020",
  "title": "Mood event production, repetition penalty and affector application",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so plus shipped mood_config.json",
  "evidence": [
    "MoodManager::TriggerEmotionEvent 0x0067b85c",
    "MoodManager::AddToEmotion 0x0067bdca",
    "MoodManager::HandleActionEnded 0x0067c770",
    "EmotionAffector::ReadFromJson 0x00679910",
    "MoodManager::SendEmotionsToGame 0x0067b724"
  ],
  "unresolved": "Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. No ActionList, ActionWatcher or RobotCompletedAction is built (0x00541BD8..0x00541C00, 0x0053F5DC, 0x0067AEE8). Before: built, awaiting strong verification: the one-shot erase and key formation of HandleActionEnded are built and tested. Not built: the live caller. The stack has no ActionList/ActionWatcher and its actions produce no RobotCompletedAction record (the nearest hooks are SteppedBehavior.StartActing/ActingEnded and the RunTriggerAction completion, which cover behaviour-started actions only). It needs an ActionList and ActionWatcher that queue a RobotCompletedAction (tag, RobotActionType, 32-bit result) at every action ending (0x00541bd8..0x00541c00), drain it after the queues in ActionList::Update (0x0053f5dc..0x0053f5e4) calling the registered callbacks in registration order (0x0054187e..0x005418de), registration at MoodManager::Init when the robot is non-null (0x0067aee8..0x0067af38) and unregistration in the destructor (0x0067ae18..0x0067ae30), plus the RobotActionType and ActionResultCategory enums that key mood_config.json."
}
```

### M8-012

```json
{
  "id": "M8-012",
  "title": "BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "BehaviorManager::Update 0x005a2f70 returns immediately when the manager's init byte at +0 is zero, logging \"BehaviorManager.Update.NotInitialized\" (0x005a2f70/0x005a2f72/0x005a2fea).",
    "It ticks the current activity first: GetCurrentActivity then the activity's virtual Update at vtable+0x20 (0x005a2f78/0x005a2f80/0x005a2f84).",
    "If robot+0x355 is non-zero, or manager+0x20 == 1, it calls EnsureRequestGameIsClear (0x005a2f8e/0x005a2f94/0x005a2f9e).",
    "If manager+0x38 is set it calls SelectUIRequestGameBehavior, clears the flag, and when the previous current behaviour's class is 0x2e (cmp r7,#0x2e at 0x005a2fe0) sets byte +0x220 on the object at manager+0x30 (strbeq.w r1,[r0,#0x220] 0x005a305c).",
    "It calls CheckReactionTriggerStrategies 0x005a3060; a non-zero result suppresses the scored choice and the UI-request switch for this tick (cbnz r5 0x005a3068).",
    "The reaction list is evaluated only when the robot's action list robot+0x250 is empty: the manager latch at +0x4c is ORed with !IsEmpty and CheckReactionTriggerStrategies returns false when the list is empty (0x005a3558..0x005a356c).",
    "If no reaction fired, nothing occupies manager+0x30, and the current behaviour's class is 0x16, it calls ChooseNextScoredBehaviorAndSwitch (ldrb r0,[r0,#0x10] 0x005a3070; cmp r0,#0x16 0x005a3072; blxeq 0x005a3078).",
    "If no reaction fired and manager+0x30 holds a behaviour different from the current one, it calls SwitchToUIGameRequestBehavior (0x005a307e/0x005a3082/0x005a309a).",
    "It ticks the current behaviour with IBehavior::Update (0x005a30bc). Return 0 logs \"BehaviorManager.Update.FailedUpdate\" (0x005a3148) and return 2 logs a debug line; both then call FinishCurrentBehavior(behavior, class != 0x16) (0x005a3128). Return 1 keeps it (cmp r0,#0 0x005a30c0; cmp r0,#2 0x005a30c4).",
    "SwitchToBehaviorBase 0x005a1e6a stops the current behaviour (StopAndNullifyCurrentBehavior), calls IBehavior::IsRunnable(robot) and logs \"BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable\" on false, then IBehavior::Init (0x005a1e94); an Init failure logs \"BehaviorManager.SetCurrentBehavior.InitFailed\" (0x005a1eae), clears the behaviour, and still sets the running/resume info (SetRunningAndResumeInfo 0x005a1f12) and sends the DAS transition (SendDasTransitionMessage 0x005a1f1c).",
    "FinishCurrentBehavior 0x005a38ca: immediate == 1 tail-branches to 0x8cbc9c; otherwise, if manager+0x30 equals the finishing behaviour, it calls EnsureRequestGameIsClear (0x005a38e6) and switches to a default class-0x16 BehaviourRunningAndResumeInfo (movs r0,#0x16 0x005a38f4; SwitchToBehaviorBase 0x005a38fe).",
    "SwitchToReactionTrigger 0x005a25e4 builds a resume info and calls the strategy's ShouldTriggerBehavior (vtable+8) (0x005a262a); only when it returns 1 does it switch (cmp r0,#1 0x005a262c; SwitchToBehaviorBase 0x005a26da).",
    "TryToResumeBehavior 0x005a2b48 bails unless the stored resume head angle differs from a constant and the robot's action list is empty (vldr s0,[pc] 0x005a2b48; ActionList::IsEmpty 0x005a2b60), logging \"Resuming behavior and don't have an action, so setting head angle %f, lift height %f\" (0x005a2b72)."
  ],
  "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. NotSupportedException seams (BehaviorManager.cs:632/649); no ActionList (MISSING at :311, :414, :873); the sends at :788-792 are MISSING. Before: built, awaiting strong verification: phantom 0x16 behaviour removed (running info = {current, resume, trigger}); tick order per Correction A2 (activity Update, EnsureRequestGameIsClear, UI request, reactions every tick, scored switch behind three gates, IBehavior::Update then FinishCurrentBehavior(trigger != None)); TryToResume with head/lift restore and no manager IsRunnable pre-check; failure switches to empty; running info stored on Init failure; per-trigger disable-count skip. Still open (seams): the meaning of manager+0x20, SelectUIRequestGameBehavior, SwitchToUIGameRequestBehavior and the +0x220 flag (no test for the class-test order before settling), EnsureRequestGameIsClear and SendDasTransitionMessage bodies, ActionList-is-empty is not wired in production, the concrete activity Update bodies (M7/M15); the stack's ChooseAndSwitch is a test-only parallel path."
}
```

## Findings against the quoted current records

- **M4-003:** current missing QueueNow diagnosis is supported. “a repeated game move never gets 0x03000019” is too broad: replacement deletes its current runner before the new runner tick, but another queue can still hold overlapping tracks and the lock test remains. Also QueueNow's pending-nonempty branch does NOT Cancel, unlike pending-empty. Preserve old RUNNING versus CANCELLED completion, not one normalized replacement result. Q6-Q8/L5/D3.
- **M4-016:** current first-UpdateInternal correction and default zero-delay / 30-second statements agree with instructions. Motion.cs still stamps/locks/sends in RunAsync before queue tick. Timestamp precedes Init, timeout precedes Init/CheckIfDone. This report does not contradict concrete head/lift CheckIfDone claims or claim nonzero head/lift delays. L7-L10/H2.
- **M7-020:** gap diagnosis supported. Evidence `HandleActionEnded 0x0067c770` is wrong for this callback: GOT relocation 0103FA44 binds symbol value0067B319, instruction entry0067B318. Watcher enqueue call is00541C04, after construction in requested00541BD8..00541C00. ActionList watcher Update call0053F5E6, after loop branch0053F5DC. Callback order is signed ascending handle, matching registration order until wrap; not an unconditional insertion-order list. Destruction can report NOT_STARTED, INTERRUPTED or RUNNING, not only terminal successes. W.
- **M8-012:** quoted evidence says both “only when ... empty” and “returns false when ... empty”. Native implements `latch |= !ActionList.IsEmpty()` then refuses evaluation if resulting latch zero OR strategies empty. An already-set latch stays set when queues become empty. IsEmpty tests map size; empty queues persist until Update erases them. 005A3558..005A357A/A7/T12. Current unresolved production gap remains valid.

## A — construction and ownership

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| A1 | 0053D8AC..0053D8D4 | ActionList ctor: signed-key queue map empty, begin sentinel this+4, root/count0; clearing+0C=false; allocate0x4C at D8C2, watcher ctor D8CA, pointer+10. No queue tick or Init. | none |
| A2 | 0053F91C..0053F936 | ActionQueue ctor: current+0=null; pending doubly-linked sentinel+4, next/front+8, count+0C=0; deletion-tag set+10 empty; clearing+1C=false. | none |
| A3 | 0053D916..0053D93A | ActionList Clear: clearing reentry returns; set true; destroy map nodes/queues, reset map/count/begin, false. No delayed tick needed to empty map here. | none |
| A4 | 0053F96A..0053F9AC | Queue Clear: +1C reentry returns; true; Cancel current F97C, Delete current F98A; pending front-to-back Delete F998..F9A8 WITHOUT Cancel; false. Pending unstarted results stay NOT_STARTED. | none |
| A5 | 0053F938..0053F968 | Queue destructor Clear, then deletion-tag set destruction, then pending list disposal. Virtual runner destructor owns child teardown, not queue-specific sibling cancellation. | none |
| A6 | 0053D8F2..0053D914 | ActionList destructor Clear before watcher destructor/delete. Clearing can enqueue watcher events; destructor does not Update/drain them. | none |
| A7 | 0053E970..0053E97A | IsEmpty loads map count+8, returns count==0. Not sum of pending/current. Cancel leaves empty queue nodes until T5. | none |
| A8 | 0053F7E0..0053F7F4 | GetCurrentAction: current if nonnull, else pending.front if count>0, else null. Inspection does not promote/Init front. | none |
| A9 | 0053DCE4..0053DD44 | Duplicate/clearing guard: clearing ->Prep incoming, virtual delete, true. Otherwise scan pending raw pointers across queues; duplicate warns/true WITHOUT deleting it. Does NOT compare each separately stored current pointer. Do not silently broaden guard. | none |

## T — Robot / list / queue tick order

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| T1 | 00513BC8..00513C62 | Robot Update timer/idle/sync-timeout work precedes first-full-state gate robot+34E; false returns before actions. Enqueue is not Init. | unrelated idle constants not action timeout; L7 governs action clock |
| T2 | 00513C6A..00513CD4 | Viz Start; vision+28 nonzero ->UpdateAllResults C76; nonzero result returns Robot Update. Absolute-localization flag+2C6 ->send/clear; Map UpdateRobotPose CD4. Vision failure skips actions. | none |
| T3 | 00513CD8..00513EAC | Charger-platform step gated +34A && !+355; Mood Update E8A; Inventory E94; Progression E9C; BlockTapFilter EA4; AIComponent EAC. Mood regular update BEFORE watcher delivery. | none for queue |
| T4 | 00513EB0..005140BC | BehaviorManager EE6 unless global debug skip counter>0 (decrement/skip manager only); activity/behavior Viz/SDK status; ActionList call005140BC, robot+250. Nonzero list result warns, does not abort later components. | none |
| T5 | 0053F580..0053F5E6 | List iterates signed ascending queue key. Updates all despite failure; retains FIRST nonzero result F5A2..F5A6. Erase only pending count0 AND current null. After all queues, including empty map, watcher Update F5E6. F5DC is loop branch. | none |
| T6 | 0053F5F4..0053F628; 0053F9BC..0053F9E2 | Null current promotes pending.front, unlinks/frees node, count--; empty+null returns0. Watcher ParentActionUpdating BEFORE runner Update. One selected/current runner update per queue tick; no next action start after deleting terminal current this tick. | none |
| T7 | 0053F62C..0053F67C; 0053F6E2..0053F708 | Exact RUNNING ->Viz text and SDK action status, retain current, queue result0. Presentation is not terminal result. | none |
| T8 | 0053F67E..0053F6E0 | Non-RUNNING ->clear text/status, Delete current. Queue return0 iff full result0 OR02000000; all other full results return1. Game result remains runner+18, not queue1. | none |
| T9 | 0051410C..00514170 | AFTER actions/watch: AnimationStreamer gated robot+29 && +2A, call411E; NVStorage416A; Path4170. Later NV change cannot retroactively authorize this tick's earlier streamer gate/actions. | none |
| T10 | 00514174..0051423E | Virtual update loop over robot+484 collection; then BlockFilter422A, CheckDisconnectedObjects4230, ConnectToRequestedObjects4236, Map Update423E. Concrete runtime collection class names UNKNOWN in this body. | none |
| T11 | 00514242..00514478 | Draw/Viz work; CubeLight Update(true)4468; BodyLight4470; PublicStateBroadcaster4478. All after ActionList/watch. | none for queue |
| T12 | 005A3558..005A357A; 0053E970 | Earlier BehaviorManager samples map-empty before T5, ORs sticky+4C with !IsEmpty. Refuse if resulting latch0 or no reaction strategies. Later action cleanup does not change already-read sample. | none |

## Q — QueueAction, all six shipped positions

Unity corroboration: unity/scripts/csharp/Anki.Cozmo/QueueActionPosition.cs:3-11. TBB0053DA2A, bytes0053DA2E=`03 50 63 5B 6B 73`; targets DA34/DACE/DAF4/DAE4/DB04/DB14. Value2 is NOW_AND_RESUME, even though the request lists five names. NEXT=3, AT_END=4, IN_PARALLEL=5.

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| Q1 | 0053D9CE..0053DA22; 0053DA24..0053DA26; 0053DB2C..0053DB70 | Null ->error/debug gate/API1, no delete. Position>5 ->error/debug/API1, no incoming delete. API int is not ActionResult. | none |
| Q2 | 0053D94C..0053D982; 0053DA44..0053DAC8 | robot+2C7 disabled gate rejects external-tag ranges1..1,000,000 or2,000,001..3,000,000 (unsigned arithmetic). BAD_TAG03000006 also discarded. Discard uses Delete path and API0, not a promise of execution. | none |
| Q3 | 0053DCA0; 0053DE60; 0053DEA4; 0053DEE8 | NOW/NEXT/END/FRONT wrappers A9 guard then find/emplace queue key0, forward incoming/retries. Map existence itself affects IsEmpty before tick. | none |
| Q4 | 0053DA34; 008CB44C ->004AA894 ->0053DCA0 | NOW value0 ->QueueActionNow. No synchronous Init/Update. QueueNow distinguishes pending count, not just current. | none |
| Q5 | 0053E24C..0053E2C8 | QueueNow null ->error/debug/API1. Nonnull branches on pending count+0C. Nonempty branch may log old current but does not Cancel. | none |
| Q6 | 0053E2C8..0053E2F2 | Pending nonempty: Delete(current,sentinel) E2D6 WITHOUT Cancel; null current allowed. Assign incoming retries+8, prepend before pending.front. Running old state can remain RUNNING in game/watch completion. | none |
| Q7 | 0053E338..0053E36E | Pending empty: nonnull current Cancel E350 BEFORE Delete E35E, then QueueAtEnd E36E, forward API return. Cancel leaves NOT_STARTED alone; other states become CANCELLED. | none |
| Q8 | 0053E2D6; 0053E350..0053E36E; 0053F5F4 | Both destroy/stop/unlock old current before incoming runner first tick. Incoming is pending, current null; another queue's overlapping lock can still produce03000019. No immediate lock/Init. | none |
| Q9 | 0053DACE..0053DAF2 | NOW_AND_CLEAR_REMAINING value1: A9 guard; list Cancel(type=-1) DAE0 across ALL queues; QueueNext key0 DAE4. Pending deleted without Cancel; empty map entries persist until T5. | none |
| Q10 | 0053E078..0053E168 | NEXT value3: pending count0 ->QueueAtEnd; otherwise insert BEFORE SECOND pending node, retries/count. CurrentR,pending[A,B] ->R,[A,new,B], not R,[new,A,B]. Same placement when no current. | none |
| Q11 | 0053DB04; 0053E16C..0053E248 | AT_END value4: append tail linked node, retries+8, count++; null/API1, normal/API0. | none |
| Q12 | 0053DB14..0053DB28; 0053DF2C..0053E076 | IN_PARALLEL value5: guard/null ->-1 from AddConcurrent; otherwise choose lowest free positive signed key from1, emplace separate queue, append incoming/retries. Dispatch -1->API1, else0. Parallel queues tick in key order, not threads/compound. | none |
| Q13 | 0053DAF4; 008CB46C ->004AA8AC ->0053DEE8; 0053E42C | NOW_AND_RESUME value2 ->QueueAtFront. No current+pending ->QueueNow; neither ->QueueAtEnd. | none |
| Q14 | 0053E49A..0053E51E; 00540250..00540296 | Current ->Interrupt E4A0. Virtual+14 predicate must equal1. If +56false and stateRUNNING unlock; virtual Reset(false); state03000009. Success pending=[new,old,residual], current null, incoming retries assigned. Old isn't destroyed / watcher-ended merely by interruption. | IAction Reset start BF800000 |
| Q15 | 0053E51E..0053E578 | Interrupt refusal logs then QueueNow fallback Q6/Q7, not wait/drop incoming. Concrete derived Reset may add effects; base Interrupt contains no Stop. | none |

## C — cancellation / identity

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| C1 | 0053FD94..0053FDAA; 0053FE54..0053FEC8; data01051020 / GOT0103EB14 | NextIdTag returns old global unsigned counter then increments; wrap0 replaced002DC6C1 (3,000,001). Ctor collision-checks id set; original+5C and current+60 initially equal. ELF data at01051020 seeds counter002DC6C1; GOT0103EB14 relocation names IActionRunner::sTagCounter. It is global, not per robot. | none |
| C2 | 00540098..00540192 | SetTag RUNNING refuses/sets BAD_TAG/false. Otherwise erase prior changed tag (original remains reserved); nonzero unique requested tag stores+60/true, zero/collision BAD_TAG/false. No numeric range gate here besides nonzero/uniqueness. | none |
| C3 | 005409DC..00540A46 | Runner Cancel: NOT_STARTED unchanged; otherwise log then stateCANCELLED. No Stop/unlock/CheckIfDone/RunCallbacks directly. | none |
| C4 | 0053E690..0053E702 | Queue Cancel(type): -1 all, otherwise+44 equals requested. Current match Cancel/Delete; pending matching Delete WITHOUT Cancel front-to-back; false pending Delete stops loop. Returns any deletion. | none |
| C5 | 0053E83C..0053E96E | Queue Cancel(unsigned idTag) compares current+60, not original+5C; current Cancel/Delete; pending matching Delete only; warn duplicate tag; returns any. No distinct original-id overload recovered: unsigned API is current-tag cancellation. | none |
| C6 | 0053DE10..0053DE5E; 0053E704..0053E83A | List Cancel(type/tag): clearing returnstrue; otherwise all sorted queues OR results; duplicate-tag warning across queues. No queue-node erase or watcher tick here. Events await next list Update. | none |

## L — runner / IAction lifecycle

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| L1 | 0053FDB0..0053FEC8 | Runner ctor retries+8=0,result+18=NOT_STARTED,type+44, name+48, mask+54 ctorargument. Word00010000 at+55 ->prepped+55false, suppress+56false, log+57true, clear-profile+58false. Stores ctor Robot reference at+4, initializes callback list+64, cached union+1C by type; ids C1. | none |
| L2 | 00540C44..00540CE6; 00540CE8..00540D1A | IAction ctor init+70false,start+74negative, UnlockTracks (not-started gate no-op), stateNOT_STARTED. Reset(bool) clears init/start; true UnlockTracks before stateNOT_STARTED, false skips unlock. | BF800000 (-1.0f) |
| L3 | 00540370..005403BC | ActionStartUpdating first. RUNNING ->virtual UpdateInternal. Only NOT_STARTED,03000009,04000000 enter start branch. Other stored states terminal ->Prep/WatcherEnd without CheckIfDone. | none |
| L4 | 005403BC..00540430 | If custom motion profile present, virtual SetMotionProfile+18; false logs unused, not failure. Store RUNNING BEFORE lock check. | none |
| L5 | 00540430..0054058E | +56nonzero bypasses AreAnyTracksLocked and LockTracks. Otherwise locked required mask ->03000019, WatcherEnd, return; no Init or Update's Prep (Delete Preps later). Unlocked ->LockTracks4058E(mask+54,tag+60,name+48). | none |
| L6 | 004F0F4C..004F0F8C; 004F0EB2..004F0F00 | Lock/unlock helper owner constructed by to_string(int), signed tag bits. Stop-owner test in destructor uses to_string(unsigned int). Preserve distinction for high-bit tags, not normalized owner text. | none |
| L7 | 00540D1C..00540D64 | UpdateInternal reads timer seconds as float; start<0 ->stamp this tick BEFORE Init. Queue/API call timestamp is not start. Initialized+70 selects post-init-delay slot vs zero. | sentinel BF800000; absent post-delay00000000 |
| L8 | 00540D68..00540DC0; 0052B0BA..0052B0C8 | Virtual pre-delay+24; post-delay+28 only when initialized; timeout+2C. FIRST now>=f32(start+timeout) ->timeout. Else now<f32(f32(start+pre)+post) ->RUNNING wait. Native branch/NaN behavior not replaced by abstract mathematical double comparisons. Default pre/post zero, timeout30. | 00000000/00000000/41F00000 |
| L9 | 00540DC4..00540DF8; 00540F2C..00540F9E | Gates pass: !init ->virtual Init+1C. Return0 sets inittrue then CheckIfDone SAME tick. Nonzero Init with init stillfalse ->result handling; if Init itself sets inittrue then CheckIfDone. Already init calls virtual CheckIfDone+20. Base Init at005413CA returns0; CheckIfDone is pure virtual (IAction vtable010214A0, slots table+24/+28 with vptr=table+8). Concrete overrides require concrete inventory. | none |
| L10 | 00540D9E..00540DAA; 00540E80..00540EE4 | Timeout03000018 before Init/Check, including equality; log then terminal callbacks. Queue wait before first UpdateInternal doesn't count. No wall-clock Task-delay fallback. | default41F00000; f32 add |
| L11 | 00540DF8..00540E7C; 005405A0 | Result category 4 and retries+8positive: decrement, startnegative, initfalse, transient stateNOT_STARTED, RETURN RUNNING. Outer runner stores returned RUNNING over+18 at405A0. No derived Reset/Stop/relock in this branch. Next tick can re-Init inside UpdateInternal; do not infer start-branch reentry from transient store alone. | BF800000 |
| L12 | 00540EEA..00540F20; 00540846..005408EA | IAction terminal non-retried result invokes completion callbacks linked-list order with full result. AddCompletionCallback appends; explicit RunCallbacks loops list. Cancel->Delete does NOT run this UpdateInternal path. | none |
| L13 | 005405A0..0054063A; 00540750..005407C2; 0052B0A6..0052B0B0 | Runner stores virtual result, optional+57log, Prep terminal, WatcherEnd, return. Prep +55guard: first virtual GetCompletionUnion into cache+1C then+55true; repeat logs AlreadyPrepped. NO RunCallbacks in Prep. Base union getter copies+1C. | none |
| L14 | 005408EC..00540916; 00540918..005409DA | Unlock skips if+56 orNOT_STARTED, otherwise helper mask/tag. SetTracksToLock only NOT_STARTED; later warning/nochange. Interrupt unlock only RUNNING(Q14). | none |
| L15 | 00541084..00541192 | Base destructor vptr installed; unprepped warns; clear custom profile iff+58; erase current/original ids; unsigned tag string. HEAD moving MC+A && ownership(mask1)==1 ->StopHead41146; LIFT MC+B/mask2 ->StopLift4116C; BODY MC+C/mask4 ->StopBody41192. HEAD thenLIFT thenBODY. Uses pertrack ownership, not just declared mask. +56 does NOT directly gate stop checks. | none |
| L16 | 0054120C..00541238 | AFTER stops: unlock declared mask iff+56false and state!=NOT_STARTED; watcher ActionEnding41238 AFTER unlock. Suppression still allows destruction event. Derived teardown precedes base teardown. | none |
| L17 | 00541238..0054127A | After watcher snapshot, temporary/name resources freed, callback list clear4125C, completionUnion clear41272, statusstrings. No unconditional destructor RunCallbacks. Game Broadcast later D6. | none |
| L18 | 00540298..0054036E; 00540836..00540844 | ForceComplete writesSUCCESS. RetriesRemain decrements positive byte and true elsefalse; compound uses this helper. Neither supplies guessed Init/destruction. | none |

## D — deletion / game completion

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| D1 | 0053F9E4..0053FA22 | DeleteActionAndIter null ->false. Insert tag+60 in queue deletion set; duplicate insertion ->false. Reentry guard keyed tag, not pointer; not a global ownership policy. | none |
| D2 | 0053FA24..0053FA4C | Prep BEFORE game snapshot/destructor. Game gate HasExternalInterface then suppress iff state03000009. No success-only, external-tag-only or started-only filter. | none |
| D3 | 0053FA4C..0053FA66; 00540AA8..00540B64 | Game snapshot BEFORE destruction. GetSubActionResults first, virtual union next; construct idTag=current+60,type+44,result FULL+18. Snapshot external pointer. Not queue boolean/category/forcedcancel result. | concrete union floats UNKNOWN without action |
| D4 | 0053FA70..0053FA9C | Virtual deleting destructorFA78; caller reference nullFA7C AFTER destructor; erase pending iterator if notsentinel. Current reference stillnonnull during destructor. Preserve reentry ordering. | none |
| D5 | 00541238; 00541BB4..00541C04 | Destruction queues watcher snapshot independently of game gate, after stop/unlock. Base vptr getter copies Prep cache. Compound children release/destroy before parent base watcher event. | none |
| D6 | 0053FAAA..0053FAB2 | AFTER destructor/null/node erase: MessageEngineToGame(RobotCompletedAction&&), external virtual+1C Broadcast. No send result check/retry; no direct watcher callback invocation. Transport delivery guarantee UNKNOWN here. | none |
| D7 | 0053FABE..0053FB1A | Erase guard tag AFTER broadcast; clean union/vector/message temps, true. Watcher is not deferred game-sender. | none |
| D8 | 00540BAC..00540C42; 00715E3E..00715EC0; 007314AE..007314DC | Native record64bytes: +0tag uint32,+4type int32,+8result uint32,+C vector12bytes,+18union40bytes. Wire: tag4,type4,result4,countbyte, all result4 elements,union Pack. Count narrowed, not clamped vector policy. | variant bits preserved |
| D9 | unity/scripts/csharp/Anki.Cozmo.ExternalInterface/RobotCompletedAction.cs:153-193; MessageEngineToGame.cs:104 | Shipped CLAD corroborates little-endian fields/order, bytecount then full array, union. Outer Tag.RobotCompletedAction=90(5A). >255 count narrows but all elements pack; no silent clamp. Concrete completion payload depends on virtual union getter. | UNKNOWN concrete variant floats |

## W — ActionWatcher / Mood action-ended callbacks

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| W1 | 00541EAC..00541F68 | GetSubActionResults missing tag leaves output untouched; present collects descendants into SET<ActionResult>, clears output, inserts set order. Root excluded; unique numeric full results, not completion-order list/child ids. | none |
| W2 | 00543902..00543982 | Recursive collector each child vector entry: insert embedded child's result node+C, recurse. No error-only/category filter. | none |
| W3 | 00541BB4..00541C04 | Watcher snapshot order: scalar tag/type/result; virtual union; THEN GetSubActionResults; deque push. Game builder has subresults BEFORE union. Existing result preserved even unstarted/running/interrupted. | none |
| W4 | 00541C08..00541D2A | Ensure ending node; newly created attaches to current root if found/nonzero; mark new node+50; copy record andname. Membership in tagmap distinct from parent's child ownership. | none |
| W5 | 00541D2A..00541DFC; 00541658; 00543B62 | Ending tag in root-stack map ->DeleteActionTree, erase stack entry. Otherwise parentless ending node dispose, then tag map entry erase. Child withparent retained in parent's vector despite tag map entry erase; ancestor can aggregate result later. Tree disposal descendants before root. | none |
| W6 | 00541598..005415D8; 005419D8..00541A3E | Ctor node map empty; root+C/current+10/previous+14=0; stackmap+18empty; callback map+24empty; next handle+30=1; record deque+34empty. Node:tag, embedded record, name+44,parent+54,childrenvector+58. | none |
| W7 | 00541974..005419D6 | ParentActionUpdating sets root=selected queue actiontag, current/previous0, ensures root node. Compound nesting remains under root; not separate queues. | none |
| W8 | 00541A40..00541B46 | ActionStartUpdating previous=oldcurrent,current=tag; new node links to previous node when found, appends child vector; pushes tag on root stack. Before runner lock/terminal checks. | none |
| W9 | 00541B48..00541BB2 | ActionEndUpdating pops root stack; current=newback,previous=preceding, absent=>0. Nested child update restores parent context, not one boolean. | none |
| W10 | 00541238; 00541BD8..00541C04 | Event is DESTRUCTION, not merely returnterminal. Can carryRUNNING Q6, CANCELLED Q7, NOT_STARTED pending deletion, INTERRUPTED independent deletion. No game-interface filter. Interrupt alone doesn't emit. | none |
| W11 | 0053F5DC..0053F5E6; 0054187E..005418DE | List afterqueues calls watcher. While deque nonempty: every callback map entry signed ascending handle receives frontrecord, thenpop_front. Appended records can drain same tick. No snapshot/cap or exception policy inferred. | none |
| W12 | 0053F878..0053F916; 0054179C..005417D2; 0054182C..0054187C | List moves callback to watcher, returns integerhandle. Handlesincrementfrom1, map keyedint; unregister erasesmatching handle/returnsfound. No HasExternalInterface gate. Signed wrap need not chronologicalorder; no new safe callback-mutation policy. | none |
| W13 | 0067AEBC..0067AEE8 | Mood Init builds action-event map before robotnonnull gateAEE8. Separate external subscription gateAEF2..AF0A does NOT gate watcher registration. | none |
| W14 | 0067AF18..0067AF38; relocation0103FA44; 0067C2BA | Bind obtains GOT MoodManager::HandleActionEnded, captures Moodthis, registers Robot ActionListAF34, handleMood+158AF38. Symbol0067B319/entry0067B318. Bind thunk memberpointer dispatch. | none |
| W15 | 0067B318..0067B350 | One-shot set+140 contains idTag ->erase/return. Else lookup key full int32 actionType(record+4), category byte=(record.result>>24). Do not infer category from success boolean. | none |
| W16 | 0067B354..0067B3D6 | Key missreturn; hit log and TriggerEmotionEvent(event,now) B3D0. Affector calculations remain existing M7 scope, not re-extracted. | timer read, no fixed float |
| W17 | 0067AE14..0067AE30 | Dtor handle+158nonzero &&robot &&ActionList ->unregisterAE2A,zeroAE30. Mood does not own action destruction. | none |

## P — shared compound mechanisms

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| P1 | 0054E990..0054EB76 | Common ctor runner typeFFFFFFFE/mask0; input child list order preserved, null children warn / skip; completion cache+7C, predicate map+88 empty; proxy+98=false; deleteOnCompletion+99=true. Constructor bools install no ignore predicate. | none |
| P2 | 0054EC7C..0054ED8E; 0054FE4E | Raw bool AddAction overload: ignoreFailure true creates predicate operator returning 1 unconditionally; false empty predicate. Other bool forwarded to virtual AddAction, not the ignore bool. Do not infer argument meaning from decompiler prototype. | none |
| P3 | 0054ED90..0054EEB0 | Base AddAction creates shared_ptr child, copies parent logging+57, appends child/name, stores nonempty predicate keyed CHILD POINTER, returnsweakref. Forwarded extra bool not used by this base body. List erase only destroys on final shared release. | none |
| P4 | 0054F170..0054F1BE; 0054F1C0 | ShouldIgnoreFailure missing pointer=false; found invoke predicate(full result,child const pointer). Arbitrary callable semantics UNKNOWN without its body; bool overload P2 exactly true, consulted only on branches S/R below. | none |
| P5 | 0054F004..0054F04E | SetDeleteOnCompletion sets+99 and propagates to compound children via dynamic_cast in child order. Defaulttrue. Retained child can have held tracks. | none |
| P6 | 0054F050..0054F16E | StoreCompletionUnionAndDelete: virtual child union BEFORE Prep, cache union/type by current tag, Prep child; matching proxy updates parent type.  +99=true ->erase node/advance, destructor only last reference. False ->unlock child unless+56 orNOT_STARTED, retain child in list / advance, no Cancel/Stop/statechange. | none |
| P7 | 0054EB78..0054EC2E; 0054FC2C..0054FCAC | DeleteActions remaining children insertion order with temporary shared reference. Retained(+99false), non-NOT_STARTED, nonsuppressed child: unsigned owner ownership test; if not owned retake tracks before destruction. Prep, erase, releasetemp. NO generic child Cancel. Clear predicates / cache / list then base runner. Child destructor/watch before parent base; external refs can delay actual child death. | none |
| P8 | 0054F20C..0054F26E; 0054F270..0054F3F4 | Proxy tag+94,+98=true; live/cache match copies child type. GetUnion proxy searches live child, then cache; missing warns/base fallback. Without proxy base union. Payload proxy does not itself substitute parent's result. | none |

## S — CompoundActionSequential

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| S1 | 0054F4F8..0054F5A0 | Ctor NOT_STARTED,delay+9C=zero,deadline+A0=negative,iterator+A4=first,firstTick+A8=true; Reset children(true). Reset(bool) resets children with bool, iterator/firstTick/deadline/state. | 00000000 delay; BF800000 deadline |
| S2 | 0054F70C..0054F77A | Derived hook vtable+24 first; nonzero ->0300001C without child tick. Concrete override semantics UNKNOWN absent subclass. | none |
| S3 | 0054F77A..0054F7DE | First tick iterator from actual list front; empty ->SUCCESS WITHOUT parent RunCallbacks in this branch. Copy+56 to child BEFORE delay gate. Deadline>=0 && now<deadline ->RUNNING; else child Update. | BF800000/00000000; timer float |
| S4 | 0054F7DE..0054F808; table0054F802 | Copy child status text; full result>>24 dispatch:0 MoveToNext(now),1 RUNNING,2/3 failure,4 retry. Category >4 SUCCESS at F882. Low bits preserved for failures/callbacks. | none |
| S5 | 0054F808..0054F8F2 | Parent RunCallbacks(full failure) BEFORE predicate. Ignore==1 log then MoveToNext; else Store/Delete current, return full failure. Ignored failure can callback before eventual success; no native once-only contract. | none |
| S6 | 0054F904..0054F992 | Category 4 retries positive ->decrement parent retry, reset children(true), transient NOT_STARTED, deadline negative,iterator first,firstTick=true, return RUNNING. Outer stores RUNNING. No predicate before retry; no retry ->S5. | BF800000 |
| S7 | 0054F5A0..0054F5F0; 0054F668 | MoveToNext(now): delay>0 deadline=f32(now+delay); Store/Delete finished child, advance. End RunCallbacks(SUCCESS)/SUCCESS. Remaining deadline>now ->RUNNING. Zero/negative delay doesn't stamp new positive deadline. | threshold 00000000; f32 add |
| S8 | 0054F5F0..0054F668 | Else copy+56, Update ONE next child SAME tick. RUNNING return. Any other Store/Delete that next child; if end RunCallbacks(full next result); remaining and SUCCESS ->RUNNING; otherwise return full nonzero. NO ignore/retry on this immediate-next path. | none |
| S9 | 0054F650..0054F668; 0054F8F6 | [A,B,C],delay0: A success can tick B same outer tick; B success with C remaining ->C next tick. B immediate failure / retry returns directly regardless predicate/retries. Not loop-until-running over all children. | 00000000 example |

## R — CompoundActionParallel

| Step | Address | Behavior, gates, order, failure/result | Float bits |
|---|---|---|---|
| R1 | 0054FA58..0054FAB0; 0054FAB0..0054FB16 | Common ctor, child tick insertion order, copy+56 to each before Update. Same tick thread; previous child result controls whether later child is ticked. | none |
| R2 | 0054FB16..0054FB50; 0054FB66..0054FB76 | Category 0 Store/Delete/continue;1 advance,remember RUNNING/continue. End any running ->RUNNING; else parent RunCallbacks(SUCCESS)/SUCCESS, including empty. | none |
| R3 | 0054FB22..0054FB44; 0054FBCC | Category 2 / 3 or unretried 4: parent RunCallbacks(full result) BEFORE predicate. Ignore==1 Prep/Store/Delete/continue. Else immediate full failure leaving failed / later children for destruction. No synchronous sibling Cancel in branch. | none |
| R4 | 0054FB18..0054FB22; 0054FBBC..0054FBD0 | Retry category 4 with retry ->decrement, virtual Reset(true), RUNNING; rest not updated this tick. Ignore not consulted first. No retry ->R3 with full result. | reset depends concrete compound |
| R5 | table0054FB12; 0054FB48..0054FB60 | Category >4 branches FB48 without iterator advance in that branch; can update same child again. UNKNOWN shipped child reachability of such category. Do not invent malformed-input failure/skip; no claim ordinary reachable deadlock. | none |
| R6 | 0054FBCC; 0054EB78; 00541238 | Parent failure ->outer Prep ->queue game snapshot ->compound child release ->base Stop/unlock/watch. Shared refs can defer destruction. Generic Cancel siblings changes child results/watch/mood and isn't supported by this base path. | none |

## H — C# entries that should host each piece

Placement proposals only; no implementation here. Lines refer to the checkout inspected. Shared queue / runner / watcher code should sit beside these entries after inventory approval.

| Step | Current C# entry | Native pieces and build boundary |
|---|---|---|
| H1 | cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs:1186,1224,1734 | A/T/Q/C/D: Robot-owned ActionList at existing ActionRunnerUpdate hook after AI, before streamer. One shared service for all action kinds. Concrete +484 component types remain UNKNOWN. |
| H2 | cozmo-stack/src/Cozmo.Robot/Motion.cs:54,770,827,898 | MoveAction / RunAsync / UpdateActions: concrete motor Init and CheckIfDone behind shared L runner. Current RunAsync stamps time, locks and sends at the API call; native queue replacement precedes lock take and start time is the first internal tick. Motor wire byte id differs from runner uint32 tag. |
| H3 | Motion.cs:279..296 | Track ownership primitives for L5-L6/L14-L16/P7: mask/owner checks, signed lock/unlock strings versus unsigned destructor stop-owner string, head/lift/body stop order and +56. Do not substitute a local Locked flag for ownership. |
| H4 | Behavior/SteppedBehavior.cs:776,794,845,952,974 | StartActing / ActingEnded / RunTriggerAction / wait / condition submit shared runners and attach native L12 completion callbacks. Local behavior handle is not the watcher event source: deletion can emit watcher record without UpdateInternal callback. Behavior-specific Stop / handle clearing needs its own inventory. |
| H5 | Behavior/SteppedBehavior.cs:989,1030,1049 | StartParallel / ParallelAction.ChildDone currently aggregate counts/outcomes and call CancelParallelChildren. P/S/R require ticked child runners, full results, pointer predicates, callback-before-ignore, retries, union cache and shared ownership teardown. Preserve result before adapting to ActionOutcome. |
| H6 | Behavior/BehaviorManager.cs:165,299,414,873 | ActionListIsEmpty must use A7 queue-map size. T12 samples before queue tick and uses sticky OR; cancelled empty queue still counts until erased. No instantaneous empty-only reaction gate. |
| H7 | Behavior/Mood.cs:420,445; Mood Init / FreeplayStack lifetime | Existing HandleActionEnded(string type,string category,string id,double time) needs W's generic live caller. Register during Mood Init, retain handle, unregister on disposal. Adapt native type/category/tag without guessing from booleans; preserve suppression-set erase before lookup. No external-interface requirement. |
| H8 | Manipulation/DriveActions.cs:151,164,231; DockActions.cs:54,63 | Concrete CheckIfDone is a candidate IAction method. Independent RunAsync polling / DockCompound async foreach cannot supply generic queue timing. Route through shared Robot ActionList; async API can await native callbacks without a second clock/cancellation policy. |
| H9 | Manipulation/FlipBlockAction.cs:86,120,182,199,384; ChargerActions.cs:338,420,509 | Lift/drive and turn/lift/backup instantiate shared compounds using recovered child inputs/order/predicate. Sequential await misses S8 same-tick asymmetry. Carry-lift cancellation uses current tag C5, not inferred Task cancellation. Concrete payloads / Init remain separately inventoried manipulation methods. |

## UNKNOWN boundaries and resolved data check

| ID | Boundary | Build consequence |
|---|---|---|
| U1 | Concrete runtime types of robot+484 virtual collection are not established by this Update body; placement T10 is established. | Preserve position; do not invent class names. |
| U2 | This generic surface does not establish every concrete override of Init / CheckIfDone / profile / delay / timeout / completion union / derived destructor. | Use approved concrete inventory or extract override; no stand-in success or Task delay. |
| U3 | Arbitrary supplied ignore callable beyond the known always-true bool overload. | Recover that callable; preserve full result, child pointer and consultation order. |
| U4 | No extra safe callback-mutation / exception / overflow / malformed-category policy established here. Ordinary native branches are recorded. | Do not add snapshot iteration, clamp, catch, result normalization or once-only callbacks as recovered behavior. Trace actual caller / STL contract if needed. |
| U5 | RESOLVED: ELF data01051020=002DC6C1 via GOT0103EB14, named IActionRunner::sTagCounter. | Use shipped global seed and C1 increment/wrap/collision, not a per-robot seed. |
| U6 | External Broadcast transport delivery guarantee and additional concrete derived destructor effects are outside this surface. | Generic queue evidence cannot settle the whole live wire / concrete action path. |

## Source-derived regression oracles (no tests added or run)

| Case | Source expectation |
|---|---|
| NOW with / without pending | Running old action remains RUNNING in nonempty branch, becomes CANCELLED in empty branch; stop/unlock before incoming lock. Q6/Q7/L15/D3. |
| NEXT with R current and A/B pending | A/new/B, no immediate Init. Q10. |
| Position2 | NOW_AND_RESUME; interrupted old state03000009, new/old/residual pending, no destruction event for interruption alone. Q13/Q14. |
| Cancel all | Current CANCELLED unless NOT_STARTED; pending NOT_STARTED; map-empty change delayed until T5. C3-C6/A7. |
| No game interface | Watcher still queues destruction event; nonnull robot still registers Mood callback. D2/W10/W13. |
| Before first full state / vision failure | No action lock/Init/start timestamp until actual tick. Timeout equality uses f32. T1/T2/L7-L10. |
| Tracks locked | 03000019, no Init; Delete Preps despite early runner return. L5/D2. |
| +56 | Skips lock/unlock, not Init/Check/event; destructor stop-owner checks independent. L5/L15/L16. |
| Sequential A/B/C, zero delay | A/B same tick, C next; B immediate failure bypasses ignore/retry lookup. S7-S9. |
| Ignored compound failure | Parent callback receives failure before predicate; later success can invoke callbacks again. S5/R3. |
| Parallel first failure | Later child not ticked; no generic sibling Cancel; destructor keeps actual child states. R3/P7. |
| Duplicate descendant results | Unique sorted full results; excludes root. W1/W2. |
| Cancel before manager / action tick | Empty queue-map entries still make IsEmpty false; latch remains set later. T12/A7. |
| High-bit tag | Signed lock/unlock owner versus unsigned stop-owner strings differ. L6/L15. |

## Delivery / fidelity impact

Production path traced: Robot Update -> ordered queue-map tick -> pending promotion -> watcher nesting -> runner profile/lock -> IAction clock/Init/Check or compound children -> full result -> Prep -> optional game snapshot -> virtual destruction (children, motor stops, unlock, watcher enqueue) -> optional game Broadcast -> queue erase -> watcher callback drain -> Mood handler. Immediate Cancel/Clear/NOW shares Delete/destruction without inventing an Update tick.

Changes: this report and native working extract only. Manifest impact: none applied. Manager must verify rows before inventory integration, including quoted M7 handler / M8 gate corrections. The four current IMPLEMENTATION_GAP statuses remain unchanged; no approval or hardware claim.

Commit: none, as required by research lane. Remaining uncertainty: U1-U4/U6, chiefly concrete virtual bodies and dynamic callable boundaries. Requested surfaces checked; UNKNOWNs are explicit, not substituted behaviors.
