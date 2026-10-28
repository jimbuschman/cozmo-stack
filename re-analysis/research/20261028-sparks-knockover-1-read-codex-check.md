# SPARKS-KNOCKOVER-1 — independent source and boundary check

2026-10-28. Answers `re-analysis/jobs/trial-m13/SPARKS-KNOCKOVER-1.md`; follows KNOCKOVER-CUBES-1 K1–K27 and checked OV65–67 rather than re-extracting those class/children bodies. Actual model/effort telemetry unavailable. Read PROJECT_STATE first. No implementation/tests/git mutation, official record/inventory/approval/Claude/REVIEW edits or hardware actions.

## Current records read before conclusions

These are the full current title/status/evidence fields, copied directly from the manifest in the manager worktree before relying on their ownership. A quotation does not endorse stronger settlement or update stale wording.

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

## Finite configured delta and actual reach

Read actual shipped `behaviors/freeplay/sparkable/sparksKnockOverCubes.json`: behaviorID SparksKnockOverCubes, class KnockOverCubes, minimumStackHeight 2, needsActionID KnockDownCubes_Sparked, executableBehaviorType KnockOverCubes; same requiredUnlock KnockOverThreeCubeStack and five triggers as ordinary instance. Actual `activities/sparks/sparksKnockOverCubes.json` is Sparked with min0/max60/repetitions1/objective KnockedOverBlocks and softSparkTrigger SoftSparkUpgradeTracks. Its strict chooser includes DriveOffCharger, this instance, SparksCheckForStackAtInterval, SparksPutDownBlock and SparksLookInPlace. Sibling bodies are inherited frontiers, not invented.

Factory binding already constructs the same native class independently from each full config. Container AddToFactory keys the real configured ID; NativeBehaviorRuntime Bound and manager registration preserve distinct ordinary and spark instances. NativeStackConfiguration supplies bottom/top and absent-middle uint.MaxValue for a two-cube stack. NativeKnock uses configured MinimumHeight/needs/ID, shared real manager ActiveSparkId/Soft and configured unlock5 common-lock input. No own-class hardcoded ordinary-ID discrepancy was found. Existing tests exercise only Bound[KnockOverCubes], even when supplying spark fields; FreeplayTests verifies needs mapping only. Meaningful regression must use actual Bound[SparksKnockOverCubes], real two-cube cache, ordinary-min3 rejection, progression5, hard/soft/no-active entry, bottom/top event filtering with absent middle, separate instance/subscription retirement, and actual success needs28 versus ordinary27.

The native raw requested-spark handler does not select this owner today: it stores requested fields and reports the unbuilt picker/ActivitySparked. A supplied checked switch leaf and explicit manager selection must remain labeled supplied admission; raw ActivateSpark, Freeplay picker and executable-type finder are not silently covered. Source A confirms executableBehaviorType's native parser at 005BC1B2..005BC1EE (string check, FromString, store+6C); current NativeBaseInputs has no corresponding field, and chooser FindBehaviorByExecutableType remains an explicit M8-013 seam. This does not license speculative dispatch.

## Independent primary spotchecks

Ran known.py for 005B186C, 005A42C4, 005B17A2 and 005B1D18 before direct follow-up; disassembled actual `resources/lib/armeabi-v7a/libcozmoEngine.so` in Thumb mode with capstone/lief. The legacy arm_disasm default Downloads location was stale; no source claim rests on that failed file lookup. Scratch helper `.scratch/sparks-b-disasm.py` reads the actual binary. Source A's annotated dump is cross-check/navigation, not a substitute for these direct checks.

