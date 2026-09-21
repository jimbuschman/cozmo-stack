# M9 — Wwise switch-state audio: Cozmo sings

Status, as of the 2026-09-20 fidelity pass and its re-audit, in the four terms the fidelity gate uses
rather than one word:

* **source investigation exhausted** — nothing on M9's live execution path is a RECOVERABLE_GAP;
* **implementation fidelity complete** — nothing on it is an IMPLEMENTATION_GAP either. The one M9
  record that is, M9-021, is off the live path: each tempo event holds exactly one Play action;
* **six records blocked externally** — every one of them inside the Wwise runtime, which does not ship
  in this package;
* **hardware validation pending** — M9-023, a recording of the stock app singing. The robot run is item
  A of `HARDWARE_TEST_PLAN.md`.

That is as far as this repository can take M9 offline, and it is not the same as saying the singing is
faithfully reproduced. The authoritative list is [`fidelity_manifest.json`](fidelity_manifest.json),
rendered as [`FIDELITY_GAPS.md`](FIDELITY_GAPS.md).

> **What the 2026-09-20 pass changed, and why the document below is not what it was.** The pass found one
> fault that made every rendered song unlistenable and several claims that were wrong:
>
> * **A sung note played its whole recording.** Every recording under the note-on layer is a sustained
>   vowel of 4.2 to 7.4 seconds that loops until stopped, and the notes in the songs are 125 ms to about a
>   second. The renderer let a note play out a whole iteration after release, so a 187 ms note sounded for
>   5.79 seconds, thirty of them overlapped, and the sum ran 13 dB over full scale. A note now sounds for
>   as long as it is held; Aba Daba's raw peak went from 149572 to 36984.
> * **The output stage was a local peak normalisation.** It is now the effect chain the robot's own bus
>   carries, read from `Init.bnk` on the bus the engine's own registration table names - see section 6.
> * **The modulators were not read at all.** They are now, and what they do is measured rather than
>   guessed: the note-off envelope's whole authority over the level is one decibel, and the vibrato is
>   silent until a cube is shaken because the shake drives its *depth*.
> * **The cube shake was not measured.** It is now, from the cube accelerometer stream.
> * **An event's Play target was flattened** to every Sound beneath it and the first that decoded was
>   played. The get-in, which is a random of three two-note phrases each drawn from three takes, came out
>   as one fixed syllable.
> * **A claim that was simply wrong:** the master compressor is not on the robot's path. It is on
>   `Cozmo_Robot_External`, the bus for the app's spoken text.

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
| a note plays the MIDI target; a blend or actor-mixer plays all children; a random container plays one child by weight, not repeating the last; a sequence container plays its next item, or its whole playlist when its play mode is continuous; a sound plays | CORROBORATED | Audiokinetic's public documentation of MIDI playback; the containers' own fields, including the play-mode bit, which `WwisePlayback` reads the same way for an ordinary event play |
| at each node the note is filtered by that node's key range (49/50) and velocity range (51/52) where set; a node without them passes everything | CORROBORATED | documentation; the shipped key containers carry min = max |
| a node whose play-on property (46) is 2 plays at note-off; inherited down the tree, default note-on | CORROBORATED | documentation; the note-off layer's property |
| **a sound with `Loop = 0` sounds for exactly as long as the note is held**, looping if the note outlasts the recording; no Loop property plays once; a finite count plays that many times | **ASSET** | all 42 note-on recordings are 4.2–7.4 s sustained vowels carrying `Loop = 0`; all 42 note-off recordings are 0.34–0.65 s at −14 dB and play at note-off; the note layer carries the break-on-note-off bit; and every note in every shipped song is shorter than the shortest sustain recording. A release tail is only a release tail if the sustain it follows has stopped. `wwise --sampler` prints all three |
| Volume (dB) and Pitch (cents) properties summed down the path, applied as gain and a resampling ratio | CORROBORATED | the property semantics are Wwise's; the values are the bank's |
| **every modulator bound to a node on the path is evaluated and mapped through that binding's curve onto the property it drives** | ASSET | `cozmo_singing_note_off` (381606890) → Volume on the note-on layer over 0 to **−1 dB**; `cozmo_singing_vibrato_lfo` (528935089, 5.5 Hz, 0.2 s attack) → Pitch on the MIDI target over 0 to **580 cents**, with its depth driven by the `cozmo_singing_vibrato` game parameter over 0 to 100 %. Neither target node sets the property its modulator drives, so no accumulation question arises. One decibel is the whole of the note-off envelope's authority over the level; at its shipped sustain level of 9.5 % it moves a note by 0.095 dB |
| **MIDI note tracking is off** | ASSET | not from the absence of a root note, although none is set anywhere, but from the node bit vectors: across all six banks only 0x00, 0x01 and 0x24 occur, 0x01 sits on exactly the three nodes that set Priority, and 0x24 on exactly the two singing note layers, where both bits are needed for the note-off layer to sound and for a looping note to end. No bit is left |
| velocity ignored | ASSET | nothing in the target binds an RTPC to velocity, and only one velocity layer of recordings ships (`*_Vel2_*`) |
| a clip plays its source from BeginTrim for its length, starting at PlayAt + BeginTrim; a note still held at the clip's end is released there | CORROBORATED | documentation; every shipped clip has PlayAt 0, BeginTrim 0 |
| **output stage**: the effect chain the robot's own bus carries - see section 6 | ASSET settings, LOCAL arithmetic | the settings are `Init.bnk`'s and the routing is the engine's; the biquad and limiter formulas between them are standard, because the Wwise runtime does not ship |
| **the get-in branch (403781184) receives notes like any other child of the MIDI target** | **BLOCKED_EXTERNAL** | it is a child of the target and carries no filter of its own, and four of the eighteen containers under it carry no key range either, so under the container rules a note reaches it: measured on Aba Daba, 41 extra voices on a 42-note song. Whether Wwise's MIDI dispatch really routes notes there is runtime behaviour and no Wwise runtime ships. Every render reports the voices each branch contributed, and `--without-branch get-in` renders the other reading so the two can be heard side by side. Only a recording of the stock app singing can settle it (manifest M9-013) |
| switch state posted before the animations; get-in, tempo, get-out in order; vibrato formula | NATIVE | `BehaviorSinging` (§1) |
| **the cube shake is measured** and posted as the game parameter | NATIVE | section 7 |
| **the song is rendered whole, ahead of playback, on a worker** (`WwiseAudioSource.Prewarm`) | LOCAL | Wwise streams a song; this stack renders it to PCM first. A 462 s sequence takes seconds, which on the scheduler thread stalled the animation timeline; the prewarm runs during the get-in animation and `GetPcm` waits for an in-flight render rather than starting another. One consequence is that a song is rendered at the vibrato value in force when it starts, where Wwise would follow the parameter continuously |
| **the final-PCM cache freezes the renderer's random choices** for the life of a music source | LOCAL_POLICY | Wwise draws afresh on every play; this stack caches the rendered PCM per selected node. Kept: re-rendering per play would put seconds of work back on the streaming path. **Ordinary (non-music) events are no longer cached this way** — only their decoded media are — so a voice line draws afresh each time, as Wwise does |
| a Stop action ends the voices under its target; the scheduler ends the streaming song when the Stop's target is the song's Play target or an ancestor | CORROBORATED | Wwise action semantics; the hierarchy walk is the bank's parent chain |

