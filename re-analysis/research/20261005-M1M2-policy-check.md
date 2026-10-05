| Record | Coverage | Checked surface |
|---|---|---|
| M1-013 | CHECKED | Current record, shipped decision and external boundary. |
| M1-014 | CHECKED | Current record, shipped decision and external boundary. |
| M1-022 | CHECKED | Current record, shipped decision and external boundary. |
| M1-034 | CHECKED | Current record, shipped decision and external boundary. |
| M1-036 | CHECKED | Current record, shipped decision and external boundary. |
| M1-037 | CHECKED | Current record, shipped decision and external boundary. |
| M1-038 | CHECKED | Current record, shipped decision and external boundary. |
| M1-039 | CHECKED | Current record, shipped decision and external boundary. |
| M1-040 | CHECKED | Current record, shipped decision and external boundary. |
| M1-042 | CHECKED | Current record, shipped decision and external boundary. |
| M2-017 | CHECKED | Current record, shipped decision and external boundary. |

# M1 / M2 policy boundary check - 2026-10-05

Answers the operator request in this chat beginning "Pull first. One task, research lane only". Read PROJECT_STATE.md first. Pull returned Already up to date. HEAD 358e77885b680913c1ad0d420ce946407345b01b. Current manifest scan found exactly these eleven COMPATIBILITY_POLICY / EQUIVALENT_IMPLEMENTATION records in M1-transport and M2-protocol. None omitted.

CHECKED means the package-versus-host question was checked, not complete subsystem verification or fidelity acceptance. UNKNOWNs are explicit limits, including residual/freed memory and unshipped phone OS implementations. No code, inventory, manifest or state changes; no tests/hardware, branches, worktrees, commits or pushes. The broader research queue stays paused.

AGENTS.md "Exact, always" says:

> Whatever ships in the APK or the OBB is reproduced exactly, however much extraction it takes. EQUIVALENT_IMPLEMENTATION is only for behaviour whose code doesn't ship at all, such as the phone's system libraries.

September 24 decisions remain quoted as history. They do not establish an outside-package boundary under the later September 27 exact rule and this request. Shipped decisions and OS primitives they call need separate provenance. Unity code, embedded libraries, crashing paths, and undefined behavior are not absent code.

Primary artifacts: resources/lib/armeabi-v7a/libcozmoEngine.so, 17,139,336 bytes, SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; shipped libc++_shared.so, 677,448 bytes; extracted Java/Unity source, manifest and OBB assets. Addresses are ELF virtual addresses, Thumb state bit removed. [Instruction companion](20261005-M1M2-policy-native.txt). Linear dumps can include literal pools at range ends; only identified instructions cited in this report are claims. Prior inventories/Ghidra were navigation aids, not substituted for checked instructions.

The engine's ELF imports socket, bind, sendto, recvmsg, close, pthread_setschedparam and clock_gettime as undefined symbols and NEEDs libc.so. The extracted resources contain no libc.so. Their system implementations are external. libc++_shared.so and Breakpad are present in the package.

## Current record quotations

Every full current record is quoted below before any per-record assessment. Manifest snapshot SHA256 5aeb86b0d3b1018dfb9efd99c2311de1139a205221c661124b752acdff776830.

### M1-013

```json
{
  "id": "M1-013",
  "subsystem": "M1-transport",
  "title": "Windows high-resolution timer realising the 2 ms and 60 ms periods",
  "location": "cozmo-stack/src/Cozmo.Transport/HighResolutionTimer.cs",
  "effect": "jitter in the 2 ms transport tick (M1-010) and the 60 ms engine tick (M1-024)",
  "provenance": "a choice of this stack",
  "authority": "the original uses a Dispatch repeating callback (M1-010) and a sleeping 60 ms engine thread (M1-024) on Android. The periods themselves are recorded there as behaviour to reproduce; only the host timer mechanism is policy",
  "evidence": [],
  "status": "COMPATIBILITY_POLICY",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": null
}
```

### M1-014

```json
{
  "id": "M1-014",
  "subsystem": "M1-transport",
  "title": "Host thread structure that realises the engine threading",
  "location": "cozmo-stack/src/Cozmo.Transport/RobotLink.cs",
  "effect": "ordering and latency of delivered messages, if the host threads do not keep the orders recorded in M1-010, M1-024 and M1-035",
  "provenance": "a choice of this stack",
  "authority": "the original: a RelTransport dispatch thread (M1-010, M1-035) and one engine thread draining arrivals every 60 ms (M1-024). Its socket reopen on ENOTCONN and its send-failure handling are M1-022, and handler isolation is M1-034. Only the host thread structure that reproduces those orders is policy. That includes thread priority: the original asks for SCHED_RR at 75% of the OS range for the RelTransport threads (CA9), which the host does not reproduce; host threads keep the default priority",
  "evidence": [],
  "status": "COMPATIBILITY_POLICY",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportHardeningTests"
}
```

### M1-022

