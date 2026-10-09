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

## Manager-checked extraction rows (2026-10-09)

Before building the remaining M1 records, the manager reopened the call targets in the shipped native library and official APK sources. The checked rows and boundaries are recorded in `inventory/M1-transport.md` and `inventory/M7-behaviour.md`:

- **M1-029 J1/J7:** real `asString` dispatch/formatting wrapper and reachable-length JSON decoding. J1's 36-byte formatter buffer, `.0` condition, comma rewrite and nonfinite branch are source-backed; the imported formatter is phone runtime. J7 uses a u16 length; no fixed token bound was found in the reader/converter slices. Complete maximum transport delivery remains owned by transport. See `fidelity_manifest.json` M1-029 evidence.
- **M1-046 Q1-Q3:** constructor/destructor member order and shared-handle last-release slots were checked. Concrete unsubscribe target and callback descendants remain UNKNOWN; build only the confirmed retirement sequence.
- **M1-050 S13:** RemoveRobot calls external slot +0x30 with robot id; the shipped base slot is pure virtual and no runtime override was found. Delivery target/payload remain UNKNOWN.
- **M1-051 E6:** local SDK state transitions in EnterMode, ExitMode, OnDisconnect and OnConnectionSuccess were reopened. The +0xE1 writer census and concrete callback descendants remain UNKNOWN.
- **M1-052 E7:** selector and virtual +0x24 callback invocation were reopened; concrete callback target/body remain UNKNOWN, so there is no generic callback implementation to build.
- **M1-053 P2:** UpdateAllRobots update/state gate/projection and +0x1C publication were checked. The second getter caller belongs to M7-022; publication sink and exhaustive caller list remain UNKNOWN.
- **Policy rows:** M1-047 owns engine executor/priority choices while host scheduling stays policy; M1-048 owns UDPTransport decisions while host socket calls/errno mapping stay policy; M1-049 owns Android bind/unbind gates and JNI signal bridge while M1-023 owns the native subscriber/fd gate/reset-flag setter; M1-042 owns Unity producers and the profile gate while engine volume conversion remains M1-026.

The primary citations and detailed row mapping are preserved in the two inventories. No implementation has been started from an unchecked extraction row.

## The app-boundary decision (operator, 2026-10-09): M1's last items

From AGENTS.md, "Scope: the app boundary":
- **M1-042 and M1-049** are now COMPATIBILITY_POLICY: the app's inputs are supplied by the stack.
- **M1-051 and M1-052** are COMPATIBILITY_POLICY: SDK mode isn't supported.
- **M1-050 and M1-053** are IMPLEMENTATION_GAP, buildable now. Deliver the disconnect report and the RobotState
  publication as C# API events, with the engine side exact from the checked rows.
- **M1-046 and M1-047** are the remaining extractions. M1-046 needs the shared-handle unsubscribe target. M1-047 needs
  the constructor's thread-priority value at its call site; give it as an integer constant with its address. Rows go
  to the manager.

After those, M1 goes to the final Opus pass. Prepare the verification packet first (CODEX-BUILDER rule 9).

## Rows checked (manager, 2026-10-09): M1-046, M1-047, M1-053

**M1-046 (rows U1–U8) and M1-047 (rows P1–P4)** in `research/20261009-M1-final-extraction.md` are adopted. The manager
re-read in the binary:
- the priority 3 at `0x008367C0`;
- the reverse-order handle release (`0x004EAE94..0x004EAEBA`: end −= 8, then release);
- the vtable slots `0x0101FE28` → `0x0051D8F9` (unsubscribe, Thumb `0x0051D8F8`) and `0x0101FE24` → `0x0051D8D5`.

**M1-053 (rows P2a–P2m)** in `research/20261009-M1-053-projection-rows.md` are adopted, with this ownership rule.
- **M1-053 owns the publication:**
  - the HasReceivedRobotState gate and the slot +0x1C call (P2a);
  - the 109-byte field layout;
  - the projection arithmetic: the pose struct and angle conversions (P2c, P2d), the lift height with its trailing
    +0.0 (P2f);
  - the status bits (P2h);
  - the carried and top IDs (P2i);
  - gameStatus (P2j).
- **Each supplied field is read from the stack component that owns it.** That supplier's exactness belongs to its
  layer:
  - **the root pose** (P2b): the stack's localization (M11, under M11-053/055);
  - **the head-tracking object** (P2k): `MovementComponent+0x1C`/`+0x20`, which the constructor initialises to −1
    (`0x0063DA86`; manager-checked). With no tracking action it is −1, as in the engine. Its writers belong to the
    M4/M13 tracking records;
  - **the last processed image timestamp** (P2m): `VisionComponent+0xE8`. Read the stack's vision component; the
    supplier's exactness is M11's. Name it in an M11 record if none covers it.

  Record each supplier boundary in M1-053's `unresolved` as "supplier owned by <record>".
- **The manager re-read P2h** (`0x005181BA..0x005181E8`): start from +0x350; animation tag ≠ 0 adds 0x40; tag 0xFF
  gives original | 0x840; carried ≠ −1 adds 0x2.

**Codex:** build M1-046's retirement (U1–U8), M1-047's priority request (P1–P4; host permission stays M1-014 policy)
and M1-053's publication. Then update the M1 verification packet and stop.