- Objective 005B181E..1832 compares incoming uint with configured +A0, increments +8C only on equality, and has no selected/running guard. Thus preselection/deselection filtering cannot be invented.
- Update 005B186C..18D0 reads actual manager CurrentBehaviorTriggeredAsReaction, advances states0/1 to2 when the current behavior is a reaction, calls Check for states2/3, may Complete then Reset then set5 when state3 and the current behavior is a reaction, and finally forwards delegate+C4 vt+20 (or returns0). Exact owner scheduling matters; merely incrementing a counter is incomplete.
- Check 005B18D4..1A16: once time is read, f32 (start+min)+negative epsilon comparison precedes nonzero repetitions and signed count>=target. For min0/reps1, matching objective13 can trigger ending. On state2 normal end: ResetLightsAndAnimations at1972, manager RequestCurrentBehaviorEndOnNextActionComplete at1978, state3 store197E, then final reaction lock199C. The replacement-request branch separately sets state4 and interrupted byte+B4; it cannot be folded into normal successful ending.
- Manager 005A42C4..4304 safely obtains the actual current behavior and calls StopOnNextActionComplete at42FE. Existing SteppedBehavior.StopOnNextActionComplete destroys the completion callback, preserves the queued action, and later returns2 after its handle clears. Immediate cancellation would change observable traffic and timing.
- Reset 005B17A2..1804 checks B5 first. If set: obtains name by vt+24; removes driving animations17DE; stops backpack looping locator+C8 at17F8; clearsB5 at17FE. There is no idle removal in this helper. OnDeselected1780..179E calls reset, optional delegate deselection, then CubeLightComponent::StopAllAnims. No-op reset when B5=0 is explicit.
- Desired behavior005B1D18..1F2A: state0 configures the actual retained animation behavior get-in, state1 waits on current running byte, state2 dispatches the internal selection; state3 retains running non-Wait current via internal dispatch, otherwise prepares get-out. Hard normal success uses count>=target, optional configured success trigger, SuccessfulSpark mood, then appended trigger0x212 and state4. State4 keeps actual running animation, then calls Complete and returns null. Soft/cancel uses Count trigger0x23F instead. This is an actual owned animation-behavior dependency, not a timer or emission-only end simulation.

## Open before source freeze

Verdict remains PENDING until A's complete packet and independent scheduling/config/constructor/animation-owner/Complete/API checks are finished. Current ActivityObjectiveOwner only handles the checked objective4 miss; objective13 reports MISSING, and the specialized activity lifecycle is expressly unbuilt. No builder release follows from these spotchecks. Verify actual configured owner construction/lifetime, scheduling through manager Update and real animation behavior, full Complete payload/order and cleanup; record finite missing joins explicitly. Compare approved inventory before treating any recovered discrepancy as correction authorization. No whole M15-059 or M13-014 settlement is proposed at this stage.

## Further independent checks while packet completes

Direct primary CompleteSparkLogic 005B1A90..1C2C and external helper005B2094..20D4 agree with checked OV67: success is repetitions==0 or signed count>=target. Replay-result diagnostics precede the conditional requested-state clear and hard ended reply. Cancellation latch or interrupted+B4 suppresses the normal clear/reply tail. Otherwise requested is cleared to (0x55,0) only if actual active==requested; a different pending request survives. Ended is emitted only for hard/no-active, carrying the computed one-byte success. Soft does not emit that hard-ended message. Native state4 desired-behavior completion calls Complete and returns null without itself storing state5; the reaction-gated Update completion branch separately performs Complete, Reset and state5. A new generic once flag or reordered pending-state write requires source justification, not convenience.

A identified the exact required animation owner as PlayArbitraryAnim ID0x3D retained in ReloadFromConfig+78 (005B1006..101C); constructor ID1 AcknowledgeObject is a different retained pointer+80. The current configured class is unbound in BehaviorFactory and the public hand-built PlayArbitraryAnimBehavior is an async host adapter. Independently ran known-first and directly checked005C07BE..08B8: IsRunnable requires signed loops+128>=0 before base dispatch; setters copy trigger(s), loop count and set configured flag+13C; Init/Stop preserve the derived flag/vector lifecycle. This is a necessary existing animation/sequence dependency, not permission to replace get-in/out with a synthetic trigger callback. A is recovering the finite inherited sequence recipient before source freeze. No wholepacket PASS yet.

### Additional current owning record read before sequence reliance

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

## Independent actual scheduler bridge