```json
{
  "id": "M1-022",
  "subsystem": "M1-transport",
  "title": "UDP socket: setup, ephemeral local port, send errors, receive loop, reopen",
  "location": "cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs",
  "status": "EQUIVALENT_IMPLEMENTATION",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "B11 OpenSocket: getaddrinfo(NULL, port, AI_PASSIVE, SOCK_DGRAM, AF_INET) (0x00839ACA, 0x00839AD0, 0x00839AF2); socket (0x00839B8A); SO_BROADCAST=1 for AF_INET (0x00839D18); bind (0x00839D3A); EADDRINUSE only a warning (0x00839E70); no O_NONBLOCK, no buffer options",
    "B12 local port: ctor 0xBAC9 (0x00839546) but Init stores 0 (0x0062EFEA str.w r5,[r6,#0x98]), so bind is ephemeral; RT->StartClient opens it (0x0062F07E, 0x0083808E, 0x0083AD58)",
    "B10 sendto flags 0 (0x0083A37C); partial send logs SentWrongNumBytes (0x0083A39A); failure: AddSendError(6) (0x0083A552), time at +0x88 (0x0083A5F4), no retry, no disconnect; non-IP addresses refused (0x0083A606)",
    "B16 receive loop while TryToReadMessage returns 1 (0x0083AD10..0x0083AD18); recvmsg MSG_DONTWAIT into 0x5C0 (0x0083AA66, 0x0083AA76 movs r3,#0x40); <= 0 stops (0x0083AA98); errno != EAGAIN warns (0x0083AAC4); ENOTCONN closes and, only if the close succeeded (0x0083AB2C cbz r0), reopens on +0x98, which CloseSocket has just set to 0xBAC9 = 47817 (0x0083AB22 cmp r0,#0x6b, 0x0083AB2E, 0x0083AB34; CloseSocket 0x00839694..0x0083969C on both paths); only the first open is ephemeral [corrected C1]",
    "B38 close failures logged; CloseSocket resets fd to -1 and port to 0xBAC9 (0x00839694..0x0083969C)",
    "CA24..CA30: OpenSocket calls CloseSocket then stores the port argument (0x00839A3A, 0x00839A46); getaddrinfo failure returns 0 with no close (0x00839B06..0x00839B7A); socket() failure stores fd -1 (0x00839B8A..0x00839CF2); SO_BROADCAST and non-EADDRINUSE bind failures close via CloseSocket, so the port becomes 47817 (0x00839D00..0x00839D24, 0x00839E6A..0x0083A026); EADDRINUSE keeps the socket unbound and returns 1 (0x00839E76..0x00839EB6); CloseSocket with fd < 0 returns 0 and stores nothing (0x008395CA..0x00839626)",
    "CA31..CA34: sendto has no fd guard (0x0083A374..0x0083A386); SentWrongNumBytes is an error log (0x0083A38A..0x0083A3DA); a send failure always counts AddSendError(6), but warns and stores +0x88 only on the first failure or when now > +0x88 + 30000.0 (0x0083A648..0x0083A666, literal 0x0083A6C8), kEnableVerboseNetworkLogging being const false (0x00C934A4); +0x88 starts at 0.0 (0x0083953C) on GetCurrentNetTimeStamp",
    "CA35: a 0-byte read ends the drain for the tick through the error path, which reads the stale errno (0x0083AA98 blt → 0x0083AAC4..0x0083AB24); whether it warns is M1-043",
    "CA36/CA37: UDP ctor values (0x00839514..0x00839556); Init stores port 0 so the first open is ephemeral (0x0062EFEA)"
  ],
  "effect": "the robot sees a different source port, or the link behaves differently after an error",
  "provenance": "reproduced with host socket mappings: MSG_DONTWAIT as Poll(0)+ReceiveFrom, ENOTCONN as SocketError.NotConnected, EADDRINUSE as AddressAlreadyInUse, MSG_TRUNC as MessageSize, a throwing Close as a failed close; getaddrinfo (CA25) has no host step; CloseSocket's success info log is not emitted; settled in batch 4(i)",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportRepairTests.M1_022_B12_TheFirstOpenIsEphemeralAndAnOpenAfterACloseBinds47817, TransportRepairTests.M1_022_B11_TheSocketIsIpv4UdpOnInaddrAnyWithSoBroadcastAndBlocking, TransportRepairTests.M1_022_B11_AnAddressInUseBindIsOnlyAWarningAndTheSocketIsKept, TransportRepairTests.M1_022_B16_ANotConnectedReopenIsSkippedWhenTheCloseFailed, TransportRepairTests.M1_022_B16_AZeroByteDatagramStopsTheDrainForThisUpdate, TransportRepairTests.M1_022_B16_OnTheHostAZeroByteDatagramStopsTheDrainAndAnEmptyReadDoesNotBlock, TransportRepairTests.M1_022_B10_AShortSendLogsSentWrongNumBytes, TransportRepairTests.M1_022_B10_AFailedSendCountsAddSendError6AndDoesNotRetryOrDisconnect, TransportRepairTests.M1_022_B10_ASendWithNoSocketCountsAddSendError6, TransportRepairTests.T_k1_NotConnectedReopensTheSocketOnPort47817AndKeepsTheConnection, TransportRepairTests.T_k2_AnyOtherReceiveErrorIsOnlyAWarning, TransportRepairTests.T_k3_WouldBlockIsSilent, TransportRepairTests.M1_022_CA24_CA26_CA27_CA28_AFailedOpenSocketStepLeavesNoSocket, TransportRepairTests.M1_022_CA32_TheSendFailureWarningAndItsTimeAreRateLimitedTo30Seconds"
}
```

