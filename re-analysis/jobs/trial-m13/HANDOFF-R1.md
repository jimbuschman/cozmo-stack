# R1 handoff

**READY FOR CLAUDE.** Code6d9ccafd381c9075fb2a4b9752ecbd4cc1e3258b on codex-m13-manager-2, basea4bbc533. Independent read/build PASS; full gate6281 passed,0 failed,0 skipped,6m49s on that committed code. Asset-complete worktree (local shipped AudioAssets.zip141488171bytes); fidelity710 PASS. No main push, record/status/approval/acceptance or REVIEW.md change, no hardware run.

All changed diagnostic claims (operator-authorized host error handling; native addresses **N/A**, as requested in REVIEW.md/R1):

| Claim | C# location/evidence |
|---|---|
| Faulted action logs action identity and full aggregate/inner exception type, message and available original stacks before selecting the supplied fallback | FaceBehaviors.cs ActionBehavior.RunAction:26..27; interpolation of Task.Exception invokes AggregateException.ToString |
| Cancelled action observes its already-completed task, logs the real cancellation exception, and labels an observation stack whose original cancellation site may be unavailable | FaceBehaviors.cs:28..35; narrow GetAwaiter().GetResult/OperationCanceledException catch |
| Late stopped/replaced action failures remain diagnostic; Step may run on completion thread via existing locked Log/Trace | FaceBehaviors.cs:26..36; SteppedBehavior.Log existing implementation; explicitly checked host-debug mechanism |

Successful result, fallback value, existing Post/Update tick delivery and `_cancel != cts` callback suppression are unchanged (FaceBehaviors.cs:37..46). No scheduler, callback/ownership refactor or broad listener catch. Only production file FaceBehaviors.cs changed (+10lines); new tests ActionBehaviorDiagnosticTests.cs exercise real RunAction/Task.Run/continuation/Post/Update with deterministic controlled tasks, not just a formatter.

Verification:
- Focused6/6; old-production control2 expected diagnostic failures/1 success pass; corrected bytes restored. Final affected16/16 includes prior KnockOver and objective flows. Reviewer independently7/7 (new6+KnockOver),0skipped. Task inputs completed/awaited in finally; Step recorder is ConcurrentQueue.
- `sh scripts/test-full.sh` on6d9ccafd: **6281/6281 PASS**,0skipped; fidelity710. Log retained locally .scratch/r1-test-full.log. No code/test/tool/frozen-record change after gate. Exact commands/results in R1-builder.md and independent reports20261026-r1-{read,build}-codex-check.md.
- Existing unchanged FaceBehaviors engine-key checker ABSENT/PART findings are documented in builder report; no new engine keys introduced or unrelated native cleanup performed.

Open: prior KnockOver Abort root cause remains UNKNOWN; these diagnostics preserve future fault/cancellation evidence, not a claimed diagnosis. No native behavior/status settlement.

Claude questions: Is the full fault/cancellation information visible on the existing debug channel before Abort, while action callback order/suppression stays intact? Is the explicit completion-thread Step diagnostic and labelled cancellation observation sufficiently clear? No binary extraction is required for this authorized host diagnostic.
