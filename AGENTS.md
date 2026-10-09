This project is reverse-engineering the official Anki Cozmo app/engine into a C# control stack.

The goal is source-faithful app-side behavior, not merely behavior that appears to work.

**Start every session by reading `PROJECT_STATE.md`.** It says which layer is in progress, what has been approved and what is open. Work follows the Process section at the end of this file.

## Exact, always

The goal is exact reproduction (operator, 2026-09-27). Whatever ships in the APK or the OBB is reproduced exactly, however much extraction it takes. EQUIVALENT_IMPLEMENTATION is only for behaviour whose code doesn't ship at all, such as the phone's system libraries. Never ask the operator whether "equivalent" would do.

### Audio DSP policy ADP-1 (operator, 2026-10-05): the one exception

In the audio layers (M6, M9), **per-sample DSP arithmetic** may be EQUIVALENT_IMPLEMENTATION, although its code ships
in the Wwise runtime inside `libcozmoEngine.so`. Nothing else may.

**Still exact:**
- bank parsing and the object graph;
- event and action order;
- the RNG, random/sequence choice, shuffle, avoid-repeat and weights;
- switches, states and RTPC values;
- voice limits, ducking decisions and routing;
- the plug-in and FX selection, parameters, enable/bypass state and slot order;
- start, stop, seek and pause, and all timing and scheduling;
- WEM decoding (Vorbis and ADPCM);
- MIDI, note and singing decisions;
- the robot output stream's framing: 22320 Hz, 744-byte chunks, chunk boundaries, start and end positions.

**May be equivalent, and nothing more:**
- the floating-point arithmetic of mixing, gain application, filters (LPF/HPF/biquad), EQ, compressor and limiter
  bodies, and the resampler;
- NEON lane order;
- the phone's libm rounding.

**Rules:**
1. **A record that mixes both** states its boundary in `unresolved`: which steps are exact, and which per-sample
   arithmetic is EQUIVALENT_IMPLEMENTATION under ADP-1. It is never reclassified in bulk. It moves only when its
   equivalent part passes the equivalence test.
2. **The equivalence test** uses the engine's own audio code run under `re-analysis/tools/emu/` as the reference, over
   a corpus that exercises each DSP path.
   - Structure is checked first, pass/fail: frame counts, chunk boundaries, start and end sample positions,
     silent/non-silent regions, no timing offset.
   - Only then is PCM error measured: RMS/SNR and maximum absolute error.
   - The thresholds are set from measurement on that corpus, written down, and then enforced.
3. **External tools** (wwiser, vgmstream, PyCozmo) are authority 6: cross-checks only. They run locally; the shipped
   banks are not uploaded anywhere.

### Scope: the app boundary (operator, 2026-10-09)

The stack reproduces the **engine** (`libcozmoEngine.so` and the assets it reads) and replaces the **app** around it:
the Unity screens and flows, user profiles, the Android Java layer and the app's SDK mode. In practice:
- **Engine-to-game messages and reports** (the external interface) are delivered to the stack's C# API. The engine's
  side (when, what, the order, the payload) stays exact.
- **Where the engine needs an input the app supplied** (stored volume, a profile, Android network callbacks), the stack
  supplies it from its own settings or host events. Each one is recorded as a COMPATIBILITY_POLICY naming the input.
- **SDK mode is not supported.** Its engine paths are unreachable and are recorded as such.

App-layer code is still evidence of what the engine receives, and stays authority 2. It isn't reproduced unless the
stack implements that feature.

### Other approved departures

- **M1-034, handler isolation** (operator, 2026-10-07): a handler exception is caught, logged and survived; the original aborts.
- **M1-040, firmware check** (operator, 2026-10-09): every robot firmware is accepted and logged; the original rejects a version that differs from the app's. Without it the operator's robot (2457) is refused.
- **M1-036, crash reporting** (operator, 2026-10-09): no Breakpad crash reporter.
- **M1-038, after a disconnect sub-message** (operator, 2026-10-09): stop processing the frame; the original continues through freed memory.
- **M2-017, failed field reads** (operator, 2026-10-09): 0/false; the original leaves uninitialised stack contents.

Apply no other departure without the operator.

## Primary rule

NEVER fill an unknown behavior with a plausible implementation and then treat it as recovered.

If primary source does not establish a behavior, say so and classify the gap appropriately.

Hardware success does not upgrade provenance.

Passing tests do not prove source fidelity.