### M1-034

```json
{
  "id": "M1-034",
  "subsystem": "M1-transport",
  "title": "Handler isolation, a deliberate departure: in the original a handler exception aborts the engine process",
  "location": "cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs",
  "status": "COMPATIBILITY_POLICY",
  "authority": "the original has no handler isolation: nothing on the dispatch path catches, and an exception in any message handler reaches std::terminate and aborts the engine process (rows G3.1..G3.17, cited in evidence). The replacement deliberately isolates handlers instead, by operator decision D6; this record exists so the departure stays visible and is never mistaken for reproduced behaviour",
  "evidence": [
    "G3.1..G3.7 the dispatch path from the signal emit to ProcessArrivedMessages and ProcessMessages has only cleanup landing pads that end in _Unwind_Resume (LSDAs 0x00B3F07C, 0x00B3F124, 0x00B3EF3C, 0x00B28A14); the only catch(...) pads go to 0x004E39F8 = __cxa_begin_catch; std::terminate (0x004E39FA, 0x004E39FE)",
    "G3.8 no frame up to the engine thread top catches (CozmoEngine::Update 0x004ED62E, CozmoInstanceRunner::Run 0x0065B420, __thread_proxy 0x0065C280)",
    "G3.10..G3.14 the EH runtime is the shipped libc++_shared.so (NEEDED order libc, libm, libc++_shared); __cxa_throw with no handler calls std::__terminate with the default handler (libc++_shared.so 0x0007A948..0x0007A98C, 0x0007B20C, reloc 0x000A6010 -> 0x00068B7D)",
    "G3.15/G3.16 the default handler writes \"terminating with uncaught exception of type T: what()\" to stderr and aborts (libc++_shared.so 0x00068B84..0x00068C4A, abort_message 0x00068AA2..0x00068ACE)",
    "G3.17 the engine never installs another terminate handler: its only set_terminate call is set_terminate(nullptr) in cozmo_shutdown (0x00667862..0x00667864)"
  ],
  "effect": "a handler exception is survived here where the app would abort",
  "provenance": "operator decision D6 (2026-09-24): this stack isolates message handlers",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportRepairTests.M1_034_RobotLinkIsolatesEachStateAndMessageSubscriber, TransportHardeningTests.AHandlerThatThrowsIsCountedAndTheOthersStillRun, TransportHardeningTests.AThrowingHandlerDoesNotStopLaterMessagesBeingDelivered"
}
```

### M1-036

```json
{
  "id": "M1-036",
  "subsystem": "M1-transport",
  "title": "Crash reporting after an engine-thread abort",
  "location": "cozmo-stack/src/Cozmo.Transport/RobotLink.cs",
  "effect": "what is left behind when the engine dies: a minidump in the original, the host default here",
  "provenance": "a choice of this stack",
  "authority": "the original installs Google Breakpad from CozmoActivity.onCreate when HOCKEYAPP_APP_ID is set (sources/com/anki/cozmo/CozmoActivity.java:50-53, 105-114; resources/AndroidManifest.xml:120-122); on SIGABRT it writes <dumps>/<APP_RUN_ID>.dmp and re-raises (0x00667A34..0x00667A8C, 0x0095107C..0x00951BD0). A Windows host has its own crash handling, so this stack uses it. Not pursued because they touch only crash output: bionic __assert2/abort (not in the package) and whether libunity/libmono install a SIGABRT handler above Breakpad at run time",
  "evidence": [],
  "status": "COMPATIBILITY_POLICY",
  "unresolved": "",
  "hardware_required": false,
  "live_path": false,
  "test": null
}
```

### M1-037

```json
{
  "id": "M1-037",
  "subsystem": "M1-transport",
  "title": "Host trigger for the socket reset",
  "location": "cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs",
  "effect": "the socket is reopened (on port 47817) when the host network changes, where the app reopened it on an Android network bind or unbind",
  "provenance": "operator decision D5 (2026-09-24): a choice of this stack",
  "authority": "the original resets on every Android process network bind or unbind (M1-023, rows G4.1..G4.13), which a Windows host does not have. This stack raises the same reset from the host notification that its network addresses changed (System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged), the host event nearest to the route to the robot changing. The reset mechanism itself is M1-023 and is reproduced from source",
  "evidence": [],
  "status": "COMPATIBILITY_POLICY",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportRepairTests.M1_037_TheHostAddressChangeRaisesTheSocketResetForTheTransportsLifetime"
}
```

