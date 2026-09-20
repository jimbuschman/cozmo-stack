# M9 — Wwise switch-state audio: Cozmo sings

Status: **COMPLETE OFFLINE (2026-09-19); hardware acceptance PENDING.** Every one of the 39 shipped Singing
behaviours resolves through the banks to a song, renders to sound, and runs through the M8 framework the
way the engine's `BehaviorSinging` runs it. What is offline-verified and what still needs a robot are kept
apart in §"What is verified, and how" below; the robot run is item A of `HARDWARE_TEST_PLAN.md`.

This document has two parts: the chain as it was **recovered** (engine, decompiled enums, banks), and the
rules this stack **applies** to that data, each with its provenance class. Authority order throughout:
`libcozmoEngine.so` 3.4.0-1204, the decompiled Unity C#, the shipped OBB; Wwise's public documentation
only where the package itself cannot answer, and labelled as such.

## 1. What a Singing behaviour does (recovered)

### The engine (NATIVE)

`Anki::Cozmo::BehaviorSinging` is exported (`re-analysis/symbols`); its four methods:

| function | address | what it does |
| --- | --- | --- |
| constructor | 0x005EE8DC | reads `audioSwitchGroup` through `SwitchGroupTypeFromString` and `audioSwitch` through the group's own `FromString` (`Cozmo_Sings_80Bpm`, `_100Bpm`, `_120Bpm`, chosen by comparing the group id with 0xC8A59578, 0xE017E775, 0xB215BB17); picks the tempo animation trigger **0x1FF `Singing_80bpm`, 0x200 `Singing_100bpm`, 0x201 `Singing_120bpm`**; an unrecognised group falls back to the 80 bpm group with switch 0 |
| `InitInternal` | 0x005EEB30 | **first** `RobotAudioClient::PostRobotSwitchState(group, switch)` (0x005EEB4E); then `SmartDisableReactionsWithLock`; then, for every connected cube, a `ShakeListener` feeding a per-cube rolling average; then one `CompoundActionSequential` of three `TriggerAnimationAction`s: **0x202 `Singing_GetIn`, the tempo trigger, 0x203 `Singing_GetOut`** (num loops 1, interrupt running, 60 s timeout) |
| `UpdateInternal` | 0x005EF0C8 | takes the largest cube shake, divides by 3000, clamps to 0..1, and smooths it: `new = 0.5·old + 0.5·clamp`; posts it every tick as **`RobotAudioClient::PostRobotParameter(Cozmo_Singing_Vibrato = 0xC20F49DF, value)`**; logs `robot.song_shake_duration_ms` when a shake above 0.1 lasted over 500 ms |
| `StopInternal` | 0x005EF2B0 | posts `Cozmo_Singing_Vibrato = 0`, removes the cube listeners |

`RobotAudioClient::PostRobotSwitchState` (0x00599F94) and `PostRobotParameter` (0x00599F62) forward to the
audio engine client with game object 6 (`Cozmo_OnDevice`) or 7 (`CozmoBus_1`), the robot's own object.

The tempo animation groups (`assets/animationGroups/CozmoSings/`) hold one clip each
(`anim_cozmosings_80_song_01`, `_100_song_01`, `_120_song_01`); get-in has three clips, get-out one. Each
song clip raises **`Play__Robot_VO__Cozmo_Singing_{80,100,120}bpm` at 0 ms**, `Play__Robot_Sfx__Scrn_Happy`
near the end, and **`Stop__Robot_VO__Cozmo_Singing_Stop` at 12210 / 9834 / 8481 ms** (read from the clips
with `wwise --clip`). So the behaviour sets the switch, the animation's ordinary audio event plays the
song, and the animation's stop event ends it.

### The Unity enums (UNITY)

`Anki.AudioMetaData.SwitchState.SwitchGroupType`: `Cozmo_Sings_80Bpm = 0xC8A59578`, `_100Bpm = 0xE017E775`,
`_120Bpm = 0xB215BB17`; the three per-group enums list 14 + 17 + 13 = 44 songs (39 shipped as behaviours).
`Anki.AudioMetaData.GameParameter.ParameterType`: `Cozmo_Singing_Vibrato = 0xC20F49DF` (also `_Tremolo`,
`_Phaser`). Every value equals the FNV-1 hash of its name (`WwiseHash`), which is how a behaviour file's
strings reach the bank; the bank text tables do not list the Cozmo_Sings names.

### The banks (ASSET)

