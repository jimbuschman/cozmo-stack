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

## Rows checked (manager, 2026-10-05): the three blockers are unblocked

`research/20261005-B-M1M2-blockers-extraction.md` is adopted as checked rows. The PARTIAL items in its coverage table
(downstream recursive effects, the FP environment, nested SDK handlers) stay UNKNOWN: build none of them; record them
as MISSING.

The manager re-read in the binary:
- **D8–D16**, the decay walk (0x0069C468..0x0069C54C):
  - the inclusive `bge` threshold;
  - minutes = elapsed / 0x42700000 before any rate;
  - f32 rate × multiplier;
  - the `bls` rate gate;
  - the current floor's crossing time;
  - `vmul` then `vsub` within a band;
  - the min-only `it mi` clamp.
- **E3–E5**, ExitSdkMode (0x00661546..0x00661566): the edu byte gate, `NeedsManager SetPaused(false)`, then the status
  exit, then the tail call.
- **E11–E12**, the movement unlock (0x00640540..0x0064056E): `robot+0x338` gate, owner `+0xD0`, mask byte `+0xD5`.
- **T4–T5**, the teardown order (0x0052F2DC..0x0052F2F6): NeedsManager, PerfMetric, DAS(0), then the destructor gated
  on a non-null Robot.

**Codex may now build:**
- **M1-024:** rows D1–D18.
- **M1-025:** rows E1–E14.
- **M1-015, M1-031, M1-044 and M1-045, by the proposed split (rows T1–T9 and the ownership table).**
  - The M1 parts are built.
  - Each higher-layer obligation becomes a new record in its own layer, with its citation. That inventory correction
    is approved with `fidelity.py --approve` under the operator's standing authorisation for cited corrections.