### M1-038

```json
{
  "id": "M1-038",
  "subsystem": "M1-transport",
  "title": "Stop processing a frame after a handled DisconnectRequest sub-message",
  "location": "cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs",
  "effect": "sub-messages that follow a handled (in-sequence) DisconnectRequest in the same frame are not delivered",
  "provenance": "operator decision D8 (2026-09-24): a choice of this stack",
  "authority": "in the original, HandleSubMessage deletes the connection on a type-3 sub-message (0x008374A0, DeleteConnection veneer 0x008D123C; 0x008374C2) and ReceiveData then keeps walking the frame through the saved, now freed, connection pointer ([sp+0x18] at 0x008378FC, 0x00837958): undefined behaviour that cannot be reproduced. This stack stops processing the frame after such a handled DisconnectRequest instead. An out-of-sequence DisconnectRequest is dropped by R12 before dispatch (0x00837438 bne 0x00837456), frees nothing, and the original walks on with a valid pointer, so that case follows the source and the frame continues (operator decision, 2026-09-24)",
  "evidence": [],
  "status": "COMPATIBILITY_POLICY",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportRepairTests.M1_038_NothingAfterAHandledDisconnectRequestInTheSameFrameIsProcessed, TransportRepairTests.AnOutOfSequenceDisconnectRequestDoesNotStopTheFrame"
}
```

### M1-039

```json
{
  "id": "M1-039",
  "subsystem": "M1-transport",
  "title": "Windows ICMP port-unreachable on UDP receive is no data",
  "location": "cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs",
  "effect": "on Windows, a ConnectionReset receive error would add a warning and end the drain for the tick, where the original never sees it",
  "provenance": "operator decision (2026-09-24): a choice of this stack",
  "authority": "the original ran on Android/Linux, where an unconnected UDP socket does not surface ICMP port-unreachable (B11 sets no IP_RECVERR, 0x00839D18). On Windows, a UDP receive that fails with ConnectionReset is treated as no data for that receive attempt: no warning, and the drain continues. Other socket errors keep their source-backed handling (B16)",
  "evidence": [],
  "status": "COMPATIBILITY_POLICY",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportRepairTests.M1_039_OnWindowsAConnectionResetReceiveIsNoDataAndTheDrainGoesOn"
}
```

### M1-040

```json
{
  "id": "M1-040",
  "subsystem": "M1-transport",
  "title": "Accept every robot firmware, and log it",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "effect": "a robot the original would refuse as OutdatedApp or OutdatedFirmware is accepted",
  "provenance": "operator decision (2026-09-24): a choice of this stack",
  "authority": "the original compares the robot's firmware version with the shipped header (2381) and refuses a mismatch (M1-029). The operator's robot runs 2457. This stack never refuses on version or build: the handshake proceeds as for Success. It logs the robot's firmware version on every connection and in every hardware bundle, and warns whenever it is not 2381, so a firmware-specific problem is known together with its firmware",
  "evidence": [],
  "status": "COMPATIBILITY_POLICY",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_029_G5_11_G5_12_M1_040_TheVersionOutcomeIsComputedAndThenNotApplied, EngineAppLayerTests.M1_029_G5_2_G5_4_CB13_FactoryAndUnparsableFirmwareProceedUnderM1_040"
}
```

### M1-042

```json
{
  "id": "M1-042",
  "subsystem": "M1-transport",
  "title": "The original app's post-connect defaults are sent by this stack",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs",
  "effect": "volume and cube connection differ from the official app after connecting",
  "provenance": "operator decision (2026-09-24, option a): a choice of this stack",
  "authority": "in the original, the phone app (Unity) sends these game messages, not the engine: SetRobotVolume with the stored value on any RobotConnectionResponse (ConnectionFlowController.cs:682-686; GameAudioClient.cs:53-121), which makes the engine send SetAudioVolume {u16 vol x 65535} (0x0059A21E..0x0059A256); and GetBlockPoolMessage plus BlockPoolEnabledMessage {true, 0} on Success (RobotEngineManager.cs:373-378; BlockPoolTracker.cs:86-104; ConnectionFlowController.cs:764-780). This stack replaces the app: after a Success response it sends the stored volume (default 1.0) and enables the block pool itself, and the controller can change either. The idle timeouts (StartIdleTimeout / CancelIdleTimeout) are app-backgrounding behaviour and are not sent automatically. Other Unity post-connect flows are replaced by this stack's API",
  "evidence": [],
  "status": "COMPATIBILITY_POLICY",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_042_CD27_TheVolumeConversionIsTheEngines, EngineAppLayerTests.M1_042_NoIdleTimeoutIsArmedAutomatically, EngineAppLayerTests.M1_041_CD17_CD18_CD22_M1_042_PostSuccessSendsInOrder"
}
```

