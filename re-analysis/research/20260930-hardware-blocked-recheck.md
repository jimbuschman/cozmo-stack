# HARDWARE_ONLY / BLOCKED_EXTERNAL shipped-artifact recheck

Request: operator message of 2026-09-30.  This is an independent research-lane
answer; it does not edit the manifest or inventories.

HEAD checked: `6514f0d67c7505b21b464a8ee569f15a46dfd03e`.

## Result

There are 12 current records: 10 `HARDWARE_ONLY` and 2
`BLOCKED_EXTERNAL`.

| Verdict | Count | Records |
|---|---:|---|
| **SETTLEABLE** | 2 | M11-016, M14-006 |
| **CONFIRMED** | 10 | M1-033, M1-043, M3-008, M3-016, M9-023, M4-013, M5-036, M4-021, M4-024, M3-036 |

Both `BLOCKED_EXTERNAL` records are misclassified under the project's current
"exact, always" rule.  M11-016's OKAO and OMCV implementations are statically
present in `libcozmoEngine.so`, not unresolved imports.  M14-006's Acapela
implementation, voice databases, engine integration, Wave Portal source plug-in,
and configuration all ship.  Proprietary machine code that ships is difficult
primary source, not external behavior.

For the ten confirmed records I searched all 28 APK native libraries, the Unity
managed/native application files, the OBB file inventory and configs, Wwise
banks/metadata/media, all seven shipped `cozmo.safe` files, and existing capture
bundles.  The shipped robot firmware headers identify versions 1299, 1859, 1889,
2158, 2214, and 2381 (2214 is duplicated as `old_firmware`); there is no 2457
image.  The bodies after the plaintext headers are high-entropy encrypted data.
Nothing in the APK/OBB supplies the robot/cube firmware 2457 behavior, a phone
kernel's stale `errno`, or a recording of the final robot acoustic output.

## M1-033 — Robot-side transport behaviour

> **Status:** HARDWARE_ONLY  
> **Location:** `cozmo-stack/src/Cozmo.Transport/RobotLink.cs`  
> **Title:** Robot-side transport behaviour  
> **Evidence:** “part A open question 4: whether the robot sets isReply when it echoes a ping, its own resend timing, and whether it accepts packed frames of types 7, 8 and 9”; “hardware run re-analysis/acceptance/hardware/20260924-112412-M1-LINK (firmware 2457): the robot sent only type-9 frames (1026); it echoed all 1025 pings with isReply clear and our timestamps; it repeated unacked reliable messages after 24..34 ms (median 33.6 ms). Not observed: whether it accepts packed type 7/8/9 frames from the engine”  
> **Effect:** this stack assumes robot behaviour the app package cannot show  
> **Unresolved:** whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none)

**CONFIRMED.** I searched `libcozmoEngine.so`'s reliable-transport send and
receive paths, every other `.so`, the Unity code, protocol schemas, configs, the
OBB inventory, and all shipped firmware files for the packed-frame constants and
robot receive logic.  The app-side engine establishes what it sends and how it
decodes received packets, not which packet types robot firmware 2457 accepts.
The only direct 2457 evidence remains the cited capture, and that capture sent no
types 7/8/9 to the robot.  The newest shipped firmware is 2381 and its encrypted
body cannot establish 2457 behavior.

## M1-043 — Whether a 0-byte UDP read warns

> **Status:** HARDWARE_ONLY  
> **Location:** `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`  
> **Title:** Whether a 0-byte UDP read warns  
> **Evidence:** “CA35: a 0-byte read takes the error path and reads the stale errno; it warns iff errno != EAGAIN and reopens on ENOTCONN (0x0083AA98 → 0x0083AAC4..0x0083AB24)”  
> **Effect:** a warning is or is not logged for a 0-byte datagram  
> **Unresolved:** the errno value after a 0-byte recvmsg on the phone

**CONFIRMED.** I checked the cited engine path, all native dependencies, and the
Unity/managed callers.  The engine does not assign `errno` between the successful
zero-byte `recvmsg` result and the read at `0x0083AAC4`; the value is whatever the
phone process's system libc/kernel call history left there.  Android's `libc.so`
and kernel do not ship in the APK/OBB, and no app artifact fixes the preceding
stale value.  The branch is recovered; its run-specific input is not.

## M3-008 — Firmware face-row mapping and playback period

