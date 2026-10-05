# Job B-M1M2: finish M1 and M2

- **Agent:** Codex, as builder (trial: `jobs/CODEX-BUILDER.md`). Its rules apply.
- **Type:** build.
- **Rules:** `CHECKLIST.md` applies, and **this job never settles a record** (section 6). The first job under the
  one-layer-at-a-time plan (PROJECT_STATE, "The plan").

## Scope: only these records

1. **Fix the defects** written in each record's `unresolved`. Each one starts with an Opus verification and cites
   addresses:
   - M1-015, M1-024, M1-025, M1-029, M1-041;
   - M2-002.

   **M1-029** needs an exact port of the shipped jsoncpp reader. Use the rows in
   `research/20260930-bcore-extractions.md` item 6, after `cozmo-verifier` has checked them.
2. **The cross-layer split** (PROJECT_STATE, "The cross-layer rule"), for M1-031, M1-044 and M1-045.
   - **Finish the M1 part:** the idle timeout's trigger and timing (M1-031); RemoveRobot's own steps up to the
     component destruction calls (M1-044).
   - **Leave the higher-layer part:** the destroyed components' effects and the go-to-sleep action tree go to new
     records in their own layers.
   - **The split itself** is an inventory correction with citations from `research/20260930-bcore-extractions.md`
     item 7. Approve it with `fidelity.py --approve M1-transport`; the operator authorised cited corrections.
3. **Nothing else.** Leave the policy and hardware records untouched; the manager handles them.

## Gates

Before each commit:
- `cozmo-verifier` gives PASS on the diff;
- `fidelity.py --check` passes;
- the full suite passes.

Push each batch. Log it in `status/B-M1M2.md`. Finish with `DONE` or `BLOCKED <reason>`.

## Trial scope (2026-10-05)

Codex extracted the rows for M1-029 (the jsoncpp reader) and for the M1-031/044/045 split itself, in `research/20260930-bcore-extractions.md`, and they are unchecked. Under CODEX-BUILDER rule 2, those two items wait for the manager's check after the Claude reset.

This trial builds item 1 without M1-029: M1-015, M1-024, M1-025, M1-041 and M2-002, whose rows come from the Opus verifications.
