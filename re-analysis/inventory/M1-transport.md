# M1 transport inventory

**State: approved by the operator at Checkpoint 2 on 2026-09-24 and frozen with `python re-analysis/tools/fidelity.py --approve M1-transport`.** D1 to D4 were approved on 2026-09-23, and D5 to D7 on 2026-09-24. Any change to this file, or to an M1 record's title, authority, evidence, live_path, hardware_required or status (other than an IMPLEMENTATION_GAP being built), fails the checker until a new approval.

## Where this comes from

- **Source:** `libcozmoEngine.so` 3.4.0-1204 and the shipped `libc++_shared.so`, freshly disassembled with capstone. Also the decompiled Unity C#, the Java (`sources/`, `smali/`), the manifest, and the shipped assets. The tracked `re-analysis/disassembly/` files were not relied on.
- **Read-only extractor passes:**
  - part A: ReliableTransport / ReliableConnection, rows R1..R43;
  - part B: UDPTransport, RobotConnectionManager, MessageHandler, RobotInitialConnection and the engine loop, rows B1..B38;
  - a gap pass for the five RECOVERABLE_GAPs of the first pass, rows G1..G5. Where a G row answers a question an R or B row left open (R35, R41, R42, B18, B21, B22, B29, B38), the G row supersedes it.
- **The M1 C# was never evidence.** It was read only to learn which behaviours need answers.
- **Manager spot-checks:** a sample of citations was re-disassembled or re-read, and every one matched.
  - Part A and B:
    - BuildHeader 0x00836AF8, NextSequenceId 0x0083675A and the always-unreliable mask at 0x00836708;
    - the chunk size at 0x00836D7A, the dispatch table at 0x0083744A and the RE prefix at 0x00837B0C;
    - the 33.3 / 32.3 / 5000 constants;
    - the ports at 0x0069DE84, the prefix bytes at 0x0062F088 and Init at 0x0062EFB6..0x0062EFC6;
    - the SendData arguments at 0x0062F5C2..0x0062F5CE and the +0xA1 read at 0x0062FA34;
    - the 1472 buffer and MSG_DONTWAIT;
    - a full call-instruction scan showing that ConfigureReliableTransport has no caller.
  - Gap pass:
    - the ctor's +0x50 store at 0x0083593C and the repeat-flag branch at 0x007FC344;
    - the terminate helper at 0x004E39F8 and the only set_terminate call at 0x00667862;
    - the NEEDED list and the manifest's HOCKEYAPP_APP_ID;
    - the Wi-Fi handler at 0x0083BAD6 and the Java bind calls;
    - the version comparison at 0x0052D7DE / 0x0052D8DA / 0x0052D9A6;
    - the shipped header's version 2381 and time 1546972025;
    - the pathToResource table at 0x0084BE3E and resources.txt:2075.

## How to read the statuses

- **IMPLEMENTATION_GAP** means the behaviour is established from the source and the code has not yet been compared against it under this process (D2). Comparing the M1 candidate against these rows is the next step after approval. Each record then either becomes EXACT_SOURCE (the code already does it) or stays a gap until built.
- **HARDWARE_ONLY** means only a robot can answer it.
- **COMPATIBILITY_POLICY** means a deliberate choice this stack keeps.
- **No RECOVERABLE_GAP remains in M1.**

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M1-001 | IMPLEMENTATION_GAP | Robot address: 172.31.1.1 / 127.0.0.1, remote port 5551 physical / 5552 simulated | B1, B3, B4 |
| M1-002 | IMPLEMENTATION_GAP | Frame header: 4-byte prefix 43 4F 5A 03, no CRC, then the 10-byte RE header; receive-side checks | R1, R3, B5, B6, B17, B19 |
| M1-003 | IMPLEMENTATION_GAP | Message types 1..11, always-unreliable set, container types, dispatch by type | R5, R6, R7, R8, R12 |
| M1-004 | IMPLEMENTATION_GAP | Sequence ids 1..65534, wrap and in-range test | R14, R15 |
| M1-005 | IMPLEMENTATION_GAP | Reliable-transport tunables as RobotConnectionManager::Init sets them | tunables table, B13 |
| M1-006 | IMPLEMENTATION_GAP | Send queue, packing, resend choice and pacing | R2, R20, R21, R25, R26, R27, R28, R29 |
| M1-007 | IMPLEMENTATION_GAP | Receive path: in-order delivery only, no buffering, mixed frames still walked | R4, R9, R10, R11, R12, R16, R17 |
| M1-008 | IMPLEMENTATION_GAP | 17-byte ping payload, sent unreliable as type 0x0B | R30 |
| M1-009 | IMPLEMENTATION_GAP | Multipart split and reassembly | R22, R23 |
| M1-010 | IMPLEMENTATION_GAP | Asynchronous 2 ms transport tick and ReliableTransport::Update order | R34, R35, B14 |
| M1-011 | IMPLEMENTATION_GAP | Ping receive: counters, and only a ping marked as a reply measures the round trip | R31 |
| M1-012 | IMPLEMENTATION_GAP | Frame payload bound 1406 and the UDP send buffer | R24, B5, B7, B8, B9 |
| M1-013 | COMPATIBILITY_POLICY | Windows high-resolution timer realising the 2 ms and 60 ms periods | policy |
| M1-014 | COMPATIBILITY_POLICY | Host thread structure that realises the engine threading | policy |
| M1-015 | IMPLEMENTATION_GAP | Connection timeout 5 s, and how a lost or failed connection is reported | R19, R41, B22, B31, B32, B36 |
| M1-016 | IMPLEMENTATION_GAP | Incoming ack processing | R18, R43 |
| M1-017 | IMPLEMENTATION_GAP | Ping schedule and ReliableConnection::Update order | R32, R33 |
| M1-018 | IMPLEMENTATION_GAP | Connections are created only by a connect request | R13, B19 |
| M1-019 | IMPLEMENTATION_GAP | Transport entry points: SendData, Connect, FinishConnection, Disconnect, Start, Stop | R38, R39, R40, B21 |
| M1-020 | IMPLEMENTATION_GAP | Production runs the transport asynchronously; sync mode is unused | R36, B15 |
| M1-021 | IMPLEMENTATION_GAP | The 2 ms Dispatch callback: fixed delay, first run after one period, never overlapping | G1.1..G1.10, R35 |
| M1-022 | IMPLEMENTATION_GAP | UDP socket: setup, ephemeral local port, send errors, receive loop, reopen | B10, B11, B12, B16, B38 |
| M1-023 | IMPLEMENTATION_GAP | Socket reset on every Android process network bind or unbind | G4.1..G4.13, B18 |
| M1-024 | IMPLEMENTATION_GAP | Engine tick 60 ms: arrivals drained FIFO and handed up once per tick | B20, B24, B25, B26 |
| M1-025 | IMPLEMENTATION_GAP | Connect request from the game, the connected response, and DisconnectCurrent | B2, B23, B33, B35 |
| M1-026 | IMPLEMENTATION_GAP | App send path: every robot message is sent reliable, no flush hint, only when connected | R42, B28 |
| M1-027 | IMPLEMENTATION_GAP | Arrived-message filters and the fatal robotError disconnect | B27 |
| M1-028 | IMPLEMENTATION_GAP | Initial-connection handshake outcomes | B29 |
| M1-029 | IMPLEMENTATION_GAP | Firmware version check against the shipped firmware header | G5.1..G5.40, B29 |
| M1-030 | IMPLEMENTATION_GAP | Message gating until the handshake validates the robot | B30 |
| M1-031 | IMPLEMENTATION_GAP | Idle-timeout disconnect | B34 |
| M1-032 | IMPLEMENTATION_GAP | A never-answered connection times out 5000 ms after it is created | G2.1..G2.10 |
| M1-033 | HARDWARE_ONLY | Robot-side transport behaviour | part A question 4 |
| M1-034 | COMPATIBILITY_POLICY | Handler isolation, a deliberate departure: in the original a handler exception aborts the engine process | G3.1..G3.17 (policy, D6), B38 |
| M1-035 | IMPLEMENTATION_GAP | Async hand-off: sends and ticks FIFO on one thread | R37 |
| M1-036 | COMPATIBILITY_POLICY | Crash reporting after an engine-thread abort | G3.18..G3.22 (policy) |
| M1-037 | COMPATIBILITY_POLICY | Host trigger for the socket reset | policy (D5) |

## What changed from the previous M1 records

- **Every source-backed record moves from EXACT_SOURCE to IMPLEMENTATION_GAP** until the comparison confirms the code (D2). The old records said "reproduced" on evidence that was mostly a symbol name, and nothing checked the code against it.
- **Stronger evidence:**
  - M1-005 cited ConfigureReliableTransport, which is dead code; the values come from RobotConnectionManager::Init.
  - M1-006 ("send / resend / batching") named no algorithm; it now carries R20..R29.
  - M1-002 said "COZ"; the prefix is the 4 bytes 43 4F 5A 03.
- **M1-015 was a policy saying the engine connect timeout had not been read.** It has been read (R19, B22, B31, B32), and M1-015 now records it, including the ConnectionRejected / ConnectionFailure distinction.
- **M1-013 and M1-014 were policies claiming nothing on the wire depends on them.** The original has visible timing and socket behaviour here. It is now its own records to reproduce (M1-010, M1-021, M1-022, M1-024, M1-035), and the two policies are narrowed to the host timer and the host thread structure (D1).
- **New records** for behaviour no record covered:
  - ack processing (M1-016), the ping schedule (M1-017), connection creation (M1-018) and the connect/disconnect frames (M1-019);
  - async mode (M1-020), the tick semantics (M1-021), the UDP socket (M1-022) and the socket reset (M1-023);
  - the engine tick and hand-up (M1-024), the connect/disconnect lifecycle (M1-025) and the app send path (M1-026);
  - message filters (M1-027) and the initial-connection handshake (M1-028, M1-029, M1-030);
  - the idle timeout (M1-031) and the connect timeout (M1-032);
  - the FIFO hand-off (M1-035), plus one HARDWARE_ONLY (M1-033);
  - three policies from D5 and D6: handler isolation (M1-034), crash reporting (M1-036) and the host reset trigger (M1-037).
- **The five first-pass gaps are resolved by the G rows:**
  - M1-021: fixed-delay 2 ms, the first run after one period, no overlap, backlogs run back to back.
  - M1-023: the reset fires on every Android process network bind or unbind.
  - M1-029: only the version number is compared, against 2381 from the shipped header, and time only marks dev builds.
  - M1-032: a silent robot times out 5000 ms after the connection is created.
  - M1-034: nothing on the dispatch path catches, so a handler exception aborts the engine process. That is recorded in full, and the replacement departs from it by policy (D6).
