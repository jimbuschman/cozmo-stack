# Fidelity progress

Counts come from `re-analysis/fidelity_manifest.json`; the manifest and the commits are the record.

## Manifest counts

| status | records |
| --- | ---: |
| EXACT_SOURCE | 80 |
| EQUIVALENT_IMPLEMENTATION | 11 |
| RECOVERABLE_GAP | 69 |
| IMPLEMENTATION_GAP | 9 |
| COMPATIBILITY_POLICY | 15 |
| HARDWARE_ONLY | 3 |
| BLOCKED_EXTERNAL | 8 |
| **total** | **195** |

Remaining RECOVERABLE_GAP: **69**, all on a live execution path, **none of them in M9**.
Remaining IMPLEMENTATION_GAP: **9**, of which **8 are on a live path** and one (M9-021) is not.

`IMPLEMENTATION_GAP` was added in this pass. It is for a behaviour the original is known to perform,
from primary evidence, that the production code knowingly does not: unfinished fidelity work, with
nothing left to read. Nine records moved into it, each reviewed on what it actually says rather than
renamed in bulk — six out of COMPATIBILITY_POLICY, which had been absorbing work that was merely hard,
and three out of EQUIVALENT_IMPLEMENTATION, which had been absorbing substitutions nobody had shown to
be equivalent. Two records went the other way, into RECOVERABLE_GAP, because the original had not in
fact been read (M5-019, M8-008).

## Two gates, not one

A subsystem can have read everything the original has to say and still not have built it, so the
manifest asks two questions and the validator enforces both:

* **source investigation exhausted** — no RECOVERABLE_GAP on a live path. Nothing is left that reading
  the original would settle.
* **implementation fidelity complete** — no IMPLEMENTATION_GAP on a live path. Nothing the original is
  known to do is knowingly not done here.

Neither says the behaviour is faithfully reproduced. BLOCKED_EXTERNAL and HARDWARE_ONLY records survive
both, and `FIDELITY_GAPS.md` counts them per subsystem for that reason. The old single
`source_complete` flag is gone; a test fails if it comes back.

## Where M9 stands

* source investigation **exhausted**: no RECOVERABLE_GAP on the live path.
* implementation fidelity **complete**: no IMPLEMENTATION_GAP on the live path. The one M9 record that
  is an IMPLEMENTATION_GAP, M9-021, is off it — each of the three tempo events holds exactly one Play
  action, so singing never meets the reduction.
* **blocked externally: 6 records.** Every one is in the Wwise runtime, which does not ship in this
  package.
* **hardware validation pending: 1 record.** M9-023, a recording of the stock app singing.

So M9 is as far as this repository can take it offline, and that is not the same as reproduced.

## M9 items resolved since the previous commit

* **M9-016** the live vibrato architecture. A song is now rendered a block at a time as it plays, each
  block reading the parameters as they stand, with a per-voice read position so a note already sounding
  picks up a shake mid-note. Before this the whole song was rendered up front and cached, so the
  continuously posted parameter could not reach the audio at all and the test that claimed it did was
  setting the parameter before rendering. The 66 ms lead is this stack's choice, and it is small against
  the engine's own 467 ms: `UpdateAmountToSend` 0x0057C6F0 runs up to 14 audio frames ahead of the robot.
* **M9-015** each play of a song draws its recordings afresh, which the cache had frozen.
* **M9-024** corrected: the renderer ends a note at the held duration, not at the end of the current loop
  iteration, and `LoopedLength` says so.
* **M9-025** `live_path` corrected to true. The cube shake is measured and the vibrato now reaches a
  playing song, so any shake exercises the unknown LFO waveform.

## M9 records re-audited in this pass

Each non-EXACT_SOURCE record on M9's live path was checked against the shipped data and the production
code rather than against its own text. Four changed.

* **M9-014 velocity — EQUIVALENT_IMPLEMENTATION → BLOCKED_EXTERNAL.** The old record said velocity is
  ignored because nothing binds it, which is true of the data: of the 199 nodes under the MIDI target
  none carries a velocity range and the only binding of any kind is the vibrato modulator. But the songs
  *do* vary velocity — Aba Daba spans 119..127, Frere Jacques 104..116 — so whether the Wwise runtime
  applies a velocity-to-level mapping of its own is a real open question about a real difference, not a
  settled equivalence.
* **M9-020 the clip window.** The record said the rule was "barely exercised" because every clip has
  PlayAt 0 and BeginTrim 0. The start of the window is indeed never moved; the *end* of it discards
  1835 notes across the 83 music events, 762 in Singing_William_Tell alone, whose MIDI holds a
  full-length rendition the clip takes twelve seconds of. The genuinely uncertain half — what becomes of
  a note still held at the end — reaches exactly two notes in the whole product, and the segment ends at
  the same instant, so both readings sound the same. Measured, not assumed.
* **M9-018 the Stop action.** The ancestor half of the rule is a generalisation of this stack, and it is
  never exercised: the three tempo events play 914766641, 139286641 and 602865028, and the stop event
  targets exactly those three.
* **M9-011 the bus chain.** Now says which part is read (routing, chain order, every setting) and which
  is not (the arithmetic inside each effect, recorded separately as M9-026), with the measurement that
  replaced peak normalisation: the 39 songs leave the chain between 29867 and 31879 of 32767.

`--sampler` and `--validate-music` print these counts, so the numbers above are reproducible rather than
recorded once.

## Blocked M9 items

* **M9-013** whether Wwise routes MIDI notes into the get-in branch. 41 extra voices on a 42-note song
  under the uniform rule; only a recording of the stock app singing, or a Wwise runtime, can settle it.
* **M9-014** whether Wwise maps note velocity to level with nothing asking it to.
* **M9-022** the container dispatch rule, in particular whether a container filters its playlist before
  or after it picks.
* **M9-024** whether the note-off envelope also stops the voice. Both readings agree the voice ends at
  note-off, which is what the renderer does.
* **M9-025** the waveform a Wwise LFO draws. On the live path now that a shake reaches a playing song.
* **M9-026** the coefficient formulas inside the parametric EQ and peak limiter. Their settings are exact.
* **M9-023** how the stock app sounded when it sang: hardware, or a recording of one.

## Earlier in this pass

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
  RECOVERABLE_GAP again and M6 has not exhausted its source investigation. `wwise --coverage` now lists
  every event that resolves to media and can play none of it, which is what found it.

Resolved in this pass: M6-006, M6-007, M9-006 to M9-013, M9-015 to M9-020, M9-024, M9-025.
