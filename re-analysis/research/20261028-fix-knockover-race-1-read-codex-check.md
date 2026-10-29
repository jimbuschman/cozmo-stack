# FIX-KNOCKOVER-RACE-1 independent read check (2026-10-28)

Scope: read-only independent review requested by root for FIX-KNOCKOVER-RACE-1. Reviewed manager checkout ee667d7a, current metadata imported from main1268d9b3 (718 records). No .NET, Git mutation, native extraction, hardware, code, manifest, inventory, approval, or official-status edits. Reviewer spawn requested DEFAULT model/effort (both omitted); no runtime model telemetry or model switch. PROJECT_STATE.md was read first, followed by AGENTS.md, research README and trial job/AUTONOMY/REVIEW/QUEUE. This is a source-code diagnostic check, not a claim of recovered engine behavior.

## Verdict at diagnosis checkpoint

No proven inner cause yet; no semantic production fix is justified by available artifacts. R1 diagnostics, the captured fixture scheduler, and N5 shared action clock are already present. Passing reruns cannot settle this fault. The independently traced public/manual path is not the new NativeKnockOverCubesBehavior/NativeDriveAndFlipRunner path. An implementation defect versus a harness defect remains unestablished; this is not evidence of a robot, native-source, or external blocker.

The coherent reader report `20261028-fix-knockover-race-1-read.md` was independently checked after delivery. PASS for the diagnosis boundary and Stage D test-only diagnostic proposal, with the implementation constraints below. No semantic production fix is released.

## Failure evidence and limits

The actual local earlier artifact is `../codex-knockover-cubes-1/.scratch/knockover-cubes-1/affected-1.trx` (also affected-1.log), not the reported Claude584a29b5 full gate. TRX test start 2026-10-28T09:49:14.7484733-05:00, duration00:00:00.7461263, records an Assert.Contains failure at NavigationTests.cs1059. Its collection preview retains only `DriveAndFlipBlockAction(7) faulted: System.Aggrega` plus truncation; the inner exception is absent. Same failed artifact preserved rather than inferred from counts. The test log's path points to codex-knockover-cubes-1, not the current manager tree. Existing builder report associates this with the KNOCKOVER-CUBES-1 affected-1 run; precise pre-run source hash needs its original command/freeze metadata rather than borrowing current HEAD. The later legacy diagnostic and affected2 TRXs pass but do not diagnose the failed run. Claude's584a29b5 failed scratch log remains unavailable here.

## Actual path traced

