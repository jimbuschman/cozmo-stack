# Cozmo capability-gap analysis: official Anki stack vs PyCozmo

Date: 2026-09-18. Scope: what the shipped Cozmo 3.4.0-1204 Android app + `libcozmoEngine.so` can do,
what PyCozmo (GitHub master, v0.8.0 + unreleased commits) actually implements, and what a complete
replacement stack still has to recover or rebuild.

## 0. Sources and evidence levels

| source | role | how it was used here |
|---|---|---|
| Decompiled C# (Unity, `unity/scripts/csharp`) | **authority** for game↔engine API, enums, config | read directly |
| `libcozmoEngine.so` dynsym (31,633 functions, 700 vtables) + Thumb disassembly | **authority** for engine internals and the engine↔robot protocol | symbol enumeration; `Pack/Size/Set_` disassembly (`tools/robot_tagmap.py`, `tools/robot_sizecmp.py`) |
| OBB `main.1204.com.anki.cozmo.obb` (`cozmo_resources/`) | authority for assets, configs, firmware | **obtained 2026-09-18 and unpacked to `re-analysis/obb/`**; full inventory in `OBB_INVENTORY.md`. Sections marked "OBB update" below were revised from it |
| PyCozmo source, docs, tests, CHANGES | readable RE reference, **unverified until checked** | cloned to scratchpad and read; catalog extracted programmatically |

Every PyCozmo claim below is tagged with the strongest evidence found:

* **S** — implemented in source only
* **D** — additionally documented as supported (README / docs/functions.md)
* **H** — additionally hardware-evidenced (pcap captures, hardware_versions.md logs, contributor-tested tools, examples that exist because someone ran them). No hardware was run for this document.
* **V** — verified against the official implementation in this analysis (symbols, sizes, tags, enums)
* **✗** — absent
* **?** — present but contradicted or unconfirmed by the official code

PyCozmo has unit tests only for: frame/window (transport), image encoder/decoder, anim encoder, robot_debug, emotion decay graph. Nothing else in PyCozmo is covered by tests.

## 1. Headline numbers

| item | official 3.4.0 | PyCozmo master |
|---|---|---|
| engine→robot message types | **105** | 48 |
| robot→engine message types | **56** | 29 |
| PyCozmo packets whose byte size matches the official CLAD `Size()` | — | **75 of 77** |
| PyCozmo packets with wrong size | — | 2 (`TurnToRecordedHeading` 0x92: 0 vs 13 B; `ShutdownRobot` 0xa9: 0 vs 32 B) |
| PyCozmo packets with misleading names | — | 7 (see §2) |
| PyCozmo fields named `unknown*`/`unused` | — | 18 fields in 15 packets |
| official messages PyCozmo does not define at all | — | **84** (57 engine→robot, 27 robot→engine) |
| game→engine / engine→game CLAD messages (Unity API) | 255 / 155 | n/a (PyCozmo bypasses the engine) |
| engine classes/namespaces with exported methods | 1,466 | — |
| engine `Behavior*` classes | ~95 | 0 real behaviors (stubs) |
| engine action classes | ~70 | 0 |
| engine vision modes | 16 | 0 |

Full per-tag comparison: `protocol/robot_protocol_official_vs_pycozmo.txt`. Official tag→type map:
`protocol/robot_protocol_official_tags.json`. PyCozmo catalog: `protocol/pycozmo_packets.json`.

Important framing: **PyCozmo speaks to the robot firmware, replacing the engine.** Everything the
official engine does in software on the phone (vision, planning, behaviors, animation selection,
mood, audio mixing, TTS, docking control loop, memory map) has to exist in the replacement stack. The
robot firmware itself provides only: motor control + odometry, path following, docking servo loop,
IMU/cliff/battery, camera JPEG streaming, OLED and speaker decoding, backpack LEDs, BLE bridge to
cubes/charger, NV flash, OTA. PyCozmo's README list of "on-board functions" is consistent with the
official message set.

## 2. Protocol verification (engine ↔ robot, UDP)

Method: the official union `Anki::Cozmo::RobotInterface::EngineToRobot` exports one `Set_<member>()`
per message; each setter stores the tag constant, which was recovered by disassembly. Each message
type exports `Size() const`; trivially constant ones were compared to PyCozmo's declared layouts.

### 2.1 Confirmed correct in PyCozmo (tag + size)

Motors/path: 0x32 DriveWheels(16), 0x34 MoveLift(4), 0x35 MoveHead(4), 0x36 SetLiftHeight(17),
0x37 SetHeadAngle(17), 0x3b StopAllMotors(0), 0x3c ClearPath(2), 0x3d/0x3e/0x3f AppendPathSeg*(28/32/29),
0x40 TrimPath(2), 0x41 ExecutePath(3), 0x58 StartMotorCalibration(2), 0x60 EnableStopOnCliff(1).
Lights: 0x0b SetHeadLight(1), `LightState` struct 10 B. Camera: 0x57 SetCameraParams(7),
0x66 EnableColorImages(1), 0xf2 ImageChunk(var), 0xf4 ImageImuData(17). Audio: 0x8e AudioSample(744),
0x8f AudioSilence(0), 0x64 SetRobotVolume(2). Anim: 0x8d, 0x91, 0x93(3), 0x94(3), 0x97(var), 0x98(10),
0x99(4), 0x9a(0), 0x9b(1), 0xca/0xcb(1), 0xf1 AnimationState(15). Cubes: 0x04, 0x05(5), 0x08(5),
0x0a(1), 0x10(5), 0xb4(21), 0xb5(8), 0xb6(12), 0xb9(10), 0xce(9), 0xd0(13), 0xd7(9), 0xf3(9), 0xf5(20).
System: 0x4b SyncTime(8), 0x45 (24), 0x81/0xcd NV (var), 0xaf FirmwareUpdate(1026), 0xef Ack(7),
0xee FirmwareVersion(var), 0xed (12), 0xc9 (6), 0xdb BackpackButton(1), 0xdd(4), 0xde(12), 0xc3(0),
0xc4(1), 0xc6(3), 0xd1 MotorCalibration(3), 0xf0 RobotState(91), 0xae WifiOff(1), 0xb0 (var).

### 2.2 Discrepancies PyCozmo must not be trusted on

