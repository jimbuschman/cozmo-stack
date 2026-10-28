# KNOCKOVER-CUBES-1 gate expectation builder (2026-10-28)

Requested model: Sol / Medium. Runtime model telemetry unavailable; no independently verified model claim.

Worker: `.scratch/worktrees/codex-knockover-cubes-1`, branch `codex/knockover-cubes-1`, starting HEAD `741cf8bf` (manager-provided). Read `PROJECT_STATE.md` first. Manager released this edit only after independent source check PASS in manager `re-analysis/research/20261028-knockover-cubes-1-gate-codex-check.md`. No new extraction performed by builder.

## Cause and exact change

Manager full gate: 6512 passed / 1 failed / 0 skipped, 6513 total. Sole failure was the stale trailing implemented-class expected set in `M7BatchThreeBTests.TheFactoryTableIsTheExtractionsTable`: eight old owners omitted the now-built native KnockOverCubes binding. All 79 native metadata comparisons passed. Independent read supports factory case 0x0059CCE4 / constructor 0x005C2EA0.

Only code change: add `BehaviorClass.KnockOverCubes` to that explicit expected set; extend its source comment with those checked addresses. Preserve the 79-row and 79-table cardinalities and every class, ordinal, engine name, constructor, allocation-size and case-address comparison. No production code, manifest, approval, inventory, Claude, project-state or main-queue edits. No assertion weakened. Existing untracked `HANDOFF-KNOCKOVER-CUBES-1.md` preserved and unstaged.

## Attempts and exact results

Every test attempt used the existing actual ignored OBB/native/marker assets in this worker; `COZMO_TESTS_WITHOUT_ASSETS` was absent (checked explicitly before each launch).

Command (both attempts; second uses distinct log filename):

```text
dotnet test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-restore --nologo --filter 'FullyQualifiedName~M7BatchThreeBTests|FullyQualifiedName~KnockOverNativeTests' --logger 'trx;LogFileName=builder-gate-retry.trx' --results-directory .scratch/knockover-gate
```

| Attempt | Actual result |
| --- | --- |
| 1, restricted execution, `builder-gate.log` | No output after roughly 90 seconds; no TRX/test result. Read-only restricted CIM process inspection denied access. Interrupted own tool session with Ctrl+C; session exit 1, not a test failure. Its empty output log is preserved. Elevated build-server shutdown then succeeded; elevated process inspection found no remaining dotnet process before retry. |
| 2, elevated execution, `builder-gate-retry.log/.trx/.exit` | Exit 0; 62 passed / 0 failed / 0 skipped, 62 total, test duration 16 s. Full M7BatchThreeBTests and full KnockOverNativeTests selected; native configured run included. TRX counters: total/executed/passed 62, all failed/error/timeout/aborted/notExecuted counters 0. Existing analyzer warnings only. |
| Final shutdown, `builder-shutdown-final.log` | Exit 0; MSBuild and VB/C# compiler servers reported successful shutdown. Immediate inspection showed only the exiting compiler process 8968 with blank command line; no runner or testhost. |
| `git diff --check` | Exit 0. |

Raw artifacts are under worker `.scratch/knockover-gate/`. No full suite run by builder. No commit, push or hardware acceptance.

## Frozen deliverable

`cozmo-stack/tests/Cozmo.Protocol.Tests/M7BatchThreeBTests.cs` raw SHA256:

```text
1AE1D683D8CD4DFC0280653A1FD1C5BCEE532617990ED74E34A27E1D5F03D9A2
```

Code/test edit frozen after this scoped pass. Independent checker and manager integration/full gate remain required. No fidelity classification or whole-behaviour/hardware acceptance claim; earlier unrelated public/manual async uncertainty remains as recorded in the original builder report.