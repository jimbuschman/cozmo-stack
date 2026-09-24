# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **244 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 159 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 21 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 0 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 27 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
| COMPATIBILITY_POLICY | 24 | A deliberate product or platform decision this stack intends to keep: offline tools, the test harness, PC-side plumbing, or a stand-in the operator has to ask for. Not a place to put fidelity work that is hard. |
| HARDWARE_ONLY | 5 | No shipped artifact can settle it; only a robot, or a recording of the stock app, can. |
| BLOCKED_EXTERNAL | 8 | The answer lies in third-party code or data that is not in the package (Omron OKAO, the Wwise runtime DSP, the Acapela text-to-speech engine). |

## Where each subsystem stands

Four separate things, because one word cannot carry them. **Source read** means nothing is left that
reading the original would settle. **Built** means nothing the original is known to do is knowingly
not done here. Neither says the behaviour is faithfully reproduced: the last two columns are what
remains after both, and they do not go away by working harder on this repository.

| subsystem | records | to read | to build | blocked externally | needs hardware | source read | built |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| M1-transport — UDP transport and reliability | 38 | 0 | 27 | 0 | 1 | yes | no |
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
| M1-transport | INVENTORY_APPROVED | 4 | 0 | 0 | 0 |
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

**M1-002 — Frame header: 4-byte prefix 43 4F 5A 03, no CRC, then the 10-byte RE header; receive-side checks** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Wire/Frame.cs`
* effect: the robot rejects frames, or this stack accepts frames the app would drop
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B5 0x0062EFB6 adr -> bytes at 0x0062F088 = 43 4f 5a 03; 0x0062EFB8 movs r1,#4; 0x0062EFBE movs r0,#0 (SetDoesHeaderHaveCRC false); B6 HeaderPrefix::Set 0x008393D2 cmp r6,#5 (truncate to 4 with a warning); 0x0083941C str r6,[r4,#4] (length); R1 BuildHeader 0x00836AF8: cmp r2,#0xa; movw r2,#0x4552; strh; strb #1 at +2; type at +3; firstSeq/lastSeq/ackSeq u16 LE at +4/+6/+8; B17 UDP receive order: 0x0083A780 size >= prefix; 0x0083A80E memcmp of 4 bytes; 0x0083AAA8 ubfx r0,r7,#5,#1 and 0x0083A888 cmp.w fp,#1: a datagram with MSG_TRUNC set is dropped with AddRecvError(1) (0x0083A8C4..0x0083A8C8) and the loop keeps reading (0x0083AABA) [corrected C2]; 0x0083A888 CRC only if enabled; 0x0083A900 hand-off to ReliableTransport::ReceiveData; no source-address filter; R3 0x00837632 cmp r7,#0xa; prefix table 0x00837B0C = 52 45 01; failures are logged, counted with AddRecvError, and the raw datagram is forwarded as data (0x00837792..0x008377A0; B19, B20)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-003 — Message types 1..11, always-unreliable set, container types, dispatch by type** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Wire/ReliableMessageType.cs`
* effect: a message is sent with the wrong reliability or handled as the wrong type
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R5 IsValidMessageType 0x0083673C subs r1,r0,#1; cmp r1,#0xb; R6 0x00836708 subs r0,#5; cmp r0,#6; movs r1,#0x7d; lsr.w r0,r1,r0: always-unreliable {5,7,8,9,10,11}; R7 IsMutlipleMessagesType 0x00836722 subs r1,r0,#7; cmp r1,#3: containers {7,8,9}; R8 0x00835F36..0x00835F52: packed frame 7 reliable-only, 8 unreliable-only, 9 mixed; a single message keeps its own type (0x00835EB6, 0x00835EC4); R12 HandleSubMessage tbb 0x00837446, table 0x0083744A = 13 1a 2b 09 09 3e 06 06 06 06 5f: 1 OnConnectRequest, 2 OnConnected, 3 OnDisconnected then DeleteConnection, 4/5 deliver, 6 multipart, 7..10 nothing, 11 ReceivePing; type names are not in the binary: ReliableMessageTypeToString 0x00836730 returns one empty string for every type
* outstanding: dispatch by type is reproduced; R12 "3 = OnDisconnected then DeleteConnection" is not: the code shuts the whole transport down (repaired with M1-015, M1-038 in batch 2); then EXACT_SOURCE

