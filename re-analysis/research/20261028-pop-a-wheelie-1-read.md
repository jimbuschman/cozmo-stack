# POP-A-WHEELIE-1 — exact ordinary configured own path

2026-10-28. Answers `jobs/trial-m13/POP-A-WHEELIE-1.md`. Requested startup gpt-6.1-sol/medium, fork_none; backend telemetry unavailable. PROJECT_STATE read FIRST, then AGENTS, job/AUTONOMY/PLAN/BEHAVIOURS/research README. Read-only extraction; this report is the only research output. No production/test/official metadata/git/hardware changes. Native ELF `resources/lib/armeabi-v7a/libcozmoEngine.so`; Python313 explicit executable. `known.py` queries preceded PopAWheelie, DriveToPopAWheelieAction, PopAWheelieAction, ObjectInteractionInfoCache, 5C7CBC, 8CB5AC, 8CC21C, CanInteractWithObject, CanPickUpObjectFromGround, 5C0CA8, 5BDA56, 59CEA6, 5C7ACE. Scratch `.scratch/pop-dump.py` prints original Thumb instructions/PLT names; ARM veneers were separately decoded with arm_disasm. No decompiler files were available; index navigation supplied no evidence.

## Current ownership before conclusions

716-record manifest reread. Literal current existing whole candidate:

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

## Literal current shared-owner snapshots

