# Autonomous M13 manager trial — 2026-10-26

Operator request: pasted Autonomous Codex Manager Trial instructions. Trial overrides the normal builder queue/lane; at most three justified bounded implementation jobs. No main push/merge, fidelity status changes, approvals, Claude-check edits, or hardware commands. Codex-only reviews remain Claude spot-check pending.

## Starting baseline

- Original checkout: main 2dd741c7, clean. It has not been changed.
- Existing trial worktree reused after inspection: `.scratch/worktrees/codex-m13-manager`, branch `codex-m13-manager`, clean at 1617322790b02cbd0457f719de55345663c8f21e. Fetch succeeded; this equals fetched origin/main and origin/codex-m13-manager. No branch reset or recreation.
- Manifest: 710 records: EXACT_SOURCE 239; EQUIVALENT_IMPLEMENTATION 6; COMPATIBILITY_POLICY 68; HARDWARE_ONLY 14; BLOCKED_EXTERNAL 2; IMPLEMENTATION_GAP 358; RECOVERABLE_GAP 23. Accepted subsystems: M1-transport, M2-protocol (4f184775), M3-device, M4-control (e95d4acd), M5-animation (ef1fc589). Target M13-017/040/003/030 all IMPLEMENTATION_GAP. Full target titles/evidence will be preserved in the reconciliation package.
- Latest relevant hardware: `re-analysis/acceptance/hardware/20261026-072854-M13-DRIVEOFF-HW-310c47`, on a7af18cd, all six sourced checks MATCH. Root-growth MISSING absent; driving animations first graded. No records settled. B-M11-22 map/object timestamp correction 27316c63 subsequently passed Claude check in 16173227; not part of run 3.
- Historical queue/status views still list B-M11-22 open; commit/current Claude check establish it is complete. These views are context only; no main queue work claimed.
- Current blockers/evidence reconciliation: delegated below. Existing manifest unresolved prose predates many recent builds and must not be mistaken for current implementation state.

## Coordination and configuration

- Pause requests sent to existing builder queue, overnight research queue, and duplicate manager chats. Duplicate manager confirmed it stood down, its two researchers paused, and it had already pushed the initial trial branch; it reports no edits/commits/tests/hardware and requests no reply. It had also sent pause requests to ten Cozmo chats.
- Main manager configuration cannot be independently inspected through available tools. Operator requested Sol High; no unsupported claim of actual configuration.
- Agent A `/root/research_reconciliation`: requested gpt-6.1-sol, medium, fresh context. Read-only: target-related RECOVERABLE_GAP reconciliation, existing evidence, duplicate/obsolete investigation requests.
- Agent B `/root/acceptance_blockers`: requested gpt-6.1-sol, medium, fresh context. Read-only: target production paths, ReportMissing census, run-3 events, reusable evidence and smallest useful jobs.
- Spawn responses expose task identities only, not actual applied model/reasoning telemetry. Requested configuration is recorded; actual configuration unverified.

## Operations and results

- Initial git fetch failed under sandbox (FETCH_HEAD write denied); escalated `git fetch origin` succeeded.
- Initial trial branch push attempt was rejected by automatic approval review before execution: publication to an unverified remote. No workaround attempted. Existing branch was already published by prior manager, confirmed by fetched remote ref. Further publication remains blocked pending approval review/authorization; local work continues.
- Read AGENTS, PROJECT_STATE, manifest, FIDELITY_PROGRESS, historical QUEUE/STATUS, run-3 judgement, current Claude checks, role/checklist instructions and push/full-test gates. No tests run yet.

## Jobs, verification and proposed fidelity changes

Pending reconciliation. Official manifest frozen; no proposed status changes yet.

## Resume and ownership clarification (2026-10-26)

- Operator stopped all sessions and explicitly resumed chat 01a1786c-a254-78b0-8f15-2329d8ce3669. This chat now owns the trial. Other manager chat confirmed idle via app inventory. Its existing untracked log is preserved, with corrections below.
- This manager created the worktree from fetched origin/main and successfully pushed `codex-m13-manager` with hooks enabled before the interruption. Remote branch publication succeeded (no approval rejection in this chat). The publication block described above applied to the other manager's attempt only; it is not a standing block on this resumed chat.
- Research agents in this chat were interrupted before initialization during coordination; both now resumed read-only with original assignments. Requested gpt-6.1-sol / medium; actual selection remains unobservable in agent API. Maximum active subagents is two.
- Explicit Python313 executable works; the PATH python shim returned access denied. This is a tool-launch issue, not a test failure. No behavioral tests run yet.
- Existing unrelated work preserved. No manifest, inventory, source, Claude review, or main changes.

