# Project state

Read first in every session. The manager keeps this file current; the process it follows is the Process section of `AGENTS.md`.

## Now

- **Phase:** M1 repair. The inventory was corrected (C1..C5) and re-frozen on 2026-09-24; M1 has 31 IMPLEMENTATION_GAP, 6 COMPATIBILITY_POLICY (M1-013, M1-014, M1-034, M1-036, M1-037, M1-038) and 1 HARDWARE_ONLY. The candidate-vs-inventory comparison is `re-analysis/inventory/M1-transport.comparison.md`.
- **Operator decisions:** D1-D4 approved 2026-09-23. On 2026-09-24: the inventory, D5, D6 (fatal behaviour recorded, isolation kept as policy), D7, the corrections C1..C5 and D8 (stop processing a frame after a DisconnectRequest, M1-038) approved; the three repair batches are authorised to run without further checkpoints.
- **Batch 1 done (7aba301). Batch 2a done** (e6f2ed7; receive path: B17 truncation, receive-error counters, partial-header overrun after the header, M1-038); records it repaired are settled in one verified pass at the end of batch 2. **Batch 2b-i done** (8c9f405; clock, construction-time 2 ms scheduler + FIFO executor, posted sends, RobotLink isolation; three verification passes). **Batch 2b-ii done** (per-address connections, type-3 and timeout delete only that connection, timed-out flag, inbound creation, FinishConnection, posted Start/Stop, per-connection multipart, unconditional Dispose disconnect; two verification passes).
- **Next:** repair batches, each implementer -> verifier -> commit. Batch 1: the 8 conforming records become EXACT_SOURCE (tags, test oracles) and the LINK check names M1-033. Batch 2: transport differences. Batch 3: the app layer. A MISSING item, a needed new policy or an inventory error stops the loop and goes to the operator.

## Parked: MISSING items awaiting extraction and operator re-approval

Found during M1 repair. Each record stays IMPLEMENTATION_GAP; nothing is guessed. They go to one extractor pass, then an inventory correction the operator approves.

