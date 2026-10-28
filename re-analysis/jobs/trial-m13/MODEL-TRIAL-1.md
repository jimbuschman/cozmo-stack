# Superseded one-job builder model trial

Operator instruction2026-10-28: next job's builder starts with `gpt-6.1-terra` at medium effort; reader and independent reviewer use `gpt-6.1-sol` at high effort. No worker is switched during a job. STACK-BLOCKS-1 was already released and keeps its existing workers/configuration.

Status: **SUPERSEDED BEFORE STARTUP** by the latest operator instruction2026-10-28. No job or worker was started under this proposed trial. Current policy in AUTONOMY.md: reader/extractor explicitly `gpt-6.1-sol`/medium; builder and reviewer default model/effort, without overrides (operator reports default `gpt-6.1-terra`). Log the requested configuration for every worker. Earlier model assignments below are historical; audit/counting rules remain in use.

Record in the selected job's builder ledger and handoff:

- Builder start model and effort, startup call/result, and any limits on actual runtime telemetry. Reader/reviewer startup configuration is recorded too.
- Reviewer fix rounds: one round is a returned batch of required code/test corrections followed by builder resubmission. Include draft-review corrections; multiple findings in one returned batch are one round. List the batches/findings so the count is auditable. Self-test attempts and source-only clarification questions are recorded separately, not renamed reviewer rounds.
- Every builder-raised MISSING or blocking source/input question, its exact owning step, whether resolved or remaining, and the evidence or implementation that settled it. Include resolved items; distinguish inherited frontiers from newly raised items. If none were raised, say so explicitly.

The completed handoff reports the actual result of this one-job trial. It does not infer better source fidelity from a model label or passing tests, extend the trial automatically, change acceptance rules, or authorize a mid-job switch.
