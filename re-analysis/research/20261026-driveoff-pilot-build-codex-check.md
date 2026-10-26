# Final independent build review — PASS for the scoped candidate; whole pilot IN PROGRESS

**Verdict: PASS for the checked implementation candidate at the final 55 source/test files below. No outstanding blocking finding in that scoped diff.** This authorizes manager/builder checkpoint commit and manager-owned full gate/Claude review; it does not certify the whole DriveOff behavior finished. Earlier PENDING/failure entries below are preserved chronological evidence, superseded only by the specific corrections/rechecks recorded here.

Final handoff identifier: builder aggregate SHA256 `2d8bae25556a44297a1884e191088c293a6f7b24773276e28944106e29b00d31`; independently checked **all 55 per-file SHA256 values with zero mismatches** against builder `.scratch/driveoff-pilot/candidate-sha256.txt`. Final Mood.cs SHA256 `59f6d72f8f8406c6a1219a9c7c56f0577df0ca7fdc9b1f97af386a0e14af30ea`; actual `// fidelity: M7-026` at341 above GetSimpleMood is a nonbehavioral owning annotation, not a status change. Final DrivingAnimationHandler.cs `c6b9d01bb49af438a7dcf3dfd69aecfd0e2cc6e0cef0a8552e6d212bfdd1736a`; M5X15BuilderTests.cs `a28503b9feb0aa021f03da104622eb465804c26fe103c8c85b6b8dc44d81a7ac`. Unchanged prior-reviewed files reused by hash; later corrections received affected-only review.

Independent actual execution (no overlapping builder hosts):

| Run / artifact stem under builder `.scratch/driveoff-pilot/` | Actual result and scope |
|---|---|
| `codex-independent-focused` | exit0,303/303 pass,0skip,1m7s. Actual configured PutDown/factory/chooser/action/path/completion plus native config, camera-worker failure/drain/reset, facade and affected regressions. Exact earlier candidate; not rerun wholesale after narrow corrections. |
| `codex-independent-c7` | exit0,7/7 pass,0skip,1s. Root/activity nonnull objective guards plus unchanged configured route. Later null branch separately reviewed/tested below. |
| `codex-independent-final-affected` | exit1,46pass/1fail/0skip,total47,3s. Actual final driving Mood/Needs+C7 null/non-null+native mood/API and legacy classes. All46 unchanged cases passed; sole failure was unrelated periodic Mood diagnostic in an overbroad stack-log collector. **This run is not relabelled a pass.** |
| `codex-independent-collector` | exit0,1/1 pass,0skip,212ms. Only the corrected failing fixture, exact ordered stack/overflow assertions retained. No production suppression or behavior change. |
| `codex-final-fidelity.log` | explicit Python313 `-X utf8 re-analysis/tools/fidelity.py --check`, exit0,710records; no manifest/status/evidence changes. |

Commands/filters and failure details are preserved below; every test run used explicit dotnet, `--no-build --no-restore --nologo -v:q`, TRX and console log. Final collector filter was `FullyQualifiedName~M5X15BuilderTests.DrivingStackErasesFirstMatchAndChangesCurrentOnlyOnInit`. No checker full suite, hardware, commits, pushes or production/test edits.

Final affected static disposition: native consumers use retained native publication instead of unknown→empty/false; BuildPyramid target reads no longer consume public host cache; bool-only Straight API call shape restored; empty/read-failed Mood config follows defined null-input continuation; actual Mood app raw+typed enqueue precedes synchronous local61 under actual external owner guard; root/activity Look objective nonnull overrides are visibly unavailable while omitted/explicitnull preserve41, with admission guard before Stop/publication; actual bound driving owner reads Mood (rawSad1) then canonical Needs for other raw values, override-first and M1 null skip preserve prior triplet. New tests distinguish Boolean-seam order tests from real canonical-level producer cases. Changed Navigation expectations derive from captured source inputs, not emitted outputs. Cleanup/join order and real5A/C6/CA/CB test reach were checked.

**RECORDS-TO-SETTLE candidates for Claude (no status upgrade here):** M7-026, now including actual driving consumer004F0D2C..74 and group cached130 bindings, exact SimpleMood raw mapping/thresholds and captured native update clock; M8-027, whose CURRENT unresolved already establishes every held callable destruction memory-only, confirmed against managed unlink/retirement semantics. Current literal record quotes and exact recommendations are below. Do not extend these named-record candidates to whole Mood/M8 subsystem acceptance.

**Remaining records/whole-pilot STOPs:** M13-017 and M13-040 remain whole-record IMPLEMENTATION_GAP handoff items: actual object1 audio/scoped active observer/scheduler/render path is unbound; live detector ROI/rectangle/GetROI producer and other enabled vision handler/lifecycle availability remain; general Meet selector/Feeding.Update and selected Look action remain explicit feature boundaries; noncube footprints and broader named cached-pose/component lifetime obligations have not been certified. Current positive map-height/cache/relative/insertion and native pending/container producers must not be described as wholly absent. M12-049 still owns unclosed BuildPyramid callbacks; M13-013 owns derived DriveOffChargerContacts, not this plain Straight path. No unknown is converted to no-effect or exclusion. Operator D1 public configuration adapter remains isolated standalone extension. Hardware verification and acceptance are **not performed**.

Evidence classification: implemented/source-compared code and real **offline** configured-path reach with explicitly admitted resources, states, calibration, RNG, raw frames/ROI/status are verified as described. Passing tests do not promote provenance. Actual selected model/reasoning telemetry is unobservable. Queued only: stale nonbehavioral comments/diagnostic wording, including old M8-027 captured-finalization prose; no extra mechanism or verify round requested.

---

# Drive-off pilot: independent build check — PENDING

No implementation verdict yet. The builder is working in isolated codex-driveoff-pilot-build from read/contract commitac8d42a4. This report is the checker's prepared verification plan, not a claim that any test exists or passed. No code/tests/git changes or concurrent test execution by this checker. Actual model/reasoning telemetry is unobservable.

## Required production reach and independent expected observations

1. **Configured entry and real action.** Load the shipped DriveOff JSON through the actual BehaviorContainer factory with the same robot/context used by configured chooser/manager admission. Verify absent extraDistance gives96f and shipped extras give156f/141f, with native eligibility supplied from actual platform/raw carrying/off-treads/severe/whiteboard owners. Explicitly distinguish entry into an activity from an already-selected admitted-state boundary. For entry: Timerfloat→chooser→PublicState.UpdateActivity order (005B3142/314A/3154); native Feeding2/MeetCozmo6/PutDownDispatch13, no added activity triplet/idle for these exact trigger-absent configs. A manually constructed ShippedBehaviors instance or reflection/private completion call does not satisfy the production claim.

