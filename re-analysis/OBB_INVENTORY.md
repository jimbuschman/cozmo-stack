# OBB inventory — `main.1204.com.anki.cozmo.obb` (Cozmo 3.4.0-1204)

Date: 2026-09-18. Source: `C:\Users\jbuschman\Downloads\com.anki.cozmo_3.4.0-1204_plus_OBB\Android\obb\com.anki.cozmo\main.1204.com.anki.cozmo.obb`
(439,620,642 bytes, plain ZIP, 2,594 entries, 446,452,664 bytes uncompressed). The xAPK `manifest.json` next to it
confirms package `com.anki.cozmo`, version_code 1204, version_name 3.4.0, and lists this OBB as the only expansion.

Unpacked, unmodified, to `re-analysis/obb/` (Wwise bank metadata additionally extracted to `re-analysis/obb/sound_meta/`;
the 2,214 `.wem` files were left inside `AudioAssets.zip`). Full relative file list: `re-analysis/obb_filelist.txt`.
Nothing was flashed, patched or re-packed.

## 1. Top-level layout

Everything is under `assets/`. Sizes are uncompressed.

| path | files | size | what it is |
|---|---|---|---|
| `assets/AssetBundles/` | 260 | 185 MB | Unity asset bundles for the app UI (`.unity3d` per platform/locale/density + `.manifest`) |
| `assets/bin/Data/` | 59 | 7.7 MB | Unity serialized data split files |
| `assets/LocalizedStrings/{de-DE,en-US,fr-FR,ja-JP}/` | 29 each | 3.7 MB | UI/behavior/onboarding strings, privacy/ToS, BadWordsFilter |
| `assets/Scratch/` | ~400 | ~40 MB | Code Lab (Scratch-Blocks web app: `lib/blocks`, `lib/vm`, featured/sample projects, fonts, images) |
| `assets/Videos/` | 4 | 27.5 MB | Keepaway, MemoryMatch, QuickTap, cozmo_performs_hny `.mp4` tutorials |
| `assets/DASConfig.json` | 1 | 340 B | DAS telemetry: SQS URL `https://sqs.us-west-2.amazonaws.com/792379844846/DasProd-dasprodSqs-…`, flush 15 s |
| `assets/resources.txt` | 1 | 174 KB | 2,457-line manifest of the app-side asset tree |
| `assets/fd1c0492-e3ed-45da-9b69-9ed9a1f452c7` | 1 | 0 B | build marker |
| **`assets/cozmo_resources/`** | **1,741** | **~183 MB** | **the engine's data tree** (copied by the app to `<persistentDataPath>/cozmo/cozmo_resources/`) |

The engine string `Initialized data platform with … resourcesPath = %s` and Unity's `PlatformUtil.GetResourcesFolder()`
(`…/cozmo/cozmo_resources`) both point at this tree; `allAssetHash.txt` (`ab93920c6a4d09c9ffc67d101a7b7080`) is
what `StartupManager` compares against the on-disk copy to decide whether to re-extract.

## 2. `cozmo_resources/` tree

