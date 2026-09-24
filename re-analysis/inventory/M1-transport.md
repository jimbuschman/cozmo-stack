# M1 transport inventory

**State: approved by the operator at Checkpoint 2 on 2026-09-24, then corrected (C1..C5 below, and the D8 wording) and re-approved the same day; completed by the closure pass (C6..C14, policies M1-039, M1-040, M1-042) and approved by the manager under the operator's standing authorisation of 2026-09-24; frozen with `python re-analysis/tools/fidelity.py --approve M1-transport`.** D1 to D4 were approved on 2026-09-23, and D5 to D8 on 2026-09-24. Any change to this file, or to an M1 record's title, authority, evidence, live_path, hardware_required or status (other than an IMPLEMENTATION_GAP being built), fails the checker until a new approval.

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
| M1-002 | EXACT_SOURCE | Frame header: 4-byte prefix 43 4F 5A 03, no CRC, then the 10-byte RE header; receive-side checks | R1, R3, B5, B6, B17, B19 |
| M1-003 | EXACT_SOURCE | Message types 1..11, always-unreliable set, container types, dispatch by type | R5, R6, R7, R8, R12 |
| M1-004 | EXACT_SOURCE | Sequence ids 1..65534, wrap and in-range test | R14, R15 |
| M1-005 | EXACT_SOURCE | Reliable-transport tunables as RobotConnectionManager::Init sets them | tunables table, B13 |
| M1-006 | EXACT_SOURCE | Send queue, packing, resend choice and pacing | R2, R20, R21, R25, R26, R27, R28, R29 |
| M1-007 | EXACT_SOURCE | Receive path: in-order delivery only, no buffering, mixed frames still walked | R4, R9, R10, R11, R12, R16, R17 |
| M1-008 | EXACT_SOURCE | 17-byte ping payload, sent unreliable as type 0x0B | R30 |
| M1-009 | EXACT_SOURCE | Multipart split and reassembly | R22, R23 |
| M1-010 | EXACT_SOURCE | Asynchronous 2 ms transport tick and ReliableTransport::Update order | R34, R35, B14 |
| M1-011 | EXACT_SOURCE | Ping receive: counters, and only a ping marked as a reply measures the round trip | R31 |
| M1-012 | EXACT_SOURCE | Frame payload bound 1406 and the UDP send buffer | R24, B5, B7, B8, B9 |
| M1-013 | COMPATIBILITY_POLICY | Windows high-resolution timer realising the 2 ms and 60 ms periods | policy |
| M1-014 | COMPATIBILITY_POLICY | Host thread structure that realises the engine threading | policy |
| M1-015 | IMPLEMENTATION_GAP | Connection timeout 5 s, and how a lost or failed connection is reported | R19, R41, B22, B31, B32, B36 |
| M1-016 | EXACT_SOURCE | Incoming ack processing | R18, R43 |
| M1-017 | EXACT_SOURCE | Ping schedule and ReliableConnection::Update order | R32, R33 |
| M1-018 | EXACT_SOURCE | Connections are created only by a connect request | R13, B19 |
| M1-019 | IMPLEMENTATION_GAP | Transport entry points: SendData, Connect, FinishConnection, Disconnect, Start, Stop | R38, R39, R40, B21 |
| M1-020 | EXACT_SOURCE | Production runs the transport asynchronously; sync mode is unused | R36, B15 |
| M1-021 | EXACT_SOURCE | The 2 ms Dispatch callback: fixed delay, first run after one period, never overlapping | G1.1..G1.10, R35 |
| M1-022 | EQUIVALENT_IMPLEMENTATION | UDP socket: setup, ephemeral local port, send errors, receive loop, reopen | B10, B11, B12, B16, B38 |
| M1-023 | EXACT_SOURCE | Socket reset on every Android process network bind or unbind | G4.1..G4.13, B18 |
| M1-024 | IMPLEMENTATION_GAP | Engine tick 60 ms: arrivals drained FIFO and handed up once per tick | B20, B24, B25, B26 |
| M1-025 | IMPLEMENTATION_GAP | Connect request from the game, the connected response, and DisconnectCurrent | B2, B23, B33, B35 |
| M1-026 | IMPLEMENTATION_GAP | App send path: every robot message is sent reliable, no flush hint, only when connected | R42, B28 |
| M1-027 | IMPLEMENTATION_GAP | Arrived-message filters and the fatal robotError disconnect | B27 |
| M1-028 | IMPLEMENTATION_GAP | Initial-connection handshake outcomes | B29 |
| M1-029 | IMPLEMENTATION_GAP | Firmware version check against the shipped firmware header | G5.1..G5.40, B29 |
| M1-030 | IMPLEMENTATION_GAP | Message gating until the handshake validates the robot | B30 |
| M1-031 | IMPLEMENTATION_GAP | Idle-timeout disconnect | B34 |
| M1-032 | EXACT_SOURCE | A never-answered connection times out 5000 ms after it is created | G2.1..G2.10 |
| M1-033 | HARDWARE_ONLY | Robot-side transport behaviour | part A question 4 |
| M1-034 | COMPATIBILITY_POLICY | Handler isolation, a deliberate departure: in the original a handler exception aborts the engine process | G3.1..G3.17 (policy, D6), B38 |
| M1-035 | EXACT_SOURCE | Async hand-off: sends and ticks FIFO on one thread | R37 |
| M1-036 | COMPATIBILITY_POLICY | Crash reporting after an engine-thread abort | G3.18..G3.22 (policy) |
| M1-037 | COMPATIBILITY_POLICY | Host trigger for the socket reset | policy (D5) |
| M1-038 | COMPATIBILITY_POLICY | Stop processing a frame after a handled DisconnectRequest sub-message | policy (D8), C5 |
| M1-039 | COMPATIBILITY_POLICY | Windows ICMP port-unreachable on UDP receive is no data | policy |
| M1-040 | COMPATIBILITY_POLICY | Accept every robot firmware, and log it | policy |
| M1-041 | IMPLEMENTATION_GAP | Robot initialisation after a Success connection response, and the gates it opens | CB21..CB24, CD12, CD17..CD23, CD30 |
| M1-042 | COMPATIBILITY_POLICY | The original app's post-connect defaults are sent by this stack | policy, CD27, CD28 |
| M1-043 | HARDWARE_ONLY | Whether a 0-byte UDP read warns | CA35 |

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
  - four policies from D5, D6 and D8: handler isolation (M1-034), crash reporting (M1-036), the host reset trigger (M1-037) and stopping a frame after a DisconnectRequest (M1-038).
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

- **D8.** A frame is not processed past a DisconnectRequest sub-message that was handled, i.e. in sequence, so that it deleted the connection (M1-038, COMPATIBILITY_POLICY). An out-of-sequence DisconnectRequest is dropped by R12, deletes nothing, and the frame continues as in the original (wording confirmed by the operator after batch 2a verification). The original deletes the connection and keeps walking the frame through the freed pointer, which is undefined behaviour and is not reproduced (correction C5).

## Corrections after the first freeze

The read-only comparison of the candidate against the first frozen inventory found five errors or omissions in it. The manager checked each against libcozmoEngine.so, and the operator approved correcting them on 2026-09-24. The corrected rows are marked `[corrected Cn]`.

- **C1. Reopen port (B16, B18, M1-022).** After ENOTCONN or a socket reset, the socket is reopened only if the close succeeded, and on port 0xBAC9 = 47817, not an ephemeral port.
  - CloseSocket sets +0x98 = 0xBAC9 on both of its paths (0x00839694..0x0083969C).
  - The reopen reads +0x98 after the close (0x0083AB2E, 0x0083ACF8), and OpenSocket binds it (0x00839A46).
  - Only the first open, with Init's port 0, is ephemeral.
- **C2. MSG_TRUNC (B17, M1-002).** The row was inverted. A datagram with MSG_TRUNC set is the one dropped: 0x0083A888 cmp.w fp,#1; bne 0x0083A8CE is the not-truncated path; the truncated path reaches AddRecvError(1) at 0x0083A8C4..0x0083A8C8, and the loop keeps reading.
- **C3. Missing guards (R25, R26, M1-006).**
  - R25's fast-resend shortcut applies only when (lastRecv − 1.0) > 0 (0x00836466, 0x00836478).
  - R26's idle clause requires lastSend > 0 (0x00836316, 0x0083631E).
