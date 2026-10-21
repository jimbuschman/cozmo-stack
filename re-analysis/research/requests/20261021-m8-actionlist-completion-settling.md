# Research request R-M8-ACTIONLIST

- **Date:** 2026-10-21; operator-requested foundation pass.
- **Kind:** independent extraction and read-only code comparison; settlement recommendation only.
- **Answer file:** `re-analysis/research/20261021-m8-actionlist-completion-settling.md`.
- **Primary records:** M8-015, M8-017, M8-020, M8-021, M8-024.
- **Context:** `PROJECT_STATE.md`, `AGENTS.md`, `inventory/M8-framework.md`, current manifest,
  `acceptance/M10-M15-blockers.md`. This is the action foundation, not all of M8.

## Questions

1. Trace queue/admission, engine tick, cancellation, completion cache/payload, synchronous local
   delivery and C# API delivery through the current ActionList/ActionQueue/IAction production path.
   Compare ordering, result values and side effects with M8-015's instruction evidence.
   Identify concrete action completion-union ownership; a generic cached payload is not proof
   that all supported concrete completions are recovered.
2. Compare compound sequential/parallel result, retry, ignoreFailure and teardown paths (017)
   and PlayAnimation/TriggerAnimation Init, event handlers, completion and destruction (020/021).
   Reuse accepted M5 streamer evidence. Check the sleep trigger factory's missing production
   writer at native call 0x0052CEDE; do not invent a producer or reopen the whole streamer.
3. Reconcile M8-024's existing callback-survivor census with current Cancel(-1), Clear and Robot
   teardown. Start at ~Robot 0x00511120..0x00511156 and existing checked subscribers. Check the
   M5 handoff: timer removal 0x004EC6B4 before RemoveRobots and later action-destructor clock reads.
   Enumerate only remaining relevant callback gaps; do not repeat the entire behavior census.
4. For each claimed step, distinguish built source-supported behavior, missing wiring/payload,
   contradicting behavior and unknown source. Inspect existing regressions for real queue/tick/
   inbound-message reach and circular expectations; propose precise missing tests without writing them.
5. Recommend settlement only where the complete claim holds; otherwise propose small jobs.
   Keep BehaviorManager switching/IBehavior lifecycle, concrete activity bodies and M12/M13
   movement algorithms as named boundary dependencies, not additions to this pass.

## Already known — search before tracing

Grep `research/` and `inventory/` first. Start with `research/20261004-actionlist-extraction.md`,
`research/overnight/20261014-ov18-m8-015-check.md`,
`research/20261012-m8-play-animation-actions-check.md`,
`research/20261019-m5-settling-b.md`, and the later B-M8/B-M1/B-M2M15 build/check notes
named in the current records. For callback ordering, reuse OV111 and the M2-022 answer.
Use `decomp/libcozmoEngine/index.tsv` to locate code; primary claims cite binary instructions.

## Output and limits

Record examined HEAD; quote each primary record's current title, status and evidence.
First table: record/step | native behavior and instruction citation | production file/line and
caller | test/reach | supported, contradicted, unbuilt or UNKNOWN. Then blockers, per-record
recommendations, test designs and explicit handoffs. Apply AGENTS.md's observable-behavior
standard; internal ownership mechanisms need equivalence of effects, not imitation of C++.
Log-only cleanup does not block settlement unless it hides a real failure.

Read-only: no code, tests, manifest/inventory edits, acceptance changes or hardware scripts/runs.
Write only the named answer; commit/push it under the queued-research rule, then continue.
Do not wait for the charger or pose/history report, its adoption or any implementation.
