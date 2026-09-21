# Fidelity progress

Counts come from `re-analysis/fidelity_manifest.json`; the manifest and the commits are the record.

## Manifest counts

| status | records |
| --- | ---: |
| EXACT_SOURCE | 77 |
| EQUIVALENT_IMPLEMENTATION | 18 |
| RECOVERABLE_GAP | 70 |
| COMPATIBILITY_POLICY | 19 |
| HARDWARE_ONLY | 3 |
| BLOCKED_EXTERNAL | 8 |
| **total** | **195** |

Remaining RECOVERABLE_GAP: **70** (all on a live execution path). M9 holds 1 of them; M6 holds none
and is the first subsystem to assert source-completeness.

## Current M9 item

M9-017, the cube shake: which cube message the engine's ShakeListener averages into the vibrato, and at
what scale. It is the last live-path gap M9 holds.

## M9 items resolved since the previous commit

* **M9-011** the output stage is now the effect chain the robot's own bus carries, read from Init.bnk and
  routed by the engine's own registration table: two parametric EQs and a peak limiter, then Anki's tap.
  Every song leaves it between 30000 and 32000 of full scale with 2.5 to 7.3 dB of limiting, where the
  local peak normalisation had been attenuating each song by 10 to 13 dB by whatever its loudest sample
  happened to be.
* A documented claim was corrected: the master compressor is on Cozmo_Robot_External, the bus for the
  app's spoken text, not on the robot's own path.

Resolved earlier in this pass: M6-006, M6-007, M9-006 to M9-010, M9-012, M9-013.

## Blocked M9 items

* **M9-026** the coefficient formulas inside Wwise's parametric EQ and peak limiter. Their settings are
  exact; the arithmetic between them is standard rather than Audiokinetic's.
* **M9-013** whether Wwise routes MIDI notes into the get-in branch. 41 extra voices on a 42-note song
  under the uniform rule; only a recording of the stock app singing, or a Wwise runtime, can settle it.
* **M9-024** whether the note-off envelope also stops the voice: modulator property 15 is set only here and
  reads 2; its name is in the Wwise SDK, which does not ship. Both readings agree the voice ends at
  note-off, which is what the renderer does.
* **M9-025** the waveform a Wwise LFO draws: not in the package. Unreachable while the vibrato depth is 0.
