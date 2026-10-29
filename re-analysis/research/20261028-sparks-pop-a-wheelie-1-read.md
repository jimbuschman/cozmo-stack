# SPARKS-POP-A-WHEELIE-1 — exact configuration and actual Sparked owner

2026-10-28. Answers `jobs/trial-m13/SPARKS-POP-A-WHEELIE-1.md`. Reader startup request explicitly gpt-6.1-sol/medium/fork_none; backend telemetry unavailable; no switch. PROJECT_STATE read FIRST (initial repository copy, then manager worktree copy), AGENTS, job, AUTONOMY, PLAN-BEHAVIOURS, BEHAVIOURS, research README and extractor instructions read. Only this research packet and ignored scratch written. No code/tests/.NET/Git/hardware/official metadata mutation.

Primary ELF `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 **02263C07F6BB60F4D7F351A3667DCA84FD3E6C0FC6F838E18B5DE7CF4E2989E1**. Manager worktree has actual readable assets/ELF. Python313 explicit executable used. `known.py` ran before ActivitySparked, BehaviorPopAWheelie, SparksPopAWheelie, 005BC208 EnumToString and ReadFromScoredJson native reads; configured ID has no separate native symbol. Existing `.scratch/pop-dump.py` decodes ELF instructions and resolves dynamic/PLT names; fresh reads are `.scratch/sparks-pop-native1.txt` through `native4.txt`, data scripts `sparks-pop-data.py`/`data2.py`. Initial attempt to read the dumper from outer root failed (file absent), then manager-local existing dumper used. Initial linear decode starting005BBCC0 was mid-instruction; corrected fresh decode from005BBC38 in native3 is controlling. No decompiler claim used.

Frozen ordinary packet **BBB5628D6B7F7367CABB3BE3EAB36D0AD6C1B806880E4F36B49F7A01CD0E65AF** (`20261028-pop-a-wheelie-1-read.md`) and followup **AAF2EB81** (`-followup-1.md`) are reused only for identical concrete native class/arguments. Their independent checks and implementation review preserve the partial manual/API boundary. Sparked common receiver reuse is checked SparksKnock read S6–S28/P/H plus `-source-followup.md` SF1–SF4, with exact per-caller replacements below. SparksRoll/Stack are navigation/cross-checks, not evidence for copied values. No whole-parent or sibling extraction.

## Current literal ownership snapshots, before conclusions

Manifest read explicitly UTF-8 programmatically: **717 records**. These are exact current title/status/evidence excerpts, not proposed mutations or classifications of the whole paths. The legacy/manual M13-015 limitation remains applicable. M12-052 is included to prevent any duplicate Ground ownership inference; the direct Pop child does not instantiate Ground.

```json
{
  "id": "M13-015",
  "title": "Pop a wheelie: one retry, the realign or retry animation, and what a failure marks",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "TransitionToPerformingAction(Robot&, bool) 0x005C7758 bumps +0x12C on a retry (0x005C777E) and zeroes it otherwise (0x005C780C); the completion lambda 0x005C7CBC splits on result >> 24: success sets +0x128=-1, plays SuccessfulWheelie 0x21C and reports objective 0x16 and the needs action; a category-3 result marks AIWhiteboard::SetFailedToUse(obj, 3) (0x005C7CF0 cmp r1,#3 / beq 0x005C7D4C -> 0x005C7DAA), and a category-4 (Retry) result calls SetupRetryAction while the count is at most 0 (0x005C7D44) and otherwise falls into the same SetFailedToUse(obj,3) (0x005C7D4C); any other category logs BehaviorPopAWheelie.FailedPopAction (0x005C7DB0)",
    "SetupRetryAction 0x005C79D0 plays PopAWheelieRealign 0x18D for exactly 0x04000001 (0x005C79F8/0x005C79FE) and PopAWheelieRetry 0x18E otherwise (0x005C7A10/0x005C7A32); ResetBehavior 0x005C76B0 sends EnableStopOnCliff(true) (0x005C76E4)"
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
  "id": "M15-071",
  "title": "SparksRewardCommunicatedToUser: the FreeplaySparksAwarded game broadcast and its amount",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "SparksRewardCommunicatedToUser 0x00696E64 clears the pending flag (+0x3D8, 0x00696E78) and builds FreeplaySparksAwarded from the amount at +0x3DC (0x00696E82..0x00696E88), then calls the E2G Broadcast(&&) through the external interface vtable +0x1C (0x00696E90). It has no pending-flag test (research/20261018-b-m1-7-check-delivery.md finding 1).",
    "Callers named in Needs.cs: IActivity::OnDeselected 0x005B3548, BehaviorEarnedSparks::StopInternal 0x005DAF7C, and RegisterNeedsActionCompleted; whether they guard the call on the pending flag is unread.",
    "The stack's public SparksRewardAwarded (Needs.cs:828) is queued with no payload and returns early when !SparksRewardPending (Needs.cs:841)."
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
  "id": "M12-017",
  "title": "IDockAction::Init setup, handler registration and the docking squint",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005514FC Init; 0x00551598 GetLocatedObjectByIdHelper; 0x00551640 GetPreActionPoses; 0x005516EA movs r3,#0xc5; 0x00551750 movs r3,#0xda",
    "the handler bodies are the action's own state machine",
    "the AddSquint call 0x00552394 is inside IDockAction::CheckIfDone 0x005521AC (body 0x005521AC..0x005523F6), not Init 0x005514FC (body 0x005514FC..0x00551C8A, no call to PLT 0x4ABA04)",
    "AddSquint args: TrackLayerComponent at robot+0xC0 (0x00552362 ldr.w r5,[r0,#0xc0]); name \"DockSquint\" (literal at 0x005524A0, 10 bytes); f1=0x3F866666=1.05 (0x0055237A/0x00552388); f2=0x3EB33333=0.35 (0x0055237E/0x0055238C); f3=0xC1200000=-10.0 (0x00552378/82/86); return at 0x00552398 strb [r4,#0xf4]",
    "TrackLayerComponent::AddSquint 0x0064F370 -> FaceLayerManager::GenerateSquint 0x0058D738 (PLT 0x4BA164) then AddPersistentLayer 0x4AE9E0; GenerateSquint ignores its float args and clips EyeScaleY=0.35, EyeScaleX=1.05, UpperLidAngle=-10.0 on both eyes",
    "GenerateSquint track: two ProceduralFaceKeyFrames, the second at 250 ms (0xFA) with Reset (0x0058D820/0x0058D82C)",
    "0x00551540..0x00551C14 the Init order; 0x00551C0C ldr r1,[r0,#0x30] / 0x00551C10 blx r1 / 0x00551C14 cbnz r4 (InitInternal is the last step; PlaceRelObjectAction vtable 0x010222F0 slot +0x30)",
    "0x00555435..0x005555B5 SelectDockAction",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q6; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q6",
    "0x00551960..0x00551B8C marker choice; 0x00551C0C..0x00551C4C the tail; 0x00529CF4..0x0052AAC6 the wire stores",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q10.1-10.2; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md (10.1, 10.6)"
  ]
}
```

```json
{
  "id": "M12-037",
  "title": "SetupTurnAndVerifyAction and GetObservedMarkers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00876C50..0x00876C96, 0x00551F9C..0x00552164",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q10; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q10 (PASS)"
  ]
}
```

```json
{
  "id": "M12-052",
  "title": "PlaceObjectOnGroundAction::GetCompletionUnion: the saved carried object as the first ObjectInteraction ID, count 1, flag false",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "GetCompletionUnion 0x00554AE0..0x00554AFA: first ID word = this+0x7C, halfword 0x0001 at +0x14 (count 1, flag false), Set_objectInteractionCompleted 0x00554AF4; the other four ID words are never written.",
    "Saved carried object: ctor 0x00554638..0x0055469C stores +0x7C (0x0055467C); Init 0x00554794. Setter 0x0075C3F0..0x0075C436 copies the 22-byte payload.",
    "re-analysis/research/20261028-sparks-roll-block-1-claude-check.md; SPARKS-ROLL-BLOCK-1 handoff proposal"
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

## Exact inputs and native joins

All paths below are source-read contracts for this job, not official EXACT_SOURCE status changes. Asset locations are relative to `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/`.

|Step|Original behavior|Primary evidence|Existing owner / reading boundary|
|---|---|---|---|
|S1 leaf identity|`behaviors/freeplay/sparkable/sparksPopAWheelie.json` class PopAWheelie, ID SparksPopAWheelie, needs PopAWheelie_Sparked, display behavior.PopAWheelie, requiredUnlockId PopAWheelieAction, confirmed/possible handling false. No executableBehaviorType, no alwaysStreamline, no considerThisHasRun. The last difference from ordinary Pop preserves base objective default41 rather than ordinary's configured considered-run objective22; success still explicitly reports22.|Asset lines2–8; checked parsers005BB9EE..BA02,005BBA70..BAC8,005BBAE8..BB46; ReadFromJson005BBFEC..C118/005BC1B2..C21E; base005BBD26 movs29,005BBD2C stores+10C; absence of ordinary considerThisHasRunForBehaviorObjective asset field; native005BC56A..57A missingkey→005BC5F8 skips005BC5A4 store10C.|M7-018/M8-001/M8-003; configuration reading only.|
|S2 actual enum data|BehaviorID152/0x98; ActivityID24/0x18; NeedsAction34/0x22; unlock13/0x0D; behaviorObjective PoppedWheelie22/0x16; soft trigger518/0x206. Never borrow Roll's IDs or max80.|Behavior EnumToString0076B32C EOR80, table01033330 cell01033390→00C19A1D; Activity accessor00766C50..62 table01033070 cell010330D0→same string. Needs accessor00782DCC..DC table01033D40 cell01033DC8→00C1C76B. Unlock cell010340F4→00C1D965. Objective cell01033198→00C19CC0. Trigger cell01032EA8→00C19185.|M7-018/M15-044 and current common enum/parser paths; source-read.|
|S3 defaults/factory|Same Pop native allocation138 and constructor as ordinary. Base executable stays12/Count (not Pop executable inferred from name); D9 false. Constructor target120=-1, previous128=-1,retries12C0,cliffLatch130false,ownRobot134. No Pop derived config override.|Factory0059CEA6 movw138/0059CEB6 call;005C7430..746E. Base005BBCA2 movs12/005BBCA4 store6C; table01033760→00C1966A Count; absent executable string skips write at005BC1C4→C200. Base005BBD02..06 clears C4..E7 includingD9; optional D9 writer005BC216..21E.|M7-018/M8-001 plus unowned configured Pop complement below.|
|S4 exact caller|`activities/sparks/sparksPopAWheelie.json` type Sparked/ID SparksPopAWheelie/requireSpark PopAWheelieAction, strategy Spark; StrictPriority **[DriveOffCharger,KnockOverCubes,SparksPutDownBlock,SparksPopAWheelie,SparksLookInPlace]**. min0,max60,reps1,objective PoppedWheelie22,softSoftSparkUpgradeTracks206. No delegate, timeout/success/failure override, or base driving/idle/process input.|Exact asset lines2–29. Generic native ctor005B0D08..D72,Reload005B0EAC..11A6; min/max fields90/94 writes005B104C/107E;reps9C005B10B0;objectiveA0005B1132;null delegate005B1150..1186.|M15-059/M8-013; actual configured input.|
|S5 actual caller defaults|waitForBehaviorToEnd98=true; success216/failure210; arbitraryAnim3D/Acknowledge1/Peek94/WaitB2 resolved from real behavior manager. No other branch's animations copied. No delegate means current chooser is actual StrictPriority, not a nested activity.|005B0D3C..46 defaults;005B0EF0/005B0F24 triggers;005B1000..28 arbitrary child;005B0D76..DCC other children. Generic factory005AA4C2..CE is the matching Sparked receiver from checked packets.|M15-059/M7-018; source-read.|
|S6 admission/scheduling|Actual selected Freeplay activity Update precedes behavior tick; current runnable/first runnable StrictPriority child wins in exact vector order. Earlier DriveOff/Knock/PutDown may win; no test may force Pop by removing them. Actual progression unlock13, severe need, timers/process/treads/charger/carry and strategy are common supplied admission inputs.|005A2F74..84/005AD9D0..DA/005A2A20..4A/005AE656..72;StrictPriority0060B23E..266;IBehavior005BD750..D968. Same receiver as checked Sparked path.|M15-006/M8-001/M8-013; missing input producers retain their existing boundaries.|
|S7 hard/soft fields|OnSelected hard-effects decision uses **requested60/soft64**, hard if requested==55 or requestedsoft=false. Pop Init's D8 uses **active58/soft5C**, hard iff active!=55 and activeSoft=false; D9 remains independent configfalse. RequiredUnlock13 matching actual active13 adds common SparkBehaviorDisables lock. No name-based Sparked=>hard assumption.|005B15D0..15E2;005BCCAA..CCC2;005BCD16..CD44. Real get-in chooses active soft, not requested soft (005B1D3C..48).|M15-058/M15-059/M8-001; source-read.|

## Actual Pop child — complete same-receiver reuse

Frozen ordinary **P3–P13/D1–D5 plus F1–F5** remain controlling with S1/S2 needs/config replacements. Ordinary PlayAlone scoring admission P2 does **not** apply; its exact Sparked owner is below. The ordinary considered-run config P1 does not apply. These joins do not import Roll's helper/recovery/Ground or Flip's custom pose picker. The actual Pop direct path has no PlaceBlockHelper/Ground child; SparksPutDown is an earlier chooser candidate with its independently owned path, not Pop recovery.

|Step|Original actual Pop contract|Primary citation / frozen row|Boundary|
|---|---|---|---|
|P1 runnable/cache|Derived runnable refreshes target via virtual90; actual AI+14 cache intention8, target!=-1. Cannot run carrying. Cache8 family2/1, CanUseForPopAWheelie, DefaultBest; failurekind3 at current pose30s/60mm/PI then CanPickUpGround. No upright-only or AxisCheck. Cache cadence/default-best retain valid priorbest and real world/clock inputs.|005C7478..94/005C7734..54;0056FA30..FA9A/FC7E..8A/FE00..2C;00570928..980;frozenP4–P6, same receiver/arguments. Geometry0063C654..8E4: flat10deg,actual MAIN-relative,carried exclusion,top15mm; height(.5,10,0).|Common cache/geometry unchanged; proposed Pop complement only concrete binding.|
|P2 get-in|D9 OR activehardD8 OR target==previous128 skips initial reaction; otherwise sequential TurnTowardsObject(target,PI,false,false) then LiftSafe18A(1,true,0,60,false). Callback caches previous only result0, performs regardless result. Init returns0. No eager cliff/own lock.|005C7496..74BC;005C751E..757A;callback005C7B52..64;frozenP7.|Unowned Pop configured lifecycle complement.|
|P3 perform/drive|Target-1 logs/returns; false entry zeros retry12C, retrytrue increments. maxFaceRadians0 whenD9/D8 elsePI/2. Construct DriveToPop(target,false,0,false,Radians(max),true), set Say18B/NoName18C/pre-dock callback, typed StartActing, then score.8. Actual IDrive preaction4, distance0; creates Pop(target,false), AddDock(pop,false), leaf-proxy tag retained.|005C7758..78D4;0055D1E8..D26C;frozenP8/D1, same exact arguments. Score literal3F4CCCCD.|M13-015 perform/retry narrow owner; generic IDrive existing M13-014, unowned Pop concrete composite complement.|
|P4 actual optional face|Native positiveRadians max constructs real LastFace and TurnObject children. Finaltrue forwards through wrapper/base to **face+191 sayName=true**. Constructor+193=false separately; retain checked laterwriters, not a permanentfalse assumption. Concrete18B/C setter targets live weak face child. Hard/streamline max0 creates neither optional child. Actual face/speech/recognition/calibration remains input boundary; no fabricatedface.|005C783E..54/0055D214..24/0055B384..3CC;0054B8F0..916/0054B754..7B6;005C7858..68/0055B8CC..8EC;frozenF1–F3 controlling.|Existing generic IDrive/LastFace/speech owners, no widening or settlement.|
|P5 real drive/wait/dock prefix|Actual innerSeq[Drive,Wait1], outerAdd(inner,true,false). Wait1 disables B9 only when innerdrive succeeded; Wait2 invokes pre-dock callback then dock, including source ignored innerdrive failure path. Concrete IDock95=false **independently C0=true,C1=false,C8=true**. Existing setup verifies empty space above target ignoringtarget (child56true/Addfalsefalse/keep child), then marker-turn maxRadians0,true,false,child56true; never copy Roll-only zeroangle no-effect claim over this Pop receiver.|0055DE34..DEEA/0055E068..E0E6/0055B820..868;0055256C..25C6;00550374..3BA;00551FDC..215A;frozenD2/F4/F5 and checked generic children.|M13-014/M12-017/M12-037 generic; proposed concrete Pop complement.|
|P6 pre-dock own output|Exact **configured behavior name SparksPopAWheelie** reaction lock21pair table00C6883D true indices0,1,3,11,12,14,15,20; then latch130=true; then EnableStopOnCliff(false), reliabletrue/secondaryfalse. No blanketdisable and no early send.|005C7BE0 loads capturedbehavior;005C7BE2 add r1,r5,#40;005C7BE8 SmartDisable;005C7BC8..7C10; base005BBC38..5E name from actualBehaviorID;frozenP9.|Unowned Pop lifecycle complement plus common lock/send recipients.|
|P7 select/wire|Pop type1D/preaction4; target relative MAIN.GetParent; failed transform03000004. Dockaction9. Reject only **ordered heightdelta>33.85** (42076666); unordered falls through to carrying check; raw carrying!=-1→03000017. Generic IDock Init/Check/abort/cleanup,marker/squint/locks/C5/DA/RobotState handling precedes DockWithObject9 using real profiles/markers and firmware callbacks.|00552768..0055287E;vtable01022178;generic005514FC/005521AC;frozenD3. No custom Roll verify subtree or arbitrary camera success.|Proposed concrete Select/wire join; generic IDock records retained.|
|P8 verify|dockaction!=9→0300001A;actual DockingComponent+5false→04000003. Else actual pitch NativeRadians<1.0→0400000A; otherwise0. Native operator< swaps to operator>; positive delta must fail IsNear epsilon3727C5AC first. Exactly1 and just-below within epsilon succeed; NaNfalse succeeds; ordinary negative pitch fails. No abs/rawfloat or visual subtree substitute.|00552962..299E/005529EC/00552A2C..72;0084CD12→0084CC90/CCA0..CCD6;frozenD4.|Concrete Pop verification complement; actual firmware flag/pitch remain independent input.|
|P9 completion/UB|Final ObjectInteraction overwrite defines target7C,count1,flagfalse; FOUR unused IDs uninitialized. Same standingM1-034 at0055268E..269C: unavailable-full-payload carrier preserves full scalar/type1D/actualtag/definedlocalfields; skip only undefined raw/body-dependent public publication, keep prior published payload, visible owner/job diagnostic. Typed LOCAL, watcher/proxy and scalar behaviorcallback continue. No PlaceRel zero-unused-word normalization extension.|005525F4..2684 then unconditional0055268E..269C;setter0075C460..C4A6/Pack0075B4F0..B536;frozenD5 + accepted ordinary implementation/check guard.|Same proposed generic Pop completion owner; no new policy or owner per Spark instance.|
|P10 retry/failure|Nonzero scalar first removes **the same configured SparksPopAWheelie name** lock, not ordinary literal PopAWheelie. Category4 and signedretry<=0→refresh target via virtual90, LiftSafe18D only exact04000001 else18E, then callback performs(true) regardless result. Othercat4 orcat3 locates currenttarget family-1 and WB failure3; other categories diagnostic. No immediateStop/Finish, no re-running initialTurn.|005C7CD0 capturedbehavior/005C7CD4 add r1,r4,#40/005C7CDA SmartRemove;005C7CCE..7DFE/005C79D0..7A5E;callback005C7F68..6E;frozenP10/P11.|Existing M13-015; this configuration does not repair legacy/manual entry.|
|P11 success/objective/needs|Category0 (not just scalar0) previous128=-1; queue ordinaryTrigger21C(1,true,0,60,false), EMPTYcallback; **then objective22(true), then NeedActionCompleted(0)** substitutes this leaf's actual needs34. E2G8B and local objective22 recipient increments Sparked count. No success inferred from outbound count, no new Pop mood/state-light call. Inherited action-clear Update controlsterminal.|005C7CF2..7D30/005C7E06..14;Spark objective handler005B181E..32;frozenP12/commonrecipient.|M13-015 success portion plus current Sparked recipient; needs/reward producer boundaries unchanged.|
|P12 stop/reset|Reset target=-1 always; only if latch130 set, clear latch BEFORE EnableStopOnCliff(true), reliabletrue/secondaryfalse. Preserve previous/retry fields. StopInternal tails Reset then common Stop cancels owned action and unwinds own remaining scope; inherited Update returns1 ifaction84 else2.|005C76AC..76F0/005BDA56..62;common005BD08C..D174;frozenP13.|Existing M13-015 Reset/common lifecycle, no unconditional clifftrue.|

## Actual Sparked owner lifecycle and reports

Use checked generic Sparked receiver, with **owner24, unlock13, min0/max60/reps1,objective22,soft206**. In the table `current` means actual current behavior supplied by its native manager; it is not synonymous with the configured Pop leaf.

|Step|Original behavior|Primary evidence / same-receiver checked row|Boundary|
|---|---|---|---|
|A1 subscriptions|Ctor subscribes8B then44 iff actual externalinterface, retains handles6C. Handlers have no selected guard. objectiveword==configuredA0(22) increments unsigned8C; observedFamily1/2 inserts uniqueID. Selection resets count/set.|005B0DF0..0E0C/005B1304..141A/005B181E..1866;KnockS6–S9/H6, same receiver.|M15-059; full observed44 hostserialization remains inherited missing, consume faithful local family/ID input.|
|A2 selection|Common selection stamp54/chooserhook/public activity24/basehooks/entryevent then ownhook. Own88=start,B0/B4/B5zero,state0,count0,observedempty. Hard requested case pushes driving20E/20D/20F with actualactivityname, smartidle213 Activity_ prefix, source0 backpack using own locatorC8; setB5=1; Started83 carries **actual requested unlock u32** (normally13, never force13); then activity lock true1,2,3,4,5,10. Requested13 does not enter special Peek25 branch.|005B3136..324E/005B1522..16D6/005B171C..175C/0071AF00..1C;locktable00C624B0/C624BC;KnockS10–S13/H1–H5/H9/H11.|M15-059/M15-058; requested and active kept separate.|
|A3 exact lights|Same generic hard pattern: RRGGBBAA black000000FF/whiteFFFFFFFF; on[black,white,white,white,black],offallblack;onMs[0,360,360,360,0],offMs[0,1110,1110,1110,0];transition arrayszero;offset[0,0,120,240,0]. C8 owned locator, not charger slot. Reset uses ownB5 and removes owneddriving/backpack, not smartidle.|005B1574..15C8/005B1634..165C;KnockH3/H4 and Reset matching receiver.|M15-059/common bodylight actualinputs; no copied caller-specific pattern.|
|A4 get-in/chooser|state0 chooses configured206 only when actualactive!=55&&activeSoft, otherwise211; single trigger1 on real arbitraryAnim child;state1. Continue while actual currentrunning; completed→state2 SelectNext via own chooser (delegate absent). StrictPriority order is S4; no forcedleaf/winner.|005B1D3C..D7A/005B1E0A..46;005B1FDC..2006;0060B23E..266.|M15-059/M8-013.|
|A5 completion/end check|In state2: repetition end when nonzero reps1 AND signedcount>=1 AND ordered rounded(rounded(start+0)+NegEps)<=now; timeout end when waittrue AND ordered rounded(rounded(start+60)+NegEps)<=now AND currentnonnull AND current.requiredUnlock!=actualactive;cancel65 also ends. NaN ordered comparisonfalse. Reset ownedlights/driving FIRST; then RequestCurrentBehaviorEndOnNextActionComplete destroyscallback/keepsactualaction;state3 then final lock true1,2,4,8. Count is actual objective subscription, not guessed terminalsuccess.|005B18D4..199C;literalB727C5AC;005A42C4..4304/005BD624..6B4;KnockS17/S21/S22 and followups. waitfalse SelectNext timeout005B2016..2062 is not active under this config.|M15-059/M8-019 request recipient remains its precise existing MISSING where unbuilt.|
|A6 reaction/request interruption|Update can state2→end→Complete→Reset→state5 under actual manager reaction branch. Actual requested mismatch/hardupgrade enters immediateend recipient thenstate4/B4true. OrdinaryStop is not a substitute for the missing general immediateend owner.|005B186C..18CE/005B19B0..1A08/005B17AC..1804;KnockS18–S20.|M15-059/M15-058/M8-019, real managerinputs only.|
|A7 get-out|state3 while actualcurrent running and class!=Wait39 continues chooser. Else active-soft orcancel→single23F. Hardnormal count<target→failure210 unlessinvalid/active41; success→216 unlessinvalid plus SuccessfulSpark mood. Append212,loops1,state4. No Pop-specific successanimation copied into owner's getout; Pop's21C remains its own earlier action.|005B1DBA..1F02,particularly1E68..1F02;KnockS23/S24.|M15-059; exact generic moodrecipient retained.|
|A8 Complete/report|state4 waits actualcurrentrunning then Complete and returnsnull WITHOUTstate5 write. Success predicate target0 OR signedcount>=target; cancelled/B4 emits selected precise diagnostic thenreturns. Otherwise clear requested55/false only if active==requested; Ended84 withactualboolsuccess only active55 ORhard. One chosen finaldiagnostic event per checkedSF3; no invented terminal/success.|005B1E00..24/005B1A90..1C2E/005B2094..20D6/0071B00E..20;KnockS25/S26 + SF3.|M15-059/M15-058; E2G83/84 raw/local/API topology unchanged.|
|A9 null/picker/deselection|Normalnull first enters actual parentpicker; it does NOT by itself deselect/clear owner. At actual supplied changedselection boundary: common reactionlocks→smartidle→interlude release→ifneedspendingrewardcallee→exitdiagnostic→ownReset→delegate(none)→cubeStopAllAnims layer1/allobjects. Destructor retires subscriptions/handles, no implicitComplete. No fallbackactivity/rewardamount invented.|005AE68C..E762/005ADE02..66;005B33B8..3660/005B1786..179E/00638D58..7A/005B1484..1500;KnockSF1/SF2/H10/H12 controlling.|M15-006/M15-059/M15-071; parentpicker and amount remain precise inherited MISSING.|

## Record assessment and remaining uncertainty

- **No whole existing record proposed settled.** Current M13-015 title/status/evidence quoted above still covers retry/result/reset, and this exact configured Spark path invokes those native portions. Actual public/manual/API adapters remain partial (ShippedBehaviors.Navigation constructs public ordinary/Sparks wrappers; ManipTool ordinary caller/hardware judge remain). Their nearest-upright/split async drive/dock/missing refresh/scope/typedcallback/immediatefinish/unconditionalcliffrestore is not repaired by binding this native configuration. Keep M13-015 partial; no API exclusion/removal or fabricated bridge.
- **Same generic unowned Pop complement as ordinary packet**, extending the demonstrated configurations to ordinary PopAWheelie and SparksPopAWheelie: constructor/cache8/runnable/initial reaction/pre-dock cliff plus actual DriveToPop and Pop Select/Verify/completion. Exclude existing M13-015 retry/result/reset ownership and shared generic IDrive/IDock/Turn/ActionList/geometry/face recipients. No new per-Spark duplicate proposed, no ID allocated, no official widening applied. Proposed completion scope records standingM1-0340055268E..269C and the identical smallest fullpayload guard; priorstate/local/scalar/proxy/callback retained.
- M15-059 current whole lifecycle remains partial across its other actual configurations/branches; this job recovers this exact owner, not every sibling. Common M7-018/config/factory, M8-001/framework, M8-013/choosers, M13-014/IDrive and M12-017/037 generic paths also remain their whole current scopes. This caller proves only the listed same-receiver invocations.
- M12-052 already owns Ground completion; direct Pop never calls it. Earlier SparksPutDown chooser candidate is its own behavior path, not a reason for another Ground owner or importing Roll recovery. M15-077 all-four Roll math-only equivalent boundary is untouched and supplies no new Pop status claim.
- Exact inherited **MISSING** boundaries: parent Freeplay spark admission/picker after null (M15-006), actual requested/active/progression/process/severe/world/face/calibration/firmware inputs where their producers remain unbound, general immediateend/stop-on-next-action recipient (M8-019 where unbuilt), reward amount (M15-071), full observed44 serializer/local topology frontier, face/speech/vision/planner/geometry/ActionList recipient gaps already named by the frozen packets. Stop only the affected recipient; do not fabricate winner/defaultface/timer/success. Other activities/audio remain leftovers, not an outward audit.

## Source-based validation targets

Use actual shipped leaf plus actual owner24/config and real common lifecycle/manager tick, independently supplied native admission/requested/active inputs and physical/firmware scene. Check min0/max60/reps1/objective22/soft206 and exact chooser PutDown-before-Pop. Exercise active soft vs requested hard separately; active hard skips Pop get-in/optionalface, active soft preserves actual initial Turn/LiftSafe/face191true with18B/C; constructor193false/laterwriters remain native. Preserve both generic C0 empty-space and C8marker-turn prefix, actual predock lock→clifffalse, native ordered/unordered height and Radians epsilon pitchverify, actual scalar/proxy guards, retry targetrefresh/cap/18D-vs18E,21C→objective22→needs34, conditional cliffrestore. Owner tests observe real objective subscription before end-on-next-action/currentrunning/getout/83/84/deselection, not injected count/goal/status/terminal/winner. No automaticnull-deselect. Boundary tests may label isolated scalarcallbacks; they do not replace end-to-end firmware-scene checks.

This is one coherent source packet ready for independent native READ check and root freeze/release. No implementation, tests, commit, hardware run or whole-behavior acceptance is claimed.
