# Codex standing queue (from 2026-09-30)

- **Requested by:** manager (Claude)
- **Kind:** a queue of research tasks. Work through them in order.

## How to use this file

1. Before each task, pull. If the task's answer file already exists on origin, skip the task.
2. **Research lane only** (`re-analysis/research/README.md`): write only your answer file; no code, manifest,
   inventory, branch or commit.
3. **Method for every task**, the same as your audit calibration:
   - cite addresses in `resources/lib/armeabi-v7a/libcozmoEngine.so`;
   - give floats as bit patterns;
   - check gates, order and failure results;
   - check that each record owns its whole production path;
   - say whether each test takes its expected values from the source or from the code;
   - mark UNKNOWN where the binary doesn't settle something;
   - quote a record's current manifest text before you contradict it.
4. When a task is finished, say so, then start the next one. The operator commits and pushes your answers.

## The queue

**Q1. Review the B-CORE2 batches.**
- Scope: aeaf977, bd12929, 4d960de, e0f4333 and caaef86 (`re-analysis/jobs/status/B-CORE2.md`).
- Check each diff against the rows it cites (`research/20260930-B-CORE-verify-{1-2,3-4,5-6}.md`), the binary and
  `re-analysis/jobs/CHECKLIST.md`.
- Per record: HOLDS, or DEFECT with the engine behaviour and address and the C# file:line. List any circular test.
- Batch 4 had a suite failure that passed on re-run. Find the flaky test and its cause, if you can from reading.
- Answer: `20260930-B-CORE2-review.md`

**Q2. Re-audit M11.**
- Scope: every current EXACT_SOURCE record in M11-vision.
- Method: the same as `requests/20260930-reaudit-sonnet-layers.md`.
- Answer: `20260930-reaudit-M11.md`

**Q3. Review R-VIS round 1.**
- Scope: the records built or touched by commits dfb84a3, f96d9e8, 1ecfa20 and 89a718e (M11 batch A, M12, M13).
- Check them against the checklist and the binary.
- Answer: `20260930-R-VIS-review.md`

**Q4. Read the RECOVERABLE_GAP records.**
- Scope: every RECOVERABLE_GAP record at HEAD.
- Read the unread source that the record's `unresolved` names, and give rows. One section per record.
- Answer: `20260930-recoverable-gaps.md`

**Q5. Calibrate the Opus audit.**
- Scope: re-audit the current EXACT_SOURCE records of M1-transport and M2-protocol. These passed the Opus audit, so
  this calibrates it.
- Give one line per layer comparing your result with the Opus audit's.
- Answer: `20260930-calibrate-opus-M1-M2.md`

**Q6. Pre-extract M5, M10 and M15.**
- Scope: the IMPLEMENTATION_GAP records in M5-animation, M10-derived and M15-freeplay whose `unresolved` doesn't
  start "built".
- For each, give the engine's production path, the C# entry, and rows.
- Answer: `20260930-pre-extraction-M5-M10-M15.md`

**Q7. Pre-extract M9 for R-M9.**
- Scope: every IMPLEMENTATION_GAP and RECOVERABLE_GAP record in M9-wwise-music.
- Include the Wwise music-engine code (it ships in the .so) and the banks and assets it reads.
- BLOCKED_EXTERNAL is only for what is truly not in the package, with the reason.
- Answer: `20260930-pre-extraction-M9.md`

**Q8. Recheck HARDWARE_ONLY and BLOCKED_EXTERNAL.**
- Scope: every HARDWARE_ONLY and BLOCKED_EXTERNAL record.
- Decide whether any shipped artifact settles it after all. Search the .so files, the Unity code, the OBB assets,
  the banks and the configs.
- Per record: SETTLEABLE, with the rows, or CONFIRMED, with what you searched.
- Answer: `20260930-hardware-blocked-recheck.md`

**Q9. Check the float-literal baseline.**
- Background: `cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/float_literal_baseline.txt` lists 437 decimal
  literals in fidelity-tagged code, which the lint tolerates.
- For each literal:
  - find the engine value it stands for;
  - give the engine's bit pattern and width;
  - report whether the C# literal, at its C# type, produces the same bits.
- Group the mismatches by record. These are exactly the defects the calibration found in M12.
- Answer: `20260930-float-baseline-check.md`

**Q10. Pre-extract M6 for R-M6.**
- Scope: every IMPLEMENTATION_GAP record in M6-wwise-bank not covered by `20260929-M6-live-audio-bodies-extraction.md`
  or your M6 check.
- Pay particular attention to the production wiring your check found missing: the live audio components and the frame
  hooks. Name the engine's calling path and the C# entry each needs.
- Answer: `20260930-pre-extraction-M6.md`

**Q11. Pre-extract the rest of M7 and M8.**
- Scope: the IMPLEMENTATION_GAP records in M7-behaviour and M8-framework that R-BEH2's 19 don't include. The list of
  19 is in `re-analysis/jobs/R-BEH2.md`.
- Answer: `20260930-pre-extraction-M7-M8-rest.md`

**Q12. Pre-extract R-VIS round 2.**
- Scope: the items R-VIS left for its next round (`re-analysis/jobs/status/R-VIS.md`, "Left for the next round"):
  - M11 batch B (the quad tree M11-045/046, the M14-007 rebuild, the M11-017 entry points, M11-047/048);
  - M13-024..027;
  - M11-038's broadcasts;
  - M11-050..053;
  - the RECOVERABLE_GAPs it lists.
- Skip anything Q4 already answered.
- Answer: `20260930-pre-extraction-R-VIS-2.md`

**Q13. Find circular tests, layer by layer.**
- Scope: for each subsystem, the tests that its records name.
- Find every assertion whose expected value is taken from the implementation, rather than from the binary or a
  shipped asset: a copied constant, the same arithmetic, the implementation's own string, or a test seam standing in
  for the production path.
- Per test: the file:line, what it copies, and the value the engine gives.
- Answer: `20260930-circular-tests.md`, one section per subsystem, M1 first.
