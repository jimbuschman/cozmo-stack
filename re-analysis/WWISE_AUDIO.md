# M6 — Cozmo's original sound assets

Status: **event resolution complete; ADPCM decoded; Wwise Vorbis blocked on a decision, not on analysis.**

The chain from an animation's audio event id to a media file is fully decoded and verified across the whole
shipped library. Of the two codecs those files use, one is decoded here and one is not, for a reason set out
under "The Vorbis boundary" below. Nothing is substituted or faked: an event that cannot be produced returns
null and is named.

## There is no engine to check against

Every layer up to M5 was settled by reading `libcozmoEngine.so`. That is not possible here. The binary
contains **no occurrence** of `Wwise`, `BKHD`, `AkVorbis`, `CAkSound`, `vorbis`, `AK::`, `SoundEngine` or
`HIRC`, and no `libAkSoundEngine.so` ships in the APK. `Anki::AudioEngine` is Anki's wrapper around a Wwise
runtime that is not in this package.

So M6 rests on the public description of the bank format plus **cross-checks against the shipped assets**,
which is weaker evidence than a decompiled implementation and is called out as such wherever it matters. The
cross-checks are strong enough to be conclusive:

| check | result |
| --- | --- |
| all six banks parse, chunk lengths consuming each file exactly | yes |
| each bank's object list consumes its HIRC chunk exactly | yes |
| type-4 object count equals that bank's `IncludedEvents` count in SoundbanksInfo.xml | 546, 171, 83, 21, 14, 0 — all six match |
| every event parses with a 32-bit action count landing exactly on the payload end | 835 of 835 |
| action references resolve to the EventAction objects present | 906 references, 906 objects |
| the media id read from a Sound object names a file that exists | 2282 of 2360 |
| decoded durations are plausible for what the event is named | yes, e.g. 0.89–1.13 s for a one-second sigh |

A field read at the wrong offset fails all of these at once.

## The resolution chain

`SoundbanksInfo.xml` lists events and it lists media files, but it does **not** connect them: an `Event`
element carries only an id, a name and an authoring path. The connection lives in the bank hierarchy.

```
animation audioEventId
  -> Event object (type 4): id, 32-bit action count, action ids
  -> EventAction (type 3): 16-bit action type at +4, target id at +6
       only 0x0403 (Play, game-object scope) starts a sound; 723 of 906 actions are Play
  -> target: either a Sound directly, or a container that holds alternatives
  -> Sound (type 2): media id at +9, after the plugin id and a stream-type byte
  -> <media id>.wem in AudioAssets.zip
```

Containers are walked by inverting the hierarchy. Objects record their parent, not their children, so the
parent links are read and turned round. The parent offsets were found by scanning each object body for a
position whose 32-bit value is another object's id, across every object of that type:

| object type | parent offset | agreement |
| --- | --- | --- |
| Sound (2) | 25 | 2314 of 2360 |
| RandomSequenceContainer (5) | 11 | 468 of 468 |
| ActorMixer (7) | 11 | 23 of 31 |
| MusicSegment (10) | 12 | 209 of 209 |
| MusicPlaylistContainer (13) | 12 | 121 of 123 |

Types whose offset was not established are not walked, so they contribute nothing rather than a guess.

**Nothing is special-cased.** There is no table of event ids anywhere in the code. `anim_bored_01` is a
verification target that goes through the same walk as every other event.

## Coverage

From `wwise <sound-dir> --coverage`:

```
events in banks               835
of those, Stop/Pause/Resume   130  (correctly play nothing)
events that should play       705
resolved to media             615  (87.2% of those)
have a decodable alternative   94  (13.3% of those)
distinct media referenced     2037 of 2214 on disk
  Vorbis    1789  not decoded
  Adpcm      227  decoded
  Unknown     21  not decoded
```

The 130 Stop, Pause and Resume events resolve to nothing because they *play* nothing; counting them as
failures would understate the result, so they are counted apart.

The 90 events that should play but reach no Sound break down as: **44** whose target object is in no bank
this build ships, and **46** that target the music hierarchy (MusicSegment, MusicPlaylistContainer,
MusicSwitchContainer) or a SwitchContainer, whose own source lists are not parsed. Music tracks hold their
media differently from Sounds; that is the remaining resolution gap and it is entirely music, not robot
voice or SFX.

The 21 "Unknown" are media ids referenced by a bank but absent from `AudioAssets.zip` — 53 such ids exist
overall. They are reported as missing rather than guessed at.

## Codecs

Of the 2214 media files:

| codec | files | state |
| --- | --- | --- |
| Wwise Vorbis (format tag 0xFFFF) | 1987 | **not decoded** |
| IMA ADPCM (format tag 2), mono | 220 | decoded |
| IMA ADPCM (format tag 2), stereo | 7 | **not decoded** |