## Baseline validation and research reconciliation

- Ran explicit Python313 `re-analysis/tools/fidelity.py --check`: PASS, 710 records, frozen counts unchanged. No behavioral tests yet.
- Saved exact current target titles/status/evidence/unresolved plus directly needed M11-074 in `re-analysis/jobs/trial-m13/baseline-records.md` before relying on them. All five are IMPLEMENTATION_GAP.
- Agent A completed read-only reconciliation (requested Sol medium; actual unobservable). Accepted: M11-048 tree geometry/growth/shift/merge are recovered and implemented in B-M13-19, B-M11-19/20 with Claude PASS; do not repeat. M12-049 prune/equality already checked (OV10 K3/OV10 check), but configuration-container producer remains genuine. M11-073/084 remain RECOVERABLE_GAP; partial contact/history/preaction rows do not close whole lifetimes.
- Rejected obsolete leads: Markerless/Ramp/HumanHead footprints are not required by charger query (20261022-r-m13-017-rest-claude-check); severe-needs selection/BehaviorManager lifetime and timestamp gate/seed already built. Row74 last-image seed wording is superseded by 20261026 Claude correction to RobotState. Get-out action admission is separate M8/API work, not an engine-created drive-off dependency.
- New focused candidate, corroborated by A and B: M13-003 still imports all located-object quads (including charger) rather than native current memory-map type3 payload polygons. Existing M11-074 Q1-Q6 and B-M13-24 payload code can be reused. Await B's complete narrow transfer contract before release.
- Deferred genuine gaps: M12-049 native configuration producer (0061682C -> 0062C56E/0062C68C), M11-073/084 retained pose ownership; neither is silently excluded or reclassified.
- Historical review status defect recorded for Claude: 20261022-r-m11-retained-owner-check.md calls M11-073/084 IMPLEMENTATION_GAP; current manifest says RECOVERABLE_GAP. No evidence file altered.
- known.py 004FDF1C succeeded and found the current import owner plus existing evidence. Native binary and indexed functions are available in trial worktree; earlier status reports of missing index do not describe this environment.

## Job release CM13-01

- Accepted Agent B's bounded native transfer contract (004FD508/5E2,004FDF18..7A,0050241C..28; FindContentIf slot0103097C and polygon constructor GOT bindings). It corroborates A's finding. Existing checked Q1-Q6 and payload rows are reused, not commissioned again.
- Released CM13-01 native memory-map obstacle import. Scope: actual FindIf + planner source transfer, preserving cache/padding/collision algorithm. Required production dispatch regression with contact-created charger plus honest unit query/payload tests. Other unresolved M13-003 robot-quad arithmetic and broad producer/lifetime/configuration gaps stay explicit.
- Builder will use separate branch/worktree and may not commit/push; manager will review before integration and run final suite once on committed reviewed code.

## Builder delegation and evidence baseline

- Committed baseline/job contract in 4d02e793 (documentation only). Created isolated builder worktree `.scratch/worktrees/codex-m13-map-import`, branch codex/m13-map-import from this exact commit; no unrelated edits transferred.
- Agent `/root/map_import_builder` requested gpt-6.1-sol medium, fresh context, actual config unobservable. Assigned CM13-01 only. No builder commits/push/full-suite; focused tests/report required. Agent B will independently verify after implementation.
- Agent A assigned read-only triage of existing drive-off objective-callback evidence only, to decide whether any second job is justified. No implementation job manufactured.
- Manager inspected run3 raw events as well as README: M8-015 host action producer, legacy DriveStraight diagnostic, M13-013 AppendLine branch, configuration owner early-ingress gap and public-state input joins remain visible. B-M11-22 fixes the run's image-clock MISSING after the recorded hardware commit. Straight drive run3 does not prove native lattice type3 reader fidelity.
- Correction to inherited baseline log: current trial QUEUE/STATUS at16173227 describe B-M11-22 complete/PASS, not open. Prior inherited note calling those views open is inaccurate. No job was taken from them.
- No additional tests run since baseline fidelity PASS. Full suite will run once after independent code review, on committed code via scripts/test-full.sh, per repository builder instructions.

## Isolated runtime preparation

