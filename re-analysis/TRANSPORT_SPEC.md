# Cozmo engine ↔ robot transport: reconstructed specification

Date: 2026-09-18. Authority order used here: (1) `libcozmoEngine.so` 3.4.0-1204 disassembly
(`disassembly/dis_reliabletransport.txt`, `dis_reliableconn.txt`, `dis_udptransport.txt`, `dis_robotconn.txt`,
`dis_rt_full.txt` in the scratch set), (2) Anki's open-sourced `util/transport` library from the Vector robot
(`reference/anki-util-transport-vector/`), which is the same code the Cozmo engine links — every constant and code
path checked matches the disassembly byte-for-byte, (3) PyCozmo's capture-derived description, used for
robot-side behaviour that only hardware shows. Implementation: `cozmo-stack/src/Cozmo.Protocol` and
`cozmo-stack/src/Cozmo.Transport` (C#, .NET 10); tests in `cozmo-stack/tests`.

## 1. The 5551 / 5552 question — resolved

`Anki::Cozmo::RobotInterface::MessageHandler::AddRobotConnection(const ExternalInterface::ConnectToRobot&)`:

```
movw r2, #0x15b0          ; 5552
ldrb r0, [r1, #0x10]      ; ConnectToRobot.isSimulated  (ipAddress[16] occupies offsets 0..15, C# Size=17)
cmp  r0, #0
it   eq
movweq r2, #0x15af        ; 5551 when isSimulated == 0
TransportAddress(ipAddress, r2) -> RobotConnectionManager::Connect
```

So: **physical robot = UDP 5551**, **Webots-simulated robot = UDP 5552**. Both are destination ports on the robot
side. Neither implementation was wrong: PyCozmo (5551) is correct for hardware; the 5552 seen earlier in the engine
is the simulator path. The engine's own local port is whatever `UDPTransport` binds (library default 47817, ephemeral
is fine — the firmware answers to the source address/port of the ConnectionRequest). The robot only accepts
172.31.1.0/24 sources (PyCozmo).

## 2. Layering

```
UDP datagram
 └─ UDPTransport header      4 B   "COZ\x03"  (RobotConnectionManager::Init: SetHeaderPrefix("COZ\x03",4), SetDoesHeaderHaveCRC(false), SetMaxNetMessageSize(1420))
    └─ AnkiReliablePacketHeader 10 B "RE\x01" + type u8 + seqIdMin u16 + seqIdMax u16 + seqIdLastReceived u16  (all little-endian)
       └─ body: one message, or for container types 7/8/9 a list of [type u8][size u16][payload]
          └─ data payload = CLAD union: [tag u8 = RobotMessageId][packed struct]
```

No CRC, no encryption, no obfuscation, no session identifier: the peer is identified by UDP source address.
Maximum: engine builds frames up to 1420 − 4 − 10 = **1406** reliable-layer bytes; PyCozmo reports the robot drops
frames whose reliable-layer body exceeds **1037** bytes (1051 total). Our default `MaxFramePayloadBytes` = 1037 until
re-measured on hardware.

## 3. Message types (`EReliableMessageType`)

| value | name | reliable? | where it appears |
|---|---|---|---|
| 1 | ConnectionRequest | yes | engine → robot, first frame (seq 1), empty body. PyCozmo "RESET" |
| 2 | ConnectionResponse | yes | robot → engine, empty body. PyCozmo "Connect" |
| 3 | DisconnectRequest | yes | either side, empty body |
| 4 | SingleReliableMessage | yes | a CLAD message. PyCozmo "COMMAND" |
| 5 | SingleUnreliableMessage | no | a CLAD message. PyCozmo "EVENT" (RobotState, AnimationState, ImageChunk …) |
| 6 | MultiPartMessage | yes | payload > frame limit: `[part 1-based u8][count u8][bytes]`, parts reassembled in order |
| 7 | MultipleReliableMessages | container | frame holding only reliable sub-messages (typical engine frame) |
| 8 | MultipleUnreliableMessages | container | only unreliable sub-messages |
| 9 | MultipleMixedMessages | container | both (typical robot frame: RobotState + events). |
| 10 | ACK | no | standalone ack; engine disables it (`sSendAckOnReceipt=false`). PyCozmo calls 0x0a "KEYFRAME" — not observed from the engine |
| 11 | Ping | no | 17-byte payload, see §7 |

`IsMessageTypeAlwaysSentUnreliably` = {5,7,8,9,10,11}; `IsMutlipleMessagesType` = {7,8,9}; valid = 1..11.
Container selection when batching (ReliableConnection::SendUnAckedMessages): reliable+unreliable → 9, only reliable → 7,
only unreliable → 8. A frame with exactly one pending message is sent as its own type with no `[type][size]` sub-header.

