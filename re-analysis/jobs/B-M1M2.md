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

## M1-029 unblocked (manager, 2026-10-05)

**The rows:** item 6 of `research/20260930-bcore-extractions.md`, with the corrections in
`research/20261005-B-M1M2-rows-check.md` ("Item 6"). A correction wins over the original row. The manager re-read the
three corrected rows in the binary:
- the INT32_MAX intValue cutoff (`0x008E1E50..0x008E1E5A`);
- the object close accepted when the last decoded key is empty (`0x008E0FA6..0x008E0FBA`);
- the whitespace mask 0x00800013 relative to byte 9, which is exactly tab, newline, carriage return and space
  (`0x008E166E..0x008E168C`).

**What to build:** M1-029 as an exact port of the shipped jsoncpp reader for the firmware JSON. It replaces
System.Text.Json on that path. The exception destination stays UNVERIFIABLE: record it as MISSING; don't invent it.

## Manager disposition of the MISSING triage (2026-10-09)

This decides `research/20261006-M1M2-missing-triage.md` row by row. These are decisions, not new rows. Codex applies
them as described under "To do" below.

1. **HIGHER-LAYER: accepted.** Each one leaves its M1/M2 record and is owned by the named higher-layer record. The
   M1/M2 record's `unresolved` keeps only a pointer: "transferred to <id>: <row>".
   - **Shared residuals:** S1–S12 and S14.
   - **M1-024:** D1, D3.
   - **M1-025:** E1–E5, E8, E9.
   - **M1-041:** N1, N2.
   - **M2-002:** P1, P3, P4.

   Where the triage proposes a NEW higher-layer record, it is created now as RECOVERABLE_GAP in that layer, with the
   triage's citation. The proposed new records are: the M15 repair-mutation and NaN contracts; the M8 SDK reset dispatch
   and the M7/M15/M3/M4/M11 SDK reset recipients; M15's SDK telemetry; M4's track-lock diagnostic; M3's NV idle
   scheduling; M5's ready-to-stream lifecycle; and the M8 ActionList/IAction lifecycle. Where an existing record
   already covers it, that record is amended instead (M11-053, the M8 queue records). It is built when its layer comes
   up.
2. **PHONE-RUNTIME: accepted as EQUIVALENT_IMPLEMENTATION assumptions,** written into the record's `unresolved`. None
   of this code ships.
   - **D2:** the initial FPSCR state is round-to-nearest, with default NaN and flush-to-zero as the phone's ARM process
     sets them.
   - **J2:** formatting through the phone's `snprintf`.
   - **J4:** allocation availability.
   - **J5, allocation failure, also goes here.** Its branch runs only when the phone's allocator fails, which is
     phone-runtime state. The stack doesn't simulate allocator failure.
3. **UNREACHABLE: accepted** with the triage's evidence, and no record: J3 and J6.
4. **D4 and J8, the escaping exceptions:** decided by the operator's approved departure **M1-034** (catch, log,
   continue). Nothing more to extract; the record points to M1-034.
5. **M1-EXTRACT: what remains of M1's work.**
   - **New M1 records,** RECOVERABLE_GAP, with the triage's citations:
     - S13: external-interface disconnect/report delivery;
     - E6: SDK mode entry and the lifecycle state writers;
     - E7: the SDK connection callback targets;
     - P2: outgoing RobotState publication.
   - **M1-029 keeps** J1 (the real-number `asString` dispatch and its formatting wrapper, from the checked rows F1–F8) and
     J7 (extreme lengths within the reachable bound).
   - **M1-046 keeps** Q1–Q3.

**After this, these records have no open MISSING left in M1:**
- M1-015, M1-024, M1-025, M1-031, M1-041, M1-044, M1-045 and M2-002 go to the Opus pass;
- M1-029, M1-046 and the four new M1 records are M1's remaining extraction and build work.

**To do (Codex):**
1. Apply items 1–5 as one inventory correction with citations. Create the new records, amend the existing ones and
   rewrite the `unresolved` texts.
2. Run `fidelity.py --approve` for every subsystem touched, under the operator's standing authorisation for cited
   corrections. Then `--check`, commit and push.
3. Then extract and build the M1-EXTRACT items under CODEX-BUILDER: S13, E6, E7, P2, M1-029's J1/J7 and M1-046's Q1–Q3.
   Extraction rows go to the manager first.

## Policy disposition (manager and operator, 2026-10-09)

From `research/20261005-M1M2-policy-check.md`:
- **M1-013 stays** COMPATIBILITY_POLICY: the Windows timer primitive.
- **M1-034, 036, 038, 040 and M2-017** are operator-approved departures (AGENTS.md).
- **M1-014, 022, 037, 039 and 042: narrow them.** The OS or host primitive stays policy. The engine's own decisions
  around it become IMPLEMENTATION_GAP rows or records, built from the check's tables:
  - M1-014: H1–H3 and T3–T4; the tick cadence and dispatch order;
  - M1-022: UDPTransport's binding, port bookkeeping, retry/reopen, logging and drain, including U3;
  - M1-037: the app's reset-trigger gates N2–N3;
  - M1-039: the shipped receive handler;
  - M1-042: the Unity default producers, including the pool-enable gate.

  Do this in the same inventory correction as the MISSING disposition. Extraction rows that aren't checked yet go to
  the manager before anything is built from them.
