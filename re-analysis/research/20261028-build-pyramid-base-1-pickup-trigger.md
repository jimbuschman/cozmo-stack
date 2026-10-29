# BUILD-PYRAMID-BASE-1 B1 — supplied pickup trigger

Narrow read requested by manager for builder questionB1. Same explicit reader request gpt-6.1-sol/medium; no backend telemetry inferred. Frozen original reports unchanged. Raw ARMv7 engine is the previously SHA256-verified file. No code, .NET, tests, hardware, git or official metadata edits.

**Answer:** Base caller0x16 is a **one-use pre-animation in the pickup sequential compound**, not the success-response trigger. Its consuming field is PickupBlockHelper+0xF8. No evidence authorizes replacing the existing success-result response sentinel with0x16.

| Step | Exact native data/effect | Primary instruction citation |
|---|---|---|
| B1a producer | Base constructs params at callerSP+18. Writes sentinel0x23F there initially, then overwrites with0x16 before factory call. Radians0, flag bytes0 remain distinct fields. Passes target wrapper Base+124 at callerSP+0 and params pointer at callerSP+4. |005DCC1E..CC46: initialCC22, finalCC32..CC34, flagsCC36;strdCC3A;factoryCC46 |
| B1b factory ABI | Hidden result pointer r0; factory r1; Robot r2; parent r3; target-pointer/params-pointer are stack arguments. Factory pushes24/sub16, then loads its SP+28hex pair (the original callerSP+0/+4), writes them at its currentSP+0/+4 for constructor. Constructs new helper with r0=new,r1=Robot,r2=parent,r3=factory. |005B553C..005B5564;loadpair5554,storepair5558,registermoves555C..5562 |
| B1c receiving storage | Constructor pushes24/sub24; loads target/params from SP+30hex (its incomingSP+0/+4) at7A7E. Target ID is params-independent target+4, stored helper+F4. Loads params[0] at7AB8 and stores helper+F8 at7ABC. Copies Radians from params+4 to helper+FC and flags params+C to helper+104/105. |005B7A4C..7ADC;7A7E,7AAE..7AD0 |
| B1d consuming order | Once StartPickupAction reaches pickup-compound construction, creates CompoundActionSequential, reads helper+F8, compares0x23F. Equal skips insertion. Otherwise creates TriggerAnimationAction with that value, loop1, interrupt1, trackmask0, timeout60f (42700000), finalflag0, adds child with ignoreFailure=false and secondbool=false. This is before the optional face/object-turn children and actual PickupObject child. |005B7CEE..7D52;fieldread7CFC,compare7D00/04/06;ctor7D18..7D32;Add7D36..7D40;laterturngate7D52..7D7C;actualpickup/proxy7E42..7E8C |
| B1e one use | After adding pre-animation, writes0x23F back into helper+F8. This occurs during compound construction, before the compound runs, not upon successful pickup. Later retry construction therefore does not reinsert the supplied trigger after the field was consumed. No changed timing from an added post-success action is established. |005B7D4A movw r0,#23F;005B7D4E str.w r0,[r4,#F8] |
| B1f completion ordering | The whole sequential pickup compound is started with response handling, and completion proxy is pickup child tag. The pre-animation is an ordinary nonignored earlier child, so it precedes pickup; failure propagates under shared sequential semantics. Trigger fieldF8 is not passed to the response wrapper. Wrapper stores original completed-action callback at helper+D8 and result-converter at+A8, then starts the compound through its parent. |005B7E8C SetProxyTag;005B7EA2..7EAA call;005B8018..804A (callback8024..802A,converter802E..8034,StartActing8044) |

Base's0x16 therefore changes robot animation/order before pickup. Native0x23F is the no-pre-animation sentinel for this field; earlier checked normal pickup callers passed0x23F and exercised the skip branch. This is a newly exercised argument branch when Base joins the shared helper, not evidence that normal pickup success should animate0x16. Success/failure response callback internals are distinct and were not broadly re-audited for B1.

