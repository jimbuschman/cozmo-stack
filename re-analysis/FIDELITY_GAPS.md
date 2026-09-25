# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **304 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 188 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 14 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 0 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 58 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
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
| M5-animation — Animation clips, scheduler and face | 36 | 0 | 15 | 0 | 1 | yes | no |
| M6-wwise-bank — Wwise bank reading and codecs | 18 | 0 | 18 | 0 | 0 | yes | no |
| M7-behaviour — Idle, mood and reactions | 17 | 0 | 0 | 0 | 0 | yes | yes |
| M8-framework — Behaviour framework and scoring | 10 | 0 | 0 | 0 | 0 | yes | yes |
| M9-wwise-music — Wwise music, the MIDI sampler and singing | 27 | 0 | 0 | 6 | 1 | yes | yes |
| M10-derived — Derived robot state and reaction strategies | 13 | 0 | 7 | 0 | 0 | yes | no |
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
| M5-animation | INVENTORY_APPROVED | 19 | 0 | 0 | 0 |
| M6-wwise-bank | INVENTORY_APPROVED | 0 | 0 | 0 | 0 |
| M7-behaviour | UNREVIEWED | 17 | 3 | 0 | 0 |
| M8-framework | UNREVIEWED | 6 | 1 | 0 | 0 |
| M9-wwise-music | UNREVIEWED | 20 | 14 | 0 | 0 |
| M10-derived | INVENTORY_APPROVED | 6 | 0 | 0 | 0 |
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
* rests on: compared against re-analysis/inventory/M3-device.md on 2026-09-24 and reproduced by the code (M3 batch, verifier fixes B1..B4): StreamSendBuffer.Drain is SendBufferedMessages: FIFO, stopping at the first message over the byte budget or at an audio message with the audio budget 0 (reported, like an emptied buffer, as the engine's 0 result), and at a failed send (the error). AnimationScheduler.Advance is one Update (A13..A20): budgets refreshed, leftovers flushed (a send error ends the Update), then while the buffer is empty and the clip has frames left one frame is built and drained, and the stream time takes its 33 ms step after every drain without a send error, a budget stop included (A18; StreamTimeMs). With no frames left and the buffer empty, EndOfAnimation goes directly when StartOfAnimation was sent; if it never was, AudioSilence and StartOfAnimation are buffered and drained and the end follows on a later Update (A20; that branch is unreachable while this stack's HasFramesLeft rule always builds a frame first). An empty clip completes with nothing sent (A12, A13). A cancel keeps the send buffer, the next Update flushes it within the budget, and no EndOfAnimation follows (A24, A25); a replacement drops it (InitStream's ClearSendBuffer, A12). AbortAnimation 0x8D is not sent (M5, A22/A23). On a production robot the engine tick runs the streamer from Robot::Update while streaming is open (CD12), with no 30 Hz tick loop; the TargetInFlight 10 / 200 ms / priming model is gone. CozmoAudio.Play (policy M3-017) goes through the same buffer and budget. M5 batch (2026-09-25): the frame loop is AnimationScheduler.UpdateStreamLocked; HasFramesLeft is D2 (any track iterator not at end) and the pull is A19; the A20 end, the A13 loop step and the leftover flush/drop are M5-026, M5-008 and M5-023.
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: C14 SendBufferedMessages 0x0057BF60..0x0057C010; C15 UpdateStream 0x0057C8E4..0x0057CABE, 0x0057CC6C..0x0057CCA6; C16 readiness 0x0059A1CC..0x0059A1F6, states per Q5 GetStringForAnimationState 0x00596434
* outstanding: NOT BUILT (MD5, M5-018): ShouldProcessAnimationFrame's audio-animation readiness (C15, C16, A15, Q5) is the M6 stand-in (always ready); a streaming sound whose samples are not rendered yet still sends silence for the frame and keeps its place. The one-keyframe-per-track pull (A19) and HasFramesLeft (D2) are built in the M5 batch.

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
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): AnimationLibrary.ParseClip reads the AnimClip keyframe tables in the D1/C1 track order (Lift, ProcFace, Head, RobotAudio, Backpack, FaceAnim, Event, Body, RecordHeading, TurnTo); a keyframe whose define fails or that the track refuses (BadTriggerTime: trigger not after the previous, TooManyFrames: more than 1000, L4) logs "Adding X frame %d failed." and ends the load, keeping everything before it and the clip itself (C2, C3; AnimationClip.LoadTruncated). Files come from assets/animations/ and config/engine/animations/ (C5), ".bin" as FlatBuffer and anything else as JSON (C4); a JSON clip is named by its first top-level key in ordinal (JsonCpp) order (J1, J2); a later file of the same name replaces the earlier (C3, C6). Verify round 1 (2026-09-25, corrections C1..C3, gap4): JsonClipLoader is gap4 J1.1..J1.10: each element's "Name" matched exactly to the keyframe class names; triggerTime_ms required (asUInt); Head, Lift, Body, ProceduralFace, BackpackLights and RobotAudio members as J1.5..J1.10 with JsonCpp's conversions (J1.4: range-checked, truncated; ±24.999999999999996 → ±24); AddNewKeyFrameToBack (BadTriggerTime); the first failure ends the load. The four shipped JSON clips load (MD3). "_PROCEDURAL_" is skipped (P1). Verify round 2 (2026-09-25, correction C4): AnimationLibrary.GetAnimation also catches the NotSupportedException of an unported JSON keyframe type, logs "error: <clip>: <message>" in the loader's form and returns null, so an idle group naming such a clip cannot throw into the engine tick.
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: D1 AnimClip keyframe voffsets 0x0057571C..0x00575EFC; cozmo_anim.fbs:80-96; gap1 C2 rejected keyframe returns at once (0x00575EA6..0x005760F2); gap1 C3..C6 container, LoadAnimationFile 0x00521308..0x005215B4, CollectAnimFiles 0x0051F4C8..0x0051F6A0; gap2 J1..J5 DefineFromJson 0x005886F4.. (first top-level key); the four shipped JSON clips
* outstanding: MISSING: SetMembersFromJson of FaceAnimation, Event, DeviceAudio, RecordHeading and TurnToRecordedHeading keyframes is not in the inventory (gap4 covers the six types the shipped JSON uses); such a JSON keyframe throws NotSupportedException. A JsonCpp conversion that throws (a value out of range or not a number) ends the load there. The TooManyFrames boundary is "refuse above 1000" (L4); BadTriggerTime is applied to every track. Cross-file load order and the 4-worker load are HARDWARE_ONLY (C5, C6).