2. **Admission, packet words and completion.** Trace actual StartActing callback-before-tag-before-busy/QueueNOW, ActionList deferred Init and real-tag matching E2G5A delivery (005BDACC;005BF0BE→005BE1E6). Native completion clears tag before busyfalse/callback synchronously; a host RunAction/Post tick is a different route. Verify one line with main-pose yaw, stored ForwardPose XY and separate f32 product/add operations; signed defaultspeed100/-80,accel200,decel500 (005470F0/00547628..AA). Mutate carry after SetPose to distinguish stored-center from late recomputation. Observe Clear3C→line3D sevenf32→Execute41 with actual C6/currentsegment feedback and same owner. No lattice plan is requested; no elapsed-time synthetic success. Assert real failure/cancel/reentrant callback and teardown behavior where source packets specify it.

3. **Concrete animation identity and API payload.** On retry, force different chosen names while retaining the first cached clip. Expected PLAYoldclip but REPORTcurrentselectedname+78 in variant1/string (005441EC/F2;00544472..7A;00543F3A..42). Test cannot use clip.Name as the expected completion oracle. Check actual CA/CB/D5/5F lifecycle, end-wins, finite-loop handling and matching streamer clear on destruction including null==null. Start/loop/end actions must be the actual handler producers, with cancel reentrancy and track-lock behavior retained. Source-complete evidence must cover every changed concrete-action claim, not just a callback bridge.

4. **Captured clock, mood and whiteboard.** Use a controllable source clock that advances between phase/callback observations. Each engine tick captures one f32 Timerseconds; Mood.Update before AI/actions uses the captured value and caches+130. Group selection uses actual GetSimpleMood and cached+130 even if wall/source time advances afterward (00513BE6/BEA,00513E84..8A,0058A954/95C). No duplicate Freeplay.Advance. Success fullresultword0 delivers objective4 public/API before local callbacks, then triggers actual Mood event using its own sourceTimer capture. Verify shared robot-lifetime LastDriveOffCharger float stamp across distinct contexts and later eligibility; default-1 must not reset when a facade/context changes. Separate periodic mood API output from same-tick event mutation.

5. **Vision gate.** Exercise a drain with multiple results where an earlier handler fails and a later succeeds: aggregate staysfailure, all source-ordered results/timestamp/broadcast work still completes, then platform/Mood/AI/ActionList/Path phases are skipped. Next empty or successful drain returnsfalse and allows phases; no sticky historical status (00654480/482,006544EE/500,00654A74/78,00654AC0..CE). ProcessedImage carries raw u8 imageMean from resultbyteC8 when mask100 is set, otherwise0; never normalize it to bool. Unsupported processors must not manufacture success. Tests must enter the actual mailbox/drain producer, not merely set the final engine delegate.

6. **Normal contact, map/configuration/audio.** Admit synchronized full RobotStates; contacttrue observes old-owned pose before new incoming history admission, retains charger identity/Known root pose and actual map cache/relative/quad/insert bindings. Conditional pending frame-tail rebuild must use source all3 configurations, including FIRST/processedIDs/duplicates/prune/nativecount/tallest. Use independently source-derived adversarial inputs already supplied in the read packets (greedy AB/AC suppresses legacyBC; fourvertical seed groups can retain duplicate first3; FIRSTupper-neighbor tie). Exercise root/MAX-asymmetric dimensions, rawcarry, scalar+.5 center, strict boundaries and known cv orientation exception→defined RectangleFallback (EH004E64FE→004E65C6→004E6778), rather than generic managed-gap fallback. Unknown owner/root/geometry must preserve the entire prior native publication/rootcache/flags/counters and retry visibly. Verify tallest/publicstate then A-before-B audio. In Feeding2 the source world-event branch is suppressed; in non2 state a stacks>1 transition produces checked audio event requests. Inspect actual requests/order, not just cache contents or shared tick association.

7. **Objective recipients and public API hold.** Objective4 filters use actual constructed activity owners/subscription lifetimes; Sparked configured mismatch has no mutation; Socialize preselection M1-034 skip/log must not fabricate phase0, while actual OnSelected initialization survives deselection and owner retirement. Other objectives/changed configs stay their named gaps. Whole public Cache/Stacks/Bases/Pyramids/MID/FirstSeenSec/ConfigurationSeen adapter remains unchanged pending operator decision; engine consumers may not accidentally retain its all-pairs results. No dependent API change or finished-whole-pilot claim until that hold is explicitly resolved.

## Review and evidence rules

Compare the entire stable diff with the approved rows/primary packets, then inspect actual production call reach and meaningful independent expected values. Reuse prior native witnesses; reopen only a concrete changed ambiguity. Tests of copied helpers, private completion or substituted final delegates are unit evidence and must be labelled accordingly. Run independent focused production/regression checks only after the builder's testhost stops and manager authorizes; do not race shared artifacts or repeat a broad suite. Manager owns the once-only final full gate.

Every threaded test must release deterministic barriers in finally and join all runtime workers before pose/parent/event/rig disposal, including failed assertions. Look for source-state races and callback reentrancy; do not add generic locks or listener catches without a specific defect and approved behavior. Verify no unsupported input is published as native empty/success. Inspect negative-control restoration and actual assertion results; do not overstate quiet logs or unobserved hardware.

Final review will state implementation/code verification, actual offline production reach, hardware and acceptance separately; quote current records before whole-record/residual claims; list exact remaining gaps and any defect correction. No manifest statuses, evidence, approvals or review states are changed here. **Current verdict: PENDING, no build PASS.**

## D1 operator disposition — supersedes the adapter hold

Operator decision delivered through origin/main5ae4d66a REVIEW.md approves option1: retain the WHOLE public configuration adapter as a documented standalone stack extension. Its existing public snapshot/MID/FirstSeenSec/ConfigurationSeen semantics must be preserved, and it MUST NOT feed ANY behavior or native engine state. The previous POLICY HOLD is resolved; this does not turn the adapter into recovered native behavior.

Final review must search all consumers, including BehaviorOnConfigSeen.Recent, and reject any retained behavior dependency on legacy Cache/FirstSeenSec/all-pairs data. OnConfigSeen's source recipient must consume native per-type counts and its own f32 clock state: clock now; dt=now-previous evaluation; store previous=now; for each tracked type, growth above saved count stores common growth-stamp=now, then saves current count; result dt<4.99999 AND abs(growth-stamp-now)<1e-5 (005DB2D4..005DB396). This is not per-arrangement first-seen age, reacted-ID history or a hybrid native-ID/legacy timestamp cache. Use actual source recipient clock/order and explicit input availability, not a fresh invented timer. Preserve public extension independently and verify behavior/native consumers cannot accidentally call it. No new native extraction or code/test change by checker.