```
Play__Robot_VO__Cozmo_Singing_80bpm (3519620195)
  -> MusicSwitchContainer 914766641   tempo 80 (meter overrides), grid 12000 ms, argument Cozmo_Sings_80Bpm
       MidiTargetNode (property 56) = 110896138
       decision tree: root -> 15 leaves; key 0 -> Yankee Doodle, key <switch id> -> that song's playlist
  -> MusicPlaylistContainer 476453346 (Aba Daba): one continuous-sequence group, one segment, played once
  -> MusicSegment 426278014: 12000 ms
  -> MusicTrack 1072657690: one source, plugin 0x00100001 = Wwise MIDI, embedded in Cozmo.bnk
       clip: play at 0, trim 0 .. -276750 of 288750 ms -> 12000 ms
```

The same shape holds for the 100 bpm (139286641, grid 9600, 17 leaves) and 120 bpm (602865028, grid 8000,
14 leaves) containers and for all 39 behaviours: every `(audioSwitchGroup, audioSwitch)` pair is a key in
its container's tree, every leaf is a playlist of exactly one segment holding exactly one MIDI clip trimmed
to the segment's length. The 19 standalone `Play__Robot_VO__Singing_*` events target playlists in the same
family directly. Two songs, Bingo and Tisket Tasket, are full-length sequences (462 s); the tempo animation
raises `Stop__Robot_VO__Cozmo_Singing_Stop` at its end (9.8 s), and the scheduler ends the streaming song on
that Stop action (`AnimationScheduler.StartAudio`; corrected 2026-09-19, when a Stop still resolved to "no
PCM" and was skipped as a silent alternative, leaving the song streaming).

**The 46 "plugin blobs" M6 set aside are the songs.** `25 80 00 00 xx xx`: a big-endian SMF division,
0x2580 = **9600 ticks per beat**, then a little-endian float tempo, then a Standard MIDI File track
(delta-time varints, running status, channel messages, two end-of-track meta events, the later one the
end). `WwiseMidi` reads all 46 (11 to 803 notes, all on channel 0).

**Timing.** For all 46 tracks, end-of-track ticks / 9600 at the effective tempo equals the source duration
Wwise wrote into the clip, to the millisecond. The effective tempo is the nearest music node above the
clip whose meter overrides its parent's (`bMeterInfoFlag`): the playlist for 12 songs, the switch container
for 34. The header tempo fits 37 of 46 and the segment's stored 120 fits none. The **MIDI target** is
inherited the same way: from the three containers, or from the two root playlists and one segment that set
it themselves. Every shipped song has a target.

**Where the notes go.** Blend container 110896138 is a MIDI sampler built from Cozmo's own voice:

| node | contents |
| --- | --- |
| 110896138 (blend) | RTPC: modulator LFO 528935089 `cozmo_singing_vibrato_lfo` on **Pitch**; three children |
| 462443456 (blend, note-on layer) | 14 random containers, one per MIDI key **48..61** (`MidiKeyRangeMin = Max`, properties 49/50), each of **three Vorbis recordings** of Cozmo singing that note, with `Pitch` corrections of 30 or 100 cents and small `Volume` offsets; many sounds carry `Loop = 0` (loop until stopped); RTPC: envelope modulator 381606890 `cozmo_singing_note_off` on Volume |
| 774902407 (blend, note-off layer) | `MidiPlayOnNoteType = 2`, `Volume = -14 dB`, 14 more per-key containers |
| 403781184 (random) | the get-in phrases: sequence containers of the same per-key containers, targets of `Play__Robot_VO__Singing_Getin_1..3` |

`cozmo_singing_vibrato` (the cube shake) drives the depth of the pitch LFO across the whole target.

## 2. What the reader established, and how

`WwiseHierarchy` reads nine object types with the layout of bank version 120 and refuses any object it does
not consume to the last byte. Every field position was hand-decoded from real objects, then the reader was
run over the six shipped banks:

| type | objects | consumed exactly |
| --- | ---: | ---: |
| Sound | 2360 | 2360 |
| RandomSequenceContainer | 468 | 468 |
| SwitchContainer | 21 | 21 |
| ActorMixer | 31 | 31 |
| BlendContainer | 6 | 6 |
| MusicSegment | 209 | 209 |
| MusicTrack | 258 | 258 |
| MusicSwitchContainer | 14 | 14 |
| MusicPlaylistContainer | 123 | 123 |

Three details were decided by the data against the public description (32-bit state-group count; 30-byte
playlist items; a parameter size on source-plug-in Sounds). Two M6 corrections followed (`WWISE_AUDIO.md`
errata): SwitchContainer's parent is at 11 not 8, and the 46 Sounds M6 could not place are source
plug-ins. Measured on the same banks, resolved playable events go 615 → 618 and decodable stays 584.

The banks and their definition text files are read from inside `AudioAssets.zip` (955 names). The
`Cozmo_Sings` names are not in those tables and reach the bank through the hash.

## 3. Rendering a song (what this stack does with the data)

`WwiseSongRenderer` renders a resolved plan to mono PCM at 22320 Hz. The rules, and what each rests on:

| rule | class | basis |
| --- | --- | --- |
| a note plays the MIDI target; a blend or actor-mixer plays all children; a random container plays one child by weight, not repeating the last (its avoid-repeat count); a sequence container plays its next item; a sound plays | CORROBORATED | Audiokinetic's public documentation of MIDI playback; the containers' own fields |
| at each node the note is filtered by that node's key range (49/50) and velocity range (51/52) where set; a node without them passes everything | CORROBORATED | documentation; the shipped key containers carry min = max |
| a node whose play-on property (46) is 2 plays at note-off; inherited down the tree, default note-on | CORROBORATED | documentation; the note-off layer's property |
| `Loop = 0` loops while the note is held, then plays out its current iteration (break on note-off); no Loop property plays once; a finite count plays that many times | CORROBORATED | documentation; the blends' bit 5 (`bIsMidiBreakLoopOnNoteOff`) |
| Volume (dB) and Pitch (cents) properties summed down the path, applied as gain and a resampling ratio | CORROBORATED | the property semantics are Wwise's; the values are the bank's |
| MIDI note tracking off | INFERRED | no node in the target sets a root note; per-key containers cover one key each, so tracking would transpose recorded notes away from their pitch; the bit that would enable it sits on nodes whose parent does not override it |
| velocity ignored | ASSET | nothing in the target binds an RTPC to velocity |
| a clip plays its source from BeginTrim for its length, starting at PlayAt + BeginTrim; a note still held at the clip's end is released there | CORROBORATED | documentation; every shipped clip has PlayAt 0, BeginTrim 0 |
| the get-in branch (403781184) receives notes like any other child | INFERRED | uniform application of the rules above; under them it adds a recording only for keys its sequences reach, and nothing was special-cased to remove it. A runtime would settle whether Wwise excludes it |
| **output stage**: when the raw sum exceeds full scale the whole render is scaled so its peak sits at full scale; raw peak and gain are reported | **LOCAL_POLICY** | the raw sum exceeds full scale on every song (raw peaks 100k–150k, about +10 to +13 dB); the robot's bus carries a peak limiter and a master compressor (`Init.txt` effects 3743559935, 2313011259) whose parameters this build does not read; this stands in for them and says so |
| vibrato LFO (528935089) and note-off envelope (381606890) not applied | DEFERRED | modulator objects (types 21, 22) are read as raw property bundles only; their semantics were not settled, so nothing is guessed. Effect: notes sustain by looping with no release shaping, and cube shaking has no audible effect |
| switch state posted before the animations; get-in, tempo, get-out in order; vibrato formula | NATIVE | `BehaviorSinging` (§1) |
| the vibrato *input* (cube shake) not measured | LOCAL_POLICY | the engine uses a streamed cube accelerometer this stack does not receive; `SingingBehavior.ShakeInput` is left for a caller and the value is reported in the acceptance record as not driven |
| **the song is rendered whole, ahead of playback, on a worker** (`WwiseAudioSource.Prewarm`, started by `SingingBehavior` when it posts the switch) | LOCAL | Wwise streams a song; this stack renders it to PCM first. A 462 s sequence takes seconds to render, which on the scheduler thread stalled the animation timeline at the tempo clip's first audio frame; the prewarm runs during the get-in animation and `GetPcm` waits for an in-flight render rather than starting another (`PrewarmRendersOffTheStreamingPathAndGetPcmFindsIt`, `TheSingingStopEventEndsTheSongOnTheScheduler`) |
| **the final-PCM cache freezes the renderer's random choices** (which of a note's three recordings plays) for the life of a source | LOCAL_POLICY | Wwise draws afresh on every play; this stack caches the rendered PCM per selected node, so a song sounds the same every time in one session (`TheMusicCacheFreezesTheRenderersRandomChoices`). Kept: re-rendering per play would put seconds of work back on the streaming path |
| a Stop action (`Stop__Robot_VO__Cozmo_Singing_Stop`) ends the voices under its target; the scheduler ends the streaming song when the Stop's target is the song's Play target or an ancestor of it | CORROBORATED | Wwise action semantics; the hierarchy walk is the bank's parent chain (`WwiseAudioSource.IsStopEvent` / `StopAffects`) |

