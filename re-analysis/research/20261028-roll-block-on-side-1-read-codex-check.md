# ROLL-BLOCK-ON-SIDE-1 independent source check

2026-10-28. Independent reviewer requested DEFAULT/DEFAULT (both overrides omitted); operator-reported default gpt-6.1-terra is not runtime telemetry. Actual runtime configuration unavailable. PROJECT_STATE.md read first, followed by AGENTS.md, AUTONOMY.md, job, BEHAVIOURS/REVIEW and research README. Source-only phase: no build, test, hardware, git mutation, production or official metadata edit. Only this research check file is written.

Native ELF independently SHA256-checked: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Python313 and Capstone/lief read actual instructions/data. known.py preceded new traces (an initially incomplete address query returned no match and was corrected to 0x005BC200/0x005BCCAA before tracing). Decompiler output is not evidence.

## Independent primary observations

| Join | Native or asset evidence checked directly | Finding |
|---|---|---|
| Exact definition | `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/rollBlockOnSide.json` | RollBlock class, RollBlockOnSide ID, needs RollACube, display behavior.RollBlock, unlock RollCube. Rotation and alwaysStreamline are absent. No behavior inferred from ID. |
| Derived factory/default | 0x0059CF86 allocation0x138; 5C85A2 base ctor; 5C85DC movs r1,#1, JSON bool/get/asBool then 5C85F4 store+128; literal key5C8634 | Same derived receiver as Hiking; absent rotation field means true. |
| Real cache recipient | 5C8CB2 load+128; 5C8CB6 intention6, 5C8CBE conditional7, 5C8CC2 GetBestObjectForIntention | This exact configured instance requests cache7. Earlier checked cache7 axis filter/specialized picker applies, including its inherited geometric inputs. |
| Spark/streamline input | 5BC200..21E binds parser output to+D9. 5BCCAA..C2 sets+D8 from active spark!=55 and XOR soft1 | Neither a direct Sparked owner nor hard-spark state follows from freeplay asset membership. Actual manager input remains required. |
| Helper arguments | 5C8734..40 tests D9/D8; 5C8744 loads0 or 5C8934 PI/2 (literals5C8A1C=0/5C8A18=3FC90FDB); 5C87F0..FA triggers1F7/1F6; 5C8804 movs r0,#1 and5C8808 stack store;5C8814 helper factory | Same target, actual Robot/behavior receiver, rollToUpright=true and callback/trigger/angle parameters as Hiking. Booltrue does not come from config rotation value. |
| Score/light joins | 5C8884..8E score bits3F4CCCCD; 5C88A6 trigger19;5C88AC customfalse;5C88B8 zero custom payload;5C88CE SetBehaviorStateLights(false) | Exact same shared light receiver/arguments; reuse checked light contracts rather than duplicate per-ID proposal. |
| Retarget | 5C8A6C..78 target exists/state0;5C8A9A..A0 axis change;5C8ABA distance function,5C8ABE success1,5C8ACA/AD2 compare to14400;5C8ADC..AE2 StopActing(false,false);5C8AE6..B22 pre-dock/success or native retarget | Same current MAIN-to-target distance and callback state as Hiking, no new host scene mutation/timer/forced completion. |
| Success | 5C8B52 state1;5C8B8E mood;5C8BA4 axis refresh;D9/D8 suppress trigger1FB otherwise ctor5C8BD8;5C8C08 score;5C8C12 objective2/true;5C8C1A NeedActionCompleted(0) | Same order and actual configured needs resolution as Hiking. No unconditional Finish is evidenced here. |
| Actual parent references | PlayAlone JSON chooser behaviors[4], Socialize behaviors[3], flatScore1 in each | Asset reference proves input, not parent admission or choice. PlayAlone Simple25/30; Socialize Simple5/300/30 plus minimum Social mood0.5, requirements and special derived effects remain parent boundaries. No sibling/chooser audit undertaken. |

## Inherited scope boundaries

Reuse the independently checked Hiking Roll class/cache/helper/composite/Roll/Place and state-light contracts only through these matching native joins. Carrying/recovery invokes the same actual Place/Ground path, with Ground completion remaining explicitly unowned. Reuse the same pending coherent Roll/Place/state-light record proposals; no new per-config ID is justified. Preserve the Roll GetCompletionUnion5567E0..EE / setter75C400..410 undefined unused-ID full-publication guard under M1-034, defined local fields/callbacks, and imported atan2f body equivalence only. Source operands/width/order, action scheduling and framing are still exact obligations. No new departure is inferred from head-only record scope.