- New builder worktree initially lacked ignored proprietary evidence/assets. Added local junctions for resources, decomp and obb to existing original checkout, plus obb for manager worktree; no proprietary data tracked/uploaded, no original files modified. Junction creation was denied in sandbox and succeeded after escalation.
- Index caveat: original checkout has no decomp index; the stopped manager had copied an index and binary into manager worktree only. Builders/reviewers were directed to the manager worktree's absolute known.py for preflight (identical frozen evidence), and can read native binary through resources. No behavior was inferred from a missing index.
- Agent A's first followup on objective delivery failed before findings because selected model was at capacity; retried same requested model/effort, no unsupported model-selection claim.

## Candidate CM13-02 evidence decision

- Agent A found objective emitter already recovered in adopted RC3 (20261007-q17-checked-additions.md); independently rechecked005BE228..308/005BE3A8..3D6. Narrow new checks establish E2G0x8B/four-byte LE serializer007285FE/0071BEAE and app-then-local rvalue006625B0. Existing stack delivery machinery is suitable.
- Accepted a PREPARED second job for objective producer/drive-off success only. No job released while first builder active. No invented local consumer or mood owner: M15-059 objective recipient and unattached mood remain visible gaps.
- Agent A explicitly distinguishes no CURRENT stack0x8B subscriber from no native effect; manager accepts this boundary. Existing M1-056/M5-056 support delivery/subscription machinery but do not settle M13-017.
- Preliminary CM13-01 reviewer reports no source mismatch, valid real native planner dispatch test with a synthetic prior obstacle snapshot. Requested a source-map cube admission/height rejection regression and honest reach label before completion. Top stale stand-in comment queued for correction.
- Git shell sandbox launch failed before executing command-v probes; this was not a test run. Final test-full will require normal shell sandbox escalation if the launch restriction remains.

## Test accounting checkpoint

- CM13-01 builder reports logkey15/15 EXACT, ABSENT0. Initial focused compile failed on namespace qualification and was corrected before any test pass claim. Initial parallel restore failed; single-process cached restore succeeded with an audit-network warning. Focused behavioral test outcome remains pending, not counted as passed.
- Added source-map admission regression at normal22mm/high200mm cube poses, accurately labelled unit with actual AddLocatedObject/native height gate/import. Production charger test explicitly labels synthetic prior planner snapshot; its actual dispatch/query/import are not replaced.
- Captured current M7-019/M15-059/M1-056/M5-056 records verbatim in CM13-02-records.md before relying on objective/delivery path. Official manifest unchanged.

## CM13-01 focused execution

- Builder corrected focused run PASS25/25, zero skipped: new native import/query/admission/dispatch16 plus retained timestamp gate9. Local IPC testhost ran successfully after escalation. Earlier90s testhost connection abort executed no assertions and is not counted as a code failure/pass.
- Builder now running old-loop-only negative controls for real charger dispatch, cube height admission and stored-polygon source; corrected bytes restored in finally before any integration. No full-suite pass claimed yet.

## CM13-01 existing-regression correction

- Broader focused run exposed NavigationTests.DriveToObjectWithThePlannerTreatsOtherCubesAsObstaclesButNotTheTarget: old assertion count0 requires removed target-ID filtering. Adopted native predicate00502424 and consumption loop004FDF48..7A have no such filter. Manager approved narrow name/count correction to include admitted target count1, retaining action success and arrival assertions; reviewer will independently check. This is a source-contradicted test oracle, not permission to weaken unrelated tests. Final broader result pending.

## CM13-01 integration / CM13-02 release

- Accepted CM13-01 after independent native/diff review PASS; report20261026-cm13-01-codex-check.md. Reviewer verified binary SHA02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1, call bindings/ordering/identity behavior and real dispatch reach.
- Builder final focused98/98 PASS, zero skipped39s. Prior broader run FAIL1/PASS96 was solely source-contradicted target-ID test; narrow fix independently accepted. Negative old-loop control3FAIL/1PASS (charger/high cube/stored polygon fail, normal cube control passes), corrected bytes restored. Reviewer independently ran dispatch+timestamp10/10 and query6/6, zero skipped. All detailed commands/results in builder/review reports.
- Manager committed verified work on builder branch9d52255b, fast-forwarded codex-m13-manager to same commit. No main merge/push. Code-verified offline production-path source blocker removed; no hardware verification or whole-record settlement. Final combined suite pending.
- Released CM13-02 objective emitter job from Agent A's adopted RC3 plus narrow native delivery findings. One active builder reused only after CM13-01 stopped/reviewed. Same separate worktree will fast-forward trial job contract before starting; requested Sol medium remains unchanged/unobservable.