Verification checklist item7's pending hold wording is superseded by this explicit decision. Requirement now: unchanged public extension semantics PLUS complete isolation from every behavior/native state consumer, with actual native-count/time recipient tests. Whole-pilot build verdict remains PENDING until stable diff review and authorized independent execution.

### Narrow read refinements reopened before wiring

Completion-union serializer/default/string width, actual resultbyteC8 producer/processed-image layout, and Q17 config/scoring arithmetic require supplemental source confirmation. Getter/consumer evidence alone was insufficient; the initial whole-read PASS overstated closure and is provisional for these exact units. Builder has stopped those units. Independent check will record their exact primary answer before implementation. Preserve public extension query semantics (GetTallestStack/membership included) while using explicit native accessors for all behavior/engine consumers under D1. No source/tests/git changes by checker; verdict remains PENDING.

Pre-wire U1/U2, V1–V4, Q1/Q2, N1–N8 and DOWN source refinements independently PASSed and recorded with exact primary addresses in the read-codex-check addendum. Notably processed-image byte is RAWimageMean, not bool; actual00712FFA call establishes timestamp/count/modes/mean order. Plain DriveStraight union wire06; animation uses String8 narrowed count+bytes. Mood null-input keeps actual curves/watchers/update, clears global repetition without inventing a default. DOWN has no carried exclusion. Chooser uses Robot.Context+14 RNG, not System.Random. Final implementation verdict remains PENDING; no tests or code changed by checker.

Chooser ownership and bonus distinction follow-up PASS recorded in read-check: canonical context RNG sharedwithgroups; chooserbonus(0,0) differsbehaviorrunninggraph(0,1); Feeding actualuniversalchooser precedesstagemap. Audio endpoint recipient remainsOPEN: no wholebackendclaim fromA/Btestdelegates. D1 Seen projection diagnostic-only afteractualnativeadmission, publicqueryextensionunchanged. Final buildverdict PENDING.

DriveOff-only removal of host Library!=null admission prerequisite sourcePASS; retain actualbase/internal gates and downstreamanimationreadiness diagnostics. RealCA/CBfixture mustloadactualassets; nullLibraryadmissiontest separate. Finalplatformfalsechooser tick is NOTcoveredbyonchargercompetitorrejection; exactderivedeligibility/scoring/RNG path needsclosure. Audioendpoints remainrealSTOP, nofacade-onlyPASS. BuildverdictPENDING.

Audio A1–A7 independently source-checked in read-codex-check: control-scoped per-rowPASS; A6 ONLYfresh/noactivevoice, explicitSTOPactiveconsumer/scheduler/render/output. Requiredminimalunit is canonicalrealowner/readiness/nativeinput/sharedFIFO/1901/scopedstate+query+eligibleobservers/sourceaudio-tail, notfacade/testcallback/unuseddict. NoaudioimplementationorfullaudioPASS fromthischeck. BuildverdictPENDING.

C6 six competing eligibility gates/base score virtuals independentlyPASSed; Interact exactselectorcaller/costbody identified but liveFaceWorld/pose-anglebindings remainpreciseSTOPifnotalreadycovered. CanonicalPutDownDispatch mayclose specificconfiguredDriveOff route withactualLookcandidate/commonstate/order/RNG, noLookactionbody beforeDOfinish; noMeet/Feedingclosure inferred. Details inreadcheck. Qualityextraction newpacket independentlyPASSedbyresearch_reconciliation forreceiver/transport/camera only; actualROI/clippingproduceravailability notcertified. BuildverdictPENDING.

## Standalone facade isolation — preliminary read, not final code PASS

Reviewed current builder FreeplayStack/FreeplaySystem/BehaviorManager and unchanged EngineNeedsOwner offline adapter. The design preserving historical Create(ctx,clock,needs,...) as an independent standalone manager is compatible with the canonical engine owner correction: existing UseNeedsForOfflineTest explicitly throws on a live engine, so implicit custom-needs engine replacement was not a previously working live path. Offline tests that need the engine to tick their replacement Needs must invoke that explicit test-only admission; preserving ctx.Needs alone does not give engine ownership. Report this intentional fixture setup change honestly rather than saying every prior offline side effect is unchanged.

Actual current diff still needs narrow isolation confirmation before final PASS: BehaviorManager's bindSensorOwner:false prevents replacing the sensor delegate but does NOT prevent its constructor/BindFreeplayInputs from assigning ctx.Arbiter.ManagerDispatchesReactions; Create defaults ctx.Mood to canonical native owner and can register a host-clock watcher if its callbackID is0; and CreateNative returns public Context referencing the canonical object, so passing that Context into historical Create currently mutates its Needs/AI/Penalty fields. Independent manager identity alone cannot establish no canonical-owner mutation. These concrete public alias/input cases were sent to manager/builder for a guard or source-backed availability proof; no proposed generic refactor or unsupported new API exclusion. Standalone custom contexts/inputs must remain usable, and returned native observer Dispose/Tick must remain observational.

No tests/build/source edits by checker. Pending final stable diff and focused execution; this paragraph is an issue list for the active implementation, not the completed review verdict.

C7 LC1–9 independent sourcePASS recorded: actualfactory/typedbase/rootJSON and completector+LoadConfigwrite-set, inactive46recipient/Stopno-op, rawflags/base scores/commonhistory. ScopeNONRUNNINGlosingLook duringDOrunning; nextselectedLook remainsexplicitowningMISSING. Noactionbody/featurecompletenessclaim. No tests bychecker; finalbuildPENDING.


## Stable-core static review checkpoint — PENDING, blocking corrections identified

Read-only review of builder checkout, against baseline ac8d42a4 and the independently checked native contracts. No checker tests/builds executed; builder testhost is active. This checkpoint is not a final build PASS. The concrete configuration geometry/producer text was read completely: signed MIN/MAX identity, MAX-first geometric binding, UP/seed versus DOWN carried asymmetry, root/local coordinate distinctions, independent per-container queries/processed sets, old-first reuse without current dedup, ordered pyramid pruning, constructor-empty snapshots, and staged publication were compared with the checked CM13-05 and whole-pilot packets. The action diffs preserve separate current selected name and cached playback clip, concrete default6/String8 wire, synchronous matching-tag callback, and semantic clip destructor equality. These observations do not close the remaining full-pilot source stops or integration tests.