1. NavigationTests.cs1012-1071 constructs SignalTestContext before Rig, renders three cubes, supplies Idle motor acknowledgements/state, starts the public KnockOverCubesBehavior, and later injects a tipped observation only after actual flip-speed path output. Test reach is explicitly unit/substituted firmware input, not whole production admission or hardware acceptance.
2. NavigationTests.RunToEnd101-121 sets animation manual clock and `ActionTaskRunner = SignalTestContext.Schedule`, starts behavior, loops Update/Pump/Frame/Idle/AdvanceBehavior, then calls Stop. SignalTestContext.cs34-52 posts the async invocation to its captured fixture context; Drain runs a snapshot, Step waits for actual posted work. This test does not begin its behavior action using the production default Task.Run. Default-scheduler task completions and real delay continuations still exist; that alone is not a proven race.
3. CubeGameBehaviors.cs70-119 subscribes ObjectObserved, selects current tallest stack, runs the reach turn plus DriveStraight then grab trigger. TransitionToKnockingOverStack121-174 creates the legacy DriveAndFlipBlockAction; action sequence is turn -> flip.RunAsync -> real Task.Delay(0.5s), with category callback retry/reaction. Stop221 unsubscribes observed callback and delegates to ActionBehavior cancellation.
4. FaceBehaviors.cs20-45 executes ActionBehavior.RunAction: existing R1 continuation logs full `t.Exception.ToString`, maps non-successful Task completion to the caller's failed value, posts callback, and rejects stale cts. This does not catch and report success. SteppedBehavior.cs63-74 supplies fixture invocation wrapper; production keeps Task.Run. Completion is posted to the behavior update queue, not run as the behavior transition on the worker.
5. FlipBlockAction.cs379-420 legacy DriveAndFlip runs DriveToObjectAction, copies drive trace, ignores drive failure with explicit trace, then optional last-face and object turns, then Flip.RunAsync. DriveActions.cs570-652 obtains object/poses, retains the yaw-only legacy goal adapter, queues a real DriveToPoseAction, matches reached goal, turns again; CheckIfDone728-757 re-asks poses and returns DidNotReachPreActionPose when flag stays false.
6. DriveToPoseAction.RunAsync368-378 queues the real ActionList action and awaits its completion source; cancellation cancels its actual tag. Its CheckIfDone149-163 consumes path state and selected robot pose; Init164 onward configures the real planner set and path component. Rig uses geometry-only driving-animation input and empty obstacle eraser, not source-complete driving animation. The native planner set is present, but this test never calls LoadPlanner and therefore has no configured lattice runtime; the conditional Rig.Pump243 worker wait is not evidence of a lattice worker in this run. Rig.Pump decodes outbound frames, feeds firmware messages and transport acknowledgements, advances transport/manual animation, ticks engine/action list. No LoadPlanner call is made by this exact test, so a loaded native lattice corpus must not be assumed.
7. FlipBlockAction.RunAsync156-214 asks GetPreActionPoses, starts embedded lift -> DriveStraight compound, and polls CheckIfDoneTick (object lookup/relative pose/lift trigger; final result marks Unknown). It can fault after the ignored-drive trace has already been created. It retains known legacy cancellation/queued-lift limitations. These are existing manifest gaps, not established explanations for this exception.
8. Rig State/Idle feed synchronous offline datagrams through CozmoRobot.CreateOffline's auto-tick path. CozmoRobot.Route981-993 catches and exposes handler exceptions through HandlerFaulted; an async action fault and a routed handler fault are distinct observations. RunToEnd calls behavior.Stop before using-var Rig disposal; Rig.Dispose471-475 disposes standalone manipulation, vision, robot. SignalTestContext.Dispose restores previous context and deliberately keeps its signal alive for late canceled timer posts. No teardown-first cause follows from the reported trace.

## Findings for the next diagnostic batch

**D1 (concrete diagnostic hole):** current1057 uses Assert.True with complete joined b.Trace for the ignored-drive expectation, but current1059 still uses Assert.Contains with collection preview for terminal flip success. The reported failure is1059. If the ignored-drive trace exists before a later flip fault,1057 passes and1059 loses the inner exception. Therefore existing improved1057 output is not enough. Keep1059's exact predicate and assertion strength; append complete diagnostic context to its failure. Prefer a bounded failure artifact retaining all b.Step/trace and existing journal/HandlerFaulted details, rather than relying on xUnit preview.

**D2 (clock precision):** N5 is present: ManipRig.cs27-30/94/108 controls M.ClockSec through volatile ActionClockSec separately from Clock. However native IAction timing is not globally frozen by N5: DriveToPoseAction.EngineClockSeconds137 and RunAsync370 use Robot.Engine.Timer, and Rig.Pump283 advances Clock then ticks engine. This is not evidence that a timeout caused the AggregateException, and does not authorize changing clock semantics. Diagnostic state should report both clocks and actual action result/inner fault instead of declaring all deadline coupling removed.

**D3 (bounded next step, requires root release):** before a production change, instrument only this exact test's failure reporting (unchanged assertions), retain b.Step/full trace and actual AsyncInvocationStarted task fault plus HandlerFaulted/journal events, and run one declared finite reproduction set with all outcomes saved. The failed task's full inner exception and stack should select the next controlled interleaving. Do not invent a race and then write a regression proving only that hypothesis. Do not replace the public behavior with NativeKnockOver or introduce retries, timeouts, or catch-and-succeed. A deterministic regression is properly selected only after the fault or independently provable conflicting access is identified.

## Current fidelity ownership

The following verbatim current title/status/evidence snapshot is an appendix. No record changes are proposed by this diagnostic check. M13-002's settled claim is constants/roles/type, not the whole legacy flow; M13-014,028 and M12-035 retain their actual public-path gaps. The unit test comments reference real records M13-014, M12-035 and M13-028 (plus success-mark address0x0055F186), not a hardware expectation. M13-035 remains scheduling uncertainty, not authority to accept arbitrary exceptions.