> **Status:** HARDWARE_ONLY  
> **Location:** `cozmo-stack/src/Cozmo.Robot/Display.cs`  
> **Title:** How the firmware maps pair bits to physical display rows, and the robot playback period  
> **Evidence:** “engine side: pair bit0 = row 2k, bit1 = row 2k+1 (B10 0x00581A48..0x00581AB0; decoder B14 0x0057FCF0..0x0057FDD8)”; “firmware images are encrypted (entropy 7.8-8.0 bits/byte) and 2457 is not shipped”  
> **Effect:** the face shows interlaced or shifted rows  
> **Unresolved:** only the robot can answer it

**CONFIRMED.** I searched the animation/face encoders and decoders in
`libcozmoEngine.so`, Unity display code, animation assets/configs, protocol
schemas, and every `cozmo.safe`.  These settle the engine-side pair packing only.
Physical row selection and consumption timing occur after the wire boundary.
No robot display implementation or timing table exists in the app/OBB, and
firmware 2457 is absent.

## M3-016 — Firmware colour-frame behavior

> **Status:** HARDWARE_ONLY  
> **Location:** `cozmo-stack/src/Cozmo.Robot/Camera.cs`  
> **Title:** Whether firmware 2457 emits colour frames in this format, and how the robot reacts to EnableColorImages  
> **Evidence:** “A14 encoding 8 with data[0] != 0 becomes 9 (0x004F1D8C)”; “3b the engine flag has no decode consumer (0x005FAC64, 0x005FBDAE)”; “firmware 2457 is not shipped”  
> **Effect:** colour frames never arrive or arrive differently  
> **Unresolved:** only the robot can answer it

**CONFIRMED.** I searched the native camera receiver, Unity camera consumers,
CLAD/schema definitions, camera and vision configs, captures, and shipped
firmware.  The engine determines the encoding-8-to-9 observation and the outgoing
flag but contains no producer for robot camera frames and no interpretation of
`EnableColorImages` on the robot.  No 2457 firmware or 2457 colour capture ships.

## M9-023 — Stock singing acoustic reference

> **Status:** HARDWARE_ONLY  
> **Location:** `re-analysis/WWISE_MUSIC.md`  
> **Title:** How the stock app and robot sounded when singing  
> **Evidence:** `re-analysis/research/20260928-X4-M9-wwise-music-extraction.md`  
> **Effect:** no reference exists to judge the rendered song against  
> **Unresolved:** Only an original-app/robot recording can establish the final audible result; a hardware pass verifies sound but cannot raise source provenance.

**CONFIRMED.** I searched the Wwise banks, `SoundbanksInfo.xml`, exported sound
metadata, all `.wem` media, the native Wwise/music paths, OBB media extensions,
and hardware/capture bundles.  The package has singing note media, events, bank
graphs, and rendering code; those are inputs to a source-faithful digital
renderer, not a recording of its final output through the robot DAC, amplifier,
speaker, enclosure, and room.  No stock-app/robot singing recording is present.
The record asks for that audible reference, so the source assets do not settle
it.

## M11-016 — Face, pet and motion detection

> **Status:** BLOCKED_EXTERNAL  
> **Location:** `cozmo-stack/src/Cozmo.Robot/Vision/Faces.cs`  
> **Title:** Face, pet and motion detection  
> **Evidence:** `FaceTracker::Impl::Update`; `FaceRecognizer`; `OKAO_DT_* exports`  
> **Effect:** no face, pet or motion is ever detected  
> **Unresolved:** nothing recoverable: the detector is third-party binary code  
> **Test:** `FaceTests`

**SETTLEABLE.** The unresolved text is factually wrong for this package.
`libcozmoEngine.so` has 177 defined/exported `OKAO_*` symbols and 30
defined/exported `OMCV_*` symbols; they are not undefined references needing an
absent provider.  The Anki wrappers, recognition logic, pet detector, and motion
detector are also full function bodies in the same library.  The shipped
`vision_config.json` enables all three modes and supplies their parameters.

