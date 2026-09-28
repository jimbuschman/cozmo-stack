# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **362 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 167 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 3 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 7 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 143 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
| COMPATIBILITY_POLICY | 30 | A deliberate product or platform decision this stack intends to keep: offline tools, the test harness, PC-side plumbing, or a stand-in the operator has to ask for. Not a place to put fidelity work that is hard. |
| HARDWARE_ONLY | 10 | No shipped artifact can settle it; only a robot, or a recording of the stock app, can. |
| BLOCKED_EXTERNAL | 2 | The answer lies in third-party code or data that is not in the package (Omron OKAO, the Wwise runtime DSP, the Acapela text-to-speech engine). |

## Where each subsystem stands

Four separate things, because one word cannot carry them. **Source read** means nothing is left that
reading the original would settle. **Built** means nothing the original is known to do is knowingly
not done here. Neither says the behaviour is faithfully reproduced: the last two columns are what
remains after both, and they do not go away by working harder on this repository.

| subsystem | records | to read | to build | blocked externally | needs hardware | source read | built |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| M1-transport — UDP transport and reliability | 43 | 0 | 1 | 0 | 2 | yes | no |
| M2-protocol — CLAD messages and protocol helpers | 17 | 0 | 1 | 0 | 0 | yes | no |
| M3-device — Camera, display and audio device layer | 36 | 0 | 8 | 0 | 3 | yes | no |
| M4-control — Motion, sensors, lights and cubes | 24 | 0 | 10 | 0 | 3 | yes | no |
| M5-animation — Animation clips, scheduler and face | 36 | 0 | 15 | 0 | 1 | yes | no |
| M6-wwise-bank — Wwise bank reading and codecs | 24 | 0 | 20 | 0 | 0 | yes | no |
| M7-behaviour — Idle, mood and reactions | 22 | 1 | 20 | 0 | 0 | no | no |
| M8-framework — Behaviour framework and scoring | 14 | 0 | 10 | 0 | 0 | yes | no |
| M9-wwise-music — Wwise music, the MIDI sampler and singing | 28 | 4 | 21 | 0 | 1 | no | no |
| M10-derived — Derived robot state and reaction strategies | 13 | 0 | 7 | 0 | 0 | yes | no |
| M11-vision — Markers, camera geometry and BlockWorld | 40 | 0 | 9 | 1 | 0 | yes | no |
| M12-manipulation — Docking, carrying and pre-action poses | 22 | 0 | 4 | 0 | 0 | yes | no |
| M13-navigation — Planning, charger and block configurations | 15 | 0 | 0 | 0 | 0 | yes | yes |
| M14-faces — Face and pet pipeline | 7 | 0 | 0 | 1 | 0 | yes | yes |
| M15-freeplay — Needs, activities and freeplay | 16 | 1 | 15 | 0 | 0 | no | no |
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
| M3-device | INVENTORY_APPROVED | 21 | 0 | 0 | 0 |
| M4-control | INVENTORY_APPROVED | 9 | 0 | 0 | 0 |
| M5-animation | INVENTORY_APPROVED | 19 | 0 | 0 | 0 |
| M6-wwise-bank | INVENTORY_APPROVED | 2 | 0 | 0 | 0 |
| M7-behaviour | INVENTORY_APPROVED | 0 | 0 | 0 | 0 |
| M8-framework | INVENTORY_APPROVED | 0 | 0 | 0 | 0 |
| M9-wwise-music | INVENTORY_APPROVED | 0 | 0 | 0 | 0 |
| M10-derived | INVENTORY_APPROVED | 6 | 0 | 0 | 0 |
| M11-vision | INVENTORY_APPROVED | 28 | 0 | 0 | 0 |
| M12-manipulation | INVENTORY_APPROVED | 17 | 0 | 0 | 0 |
| M13-navigation | UNREVIEWED | 15 | 5 | 0 | 0 |
| M14-faces | UNREVIEWED | 6 | 0 | 0 | 0 |
| M15-freeplay | INVENTORY_APPROVED | 0 | 0 | 0 | 0 |
| tools | UNREVIEWED | 1 | 0 | 0 | 0 |

## Still to read: every RECOVERABLE_GAP

Each of these is a question the original can answer and nobody has asked it yet.

### M7-behaviour — Idle, mood and reactions

**M7-021 — Reaction robot-field meanings and cliff helper bodies remain to be recovered** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/OffTreadsBehaviors.cs`
* effect: reaction variants or cliff recovery may be named or implemented from an unsupported interpretation
* rests on: the cited source path is only partially recovered; the unresolved field names or branches remain explicit
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: M7 gap1 G8: live reaction rows read offsets robot+0x355, +0x338, +0x300 and +0x37c; unread helper bodies 0x0055b554 and 0x005c0ca8
* outstanding: Trace every writer/reader of the four robot offsets and disassemble helper bodies 0x0055b554 and 0x005c0ca8 with all callers.

**M7-022 — DockingTestSimple developer-test state machine is not exhaustively recovered** (not on the live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: the non-live developer docking test follows different states, callbacks or random arguments
* rests on: the cited source path is only partially recovered; the unresolved field names or branches remain explicit
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: BehaviorDockingTestSimple::UpdateInternal 0x005cbc10..0x005ccc3f; M7 gap1 G7
* outstanding: Enumerate every +0x11c state, callback transition, random range and action argument in 0x005cbc10..0x005ccc3f.

### M9-wwise-music — Wwise music, the MIDI sampler and singing

**M9-013 — Whether Wwise routes MIDI notes into the singing sampler get-in branch** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSongRenderer.cs`
* effect: a get-in recording is layered onto roughly every sung note of every song, or none at all
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-013; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: statically linked Wwise runtime in libcozmoEngine.so
* evidence: 0x009B3260..0x009B4033; 0x009BBF9C..0x009BC17B; 0x00A78D10..0x00A78DE3
* outstanding: Trace the runtime MIDI-event entry from the post-load music/actor-mixer vtables through child filtering for MIDI target 110896138 and establish whether branch 403781184 receives the notes.

**M9-014 — Whether MIDI note velocity implicitly changes voice level** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSongRenderer.cs`
* effect: the softer notes of a song come out quieter, or every note comes out at the same level
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-014; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: statically linked Wwise runtime in libcozmoEngine.so
* evidence: 0x009B3260..0x009B4033; 0x00A78D10..0x00A78DE3; re-analysis/research/20260928-I-M9-gap3-extraction.md
* outstanding: Trace the MIDI velocity byte from the runtime event entry through every per-voice gain and RTPC input when the bank sets no velocity binding.

**M9-024 — Whether the note-off envelope stops the voice it is attached to** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseModulator.cs`
* effect: a held note is cut when it is released, or plays on past it
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-024; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: statically linked Wwise runtime in libcozmoEngine.so
* evidence: 0x009D552C..0x009D55F3; 0x009D5934..0x009D6598; 0x009D7FC0..0x009D8137
* outstanding: Trace the type-22 property-15 boolean from virtual method 0x009D552C through the per-voice object and identify whether it stops the attached note-off voice.

**M9-025 — The exact waveform produced by the Wwise LFO between its extrema** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseModulator.cs`
* effect: with a cube being shaken, the vibrato sharpens and flattens, or only sharpens, and by a different amount at each instant
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-025; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: statically linked Wwise runtime in libcozmoEngine.so
* evidence: 0x009D671C..0x009D7727; 0x009D7FC0..0x009D8137; 0x009E266C..0x009E2813
* outstanding: Follow the type-21 per-voice object created at 0x009D7FC0 through vtable 0x0104B268 and map reads of LFO state +0x34..+0x48 to the waveform sample equation.

### M15-freeplay — Needs, activities and freeplay

**M15-014 — Needs connection and per-serial persistence lifecycle** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs`
* effect: the robot-specific needs file selected, read and written changes
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: NeedsManager::WriteToDevice 0x00693BB0; NeedsManager::InitAfterSerialNumberAcquired 0x006943A0; NeedsManager::NeedsFilenameFromSerialNumber 0x00695224; NeedsManager::ReadFromDevice 0x006998B4; persistence string initializer 0x004D8DDC; RobotInterface::MessageHandler::ConnectRobotToNeedsManager 0x0069DEE4
* outstanding: The generated or indirect dispatch registration that calls 0x0069DEE4 was not recovered. Read that table to identify the inbound serial field and connection ordering.

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

**M3-032 — NV dispatch is gated in Robot::Update (Running state, a first full state after SyncTimeAck, and a passing UpdateAllResults once calibrated)** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`
* effect: NV requests are sent before the engine is ready, or after a vision failure, where the original holds them
* rests on: NvStorageComponent sends whenever enqueued; the engine's update walk has no Running-state, Gate A or Gate B; there is no SyncTimeAck watchdog
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: CozmoEngine::Update returns early unless engine+0x10 != 0 and UiMessageHandler::Update == 0 (0x4ED4DE..0x4ED51C); UpdateAllRobots runs only in state 3 Running (0x4ED5C4..0x4ED6BE); SyncTime watchdog 5 s (0x513BF6..0x513C5A); Gate A robot+0x34E (0x513C5C..0x513C62) is set only by UpdateFullRobotState when robot+0x29 (0x51293C..0x512948); Gate B vc+0x28 calibration + UpdateAllResults failure returns (0x513C6E..0x513CBC); replies are not gated (0x52F850..0x52F856)
* outstanding: build the Running-state walk, Gate A (SyncTimeAck + first full state), Gate B and the SyncTimeAck watchdog around the NV send

**M3-033 — At connection the engine queues 12 NV reads, then the CameraCalib read, then Lab and Needs; ready-to-stream waits for the whole queue** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`
* effect: ready-to-stream (and the cube/audio/animation start it gates) is set before the robot's stored data is read, or reads the original never issues are sent
* rests on: the stack queues only the CameraCalib read (M3-022); the 12 constructor reads and the Lab/Needs reads are not queued, so readiness is set early
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: Robot ctor reads #1 ProgressionUnlock 0x182000 (0x64BEFA), #2 Inventory 0x195000 (0x63CA52), #3/#4 FaceAlbum 0x184000/0x183000 (0x6512F0/0x651330), #5..#12 the 8 RDBM backup reads (0x51AD0A); #13 CameraCalib 0x80000001 (0x6583FA) then Lab 0x196000 (0x6A5B1E) and Needs 0x194000 (0x6944CC) in the mfgId lambda (0x52E3A6..0x52E3B2); ProcessOnIdleCallbacks waits for the deque to drain (0x645B10..0x645B1A)
* outstanding: build the connection-time read queue in the engine's order and let ready-to-stream wait for it

**M3-034 — The connection reads' callbacks and data sinks (progression, inventory, face album, backup, lab, needs)** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/NvStorage.cs`
* effect: the robot's stored progression, inventory, face album, backup, lab and needs data is read but never applied
* rests on: the stack has no callers for the constructor/Lab/Needs reads; the camera calibration callback is the only one built (M3-022)
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: ProgressionUnlock 0x102F6B8+0x14 = 0x64CE34 (defaults on -1, SendUnlockStatus); Inventory 0x102EF3C+0x14 = 0x63D7C4 (+0x104 = 1, Unpack, SendInventoryAllToGame, RequestDefaultSparks on -1); FaceAlbum 0x184000 empty callback fills VC+0x2F4; 0x102F914+0x14 = 0x65A860 consumes it via SetSerializedFaceData + BroadcastLoadedNamesAndIDs; RDBM 0x101FD38+0x14 = 0x51DF34 (map store, OnboardingData for 0x181000, WriteBackupFile after the last); Lab 0x10317B8+0x14 = 0x6A6486 -> 0x6A5C34; Needs 0x1031098+0x14 = 0x69BEB2 -> FinishReadFromRobot + InitAfterReadFromRobotAttempt
* outstanding: wire each connection read's callback to its layer (M15 needs/progression, M11 face album, M12 backup, lab); the consumers' own semantics are those layers' records

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
* evidence: HIRC dispatch 0x009B338C; Event 0x9CD01C; Action 0xA613B0 / factory 0xA60C1C; Sound 0xA1DA08; RanSeq 0xA0828C; Switch 0xA2F1D0; ActorMixer 0xA669EC; NodeBase 0x9F6EF8; Bus 0x9C3FFC; Layer 0x9D24D4; RTPC entry 0x9F7254..0x9F72EC (varint param id); STMG 0x9B0B14; conditional branches the shipped banks never exercise (3D positioning 0x9ECF44, bus A/B bits, the LayerCntr layer body): the rows are partial and the code fails closed (throws) rather than guessing
* outstanding: compare the code against the inventory rows; the unread conditional branches (positioning, bus A/B bits, the LayerCntr layer body) stay gaps

