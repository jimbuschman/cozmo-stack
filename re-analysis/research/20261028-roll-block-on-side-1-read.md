# ROLL-BLOCK-ON-SIDE-1 — exact configured own-path source packet

2026-10-28. Answers `jobs/trial-m13/ROLL-BLOCK-ON-SIDE-1.md`. Reader startup requested explicit `gpt-6.1-sol` / medium, fork_none; actual runtime telemetry unavailable. PROJECT_STATE.md was read first, then AGENTS.md, AUTONOMY.md, BEHAVIOURS.md, current REVIEW.md and research README. This is extraction only: no production/tests/official records/PROJECT_STATE/REVIEW/Claude-file/git/hardware changes.

Primary artifact: `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (fresh hash match). Python executable used: `C:/Users/JimBu/AppData/Local/Programs/Python/Python313/python.exe`. known.py lookup queries included RollBlock, 0076B340, 0076A2F4, 005BBFB4, 005B023C, 00782DE4, 00794D84, 005B07BC and the parser/base-gate names. Fresh native dumps are `.scratch/roll-side-bindings.txt`, `roll-side-native.txt`, `roll-side-enums.txt`, `roll-side-gates.txt`, `roll-side-socialize-recipient.txt`; these contain decoded ELF instructions/data, not decompiler evidence. Missing decomp files were navigation-only attempts and supplied no claim.

## Current ownership quotations

These title/status/evidence fields were read directly from the CURRENT 713-record manifest before the ownership conclusions below. They are snapshots, not proposed official changes. An address repeated by a broad record does not make that record own every caller.

```json
{
  "id": "M7-018",
  "title": "Shipped behaviour configuration and BehaviorClass factory binding",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "178-config table in M7 inventory Appendix C",
    "79-class entry-point table in Appendix B",
    "BehaviorContainer::CreateBehavior 0x0059c888",
    "class 0x39 BehaviorWait case constructs base IBehavior and installs the BehaviorWait vtable"
  ]
}
```

```json
{
  "id": "M7-002",
  "title": "The shipped animation/reaction maps, behaviour configs and activity tree",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/obb/assets/cozmo_resources/assets/animationGroupMaps/AnimationTriggerMap.json (573 pairs)",
    "re-analysis/obb/assets/cozmo_resources/assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json (40 pairs)",
    "re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/reactionTrigger_behavior_map.json (22 rows over 21 triggers)",
    "RobotDataLoader::LoadReactionTriggerMap 0x00520bc8",
    "BehaviorManager::InitReactionTriggerMap 0x005a16e4",
    "178 shipped behaviour configs enumerated in re-analysis/inventory/M7-behaviour.md Appendix C"
  ]
}
```

```json
{
  "id": "M8-001",
  "title": "The IBehavior lifecycle: constructor fields, Init, Update, Stop, StopActing, Resume, IsRunnable and IsRunnableScored",
  "status": "IMPLEMENTATION_GAP",
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
  ]
}
```

```json
{
  "id": "M8-011",
  "title": "The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "SmartPushIdleAnimation 0x005be41c: if the idle-animation flag +0xb0 is already set it fails through sVerifyFailedReturnFalse (0x005be448 b.w 0x8cbe5c); otherwise AnimationStreamer::PushIdleAnimation(trigger, name) and +0xb0 = 1 (0x005be464..0x005be478).",
    "SmartRemoveIdleAnimation 0x005bd4c8: when +0xb0 is clear it raises VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set through sVerifyFailedReturnFalse (cbz r0,#0x5bd508 0x005bd4d4; string 0x005bd51e; b.w 0x8cbe5c 0x005bd526); otherwise AnimationStreamer::RemoveIdleAnimation(name) and +0xb0 = 0 (0x005bd4ec/0x005bd500).",
    "SmartSetMotionProfile 0x005be518 verifies +0xc0 == 0 (sVerifyFailedReturnFalse 0x005be52a), calls PathComponent::SetCustomMotionProfile (0x005be534) and sets +0xc0 = 1. SmartClearMotionProfile 0x005bd584 verifies +0xc0 != 0, calls PathComponent::ClearCustomMotionProfile (0x005bd59c) and clears +0xc0.",
    "SmartLockTracks 0x005be5bc emplaces (name -> mask) into the map at +0xb4; on a new key it calls MovementComponent::LockTracks(mask, name, reason) and returns 1; on an existing key it warns \"Attempted to lock tracks with key named %s but key already exists\" and returns 0 (0x005be600/0x005be624/0x005be636). SmartUnLockTracks 0x005be6e0 looks the name up, calls MovementComponent::UnlockTracks(mask, name), erases the entry and returns 1, or warns and returns 0 (0x005be6ee..0x005be716).",
    "SmartSetCustomLightPattern 0x005be7f0: if the ObjectID is already in the vector at +0xcc it logs on the \"Unnamed\" channel and returns 0; otherwise CubeLightComponent::PlayLightAnim(objectID, trigger, callback, true, lights, timeout) and appends the ObjectID to the vector at +0xd0, returning 1 (0x005be806..0x005be8cc).",
    "SmartRemoveCustomLightPattern 0x005be9b0 searches the vector at +0xcc for the element whose +4 equals objectID+4; not found logs \"No custom light pattern is set for object %d\" and returns 0 (0x005bea4e/0x005bea74); found calls CubeLightComponent::StopLightAnimAndResumePrevious(trigger, objectID) for each trigger and erases the element, returning 1 (0x005be9e8/0x005bea34).",
    "SmartDelegateToHelper 0x005beb10 logs on the \"Behaviors\" channel, warns and calls StopHelperWithoutCallback() when the current helper +0xc8 is already live (0x005beba4/0x005bebe8), then calls BehaviorHelperComponent::DelegateToHelper on [robot+0x264]+0x10 (0x005bec2c); on success it stores the helper as a weak ref at +0xc4/+0xc8 through __add_weak at 0x005bec6e, otherwise logs \"SmartDelegateToHelper.Failed\" (0x005bec8c/0x005becb2).",
    "SmartDisableReactionsWithLock 0x005bce3c appends \"_behaviorLock\" to the name, calls BehaviorManager::DisableReactionsWithLock(manager, name+\"_behaviorLock\", table, true) (0x005bce62) and inserts the original name into the per-behaviour set at +0xa4 (0x005bce7e). SmartRemoveDisableReactionsLock 0x005bd470 appends the same suffix, calls BehaviorManager::RemoveDisableReactionsLock (0x005bd48c) and erases the name from +0xa4 (0x005bd4a4). The manager side is M7-014.",
    "SmartDelegateToHelper 0x005beb10 calls BehaviorHelperComponent::DelegateToHelper, whose body is 0x0056dad8..0x0056db37 (PLT 0x004b29d0) at AIComponent+0x10 ([robot+0x264]+0x10, AIComponent constructed at 0x00569aae/0x00569ab2): it clears stack-maintenance vars, copies the two callbacks to +0x10/+0x28, and on an empty helper stack pushes the helper and stores Robot::GetWorldOriginID at +0x40, returning 1. R-BEH C2 read the component and its helper-stack runtime in full (report re-analysis/research/20260929-R-BEH-M8-framework-gap1-extraction.md); the component and the M7/M15 helper subclasses are still unbuilt, so the record stays IMPLEMENTATION_GAP."
  ]
}
```

```json
{
  "id": "M8-012",
  "title": "BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate",
  "status": "IMPLEMENTATION_GAP",
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
  ]
}
```

```json
{
  "id": "M8-013",
  "title": "The behaviour choosers and the activity selection dispatch",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060a44a walks its behaviour map, calls EvaluateScore(robot) on each (0x0060a44a), skips scores <= 0 (vcmpe s0,#0 0x0060a452), adds a running-duration graph bonus for a running behaviour (GraphEvaluator2d::EvaluateY 0x0060a474) and a RandomGenerator::RandDbl tie-break for a non-running one (0x0060a4a8), and keeps the maximum. It logs \"behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f\" (string 0x0060a60a; sChanneledInfoF 0x0060a61c) and warns \"Looks like more than one behavior returned IsRunning(). One of them is '%s'\" (0x0060a550).",
    "SelectionBSRunnableChooser::GetDesiredActiveBehavior 0x0060ad64 chooses between the behaviour at +0x2c and the one at +0x34, gated by a countdown at +0x3c and a latch at +0x40; a running behaviour short-circuits the countdown (movs r6,#1 0x0060ad84; subs r1,#1 0x0060adbe; str r1,[r5,#0x3c] 0x0060adc0; strb.w r0,[r5,#0x40] 0x0060adc8).",
    "StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060b23e walks its behaviour vector at +0x1c..+0x20; a behaviour with +0xa1 (running) non-zero is returned immediately, otherwise the first behaviour for which IBehavior::IsRunnable(robot) == 1 is returned; if none, it returns a null shared_ptr (0x0060b242..0x0060b266). No scoring and no tie-break.",
    "IActivity::GetDesiredActiveBehavior 0x005b387c returns the activity's current behaviour when it equals the caller's; otherwise it calls the activity's GetDesiredActiveBehaviorInternal at vtable+0x30 (0x005b38c2) and, when the activity holds an interlude behaviour at +0x28 and the result differs, calls the interlude's method at vtable+0x28 (0x005b38f6), logging \"Activity %s is inserting interlude %s between behaviors %s and %s\" (0x005b399c).",
    "IActivity::GetDesiredActiveBehaviorInternal 0x005b3a6c reads the chooser at +0x24; a null chooser warns \"VERIFY(%s): ChooseNextBehaviorInternal called without behavior chooser overwritten\" and returns a null behaviour; otherwise it calls the chooser's vtable+0x28 (0x005b3a70/0x005b3aa2/0x005b3a94).",
    "The concrete activities implement GetDesiredActiveBehaviorInternal: ActivityFreeplay 0x005ae29c, ActivitySocialize 0x005b023c, ActivitySparked 0x005b1d18, ActivityStrictPriority 0x005b267c, ActivityBuildPyramid 0x005a8854, ActivityFeeding 0x005abdd8. Their bodies belong to M7/M15.",
    "SelectionBSRunnableChooser::GetDesiredActiveBehavior 0x0060ad64 full algorithm: +0x2c is the behaviour named by the last ExecuteBehaviorByID/ByExecutableType game message (set 0x0060ac2c/0x0060ac30), +0x34 is the Wait behaviour (BehaviorID 0xb2 resolved in the ctor 0x0060a988/0x0060a99a), +0x3c is the message's numRuns (ctor writes -1 unlimited, 0x0060a880/0x0060a884; message stores 0x0060aa88/0x0060aac8), +0x40 is a byte latch holding the previous call's running state (0x0060adc8/0x0060ae3a). It returns +0x2c while it is running or IsRunnable (0x0060ad96) and numRuns has not run out, decrementing +0x3c once per running->stopped edge; otherwise the same for +0x34 (0x0060ae08) but its countdown block runs only when +0x34 == +0x2c (0x0060ae10/0x0060ae14); otherwise a null shared_ptr (0x0060ae64).",
    "The selection chooser calls IBehavior::IsRunnable (PLT 0x004b0f54), not IsRunnableScored; IsRunnableScored has no engine caller."
  ]
}
```

```json
{
  "id": "M12-016",
  "title": "A roll gets three attempts",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x005B9EF8 StartRollingAction; 0x005B9F0A ldr.w r0,[r4,#0x124]; 0x005B9F0E cmp r0,#3; 0x005B9F10 blo 0x5b9f20; 0x005B9F16 blx 0x4B26AC (MarkTargetAsFailedToRoll)",
    "there is no charger helper in the build; the charger mount's one attempt is M13-008"
  ]
}
```

```json
{
  "id": "M12-029",
  "title": "The default +0x150 pose function, the DriveToHelper functor and RemoveMatchingPredockPose",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x005586AC setter; 0x0055DB72, 0x0055DBDE default functors; 0x00558C80 GetPossiblePoses; 0x005B61A2 the DriveToHelper functor; 0x00551418 RemoveMatchingPredockPose",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q9; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q9 (PASS)"
  ]
}
```

```json
{
  "id": "M13-014",
  "title": "Knock over a stack: BehaviorKnockOverCubes flow, DriveAndFlipBlockAction and IDriveToInteractWithObject",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorKnockOverCubes body 0x005C2EA0..0x005C3E2A; IsRunnableInternal 0x005C314C needs StackOfCubes::GetStackHeight() >= +0x124 (minimumStackHeight default 3); InitInternal 0x005C31A2 runs the reach unless +0xD9 (alwaysStreamline) or +0xD8 (the soft/hard switch flag) is set (0x005C31B4/0x005C31BA)",
    "TransitionToReachingForBlock 0x005C3254: TurnTowardsObjectAction(max pi), then DriveStraightAction(x-85, 60) when the block x+10 > 85.0, then TriggerLiftSafeAnimationAction(+0x150), then TransitionToKnockingOverStack regardless of result",
    "TransitionToKnockingOverStack 0x005C34A8: TurnTowardsObjectAction(max pi), DriveAndFlipBlockAction with maxTurn = pi/2 on the first attempt (0x005C36BC) and 0.0 once +0x140 > 0 (0x005C36C0); +0xD9 or +0xD8 also forces 0.0 (0x005C34EA..0x005C34F8); WaitAction(0.5); the callback 0x005C3DCE writes AIWhiteboard+0x70 on NoPreActionPoses (0x03000010), re-runs while +0x140 <= 1 on a Retry result and otherwise blind-flips, incrementing +0x140 either way",
    "success goes to TransitionToPlayingReaction 0x005C3908: it sets robot->[+0x34]->[+0x94]->[+0xC] = 1 (0x005C3946..0x005C394E), i.e. the BlockWorld's BlockConfigurationManager dirty flag (BlockWorld+0x94 is the manager; BlockConfigurationManager+0xC is read by its Update 0x00616D84) forcing all block configurations to recompute; the tipped-object set size at +0x14C selects the success trigger +0x15C with BehaviorObjectiveAchieved(0xD, true) and NeedActionCompleted(0) (0x005C3950..0x005C3964) or the failure trigger +0x160 (0x005C396E); when +0xD9 or +0xD8 is set the reaction animation is skipped (0x005C3972/0x005C3978)",
    "IDriveToInteractWithObject 0x0055B1F4 adds TWO actions when maxTurn > 0 (0x0055B37C..0x0055B392): a TurnTowardsLastFacePoseAction (vtable overwritten from TurnTowardsFaceAction at 0x0055B3C4..0x0055B3D8) and a TurnTowardsObjectAction (0x0055B42E/0x0055B43C), both with failure ignored; the trailing float 20.0 is not read by DriveAndFlipBlockAction ctor 0x0055E208",
    "+0xD9 is the JSON config key alwaysStreamline (IBehavior::ReadFromJson 0x005BC208/0x005BC216/0x005BC21E); +0xD8 is computed by IBehavior::Init 0x005BCCAA..0x005BCCC2 as 1 when the behaviour was entered by a hard spark switch and 0 for a soft switch or no current behaviour (corrected operator 2026-10-28: SetRequestedSpark 0x005A3ED4..0x005A3ED8 maps a non-zero bool to \"soft\" 0x00BF0B78 and zero to \"hard\" 0x00BF0B7D)",
    "0x0055B3C0..0x0055B3CC, 0x005DE160..0x005DE16C, 0x005B7DB2..0x005B7DC2 the vtable overwrite; vtable 0x010204F4 slots 2, 4, 6, 9, 10",
    "0x0054B7B6 the ctor store of +0x193; 0x005C229C, 0x005DE170, 0x005F20B8, 0x005F6942 the behaviour stores",
    "0x0054B978..0x0054B9F4 SetSayNameAnimationTrigger; 0x0054C606..0x0054C61E and 0x0054C2D0..0x0054C2E6 the NeedsManager registrations",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q4; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md objection A8 (the class adds no logic of its own)",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q2, Q3, Q6; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q2/Q3, Q6 (PASS)",
    "0x0055B2C8..0x0055B438 the IDriveToInteractWithObject ctor compound construction and its AddAction flags (manager read)"
  ]
}
```

```json
{
  "id": "M15-008",
  "title": "Desired activity from objects and behavior-owned needs-action reporting",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::CalculateDesiredActivityFromObjects 0x005ADF4C",
    "IBehavior::ExtractNeedsActionIDFromConfig 0x005BBAE8",
    "IBehavior::NeedActionCompleted 0x005BE40C",
    "ActivityGatherCubes::Update 0x005AF2F0"
  ]
}
```

```json
{
  "id": "M15-044",
  "title": "UnlockId name-to-value table and InitConfig details: UnlockIdFromString map builder; the root state after a failed readAsJson",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261013-backup-unlock-and-stoull-check.md: UnlockIdFromString static map builder E:0x00794D84..0x007957AC (undecoded); DataPlatform::readAsJson failure root state (PLT E:0x004A64E4) unread.",
    "re-analysis/research/20261013-small-reads-2-check.md: M15-044 section. UnlockIdFromString map 0x00794D84..0x00795834 (81 pairs, emulated; std::cerr on a miss); readAsJson 0x0084BFD4..0x0084C206; JsonCpp Reader 0x008E075A..0x008E12EC; InitConfig 0x0064BB68..0x0064BDB4; callers 0x0064B89E..0x0064B8C4, 0x0064C09C..0x0064C0C8."
  ]
}
```

```json
{
  "id": "M12-015",
  "title": "PlaceObjectOnGround sends three zero offsets and a constant speed triple",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x00632A88 PlaceObjectOnGround zeroes the offsets (0x00632AA6 str r2,[sp,#0x18]; 0x00632AA8 strd r2,r2,[sp,#0x10])",
    "0x00632B88 builder; 0x00632BAE/0x00632BB2/0x00632BB6 vldr; 0x00C7CD90/94/98 = 100/200/500"
  ]
}
```

```json
{
  "id": "M15-022",
  "title": "PlaceObjectOnGroundAtPoseAction composition and PlaceObjectOnGroundAction Init/CheckIfDone",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PlaceObjectOnGroundAction Init sends message 0x44 (0, 0, 0, 100, 200, 500 as raw bits), builds a TurnTowardsObject verify action and calls StopAllMotors; CheckIfDone never reads DockingComponent+5, its result is the verify action's (extraction sections 2.3..2.8; construction of PlaceObjectOnGroundAtPoseAction at 0x005DFF14)",
    "M12-015 owns only the wire message of PlaceObjectOnGround (its evidence does not cover the action)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ]
}
```

```json
{
  "id": "M12-042",
  "title": "PlaceRelObjectHelper lifecycle: ctor, Init (pre-action check, DriveTo delegation), StartPlaceRelObject (3-attempt counter), RespondToPlaceRelResult, MarkFailedToStackOrPlace",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: ctor 0x005B8F00..0x005B8F82; Init 0x005B8FD0..0x005B90FE; Start 0x005B9168..0x005B930E; Respond 0x005B94C0..0x005B952E; MarkFailed 0x005B93A8..0x005B9460; invokers 0x005B956A, 0x005B95D6."
  ]
}
```

```json
{
  "id": "M12-050",
  "title": "PlaceRelObjectAction Verify and GetCompletionUnion: dock-success and carry checks, the saved-carried turn child, high-place lift child, failure cleanup, and the ObjectInteraction completion payload",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Verify 0x005555B4..0x0055581E: selected dock action 2/3 checks, docking success and carry predicates, TurnTowardsObject saved-carried child, first/second Update sequence, failure cleanup, high-place MoveLift child.",
    "GetCompletionUnion 0x0055515C..0x00555344: the chain it builds is overwritten by the final object-interaction payload at 0x00555324..0x0055533E.",
    "Completion setters 0x0075C3F0..0x0075C436 / 0x0075C460..0x0075C4A6 copy the five 32-bit IDs, count and flag; Pack 0x0075B4F0..0x0075B536 serializes all five IDs, count and flag.",
    "Normal located-target path: IDs [target, 0, 0, phone stack address, 0], count 1, flag false (jobs/trial-m13/SPARKS-STACK-1-COMPLETION-POLICY.md).",
    "Read packet re-analysis/research/20261028-sparks-stack-1-read.md and its independent check; HANDOFF-SPARKS-STACK-1.md; re-analysis/research/20261028-sparks-stack-1-claude-check.md"
  ]
}
```

```json
{
  "id": "M4-018",
  "title": "CubeLightComponent: layers, PlayLightAnim gates, SetObjectLights/SetLEDs, the SetLights wire group (SetCubeGamma, CubeID, CubeLights, white balance), WakeUp, EnableGameLayerOnly, default-layer anims, the +0x2C4 hold",
  "status": "EXACT_SOURCE",
  "evidence": [
    "S5..S8, LC1..LC8, LC6, C10.3 rows S3..S7, W1..W5, P1..P5, D1..D5 as before",
    "C11.2 robot+0x2C4 is the robot-is-localized flag: default 1 (0x0050FF10), 0 by Robot::Delocalize (0x00510A4A), 1 by SetLocalizedTo (0x0051245E, 0x005124EE) and CheckAndUpdateTreadsState (0x0051217C); the RobotDelocalized +0x21 refresh is held while delocalized and applied when it returns to 1 (gate 0x00637930)",
    "S3 the cube-sleep flags are written only by EnableCubeSleep; the equivalent public API is Robot.cs:2090",
    "C12.1 the static ObjectLights for EnableGameLayerOnly(enable=1) is 0x0105B4A0, filled by _INIT_36 (0x004D7EB4..0x004D7EE5): eight u32 0x000000FF at +0x00..+0x1F, the rest zero",
    "C12.2 the enable=1 branch is live: game tag 0xBB EnableLightStates -> 0x0063A48A..0x0063A496 and 0x0063AFF8..0x0063B006 pass enable = (msg.byte0 == 0); the app sends it from Robot.cs:1902-1907",
    "C13.1 EnableGameLayerOnly branch detail: all-objects enable=0 stops layer 0 once (0x00639ADA/DE, 0x00639AE6) and sets comp+0x22 := 0 (0x00639B32); single-object enable=0 stops layers 0 and 2 (0x00639AA2, 0x00639AB0) and does not touch comp+0x22; enable=1 sets ObjectInfo+0x1C := 1 and (all-objects only) comp+0x22 := 1",
    "C13.4 EnableGameLayerOnly returns without acting when comp+0x22 (all-objects, 0x00639A16..0x00639A1C) or ObjectInfo+0x1C (single-object, 0x006399DC..0x006399E0) already equals enable",
    "Handoffs: PickNextAnimForDefaultLayer's BlockWorld::GetLocatedObjectByIdHelper blx 0x4A7114 at 0x00637DB4 (Visible needs the located object's +0x24 == 1), SetObjectLights' located lookup blx 0x4A7114 0x00637E82 and connected lookup blx 0x4A6FB8 0x00637E96, ActiveCube::MakeStateRelativeToXY blx 0x4B9288 0x00637EE8: M11-004; the +0x2C4 writers: Robot::Delocalize M11-053, SetLocalizedTo M11-044, CheckAndUpdateTreadsState M10-001",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 5 (Opus extraction, manager-adopted 2026-10-11): the full log rows (level, channel, key, format, arguments, gates, errG/break and order) and the differences from the C#.",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 13.2 (follow-up Opus extraction, manager-adopted 2026-10-11)."
  ]
}
```

```json
{
  "id": "M4-041",
  "title": "BlendAnimWithCurLights body, the StopLignAnim.Result serializer and the GetLEDState callers",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m3m4-settling-2.md: PlayLightAnim blend body (unread); StopLignAnim.Result current-state serializer (0x00639300); ActiveObject::GetLEDState IndexTooLarge/NegativeIndex (0x0063958E/0x006395CC) and its callers.",
    "re-analysis/research/20261012-m4-cube-lights-check.md section M4-041: Blend 0x00638B3C..0x00638BF0 (NullActiveObject channeled info 0x638C24/0xBFAA4C returns 0; per i=0..3 GetLEDState 0x00638B7C, zero on/off word -> rev of the LED word, flag; returns flag&1); StopLignAnim.Result debug 0x006390BA..0x00639300 (grammar and literals 0x6394EC..0x639524, layer table 0x0102EE64, found/did not find 0x639538/0x639528); GetLEDState 0x0063956C..0x00639600 (callers only 0x00638B7C and 0x0063A642; both warnings unreachable)."
  ]
}
```

```json
{
  "id": "M4-046",
  "title": "ApplyAnimModifier body and the PlayLightAnim callers that pass modifier = true",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m4-cube-lights-check.md: ApplyAnimModifier 0x00638A8C..0x00638B3A (only caller 0x006384F0) is unread; the callers of PlayLightAnim 0x006382D0 and its overload 0x006389FC that pass modifier = true are unscanned.",
    "re-analysis/research/20261012-m4-046-anim-modifier.md: ApplyAnimModifier 0x00638A8C..0x00638B3A: copy src to dst; per node, i = 0..3: on/off colours OR, the four periods and offset wrapping 32-bit add; rotationPeriod += mod+0x70 inside the i-loop (4x, 0x00638B04..0x00638B0E); mode byte := mod+0x74 (0x00638B1A); relativePoint := mod+0x78/+0x7C raw copy; duration, canBeOverridden and name unchanged; no logs. PlayLightAnim call 0x006384F0 and the modified list into Blend (0x00638502..0x00638506); the overload 0x006389FC forwards with layer 1; every caller passes int 0; callers table in the research file."
  ]
}
```

```json
{
  "id": "M1-034",
  "title": "Handler isolation, a deliberate departure: in the original a handler exception aborts the engine process",
  "status": "COMPATIBILITY_POLICY",
  "evidence": [
    "G3.1..G3.7 the dispatch path from the signal emit to ProcessArrivedMessages and ProcessMessages has only cleanup landing pads that end in _Unwind_Resume (LSDAs 0x00B3F07C, 0x00B3F124, 0x00B3EF3C, 0x00B28A14); the only catch(...) pads go to 0x004E39F8 = __cxa_begin_catch; std::terminate (0x004E39FA, 0x004E39FE)",
    "G3.8 no frame up to the engine thread top catches (CozmoEngine::Update 0x004ED62E, CozmoInstanceRunner::Run 0x0065B420, __thread_proxy 0x0065C280)",
    "G3.10..G3.14 the EH runtime is the shipped libc++_shared.so (NEEDED order libc, libm, libc++_shared); __cxa_throw with no handler calls std::__terminate with the default handler (libc++_shared.so 0x0007A948..0x0007A98C, 0x0007B20C, reloc 0x000A6010 -> 0x00068B7D)",
    "G3.15/G3.16 the default handler writes \"terminating with uncaught exception of type T: what()\" to stderr and aborts (libc++_shared.so 0x00068B84..0x00068C4A, abort_message 0x00068AA2..0x00068ACE)",
    "G3.17 the engine never installs another terminate handler: its only set_terminate call is set_terminate(nullptr) in cozmo_shutdown (0x00667862..0x00667864)"
  ]
}
```

```json
{
  "id": "M13-026",
  "title": "GetAbsoluteHeadAngleToLookAtPose and Robot::ComputeHeadAngleToSeePose",
  "status": "EQUIVALENT_IMPLEMENTATION",
  "evidence": [
    "0x0054B428..0x0054B55A, 0x00518344..0x00518780 (pools 0x00518744, 0x00518780)",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q3"
  ]
}
```

```json
{
  "id": "M4-052",
  "title": "Relative-mode SetObjectLights: located lookup, ActiveCube cast, SetLEDs and MakeStateRelativeToXY",
  "status": "EQUIVALENT_IMPLEMENTATION",
  "evidence": [
    "re-analysis/research/20261018-m3m4-settling-a.md: GetLocatedObjectByIdHelper(id, -1) 0x00637E82, dynamic_cast to ActiveCube, SetLEDs 0x00637EDA, then MakeStateRelativeToXY 0x004E0DEC called at 0x00637EE8. Producer: BuildPyramid (M15-037).",
    "re-analysis/research/overnight/20261018-ov128-m3m4-settling-narrow.md L1-L8, confirmed with corrections by re-analysis/research/overnight/20261018-ov128-check.md: lookup and ActiveCube cast for both modes (mode 0 via ActiveObject, 0x00637E96..0x00637EAA); SetLEDs zero-duration normalization 0x004E4902..0x004E49A2; MakeStateRelativeToXY 0x004E0DF4..0x004E100E with WhichCubeLEDs TBH 0x004E0E1C; GetTopMarker 0x004E69B8..0x004E6A7C; corner/face choice through Radians rescale/Near/GT/GE/LT (0x0084C87C..0x0084CD18, epsilon 0x3727C5AC); RotatePatternAroundTopFace 0x004E30A0: false -> [1,2,3,0] (0x00C45B94), true -> [3,0,1,2] (0x00C45B90), dest[table[i]] = src[i], 7 words, stride 0x1C; SetLights 0x0063A604 with CubeID's second byte from ObjectLights+0x70 (0xFF or floor((v+29)/30), 0x0063A7A2..0x0063A7BC).",
    "re-analysis/research/overnight/20261019-ov129-m4-052-compute-normal.md N1-N4, C1-C5, R1-R3 (manager-checked 2026-10-19): GetTopMarker's normal is KnownMarker::ComputeNormal 0x0087E3FA = (C1 - C0) x (C2 - C0) over Get3dCorners 0x0087E2E8 (canonical corners 0x0105E0F0 scaled by marker width/height in f32, then the supplied root pose's Transform3d::ApplyTo 0x0050343C, rotation 0x0084ACA6 -> 0x00849C1C); f32 per operation, products/differences in the order 0x0087E46C..0x0087E494; not normalized; no failure path."
  ]
}
```

## Exact inputs, native parser and actual factory

All numeric values below explicitly distinguish decimal from hexadecimal. Source finding `read` means the cited step is established; it is not an official EXACT_SOURCE status.

| Step | Behavior-changing contract | Primary citation | Ownership/classification |
|---|---|---|---|
| S1 exact shipped input | `behaviorClass=RollBlock`, `behaviorID=RollBlockOnSide`, `needsActionID=RollACube`, `displayNameKey=behavior.RollBlock`, `requiredUnlockId=RollCube`. No orientation bool, executableBehaviorType, alwaysStreamline, strategy or recent timer fields. Five values match Hiking_RollCube except the exact ID; no ID-name inference. | `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/rollBlockOnSide.json`:2–6. | M7-018/shared parser; read. |
| S2 ID parser/data | ExtractBehaviorID parses literal `behaviorID`, then BehaviorIDFromString. Native RollBlockOnSide byte is decimal99 = hex0x63. EnumToString uses XOR0x80, hence index0xE3; table BASE0x01033330 + index*4 gives CELL0x010336BC -> 0x00C1A735 `RollBlockOnSide`. Different LowScore is a separate ID. | 0x005BB9EE..005BBA02; 0x0076B32C..B338; map builder string0x0076C1F8, its following value0x63 at0x0076C218/228 belongs to Roll string +0x630 (preceding sp+62C store is prior entry). | M7-018/shared; read. |
| S3 class factory | Class string parsed/isString/asCString then BehaviorClassFromString. Class decimal49 = hex0x31: table BASE0x010331F0, CELL0x010332B4 -> 0x00C1A0E7 `RollBlock`. Actual factory allocates0x138 bytes, passes actual Robot/config to BehaviorRollBlock ctor and shared_ptr. | 0x005BBA70..BBAC8; 0x0076A2DC..A2EE; map string0x0076A914 and following value0x31 store0x0076A93A; factory0x0059CF86..CFA4. | M7-018; same derived class as Hiking; read. |
| S4 needs/unlock | Needs parser reads `needsActionID`, converts string, stores base+68. RollACube is decimal39 = hex0x27: needs table BASE0x01033D40, CELL0x01033DDC -> 0x00C1C7BF. Required unlock is decimal9 = hex0x09: unlock table BASE0x010340C0, CELL0x010340E4 -> 0x00C1C062. ReadFromJson parses unlock and stores+70. | 0x005BBAE8..BBB46, 0x005BBC9A..BCAA; 0x00783282..32A4 pair layout; 0x00794E64..4E86 pair layout; 0x005BBFEC..C010/0x005BC118. | M15-044 already settled enum ownership; needs M15-008; read. |
| S5 defaults | Own ctor default JSON true for `isBlockRotationImportant`, stores+128; hence cache7, never cache6 merely because ID says OnSide. Base executable+6C defaults decimal12=hex0x0C (`Count`, table BASE01033730 CELL01033760 -> C1966A); required severe need+74=3(Count), recent timers+78/+7C=-1f. D8/D9 initially zero; optional streamline absent retainsfalse. No manufactured executable Roll type. | 0x005C85DC `movs r1,#1`,0x005C85EA get,0x005C85F0 asBool,0x005C85F4 store; key0x005C8634. Base0x005BBCA0..BCC2,0x005BBCE6..BD06; optional0x005BC1B2..C200/0x005BC208..C21E; executable accessor0x0076D50C..D51E. | M8-001/shared; read. |
| S6 readiness/spark input | Same shared IsRunnable gate: running bypass, required process if any, severe need if configured, actual unlock9 IsUnlocked(true), configured timers, off-treads/on-charger/carry virtual gates, strategy and suppression stamp. Derived target gate then checks target or action. Active hard spark is computed from actual manager activeID!=0x55 and inverse soft flag. Ordinary parent asset does not prove falseD8; activeID0x55 givesfalse. | 0x005BD750..D770/0x005BD778..D968; unlock call0x005BD810; base hard-spark0x005BCCAA..CCC2. | M8-001; shared inputs remain explicit MISSING wherever producers unavailable; no readiness default. |

