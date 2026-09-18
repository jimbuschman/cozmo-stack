# cozmo-stack

The beginning of the standalone replacement Cozmo stack. No Android app, no `libcozmoEngine.so`, no Python at runtime.
Everything in here is derived from the official engine binary (authoritative), Anki's open-sourced transport library
(identical code) and PyCozmo's hardware captures (test fixtures only). See `../re-analysis/TRANSPORT_SPEC.md` for the
reconstructed protocol and `../re-analysis/CAPABILITY_GAP.md` for the roadmap.

```
Cozmo.sln
src/Cozmo.Protocol      wire format: 14-byte frame header, sub-message framing, sequence ids, ping payload,
                        CLAD reader/writer, official 161-message catalog (generated), typed messages for the
                        handshake/telemetry/harmless-command set, raw fallback for everything else
src/Cozmo.Transport     ReliableConnection (port of Anki::Util::ReliableConnection with the engine's tunables),
                        ReliableTransport (UDP socket, receive + 2 ms update threads, official receive semantics),
                        RobotLink (typed facade: identity, telemetry, handshake, SetHeadAngle/backpack LEDs)
src/Cozmo.Conformance   `cozmo-conformance` CLI: decode / diff / fixtures / catalog / pcap / replay / fakerobot / connect
tests/                  xunit: PyCozmo hardware fixtures round-trip, official sizes, reliability state machine
```

Requires the .NET 10 SDK (present on this machine).

```
dotnet build Cozmo.sln
dotnet test  Cozmo.sln                                   # 69 tests
dotnet run --project src/Cozmo.Conformance -- fixtures  # PyCozmo captures decode + re-encode byte-identically
dotnet run --project src/Cozmo.Conformance -- catalog 0xc2
```

## Loopback self-test (no hardware)

```
dotnet run --project src/Cozmo.Conformance -- fakerobot --seconds 30 &
dotnet run --project src/Cozmo.Conformance -- connect 127.0.0.1 --seconds 6 --head 0.3 --led --log frames.log
```

## Hardware smoke test (milestone success criterion)

1. Power the robot on its charger; join its Wi-Fi network `Cozmo_XXXXXX` with the password shown on its face
   (the robot is `172.31.1.1`, your machine gets a 172.31.1.x address).
2. `dotnet run --project src/Cozmo.Conformance -- connect 172.31.1.1 --seconds 20 --head 0.4 --log frames.log`
3. Expected: `connected in <100 ms`, RobotAvailable + FirmwareVersion (v2381, CLAD hashes `9e4a965a…`/`a259247f…`),
   MfgId after GetManufacturingInfo, SyncTimeAck, RobotState at ~30 Hz for the whole run with `pending` staying near 0
   and few resends, MotorActionAck after SetHeadAngle and the head angle in RobotState moving to the target, then a
   clean DisconnectRequest. `frames.log` holds every raw frame for byte-level comparison (`diff`, `replay`).
4. Add `--led` to light the backpack, `--headlight` for the IR LED, `--origin` to also send PyCozmo's
   AbsoluteLocalizationUpdate. Use `--port 5552` only against a Webots simulator.

If the connect times out: the machine must be on the robot's AP (source address 172.31.1.x); the robot drops
everything else. If frames larger than 1037 bytes are ever needed, `TransportOptions.MaxFramePayloadBytes` is the knob
(engine builds up to 1406; robot tolerance still to be measured).

## Regenerating the message catalog

```
python ../re-analysis/tools/gen_message_catalog.py ../re-analysis/protocol .
```