**M6-002 — Vorbis decoding is the runtime Tremor-lowmem fork: stripped setup, library codebooks, 1-bit mode, integer residue and dequantisation, Tremor floor table, float IMDCT, planar float, skip/trim** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseVorbisNative.cs`
* effect: Cozmo sounds decode to different samples
* rests on: The frozen rows were read and the settled parts reproduced in the standalone cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseVorbisNative.cs against M6-002 V2/V3/V6 and gapG 6.2, 6.3, 6.5, 6.6, 6.7, 6.10, 6.12: the block-size 1<<pow and its 64/8192 range check (0x00AB6380), the codebook ids and the built-in library 0x01058290 (byte-identical to ww2ogg packed_codebooks_aoTuV_603.bin), the residue setup fields, the hard-coded 1-bit mode number (0x00AB37FC), the stock Tremor _float32_unpack (0xABA468), the q_bits/q_seq step where q_seq is read and discarded (0xABA4D8), the decode_map type-1 dequantisation (0xAB9BB0), and the vorb-header skip/trim selection (0x00AB3244, 0x00AB0B98..0x00AB0CA8; the fmt+0x24/+0x26/+0x32 fields are parsed in WwiseMedia.cs). The Decode entry throws NotSupportedException naming each unread piece rather than substituting a Tremor or NVorbis port. Not wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler. WwiseVorbis.cs (NVorbis) and WwiseVorbisRebuilder.cs/WwiseCodebookLibrary.cs remain diagnostic/cross-check utilities. Correction C5 (2026-09-27) added the floor1 setup 0x00AB88D8, the mapping setup 0x00AB6788, the decode_map tree step and leaf 0x00AB9BB0, the residue decodev_add 0x00ABAA6C / decodevv_add 0x00ABABB8, the coupling inverse 0x00AB6E30, the 256-float floor dB table 0x01058BF0, floor1 inverse2/render_line 0x00AB915C, the five libvorbis window tables 0x01054490 with the combine 0x00AB5A94 and planar layout 0x00AB3520, and the end-trim consumption 0x00AB3884. The decoder still throws for the four remaining RECOVERABLE_GAPs (the decode-table builder 0x00AB96EC, the residue stage walk 0xAB7808..0xAB7E00, the floor look helper 0x00AB8018 and the window default 0x00AB3728) plus the IMDCT 0x00AB4E34, whose normalisation C5 7b leaves unestablished. Correction C6 (the pass-2 report re-analysis/evidence/m6-vorbis/vorbis-arithmetic-2.md) corrected the C5 leaf/internal polarity (internal nodes >= 0, leaves are the bit31-set entries; 0x00AB9C44/0x00AB9C4C), the C5 '0x00AB39D8 has no decode caller' error (0x00AB4E34 tail-branches into it at 0x00AB5A44 and applies 2^-24 there), and the C5 9b allocation attribution (0x00AB3780 allocates and stores the per-channel pointers; 0x00AB3520 selects windows and drives the overlap). Built in WwiseVorbisNative.cs from C6: the corrected leaf/internal polarity and the 8/16-bit decode-table entry builder and walk with the two-word leaf payload (0x00AB96EC/0x00AB9BB0: 8-bit entries with a bit7 leaf marker for format 1, the entry's high 7 bits combined with the next byte; 16-bit with bit15 for format 2, the high 15 combined with the next halfword; no 32-bit store; dispatch on codebook+0x14), the residue stage/partition accessors and class-word split (0xAB7808..0xAB7E00: type 0 shares type 1, info+4[class] cascade, info+8[class*8+s] book, fullbooks+60*book, point -8; quotient = running/partword[ch] stored, remainder carries, the last channel takes the remainder), the window-combine add/sub/mirror/negate forms (0x00AB5A94), the floor1 low/high-neighbour scan (0x00AB8D68) and the floor-look right-biased merge sort of floor+0x0C keyed by the postlist (0x00AB8018), and the IMDCT shift (C5 7a) plus the 2^-24 tail scale (C6 2b). Correction C7 (the pass-3 report re-analysis/evidence/m6-vorbis/vorbis-arithmetic-3.md) rebuilt the decode-table layer and removed the C6 mislabels: codebook+0x14/+0x18 are dec_nodeb (1/2/4) and dec_leafw (1/2), not a format selector, with codebook+0x1C dec_type and codebook+0x38 q_val; _determine_node_bytes/_determine_leaf_words are inlined at 0x00ABA314..0x00ABA440; _make_decode_table 0x00AB96EC and _make_words 0x00AB9300 build the tree (marker[33], chase, node append, decpack|0x80000000, the overpopulated -1) and repack it to the five (nodeb,leafw) forms (1,1)=8/8, (1,2)=8/16, (2,1)=16/16, (2,2)=16/32, (4,*)=32/32; C6's 16-bit-store-vs-32-bit-read mismatch is void because the dec_nodeb==4 path writes a 32-bit table directly (unexercised by shipped books, C7 5c); decpack dec_type 2/3 are unreachable (C7 5d). Floor1 inverse1 0x00AB8E60 is built (quant table {256,128,86,64}, tristate read(1), fit_value[0]/[1], the class cascade, the sub-book 0xFF sentinel and the unwrap loop), and so is the residue divisor array 0x00AB770C..0x00AB7808 (spp, partitions_per_word=groupbook->dim, partwords, the [partitions^(dim-1)..1] sequence and its channel copy). The decoder dispatch 0x00AB9BB0 selects on nodeb then leafw.
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: setup 0x00AB63E0..0x00AB6780; codebook unpack 0x00ABA188; library 0x01058290; packet 0x00AB37FC..0x00AB3934 (1-bit mode); residue 0x00AB73F8 / decodev_add 0x00ABAA6C / decodevv_add 0x00ABABB8; decode_map 0x00AB9BB0; floor table 0x01058BF0; IMDCT 0x00AB4E34 tail-branches into 0x00AB39D8 (0x00AB5A44 b), which applies the only output scale 2^-24; windows 0x01054490; skip/trim 0x00AB3244, 0x00AB0B98..0x00AB0CA8; floor1 setup 0x00AB88D8 (partitions/classes/mult/rangebits/postlist, entry +0x00..+0x20); mapping setup 0x00AB6788 (submaps/coupling/mux, three read(8) per submap); evidence re-analysis/evidence/m6-vorbis/vorbis-arithmetic.md; decode_map 0x00AB9BB0 tree walk and leaf packing; builder _make_decode_table 0x00AB96EC RECOVERABLE_GAP (16-bit store vs 32-bit read unreconciled); residue 0x00AB770C / 0x00AB745C, decodev_add 0x00ABAA6C / decodevv_add 0x00ABABB8, coupling 0x00AB6E30; floor dB table 0x01058BF0 (256 floats transcribed); floor1 inverse2 0x00AB915C; IMDCT 0x00AB4E34 (the 2^-24 constants belong to 0x00AB39D8, no decode caller -> normalisation RECOVERABLE_GAP); windows 0x01054490 = vwin256/512/1024/2048/4096; window-combine 0x00AB5A94; skip/trim 0x00AB3244/0x00AB3884; C6: decode-table widths 8-bit (format 1, bit7 leaf) / 16-bit (format 2, bit15 leaf), no 32-bit store; the walk loops while the entry >= 0, so leaves are the bit31-set entries (0x00AB9C44 cmp/bge, 0x00AB9C4C bic 0x80000000) -- C5 3a/3b inverted; the +0x14==4 case unresolved; C6: IMDCT 0x00AB4E34 tail-branches into 0x00AB39D8 (0x00AB5A44 b), which applies the only output scale 2^-24 -- C5 7b 'no decode caller' corrected; nominal output still UNKNOWN; C6: residue stage walk 0xAB7808..0xAB7E00 (type 0 shares type 1; class digit, cascade info+4[class], book info+8[class*8+s], fullbooks+60*book, point -8); floor look 0x00AB8018 is a merge sort of floor+0x0C keyed by postlist; C6: window-combine branches (a*wA+b*wB vs a*wA-b*wB, the vrev64/vswp mirror, the single-window vneg); window default 0x00AB3728 is a 0 pointer dereference (reachability UNKNOWN); floor1 neighbour scan 0x00AB8D68; allocation is 0x00AB3780, overlap driver 0x00AB3520 (corrects C5 9b); C7: codebook fields dec_nodeb (+0x14 in {1,2,4}), dec_leafw (+0x18 in {1,2}), dec_type (+0x1C), q_val (+0x38); _make_decode_table 0x00AB96EC has five forms (1,1)=8/8, (1,2)=8/16, (2,1)=16/16, (2,2)=16/32, (4,*)=32/32; _make_words 0x00AB9300 builds the tree (marker/chase/decpack); C6's 16-bit-vs-32-bit mismatch is void (the ==4 path writes 32-bit); C7: the ==4 case (dec_nodeb==4 iff used<2) is unexercised (no shipped book has <4 used); dec_type 2/3 unreachable; C7: floor1 inverse1 0x00AB8E60 (quant table {256,128,86,64}, tristate read(1), fit_value, class lookup, cascade, sub-book 0xFF->0, the unwrap loop); C7: residue divisor array 0x00AB770C..0x00AB7808 (spp, partitions_per_word=groupbook->dim, partwords; per-channel base stride dim*partwords; the divisor sequence [partitions^(dim-1)..1]); C7: IMDCT is mdct_backward(n,in) in place; stage order presymmetry 0x00AB3D28 -> butterflies 0x00AB3FCC -> step7/8 -> tail 0x00AB39D8 (2^-24); the per-stage butterfly and trig-table indexing remain RECOVERABLE_GAP (13 tables at 0x01004A40 via GOT 0x01040230); C9: exact IMDCT lane arithmetic and indexing: pre-symmetry 0x00AB3D28..0x00AB3FB0; large butterfly 0x00AB3FCC..0x00AB4E30; recursive stages 0x00AB4FAC..0x00AB5244; terminal butterfly 0x00AB5248..0x00AB5A1C; bit-reversed rotation/tail 0x00AB39D8..0x00AB3CF0; report re-analysis/research/20260927-X5-imdct-extraction.md; C9 corrects C8: the exact trig data is 584 binary32 words at 0x01004A40..0x01005360, followed by 31 integer masks at 0x01005360..0x010053DC; 13 GOT views start at float offsets 0,36,72,108,144,172,200,228,256,296,368,440,512; bitrev9 is 512 u16 at 0x01004640..0x01004A40; C9: GOT 0x01040268 -> BSS 0x0108E648 supplies the native work pointer (reads 0x00AB4E50..0x00AB4E64/0x00AB4FAC/0x00AB526C/0x00AB5A2C); three static gap passes did not locate its writer, so native allocation/lifetime remains RECOVERABLE_GAP; an explicit internal implementation buffer does not claim that ownership path; C11 packet driver: entry 0x00AB3780 (mode read 0x00AB37FC; block flag/block size 0x00AB380C..0x00AB382C; first-window copy 0x00AB3814..0x00AB3874; start-skip/end-trim 0x00AB3878..0x00AB3970) tail-calls the inverse 0x00AB6B14 at 0x00AB3934; C11 inverse 0x00AB6B14: per channel floor1 inverse1 0x00AB8E60 (0x00AB6B98..0x00AB6C18); coupling mark 0x00AB6C24..0x00AB6C7C; per submap residue 0x00AB73F8 (0x00AB6C80..0x00AB6DB4); inline coupling inverse 0x00AB6DC4..0x00AB6E78; per channel floor1 inverse2 0x00AB915C (0x00AB6E7C..0x00AB6EE0); mdct_backward 0x00AB4E34 at 0x00AB6EEC..0x00AB6F10; clear dsp+0x30 0x00AB6F18; C11 framing 0x00AB7E40 (u16 packet size, results 0x2D/0x2E/0x11) and window/overlap driver 0x00AB3520 (per channel 0x00AB5A94 at 0x00AB3664, overlap save 0x00AB3698, planar output); C11 decoder-state offsets dsp+0x00 (bit data), +0x04 (bit pos), +0x08 (remaining), +0x0c (channels), +0x10 (setup), +0x14 (per-channel output), +0x18 (overlap), +0x1c/+0x20 (skip/end), +0x24/+0x28 (prev/cur block flag), +0x2c/+0x2e (start-skip/end-trim), +0x30 (prev-window flag); C11 setup offsets +0x00/+0x04 (bs0/bs1), +0x08..+0x18 (mode/mapping/floor/residue/codebook counts), +0x1c (mode array stride 2), +0x20 (mapping stride 0x14), +0x24 (floor stride 0x24), +0x28 (residue stride 0x1c), +0x2c (codebook stride 0x3c); setup parser 0x00AB6380/0x00AB63E0 from the cache 0x00AB2D74; C11 residual: stream reset 0x00AB3978 (called from 0x00AB7E40 on a flagged last packet) is unread (RECOVERABLE_GAP); C12 source render wrappers (report 20260928-B-M6b-source-classes.md): the shipped Vorbis source classes and their live render slots - streamed 0x103E0B8 render 0xAB0448 (0xAB0448..0xAB04E8: decoder state, framing 0xAB7E40, emit 0xA73490), in-memory 0x103E138 render 0xAB1550 (internal read buffer +0xEC..+0xF8, framing 0xAB7E40, emit 0xA73490); StartStream vt+0x28 = 0xAB0B20 / 0xAB22D4; decoder fields [src+0x38] config, [src+0x80] output, [src+0x3C] frames, [src+0xBC] rate
* outstanding: B1 built and verified the C9 exact IMDCT (WwiseVorbisImdct.cs; the 584 trig words and 512 bitrev entries vendored in WwiseVorbisNative.Windows.cs). C11 now settles the packet driver: the entry 0x00AB3780, the inverse 0x00AB6B14, the framing 0x00AB7E40 and the window/overlap driver 0x00AB3520, with the decoder-state and setup field offsets. What remains for this record is building those driver rows in WwiseVorbisNative.cs and wiring the completed decoder into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler. Native work-buffer ownership remains RECOVERABLE_GAP: three static passes found no writer for BSS 0x0108E648; settle it by a runtime write watchpoint followed by reopening the writer and its allocation/free callers. An explicit decoder-internal work buffer is permitted for B1 but does not establish the native allocation/lifetime path. The stream reset 0x00AB3978 (end-of-stream path) is now an explicit RECOVERABLE_GAP. The window-default reachability, seek start-skip and loop-count host widths, and DecodeMapEntry failure-path extra-bit consumption remain as previously recorded; inert shipped-media deviations remain fail-closed. C12 adds the Vorbis source render wrappers (0xAB0448 streamed / 0xAB1550 in-memory) that call the framing 0xAB7E40 and the emit 0xA73490; build them with the driver.

**M6-004 — CAkResampler linear interpolation at the voice stage (int16 Q16, bypass x1/32768) and the Hijack stage (float to 22320 Hz); step formula; pitch ramp** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseResampler.cs`
* effect: sounds are resampled differently
* rests on: the frozen rows were read and reproduced in the standalone cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseResampler.cs (Init 0x00A47038, SetPitch 0x00A47384, Execute 0x00A47178 and the mono kernels 0x00A48F5C, 0x00A4913C, 0x00A49E40, 0x00A4A2D8) against M6 0.11, gapB P1..P2/P8 and gapE 3.5..3.6. +0x3C is the integer __aeabi_uidiv(48000, outRate) (0xA470B0), and the sample index is (phase>>16)-1, so the ctor phase 0x10000 still starts at input[0]. The class is not wired into the runtime: WwiseAudioSource still resamples with its own windowed-sinc ToRobotRate/Lanczos, so the production path this record describes is still the old code.
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: Init 0x00A47038, SetPitch 0x00A47384, Execute 0x00A47178, kernel table 0x0103C0B8; kernels 0x00A48F5C, 0x00A4913C, 0x00A49E40, ramp 0x00A4A2D8
* outstanding: wiring: WwiseAudioSource.ToRobotRate/Lanczos is deliberately untouched until the live voice/mixer (M6 batches 3-4) can replace it with WwiseResampler; the row's format offsets (which fields carry channels and the input rate) are not in the rows and are carried as named fields on WwiseResamplerFormat. Not read by the rows, and refused with NotSupportedException rather than guessed: the stereo int16 kernel 0x00A49634, the float stereo kernels, and the float mono bypass 0x00A479F4 and ramp 0x00A4A958 (a float Execute is valid only in the read mode 1, kernel 0x00A49E40). Execute's zero-input 0x11/17 and the ctor's initial phase 0x10000 (+0x2C, 0x00A46D80..0x00A46D88) are cited from outside the scoped rows (gapD D2.8; the ctor).

**M6-005 — FNV-1 32-bit with ASCII lowercasing over at most 0x103 bytes** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHash.cs`
* effect: event and object ids hash differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: GetIDFromString 0x0099DB84: strncpy copies min(strlen+1, 0x103) bytes (0x0099DBB0), then lowercases and hashes strlen bytes (0x0099DBD4/0x0099DC04); for a NUL-terminated name of length L the hashed input is min(L, 0x102) bytes
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-006 — Control path: queued PostEvent, ExecuteEvent, EnqueueOrExecute (frames + remainder; PlayAndContinue one frame early), drain order, Play/Stop/Seek execute, switch resolution** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseEventRuntime.cs`
* effect: events play different sounds or at different times
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: PostEvent 0x009A6704 / core 0x9A0EF8; pump 0x9AE0B0; ExecuteEvent 0x9AA3DC; EnqueueOrExecute 0x9AA0FC; drain 0x9A9F88; Perform 0x9AF8A8; Play 0xA62D38 / 0xA62A1C; Stop 0xA663C8; Seek 0xA645C8; Switch PlayInternal 0xA2C730
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-007 — Random/sequence step selection: global time-seeded 64-bit LCG, shared state, k-th eligible, shuffle, avoid list, weights, sequence wrap/ping-pong** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSelection.cs`
* effect: a different alternative plays
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: LCG 0xA08A7C..0xA08AC0; SetSeed 0x99DB58 from SoundEngine::Init 0x99EF80; state 0xA09698 / 0xA099BC; SelectPlayable 0xA0A3B4; SelectRandomly 0xA08A44; avoid 0xA08694; sequence 0xA0A524..0xA0A6E8
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-008 — Continuous containers: loop semantics, next chosen at voice start, modes 1/2 cross-fades, mode 4 same-voice chaining, mode 5 trigger rate, EndOfEvent at the last PBI Term** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseContinuous.cs`
* effect: looping and chained sounds play differently
* rests on: WwiseContinuous.cs implements the continuation decisions from re-analysis/inventory/M6-wwise-bank.md Appendices G §2 (2.1..2.9) and H §1 (1.1..1.7): the loop info and its drawn count at 0xA091CC (0xA09230..0xA093F0), the ping-pong reversal and start-side count at 0xA0863C..0xA08630, the next-item selection at 0xA09C40, PrepareNextToPlay 0xA6A07C, the modes 1/2 scheduling 0xA6A580, the cross-fade gains 0xA35998, the mode-4 chain 0xA6A2DC and the mode-5 trigger and re-entry period 0xA09F04/0xA0A16C..0xA0A1C0, with H's corrections (the PlayAndContinue delay is frame-quantised with a lookahead frame and a start offset; src+0x10 bit0 is a StartStream latch, not a duration flag; all 18 stereo media are robot-routed). Standalone decision module: no audio production and not wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler.
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: PlayInternal 0xA0AFDC; loop info 0xA091CC; next 0xA09C40; PrepareNextToPlay 0xA6A07C; modes 1/2 scheduling 0xA6A580; PlayAndContinue 0xA62ED4; fades 0xA35998; mode 4 0xA6A2DC, voice attach 0xA4304C, switch 0xA52B90 / 0xA549A0; mode 5 0xA09F04; end 0xA6ACC0; EndOfEvent 0xA03618; ping-pong loop count 0xA0863C..0xA08664 (forward reversal: forward=0, index-- and no count/end check) and 0xA085E4..0xA08630 (start reversal: bit0 clear -> end, bit1 -> no decrement, else count-- and 0 -> end); loop-count draw 0xA09230..0xA09264 / 0xA09350..0xA093F0: only when loop >= 2; fraction = (rng.Hi>>1)/2147483647.0; draw = (int)(0.5 + fraction*(short)(loopMax-loopMin)); count = (short)(loop+loopMin+draw) clamped >= 1; mode-5 re-entry period 0xA0A16C..0xA0A1C0: max(transitionTime_ms/1000, 0.022) + params+0x74/48000, where params+0x74 is the PBI start offset in samples (pending+0xC) and params+0x7C == 0 so no InitialDelay term; unobservable in shipped banks: every RanSeq +0x91 byte is 0x12/0x1A (ping-pong bit5 clear) and all 25 continuous containers are loop 0 or 1
* outstanding: the live voice/PBI is not built, so the model stops at decisions: the mode-4 PBI continuation internals beyond the start offset and chain id (gapG 1.3..1.7), the action manager's exact delay resolution for modes 1/2 (gapF 2.5 / gapG 2.2..2.7: the frame/lookahead split is not computed), EndOfEvent counting and PBI Term latency (gapF 2.9, gapG 3.3/3.4); the shared/per-object state path when bank bit1 is clear (gapD D4.2) is modelled only as the FreshStateFlag bool, and the shared/per-object distinction is not built; mode 3 has no enum value because the rows do not name it; a zero-frame cross-fade (gapF 2.6) throws NotSupportedException; the start-notification drain and src+0x10 bit0 timing (gapG 4.1/4.6) are not modelled. The ping-pong end rule (gapF 2.3, 0xA0863C..0xA08630), the drawn loop count and the loopMin/loopMax range (gapF 2.2, 0xA09230..0xA093F0) are source-exact, but unreachable in the shipped banks: every RanSeq +0x91 byte is 0x12/0x1A with the ping-pong bit (bit5) clear and all 25 continuous containers are loop 0 or 1, so they are not capture-verifiable.

