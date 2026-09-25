# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **289 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 177 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 16 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 0 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 52 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
| COMPATIBILITY_POLICY | 27 | A deliberate product or platform decision this stack intends to keep: offline tools, the test harness, PC-side plumbing, or a stand-in the operator has to ask for. Not a place to put fidelity work that is hard. |
| HARDWARE_ONLY | 9 | No shipped artifact can settle it; only a robot, or a recording of the stock app, can. |
| BLOCKED_EXTERNAL | 8 | The answer lies in third-party code or data that is not in the package (Omron OKAO, the Wwise runtime DSP, the Acapela text-to-speech engine). |

## Where each subsystem stands

Four separate things, because one word cannot carry them. **Source read** means nothing is left that
reading the original would settle. **Built** means nothing the original is known to do is knowingly
not done here. Neither says the behaviour is faithfully reproduced: the last two columns are what
remains after both, and they do not go away by working harder on this repository.

| subsystem | records | to read | to build | blocked externally | needs hardware | source read | built |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| M1-transport — UDP transport and reliability | 43 | 0 | 1 | 0 | 2 | yes | no |
| M2-protocol — CLAD messages and protocol helpers | 17 | 0 | 1 | 0 | 0 | yes | no |
| M3-device — Camera, display and audio device layer | 24 | 0 | 6 | 0 | 2 | yes | no |
| M4-control — Motion, sensors, lights and cubes | 24 | 0 | 10 | 0 | 3 | yes | no |
| M5-animation — Animation clips, scheduler and face | 36 | 0 | 34 | 0 | 1 | yes | no |
| M6-wwise-bank — Wwise bank reading and codecs | 7 | 0 | 0 | 0 | 0 | yes | yes |
| M7-behaviour — Idle, mood and reactions | 17 | 0 | 0 | 0 | 0 | yes | yes |
| M8-framework — Behaviour framework and scoring | 10 | 0 | 0 | 0 | 0 | yes | yes |
| M9-wwise-music — Wwise music, the MIDI sampler and singing | 27 | 0 | 0 | 6 | 1 | yes | yes |
| M10-derived — Derived robot state and reaction strategies | 9 | 0 | 0 | 0 | 0 | yes | yes |
| M11-vision — Markers, camera geometry and BlockWorld | 20 | 0 | 0 | 1 | 0 | yes | yes |
| M12-manipulation — Docking, carrying and pre-action poses | 16 | 0 | 0 | 0 | 0 | yes | yes |
| M13-navigation — Planning, charger and block configurations | 15 | 0 | 0 | 0 | 0 | yes | yes |
| M14-faces — Face and pet pipeline | 7 | 0 | 0 | 1 | 0 | yes | yes |
| M15-freeplay — Needs, activities and freeplay | 12 | 0 | 0 | 0 | 0 | yes | yes |
| tools — Conformance CLI and offline tools | 5 | 0 | 0 | 0 | 0 | yes | yes |

## Evidence process

Separate from both columns above (AGENTS.md, "Process"). An **UNREVIEWED** subsystem's records and
flags were written before the evidence process; nothing in this report vouches for them, and its
"source read" and "built" say only what those records claim. **Uncited** counts the settled records
(EXACT_SOURCE or EQUIVALENT_IMPLEMENTATION) whose evidence names no address and no file: a bare symbol
name or prose. **Verified** counts records a capture or a robot has agreed with; that never changes a
status.

| subsystem | review | settled | uncited | capture verified | hardware verified |
| --- | --- | ---: | ---: | ---: | ---: |
| M1-transport | INVENTORY_APPROVED | 31 | 0 | 0 | 3 |
| M2-protocol | INVENTORY_APPROVED | 15 | 0 | 0 | 0 |
| M3-device | INVENTORY_APPROVED | 12 | 0 | 0 | 0 |
| M4-control | INVENTORY_APPROVED | 9 | 0 | 0 | 0 |
| M5-animation | INVENTORY_APPROVED | 0 | 0 | 0 | 0 |
| M6-wwise-bank | UNREVIEWED | 7 | 5 | 0 | 0 |
| M7-behaviour | UNREVIEWED | 17 | 3 | 0 | 0 |
| M8-framework | UNREVIEWED | 6 | 1 | 0 | 0 |
| M9-wwise-music | UNREVIEWED | 20 | 14 | 0 | 0 |
| M10-derived | UNREVIEWED | 9 | 0 | 0 | 0 |
| M11-vision | UNREVIEWED | 17 | 5 | 0 | 0 |
| M12-manipulation | UNREVIEWED | 16 | 5 | 0 | 0 |
| M13-navigation | UNREVIEWED | 15 | 5 | 0 | 0 |
| M14-faces | UNREVIEWED | 6 | 0 | 0 | 0 |
| M15-freeplay | UNREVIEWED | 12 | 1 | 0 | 0 |
| tools | UNREVIEWED | 1 | 0 | 0 | 0 |

## Still to read: every RECOVERABLE_GAP

Each of these is a question the original can answer and nobody has asked it yet.

## Still to build: every IMPLEMENTATION_GAP

Each of these is a question already answered. The original's behaviour is established and this stack knowingly does something else, so the work outstanding is writing it, not reading.

### M1-transport — UDP transport and reliability

**M1-029 — Firmware version check against the shipped firmware header** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`
* effect: a robot is accepted or refused as outdated where the app decides otherwise
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: G5.1..G5.6 HandleFirmwareVersion 0x0052D470: guard (0x0052D478..0x0052D484); JSON parse (0x0052D4BA); FACTORY build path (0x0052D4C2..0x0052D506); v = version, t = time (0x0052D51A..0x0052D536); expected E_v/E_t at +0x1C/+0x20 (0x0052D538); sim flag (0x0052D53E..0x0052D5B6); G5.9/G5.10 no robotAvailable and not sim -> result 1 (0x0052D7B8..0x0052D850); sim -> 0 (0x0052D7C6); G5.11 robotDev = v == t, appDev = E_v == E_t; if they differ -> 3 OutdatedFirmware (0x0052D7DE teq.w r0,r1; 0x0052D7E4 movs r0,#3); G5.12 unsigned: E_v == v -> 0; E_v > v -> 3; E_v < v -> 4 OutdatedApp (0x0052D8DA..0x0052D8E0; 0x0052D9A6..0x0052D9AC); time is not compared again; G5.14/G5.15 E_v/E_t are copied from RobotManager+0x84/+0x88 in AddRobot (0x0052EEF2/0x0052EEF8; ctor 0x0052D196); the RobotManager ctor zeroes them (0x0052E512, 0x0052E516); G5.16..G5.20 RobotManager::Init -> FirmwareUpdater::LoadHeader starts a loader thread (0x0052E7F8; pthread_create 0x0067692A) that reads config/engine/firmware/cozmo.safe and parses the JSON header in the first 0x800 bytes (0x00677C44..0x00677D34); ParseFirmwareHeader stores version -> +0x84, time -> +0x88 (0x0052EA36..0x0052EA9A); G5.21/G5.32..G5.37 Scope 1 is DataPlatformResourcesPath = persistentDataPath/cozmo/cozmo_resources (pathToResource 0x0084BE34 table 03 14 22 2f 41; unity/scripts/csharp/PlatformUtil.cs:5-13), extracted from the shipped assets (re-analysis/obb/assets/resources.txt:2075); the shipped header has version 2381, time 1546972025 (re-analysis/obb/assets/cozmo_resources/config/engine/firmware/cozmo.safe offsets 0-445); G5.22..G5.30 nothing orders the header load before AddRobot: the loader starts in cozmo_startup before the engine thread (0x006661EA, 0x0065B14E), and neither the ConnectToRobot handler nor AddRobot checks the load (0x004ED026..0x004ED1EC; loaded flag +0x18 unread on that path); G5.31 a missing, short or unparsable file leaves E_v = E_t = 0 for the session (0x006764E4, 0x00677C50..0x00677D28); G5.38..G5.40 no writer of RobotManager+0x84/+0x88 besides the ctor and ParseFirmwareHeader was found; the scan cannot prove absence (adjusted-base, register-offset, whole-object and untyped accesses are outside it); see decision D7
* outstanding: the mechanism is built (batch 3); jsoncpp's grammar (comments, trailing commas) and asUInt of a non-number are not in the rows, so the reader's leniency is MISSING; under M1-040 the outcome never refuses; then EXACT_SOURCE

### M2-protocol — CLAD messages and protocol helpers

**M2-002 — RobotStatusFlag names and values, and where the engine stores each consumed bit** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Sensors.cs`
* effect: a status bit is read with the wrong meaning
* rests on: compared against re-analysis/inventory/M2-protocol.md on 2026-09-24: the 17 RobotStatusFlag names and values in MessageExtras.cs match EnumToString 0x007D57C8 / Unity RobotStatusFlag.cs:8-25, and the whole status word is kept with the latest RobotState (RS11). The per-bit storage the title also claims (MovementComponent+9, robot+0x349, SetOnCharger, ...) is not reproduced as such here; the inventory assigns what the engine does with those fields to M4.
* best authority: decompiled Unity
* evidence: EnumToString(RobotStatusFlag) 0x007D57C8: 17 names and values, agreeing with Unity RobotStatusFlag.cs:8-25; RS11 the whole status word -> robot+0x350 (0x00512AD8..0x00512ADC); bit storage: 0x1 MovementComponent+9 (0x0063E30A); 0x2 Delocalize argument (0x00512B98); 0x4 [robot+0x280]+4 (0x00512A96); 0x8 robot+0x349 (0x00512AA2); 0x10 robot+0x34C (0x00512ACE); 0x20 treads classifier (0x00511EC4); 0x100 MovementComponent+0xB = !bit (0x0063E32C); 0x200 +0xA = !bit (0x0063E320); 0x1000 SetOnCharger (0x00512AAC); 0x2000 robot+0x339 (0x00512AB8); 0x4000 CliffSensorComponent+6 (0x00634026); 0x8000 MovementComponent+0xC (0x0063E338); 0x10000 robot+0x33A (0x00512AC2)
* outstanding: the names and values match. Per-bit storage after the M4 batch (2026-09-25): the M4 consumers now keep what the engine keeps - MC+0xA/+0xB/+0xC from !HEAD_IN_POS, !LIFT_IN_POS and ARE_WHEELS_MOVING (CozmoMotion), CliffSensorComponent+6 from CLIFF_DETECTED (CozmoSensors), the IS_BODY_ACC_MODE count and the stored status word before the origin check (EngineRobot.StoredState, read by BodyLightComponent for IS_ON_CHARGER, IS_CHARGING and IS_CHARGER_OOS); the other bits are read from the latest handled state by their consumers. Since M4 correction C3 an origin-rejected synced state also reaches the devices, as the engine stores these bits before the origin check. Residuals: SetOnCharger (0x1000) has no charger-platform counterpart (M4-019); 0x2 (the treads-path Delocalize), 0x4 ([robot+0x280]+4) and 0x8/0x20 (the treads classifier) belong to M10/M12

