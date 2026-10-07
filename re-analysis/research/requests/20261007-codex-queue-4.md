# Codex standing queue 4 (from 2026-10-07): emulator-checked work

- **Requested by:** manager (Claude)
- **Use:** work through the tasks in order. Pull before each task, and skip a task that is already done on origin.

## Why emulator-checked

The manager can't review new extraction for a while. Work whose correctness comes from running the shipped code
under the emulator (`re-analysis/tools/emu/`, Unicorn; or your qemu harness) needs no reading review: a mismatch is a
proven defect, and a match is proven equality. This queue does only that kind of work. M1-029's converter and M6-002's
Vorbis decode are the models to follow.

## Rules

1. **The rules of queues 3 and CODEX-BUILDER apply:**
   - depth over speed;
   - a coverage table first;
   - nothing NOT DONE when a task finishes;
   - you commit and push your own work;
   - you never settle a record.
2. **Expected values come only from the emulator** running the shipped function. Check them in as fixtures, and
   record the generator script and its inputs next to them. Never compute an expected value with the C#.
3. **When the C# disagrees with the emulator, fix it only to match the emulator's output.** A fix that needs reading
   the code's logic, rather than matching its output, isn't this queue's work: write it up as a DEFECT for the
   manager.
4. **ADP-1 boundary.** In M6/M9, a per-sample DSP difference is not a defect. Record its numbers in the ADP baseline
   instead.
5. **Stay inside layers M1–M5.** Don't follow callers into higher layers.

## The queue

**Q10. Differential oracles for the numeric functions of M1–M5.**
- **Scope:** every engine function in M1–M5 that is numeric or a pure transform, and that the C# reproduces. For
  example: the mu-law encoder, the lift and head angle maths, RescaleRadians, IsNear, the procedural-face maths,
  GenerateEyeShift, the keyframe interpolation, the RNG (`GetNextDbl`, mt19937), the blink table and the image
  ToGray.
- **For each:** an oracle, and a differential test over a corpus with edges, NaN, ±0, infinities and boundaries.
- **Fix the C#** where it differs, per rule 3. Tests go under `cozmo-stack/tests` with fixtures.
- **Report:** `research/20261007-oracle-coverage.md`: each function and record, its oracle, the cases it covers, and
  whether it matched, or what it matched after a fix.

**Q11. Replace the circular tests with oracle fixtures.**
- **Scope:** the circular tests you found in Q7 (`20261006-circular-tests-M3-M5.md`), together with the ones Opus
  recorded for M1/M2 (for example `EngineAppLayerTests.cs:370`).
- **For each:** if the engine function can run under the emulator, replace the copied expected value with an oracle
  fixture. If it can't, leave it, and list it with the reason.
- **Report:** `research/20261007-circular-fixes.md`.

**Q12. The float literals, M1–M5.**
- **Scope:** each literal in `20261006-float-baseline-M3-M5.md` (and M1/M2's) whose engine bits you read directly from
  a `movw/movt`, a `vmov` immediate or a literal pool.
- **The fix:** replace the decimal with the engine's bit pattern, using `BitConverter.Int32BitsToSingle(0x...)`, or the
  double equivalent at the engine's width. Cite the address in a comment. Remove the line from the lint baseline.
- **Don't change behaviour beyond the literal.** If the width is wrong as well (double where the engine uses float),
  that is a DEFECT for the manager.
- **Report:** `research/20261007-literal-fixes.md`.

**Q13. Extend the ADP harness corpus.**
- **Scope:** add every DSP path still missing from `20261006-adp-baseline.md`'s corpus, as far as the engine's audio
  path can run under emulation.
- **Report:** update the baseline numbers. Don't choose thresholds.

Stop after Q13 and append a one-paragraph summary to `research/20261007-oracle-coverage.md`.
