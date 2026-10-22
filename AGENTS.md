This project is reverse-engineering the official Anki Cozmo app/engine into a C# control stack.

The goal is source-faithful app-side behavior, not merely behavior that appears to work.

**Start every session by reading `PROJECT_STATE.md`.** It says which layer is in progress, what has been approved and what is open. Work follows the Process section at the end of this file.

## What we reproduce (operator, 2026-10-17; replaces "Exact, always" of 2026-09-27)

The stack is a standalone C# system that drives a real Cozmo. It reproduces the engine's **observable behaviour** for
the features we support, using implementations suited to a standalone PC program.

**Reproduced from source (the evidence rules below apply in full):**
1. what the stack sends the robot: messages, payloads, order, timing;
2. what the robot does as a result;
3. what the stack's C# API delivers for supported features: which events, when, in what order, with what payload,
   and the readiness it reports;
4. engine state that later feeds 1–3 (config values, world state, needs, mood, timers, readiness).

Within that scope there is no guessing: the behaviour comes from the engine, or it is a recorded gap.

**Implementation is free where the observable result is the same.** Threads, containers, startup plumbing and error
machinery may be written however suits this system, as long as 1–4 come out the same.

**Not reproduced:**
- the original's bugs, crashes, hangs and undefined behaviour (the M1-034 standing rule below);
- phone and app machinery the stack replaces: the Unity UI and flows, the Android layer, the game-message socket
  plumbing, SDK mode, firmware update, crash upload, the on-device log files;
- failure branches for inputs our system never produces.

**Failures that can happen here** (a missing file, a bad host config, a dropped connection, a robot error) are handled
sensibly: fail clearly, keep running where safe, and log enough to diagnose. The original's exact error machinery is
not copied.

**Logging** is a debugging tool. Where a built step logs, use the engine's key and text if known; it costs little and
helps comparison. Exact wording, levels and typos are best effort and never block acceptance by themselves. A missing
or misleading diagnostic that prevents understanding a real failure is a defect.

**Not supported is a decision; no effect is a claim.** They are recorded differently.
- *Not supported:* recorded as "not supported: <reason>, decided by <who> <date>", in the record's `unresolved`
  (COMPATIBILITY_POLICY with `live_path: false` when the whole record is excluded). Excluding a robot behaviour or a
  C# API feature needs the operator. The manager may exclude phone-app machinery and failure branches our system
  cannot reach, recording the reason. An exclusion keeps its evidence and is reversible.
- *No effect:* needs evidence that the step touches none of 1–4. "Unknown" is not "unnecessary", and "nothing uses it
  yet" never justifies dropping a feature we intend to provide.

**Narrow checks.** When it is unclear whether a routine matters, ask the narrow question first: does it touch 1–4,
including API delivery, readiness and downstream state? Do not fully extract a routine just because it is on the path.

**Applied layer by layer, never in bulk.** A record changes status only when its layer is worked, with its own reason.
Unresolved records elsewhere are not dismissed by this section.

The audio policy ADP-1, the app-boundary scope and the approved departures below are unchanged; the departures are
now instances of this section.

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
- **SDK mode is not supported,** neither external SDK programs nor the internal SDK mode the app enters for CodeLab and Edu mode. Its engine paths are unreachable and are recorded as such.
- **Robot firmware update is not supported** (operator, 2026-10-14). Only the app starts the engine's FirmwareUpdater, and the stack accepts any firmware (M1-040). The updater's engine paths (for example the flash-write ack handler EF and FirmwareUpdateComplete) are unreachable in the stack and are recorded as such, not built.

App-layer code is still evidence of what the engine receives, and stays authority 2. It isn't reproduced unless the
stack implements that feature.

### Other approved departures

