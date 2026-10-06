# Codex standing queue 3 (from 2026-10-06; the operator is away for a few days)

- **Requested by:** manager (Claude)
- **Use:** work through the tasks in order. Pull before each task, and skip a task whose answer file is already on
  origin.

## Rules (they matter more than speed)

1. **Depth over speed.** The last long queue was rushed and had to be redone. Each answer opens with a coverage table:
   every item in scope marked CHECKED, PARTIAL or NOT DONE. A task isn't finished while anything is NOT DONE, so don't
   start the next one.
2. **The layer boundary.** The current layers are M3+M4, then M5 (PROJECT_STATE, "The plan"). A step that belongs to
   another layer is listed as HIGHER-LAYER with its citation, not extracted. Don't follow destructors or callers into
   upper layers.
3. **Research tasks** (all except Q3) follow `re-analysis/research/README.md`, with one exception: you may commit and
   push your own answer files, under `re-analysis/research/` only, one commit per task. Keep binaries, downloaded
   dependencies and third-party source out of git (`.gitignore`).
4. **Q3 is a build job,** under `jobs/CODEX-BUILDER.md`.
5. **Defect reviews** (Q4, Q5, Q6) report DEFECTs and circular tests only, never HOLDS. Each defect gives the engine
   behaviour with its address and the C# file:line.
6. **Floats** are given as bit patterns. Write UNKNOWN wherever the binary doesn't settle something; never guess.
7. **Quote a record's current manifest text** before contradicting it.

## The queue

**Q1. M3+M4 build rows.**
- Scope: every M3-device and M4-control record that your B-M3M4 log (`jobs/status/B-M3M4.md`) lists as NEEDS
  EXTRACTION, or as blocked on missing checked rows.
- Give rows: step, address, behaviour, gates, order, failure results, floats as bits.
- Answer: `20261006-M3M4-rows-extraction.md`

**Q2. M5 build rows.**
- Scope: every M5-animation record that isn't EXACT_SOURCE.
- Start from your rushed `20260930-pre-extraction-M5-M10-M15.md` (the M5 part only). Re-check each row in the binary
  and complete it.
- Answer: `20261006-M5-rows.md`

**Q3. Build B-ADP-HARNESS** (`jobs/B-ADP-HARNESS.md`).
- This is test tooling and settles no record.
- Report the measured numbers; don't choose thresholds.

**Q4. Defect review of DeepSeek's B-ACTIONS and B-FACE.**
- Scope: the commits matching `git log --grep "B-ACTIONS\|B-FACE"`, against the rows they cite
  (`20261004-actionlist-extraction.md`, `20261004-procedural-live-extraction.md`) and the binary.
- Answer: `20261006-B-ACTIONS-B-FACE-review.md`

**Q5. Defect review of Sonnet's B-M6b-4 builds.**
- Scope: the commits matching `git log --grep "B-M6b-4"` since 2026-10-02, against corrections C30 onward in
  `inventory/M6-wwise-bank.md` and the binary.
- Mark each finding by ADP-1: a DEFECT in a decision is always reported. Per-sample DSP arithmetic is not a defect
  under ADP-1; note it as "ADP-1 equivalent candidate".
- Answer: `20261006-B-M6b-4-review.md`

**Q6. Defect review of R-FIX, R-FIX2 and R-FIX3.**
- Scope: R-FIX and R-FIX2 on main, and R-FIX3 on the branches `r-fix3-rounds` and `r-fix3-wip`, if they're on origin.
- List R-FIX3's conflicts with main (AnimationScheduler.cs, TrackLayers.cs, Activities.cs, the manifest), for the
  merge later.
- Answer: `20261006-R-FIX-review-2.md`

**Q7. Circular tests in M3, M4 and M5.**
- Every assertion whose expected value comes from the implementation rather than from the binary or an asset.
- Give the file:line and the engine's value.
- Answer: `20261006-circular-tests-M3-M5.md`

**Q8. M3, M4 and M5 tests that miss the live entry.**
- The records whose tests don't go through the live production entry (CHECKLIST section 1).
- Name the entry point that should be used.
- Answer: `20261006-test-entry-M3-M5.md`

**Q9. The float-literal baseline, M3 to M5.**
- Scope: the entries of `cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/float_literal_baseline.txt` that sit in M3,
  M4 or M5 code.
- For each: the engine's bits and width, and whether the C# literal produces them.
- Answer: `20261006-float-baseline-M3-M5.md`

When Q9 is done, stop and write a one-paragraph summary at the end of the Q9 answer.