### ADPCM

Ordinary IMA in 36-byte blocks: a signed 16-bit starting predictor, an 8-bit step index, an unused byte,
then 32 bytes of nibble pairs, low nibble first, with the header's sample used as the starting state and not
emitted. That gives 64 samples per block, which the header's own numbers corroborate — 44100 Hz with 24806
average bytes per second over a 36-byte block is 689.05 blocks per second, so 64.0 samples each.

The decoding is self-verifying. IMA is a feedback loop: a wrong nibble order, step table or index table
makes the predictor run away and pin to full scale within a few dozen samples. All 220 mono files decode
with peaks just under full scale and **zero** clipped samples.

The seven stereo files are refused. Both candidate stereo block layouts fail on the same evidence: the byte
that should hold each channel's starting step index falls outside the table's 0..88 range, and the output
pins to full scale for roughly a tenth of its samples. Whatever Wwise does for stereo here, it is not either
arrangement of IMA. All seven are 48 kHz music, and the robot's speaker is mono, so this is recorded rather
than pursued.

### The Vorbis boundary

These files are Wwise Vorbis with the Ogg container and the codebooks stripped. The header is the Wwise
`vorb` block, which in bank version 120 lives inside the `fmt` chunk: it begins at `fmt + 0x18`, is 0x2A
bytes long, and holds the sample count at +0x00, the setup packet offset at +0x10, the first audio packet
offset at +0x14, a codebook-set id at +0x24, and the two blocksize exponents at +0x28 and +0x29.

The layout is confirmed by the assets: those last two bytes decode to 2^8/2^11 in 1955 files and 2^9/2^10 in
32, and those are the only legal Vorbis blocksize pairs — a mis-read offset could not produce them.

**The blocker is the codebooks, and it is measured, not assumed.** Across all 1987 files:

* setup packets are 185 to 230 bytes, mean 221;
* **none** of them contains the `BCV` codebook sync pattern that an inline Vorbis setup would carry;
* they reference 5 distinct external codebook-set ids.

A Vorbis setup packet carrying real codebooks runs to kilobytes. At ~221 bytes these carry codebook
*indices* into an external library that is not shipped in the APK or the OBB.

Decoding them therefore needs three things, and the first is a decision rather than a task:

1. **A packed codebook library.** The usual source is `packed_codebooks_aoTuV_603.bin` from
   [ww2ogg](https://github.com/hcs64/ww2ogg) (BSD-3-Clause, so redistributable with attribution). It is a
   third-party binary blob derived from the aoTuV codebooks, and committing one into this repository changes
   what this project redistributes. That is not a call to make unilaterally.
2. **A port of ww2ogg's bitstream rebuilder**, roughly a thousand lines of bit-level work, to turn a Wwise
   packet stream back into a standard Ogg Vorbis one.
3. **A Vorbis decoder** to turn that into PCM — either a dependency such as NVorbis (MIT), or writing one,
   which is a large piece of work in its own right. Every layer so far has been dependency-free by choice,
   so this is also a decision about the project's posture rather than a detail.

Steps 2 and 3 are ordinary work. Step 1 is the one that needs an answer first, which is why M6 stops here
rather than guessing at it.

## What this means for the acceptance target

`anim_bored_01` names two audio events, and they fall on either side of the boundary:

```
at  99 ms  event 1620542011  Play__Robot_Sfx__Scrn_Sad_Long        5 alternatives, all ADPCM   -> PLAYABLE
at 272 ms  event 2741090610  Play__Robot_Vo__Shared_Bored_Sigh_Short  3 alternatives, all Vorbis -> not decodable
```

So the clip plays **one of its two original shipped sounds** with no WAV mapping supplied, at its encoded
timestamp, and reports the other as not produced. That is a partial result against the stated acceptance and
is not claimed as more.

## Commands

```
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --event Play__Robot_Vo__Shared_Bored_Sigh_Short
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --clip anim_bored_01 --assets <animations-dir>
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --coverage
dotnet run --project src/Cozmo.Conformance -- anim <robot-ip> --assets <dir> --name anim_bored_01 --wwise <sound-dir>
```

`<sound-dir>` needs both the banks and the media. In the unpacked OBB the banks are in `sound_meta` and
`AudioAssets.zip` is under `assets/cozmo_resources/sound`, so either pass a directory holding both or copy
them together. No robot is involved except in the last command.

`--wwise` takes precedence over `--audio`, which stays for supplying a sound the shipped library cannot yet
produce. Anything not produced is named on the console; it is never passed over in silence.