## CM13-02 source-review checkpoint

- Independent reviewer reopened RC3 body/adapters, E2G constructor/Pack, UI rvalue and drive-off callback in binary before reviewing implementation; contract matches instructions. Actual applied model/reasoning still not exposed.
- Builder found current BehaviorObjective enum lacks4; rather than guessing a native diagnostic name, requested read-only extractor's narrow EnumToString table lookup. Payload is numeric u32 and already settled by serializer; no broad enum recovery is commissioned. Logs remain best effort, but existing native value can be reused cheaply.
- Optional third-job feasibility delegated read-only ONLY for remaining M13-003 RobotQuad quaternion/ComputeOriginPose join using already checked helpers; no third job released or promised. It will be deferred if recovery/verification grows beyond a bounded direct planner change.

## Third-job stop decision

- No third implementation job released. Agent A's bounded feasibility check found state-dependent ComputeOriginPose00517F6C (Robot+284->component+8, default20.0 and alternate offset before008CAE4C). Existing generic pose/footprint rows do not justify substituting a helper. Agent stopped further tracing on request. This clarifies an existing M13-003 gap, not a new bulk audit or silent exclusion.
- Objective4's native diagnostic name is DroveAsIntended, independently confirmed by A and reviewer from table01033150 ->00C19BAC. It is distinct from behavior/mood key DriveOffCharger. Builder received adopted exact row; no guessed name used.
- Trial will finish with two justified implementation jobs if CM13-02 verifies. PROPOSALS.md records pending unresolved-prose reconciliation and retained boundaries; no record is proposed for whole-path settlement.

## CM13-02 focused/negative evidence

- Builder initial focused8/8 PASS, including received PathFollowingEvent through actual DriveStraight ActionList and queued behavior callback. Host admission/scheduler substitutions are explicitly unit-boundary-labelled; no private callback reflection/manual result injection claims production completion.
- Old callback-only negative control FAIL1/PASS1 as expected: successful completion lacks objective and fails, non-success suppression control passes; corrected bytes restored exactly. Final broader result/reviewer run pending.
- New shared engine producer uses broadcast byte==1 and native external-interface gate, raw0x8B/u32LE delivery, app enqueue then local emit, diagnostic then DAS regardless gate. Only drive-off call site wired. Explicit M15-059 recipient and existing mood gap remain.

## CM13-02 integration and final gate

- Independent native/diff verification PASS, report20261026-cm13-02-codex-check.md. Reviewer personally checked RC3, callback, serializer, rvalue delivery and exact DroveAsIntended string; independently ran final objective class8/8 PASS, zero skipped380ms.
- Builder final broader66/66 PASS, zero skipped39s; initial8/8, expected old-callback1FAIL/1PASS behavioral control with corrected bytes restored; logkey3 EXACT/0ABSENT, diffcheck PASS. Detailed commands/reach in builder report.
- Manager committed verified CM13-02 on builder branch3d2021a8, fast-forwarded trial branch. No main merge/push. This removes the objective sender omission, with current host action-admission test boundary/mood/native recipients retained as gaps; no complete native behavior-manager or hardware claim.
- Two implementation jobs completed; third deliberately deferred. Both independent reviewers found no remaining blocking defect in their bounded diffs. No source changes remain unreviewed.
- Preparing single required combined final scripts/test-full.sh run on clean committed trial HEAD (repository instruction runs after code commit, records fingerprint). Counts/statuses and accepted subsystems will be compared to frozen baseline after actual outcome. No final full-suite result claimed yet.

## Ending manifest audit (gate still running)

- Explicit UTF-8 JSON comparison confirms ENTIRE fidelity_manifest.json identical to baseline16173227; git diff also empty. A preliminary diagnostic comparison used the Windows default decoder and falsely differed on historical Unicode text; corrected the comparator, not the manifest. Regenerated frozen snapshots using UTF-8 and obtained no content diffs.
- Counts remain EXACT239/EQUIVALENT6/POLICY68/HARDWARE14/BLOCKED2/IMPLEMENTATION358/RECOVERABLE23=710; accepted subsystems remain M1/M2/M3/M4/M5 only. Unchanged official counts are not used to deny the two bounded repairs or to imply unapproved settlements.
- Final gate started02e83626; source/code records were clean at start, test log .scratch/test-full.log. Handoff draft records pending result honestly; no new source changes made during gate.

