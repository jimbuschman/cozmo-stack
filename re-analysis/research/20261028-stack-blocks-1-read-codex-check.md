# STACK-BLOCKS-1 independent source check

2026-10-28. Status: IN PROGRESS — source-only; no implementation or tests authorized by this report. PROJECT_STATE.md, the current job and REVIEW.md were read first. Actual model telemetry is unavailable.

Scope is the ordinary configured StackBlocks instance and the finite differences from the already checked SparksStackBlock path. Shared placement, Ground, action-child and completion evidence is reused without another extraction. Existing target-validity correction and PlaceRel-only unused-slot policy remain in force. No official fidelity, inventory or approval file is changed.

## Current ownership before conclusions

The following current records' full title/status/evidence were read directly from the manifest. Their scope is not silently narrowed by this job.
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
  "id": "M8-002",
  "title": "The repetition penalty graph and its suppression window",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "config/engine/mood_config.json:88-99 ships defaultRepetitionPenalty with the nodes (0.0, 0.0) and (30.0, 1.0).",
    "IBehavior::ReadFromScoredJson 0x005bc488 reads repetitionPenalty into the GraphEvaluator2d at +0xe8; a missing or unreadable graph warns (IScoredBehavior.BadRepetitionPenalty) and leaves it empty, and an empty graph gets the single node AddNode(0, 1, true) at 0x005bc566 (0x005bc678).",
    "IBehavior::EvaluateRepetitionPenalty 0x005beee6 returns 1.0 when the last-run stamp +0x30 <= 0 (vldr s0,[r4,#0x30] 0x005beeea; movle.w r0,#0x3f800000 0x005beef8), otherwise evaluates the graph at +0xe8 at now - +0x30 (add.w r0,r4,#0xe8 0x005bef0e; b.w 0x8cbf9c 0x005bef1e).",
    "The +0x30 stamp is written by IBehavior::Stop 0x005bd11a/0x005bd11e as BaseStationTimer::GetCurrentTimeInSeconds().",
    "The +0x108 suppression threshold: StopWithoutImmediateRepetitionPenalty 0x005beea0 sets +0x108 = now + 1.0 (vmov.f32 s0,#1.0 0x005beeb0; vadd.f32 0x005beeb8; vstr s0,[r4,#0x108] 0x005beebc); EvaluateScore applies the penalty only when now >= +0x108 (vldr s0,[r4,#0x108] 0x005befe2; vcmpe.f32 0x005befea; bpl 0x005beff2).",
    "StopWithoutImmediateRepetitionPenalty's only three callers are BehaviorPickUpCube::UpdateInternal 0x005c685c, BehaviorStackBlocks::UpdateInternal 0x005c991c and BehaviorBuildPyramidBase::UpdateInternal 0x005dd110 (all M7/M15); IBehavior::Stop does not call it."
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
  "id": "M12-042",
  "title": "PlaceRelObjectHelper lifecycle: ctor, Init (pre-action check, DriveTo delegation), StartPlaceRelObject (3-attempt counter), RespondToPlaceRelResult, MarkFailedToStackOrPlace",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: ctor 0x005B8F00..0x005B8F82; Init 0x005B8FD0..0x005B90FE; Start 0x005B9168..0x005B930E; Respond 0x005B94C0..0x005B952E; MarkFailed 0x005B93A8..0x005B9460; invokers 0x005B956A, 0x005B95D6."
  ]
}
```

## Independently refreshed finite primary facts

Known.py was run before new native reads. Primary source is the shipped libcozmoEngine.so; decompiled notes were not used as authority. The prior checked shared Stack packet remains the source for unchanged placement and lifecycle rows.

- Top4 predicate 0x005705D0: when a carried ID exists, the actual located carried pointer must equal the candidate, and the candidate LOCAL pose's rotated parent Z axis must equal3. The combined XOR/OR test is at 0x00570608..0x00570618. With no carried ID the tail 0x00570628 calls the axis-check pickup predicate.
- CanPickupAxisCheck 0x0057052C first requires the shared NoAxis predicate to return exactly1. It then sets the result register to1 and asks IsUnlocked(9,true). When that query is NOT1, branch 0x0057054E returns1 without the extra root-axis check. Only query==1 obtains the ROOT pose and requires rotated parent Z axis3. This direction is explicit; names do not justify an always-upright rule.
- Bottom2 predicate 0x00570632 invokes the shared bottom helper with dependency intention4, requires exact1, then checks the candidate LOCAL pose axis3. It is not an alias of bottom3.
- Simple strategy constructor 0x005B4E60 calls the common constructor and installs vtable 0x01025A1C+8. Its start-internal cell points to 0x005B4E86 (MOV1/BXLR); its end-internal cell points to 0x005B4E8A (MOV0/BXLR). BehaviorsOnly Update vtable slot20 points to 0x005A7372 (MOV0/BXLR), reusing the prior checked no-op body.
- Common WantsToEnd 0x005B5444 evaluates configured duration thresholds in float32. The minimum deadline is f32(f32(start+min)+bits B727C5AC); the maximum is f32(f32(start+max)+bits3727C5AC). The instruction comparisons and ordered branches, not a double host deadline, govern acceptance. Otherwise it calls the internal-end virtual hook, false for Simple.
- Scoring does matter while Stack runs: 0x005BEF60 running branch uses the running-score internal virtual, adds +0x104, then optionally multiplies the running penalty. Scoring chooser 0x0060A466..0x0060A498 adds the current behavior's running-duration graph and0.1, with floor0.01. Non-running challengers receive the actual RandDbl at0x0060A4A8. This can change which behavior continues; it cannot be dismissed as admission-only metadata.
- IActivity::GetDesiredActiveBehavior 0x005B387C routes to virtual+30 unless its retained interlude/current branch applies; the common internal path uses the actual chooser. The PlayAlone scored ID list and the separate executable selection finder must not be conflated.
- The Freeplay caller 0x005AE35C..0x005AE37C checks pending Needs+3D8 before WantsToEnd(strategy+20,start+54). Its post-choice branch 0x005AE68C..0x005AE6B2 separately tests selected behavior running, pending reward and WantsToEnd. The 25s asset alone does not establish immediate unconditional behavior cancellation.

## Current implementation reach and pending verification

Current NativeBehaviorRuntime rejects PlayAlone admission: only the checked PutDownDispatch BehaviorsOnly route or an actual Sparked owner is admitted. BehaviorsOnly Update is bound, scoring has an actual RNG recipient, but no current claim follows that the full PlayAlone lifecycle/strategy/parent path runs. This is a concrete existing caller frontier, not an invented host timer or picker opportunity. The exact finite adapter scope must be explicit before implementation release.

Executable metadata belongs to common parsing and the separate Selection message path, not the PlayAlone Scoring ID lookup. Its unbuilt inherited finder remains named unless the job explicitly recovers it. Absence of a current reader is not a global no-effect claim.

The ordinary cache4/2 constructor/dependency/default-best/cadence packet and the exact config/enum/default joins remain pending A's coherent packet and reciprocal review. These predicate checks alone are not a whole-packet PASS. Existing narrow records can be proposed for settlement only if their complete current path is covered; the prior broader public/helper/geometry limits are not removed by adding native4/2. An additive coverage note on the pending Stack lifecycle proposal is preferable to a second record solely for another configuration of the same class, provided the final combined packet supports it.

## Asset, binding and score-virtual confirmation

The actual ignored shipped assets were read at `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/stackBlocks.json` and `activities/freeplay/playAlone.json`. The behavior has ID/class StackBlocks, needs StackCube, executable StackBlocks, required unlock StackTwoCubes, and no any-orientation or always-streamline property. PlayAlone has Simple max-duration25/cooldown30, Stack flatScore0.8 and repetition nodes(5,0),(30,0.5),(100,1). It ALSO has a StrictPriority interlude chooser (EarnedSparks and five needs request IDs). Their unresolved readiness cannot be treated as false; they are a named transition input frontier. No sibling bodies were audited.

Independent constructor/data check confirms sp1C maps to cache key2 and sp24 to key4 (0x0056FC4C..0x0056FC62); predicate GOT103EC94 is TopAxis and GOT103ECA0 is BottomAxis. Both use family words{2,1}. Exact literal addresses are C589EC for top and C589FC for bottom (PC-relative additions at56F7BA/56F8BE). A preliminary message cited C58A00, which begins one word later and yields the same set in reversed order; that address citation was wrong and is superseded here. This does not change the family set or behavior.

The dependency table1023580 key2 points to105A9A4, populated by4D7062..7072 from literalC589C0=4. Key4 points to shared empty105A998. Both default-best function bindings use actual GOT103ECC0. Shared per-entry timestamp, dependency-first lazy refresh and retained-best behavior reuse the already checked source, not an alias to entries5/3.

Direct primary data confirms NeedsStackCube42: slot1033DE8→C1C7E3; executableStackBlocks9: slot1033754→C1A0F1; BehaviorIDStackBlocks byteA0: EnumToString indexed slot10333B0→C1A0F1; unlock9: slot10340E4→C1C062 RollCube. Enum table functions/index transformations remain A's independently extracted navigation joins for reciprocal checking.

The actual Stack vtable addresspoint10279C4 has score cells+84/+88 at1027A48/1027A4C relocated to base EvaluateRunningScoreInternal/EvaluateScoreInternal. Base running5BEEDE tail-calls virtual+88. Thus this class has no derived score override to invent. This does not settle M8-003's broader current scope or other classes' overrides.

Manager accepted retaining the concrete caller/admission/interlude frontier without expanding into all PlayAlone siblings. The final packet must state which input boundary is explicitly supplied and which own steps actually run; a configured own-class claim is not a whole PlayAlone or raw parent-picker claim. No native caller can be replaced by an unsourced host duration/cancellation rule.

## Axis recipient and whole-record limits

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

Known-first direct5507A0..550852 confirms float32 row2 component loads, strict dominant-axis comparisons retaining X then Y on ties, and positive/nonpositive sign. The new predicates pass a null selected-component output pointer. Existing double Mat3 RotatedParentAxis.Get must not silently substitute at boundaries where different doubles round to equal floats. New native predicates need the source float32 matrix/pose recipient and ordered comparisons; this does not authorize changing all public geometry or settling whole030/039.

Current full unresolved text for012/026/031/042 was reread. The prior native Stack implementation removes some stale “no production caller” statements for exact native zero-offset helper routes, but it does not establish all offset/transform/public geometry/other caller branches. M12-012's historical Math.Acos note is partly stale (current resting-flat uses MathF), while the actual public legacy neighbor/history and FromGround limits remain. None of these whole records is proposed settled from the new4/2 cache subset. M8-003's current unresolved derived virtual-score overrides elsewhere remain outside this one class's verified base slots. M15-003/006 retain current whole caller/cooldown scopes.
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
  "id": "M7-019",
  "title": "M7 concrete reaction-behaviour state machines",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "reaction-class rows in M7 inventory Appendices E and F",
    "corrected ReactToCliff mood-event sequence 0x00604d70..0x00604d84",
    "ReactToImpact 0x006061f8..0x0060647c",
    "ReactToOnCharger 0x00606c94..0x00606e86",
    "ReactToPickup 0x00607750..0x00607d9c"
  ]
}
```
```json
{
  "id": "M12-028",
  "title": "The behaviour-owned PlaceRelObjectParameters producers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055E2CA, 0x0055E3E0, 0x0055EB74, 0x0055EC2A, 0x005B58C2 the installers; 0x005C9588, 0x005DC266, 0x005DCE44 the parameter producers",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1, Q2 and open questions 2 and 6",
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: rows 1-5. Stack 0x005C9520..0x005C961A; top 0x005DC08C..0x005DC322 (+0x005DD134, 0x005DC78A, 0x005DC7F0..0x005DC9A4); base 0x005DCDA4..0x005DCF38; UpdateBlockPlacementOffsets 0x005DD140..0x005DD3B4; CheckBaseBlockPoseIsFree 0x005DD650..0x005DD8FC with 0x0061F8EC..0x0061F9E0 and 0x00627F92..0x00628064."
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

The current factory/configuration, concrete-reaction, placement-parameter and needs-reporting records above retain their scopes. They do not become a whole derived Stack state-machine owner merely because they contain one of its calls. The existing pending Stack lifecycle proposal is therefore still the suitable allocation vehicle; no extra per-config ID is proposed. A narrow actual shipped-tree search found exactly two configs whose behaviorClass is StackBlocks: regular stackBlocks.json and sparkable/sparksStackBlock.json. Combined ordinary4/2 and5/3 coverage can support an additive whole shipped-class lifecycle proposal, without claiming arbitrary host variants or the whole PlayAlone parent.

## Final whole-packet verdict — 2026-10-28

**Bounded READ PASS** on A's complete `20261028-stack-blocks-1-read.md`, raw SHA256 `874E9403F37EC759EAE40AC32D7FD4EB076E23B5FB6B95F6E6DA38359B41DCE1`. This final verdict supersedes the earlier IN PROGRESS status. All19 quoted record title/status/evidence blocks were compared against the current manifest and match exactly. The final packet includes the current M15-024 quote; no source rows changed between its coherent candidate and final freeze.

The complete finite ordinary delta is supported: actual shipped config and enum/default inputs; live unlock9 versus any-orientation mode; distinct entries4/2 and their dependency, filters, predicates, selector and lazy state; carried pointer/local-axis and no-carry root-axis/unlock behavior; exact float32 axis recipient; ordinary target/runnable/validity/closest/carry/pickup/placement/recovery/success/needs/stop consumers, with unchanged bodies explicitly reused from the prior checked packet and SF1–SF11. Ordinary needs42 is distinct from Sparked43; the ordinary config does not itself force the shared hard-spark state false. No new source UNKNOWN or frozen-contract contradiction was found inside these finite changed joins.

The surprising CanPickupAxisCheck unlock direction, local-versus-root pose distinction and family-literal citation correction remain explicit. They must survive implementation and tests; names or visual upright heuristics cannot replace them. Source float32 matrix/axis comparisons are required at the new callers, including ties where double components round equal. Existing broad math records are not settled by this one recipient.

The whole own-path claim uses explicitly supplied canonical manager admission/choice inputs. Native PlayAlone admission is currently rejected, and scoring can change continuation while the behavior runs. Its actual strategy/competition/interlude inputs and subsequent parent transition remain visible MISSING frontiers; no fake false sibling readiness, host25s cancellation or guaranteed scored winner is authorized. Separate executable selection and unnamed external+2C/+7C consumers remain inherited boundaries. The packet does not claim a complete PlayAlone, executable API or autonomous parent picker.

Implementation may therefore proceed on the bounded combined ordinary class/cache path after manager release. Meaningful regressions must exercise actual regular shipped factory/config and both progression9 values, distinct cache4/2 versus5/3 ownership/dependency/timing/retention, carried identity and local/root orientation cases, literal float tie/sign semantics, actual pickup/placement/retarget/recovery/stop and preserved unrelated work, and exact objective40→optional21A/score→needs42 outputs. Parent choice, scene/firmware and explicit callback boundaries must be labeled. Tests must not force runner/helper success or generate input geometry from production release output. No test was run during this source check.

An additive coverage note on the existing pending Stack lifecycle proposal is supported across the two shipped class configurations. It is not a new per-config record, assigned ID, status or acceptance claim. The related placement producer proposal and the prior PlaceRel-only compatibility policy/M1 Ground guard retain their exact bounds. Existing narrow records012/026/031/042 and broader scoring/strategy/parent records retain their complete current scopes and named unbuilt recipients; none is proposed partially settled here. No official metadata, approval, production or test file changed. Only the manager releases implementation and later assesses built whole-record coverage.
