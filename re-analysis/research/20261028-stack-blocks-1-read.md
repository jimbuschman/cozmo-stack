# STACK-BLOCKS-1 — regular configured StackBlocks source packet

2026-10-28. Answers `re-analysis/jobs/trial-m13/STACK-BLOCKS-1.md`, started after SparksStack publication `b691cc61` and full 6557 PASS. PROJECT_STATE, current REVIEW and the job were read. Extractor A: source only; no code, tests, git operations, hardware or official record changes. Actual model/effort telemetry is unavailable. Native authority is local `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Known.py preceded new native reads; decompiler text is not evidence.

This is an additive delta to the complete independently checked Stack class and placement path. It does not repeat that extraction or claim whole PlayAlone admission/competition. The existing operator-approved target-validity correction and PlaceRel-only unused-ID policy persist unchanged. No new contradiction or departure was found.

## Current owning records

Current title, status and evidence were read directly before the ownership claims below. Quotations do not change their statuses.

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
  "id": "M12-012",
  "title": "What makes a cube a valid bottom to stack on: resting flat within ten degrees",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0063C5C4 CanStackOnTopOfObject; 0x0063C654 CanInteractWithObjectHelper",
    "0x0063C66C movw r1,#0xB8C2 + 0x0063C670 movt r1,#0x3E32 = 0.174533 rad = 10 deg; 0x0087751C IsRestingFlat",
    "the 15.0 is owned by M13-007",
    "IsPoseTooHigh 0x00877954: 0x00877978 D=GetDimInParentFrame<'Z'> (0x00557794); 0x00877990 s0=D*f1; 0x00877994 s16=D*f3; 0x00877998 s18=D*f1+f2; 0x008779A0 pose.z ([r0+0x28]); 0x008779AA D*f3+pose.z; 0x008779AE +1e-5 (0x3727C5AC); 0x008779B2 vcmpe; 0x008779BA movmi r0,#1",
    "0x0063C6C0..0x0063C730, 0x0063C78A..0x0063C792, 0x0063C5FE..0x0063C61C, 0x0063C840, 0x0063C8C2..0x0063C8D2",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q7"
  ]
}
```

```json
{
  "id": "M12-026",
  "title": "PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005558F4..0x00556300 the function; 0x005561A0 threshold call; 0x0055E1C6, 0x005B5908..0x005B5918, 0x005B7030..0x005B7054 the callers; 0x005B6F5C..0x005B70DA IsAtPreActionPoseWithVisualVerification",
    "re-analysis/research/20260929-R-VIS-pre-extraction.md Part 3 item 12 (rows 12.1-12.18); re-analysis/research/20260929-R-VIS-verify-pre-items11-13.md (all PASS); re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q2 rows for A,B sources",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q3, Q4; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q3, Q4"
  ]
}
```

```json
{
  "id": "M12-030",
  "title": "DriveToPlaceCarriedObjectAction: Init, CheckIfDone, IsPlacementGoalFree and ComputePlacementApproachAngle",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00559CD4, 0x00559DB8, 0x00559FA4, 0x0055A070",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q13; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q13 (PASS; the angle helper 0x00550858 and its axis table unopened)",
    "0x004E38D4, 0x0087660E, 0x004E0C4C..0x004E0C58, 0x00550904, 0x00627148..0x006271C2, 0x00626870..0x006268D6, 0x0062750E..0x00627530, 0x0055A070..0x0055A1B6, 0x00550858..0x005508C4",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q4-Q6; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md"
  ]
}
```

```json
{
  "id": "M12-031",
  "title": "IDockAction::GetPreActionPoses and SelectObject/GetObstacles",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005508C8..0x00551200",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q10; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q10 (PASS)",
    "0x005B6F5C..0x005B70DA, 0x0055EE10..0x0055EE66",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q9, Q3.7; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md"
  ]
}
```

```json
{
  "id": "M12-039",
  "title": "ComputePlacementApproachAngle 0x005504A0 and the X/Y angle functions",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005504A0, 0x00550858..0x005508C4, PLT 0x004AB914, 0x004AB908",
    "re-analysis/research/20260929-R-VIS-verify-M12-fix2.md objection 6",
    "re-analysis/research/overnight/20261014-ov05-m12-039-check.md: B1 ComputePlacementApproachAngle 0x005504A0..0x0055071C; B2 GetRotatedParentAxis<Z> 0x005507A0..0x00550856, helper 0x00550858..0x005508C6; B3 RotationMatrix3d angles 0x0084B370..0x0084B47E (M11-068); B4 Radians 0x0084C832, 0x0084C846, 0x0084C87C..0x0084C936, 0x0084CB04..0x0084CB28."
  ]
}
```