Reused approved M15-055 and M8-012 before checking the finite current-owner branch in primary code. Manager Update005A2F74/78 gets actual current activity and invokes its vt+20 at2F84 before reactions/selection/behavior Update. Freeplay Update005AD9D0 loads selected subactivity+80 and tail-dispatches its vt+20 at9DA; null returns0. Thus the actual selected Sparked owner receives its own Update, rather than a global counter scheduler.

ChooseNextScoredBehaviorAndSwitch005A2A20 gets actual current activity and current behavior, then IActivity::GetDesiredActiveBehavior via PLT4B0FA8 at2A4A. In the retained-current-subactivity branch of Freeplay desired,005AE656 loads actual+80;65A copies actual current behavior;66E supplies selected activity,670 Robot;672 invokes the same IActivity getter and returns the resulting behavior at680. IActivity005B387C first compares its cached+2C to caller; otherwise clears cached shared state, loads vt+30 and dispatches its actual internal getter at38C2. Sparked's internal getter is005B1D18. This reaches its get-in/state machine, internal chooser, normal ending, get-out and Complete. Manager's existing choice/switch/update then owns the actual behavior and queued ActionList children.

Current production does not implement that specialized configured owner/scheduling: NativeBehaviorRuntime.AdmitActivity permits only PutDownDispatch, while the specialized Sparked data model reports MISSING. A source-consistent finite implementation must attach the actual configured Sparked lifecycle as the manager's Update/GetDesired recipient with explicitly labeled supplied admission (or a finite exact parent forwarding recipient). Calling a detached counter/Complete helper is insufficient. Do not treat this finite scheduling recovery as raw ActivateSpark selection. When Sparked returns null, Freeplay's source tail proceeds to subsequent activity selection; that inherited picker remains unbuilt and cannot be replaced by an invented fallback/deselection decision. A test can supply the next/deselection input explicitly and verify this owner's cleanup, preserving that boundary.

### Corrected interpretation before freeze

The first checker draft misnamed Update005B1874's callee as a soft-spark predicate. Direct annotated PLT resolution identifies BehaviorManager::CurrentBehaviorTriggeredAsReaction. Primary r6 = return XOR1; state0/1 advances2 when that return is true, and state3's Update Complete/Reset/state5 path requires a current reaction. Soft/hard gates inside desired/getout and Complete are separate manager fields. The draft wording above is corrected; manager and source A were informed before builder release. This was the checker's interpretation error, not a contradiction of the existing approved packet. No implementation should use soft mode to drive that Update branch.

Correction preserved by 2026-10-28 10:58:59 -05:00 (manager and A notified before this timestamp). Independently resolved dynamic symbol `_ZNK4Anki5Cozmo15BehaviorManager34CurrentBehaviorTriggeredAsReactionEv` to Thumb address005A19DF/body005A19DE, 14 bytes; known.py identifies PLT004B1C5C and body005A19DE. Direct body loads manager+1C, then running-info+10, compares with0x16 NoneTrigger and returns1 iff not equal. Thus Update's reaction predicate comes from canonical running-info, not active spark soft+5C. Existing manager exposes the same running trigger via CurrentReactionTrigger; tests can supply a genuine reaction through existing manager reaction flow, not invent a soft substitute. Checked OV67 row112 already explicitly states this reaction cadence; approved source was not at fault.

