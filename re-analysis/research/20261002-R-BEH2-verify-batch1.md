# R-BEH2 batch 1 verification (M8-001/002/003/005/006/007/009/012), uncommitted diff

Verifier: Claude (Sonnet 5.5 session), read-only. Binary: resources/lib/armeabi-v7a/libcozmoEngine.so, Thumb-2 via capstone (my own
disassembly). Addresses are VAs. Manifest: statuses unchanged (all eight still IMPLEMENTATION_GAP); only the M8 approval date and sha
moved; no frozen field changed. Commands: fidelity.py --check OK (418 records); dotnet test filtered to
Behavior|Freeplay|Correction|DerivedState: 261 passed; FidelityLiteralLint|FidelityManifest: 11 passed.
Verdict: FAIL (blocking findings below).

## BLOCKING

B1  BehaviorManager.cs:703-753 (SwitchToBehaviorBaseCore), choice 6. _current/_scope are set BEFORE StartAsync (Init).
    Engine: SwitchToBehaviorBase 0x5a1e20 calls StopAndNullifyCurrentBehavior (0x5a1e6a; it nulls info.current, 0x5a2054..0x5a2058),
    IsRunnable (0x5a1e76), IBehavior::Init (0x5a1e94) with the manager's current still null, and only then SetRunningAndResumeInfo
    (0x5a1f12). Consequence: an Init that takes a reaction lock (BehaviorScope.SmartDisableReactionsWithLock ->
    BehaviorManager.DisableReactionsWithLock stopCurrent, BehaviorManager.cs:225-231) stops and scope-disposes the behaviour that is
    starting (engine: the nested SwitchToBehaviorBase on a null current is a no-op and the outer call then stores the info); Current /
    IsRunning read during Init show the new behaviour (engine: null). Not settled by the engine: INVENTION. Set after Init, or MISSING.

B2  BehaviorManager.cs:746-751 (the SetRunningAndResumeInfo step) and :358-367. OMITTED: SetRunningAndResumeInfo 0x5a209e..0x5a22ae does
    much more than store three fields: (a) when the trigger changes it sends the game message ReactionTriggerTransition{old,new}
    (0x5a20ba GetExternalInterface; 0x5a20d6 -> 0x5a2ee8 builds MessageEngineToGame(ReactionTriggerTransition) and calls the
    interface vslot 0x1c); (b) UpdateRobotPropertiesForReaction: writes [[robot+0x254]]+0xd4 and robot+0x2c7 = (new trigger != 0x16)
    (0x5a2102..0x5a2110; helper 0x5a2f54); (c) on a behaviour change with the +0x85 gate it walks the vector at manager+0x78 (stride
    0x90) calling CubeLightComponent::StopLightAnimAndResumePrevious (0x5a2230..0x5a223e). The stack does none of it. MC+0xD4, read by C7
    (BehaviorManager.cs:362 DirectDriveDisabled), has no writer anywhere (Motion.cs _directDriveDisabled is only ever reset, Motion.cs:115),
    so C7's "|| MC+0xD4" half (0x5a3642..0x5a3646) is dead. No inventory row covers (a)-(c); M8-012 owns the whole path (CHECKLIST 3), so
    they need their own records (gap), and the code must not present the step as done.

B3  SteppedBehavior.cs:163-171, 178-204 (IsRunnableBase gates). Production never wires the gate inputs: RobotState355, RobotState34a,
    RobotComponent284, RequiredProcessRunning, RobotStateAllowsRun, UnlockAllowsRun, RecentTimersAllowRun, WantsToRunAllowsRun, SparkGate
    are set only in tests (grep); null means "allow" (silent default; CHECKLIST 1 and 2, rule 6). Engine gates on the live path:
    robot+0x355 != 0 -> vtable+0x20 must return 1 (0x5bd864..0x5bd874); robot+0x34a != 0 -> vtable+0x24 (0x5bd876..0x5bd886);
    [robot+0x284]+8 != -1 -> vtable+0x28 (0x5bd888..0x5bd89a). For PlayAnim the slots are 0, 0, 1 (relocations 0x01026940/44/48 ->
    0x5bf04c "movs r0,#0", 0x59ec12 "movs r0,#0", 0x5bff1a "movs r0,#1"), so the engine's PlayAnim is NOT runnable while robot+0x355 is
    set (the byte BehaviorManager.cs:624 already reads as OffTreadsState) and the C# one is. The base defaults are wrong too: IBehavior's
    vtable (0x10264e8) has +0x20 = 0x5bf04c (returns 0), +0x24 = 0x59ec12 (returns 0), +0x28 = __cxa_pure_virtual, but
    RunnableGate20/24/28 default to true (SteppedBehavior.cs:200-204) for every class that does not override. Fix facts: wire 355 =
    OffTreadsState != OnTreads and 284 = Motion.IsCarryingObject (already wired, ManipulationSystem.cs:40); robot+0x34a, the AI process,
    the +0x74 state, +0x70 unlock, +0x78/+0x7c timers and the spark gate are not recovered here: MISSING, not allow. The tests
    (BehaviorFrameworkTests.cs about 567-570 and 947) drive the seams, not the production wiring.

B4  SteppedBehavior.cs:257-265, 615-619 (ScoredActingStateChanged "two callers"; ActingEnded). CONTRADICTED. There is a third caller:
    IBehavior::HandleActionComplete 0x5be1e6: if +0x84 != 0 and the completed tag == +0x84 (0x5be1ec..0x5be1f6) it stores 0 to +0x84
    (0x5be1fc) and calls vtable+0x80(0) ("ldr.w r2,[r0,#0x80]; blx r2" 0x5be202..0x5be208) = ScoredActingStateChanged, which clears
    +0x104; only then, if +0xa1 != 0 and the callback function (+0x98) is non-null, does it invoke the callback (0x5be20a..0x5be21c). The
    C# ActingEnded only clears the handle, so RunningScoreBonus survives a completed action; the test
    ScoredActingStateChangedClearsTheBonusOnEveryActionStartAndStop asserts only start and stop (it encodes the contradicted claim). The
    callback is also gated on +0xa1.