- **M1-034, handler isolation** (operator, 2026-10-07): a handler exception is caught, logged and survived; the original aborts. It also covers the backpack-light loader's
  non-object JSON throw (operator, 2026-10-11; M4-036), which the shipped file never reaches. It also covers the
  animation and face loaders' fatal paths (operator, 2026-10-12; `research/20261012-m5-x9-extraction.md`), which no
  shipped file reaches:
  - a JsonCpp throw (stack limit; a non-object root's find/getMemberNames LogicError);
  - a face frame number that `stoi` rejects as out of range;
  - a malformed non-empty `.bin` (the original reads it unchecked: undefined behaviour).

  The stack skips that file, logs the skip visibly, and carries on.

  It also covers the streamer's null-idle path (operator, 2026-10-12; M5-027). An idle stack whose top trigger has no
  animation, with no old idle, makes the original dereference null: `InitStream(null)` → `Animation::Init` at
  0x00577BC6, or `HasFramesLeft`/`UpdateStream(null)`. The stack skips that tail, logs it visibly, and carries on.

  It also covers the neutral face with no face keyframe (operator, 2026-10-12; M5-010). If the first clip of
  `ag_neutral_face` has no ProceduralFace keyframe, the original reads address 0xC (0x0057A20A/0x0057A20C). No shipped
  file reaches it. The stack skips SetResetData, keeps the previous reset data, logs it visibly, and carries on.

  It also covers the cube-light loader's fatal paths (operator, 2026-10-12; M4-044;
  `research/20261012-m4-cube-lights-check.md`), which none of the 37 shipped files reaches: a JsonCpp stack-limit
  throw; getMemberNames on a non-empty array or scalar root; a non-empty object as an animation value; an entry or
  pattern that isn't an object; a wrong-kind or out-of-range `as*()` call. The original reaches std::terminate. The
  stack skips that file (any earlier definition of the animation is kept), logs the skip visibly, and carries on.

  It also covers the cube-light dangling blended entry (operator, 2026-10-13; M4-045;
  `research/20261013-b-m3m4-17-check.md`). A later, shorter blended play reassigns the shared slot (0x006385B6) under a
  buried blended entry whose iterator is past the new end. When that entry resumes, the original reads a freed node
  (0x00637ACC..0x00637AE4). This is reachable with shipped SpeedTapWin, then Flash. The stack skips that entry's
  resend, logs it visibly, and carries on.

  **A standing rule for every such case** (operator, 2026-10-13): any path where the original crashes, aborts or reads
  undefined memory is covered without a further operator question. That includes std::terminate, an uncaught throw, a
  null dereference, a freed node, a heap read past an object, or a stoull/JsonCpp throw on malformed input. The stack
  skips the smallest unit the original would have lost, keeps the prior state, logs the skip visibly (naming M1-034 and
  the owning record), and carries on.

  Each case is still recorded: the owning record's `unresolved` names the crash point (address) and the skip.

  The rule covers only undefined or terminating behaviour. Where the original's behaviour is defined, "What we
  reproduce" governs it.

  **Hangs are covered too** (operator, 2026-10-16: "skip and log. generally we don't want to reproduce errors"). A path
  where the original blocks forever or deadlocks is treated the same way: the stack skips the wait, logs the skip
  visibly (naming M1-034 and the owning record), and carries on. The first case is M6-042 N12, the Wwise Term
  CancelEventCallback wait (0xA03604).

  This is about faults of the original, not its error handling. Where the engine handles an error in a defined way
  (an error log, a failure result, an error flag), "What we reproduce" governs it (2026-10-17): an effect on robot
  traffic, robot behaviour, API delivery or readiness, or the state that feeds them (a failure result, a flag something
  later acts on) is reproduced when the error can occur in our system; the log text is best effort; branches for inputs
  our system never produces are not built.
  The first case under the standing rule is the backup manager's malformed inputs (M15-039, M15-040, M15-042):
  - a non-numeric `.backup` name;
  - a statsForBackup.json whose ConnectionCount isn't an object or isn't numeric;
  - a short or oversized backup file.
- **M1-040, firmware check** (operator, 2026-10-09): every robot firmware is accepted and logged; the original rejects a version that differs from the app's. Without it the operator's robot (2457) is refused.
- **M1-036, crash reporting** (operator, 2026-10-09): no Breakpad crash reporter.
- **M1-038, after a disconnect sub-message** (operator, 2026-10-09): stop processing the frame; the original continues through freed memory.
- **M3-054, verbose log display** (operator, 2026-10-11): the stack's log display follows libDAS sink (a) exactly by
  default (M3-053); a stack setting, off by default, also shows the info and debug entries the phone drops.
- **M5-047, the stale head window** (operator, 2026-10-11).
  - **The original:** reads two heap addresses as the head-window min/max of group entries that come before their
    file's first UseHeadAngle entry, so its backup choice depends on the phone's memory layout.
  - **The stack:** follows the 64-bit-phone case. Those entries are never accepted by the all-on-cooldown backup.
- **M3-051, libDAS sink (b)** (operator, 2026-10-12): the stack does not write the on-device log files. They exist only
  to be uploaded through the app's Java `postToServer` to Anki's servers, and nothing in scope reads them. The engine's
  own calls into libDAS stay exact (M3-055). A flush with a callback reports a failed transfer, as a phone with no
  connection does (`research/20261012-m3-051-libdas-sink-b-check.md`).
- **M3-045, non-JPEG image decoders** (operator, 2026-10-12): only the JPEG decoder is ported. The decoder choice for
  encodings 5/6/7 stays exact (the signature window, the registration order, the first match). A payload that matches
  one of the nine other OpenCV decoders (BMP, HDR, WebP, SunRaster, PxM, TIFF, PNG, JPEG 2000, EXR) returns the empty
  image with a visible MISSING, where the original would decode it. The operator's robot sent only encoding 8 (M3-050),
  and the engine cannot request 5/6/7.