```json
[
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
  },
  {
    "id": "M12-023",
    "title": "DriveToPoseAction::CheckIfDone: the path-status state machine, tolerance and arrival test",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "0x0055AB4C..0x0055B0CC CheckIfDone; 0x0055AB84 tbh table; 0x006492D6..0x006492E8 and 0x0102F610 the status name table (0 Failed .. 6 WaitingToCancelPathAndSetFailure)",
      "0x0055ACA2..0x0055ACFC tolerance; 0x0055AD7A..0x0055B0A6 arrival test; 0x0055B0AA..0x0055B0CC tail; 0x0055A31E, 0x0055933E, 0x0055962E the +0xC0 writers",
      "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1c; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md priority check 3 (A6: the logged diff is 3-D, log only)"
    ]
  },
  {
    "id": "M12-024",
    "title": "DrivingAnimationHandler PlayStartAnim / PlayEndAnim",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "0x0055AC08 PlayStartAnim call; 0x0055B0AA..0x0055B0CC PlayEndAnim call and its return handling",
      "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1c",
      "re-analysis/research/20261013-m12-024-driving-lifecycle.md: DriveToPose::Init calls handler Init first (0x0055A86C..0x0055A888; tracks +0x54, tag +0x60, suppress +0x56, repeat 0); CheckIfDone state-3 gate 0x0055AB5A..0x0055AB6A; status 3 PlayStartAnim every tick and the every-10th WaitingForPathCompletion info (0x0055AC06..0x0055ACA0, 0xBECBFB/0xBECC32); tail 0x0055B0AA..0x0055B0CC; status 0 and 2 return directly; the result is recomputed after the end animation (state 4); destructor 0x0055A5FC..0x0055A63C (Abort, EraseAllPlannerObstacles(1)/(0), ActionIsBeingDestroyed); no Cancel override; IAction order (TIMEOUT before CheckIfDone, retry re-runs Init) 0x00540D1C..0x00540F9E."
    ]
  },
  {
    "id": "M12-035",
    "title": "The flip-block +0x150 installers and DriveAndFlipBlockAction::GetPossiblePoses",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "0x0055E208..0x0055E32A, 0x0055F366..0x0055F464, 0x0055E3B0..0x0055E3F6, 0x0055F4C6..0x0055F52E, 0x0055EB04..0x0055EB8A, 0x0055F592..0x0055F5A4, 0x0055EC04..0x0055EC2E, 0x0055F60E..0x0055F624, 0x0055E438..0x0055E91A",
      "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q3 (PASS)",
      "0x00559A86..0x00559AE6 the non-type-6 CheckIfDone tail (manager read)",
      "0x0055B2C8..0x0055B438 the IDriveToInteractWithObject ctor compound construction and its AddAction flags (manager read)"
    ]
  },
  {
    "id": "M13-002",
    "title": "FlipBlockAction's constants, their roles and IAction type",
    "status": "EXACT_SOURCE",
    "evidence": [
      "FlipBlockAction ctor 0x0055EC80: IAction type 0xF (0x0055ECA8/0x0055ECAA); +0x12C=150.0 (0x43160000), +0x130=20.0 (0x41A00000) at 0x0055ECF6/0x0055ECFA; +0x134=45.0 (0x42340000), +0x138=40.0 (0x42200000), +0x13C=-1 at 0x0055ECFE..0x0055ED0E; +0x140=1 at 0x0055ED10/0x0055ED12",
      "+0x12C is the drive speed: Init 0x0055EF18 loads it into r3 and passes it to DriveStraightAction 0x0055EF2C; the r3 argument is stored at DriveStraightAction+0x7C (0x005472A4)",
      "+0x130 is the drive-past distance: Init computes the robot-to-object distance (vsqrt.f32 0x0055EEFA), adds +0x130 (0x0055EF14/0x0055EF1C) and passes the sum as DriveStraightAction's distance argument 0x0055EF20 (stored at DriveStraightAction+0x78)",
      "+0x134 is the approach lift height: Init 0x0055EF3A loads it into r2 for MoveLiftToHeightAction(45.0, 5.0, 0) 0x0055EF4A (added before the drive)",
      "+0x138 is the lift trigger distance: CheckIfDone 0x0055F124 vldr s0,[r4,#0x138] / vcmpe / bpl 0x0055F130; when the object is closer than it and +0x13C is still -1 it queues MoveLiftToHeightAction(preset 2, speed 5.0) 0x0055F14E and stores that action's id in +0x13C (0x0055F15C) through ActionList::QueueAction position 5 (0x0055F16A)",
      "+0x13C is the queued lift action's id, -1 when none; the destructor cancels it on robot+0x250 ActionList (0x0055ED6C/0x0055ED70/0x0055ED7A)",
      "+0x140 is shouldCheckPreActionPose: Init reads it (0x0055EE10) into the PreActionPoseInput byte at sp+0x64 (0x0055EE1C) passed to IDockAction::GetPreActionPoses with ActionType 5 (0x0055EE14/0x0055EE5E); SetShouldCheckPreActionPose writes it (0x0055EDC2)"
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
    "id": "M13-035",
    "title": "Planner thread scheduling: which tick UpdatePlanning sees running (1), complete (2/3) or the transient 0 published by DoPlanning (0x005004DE)",
    "status": "HARDWARE_ONLY",
    "evidence": [
      "re-analysis/research/20261013-planner-internals-check.md (and re-analysis/research/20261013-planner-internals.md): StartPlanning async dispatch 0x004FF432..0x004FF49E (F5 = 0 in production), worker 0x005007E0..0x00500854, DoPlanning F8 stores 0x00500216/0x00500212/0x0050058E and the transient 0 at 0x005004DE; UpdatePlanning reads F8 each tick (0x006495E6..0x006495EC)."
    ]
  }
]
```

