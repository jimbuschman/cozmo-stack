# Fidelity progress

Counts come from `re-analysis/fidelity_manifest.json`; the manifest and the commits are the record.

## Manifest counts

| status | records |
| --- | ---: |
| EXACT_SOURCE | 74 |
| EQUIVALENT_IMPLEMENTATION | 16 |
| RECOVERABLE_GAP | 75 |
| COMPATIBILITY_POLICY | 19 |
| HARDWARE_ONLY | 3 |
| BLOCKED_EXTERNAL | 6 |
| **total** | **193** |

Remaining RECOVERABLE_GAP: **75** (all on a live execution path). M9 holds 5 of them.

## Current M9 item

M9-010, note release: what ends a held note, and when.

## M9 items resolved since the previous commit

* **M9-006** modulator objects (types 21 and 22) are read; all eleven consume exactly.
* **M9-007** the note-off envelope is applied. Its whole authority over the level is 1 dB, and at the
  shipped sustain level it moves a note by 0.095 dB. It is not what makes notes sustain without release.
* **M9-008** the vibrato LFO is applied, with its depth driven by the game parameter the behaviour posts.
* **M9-009** modulator bindings are applied through their own curves; the accumulation question does not
  arise, because neither target node sets the property its modulator drives.
* **M9-012** MIDI note tracking is off everywhere, from the node bit vectors rather than from the absence
  of a root note.

## Blocked M9 items

* **M9-024** whether the note-off envelope also stops the voice: modulator property 15 is set only here and
  reads 2; its name is in the Wwise SDK, which does not ship. Both readings agree the voice ends at
  note-off, which is what the renderer does.
* **M9-025** the waveform a Wwise LFO draws: not in the package. Unreachable while the vibrato depth is 0.