**M1-005 — Reliable-transport tunables as RobotConnectionManager::Init sets them** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/TransportConstants.cs`
* effect: resend, ping, spacing and timeout timing differ from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B13 RobotConnectionManager::Init 0x0062EFCE..0x0062F076 sets: MaxPacketsToReSendOnAck 0, MaxPacketsToSendOnSendMessage 1, SendUnreliableMessagesImmediately 0, SendPacketsImmediately 0, SendAckOnReceipt 0, MaxPacketsToReSendOnUpdate 1, TrackAckLatency 1, SendSeparatePingMessages 0, MaxPingRoundTripsToTrack 10 (0x0062F074 movs r3,#0xa), TimeBetweenPingsInMS 33.3 (0x0062F028, 0x4040A666_66666666), MaxTimeSinceLastSend 32.3 (0x0062F03E, 0x40402666_66666666), TimeBetweenResendsInMS 33.3 (0x0062F04E), PacketSeparationIntervalInMS 2.0 (0x0062F056), ConnectionTimeoutInMS 5000.0 (0x0062F068 movt #0x40b3); ConfigureReliableTransport 0x0062F0C8 writes the identical set but has no BL/BLX/B.W caller in .text (manager scan): the previous evidence cited dead code; unset by Init, so the .data defaults stand: MinExpectedPacketAckTimeMS 1.0 (read 0x0083641C), MaxAckRoundTripsToTrack 100, MaxBytesFreeInAFullPacket 0 (read 0x0083634E); GetCurrentNetTimeStamp 0x008355F8: double milliseconds from steady_clock (ns / 0x3E8 at 0x00835644, * 0.001 at 0x0083564C)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-006 — Send queue, packing, resend choice and pacing** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableConnection.cs`
* effect: frames go out in a different order, size, number or timing from the app; unreliable data is resent or lost differently
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R2 ackSeq (conn+0x42) is read fresh into every built frame: 0x00836A62 ldrh.w r5,[r4,#0x42]; 0x00836E02; R20 PendingMessage 0x30 bytes, no retry counter: 0x008357DE..0x00835832; AddMessage 0x00835A38 movs r0,#0x30; R21 SendMessage: maxPayload = UDP MaxTotalBytesPerMessage - 10 (0x00836C78..0x00836C8C); an oversize unreliable message is forced reliable (0x00836C92); anything above maxPayload is type 6 (0x00836CD6, 0x00836D60); queued via AddMessage, then SendOptimalUnAckedPackets(1) (0x00836EB8..0x00836EDC); R25 SendOptimalUnAckedPackets: 2.0 ms spacing gate 0x008363C8..0x008363FC; start entry by smallest effective time 0x0083640E..0x008364A6, where a sent entry counts 33.3 earlier only if (lastRecv - 1.0) > 0 and it was sent before that (0x00836466, 0x00836478) [corrected C3]; IsPacketWorthSending 0x008364B2; now > effective + 33.3 at 0x008364B8..0x008364CE; R26 IsPacketWorthSending 0x008362FE..0x0083639A; its idle clause requires lastSend > 0 (0x00836316 vcmpe.f64 d0,#0; 0x0083631E ble) [corrected C3]; R27 SendUnAckedMessages: forward packing 0x00835DF2..0x00835E40 then backward 0x00835E56..0x00835EA2; seq-0 entries deleted after one send, seq entries kept with updated sent time 0x00835F72..0x0083604A; lastSend = now; R28 no per-message retry limit; the only give-up is the connection timeout (M1-015); R29 at most one packet per ReliableConnection::Update (0x00836592..0x0083659E), one per SendMessage (0x00836ECA..0x00836EDC), none on ack (0x00837820..0x00837828)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-007 — Receive path: in-order delivery only, no buffering, mixed frames still walked** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: messages are delivered twice, out of order, or dropped where the app would deliver them
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R4 reliable iff firstSeq or lastSeq non-zero: 0x0083764C; 0x008377B0; R9 sub-message [type u8][size u16 LE][payload]: build 0x00835F10..0x00835F24; parse 0x00837904, 0x0083791C; R10 container errors: bad sub-type AddRecvError(4) 0x0083790A..0x00837910; size overrun AddRecvError(1) 0x00837926; the rest of the frame is abandoned, earlier subs already delivered; R11 seq assignment on receive 0x0083792A..0x00837982; single message 0x0083798A..0x0083799C; R12 seq != 0 and != nextIn (+0x44) is dropped silently; equal advances nextIn: 0x0083742E..0x0083743C; R16 0x00837838 IsWaitingForAnyInRange: ackOut (+0x42) = lastSeq before the walk (0x00837848); out of range: type 9 still walked (0x008378EA cmp.w r8,#9), else AddRecvError(5) and dropped (0x00837A6A); R17 no receive buffer in the connection layout (ctor 0x00835900..0x00835984)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-008 — 17-byte ping payload, sent unreliable as type 0x0B** (live path)

* where: `cozmo-stack/src/Cozmo.Protocol/Wire/PingPayload.cs`
* effect: the robot misreads keep-alive pings
* rests on: the M1 candidate implementation; payload layout confirmed by the comparison and batch 1
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R30 SendPing 0x00835C00..0x00835C62: f64 time, u32 numPingsSent (+0x60, incremented first), u32 numPingsReceived (+0x64), u8 isReply; movs r1,#0xb; movs r2,#0x11; unreliable, flag 1; a request sets +0x58 = now; SendPing calls ReliableTransport::SendMessage (0x00835C58): AddMessage then SendOptimalUnAckedPackets(1), so the ping can go out on the same call, subject to the 2.0 ms gate [corrected C4]
* outstanding: the 17-byte layout, counters, type and send path are reproduced; the f64 time field is filled from the per-transport Stopwatch, not GetCurrentNetTimeStamp (called at 0x00835C0E), so its value differs until M1-005's clock is repaired in batch 2; then EXACT_SOURCE

**M1-009 — Multipart split and reassembly** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: large messages (animations, images) are split or rebuilt differently
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R22 split: chunk = maxNet - 12 (0x00836D7A sub.w r0,r8,#0xc); count = ceil(size/chunk) (0x00836D90..0x00836D98); prefix [idx u8 from 1][count u8] (0x00836E42, 0x00836E46); count is strb with no 255 check; size 0 still gives one part; R23 AddMessagePart 0x008358A0..0x008358E8: part < 3 bytes rejected; idx must equal expected with no reset on mismatch; part 1 sets total; payload from byte 2 appended; complete at idx == total, then Clear (HandleSubMessage 0x008374C6..0x00837504); no size bound
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-010 — Asynchronous 2 ms transport tick and ReliableTransport::Update order** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: resend and ping timing drift from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R35/B14 ctor: Dispatch::Create "RelTransport" priority 3 (0x008367C2); +0xA0=1, +0xA1=0 (0x008367DA); ChangeSyncMode(false) (0x008367E2) -> ScheduleCallback(2 ms) (0x00836896 movs r2,#2; 0x0083689E); lambda 0x008383CE; R34 ReliableTransport::Update 0x00837B9A..0x00837C92: mutex +0x24; UDP Update (vtable +0x28, receive drain); ReliableConnection::Update per connection; false if any timed out; the exact repeat semantics are M1-021
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-015 — Connection timeout 5 s, and how a lost or failed connection is reported** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: a dead link is noticed at a different time, or the game is told the wrong result
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R19 ReliableConnection::HasConnectionTimedOut: now > lastRecv (+0x50) + ConnectionTimeoutInMS (0x00836080..0x0083608C); on timeout ReceiveData(OnDisconnected, 0, addr), delete, Update false (0x00837BCC..0x00837C74); no frame sent; B22 0x00837C50..0x00837C56 OnDisconnected to the receiver; tick lambda sets +0xA1 (0x008383D4..0x008383DC); B31 HandleDisconnectMessage: +0xA1 -> reason 1 WifiTimeout (0x0062FA34 ldrb.w r0,[r1,#0xa1]); RemoveRobot(id, wasConnecting) -> 2 ConnectionRejected if the handshake was in progress, else 1 ConnectionFailure (0x0062FAEE..0x0062FAF8, 0x0052F23E..0x0052F244); B32 pending handshake: RobotConnectionResponse(result) and no RobotDisconnected; else RobotDisconnected and $session_id cleared; Robot deleted; the engine does not reconnect (0x0052DCC0..0x0052DD16, 0x0052F248..0x0052F302); this record was a COMPATIBILITY_POLICY saying the engine connect timeout had not been read; it has now been read
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-016 — Incoming ack processing** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableConnection.cs`
* effect: acked messages stay queued and are resent, or unacked ones are discarded
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R18 every valid frame from a known connection runs UpdateLastAckedMessage(ackSeq) (0x00837814, 0x00835AEA): +0x50 = now always (0x00835B9C); for ackSeq != 0 remove the front entry while ackSeq is within the pending seq range (0x00835B08..0x00835B96); resend on ack only if MaxPacketsToReSendOnAck (0); R43 the loop removes pending[0] each pass (0x00835B08..0x00835B0A; memmove 0x00835B3C), so an unsent seq-0 entry at the front is discarded with the acked entries
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-018 — Connections are created only by a connect request** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: frames from or to an unconnected peer are handled where the app drops them
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R13 receive: created only for type 1 or a container whose first sub-type is 1 (0x008377CE..0x008377FC, FindConnection 0x00837098 cmp r5,#1); otherwise "unconnected source", dropped, ack not processed (0x0083789A); send: created only for type 1, otherwise "unconnected destination" and nothing queued (SendMessage 0x00836C5A..0x00836C6E); new connection 0x250 bytes, nextOut 1, nextIn 1 (0x0083592A, 0x0083592C)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-019 — Transport entry points: SendData, Connect, FinishConnection, Disconnect, Start, Stop** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: the connect / disconnect frames on the wire differ from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R38 SendData type 4 reliable / 5 unreliable (0x008370EC); Connect clears +0xA1 and queues reliable type 1 flag 1 (0x0083710E); FinishConnection reliable type 2 flag 1 (0x0083712E); Disconnect closure 0x00837FFA: SendMessage type 3, then DeleteConnection (b.w 0x8d123c), so type 3 gets at most one send; R39 Start -> UDP StartClient/StartHost (0x0083808E); Stop -> UDP Stop*, ClearConnections, no frames (0x008380F6, 0x00837374); R40 receiver events ReceiveData(marker, 0, addr) with markers OnConnectRequest 0x01037598, OnConnected 0x01037594, OnDisconnected 0x0103759C (0x00837478..0x00837496); B21 RobotConnectionManager::Connect: Clear, address at RCD+0x30, RT->Disconnect(addr) (0x0062F576) then RT->Connect(addr) (0x0062F580), state 1 (0x0062F58A); the engine sends first; combining R13 and R37 with B21: the type 3 is sent only if a connection to that address already exists, and it precedes the type 1 because QueueAction (0x00836FF6) and QueueMessage (0x00836B66) post to the same FIFO queue
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-020 — Production runs the transport asynchronously; sync mode is unused** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: the tick owner and threading differ from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R36 with +0xA0 set, QueueMessage calls SendMessage directly and the owner must call Update (0x00836B42..0x00836B5C); B15 RobotConnectionManager::Update calls RT::Update only when +0xA0 is set (0x0062F1BE); SetReliableTransportRunMode 0x0062FE36 is reached only from game message ReliableTransportRunMode (callers 0x0069E000, 0x0069E150), and nothing in unity/scripts/csharp sends it beyond the generated definition (unity/scripts/csharp/MessageGameToEngine.cs:93)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-021 — The 2 ms Dispatch callback: fixed delay, first run after one period, never overlapping** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: the transport tick drifts, bunches or overlaps differently from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: G1.1 ScheduleCallback -> Queue vtable +0x14 -> WakeAfterRepeat (0x007FB93A, 0x007FBB1E..0x007FBB2A); G1.2 first time = steady_clock::now() + period x 1e6 ns (0x007FCEBA..0x007FCED6): the first run is one period after scheduling; G1.3/G1.4 task holder with repeat flag, pushed to a deferred queue sorted so back() is the earliest (0x007FCE40..0x007FCF60; AddTaskHolderToDeferredQueue 0x007FCD92..0x007FCE14; comparator 0x007FE2FA..0x007FE316); G1.6/G1.7 the deferred thread waits with wait_until<steady_clock> (0x007FC194), reads now (0x007FC1BC) and posts a copy of each due entry to the immediate queue (AddTaskHolder 0x007FC270); it never runs the function itself; G1.8 a repeating entry is re-added at that now + period (0x007FC344 repeat byte; 0x007FC352..0x007FC376; 0x007FC3C4): fixed delay from when it was seen due; missed periods are not added back; G1.9/G1.10 posted copies are never merged (0x007FCD50..0x007FCD66); one Execute thread runs them in order (Run 0x007FD0C6..0x007FD128), skipping a task whose handle has expired, so runs never overlap and a backlog runs back to back
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-022 — UDP socket: setup, ephemeral local port, send errors, receive loop, reopen** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: the robot sees a different source port, or the link behaves differently after an error
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B11 OpenSocket: getaddrinfo(NULL, port, AI_PASSIVE, SOCK_DGRAM, AF_INET) (0x00839ACA, 0x00839AD0, 0x00839AF2); socket (0x00839B8A); SO_BROADCAST=1 for AF_INET (0x00839D18); bind (0x00839D3A); EADDRINUSE only a warning (0x00839E70); no O_NONBLOCK, no buffer options; B12 local port: ctor 0xBAC9 (0x00839546) but Init stores 0 (0x0062EFEA str.w r5,[r6,#0x98]), so bind is ephemeral; RT->StartClient opens it (0x0062F07E, 0x0083808E, 0x0083AD58); B10 sendto flags 0 (0x0083A37C); partial send logs SentWrongNumBytes (0x0083A39A); failure: AddSendError(6) (0x0083A552), time at +0x88 (0x0083A5F4), no retry, no disconnect; non-IP addresses refused (0x0083A606); B16 receive loop while TryToReadMessage returns 1 (0x0083AD10..0x0083AD18); recvmsg MSG_DONTWAIT into 0x5C0 (0x0083AA66, 0x0083AA76 movs r3,#0x40); <= 0 stops (0x0083AA98); errno != EAGAIN warns (0x0083AAC4); ENOTCONN closes and, only if the close succeeded (0x0083AB2C cbz r0), reopens on +0x98, which CloseSocket has just set to 0xBAC9 = 47817 (0x0083AB22 cmp r0,#0x6b, 0x0083AB2E, 0x0083AB34; CloseSocket 0x00839694..0x0083969C on both paths); only the first open is ephemeral [corrected C1]; B38 close failures logged; CloseSocket resets fd to -1 and port to 0xBAC9 (0x00839694..0x0083969C)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-023 — Socket reset on every Android process network bind or unbind** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: the socket is reopened (on port 47817) at different times from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: G4.1/G4.2 the RCM ctor registers its UDP transport with WifiUtil (0x0062EDB4..0x0062EDC0); RegisterTransport binds handler 0x0083BAD1 to the static signal at 0x0105DF8C (0x0083B9AA..0x0083B9D4); G4.3 that signal has exactly one subscriber (xref scan; static ctor 0x004DD62E, emitters 0x0083BC3E and 0x0083BC86); G4.4 the handler acts only if fd (+0x94) >= 0 (0x0083BAD6..0x0083BADC), then ResetSocket (0x0083BAE0); G4.5 ResetSocket sets +0x9D (0x0083A258); the next UDP Update closes, reopens only if the close returned 1, and clears +0x9D either way (0x0083ACE8..0x0083AD18); G4.6/G4.7 emitted only by NativeBindNetworkCallback and NativeBindLollipopCallback (0x0083BC3C..0x0083BC88), never by NativeStatusCallback or NativeScanCallback (0x0083BB7C..0x0083BC08); G4.8..G4.10 Java: attemptNetworkBind emits only on a successful bindProcessToNetwork / setProcessDefaultNetwork; unbindFromNetwork always emits; bindToNetwork retries every 100 ms up to 10 times (sources/com/anki/util/WifiUtil.java:270-332, :381-404); G4.11/G4.12 the callers are the CozmoWifi receiver on Wi-Fi state changes (sources/com/anki/cozmo/CozmoWifi.java:28-85) and the Unity ping test on API >= 26 (unity/scripts/csharp/AndroidConnectionFlow.cs:95-153, :302, :310)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE. The trigger on this host is the policy M1-037 (decision D5)

**M1-024 — Engine tick 60 ms: arrivals drained FIFO and handed up once per tick** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: handlers see messages at other times or in other batches from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B24 CozmoInstanceRunner::Run: 60 ms period 0x3938700 ns (0x0065B3D2/0x0065B3D8), sleep to target, overtime/catchup logs; CozmoEngine::Update state 3 (tbb 0x004ED5D0): UpdateRobotConnection -> MessageHandler::ProcessMessages, then UpdateAllRobots (0x004ED62E, 0x004ED648); B25 ProcessMessages (0x0069D870) -> RobotConnectionManager::Update (0x0062F1FA) -> ProcessArrivedMessages drains the RCD queue FIFO (0x0062F3EA..0x0062F4BC, tbb 0x0062F434); then PopData until empty, no cap (0x0069D8C0); B20 RCD::ReceiveData drops the OnConnectRequest marker (0x0062E4BE); PushArrivedMessage maps OnConnected 2, OnDisconnected 3, else data, under the mutex (0x0062E274..0x0062E2C2); B26 HandleDataMessage: state != 2 drops "Connection not yet valid" (0x0062F728); source != robot address drops (0x0062F744)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-025 — Connect request from the game, the connected response, and DisconnectCurrent** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: a second connect, a response at the wrong time, or a local disconnect is handled differently
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B2 ConnectToRobot handler: robot 1 exists -> "Robot already connected", nothing (0x004ED022, 0x004ED026); else AddRobotConnection (0x004ED074), then AddRobot(1) at once (0x004ED07C); B23 HandleConnectionResponseMessage: state 1 -> 2 and reason 0 (0x0062F954, 0x0062F958, 0x0062F95E); otherwise "Got connection response at unexpected time"; B33 DisconnectCurrent: RT->Disconnect then QueueConnectionDisconnect (0x0062EF38..0x0062EF58); callers RCM dtor 0x0062EE98, fatal robotError 0x0069DACA, MessageHandler::Disconnect 0x0069DCF2, ExitSdkMode 0x0069E00A; B35 disconnect reasons: SleepPlacedOnCharger 4 (0x00606D1A), SetRobotDisconnectReason writes the byte (0x0069E01C)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-026 — App send path: every robot message is sent reliable, no flush hint, only when connected** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: messages go out unreliable, early, or flushed differently from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B28 MessageHandler::SendMessage ignores its reliable and hot arguments; sends only if initialised, state 2 and not filtered (0x0069DCFE..0x0069DD58); RobotConnectionManager::SendData refuses unless state 2 (0x0062F5A6) and calls RT->SendData(reliable 1 at 0x0062F5CE, flush 0 via 0x0062F5C2 movs r1,#0 / 0x0062F5CA strd r2,r1,[sp]); R42 the flush flag is stored at +0x2B and makes IsPacketWorthSending true (0x008362FE..0x0083639A), so app messages never force an early send
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-027 — Arrived-message filters and the fatal robotError disconnect** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: messages the app drops are handled, or a fatal robot error does not disconnect
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B27 no robot -> silent drop (0x0069D8D2..0x0069D8D8); size 0 -> error (0x0069D8E0); ShouldFilterMessage -> drop (0x0069D8EE); unpack size mismatch -> drop (0x0069D910); tag 0xD9 robotError (0x0069D916): fatal -> event, broadcast, ClearData, DisconnectCurrent and stop the loop (0x0069DAC4..0x0069DACA); non-fatal -> RobotErrorPassThrough then broadcast (0x0069D936)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-028 — Initial-connection handshake outcomes** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: the robot is accepted, rejected or reported with a different result from the app
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B29 RobotInitialConnection subscribes to factoryFirmwareVersion 0xD2 (0x0052D1B4), firmwareVersion 0xEE (0x0052D208), robotAvailable 0xC9 (0x0052D260); factoryFirmwareVersion -> 3; firmwareVersion: parse fail or FACTORY build -> 3, no robotAvailable yet -> 1, simulator -> 0 (0x0052D3BE, 0x0052D59E, 0x0052D4F2, 0x0052D7E4); OnNotified 3 -> reason 7, 4 -> reason 8, then SendConnectionResponse (0x0052DDAA..0x0052DDC4, 0x0052DE06); success: filter off (+0x2D), GetManufacturingInfo, then on mfgId 0xED store serial, HW version, colour, $session_id and send RobotConnectionResponse(Success) (0x0052DE6C, 0x0052DE7C, 0x0052E304..0x0052E3A2)
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE. The version/time comparison itself is M1-029

**M1-029 — Firmware version check against the shipped firmware header** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: a robot is accepted or refused as outdated where the app decides otherwise
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: G5.1..G5.6 HandleFirmwareVersion 0x0052D470: guard (0x0052D478..0x0052D484); JSON parse (0x0052D4BA); FACTORY build path (0x0052D4C2..0x0052D506); v = version, t = time (0x0052D51A..0x0052D536); expected E_v/E_t at +0x1C/+0x20 (0x0052D538); sim flag (0x0052D53E..0x0052D5B6); G5.9/G5.10 no robotAvailable and not sim -> result 1 (0x0052D7B8..0x0052D850); sim -> 0 (0x0052D7C6); G5.11 robotDev = v == t, appDev = E_v == E_t; if they differ -> 3 OutdatedFirmware (0x0052D7DE teq.w r0,r1; 0x0052D7E4 movs r0,#3); G5.12 unsigned: E_v == v -> 0; E_v > v -> 3; E_v < v -> 4 OutdatedApp (0x0052D8DA..0x0052D8E0; 0x0052D9A6..0x0052D9AC); time is not compared again; G5.14/G5.15 E_v/E_t are copied from RobotManager+0x84/+0x88 in AddRobot (0x0052EEF2/0x0052EEF8; ctor 0x0052D196); the RobotManager ctor zeroes them (0x0052E512, 0x0052E516); G5.16..G5.20 RobotManager::Init -> FirmwareUpdater::LoadHeader starts a loader thread (0x0052E7F8; pthread_create 0x0067692A) that reads config/engine/firmware/cozmo.safe and parses the JSON header in the first 0x800 bytes (0x00677C44..0x00677D34); ParseFirmwareHeader stores version -> +0x84, time -> +0x88 (0x0052EA36..0x0052EA9A); G5.21/G5.32..G5.37 Scope 1 is DataPlatformResourcesPath = persistentDataPath/cozmo/cozmo_resources (pathToResource 0x0084BE34 table 03 14 22 2f 41; unity/scripts/csharp/PlatformUtil.cs:5-13), extracted from the shipped assets (re-analysis/obb/assets/resources.txt:2075); the shipped header has version 2381, time 1546972025 (re-analysis/obb/assets/cozmo_resources/config/engine/firmware/cozmo.safe offsets 0-445); G5.22..G5.30 nothing orders the header load before AddRobot: the loader starts in cozmo_startup before the engine thread (0x006661EA, 0x0065B14E), and neither the ConnectToRobot handler nor AddRobot checks the load (0x004ED026..0x004ED1EC; loaded flag +0x18 unread on that path); G5.31 a missing, short or unparsable file leaves E_v = E_t = 0 for the session (0x006764E4, 0x00677C50..0x00677D28); G5.38..G5.40 no writer of RobotManager+0x84/+0x88 besides the ctor and ParseFirmwareHeader was found; the scan cannot prove absence (adjusted-base, register-offset, whole-object and untyped accesses are outside it); see decision D7
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE. Reproduce the mechanism (asynchronous load, values copied at AddRobot, 0/0 when not loaded), not a fixed 2381

**M1-030 — Message gating until the handshake validates the robot** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: the robot receives commands, or the engine acts on messages, before the app would allow it
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B30 while +0x2D is 0: robot -> engine only robotAvailable, factoryFirmwareVersion, firmwareVersion and otaAck pass (0x0052DC76..0x0052DC96); engine -> robot only shutdownRobot 0xA9 and otaWrite 0xAF (0x0052DCA8, 0x0052DCAE); tag names from 0x010343A0 / 0x01034740
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-031 — Idle-timeout disconnect** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: the link is dropped after a pause at a different time, or never
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: B34 StartIdleTimeout: deadline = now + disconnectTime_s if >= 0, keeping an earlier deadline (0x0052D030..0x0052D066); Cancel sets -1 (0x0052D06C); on expiry Update clears it and calls MessageHandler::Disconnect (0x0052CE6E..0x0052CE98); the reason is not set; driven from unity/scripts/csharp/PauseManager.cs:219, :289, :360
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-032 — A never-answered connection times out 5000 ms after it is created** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableConnection.cs`
* effect: a connect to an absent robot fails at a different time
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: G2.1/G2.2 the connection is created by the first type-1 SendMessage (0x00836C5A..0x00836C6E; FindConnection 0x00837098..0x008370C0); G2.3 the ReliableConnection ctor sets +0x50 = GetCurrentNetTimeStamp() (0x0083592E blx; 0x0083593C vstr d0,[r4,#0x50]); G2.5/G2.6 the only other store to +0x50 is UpdateLastAckedMessage (0x00835B9C), whose only caller is ReceiveData for a valid frame from a known connection (0x00837818); G2.7/G2.8 timed out when now > +0x50 + 5000.0, strictly (0x00836080..0x0083623E); every ReliableConnection::Update checks it (0x008365A4); G2.9 detection runs on the 2 ms tick in async mode (M1-020, M1-021); in async mode the 5 s start when the queued connect closure runs SendMessage, not at the Connect() call
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

**M1-035 — Async hand-off: sends and ticks FIFO on one thread** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: a send can overtake a tick or another send
* rests on: the M1 candidate implementation (uncommitted working tree on bcab556); not yet compared against re-analysis/inventory/M1-transport.md
* best authority: libcozmoEngine.so 3.4.0-1204
* evidence: R37 QueueMessage 0x00836B66..0x00836BE4 posts a closure to RelTransport; the closure takes the transport mutex and calls SendMessage with the posted time (0x00837DEA); QueueAction 0x00836FF6 posts to the same queue; wrapper 0x00837F48
* outstanding: confirm the code reproduces every row this record cites, or build what it does not; then EXACT_SOURCE

## What remains after both: blocked externally, or needing hardware

| id | subsystem | status | what | why it cannot be settled here |
| --- | --- | --- | --- | --- |
| M1-033 | M1-transport | HARDWARE_ONLY | Robot-side transport behaviour | observe on a robot: ping echo isReply, robot resend timing, acceptance of types 7/8/9 |
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
| M1-014 | M1-transport | COMPATIBILITY_POLICY | Host thread structure that realises the engine threading | the original: a RelTransport dispatch thread (M1-010, M1-035) and one engine thread draining arrivals every 60 ms (M1-024). Its socket reopen on ENOTCONN and its send-failure handling are M1-022, and handler isolation is M1-034. Only the host thread structure that reproduces those orders is policy |
| M1-034 | M1-transport | COMPATIBILITY_POLICY | Handler isolation, a deliberate departure: in the original a handler exception aborts the engine process | the original has no handler isolation: nothing on the dispatch path catches, and an exception in any message handler reaches std::terminate and aborts the engine process (rows G3.1..G3.17, cited in evidence). The replacement deliberately isolates handlers instead, by operator decision D6; this record exists so the departure stays visible and is never mistaken for reproduced behaviour |
| M1-036 | M1-transport | COMPATIBILITY_POLICY | Crash reporting after an engine-thread abort | the original installs Google Breakpad from CozmoActivity.onCreate when HOCKEYAPP_APP_ID is set (sources/com/anki/cozmo/CozmoActivity.java:50-53, 105-114; resources/AndroidManifest.xml:120-122); on SIGABRT it writes <dumps>/<APP_RUN_ID>.dmp and re-raises (0x00667A34..0x00667A8C, 0x0095107C..0x00951BD0). A Windows host has its own crash handling, so this stack uses it. Not pursued because they touch only crash output: bionic __assert2/abort (not in the package) and whether libunity/libmono install a SIGABRT handler above Breakpad at run time |
| M1-037 | M1-transport | COMPATIBILITY_POLICY | Host trigger for the socket reset | the original resets on every Android process network bind or unbind (M1-023, rows G4.1..G4.13), which a Windows host does not have. This stack raises the same reset from the host notification that its network addresses changed (System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged), the host event nearest to the route to the robot changing. The reset mechanism itself is M1-023 and is reproduced from source |
| M1-038 | M1-transport | COMPATIBILITY_POLICY | Stop processing a frame after a DisconnectRequest sub-message | in the original, HandleSubMessage deletes the connection on a type-3 sub-message (0x008374A0, DeleteConnection veneer 0x008D123C; 0x008374C2) and ReceiveData then keeps walking the frame through the saved, now freed, connection pointer ([sp+0x18] at 0x008378FC, 0x00837958): undefined behaviour that cannot be reproduced. This stack stops processing the frame after the DisconnectRequest instead |
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

