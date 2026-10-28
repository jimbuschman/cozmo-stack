# SPARKS-KNOCKOVER-1 — configured instance and actual ActivitySparked own lifecycle

2026-10-28. Answers `jobs/trial-m13/SPARKS-KNOCKOVER-1.md`; manager job started after KnockOver push74216d25. Actual model/effort telemetry unavailable. Read PROJECT_STATE first. Research only: no code/tests/git mutations/official status/inventory/approval/Claude/hardware changes. This source packet requires independent B check before builder release. Initial preflight retained in `.scratch/sparks-knockover-source/report.md`.

## Evidence and approved-contract preflight

Reuse independently checked `20261028-knockover-cubes-1-read.md` K1–K27 and its read/build checks. Reuse OV67 (`overnight/20261015-ov67-activatespark.md`) and manager-adopted independent `overnight/20261015-ov65-67-check.md`, whose complete checked own lifecycle is M15-059. M15-freeplay approved inventory adoption rows in sections Check of OV65-OV67/Batch check20261016 retain M15-059 IMPLEMENTATION_GAP and M15-055 forwarding; M8 approved inventory correction A3 records actual manager tick order and StopOnNextActionComplete callback destruction. No frozen inventory contradiction newly established here. Formal Q8/M13-014 hard-spark evidence correction and reapproval are now imported from official main a2b72726/2c319e8f; the CURRENT M13-014 quote below reflects that manifest. Historical K1–K27 correction history is retained in its original packet. No native-code change or researcher-authored inventory/approval edit accompanied this refresh.

Primary `.so`: `resources/lib/armeabi-v7a/libcozmoEngine.so`. `known.py` preflights preceded new class/constructor/config/reset/sequence/scheduling traces. Python313 explicit interpreter used because default alias denied. Primary dumps/navigation scratch: `.scratch/sparks-knockover-source/spark-functions-native.txt` (per-function native instructions; use instead of initial linear dump, which stopped at literal data), `sequence-native.txt`, `BehaviorPlayArbitraryAnim.txt`, `IActivity10OnSelected.txt`, `IActivity12OnDeselected.txt`, `ActivityFactory.txt`, and `known*.txt`. Dump is not substitute for B independently rereading `.so`.

## Current records quoted before ownership reliance

```json
{
  "id": "M7-014",
  "title": "Reaction-lock manager lifetime and concrete per-class lock tables",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::SmartDisableReactionsWithLock 0x005bce3c",
    "BehaviorManager::DisableReactionsWithLock 0x005a27e8",
    "BehaviorManager::RemoveDisableReactionsLock 0x005a3a48",
    "13 concrete 21-entry tables in M7 inventory Appendix F"
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
  "id": "M15-006",
  "title": "Freeplay activity selection and selected/deselected lifecycle",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::PickNewActivityForSpark 0x005ADC44",
    "ActivityFreeplay::GetDesiredActiveBehaviorInternal 0x005AE29C",
    "IActivity::OnSelected 0x005B312C",
    "IActivity::OnDeselected 0x005B33B8",
    "IActivity::GetDesiredActiveBehavior 0x005B387C"
  ]
}
```

