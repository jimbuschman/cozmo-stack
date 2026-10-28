# SPARKS-KNOCKOVER-1 — independent changed-diff check

2026-10-28. Actual model/effort telemetry unavailable. Answers `jobs/trial-m13/SPARKS-KNOCKOVER-1.md`; approved finite source packet SHA256 `1E0C4E7B51DAADE69A890CD3782B9FB48069C86EE8EFF7283FDBC5202ED128D3`. Companion independent read check is `20261028-sparks-knockover-1-read-codex-check.md`. Source and build roles remain separate. Builder worker `.scratch/worktrees/codex-sparks-knockover-1`; this report is written only in manager worktree `.scratch/worktrees/codex-m13-manager-2`.

## Current owning records before claims

Read current manifest directly before initial changed-diff review; official hard-spark014 correction is imported. Full relevant title/status/evidence fields below. This is not an authored official update or settlement.

```json
{
  "id": "M4-017",
  "title": "BodyLightComponent and SetHeadlight: priority, Off resent every tick while no source, charging state machine, shared locator, wire conversion, the headlight's EnableMode(14) call and send",
  "status": "EXACT_SOURCE",
  "evidence": [
    "LB1..LB6, LB4a..LB4i as before",
    "E2 0x0063234A..0x00632364 SetHeadlight calls VisionComponent::EnableMode(14, enable) on robot+0x258",
    "E3 VisionMode 14 = LimitedExposure (name table 0x01034230, entry 14 -> 0x00C1DC64)",
    "Handoff, owned by M11-061: E4 0x006527AC..0x006527B6 EnableMode tail-calls VisionSystem::SetNextMode (PLT 0x4BA35C)",
    "Handoff, owned by M11-061: E5 0x006B2282..0x006B2298 SetNextMode queues (mode,bool) on the deque at VisionSystem+0xB0",
    "Handoff, owned by M11-061: E6 0x006B4FB6..0x006B4FE4 VisionSystem::Update drains the deque",
    "Handoff, owned by M11-061: E7 0x006B1A76..0x006B1CC6 EnableMode(14) sets/clears mask bit 14 at VisionSystem+0xAC",
    "E8 0x00632368..0x00632380 EngineToRobot(SetHeadlight) then SendMessage reliable, not hot",
    "Re-read handoff: ldr.w r0,[r0,#0x258]; movs r1,#0xE; blx 0x4A91CC VisionComponent::EnableMode(14, on) at 0x00632350..0x00632364 (M11-061); then SetHeadlight blx 0x4B8E38 0x00632374 and Robot::SendMessage blx 0x4A5368 0x00632380; the LimitedExposure reader (E9): M11-057",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 4 (Opus extraction, manager-adopted 2026-10-11): the full log rows (level, channel, key, format, arguments, gates, errG/break and order) and the differences from the C#.",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 13.4 (follow-up Opus extraction, manager-adopted 2026-10-11)."
  ]
}
```

