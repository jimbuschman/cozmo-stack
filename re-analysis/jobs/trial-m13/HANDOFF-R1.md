# R1 handoff (subscriber-race correction)

**READY FOR CLAUDE.** FIX independently code-checked; corrected full gate **6285/6285 PASS,0failed/0skipped,6m36s**, committed codee592d1b2a3a0663a55d08b4791040b70b9ebe897. Branch codex-m13-manager-2, base origin/main4892b66d. This supersedes the initial READY at12963731/code6d9ccafd: Claude correctly found the ordinary shared-list consumers unsafe. Original exception diagnostics remain unchanged; the fix protects those consumers. No main push, native record/status/approval/acceptance change, REVIEW.md edit or hardware run.

Every changed behavior claim is operator-authorized **host error handling; native address N/A**:

| Claim | Production evidence |
|---|---|
| Fault logs full aggregate/inner type, message and available stacks; cancellation logs the observed exception with an honest observation-stack label before unchanged fallback | FaceBehaviors.cs RunAction26..46 (original6d9ccafd, rebasedae850034); unchanged in this fix |
| Late stopped/replaced faults stay visible; callbacks retain Post/Update and stale guard | Same RunAction; no scheduler or behavior locking change |
| Every ManipTool.Say producer (Step, vision/world/manip/follower/message/main) now serializes console output and append through one shared journal | ManipTool.cs Run; DiagnosticJournal<T>.Write |
| Reactions.Line does the same for multiple behaviors, manager and sensor producers; strings versus timestamped-object shapes preserved | Reactions.cs Run; same DiagnosticJournal<T>.Write |
| Acceptance serialization receives a separately allocated array snapshot taken under the same journal lock, so later producers cannot mutate the serialized collection | DiagnosticJournal<T>.Snapshot; both WriteAcceptance call sites |

Actual tests: builder corrected10/10 zero skipped2s; different reviewer11/11 (diagnostic10+KnockOver) zero skipped4s. New four cases attach real RunAction worker fault/cancellation Step to the production shared journal with concurrent ordinary producers/snapshots, verify all2000 ordinary entries, full diagnostic details, output serialization and pre-tick/fallback order. Held diagnostic/readiness barriers and finally joins cover the overlap and fixture lifetime. Existing six cover success and late stop/replacement. Quiet lock-removal control failed4/4 before final barrier strengthening; exact failing assertions UNKNOWN (no TRX), not claimed as a specific oracle.

Independent read/build reports: research/20261026-r1-fix-{read,build}-codex-check.md. Exact commands/results in R1-builder.md. Prior full6281/6281 on original code is historical and does not certify this correction. Corrected scripts/test-full.sh on e592d1b2 passed fidelity710 and full6285/6285, zero skipped6m36s. Original log copied locally to .scratch/r1-fix-test-full.log; code fingerprint recorded by the repository script. This R1 checkpoint excludes paused CM13-04 production code.

Open: Step remains synchronous on the calling/completion thread; shared sink safety is fixed, no new event-thread guarantee. Historical KnockOver Abort cause remains UNKNOWN. No native behavior/status settlement. Claude should spot-check shared producer convergence, locked snapshots/unchanged JSON shape, and tests' actual worker completion path. No binary extraction is needed for this authorized host correction.