B5  SteppedBehavior.cs:434-444, 327 (StopOnNextActionComplete, _sharedHandle). +0x98 is NOT "a shared handle nothing in M8 sets": it is
    the std::function that StartActing stores (operator= into +0x88, 0x5bdb40..0x5bdb46; invoked at 0x5be216..0x5be21c only when
    +0x98 != 0). StopOnNextActionComplete 0x5bd624 sets +0xa0 = 1 (0x5bd698) and DESTROYS that function (0x5bd694..0x5bd6b4, +0x98 := 0);
    Init does the same (0x5bccc6..0x5bcce6). The C# sets _sharedHandle = null (a no-op), so the pending completion callback still runs
    after StopOnNextActionComplete. Fix fact: the completion callback of the current action is discarded; the behaviour then completes
    when +0x84 clears (UpdateInternal 0x5bda56 returns 2).

B6  SteppedBehavior.cs:607-613 (StartActing). OMITTED gates and result. IBehavior::StartActing 0x5bdacc: if +0xa0 is set and an action is
    given, the action is deleted and it returns 0 (0x5bdad4..0x5bdadc, 0x5bdc04); if neither +0xa2 nor +0xa1 is set it warns
    "IBehavior.StartActing.Failure.NotRunning" and returns 0 (0x5bdae2..0x5bdaee, 0x5bdbaa); if +0x84 != 0 it warns
    "IBehavior.StartActing.Failure.AlreadyActing" and returns 0 (0x5bdaf0..0x5bdb3e); only then +0x84 = tag, ScoredActingStateChanged(true),
    ActionList::QueueAction, whose failure warns "...Failure.NotQueued" and returns 0 (0x5bdb6c..0x5bdba8). The C# always starts, never
    returns false, and lets a second StartActing overwrite a live handle.

B7  SteppedBehavior.cs:736-738, choice 2. UNSUPPORTED: an animation that ends Replaced/Cancelled/Error counts as a failed action. The
    engine's PlayAnimationAction has no end reason: it registers only animStarted (0xca), animEnded (0xcb), animEvent (0xd5) and one
    external message (0x5f) (Init 0x543f9c..0x544128); the animEnded handler 0x54541d matches the tag (0x545432..0x54543a), counts the
    loops down (0x545442..0x545448) and sets +0x89 (0x5454ee); CheckIfDone 0x5441c0 returns SUCCESS when +0x89, FAILURE 0x03000001 when
    +0x8a (set only when SetStreamingAnimation refused, 0x543ffc), else RUNNING. What the streamer or robot do with a replaced or aborted
    tag is not settled by anything the implementer cites. Remove, or turn into MISSING; the consequence for a CompoundActionSequential
    child (ignoreFailure 0) is large (the loop ends).

B8  SteppedBehavior.cs:662-722 and CozmoAnimations.cs:408-452, choice 4. The lock is TESTED before the clip is resolved (matches the
    engine) but TAKEN after it (inside PlayTracked, after Resolve and before Play). Engine: IActionRunner::Update 0x540370 tests
    AreAnyTracksLocked (0x540440) and calls LockTracks (0x540584..0x54058e) BEFORE UpdateInternal (0x540592) -> IAction::UpdateInternal ->
    TriggerAnimationAction::Init 0x54443c. So with mask != 0 (lift while carrying) and an unresolvable trigger, the engine sends
    DisableAnimTracks{2} and later EnableAnimTracks{2}; the C# sends neither. The failure results are also dropped: Init returns
    0x0300000C for an empty group or clip name (0x544444..0x544448; warn "TriggerAnimationAction.NoAnimationForTrigger" at 0x5445f4) and
    0x03000001 for a refused stream (0x543ffc, 0x544132); the C# gives ActionOutcome.Failed() with Result null (SteppedBehavior.cs:691,
    703) although the binary has both codes. Choice 3 (an unresolvable trigger fails the action) IS settled (0x54445e..0x5445f4,
    0x54448e); only the code and the wire are wrong.

B9  BehaviorManager.cs:742, 850, 900 (choice 5). The manager disposes the scope even when Stop is skipped. Engine
    StopAndNullifyCurrentBehavior 0x5a2028: Stop() only if +0xa1 (0x5a203e..0x5a2046), nothing else releases anything; an Init failure
    (0x5a1e98..0x5a1efc) and a failed resume (0x5a2c7a..0x5a2cd6) never call Stop, so locks taken by that Init or Resume stay held until
    the behaviour's next Stop. The C# releases them at once. Not settled by the engine: remove, or MISSING.

B10 BehaviorManager.cs:839-846 (choice 7). The non-SteppedBehavior resume is invented (!IsRunnable -> failed, else StartAsync). The
    engine's Resume 0x5bceac has the +0x114 counter and refusal (0x5bcf16..0x5bcf7c), +0xa2, the +0x34 stamp, ResumeInternal vtable+0x4c
    and the spark lock for every behaviour. Production non-stepped IBehaviors exist (Behaviors.cs:339 PlayArbitraryAnimBehavior, :403
    ReactBehavior, SingingBehavior.cs:53). The stack has no Resume on IBehavior, so these paths are MISSING (visible), not a guess.

B11 BehaviorManager.cs:630-636. Order: the class test for the UI game request is taken BEFORE SelectUIRequestGameBehavior
    (classBefore = Current?.Class; select();). Engine 0x5a2faa calls SelectUIRequestGameBehavior, clears +0x38 (0x5a2fb4), THEN loads the
    running behaviour (0x5a2fb8..0x5a2fc8) and compares its class byte to 0x2e (0x5a2fd6/0x5a2fe0; RequestGameSimple = 0x2e, computed from
    BehaviorClass.g.cs). Fix fact: read the class after the select.