### M2-017

```json
{
  "id": "M2-017",
  "subsystem": "M2-protocol",
  "title": "A field whose read failed in a kept malformed message holds 0 / false (the engine leaves stale stack bytes)",
  "location": "cozmo-stack/src/Cozmo.Protocol/Clad/Clad.cs",
  "status": "COMPATIBILITY_POLICY",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "ProcessMessages sets only the union tag byte to 0xFF before Unpack (0x0069D8F8/0x0069D8FA); ClearCurrent 0x007B24A0 destroys only vector/string members and stores 0xFF (0x007B2526/0x007B2528)",
    "member constructors do not zero scalars: RobotErrorReport 0x007D3E50, PickAndPlaceResult 0x007C1ABA, FallingStopped 0x007B0EAA; fixed arrays store only successful reads (0x007BF21E..0x007BF22C, 0x007C885C..0x007C886A); FWVersionInfo zeroes its first three words (0x007C32D4..0x007C32DA)",
    "visible only in a kept truncated message (M2-011 D11), e.g. a 5-byte robotError: the engine reads a stale fatal byte at 0x0069D930"
  ],
  "effect": "a truncated robot message that the engine would read with stale bytes is read with zeros",
  "provenance": "forced policy (manager, 2026-09-24): undefined stack contents cannot be reproduced; a healthy robot does not send truncated messages",
  "unresolved": "",
  "hardware_required": false,
  "live_path": true,
  "test": "M2ProtocolTests.M2_001_D8_AFailedReadDoesNotAdvanceAndIsNotSticky"
}
```


## Assessments and build rows

All quotations above precede these conclusions. A recommendation about status boundaries below is not a manifest change.

### M1-013 - legitimate only for the Windows timer primitive

The record is already tightly scoped to an external host mechanism. The Windows winmm.dll timeBeginPeriod/timeEndPeriod implementation is not shipped Android app code. HighResolutionTimer.cs requests/releases period1. This is a legitimate host policy, provided the shipped schedule remains owned and reproduced by its other records.

| Step | Address | Shipped behavior | Gates / order / values | External limit |
|---|---|---|---|---|
| T1 | 008367C0..67E2;00836896..689E | Create RelTransport queue, switch async, request repeating callback. | Priority3; period u64=2 ms. | Callback timing decisions ship. |
| T2 | 007FCEBA..CED6 | Initial deadline=steady_clock now + period*1,000,000. | Integer u64 multiply/add; first run one period later. | OS clock implementation external. |
| T3 | 007FC344..C376,007FC3C4 | Repeat predicate then saved deferred-pass now+period, reinsert. | This schedule ships; changing it is not covered by host timer policy. | Actual OS wake jitter UNKNOWN. |
| T4 | 0065B3D2..B3EA;0065B420;0065B5D2 | Engine target uses0x03938700 ns=60,000,000; Update then remaining-wait sleep_for. | Cadence/wait arithmetic ships. | Host sleep primitive can be mapped; no concurrency/catch-up waiver inferred. |

### M1-014 - mixed boundary; default priority is not absent package behavior

Creating Windows thread objects is host mechanism. TaskExecutor's work split, queues, serial dispatch and priority request ship. The OS's permission and ability to honor that request are external; leaving default priority omits a shipped request. This distinction needs to remain explicit rather than accepting all thread behavior as policy.

| Step | Address | Shipped behavior | Gates / order / values | External limit |
|---|---|---|---|---|
| H1 | 007FBDBA,007FBDEC;007FBE02..BE14 | Construct two threads; call SetThreadPriority on both. | Calls only if priority !=2. | Native thread API external; count/work partition ships. |
| H2 | 008334CC..33532 | priority3 chooses policy2/SCHED_RR, obtains min/max, requests min+trunc_f32((max-min)*0.75). | 0.75 bits3F400000. pthread_setschedparam result0 succeeds; result1/EPERM skips error path. | Actual min/max, privilege and scheduling effect UNKNOWN from package. |
| H3 | 0062F3EA..F4BC;0069D902..DABA | Engine drains arrivals and then dispatches unpacked messages to subscribers. | Queue/dispatch ordering ships; M1-010/021/024/035 cannot be replaced by a host-thread label. | Current Windows concurrency equivalence not certified by this check. |

### M1-022 - shipped state machine incorrectly included in equivalent status

Only the system resolver/socket/syscall implementation can be an external equivalent. UDPTransport's binding, port bookkeeping, retry/reopen, logging and drain decisions are shipped engine code. A C# Socket adapter can legitimately implement the external primitive; this does not make the entire production path EQUIVALENT_IMPLEMENTATION.