`WwiseAudioSource` implements `IAudioSwitchStates`: `SetSwitch(group, switch)` is what the behaviour calls,
and `GetPcm` for an event whose Play target is music renders the plan under the current switches (cached
by the node the tree selected). `SingingBehavior` is the M8 behaviour; `ShippedBehaviors.Singing(obb)`
builds the 39 from their configs.

## 4. What is verified, and how

### Offline-verified (tests and tools, run here)

* All 3490 hierarchy objects consume exactly (`EveryHierarchyObjectInTheShippedBanksConsumesExactly`).
* All 39 behaviours resolve to their own song, one MIDI clip exactly one segment long
  (`EverySingingBehaviourResolvesToOneMidiSegment`); all 46 MIDI sources decode at 9600 ticks per beat and
  their clip durations follow the effective tempo (`SongMidiSources…`).
* **Every one of the 39 songs renders**: full segment length, every note in the window sung (no note in
  any shipped song falls outside the voice's 48..61 range), a note-off per note, no clipping after the
  output stage, deterministic for a seed (`EveryShippedSongRendersCompletely`, `AbaDabaRendersTo…`).
  `wwise --validate-music --obb <dir>` renders the 39 plus every other music event: 82 render (the 39, the
  three tempo defaults, the 19 standalone songs, 26 Code Lab music pieces from Vorbis clips); the one that
  does not is `Play__Music__Play`, whose default path is a 1 s silent segment (the app's soundtrack, out of
  scope).
* The seam: `GetPcm` returns the default song without a switch, the selected song with one; the Stop event
  is recognised as a Stop action covering the song and ends it on the scheduler at the clip's end
  (`TheAudioSourcePlaysTheSongTheSwitchSelects`, `TheSingingStopEventEndsTheSongOnTheScheduler`). An earlier
  version of this line equated "the Stop event resolves to no PCM" with correct behaviour; it was not: the
  scheduler skipped the Stop and kept streaming.
* The behaviour posts the switch before any animation, holds reactions off, plays a get-in clip first, and
  stops cleanly with the vibrato reset (`StartingTheBehaviourPostsTheSwitchThenPlaysTheGetIn`); the
  engine's constructor table, fallback and vibrato formula are pinned by pure-function tests.
* A rendered song can be listened to on a PC: `wwise <sound-dir> --render Play__Robot_VO__Cozmo_Singing_80bpm
  --switch Cozmo_Sings_80Bpm=Cozmo_Sings_Aba_Daba --wav aba.wav`. That is an offline check of the sampler's
  rules by ear; it is not a claim about the robot.

### Hardware-pending

* That the robot plays the song at all through the animation path (the tempo clip's audio keyframe → the
  music event → the render), at an acceptable level, with the get-in and get-out around it. Command and
  human check: `HARDWARE_TEST_PLAN.md` item A. Nothing in M9 has been heard from a Cozmo speaker.
* How the sampler's rendering compares with what the stock app made the robot sing (sustain, release
  tails, level, the get-in branch). Only a stock-app recording would settle the CORROBORATED and INFERRED
  rows above; none is available.

