# Job B-ADP-HARNESS: the audio equivalence test (AGENTS.md, ADP-1)

- **Agent:** Codex, as builder (`jobs/CODEX-BUILDER.md`).
- **Type:** build, test tooling only.
- **Records:** none settle here. This job builds the test that later lets an ADP-1 boundary pass.

## What to build

1. **A reference renderer under the emulator.**
   - It runs the engine's own audio path (`re-analysis/tools/emu/`, the way `emu_vorbis.py` runs the Vorbis decode)
     for a given event, its state, its RTPCs and its timing.
   - It writes the robot output stream as the engine produces it: 22320 Hz in 744-byte chunks.
   - Where an engine stage can't yet run under emulation, record that stage as MISSING. Don't approximate it.
2. **The same render from the C# stack** for the same inputs.
3. **The comparison**, in order:
   - **Structure, pass/fail:** frame counts, chunk boundaries, start and end sample positions, silent/non-silent
     regions, timing offset.
   - **Then PCM error:** RMS/SNR and maximum absolute error, per case and overall.
4. **The corpus:** shipped events covering each DSP path:
   - mono Vorbis, stereo Vorbis → mono, ADPCM;
   - volume and RTPC changes, pitch/resampling;
   - LPF/HPF, EQ, limiter, compressor;
   - several simultaneous voices, a voice-limit or ducking case, a continuous/crossfade case;
   - singing, once M9 can run.
5. **Report, don't decide.** The job measures and writes the numbers to `re-analysis/research/<date>-adp-baseline.md`.
   The manager and the operator set the thresholds from them.

## Gates

Before each commit:
- self-review against CHECKLIST.md;
- `fidelity.py --check` passes;
- the full suite passes.

Log each batch in `status/B-ADP-HARNESS.md`. Finish with `DONE` or `BLOCKED`.