| Step | Shipped row/address | What it settles |
|---:|---|---|
| 1 | `VisionSystem::Init`, `0x006B0F96` (FaceTracker construction), `0x006B1002` (MotionDetector construction), `0x006B1024` (PetTracker init/result gate) | The production path owns and initializes all three detectors; this is not a test-only path. |
| 2 | `FaceTracker::Update` `0x0086B232..0x0086B239`; `FaceTracker::Impl::Update` `0x0086D740..0x0086DE5D` | Live face images enter the detector. The implementation calls detect, result-count, raw-result, face-part, expression, gaze/blink, smile, and recognition paths. |
| 3 | `OKAO_DT_Detect_GRAY` body `0x009067C8..0x0090681B`; `OKAO_DT_GetResultCount` `0x00906920..0x0090692F`; `OKAO_DT_GetRawResultInfo` `0x00906958..0x0090697F` | The supposedly external face-detection operations themselves ship as ARM code in `libcozmoEngine.so`. |
| 4 | `FaceRecognizer::RecognizeFace` `0x008640E4..0x00864CD7`; `OKAO_FR_ExtractHandle_GRAY` `0x0092C8E4..0x0092CA37`; `OKAO_FR_Identify` `0x0092DDB8..0x0092DEE7` | Face feature extraction, recognition, thresholds/order/failures, and the underlying OKAO calls are recoverable from shipped code. |
| 5 | `PetTracker::Init` `0x0087BE80..0x0087C5C9`; `PetTracker::Update` `0x0087C980..0x0087CBFD`; `VisionSystem::DetectPets` `0x006B3940..0x006B3ACF` | The live pet path, JSON gates, black-out rectangles, result conversion, and failures ship. |
| 6 | `OMCV_PD_Detect` body `0x00933C28..0x00933D67`; `OMCV_PD_GetResultCount` `0x00933D68..0x00933D87`; `OMCV_PD_GetResultInfo` `0x00933D88..0x00933DBB` | The pet classifier/tracker operations also ship inside `libcozmoEngine.so`; they are not an absent external library. |
| 7 | `VisionSystem::DetectMotion` `0x006B3B90..0x006B3BB7`; `MotionDetector::Detect` `0x006AAAF0..0x006AAB63`; gray/RGB helpers `0x006A8FA4..0x006A96A1` and `0x006A9EF8..0x006AA55D` | The complete native motion-comparison path is present, including gray/RGB selection and result production. |
| 8 | `re-analysis/obb/assets/cozmo_resources/config/engine/vision_config.json`, `InitialVisionModes`, `PetTracker`, and `MotionDetector` objects | Shipped gates and constants: all three modes enabled; pet thresholds/cycles/lost/steadiness values and motion region/sensitivity/stability values are present. |

This does not by itself certify a future C# port: the rows must still be extracted
instruction by instruction, including widths, float bits, order, failure returns,
and the result-to-world path.  It does establish that `BLOCKED_EXTERNAL` is no
longer supportable.  The appropriate next state is a recoverable or implementation
gap, not an equivalent substitute.

## M14-006 — Text to speech

> **Status:** BLOCKED_EXTERNAL  
> **Location:** `cozmo-stack/src/Cozmo.Robot/Behavior/FaceBehaviors.cs:69`  
> **Title:** Text to speech is not implemented  
> **Evidence:** `libacattsandroid.so`; `PluginInfo.xml Anki Wave Portal`  
> **Effect:** SayTextAction says nothing  
> **Unresolved:** the voice model and the plug-in are third-party binaries

**SETTLEABLE.** Both things named as missing in `unresolved` ship.  The APK has
the 1.99 MiB `libacattsandroid.so` with 2,327 exported functions and its internal
Acapela/BABILE implementation.  The OBB has 87 voice files (27,790,421 bytes)
for Ryan, Bruno, Klaus, and Sakura, plus `tts_config.json` and
`sayTextintentConfig.json`.  Anki Wave Portal is implemented and statically
registered in `libcozmoEngine.so`; it is not merely a name in `PluginInfo.xml`.

