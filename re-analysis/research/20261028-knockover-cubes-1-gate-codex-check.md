# KNOCKOVER-CUBES-1 — bounded full-gate oracle check

2026-10-28. Independent read-only checker, manager worktree `.scratch/worktrees/codex-m13-manager-2`, supplied candidate HEAD `48e7a7cf`. Actual model/effort telemetry unavailable. This answers the manager's delegated narrow check; it does not repeat the behavior extraction. No tests were launched: the manager's full gate remains the sole runner. No production/test/manifest/inventory/approval/Claude/REVIEW edits, git mutations, or hardware actions.

## Recommendation

PASS for the proposed narrow correction: add only `BehaviorClass.KnockOverCubes` to the explicit expected implemented-class array in `M7BatchThreeBTests.TheFactoryTableIsTheExtractionsTable` (line 269). Leave its 79-row extraction parser, count assertions, ordinal comparisons, class/engine-name comparisons, constructor/allocation/case comparisons and actual factory untouched.

The failing trailing assertion describes the stack's currently bound constructors, independently of the native 79-way table oracle. The candidate adds precisely one dictionary binding at `BehaviorContainer.cs:291`: `(ctx,json)=>new NativeKnockOverCubesBehavior(ctx,json)`. `Table` attaches that dictionary's delegate to each unchanged row through `Constructors.GetValueOrDefault(r.Class)`. Thus the expected finite set legitimately grows from eight to nine. Do not derive the expected set from `Constructors`, suppress this assertion, loosen it to Contains, or change the source table to obtain a pass.

## Checked path and evidence

Read PROJECT_STATE first, then research README, the KNOCKOVER-CUBES-1 read packet and codex-check, the candidate factory diff against `ecb33f52`, the complete relevant test body, native constructor and configured tests. No engine retrace was needed. `known.py 0x0059CCE4` was run successfully using the installed Python313 executable before any possible native follow-up; it resolves the containing `BehaviorContainer::CreateBehavior` at 0x0059C888. The default python shim was inaccessible; no source claim rests on that failed invocation.

The original extraction's literal row (`20261002-R-BEH2-M7-gap1-extraction.md:131`) is:

`0x1C KnockOverCubes case@0x59cce4 new=0x168 BehaviorKnockOverCubes ctor=0x5c2ea0 plt=0x4afc94`

The independent K1 packet (`20261028-knockover-cubes-1-read.md:37`) records native case 0x0059CCE4, allocation 0x168, constructor call at 0x0059CCF4 to 0x005C2EA0 with actual Robot and the same Json, followed by shared ownership at 0x0059CCFE. This report reuses that checked packet rather than presenting repo prose as a new primary disassembly read. The current literal row in `BehaviorContainer.cs:230` agrees on class, engine class, ctor, size and case. Its row is unchanged in the inspected candidate diff. The test continues to compare every one of those fields for all 79 extracted entries, plus ordinal and count.

The actual binding supplies the real `BehaviorFactoryContext` and JsonElement; the new constructor consumes `source.Manipulation`, `source.NativeRobot`, configured ID, minimum height, streamline, needs and animation triggers. Factory input guards reject missing manipulation/native Robot; its shared completion owner and NativeBaseInputs are bound before AddToFactory and Created. Knock-only common inputs are then connected to the canonical manager. This is the configured native owner, not the retained public manual async class. `KnockOverNativeTests.ConfiguredFactoryUsesCanonicalStackProgressionAndHardSpark` obtains that actual configured owner from `Runtime.Bound["KnockOverCubes"]`, and exercises progression, stack and hard-spark admission. The prior independent report's frozen 1,637-test pass is historical evidence only; this checker did not execute it.

## Current owning record, quoted before reliance

Read directly from the candidate's current fidelity_manifest.json:

- ID: `M7-018`.
- Title: `Shipped behaviour configuration and BehaviorClass factory binding`.
- Status: `IMPLEMENTATION_GAP`.
- Authority: `shipped OBB configuration plus libcozmoEngine.so BehaviorContainer`.
- Complete evidence array:
  - `178-config table in M7 inventory Appendix C`
  - `79-class entry-point table in Appendix B`
  - `BehaviorContainer::CreateBehavior 0x0059c888`
  - `class 0x39 BehaviorWait case constructs base IBehavior and installs the BehaviorWait vtable`

The current unresolved field retains broad factory/container obligations and historical constructor counts. This finite expected-set correction does not settle M7-018, alter evidence, or certify all classes. M13-014/028 and M12-035 retained public-path limitations remain exactly as described by the existing KNOCKOVER independent report; no new whole-record claim is made here.

## Regression scope and limits

After the active full gate releases the host, inspect the proposed one-line test diff and rerun the exact failing test (or its M7BatchThreeBTests class), then follow the manager's required final gate policy. Existing configured KnockOverNativeTests are the behavior-binding regression; no new production fix or broad source audit is indicated by this failure. Preserve the failed full-gate artifact and distinguish its stale expected-class failure from the earlier unresolved manual async KnockOver fault. A passing rerun cannot explain that separate fault.

Classification: stale harness expectation after a source-backed configured binding, not a native table mismatch. Recommendation is bounded to that expectation; post-edit verification and execution are pending. No fidelity status or hardware acceptance follows.

## Released one-line correction: independent diff and regression

After explicit manager release, inspected only the new worker diff at `.scratch/worktrees/codex-knockover-cubes-1`. It changes exactly two adjacent lines in M7BatchThreeBTests.cs: the existing comment gains the checked KnockOver case0059CCE4/ctor005C2EA0, and the explicit expected class array gains only BehaviorClass.KnockOverCubes. All 79 extraction comparisons and actual-value query are unchanged; no assertion weakening or production change is present in this bounded diff.

Raw SHA256 before execution and after server shutdown both equal the builder's frozen hash:

`1AE1D683D8CD4DFC0280653A1FD1C5BCEE532617990ED74E34A27E1D5F03D9A2`

Independently executed, without build/restore, exactly the released two-class scope:

`dotnet test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-build --no-restore --nologo -v:q --filter 'FullyQualifiedName~M7BatchThreeBTests|FullyQualifiedName~KnockOverNativeTests' --logger 'trx;LogFileName=reviewer-gate.trx' --results-directory .scratch/knockover-gate`

Authorized elevated local IPC run: exit 0; **62 executed, 62 passed, zero failed/skipped**, reported duration 15 seconds. Independently parsed TRX: 36 M7BatchThreeBTests and 26 KnockOverNativeTests; all error/timeout/aborted/inconclusive/notRunnable/notExecuted/disconnected/pending counters zero and no RunInfo entries. The exact formerly failing test is included by the full class filter. The builder's earlier restricted empty run remains a non-result; this pass does not relabel it.

Worker scratch artifacts: `reviewer-gate.log`, `.exit`, `.trx`, and `reviewer-serverstop.log/.exit` in `.scratch/knockover-gate/`. Test exit and server-stop exit both 0. MSBuild and VB/C# compiler servers reported successful shutdown; final active testhost count 0. The runner is released. No full suite or hardware run by this checker.

**Final bounded verdict: PASS.** The exact approved expected-class correction preserves the native source oracle and the configured binding regression passes independently on unchanged reviewed bytes. No official record/status/approval change, no general source-fidelity/full-suite claim, and no resolution of the separate historical manual async fault follows. Only this research report and authorized scratch verification artifacts were written.
