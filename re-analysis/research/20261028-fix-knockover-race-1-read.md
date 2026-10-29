# FIX-KNOCKOVER-RACE-1 — read-only diagnosis (2026-10-28)

Answers `jobs/trial-m13/FIX-KNOCKOVER-RACE-1.md`. Requested reader configuration: gpt-6.1-sol / medium / fork_none; no backend identity inferred. Read PROJECT_STATE first, AGENTS, AUTONOMY, QUEUE, REVIEW and research README. Checkout supplied by manager: manager-2, HEAD ee667d7a; current manifest 718 / imported main1268d9b3; production revision19b61cf. No .NET, git operation, hardware, production/test or official metadata edit performed. Only this research report written.

## Result

**UNKNOWN local async fault; no proven race or semantic fix.** The existing artifacts establish a fault in the legacy public KnockOver action chain, not which inner operation threw. This cannot yet be classified between implementation defect and harness defect. It is not evidence of a hardware-only unknown, blocked external behavior or native recovery gap. Those categories cannot explain a C# exception without its type/stack. Passing isolated/full runs do not close it.

The concrete diagnostic gap is historical *test-output truncation*, despite R1 already preserving the full exception in the behavior trace. Current1057 improves only the missing ignored-drive-line failure;1059 remains abbreviated Assert.Contains and can still hide an exception thrown after the drive. No new production fault logger, retry, timeout increase, assertion weakening or swallowed exception is justified.

## Preserved failure evidence

All paths below are relative to `.scratch/worktrees/` unless stated otherwise.

- `codex-knockover-cubes-1/.scratch/knockover-cubes-1/affected-1.trx`: actual failed event start `2026-10-28T09:49:14.7484733-05:00`, duration `00:00:00.7461263`, test `NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates`, assertion1059. Error is `Assert.Contains() Failure: Filter not matched in collection`; collection contains `start reach for block 7`, reach Success, GrabAttempt, start DriveAndFlip, then `DriveAndFlipBlockAction(7) faulted: System.Aggrega` followed by xUnit truncation. The **inner exception and its stack are absent**. Corresponding `affected-1.log` last-write 2026-10-28 09:49:36 local reports1633 passed/1failed/0skip. The TRX does not preserve a second full copy of b.Trace. `HANDOFF-KNOCKOVER-CUBES-1.md:16` identifies the same run, isolated1PASS and affected2PASS; no fix claimed. Exact failing pre-diagnostic C# revision is not independently stamped by that TRX; do not equate the eventual frozen SHA file with its earlier test image.
- `codex-m13-manager-2/.scratch/test-full-attempt-1.log:2160..2168`, last-write2026-10-26 11:14:52 local: full6274passed/1failed, old assertion976, DriveAndFlip -> Abort; no inner exception because this precedes R1. `jobs/trial-m13/HANDOFF-2.md:5,16` identifies tested commit b1c1169d4a8291b9f00b5d007a86dcc7912e12aa and the once-only full gate. `CM13-03-gate-investigation.md` retains isolated current/baselinePASS and PlannerNull/RuntimeNull observations. This is an earlier failure, **not** the remote584a29b5 gate.
- `research/20261028-pop-a-wheelie-1-claude-check.md:28..30` is the report of the remote first full gate on584a29b5: assertion1059, AggregateException; isolated5/5/fullrerun6661PASS. Its original failed `.scratch/test-full.log` is not present in the investigated local historical worktrees. No inner exception inferred.
- Current SparksPop full6666PASS is a non-reproduction only. No new reproduction was run by this reader.

## Actual current execution chain

C# below is primary evidence for what this stack/test currently executes. Native addresses are existing record evidence, not a fresh source extraction or whole-path certification. The failing fixture uses **legacy KnockOverCubesBehavior**, not NativeKnockOverCubesBehavior/the newly configured Sparks owner.