```json
{
  "id": "M15-002",
  "title": "Chooser factory, scoring, strict-priority and selection chooser semantics",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BSRunnableChooserFactory::CreateBSRunnableChooser 0x00609C88",
    "ScoringBSRunnableChooser::ReloadFromConfig 0x00609F8C",
    "ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060A3D8",
    "StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060B23E",
    "SelectionBSRunnableChooser::OnSelected 0x0060AF20"
  ]
}
```

```json
{
  "id": "M15-003",
  "title": "Activity start/end predicates and cooldown lifecycle",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IActivityStrategy constructor 0x005B4EC8",
    "IActivityStrategy::WantsToStart 0x005B529C",
    "IActivityStrategy::RandomizeCooldown 0x005B5408",
    "IActivityStrategy::WantsToEnd 0x005B5444",
    "IActivityStrategy::SetCooldown 0x005B54E4"
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
  "id": "M8-003",
  "title": "Scored selection: emotion scorers or the flat score, times the repetition and running penalties",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::ReadFromScoredJson 0x005bc488 reads five keys, not four: \"emotionScorers\" into a MoodScorer at +0xdc, \"flatScore\" into the float at +0x100 (0x005bc4ca/0x005bc4e0), \"repetitionPenalty\" into the graph at +0xe8 (0x005bc4ee), \"considerThisHasRunForBehaviorObjective\" into a behaviour objective at +0x10c through BehaviorObjectiveFromString (call 0x005bc5a0), and \"runningPenalty\" into the graph at +0xf4 (string 0x005bc602; add.w r6,r4,#0xf4 0x005bc5f8; AddNode(0,1,true) 0x005bc678).",
    "IBehavior::EvaluateScoreInternal 0x005beec2: if the MoodScorer vector at +0xdc/+0xe0 is non-empty it tail-calls MoodScorer::EvaluateEmotionScore (0x8cbf8c, with robot+0x440); otherwise it returns the flat score at +0x100 (vldr s0,[r0,#0x24] after the pre-index 0x005beed4). The two are alternatives, not addends.",
    "MoodScorer::EvaluateEmotionScore 0x0067c9b8 walks the scorers, evaluates each one's graph at its emotion's value or at the value minus the value sixty ticks ago when trackDelta is set (Emotion::GetHistoryValueTicksAgo(60) 0x0067c9f4), and returns the mean (0x0067ca6e..0x0067ca72); a result within 1e-05 of zero ends the walk and the score is zero (0x0067ca50).",
    "IBehavior::EvaluateScore 0x005bef60: while running it uses EvaluateScoreInternal plus the float at +0x104 (vldr s2,[r4,#0x104] 0x005bef80; vadd.f32 s16,s0,s2 0x005bef88) and multiplies by EvaluateRunningPenalty when +0x111 is set (ldrb.w r0,[r4,#0x111] 0x005bef84; cbz 0x005bef8c; blx EvaluateRunningPenalty 0x005bef90; vmul.f32 0x005bef98). Otherwise it scores only a behaviour that IsRunnableBase and passes the vtable+0x50 test, applies the repetition penalty only when +0x110 is set and now >= +0x108 (ldrb.w r0,[r4,#0x110] 0x005befd4; cbz 0x005befd8).",
    "IBehavior::EvaluateRunningPenalty 0x005bef22: returns 1.0 when +0x34 <= 0, otherwise evaluates the graph at +0xf4 at now - +0x34 (vldr s0,[r4,#0x34] 0x005bef26; add.w r0,r4,#0xf4 0x005bef4a; b.w 0x8cbf9c 0x005bef5a).",
    "The four constructor fields: +0x104 is the running-score bonus (IncreaseScoreWhileActing 0x005bf02c adds to it only while the current-action handle +0x84 is non-zero; ScoredActingStateChanged 0x005bf046 clears it); +0x108 is the repetition-penalty suppression threshold; +0x110 enables the repetition penalty; +0x111 enables the running penalty (both set to 1 by the halfword 0x0101 at 0x005bbd34, with no other writer).",
    "EmotionScorer::ReadFromJson 0x0067aabc names emotionType, scoreGraph and trackDelta. No shipped behaviour or activity config carries an emotionScorers block, so every scored behaviour in the app is scored by its flatScore alone.",
    "IncreaseScoreWhileActing 0x005bf02c is called only from concrete behaviour transitions (ten call sites, M7/M15); ScoredActingStateChanged 0x005bf044 has no engine caller, so +0x104 is never cleared in-engine."
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
  "id": "M11-065",
  "title": "Pose3d / Rotation3d / Transform3d arithmetic: double unit quaternion, float translation, float point rotation, renormalised composition (rows R1-R34, R38-R40)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: rows R1-R34 and R38-R40 (storage 0x0084A656.., Normalize 0x00849DB8..0x00849E52, product 0x00849EF4..0x00849F8E, Rotation3d*Point3f 0x0084ACA6 -> UnitQuaternion<float>* 0x00849C1C..0x00849CF8, GetAngleDiffFrom 0x0084A694..0x0084A746, GetAngleAroundZaxis 0x0084AA1C..0x0084AAB8, GetRotationMatrix 0x0084AAE0..0x0084ABEC, Renormalize 0x008494E0..0x0084969E, Transform3d ops 0x0084B92A..0x0084BB98, Pose3d ctors 0x0084683C..0x00846DEE, PoseBase ops 0x0084505C..0x008451EA, IsSameAs_WithAmbiguity 0x00846EA4..0x008471A6, RotationMatrix3d ops 0x0084B0E4..0x0084B1B4)."
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
  "id": "M15-024",
  "title": "The whiteboard failure memory: tables, caps, FailureInfo and EntryMatches",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "AIWhiteboard::DidFailToUse / SetFailedToUse with a pose: per-object-id tables with caps {1, 1, 10, 1} (PlaceObjectAt keeps the last 10); EntryMatches expiry is strict with 1e-5f slack; id -1 means any object; the floor path's DidFailToUse is 0x005E0B78 (failure 2, id -1, 100.0f, pi) and the stack-on filter's is 0x005E1D24 (failure 1, 20.0f, pi/8)",
    "Pose3d::IsSameAs_WithAmbiguity 0x00846F3C (the per-axis and angle test) is unread: RECOVERABLE_GAP",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ]
}
```