## 4. Sequence numbers

* 16-bit, valid 1..65534, wrap 65534 → 1, 0 = "no sequence / unreliable". (`NextSequenceId`: `movw #0xfffe; addne #1; moveq #1`.)
* Each direction has its own counter. A new connection starts at `_nextOutSequenceId = 1`, `_nextInSequenceId = 1`,
  `_lastInAckedMessageId = 0`. The ConnectionRequest consumes seq 1 on the engine side; the ConnectionResponse is seq 1
  on the robot side.
* Only reliable messages (types 1,2,3,4,6) consume ids; unreliable ones never do.
* In a container frame, `seqIdMin` = id of the first reliable sub-message, `seqIdMax` = id of the last; the receiver
  assigns ids to reliable sub-messages sequentially from `seqIdMin`, skipping unreliable ones.
* PyCozmo stores `wire − 1` internally and adds 1 when writing; the wire values are what this spec and our code use.

## 5. Acknowledgement and ordering (receiver side, `ReliableTransport::ReceiveData`)

1. Prefix check; frames < 10 bytes or with a wrong prefix are dropped.
2. `UpdateLastAckedMessage(header.seqIdLastReceived)`: every pending outgoing reliable message whose id is in
   `[firstUnacked, lastUnacked]` up to the acked id is removed. **The ack is cumulative and piggy-backed on every frame**
   (including pings and unreliable frames). There are no negative acks.
3. If the header is reliable (`seqIdMin||seqIdMax != 0`): `IsWaitingForAnyInRange(min,max)` = is `_nextInSequenceId` in
   `[min,max]`. If not, the frame is a duplicate/out-of-order: it is dropped **unless** it is a MultipleMixedMessages
   frame, which is still walked so its unreliable content (e.g. RobotState) is delivered. If yes, `AckMessage(seqIdMax)`
   sets `_lastInAckedMessageId = seqIdMax` (advertised in our next outgoing header).
4. Each sub-message: reliable ones are accepted only if `seq == _nextInSequenceId` (then it advances); otherwise
   ignored. Delivery is strictly in order. Example: a resent frame carrying ids 7,8,9 while 8 is expected delivers
   8 and 9 (7 is dropped as a duplicate, the expected id advances after each acceptance).
5. Dispatch: 1 → OnConnectRequest, 2 → OnConnected, 3 → OnDisconnected + delete connection, 4/5 → deliver payload,
   6 → multipart reassembly (deliver when part == count), 11 → ReceivePing.

There is **no receive window**: anything not exactly next-in-sequence is discarded and must be resent by the peer.

## 6. Sending, batching, resend (`ReliableConnection`)

Engine configuration (RobotConnectionManager::Init/ConfigureReliableTransport, decoded from the double constants):

| parameter | engine value | library default |
|---|---|---|
| TimeBetweenPingsInMS | 33.3 | 250 |
| TimeBetweenResendsInMS | 33.3 | 50 |
| MaxTimeSinceLastSend | 32.3 | 49 |
| ConnectionTimeoutInMS | 5000 | 5000 |
| PacketSeparationIntervalInMS | 2.0 | 0 |
| MinExpectedPacketAckTimeMS | 1.0 (default) | 1.0 |
| MaxPingRoundTripsToTrack | 10 | 20 |
| MaxPacketsToReSendOnUpdate | 1 | 3 |
| MaxPacketsToReSendOnAck | 0 | 1 |
| MaxPacketsToSendOnSendMessage | 1 | 1 |
| SendSeparatePingMessages | false | true |
| SendPacketsImmediately | false | true |
| SendAckOnReceipt | false | true |
| SendUnreliableMessagesImmediately | false | true |
| TrackAckLatency | true | false |
| MaxNetMessageSize | 1420 | 1472 |

Algorithm:
* `SendMessage` appends a `PendingMessage` (type, seq or 0, bytes, flush flag) to one list, then calls
  `SendOptimalUnAckedPackets(1)`.
* `SendOptimalUnAckedPackets`: honour the 2 ms packet separation; find the pending message with the oldest
  last-sent time (never sent counts as "resend interval + 1 ms ago"; anything sent before `latestRecv − 1 ms` is
  treated as 33.3 ms older so it is prioritised); if `IsPacketWorthSending` and `now > oldest + 33.3 ms`, build and
  send one frame from that message.
* `IsPacketWorthSending` (with SendPacketsImmediately=false): true if 32.3 ms passed since our last send, or any
  pending message has `flush`, or any already-sent pending message is older than 32.3 ms, or the pending bytes would
  fill a frame.