### M3-device — Camera, display and audio device layer

**M3-001 — JPEG reconstruction headers (gray 324 B, colour 334 B 4:2:2), height/width BE at 0x5E..0x61, 240x320 decode check, frame timestamp = last chunk** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/MiniJpeg.cs`
* effect: a camera frame is reconstructed or rejected differently
* rests on: compared against re-analysis/inventory/M3-device.md on 2026-09-24 and reproduced by the code (M3 batch): the MiniJpeg header tables match the inventory's SHA-256 prefixes; height/width BE at 0x5E..0x61; EncodedImageDecoder.TryDecodeGray dispatches 3/4 (unsupported), 5/6 (JPEG), 7 (JPEG plus 160-column borders), 8, and 9 (the half-width colour JPEG to gray, then INTER_LINEAR to 320 x 240), and rejects anything but 240 x 320 (BadDecode); the frame timestamp is the last chunk's. VisionSystem decodes through it.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: A13 header tables 0x00C48C40 (gray, 0x144 B) and 0x00C48D84 (colour, 0x14E B): SOF0 at 0x59, 1 or 3 components (Y 0x21, Cb/Cr 0x11); A12 MiniToJpegHelper writes height BE at 0x5E/0x5F and width BE at 0x60/0x61 (0x004F31C4..0x004F32CC); A8 gray dispatch tbh 0x004F2898; A11 rows==240, cols==320 else BadDecode (0x004F2CDA..0x004F2D34); ts = EncodedImage+0xC
* outstanding: MISSING: A8 case 2 (ToGray of a raw RGB payload; the conversion is not in the rows), case 1 with a payload that is not exactly 320 x 240 bytes, and encodings 0 and above 9 (outside the tbh table): each is reported as a decode failure. The JPEG entropy decode is StbImageSharp in place of OpenCV's libjpeg (last-bit pixel differences possible); whether that library substitution is EQUIVALENT_IMPLEMENTATION is the manager's call.

**M3-010 — encodeMuLaw(float) exactly; no volume scaling; short frames zero-padded** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Audio.cs`
* effect: audio sounds different
* rests on: compared against re-analysis/inventory/M3-device.md on 2026-09-24 and reproduced by the code (M3 batch): AnkiMuLaw.Encode(float): NaN gives 0; f <= -1 gives -32767, else trunc(min(f, 1) * 32767); mag = s ^ (s >> 15); the exponent from the segment table; the mantissa mag >> 4 when mag >> 8 is 0, else (mag >> (exp + 3)) & 0xF; sign 0x80. No volume on the path; short frames zero-padded (0x00).
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: C6 encodeMuLaw 0x00597AD8..0x00597B8E, segment table 0x00C5C3F0, 32767.0 at 0x00597C18; NaN warns and gives 0; C5 PopRobotAudioMessage 0x00597DD4..0x00597E4E: zero-pad below 744; C7 no volume scaling (0x00597DFC..0x00597E02)
* outstanding: Not settled by the rows: the segment table 0x00C5C3F0 is cited but its values are not in the inventory (the code's table is the earlier transcription; the tests are table-relative), and whether the product with 32767.0 is taken in single or double precision (the code multiplies in f32). The engine's NaN warning is not logged. Needs the table values and the literal's width from the extractor.

**M3-013 — The feed: FIFO drain stopping at the first over-budget message; per Update, one 33 ms frame at a time while the drain completes and the audio animation is ready** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: audio and face frames are paced differently
* rests on: compared against re-analysis/inventory/M3-device.md on 2026-09-24 and reproduced by the code (M3 batch, verifier fixes B1..B4): StreamSendBuffer.Drain is SendBufferedMessages: FIFO, stopping at the first message over the byte budget or at an audio message with the audio budget 0 (reported, like an emptied buffer, as the engine's 0 result), and at a failed send (the error). AnimationScheduler.Advance is one Update (A13..A20): budgets refreshed, leftovers flushed (a send error ends the Update), then while the buffer is empty and the clip has frames left one frame is built and drained, and the stream time takes its 33 ms step after every drain without a send error, a budget stop included (A18; StreamTimeMs). With no frames left and the buffer empty, EndOfAnimation goes directly when StartOfAnimation was sent; if it never was, AudioSilence and StartOfAnimation are buffered and drained and the end follows on a later Update (A20; that branch is unreachable while this stack's HasFramesLeft rule always builds a frame first). An empty clip completes with nothing sent (A12, A13). A cancel keeps the send buffer, the next Update flushes it within the budget, and no EndOfAnimation follows (A24, A25); a replacement drops it (InitStream's ClearSendBuffer, A12). AbortAnimation 0x8D is not sent (M5, A22/A23). On a production robot the engine tick runs the streamer from Robot::Update while streaming is open (CD12), with no 30 Hz tick loop; the TargetInFlight 10 / 200 ms / priming model is gone. CozmoAudio.Play (policy M3-017) goes through the same buffer and budget.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: C14 SendBufferedMessages 0x0057BF60..0x0057C010; C15 UpdateStream 0x0057C8E4..0x0057CABE, 0x0057CC6C..0x0057CCA6; C16 readiness 0x0059A1CC..0x0059A1F6, states per Q5 GetStringForAnimationState 0x00596434
* outstanding: NOT BUILT (MD5, M5): ShouldProcessAnimationFrame's audio-animation readiness (C15, C16, A15, Q5); a streaming sound whose samples are not rendered yet still sends silence for the frame and keeps its place. Also M5's: the one-keyframe-per-track-per-frame pull rule (A19) and HasFramesLeft itself (this stack ends a clip at its duration once every keyframe has fired).

**M3-018 — Colour decode: half-width JPEG, BGR to RGB, cv::resize INTER_LINEAR to 320x240; IsColor; Save at quality 90** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Camera.cs`
* effect: a colour frame looks different
* rests on: compared against re-analysis/inventory/M3-device.md on 2026-09-24 and reproduced by the code (M3 batch): EncodedImageDecoder: IsColor per the tbb table (2, 3, 4, 6, 7, 9, > 8); encoding 9 decoded from the half-width colour JPEG as RGB and resized to 320 x 240 by ResizeLinear, OpenCV INTER_LINEAR's fixed-point pixel-centre path (exact for the 160 -> 320 / 240 -> 240 case on every OpenCV path); BadDecode unless 240 x 320; CameraFrame.PresentationJpeg/Save write encoding 9 as a quality-90 JPEG, 8 as the rebuilt JPEG, others raw.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: A9 RGB dispatch tbh 0x004F21AE, case 9 MiniToJpegHelper(h, w/2, 0x00C48D84), imdecode(1), cvtColor(4) (0x004F237E..0x004F2440); A10 Resize -> cv::resize(..., m) with m = 1 = INTER_LINEAR (0x00870486..0x008704D6); A7 IsColor tbb 0x004F2110 (0x004F2102..0x004F211E); A25 EncodedImage::Save quality 90 (movs r2,#0x5a; 0x004F2EFC..0x004F2FA8)
* outstanding: MISSING: the RGB dispatch for any encoding but 9 (A9 names only 'the JPEG cases'), and IsColor of encoding 0 (a VERIFY failure in the engine; false here). The JPEG codec is StbImageSharp/StbImageWriteSharp in place of OpenCV's libjpeg: decoded pixels may differ in the last bit, and saved files byte for byte; whether that is EQUIVALENT_IMPLEMENTATION is the manager's call.

**M3-021 — Camera exposure and gain: constructor limits, the initial exposure from vision_config.json, DefaultCameraParams handling, the SetCameraSettings range check and send** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Camera.cs`
* effect: the camera exposure is set differently
* rests on: compared against re-analysis/inventory/M3-device.md on 2026-09-24 and reproduced by the code (M3 batch): CameraSettings: the constructor limits 1..66 ms and 0.1..4.0, current 16 / 2.0. HandleDefaultCameraParams, with no time-sync gate: 16 must lie in [msg+8, msg+0xA] (else BadInitialExposureTime and nothing more); then SetCameraSettings(16, f32 msg+4) against the limits in force; then max, min (1 if 0), minGain 0.1, maxGain f32 msg+0 and the gamma table installed and SetNextCameraParams queued. SetCameraSettings sends {gain, exposure, false} only when both are in range (NaN invalid), queues SetNextCameraParams (warning when one is pending) and raises CurrentCameraParams {g, e, auto-exposure 1}. Uses M2 correction C3 (fields 0 and 1 are f32).
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: 1a VisionSystem ctor: max 66, min 1, minGain 0.1, maxGain 4.0, cur 16, gain 2.0 (0x006B002A..0x006B004A); 1b Init changes none (0x006B0658); 1f VisionComponent::Init reads ImageQuality.InitialExposureTime_ms into .data 0x01051054 (0x00650DAE..0x00650DCA); vision_config.json:46 = 16; 1k HandleDefaultCameraParams: no time-sync gate (0x00537114..0x0053712A); needs IsInitialized (0x006B2CD6); min <= init <= max; SetCameraSettings(init, gain) first (0x00657CCA), then SetCameraExposureParams (0x00657D04); 1l SetCameraSettings: IsExposureValid/IsGainValid (0x006B9DAA..0x006B9DBE, 0x006B9E80..0x006B9EA8), sends {f32 g, u16 e, false} reliable (0x0065614A..0x00656166); 1o the engine never requests DefaultCameraParams
* outstanding: Not reproduced exactly: (1f) the initial exposure is the constant 16 (the .data value and the shipped vision_config.json value), because this stack does not load vision_config.json; (1g) VisionSystem::IsInitialized is taken as always true (there is no config load that can fail); (1d) applying the pending params to the current exposure and gain is VisionSystem::Update's (M11) and is not done.

**M3-022 — At connection the NV CameraCalib read is queued; its callback enables vision on every path and on success installs the calibration and starts processing** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`
* effect: vision never starts, or starts without the robot calibration
* rests on: compared against re-analysis/inventory/M3-device.md on 2026-09-24 and reproduced by the code (M3 batch): CameraSettings.OnRobotConnected queues the NV CameraCalib read (tag 0x80000001) through this stack's NV path (NvCalibrationReader, no timeout). Its callback logs Failed / SizeMismatch (size != 56) / Recvd, installs the calibration on success (CameraSettings.Calibration, handed to VisionSystem) and sets VisionEnabled on all three paths (handed to VisionSystem.Enabled). VisionEnabled is false from construction and after a removal.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: 1h NVStorageComponent::Read(0x80000001, cb) queued in the connection handler (0x006583E2..0x006583FA); M1 CD21; 1j callback 0x0065AB68: the Failed, SizeMismatch and Recvd paths all end strb.w #1 at +0x48 (0x0065AE7E/0x0065AE80); distortion zeroed when robot+0x24 <= 6; SetCameraCalibration starts processing (0x0065175E..0x00651766); 2a..2f +0x48 is written only by that callback; +0x4B never; +0x49 stays 0
* outstanding: MISSING: the distortion coefficients are zeroed when robot+0x24 <= 6, and what robot+0x24 holds is not established (gap-pass open question 4), so they are installed as read. Not built here (M11, A4): the engine's +0x48 starts 0 (2a) and only the callback sets it, but VisionSystem.Enabled defaults to true and is a public setter, so vision is not enabled only by the callback; the gate that reads it is the M11 VisionComponent's. The NV wire exchange and its timeout are the NV subsystem's.

### M4-control — Motion, sensors, lights and cubes

**M4-003 — The head and lift API follows the game-message path: caller speed/accel/duration; the original app passes head 10/20 and lift 10/20, duration 0** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Motion.cs`
* effect: the head or lift moves at a different speed
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): the head and lift API is the game path (MD1): the caller's speed, acceleration and duration go into the action, with the app's 10/20/0 (head) and 10/20/0 (lift) as the defaults (MA10, MA11, MA12); engine-internal callers in this stack (behaviours, manipulation) pass the action defaults 15/20 explicitly (MA9). CozmoMotion.SetHeadAngleAsync / SetLiftHeightAsync.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: MA10 game SetHeadAngle overwrites +0x90..+0x98 with the message values (0x0052ABE0..0x0052AC24); MA11 unity/scripts/csharp/Robot.cs:1443-1450 (head 10, 20, 0) and 1638-1646 (lift 10, 20, 0); MA12 game SetLiftHeight: 32.0 while carrying -> PlaceObjectOnGroundAction (0x0052AC40..0x0052AC9E); MA9, MA13 action ctor defaults (head 15/20 at 0x00547F14..0x00547F28; lift 10/20 at 0x00548A68..0x00548A78)
* outstanding: MA12: on the game path a lift height of exactly 32 mm while carrying becomes PlaceObjectOnGroundAction; the carrying state is the M12 CarryingComponent, which CozmoMotion does not see, so that branch is not taken (MISSING, M12 interface)

**M4-005 — Action ids: one u8 counter shared by head, lift and body, pre-incremented from 0; ids run 1..255, 0, 1** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Motion.cs`
* effect: an ack is matched to the wrong command
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): CozmoMotion keeps one u8 counter, 0 at construction and pre-incremented when a head or lift command is sent, so its ids run 1..255, 0, 1 (MA8).
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: MA8 MC+8 = 0 at construction (0x0063DA7C); pre-increment in MoveLiftToHeight 0x0064070C..0x0064071A, MoveHeadToAngle 0x006407D8..0x006407E6, GetNextMotorActionID 0x006406EC..0x006406F4
* outstanding: MA8 shares the counter with the body (TurnInPlace, GetNextMotorActionID); in this stack the TurnInPlace and direct SetHeadAngle sends outside M4 (VisionSystem, FaceActions, ExplorerBehaviors) use the fixed id 3 rather than CozmoMotion.NextActionId - those callers are M7/M11 and were not changed in the M4 batch

**M4-008 — Cliff sensor data (raw values, CLIFF_DETECTED, timestamp, enum names); IMU and cube battery in the engine units** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Sensors.cs`
* effect: cliff readings are reported differently
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): CozmoSensors stores the four raw cliff values, CLIFF_DETECTED and the timestamp of each handled state (SC2, RS12); the IMU and cube battery are passed through in the engine units.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: SC2 UpdateRobotData 0x00634016..0x00634036; M2 RS12 cliffDataRaw 8-byte copy (0x0063401A..0x00634022)
* outstanding: where UpdateRobotData runs within UpdateFullRobotState (before or after the origin check) is not in the rows; the stack stores from accepted states only