## Accepted helper ownership read before per-locator conclusions

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
  "id": "M5-063",
  "title": "DrivingAnimationHandler bodies: HandleActionCompleted (E2G 0x5A), PushDrivingAnimations (G2E 0xB5), RemoveDrivingAnimations (G2E 0xB6)",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m5-teardown-members.md: callbacks 0x004F110E -> HandleActionCompleted 0x004F0D78 (engine sender: ActionQueue 0x0053FAAA); 0x004F1186 -> PushDrivingAnimations 0x004F0944; 0x004F120E -> RemoveDrivingAnimations 0x004F0A94; registered only if HasExternalInterface() == 1 (0x004F072C..0x004F0732).",
    "re-analysis/research/20261012-m5-driving-handler-read.md: handler state ctor 0x004F06B0..0x004F0878 / Init 0x004F0FC4..0x004F0FF4; Push gate warning 0x004F094C..0x004F0964 and push_back 0x004F0988..0x004F09FA; Remove gate 0x004F0A9E..0x004F0AB4, empty-stack warning 0x004F0ADA..0x004F0C22 (0xBE4672), first-match erase 0x004F0AE4..0x004F0C6C, LoopBoundOverflow drivingAnimationHandler.cpp:108 0x004F0B5A..0x004F0BB6; UpdateCurrDrivingAnimations only from Init (0x004F0D2C..0x004F0D74: back(), else Sad 0x2C, else needs-met 0x38, else default 0x20); HandleActionCompleted 0x004F0D78..0x004F0E2A with PlayEndAnim 0x004F0EFA..0x004F0F4A, PlayDrivingLoopAnim 0x004F0E58..0x004F0EA0, PlayDrivingEndAnim 0x004F106C..0x004F10C0, lock/unlock helpers 0x004F0EB2..0x004F0EE2 / 0x004F0F4C; ActionIsBeingDestroyed 0x004F0F94..0x004F0FC0."
  ]
}
```

Direct known-first primary63228C..6333E confirms StartInternal first stops the supplied locator6322A4, obtains the source bucket, copies exactly0x8C bytes6322D0, inserts the new front6322DC, then captures source/list/node weak state into that supplied locator632320..32. Stop6324A0 validates the supplied weak control/strong count; removes that locator's list node6324BC and erases an empty source bucket6324CA; invalid locator logs and returns0. Thus an activity-owned locator can coexist with charging/host shared locator. Current BodyLightComponent.StartLooping always uses its one private shared locator; adding a finite per-owner capability must preserve existing arbitration/wire conversion and leave other source nodes intact. Direct packet callsite passes source0 and its own+C8; do not send direct light frames or alias the charging locator.

Direct known-first sequence005C0158/0294 primary matches P4/P5: exactly one trigger uses one TriggerLiftSafe with supplied loop count (including zero), interrupt1/tracks0/timeout60/strict0; otherwise loop index resets0, signed index<loops, owned sequential compound contains every trigger in order, each loop1 and AddAction(false,false), index increments before StartActing. Existing PlayAnim async wrapper's callback/listener behavior is not automatically reusable; new native arbitrary child must use these exact recipients. Derived Init/Resume/Stop primary and base Update84!=null?1:2 also checked.

### Packet corrections caught before release

A's preliminary S23 called literal0x39 PlayAnim. Independent factory/source check establishes0x39 Wait (PlayAnim0x26, arbitrary0x29); A acknowledged and corrected. Get-out must retain a running non-Wait current behavior through internal selection; an ordinary PlayAnim is not the exception. This is an extraction-packet interpretation defect, not a contradiction of approved OV67, which already names Wait. No implementation was released against it.

S15 initially labeled native observed payload+4 ObjectType. Existing checked immutable local44 prefix is NativeObservedObject(Timestamp,Family,Type,ObjectId); BlockWorld's source-bound producer and VisionSystem's local dispatch preserve that prefix, and ObjectFamily1/2 are Block/LightCube. A was asked to primary/schema-confirm this before freeze; filtering Type would be wrong. Parent was informed while packet remained pending. Current complete app44 packing remains M11-038, visibly MISSING upstream; use actual local prefix or clearly labeled supplied native input, not facade ObjectObserved reconstruction.

### Raw backpack color correction raised before freeze

Independent direct `.so` bytes at NamedColors.BLACK C9744F are `00 00 00 FF`; native little-endian load yields0xFF000000, and initializer REV at005B1586 yields0x000000FF. White C9744B is allFF and remains0xFFFFFFFF. Therefore the initial source packet's 35-word appendix BLACK0xFF000000-after-REV is incorrect if interpreted as stored native words. Native005B1588 stores r1 (black) then r0 (white). Parent and A were informed before any builder release; require raw appendix correction and consistent LedColor interpretation. This is an extraction literal/endian defect, not permission to change accepted wire-color conversion or an approved departure. A must confirm exact GOT pointers and normalize byte/word terminology; a silent wrong red/alpha replacement is not acceptable.

## Final backpack initializer and semantic mapping, independently checked

Re-read the full initializer005B156A..15C8 and actual native SetBackpackLightsInternal631F3A..63215A, then compared settled M4 LB2/LB4f/LB4i and current LedPattern/Wire. All 35 corrected native words below are hexadecimal, in increasing address order:

```
000000FF FFFFFFFF FFFFFFFF FFFFFFFF 000000FF
000000FF 000000FF 000000FF 000000FF 000000FF
00000000 00000168 00000168 00000168 00000000
00000000 00000456 00000456 00000456 00000000
00000000 00000000 00000000 00000000 00000000
00000000 00000000 00000000 00000000 00000000
00000000 00000000 00000078 000000F0 00000000
```

Initializer sources: stores1588/158C write words0–3;1594/1598/15A6 words4–9;15AC/15B0/15B4 words10–15;159C/15A0 words16–18;memclr15B8 clears0x34 bytes from offset4C (words19–31);15C4/15C8 words32–34. NamedColors.BLACK bytes and REV proof above settle the corrected black word.

Consumer indexing uses a structure of arrays, each five words: OnColor+0, OffColor+14, OnMs+28, OffMs+3C, TransitionOnMs+50, TransitionOffMs+64, OffsetMs+78. Direct consumer loads631F50,631F96,631FC0/632012,63203E/2064,632084/20A6,6320C4/20E2,632100/2122 establish these fields. Color packing tests the low alpha byte and shifts high RGB bits; stored words are RRGGBBAA. The exact semantic LedPattern arrays, times below decimal:

| Field | LED0 | LED1 | LED2 | LED3 | LED4 |
|---|---:|---:|---:|---:|---:|
| On RGBA word | 000000FF | FFFFFFFF | FFFFFFFF | FFFFFFFF | 000000FF |
| Off RGBA word | 000000FF | 000000FF | 000000FF | 000000FF | 000000FF |
| OnMs | 0 | 360 | 360 | 360 | 0 |
| OffMs | 0 | 1110 | 1110 | 1110 | 0 |
| TransitionOnMs | 0 | 0 | 0 | 0 | 0 |
| TransitionOffMs | 0 | 0 | 0 | 0 | 0 |
| OffsetMs | 0 | 0 | 120 | 240 | 0 |

Therefore black maps to LedColor(0,0,0,255), packed0x8000 (same as current OffLights); white maps to LedColor(255,255,255,255), packed0xFFFF. Existing M4 Wire yields middle LED1/2/3 on/off frame counts12/37, transitions0, offsets0/4/8; outer LED0/4 on/off0x8000 with all frame counts and offsets0. The existing accepted conversion sends middle0x03 then outer0x11; no color white balance. This derived wire expectation is source-based, not inferred from current runtime output. Do not pass raw little-endian memory bytes straight into LedColor channels or reverse colors again. No settled M4 behavior change is necessary; only finite per-locator access and exact activity pattern are needed.

## Exact timer and helper prefreeze checks

Timer values stay f32 at each native operation. Min threshold:005B18FA VADD.F32(start,min), then1902 VADD.F32(the rounded sum, constant loaded18FE from005B1A44). That constant's exact bits are0xB727C5AC, approximately -9.999999747378752e-06. VCMPE1906/BLS190E takes ordered threshold<=now. Wait-true timeout separately computes rounded start+max1930 then adds the same constant1934; VCMPE1938/BLS1940 is ordered<=. Wait-false hardmax computes rounded start+max2028, loads the same bits at202C from005B2090, adds2030 and VCMPE2034/BHI203C skips greater or unordered. If not skipped and actual current pointer differs from chosen, retained Wait is returned and state3 stored. Preserve separate f32 additions, exact negative constant and comparison; do not express a double deadline or accidentally subtract a negative epsilon. Parent requested the exact wording/citations; A informed before final freeze.

Direct helper checks match H4/H5: activity reaction disable calls manager with suffixed activity-lock name and force1 before unique own tracking; repeat helper invocation is not suppressed by the set. SmartPush idle005B32BC guards50, uses activity prefix/name, invokes streamer then sets50; SmartRemove37C0 guards50, removes exact prefix/name then clears50. Base deselect34B4 repeatedly removes activity locks, then34EA removes smart idle if owned, then34EE clears interlude; pending needs reward check3502 leads existing recipient3548;3654/365C dispatches specialized deselection last. Reset's absence of idle removal therefore agrees with later base cleanup. Source-specific prefixes and own flags must remain distinct from behavior locks and driving-name entries.

Direct native union constructors727DFE and727E88 set tags83/84 and copy respectively u32 unlock / u8 success. These agree with prior checked OV67 shapes and own wrappers. Actual report-channel ordering must use existing local delivery plus queued API path. A's native serializers/adapter resolution provides the finite cross-check; no new ad-hoc app event is necessary.

M8-019 immediate-end remains an explicit inherited callee implementation gap. S20's own request/state4/interrupted byte are sourced; reporting that callee MISSING cannot become a generic Stop substitution or a whole-059 success claim for that branch. Other own parameter branches not implemented remain exact unsettled obligations. No counter-only implementation qualifies as this lifecycle.

Final remaining query before READ verdict: common selection publishes the parsed ActivityID byte at+34, but the first packet did not state SparksKnockOverCubes's actual numeric native table entry. A was asked to supply that finite mapping/parser evidence or name its existing owning frontier, so builder cannot guess a host ordinal. All prefreeze errors and this question were sent to manager before builder release.

## Final current-record refresh and additional inherited callees

Official Q8/M13-014 hard-spark evidence correction imported by manager merge2c319e8f before source freeze. Re-read current manifest directly and refreshed the full M13-014 quotation above. Native code unchanged. Earlier K correction history is historical; current manifest now says hard-switch D8. Checker authored no official record change.

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

Final activity public-state input confirmed directly: EnumToString766C50 indexes table1033070; slot21 at10330C4 containsC199E8, whose actual bytes spell SparksKnockOverCubes. FromString initializer766F0A sets0x15 and766F14 stores the paired value atsp15C; ReadConfig005B2A80 calls ExtractActivityIDFromConfig and005B2A84 stores byte+34. This closes the final mapping question with ActivityID21. Preserve that actual byte through existing PublicStateBroadcaster, not a guessed host ordinal.

## Final frozen whole-packet READ verdict

**PASS for bounded source recovery**, after independent review of final S1–S28, P1–P6 and H1–H12 and the corrected literal/field/timer appendix. Final extractor packet SHA256:

`1E0C4E7B51DAADE69A890CD3782B9FB48069C86EE8EFF7283FDBC5202ED128D3`

Verified that hash directly from the final file after official current014 refresh. This supersedes the earlier PENDING checkpoints. Four A packet defects (Wait class, observed Family field, BLACK REV, StopAllAnims layer scope) and this checker's preliminary reaction/soft misinterpretation were corrected and retained in the record before any implementation release. Exact negative-epsilon operations/literal addresses and ActivityID21 now close the final two questions. No new approved-inventory contradiction remains. The operator/Claude's formal Q8 change was imported, not authored by these researchers.

The complete finite observable chain has source support: shipped two-cube config and distinct factory/manager instance; canonical configured progression/active hard or soft inputs; actual own native children from K1–K27; actual objective13 local producer into the constructed activity's lifetime subscription; matching counter and needs28; actual manager/activity Update cadence; exact f32 end predicates; next-action callback retirement without cancelling that action; actual manager end/update/choice boundary; configured native PlayArbitraryAnim get-in/get-out and real ActionList animation children; result/mood/requested-state/report ordering; Started83(u32 unlock) and Ended84(u8 success) through existing external/API channel; common and specialized cleanup with independent source0 light locator and constructor-lifetime subscription retirement. This is an implementation inventory; current production still has the documented missing owner/joins/child binding.

Builder evidence boundaries remain mandatory:

- Supply and label admission/canonical request-active switch inputs. Do not claim raw ActivateSpark or the Freeplay picker, post-null next activity choice, executable-type dispatch, or sibling bodies are built.
- Bind the actual configured activity as the manager's scheduled Update/GetDesired recipient. The actual local objective producer must reach that owner, not a detached counter or manual Complete callback.
- Preserve native state4 Complete/null versus reaction-gated state5 distinction. When missing parent next-picker is reached, visibly stop at that boundary or accept an explicitly supplied next/deselection input; no fabricated fallback, own completed-once flag, or repeated Complete/API emission.
- S20 requests the separately owned M8-019 immediate-end callee, which currently remains visibly MISSING. Preserve that own call/state flow and exact frontier; no generic Stop substitution or whole lifecycle claim for an unsupported branch. Other unbuilt parameter/delegate branches remain exact unsettled obligations, not exclusions.
- Reuse existing owned animation/action/planner/face/audio and full upstream44 serialization frontiers honestly. A real local immutable prefix is available; do not fabricate full44 wire packets or filter ObjectType in place of Family.

Meaningful source-derived regression must reach the actual Sparks ID with a two-cube cache and absent middle; regular min3 rejection, progression gating and hard/soft/no-active admission; exact member-axis and Family1/2 input filtering, constructor-before/after-selection objective handling and selection reset; objective13 and needs28, real queued children and next-action retirement, actual activity tick/get-out/Complete/local/raw API payload/order; nonmatching objective, unsigned wrap/signed comparison, exact f32 min/max and Wait-class boundary; source0 backpack pattern/arbitration coexistence with charging and another locator, reset/deselect idle/reaction/cube cleanup; actual animation child single/multi loop/cancel paths and owner/subscription retirement. Supplied world/firmware/admission inputs must be labeled and expected values sourced above. Existing ordinary KnockOver regression must remain unchanged and pass. Tests have not been run by this reader.

Whole M15-059, M8-005, M13-014 and other existing records are not settled by this READ verdict. Later frozen implementation verification must assess the actual delivered scope and exact remaining branches. No hardware result, general stack fidelity, gate success or acceptance is asserted. The manager alone decides builder release. No code/tests, git mutation, official record/inventory/approval/Claude/REVIEW edits or hardware actions by this checker; writes limited to this authorized report and scratch read aids.

## Final H12 stop-helper scope check and re-freeze

The manager identified a final helper-composition question after the prior4F07 source freeze. The preliminary READPASS was explicitly withdrawn before manager/builder release, then the finite helper was checked; no tests or code changes occurred in between.

Current complete owning record quotation read before reliance:

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

Independent known-first primary CubeLightComponent::StopAllAnims00638D58..8D7A is exactly36 bytes: mov layer1 at8D5E/store8D60, ObjectID-1 at8D62/store8D68, construct actual ObjectID reference, and ONE StopAllAnimsOnLayer call8D74. This means all objects on ENGINE LAYER1 only; user layer0 and state layer2 survive. It does not stop all layers. Current Lights.cs StopAllAnimsOnLayer(EngineLayer,null) logs allobjects, marks every entry in that layer and invokes one Update(pickDefaultLayer:true), matching accepted W2. No three-layer composition or new directwire helper is source-supported.

A corrected the earlier overly broad cube-stop prose and appended H12. Final source hash independently matches `1E0C4E7B51DAADE69A890CD3782B9FB48069C86EE8EFF7283FDBC5202ED128D3`; the final frozen verdict/hash above now refer to that re-frozen packet. **Final bounded READ verdict: PASS**, including H12 and all four corrected source-packet defects. Add a meaningful cleanup regression preserving user/state layers while retiring engine-layer entries with one update, together with the listed whole pipeline tests. No remaining own shipped-path source question was found; all named inherited gaps and no-whole-record-settlement limits above remain. Manager alone releases implementation.