**M5-006 — Body 0x99: radius strings, speed clamps (point turn +-300, straight/turn +-220), negative duration never stops, the stop at duration end** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationClip.cs`
* effect: the body moves differently
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): DefineBody (C4, S1): the radius string (any digit: atoi clamped to int16 and CheckTurnSpeed; TURN_IN_PLACE/POINT_TURN: 0 and CheckRotationSpeed |v| > 300 → ±300; STRAIGHT: 0x7FFF and CheckStraightSpeed |v| >= 221 → ±220; anything else rejects the keyframe); a negative duration becomes INT_MAX. The stream (C5): counter 0 sends {speed, radius}, nothing while counter < duration, the stop {0, 0x7FFF} on the first frame with counter >= duration (IsDoneHelper, +33 per frame), whatever the speed; enableStop is the ctor's 1. No other BodyStop is sent (RobotAnimationSink.Finished sends nothing; the stack's own stops before End and on cancel are gone).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C4 0x004FB494..0x004FB68C; C5 0x004FBA8C..0x004FBB0E; gap1 S1 0x004FB1A8..0x004FB3FE
* outstanding: The radius tokens are matched case-insensitively (the earlier code's rule; C4 does not say whether the engine compares with strcmp); the speed is taken as read before the radius string is processed. A keyframe built in code with an unknown token sends neither its start nor its stop (the engine rejects it at load, so it never streams).

**M5-010 — Neutral face: the first ProceduralFace keyframe of the first clip of ag_neutral_face; reset data and layer base; replayed after abort-to-nothing and RemoveIdle** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`
* effect: the resting face differs
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): AnimationScheduler.LoadNeutralFace is A2 (GetAnimationForTrigger(NeutralFace) → GetFirstAnimationName; an empty group an error, more than one a warning; the clip stored as +0x40 and the face of its first ProceduralFace keyframe given to TrackLayerComponent.Init as the layer base); CozmoAnimations.LoadFrom runs it with the loaded library as the catalog. The keep-alive block replays it after an abort to nothing (+0x73, A6, A31) and RemoveIdleAnimation does when the top becomes Count while an idle plays and nothing streams (A30).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A2 0x0057A0D4..0x0057A20E, ProceduralFace::Reset 0x0058359C; AnimationTriggerMap.json:1308-1309; A30 0x0057BA60..0x0057BD6E; A31 0x0057CF6A..0x0057CFF2
* outstanding: ProceduralFace's reset data before a neutral face is loaded (no assets, or before LoadFrom) is not in the inventory; the layer base is the default face then. The engine reads the neutral in the streamer constructor with the assets already loaded; here it is read at LoadFrom.

