# CM13-03 independent build check

**PASS for the bounded lattice-runtime ownership change; no remaining blocking findings.** Reviewed stable final production/test diff in the isolated build checkout, including the source-order correction below. Current M13-033 remains IMPLEMENTATION_GAP and M11-084 RECOVERABLE_GAP; no settlement, hardware verification or subsystem acceptance. Actual model/reasoning/tier are unobservable.

Evidence/record boundary: complete current record quotations and independent instruction checks are in `20261026-cm13-03-ownership-read.md` and `20261026-cm13-03-read-codex-check.md` (including checked capacity supplement). Reviewer independently reopened those primary instructions during Phase1, then compared this entire bounded diff against them. No decompiler-based claim or new source assumption introduced here.

## Source comparison

- Accepted ComputePath request captures all full poses only after existing mutex/pending/running gates. Independent Pose3d nodes preserve shared parent references. Common-prefix Assign, appended copy, self-vector skip, reverse shrink and reverse release match00501BC0/00501C40/70/92/00501CA8..BA. Logical capacity follows checked supplement: after exceed-capacity deallocation it becomes exactly incoming count; shrink retains capacity. Existing Pose3d copy/Assign helpers are reused, with no invented recursive parent clone.
- Native helper004FD296..3B2 stores full vector separately from initially valid **raw** `(original byte ID,StateC)` pairs. Code preserves rejected full-pose positions, stores unrounded values/original IDs, and replaces full vector/clears prior actions/context even for an accepted all-invalid request. Rejected pending/running requests do not replace admitted storage. Existing numeric tool APIs retain their default sequential-ID wrappers; runtime uses explicit original-ID entry points.
- Start snapshot004FEB82..AA and selected-start context store004FEED6/DA are scalar values before validation/notification. Unforced replan uses the retained-prefix endpoint when present and refreshes start without recopying goals. Worker now consumes persistent raw numeric context, preserving repeated temporary validation and original IDs (008586CE/00858792;008591CE..00859488). It no longer reads caller wrappers. GetCompletePath still reads original yaw from full copied vector indexed by original chosen ID004FFC7A..96.
- Stop retains ownership/pending semantics. Actual worker join precedes reverse owned-pose release, matching00501954 before005019D8/DC. No broad worker catch, early-release policy or scheduling shortcut added. Production mutation of owned runtime state remains under existing context mutex; worker consumes that same serialized state.
- Upstream NativePlannerSet still borrows inputs. Added diagnostic accurately names missing action copies/root/drive-center ownership/conversion. No plain clone or isolated-20mm geometry change is passed off as the complete upstream path. Those and generic parent-lifetime/VERIFY/pre-Init questions remain open.

**Finding corrected during review:** first diff cleared old plan/context before importing obstacles. Native helper imports at004FD2B0 after full-pose assignment and before clears004FD2B8..F0. Builder restored exact order: AssignGoalPoses -> import -> old actions/context clear -> raw validation. Independently re-read corrected lines; no equivalence assumption retained. Review thereafter limited to affected lines/tests.

## Tests, reach and lifecycle

Read every new fixture and all teardown paths. ActualDispatchCapturesGoalAndNumericStartBeforeCallerMutation enters actual DriveToPoseAction.Init/PathComponent/native runtime with admitted RobotStates, holds the real mutex to keep work pending, mutates caller goal/robot state and destroys action before worker consumption. It checks original admitted endpoint and copied final yaw. This is offline production runtime dispatch with host action admission and explicit upstream MISSING, not whole native action/geometry/hardware coverage.

Other fixtures are unit-labelled and explicitly substitute hard C-space/missing-map inputs or synchronous mode. They test original-ID1 after ID0 rejection, raw101.25 revalidation/RoundSafe to110, refreshed and prefix replan starts, pending/running rejection, node/parent sharing, assignment/append/shrink/self/growth, all-invalid replacement, Stop and actual join-before-release observation. These expectations come from the checked source contract, not the implementation alone.

Every runtime is joined before caller poses/parents/events/rig disposal, including failed assertions. The deterministic worker barrier is released in finally before implicit runtime disposal/join. Locks unwind before joins. No fixture disposes admitted inputs early; mutations establish independence safely. This explicitly checks the teardown class missed in CM13-01's original review.

Independent final-binary execution, after builder testhost stopped:

`dotnet test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~M13PlannerOwnershipTests|FullyQualifiedName~M13NativeMapImportTests.ContactCreatedChargerIsExcludedThroughRealPathComponentDispatch' --verbosity quiet -m:1 -nr:false -clp:ErrorsOnly`

**9 passed,0 failed,0 skipped,793ms** (new8 ownership/replan/lifecycle tests plus prior actual dispatch regression). Reviewer git diff --check PASS. Local testhost IPC required approved outside-sandbox execution; no robot commands.

Builder separately records old-runtime-only negative control: actual dispatch mutation fails behaviorally, expected endpoint20/actual50, no crash, corrected runtime restored byte-for-byte. Initial7/7, then8/8; broader100/100 before order fix; affected33/33 after fix; logkey27 EXACT/0ABSENT. Reviewer read complete builder report but did not repeat old-file substitution or broader filter. No full suite run here; manager runs once on committed branch after this PASS.

No remaining unsupported behavior within this bounded diff, circular oracle, introduced race/deadlock or misleading closure claim found. Remaining upstream conversion/ownership and other recorded planner gaps are explicit. No commit, manifest, inventory, status or production/test edits by reviewer; only this report written.
