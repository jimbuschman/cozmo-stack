| Review item | Coverage | Scope / remaining boundary |
|---|---|---|
| B-ACTIONS queue/tick 6539e66; synchronization follow-up298ec71 | CHECKED | ActionList/ActionQueue, rejection/deletion and tick iteration. |
| Runner lifecycle bb8c9d5; global counter2666e70 | CHECKED | Init/timer/retry/lock/destruction surface; tag-release omission below. |
| Compound construction/order/failure6639fd4 | CHECKED | Common and sequential/parallel row families, including reset and interrupt slots. |
| Watcher/completion/game/mood registration8e6c719 | CHECKED | Local watcher and serializer; higher-layer emotion recipient remains an interface. |
| B-FACE e0b2103 | CHECKED | Desired-degree parser/getter and live failure write; same-implementation expected RNG values flagged below. |
| Claim/job/status-only matching commits | CHECKED | 0100a8e,1398e06,b0c10a7,18ea788,7f552d2,5b86b68,f876450; no behavior diff. |
| Concrete batch3b child-action recipients | PARTIAL | Job explicitly blocked; not built by these commits and not expanded into upper layers. |
| Engine-to-game delivery sink / configurable 0x198 producer | PARTIAL | Explicit existing gaps, not asserted recovered by this review. |

Q4 of `requests/20261006-codex-queue-3.md`. Pulled main; reviewed through `6f13a5b` (production code unchanged from `b4ce02b`). Defects and circular tests only. No implementation, inventory or manifest edits. Findings below identify actual omissions/contradictions rather than restating a known absence of an app transport or a fixed ProceduralLive producer. Companion `20261006-B-ACTIONS-B-FACE-native.txt` reopens the cited native paths in the shipped engine. SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.

## Current manifest quotations before findings


```json
{
  "id": "M4-003",
  "title": "The head and lift API follows the game-message path: caller speed/accel/duration; the original app passes head 10/20 and lift 10/20, duration 0",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "location": "cozmo-stack/src/Cozmo.Robot/Motion.cs",
  "evidence": [
    "MA10 game SetHeadAngle overwrites +0x90..+0x98 with the message values (0x0052ABE0..0x0052AC24)",
    "MA11 unity/scripts/csharp/Robot.cs:1443-1450 (head 10, 20, 0) and 1638-1646 (lift 10, 20, 0)",
    "MA12 game SetLiftHeight: 32.0 while carrying -> PlaceObjectOnGroundAction (0x0052AC40..0x0052AC9E)",
    "MA9, MA13 action ctor defaults (head 15/20 at 0x00547F14..0x00547F28; lift 10/20 at 0x00548A68..0x00548A78)"
  ],
  "unresolved": "Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT. Stop-before-unlock and the AreAllTracksLockedBy gate hold (Motion.cs:904-919); the game path's ActionQueue::QueueNow replacement (0x0053E24C..0x0053E35E) is absent, so a repeated game move fails 0x03000019 (Motion.cs:831-844). The queue semantics are in Codex's 20260930-bcore-extractions.md (unchecked). Before: built, awaiting strong verification: B-CORE2 batch 4 (head/lift/action timeout). ~IActionRunner's teardown now stops the track before the lock release: RunAsync/UpdateActions stop (gated on AreAllTracksLockedBy(mask, owner) 0x00541138/0x0054115E) then unlock (0x0054120C..0x0054122A), and the lock owner is a per-action tag (the engine's to_string(+0x60), counter 0x0053FE54..0x0053FE68 store 0x0053FEC6) instead of the old constant. Citation corrected: 0x005408EC is IActionRunner::UnlockTracks, called only from the IAction constructor (0x00540CB0) and IAction::Reset (0x00540D02); the action's end release is inline in ~IActionRunner. Still out of scope: the QueueNow part (a repeated game move never gets 0x03000019) awaits Codex extraction. Tests: M4ControlTests.M4_003_MA_ALockedTrackFailsTheHeadMoveWith03000019, M4ControlTests.M4_003_MA_TheHeadMoveLocksThenUnlocksItsTrack, M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds."
}
```

