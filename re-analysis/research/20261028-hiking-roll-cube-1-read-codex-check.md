# HIKING-ROLL-CUBE-1 independent source check

2026-10-28. Request: `jobs/trial-m13/HIKING-ROLL-CUBE-1.md`. Requested reviewer model/effort: DEFAULT/DEFAULT, overrides omitted. Actual runtime telemetry unavailable; operator-reported default is not independent telemetry. No code/test edits, build/test execution, hardware, git mutation, official metadata or Claude-check writes.

Native ELF independently hashed SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. PROJECT_STATE, AGENTS, AUTONOMY, job and research instructions read. `known.py` preceded newly traced native areas. Independent dumps in `.scratch/hiking-review-*.txt` are direct Capstone/lief instruction/data observations, not decompiler evidence.

Verdict: **READ PASS** for coherent finite packet `20261028-hiking-roll-cube-1-read.md` after source-only clarifications below. This is authority-6 research checked against primary instructions, not official inventory approval/settlement, implementation acceptance or hardware validation. Only the manager releases the builder.

## Corrected configuration boundary

Actual `obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/hiking/Hiking_rollCube.json` specifies class RollBlock, ID Hiking_RollCube, needs RollACube, display behavior.RollBlock, unlock RollCube; it omits `isBlockRotationImportant`.

The native ctor constructs JSON boolean TRUE at 0x005C85DC, gets field name bytes `isBlockRotationImportant` at 0x005C8634 via 0x005C85E4/EA, reads asBool and stores +0x128 at 0x005C85F4. Therefore this configured instance uses TRUE, not the old hand-built registration's false. UpdateTargetBlock 0x005C8CB2..C2 chooses intention7 for true, intention6 for false. The virtual intention set 0x005C8CEC..D18 uses the same 6/7 condition. This is an implementation-candidate correction, not a contradiction of a frozen official record.

Hiking's actual activity asset has BehaviorsOnly/Simple60s/cooldown15s, Hiking driving triplet, CalculateInterestingRegions input, Scoring8.5 for this ID, current-behavior bonus1 and strict-priority interlude. SparksGatherCubes embeds a GatherCubes delegate whose StrictPriority list places this ID after DriveOffCharger/KnockOverCubes. These assets are inputs, not guaranteed admission or competition wins. Reuse already checked common strategy/scoring/interlude recipients; retain their actual unavailable readiness/picker/delegate inputs visibly. No host60s cancellation or manufactured Sparked owner follows from these files.

## Independently checked native lifecycle/cache/helper facts

