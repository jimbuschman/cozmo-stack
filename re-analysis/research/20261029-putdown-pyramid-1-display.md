# PUTDOWN-PYRAMID-1 — display-key followup

2026-10-29. Same source-only reader/startup request gpt-6.1-sol/medium; backend telemetry unavailable. Answers root followup asking for the displayNameKey consumer and downstream effect. Original frozen read.md remains unchanged, SHA25642a9cf261e9914fb941df299f3f2d137256c80ed14fa1ecab89331376a327853. No implementation, .NET, git, official metadata, hardware or policy changes.

known.py preceded displayNameKey, SendDasTransitionMessage and005A2560 checks. Same verified primary binary. Native sources are native-display.txt/native-display-report.txt, prior native-followup.txt/native.txt. Current whole M8-012 and M8-019 objects were reread and saved in display-records-current.json before ownership conclusions (verbatim below). M8-012 currently IMPLEMENTATION_GAP owns manager tick/switch/finish/reaction; evidence explicitly includes SendDasTransitionMessage005A1F1C as SwitchToBehaviorBase tail. M8-019 currently IMPLEMENTATION_GAP owns RequestCurrentBehaviorEndImmediately and its unresolved contains BehaviorTransition delivery work. No complete owner settlement or contradiction is proposed.

| Narrow step | Primary evidence | Consequence / boundary |
|---|---|---|
| Exact parsed field |005BBFB4..BFDA creates key displayNameKey(literal005BC2C4), adds targetthis+4C at005BBFD2, GetValueOptional<string>005BBFDA. | Shipped behavior.PrepareCubes is retained as separate12-byte string field+4C. It is not a proven no-effect/UI-only field. |
| Canonical own identity |005BBC38 storesID+3C;005BBC3E EnumToString;005BBC44..BC5E constructs name+40. PutDownInit005C7FDC..7FE2 passes this+40 into smart lock;005BCE46..CE7E appends_behaviorLock. | Canonical name stays PyramidPutDownBlock, owned lock PyramidPutDownBlock_behaviorLock. Display key neither replaces name nor changes lock identity. |
| Actual supported report consumer |Manager SendDasTransitionMessage005A22B4 uses old/new info;005A22DC loads new behavior r4 from new info r5. With external interface gate005A22C2..22C8 true,005A2408 branches on nonnull new behavior;005A2422 mov r0,r4;005A2424 ldrb r1,[r0,#4C]! reads that exact display string;005A2428..245C copies short/long string,005A2460..2466 assigns it into transition object atsp+18 (=object+8). | When this instance is the new behavior at an actual manager switch, BehaviorTransition string field+8 contains behavior.PrepareCubes. It is an engine API payload consequence, regardless of whether the replaced phone UI renders it. The null-new case constructs empty string005A2438..2450. |
| Delivery join |005A2478..2480 obtains Robot external interface and calls005A2560;helper005A2578 moves BehaviorTransition,005A2580 typed MessageEngineToGame(BehaviorTransition&&),005A2588 invokes actual external interface vtable+1C. SwitchToBehaviorBase invokes SendDasTransitionMessage at005A1F1C. | Real external message delivery edge, not a log-only claim; existing generic report transport/manager ownership retains all its current gaps. No fabricated wire field name/tag/full message implementation is prescribed by this narrow read. |
| Readiness scope |Finite own class005C7F98..84D4 and shared IsRunnableBase005BD778..D968 were checked. Canonical name+40 appears in common diagnostic path, while this display field+4C is not a predicate operand in those finite routines. StrictPriority0060B23E..B27E tests active+A1/IsRunnable; no display read. | Within the checked configured own path and these three caller selection edges, display string does not alter readiness/scoring/motion/timing. This is bounded negative evidence, not a global no-effect claim. Report effect above remains supported. Wider app/UI/debug consumers were not audited. |

There is no new per-alias owner or widening: shared manager transition/report remains current M8-012/M8-019/M8-001 related delivery frontier. The source establishes this exact instance argument without claiming a complete manager/report subsystem. Tests with supplied manager transition input would remain supplied-input units, not proof of automatic activity selection or hardware readiness.

Audit: exploratory rg wildcard paths were invalid under PowerShell, and absent unity/sources folders returned errors; no claim uses those searches. Exact native report consumer settled the question. Helpers retain trailing literal disassembly, which is not cited as code. No source uncertainty remains for the specific display+4C to BehaviorTransition+8 join. Full app payload/transport handling outside that join is its existing boundary.

## Current whole record snapshot

```json
[
  {
    "id": "M8-012",
    "subsystem": "M8-framework",
    "title": "BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate",
    "location": "cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs",
    "status": "IMPLEMENTATION_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204",
    "evidence": [
      "BehaviorManager::Update 0x005a2f70 returns immediately when the manager's init byte at +0 is zero, logging \"BehaviorManager.Update.NotInitialized\" (0x005a2f70/0x005a2f72/0x005a2fea).",
      "It ticks the current activity first: GetCurrentActivity then the activity's virtual Update at vtable+0x20 (0x005a2f78/0x005a2f80/0x005a2f84).",
      "If robot+0x355 is non-zero, or manager+0x20 == 1, it calls EnsureRequestGameIsClear (0x005a2f8e/0x005a2f94/0x005a2f9e).",
      "If manager+0x38 is set it calls SelectUIRequestGameBehavior, clears the flag, and when the previous current behaviour's class is 0x2e (cmp r7,#0x2e at 0x005a2fe0) sets byte +0x220 on the object at manager+0x30 (strbeq.w r1,[r0,#0x220] 0x005a305c).",
      "It calls CheckReactionTriggerStrategies 0x005a3060; a non-zero result suppresses the scored choice and the UI-request switch for this tick (cbnz r5 0x005a3068).",
      "The reaction list is evaluated only when the robot's action list robot+0x250 is empty: the manager latch at +0x4c is ORed with !IsEmpty and CheckReactionTriggerStrategies returns false when the list is empty (0x005a3558..0x005a356c).",
      "If no reaction fired, nothing occupies manager+0x30, and the current behaviour's class is 0x16, it calls ChooseNextScoredBehaviorAndSwitch (ldrb r0,[r0,#0x10] 0x005a3070; cmp r0,#0x16 0x005a3072; blxeq 0x005a3078).",
      "If no reaction fired and manager+0x30 holds a behaviour different from the current one, it calls SwitchToUIGameRequestBehavior (0x005a307e/0x005a3082/0x005a309a).",
      "It ticks the current behaviour with IBehavior::Update (0x005a30bc). Return 0 logs \"BehaviorManager.Update.FailedUpdate\" (0x005a3148) and return 2 logs a debug line; both then call FinishCurrentBehavior(behavior, class != 0x16) (0x005a3128). Return 1 keeps it (cmp r0,#0 0x005a30c0; cmp r0,#2 0x005a30c4).",
      "SwitchToBehaviorBase 0x005a1e6a stops the current behaviour (StopAndNullifyCurrentBehavior), calls IBehavior::IsRunnable(robot) and logs \"BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable\" on false, then IBehavior::Init (0x005a1e94); an Init failure logs \"BehaviorManager.SetCurrentBehavior.InitFailed\" (0x005a1eae), clears the behaviour, and still sets the running/resume info (SetRunningAndResumeInfo 0x005a1f12) and sends the DAS transition (SendDasTransitionMessage 0x005a1f1c).",
      "FinishCurrentBehavior 0x005a38ca: immediate == 1 tail-branches to 0x8cbc9c; otherwise, if manager+0x30 equals the finishing behaviour, it calls EnsureRequestGameIsClear (0x005a38e6) and switches to a default class-0x16 BehaviourRunningAndResumeInfo (movs r0,#0x16 0x005a38f4; SwitchToBehaviorBase 0x005a38fe).",
      "SwitchToReactionTrigger 0x005a25e4 builds a resume info and calls the strategy's ShouldTriggerBehavior (vtable+8) (0x005a262a); only when it returns 1 does it switch (cmp r0,#1 0x005a262c; SwitchToBehaviorBase 0x005a26da).",
      "TryToResumeBehavior 0x005a2b48 bails unless the stored resume head angle differs from a constant and the robot's action list is empty (vldr s0,[pc] 0x005a2b48; ActionList::IsEmpty 0x005a2b60), logging \"Resuming behavior and don't have an action, so setting head angle %f, lift height %f\" (0x005a2b72)."
    ],
    "effect": "the manager ticks, switches or finishes behaviours in the wrong order, or fires a reaction when it should not",
    "provenance": "BehaviorManager::Update and its switch/finish/resume helpers, read at the addresses above",
    "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. NotSupportedException seams (BehaviorManager.cs:632/649); no ActionList (MISSING at :311, :414, :873); the sends at :788-792 are MISSING. Before: built, awaiting strong verification: phantom 0x16 behaviour removed (running info = {current, resume, trigger}); tick order per Correction A2 (activity Update, EnsureRequestGameIsClear, UI request, reactions every tick, scored switch behind three gates, IBehavior::Update then FinishCurrentBehavior(trigger != None)); TryToResume with head/lift restore and no manager IsRunnable pre-check; failure switches to empty; running info stored on Init failure; per-trigger disable-count skip. Still open (seams): the meaning of manager+0x20, SelectUIRequestGameBehavior, SwitchToUIGameRequestBehavior and the +0x220 flag (no test for the class-test order before settling), EnsureRequestGameIsClear and SendDasTransitionMessage bodies, ActionList-is-empty is not wired in production, the concrete activity Update bodies (M7/M15); the stack's ChooseAndSwitch is a test-only parallel path. OV20/21 check (re-analysis/research/overnight/20261014-ov20-21-check.md): owns R4-R7, T1-T4, E1-E5; the SendDasTransitionMessage body 0x005A22B4..0x005A24AC is now read (tags 0x87/0x88, E3 triggers 0x15 Count / 0x16 NoneTrigger); EnsureRequestGameIsClear is not. Citation: SwitchToBehaviorBase is 0x005A1E20 (0x005A1E6A is a call site). Batch check 2026-10-16 (re-analysis/research/overnight/20261016-batch-check.md): BehaviorManager::Update discards the activity Update result (0x005A2F70..0x005A2F8A); a missing current activity is a null dereference at 0x005A2F7E (M1-034; reachable only if the app requests an undefined HighLevelActivity). OV108 answer (re-analysis/research/overnight/20261016-ov105-107-108-answer.md): correction: an app-requested undefined activity (v >= 4) faults first at 0x005B3140 inside SetCurrentActivity (OnSelected(null) from 0x005A11F4), not at 0x005A2F7E; the stack API cannot send v >= 4.",
    "hardware_required": false,
    "live_path": true,
    "test": "BehaviorFrameworkTests"
  },
  {
    "id": "M8-019",
    "subsystem": "M8-framework",
    "title": "BehaviorManager::RequestCurrentBehaviorEndImmediately, as HandleRobotStopped calls it",
    "location": "cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs",
    "status": "IMPLEMENTATION_GAP",
    "effect": "a robot stop with the cliff sensor enabled does not end the current behaviour, or ends it differently",
    "provenance": "Cross-layer split (manager, 2026-10-10): S-7 of re-analysis/research/20261010-m3m4-cross-layer-splits.md, from M4-019 H4.",
    "authority": "libcozmoEngine.so 3.4.0-1204",
    "evidence": [
      "Called at 0x00535454 (blx 0x4AA1C8) from Robot::HandleRobotStopped when the cliff sensor is enabled (gate 0x00535436..0x00535438; M4-019 H4 0x00535432..0x00535480)",
      "re-analysis/research/overnight/20261014-ov20-21-check.md: R2/R3 0x005A3930..0x005A39A6; caller R1 0x00535432..0x00535486 (interface to M4-019 H4)."
    ],
    "unresolved": "RECOVERABLE_GAP: read the body; no record covers it (M8-012 is BehaviorManager::Update's tick). The stack leaves the call unwired with a comment only (Sensors.cs). OV20/21 check (re-analysis/research/overnight/20261014-ov20-21-check.md): IMPLEMENTATION_GAP: the request logs Info \"Behaviors\" BehaviorManager.RequestCurrentBehaviorEndImmediately \"Forcing current behavior to stop: %s\" with no guard, then SwitchToBehaviorBase({null,null,0x16}) with the result discarded, then releases +0x14 then +0xC. A second caller exists (ActivitySparked::CheckIfSparkShouldEnd 0x005B19F0), owned by the activity record.\nB-M8-3 (2026-10-15), bounded build, awaiting strong verification; BLOCKED for request body: actual manager entry now emits unconditional checked Info/Behaviors BehaviorManager.RequestCurrentBehaviorEndImmediately / Forcing current behavior to stop: reason, even uninitialized/null current. Actual FreeplayStack binds this entry to Sensors; cliff-enabled handler calls request then ActionList.Cancel(-1) then RobotStopped. Request stops at explicit stack MISSING; no invocation of known-wrong SwitchToBehaviorBaseCore is presented as recovered. Existing helper stores running fields before ReactionTriggerTransition, omits empty-target DAS/BehaviorTransition, and lacks native shared-control retention/release graph. Thus R3 switch/result-discard/resume-before-current release remain unbuilt. No generic Stop/Finish substitution, no stopping activities/UI request or fabricated transition success. Fixtures verify entry Info and caller order only.",
    "hardware_required": false,
    "live_path": true,
    "test": "; M8CompoundCheckedRowsTests; CompoundActionTests"
  }
]
```