```json
{
  "id": "M4-016",
  "title": "Head and lift move semantics: no send when in position, ack matched by id, completion in position and stopped, tolerances and error codes",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "location": "cozmo-stack/src/Cozmo.Robot/Motion.cs",
  "evidence": [
    "MA9, MA13, MA15..MA17, C1, C6, C10.1 as before",
    "C11.3 the lift CheckIfDone body 0x005493F6..0x00549508 contains no eye-shift removal; the head +0xA8 has only three writers, all zero (0x00547F3C, 0x005484B0, 0x00548724), so H4..H6 never execute"
  ],
  "unresolved": "Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT, confirmed by the Opus verifier. +0x74 starts negative and is set from the tick clock at the first UpdateInternal (0x00540D52..0x00540D64); Motion.cs:828 stamps it at RunAsync. Codex's \"second precondition-time gate\" (0x00540DAC..0x00540DC0) cannot fire for these actions: slots 0x24/0x28 return 0.0 (0x0052B0BA/0x0052B0BE), slot 0x2C returns 30.0f (0x0052B0C2). Before: built, awaiting strong verification: B-CORE2 batch 4 (head/lift/action timeout). The timeout is now tested on the engine clock (CozmoEngine.Timer.Seconds) from the action's Init/first UpdateInternal (IAction::UpdateInternal 0x00540D4A..0x00540D64) before CheckIfDone, run by Robot::Update's ActionList step (CozmoMotion.UpdateActions, hook 0x00540370/CD12), and fails with EngineResult 0x03000018 (0x00540E80); the wall-clock Task.Delay is gone. Strict IsNear (0x0084CC0A) and the 30.0 s default hold. Tests: M4ControlTests.M4_016_MA15_NothingIsSentWhenAlreadyInPosition, M4ControlTests.M4_016_MA16_MA17_CompletionIsTheAckThenInPositionAndStopped, M4ControlTests.M4_016_MA17_StoppingOutOfPositionIsStoppedMakingProgress, M4ControlTests.M4_016_C1_TheHeadInPositionIsLatched, M4ControlTests.M4_016_R1_PassingTheTargetBeforeTheAckDoesNotLatch, M4ControlTests.M4_016_C6_TheLiftCompletion, M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds, ControlTests.AnUnacknowledgedActionTimesOutRatherThanReportingSuccess."
}
```

```json
{
  "id": "M7-020",
  "title": "Mood event production, repetition penalty and affector application",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so plus shipped mood_config.json",
  "location": "cozmo-stack/src/Cozmo.Robot/Behavior/Mood.cs",
  "evidence": [
    "MoodManager::TriggerEmotionEvent 0x0067b85c",
    "MoodManager::AddToEmotion 0x0067bdca",
    "MoodManager::HandleActionEnded 0x0067c770",
    "EmotionAffector::ReadFromJson 0x00679910",
    "MoodManager::SendEmotionsToGame 0x0067b724"
  ],
  "unresolved": "built, awaiting strong verification: B-ACTIONS batch 4 built the ActionWatcher node tree, destruction-event deque and callback drain (W1..W12), RobotCompletedAction with the D8/D9 wire body, the ActionQueue deletion path (D1..D7), the runner watcher hooks (W8/W9/W10), the shipped RobotActionType/ActionResultCategory tables, and the MoodManager::Init registration in FreeplayStack.Create (W13/W14) with the W17 unregistration. Boundary (still open): the watcher node's name at +0x44 has no source on IActionRunner, so Node.Name stays null (W6); MoodState keys by the JSON string names (M7-012's documented stand-in); the native int-keyed model (full int32 actionType at record+4 and the category byte result>>24, 0x0067b318) and the forward-map miss fallback are not built. The RobotCompletedAction completion union carries only the 4-byte +0x1C cache value; the concrete ActionCompletedUnion variant is UNKNOWN (report U6). No commit (the worker does not commit). Tests: ActionCompletionTests. Not settled."
}
```

