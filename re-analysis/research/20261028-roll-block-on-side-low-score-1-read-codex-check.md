# ROLL-BLOCK-ON-SIDE-LOW-SCORE-1 independent source check

2026-10-28. Reviewer startup requested DEFAULT/DEFAULT, both overrides omitted, fork_none. Operator reports default gpt-6.1-terra; actual runtime telemetry unavailable. PROJECT_STATE read first, followed by AGENTS, AUTONOMY, job, BEHAVIOURS, REVIEW, research README and extractor instructions. Read-only source phase; only this research check is written. No .NET, production/test/official metadata, hardware or git mutations.

Native artifact independently hashed SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Python313/lief/Capstone decoded primary ELF data and instructions. known.py preceded new native traces (RollBlock, ActivityBehaviorsOnly, 0x005BC488, 0x005B3F54, 0x005B4014). Decomp/index used only for navigation. Initial wrong asset path and Windows rg-glob attempts returned no evidence; corrected explicit freeplay paths/read with --no-ignore.

## Primary observations

| Join | Independently checked evidence | Finding |
|---|---|---|
| Literal configuration | OBB config/engine/behaviorSystem/behaviors/freeplay/rollBlockOnSideLowScore.json, lines1-7 | Exactly RollBlock/RollBlockOnSideLowScore/RollACube/behavior.RollBlock/RollCube. Rotation, streamline, executable and own flat score fields absent. |
| Exact enum | Native table010336C0 -> pointer00C1A745 string RollBlockOnSideLowScore; index XOR80 yields100 | Distinct ID100, not ID99 RollBlockOnSide or Hiking. |
| Derived defaults | 005C85A2 base ctor;005C85DC movs r1,#1;005C85EA JSON get;005C85F0 asBool;005C85F4 store+128 | Same RollBlock receiver; absent rotation means true. |
| Actual cache consumer |005C8CB2 load+128;005C8CB6 intention6;005C8CBE conditional7;005C8CC2 native GetBestObjectForIntention| Exact same cache7 consumer as checked RollSide/Hiking; no orientation inferred from name. |
| Score source | Parent playWithHumans.json behaviors[2] score7;005BC4CE JSON key lookup,005BC4D8 absent skip,005BC4DC asFloat,005BC4E0 store+100 | Parent entry input7 is explicit. Own absent score remains separate default0; no unconditional winner/readiness claim. |
| Actual caller | ActivityBehaviorsOnly ctor005A718C calls IActivity; vtable010252D0 relocation01025308 binds IActivity::GetDesiredActiveBehaviorInternal;005B3A70 reads chooser+24,005B3A80 vslot28,005B3A94 call | Actual configured parent uses common chooser route. This proves call contract, not full parent admission/competition. |
| Parent inputs | playWithHumans.json type BehaviorsOnly; Scoring; strategy typePlayWithHumans cooldown30/rejection120/exponent1.3; StrictPriority interlude list | No Simple timer, synthetic chooser, Sparked ownership or false sibling readiness imported. |
| Objective |005C8C0E movs r1,#2;005C8C10 movs r2,#1;005C8C12 BehaviorObjectiveAchieved then005C8C1A NeedActionCompleted(0)| Same objective2/true and order; broader subscribers/admission effects remain UNKNOWN/MISSING. |
| Narrow parent subscriptions |005B3F5C tag82/store3F74, external interface vslot28 at3F6C/call3F86;005B401C tag3A/store4034,vslot2C at402C/call4046| Distinct interface/tag inputs observed. Neither an absent handler name nor these two calls proves all objective effects absent. |

## CURRENT record snapshots

The following title/status/evidence fields were read from the current 713-record manifest before ownership conclusions. These are quotations, not official edits.

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
  },
  {
    "id": "M8-004",
    "title": "The engine's behaviour score default is zero; this stack's code-built behaviours carry 1.0/5.0",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "The engine has no in-code default score. IBehavior::IBehavior 0x005bbb74 writes zero to the flat score at +0x100 and to +0x104 (0x005bbd1c mov.w r8,#0; 0x005bbd28 strd r8,r8,[r4,#0x100]). EvaluateScoreInternal 0x005beec2 returns that float when the mood-scorer vector at +0xdc is empty (0x005beec2 ldr r2,[r0,#0xdc]!; 0x005beec6 ldr r3,[r0,#4]; 0x005beed4 vldr s0,[r0,#0x24] -> +0x100). The only non-zero writer is ReadFromScoredJson 0x005bc488 from the optional flatScore key (0x005bc4e0 str.w r0,[r4,#0x100]); there is no hard-coded default.",
      "Selection is by the activity chooser or the reaction map, not a flat ranking: the app's scored behaviours are the ones an activity config lists with a flatScore, and ScoringBSRunnableChooser 0x0060a44a skips a score <= 0 (0x0060a452). The policy review of 2026-09-29 (verdict C) found this divergence; @cozmo-verifier re-read 0x005bbd28 and 0x005beec2 and confirmed it.",
      "This stack's ScoredBehaviorEntry.FromJson already defaults flatScore to zero, so the config-driven chooser path matches. The divergence is on behaviours built in code: PlayAnimBehavior's constructor defaults score 1.0, ReactBehavior 5.0, SteppedBehavior.Score 5.0, PlayArbitraryAnimBehavior.Score 1.0 and SingingBehavior 1.0. Those feed this stack's BehaviorManager.ChooseAndSwitch, which ranks every behaviour by IBehavior.EvaluateScore; the engine has no such path."
    ]
  },
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
]
```

## Scope and verdict

**READ PASS** for the bounded exact configured own-path packet. Reviewed source report SHA256 `a71fe1c4fc44d73a9ae5a810263de385fdce7e85b1a65b983072816823c73ae4`; all 25 current title/status/evidence snapshots independently compare exactly to the current manifest. No required source correction batch. This verdict permits root consideration of a build release; it is not a build, execution, hardware, fidelity-status or finished-behavior verdict. No whole-record settlement is inferred from the configured delta. M12-016 already owns the three-attempt cap; M12-050 owns PlaceRel, not Ground or Roll completion. The same pending generic Roll, PlaceBlockHelper and state-light scopes should be reused only where actual receiver/arguments match. Preserve unowned Ground API bridge, Roll M1-034 full-publication guard, imported atan2f-body-only equivalence and inherited producer/input frontiers. No new per-configuration record or policy is proposed.


Additional fresh primary checks after packet arrival: known.py 0x00609FEE preceded ScoringBSRunnableChooser::ReloadFromConfig. At0060A00E it parses the entry ID;0060A022..26 fetches scoring;0060A032 resolves that exact manager behavior;0060A03A..3E passes scoring/true to that receiver, then0060A05A TryAddBehavior. This supports report P3 and distinguishes parent input7 from initial own default0. The native map pair at0076C218/228 stores preceding OnSide63;0076C21A resolves LowScore string;0076C23C/24C stores LowScore64. These agree with the separately checked enum table.

Shared S3–S16 applicability is reuse of already independently checked Hiking/RollSide contracts with matching concrete class/receiver/arguments, not fresh certification of every inherited implementation. The source report explicitly retains the complete correction precedence and producer frontiers. No contradiction, unsupported policy, new per-ID owner or newly complete whole existing record was found. Required code/test correction batches in this source phase: zero; source clarifications are separate and no .NET execution has occurred.
