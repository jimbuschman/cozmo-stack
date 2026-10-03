CLAIMED claude-sonnet 2026-09-30

## Progress (2026-10-02)

- Pulled; claim line above. Read the audit (research/20260929-audit-M7-M8.md), Codex's pre-extraction and its check.
- Step 1, row checks and extraction (three read-only passes, started):
  - check 1: the audit's M8 rows (M8-001/002/003/005/006/007/009/012) -> research/20261002-R-BEH2-check1-M8-rows.md
  - check 2: the audit's M7 idle rows (M7-005..010, 013, 016, 017) -> research/20261002-R-BEH2-check2-M7-idle-rows.md
  - extraction gap pass 1 (M7-021 +0x58 reader scan, M7-018 unchecked rows, M7-014 call-site enumeration, M7-015, M7-019) -> research/20261002-R-BEH2-M7-gap1-extraction.md
- Plan: batch 1 = M8-framework (lifecycle, repetition, scored selection, PlayAnim, track locks, manager tick), batch 2 = idle path (route through UpdateLiveAnimation, retire IdleBehavior's generator), batch 3 = the remaining M7 records.
- Check 2 done (research/20261002-R-BEH2-check2-M7-idle-rows.md). The audit's idle rows hold, except M7-008 bullets 1 and 2 (+0x194 clear and picking/placing return with no decrement, so only the MovementComponent flag, locked tracks, a countdown above 0 and lift-carrying decrement). Additions:
  - float32 widths in Mood and the streamer clock;
  - the 1e-4f dt floor in MoodManager::Update;
  - the history sample in Emotion::Update;
  - the LookAt float association is wrong in the C#;
  - GenerateEyeShift clamps the limits to 17 and 12;
  - no pusher of ProceduralLive (0x198) was found in the engine, so the pusher is a RECOVERABLE_GAP and the rebuild does not invent one.
- Check 1 done (research/20261002-R-BEH2-check1-M8-rows.md). M8-001/002/003/006/009 hold. Corrections to the audit:
  - M8-005: "num_loops 0 plays nothing" is true only for the loop path (zero or two-or-more triggers; StartSequenceLoop, 0x5c02a8). A single trigger takes StartPlayingAnimations (0x5c0160) with numLoops=[+0x128], and 0 loops forever.
  - M8-007: TriggerLiftSafe ORs LIFT into the mask when carrying and robot+0x355==0 (0x544714), so DisableAnimTracks{2} is sent then; otherwise the mask is 0 and nothing is sent.
  - M8-012: no global arbiter gate, but each trigger with a non-zero disable-lock count is skipped (0x5a359a).
  - Omissions: StartActing calls ScoredActingStateChanged(true) (0x5bdb54), so +0x104 clears at every action start; refused resumes stamp +0x118 = now+15.0f (0x41700000); the scored switch has three gates (0x5a3068..0x5a3074); the tick starts with the activity Update (slot 8, 0x5a2f84); StopAllMotors/CompletelyUnlockAllTracks on a reaction switch; TryToResume queues a head/lift restore (0x5a2b48..).
- Correction A2 written into inventory/M8-framework.md (check 1's corrections folded in) and M8-framework re-approved (`--check` passes).
- Batch 1 (M8-001/002/003/005/006/007/009/012) handed to the implementer; the extraction pass for M7 is still running.
- Extraction gap pass 1 done (research/20261002-R-BEH2-M7-gap1-extraction.md), kept for batch 3. Key findings:
  - M7-021: the "no behaviour-changing +0x58 reader" claim is contradicted. `Robot::Update` 0x00513F6A..0x00513F9C builds robot+0x4C from activity + BehaviorID + "-state" and sends it to VizManager::SetText and SetSdkStatus(Behavior). The other readers are log-only. Unread (RECOVERABLE_GAP): the SdkStatus send path, the SetText body, the writer of the skip counter at 0x01051010.
  - M7-018: loader, container, factory read, with the full 79-way table in the report. Absent/unknown behaviorClass writes to cerr and returns 0 (Bouncer). `Robot::Robot` builds two BehaviorContainers.
  - M7-014: 31 SmartDisable sites enumerated, all five tables match; sites the record omits (Init/Resume "SparkBehaviorDisables", KnockOverCubes, PopAWheelie, DevTurnInPlaceTest, FeedingEat, BuildPyramid, RequestGameSimple, RamIntoBlock); three non-static tables are RECOVERABLE_GAP.
  - M7-015: ReactToPickup gating; writers are `Robot::CheckAndUpdateTreadsState` (0x0051208E) and `Robot::SetOnCharger` (0x00511C14). The OffTreadsState classifier body is not decoded (M10 boundary).
  - M7-019: ReactToCliff and ReactToPickup StartAnim read in full. 0x1A9/0xE6 is the pickup fallback, not the face path.
- Batch 1 built (suite 2649/2649 at that point). Verifier FAILED it: 12 blocking findings (research/20261002-R-BEH2-verify-batch1.md): switch order (current nulled before Init), SetRunningAndResumeInfo's omitted effects, IsRunnableBase gates unwired, HandleActionComplete clearing +0x104, the StartActing std::function at +0x98, StartActing's gates, an invented animation end-reason, lock taken after clip resolve, scope disposed when Stop is skipped, an invented non-stepped Resume, the UI-request class test order, float widths in the penalty graph. Its facts are folded into inventory Correction A3 (re-approved). The implementer is fixing them; the fixed hunks get a second verifier pass, then the full suite once and the commit.
- Batch 1 verified: rounds 1-3 FAILED (12, 2 and 3 blocking findings, each fixed), round 4 PASS (research/20261002-R-BEH2-verify-batch1.md). Full suite 2661/2661, `fidelity.py --check` clean. Records M8-001/002/003/005/006/007/009/012 stay IMPLEMENTATION_GAP, unresolved "built, awaiting strong verification:" with what remains. M8-008: the M7 callers ignore the 0x03000018 result (recorded in `unresolved`).
  - Queued (non-blocking): no test for a failed turn child in the parallel compound or for the UI-request class-test order; MoodState holds emotions as double where the engine loads float (M7-013); the base vtable+0x20/+0x24/+0x28 default true (a dozen M10/M15 classes would be refused off treads: blocking once those records settle); the stale "ActionRunnerWho" note in PROJECT_STATE.md (the constant no longer exists).
  - For R-VIS: none (no edits to FaceBehaviors.cs, ManipulationBehaviors.cs, Manipulation/**).
  - Files outside M8 touched: Cozmo.Robot/Animation/CozmoAnimations.cs, Motion.cs, Conformance/Reactions.cs, Behavior/CubeReactions.cs, Mood.cs (DecayGraph.At), and three tests that asserted contradicted behaviour (CorrectionTests, FreeplayTests, DerivedStateTests).

## Batch 1 committed: 603d459 (M8-framework; not pushed)

## Batch 2 (idle/face/mood: M7-005..010, 013, 016, 017), started 2026-10-03
- Correction A2 written into inventory/M7-behaviour.md (check 2 folded in); M7-behaviour re-approved. Implementer running.
- Batch 2 verified: round 1 FAIL (2 blocking: SetParam's lazy default-set and clamp; CORE-001's wire lock), round 2 PASS (research/20261002-R-BEH2-verify-batch2.md). Full suite 2679/2679, `fidelity.py --check` clean. M7-005..010, 013, 016, 017 stay IMPLEMENTATION_GAP, "built, awaiting strong verification:". IdleBehavior, IdleParameters and IdleTests are retired; the manifest `location`s of M7-004..010/016 moved to the Animation files.
  - Queued: Workouts.cs (R-VIS) still refuses trackDelta (its comments are stale, MoodState.GetHistoryValueTicksAgo now exists; it has no production caller); BEHAVIOR_LAYER.md:182-183 and jobs/status/B-M7.md:33-34 still say "no face-only mode"; nothing asserts the QuietLiveIdle delegate is restored; the production clock is Environment.TickCount64, not BaseStationTimer's origin; DesiredFaceDistortion has no source; the ProceduralLive pusher has no record yet (RECOVERABLE_GAP in prose).
  - For R-VIS: Manipulation/Workouts.cs lines 37-42 and 121-123 (wire `e => mood.GetHistoryValueTicksAgo(e, 60)` when a caller exists).

## Batch 2 committed: 9600ef1 (idle/face/mood; not pushed)

## Batch 3a (M7-003, 014, 015, 019, 021), started 2026-10-03
- Rows: inventory/M7-behaviour.md Correction A3. Implementer running. Then 3b (M7-012/020 MoodState message + live HandleActionEnded caller; M7-002/018 loader, factory, FistBump, ReactToSparked) and 3c (M8-011/013/014, M8-004).
- Batch 3a verified: round 1 FAIL (1 blocking: the severe-need seam threw out of Tick), round 2 PASS (research/20261002-R-BEH2-verify-batch3a.md). Full suite 2717/2717, `fidelity.py --check` clean. M7-003, 014, 015, 019, 021 stay IMPLEMENTATION_GAP, "built, awaiting strong verification:". The seam [[robot+0x264]+0x30]+0x14 returns 3 (none) with a once-only MISSING (no SevereNeedsComponent writers exist: SetSevereNeedExpression 0x00572e48 from NotifyCozmoWakeup 0x00572e00 and BehaviorPlayAnimOnNeedsChange::StopInternal 0x005bfef8; clearers listed in the report).
  - Files outside M7 touched: Sensors.cs (ChargerEvent raised from SetOnCharger edges, M4), Behaviors.cs (M8-006's unset seam). For other owners: the nine classes with a recovered lock table still call Scope.DisableReactions() in CubeGameBehaviors.cs, ManipulationBehaviors.cs, ObjectBehaviors.cs (R-VIS/M12-15).
  - Queued: stale "no broadcaster" comments at CliffPickupBehaviors.cs:86 and ReactionStrategies.cs:341-342 (the PlacedOnCharger strategy may not receive the new ChargerEvent); no test for the SetCarriedObjectAsUnattached wiring; NaN deadline and t == 500 tests need setters.

## Batch 3a committed: ea1ff4a (M7 reactions; not pushed)

## Batch 3b (M7-012/020 MoodState + HandleActionEnded; M7-002/018 loader, factory, FistBump, ReactToSparked), started 2026-10-03
- Batch 3b verified: round 1 FAIL (4 blocking: FistBump state-2 branch, scan wrap, terminal order, the BehaviorID miss path threw), round 2 PASS (research/20261002-R-BEH2-verify-batch3b.md). Full suite 2752/2752, `fidelity.py --check` clean. M7-012, 020, 002, 018 stay IMPLEMENTATION_GAP, "built, awaiting strong verification:". M7-020's live caller is NOT built: the stack has no ActionList/ActionWatcher/RobotCompletedAction (what would build it is in the record's `unresolved`).
  - For other owners: M10 ReactionTriggerStrategyFistBump/Hiccup/Sparked are unbuilt (the map rows stay unbound); M13-020 a live PanAndTiltAction; M4 EnableLiftPower/EnableHeadPower bodies; SparksFistBump is now bound and can run live with FistBump's seams unset.
  - Queued: Implementable() still hand-builds Hiccup, ReactToObstacle and six Feeding* PlayAnims (shadowed by the container's copies, dead in production); Behaviors.cs:67 Enum.TryParse looser than the engine's name map; the reaction-map failure line lacks its event name; no test for RequireVerifiedFace = false.

## Batch 3b committed: 95f747d (MoodState, loader/container/factory, FistBump, ReactToSparked; not pushed)

## Batch 3c (M8-013, M8-014, M8-004; M8-011 waits for extraction), started 2026-10-03
- Correction A4 in inventory/M8-framework.md (M8-framework re-approved). An extractor pass reads the BehaviorHelperComponent runtime in full (M8-011) and the unidentified pose base 0x004EA398 (M8-014) -> research/20261002-R-BEH2-M8-gap1-extraction.md. The implementer builds M8-013/014/004.
- Batch 3c verified: round 1 FAIL (1 blocking: tag 53's 32-bit float store), round 2 FAIL (1 blocking: the default tilt formula was a guess; the engine takes the largest-|value| component of row 2 of the rotation matrix, 0x5507a0..0x550854), fixed and the fix read against the verifier's disassembly sequence by the manager (the rewritten test includes the counter-example). Full suite 2773/2773, `fidelity.py --check` clean. M8-013, 014, 004 stay IMPLEMENTATION_GAP, "built, awaiting strong verification:". M8-011 is not built yet (batch 3d).
  - For R-VIS: AIWhiteboard.cs (hold the possible-object list, make OffTreadsStateChangedAtSec/RecordOffTreadsStateChanged float, give AIBeacon the +0x10 float last-failure time, make Init's subscriptions deliver the three handlers; the M8-014 manifest location still points at AIWhiteboard.cs).
  - Queued: the A4 text mis-attributed the pose base (UpdateBeaconRender calls 0x004DF628; Robot::GetPose() 0x004EA398 is used in ConsiderNewPossibleObject: A5 corrects it); remaining NotSupportedException seams outside this batch: BehaviorManager.cs:632/649 (UI-request), Behaviors.cs:196, IBehavior.cs:555 (SmartDelegateToHelper); the round-2 tilt fix had no separate verifier pass (the manager compared it to the verifier's sequence).

## Batch 3c committed: 061a201 (M8-013/014/004; not pushed)

## Batch 3d (M8-011 BehaviorHelperComponent, IHelper base, SmartDelegateToHelper), started 2026-10-03
- Batch 3d verified: round 1 FAIL (3 blocking: IBehavior::Stop's own helper stop at 0x005bd0f4 was omitted; the empty-stack StopHelperWithoutCallback threw where the engine compares the stale bottom pointer; a lock inversion between the component lock and the behaviour's _gate), round 2 PASS (research/20261002-R-BEH2-verify-batch3d.md). Full suite 2811/2811, `fidelity.py --check` clean. M8-011 stays IMPLEMENTATION_GAP, "built, awaiting strong verification:" (the six concrete helper classes are unread and not built; the nine C# behaviours that use DockHelper/SearchForBlockHelper are not rewired).
  - Queued: ACompletionCallbackMayStartTheNextAction is a timing-sensitive test (a probabilistic stress test since batch 1); a wrong comment at SteppedBehavior.cs:680-689; the concurrency test never reaches the component-to-_gate path.

## DONE 2026-10-03 (round 1 of R-BEH2)
Commits (local, not pushed): 603d459 (batch 1, M8 lifecycle/scoring/PlayAnim/locks/manager), 9600ef1 (batch 2, idle/face/mood), ea1ff4a (batch 3a, M7 reactions), 95f747d (batch 3b, MoodState/loader/FistBump), 061a201 (batch 3c, ExecuteBehavior/whiteboard/default score), plus the batch 3d commit below.
Records: none settled (Sonnet job: CHECKLIST section 6). All 19 demoted records and the M7/M8 records this job built are IMPLEMENTATION_GAP with unresolved "built, awaiting strong verification:" and what remains: M7-002, 003, 005..010, 012..021 (except 011), M8-001..009, 011..014 (M8-010 unchanged). Left out of scope: M7-022 (non-live RECOVERABLE_GAP). Open items needing other owners (each in its record's `unresolved`): the ProceduralLive pusher (RECOVERABLE_GAP with no record yet), M7-020's ActionList/ActionWatcher, the engine-to-game sink and the game-to-engine channel, SevereNeedsComponent writers, the six concrete helpers, FistBump's seams, M10's FistBump/Hiccup/Sparked strategies, R-VIS's AIWhiteboard.cs/Workouts.cs.
