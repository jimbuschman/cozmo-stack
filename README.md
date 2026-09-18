# cozmo-stack

A standalone replacement stack for the Anki Cozmo robot, built from the official app/engine as the authority.
Private working repository.

| path | what |
|---|---|
| `cozmo-stack/` | **the code**: C#/.NET 9 (or newer) solution — `Cozmo.Protocol` (wire format, official 161-message catalog, typed messages), `Cozmo.Transport` (Anki reliable UDP transport with the engine's tunables), `Cozmo.Conformance` (CLI: decode/diff/fixtures/catalog/pcap/replay/fakerobot/connect), tests. See `cozmo-stack/README.md` for build and the **hardware smoke test**. |
| `re-analysis/TRANSPORT_SPEC.md` | reconstructed engine↔robot transport specification (frames, sequence ids, acks, resends, pings, handshake) |
| `re-analysis/CAPABILITY_GAP.md` | official stack vs PyCozmo capability-gap analysis, target architecture, milestones |
| `re-analysis/OBB_INVENTORY.md` | inventory of the app's OBB resources and the seven official firmware images (hashes only; images are not in the repo) |
| `re-analysis/README.md` | notes on `libcozmoEngine.so` and the Python RE tooling in `re-analysis/tools/` |
| `re-analysis/protocol/` | official tag map, size comparison with PyCozmo, recovered field widths |
| `re-analysis/reference/` | PyCozmo snapshot (MIT) and Anki's open-sourced `util/transport` (from the Vector repository) — comparison references only |

Not in the repo (regenerate locally): the decompiled APK folders (`resources/ sources/ smali/ unity/`), the unpacked OBB
(`re-analysis/obb/`, 441 MB incl. `cozmo.safe` firmware), and the large symbol dumps (`re-analysis/tools/*.py` rebuild them
from the `.so`).

## Quick start (Windows/Linux/macOS with .NET 9 (or newer) SDK)

```
cd cozmo-stack
dotnet test Cozmo.sln
dotnet run --project src/Cozmo.Conformance -- fixtures
# on the robot's Wi-Fi (Cozmo_XXXXXX):
dotnet run --project src/Cozmo.Conformance -- connect 172.31.1.1 --seconds 20 --head 0.4 --log frames.log
```