B12 Floats (CHECKLIST 4). The engine computes the M8-002/003 score path in float: stamps are floats from BaseStationTimer (0x5bd11a,
    0x5beea8..0x5beebc), vsub.f32 for now - stamp (0x5bef12), GraphEvaluator2d::EvaluateY(float) 0x804bd0..0x804c40 (float compares,
    vdiv.f32 then vmul.f32 then vadd.f32, and a "ble" against 1e-5 at 0x804c14..0x804c1c that returns the LEFT node's y), vmul.f32
    (0x5bef98). The batch converted only the +0x108 sum (StopWithoutImmediateRepetitionPenalty) and left RepetitionPenalty.For
    (_graph.At(nowSec - last) in double), Ran (double stamp), Graph2d.EvaluateY (Activities.cs:10-19: double, no 1e-5 rule, "dx <= 0"
    returns the RIGHT node) and DecayGraph.At (Mood.cs: its doc cites the 1e-5 rule the code lacks) in double. Constants checked and
    fine: 15.0f 0x41700000 (0x5bcf36), 60.0f 0x42700000 (0x5c017e/0x5c02de), 1.0f 0x3f800000.

## QUEUED (cosmetic or visibility, do not block)

Q1  Invented log wording (choice 10). Engine strings, all via sChanneledInfoF on "Behaviors": tag "BehaviorManager.Update.BehaviorComplete"
    with format "Behavior %s returned  Status::Complete" (two spaces; 0x5a30de/0x5a3260) and a DIFFERENT message for status 0,
    "BehaviorManager.Update.FailedUpdate" (0x5a3148); resume failure "BehaviorManager.ResumeFailed" (0x5a2c94/0x5a2c96) and success
    "BehaviorManager.ResumeBehavior" / "Successfully resumed" (0x5a2d24/0x5a2d2a). The C# prints its own text (one space; Selected reasons
    "tried to resume, but it would not resume" and "resumed after the reaction") and collapses status 0 and 2 because IBehavior.Update
    returns bool.
Q2  Silent no-op seams: BehaviorManager.cs:624-625 EnsureRequestGameIsClear (called every tick in production, since ManagerByte0x20
    defaults to 0), SparkBehaviorDisables (SteppedBehavior.cs:340), StopHelperWithoutCallback (:598), CallToListeners (Behaviors.cs:305),
    IManagedActivity.Update (FreeplaySystem.cs; empty: the engine ActivityFreeplay::Update 0x5ad9d0 forwards to the sub-activity
    vtable+0x20, so the forward is verified and only the unbuilt sub-activities are missing). Each is in a comment; none is visible at
    run time.
Q3  Not modelled in Init/Stop: Init writes +0xb0 = 0 (0x5bccfa) and warns IBehavior.Init.ActionsInQueue (0x5bcc7e); Stop starts with the
    helper stop (0x5bd0f4..0x5bd102); IsRunnableBase logs "Behavior %s is already running" (0x5bd7aa).
Q4  Stale comments: CozmoAnimations.cs about line 414 still says a locked action is retried next tick (0x005404a8), contradicted by the new
    fail-with-0x03000019 path; PROJECT_STATE.md:54 and research/20260930-B-CORE-verify-3-4.md:55 (see ActionRunnerWho below).
Q5  Outside this batch (M10-004, unchanged): CheckReactions warns "Multiple behaviors switched" only after a previous successful switch;
    the engine sets its flag after any attempt (0x5a3744..0x5a3794).
Q6  ChooseAndSwitch is a parallel selection path (only tests and tools call it; documented as M8-004).
Q7  Timeout vs completion order inside one tick: IAction::UpdateInternal tests the timeout first (0x540da2 bge); the C# drains
    completions first (SteppedBehavior.cs:501, 529).

## Choices to confirm, one by one

(2) Replaced/Cancelled/Error = failed child: NOT settled -> B7.
(3) Unresolvable trigger fails the action: SETTLED (0x54445e..0x5445f4, result 0x0300000C); code and wire missing -> B8.
(4) Lock before resolve: the order of the TEST is settled (0x540428..0x540592), the TAKING is wrong -> B8.
(5) Manager always disposes the scope: the engine does not -> B9.
(6) _current set before Init: the engine does not -> B1.
(7) Non-stepped Resume: invented -> B10.
(10) Log wording: invented -> Q1.
ActionRunnerWho: the claim "does not exist" is TRUE for the code and the binary. No hit in git grep at HEAD or in the working tree under
cozmo-stack, and the string is absent from libcozmoEngine.so (the nearest symbol is MovementComponent::WhoIsLocking). The HEAD code used
"play-"+counter (CozmoAnimations) and _lockOwnerCounter; the PROJECT_STATE / B-CORE statement that the C# uses one constant
ActionRunnerWho was never true of this tree.

## Supported (opened and confirmed)

- IBehavior::Update 0x5bd074: +0xa0 set and +0x84 == 0 -> 2, else vtable+0xc (UpdateInternal 0x5bda56: +0x84 ? 1 : 2).
- Init order 0x5bcb54: spark gate +0xd8, 0x0100 at +0xa0, callback cleared, +0x34 = now, vtable+0x48, +0x80++ on zero, spark lock, +0x114 = 0
  (modulo B5/Q3). Resume 0x5bceac: triggers 0 and 0x14 (CliffDetected, UnexpectedMovement per the enum), counter stored first, signed blt,
  +0x118 = now + 15.0f, TriggerEmotionEvent for TooManyResumesCliffOrMovement, normal path +0xa2 / +0x34 / vtable+0x4c.
- IsRunnableBase check ORDER 0x5bd778..0x5bd8c8; IsRunnableScored 0x5bda28 (float compare); IsRunnable 0x5bd750.
- PlayAnim: vtable +0x4c 0x5bff1e returns 1; IsRunnableInternal 0x5c013a; StartPlayingAnimations 0x5c0158 (single trigger, numLoops
  [+0x128], 60.0f, tracks 0; lift-safe ctor 0x544710 ORs 2 when [[robot+0x284]+8] != -1 and robot+0x355 == 0); StartSequenceLoop 0x5c0294
  (signed bge, ignoreFailure 0 at 0x5c02f6, AddAction 0x54ec7c, CompoundActionSequential::UpdateInternal 0x54f70c/0x54f81a);
  PlayAnimationAction ctor FLT_MAX rule 0x543c96..0x543cb2; LockTracks with mask 0 sends nothing (0x640098..0x640192).