```json
{
  "id": "M4-018",
  "title": "CubeLightComponent: layers, PlayLightAnim gates, SetObjectLights/SetLEDs, the SetLights wire group (SetCubeGamma, CubeID, CubeLights, white balance), WakeUp, EnableGameLayerOnly, default-layer anims, the +0x2C4 hold",
  "status": "EXACT_SOURCE",
  "evidence": [
    "S5..S8, LC1..LC8, LC6, C10.3 rows S3..S7, W1..W5, P1..P5, D1..D5 as before",
    "C11.2 robot+0x2C4 is the robot-is-localized flag: default 1 (0x0050FF10), 0 by Robot::Delocalize (0x00510A4A), 1 by SetLocalizedTo (0x0051245E, 0x005124EE) and CheckAndUpdateTreadsState (0x0051217C); the RobotDelocalized +0x21 refresh is held while delocalized and applied when it returns to 1 (gate 0x00637930)",
    "S3 the cube-sleep flags are written only by EnableCubeSleep; the equivalent public API is Robot.cs:2090",
    "C12.1 the static ObjectLights for EnableGameLayerOnly(enable=1) is 0x0105B4A0, filled by _INIT_36 (0x004D7EB4..0x004D7EE5): eight u32 0x000000FF at +0x00..+0x1F, the rest zero",
    "C12.2 the enable=1 branch is live: game tag 0xBB EnableLightStates -> 0x0063A48A..0x0063A496 and 0x0063AFF8..0x0063B006 pass enable = (msg.byte0 == 0); the app sends it from Robot.cs:1902-1907",
    "C13.1 EnableGameLayerOnly branch detail: all-objects enable=0 stops layer 0 once (0x00639ADA/DE, 0x00639AE6) and sets comp+0x22 := 0 (0x00639B32); single-object enable=0 stops layers 0 and 2 (0x00639AA2, 0x00639AB0) and does not touch comp+0x22; enable=1 sets ObjectInfo+0x1C := 1 and (all-objects only) comp+0x22 := 1",
    "C13.4 EnableGameLayerOnly returns without acting when comp+0x22 (all-objects, 0x00639A16..0x00639A1C) or ObjectInfo+0x1C (single-object, 0x006399DC..0x006399E0) already equals enable",
    "Handoffs: PickNextAnimForDefaultLayer's BlockWorld::GetLocatedObjectByIdHelper blx 0x4A7114 at 0x00637DB4 (Visible needs the located object's +0x24 == 1), SetObjectLights' located lookup blx 0x4A7114 0x00637E82 and connected lookup blx 0x4A6FB8 0x00637E96, ActiveCube::MakeStateRelativeToXY blx 0x4B9288 0x00637EE8: M11-004; the +0x2C4 writers: Robot::Delocalize M11-053, SetLocalizedTo M11-044, CheckAndUpdateTreadsState M10-001",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 5 (Opus extraction, manager-adopted 2026-10-11): the full log rows (level, channel, key, format, arguments, gates, errG/break and order) and the differences from the C#.",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 13.2 (follow-up Opus extraction, manager-adopted 2026-10-11)."
  ]
}
```

```json
{
  "id": "M8-005",
  "title": "PlayAnim plays every trigger it lists, in order, num_loops times",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "StartPlayingAnimations 0x005C0158 measures the trigger vector at +0x11C: a byte length of exactly 4 - one trigger - is special-cased and played as a single TriggerLiftSafeAnimationAction with the loop count from +0x128 (0x005C0190). Anything else falls through to StartSequenceLoop.",
    "StartSequenceLoop 0x005C0294 runs while the counter at +0x12C is below the loop count at +0x128: it makes a CompoundActionSequential and walks the whole trigger vector, adding one TriggerLiftSafeAnimationAction per trigger (0x005C02CA..0x005C030E), then runs the compound and increments the counter. So every trigger plays, in the config's order, and the list repeats.",
    "The loop count is the config's num_loops, read with a default of 1: Json::Value::Value(1) then Json::Value::get(\"num_loops\", default) at 0x005C001C..0x005C0036.",
    "Of the fifteen shipped PlayAnim configs, one lists more than one trigger - NothingToDo_BoredAnim - and none sets num_loops. This stack played the first trigger that resolved and stopped; it now plays the list."
  ]
}
```

```json
{
  "id": "M8-019",
  "title": "BehaviorManager::RequestCurrentBehaviorEndImmediately, as HandleRobotStopped calls it",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Called at 0x00535454 (blx 0x4AA1C8) from Robot::HandleRobotStopped when the cliff sensor is enabled (gate 0x00535436..0x00535438; M4-019 H4 0x00535432..0x00535480)",
    "re-analysis/research/overnight/20261014-ov20-21-check.md: R2/R3 0x005A3930..0x005A39A6; caller R1 0x00535432..0x00535486 (interface to M4-019 H4)."
  ]
}
```

