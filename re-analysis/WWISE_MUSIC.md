# M9 — Wwise switch-state audio: the Cozmo_Sings chain

Status: **IN PROGRESS — increment 1 complete (2026-09-19): recovery and resolution.** The songs are located,
read and resolved end to end from the shipped banks; nothing is rendered to sound yet. Increment 2 is the
renderer. What is recovered, what is inferred and what is still open are separated below.

## What a Singing behaviour actually does

Everything in this section was read, not designed. Authority in order: `libcozmoEngine.so` 3.4.0-1204,
the decompiled Unity C#, the shipped OBB.

### The engine (NATIVE)

`Anki::Cozmo::BehaviorSinging` is exported (`re-analysis/symbols`), and its four methods say exactly what
the behaviour does:

| function | address | what it does |
| --- | --- | --- |
| constructor | 0x005EE8DC | reads `audioSwitchGroup` through `SwitchGroupTypeFromString` and `audioSwitch` through the group's own `FromString` (`Cozmo_Sings_80Bpm`, `_100Bpm`, `_120Bpm`, chosen by comparing the group id with 0xC8A59578, 0xE017E775, 0xB215BB17); picks the tempo animation trigger: **0x1FF `Singing_80bpm`, 0x200 `Singing_100bpm`, 0x201 `Singing_120bpm`**; an unrecognised group falls back to the 80 bpm group with switch 0 |
| `InitInternal` | 0x005EEB30 | **first** `RobotAudioClient::PostRobotSwitchState(group, switch)` (0x005EEB4E); then `SmartDisableReactionsWithLock`; then, for every connected cube, a `ShakeListener` feeding a per-cube rolling average; then one `CompoundActionSequential` of three `TriggerAnimationAction`s: **0x202 `Singing_GetIn`, the tempo trigger, 0x203 `Singing_GetOut`** (each with num loops 1, interrupt running, timeout 60 s) |
| `UpdateInternal` | 0x005EF0C8 | takes the largest cube shake, divides by 3000, clamps to 0..1, halves it, and smooths it into a state (`new = 0.5·old + 0.5·clamp`); posts it every tick as **`RobotAudioClient::PostRobotParameter(Cozmo_Singing_Vibrato = 0xC20F49DF, value)`**; logs a `robot.song_shake_duration_ms` DAS event when a shake above 0.1 lasted more than 500 ms |
| `StopInternal` | 0x005EF2B0 | posts `Cozmo_Singing_Vibrato = 0`, removes the cube listeners |

`RobotAudioClient::PostRobotSwitchState` (0x00599F94) and `PostRobotParameter` (0x00599F62) forward to the
audio engine client with game object 6 (`Cozmo_OnDevice`) or 7 (`CozmoBus_1`) depending on the client's
mode byte — the robot's own game object, so the switch applies to what the robot plays.

The three tempo animation groups (`assets/animationGroups/CozmoSings/`) each hold one clip:
`anim_cozmosings_80_song_01`, `_100_song_01`, `_120_song_01`; get-in has three clips, get-out one. The song
clips' audio keyframes raise `Play__Robot_VO__Cozmo_Singing_80bpm` / `_100bpm` / `_120bpm`. So the
behaviour sets the switch, and the animation's ordinary audio event does the rest.

### The Unity enums (UNITY)

`Anki.AudioMetaData.SwitchState.SwitchGroupType` carries `Cozmo_Sings_80Bpm = 0xC8A59578`,
`Cozmo_Sings_100Bpm = 0xE017E775`, `Cozmo_Sings_120Bpm = 0xB215BB17`; the three per-group enums list the
songs (14 + 17 + 13 = 44 switch values; 39 behaviours use them, the rest are unshipped songs).
`Anki.AudioMetaData.GameParameter.ParameterType` has `Cozmo_Singing_Vibrato = 0xC20F49DF`,
`Cozmo_Singing_Tremolo`, `Cozmo_Singing_Phaser`. Every one of these equals the FNV-1 hash of its name
(`WwiseHash`), which is how a name from a behaviour file reaches the bank.

### The banks (ASSET)

Read with the hierarchy reader below and cross-checked object by object:

```
Play__Robot_VO__Cozmo_Singing_80bpm (3519620195)
  -> MusicSwitchContainer 914766641   tempo 80, grid 12000 ms, argument Cozmo_Sings_80Bpm
       MidiTargetNode (property 56) = 110896138
       decision tree: root -> 15 leaves; key 0 -> Yankee Doodle, key <switch id> -> that song's playlist
  -> MusicPlaylistContainer, e.g. 476453346 (Aba Daba): one sequence group of one segment
  -> MusicSegment 426278014: 12000 ms
  -> MusicTrack 1072657690: one source, plugin 0x00100001 = Wwise MIDI, 130..5831 bytes embedded in Cozmo.bnk
       clip: play at 0, trim 0 .. -276750 of 288750 ms  -> 12000 ms
```

The same shape holds for all three containers (100 bpm: 139286641, grid 9600, 17 leaves; 120 bpm:
602865028, grid 8000, 14 leaves) and for all 39 behaviours: every `(audioSwitchGroup, audioSwitch)` pair is a
key in its container's tree, and every leaf is a playlist of exactly one segment holding exactly one MIDI
clip trimmed to exactly the segment's length (`WwiseMusicTests.EverySingingBehaviourResolvesToOneMidiSegment`).
The 19 standalone `Play__Robot_VO__Singing_*` events (Happy Birthday, Row Your Boat, ...) target playlists
in the same set directly.

**The 46 "plugin blobs" M6 set aside are the songs.** They begin `25 80 00 00 xx xx`: a big-endian SMF
division, 0x2580 = **9600 ticks per beat**, then a little-endian float tempo, then a Standard MIDI File
track (delta-time varints, running status, channel messages, meta events). `WwiseMidi` reads them; all 46
parse, with 11 to 803 notes each, all on channel 0.

**Timing.** Two facts were settled against the data rather than assumed. The division is 9600: for all 46
tracks, end-of-track ticks / 9600 at the effective tempo equals the source duration Wwise wrote into the
clip, to the millisecond. The effective tempo is the nearest music node above the clip whose meter
overrides its parent's (the bank's `bMeterInfoFlag`): the playlist for 12 songs (160, 200, 240, 90 and 100
bpm), the switch container for the other 34 (80, 100, 120). The tempo in the file header fits only 37 of 46
and the segment's stored 120 fits none, so neither is what Wwise used
(`SongMidiSourcesUseNineThousandSixHundredTicksPerBeatAtTheEffectiveHierarchyTempo`). Each song therefore
plays as one segment of 8, 9.6 or 12 s — four bars at its tempo — cut from a longer sequence.

**Where the notes go.** The containers' MIDI target, blend container 110896138, is a MIDI sampler built
from Cozmo's own voice:

| node | what it is |
| --- | --- |
| 110896138 (blend) | RTPC: modulator LFO 528935089 `cozmo_singing_vibrato_lfo` on **Pitch** (property 2). Three children below |
| 462443456 (blend) | the note-on layer: 14 random containers, one per MIDI key **48..61** (`MidiKeyRangeMin = Max`, property 49/50, integers), each holding **three Vorbis recordings** of Cozmo singing that note, some with `Pitch` corrections of 30 or 100 cents and `Volume` offsets; many sounds carry `Loop = 0` (loop until stopped). Its RTPC is envelope modulator 381606890 `cozmo_singing_note_off` on Volume |
| 774902407 (blend) | the note-off layer: `MidiPlayOnNoteType = 2` (plays at note-off), `Volume = -14 dB`, 14 more per-key containers |
| 403781184 (random) | the get-in phrases: three sequence containers the `Play__Robot_VO__Singing_Getin_1..3` events target; not MIDI |

So a note plays one of three recordings of that pitch, sustains by looping, and stops at note-off with a
quiet tail; notes outside 48..61 have no container and play nothing. `cozmo_singing_vibrato` (the cube
shake) drives the depth of a pitch LFO across the whole target. These are the bank's contents; how Wwise
acts on them is the next section's business.

## What the reader established, and how

