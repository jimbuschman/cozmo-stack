# CM13-03 handoff (2026-10-26)

**Bounded runtime ownership fix: independent code PASS. Full gate: FAIL (6274 passed, 1 failed, 0 skipped). Review branch only; do not merge as a passing batch.**

Branch `codex-m13-manager-2`, from pulled main ee01b4e9. Phase1 read/check committed06c60764 before any code; narrow allocation supplement independently checked/committedb688b0fd. Reviewed implementation commit **b1c1169d4a8291b9f00b5d007a86dcc7912e12aa** (isolated builder original d2c73434). No main push, official-record/status/inventory/approval/acceptance changes or hardware runs.

What changed:
- Runtime captures independent full goal poses with shared parents at accepted request admission. Separate persistent raw numeric goals retain their original byte IDs; start/prefix state is captured before worker notification. Worker/replan no longer rereads caller wrappers. Final desired yaw comes from the retained copied pose.
- Vector replacement/assignment/shrink and join-before-release follow the checked native lifecycle. Native import-before-old-context-clear order was corrected during independent review.
- Files: NativeLatticePlannerRuntime.cs and LatticePlanner.cs; NativePlannerSet.cs adds an accurate MISSING for upstream borrowing/conversion. New M13PlannerOwnershipTests.cs exercises actual runtime dispatch plus explicitly labelled edge-case units.

Evidence and checks:
- [Ownership read](../../research/20261026-cm13-03-ownership-read.md), including current mandatory record quotations; [independent Phase1 PASS](../../research/20261026-cm13-03-read-codex-check.md); [independent build PASS](../../research/20261026-cm13-03-build-codex-check.md).
- Primary anchors: all-goal copy004FD296..2A4; raw original-ID context004FD348..3B2; scalar start004FEB82..AA/004FEED6..DA; worker005007E0..854 and numeric revalidation008586CE/00858792; copied yaw004FFC7A..96; join00501954 before goal destruction005019D8/DC. Allocation-sizing supplement00501BC0..1D28 is independently checked.
- Builder focused7/7,8/8,100/100; after source-order correction33/33. Reviewer independently9/9. Old-runtime-only production-dispatch negative control failed expected endpoint20/actual50 (200mm admitted vs500mm moved), safely without a host crash. All new threaded fixtures join before input/parent/event/rig cleanup, including failed assertions.
- Full `sh scripts/test-full.sh` ran **once**, on committed b1c1169d after independent review: fidelity710 PASS; full6274 passed/1 failed/0 skipped,6m48s. No passing fingerprint recorded. [Gate result](CM13-03-gate.md), [bounded investigation](CM13-03-gate-investigation.md), exact focused commands/results in [builder report](CM13-03-builder.md).

Remaining blocker and limits:
- Full gate failed `NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates`: missing Flip compound-success trace, outer Abort. This test does not construct the lattice runtime. Focused current and unchanged-main ee01b4e9 executions both genuinely completed the cube/flip path and passed1/1; neither explains the broad-suite failure. Outer Abort can mask an async fault/cancellation. Cause **UNKNOWN**; no direct ownership regression or proven baseline flake established. No unrelated source/oracle fix and no full-suite rerun.
- Upstream action/NativePlannerSet/PathComponent borrowing, root/drive-center conversion, generic parent lifetimes/VERIFYs and other recorded planner gaps remain MISSING/open. Offline runtime dispatch is exercised; complete native admission, public-API early-disposal safety and hardware behavior are not certified.
- M13-033 stays IMPLEMENTATION_GAP; M11-084 stays RECOVERABLE_GAP. All710 records unchanged. Proposed evidence/unresolved reconciliation only is separate in PROPOSALS.md; no status change proposed. One manager, one builder, separate extractor/reviewer; actual worker model/effort could not be observed.

Questions for the Claude check (on the other machine):
1. Do the checked raw-context/original-ID, copied-yaw, capacity/reuse and join/release implementation boundaries match the instructions, with no bridge assumption in runtime admission/replan?
2. How should the single broad-suite KnockOver Abort be captured/diagnosed before any merge or full-gate retry? Inspect both gate reports; do not infer its cause from isolated passes.
3. After that blocker is resolved, should the next separately authorized job build the upstream action/PathComponent owned conversion path? The read establishes it, but this batch deliberately leaves its MISSING visible.