**M4-009 — Cube tracking: ObjectAvailable / ObjectConnectionState, Moved / Stopped / UpAxisChanged handling with the charger and carry filters, per-object IsMoving** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Cubes.cs`
* effect: cube motion or identity is reported differently
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): Moved (CD10a) and Stopped (CD10b) look the connected object up by activeID (an unknown one warns), drop the charger's moves, ignore a Moved inside the double-tap window, SetIsMoving only on a change, and broadcast every message; UpAxisChanged for an unknown id is an error (CD10c); a disconnection erases the connected entry (LC8b). Verifier round (2026-09-25, corrections C1..C5): VisionSystem's ObjectMoved path (BlockWorld SetMoving and MarkDirty) now returns first for an unknown id, the charger and a movement inside the double-tap window (CD10a steps 1..3, CozmoCubes.MovedStopsBeforeTheWorld). Q-c: VisionSystem's ObjectStoppedMoving path applies the same lookup and charger filter before BlockWorld.SetMoving(false) (CD10b; the double-tap result is discarded).
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: S1 ObjectConnectionState handler 0x00533B56..0x00533D2C; S2 AddConnectedActiveObject 0x00623040..; CD10a Moved 0x00533E4C..0x005341BA; CD10b Stopped 0x00534636..0x00534AA4; CD10c UpAxisChanged 0x00534D66..0x00534E1E; LC8a..LC8e reuse rules 0x00623040..0x00623A8C, RemoveConnectedActiveObject 0x006243C6..0x00624436; one ObjectID per ActiveCube type (0x004EF468..0x004EF52A)
* outstanding: LC8a: the engine ignores a connection report for a slot above 4; this stack uses the slot as its BlockWorld ObjectID (M11 interface; the engine's ObjectIDs come from SetID, LC8e) and its manipulation/vision fixtures connect cubes on slots 7..9, so the bound is recorded but not enforced. CD10a/CD10b: the carried-object and [robot+0x280]+0xC broadcast exclusions are not wired (M12 interface; +0x280 not read), and robot+0x334 (the charger id) is stood in for by the object type; the located-copy updates (MarkObjectDirty, SetIsMoving on located objects) are M11

**M4-010 — Outbound cube connection: block pool, five slots, SetPropSlot, filter timers, advertisement expiry, slot states, disconnect handling** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CubeConnections.cs`
* effect: cubes connect or disconnect differently
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): the connection path (CubeConnections) now runs in Robot::Update after the first full state, in the CD2 order after the tap filter, on BaseStationTimer seconds, with the robot clock set from each synced state before the origin check (RS1); CD3..CD8 and S3 were already reproduced.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: CD1 0x00514174..0x00514224; CD2 update order 0x0051422A..0x00514470; CD3 BlockFilter::Update 0x0061A79A..0x0061A7E0; CD4 UpdateConnecting 0x0061AA8C..0x0061AB88; CD5 ConnectToRequestedObjects 0x00514A88..0x00514C6C; CD6 0x005179DA..0x00517AA6; CD7 0x00517BD8..0x00517D14; CD8 0x00514924..0x00514A0E; S3 HandleConnectedToObject 0x005179D6..0x00517AA6
* outstanding: CD1: ObjectUnavailable is broadcast only behind robot+0x490, whose default and writers are not in the rows (the stack broadcasts nothing); no dedicated M4-010 regression test was added in this batch (the path is exercised by EngineAppLayerTests.M1_025_M1_015_* and RemainingGapTests)