Blocking finding D1: CubeGameBehaviors.cs BuildPyramidBaseBehavior.Targets (lines591–599 in this snapshot) still reads M.Configurations public host membership twice and public PyramidBases.FirstOrDefault. These directly select behavior targets and violate the operator-approved isolated-extension boundary. Use native IDs/cache for these reads; no native midpoint is needed. The authorized OnConfigSeen Recent diagnostic projection after actual native admission is a separate exception and must not become an eligibility input.

Blocking UNKNOWN finding: ManipulationSystem.cs native accessor family (lines119–135) returns empty arrays/null/false when InputsAvailable=false, although NativeConfigurationManager correctly retained its complete prior native publication. This converts unavailable geometry into fabricated absence and may change source behavior eligibility/target exclusions. Preserve the retained publication for consumers when one exists, or explicitly skip the entire unavailable consumer unit without changing prior state; a diagnostic alone does not make empty a source result. Strong-cache accessor has the same availability issue to review with this correction. Sent both findings to manager and builder before any correction.

API compatibility question sent for resolution: replacing DriveStraightAction optional-speed constructor removes the prior named-only shouldPlayAnimation call shape. Preserve that supported source-level call shape with a compatible overload unless the manager establishes it was not a supported API contract; direction-dependent two-argument speed must remain the newly recovered default. No unrelated API redesign requested.

Snapshot SHA256 values below identify bytes read, not approval of files with findings. Any later changes need affected recheck; unchanged files need not be read wholesale again.

- `Manipulation/NativeBlockConfigurations.cs`: `387a6e706fb6ff6a2b75b365ee6a1f2b735ab229983acf4b77b958a0e52f30d3`
- `Manipulation/NativeConfigurationGeometry.cs`: `0bdfb280399871570450c53f1c6fb13c17b99520b789b3ae56d29b4ba79d163e`
- `Manipulation/NativeConfigurationManager.cs`: `920cd85d29178b41dacb1d3f33896e0258364192bf05e68a9d15e6767da78375`
- `Manipulation/BlockConfigurations.cs`: `5302886a73e3a23439d39838fa92fab1b5518e4eaf18cf78e90709d9dccdb006`
- `Manipulation/ManipulationSystem.cs`: `d30948d25689408a620309d57d4937e0039231ed73288c2bea171d7a959fd568`
- `Vision/BlockWorld.cs`: `fc834f0df2cfb047592c055275303e5c29b4e706a86e9fb1cd2aaf23a0964d4b`
- `Behavior/CubeGameBehaviors.cs`: `42e66c6f5346228fa336bcc2635adf0b86d9fce20a1a66f1d3a2742f7d85fbce`
- `Behavior/ChargerBehaviors.cs`: `bc9e0a3ba65057554955dfe9c060a542c41e9fcab004ce3338c5ce1b7d1591bc`
- `Behavior/SteppedBehavior.Actions.cs`: `ba0fd6612c40b4cfd553eb38472fcd3cfe8301e532c0198c1ba66f4e3edda424`
- `Manipulation/DriveActions.cs`: `618f16898c9a6176c110822749759f0dfece17367127f9e61810b36394803748`
- `Actions/ActionWatcher.cs`: `65eee66f7ea331ef6b6f6bc151c2520c674091f8b8c25f9c2ffcc4d9da1ff498`
- `Actions/IActionRunner.cs`: `f1f73d16586f0acbb1bfab31c7cf7e6e0b42bcb271df7c4318ca5acbc434107e`
- `Actions/RobotCompletedAction.cs`: `ad0d1481f1a443a25346d4abb4cbfd4539845e1ee497f4725487a8139cd4a3a3`
- `ActionCompletionReport.cs`: `79251fc3eaad742c50054a6bcf4697cec40a03ac2f4282f38a4d962c39eec385`
- `Animation/TriggerLiftSafeAnimationAction.cs`: `53f95d7c968bba87b2c71dda2ce9aba28b401fecb77987762577e781ccf5506a`
- `Animation/AnimationScheduler.cs`: `769b42d53dc6d6f6f5c2c1acebb30a464e16188eb472871c82db9b9e6e2777af`
- `Animation/CozmoAnimations.cs`: `758e347b793bf95c723b3789f63d71258f05b112eba3d491d209ed93bf66b1f4`


### Affected core corrections — static recheck

Builder correction now inspected: BuildPyramidBase.Targets consumes native membership and MIN/MAX IDs; no host cache/MID input remains in those three reads. Existing canonical native manager accessors now expose its complete retained snapshot on InputsAvailable=false with an UNKNOWN diagnostic, and absent owner raises typed NativeConfigurationUnavailable rather than fabricating empty/false. The strong-cache copy likewise returns the retained snapshot; its existing explicit null/unavailable caller boundary remains separate from a valid constructed empty cache. Direction-dependent DriveStraight defaults retain the historical named shouldPlayAnimation call shape through a bool-only overload. These specific static findings are resolved in the inspected bytes. Focused tests are still builder-owned/running; no independently executed tests or whole build PASS asserted.

- `Manipulation/ManipulationSystem.cs` corrected SHA256 `2d51d7b565eba3b73baf578fd73d5efb3731dc93cc609c3c69e27fe60424c60b`
- `Manipulation/BlockConfigurations.cs` corrected SHA256 `6fb207c654b562e0bd73e4a645c61242996a7cb7d32507c5561f2413a2a602d9`
- `Behavior/CubeGameBehaviors.cs` corrected SHA256 `42e66c6f5346228fa336bcc2635adf0b86d9fce20a1a66f1d3a2742f7d85fbce`
- `Manipulation/DriveActions.cs` corrected SHA256 `618f16898c9a6176c110822749759f0dfece17367127f9e61810b36394803748`


## Remaining-unit static checkpoint — PENDING integration and tests

Read actual configured runtime/base inputs, concrete losing Look candidate, actual per-activity objective subscription owners, facade ownership guard, native Mood binding/loading/clock, Vision worker/result queue/raw mean/quality receiver and Camera API diff. No checker tests/builds executed while builder hosts run. Correctly bounded: only configured PutDownDispatch admission is built; unsupported specialized activities retain prior selection visibly. A selected Look action retains prior execution and reports MISSING rather than running a fake successful action. Constructor source default41 is a filter invariant, not permission to suppress changed objective configurations.

Two additional concrete defects sent to manager/builder and accepted for correction: (1) Look objective callback is unconditional no-op with no root/activity scoring objective41 guard, despite the proof applying only to absent/default41; (2) MoodModel.LoadNative parses a present empty/unreadable resource without handling failed-reader/null continuation, so watcher/owner construction can be lost. N1-N8 requires visible failed-input handling followed by retained/default curves and actual watcher/update continuation. No broad mood parser or objective implementation expansion requested. These findings remain pending affected source/tests recheck.