1. `NavigationTests.cs:1012..1070`: installs SignalTestContext, creates Rig, renders three stacked cubes at260mm, obtains stack, sends Idle state, constructs public KnockOverCubesBehavior. Explicit inputs are marker-rendered vision, turn stand-ins, successful fake firmware movement, and the injected up-axis observation after a150mm/s flip line. No LoadPlanner, resource argument, real robot or lattice scheduling assertion. Test has `reach: unit`; no test-specific Fidelity trait. Manifest M12-035/M13-028 explicitly name this test; comments name M13-014/M12-035. Its configured-behavior sibling tests do not replace this route.
2. `NavigationTests.cs:104..125` RunToEnd makes animations manual, assigns `ActionTaskRunner=SignalTestContext.Schedule`, starts behavior and loops behavior.Update -> rig.Pump -> rig.Frame/Idle -> AdvanceBehavior. The ms argument is unused; modelSteps10000 is a harness guard. This finding is not a proposal to alter any timing/guard.
3. `ManipRig.cs:19..185`: standalone M borrows native path/follower/docking services, private whiteboard/configuration adapter; actionclock snapshots transport time once at1s (`ActionClockSec`, volatile). N5 is already present. Pump advances transport/engine clock independently. Real DriveToPose/DriveStraight runners below still read Engine.Timer for their IAction timeout; do not claim N5 freezes every action deadline. DrivingAnimationsForTest supplies GeometryOnlyDrivingDependency and planner-obstacle erase is a fixture no-op. M.Wait is immediate. M.TurnOverride directly changes Angle/Head, sends State and returns completed true; Vision turn overrides similarly replace physical turns.
4. `CubeGameBehaviors.cs:69..119`: OnStart locks reactions, obtains native tallest stack, resets state/subscribes ObjectObserved; reach runs M.TurnTowardsObjectAsync (fixture override), object-to-robot distance check, DriveStraight at60mm/s when needed, then GrabAttempt trigger. Missing animation assets take the existing no-assets completion path. Native reach anchor0x005C3254..0x005C33A0 is M13-014, still partial.
5. `CubeGameBehaviors.cs:121..166`: creates DriveAndFlipBlockAction(bottom7,maxTurn=pi/2), starts another legacy async action: fixture turn -> flip.RunAsync -> success-only real Task.Delay(0.5s,cancel) -> callback. Result category0 reacts; NoPreActionPoses writes whiteboard/finishes; category4 retries then blind flip; other categories (including Abort from a fault) log failure/finish. The label 'DriveAndFlipBlockAction(7)' owns this **entire async lambda**, including its leading turn and trailing delay. It does not locate a throw within flip.RunAsync.
6. `FlipBlockAction.cs:377..420`: DriveAndFlip's no-carry branch calls DriveToObjectAction(Flipping) with L1/L2 installer; awaits drive then appends its trace, ignores its result, awaits last-face turn and object turn with ignored ordinary results, awaits FlipBlockAction, returns its result. Trace copying occurs *after* awaits; a child throw can prevent its accumulated trace from reaching b.Trace. The inapplicable carrying branch directly flips. Field0x100 defaults null; closestOnly defaultsfalse; no test setter changes either. Throwing nearest/second-nearest guards exist at340/345 but **no evidence says either threw**.
7. `DriveActions.cs:570..660`: object lookup -> InitHelper -> installed pose function -> DockPreActionPoses.Get using copied robot pose/obstacles -> nearest pose -> current labeled yaw-only goal adapter with origin retained -> real DriveToPoseAction -> final fixture object turn -> CheckIfDone. Flip's inPosition remainsfalse, so ordinary drive result is DidNotReachPreActionPose. Its failure is ignored by DriveAndFlip, per existing AddAction ignoreFailure evidence0x0055B370. Both this adapter and helper/WaitForLambda are already recorded partial paths, not new causes.
8. `DriveActions.cs:163..198,368..380`: host DriveToPose.Init initializes fixture driving handler, obtains native planner set, calls PathComponent.StartDrivingToPose with current host goal/start/planning callbacks; RunAsync registers terminal callback, QueueAction(Now), cancellation registration then awaits `_done`. No M.LoadPlanner here; Planner initiallynull, so NativePlannerSet has no lattice runtime. Short planning/PathComponent still execute; exclude only the lattice worker from this route, not the whole planner component. Engine path emits ClearPath/append/ExecutePath; `ManipRig.Pump/ReactMessages/FollowPath` decode outbound data, supply Started/Completed/odometry and ack reliable frames.
9. `CozmoEngine.cs:2070..2140`, EngineRobot.Update line1325, `Actions/ActionList.cs:121..151`, `IActionRunner.cs:283..290,430,536`: incoming fake messages are handled under engine tick, states/path flags feed action checks, ActionList updates queue/runner and runs completion callbacks with fullresult. Head/lift/path handlers and queue deletion clean up the actual runner. `_done` uses RunContinuationsAsynchronously. WatcherEnding also completes a stored result when cancellation/deletion bypasses normal update callbacks. Await continuations captured by SignalTestContext execute when fixture drains its queue; this fixture is not a default Task.Run production-scheduling stress test.
10. `FlipBlockAction.cs:161..214`: Flip finds located target, copies robot pose, calls DockPreActionPoses, computes norm+20, starts embedded initial45mm lift then DriveStraight150. Initial lift is real `Motion.SetLiftHeightAsync` -> BuildLiftAction -> ActionList Now; `Motion.cs:830..878` returns MoveAction.Done; firmware MotorActionAck + in-position state from Rig/Idle complete it (M4-016). DriveStraight (`DriveActions.cs:1022..1119`) queues actual IAction runner, emits custom native line, checks real path state, awaits full completion. Flip polls compound with Task.WhenAny(compound,real10msdelay), marks objectUnknown on terminal result; while RUNNING copies robot/relative pose and can queue parallel92mm carry lift below40mm. Its counted unmodeled cancel/reaction-lock/destructor boundaries remain explicit. No evidence establishes which substep was reached in the failed run.
11. `FaceBehaviors.cs:20..45`, `SteppedBehavior.cs:65..74,654,1181..1192`: RunAction cancels old token, uses supplied scheduler; R1 continuation on TaskScheduler.Default logs full `t.Exception.ToString()` (or cancellation observation), maps failed task toAbort, Post enqueues callback. Behavior.Update drains a snapshot of pending work; stale-cts guard prevents replaced completion. b.Trace is a locked snapshot (`SteppedBehavior.cs:167`). Log's Step event can run on the completion thread; diagnostics subscribers must use the existing DiagnosticJournal/another safe sink (R1 fixed hardware subscribers; plain List is unsuitable). Native action completion and legacy behavior completion are distinct boundaries.
12. Reaction sets configuration dirty, selects success from tipped set, logs objective/needs, triggers reaction then Finish (`CubeGameBehaviors.cs:195..221`). Test manually calls b.OnObserved with the changed up-axis; vision Frame also handles World.ObjectObserved synchronously and can change object poses. RunToEnd calls Stop(Completed) after Update returnsfalse; KnockOver.OnStop unsubscribes world observer then cancels pending token. `SteppedBehavior.Stop:708..720` stops helper/acting. Assertions run **before** using-var disposal. Rig.Dispose detaches MessageQueued, disposes standalone M then Vision then Robot; M removes only installed callbacks and stops/disposes owned planners, preserving borrowed services for robot teardown (`ManipulationSystem.cs:326..338`). SignalTestContext.Dispose restores previous context, intentionally leaves late-post signal alive and executes no queued callback after scope end. A teardown fault from this test's final disposal cannot retrospectively produce the earlier logged action fault; earlier overlapping work from another owner is unproven.