**M5-011 — Group choice: mood, head window and cooldown filters; RandDbl(sum w) weighted draw; fallback to the Default mood** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationLibrary.cs`
* effect: a different clip is chosen
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): AnimationGroup.GetAnimationName is D5/D6: candidates by mood (SimpleMoodType), the head window in radians when UseHeadAngle, and cooldown; r = RandDbl(Σw) minus each weight, the pick where r < 0, else the last; the pick's cooldown end set to now + CooldownTime_Sec; nothing left and the mood not Default → again with Default. AnimationLibrary implements IAnimationCatalog.GetAnimationNameFromGroup with the mood, cooldown time and head angle providers. Verify round 1 (2026-09-25, corrections C1..C3, gap4): The group draw is on the context RNG (R3), shared with the live idle and the layer managers (AnimationLibrary.ContextRandom = AnimationScheduler.ContextRandom).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: D4 0x0058C4C0..0x0058C7BE; D5 0x0058A946..0x0058AB2E; D6 0x0058AB30..0x0058AE2A
* outstanding: D4 says an entry's Name must exist as a clip; what the loader does with one that does not is not in the rows, so such entries are kept. The defaults of Weight (1), Mood ("Default") and HeadAngleMin/Max_Deg (±infinity) when absent are the earlier code's, not the rows'. The mood and the cooldown time come from MoodManager (M7): unset, Default and this machine's monotonic clock.

**M5-013 — faceAnimations: one sprite frame per stream frame, empty frames skipped, two RLE variants per image chosen by _firstScanLine, index reset on abort** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/FaceAnimationLibrary.cs`
* effect: sprite faces look different
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): FaceAnimationLibrary.Variants stores each image thresholded at 0x80 as two CompressRLE variants, even rows cleared and odd rows cleared (C12); the FaceAnim track sends one stored frame per stream frame with the FaceAnimationManager's _firstScanLine (toggled by InitStream, A11), an empty frame skipped (index +1, no message), and is done when the index reaches the frame count; Abort resets the current keyframe's index (A24).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C12 0x004F9708..0x004F98C4; FaceAnimationManager 0x00581254..0x005812C4; GetFrame 0x005817B4..0x005817CA; M3 B4
* outstanding: Which stored variant GetFrame returns for which _firstScanLine value is not in C12; 0 takes the even-rows-cleared one (the drawer's convention, E2). The index is reset to 0 when the keyframe is done (a looping clip would otherwise find it done at once); the rows do not say. Image::Threshold(0x80) is taken as "at or above 0x80".

**M5-014 — Cooldown keyed by clip name across groups; the Default-mood backup rule within +-0.05 rad, else the first entry** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationLibrary.cs`
* effect: clips repeat or are skipped differently
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): The cooldown map (GroupContainer) is keyed by animation name and shared by every group of the library (D7: on cooldown iff end > now); the Default backup (D6, not strict) is the Default entry with the smallest TimeUntilCooldownOver among those whose [min - 0.05, max + 0.05] rad window holds the head angle, whatever their UseHeadAngle, with no cooldown set; without one, the first entry; strict gives nothing.
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: D5 cooldown set on a pick; MoodManager::Update 0x0067B5E6; D6 0x0058AB30..0x0058AE2A; D7 0x0058BA28..0x0058BA98
* outstanding: The cooldown time is MoodManager+0x130 (D5; M7): unset, this machine's monotonic clock stands in. HeadAngleMin/Max_Deg defaults when absent (here ±infinity, so every entry qualifies for the backup) are not in the rows.

**M5-016 — Backpack-lights track: loaded via JSON, colours raw-or-normalised, 0x98 sent every frame while current, LED order Left Front Middle Back Right** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationClip.cs`
* effect: the backpack lights ignore or misplay animations
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): The backpack track is loaded (DefineLights, C16) and each colour goes through BackpackColor.FromArray (GetColorOptional, C17: raw when any of r, g, b > 1, else x255, truncated; alpha from a 4th element >= 0; default 0xFF00CCFF) and BackpackColor.Encode; TrackLayerComponent.ApplyLayersToAnim sends 0x98 BackpackLights every frame while the keyframe is current and due, until counter >= duration, that frame included (C18), in the order Left, Front, Middle, Back, Right, at the A16 (10) slot. Verify round 1 (2026-09-25, corrections C1..C3, gap4): BackpackColor.TryReadAll is gap4 J1.9: "Back", "Front", "Middle", "Left", "Right" in that order into one reused ColorRGBA (a 3-element array keeps the previous alpha), each an array of 3 or 4 floats or the keyframe is rejected, vcvt.u32.f32 conversions; the FlatBuffer table goes the same way (C16). The animation keyframe is current and due (B1/B3: trigger <= stream - start); a backpack layer's current keyframe overwrites all five LEDs (B2).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C16 0x005758C8..0x00575CBE; C17 GetColorOptional 0x0084024C..0x0084050C; encoding 0x004FABDC..0x004FAC12; C18 0x004FAC7C..0x004FADB6, 0x004FB0F4..0x004FB11A; 7140 keyframes in 111 shipped clips
* outstanding: MISSING: a colour given as a string names a NamedColors entry (J1.9); the table is not in the inventory, so such a JSON keyframe throws NotSupportedException (no shipped keyframe uses one).

**M5-018 — Frames per Update while ShouldProcessAnimationFrame: empty buffer and keyframes left, or audio ready while audio exists** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: animations stream faster or slower
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): UpdateStreamLocked builds frames while ShouldProcessAnimationFrame holds (A15): a non-empty buffer refuses; without an audio animation, HasFramesLeft (any track iterator not at end, the RobotAudio track included, D2); with one, its Update(start, streamTime) and its readiness, a completed one being cleared (C16).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A15 0x0057CC6C..0x0057CCA6; M3 C15, C16
* outstanding: The audio animation is a stand-in for RobotAudioClient's RobotAudioAnimation (M6): it exists for an animation with RobotAudio keyframes, starts each at its trigger in Update, is always ready, and is complete once its track is at the end and nothing plays; the engine's states (C16, Q5), whether an audio-less animation gets one, and the first frame's latency are M6's. A sound whose samples are not rendered yet sends silence for the frame and keeps its place.

**M5-019 — The layer base face is written back only by a streaming animation, not idle ones** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: the held face differs
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): ApplyLayersToAnim copies the stored face (TLC+0x10); an animation's face replaces it and only a streaming animation (storeFace = 1) writes it back as the new stored face (C11); idle animations and StreamLayers do not.
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: C11 0x0064EFA8..0x0064EFEA, 0x0064F154..0x0064F244
* outstanding: C11 does not say whether the face written back is the animation's (before the layers are Combined) or the composed one; the animation's is written.

