# M6 — Cozmo's original sound assets

Status: **COMPLETE and FROZEN** (2026-09-18), hardware-verified. Event resolution, Wwise Vorbis and mono
ADPCM all decode from the shipped assets, and `anim_bored_01` plays its original Cozmo sound on the robot
with no manual WAV mapping.

Frozen means the decoding path and its API are settled and are not to be reopened unless a specific failure
appears. What is deferred rather than missing is listed under "Deferred" at the end.

The chain from an animation's audio event id to a media file is decoded and verified across the whole
shipped library, and so are both codecs those files use. **All 2019 Wwise Vorbis files rebuild and decode,
and 220 of 227 ADPCM files decode**; what remains unsupported is listed at the end. Nothing is substituted
or faked: an event that cannot be produced returns null and is named.

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
distinct media referenced     2037 of 2214 on disk
  Vorbis    1789
  Adpcm      227
  Unknown     21  (referenced but absent from the archive)
```

All 615 of those resolved events now have a decodable alternative; see the validation section below.

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
| Wwise Vorbis (format tag 0xFFFF) | 2019 | **decoded** |
| IMA ADPCM (format tag 2), mono | 220 | decoded |
| IMA ADPCM (format tag 2), stereo | 7 | **not decoded** |

The Vorbis count is 2019 rather than the 1987 in `AudioAssets.zip` because 32 more are embedded in the
banks themselves.

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

### Wwise Vorbis

These files are Wwise Vorbis with the Ogg container, the codebooks and the granule positions all stripped.
The header is the Wwise `vorb` block, which in bank version 120 lives inside the `fmt` chunk: it begins at
`fmt + 0x18`, is 0x2A bytes long, and holds the sample count at +0x00, a flag at +0x04 saying whether the
audio packets were stripped further, the setup packet offset at +0x10, the first audio packet offset at
+0x14, a codebook-set id at +0x24, and the two blocksize exponents at +0x28 and +0x29.

The layout is confirmed by the assets: those last two bytes decode to 2^8/2^11 in 1955 files and 2^9/2^10 in
32, and those are the only legal Vorbis blocksize pairs — a mis-read offset could not produce them.

**The codebooks are external, measured rather than assumed.** Across all the Vorbis files: setup packets are
185 to 230 bytes, mean 221; **none** contains the `BCV` codebook sync that an inline setup would carry; and
they reference 5 distinct codebook-set ids. A setup packet carrying real codebooks runs to kilobytes, so at
~221 bytes these hold codebook *indices* into a library that ships nowhere in the APK or OBB.

Three pieces put the stream back together:

1. **The packed codebook library**, `packed_codebooks_aoTuV_603.bin`, vendored from
   [ww2ogg](https://github.com/hcs64/ww2ogg) under BSD-3-Clause. Provenance, revision and SHA-256 are in
   `cozmo-stack/third-party/ww2ogg/README.md`. It is generic Vorbis codec data, not a Cozmo asset.
2. **`WwiseVorbisRebuilder`**, a port of the parts of ww2ogg these files need. It writes the identification
   and comment headers Wwise discards, expands each 10-bit codebook index back into a full codebook, widens
   the fields Wwise packed into fewer bits than the specification uses, and re-frames the audio packets into
   Ogg pages. Only this build's shape is ported — external codebooks, `vorb` size 0x2A, no granule — and any
   file that is not that shape is refused rather than guessed at.
3. **NVorbis** 0.10.5 (MIT) decodes the rebuilt stream. It is confined to `WwiseVorbis.cs`, so nothing else
   in the stack depends on it.

**Granule positions have to be computed.** Wwise strips them, so every packet header carries only a size.
ww2ogg leaves them zero and users then run the separate `revorb` tool to fix them up; this does that inline
instead, accumulating `(previous blocksize + current blocksize) / 4` samples per packet. This is not
cosmetic: with every granule zero the container is still structurally valid — all 61 pages of a test file
passed their CRCs — but NVorbis will not produce samples from a stream whose final granule is zero, and
hangs instead of failing. That cost an hour to find.

## Library-wide validation

From `wwise <sound-dir> --validate`, decoding **every** media file present rather than only those an event
references, so that a codebook family no event happens to name is still exercised:

```
library-wide decode of all 2313 media files
  Vorbis            2019
    rebuilt to Ogg  2019  (100.0%)
    decoded by NVorbis 2019  (100.0%)
  ADPCM             227
    decoded         220  (96.9%)
  missing on disk   21
  other/unreadable  46

sanity checks
  total decoded audio   01:40:38
  files >1% clipped     0
  files decoding empty  0

