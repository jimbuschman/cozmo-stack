# PUTDOWN-NOTHING-1 — Claude check (manager, 2026-10-29)

**Verdict: PASS (test-only). Nothing settled.** Merged 2b6aa7b5 (tests 47953f64).

Four tests drive the shipped PutDownBlockNothingToDo instance (ID 66, class 44) through the existing native
PutDownBlock path. No production code changed.

## Checked by Claude

- `behaviors/freeplay/putDownBlockNothingToDo.json`: only behaviorClass PutDownBlock and behaviorID
  PutDownBlockNothingToDo.
- `activities/freeplay/nothingToDo.json`: the Scoring chooser's entry 3 is this ID with flatScore 1.1.
- Scoring reload 0x00609FEE..0x0060A03E:
  1. ExtractBehaviorIDFromConfig.
  2. `["scoring"]`.
  3. BehaviorManager::FindBehaviorByID: the same manager-owned instance.
  4. ReadFromScoredJson(scoring, true).

  This matches the tests' real scored receiver.

The alias lock name, the common putdown flow and the RNG rest on Codex's checks; the common flow is M15-075's, already
EXACT_SOURCE and owning all four shipped IDs. Admission and competition (P4) stay unclaimed.