| tag | PyCozmo name | official member (type) | consequence |
|---|---|---|---|
| 0x33 | `TurnInPlaceAtSpeed` | `driveCurvature` (`DriveWheelsCurvature`, 10 B) | PyCozmo's "turn at speed" is really drive-with-curvature; field semantics likely wrong |
| 0x3a | — (missing) | `turnInPlaceAtSpeed` (`TurnInPlaceAtSpeed`) | the real message is absent |
| 0x39 | `TurnInPlace` (2 unknown bytes) | `setBodyAngle` (`SetBodyAngle`, 20 B) | same size; PyCozmo fields `unknown4/5` unresolved |
| 0x25 | `Enable` | `getMfgInfo` (`GetManufacturingInfo`, 0 B) | it is a request; the 0xed reply is `ManufacturingID` (PyCozmo `BodyInfo`). PyCozmo sends it twice "to trigger BodyInfo" — cargo cult |
| 0x4c | `EnableCamera` | `imageRequest` (`ImageRequest`) | fine functionally, uses `ImageSendMode` {Off, Stream, SingleShot} (C# enum) |
| 0x9f | `EnableAnimationState` | `initAnimController` (`InitController`) | misnamed; it initialises the on-robot animation controller |
| 0xc2 | `RobotDelocalized` | `syncTimeAck` (`SyncTimeAck`, 0 B) | **wrong**: delocalisation is computed by the engine, not sent by the robot. PyCozmo's delocalisation handling is built on a misidentified packet |
| 0xc9 | `HardwareInfo` | `robotAvailable` (`RobotAvailable`, 6 B) | 2 unknown bytes |
| 0xb0 | `DebugData` | `trace` (`PrintTrace`) | name only |
| 0x92 | `TurnToRecordedHeading` (0 B) | 13 B | **size wrong** → animations using this keyframe cannot be streamed correctly |
| 0xa9 | `ShutdownRobot` (0 B) | 32 B | **size wrong** |
| 0x45 | `SetOrigin` | `absLocalizationUpdate` (`AbsoluteLocalizationUpdate`) | 2 unknown fields; this is the engine correcting robot pose after localising to an object/mat, not just "set origin" |

### 2.3 Frame/transport layer

The official transport is generic `Anki::Util::ReliableTransport` over `Anki::Util::UDPTransport`
(with `HeaderPrefix`, optional CRC, multi-part messages, ping/latency stats, configurable timeouts via
`RobotConnectionManager::ConfigureReliableTransport` and the game message `ReliableTransportRunMode`).
The frame magic `COZ\x03RE\x01` is not a literal in the binary; it is assembled by `HeaderPrefix::Set`.
The same transport code appears in the Vector source Digital Dream Labs released (`lib/util/.../transport/`),
which is the correct reference to verify PyCozmo's `frame.py`/`window.py`/`conn.py` (selective-repeat,
window 16, ACK timeout 0.1 s, ping 0.5 s, 5 s robot-side timeout, MAX_FRAME 1051 B). Evidence for
PyCozmo here: **H** (pcap-derived, in daily use by the project) but **not V**; multi-part messages are
not implemented in PyCozmo.

**Resolved (2026-09-18, M1):** `MessageHandler::AddRobotConnection` selects the destination port on
`ConnectToRobot.isSimulated`: physical robot → **5551**, Webots simulator → 5552. PyCozmo was right for hardware.
The transport has since been fully reconstructed from the engine disassembly and the identical Vector
`util/transport` code (fetched from the Vector repository into `reference/anki-util-transport-vector/`); see
`TRANSPORT_SPEC.md`. PyCozmo's framing is correct; its ping payload (16 vs 17 B), ping cadence, window model and
type-0x0a naming differ from the official stack, and it lacks multipart messages.

## 3. Subsystem gap table

Legend for the "PyCozmo" column uses §0 letters. "Rebuild" = must exist in our stack regardless of PyCozmo.

### 3.1 Connection, discovery, reliable transport
* **Official**: Java `WifiUtil` scans SSIDs, C# filters `StartsWith("Cozmo_")`, user types the PSK shown on the robot's face, app pings 172.31.1.1, Unity sends `ConnectToRobot{robotID, ipAddress}` to the engine, engine opens ReliableTransport, receives `robotAvailable`(0xc9) → `firmwareVersion`(0xee) → CLAD-hash check (`EngineRobotCLADVersionMismatch`, strings "Engine to Robot CLAD version hash mismatch…") → `getMfgInfo`/`mfgId`, `syncTime`/`syncTimeAck`, `initAnimController`, `appRunID`(0xa0), `setRTTO`. Engine also supports `appConCfg*`(0xaa–0xad) to push Wi-Fi client/IP configuration to the robot and `setBodyRadioMode`.
* **C#**: `AndroidConnectionFlow`, `ConnectionFlowController`, `RobotEngineManager.ConnectToRobot`, `UiDeviceConnected`, `RobotConnectionResponse`, `EngineRobotCLADVersionMismatch`.
* **Engine**: `RobotConnectionManager`, `RobotInitialConnection::HandleFirmwareVersion`, `Anki::Util::{ReliableTransport, ReliableConnection, UDPTransport, TransportAddress}`.
* **PyCozmo**: transport **H**, handshake **H/?** (uses misnamed 0x25/0xc2; no hash check; no Wi-Fi discovery, relies on OS).
* **OBB needed**: no.
* **Rebuild**: Wi-Fi discovery/association helper per OS, transport (verify against Vector source), handshake incl. CLAD hash check, hardware/firmware identification.
* **Unknowns**: 5551/5552 roles; timeout constants; `appConCfg*` semantics (could allow robot on home Wi-Fi — high value); multi-part message framing.

### 3.2 Robot state / telemetry
* **Official**: robot streams `RobotState`(0xf0, 91 B, ~33 Hz): timestamp, pose frame/origin ids, pose x/y/z/angle/pitch, wheel speeds, head angle, lift height, accel, gyro, battery, cliff raw, status flags (`RobotStatusFlag`: IS_MOVING, IS_CARRYING_BLOCK, IS_PICKING_OR_PLACING, IS_PICKED_UP, IS_BODY_ACC_MODE, IS_FALLING, IS_ANIMATING, IS_PATHING, LIFT_IN_POS, HEAD_IN_POS, IS_ANIM_BUFFER_FULL, IS_ANIMATING_IDLE, IS_ON_CHARGER, IS_CHARGING, CLIFF_DETECTED, ARE_WHEELS_MOVING, IS_CHARGER_OOS). Engine keeps `RobotStateHistory` (time-indexed poses for vision), derives off-treads state (`OffTreadsState`), gyro drift, unexpected movement, and re-publishes an enriched `ExternalInterface::RobotState` (adds carryingObjectID, localizedToObjectID, headTrackingObjectID, lastImageTimeStamp).
* **PyCozmo**: `RobotState` **V** (size), field semantics **H**; derived events (`on_robot_picked_up_change`, orientation) **S**.
* **Rebuild**: history buffer + derived state machine. Low risk.

### 3.3 Wheels / head / lift
* **Official robot messages**: drive(0x32), driveCurvature(0x33), moveLift/moveHead, liftHeight/headAngle (with action ids → `motorActionAck` 0xc4), setBodyAngle(0x39), turnInPlaceAtSpeed(0x3a), stop, `headAngleUpdate`(0x38), `enableMotorPower`(0x50), `setControllerGains`(0x47), `setMotionModelParams`(0x51), `checkLiftLoad`(0x4f)/`liftLoad`(0xda), `motorAutoEnabled`(0xd8), `enableBraceWhenFalling`(0x63), `startMotorCalibration`/`motorCalibration`.
* **Engine**: `MovementComponent` (120 methods), `MoveHeadToAngleAction`, `MoveLiftToHeightAction`, `TurnInPlaceAction`, `DriveStraightAction`, `PanAndTiltAction`, `CalibrateMotorAction`; C# `Robot.DriveWheels/DriveHead/MoveLift/SetHeadAngle/SetLiftHeight/TurnInPlace`.
* **PyCozmo**: basic motors **V/H**; 0x33 mislabeled (**?**); 0x3a, 0x38, 0x47, 0x4f/0xda, 0x50, 0x51, 0x63, 0xd8 **✗**.
* **Rebuild**: action-level wrappers with completion tracking; lift-load check (used to detect carried cube).