```json
{
  "id": "M15-059",
  "title": "ActivitySparked lifecycle: OnSelected (HardSparkStartedByEngine tag 131), OnDeselected (StopAllAnims), objective/observed, Update/checks/end/interrupt, get-in/out, Complete (meta.upgrade_replay_* events, HardSparkEndedByEngine tag 132)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: OnSelected 0x005B1518..0x005B16DC, 0x005B171C; OnDeselected 0x005B1780..0x005B179E; objective/observed 0x005B181E..0x005B1866; Update/check/end/interrupt 0x005B186C..0x005B1A16; get-in/out 0x005B1D18..0x005B1F2A, hard max 0x005B1F94..0x005B2066; Complete 0x005B1A90..0x005B1C2E, 0x005B2094."
  ]
}
```

## Initial coherent production draft, before tests/freeze

Read all new NativeSparkedActivity, NativePlayArbitraryAnimBehavior and HardSparkReport bodies and the Activities/NativeBehaviorRuntime/BehaviorFactory/BehaviorManager/Lights diff. Builder explicitly identifies CompletionDiagnostics as unfinished placeholder keys, so it is not evaluated as final production fidelity. No reviewer test process; builder owns the runner.

- H9 report delivery uses existing DeliverAnimationReportToApp followed by synchronous local AnimationMessages.Deliver;83 u32LE and84 bool payloads and no new public ad-hoc event. No duplicate-delivery defect identified in this initial delta.
- Concrete introduced race: BodyLight StartLooping separates StopLooping's lock from insertion's lock. Two same-locator starts can both remove the old node, insert B, then insert A and overwrite locator; B becomes untracked and may reappear later on removal. Earlier shared-locator replacement was atomic. Charging update and host light entry use that shared locator; preserve atomic source replacement and existing locking diagnostics.
- Concrete omitted source step: S27 common activity deselection must call Needs.SparksRewardCommunicatedToUser when pending before own specialized reset/cube cleanup (005B3548). Neither existing generic Activity.OnDeselected nor new SparkedOwner.Deselected does it in the initial draft. Needs28 can create a real pending reward; this affects API delivery. Builder informed.
- The new runtime automatically deselects, clears selected owner and manager.Activity after own Desired returns null, before reporting missing parent picker. Source H8 forbids guessing post-null owner choice. Exact native parent branch ordering is being checked by A before directing correction; no automatic cleanup ordering is endorsed yet.
- Own desired get-in/get-out tests ActiveSparkSoft alone; native S21/S23 additionally requires activeID!=0x55. Builder asked to preserve the explicit canonical guard rather than assume soft flag cleared whenever ID is invalid.
- The actual target config is tightly bound to SparksKnockOverCubes, leaving other Sparked owner configs visibly MISSING. Hardcoded objective13 matches this shipped asset, but config parsing/validation must not imply generalized A0 behavior. Wait-false retained Wait and delegate parameter branches remain named gaps, and M8-019 immediate switch remains visible rather than substituted with generic Stop. No whole059/005/014 claim from a finite native owner.

Findings were sent promptly to builder and manager before test/freeze. These are draft checkpoints, not final verdicts. Review and all actual regression/freeze audit remain PENDING.

## Finite source follow-up and draft corrections

Source supplement `20261028-sparks-knockover-1-source-followup.md`, independently hashed `A522CCE9BFB0A3C35FC1892B4B13CE9B7828387EDFD9AA081B1260F9A66919D5`, refines SF1–SF4 without changing the frozen original packet. Independently checked native post-null prefix, pending-reward caller order, literal bytes, and actual chooser table cells/bodies.

Current downstream recipient record read in full before reliance:

```json
{
  "id": "M15-071",
  "title": "SparksRewardCommunicatedToUser: the FreeplaySparksAwarded game broadcast and its amount",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "SparksRewardCommunicatedToUser 0x00696E64 clears the pending flag (+0x3D8, 0x00696E78) and builds FreeplaySparksAwarded from the amount at +0x3DC (0x00696E82..0x00696E88), then calls the E2G Broadcast(&&) through the external interface vtable +0x1C (0x00696E90). It has no pending-flag test (research/20261018-b-m1-7-check-delivery.md finding 1).",
    "Callers named in Needs.cs: IActivity::OnDeselected 0x005B3548, BehaviorEarnedSparks::StopInternal 0x005DAF7C, and RegisterNeedsActionCompleted; whether they guard the call on the pending flag is unread.",
    "The stack's public SparksRewardAwarded (Needs.cs:828) is queued with no payload and returns early when !SparksRewardPending (Needs.cs:841)."
  ],
  "unresolved": "New (manager, 2026-10-18; B-M1-7 census). To read: the three callers' guards (does any call without a pending reward, which the native body would still broadcast?) and the writers of +0x3DC (the amount). Open stack differences: the amount is not carried (the 'app's economy' comment is not a recorded decision), and the early return on !SparksRewardPending has no source. Not an exclusion."
}
```

