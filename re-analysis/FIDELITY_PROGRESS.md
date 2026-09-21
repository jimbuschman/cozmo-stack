# Fidelity progress

Counts come from `re-analysis/fidelity_manifest.json`; the manifest and the commits are the record.

## Manifest counts

| status | records |
| --- | ---: |
| EXACT_SOURCE | 75 |
| EQUIVALENT_IMPLEMENTATION | 16 |
| RECOVERABLE_GAP | 74 |
| COMPATIBILITY_POLICY | 19 |
| HARDWARE_ONLY | 3 |
| BLOCKED_EXTERNAL | 6 |
| **total** | **193** |

Remaining RECOVERABLE_GAP: **74** (all on a live execution path). M9 holds 4 of them.

## Current M9 item

M6-006 and M6-007: an event Play target is flattened to every Sound beneath it, and the first
alternative that decodes is played every time. Both are on the singing path: they are what turns
the get-in phrase into one fixed syllable.

## M9 items resolved since the previous commit

* **M9-010** a sung note sounds for as long as it is held. Every sustain recording is a 4-to-7-second
  vowel that loops until stopped, and every note in every song is shorter than the shortest of them;
  playing a whole recording per note ran the sum 13 dB over full scale. It now peaks 0.6 dB over, which
  is where a mix feeding a limiter with a -1 dB threshold belongs.

Resolved earlier in this pass: M9-006, M9-007, M9-008, M9-009, M9-012.

## Blocked M9 items

* **M9-024** whether the note-off envelope also stops the voice: modulator property 15 is set only here and
  reads 2; its name is in the Wwise SDK, which does not ship. Both readings agree the voice ends at
  note-off, which is what the renderer does.
* **M9-025** the waveform a Wwise LFO draws: not in the package. Unreachable while the vibrato depth is 0.