## Same native own path and exact inherited recipient joins

This exact caller selects the SAME concrete vtable and same arguments as checked Hiking. Reuse the independently checked source contracts listed below; this is not permission to reuse an ID-specific parent or fabricate inputs. Each row is source-read evidence for this caller. Full original primary tables and their independent checks remain frozen, with additive corrections taking precedence as named.

| Step | Exact own-path behavior / join | Primary citation / inherited checked row | Ownership |
|---|---|---|---|
| S7 ctor/target/cache | Target124=-1,129false, saved-axis12C=1,state130=0; IsRunnable updates target then true iff ID!=-1 or action84 exists. With S5 true, actual intention7 uses AxisCheck/NoAxisCheck and Roll picker. NoAxisCheck rejects failure3 at current pose within60s/30mm/PI, requires pose1/2, flat10deg, transform to actual MAIN, not too high(2,15,.5), no stack>=3. AxisCheck uses ROOT-flattened pose and rejects +Z axis+3. Picker's preference uses object's OWN-parent axis+3, CanPickUpObject and fallback original set; actual DefaultBest recipient unchanged. | 005C85B0..CC;005C8654..74;005C8CAE..C8;0056F9B4..FA18/0056FC72..7C/0056FDE4..FDFC;005707F0..083C/00570676..07C4/00570876..0902. Hiking H2–H5 checked; original10deg bits3E32B8C2, PI initializer004D7034..3E. | Pending generic Roll own-path proposal; shared cache/geometry boundaries unchanged. |
| S8 Init/perform | Clear129; targetmissing=>1 else save OWN-parent up-axis and Performing(false),return0. State130=0. Actual maxFaceAngle PI/2 unless D9/D8 then0. CreateRollBlockHelper(actualtarget,TRUE,params), retained pre-dock callback129=true, triggers1F6/1F7, SmartDelegate owned success/failure callbacks; score0.8; state lights one actualtarget/trigger25/modifierfalse with setter boolfalse. | 005C867A..6DE;005C8712..8814; helperbool005C8804/08;005C8846..50;005C8884..88E;005C88A6..CE. Hiking H6–H8. | Same pending Roll/shared helper/state-light proposals. |
| S9 live axis/distance | In state0 with located target, compare own-parent axis with saved. Only changed-axis AND successful MAIN-target distanceSQ>14400 triggers update axis/StopActing(false,false). If129 set=>RollSuccess; otherwise recompute target,valid=>Performing(false),invalid=>2. Other path calls base UpdateInternal. No target-displacement substitution. | 005C8A5E..B22, literal005C8B28. Hiking H9. | Pending Roll. |
| S10 success/stop | state1; RollSucceeded Mood event/currentMoodClock; refresh saved axis. Unless D9/D8, actual animation1FB loops1/interrupttrue/timeout60/tailfalse with empty callback. Then score0.8, objective2(true), NeedActionCompleted(0) resolves configured39. StopInternal and ResetBehavior set target=-1; shared owned helper/action stop/resume retirement unchanged. No unconditional finish. | 005C8B52..C1A;005C8C94..CE4; Hiking H10/H11; actual NeedAction call005C8C16..1A. | Pending Roll plus shared M7/M8/M15 recipients; no whole shared closure. |
| S11 Roll helper | ctor copies actualtarget/callback/triggers/maxangle/booltrue; Init count0/firsttrue. Carry=>PlaceBlockHelper; missing target=>0; actual CanRoll eligibility; preaction type4,forcefalse,angle0,useAnglefalse,offset0/0. Native preaction0 starts rolling, nonzero delegates drive; booltrue obtains actual upright angle when available. Drive failure reevaluates. StartRollingAction count>=3 marksfailure3/status0, otherwise increments BEFORE constructing composite. Recovery actual target/top query30mm, same defaultPickupparams and actual picked target; no-interaction branch only logs/retainsstatus. | 005B9938..996C;005B9A12..2E;005B9A44..BDc;005B9D98..DF2;005B9F0A..FD6;005BA060..37E. Hiking R1–R8, checked callbacks. | M12-016 already EXACT three-attempt cap; generic Roll proposal covers rest. |
| S12 drive and approach | Construct DriveToRollObject(target,false,0,false,Radians0,true), type4 IDrive with actual Roll dock false. Upright angle from obstacle/preaction face5 marker and ComputeVectorBetween(object,selectedpreaction) -> atan2f(y,x); no nearest substitute. Type4 approach retry clears helper104 before reconstruction only for checked03000013 recipient, under existing3 attempt counter. Actual IDrive uses default posefunction, required waits, weak dock/pre-dock callback. ZERO ctor angle makes no optional face children; later say/noName/max setters leave absent children absent and warn as native. Actual IDock turn/visual prefix still runs. | 0055C9DC..CAA6/0055CAF8..CB26;0055CC36..CECE;005B5DC2..DDA and005B5870..89E. Hiking drive-joins D1–D5 supersede generic optional-face wording in old R9. | Generic Roll/shared M13-014/DriveTo; imported atan2f BODY ONLY existing equivalence (libm applicability). |
| S13 actual Roll dock | RollAction type1F/method0/action5,liftanim20A. OldPose is target WRT actual MAIN.PARENT; select saves actual geometric topMarker, reject objectrelativeheight>33.85/carry. Preserve B9false observed-marker branch and NoObjectAtPose/turn prefix; shared IDock/locks/markers/C5/DA/wire/cleanup recipients unchanged. Verify actions5..7; no docking=>04000003,carry=>03000017; retained sequential MoveHead(-15deg,tolerance2deg,accel0) then VisuallyVerify(target,topMarker), update-once/again-ifRunning and deep-roll failure0300001D unchanged. |00556540..56DCE; shared005514FC/005521AC; Hiking R11–R14 with checked first-review corrections, drive-joins D3 head defaults/main-parent/topmarker distinction; caller setters in D5. | Generic Roll/shared M12 owners, all input/firmware boundaries retained. |
| S14 response/callback/API | Converter raw04000003->1->RollRetry1FA; success0->2->missing23F bypass; others(including CANCELLED)->0->Realign1F9 before original callback. Deferred response callback carries ORIGINAL rawresult; Respond cancelled preservesstatus,success=>2,else retry. Actual director robotAI+C lifetime/lookup and reentrant callback clears/order reused. Roll full completion SECOND22byte setter copies four undefined unusedIDs: preserve definedlocaltarget/count1/false, scalar/proxy/callback, suppress ONLY full-publication with visible M1-034 guard. No zero-padding policy imported from PlaceRel. |005BAC3E..54/005BA7C8..DA;005B71DA..72C8/005B7752..77E8;0056D86C..946;00556744..7EE esp005567E0..EE,0075C400..410. Hiking helper-response and reentry packets supersede generic old response summary. | Generic Roll/M1-034; existing shared ActionList/API contract only to its checked boundary. |
| S15 carry/Place | Actual PlaceBlockHelper init consumes two canonical double RNG draws for anglePI/4..PI/2 and independent sign, one f32 narrowing; TurnInPlace(angle,false) retains actual constructor defaults2deg/D8true. Callback ignores turnresult and schedules sequential Ground Add(false,true); placed callback ignoresresult=>status2; Roll successcallback setsfirsttrue/re-evaluates. No direct carry-clear. Ground actual wire/C5 release/reevaluation and typed/scalar callbacks remain shared; Ground completion getter is still UNOWNED, not M12-050. |005B8C1C..E68;005BAA82..A92; shared IHelperStop005B6768..7F7; Place vtables and helper-response PB1–PB4. | Reuse pending generic PlaceBlockHelper proposal; inherited Ground ownership/publication boundary remains explicit. |
| S16 state lights | Actual manager-owned setter old/new diff, first matching replacement, per-entry modifier, duplicate handling, retain evenfailure and ordered stop/play; actual lower Layer1(binding independent of trailinginteger0), trigger25=InteractingBehaviorLock; later post-Init switch cleanup uses retainedmanagerstate. Setter boolfalse does not disable traffic. All checked lower stop/group-selection/retention and switches reused. |005BEE38..46 ->005A4320..45F2;ctor005A0918..92A;lower006396AC..722;MS4005A2114..226E;Enum7655F0/table01032F90 cell01032FF4. Hiking state-lights-read/lower-bindings/reentry independent addenda. | Reuse pending shared state-light-manager proposal; M4-018/041/046 lower owners unchanged. |