| Path | Primary instructions/data and conclusion |
|---|---|
| Runnable/init | 0x005C8654..74 invokes virtual target update; valid target OR +0x84 action tag is runnable. 0x005C867A clears +0x129; located-target failure returns1, valid target captures up-axis then starts performing and returns0. |
| Axis capture | 0x005C86AE..E4 reads located target's own pose rotation, GetRotatedParentAxisZ, stores +0x12C. |
| Performing | 0x005C8712 sets state0; +D9 OR +D8 selects max face turn0 literal0x005C8A1C (vldr0x005C8744), else PI/2 literal0x005C8A18 (vldr0x005C8934). Parameters carry callback setting +129 and animation triggers0x1F6/0x1F7. CreateRollBlockHelper at0x005C8814 passes bool TRUE independently of +128. Then SmartDelegateToHelper(success/failure), score0.8, target-state light25(false). |
| Update | 0x005C8A6C..D2 requires located target, state0, changed up-axis, successful ComputeDistanceSQBetween, and distanceSQ >14400 literal0x005C8B28. Equality and unordered/NaN skip (`ble`: N!=V on unordered). Then recapture axis, StopActing(false,false), if +129 true enter success else retarget; missing new target returns2, valid new target restarts performing. Ordinary tail calls base UpdateInternal. |
| Success | 0x005C8B52 state1; mood event then recapture axis; no hard/soft spark constructs trigger0x1FB action with loops1, bool1, byte0,60f,bool0. Then score0.8, objective2/true, NeedActionCompleted(0) (configured own need), in that order. Spark flags suppress success action only. |
| Stop/reset | 0x005C8C94..A6 resets targetID=-1 only; no separate derived Resume override. Derived deleting dtor0x005C8CDA invokes base dtor. |
| Derived callbacks | Data vtables0x01027858/898/8D8 slot+20 yield0x005C8D6E (+129=true),0x005C8DD6 (success tail),0x005C8E3C (failure marks current located target WB failure3). ARM veneer0x008CC23C+literal resolves PLT0x004B35B8 TransitionToRollSuccess. |
| Cache7 | Ctor0x0056F9B4 stores filter in stack+14;0x0056F9F4..FA18 installs AxisCheck. Families data0x00C58A0C contains2,1.0x0056FC72..7C maps intention7 to this filter.0x0056FDE4..FDFC binds RollBlockBestObjectFunction, not plain nearest. |
| Cache6 dependency | 0x0056F932 stores filter stack+18;0x0056F97C..996 installs NoAxisCheck;0x0056FC6A..70 maps6.0x0056FDB8..FDCC binds same specialized picker. AxisCheck delegates here. |
| No-axis eligibility | 0x0057066C..B2 checks whiteboard failure3 using60sec/30mm/PI; pose-state1/2; IsRestingFlat(0x3E32B8C2); candidate relative to MAIN, IsPoseTooHigh(2,15,.5); reject candidate contained in any actual stack height>=3 (0x0057074C..7C4). PI pointer0x0105A990 is initialized by0x004D7034 movwFDB,7038 movt4049,703C address,703E Radians ctor. Do not read NOBITS as missing/zero. |
| Axis eligibility | 0x005707F0..83C root-flattens with0x004DF628, invokes no-axis predicate, then requires rotatedParentZ axis!=3.0x004DF65C..67C confirms root copy or FindRoot/GetWithRespectTo path. |
| Specialized picker | 0x0057084E..902 walks valid IDs; preferred subset requires located object, Docking.CanPickUpObject==true and own-pose parentZ axis!=3. Nonempty preferred subset passes to DefaultBestObjectFunction; otherwise original set passes. No arbitrary first-ID or guaranteed nearest shortcut. Shared default picker/whiteboard/pose/configuration bodies remain their existing checked recipients. |
| Helper construction | 0x005B98D4..9974 copies target, parameters/callable, bool into+121; sets+120=1 and attempt+124=0. ShouldCancelDelegates0x005B9A12=false; active-update0x005B9D80 no-op. |
| Helper initial choice | 0x005B9A44..68 any carriedID!=-1 delegates putdown. Invalid target logs then status0. Cannot-roll goes UnableToRollDelegate. The pre-action visual-verification call at0x005B9AE0 uses type4 and0/0. Result0 goes StartRollingAction (0x005B9BD4); nonzero builds DriveToHelper with native type4/default flags and optional upright approach angle. |
| Drive delegate callbacks |0x005BA81A starts rolling and returns helper status;0x005BA886 re-runs DetermineAppropriateAction and returns status. Actual callable tables0x01026134/174 are bound by0x005B9B44/9BE4. |
| Putdown |0x005B9DA6 creates actual PlaceBlockHelper, with success0x005BAA82 storing+120=1, re-running DetermineAppropriateAction, returning1; failure0x005BAAEA returns0. No fabricated place completion. |
| Unable-to-roll fallback |0x005BA074..82 tests intention0 for target; on-top search0x005BA136 passes30mm, families2/1, onTop=true. Valid pickup target wins; else pickup on-top if found; else log CantInteract and preserve current helper status. No failure/mark store occurs in that last branch. Both pickups use params Count/0/false/false and real helper; success callbacks0x005BA8F2/9BA delegate putdown then return helper status; failures0x005BA956/A1E return0. |
| Rolling action |0x005B9F0A..28 unsigned count>=3 marks failure thenstatus0; else clears+120, increments BEFORE action admission.0x005B9F36..52 constructs DriveToRoll(false,0f,false,Radians0,true);+121 invokes RollToUpright. Applies callable, pre-face triggers and max-turn params, then response-animation wrapper. Three helper attempts are not three guaranteed outbound dock messages. |
| Response/result |0x005BAC3E maps04000003 to user-facing1, success0 to2, others0.0x005BA7C8 ignores CANCELLED02000000; success0 sets helperstatus2; all other results restart rolling via ARM0x008CBF3C=>PLT0x004B267C.0x005BA764 marks current located target failure3. |

## Roll completion undefined-read confirmation

Independent entry-to-tail inspection confirms a real original uninitialized read, not stable phone pointer semantics:

1.0x00556744 allocates0x30 stack bytes. It initializes only halfword sp+14 at entry. On ordinary dock5..7, not-carrying path,0x005567C2..D6 initializes *first* buffer sp+00..14: target, four -1 words, count1/false.
2.0x005567DC move-setter publishes first buffer into union(tag0).
3.0x005567E0..EE ALWAYS overwrite same union via const-setter from *second* buffer sp+18. Only target sp+18 and halfword1 sp+2C are written. On the ordinary path, sp+1C/20/24/28 have no prior initialized writes. Warning branches can reuse pieces of local diagnostic state, which does not define the ordinary path or all four words.
4.Const-setter0x0075C3F0 sees tag0 from first setter and0x0075C400..410 copies five words+halfword (22bytes) including all four indeterminate IDs. Earlier -1s do not survive this copy.
5.Serializer0x0075B4F0..536 writes all five words (loop through offset0x10) regardless of count; then byte14 and bool15. Therefore count1 does NOT exclude undefined IDs from full public payload.

Confirmed defined scalar fields are target/count1/false. Under root's M1-034 standing-rule disposition, skip/log the smallest affected full-public-payload publication; preserve defined scalar/local result, native callback/delegation and downstream completion. Do not invent four zero/-1 words or extend PlaceRel's separate policy. This guard must survive cache/compound/proxy/callback/API forwarding without stranding the helper. Owning Roll action record must name0x005567EE/0x0075C404 and the visible skip; shared M8-015 owns publication machinery, not the concrete payload producer.

## Ownership and settlement limits

Current title/status/evidence quotations follow below; official records were not changed. Existing M12-016 owns ONLY the three-attempt cap. A newly recovered class, helper and action are not covered by that narrow exact record. M7-018 owns factory/config coverage; one Roll implementation cannot settle all178configs/79classes. M13-028 is Flip, not Roll. Newly unowned Roll class/helper/action/completion require coherent proposals, without invented official IDs. Shared M13-014 IDriveTo, M8-001 base, M8-015 queue/publication, M8-017 compound and M15-063 teardown stay partial unless their entire current claims are independently completed; this job must not narrow them for settlement.