## Full gate attempt1 failed / bounded correction

- scripts/test-full.sh on02e83626 exit1: fidelity710 PASS; testhost crashed after5009 passed/0 failed/0 skipped (partial count, NOT full pass). Fatal disposed Pose3d in native planner worker; blame names new CM13-01 dispatch fixture. Exact stack/trace in gate-run-1.md. No passed fingerprint accepted.
- Builder and reviewer independently traced fixture reverse using-disposal: goal destroyed before m/runtime join while asynchronous worker still maps retained goal wrapper. Reviewer acknowledges initial cleanup race missed. Correct fixture ordering without sleeps or production catch, then affected-only verify and repeat full gate because test code changed.
- This correction does not fix/certify production native goal-copy/start-state ownership. M11-084/M13-033 retained reference boundary remains explicit high-priority follow-up; no exclusion or harmlessness claim. Trial remains not accepted.

## Teardown correction accepted / final gate attempt2

- Builder changed ONLY dispatch fixture declaration/lifetime order so drive -> m.Dispose/worker.Join -> goal -> rig cleanup holds even on assertion failure; no source behavior changed, no sleeps or catches. Manager accepted independent affected-diff review PASS. Reviewer appended acknowledgment that original review missed fixture race, superseding earlier no-race statement for teardown; production ownership uncertainty retained.
- Builder focused25/25 PASS;20 separate testhost dispatch/cleanup repetitions20/20 PASS, zero skipped/aborts. Reviewer independent dispatch1/1 PASS348ms. Detailed current-record quotes and commands in updated CM13-01 reports. Test-only logkey0keys/0ABSENT; diffcheck PASS.
- Manager committed correctionb2891e6c and fast-forwarded trial branch. Original first-gate failure preserved in gate-run-1.md and local .scratch/test-full-attempt-1.log. No failure concealed or counted as acceptance.
- Required full suite will repeat because test code changed after abort; this is a justified correction/reverify cycle, not a repeated passing suite. No third implementation job added.

## Final complete gate PASS / measured outcome

- Required repeat scripts/test-full.sh on368172018791ea74d1a9fe29ebf067cf85401d5a completed exit0: fidelity710 PASS; full6267 passed,0 failed,0 skipped, total6267, duration5m17s. Repository script recorded passed content fingerprint. Exact gate outcome in gate-run-2.md; prior abort remains gate-run-1.md. No subsequent source/test/tool/record change.
- Actual progress: native map-query/stored-polygon planner source join repaired and independently code-verified, with real bounded dispatch regression; drive-off objective sender omission repaired and independently code-verified, with received-completion integration exercised under explicit host admission/scheduler unit boundary. These are two concrete defect repairs, not two settled records.
- Reuse/uncertainty: rejected duplicate tree geometry/growth/merge, seed/gate, SevereNeeds/manager lifetime, Markerless charger and prune/equality investigations. Recovered objective emitter from older checked RC3 instead of rereading a broad subsystem. New uncertainty clarified in ComputeOriginPose state input and retained planner goal/start ownership; no shortcut built.
- Review/rework honestly retained: original reviewer missed new fixture cleanup race; full gate caught it. Goal-before-runtime declaration fixes only test borrowed lifetime. Independent affected re-review and20 host repetitions passed; native production copy ownership stays open. One source-contradicted existing target exclusion oracle corrected with independent agreement.
- Ending manifest counts identical to starting baseline: EXACT239, EQUIVALENT6, POLICY68, HARDWARE14, BLOCKED2, IMPLEMENTATION358, RECOVERABLE23=710. Entire JSON unchanged; accepted M1-M5 only. Proposed reconciliation separate, none approved/applied. No fidelity approval, Claude-file edit, main merge/push or physical hardware action.
- Trial assessment: duplicate manager setup cost one operator clarification; thereafter autonomous one-builder delegation, evidence reuse and independent checks reduced orchestration/duplicate reading. It did not eliminate rework or make Codex checks acceptance. Next trial should designate one owning chat/worktree, preflight assets/index/local testhost and explicitly inspect asynchronous fixture lifetime. Recommend another bounded batch after Claude reviews current commits, prioritizing production planner ownership/origin recovery; operator decides hardware follow-up.
- Publication pending below; full handoff at re-analysis/jobs/trial-m13/HANDOFF.md. All workers stopped at their completed/reviewed tasks; no queued background work remains.

## Publication and closeout