```json
{
  "id": "M15-013",
  "title": "Activity tree construction, desired names and discarded activityPriority",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::CreateFromConfig 0x005AD478",
    "ActivityStrictPriority constructor 0x005B23DC",
    "activities_config.json"
  ]
}
```

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
  "id": "M4-017",
  "title": "BodyLightComponent and SetHeadlight: priority, Off resent every tick while no source, charging state machine, shared locator, wire conversion, the headlight's EnableMode(14) call and send",
  "status": "EXACT_SOURCE",
  "evidence": [
    "LB1..LB6, LB4a..LB4i as before",
    "E2 0x0063234A..0x00632364 SetHeadlight calls VisionComponent::EnableMode(14, enable) on robot+0x258",
    "E3 VisionMode 14 = LimitedExposure (name table 0x01034230, entry 14 -> 0x00C1DC64)",
    "Handoff, owned by M11-061: E4 0x006527AC..0x006527B6 EnableMode tail-calls VisionSystem::SetNextMode (PLT 0x4BA35C)",
    "Handoff, owned by M11-061: E5 0x006B2282..0x006B2298 SetNextMode queues (mode,bool) on the deque at VisionSystem+0xB0",
    "Handoff, owned by M11-061: E6 0x006B4FB6..0x006B4FE4 VisionSystem::Update drains the deque",
    "Handoff, owned by M11-061: E7 0x006B1A76..0x006B1CC6 EnableMode(14) sets/clears mask bit 14 at VisionSystem+0xAC",
    "E8 0x00632368..0x00632380 EngineToRobot(SetHeadlight) then SendMessage reliable, not hot",
    "Re-read handoff: ldr.w r0,[r0,#0x258]; movs r1,#0xE; blx 0x4A91CC VisionComponent::EnableMode(14, on) at 0x00632350..0x00632364 (M11-061); then SetHeadlight blx 0x4B8E38 0x00632374 and Robot::SendMessage blx 0x4A5368 0x00632380; the LimitedExposure reader (E9): M11-057",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 4 (Opus extraction, manager-adopted 2026-10-11): the full log rows (level, channel, key, format, arguments, gates, errG/break and order) and the differences from the C#.",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 13.4 (follow-up Opus extraction, manager-adopted 2026-10-11)."
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
  "id": "M8-005",
  "title": "PlayAnim plays every trigger it lists, in order, num_loops times",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "StartPlayingAnimations 0x005C0158 measures the trigger vector at +0x11C: a byte length of exactly 4 - one trigger - is special-cased and played as a single TriggerLiftSafeAnimationAction with the loop count from +0x128 (0x005C0190). Anything else falls through to StartSequenceLoop.",
    "StartSequenceLoop 0x005C0294 runs while the counter at +0x12C is below the loop count at +0x128: it makes a CompoundActionSequential and walks the whole trigger vector, adding one TriggerLiftSafeAnimationAction per trigger (0x005C02CA..0x005C030E), then runs the compound and increments the counter. So every trigger plays, in the config's order, and the list repeats.",
    "The loop count is the config's num_loops, read with a default of 1: Json::Value::Value(1) then Json::Value::get(\"num_loops\", default) at 0x005C001C..0x005C0036.",
    "Of the fifteen shipped PlayAnim configs, one lists more than one trigger - NothingToDo_BoredAnim - and none sets num_loops. This stack played the first trigger that resolved and stopped; it now plays the list."
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
  "id": "M4-045",
  "title": "PlayLightAnim: the blended / non-blended branch after BlendAnimWithCurLights",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m4-cube-lights-check.md: branch condition (modifierArg | BlendReturn) & !backBlended (0x00638590..0x0063859A; backBlended from the stack back +0x40, 0x0063830C..0x00638326); blended: per-layer slot node+0x44+12*layer := blended list (0x006385B6), name back.name + \"+\" (0x6389AC) + animName or \"\" + animName on an empty stack (0x006385C0..0x0063860C), info +0x10/+0x14 -> slot, +0x38 blended = 1 (0x00638698..0x006386AE), no zero-duration clear; non-blended: name animName, container list, stack cleared if the first pattern duration_ms == 0 (0x006385EA..0x006385F4). Shipped DanceFadeOff, Flash and SpeedTapWin hold [0,0,0,0] colours, so they take the blended branch with a connected object."
  ]
}
```

```json
{
  "id": "M5-063",
  "title": "DrivingAnimationHandler bodies: HandleActionCompleted (E2G 0x5A), PushDrivingAnimations (G2E 0xB5), RemoveDrivingAnimations (G2E 0xB6)",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m5-teardown-members.md: callbacks 0x004F110E -> HandleActionCompleted 0x004F0D78 (engine sender: ActionQueue 0x0053FAAA); 0x004F1186 -> PushDrivingAnimations 0x004F0944; 0x004F120E -> RemoveDrivingAnimations 0x004F0A94; registered only if HasExternalInterface() == 1 (0x004F072C..0x004F0732).",
    "re-analysis/research/20261012-m5-driving-handler-read.md: handler state ctor 0x004F06B0..0x004F0878 / Init 0x004F0FC4..0x004F0FF4; Push gate warning 0x004F094C..0x004F0964 and push_back 0x004F0988..0x004F09FA; Remove gate 0x004F0A9E..0x004F0AB4, empty-stack warning 0x004F0ADA..0x004F0C22 (0xBE4672), first-match erase 0x004F0AE4..0x004F0C6C, LoopBoundOverflow drivingAnimationHandler.cpp:108 0x004F0B5A..0x004F0BB6; UpdateCurrDrivingAnimations only from Init (0x004F0D2C..0x004F0D74: back(), else Sad 0x2C, else needs-met 0x38, else default 0x20); HandleActionCompleted 0x004F0D78..0x004F0E2A with PlayEndAnim 0x004F0EFA..0x004F0F4A, PlayDrivingLoopAnim 0x004F0E58..0x004F0EA0, PlayDrivingEndAnim 0x004F106C..0x004F10C0, lock/unlock helpers 0x004F0EB2..0x004F0EE2 / 0x004F0F4C; ActionIsBeingDestroyed 0x004F0F94..0x004F0FC0."
  ]
}
```

```json
{
  "id": "M8-019",
  "title": "BehaviorManager::RequestCurrentBehaviorEndImmediately, as HandleRobotStopped calls it",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Called at 0x00535454 (blx 0x4AA1C8) from Robot::HandleRobotStopped when the cliff sensor is enabled (gate 0x00535436..0x00535438; M4-019 H4 0x00535432..0x00535480)",
    "re-analysis/research/overnight/20261014-ov20-21-check.md: R2/R3 0x005A3930..0x005A39A6; caller R1 0x00535432..0x00535486 (interface to M4-019 H4)."
  ]
}
```

```json
{
  "id": "M15-031",
  "title": "SDK high-level activity activation recipient",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivateHighLevelActivity(1) at 0x0065DDBA; receiver body UNKNOWN (triage E2).",
    "re-analysis/research/overnight/20261016-ov105-107-108-answer.md: wire G2E 0x92 Unpack 0x00738A94, Get 0x0074D90A; subscription 0x005A147E..0x005A14C8 (only with external interface 0x005A0F5A..0x005A0F64); adapter 0x005A4FA2..0x005A4FE0; SetCurrentActivity 0x005A106C..0x005A1230; init call 0x005A0F44..0x005A0F4C; EnumToString 0x007679D4..0x007679E6."
  ]
}
```

```json
{
  "id": "M15-055",
  "title": "Activity Update forwarding: ActivityFreeplay::Update to the current sub-activity, and ActivitySparked::Update spark-state logic (CheckIfSparkShouldEnd, CompleteSparkLogic, reset) before forwarding to its delegate",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov59-61-check.md: ActivityFreeplay::Update 0x005AD9D0..0x005AD9DE (+0x80 vt+0x20, null returns 0); ActivitySparked::Update 0x005B186C..0x005B18D0 (spark state, then +0xC4 vt+0x20); CheckIfSparkShouldEnd 0x005B18D4..; two ActivityBuildPyramid instances (activities/freeplay/buildPyramid.json, activities/sparks/sparksBuildPyramid.json).",
    "re-analysis/research/overnight/20261016-batch-check.md: F1 0x005AD9D0..0x005AD9DE (slot +0x20 in vtables 0x010255E0, 0x01025738)."
  ]
}
```

```json
{
  "id": "M15-058",
  "title": "BehaviorManager spark fields: init, SetRequestedSpark, SwitchToRequestedSpark, high-level-activity adapter clear",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: field init 0x005A08DA..0x005A0900; setter 0x005A3EAC..0x005A3F0E (\"BehaviorManager.SetRequestedSpark\" \"requested %s spark is '%s'\"); switch 0x005A4198..0x005A422C (Info first; +0x60 not cleared); adapter 0x005A4FA2..0x005A4FE0 (SetCurrentActivity body 0x005A106C unread, overlaps M15-031); writers ctor, 0x005A3C9A, 0x005A3EB2/0x005A3EB6, 0x005A4220/0x005A4224/0x005A4226, 0x005A4FDC/0x005A4FDE."
  ]
}
```

```json
{
  "id": "M15-059",
  "title": "ActivitySparked lifecycle: OnSelected (HardSparkStartedByEngine tag 131), OnDeselected (StopAllAnims), objective/observed, Update/checks/end/interrupt, get-in/out, Complete (meta.upgrade_replay_* events, HardSparkEndedByEngine tag 132)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: OnSelected 0x005B1518..0x005B16DC, 0x005B171C; OnDeselected 0x005B1780..0x005B179E; objective/observed 0x005B181E..0x005B1866; Update/check/end/interrupt 0x005B186C..0x005B1A16; get-in/out 0x005B1D18..0x005B1F2A, hard max 0x005B1F94..0x005B2066; Complete 0x005B1A90..0x005B1C2E, 0x005B2094."
  ]
}
```

## Finite own-path inventory

`READ` means recovered source for this row, not an official fidelity upgrade or proof of existing production implementation.

| Row | Source-established behavior and order | Native/file citation | Current ownership / classification |
|---|---|---|---|
| S1 configured leaf | Actual behaviorID SparksKnockOverCubes/classKnockOverCubes, minimumStackHeight2, needsActionID KnockDownCubes_Sparked(28), executableBehaviorTypeKnockOverCubes, requiredUnlockIdKnockOverThreeCubeStack. Five trigger values identical to regular class instance. No alwaysStreamline override. | `obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/sparkable/sparksKnockOverCubes.json` lines2–13; K1–K27 | M13-014/M8-001 READ. Generic native class already reads own min/needs. No separate state-machine build presumed. |
| S2 two-cube class | Reuse K1–K27 exactly with two-member cache: bottom/top valid, middle invalid. Admission tallest cached height>=configured2; normal regular3 rejects same cache. Only bottom/top member axis inputs count when middle invalid; no physical-success inference. Own result dirty→objective13→needs28 before hard reaction skip. | K1–K4,K12–K14,005C314C/31A2,005C3A98..3AD4,005C3946..3978 | M13-014 READ. Tests must select actual Sparks instance, not rename ordinary/manual wrapper. |
| S3 common spark inputs | Native Init reads manager active+58; invalid55=>D8zero else D8=soft+5C XOR1. Hard0 streamlines; soft1 retains reach/reaction. RequiredUnlock5 admission from actual progression and matching active5 adds common SparkBehaviorDisables lock. | Primary005BCCAC..005BCCC2,005BCD16..005BCD44; K shared checks | M8-001/M7-014/M15-058 READ; active inputs not inferred from requested. |
| S4 executable metadata | Read common executableBehaviorType string, convert, store byte+6C. Separate executable selection path is not the Spark activity's StrictPriority ID lookup. | Primary005BC1B2..005BC1EE (literal C1B6;isStringC1BE/C2;FromStringC1EA;strbC1EE) | M8-001/M8-013 READ parser; separate finder inherited MISSING. |
| S5 actual caller config | Actual activityID SparksKnockOverCubes/typeSparked/requireSparkKnockOverThreeCubeStack, Spark strategy, StrictPriority [DriveOffCharger,SparksKnockOverCubes,SparksCheckForStackAtInterval,SparksPutDownBlock,SparksLookInPlace]. min0/max60/reps1, objectiveKnockedOverBlocks, softSparkTriggerSoftSparkUpgradeTracks, no subActivityDelegate. | `obb/assets/cozmo_resources/config/engine/behaviorSystem/activities/sparks/sparksKnockOverCubes.json` lines2–27 | M15-059/M8-013 READ. Sibling bodies remain inherited; asset membership alone not admission. |
| S6 actual constructed owner | ActivityFactory Sparked case allocatesE8 and calls ctor. Ctor calls IActivity base; zeroes+68..8F; min/max=-1,wait98true,reps9C=-1,flagsB4/B5zero, empty observed setB8. Reload before resolving additional common behavior refs. | Primary005AA4C2..4CE;005B0D14..005B0D72 | M15-059 own constructor READ; tree assembly M15-013/M15-031 interface. No global/fake watcher. |
| S7 retained children | Reload obtains configured BehaviorID3D PlayArbitraryAnim into+78/7C. Constructor additionally retainsID1 AcknowledgeObject+80/84,ID94 SparksPeekABoo+D8/DC,IDB2 Wait+E0/E4. Activity body examined never reads+80; Peek hook only requested25; Wait only waitfalse hardmax. Do not replace missing configured PlayArbitraryAnim with generic PlayAnim. | Primary005B1000..1028;005B0D76..0DEC;005B16C6..16D6;005B2046 | M15-059/M7-018/M8-005 READ; instance child ownership is necessary. No blanket no-effect claim beyond examined method reads. |
| S8 reload config | Soft triggerA4 resets23F then optional key; default success216/failure210 through EnumToString/default JSON then FromString; empty string=>23F. ParseFloat min→90,max→94, ParseUint8 reps→9C; optional maxTimeoutForActionComplete→98 preserves ctortrue. behaviorObjective default enum0 name, convert→A0. Absent delegate stays null; present delegate factory replaces old. | Primary005B0EAC..005B11A6; stores0F90/0FE4/104C/107E/10B0/10DE/1132; delegate1150..1184 | M15-059 READ normal shipped path. Malformed config exception cleanup11AA..1255 not claimed ordinary behavior. |
| S9 subscriptions | If actual Robot.HasExternalInterface, ctor subscribes8B then44 through externalinterface vt28. Captures this; handles retained in own vector6C. No selected/running guard in handlers. Selection resets count/observed set; preselection/deselected matching messages still reach owner. | Primary005B0DF0..0E0C;005B1304..135A(tag8B);005B13C4..141A(tag44);005B181E/1834 | M15-059 READ. Existing NativeActivityObjectiveOwner already has real owner/subscription but currently only objective4 miss logic; cannot add selected-only count guard. |
| S10 common selection | IActivity.OnSelected stamps54, chooser selected hook, public UpdateActivity, configured base driving/idle/analyzer hooks, entry event, then vtable28 specialized OnSelectedInternal. This activity has no base drive/idle/process config; specialized hooks below add its own. | Primary005B3136..3154,3158..31C0,31C0..3208,3246..324E | M15-006 base lifecycle READ; own class M15-059. No generic host OnSelected substitution without actual specialized hook. |
| S11 specialized selection/reset | Read clock once; stamp88 and zero8C, state68zero,B0zero,B4/B5zero; clear observed tree. Hard iff requestedID55 OR requestedSoft64zero (requested fields, not active, because active switch can follow OnSelected). Hard branch named driving push→SmartPushIdle213→start backpack source0 owned locatorC8→B5=1→Started(requestedID). Then activity-name reaction lockC624BC; optionaldelegate.OnSelected; requested25 Peek hook. | Primary005B1522..1550;005B15D0..1674;005B1678..16D6; checked OV67 selected-before-active sequence | M15-059/M15-058 READ. Requested/active distinction not optional. |
| S12 hard outputs literals | Driving descriptorC624B0 words{20E,20D,20F}; selection reaction tableC624BC 21 pairs true only1,2,3,4,5,10. Backpack static initializer uses NamedColors.WHITE revFFFFFFFF and BLACK rev000000FF; full raw state appendix below. Source0 and independent activity locatorC8 passed to BodyLight start; do not borrow charging/shared locator. | Primary005B1574..15C8,005B1610/1616,005B1634..165C,005B16A0/A8; bytesC624B0/C624BC;WHITE C9744B,BLACKC9744F | M15-059 callsite; existing M4-017/M5-063/M7-014 helpers. Current shared BodyLight locator API cannot prove per-owner capability. |
| S13 started delivery | Started wrapper copies unlock32, builds E2G union, broadcasts through actual external vt1C, clears temporary. Native outer tag83 payload unlock little-endian4. Happens after hard light start and B5 store, before activity reaction lock. | Primary005B1660..1674/171C..175C; existing checked OV67 reply schema | M15-059 READ. Use actual report delivery/retained local/API topology, not log-only success. |
| S14 objective recipient | Incoming objective word==A0 increments8C unsigned wrap. No success byte, selected flag or activity-running check. This config matches13, unlike prior driveoff4 miss. | Primary005B181E ldr+A0;1822 ldrpayload;1824cmp;1828ldreq+8C;182Cadd1;182Estr;1832return; config005B112E/1132 | M15-059 READ. Current NativeActivityObjectiveOwner logs MISSING for13 and has no count; real production discrepancy. |
| S15 observed recipient | Incoming ObjectFamily atpayload+4 minus1 <=1 unsigned accepts exactly1/2; insert payload ObjectID+C uniquely into observed treeB8. No selected gate. This set feeds completion failure logging. | Primary005B1834..005B1866 | M15-059 READ. Existing immutable NativeObservedObject LOCAL carrier has Family at+4,Type+8,ObjectId+C; see helper appendix. Complete upstream host44 serializer remains inherited M11-038; no facade reconstruction. |
| S16 real tick cadence | Manager.Update ticks actual current high-level activity vt20 BEFORE reaction check/chooser/behavior.Update. Freeplay.Update forwards current subactivity+80 vt20. Sparked.Update gets CurrentBehaviorTriggeredAsReaction; manager returns stacktop trigger!=16. State0/1 +reaction sets2, then check; state2/3 checks; resulting state3 +reaction calls Complete→Reset→state5. Always forwards optional delegateUpdate, else return0. | Existing checked M8-012/M15-055; primary005A19DE..19EA;005B186C..18CE | M8-012/M15-055 interfaces; M15-059 READ. This fast complete branch is REACTION, not a soft-spark gate. |
| S17 end predicates | Check current behavior and read clock once. repetitionDone iff ordered f32(f32(start+min)+NegEps)<=now, target!=0, signed count>=target. Timeout only wait98true and ordered f32(f32(start+max)+NegEps)<=now AND currentnonnull AND current.requiredUnlock!=active. State2 and(reps ORtimeout ORcancel65)→Reset first→request current end next action→state3→finalAnimLockReactions C624E6(true1,2,4,8). | Primary005B18D4..19AE;NegEps bitsB727C5AC atliteral005B1A44 loaded18FE; exact ops18EE..1924;timeout1926..1954;enterend1972/1978/197E/199C | M15-059 READ. This shipped min0/reps1 makes count13 affect real end/API. No host-clock/deadline approximation. |
| S18 reset helper | IfB5zero return. Else obtain actual activity name(vt24), RemoveDrivingAnimations(name), StopLoopingBackpackLights(own locatorC8), thenB5zero. It does NOT remove idle or cube lights. | Newly primary005B17AC..005B1804:17DE remove;17F8 stop;17FEclear | M15-059 READ. Base deselection and specialized OnDeselected handle remaining units. |
| S19 next-action stop | Manager obtains current actual behavior via current stack sharedref; if nonnull calls StopOnNextActionComplete. That sets A0 and destroys88/98 completion callback, current action remains live. On completion84 clears; IBehavior.Update seesA0&&84null→2; normal manager Finish/Stop owns teardown. Not immediate Stop/cancel. | Primary005A42C4..4304,42FEcall;existing native/approved M8-001005BD624..6B4 and005BD074..88;M8-012005A30BC..3128 | M8-001/M8-012; exact manager forwarding address absent current evidence list, inventory owns lifecycle/caller boundary; don't invent new record from search absence. READ call. |
| S20 interruption | Otherwise requested!=55 AND(active!=requested OR active!=55&&activeSoft!=0&&requestedSoft==0) requests immediate current end with literal reason `Sparks transition soft spark to soft spark`; state4,B4true. This bypasses normal get-out setup on next desired path, as source. | Primary005B19B0..1A08;checked M8-019 RequestCurrentBehaviorEndImmediately | M15-059 call/M8-019 callee. Current immediate-switch recipient visibly MISSING; no generic Stop substitute. |
| S21 get-in/internal desired | State0 chooses triggerA4 for activesoft, else211; SetAnimationTrigger(trigger,1) on actual+78 child→state1. State1 returns same animation while caller-current running; after finish state2 then SelectNext. State2 SelectNext. No own state1 trigger reinstallation each tick. | Primary005B1D3C..1D7A,005B1E0A..1E46; jump table at1D34 | M15-059/M8-005 READ. Actual C# factory currently does not create required native PlayArbitrary child. |
| S22 internal chooser/hardmax | Ifdelegate exists call its IActivity.GetDesired; else call qualified base IActivity.GetDesiredActiveBehaviorInternal (actual chooser). Then read clock. Ifwaitfalse AND ordered f32(f32(start+max)+NegEps)<=now AND caller-current pointer!=chosen pointer, replace chosen with retainedWait+E0,state3. Ifwaittrue do no override. | Primary005B1F94..2066,1FC6/1FF4,2016/201A,203C/2044,2046/2062 | M15-059/M8-013 READ. Shipped waittrue bypasses hardmax branch, not feature exclusion. Sibling chooser choices remain actual interfaces. |
| S23 get-out | State3: caller-current nonnull AND class!=39 AND running→continue SelectNext. Else activesoft ORcancel chooses singleCount23F; hardnormal builds vector: signedcount<target adds failureAC onlyif!=23F&&active!=41; else adds successA8 if!=23F then SuccessfulSpark mood event regardless success-trigger presence. Append212; SetAnimationTriggers(vector,1); return actual+78,state4. | Primary005B1DBA..1DFC,1E68..1F02;Moodclock1EC0/call1ECA | M15-059 READ; class0x39 is native Wait, not ordinaryPlayAnim0x26 or arbitrary0x29. Do not replace “class!=39” with pointer==specific animation or blanket await. |
| S24 final completion cadence | State4: caller-current running returns animation+78; otherwise calls Complete then returnsnull. It does not storestate5. Reaction Update fast path explicitly stores5. No invented completed-once latch justified by this class. Actual owner deselection and next pick prevent repeated normal calls. State5/default null. | Primary005B1E00..1E24;005B18AC..18BA | M15-059 READ. Source lifecycle ownership must preserve exactly when Complete is callable. |
| S25 completion success/log | success iff target0 OR signedcount>=target. cancel65 orB4 choosescancel event; else success chooses_success; failure without observed uses_timeout, failure with observed uses_fail and hard/55 additionally_fail_cubes_observed count event. Always result event(text active UnlockId name,$data soft iff active!=55&&soft!=0 elsehard). | Primary005B1A9A..1BCA;eventstrings in literal appendix;checked OV67 | M15-059 READ. Observed set affects diagnostic choice, count affects success/report. |
| S26 completion clear/reply | After result event: ifcancel65 ORB4 return. Ifactive==requested, SetRequestedSpark55,false; reloadactive. Only active55 orhard emitsEnded(successbyte) through external interface. Soft skips. No active field clear here, no pending unrelatedrequest clear. | Primary005B1BEE..1C2E;wrapper005B2094..20D6 copiesbyte/buildsunion/broadcast/clear | M15-059/M15-058 READ; E2G84 payload1byte. |
| S27 deselection | Common OnDeselected stamps58, chooser hook, configured base idle/driving/analyzer cleanup, activity reaction-lock loop, smart-owned idle cleanup if flag50, clears interlude2C, pending SparksRewardCommunicatedToUser if needs+3D8, exit diagnostic; then virtual own OnDeselectedInternal. Own: Reset, optionaldelegate.OnDeselected, CubeLightComponent.StopAllAnims. | Primary005B33B8..005B3660;locks34B4..34C6,idle34EA,interlude34EE,needs3548;own005B1786/1792/179E→8CBE3C | M15-006/M15-059/M7-014/M4-045 READ call topology. StopAllAnims is actual component helper, not facade clear. |
| S28 owner destruction | Own destructor clears/releases animation78 thenWaitE0,PeekD8,weaklocatorD4,delegateC4,observedtreeB8,Acknowledge80; subscription vector6C is destroyed before IActivity base destructor. No automatic Complete or deselection appears in own destructor. | Primary005B1484..005B1500 | M15-059 READ. C# runtime must retire actual constructed subscriptions with owner; no global observer survives. |

## Required PlayArbitraryAnim child (finite recovered path)

This is the actual configured ID3D, not the legacy manual PlayArbitraryAnimBehavior that plays by clip name. Parent factory case41 at0059CE86..CEA6 constructs native BehaviorPlayArbitraryAnim, with its own source base common config. Current factory lacks it. Existing whole M8-005 owns shared sequence, M7-018 generic configured factory; no new record is proposed just to split this caller.

| Row | Exact child behavior | Primary citation / limitation |
|---|---|---|
| P1 ctor/common | BehaviorPlayAnimSequence ctor/IBehavior; empty vector11C,loop128,index12C,emptylistener130; arbitrary ctor sets13Cfalse,128=-1. VtableUpdateInternal is IBehavior base; slots20/24=0,28=1; ownIsRunnable requires128>=0 then virtual90 returns1 (5C03B4). |005BFF24..005C004A,005C0784..07A4,005C07BE..07CC;vtable01026B48 relocations; inherited source admission retained. |
| P2 setters | Single clears11C vector then pushes supplied trigger evenCount; list clears then copies every supplied trigger in order; both13C=true,128=loops. They do not start/stop a current action. |005C07CE..0820,005C0822..0892. No Count filtering from config parser borrowed into setter. |
| P3 init/resume/stop | Init clears13C then StartPlayingAnimations andreturns0. Resume returns0 (different from ordinary sequenceResume1). Stop clears13C andvector, then common Stop owns action cancellation. |005C0896..08CC. |
| P4 one trigger | Exactlyonevectorword: create TriggerLiftSafe(trigger,numLoops,true,tracks0,timeout60f,strictfalse), StartActing with CallToListeners completion. This includes Count23F if supplied. |005C0160..01A6; callback005C0254..0292. Existing M8 action semantics own unresolved trigger results, no local success substitute. |
| P5 multiple triggers | Else resetindex12C0, StartSequenceLoop: signed index<loops builds ownedCompoundSequential, each trigger has one TriggerLiftSafe(loop1,true,0,60,false), AddAction(false,false), index increments before StartActing; completion calls StartSequenceLoop again. Exhausted returns no action. |005C01AA..01BA;005C0294..035A. Checked existing M8-005 evidence. |
| P6 listeners/update | Single CallToListeners walks ordered storedpointerset, each listener vt0(robot); multi-loop callback only starts next loop. Actual Sparked does not install a listener in examined own methods. BaseUpdateInternal returns1 while84nonnull else2; no extra timer/poll/13C update path. |005C0254..0292,005BDA56..62;sourcecalls all current classmethods read. Whole M8-005 listener functionality remains an obligation if claiming record settled; no broad absence claim from no local caller. |

## Literal appendix and exact helper boundaries

- Driving descriptorC624B0 = {0x20E,0x20D,0x20F}. Reaction tables are 21 enum,bool pairs: C624BC true{1,2,3,4,5,10}, C624E6 true{1,2,4,8}; all othersfalse. Same activity-name lock for selection, literal `finalAnimLockReactions` for end.
- Backpack raw state is 0x8Cbytes at runtime105ADD0, initialized005B156A..15C8. Words[0..34]: {000000FF,FFFFFFFF,FFFFFFFF,FFFFFFFF,000000FF,000000FF,000000FF,000000FF,000000FF,000000FF,0,360,360,360,0,0,1110,1110,1110,0,0,0,0,0,0,0,0,0,0,0,0,0,120,240,0}. `1110=0x456`, `360=0x168`. Source uses namedcolor endianness rev. Start passes first3 words in r1-r3, next0x80 bytes onstack, source0 atstack80, locatoraddress+C8 atstack84 (005B1648..165C). Existing M4-017 controls wire conversion/arbitration; do not reinterpret raw words without its native struct evidence.
- Reset only operates B5-owned driving/backpack. Specialized deselection also invokes StopAllAnims, whose exact wrapper marks layer1 on all objects (H12); baseSmartRemoveIdle removes hard idle213 using source-owned flag50. No extra idle remove inside Reset or early cube stop.
- Native wrappers Started83/Ended84 broadcast through external interface; queue/local/host delivery must use the already checked external report channel. No new public ad-hoc callback replaces it.

## Actual current production frontiers and release conclusion

1. Actual NativeBehaviorRuntime constructs distinct configured KnockOver instances and ActivityTreeLoader constructs activity nodes/owner subscriptions before selection. Matching13 currently enters NativeActivityObjectiveOwner and logs MISSING; it never increments, never schedules end. Activities.cs1069 explicitly reports specialized Sparked lifecycle absent. These are concrete implementation gaps under M15-059, not new behavior assumptions.
2. NativeBehaviorRuntime currently admits only native PutDownDispatch and rejects specialized Sparked admission; raw request/picker remains M15-006/058 boundary. Building this own owner must use real constructed activity/config and native scheduling interfaces; tests may label supplied admission/canonical active/requested fields, but cannot call a disconnected counter callback and call the behavior finished. Raw ActivateSpark/picker and sibling bodies stay inherited frontiers, one line each.
3. Required native PlayArbitraryAnim is absent from configured factory. Its finite source rowsP1–P6 must be included and independently checked; inherited action/animation streaming stays existing owned path. No guessed PlayAnim alias, no direct async clip wrapper, no one-shot completion latch.
4. Current BodyLightComponent shared locator for charging/host does not establish activity-ownedC8 Start/Stop. Need the source-equivalent per-locator operation using existingM4-017 arbitration, rather than direct light packets or overwriting charginglocator. CubeStopAll, SmartIdle and reactionlock helpers must preserve their real owner/lifetime and accepted source semantics. Their callsites are recovered above; missing callable capability must remain visible until built.
5. M15-059 is a whole-record candidate only if entire claimed lifecycle is built/checked (including existing class parameter branches/retirement), not solely this matchingcounter slice. M15-058/006/031 and M8-001/005/012/013/019 remain their whole existing claims with exact remaining obligations; no blanket settlement. Current approved evidence has normalLifecycle semantics; source report makes no official amendment or featureexclusion.

Independent B scheduler/owner check is pending. No implementation release from this report yet. No hardware acceptance or whole finished-Spark claim. Further narrowly required native joins must be appended and checked before builder work; no source gaps filled by plausible bridges.


### Prefreeze interpretation correction (A/B, 2026-10-28)

Independent B caught A's initial S23 semantic label: native `cmp #0x39` means **BehaviorClass.Wait**, not PlayAnim. S23 now preserves the correct Wait exception: running current class!=Wait continues internal choice. PlayAnim=0x26 and PlayArbitraryAnim=0x29; actual factory enum/table is the evidence, decimal/hex must never be conflated. This corrects this new research report; prior checked OV67 already preserved the Wait predicate, so no frozen approved-contract change is requested. Common OnDeselected final own dispatch range corrected to005B3654..365C,return3660 (initial table endpoint3646 omitted the final virtual dispatch from its range).