| Step | Shipped row/address | What it settles |
|---:|---|---|
| 1 | `SayTextAction::GenerateTtsAudio` `0x00560D80..0x00560EC7` | Validates voice style, calls `TextToSpeechComponent::CreateSpeech`, stores the operation id/state, and records the zero/failure case. |
| 2 | `TextToSpeechComponent::CreateSpeech` `0x0069E4C8..0x0069E80B` | Allocates an 8-bit operation id (wraps `0xFF` to 1), rejects an id already cached, creates the operation bundle, and dispatches synthesis asynchronously. |
| 3 | `TextToSpeechProviderImpl::CreateAudioData` `0x006A0210..0x006A057D`; native PCM callback `Java_com_anki_cozmo_CozmoTextToSpeech_callback` `0x0069FA00..0x0069FA3F` | The engine calls Java `com/anki/cozmo/CozmoTextToSpeech.createAudioData(String,III)I`; the callback appends returned 16-bit PCM to the native vector. |
| 4 | `libacattsandroid.so`: `nLoadVoice` `0x0003ECE5..0x0003EEDC`, `nQueueText` `0x0003EEE1..0x0003F034`, `nSpeak` `0x0003F13D..0x0003F258`; internal `tts_function_load_voice` `0x0003DCAD..0x0003E274`, `tts_function_analyse_and_speak` `0x0003E325..0x0003E724`, `tts_function_generate_samples` `0x0003C7E1..0x0003CEE8` | The proprietary voice loading, text analysis, and sample-generation implementation is present as addressable ARM code. |
| 5 | `TextToSpeechComponent::CreateAudioData` `0x0069EF10..0x0069F041` | Calls the provider; on success converts every signed 16-bit sample to float and constructs the wave container; on provider error it returns null. |
| 6 | `TextToSpeechComponent::PrepareAudioEngine` `0x0069E9A8..0x0069EB03`; Wave Portal static registration `0x004DD97C..0x004DD9C6`; plug-in setup `0x008DD624..0x008DD6AF` | Clears any previous portal data, computes duration from samples/rate/channels, gives the generated wave container to Wave Portal, and exposes it to the Wwise event. |
| 7 | `SayTextAction::Init` `0x00561488..0x005617B9` | Gates on operation state, prepares audio, rejects overlong duration, creates the live audio keyframe/animation path, posts the processing switch and pitch parameter, and returns explicit failure results. |
| 8 | OBB `tts/Voices/co-{USEnglish-Ryan,French-Bruno,German-Klaus,Japanese-Sakura}-22khz/**`; `config/engine/tts_config.json`; `config/engine/sayTextintentConfig.json`; `sound_meta/PluginInfo.xml:6`; English bank metadata Wave Portal rows | The selected Android voices, speed/pronunciation configuration, per-intent duration/pitch/style traits, voice databases, Wwise event graph, and source plug-in id 6558402 all ship. |

One corpus caveat does not restore `BLOCKED_EXTERNAL`: the checked repository
extract contains no `classes.dex`, so the Java body of
`CozmoTextToSpeech.createAudioData` is not locally inspectable here.  Its native
callee (`libacattsandroid.so`), native callback, voice databases, and both sides
of the engine/Wwise path do ship.  A complete extraction should recover that APK
bytecode (or mark only that bridge `UNKNOWN`) and then read the cited ARM bodies;
it should not replace Acapela with a plausible synthesizer.

## M4-013 — StartMotorCalibration after connection calibration

> **Status:** HARDWARE_ONLY  
> **Location:** `cozmo-stack/src/Cozmo.Conformance/Control.cs`  
> **Title:** Whether the robot honours StartMotorCalibration after its connection-time calibration  
> **Evidence:** “hardware check I”  
> **Effect:** a requested recalibration never happens, and a behaviour waiting on it waits for its backstop  
> **Unresolved:** only a robot can show whether the request is honoured

**CONFIRMED.** I searched the engine sender/handler and connection calibration
paths, CLAD tags, Unity call sites, configs, firmware updater assets, and captures.
They settle when and how the app sends `StartMotorCalibration`, but no app-side
artifact executes the command or produces the completion response.  The shipped
encrypted firmware is at most version 2381; the operator's behavior is 2457.

## M5-036 — Robot-side animation behavior

> **Status:** HARDWARE_ONLY  
> **Location:** `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`  
> **Title:** Robot-side animation behaviour: AbortAnimation handling and leftovers, Start without End and the unbounded keep-alive stream, locked-track suppression, animStarted/animEnded echo  
> **Evidence:** “A23, A25, B3, gap2 Q1.10”; “robot firmware 2457 is not shipped”  
> **Effect:** the robot animates differently from what the engine expects  
> **Unresolved:** only the robot can answer it

