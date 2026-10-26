# Claude handoff — autonomous M13 trial, 2026-10-26

Branch: `codex-m13-manager`. Baseline: `1617322790b02cbd0457f719de55345663c8f21e`. Test teardown correction: `b2891e6c`. Main remains untouched. Two bounded implementation jobs; third build deliberately deferred. No physical robot commands, official status changes, approvals, Claude-check edits or subsystem acceptance.

## Actual changes and evidence

| Job / commit | Behavior repaired | Primary evidence | Independent review / offline reach |
| --- | --- | --- | --- |
| CM13-01 `9d52255b` | Planner reads current map type3 stored polygons instead of all located-object footprints; charger and height-rejected cube no longer enter through the stand-in; unsupported carried/target ID filtering removed. | M13-003/M11-074; map query00681674,0068A5F8,006884FC; consumer004FDF18..8C; predicate0050241C..28; adopted OV42 + operator first-found order; gate M13-030 unchanged. | `research/20261026-cm13-01-codex-check.md` bounded PASS; builder98/98, reviewer10+6. Real received-state charger -> PathComponent/native lattice dispatch, with synthetic prior planner snapshot explicitly labelled. Unit cube fixtures traverse actual admission/height gate; no camera-observation claim. |
| CM13-02 `3d2021a8` | Successful drive-off emits objective4 to existing raw C# API/local hub before existing mood attempt; flag/interface gates and diagnostic order preserved. | M13-017/M7-019 RC3;005BE228..308/3A8..3D6; callback005C0E9C..EE2; union007285FE; Pack0071BEAE; UI rvalue006625B0. E2G0x8B, four LE bytes04 00 00 00. Objective name DroveAsIntended at01033150 ->00C19BAC. | `research/20261026-cm13-02-codex-check.md` bounded PASS; builder66/66, reviewer8/8. Received PathFollowingEvent -> real ActionList -> posted callback, but host admission/deterministic scheduling substitutions honestly unit-labelled. No complete native manager/queue claim. |

Major production files: `Manipulation/LatticePlanner.cs`, `Vision/MemoryMap.cs`, `Vision/SourceQuadTree.cs`, `Behavior/ChargerBehaviors.cs`, new `BehaviorObjectiveReport.cs`. New source-driven regressions: `M13NativeMapImportTests.cs`, `BehaviorObjectiveReportTests.cs`; one source-contradicted target-exclusion assertion in `NavigationTests.cs` corrected while success/arrival checks retained.

Old-code controls: CM13-01 prior world import yields3 expected behavioral failures plus1 passing normal-height control; CM13-02 prior callback yields1 expected missing-report failure plus1 passing non-success control. Corrected source restored byte-for-byte. These show tests detect the defects, not that tests alone establish fidelity.

## Final gate

First `scripts/test-full.sh` on committed `02e83626` ABORTED after5009 partial passes because the new asynchronous dispatch fixture disposed its borrowed goal before planner join. This escaped initial focused/reviewer runs. Fixture teardown correctionb2891e6c passed affected re-review, builder25/25 plus20 separate host repetitions and reviewer1/1. A repeat final gate is pending. See gate-run-1.md. No aborted/pending result is represented as a full pass.

## Remaining blockers and uncertainty

- Production planner goal-copy/start-state ownership remains an explicit M11-084/M13-033 boundary. Fixture lifetime ordering does not validate early-disposal API behavior or repair native ownership. Prioritize a narrow source/production-ownership follow-up before whole planner acceptance.
- M13-003 RobotQuad pose/origin operation remains incomplete. Narrow inspection found ComputeOriginPose00517F6C uses Robot+284/component+8 and state-dependent offset before008CAE4C. Existing pose/footprint helpers do not establish equivalence. Next recovery should resolve only this tail, offset and live input binding, then get independent check before code.
- M13-017/M7-019 whole callback/behavior fidelity remains open: existing unattached mood and native host-action admission M8 gap remain visible. M15-059 native objective recipients are unbuilt; local publication is preserved, not invented away.
- M12-049 configuration-container producer and retained-pose ownership M11-073/084 remain genuine boundaries. No blanket no-effect/exclusion decisions.
- M13-040 contact-created charger is exercised, but no whole shared owner/lifetime acceptance or new hardware verification is claimed.
- Run3 six MATCH checks were on a7af18cd; B-M11-22 and this trial are later. That straight drive does not verify the lattice type3 reader.

## Questions for Claude

1. Check both code commits and the native bindings/order cited above, including physical-child postorder/reference dedup, absence of reader-side ID filters and retained unsigned timestamp gate.
2. Check objective byte gates, raw payload, app enqueue/local emit ordering, diagnostics and successful-only call site. Confirm test reach labels and retained native recipient/mood/host-admission boundaries are adequate.
3. Review `PROPOSALS.md`: proposed unresolved-prose reconciliation only; no whole-record status promotion proposed. Current M11-073/084 RECOVERABLE_GAP overrides historical reviewer status wording. No approved evidence was rewritten.
4. Decide the next narrow recovery/build priority (ComputeOriginPose or configuration/action ownership) after review; operator hardware checks remain required for acceptance.

## Trial evaluation and recommendation

The trial reused checked geometry/growth/merge/payload/seed/gate evidence, rejected obsolete work and repaired two concrete source-fidelity defects without broad audits or filling unknowns. One source-contradicted test expectation was caught and corrected with independent agreement. Initial independent review missed an asynchronous fixture teardown race; the full gate caught it before publication, requiring a correction and re-review. A third build was rejected because existing helpers did not establish the native origin operation.

Initial duplicate manager chats caused a coordination interruption requiring operator clarification. After explicit ownership resumed here, work stayed with one manager, one active builder and independent reviewers. Next trial should name one owning chat/worktree and preflight local assets/index/testhost before delegation. Actual worker model/effort telemetry was unavailable; requested Sol medium is recorded, not asserted as measured.

Recommendation: continue with another bounded Codex-managed batch after Claude reviews these commits and the operator decides the relevant hardware follow-up. Do not call the acceptance target or M13 complete.