## Source authority order

For any behavioral question, inspect in this order:

1. libcozmoEngine.so native code / disassembly / call sites
2. Unity/native application code
3. shipped assets, configs, banks and schemas
4. CLAD/generated serializers
5. original official-app captures
6. verified repo notes

Do not substitute a reasonable design before exhausting the available primary source.

## Fidelity taxonomy

Use the existing manifest classifications:

- EXACT_SOURCE
- EQUIVALENT_IMPLEMENTATION
- RECOVERABLE_GAP
- IMPLEMENTATION_GAP
- COMPATIBILITY_POLICY
- BLOCKED_EXTERNAL
- HARDWARE_ONLY

Do not silently reclassify an item to make a gate pass.

`EXACT_SOURCE` must mean the behavior-changing production path being claimed is actually supported by source evidence. Evidence for one function inside a larger unverified path is not evidence for the whole path.

## Required workflow for every hardware failure

Before editing code:

1. Trace the complete failing production path.
2. Identify the provenance of every behavior-changing step.
3. Check the relevant fidelity-manifest entries.
4. Check whether the hardware test references the correct fidelity records.
5. Determine whether the failure is:
   - implementation bug,
   - source-recovery gap,
   - hardware-only unknown,
   - blocked external behavior,
   - test/harness bug.
6. Only then propose a correction.

If a necessary behavior remains recoverable from available primary source, investigate it.

If it cannot be established from available source, STOP that item rather than inventing an answer.

## Tests and manifests

Any hardware acceptance test that names a fidelity record must reference a real record.

Add/maintain automated validation that:
- every referenced fidelity ID exists;
- live production-path claims are not marked more strongly than their evidence supports;
- blocked/hardware-only uncertainty relevant to a hardware test is visible in that test.

## Current warning

Previous work repeatedly made this mistake:

source-backed pieces were individually correct, but an unverified assumption was inserted between them and the overall subsystem was called complete.

Actively look for this failure mode.

Examples already suspected from hardware acceptance:
- cube discovery works, but the outbound cube-connection initiation path may never have been recovered;
- live-animation keyframes are source-backed, but the complete live-animation wire lifecycle may not have been reproduced;
- color camera format was explicitly HARDWARE_ONLY;
- singing still contains BLOCKED_EXTERNAL Wwise-runtime semantics even though the streaming implementation is working.

## Scope discipline

Work on one bounded failure at a time.

Do not launch another repository-wide audit unless concrete evidence requires it.

Do not modernize unrelated code.

Do not begin hardware acceptance yourself.

When an item is finished, report:
- complete production path traced;
- primary evidence;
- gap or root cause;
- exact change;
- fidelity-manifest impact;
- regression test;
- commit;
- remaining uncertainty.

Then stop.

## Process

Adopted 2026-09-23. It exists because recovery and implementation were being done in one step, and the gaps between recovered facts got filled with guesses.

### Roles

- **Operator (the user).** Sets direction and approves at the checkpoints below. Runs hardware scripts, since only the operator's machine reaches a robot. Does not relay messages between agents.
- **Manager (the main Claude Code session).** Owns `PROJECT_STATE.md` and the backlog. Scopes each task, hands it to a worker, checks the result against the evidence, and accepts or rejects it. Reports to the operator only at checkpoints or when blocked.
- **Workers** (`.claude/agents/`):
  - `cozmo-extractor` (read-only): traces the original's production path and returns an inventory with a citation or UNKNOWN for every behaviour-changing step.
  - `cozmo-implementer`: changes code only from an approved inventory, and stops with `MISSING:` on anything the inventory does not settle.
  - `cozmo-verifier` (read-only): tries to find unsupported, contradicted or omitted behaviour and circular tests in the implementer's diff.
- **The same roles in opencode** are `.opencode/agent/cozmo-manager.md` (the primary agent) and `cozmo-extractor`, `cozmo-implementer` and `cozmo-verifier` (subagents). Scratch output goes to `.scratch/`, which is gitignored.
- **Parallel jobs (2026-09-27):** several opencode windows, each in its own clone, run chains of jobs from `re-analysis/jobs/BOARD.md` unattended: extraction (X), integration (I) and build (B). Each job stays within its write scope; read `re-analysis/jobs/README.md`. The push gate (`git config core.hooksPath scripts/hooks`) refuses a push to main unless the fidelity check and the full suite pass.
- **Research (Codex / ChatGPT)** works only in `re-analysis/research/`, under the rules in `re-analysis/research/README.md`: no branches, no worktrees, no commits. Background research is authority 6, and a Codex extraction is checked like any extractor report.