- **C4. Ping send path (R30, M1-008).** "Queued, not sent immediately" was misleading. SendPing calls ReliableTransport::SendMessage (0x00835C58), which does AddMessage then SendOptimalUnAckedPackets(1), so a ping can go out on the same call.
- **C5. A frame after a DisconnectRequest (R12, new M1-038).** No row covered it. The original's behaviour is undefined (use-after-free), so it is a policy (D8).

- **Closure-pass policies (operator, 2026-09-24):**
  - **M1-039:** on Windows, a UDP receive that fails with ConnectionReset is no data; it gives no warning and the drain continues.
  - **M1-040:** every robot firmware is accepted; the version is logged, with a warning when it is not 2381.
  - **M1-042:** after a Success connection response, the stack sends the app's defaults itself (stored volume, block pool enabled).
- **Manager calls under the operator's standing authorisation:**
  - thread priority (CA9) and the unused sync mode (CA17) are host mechanism under M1-014;
  - SetCameraParams (CD21), the cliff threshold (CD24), the body radio mode (CD25), the NV reads (CD26), the block-pool detail (CD28), the backpack lights (CD29) and the sleep animation (CC8) are inputs to M3, M4, M5 and the NV subsystem, not M1.

## Hardware evidence

- `re-analysis/acceptance/hardware/20260924-112412-M1-LINK` is the operator's run of `m1-link-check` on commit fef527c, firmware 2457. All 11 judged checks passed:
  - an ephemeral socket;
  - ConnectionResponse within 5 s;
  - a 30 s hold with no timeout;
  - the idle ping cadence;
  - disconnect type 3 with no resend;
  - reconnect on the same port;
  - nothing after Stop;
  - no exceptions.
- M1-017 and M1-019 carry HARDWARE_VERIFIED from it; M1-033's robot-side observations are recorded in its evidence.

## Corrections from the closure pass

- **C6. B31 / M1-015:** 2 ConnectionRejected means the transport connect was never answered (RCD state 1). A drop during the handshake is 1 ConnectionFailure (CB32, CC24).
- **C7. B33 / M1-025:** the ExitSdkMode caller is the lambda 0x0069E1EC..0x0069E1FA, not the template body 0x0069E00A.
- **C8. B21 / R38:** with no connection, the Disconnect closure sends nothing and only warns (CA14).
- **C9. R35:** priority 3 is SCHED_RR at 75% of the OS range, with EPERM ignored (CA9).
- **C10. B10:** the send-failure warning is rate-limited (first failure, then after 30 s) and SentWrongNumBytes is an error log (CA31..CA34).
- **C11. B11:** OpenSocket's failure paths are in CA24..CA29; EADDRINUSE returns success with an unbound socket.

- **C12. R27 / M1-006:** the frame header's firstSeq/lastSeq are the first and last reliable seq in queue order, not a numeric min/max (0x00835DDA..0x00835E96, 0x00835F40..0x00835F5E). The code already did this; only the row's wording changes (batch 4(i) verifier).
- **C13. R30 / M1-008:** SendPing passes SendMessage a second clock reading as the posted time (0x00835C38..0x00835C58). It is stats-only.
- **C14. R23 / M1-009:** R23's own cited range shows the multipart assembly is per connection (0x008374C6 → 0x00835A94).

## Open before M1 can be accepted