SF1 establishes the draft automatic retirement as a real implementation bug: leaf null goes through005AE70A/75C/760/762 to parent005AE40C and actual PickNewActivityForSpark at46E before any deselection. Even the native picker has a no-candidate path without old-owner deselection. The revised runtime retains selected owner/manager activity/own state4 or5 at a visible missing-parent continuation; its scheduling latch belongs to that missing caller boundary, not a fabricated own completed-once flag. Explicit supplied next admission resumes and invokes recovered cleanup. Historical focused passes using early automatic cleanup are not final source-fidelity proof.

SF2 proves the common pending guard and required locks→smart idle→interlude→pending reward→specialized reset/cube ordering. The revised own cleanup calls the existing Needs recipient only when pending and visibly names M15-071. That fixes this source caller only. Current recipient lacks native amount and raw FreeplaySparksAwarded payload and has broader no-pending-call differences; no accepted full-reward-delivery claim follows.

SF3 confirms exact meta.upgrade_replay_* selected final event; optional hard fail_cubes_observed event precedes it; both precede request clear and HardSparkEnded. Placeholder robot.spark_* draft keys were explicitly unfinished and replaced. Missing native UnlockId names remain inherited diagnostic input; no fabricated amount or extra result event.

SF4 establishes actual StrictPriority +20/+24 hooks are BX LR at0060A7BE/C0, confirmed from vtable cells102CD34/38. No missing stateful chooser lifecycle step exists for this actual chooser. This is not a no-effect claim for every chooser or all Spark strategy machinery. Existing host strategy timestamp setters represent the common activity's recorded54/58 times; they do not prove a native picker.

Atomic source-locator replacement and activeID!=55 soft guards were corrected in the draft. Initial action callback-pointer suggestion was narrowed by manager: an empty-listener private single callback changes no observable1–4 by itself; presence of+88/+98 is not an independent fidelity oracle. A private no-op callback is acceptable mechanism. Meaningful StopNext regression must prove running action retention, no next owned work after completion (multi-loop suppression is observable), and normal manager finish. Do not claim whole M8 listener support from an empty placeholder.

The initial soft get-in test failed because it guessed0x215. Actual shipped activity softSparkTrigger is SoftSparkUpgradeTracks; generated official enum ordinal is518/0x206, while0x215 is SparkPickupInitialCubeReaction. The source-symbol correction is justified, not a production fix. Hard0x211 is SparkGetIn,0x212 SparkGetOut,0x213 SparkIdle and0x216 SparkSuccess. Preserve the failed attempt. Builder-reported focused2=5 passed/1 failed of6 and focused3=7/7 are historical draft results, not independently run or frozen verification. Exact ledger/artifacts and later attempts are pending builder release.

No reviewer tests have run. Final code/test diff and source-hash freeze remain pending. No production/test edits by reviewer.

## Final frozen changed-diff verdict

**PASS for the finite delivered changed diff and independent affected regression.** Inspected the complete production and test delta, including all four new files, against frozen S/P/H source plus SF1–SF4. No unsupported new live join, circular source oracle, or concrete remaining introduced race/deadlock was identified in this delivered scope. This verdict retains the UNKNOWN startup fault and the exact existing record/frontier limits below; it is not whole-record settlement, full-suite success, physical firmware fidelity or hardware acceptance.