```json
{
  "id": "M7-017",
  "title": "The exact live-animation wire lifecycle used by idle behavior",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "location": "cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs",
  "evidence": [
    "AnimationStreamer::UpdateLiveAnimation 0x0057d5f8",
    "Update 0x0057ce5c",
    "InitStream 0x0057b674",
    "UpdateStream 0x0057c84c",
    "SendStartOfAnimation 0x0057c400",
    "SendBufferedMessages 0x0057bf60",
    "DesiredFaceDistortionComponent ctor 0x0063b3be, Init 0x0063b3d4, Params 0x0063b434, GetCurrentDesiredDistortion 0x0063b760 (research/20261004-procedural-live-extraction.md D01-D18)",
    "NeedsManager owns it at +0x3D4 (0x0069210c, 0x00692212); TrackLayerComponent::Update 0x0064ede8 reads it at 0x0057cf7a and compares with 0x3727c5ac before AddGlitch (0x0064edf8..0x0064ee14)",
    "LiveUpdateFailed 0x0057d084..0x0057d0e6 (log 0xbef148/0xbef172, _errG 0x0105DD34)"
  ],
  "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. B-FACE batches 1-3 (2026-10-04): built, awaiting strong verification. StreamLive no longer pushes ProceduralLive and the 'StreamLive' short-circuit is removed; the gated UpdateLiveAnimation port (M5-030) runs whenever ProceduralLive is on top; the S4 error path returns without the tail and now logs 'AnimationStreamer.Update.LiveUpdateFailed: Failed updating live animation from current robot state.' and sets the process-global _errG (0x0057d084..0x0057d0e6, 0x0105DD34); the idle clock +0x44 is zeroed while streaming and advances 60 per idle tick. The DesiredFaceDistortionComponent is built from D01-D18 (ctor 0x0063b3be, Init 0x0063b3d4, Params 0x0063b434, GetCurrentDesiredDistortion 0x0063b760), owned by NeedsManager at +0x3D4, initialised from needs_handlers_config.json's needsBasedFaceDistortion block with the robot context RNG, and wired through AnimationScheduler.DesiredFaceDistortion; the earlier 'DesiredFaceDistortion has no source' claim is contradicted by the extraction and is corrected here. Still open (out of scope, left as the visible gap): no fixed engine producer pushes 0x198 (PARTIAL in the extraction); it arrives through the PushIdleAnimation message (tag 0xAE, P09/P10) from the app, so it waits on the game-to-engine channel. The config parsers' asFloat of a present non-number key is UNKNOWN (the shipped config has numbers only), and what GraphEvaluator2d::EvaluateY returns for a 0-node graph is UNKNOWN (the C# evaluates a null graph as 0.0; the shipped config is non-empty)."
}
```