* `SendUnAckedMessages(first)`: greedily pack consecutive pending messages from `first` forward (then backward) while
  `Σ(3 + size) ≤ max`; emit one frame (single type if one message, else 7/8/9); stamp `lastSentTime`; **delete
  unreliable messages once sent**, keep reliable ones until acked. Reliable messages are therefore resent as whole
  frames every ≥ 33.3 ms until the header ack covers them; there is no retry limit other than the 5 s receive timeout.
* `Update()` (every 2 ms from the transport's dispatch queue): idle ping (§7), `SendOptimalUnAckedPackets(1)`,
  timeout check `now > latestRecv + 5000`.
* Header `seqIdLastReceived` of every outgoing frame = `_lastInAckedMessageId` at build time.

## 7. Ping / keepalive

`PingPayload` (17 bytes): `f64 timeSent` (sender's steady-clock ms), `u32 numPingsSent`, `u32 numPingsReceived`,
`u8 isReply`. Engine behaviour with `SendSeparatePingMessages=false`: a ping is queued only when the pending list is
empty and ≥ 33.3 ms passed since the last send, at most every 33.3 ms; the engine never echoes robot pings. The
robot echoes engine pings (the PyCozmo capture shows a verbatim echo, `isReply` still 0). RTT = now − timeSent
when a reply/echo returns. If the robot receives nothing for ~5 s it disconnects and shows "COZMO 01" (PyCozmo).

## 8. Connection lifecycle

```
engine                                    robot (172.31.1.1:5551)
  ConnectionRequest  type 1, seq 1, ack 0  ─────────►      (frame = COZ\x03 RE\x01 01 0100 0100 0000, 14 bytes)
  ◄─────────── ConnectionResponse type 2, seq 1, ack 1     → engine: OnConnected
  ◄─────────── RobotAvailable(0xC9) + FirmwareVersion(0xEE) as reliable data (robot seq 2,3)
  GetManufacturingInfo(0x25) ─────────────►  ◄── MfgId(0xED)
  SyncTime(0x4B) ─────────────────────────►  ◄── SyncTimeAck(0xC2); RobotState(0xF0) unreliable stream ~30 Hz begins
  … pings every 33 ms while idle; every frame carries the cumulative ack …
  DisconnectRequest type 3 ───────────────►  (ReliableTransport::Disconnect then deletes the connection)
```
The engine-side transport also checks the CLAD hashes in FirmwareVersion against its own
(`EngineRobotCLADVersionMismatch`); the shipped firmware is 2381 (hashes in `OBB_INVENTORY.md §3`). Reconnect =
new socket, counters back to 1, new ConnectionRequest (the robot resets its state on ConnectionRequest).

## 9. Discrepancies found against PyCozmo (its transport is otherwise correct)

| topic | PyCozmo | official |
|---|---|---|
| ping payload | 16 B (time, counter, unknown) | 17 B incl. `isReply` |
| ping cadence | every 0.5 s | every 33.3 ms when idle |
| ack timing | ACK_TIMEOUT 0.1 s; window 16/62 | resend every 33.3 ms from oldest unacked; no window |
| type 0x0a | "KEYFRAME" | ACK (unused by engine) |
| out-of-order handling | receive window buffers | strict next-id only; duplicates dropped |
| MultipleMixed out-of-range | dropped | still walked for unreliable content |
| multipart (type 6) | not implemented | `[idx][count]` reassembly |
| frame size | 1051 total (robot-measured) | engine can build up to 1420 total |
| seq representation | wire−1 | wire |

## 10. Still to confirm on hardware

* Whether the robot honours frames up to 1420 bytes (engine limit) or only 1051 (PyCozmo observation).
* Whether robot ping echoes set `isReply` for official 17-byte pings.
* Robot-side resend cadence/timeout (only the 5 s "COZMO 01" is documented).
* Exact robot behaviour on a second ConnectionRequest while connected (expected: reset).

## 11. Conformance harness (`cozmo-conformance`)

`decode`, `diff` (byte-by-byte with field hints), `fixtures` (PyCozmo hardware captures must round-trip
byte-identically — they do), `catalog` (161 official messages), `pcap` and `replay` (pcap/hex frames through our
receive state machine), `fakerobot` (loopback transport stand-in), `connect` (hardware smoke test with frame log).
Loopback result: connect 25 ms, identity + handshake, telemetry at the fake robot's rate, SetHeadAngle acked and
reflected in RobotState, clean disconnect — the socket/thread/state-machine path works end to end; hardware is the
remaining step.