- IActionRunner::Update locked-track fail 0x03000019, no retry (0x540572..0x54057c; ActionQueue::Update 0x53f5f4 deletes). The completion
  callback does run for it: DeleteActionAndIter 0x53f9e4 calls PrepForCompletion at 0x53fa24, which settles the A2 "still UNKNOWN" for the
  locked-track case.
- IAction::UpdateInternal timeout: now >= start + vtable+0x2c (0x540da2 bge) -> 0x03000018 (0x540e80..0x540e82).
- BehaviorManager::Update order and gates 0x5a2f68..0x5a31be (activity vslot 0x20; 0x355 / +0x20 ensure; UI request; reactions; the three
  gates 0x5a3068..0x5a3074; UI fallback 0x5a307c..0x5a309a; IBehavior::Update 0 or 2 -> Finish(trigger != 0x16) 0x5a311e/0x5a319c);
  FinishCurrentBehavior 0x5a38c4; TryToResumeBehavior 0x5a2b40 (restore gate on +8 and ActionList::IsEmpty, resume with the info trigger,
  {behaviour, none, 0x16} on success, empty switch on failure); ChooseNextScoredBehaviorAndSwitch 0x5a2a20; IsRunnable-then-carry-on
  0x5a1e76..0x5a1e94; InitFailed clears info.current and still stores the info 0x5a1efc..0x5a1f12.
- Stop 0x5bd08c order (+0xa1 clear, StopInternal, +0x30 stamp, StopActing(0,0), undo: reaction locks, idle, motion profile, track map in
  key order, light patterns); StopWithoutImmediateRepetitionPenalty 0x5beea0 (Stop first, then +0x108 = now + 1.0f in float).
- ReadFromScoredJson flat graph defaults 0x5bc554..0x5bc566 and 0x5bc666..0x5bc678; EvaluateScore branches 0x5bef60..0x5beffe; repetition and
  running penalty "<= 0 -> 1.0" 0x5beeea..0x5beef8, 0x5bef2a..0x5bef34.
- Tests with binary-derived expectations: the 0x42700000 bit assert, the loop / ignoreFailure / 60 s tests, the key-order and name-order
  release tests, PlayAnim never resumes, the ExpressNeedsTransition value seam. Circular or weak: the PlayAnim / IsRunnableBase gate tests
  drive seams the production path never sets (B3); the lock-owner test derives its expected key from NextActionTag itself (the engine
  counter value is not observable); ScoredActingStateChangedClearsTheBonusOnEveryActionStartAndStop encodes the contradicted "two callers"
  claim (B4).
- Live entry: BehaviorManager.Update <- FreeplaySystem.Tick (FreeplaySystem.cs:158) <- FreeplayStack (FreeplayStack.cs:240); the Reactions
  and Sing tools call manager.Update directly. FreeplaySystem sets manager.Activity = this in its constructor.
- Checked and NOT a defect: AnimationScheduler _done is created with RunContinuationsAsynchronously (AnimationScheduler.cs:283-284), so the
  scheduler-lock / behaviour-lock inversion I suspected through the synchronous ContinueWith (SteppedBehavior.cs:723) does not occur; the
  behaviour _gate is held across StopOwnAnimation -> scheduler gate (SteppedBehavior.cs:531-541) in one direction only.

# Round 2 (re-verification of the fixes; Claude Sonnet 5.5, read-only, own capstone disassembly of libcozmoEngine.so)

Commands: fidelity.py --check OK (418 records, statuses unchanged; the M8 approval date and sha moved only; no frozen field changed).
dotnet test tests/Cozmo.Protocol.Tests --filter Behavior|Freeplay|Correction|DerivedState|Fidelity: 350 passed, 0 failed.
Verdict: FAIL (2 BLOCKING items; the rest QUEUED).

## Per finding (Round 2)

B1 FIXED. BehaviorManager.cs:705-749: StopAndNullify, IsRunnable (log, carry on), StartAsync, only then SetRunningAndResumeInfo (:746). Matches 0x5a1e6a / 0x5a1e76 / 0x5a1e94 / 0x5a1f12; the Init-failure path (0x5a1e98 non-zero -> info.current null 0x5a1efc, info still stored, DAS 0x5a1f1c) and the return value (1 only when Init returned 0, 0x5a1f0c) match. Test InitRunsBeforeTheManagerStoresTheRunningInfo is derived from the addresses (not circular).

B2 PARTLY (visible seams). :760-786 stores the three fields and reports MISSING for (a) ReactionTriggerTransition, (b) UpdateRobotPropertiesForReaction, (c) the cube-light stop. The CONDITIONS guarding the seams do not match the engine (0x5a209e..0x5a2270): the engine skips everything only when old == 0x16 and new == 0x16 (0x5a20ac..0x5a20b6), so the message is sent also for old == new != None; the properties write (robot+0x254 -> +0xd4, robot+0x2c7, 0x5a2102..0x5a2110) happens only on a None <-> non-None boundary and writes (new != 0x16) (0x5a20e2..0x5a2100); the light stop (0x5a2216..0x5a223e) is gated on the new behaviour class byte at +0x64 != manager byte +0x84 (0x5a2144..0x5a2168), trigger flags (0x5a217c..0x5a218e) and old != new current. C# :774 fires on any trigger change and :778 passes (trigger != null) on every change, including reaction -> reaction (engine: no write); oldCurrent at :763 is always null (StopAndNullify ran), so :781 is true for every non-null new current. Seams are unattached so nothing is wrong on the wire yet; QUEUED (correct before a seam is attached or the record settled).

B3 PARTLY. Wiring: robot+0x355 = Sensors.OffTreadsState != OnTreads (SteppedBehavior.cs:193; engine 0x5bd864; writer 0x51208e; enum OffTreads.cs, OnTreads = 0) and robot+0x284 -> +8 = Motion.IsCarryingObject (:197) are wired; gate order matches 0x5bd778..0x5bd8c8 line by line. PlayAnim overrides 0/0/1 (Behaviors.cs:163-169): IBehavior vtable slots at vptr+0x20/+0x24/+0x28 = 0x1026508/0x102650c/0x1026510 = 0x5bf04c (ret 0), 0x59ec12 (ret 0), __cxa_pure_virtual (ARM_ABS32 reloc); BehaviorPlayAnimSequence 0/0/1. CONFIRMED. Residual: base RunnableGate20/24/28 (SteppedBehavior.cs:229-233) answer true for every class except PlayAnim; table in (b). Production never wires 0x34a. Test gap: no test drives the 0x355 gate through production (Sensors.OffTreadsState); only the seam RobotState355 (BehaviorFrameworkTests.cs about 567 and 999). The 0x284 gate IS tested through the production wire (TheCarryingGateIsReadFromTheRobot).