```json
{
  "id": "M8-001",
  "title": "The IBehavior lifecycle: constructor fields, Init, Update, Stop, StopActing, Resume, IsRunnable and IsRunnableScored",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "location": "cozmo-stack/src/Cozmo.Robot/Behavior/IBehavior.cs",
  "evidence": [
    "IBehavior::IBehavior 0x005bbc38 stores behaviourID at +0x3c, behaviourClass at +0x64, needsActionID at +0x68; zeroes +0x24/+0x28, the flat score at +0x100 and +0x104 (strd r8,r8,[r4,#0x100] 0x005bbd28); sets +0x108 = 0, +0x10c = 0x29 (invalid behaviour objective), and +0x110 = +0x111 = 1 (movw r0,#0x101 0x005bbd30, strh.w r0,[r4,#0x110] 0x005bbd34); constructs the repetition graph at +0xe8 and the running-penalty graph at +0xf4 (GraphEvaluator2d::GraphEvaluator2d 0x005bbd12/0x005bbd22); calls ReadFromJson 0x005bbd40 and ScoredConstructor 0x005bbe32.",
    "IBehavior::Init 0x005bcb54 walks the robot's ActionList at robot+0x250. action+0x60 is the action's 32-bit tag; the guard sets its flag when a tag is > 0x2dc6c0 (movw r6,#0xc6c0 0x005bcbd0 / movt r6,#0x2d 0x005bcbda; cmp r2,r6 0x005bcbec; movhi r5,#1 0x005bcbf0). sTagCounter at 0x01051020 starts at 0x002dc6c1, so every engine-constructed action tag is >= 0x2dc6c1, while an action tagged through the game path (IActionRunner::SetTag 0x00540098, called from HandleActionEvents 0x00525ab0 and the QueueSingleAction/QueueCompoundAction handlers 0x00527a78/0x00527c16) carries a tag <= 0x2dc6c0. The warning IBehavior.Init.ActionsInQueue at 0x005bcc84 fires only when at least one engine-tagged action is present; its count is the main queue's (map key 0) current action plus queued actions (ldr r1,[r0,#0x14] 0x005bcd64, ldr r0,[r0,#0x20] 0x005bcd66, addne r0,#1 0x005bcd6c).",
    "IBehavior::Init spark gate +0xd8: ldr r0,[r0,#0x44] 0x005bccac; if [r0+0x58] != 0x55 then +0xd8 = ! [r0+0x5c] else 0 (0x005bccb0..0x005bccc2). It then clears +0x98, writes halfword 0x0100 at +0xa0 (so +0xa1 = running = 1, +0xa0 = 0), stamps the running-penalty clock +0x34 = BaseStationTimer::GetCurrentTimeInSeconds(), clears the idle-animation flag +0xb0, and calls vtable+0x48 (IsRunnableInternal): non-zero clears +0xa1, otherwise +0x80++ (0x005bccbe..0x005bcd0c). If the unlock id +0x70 is not 0x55 and equals robot+0x44->0x58 it takes the SparkBehaviorDisables lock through SmartDisableReactionsWithLock and clears +0x114 (0x005bcd16..0x005bcd58).",
    "IBehavior::Update 0x005bd074 calls vtable+0xc (UpdateInternal) unless byte +0xa0 is set and the current-action handle +0x84 is zero, in which case it returns 2 (ldrb.w r1,[r0,#0xa0] 0x005bd074; cbz r1 0x005bd078; ldr.w r1,[r0,#0x84] 0x005bd07a; cbz r1 0x005bd07e; movs r0,#2 0x005bd088).",
    "IBehavior::Stop 0x005bd08c logs, stops any helper, clears +0xa1 (0x005bd10c), calls vtable+0x54 (StopInternal, 0x005bd110/0x005bd114), stamps the last-run clock +0x30 = BaseStationTimer::GetCurrentTimeInSeconds() (0x005bd11a/0x005bd11e), calls StopActing(0,0) (0x005bd126), then undoes the scope in this order: (a) remove disable-reactions locks while +0xac != 0 (0x005bd12c/0x005bd136), (b) remove the idle animation if +0xb0 (0x005bd148), (c) clear the motion profile if +0xc0 (0x005bd158), (d) walk the track-lock map at +0xb4 and MovementComponent::UnlockTracks(mask, name) each, then destroy the map (0x005bd16c/0x005bd174).",
    "IBehavior::StopActing 0x005bd34c calls vtable+0x80(0) (blx r2 0x005bd360); when viaCallback is false and a helper is live it logs \"Stopping behavior helper because action stopped without callback\" and calls StopHelperWithoutCallback (0x005bd39c/0x005bd3d6); then, if +0x84 is set, cancels it in ActionList (0x005bd3f0) and clears +0x84 unless keepAction.",
    "IBehavior::Resume 0x005bceac: for any trigger other than 0 (CliffDetected) or 0x14 (UnexpectedMovement) it takes the normal resume path (cmp r5,#0x14 0x005bcf16; cmpne r5,#0 0x005bcf1a; bne 0x5bcf7e 0x005bcf1c). For those two it reads +0x114, increments it and takes the normal path while the pre-increment value is < 1 (ldr.w r0,[r4,#0x114] 0x005bcf1e; cmp r0,#1 0x005bcf22; str.w r1,[r4,#0x114] 0x005bcf28; blt 0x5bcf7e 0x005bcf2c); otherwise it sets +0x118 = now + 15.0, builds \"TooManyResumesCliffOrMovement\" and calls MoodManager::TriggerEmotionEvent(name, now), returning 1 (0x005bcf36..0x005bcf7a). The normal path sets +0xa2 = 1, stamps +0x34 = now, calls vtable+0x4c (ResumeInternal), clears +0xa2; on a zero result sets +0xa1 = 1 and, when +0x70 != 0x55 and equals robot+0x44->0x58, takes the SparkBehaviorDisables lock; on a non-zero result clears +0xa1 (0x005bcf80..0x005bcfde).",
    "IBehavior::IsRunnable 0x005bd750 calls IsRunnableBase(this->robot); a result other than 1 returns 0, otherwise it tail-calls vtable+0x50 with the caller's robot (0x005bd756..0x005bd770). IBehavior::IsRunnableScored 0x005bda28 returns 1 when the current time is >= the suppression stamp +0x118, else 0 (0x005bda38/0x005bda3e/0x005bda46/0x005bda48). IBehavior::IsRunnableBase 0x005bd778: +0xa1 (running) returns true; otherwise the AI process (robot+0x264), the robot state byte +0x74 against 3, the unlock id +0x70 (0x55 bypasses) through ProgressionUnlockComponent::IsUnlocked(robot+0x448, id, true) (0x005bd810), and the float timers at +0x78.",
    "ReactionTrigger ordinals (table 0x10337c0; EnumToString 0x0077065c): 0 = CliffDetected, 0x14 = UnexpectedMovement, 0x15 = Count, 0x16 = NoneTrigger (ReactionTriggerFromString 0x00770674 confirms the same mapping).",
    "StopWithoutImmediateRepetitionPenalty 0x005beea0 has exactly three callers, all concrete-behaviour UpdateInternal methods, none of them IBehavior::Stop/StopActing/FinishCurrentBehavior: BehaviorPickUpCube::UpdateInternal (blx #0x4b3384 0x005c685c, then movs r0,#2 0x005c6860), BehaviorStackBlocks::UpdateInternal (0x005c991c/0x005c9920), BehaviorBuildPyramidBase::UpdateInternal (0x005dd110/0x005dd114). Those behaviours are M7/M15, not M8.",
    "IncreaseScoreWhileActing 0x005bf02c has ten callers, all concrete behaviours (KnockOverCubes, PopAWheelie, PutDownBlock, RollBlock, StackBlocks and the FUN_0059ec54 helper that pairs it with StartActing); ScoredActingStateChanged 0x005bf044 (clears +0x104) has no engine caller at all (exhaustive bl/blx and data-word scan). IBehavior::IsRunnableScored 0x005bda28 likewise has no engine caller and is not in the IBehavior vtable (0x010264e0); only its .dynsym st_value 0x005bda29 exists."
  ],
  "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. the base vtable (0x010264E0) slot +0x20 is 0x5BF04C (returns 0) and +0x28 is __cxa_pure_virtual, while RunnableGate20/24/28 return true with MISSING (SteppedBehavior.cs:232-236); ReactionTriggerTransition, UpdateRobotPropertiesForReaction and the cube-light stop are MISSING (BehaviorManager.cs:788-792). Before: built, awaiting strong verification: R-BEH2 batch 1 rebuilt the lifecycle (Init calls InitInternal = vptr+0x48; ScoredActingStateChanged(true/false) at StartActing/StopActing/HandleActionComplete; StartActing refusal gates; the +0x98 callback destroyed by Stop/Init; Update returns 2 before UpdateInternal; resume counter, +0x118 = now+15.0f and the TooManyResumes mood event applied to non-stepped behaviours too; switch order with current null during Init). Still open: the IsRunnableBase gates other than robot+0x355 and [robot+0x284]+8 (0x34a, AI process, +0x70/+0x74 inputs, timers, spark) are seams, and the base vtable+0x20/+0x24/+0x28 default to true although the engine base returns 0/0/pure virtual (about a dozen M10/M15 classes would be refused off treads; list in research/20261002-R-BEH2-verify-batch1.md Round 2); SetRunningAndResumeInfo's ReactionTriggerTransition message, robot+0x2c7/MC+0xD4 writes and cube-light stop are MISSING seams; the Init action-tag guard has no production caller; the normal resume path for non-stepped behaviours is not the engine's; CallToListeners and helper-stop are silent seams. Verifier rounds 1-4 in that report."
}
```