## Checked shared path reused without re-extraction

Reuse `20261028-sparks-stack-1-read.md` S1–S14, P1–P10 and C1–C5, plus `20261028-sparks-stack-1-source-followup.md` and `-source-followup-2.md`, `-3.md`, `-4.md` (SF1–SF11). Those packets and their independent checks establish the actual class factory/constructor, ObjectID wrappers, native pickup/DriveTo/PlaceRel/Ground actions, corrected validity and one-retarget gate, real callbacks/selection/float32 freshness/scheduler order, exact success output order, stop/resume/destruction and completion availability policy.

In particular, SF9 supersedes SF8's mistaken pure-query wording: actual native pre-action recipients select the target after null/carry guards, including the helper routes checked in SF11. Helper processing precedes the manager phase, but later S8 validation may still cancel recovery. The PlaceRel normal payload's four unused IDs use the approved policy; Ground's uninitialized full raw payload remains under the existing M1-034 availability guard. None of these boundaries is changed for the regular instance.

## Exact ordinary instance deltas and own consumers

| Row | Source behavior | Primary evidence / ownership |
| --- | --- | --- |
| R1: shipped inputs | The regular asset has class/ID StackBlocks, needsActionID StackCube, executableBehaviorType StackBlocks and requiredUnlockId StackTwoCubes. stackInAnyOrientation and alwaysStreamline are absent. No local replacement values are invented. | `obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/stackBlocks.json`. M7-002/M7-018 configuration and factory; shared native class case `0x0059CFE6`. |
| R2: enum recipients | Native behavior byte is A0 (160); needs StackCube is 42; executable StackBlocks is 9; required unlock StackTwoCubes is 10. Unlock 9, queried by the class/cache, is RollCube, a distinct value. | Behavior EnumToString `0x0076B32C` uses table `0x01033330` with index byte XOR80: slot `0x010333B0`→`0x00C1A0F1` StackBlocks. Needs table `0x01033D40`, slot `0x01033DE8`→`0x00C1C7E3`. Executable EnumToString `0x0076D50C`, table `0x01033730`, slot `0x01033754`→`0x00C1A0F1`. Unlock slots `0x010340E8`→StackTwoCubes and `0x010340E4`→`0x00C1C062` RollCube. A/B independently checked the data. |
| R3: defaults | Own constructor initializes any-orientation byte134 false, then reads stackInAnyOrientation with JSON default false. Thus this regular instance has any=false. Common alwaysStreamline D9 retains the checked false default when absent. The actual active Spark manager fields still compute D8; do not force D8 false merely because this asset is regular. With activeSpark55/no spark it is false. | Shared S2 and K/common-base rows; own `0x005C93FA..0x005C942A`; common ReadFromJson alwaysStreamline `0x005BC200..0x005BC21E`, Init hard/soft computation `0x005BCCAA..0x005BCCC2`. M8-001. |
| R4: live mode | Target refresh and own Update both query actual progression IsUnlocked(9,true). Query true selects top5/bottom3 even for this ordinary asset. Query false with any=false selects top4/bottom2. Recovery and the optional refresh hook use the same current mode; no new snapshot or per-tick refresh cadence is invented. | `0x005C94A6..0x005C9500`, `0x005C984E..0x005C988C`, shared S4/S8/S12/S14. Canonical progression owner/M15-044 input remains required; unavailable input is MISSING, not false. |
| R5: exact extra getters | The own +2C getter returns set{1} for any=false, independently of current unlock9. The +7C hook forwards to target refresh if called. Neither establishes an extra readiness gate or automatic scheduling consumer. | Shared S14/getter row; `0x005CA1EA..0x005CA20C`, `0x005CA224`. The inherited unbuilt external consumer frontier remains explicit. |
| R6: target/validity consumers | The top and bottom ObjectID wrappers/scalars are unchanged. Initial runnable refreshes both best IDs; carry-dependent Init chooses Stack only if actual carried ID equals the saved top. Own validity now uses the actual current 4/2 lists on the locked branch, including the approved both-valid skip and bottom-invalid neighbor-on-BOTTOM test. Closest-bottom receives intention2 there, preserving current ID on ties/conversion failure. All helper targets, callback and one-retarget ordering remain the shared source. | Shared S3–S10, S12–S14; exact changed mode inputs at `0x005C9876` and `0x005C988C`; closest call `0x005C9A5E` receives the selected bottom intention. No class-body redesign is supported. |
| R7: output/needs | Success still emits objective40,true first; if not D9/D8 it starts plain trigger21A and scores0.8; NeedActionCompleted(0) runs last and substitutes THIS asset's StackCube42, not Sparked43. The objective still reaches the actual API and any matching constructed subscribers; ordinary identity does not suppress it. There is no own Sparked completion call added to PlayAlone. | Shared S11 and real report/needs recipients; `0x005C9EA4..0x005C9F14`, configured needs R1/R2, M15-008. Normal D8=false/D9=false retains 90f-through-Radians pickup turn and final animation; actual manager inputs still govern the hard case. |
| R8: executable metadata | Common parser reads the executable string, converts it and stores byte+6C. Value9 is metadata for the separate executable finder/Selection route; PlayAlone's Scoring chooser identifies this entry by behaviorID. Do not invent an executable readiness or winner shortcut. | `0x005BC1B2..0x005BC1EE`; shared Sparks-KnockOver S4. The separate executable finder remains an inherited M8-013/M15-002 boundary, not a new feature exclusion. |