B4 FIXED. SteppedBehavior.cs:663-667 ActingEnded: handle == +0x84, then +0x84 = 0, then ScoredActingStateChanged (0x5be1ec..0x5be208 verified); callback only when +0xa1 and +0x98 (CallbackMayRun :670, 0x5be20a..0x5be21c). Test ScoredActingStateChangedClearsTheBonusOnActionStartStopAndCompletion covers start, completion, stop and Stop; non-circular.

B5 FIXED. StopOnNextActionComplete :465-472 and InitLifecycle :358 discard the completion callback (epoch); engine 0x5bd694..0x5bd6b4 and 0x5bccc6..0x5bcce6 (inline or heap std::function destroy, +0x98 := 0) verified. Caveat: only the animation actions callbacks are epoch-gated; Wait/WaitUntil/CalibrateHead callbacks (:818-875) are not (see (a)).

B6 StartActing gates FIXED (:645-654): order +0xa0 refuse, NotRunning, AlreadyActing, then +0x84 set and ScoredActingStateChanged(true) (0x5bdad4..0x5bdb5c). NotQueued is documented as not modelled (0x5bdb6c; QueueAction returns 0 = OK: 0x5bdb70 beq 0x5bdc0e returns r6 = 1). Wait/WaitUntil/CalibrateHead leave +0x84 untouched: see (a).

B7 FIXED as MISSING: every end of the animation counts as success (:804-808), reported once; matches the animEnded-only engine model. QUEUED: the report has no production subscriber.

B8 FIXED. RunTriggerAction :727-749: test (0x540440), then LockTracks before the clip is resolved (0x540584..0x54058e, then Init 0x540592), failure unlock (PrepForCompletion 0x540628 -> 0x541210..0x54122a unlocks unless result == 0x02000001), result codes 0x0300000C (0x544444..0x544448, 0x5445f4) and 0x03000001 (0x543ffc/0x544132). Test ALockedActionWhoseInitFailsStillSendsTheLockAndTheRelease expects DisableAnimTracks{2} then EnableAnimTracks{2}: derived from the addresses. CozmoAnimations.PlayTracked heldLockMask/heldLockOwner: released at animation end with the held owner; a refused stream is released by Fail(). OK. Weak test: AnAnimationActionLocksItsMaskUnderItsActionId derives the owner key from NextActionTag itself (QUEUED).

B9 FIXED. StopAndNullifyLocked :958-980: Stop and scope dispose only when +0xa1 (0x5a203e..0x5a2046); an Init failure (:742) and a failed resume (:898) park the scope in _orphanScopes. Test AFailedInitLeavesItsScopeHeldUntilTheNextStop non-circular.

B10 PARTLY: counter, refusal, +0x118 and the TooManyResumes event are applied to non-stepped behaviours (BehaviorManager.cs:924-941; 0x5bcf16..0x5bcf7c) and reported MISSING; the normal path (not IsRunnable -> failed, else StartAsync) is still the invention (engine: +0xa2, the +0x34 stamp, ResumeInternal vtable+0x4c = IsRunnableBase + vtable+0x50 + InitInternal, spark lock). Unchanged from HEAD, now visible only to a test subscriber. QUEUED (not a regression).

B11 FIXED. BehaviorManager.cs:630-638 selects, clears +0x38, then reads Current?.Class (0x5a2faa, 0x5a2fb4, 0x5a2fc8, 0x5a2fd6); RequestGameSimple is index 46 = 0x2e in BehaviorClass.g.cs (computed). No test exists.

B12 PARTLY, see BLOCKING 1.