**M5-021 — Eye fill = shipped OpenCV 3.1.0 fillConvexPoly LINE_4 and ellipse2Poly; DrawEye outline and lid polygons; _firstScanLine offset; fill order** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`
* effect: the eyes are drawn with different pixels
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): OpenCv310 ports stock 3.1.0 fillConvexPoly (the InputArray wrapper, the Mat& overload and FillConvexPoly, with Line/LineIterator LINE_4 edges and clipLine, gap3 A1..A10) and ellipse2Poly (the normalisation, the SinTable, cvRound ties to even, gap3 B1..B3). ProceduralFaceRenderer.DrawEye builds the outline (D1), the lower and upper lid polygons with their tan/cos bends (D2, D3), transforms every point with the per-eye matrix about (0, 0), mirroring the second eye, (int)roundf(x) and (int)(roundf(y) + _firstScanLine) (D4), takes the eye box (D5) and fills outline 255, AddOffNoise with a distorter, upper lid 0, lower lid 0 (D6). Verify round 1 (2026-09-25, corrections C1..C3, gap4): The lid degrees-to-radians constant is the float 0x3C8EFA35 (C2).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: gap1 D1..D6 DrawEye 0x0058520E..0x00585896; gap3 A1..A10 libopencv_imgproc fillConvexPoly 0x00042F58, FillConvexPoly 0x00042958, Line 0x000419DC, LineIterator 0x00041810, clipLine 0x000410DC (matches stock 3.1.0); gap3 B1..B3 ellipse2Poly 0x00043C48 (SinTable 0x000E7910, vcvtr ties-to-even)
* outstanding: The trig in DrawEye and GetTransformationMatrix is MathF.Tan/Cos/Sin (bionic libm may differ in the last ulp, which can move a rounded point at a tie); the float operation order of the point transform is m00·x + m01·y + m02. Rounding assumes the default FPSCR mode (MD2).

**M5-022 — Disconnect and teardown: a failed send only returns; ~Robot AbortAll cancels actions, aborts path and docking, sends AbortAnimation 0x8D and StopAllMotors** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: the robot keeps animating or moving after teardown
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): A failed send only returns its error: the message stays at the front and the Update ends (A37, C14). CozmoRobot.Dispose is ~Robot's AbortAll as far as this stack has it: the streaming animation is cancelled (SetStreamingAnimation(null), whose AnimationAborted broadcast sends 0x8D, A22/A23), then AbortAnimation 0x8D and StopAllMotors are sent, before the streamer goes (A37). Verify round 1 (2026-09-25, corrections C1..C3, gap4): CozmoRobot.Dispose stops and joins the engine tick first (CozmoEngine.StopTick), so no Update streams after AbortAll; then the animation is cancelled (its AnimationAborted broadcast), AbortAnimation 0x8D, StopAllMotors and the decision-note DriveWheels(0) are sent, and the engine and transport are disposed (DisconnectRequest).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A37 0x0051194C..0x0051197C; 0x00511120; 0x0051154A; ~AnimationStreamer 0x0057AF58
* outstanding: PathComponent::Abort and AbortDocking (A37) are M12/M13's and not run at teardown. The extra DriveWheels(0) after StopAllMotors is this stack's (PROJECT_STATE decision note), not the engine's.

**M5-023 — Abort: AnimationAborted broadcast -> AbortAnimation 0x8D reliable; state cleared; buffer kept and flushed by the next no-animation Update unless InitStream drops it; no EndOfAnimation** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: a cancelled animation leaves the robot in a different state
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): AnimationScheduler.AbortLocked is A22/A24: with +0xA0 != 0 the AnimationAborted{tag} broadcast, synchronous; nothing more when neither +0x38 nor +0x34 is set; the current FaceAnimation keyframe's index reset to 0; startSent = endSent = 0; the audio animation aborted and cleared; +0x38, +0xA0 and the send buffer kept. CozmoAnimations subscribes the broadcast as RobotEventHandler does and sends AbortAnimation 0x8D directly through CozmoRobot.SendMessage (reliable, not budget-gated, A23). The leftovers are flushed by the next no-animation Update or dropped by an InitStream, and no EndOfAnimation follows (A25).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A22 0x0057B3F2..0x0057B426; A23 0x005276E8..0x0052771A, 0x0052C22E -> 0x00517DDE..0x00517E0A; A24 0x0057B442..0x0057B58A; A25 0x0057D122..0x0057D1DE
* outstanding: A24 does not say whose FaceAnimation keyframe is reset when both a streaming and an idle animation are set; the streaming one's is.

**M5-027 — Idle animations: the idle stack, PushIdle/RemoveIdle, idle InitStream with tag 0xFF, ProceduralLive, the no-animation path** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: idle behaviour differs
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): The idle stack starts {Count 0x23F, "default_anim_lock"} (A1); PushIdleAnimation/RemoveIdleAnimation are A30 (Count clears +0x34/+0x64; the last entry and an unknown lock refused; a removal from the middle warns; a Count top with an idle playing and nothing streaming replays the neutral). The no-animation path is A28/A29: a Count top with layers → StreamLayers; a ProceduralLive top makes the live animation the idle; a non-empty buffer is flushed and then a pending End sent (Q1.9); a non-Count idle is picked through the catalog (HasAnimationForTrigger → GetAnimationForTrigger → GetAnimationNameFromGroup(strict = false) → GetAnimation, an error without popping) and initialised with InitStream(anim, 0xFF) and no frame, otherwise UpdateStream(storeFace = 0); +0x44 += 60. StreamLive (the M7-017 seam) appends to the live animation and puts ProceduralLive on the stack; it is refused while an animation streams. Verify round 1 (2026-09-25, corrections C1..C3, gap4): The no-animation path is now B1: with the stack empty or its top Count, StreamLayers when layers exist, otherwise the flush and a pending End, and nothing more; any other top goes straight to the idle, which neither flushes nor sends an End. The live idle is gap4 L8 (UpdateLiveAnimation first; InitStream(live, 0xFF) when the previous idle was not the live one or it has ended, otherwise UpdateStream); after an idle's or the live idle's UpdateStream +0x88 = now (B3). A failed pick sets the idle to null and returns (Q1); a trigger with no animation goes on to the tail with no error. Verify round 2 (2026-09-25, correction C4): the idle and live-idle tail is C4 (0x0057D3F0..0x0057D412): InitStream(idle, 0xFF) when the previous idle is not this one, +0x64 == 0, or the idle has ended, otherwise UpdateStream; a streaming Update clears +0x64 (A13), so after a clip the idle (the live one included) re-inits with 0xFF; the HasAnimationForTrigger-miss path reaches the same tail (0x0057D218), so a kept idle re-inits there too.
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A1 0x0057A064..0x0057A0AC; A28 0x0057D03A..0x0057D060, 0x0057D122..0x0057D1E2; A29 0x0057D064..0x0057D448; A30 0x0057B914..0x0057BD6E
* outstanding: +0x64 is taken as set by an idle's InitStream (A29 says only that +0x64 = 0 re-inits). The Count-top flush refreshes the budgets before its drain; the rows do not say. HasAnimationForTrigger is HasResponse (0x00670AD0, not re-read), taken as "the key is present".

**M5-030 — Live idle (UpdateLiveAnimation): gates, body/lift/head wiggles with their parameters, LiveIdleTurn eye shift, lock and carry checks** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: the live idle moves differently
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): The ProceduralLive idle is streamed as A29 says (the live animation as +0x34, InitStream(live, 0xFF), UpdateStream(storeFace = 0)); its keyframes come from the M7 idle behaviour through StreamLive. FaceLayerManager.AddOrUpdateEyeShift and GenerateEyeShift(x, y, xMax, yMax, ...) are gap1 K2/K10 (the caller's xMax/yMax replaced by the default face's 17/12). Verify round 1 (2026-09-25, corrections C1..C3, gap4): AnimationScheduler.UpdateLiveAnimationLocked is gap4 L1..L7: the six timers as duration/spacing pairs zeroed by the ctor and kept across idle changes; the gates (+0x194, +0x44 >= GetParam<int>(2) unsigned, picking or placing) returning with no decrement; per track body → lift → head the countdown (duration -= 60 while the MovementComponent flag, a lock, carrying for the lift, or duration + spacing > 0); the draws in the L4..L6 order on the context RNG (RandIntInRange, RandDblInRange(0, 1) for the straight fraction); the LiveIdleTurn eye shift through AddOrUpdateEyeShift and its removal with RemoveEyeShift(tag, 0); the head angle (s8)trunc(robot+0x2FC·57.2958f); keyframes with trigger 0 appended by AddKeyFrameToBack with no order check (L7, a failure logging LiveUpdateFailed). The robot inputs are wired from the last RobotState (status bit 2, IS_MOVING, LIFT_IN_POS, HEAD_IN_POS), MovementComponent's track locks and the head angle (CozmoAnimations). The live idle's wire lifecycle is L8 (M5-027). Verify round 2 (2026-09-25, correction C4): the head angle is (s8)trunc(robot+0x2FC · 0x42652EE1) ([0x0057DB2C], loaded at 0x0057D838); the LiveIdleTurn x sign is the int16 speed's sign bit, so a speed of 0 gives + (verified at 0x0057D790..0x0057D7AA).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: A35 0x0057D5F8..0x0057DA82; gap1 K2 0x0058CFCE..0x0058D04A; K10 0x0064F3C8..0x0064F498; R2..R4
* outstanding: CarryingComponent (+0x284, M12) is not on this robot, so the lift's carrying gate reads clear. With ProceduralLive pushed by the StreamLive seam (the M7 idle behaviour, an M7-017 interface) the generator does not run, as that behaviour appends its own keyframes.

**M5-032 — DrawFace: 64x128 canvas, the face transform via shipped cv::warpAffine INTER_NEAREST BORDER_CONSTANT 0, row extent, interlace clearing, scan-line shift** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`
* effect: the face is drawn with different pixels
* rests on: compared against re-analysis/inventory/M5-animation.md on 2026-09-25 and reproduced by the code (M5 batch): ProceduralFaceRenderer.DrawFace is E1/E2/G9: the 64 x 128 canvas, both eyes; with the identity face transform the row extent from the eye boxes, otherwise GetTransformationMatrix(angle, sx, sy, cx, cy, 64, 32) through OpenCv310.WarpAffineNearest (gap3 C1..C6: in place so the source cloned, the inverse, AB_BITS 10, remapNearest with BORDER_CONSTANT 0) and the extent from the transformed box corners (floor/ceil); rows clamped to 0..63; the rows of the drawer's _firstScanLine parity cleared in [min, max); the kept rows shifted by the distorter. The stream draws with the drawer's _firstScanLine and sends CompressRLE of the canvas (BufferFaceToSend, every frame, no de-duplication). Verify round 1 (2026-09-25, corrections C1..C3, gap4): The rotated row extent uses the 4 corners of each eye rectangle, 8 points, with floor/ceil, the min from 63 and the max from 0 (C2; ProceduralFaceRenderer.TransformedRowExtent).
* best authority: libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)
* evidence: E1 0x00585B30..0x00585D9A; E2 0x00585D9C..0x00585E94; gap1 D4, D5; gap3 C1..C6 warpAffine 0x00081850.., invoker 0x0007FC60..0x0008016A, remapNearest 0x00072C38..0x00072D8C (matches stock 3.1.0)
* outstanding: The face matrix is built in float and converted to double (gap3 open question 2: the engine M type not re-read).