## Ordinary cache4/2 — new finite primary rows

The common cache owner, DefaultBest algorithm, signed ID ordering, whiteboard/docking predicates, configuration lifetimes and float32 lazy timestamp gate are already checked in C1–C5 and the pickup packets. These rows add the actual ordinary bindings and distinct predicates. B independently checked the new source against A's packet.

| Row | Exact native behavior | Primary evidence |
| --- | --- | --- |
| O1: filter construction | Key4 binds TopAxis filter stored at sp24, using actual predicate GOT103EC94. Key2 binds BottomAxis filter at sp1C, GOT103ECA0. Both default filters admit families1/2 and retain their own predicate. Literal address order is {2,1}; the sets are sorted by native enum comparison. | Top construction `0x0056F7A6..0x0056F810`, literal `0x00C589EC`; bottom `0x0056F8A8..0x0056F914`, literal `0x00C589FC`; key binding `0x0056FC4C..0x0056FC62`. B's preliminary C58A00 address label was corrected before freeze; it did not change the set. |
| O2: dependency/best | Bottom2 ensures top4 before its own cache entry. Top4 has no dependencies. Both entries install the same actual DefaultBestObjectFunction and have separate best/valid/timestamp state. | Dependency pair `0x01023580` key2→`0x0105A9A4`; initializer `0x004D705E..0x004D7072` inserts sole literal `0x00C589C0=4`. Key4 pair `0x01023590`→empty `0x0105A998`. Best binding key2 `0x0056FCEC..0x0056FD16`, key4 `0x0056FD4C..0x0056FD7A`, GOT103ECC0. |
| O3: carried top4 | If actual carried ID is not -1, locate the actual carried object. Require pointer identity with the candidate AND candidate's LOCAL pose transform GetRotatedParentAxis<'Z'> result exactly3. This is not the root-pose branch below and not an ID-only equality test. | `0x005705D0..0x0057061E`: lookup `0x005705EA`, direct GetPose/GetTransform `0x005705F2..0x005705F6`, matrix/axis `0x00570600..0x0057060C`, pointer XOR plus axis XOR3 `0x00570608..0x00570618`. |
| O4: no-carry top4 | Tail-call CanPickupAxisCheck. First evaluate the SAME NoAxis eligibility used by checked intention0, including whiteboard and actual docking eligibility; require result exactly1. Then query IsUnlocked(9,true), with result variable initialized1. If unlock result !=1, RETURN1. Only result==1 enters a ROOT-expressed pose rotation axis==3 test. Preserve this literal direction; names do not authorize reversing it. | `0x00570620..0x00570628`→veneer8CB7AC; predicate `0x0057052C..0x0057058E`. Shared NoAxis at `0x00570534`; exact1 test `0x00570538..3A`; unlock/default1 `0x0057053E..4E`; root conversion `0x0057055C`; axis test `0x00570574..84`. Therefore locked-mode no-carry top eligibility is not simply an upright filter. |
| O5: bottom2 | First call shared CanUseAsStackBottomHelper with intention4. It ensures/excludes top4, performs the same action1/3-second whiteboard test, requires family2 and actual CanStackOnTop. Only helper result exactly1 proceeds to candidate LOCAL pose matrix axis==3. | `0x00570632..0x0057066A`, dependency arg4 at `0x00570636`; shared helper `0x0057154C..0x005715C0`; direct local GetPose/GetTransform `0x00570646..4A`; axis comparison `0x0057065C..64`. M15-024/M12-012 bodies remain their existing owners. |
| O6: lazy cadence | Reuse the exact entry timestamp and best-retention contract for each NEW entry: dependencies first; strict abs(f32(last-now))<bits3727C5AC suppresses rebuild; otherwise stamp now before collection, rebuild signed ascending unique IDs, retain old best if valid, otherwise call installed DefaultBest. No pose event, unlock event or repeated getter forces an extra reset within epsilon. | Shared C5: `0x00571040..0x005710CC`, `0x005711E4..0x005712B6`. Entry initialization and selector algorithm are unchanged checked recipients. |