## Coherent reader and Stage D independent check

Checked the reader's complete current-path report and current manifest appendix. Its UNKNOWN classification, R1 logger already present,1059 diagnostic truncation, N5/native-IAction-clock distinction, no LoadPlanner/lattice-runtime boundary, legacy public-versus-native configured boundary and teardown limits agree with independently read code. PASS for test-only Stage D subject to root release and draft review before execution; no production correction supported yet.

Stage D scope: retain the EXACT current1059 predicate (`l.Contains("FlipBlockAction.CheckIfDone: compound result Success; object 7 marked Unknown")`) and every other expectation. An Assert.True of the same Any(predicate), with full diagnostic failure message, is equivalent assertion strength. Do not add a new success/fault expectation or bypass existing success checks. Capture b.Step and HandlerFaulted in DiagnosticJournal (its Write/Snapshot are under its gate); use an inert TextWriter for the journal if artifact-only output is intended, so diagnostics do not introduce a blocking application output sink. Snapshot traces with their existing locked API; snapshot journals with Snapshot, never enumerate a live List.

Task observation is completed-only: first test AsyncInvocationStarted.IsCompletedSuccessfully, then inspect its returned Task status. Read Exception only for an actually faulted task; no GetResult/Result on pending/faulted wrapper, no awaiting, no waiting, no new timeout or scheduling hook. In this legacy chain ObserveAsyncInvocation uses TrySetResult and RunAction itself does not call StartActing, so the first observation may be the already successful reach, and a later trigger may reset it. State the observed identity/limitation; do not call it the DriveAndFlip task without evidence. An absent task snapshot is a valid diagnostic outcome. Actual R1 fault trace remains the authoritative inner-exception channel.

The failure-finally artifact must preserve the original test exception, use a unique ignored destination, detach event handlers, and not run model ticks/waits/disposal before snapshots. If writing the artifact fails, retain that separate error without replacing the original assertion/fault. Do not make passing test teardown call new waits to flush diagnostics. Thread ID/counters/clocks/path state are observations only and cannot select behavior results.

Finite experiment accepted as diagnosis evidence: freeze instrumented source then one preserved historical affected filter; if it has any failure, retain every failure and stop for analysis. If clean, at most20 separate exact-target invocations with normal settings, stopping first failure. Preserve literal commands, hashes, actual exits/counts/names/results for every attempt (including prelaunch/build failures); no claim of acceptance/fix from zero reproductions. No .NET by reviewer, no new regression asserting an imagined interleaving, no semantic production change, and no record settlement. Root remains responsible for release and exclusive .NET lease.