```
cozmo_resources/
  allAssetHash.txt                     32 B  md5 of the tree, checked by StartupManager
  assets/
    animations/           289 .bin   8.43 MB  FlatBuffers AnimClips (993 clips)
    animationGroups/      507 .json  0.18 MB  in 47 group dirs (Bored, Codelab, Freeplay, Needs, SDK, VoiceCommand, …)
    animationGroupMaps/AnimationTriggerMap.json       573 CladEvent→AnimName pairs
    cubeAnimationGroupMaps/CubeAnimationTriggerMap.json 40 pairs
    faceAnimations/       2 dirs (face_bored_event_02/_04), 552 PNG frames  sprite face animations
    RewardedActions/RewardedActions.json  27 KB  Unity-side reward rules ($type = Anki.Cozmo.*Condition)
  config/
    cozmo_anim.fbs                       FlatBuffers schema for animations (namespace CozmoAnim)
    features.json                        17 feature gates (AndroidConnectionFlow, CodeLabGame, EduMode, …)
    experiments.json                     AnkiLab A/B definitions (report_test_auto, unconnected_decay_rates, preholiday_tuning)
    engine/
      configuration.json                 {basestation_mode:0, playback_log_folder, test_value} — NOT the network config
      AnkiLogStringTables.json           nameTable 323 + formatTable 325 entries: decodes robot `trace`(0xb0) messages
      cozmo_mprim.json         1.27 MB   xytheta planner motion primitives: 16 angles, 10 mm resolution, 9 actions
      vision_config.json                 vision modes/schedules, auto-exposure, PetTracker, MotionDetector params
      mood_config.json                   emotion decay graphs per EmotionType
      needs_*.json (5 variants each)     needs levels/decay/actions; _alloff/_testAoff/_testBoff/_testCoff are AnkiLab variants
      needs_handlers_config.json         needs-based face glitch ("eye glitch") curves vs repair level
      unlock_config_nurture.json         default unlocks list
      do_a_trick_weights.json, game_request_weights.json, inventory_config.json (Sparks cap 999)
      tts_config.json, sayTextintentConfig.json
      backup_config.json                 NV tags to back up, with numeric values (see §5)
      console_filter_config.json, das_event_config.json, local_notification_config.json
      animations/                        4 test JSON animations (ANIMATION_TEST, anim_qa_firmwaremessaging_01, anim_triple_backup, soundTestAnim)
      behaviorSystem/                    207 files (see §7)
      emotionevents/                     11 files (action, charger, cliff, cube, face, hiking, motion, ocd, reaction, spark, workout)
      lights/backpackLights/backpackLightPatterns.json ; lights/cubeLights/ 37 files (incl. feeding/)
      firmware/cozmo.safe                current firmware 2381
      firmware_1299/ firmware_1859/ firmware_1889/ firmware_2158/ firmware_2214/  one cozmo.safe each
      old_firmware/cozmo.safe            identical to firmware_2214
  sound/AudioAssets.zip     141.5 MB   Wwise banks + 2,214 .wem (see §4)
  tts/Voices/               87 files, 27.8 MB  Acapela voices (see §6)
```

## 3. Firmware (documented only — nothing modified or flashed)

All files are `cozmo.safe`: a JSON signature header, a single `\0` terminator, then a body. `FirmwareUpdater::LoadHeaderData`
in the engine finds the terminator with `memchr` and parses the JSON, which is exactly what the robot later reports in
`firmwareVersion`(0xee). The body is streamed unchanged in 1,024-byte `OTA::Write`(0xaf) chunks.

| directory | version | build date | size (B) | body (B) | md5 | sha256 (first 16) |
|---|---|---|---|---|---|---|
| `firmware/` | **2381** | 2019-01-08 | 382,416 | 381,970 | `5bc0aeffcfd346607c4c19b136f3f4f3` | `a4e266cfaaa95e18…` |
| `firmware_2214/` | 2214 | 2017-08-09 | 378,304 | 377,858 | `b5740ad7c94021bb2c4547f4108a7476` | `f674fa7cf71050ae…` |
| `old_firmware/` | 2214 | 2017-08-09 | 378,304 | 377,858 | `b5740ad7c94021bb2c4547f4108a7476` (identical) | `f674fa7cf71050ae…` |
| `firmware_2158/` | 2158 | 2017-06-15 | 378,304 | 377,858 | `6796a312bbe9b0ce301662842c3136c4` | `cae7a6366e50f6b5…` |
| `firmware_1889/` | 1889 | 2017-03-23 | 378,304 | 377,858 | `201aa279bb1d77a554448df09e949fcb` | `d4652c68a33b6c9d…` |
| `firmware_1859/` | 1859 | 2017-03-20 | 386,528 | 386,082 | `26fc309d5e6e3c35c958fc6998261ee5` | `a4e3f6e02e339c5a…` |
| `firmware_1299/` | 1299 | 2016-11-21 | 384,472 | 384,050 | `50b2db65db932cafcef261a93f4e161d` | `edbcc3fa4be3fbe4…` |

Current (2381) header:

```json
{"version": 2381, "git-rev": "408d28a7f6e68cbb5b29c1dcd8c8db2b38f9c8ce", "date": "Tue Jan  8 10:27:05 2019",
 "time": 1546972025, "messageEngineToRobotHash": "9e4a965ace4e09d86997b87ba14235d5",
 "messageRobotToEngineHash": "a259247f16231db440957215baba12ab", "build": "DEVELOPMENT",
 "wifiSig": "69ca03352e42143d340f0f7fac02ed8ff96ef10b", "rtipSig": "36574986d76144a70e9252ab633be4617a4bc661",
 "bodySig": "695b59eff43664acd1a5a956d08c682b3f8bd2c8"}
```