### Literal axis3 and its actual float32 recipient

`GetRotatedParentAxis<'Z'>`, `0x005507A0..0x00550854`, loads matrix ROW2, columns0,1,2 through SmallMatrix<float> access (`0x005507B4..0x005507C6`). It compares absolute float32 values, choosing Y only when abs(Y)>abs(X), then Z only when abs(Z)>abs(selectedXY). Ties retain X before Y before Z. Selected positive components return +1/+2/+3; nonpositive return negative codes. Thus literal3 requires positive row2.Z to strictly dominate both other magnitudes. It is not a general resting-flat test. The new cache callers pass a null optional component-output pointer.

Use the actual LOCAL or ROOT matrix specified by O3–O5 and the checked native float32 matrix construction. Existing `RotatedParentAxis.Get(Mat3,2)` currently compares double components; it cannot silently replace these native float32 comparisons when distinct doubles round equal. A and B independently checked this finite body. The current M12-030/039 and shared rotation records remain broad owners/frontiers; this row does not settle them or authorize a broad math rewrite.

## Actual PlayAlone caller: what matters and what remains unavailable

The asset is `obb/assets/cozmo_resources/config/engine/behaviorSystem/activities/freeplay/playAlone.json`: BehaviorsOnly, Simple strategy, 25-second end-duration input, 30-second cooldown, Scoring chooser. StackBlocks has flatScore0.8 and repetition graph[(5,0),(30,0.5),(100,1)]. Its StrictPriority interlude contains EarnedSparks and five Needs request IDs. These are actual inputs, not proof that Stack wins admission.