**M4-012 — StartMotorCalibration is never automatic; byte0 head, byte1 lift; MotorCalibration handling** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Motion.cs`
* effect: the robot recalibrates when the engine would not
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): StartMotorCalibration {byte0 head, byte1 lift} is sent only by CozmoMotion.RequestMotorCalibration, called by the explicit calibration behaviour and the conformance tool, never automatically (MA18, MA19).
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: MA18 senders: CalibrateMotors (from CalibrateMotorAction::Init) and 0x005CFAC2 only; MA19 CheckIfDone 0x00547D38..0x00547DC2; MA20 HandleMotorCalibration 0x00536BAE..0x00536C0E
* outstanding: MA20: when the lift starts calibrating while carrying, HandleMotorCalibration calls SetCarriedObjectAsUnattached(true); the carrying state is the M12 CarryingComponent and this is not wired from the MotorCalibration handler (M12 interface)

**M4-016 — Head and lift move semantics: no send when in position, ack matched by id, completion in position and stopped, tolerances and error codes** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Motion.cs`
* effect: a head or lift move completes, fails or is sent differently
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): nothing is sent when the head is within tolerance + 1e-5 of the target, or the lift within 5 mm and not moving (MA15); the id is taken only when the command is sent; the ack counts only for a sent action with that id (MA16); after it the move succeeds once in position and not moving, fails with 0x04000004 when it stops out of position after having moved, and a failed send is 0x03000016 (MA17). Verifier round (2026-09-25, corrections C1..C5): the head completion follows C1: in-position latched at +0xAC (0x005485E4..0x005485F4), +0xAD set whenever MC+0xA is set before the in-position test (0x0054872E..0x00548734), success = latched and not moving, 0x04000004 only when not in position, not moving and having moved (0x00548738..0x005488AC). Re-verify round (2026-09-25, C6..C8): R1: the head latch now comes after the sent-not-acked test (0x005485D8..0x005485E2). C6 (rows L1..L6): the lift follows the same CheckIfDone: sent and not acked Running; the in-position latch (+0x97); has moved (+0x98) := 1 while MC+0xB; in position: Success if not moving, else Running; not in position: moving Running, not moving and has moved 0x04000004, otherwise Running; Init latches in-position and sends only when out of position, a failed send giving 0x03000016. A move already in position succeeds at once when the motor is not moving (always so for the lift). The MA13 lift angular-tolerance clip is omitted (its formula is not in the rows; it cannot bind at the game path's 5 mm), and the IAction timeout is M8.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: MA15 IsHeadInPosition 0x005484F4..0x0054852C, Init no-send 0x00548544..0x0054854E; IsLiftInPosition 0x00548FEA..0x00549034, 0x005492F2..0x005492FE; MA16 ack lambdas 0x0054D624 (head), 0x0054D748 (lift); MA17 completion 0x005485D8..0x005488B6, 0x005493F6..0x00549508; errors 0x04000004, 0x03000016; MA9 head tolerance >= 2 deg (0x0054803E..0x005480AC); MA13 lift angular tolerance >= 1.5 deg (0x005491D6..0x005492EE); C1: head CheckIfDone latches in-position at +0xAC (0x005485E4..0x005485F4); hasMoved +0xAD set while MC+0xA (0x0054872E..0x00548734); failure 0x04000004 only if not in position, not moving and hasMoved (0x00548738..0x005488AC); C6: lift CheckIfDone 0x005493F6..0x00549508 (latch after the sent-not-acked test; hasMoved while MC+0xB; 0x04000004 only if not in position, not moving and hasMoved); head latch after the ack test 0x005485D8..0x005485E2
* outstanding: the head's CheckIfDone has further code at 0x005485F8..0x00548728 that has not been read (C6)

**M4-017 — Backpack lights: priority, Off resent every tick while no source, charging state machine, shared locator, wire conversion, headlight** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Lights.cs`
* effect: the backpack lights show something different
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): BodyLightComponent: sources in priority {1,0,2}, Off resent every Robot::Update while no source (LB1, LB2, LB4h); the charging state machine with the engine's own OffCharger and the JSON's Charging/Charged/BadCharger (LB4, LB4a..LB4e, loaded from config/engine/lights/backpackLights under CozmoEngineOptions.ResourcesPath); the shared locator at source 2 (LB4i, LB5: CozmoLights.SetBackpack/SetBackpackPattern/BlinkBackpack); the wire conversion to 0x03 (LEDs 1..3) then 0x11 (LEDs 0 and 4) with no white balance (LB3, LB4f, LB4g).
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: LB1 BodyLightComponent::Update 0x00631D54..0x00631EB4; LB2/LB4h Off lights 0x0063221C..0x00632260; LB4a names 0x0102EA18 indexed by state^2 (0x00631BD0); LB4b DefineFromJson 0x00587558..0x00587638; LB4c lookup 0x00587434..0x005874A4; LB4d parser 0x00586D20..0x005870D0; LB4f SetBackpackLightsInternal 0x00631F3A..0x006321BE; LB4i shared locator 0x006322A4; LB5 0x006323C4..0x0063244E; LB6 headlight 0x00632344..0x00632374
* outstanding: LB6: the engine calls EnableMode(14) before the headlight send; what EnableMode is is not in the inventory, so only the SetHeadlight send is reproduced

**M4-018 — Cube lights: WakeUp on connection and reconnect, layers and PlayLightAnim gates, SetObjectLights / SetLEDs, SetCubeGamma, CubeID, CubeLights, white balance, default-layer anims** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Lights.cs`
* effect: cubes light up differently or not at all
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): CubeLightComponent: a connecting light cube (and every reconnect) gets an ObjectInfo {layer 2} unless one exists and PlayLightAnim(WakeUp, layer 2) with the LC2 gates; SetObjectLights with the SetLEDs rules and gamma 0x80; SetLights sends SetCubeGamma on the first call and on a change, then CubeID {activeID, (rot+29)/30}, then CubeLights with white balance; patterns advance on their timers in Robot::Update and an emptied layer plays the default layer-2 animation (LC1..LC8, S5..S8). The patterns load from CubeAnimationTriggerMap.json and config/engine/lights/cubeLights under CozmoEngineOptions.ResourcesPath. Verifier round (2026-09-25, corrections C1..C5): SetLights' SetCubeGamma + CubeID + CubeLights are one group under a lock, so another SetLights cannot interleave; LC2 (b) is the requested layer's stack (0x006382FC..0x0063831C); a timer expires at now >= timer (0x00637998 bhs); a pattern with a timer of 0 (duration_ms 0) holds unless +0x41 is set (0x0063798A..0x00637994).
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: S5/LC1 0x00639CE4..0x00639D8A (trigger 0x26); LC2 PlayLightAnim 0x006382EA..0x006387D8; LC3 cubeLights/wakeUp.json; pattern advance 0x00637998..0x006379DA; default layer 0x00637D4C..0x00637E02; LC4 SetObjectLights 0x00637E6C..0x00637EFC; SetLEDs 0x004E48B0..0x004E49A2 (gamma 0x80); LC5 SetLights 0x0063A654..0x0063A800; WhiteBalanceColor 0x0063A894..0x0063A98E; LC7 Robot::Broadcast delivery 0x00511DE8..0x00511DF8, 0x00662508..0x006625A8; LC8 reconnect replay
* outstanding: LC3 PickNextAnimForDefaultLayer: the cube-sleep flags (writers not in the inventory), the carried object (M12) and the located object's +0x24 (M11) are not wired, so the default is always Connected; +0x41 (which releases a timer-0 pattern) has no writer in the rows and is never set here; a pop that leaves an animation below it on the same layer (LC8 double WakeUp) resends nothing - not in the rows; makeRelative patterns need the located object (M11) and go to the connected cube; which directory the engine reads the cube animations from is not in the rows (loaded from config/engine/lights/cubeLights)