## DEFECTs

| Finding | Engine behavior and reopened instructions | C# file:line / observable consequence |
|---|---|---|
| A1: reserved tags survive runner destruction | `0x005410FA..0x00541114`: erase current `+0x60` then original `+0x5C` from the global in-use tree, **before** track stop/unlock and watcher enqueue. Constructor reserves and SetTag collision checks use that same tree. | `cozmo-stack/src/Cozmo.Robot/Actions/IActionRunner.cs:431`: WatcherEnding stops/unlocks/enqueues but releases neither tag. Its sole Global.Release call is SetTag at387. After action deletion, a new action cannot reuse the released external tag: C# returns BAD_TAG `0x03000006` where native uniqueness permits it. This is a lifecycle omission, not an OS policy. |
| A2: rejected actions lose their watcher destruction events | Native QueueAction rejection `0x0053D972..0x0053DA9A` deletes via key0 queue; base runner already owns Robot `+4` from ctor `0x0053FDDA`. Destructor `0x00541230..0x00541238` follows Robot+0x250+0x10 to ActionEnding regardless of game interface. | `cozmo-stack/src/Cozmo.Robot/Actions/ActionList.cs:158` discards **before** Watcher is assigned at167. `ActionQueue.cs:239` obtains watcher from that still-unbound runner, and `IActionRunner.cs:441` only invokes Watcher?.ActionEnding. A newly constructed ActionRunner with BAD_TAG, or an external tag while ExternalActionsDisabled, therefore creates no completion-to-watcher event; native does. The fake-action Q2 test checks deletion/map occupancy, not this recipient. |
| A3: queue current/list ownership is cleared after broadcast | `0x0053FA78` invokes destructor; `0x0053FA7C` nulls caller runner reference; `0x0053FA86..0x0053FA9C` unlinks pending node/decrements count; **then** `0x0053FAAA..0x0053FAB2` constructs/broadcasts completion. Guard-tag erase remains after broadcast. | `cozmo-stack/src/Cozmo.Robot/Actions/ActionQueue.cs:242` broadcasts inside DeleteRunner; callers null current at85/101/111/174/203, or unlink pending, only after it returns. A synchronous game sink inspecting GetCurrentAction/Pending sees the destroyed runner still owned, unlike native. Reentrant QueueAction/Cancel also sees a different current/pending topology. The code implements “destructor before broadcast” but omits the intervening null/unlink steps. |
| A4: queue erasure and traversal are delayed/invalidated | `0x0053F5A8..0x0053F5D6` checks and erases each empty queue **inside** the tree traversal; erase returns next iterator. Nonempty successor is found from live tree links `0x0053F5B2..0x0053F5CC`. No vector/snapshot or version check occurs. | `cozmo-stack/src/Cozmo.Robot/Actions/ActionList.cs:114` uses a fail-fast SortedDictionary enumerator and collects keys for later erase at118..124. A later runner callback observes an earlier empty parallel key still occupied and allocates a different lowest free key. A callback that inserts an IN_PARALLEL queue invalidates the enumerator, producing InvalidOperationException on continuation instead of native live-tree traversal. The reentrant lock does not stop same-thread callbacks from inserting. Completion callbacks execute within UpdateInternal before traversal continues (`0x00540EEA..0x00540F20`). |
| A5: completion union replaced with arbitrary four-byte wire/cache value | Native record owns a40-byte tagged ActionCompletedUnion at+0x18. `0x00715E7A..0x00715E80` calls that union's Pack; `0x0075C8E0..0x0075C8EE` writes **one tag byte**, then `0x0075C8F2..0x0075C944` dispatches variant payload packing. DefaultCompleted constructor `0x0075C838..0x0075C83C` sets tag6; shipped `unity/scripts/csharp/Anki.Cozmo/DefaultCompleted.cs` has zero-byte payload. INVALID likewise has no dispatched payload. Base GetCompletionUnion `0x0052B0A6..0x0052B0AE` copies the cached union via its recipient, not a four-byte integer field. | `cozmo-stack/src/Cozmo.Robot/Actions/RobotCompletedAction.cs:41` writes U32(CompletionUnion), while `IActionRunner.cs:160` stores a uint cache and compounds cache/proxy that uint. For DefaultCompleted the native union bytes are `06`, not `06 00 00 00`; for INVALID they are `FF`. A named UNKNOWN variant does not authorize emitting a guessed scalar. The existing transport sink is still missing, so this wire defect is currently in the built packer rather than a demonstrated robot/app capture. |