B's narrow primary check and A's reciprocal spot-check establish:

- Simple ctor `0x005B4E60..0x005B4E70` uses the common strategy then its own vtable. Start/end hooks at `0x005B4E86` / `0x005B4E8A` return1/0. BehaviorsOnly Update at `0x005A7372` returns0. Common strategy duration/cooldown and lifecycle remain M15-003/M15-006; the 25-second input does not establish a host timer that immediately cancels Stack.
- Scoring DOES change desired behavior while Stack runs. `0x005BEF60..0x005BEF98` reads running state, calls the running scorer, adds actual while-acting bonus+104, then applies the running penalty when enabled. Stack's score virtuals use the common base rather than a private override. Native chooser `0x0060A466..0x0060A4A8` treats a running entry via its duration graph/retention score and challengers via the actual Robot RNG; do not replace this with fixed priority or a guaranteed Stack winner. The exact shared scoring owners are M8-003/M8-013/M15-002, not newly accepted here.
- Freeplay `0x005AE35C..0x005AE37C` checks pending reward before strategy WantsToEnd. Common IActivity `0x005B387C` can consult interlude when a scored choice differs. Sibling readiness, competition, interlude and the subsequent parent transition remain real unresolved inputs. They are not false/no-effect defaults.

**Visible inherited frontier:** NativeRuntime currently rejects PlayAlone admission. This packet supports the exact regular leaf through explicitly supplied canonical manager admission/choice inputs; it does not claim whole PlayAlone runtime selection. Keep unavailable parent readiness/scoring/interlude visible as MISSING. Do not implement a host timer, chooser shortcut, fake sibling false or success adapter to fill it. This is neither an operator feature exclusion nor a reason to postpone the complete cache4/2 and own-class work. Separate executable selection is also inherited, not required for PlayAlone's scored ID lookup.

## Current code discrepancy and meaningful verification

The native StackBlocks class already parses either configured instance and has the shared lifecycle. The concrete blocker is the actual target cache: current NativePickupTargetCache has entries0/3/5 and explicitly rejects ordinary4/2, so regular RollCube-locked readiness cannot run. Add actual distinct4/2 recipients from O1–O6; never alias to5/3. The float32 axis recipient must be bound correctly at the new callers. Preserve the exact ordinary needs42 and executable metadata/frontier rather than copying Sparked43/owner27 semantics.

Production regressions should use the actual shipped regular factory/config and canonical progression9 false/true, separate cache entries/dependency/cadence/retained best, carried-pointer identity and local/root pose differences, float32 tie/sign cases, ordinary pickup/stack/retarget/recovery cancellation and shared callbacks, objective40→optional21A/score→needs42 order, stop/replacement and unrelated work preservation. Label supplied parent choice/admission, scene and firmware inputs; do not claim a supplied choice tests whole PlayAlone. Expected values come from source, not planner-produced fixture geometry or forced completion. Extractor runs no tests.

## Additive ownership proposal and bounded conclusion

Leave the frozen `PROPOSAL-SPARKS-STACK-LIFECYCLE.md` unchanged. The new ordinary4/2 coverage is an additive source-coverage note for that pending unowned lifecycle proposal, allowing manager/Claude to consider a single shared StackBlocks lifecycle scope across the regular and Sparks configurations when allocating it. Do not multiply records per configuration or silently narrow an existing approved record. This is source coverage, not a status change or whole-class acceptance while inherited inputs remain unresolved.

Existing narrow M12-012/026/031/042 records retain their entire titles and other live recipients. Reusing their correct bodies for ordinary Stack does not settle their whole scopes. The separate proposed concrete PlaceRel verification/completion and Ground completion steps remain as already recorded; no duplicate configuration or helper record is proposed.

No new source UNKNOWN remains in the ordinary cache4/2 predicates, dependencies, exact instance inputs or changed own-class consumers. The explicitly named PlayAlone admission/scoring/interlude, executable finder, unnamed external +2C/+7C consumers and inherited planner/vision/math/audio/raw-report/scheduler frontiers remain visible. Existing operator approvals are reused only within their scopes. Independent B whole-packet verdict is pending; only the manager may release implementation.
