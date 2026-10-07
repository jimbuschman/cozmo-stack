# B-ADP-HARNESS

CLAIMED Codex 2026-10-06, queue 3 Q3. Test tooling only; no fidelity record settlement.

Status: BLOCKED after comparison-tooling batch. Native renderer and matching live C# chunk/timeline capture adapter are MISSING; zero engine/C# pairs measured. Continue queue Q4 as the operator authorized.

Batch 1: `re-analysis/tools/emu/adp_compare.py`, its six hand-stream tests and usage contract. Structure validates the identical input case, 22320Hz mono s16le, exact744-byte chunks, positions/counts and zero/nonzero regions. Offset/alignment and silence mismatch stop before metrics. Per-case and aggregate metrics report RMS/SNR/max error; no thresholds chosen and no record settles. Research report: `re-analysis/research/20261006-adp-baseline.md`.

Self-review against CHECKLIST: no parallel live renderer, no guessed reference stage, no silence recutting or time alignment; independent expected unit-test values; exact behavior-changing stages remain required. `fidelity.py --check` passes (445 records). Comparator tests6/6. Full suite3946/3946, zero failures/skips, 2026-10-06 (`.scratch/B-ADP-HARNESS-suite.log`). Batch commit is the commit containing this log entry; publication hash follows.

MISSING: bank-load/live graph delivery, per-voice/source/filter/FX recipients, bus-to-Hijack routing/rate/chunk binding, common scheduled event/state/RTPC/timing native renderer, live C# capture adapter, shipped event selection for each corpus category. Existing component oracles replace decision/control callees, so their outputs are not an ADP-1 reference stream. Missing checked rows are identified by address and inventory/job citation in the report. Singing waits for M9 as allowed. No hardware run or asset upload.