### M6-wwise-bank — Wwise bank reading and codecs

**M6-001 — Bank and HIRC readers in the runtime field order (Event, Action, Sound, RanSeq, Switch, ActorMixer, Bus, Layer, NodeBase, RTPC varint, STMG)** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHierarchy.cs`
* effect: a bank object is read with different fields
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: HIRC dispatch 0x009B338C; Event 0x9CD01C; Action 0xA613B0 / factory 0xA60C1C; Sound 0xA1DA08; RanSeq 0xA0828C; Switch 0xA2F1D0; ActorMixer 0xA669EC; NodeBase 0x9F6EF8; Bus 0x9C3FFC; Layer 0x9D24D4; RTPC entry 0x9F7254..0x9F72EC (varint param id); STMG 0x9B0B14; conditional branches (positioning 0x9ECF44, source plugin params 0x9B9C90, BKHD flag 0x9B2224) read exactly
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-002 — Vorbis decoding is the runtime Tremor-lowmem fork: stripped setup, library codebooks, 1-bit mode, integer residue and dequantisation, Tremor floor table, float IMDCT, planar float, skip/trim** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseVorbis.cs`
* effect: Cozmo sounds decode to different samples
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: setup 0x00AB63E0..0x00AB6780; codebook unpack 0x00ABA188; library 0x01058290; packet 0x00AB37FC..0x00AB3934 (1-bit mode); residue 0x00AB73F8 / decodev_add 0x00ABAA6C / decodevv_add 0x00ABABB8; decode_map 0x00AB9BB0; floor table 0x01058BF0; IMDCT 0x00AB4E34 (x2^-24 at 0x00AB3CB4); windows 0x01054490; skip/trim 0x00AB3244, 0x00AB0B98..0x00AB0CA8
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-003 — IMA ADPCM exactly: header predictor is sample 0, 63 nibbles, diff ((2n+1)*step)>>3, interleaved int16, 64 frames per block** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAdpcm.cs`
* effect: ADPCM sounds decode to different samples
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: decoder 0x00A7A194..0x00A7A3C4 (step table 0x00FFD650, index 0x00FFD708); callers 0x00A725F8..0x00A72624, 0x00A73E88..0x00A73EAC, 0x00A740E0..0x00A7410C
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-004 — CAkResampler linear interpolation at the voice stage (int16 Q16, bypass x1/32768) and the Hijack stage (float to 22320 Hz); step formula; pitch ramp** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAudioSource.cs`
* effect: sounds are resampled differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: Init 0x00A47038, SetPitch 0x00A47384, Execute 0x00A47178, kernel table 0x0103C0B8; kernels 0x00A48F5C, 0x00A4913C, 0x00A49E40, ramp 0x00A4A2D8
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-005 — FNV-1 32-bit with ASCII lowercasing over at most 0x103 bytes** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHash.cs`
* effect: event and object ids hash differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: GetIDFromString 0x0099DB84..0x0099DC40
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-006 — Control path: queued PostEvent, ExecuteEvent, EnqueueOrExecute (frames + remainder; PlayAndContinue one frame early), drain order, Play/Stop/Seek execute, switch resolution** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwisePlayback.cs`
* effect: events play different sounds or at different times
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: PostEvent 0x009A6704 / core 0x9A0EF8; pump 0x9AE0B0; ExecuteEvent 0x9AA3DC; EnqueueOrExecute 0x9AA0FC; drain 0x9A9F88; Perform 0x9AF8A8; Play 0xA62D38 / 0xA62A1C; Stop 0xA663C8; Seek 0xA645C8; Switch PlayInternal 0xA2C730
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-007 — Random/sequence step selection: global time-seeded 64-bit LCG, shared state, k-th eligible, shuffle, avoid list, weights, sequence wrap/ping-pong** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAudioSource.cs`
* effect: a different alternative plays
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: LCG 0xA08A7C..0xA08AC0; SetSeed 0x99DB58 from SoundEngine::Init 0x99EF80; state 0xA09698 / 0xA099BC; SelectPlayable 0xA0A3B4; SelectRandomly 0xA08A44; avoid 0xA08694; sequence 0xA0A524..0xA0A6E8
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-008 — Continuous containers: loop semantics, next chosen at voice start, modes 1/2 cross-fades, mode 4 same-voice chaining, mode 5 trigger rate, EndOfEvent at the last PBI Term** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwisePlayback.cs`
* effect: looping and chained sounds play differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: PlayInternal 0xA0AFDC; loop info 0xA091CC; next 0xA09C40; PrepareNextToPlay 0xA6A07C; modes 1/2 scheduling 0xA6A580; PlayAndContinue 0xA62ED4; fades 0xA35998; mode 4 0xA6A2DC, voice attach 0xA4304C, switch 0xA52B90 / 0xA549A0; mode 5 0xA09F04; end 0xA6ACC0; EndOfEvent 0xA03618
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-009 — RTPC: curve shapes and scaling after the curve, sum/product accumulation, value store precedence with STMG defaults at entry+8, bus RTPC empty key, immediate ramps** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHierarchy.cs`
* effect: parameter curves change sounds differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: curve 0xA14E28..0xA15244; accumulation 0xA17724 / 0xA17878; node pull 0xA11590; store 0xA0F07C, STMG defaults 0xA0F594; set 0xA1404C..0xA12CA0; lookup 0xA17280; bus key 0x9C3A48..0x9C3A78
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-010 — Gain composition: GetAudioParameters links and sums, randomizer once per voice, dBToLin fast pow, game-defined aux send gain, muted dry path, Bus Volume after FX (robot_volume does not reach the Hijack)** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBus.cs`
* effect: the robot gets a different level
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: GetAudioParameters 0x9EF258 (links 0x9F1C40 / 0x9F1F4C); CalcEffectiveParams 0x9FFAD4; dBToLin 0xA4B608..0xA4B674; sends 0x9BD368..0x9BD8B4; mix gains 0xA597A4..0xA597BC, 0xA4FBEC; bus gain after FX 0xA4FEF8 / 0xA4D994; collapsed buses 0x9C54E8
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-011 — Voice filter A: Butterworth LPF/HPF biquad before the aux sends, value-to-cutoff map, 8-step chunked ramp; filter B dry-only** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAudioSource.cs`
* effect: the robot audio has a different tone
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: composition 0x9FFD14..0x9FFD74; per connection 0xA4BC58; targets 0xA550D8..0xA551EC; order 0xA44630; LPF 0xA766F0 / HPF 0xA77480; cutoff 0xA7A3D8 / 0xA7A4AC; ramp 0xA76728..0xA77428
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-012 — Mixer: linear per-frame gain ramp, first update not ramped, mono to mono 1.0, stereo to mono 0.70710677 per channel** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBus.cs`
* effect: mixing levels differ
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: ConsumeBuffer 0xA4FBEC; mixer 0xA45E9C; ramp kernel 0xA46668; pan 0xA25FF8 / 0xA1F79C / 0xA209BC (table 0xFFA970)
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-013 — Robot_Bus FX chain in slot order: two Parametric EQs and the Peak Limiter before the Hijack, with their settings and algorithms** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBusChain.cs`
* effect: the robot audio is equalised and limited differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: FX loop 0xA4FD84..0xA4FEF4; registration .init_array 0x4DEB18 / 0x4DEB8C; EQ coefficients 0xAA25E0, execute 0xAA2A84, biquad 0xAA2324; ShareSets 0x6767FC1F, 0x174901C6; limiter setup 0xAA19CC, DSP 0xAA0EB4; ShareSet 0xDF2230FF
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-014 — Bus and Hijack lifetime: on-demand bus, lazy FX instantiation, destroyed after an idle frame with no connections, the one-frame tail and zero-length chunk** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBusChain.cs`
* effect: robot audio streams open and close at different times
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: CreateMixBus 0xA42210; GetResultingBuffer 0xA4FEF8 / SetInsertFx 0xA4F754; ReleaseBuffer 0xA4F36C; destruction 0xA43F64; teardown 0xA4ECE4; UpdateBuffer 0x005985FC
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-015 — The Hijack plug-in: registration (last caller wins), Init (1024 floats/channel, resampler 22320, pitch 0), Execute to 744-sample chunks, Term** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBusChain.cs`
* effect: the robot receives different audio frames
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: RegisterPlugin 0x008DB300; static registration 0x004DD90C; create 0x008DBC70; Init 0x008DBD74; Execute 0x008DBFE8; Term 0x008DBDF8; SetupPlugins 0x005942C6..0x00594354
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-016 — The engine robot-audio path: alternatives drawn up front, wall-clock posting, event_volume per playing id, routing 7..10 to Robot_Bus_1..4, queued callbacks, states, PopRobotAudioMessage, abort** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAudioSource.cs`
* effect: animation sounds reach the robot differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: InitAnimation 0x0059687E..0x00596914; BeginBuffering 0x00597F12..0x00597F8E; lambda 0x0059818C..0x005982A0; routing 0x0059962A..0x005999A4; callbacks 0x00599E6A..0x00599EB8, 0x008D8D40, drain 0x008D88CC from 0x004ED6C4; UpdateLoading 0x00597C9E..0x00597D7E; PopRobotAudioMessage 0x00597DB4..0x00597E8E; abort 0x0059678E..0x005967B8
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-017 — Audio-thread frame model: Perform order, sink-driven frames, EndOfEvent after the bus pass of the last frame** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwisePlayback.cs`
* effect: audio events end at different times
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: Perform 0x9AF8A8 / 0x9AF9F4..0x9AFAB8; audio thread 0xA4087C; RenderAudio 0x9AFD10; PBI teardown 0xA38600 / flush 0xA38420; EndOfEvent 0xA03618
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-018 — Mix rate 48000 Hz and frame 1024 samples (phone-dependent in the original: min(native, 48000) and hardware rounding)** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAudioSource.cs`
* effect: derived timings and resampling differ
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: platform init 0x00A57724: rate cache 0x0108DF90 = min(getNativeOutputSampleRate, 48000), 48000 default 0x00A578B4..0x00A578C8; frame rounding 0x00A577B8..0x00A57830; SetRate 0xA1C75C / SetFrame 0xA1C7D4
* outstanding: compare the code against the inventory rows (the step after approval)