## Current-record ownership and bounded impact

Whole current records are quoted below, including title/status/evidence/unresolved. Current M12-009 is EXACT_SOURCE **The dock helper allows two attempts**; it claims the attempt bound, not the whole pickup helper or pre-animation parameter. Its evidence explicitly owns+108/count comparison8192/8196/8198; no contradiction or status weakening follows from the previously unused+F8 parameter. Current M8-011 remains IMPLEMENTATION_GAP, title **The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks**; it names concrete helper gaps, not an EXACT whole-helper claim. Current M15-076 remains IMPLEMENTATION_GAP and owns StackBlocks' helper caller; no whole-helper EXACT claim inferred from it. No current EXACT record identified by this bounded title/address ownership lookup claims all PickupBlockHelper arguments. Base's producer belongs to proposed coherent Base own lifecycle; the receiving field/consumer is a shared helper dependency already recovered at primary instructions here. No new per-caller duplicate record is asserted.

This is an extraction conclusion, not an inventory/status/approval edit or implementation prescription. Manager must compare adopted inventory scope before deciding whether an approval checkpoint applies. Existing lower PlaceRel/DriveTo nonzero-offset missing path remains M12-042/M12-022 and is not changed by B1; no replacement drive or success completion established.

Known.py before native lookup: explicit Python313 ran CreatePickupBlockHelper,PickupBlockHelper::PickupBlockHelper,0x005B8050,0x005B7B48,StartActingWithResponseAnim. Class Base lookup/caller bytes were already checked in frozen report. Native dump.py raw reads005B553C..557A,005B7A4C..7AE0,005B7B48..7EE4,005B8018..804C; sourceconsumer dump retained pickup-start-raw.txt. Native response dump was navigation only for this question, and did not supply unclaimed behavior. Current C# read only to locate questioned branch, never evidence of native behavior. No failed prelaunch/test/build was suppressed.

## Whole current manifest records

