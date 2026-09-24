# M1 candidate vs the frozen M1 inventory: comparison

Two read-only verifiers, 2026-09-24:
- **V1** covered the reliable layer.
- **V2** covered the UDP socket, the engine side and the lifecycle.

They changed nothing: the working-tree fingerprint was identical before and after. They compared the M1 candidate (the working tree on `bcab556`) against `M1-transport.md` as first frozen. This file reconciles their findings with the corrected, re-frozen inventory (corrections C1..C5).

Line references are to the candidate as compared:
- RT = `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
- RC = `ReliableConnection.cs`
- TC = `TransportConstants.cs`
- FR = `Cozmo.Protocol/Wire/Frame.cs`
- CR = `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`

## Reconciliation with the corrections

- **C1 (reopen port):** V2's B16 DIFFERS stands. The target is now "reopen only if the close succeeded, on port 47817"; the candidate reopens unconditionally on an ephemeral port (RT:456-466). T-k1 (TransportRepairTests.cs:602-630) encodes the old reading and must change.
- **C2 (MSG_TRUNC):** V1 already judged B17 against the engine, so its DIFFERS stands.
- **C3 (guards):** V1 found both guards in the code (RC:172-190, RC:154-169), so M1-006 CONFORMS against the corrected rows.
- **C4 (ping send path):** RC:229-230 does AddMessage then SendOptimal(1), so M1-008 CONFORMS against the corrected row.
- **C5 (M1-038):** the candidate delivers the rest of a frame after a DisconnectRequest (RT:566-593, 630). That DIFFERS from the policy.
- **Clock (G2.4 in M1-005's evidence).** V2 called it equivalent; V1 noted a difference. Resolved as a difference:
  - the engine's clock is milliseconds from a process-wide steady_clock epoch, truncated to whole microseconds (0x00835644);
  - the candidate uses a Stopwatch per transport with 100 ns resolution (TC:71-75, RT:73);
  - this changes the timestamps in ping payloads.

  M1-005 is therefore PARTIAL.

## Verdict per record

| record | verdict | what differs or is missing | batch |
|---|---|---|---|
| M1-003 | CONFORMS | – | 1 |
| M1-004 | CONFORMS | – | 1 |
| M1-006 | CONFORMS | – | 1 |
| M1-008 | CONFORMS | – | 1 |
| M1-009 | CONFORMS | – | 1 |
| M1-011 | CONFORMS | – | 1 |
| M1-012 | CONFORMS | – | 1 |
| M1-017 | CONFORMS | – | 1 |
| M1-005 | PARTIAL | clock resolution and epoch (G2.4 line) | 2 |
| M1-002 | PARTIAL | B17: 2048 buffer, no truncation drop, and an oversize datagram ends the drain (RT:54, 438-446); R3: no AddRecvError counters | 2 |
| M1-007 | PARTIAL | R10: a container ending in a 1..2-byte partial sub-header is rejected whole before the ack and earlier subs (RT:536-547, 605-617); the engine treats it as a size overrun, AddRecvError(1), after both | 2 |
| M1-010 | PARTIAL | R35: the tick starts at Connect and stops at Shutdown (RT:233-240, 318-354); the engine schedules it once at construction and keeps it; priority 3 not reproduced | 2 |
| M1-016 | PARTIAL | R18: in the partial-header case, lastRecv and the ack are not applied (RT:541-547) | 2 |
| M1-018 | PARTIAL | R13 receive side: no inbound connection created for type 1 (or a container whose first sub is type 1) from an unknown address (RT:514-526) | 2 |
| M1-019 | PARTIAL | R38: FinishConnection missing; R39: Start folded into Connect (socket per connection) | 2 |
| M1-021 | PARTIAL | G1.2: first run one period after each Connect; G1.7: due check and run on one thread; G1.9/G1.10: no backlog of copies run back to back (RT:397-403, 700-706) | 2 |
| M1-035 | DIFFERS | R37: sends run synchronously on the caller's thread under _lock, not posted FIFO with the ticks (RT:241, 272-276, 286-290, 300/308) | 2 |
| M1-038 | DIFFERS (policy) | frame processing continues after a DisconnectRequest (RT:566-593, 630) | 2 |
| M1-001 | PARTIAL | B1: no 172.31.1.1 / 127.0.0.1 choice; B3: 5552 unused, arbitrary port override (TC:15-16, RT:228); B4: no advertising-port check | 2 |
| M1-015 | PARTIAL | R19/G2.8: a timeout shuts down the whole transport (RT:419-420, 318-354) instead of deleting the connection; B22: an extra app-level connect timer (CR:249-251), and the half-built robot is not disposed (CR:240-258); R41, B31, B32, B36: no reason codes, no Rejected/Failure result, no timed-out flag | 2 (transport), 3 (reporting) |
| M1-020 | PARTIAL | R36: sends are synchronous (with M1-035) | 2 |
| M1-022 | DIFFERS | B10: byte count ignored, no AddSendError, warning unconditional (RT:376-378); B11: non-blocking socket, no SO_BROADCAST, bind failure throws (RT:246-256); B12: socket opened per Connect (RT:229, 329-330); B16: 2048 buffer, 0-byte datagram processed and drain continues, unconditional ephemeral reopen (RT:54, 449, 456-466); B38: close failure swallowed, no reset to 47817 | 2 |
| M1-023 | MISSING | no reset mechanism | 2 |
| M1-032 | PARTIAL | G2.6: the partial-header frame does not refresh lastRecv (RT:541-547); G2.8: whole-transport shutdown; G2.10: no inbound connection creation | 2 |
| M1-024 | PARTIAL | B24: no 60 ms engine tick; B25: arrivals raised per datagram on cozmo-dispatch (RT:109-116, 159-164), not drained FIFO once per tick | 3 |
| M1-025 | PARTIAL | B2: a second Connect throws, and each ConnectAsync builds a new robot and transport (RT:218, CR:240); B23: no reason-0 write or unexpected-time error; B33: DisconnectCurrent path absent; B35: no reason byte (ChargerBehaviors.cs:115-121) | 3 |
| M1-026 | PARTIAL | B28: a refused send throws instead of returning a failure (RT:269-277); no filter check (M1-030) | 3 |
| M1-027 | DIFFERS | B27: no ShouldFilterMessage; a size-mismatched message becomes a RawRobotMessage (MessageBase.cs:38-39, CR:437-447); no robotError 0xD9 fatal handling | 3 |
| M1-028 | MISSING | no initial-connection handshake; CR:261-274 sends GetManufacturingInfo, SyncTime and InitController and starts the block pool unconditionally on ConnectionResponse | 3 |
| M1-029 | MISSING | no firmware-header load or version check | 3 |
| M1-030 | MISSING | no pre-validation gating | 3 |
| M1-031 | MISSING | no idle timeout | 3 |

## Policies

| record | verdict | note |
|---|---|---|
| M1-013 | implements, 2 ms only | the 60 ms period is realised with M1-024 in batch 3 |
| M1-014 | departs further | resolved by M1-035 (batch 2) and M1-024 (batch 3) |
| M1-034 | implements | RobotLink.OnData raises State and Message as plain multicasts (RobotLink.cs:109-110); make it consistent in batch 2 |
| M1-036 | implements | – |
| M1-037 | absent | batch 2, with M1-023 |
| M1-038 | differs | batch 2 |
| M1-033 (HARDWARE_ONLY) | no silent assumption | the LINK check (HardwareCatalog.cs:72) must name M1-033: batch 1 |

## Tests to repair

- **Contradicted by the source, or circular:**
  - TransportRepairTests.cs:361-372 (partial-header rejection; contradicted by R10);
  - TransportRepairTests.cs:602-630 T-k1 (ephemeral reopen; contradicted by C1);
  - TransportHardeningTests.cs:122-132 (a second Connect throws; contradicted by B2);
  - TransportHardeningTests.cs:134-145 (implementation-defined);
  - TransportHardeningTests.cs:158-165 and the throw in TransportRepairTests.cs:202-216 (contradicted by B28).
- **Circular or off the live path:**
  - FrameTests.cs:110-117 (ping offsets round-trip their own encoding);
  - FrameTests.cs:99-108 (TryDecode is not the live path);
  - TransportSourceTests.cs:53-57 (checks the constant only);
  - TransportSourceTests.cs:64-87 (SSPM true only; line 80's claim is false for the engine configuration);
  - ConnectionTests.cs:90-102 (InRange(4,6) is looser than R32);
  - TransportRepairTests.cs:543-558 (cadence unit only; its comment contradicts G1.10).
- **Other oracle problems:**
  - ConnectionTests.cs:27-47 (PyCozmo oracle);
  - ConnectionTests.cs:49-56 and TransportRepairTests.cs:71 (cite the dead ConfigureReliableTransport);
  - FrameTests.cs:55 and TransportSourceTests.cs:75 (robot behaviour asserted from PyCozmo).

## Behaviour no row or policy supports

1. The app-level connect timer (CR:249-251, RobotLink.cs:63-67).
2. The post-connect setup sequence (CR:261-275).
3. RobotLink.BeginSession's PyCozmo origin reset (RobotLink.cs:73-78).
4. Dispose sends StopAllMotors and DriveWheels(0) before disconnecting (CR:401-405, 414-423).
5. The socket lifetime is per Connect (RT:229, 318-354).
6. The non-blocking socket (RT:252).
7. SIO_UDP_CONNRESET is not disabled, so an ICMP port-unreachable on Windows ends the drain (RT:246-256, 440-445).
8. Exceptions where the original logs and returns (RT:218, 274, 288).
9. The remote-port override (RT:228).
10. The 2048-byte buffer (RT:54).
11. Size-mismatched messages delivered (MessageBase.cs:38-39).
12. Options declared but ignored (TC:31, 47, 49).
13. Dispose sends a DisconnectRequest only when Connected (RT:362-366).

Each of these is either removed by a batch or, if it has to stay, recorded as a policy before its batch is accepted.