| Step | Address | Engine behavior | Gates / order / failure / values |
|---|---|---|---|
| U1 | 00839A30..9AF2 | Close first, store requested port+98, build passive datagram hints/service, getaddrinfo(NULL,service,hints,out). | Production IPv4 family2,type2,flags1; address resolution itself external. |
| U2 | 0062EFEA;00839D00..9D3A | Init sets port0; first open asks for ephemeral binding; enable SO_BROADCAST before bind. | Option SOL_SOCKET1/SO_BROADCAST6,value1; choice of ephemeral port is OS output. |
| U3 | 00839E6A..9EB6 | errno98/EADDRINUSE warns and rejoins success-side processing, keeping fd. | Not a generic throw-on-bind-error contract. |
| U4 | 008395CA..96AA | fd<0 returns0 without reset; performed close reaches fd=-1/port47817 stores. | Stores00839694..969C follow success/error cleanup; return reflects close result. |
| U5 | 0083A374..A3A4;0083A54E..A56C;0083A648..A666 | Send flags0; short nonnegative count logs error; negative send increments error6; warning path time-limited. | No retry/disconnect introduced here. Double strict now>previous+30000; bits40DD4C0000000000. |
| U6 | 0083AA66..AA98;0083AAC4..AB38;0083ABF4..AC0C | Read0x5C0 bytes with MSG_DONTWAIT0x40. Count<1 fetches errno;11 silent, others warn;107 closes and successful close reopens using reset port47817. | Error/empty returns0, ending this drain. Zero-byte datagram also reads residual errno. |
| U7 | 0083ACE8..AD1A | Reset closes, reopens only when close==1, clears flag; loop reads while result nonzero if fd>=0. | All these branches are app code, not host socket policy. |

This assessment requires splitting/narrowing the status claim, not banning host APIs. Full UDP implementation verification is outside this bounded policy check.

### M1-034 - shipped exception path; isolation is a departure

The fatal dispatch/EH behavior is package code. The final phone abort primitive is external; it cannot justify catching a handler exception and continuing to later handlers. D6 records historical authorization for a departure, not an absent-code boundary under the current exact rule.

| Step | Address | Shipped behavior | Result / uncertainty |
|---|---|---|---|
| X1 | 0069E286..E294 | Invoke existing subscriber, then load next link after return. | No per-subscriber isolation in this loop. |
| X2 | 0069DCD4..DCDE;0069DB64..DBAC | Exception cleanup clears union, frees owned temporaries and resumes unwinding. | Inspected pads do not log-and-continue the message drain. |
| X3 | 004E39F8..39FE | Helper calls __cxa_begin_catch then std::terminate. | Fatal helper explicitly ships. |
| X4 | libc++_shared.so0007A948..A98C,0007B208..B21A | Throw saves terminate handler; unsuccessful unwinding invokes it; __terminate invokes handler and reports fatal error if it returns/throws. | C++ runtime ships. |
| X5 | libc++_shared.so00068B84..C4A,00068AA8..AACE | Default handler formats diagnostic, stderr newline, __assert2 then abort. | Exact phone __assert2/abort implementation and final OS crash output UNKNOWN/external. |

Representative dispatch/EH boundaries and the shipped fatal machinery were checked. No exhaustive dynamic terminate-handler census or phone crash capture is invented.

### M1-036 - Breakpad crash reporting ships

CozmoActivity's installation condition, app-run-id dump path, embedded Breakpad and dump callback are recovered app behavior. Windows default crash reporting alone does not reproduce them. The OS signal/filesystem primitive is external; app crash-output behavior is not.

| Step | Primary evidence | Behavior / gate / order | Values / uncertainty |
|---|---|---|---|
| B1 | sources/com/anki/cozmo/CozmoActivity.java:45-52;resources/AndroidManifest.xml:120-122 | Generate run UUID; nonempty HOCKEYAPP_APP_ID installs Breakpad at dumpsDir/run-id.dmp. | Shipped manifest ID is nonempty. |
| B2 | 006679D2..79DC;00667A3A..7A8C | JNI obtains path; open; construct fd descriptor and ExceptionHandler with callback/install_handler1. | flags0x2C1 O_WRONLY/O_CREAT/O_EXCL/O_TRUNC, mode0x180/0600; these choices ship. |
| B3 | 009512E4..5139C ARM | Save old actions for signals11,6,8,4,7,5. | Signal6 is SIGABRT; embedded Breakpad, not absent code. |
| B4 | 00667AF4..7B68;00951B88..51BD0 ARM | Callback reports result and closes matching fd; SIGABRT re-raise calls getpid/gettid/tgkill, falls back _exit(1) on failure. | Conditional fatal path ships. Actual dump success/OS response UNKNOWN without runtime evidence. |

The complete minidump byte format was not re-extracted here; it is recoverable shipped code. Later Unity/Mono signal-handler installation order remains separately UNKNOWN and does not erase Breakpad provenance.

### M1-037 - platform API external; trigger substitution unproved

Android process-binding implementation and Windows NetworkAddressChanged are external. The app's selection of when to emit the reset callback ships. A generic address-change event is not evidence of successful process bind/unbind and can include unrelated interface changes.