`WwiseAudioSource` implements `IAudioSwitchStates`: `SetSwitch(group, switch)` and `SetParameter(id, value)`
are what the behaviour calls, and `GetPcm` renders the plan under the current switches (music) or walks the
containers under the Play target (everything else). `SingingBehavior` is the M8 behaviour;
`ShippedBehaviors.Singing(obb)` builds the 39 from their configs.

## 4. What is verified, and how

### Offline-verified (tests and tools, run here)

* All 3604 hierarchy objects of fourteen types consume exactly, buses, effects and modulators included
  (`EveryHierarchyObjectInTheShippedBanksConsumesExactly`). The fifteen buses are what settled the two
  variable parts of a bus's layout, and the 89 effects carry the plug-in ids `PluginInfo.xml` lists.
* All 39 behaviours resolve to their own song, one MIDI clip exactly one segment long
  (`EverySingingBehaviourResolvesToOneMidiSegment`); all 46 MIDI sources decode at 9600 ticks per beat and
  their clip durations follow the effective tempo (`SongMidiSources…`).
* **Every one of the 39 songs renders**: full segment length, every note in the window sung (no note in
  any shipped song falls outside the voice's 48..61 range), a note-off per note, no clipping after the
  bus chain, deterministic for a seed (`EveryShippedSongRendersCompletely`, `AbaDabaRendersTo…`), and
  every one leaves the chain between 30000 and 32000 of full scale with 2.5 to 7.3 dB of limiting
  (`EverySongLeavesTheChainAtAboutTheSameLevel`) — which is the point of a bus limiter, and the thing the
  local peak normalisation could not have got right.
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

## 5. What is left, and what it is

Nothing on M9's live path is a RECOVERABLE_GAP. What remains is of two kinds.

**In the Wwise runtime, which does not ship in this package.** Verified, not assumed: no `AkSoundEngine`,
`CAk*`, `AkModulator` or `Wwise` string occurs in `libcozmoEngine.so`, `libunity.so` or `libmain.so`, and
there is no separate Wwise library in the APK.

* **M9-013, the get-in branch.** Whether Wwise's MIDI dispatch routes notes into a child of the MIDI target
  that carries no filter of its own. Measured both ways; see section 3. This is the largest remaining doubt about
  how a rendered song sounds.
* **M9-024, modulator property 15.** Set on `cozmo_singing_note_off` and on no other of the eleven
  modulators, reading 2. Both readings that fit the numbering agree on what a listener hears — the voice
  ends when the note is released — which is what the renderer does.
* **M9-025, the waveform an LFO draws.** Unreachable while the vibrato depth is 0, which it is unless a
  cube is being shaken.
* **M9-026, the coefficient formulas** inside the parametric EQ and the peak limiter. Their settings are
  exact; the arithmetic between them is standard rather than Audiokinetic's.

**Needs a robot, or a recording of one.**

* **M9-023.** How the stock app actually sounded when it sang. No recording exists, and nothing offline can
  stand in for one.

**Out of scope, unchanged.** `Music.bnk`'s real music (`Play__Music__Play`, a four-argument tree of 269
nodes keyed by the `music` state group and `freeplay_mood` switch) resolves structurally and its Vorbis
clips render; it is the app's soundtrack, not the robot's.
`Play__Codelab__Music_Tiny_Orchestra_Init` fires nine Play actions and the plan takes the first.