### 3.4 Backpack LEDs
* **Official**: `setBackpackLightsMiddle`(0x03, variable), `setBackpackLightsTurnSignals`(0x11), `setBackpackLayer`(0x02, `BackpackLayer` enum) plus `animBackpackLights` keyframes (0x98); engine `BodyLightComponent`, `BackpackLightAnimationContainer` loads `config/engine/lights/backpackLights/*.json`; C# `Robot.SetBackpackBarLED/SetAllBackpackBarLED/SetFlashingBackpackLED`.
* **PyCozmo**: 0x03/0x11 **H** (each with one `unknown` field), `LightState` **V**, light-animation JSON loading **S**; 0x02 **✗**.
* **OBB**: needed for the stock light animations.

### 3.5 OLED display / procedural face
* **Official**: robot accepts `animFaceImage`(0x97, RLE-compressed 128×32 interlaced frames). Engine `ProceduralFace` (19 params per eye: EyeCenterX/Y, EyeScaleX/Y, EyeAngle, Lower/Upper Inner/Outer RadiusX/Y, Upper/LowerLidY/Angle/Bend; plus face position/scale/angle and a "scanline distorter"), `ProceduralFaceDrawer`, `FaceAnimationManager` (sprite face animations from `assets/faceAnimations`), `FaceLayerManager`, keyframe interpolation (`ProceduralFace::Interpolate`), blink/look-at (`LookAt`), `oledDisplayNumber`(0xa3, service display). C#: `DisplayFaceImage`, `DisplayProceduralFace`.
* **PyCozmo**: RLE encoder **V-ish** (unit-tested against captured frames, not against the engine's encoder). *Our M3 decoder is verified against all 28 of those Cozmo-produced sequences; our encoder uses only the two run commands so it is exact by construction — see `DEVICE_LAYER.md`.*, procedural face renderer **S/D** with matching parameter set (**V** for parameter names), but rendering fidelity, scanline effect and interpolation vs engine unverified. *Ours is now reconstructed from `ProceduralFaceDrawer` itself — canvas, eye geometry, corner arcs, lids and both transforms — see [PROCEDURAL_FACE.md](PROCEDURAL_FACE.md) for what was recovered and the three points that were not*; face-animation sprites **✗**.
* **OBB**: `assets/faceAnimations` for sprite faces.

### 3.6 Camera / image streaming
* **Official**: `imageRequest`(0x4c, `ImageSendMode` Off/Stream/SingleShot), `setCameraParams`(0x57), `enableColorImages`(0x66), `defaultCameraParams`(0xc8), `cameraFOVInfo`(0x5a), `ImageChunk`(0xf2) + `ImageImuData`(0xf4); engine `EncodedImage::AddChunk` handles out-of-order/incomplete/timestamp checks and 5 encodings: JPEGGray, JPEGColor, JPEGColorHalfWidth, JPEGMinimizedGray, JPEGMinimizedColor (`MiniGrayToJpeg`, `MiniColorToJpeg` rebuild the stripped JPEG header). Camera calibration persisted in NV (`NVEntry_CameraCalib`, `CalibImage1-6`), `ComputeCameraCalibration` messages, `Vision::CameraCalibration`.
* **PyCozmo**: streaming **H**, mini-gray/mini-color header reconstruction **V** (same algorithm names exist in the engine; unit-tested), plain JPEG modes and half-width **✗**, 0xc8/0x5a **✗**, calibration **✗**.
* **Rebuild**: ~~full chunk reassembly state machine~~ *(done in M3: reassembly, minimized-gray/colour reconstruction, colour flag, gyro pairing — verified by Huffman-decoding every captured frame)*; calibration read from NV, exposure control loop, plain and half-width JPEG modes still to do.

### 3.7 Vision pipeline (engine-only)
* **Official**: `VisionSystem`/`VisionComponent` with `VisionMode` schedule {DetectingMarkers, DetectingFaces, DetectingMotion, DetectingOverheadEdges, ReadingToolCode, ComputingCalibration, CheckingQuality, ComputingStatistics, DetectingPets, EstimatingFacialExpression, DetectingSmileAmount, DetectingGaze, DetectingBlinkAmount, LimitedExposure, DetectingLaserPoints}; `VisionPoseData` ties images to `RobotStateHistory`; `config/engine/vision_config.json`. Marker detection is Anki's own fixed-point library (`Anki::Embedded::*`, 437 exports, `MarkerDetector`, 40 marker types incl. 3 cubes × 6 faces, charger, custom SDK markers). Ground-plane ROI, overhead edge (drivable area) detection, `ImageQuality`/auto-exposure metering.
* **PyCozmo**: **✗** (all TODOs in `brain.py`).
* **OBB**: `vision_config.json`.
* **Rebuild**: entire pipeline. The custom fiducial decoder is the critical piece (cube pose depends on it). Unknown: marker bit layout / code table location (probably `Anki::Vision::MarkerDefinitions` data in `.rodata`).

### 3.8 Face detection
* **Official**: Omron **OKAO Vision** statically linked (`faceRecognizer_okao.cpp`, `OkaoIdentify`) via `Vision::FaceTracker`; outputs `RobotObservedFace`, expression/smile/gaze/blink estimates; `FaceWorld` tracks faces over time.
* **PyCozmo**: **✗**.
* **Rebuild**: with an open detector (proprietary OKAO cannot be reused). Facial-expression/gaze features are optional.

### 3.9 Face recognition / enrollment
* **Official**: OKAO identify + `Vision::FaceRecognizer`, `EnrolledFaceEntry` (JSON album), `BehaviorEnrollFace`, `FaceEnrollmentPose`/`FaceEnrollmentResult`, album saved on robot NV (`NVEntry_FaceAlbumData`, `NVEntry_FaceEnrollData`) and `/enrollData.json`; C# `SetFaceToEnroll`, `EraseEnrolledFaceByID`, `UpdateEnrolledFaceByID`, `LoadFaceAlbumFromFile`.
* **PyCozmo**: **✗**.
* **Rebuild**: enrollment flow + recognizer. Unknown: OKAO album binary format (needed only to import existing robots' data; otherwise start fresh).

### 3.10 Motion / pet / laser / object detection
* **Official**: `MotionDetector` (centroid → ground projection, params in vision_config), `LaserPointDetector`, `Vision::PetTracker`/`PetWorld` (OKAO pet detection), `OverheadEdgesDetector`, custom objects (`DefineCustomBox/Cube/Wall`, `CreateFixedCustomObject`, `ObjectType` CustomType00–19, `CustomFixedObstacle`), `MarkerlessObject`, `ProxObstacle`/`CliffDetection`/`CollisionObstacle` in `BlockWorld`.
* **PyCozmo**: **✗**.

### 3.11 Cubes and cube communication
* **Official robot side**: `setAccessoryDiscovery`(0x0a), `setPropSlot`(0x05, connect/disconnect by factory id), `setCubeID`(0x10), `streamObjectAccel`(0x08), `flashObjectIDs`(0x4d), `objectConnectionStateToRobot`(0x62), `setObjectBeingCarried`(0x4e), `setCubeGamma`(0x0c); events `activeObjectAvailable`(0xf3), `activeObjectConnectionState`(0xd0), moved/stopped/tapped/tappedFiltered/upAxisChanged (0xb4/b5/b6/b9/d7), `objectPowerLevel`(0xce), `objectAccel`(0xf5). A raw BLE tunnel also exists: `bleSendData`(0x26), `bleDisconnect`(0x27), `bleConnectionState`(0x86), `bleDataReceived`(0x87) with `BLE::Frame`/`PayloadFlags` framing.
* **Engine**: `ActiveObject`/`ActiveCube`/`Block`, `CubeAccelComponent`+`CubeAccelListeners`, `BlockTapFilterComponent`, `ObjectPoseConfirmer`, `BlockWorld`, cube firmware protocol union `BlockMessages::LightCubeMessage` {accel, available, flashID, moved, setCubeID, setCubeLights, setObjectBeingCarried, stopped, streamObjectAccel, tapped, upAxisChanged}; C# `ConnectToSpecificCube`, `BlockPool*`, `SaveCubeIDDataToRobot` (`NVEntry_SavedCubeIDs`).
* **PyCozmo**: connect/events/accel **H** (sizes **V**); 0x4d/0x62/0x4e/0x0c and the BLE tunnel **✗**; no world model.
* **Unknowns**: cube OTA (string `OBJECT_OTA_FAIL`), BLE tunnel usage, `setCubeGamma`.

### 3.12 Cube LEDs
* **Official**: `setCubeLights`(0x04, 4 × `LightState`), engine `CubeLightComponent` with layers, blending, white balance (`WhiteBalanceColor`), animation triggers from `config/engine/lights/cubeLights/*.json` and `cubeAnimationGroupMaps`; C# `PlayCubeAnimationTrigger`, `SetActiveObjectLEDs`.
* **PyCozmo**: 0x04 **H**, `LightState` **V**, light-anim JSON **S**.
* **OBB**: yes for stock animations.

### 3.13 Audio playback
* **Official**: robot receives `animAudioSample`(0x8e, 744 samples/frame ≈ 22.05 kHz at 30 fps) and `animAudioSilence`(0x8f), `setAudioVolume`(0x64). Engine runs **Audiokinetic Wwise** (banks `Init.bnk`, `Cozmo.bnk`, `SFX.bnk`, `Music.bnk`, `UI.bnk`, `Dev_Debug.bnk` in `sound/`) and a "HijackAudioPlugin" that captures the Wwise robot bus into `RobotAudioBuffer` → `RobotAudioFrameStream` → audio keyframes (`RobotAudioClient`, `RobotAudioAnimation`, on-device vs on-robot output `RobotAudioOutputSource`). Audio events are posted by animations (`AnimEvent`) and game code (`PostAudioEvent`, 1,000+ event names).
* **PyCozmo**: raw sample streaming **H**, `audiokinetic` .bnk/.wem parsing **S**; no mixer/event model; codec claimed µ-law 8-bit (**H**, not **V**). *M3 implements the streaming side. The codec is now **V**, taken from the engine rather than assumed: `Anki::Cozmo::Audio::encodeMuLaw(float)` at 0x00597AD8 is mu-law with **no 132 bias and no final complement**, so silence is 0x00 not 0xFF. PyCozmo's claim of plain µ-law is wrong in the bias; its omission of the complement is right. Also learned: the robot buffers only ~14 audio frames and drains one every 28.6 ms.*
* **OBB**: yes (all sound).
* **Rebuild**: an event→sound mapping and mixer replacing Wwise (bank format parsing partially exists in PyCozmo; WEM → PCM decoding needs a Vorbis/ww2ogg path); confirm codec by disassembling `RobotAudioBuffer`/`AudioSample` producers.
* **OBB update**: `sound/AudioAssets.zip` holds the six banks named by the engine plus 2,214 `.wem` (1,987 Wwise Vorbis, 227 ADPCM; mostly 48 kHz mono), `SoundbanksInfo.xml` (835 events) and `PluginInfo.xml`. The plugin list settles the voice chain: **Anki Hijack** (robot-bus capture), **Anki Wave Portal** (TTS PCM input), Wwise **Harmonizer** (the Cozmo pitch effect), Parametric EQ, Compressor, Expander, Peak Limiter. Bank `.txt` dumps give human-readable event/switch/RTPC tables, so the event→source mapping can be built from data rather than RE.

### 3.14 Speech / TTS
* **Official**: **Acapela** TTS (`libacattsandroid.so`, licensed; voices in `tts/`, `tts_config.json`), `TextToSpeechComponent` → Wwise event `Play__Robot_Vo__External_Acapela_English_Sentence` with "Cozmo_Voice_Processing" (pitch/character processing) and `SayTextVoiceStyle`/`SayTextIntent`; `SayTextAction` syncs mouth/animation.
* **PyCozmo**: **✗**.
* **Rebuild**: open TTS + voice-processing chain approximating Cozmo's timbre. Acapela voices cannot be redistributed.

### 3.15 Animation playback
* **Official**: `AnimationTrigger` (201 triggers) → `AnimationGroup` (weighted, mood-dependent, cooldowns; strings "All animations in group '%s' were on cooldown…") → `Animation` (tracks: head, lift, body, face/procedural face, backpack lights, robot audio, device audio, event, record/turn-to-heading) → `AnimationStreamer` streams keyframes ahead of time watching `AnimationState`(0xf1, buffer fullness) and `IS_ANIM_BUFFER_FULL`; `enable/disableAnimTracks`(0x9e/0x9d) lets actions lock tracks; `animEvent`(0x95, 0xd5) fires events back to engine; idle and driving animation stacks (`PushIdleAnimation`, `DrivingAnimationHandler`).
* **PyCozmo**: 30 fps `anim_controller` streaming **S/D**, `StartAnimation/EndAnimation` **V**; 0x95/0x96/0x9d/0x9e/0xd5 **✗**; `TurnToRecordedHeading` size wrong (**?**); group/trigger selection **S** (loads maps, no mood weighting).
* **OBB**: essential.

### 3.16 Animation streaming (wire level) — covered above; the missing track-enable and event messages mean PyCozmo cannot mix an animation with a concurrent action (e.g. animate face while driving a path), which the official stack does constantly.

### 3.17 Animation assets
* **Official**: `assets/animations/*.bin` FlatBuffers per `cozmo_anim.fbs`, `animationGroups/*.json`, `animationGroupMaps`, `cubeAnimationGroupMaps`, `faceAnimations`, `RewardedActions`; loaded by `RobotDataLoader::Load*`.
* **PyCozmo**: FlatBuffers reader/writer + JSON round-trip **V-ish** (unit-tested against real files), `pycozmo_anim.py` tool **H**, resource downloader points at this exact OBB (`main.1204.com.anki.cozmo.obb`) **H**.
* **Reuse**: the `.fbs`-derived schema knowledge is reusable; regenerate from the OBB's `cozmo_anim.fbs` in our language rather than depend on PyCozmo.

### 3.18 Action queue / system (engine-only)
* **Official**: `ActionList` (QueueActionNow/Next/AtEnd/AtFront, concurrent actions, duplicate detection, external vs internal), `IAction`/`ICompoundAction`/`RetryWrapperAction`, ~70 actions (docking, driving, turning, tracking, visual verification, say text, play animation, wait), `ActionResult` (45 codes), C# `QueueSingleAction`/`QueueCompoundAction`/`CancelAction`, `RobotCompletedAction`.
* **PyCozmo**: **✗** (direct commands, blocking helpers).
* **Rebuild**: yes; this is the API most of "Cozmo.Engine" hangs off and what an LLM controller would call.

### 3.19 Localization
* **Official**: robot integrates odometry; engine owns world origins (`Robot::UpdateWorldOrigin`, `IsZombiePoseOrigin`), localizes to cubes/mats (`LocalizeToObject`, `LocalizeToMat`) and pushes corrections with `absLocalizationUpdate`(0x45); delocalizes on pickup/fall (`Robot::Delocalize` → `RobotDelocalized` to game). `syncTime`/`syncTimeAck` align clocks.
* **PyCozmo**: origin set + pose reading **H**; localization-to-objects **✗**; misidentifies 0xc2 (**?**).
* **Rebuild**: origin management and object-based relocalisation; requires marker detection.

### 3.20 Memory map / world representation
* **Official**: `MapComponent` + `QuadTree`/`QuadTreeNode`/`QuadTreeProcessor` (`MemoryMapTypes`: cliffs, obstacles, visited, interesting edges), `BlockWorld` (84 methods), `FaceWorld`, `PetWorld`, `ObservableObject`/`ActionableObject`, broadcast to game as `MemoryMapMessage*`; C# `SetMemoryMapRenderEnabled`.
* **PyCozmo**: **✗**.

### 3.21 Navigation / path planning
* **Official**: robot follows segment lists (0x3c–0x41, `PathFollowingEvent`, `PathMotionProfile`); engine plans with `xythetaPlanner`/`LatticePlanner` (motion primitives `mprim.json`), `MinimalAnglePlanner`, `DubbinsPlanner`, obstacle penalties from the memory map, `PathComponent`/`PathDolerOuter`, `DriveToPoseAction`, `DriveToObjectAction`; `goalPose`(0xb3) from robot.
* **PyCozmo**: segment messages **V**, `go_to_pose` straight-line **S/D**; planner **✗**.
* **OBB**: `mprim.json` if reusing Anki's primitives.

### 3.22 Charger detection / docking
* **Official**: `MARKER_CHARGER` + `Charger` object; docking is a robot-side servo loop fed by engine visual error signals: `dockWithObject`(0x42), `dockingErrorSignal`(0x48), `abortDocking`(0x43), `placeObjectOnGround`(0x44), `dockingStatus`(0xd3), `pickAndPlaceResult`(0xb8), `movingLiftPostDock`(0xc5), `setCarryState`(0x49); actions `DockAction`, `PickupObjectAction`, `PlaceOnObjectAction`, `RollObjectAction`, `MountChargerAction`, `BackupOntoChargerAction`, `DriveOffChargerContactsAction`, `AscendOrDescendRampAction`, `CrossBridgeAction` (`DockingMethod`, `DockingResult`).
* **PyCozmo**: **✗** entirely (only IS_ON_CHARGER flag).
* **Rebuild**: marker-based pre-dock pose → dock loop. Unknown: exact `DockingErrorSignal` field semantics (recover from `Pack()` disassembly + C# `DockingErrorSignal` if present).

### 3.23 Behaviors
* **Official**: ~95 `Behavior*` classes, `BehaviorManager`/`BehaviorSystemManager`, `ActivityFreeplay/Sparked/Socialize/GatherCubes/BuildPyramid/Feeding/ExpressNeeds`, `ReactionTrigger` → behavior map (`ReactionTriggerStrategy*`), configs `behavior_system_config.json`, `activities_config.json`, per-behavior JSON; C# `ExecuteBehaviorByID`, `RequestAllBehaviorsList`.
* **PyCozmo**: config loaders **S**, `Brain` with reaction/heartbeat threads **S**, behaviors themselves stubs (TODOs) — README: "work in progress".
* **Rebuild**: all behavior logic; JSON configs from OBB reusable as data.

### 3.24 Emotions / needs / personality
* **Official**: `MoodManager` (`EmotionType`: Happy, Calm, Brave, Confident, Charged, Excited, Social, Winning, WantToPlay; decay graphs; `EmotionEvent` mapper from `emotionevents/`), `NeedsManager` (`NeedId`: Repair, Energy, Play; brackets, rewards, `NeedsStateOnRobot` v01–v04 persisted to robot NV `NVEntry_NurtureGameData`), sparks/unlocks/inventory (`ProgressionUnlockComponent`, `InventoryComponent`, `NVEntry_GameUnlocks`, `NVEntry_InventoryData`), AnkiLab experiments.
* **PyCozmo**: emotion decay **S** (unit-tested), event loading **S**; needs/progression **✗**.
* **Rebuild**: mood + needs models; decide whether to honor existing on-robot save data (format = CLAD `NeedsStateOnRobot_v0x`, recoverable from C#).

### 3.25 Sensors
* **Official**: cliff (`cliffEvent` 0xc0, `potentialCliff` 0xc1, `setCliffDetectThreshold` 0x54, `CliffSensorComponent`, `NVEntry_CliffValOnDrop/OnGround`), IMU (`imuRequest` 0x4a → `imuDataChunk` 0xbf / `imuRawDataChunk` 0xc7, `imuTemperature` 0xdc, `IMUInfo`/`NVEntry_IMUAverages`, `RobotGyroDriftDetector`), `robotStopped`(0xd4), `robotError`(0xd9), `robotPoked`, falling, backpack button, battery (`BatteryPercent` in C#), tool-code reader (`enableReadToolCodeMode`, `ReadToolCodeAction`).
* **PyCozmo**: RobotState-derived cliff/IMU/battery **H**; all event/request messages above **✗** except poked/falling/button.

### 3.26 Firmware query / update
* **Official**: on connect `firmwareVersion`(0xee, JSON signature: version, git-rev, CLAD hashes, wifiSig/rtipSig/bodySig) and `factoryFirmwareVersion`(0xd2); `FirmwareUpdater` states {Init, LoadingFile, Flash, SendFlashEOF, WaitOTAUpgrade}: `GetFirmwareFilename(FirmwareType, version)` under `config/engine/firmware/`, `LoadHeaderData` finds the header terminator with `memchr` and JSON-parses it, then streams `OTA::Write`(0xaf, 2 B chunk id + 1024 B) and waits for `OTA::Ack`(0xef), then robots reboot (`ShouldRobotsBeRebooting`/`HaveAllRobotsRebooted`). Also `bodyEnterOTA`(0x0d), `enterRecoveryMode`(0x30), `killBodyCode`(0x06), `wifiFlashID`(0xec), `getBodySerialNumber`/`bodySerialNum`, body storage read/write (0x0e/0x0f/0xa4).
* **PyCozmo**: `pycozmo_update.py` chunk loop **H** (contributor-tested), sizes **V**; recovery/body-OTA/serial messages **✗**; `.safe` internals unknown (both).
* **OBB**: yes (`cozmo.safe` lives there).
* **OBB update**: seven `cozmo.safe` files are present — current **2381** in `config/engine/firmware/`, plus developer downgrade targets `firmware_1299/1859/1889/2158/2214` and `old_firmware/` (= 2214). Sizes 378–387 KB, JSON header + `\0` + opaque high-entropy body (encrypted or compressed; no image magic found). Hashes and per-version CLAD hashes are tabulated in `OBB_INVENTORY.md §3`. The header format PyCozmo documents is confirmed exact. Selection logic: normal flow uses `FirmwareType.Current` → `firmware/`; the debug pane enumerates `firmware_*` for `FirmwareType.Old`.

### 3.27 NV storage
* **Official**: `commandNV`(0x81) / `nvOpResult`(0xcd), `NVStorageComponent` (size table, word alignment, multi-blob entries, factory-block protection, `WipeAll`/`WipeFactory`), 38 `NVEntry_*` tags (game data, face album, calibration images, IMU, cliff cal, tool codes, playpen results, birth certificate).
* **PyCozmo**: read/erase/write **H/D**, tag enum **S** (numeric values **not V** — only names were recoverable from symbols; values must be confirmed via `NVStorage::NVEntryTag` switch tables or captures).
* **OBB update**: `config/engine/backup_config.json` gives official values for the 8 user-data tags (GameSkillLevels 0x180000 … LabAssignments 0x196000) and they match PyCozmo's `NvEntryTag` → those 8 are now **V**. The 30 factory/calibration tags remain unverified.
* **Rebuild**: component with size table; backup/restore of user data (mirrors C# `RestoreRobotFromBackup`).

### 3.28 Factory / service / debug
* **Official**: `enterTestMode`(0xa2)/`testState`(0xa1)/`factoryTestParam`(0xd6), `startControllerTestMode`(0x46), `generateTestTone`(0x65), `oledDisplayNumber`(0xa3), `sendDTMCommand`(0x12, RF test), `setRTTO`(0x89), `requestCrashReports`(0x80)/`crashReport`(0xcf), `trace`(0xb0)/`printText`(0xb1)/`dataDump`(0xb7)/`timeProfStat`(0xbe)/`mainCycleTimeError`(0xb2), `enableWiFiTelemetry`(0x82), `BehaviorFactoryTest` (playpen), `FactoryTestLogger`, debug console vars/functions (`SetDebugConsoleVar`, `RunDebugConsoleFunc`), viz interface (971 `VizInterface` exports) to a desktop visualizer, `EnableDroneMode`.
* **PyCozmo**: `robot_debug.py` decodes `trace` using `AnkiLogStringTables.json` (**V** for the format-id concept, unit-tested); everything else **✗**.

## 4. Official functionality PyCozmo missed entirely (robot protocol)

Engine→robot (57): adjustTimestamp 01, setBackpackLayer 02, killBodyCode 06, setBodyRadioMode 07,
setCubeGamma 0c, bodyEnterOTA 0d, readBodyStorage 0e, writeBodyStorage 0f, sendDTMCommand 12,
getBodySerialNumber 24, bleSendData 26, bleDisconnect 27, enterRecoveryMode 30, headAngleUpdate 38,
turnInPlaceAtSpeed 3a, dockWithObject 42, abortDocking 43, placeObjectOnGround 44,
startControllerTestMode 46, setControllerGains 47, dockingErrorSignal 48, setCarryState 49,
imuRequest 4a, flashObjectIDs 4d, setObjectBeingCarried 4e, checkLiftLoad 4f, enableMotorPower 50,
setMotionModelParams 51, enterSleepMode 52, powerState 53, setCliffDetectThreshold 54,
enableReadToolCodeMode 55, rollActionParams 59, cameraFOVInfo 5a, forceDelocalizeSimulatedRobot 61,
objectConnectionStateToRobot 62, enableBraceWhenFalling 63, generateTestTone 65, requestCrashReports 80,
enableWiFiTelemetry 82, bleConnectionState 86, bleDataReceived 87, setRTTO 89, animEvent 95,
animEventToRTIP 96, disableAnimTracks 9d, enableAnimTracks 9e, appRunID a0, testState a1,
enterTestMode a2, oledDisplayNumber a3, bodyStorageContents a4, bodySerialNum a5, appConCfgString aa,
appConCfgFlags ab, appConCfgIPInfo ac, appConGetRobotIP ad.

Robot→engine (27): printText b1, mainCycleTimeError b2, goalPose b3, dataDump b7, pickAndPlaceResult b8,
rampTraverseStarted/Completed ba/bb, bridgeTraverseStarted/Completed bc/bd, timeProfStat be,
imuDataChunk bf, cliffEvent c0, potentialCliff c1, movingLiftPostDock c5, imuRawDataChunk c7,
defaultCameraParams c8, crashReport cf, factoryFirmwareVersion d2, dockingStatus d3, robotStopped d4,
animEvent d5, factoryTestParam d6, motorAutoEnabled d8, robotError d9, liftLoad da, imuTemperature dc,
wifiFlashID ec.

Field layouts for all of these are recoverable: each type exports `Pack`/`Unpack` (field order and
widths from the store/load sequence) and many have same-named C# twins in `Anki.Cozmo.ExternalInterface`
or `Anki.Cozmo` with field names (e.g. `ObjectMoved`, `CameraCalibration`, `IMURequest`, `SetBodyRadioMode`,
`StartControllerTestMode`, `ObjectConnectionStateToRobot`, `FlashObjectIDs`).

Engine-level functionality with no PyCozmo counterpart at all: action queue, docking/pick-up/roll/stack,
planner + memory map, all vision, face/pet recognition, TTS, Wwise event audio, needs/sparks/unlocks,
behaviors/activities, idle/driving animation layers, cube light layers, viz, debug console, SDK server,
backup/restore.

## 5. What PyCozmo does save us

Use as **reference and test oracle**, never as a runtime dependency:

1. Frame/packet framing, sequence/ACK semantics and connection handshake as observed on hardware (`docs/protocol.md`, `frame.py`, `window.py`, `conn.py`, `tests/test_frame.py`, `tests/test_window.py`). Verify against Vector's open-source `ReliableTransport`.
2. Field names for 75 messages whose sizes now match the official code (adopt names but re-derive the 18 unknown fields).
3. Mini-JPEG header reconstruction (`camera.py`) — same algorithm the engine names `MiniGrayToJpeg`/`MiniColorToJpeg`.
4. Display RLE encoder and the 128×32 interlace rule (`image_encoder.py`, unit-tested).
5. Animation FlatBuffers schema handling and JSON round trip (`anim_encoder.py`, `CozmoAnim/`).
6. Procedural face parameter model (names match the engine's 19-per-eye set).
7. NV flash semantics (erase-before-write, page behaviour) and the backup key list.
8. Firmware OTA chunk protocol (`pycozmo_update.py`) and the `.safe` JSON header format; ESP8266 flash map (`docs/esp8266.md`).
9. `robot_debug.py` trace decoding.
10. Hardware-version notes (HW 4/5/6/7, factory firmware 10501/10700, body colours).
11. The pointer to a downloadable copy of this exact OBB (`tools/pycozmo_resources.py`, `OBB_URL`).

## 6. Unknowns that need further reverse engineering (ordered by blocking impact)

1. ~~Field layouts of the 84 missing messages.~~ *Resolved in M2* — all 161 messages now have layouts in `protocol/cozmo_robot_protocol.json`; what remains is semantics for 173 placeholder-named fields and 2 unresolved messages. See `PROTOCOL_STATUS.md`.
2. ~~Official transport constants and multi-part message framing; confirm 5551/5552.~~ *Resolved in M1* — see `TRANSPORT_SPEC.md`.
3. `AbsoluteLocalizationUpdate` and `SetBodyAngle` unknown fields; `RobotAvailable` bytes.
4. Fiducial marker encoding/decoding tables (`Anki::Vision::MarkerDefinitions` / `Anki::Embedded`).
5. `DockingErrorSignal` semantics and the docking state machine (`IDockAction`, `DockingComponent`).
6. Audio sample codec/sample-rate confirmation for the robot link (the 744-sample frame). *OBB update:* the source side is now known — Wwise Vorbis/ADPCM `.wem`, bank `.txt` event tables, and the Hijack/Harmonizer plugin chain — so only the robot-frame encoding still needs confirming.
7. `NVEntryTag` numeric values and the per-tag size table (`NVStorageComponent::InitSizeTable`). *OBB update:* 8 user-data tags verified from `backup_config.json`; 30 factory tags remain.
8. `.safe` container structure (three controller images + signatures) — needed for custom firmware; not needed for stock OTA. *OBB update:* bodies confirmed opaque (encrypted/compressed, no image magic); six historical versions available for differential analysis.
9. `appConCfg*` (robot as Wi-Fi client?) and `setBodyRadioMode`.
10. Cube BLE tunnel and cube OTA.
11. Engine's expected CLAD hashes (stored as bytes, not strings). *OBB update:* the shipped current firmware is 2381 with hashes `9e4a965a…`/`a259247f…`; the engine must match these, so the open question is reduced to confirming the byte constants in the binary.
12. OKAO face-album binary format (only if importing existing enrolled faces).

## 7. Proposed architecture for the replacement stack

Evidence-driven changes to the suggested layering: (a) almost every engine subsystem is data-driven
from the OBB, so a first-class **Assets** layer is required; (b) vision and audio are large, replaceable
third-party-backed pipelines and should not live inside Engine; (c) verification against the official
stack is the project's core risk, so a **Conformance** layer (captures, replay, protocol diff) is part of
the product, not tooling.

```
Cozmo.Protocol      generated from ONE declaration file (tags, sizes, fields, enums, CLAD hashes);
                    covers all 161 robot messages + LightCubeMessage + NV tags; unknown fields are
                    explicit `raw` slots; every message carries its official Size() as a test.
Cozmo.Transport     UDP frames, selective-repeat window, ACK/retransmit, ping/RTT, multi-part,
                    Wi-Fi discovery/association adapters (Android/Win/Linux), connection handshake
                    (version + hash check, mfg info, sync time, anim-controller init).
Cozmo.Robot         thin typed facade over the firmware: Motors, Sensors (state history, cliff, IMU,
                    battery, button), Display (RLE, interlace), Camera (chunk reassembly, 5 encodings,
                    calibration), Cubes (discovery/connect/accel/taps/lights, BLE tunnel), Speaker
                    (frame streaming), NV, Power/OTA primitives, Debug (trace decoding).
Cozmo.Assets        OBB loader: animations (FlatBuffers), groups/maps, light anims, face sprites,
                    sound banks (.bnk/.wem→PCM), configs (vision, behaviors, needs, tts), firmware files;
                    integrity via allAssetHash.txt.
Cozmo.Vision        marker detector (custom fiducials), pose estimation, face detect/recognise
                    (open backend), motion/laser/pet, edge detection, image quality/exposure; pluggable.
Cozmo.Audio         event→sound resolver replacing Wwise, mixer, robot bus capture → AudioSample frames,
                    TTS backend + Cozmo voice processing.
Cozmo.Engine        ActionQueue + actions; AnimationStreamer (tracks, locks, events, idle/driving
                    layers, procedural face); Localization/WorldOrigins; BlockWorld/FaceWorld/MemoryMap;
                    Planner + PathComponent; Docking controller; Behaviors/Activities/Reactions;
                    Mood/Needs/Progression; public typed API (the LLM/autonomy surface).
Cozmo.Firmware      version/signature parsing, stock OTA (state machine mirroring FirmwareUpdater),
                    recovery/body-OTA modes, later .safe analysis and custom images.
Cozmo.Conformance   pcap capture + annotate (replaces pycozmo_dump), replay, official-vs-ours protocol
                    diff (from re-analysis/protocol/*), hardware smoke tests.
Cozmo.App           user application over Cozmo.Engine.
```

Language: not decided here, but Protocol must be generated (PyCozmo's AST-to-code approach is a good
model), and Robot/Engine must not depend on Python at runtime.

## 8. Recommended next single milestone

**M1 — Verified protocol + transport core, proven on hardware.**

Deliverables:
1. `Cozmo.Protocol` declaration covering **all 161 robot messages** with official tags and sizes as
   generated tests; fields filled for the 75 PyCozmo-matching messages (renamed per §2.2), the 2 wrong
   sizes fixed, and the 84 missing messages laid out from `Pack/Unpack` disassembly plus C# twins,
   with any still-unknown bytes as explicit raw fields.
2. `Cozmo.Transport` with selective-repeat reliability, verified against Vector's open-source
   `ReliableTransport` and against pcap captures of the official app talking to a robot (this also
   settles 5551/5552, timeouts and multi-part).
3. Hardware smoke test: associate to `Cozmo_*`, connect, complete the official-style handshake
   (version/hash check, mfg info, sync time, anim-controller init), stream `RobotState` and camera for
   60 s with zero window resets, drive a path segment, run one animation from the OBB
   (`re-analysis/obb/assets/cozmo_resources/assets/animations`, 993 clips now available), cleanly disconnect.

Why this first: every other subsystem (docking, vision, animation with track locks, IMU/cliff events,
firmware modes) is blocked on messages PyCozmo never defined, and PyCozmo's handshake rests on two
misidentified packets. Nothing above the protocol can be verified until the wire layer is known to be
correct. The OBB is now on disk (see `OBB_INVENTORY.md`), so the only remaining external dependency
for this milestone is a robot on firmware 2381. In the event the robot available for testing runs 2457,
a 2025 Digital Dream Labs build; everything below was verified against that, not against 2381.

**M1 status (2026-09-18):** transport core implemented in `../cozmo-stack/` (C#, .NET 9): frame codec,
sequence/ack/resend state machine ported from the official code with the engine's tunables, UDP transport,
typed handshake/telemetry/command messages, official 161-message catalog, conformance CLI (decode/diff/fixtures/
pcap/replay/fakerobot/connect). PyCozmo's hardware-captured frames round-trip byte-identically,
and the loopback smoke test against the fake robot passes (connect, identity, handshake, telemetry, SetHeadAngle
acked and reflected, clean disconnect). Deliverable 1 is scoped to the conformance set as instructed; the full
161-message field layouts are the next stage (official field widths for 78 of them are already in
`protocol/robot_msg_field_widths_official.json`).

**M2 status (2026-09-18): protocol layer complete and generated.** The canonical definition
`protocol/cozmo_robot_protocol.json` now carries all **161** official robot messages with tag, direction,
byte layout, field names and their evidence source, variable-length encoding, subsystem, probe-safety class,
layout confidence and hardware-verification status. The C# types and codecs are **generated** from it
(`tools/gen_protocol.py` -> `Cozmo.Protocol/Generated/RobotMessages.g.cs`, 161 classes + 7 structs + 9 enums;
the definition records 24 enums, of which 9 are referenced by a field and therefore emitted),
not hand-maintained. 159 of 161 have a complete byte layout; the 2 that do not (TestState 0xA1,
AppConnectConfigString 0xAA, both factory/destructive) keep an explicit raw tail rather than a guess.
Verification after the 2026-09-18 subsystem probe: **24 hardware verified**, 4 capture verified, 56 statically
verified, 75 layout-known/semantics-uncertain, 2 unresolved, **0 conflicts**. Identity, logging, robot state,
IMU, camera, animation, cubes and head motion are all confirmed on the real firmware-2457 robot.
Tests assert every fixed message serialises to the engine's own `Size()`, every message round-trips, and every CLAD
payload in the 20 s hardware capture decodes and re-encodes byte-identically. Full detail: `PROTOCOL_STATUS.md`.

**M3 status (2026-09-18): COMPLETE and FROZEN.** Camera, face display and audio all passed on hardware. On the firmware-2457 robot
a real photograph was captured and saved, a known image was shown on the OLED, and a generated tone played
cleanly through the speaker. Five faults were found and fixed along the way, the last and only audible one
being that Cozmo's mu-law is not G.711. Details in `DEVICE_LAYER.md`. Among them: the robot
silently discards face and audio keyframes until `initAnimController` (0x9F) starts its animation
controller, and the first ~11 camera frames after a stream starts are torn while the sensor locks (they
decode perfectly but roll by one macroblock row per frame). The mu-law audio codec moves from hypothesis to
**verified** as a result. Details: `DEVICE_LAYER.md`.

**M3 build notes:** `cozmo-stack/src/Cozmo.Robot`
turns the verified protocol into three stateful pipelines plus a live robot-state view: `CozmoCamera`
(chunk reassembly and minimized-JPEG reconstruction, including the colour flag in payload byte 0),
`CozmoDisplay` (128x32 face bitmap, exact run-length codec) and `CozmoAudio` (mu-law, 744-sample frames,
paced at the animation tick). 150 tests pass, including the 28 image/byte-sequence pairs captured from
Cozmo's own face encoder and a full Huffman decode of every camera frame in the hardware capture. Hardware
acceptance commands `camera`, `face` and `tone` are in the conformance CLI and not yet run. Detail and the
open gaps: `DEVICE_LAYER.md`.

**M5 original description (superseded by the frozen status above).**
`robot.Animations` loads Cozmo's own assets (289 FlatBuffers clips and 507 JSON groups in this build) and
plays them on one 30 Hz scheduler that owns all animation timing; the device classes keep their APIs but no
longer decide when anything happens. Track ownership stops two animations fighting: a clip claims the tracks
it touches and a second either replaces it or is refused. `robot.Face` implements the engine's own
19-parameter eye model, whose names and order come from libcozmoEngine .rodata at 0x00C1D399, with a
renderer and a set of named expressions that are **ours, not Anki's**. Arc body motion is now sent the way the engine
sends it, as a speed and a 16-bit radius the firmware turns into geometry, and is hardware-verified. Audio
runs on the scheduler's own tick from a pluggable source; backpack-light keyframes are decoded and carried
but not acted on, because the asset colour encoding is unestablished. Detail: `ANIMATION_LAYER.md`.

**M6 status (2026-09-18): COMPLETE and FROZEN, hardware-verified.** Cozmo's own shipped sounds play from the OBB assets. The chain from
an animation's audio event id through the Wwise banks to a `.wem` is decoded and verified library-wide, and
both codecs decode: **all 2019 Wwise Vorbis files rebuild and decode (100%)** and 220 of 227 ADPCM files
decode, totalling 1h40m of audio with no clipping and no empty output. Wwise strips the Ogg container, the
codebooks and the granule positions; the codebooks come from ww2ogg's packed library (BSD-3-Clause, vendored
with provenance and SHA-256), the rebuild is a port of the parts of ww2ogg these files need, granules are
computed inline, and NVorbis (MIT) decodes the result. **Unlike M1-M5 there is no native authority here**:
Wwise is not linked into `libcozmoEngine.so` at all, so the format rests on cross-checks against the assets.
On hardware, `anim_bored_01` played its original shipped Cozmo sound automatically with no manual WAV
mapping. The 7 stereo ADPCM files decode as of 2026-09-21: the 72-byte block is two 36-byte mono blocks
side by side (M6-003). **Deferred and non-blocking**: 21 media ids with no file behind them, 46
bank-embedded plugin blobs that are not audio, and 90 events that reach no Sound (all music). Detail:
`WWISE_AUDIO.md`.

**M7 status (2026-09-18): CODE COMPLETE - HARDWARE ACCEPTANCE PENDING.** The reactive foundation, built
on frozen M1-M6. Anki's own trigger table resolves all 573 shipped triggers to a present animation; four
reactions (cliff, picked up, placed on charger, falling) run through M5 and M6; the keep-alive layer uses
all 30 tunables disassembled out of AnimationStreamer::SetDefaultParams and yields per track as the engine
does; and an explicit arbiter puts caller above reaction above idle with autonomy off by default. Mood was
recovered as data and stopped there, because every one of the 1047 shipped animation-group entries is
"Mood": "Default" and so selection is mood-invariant in this build. One assumption is carried openly: that
each ReactionTrigger plays the identically-named AnimationTrigger, since the native BehaviorReactToX
classes are not exported. Nothing has been run on a robot. Detail: `BEHAVIOR_LAYER.md`.

**M1 through M6 are frozen as of 2026-09-18.** Transport, protocol, device layer, control layer, the
animation layer and the audio-asset layer are all hardware-verified and are not to be reopened unless a specific failure appears.
Work above them builds on these APIs rather than changing them.

**M5 status (2026-09-18): COMPLETE and FROZEN.** `anim_bored_01` plays through with head, lift, body and
face, and `anim --arc` drives a visible curve and an equal arc back. The last two faults were one cause:
the engine buffers exactly one audio message on every streamed animation frame, `animAudioSample` (0x8E)
or `animAudioSilence` (0x8F), and those frames are what carry an animation forward on the robot. We were
sending none for clips with no audio track, so an opened animation was never fed — the arc never moved and
the face stopped displaying. Working: `DIAGNOSTIC_animation_start_sequence.md`. 255 tests pass.

Seven items are **deferred, not blocking**: Wwise bank and media decoding, enhanced backpack-light
keyframes, exact Anki procedural-face renderer fidelity, pre-rendered `faceAnimations`, group cooldown
enforcement, the unresolved lift 0 mm semantics, and M4 cube hardware acceptance when a cube is available.
Each needs evidence or hardware we do not yet have, rather than being a defect in what is built. The table
with what would close each one is at the end of `ANIMATION_LAYER.md`.

**M4 status (2026-09-18): COMPLETE and FROZEN.** Sensors and state, lights, head and lift motion, and
wheel drive all passed on the firmware-2457 robot, the last with `--allow-drive` and with the robot's
stop-on-cliff reflex enabled first. Cubes are **code-complete and offline-tested with hardware acceptance
pending**, only because no cube was available; that is not a failure or a gap in the implementation. Results:
`ACCEPTANCE.md`. Original description: `Cozmo.Robot` now exposes `Motion`, `Lights`, `Sensors` and `Cubes` over the frozen M1/M2 baseline:
wheels, head, lift, a checked stop, cliff sensing, IMU, charger and battery state, backpack LEDs, the
headlight, and cube discovery with connection state and basic telemetry. Positioning waits for the robot's
own `MotorActionAck` carrying that action's id; wheel commands are confirmed against the reported wheel
speeds; a command that is not confirmed reports a timeout rather than success, and one issued before the
robot is ready sends nothing at all. 177 tests pass, 27 of them new, all offline. Uncertainties are
preserved rather than resolved by guesswork: the lift reading, the IMU units, which physical corner each
cliff sensor is, and the cube battery scale are all surfaced raw. Detail: `CONTROL_LAYER.md`.

**M1 hardware smoke test: PASSED (2026-09-18)** on a hardware-1.5 Cozmo running firmware **2457** (a 2025 Digital
Dream Labs build, newer than the 2381 shipped in this APK): direct connection from our code, stable handshake, 722
RobotState frames at 33.5 Hz over 20 s with zero resends, SetHeadAngle acked and reached, clean disconnect; no
Android app, engine library or Python in the path. Details and captures: `TRANSPORT_SPEC.md §10`, `captures/`.
Two consequences for the plan: (a) fw 2457's CLAD hashes differ from 2381, so the replacement stack must tolerate the
mismatch and the next stage must verify each message layout against the robot rather than assume 2381; (b) the robot
recalibrates head and lift on every connect, which the engine layer must wait out before issuing motor commands.