Prefreeze B corrections also resolved two semantic labels in this report: S15 payload+4 is **ObjectFamily** (Block1/LightCube2), not ObjectType. Native `ldr` at the NamedColors.BLACK addressC9744F sees LEwordFF000000 (bytes000000FF); `REV`005B1586 makes stored structword**000000FF** (RRGGBBAA), notFF000000. S12 and the raw35word appendix are corrected. WHITE remainsFFFFFFFF. These were A's interpretation errors; native instructions/prior approved source are unchanged. Do not encode black with inverted alpha/red/blue placement.


## Final finite helper/scheduler resolutions (A, pending independent B)

Current additional record quotes before reliance:

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
  "id": "M11-038",
  "title": "The BlockWorld game-broadcast entry points",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BroadcastObjectObservation 0x0061FED8; BroadcastLocatedObjectStates 0x0061E6C0; BroadcastConnectedObjects 0x0061E91C; VisionSystem::CheckMailbox 0x006B2AD4",
    "re-analysis/research/20260929-R-VIS-verify-pre-items4-5.md (items 5a-5c PASS)",
    "re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q6; re-analysis/research/20260929-R-VIS-verify-M11-gap1.md (Q6 PASS)"
  ]
}
```

| Row | Recovered finite bridge | Primary evidence / boundary |
|---|---|---|
| H1 per-locator backpack | PublicStart63245C forwards full8Cbyte value/source/locator unchanged to Internal63228C. Internal calls Stop(providedlocator) first22A4, finds/creates suppliedsourcebucket22BE, copies8Cbyte pattern22D0, insertsfront22DC, writes sourcebucket/listnode/weakhandle to providedlocator at2320/2324/232C (oldweakreleased2332). It never implicitly uses chargingcomponent+24. | `.scratch/sparks-knockover-source/backpack-native.txt`; approvedM4LB4i already callsStop(locator) thenemplace_front. New Spark suppliedC8 is independent from charging/game sharedslot. No re-extraction of arbitration/wire needed. |
| H2 locator stop/coexistence | Stop6324A0: weakcontrol+Cnonzero AND control+4+1!=0 means live; sourcebucket+1Cnonempty→erase suppliednode+4 from source list24BC, eraseemptybucket24CA, return1. Invalid/stale logs InvalidLocator24E4 and returns0. A liveweak with sourcealreadyempty returns0 through632510→63250A. Stop takes constlocator and does not zero it: node lifetime makes staleweak expire. Existing other source/list entries remain. | Primary6324A8..632510. Destructor releases locatorweakD4 only; explicit Reset owns stop. C# lifetime mechanism is free but observations/coexistence/stale-result must agree. |
| H3 native backpack fields | Native BackPackLights is seven5word arrays: on0,off14,onPeriod28,offPeriod3C,transitionOn50,transitionOff64,offset78. Thus hardSpark pattern is on{black,white,white,white,black},offallblack,onMs{0,360,360,360,0},offMs{0,1110,1110,1110,0},transitionsall0,offset{0,0,120,240,0}. BLACK rawword000000FF and WHITEFFFFFFFF (RRGGBBAA), with opaque alpha255. | ApprovedM4LB4d; directly rechecked parser00586D2E..6E78/.scratch/backpack-parser-native.txt: respectivearraydestinations6D42,6D78,6DAC,6DDE,6E0E,6E46,6E78; initializer005B1584/86 REV. Existing LedPattern fields map exactly by correspondingindex. Wire conversion/arbitration remain accepted M4-017. |
| H4 activity locks | SmartDisable passes manager lockname=callername+`_activityLock`, table unchanged, force1; only AFTERmanagercall inserts ORIGINAL callername uniquely into activityset5C. Repeatedhelper calls still call manager; uniquename set is cleanup tracking, not a caller-side no-op guard. SmartRemove passes same suffixedname to manager first then erases original from ownset. BaseOnDeselected loops sourceorderedset until empty. | Primary005B3B34..3B7A,3B42suffix,3B56force1,3B58call,3B74insert;005B3768..37A2;005B34B4..34C6. Existing managerM7-014 force/reactioncount semantics reused, not rebuilt from guessed peractivitycounts. |
| H5 activity idle | SmartPush tests+50 first: alreadyset→VERIFYandreturn; otherwise prefix`Activity_`+actualactivityname, streamer.PushIdle(suppliedtrigger,lockname), then+50=1 regardless recipient result. SmartRemove tests+50; when set removes exactsameprefixname then+50=0; when clear VERIFYandreturn. Both are void native helpers; no fabricated bool/state-driven fallback. | Primary005B32BC..331E;005B37C0..3822; staticprefix004D7590..75C2 copiesliteralC49CD0 `Activity_` into105AE60; sourcepreflightchecked first. Streamer accepted Push/Remove semantics reused. Prefix differs from behavior idle/locks and drivingname. |
| H6 observed realcarrier | Current `Vision/NativeObservedObject.cs` immutableLOCAL prefix is `(Timestamp,Family,Type,ObjectId)`. `BlockWorld.cs1818` emits from actual located object; `VisionSystem.cs` retainedWorld.NativeObjectObserved callback delivers0x44locally only when actualexternalinterface exists and visibly reports fullhostreportMISSING. Sparked must consume Family1/2 and ObjectId, not Type, not public facade ObjectObserved. Native adapter5B21B2 loads capturethis/event+10→GetRobotObservedObject→ownhandler; native objectiveadapter5B2132 does direct equivalentA0/count comparison. | Sourceprefix0061FF5E..FFAC: object+1C timestamp,object+48family,+4Ctype packed atsp10/14/18,objectIDsp1C via virtualid; broadcast0061FFDE; nativegetter728608/724984 from symbol; ownadapter005B2132..2152/21B2..21C6 and1025758/1025798 closuretables. Completehost44 serializer remains M11-038 boundary, not a new truncated wire. |
| H7 actual scheduler | Manager.Update005A2F74/78 gets actualcurrenthighlevelactivity→vt20at2F84; Freeplay.Update005AD9D0 loads actualselectedsubactivity+80→vt20tail9DA (nullreturns0). ManagerChoose005A2A20 obtainsactualactivity/currentbehavior→IActivity.GetDesired at2A4A; Freeplay desired005AE656..672 passes selected+80,currentbehavior,Robot to samewrapper. IActivity wrapper005B387C calls vt30specialized005B1D18 unless cachedINTERLUDE2C==caller-current; no normalwinnercache. Absentinterlude/delegate in this config permits exact own state machine withoutinventedgenericcache. | ReusedM8-012/M15-055/M15-006 checkedrows; independently primarycheckedB scheduler packet. Actualconfigurednativeowner can be wired to managerIManagedActivity equivalents under explicit suppliedadmission. Sourcefullrawpicker is inherited boundary, not silently simulated. |
| H8 end-to-scheduler boundary | EndnextactionH19 acts on actualmanagercurrentbehavior; noaction immediately allowsbaseUpdate2 atnormalmanagerstep, currentaction keepsrunning butcallbackdestroyed. Sparked desiredstate4 Complete returnsnull; nativeparentFreeplay then loopsnextactivitypick. Do not call Complete repeatedly while leaving ownerselected, inventonceflag, auto-restartsameSpark, or fabricatefallbackactivity. ActualownerOnDeselected is the sourceequivalent lifecycle boundary; if parentpicker absent, expose/report that exact missingnextpick after null ratherthan claimengineFreeplay complete. | Ownstate4at005B1E00..1E24 and parentFreeplay005AE68C..E762 checkedexistingrows. M15-006 rawpickerpostnull remains inherited. Normalownercompletion and commonStop are supported independently of pickinganotheractivity. |
| H9 rawreplies | NativeStartedunionctor00727DFE sets tag83 halfword andcopiesunlockwordtopayload+8; Ended00727E88 sets tag84 andcopiesboolbyte+8. StartedPack0071AF00..1C writes4bytes viaWriteBytes; EndedPack0071B00E..20 writesonebool. Wrappers005B171C/2094 broadcast through externalvt1C, thenClearCurrent. | Newdirectprimaryknown-first verifiednativeinstructions. Actualstackreportchannel is existing CozmoEngine.DeliverAnimationReportToApp + synchronous AnimationMessages.Deliver, followed by existingqueuedrawAPI drain, same E2G framing ascheckedownobjective. Native tag/payload shapes do not requireinvented ad-hoc typedpublicevent. |
| H10 ownfinaldispatch/deletion | CommonOnDeselected's ownvirtual+2C dispatch is3654..365C, before3660return. Specialized1780 Reset→delegate→CubeStopAll (veneer8CBE3C ARMtail→PLTcheckedbelow); own destructor1484..1500 releaseshandles/subscriptions andbase, noComplete. | DirectprimarybaseOnDeselected dumpthrough3660, ownReset/dtorabove. Existing cube-lightStopAll implementation M4-018/045 must be actualhelper, no directworld-fieldclear. |

The source-owned finite questions identified by builder inspection now have explicit H1–H10 contracts. No remaining source UNKNOWN is to be filled inside the shipped own class/count/end/reply path. Remaining boundaries are **inherited and named**: rawActivateSpark/picker including post-null nextpick; sibling bodies if chooser selects them; fullupstream44hostserializer/visiongeometry; existing planner/face/audio runtime; other wholeframework/factory claims. Existing class/count/end pipeline can be built/checked under explicit suppliedadmission, canonicalrequested/activefields and real localobservation/firmwareinputs. This is not a claim those inherited paths are complete or permission to simulate them.

Wholepacket independentB review remains required. Malformedreload unwind branch11AA..1255 is constructor cleanup for invalid inputs, outside shipped validconfig and covered by standing M1-034 where terminating; no new parserbehavior inserted. Other classparameterbranches remain recorded in S rows; any implementation omittingdefinedsupportedbranch must leave wholeM15-059 unsettled. No status/inventory/approvalchanges made.


### Final exact time/public-state joins

H11 **Configured native ActivityID is21 (0x15).** Primary known-first ReadConfig005B2A80 calls ExtractActivityIDFromConfig,005B2A84 stores byte+34. Extract005B29B4..2A02 uses configuredactivityID string andActivityIDFromString. Native FromString initializer00766EF2 resolves literal00C199E8 `SparksKnockOverCubes`, constructs the string atstack150, then00766F0A `movs r0,#0x15` and00766F14 `strb.w r0,[sp,#0x15C]` fills thatpair'svalue; the nearby0x14at14C belongs toprecedingSparksGatherCubes, not thisentry. Independently, native ActivityID EnumToString00766C50..62 indexes table01033070; slot21 at010330C4 contains00C199E8. Therefore commonselected005B3154 PublicStateBroadcaster.UpdateActivity receives**byte21** for actualSparkactivity, and its entry/exitDAS name uses nativeSparksKnockOverCubes, not hostnumericToString. Do not reuse behaviorID162 orunlock5 asactivityID.