```json
[
  {
    "id": "M12-009",
    "subsystem": "M12-manipulation",
    "title": "The dock helper allows two attempts",
    "location": "cozmo-stack/src/Cozmo.Robot/Manipulation/ManipulationSystem.cs",
    "status": "EXACT_SOURCE",
    "authority": "libcozmoEngine.so 3.4.0-1204: PickupBlockHelper::RespondToPickupResult 0x005B8050 reads the attempt count at helper+0x108 (0x005B8192) and retries only while it is 1 or less (0x005B8196 cmp r0,#1 / 0x005B8198 bls); the log literal 2 is at 0x005B814A; the entry point PickupBlockHelper::StartPickupAction is 0x005B7B48",
    "evidence": [
      "0x005B8050 RespondToPickupResult; 0x005B8192 ldr.w r0,[sl,#0x108]; 0x005B8196 cmp r0,#1; 0x005B8198 bls 0x5b826c",
      "0x005B814A movs r6,#2 (the log literal)",
      "PickupBlockHelper::StartPickupAction 0x005B7B48"
    ],
    "effect": "the stack keeps trying after the app would have given up",
    "provenance": "the current code allowed three attempts",
    "unresolved": "compare ManipulationSystem.cs with RespondToPickupResult 0x005B8050",
    "hardware_required": false,
    "live_path": true,
    "test": "PickupVerifyTests.TheDockHelperAllowsTwoAttempts"
  },
  {
    "id": "M8-011",
    "subsystem": "M8-framework",
    "title": "The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks",
    "location": "cozmo-stack/src/Cozmo.Robot/Behavior/IBehavior.cs",
    "status": "IMPLEMENTATION_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204",
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
    ],
    "effect": "a behaviour leaks or double-releases a track lock, idle animation, motion profile, light pattern, helper or reaction lock",
    "provenance": "the eleven Smart* functions, read at the addresses above",
    "unresolved": "Cross-layer split (manager, 2026-10-10; re-analysis/research/20261010-m3m4-cross-layer-splits.md, Opus extractor at f56329c): overlap ruling: the VisionComponent pre-gate (Gate B, 0x00513C6E..0x00513C9A in Robot::Update) is M3-032's, not this record's; the tag at CozmoEngine.cs:1147 is retagged M3-032 in the build round. Before: Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. No concrete IHelper subclass exists; SmartDelegateToHelper (SteppedBehavior.cs:724) has no caller; the eight DockHelper/SearchForBlockHelper behaviours are not rewired; the manager ticks from FreeplayStack, not the engine tick; the VisionComponent pre-gate (0x00513C6E) is MISSING; LogStopEvent's layout is guessed. Before: built, awaiting strong verification: the BehaviorHelperComponent (all 12 members: AddHelperToComponent 0x0056da8a, DelegateToHelper 0x0056dad8, PushHelperOntoStackAndUpdate 0x0056dbdc, ClearStackMaintenanceVars 0x0056db4c, StopHelperWithoutCallback 0x0056dcc0 with the stale-bottom compare on an empty stack, ClearStackFromTopToIter 0x0056dce0, ClearStackLifetimeVars 0x0056dcdc, CheckInactiveStackHelpers 0x0056de36, UpdateActiveHelper 0x0056de74 branch by branch, Update), the abstract IHelper base, a minimal AIComponent host run from the engine tick after the first-full-state gate and before ActionRunnerUpdate (0x00513eac), IBehavior::SmartDelegateToHelper (0x005beb10), IBehavior::Stop's own helper stop (0x005bd0f4..0x005bd108) and StopActing's gated helper stop (0x005bd364..0x005bd36e). Still open: the six concrete helper classes' bodies and the callbacks each behaviour passes are unread (RECOVERABLE_GAP) and not built; the existing C# behaviours that use DockHelper.RunAsync/SearchForBlockHelper (PickUpCube, RollBlock, StackBlocks, PickUpAndPutDownCube, CubeLiftWorkout, BuildPyramidBase, RespondPossiblyRoll, DriveInDesperation) are not rewired and no C# class exists for BuildPyramid, RequestGameSimple or OnboardingShowCube; the VisionComponent::UpdateAllResults status and its pre-gate [[robot+0x258]+0x28] != 0 (0x00513c6e..0x00513c9a) have no source here (a seam that reports MISSING once); the BehaviorManager is ticked from FreeplayStack.Tick, not the engine tick, so 'helpers before the manager' is not realised, and four of AIComponent::Update's five steps (the analyzer, the whiteboard, SevereNeeds, FreeplayDataTracker 0x00569f46) stay elsewhere; the world-origin id is Engine.Robot.CurrentOriginId (the engine's value, whose only writer here is the constructor: the origin-change branch is unreachable until a runtime Delocalize exists); LogStopEvent's duration and argument layout are guessed; the ClearStackFromTopToIter error path (0x0056dd3c) omits the global byte store at 0x0056dd88 (unreachable, reported MISSING); the helper bodies run under the component lock; the manifest location still points at IBehavior.cs. OV74 check (re-analysis/research/overnight/20261016-ov69-74-75-77-check.md): LogStopEvent is no longer guessed: 0x005B6850..0x005B68EA (by +0x1C: 0 failure, 2 success, 1+active cancel, 1 otherwise inactive_stop) and the emitter 0x005B698C..0x005B6ACC (negative duration -> IHelper.Stop.InvalidTime error, _errG, 0). B-M12-6 built, awaiting strong verification of M12-047 overlap: LogStopEvent layout/duration/event level corrected from adopted OV74; component empty-stack stale-bottom assumption replaced with visible M1-034 skip at0x0056DCC6, and checked clear-loop error/global store built. SmartDelegateToHelper and weak-lock wrapper production bodies retained. Existing separate helper destructor/callback/concrete lifecycle gaps are not settled.",
    "hardware_required": false,
    "live_path": true,
    "test": "BehaviorFrameworkTests; HelperStopAndPyramidLoopTests; M8BatchThreeDTests"
  },
  {
    "id": "M15-076",
    "subsystem": "M15-freeplay",
    "title": "BehaviorStackBlocks, both shipped instances (SparksStackBlock and ordinary StackBlocks): target caches and validity, Init, pickup and PlaceRel helper delegation, one retarget, ground recovery, success objective/animation/needs and stop/reset",
    "location": "cozmo-stack/src/Cozmo.Robot/Behavior/NativeStackBlocksBehavior.cs",
    "status": "IMPLEMENTATION_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204",
    "evidence": [
      "Factory 0x0059CFE6; class range 0x005C93D0..0x005CA33A: ctor 0x005C93D0, IsRunnableInternal 0x005C9484, Init 0x005C9502, stacking 0x005C9520..0x005C961A, StopInternal 0x005C97F8.",
      "UpdateInternal 0x005C9844..0x005C9A88 (M7 inventory row 885, corrected 2026-10-28): both targets valid skips the neighbour query (0x005C98BC..0x005C98C2); bottom invalid -> FindObjectOnTopOrUnderneathHelper(bottom, 15.0f, filter, true) must return the top (0x005C99C0, 0x005C9A1E); one GetClosestValidBottom retarget (0x005C9A5E..0x005C9A84).",
      "Target caches: entries 0x0056F724..0x0056FDA2, dependencies 0x00571040..0x005710CC (5->3) and 0x004D705E..0x004D7072 (4->2), predicates 0x0057052C..0x0057066A / 0x0057154C, entry cadence 0x005711E4..0x005712B6; progression unlock 9 or stackInAnyOrientation selects 5/3, otherwise 4/2 (0x005C94A6..0x005C9500, 0x005C984E..0x005C988C); GetRotatedParentAxis<'Z'> 0x005507A0..0x00550854.",
      "Success 0x005C9EA4..0x005C9F14: objective 40, optional animation 0x21A with score, then the configured needs action (43 for SparksStackBlock, 42 for StackBlocks).",
      "Shipped configs: SparksStackBlock (activity 27) and freeplay/stackBlocks.json (behaviorID 0xA0, executable 9, unlock 10). Read packets re-analysis/research/20261028-sparks-stack-1-read.md and 20261028-stack-blocks-1-read.md with their independent checks; HANDOFF-SPARKS-STACK-1.md, HANDOFF-STACK-BLOCKS-1.md; re-analysis/research/20261028-sparks-stack-1-claude-check.md, re-analysis/research/20261028-stack-blocks-1-claude-check.md."
    ],
    "effect": "which cubes Cozmo picks up and stacks on, when it gives up or retargets, and what objective, animation and needs change a successful stack produces",
    "provenance": "SPARKS-STACK-1 (Codex read and build, 2026-10-28), checked by Claude 2026-10-28; record added with operator approval 2026-10-28 (jobs/trial-m13/PROPOSAL-SPARKS-STACK-LIFECYCLE.md). Widened to both shipped instances with operator approval 2026-10-28 (STACK-BLOCKS-1).",
    "unresolved": "Built and checked in SPARKS-STACK-1 and STACK-BLOCKS-1 but not settled. Still missing: ordinary StackBlocks reaches the behaviour only through supplied admission; PlayAlone admission/scoring, parent transitions and the executable finder are unbuilt. Not this record: the factory/config binding (M7-018), the PlaceRel helper (M12-042), placement parameter producers (M12-028), the bottom-flat gate (M12-012), the neighbour query body (M13-007), the axis helper's other callers (M12-039), the PlaceRel action's Verify/completion (M12-050), the Sparked activity (M15-059), needs/desired-activity reporting (M15-008).",
    "hardware_required": false,
    "live_path": true,
    "test": "SparksStackNativeTests"
  }
]
```