### M10-derived — Derived robot state and reaction strategies

**M10-001 — Off-treads classifier CheckAndUpdateTreadsState: gate, inputs, thresholds, branches, 250 ms debounce commit and every consequence** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/OffTreads.cs`
* effect: the robot state (picked up, on back, on side) is classified differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M10-derived.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: A1..A7 CheckAndUpdateTreadsState 0x511E00..0x5121F8 (thresholds 0x5121FC, 0x5122A0..0x5122BC); A8..A15 consequences: Falling DAS + ActionList::Cancel(-1) (0x511FF0..0x512084; gap1 3a..3e), RobotOffTreadsStateChanged broadcast 0x512092, OnTreads 0x512112..0x512184, carried 0x512100, SetOnChargerPlatform 0x512188, pause flag 0x5121E2; gap1 5a IMU filter state zeroed (0x5100E0..0x5100FE)
* outstanding: A5: the Anki Radians operator> / operator< (0x84CC90: diff > 0 && !IsNear(1e-5)) and whether the difference is wrapped are not in the rows, so the plain float comparisons are kept. The M11/M12/M15 consequences are seams.

**M10-002 — Unexpected-movement detector: gates, wheel/gyro rules and constants, fire at count > 10** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/UnexpectedMovement.cs`
* effect: unexpected movement is detected differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M10-derived.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B1 tail call 0x63E392; B2..B5 gates 0x63E3B4..0x63E426; B6 ctor constants 0x63DAB0..0x63DB00; B7..B11 rules 0x63E428..0x63E5DA
* outstanding: B8: whether the +1 paths add l and r to the sums. B9: whether the same-sign decrement is guarded by count > 0. Both keep the existing reading until the rows are extracted.