Frozen raw manifest contains exactly10 C# files: Activities, BehaviorContainer, BehaviorManager, NativeBehaviorRuntime, NativePlayArbitraryAnimBehavior, NativeSparkedActivity, HardSparkReport, Lights, M7BatchThreeBTests and SparksKnockOverNativeTests. Aggregate SHA256 of the exact raw hash-list file:

`7E766D5ECE9CEA26BD1A135143C0879E9C81C1B2EDAE86C2E1C40C16F55024A7`

Independently compared all10 files against that manifest before execution and after both runs/server shutdown:10/10 match both times, zero mismatches. The new/untracked files were read in full, not omitted by git diff --stat. `git diff --check` exited0. Reviewer source read/verification artifacts are separate from builder assertions.

Final code review confirms:

- Distinct configured Sparks ID/min2/needs28 reuses native Knock class; ordinary Knock/public/manual source code is unchanged. Native arbitrary class0x29 is created by its real factory row, guarded by actual native Robot/manipulation ownership, and supplies real ActionList get-in/out children. All79 extraction/cardinality/ordinal/constructor/size/case comparisons remain unchanged; trailing supported-constructor expectation adds only PlayArbitraryAnim.
- Objective13 and Family1/2 recipients belong to the constructed activity lifetime, with no invented selected guard; selection resets counters. Actual manager/activity scheduling uses the reaction predicate, exact float operations/negative-epsilon bits, source Wait-class distinction and end-next-action forwarding. Hard/soft/invalid-active guards and local/raw83/84 report framing/order agree with source.
- The parent-null boundary retains owner/manager activity/state4 or5 and uses a forwarding-continuation guard to avoid re-entering the unavailable parent. It neither invents state5 nor performs early cleanup. Labeled explicit changed next admission exercises deselection later. Actual constructor disposal only releases subscriptions; the retirement lead found no added deselection/Complete call and required no fix.
- Cleanup preserves source lock/idle/pending-reward/reset/cube order. The existing reward recipient is called under this source pending guard, with visible M15-071 amount/raw-report limitation. Cube stop invokes existing EngineLayer1/all-objects helper once and preserves user0/state2. Raw backpack vector, RGBA packing, periods/offsets and independent source0 locator agree with the exact checked literals and accepted M4 helpers. Shared locator replacement/reset now remain atomic under one gate; other source nodes survive.
- Single empty-listener callback is an internal mechanism, not proof of listener functionality. Observable multi-loop StopNext regression proves no second owned action starts and the real manager finishes after retained action completion. No general listener support or whole M8-005 completion is claimed.

The tests distinguish source-derived component inputs from the two actual full configured hard/soft motion runs. Full motion runs use actual configured owner/manager/ActionList, received path-segment progression, checked center-to-MAIN inverse, worker images, real active flip/member-axis event and supplied firmware motor/path/animation inputs. They do not force a terminal action result or replace a selected goal/yaw. Exact Started83/Ended84 bodies, local-before-deferred-raw order, soft omission, objective13/needs28 and source-shaped outer/nested completion reports are observed. Unit cases clearly label supplied lifecycle/count/terminal/layer inputs. Expected soft trigger/table masks/color/time/layer values were checked against source, including rejected earlier guesses. No actual-output-derived oracle was used to justify those corrections.

## All execution attempts retained

Artifacts below are in worker `.scratch/sparks-knockover-build/`; failures are preserved rather than overwritten.