per codebook set (uid)
  uid 471264238    ok  1832   failed 0
  uid 1069932774   ok   103   failed 0
  uid 103219657    ok    52   failed 0
  uid 2141838623   ok    26   failed 0
  uid 3312772280   ok     6   failed 0

failures grouped by reason
     46  header: not a RIFF file
      7  adpcm: ADPCM with 2 channels is not decoded: the stereo block layout is not established
```

All five codebook sets decode with no failures, so no family is being skipped to flatter the percentage.
Decoded sample rates are 48000, 44100, 32000, 24000 and 36000 Hz, mono and stereo, and every decoded file
is non-empty, within one block of its declared length, and free of clipping.

## Deferred

M6 is frozen with these open. None of them blocks it: each is a case the shipped assets do not need for
animation audio, or one that would take evidence this build does not contain. They are recorded so that
"not decoded" is never mistaken for "overlooked".

| case | count | why it is deferred | what would close it |
| --- | --- | --- | --- |
| **Stereo ADPCM music** | 7 | The stereo block layout is not established. Under both candidate layouts the step-index byte falls outside the table's 0..88 range and roughly a tenth of the output pins to full scale, so decoding would produce plausible-sounding wrong audio. All seven are 48 kHz music and the robot's speaker is mono, so nothing animation-related depends on them. | A capture of the stock app playing one, or a reference implementation of Wwise's stereo ADPCM interleave. |
| **Media ids with no file** | 21 | Referenced by a bank but absent from `AudioAssets.zip`. The asset simply is not in this build; nothing can decode what is not there. | A build of the OBB that ships them, if one exists. |
| **Bank-embedded plugin blobs** | 46 | 173 to 1379 bytes each, beginning `25 80 00 00` rather than `RIFF`. These are Wwise plugin source data, not codec media, so there is no audio in them to decode. | Identifying the plugin source format, only worth doing if something turns out to need it. |
| **Events reaching no Sound** | 90 | 44 target objects held in banks this build does not ship; 46 target the music hierarchy (MusicSegment, MusicPlaylistContainer, MusicSwitchContainer) or a SwitchContainer, whose own source lists are not parsed. Entirely music, not robot voice or SFX. | Parsing MusicTrack source lists and the switch-container child layout. |

### The same list, as the earlier table

| case | count | why |
| --- | --- | --- |
| Stereo ADPCM | 7 | The stereo block layout is not established. Under both candidate layouts the step-index byte falls outside the table's 0..88 range and a tenth of the output pins to full scale, so it is refused rather than guessed. All seven are 48 kHz music; the robot's speaker is mono. |
| Media ids with no file | 21 | Referenced by a bank but absent from `AudioAssets.zip`. Reported as missing. |
| Bank-embedded non-RIFF blobs | 46 | 173 to 1379 bytes, beginning `25 80 00 00` rather than `RIFF`. These are plugin source data, not codec media, and are reported rather than decoded. |
| Events reaching no Sound | 90 | 44 target objects in banks this build does not ship; 46 target the music hierarchy or a SwitchContainer, whose own source lists are not parsed. Entirely music, not robot voice or SFX. |

## The acceptance target, met on hardware

`anim_bored_01` names two audio events, and both produce audio from the shipped assets with no WAV mapping
supplied:

```
at  99 ms  event 1620542011  Play__Robot_Sfx__Scrn_Sad_Long           5 alternatives, ADPCM   -> plays
at 272 ms  event 2741090610  Play__Robot_Vo__Shared_Bored_Sigh_Short  3 alternatives, Vorbis  -> plays
```

Both are covered by an offline test, and the clip was **run on the robot on 2026-09-18**: it played the
animation, displayed the face correctly, and played its original shipped Cozmo sound automatically. That is
the M6 acceptance target met.

## Commands

```
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --event Play__Robot_Vo__Shared_Bored_Sigh_Short
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --decode <media-id>
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --clip anim_bored_01 --assets <animations-dir>
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --coverage
dotnet run --project src/Cozmo.Conformance -- wwise <sound-dir> --validate
dotnet run --project src/Cozmo.Conformance -- anim <robot-ip> --assets <dir> --name anim_bored_01 --wwise <sound-dir>
```

`<sound-dir>` needs both the banks and the media. In the unpacked OBB the banks are in `sound_meta` and
`AudioAssets.zip` is under `assets/cozmo_resources/sound`, so either pass a directory holding both or copy
them together. No robot is involved except in the last command.

`--wwise` takes precedence over `--audio`, which stays for supplying a sound the shipped library cannot
produce. Anything not produced is named on the console; it is never passed over in silence.

## Assets stay out of the repository

No `.bnk`, `.wem` or OBB file is committed. They are excluded by `.gitignore` and the tools read them from
a local path. The only vendored third-party artifact is the generic codebook blob described above.
