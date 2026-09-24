This project is reverse-engineering the official Anki Cozmo app/engine into a C# control stack.

The goal is source-faithful app-side behavior, not merely behavior that appears to work.

**Start every session by reading `PROJECT_STATE.md`.** It says which layer is in progress, what has been approved and what is open. Work follows the Process section at the end of this file.

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

No role does both extraction and implementation in the same task.

### Per layer

Layers go bottom-up, one at a time: M1 transport, M2 protocol, then device, control, animation, behaviours and higher layers as `PROJECT_STATE.md` orders them.

1. **Inventory.** The extractor traces the layer. The manager writes `re-analysis/inventory/<subsystem>.md`, which names every record of the subsystem (new ones included) with its citation or UNKNOWN, and updates the manifest records to match.
2. **Checkpoint: the operator approves the inventory.** Then the manager runs `python re-analysis/tools/fidelity.py --approve <subsystem>`, which freezes the evidence. From then on the checker rejects any change to the inventory, to a record's title, authority, evidence, live_path or hardware_required, or to a status, except an IMPLEMENTATION_GAP being built. Anything new found during implementation goes back to the extractor and a new approval.
3. **Implement** the discrepancies between the code and the inventory. Existing code is a candidate: whatever the inventory supports stays, and whatever it doesn't is repaired or rebuilt.
4. **Verify.** The verifier returns PASS, `fidelity.py --check` passes, and the tests pass. Then the manager commits without further operator review. `PROJECT_STATE.md` records what each commit accepted.
5. **Hardware**, where the layer needs it: the manager writes the script and the operator runs it (see below).
6. **Accept.** The manager sets the subsystem's review state to ACCEPTED with the accepted commit.

### What the checker enforces

`re-analysis/tools/fidelity.py --check` and `FidelityManifestTests` enforce these rules:
- every subsystem has a review state (UNREVIEWED, INVENTORY_APPROVED, ACCEPTED). UNREVIEWED means nothing vouches for its records;
- in an approved subsystem, every settled or to-be-built record cites an address or a file, not just a symbol name. Every settled record has a `// fidelity: <id>` tag in the file it points at, and by acceptance every record does. Until the comparison with the code confirms a source-backed behaviour, the inventory records it as IMPLEMENTATION_GAP (to confirm or build);
- every `// fidelity:` tag anywhere names a real record;
- a record's `verification` (NONE, CAPTURE_VERIFIED, HARDWARE_VERIFIED) names the bundles it rests on. It is independent of status: a hardware pass never raises provenance.

The checker proves the evidence is present and unchanged. Whether the evidence says what the record claims is the verifier's job.

### Hardware runs

- Each test is a script the manager writes, plus any physical setup steps.
- A run writes one self-contained bundle to `re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-<test-id>/`, containing the machine-readable result, logs, captures and the script's version hash.
- The script never commits. The operator copies the bundle back, and the manager judges it from the bundle and decides whether it goes into git.
