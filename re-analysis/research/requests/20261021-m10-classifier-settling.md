# Research request R-M10-CLASSIFIER

- **Date:** 2026-10-21; operator-requested foundation pass.
- **Kind:** independent extraction and read-only code comparison; settlement recommendation only.
- **Answer file:** `re-analysis/research/20261021-m10-classifier-settling.md`.
- **Primary records:** M10-001, M10-002, M10-009, M10-010, M10-011, M10-013,
  M10-014, M10-015, M10-017.
- **Context:** `PROJECT_STATE.md`, `AGENTS.md`, `inventory/M10-derived.md`, current manifest,
  `acceptance/M10-M15-blockers.md`. Scope is classifier/sensor foundations, not all reactions.

## Questions

1. Compare received RobotState admission and actual live calls into off-treads classification,
   unexpected-movement detection and gyro drift/bias. Check input provenance, raw versus smoothed
   IMU, float operation order, gates, thresholds, counters, debounce, resets and lifetime.
   Start with CheckAndUpdateTreadsState 0x00511E00..0x005122B0 and the current records'
   citations for the detector bodies; do not use a standalone helper test as wiring evidence.
2. Trace classifier consequences in order: committed state, action cancellation, falling/needs
   hooks, charger-platform changes, pause flags, local events and API reports. Confirm each join
   against current code. Follow downstream M11/M12/M15/M8 owners only to identify whether the
   required recipient exists; hand off missing recipient bodies instead of absorbing those layers.
3. Reconcile the physical flag and pre-first-state IMU policy (010/013), never-raised obstacle
   flag (009), filter/drift lifetime (014) and raw magnitude (017). For 015, check whether later
   history and formatter builds close its older admission/log gaps. For 002, stop at the
   detector output; the full rewind/obstacle response M10-007 is a separate work item.
4. Inspect existing regression reach and source oracles. List precise missing boundaries
   (including threshold equality, debounce timing, first-state/reset and API ordering where
   applicable) and recommend tests without writing or running a hardware test.
5. Give a current per-record settlement recommendation or bounded remaining job. Do not repeat
   old “no ActionList/history” claims without checking current production wiring. Unread or
   unbuilt dependencies stay explicit and prevent overclaiming a whole production path.

## Already known — search before tracing

Grep `research/` and `inventory/` by record/address first. Reuse the M10 rows in
`research/20260930-pre-extraction-M5-M10-M15.md`, the existing R-FIX2 checks,
`research/20261004-m10-strategies-extraction.md` for boundary context only, OV23/OV24
and their adopted checks, and B-M10-1/B-M11-10/B-M1-5/B-M3M4-33/34 notes named in
the current manifest. Locate engine bodies through `decomp/libcozmoEngine/index.tsv`;
cite actual instructions and resolved constants, not decompiler output.

## Output and limits

Record examined HEAD; quote each primary record's current title, status and evidence.
First table: record/step | native behavior and instruction citation | production file/line and
caller | existing test/reach | supported, contradicted, unbuilt or UNKNOWN.
Then blockers, settlement recommendations, regression designs and cross-record handoffs.
Separate observable behavior/readiness/downstream-state defects from log-only cleanup and
permitted internal differences. No guessed numeric policy or audio ADP-1 exemption here.

Read-only: no code, tests, manifest/inventory edits, acceptance changes or hardware scripts/runs.
No strategy-factory, Hiccup, resume or concrete reaction state-machine extraction (003/004/008/016).
Write only the named answer; commit/push it under the queued-research rule. This pass does
not depend on the preceding research answers being adopted or implemented.