**M6-009 — RTPC: curve shapes and scaling after the curve, sum/product accumulation, value store precedence with STMG defaults at entry+8, bus RTPC empty key, immediate ramps** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseRtpcStore.cs`
* effect: parameter curves change sounds differently
* rests on: WwiseRtpc.Evaluate/ApplyScaling/EvaluateScaled (WwiseHierarchy.cs) implement the curve shapes and post-curve scaling; WwiseRtpcStore.cs implements the value store, lookup precedence, accumulation, ramps and the set entry points, from re-analysis/inventory/M6-wwise-bank.md gapF 1.1..1.10, gapE 7.1..7.4 and gapA 5.1..5.4
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: curve 0xA14E28..0xA15244; accumulation 0xA17724 / 0xA17878; node pull 0xA11590; store 0xA0F07C, STMG defaults 0xA0F594; set 0xA1404C..0xA12CA0; lookup 0xA17280; bus key 0x9C3A48..0x9C3A78; ramp 0xA137D8..0xA13A60: type 1 up/down zero-rate check (0xA139E8/0xA13A50 vcmp.f32 s14,#0 -> 0xA139F4/0xA13A58 beq skips the divide, no duration); duration = vcvt.s32.f32 truncation (0xA13A04/0xA13A40); final = signed max(computed, callerTime) (0xA139BC..0xA139C8); >0 -> transition 0xA0E5E4, else immediate 0xA12CA0 (0xA13940..0xA13944); ramp gates gapF 1.5 omits: explicit-time byte [arg6+8] (0xA139A0..0xA139A8); when arg1==0 the 0xA1B5FC(entry.id,key) transition gate (0xA13948..0xA13A24); shipped STMG 0xCE871DAC: type 1, up 2.0, down 0.0, builtin 1 (Init.bnk 0x498); builtin 1 -> 0xA0F678
* outstanding: the store is not wired into the live voice/bus graph (M6-006, M6-010); the transition's value evolution across 0xA0E5E4 is not modelled (gapF 1.5 gives only the duration); the 0xA1B5FC(entry.id, key) transition gate (0xA13948..0xA13A24, correction C2) is not modelled — its meaning is only partly read, so a positive duration whose exact written slot had no valid prior value is reported as GatedTransition; WwiseStmgParam.BuiltIn (0xA0F678) is dropped (semantics UNKNOWN)

**M6-010 — Gain composition: GetAudioParameters links and sums, randomizer once per voice, dBToLin fast pow, game-defined aux send gain, muted dry path, Bus Volume after FX (robot_volume does not reach the Hijack)** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseGain.cs`
* effect: the robot gets a different level
* rests on: built from re-analysis/inventory/M6-wwise-bank.md gapC 1.1..1.11, 2.1..2.8, 3.2 and gapE 1.1 into a standalone class (WwiseGain.cs): GetAudioParameters over the parent (+0x34) / output-bus (+0x38) links with the paramSelect sums and the gated/global/game-object bundles (gapC 1.2..1.4), the per-voice randomizer draw (gapC 1.5/1.6), the fast-pow dBToLin with the −37·20 dB floor and the mute/fade product (gapC 1.11), the game-defined and user aux decisions and send gains (gapC 1.7/1.8, 2.1..2.3), the muted dry path (gapC 2.2/2.4), the collapsed-bus fold (gapC 2.7) and the bus-volume-after-FX placement (gapC 3.2); the RTPC pull composes with the M6-009 store (0xA11590). Caller-side inputs are the node graph and its links, the additive bundles of gapC 1.4 whose writers the rows call RECOVERABLE_GAP, the game object's send value, the 3D attenuation, the fade factors pbi+0x168/+0x16C, the send-emit thresholds and the 0x9FAE18 root-note value; the class is not yet wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: GetAudioParameters 0x9EF258 (links 0x9F1C40 / 0x9F1F4C); CalcEffectiveParams 0x9FFAD4; dBToLin 0xA4B608..0xA4B674; sends 0x9BD368..0x9BD8B4; mix gains 0xA597A4..0xA597BC, 0xA4FBEC; bus gain after FX 0xA4FEF8 / 0xA4D994; collapsed buses 0x9C54E8; C10 thresholds: STMG call 0x9B0B20..0x9B0B4C; setter 0x9A080C..0x9A08FC; linear consumer 0x9BD37C..0x9BD404; dB consumer 0x9BD5F0..0x9BD614
* outstanding: not wired into the live voice/bus graph (M6-011/M6-012/M6-013, M6-014, M6-017); the record stays IMPLEMENTATION_GAP until that wiring exists. C10 settles [0x1052450] = -80.0f and [0x1052454] = binary32 0x38D1B717 (0.0001f). The additive bundles of gapC 1.4 and their writers remain RECOVERABLE_GAP caller inputs; 0x9FAE18 (the Sound override's root note) remains a RECOVERABLE_GAP caller input; the 3D attenuation and pbi fade factors are caller inputs. The paramSelect masks, MakeUpGain inclusion, randomizer order, global/object MuteRatio keying, ducking maximum and the M6-011 LPF/HPF handoff remain as recorded in gapC/gapE and the standalone implementation provenance.

**M6-011 — Voice filter A: Butterworth LPF/HPF biquad before the aux sends, value-to-cutoff map, 8-step chunked ramp; filter B dry-only** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseVoiceFilter.cs`
* effect: the robot audio has a different tone
* rests on: built from re-analysis/inventory/M6-wwise-bank.md gapE 1.1..1.9 into a standalone class (WwiseVoiceFilter.cs); the aligned 4-sample NEON block matrix is not transcribed, so every sample uses the row's scalar DF-I form, which is equivalent and not block-exact (MD3); the class is not yet wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: composition 0x9FFD14..0x9FFD74; filter call site 0xA44630 step (2) -> 0xA4C60C (thunk r0+=0x10; b 0xA766B8) -> 0xA766B8 -> 0xA766F0 (LPF) / 0xA77480 (HPF); 0xA4BC58 is the per-connection gain/buffer update, not the filter body (C11); targets 0xA550D8..0xA551EC; order 0xA44630; LPF 0xA766F0 / HPF 0xA77480; cutoff 0xA7A3D8 / 0xA7A4AC; ramp 0xA76728..0xA77428
* outstanding: not wired into the live voice path. The NEON aligned-block matrix 0xA767BC..0xA769C4 is not transcribed (scalar DF-I equivalent, not bit-exact). The 3D attenuation values are caller inputs; the inventory does not settle their source. gapE 1.1's pbi+0xA0/+0xA8 are inputs defaulting to 0 because the writer scan covered direct offsets only. gapE 1.7's HPF example '40 -> ~445' contradicts the row's own formula (map(100-40) ~ 527); it is not pinned and was reported up. gapE 1.8's +0xA countdown is armed to 4 only when a ramp finishes with the target at or below 0.1 (the ramp-start paths zero it, 0x00A76770/0x00A76A0C), so the ramp buffer and four more are filtered before the bypass; a target change whose current and new target are both at or below 0.1 bypasses at once with no ramp (0x00A769EC..0x00A76A2C).

**M6-012 — Mixer: linear per-frame gain ramp, first update not ramped, mono to mono 1.0, stereo to mono 0.70710677 per channel** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseMixer.cs`
* effect: mixing levels differ
* rests on: built from re-analysis/inventory/M6-wwise-bank.md gapE 2.1..2.7, gapF 3.1 and gapG 5.1..5.3 into a standalone class (WwiseMixer.cs); the double-buffered gain pair and the first-update force (gapE 2.5), the concrete start/delta ramp (gapE 2.2..2.4), the consume/zero-pad order (gapE 2.1) and the two shipped robot matrices (mono->mono 1.0, stereo->mono 0.70710677) are built; the pan result is a caller-supplied matrix, not computed by the class; the per-connection gain product of gapE 2.1 is the caller's composition (M6-010); the class is not yet wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: ConsumeBuffer 0xA4FBEC; mixer 0xA45E9C; ramp kernel 0xA46668; pan 0xA25FF8 / 0xA1F79C / 0xA209BC (table 0xFFA970)
* outstanding: not wired into the live voice/bus graph (M6-010, M6-014, M6-017). The general 2D panner arithmetic (gapE 2.6, 0x00A25FF8) beyond the settled mono->mono and stereo->mono matrices is not built; the pan result is a caller input. LFE->LFE (gapE 2.2) is refused with NotSupportedException because the row names it but gives no arithmetic. The conn+0x6C bit2 fade-in (gapE 2.5) is a caller flag; its arming condition (the ctor arg = !(voice+0xCD bit0)) is not modelled. The ramp is the scalar g(k)=start+k·delta over all frames; the native's NEON 4-lane/8-sample ordering is not transcribed (equivalent, not bit-exact, MD3).

**M6-013 — Robot_Bus FX chain in slot order: two Parametric EQs and the Peak Limiter before the Hijack, with their settings and algorithms** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBusFx.cs`
* effect: the robot audio is equalised and limited differently
* rests on: a standalone class built from re-analysis/inventory/M6-wwise-bank.md gapC 3.1, 4.1..4.9, the gapC addendum and gapE §0 (WwiseBusFx.cs): the Robot_Bus chain in slot order EQ 0x6767FC1F -> EQ 0x174901C6 -> Peak Limiter 0xDF2230FF -> Hijack while the bus state is 1 (gapC 3.1, D2.4); the two EQ ShareSet settings (gapC 4.2); the EQ coefficient routine (gapC 4.3), scalar direct-form-I biquad with the row's accumulation order (gapC 4.4, 0xAA2324) and the output-gain ramp with the scalar-tail restart quirk (gapC 4.4); the limiter settings and defaults (gapC 4.5), the setup L = u32(float(sr)*0.009) = 431 at 48 kHz with attack = expf(-2.2/(L/2)) and release = expf(-2.2/(sr*release)) (gapC 4.6), and the unlinked/mono DSP with the peak-hold detector, fast log, fast-pow gain, first-buffer pre-scan and the L-sample tail (gapC 4.7). The fast pow is the shared M6-010 WwiseGain.DbToLinear (gapC 1.11). WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler and the M6-014 bus lifetime are untouched
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: FX loop 0xA4FD84..0xA4FEF4; registration .init_array 0x4DEB18 / 0x4DEB8C; EQ coefficients 0xAA25E0, execute 0xAA2A84, biquad 0xAA2324; ShareSets 0x6767FC1F, 0x174901C6; limiter setup 0xAA19CC, DSP 0xAA0EB4; ShareSet 0xDF2230FF
* outstanding: not wired into the live voice/bus graph (M6-014/M6-017); it does not implement IWwiseBusInsertFx, so the M6-014 bus cannot run it until a later wiring pass adapts it. CHOICES reported to the manager, not settled by the rows: (1) the frozen row gapC 4.3 names the Butterworth LP/HP and RBJ band-pass/notch/peaking/shelf designs and gives their intermediates (c and b0 for LP/HP; w0, alpha, A for RBJ; S = 1) but does NOT transcribe the numerator/denominator polynomials; the LP/HP companion terms here are the same Butterworth design the inventory writes out in full for the voice filter (gapE 1.6) and the RBJ terms are the named cookbook's; (2) the row's shelf alpha = sin·sqrt2/2 is used literally, which is not the usual RBJ S = 1 alpha = sin(w0/2)·sqrt2; (3) the output-gain ramp's NEON lane increments and the exact scalar-tail restart split are not transcribed, so the aligned part uses a single per-sample delta and the tail restarts from prev; (4) the limiter's fastpow10 cutoff is not stated, so the shared M6-010 fast pow is used unchanged (it returns 0.999039 at 0 dB, a factor the detector and output gain both carry). NOT BUILT, named in the rows with no arithmetic: the linked limiter detector (0xAA1464) and LFE variant (0xAA09B8) are refused with NotSupportedException (the shipped ShareSet is channelLink = 0). The EQ processLFE shipped value is not in gapC 4.2 and defaults false (inert on the mono bus); per-planar-channel EQ state is not modelled (the shipped bus is mono, 0x4101)

**M6-014 — Bus and Hijack lifetime: on-demand bus, lazy FX instantiation, destroyed after an idle frame with no connections, the one-frame tail and zero-length chunk** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBusLifetime.cs`
* effect: robot audio streams open and close at different times
* rests on: built from re-analysis/inventory/M6-wwise-bank.md gapD D2.1..D2.9 into a standalone class (WwiseBusLifetime.cs): on-demand CreateMixBus with key reuse and +0x1CC b0 (D2.1), lazy SetInsertFx at the first GetResultingBuffer with Init vt+0x1C then Reset vt+0xC before the first Execute vt+0x20 (D2.2), the FX loop gated on state +0x1BC == 1 in slot order with the slot-b0/+0x1B8-b0 newly-bypassed Reset (D2.4), the ReleaseBuffer state machine (+0x68 == 0x11 ? 4 : 1, non-bypassed out-of-place override, +0x6E = 0, buffer zeroed, D2.5), the empty-input tail frame (D2.8) and the destruction predicate (state != 1, +0x1C0 == 0, +0x1CC b0 == 0) with Term vt+8 (D2.6); the bus identity key, the FX slots, the frame size (M6-018) and the UpdateBuffer chunk sink are caller inputs; the class is not yet wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: CreateMixBus 0xA42210; GetResultingBuffer 0xA4FEF8 / SetInsertFx 0xA4F754; ReleaseBuffer 0xA4F36C; destruction 0xA43F64; teardown 0xA4ECE4; UpdateBuffer 0x005985FC
* outstanding: not wired into the live voice/bus graph (M6-010/M6-012/M6-013/M6-015/M6-017); the record stays IMPLEMENTATION_GAP until that wiring exists and until the FX chain itself is built (M6-013/M6-015). The aux-send connection policy (gapB P9) is RECOVERABLE_GAP and stays a caller decision (Connect/Disconnect). The bus-pass gating flag writer (D2.8, the arg of 0xA44C18) is settled by C11: gate2 0x108DB08 is written at 0x9EAE18/0x9EC47C/0x9EC4A0 and gate3 0x1052430 at 0x9EAE24/0x9EBBAC/0x9EC488/0x9EC4A8 (SetOutputDevice 0x9EC418); the runtime value is HARDWARE_ONLY. Caller inputs, not recovered values: the reuse-key field meanings (node ctx +0x4C/+0x50, device key +0x28/+0x2C), the bus-level FX bypass +0x1B8 b0 and the slot bypass bits (from the bank, gapB P6), the FX factory (create 0x9CC2AC / type-3 check 0x9CC4D8 / async flag / in-place flag / slot state), and the flushed chunk (M6-015). The eState (+0x68) initial value is not read (D2.1 Init only sets +0x1BC = 4); the class starts it at 0x11, the value ReleaseBuffer writes, so a never-mixed bus releases to state 4 - the native creates a bus only when it is mixed into, so that path is unreachable there. Disconnect throws rather than letting +0x1C0 underflow. State 2 (the not-reusable value in D2.1) is never produced by this class's transitions, so that reuse branch is unreachable until another owner sets it.

**M6-015 — The Hijack plug-in: registration (last caller wins), Init (1024 floats/channel, resampler 22320, pitch 0), Execute to 744-sample chunks, Term** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHijackPlugin.cs`
* effect: the robot receives different audio frames
* rests on: built from re-analysis/inventory/M6-wwise-bank.md rows M6 A13..A18 and gapB R1..R5 into a standalone class (WwiseHijackPlugin.cs): the static registration {type 3, company 0x12C, id 1, create 0x008DBC71, params 0x008DBD11} prepended to g_pAKPluginList (R3, 0x004DD90C); RegisterPlugin swapping the instance into the global std::function with the last caller winning (R2, 0x008DB300; R5 leaves plug-in B's CozmoAudioController callbacks live); HijackFx create allocating the effect and calling the global to bind fx+0x0C = 22320, fx+0x10 = 744 and the create/process/destroy callbacks (R4, 0x008DBC70; A14, 0x008DB47C..0x008DB4EA); Init's numChannels x 1024-float allocation (numChannels from the format channel-config byte; 0 fails with error 2), CAkResampler Init(fmt, 22320), SetPitch(0), then the create callback (A15, 0x008DBD74); Execute resampling the caller's float input to 744-frame chunks through WwiseResampler (M6-004, gapB P8) and firing the process callback per chunk (A16, 0x008DBFE8); Term firing the destroy callback (A18, 0x008DBDF8). The audio-buffer callbacks (PrepareAudioBuffer 0x005984E8, UpdateBuffer 0x005985FC, CloseAudioBuffer 0x0059878C) and the input source are caller inputs; the class is not yet wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler. WwiseResampler gained LastProducedFrames (the native output buffer's frame count; no arithmetic change) so Execute can report the chunk count the native flushes as validFrames.
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: RegisterPlugin 0x008DB300; static registration 0x004DD90C; create 0x008DBC70; Init 0x008DBD74; Execute 0x008DBFE8; Term 0x008DBDF8; SetupPlugins 0x005942C6..0x00594354; C10 persistent output: layout 0x008DBF76..0x008DBFB6; Execute/reset 0x008DBFE8..0x008DC034; empty-input result 0x00A47178..0x00A47188; mono kernel stores 0x00A49FEC..0x00A4A020
* outstanding: not wired into the live bus/voice graph (M6-013/M6-014/M6-017); the record stays IMPLEMENTATION_GAP until that wiring exists. Caller inputs are the create/process/destroy callbacks and the float bus input. C10 settles that the 1024-float-per-channel allocation backs a persistent AkAudioBuffer: uValidFrames accumulates across Execute calls and is reset only after DataReady (0x2D) or NoMoreData (0x11) invokes the process callback. B1 repaired the standalone class to C10 (a persistent `_validFrames`, flushed and reset only after DataReady/NoMoreData invokes the process callback); the record stays IMPLEMENTATION_GAP until wired into the live bus/voice graph.

**M6-016 — The engine robot-audio path: alternatives drawn up front, wall-clock posting, event_volume per playing id, routing 7..10 to Robot_Bus_1..4, queued callbacks, states, PopRobotAudioMessage, abort** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseRobotAudioPath.cs`
* effect: animation sounds reach the robot differently
* rests on: a standalone class built from re-analysis/inventory/M6-wwise-bank.md rows M6 A1..A25 (minus the unbuilt rows named in unresolved), gapC 2.8 and gapE 6.1..6.4: A1 states; A2 the up-front draw and the +0x3D flag; A3 the no-events/no-buffer/OnDevice branches and Dispatch::Create(queue, 2); A4 PrepareAnimation/Update dispatch; A5 wall-clock Dispatch::After; A6/A7/A8 the lambda, PostCozmoEvent, the playing id and event_volume per playing id; A9 Complete/Error; A11 the 7..10 (and 6) routing; A19 UpdateLoading; A20 UpdateAudioFramesReady; A21 PopRobotAudioMessage; A22 the no-stream silence; A23 abort; gapE 6.1..6.4 the queued context and drain. WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler are untouched
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: InitAnimation 0x0059687E..0x00596914; BeginBuffering 0x00597F12..0x00597F8E; lambda 0x0059818C..0x005982A0; routing 0x0059962A..0x005999A4; callbacks 0x00599E6A..0x00599EB8, 0x008D8D40, drain 0x008D88CC from 0x004ED6C4; UpdateLoading 0x00597C9E..0x00597D7E; PopRobotAudioMessage 0x00597DB4..0x00597E8E; abort 0x0059678E..0x005967B8
* outstanding: not wired into the runtime (WwisePlayback, WwiseAudioSource, WwiseSongRenderer, AnimationScheduler); the wired subsystem is M6-017's job. Not built from this record's rows: A3's OnDevice path (game object 6, 0x00596DC8) throws NotSupportedException, and A2's +0x3D is carried without its UNKNOWN use; A10's audio-thread scheduling and A12's mix rate are M6-017/M6-018; A13..A18 are the Hijack/bus records M6-013..M6-015; A24's robot_volume RTPC reach is unread (RECOVERABLE_GAP); A25's second Hijack registration is M6-015

**M6-017 — Audio-thread frame model: Perform order, sink-driven frames, EndOfEvent after the bus pass of the last frame** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseFrameDriver.cs`
* effect: audio events end at different times
* rests on: the standalone modules already built: WwiseEventRuntime (M6-006) supplies the message pump, the pending-action drain and the audio manager tick (+0x4C); WwiseFrameDriver (new) applies gapD D1.6's Perform order and D3.5's EndOfEvent position around a caller-supplied render seam. The three render steps (the row's "buses" group, LEngine 0xA57FF8, PBI-notification flush 0xA38420), the sink (gapB T2) and the frames-per-Perform count (D1.7) are caller inputs; M6-008..M6-016 plug into the render seam. The record stays IMPLEMENTATION_GAP while unwired.
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: Perform 0x9AF8A8 / 0x9AF9F4..0x9AFAB8; audio thread 0xA4087C; RenderAudio 0x9AFD10; PBI teardown 0xA38600 / flush 0xA38420; EndOfEvent 0xA03618; C11 correction: the three gate bytes have writers and the branch is dynamic. gate1 0x108DAF0 written at 0x9EB090/0x9EBBA8/0x9EC00C/0x9EC1B4; gate2 0x108DB08 at 0x9EAE18/0x9EC47C/0x9EC4A0; gate3 0x1052430 at 0x9EAE24/0x9EBBAC/0x9EC488/0x9EC4A8; SetOutputDevice 0x9EC418 (call site 0x9AE3B0); output-device state struct 0x108DAE8; C11 audio thread: SoundEngine::Init 0x0099E3EC (thread-active byte 0x0108D949) -> FUN_009B0200 -> FUN_00A40940 (sem_init 0x4D6784, pthread_create 0x4A6934, entry 0x00A4087C, sem engine+0x54); thread loop Perform 0x9AF8A8 then sem_wait 0x4D679C; sem_post helper 0x00A40924; C11 render pump: RenderAudio veneer 0x0099F130 -> 0x9AFD10(engine,1) (sem_post engine+0x54 when the thread flag is set, else Perform); engine tick CozmoEngine::Update 0x004ED4D4 -> AudioMultiplexer::UpdateAudioController 0x008DF3DA -> AudioEngineController::Update 0x008D2928 -> FUN_008D88C0; robot pump RobotAudioClient::ProcessEvents 0x00599FE2 -> ProcessAudioQueue 0x008D2946 -> FUN_008D88C0; C11: the four Perform group members (0xA36AC4, 0x9FF308, 0x9D3C98, 0x9E6D2C) and the voice engine inside LEngine (0xA57FF8 -> 0x44D4C) are M6-022
* outstanding: not wired into the runtime (WwisePlayback, WwiseAudioSource, WwiseSongRenderer, AnimationScheduler); the wiring pass is separate. C11 corrected the frame model: the gate bytes have writers (the bus-pass arg is (gate2==0)?1:gate3 and the frames-per-Perform branch is dynamic; the runtime value depends on the Wwise output-device state and is HARDWARE_ONLY). The audio-thread lifecycle is now read (SoundEngine::Init -> FUN_009B0200 -> FUN_00A40940 pthread_create entry 0x00A4087C, semaphore engine+0x54, sem_post 0x00A40924) and is part of this record's production path. The four group members and the voice engine inside LEngine are M6-022. The render pass is a caller seam; D1.7's frames-per-Perform count is the device path (0x9D4778 -> 0x9EBE6C(0)) or the clock-paced branch, and the device/sink pacing (gapB T2) is HARDWARE_ONLY. M6-006's only public tick advance is AdvanceFrame(), which also pumps and drains; after the driver's explicit pump and drain those calls are no-ops, so the order stays messages -> drain -> render -> tick. The pending-action drain is M6-006's minimal play-count model (0xA04F54 internals unread); the EndOfEvent callback-manager internals (D3.3) and the post-Term latency (D2) remain those records' gaps.

**M6-018 — Mix rate 48000 Hz and frame 1024 samples (phone-dependent in the original: min(native, 48000) and hardware rounding)** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseRuntimeSettings.cs`
* effect: derived timings and resampling differ
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M6-wwise-bank.md
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: platform init 0x00A57724: rate cache 0x0108DF90 = min(getNativeOutputSampleRate, 48000), 48000 default 0x00A578B4..0x00A578C8; frame rounding 0x00A577B8..0x00A57830; SetRate 0xA1C75C / SetFrame 0xA1C7D4
* outstanding: compare the code against the inventory rows (the step after approval)

**M6-020 — STMG: the group-item field meanings and the two trailing bodies (unread source; unexercised by shipped banks)** (not on the live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseStmg.cs`
* effect: a non-zero trailing count or a named group item would be read differently from the engine
* rests on: the stack keeps the group items raw and refuses a non-zero trailing count (WwiseRuntimeTests.AnStmgSectionAfterTheParameterTableIsRefusedRatherThanGuessed); the engine instead reads the bodies
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: C9 state items: reader 0x009B0C9C..0x009B0D0C; handler 0x00A27CA4..0x00A27D60 keys 12-byte entries by words 0/1 and stores word 2 at +8; a nonzero handler flag also writes the reversed pair at 0x00A27D78..0x00A27DD0, while the bank reader passes zero; C9 switch items: handler 0x00A325E0..0x00A32914 copies source word 0 to generated curve point +0, float(index) to +4, source word 2 to +8, and source word 1 to a parallel ID array before calling 0xA12050; C9 trailing A: reader 0x009B0FBC..0x009B127C reads u32 id + 6*u16 + 10*u32; 0x00A3B84C..0x00A3B998 deduplicates by id/refcount or allocates 0x48 and copies 56 bytes to object+0x10; numeric property stores 0x00A3B0F8..0x00A3B1E8; C9 trailing B: reader 0x009B12A4..0x009B1410 reads u32 id + 9*u32; 0x00A3BA44..0x00A3BB80 deduplicates by id/refcount or allocates 0x38 and copies 40 bytes to object+0x10; numeric property stores 0x00A3B1EC..0x00A3B268; human-readable trailing-object class/property names are stripped and BLOCKED_EXTERNAL; raw byte behavior is settled; both trailing counts and every state-item count are zero in shipped banks
* outstanding: Build the C9 raw layouts and consumer behavior in WwiseStmg.cs, including nonzero trailing counts; do not invent names for the stripped trailing-object types or numeric properties. Public Wwise labels for the state/switch triples are labels only. Human-readable trailing names remain BLOCKED_EXTERNAL and are not required for the exact byte path.

**M6-022 — The live voice and bus engine: the 0xA57FF8 wrapper and 0xA44D4C render body, the voice pass, the bus pass, idle removal, the per-voice DSP chain, the four Perform group members, the PBI flush, the 0x108DAE8 output-device state and its three gate bytes, and the JNI audio-route poll** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseVoiceEngine.cs`
* effect: the live voice and bus graph renders differently, or not at all
* rests on: read from libcozmoEngine.so in re-analysis/research/20260927-B1-voice-engine-extraction.md (rows B1-V1..V30), citation-checked in re-analysis/research/20260928-I-M6b-citation-check.md, and gap-passed in re-analysis/research/20260928-I-M6b-gap1-extraction.md; frozen as correction C11. The four group members' Wwise class names are UNKNOWN (no RTTI/symbols). The callee bodies C11 named but did not give were read by the five C12 passes (re-analysis/research/20260928-B-M6b-voice-callees.md, -bus-group-callees.md, -bus-metering.md, -source-classes.md, -modulator-evaluator.md), frozen as correction C12.
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: wrapper 0xA57FF8 -> 0xA57D64 + 0xA44D4C; render body 0xA44D4C pre-loop over the output-device list 0x108DB04 (0xA44D5C..0xA44DA0); throttle 0x108DA9C/0x108D870+0x4c (0xA44DAC..0xA44DD4); voice pass 0xA44948 (pre-pass 0x9D3CC0/0xA43D24/0xA39564; voice list base 0x108DF54, head +0x14, next +0xD0, state +0xDC); bus pass 0xA44C18; idle removal 0xA43F64; voice mix dispatcher 0xA44630; params fill 0xA54F1C; source execute 0xA548C0; pitch 0xA53134; resampler execute 0xA52D4C; mix-in 0xA4FBEC; mixer 0xA45E9C; callee bodies 0xA4C60C -> 0xA766B8 (0xA766F0 LPF / 0xA77480 HPF), 0xA56E00 -> 0xA56A7C, 0xA03E8C, 0xA05574, 0xA56650, 0xA4F9E0, 0x9E9E78, 0x9E9F08, 0xA55750, 0xA4AF50, 0x9D3CC0; group members 0xA36AC4, 0x9FF308, 0x9D3C98, 0x9E6D2C; Perform call order 0x9AFA4C..0x9AFAA8; PBI flush 0xA38420; output-device state struct 0x108DAE8 (gate1 0x108DAF0 at +8, 0x108DAFC at +0x14, list 0x108DB04 at +0x1C, gate2 0x108DB08 at +0x20, 0x108DB0C at +0x24, 0x108DB18 at +0x30); gate3 0x1052430; gate writers: gate1 0x9EB090/0x9EBBA8/0x9EC00C/0x9EC1B4; gate2 0x9EAE18/0x9EC47C/0x9EC4A0; gate3 0x9EAE24/0x9EBBAC/0x9EC488/0x9EC4A8; module init 0x9EADE8; term 0x9EAF90; device advance 0x9EBA54; SetOutputDevice 0x9EC418 (call site 0x9AE3B0); frame count 0x9D4778 -> 0x9EC38C -> 0x9EBE6C; JNI audio-route poll 0xA57D64..0xA5803C (GetEnv/AttachCurrentThread, FindClass, NewStringUTF, GetMethodID, CallObjectMethodV 0x593E58, CallBooleanMethodV 0xA58008, result 0x108DF98, notify 0x9EA66C); C12 voice callees (report 20260928-B-M6b-voice-callees.md): device node vt+0x2c=0x9E935C / vt+0x30=0x9E9420, node vtable 0x103B498 (0xA44D4C pre-loop); 0xA43D24 ducking/volume pre-pass (0xA43D24..0xA43EFC; 0xA55750 -> bus +0x88/+0x8C -> 0xA4AF50 -> 0xA437E0 -> 0xA4B4B0; container 0x108DF50, bus count 0x108DF54, voice count 0x108DF5C, voice head 0x108DF64); 0xA39564 node cleanup (0xA39564..0xA395FC); 0xA54F1C full state machine (0xA54F1C..0xA5574C) with 0xA4B4B0 and 0xA4BC58 (0xA4BC58..0xA4C000); 0xA44630 full order (0xA44630..0xA44938); 0xA548C0 and bus vt+0x58 = 0x9C07C4 (0x103ACE0); 0xA52D4C / 0xA5268C / 0xA4721C / 0xA47224 (0xA52D4C..0xA53134); 0xA55D04 / 0xA55A84 / 0xA54A30 and the state-0x11 tail (0xA44A94..0xA44BFC); 0xA01BD8 (0xA01BD8..0xA01CA0); 0x9E84C8 (0x9E84C8..0x9E859C); C12 bus-output/group callees (report 20260928-B-M6b-bus-group-callees.md): 0xA4FEF8 branch/mask and non-FX path 0xA50120 (0xA4FEF8..0xA50174, 0xA50EA4..0xA50F04); 0xA4F754 SetInsertFx and 0xA4E974 per-slot create/init/reset (0xA4F754..0xA4F9AB, 0xA4E974..0xA4ECD4) and 0xA4E7CC slot drop; 0xA38420 PBI flush and 0x9D3470 (0xA38420..0xA385D7), PBI vtable 0x103B768 (+0x10=0xA029DC, +4=0x009FF54C), ContinuousPBI 0x103D3B0 (+0x10=0xA6ACC0), queue 0xA38600/0xA01800; 0xA3587C (0xA3587C..0xA358B0); 0x9FDD90 (0x9FDD90..0x9FDE48); 0x9D3644/0x9D3864/0xA01800/0xA41854/0xA431A8/0xA54480 (0x9D3644..0x9D3830, 0x9D3864..0x9D3C58); 0x9E6D2C purge (0x9E6D2C..0x9E6E10, 0x9E2AE4..0x9E2BC8, 0x9E21FC..0x9E24AC, 0x9E2510..0x9E2550, 0x9D8A24..0x9D8F54); C12 bus-output tail (report 20260928-B-M6b-bus-metering.md): 0xA4FEF8 tail 0xA4FF40..0xA50FD0 is the bus level-analysis/metering stage (min/max peak 0xA50D48, RMS 0xA50C84, filtered peak 0xA501B0, 0xA52164 at 0xA500B0; flag byte at [out+0x18]+0x20; buffer fields +0/+4/+0xc/+0xe/+0x10/+0x14/+0x18); 0xA4D994 bus gain/param update (0xA4D994..0xA4DD43) with 0xA25FF8; 0x9C806C callback dispatch (0x9C806C..0x9C8104); 0xA52164 biquad+loudness (0xA52164..0xA5266B); C12 source classes (report 20260928-B-M6b-source-classes.md): factory 0xA562B8 dispatch on (mode, plugin>>16); shipped plugin ids 0x00040001 Vorbis / 0x00020001 ADPCM; Vorbis via registered-plugin 0x9CC3EC (vtables 0x103E0B8 streamed / 0x103E138 in-memory); real render slots 0xA73D34 (ADPCM t1) / 0xA72554 (ADPCM t3) / 0xAB0448 (Vorbis streamed) / 0xAB1550 (Vorbis in-memory); vt+0x4C = 0xA72B14 (bit6 of [[source+0xC]+0x1BE]); [source+0xC] is the PBI, not the bus; source-class field layouts; C12 modulator evaluator (report 20260928-B-M6b-modulator-evaluator.md): 0x9E2BD0 (0x9E2BD0..0x9E52F3) per-voice LFO (five shapes 0..4) + transition evaluator and pool allocator; type-0 records 0x4C (list [arg+0x20]) and type-1 records 0x30 (list [arg+0x14]); 0x9E52F8 (0x9E52F8..0x9E5E8B) five-segment ramp
* outstanding: not built and not wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler (the wiring is the M6 build job). C12 supplies the callee bodies; the remaining gaps are: the bus metering DSP identity and coefficient set 0xA50044..0xA50FD0 (structure read, filter identity RECOVERABLE_GAP); 0xA25FF8 (12 KB, called by 0xA4D994) RECOVERABLE_GAP; the 0x9E2BD0/0x9E52F8 NEON lane order RECOVERABLE_GAP; 0x9FD910 RECOVERABLE_GAP; the FX-slot object identity RECOVERABLE_GAP; the [node+0x70] device sub-object identity UNKNOWN; the callback registry 0x0108D95C owner RECOVERABLE_GAP; the source-class and four group members' Wwise class names UNKNOWN. HARDWARE_ONLY: the native device frame count 0x9EBE6C's sink value, the OpenSL pacing, and the runtime value of the gate bytes (the bus-pass arg (gate2==0)?1:gate3 and the frames-per-Perform branch); the code paths and writers are settled.

**M6-023 — The app audio-input dispatch: Unity PostAudioEvent -> AudioUnityInput -> AudioMuxInput -> AudioMultiplexer -> AudioEngineController::PostAudioEvent -> Wwise PostEvent** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseEventRuntime.cs`
* effect: app-posted SFX, UI and VO events do not reach the engine
* rests on: read from the Unity code and libcozmoEngine.so in re-analysis/research/20260928-I-M6b-gap3-extraction.md (rows A1-A5, B1-B6); frozen as correction C11
* best authority: unity/ (decompiled Unity C#) and libcozmoEngine.so 3.4.0-1204
* evidence: Unity: unity/scripts/csharp/Anki.Cozmo.Audio/PlaySound.cs:29,72-82; GameAudioClient.cs:17-45; UnityAudioClient.cs:224-237,279-287; Anki.AudioEngine.Multiplexer/PostAudioEvent.cs:89-107; Anki.Cozmo.ExternalInterface/MessageGameToEngine.cs:14-16; native: AudioUnityInput ctor 0x00591590; HandleGameEvents 0x005919B8; vtable 0x1023CA8 reloc 0x1023CBC -> AudioMuxInput::HandleMessage 0x008DFC4C; AudioMultiplexer::ProcessMessage 0x008DED14; AudioEngineController::PostAudioEvent 0x008D1F20 -> 0x008D8CE4 -> Wwise PostEvent 0x009A6704
* outstanding: not built and not wired. The Wwise core PostEvent it reaches is M6-006.

**M6-024 — Bank and scene loading call sites: the CozmoAudioController six-bank list and InitScene, LoadAudioScene/LoadSoundbank, AddZipFiles** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSoundLibrary.cs`
* effect: the engine loads a different bank/scene set
* rests on: read from libcozmoEngine.so in re-analysis/research/20260928-I-M6b-gap3-extraction.md (rows F1-F3); frozen as correction C11
* best authority: libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40)
* evidence: CozmoAudioController ctor 0x00592BB0; InitializeAudioEngine 0x008D1D1E; RegisterAudioScene 0x005935E2; LoadAudioScene 0x005935EA; banks Init.bnk/Music.bnk/UI.bnk/SFX.bnk/Cozmo.bnk/Dev_Debug.bnk and scene InitScene; LoadAudioScene 0x008D2EE8 -> LoadSoundbank 0x008D2FE4; AddZipFiles 0x008D1E3E feeds the OBB archives
* outstanding: not built and not wired. Bank parsing is M6-001.

### M7-behaviour — Idle, mood and reactions

**M7-001 — The shipped AnimationTrigger, ReactionTrigger, BehaviorClass and BehaviorID enums** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/AnimationTrigger.g.cs`
* effect: trigger, class or behaviour identifiers differ from the shipped app
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: decompiled Unity enums checked against the engine EnumToString tables
* evidence: unity/scripts/csharp/Anki.Cozmo/AnimationTrigger.cs; unity/scripts/csharp/Anki.Cozmo/ReactionTrigger.cs; unity/scripts/csharp/Anki.Cozmo/BehaviorClass.cs; unity/scripts/csharp/Anki.Cozmo/BehaviorID.cs
* outstanding: Compare all generated enum values and consumers with the shipped sets.

**M7-002 — The shipped animation/reaction maps, behaviour configs and activity tree** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/AnimationTriggerMap.cs`
* effect: a trigger resolves to the wrong animation or reaction, or the activity tree selects the wrong configured behaviour
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: shipped OBB configuration plus libcozmoEngine.so loaders
* evidence: re-analysis/obb/assets/cozmo_resources/assets/AnimationTriggerMap.json (573 pairs); re-analysis/obb/assets/cozmo_resources/assets/CubeAnimationTriggerMap.json (40 pairs); re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/reactionTrigger_behavior_map.json; RobotDataLoader::LoadReactionTriggerMap 0x00520bc8; BehaviorManager::InitReactionTriggerMap 0x005a16e4; 178 shipped behaviour configs enumerated in re-analysis/inventory/M7-behaviour.md Appendix C
* outstanding: Compare the complete shipped map/config corpus and loader behavior with production.

**M7-003 — ReactToImpact FallingStopped, intensity, recalibration and animation state machine** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/ReactionTable.cs`
* effect: impact reactions gate, calibrate, time out or animate differently
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: BehaviorReactToImpact::InitInternal 0x006061f8; TransitionToPlayingAnim 0x00606348; AlwaysHandle 0x00606408; 1000.0 threshold literal at 0x00606470; 5.0 second wait literal 0x40a00000 in InitInternal
* outstanding: Compare/build the complete cited state machine, including trigger 0x1a0 and its 60 second action argument.

**M7-004 — All 30 live-idle tunables and defaults** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleParameters.cs`
* effect: idle blink, gaze and motion distributions differ
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: AnimationStreamer::SetDefaultParams 0x0057db40, read
* evidence: SetDefaultParams 0x0057db40..0x0057dcd0; decoded values in M7 inventory Appendix A row 4
* outstanding: Compare all 30 production values and their types with the decoded table.

**M7-005 — Blink is a fixed seven-frame squash multiplied onto the base face** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: blink shape, timing or composition differs
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: ProceduralFaceDrawer::GetNextBlinkFrame 0x00585f18; blink table 0x00c5aad8; FaceLayerManager::GenerateBlink 0x0058d2ac; ProceduralFace::Combine 0x005846a8
* outstanding: Compare the complete blink table and compositor call path.

**M7-006 — Eye shift moves the whole face through LookAt with the engine bounds** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: idle gaze displacement or clipping differs
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: FaceLayerManager::GenerateEyeShift 0x0058d100; ProceduralFace::LookAt 0x00584158
* outstanding: Compare the complete shift and bounds path.

**M7-007 — The eye dart interpolates to a persistent gaze and does not fade to centre** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: the gaze returns early, snaps, or removes the wrong layer
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: ITrackLayerManager::ApplyLayersToFrame 0x0058e644; AddToPersistentLayer 0x0058eaa0; FaceLayerManager::GetFaceHelper 0x0058cd80; KeepFaceAlive persistent dart call 0x0058d3e2
* outstanding: Compare interpolation, rewind/trim and persistence with the cited path.

**M7-008 — Idle timers are integer milliseconds advanced by the engine's 60 ms tick** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: idle actions fire at different times or are rescheduled when their track is busy
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: CozmoInstanceRunner::Run 0x0065b3a8 uses 0x03938700 ns; UpdateLiveAnimation decrements at 0x0057d650/0x0057d68a/0x0057d6ba; KeepFaceAlive decrements at 0x0058d388; idle clock increments at 0x0057d444
* outstanding: Compare initialization, inclusive integer draws, counter updates and blocked-track handling.

**M7-009 — Idle head and lift are keyframes of the live animation** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: idle head/lift uses different wire messages or variability
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: AnimationStreamer live Animation construction 0x0057a060; head keyframe 0x0057d85c/append 0x0057d866; lift keyframe 0x0057d9c0/append 0x0057d9ca; HeadAngleKeyFrame::GetStreamMessage 0x004f8c08
* outstanding: Compare the M7 scheduling interface to M5's live-keyframe path.

**M7-010 — Idle body shuffle and its paired turn eye shift** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: the robot stays still or shuffles/looks with different values
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: AnimationStreamer::UpdateLiveAnimation 0x0057d5f8, read
* evidence: body decision and draws 0x0057d6cc..0x0057d8f4; turn eye layer 0x0057d7fc; straight removal 0x0057d8d2; next spacing 0x0057d978
* outstanding: Compare the complete body/eye keyframe path with M5 streaming.

**M7-011 — A reaction is held off only where its own strategy config names a cooldown** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorArbiter.cs`
* effect: a reaction is suppressed or repeated on the wrong schedule
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so plus shipped reactionTrigger_behavior_map.json
* evidence: reactionTrigger_behavior_map.json: FrustrationMinor cooldownTime_s 60.0; other objective cooldowns remain per-objective; ReactionTriggerStrategyFrustration::ShouldTriggerBehaviorInternal 0x0060ee6e..0x0060eed8
* outstanding: Compare each configured strategy cooldown and separate objective cooldown behavior.

**M7-012 — Mood schema, decay graphs, action-result events, affectors, update and game output** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Mood.cs`
* effect: mood axes, event effects, decay or app-visible state differ
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: shipped mood_config.json plus libcozmoEngine.so
* evidence: re-analysis/obb/assets/cozmo_resources/config/engine/mood_config.json; StaticMoodData::ReadFromJson 0x0067ce6c; MoodManager::Update 0x0067b5d4; MoodManager::SendEmotionsToGame 0x0067b724..0x0067b7f8
* outstanding: Compare/build the full parser, nine-emotion update and broadcast path.

**M7-013 — Mood clamp, graph evaluation and decay-clock restart arithmetic** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Mood.cs`
* effect: emotion magnitude or decay differs at clamps, graph edges or clock resets
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so plus shipped mood graphs
* evidence: Emotion::Add 0x00679618; Emotion::Update 0x006795a4; old<=1e-5 raw-new multiply at 0x006795ee..0x006795fc; GraphEvaluator2d::EvaluateY 0x00804bd0; mood_config.json decayGraphs
* outstanding: Correct the prior old<=1e-5 wording and compare every arithmetic branch.

**M7-014 — Reaction-lock manager lifetime and concrete per-class lock tables** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/ReactiveBehavior.cs`
* effect: reactions interrupt one another when locked, remain disabled, or use the wrong per-class set
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: IBehavior::SmartDisableReactionsWithLock 0x005bce3c; BehaviorManager::DisableReactionsWithLock 0x005a27e8; BehaviorManager::RemoveDisableReactionsLock 0x005a3a48; 13 concrete 21-entry tables in M7 inventory Appendix F
* outstanding: Compare the manager lock bookkeeping, name suffix and all recovered class tables.

**M7-015 — Pick-up and off-treads reaction gating follows the engine state path** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: pick-up reactions fire from a stack-specific raw fallback instead of the engine state
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: Robot::CheckAndUpdateTreadsState 0x00511e00; BehaviorReactToPickup 0x00607750..0x00607d9c
* outstanding: Compare/build the complete classifier and ReactToPickup gates; do not retain the former equivalence label without a full source path.

**M7-016 — The idle face composes a stack of named persistent and transient layers** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: simultaneous blink, dart and turn layers overwrite rather than compose
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: ITrackLayerManager::ApplyLayersToFrame 0x0058e644; ProceduralFace::Combine 0x005846a8; KeepFaceAlive dart 0x0058d3e2 and blink 0x0058d4be
* outstanding: Compare the live named-layer stack and ordering with M5's compositor.

**M7-017 — The exact live-animation wire lifecycle used by idle behavior** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: idle keyframes open, frame, interleave or close differently on the wire
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: AnimationStreamer::UpdateLiveAnimation 0x0057d5f8; Update 0x0057ce5c; InitStream 0x0057b674; UpdateStream 0x0057c84c; SendStartOfAnimation 0x0057c400; SendBufferedMessages 0x0057bf60
* outstanding: Compare the M7 caller lifecycle against M5's exact stream; the former equivalence claim is not retained.

**M7-018 — Shipped behaviour configuration and BehaviorClass factory binding** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: a shipped behaviour ID instantiates the wrong class, animation list or executable behavior
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: shipped OBB configuration plus libcozmoEngine.so BehaviorContainer
* evidence: 178-config table in M7 inventory Appendix C; 79-class entry-point table in Appendix B; BehaviorContainer::CreateBehavior 0x0059c888; class 0x39 BehaviorWait case constructs base IBehavior and installs the BehaviorWait vtable
* outstanding: Compare all 178 IDs and 79 class bindings, including the five differently named native classes and BehaviorWait's base defaults.

**M7-019 — M7 concrete reaction-behaviour state machines** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: a reaction selects a different animation, action, state transition, lock or completion condition
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: reaction-class rows in M7 inventory Appendices E and F; corrected ReactToCliff mood-event sequence 0x00604d70..0x00604d84; ReactToImpact 0x006061f8..0x0060647c; ReactToOnCharger 0x00606c94..0x00606e86; ReactToPickup 0x00607750..0x00607d9c
* outstanding: Compare/build every M7-owned reaction row. Cross-layer derived-state and face/object inputs remain owned by M10/M14.

**M7-020 — Mood event production, repetition penalty and affector application** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Mood.cs`
* effect: completed actions or repeated events change the wrong emotions or amount
* rests on: the cited X3 and I-M7 extraction rows, checked directly against the shipped binary or shipped asset
* best authority: libcozmoEngine.so plus shipped mood_config.json
* evidence: MoodManager::TriggerEmotionEvent 0x0067b85c; MoodManager::AddToEmotion 0x0067bdca; MoodManager::HandleActionEnded 0x0067c770; EmotionAffector::ReadFromJson 0x00679910; MoodManager::SendEmotionsToGame 0x0067b724
* outstanding: Compare/build the action-result map, enable set, repetition clocks/graphs and each affector add.

### M8-framework — Behaviour framework and scoring

**M8-001 — The IBehavior lifecycle: constructor fields, Init, Update, Stop, StopActing, Resume, IsRunnable and IsRunnableScored** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IBehavior.cs`
* effect: the behaviour never starts, stops, resumes or reports runnable the way the engine does
* rests on: the original's IBehavior constructor and lifecycle functions, read at the addresses above
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: IBehavior::IBehavior 0x005bbc38 stores behaviourID at +0x3c, behaviourClass at +0x64, needsActionID at +0x68; zeroes +0x24/+0x28, the flat score at +0x100 and +0x104 (strd r8,r8,[r4,#0x100] 0x005bbd28); sets +0x108 = 0, +0x10c = 0x29 (invalid behaviour objective), and +0x110 = +0x111 = 1 (movw r0,#0x101 0x005bbd30, strh.w r0,[r4,#0x110] 0x005bbd34); constructs the repetition graph at +0xe8 and the running-penalty graph at +0xf4 (GraphEvaluator2d::GraphEvaluator2d 0x005bbd12/0x005bbd22); calls ReadFromJson 0x005bbd40 and ScoredConstructor 0x005bbe32.; IBehavior::Init 0x005bcb54 walks the robot's ActionList at robot+0x250. action+0x60 is the action's 32-bit tag; the guard sets its flag when a tag is > 0x2dc6c0 (movw r6,#0xc6c0 0x005bcbd0 / movt r6,#0x2d 0x005bcbda; cmp r2,r6 0x005bcbec; movhi r5,#1 0x005bcbf0). sTagCounter at 0x01051020 starts at 0x002dc6c1, so every engine-constructed action tag is >= 0x2dc6c1, while an action tagged through the game path (IActionRunner::SetTag 0x00540098, called from HandleActionEvents 0x00525ab0 and the QueueSingleAction/QueueCompoundAction handlers 0x00527a78/0x00527c16) carries a tag <= 0x2dc6c0. The warning IBehavior.Init.ActionsInQueue at 0x005bcc84 fires only when at least one engine-tagged action is present; its count is the main queue's (map key 0) current action plus queued actions (ldr r1,[r0,#0x14] 0x005bcd64, ldr r0,[r0,#0x20] 0x005bcd66, addne r0,#1 0x005bcd6c).; IBehavior::Init spark gate +0xd8: ldr r0,[r0,#0x44] 0x005bccac; if [r0+0x58] != 0x55 then +0xd8 = ! [r0+0x5c] else 0 (0x005bccb0..0x005bccc2). It then clears +0x98, writes halfword 0x0100 at +0xa0 (so +0xa1 = running = 1, +0xa0 = 0), stamps the running-penalty clock +0x34 = BaseStationTimer::GetCurrentTimeInSeconds(), clears the idle-animation flag +0xb0, and calls vtable+0x48 (IsRunnableInternal): non-zero clears +0xa1, otherwise +0x80++ (0x005bccbe..0x005bcd0c). If the unlock id +0x70 is not 0x55 and equals robot+0x44->0x58 it takes the SparkBehaviorDisables lock through SmartDisableReactionsWithLock and clears +0x114 (0x005bcd16..0x005bcd58).; IBehavior::Update 0x005bd074 calls vtable+0xc (UpdateInternal) unless byte +0xa0 is set and the current-action handle +0x84 is zero, in which case it returns 2 (ldrb.w r1,[r0,#0xa0] 0x005bd074; cbz r1 0x005bd078; ldr.w r1,[r0,#0x84] 0x005bd07a; cbz r1 0x005bd07e; movs r0,#2 0x005bd088).; IBehavior::Stop 0x005bd08c logs, stops any helper, clears +0xa1 (0x005bd10c), calls vtable+0x54 (StopInternal, 0x005bd110/0x005bd114), stamps the last-run clock +0x30 = BaseStationTimer::GetCurrentTimeInSeconds() (0x005bd11a/0x005bd11e), calls StopActing(0,0) (0x005bd126), then undoes the scope in this order: (a) remove disable-reactions locks while +0xac != 0 (0x005bd12c/0x005bd136), (b) remove the idle animation if +0xb0 (0x005bd148), (c) clear the motion profile if +0xc0 (0x005bd158), (d) walk the track-lock map at +0xb4 and MovementComponent::UnlockTracks(mask, name) each, then destroy the map (0x005bd16c/0x005bd174).; IBehavior::StopActing 0x005bd34c calls vtable+0x80(0) (blx r2 0x005bd360); when viaCallback is false and a helper is live it logs "Stopping behavior helper because action stopped without callback" and calls StopHelperWithoutCallback (0x005bd39c/0x005bd3d6); then, if +0x84 is set, cancels it in ActionList (0x005bd3f0) and clears +0x84 unless keepAction.; IBehavior::Resume 0x005bceac: for any trigger other than 0 (CliffDetected) or 0x14 (UnexpectedMovement) it takes the normal resume path (cmp r5,#0x14 0x005bcf16; cmpne r5,#0 0x005bcf1a; bne 0x5bcf7e 0x005bcf1c). For those two it reads +0x114, increments it and takes the normal path while the pre-increment value is < 1 (ldr.w r0,[r4,#0x114] 0x005bcf1e; cmp r0,#1 0x005bcf22; str.w r1,[r4,#0x114] 0x005bcf28; blt 0x5bcf7e 0x005bcf2c); otherwise it sets +0x118 = now + 15.0, builds "TooManyResumesCliffOrMovement" and calls MoodManager::TriggerEmotionEvent(name, now), returning 1 (0x005bcf36..0x005bcf7a). The normal path sets +0xa2 = 1, stamps +0x34 = now, calls vtable+0x4c (ResumeInternal), clears +0xa2; on a zero result sets +0xa1 = 1 and, when +0x70 != 0x55 and equals robot+0x44->0x58, takes the SparkBehaviorDisables lock; on a non-zero result clears +0xa1 (0x005bcf80..0x005bcfde).; IBehavior::IsRunnable 0x005bd750 calls IsRunnableBase(this->robot); a result other than 1 returns 0, otherwise it tail-calls vtable+0x50 with the caller's robot (0x005bd756..0x005bd770). IBehavior::IsRunnableScored 0x005bda28 returns 1 when the current time is >= the suppression stamp +0x118, else 0 (0x005bda38/0x005bda3e/0x005bda46/0x005bda48). IBehavior::IsRunnableBase 0x005bd778: +0xa1 (running) returns true; otherwise the AI process (robot+0x264), the robot state byte +0x74 against 3, the unlock id +0x70 (0x55 bypasses) through ProgressionUnlockComponent::IsUnlocked(robot+0x448, id, true) (0x005bd810), and the float timers at +0x78.; ReactionTrigger ordinals (table 0x10337c0; EnumToString 0x0077065c): 0 = CliffDetected, 0x14 = UnexpectedMovement, 0x15 = Count, 0x16 = NoneTrigger (ReactionTriggerFromString 0x00770674 confirms the same mapping).
* outstanding: To build: the lifecycle as cited. The reaction-lock wrappers it calls are M8-011; the reaction strategies are M10-003/M10-004. Init's warning fires only for engine-tagged actions and counts the main queue (gap pass 2).

**M8-002 — The repetition penalty graph and its suppression window** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs`
* effect: a behaviour's score is penalised by the wrong curve, or not at all
* rests on: the shipped config plus the code that reads and consumes it
* best authority: libcozmoEngine.so 3.4.0-1204 and the shipped mood_config.json
* evidence: config/engine/mood_config.json:88-99 ships defaultRepetitionPenalty with the nodes (0.0, 0.0) and (30.0, 1.0).; IBehavior::ReadFromScoredJson 0x005bc488 reads repetitionPenalty into the GraphEvaluator2d at +0xe8; a missing or unreadable graph warns (IScoredBehavior.BadRepetitionPenalty) and leaves it empty, and an empty graph gets the single node AddNode(0, 1, true) at 0x005bc566 (0x005bc678).; IBehavior::EvaluateRepetitionPenalty 0x005beee6 returns 1.0 when the last-run stamp +0x30 <= 0 (vldr s0,[r4,#0x30] 0x005beeea; movle.w r0,#0x3f800000 0x005beef8), otherwise evaluates the graph at +0xe8 at now - +0x30 (add.w r0,r4,#0xe8 0x005bef0e; b.w 0x8cbf9c 0x005bef1e).; The +0x30 stamp is written by IBehavior::Stop 0x005bd11a/0x005bd11e as BaseStationTimer::GetCurrentTimeInSeconds().; The +0x108 suppression threshold: StopWithoutImmediateRepetitionPenalty 0x005beea0 sets +0x108 = now + 1.0 (vmov.f32 s0,#1.0 0x005beeb0; vadd.f32 0x005beeb8; vstr s0,[r4,#0x108] 0x005beebc); EvaluateScore applies the penalty only when now >= +0x108 (vldr s0,[r4,#0x108] 0x005befe2; vcmpe.f32 0x005befea; bpl 0x005beff2).
* outstanding: To build: the config graph, the evaluator and the +0x108 suppression window. The scored selection that consumes the penalty is M8-003.

**M8-003 — Scored selection: emotion scorers or the flat score, times the repetition and running penalties** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`
* effect: behaviours are chosen by the wrong score
* rests on: ReadFromScoredJson, EvaluateScore, EvaluateScoreInternal, EvaluateRunningPenalty and MoodScorer, read
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: IBehavior::ReadFromScoredJson 0x005bc488 reads five keys, not four: "emotionScorers" into a MoodScorer at +0xdc, "flatScore" into the float at +0x100 (0x005bc4ca/0x005bc4e0), "repetitionPenalty" into the graph at +0xe8 (0x005bc4ee), "considerThisHasRunForBehaviorObjective" into a behaviour objective at +0x10c through BehaviorObjectiveFromString (call 0x005bc5a0), and "runningPenalty" into the graph at +0xf4 (string 0x005bc602; add.w r6,r4,#0xf4 0x005bc5f8; AddNode(0,1,true) 0x005bc678).; IBehavior::EvaluateScoreInternal 0x005beec2: if the MoodScorer vector at +0xdc/+0xe0 is non-empty it tail-calls MoodScorer::EvaluateEmotionScore (0x8cbf8c, with robot+0x440); otherwise it returns the flat score at +0x100 (vldr s0,[r0,#0x24] after the pre-index 0x005beed4). The two are alternatives, not addends.; MoodScorer::EvaluateEmotionScore 0x0067c9b8 walks the scorers, evaluates each one's graph at its emotion's value or at the value minus the value sixty ticks ago when trackDelta is set (Emotion::GetHistoryValueTicksAgo(60) 0x0067c9f4), and returns the mean (0x0067ca6e..0x0067ca72); a result within 1e-05 of zero ends the walk and the score is zero (0x0067ca50).; IBehavior::EvaluateScore 0x005bef60: while running it uses EvaluateScoreInternal plus the float at +0x104 (vldr s2,[r4,#0x104] 0x005bef80; vadd.f32 s16,s0,s2 0x005bef88) and multiplies by EvaluateRunningPenalty when +0x111 is set (ldrb.w r0,[r4,#0x111] 0x005bef84; cbz 0x005bef8c; blx EvaluateRunningPenalty 0x005bef90; vmul.f32 0x005bef98). Otherwise it scores only a behaviour that IsRunnableBase and passes the vtable+0x50 test, applies the repetition penalty only when +0x110 is set and now >= +0x108 (ldrb.w r0,[r4,#0x110] 0x005befd4; cbz 0x005befd8).; IBehavior::EvaluateRunningPenalty 0x005bef22: returns 1.0 when +0x34 <= 0, otherwise evaluates the graph at +0xf4 at now - +0x34 (vldr s0,[r4,#0x34] 0x005bef26; add.w r0,r4,#0xf4 0x005bef4a; b.w 0x8cbf9c 0x005bef5a).; The four constructor fields: +0x104 is the running-score bonus (IncreaseScoreWhileActing 0x005bf02c adds to it only while the current-action handle +0x84 is non-zero; ScoredActingStateChanged 0x005bf046 clears it); +0x108 is the repetition-penalty suppression threshold; +0x110 enables the repetition penalty; +0x111 enables the running penalty (both set to 1 by the halfword 0x0101 at 0x005bbd34, with no other writer).; EmotionScorer::ReadFromJson 0x0067aabc names emotionType, scoreGraph and trackDelta. No shipped behaviour or activity config carries an emotionScorers block, so every scored behaviour in the app is scored by its flatScore alone.
* outstanding: To build: the fifth key runningPenalty, EvaluateRunningPenalty, and the +0x104/+0x108/+0x110/+0x111 semantics, on top of the scorer and flat-score rule. The chooser that consumes the score is M8-013.

**M8-005 — PlayAnim plays every trigger it lists, in order, num_loops times** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: a PlayAnim behaviour plays the wrong set of triggers
* rests on: BehaviorPlayAnimSequence, read; the bodies were not re-read in the M8 passes
* best authority: BehaviorPlayAnimSequence::StartPlayingAnimations 0x005C0158 and BehaviorPlayAnimSequence::StartSequenceLoop 0x005C0294, read
* evidence: StartPlayingAnimations 0x005C0158 measures the trigger vector at +0x11C: a byte length of exactly 4 - one trigger - is special-cased and played as a single TriggerLiftSafeAnimationAction with the loop count from +0x128 (0x005C0190). Anything else falls through to StartSequenceLoop.; StartSequenceLoop 0x005C0294 runs while the counter at +0x12C is below the loop count at +0x128: it makes a CompoundActionSequential and walks the whole trigger vector, adding one TriggerLiftSafeAnimationAction per trigger (0x005C02CA..0x005C030E), then runs the compound and increments the counter. So every trigger plays, in the config's order, and the list repeats.; The loop count is the config's num_loops, read with a default of 1: Json::Value::Value(1) then Json::Value::get("num_loops", default) at 0x005C001C..0x005C0036.; Of the fifteen shipped PlayAnim configs, one lists more than one trigger - NothingToDo_BoredAnim - and none sets num_loops. This stack played the first trigger that resolved and stopped; it now plays the list.
* outstanding: To build: the ordered multi-trigger sequence and the num_loops repeat. The citations resolve to the named symbols; the detailed claims were not re-read in this pass.

**M8-006 — The wants-to-run strategy types, and what each one tests** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: a behaviour runs when the engine would not run it
* rests on: the factory and the strategy classes, read; the bodies were not re-read in the M8 passes
* best authority: WantsToRunStrategyFactory::CreateWantsToRunStrategy 0x00614710, StrategyInNeedsBracket::WantsToRunInternal 0x006141A0 and StrategyExpressNeedsTransition::WantsToRunInternal 0x006136D8, read
* evidence: The nine WantsToRunStrategyType values are the table EnumToString 0x007716A4 indexes at 0x01033820: Invalid, AlwaysRun, ExpressNeedsTransition, Generic, InNeedsBracket, ObstacleDetected, PlacedOnCharger, RobotPlacedOnSlope, RobotShaken. CreateWantsToRunStrategy 0x00614710 dispatches on that value through a tbb and constructs the matching Strategy class.; StrategyInNeedsBracket::WantsToRunInternal 0x006141A0 is one call: NeedsManager::GetCurNeedsStateMutable() then NeedsState::IsNeedAtBracket(need, bracket), the pair held at strategy+0x18 and +0x1C from the config's need and needBracket.; StrategyExpressNeedsTransition::WantsToRunInternal 0x006136D8 asks IsNeedAtBracket(need, Critical) - the literal 3 at 0x006136EC, Critical being index 3 of the NeedBracketId table at 0x01033EE0 (Full, Normal, Warning, Critical) - and then returns false when the need equals the one the AI component is already expressing (0x006136FC). Both are implemented here now.; Of the shipped configs only reactToObstacle.json gives a behaviour a strategy, and it is ObstacleDetected, whose flag nothing in this build ever sets. The needs strategies appear on activity configs (M7/M15), and PlacedOnCharger, RobotPlacedOnSlope and RobotShaken appear in reactionTrigger_behavior_map.json (M10).
* outstanding: To build: the factory dispatch and the per-type tests. The citations resolve to the named symbols; the detailed claims were not re-read in this pass.

**M8-007 — An action's track mask is a lock, taken while it runs** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/SteppedBehavior.cs`
* effect: a locked track is driven by two actions at once, or an action is dropped instead of retried
* rests on: IActionRunner::Update and MovementComponent::LockTracks, read; the bodies were not re-read in the M8 passes
* best authority: IActionRunner::Update 0x00540370, MovementComponent::LockTracks 0x00640098 and MovementComponent::UnlockTracks 0x0063FE5C, read
* evidence: Every action carries a track mask at +0x54. IActionRunner::Update 0x00540370 reads it and asks MovementComponent::AreAnyTracksLocked(mask) at 0x00540440: while any of those tracks is held it warns "Action %s [%d] not running because required tracks are locked" (0x005404A8) and does not run the action - it stays queued and is tried again on the next tick. Otherwise it locks them through the helper at 0x004F0F4C, which calls MovementComponent::LockTracks(mask, tag, name), and then runs the action.; LockTracks 0x00640098 walks the bits of the mask and inserts one LockInfo - the owner's name and reason - per track into a multiset at component+0x30; UnlockTracks 0x0063FE5C removes them. So a track is held by a set of owners and is free when the last one lets go.; The mask is therefore mutual exclusion, not muting: the animation plays its tracks and nothing else may drive them while it does. This stack claims the clip's tracks on the behaviour's scope and the scheduler refuses a play whose tracks are owned; the refusal waits and retries, as IActionRunner does, instead of treating the play as done.
* outstanding: To build: the per-action track mask lock and the retry-on-locked behaviour. The citations resolve to the named symbols; the detailed claims were not re-read in this pass.

**M8-011 — The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IBehavior.cs`
* effect: a behaviour leaks or double-releases a track lock, idle animation, motion profile, light pattern, helper or reaction lock
* rests on: the eleven Smart* functions, read at the addresses above
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: SmartPushIdleAnimation 0x005be41c: if the idle-animation flag +0xb0 is already set it fails through sVerifyFailedReturnFalse (0x005be448 b.w 0x8cbe5c); otherwise AnimationStreamer::PushIdleAnimation(trigger, name) and +0xb0 = 1 (0x005be464..0x005be478).; SmartRemoveIdleAnimation 0x005bd4c8: when +0xb0 is clear it raises VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set through sVerifyFailedReturnFalse (cbz r0,#0x5bd508 0x005bd4d4; string 0x005bd51e; b.w 0x8cbe5c 0x005bd526); otherwise AnimationStreamer::RemoveIdleAnimation(name) and +0xb0 = 0 (0x005bd4ec/0x005bd500).; SmartSetMotionProfile 0x005be518 verifies +0xc0 == 0 (sVerifyFailedReturnFalse 0x005be52a), calls PathComponent::SetCustomMotionProfile (0x005be534) and sets +0xc0 = 1. SmartClearMotionProfile 0x005bd584 verifies +0xc0 != 0, calls PathComponent::ClearCustomMotionProfile (0x005bd59c) and clears +0xc0.; SmartLockTracks 0x005be5bc emplaces (name -> mask) into the map at +0xb4; on a new key it calls MovementComponent::LockTracks(mask, name, reason) and returns 1; on an existing key it warns "Attempted to lock tracks with key named %s but key already exists" and returns 0 (0x005be600/0x005be624/0x005be636). SmartUnLockTracks 0x005be6e0 looks the name up, calls MovementComponent::UnlockTracks(mask, name), erases the entry and returns 1, or warns and returns 0 (0x005be6ee..0x005be716).; SmartSetCustomLightPattern 0x005be7f0: if the ObjectID is already in the vector at +0xcc it logs on the "Unnamed" channel and returns 0; otherwise CubeLightComponent::PlayLightAnim(objectID, trigger, callback, true, lights, timeout) and appends the ObjectID to the vector at +0xd0, returning 1 (0x005be806..0x005be8cc).; SmartRemoveCustomLightPattern 0x005be9b0 searches the vector at +0xcc for the element whose +4 equals objectID+4; not found logs "No custom light pattern is set for object %d" and returns 0 (0x005bea4e/0x005bea74); found calls CubeLightComponent::StopLightAnimAndResumePrevious(trigger, objectID) for each trigger and erases the element, returning 1 (0x005be9e8/0x005bea34).; SmartDelegateToHelper 0x005beb10 logs on the "Behaviors" channel, warns and calls StopHelperWithoutCallback() when the current helper +0xc8 is already live (0x005beba4/0x005bebe8), then calls BehaviorHelperComponent::DelegateToHelper on [robot+0x264]+0x10 (0x005bec2c); on success it stores the helper as a weak ref at +0xc4/+0xc8 through __add_weak at 0x005bec6e, otherwise logs "SmartDelegateToHelper.Failed" (0x005bec8c/0x005becb2).; SmartDisableReactionsWithLock 0x005bce3c appends "_behaviorLock" to the name, calls BehaviorManager::DisableReactionsWithLock(manager, name+"_behaviorLock", table, true) (0x005bce62) and inserts the original name into the per-behaviour set at +0xa4 (0x005bce7e). SmartRemoveDisableReactionsLock 0x005bd470 appends the same suffix, calls BehaviorManager::RemoveDisableReactionsLock (0x005bd48c) and erases the name from +0xa4 (0x005bd4a4). The manager side is M7-014.
* outstanding: To build: the helpers as cited. The reaction-lock manager side is M7-014; the track lock itself is M8-007.

**M8-012 — BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs`
* effect: the manager ticks, switches or finishes behaviours in the wrong order, or fires a reaction when it should not
* rests on: BehaviorManager::Update and its switch/finish/resume helpers, read at the addresses above
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: BehaviorManager::Update 0x005a2f70 returns immediately when the manager's init byte at +0 is zero, logging "BehaviorManager.Update.NotInitialized" (0x005a2f70/0x005a2f72/0x005a2fea).; It ticks the current activity first: GetCurrentActivity then the activity's virtual Update at vtable+0x20 (0x005a2f78/0x005a2f80/0x005a2f84).; If robot+0x355 is non-zero, or manager+0x20 == 1, it calls EnsureRequestGameIsClear (0x005a2f8e/0x005a2f94/0x005a2f9e).; If manager+0x38 is set it calls SelectUIRequestGameBehavior, clears the flag, and when the previous current behaviour's class is 0x2e (cmp r7,#0x2e at 0x005a2fe0) sets byte +0x220 on the object at manager+0x30 (strbeq.w r1,[r0,#0x220] 0x005a305c).; It calls CheckReactionTriggerStrategies 0x005a3060; a non-zero result suppresses the scored choice and the UI-request switch for this tick (cbnz r5 0x005a3068).; The reaction list is evaluated only when the robot's action list robot+0x250 is empty: the manager latch at +0x4c is ORed with !IsEmpty and CheckReactionTriggerStrategies returns false when the list is empty (0x005a3558..0x005a356c).; If no reaction fired, nothing occupies manager+0x30, and the current behaviour's class is 0x16, it calls ChooseNextScoredBehaviorAndSwitch (ldrb r0,[r0,#0x10] 0x005a3070; cmp r0,#0x16 0x005a3072; blxeq 0x005a3078).; If no reaction fired and manager+0x30 holds a behaviour different from the current one, it calls SwitchToUIGameRequestBehavior (0x005a307e/0x005a3082/0x005a309a).; It ticks the current behaviour with IBehavior::Update (0x005a30bc). Return 0 logs "BehaviorManager.Update.FailedUpdate" (0x005a3148) and return 2 logs a debug line; both then call FinishCurrentBehavior(behavior, class != 0x16) (0x005a3128). Return 1 keeps it (cmp r0,#0 0x005a30c0; cmp r0,#2 0x005a30c4).; SwitchToBehaviorBase 0x005a1e6a stops the current behaviour (StopAndNullifyCurrentBehavior), calls IBehavior::IsRunnable(robot) and logs "BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable" on false, then IBehavior::Init (0x005a1e94); an Init failure logs "BehaviorManager.SetCurrentBehavior.InitFailed" (0x005a1eae), clears the behaviour, and still sets the running/resume info (SetRunningAndResumeInfo 0x005a1f12) and sends the DAS transition (SendDasTransitionMessage 0x005a1f1c).; FinishCurrentBehavior 0x005a38ca: immediate == 1 tail-branches to 0x8cbc9c; otherwise, if manager+0x30 equals the finishing behaviour, it calls EnsureRequestGameIsClear (0x005a38e6) and switches to a default class-0x16 BehaviourRunningAndResumeInfo (movs r0,#0x16 0x005a38f4; SwitchToBehaviorBase 0x005a38fe).; SwitchToReactionTrigger 0x005a25e4 builds a resume info and calls the strategy's ShouldTriggerBehavior (vtable+8) (0x005a262a); only when it returns 1 does it switch (cmp r0,#1 0x005a262c; SwitchToBehaviorBase 0x005a26da).; TryToResumeBehavior 0x005a2b48 bails unless the stored resume head angle differs from a constant and the robot's action list is empty (vldr s0,[pc] 0x005a2b48; ActionList::IsEmpty 0x005a2b60), logging "Resuming behavior and don't have an action, so setting head angle %f, lift height %f" (0x005a2b72).
* outstanding: To build: the tick order and the switching paths as cited. The reaction strategies themselves are M10-003/M10-004; the activity tick is M8-013/M7.

**M8-013 — The behaviour choosers and the activity selection dispatch** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`
* effect: the activity picks a different behaviour than the engine would
* rests on: the three choosers and the activity dispatch, read at the addresses above
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060a44a walks its behaviour map, calls EvaluateScore(robot) on each (0x0060a44a), skips scores <= 0 (vcmpe s0,#0 0x0060a452), adds a running-duration graph bonus for a running behaviour (GraphEvaluator2d::EvaluateY 0x0060a474) and a RandomGenerator::RandDbl tie-break for a non-running one (0x0060a4a8), and keeps the maximum. It logs "behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f" (string 0x0060a60a; sChanneledInfoF 0x0060a61c) and warns "Looks like more than one behavior returned IsRunning(). One of them is '%s'" (0x0060a550).; SelectionBSRunnableChooser::GetDesiredActiveBehavior 0x0060ad64 chooses between the behaviour at +0x2c and the one at +0x34, gated by a countdown at +0x3c and a latch at +0x40; a running behaviour short-circuits the countdown (movs r6,#1 0x0060ad84; subs r1,#1 0x0060adbe; str r1,[r5,#0x3c] 0x0060adc0; strb.w r0,[r5,#0x40] 0x0060adc8).; StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060b23e walks its behaviour vector at +0x1c..+0x20; a behaviour with +0xa1 (running) non-zero is returned immediately, otherwise the first behaviour for which IBehavior::IsRunnable(robot) == 1 is returned; if none, it returns a null shared_ptr (0x0060b242..0x0060b266). No scoring and no tie-break.; IActivity::GetDesiredActiveBehavior 0x005b387c returns the activity's current behaviour when it equals the caller's; otherwise it calls the activity's GetDesiredActiveBehaviorInternal at vtable+0x30 (0x005b38c2) and, when the activity holds an interlude behaviour at +0x28 and the result differs, calls the interlude's method at vtable+0x28 (0x005b38f6), logging "Activity %s is inserting interlude %s between behaviors %s and %s" (0x005b399c).; IActivity::GetDesiredActiveBehaviorInternal 0x005b3a6c reads the chooser at +0x24; a null chooser warns "VERIFY(%s): ChooseNextBehaviorInternal called without behavior chooser overwritten" and returns a null behaviour; otherwise it calls the chooser's vtable+0x28 (0x005b3a70/0x005b3aa2/0x005b3a94).; The concrete activities implement GetDesiredActiveBehaviorInternal: ActivityFreeplay 0x005ae29c, ActivitySocialize 0x005b023c, ActivitySparked 0x005b1d18, ActivityStrictPriority 0x005b267c, ActivityBuildPyramid 0x005a8854, ActivityFeeding 0x005abdd8. Their bodies belong to M7/M15.
* outstanding: To build: the chooser algorithms and the activity dispatch. The concrete activity bodies are M7/M15; the score itself is M8-003.

**M8-014 — AIWhiteboard: init handlers, the no-op update and AddBeacon** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/AIWhiteboard.cs`
* effect: the whiteboard misses events or renders the wrong beacons
* rests on: AIWhiteboard::Init, Update and AddBeacon, read at the addresses above
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: AIWhiteboard::Init 0x0056a394 registers three external-interface handlers when the robot has an external interface (HasExternalInterface 0x0056a39c; three bl at 0x0056a3b8/0x0056a3be/0x0056a3c4 to 0x56a444/0x56a504/0x56a5c4); otherwise it warns "Initialized whiteboard with no external interface. Will miss events." (0x0056a3d2).; AIWhiteboard::Update 0x0056a684 is a no-op (bx lr).; AIWhiteboard::AddBeacon(pose, radius) 0x0056c39c appends an AIBeacon (Pose3d plus a float at +0xc) to the vector at +0x60 (vstr s16,[r0,#0xc] 0x0056c3c0; str r0,[r4,#0x64] 0x0056c3ce) and calls UpdateBeaconRender 0x0056c3de.
* outstanding: To build: the three handler registrations, the no-op update and the beacon vector/render. The handlers' bodies are interfaces to M11/M12.

### M9-wwise-music — Wwise music, the MIDI sampler and singing

**M9-001 — BehaviorSinging configuration, switch mapping and default tempo trigger** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/SingingBehavior.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-001; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so
* evidence: 0x005EE8DC..0x005EEA0F; X4 S1-S4
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-002 — Singing initialization posts the switch, locks reactions and starts the three-animation sequence in order** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/SingingBehavior.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-002; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so
* evidence: 0x005EEB30..0x005EEE9F; X4 S5-S6 and S17-S18
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-003 — Cube running means, vibrato smoothing/posting, duration log, update result and stop cleanup** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/SingingBehavior.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-003; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so
* evidence: 0x005EF0C8..0x005EF30F; 0x005EF490..0x005EF4DF; X4 S7 and S11-S16 and S19
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-004 — Music switch containers, decision trees, meters and MIDI target** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseMusic.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-004; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Wwise banks and approved M6 inventory
* evidence: re-analysis/inventory/M6-wwise-bank.md; Cozmo.bnk objects 914766641, 139286641, 602865028 and MIDI target 110896138
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-005 — Singing MIDI sources use SMF division 9600 and the effective meter tempo** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseMidi.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-005; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Wwise banks
* evidence: Cozmo.bnk MIDI source plugin 0x00100001 and division 0x2580; re-analysis/research/20260928-X4-M9-wwise-music-extraction.md
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-006 — HIRC LFO and Envelope payloads and their runtime classes** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseModulator.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-006; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Wwise banks and libcozmoEngine.so
* evidence: 0x009D7B6C..0x009D7C97; vtable 0x0103B1E8 and 0x0103B218; re-analysis/research/20260928-I-M9-gap1-extraction.md
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-007 — The note-off envelope binding applies Wwise scaling 2 after its curve** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSongRenderer.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-007; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so and shipped Wwise bank
* evidence: 0x00A14F88..0x00A15038; re-analysis/inventory/M6-wwise-bank.md; Cozmo.bnk object 381606890 and binding 462443456
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-008 — The vibrato LFO binding depth is driven by the posted cube-shake parameter** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSongRenderer.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-008; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Wwise bank and libcozmoEngine.so
* evidence: 0x009D671C..0x009D7727; 0x005EF184..0x005EF18C; Cozmo.bnk objects 528935089 and 110896138
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-009 — Modulator bindings evaluate their curves and accumulate onto the named property** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSongRenderer.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-009; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so
* evidence: 0x00A6E848..0x00A6F133; X4 S27
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-010 — Singing note-on and note-off layers determine held-note lifetime** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSongRenderer.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-010; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Wwise banks and media
* evidence: re-analysis/inventory/M6-wwise-bank.md; Cozmo.bnk singing sampler note-on and note-off layers
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-011 — Singing renders through the shipped Robot_Bus_1 EQ, limiter and Hijack chain** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBusChain.cs`
* effect: a song is quieter or louder, and differently shaped, than the app made it
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-011; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: approved M6 inventory and shipped Init.bnk
* evidence: re-analysis/inventory/M6-wwise-bank.md; 0x00AA257C; 0x00AA18F4
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-012 — MIDI note tracking is disabled on every shipped node** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHierarchy.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-012; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Wwise banks and libcozmoEngine.so
* evidence: re-analysis/inventory/M6-wwise-bank.md; M6 gapE 3.3; Cozmo.bnk node property 45 absence
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-015 — Each play draws container selections afresh using Wwise's own LCG** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAudioSource.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-015; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so
* evidence: 0x0098A6D4..0x0098A7B8; re-analysis/inventory/M6-wwise-bank.md
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-017 — Cube acceleration stream, high-pass filter and shake hysteresis drive singing vibrato** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CubeAccel.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-017; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so
* evidence: 0x005EECB0..0x005EED6F; 0x00635474..0x006355BF; 0x00636598..0x0063682F
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-018 — The shipped singing Stop event ends the three tempo containers** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAudioSource.cs`
* effect: the song keeps playing past the end of the tempo clip, or is cut short
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-018; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Wwise banks
* evidence: Cozmo.bnk Stop__Robot_VO__Cozmo_Singing_Stop and tempo Play events; re-analysis/research/20260928-X4-M9-wwise-music-extraction.md
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-019 — No shipped blend container has a blend track** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHierarchy.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-019; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: all six shipped Wwise banks
* evidence: re-analysis/inventory/M6-wwise-bank.md; Cozmo.bnk and SFX.bnk blend containers
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-020 — A music clip uses BeginTrim and length and releases a held note at clip end** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSongRenderer.cs`
* effect: the wrong part of a song is sung, or its last note is cut or left hanging
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-020; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Wwise banks
* evidence: re-analysis/inventory/M6-wwise-bank.md; re-analysis/research/20260928-X4-M9-wwise-music-extraction.md
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-021 — A multi-action event dispatches every Play action** (not on the live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseMusic.cs`
* effect: one or more sounds targeted by the event are omitted from playback
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-021; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Wwise banks
* evidence: re-analysis/inventory/M6-wwise-bank.md; Cozmo.bnk singing events and action lists
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-022 — Wwise container selection uses the recovered eligibility, blocked-list, random and sequence algorithms** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSongRenderer.cs`
* effect: the wrong recording, or too many recordings, sound for a note
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-022; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so and approved M6 inventory
* evidence: 0x0098A6D4..0x0098A7B8; 0x00A08A44; 0x00A0A524; re-analysis/inventory/M6-wwise-bank.md
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-026 — The shipped parametric EQ and peak-limiter arithmetic** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBusChain.cs`
* effect: the EQ curves and the limiter's knee differ in detail from the product's, at the same settings
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-026; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so and approved M6 inventory
* evidence: 0x00AA257C; 0x00AA2A84; 0x00AA18F4; 0x00AA0EB4; re-analysis/inventory/M6-wwise-bank.md
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-027 — Robot_Bus_Eq_HiLowPass behavior at 14298 Hz and the robot output rate** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBusChain.cs`
* effect: none: the band cannot act at 22320 Hz and is reported rather than applied at a frequency it cannot have
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-027; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: shipped Init.bnk and libcozmoEngine.so
* evidence: re-analysis/inventory/M6-wwise-bank.md; 0x00AA25E0; Init.bnk Robot_Bus_Eq_HiLowPass settings
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

**M9-028 — RobotAudioClient dispatches singing parameters and switches to game object 7 on-robot or 6 off-robot** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationAudio.cs`
* effect: singing behavior, timing, selection or rendered audio differs from the official app
* rests on: Inventory-approved source path in re-analysis/inventory/M9-wwise-music.md for M9-028; implementation has not yet been compared except where this record is an explicit policy or unresolved source gap.
* best authority: libcozmoEngine.so
* evidence: 0x00599F60..0x00599FBF; X4 S20-S21
* outstanding: Compare and, where different, build the complete production path described by this record against the approved inventory.

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

### M11-vision — Markers, camera geometry and BlockWorld

**M11-004 — BlockWorld connected-object rule, moving and rotating gates, position-update thresholds** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs`
* effect: an unconnected cube is dropped where the engine keeps it, and the motion/rotation gates differ
* rests on: the stack's BlockWorld.cs connected-object rule and gates are to be compared against the instructions above
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: position-update thresholds: 45 deg 0x3F490FDB to [this+0x2c] (ctor 0x00612182), 80 mm 0x42A00000 to [this+0x38] (0x006121A2), 600000 ms 0x000927C0 to [this+0x34] (0x00612198); used at 0x006124C6..0x006124F2 and 0x00612592..0x006125A6; connected-object rule: AddAndUpdateObjects 0x00620AD4 warns and records a 10 s cooldown when the connected counterpart is absent (0x00620E70..0x00620EDA) and continues to 0x00620EDE; it does not drop the object; moving gate: CheckForUnobservedObjects 0x00621C6C skips when MovementComponent::WasMoving(timestamp) != 0 (0x00621C88..0x00621C94); WasMoving is HistRobotState+0x58 & 1 (IS_MOVING), lambda 0x00642672; rotating gate: WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0) 0x00621C9A..0x00621CAE; the ImuDataHistory is VisionComponent+0xb0 (0x656286/0x6562B4/0x6563AC/0x6563D8) filled by HandleImageImuData 0x535C20 -> ImuDataHistory::AddImuData 0x538B24 (ImuData: timestamp +0, rateX +4, rateY +8, rateZ +0xC, u8 +0x10); head uses rateY, body rateZ, |rate| > threshold, a missing IMU bracket returns 1; object match: ObjectPoseConfirmer::FindObjectMatchForObservation 0x5063CC; tolerances from the observed object's virtuals vptr+0x30 = 0.8*extent (thunk 0x4E025C, 0.8 at 0x4E028C) and vptr+0x34 = pi/4 (thunk 0x4E0290); primary FindLocatedClosestMatchingObjectHelper 0x61FA68 predicate 0x6281DA (ObjectType match + Pose3d::IsSameAs, narrowing the captured tolerance to abs(delta), closest wins); fallback over the confirmer's list with ObservableObject::IsSameAs 0x8769A8 (last match). FindLocatedObjectHelper 0x61EB78's returned object is a RECOVERABLE_GAP
* outstanding: the engine's connected-object check looks the counterpart up by ObjectID (GetConnectedActiveObjectByIdHelper 0x00620E78); the stack matches by type and disambiguates by pose (M11-013 policy); the object returned by FindLocatedObjectHelper 0x61EB78 (and its tie-break) is a RECOVERABLE_GAP

**M11-007 — Two misses before an object pose is forgotten, and two sightings before one is confirmed** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs`
* effect: an object pose is forgotten or confirmed one frame too early or late
* rests on: the stack BlockWorld.cs counters are to be compared against the instructions above
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: ObjectPoseConfirmer::MarkObjectUnobserved 0x00506FBC reads the miss count at +0x28 and writes count+1 zeroing +0x24 (strd r2,r1,[r0,#0x24] at 0x00506FE0); the forgetting branch is at 0x00506FDE; the confirming side at 0x00506A04 mirrors it; the two counters reset each other, so the misses have to be consecutive; this stack already used two; what was missing was the counter it rests on; confirming side: AddVisualObservation 0x50684C starts a new entry at count 1; a matching second sighting increments to 2 and calls UpdatePoseInInstance (0x506A04..0x506A26); a mismatching sighting resets the count to 1 (0x506A46..0x506A4E); IsReferencePoseConfirmed is count > 1 (0x506340); IsObjectConfirmedAtObservedPose requires count >= 2 and a matching pose (0x50634C..0x5063B8)
* outstanding: the PoseState mapping is not settled: what the caller sets when AddVisualObservation returns false; the stack still sets Known on the first sighting while tracking confirmation separately

**M11-017 — The memory map's vision-derived content: the overhead edges** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/OverheadEdges.cs`
* effect: none known
* rests on: the detector, the chains and the four MapComponent entry points that turn them into map content are the engine's, with every constant read from it
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: The detector. GroundPlaneROI is the trapezoid its four statics describe, 40 to 190 mm ahead and 40 to 150 mm wide (0x00C48F60, GetGroundQuad 0x004F7774). Detect projects it, filters the bounding rectangle with the seven-by-five kernel at 0x00C8E020 (identical for the colour and grey paths), masks everything outside the quad away (fillConvexPoly of the quad's corners 0, 2, 3, 1 then SetMaskTo), transposes and walks each column from the bottom up for the first response past the threshold of 50 that VisionSystem constructs it with (0x006B0120). A column with none reports the far end clear, but only between the x of the quad's two far corners (0x006AD23E). Image points become ground points through the homography, refused when its third component is not positive (0x006AE0A8). The lift is a gate rather than a mask: unless the first of two points on it projects below the ROI rectangle, or the second above it, the frame is abandoned (0x006AC4A2..0x006ACDC8). Points join a chain while they are of the same kind and within 5 mm (0x006AE1B0).; The map side. AddVisionOverheadEdges puts each point in world coordinates through the robot's pose at the frame's timestamp, splits the ray at the ROI's near edge and asks HasCollisionRayWithTypes about each half with the two masks at 0x00C8768B and 0x00C876A1, and accumulates runs that hold their direction to within forty degrees (0.766 at 0x0067F980). A run longer than the noise length (the literal 6.00001 at 0x0067FA14) becomes the triangle between the robot and its ends as ClearOfObstacle, or a line to its midpoint when the run is under fifteen millimetres (225 as a squared length at 0x0067FF5C); a run from a border chain also goes in as a two-point InterestingEdge (the type byte 9 at 0x006802AC). Then the border pass, QuadTreeProcessor::FillBorder 0x00689FAC through RefreshBorderCombination: an interesting edge touching one of the masked types - the obstacles and NotInterestingEdge, the table at 0x00C87675 - is written off as NotInterestingEdge.; The two entry points the visit behaviour uses are in as well: FlagQuadAsNotInterestingEdges 0x0067E6B0 inserts the quad as type 10 with the last image's timestamp, and FlagGroundPlaneROIInterestingEdgesAsUncertain 0x0067E50C transforms the content inside the ROI with a lambda (0x00680B54) that turns type 9 into type 0 and leaves the rest alone.; Two differences, neither observable in what the map answers: the engine carries its regions in a quad tree subdivided to ten millimetres (GetContentPrecisionMM 0x00685000) and this keeps the polygons, which is the same difference M14-007 already records; and the filter response is computed here in double over one channel rather than in 16-bit over three, which moves no threshold decision that a marker-sized step would not pass either way.
* outstanding: the engine's OverheadEdgesDetector::Detect 0x006ABE34 body, GroundPlaneROI 0x004F7774 and the four MapComponent entry points (0x0067F7AC/0x0067F814/0x0067E6B0/0x0067E50C) are not read; the stack's detector is an equivalent (SD1)

**M11-021 — The per-frame marker-mode gate** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/MarkerDetector.cs`
* effect: marker detection differs
* rests on: extracted read-only; the stack's marker front end compared against it in re-analysis/evidence/m11/marker-frontend.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: ShouldProcessVisionMode 0x006B5AA4: (1<<mode) & [VisionSystem+0xac] then the front schedule's CheckTimeToProcessAndAdvance 0x006AF1F8 (bool vector at schedule+0, wrapping counter at schedule+0xc); InitDefaultSchedules 0x006AEFDE fills all 16 modes with {true} and counter 0; sDefaultSchedules 0x0105C540; ApplyCLAHE(image, 4, out) 0x006B44EC (tbb table 0x006B44F4; live value 4): enum 4 sets [VisionSystem+0x358]=1, sums every 3rd byte of every 3rd row and clears the flag when sum >= 80*((cols+2)/3)*((rows+2)/3) (0x006B451A..0x006B457E, threshold 80 at 0xC8E14C); lazily CLAHE::setTilesGridSize(4,4) 0x006B4580..0x006B45D8 (0xC8E148) and setClipLimit(32.0) 0x006B45DC..0x006B4636 (0xC8E144); CLAHE::apply via [vptr+0x20] 0x006B4656..0x006B4672; post-CLAHE boxFilter(out,out,-1,(3,3),(-1,-1),true,4) 0x006B4686..0x006B46A8; timestamp copy 0x006B46B4. DetectMarkersWithCLAHE 0x006B4796 (tbh table 0x006B479E) enum 4 picks grey when [VisionSystem+0x358]==0 else the CLAHE image 0x006B47A8..0x006B47B4; the chosen image is copied into the Array<u8> and passed to DetectFiducialMarkers 0x008757AC..0x008757B2. Shipped CLAHE in libopencv_imgproc.so: CLAHE_Impl::apply 0x20DAC, tile LUT CLAHE_CalcLut_Body<uchar,256,0>::operator() 0x20834, bilinear CLAHE_Interpolation_Body<uchar>::operator() 0x203FE; divisible 320x240 -> tileW 80, tileH 60, tileSizeTotal 4800, lutScale 255/4800=0.053125, clipLimit (int)(32.0*4800/256)=600; clip 0x2094E..0x209B8, LUT 0x209BA..0x209F2 (vcvtr.s32.f32 ties-to-even then saturate), per-column tables 0x2129E..0x21370, interpolation 0x204B8..0x20542; re-analysis/obb/assets/cozmo_resources/config/engine/vision_config.json:5 DetectingMarkers true; InitialModeSchedules has no DetectingMarkers entry
* outstanding: the non-divisible CLAHE_Impl::apply branch (copyMakeBorder 0x20EC0..0x20F58) is not transcribed and the post-CLAHE ColumnSum<int,uchar> body is not read (the 16S body is reused); the shipped 320x240 camera is divisible so the branch is inert

**M11-033 — The image hand-off and the two VisionSystem::Update overloads** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/VisionSystem.cs`
* effect: the camera image reaches the vision system
* rests on: the stack's vision input path is to be compared against the engine's two Update overloads
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: VisionComponent::SetNextImage 0x00652B04; Processor 0x00651F08; UpdateVisionSystem(pose, image) 0x00653D30; VisionSystem::Update(PoseData, EncodedImage) 0x006B4B68: IsColor 0x006B4B7C, DecodeImageRGB 0x006B4B90 or DecodeImageGray 0x006B4C02, then Update(PoseData, ImageCache) 0x006B4C78; Update(PoseData, ImageCache) 0x006B4D5C: the pose and image guards 0x006B4D66/0x006B4D70, UpdatePoseData 0x006B4D88, GetGray 0x006B4D94. ImageCache layout: grey Image at entry+0x14, RGB ImageRGB at entry+0x54, grey-valid +0x94, RGB-valid +0x95 (ResetHelper<Image> 0x008744F0, ResetHelper<ImageRGB> 0x0087459E, GetImageHelper 0x0087465C). An RGB entry becomes grey via ImageRGB::FillGray 0x00872998 -> cv::cvtColor(rgb,gray,COLOR_RGB2GRAY=7,0) 0x008729CA (libopencv_imgproc.so 0x2BC78). EncodedImage::IsColor 0x4F2100 (encoding byte at +0x20, tbb table 0x4F2110). For the shipped 320x240 grey camera the colour branch is inert: only the grey member is stored, +0x95 stays 0 and FillGray is never called
* outstanding: the RGB2GRAY kernel body is not instruction-transcribed; the colour branch is inert for the shipped grey camera

**M11-035 — The VisionComponent per-mode result handlers and the frame broadcast** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/VisionSystem.cs`
* effect: the engine-to-game vision results and their order
* rests on: the stack's vision result dispatch is to be compared against the engine's handler order
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: VisionComponent::UpdateAllResults 0x006542EC runs markers 0x006544A2, faces 0x00654510, pets 0x0065457E, motion 0x006545E8, overhead edges 0x00654652, tool code 0x006546BC, computed calibration 0x00654726, image quality 0x00654790, laser points 0x006547FA; then CheckMailbox 0x00654A74 and the RobotProcessedImage broadcast 0x00654A14/0x00654A1C
* outstanding: build the M11-owned dispatch order and the VisionProcessingResult field mapping; the non-marker handler bodies and message layouts belong to other layers: faces/pets M14, motion M10, tool code M11 with the M2/M10 layout, computed calibration M11-039/M3 with the M2/M10 layout, image quality M11/M3 with the EngineErrorCodeMessage M2/M10 layout, laser points M11 with the M2/M10 layout, CheckMailbox, the RobotProcessedImage layout M2/M10

**M11-037 — The BlockWorld frame sequence for observed markers** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs`
* effect: objects are created, updated and forgotten each frame
* rests on: the stack's BlockWorld frame update is to be compared against the engine's sequence
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: BlockWorld::UpdateObservedMarkers 0x00624EE8: non-empty path is ClearOccluders 0x00624F98, AddLiftOccluder 0x00624FA4, CreateObjectsFromMarkers 0x00624FC4, AddAndUpdateObjects 0x0062505A, then (on its success) CheckForUnobservedObjects 0x006250CA, UpdatePoseOfStackedObjects 0x006250D0, BlockConfigurationManager::Update 0x0062520C, UpdateMarkerlessObjects 0x0062521A; the empty-list branch 0x625020 does ClearOccluders, AddLiftOccluder, CheckForUnobservedObjects 0x0062504E and skips the stacked-pose update. AddLiftOccluder body 0x6564D8 (raw state at the timestamp, Robot::GetLiftTransformWrtCamera, Transform3d::ApplyTo, Camera::Project3dPoints, Camera::AddOccluder with the transform z scale); BlockConfigurationManager::Update body 0x616D7C (DidAnyObjectsMovePastThreshold gate, UpdateAllBlockConfigs, PruneFullPyramids, UpdateLastConfigCheckBlockPoses, NotifyBroadcasterOfConfigurationManagerUpdate). UpdatePoseOfStackedObjects 0x621794 and UpdateMarkerlessObjects 0x625704 are cross-layer entry points (M10/M12)
* outstanding: the AddLiftOccluder occluder points (VisionComponent+4) are not read; the BlockConfigurationManager gate fields (this+0x24/this+0xc) are not read; UpdatePoseOfStackedObjects 0x621794 and UpdateMarkerlessObjects 0x625704 need M10/M12

**M11-038 — The BlockWorld game-broadcast entry points** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs`
* effect: the game sees object observations and connected states
* rests on: the stack's BlockWorld broadcasts are to be compared against the engine's entry points
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: BroadcastObjectObservation 0x0061FED8; BroadcastLocatedObjectStates 0x0061E6C0; BroadcastConnectedObjects 0x0061E91C; VisionSystem::CheckMailbox 0x006B2AD4
* outstanding: BroadcastLocatedObjectStates 0x0061E6C0, BroadcastConnectedObjects 0x0061E91C and VisionSystem::CheckMailbox 0x006B2AD4 need the M2/M10 message layouts and the mailbox

**M11-040 — The VisionComponent mailbox and processor thread** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/VisionSystem.cs`
* effect: images are queued and processed on a thread
* rests on: the stack's vision component thread is to be compared against the engine's
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: VisionComponent::SetNextImage 0x00652B04; Processor 0x00651F08
* outstanding: the engine's VisionComponent thread/mailbox body is not in the inventory; the stack uses a single-in-flight managed stand-in

### M12-manipulation — Docking, carrying and pre-action poses

**M12-008 — The pose chain that holds a carried object** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/LiftGeometry.cs`
* effect: a carried cube is held at the wrong point, so it cannot be placed from the lift height alone
* rests on: the -41 was missing, so a carried cube could not be placed from the lift height alone
* best authority: libcozmoEngine.so 3.4.0-1204: Robot::Robot 0x0050FBF0 builds the lift pivot at robot+0x2E4 (child of the origin at (-41,0,45); 0xC2240000 at 0x0050FFE0, 0x42340000) and the lift pose at robot+0x2F0 (child of the pivot at (66,0,0); 0x42840000 at 0x00510038); Robot::ComputeLiftPose 0x005151AC; CarryingComponent::SetObjectAsAttachedToLift 0x00632CC4 places the object at (|dockMarkerOffset|+4, 0, -12.5) (norm 0x00632E42..0x00632E74, 4.0 at 0x00632E7C, 0xC1480000 at 0x00632E82, sum 0x00632E8C)
* evidence: 0x0050FBF0 Robot ctor; 0x0050FFE0 -41.0 (0xC2240000) and 45.0 (0x42340000); 0x00510038 66.0 (0x42840000); 0x005151AC ComputeLiftPose; 0x00632CC4 SetObjectAsAttachedToLift; 0x00632E42/0x00632E4A/0x00632E5C/0x00632E62/0x00632E74 norm; 0x00632E7C 4.0; 0x00632E82 0xC1480000 = -12.5; 0x00632E8C vadd; the 15.0 stack tolerance this function passes to FindObjectOnTopOrUnderneathHelper is owned by M13-007
* outstanding: the FindObjectOnTopOrUnderneathHelper 15.0 stack tolerance this path passes is owned by M13-007 and is not built here; build it with M13.

**M12-011 — A drive that ends outside the pre-action pose tolerance reports DidNotReachPreActionPose** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/DriveActions.cs`
* effect: a failed drive reports success, or the wrong failure code
* rests on: the record carried a truncated BadObject citation and an incorrect claim that this function calls the threshold
* best authority: libcozmoEngine.so 3.4.0-1204: DriveToObjectAction::CheckIfDone 0x00559880 forms 0x04000001 (0x005598E2 movw r8,#1 + 0x005598E6 movt r8,#0x400), compares the distance from the pre-action pose against the tolerance at +0x84, and on failure moves it into the result (0x005599FE); it returns 0x03000004 BadObject when the object is not located (0x005598B6 movs r4,#4 + 0x005598BA movt r4,#0x300)
* evidence: 0x00559880 CheckIfDone; 0x005598E2/0x005598E6 form 0x04000001; 0x005599FE mov r4,r8; 0x005598B6/0x005598BA form 0x03000004 BadObject; this function does not call ComputePreActionPoseDistThreshold (M12-020)
* outstanding: the drive goal its CheckIfDone checks is M12-022's InitHelper goal, not yet built exactly; build M12-022.

**M12-020 — The production callers of ComputePreActionPoseDistThreshold** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/PreActionPose.cs`
* effect: a caller uses the threshold without the engine's sentinel handling, or a non-caller uses it
* rests on: the report had DriveToObjectAction::CheckIfDone as a caller; it is not
* best authority: libcozmoEngine.so 3.4.0-1204: a whole-.text scan finds exactly three callers of PLT 0x004AB938: DriveToPoseAction::CheckIfDone 0x0055AB4C (call at 0x0055ACF0), PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses 0x005558F4 (call at 0x005561A0) and IDockAction::GetPreActionPoses 0x005508C8 (call at 0x00550FF8); each caller reads BOTH outputs with one ldrd: DriveToPoseAction::CheckIfDone 0x0055ACF4 ldrd -> Point3 {out[0],out[1],GetHeight} at sp+0x8c used as the x/y position tolerance of Pose3d::IsSameAs 0x4A7060; PlaceRelObjectAction 0x005561B0 ldrd -> Point3 {out[0],out[1],100.0} for IsSameAs with Radians(0.1308997); IDockAction::GetPreActionPoses 0x00550FFC ldrd -> sb+0x20/sb+0x24, both must be > 0 and are compared against the robot's |dx|/|dy| to the closest pose, failing with 0x04000001; FlipBlockAction::Init 0x0055EDC8 also reaches the threshold through IDockAction::GetPreActionPoses (call 0x0055EE5E), so its pre-action check uses the pair; Pose3d::IsSameAs 0x00846EA4 compares the full rotation (the static RotationAmbiguities 0x0084B496 is default-constructed empty), no yaw flattening; the Point3 z of caller 1 is Robot::GetHeight 0x00516F0C = max(66*sin(liftAngle)+45+5, 67.7)
* evidence: 0x0055ACF0 blx 0x4AB938 (DriveToPoseAction::CheckIfDone); 0x005561A0 blx 0x4AB938 (PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses); 0x00550FF8 blx 0x4AB938 (IDockAction::GetPreActionPoses); DriveToObjectAction::CheckIfDone 0x00559880 contains no call to 0x4AB938; the formula itself is M12-001; DriveToPoseAction 0x0055ACF0 blx then 0x0055ACF4 ldrd r0,r1,[sp,#0x80] / 0x0055ACF8 strd -> sp+0x8c; used at 0x0055ADA8 IsSameAs 0x4A7060; PlaceRelObjectAction 0x005561A0 blx then 0x005561B0 ldrd r0,r1,[sp,#0x54]; 0x005561B4 r2=100.0; 0x005561BC stm r3!,{r0,r1,r2}; used at 0x005561EA IsSameAs with Radians(0x3E060A92); IDockAction::GetPreActionPoses 0x00550FF8 blx then 0x00550FFC ldrd r1,r2,[sp,#0x70]; stored at sb+0x20 (0x00551002) and sb+0x24 (0x00551008); positivity checks 0x0055101C/0x00551048; TooFarFromGoal 0x04000001; FlipBlockAction::Init 0x0055EE5E blx 0x4AB950 (GetPreActionPoses); the pre-action check uses the threshold pair out[0]/out[1] (0x00551002/0x00551008), not a 100 mm box; Pose3d::IsSameAs 0x00846EA4 -> IsSameAs_WithAmbiguity 0x00846F3C -> GetAngleDiffFrom 0x0084A694 (full quaternion, acos(2*dot^2-1)); RotationAmbiguities 0x0084B496 is empty (begin=end=0); Robot::GetHeight 0x00516F0C: sinf; *66.0 (0x00516F54); +45.0 (0x00516F58); +5.0; vcmpe 67.7 (0x00516F60 = 0x42876666)
* outstanding: caller 2, PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses 0x005558F4 (threshold call 0x005561A0), is not built: the C# PlaceRelObjectAction has no placement-pose computation; build it and wire the threshold pair {out0, out1, 100.0} with Radians(0.1308997) there.

**M12-022 — DriveToObjectAction::InitHelper's DriveToPoseAction goal** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/DriveActions.cs`
* effect: the drive goes to the wrong point or heading, or fails where the engine would dock
* rests on: the C# DriveToObjectAction drove to the pre-action pose's full rotation (later yaw-reduced); the engine's goal is a yaw-only point built from the object, the robot and +0x84
* best authority: libcozmoEngine.so 3.4.0-1204: DriveToObjectAction::InitHelper 0x00558FB4 builds the goal at 0x00559200..0x00559278: objectInRobotParent = objectPose.GetWithRespectTo(robotPose.GetParent()) (0x005590AE); delta = normalize(robot.xy - objectInRobotParent.xy) * DriveToObjectAction+0x84 (0x005590E6..0x005591E4; 0x005591C2 vldr s2,[sb,#0x84]); goal Point3 = {objectInRobotParent.x + delta.x, objectInRobotParent.y + delta.y, robot.z} (0x00559220..0x00559230); yaw = atan2f(-delta.y, -delta.x) (0x00559232/0x00559236/0x0055923A); Pose3d(yaw, Z_AXIS_3D, goal, parent=objectInRobotParent.GetParent()) (0x00559278); SetGoals 0x0055A6D0 stores the vector as-is
* evidence: 0x005590AE objectPose.GetWithRespectTo(robot.parent, local_5c); 0x005590E6..0x005590FE delta = robot.xy - local_5c.xy; 0x00559100..0x00559160 normalize; 0x005591C2 vldr s2,[sb,#0x84]; 0x005591E4 vmul delta *= distance; 0x00559220 vadd goal.y = delta.y + local_5c.y; 0x00559224 goal.x = delta.x + local_5c.x; 0x00559230 goal.z = robot.z; 0x00559232 eor r0,r1,#0x80000000 / 0x00559236 eor r1,r2,#0x80000000 / 0x0055923A blx 0x4A4510 (atan2f); 0x00559278 blx 0x4A47E0 Pose3d(yaw, Z_AXIS, goal, parent, name); the C# uses the chosen pre-action pose's position with the heading toward the object; the engine's +0x84 distance and exact point are unread
* outstanding: read the value/setter of DriveToObjectAction+0x84 (the 7-arg ctor sets -1.0) and build the engine's object+delta goal; until then the C# pre-action-pose position is a labelled reduction

### M15-freeplay — Needs, activities and freeplay

**M15-001 — NeedsManager initialization, periodic update and need-bracket rules** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs`
* effect: need levels, notifications, persistence cadence and bracket observations change
* rests on: libcozmoEngine.so and shipped needs configuration
* best authority: libcozmoEngine.so and shipped needs configuration
* evidence: CozmoEngine::Init 0x004EC9E6..0x004ECA0A; NeedsManager::Init 0x00692574; NeedsManager::Update 0x00695C9C; NeedsState::UpdateCurNeedsBrackets 0x0069C12C; needs_config.json
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-002 — Chooser factory, scoring, strict-priority and selection chooser semantics** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs`
* effect: the behavior selected for an activity changes
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: BSRunnableChooserFactory::CreateBSRunnableChooser 0x00609C88; ScoringBSRunnableChooser::ReloadFromConfig 0x00609F8C; ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060A3D8; StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060B23E; SelectionBSRunnableChooser::OnSelected 0x0060AF20
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-003 — Activity start/end predicates and cooldown lifecycle** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`
* effect: an activity becomes eligible, remains active or ends at different times
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: IActivityStrategy constructor 0x005B4EC8; IActivityStrategy::WantsToStart 0x005B529C; IActivityStrategy::RandomizeCooldown 0x005B5408; IActivityStrategy::WantsToEnd 0x005B5444; IActivityStrategy::SetCooldown 0x005B54E4
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-004 — Needs decay modifiers and damaged-part thresholds** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs`
* effect: need decay rates and the reported damaged-part count change
* rests on: libcozmoEngine.so and shipped needs configuration
* best authority: libcozmoEngine.so and shipped needs configuration
* evidence: NeedsState::GetDecayMultipliers 0x0069C214; decay modifier descending sort 0x00691040; NeedsState::NumDamagedPartsForRepairLevel 0x0069CCAC; needs_decay_config.json; needs_config.json
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-005 — Flat three-second cooldown after an activity ends within two ticks** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`
* effect: an immediately ending activity is delayed before it can restart
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: IActivityStrategy::WantsToStart 0x005B529C; activity times call site 0x005B26FE; IActivityStrategy::RandomizeCooldown 0x005B5408
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-006 — Freeplay activity selection and selected/deselected lifecycle** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs`
* effect: the active activity and behavior, animation state and pending reward handling change
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: ActivityFreeplay::PickNewActivityForSpark 0x005ADC44; ActivityFreeplay::GetDesiredActiveBehaviorInternal 0x005AE29C; IActivity::OnSelected 0x005B312C; IActivity::OnDeselected 0x005B33B8; IActivity::GetDesiredActiveBehavior 0x005B387C
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-007 — Activity feature gate and dead boredomMultiplier scoring data** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`
* effect: feature-disabled activities are excluded while dead scoring data has no effect
* rests on: libcozmoEngine.so and shipped configs
* best authority: libcozmoEngine.so and shipped configs
* evidence: IActivityStrategy::WantsToStart 0x005B529C; CozmoFeatureGate::IsFeatureEnabled 0x006A679C; features.json; BehaviorPounceOnMotion constructor 0x005F7F91
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-008 — Desired activity from objects and behavior-owned needs-action reporting** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs`
* effect: the chosen freeplay activity and needs rewards change
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: ActivityFreeplay::CalculateDesiredActivityFromObjects 0x005ADF4C; IBehavior::ExtractNeedsActionIDFromConfig 0x005BBAE8; IBehavior::NeedActionCompleted 0x005BE40C; ActivityGatherCubes::Update 0x005AF2F0
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-009 — Emotion event names fired when a cube is placed in a beacon** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs`
* effect: the mood events emitted by cube placement change
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: BehaviorExploreBringCubeToBeacon::FireEmotionEvents 0x005E002C
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-010 — Missing moved-block location ends the behavior without a reaction** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeReactions.cs`
* effect: the cube-moved behavior either acts or ends
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: BehaviorAcknowledgeCubeMoved::TransitionToTurningToLastLocationOfBlock 0x00602270
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-011 — Freeplay beacon is centered on the robot pose** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs`
* effect: the location used for subsequent cube gathering changes
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: BehaviorThinkAboutBeacons::SelectNewBeacon 0x005E5F0C; AIWhiteboard::AddBeacon 0x0056C39C
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-012 — Put-down image wait and CantHandleTallStack animation trigger** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/ManipulationBehaviors.cs`
* effect: the post-put-down action sequence and tall-stack reaction animation change
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: BehaviorPutDownBlock::CreateLookAfterPlaceAction 0x005C8174; BehaviorCantHandleTallStack::TransitionToDisapointment 0x005ED0F0
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-013 — Activity tree construction, desired names and discarded activityPriority** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs`
* effect: activity membership and ordering change
* rests on: libcozmoEngine.so and shipped activity configuration
* best authority: libcozmoEngine.so and shipped activity configuration
* evidence: ActivityFreeplay::CreateFromConfig 0x005AD478; ActivityStrictPriority constructor 0x005B23DC; activities_config.json
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-015 — Freeplay active-time tracker and its four pause sources** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs`
* effect: active freeplay telemetry accumulation and reporting change
* rests on: libcozmoEngine.so plus Unity HighLevelActivity enum
* best authority: libcozmoEngine.so plus Unity HighLevelActivity enum
* evidence: FreeplayDataTracker constructor 0x0056EBD4; FreeplayDataTracker::SendData 0x0056EC48; FreeplayDataTracker::SetFreeplayPauseFlag 0x0056EEBC; BehaviorManager::SetCurrentActivity 0x005A106C; Robot::CheckAndUpdateTreadsState 0x005121F4; Robot::SetOnChargerPlatform 0x00511DB0; unity/scripts/csharp/Anki.Cozmo/HighLevelActivity.cs
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

**M15-016 — NeedsManager pause and disconnect transitions** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs`
* effect: needs writes, schedules, notifications and bracket telemetry change across pause or disconnect
* rests on: libcozmoEngine.so
* best authority: libcozmoEngine.so
* evidence: NeedsManager::SetPaused 0x00695E04; NeedsManager::OnRobotDisconnected 0x00695908
* outstanding: Compare the current implementation with this approved source inventory and build every discrepancy.

## What remains after both: blocked externally, or needing hardware

| id | subsystem | status | what | why it cannot be settled here |
| --- | --- | --- | --- | --- |
| M1-033 | M1-transport | HARDWARE_ONLY | Robot-side transport behaviour | whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none) |
| M1-043 | M1-transport | HARDWARE_ONLY | Whether a 0-byte UDP read warns | the errno value after a 0-byte recvmsg on the phone |
| M3-008 | M3-device | HARDWARE_ONLY | How the firmware maps pair bits to physical display rows, and the robot playback period | only the robot can answer it |
| M3-016 | M3-device | HARDWARE_ONLY | Whether firmware 2457 emits colour frames in this format, and how the robot reacts to EnableColorImages | only the robot can answer it |
| M9-023 | M9-wwise-music | HARDWARE_ONLY | How the stock app and robot sounded when singing | Only an original-app/robot recording can establish the final audible result; a hardware pass verifies sound but cannot raise source provenance. |
| M11-016 | M11-vision | BLOCKED_EXTERNAL | Face, pet and motion detection | nothing recoverable: the detector is third-party binary code |
| M14-006 | M14-faces | BLOCKED_EXTERNAL | Text to speech is not implemented | the voice model and the plug-in are third-party binaries |
| M4-013 | M4-control | HARDWARE_ONLY | Whether the robot honours StartMotorCalibration after its connection-time calibration | only a robot can show whether the request is honoured |
| M5-036 | M5-animation | HARDWARE_ONLY | Robot-side animation behaviour: AbortAnimation handling and leftovers, Start without End and the unbounded keep-alive stream, locked-track suppression, animStarted/animEnded echo | only the robot can answer it |
| M4-021 | M4-control | HARDWARE_ONLY | Whether the robot reports the origin id and frame from AbsoluteLocalizationUpdate in RobotState | only the robot can answer it |
| M4-024 | M4-control | HARDWARE_ONLY | What makes the robot forward cube telemetry after connection, and the robot-side effect of StreamObjectAccel | only the robot can answer it |
| M3-036 | M3-device | HARDWARE_ONLY | The robot's reply to a factory read with Length = 1, and the non-factory Length = size+16 re-request contract | a robot run sending Length = 1 and recording the reply, and a non-factory read large enough to need a re-request |

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
| M9-016 | M9-wwise-music | COMPATIBILITY_POLICY | Streaming uses a 66 ms stack lead policy against the original 30000-byte and 14-frame budget | libcozmoEngine.so for the original budget; explicit stack policy for 66 ms |
| M11-012 | M11-vision | COMPATIBILITY_POLICY | The nominal camera calibration stand-in | not applicable: the live path reads the robot own calibration and fails closed without it |
| M11-013 | M11-vision | COMPATIBILITY_POLICY | AllowUnconnectedObjects switch | the engine connected-object rule, which is implemented |
| M12-014 | M12-manipulation | COMPATIBILITY_POLICY | The docking error signal's last two bytes are whatever was on the engine's stack | libcozmoEngine.so 3.4.0-1204: UpdateDockingErrorSignal 0x0063BE80 is the only builder and never writes the struct bytes at +0x14/+0x15 (stack +0xb4/+0xb5); nothing clears the struct (no memclr, no ctor call), and Pack 0x007C0B26 reads both bytes and sends them |
| TOOL-001 | tools | COMPATIBILITY_POLICY | Conformance CLI pass and fail criteria | not applicable: the harness is not part of the app |
| TOOL-002 | tools | COMPATIBILITY_POLICY | The fake robot side answers place docks without a marker signal | not applicable: this is the test double, not the robot |
| TOOL-003 | tools | COMPATIBILITY_POLICY | The --nominal calibration override in the vision, manipulation and freeplay tools | the live path fails closed without a real calibration |
| TOOL-005 | tools | COMPATIBILITY_POLICY | The hardware acceptance campaign: how a check is judged, and what a result may change | not applicable: the harness is not part of the app. The rule that matters runs the other way - a hardware result never changes a fidelity record's status. A check that passes says the behaviour was observed; a check that fails is an investigation item, not a licence to tune a source-backed constant. M11-005 stays open whatever the vision check reports, and the charger's 20 x 27 mm marker geometry is not adjusted to make the mount succeed |
| M13-014 | M13-navigation | EQUIVALENT_IMPLEMENTATION | Knock over a stack: BehaviorKnockOverCubes has no verified fidelity record | BehaviorKnockOverCubes 0x005C2EA0..0x005C3C00, its knock-over callback 0x005C3DCE, IBehavior::StartActing(action, function<void(Robot&)>) 0x005BE0E4 and its lambda 0x005BF8F4, IBehavior::Init 0x005BCB54 and ReadFromJson 0x005BBFB4, read; DriveAndFlipBlockAction 0x0055E208 not read past its arguments; DriveAndFlipBlockAction 0x0055E208, IDriveToInteractWithObject 0x0055B1F4, ReactionTriggerStrategyNoPreDockPoses::ShouldTriggerBehaviorInternal 0x00610E32, AIWhiteboard::AIWhiteboard 0x0056A270 and BehaviorRamIntoBlock's transitions, read |
| M2-017 | M2-protocol | COMPATIBILITY_POLICY | A field whose read failed in a kept malformed message holds 0 / false (the engine leaves stale stack bytes) | libcozmoEngine.so 3.4.0-1204 |
| M3-019 | M3-device | COMPATIBILITY_POLICY | The connection-time SetCameraParams: the engine sends stale stack bytes for f32@0 and u16@4 and bool@6 = 1; this stack sends 0.0, 0, true | libcozmoEngine.so 3.4.0-1204 |
| M3-020 | M3-device | COMPATIBILITY_POLICY | A payload that is empty or all 0xFF: the engine reads data[-1] (undefined); this stack treats it as a decode failure | libcozmoEngine.so 3.4.0-1204 |
| M6-021 | M6-wwise-bank | COMPATIBILITY_POLICY | The injectable RNG seed seam: WwiseSelection's constructor takes a seed for tests; the live default is Unix seconds, matching the engine's time(NULL) | libcozmoEngine.so 3.4.0-1204 (statically linked Wwise 2016.2 runtime, ARM 0x0095E540..0x00AE2E40) |
| M8-004 | M8-framework | COMPATIBILITY_POLICY | Behaviours built in code carry a score of their own; the engine's default is zero | IBehavior::IBehavior 0x005bbb74 and IBehavior::EvaluateScoreInternal 0x005beec2, read |
| M8-008 | M8-framework | COMPATIBILITY_POLICY | The head recalibration wait: the engine has no timeout, this stack keeps a backstop | CalibrateMotorAction::CheckIfDone 0x00547D38 and IAction::IAction 0x00540C44, read |
| M8-009 | M8-framework | COMPATIBILITY_POLICY | Behaviour scope undo order: the engine releases in a fixed order, this stack releases most-recent-first | IBehavior::Stop 0x005bd08c, read |
| M8-010 | M8-framework | COMPATIBILITY_POLICY | Behaviour inventory classifier rules | not applicable: this is bookkeeping, not robot behaviour |