**Exactf32 expressions:** define NegEps = bitsB727C5AC (approximately -9.999999747e-6f), not a doubleliteral. Minimum uses18FA VADD.F32(start,min),18FE VLDR from005B1A44,1902 VADD.F32(roundedSum,NegEps),1906VCMPE and190E BLS (ordered<=). Waittimeout uses1930 VADD.F32(start,max),1934 VADD.F32(sum,sameNegEps),1938VCMPE/1940BLS. waitfalsehardmax uses2028VADD.F32(start,max),202CVLDR005B2090samebits,2030VADD.F32,2034VCMPE/203CBHI skip (unorderedalso skips), thenpointerchange andWait/state3. No widened deadline orsingle-rounded `(double)(start+window-eps)` surrogate. This clarifies S17/S22; no sourcechange.

H10 veneer completed: ownOnDeselected005B179E→Thumb8CBE3C switchesARM;8CBE40 `ldr ip,[pc]` /8CBE44 `add pc,ip,pc`,literal8CBE48 resolvesPLT004B1C44 **CubeLightComponent.StopAllAnims**. Observedadapter005B21C6→8CBE4C/ARM8CBE50 resolvesPLT004B1CD4 ownHandleMessage<RobotObservedObject>. Live-weakbutempty-source StopLooping632510 callsnativeWarning632520,thenreturnsfalse via6324E8/63250A; invalid/stalelocator usesInfo6324E4. No silent realfailure diagnostics.

