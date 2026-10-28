# SPARKS-STACK-1 — native source packet

2026-10-28. Answers `re-analysis/jobs/trial-m13/SPARKS-STACK-1.md`; manager formal start f59a3ed3, approved correction publication3d9b801b. Extractor A, source only. PROJECT_STATE and research/role instructions read. Actual model/effort telemetry unavailable. No implementation, tests, git mutation, hardware or official record edits. Native authority: local `resources/lib/armeabi-v7a/libcozmoEngine.so` SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; ARM/Thumb instructions and literal data after known.py. Decompiled text is not authority.

This promotes the read-only preflight and supersedes its partial-dump interpretations. This packet recovers the entire finite configured own path; independent B whole-packet check and manager release remain separate. Cache reciprocal cross-check, exactzero Drive1, placement verification/completion and required recovery action joins are complete below. Frozen approved M7 row885 contradiction is recorded separately: parent reread current record/inventory/snapshot, B independently confirmed, and operator explicitly approved “Approve corrected contract and fix.” Formal record/inventory changes remain with the manager/Claude. Correct native gate is S8 below, never the old both-valid-neighbor/top-argument wording.

## Current owning records, quoted before claims

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
  "id": "M12-025",
  "title": "PlaceRelObjectAction: ctor, InitInternal and TransformPlacementOffsetsRelativeObject",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00554BE0..0x00554D0E ctor; 0x00554DCC..0x00554E30 InitInternal; 0x00554E40..0x00555094 the transform; literals 0x3E860A92, 0xBFC90FDB, 0x3FC90FDB, 0xC0490FDB, 0x40490FDB, 0xC1800005",
    "0x0052A6C2..0x0052A754 the wire factory; 0x0055C7D0 ctor, 0x0055C83A ldr sb,[sp,#0xAC], 0x0055C882 cmp sb,#0; 0x0052A6DC movs r1,#1",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q2; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md priority check 1 and objections A3, A4, A5",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q6, Q7; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q6, Q7"
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
  "id": "M15-059",
  "title": "ActivitySparked lifecycle: OnSelected (HardSparkStartedByEngine tag 131), OnDeselected (StopAllAnims), objective/observed, Update/checks/end/interrupt, get-in/out, Complete (meta.upgrade_replay_* events, HardSparkEndedByEngine tag 132)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: OnSelected 0x005B1518..0x005B16DC, 0x005B171C; OnDeselected 0x005B1780..0x005B179E; objective/observed 0x005B181E..0x005B1866; Update/check/end/interrupt 0x005B186C..0x005B1A16; get-in/out 0x005B1D18..0x005B1F2A, hard max 0x005B1F94..0x005B2066; Complete 0x005B1A90..0x005B1C2E, 0x005B2094."
  ]
}
```

## Additional current recipient record quotations

Read directly from the current manifest before assigning the recipient ownership below; statuses remain unchanged.

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
  "id": "M12-022",
  "title": "DriveToObjectAction::InitHelper's DriveToPoseAction goal",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005590AE objectPose.GetWithRespectTo(robot.parent, local_5c)",
    "0x005590E6..0x005590FE delta = robot.xy - local_5c.xy; 0x00559100..0x00559160 normalize; 0x005591C2 vldr s2,[sb,#0x84]; 0x005591E4 vmul delta *= distance",
    "0x00559220 vadd goal.y = delta.y + local_5c.y; 0x00559224 goal.x = delta.x + local_5c.x; 0x00559230 goal.z = robot.z",
    "0x00559232 eor r0,r1,#0x80000000 / 0x00559236 eor r1,r2,#0x80000000 / 0x0055923A blx 0x4A4510 (atan2f)",
    "0x00559278 blx 0x4A47E0 Pose3d(yaw, Z_AXIS, goal, parent, name)",
    "the C# uses the chosen pre-action pose's position with the heading toward the object; the engine's +0x84 distance and exact point are unread",
    "0x0055883C/0x0055884C 4-arg ctor stores; 0x005585A0 7-arg ctor strd (r1=-1.0); 0x0052A0EE and 0x0055B6E2 the only 4-arg ctor calls; 0x0052A0BA ldrb [r4,#0x35] usePreDockPose",
    "0x005B7BF8, 0x005B9038, 0x005B91E6, 0x005B9B2E IHelper::CreateDriveToHelper callers; 0x005B584A ldr [r7,#0xf8]; 0x005532BC..0x00553360 the AlignmentType table",
    "0x00559CDE/0x00559CE8 DriveToPlaceCarriedObject ActionType 1 or 2; 0x00554B40/0x00554B42 the only caller passes 1 (so 2)",
    "unity/scripts/csharp/Robot.cs:1580-1585 and SpeedTap/SpeedTapPlayerGoalCozmoSelectCube.cs:97 (goToPreDockPose:true)",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1a-Q1d and re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md (rows N-1..N-8 verified; priority check 2)",
    "re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q5 (the NaN row: contradicted the earlier record text)"
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
  "id": "M12-034",
  "title": "Unread dock-action handler bodies and the ramp/charger validity overrides",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005516EA, 0x00551750, 0x00551F9C, 0x00550858",
    "re-analysis/research/20260929-R-VIS-verify-M12-gap2.md 'What stays open'",
    "re-analysis/research/overnight/20261014-ov03-m12-034-check.md: build rows 1-4. Registration/lifetime 0x005516DE..0x005517C0, 0x00551EDC..0x00551F66, dtor 0x0055745C..0x005575B2; +0xF0 sources 0x005503D6, 0x005536EE, 0x00554C68..0x00554C94, 0x005565E0; RobotDeletedLocatedObject 0x005524C0..0x00552510 with CheckIfDone 0x005521BE..0x005521C6/0x005522BC/0x005523DC; Charger 0x004EA648; Ramp 0x0050F9AC..0x0050FA9E read and unreachable (ctor callers 0x0050F89A only, inside CloneType)."
  ]
}
```

