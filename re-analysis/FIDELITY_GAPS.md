# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **249 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 174 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 22 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 0 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 12 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
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
| M1-transport — UDP transport and reliability | 43 | 0 | 12 | 0 | 2 | yes | no |
| M2-protocol — CLAD messages and protocol helpers | 7 | 0 | 0 | 0 | 0 | yes | yes |
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
| M1-transport | INVENTORY_APPROVED | 20 | 0 | 0 | 3 |
| M2-protocol | UNREVIEWED | 7 | 1 | 0 | 0 |
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

**M1-001 — Robot address: 172.31.1.1 / 127.0.0.1, remote port 5551 physical / 5552 simulated** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/TransportConstants.cs`
* effect: the stack talks to the wrong address or port
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B1 unity/scripts/csharp/ConnectionFlowController.cs:199, :204, :673 and RobotEngineManager.cs:524-526: 172.31.1.1 physical, 127.0.0.1 simulator, one ConnectToRobot(ipAddress, isSimulated); B3 0x0069DE84 movw r2,#0x15b0 (5552); 0x0069DE92 ldrb r0,[r1,#0x10] (isSimulated); 0x0069DE98 movweq r2,#0x15af (5551); 0x0069DE9E TransportAddress(char const*,int); B4 0x0069D0CE cmp.w r6,#0x10000: kP_ROBOT_ADVERTISING_PORT is only logged; >= 0x10000 aborts MessageHandler::Init
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-015 — Connection timeout 5 s, and how a lost or failed connection is reported** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: a dead link is noticed at a different time, or the game is told the wrong result
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R19 ReliableConnection::HasConnectionTimedOut: now > lastRecv (+0x50) + ConnectionTimeoutInMS (0x00836080..0x0083608C); on timeout ReceiveData(OnDisconnected, 0, addr), delete, Update false (0x00837BCC..0x00837C74); no frame sent; B22 0x00837C50..0x00837C56 OnDisconnected to the receiver; tick lambda sets +0xA1 (0x008383D4..0x008383DC); B31 HandleDisconnectMessage: +0xA1 -> reason 1 WifiTimeout (0x0062FA34 ldrb.w r0,[r1,#0xa1]); RemoveRobot(id, wasConnecting) -> 2 ConnectionRejected when RCD state was 1 (the transport connect was never answered), else 1 ConnectionFailure, including a drop during the handshake [corrected C6] (0x0062FAEE..0x0062FAF8, 0x0052F23E..0x0052F244); B32 pending handshake: RobotConnectionResponse(result) and no RobotDisconnected; else RobotDisconnected and $session_id cleared; Robot deleted; the engine does not reconnect (0x0052DCC0..0x0052DD16, 0x0052F248..0x0052F302); this record was a COMPATIBILITY_POLICY saying the engine connect timeout had not been read; it has now been read; CC23/CB32: HandleDisconnectMessage ignores the message fields, captures the RCD state, writes reason 1 if the timed-out flag is set, emits DAS, resets the reason to 0, clears RCD, then RemoveRobot(id, state == 1) (0x0062FA2E..0x0062FAF8); RIC::HandleDisconnect answers with RobotConnectionResponse {result, 0, 0, -1, -1} unless the response was already sent (0x0052DCC0..0x0052DD1A); CB33/CC26: RemoveRobot skips the RobotDisconnected broadcast and the $session_id clear when HandleDisconnect answered; either way it deletes the Robot and its RIC (0x0052F248..0x0052F364)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-019 — Transport entry points: SendData, Connect, FinishConnection, Disconnect, Start, Stop** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: the connect / disconnect frames on the wire differ from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R38 SendData type 4 reliable / 5 unreliable (0x008370EC); Connect clears +0xA1 and queues reliable type 1 flag 1 (0x0083710E); FinishConnection reliable type 2 flag 1 (0x0083712E); Disconnect closure 0x00837FFA: SendMessage type 3, then DeleteConnection (b.w 0x8d123c), so type 3 gets at most one send; R39 Start -> UDP StartClient/StartHost (0x0083808E); Stop -> UDP Stop*, ClearConnections, no frames (0x008380F6, 0x00837374); R40 receiver events ReceiveData(marker, 0, addr) with markers OnConnectRequest 0x01037598, OnConnected 0x01037594, OnDisconnected 0x0103759C (0x00837478..0x00837496); B21 RobotConnectionManager::Connect: Clear, address at RCD+0x30, RT->Disconnect(addr) (0x0062F576) then RT->Connect(addr) (0x0062F580), state 1 (0x0062F58A); the engine sends first; combining R13 and R37 with B21: the type 3 is sent only if a connection to that address already exists, and it precedes the type 1 because QueueAction (0x00836FF6) and QueueMessage (0x00836B66) post to the same FIFO queue; CA14: with no connection, the Disconnect closure sends nothing ("unconnected destination", 0x00836CDC..0x00836D06) and DeleteConnection is a no-op (0x00837604..0x00837608); the closure time is 0.0 (0x0083800A); CA20/CA21: StartClient/StopClient (and Host) are posted through QueueAction (0x00837214, 0x008372A4, 0x0083731C, 0x008373AC); UDP StopClient closes via CloseSocket when fd >= 0 (0x0083AD66..0x0083AD70) and StartClient opens only when fd == -1 (0x0083AD4C..0x0083AD5C); CA22: FinishConnection is QueueMessage(type 2, flag 1) (0x0083712A..0x0083713A)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-024 — Engine tick 60 ms: arrivals drained FIFO and handed up once per tick** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: handlers see messages at other times or in other batches from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B24 CozmoInstanceRunner::Run: 60 ms period 0x3938700 ns (0x0065B3D2/0x0065B3D8), sleep to target, overtime/catchup logs; CozmoEngine::Update state 3 (tbb 0x004ED5D0): UpdateRobotConnection -> MessageHandler::ProcessMessages, then UpdateAllRobots (0x004ED62E, 0x004ED648); B25 ProcessMessages (0x0069D870) -> RobotConnectionManager::Update (0x0062F1FA) -> ProcessArrivedMessages drains the RCD queue FIFO (0x0062F3EA..0x0062F4BC, tbb 0x0062F434); then PopData until empty, no cap (0x0069D8C0); B20 RCD::ReceiveData drops the OnConnectRequest marker (0x0062E4BE); PushArrivedMessage maps OnConnected 2, OnDisconnected 3, else data, under the mutex (0x0062E274..0x0062E2C2); B26 HandleDataMessage: state != 2 drops "Connection not yet valid" (0x0062F728); source != robot address drops (0x0062F744); CD1..CD5: the tick is fixed rate on steady_clock: the first tick runs at once, the next target is the old target + 60 ms, it sleeps only if at least 1 us remains, overrun ticks run back to back, and when 240 ms or more behind it skips whole periods instead of running extra ticks (0x0065B3B8..0x0065B63E); CD6..CD11: the per-tick order: counters zeroed, UiMessageHandler::Update (game messages dispatched synchronously), then in state 3 BaseStationTimer::UpdateTime, UpdateRobotConnection (all robot-message handlers), NeedsManager::Update, UpdateAllRobots (Robot::Update, then the RobotState broadcast), then the audio controller; every engine timestamp in a tick is the tick start (0x004ED4DE..0x004ED6C4; 0x0084BC38..0x0084BCD4)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-025 — Connect request from the game, the connected response, and DisconnectCurrent** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: a second connect, a response at the wrong time, or a local disconnect is handled differently
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B2 ConnectToRobot handler: robot 1 exists -> "Robot already connected", nothing (0x004ED022, 0x004ED026); else AddRobotConnection (0x004ED074), then AddRobot(1) at once (0x004ED07C); B23 HandleConnectionResponseMessage: state 1 -> 2 and reason 0 (0x0062F954, 0x0062F958, 0x0062F95E); otherwise "Got connection response at unexpected time"; B33 DisconnectCurrent: RT->Disconnect then QueueConnectionDisconnect (0x0062EF38..0x0062EF58); callers RCM dtor 0x0062EE98, fatal robotError 0x0069DACA, MessageHandler::Disconnect 0x0069DCF2, ExitSdkMode lambda 0x0069E1EC..0x0069E1FA [corrected C7]; B35 disconnect reasons: SleepPlacedOnCharger 4 (0x00606D1A), SetRobotDisconnectReason writes the byte (0x0069E01C); CB2/CB6: ConnectToRobot logs "Connected to robot!" before any transport exchange, then NeedsManager::InitAfterConnection and DASPauseUploadingToServer(1); nothing goes to the game (0x004ED074..0x004ED114); CC18/CC19: RobotDisconnectReason values 0..11 (0x00775BEC, table 0x01033970) and every writer of RCM+0x38; its only reader is the DAS disconnect event (0x0062FAAA), so it changes no behaviour; CC27..CC30: a later ConnectToRobot builds everything afresh once robot 1 is gone; DisconnectCurrent queues type 3 + DeleteConnection and pushes one OnDisconnected marker, handled at the next RCM::Update in FIFO order (0x0062EF32..0x0062EF7A; 0x0062F3BC..0x0062F4BC); CB38: the app layer does not filter connection events by address; only data is compared against the robot address (0x0062F434; 0x0062F72C..0x0062F748); E7..E9: ExitSdkMode subscribers run UiMessageHandler, then MessageHandler (reason 6, DisconnectCurrent), then MovementComponent; an EnableAnimTracks it sends is posted behind the Disconnect action, finds no connection and is not sent (0x00660918..0x0063DE96; 0x0069E1DC..0x0069E1FE; CA12, CA14)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-026 — App send path: every robot message is sent reliable, no flush hint, only when connected** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: messages go out unreliable, early, or flushed differently from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B28 MessageHandler::SendMessage ignores its reliable and hot arguments; sends only if initialised, state 2 and not filtered (0x0069DCFE..0x0069DD58); RobotConnectionManager::SendData refuses unless state 2 (0x0062F5A6) and calls RT->SendData(reliable 1 at 0x0062F5CE, flush 0 via 0x0062F5C2 movs r1,#0 / 0x0062F5CA strd r2,r1,[sp]); R42 the flush flag is stored at +0x2B and makes IsPacketWorthSending true (0x008362FE..0x0083639A), so app messages never force an early send; CB29: MessageHandler::SendMessage returns 1 silently (no throw, no log) when uninitialised, not connected or filtered (0x0069DCFE..0x0069DD2C); callers decide; CD13: there is no per-tick batching of outgoing messages; each send posts to the transport at once (0x005134A4..0x005134B8; 0x00836B42..0x00836B66)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-027 — Arrived-message filters and the fatal robotError disconnect** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: messages the app drops are handled, or a fatal robot error does not disconnect
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B27 no robot -> silent drop (0x0069D8D2..0x0069D8D8); size 0 -> error (0x0069D8E0); ShouldFilterMessage -> drop (0x0069D8EE); unpack size mismatch -> drop (0x0069D910); tag 0xD9 robotError (0x0069D916): fatal -> event, broadcast, ClearData, DisconnectCurrent and stop the loop (0x0069DAC4..0x0069DACA); non-fatal -> RobotErrorPassThrough then broadcast (0x0069D936); CC12..CC17: RobotErrorReport {u32 code 0..5, bool fatal} (0x007D3E66..0x007D3F40); fatal is decided by the byte only (0x0069D930); before validation it is filtered (0x0069D8EE); fatal: event, broadcast, ClearData, DisconnectCurrent, no game PassThrough, no reason (0x0069D938..0x0069DAEA); non-fatal: event, RobotErrorPassThrough to the game, broadcast (0x0069DA36..0x0069DAAE); CC32..CC36: ProcessMessages exactly (0x0069D870..0x0069D9FC); a 1-byte message with a tag outside 0xB0..0xF5 unpacks as 1 byte and is broadcast (0x007B1ADC..0x007B1BCA)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-028 — Initial-connection handshake outcomes** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: the robot is accepted, rejected or reported with a different result from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B29 RobotInitialConnection subscribes to factoryFirmwareVersion 0xD2 (0x0052D1B4), firmwareVersion 0xEE (0x0052D208), robotAvailable 0xC9 (0x0052D260); factoryFirmwareVersion -> 3; firmwareVersion: parse fail or FACTORY build -> 3, no robotAvailable yet -> 1, simulator -> 0 (0x0052D3BE, 0x0052D59E, 0x0052D4F2, 0x0052D7E4); OnNotified 3 -> reason 7, 4 -> reason 8, then SendConnectionResponse (0x0052DDAA..0x0052DDC4, 0x0052DE06); success: filter off (+0x2D), GetManufacturingInfo, then on mfgId 0xED store serial, HW version, colour, $session_id and send RobotConnectionResponse(Success) (0x0052DE6C, 0x0052DE7C, 0x0052E304..0x0052E3A2); CB7..CB21: RIC state and subscriptions (0x0052D184..0x0052D28E); robotAvailable sets +0x2E (0x0052DC64); OnNotified outcomes (0x0052DDAA..0x0052DDFC); on Success +0x2D is set first, mfgId is subscribed, then GetManufacturingInfo is sent reliable with no timer or retry (0x0052DDD0..0x0052DE7C); the mfgId lambda stores serial, hw and colour (colour only if in {0,2,3,4}), sets $session_id and sends RobotConnectionResponse(Success) (0x0052E304..0x0052E3B2); SendConnectionResponse sets +0x10, releases the handles and broadcasts the 14-byte response, engine subscribers running synchronously (0x0052DF2E..0x0052DF64; 0x006625C6..0x006625FA); CB14: with no robotAvailable, SendConnectionResponse(1, 0) is called directly, setting no disconnect reason (0x0052D7B8..0x0052D850); CB31/CB34: there are no handshake timers or retries, and results 1, 3 and 4 keep the link and the Robot (0x0052DD98..0x0052DF80); CD15/CD16: GetManufacturingInfo follows an accepted firmwareVersion; after the response, the mfgId handler also queues ReadLabAssignmentsFromRobot and ConnectRobotToNeedsManager (0x0052E3AA, 0x0052E3B2); E1..E6: a second firmwareVersion before mfgId subscribes a second mfgId lambda and sends a second GetManufacturingInfo, but the first mfgId yields exactly one RobotConnectionResponse, because SendConnectionResponse unlinks both lambdas during the emit; a second mfgId reaches only the Robot's HandleRobotSetBodyID (0x0052DF2E..0x0052DFC8; emit 0x0069E27C..0x0069E2B2; unlink 0x0051D338..0x0051D3A4; 0x00532CD0..0x00532CDA)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE. The version/time comparison itself is M1-029

**M1-029 — Firmware version check against the shipped firmware header** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: a robot is accepted or refused as outdated where the app decides otherwise
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: G5.1..G5.6 HandleFirmwareVersion 0x0052D470: guard (0x0052D478..0x0052D484); JSON parse (0x0052D4BA); FACTORY build path (0x0052D4C2..0x0052D506); v = version, t = time (0x0052D51A..0x0052D536); expected E_v/E_t at +0x1C/+0x20 (0x0052D538); sim flag (0x0052D53E..0x0052D5B6); G5.9/G5.10 no robotAvailable and not sim -> result 1 (0x0052D7B8..0x0052D850); sim -> 0 (0x0052D7C6); G5.11 robotDev = v == t, appDev = E_v == E_t; if they differ -> 3 OutdatedFirmware (0x0052D7DE teq.w r0,r1; 0x0052D7E4 movs r0,#3); G5.12 unsigned: E_v == v -> 0; E_v > v -> 3; E_v < v -> 4 OutdatedApp (0x0052D8DA..0x0052D8E0; 0x0052D9A6..0x0052D9AC); time is not compared again; G5.14/G5.15 E_v/E_t are copied from RobotManager+0x84/+0x88 in AddRobot (0x0052EEF2/0x0052EEF8; ctor 0x0052D196); the RobotManager ctor zeroes them (0x0052E512, 0x0052E516); G5.16..G5.20 RobotManager::Init -> FirmwareUpdater::LoadHeader starts a loader thread (0x0052E7F8; pthread_create 0x0067692A) that reads config/engine/firmware/cozmo.safe and parses the JSON header in the first 0x800 bytes (0x00677C44..0x00677D34); ParseFirmwareHeader stores version -> +0x84, time -> +0x88 (0x0052EA36..0x0052EA9A); G5.21/G5.32..G5.37 Scope 1 is DataPlatformResourcesPath = persistentDataPath/cozmo/cozmo_resources (pathToResource 0x0084BE34 table 03 14 22 2f 41; unity/scripts/csharp/PlatformUtil.cs:5-13), extracted from the shipped assets (re-analysis/obb/assets/resources.txt:2075); the shipped header has version 2381, time 1546972025 (re-analysis/obb/assets/cozmo_resources/config/engine/firmware/cozmo.safe offsets 0-445); G5.22..G5.30 nothing orders the header load before AddRobot: the loader starts in cozmo_startup before the engine thread (0x006661EA, 0x0065B14E), and neither the ConnectToRobot handler nor AddRobot checks the load (0x004ED026..0x004ED1EC; loaded flag +0x18 unread on that path); G5.31 a missing, short or unparsable file leaves E_v = E_t = 0 for the session (0x006764E4, 0x00677C50..0x00677D28); G5.38..G5.40 no writer of RobotManager+0x84/+0x88 besides the ctor and ParseFirmwareHeader was found; the scan cannot prove absence (adjusted-base, register-offset, whole-object and untyped accesses are outside it); see decision D7
* outstanding: build the mechanism (asynchronous header load, expected values copied at AddRobot, 0/0 when not loaded, the comparison) and log its outcome; under policy M1-040 the outcome never refuses the robot; then EXACT_SOURCE

**M1-030 — Message gating until the handshake validates the robot** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: the robot receives commands, or the engine acts on messages, before the app would allow it
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B30 while +0x2D is 0: robot -> engine only robotAvailable, factoryFirmwareVersion, firmwareVersion and otaAck pass (0x0052DC76..0x0052DC96); engine -> robot only shutdownRobot 0xA9 and otaWrite 0xAF (0x0052DCA8, 0x0052DCAE); tag names from 0x010343A0 / 0x01034740; CB27/CC33: no RIC for the robot id means nothing is filtered (0x0052FA90..0x0052FAD2); a filtered receive is dropped silently before unpack (0x0069D8D2..0x0069D8F4); a filtered send returns 1 silently (0x0069DCFE..0x0069DD2C); CB30: validation happens at OnNotified(0), before mfgId; OnNotified(3/4) and RM::InitUpdateFirmware (0x0052F5C4..0x0052F6AC) clear it again
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-031 — Idle-timeout disconnect** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: the link is dropped after a pause at a different time, or never
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B34 StartIdleTimeout: deadline = now + disconnectTime_s if >= 0, keeping an earlier deadline (0x0052D030..0x0052D066); Cancel sets -1 (0x0052D06C); on expiry Update clears it and calls MessageHandler::Disconnect (0x0052CE6E..0x0052CE98); the reason is not set; driven from unity/scripts/csharp/PauseManager.cs:219, :289, :360; CC1..CC7: the idle component has two deadlines: faceOff (armed only after the first full robot state, robot+0x34E) and disconnect; earliest wins; Cancel sets both to -1; expiry sets 0.0, which blocks re-arming until a Cancel; sleep fires before disconnect in one Update (0x0052CC64..0x0052D0C4; 0x0052CE3C..0x0052CE98); CC8: the sleep half queues a go-to-sleep animation sequence (0x0052CEA2..0x0052CFBE), an interface to the animation layer (M5); CC10: deadlines are checked once per 60 ms tick in engine state 3
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-041 — Robot initialisation after a Success connection response, and the gates it opens** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: the robot is not synced or initialised, or is initialised in the wrong order or at the wrong time; state or animations are processed before the engine would
* rests on: not implemented: the candidate sends GetManufacturingInfo, SyncTime, InitController and block-pool setup unconditionally on the transport ConnectionResponse (CozmoRobot.cs ~261-275)
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: CD17/CB21: the Success RobotConnectionResponse reaches, synchronously and in this order: RobotEventHandler, VisionComponent, TracePrinter (0x006625C6..0x006625FA; 0x00663C74..0x00663CA8; 0x005259AC, 0x006501E4, 0x0053BCBA); CD18/CB22/CB23: RobotEventHandler (result 0) calls Robot::SyncTime: +0x29 = 0, history clear, then SyncTime {u32 BaseStationTimer ms, 0xC1A00000} reliable; only if that was sent, InitController; only if that was sent, ImageRequest {Stream, QVGA} and AbsoluteLocalizationUpdate {0, frameId, originId, 0, 0, 0}; a failed send warns and stops (0x005289BA..0x005289D2; 0x0051521E..0x005153AE); CD19: SyncTime is never retried; after 5.0 s without SyncTimeAck it warns "SyncTimeAckNotReceived" (0x00513BF6..0x00513C5A); SyncTimeAck sets +0x29 = 1 (0x005366A4..0x005366AC); CD20: RobotEventHandler then sets ready-to-stream (+0x2A) through an NVStorage on-idle callback, which runs once the NV request queue is empty (0x00528A5A..0x00528A6E; 0x00645C20..0x00645C32); CD22: TracePrinter sends SetAppRunID (16-byte UUID, 0xFF unless a platform id exists), then RequestCrashReports{0}, each crash report received asking for the next index up to 3 (0x0053D398..0x0053D430; 0x0053CA88..0x0053CA9E); CD23/CD12: robot state is dropped until time sync (0x0051293C..0x0051294E); Robot::Update does nothing past the idle component until the first full state is handled (+0x34E); the AnimationStreamer runs only when synced and ready to stream (0x00513BF2..0x00514470); CD30: nothing sends SendHeadAngleUpdate, setAccessoryDiscovery or SetRobotImageSendMode on this path (BL scan)
* outstanding: build it; VisionComponent's SetCameraParams (CD21) belongs to M3 and the NV reads (CD26) to the NV subsystem; with no NV requests in this stack the ready-to-stream gate opens when its queue is empty; then EXACT_SOURCE

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