```json
[
  {
    "id": "M12-016",
    "title": "A roll gets three attempts",
    "status": "EXACT_SOURCE",
    "evidence": [
      "0x005B9EF8 StartRollingAction; 0x005B9F0A ldr.w r0,[r4,#0x124]; 0x005B9F0E cmp r0,#3; 0x005B9F10 blo 0x5b9f20; 0x005B9F16 blx 0x4B26AC (MarkTargetAsFailedToRoll)",
      "there is no charger helper in the build; the charger mount's one attempt is M13-008"
    ]
  },
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
  },
  {
    "id": "M13-028",
    "title": "FlipBlockAction::Init and DriveAndFlipBlockAction's constructor guards",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "0x0055EED6..0x0055EEFA, 0x0055EF3A..0x0055EF70, 0x0055ECE0, 0x0055EEC6, 0x0055EF7E, 0x0055B258..0x0055B264, 0x0055B38C",
      "re-analysis/research/20260929-R-VIS-verify-M12-fix2.md objections 2, 3, 9",
      "0x0055F074..0x0055F17A FlipBlockAction::CheckIfDone; 0x0055EF3A..0x0055EF4A; 0x00548A54..0x00548A78 (fix-round-3 verifier)",
      "0x0055F074..0x0055F208 CheckIfDone; 0x0055ED54..0x0055EDA0 destructor (round-4 verifier, .scratch/m12v5_cid.txt)"
    ]
  },
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
  },
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
  },
  {
    "id": "M8-015",
    "title": "ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "0x0053E6A8..0x0053E6E0; 0x005409E2..0x00540A40; 0x00541146..0x00541238; 0x0053FAB2; Current Cancel then delete; pending direct deletion preserves existing result. Cancelled0x02000000 except NotStarted0x02000001; stop before unlock, watcher then broadcast. Existing B-ACTIONS candidate must be checked for full teardown effects and concrete descendants.",
      "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u00e2\u20ac\u201cT9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
      "P3/P4 full ActionList/ActionQueue/IAction lifecycle: ActionList 0x0053F580..0x0053F5F2, ActionQueue 0x0053F5F4..0x0053F70C, IActionRunner 0x00540370..0x0054063E, clock/timeout 0x00540D4A..0x00540E80, CheckIfDone 0x005549B8..0x005549F4 (20261006-M1M2-missing-triage.md P3/P4).",
      "re-analysis/research/20261012-m8-play-animation-actions.md rows C2..C7, L14..L23, D5, S2..S4 and re-analysis/research/20261012-m8-play-animation-actions-check.md (the +0x57 gate on L17/L18/L20, the DeleteActionAndIter guard set at queue+0x10, ActionWatcher Start/EndUpdating 0x00540386/0x0054057E/0x00540630, the UpdateInternal status string 0x00540D46..0x00540F1C).",
      "re-analysis/research/overnight/20261014-ov18-m8-015-check.md: L1-L9 0x00540370..0x0054063A, 0x00540D1C..0x00540F9C, 0x00540750..0x005407C0, 0x005409DC..0x00540A44; Q1-Q7 0x0053F580..0x0053F708, 0x0053DCE4..0x0053DD92, 0x0053E24C..0x0053E2F6, 0x0053DF2C..0x0053DFC8, 0x0053DE10..0x0053DE5C, 0x0053E690..0x0053E700; D1-D6, D12 0x0053F9E4..0x0053FB06, 0x00541084..0x00541286, 0x00541BB4..0x00541C04, 0x0054187E..0x005418DE; T2 List Clear 0x0053D916..0x0053D93A, 0x0053F938..0x0053F950, 0x0053FB5C..0x0053FB82; QueueAction front end 0x0053D93C..0x0053DAC8; position table 0x0053DA24..0x0053DB70.",
      "re-analysis/research/overnight/20261015-ov56-58-check.md: teardown mechanics from OV56: ~Robot order 0x005110EE..0x00511156; AbortAll 0x00511952..0x0051198C (0x0063BE2A..0x0063BE36, 0x00517DFE..0x00517E0A); ActionList::Cancel clearing gate and slot order 0x0053DE18..0x0053DE54; ActionQueue::Cancel 0x0053E69A..0x0053E6FA (early exit 0x0053E6E4); QueueNow/QueueAtEnd 0x0053E25A..0x0053E36E; DeleteActionAndIter 0x0053F9E4..0x0053FAB2 (re-entrancy set, INTERRUPTED/no-interface gates); result codes 0x0053FDBA..0x0053FDD4, 0x005409E2..0x00540A40; Clear lock-out 0x0053D91A..0x0053D938, 0x0053DCEA..0x0053DD00, 0x0053DCAA, 0x0053DF40, 0x0053F93C, 0x0053F970..0x0053F9AC; ~ActionList double Clear 0x0053D8F6..0x0053D90E; IBehavior StartActing reject-delete 0x005BDC04, Init failure 0x005BCCC6..0x005BCD06."
    ]
  },
  {
    "id": "M8-017",
    "title": "The compound action runtime: ICompoundAction, CompoundActionSequential and CompoundActionParallel (construction, AddAction naming, update/result tables, retry, completion store, teardown), as used by the go-to-sleep sequence and other compounds",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "0x0052CEA2..0x0052CFC0; Parallel/sequential child execution, ignore-failure predicate and completion/track interactions remain higher framework obligations; M1 owns only factory handoff and NOW/retries0 submission.",
      "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u00e2\u20ac\u201cT9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
      "re-analysis/research/20261012-m8-play-animation-actions-check.md addition 4..5: AddAction(IActionRunner*, bool, bool) 0x0054EC7C -> AddAction(function, bool) 0x0054ED90 (empty predicate; child inherits +0x57; compound name appended); the trailing boolean is never loaded.",
      "re-analysis/research/overnight/20261014-ov20-21-check.md: C1 0x0052CECA..0x0052CFA2; C2 0x0054E990..0x0054EA86; C3 0x0053FDB8..0x0053FE1C; C4 0x0054EC7C..0x0054EEAC; C5 0x0054F4F8..0x0054F54C; S1-S8 0x0054F70C..0x0054F992, 0x0054F5A0..0x0054F696, 0x0054F560..0x0054F59E; P1-P5 0x0054FAB0..0x0054FBCC, 0x0054EC56..0x0054EC7A; D1-D4 0x0054F050..0x0054F130, 0x0054F170..0x0054F1BC, 0x0054F270..0x0054F3EA, 0x0054EB78..0x0054EC2A, 0x0054FC2C..0x0054FC66; the seven log sites; AlreadyPrepped call sites 0x0054F0C0, 0x0054FB3C."
    ]
  },
  {
    "id": "M15-063",
    "title": "AIComponent teardown residue: concrete std::function and IHelper types, BorderSegment pointee, AIBeacon/PossibleObject/FailureInfo dtors, BehaviorEventAnimResponseDirector D0",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "re-analysis/research/overnight/20261016-batch-check.md: writers of BehaviorHelperComponent +0x10/+0x28, ProcessInfo and cache-entry callables, filter predicates; BorderSegment element+0x28 pointee.",
      "re-analysis/research/overnight/20261016-ov105-107-108-answer.md: helper component 0x00569D38..0x00569D7C; IHelper base dtor 0x005B5F20..0x005B600C; subclasses (DriveTo PoseBase +0x114, SearchForBlock Camera +0x110 and E2G 0x44 handle); BorderSegment pointee MemoryMapData (dtors 0x0067E508, 0x0068072C, 0x00684A34, 0x00684C04, 0x00684B0C); AIBeacon 0x0056A2F4..0x0056A31A; PossibleObject 0x0056CAC4..0x0056CAFC; FailureInfo 0x0056CAFE..0x0056CB62; BehaviorEventAnimResponseDirector D0 0x0056D968..0x0056D986."
    ]
  }
]
```