Packet status: **source-complete for finite configured own path, independent B wholepacket verdict pending; implementation not released by extractor.** Exactcaller joins/includePlayArbitrary/getin/end/API/cleanup are present above. Existing parentadmission/postnullpicker, siblingchoices, fullupstream44hostserialization and separatelyowned planner/vision/audio remain named boundaries. No officialrecordsettled, nohardwareclaim.


H12 **Cube StopAllAnims is one layer, not every layer.** Last builder API question resolved by known-first direct `.so` read of00638D58..7A (`.scratch/sparks-knockover-source/cube-stopall-native.txt`):638D5E `movs r2,#1`,638D60 stores layer1atsp+C;638D62 `mov.w r2,#-1`,638D68 stores invalid/allObjectIDatsp+8;638D6A setsObjectIDrefsp+4;638D72 setslayerrefsp+C;638D74 calls **StopAllAnimsOnLayer(layer1,ObjectID-1) exactly once**. No userlayer0/statelayer2 clear and no extra inlinewire. Accepted M4-018 inventoryW2/W4/P1/P2/P3/P5 owns callee stopflags, normalUpdate, pops/callbacks/default-layer choice. Existing C# `CubeLights.StopAllAnimsOnLayer(CubeLights.EngineLayer, null)` is the corresponding already callable all-object composition (Lights.cs1329); native layer1=EngineLayer, null selects allobjects. A small source-equivalent wrapper/exposure is sufficient; source does not justify directworld-fieldclear or newanimation logic. This completes H10's callee boundary and corrects earlier broad narrative phrase 'stops every cube animation'. No accepted M4 contract changes.