**M10-003 — Reaction-strategy factory rules and strategy classes (Cliff, Falling, PickedUp, Shaken, Slope, Frustration, PlacedOnCharger, Sparked, NoPreDockPoses, FistBump, Hiccup, Pet, CubeMoved, FacePositionUpdated, ObjectPositionUpdated)** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/ReactionStrategies.cs`
* effect: reactions trigger differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M10-derived.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: C12 factory switch 0x60D618; C13..C17 Generic, Shaken, Slope, Frustration; gap1 1 Cliff filter 0x60DC7C..0x60DCA2; gap1 8 strategy predicates; gap2 1..6 PlacedOnCharger 0x614474..0x6144D0, CubeMoved 0x60C04A..0x60C1E2 / 0x60BEB4, Face 0x60CB84..0x60CE20, position-update base 0x612168..0x612B22, Object 0x6114A0..0x611582
* outstanding: EnabledStateChanged for Shaken, Slope and Frustration is not in gap1 4j (a no-op here); Hiccup, FistBump, Sparked, the NoPreDockPoses +0x70 writers and Pet EnabledStateChanged (InitReactedTo) are not built.

**M10-004 — CheckReactionTriggerStrategies: sticky action gate, map order, disable locks, predicates, StopAllMotors and track unlock, no-break loop, IsReactionTriggerEnabled and lock messages** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs`
* effect: reactions are enabled, ordered or suppressed differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M10-derived.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: C3 gate 0x5A355A..0x5A357A; C4..C8 0x5A359A..0x5A37CA; gap1 4a IsReactionTriggerEnabled 0x5A40B8; 4b DisableReactionsWithLock 0x5A27F6..0x5A2960; 4c RemoveDisableReactionsLock 0x5A3A52..0x5A3B94; 4e game messages 0x5A4D2A..0x5A4F3A
* outstanding: CompletelyUnlockAllTracks body (which locks it clears and what it sends) is not in the rows; the C3 sticky first-action gate cannot be evaluated without an ActionList and is logged when absent.

