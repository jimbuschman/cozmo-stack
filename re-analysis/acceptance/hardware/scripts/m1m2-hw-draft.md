# M1M2-HW: the M1+M2 acceptance run

**APPROVED for the operator's run (manager, 2026-10-18),** at the commit that contains it. The file keeps its `-draft`
name because the tool hashes this path into `env.json`. Dry-run without a robot (loopback, shipped resources): STARTUP
came out `OBSERVED_STARTUP_AS_SOURCE` (997 names, 0→1, 1→2, 2→3, EndOfMessage 0, progress ending at 1.0 in about 1 s);
the robot checks were INCONCLUSIVE as expected.

It never commits, never edits the manifest, **never moves a motor and never writes the robot's NV storage.** Every check
ends **OBSERVED_\*** (with the rule that produced it) or **INCONCLUSIVE** (with what is missing). There is no PASS: the
manager judges the bundle. A hardware observation never raises provenance (AGENTS.md).

## What it is for

M1-transport and M2-protocol have no implementation gap left (`PROJECT_STATE.md`, 2026-10-18). Their acceptance needs
this run: the production path the layers serve, on the operator's robot, and evidence for **M1-033** (HARDWARE_ONLY,
robot-side transport behaviour). The tool reads `re-analysis/fidelity_manifest.json` first and stops (exit 2, no
network) if any record it names is missing; it warns if M1-033 is no longer HARDWARE_ONLY or another record is not
settled or policy.

## Where it is, and how it is built

`cozmo-stack/src/Cozmo.Conformance/M1M2Hw.cs`, command `m1m2-hw`. It creates the production engine with the shipped
resources and observers attached through the public `BeforeStart` option, then connects the way
`CozmoRobot.ConnectAsync` does: wait for delivered readiness, then `ConnectToRobot` (the engine does not expose its
transport to `BeforeStart`, so the first connection is made this way to record every frame from `FrameTrace`). The last
check connects a second engine through the real `CozmoRobot.ConnectAsync`. Structure test:
`tests/Cozmo.Protocol.Tests/M1M2HwTests.cs`.

## Physical setup

- Computer on the robot's Wi-Fi (172.31.1.1 default). Close the official app and every other client.
- Cozmo on its treads, **off** the charger, battery not low. Nothing moves.
- Keep the console and the robot in view: in WIRE you are asked to tap the top of Cozmo's head once.

## Operator commands

From the repository root, on current main:

```powershell
dotnet build cozmo-stack/Cozmo.sln
dotnet cozmo-stack/src/Cozmo.Conformance/bin/Debug/net9.0/cozmo-conformance.dll m1m2-hw 172.31.1.1 --resources <path to cozmo_resources>
```

`--resources` is required (`--obb`/`--assets` also work); **`--state-dir` is refused.** Options: `--skip-connect-async`,
`--operator <label>`, `--out <dir>`, `--yes` (no prompts; the tap is skipped). Ctrl+C stops, disposes (bounded) and
still writes the bundle; a 10-minute watchdog does the same. The whole run takes about 1.5 minutes.

## Bundle

`re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-M1M2-HW/`: `result.json` (per check: verdict, rule, problems,
measurements), `env.json` (git HEAD/status, command line, sha256 of the tool, this document and the assemblies,
timings), `records.json`, `host-events.jsonl` (every report delivered to the C# API, in delivery order),
`events.jsonl` (engine and stack log, transport events, the operator's answer), `frames.jsonl` (every UDP frame in and
out, full hex), `needs/nv-0x194000.bin` (the robot's needs record, read only), `hashes.json`, and copies of the tool
and this document. Copy the whole folder back.

## Checks (run order)

| Check | Records | What it observes | `AS_SOURCE` rule |
| --- | --- | --- | --- |
| STARTUP | M1-054, M1-055, M1-056, M1-058 | UpdateEngineState, loading progress, AnimationAvailable, EndOfMessage, readiness, through BeforeStart | 0→1, 1→2, 2→3 in order; 997 distinct names, no `_PROCEDURAL_`; names, then EndOfMessage, then 2→3; progress non-decreasing, last 1.0; IsReadyDelivered |
| CONNECT | M1-040, M1-042, M2-021 | ConnectToRobot after delivered readiness; ConnectionResponse, firmware, serial; time sync, first full state, ready to stream; BlockPoolData | Success with all three; exactly one BlockPoolData, after the response |
| LATENCY | M1-061, M1-062 | 12 s of LatencyMessage, then 4 s with `SetGameBeingPaused(true)`, then 6 s resumed | median period within 120 ms of 660 ms (11 × 60 ms); wifi/extSend/send all 0; unity, sdk and image avg/min/max = (0, +inf, −inf); none from 200 ms after the pause until resume; some after |
| WIRE (tap phase) | M1-033, M2-024 | the operator taps Cozmo's head once | — (data for WIRE) |
| DISCONNECT | M1-019, M1-044 | Dispose: frames sent, the DisconnectRequest, the end of inbound traffic | a DisconnectRequest after Dispose; Dispose within 5 s |
| NEEDS_READ | M3-033, M1-054 | NV commands and results for 0x194000 over the whole run; the gate's MISSING | two READs per connection (the backup sweep, then the needs read: M3-033), no WRITE/ERASE/WIPE of 0x194000; the MISSING naming M15-030 logged |
| WIRE | M1-033, M2-024 | frame sizes and types each way, packing, reliable sequence range, resends, frame errors; arrivals of 0xBA, 0xBB, 0xC3, 0xB0, 0xEC, 0xB1, 0xB2, 0xB7, 0xBC, 0xBD; RobotPoked deliveries; unknown tags | observation only (`OBSERVED_LINK`) |
| CONNECT_ASYNC | M2-021, M1-056 | a second engine through the real `CozmoRobot.ConnectAsync` | Success; readiness delivered before the response; the full startup seen; BlockPoolData; Dispose within 5 s |

Known differences the bundle will show (recorded decisions, not failures):

- **Needs persistence is off** (B-M1-10; `research/20261018-b-m1-10-check.md`): the stack reads 0x194000 and skips
  everything the native resolver would do with it. Natively, with a version-5 record from the official app and no
  device copy (case 5), nothing is written back; with no record (case 1) or a v1–4 record, native writes 0x194000. The
  stack writes nothing in any case.
- **Motor stop on shutdown:** the stack sends StopAllMotors and DriveWheels(0) before the DisconnectRequest; the original
  sends only the DisconnectRequest (`PROJECT_STATE.md` decision note).
- **GetBlockPool** is supplied by the stack as the app's input (M1-042).
- **Firmware:** any version is accepted and logged (M1-040).
- **Not exercised:** sequence wraparound (about 65534 reliable messages); maximum-size frames only as far as
  `maxFrameBytes` shows (bound 1406).

## Corrections after the first run (manager, 2026-10-18)

Bundle `20261018-170546-M1M2-HW` showed two tool bugs, fixed in the tool: LATENCY compared serialized JSON, where
`+inf` is written `+inf` (the values were right); NEEDS_READ expected one READ of 0x194000, but M3-033 has two
per connection (the backup sweep and the needs read). Neither changed what the run sent the robot.

## What the manager does with the bundle

Reads `result.json` and the raw files; checks each `AS_SOURCE` rule against `host-events.jsonl` and `frames.jsonl`;
records M1-033's observation (frame sizes, packing mix, sequence range, resends) on the record; compares the saved
needs record with the hand-encoded v5 fixture (B-M1-10 check A4); and, if nothing contradicts the settled records, sets
M1-transport and M2-protocol to ACCEPTED with the accepted commit.
