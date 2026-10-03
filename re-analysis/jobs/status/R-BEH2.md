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