## 5. Not done, and deliberately so

* The vibrato LFO, the note-off envelope, the bus limiter and compressor: their objects are read, their
  parameters are not interpreted. Recorded above as DEFERRED / LOCAL_POLICY.
* Cube-shake vibrato input: needs the cube accelerometer stream (M4 has movement reports only).
* Music.bnk's real music (`Play__Music__Play`, a four-argument tree of 269 nodes keyed by the `music`
  state group and `freeplay_mood` switch) resolves structurally and its Vorbis clips render; it is the
  app's soundtrack, not the robot's, and is out of M9's scope.
* `Play__Codelab__Music_Tiny_Orchestra_Init` fires nine Play actions; the plan takes the first.

## 6. Commands

```
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --hierarchy
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --music Play__Robot_VO__Cozmo_Singing_80bpm --switch Cozmo_Sings_80Bpm=Cozmo_Sings_Aba_Daba --midi
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --render Play__Robot_VO__Cozmo_Singing_80bpm --switch Cozmo_Sings_80Bpm=Cozmo_Sings_Aba_Daba --wav aba.wav
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --validate-music --obb <obb dir>
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --coverage
dotnet run --project src/Cozmo.Conformance -- sing 172.31.1.1 --obb <obb dir> --behavior Singing_AbaDaba --acceptance
```

`<sound-dir>` may be the OBB's `cozmo_resources/sound` alone; the archive holds the banks.