Per-version CLAD hashes (what the engine compares against its own; a mismatch raises `EngineRobotCLADVersionMismatch`):

| version | messageEngineToRobotHash | messageRobotToEngineHash |
|---|---|---|
| 2381 | 9e4a965ace4e09d86997b87ba14235d5 | a259247f16231db440957215baba12ab |
| 2214 | 861bbc71828456c0f073c4464bdcb21e | 2dc8419f768f6f3fd4843cbb0a86f7f7 |
| 2158 | 71beec8d11144f3a3718d2cc5ea602f3 | 4018b2e764ec08f5fcacdb6358847cb0 |
| 1889 | 7098b4a266c0ccc2102a61fda53b8999 | 9b83f21da9120fdeebfeabe84af81c32 |
| 1859 | 54195812be0de998a4ebde795364d62b | 90d8f3273055624b8444fbcbef555ee8 |
| 1299 | 61879d8808f0308cd8ae6340ddfe06e6 | 5914fda0b97c7aadaf0e4d97fc72610f |

Body observations (read-only scan of 2381): 578 zero bytes, then near-uniform high entropy (7.99–8.00 bits/byte per
32 KB block) with a single plaintext fragment (the build date). No ELF, no ESP8266 `0xE9` image header at a
structural offset, no `SAFE`/`COZM` magic. The body is therefore encrypted or compressed; the header's three
`*Sig` SHA-1-sized fields presumably sign the three controller images (Wi-Fi/ESP8266, RTIP/K02, body/nRF51).
Container layout is **not** recoverable from the file alone — this is the "SAFE analysis" phase, deferred.

How the app chooses a file: normal flow → `RobotConnectionResult.OutdatedFirmware` → `UpdateFirmware(FirmwareType.Current, 0)`
→ engine `GetFirmwareFilename(Current, 0)` → `firmware/cozmo.safe`. The debug `FirmwarePane` enumerates
`config/engine/firmware_*` directories and calls `UpdateFirmware(FirmwareType.Old, <version>)`, so the
`firmware_<ver>` folders are developer downgrade targets; `old_firmware/` is the legacy "Old" default.

## 4. Audio / Wwise (`sound/AudioAssets.zip`, 141.5 MB, 2,229 entries)

| item | detail |
|---|---|
| banks | `Init.bnk` (3.5 KB), `Dev_Debug.bnk`, `SFX.bnk` (101 KB), `Music.bnk` (1.07 MB), `UI.bnk` (43 KB), `English(US)/Cozmo.bnk` (6.24 MB) — exactly the six names in the engine strings |
| bank text dumps | `Init.txt`, `SFX.txt`, `Music.txt`, `UI.txt`, `Dev_Debug.txt`, `English(US)/Cozmo.txt` (1.4 MB) — human-readable event/switch/RTPC tables |
| `SoundbanksInfo.xml` | 1.19 MB, 835 events, 4,410 file references, languages `English(US)` and `SFX` |
| `PluginInfo.xml` | plugins: **Anki Hijack** (id 70339, the robot-bus capture), **Anki Wave Portal** (feeds TTS PCM in), Wwise Sine/Silence/ToneGen, Parametric EQ, Compressor, Expander, Peak Limiter, **Harmonizer** (the "Cozmo voice" pitch effect) |
| `.wem` streams | 2,214 files, 130.9 MB: 1,987 Wwise Vorbis (fmt 0xFFFF), 227 ADPCM (fmt 0x0002); 1,862 @ 48 kHz, 208 @ 44.1 kHz, 110 @ 32 kHz, 32 @ 24 kHz; 2,030 mono / 184 stereo |
| switch groups seen in Init.txt | `Cozmo_Robot_Voice/{external, mood, relationship}`, `music_switches/freeplay_mood` (hiking, dancing, guard_dog, nurture_*, sleep, …) |

Implication: replacing Wwise means (a) parsing `.bnk` HIRC to map the 835 events to sources, (b) decoding
Wwise Vorbis (ww2ogg-style) and ADPCM, (c) reimplementing the Hijack capture into 744-sample robot frames and
the Harmonizer/EQ voice chain. PyCozmo's `audiokinetic` module covers part of (a); (b) and (c) are open.

## 5. NV storage values resolved