## 6. The robot's bus chain

What the robot hears is not what the sampler sums. `RobotAudioClient`'s constructor (0x005994A0) registers
five robot audio buffers, each with a game object, an Anki Hijack plug-in index and a bus
(0x0059962A..0x0059966A):

| game object | plug-in | bus |
| --- | --- | --- |
| 7 | 1 | 2678428988 `Robot_Bus_1` |
| 8 | 2 | 2678428991 `Robot_Bus_2` |
| 9 | 3 | 2678428990 `Robot_Bus_3` |
| 10 | 4 | 2678428985 `Robot_Bus_4` |
| 6 | 0 | 0 — `Cozmo_OnDevice`, which plays on the phone |

Each of those four buses carries the same three effects followed by its own Anki Hijack, and that hijack's
one parameter is the same index the engine passed — so the bank and the binary agree on the routing from
two directions. A singing behaviour posts on game object 7, so a song leaves through `Robot_Bus_1`:

| effect | settings, from `Init.bnk` |
| --- | --- |
| `Robot_Bus_Eq_MasterCurve` | low shelf +2 dB at 835 Hz Q 2.1; peaking −2.5 dB at 1359 Hz Q 4.2; peaking −4 dB at 5091 Hz Q 1.5; output +1.5 dB |
| `Robot_Bus_Eq_HiLowPass` | high pass 333 Hz Q 1; a peaking band at 1000 Hz switched off; low pass 14298 Hz Q 1 |
| `Robot_Bus_Peak_Limiter` | threshold −1 dB, ratio 10.8, look-ahead 9 ms, release 41 ms |
| `Anki Hijack (Custom)` | parameter 1: the tap that sends this bus to robot 1 |

The low pass at 14298 Hz is above Nyquist for the robot's 22320 Hz, so it cannot act — reported as a note
rather than applied at a frequency it cannot have, and it could not have acted in the engine either, which
runs the same rate. `Cozmo_Voice_Master_Compressor` is **not** on this path: it is on
`Cozmo_Robot_External`, the bus for the app's spoken text, and `Cozmo_Robot`, the bus the sampler's
actor-mixer routes to, carries no effects at all.

## 7. The cube shake

`BehaviorSinging::InitInternal` puts a `ShakeListener` on every connected cube (0x005EECF4..0x005EED08)
with **0.5, 2.5 and 3.9**, and `CubeAccelComponent::AddListener` (0x0063547E) turns that cube's
accelerometer stream on by sending `StreamObjectAccel` (0x00635562). What arrives is `ObjectAccel` (0xF5,
twenty bytes: timestamp, object id, three floats).

* `HighPassFilterListener::UpdateInternal` (0x00636598), per axis: `y = a · (y + x − xPrevious)`, then
  `xPrevious = x`. A constant decays away, so gravity is invisible to it.
* `ShakeListener` (constructor 0x00636620, update 0x0063679E) squares both thresholds and compares them
  against **x² + y² + z²** of the filtered value: the higher one to start shaking, the lower one to keep
  it. The callback is handed that same squared magnitude, on every sample while the cube counts as
  shaking.
* `UpdateInternal` (0x005EF0C8) takes the largest of them, divides by 3000, clamps to 0..1 and smooths
  half and half. So the vibrato saturates at a filtered magnitude of about 55, against a start threshold
  of 3.9 — the effect is for shaking, not for handling.

## 8. Commands

```
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --hierarchy
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --sampler
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --event Play__Robot_VO__Singing_Getin_1
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --music Play__Robot_VO__Cozmo_Singing_80bpm --switch Cozmo_Sings_80Bpm=Cozmo_Sings_Aba_Daba --midi
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --render Play__Robot_VO__Cozmo_Singing_80bpm --switch Cozmo_Sings_80Bpm=Cozmo_Sings_Aba_Daba --wav aba.wav
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --render Play__Robot_VO__Cozmo_Singing_80bpm --switch Cozmo_Sings_80Bpm=Cozmo_Sings_Aba_Daba --without-branch get-in --wav aba-no-getin.wav
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --validate-music --obb <obb dir>
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --coverage
dotnet run --project src/Cozmo.Conformance -- sing 172.31.1.1 --obb <obb dir> --behavior Singing_AbaDaba --acceptance
```

`<sound-dir>` may be the OBB's `cozmo_resources/sound` alone; the archive holds the banks.