**M4-019 — Cliff: sensor defaults, CliffEvent and PotentialCliff handling, EnableStopOnCliff senders, the threshold schedule 50 / 400 / 150** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Sensors.cs`
* effect: cliffs are detected or handled differently
* rests on: compared against re-analysis/inventory/M4-control.md on 2026-09-25 (M4 batch): CliffSensorComponent defaults (enabled, cache 400, raw 0xFFFF, SC1); SendCliffDetectThresholdToRobot always sends (SC3); CliffEvent with the sensor disabled and flags != 0 is dropped (SC5); the game EnableCliffSensor only stores +4 (SC6); PotentialCliff -> StopAllMotors + EnableStopOnCliff{0} (SC7); EnableStopOnCliff only from the caller (SC8); the first accepted state of the current frame sends 50 and 400 follows once after more than 50 mm (SC4, SC4e). Verifier round (2026-09-25, corrections C1..C5): the threshold schedule runs only for a state that passed the origin check (C3). Re-verify round (2026-09-25, C6..C8): C7 (rows D1..D5): the distance behind the 400 threshold is the XY displacement of the drive centre, MoveRobotPoseForward(pose, -20 mm) = (x + d cos(theta), y + d sin(theta), 0), between the robot pose before and after each accepted state's pose update, added on every state whose frame id matches from the first one (CozmoSensors.CliffThresholdSchedule). C8 (rows P1..P6): SetOnChargerPlatform(b) = b or the contacts flag, with RobotOnChargerPlatformEvent and 50/400 on a change; set true by SetOnCharger on the rising IS_ON_CHARGER edge, left alone by SetOnCharger(false), set false by a committed off-treads change to anything but OnTreads; SC7: a PotentialCliff on the platform is ignored.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: SC1 ctor 0x00633FA0..0x00633FCE; SC3 SendCliffDetectThresholdToRobot 0x00634270..0x00634368; SC4 schedule 0x00512D7A..0x00512EA6; SC4a 0x00634514..0x006345AE; SC4b 0x006343B8..0x006344B0; SC4c 0x00634630..0x00634724; SC4d 0x006347A4..0x0063480C; SC4j 0x00511D66..0x00511DA0; SC5 CliffEvent 0x00535548..0x005356E6; SC6 0x00527E8E..0x00527EEA; SC7 PotentialCliff 0x0053582C..0x00535998; SC8 EnableStopOnCliff senders; C2: the 50 mm distance is the 3-D norm between MoveRobotPoseForward(pose, -20 mm or the carry literal) before and after UpdateCurrPoseFromHistory (0x00512D48..0x00512D74), accumulated from the first matching state (0x00512DD2..0x00512EA6); C7: drive-centre XY displacement, MoveRobotPoseForward 0x00517ED0..0x00517F3E, d = -20 or 0.0 when carrying (0x00512D14); C8: charger platform SetOnChargerPlatform 0x00511D4C..0x00511DB0, set by SetOnCharger rising edge, cleared by CheckAndUpdateTreadsState 0x00512188..0x00512192 and Robot::Update 0x00513C5C..0x00513E2A
* outstanding: C7 D2: MoveRobotPoseForward's distance is 0.0 while carrying (CarryingComponent(+0x284)+8 != -1); the carrying state is M12's and not seen here, so -20 mm is always used. C8 P7..P9: Robot::Update also clears the platform flag when no charger is located or the robot footprint no longer intersects the charger quad (0x00513C5C..0x00513E2A); that is M11 geometry (the charger quad 0x0087713A, the dock pose 0x004EA304, the filter's origin scope) and is not built; the FreeplayDataTracker pause flag 3 after a platform change is M15's. The 150 send (SC4a..SC4c) needs HandleRobotStopped's tag (not read), the removal step of the 100-sample sliding Welford window and RobotStateHistory's lower_bound walk; the 400 on Delocalize (SC4d) needs the UFRS Delocalize, which C9 records as the M11 interface; drone mode (SC7, SC8 EnableDroneMode) is not offered

### M5-animation — Animation clips, scheduler and face

**M5-001 — Clip loading: FlatBuffer fields 0..9 in engine track order, first rejected keyframe truncates, JSON clips named by their first key, the two animation directories** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/FlatBufferReader.cs`
* effect: a clip loads with different content
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: D1 AnimClip keyframe voffsets 0x0057571C..0x00575EFC; cozmo_anim.fbs:80-96; gap1 C2 rejected keyframe returns at once (0x00575EA6..0x005760F2); gap1 C3..C6 container, LoadAnimationFile 0x00521308..0x005215B4, CollectAnimFiles 0x0051F4C8..0x0051F6A0; gap2 J1..J5 DefineFromJson 0x005886F4.. (first top-level key); the four shipped JSON clips
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-002 — 19 procedural-eye parameters and the ProceduralFace default (all 0 except EyeScaleX/Y 1, face scale 1, no distorter); SetFromFlatBuf rules** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFace.cs`
* effect: a face is built with different parameters
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C6 SetFromFlatBuf 0x005838D0..0x00583AAC; SetFacePosition 0x00583B20..0x00583BF8; gap3 K4 ProceduralFace() 0x00583660..0x005836A0 (table 0x00C5A970 = {2,3})
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-003 — Face per frame and blending: GetFaceHelper, Interpolate, the Clip table, Combine for layers** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFace.cs`
* effect: faces blend differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C7 GetFaceHelper 0x0058CD80..0x0058CEE4; C8 Interpolate 0x00584290..0x0058454C; C9 Clip table 0x00C5A97C (0x005847A8..0x00584908); C10 Combine 0x00584648..0x0058478E; gap1 G10 distorter through layering
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-004 — Head 0x93 {u16 duration, s8 angle} and lift 0x94 {u16 duration, u8 height} keyframes, sent once at the trigger** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: head or lift keyframes are sent differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C2 0x004F8C68, 0x004F8C90..0x004F8CD4, 0x004F8C08..0x004F8C4A; C3 0x004F8FDC..0x004F9048, 0x004F8F80..0x004F8FC4
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-005 — Variability: RandIntInRange on the static keyframe RNG per GetStreamMessage, unclamped** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: keyframe values vary differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C2/C3 RandIntInRange(a-v, a+v) with strb truncation; gap1 RNG semantics 0x0082F9B0..0x0082FAC6
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-006 — Body 0x99: radius strings, speed clamps (point turn +-300, straight/turn +-220), negative duration never stops, the stop at duration end** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationClip.cs`
* effect: the body moves differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C4 0x004FB494..0x004FB68C; C5 0x004FBA8C..0x004FBB0E; gap1 S1 0x004FB1A8..0x004FB3FE
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-007 — Timeline: start + frames built x 33; advances whenever the drain returns OK (a budget stop included); frozen while the buffer is non-empty** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: keyframes fire at different times
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A18 0x0057CA7E..0x0057CABE; SendBufferedMessages returns 0 on a budget stop 0x0057BF98/0x0057BFA0; M3 C15
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-008 — One streaming animation: refuse or interrupt, tags 1..0xFE returned, loops with the same tag, live forced to 1 loop, ReplayLastAnimation** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: a second animation is handled differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A4 0x0057B17E..0x0057B1B2; A5 0x0057B1DE..0x0057B24A; A6 0x0057B2FC..0x0057B304; A8 0x0057B29C..0x0057B2C8; A9 0x0057B2D4..0x0057B2F6, 0x0057B660..0x0057B672; A13 0x0057CFF6..0x0057D036; A27 0x0057ED0E..0x0057ED2A
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-009 — Audio keyframe load rules: id low 32 bits, volume default 1.0, absent probabilities 1/N, mismatch or sum > 1 rejects** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: audio keyframes load differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C13 0x004F9E54..0x004FA05C
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-010 — Neutral face: the first ProceduralFace keyframe of the first clip of ag_neutral_face; reset data and layer base; replayed after abort-to-nothing and RemoveIdle** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`
* effect: the resting face differs
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A2 0x0057A0D4..0x0057A20E, ProceduralFace::Reset 0x0058359C; AnimationTriggerMap.json:1308-1309; A30 0x0057BA60..0x0057BD6E; A31 0x0057CF6A..0x0057CFF2
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-011 — Group choice: mood, head window and cooldown filters; RandDbl(sum w) weighted draw; fallback to the Default mood** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationLibrary.cs`
* effect: a different clip is chosen
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: D4 0x0058C4C0..0x0058C7BE; D5 0x0058A946..0x0058AB2E; D6 0x0058AB30..0x0058AE2A
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-012 — An audio keyframe volume (default 1.0) is passed on to the audio layer; its Wwise effect is M6** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAudioSource.cs`
* effect: audio plays at a different level
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C13 volume f2 default 1.0 (0x004F9E54..0x004FA05C); C14 the static default {eventId 0, volume 1.0} (0x004F9E18)
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-013 — faceAnimations: one sprite frame per stream frame, empty frames skipped, two RLE variants per image chosen by _firstScanLine, index reset on abort** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/FaceAnimationLibrary.cs`
* effect: sprite faces look different
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C12 0x004F9708..0x004F98C4; FaceAnimationManager 0x00581254..0x005812C4; GetFrame 0x005817B4..0x005817CA; M3 B4
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-014 — Cooldown keyed by clip name across groups; the Default-mood backup rule within +-0.05 rad, else the first entry** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationLibrary.cs`
* effect: clips repeat or are skipped differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: D5 cooldown set on a pick; MoodManager::Update 0x0067B5E6; D6 0x0058AB30..0x0058AE2A; D7 0x0058BA28..0x0058BA98
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-015 — Corner radii rx = roundf(p*15), ry = roundf(p*20) into four ellipse2Poly corners; below 1 a point** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`
* effect: the eye corners look different
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: gap1 D1 0x0058520E..0x005853C4
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-016 — Backpack-lights track: loaded via JSON, colours raw-or-normalised, 0x98 sent every frame while current, LED order Left Front Middle Back Right** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationClip.cs`
* effect: the backpack lights ignore or misplay animations
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C16 0x005758C8..0x00575CBE; C17 GetColorOptional 0x0084024C..0x0084050C; encoding 0x004FABDC..0x004FAC12; C18 0x004FAC7C..0x004FADB6, 0x004FB0F4..0x004FB11A; 7140 keyframes in 111 shipped clips
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-017 — Audio alternative: r = RandDbl(1), first with lower <= r <= upper skipping |p| < 1e-5, else nothing** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: a different sound plays
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C14 0x004F9AEC..0x004F9CBE, 0x004F9DAC..0x004F9DFC
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-018 — Frames per Update while ShouldProcessAnimationFrame: empty buffer and keyframes left, or audio ready while audio exists** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: animations stream faster or slower
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A15 0x0057CC6C..0x0057CCA6; M3 C15, C16
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-019 — The layer base face is written back only by a streaming animation, not idle ones** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: the held face differs
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C11 0x0064EFA8..0x0064EFEA, 0x0064F154..0x0064F244
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-021 — Eye fill = shipped OpenCV 3.1.0 fillConvexPoly LINE_4 and ellipse2Poly; DrawEye outline and lid polygons; _firstScanLine offset; fill order** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`
* effect: the eyes are drawn with different pixels
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: gap1 D1..D6 DrawEye 0x0058520E..0x00585896; gap3 A1..A10 libopencv_imgproc fillConvexPoly 0x00042F58, FillConvexPoly 0x00042958, Line 0x000419DC, LineIterator 0x00041810, clipLine 0x000410DC (matches stock 3.1.0); gap3 B1..B3 ellipse2Poly 0x00043C48 (SinTable 0x000E7910, vcvtr ties-to-even)
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-022 — Disconnect and teardown: a failed send only returns; ~Robot AbortAll cancels actions, aborts path and docking, sends AbortAnimation 0x8D and StopAllMotors** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: the robot keeps animating or moving after teardown
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A37 0x0051194C..0x0051197C; 0x00511120; 0x0051154A; ~AnimationStreamer 0x0057AF58
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-023 — Abort: AnimationAborted broadcast -> AbortAnimation 0x8D reliable; state cleared; buffer kept and flushed by the next no-animation Update unless InitStream drops it; no EndOfAnimation** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: a cancelled animation leaves the robot in a different state
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A22 0x0057B3F2..0x0057B426; A23 0x005276E8..0x0052771A, 0x0052C22E -> 0x00517DDE..0x00517E0A; A24 0x0057B442..0x0057B58A; A25 0x0057D122..0x0057D1DE
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-024 — InitStream: tag and start, the scan-line toggle rule, buffer dropped, start/end flags, audio animation created, RemoveKeepFaceAlive(99) for non-live; nothing sent** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: animations start differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A10 0x0057B682..0x0057B69C; A11 0x0057B6A8..0x0057B738, 0x0057B804..0x0057B828; A12 0x0057B784..0x0057B7F8; gap1 L7
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-025 — Frame build order and track pull: audio, Start once, Head, Lift, Event, FaceAnimation, procedural face, BackpackLights, Body, RecordHeading, TurnTo; one keyframe per track per frame** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: messages go out in a different order
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A16 0x0057C94E..0x0057CA7A; A17 0x0057CA1C..0x0057CA2A; A19 0x0057CCAA..0x0057CE5A
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-026 — End of animation: EndOfAnimation directly when Start was sent, else silence + Start first; nothing after End; empty clips send nothing** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: animations end differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A20 0x0057CB44..0x0057CBB0; SendEndOfAnimation 0x0057C448..0x0057C496; A21 0x0057C400..0x0057C42E
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-027 — Idle animations: the idle stack, PushIdle/RemoveIdle, idle InitStream with tag 0xFF, ProceduralLive, the no-animation path** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: idle behaviour differs
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A1 0x0057A064..0x0057A0AC; A28 0x0057D03A..0x0057D060, 0x0057D122..0x0057D1E2; A29 0x0057D064..0x0057D448; A30 0x0057B914..0x0057BD6E
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-028 — Keep-alive: timeout 0.5 s, KeepFaceAlive darts and blinks, default params, continuous StreamLayers while a persistent layer exists** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: the idle face behaves differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A31 0x0057CF6A..0x0057CFF2; A32 0x0057A050, 0x0057E010; A33 0x0058D374..0x0058D558; A34 0x0057DB40..0x0057DCD6; gap1 K1..K9 GenerateEyeShift 0x0058D10E.., LookAt 0x00584158..0x0058428A, GetNextBlinkFrame table 0x00C5AAD8; gap2 Q1.7..Q1.10 StreamLayers 0x0057C4F6..0x0057C680
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-029 — Track layer manager: tags, AddLayer/AddPersistentLayer, persistent hold, AddToPersistentLayer, the Remove cross-fade, HaveLayersToSend, frame flags** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: layered faces behave differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: gap1 L1..L8 0x0058E630..0x0058EC6C; gap2 Q1.1..Q1.6 0x0064F4FE..0x0064F518, 0x0058E66E..0x0058E77C, 0x0064EFA8..0x0064F22C; gap3 K5 Remove end keyframe
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-030 — Live idle (UpdateLiveAnimation): gates, body/lift/head wiggles with their parameters, LiveIdleTurn eye shift, lock and carry checks** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: the live idle moves differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A35 0x0057D5F8..0x0057DA82; gap1 K2 0x0058CFCE..0x0058D04A; K10 0x0064F3C8..0x0064F498; R2..R4
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-031 — Glitch: AddGlitch face and backpack layers, GetNextDistortionFrame table, ScanlineDistorter, per-row shift, AddOffNoise** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: glitches look different
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: gap1 G1, G3..G11 (0x0064EDE8..0x0064EEFE, 0x0053A9A8..0x0053AB1E, 0x0053A0B0..0x0053A898, 0x00585DFE..0x00585E94); gap2 G1..G7 GenerateGlitchLights 0x0058CB64..0x0058CD2A
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-032 — DrawFace: 64x128 canvas, the face transform via shipped cv::warpAffine INTER_NEAREST BORDER_CONSTANT 0, row extent, interlace clearing, scan-line shift** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`
* effect: the face is drawn with different pixels
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: E1 0x00585B30..0x00585D9A; E2 0x00585D9C..0x00585E94; gap1 D4, D5; gap3 C1..C6 warpAffine 0x00081850.., invoker 0x0007FC60..0x0008016A, remapNearest 0x00072C38..0x00072D8C (matches stock 3.1.0)
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-033 — Event 0x95, RecordHeading 0x91, TurnToRecordedHeading 0x92 keyframes and their clamps** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationClip.cs`
* effect: event and heading keyframes behave differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C15 0x004FA7A0..0x004FA852; C19 0x004FBB58..0x004FBB82, 0x004FBE2C..0x004FBF28; gap1 S2 0x004FBBDC..0x004FBDAC
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-034 — AnimationTrigger to group name from assets/animationGroupMaps Pairs; first entry wins; unknown gives a warning and ""** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationLibrary.cs`
* effect: a trigger plays a different group
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: gap1 C7 0x00670438..0x0067075C; GetResponse 0x00670810..0x00670920
* outstanding: compare the code against the inventory rows (the step after approval)