## Exact parent caller boundary (bounded facts, not parent completion)

| Step | Known effect/input | Citation | Boundary |
|---|---|---|---|
| P1 PlayAlone | activityType BehaviorsOnly; Scoring list entry index4 (zero-based) RollBlockOnSide flat1.0, after DriveOff1000/ReactObstacle30/KnockOver20/GuardDog1, before other scored siblings. Simple shouldEnd25s/cooldown30s. StrictPriority interlude list EarnedSparks,SevereLowPlayBored,SevereLowPlayRequest,MildLowRepair,MildLowEnergy,MildLowPlay. | `activities/freeplay/playAlone.json`:4–17,53–59,160–171 (under same OBB behaviorSystem prefix). Common ActivityBehaviorsOnly ctor005A7180 -> IActivity; checked StackBlocks parent packet. | Real parent scoring/admission/competition/interlude/reaction/mood/progression producers MISSING; asset is not chosenwinner or hosttimer. |
| P2 Socialize | actual activityType Socialize; Scoring entry index3 RollBlockOnSide flat1.0; preceding1000/30/20, following PutDown0.1 and directly selected siblings0. Same interlude list. Simple canEnd5/shouldEnd300/cooldown30/startMoodMin0.5/Social graph; maxFindFaces2 and configured PouncedAndCaught/PeekABoo objectives. | `activities/freeplay/socialize.json`:1–118. | Socialize is a derived parent with actual on-run state/face/objective effects; no inheritance-by-name assumption. |
| P3 actual Socialize boundary | GetDesiredActiveBehaviorInternal FIRST calls base IActivity chooser; nonnull chosen behavior equalcurrent returnsunchanged, nonnull different logs/usechosen, then return. Only base null proceeds to own state94 switch and other IsRunnable/FindBehaviorByID paths. This establishes an actual route for the scored Roll instance without certifying parent admission. |005B0260..288;005B0294..2DA;005B02DC..2E6; othercallers005B02FC/030A/03D6. | Full Socialize state-machine/admission/siblings/outside effects remain one inherited MISSING line, not read in bulk. |
| P4 Socialize objective recipient | Handler's state2 shortcut requires incomingobjective0x0C, hence not Roll's2. Otherwise maplookup for incomingobjective, decrement/erase ifpresent, and state4 may transition5 when checked count becomeszero. Parent state/map producers are outside this bounded job: Roll objective2 is still broadcast, never assumed no-effect merely because no Rolled objective is in asset. |005B07C2..CE,005B0812..838,005B085A..AE; Rollobjective005C8C0E..12. | Named recipient fact; actual parent state/population/scheduling effects MISSING. |