These snapshots supply current ownership comparison only; none is proposed settled or contradicted here. JSON read explicitly UTF-8.

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
  "id": "M12-018",
  "title": "The dock abort path",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x0063BE10 AbortDocking; 0x0063BE2A blx 0x4B94C8; 0x0063BE36 blx 0x4A5368"
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
  "id": "M13-007",
  "title": "FindObjectOnTopOrUnderneathHelper: its found-object test and the tolerance at each of its eleven callers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BlockWorld::FindObjectOnTopOrUnderneathHelper(obj, float tolerance, filter, bool) 0x0062601C takes the tolerance as its second argument (r2 captured at 0x00626038 into the lambda at 0x006260CE/0x006260D8), reads the height through GetDimInParentFrame<(char)90> (0x00626070), adds +/-half to the parent Z (0x0062607E..0x006260A0) and calls FindLocatedObjectHelper (0x0062610E)",
    "StackOfCubes::BuildTallestStackForObject passes 30 (0x41F00000 at 0x0061928C and 0x00619344); BlockWorld::UpdatePoseOfStackedObjects 0x00621908, DockingComponent::CanInteractWithObjectHelper 0x0063C728 and CarryingComponent::SetObjectAsAttachedToLift 0x00632F0C pass 15 (0x41700000)",
    "0x0062601C..0x0062619A the helper; 0x0062B9FC..0x0062BB1E the predicate lambda (vtable 0x0102E194): 0x0062BA0E..0x0062BA46 wrt-parent, 0x0062BA50..0x0062BAA2 quad intersection, 0x0062BAC2..0x0062BB0C the height test",
    "the eleven call sites and their tolerance/onTop: 0x00507258, 0x00507664, 0x0055526E, 0x005BA136, 0x005C99C0, 0x0061929E, 0x00619358, 0x0062190C, 0x00625E1A, 0x00632F14, 0x0063C730",
    "the CORRECTION: 'everywhere else 15 mm' is wrong; RollBlockHelper::UnableToRollDelegate passes 30.0 (0x005BA12C movt r2,#0x41f0), and BuildTallestStackForObject's second call (0x00619358) is the underneath search",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md priority check 4",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q1; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q1 (PASS, with the float-direction omission recorded above)",
    "0x008F6220..0x008F6304, 0x008F6320..0x008F638C, 0x004E03B8..0x004E047E, 0x004DF764..0x004DF7BC",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q6; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q6"
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
  "id": "M8-009",
  "title": "Behaviour scope undo order: the engine releases in a fixed order (reaction locks, idle, motion profile, track locks, light patterns)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::Stop 0x005bd08c releases the scope in a fixed order regardless of acquisition order: (a) remove every disable-reactions lock while +0xac != 0 (0x005bd12c ldr.w r0,[r4,#0xa4]; 0x005bd136 blx SmartRemoveDisableReactionsLock; 0x005bd13a/0x005bd13e/0x005bd140), (b) remove the idle animation if +0xb0 (0x005bd142 ldrb.w r0,[r4,#0xb0]; 0x005bd14c blx SmartRemoveIdleAnimation), (c) clear the motion profile if +0xc0 (0x005bd150 ldrb.w r0,[r4,#0xc0]; 0x005bd158 blx SmartClearMotionProfile), (d) walk the track-lock map at +0xb4 and MovementComponent::UnlockTracks(mask, name) each, then destroy the map (0x005bd15c/0x005bd16e/0x005bd174 blx 0x4a5794; 0x005bd19e blx __tree::destroy), then clear the custom light-pattern vector at +0xcc/+0xd0 (0x005bd1a2 ldrd r1,r0,[r4,#0xcc] ... 0x005bd1c6 strne.w r0,[r4,#0xd0]).",
    "This stack's BehaviorScope groups its undos by the same categories and releases them in that order (IBehavior.cs, BehaviorScope.Dispose): reaction locks (SmartDisableReactionsWithLock/DisableReactions), idle (SmartPushIdleAnimation), motion profile (SmartSetMotionProfile), track locks (LockTracks/SmartLockTracks), custom light patterns (SmartSetCustomLightPattern). OnRelease hooks have no engine counterpart and run last. Within a category the undos run most-recent-first, which is required by the track-lock undos' saved-mask restore; the engine unlocks each map entry independently, so only the category order is observable.",
    "The policy review of 2026-09-29 (verdict C) found the divergence; @cozmo-verifier re-read all five release sites and the light-pattern tail (0x005bd1a2..0x005bd1c6), which the review had not read past 0x5bd180."
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
  "id": "M15-077",
  "title": "BehaviorRollBlock (Hiking_RollCube): cache 7/6 target choice, Init, TransitionToPerformingAction, RollBlockHelper, DriveToRollObjectAction and RollObjectAction, the axis-change retarget, success animation/objective/needs and stop/reset",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Factory 0x0059CF86; ctor 0x005C8598 (isBlockRotationImportant default true 0x005C85DC..0x005C85F4); class range 0x005C867A..0x005C8E3C.",
    "TransitionToPerformingAction 0x005C86E8..0x005C8938: target -1 warns and returns; face-turn 0.0f when +0xD9 or +0xD8 set, else 1.5707964f (0x005C8734..0x005C8744, 0x005C8934); CreateRollBlockHelper(...,target,true,params) 0x005C8814; SmartDelegateToHelper 0x005C8850; score 0.8f 0x005C888E; state light {target,25,false} 0x005C88A6..0x005C88CE.",
    "UpdateInternal 0x005C8A54..0x005C8B24: axis change AND ComputeDistanceSQBetween(robot,target) > 14400.0f (0x005C8ABE..0x005C8AD2); StopActing(false,false); pre-dock latch +0x129 -> TransitionToRollSuccess; else refresh and +0x124 == -1 returns 2.",
    "Target cache 0x0056F9B4..0x0056FA18 / 0x005707F0..0x00570902 (intention 7 when rotation is important, else 6); RollBlockHelper 0x005B98D4..0x005BAC54; DriveToRollObjectAction 0x0055C9DC..0x0055CECE with IDriveToInteractWithObject 0x0055B2C8..0x0055E0E6; RollObjectAction 0x00556540..0x00556DCE with shared IDock 0x005514FC/0x005521AC.",
    "Read packets re-analysis/research/20261028-hiking-roll-cube-1-read.md, -drive-joins.md, -reentry-contract.md with their independent checks; HANDOFF-HIKING-ROLL-CUBE-1.md; re-analysis/research/20261028-hiking-roll-cube-1-claude-check.md"
  ]
}
```


This current title owns retry/result/failure/reset, and can be proposed complete independently after those actual native joins are built and checked. Its Reset true shorthand does not assert unconditional transmission. No current metadata correction is implied. Broader class/cache/init and leaf-action scopes are not silently absorbed by it.

## Config, admission and complete class path

Each row is source read, not a fidelity status change. Exact asset root is `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/`.

|Step|Observable/state contract|Primary citation|Owner/boundary|
|---|---|---|---|
|P1 exact configured instance|`behaviors/freeplay/popAWheelie.json`: class/ID/needs PopAWheelie, display behavior.PopAWheelie, unlock PopAWheelieAction, confirmed/possible object handling false, considerThisHasRun objective PoppedWheelie. No alwaysStreamline override. Do not import Sparks instance.|Asset lines2–9; common actual ReadFromJson parsers already checked at005BB9EE..BA02/005BBA70..BAC8/005BBAE8..BB46/005BBFEC..C118. Factory0059CEA6 allocates138 and0059CEB6 calls actual class constructor.|M7-018; existing common config/parser.|
|P2 caller boundary|PlayAlone BehaviorsOnly/Simple duration25/cooldown30. Scoring entry PopAWheelie flat0.8, repetition graph(5,0),(30,.5),(100,1). Earlier/different candidates can win. No forced chosen child, no host timer substitutes parent picker.|Exact `activities/freeplay/playAlone.json`, PopAWheelie entry and strategy. Same checked Scoring admission contract used by Side/Stack packets; no new whole-parent extraction.|M15-006/M8-001/013; actual parent selection remains inherited MISSING.|
|P3 constructor|Base IBehavior then target120=-1, previous128=-1,retries12C=0,cliffChanged130=false, own Robot134. No custom UpdateInternal: vptr010274D4 slot0C relocation→005BDA56.|005C7430..746E; actual vtable010274CC+8;005BDA56..62 returns1 iff action84!=0 else2.|NEW class lifecycle complement to narrow015; common lifecycle M8-001.|
|P4 runnable/gates|IsRunnableInternal invokes virtual90 UpdateTargetBlock then checks120!=-1. Target queries actual Robot134 AI264 cache14 intention8, not ClosestCube. Class gate28=005C7ACE returns0 (cannot run carrying);20/24 inherit false defaults. Common source unlock/severe/tread/platform/process gates retain actual inputs.|005C7478..7494,005C7734..7754 movs r2,#8, vtable01027564→7734;010274FC→5C7ACE;5C7ACE movs0;20/24 cells010274F4/F8→5BF04C/59EC12.|NEW class/cache; common M8-001.|
|P5 cache8|Constructor family2,1 filter instance atsp10 bound to key8; predicate CanUseForPopAWheelie, best DefaultBestObjectFunction. Predicate first rejects failurekind3 within30s/60mm/PI then actual Docking.CanPickUpObjectFromGround. This is not upright-only or AxisCheck.|0056FA30..FA9A family literal00C58A14={2,1}, GOT0103ECAC→00570929;0056FC7E..8A key8 pointersp10;0056FE00..2C key8 picker GOT0103ECC0→DefaultBest.00570928..980: r2=3,r3=41F00000,time30;stackdistance42700000=60; Radians global0105A990 (PI initialized004D7034..703E) supplied at00570954. ARM veneer008CB5AC..B8→PLT004ABB48→0063C880.|NEW cache-own row; existing shared cache cadence/default-best checked Pickup/Stack/Hiking packets, matching entry/receiver.|
|P6 geometric predicate|CanPickUpGround calls CanInteract(target,out pose), then rejects IsPoseTooHigh(pose,.5,10,0). CanInteract permits families1/2, IsRestingFlat10deg, rejects carried target, expresses target WRT actual MAIN, rejects any top object within15mm (filter default,true). No +Z-only check.|0063C880..8E4;0063C654..6BE family/flat/carry/transform;0063C724..730 top15 and0063C78A..792 foundnull test. Same native shared predicates as checked Stack/Pickup packets.|M12-012/M13-007 geometry portions; not whole-record settlement.|
|P7 Init/get-in|If D9(alwaysStreamline) OR D8(hard spark) OR currenttarget==previous128, direct Performing wrapper. Else Reacting: state label then sequential TurnTowardsObject(target,PI,false,false) and TriggerLiftSafe18A(loops1,interrupttrue,tracks0,timeout60,finalfalse). Callback sets previous128=current120 ONLY if result0, then performs regardless result. No eager reaction lock or cliff toggle here.|005C7496..74BC;005C751E..753A PI40490FDB;005C7548..757A;callback005C7B52..64→ARM veneer8CC21C→4B3480/perform wrapper. State label helper005C0CA8..D1C only logs and assigns state string.|NEW class lifecycle; native shared Turn/trigger/compound reused matching arguments.|
|P8 perform construction|Wrapper sets state Performing then calls boolfalse. Boolbody target==-1 warns/returns without action. Retry=true increments12C; false zeros12C. Face maxRadians0 if D9/D8 elsePI/2. Construct DriveToPop(target,false,0f,false,Radians(max),true), set SayName18B/NoName18C, set pre-dock callback, StartActing typed report; then IncreaseScoreWhileActing(.8f). No class state-light setter.|005C7640..7678;005C776A..7770;005C7776..7780/005C780A..781C/005C78EE literal3FC90FDB;005C7822..7854;005C7858..78B2;005C78CA..D4 literal3F4CCCCD.|M13-015 perform/result portion; NEW broader constructor/lifecycle; common M13-014 IDrive.|
|P9 pre-dock callback|When actual IDrive Wait2 reaches callbackE0: own-name SmartDisableReactionsWithLock with native21pair table00C6883D; set130=true; send EnableStopOnCliff(false), reliabletrue/secondaryfalse. This occurs before actual dock Init, including ignored inner-drive failure path.|005C7BC8..7C10 table PC-add005C7BD6+4+006A0C63; bytes trueindices0,1,3,11,12,14,15,20. Shared Wait2 actual0055E0C8..E0E6 and AddDock0055B7AC..B868 checked Hiking drive D2.|NEW lifecycle/cliff side effect + M8-011 scope; do not use blanketDisableReactions.|
|P10 complete retry/fail|Nonzero scalar report first removes ONLY own-name reaction lock. category4 and signed12C<=0 calls SetupRetry. Other cat4 orcat3 logs FailedAbort, located currenttarget family-1 then WB.SetFailedToUse(target,3); other categories log FailedPopAction. It does not immediately call Stop/Reset; inherited Update returns2 when no action. Full original report scalar retained.|005C7CCE..7CFA;005C7D44..4A;005C7D8C..DAA;005C7DB0..DFE; inherited005BDA56.|M13-015 whole retry/result owner.|
|P11 retry action|SetupRetry first virtual90 refreshes actual cache target (retarget may change orbe-1). If fullresult04000001, LiftSafe18D; else18E; loops1/interrupttrue/tracks0/timeout60/finalfalse. No-args callback performs(true) regardless animationresult. Does not rerun initialTurn/get-in.|005C79E4..9EC;005C79F8..7A40;005C7A5E noargStart;callback005C7F68..6E setsbool1→ARM veneer8CC22C→4B3498. Whole targetrefresher7734.|M13-015.|
|P12 success/objective/needs|Category0 (not merely fullresult0) sets128=-1, queues ordinary TriggerAnimation21C (not liftsafe),loops1/interrupttrue/tracks0/timeout60/finalfalse with EMPTY callback. THEN BehaviorObjectiveAchieved(22,true), THEN NeedActionCompleted(0=use configured). Inherited action-complete/Update supplies terminal timing. No extra class MoodManager or state-light call exists in its inspected complete native class range; common needs/mood/animation recipients still execute.|005C7CF2..7D30;005C7E06..14. Same objective/API8B recipient checked drive-off; same configured NeedAction recipient checked Roll/Stack. No invented rewardamount or direct event beyond existing native recipient.|M13-015 success portion; M15 needs common owner.|
|P13 stop/reset|StopInternal tailcalls Reset. Reset stores target120=-1 unconditionally. Only when130true: clear130 FIRST, send EnableStopOnCliff(true), reliabletrue/secondaryfalse. It preserves128/retry count. Common Stop then cancels actualownedaction and undoes remaining scope in established order; no blanket always-send.|005C76AC→ARM8CC210→4B34A4;005C76C8..76F0. Common005BD08C..D174 retained.|M13-015 Reset whole; M8-001/009/011 inherited.|

## Actual DriveToPop and leaf docking

|Step|Contract|Primary citation / checked identical shared recipient|Owner/boundary|
|---|---|---|---|
|D1 composite constructor|DriveToPop type4 preaction, distance0; delegates full supplied bool/float/angle/manual fields to actual IDrive base. Creates PopAction(target, callerthirdbool=false) and AddDockAction(pop,false); outer proxy tag is actual Pop actiontag. This is a compound native runner, not async DriveToObject followed by Pop.|0055D1E8..D224;0055D248..D26C. IDrive0055B2C8 shared ctor and AddDock0055B7AC checked Hiking D2; Pop ctor0055256C..25C6.|NEW Pop composite/leaf scope; M13-014 shared IDrive remains broader open.|
|D2 inner/face/waits|Reuse checked actual IDrive innerSeq[Drive,Wait1], outerAdd(inner,true,false), optional face/object children constructed iff maxRadians>0. Ordinary Pop's PI/2 DOES construct these (Roll zeroangle no-effect D4 does NOT apply). Say/noName setters18B/C are effective on live face child. Actual face input/query/calibration/speech frontiers are existing M13-014/M14-006; absent producers remain visible, no fabricatedface. Wait1 disables dockB9 only after drive success; missingdock defined diagnostic. Wait2 invokes own predock callback then dock. Proxy preserves leaf completion.|Shared0055B384..392,0055B8CC..B94E/0055B9AC..BA2E;Wait1 0055DE34..DEEA;Wait2 0055E068..E0E6;AddDock0055B820..868. Checked `20261028-hiking-roll-cube-1-drive-joins.md` D2, `20261028-shared-children-1-read.md`, `20261028-knockover-cubes-1-read.md` matching actual receiver/arguments only.|M13-014/M13-027/M14-006; inherited named input boundary, not permission to drop children.|
|D3 leaf metadata/select|Pop ctor delegates IDock(namePopAWheelie, actiontype1D, flag95=callerfalse). Actualvtable InitInternal returns0; preactiontype4; generic Init/Check/abort/cleanup/turn-marker/visual/wire lifecycle identical IDock from checked Pickup. Select target WRT MAIN.GetParent, failure03000004. Set dockaction9. Heightdelta<=33.85f(42076666) then require rawcarrying==-1 else03000017; too high→03000004. NaN follows BLE unordered branch to the carrying check; only ordered >33.85 rejects as too high.|0055256C..25C6;00552768..278E/00552796..27CE/00552824..287E. vtable01022178+8:Init1C/Check20 IDock;30→00557412=0;3C→00557428=4;40→0055295C. IDock checked Pickup2/PickupChildren source packets same exact receiver and flag arguments.|M12-017/018/037 common; NEW leaf Select/Verify/completion.|
|D4 firmware/verification|Same IDock DockWithObject9 and source18B fields/marker ownership/handlers, checked M12-003/005/034. Leaf Verify does NOT create Pickup visual subtree. If dockaction!=9→0300001A. If actual DockingComponent+5 successfalse→04000003. Successflagtrue tests actual Robot.GetPitchAngle() < Radians1.0: true→0400000A, false→0 (do not abs pitch or add camera check). Use native Radians comparison, not raw float comparison: operator<0084CD12 swaps arguments to operator>0084CC90; positive delta must also fail IsNear(epsilon3727C5AC) before returningtrue (0084CCA0..CCD6). Thus pitch1.0 and just-below1 within epsilon succeed; NaN comparisonfalse also follows success; negative ordinary pitch fails. Existing Radians recipient is retained.|00552962..299E/005529EC/00552A2C..A32/00552A34..A72. Common IDockCheck005521AC and same native PnP/DockingSuccess flag as checked Pickup/Roll. Native timeout actualbase30s unchanged.|NEW leaf Verify; firmware reality/hardware unknown not upgraded.|
|D5 typed fullcompletion UB|GetCompletionUnion first creates ObjectInteraction candidate then unconditionally overwrites with second constsetter. Final defined target=7C,count1,flagfalse (strh1 atsp2C). Four unused IDwords atsp1C..28 are uninitialized and serialized/copied by existing22byte setter. Do not infer successflag from pitch, preserve first candidate -1s, or claim zeros source-faithful. This is undefined-memory behavior under standing M1-034; smallest affected full-payload construction needs visible owning-record guard, prior state retained. Exact scalar ActionResult and defined field carrier are recoverable; manager must freeze the precise guard before builder release. Existing PlaceRel compatibility policy does not automatically cover this Pop leaf.|005525F4..266E firstbranch;00552670..2684 firstcandidate;0055268E..269C unconditional finalconstsetter, countflagstrh1, targetstore. Existing checked setter0075C460..C4A6 copies all words/Pack0075B4F0..B536. No new policy inferred.|NEW leafcompletion, standingM1-034.|

Shared pre-action/default posefn/fullgoal list/planner/profile/pathclock/IDock source recipients reuse `20261028-pickup-children-1-read.md` rows4–13 and `20261028-hiking-roll-cube-1-drive-joins.md` D2, only actual type4/default receiver cases. No Flip-specific pose picker, RollToUpright setter, Roll custom Verify or Pickup own visual solver is imported. Native child lifecycle uses actual ActionList updates, current canonical MAIN/FORWARD/carry/docksuccess/pitch, not task completion or supplied status. No hardware run.

## Live candidate differences and source tests required

Current `CubeGameBehaviors.cs` ordinary Pop uses closest upright cube and blanketdisable at start, always plays18A without TurnObject, async DriveToObject→Pop instead of actualIDrive composite, missing own predock lock/clifffalse and typedcallback. It logs objective/needs instead of actual native deliveries, ends synchronously on callback failure, and unconditionally sendsclifftrue onstop. `DockActions.cs` Pop leaf uses currentfacade carrying and simplified dockcheck rather than exact Selectheight/Verifyflag+pitch. These are candidate differences, not source evidence.

Meaningful tests must enter the actual configured Pop instance after explicitly supplied actual admission, exercise real cached target selection (including lying geometric closest/axis candidates), native get-in compound and successful/failed initial animation cache behavior; D8/D9/sameprevious skip; native IDrive children/proxy/fullreport; predock locktable then clifffalse; exact B8/RobotState successflag/pitch verification around1.0 including native epsilon/NaN/negative; firstretry refreshing different/no target, exact04000001 versus othercategory4 triggers, one retry cap; category3mark vs category2 no mark; category0nonzero scalar success path lock removal and source queue→objective8B22→configuredneeds order; cancellation before/after predock and exactly conditional cliffrestore; inherited Update/actionclear terminal timing. Tests cannot fabricate native status/goal/winner or use production-derived expected values.

## Ownership and remaining frontiers

- Whole current **M13-015** candidate: its full retry/result/reset contract is now source-read, including resolved virtual90 and callbacks; propose settlement only after build/independent tests, independently of broader neighbor gaps. No status changed.
- Coherent NEW proposal: **BehaviorPopAWheelie ordinary configured lifecycle/cache/get-in/pre-dock cliff ownership plus DriveToPopAWheelieAction and PopAWheelieAction Select/Verify/completion**. Existing narrower015 retains result/retry/reset ownership; proposal must explicitly exclude those duplicate claims while cite their connection. An alternative is a manager/operator-approved widening of015; this reader does not choose metadata mutation. All716 titles were searched for wheelie/docking/interaction/cache owners; shared M13-014/M12-017/018/037 own their common machinery, not this leaf Select/Verify or configured cache/init.
- Partial existing records remain existing: M7-018 allconfig/factory, M8-001/009/011 allframework/scope, M13-014 Knock/IDrive/face, M12-012/031/036/037 geometry/marker, M15 commonneeds/reward, M14-006 speech. Do not propose their whole settlement from this caller.
- Exact inherited MISSING: PlayAlone parent Scoring reload/winner/admission and actual active spark/process/progression/face/calibration/scene inputs unavailable in current producers; retain their own records and stop only affected recipient. Source operation is not replaced with guessed winner/defaultface/success/timer. Other sibling activities and audio uncertainty remain leftovers, not audit scope.
- Defined fullcompletion guard is not frozen yet (D5). M1-034 permits skipping original undefined unit, not silently borrowing PlaceRel zero-unused-word policy. Root must state smallest guarded unit and owningrecord before implementation.

No native contradiction of current M13-015 wording established; no official file changed, no record settled, no commit. Source-only output awaits independent native check/root freeze.

