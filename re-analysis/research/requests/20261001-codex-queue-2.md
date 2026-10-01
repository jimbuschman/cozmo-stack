# Codex standing queue 2 (from 2026-10-01)

- **Requested by:** manager (Claude)
- **Kind:** a queue of research tasks, after `20260930-codex-queue.md`. Work through them in order.

The rules and the method are the same as queue 1 ("How to use this file" there):
- pull first;
- skip a task whose answer file is already on origin;
- research lane only;
- addresses, floats as bit patterns, gates, order and failure results;
- check that each record owns its whole production path;
- say whether each test takes its values from the source or from the code;
- mark UNKNOWN where the binary doesn't settle something;
- quote a record's current manifest text before you contradict it.

Say when each task is finished, then carry on with the next.

## The queue

**Q14. Re-audit M3 and M4.**
- Scope: every current EXACT_SOURCE record in M3-device and M4-control. The Opus audit called both layers weak.
- Records settled on 2026-09-30 (M4-011, M4-025, M2-003, M3-029, M1-028) are in scope too.
- Answer: `20261001-reaudit-M3-M4.md`

**Q15. Re-audit M7, M8, M9 and M13.**
- Scope: every current EXACT_SOURCE record in M7-behaviour, M8-framework, M9-wwise-music and M13-navigation. The
  audit called these the worst layers; the records that survived it are the ones to check.
- Answer: `20261001-reaudit-M7-M8-M9-M13.md`

**Q16. Re-audit M5 part B and M6.**
- Scope: every current EXACT_SOURCE record in M5-animation not covered by your calibration's part A, and every one in
  M6-wwise-bank.
- Answer: `20261001-reaudit-M5B-M6.md`

**Q17. Find parallel copies of production paths.**
- **What to find:** for each subsystem, any C# code path that duplicates or stands in for the live production path:
  - a second implementation of the same engine function;
  - a tool-only or test-only path that the live stack doesn't use, but that a record or test cites as if it did;
  - an adapter left beside an old path.

  The audit's IdleBehavior case, and the parallel calibration read B-CORE2 removed, are examples.
- **For each:** the file:line, the live path it shadows, which records or tests rely on it, and whether the live
  stack ever runs it.
- **Answer:** `20261001-parallel-paths.md`

**Q18. Find records with no live test.**
- **What to find:** for each settled or "built" record, whether some test drives the behaviour through the live
  production entry (CHECKLIST section 1). The alternatives to flag:
  - the test calls a helper directly;
  - the test uses a test seam in place of the production path;
  - the test returns early when an asset is missing, as `FreeplayTests` does without the OBB;
  - no test names the record at all.
- **For each:** the record, the test, and the entry point a proper test would use.
- **Answer:** `20261001-test-entry-gaps.md`

**Q19. Audit the hardware acceptance scripts.**
- **Scope:** the scripts under `re-analysis/acceptance/hardware/` and the hardware tests in `cozmo-stack` that name
  fidelity records.
- **AGENTS.md requires** that every fidelity id a hardware test names is a real record, and that any BLOCKED_EXTERNAL,
  HARDWARE_ONLY or open gap relevant to the test is visible in the test.
- **Report:** each test, the records it names, the records it should name (the live path it exercises), and the
  uncertainty it hides.
- **Also say** which HARDWARE_ONLY records no script could settle as written.
- **Answer:** `20261001-hardware-tests-audit.md`

**Q20. Check the wire messages.**
- **For each message the C# sends to the robot**, compare its byte layout with the engine's builder: field order,
  widths, signedness, padding and the message tag. Then compare the conditions under which it is sent with the
  engine's send sites.
- **Start with** the messages on the connection path, then motion, then animation streaming, then the rest.
- **Report:** HOLDS or DEFECT per message, with the engine's builder address and the C# file:line.
- **Answer:** `20261001-wire-messages.md`

**Q21. Pre-extract the rest of M3 and M4.**
- **Scope:** the IMPLEMENTATION_GAP records in M3-device and M4-control that are not in B-CORE or B-CORE2 and that
  your B-CORE extractions (`20260930-bcore-extractions.md`) don't cover.
- **Answer:** `20261001-pre-extraction-M3-M4-rest.md`

**Q22. Pre-extract M14.**
- **Scope:** every IMPLEMENTATION_GAP and RECOVERABLE_GAP record in M14-faces.
- **Separate** what is engine code in `libcozmoEngine.so` from what is truly inside OKAO, which is BLOCKED_EXTERNAL.
- **Name the exact boundary:** every call into OKAO and the data it carries each way, so that the stack can
  reproduce everything on the engine's side of it.
- **Answer:** `20261001-pre-extraction-M14.md`
