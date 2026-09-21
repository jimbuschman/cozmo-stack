# Fidelity progress

Counts come from `re-analysis/fidelity_manifest.json`; the manifest and the commits are the record.

## Manifest counts

| status | records |
| --- | ---: |
| EXACT_SOURCE | 77 |
| EQUIVALENT_IMPLEMENTATION | 16 |
| RECOVERABLE_GAP | 71 |
| COMPATIBILITY_POLICY | 19 |
| HARDWARE_ONLY | 3 |
| BLOCKED_EXTERNAL | 7 |
| **total** | **193** |

Remaining RECOVERABLE_GAP: **71** (all on a live execution path). M9 holds 2 of them; M6 now holds none
and is the first subsystem to assert source-completeness.

## Current M9 item

M9-011, the output stage: the bus effect chain the robot's audio actually passes through, read from
Init.bnk, in place of the local peak normalisation.

## M9 items resolved since the previous commit

* **M6-006** an event's Play target is walked with each container's own semantics instead of flattened.
* **M6-007** a container that chooses now draws, instead of the first alternative that decodes winning
  every time.
* **M9-013** reclassified BLOCKED_EXTERNAL, with the search that exhausted the shipped data recorded, the
  branch's share reported on every render, and `--without-branch get-in` to hear the other reading.

Resolved earlier in this pass: M9-006, M9-007, M9-008, M9-009, M9-010, M9-012.

## Blocked M9 items

* **M9-013** whether Wwise routes MIDI notes into the get-in branch. 41 extra voices on a 42-note song
  under the uniform rule; only a recording of the stock app singing, or a Wwise runtime, can settle it.
* **M9-024** whether the note-off envelope also stops the voice: modulator property 15 is set only here and
  reads 2; its name is in the Wwise SDK, which does not ship. Both readings agree the voice ends at
  note-off, which is what the renderer does.
* **M9-025** the waveform a Wwise LFO draws: not in the package. Unreachable while the vibrato depth is 0.