- **M1-006, R25(a):** does the 2.0 ms send-spacing gate (0x008363C8..0x008363FC) exempt lastSend == 0? The code exempts it (ReliableConnection.cs:176).
- **M1-002, B17:** do the UDP-layer TooSmall / BadPrefix drops count AddRecvError, and with which codes? B17 gives a code only for the truncated path (batch 2a added none). The batch 2a verifier read them in passing: UDPTransport::HandleReceivedMessage counts AddRecvError(0) for TooSmall (0x0083A7F0..0x0083A7F4), (2) for BadPrefix (0x0083A87E..0x0083A882) and (3) for a CRC mismatch (0x0083A90E..0x0083A912) on its own stats (this+8). This still needs an inventory correction before the code may use it.
- **M1-010, R35:** which host thread priority corresponds to the RelTransport Dispatch queue's "priority 3" (0x008367C2)? The threads are left at the default.
- **M1-035, R37:** what SendMessage does with the posted time the closure passes (which PendingMessage field, e.g. R20's +0 extQueued or +8 created). The time is passed but changes nothing. The batch 2b-i verifier read it in passing: SendMessage passes it to ReliableConnection::AddMessage (0x00836E9C), PendingMessage::Set stores it at +0x00 (0x00835802, R20's extQueued), and its only reader found is SendUnAckedMessages 0x00835FB8..0x00835FD8, which feeds RecentStatsAccumulator::AddStat(created - extQueued) at conn+0x70 on a message's first send: stats only, nothing on the wire. This still needs an inventory correction. Also: in sync mode QueueMessage passes 0.0 (0x00836B48).
- **M1-019, B21:** the Disconnect closure passes time 0.0 (0x0083800A, inside the cited closure 0x00837FFA; verifier reading); set to 0.0 in batch 2b-ii; needs an inventory correction only. Also: the sync-mode QueueMessage time 0.0 and ChangeSyncMode(true) keeping the queue come from a verifier reading, not a frozen row (0x00836B48, 0x00836862..0x00836878).
- **M1-010/M1-018, R34:** the order ReliableTransport::Update visits connections. The engine's map is ordered by TransportAddress::operator< (0x00838ED2..0x00838F62: the unsigned IPv4 word at +8, then the port at +0xC; verifier reading). The code uses creation order. Needs an inventory correction, then a code change.
- ~~M1-009, R23: multipart per connection or transport~~ answered by R23's own cited range (0x008374C6 connection → GetPendingMultiPartMessage 0x00835A94): per connection; repaired in batch 2b-ii.
- **M1-019, R39:** Start/Stop are posted through QueueAction (StartClient 0x008371F4..0x00837214, StopClient 0x00837284..0x008372A4 → QueueAction 0x4cd604; the row cites only the closure bodies 0x0083808E / 0x008380F6). Implemented as posted in batch 2b-ii per the verifier reading; the post needs an inventory correction.
- **M1-019, R38:** FinishConnection goes through QueueMessage (0x0083713A → QueueMessage, verifier reading); the code matches. Needs an inventory correction only.
- **M1-018, R13:** an empty container body means no creation (0x008377DC cmp.w fp,#0, verifier reading); the code matches. Needs an inventory correction only.
- **M1-025/M1-026 (batch 3), B23/B31:** whether the app layer filters connection events by address.
- **M1-009, R21/R22:** the +0x2B flush flag of each type-6 part built in SendMessage's split path (0x00836DC0..0x00836EB4). The code gives every part the caller's flag (ReliableConnection.cs:85).

## Layer order and review state

The review state of each subsystem is recorded in `re-analysis/fidelity_manifest.json`, and `re-analysis/FIDELITY_GAPS.md` renders it. Every subsystem is **UNREVIEWED**. Their records and their "source read / built" flags were written before this process, and nothing vouches for them yet.

| order | subsystem | review | notes |
| ---: | --- | --- | --- |
| 1 | M1-transport | INVENTORY_APPROVED (frozen 2026-09-24) | 31 to confirm/build, 5 policies, 1 hardware-only; candidate changes in the working tree (below) |
| 2 | M2-protocol | UNREVIEWED | |
| 3 | M3-device (camera, display, audio device) | UNREVIEWED | colour camera format is HARDWARE_ONLY |
| 4 | M4-control (motion, sensors, lights, cubes) | UNREVIEWED | the outbound cube-connection path is suspected unrecovered |
| 5 | M5-animation | UNREVIEWED | the live-animation wire lifecycle is suspected incomplete |
| 6 | M6-wwise-bank, M9-wwise-music | UNREVIEWED | singing holds BLOCKED_EXTERNAL Wwise runtime semantics |
| 7 | M7, M8, M10 (behaviour, framework, derived state) | UNREVIEWED | |
| 8 | M11–M15 (vision, manipulation, navigation, faces, freeplay) | UNREVIEWED | |
| – | tools | UNREVIEWED | offline tooling |

The operator can reorder layers 3 and up.

## M1 candidate (uncommitted working tree)

- **What it is:** the M1 repair done with ChatGPT on top of `bcab556`, applied from `~/Downloads/m1-repair-review.patch` and then `m1-final-corrections.patch`. It touches 27 files: `ReliableTransport.cs` (the bulk of it), `ReliableConnection.cs`, `CozmoRobot.cs`, `Cozmo.Transport.csproj`, conformance tools and tests, plus the new `TransportRepairTests.cs`.
- **Reported results:** 48/48 focused tests and 999/999 overall. That report comes from the ChatGPT session and the manager has not re-run it. It has had no hardware acceptance.
- **Treatment:** a candidate implementation. The M1 inventory decides what survives. Nothing is discarded or accepted before then.
- The setup work for this process touches none of those 27 files.

## Hardware

- Only the operator's machine reaches a robot. The manager never plans a step that assumes otherwise.
- New runs go to `re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-<test-id>/` (AGENTS.md, "Hardware runs").
- **Existing tooling, not yet reviewed under this process:**
  - `hardware-test` in `Cozmo.Conformance` asks the operator for a verdict per check, where this process wants the result judged from a machine-readable bundle.
  - Earlier bundles are in `cozmo-stack/re-analysis/acceptance/` and `cozmo-stack/cozmo-acceptance-*.json`.
  - `HardwareCatalog.cs` maps checks to fidelity ids.
  - Each is to be reviewed alongside the layer it tests.

## Legacy documents

`re-analysis/HANDOFF.md`, `NEXT_MILESTONE.md`, the milestone documents and the claims of "complete offline" predate this process. They are notes, the lowest authority. Where they disagree with an approved inventory, the inventory wins.

The user-level agents in `~/.claude/agents/` (`cozmo-m1-transport-auditor`, `cozmo-m2-protocol-auditor`, `cozmo-proof-auditor`, `cozmo-adversarial-reviewer`) come from an earlier salvage audit and point at a stale scratch directory. The process uses the project agents in `.claude/agents/` instead.

## Accepted commits

| commit | subsystem | what it accepted |
| --- | --- | --- |
| 7aba301 | M1-transport | M1-004, M1-011, M1-012, M1-017 settled EXACT_SOURCE with evidence-derived tests; LINK check names M1-033. M1-003 and M1-008 tagged but held (DeleteConnection semantics; clock) |

## Open decisions for the operator

- none pending
