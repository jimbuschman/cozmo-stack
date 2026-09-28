# B-M6b-1 numeric-stage localisation

Date: 2026-09-28

This is a diagnostic comparison, not primary-source evidence. It does not change any
approved M6 inventory row.

## Method

The same shipped mono Wwise Vorbis media (`557491`) was decoded twice:

1. by the B-M6b-1 packet driver; and
2. by NVorbis over `WwiseVorbisRebuilder.ToOgg`, instrumented only to copy its
   per-packet residue, post-coupling and post-floor buffers before its IMDCT.

The B-M6b-1 driver was instrumented at the corresponding three boundaries. The setup
and packet bytes were unchanged. Five consecutive audio packets were compared by
normalised dot product, which ignores the known fixed-point-to-float scale difference.

## Result

| stage | packet correlations |
|---|---|
| residue | 1, 1, 1, 1, 1 |
| post-coupling | 1, 1, 1, 1, 1 |
| post-floor | 0.9999999796, 0.9999999820, 0.9999999749, 1.0000000118, 1.0000000037 |

The first divergent behavior-changing stage is therefore the call at
`0x00AB6F04` into `mdct_backward` entry `0x00AB4E34`. The existing generated
`WwiseVorbisImdct.cs` output has low correlation with an independent Vorbis IMDCT for
the same spectral input, and the shipped end-to-end signal remains about 0.002 with
that generated kernel.

A diagnostic substitution of an independent standard Vorbis IMDCT proved that the
upstream spectrum is usable, but it was not retained: its packed half-buffer layout
and transition combine are not the approved native instruction path, so committing it
would violate the exact-source rule.

## Remaining blocker

The approved X5 extraction is an instruction listing/transliteration input but contains
no native intermediate vectors or worked output. The exact first bad instruction
inside `0x00AB4E34..0x00AB5A1C` cannot be selected from final-output comparison alone.
Repair requires either:

- native intermediate captures at the phase boundaries (pre-symmetry, butterfly,
  stage network, terminal butterfly and tail), or
- a fresh instruction-by-instruction audit of the generated control-flow translation
  against the `.so`, with numeric checks after each repaired phase.

Until that is done, M6-002 remains `IMPLEMENTATION_GAP` and B-M6b-1 remains blocked.