## Proposed next step — diagnostic/test work only, needs manager release

No semantic production fix is supported. First retain exact bytes/version of current NavigationTests, Rig, SignalTestContext, ActionBehavior and relevant production files. Ask the manager/remote owner for the failed full trace if already retained; do not block local diagnostics on that request.

A finite experiment, preserving all outcomes:

1. One historical affected-filter run, using preserved `codex-knockover-cubes-1/.scratch/knockover-cubes-1/affected-filter.txt`, on frozen current code with console detailed + TRX. Confirm assets execute the test; retain process exit, actual test count, hashes and each raw result. This is reproduction evidence, not retry acceptance.
2. If no failure: at most20 separate invocations of the unchanged target filter, keeping normal collection/parallel settings; save every result. Stop this experiment on first failure. If all pass, explicitly report0/20 reproductions, UNKNOWN; do not run until green or declare fixed. No extra full gate merely to hunt this failure without a separate manager decision.
3. First preserve1059's exact predicate/result semantics and supply a full-trace failure message;1057 alone does not cover a later fault. If root releases test-only instrumentation, attach a thread-safe DiagnosticJournal to b.Step before Start and write the complete b.Trace/journal in failure-finally to an ignored unique artifact (plus pump/model step, phase, thread id, action/transport/engine clock values, current path IDs and actual task status). Inspect only safely completed properties of the ALREADY existing AsyncInvocationStarted/task observation; do not await to acquire an unavailable observation. It is first-only and may identify reach, not DriveAndFlip; record that limitation. A faulted task's Exception can be inspected; do not touch Result on an incomplete/faulted task. Observe faults without waits, status injection, replacing results or introducing a new invocation hook. Do not add production hooks, new delays or alter callback scheduling yet. Preserve original assertions exactly.
4. Once the actual exception/throw site is known, design a controlled-scheduling regression for that demonstrated boundary (e.g. only if the stack proves an ownership/deletion interleaving, control that real boundary). Reproduce failure before correction and verify same real route after. Never fabricate an action result, winner, goal or terminal event to simulate the symptom. Native behavior changes then require known.py first and primary disassembly for the actual changed step; internal-only ownership/error handling requires an unchanged observable-result argument. Independent review precedes release to builder.

The exact blocker is absent inner exception/context, not inadequate logging inside R1. Build/test work remains manager-owned; reader ran none. No commit or settlement proposed; no official record changed.

## Current manifest quotations

These are current title/status/evidence/test fields, read from manager's current718 manifest, not remembered historical claims. Whole records remain partial where stated. The selected test is named by M12-035/M13-028; M13-002's settled constants do not certify its entire wrapper. Full exact evidence arrays are reproduced below to make that boundary reviewable.

### M12-022 — IMPLEMENTATION_GAP

Title: DriveToObjectAction::InitHelper's DriveToPoseAction goal

Evidence:

- 0x005590AE objectPose.GetWithRespectTo(robot.parent, local_5c)
- 0x005590E6..0x005590FE delta = robot.xy - local_5c.xy; 0x00559100..0x00559160 normalize; 0x005591C2 vldr s2,[sb,#0x84]; 0x005591E4 vmul delta *= distance
- 0x00559220 vadd goal.y = delta.y + local_5c.y; 0x00559224 goal.x = delta.x + local_5c.x; 0x00559230 goal.z = robot.z
- 0x00559232 eor r0,r1,#0x80000000 / 0x00559236 eor r1,r2,#0x80000000 / 0x0055923A blx 0x4A4510 (atan2f)
- 0x00559278 blx 0x4A47E0 Pose3d(yaw, Z_AXIS, goal, parent, name)
- the C# uses the chosen pre-action pose's position with the heading toward the object; the engine's +0x84 distance and exact point are unread
- 0x0055883C/0x0055884C 4-arg ctor stores; 0x005585A0 7-arg ctor strd (r1=-1.0); 0x0052A0EE and 0x0055B6E2 the only 4-arg ctor calls; 0x0052A0BA ldrb [r4,#0x35] usePreDockPose
- 0x005B7BF8, 0x005B9038, 0x005B91E6, 0x005B9B2E IHelper::CreateDriveToHelper callers; 0x005B584A ldr [r7,#0xf8]; 0x005532BC..0x00553360 the AlignmentType table
- 0x00559CDE/0x00559CE8 DriveToPlaceCarriedObject ActionType 1 or 2; 0x00554B40/0x00554B42 the only caller passes 1 (so 2)
- unity/scripts/csharp/Robot.cs:1580-1585 and SpeedTap/SpeedTapPlayerGoalCozmoSelectCube.cs:97 (goToPreDockPose:true)
- re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1a-Q1d and re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md (rows N-1..N-8 verified; priority check 2)
- re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q5 (the NaN row: contradicted the earlier record text)

Test: M12RVisBuildTests.M12_022_*; NavigationTests


### M12-024 — IMPLEMENTATION_GAP

Title: DrivingAnimationHandler PlayStartAnim / PlayEndAnim

Evidence:

- 0x0055AC08 PlayStartAnim call; 0x0055B0AA..0x0055B0CC PlayEndAnim call and its return handling
- re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1c
- re-analysis/research/20261013-m12-024-driving-lifecycle.md: DriveToPose::Init calls handler Init first (0x0055A86C..0x0055A888; tracks +0x54, tag +0x60, suppress +0x56, repeat 0); CheckIfDone state-3 gate 0x0055AB5A..0x0055AB6A; status 3 PlayStartAnim every tick and the every-10th WaitingForPathCompletion info (0x0055AC06..0x0055ACA0, 0xBECBFB/0xBECC32); tail 0x0055B0AA..0x0055B0CC; status 0 and 2 return directly; the result is recomputed after the end animation (state 4); destructor 0x0055A5FC..0x0055A63C (Abort, EraseAllPlannerObstacles(1)/(0), ActionIsBeingDestroyed); no Cancel override; IAction order (TIMEOUT before CheckIfDone, retry re-runs Init) 0x00540D1C..0x00540F9E.

Test: M12RVisBuildTests.M12_024_*; M5X16BuilderTests; M5X14BuilderTests; M12RVisBuildTests; ManipRig (explicit dependency isolation); NavigationTests.PopAWheelieStopsAtMissingDrivingLifecycleBeforeDockRetry; M5X17BuilderTests; M12RVisBuildTests; NavigationTests; R2FloorPlacementTests; M5X15BuilderTests; M5X16BuilderTests (B-M5-17 regressions); CorrectionTests.ADriveCancelledMidPathAbortsItOnTheRobot; CorrectionTests.APlannerFailureSendsNoPathEvenWhenTheStraightLineIsClear; RFixBatch2Tests.M13_005_AStoppedPlanSendsNoPathThroughDriveToPoseAction; ManipulationTests.DriveToPoseSucceedsWhenThePathCompletesAtTheGoal; M5X18BuilderTests; M5X17BuilderTests; NavigationTests; CorrectionTests; M12RVisBuildTests; R2FloorPlacementTests (M12-032 stand-in, not native status verification); M12PathMachineBuilderTests; M5X18BuilderTests; M12RVisBuildTests; NavigationTests; M4AbortSleepTests (B-M12-1); M5X19BuilderTests; M5X15BuilderTests; M5X17BuilderTests; M5X18BuilderTests; NavigationTests; FreeplayTests; ActionListTests


### M12-029 — EXACT_SOURCE

Title: The default +0x150 pose function, the DriveToHelper functor and RemoveMatchingPredockPose

Evidence:

- 0x005586AC setter; 0x0055DB72, 0x0055DBDE default functors; 0x00558C80 GetPossiblePoses; 0x005B61A2 the DriveToHelper functor; 0x00551418 RemoveMatchingPredockPose
- re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q9; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q9 (PASS)

Test: M12RVisBuildTests.M12_029_*


### M12-031 — IMPLEMENTATION_GAP

Title: IDockAction::GetPreActionPoses and SelectObject/GetObstacles

Evidence:

- 0x005508C8..0x00551200
- re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q10; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q10 (PASS)
- 0x005B6F5C..0x005B70DA, 0x0055EE10..0x0055EE66
- re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q9, Q3.7; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md

Test: M12RVisBuildTests.M12_031_*; M12RVisBuildTests.M12_001_*


### M12-035 — IMPLEMENTATION_GAP

Title: The flip-block +0x150 installers and DriveAndFlipBlockAction::GetPossiblePoses

Evidence:

- 0x0055E208..0x0055E32A, 0x0055F366..0x0055F464, 0x0055E3B0..0x0055E3F6, 0x0055F4C6..0x0055F52E, 0x0055EB04..0x0055EB8A, 0x0055F592..0x0055F5A4, 0x0055EC04..0x0055EC2E, 0x0055F60E..0x0055F624, 0x0055E438..0x0055E91A
- re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q3 (PASS)
- 0x00559A86..0x00559AE6 the non-type-6 CheckIfDone tail (manager read)
- 0x0055B2C8..0x0055B438 the IDriveToInteractWithObject ctor compound construction and its AddAction flags (manager read)

Test: M12RVisBuildTests.M12_035_*; M12RVisBuildTests.M13_028_*; NavigationTests.DriveAndFlipBlockIgnoresTheFailedDriveAndRunsBothTurnsAndTheFlip; NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates


### M13-002 — EXACT_SOURCE

Title: FlipBlockAction's constants, their roles and IAction type

Evidence:

- FlipBlockAction ctor 0x0055EC80: IAction type 0xF (0x0055ECA8/0x0055ECAA); +0x12C=150.0 (0x43160000), +0x130=20.0 (0x41A00000) at 0x0055ECF6/0x0055ECFA; +0x134=45.0 (0x42340000), +0x138=40.0 (0x42200000), +0x13C=-1 at 0x0055ECFE..0x0055ED0E; +0x140=1 at 0x0055ED10/0x0055ED12
- +0x12C is the drive speed: Init 0x0055EF18 loads it into r3 and passes it to DriveStraightAction 0x0055EF2C; the r3 argument is stored at DriveStraightAction+0x7C (0x005472A4)
- +0x130 is the drive-past distance: Init computes the robot-to-object distance (vsqrt.f32 0x0055EEFA), adds +0x130 (0x0055EF14/0x0055EF1C) and passes the sum as DriveStraightAction's distance argument 0x0055EF20 (stored at DriveStraightAction+0x78)
- +0x134 is the approach lift height: Init 0x0055EF3A loads it into r2 for MoveLiftToHeightAction(45.0, 5.0, 0) 0x0055EF4A (added before the drive)
- +0x138 is the lift trigger distance: CheckIfDone 0x0055F124 vldr s0,[r4,#0x138] / vcmpe / bpl 0x0055F130; when the object is closer than it and +0x13C is still -1 it queues MoveLiftToHeightAction(preset 2, speed 5.0) 0x0055F14E and stores that action's id in +0x13C (0x0055F15C) through ActionList::QueueAction position 5 (0x0055F16A)
- +0x13C is the queued lift action's id, -1 when none; the destructor cancels it on robot+0x250 ActionList (0x0055ED6C/0x0055ED70/0x0055ED7A)
- +0x140 is shouldCheckPreActionPose: Init reads it (0x0055EE10) into the PreActionPoseInput byte at sp+0x64 (0x0055EE1C) passed to IDockAction::GetPreActionPoses with ActionType 5 (0x0055EE14/0x0055EE5E); SetShouldCheckPreActionPose writes it (0x0055EDC2)

Test: NavigationTests.FlipDrivesThroughTheCubeRaisingTheLiftAndForgetsItsPose


### M13-014 — IMPLEMENTATION_GAP

Title: Knock over a stack: BehaviorKnockOverCubes flow, DriveAndFlipBlockAction and IDriveToInteractWithObject

Evidence:

- BehaviorKnockOverCubes body 0x005C2EA0..0x005C3E2A; IsRunnableInternal 0x005C314C needs StackOfCubes::GetStackHeight() >= +0x124 (minimumStackHeight default 3); InitInternal 0x005C31A2 runs the reach unless +0xD9 (alwaysStreamline) or +0xD8 (the soft/hard switch flag) is set (0x005C31B4/0x005C31BA)
- TransitionToReachingForBlock 0x005C3254: TurnTowardsObjectAction(max pi), then DriveStraightAction(x-85, 60) when the block x+10 > 85.0, then TriggerLiftSafeAnimationAction(+0x150), then TransitionToKnockingOverStack regardless of result
- TransitionToKnockingOverStack 0x005C34A8: TurnTowardsObjectAction(max pi), DriveAndFlipBlockAction with maxTurn = pi/2 on the first attempt (0x005C36BC) and 0.0 once +0x140 > 0 (0x005C36C0); +0xD9 or +0xD8 also forces 0.0 (0x005C34EA..0x005C34F8); WaitAction(0.5); the callback 0x005C3DCE writes AIWhiteboard+0x70 on NoPreActionPoses (0x03000010), re-runs while +0x140 <= 1 on a Retry result and otherwise blind-flips, incrementing +0x140 either way
- success goes to TransitionToPlayingReaction 0x005C3908: it sets robot->[+0x34]->[+0x94]->[+0xC] = 1 (0x005C3946..0x005C394E), i.e. the BlockWorld's BlockConfigurationManager dirty flag (BlockWorld+0x94 is the manager; BlockConfigurationManager+0xC is read by its Update 0x00616D84) forcing all block configurations to recompute; the tipped-object set size at +0x14C selects the success trigger +0x15C with BehaviorObjectiveAchieved(0xD, true) and NeedActionCompleted(0) (0x005C3950..0x005C3964) or the failure trigger +0x160 (0x005C396E); when +0xD9 or +0xD8 is set the reaction animation is skipped (0x005C3972/0x005C3978)
- IDriveToInteractWithObject 0x0055B1F4 adds TWO actions when maxTurn > 0 (0x0055B37C..0x0055B392): a TurnTowardsLastFacePoseAction (vtable overwritten from TurnTowardsFaceAction at 0x0055B3C4..0x0055B3D8) and a TurnTowardsObjectAction (0x0055B42E/0x0055B43C), both with failure ignored; the trailing float 20.0 is not read by DriveAndFlipBlockAction ctor 0x0055E208
- +0xD9 is the JSON config key alwaysStreamline (IBehavior::ReadFromJson 0x005BC208/0x005BC216/0x005BC21E); +0xD8 is computed by IBehavior::Init 0x005BCCAA..0x005BCCC2 as 1 when the behaviour was entered by a hard spark switch and 0 for a soft switch or no current behaviour (corrected operator 2026-10-28: SetRequestedSpark 0x005A3ED4..0x005A3ED8 maps a non-zero bool to "soft" 0x00BF0B78 and zero to "hard" 0x00BF0B7D)
- 0x0055B3C0..0x0055B3CC, 0x005DE160..0x005DE16C, 0x005B7DB2..0x005B7DC2 the vtable overwrite; vtable 0x010204F4 slots 2, 4, 6, 9, 10
- 0x0054B7B6 the ctor store of +0x193; 0x005C229C, 0x005DE170, 0x005F20B8, 0x005F6942 the behaviour stores
- 0x0054B978..0x0054B9F4 SetSayNameAnimationTrigger; 0x0054C606..0x0054C61E and 0x0054C2D0..0x0054C2E6 the NeedsManager registrations
- re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q4; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md objection A8 (the class adds no logic of its own)
- re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q2, Q3, Q6; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q2/Q3, Q6 (PASS)
- 0x0055B2C8..0x0055B438 the IDriveToInteractWithObject ctor compound construction and its AddAction flags (manager read)

Test: NavigationTests.DriveAndFlipBlockAddsBothTurnsWhenMaxTurnIsPositive; M13RVisBuildTests.M13_014_*; FaceTests.TurnTowardsFaceTurnsThenFineTunesOnAFreshObservation; NavigationTests.DriveAndFlipBlockIgnoresTheFailedDriveAndRunsBothTurnsAndTheFlip


### M13-028 — IMPLEMENTATION_GAP

Title: FlipBlockAction::Init and DriveAndFlipBlockAction's constructor guards

Evidence:

- 0x0055EED6..0x0055EEFA, 0x0055EF3A..0x0055EF70, 0x0055ECE0, 0x0055EEC6, 0x0055EF7E, 0x0055B258..0x0055B264, 0x0055B38C
- re-analysis/research/20260929-R-VIS-verify-M12-fix2.md objections 2, 3, 9
- 0x0055F074..0x0055F17A FlipBlockAction::CheckIfDone; 0x0055EF3A..0x0055EF4A; 0x00548A54..0x00548A78 (fix-round-3 verifier)
- 0x0055F074..0x0055F208 CheckIfDone; 0x0055ED54..0x0055EDA0 destructor (round-4 verifier, .scratch/m12v5_cid.txt)

Test: NavigationTests.FlipDrivesThroughTheCubeRaisingTheLiftAndForgetsItsPose; NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates; M12RVisBuildTests.M13_028_*; M12RVisBuildTests.M13_028_ALostObjectEndsARunningFlipWithBadObjectAndNoPathIsSentAfterwards; M12RVisBuildTests.M13_028_* (B-M12-1 held-clock fixture correction)


### M4-016 — EXACT_SOURCE

Title: MoveHeadToAngleAction / MoveLiftToHeightAction Init, CheckIfDone and the MotorActionAck handlers: no send in position, ack by id, the latch and has-moved bytes, 0x04000004 / 0x03000016, the u16 diagnostic counters

Evidence:

- MA9, MA13, MA15..MA17, C1, C6, C10.1 as before
- C11.3 the lift CheckIfDone body 0x005493F6..0x00549508 contains no eye-shift removal; the head +0xA8 has only three writers, all zero (0x00547F3C, 0x005484B0, 0x00548724), so H4..H6 never execute
- Handoff: IAction::UpdateInternal (0x00540D1C..0x00540F9E) calls the action's Init and CheckIfDone virtuals; the timer, the 30 s timeout slot, the TimedOut warning (+0x57 test 0x00540E7C, sWarningF 0x00540EB6), Cancel, SetTag and PrepForCompletion are IActionRunner's: M8-015
- Lift angular-tolerance clip 0x005491D6..0x005492EE (Opus final settling pass (2026-10-11, HEAD 8f035387; jobs/B-M3M4.md "Final settling pass")): default angular tolerance FLT_MAX, bounds 32.0/92.0, threshold 0x3CD67750 (1.5 deg), warning "HeightTol %f mm == AngleTol %f rad near height of %f mm. Clipping tol to %f mm"; with a 5.0 mm tolerance every target in [32,92] gives >= ~0.076 rad, so the clip cannot fire for any engine or stack caller.

Test: M4ControlTests.M4_016_MA15_NothingIsSentWhenAlreadyInPosition, M4ControlTests.M4_016_MA16_MA17_CompletionIsTheAckThenInPositionAndStopped, M4ControlTests.M4_016_MA17_StoppingOutOfPositionIsStoppedMakingProgress, ControlTests.SetHeadAngleWaitsForTheRobotToAcknowledgeThatExactAction, M4ControlTests.M4_016_C1_TheHeadInPositionIsLatched, M4ControlTests.M4_016_R1_PassingTheTargetBeforeTheAckDoesNotLatch, M4ControlTests.M4_016_C6_TheLiftCompletion, M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds, M4ControlTests.M4_003_MA_TheHeadMoveLocksThenUnlocksItsTrack; M3M4X18BuilderTests; M3M4X25BuilderTests.HeadAckIsUnlinkedBeforeTheBaseTailUnlocksItsTrack


### M8-015 — IMPLEMENTATION_GAP

Title: ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown

Evidence:

- 0x0053E6A8..0x0053E6E0; 0x005409E2..0x00540A40; 0x00541146..0x00541238; 0x0053FAB2; Current Cancel then delete; pending direct deletion preserves existing result. Cancelled0x02000000 except NotStarted0x02000001; stop before unlock, watcher then broadcast. Existing B-ACTIONS candidate must be checked for full teardown effects and concrete descendants.
- Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt
- P3/P4 full ActionList/ActionQueue/IAction lifecycle: ActionList 0x0053F580..0x0053F5F2, ActionQueue 0x0053F5F4..0x0053F70C, IActionRunner 0x00540370..0x0054063E, clock/timeout 0x00540D4A..0x00540E80, CheckIfDone 0x005549B8..0x005549F4 (20261006-M1M2-missing-triage.md P3/P4).
- re-analysis/research/20261012-m8-play-animation-actions.md rows C2..C7, L14..L23, D5, S2..S4 and re-analysis/research/20261012-m8-play-animation-actions-check.md (the +0x57 gate on L17/L18/L20, the DeleteActionAndIter guard set at queue+0x10, ActionWatcher Start/EndUpdating 0x00540386/0x0054057E/0x00540630, the UpdateInternal status string 0x00540D46..0x00540F1C).
- re-analysis/research/overnight/20261014-ov18-m8-015-check.md: L1-L9 0x00540370..0x0054063A, 0x00540D1C..0x00540F9C, 0x00540750..0x005407C0, 0x005409DC..0x00540A44; Q1-Q7 0x0053F580..0x0053F708, 0x0053DCE4..0x0053DD92, 0x0053E24C..0x0053E2F6, 0x0053DF2C..0x0053DFC8, 0x0053DE10..0x0053DE5C, 0x0053E690..0x0053E700; D1-D6, D12 0x0053F9E4..0x0053FB06, 0x00541084..0x00541286, 0x00541BB4..0x00541C04, 0x0054187E..0x005418DE; T2 List Clear 0x0053D916..0x0053D93A, 0x0053F938..0x0053F950, 0x0053FB5C..0x0053FB82; QueueAction front end 0x0053D93C..0x0053DAC8; position table 0x0053DA24..0x0053DB70.
- re-analysis/research/overnight/20261015-ov56-58-check.md: teardown mechanics from OV56: ~Robot order 0x005110EE..0x00511156; AbortAll 0x00511952..0x0051198C (0x0063BE2A..0x0063BE36, 0x00517DFE..0x00517E0A); ActionList::Cancel clearing gate and slot order 0x0053DE18..0x0053DE54; ActionQueue::Cancel 0x0053E69A..0x0053E6FA (early exit 0x0053E6E4); QueueNow/QueueAtEnd 0x0053E25A..0x0053E36E; DeleteActionAndIter 0x0053F9E4..0x0053FAB2 (re-entrancy set, INTERRUPTED/no-interface gates); result codes 0x0053FDBA..0x0053FDD4, 0x005409E2..0x00540A40; Clear lock-out 0x0053D91A..0x0053D938, 0x0053DCEA..0x0053DD00, 0x0053DCAA, 0x0053DF40, 0x0053F93C, 0x0053F970..0x0053F9AC; ~ActionList double Clear 0x0053D8F6..0x0053D90E; IBehavior StartActing reject-delete 0x005BDC04, Init failure 0x005BCCC6..0x005BCD06.

Test: ; M5X19BuilderTests; M5X15BuilderTests; M5X17BuilderTests; M5X18BuilderTests; NavigationTests; FreeplayTests; ActionListTests; M8ActionCheckedRowsTests; ActionListTests; ActionCompletionTests; M8TeardownCheckedRowsTests; M4AbortSleepTests.M4_026_A1_TheResultIsTheEnginesFailIfAnyFailed; BehaviorRunnerOwnershipTests; M2M15HandlerBuilderTests.RunningNeverInvokesCompletionCallbacksAndNonzeroInitWithInitializedFlagStillChecks; B-M1-5 ReportOrderingRegressionTests.CliffStopPublishesAnimationAbortBeforeRobotStoppedAndCompletionHasAHostView (production queue/action cancellation/host completion); concrete completion-union recovery remains with M8-020.


### M13-033 — IMPLEMENTATION_GAP

Title: Planner side of PathComponent: planner construction, SelectPlannerHelper, lattice ComputePath/ComputeNewPathIfNeeded/StartPlanning (both force values, safe-prefix retention), async worker and F0-F8 flags, StopPlanning guard, GetCompletePath, the short planners, IPathPlanner wrappers, slot 0x20/0x18 safety dispatch

Evidence:

- re-analysis/research/20261013-m12-032-path-status.md: "Unread" section: SelectPlannerHelper 0x0064A5BA..0x0064AA18; LatticePlanner 0x004FE688, 0x004FEB00, 0x004FD1A2, 0x004FF936; FaceAndApproach/MinimalAngle vtbl+0x2C; ComputeClosestGoalPose 0x005087AC; ApplyMotionProfile 0x00508AE4; planner status base 0x00508A94..0x00508AAA.
- re-analysis/research/20261013-planner-internals-check.md: ctor 0x00648AE0..0x00648C54; SelectPlannerHelper 0x0064A530..0x0064A8DA (not 0x0064A5BA..0x0064AA18); ComputeNewPathIfNeeded 0x004FEB00..0x004FEB34; StartPlanning 0x004FEB44..0x004FF452 (false branch, prefix 0x004FECDE..0x004FECF6, Append 0x00500540); worker 0x005007E0..0x00500854; DoPlanning 0x00500090..0x00500590; StopPlanning guard 0x004FD1A2..0x004FD1CC; lattice GetCompletePath 0x004FFB64..0x004FFE2E; FaceAndApproach 0x004F35A8..0x004F38F6; MinimalAngle 0x00503C18..0x005041B2; IPathPlanner 0x005087AC..0x00508AE2, 0x005091CC..0x0050928A (base internal returns 0 when +0x28 == 0); safety dispatch slot 0x18 0x00649FB6 -> 0x00509202 -> slot 0x1C.

Test: M12PathMachineBuilderTests; M5X18BuilderTests; M12RVisBuildTests; NavigationTests; M4AbortSleepTests (B-M12-1); M13X1BuilderTests; NavigationTests; RFixBatch2Tests (B-M13-1); CorrectionTests.ASoftObstacleDoesNotBlockTheGoal; CorrectionTests.APlannerFailureSendsNoPathEvenWhenTheStraightLineIsClear (B-M13-1); M13X2BuilderTests; M13X1BuilderTests; NavigationTests (B-M13-2); M13X2BuilderTests; M13X1BuilderTests (B-M13-3); M13X2BuilderTests.FreshPlanLogsStateAndBothPlanLabelsBeforeReady; M13X2BuilderTests.NonForcedEmptyPlanLogsUnsafeAfterForcePlan; M13X2BuilderTests.CompletePathKeepsGoalFromSuccessfulWorkerPlan (B-M13-5); M13X2BuilderTests.FreshEmptyPlanRetrievalReportsPositionErrorAndClears (B-M13-6)


### M13-035 — HARDWARE_ONLY

Title: Planner thread scheduling: which tick UpdatePlanning sees running (1), complete (2/3) or the transient 0 published by DoPlanning (0x005004DE)

Evidence:

- re-analysis/research/20261013-planner-internals-check.md (and re-analysis/research/20261013-planner-internals.md): StartPlanning async dispatch 0x004FF432..0x004FF49E (F5 = 0 in production), worker 0x005007E0..0x00500854, DoPlanning F8 stores 0x00500216/0x00500212/0x0050058E and the transient 0 at 0x005004DE; UpdatePlanning reads F8 each tick (0x006495E6..0x006495EC).

Test: M13X1BuilderTests; NavigationTests; RFixBatch2Tests (B-M13-1); CorrectionTests.ASoftObstacleDoesNotBlockTheGoal; CorrectionTests.APlannerFailureSendsNoPathEvenWhenTheStraightLineIsClear (B-M13-1)

