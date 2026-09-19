# Handoff — 2026-09-19 (M9 increment 1 complete)

## Where things stand

| | |
| --- | --- |
| Sweep commits | `dbdfa29`..`d483fd2` (five commits, pushed) — see [SOURCE_FIDELITY_AUDIT.md](SOURCE_FIDELITY_AUDIT.md) |
| Hardware retests | **both passed visually on 2026-09-19** (audit §8); M5 and M7 re-verified, errata cleared |
| Reconciliation | `0e74c9a`, `7166568` (pushed) — audit §10 |
| M9 | **IN PROGRESS — increment 1 complete**: the Cozmo_Sings chain recovered and resolved from engine, enums and banks; no sound rendered yet. See [WWISE_MUSIC.md](WWISE_MUSIC.md) |
| Tests | **462 passed, 0 failed (`dotnet test Cozmo.sln`, 3 m 31 s; 444 before M9)**, all passing offline |

## Milestone status

| Milestone | Status |
| --- | --- |
| M1 transport core | Frozen. Audited; nothing changed. |
| M2 protocol catalogue | Frozen. `animHeadAngle` (0x93) and `animLiftHeight` (0x94) carry engine-derived field names and are statically verified. The hand-written `RobotState` helpers now read `liftAngle` as radians and convert to millimetres with the engine's `45 + 66 sin(angle)` (D10). |
| M3 device layer | Frozen. Audio sample rate 22320 Hz (engine `AnimConstants`); JPEG headers native. |
| M4 control layer | Frozen. Head and lift limits native. `Sensors.LiftPositionRaw` became `LiftAngleRad` + `LiftHeightMm`. **Cubes:** hardware discovery observed on 2026-09-19 (a real cube appeared during discovery after being tapped); connection, tap, movement, up-axis and battery telemetry acceptance still pending. |
| M5 animation and expression | **Frozen, hardware re-verified 2026-09-19** (`anim_bored_01 --wwise`, visual). Reconciliation: the scheduler's timeline is now a count of streamed frames × 33 ms as in the engine, frozen while the robot has no room and caught up frame by frame (D11); a second clip is refused while anything streams unless it interrupts, as `SetStreamingAnimation` does (D12). Both offline-verified; neither changes a normally paced stream. |
| M6 Wwise audio | Frozen. Resampling target 22320 Hz; covered by the M5 retest. |
| M7 reactive behaviour and idle | **Frozen, hardware re-verified 2026-09-19** (`behavior --seconds 60`, visual): shipped resting face, engine blink table, `LookAt` dart geometry, `ReactToImpact` on landing. |
| M8 behaviour inventory and framework | Complete offline. Audited; nothing changed. |
| M9 Wwise switch-state audio | **IN PROGRESS.** Increment 1: `WwiseHierarchy` reads all nine node types with exact consumption (3490 of 3490 objects); music switch trees, playlists, segments, tracks and the 46 MIDI song sources are read; every one of the 39 Singing behaviours resolves to its song; `BehaviorSinging` disassembled (switch first, then GetIn / tempo / GetOut triggers, cube-shake vibrato parameter). Increment 2 (next): render a song through the per-note vocal sampler and wire the behaviour. |

## What the sweep and reconciliation found, in one paragraph

Nine confirmed divergences in the sweep (audit §1) and three in the reconciliation (audit §10), all in places
where a plausible value or mechanism had been chosen and had passed acceptance because acceptance judged
the code against itself: the sample rate, the falling reaction's animation, the blink, the dart geometry,
the audio-alternative choice, head and lift keyframes as motor commands, the resting face, a claim that a
shipped artifact did not exist; then the lift unit, a wall-clock animation timeline that jumped after
stalls, and a second-clip rule the engine does not have. Every fix carries a regression that names the old
behaviour. The INFERRED rows of the audit's §5 remain the open list; §2 says what would settle each.

## Hardware

Both post-sweep retests passed visually (operator report; no JSON record committed):

```
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --name anim_bored_01 --wwise <obb dir>
dotnet run --project src/Cozmo.Conformance -- behavior 172.31.1.1 --obb <dir> --seconds 60
```

Not yet on hardware: the reconciliation's scheduler changes (D11, D12: only visible under a stall or a late
tick), the lift-height readout (D10: `control 172.31.1.1` now prints `lift=<rad>rad/<mm>mm`; with the lift
down expect about −0.198 rad / 32 mm), the falling → impact reaction (D9), and `cubes --acceptance`.

Deferred and unchanged: full cube telemetry acceptance, enhanced backpack-light keyframes, pre-rendered
`faceAnimations`, group cooldown enforcement (mechanism recorded in `AnimationLibrary.cs`), lift 0 mm
semantics, the seven stereo ADPCM files, the music-hierarchy events.

## Open unknowns carried forward

From the audit's §2, the ones most worth a bounded look next:

1. **Eye-dart lifecycle** — ramp-then-hold or snap-then-hold; static reading of the persistent-layer
   replay is ambiguous. A stock-app face capture settles it.
2. **Scanline parity** — transmitted as the position of the set bit in each run's two draw bits; whether
   the firmware uses it is not established.
3. The renderer's corner-radius assignment and fill rule, unchanged.
4. **Frames per engine update** — the engine streams to the audio budget on every update regardless of the
   clock; this stack streams one frame per 33 ms tick and makes up late ticks. Recorded as local policy; a
   wire capture of the stock app's burst pattern would show whether it matters on the robot.

## Next task

**M9 increment 2: make a song audible.** Read `WWISE_MUSIC.md` first; everything below is already recovered
there and must not be re-derived.

1. A MIDI sampler over the target blend container 110896138: for each note (from `WwiseMidi.NotesAt` at
   the segment's effective tempo) pick the note-on child whose `MidiKeyRangeMin..Max` holds the key, choose
   one of its three recordings as `RandomSequenceContainer` does, apply its `Pitch` (cents) and `Volume`
   (dB) properties, loop the recording while the note is held when `Loop = 0`, stop at note-off, and play
   the note-off layer (`MidiPlayOnNoteType = 2`, -14 dB). Mix at `CozmoAudio.SampleRate`. Label the
   dispatch rules CORROBORATED (public Wwise documentation), not NATIVE.
2. A switch-state API on `WwiseAudioSource` (`SetSwitch(group, switch)`) so an audio keyframe whose event
   targets a music switch container renders the selected song; the three tempo events are the only such
   events an animation raises.
3. An M8 `Singing` behaviour following `BehaviorSinging`: post the switch, play `Singing_GetIn`, the tempo
   trigger, `Singing_GetOut`; drive `Cozmo_Singing_Vibrato` from cube shake only if cubes are available,
   otherwise leave it at 0 and say so.
4. Decide, and record, what to do about the vibrato LFO and note-off envelope modulators (types 21, 22):
   read their fields or defer them explicitly.

Hardware acceptance for M9 is a `behavior` run that sings one song audibly on the robot.