- Normal git push origin codex-m13-manager succeeded, publishing reviewed/tested package through b5a49a32. Hook remained enabled; no --no-verify. Only trial branch advanced, not main.
- Final post-documentation fidelity check PASS710 and diff check clean after EOF formatting cleanup; no source/test/tool/manifest/Claude-file difference from the tested content. Original checkout remains clean at2dd741c70ff95c428b9779d7d4e3de69e891862c.
- This closeout entry is documentation only; source/test content remains exactly the complete passing gate36817201. Trial ready for external Claude review, not subsystem acceptance. No further agent work or robot commands scheduled.

## CM13-03 / second trial baseline (2026-10-26)

- Operator released exactly CM13-03, with this chat as the only manager. Pulled clean main from 2dd741c7 to ee01b4e9 (fast-forward); created clean isolated worktree .scratch/worktrees/codex-m13-manager-2 and branch codex-m13-manager-2 from ee01b4e9. No main push. Previous trial worktrees preserved.
- Official manifest baseline: 710 records = EXACT_SOURCE239, EQUIVALENT_IMPLEMENTATION6, COMPATIBILITY_POLICY68, HARDWARE_ONLY14, BLOCKED_EXTERNAL2, IMPLEMENTATION_GAP358, RECOVERABLE_GAP23. Accepted subsystems remain M1-M5. All official records/statuses/approvals frozen for this job.
- Prior CM13-01/02 external Claude PASS is on current main (1f7567ea); latest drive-off hardware remains run3 on a7af18cd, before this job. No hardware actions authorized or performed here. CM13-03 concerns goal/start ownership only, not whole-path acceptance.
- Phase 1 delegated to research_reconciliation: full four-question native ownership extraction, required ownership-read report only; no implementation. acceptance_blockers is the different independent instruction checker. Existing native tool configuration does not expose actual selected worker model/effort; requested prior Sol Medium cannot be verified and is not claimed as observed. No additional manager chats or background builder work launched.
- Gate: no production or test code changes before explicit independent Phase1 PASS. Earlier ownership research is a lead; every new native address receives known.py preflight. Required full test gate runs once on committed branch after independent implementation review, as CM13-03 specifies.
- Environment: existing binary, decomp navigation index and shipped OBB are linked locally into the new worktree. Initial default-sandbox junction creation was denied; authorized escalation succeeded. No assets committed/uploaded. No tests executed yet.
- Phase1 extractor report is ready: re-analysis/research/20261026-cm13-03-ownership-read.md. Independent reviewer is rereading original instructions; no verdict yet and no code/tests changed. Manager accepted reviewer's proposed runtime-only build boundary: own actual arriving inputs at native capture point; upstream action/PathComponent copies and drive-center/root conversion remain explicit gaps, not falsely certified by cloning.
- Baseline fidelity validation: explicit Python313 re-analysis/tools/fidelity.py --check, exit0, 710 records, status counts unchanged. Manifest SHA256 D820E76AB26FAAF0D32148E03D87024C6441D823E382968A6EB3E7A15F7D54E3; native evidence SHA256 02263C07F6BB60F4D7F351A3667DCA84FD3E6C0FC6F838E18B5DE7CF4E2989E1. This is static validation, not a test-suite or fidelity acceptance claim.
- Phase1 independent PASS received before ANY source/test edit: re-analysis/research/20261026-cm13-03-read-codex-check.md. All four questions checked against original instructions by acceptance_blockers (different from extractor). Extractor corrected only T1 Stop guard addresses; behavior unchanged. Manager accepts PASS for runtime-only capture/ownership contract, not upstream geometry or subsystem acceptance.
- Phase2 released to a different builder: own all full-goal poses with shared parents, retain raw original-ID context separately, synchronously capture numeric selected start, worker/replan temporary validation preserving IDs, copied-yaw retrieval, serialized replacement and release only after Join. Upstream SetGoal/Path conversion remains visibly MISSING; no speculative conversion added. New threaded tests must join before disposing any inputs/barriers/rig on all assertion paths. Full script once after independent code review on committed branch.
- Builder correctly raised one checked-read MISSING before adopting capacity-sensitive reuse: vector replacement allocation sizing. Extractor supplemented T3 with instructions00501BF0/1CF6..FC (old capacity zeroed),00501C20/24/28 (incoming count selected),00501D12..24 (12*N allocation and capacity). Different reviewer independently confirmed and appended supplemental PASS. Manager released exact-capacity sizing only after this PASS; no new behavior guessed or scope expanded. Upstream geometry remains deferred.
