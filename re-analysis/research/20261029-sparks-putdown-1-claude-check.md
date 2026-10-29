# SPARKS-PUTDOWN-1 — Claude check (manager, 2026-10-29)

**Verdict: PASS (test-only). Nothing settled.** Merged 28f93f0b (tests 3ba3524e/5a1e3876).

Four tests drive the shipped SparksPutDownBlock instance (ID 154, class 44) through the existing native PutDownBlock
path (M15-075, already EXACT_SOURCE). No production code changed.

## Checked by Claude

- `sparksPutDownBlock.json`: class PutDownBlock and ID SparksPutDownBlock only. There are no needs, display or scoring
  keys, so the instance keeps the ctor defaults: score 0 and an empty display.
- SparksPutDownBlock is referenced from 8 shipped activity files, matching Codex's eight Spark StrictPriority callers.
  None of them has a scoring entry.

The enum and factory values, the StrictPriority resolution and the Spark bridge rest on Codex's independent checks.
Spark admission and timeouts stay open. The M8-012 typed transition report stays IMPLEMENTATION_GAP.