`backup_config.json` gives official numeric tags: GameSkillLevels 0x180000, Onboarding 0x181000, GameUnlocks 0x182000,
FaceEnrollData 0x183000, FaceAlbumData 0x184000, NurtureGameData 0x194000, InventoryData 0x195000, LabAssignments 0x196000.
These match PyCozmo's `NvEntryTag` values for the same names, so that part of PyCozmo is now **verified**. The remaining
30 factory/calibration tags (CameraCalib, CalibImage1-6, IMUAverages, CliffValOnDrop/OnGround, ToolCode*, PlaypenTestResults,
BirthCertificate, …) still have no official numeric value outside the binary.

## 6. TTS (`tts/Voices/`, 27.8 MB)

Acapela "CO" (Cozmo-tuned) 22 kHz voices: `co-USEnglish-Ryan-22khz`, `co-French-Bruno-22khz`, `co-German-Klaus-22khz`,
`co-Japanese-Sakura-22khz`, each with an NLP folder (`.dca/.ldi/.trz/.bnx/.oso`) and a voice folder (`*_22k_co.alf/.erf`,
`.fl.inf/.fl.ini`). `tts_config.json` selects voice + speed per platform/locale and notes that **pitch is applied by
the audio layer, not the TTS SDK** (matches the Wwise Harmonizer). `sayTextintentConfig.json` defines per-intent
duration/pitch traits and `SayTextVoiceStyle`. Licensed content: reference only.

## 7. Behavior / personality / needs configs

* `behaviorSystem/` (207 files): `behavior_system_config.json` (root activity `StrictPriorityFreeplay` with sub-activities
  Socialize 11, Singing 12, PlayWithHumans 13, BuildPyramid 14, PlayAlone 15, Hiking 16, NothingToDo 17),
  `activities_config.json` (activities with Scoring/Selection behavior choosers, repetition penalties),
  `activities/freeplay/*` (10), `activities/sparks/*` (14), `reactionTrigger_behavior_map.json` (23 triggers with cooldown/probability params),
  `workout_config.json`, and 170 behavior JSONs: `reactions/` (24), `freeplay/` (singing 39, sparkable 20, needs 12, hiking 10,
  buildPyramid 6, requestGame 5, userInteractive 3, …), `voiceCommands/` (18), `feeding/` (11), `meetCozmo/` (4),
  `devBehaviors/` (5: factoryTest, dockingTestSimple, liftLoadTest, …), `onboarding/`, `playArbitraryAnim.json`, `wait.json`.
  Each behavior JSON is small: `behaviorClass` (maps to a native `Behavior*` class), `behaviorID`, optional
  `needsActionID`, `requiredUnlockId`, class-specific params. The logic lives in the engine; the JSON only wires it.
* `emotionevents/` (11 files): `emotionEvents[] {name, emotionAffectors[{emotionType, value}]}` per subsystem.
* `mood_config.json`: decay graphs per emotion; `needs_config.json` (levels/brackets exported from a Google sheet 2018-08-09),
  `needs_decay_config.json` (connected/unconnected decay per bracket), `needs_level_config.json`, `needs_action_config.json`
  (which actions move which need), `needs_handlers_config.json` (face glitch vs repair level).
* `lights/`: `backpackLightPatterns.json` (charging/charged/badCharger patterns, 5 LEDs) and 37 cube light animations
  (`pattern{onColors, offColors, on/offPeriod_ms, transitionOn/OffPeriod_ms, offset, rotationPeriod_ms}`, `duration_ms`).
* `unlock_config_nurture.json`, `do_a_trick_weights.json`, `game_request_weights.json`, `inventory_config.json`.

## 8. Vision / model / config resources

`vision_config.json` is the only vision resource: initial modes (Markers, Faces, Pets, Motion, OverheadEdges, CheckingQuality,
LaserPoints, Statistics on; expression/smile/gaze/blink off), per-mode frame schedules, `FaceAlbum: "robot"`,
face detection `video` mode, recognition `asynchronous`, auto-exposure parameters (target mid value 115, percentiles,
initial exposure 16 ms), PetTracker and MotionDetector parameters. **No neural-network or classifier model files exist
in the OBB**: face/pet detection and recognition data are inside the OKAO library linked into `libcozmoEngine.so`,
and marker definitions are compiled into the engine. Camera calibration comes from robot NV, not the OBB.

## 9. Navigation / planning