- No RECOVERABLE_GAP remains.
- HARDWARE_ONLY: M1-033 (whether the robot accepts packed type 7/8/9 frames from the engine) and M1-043 (whether a 0-byte read warns).
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
| R25 send gate / start choice | (a) Nothing within 2.0 ms of the last send (+0x48). (b) Start = entry with the smallest effective time, first index on ties; never-sent counts as now - (resend+1); sent counts as latestSent, or latestSent - 33.3 if (lastRecv - 1.0) > 0 and it was sent before (lastRecv - 1.0) [corrected C3: guard 0x00836466 vcmpe.f64 d1,#0; 0x00836478 it gt]. (c) IsPacketWorthSending must be true. (d) now > effective + 33.3. Then SendUnAckedMessages up to max times. | (a) 0x008363C8..0x008363FC; (b) 0x0083640E..0x008364A6; (c) 0x008364B2; (d) 0x008364B8..0x008364CE; loop 0x008364D2..0x008364E8 | M1-006 | EXACT_SOURCE |
| R26 worth sending | Any of: sSendPacketsImmediately; lastSend > 0 and lastSend + 32.3 < now [corrected C3: guard 0x00836316 vcmpe.f64 d0,#0; 0x0083631E ble]; the +0x2B flag on any entry from start; a sent entry with latestSent + 32.3 < now; the running sum of (size+3) reaching maxPayload - 0. | 0x008362FE..0x0083639A | M1-006 | EXACT_SOURCE |
| R27 packing / resend content | Pack forward from start (start is taken unconditionally), then extend BACKWARDS while it fits. Header seqs = the first and the last reliable (non-zero) seq in queue (packing) order, not a numeric min/max: across the 65534 to 1 wrap a queue [65534, 1] gives first 65534, last 1 [corrected C12: 0x00835DDA..0x00835E96, header 0x00835F40..0x00835F5E / 0x00835EC8..0x00835EDA → ReSendReliableMessage +4/+6 0x00836A5C..0x00836A7C]. lastSend = now. seq != 0 entries get their sent-time updated and stay; seq 0 entries are deleted (an unreliable message is sent once). Returns the forward count. A resend is a re-pack, not a byte replay. | 0x00835DF2..0x00835E40; 0x00835E56..0x00835EA2; 0x00835EDE, 0x00835F64; 0x00835F72..0x0083604A; 0x0083604C | M1-006 | EXACT_SOURCE |
| R28 resend interval / retries / give-up | 33.3 ms, shortcut by R25b. No per-message retry limit. The only give-up is the 5 s timeout, which deletes the queue. | R20, R25, R19 | M1-006 | EXACT_SOURCE |
| R29 packets per call | At most 1 per ReliableConnection::Update, 1 per SendMessage, 0 on ack; all under the 2.0 ms spacing. | 0x00836592..0x0083659E; 0x00836ECA..0x00836EDC; 0x00837820..0x00837828 | M1-006 | EXACT_SOURCE |
| R30 ping payload | 17 bytes: f64 time (now, or echoed for a reply), u32 numPingsSent (+0x60, incremented first), u32 numPingsReceived (+0x64), u8 isReply. Unreliable type 0x0B, flag 1. A request sets +0x58 = now. SendPing calls ReliableTransport::SendMessage (0x00835C58), i.e. AddMessage then SendOptimalUnAckedPackets(1), so the ping can go out on the same call, subject to the 2.0 ms gate [corrected C4]. | SendPing 0x00835C00..0x00835C62 | M1-008 | EXACT_SOURCE |
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
| B16 | The receive loop runs while TryToReadMessage returns 1: recvmsg(MSG_DONTWAIT) into 0x5C0; ≤ 0 stops (a 0-byte datagram too). errno ≠ EAGAIN warns "ReadFailed"; ENOTCONN closes the socket and reopens it only if the close succeeded, on port +0x98, which CloseSocket has just set to 0xBAC9 = 47817 (B38); only the first open, with Init's port 0, is ephemeral. [corrected C1] | 0x83ad10–0x83ad18; 0x83aa66; 0x83aa76; 0x83aa98; 0x83aac4; 0x83ab22; 0x83ab28 CloseSocket; 0x83ab2c cbz r0; 0x83ab2e ldr.w r1,[r4,#0x98]; 0x83ab34 OpenSocket; CloseSocket 0x839694..0x83969c movw r0,#0xbac9 / strd r1,r0,[r4,#0x94] on both paths | M1-022 | EXACT_SOURCE |
| B17 | Receive checks in order: size ≥ prefix length (+2 with CRC), else TooSmall; memcmp of 4 bytes, else BadPrefix; a datagram with MSG_TRUNC set (larger than the 1472-byte buffer) logs Recv.Truncated, counts AddRecvError(1) and is dropped, and the read loop continues [corrected C2]; CRC if enabled. Then payload + source go to RT::ReceiveData. No source filter at this layer. | 0x83a780; 0x83a80e; 0x83aaa8 ubfx r0,r7,#5,#1; 0x83a888 cmp.w fp,#1; bne 0x83a8ce (not truncated: continue); 0x83a8c4..0x83a8c8 AddRecvError(1); 0x83aaba returns 1 (keep reading); 0x83a8ea; 0x83a900 | M1-002 | EXACT_SOURCE |
| B18 | The RCM ctor registers the UDP transport with WifiUtil; a signal handler calls ResetSocket (+0x9D=1); the next Update closes and, if the close succeeded, reopens on port 47817 (B38). [corrected C1] The trigger is G4. | 0x62edb8; 0x83bae0; 0x83a258; 0x83ace8–0x83acfe | M1-023 | EXACT_SOURCE for the reset; RECOVERABLE_GAP for the trigger (0x83ba34, WifiUtil::Native*Callback 0x83bb7d...) |
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

## Closure pass rows: CA (transport), CB (handshake), CC (idle, errors, reasons), CD (engine tick, setup), E (follow-up)

Rows from the closure pass of 2026-09-24. Where a closure row answers or corrects an earlier R, B or G row, the closure row supersedes it (see Corrections C6..C11).

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| CA1 | R25(a): the 2.0 ms gate is skipped when lastSend (+0x48) == 0.0 or when sPacketSeparationIntervalInMS <= 0. Otherwise it returns 0 only if now < last + interval (strict). | 0x008363D4 vcmpe d0,#0 / 0x008363E0 ble; 0x008363E2 vldr d1,[r6,#0x48] / 0x008363EE beq; 0x008363F4 vcmpe d8,d0 / 0x008363FC bmi 0x8364ec | M1-006 | EXACT_SOURCE |
| CA2 | The ctor sets +0x48 = 0.0; the only other write is now, after each send. It is never reset, so the exemption applies only before the first send. The interval is 2.0 (Init). | ctor 0x0083591C..0x00835922; 0x00835F72 → 0x00835F80 vstr d8,[sb,#0x48]; Init 0x0062F052..0x0062F05E | M1-006 | EXACT_SOURCE |
| CA3 | B17 TooSmall: prefix length (+2 with CRC) > size gives the warning "UDPTransport.BadPrefix.TooSmall" and AddRecvError(0) on the UDP stats (this+8). | 0x0083A780; 0x0083A7AA; 0x0083A7F0..0x0083A7F4 | M1-002 | EXACT_SOURCE |
| CA4 | B17 BadPrefix: a failed memcmp gives the warning "UDPTransport.BadPrefix" and AddRecvError(2). | 0x0083A80E; 0x0083A844; 0x0083A87E..0x0083A882 | M1-002 | EXACT_SOURCE |
| CA5 | B17 truncated: checked only after the prefix matches. MSG_TRUNC gives the warning "UDPTransport.Recv.Truncated" and AddRecvError(1). | 0x0083A888 cmp.w fp,#1; 0x0083A8C4..0x0083A8C8; flag from 0x0083AAA8 ubfx r0,r7,#5,#1 | M1-002 | EXACT_SOURCE |
| CA6 | B17 CRC, only if sDoesHeaderHaveCRC (off in the engine, Init 0x0062EFC2). A mismatch gives AddRecvError(3), then the warning "UDPTransport.Recv.BadCRC". | 0x0083A8E2; 0x0083A8EA; 0x0083A90E..0x0083A912; 0x0083A922 | M1-002 | EXACT_SOURCE |
| CA7 | AddRecvMessage(size) is counted for every datagram before any check. A pass goes to receiver(+4)->vtbl+8, and a null receiver means a silent drop. Error enum: 0 TooSmall, 1 TooBig, 2 WrongHeader, 3 BadCRC, 4 BadType, 5 OutOfOrder, 6 SendFailed. | 0x0083A776; 0x0083A8F4..0x0083A90C; table 0x01037868 | M1-002 | EXACT_SOURCE |
| CA8 | R35: Dispatch::Create(name,3) → Queue → TaskExecutor(name,3) starts two threads (Execute, ProcessDeferredQueue). If priority != 2 it calls SetThreadPriority on both. | 0x008367C0; 0x007FB89A; 0x007FBDBA/0x007FBDEC; 0x007FBE02..0x007FBE14 | M1-010, M1-021 | EXACT_SOURCE |
| CA9 | R35: SetThreadPriority uses policy SCHED_RR (SCHED_OTHER if prio == 2), with param = min + (int)((max-min) × 0.75f) for prio 3, via pthread_setschedparam. EPERM is silently ignored. So 3 means real-time SCHED_RR at 75% of the OS range. | 0x008334CC..0x00833532 (tbb 0x008334EC, bytes 02 05 08 17; vmov.f32 s0,#0.75 at 0x00833500) | M1-010 | EXACT_SOURCE for the request; whether it took effect on the phone is HARDWARE_ONLY |
| CA10 | R37: the posted time is stored at PendingMessage +0x00 (0x00835802). Its only reader on a message's first send feeds a stats accumulator (conn+0x70) used only in the timeout warning text. Nothing on the wire. | 0x00835FA6..0x00835FD8; 0x00836080..0x00836098 vs 0x00836166, 0x008361A8..0x008361B2 | M1-035 | EXACT_SOURCE within the transport |
| CA11 | R37: QueueMessage copies the buffer, captures now into closure+0x40 and posts via QueueAction. The closure calls SendMessage with that time, then frees the buffer. The wrapper locks this+0x24. | 0x00836B66..0x00836BE4; 0x00837DEA..0x00837E20; 0x00837F48..0x00837F66 | M1-035 | EXACT_SOURCE |
| CA12 | The queue is FIFO: Dispatch::Async → Wake → AddTaskHolder push_back; Run executes in order on one thread. | 0x007FB91A; 0x007FB990; 0x007FC7A0; 0x007FC886; 0x007FC95E; 0x007FCCF8..0x007FCD66; 0x007FD0C6..0x007FD128 | M1-035, M1-019 | EXACT_SOURCE |
| CA13 | R21/R22: every type-6 part carries the caller's +0x2B flag and the same posted time. | 0x00836D60; 0x00836DE8; 0x00836E98..0x00836EA8; Set 0x00835832; reader 0x00836368 | M1-009 | EXACT_SOURCE |
| CA14 | B21 Disconnect closure: SendMessage(1, addr, NULL, 0, type 3, flag 1, time 0.0), then DeleteConnection(addr). With no connection, FindConnection returns null and gives the "unconnected destination" warning; nothing is sent, and DeleteConnection is a no-op. | 0x00838004..0x0083801C (0x0083800A strd r1,r1,[sp,#0x10]); 0x0083802A; 0x00836C5A..0x00836C6E, 0x00836CDC..0x00836D06; 0x00837098..0x008370CA; 0x00837604..0x00837608 | M1-019 | EXACT_SOURCE |
| CA15 | Sync-mode QueueMessage: if +0xA0 != 0, SendMessage directly with time 0.0, with no mutex and no copy. | 0x00836B42..0x00836B5C | M1-035 | EXACT_SOURCE |
| CA16 | ChangeSyncMode(true) drops only the callback handle (+0x20) and keeps the queue (+0x1C). QueueAction does not check +0xA0, so Disconnect, Start and Stop are posted in both modes. | 0x00836862..0x0083687C; 0x0083836E..0x0083838E; 0x00836880..0x008368A8; 0x008383CE..0x008383DC; 0x00836FAC..0x00836FF6 | M1-010, M1-019 | EXACT_SOURCE |
| CA17 | ChangeSyncMode has one caller, the RT ctor with false, so sync mode is never active after construction. | 0x008367D8..0x008367E2; full .text call scan | M1-020 | EXACT_SOURCE (BL scan; indirect callers not excluded) |
| CA18 | R34 Update visits connections in ascending TransportAddress::operator< order. A timed-out connection gives the info log, OnDisconnected, delete, erase, and result false. | 0x00837B9A..0x00837C92 | M1-010, M1-018 | EXACT_SOURCE |
| CA19 | operator< compares the type byte first: '6' IPv6 < 'b' BLE < 'i' IPv4 < 'n' unset < 'v' virtual. For IPv4 it compares the u32 at +8 (raw sin_addr), then the u16 port (host order). IPv6 is memcmp of 16 bytes, then the port. | 0x00838EDA..0x00838F62; 0x008384E8..0x00838518 | M1-010, M1-018 | EXACT_SOURCE |
| CA20 | R39: StartClient, StopClient, StartHost and StopHost are each posted via QueueAction. StopClient → UDP vtbl+0x18, then ClearConnections. | 0x00837214, 0x008372A4, 0x0083731C, 0x008373AC; closures 0x0083808E, 0x008380F6..0x00838108 | M1-019 | EXACT_SOURCE |
| CA21 | UDP StopClient: if fd >= 0, CloseSocket (fd -1, port 47817). StartClient: only if fd == -1, OpenSocket(+0x98). So a Start after Stop binds 47817. | 0x0083AD66..0x0083AD70; 0x0083AD4C..0x0083AD5C | M1-019, M1-022 | EXACT_SOURCE |
| CA22 | FinishConnection = QueueMessage(1, addr, NULL, 0, type 2, flag 1). Connect clears +0xA1, then queues type 1. | 0x0083712A..0x0083713A; 0x0083710E..0x0083711C | M1-019 | EXACT_SOURCE |
| CA23 | R13: an empty container body means no creation, so "unconnected source". | 0x008377BC; 0x008377DC cmp.w fp,#0 | M1-018 | EXACT_SOURCE |
| CA24 | OpenSocket calls CloseSocket first, then stores +0x98 = the port argument. | 0x00839A3A; 0x00839A46 | M1-022 | EXACT_SOURCE |
| CA25 | getaddrinfo failure: error and return 0, with no CloseSocket. fd stays -1 and the port is kept. | 0x00839B06..0x00839B7A → 0x00839CF6 | M1-022 | EXACT_SOURCE |
| CA26 | socket() failure: store fd -1, error "OpenSocketFailed", CloseSocket (a no-op), return 0. | 0x00839B8A..0x00839B96; 0x00839C80..0x00839CF2 | M1-022 | EXACT_SOURCE |
| CA27 | SO_BROADCAST failure: error "SetBroadcastFailed", CloseSocket (port 47817), return 0. | 0x00839D00..0x00839D24; 0x00839F48..0x00839FA0 → 0x00839CE0 | M1-022 | EXACT_SOURCE |
| CA28 | A bind failure other than EADDRINUSE: error "BindFailed", CloseSocket (port 47817), return 0. | 0x00839D3A..0x00839D42; 0x00839E6A..0x00839E72; 0x00839FA2..0x0083A026 | M1-022 | EXACT_SOURCE |
| CA29 | EADDRINUSE: warning "BindInUse". The socket stays open but unbound, and it returns 1. | 0x00839E76..0x00839EB6; 0x00839E14..0x00839E66 | M1-022 | EXACT_SOURCE |
| CA30 | CloseSocket: fd < 0 → return 0 and store nothing. Otherwise close, log Success or error Failed, and on both paths set fd -1 and port 0xBAC9. It returns whether the close succeeded. | 0x008395CA..0x00839626; 0x00839694..0x008396A8 | M1-022 | EXACT_SOURCE |
| CA31 | Send: BuildPacket into 0x5C0 and AddSentMessage before sendto(fd, flags 0), with no fd guard. A partial result is an error "SentWrongNumBytes". | 0x0083A35E; 0x0083A36A; 0x0083A374..0x0083A386; 0x0083A38A..0x0083A3DA | M1-022 | EXACT_SOURCE |
| CA32 | A send failure gives AddSendError(6) and now. It warns "UDPTransport.SendFailed" only if verbose, or +0x88 == 0.0, or now > +0x88 + 30000.0, and only that path stores +0x88 = now. A non-IP address is an error with no count. | 0x0083A546..0x0083A56C; 0x0083A648..0x0083A666 (literal 0x0083A6C8); 0x0083A5F4; 0x0083A5FA..0x0083A642 | M1-022 | EXACT_SOURCE |
| CA33 | kEnableVerboseNetworkLogging is a const byte 0 in .rodata, so verbose is false. | 0x00C934A4; 0x0083A55A..0x0083A568 | M1-022 | EXACT_SOURCE |
| CA34 | The +0x88 clock is GetCurrentNetTimeStamp (ms, whole µs); it is initially 0.0 (ctor). | 0x008355F8..0x0083565E; 0x0083953C | M1-022 | EXACT_SOURCE |
| CA35 | B16, a 0-byte read: recvmsg(MSG_DONTWAIT) returns 0, the error path reads the current (stale) errno, and it warns iff errno != 11. If errno == 107 it closes and reopens. It always ends the drain for the tick. | 0x0083AA76; 0x0083AA98 cmp r5,#1 / blt; 0x0083AABE..0x0083AB24; 0x0083ABF4 | M1-022 | EXACT_SOURCE for the path; whether it warns is HARDWARE_ONLY |
| CA36 | UDP ctor: +0x88 0.0, +0x90 family 2, +0x94 fd -1, +0x98 port 0xBAC9, +0x9C/+0x9D 0. | 0x00839514..0x00839556; 0x0062ED56 | M1-022 | EXACT_SOURCE |
| CA37 | RCM::Init stores port 0 before start, so the first open is ephemeral. OpenSocket's callers: StartClient, StartHost, UDP Update (reset) and TryToReadMessage (ENOTCONN). | 0x0062EFE2..0x0062EFEA; 0x0062F078..0x0062F084; 0x0083AD5C, 0x0083AD2E, 0x0083ACFE, 0x0083AB34 | M1-022 | EXACT_SOURCE |
| CB1 | Unity sends exactly one ConnectToRobot (UTF-8 IP, NUL-terminated, isSimulated). There is no other handshake message and no handshake timeout in Unity. | RobotEngineManager.cs:510-528; ConnectionFlowController.cs:437, :670-674; ConnectingToCozmoScreen.cs:14 | M1-025 | EXACT_SOURCE (the absence is a text search) |
| CB2 | ConnectToRobot handler: if robot 1 exists, "Robot already connected" and return. Otherwise AddRobotConnection, then CozmoEngine::AddRobot(1): on failure an error; on success "Connected to robot!" (logged before any transport exchange), then NeedsManager::InitAfterConnection and DASPauseUploadingToServer(1). Nothing goes to the game. | 0x4ED026..0x4ED02C; 0x4ED074; 0x4ED07C; 0x4ED080; 0x4ED10E; 0x4ED114 | M1-025 | EXACT_SOURCE |
| CB3 | AddRobotConnection: port 5552 if msg+0x10, else 5551; TransportAddress(ip, port); RCM::Connect; return 0. | 0x69DE84..0x69DEA6 | M1-001 | EXACT_SOURCE |
| CB4 | RCM::Connect: RCD Clear; address to RCD+0x30; RT Disconnect(addr); RT Connect(addr); RCD state = 1. | 0x62F55E..0x62F58A | M1-019 | EXACT_SOURCE |
| CB5 | RM::AddRobot(id): if the id exists, warning only. Otherwise a new Robot (0x530 bytes), map insert, id list push, and a RIC emplaced in RM+0x70 with (id, MessageHandler, IExternalInterface, &RM+0x84, &RM+0x88). | 0x52EB7A..0x52EBB2; 0x52EE8E..0x52EF3A | M1-028, M1-029 | EXACT_SOURCE |
| CB6 | CozmoEngine::AddRobot: RM::AddRobot, then GetRobotByID; if found, audio input and multiplexer registration and return 0, else error and return 1. | 0x4ED1EC..0x4ED28C | M1-025 | EXACT_SOURCE |
| CB7 | RIC ctor: +0xC robotId; +0x10 responseSent 0; +0x14 IExternalInterface; +0x18 MessageHandler; +0x1C/+0x20 E_v/E_t; +0x24 serial 0; +0x28 hw 0xFFFFFFFF; +0x2C colour 0xFF; +0x2D validated 0; +0x2E robotAvailableSeen 0. There are no subscriptions without an IExternalInterface, and no timer or Update. | 0x52D184..0x52D1AE | M1-028 | EXACT_SOURCE |
| CB8 | Subscriptions, in order: 0xD2 → HandleFactoryFirmware, 0xEE → HandleFirmwareVersion, 0xC9 → HandleRobotAvailable. The handles go into the RIC vector. | 0x52D1B4..0x52D28E; 0x52D44C | M1-028 | EXACT_SOURCE |
| CB9 | RobotDataBackupManager also subscribes to robotAvailable (it reads the first u32 to find a backup file). Outside the handshake. | 0x519B50/0x519B78; 0x51A010..0x51A040 | M1-028 (interface) | EXACT_SOURCE for the subscription |
| CB10 | OnConnected → HandleConnectionResponseMessage: state 1 → 2, reason 0; otherwise an error event. No address compare, and nothing is sent. | 0x62F950..0x62F9E4 | M1-025 | EXACT_SOURCE |
| CB11 | Between OnConnected and validation the engine sends nothing to the robot and never asks for robotAvailable or firmwareVersion: the robot volunteers them. | absence in 0x52D168..0x52E3B8 | M1-028, M1-033 | EXACT_SOURCE for the engine side; the robot side is HARDWARE_ONLY |
| CB12 | HandleRobotAvailable sets +0x2E = 1, with no guard, and ignores the payload. | 0x52DC64..0x52DC6A | M1-028 | EXACT_SOURCE |
| CB13 | HandleFactoryFirmware: guard on +0x10/+0x14; info; event robot.factory_firmware_version "0"; OnNotified(3, 0). It does not check +0x2E. | 0x52D346..0x52D3C2 | M1-028 | EXACT_SOURCE |
| CB14 | HandleFirmwareVersion (G5.1..G5.13 confirmed). With no robotAvailable and not sim: error; +0x2D = 1; SendConnectionResponse(1, 0) called directly (not OnNotified, so no reason is set); then +0x2D = 0. | 0x52D7B8..0x52D850; 0x52D7C6..0x52D9B4 | M1-028, M1-029 | EXACT_SOURCE |
| CB15 | OnNotified(result, fw): 4 → reason 8, +0x2D = 0, response; 3 → reason 7, +0x2D = 0, response; any other value → +0x2D = 1, and if non-zero the response, if 0 → CB16. | 0x52DDAA..0x52DDFC | M1-028 | EXACT_SOURCE |
| CB16 | Success: +0x2D = 1 first; then subscribe to 0xED mfgId (lambda {this, 0, fw}); then send GetManufacturingInfo (E2R 0x25, empty) reliable=1, hot=0. No timer and no retry. | 0x52DDD0..0x52DE7C | M1-028 | EXACT_SOURCE |
| CB17 | The robot answers with mfgId 0xED: 3 × u32, Size 12. | 0x7B1750..0x7B1856 | M1-033 | layout EXACT_SOURCE; robot side HARDWARE_ONLY |
| CB18 | mfgId lambda: +0x24 = serial (word0); +0x28 = hw (word1); +0x2C = colour only if the low byte of word2 is in {0,2,3,4}, else an error and 0xFF; sets $session_id to a new UUID; SendConnectionResponse(0, fw); then ReadLabAssignmentsFromRobot(serial) and ConnectRobotToNeedsManager(serial). No +0x10 guard. | 0x52E304..0x52E3B2 | M1-028 | EXACT_SOURCE |
| CB19 | SendConnectionResponse: +0x10 = 1; releases every subscription handle; broadcasts RobotConnectionResponse {result u8, fwVersion u32, serial u32 +0x24, bodyHWVersion i32 +0x28, bodyColor i8 +0x2C} (14 bytes) through UiMessageHandler::Broadcast. It is the only producer of that message. | 0x52DF2E..0x52DF64; 0x52DFA4 | M1-028 | EXACT_SOURCE |
| CB20 | Field values by outcome. Success: v, serial, hw, colour. Outdated*: v, 0, -1, -1. Factory, parse failure or factoryFirmwareVersion: 0, 0, -1, -1. No robotAvailable: 0, 0, -1, -1. Disconnect before the response: 0, 0, -1, -1. Sim: 0 with fw = v (0). | CB13..CB19 | M1-028 | EXACT_SOURCE (derived) |
| CB21 | UiMessageHandler::Broadcast(E2G) delivers to the game first, then fires engine subscribers synchronously, so they run inside the mfgId handler before ReadLabAssignments. | 0x6625C6..0x6625FA; 0x662590..0x6625A8 | M1-028 | EXACT_SOURCE |
| CB22 | RobotEventHandler, on RobotConnectionResponse result 0: Robot::SyncTime, then an NVStorage one-shot-on-idle callback that sets Robot+0x2A = 1 (ready to stream animations). | 0x527870..0x52789A; 0x52C31E..0x52C32A; 0x5289BA..0x5289D2; 0x528A5A..0x528A6E; 0x52C3A6 | M1-041 NEW | EXACT_SOURCE |
| CB23 | Robot::SyncTime: +0x29 = 0; RobotStateHistory::Clear; SendSyncTime: SyncTime (0x4B, {u32 timestamp, u32 0xC1A00000}) reliable; if that succeeds, InitController (0x9F); if that succeeds, ImageRequest (0x4C, 0x0401) and AbsoluteLocalizationUpdate (origin pose). A send failure warns and stops. | 0x515222..0x5153AE | M1-041 NEW | EXACT_SOURCE |
| CB24 | Other RobotConnectionResponse subscribers are VisionComponent (SetCameraParams) and TracePrinter (SetAppRunID). The order was resolved by closure D (CD17). | 0x6583A4..0x65842C; 0x53D388..0x53D3DC | M1-041 | EXACT_SOURCE with CD17 |
| CB25 | Robot → engine filter: +0x2D != 0 passes everything. Otherwise only 0xC9, 0xD2, 0xEE and 0xEF pass (mask 0x30000001 over tag-0xD2 <= 0x1D); mfgId 0xED is filtered. | 0x52DC6C..0x52DC9A | M1-030 | EXACT_SOURCE |
| CB26 | Engine → robot filter: +0x2D != 0 passes everything, otherwise only 0xA9 shutdownRobot and 0xAF otaWrite. | 0x52DC9C..0x52DCB4 | M1-030 | EXACT_SOURCE |
| CB27 | RM::ShouldFilterMessage looks the RIC up by robot id; with no RIC, nothing is filtered. | 0x52FA90..0x52FAD2 | M1-030 | EXACT_SOURCE |
| CB28 | A filtered receive is dropped silently before Unpack; the loop continues. | 0x69D8D2..0x69D8F4 | M1-030 | EXACT_SOURCE |
| CB29 | MessageHandler::SendMessage always increments +0x3C and returns 1 silently if uninitialised, !IsValidConnection (RCD state != 2) or filtered. Callers decide (SendSyncTime warns). | 0x69DCFE..0x69DD2C; 0x62F694..0x62F6A0 | M1-026, M1-030 | EXACT_SOURCE |
| CB30 | Validation (+0x2D = 1) happens at OnNotified(0), before mfgId and the response. +0x2D is cleared again by OnNotified(3/4) and by RM::InitUpdateFirmware (the UpdateFirmware game message). MakeRobotFirmwareUntrusted has no static caller. | 0x52DDD0..0x52DDD4; 0x52F5C4..0x52F5CA; 0x52F6A2..0x52F6AC | M1-030 | EXACT_SOURCE (static calls) |
| CB31 | There are no handshake timers or retries: a robot that never sends firmwareVersion or mfgId leaves the engine waiting while pings keep the link up. Only the transport timeouts apply. | CB7, CB16, CB1 | M1-028 | EXACT_SOURCE |
| CB32 | Disconnect before the response: wasConnecting = (RCD state == 1) gives 2 ConnectionRejected (the transport connect was never answered); otherwise 1 ConnectionFailure. HandleDisconnect: if +0x10 is set or there is no IExternalInterface, return 0; else info, OnNotified(result, 0) (+0x2D = 1, response 0,0,-1,-1), return 1. | 0x62FA2E..0x62FAF8; 0x52F22C..0x52F244; 0x52DCC0..0x52DD1A | M1-015 | EXACT_SOURCE (contradicts B31's wording) |
| CB33 | RemoveRobot tail: if HandleDisconnect returned 1, skip OnRobotDisconnected, the RobotDisconnected broadcast and the $session_id clear. Otherwise do all three (RobotDisconnected{timeSinceLastMsg_sec 0.0}). Either way: NeedsManager/PerfMetric OnRobotDisconnected, DASPauseUploadingToServer(0), delete the Robot, erase the map, id and RIC, clear $phys/$group. | 0x52F248; 0x52F29E..0x52F364 | M1-015 | EXACT_SOURCE |
| CB34 | Results 1, 3 and 4 keep the link and Robot 1: nothing in RIC, OnNotified or SendConnectionResponse disconnects. Gating resumes (+0x2D = 0). | 0x52DD98..0x52DE9C; 0x52DF18..0x52DF80 | M1-028 | EXACT_SOURCE (absence) |
| CB35 | Unity's reactions: Success stores the identity; Failure → ReturnToSearch; OutdatedApp → modal; OutdatedFirmware → UpdateFirmware; Rejected → Retry/Cancel. NeedsPin, InvalidPin and PinMax are never produced by the engine. | RobotEngineManager.cs:373-392; ConnectionFlowController.cs:682-760 | M1-028 | EXACT_SOURCE |
| CB36 | After result 1, Unity's retry ConnectToRobot is ignored ("Robot already connected") while Robot 1 exists. | CB2 + CB34 + CB35 | M1-025 | EXACT_SOURCE (derived) |
| CB37 | Duplicates and out-of-order messages: a repeated robotAvailable is harmless; firmwareVersion before robotAvailable gives result 1; mfgId before acceptance is filtered; factoryFirmwareVersion after acceptance and before mfgId gives OutdatedFirmware fw 0; anything after the response hits the +0x10 guard; data before OnConnected gives "Connection not yet valid". firmwareVersion twice before mfgId is settled by closure E1. | CB8, CB12..CB19; 0x52D478..0x52D484 | M1-028 | EXACT_SOURCE with E1 |
| CB38 | The app layer does not filter connection events by address: types 1/2/3 are dispatched with no compare, and only HandleDataMessage compares against RCD+0x30. So a stray inbound type-1 from another address creates a connection whose 5 s timeout would remove Robot 1. | tbb 0x62F434; 0x62F490/0x62F498/0x62F4A0; 0x62F72C..0x62F748 | M1-025, M1-015 | EXACT_SOURCE for the app layer; the chain is derived |
| CC1 | RobotIdleTimeoutComponent is built in the Robot ctor at robot+0x51C. Its deadlines are +0x10 (faceOff) and +0x14 (disconnect), both -1.0f. It subscribes only with an external interface: game tag 84 CancelIdleTimeout and tag 83 StartIdleTimeout. | 0x510224; 0x52CC64..0x52CC7E; 0x52CCB8; 0x52CD78; MessageGameToEngine.cs:96-97 | M1-031 | EXACT_SOURCE |
| CC2 | StartIdleTimeout = {f32 faceOffTime_s, f32 disconnectTime_s}. | StartIdleTimeout.cs; 0x52CFF8; 0x52D030 | M1-031 | EXACT_SOURCE |
| CC3 | Start: now = BaseStationTimer seconds. faceOff is handled only if robot+0x34E != 0 and faceOff >= 0; the candidate now + faceOff is stored if the deadline is -1, else only if it is earlier. disconnect is handled if >= 0, with the same rule and no 0x34E gate. 0 is accepted. | 0x52CFE4..0x52D066 | M1-031 | EXACT_SOURCE |
| CC4 | robot+0x34E is set to 1 in UpdateFullRobotState only when robot+0x29 (time synced) != 0. HandleSyncTimeAck sets +0x29 = 1 (CD19). | 0x51293C..0x512948; 0x515228; 0x5366AC | M1-031 | EXACT_SOURCE with CD19 |
| CC5 | Cancel sets both deadlines to -1.0f. | 0x52D0BE..0x52D0C4 | M1-031 | EXACT_SOURCE |
| CC6 | Update runs first in Robot::Update. Sleep: if 0 < faceOff deadline <= now, set it to 0.0 and queue CreateGoToSleepAnimSequence on the ActionList. Then disconnect: if 0 < deadline <= now, set it to 0.0 and call MessageHandler::Disconnect. Both can fire in one Update, sleep first. | 0x513BE2..0x513BF2; 0x52CE3C..0x52CE98 | M1-031 | EXACT_SOURCE |
| CC7 | A deadline of 0.0 does nothing (Update needs > 0) and blocks re-arming until a Cancel. | CC3 + CC6 | M1-031 | EXACT_SOURCE (derived) |
| CC8 | The sleep sequence: Parallel{Sequential{GoToSleepGetIn, GoToSleepSleeping, GoToSleepOff}, MoveLiftToHeight(preset 0, tol 5.0)}. No robot message is sent by the component itself. | 0x52CEA2..0x52CFBE | M5 interface | EXACT_SOURCE for the structure |
| CC9 | MessageHandler::Disconnect is RCM::DisconnectCurrent; its only caller is idle Update. No reason is written on this path. | 0x69DCF0..0x69DCF2; 0x52CE98 | M1-031 | EXACT_SOURCE |
| CC10 | The idle clock is BaseStationTimer, updated only in engine state 3 once per 60 ms tick, before UpdateRobotConnection and UpdateAllRobots. State 4 checks no idle deadlines. | 0x84BC5A..0x84BCA8; 0x4ED5CC, 0x4ED61E..0x4ED648 | M1-031, M1-024 | EXACT_SOURCE |
| CC11 | Unity's values: background pause (20 / 40 s defaults), player-induced sleep (0 / 2), SDK modal and force boot (0, 0); Cancel on resume. | PauseManager.cs; DefaultSettingsValuesConfig.cs:44-60; NeedsConnectionManager.cs:305; SDKModal.cs:193 | M1-031 (app input) | Unity side; the serialized asset values are UNKNOWN |
| CC12 | RobotErrorReport (0xD9): u32 code, bool fatal, size 5. Codes 0 CrashReport, 1 I2SPI_TMD, 2 ReliableTransport, 3 I2SPI_Sync, 4 I2SPI_Lost, 5 NO_K02. | 0x7D3E66..0x7D3F40; 0x7D3810 | M1-027 | EXACT_SOURCE |
| CC13 | Fatal vs non-fatal is decided only by the fatal byte, never by the code. | 0x69D930 | M1-027 | EXACT_SOURCE |
| CC14 | Before validation, robotError is filtered (dropped before unpack): no event, no disconnect. | 0x69D8EE; 0x52DC6C..0x52DC9A | M1-027 | EXACT_SOURCE |
| CC15 | Fatal path: event robot.error.fatal → Broadcast to engine subscribers → RCM::ClearData (clears RCM+0x20) → DisconnectCurrent → loop exits. No game RobotErrorPassThrough and no reason. | 0x69D938..0x69DAEA | M1-027 | EXACT_SOURCE |
| CC16 | Non-fatal path: event robot.error.nonfatal → the game gets RobotErrorPassThrough{code} → Broadcast → the loop continues. | 0x69DA36..0x69DAAE | M1-027 | EXACT_SOURCE |
| CC17 | robotError's only engine reader is ProcessMessages. | callers scan 0x69D91E | M1-027 | EXACT_SOURCE (BL scan) |
| CC18 | RobotDisconnectReason: 0 Unknown, 1 WifiTimeout, 2 SleepSettings, 3 SleepEraseCozmo, 4 SleepPlacedOnCharger, 5 SleepBackground, 6 ExitSDKMode, 7 OutdatedFirmware, 8 OutdatedApp, 9 DebugForceDisconnect, 10 DebugDataPersistenceReset, 11 AppTerminated. | 0x775BEC, table 0x1033970 | M1-025 | EXACT_SOURCE |
| CC19 | Writers of RCM+0x38: ctor 0; sync-mode RT timeout 1; HandleConnectionResponse 0; HandleDisconnectMessage 1 (+0xA1) then 0; SetRobotDisconnectReason (OnNotified 7/8, ReactToOnCharger 4); game tag 88; game tag 242 → 6. RCM::Connect does not reset it. | 0x62ED7E; 0x62F1CC; 0x62F95E; 0x62FA3C, 0x62FAE2; 0x69DECE; 0x69E24A; 0x69E1F0 | M1-025 | EXACT_SOURCE (immediate-offset scan) |
| CC20 | The reason's only reader is the DAS disconnect event, so it changes no behaviour. | 0x62FAAA | M1-025 | EXACT_SOURCE (immediate-offset scan) |
| CC21 | Outdated firmware or app sets reason 7/8 and keeps the link; RIC does not disconnect. | 0x52DDAA..0x52DE7C | M1-028 | EXACT_SOURCE |
| CC22 | ExitSdkMode (tag 242: isExternalSdkMode, isEduMode): MessageHandler sets reason 6 + DisconnectCurrent if isExternalSdkMode; UiMessageHandler handles the SDK state. Subscriber order is settled by closure E2. | 0x69E1DC..0x69E1FE; 0x661538..0x661566; ExitSdkMode.cs | M1-025 | EXACT_SOURCE with E2 |
| CC23 | HandleDisconnectMessage ignores the message fields and captures RCD state first. +0xA1 gives reason 1. A DAS event with the reason and battery follows, then reason = 0, then RCD::Clear, then RemoveRobot(id, state == 1) if a robot exists. It runs even with state 0. | 0x62FA2E..0x62FAF8; 0x62E4CC..0x62E50A | M1-015 | EXACT_SOURCE |
| CC24 | Result 2 ConnectionRejected means RCD state == 1 (the transport connect was never answered). A disconnect during the handshake (state 2) gives 1 ConnectionFailure. | 0x62FAEE..0x62FAF4; 0x52F23A..0x52F242 | M1-015 | EXACT_SOURCE (contradicts B31's wording) |
| CC25 | RIC::HandleDisconnect: 0 if the response was already sent or there is no interface; else OnNotified(result, 0), response {result, 0, 0, -1, -1}, return 1. | 0x52DCC0..0x52DD1A; 0x52DF2E..0x52DF64 | M1-015 | EXACT_SOURCE |
| CC26 | RemoveRobot, as in CB33. | 0x52F238..0x52F360 | M1-015 | EXACT_SOURCE |
| CC27 | A subsequent ConnectToRobot is allowed once robot 1 is gone, and builds everything afresh. The reason byte carries over. There is no automatic reconnect. | 0x4ED026..0x4ED07C; 0x62F554..0x62F58A | M1-025 | EXACT_SOURCE |
| CC28 | The CozmoContext dtor calls RemoveRobot and the RCM dtor calls DisconnectCurrent (shutdown only). | 0x4EAABE; 0x62EE98 | M1-025 | EXACT_SOURCE |
| CC29 | DisconnectCurrent: RT Disconnect(RCD+0x30), which queues type 3 then DeleteConnection (no OnDisconnected from the transport). Then QueueConnectionDisconnect pushes an OnDisconnected marker onto the RCD queue on the engine thread, then queue-stats DAS. No state check; exactly one disconnect is delivered. | 0x62EF32..0x62EF7A; 0x837FFA..0x83802A; 0x8375F0..0x83761E; 0x62E534..0x62E542 | M1-025 | EXACT_SOURCE |
| CC30 | The marker is handled at the next RCM::Update → ProcessArrivedMessages, in FIFO order; RCD::Clear drops later items. On the idle and fatal paths that is the next tick. Game messages are dispatched synchronously in UiMessageHandler::Update (CD7), so ExitSdkMode's is also handled at that tick's UpdateRobotConnection. | 0x62F3BC..0x62F4BC; CD7 | M1-025 | EXACT_SOURCE with CD7 |
| CC31 | Data left in RCM+0x20 after removal is dropped (no robot). | 0x69D8D2..0x69D8D8 | M1-027 | EXACT_SOURCE |
| CC32 | ProcessMessages exactly: if MH+0x28 == 0 skip everything; else RCM::Update, PopData FIFO, MH+0x38++. No robot → drop; size 0 → error; filtered → drop; unpack size mismatch → error and drop; then CC15, CC16 or Broadcast. | 0x69D870..0x69D9FC | M1-027 | EXACT_SOURCE |
| CC33 | ShouldFilterMessage: no RIC means no filter. | 0x52FA90..0x52FAD0 | M1-027, M1-030 | EXACT_SOURCE |
| CC34 | The RIC filter lists, as CB25 and CB26. | 0x52DC6C..0x52DCB4 | M1-030 | EXACT_SOURCE |
| CC35 | Unpack of a tag outside 0xB0..0xF5 returns bytesRead 1, so a 1-byte unknown-tag message passes and is broadcast; a longer one is a size error. | 0x7B1ADC..0x7B1BCA | M1-027 | EXACT_SOURCE |
| CC36 | Broadcast builds AnkiEvent{BaseStationTimer seconds, tag, msg} and emits per tag. | 0x69DC82..0x69DE74 | M1-027 | EXACT_SOURCE |
| CD1 | The tick uses steady_clock. runStart = now; the first tick runs at once, and the first target is runStart + 60 ms. | 0x65B3B8; 0x65B3D2/0x65B3D8; 0x65B3E0..0x65B3EA | M1-024 | EXACT_SOURCE |
| CD2 | The Update argument is the ns elapsed since runStart at tick start. CozmoInstanceRunner::Update holds mutex +0x14 around CozmoEngine::Update; a nonzero result logs and stops the loop. | 0x65B3F4..0x65B42C; 0x65BB0C..0x65BB34; 0x65B704..0x65B712 | M1-024 | EXACT_SOURCE |
| CD3 | After Update: rem = target - now. It sleeps rem_us only if rem_ns >= 1000. Fixed rate: next target = old target + 60 ms, so overrun ticks run back to back until caught up. | 0x65B432..0x65B45E; 0x65B576..0x65B5D2; 0x65B5E0..0x65B5F0 | M1-024 | EXACT_SOURCE |
| CD4 | "overtime" is log only: a debug line below -10 ms, and a warning below -200 ms. | 0x65B478..0x65B54E | M1-024 | EXACT_SOURCE |
| CD5 | "catchup" skips ticks: if rem <= -240 ms, the target moves forward by floor(-rem/60 ms) × 60 ms. It never runs extra ticks. RegisterEngineTickPerformance is telemetry. | 0x65B5DC..0x65B63E; 0x65B680/0x65B70A; 0x65B700 | M1-024 | EXACT_SOURCE |
| CD6 | CozmoEngine::Update pre-switch: not initialised → error; first call → SetMainThread; the UI, MessageHandler and VizManager counters are zeroed; UiMessageHandler::Update runs, and a failure stops the runner. | 0x4ED4DE..0x4ED51C | M1-024 | EXACT_SOURCE |
| CD7 | UiMessageHandler::Update, in every state: ping; ProcessMessages (game → engine messages dispatched synchronously here, so a robot send they trigger happens before UpdateRobotConnection); UI device connects; the deferred queues; UpdateSdk. | 0x662A66..0x662C50 | M1-024 | EXACT_SOURCE |
| CD8 | State switch (tbb 0x4ED5CC): 1 → SendSupportInfo → 2; 2 → DoNonConfigDataLoading → BroadcastAvailableAnimations → 3; 3 → BaseStationTimer::UpdateTime → UpdateRobotConnection → NeedsManager::Update → UpdateAllRobots → UpdateLatencyInfo; 4 → UpdateRobotConnection + UpdateFirmware. Then UpdateAudioController. The engine clock advances only in state 3. | 0x4ED5D6..0x4ED6C4 | M1-024 | EXACT_SOURCE |
| CD9 | UpdateTime stores ns, double seconds, float seconds, dt and tick count. GetCurrentTimeStamp = (u32)(seconds × 1000). Every engine timestamp within a tick equals the tick start. | 0x84BC38..0x84BCD4 | M1-024 | EXACT_SOURCE |
| CD10 | UpdateRobotConnection tail-calls MessageHandler::ProcessMessages; all robot-message handlers run synchronously in it. | 0x52F850..0x52F856 | M1-024 | EXACT_SOURCE |
| CD11 | UpdateAllRobots calls Robot::Update for each robot, then broadcasts RobotState to the game if one has been received. | 0x52F6E0..0x52F774 | M1-024 | EXACT_SOURCE |
| CD12 | Robot::Update: the idle component always runs, then the CD19 check. If +0x34E (first full state handled) is 0, it returns ("Waiting for first full robot state to be handled"). After that: ActionList, then AnimationStreamer::Update only if +0x29 (time synced) and +0x2A (ready to stream), then NVStorage, Path, BlockFilter, object connection, map, cube lights and body lights. | 0x513BF2..0x514470 | M1-041 NEW | EXACT_SOURCE |
| CD13 | There is no per-tick batching of outgoing messages. Robot::SendMessage → MessageHandler → RT::SendData (reliable 4) → QueueMessage posts a closure at once (R37); packing is decided only by the transport. | 0x5134A4..0x5134B8; 0x8370EC..0x8370FC; 0x836B42..0x836B66 | M1-026 | EXACT_SOURCE |
| CD14 | Before validation the engine sends no app message; the Robot ctor queues NV reads that cannot go out before CD23. | CB26, CB2 | M1-030 | EXACT_SOURCE |
| CD15 | OnNotified(0): +0x2D = 1; subscribe mfgId; send GetManufacturingInfo (reliable, hot 0). The trigger is an accepted firmwareVersion. +0x2D = 1 is also set for any result other than 3 or 4 before the response. | 0x52DDD0..0x52DE7C | M1-028 | EXACT_SOURCE |
| CD16 | The mfgId handler calls SendConnectionResponse(Success), then ReadLabAssignmentsFromRobot and ConnectRobotToNeedsManager (NV reads). | 0x52E3A2; 0x52E3AA; 0x52E3B2 | M1-028 | EXACT_SOURCE |
| CD17 | SendConnectionResponse → UiMessageHandler::Broadcast: game first, then engine subscribers synchronously in subscription order: 1. RobotEventHandler (engine init), 2. VisionComponent (Robot ctor), 3. TracePrinter (later in the Robot ctor). | 0x52DF3A..0x52DF64; 0x6625C6..0x6625FA; 0x663C74..0x663CA8; 0x5259AC/0x52789A; 0x6501E4/0x650BE6; 0x53BCBA/0x53CECE | M1-041 | EXACT_SOURCE for the order (pattern-scanned subscriber set) |
| CD18 | RobotEventHandler (result 0): Robot::SyncTime: +0x29 = 0, history clear, SendSyncTime; on success +0x520 = now. SendSyncTime, all reliable: 1. SyncTime {u32 BaseStationTimer ms, 4 bytes 0xC1A00000}; 2. only if 1 was sent, InitController (empty); 3. only if 2 was sent, ImageRequest {1 Stream, 4 QVGA}; 4. log "Setting pose to (0,0,0)", then AbsoluteLocalizationUpdate {ts 0, frameId robot+0x2B0, originId world origin, 0, 0, 0}. A send failure warns "FailedToSend" and skips the rest. | 0x5289BA..0x5289D2; 0x51521E..0x5153AE; 0x512774..0x5127B6 | M1-041 | EXACT_SOURCE |
| CD19 | SyncTime is never retried. If +0x520 > 0 and now > +0x520 + 5.0 s, it warns "SyncTimeAckNotReceived" and sets +0x520 = 0. HandleSyncTimeAck sets +0x520 = 0 and +0x29 = 1 and sends nothing. | 0x513BF6..0x513C5A; 0x5366A4..0x5366AC | M1-041 | EXACT_SOURCE |
| CD20 | RobotEventHandler then adds an NVStorage one-shot on-idle callback that sets +0x2A = 1. Callbacks run only when the NV request deque is empty and the NV state is 0, so the AnimationStreamer starts only after every queued NV request has drained. | 0x528A5A..0x528A6E; 0x52C394..0x52C3A6; 0x645C20..0x645C32; 0x645B10..0x645B26; 0x6456CA..0x6456EC | M1-041 | EXACT_SOURCE |
| CD21 | VisionComponent (result 0) queues NV Read CameraCalib and sends SetCameraParams {bytes 0..5 uninitialised stack, byte 6 = 1}. On DefaultCameraParams: if the static exposure 16 is within [msg+8, msg+0xA], it sends SetCameraParams {gain, exposure 16, byte6 0}; otherwise "BadInitialExposureTime" and nothing. | 0x6583BA..0x65842C; 0x657CA0..0x657DA2 | M3 interface | EXACT_SOURCE; bytes 0..5 UNKNOWN |
| CD22 | TracePrinter (result 0) sends SetAppRunID (16-byte UUID, all 0xFF unless DAS gives a platform id), then RequestCrashReports{0}. Each crashReport received triggers the next index up to 3. | 0x53D398..0x53D430; 0x53D318..0x53D342; 0x53CA88..0x53CA9E | M1-041 | EXACT_SOURCE |
| CD23 | Robot state before sync is dropped: UpdateFullRobotState returns 0 while +0x29 == 0. The first state after SyncTimeAck sets +0x34E = 1, which opens CD12. | 0x51293C..0x51294E; 0x513054 | M1-041 | EXACT_SOURCE |
| CD24 | SetCliffDetectThreshold sends 50, then 400 after 50 mm driven; the charger platform also switches it. | 0x50FEEA..0x512EA6; 0x511D92..0x511DA0 | M4 interface | EXACT_SOURCE for the sends; the path condition is RECOVERABLE (M4) |
| CD25 | SetBodyRadioMode {ACCESSORY} after 16 states with IS_BODY_ACC_MODE clear. | 0x512AD8..0x512B52 | M4 interface | EXACT_SOURCE |
| CD26 | NV reads are queued at Robot construction (progression, inventory, face album, data backup) and at connection (camera calib, lab assignments, needs), and go out one at a time FIFO after CD23. | 0x51036C..0x5103D4; 0x52E3AA; 0x52E3B2; 0x644FEE | NV subsystem interface | EXACT_SOURCE for the triggers; the NV wire format is outside M1 |
| CD27 | Unity sends SetRobotVolume on any RobotConnectionResponse; the engine sends SetAudioVolume {u16 vol × 65535}. | ConnectionFlowController.cs:682-686; GameAudioClient.cs; 0x59A21E..0x59A256 | M3 interface (app input) | EXACT_SOURCE |
| CD28 | Block pool: the engine sends no robot message at connection. Unity sends GetBlockPoolMessage and BlockPoolEnabledMessage. The only robot-level cube message on this path is SetPropSlot from ConnectToRequestedObjects. | RobotEngineManager.cs:373-378; BlockPoolTracker.cs:86-104; ConnectionFlowController.cs:764-780 | M4 interface (app input) | EXACT_SOURCE for the boundary |
| CD29 | Backpack lights: each Robot::Update sets the best config or Off when it changes; with no config both are null and Off is sent every tick. | 0x631E38..0x631C36 | M4 interface | EXACT_SOURCE for the mechanism |
| CD30 | No direct call sites for SendHeadAngleUpdate or setAccessoryDiscovery; enableColorImages only from BehaviorTrackLaser; Unity never sends SetRobotImageSendMode. | BL scan | M1-041 | EXACT_SOURCE within the BL scan |
| CD31 | Other Unity post-connect game messages were not traced. Unity is replaced by this stack's API. | — | app layer | out of M1 scope (policy M1-042) |
| E1 | A second firmwareVersion before mfgId passes the guard (+0x10 is set only by SendConnectionResponse). If accepted again, it subscribes a second mfgId lambda and sends a second GetManufacturingInfo. | 0x52D478..0x52D484; 0x52DF2E; 0x52DDD0..0x52DE7C | M1-028 | EXACT_SOURCE |
| E2 | Robot→engine subscriptions append at the tail of a per-(tag, robotId) signal ring. The Robot's own HandleRobotSetBodyID is subscribed to 0xED before the RIC (Robot ctor before RIC emplace), so the ring is [SetBodyID, lambda A, lambda B]. | 0x51CD34; 0x51D56C..0x51D8A0; 0x532CD0..0x532CDA; 0x52EE96..0x52EF08 | M1-028 | EXACT_SOURCE |
| E3 | The emit calls a link only if its function is set, and reads next after the call. Releasing a handle unlinks it at once (the ScopedHandleContainer dtor → ProtoHandle → unlink, which nulls the function and relinks the neighbours). | 0x69E27C..0x69E2B2; 0x4EF1B8..0x4EF1CC; 0x51D8F8..0x51D932; 0x51D338..0x51D3A4 | M1-028 | EXACT_SOURCE |
| E4 | So on the first mfgId: SetBodyID runs, then lambda A. SendConnectionResponse inside A pops the handle vector from the back, unlinking B and then A, and the emit never reaches B. The result is exactly one RobotConnectionResponse and one set of post-connect sends. | 0x52DF2E..0x52DF64; 0x52DFA4..0x52DFC8; E2, E3 | M1-028 | EXACT_SOURCE (derived) |
| E5 | A second mfgId finds no RIC subscriber and is not filtered (+0x2D = 1), so only HandleRobotSetBodyID runs again. A later firmwareVersion is ignored. | 0x52DC6C | M1-028 | EXACT_SOURCE |
| E6 | HandleRobotSetBodyID logs, sets $phys, stores the mfgId words at Robot+0x1C/+0x24, then calls SetBodyColor and AutoActivateExperiments. There is no send in its body. | 0x536FB0..0x53706C | M1-028 | EXACT_SOURCE (callees not read) |
| E7 | Game→engine tag 242 (ExitSdkMode) emits to its subscribers in this order: UiMessageHandler::OnExitSdkMode (engine init), then MessageHandler's lambda (RobotManager::Init), then MovementComponent (per Robot, always last). | 0x660918..0x660944; 0x69D3DA..0x69D70E; 0x63DB1E..0x63DE96; 0x663954..0x663992 | M1-025 | EXACT_SOURCE (heuristic subscriber scan) |
| E8 | MessageHandler's lambda: if isExternalSdkMode, reason 6 and DisconnectCurrent, which posts RT Disconnect and enqueues the OnDisconnected marker without changing the RCD state. So MovementComponent still runs in the same emit. | 0x69E1DC..0x69E1FE; 0x62EF20..0x62EF7A; 0x837144..0x83719A | M1-025 | EXACT_SOURCE |
| E9 | MovementComponent may then send EnableAnimTracks. It passes IsValidConnection (state is still 2) and goes to QueueMessage, which posts after the already-posted Disconnect action. The FIFO queue (CA12) runs the Disconnect first, which deletes the connection (CA14), so the EnableAnimTracks SendMessage finds no connection: an "unconnected destination" warning, and nothing is sent. | 0x64053A..0x640570; 0x63FE92..0x63FFB4; 0x69DCF6; 0x62F694; CA12; CA14 | M1-025 | EXACT_SOURCE (derived from CA12 and CA14) |