- **Not recorded:** B37 (DAS telemetry, nothing on the wire or in behaviour).

## Decisions for the operator

Approved 2026-09-23:
- **D1.** Reproduce the original timing and socket behaviour; M1-013/M1-014 shrink to the host mechanism.
- **D2.** Everything source-backed is IMPLEMENTATION_GAP until the comparison confirms it.
- **D3.** The app-layer connection handshake belongs to M1.
- **D4.** Reproduce odd source behaviour as it is (R43, R22, B9).

Approved 2026-09-24:
- **D5.** Reproduce M1-023's reset mechanism behind a host trigger. The trigger is the policy M1-037: the host notification that its network addresses changed (`NetworkChange.NetworkAddressChanged`), standing in for Android's process network bind and unbind.
- **D6.** The original's fatal behaviour (a handler exception aborts the engine process) is preserved in the inventory: rows G3.1..G3.17, cited in M1-034. The replacement keeps handler isolation as a deliberate departure, so M1-034 is a COMPATIBILITY_POLICY that states what the original does and that this stack does otherwise. Crash reporting after any other abort is the policy M1-036.
- **D7.** Accept M1-029's two source-proof limits as documented:
  - the header load is not ordered before AddRobot in code;
  - absence of other writers is established only within the scan in G5.38..G5.40.
  Implement the mechanism exactly: load asynchronously at startup, copy the values when the robot is added, and use 0/0 when not loaded.

## Open before M1 can be accepted

- No RECOVERABLE_GAP remains.
- M1-033 (robot-side behaviour: ping echo isReply, robot resend timing, acceptance of types 7/8/9) needs a hardware run.
- Follow-up for the implementation pass: the LINK hardware check (`HardwareCatalog.cs`) must name M1-033, so that hardware-only uncertainty is visible in the check it affects.

## Engine tunables (RobotConnectionManager::Init), for M1-005



| static | default | store (Init / Configure) | value used |
|---|---|---|---|
| sSendUnreliableMessagesImmediately | 1 | 0x0062F008 / 0x0062F0E6 | 0 |
| sMaxPacketsToReSendOnAck | 1 | 0x0062EFF0 / 0x0062F0F6 | 0 |
| sSendAckOnReceipt | 1 | 0x0062F022 / 0x0062F102 | 0 |
| sMaxPacketsToSendOnSendMessage | 1 | 0x0062EFF6 / 0x0062F106 | 1 |
| sTimeBetweenPingsInMS | 250 | 0x0062F028 (double 0x4040A666_66666666) / 0x0062F10C | 33.3 |
| sTrackAckLatency | bss | 0x0062F034 / 0x0062F12A | 1 |
| sMaxTimeSinceLastSend | 49 | 0x0062F03E and 0x0062F040 (double 0x40402666_66666666) / 0x0062F136 | 32.3 |
| sSendSeparatePingMessages | 1 | 0x0062F06E / 0x0062F14E | 0 |
| sTimeBetweenResendsInMS | 50 | 0x0062F04E and 0x0062F050 / 0x0062F152 | 33.3 |
| sPacketSeparationIntervalInMS | bss | 0x0062F056 and 0x0062F05E / 0x0062F15E | 2.0 |
| sSendPacketsImmediately | 1 | 0x0062F01C / 0x0062F166 | 0 |
| sMaxPacketsToReSendOnUpdate | 3 | 0x0062F02E / 0x0062F170 | 1 |
| sConnectionTimeoutInMS | 5000 | 0x0062F070 / 0x0062F172 | 5000 |
| sMaxPingRoundTripsToTrack | 20 | 0x0062F076 / 0x0062F178 | 10 |

Statics with no writer:
- sMinExpectedPacketAckTimeMS stays 1.0; only reader 0x0083641C.
- sMaxAckRoundTripsToTrack stays 100; read only in the ctor.
- sMaxBytesFreeInAFullPacket is bss 0; only reader 0x0083634E.