No role does both extraction and implementation in the same task.

### Per layer

Layers go bottom-up, one at a time: M1 transport, M2 protocol, then device, control, animation, behaviours and higher layers as `PROJECT_STATE.md` orders them.

1. **Inventory.** The extractor traces the layer. The manager writes `re-analysis/inventory/<subsystem>.md`, which names every record of the subsystem (new ones included) with its citation or UNKNOWN, and updates the manifest records to match.
2. **Checkpoint: the operator approves the inventory.** Then the manager runs `python re-analysis/tools/fidelity.py --approve <subsystem>`, which freezes the evidence. From then on the checker rejects any change to the inventory, to a record's title, authority, evidence, live_path or hardware_required, or to a status, except an IMPLEMENTATION_GAP being built. Anything new found during implementation goes back to the extractor and a new approval.
3. **Implement** the discrepancies between the code and the inventory. Existing code is a candidate: whatever the inventory supports stays, and whatever it doesn't is repaired or rebuilt.
4. **Verify.** Then the manager commits without further operator review. `PROJECT_STATE.md` records what each commit accepted, and the queue of cleanup items.
   - **Findings that block a commit:**
     - behavioural or source-fidelity defects: the code does something on the wire, or to what a handler or receiver sees, that the source does not support or contradicts;
     - circular tests;
     - races and deadlocks.
   - **Findings that are queued instead** (fixed in a later batch, with no extra verify round): non-behavioural cleanup such as comments, citations, labels and formatting. This is allowed only while `fidelity.py --check` passes and no fidelity status becomes misleading.
   - **After a behavioural correction,** re-verify only the affected diff and tests, not the whole batch.
   - **The full test suite runs once,** immediately before the commit.
   - **Batches are large.** Split one only when its diff would be unreviewable.
5. **Hardware**, where the layer needs it: the manager writes the script and the operator runs it (see below).
6. **Accept.** The manager sets the subsystem's review state to ACCEPTED with the accepted commit.

### What the checker enforces

`re-analysis/tools/fidelity.py --check` and `FidelityManifestTests` enforce these rules:
- every subsystem has a review state (UNREVIEWED, INVENTORY_APPROVED, ACCEPTED). UNREVIEWED means nothing vouches for its records;
- in an approved subsystem, every settled or to-be-built record cites an address or a file, not just a symbol name. Every settled record has a `// fidelity: <id>` tag in the file it points at, and by acceptance every record does. Until the comparison with the code confirms a source-backed behaviour, the inventory records it as IMPLEMENTATION_GAP (to confirm or build);
- every `// fidelity:` tag anywhere names a real record;
- a record's `verification` (NONE, CAPTURE_VERIFIED, HARDWARE_VERIFIED) names the bundles it rests on. It is independent of status: a hardware pass never raises provenance.

The checker proves the evidence is present and unchanged. Whether the evidence says what the record claims is the verifier's job.

Two rules the checker cannot see, so they are the manager's:

- **A settled record owns its whole production path.** If any behaviour-changing part of
  the path it claims has no record of its own, that part is a gap (RECOVERABLE_GAP or
  IMPLEMENTATION_GAP) and gets a record *before* the claiming record is settled. A
  settled record never defers part of its path to another record, to a code comment, or
  to prose. (M3-022 claimed the whole connection-time NV read while the NV wire — its
  queue, timeout, retries, header and reassembly — had no records; the gap lived in a
  code comment and readiness was set early. That is the `Current warning` failure mode
  again.)
- **A claim about an existing record is checked against the current manifest, not
  recalled.** When an extractor, verifier or pass calls a record contradicted, too weak,
  or already covering something, it quotes that record's current title, status and
  evidence; the manager re-reads the manifest before acting. A claim made against an
  earlier revision is a lead to check, never a finding. (A pass asserted M11-011 still
  described the old Length=1024 request after the record had been rewritten to Length=1.)

### Hardware runs

- Each test is a script the manager writes, plus any physical setup steps.
- A run writes one self-contained bundle to `re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-<test-id>/`, containing the machine-readable result, logs, captures and the script's version hash.
- The script never commits. The operator copies the bundle back, and the manager judges it from the bundle and decides whether it goes into git.