**M5-035 — The streamer never reads enabledAnimTracks or skips a locked track; locks only through DisableAnimTracks/EnableAnimTracks; the live idle checks MovementComponent locks** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: tracks are suppressed that the engine would send
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M5-animation.md
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: B1 0x00537FFC..0x00537FFE; B2 0x00513000..0x0051304E; B3 0x0057C94E..0x0057CA7A; B4 0x0057D636, 0x0057D664, 0x0057D69E
* outstanding: compare the code against the inventory rows (the step after approval)

## What remains after both: blocked externally, or needing hardware

| id | subsystem | status | what | why it cannot be settled here |
| --- | --- | --- | --- | --- |
| M1-033 | M1-transport | HARDWARE_ONLY | Robot-side transport behaviour | whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none) |
| M1-043 | M1-transport | HARDWARE_ONLY | Whether a 0-byte UDP read warns | the errno value after a 0-byte recvmsg on the phone |
| M3-008 | M3-device | HARDWARE_ONLY | How the firmware maps pair bits to physical display rows, and the robot playback period | only the robot can answer it |
| M3-016 | M3-device | HARDWARE_ONLY | Whether firmware 2457 emits colour frames in this format, and how the robot reacts to EnableColorImages | only the robot can answer it |
| M9-013 | M9-wwise-music | BLOCKED_EXTERNAL | Whether Wwise routes MIDI notes into the get-in branch of the singing sampler | the dispatch rule itself. Only a recording of the stock app singing, or a Wwise runtime of this bank version, can settle it. This is the largest remaining doubt about how a rendered song sounds |
| M9-014 | M9-wwise-music | BLOCKED_EXTERNAL | Whether a note's velocity changes anything, when nothing in the bank binds it | whether Wwise applies a velocity-to-level mapping of its own to a MIDI voice. The shipped songs vary velocity over about 20 units, so if it does, some notes are a few dB quieter than this renders them. Only a Wwise runtime of this bank version, or a recording of the stock app, can settle it. Until the M9 re-audit this was recorded as an equivalent implementation, which the varying velocities do not support |
| M9-022 | M9-wwise-music | BLOCKED_EXTERNAL | Container semantics: blend and actor-mixer play all children, random picks one by weight avoiding the last, sequence steps its playlist | the runtime dispatch rule, in particular whether a container pre-filters its playlist to children that accept a MIDI note or picks first and then filters |
| M9-023 | M9-wwise-music | HARDWARE_ONLY | How the stock app sounded when it sang | sustain, release tails, level and whether the get-in branch is audible during a song |
| M9-024 | M9-wwise-music | BLOCKED_EXTERNAL | Whether cozmo_singing_note_off also stops the voice it is attached to | the name of modulator property 15 and the enumeration of its values, which live in the Wwise SDK; no Wwise runtime or header ships in the APK. What is left open is only whether the stop is this modulator or the break-on-note-off bit, not whether the note stops |
| M9-025 | M9-wwise-music | BLOCKED_EXTERNAL | The shape a Wwise LFO produces between its extremes | whether the output is unipolar or bipolar, and the exact waveform. This is on the live path: the cube shake is measured and the vibrato reaches a playing song, so any shake exercises this shape |
| M9-026 | M9-wwise-music | BLOCKED_EXTERNAL | The filter and limiter arithmetic between the shipped settings | the exact coefficient formulas and detector behaviour. The settings they act on are exact, so the shape is right and the detail is not |
| M11-016 | M11-vision | BLOCKED_EXTERNAL | Face, pet and motion detection | nothing recoverable: the detector is third-party binary code |
| M14-006 | M14-faces | BLOCKED_EXTERNAL | Text to speech is not implemented | the voice model and the plug-in are third-party binaries |
| M4-013 | M4-control | HARDWARE_ONLY | Whether the robot honours StartMotorCalibration after its connection-time calibration | only a robot can show whether the request is honoured |
| M5-036 | M5-animation | HARDWARE_ONLY | Robot-side animation behaviour: AbortAnimation handling and leftovers, Start without End and the unbounded keep-alive stream, locked-track suppression, animStarted/animEnded echo | only the robot can answer it |
| M4-021 | M4-control | HARDWARE_ONLY | Whether the robot reports the origin id and frame from AbsoluteLocalizationUpdate in RobotState | only the robot can answer it |
| M4-024 | M4-control | HARDWARE_ONLY | What makes the robot forward cube telemetry after connection, and the robot-side effect of StreamObjectAccel | only the robot can answer it |