| Attempt | Actual outcome | Disposition |
|---|---|---|
| Builder focused1 | exit1, no tests | Fresh worker missing project.assets.json (NETSDK1004), not a behavioral test result. |
| Builder restore1 | exit0, no tests | Dependency preparation from available cache. |
| Builder focused2 |5 passed,1 failed,0 skipped /6;2s;exit1 | Test guessed soft0x215; actual asset/enum is SoftSparkUpgradeTracks0x206. Source-checked oracle correction, no production fix. |
| Builder focused3 |7/7 passed,0 skipped;2s;exit0 | Historical draft still had unsupported automatic deselection. Not accepted as final source scheduling proof. |
| Builder focused4 |14 passed,2 failed,0 skipped /16;3s;exit1 | Both new positive tests used wrong selected-lock8 oracle. Native selection table has8false and1true; final table has8true. Corrected test expectation without production change; final8 remains explicitly covered. |
| Builder focused5 |21/21 passed,0 skipped;3s;exit0 | Corrected draft finite scope before final reset/re-entry changes. |
| Builder affected1 |865 passed,1 failed,0 skipped /866;58s;exit1 | Unchanged startup RobotValidated5s wait failed; cause UNKNOWN, discussed below. |
| Builder diagnostic1 |23/23 passed,0 skipped;4s;exit0 | Final new class22 plus isolated unchanged startup1. Passing rerun establishes no cause/fix for affected1. |
| Independent reviewer-affected |867/867 passed,0 failed/skipped;58s;exit0 | Exact original saved affected filter; final additional signed-count test explains +1 from866. |
| Independent reviewer-consumer |404/404 passed,0 failed/skipped;23s;exit0 | Separate approved finite extension for omitted public/shared consumers. |

The builder's PowerShell splice failure produced no valid generated test and no test process; it is retained in the builder ledger. No full suite was run by the builder or this checker. Every actual test run used real assets and no asset-bypass setting. Reviewer runs were authorized local IPC execution with `--no-build --no-restore --nologo -v:q`, existing project `cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj`, and separate TRX/log/exit files. No source/test changes were made by the reviewer.

Reviewer-affected used the **exact unchanged contents** of `affected-filter.txt`:

```text
FullyQualifiedName~SparksKnockOverNativeTests|FullyQualifiedName~KnockOverNativeTests|FullyQualifiedName~PickupChildrenNativeTests|FullyQualifiedName~PickupPyramidNativeExecutionTests|FullyQualifiedName~PickupPyramidPilotTests|FullyQualifiedName~PickupStackNativeVisionTests|FullyQualifiedName~PickupSearchNativeTests|FullyQualifiedName~PutDownBlockNativeTests|FullyQualifiedName~SharedChildrenNativeTests|FullyQualifiedName~M7|FullyQualifiedName~M8|FullyQualifiedName~BehaviorFrameworkTests|FullyQualifiedName~Freeplay|FullyQualifiedName~ActivityTests|FullyQualifiedName~M4ControlTests|FullyQualifiedName~M3M4X17|FullyQualifiedName~SparkManagerInputCheckedRowsTests|FullyQualifiedName~Needs
```

That TRX independently contains22 Sparks cases,26 ordinary native Knock cases and the unchanged StartupOwnsNeedsAndConnectRequestsPoolBeforeTheHostResponse. It excludes public NavigationTests, which justified the separate finite consumer run. The extension also covers shared/public backpack, charging/reset, abort/sleep and current native runtime ownership consumers that the original filter missed. Exact preserved `reviewer-consumer-filter.txt`:

```text
FullyQualifiedName~Cozmo.Protocol.Tests.NavigationTests|FullyQualifiedName~Cozmo.Protocol.Tests.ControlTests|FullyQualifiedName~Cozmo.Protocol.Tests.EngineAppLayerTests|FullyQualifiedName~Cozmo.Protocol.Tests.M3M4X10BuilderTests|FullyQualifiedName~Cozmo.Protocol.Tests.M3M4X21BuilderTests|FullyQualifiedName~Cozmo.Protocol.Tests.M4AbortSleepTests|FullyQualifiedName~Cozmo.Protocol.Tests.M5LastReadsTests|FullyQualifiedName~Cozmo.Protocol.Tests.CoreReviewTests|FullyQualifiedName~Cozmo.Protocol.Tests.DriveOffPilotTests|FullyQualifiedName~Cozmo.Protocol.Tests.NativeTrackerOwnerTests|FullyQualifiedName~Cozmo.Protocol.Tests.StandaloneManipulationOwnerTests
```

Its TRX includes the previously relevant public NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates, which passed. No assertion or timeout in that old test or the startup test was weakened.