There is no direct Sparked owner for this configured ID. The two referenced parents do not turn RollBlockOnSide into SparksRollBlock. RollBlockOnSideLowScore and SparksRollBlock remain separate scoreboard jobs.

## Reused checked packet index and correction precedence

All names below are under `re-analysis/research/`, date20261028, prefix `hiking-roll-cube-1-`:

- `read.md` / `read-codex-check.md`: H1–H11, R1–R15 original primary tables; this packet S1–S6 replaces only the configured identity/parent inputs.
- `drive-joins.md` / `drive-joins-codex-check.md`: D1–D5 actual drive defaults/waits/zero-angle/no-child behavior, MAIN-parent oldPose, geometric topMarker and inherited IDock prefix. These correct original generic wording.
- `helper-response-clarification.md` / `joins-codex-check.md`; `reentry-contract.md` / `reentry-contract-codex-check.md`: actual Place RNG/ignored-result Ground path, Roll response converter/director/deferred originalscalar/callback-clear and scheduler reentry.
- `state-lights-read.md` / `state-lights-read-codex-check.md`, `lower-light-bindings.md` / its independentcheck: complete manager setter/retention/order/switch cleanup, actual trigger25 and Layer1. These supersede earlier `state-lights-boundary.md` unrecovered-recipient note and original layer0 shorthand.
- `libm-applicability.md`: same imported atan2f PLT004A4510 is unshipped external body, existing approved equivalence applies ONLY to body; operands/width/order/geometry/selection/scheduling exact.
- `place-helper-proposal.md`: distinct generic helper ownership; keeps unowned Ground completion bridge separate from M12-050.
- `record-quote-correction.md`: historical encoding-only correction. This report's quotes were regenerated from CURRENT manifest, no mojibake copied.