**M10-007 — Unexpected-movement response: gate, history lookup, side and obstacle (d = 25), rewind SetNewPose, AddCollisionObstacle, broadcast always, reset** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/UnexpectedMovement.cs`
* effect: the robot re-localises or maps obstacles differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M10-derived.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B12..B17 0x63E604..0x63E962; gap1 7a GetSizeByType CollisionObstacle {20, 54.2, 67.7} 0x50270E..0x502772; B18 kCreateUnexpectedMovementObstacles has no reader
* outstanding: SetNewPose, its AbsoluteLocalizationUpdate and the collision obstacle are not performed: the M4 C9 F1..F3 fields are not in the rows and there is no RobotStateHistory::ComputeStateAt (M11). kCreateUnexpectedMovementObstacles has no reader.

**M10-008 — Resume after a reaction: SwitchToReactionTrigger parking, the track unlock rule, head/lift restore from SetDefaultHeadAndLiftState** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs`
* effect: a behaviour resumes differently after a reaction
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M10-derived.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: C7 0x5A3610..0x5A3682; C9 0x5A25E4..0x5A26DA; C10 flags 0x60F904; C11 0x5A2BB8..0x5A2C3A, 0x5A1BCC..0x5A1D26
* outstanding: CompletelyUnlockAllTracks is not in the rows; the manager+8/+0xC constructor values and MoveLiftToHeightAction's defaults are not in the rows.

**M10-013 — Raw accel/gyro before the first RobotState: heap contents in the engine; 0 here (forced policy)** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/OffTreads.cs`
* effect: the slope reaction could differ before the first state
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M10-derived.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: gap2 7b: no ctor store to +0x35C..+0x377; operator new(0x530) without memset (0x52EE8E..0x52EE9C)
* outstanding: The forced policy MD1 (0 before the first RobotState) is implemented; the record stays a forced choice and is COMPATIBILITY_POLICY at the next approval.

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
| M5-003 | M5-animation | EQUIVALENT_IMPLEMENTATION | Face per frame and blending: GetFaceHelper, Interpolate, the Clip table, Combine for layers | libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped) |
| M5-020 | M5-animation | COMPATIBILITY_POLICY | Expressions helper faces | not applicable |
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