- **M2-017, failed field reads** (operator, 2026-10-09): 0/false; the original leaves uninitialised stack contents.
- **M11-047, the border-walk seed order** (operator, 2026-10-14).
  - **The original:** iterates a type set (`unordered_set` of quad-tree node pointers, murmur2 hash) whose order follows
    the phone's heap addresses (bionic malloc, not shipped).
  - **The stack:** iterates the seeds in insertion order (the order the nodes entered the type set), deterministically.
    Everything else in the walk stays exact (`research/overnight/20261014-ov11-m11-047-check.md`).
  - **Extended to the memory map's FindIf results** (operator, 2026-10-15; M11-074). FindContentIf/FindIf collects the
    matching data pointers into an `unordered_set` (murmur2 hash, 0x006884FC..0x006886F4), so its order follows heap
    addresses too. The stack returns them in insertion order: the order the post-order walk first finds them. Everything
    else stays exact (`research/overnight/20261015-ov42-44-check.md`).
  - **Extended to two more cases** (operator, 2026-10-15; `research/overnight/20261015-ov50-52-check.md`). Everything else
    stays exact.
    - **GetBorders' region area sum (M11-079):** the f32 area terms (0x006899D0..0x006899F4) are added in insertion
      order, the order the nodes entered the pointer set, instead of heap-address iteration order.
    - **The profiler's teardown print order (M11-078 / M11-055):** ~Profiler's timer lines (0x0087CFD4..0x0087CFE8) are
      printed in insertion order, the order the timers were created, instead of `std::hash<const char*>` over
      string-literal addresses.
  - **Extended to the animation-name list (M1-055)** (operator, 2026-10-17; `research/20261017-b-m1-4-check.md`).
    GetAnimationNames (0x00588190) walks an `unordered_map<string, Animation>` filled in readdir order by up to four
    loader threads, so its order differs between phones. The stack delivers the names in its loader's deterministic
    definition order. The `_PROCEDURAL_` skip, first-definition-wins and the broadcast tail stay exact.

Apply no other departure without the operator, except the manager's exclusions allowed in "What we reproduce".

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
- **Parallel jobs (2026-09-27):** several opencode windows, each in its own clone, run chains of jobs from `re-analysis/jobs/BOARD.md` unattended: extraction (X), integration (I) and build (B). Each job stays within its write scope; read `re-analysis/jobs/README.md`. The push gate (`git config core.hooksPath scripts/hooks`) refuses a push to main unless the fidelity check and the full suite pass. A push that changes only the fidelity
  records (manifest, `FIDELITY_GAPS.md`, inventory), with nothing under `cozmo-stack/` or `re-analysis/tools/`, runs
  only the test classes that read those records (operator, 2026-10-12). A push whose code changes are only the test harness
  (`cozmo-stack/src/Cozmo.Conformance/`) and the test files that use it runs only those test classes (operator, 2026-10-20).
  The gate also enforces the queue (operator, 2026-10-21; `re-analysis/tools/queue_gate.py`): at most 4 open research
  rows, no research row added under "HOLD ON NEW READING", and no record settled or subsystem ACCEPTED without a
  Claude check file `re-analysis/research/*claude-check*.md` naming it. Before tracing engine code, run
  `python re-analysis/tools/known.py <address|name>` to see what earlier passes established.
- **Research (Codex / ChatGPT)** works only in `re-analysis/research/`, under the rules in `re-analysis/research/README.md`: no branches, no worktrees, no commits. Background research is authority 6, and a Codex extraction is checked like any extractor report.

No role does both extraction and implementation in the same task.

### Per layer

Layers go bottom-up, one at a time: M1 transport, M2 protocol, then device, control, animation, behaviours and higher layers as `PROJECT_STATE.md` orders them.

1. **Inventory.** The extractor traces the layer. The manager writes `re-analysis/inventory/<subsystem>.md`, which names every record of the subsystem (new ones included) with its citation or UNKNOWN, and updates the manifest records to match.
2. **Checkpoint: the operator approves the inventory.** Then the manager runs `python re-analysis/tools/fidelity.py --approve <subsystem>`, which freezes the evidence. From then on the checker rejects any change to the inventory, to a record's title, authority, evidence, live_path or hardware_required, or to a status, except an IMPLEMENTATION_GAP being built. Anything new found during implementation goes back to the extractor and a new approval.
3. **Implement** the discrepancies between the code and the inventory. Existing code is a candidate: whatever the inventory supports stays, and whatever it doesn't is repaired or rebuilt.
4. **Verify.** Then the manager commits without further operator review. `PROJECT_STATE.md` records what each commit accepted, and the queue of cleanup items.
   - **Findings that block a commit** (operator, 2026-10-17):
     - behaviour in "What we reproduce" 1–4 that the source does not support or contradicts;
     - a "no effect" claim without evidence, or an exclusion with no recorded decision;
     - a missing or misleading diagnostic that would hide a real failure;
     - circular tests;
     - races and deadlocks.
   - **Never blocking by themselves:** log wording, levels and typos; the fidelity of branches our system cannot reach;
     internal mechanism differences with the same observable result.
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
- **Every expectation is sourced** (operator, 2026-10-20). A check passes or fails only on an expected result that
  cites the engine (a record and its native address or file), the same evidence rule as the stack. Anything that
  cannot be sourced (appearance, random timing, firmware display) is recorded as an observation for the operator
  to judge, never as an automatic pass or fail. A rule with no citation is a harness defect.
- The script never commits. The operator copies the bundle back, and the manager judges it from the bundle and decides whether it goes into git.