Both reviewer TRXs were independently parsed: all error, timeout, aborted, inconclusive, passedButRunAborted, notRunnable, notExecuted, disconnected, warning, completed/inProgress/pending counters0; RunInfo entries0; all reported tests executed. `reviewer-summary.json` retains each builder/reviewer TRX's counters and failed names. Reviewer artifacts: `reviewer-affected.log/.exit/.trx`, `reviewer-consumer.log/.exit/.trx`, `reviewer-consumer-filter.txt`, `reviewer-hash-before.json`, `reviewer-hash-after.json`, `reviewer-summary.json`, `reviewer-serverstop.log/.exit`.

Final build-server shutdown exited0; MSBuild and VB/C# compiler servers reported successful shutdown. Final active testhost count0. **Host released; no further reviewer tests planned.**

## Unresolved startup failure: bounded path investigation

Builder affected1 genuinely failed existing EngineSettlingRegressionTests.StartupOwnsNeedsAndConnectRequestsPoolBeforeTheHostResponse at line68 after5.2949s: RobotValidated did not become true in the existing5s wait after one scripted FirmwareVersion. TRX contains the assertion/stack only, without a trace of validator transitions, transport drop/reorder, disconnect or scheduling state. A passing isolated diagnostic and this independent broad pass do not explain it.

Traced the actual current path, including shared changed-code reach: public Create/BeforeStart initializes Needs/config/real ticker; Connect posts RCM.Connect then actual AddRobot and RIC; framed transport ConnectionResponse is queued/handled to state2; Firmware datagram goes through Transport.ProcessIncoming/peer/reliability effects, Received/RCD arrivals, engine RCM.Update/PopData, M2-022 filtering/unpack, actual Robot per-tag bus/RIC subscriber, Firmware JSON build/version/time/sim parsing, then OnNotified writes volatile validated before OnSuccess registers the Mfg recipient and sends its request. Manufacturing info and block-pool/host response follow the failed assertion and were not reached by that failed test.

The fixture resource root contains only animations/a.json: behavior map is empty, activity resources absent. More than absence of a Spark owner was checked: new factory delegate table initialization/guards do not execute a native arbitrary constructor with an empty config map; no owner subscription/count/report is created. New runtime selected-owner branch is unselected. New BodyLight Start/Stop/reset implementations and callers were inspected: no RobotState/light arbitration occurs before this firmware assertion, reset is a separate removal/reconstruction edge, and shared per-instance gates contain no new cross-instance wait/callback. Transport/parser/RIC/ticker/validation bodies and this test are unchanged. This bounds the changed-path reach; it is not a proof that global timing/load, shared state, startup scheduling, transport behavior or a harness issue cannot contribute.

Classification remains **unresolved observed startup fault; implementation bug versus harness/timing/transport cause not established**. No source-recovery contradiction, hardware-only behavior, external blocker or specific repair was demonstrated. No timeout enlargement, unrelated production fix, test narrowing or assertion weakening was authorized/performed. Retain this uncertainty in manager/full-gate assessment. A current pass is evidence of this run only.

## Exact remaining scope and final disposition

No official fidelity records/status/evidence/inventory/approval/Claude/REVIEW/PROJECT_STATE edits or new records by this reviewer. No whole-record settlement proposed: M15-059 remains partial to the actual configured owner and supported parameters; other Spark owners, native Wait/hardmax/delegate/Peek branches and general reload/objective mapping are not certified. M13-014 retains public/manual/generic path limitations; M8-005 retains general listener and old sequence/framework claims. M15-006/M15-058 parent admission/picker/tracker context and executable selection remain inherited. M8-019 immediate switch and M15-071 native reward amount/raw payload remain visible gaps; full upstream44 serializer, generic planner/vision geometry and audio-runtime boundaries are unchanged.

**Final verdict: PASS for this finite changed implementation and the two independent frozen regressions, with the original UNKNOWN startup fault explicitly retained.** This is not a claim of full-suite success, whole behavior/source fidelity, resolved startup race, complete native reward delivery or hardware acceptance. Manager owns integration, full gate, commit and later Claude assessment. No git mutation/commit or hardware action by this checker.