**CONFIRMED.** I searched the complete app-side animation scheduler, outgoing
start/end/abort/keyframe encoders, incoming animation-state handlers, animation
assets, configs, Unity code, schemas, and firmware images.  Those artifacts show
the engine's requests and expectations.  None implements the robot's buffer,
track-lock arbitration, keep-alive timeout, cleanup, or echo generation.  There
is no 2457 firmware body or capture covering all listed cases.

## M4-021 — AbsoluteLocalizationUpdate reflected in RobotState

> **Status:** HARDWARE_ONLY  
> **Location:** `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`  
> **Title:** Whether the robot reports the origin id and frame from AbsoluteLocalizationUpdate in RobotState  
> **Evidence:** “SC4i: this stack never sent AbsoluteLocalizationUpdate; all 855 states in re-analysis/acceptance/hardware/20260924-202748-CONTROL/frames.jsonl reported origin 0, frame 0”; “no original-app capture exists”  
> **Effect:** no robot state is accepted, or all are  
> **Unresolved:** only the robot can answer it

**CONFIRMED.** I searched the engine's localization sender and RobotState
consumer, Unity/native call sites, CLAD field layouts, localization configs,
hardware bundles, and all firmware assets.  The app package defines both wire
messages but not the firmware state transition joining them.  The only capture
never sent the update, and no original-app capture or 2457 firmware ships.

## M4-024 — Cube telemetry forwarding and StreamObjectAccel

> **Status:** HARDWARE_ONLY  
> **Location:** `cozmo-stack/src/Cozmo.Robot/CubeAccel.cs`  
> **Title:** What makes the robot forward cube telemetry after connection, and the robot-side effect of StreamObjectAccel  
> **Evidence:** “S12 AddListener sends {activeID, 1} reliable (0x00635556, 0x00635562)”; “S18/S19 no shipped robot or cube firmware for 2457”; “CONTROL run: no cube telemetry after the connection”  
> **Effect:** the cube accelerometer and events never arrive  
> **Unresolved:** only the robot can answer it

**CONFIRMED.** I searched the engine cube connection/listener/stream commands,
cube telemetry handlers, Unity cube code, BLE/protocol schemas, configs, OBB
assets, captures, and firmware files.  The engine's `{activeID,1}` request is
settled, but forwarding requires robot and cube firmware.  No cube firmware and
no robot firmware 2457 ships; the cited capture observed no telemetry and cannot
distinguish the missing trigger from unsupported forwarding.

## M3-036 — NV reply and re-request contract

> **Status:** HARDWARE_ONLY  
> **Location:** `cozmo-stack/src/Cozmo.Robot/NvStorage.cs`  
> **Title:** The robot's reply to a factory read with Length = 1, and the non-factory Length = size+16 re-request contract  
> **Evidence:** “pass 1 step 17: what the robot does with Length = 1 is unknown; the Length = 1024 reply looked like tag offsets from StartTag with 56 bytes at index 0, an inference only”; “pass 4 open q5: whether the Length = size+16 re-request is answered from index 1 / byte size+16 is firmware behaviour”  
> **Effect:** the calibration read's reply shape and the multi-blob re-request cannot be judged from the engine alone  
> **Unresolved:** a robot run sending Length = 1 and recording the reply, and a non-factory read large enough to need a re-request  
> **Test:** `M3DeviceTests.M3_036_Hardware`

**CONFIRMED.** I searched both native NV request builders, reply/reassembly code,
CLAD layouts, calibration/NV configs and assets, tests/captures, Unity code, and
all firmware files.  The engine shows the exact outgoing lengths and what reply
shapes it is prepared to consume; it does not generate the robot's index/offset
or chunking decisions.  No capture exercises either requested case and firmware
2457 is absent, so the two proposed robot runs remain necessary.

## Manager action indicated by this recheck

- Reopen M11-016 as recoverable/implementation work and split it if necessary so
  face/recognition, pet, motion, and result publication each own their whole live
  path.  Do not retain `BLOCKED_EXTERNAL` merely because OKAO/OMCV are proprietary.
- Reopen M14-006 as recoverable/implementation work.  Recover the missing APK Java
  bridge first, then extract the shipped Acapela and Wave Portal paths.  The voice
  models and plug-in are present, so an unrelated TTS substitute would violate
  source fidelity.
- Leave the ten `CONFIRMED` records at their current provenance unless a targeted
  robot/original-app capture supplies the missing runtime observation (or firmware
  2457 is later obtained).