| Step | Primary evidence | Shipped condition / order | Boundary |
|---|---|---|---|
| N1 | sources/com/anki/util/WifiUtil.java:321-331 | Cancel callbacks; API>=23 unbind then emit NativeBindNetworkCallback(0); API21-22 unbind then emit Lollipop callback. | Emission unconditional after unbind, regardless of API return; below21 no native emission here. |
| N2 | WifiUtil.java:381-404 | Mark binding; call OS binding; emit native callback only on successful result for API>=21. | Success gate ships; OS ability to bind external. |
| N3 | 0083BC3C..BC88;0083BAD6..BAE0 | JNI callbacks signal subscribers, ignore network handle; handler requires fd>=0 then ResetSocket. | Native predicate ships. |
| N4 | 0083ACE8..AD04 | Handle flag on UDP Update; close, reopen only on close==1, clear flag. | M1-023 mechanism must survive host mapping. |

The legitimate host policy can name the Windows facility used for the corresponding network operation. The broader current trigger equivalence is unproved. Shipped CozmoWifi/AndroidConnectionFlow/WifiUtil logic cannot be discarded as Android OS implementation. No Windows process-binding API or behavior is inferred here.

### M1-038 - shipped use-after-free, not outside package

Deletion and frame continuation are deterministic recovered instructions; subsequent freed-allocation contents/effects are UNKNOWN. The binary supplies no unconditional post-disconnect frame stop.

| Step | Address | Shipped gate / order | Result / limit |
|---|---|---|---|
| D1 | 00837438..7446 | Out-of-sequence returns before dispatch; in-sequence advances sequence and dispatches type. | Out-of-sequence disconnect does not delete here. |
| D2 | 008374A0..74C2;008D123C veneer->PLT004CD6B8->008375F0 | Type3 reports disconnect if listener exists, then DeleteConnection. | Veneer begins Thumb bx pc, ARM branch target resolved. |
| D3 | 0083760A..761E | Connection destructor, operator delete, erase map node. | Lookup miss returns without deletion. |
| D4 | 008378FC;00837958..796C | Keep saved connection pointer at stack+18 and forward to subsequent submessage calls. | No stop after handled disconnect; later freed-memory behavior UNKNOWN. |

D8 is a deliberate workaround, not proof the behavior does not ship. Do not invent freed bytes or a unique crash outcome. Under the requested rule, a defined stop cannot be called exact recovered continuation. This finding does not instruct anyone to intentionally dereference freed memory in C#; the manager must resolve and expose the fidelity limit rather than misdescribe its provenance.

### M1-039 - wrong Linux premise; shipped handler differs