Facade alias corrections inspected: canonical Context or a nonnull canonical Arbiter is rejected before constructing the standalone manager or mutating ctx fields; default standalone Mood is independently constructed; explicit canonical Mood is not given an extra host-clock ActionList watcher even when native callbackID is0. Host Dispose uses its own manager/severe owner and canonical observer Tick is observation-only. Canonical ownership mutation findings from preliminary pass are addressed statically; integration regressions still required.

Vision receiver static comparison: raw mean byte and ascending set-bit mode vector, tag43 timestamp/count/U32modes/mean payload; per-drain aggregate reset and continued result drain; entry-captured Mood clock; quality unsigned strict elapsed threshold, raw prior-quality assignment after synchronous broadcast, repeat globals reread; camera validation before reliable request/pending/API and live auto flag are represented. Producer ROI/rectangle/clipping and other handler stops remain explicit; no full vision source/acceptance claim. Worker completion queues owned pose copies and result application moves host FrameProcessed outside processing lock; final lifecycle tests/joins and changed fixture cleanup still need review.

Source bytes inspected (SHA256; this list is a checkpoint, NOT whole-file or whole-pilot PASS):

- `Behavior/NativeBehaviorRuntime.cs`: `fb2214fef04b3fbf52a9c96648edac0d373f252fa6cdae6d80ae26171e5f72b5`
- `NativeBehaviorRuntime.cs`: `5f7e39581738fafe3db50588688642a3c3b467e1ca02403db96b348048d288c2`
- `Behavior/NativeBehaviorBaseInputs.cs`: `f5a33e00a647d76b28364b1deabc773954f7bd7c2e657f0b7aed80adea31512e`
- `Behavior/NativeLookForFaceAndCubeBehavior.cs`: `ec678a4ec646b962d8a73b6af96128e2514b2d3c940aa8addcbc6e833fa0e9c0`
- `Behavior/NativeActivityObjectiveOwner.cs`: `c93c96dcd22977816f420eb148529c186328f0e22b66403d039b876fcaa2d345`
- `Behavior/Activities.cs`: `c79830df788386dbeb176f1241a611029ab01ff260be078fbaf62a18766bdafe`
- `Behavior/BehaviorContainer.cs`: `61814932df5b82d2c969de668c36b9b2aeda2fd10ced1fbe8d17f6a5f8804e94`
- `Behavior/FreeplayStack.cs`: `fa644aff3a7dc3542091a3f890137d405c8c3e765052900ca06ca46091c3875d`
- `Behavior/FreeplaySystem.cs`: `840081077add07246ce85c5b7de5e16e8df6101a803b91f3b4b65c611bcc03e5`
- `Behavior/BehaviorManager.cs`: `0fcd0b0f50fc1ed1e5e19a80687910f5760f5c798ac95165882ea529ff2a3d80`
- `Behavior/Mood.cs`: `3fa3f11557134da81c4f962d75edeb5146c4d5e6ad44a22b3c8d3deb6353e4e1`
- `Behavior/MoodLifetime.cs`: `658b907bbbceaf4c3bb43898ead927caca46ec3485959b1d702d429e43f8d1e9`
- `Behavior/IBehavior.cs`: `8c4014d99b25bc550ef1ba09d920b784581022b1badbd88de409738c67998ec4`
- `Behavior/SteppedBehavior.cs`: `125480ed38534bcbdf28e98f82e5be41270f857c1a6b2cbef5c9112ac1c579c5`
- `Vision/VisionSystem.cs`: `af0aa94d5c1f721d9d65fc1dd06d21161471c08b15a851379c216a7f592c7802`
- `Vision/VisionResultQueue.cs`: `4f085642bc6bf34537b6568c280a108acbc4b05bba8bede8c4b9652c722d3a5b`
- `Vision/VisionImageQualityReceiver.cs`: `c84fb81176d15ced7c13fd98366771e3f7a6f3f63a2c7b8ecb52747f0df6d4fc`
- `Camera.cs`: `c314a51d46a67450ff7f95708bd56277bf83f0b8fe8ab971e5eb307c344a8151`
- `CozmoEngine.cs`: `6130af1031e48da93f1146490bf2d27a6c4ee2ca488cde29580b3d8248271b76`
- `CozmoRobot.cs`: `7baf3a045aa004f6fb40fa716f65779f729b3f09f3cc61ddba3920caa043df28`


Mood delivery join independently primary-PASSed and detailed in read-check: typed-only output omits required rawAPI61/local61. Required actual external guard, appenqueue-before-localemit,37byteU8count9+9f32 payload. Builder correction pending; no test-only suppression accepted. Narrow ScoringChooser.OnSelected absence also checked: Activity005B314A calls chooser vslot20; Scoring vtable0102CBDC+8+20→0060A7BE BX LR, so no missing chooser mutation on the admitted scoring route. No tests executed by checker.


## Independent stable-candidate execution and affected review