```json
{
  "id": "M12-036",
  "title": "ActionableObject::IsPreActionPoseValid and BlockWorld::GetObstacles: the pre-action pose filter",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x004DF2C0..0x004DF5F4 IsPreActionPoseValid; 0x004DF61C the literal; 0x00626D44..0x00626E0E GetObstacles; 0x00626BB5, 0x0062C1D4..0x0062C2D6",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q2.4-2.8; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q2 and objection 2.8"
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

```json
{
  "id": "M8-015",
  "title": "ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0053E6A8..0x0053E6E0; 0x005409E2..0x00540A40; 0x00541146..0x00541238; 0x0053FAB2; Current Cancel then delete; pending direct deletion preserves existing result. Cancelled0x02000000 except NotStarted0x02000001; stop before unlock, watcher then broadcast. Existing B-ACTIONS candidate must be checked for full teardown effects and concrete descendants.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "P3/P4 full ActionList/ActionQueue/IAction lifecycle: ActionList 0x0053F580..0x0053F5F2, ActionQueue 0x0053F5F4..0x0053F70C, IActionRunner 0x00540370..0x0054063E, clock/timeout 0x00540D4A..0x00540E80, CheckIfDone 0x005549B8..0x005549F4 (20261006-M1M2-missing-triage.md P3/P4).",
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C2..C7, L14..L23, D5, S2..S4 and re-analysis/research/20261012-m8-play-animation-actions-check.md (the +0x57 gate on L17/L18/L20, the DeleteActionAndIter guard set at queue+0x10, ActionWatcher Start/EndUpdating 0x00540386/0x0054057E/0x00540630, the UpdateInternal status string 0x00540D46..0x00540F1C).",
    "re-analysis/research/overnight/20261014-ov18-m8-015-check.md: L1-L9 0x00540370..0x0054063A, 0x00540D1C..0x00540F9C, 0x00540750..0x005407C0, 0x005409DC..0x00540A44; Q1-Q7 0x0053F580..0x0053F708, 0x0053DCE4..0x0053DD92, 0x0053E24C..0x0053E2F6, 0x0053DF2C..0x0053DFC8, 0x0053DE10..0x0053DE5C, 0x0053E690..0x0053E700; D1-D6, D12 0x0053F9E4..0x0053FB06, 0x00541084..0x00541286, 0x00541BB4..0x00541C04, 0x0054187E..0x005418DE; T2 List Clear 0x0053D916..0x0053D93A, 0x0053F938..0x0053F950, 0x0053FB5C..0x0053FB82; QueueAction front end 0x0053D93C..0x0053DAC8; position table 0x0053DA24..0x0053DB70.",
    "re-analysis/research/overnight/20261015-ov56-58-check.md: teardown mechanics from OV56: ~Robot order 0x005110EE..0x00511156; AbortAll 0x00511952..0x0051198C (0x0063BE2A..0x0063BE36, 0x00517DFE..0x00517E0A); ActionList::Cancel clearing gate and slot order 0x0053DE18..0x0053DE54; ActionQueue::Cancel 0x0053E69A..0x0053E6FA (early exit 0x0053E6E4); QueueNow/QueueAtEnd 0x0053E25A..0x0053E36E; DeleteActionAndIter 0x0053F9E4..0x0053FAB2 (re-entrancy set, INTERRUPTED/no-interface gates); result codes 0x0053FDBA..0x0053FDD4, 0x005409E2..0x00540A40; Clear lock-out 0x0053D91A..0x0053D938, 0x0053DCEA..0x0053DD00, 0x0053DCAA, 0x0053DF40, 0x0053F93C, 0x0053F970..0x0053F9AC; ~ActionList double Clear 0x0053D8F6..0x0053D90E; IBehavior StartActing reject-delete 0x005BDC04, Init failure 0x005BCCC6..0x005BCD06."
  ]
}
```

```json
{
  "id": "M8-017",
  "title": "The compound action runtime: ICompoundAction, CompoundActionSequential and CompoundActionParallel (construction, AddAction naming, update/result tables, retry, completion store, teardown), as used by the go-to-sleep sequence and other compounds",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0052CEA2..0x0052CFC0; Parallel/sequential child execution, ignore-failure predicate and completion/track interactions remain higher framework obligations; M1 owns only factory handoff and NOW/retries0 submission.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md addition 4..5: AddAction(IActionRunner*, bool, bool) 0x0054EC7C -> AddAction(function, bool) 0x0054ED90 (empty predicate; child inherits +0x57; compound name appended); the trailing boolean is never loaded.",
    "re-analysis/research/overnight/20261014-ov20-21-check.md: C1 0x0052CECA..0x0052CFA2; C2 0x0054E990..0x0054EA86; C3 0x0053FDB8..0x0053FE1C; C4 0x0054EC7C..0x0054EEAC; C5 0x0054F4F8..0x0054F54C; S1-S8 0x0054F70C..0x0054F992, 0x0054F5A0..0x0054F696, 0x0054F560..0x0054F59E; P1-P5 0x0054FAB0..0x0054FBCC, 0x0054EC56..0x0054EC7A; D1-D4 0x0054F050..0x0054F130, 0x0054F170..0x0054F1BC, 0x0054F270..0x0054F3EA, 0x0054EB78..0x0054EC2A, 0x0054FC2C..0x0054FC66; the seven log sites; AlreadyPrepped call sites 0x0054F0C0, 0x0054FB3C."
  ]
}
```

```json
{
  "id": "M12-040",
  "title": "Carrying/docking lifetime after preceding control abort",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0063BE10..0x0063BE5C; 0x005113FA..0x00511414; Dock Abort precedes carrying/docking raw owner deletes; do not add another destructor/send at the raw-delete sites. Remaining owned state/subscription effects UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/overnight/20261014-ov06-m12-040-check.md: R1 ctor order 0x0050FE24..0x0050FE4A; R2 Carrying ctor 0x00632A68..0x00632A82; R3 Docking ctor 0x0063BA1C..0x0063BA3E; R4 teardown 0x005113FA..0x00511418 (ranges corrected)."
  ]
}
```

```json
{
  "id": "M11-063",
  "title": "BlockWorld+0x74: the writer of the pose-change queue flag that gates OnObjectPoseChanged logs and queue",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-cliff-world-helpers-check.md: OnObjectPoseChanged 0x00624808..0x006248B8 tests byte BlockWorld+0x74 (MultipleChanges 0x006248EC, FirstPoseForObject 0x00624924, PoseChange push); no strb to +0x74 found in 0x00610000..0x00630000.",
    "re-analysis/research/20261013-blockworld-flag-and-map-helper.md Q1: constructor +0x74 = 0 (0x0061BD8A, strb [sl,#0x28] with sl = this+0x4C) and list setup (0x0061BD7C/0x0061BD86); UpdateObservedMarkers clears the PoseChange list (0x00624F78) and sets +0x74 = 1 (0x00624F7E), runs CreateObjectsFromMarkers, AddAndUpdateObjects, CheckForUnobservedObjects, UpdatePoseOfStackedObjects, then clears it (0x006250D4..0x006250D8) before BlockConfigurationManager::Update (0x0062520C) and UpdateMarkerlessObjects (0x0062521A). Reader 0x00624808..0x006248B8: flag 0 -> SetObjectPoseChanged only; flag 1 and queued -> MultipleChanges info (0x006248EC, 0x00BF8F50), not pushed; flag 1 and old 0 -> FirstPoseForObject info (0x00BF8F99, 0x00624924), not pushed; else emplace_back. Consumer UpdatePoseOfStackedObjects 0x00621794..0x00621AF4. Cliffs come from HandleCliffEvent on the same thread before UpdateAllRobots (0x004ED62E before 0x004ED648), so the flag is 0. The earlier note \"no strb to +0x74\" was wrong (da.py stops at undecodable halfwords)."
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

## Reused complete checked contracts

Reuse `20261028-sparks-knockover-1-read.md` S/P/H and `-source-followup.md` SF1–SF4 for the entire finite Sparked owner lifecycle, native PlayArbitrary child, reports83/84, reaction/idle/backpack, observed Family/objective delivery, end-next-action and explicit deselection. SF1 post-null native parent picker is before deselection; do not auto-retire this owner merely because Desired returned null. M8-019 scheduling and M15-071 reward payload remain named inherited frontiers. No sibling activity bodies/raw admission audit.

Reuse checked `20261026-pickup-pyramid-1-read.md` PK rows, `20261027-pickup-pyramid-2-read.md` direct IDock, `20261027-pickup-stack-1-read.md` native turn/visual inputs, `20261028-pickup-children-1-read.md` actual DriveTo/Search/planner/ActionList and shared children. Reuse M12-028/042 checked OV02 and its independent check for PlaceRel producer/helper. These source facts are dependencies, not whole-record settlement by citation.

## Own configured source rows

All “recovered” below proposes IMPLEMENTATION_GAP until actual production comparison; no manifest status is changed.

|Row|Original behavior and exact order|Primary locator / owner|
|---|---|---|
|S1|Exact behavior asset ID SparksStackBlock/classStackBlocks/needsStackCube_Sparked/requiredUnlockStackTwoCubes/stackInAnyOrientation=true; no executableBehaviorType or streamline override. Caller Sparked/ID27/requireSparkStackTwoCubes/StrictPriority[DriveOffCharger,KnockOverCubes,SparksStackBlock,SparksPickupSingleCubeToStack,SparksLookInPlace], min0,max80,reps1,objectiveStackedBlock40,softSoftSparkUpgradeLift.|`obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/sparkable/sparksStackBlock.json`; corresponding`activities/sparks/sparksStackBlock.json`. Activity table1033070 slot10330DC→C19A54; objective table1033140 slot10331E0→C19E0E; unlock10 slot10340E8→C1D931. NeedsEnumToString782DCC/table1033D40 slot1033DEC→C1C7ED confirms43; animationEnum75E298/table1032690 slot1032EA4→C19170 confirms softLift205. A independently primary checked these B-extracted table selectors. M7-018/M15-059 and own new lifecycle.|
|S2|Factory class0x34 alloc138; ctorIBehavior, Robot retained11C, topObjectID wrapper124/scalar128 and bottomwrapper12C/scalar130 initialized-1; flags134(anyOrientation)/135(replaced)zero; Read bool stackInAnyOrientation defaultfalse→134. Scalar and wrapper are the SAME target, not separate writers.|0059CFE6;005C93D0..9440 (ctor writes93F6,940C,942A). M7-018 + NEWderivedlifecycle.|
|S3|Common runnable gates off-treads0,charging0,carrying1; actual derived runnable refreshes targets then requires both scalar IDs!=-1. Running commonIsRunnable bypasses normal admission gates per reused framework. No own all-reaction-disable call exists in full class Init/Pickup/Stack bodies.|Vtable10279BC addresspoint10279C4 cells+20/+24/+28→5BF04C/59EC12/5CA1E6=0/0/1; checked RV18 in20261007-q17-runnable-rows.md;005C9484..94A4. M8-001 + NEW.|
|S4|UpdateTarget first calls progressionIsUnlocked(9,true), even shipped any=true. Unlock9 OR134 selects intentions5 top and3 bottom, otherwise4/2. Best top written128 first, best bottom written130 second.|005C94A6..9500, calls94BA/94CC/94F4,stores94D4/94FA. Cache C rows independently cross-checked A/B below.|
|S5|Init compares actual CarryingComponentID+8 with savedtop128; equal→Stack, otherwisePickup; returns0. It does not refresh targets again here.|005C9502..951E. NEW.|
|S6|Pickup state120=0,labelPickingUpBlock; params triggerCount23F,Radians0,flags00. D9(always) OR D8(hard) selects rawfloat0; normal selects raw90f42B40000; passed through actual Radians::operator=(float), so retain native normalization rather than treating90 as degrees orPI/2. Targetwrapper124. Success→Stack, failure callback EMPTY. Delegate return ignored; score0.8 afterwards.|005C96E4..97AA; params971A..42; literal97EC=42B40000/97F0=0;factory975C;success9780;score9796; onecallbackwrapper9C8C..9CDA passes emptyfailure. Reuse actual NativePickup helper/response/children.|
|S7|Stack state1,labelStackingBlock. Requires actualcarry!=-1 AND==top128; mismatch logs FailBackToPickup and entersPickup. PlaceRel targetwrapper12C,lowfalse,P{+0,+0,skiptrue}; success→FinalAnim,failure→FailedToStack; ignoredelegatebool; score0.8 afterwards.|005C9520..961A;factory9588;delegate95B4;score95CA; dataC69140. Already checked M12-028 S1–S4.|
|S8|Update obtains real validsets for top5/bottom3(shipped), tests cached wrappers. BOTHvalid skips neighbor query directly tostate/retarget. Topinvalid+bottomvalid logsinvalid/stops. Bottominvalid locates BOTTOM and TOP; either missing logsinvalid/stops. Else default EMPTY BlockWorldFilter, call FindObjectOnTopOrUnderneathHelper(BOTTOM,15f,filter,true); tolerate ONLY returnedptr==TOP. Other cases StopWithoutImmediateRepetitionPenalty and return2.|005C9844..9A24:98BC..98C2 bothvalid→9A24;98C6..98C8 bottominvalid→9924;992E..9954 lookups;99BC BOTTOMarg;99C0helper;9A1EcompareTOP. Corrected approved contract, operator approval recorded above. M13-007 body + NEWcaller.|
|S9|After valid/tolerated gate: only state1&&replaced135false calls GetClosestValidBottom. If samebottom, baseUpdate. Changedbottom calls StopActing(false,false) FIRST, then135=true,store130,newStack; thenbaseUpdate. Otherstates/replacedtrue go straightbaseUpdate. At most one successful retarget until Stop/Reset clears135.|005C9A24..9A84;9A72cancel,9A78flag,9A7Cscalar,9A84Stack. M8-001 cancellation + NEW.|
|S10|ClosestBottom seeds result from currentbottom. Missingcurrent or failed initial pose conversion returns seed. Uses actualMAIN and 3D distance, valid intention3 set, orderedObjectID iteration; only strictf32less updatesresult/best; equal/failedconversion retainsprior. No world-nearest-upright replacement.|005C9B80..9C70;9BDCvalidset;9C26/2EstrictMI. NEWcacheconsumer + reusedcheckedposeops.|
|S11|Final state2,labelPlayingFinalAnim; objective40,true FIRST; D9orD8 skipsanim+score. Otherwise plainTriggerAnimationAction21A/loops1/interrupttrue/tracks0/timeout60/strictfalse,StartActing(empty),score0.8. NeedActionCompleted(0) LAST substitutesconfigured needs43. Objective/animation/needs order differs fromKnockOver.|005C9E60..9F14;objective9EA4/9EA8;anim9EDC/9EEA;score9F0C;needs9F14. Actualobjective8B→Sparked40 counter→checkedownending/API. M15-008/M15-059 + NEW.|
|S12|Failed stack with actualcarry!=-1 builds Sequential[DriveStraight(-40f,100f,true),PlaceObjectOnGroundAction],bothAddActionfalse,false; starts resultless Robot&callback. On ANY deliveredterminal result callback refreshes targets, returns if eitherinvalid, else entersPickup. No-carry failure branch does same refresh→validity→Pickup directly. NoinventedFinish/Init/forcedsuccess.|005C9F80..CA04C;actions9FC0/9FD2/9FE8;StartCA006;callbackvtable1027A60,addresspoint1027A68+18→5CA2EA..312;veneer8CC24C→ARM8CC250/254+word8CC258FFBE741C→PLT4B3678(Pickup). M15-022 groundbody + NEWcaller.|
|S13|StopInternal and ResetBehavior clear top128,bottom130 to-1 and135false; state120 notresetthere. Resume uses inheritedIBehavior.ResumeInternal (baseUpdate). Destructor is inheritedIBehaviorD2, deletingdtor callsbase thenoperatorDelete. Stop,helper/component cancellation, ownedrunner watcher and callbacks use existingnative framework; no leafDispose→Complete/deselection synthesis.|005C97F8..981E;vtablecell1027A10 Resume relocation;10279C4 baseDtor relocation;5CA1D8..1E2→deleteveneer8CA88C. M8-001/M8-011 + NEW.|

|S14|Additional own virtual refresh hook(addresspoint+7C) forwards directly to UpdateTargetBlocks if called. It does not establish an extra automatictick cadence. The actual Stack runnable already calls UpdateTarget directly(S4). Expose exact hook, retaining external/unbuilt caller as inheritedframeworkfrontier.|Vtable1027A40→005CA224→veneer8CC25C/ARMword8CC268→PLT4B3660. PriorcheckedPickup0 uses its OWN7C in itsderivedrunnable, notproof ofadditionalStackcaller; A+BcheckedactualStackforwarding.|

## PlaceRel helper and required action recipient

|Row|Recovered exact source|Owner / locator|
|---|---|---|
|P1|Factoryalloc110/registershelper withactualcomponent; targetF4,lowF8,PFC/100/104,attempt108=0. Init resetsattempt0,preactiontype1,A/B. Precheck0→StartPlace; otherwise DriveToHelper params{type1,byte4=0,word8=0,byte12=0,A,B}, retaineddelegate success reentersStartPlace.|M12-042; checkedOV02 F1–F3;factory5B55C8..5606/ctor5B8EF8..8F82/Init5B8FD0..90FE. Exactzero Drive1 follows ordinary ctor branch P10 below, independently A+B checked.|
|P2|Start unsignedattempt>=3→MarkFailed thenstatus0; elseincrementFIRST,repeatvisualprecheck; actiontuple(robot,target,lowfalse,A0,B0,IDockflagfalse,skiptrue). ResponseAnim storesbound RespondToPlaceRelResult atC0, EMPTYconverter atA8; startsactionthroughgenericIHelper.RespondToResultWithAnim,notdirectsuccess. UpdateWhileActive simplyreturnsstatus1C. ShouldCancelDelegatesfalse; inheritedhelperretirement.|M12-042 checkedOV02F4–5;5B9168..930E;5B9488..94BA;5B93A4..A6;5B8FCA..CE. Reuse checked genericresponse director/callbackorder.|
|P3|Response0→status2;02000000unchanged;03000000/03000004→status0;03000010 writeswhiteboard70targetID;remaining→StartPlace(max3). MarkFailedlowfalse marksaction1 onlyifbothtargetandcurrentcarriedlocated. Exactcurrenthelperstacksuccess/failurecallbackconsumersretained.|M12-042 checked5B94C0..952E/5B93A8..9460. Noextra local Retry table.|
|P4|PlaceRel ctorIDocktype15/tracks7;lowfalse;F0trigger208;FCsavedcarried=-1,100verifychildnull;C0false,C8true,F7false,B9truebecauseoffsets0. Select requirescarryelse03000011;CanStackOnTopfalse→03000004; successDock2,type19,BB0,savesactualcarryFC. Init skipTransformtrue→noTransform;offsetsA/B0 andmethod0.|M12-025/017;checkedOV02F6–8;Select00555434..54D6;Init00554DCC..4E30. SameactualIDockC5/DA/E2GhandlesandSharedCurrentDockowner;F0notpickup207.|
|P5|ActualIDock Init/commonCheck/marker choice/turnandverify/squint/motor gates/timer/carry/free callbacks/destruction reuse checkednativePickupIDockpath withthisexactderivedtuple. C0false means NO VerifyNoObjectchild; C8true meansretainedTurnTowardsObject(target,selectedMarker,Radians0,true,false), then dock. No asyncDockHelper bridge.|M12-017/034/037/040 sharedcheckedP2+PICKUP-CHILDREN;exactStack offsets do notskippreaction/CanStack. NativePlaceRel runner currentlyabsent,sourceintegrationrequired.|
|P6|Verify selectedDock2/3; !actualDockSuccess→04000003; stillactualcarrying→03000017. Else retainedTurnTowardsObject(savedcarryFC,Radians0,true,false),suppressedtracklock. FirstconstructionUpdate: terminalnonzero/nonRUNNINGreturnimmediatelyWITHOUTnormalretire;0/RUNNINGfalls throughsecondUpdate. LaterRUNNINGwaits;terminaldeletechild;failureclearslocatedsavedcarryID andreturnsresult. SuccessDock2 withflag104false→flagtrue,MoveLift(preset0,speed5f),suppressedtracks,returnRUNNING. Afterlift success0. Otherdock→0300001A.|005555B4..5581E, key5556A4..570CdoubleUpdate;555760..57CAfailureclear;5557D0..581Ehighlift. Separately unowned concrete action body proposed in companion proposal; no wholeM12-017claim.|
|P7|CompletionUnion methodprimaryread; boundedtarget/topchainqueriedwith15f. Then TWO Set_objectInteractionCompleted calls: moveat55532A, thenconst&at55533Eafterwritingtargetscalarandcount1/flag0. The constsetter overwrites all5words/count/flag, so finalmeaningful payload is target/count1/false, NOT accumulatedchain. Normalunused-word policy/guard below.|0055515C..55344. Final overwrite independently confirmed by setters75C3F0..436/75C460..4A6. Meaningful target/count1/flagfalse known; approved raw unused-word policy and availability guard below.|
|P8|RecoveryGround isnativeIActiontype18/tracks7/timeout30. Init resetslatch;carrymissing03000011;else snapshotcarryID,Carrying.PlaceObjectOnGround(false) sends44 thencreatesTurnTowardsObject(savedID,carriedmarker,Radians0,true,false),suppressedtracks;bothsendoutcomesbuildverify. ALLbranchesStopAllMotors;successonly reactionlockplaceOnGroundAction(tableC55A21onlyindex8true). Check waitsIS_PICKING_OR_PLACINGrisinglatchtheninactiveandbody-notmoving;returnsverifyresult;failureclearslocatedsavedID. Never readsDockSuccess/carryforcompletion. Destructorremoveslockifstarted,prepschild.|M15-022 completeexistingextraction20261003-R-FIX2-M15-gap1 sections2.5–8;554638/554794..48DC/5549B0..4A3A/5546D0;wire6332A88..2BFE M12-015. ReuseactualC5/B8/carryreleaseandnativeTurnchildren;firmware44latchtimingremainsHARDWARE_ONLY.|

|P9|ClearLocatedObjectByIDInCurOrigin is only GetLocatedObjectByIdHelper(ID,family-1), then ClearLocatedObjectHelper(pointer). Reuse existing shared helper's world/carry/selected-object/cache behavior, including null diagnostic. It does NOT DeleteLocatedObjects or synthesize RobotDeleted notification.|0062712C..140;veneer8CCAAC/word8CCAB8→PLT4B7D58; A known-first and B independent wrapper PASS. Current M11-063 owns downstream queue writer; wrapper is actual newcaller join, not broaderworldsettlement.|
|P10|Drive helper increments attempts beforeconstruction. For type1 AND A/B exactzero, enters ordinary DriveToObjectAction ctor with targetF0,typeF8 and allfourstackargszero. Approach104false skipsangle setter; ForceFCfalse skipsforcedposesfunction. Startswith actualRespondToDriveResult. Nonzero-offsetvector/callback is outside this tuple; do not route Stack through it. SharedDriveToObject.GetPossiblePoses consumes actualtype1 defaultpreactionposes.|005B5824..599A;584A..586Ezero gate;5870..588Ector;5892..58A8flag gates;598E..599AStart. Existing M12-029/022/026 and checkedsharedDrivebody; A+B finiteprimaryPASS.|

## Actual cache5/3 producers and cadence — A independent cross-check of B extraction

The following rows were first recovered by B, then independently checked by A against original instructions/literaldata after individual known.py preflights. This avoids self-checking extraction. Scratch`cache-primary.txt` records A ranges; B report has its primary locators. Existing shared selector/geometry bodies retain their current ownership and standing frontiers.

|Row|Native result|Primary locator / existing owner|
|---|---|---|
|C1|Construct distinct default filters with allowedfamilies1/2. TopNoAxis predicate bound tosp28; BottomNoAxis boundsp20. Actual tablekey5→topfilter,key3→bottomfilter; both install DefaultBestObjectFunction. Each entry owns distinctbest/validIDs/timestamp initializedbest-1,emptyset,last-1f. |0056F724..78E,0056F828..88E,0056FC54..68,0056FD26..48/FD84..FDA2;GOT103EC90/9C/ECC0;literalC589E4/F4 both{2,1};entry00571604..634. Same actualAI+14 cache owner ascheckedpickup0; newfiniteentrybindings.|
|C2|Ensure bottom3 recursively ensures5 FIRST. Top5 dependencysetempty. No arbitrary iteration of allentries or forcedinvalidations. |00571040..10CC;table1023570 +key*8:1023588→105A9B0 initialized4D7086..709A withsoleliteralC589C4=5;key5pair1023598→105A998 initialized4D7042..7058empty.|
|C3|Top5: ifactualcarryID!=-1,locate actualcarried object and accept ONLY pointeridentity withcandidate. Else reuse SAME CanPickupNoAxisCheck as intention0, includingwhiteboardactions0/3,30s,60mm,PI then actualDocking.CanPickUpObject ALWAYS(evenwhiteboardmatch),return !failureANDcanPick. NoAxis still requires flat-on-any-face10deg in sharedCanInteract; it omits separate uprightparentZaxis gate, notflatrestinggate.|0057059E..5CE;tail8CB79C→PLT4ACFC4=00570398;GOT103EC88 previouslycheckedintention0pointsSAMEpredicate. SharedCanInteract63C654..67E/CanPick63C7F0 existingcheckedsource. No closestuprightheuristic.|
|C4|Bottom3 ensures5, excludescandidateID equalstopbest, then DidFailToUse(action1,3f,candidatepose,60f,PI);rejectmatch,requirefamily2,thenactualCanStackOnTop. Predicateorderandactualwhiteboardreceiverretained. |0057062C..630→57154C..15C0;571564..56Aexclude;571580pose;57158C/5983f60f;GOT/globalPI105A990 initialized4D7030..3E;5715A2whiteboard;5715A8family;5715B6CanStack. M15-024/M12-012 bodies + NEWowncacheentry.|
|C5|Timestampgate strictabs(f32(last-now))<bits3727C5AC skipsrebuild; otherwise stampnowBEFOREclearingvalidset. CollectactualFindLocatedMatchingObjects, uniqueSIGNEDascendingIDs; retainoldbestwhilevalid,otherwiseinstalledDefaultBest. Sameclock/dependencygateapplies throughoutInit/tick/recoveryqueries. Noimmediateobservation/pose-invalidationinvented.|005711E4..12B6;5711FCsubtract;571216strictcompare/BMI12B4;57122Cstamp;57124Ecollection;57126Cinsert;57127Cfind;571282retain;57128Cbestfunc. Actualsharedpickup0pathpreviouslychecked; A independently rereadnewuses.|

## Additional own getter fact and bounded caller frontier

Vtableaddresspoint10279C4+2C calls005CA1EA..20C: buildset<int> containing exactly byte134 XOR1, so shippedanytrue returns{0}. Do not guess itsname/meaning as an orientationreadinessgate. Native IsRunnableBase5BD778..8C8 only useseligibilityslots20/24/28 and derivedslot50; the complete actual classentry/tick/stop/callback path above has no2Ccall. A bounded directcaller navigation across behavior/framework exportedbodies found otherinterfaces2C only, no actualIBehaviorrecipient. Any unbuilt externalconsumer remains an explicit inheritedframeworkfrontier, not a provedgloballynoeffectclaim. Exactgetteroutput is recovered ifcalled; no new sideeffect in itsbody. B independently agreesbodyfact and unknownconsumer; final scope disposition is manager-owned.

## Concrete placement completion and approved unused-word policy

P7 full primary setter semantics: `0075C3F0..436` and `0075C460..4A6` select variant0 and copy20bytes plus count/flagu16. Pack`0075B4F0..536` sends all5int32 slots then countbyte/flagbyte. At`0055532A` the native getter first writes its computed target/topchain; at`0055532E..33E` it unconditionally overwrites it with a second concrete payload: word0 targetscalar7C, count1,flagfalse. The chain computed earlier is not the final API list. Normal filter scratch reuses the unused four slots, including a phone stack address; these bytes must not be represented as recovered zeroes or FFFFFFFF.

**Operator-approved compatibility policy2026-10-28: “Approve zero unused ID slots.”** Preserve exact meaningful targetID/count1/false; normalize the four unused IDs tozero. Decision document: `re-analysis/jobs/trial-m13/SPARKS-STACK-1-COMPLETION-POLICY.md`. Formal policy record/inventory work remains manager/Claude-owned; normalized unused slots are COMPATIBILITY_POLICY, not EXACT_SOURCE. This applies only when selectedDock is explicitly known2or3 AND the actual target located lookup succeeds, so those meaningful fields and the normal concrete action are established. Do not use this policy to invent unavailable meaningful fields for bad/unselected/uninitialized paths. On those branches mark the full payload unavailable, visibly name the source gap or M1-034 where original undefined behavior is proved, retain the independently defined internal/scalar result/callback flow, and do not synthesize success or a fallback packedU32 publication. Existing full typedcarrier/availabilityguard pattern from checkedpickupP2 is reused; no silent padding or rawphoneaddress serialization is claimed exact.

## Proposed owners and production verification reach

Companion `re-analysis/jobs/trial-m13/PROPOSAL-SPARKS-STACK-LIFECYCLE.md` proposes the whole currently unowned exact configured SparksStackBlock lifecycle, plus a separately unowned concrete PlaceRel Verify/completion scope for manager allocation. No IDs/statuses are assigned. Current full title/status/evidence quotations above show why existing narrow factory/placement-parameter/helper/Init/needs records do not own these complete paths. Formal manifest/inventory/approval changes remain outside this extraction.

Existing whole M12-042 is a potential candidate only after its entire helper title is implemented and independently verified, including all source-required parameter and result recipients. This exact Stack tuple alone does not establish other nonzero-offset Drive1 consumers. M12-028 contains pyramid producers; M15-022 includes AtPose composition; M15-059 contains other activity hooks. None is proposed settled from a partial Stack caller. Existing M12-012 bottom gate is source-backed and reused without manufacturing a new owner for its body. Other broad M12/M7/M8 records remain under their current statuses.

Meaningful production-path verification should exercise actual shipped factory/configured owner27/manager scheduling, native cache5/3 and its dependency/timestamp retention, initialcarry/top choice, real Pickup/Drive1/PlaceRel helper/component and ActionList runners, exact active docking/B8/DA/C5/carry/markers/visual43 inputs, successfulturn/highlift and all defined failure/retry branches, corrected bothvalid/bottom-invalid gate and one retarget cancellation/order, groundrecovery's nonignored childfailure and resultless reentry, explicitstop/replacement/unrelatedactionpreservation, objective40→optionalplain21A→needs43 order, actual Sparked counter→end/getout/report/cleanup and approved completion payload. Tests must label supplied admission, scene, firmware and explicitpost-null deselection; expected targets, results, order and payloads come from the cited source, never output-derived expectations or direct callback shortcuts. No tests are run by extractor.

## Final bounded source disposition

No finite source UNKNOWN remains inside this exact shipped own-class/helper/action/Sparked path after the approved gate correction and completion unused-slot policy. Exact unnamed getter+2C output is exposed as{0}; ownrefreshhook+7C exposesUpdateTargetBlocks; their external/unbuilt consumers are named inherited framework frontiers, with no invented readiness/orientation effect. Raw admission/post-null parent picker and sibling bodies, M8-019 immediate scheduling switch, M15-071 reward amount/rawreport, full upstream44/vision/calibration ownership, planner/geometry/math/audio frontiers and firmware44status timing remain inherited as explicitly named in adopted shared packets. This is not an acceptance or hardware claim for those systems.

A source recovery is complete for the finite configured scope; **independent B whole-packet verdict pending** and no builder release is made by extractor. Original undefined/terminating branches use M1-034 smallest-unit visible skip, never fabricated success. Operator-approved departures are limited to their recorded gate correction/unused-slot decision; no official status is changed here.
