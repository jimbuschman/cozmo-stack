BLOCKED 2026-09-28 11:14 -05:00 opencode-w2 — the M6-002 packet driver and source render wrappers are built and structurally faithful, but the decoded sample values are not, and the divergent value stage could not be localised.

## What is committed (main)

`96b6f9d` (this job, after the rebase onto `f813461`):
- `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseVorbisDecode.cs` (new): the runtime codebook unpack 0x00ABA188, the setup parse 0x00AB63E0, the packet entry 0x00AB3780, the packet inverse 0x00AB6B14 (floor1 inverse1 0x00AB8E60, residue inverse 0x00AB73F8 types 0/1 and 2, inline coupling inverse 0x00AB6E30, floor1 inverse2 0x00AB915C, mdct_backward 0x00AB4E34), the framing 0x00AB7E40, the window/overlap combine 0x00AB5A94 and driver 0x00AB3520, the stream reset 0x00AB3978, and the `Decode` entry.
- `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseVorbisSource.cs` (new): the two shipped Vorbis source classes (0xAB0448 streamed / 0xAB1550 in-memory) and the emit 0xA73490. Unwired.
- `WwiseVorbisNative.cs`: residue `Stages`, the corrected `Dequantize` shift direction (0x00AB9CB0..0x00AB9D14), `UnreadArithmetic`, and the dead pure `DecodevAdd`/`DecodevvAdd` removed.
- `WwiseCodebookLibrary.cs`: `OpenPacked(id)` for the runtime parse.
- tests: the residue/emit/stream-reset/setup tests; the failing faithfulness test removed.
- `re-analysis/inventory/M6-wwise-bank.md` + `.approved.json`: correction C13 (stream reset, window combine, emit, geometry, derived choices) and C14 (the verifier's class-word count, coupling mark direction, emit argument, reset gate); M6-wwise-bank re-approved.
- `re-analysis/research/20260928-B-M6b-1-stream-reset.md` and `20260928-B-M6b-1-driver-residuals.md`: the two extraction reports.

Verifier: **FAIL** (its blocking findings were fixed: the residue type-0/1 class word is now decoded once per used channel, the circular dead-helper test is gone, the framing renders `available` and the eofflag gate is reproduced, and the null-memo path is handled). `fidelity.py --check` clean. Full suite **1707/1707, 0 skipped**.

## Why it is blocked

The decoder returns samples of the header's `SampleCount` and its **per-packet bit consumption matches the packet byte length exactly** (so the parse is structurally faithful), but the **sample values are wrong**: a cross-check against the NVorbis rebuild gives correlation ≈ 0.002 on shipped mono and stereo media, with no lag peak. The divergent **value stage is not localised**. The candidates, in order, are floor1 inverse1/inverse2, the IMDCT input/output convention, or the window combine. The implementer's delta test of `ImdctBackward` did not match a simple direct formula, so the IMDCT kernel (a B1 artifact, commit c8e45e5) is a candidate.

## What remains (the next job)

1. Localise the divergent value stage by comparing, for one shipped packet, the native's intermediate buffers (the floor memo, the residue ints, the post-floor work, the post-IMDCT buffer) against a reference decode of the same packet. Name the first stage that diverges and the exact instruction. The reports already give every arithmetic leaf; this is a numeric comparison, not new extraction.
2. Fix that stage, then re-run the NVorbis correlation check on several shipped mono and stereo files (a strong, non-circular test).
3. Then B-M6b-2 (the voice/bus engine) and B-M6b-3 (the live wiring).

`M6-002` stays `IMPLEMENTATION_GAP`; its `unresolved` names the defect. Nothing is wired into the live path.

## Records

- No record settled. `M6-002` IMPLEMENTATION_GAP with the value-stage residual named.
- Corrections C13 and C14 recorded and approved.