Actual independent run from builder worktree (only testhost active; builder held mutations):

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-build --no-restore --nologo -v:q --filter 'FullyQualifiedName~DriveOffPilot|FullyQualifiedName~NavigationTests|FullyQualifiedName~FreeplayTests|FullyQualifiedName~CorrectionTests|FullyQualifiedName~HelperStopAndPyramidLoopTests|FullyQualifiedName~M12CheckedRowsBuilderTests|FullyQualifiedName~BehaviorFrameworkTests' --logger 'trx;LogFileName=codex-independent-focused.trx' --results-directory .scratch/driveoff-pilot
```

Exit0: **303 passed,0 failed,0 skipped**, duration1m7s. Console/TRX: builder `.scratch/driveoff-pilot/codex-independent-focused.log` and `.trx`. This includes current new actual-worker marker-failure/continued-drain/Robot.Update gate/reset test. It is independent execution of builder tests, not independently authored test coverage or hardware evidence. Explicit Python313 `-X utf8 re-analysis/tools/fidelity.py --check` independently exit0,710records; output builder `.scratch/driveoff-pilot/codex-fidelity-check.log`. Manifest statuses unchanged. All51 individual handoff source/test hashes exactly matched `.scratch/driveoff-pilot/candidate-sha256.txt` before execution; no checker code/test changes. Builder-reported aggregate2a91a0b... identifies its serialization convention; exact per-file matches are the independent byte comparison.

Affected static corrections: Mood failed-reader now handles empty/read/parse failure on native load and continues defined null-input, keeping actual owner/watcher/periodic update. SendEmotionsToGame preserves nullRobot/actual captured native external-interface guards; app raw+typed is queued before local61 sync delivery,37byte count9/f32 payload. Standalone AttachRobot captures actual native owner rather than inventing interface presence. Native root-cache disposal (including UNKNOWN single removal, staged/old roots and destruction) now uses actual PoseTreeGate in existing World.FrameGate→manager→pose-tree order. Native retained-publication getters and all three BuildPyramid target reads remain corrected.

The three Navigation oracle changes were read fully: expected action distances derive from fixture captured initial object/main-pose vectors (Flip norm+20; KnockOver distance-85; Ram distance), with independent f32 stored-forward endpoint recipe; old action displacement ranges, speeds, packet count/order and success/arrival assertions remain. No expected distance is backsolved from emitted path endpoint. Those tests remain fixture/offline evidence, with existing libm/quaternion boundary and0.01position tolerance explicitly labelled; no firmware model edit or physical-position relaxation was used.

New threaded vision fixtures join via Vision.Dispose in finally before rig/history/pose/ROI owners retire. Configuration fixtures release actual robot/native publisher before admitted child poses. Actual configured route loads shipped animation assets; raw robot states/CA/CB/C6/calibration and explicit RNG words are supplied inputs. Actual canonical PutDownDispatch factory→manager chooser→DriveStraight ActionList/path wire→received completion is reached; Meet/Feeding/selectedLook are not claimed. Vision tests use real worker/raw-image decode/producer/result drain with substituted admitted calibration/world-status/ROI; they do not claim real robot JPEG or live rectangle collection.

**One final blocker remains at this checkpoint:** the C7 callback's new default41-first filter reads `BehaviorObjective`, but actual native factory/root/scoring JSON never assigns that property. A changed `considerThisHasRunForBehaviorObjective` therefore silently remains41 and misses the promised MISSING branch. Root accepted this finding; builder now owns a narrow actual-root/activity input guard and actualJSON regression. Shipped absent/default41 case passes. Final candidate PASS withheld only pending that affected correction/recheck; no whole-repeat audit or full-suite run requested.

All full-pilot boundaries remain: actual audio object1/scoped active observers/scheduler/render STOP; live ROI rectangle/GetROI producer STOP; Meet selector/Feeding.Update/selectedLook and other named owner/lifecycle handlers remain MISSING. Passing303 tests does not turn these into absence/no-effect/exclusions or whole behavior acceptance. Hardware and acceptance remain unperformed. Actual selected worker model/reasoning telemetry is unobservable.


## Record-wide handoff recommendations — current records, no status changes

Current literal titles/status/evidence reread before these recommendations:

```json
{
  "id": "M7-026",
  "title": "MoodManager::GetSimpleMood and the mood time MoodManager+0x130 (inputs to the animation group draw)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m5-x9-extraction.md section 4f: GetSimpleMood 0x0067C084 (Default; Happy > 0.33f -> Happy; Confident < -0.29f -> Sad; Happy < -0.33f -> Sad; constants 0x0067C0C0..0x0067C0C8); the group draw reads MoodManager+0x130 (0x0058A95C), written only by MoodManager::Update(t) 0x0067B67E and Reset/ctor."
  ]
}
```

```json
{
  "id": "M8-027",
  "title": "ProtoHandle subscriber-callable census: which callables the 8 ProtoHandle kinds hold at teardown and what destroying them does",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov53-55-check.md: ProtoHandle vtables (8) confirmed; EngineToGame release 0x00664708, unlink 0x006634F4 (nulls the callable, decrements node refcount +0x20, frees at zero 0x0066352E..0x00663560).",
    "re-analysis/research/overnight/20261016-ov105-107-108-answer.md: void() signal 0x0105DF8C: one subscriber via RegisterTransport 0x0083B9A0..0x0083BE4C (bind vt 0x0103795C trivial); Dispatch signal TransferQueueMgr+0xC: two RegisterTask::$_0 (vt 0x01031418, destroy memory-only) from TransferTask::Init 0x006A4358..0x006A438E; release bodies 0x004EF138, 0x0051D8F8, 0x00591550, 0x0064E618, 0x00664708, 0x00664EB8, 0x006A3EFC, 0x0083BED0 identical; ~TransferQueueMgr 0x006A3860..0x006A3898."
  ]
}
```

```json
{
  "id": "M13-017",
  "title": "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ctor 0x005C0980 stores 96.0 + json extraDistanceToDrive_mm at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6); IsRunnableInternal 0x005C0B10 returns robot+0x34A (the on-charger-platform flag; contacts are +0x338)",
    "InitInternal 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions only when robot+0x355 == 0 (0x005C0B54); +0x355 is the robot's OffTreadsState (OnTreads = 0), written only by Robot::CheckAndUpdateTreadsState 0x00512088/0x0051208E and named by OffTreadsStateFromString 0x0078DF58 (OnTreads 0, InAir 1, OnBack 2, OnLeftSide 3, OnRightSide 4, OnFace 5, Falling 6)",
    "TransitionToDrivingForward 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08)",
    "UpdateInternal 0x005C0DA8: once off the platform (robot+0x34A == 0) it records the time at robot->[+0x264]->[+0x18]+0x44 and returns 2 (0x005C0DF4..0x005C0E0A); while still on the platform and robot+0x355 != 0 it calls StopActing(false,false) (0x005C0DB6/0x005C0DC4) and waits; on the platform with +0x355 == 0 it transitions to driving forward (0x005C0E18)"
  ]
}
```

```json
{
  "id": "M13-040",
  "title": "Contact-true charger observation: live ingress, located identity, Known pose and ordered world/contact effects",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "RobotState sync gate 0x0051293C..942, contact bit12 0x00512AAC/AAE and SetOnCharger call 0x00512AB4; SetOnCharger 0x00511990..0x00511C2D observes on every true input before old-contact gate 0x00511BA8/BB0. Contacts +0x338; platform +0x34A.",
    "ChargerDockPose 0x004EA304..0x004EA35A: Z rotation pi bits 0x40490FDB, translation (30,0,0), robot pose parent and ChargerDockPose name. Located family4/type13 first/current-origin query 0x00511A1C..A52, 0x0061EBF2..0x0061EC26; factory and connected identity/unique SetID 0x00511AD6..0x00511B40, 0x004EF468..0x004EF528.",
    "Known observation 0x00511B4A -> 0x00506B70..0x00506C48: reparent/root flatten, existing/new branch, SetPoseHelper 0x0050609C, distance -1, confirmer count1/reference/existing timestamp. Actionable cache invalidation 0x004E01E4..226; common SetPose 0x004EF590..646. AddLocatedObject 0x00622AC0..EB0 includes obstacle deletion, VizManager pointer store obj+0x54 (0x00622D24..0x00622D30; CozmoContext+0x24, ctor 0x004EA734; read by EraseVisualization 0x004EA5E8..0x004EA5F2): viz, no effect on 1-4 and notification.",
    "Notifications 0x00506F98/FA8/FB8 world/map/state-change; unchanged return 0x00506296..29A; changed nonzero new state map 0x005062B4 and active light recipient. 0x00639840 compares NEW state r3 with1: Known trigger0x25/layer2, other changed trigger1/layer2; unchanged no request.",
    "Contact edge/store ordering 0x00511A66..ACC, 0x00511B50..0x00511C14; platform setter 0x00511D44..DCA; existing footprint tick 0x00513CD8..0x00513E2A and M4-019 threshold send 0x0063427E, 0x006342C8..CE, 0x00634364..368.",
    "re-analysis/research/20261021-m13-017-located-charger-check.md: independent check of A1-A11/P1-P4 with corrected A9; supplementary mount and exhaustive caller census excluded."
  ]
}
```


- **M8-027: RECORD-TO-SETTLE candidate for Claude confirmation.** Its CURRENT unresolved explicitly says every held callable release/remove is memory-only, no log/message/invocation and nothing behavioral remains to build. Current UI hub nulls/unlinks callbacks, retires signal token, and checks node value before snapshot dispatch; GC replaces native refcount/storage. No allocator/captured-pointer imitation is needed. The stale captured-finalization diagnostic is queued cleanup, not a reason to manufacture an engine behavior. This is a confirmation recommendation under the record's existing evidence, not whole subscriber graph or subsystem acceptance.
- **M7-026: conditional RECORD-TO-SETTLE candidate, not yet final at this checkpoint.** Actual Mood.GetSimpleMood and group CurrentMood/cached130 bindings were built and reviewed. However an additional owned consumer in CURRENT unresolved, DrivingAnimationHandler004F0D2C..74, still used only a test seam and default/needs fallback. That concrete residual was found during record-wide comparison and released for correction in this same candidate; settlement recommendation waits the affected code/tests. No extension to full Mood schema/event/actions/lifetime acceptance follows from this named record.
- **M13-017: retain IMPLEMENTATION_GAP / remaining-record handoff, no record-wide settlement.** The specific behavior state machine/admission gate/distance/wait/redrive/owned action5A/whiteboard/objective/mood path is now implemented and exercised through canonical configured PutDownDispatch plus admitted full states and real received action/animation events. The record's broader live production path still encounters actual audio recipient/active schedule/render STOP and live default quality ROI/other handler producer availability, with general Meet/Feeding/enclosing activity and selected Look boundaries explicit. Markerless/Ramp/HumanHead footprint inputs and broader named cached-pose/component lifetime obligations have not been certified by this pilot. Do not call those no-effect, excluded or covered by tests of source-backed pieces. Conversely, old claims that all native pose-notification/map-height/cache/insert continuations are simply absent are stale: current source packets G4/G5/S12 and actual code recover positive branches and native staged configuration producer; cite precise residuals instead of repeating old blanket prose.
- **M13-040: retain IMPLEMENTATION_GAP / exact shared-dependency reconciliation, no whole-record settlement.** Existing received synchronized contact-true identity/current-origin/Known observation/cache invalidation/world/map/light/contact ordering remains the checked reused path. New native SetObjectPoseChanged/common container/count/tallest/frame-tail publication closes that earlier concrete producer gap, and current positive map cache/relative-height/quad/insertion bindings exist. The conditional configuration notification still reaches an unbound real object1 audio consumer; live frame/default quality producer and noncube footprint availability plus the current record's broader cached-pose destructor ownership boundaries are not newly certified. Whole title includes ordered world/contact effects, so this candidate cannot erase those dependencies merely because direct contact/platform/action tests pass. This is not a claim that every historical unresolved phrase is still an unimplemented algorithm; manager/Claude must reconcile each current whole-owned boundary.

A source-checked partial candidate can be handed to Claude with this RECORDS-TO-SETTLE versus REMAINING register and a precisely labelled implementation verdict. That does **not** complete the whole DriveOff pilot, grant hardware verification, allocate proposed owners, approve exclusions or change any fidelity status. The operator's whole-behavior goal remains open; the finite checked improvements are reviewable without disguising the real stops.

### C7 actual-input affected recheck

Builder changed only the accepted four files: actual Look root key presence marks objective input unavailable; native activity loader checks actual typed-Look entry.scoring input; callback checks availability before invalid41 no-op; actual PutDown admission checks the relevant chooser candidates before Stop/OnSelected/public-state/motion. No global suppression, arbitrary objective mapping, fabricated score0 or fallback candidate was introduced. The two new tests modify actual copied root/activity JSON and traverse resource factory/tree/subscription/app admission, rather than manually setting the property. Independent affected command uses the same no-build flags/project/logger pattern with `--filter FullyQualifiedName~DriveOffPilotTests`, logs `codex-independent-c7.log/.trx`: exit0 **7/7 passed,0 skipped**,1s. C7 finding resolved in inspected/tested bytes. No whole repeat audit or suite run.

### Additional M7-026 driving consumer primary read contract

Known004F0D2C then primary004F0D2C..74: nonempty override stack wins at30..3A before Mood. Empty stack unconditionally loads actual handler+4 Robot, Robot+440 Mood at3C/3E, calls GetSimpleMood004F0D42. Only RAW1 selects triplet94/90/8C; other raw values use existing Needs.AreNeedsMet at50..66; copy6A..72. There is no native null-Mood-to-default fallback. Missing native owner/Mood is an M1-034 smallest selection skip preserving prior Current with visible M7-026; do not invent needs/default selection. Preserve explicit test override, otherwise read actual BOUND handler owner, not a replaced facade robot.

Numeric correction recorded explicitly: my first message incorrectly labelled raw1's triplet “happy.” Fresh known0067C084+primary0067C084..BE confirms constructor result2(Default), Happy>0.33 changes0, negative Confident/Happy changes1(Sad); current enum is `Happy=0,Sad=1,Default=2`. Thus Driving's raw1 is SAD, while Happy0/Default2 continue needs. The manager caught this label/test error before implementation; corrected contract and actual Sad/Happy/Default/override/null tests were released. No source code was authored by this checker. Final M7-026/candidate verdict remains pending affected build/review/tests.


### Driving selection Needs owner — bounded independent primary PASS

Current literal record before reuse:
```json
{
  "id": "M15-001",
  "title": "NeedsManager initialization, periodic update and need-bracket rules",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "CozmoEngine::Init 0x004EC9E6..0x004ECA0A",
    "NeedsManager::Init 0x00692574",
    "NeedsManager::Update 0x00695C9C",
    "NeedsState::UpdateCurNeedsBrackets 0x0069C12C",
    "needs_config.json",
    "NeedsManager::Init time threading 0x004EC9DA..0x004ECA0A, 0x0069257E, 0x006926CE, 0x006934B0, 0x0069358A..0x0069359E",
    "NeedsManager::PossiblyWriteToDevice 0x00695DC4 (0x03A2C940 against a microsecond clock = 61 s; ApplyDecayForTimeSinceLastDeviceWrite divides by 1e6 at 0x0069532C)"
  ]
}
```

Known preflight for NeedsState::AreNeedsMet, NeedsManager::GetCurNeedsStateMutable and IsNeedBracketMet preceded primary reads006960A8..BA,0069CB4C..B0,00782986..9E plus already reopened004F0D50..66. Actual bound Robot dereferences context+34 NeedsManager; getter returns embedded state+8 after UpdateCurNeedsBrackets with manager+140 config. AreNeedsMet refreshes state bracket cache and checks NeedId0,1,2, short-circuiting false; IsNeedBracketMet returns true only valid bracket0/1, with false fallback for unknown>=5. Current C# NeedsState uses Repair0/Energy1/Play2, Full0/Normal1 and bracket refresh. Canonical EngineNeedsOwner initializes Engine.Needs before RobotManager; bind this actual bound-owner Engine.Needs.State, never a standalone Freeplay context replacement. Existing explicit offline replacement remains the admitted owner in offline tests.

Required source behavior: after no stack override and SimpleMood not raw1, actual Needs all met selects96/92/8E; any Warning/Critical selects95/91/8D. Preserve explicit test seams; missing actualowner/Needs is null dereference under M1-034, skip smallest selection unit preserving Current and visibly diagnose, not invent default false/true. New production-owner tests must mutate canonical needs levels via existing actual component input and assert both selections, without replacing AreNeedsMet delegate; Sad1 must bypass Needs, stack override bypasses both. This is only producer-to-consumer binding and numeric branch verification, not a new whole Needs audit or M15-001 settlement. No code/tests by checker for this read unit.


C7 exact null-boundary follow-up (before final verdict): reopened primary005BC574..57A uses Json.isNull, so explicit JSON null preserves41 exactly like omitted key. The first actual-input guard rejected ANY present key, including this defined no-op; manager accepted a tiny correction to admit explicitnull while retaining MISSING for nonnull unknown overrides. Prior7/7 confirms string-input guards and unchanged shipped path, but final C7 PASS awaits the null correction and combined affected run. This does not invalidate the prior independent303 result for its exact bytes and tested inputs; it prevents overclaiming the newly inspected null branch. No further whole audit or code/test changes by checker.


### Legacy M5 driving oracle disposition (tests-only, before final affected run)

Independently read unchanged M5X15.MissingMoodAndNeedsFallThroughToDefaultAndQueueARealRunner and M5X19.MissingInputSelectionFallsThroughAndNonDriveMissingIsNotDeduplicated after builder reported16pass/2fail. Both use CreateOffline and actual constructed/bound canonical owners, not deliberately missing dependencies. Fresh Mood values0 give Default RAW2; canonical NeedsState initializes configured/default levels and its actual Full/Normal brackets yield all-met. Source004F0D50..66 therefore selects96/92/8E. Old95/default and missing-Mood/Needs assertions are stale inputs, not evidence for changing production. Authorized correction must assert actual canonical prestate (Default2 and each relevant bracketFull/Normal, no test delegates), then exact96 and absence of false M7/M15 missing diagnostics.

M5X15 must retain queue count/actual ActionList updates and strengthen real5A LoopId/type26/result0300000C NO_ANIM_NAME/variant1 empty-name proof in place of obsolete concrete-lifecycle-gap wording. M5X19 keeps exact retirement6cancel-recipient diagnostics unchanged. No firmware change, relaxed acceptance interval, injected completion or production behavior adjustment is justified by those two failures. Builder changes/tests and final affected review remain pending.


### Final combined affected execution — actual failure retained

Independent final affected command used prior no-build project/logger flags and filter `FullyQualifiedName~DriveOffPilotDrivingMoodTests|FullyQualifiedName~DriveOffPilotTests|FullyQualifiedName~DriveOffPilotMoodTests|FullyQualifiedName~M5X15BuilderTests|FullyQualifiedName~M5X19BuilderTests`; log/TRX `codex-independent-final-affected.*`. Actual exit1: **46pass/1fail/0skip,total47,3s**. All new driving Mood/Needs, actualJSON null/non-null objective, native mood/API, real5A and corrected legacy prestate units passed. Only M5X15.DrivingStackErasesFirstMatchAndChangesCurrentOnlyOnInit failed line87: expected two ordered PopDrivingAnimations log keys, actual same two plus legitimate MoodManager.Update.TimeStepTooSmall from the newly live canonical periodic Mood update. Test subscribed to every engine LogEntry, so unrelated diagnostics polluted its exact list. This is a diagnostic collector fixture defect, not evidence to suppress source Mood logging or alter stack behavior. Root authorized only filtering collector to DrivingAnimationHandler.* OR LoopBoundOverflow; retain exact ordered warning/overflow assertions. Reverify only this changed fixture; the unchanged46 passing cases are not silently represented as a47pass run. No fullsuite.

Affected source inspection complete: bound_owner Mood and Engine.Needs, rawSad1 and other0/2 branch, override-first/null-M1 preservation match primary; explicitJSONnull now shares source isNull→preserve41 with omittedkey. New actual-level tests use canonical State.SetNeedLevel with admitted config thresholds; the separate Boolean-seam cases are honestly labelled and test short-circuit order. Each queued action is canceled in finally before robot/resources retire; no new worker barriers or unsafe early pose disposal added. Final scoped verdict waits the single collector correction/run and final hashes.


## Final single-fixture correction and closeout

Independent inspected collector now admits only `DrivingAnimationHandler.*` or exact `LoopBoundOverflow`; all existing exact two-Pop order, current-only-on-Init, first-match removal,1000bound/errorflag and entry-count assertions remain. Production files are unchanged by this fix. Single affected test independently exit0,1pass/0fail/0skip,212ms (`codex-independent-collector.log/.trx`). The previous46pass/1fail execution remains above as the actual history; final changed fixture is verified without rerunning unrelated303 cases. M7-026 owning comment inspected at Mood.cs341; no behavior/test bytes changed for that annotation and no extra test cycle attributed to it. Final55hashes0mismatches and fidelity710exit0 confirmed after annotation. Final scoped candidate PASS, whole pilot IN PROGRESS, with record-wide recommendations and genuine remaining dependencies stated at the top. Manager owns commit/full gate/Claude/hardware/settlement.