`cozmo_mprim.json` (1.27 MB): `resolution_mm: 10`, `num_angles: 16`, `angle_definitions`, and 9 motion-primitive
`actions` (`{index, name, extra_cost_factor, …}`) for `Anki::Planning::xythetaPlanner`. The engine's `planner/contextDump/mprim.json`
string is a debug dump path, not a load path. No map or obstacle data ships; the memory map is built at runtime.

## 10. Protocol / network configuration

None in the OBB. The engine's network config (`AdvertisingHostIP`, `RobotAdvertisingPort` 5100, `UiAdvertisingPort` 5102,
`SdkAdvertisingPort` 5104, `VizHostIP`, …) lives in a Unity TextAsset inside the APK (`protocol/engine_configuration.json`),
and `cozmo_resources/config/engine/configuration.json` only carries `basestation_mode`/`playback_log_folder`/`test_value`.
The only URL is the DAS SQS endpoint in `assets/DASConfig.json`. Robot IP/ports remain hard-coded in engine and app code.

## 11. Cross-reference with `libcozmoEngine.so` strings

Resource-like strings recovered from `.rodata` (35) vs the OBB:

* **Present at the exact path**: `config/features.json`, `config/experiments.json`, `config/engine/{backup_config, console_filter_config,
  das_event_config, do_a_trick_weights, inventory_config, local_notification_config, needs_handlers_config, tts_config, vision_config}.json`,
  `config/engine/behaviorSystem/{activities_config, behavior_system_config, workout_config}.json`,
  `config/engine/needs_{action,level,decay,}config` (engine appends the AnkiLab variant suffix and `.json`),
  `assets/cozmo_resources/sound/AudioAssets.zip`, `faceAnimations`, `configuration.json`.
* **Present inside `AudioAssets.zip`**: `Init.bnk`, `Cozmo.bnk`, `SFX.bnk`, `Music.bnk`, `UI.bnk`, `Dev_Debug.bnk`.
* **Present outside `cozmo_resources`**: `DASConfig.json` (`assets/DASConfig.json`).
* **Runtime-generated, correctly absent**: `data.bin`, `enrollData.json`, `statsForBackup.json`, `lastCachedLocation.json`,
  `uniqueDeviceID.dat`, `planner/contextDump/mprim.json`, `TestAnim.json`.
* **Loaded without a literal path string** (directory walk or config-listed, per `RobotDataLoader::WalkAnimationDir/CollectAnimFiles`):
  `animations/*.bin`, `animationGroups/`, `AnimationTriggerMap.json`, `CubeAnimationTriggerMap.json`, `emotionevents/*`,
  `lights/*`, `mood_config.json`, `sayTextintentConfig.json`, `cozmo_mprim.json`, `AnkiLogStringTables.json` (engine has
  `Robot.AnkiLogStringTablesNotFound`), `unlock_config_nurture.json`, `game_request_weights.json`, behavior JSONs.
* **Unity-side only**: `RewardedActions.json` (`RewardedActionManager.cs`), `allAssetHash.txt` (`StartupManager.cs`),
  LocalizedStrings, Scratch, Videos, AssetBundles.

Nothing referenced by the engine is missing from the OBB.

## 12. Animation assets in numbers

289 `.bin` → 993 clips (parsed with the `cozmo_anim.fbs` schema): keyframes per track — ProceduralFace 33,485,
HeadAngle 12,625, BodyMotion 8,093, BackpackLights 7,140, LiftHeight 5,225, RobotAudio 4,184, Event 36,
FaceAnimation 2, RecordHeading 1, TurnToRecordedHeading 1. Groups: 507 JSON in 47 dirs
(`{"Animations":[{Name, Weight, CooldownTime_Sec, Mood}]}`), 573 trigger→group pairs, 40 cube trigger pairs.
Schema highlights: `ProceduralFace` keyframes carry `leftEye`/`rightEye` float arrays (the 19 per-eye params),
`RobotAudio` carries Wwise `audioEventId:[long]` with probabilities/alternates, `BodyMotion.radius_mm` is a string
("STRAIGHT"/"TURN_IN_PLACE"/number), `TurnToRecordedHeading` has 7 fields (consistent with the official 13-byte
robot message PyCozmo mis-sizes as 0).

## 13. What the OBB does NOT contain

Cube firmware, robot factory/calibration data, marker code tables, OKAO models, Wwise runtime, Acapela runtime,
the network configuration, Vector-era assets, or any source for the firmware. The `.safe` bodies are opaque.