Those files and HANDOFF-HIKING-ROLL-CUBE-1.md remain untouched. Earlier packet source/pass/freeze manifests are the audit trail; no old packet is silently rewritten or broadened.

## Ownership conclusions, finite leftovers and settlement candidates

Existing whole records newly eligible for settlement from this configured delta: **NONE**. M12-016 is already EXACT_SOURCE for THREE HELPER ATTEMPTS. M15-044 already owns the unlock table. Broader factory/base/helper/manager/action/DriveTo/IDock/geometry/needs records retain other owned paths and are not narrowed to this one configured test. M12-050 explicitly owns PlaceRel Verify/completion, not Roll or Ground. M13-014 retains its public/manual and other concrete descendants/frontiers. No current record is contradicted or downgraded by these concrete caller matches.

Reuse the SAME three pending coherent proposals from Hiking: generic Roll own path (class/cache/RollHelper/DriveToRoll/RollAction), generic PlaceBlockHelper lifecycle, shared BehaviorManager state-light setter/bindings. Add RollBlockOnSide as another actual caller/configuration to the generic Roll proposal, never a duplicate per-config record. No ID/status/inventory/approval allocated here. Ground completion getter/full-publication bridge remains UNOWNED and must gain formal ownership before a whole API-path settled claim includes it.

Inherited visible MISSING: actual PlayAlone/Socialize admission, competition/interlude/outside parent state/face/objective effects; actual locatedworld/carry/firmware/image/progression/spark/mood/needs inputs wherever unavailable; inherited planner/foreign-pose/geometry/vision/audio/scheduler/full-public report boundaries. Caller contract reuse does not close these producers. Hardware timing remains M12-041. Native CantInteract keeps defined Running; no unconditional hang was proved, no inventedtimeout/failure/M1-034hang shortcut. Existing Roll M1-034 guard suppresses only full-public payload, preserving defined fields/localcallback/scalar/proxy. Imported atan2f equivalentBODY boundary remains as approved, no new departure.

No implementation/test/commit performed. Source packet is ready for independent primary check and root release; it is not itself builder authorization, a whole behavior finished claim, hardware pass or acceptance.