## Settled differences: equivalent implementations and kept policies

| id | subsystem | status | what | why it is settled |
| --- | --- | --- | --- | --- |
| M1-013 | M1-transport | COMPATIBILITY_POLICY | Windows high-resolution timer realising the 2 ms and 60 ms periods | the original uses a Dispatch repeating callback (M1-010) and a sleeping 60 ms engine thread (M1-024) on Android. The periods themselves are recorded there as behaviour to reproduce; only the host timer mechanism is policy |
| M1-014 | M1-transport | COMPATIBILITY_POLICY | Host thread structure that realises the engine threading | the original: a RelTransport dispatch thread (M1-010, M1-035) and one engine thread draining arrivals every 60 ms (M1-024). Its socket reopen on ENOTCONN and its send-failure handling are M1-022, and handler isolation is M1-034. Only the host thread structure that reproduces those orders is policy. That includes thread priority: the original asks for SCHED_RR at 75% of the OS range for the RelTransport threads (CA9), which the host does not reproduce; host threads keep the default priority |
| M1-022 | M1-transport | EQUIVALENT_IMPLEMENTATION | UDP socket: setup, ephemeral local port, send errors, receive loop, reopen | libcozmoEngine.so 3.4.0-1204 |
| M1-034 | M1-transport | COMPATIBILITY_POLICY | Handler isolation, a deliberate departure: in the original a handler exception aborts the engine process | the original has no handler isolation: nothing on the dispatch path catches, and an exception in any message handler reaches std::terminate and aborts the engine process (rows G3.1..G3.17, cited in evidence). The replacement deliberately isolates handlers instead, by operator decision D6; this record exists so the departure stays visible and is never mistaken for reproduced behaviour |
| M1-036 | M1-transport | COMPATIBILITY_POLICY | Crash reporting after an engine-thread abort | the original installs Google Breakpad from CozmoActivity.onCreate when HOCKEYAPP_APP_ID is set (sources/com/anki/cozmo/CozmoActivity.java:50-53, 105-114; resources/AndroidManifest.xml:120-122); on SIGABRT it writes <dumps>/<APP_RUN_ID>.dmp and re-raises (0x00667A34..0x00667A8C, 0x0095107C..0x00951BD0). A Windows host has its own crash handling, so this stack uses it. Not pursued because they touch only crash output: bionic __assert2/abort (not in the package) and whether libunity/libmono install a SIGABRT handler above Breakpad at run time |
| M1-037 | M1-transport | COMPATIBILITY_POLICY | Host trigger for the socket reset | the original resets on every Android process network bind or unbind (M1-023, rows G4.1..G4.13), which a Windows host does not have. This stack raises the same reset from the host notification that its network addresses changed (System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged), the host event nearest to the route to the robot changing. The reset mechanism itself is M1-023 and is reproduced from source |
| M1-038 | M1-transport | COMPATIBILITY_POLICY | Stop processing a frame after a handled DisconnectRequest sub-message | in the original, HandleSubMessage deletes the connection on a type-3 sub-message (0x008374A0, DeleteConnection veneer 0x008D123C; 0x008374C2) and ReceiveData then keeps walking the frame through the saved, now freed, connection pointer ([sp+0x18] at 0x008378FC, 0x00837958): undefined behaviour that cannot be reproduced. This stack stops processing the frame after such a handled DisconnectRequest instead. An out-of-sequence DisconnectRequest is dropped by R12 before dispatch (0x00837438 bne 0x00837456), frees nothing, and the original walks on with a valid pointer, so that case follows the source and the frame continues (operator decision, 2026-09-24) |
| M1-039 | M1-transport | COMPATIBILITY_POLICY | Windows ICMP port-unreachable on UDP receive is no data | the original ran on Android/Linux, where an unconnected UDP socket does not surface ICMP port-unreachable (B11 sets no IP_RECVERR, 0x00839D18). On Windows, a UDP receive that fails with ConnectionReset is treated as no data for that receive attempt: no warning, and the drain continues. Other socket errors keep their source-backed handling (B16) |
| M1-040 | M1-transport | COMPATIBILITY_POLICY | Accept every robot firmware, and log it | the original compares the robot's firmware version with the shipped header (2381) and refuses a mismatch (M1-029). The operator's robot runs 2457. This stack never refuses on version or build: the handshake proceeds as for Success. It logs the robot's firmware version on every connection and in every hardware bundle, and warns whenever it is not 2381, so a firmware-specific problem is known together with its firmware |
| M1-042 | M1-transport | COMPATIBILITY_POLICY | The original app's post-connect defaults are sent by this stack | in the original, the phone app (Unity) sends these game messages, not the engine: SetRobotVolume with the stored value on any RobotConnectionResponse (ConnectionFlowController.cs:682-686; GameAudioClient.cs:53-121), which makes the engine send SetAudioVolume {u16 vol x 65535} (0x0059A21E..0x0059A256); and GetBlockPoolMessage plus BlockPoolEnabledMessage {true, 0} on Success (RobotEngineManager.cs:373-378; BlockPoolTracker.cs:86-104; ConnectionFlowController.cs:764-780). This stack replaces the app: after a Success response it sends the stored volume (default 1.0) and enables the block pool itself, and the controller can change either. The idle timeouts (StartIdleTimeout / CancelIdleTimeout) are app-backgrounding behaviour and are not sent automatically. Other Unity post-connect flows are replaced by this stack's API |
| M3-004 | M3-device | COMPATIBILITY_POLICY | Warm-up frames are flagged and delivered like any other (the engine has no warm-up discard) | libcozmoEngine.so 3.4.0-1204 |
| M3-017 | M3-device | COMPATIBILITY_POLICY | Test tones, beeps and sweeps | libcozmoEngine.so 3.4.0-1204 |
| M4-004 | M4-control | COMPATIBILITY_POLICY | Motion is gated on calibration here; the engine reacts to it instead | libcozmoEngine.so 3.4.0-1204 |
| M4-006 | M4-control | COMPATIBILITY_POLICY | Wheel confirmation tolerance 35 percent / 5 mm per s | libcozmoEngine.so 3.4.0-1204 |
| M5-020 | M5-animation | COMPATIBILITY_POLICY | Expressions helper faces | not applicable |
| M6-002 | M6-wwise-bank | EQUIVALENT_IMPLEMENTATION | Vorbis rebuild with external codebooks and granule computation | the Wwise Vorbis packing; no runtime in the package to check against |
| M6-004 | M6-wwise-bank | EQUIVALENT_IMPLEMENTATION | Resampling to the robot rate is a band-limited windowed sinc, not Audiokinetic resampler | the Wwise runtime resampler, which does not ship in the APK. What was fixed here is a defect of this stack, not a reproduction of theirs: nearest-sample decimation aliases, and no competent resampler does |
| M7-015 | M7-behaviour | EQUIVALENT_IMPLEMENTATION | Pick-up falls back to the raw status flag until the off-treads classifier is enabled | Robot::CheckAndUpdateTreadsState 0x00511E00, which is implemented and used once calibration is reported |
| M8-004 | M8-framework | COMPATIBILITY_POLICY | Behaviours built in code carry a score of their own; the engine's default is zero | IBehavior::IBehavior 0x005BBB74 and IBehavior::EvaluateScoreInternal 0x005BEEC2, read |
| M8-008 | M8-framework | COMPATIBILITY_POLICY | The head recalibration wait: the engine has no timeout, this stack keeps a backstop | CalibrateMotorAction::CheckIfDone 0x00547D38 and IAction::IAction 0x00540C44, read |
| M8-009 | M8-framework | COMPATIBILITY_POLICY | Behaviour scope undo order | the engine Smart* destructor order |
| M8-010 | M8-framework | COMPATIBILITY_POLICY | Behaviour inventory classifier rules | not applicable: this is bookkeeping, not robot behaviour |
| M9-011 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | The render goes through the effect chain the robot bus carries, not a local peak normalisation | libcozmoEngine.so for the routing and Init.bnk for the chain and its parameters |
| M9-016 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A song is rendered as it plays, a block at a time, about 66 ms ahead of the clock | BehaviorSinging::UpdateInternal 0x005EF0C8 posts Cozmo_Singing_Vibrato every tick, and the bank binds that parameter to the depth of the LFO on the sampler pitch. Continuous is what the engine does, and this reproduces it. The 66 ms is this stack's choice, and it is small against the engine's own: UpdateAmountToSend 0x0057C6F0 lets the engine run up to 14 audio frames ahead of what the robot has played, which at 744 samples and 22320 Hz is 467 ms of audio already committed before it is heard. A parameter cannot reach audio either stack has already sent |
| M9-018 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A Stop action ends the streaming song when its target is the Play target or an ancestor | the shipped bank. The three tempo events play targets 914766641, 139286641 and 602865028, and Stop__Robot_VO__Cozmo_Singing_Stop holds three action-type-1 actions targeting exactly those three, so every stop the product can post is a direct hit on the container that is playing |
| M9-020 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A clip plays its source from BeginTrim for its length; a note still held at the clip end is released there | the shipped clip fields, which are read exactly. Every clip in every bank has PlayAt 0 and BeginTrim 0, so the start of the window is never moved; the end of it is what makes a twelve-second song out of a MIDI source minutes long, and is far from decorative. The one part of the rule that is a runtime judgement - what becomes of a note still held at the end - reaches two notes in the whole product, and the segment ends at the same instant, so what they would have sounded past it is outside the rendered song under either reading |
| M9-027 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | Robot_Bus_Eq_HiLowPass low-pass at 14298 Hz is above Nyquist for the robot's rate | Init.bnk gives 14298 Hz; AnimConstants::AUDIO_SAMPLE_RATE gives 22320 Hz, so Nyquist is 11160 Hz in the engine too |
| M10-005 | M10-derived | EQUIVALENT_IMPLEMENTATION | The off-treads debounce runs on the local clock | the engine debounces against its own base-station clock in milliseconds; the same quantity, a different source |
| M11-005 | M11-vision | EQUIVALENT_IMPLEMENTATION | The sub-pixel corner refinement | VisionMarker::RefineCorners 0x0089FD98, VisionMarker::ComputeBrightDarkValues 0x0089F8E8, RefineQuadrilateral 0x008C55E0 and its corner helper 0x008C66C4, MarkerDetector::Parameters::Initialize 0x008752F8, DetectFiducialMarkers 0x00898760, read |
| M11-012 | M11-vision | COMPATIBILITY_POLICY | The nominal camera calibration stand-in | not applicable: the live path reads the robot own calibration and fails closed without it |
| M11-013 | M11-vision | COMPATIBILITY_POLICY | AllowUnconnectedObjects switch | the engine connected-object rule, which is implemented |
| M11-017 | M11-vision | EQUIVALENT_IMPLEMENTATION | The memory map's vision-derived content: the overhead edges | OverheadEdgesDetector::Detect 0x006ABE34, its construction in VisionSystem::VisionSystem 0x006B0120, GroundPlaneROI 0x004F7774 and its statics at 0x00C48F60, MapComponent::ProcessVisionOverheadEdges 0x0067F7AC, AddVisionOverheadEdges 0x0067F814, FlagQuadAsNotInterestingEdges 0x0067E6B0, FlagGroundPlaneROIInterestingEdgesAsUncertain 0x0067E50C with its lambda at 0x00680B54, MemoryMap::FillBorderInternal and QuadTreeProcessor::FillBorder 0x00689FAC, all read |
| M12-014 | M12-manipulation | EQUIVALENT_IMPLEMENTATION | The docking error signal's last two bytes are whatever was on the engine's stack | UpdateDockingErrorSignal 0x0063BE80 is the only builder and it never writes the struct bytes at +0x14 and +0x15. Nothing clears the struct either: it is a stack local at sp+0xa0 filled field by field, with no memclr and no constructor call, and Pack 0x007C0B26 reads both bytes and sends them |
| TOOL-001 | tools | COMPATIBILITY_POLICY | Conformance CLI pass and fail criteria | not applicable: the harness is not part of the app |
| TOOL-002 | tools | COMPATIBILITY_POLICY | The fake robot side answers place docks without a marker signal | not applicable: this is the test double, not the robot |
| TOOL-003 | tools | COMPATIBILITY_POLICY | The --nominal calibration override in the vision, manipulation and freeplay tools | the live path fails closed without a real calibration |
| TOOL-005 | tools | COMPATIBILITY_POLICY | The hardware acceptance campaign: how a check is judged, and what a result may change | not applicable: the harness is not part of the app. The rule that matters runs the other way - a hardware result never changes a fidelity record's status. A check that passes says the behaviour was observed; a check that fails is an investigation item, not a licence to tune a source-backed constant. M11-005 stays open whatever the vision check reports, and the charger's 20 x 27 mm marker geometry is not adjusted to make the mount succeed |
| M7-017 | M7-behaviour | EQUIVALENT_IMPLEMENTATION | The complete live-animation wire lifecycle | AnimationStreamer::UpdateLiveAnimation 0x0057D5F8, AnimationStreamer::Update 0x0057CE5C, InitStream 0x0057B674, UpdateStream 0x0057C84C, SendStartOfAnimation 0x0057C400, SendBufferedMessages 0x0057BF60, read |
| M13-014 | M13-navigation | EQUIVALENT_IMPLEMENTATION | Knock over a stack: BehaviorKnockOverCubes has no verified fidelity record | BehaviorKnockOverCubes 0x005C2EA0..0x005C3C00, its knock-over callback 0x005C3DCE, IBehavior::StartActing(action, function<void(Robot&)>) 0x005BE0E4 and its lambda 0x005BF8F4, IBehavior::Init 0x005BCB54 and ReadFromJson 0x005BBFB4, read; DriveAndFlipBlockAction 0x0055E208 not read past its arguments; DriveAndFlipBlockAction 0x0055E208, IDriveToInteractWithObject 0x0055B1F4, ReactionTriggerStrategyNoPreDockPoses::ShouldTriggerBehaviorInternal 0x00610E32, AIWhiteboard::AIWhiteboard 0x0056A270 and BehaviorRamIntoBlock's transitions, read |
| M11-020 | M11-vision | EQUIVALENT_IMPLEMENTATION | Illumination normalisation of each marker's region before its corners are refined and it is decoded | DetectFiducialMarkers 0x00898760 per-marker loop 0x008990AA..0x008995A2, Quadrilateral<float>::ComputeBoundingRectangle<int> 0x0088A16C, ArrayToCvMat<u8> 0x00899B50, read |
| M2-017 | M2-protocol | COMPATIBILITY_POLICY | A field whose read failed in a kept malformed message holds 0 / false (the engine leaves stale stack bytes) | libcozmoEngine.so 3.4.0-1204 |
| M3-019 | M3-device | COMPATIBILITY_POLICY | The connection-time SetCameraParams: the engine sends stale stack bytes for f32@0 and u16@4 and bool@6 = 1; this stack sends 0.0, 0, true | libcozmoEngine.so 3.4.0-1204 |
| M3-020 | M3-device | COMPATIBILITY_POLICY | A payload that is empty or all 0xFF: the engine reads data[-1] (undefined); this stack treats it as a decode failure | libcozmoEngine.so 3.4.0-1204 |