The A4 callback-insertion case is a specific missing branch, not a claim that any arbitrary native callback mutation (especially deleting its own iterator) is safe. The native tree walks cited establish insertion and earlier-key erasure behavior; no new safe-mutation policy is inferred.

## Circular or unsupported tests

| Test / line | Why it cannot verify the claimed source behavior | Independent source expectation |
|---|---|---|
| `cozmo-stack/tests/Cozmo.Protocol.Tests/ActionCompletionTests.cs:413`,420,435 | Expected lengths and trailing four bytes are chosen from the new uint-cache design, despite the comment claiming D8/D9. The test at408 accepts DEADBEEF as a “union”; no shipped variant has been established for that value. This is an unsupported design baseline in addition to A5. | Native tagged pack `0x0075C8D8..0x0075C946`: one byte tag then variant payload. DefaultCompleted tag6 and zero payload (native ctor above / shipped generated class). With two subresults a default body length is22, not25; with256 results it is1038, not1041. |
| `cozmo-stack/tests/Cozmo.Protocol.Tests/DesiredFaceDistortionTests.cs:53..55` (assertion55; callsites79,99,134,155,168,262) | Expected RNG state comes from a fresh instance of the same EngineRandom implementation. This can test relative consumption but a shared RNG defect passes both sides; it is not an independent native expected-value oracle. | `0x0063B804..0x0063B884`: successful sample consumes degree draw then cooldown draw; each GetNextDbl consumes two MT words. A blocked/cached getter consumes none. Use native-produced next-word/bits fixtures, not this class on both sides. |
| `cozmo-stack/tests/Cozmo.Protocol.Tests/DesiredFaceDistortionTests.cs:216..220`,226..227,246..258 | Sampled degree/deadline expected values use the same production EngineRandom seed/draw code. Writing the native bound formula around that generator does not independently establish its output. | Bounds and sample storage are binary32 at `0x0063B804..0x0063B83C`, `0x0063B840..0x0063B884`, `0x0063B94C..0x0063B954`; draws use binary64 `0x0082FA48..0x0082FA72`. Expected sampled bits need captured/emulated shipped RNG+getter results. This review does not fabricate those bits by running the C#. |

No new regression code was added in the research lane. These omissions require an independent corrected build and checked source-derived expectations; passing3946 existing tests does not remove them. PARTIAL boundaries in the coverage table remain explicit and do not hide any unexamined task as finished.