## Part A rows: ReliableTransport / ReliableConnection

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| R1 header build | 10 bytes: 'R','E',0x01, type u8, firstSeq u16 LE, lastSeq u16 LE, ackSeq u16 LE. Returns 0 if the buffer is under 10. | BuildHeader 0x00836AF8 (see spot-check). Also inline at 0x00836A6A..0x00836A80 and 0x00836E06..0x00836E20. | M1-002 | EXACT_SOURCE |
| R2 ackSeq in every outgoing frame | conn+0x42, read fresh on every build or rebuild. | 0x00836A62 ldrh.w r5,[r4,#0x42]; 0x00836E02 | M1-006 | EXACT_SOURCE |
| R3 header validation | A datagram under 10 bytes, or with a bad 'R','E',0x01 prefix, is logged (TooSmall / BadPrefix), counted with AddRecvError (0 or 2, then 4), and the raw datagram is passed to the receiver's ReceiveData. | 0x00837632 cmp r7,#0xa; prefix table 0x00837B0C (loop 0x0083763C..0x00837648); pass-through 0x00837792..0x008377A0 | M1-002 | EXACT_SOURCE |
| R4 reliable-packet test | Reliable iff firstSeq != 0 or lastSeq != 0. | 0x0083764C; 0x008377B0 cmp.w sb,#0 | M1-007 | EXACT_SOURCE |
| R5 valid types | 1..11 | IsValidMessageType 0x0083673C subs r1,r0,#1; cmp r1,#0xb | M1-003 | EXACT_SOURCE |
| R6 always-unreliable | {5,7,8,9,10,11} | 0x00836708 | M1-003 | EXACT_SOURCE |
| R7 container types | {7,8,9} | IsMutlipleMessagesType 0x00836722 | M1-003 | EXACT_SOURCE |
| R8 packed frame type | 7 = reliable only, 8 = unreliable only, 9 = mixed. A single message keeps its own type with no sub-header. | 0x00835F36..0x00835F52; single case 0x00835EB6, 0x00835EC4 | M1-003 | EXACT_SOURCE |
| R9 sub-message framing | [type u8][size u16 LE][payload] | build 0x00835F10..0x00835F24; parse 0x00837904, 0x0083791C | M1-007 | EXACT_SOURCE |
| R10 container parse errors | A bad sub-type gives AddRecvError(4) and abandons the rest; a size overrun gives AddRecvError(1). Earlier subs have already been delivered. | 0x0083790A..0x00837910; 0x00837926; 0x00837A04; 0x00837A62 | M1-007 | EXACT_SOURCE |
| R11 seq assignment on receive | In a reliable container the running seq starts at firstSeq. Non-always-unreliable subs take it and it advances; always-unreliable subs get 0; subs of an unreliable packet get 0; a single message gets firstSeq. | 0x0083792A..0x00837982; 0x0083798A..0x0083799C | M1-007 | EXACT_SOURCE |
| R12 HandleSubMessage | seq != 0 and != nextIn(+0x44) is dropped silently; equal advances nextIn. Dispatch: 1 = OnConnectRequest; 2 = OnConnected; 3 = OnDisconnected then DeleteConnection; 4 and 5 = deliver; 6 = multipart; 7..10 = nothing; 11 = ReceivePing; above 11 = error. | 0x0083742E..0x0083743C; tbb 0x00837446, table 0x0083744A. Targets 0x837470, 0x83747E, 0x8374A0 (+veneer 0x8D123C DeleteConnection), 0x83745C, 0x8374C6, 0x837456, 0x837508 (veneer 0x8D125C ReceivePing). | M1-003, M1-007 | EXACT_SOURCE |
| R13 connection creation | Receive side: created only for type 1, or a container whose first sub is type 1; other frames from an unknown source are logged "unconnected source" and dropped, ack not processed. Send side: created only for type 1, else the warning "unconnected destination". New object is 0x250 bytes with nextOut=1, nextIn=1, ackOut=0. | 0x008377CE..0x008377FC; 0x00837878..0x008378B0; SendMessage 0x00836C5A..0x00836C6E; FindConnection 0x00837098; ctor 0x0083592A, 0x0083592C | M1-018 | EXACT_SOURCE |
| R14 outgoing seq ids | Next: 0xFFFE to 1, else +1, so ids run 1..65534 and 0 means none. Previous: 1 to 0xFFFE. GetNextOutSequenceNumber returns +0x40 and then increments. One id per reliable part. | 0x0083675A; 0x00836748; 0x00835A9C..0x00835AAA; 0x00836DE0..0x00836DF0 | M1-004 | EXACT_SOURCE |
| R15 in-range | last >= first: first <= id <= last. Wrapped: id >= first OR id <= last. | 0x0083676A..0x0083678C | M1-004 | EXACT_SOURCE |
| R16 reliable acceptance | If nextIn is in [first,last], +0x42 = lastSeq BEFORE the subs are walked. A standalone ack is sent only if sSendAckOnReceipt (0 in the engine). Then the subs are walked. If nextIn is not in range, a type-9 frame is still walked (unreliable subs delivered, reliable ones dropped by R12); any other type gets AddRecvError(5) and is dropped, not re-acked then. | 0x00837838 (IsWaitingForAnyInRange 0x00835AAE); 0x00837848; 0x0083784E..0x00837872; 0x008378EA cmp.w r8,#9; 0x00837A6A | M1-007 | EXACT_SOURCE |
| R17 no out-of-order buffering | Future reliable messages are not buffered; recovery is by the sender's resend. | R12, R16; ctor layout 0x00835900..0x00835984 | M1-007 | EXACT_SOURCE |
| R18 incoming ack | Every valid frame from a known connection runs UpdateLastAckedMessage(header ackSeq). It always stores now at +0x50. For ackSeq != 0 it loops while ackSeq is between the first and last pending seq != 0, removing the FRONT entry. Resend on ack only if sMaxPacketsToReSendOnAck (0). | 0x00837814; 0x00837820..0x0083782E; 0x00835AEA; loop 0x00835B08..0x00835B96; 0x00835B9C | M1-016 | EXACT_SOURCE |
| R19 connection timeout | now > lastRecv(+0x50) + 5000. On timeout: warning; ReceiveData(OnDisconnected,0,addr); connection deleted; ReliableTransport::Update returns false. No frame is sent. | 0x00836080..0x0083608C; 0x00836232..0x0083623E; 0x008365A8; 0x00837BCC..0x00837C74 | M1-015 | EXACT_SOURCE |
| R20 PendingMessage (0x30 bytes) | +0 extQueued, +8 created, +0x10 firstSent, +0x18 latestSent, +0x20 buf, +0x24 size, +0x28 seq, +0x2A type, +0x2B flag. No retry counter. | 0x008357DE..0x00835832; 0x0083583E; AddMessage 0x00835A38 | M1-006 | EXACT_SOURCE |
| R21 SendMessage | maxPayload = UDP MaxTotalBytesPerMessage - 10. An unreliable message above maxPayload is forced reliable (warning). Anything above maxPayload becomes type 6. Immediate send only if sSendUnreliableMessagesImmediately and unreliable (never in the engine); otherwise AddMessage. Then SendOptimalUnAckedPackets(1). | 0x00836C78..0x00836C8C; 0x00836C92; 0x00836CD6, 0x00836D60; 0x00836DA8..0x00836DBC; 0x00836DC0..0x00836EB4; 0x00836E84..0x00836E94; 0x00836EA8; 0x00836EB8..0x00836EDC | M1-006 | EXACT_SOURCE |
| R22 multipart split | Chunk = maxNet - 12 = 1404; count = ceil(size/chunk). Each part is prefixed [idx u8, 1-based][count u8], has its own seq and type 6. The count is written with strb and not checked against 255. Size 0 still gives one part. | 0x00836D7A; 0x00836D90..0x00836D98; 0x00836E42; 0x00836E46; 0x00836DA6 | M1-009 | EXACT_SOURCE |
| R23 multipart reassembly | A part under 3 bytes is rejected. idx must equal expected (+4, from 1); a mismatch is rejected with no reset. Part 1 sets total from byte 1; payload from byte 2 is appended. Complete at idx == total: deliver, then Clear(). No size bound. | AddMessagePart 0x008358A0..0x008358E8; 0x008374C6..0x00837504 (Clear veneer 0x8D124C) | M1-009 | EXACT_SOURCE |
| R24 frame size bound | Reliable maxPayload = UDP MaxTotalBytesPerMessage - 10, where UDP's value = sMaxNetMessageSize - prefix len - (CRC ? 2 : 0). With Init: 1420 - 4 - 0 - 10 = 1406. | 0x00836A36..0x00836A3E; UDP vtable 0x0103788C slot 9; 0x0083AF4A..0x0083AF56; Init 0x0062EFB8, 0x0062EFBE, 0x0062EFC6 movw #0x58c | M1-012 | EXACT_SOURCE, except the prefix length at sHeaderPrefix+4 (HeaderPrefix::Set 0x008393C9, part B) |
| R25 send gate / start choice | (a) Nothing within 2.0 ms of the last send (+0x48). (b) Start = entry with the smallest effective time, first index on ties; never-sent counts as now - (resend+1); sent counts as latestSent, or latestSent - 33.3 if sent before (lastRecv - 1.0). (c) IsPacketWorthSending must be true. (d) now > effective + 33.3. Then SendUnAckedMessages up to max times. | (a) 0x008363C8..0x008363FC; (b) 0x0083640E..0x008364A6; (c) 0x008364B2; (d) 0x008364B8..0x008364CE; loop 0x008364D2..0x008364E8 | M1-006 | EXACT_SOURCE |
| R26 worth sending | Any of: sSendPacketsImmediately; lastSend + 32.3 < now; the +0x2B flag on any entry from start; a sent entry with latestSent + 32.3 < now; the running sum of (size+3) reaching maxPayload - 0. | 0x008362FE..0x0083639A | M1-006 | EXACT_SOURCE |
| R27 packing / resend content | Pack forward from start (start is taken unconditionally), then extend BACKWARDS while it fits. Header seqs = min/max non-zero seq included. lastSend = now. seq != 0 entries get their sent-time updated and stay; seq 0 entries are deleted (an unreliable message is sent once). Returns the forward count. A resend is a re-pack, not a byte replay. | 0x00835DF2..0x00835E40; 0x00835E56..0x00835EA2; 0x00835EDE, 0x00835F64; 0x00835F72..0x0083604A; 0x0083604C | M1-006 | EXACT_SOURCE |
| R28 resend interval / retries / give-up | 33.3 ms, shortcut by R25b. No per-message retry limit. The only give-up is the 5 s timeout, which deletes the queue. | R20, R25, R19 | M1-006 | EXACT_SOURCE |
| R29 packets per call | At most 1 per ReliableConnection::Update, 1 per SendMessage, 0 on ack; all under the 2.0 ms spacing. | 0x00836592..0x0083659E; 0x00836ECA..0x00836EDC; 0x00837820..0x00837828 | M1-006 | EXACT_SOURCE |
| R30 ping payload | 17 bytes: f64 time (now, or echoed for a reply), u32 numPingsSent (+0x60, incremented first), u32 numPingsReceived (+0x64), u8 isReply. Unreliable type 0x0B, flag 1. A request sets +0x58 = now. Queued, not sent immediately. | SendPing 0x00835C00..0x00835C62 | M1-008 | EXACT_SOURCE |
| R31 ping receive | Under 17 bytes is ignored. numPingsReceived++; peer counters kept only if the incoming numPingsSent is greater. A reply records now - timeSent, negative included. A request is answered only if sSendSeparatePingMessages (never). | ReceivePing 0x00835C7C..0x00835D30 | M1-011 | EXACT_SOURCE |
| R32 ping schedule | With sSendSeparatePingMessages=0: only when the queue is empty, lastSend > 0, now > lastSend + 33.3, and now >= lastPing + 33.3. An idle keep-alive; nothing before the first frame. | 0x0083652E..0x00836590 | M1-017 | EXACT_SOURCE |
| R33 ReliableConnection::Update | Ping check, then SendOptimalUnAckedPackets(1), then return !HasConnectionTimedOut. | 0x00836518..0x008365A8 | M1-017 | EXACT_SOURCE |
| R34 ReliableTransport::Update | Takes the mutex at +0x24; UDP Update (vtable +0x28, the receive pump); each connection's Update; timeouts per R19; returns false if any timed out. | 0x00837B9A..0x00837C92 | M1-010 | EXACT_SOURCE |
| R35 async tick | The ctor sets +0xA0=1, then ChangeSyncMode(false) creates the Dispatch queue "RelTransport" (priority 3) with ScheduleCallback(2 ms), which goes through Queue vtable slot 0x14 to TaskExecutor::WakeAfterRepeat. The lambda runs Update and sets +0xA1=1 on false. | 0x008367BE..0x008367E2; 0x00836896; lambda 0x008383CE; 0x007FB93A; Queue vtable 0x010357B8 slot 5 = 0x007FBAF8, WakeAfterRepeat at 0x007FBB2A | M1-010, M1-021 | EXACT_SOURCE that a 2 ms repeating callback is requested. RECOVERABLE_GAP for repeat semantics (WakeAfterRepeat 0x007FCE35, ProcessDeferredQueue 0x007FC0C9). |
| R36 sync mode | With +0xA0=1, QueueMessage calls SendMessage directly and no timer exists, so the owner must call Update. The switch is SetReliableTransportRunMode 0x0062FE36, which calls ChangeSyncMode. | 0x00836B42..0x00836B5C; 0x0062FE36 | M1-020 | EXACT_SOURCE for the mechanism; which mode production uses is part B |
| R37 async hand-off | Messages are posted as closures to RelTransport; each takes the same mutex and calls SendMessage with the posted time. FIFO on one thread. | QueueMessage 0x00836B66..0x00836BE4; closure 0x00837DEA; QueueAction 0x00836FF6; 0x00837F48 | M1-035 | EXACT_SOURCE |
| R38 public entry points | SendData: type 4 if reliable, else 5. Connect: clears +0xA1, queues reliable type 1 with flag 1. FinishConnection: reliable type 2, flag 1. Disconnect: sends reliable type 3, then deletes the connection immediately (at most one send, which the 2 ms gate can block; never resent). | 0x008370EC; 0x0083710E; 0x0083712E; Disconnect closure 0x00837FFA (movs r3,#3; SendMessage; b.w 0x8d123c) | M1-019 | EXACT_SOURCE |
| R39 start/stop | Start calls UDP StartClient/StartHost. Stop calls UDP Stop*, then ClearConnections; no frames are sent. | 0x0083808E; 0x008380F6 + veneer 0x8D122C; 0x00837374 | M1-019 | EXACT_SOURCE |
| R40 receiver interface | INetTransportDataReceiver* at +4. Events arrive as ReceiveData(marker,0,addr) with markers OnConnectRequest 0x01037598, OnConnected 0x01037594, OnDisconnected 0x0103759C. The +8 subobject is registered as the UDP receiver. | 0x00837478..0x00837496; 0x008367E6..0x008367E8 | M1-019 | EXACT_SOURCE |
| R41 +0xA1 flag | Set by the tick lambda on timeout, cleared by Connect. No reader found in scope. | 0x008383DC; 0x0083710E | M1-015 (reader found in B36) | RECOVERABLE_GAP (part B) |
| R42 SendData flag | Stored at +0x2B; makes IsPacketWorthSending true (a flush hint). The value the app passes is outside scope. | R20, R26 | M1-026 (value found in B28) | part B interface |
| R43 ack removes front | UpdateLastAckedMessage removes pending[0] each pass, so an unsent seq-0 entry at the front is discarded with the acked ones. | 0x00835B08..0x00835B0A; memmove 0x00835B3C | M1-016 | EXACT_SOURCE |

## Part B rows: UDP transport and connection lifecycle

Object layout used by the citations:
- RobotConnectionManager: +0x10 RobotConnectionData, +0x14 UDPTransport, +0x18 ReliableTransport, +0x38 disconnect reason.
- RobotConnectionData: +4 state (0 none, 1 connecting, 2 connected), +0x30 robot address.
- ReliableTransport: +0xA0 sync-mode flag, +0xA1 timed-out flag.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| B1 | Unity picks the IP: 172.31.1.1 physical, 127.0.0.1 simulator. It sends one ConnectToRobot (ipAddress[16], isSimulated). | unity/scripts/csharp/ConnectionFlowController.cs:199, :204, :673; RobotEngineManager.cs:524-526 | M1-001 | EXACT_SOURCE |
| B2 | ConnectToRobot handler: if robot 1 exists, log "Robot already connected" and do nothing. Otherwise AddRobotConnection, then create Robot 1 immediately, before any handshake. | 0x4ed022; 0x4ed026 DoesRobotExist; 0x4ed074 AddRobotConnection; 0x4ed07a; 0x4ed07c AddRobot | M1-025 | EXACT_SOURCE |
| B3 | Remote port 5552 if isSimulated (+0x10), else 5551; TransportAddress built from the ipAddress string. | 0x69de84; 0x69de92; 0x69de98; 0x69de9e | M1-001 | EXACT_SOURCE |
| B4 | kP_ROBOT_ADVERTISING_PORT is only logged. If it is ≥ 0x10000, Init logs an error and returns with no RCM. | 0x69d0c8; 0x69d0ce cmp.w r6,#0x10000; 0x69d0e2; RCM ctor only at 0x69d3bc | M1-001 | EXACT_SOURCE |
| B5 | RCM::Init: prefix 43 4F 5A 03 (4 bytes), CRC off, sMaxNetMessageSize 0x58C. | 0x62efb6 adr → 0x62f088; 0x62efb8; 0x62efbe; 0x62efc6 | M1-002, M1-012 | EXACT_SOURCE |
| B6 | HeaderPrefix::Set: length ≥ 5 warns and truncates to 4; memcpy to +0; length stored at +4. | 0x8393d2 cmp r6,#5; 0x839410; 0x839418; 0x83941c str r6,[r4,#4] | M1-002 | EXACT_SOURCE |
| B7 | Defaults before Init: CRC=1, sMaxNetMessageSize=0x5C0, prefix length 0 (.bss). Init is the only caller of the three setters. | .data 0x1051f74=01; 0x1051f78=c0050000; 0x105df14 .bss; callers 0x62efba/0x62efc2/0x62efca | M1-012 | EXACT_SOURCE |
| B8 | UDP MaxTotalBytesPerMessage = 1420 − 4 (−2 if CRC) = 1416; RT subtracts 10 = 1406. Users: SendUnAckedMessages, IsPacketWorthSending. GetMaxNetMessageSize and GetHeaderSize have no callers. | 0x83af50; 0x83af56; 0x836a3e; callers 0x835dce, 0x83633c | M1-012 | EXACT_SOURCE |
| B9 | The send buffer is a fixed 1472-byte stack buffer. If prefix + payload > 1472, BuildPacket returns 0 and sendto still sends a 0-length datagram; nothing is logged. | 0x83a34e; 0x83a2c8/0x83a2cc; 0x83a372; 0x83a386; 0x83a38c | M1-012 | EXACT_SOURCE |
| B10 | sendto flags 0. A partial send logs "SentWrongNumBytes". On failure: AddSendError(6), a warning if verbose, time at +0x88; no retry, no disconnect. Only IPv4/IPv6 are sent; anything else logs "UDP can only send to IP addresses!". | 0x83a37c; 0x83a39a; 0x83a552; 0x83a5f4; 0x83a606 | M1-022 | EXACT_SOURCE |
| B11 | OpenSocket: close the previous socket; getaddrinfo(NULL, port, AI_PASSIVE, SOCK_DGRAM, AF_INET); socket; for AF_INET, SO_BROADCAST=1; bind. EADDRINUSE is a warning only. No O_NONBLOCK, no buffer options. | 0x62ed56; 0x839a5a; 0x839aca; 0x839ad0; 0x839af2; 0x839b8a; 0x839d18/0x839d1a; 0x839d3a; 0x839e70 | M1-022 | EXACT_SOURCE |
| B12 | The ctor sets local port +0x98 = 0xBAC9, but Init sets it to 0, so the bind is ephemeral on INADDR_ANY. Init's tail call RT->StartClient → UDPTransport::StartClient → OpenSocket if fd == −1. | 0x839546; 0x62efe2; 0x62efea; 0x62f07e; 0x83808e; 0x83ad58 | M1-022 | EXACT_SOURCE |
| B13 | Init sets the 14 reliable tunables (see the part A table). ConfigureReliableTransport writes the same set but is dead code. | 0x62efce–0x62f076; 0x62f0c8, no callers | M1-005 | EXACT_SOURCE (values); the record cites the wrong function |
| B14 | RT ctor: Dispatch "RelTransport" at priority 3; +0xA0=1, +0xA1=0; ChangeSyncMode(false) → repeating 2 ms callback. Each tick: RT::Update → UDP Update (drain) → ReliableConnection::Update per connection. | 0x8367c2; 0x8367da; 0x8367e2; 0x836896; 0x83689e; 0x8383ce; 0x837ba2 | M1-010 | EXACT_SOURCE |
| B15 | RCM::Update calls RT::Update only when +0xA0 is set; false → reason 1. Sync mode is reachable only through game message ReliableTransportRunMode (tag 80), which nothing in Unity sends, so production is async. | 0x62f1be; 0x62f1c4; 0x62f1ca/0x62f1cc; callers of 0x62fe36: 0x69e000, 0x69e150; MessageGameToEngine.cs:93 | M1-020 | EXACT_SOURCE (the Unity absence is a text search) |
| B16 | The receive loop runs while TryToReadMessage returns 1: recvmsg(MSG_DONTWAIT) into 0x5C0; ≤ 0 stops (a 0-byte datagram too). errno ≠ EAGAIN warns "ReadFailed"; ENOTCONN closes and reopens the socket on port +0x98 (0, so ephemeral). | 0x83ad10–0x83ad18; 0x83aa66; 0x83aa76; 0x83aa98; 0x83aac4; 0x83ab22; 0x83ab34 | M1-022 | EXACT_SOURCE |
| B17 | Receive checks in order: size ≥ prefix length (+2 with CRC), else TooSmall; memcmp of 4 bytes, else BadPrefix; MSG_TRUNC must be set, else Recv.Truncated; CRC if enabled. Then payload + source go to RT::ReceiveData. No source filter at this layer. | 0x83a780; 0x83a80e; 0x83aaa8 ubfx r0,r7,#5,#1; 0x83a888; 0x83a8ea; 0x83a900 | M1-002 | EXACT_SOURCE |
| B18 | The RCM ctor registers the UDP transport with WifiUtil; a signal handler calls ResetSocket (+0x9D=1); the next Update closes and reopens on an ephemeral port. The trigger event was not read. | 0x62edb8; 0x83bae0; 0x83a258; 0x83ace8–0x83acfe | M1-023 | EXACT_SOURCE for the reset; RECOVERABLE_GAP for the trigger (0x83ba34, WifiUtil::Native*Callback 0x83bb7d...) |
| B19 | RT::ReceiveData where it meets part B: under 10 bytes or a non-"RE\x01" start → forwarded raw to RCD::ReceiveData as Data. Otherwise FindConnection(addr, create), where create means type 1 or a multi-message with first sub type 1. No connection → "unconnected source", dropped. | 0x837632; 0x83763c–0x837648; 0x837792–0x8377a0; 0x8377ce–0x8377f4; 0x8377fc; 0x83789a | M1-002, M1-018 | EXACT_SOURCE |
| B20 | RCD::ReceiveData drops the OnConnectRequest sentinel. PushArrivedMessage maps OnConnected→2, OnDisconnected→3, else Data (0, bytes copied), timestamped, under the mutex. HandleConnectionRequestMessage is unreachable. | 0x62e4be; 0x62e274/0x62e280/0x62e28c; 0x62e2ba–0x62e2c2; 0x62e296; 0x62fbbe | M1-024 | EXACT_SOURCE |
| B21 | RCM::Connect(addr): clear RCD, address at +0x30, RT->Disconnect(addr), RT->Connect(addr), state 1. RT::Disconnect queues an action: SendMessage(1, addr, null, 0, type 3, 1) then DeleteConnection. RT::Connect clears +0xA1 and queues QueueMessage(1, addr, null, 0, type 1, 1). The engine sends first. | 0x62f55e; 0x62f568; 0x62f576; 0x62f580; 0x62f58a; 0x83719a; 0x838006–0x83802a; 0x83710e; 0x83711c | M1-019 | EXACT_SOURCE here; UNKNOWN whether the type-3 is sent with no connection, and the queue order (part A) |
| B22 | There is no connect retry or timer in part B. Failure: RT::Update sees ReliableConnection::Update false → "Disconnecting TimedOut Connection" → ReceiveData(OnDisconnected) → delete → false; the lambda sets +0xA1=1. When the timeout applies to a never-answered connection is part A. | 0x837bcc; 0x837c0e; 0x837c50–0x837c56; 0x837c60; 0x8383d4–0x8383dc | M1-015 | EXACT_SOURCE (part B) |
| B23 | HandleConnectionResponseMessage: state 1 → state 2, reason 0; otherwise error "Got connection response at unexpected time". | 0x62f954; 0x62f958; 0x62f95e | M1-025 | EXACT_SOURCE |
| B24 | cozmo_startup → StartRun → std::thread(CozmoInstanceRunner::Run), with a 60 ms period (0x3938700 ns), sleep until target, "overtime"/"catchup" logs. State 3: UpdateRobotConnection → MessageHandler::ProcessMessages, then UpdateAllRobots. State 4: UpdateRobotConnection + UpdateFirmware. | 0x6661ea; 0x65b14e; 0x65b3d2/0x65b3d8; 0x65b420; tbb 0x4ed5d0 (77 03 10 27 42); 0x4ed62e; 0x4ed648; 0x4ed658; 0x52f854 | M1-024 | EXACT_SOURCE |
| B25 | Per tick: ProcessMessages (if +0x28) → RCM::Update → ProcessArrivedMessages drains the RCD queue FIFO (0 → HandleDataMessage; 1/2/3 → handlers). Then PopData loops RCM+0x20 until empty, with no cap. | 0x69d870; 0x69d87c; 0x62f1fa; 0x62f3ea–0x62f4bc; tbb 0x62f434 (02 2b 2f 33); 0x69d8c0 | M1-024 | EXACT_SOURCE |
| B26 | HandleDataMessage: state ≠ 2 → "Connection not yet valid, dropping message"; source ≠ +0x30 → "Expected messages from %s ... Dropping"; else push onto RCM deque. | 0x62f728; 0x62f744; 0x62f752; 0x62f768; 0x62f7de | M1-024 | EXACT_SOURCE |
| B27 | Dispatch: no robot → silent drop; size 0 → error; ShouldFilterMessage → drop; unpack size mismatch → error and drop. Tag 0xD9 robotError: fatal → event, Broadcast, ClearData, DisconnectCurrent, loop exits; non-fatal → RobotErrorPassThrough, then Broadcast. Otherwise Broadcast. | 0x69d8d2–0x69d8d8; 0x69d8e0; 0x69d8ee; 0x69d910; 0x69d916; 0x69d936; 0x69dac4/0x69daca; 0x69dab4 | M1-027 | EXACT_SOURCE |
| B28 | MessageHandler::SendMessage ignores its reliable/hot arguments. It sends only if initialised, state 2 and not filtered, else returns 1. It packs, then RCM::SendData (state 2 or "NotValidState"), then RT->SendData(reliable=1, addr, buf, size, flush=0). | 0x69dcfe; 0x69dd12; 0x69dd20; 0x69dd58; 0x62f5a6; 0x62f5ca; 0x62f5ce; 0x62f5d2 | M1-026 | EXACT_SOURCE |
| B29 | RobotInitialConnection subscribes to factoryFirmwareVersion (0xD2), firmwareVersion (0xEE) and robotAvailable (0xC9). robotAvailable sets +0x2E. factoryFirmwareVersion → 3. firmwareVersion: parse fail → 3; FACTORY build → 3; no robotAvailable yet → 1 (ConnectionFailure); sim → 0; otherwise version/time → 0, 3 (OutdatedFirmware) or 4 (OutdatedApp). OnNotified: 3 → reason 7, 4 → reason 8, then SendConnectionResponse. On 0: +0x2D (filter off), GetManufacturingInfo, wait for mfgId (0xED); the mfgId handler stores serial/HW/colour and $session_id, then RobotConnectionResponse(Success, fw, serial, hw, colour). | 0x52d1b4; 0x52d208; 0x52d260; 0x52dc66; 0x52d3be; 0x52d59e; 0x52d4f2; 0x52d7e8–0x52d84c; 0x52d7e4; 0x52d9a6–0x52d9ac; 0x52ddaa–0x52ddc4; 0x52de06; 0x52de6c; 0x52de7c; 0x52e304; 0x52e38a; 0x52e3a2 | M1-028, M1-029 | EXACT_SOURCE for outcomes; RECOVERABLE_GAP for the version/time test at 0x52d7c6–0x52d8e2 and the expected values (+0x1C/+0x20, RobotManager::AddRobot 0x52eb70) |
| B30 | Before validation (+0x2D == 0), robot → engine passes only robotAvailable, factoryFirmwareVersion, firmwareVersion and otaAck; engine → robot passes only shutdownRobot (0xA9) and otaWrite (0xAF). | 0x52dc76–0x52dc96; 0x52dca8; 0x52dcae; tag tables 0x10343a0/0x1034740 | M1-030 | EXACT_SOURCE |
| B31 | HandleDisconnectMessage: +0xA1 → reason 1; DAS disconnect_reason with battery; reason reset to 0; clear RCD; RemoveRobot(id, wasConnecting) → RobotInitialConnection::HandleDisconnect(2 ConnectionRejected if the handshake was in progress, else 1 ConnectionFailure). | 0x62fa34; 0x62fa3c; 0x62fab8; 0x62fae2; 0x62fae6; 0x62faee–0x62faf8; 0x52f23e–0x52f244 | M1-015 | EXACT_SOURCE |
| B32 | If the response is still pending, the game gets RobotConnectionResponse(result) and no RobotDisconnected; otherwise RobotDisconnected and $session_id is cleared. NeedsManager/PerfMetric are notified; the Robot is deleted. No reconnect. Unity: Failure → ReturnToSearch; Rejected → a user Retry. | 0x52dcc0–0x52dd16; 0x52f248; 0x52f2c4; 0x52f2d8; 0x52f2e0–0x52f302; ConnectionFlowController.cs:697-702, :718-722, :754 | M1-015 | EXACT_SOURCE |
| B33 | DisconnectCurrent: RT->Disconnect(addr), QueueConnectionDisconnect, stats if count ≥ 1; the next ProcessArrivedMessages runs B31/B32. Callers: RCM dtor, fatal robotError, MessageHandler::Disconnect, ExitSdkMode (reason 6 first). | 0x62ef38/0x62ef52; 0x62ef58; callers 0x62ee98, 0x69daca, 0x69dcf2, 0x69e00a–0x69e012 | M1-025 | EXACT_SOURCE |
| B34 | Idle timeout: StartIdleTimeout sets the deadline to now + disconnectTime_s (if ≥ 0), keeping an earlier one; Cancel sets −1; on expiry Update clears it and calls MessageHandler::Disconnect. The reason is not set. | 0x52d030–0x52d066; 0x52d06c; 0x52ce6e–0x52ce98; PauseManager.cs:219, :289, :360 | M1-031 | EXACT_SOURCE |
| B35 | Reasons: BehaviorReactToOnCharger → 4; game SetRobotDisconnectReason writes the byte. The enum is from RobotDisconnectReason.cs. | 0x606d1a; 0x606d20; 0x69e01c | M1-025 | EXACT_SOURCE |
| B36 | +0xA1: cleared by the ctor and Connect, set by the tick lambda, read only by HandleDisconnectMessage. | 0x8367da; 0x83710e; 0x8383dc; 0x62fa34 | M1-015 | EXACT_SOURCE (the sweep misses register-offset accesses) |
| B37 | Telemetry only: DAS queue stats at 4000 samples and at disconnect; EnableWifiTelemetry is a no-op warning. | 0x62f1e8; 0x62f29c; 0x62fe48 | not recorded (telemetry only) | EXACT_SOURCE |
| B38 | Close failures are logged; CloseSocket resets the fd to −1 and the port to 0xBAC9. Handler exception isolation was not determined. | 0x839694–0x83969c | M1-022, M1-034 | EXACT_SOURCE / UNKNOWN |

## Gap rows: G1..G3 (transport) and G4..G5 (app layer)

These supersede the open parts of R35, R41, R42, B18, B21, B22, B29 and B38.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| G1.1 | ScheduleCallback → Queue vtable +0x14 → WakeAfterRepeat(TaskExecutor at Queue+4, fn, u64 ms period, name). | 0x007FB93A ldr.w ip,[ip,#0x14]; blx ip; 0x007FBB1E adds r1,r7,#4; 0x007FBB20 strd r6,r5,[sp]; 0x007FBB2A blx WakeAfterRepeat | M1-021 | EXACT_SOURCE |
| G1.2 | First time = steady_clock::now() + period×1e6 ns, so the first run is one period later. | 0x007FCEBA blx steady_clock::now; 0x007FCEBE/C2 movw/movt #0xF4240; 0x007FCEC6 umull; 0x007FCECA mla; 0x007FCED2 adds/adcs; 0x007FCED6 strd r1,r0,[sb] | M1-021 | EXACT_SOURCE |
| G1.3 | Task holder 0x50 bytes: byte0 sync 0, byte1 repeat 1, fn +8, weak handle +0x20/+0x24, time +0x30, period ms +0x38, name +0x40, id +0x4C; pushed with AddTaskHolderToDeferredQueue. | 0x007FCE40 mov.w r1,#0x100; 0x007FCE5A strh.w r1,[sp,#0x60]; 0x007FCE7A; 0x007FCF40–0x007FCF44; 0x007FCF60 | M1-021 | EXACT_SOURCE |
| G1.4 | AddTaskHolderToDeferredQueue: lock +0x20, append to the vector +0x28/+0x2C, sort (larger time first, so back() is the earliest), notify cv +0x24. | 0x007FCD92; 0x007FCE0C blx __sort; 0x007FCE14 notify_one; comparator 0x007FE2FA–0x007FE316 | M1-021 | EXACT_SOURCE |
| G1.5 | Two executor threads: Execute (+4) and ProcessDeferredQueue (+0x1C). | ctor 0x007FBDA4–0x007FBDBA; 0x007FBDD6–0x007FBDEC | M1-021 | EXACT_SOURCE |
| G1.6 | The deferred thread waits with wait_until<steady_clock> for the next time, or until the queue grows past +0x5C. | 0x007FC172–0x007FC182; 0x007FC194; 0x007FC198 cmp r0,#1 | M1-021 | EXACT_SOURCE |
| G1.7 | Each pass: now; if now < back().time, compute the wait and stop; else copy the holder and post it to the immediate queue with AddTaskHolder. The deferred thread never runs the function. | 0x007FC1BC blx steady_clock::now → [sp,#0x30]; 0x007FC1C8–0x007FC1D6; 0x007FC270 blx AddTaskHolder | M1-021 | EXACT_SOURCE |
| G1.8 | pop_back; if the repeat byte is set: unlock, time = the now from 0x007FC1BC + period×1e6, re-add. Fixed delay; missed periods are not added back. | 0x007FC304–0x007FC342; 0x007FC344 ldrb r0,[sp,#0x99]; 0x007FC34E; 0x007FC352–0x007FC376; 0x007FC3C4; 0x007FC3FA | M1-021 | EXACT_SOURCE |
| G1.9 | AddTaskHolder: push to +0x10 under mutex +8, notify cv +0xC; never merged. | 0x007FCCF4; 0x007FCD50–0x007FCD54; 0x007FCD66 | M1-021 | EXACT_SOURCE |
| G1.10 | Execute loops lock → Wait (0x007FD080) → Run. Run swaps out the vector, unlocks and calls each task in order on one thread, skipping tasks whose handle weak_ptr has expired. Runs never overlap; a backlog runs back-to-back. | 0x007FBF78–0x007FBF8E; Run 0x007FD0C6–0x007FD0DA; 0x007FD0F2–0x007FD106; 0x007FD124–0x007FD128 | M1-021 | EXACT_SOURCE |
| G2.1 | SendMessage: FindConnection(addr, create = type == 1). | 0x00836C52; 0x00836C5A cmp r2,#1; 0x00836C68–0x00836C6A; 0x00836C6E | M1-032 | EXACT_SOURCE |
| G2.2 | A miss with create: new 0x250 bytes, ctor, map insert. | 0x00837098; 0x0083709C; 0x008370A8; 0x008370C0 | M1-032 | EXACT_SOURCE |
| G2.3 | The ctor sets +0x50 = GetCurrentNetTimeStamp(). | 0x0083592E blx GetCurrentNetTimeStamp; 0x00835932 vmov d0,r0,r1; 0x0083593C vstr d0,[r4,#0x50] | M1-032 | EXACT_SOURCE |
| G2.4 | GetCurrentNetTimeStamp: double ms on steady_clock from a static epoch taken at first call. | 0x0083561A/0x00835628; 0x00835638; 0x0083563E; 0x00835644; 0x0083564C (literal 0x3F50624DD2F1A9FC at 0x00835660); 0x00835654 | M1-032 | EXACT_SOURCE |
| G2.5 | Only two stores to +0x50 in 0x835600–0x83B000: the ctor and UpdateLastAckedMessage; the other #0x50 hits are other objects. | 0x0083593C; 0x00835B9C | M1-032 | EXACT_SOURCE |
| G2.6 | UpdateLastAckedMessage has one caller: ReceiveData, for a valid frame from a known connection. | 0x00837818 (after 0x008377FC) | M1-032 | EXACT_SOURCE |
| G2.7 | Timeout test: now > +0x50 + 5000.0 (.data 0x01051F48), strictly greater. | 0x00836080; 0x00836088; 0x0083608C vadd.f64; 0x00836090 vcmpe; 0x00836098; 0x0083623C–0x0083623E | M1-032 | EXACT_SOURCE |
| G2.8 | Every ReliableConnection::Update reaches HasConnectionTimedOut; on true, log, OnDisconnected, delete. | 0x008365A4; 0x00837BCC; 0x00837C56; 0x00837C60 | M1-015, M1-032 | EXACT_SOURCE |
| G2.9 | In sync mode, RCM::Update calls RT::Update only when +0xA0 is set, so detection is per engine tick; in async mode it is the 2 ms tick. | 0x0062F1BE; 0x0062F1C2; 0x0062F1C4 | M1-032 | EXACT_SOURCE |
| G2.10 | ReceiveData can also create a connection (inbound connect); the conditions are R13. | 0x008377D6; 0x008377EA–0x008377F2; 0x008377FC | M1-018 | EXACT_SOURCE (conditions per R13) |
| G3.1 | Signal emit calls each subscriber's std::function; compact pr0 entry, no LSDA. | 0x0069E290; exidx 0x00BC169C word 0x8000ABB0 | M1-034 | EXACT_SOURCE |
| G3.2 | The broadcast helper 0x0069DE18 calls emit; compact, no LSDA. | 0x0069DE44/0x0069DE62/0x0069DE74 bl 0x69e274; exidx 0x00BC15D4 word 0x8001AAB0 | M1-034 | EXACT_SOURCE |
| G3.3 | Broadcast(&&): cleanup-only call site; the pad runs ClearCurrent, then _Unwind_Resume. catch(...) pads go to the terminate helper. | 0x0069DCAE; LSDA 0x00B3F07C call site 0x69DCA8–0x69DCB2 → lp 0x69DCD4, action 0; 0x0069DCD8; 0x0069DCDE; pads 0x69DCD0/0x69DCE2 → bl 0x4e39f8 | M1-034 | EXACT_SOURCE |
| G3.4 | Broadcast(const&) 0x0069DD94: the same structure. | LSDA 0x00B3F124 (0x69DDD0–0x69DDDA → lp 0x69DDFC, action 0) | M1-034 | EXACT_SOURCE |
| G3.5 | ProcessMessages: Broadcast in a cleanup-only call site (ClearCurrent, free, _Unwind_Resume); the loop does not continue; its four catch(...) pads are terminate pads; the RCM::Update call has no landing pad. | 0x0069DABA; LSDA 0x00B3EF3C 0x69DAB4–0x69DACE → lp 0x69DB64, action 0; 0x0069DB68; 0x0069DBA6; 0x0069DBAC; pads 0x69DB18/26/3A/6E; 0x0069D87C (0x69D85C–0x69D8BE, lp none) | M1-034 | EXACT_SOURCE |
| G3.6 | 0x004E39F8 = __cxa_begin_catch; std::terminate. | 0x004E39FA; 0x004E39FE | M1-034 | EXACT_SOURCE |
| G3.7 | ProcessArrivedMessages: every handler call is in a cleanup-only call site; no catch. | 0x0062F440, 0x0062F490, 0x0062F498, 0x0062F4A0; LSDA 0x00B28A14 → lp 0x62F504, action 0; 0x0062F518; 0x0062F1FA b.w 0x8ccc1c → 0x4B8B80 | M1-034 | EXACT_SOURCE |
| G3.8 | No catch anywhere upward: UpdateRobotConnection (compact) → CozmoEngine::Update (lp none) → CozmoInstanceRunner::Update (unlock, resume) → Run (lp none) → __thread_proxy (cleanup, resume), the top of the engine thread. CozmoAPI::Update has no static caller and no export. | 0x0052F850–0x0052F856; vtable 0x010310E4; LSDAs 0x00AE630C, 0x00B31D90, 0x00B31C44, 0x00B31FC8; 0x0065C280, lp 0x65C2A2 | M1-034 | EXACT_SOURCE for every frame read. That StartRun starts this thread is B24 (0x6661ea, 0x65b14e). |
| G3.9 | With no handler found, __cxa_throw calls std::terminate. The shipped libc++_shared.so and any Anki terminate or breakpad hook (cozmo_install_google_breakpad 0x00665701) were not read. | imports __cxa_throw, __cxa_begin_catch, __gxx_personality_v0 (re-analysis/symbols/imports.txt:242, 243, 264) | M1-034 | RECOVERABLE_GAP (the exact terminate action is unread) |
| G3.10 | The EH imports are unversioned. NEEDED lists libc.so, libm.so, libc++_shared.so, ... libstdc++.so, libdl.so; not gnustl or stlport. | lief .dynamic NEEDED; .dynsym UNDEF __cxa_throw, _ZSt9terminatev, _ZSt13set_terminatePFvvE, __gxx_personality_v0, __cxa_begin_catch, _Unwind_Resume | M1-034 | EXACT_SOURCE |
| G3.11 | libc++_shared.so defines __cxa_throw 0x7A92D, std::terminate 0x7B28D, set_terminate 0x68D39, get_terminate 0x7B1E9, __cxa_terminate_handler 0xA6010. No shipped .so NEEDs gnustl or stlport. Java loads c++_shared (DAS.java:56), then DAS and cozmoEngine (CozmoActivity.java:40-41). | lief .dynsym of the runtimes; NEEDED scan; sources/com/anki/daslib/DAS.java:56 | M1-034 | EXACT_SOURCE for shipped artifacts; that the unshipped bionic libc/libm define none of these is BLOCKED_EXTERNAL |
| G3.12 | __cxa_throw stores get_terminate() in the header, runs the unwinder; if that returns (no handler), __cxa_begin_catch then std::__terminate(handler). No cleanups run. | 0x0007A948; 0x0007A966; 0x0007A97E bl 0x85564; 0x0007A984; 0x0007A988; 0x0007A98C bl 0x7b208 | M1-034 | EXACT_SOURCE (the unwinder 0x85564 itself is standard ABI, not read) |
| G3.13 | std::__terminate(h): h(); abort_message if it returns or throws. | 0x0007B20C blx r0; 0x0007B20E/0x0007B210; 0x0007B218/0x0007B21A | M1-034 | EXACT_SOURCE |
| G3.14 | The initial handler is default_terminate_handler 0x68B7D (reloc 0xA6010); set_terminate(nullptr) installs the same. | reloc 0xA6010 → 0x68B7D; 0x00068D38–0x00068D40 | M1-034 | EXACT_SOURCE |
| G3.15 | default_terminate_handler: for a std::exception, abort_message("terminating with %s exception of type %s: %s", "uncaught", demangled type, what()); other variants for non-std, foreign, or no exception. | 0x00068B84; 0x00068B9A–0x00068BB8; 0x00068BF4; 0x00068C12; 0x00068C26; 0x00068C30/0x00068C3C/0x00068C4A/0x00068B92; strings 0x68CB4, 0x68C84, 0x68C54, 0x9AC1E; 0xA6018 → 0x9AC2A | M1-034 | EXACT_SOURCE |
| G3.16 | abort_message: message + '\n' to stderr, vasprintf, __assert2(abort_message.cpp, 74, ...), abort(). | 0x00068AA2; 0x00068AA8; 0x00068AB0; 0x00068ABC; 0x00068AC6 movs r1,#0x4a; 0x00068ACA; 0x00068ACE | M1-034 | EXACT_SOURCE for libc++; bionic __assert2/abort are BLOCKED_EXTERNAL |
| G3.17 | The only set_terminate call is set_terminate(nullptr) in AndroidBinding::cozmo_shutdown, which also resets SIGSEGV/SIGBUS/SIGTRAP/SIGABRT/SIGSYS/SIGPIPE to SIG_DFL; reached only from the exported cozmo_shutdown. No set_unexpected; no other shipped .so imports either. Production uses the default handler. | 0x00667862; 0x00667864; 0x00667868–0x00667894; caller 0x006668DA; scan for PLT 0x4BB964; import scan | M1-034 | EXACT_SOURCE for static calls (dlsym and function pointers not excluded) |
| G3.18 | Breakpad install: CozmoActivity.onCreate calls installBreakpad(dumpsDir/APP_RUN_ID.dmp) when HOCKEYAPP_APP_ID is non-empty, which it is. Unity declares cozmo_install_google_breakpad but never calls it; there are no native callers of 0x00665701. | sources/com/anki/cozmo/CozmoActivity.java:50–53, 105–114; CozmoActivity.smali:690; resources/AndroidManifest.xml:120–122; unity/scripts/csharp/CozmoBinding.cs:53 | M1-034 | EXACT_SOURCE |
| G3.19 | The JNI installBreakpad calls InstallGoogleBreakpad(path): open(O_WRONLY\|O_CREAT\|O_EXCL\|O_TRUNC, 0600), MinidumpDescriptor{fd}, then new ExceptionHandler(desc, null, DumpCallback 0x667AF5, 0, install_handler 1, -1). | 0x006679D2/0x006679DC; 0x00667A34; 0x00667A3A–0x00667A42; 0x00667A4E; 0x00667A7A–0x00667A8C | M1-034 | EXACT_SOURCE |
| G3.20 | The ExceptionHandler ctor sets up sigaltstack, then InstallHandlersLocked saves the old handlers and installs SignalHandler (SA_SIGINFO\|SA_ONSTACK) for SIGSEGV, SIGABRT, SIGFPE, SIGILL, SIGBUS and SIGTRAP. | 0x0095107C; 0x00951118; 0x009511DC; 0x009512E4–0x0095139C; 0x009513C0; 0x009513C8; 0x009513D0–0x009513D4; 0x009513DC–0x00951434 | M1-034 | EXACT_SOURCE |
| G3.21 | On SIGABRT: HandleSignal → GenerateDump → DumpCallback (log, close fd, return succeeded). If handled: SIG_DFL and tgkill(SIGABRT), or _exit(1). If not: restore the saved handlers and re-raise. | 0x009519F0/0x00951A30; 0x00951A78–0x00951A8C; 0x00951AE8–0x00951B68; 0x00951B88; 0x00951B9C–0x00951BBC; 0x00951BD0; 0x00951E04; 0x00952424–0x0095245C; 0x00667AF4–0x00667B68 | M1-034 | EXACT_SOURCE |
| G3.22 | End result: the stderr message and __assert2, abort, a Breakpad minidump, then the process dies from SIGABRT. | G3.12–G3.21 | M1-034 | EXACT_SOURCE for the shipped path. HARDWARE_ONLY: whether a later libunity/libmono SIGABRT handler sits above Breakpad, and debuggerd/logcat output |
| G4.1 | The RCM ctor passes its UDP transport (RCM+0x14) to RegisterTransport. The returned ScopedHandle is pushed into the handle vector at RCM+4, so it lives as long as the RCM. RegisterTransport has no other caller (branch scan of .text). | 0x62edb4 ldr r1,[r5,#0x14]; 0x62edb8 blx 0x4b8af0 (PLT RegisterTransport); 0x62ed0e str r6,[r8,#4]!; 0x62edbe/0x62edc0 mov r0,r8; bl 0x52d44c (push_back, 0x52d44c–0x52d46c) | M1-023 | EXACT_SOURCE |
| G4.2 | RegisterTransport binds handler 0x83bad1 to the transport and connects it to the static ProtoSignal<void()> at 0x105df8c. | 0x83b9aa/0x83b9b4 r4=0x83bad1; 0x83b9c4 str r1,[r2,#4]; 0x83b9ae/0x83b9b8 ip=0x105df8c; 0x83b9d4 bl 0x83ba34 (connect; ProtoHandle vtable 0x103fdbc) | M1-023 | EXACT_SOURCE |
| G4.3 | The signal at 0x105df8c has exactly one subscriber (G4.2). The xref scan finds only: static ctor 0x4dd62e, atexit dtor 0x4dd654, RegisterTransport 0x83b9b8, emitters 0x83bc3e and 0x83bc86 (false positives checked). Only 0x83b9d4 branches to connect 0x83ba34. | xref scan; 0x4dd62a–0x4dd658 | M1-023 | EXACT_SOURCE |
| G4.4 | Handler: if transport+0x94 (fd) < 0, nothing. Otherwise ResetSocket and a log "WifiUtil.BindTransport" / "reset socket %d". No other condition. | 0x83bad6 ldr.w r0,[r4,#0x94]; 0x83badc blt 0x83bb20; 0x83bae0 blx ResetSocket; strings 0x83bb54, 0x83bb6c | M1-023 | EXACT_SOURCE |
| G4.5 | ResetSocket sets +0x9D = 1 only. The next UDPTransport::Update calls CloseSocket; if that returns 1, OpenSocket(+0x98). +0x9D is cleared either way (a failed close is not reopened). The read loop runs only if fd >= 0. | 0x83a256 movs r1,#1; 0x83a258 strb.w r1,[r0,#0x9d]; 0x83ace8–0x83acec; 0x83acf0 CloseSocket; 0x83acf4 cmp r0,#1; bne; 0x83acf8–0x83acfe OpenSocket; 0x83ad04 clear; 0x83ad08–0x83ad18 | M1-023 | EXACT_SOURCE |
| G4.6 | The emitters, Java_com_anki_util_WifiUtil_NativeBindNetworkCallback and ...NativeBindLollipopCallback, load 0x105df8c and jump to the emit loop 0x83bc48, which calls every handler. The jlong network handle is ignored. | 0x83bc3c/0x83bc3e/0x83bc40 b.w 0x83bc48; 0x83bc84/0x83bc86/0x83bc88; loop 0x83bc4c–0x83bc76 (0x83bc5e blx function<void()>::operator()) | M1-023 | EXACT_SOURCE |
| G4.7 | NativeStatusCallback only stores the SSID and status strings (0x105df70/0x105df80); NativeScanCallback is bx lr. Neither emits. | 0x83bb80–0x83bc08; 0x83bb7c; 0x83b928, 0x83b964 | M1-023 | EXACT_SOURCE |
| G4.8 | Java attemptNetworkBind: on API >= 23, bindProcessToNetwork, and NativeBindNetworkCallback only if the result is true; on API 21–22, setProcessDefaultNetwork and the Lollipop callback only if true; below 21 there is no native call. | sources/com/anki/util/WifiUtil.java:381-404; smali/com/anki/util/WifiUtil.smali:275, 303, 310, 336, 364, 367 | M1-023 | EXACT_SOURCE |
| G4.9 | unbindFromNetwork always emits: bindProcessToNetwork(null) plus NativeBindNetworkCallback(0) on API >= 23, or setProcessDefaultNetwork(null) plus the Lollipop callback on 21–22. | WifiUtil.java:321-332; smali:2638-2696 | M1-023 | EXACT_SOURCE |
| G4.10 | bindToNetwork(info, cb): on API < 21, callback only; otherwise findNetworkForInfo + attemptNetworkBind, retried every 100 ms up to 10 times, each able to emit. | WifiUtil.java:270-319 | M1-023 | EXACT_SOURCE |
| G4.11 | CozmoWifi BroadcastReceiver (registered by CozmoJava.init from Unity CozmoBinding.cs:72), on STATE_CHANGE / WIFI_STATE_CHANGED, runs handleNetworkUpdate for Wi-Fi-type NetworkInfo. If bound and not connected (CONNECTED / VERIFYING_POOR_LINK / CAPTIVE_PORTAL_CHECK), unbind, which always resets. Else if not bound, not binding, connected, and a Cozmo AP (^Cozmo_[0-8]{2}[0-9A-F]{4}$, or <unknown ssid> with IP 172.31.*), bindToNetwork, which resets on success. | sources/com/anki/cozmo/CozmoWifi.java:28-36, 38-52, 63-85; CozmoJava.java:8-12; WifiUtil.java:354-363, 580-598 | M1-023 | EXACT_SOURCE |
| G4.12 | AndroidConnectionFlow.StartPingTest → beginPingTest("172.31.1.1",1000,500), from Initialize(shouldStartPingTest) (ConnectionFlowController.cs:357) and AndroidConnectionFlow.cs:302/310. On API >= 26, with a Cozmo SSID, not bound and not binding, bindToNetwork, which resets on success. | unity/scripts/csharp/AndroidConnectionFlow.cs:95-100, 131-133, 150-153, 302, 310; WifiUtil.java:188-217 | M1-023 | EXACT_SOURCE |
| G4.13 | WifiUtil's own receiver (CONNECTIVITY_CHANGE, STATE_CHANGE, ...) calls only NativeStatusCallback and scan handling, never bind or unbind. | WifiUtil.java:50-69, 518-538 | M1-023 | EXACT_SOURCE |
| G5.1 | Returns if +0x10 != 0 (response already sent; set at 0x52df2e/0x52df30) or +0x14 (IExternalInterface*) == 0. | 0x52d478–0x52d484; 0x52df30 strb r0,[r6,#0x10] | M1-029 | EXACT_SOURCE |
| G5.2 | Parses the firmwareVersion bytes (msg+4..+8) with Json::Reader; failure → error, OnNotified(3, 0). | 0x52d48c; 0x52d490 ldrd r1,r2,[r0,#4]; 0x52d4ba; 0x52d4c0 beq 0x52d548; 0x52d59c–0x52d5a2 | M1-028, M1-029 | EXACT_SOURCE |
| G5.3 | Keys "build", "version", "time" (0x1030630/0x1030628/0x103062c → 0xbfef65/0xc49fa8/0xbfef60). "build" of length 7 equal to "FACTORY" (0x52dbd0) takes the factory path. | 0x52d4c2–0x52d506 (cmp r0,#7; compare; beq.w 0x52d856) | M1-029 | EXACT_SOURCE |
| G5.4 | Factory path: version string starting 'F' logs FactoryFirmware, otherwise UnknownVersion; both send robot.factory_firmware_version, then OnNotified(3, 0). | 0x52d856–0x52d87c (cmp r0,#0x46); 0x52d8e4–0x52d908; 0x52d98c–0x52d992 | M1-029 | EXACT_SOURCE |
| G5.5 | v = "version".asUInt() (r8), t = "time".asUInt() (r5); E_v = +0x1C (fp), E_t = +0x20. | 0x52d51a/0x52d51e; 0x52d532/0x52d536; 0x52d538 ldrd fp,r0,[r6,#0x1c]; 0x52d53c | M1-029 | EXACT_SOURCE |
| G5.6 | sim = (v\|t == 0) && !json["sim"].isNull(); else 0. | 0x52d53e orrs.w r0,r5,r8; 0x52d542; 0x52d5a8–0x52d5b6; 0x52d544 | M1-029 | EXACT_SOURCE |
| G5.7 | Telemetry and log only ("robot firmware: %d%s%s%s (app: %d%s)" with " (dev)" when v == t, " (SIM)", app " (dev)" when E_v == E_t). No behavioural effect. | 0x52d68a–0x52d692; 0x52d72c; 0x52d774; 0x52d766; strings 0x52dc18, 0x52dc20, 0xbe8996 | M1-029 | EXACT_SOURCE |
| G5.8 | Register moves before the decision: [sp+0x20]=sim, [sp+0x24]=v; sl=t, r8=E_v, fp=E_t, r7=v, r4=sim. | 0x52d5e0; 0x52d5e4; 0x52d696; 0x52d69a; 0x52d69c; 0x52d77c; 0x52d796 | M1-029 | EXACT_SOURCE |
| G5.9 | No robotAvailable (+0x2E) and not sim: error, +0x2D=1, SendConnectionResponse(1, 0), +0x2D=0. | 0x52d7b8–0x52d7c4; 0x52d7e8–0x52d850 | M1-028 | EXACT_SOURCE |
| G5.10 | sim: result 0, even without robotAvailable. | 0x52d7c6 movs r0,#0; 0x52d7c8 cmp r4,#0; bne.w 0x52d9ae | M1-029 | EXACT_SOURCE |
| G5.11 | robotDev = (v == t), appDev = (E_v == E_t); if they differ, 3 OutdatedFirmware. | 0x52d7ce–0x52d7dc; 0x52d7de teq.w r0,r1; 0x52d7e2 beq 0x52d8da; 0x52d7e4 movs r0,#3 | M1-029 | EXACT_SOURCE |
| G5.12 | Unsigned: E_v == v → 0; E_v > v → 3; E_v < v → 4. Time is not compared again. | 0x52d8da ldr r0,[r6,#0x1c]; 0x52d8dc cmp r0,r7; 0x52d8de bne 0x52d9a6; 0x52d8e0 movs r0,#0; 0x52d9a6 mov.w r0,#4; 0x52d9aa–0x52d9ac it hi; movhi r0,#3 | M1-029 | EXACT_SOURCE |
| G5.13 | OnNotified(uxtb(result), v); the factory and parse-fail paths pass 0. | 0x52d9ae–0x52d9b4 (mov r2,r7) | M1-028 | EXACT_SOURCE |
| G5.14 | The ctor stores stack args 4 and 5 at +0x1C/+0x20. AddRobot passes &RobotManager+0x84 and &RobotManager+0x88. | 0x52d170 ldrd ip,r6,[sp,#0x70]; 0x52d196 strd ip,r6,[r4,#0x1c]; AddRobot 0x52eedc–0x52eefe; 0x52ef08 | M1-029 | EXACT_SOURCE |
| G5.15 | RobotManager ctor: +0x84 = 0, +0x88 = 0. | 0x52e508; 0x52e512 strd r1,r0,[r6,#-0x10]; 0x52e516 strd r0,r6,[r6,#-8] | M1-029 | EXACT_SOURCE |
| G5.16 | RobotManager::Init calls FirmwareUpdater(+0x5C)::LoadHeader(type 0, 0, bind(ParseFirmwareHeader)), LoadHeader's only caller. | 0x52e7d6–0x52e7f8 | M1-029 | EXACT_SOURCE |
| G5.17 | GetFirmwareFilename(0) = "config/engine/" + "firmware" + "/cozmo.safe", then pathToResource(Scope 1, ...). | 0x6766b2–0x6766c6; 0x676752; 0x67678c; 0x676872 movs r0,#1; 0x676884 | M1-029 | EXACT_SOURCE except how Scope 1 resolves |
| G5.18 | The load is asynchronous: pthread_create runs LoadFirmwareFile (reads the whole file, sets +0x18), then LoadHeaderData on the loader thread. | 0x67692a; 0x67861c; 0x6764dc–0x6765da; lambda vtable 0x1030634, 0x67886c → 0x8cd62c → PLT 0x4bc2f4 | M1-029 | EXACT_SOURCE |
| G5.19 | LoadHeaderData: error if not loaded or size < 0x800; parses JSON over the first 0x800 bytes up to the first NUL; callback on success. | 0x677c44–0x677c54; 0x677cf6 memchr(...,0,0x800); 0x677d24; 0x677d28–0x677d34 | M1-029 | EXACT_SOURCE |
| G5.20 | ParseFirmwareHeader: "version" → +0x84, "time" → +0x88 (GetValueOptional<uint>); a warning if either is 0. | 0x52ea36–0x52ea5e; 0x52ea76–0x52ea9a; 0x52eaac–0x52eac8 | M1-029 | EXACT_SOURCE |
| G5.21 | Shipped asset re-analysis/obb/assets/cozmo_resources/config/engine/firmware/cozmo.safe (382416 bytes, sha256 a4e266cf...a449c0). The header JSON (NUL at 445) has "version": 2381, "time": 1546972025, "build": "DEVELOPMENT". | file offsets 0–445 | M1-029 | EXACT_SOURCE for the values; the Scope 1 → assets/cozmo_resources/ mapping was not read (matched by file name) |
| G5.22 | Unity extracts the resource files, then CozmoEngineInitialization → CozmoBinding.Startup → cozmo_startup. | unity/scripts/csharp/StartupManager.cs:259, :272; RobotEngineManager.cs:203-215; CozmoBinding.cs:26, :70 | M1-029 | EXACT_SOURCE |
| G5.23 | cozmo_startup → StartRun: CozmoInstanceRunner is built synchronously (its ctor calls CozmoEngine::Init), then the engine thread is created. | 0x6661ea; 0x65b122; 0x65c024; 0x65b14e | M1-029 | EXACT_SOURCE |
| G5.24 | CozmoEngine::Init: LoadRobotConfigs, InitExperiments, NeedsManager::Init, RobotManager::Init (its only caller), PerfMetric::Init; +0x10 = 1. | 0x4ec9ce; 0x4ec9d6; 0x4eca0a; 0x4eca1a; 0x4eca22; 0x4eca9c | M1-029 | EXACT_SOURCE |
| G5.25 | RobotManager::Init → LoadHeader → pthread_create. The read and ParseFirmwareHeader run on the loader thread. There is no join; a second load terminates. | 0x52e7f8; 0x67692a; 0x676962–0x67696e; 0x67861c; 0x67886c → 0x8cd62c | M1-029 | EXACT_SOURCE |
| G5.26 | CozmoEngine::Update calls UiMessageHandler::Update every tick. States: 0 → 2 on UI devices; 1 runs DoNonConfigDataLoading and sends EngineLoadingDataStatus, → 3 when done; 3 Running; 4 firmware update. | 0x4ed514; 0x4ed5c4–0x4ed5cc; 0x4ed5d8–0x4ed5ea; 0x4ed5fa–0x4ed60e; 0x4ed618 → 0x4ed7de | M1-029 | EXACT_SOURCE (UiMessageHandler dispatch not read) |
| G5.27 | The ConnectToRobot handler checks only DoesRobotExist(1); AddRobot copies +0x84/+0x88 with no check of engine state or firmware load. | 0x4ed026–0x4ed02c; 0x4ed074; 0x4ed07c; 0x4ed1ec; 0x52eef2/0x52eef8 → 0x52ef08 | M1-029 | EXACT_SOURCE |
| G5.28 | The loaded flag +0x18 is read only by LoadHeaderData, UpdateSubState (OTA) and a lambda at 0x678828; nothing on the AddRobot path. The loader is joined only in ~FirmwareUpdater and WaitForLoadingThreadToExit, which has no direct branch. | range 0x6763a8–0x678b3c #0x18 scan; 0x6763e2–0x6763ea | M1-029 | EXACT_SOURCE for the range (register-offset access would be missed) |
| G5.29 | Unity loads the main scene only after EngineLoadingDataStatus ratioComplete reaches 1; ConnectToRobot comes from the main-scene connection flow. That gate measures DoNonConfigDataLoading, not the header. | StartupManager.cs:274, :318-326, :337-343, :368-371; ConnectionFlowController.cs:195, :437, :670-673 | M1-029 | EXACT_SOURCE for the gate (not every route into StartConnectionFlow was traced) |
| G5.30 | The loader thread starts before the engine thread and before any game message. Before the first ConnectToRobot come the rest of startup, the engine data load, the main-scene load and a user-driven flow. The load itself is one 382416-byte read plus ≤ 0x800 bytes of JSON. If AddRobot ran first, the robot would be checked against 0/0 (appDev = true). | G5.22–G5.29 | M1-029 | Ordering not guaranteed by code; "load always wins in practice" is a timing property |
| G5.31 | Missing file, size < 0x800, or a parse failure: the callback never runs, so the values stay 0/0 for the session. | 0x6764e4 → 0x6765d8; 0x677c50–0x677c54; 0x677d28; ctor 0x52e512/0x52e516 | M1-029 | EXACT_SOURCE |
| G5.32 | pathToResource: tbb on scope 0..4 (table 03 14 22 2f 41). 0 → +0x00 + "/output"; 1 → +0x24; 2 → +0x0C; 3 → +0x0C + "/gameLogs"; 4 → +0x18; then "/" + name. | 0x84be34–0x84be3a, table 0x84be3e; case 1 0x84be66; 0x84bef4–0x84bf16; strings 0x84bfac, 0x84bfa0, 0x84bfb4, 0x84bf74; 0x84bddc–0x84bdfe | M1-029 | EXACT_SOURCE |
| G5.33 | The DataPlatform ctor stores its four strings at +0x00/+0x0C/+0x18/+0x24; +0x24 is the 4th (stack) argument. | 0x84bcec–0x84bd7c (0x84bd60; 0x84bd64) | M1-029 | EXACT_SOURCE |
| G5.34 | cozmo_startup builds DataPlatform from DataPlatformFilesPath, DataPlatformCachePath, DataPlatformExternalPath and (on the stack) DataPlatformResourcesPath. | 0x6658c8; 0x6658f0; 0x66591a; 0x665944; 0x6659d2; 0x6659d4 | M1-029 | EXACT_SOURCE |
| G5.35 | Unity: DataPlatformResourcesPath = persistentDataPath + "/cozmo" + "/cozmo_resources". | RobotEngineManager.cs:531-538; PlatformUtil.cs:5-13 | M1-029 | EXACT_SOURCE |
| G5.36 | Before startup, Unity extracts every resources.txt file from streamingAssetsPath to persistentDataPath/cozmo/, skipping if allAssetHash.txt matches. | StartupManager.cs:585-705 | M1-029 | EXACT_SOURCE |
| G5.37 | resources.txt lists cozmo_resources/config/engine/firmware/cozmo.safe, so the engine reads the extracted shipped file (version 2381, time 1546972025). This confirms G5.21. | re-analysis/obb/assets/resources.txt:2075 | M1-029 | EXACT_SOURCE |
| G5.38 | Writer scan method: all of .text split by .ARM.exidx and dynsym (33526 functions), Thumb capstone, every store and add with #0x84/#0x88; 1238 hits, 425 not sp-relative, in 283 functions; filtered to RobotManager methods or functions that call one. | scratch scan84.py, filt2.py, hits84.json | M1-029 | method |
| G5.39 | Within RobotManager, only ParseFirmwareHeader (0x52ea54, 0x52ea92) and AddRobot (0x52eef2/0x52eef8) touch them; the other matches are other objects. ParseFirmwareHeader is referenced only through GOT 0x103ea48 in RobotManager::Init; LoadHeader has one caller; the version/time key GOT entries are used only in HandleFirmwareVersion and ParseFirmwareHeader. | 0x52ea54; 0x52ea92; 0x52eef2; 0x52eef8; 0x4ea94a; 0x579fd8–0x579fe2 | M1-029 | EXACT_SOURCE within the method's reach |
| G5.40 | The method cannot see: adjusted-base access (like the ctor's own zeroing, found by reading); register-offset stores; whole-object copies; inlined writes in unrelated functions; 277 untyped functions; ARM-mode code. | — | M1-029 | absence not established |