The kernel event is external, but the claim that unconnected Linux UDP never surfaces ICMP port-unreachable is wrong. Linux documents fatal asynchronous errors even for unconnected sockets; IP_RECVERR enables an extended error queue and its absence does not prove ordinary receive errors absent. See [Linux udp(7), error handling and ECONNREFUSED](https://man7.org/linux/man-pages/man7/udp.7.html). [Microsoft WSASendTo](https://learn.microsoft.com/en-us/windows/win32/api/winsock2/nf-winsock2-wsasendto) identifies UDP WSAECONNRESET as ICMP port-unreachable from a prior send.

| Step | Address | Engine behavior | Boundary |
|---|---|---|---|
| I1 | 00839D00..9D3A | SO_BROADCAST then bind. | This is not proof ICMP errors cannot be surfaced. |
| I2 | 0083AA98..AAC6 | Read count<1 fetches errno; suppress warning only for11/EAGAIN. | Other returned async errors use shipped handling. |
| I3 | 0083AAF6;0083AB22..AB24;0083ABF4..AC0C;0083AD16..AD18 | Other errno warns; only107/ENOTCONN reopens; return0 stops drain. | No ICMP-specific swallow-and-continue branch. |

Actual kernel/version/network conditions on the phone remain UNKNOWN from the package. This does not assert a particular phone capture would receive ECONNREFUSED. An OS error/event mapping can be an external equivalent; silent Windows ConnectionReset suppression and continuation cannot be justified by the quoted never-sees-it premise. The documentation check is authority6 background about the external boundary; native handler decisions are checked primary evidence.

### M1-040 - firmware rejection ships

Unconditional acceptance is a departure. Operator firmware2457 does not turn the engine/OBB comparison into external behavior; logging a mismatch cannot reproduce rejection.

| Step | Primary evidence | Shipped condition / result | Values |
|---|---|---|---|
| V1 | re-analysis/obb/assets/cozmo_resources/config/engine/firmware/cozmo.safe, JSON offsets0..445 | Shipped version/time/build. | 2381;1546972025;DEVELOPMENT. SHA256 a4e266cfaaa95e1895fb48729603a732c24f4808a19e14677792137d63a449c0. |
| V2 | 0052D7CE..D7E6 | robot version==time and app version==time development predicates differ ->result3. | Native predicate, not host choice. |
| V3 | 0052D8DA..D8E2;0052D9A6..D9AE | Equal versions ->0; unsigned app greater ->3, otherwise4. | 0Success,3OutdatedFirmware,4OutdatedApp. |

Factory, parse-failure, robot-available and simulator gates remain M1-028/029 engine paths; this check does not reduce them to version-only logic. None authorizes accepting every build/version.

### M1-042 - Unity producers ship; pool-enable gate omitted

This policy fails the package-exclusion test. Unity app code is shipped primary source. The record additionally overstates the pool-enable condition.

| Step | Primary evidence | Producer / gates / order | Values / limit |
|---|---|---|---|
| A1 | unity/scripts/csharp/ConnectionFlowController.cs:682-686;Anki.Cozmo.Audio/GameAudioClient.cs:53-108 | Every RobotConnectionResponse applies Robot persistence volume before HandleRobotConnectResponse; preference or default; storeValue:false. | Default1 bits3F800000; missing CurrentRobot logs instead of sending. |
| A2 | unity/scripts/csharp/Robot.cs:1461-1465;0059A21E..A256 | SetRobotVolume game message; engine binary32 multiply by65535, VCVT.u32.f32, lowu16 store; SendMessage reliable1/flush0. | Multiplier477FFF00; no added clamp in checked slice. |
| A3 | unity/scripts/csharp/RobotEngineManager.cs:373-380,223-231;Cozmo.BlockPool/BlockPoolTracker.cs:86-94 | !_IsRobotConnected && Success ->AddRobot; tracker nonnull ->InitBlockPool ->subscribe then GetBlockPoolMessage. | Not an engine-originated default. |
| A4 | unity/scripts/csharp/ConnectionFlowController.cs:764-780;BlockPoolTracker.cs:97-104 | Success UI flow EnableAutoBlockPool(true) only when !DefaultProfile.FirstTimeUserFlow. | Default discoveryTimeSeconds0 bits00000000. Quoted blanket success enable omits this shipped gate. |
| A5 | unity/scripts/csharp/Cozmo/PauseManager.cs:219,289,350-374 | Background/sleep start timeout guarded by !_StartedIdleTimeout; reset robot/enable cube sleep before request; stop disables sleep if applicable then CancelIdleTimeout. | Lifecycle producers ship; exact config timeout values not re-extracted in this producer check. |

"Other Unity post-connect flows are replaced by this stack's API" is a scope substitution, not proof of absent code. Untraced package flows are recoverable gaps. Under the current cross-layer rule their higher-layer parts can be separately owned; they cannot disappear behind an M1 policy. No exhaustive post-connect Unity audit is claimed.

### M2-017 - failed read/no-store ships; residual values UNKNOWN

Residual stack contents are not the phone OS implementation. Zero/false is a defined replacement for that uncertainty. The current exact rule provides no general exception making shipped undefined/uninitialized paths legitimate outside-package policy.

| Step | Address | Shipped gate / order | Result / uncertainty |
|---|---|---|---|
| P1 | 0069D8F8..D902;007B2526..252A | New local gets tagFF only; ClearCurrent destroys dynamic members and stores tagFF without scalar blanket clear. | Residual scalar storage possible. |
| P2 | 007D3E50..3E70;007C1ABA..1ADE;007B0EAA..0ED8 | Constructors unpack without zeroing scalar members, reading fields in order. | No universal zero-on-failure contract. |
| P3 | 0083C09A..C0CA;0083C0F0..C118 | Unsigned bounds test; failed read performs no destination store/copy and does not advance cursor. Bool normalized/store only on success. | Return0 failure; later reads may succeed at unchanged cursor. |
| P4 | 0069D906..D918;0069D930 | Keep when consumed length==packet length; robotError handler reads fatal+4. | Total5 bytes: tag1+errorCode4 consumes5; missing fatal1 unchanged yet handler reads it. |
| P5 | 007C32D4..32DA | FWVersionInfo explicitly zeroes first3 words before unpack. | Those fields have known defaults; not all incomplete fields are residual. |

The actual stale fatal byte cannot be settled to a constant from these instructions. It is UNKNOWN; neither zero nor guaranteed nonzero/abort may be invented as recovered fact. Healthy-robot assumptions and tests do not change provenance. A separately authorized zero-default exception would be a visible departure; it is not legitimate because the implementation lies outside the package.

## Disposition

All eleven package-boundary questions were checked. M1-013 is cleanly host-only. M1-014/022/037 mix external mechanisms with shipped decisions; M1-039 also has a contradicted Linux premise. M1-034/036/040/042 cover identifiable shipped behavior. M1-038/M2-017 cover shipped instructions with residual/freed-memory outcomes that remain UNKNOWN.

No status changes are made by this research artifact. Source-backed parts require manager-verified inventory/implementation treatment; genuine OS behavior remains an external boundary. This is not a subsystem acceptance or a claim that every transitive function has been ported. No regression test/hardware run/commit/push performed.