`WwiseHierarchy` reads nine object types with the layout of bank version 120 — the shared node block
(routing, parent, property bundle, positioning, aux, advanced settings, state groups, RTPCs) and each type's
own fields — and refuses any object it does not consume to the last byte. Every field position was
hand-decoded from real objects first, then the reader was run over the six shipped banks:

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

Three details were decided by the data against the public description: the state-group count is 32 bits
wide in this version; a playlist item is 30 bytes with the loop fields before the weight; and a Sound whose
plugin is a source plug-in (low nibble 2: Wwise Sine, Silence, Anki Wave Portal) carries a four-byte
parameter size before its node block.

**Two M6 corrections follow** (recorded in `WWISE_AUDIO.md`): the parent of a SwitchContainer is at offset
11, not the 8 M6's scan settled on (8 read the bus id); and the 46 Sounds M6 could not place are the
source plug-ins. Neither changes a decodable event: coverage of the 705 playable events moves from 615 to
618 resolved, the three gained being events whose only sources are Wave Portal or Silence plug-ins (no
media to decode), and the music events now counted separately: **43 reach a clip through the music
hierarchy** on the default switch path.

The banks and their definition text files are read from inside `AudioAssets.zip` as well as from loose
files; the shipped archive is a complete library on its own. The text files (`Cozmo.txt`, `Init.txt`, ...)
give 955 names — switch groups, switches, state groups, states, game parameters, buses — which is where
`cozmo_singing_vibrato`, `cozmo_singing_vibrato_lfo` and `cozmo_singing_note_off` were read. The
`Cozmo_Sings` groups themselves are not in those tables; they reach the bank through the hash.

## Provenance of the resolver's rules

| decision | class | basis |
| --- | --- | --- |
| switch posted before the animations; the three triggers and their order; the vibrato parameter and its formula | NATIVE | `BehaviorSinging` disassembly above |
| group and switch ids from names | UNITY, confirmed by hash | enums; `WwiseHash` |
| every field of the nine node types | ASSET | exact consumption, 3490 of 3490 |
| 9600 ticks per beat; effective tempo by meter override | ASSET | 46 of 46 clip durations |
| decision tree: take the child keyed by the group's value, else the child keyed 0 | INFERRED | the smallest reading of the data: every shipped tree has a key-0 child first; a runtime would be needed to see, for example, whether Wwise weights or re-evaluates. Reported in the plan; not silently defaulted |
| sequence groups play children in order, repeated by loop count; random groups report one alternative and flag it | INFERRED | public description; the singing playlists are all one sequence group of one segment, so nothing shipped exercises the rest |
| MIDI target chooses a child by key range; loops until note-off; note-off layer plays at note-off | not yet implemented | Wwise runtime behaviour, to be labelled CORROBORATED (public documentation) when increment 2 builds it; the vibrato LFO and note-off envelope are bank data whose runtime effect is not reproduced |

## Not done, and deliberately so

* **No sound is produced yet.** Increment 2: play a MIDI clip through the sampler (per-key containers,
  three alternatives, loop until note-off, note-off layer), at the effective tempo, into `WwiseAudioSource`
  behind a switch-state API, and give M8 a `Singing` behaviour that does what `BehaviorSinging` does.
* The vibrato LFO (`528935089`) and note-off envelope (`381606890`) are modulator objects (types 21 and
  22) whose fields are not yet read; their effect will be recorded as deferred unless read.
* Music.bnk's real music (Vorbis tracks under `Play__Music__Play`, a four-argument tree of 269 nodes keyed
  by the `music` state group and the `freeplay_mood` switch) resolves structurally but is out of M9's
  scope; it is the app's soundtrack, not the robot's.
* `Play__Codelab__Music_Tiny_Orchestra_Init` fires nine Play actions at nine containers; the plan takes
  the first. Code Lab is out of scope.

## Commands

```
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --hierarchy
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --music Play__Robot_VO__Cozmo_Singing_80bpm --switch Cozmo_Sings_80Bpm=Cozmo_Sings_Aba_Daba --midi
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --coverage
```

`<sound-dir>` may now be the OBB's `cozmo_resources/sound` alone (the archive holds the banks).