## Whole-packet disposition and action cross-check

Reader packet H1-H13/R1-R15 independently reviewed as one finite path. Direct primary cross-check additionally covered Roll constructor/EnableDeepRoll/SelectDockAction/Verify, DriveToRoll constructor/upright pre-action arguments, and narrow Gather update effects. The checked shared recipients remain inherited bounded evidence, not newly certified whole subsystems.

- SelectDock 0x005568B8..8F0 stores target relative to MAIN parent, or03000004; 0x0055691E Block cast required; 0x00556956 saves top marker. Height subtraction at0x0055695A with literal0x00556B08=0x42076666 uses ordered >33.85 to return03000004; carried ID!=-1 returns03000017; otherwise0.
- Verify0x00556B7C starts result03000017; accepts dock5..7, else result+3=0300001A. Docking+5 false=>04000003. Otherwise raw carry present preserves03000017. Real retained sequence is MoveHead(-15deg,2deg tolerance,accel0), VisuallyVerifyObject(target,saved marker), suppress-track-locking true. New sequence Update once then common existing Update again when Running; actual child resultRunning survives. Terminal deletes retained sequence. Visual failure enables deep roll and returns0300001D (initial03000017+6); success0. No timer or fabricated visual result is sourced.
- DriveToRoll0x0055C9DC..CA18 base arguments are type4, distance0f, callerfalse, angle0f, false, Radians0, true.0x0055CA4C..CAA6 creates actual Roll child(boolfalse), adds withfalse and sets proxy tag. Upright0x0055CC9A..CCD6 calls GetCurrentPreActionPoses with type4, actual MAIN, empty marker set, actual obstacles, null pointer,0,false.0x0055CCF4..CD18 selects first face5 marker match, then0x0055CEAC..CECE ComputeVectorBetween(object,preaction) and atan2f(y,x);0x0055CF10 returns true after cleanup. No-poses/no-bottom paths log then returnfalse, preserving unrestricted approach. The libm numerical boundary stays named uncertainty unless independently covered by an approved applicable policy; a prior different-function libm-only settlement is not blanket permission.
- Narrow Gather update0x005AF286 guards one-shot+78;0x005AF2A8 true AreAllCubesInBeacons sends finish lights for connected cubes, external objective0B if available, needs14, sets+78=true.0x005AF2FE..386 uses actual active beacon and located known poses for progress/freeplay lights. Packet correctly leaves unavailable actual parent/delegate/WB/light inputs visibly MISSING and does not silently pretend these effects cannot happen while Roll runs.

Source-only clarifications resolved before implementation release: configured isBlockRotationImportant default TRUE/cache7 (legacy manual false is wrong); spark angle0/ordinary PI2 literal correction; actual specialized picker; PI static initialization; explicit two float DriveToRoll arguments; response callbacks and ARM veneer resolutions; concrete Roll completion UB; CantInteract nonterminal status. These are reader/checker questions, **zero builder reviewer-fix rounds**, since no builder/code review existed at this source checkpoint.

CantInteract specifically0x005BA332..37E only logs, then cleanup0x005BA4C6..52C. IHelper.Initialize0x005B6710..14 sets status1; Roll activeUpdate0x005B9D80 is no-op. Preserve its inert Running status and absence of failure memory. A later external target-axis change can drive class retargeting; an unconditional original hang has not been established merely from no immediate internal work. Do not silently normalize it to failure or infer the M1-034 hang guard from idle waiting. Confirmed full-payload UB remains separately covered by M1-034 as above.

