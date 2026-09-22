This project is reverse-engineering the official Anki Cozmo app/engine into a C# control stack.

The goal is source-faithful app-side behavior, not merely behavior that appears to work.

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