# Research request R-M11-POSE-HISTORY

- **Date:** 2026-10-21; operator-requested foundation pass.
- **Kind:** independent extraction and read-only code comparison; settlement recommendation only.
- **Answer file:** `re-analysis/research/20261021-m11-pose-history-settling.md`.
- **Primary records:** M11-051, M11-052, M11-065, M11-066, M11-067, M11-068, M11-097.
- **Context:** `PROJECT_STATE.md`, `AGENTS.md`, `inventory/M11-vision.md`, current manifest,
  `acceptance/M10-M15-blockers.md`. Read the current records, including later build notes.

## Questions

1. Compare the seven records' complete claimed pose/history paths with current production code:
   arithmetic and copy/parent semantics; raw-state admission; interpolation; ComputeStateAt and
   GetLastStateWithFrameID; callers' flags, timestamps, result gates and state/output consequences.
   Start from the inventory's instruction citations and the actual RobotState/image entry points.
2. Reconcile B-M3M4-33/34's history and SetNextImage joins with older MISSING notes. Which rows
   are implemented and source-supported, which remain unbuilt, and which claims are contradicted?
   Do not infer completion from the accepted M3/M4 layer or an isolated helper test.
3. Check the actual consumer boundaries: cliff pose, unexpected movement, vision-frame pose and
   face actions. Confirm the history input/result join; list downstream-owned gaps without
   extracting their entire algorithms. M10's classifier/response and M13's charger creation
   are separate jobs, not prerequisites for publishing this report.
4. Inspect existing regression oracles and reach. Identify circular expectations, missing
   production wiring and specific boundary cases needing tests; do not write tests.
5. Recommend per-record settlement or a small corrective/recovery job, with precise remaining
   obligations. For M11-053/064/073/076/077/084 dependencies, inspect only what these paths need;
   no full delocalization, world-model or pose-owner census.

## Already known — search before tracing

Grep `research/` and `inventory/` for each record and unresolved address first. Starting reports:
`research/20261013-pose3d-arithmetic.md`, `research/20261013-remaining-pose-bodies-check.md`,
`research/20261015-b-pose-5-pass.md`, `research/overnight/20261018-ov128-check.md`,
`research/20261019-b-m3m4-33-check.md`, `research/20261019-b-m3m4-34-check.md`.
Use later checked corrections over earlier drafts; name the superseded claim explicitly.
Locate engine bodies via `decomp/libcozmoEngine/index.tsv`; cite instructions, not decompilation.

## Output and limits

Record examined HEAD and quote each primary record's current title, status and evidence.
First table: record/step | native behavior and instruction citation | current production file/line
and caller | existing test/reach | supported, contradicted, unbuilt or UNKNOWN.
Then give blockers, settlement recommendations, regression designs and cross-record handoffs.
Separate wire/robot/API/readiness/downstream-state defects from best-effort logging/internal
mechanism differences under AGENTS.md. A whole-record recommendation covers its whole claim;
otherwise list the remaining steps. Unknown behavior stays unknown.

Read-only: no code, tests, manifest/inventory edits, acceptance changes or hardware scripts/runs.
Write only the named answer; commit/push that answer under the queued-research rule, then continue
to the next open research request. No dependency on another pending answer being adopted.