## (a) B6: Wait/WaitUntil do not take +0x84 - UNSUPPORTED claim (SteppedBehavior.cs:814-826)
The doc says the transcribed classes run Wait in parallel with an animation (an engine CompoundActionParallel, one action). That holds for AcknowledgeCubeMoved (WaitAction ctor sites 0x6022fe and 0x60246e are followed by CompoundActionParallel + StartActing<BehaviorAcknowledgeCubeMoved>). Counter-examples where the engine starts a STANDALONE WaitAction through StartActing (so +0x84 is set while it waits): BehaviorReactToReturnedToTreads::InitInternal 0x608500 (new WaitAction(robot, 0.5f) 0x608514, then StartActing<ReactToReturnedToTreads> 0x608524; C# counterpart OffTreadsBehaviors.cs:331 Wait(0.5, CheckForHighPitch)); BehaviorDriveInDesperation::TransitionToIdle 0x5d8ef0 (WaitAction, StartActing 0x5d8fd4; ExplorerBehaviors.cs:351); BehaviorKnockOverCubes::TransitionToKnockingOverStack 0x5c34a8 (0x5c35ea); BehaviorPounceOnMotion::TransitionToWaitForMotion 0x5f8ed4 (0x5f8f74); ReactToPickup::InitInternal 0x607750 and ReactToMotorCalibration::InitInternal 0x6065f0 (WaitAction followed by StartActing). Consequences in the stack: StartActing is not refused while a standalone wait runs; IncreaseScoreWhileActing drops the bonus; UpdateStatus (+0xa0 and +0x84 == 0) returns 2 at once after StopOnNextActionComplete although the engine action is still pending; the Wait/WaitUntil/CalibrateHead callbacks survive StopOnNextActionComplete (+0x98 destroyed in the engine). It is an invention stated as fact and, for the standalone cases, contradicted. Pre-existing behaviour (HEAD Wait is identical), but the batch adds the comment and presents the +0x84 contract (B4/B5/B6) as complete. BLOCKING for the claim (BLOCKING 2); the honest state is a MISSING seam, not the assertion.

## (b) B3: leaving the base gates true
The seam is reported (ReportMissing, once per class and message) but (i) the fallback is allow while the engine base answers 0, 0, pure virtual, and (ii) the per-class values are NOT unrecovered: they are in the vtables (relocation at vptr+0x20/+0x24/+0x28, vptr = vtable+8). I read them for every Behavior* vtable (ret0/ret1 decoded from the target: movs r0,#n; bx lr):
 ReactToRobotOnBack, ReactToRobotOnFace, ReactToRobotOnSide, ReactToRobotShaken, ReactToPlacedOnSlope, ReactToReturnedToTreads: 1,0,1 (differ from the C# true,true,true only at +0x24, which is unwired).
 ReactToOnCharger: 1,1,1 (no change). BehaviorWait: 1,0,1.
 ReactToImpact: 0,0,0. ReactToUnexpectedMovement, ReactToMotorCalibration, ReactToFrustration, AcknowledgeCubeMoved, AcknowledgeObject: 0,0,1.
 PlayAnimOnNeedsChange, EarnedSparks, BuildPyramid, BuildPyramidBase: 0,0,1. ExpressNeeds, PickUpAndPutDownCube: 0,0,0. DriveInDesperation: 1,0,0. FindFaces, ExploreLookAroundInPlace: 0,0,(byte at +0x120).
What would change TODAY (only the two wired gates): off treads the engine refuses ReactToImpact, ReactToUnexpectedMovement, ReactToMotorCalibration, ReactToFrustration, AcknowledgeCubeMoved, AcknowledgeObject, PlayAnimOnNeedsChange, EarnedSparks, BuildPyramid*, ExpressNeeds, PickUpAndPutDownCube, FindFaces, ExploreLookAroundInPlace (+0x20 = 0) while the C# runs them; carrying, the engine refuses ReactToImpact, ExpressNeeds, PickUpAndPutDownCube, DriveInDesperation (+0x28 = 0) and the C# runs them. Classification: QUEUED while M8-001 stays IMPLEMENTATION_GAP (no regression: HEAD had no gate), but the wording that the class own slot is not recovered (:229-233) is false, the data above is the next row, and it becomes BLOCKING the moment any M10 record depending on these classes is settled.

## (c) B12
GraphEvaluator (Activities.cs:10-35) vs 0x00804bd0..0x00804c44: operation order (vsub, vdiv, vsub, vmul, vadd), float width and the 1e-5f constant (0x3727c5ac read at 0x804c48; ble 0x804c14..0x804c1c returns the LEFT node) all match. The first-node test (C# x <= x0; engine x0 > x then the loop x1 >= x) agrees for every non-NaN x; for NaN the engine falls through to the last node y and the C# returns NaN (QUEUED edge). Mood.cs DecayGraph.At -> GraphEvaluator is correct: I enumerated every caller of EvaluateY; the engine Emotion code calls it at 0x6795b4/0x6795d2 and MoodScorer at 0x67ca14, and the repetition and running penalties reach it through veneer 0x8cbf9c -> PLT 0x4b1ee4. The Mood.cs hunk has no fidelity tag of its own (tag is on GraphEvaluator, M8-003; DecayGraph row is M7): QUEUED. RepetitionPenalty.For/Ran/StopWithoutImmediateRepetitionPenalty are float now (0x5bef12, 0x5bd11a, 0x5beeb0..0x5beebc): correct.

## (d) B11 has no test
Does not block: the branch is unreachable in production (nothing sets UiGameRequestPending or SelectUIRequestGameBehavior; the manager throws NotSupportedException when pending without a select), the code matches 0x5a2faa..0x5a2fe0, and M8-012 is not being settled. QUEUED; the record cannot be settled without a test of the order (class read after the select).

## (e) New defects and residuals
- BLOCKING 1 (B12 residual; CHECKLIST 4 width and operation order), live path ScoringChooser -> ScoredBehaviorEntry.Evaluate: Activities.cs:112 g2.EvaluateY(nowSec - last) subtracts in double and casts after; the engine subtracts float - float (vsub.f32 0x5bef12; stamp float 0x5bd11a). Activities.cs:100 rp.EvaluateY(r): r is the manager double nowSec - _startedSec; the engine input is float(now) - float(+0x34) (vsub.f32 0x5bef4e); the +0x34 stamp is used only for the "<= 0" test. Activities.cs:88,94,100,112: score is double; the engine adds and multiplies in float (vadd.f32 0x5bef88, vmul.f32 0x5bef98, and 0x5beffe). IBehavior.cs:684 IsSuppressed: nowSec < until in double; the engine compares float (vcmpe.f32 0x5befea; bpl: penalty applied when now >= +0x108). Numeric proof the routes differ: now = 12345.678, stamp = 12000.1f: double route 345.5784, engine route 345.57812. Test TheScoreAppliesNoPenaltyForAZeroStampOrAMissingGraph uses integer seconds and a x2.0 score, so it cannot see any of this (weak, not circular).
- BLOCKING 2: the Wait/WaitUntil/CalibrateHead +0x84 claim in (a).
- QUEUED: ReportMissing has no production subscriber (SteppedBehavior.cs:98-105; the only subscriber is a test) and marks the message seen even when nobody listened, so in production every MISSING above (gates, 0x34a, transition message, light stop, replaced/aborted tag, non-stepped resume) is silent. The manifest unresolved text for M8-001/003/005/007/012 still carries only the 2026-09-29 audit text; the seams exist only in code comments and Correction A3 (as facts).
- QUEUED: RunningScoreBonus (auto-property double) is reset by ActingEnded from the animation thread (ContinueWith ExecuteSynchronously, SteppedBehavior.cs:800) while IncreaseScoreWhileActing does += on the manager thread: a lost update is possible; the engine does both on one thread.
- QUEUED: SetRunningAndResumeInfo seam conditions (B2); Evaluate defaultPenalty parameter now unused (Activities.cs:84); ChooseAndSwitch still a parallel selection path (Q6); the PlayAnim 0x355 gate has no production-wire test.

# Round 3 (Claude Sonnet 5.5, read-only, own capstone disassembly of libcozmoEngine.so)

Commands: fidelity.py --check OK (418 records); dotnet test Behavior|Freeplay|Correction|DerivedState|Fidelity: 352 passed, 0 failed.
Verdict: FAIL (3 BLOCKING; rest QUEUED).

## Supported
- Round 2 BLOCKING 1 fixed: ScoredBehaviorEntry.Evaluate (Activities.cs:85-120), RepetitionPenalty.For/Ran/IsSuppressed/StopWithoutImmediate... (IBehavior.cs:670-714) match 0x5bef12 (vsub.f32 float(now)-float(+0x30)), 0x5bef4e (+0x34), 0x5bef88 vadd.f32, 0x5bef98 vmul.f32, 0x5beffe (vmul via 0x4b29f4), 0x5befea (vcmpe.f32 now vs +0x108; bpl = apply, NaN applies; C# NaN not suppressed), 0x5beeb0..bc (vadd.f32 1.0f), 0x5bd11a (+0x30 float store). GraphEvaluator 0x804bd0..0x804c44 (vsub,vdiv,vsub,vmul,vadd; 1e-5f = 0x3727c5ac at 0x804c48; left-node return; NaN falls to the last node) matches. Mood.cs DecayGraph.At -> GraphEvaluator, tagged M8-003: ok.
- Test TheScorePathIsFloatWithTheEnginesOperationOrder: NOT circular. Expectation uses the engine's operation order, and the dt bits 345.57812f vs 345.5784 (double route) are pinned independently; the double route differs by 2.8e-7 in y, more than a float ulp at 0.34, so it discriminates. Suppression half discriminates old (double now+1.0) from new. Weakness (QUEUED): running branch, runningPenalty and the +0x34 clock are not exercised; the x3.0f vmul cannot show width.
- Wait/WaitUntil/CalibrateHead standalone: engine standalone StartActing confirmed for WaitAction (0x608500, 0x5d8ef0, 0x5c34a8, 0x5f8ed4, 0x607750, 0x6065f0), WaitForLambdaAction (ReactToMotorCalibration::InitInternal 0x6061f8: ctor 0x55b554 timeout 0x40a00000 then StartActing<> 0x606254) and CalibrateMotorAction (ctor then StartActing at 0x607ca8, 0x60809a, 0x608666, 0x6088a2). StopOnNextActionComplete destroys +0x98 (0x5bd694..b4), epoch gate implemented (SteppedBehavior.cs:824-825,846-848). HandleActionComplete order 0x5be1ec..0x5be21c matches ActingEnded + CallbackMayRun.
- RunningScoreBonus lock: _bonusGate is a leaf lock (only ever taken last; ActingEnded under _gate takes it, nothing takes _gate under it): no deadlock. NaN GraphEvaluator edge matches engine.

## BLOCKING
1. SteppedBehavior.cs:884-889 ParallelAction.ChildDone and PlayTriggerInParallel :870-871 / CubeReactions.cs turn child (done via both.ChildDone() ignoring t.Result) vs CompoundActionParallel::UpdateInternal 0x54fab0..0x54fbd0: the C# ends the compound only when ALL children have ended, counting a failed child as ended. The engine: per child IActionRunner::Update (0x54faf6); category = result>>24 via tbb 0x54fb0e: 0 SUCCESS -> PrepForCompletion + StoreUnionAndDelete, continue (0x54fb3a..44); 1 RUNNING -> compound stays RUNNING (r8=0x1000000, 0x54fb52/0x54fb5e); 2,3 -> RunCallbacks(result) 0x54fb26 then ShouldIgnoreFailure 0x54f171 (per-child functor map at +0x8c, EMPTY for the list constructor 0x54e991 used by both AcknowledgeCubeMoved sites: its AddAction call passes ignoreFailure 0, 0x54ea34) -> returns 0, so `cmp r0,#1; bne 0x54fbcc` returns the child's FAILURE result at once (remaining children abandoned); 4 -> RetriesRemain 0x54fb1a, else the same. Only when no child is RUNNING and none failed: RunCallbacks(0) and SUCCESS (0x54fb64). HandleActionComplete 0x5be1e6 then calls the callback for ANY result (the template StartActing<AcknowledgeCubeMoved>(action, void (T::*)(Robot&)) wrapper 0x602625 -> 0x4b2b5c takes no result). Corrected fact: an animation child that fails (NoAnimationForTrigger 0x0300000C, TRACKS_LOCKED 0x03000019, stream refused 0x03000001, timeout 0x03000018 - all category 3) or a turn child that fails ends the compound on that tick and the callback runs at once; the wait child is never awaited. The C# keeps +0x84 for the full 0.5 s and delays the callback.
   Test BehaviorFrameworkTests.cs:2028-2038 (pair: "the animation child fails at once (no map): the wait is the other", expects both==0 at t=0 and ==1 only after the 0.5 s wait) pins this non-engine behaviour; its expectation comes from the implementation's choice, not from the cited 0x6022fe.. addresses: CIRCULAR/CONTRADICTED. Production relevance: any TRACKS_LOCKED or unmapped CubeMovedSense in AcknowledgeCubeMoved.
2. SteppedBehavior.cs:801-804 (animation-completion race "fix") introduces a new ordering race: the completion is enqueued into _pending (:802) BEFORE ActingEnded clears +0x84 (:804). The manager thread's drain in Update (:536-537) takes no _gate, so it can run the callback in the window between the two statements; a callback that calls StartActing/PlayTrigger/Wait then sees HasCurrentAction true and is refused with IBehavior.StartActing.Failure.AlreadyActing (:651), ending the behaviour's chain. HEAD enqueued after clearing (gap in Busy instead). Engine HandleActionComplete 0x5be1fc stores +0x84 = 0 (and 0x5be208 ScoredActingStateChanged) strictly BEFORE the callback is reachable (0x5be216), single threaded. Corrected fact: +0x84 must be zero before any callback of that action can execute; the Busy gap and the +0x84 gap must both be closed. (Complete() :720-724, the synchronous path, has the right order.)
3. Activities.cs:90 / :89 comment "The engine computes the score in float": EmotionScore (Activities.cs:128-150, feeding the float cast) sums and means in double and compares Math.Abs(y) < 1e-5 in double. MoodScorer::EvaluateEmotionScore 0x67c9b8: vadd.f32 s18,s18,s22 (0x67ca5a), count converted vcvt.f32.u32 and vdiv.f32 (0x67ca6e..72), veto compare vcmpe.f32 against the float literal 0x3727c5ac (0x67c9da/0x67ca50, pool 0x67cab4), graph value float from 0x4b1ee4. Not changed by this batch (pre-existing, tagged M8-003 and M13-010) but it is the first term of the live Evaluate path the batch now declares float; CHECKLIST float-width item. Mark BLOCKING unless the manager records it explicitly as a visible M13-010/M8-003 gap.

## QUEUED
- Test AStandaloneWaitIsTheCurrentActionAndAParallelPairIsOne, middle block (BehaviorFrameworkTests.cs:2021-2026) is vacuous: `d` waits 1.0 s but only Update(ctx,0) runs, so called==1 holds even without the callback being destroyed; the epoch gate is untested there.
- IncreaseScoreWhileActing (SteppedBehavior.cs:285) checks HasCurrentAction outside _bonusGate: a clear between check and add leaves a stale bonus (engine single-threaded). Narrow window.
- WaitUntil standalone-ness is proven only for WaitForLambdaAction (0x6061f8) and CalibrateMotorAction; the other C# WaitUntil users (ChargerBehaviors.cs:43, ManipulationBehaviors.cs:142, ObjectBehaviors.cs:284, CubeGameBehaviors.cs:887, OffTreadsBehaviors.cs:536) model engine WaitForImages/visual-verify/other actions whose StartActing wiring is not cited.
- Mood.cs hunk: tag M8-003 now present; DecayGraph row belongs to M7 (cosmetic).

# Round 4 (Claude Sonnet 5.5, read-only; own capstone disassembly of libcozmoEngine.so)

Commands: fidelity.py --check OK (418 records); dotnet test Cozmo.Protocol.Tests filter Behavior|Freeplay|Correction|DerivedState|Fidelity: 354 passed, 0 failed. Verdict: PASS (no BLOCKING; QUEUED below).

## Round 3 BLOCKING items
1. ParallelAction.ChildDone (SteppedBehavior.cs:902-916), PlayTriggerInParallel :870-872, CubeReactions.cs:322-356: FIXED. Re-read 0x54fab0..0x54fbd0 and decoded the tbb table at 0x54fb12 (bytes -> 0:0x54fb40 success continue, 1:0x54fb50 running, 2/3:0x54fb22 RunCallbacks + ShouldIgnoreFailure, cmp #1 / bne 0x54fbcc returns the failure, 4:0x54fb18 RetriesRemain). The C# ends on the first failed child (success only when the last child ends), runs the callback once (_ended guard), clears +0x84 (ActingEnded) before the callback (0x5be1fc < 0x5be216), cancels the other children with a ReportMissing. The failed turn child passes ActionOutcome.Failed() (CubeReactions.cs:354). Pair test (BehaviorFrameworkTests.cs:2036-2050) now expects both==1 at t=0 with HasCurrentAction false and no second callback at 600; expectation cites 0x54fb36..0x54fbcc, not an implementation constant: NOT circular. It covers a failed animation child only; no test fails the turn child (QUEUED).
2. Completion ordering (SteppedBehavior.cs:795-808): FIXED. Under _gate: ActingEnded(+0x84=0, bonus cleared) -> Enqueue -> _owns/_acting cleared. +0x84 is 0 before any callback is dequeued; _acting stays true until after the enqueue, so Busy has no gap (EndOfStart :507 runs on the manager thread after the drain). A callback dequeued in the window and starting an action blocks on _gate (RunTriggerAction :780) until the continuation leaves, so the stale _acting=false cannot clobber the new action. Lock order: _gate -> _bonusGate (leaf); nothing takes _gate under _bonusGate; the manager's drain holds no _gate: no deadlock. Complete() :720-724 (synchronous) has the same order. HandleActionComplete 0x5be1ec..0x5be21c matches.
3. EmotionScore (Activities.cs:140-150): FIXED. 0x67c9b8..0x67ca82: vldr s22,[r0,#0x18] float load; graph float (0x4b1ee4); |y| via vneg/vmovmi, vcmpe.f32 vs s20 = pool 0x67cab4 = 0x3727c5ac, bmi (NaN not vetoed, same as C# Math.Abs(y) < Epsilon); vadd.f32 s18 (0x67ca5a); count vcvt.f32.u32 + vdiv.f32 (0x67ca6e..72); empty returns 0. C# matches in order and width. Test TheEmotionScoreIsAFloatMeanWithAFloatVeto expects 0.59999996f = 0x3f199999 (I recomputed ((0+0.3f)+0.6f)+0.9f)/3f in float32; the double route gives 0x3f19999a): discriminating, not circular.

## QUEUED
- No test fails the turn child of AcknowledgeCubeMoved (DerivedStateTests.cs FakeLocator.CompleteTurn only completes true).
- ACompletionCallbackMayStartTheNextAction (BehaviorFrameworkTests.cs ~2063) is a probabilistic stress test and returns silently without the OBB; it cannot deterministically pin the window.
- The EmotionScore veto test value 5e-6 does not discriminate float vs double epsilon; MoodState holds emotions as double (Mood.cs:232) where the engine loads a float (0x67c9f0/0x67ca02): the (float) cast at Activities.cs:147 is not the engine's value if the double accumulation differs; belongs to the M7 float-width item (check 2), keep it visible.
- IncreaseScoreWhileActing now locks _bonusGate around the +0x84 check (fixed for ActingEnded); StopActing's order (clear bonus, then CAS +0x84) still leaves a stale-bonus window under cross-thread use (engine is single-threaded).
- WaitUntil standalone claim is proven only for WaitForLambdaAction (0x6061f8) and CalibrateMotorAction; other WaitUntil callers (ChargerBehaviors.cs:43 etc.) uncited. Engine child order on a simultaneous double failure is list order; the C# uses arrival order.
