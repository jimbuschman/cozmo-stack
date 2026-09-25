# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **258 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 176 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 22 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 0 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 19 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
| COMPATIBILITY_POLICY | 27 | A deliberate product or platform decision this stack intends to keep: offline tools, the test harness, PC-side plumbing, or a stand-in the operator has to ask for. Not a place to put fidelity work that is hard. |
| HARDWARE_ONLY | 6 | No shipped artifact can settle it; only a robot, or a recording of the stock app, can. |
| BLOCKED_EXTERNAL | 8 | The answer lies in third-party code or data that is not in the package (Omron OKAO, the Wwise runtime DSP, the Acapela text-to-speech engine). |

## Where each subsystem stands

Four separate things, because one word cannot carry them. **Source read** means nothing is left that
reading the original would settle. **Built** means nothing the original is known to do is knowingly
not done here. Neither says the behaviour is faithfully reproduced: the last two columns are what
remains after both, and they do not go away by working harder on this repository.

| subsystem | records | to read | to build | blocked externally | needs hardware | source read | built |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| M1-transport — UDP transport and reliability | 43 | 0 | 3 | 0 | 2 | yes | no |
| M2-protocol — CLAD messages and protocol helpers | 16 | 0 | 16 | 0 | 0 | yes | no |
| M3-device — Camera, display and audio device layer | 18 | 0 | 0 | 0 | 1 | yes | yes |
| M4-control — Motion, sensors, lights and cubes | 13 | 0 | 0 | 0 | 1 | yes | yes |
| M5-animation — Animation clips, scheduler and face | 23 | 0 | 0 | 0 | 0 | yes | yes |
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
| M1-transport | INVENTORY_APPROVED | 29 | 0 | 0 | 3 |
| M2-protocol | INVENTORY_APPROVED | 0 | 0 | 0 | 0 |
| M3-device | UNREVIEWED | 13 | 1 | 0 | 0 |
| M4-control | UNREVIEWED | 8 | 0 | 0 | 0 |
| M5-animation | UNREVIEWED | 22 | 3 | 0 | 0 |
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

