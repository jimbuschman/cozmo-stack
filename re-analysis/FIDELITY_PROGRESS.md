# Fidelity progress

Counts come from `re-analysis/fidelity_manifest.json`; the manifest and the commits are the record.

## Manifest counts

| status | records |
| --- | ---: |
| EXACT_SOURCE | 79 |
| EQUIVALENT_IMPLEMENTATION | 17 |
| RECOVERABLE_GAP | 69 |
| COMPATIBILITY_POLICY | 20 |
| HARDWARE_ONLY | 3 |
| BLOCKED_EXTERNAL | 7 |
| **total** | **195** |

Remaining RECOVERABLE_GAP: **69**, all on a live execution path, **none of them in M9**.

## Current M9 item

None. **M9 holds no RECOVERABLE_GAP on its live execution path**, which is what the gate means by
source-complete, and the flag is set in the manifest. Everything M9 has left is named in
WWISE_MUSIC.md section 5 and is one of two kinds: in the Wwise runtime, which does not ship in this
package, or waiting on a recording of the stock app singing.

## M9 items resolved since the previous commit

* **M9-017** the cube shake is measured: `ObjectAccel` (0xF5) through the engine's own high-pass filter and
  `ShakeListener`, with the 0.5 / 2.5 / 3.9 that `BehaviorSinging::InitInternal` passes, and
  `StreamObjectAccel` sent to turn a cube's stream on as `CubeAccelComponent::AddListener` does.
* **M9-019** no blend container in any shipped bank groups its children into blend tracks, so nothing a
  crossfade curve would have done is lost. Read from all six of them rather than the three that had been
  checked.
* **M6-004** resampling to the robot's rate no longer folds everything above 11160 Hz back into the band:
  it is a band-limited windowed sinc, where it had been nearest-sample decimation.
* **M6-003 corrected the other way.** The record claimed no robot event reaches a stereo ADPCM file.
  Eight do — the three effort grunts, the spark launch and the four scan sounds are silent — so it is a
  RECOVERABLE_GAP again and M6 is not source-complete. `wwise --coverage` now lists every event that
  resolves to media and can play none of it, which is what found it.

Resolved in this pass: M6-006, M6-007, M9-006 to M9-013, M9-017, M9-019.

## Blocked M9 items

* **M9-013** whether Wwise routes MIDI notes into the get-in branch. 41 extra voices on a 42-note song
  under the uniform rule; only a recording of the stock app singing, or a Wwise runtime, can settle it.
* **M9-024** whether the note-off envelope also stops the voice. Both readings agree the voice ends at
  note-off, which is what the renderer does.
* **M9-025** the waveform a Wwise LFO draws. Unreachable while the vibrato depth is 0.
* **M9-026** the coefficient formulas inside the parametric EQ and peak limiter. Their settings are exact.
* **M9-023** how the stock app sounded when it sang: hardware, or a recording of one.