No fresh frozen-inventory contradiction or defined departure found. No existing broad record newly completed. Proposed new Roll own-path record is reasonable because configured class/helper/action/completion is unowned beyond the narrow three-attempt record and shared recipients; allocation remains manager/operator work. No partial old record settlement or invented ID. Parent admission, competition, delegate inputs, foreign pose/math/vision/firmware/report boundaries remain explicit. Meaningful future regression must enter configured factory/manager/cache and actual helper/action/wire/firmware-handler/verification/callback path, with independently controlled scene and firmware inputs, and check publication suppression without stranding local completion. Source checks alone do not prove that implementation exists.


## Finite existing libm-policy applicability check (root follow-up)

**PASS: the existing unshipped-system-libm policy applies to the numerical body of this same external atan2f call only.** This updates the earlier conservative numerical-MISSING qualification; it does not expand an action's exact-source claim or create a new exception.

Primary binding: Roll upright call0x0055CECA is `blx #0x004A4510`. Independent ELF dynamic-symbol inspection gives `atan2f`, shndx0/value0 (undefined import); the PLT relocation at0x004A4510 is exactly atan2f. This is the same external symbol/PLT identified by current settled M13-026, not a second shipped Roll arithmetic body.

Current M13-023 unresolved(e) explicitly answers: “bionic libm does not ship, so its calls are EQUIVALENT_IMPLEMENTATION under AGENTS.md; the operands, widths and order stay exact.” Current M13-026 settlement explicitly bounds only bionic atan2f0x004A4510 and sqrtf fallback under M13-023(e), as M5-021/M4-052. Current M4-052 settlement likewise bounds only phone atan2f/MathF.Atan2 under ruling4.2 and says everything else exact. The original20261011-m5-settling-pass section4 item2 starts with M5 tanf/cosf/sinf, but the later current accepted M4/M13 records and answered generic libm item establish that the operative ruling is library-body scoped, not restricted to one head method or M5.

For Roll: source selection of first bottom-marker pre-action pose, actual ComputeVectorBetween(object,preaction), single-precision y/x operand extraction0x0055CEC6, argument order atan2f(y,x), and float output store0x0055CECE remain exact obligations. Only host libm atan2f numerical body is equivalent; no permission for different inputs, double atan2 plus cast, association, fallback, target selection, scheduling, timing or packet changes. A future coherent new Roll record may be proposed EQUIVALENT_IMPLEMENTATION with this narrow named boundary, rather than incorrectly EXACT_SOURCE for the entire numeric path. No official status/approval/inventory edit made. No broader math audit, implementation or tests performed.

Current full title/status/evidence snapshots consulted for this follow-up:

```json
[
  {
    "id": "M13-023",
    "title": "Footprint corner ordering: SortCornersClockwise (centroid, atan2f, __sort4/sort3 ties, Quadrilateral slots) and the cv::Rectangle fallback (empty input, bounds, center); corner writers read and unreachable/out of scope",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "0x00502DD8, 0x004EF78A the corner getters; 0x0084AAE0, 0x008494E0, 0x008499B0 the rotation-matrix path; 0x004E7EFC SortCornersClockwise; 0x004E6778 InitFromPointContainer",
      "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md open questions; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md 'What stays open'",
      "re-analysis/research/overnight/20261014-ov08-m13-023-check.md: S1-S6 0x004DF7BE..0x004DF84C, 0x004E7EFC..0x004E7F9C, 0x004E8260..0x004E8342, 0x004E847E..0x004E84E8, 0x004E8508..0x004E8642, 0x004E7E9C..0x004E7EFA; R1-R3 0x004E81B4..0x004E825E, 0x004E676A..0x004E67C4. Corner writers: MatPiece 0x00502DD8..0x00502ECE, FlatMat 0x004F6B58..0x004F70C0 (no constructor caller outside CloneType); CustomObject 0x004EF790..0x004F0340, clone 0x004F0470..0x004F04DC (callers only BlockWorld DefineCustom*/CreateFixedCustomObject handlers 0x0061E1EC..0x0061E6xx and DockingTestSimple 0x005CC1C0)."
    ]
  },
  {
    "id": "M13-026",
    "title": "GetAbsoluteHeadAngleToLookAtPose and Robot::ComputeHeadAngleToSeePose",
    "status": "EQUIVALENT_IMPLEMENTATION",
    "evidence": [
      "0x0054B428..0x0054B55A, 0x00518344..0x00518780 (pools 0x00518744, 0x00518780)",
      "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q3"
    ]
  },
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
]
```