Parent admission/competition/interlude and Socialize derived on-run effects are named outside inputs; inherited unavailable readiness/progression/face/speech/geometry/planner/firmware/full-report inputs remain visible MISSING. No assertion that absent inputs cannot affect the behavior is made. Existing broad factory/cache/framework/M12 claims are not narrowed or marked complete by one configured instance. No official status change, inventory approval, hardware acceptance or implementation claim follows.

## Current ownership quotations

The following current title/status/evidence objects were read before the ownership statements above. They are claim snapshots, not primary source and not edits.

```json
[
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
  },
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
    "id": "M13-026",
    "title": "GetAbsoluteHeadAngleToLookAtPose and Robot::ComputeHeadAngleToSeePose",
    "status": "EQUIVALENT_IMPLEMENTATION",
    "evidence": [
      "0x0054B428..0x0054B55A, 0x00518344..0x00518780 (pools 0x00518744, 0x00518780)",
      "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q3"
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
      "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
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
      "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
      "re-analysis/research/20261012-m8-play-animation-actions-check.md addition 4..5: AddAction(IActionRunner*, bool, bool) 0x0054EC7C -> AddAction(function, bool) 0x0054ED90 (empty predicate; child inherits +0x57; compound name appended); the trailing boolean is never loaded.",
      "re-analysis/research/overnight/20261014-ov20-21-check.md: C1 0x0052CECA..0x0052CFA2; C2 0x0054E990..0x0054EA86; C3 0x0053FDB8..0x0053FE1C; C4 0x0054EC7C..0x0054EEAC; C5 0x0054F4F8..0x0054F54C; S1-S8 0x0054F70C..0x0054F992, 0x0054F5A0..0x0054F696, 0x0054F560..0x0054F59E; P1-P5 0x0054FAB0..0x0054FBCC, 0x0054EC56..0x0054EC7A; D1-D4 0x0054F050..0x0054F130, 0x0054F170..0x0054F1BC, 0x0054F270..0x0054F3EA, 0x0054EB78..0x0054EC2A, 0x0054FC2C..0x0054FC66; the seven log sites; AlreadyPrepped call sites 0x0054F0C0, 0x0054FB3C."
    ]
  }
]
```

## Coherent packet verdict

**READ PASS** for `20261028-roll-block-on-side-1-read.md`, raw SHA256 `d448a4ff7a7839916e178d6b3c018fb48446b5bdb6243655360d352952413444`, 54459 bytes. All21 embedded CURRENT title/status/evidence objects match the current manifest exactly, including array order. This is a finite source-contract check, not official approval or a behavior-finished claim; only root releases implementation.

Read S1-S16, P1-P4, inherited packet index/correction precedence and complete ownership/leftover conclusions. Primary caller/default/enum/recipient checks support exact configured reuse. Direct enum checks confirm decimal99=0x63 behavior (XOR0x80 givesE3, base1033330/cell10336BC), class49=31(base10331F0/cell10332B4), needs39=27(base1033D40/cell1033DDC), unlock9(base10340C0/cell10340E4); default executable0C store5BBCA4. Source-only shorthand clarification distinguished table bases from individual cells and decimal from hexadecimal; no contradiction remained and no builder corrective batch occurred.

Additional known-first primary Socialize inspection supports the bounded parent claims:5B0268 calls base;5B0282 null alone enters state dispatch5B02DC, nonnull samecurrent5B0288 returns, different chosen logs and returns5B02DA. Objective handler5B07C6..CE special state2 needsobjective0C, not Roll2;5B081C mapfind plus5B082A..38 decrement/erase,5B085A..62 gates empty/state4 and5B08AC transitions5. No no-effect conclusion is drawn from absent RolledBlock requirement in the asset. Actual parent-state/population/face/scheduling remain named MISSING and no parent audit is required for this release.

No new unsupported native behavior, frozen inventory contradiction, whole-existing-record completion or defined departure found in the finite packet. Existing pending generic Roll/Place/state-light ownership proposals are reused additively. Unowned Ground completion/full-publication bridge, Roll full-public UB guard, imported atan2f-body-only equivalence and inherited production-input boundaries are retained explicitly. Meaningful implementation verification should reach the exact configured instance, actual manager/cache/helper/action/firmware-handler/verification/callback path and state-light/API consumers; supplied scene or firmware inputs must remain distinguishable from engine-derived results. No hardware output or source fidelity is proven by a test count alone.

Source-only clarification rounds are not reviewer behavioral correction batches: builder not released at this checkpoint, reviewer-required code-fix rounds0. No .NET execution, build host use/release, changed-source hash freeze or implementation verdict is implied by this source PASS.

Reader corrected asset line locators P1/P2 before freeze (PlayAlone interlude160–171; Socialize1–118). Final raw hash above includes that citation-only selfcheck; behavior contracts unchanged.