**M1-027 — Arrived-message filters and the fatal robotError disconnect** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`
* effect: messages the app drops are handled, or a fatal robot error does not disconnect
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B27 no robot -> silent drop (0x0069D8D2..0x0069D8D8); size 0 -> error (0x0069D8E0); ShouldFilterMessage -> drop (0x0069D8EE); unpack size mismatch -> drop (0x0069D910); tag 0xD9 robotError (0x0069D916): fatal -> event, broadcast, ClearData, DisconnectCurrent and stop the loop (0x0069DAC4..0x0069DACA); non-fatal -> RobotErrorPassThrough then broadcast (0x0069D936); CC12..CC17: RobotErrorReport {u32 code 0..5, bool fatal} (0x007D3E66..0x007D3F40); fatal is decided by the byte only (0x0069D930); before validation it is filtered (0x0069D8EE); fatal: event, broadcast, ClearData, DisconnectCurrent, no game PassThrough, no reason (0x0069D938..0x0069DAEA); non-fatal: event, RobotErrorPassThrough to the game, broadcast (0x0069DA36..0x0069DAAE); CC32..CC36: ProcessMessages exactly (0x0069D870..0x0069D9FC); a 1-byte message with a tag outside 0xB0..0xF5 unpacks as 1 byte and is broadcast (0x007B1ADC..0x007B1BCA)
* outstanding: robot-to-engine tags inside 0xB0..0xF5 that have no codec in this stack cannot be size-checked (CC35); needs the M2 protocol layer; then EXACT_SOURCE

**M1-029 — Firmware version check against the shipped firmware header** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`
* effect: a robot is accepted or refused as outdated where the app decides otherwise
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: G5.1..G5.6 HandleFirmwareVersion 0x0052D470: guard (0x0052D478..0x0052D484); JSON parse (0x0052D4BA); FACTORY build path (0x0052D4C2..0x0052D506); v = version, t = time (0x0052D51A..0x0052D536); expected E_v/E_t at +0x1C/+0x20 (0x0052D538); sim flag (0x0052D53E..0x0052D5B6); G5.9/G5.10 no robotAvailable and not sim -> result 1 (0x0052D7B8..0x0052D850); sim -> 0 (0x0052D7C6); G5.11 robotDev = v == t, appDev = E_v == E_t; if they differ -> 3 OutdatedFirmware (0x0052D7DE teq.w r0,r1; 0x0052D7E4 movs r0,#3); G5.12 unsigned: E_v == v -> 0; E_v > v -> 3; E_v < v -> 4 OutdatedApp (0x0052D8DA..0x0052D8E0; 0x0052D9A6..0x0052D9AC); time is not compared again; G5.14/G5.15 E_v/E_t are copied from RobotManager+0x84/+0x88 in AddRobot (0x0052EEF2/0x0052EEF8; ctor 0x0052D196); the RobotManager ctor zeroes them (0x0052E512, 0x0052E516); G5.16..G5.20 RobotManager::Init -> FirmwareUpdater::LoadHeader starts a loader thread (0x0052E7F8; pthread_create 0x0067692A) that reads config/engine/firmware/cozmo.safe and parses the JSON header in the first 0x800 bytes (0x00677C44..0x00677D34); ParseFirmwareHeader stores version -> +0x84, time -> +0x88 (0x0052EA36..0x0052EA9A); G5.21/G5.32..G5.37 Scope 1 is DataPlatformResourcesPath = persistentDataPath/cozmo/cozmo_resources (pathToResource 0x0084BE34 table 03 14 22 2f 41; unity/scripts/csharp/PlatformUtil.cs:5-13), extracted from the shipped assets (re-analysis/obb/assets/resources.txt:2075); the shipped header has version 2381, time 1546972025 (re-analysis/obb/assets/cozmo_resources/config/engine/firmware/cozmo.safe offsets 0-445); G5.22..G5.30 nothing orders the header load before AddRobot: the loader starts in cozmo_startup before the engine thread (0x006661EA, 0x0065B14E), and neither the ConnectToRobot handler nor AddRobot checks the load (0x004ED026..0x004ED1EC; loaded flag +0x18 unread on that path); G5.31 a missing, short or unparsable file leaves E_v = E_t = 0 for the session (0x006764E4, 0x00677C50..0x00677D28); G5.38..G5.40 no writer of RobotManager+0x84/+0x88 besides the ctor and ParseFirmwareHeader was found; the scan cannot prove absence (adjusted-base, register-offset, whole-object and untyped accesses are outside it); see decision D7
* outstanding: the mechanism is built (batch 3); jsoncpp's grammar (comments, trailing commas) and asUInt of a non-number are not in the rows, so the reader's leniency is MISSING; under M1-040 the outcome never refuses; then EXACT_SOURCE

**M1-041 — Robot initialisation after a Success connection response, and the gates it opens** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`
* effect: the robot is not synced or initialised, or is initialised in the wrong order or at the wrong time; state or animations are processed before the engine would
* rests on: not implemented: the candidate sends GetManufacturingInfo, SyncTime, InitController and block-pool setup unconditionally on the transport ConnectionResponse (CozmoRobot.cs ~261-275)
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: CD17/CB21: the Success RobotConnectionResponse reaches, synchronously and in this order: RobotEventHandler, VisionComponent, TracePrinter (0x006625C6..0x006625FA; 0x00663C74..0x00663CA8; 0x005259AC, 0x006501E4, 0x0053BCBA); CD18/CB22/CB23: RobotEventHandler (result 0) calls Robot::SyncTime: +0x29 = 0, history clear, then SyncTime {u32 BaseStationTimer ms, 0xC1A00000} reliable; only if that was sent, InitController; only if that was sent, ImageRequest {Stream, QVGA} and AbsoluteLocalizationUpdate {0, frameId, originId, 0, 0, 0}; a failed send warns and stops (0x005289BA..0x005289D2; 0x0051521E..0x005153AE); CD19: SyncTime is never retried; after 5.0 s without SyncTimeAck it warns "SyncTimeAckNotReceived" (0x00513BF6..0x00513C5A); SyncTimeAck sets +0x29 = 1 (0x005366A4..0x005366AC); CD20: RobotEventHandler then sets ready-to-stream (+0x2A) through an NVStorage on-idle callback, which runs once the NV request queue is empty (0x00528A5A..0x00528A6E; 0x00645C20..0x00645C32); CD22: TracePrinter sends SetAppRunID (16-byte UUID, 0xFF unless a platform id exists), then RequestCrashReports{0}, each crash report received asking for the next index up to 3 (0x0053D398..0x0053D430; 0x0053CA88..0x0053CA9E); CD23/CD12: robot state is dropped until time sync (0x0051293C..0x0051294E); Robot::Update does nothing past the idle component until the first full state is handled (+0x34E); the AnimationStreamer runs only when synced and ready to stream (0x00513BF2..0x00514470); CD30: nothing sends SendHeadAngleUpdate, setAccessoryDiscovery or SetRobotImageSendMode on this path (BL scan)
* outstanding: built (batch 3) except AbsoluteLocalizationUpdate, which is not sent: its frameId (robot+0x2B0) and originId (+0x294 then +0x14) are not state this stack holds; and RobotStateHistory::Clear has no owner here; then EXACT_SOURCE

### M2-protocol — CLAD messages and protocol helpers

**M2-001 — CLAD wire primitives: little-endian, bool write 0/1 and read (byte != 0), all-or-nothing ReadBytes, u8/u16-count arrays and strings, fixed arrays without a prefix** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Clad/Clad.cs`
* effect: a field is encoded or decoded differently from the engine
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: CLAD schema and generated serialisation
* evidence: S4 WriteBytes memcpy native LE (0x0083C084); Write<bool> stores the bool byte (0x0083C0DE); S5 byte-vector writer 0x0071A890: u16 count = end-begin stored with strh, truncated (0x0071A898..0x0071A8A4); one WriteBytes(1) per element (0x00732108..0x00732130); D8 ReadBytes all-or-nothing, no advance and no sticky error on overrun (0x0083C09A..0x0083C0CA); D9 Read<bool> stores (byte != 0) (0x0083C0F0..0x0083C118); D10 variable readers read the count then elements, stop at the first failed element; u8-count i32[] 0x0073923A, u8-count string (ldrsb chars) 0x006C235C, u8-count u8[] 0x007A17D8, u16-count u8[] 0x0071A8F6, u8-count u32[] 0x0071283C; D12 fixed arrays have no prefix and stop at the first failure (0x007D6EB6..0x007D6EDA); D13 little-endian: fields memcpy natively (0x0083C0B6); ELF EI_DATA = 1
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-002 — RobotStatusFlag names and values, and where the engine stores each consumed bit** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Sensors.cs`
* effect: a status bit is read with the wrong meaning
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: decompiled Unity
* evidence: EnumToString(RobotStatusFlag) 0x007D57C8: 17 names and values, agreeing with Unity RobotStatusFlag.cs:8-25; RS11 the whole status word -> robot+0x350 (0x00512AD8..0x00512ADC); bit storage: 0x1 MovementComponent+9 (0x0063E30A); 0x2 Delocalize argument (0x00512B98); 0x4 [robot+0x280]+4 (0x00512A96); 0x8 robot+0x349 (0x00512AA2); 0x10 robot+0x34C (0x00512ACE); 0x20 treads classifier (0x00511EC4); 0x100 MovementComponent+0xB = !bit (0x0063E32C); 0x200 +0xA = !bit (0x0063E320); 0x1000 SetOnCharger (0x00512AAC); 0x2000 robot+0x339 (0x00512AB8); 0x4000 CliffSensorComponent+6 (0x00634026); 0x8000 MovementComponent+0xC (0x0063E338); 0x10000 robot+0x33A (0x00512AC2)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-003 — liftAngle is radians; height = 66 sin(angle) + 45 with no clamp on the angle-to-height path; 32..92 clamps only height-to-angle** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Sensors.cs`
* effect: the lift height is reported differently from the engine
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so
* evidence: RS7 liftAngle -> robot+0x300 raw and ComputeLiftPose (0x0051295E..0x0051296E); GetLiftHeight 0x00516F64..0x00516F8E: 66*sinf(+0x300) + 45 + 0 (literals 0x00516F90/94/98), no clamp; ConvertLiftHeightToLiftAngleRad 0x005170B0: height raised to 32 (literal 0x005170F4); height >= 92 (0x00517100) uses 0.712121 (0x00517104); asinf; RS0 UpdateFullRobotState is skipped while robot+0x29 == 0 (0x0051293C..0x00512942)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-004 — The SyncTime payload: u32 timestamp and f32 -20.0** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`
* effect: the robot gets a different time-sync payload
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: SyncTime Pack 0x007A3A94, size 8; operator== 0x007A3AFA: word 0 integer cmp, word 1 vcmp.f32 (0x007A3B0A), so word 1 is f32; SendSyncTime 0x0051524C builds {GetCurrentTimeStamp() (0x00515266), 0xC1A00000 = -20.0f (0x0051526C/0x00515270)}; the rest of SendSyncTime (InitController, ImageRequest {1,4}, AbsoluteLocalizationUpdate, the +0x520 stamp) is M1-041
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-005 — The light colour word: 5-5-5 with red at bit 10, and bit 15 set from the alpha byte** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Clad/MessageExtras.cs`
* effect: a light shows a different colour
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: CubeLightComponent::SendTransitionMessage 0x00638056 packs the engine ColorRGBA word whole: mov.w ip,#0x7c00 / mov.w r3,#0x3e0 / and.w r4,ip,r5,lsr #17 (red bits 27..31 to bit 10) / and.w r7,r3,r5,lsr #14 (green bits 19..23 to bit 5) / ubfx r4,r5,#0xb,#5 (blue bits 11..15 to bit 0) / tst.w r5,#0xff with orrne r7,r7,#0x8000 (bit 15 from the alpha byte). ColorRGBA holds red in the top byte and alpha in the bottom, which CubeLightComponent::WhiteBalanceColor 0x0063a894 confirms by reading the low byte as the alpha it gates on
* evidence: CubeLightComponent::SendTransitionMessage 0x00638056; CubeLightComponent::WhiteBalanceColor 0x0063a894 treats the low byte as alpha; every colour in every shipped light config has a non-zero alpha - cube configs write [r,g,b,255] and backpack configs [r,g,b,1] - so the engine sets bit 15 on every word it sends, the black that turns a light off included; CubeLightComponent::SendTransitionMessage 0x00638052..0x00638070 re-read in the M2 OUT pass; the same expression in BodyLightComponent::SetBackpackLightsInternal 0x00631F50..0x00631F72 (on) and 0x00631F96..0x00631FBE (off)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-006 — FirmwareVersion 0xEE layout {u16, u16-count u8[]}; the engine parses build/version/time/sim from msg+4 and has no hash fields** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: the firmware version is read differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: FirmwareVersion Unpack 0x007B907E: 2B@0, u16-count u8[]@4; M1 G5.2..G5.6: the handshake parses msg+4 (0x0052D48C..0x0052D5B6); messageEngineToRobotHash and messageRobotToEngineHash: 0 byte hits in libcozmoEngine.so
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-007 — absLocalizationUpdate: the timestamp first and the heading last** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Generated/RobotMessages.g.cs`
* effect: the robot is told a different pose
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: Robot::SendAbsLocalizationUpdate(Pose3d const&, unsigned int const&, unsigned int const&) 0x00512734 and Robot::SendAbsLocalizationUpdate() 0x00514710, read
* evidence: The packer 0x00512734 fills the 24 bytes in order from 0x00512774: its two unsigned arguments (strd r8, sb at 0x0051279E), the pose parent id from PoseBase::GetID (0x00512762), the transform translation x and y at +0x20 and +0x24 (0x00512782, 0x0051278A), and Rotation3d::GetAngleAroundZaxis (0x00512796). So the last word is a float heading in radians, which pycozmo had as an unknown u32 - and pycozmo sending 0x80000000 there is sending -0.0.; The argument order comes from the no-argument twin 0x00514710: it asks RobotStateHistory::GetLatestVisionOnlyState for the latest vision-only state with the timestamp in an out-parameter at sp+0x7c, and passes that as the second argument and the state word at +0x10 as the third (0x00514770..0x0051477C). So the first field is the timestamp and the second the pose frame id.; That is also the path a corrected pose takes to the robot: Robot::SetNewPose 0x005126EC calls SetPose and AddVisionOnlyStateToHistory, and Robot::Update calls SendAbsLocalizationUpdate (0x00513CC6), which sends the state just added.; M2 OUT pass: Pack 0x007A384E, Size 0x007A38E8, operator== 0x007A38EC (cmp at 0,4,8; vcmp at 0xC..0x14) confirm the types; the ContainsOriginID send gate (0x00512766..0x00512772) is M1-041
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-008 — Outbound union: tag byte first, then the member; size = 1 + member; typed constructors write the tag** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Clad/Clad.cs`
* effect: a message goes out with the wrong tag or size
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: S1 EngineToRobot::Pack writes the 1-byte tag at this+0 (0x007AB6B8..0x007AB6C4), then tbh on tag-1 (0x007AB6CC..0x007AB6D2, table 0x007AB6D6); S2 single-scalar members packed inline (0x007AB834, 0x007AB840, 0x007AB84A, 0x007AB858, 0x007AB868); S3 EngineToRobot::Size = 1 + member Size (0x007ABB98 movs r0,#1); S7 each typed constructor writes its tag, confirmed for all 42 (e.g. SetHeadlight 0x007A7B34 movs #0xb / 0x007A7B38 strb)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-009 — Outbound layouts and sizes of the 42 engine-to-robot messages this stack sends** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Generated/RobotMessages.g.cs`
* effect: the robot receives a message with different bytes
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: the per-message Pack/Size/operator== addresses in re-analysis/inventory/M2-protocol.md Appendix A section 1 (tags 03 05 08 0A 0B 25 32 34 35 36 37 39 3B 3C 3D 3E 3F 41 42 43 44 45 48 4A 4B 4C 58 60 64 66 80 81 8E 8F 93 94 97 99 9A 9B 9F A0); type-only differences from the protocol definition: SyncTime word 1 f32; DockWithObject field0 f32; bool (not u8) in SetBodyAngle isAbsolute (0x007A29BE), PointTurn useShortestDirection (0x007A343C), DockWithObject fields 5 and 8 (0x007C06B4, 0x007C06DC), PlaceObjectOnGround field 6 (0x007C0986), DockingErrorSignal fields 5 and 6 (0x007C0B78, 0x007C0B80)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-010 — Inbound dispatch: TBH at 0x007B1B10; out-of-range tags and the 14 in-range tags without a codec (0xCC, 0xDF..0xEB) consume only the tag byte; 56 codecs** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`
* effect: a robot message is dropped or delivered differently
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: D1 ProcessMessages: fresh RobotToEngine tag 0xFF, Unpack(ptr,len), drop when the return differs from the length (0x0069D8F8..0x0069D910); D2 Unpack(ptr,len) wraps SafeMessageBuffer (0x007B510A..0x007B511E); D3 tag read with ReadBytes(1), initialised 0xFF (0x007B1ADC..0x007B1AFC); D4 subs r0,#0xb0; cmp r0,#0x45; bhi default; tbh, table 0x007B1B10..0x007B1B9B (0x007B1B04..0x007B1B0C); D5 the default 0x007B1BBE..0x007B1BCC stores the tag and returns GetBytesRead = 1; taken by out-of-range tags and 0xCC, 0xDF..0xEB; D6 inline codecs 0x007B1B9C..0x007B1BB4; D7 the return is always GetBytesRead (0x0083C032..0x0083C038)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-011 — Inbound size rule: kept only when bytes consumed == length; field read failures otherwise ignored** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Clad/Clad.cs`
* effect: a malformed or over-counted message is kept or dropped differently from the engine
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: D1 the length compare (0x0069D906..0x0069D910); D7 GetBytesRead is returned whatever the field reads did (0x0083C032..0x0083C038); D8 ReadBytes all-or-nothing without a sticky error (0x0083C09A..0x0083C0CA); D10 variable readers stop at the first failed element and the member discards the result (0x0071A8F6, 0x007A17D8, 0x0073923A); D12 fixed arrays stop at the first failure (0x007C8852..0x007C886A)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-012 — Inbound layouts and sizes of the 56 robot-to-engine codecs** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Generated/RobotMessages.g.cs`
* effect: a robot message is decoded into different values
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: the per-codec Unpack addresses in re-analysis/inventory/M2-protocol.md Appendix B section 2 (0xB0..0xF5 except 0xCC and 0xDF..0xEB); type-only differences: bool (not u8) in GoalPose 0x007C2206, PickAndPlaceResult field1 0x007C1ADA, Ramp/BridgeTraverseComplete, TimeProfileStat field2, RobotErrorReport 0x007D3E70, LiftLoad (inline 0x007B1BA4); PrintTrace first field is one 4-byte field (0x007D4BF6)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-013 — FallingStopped 0xDE is {u32 timestamp, u32 duration_ms, f32 impactIntensity}** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Generated/RobotMessages.g.cs`
* effect: the fall duration and intensity are wrong
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: Unpack 0x007B0EB6: three ReadBytes(4) at +0, +4, +8; HandleFallingStopped 0x00535040: logs "timestamp: %u, duration (ms): %u, intensity %.1f" (0x00535274) from ldrd r1,r2,[r5] (0x0053506C) and vldr s0,[r5,#8] (0x00535068); [r5+8] > 1000.0f triggers NeedsManager action 0x11 (0x005350AA..0x005350C2); +4 to to_string(unsigned) (0x005350CC); the game message {duration_ms, impactIntensity} from ldrd [r5,#4] (0x00535186)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-014 — PickAndPlaceResult 0xB8: u32, bool success, i8 DockingResult, BlockStatus 0 NO_BLOCK / 1 BLOCK_PLACED / 2 BLOCK_PICKED_UP** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/Docking.cs`
* effect: a pick-up is taken as a place and the reverse
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: Unpack 0x007C1AC6 (Read<bool> 0x007C1ADA); HandlePickAndPlaceResult: +4 stored (0x00533794..0x0053379A); +6 == 2 -> 0x005337F8, == 1 -> 0x00533856 (0x005337A4..0x005337AA); +5 read ldrsb (0x005337F8); EnumToString(BlockStatus) 0x007C168C, name table 0x01034984 -> "NO_BLOCK", "BLOCK_PLACED", "BLOCK_PICKED_UP"
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-015 — Outbound builders copy caller speed, acceleration, duration and angle values verbatim: no defaults, no unit conversion** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Clad/MessageExtras.cs`
* effect: a motion command goes out with values the caller did not give
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: DriveWheels/MoveLift/MoveHead verbatim word copies (0x0063F174, 0x00640E1C, 0x0063F900, 0x00640C00, 0x0063F6BC, 0x00640ACC); MoveLiftToHeight 0x00640700 and MoveHeadToAngle 0x006407CC copy the four floats in order (0x0064076C, 0x00640838 stm); TurnInPlace 0x00640898 verbatim (0x006408B0..0x00640940)
* outstanding: compare the code against the inventory rows (the step after approval)

**M2-016 — ImageChunk 0xF2 field types: u32 ts, u32 id, i32 chunkDebug, u8 imageEncoding, i8 resolution, u8 count, u8 chunkId, i16 status, u16-count data** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Generated/RobotMessages.g.cs`
* effect: an image chunk field is decoded with the wrong sign
* rests on: the existing stack code; not yet compared against re-analysis/inventory/M2-protocol.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: Unpack 0x007C60B4: ReadBytes 4,4,4,1,1,1,1,2 then the u16-count vector 0x0071A8F6; imageEncoding: ldrb (0x004F1D7A), unsigned range check (0x004F218E), zero-extended to EnumToString(ImageEncoding) (0x004F225E); the json i8 is contradicted; resolution: sxtb before EnumToString(ImageResolution) (0x004F1E56); equality with 4 (0x004F1D4C); imageChunkCount ldrb + 32-bit subs (0x004F1E0C); chunkId ldrb (0x004F1DB2, 0x004F1D5E); frameTimeStamp unsigned compare (0x004F1E2C bls); chunkDebug and status are only copied or equality-compared (Pack 0x007C61B4, 0x007C6202; operator== 0x007C6276, 0x007C629E; Viz 0x006C067C); declared i32/i16 by the Unity generated CLAD ImageChunk.cs:212,217 (MD6)
* outstanding: compare the code against the inventory rows (the step after approval)

## What remains after both: blocked externally, or needing hardware

| id | subsystem | status | what | why it cannot be settled here |
| --- | --- | --- | --- | --- |
| M1-033 | M1-transport | HARDWARE_ONLY | Robot-side transport behaviour | whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none) |
| M1-043 | M1-transport | HARDWARE_ONLY | Whether a 0-byte UDP read warns | the errno value after a 0-byte recvmsg on the phone |
| M3-008 | M3-device | HARDWARE_ONLY | Scanline parity: dd written as 01 | whether the firmware selects a physical row from the bit position; only the panel can show this |
| M3-016 | M3-device | HARDWARE_ONLY | Colour camera frames are half-width three-component JPEG | no colour capture exists; the robot has only ever been asked for grayscale |
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
| M3-004 | M3-device | COMPATIBILITY_POLICY | Warm-up frames are flagged, and delivered like any other | RobotToEngineImplMessaging::HandleImageChunk and VisionComponent::SetNextImage, read |
| M3-013 | M3-device | EQUIVALENT_IMPLEMENTATION | TargetInFlight 10, counter-paced feed, Busy window 200 ms, priming | the engine feeds to its own budget every update (UpdateStream 0x0057C84C) |
| M3-017 | M3-device | COMPATIBILITY_POLICY | Test tones, beeps and sweeps | not applicable |
| M3-018 | M3-device | COMPATIBILITY_POLICY | Nominal-width diagnostic colour JPEG presentation | COMPATIBILITY_POLICY; the official app interpolation algorithm has not been recovered |
| M4-004 | M4-control | COMPATIBILITY_POLICY | Motion is gated on calibration here; the engine reacts to it instead | HandleMotorCalibration 0x00536A68, BehaviorReactToMotorCalibration 0x006065F0, and the callers of IsHeadCalibrated / IsLiftCalibrated, all read |
| M4-005 | M4-control | COMPATIBILITY_POLICY | Action ids cycle 1..255 | the engine action id allocation |
| M4-006 | M4-control | COMPATIBILITY_POLICY | Wheel confirmation tolerance 35 percent / 5 mm per s | not applicable: the engine does not confirm wheel speeds this way |
| M4-007 | M4-control | COMPATIBILITY_POLICY | StopAll sends StopAllMotors and a zero DriveWheels | the engine stop path |
| M4-009 | M4-control | EQUIVALENT_IMPLEMENTATION | Cube tracking from ObjectAvailable and ObjectConnectionState | HandleActiveObjectAvailable 0x0053391C, HandleActiveObjectConnectionState 0x00533B3C, HandleActiveObjectMoved 0x00533E30 and HandleObjectPowerLevel 0x00537130, read |
| M5-018 | M5-animation | EQUIVALENT_IMPLEMENTATION | How many frames one wall-clock tick streams | the engine streams to the audio budget on every update regardless of the clock (UpdateStream 0x0057C84C) |
| M5-020 | M5-animation | COMPATIBILITY_POLICY | Expressions helper faces | not applicable |
| M5-021 | M5-animation | EQUIVALENT_IMPLEMENTATION | The eye and its lids are filled by scanline, not by cv::fillConvexPoly | ProceduralFaceDrawer::DrawEye 0x005850E0, read |
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
| M5-022 | M5-animation | EQUIVALENT_IMPLEMENTATION | What the engine does when the robot disconnects during a streaming animation | Robot::SendMessage 0x0051349C, AnimationStreamer::SendBufferedMessages 0x0057BF60, UpdateStream 0x0057C84C, Update 0x0057CE5C, RobotManager::RemoveRobot 0x0052F1A4, Robot::~Robot 0x005110D4, AnimationStreamer::~AnimationStreamer 0x0057AF48, read |
| M5-023 | M5-animation | EQUIVALENT_IMPLEMENTATION | Native cancellation and emission sequencing of a cancelled streaming animation | AnimationStreamer::Abort 0x0057B3E0, SetStreamingAnimation 0x0057B174, InitStream 0x0057B674, Update 0x0057CE5C, read |
| M11-020 | M11-vision | EQUIVALENT_IMPLEMENTATION | Illumination normalisation of each marker's region before its corners are refined and it is decoded | DetectFiducialMarkers 0x00898760 per-marker loop 0x008990AA..0x008995A2, Quadrilateral<float>::ComputeBoundingRectangle<int> 0x0088A16C, ArrayToCvMat<u8> 0x00899B50, read |

