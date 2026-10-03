# Q18 — settled/built records without a live production-entry test

## Coverage audit (2026-10-01)

| record | coverage | strongest test entry | unchecked basis affecting conclusions |
| --- | --- | --- | --- |
| M1-001 | CHECKED | L | None. |
| M1-002 | CHECKED | L | None. |
| M1-003 | CHECKED | L | None. |
| M1-004 | CHECKED | L | None. |
| M1-005 | CHECKED | L | None. |
| M1-006 | CHECKED | L | None. |
| M1-007 | CHECKED | L | None. |
| M1-008 | CHECKED | L | None. |
| M1-009 | CHECKED | L | None. |
| M1-010 | CHECKED | L | None. |
| M1-011 | CHECKED | L | None. |
| M1-012 | CHECKED | L | None. |
| M1-016 | CHECKED | L | None. |
| M1-017 | CHECKED | L | None. |
| M1-018 | CHECKED | L | None. |
| M1-019 | CHECKED | L | None. |
| M1-020 | CHECKED | L | None. |
| M1-021 | CHECKED | L | None. |
| M1-022 | CHECKED | L | None; host-socket equivalence is exercised through the production transport methods. |
| M1-023 | CHECKED | L | None. |
| M1-024 | CHECKED | L | None. |
| M1-026 | CHECKED | L | None. |
| M1-027 | CHECKED | L | None. |
| M1-028 | CHECKED | L | None. |
| M1-030 | CHECKED | L | None. |
| M1-032 | CHECKED | L | None. |
| M1-035 | CHECKED | L | None. |
| M1-041 | CHECKED | L | None. |
| M2-001 | CHECKED | L | None. |
| M2-002 | CHECKED | L | None. |
| M2-003 | CHECKED | L | None. |
| M2-004 | CHECKED | L | None. |
| M2-005 | CHECKED | L | None. |
| M2-006 | CHECKED | L | None. |
| M2-007 | CHECKED | L | None. |
| M2-008 | CHECKED | L | None. |
| M2-009 | CHECKED | L | None. |
| M2-010 | CHECKED | L | None. |
| M2-011 | CHECKED | L | None. |
| M2-012 | CHECKED | L | None. |
| M2-013 | CHECKED | L | None. |
| M2-014 | CHECKED | L | None. |
| M2-015 | CHECKED | L | None. |
| M2-016 | CHECKED | L | None. |
| M3-002 | CHECKED | L | None. |
| M3-003 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M3-005 | CHECKED | L | None. |
| M3-006 | CHECKED | L | None. |
| M3-007 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M3-009 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M3-010 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M3-011 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M3-012 | CHECKED | L | None. |
| M3-014 | CHECKED | L | None. |
| M3-015 | CHECKED | L | None. |
| M3-018 | CHECKED | L | None. |
| M3-022 | CHECKED | L | None. |
| M3-024 | CHECKED | S | None; the checked conclusion is a test-entry gap. |
| M3-025 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M3-026 | CHECKED | L | None. |
| M3-028 | CHECKED | L | None. |
| M3-029 | CHECKED | L | None. |
| M3-030 | CHECKED | L | None. |
| M3-031 | CHECKED | L | None. |
| M3-033 | CHECKED | L | None. |
| M3-034 | CHECKED | L | None. |
| M3-035 | CHECKED | L | None. |
| M4-001 | CHECKED | L | None. |
| M4-002 | CHECKED | L | None. |
| M4-003 | CHECKED | L | None. |
| M4-004 | CHECKED | L | None. |
| M4-005 | CHECKED | L | None. |
| M4-007 | CHECKED | L | None. |
| M4-011 | CHECKED | H | None; its test covers only Init placement, not persistence grammar or RSSI tie order. |
| M4-012 | CHECKED | N | None; the manifest names no test. |
| M4-014 | CHECKED | L | None. |
| M4-015 | CHECKED | L | None. |
| M4-016 | CHECKED | L | None. |
| M4-020 | CHECKED | L | None. |
| M4-022 | CHECKED | L | None. |
| M4-023 | CHECKED | L | None. |
| M4-025 | CHECKED | L | None. |
| M5-001 | CHECKED | A/H | None; the checked conclusion is a test-entry gap. |
| M5-002 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M5-003 | CHECKED | H | None; all four tests call face/layer helpers directly. |
| M5-004 | CHECKED | L | None. |
| M5-006 | CHECKED | L | None. |
| M5-007 | CHECKED | L | None. |
| M5-008 | CHECKED | L | None. |
| M5-009 | CHECKED | L | None. |
| M5-010 | CHECKED | L | None. |
| M5-012 | CHECKED | L | None. |
| M5-015 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M5-016 | CHECKED | L | None. |
| M5-019 | CHECKED | L | None. |
| M5-023 | CHECKED | L | None. |
| M5-024 | CHECKED | L | None. |
| M5-025 | CHECKED | L | None. |
| M5-026 | CHECKED | L | None. |
| M5-028 | CHECKED | S | None; the parallel-idle half is a test-entry gap. |
| M5-029 | CHECKED | L | None. |
| M5-033 | CHECKED | L | None. |
| M5-034 | CHECKED | A/H | None; the checked conclusion is a test-entry gap. |
| M6-003 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M6-019 | CHECKED | H/A | None; direct parser tests, and the shipped-asset test can return early. |
| M6-026 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M7-001 | CHECKED | L | None. |
| M7-004 | CHECKED | S | None; the checked conclusion is a test-entry gap. |
| M7-011 | CHECKED | L | None. |
| M7-019 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M7-021 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M9-001 | CHECKED | L | None. |
| M9-012 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M9-018 | CHECKED | L | None. |
| M9-019 | CHECKED | H | None; the checked conclusion is a test-entry gap. |
| M9-021 | CHECKED | L | None. |
| M10-002 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M10-005 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M10-006 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M10-009 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M10-010 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M10-011 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M10-012 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M11-001 | CHECKED | L | None. |
| M11-002 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-003 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-004 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-005 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-006 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-007 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-008 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-009 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-010 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-011 | CHECKED | L | None. |
| M11-013 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-014 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-015 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-018 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-019 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-020 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-022 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-023 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-024 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-025 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-026 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-027 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-028 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-029 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-030 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-031 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-032 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-034 | CHECKED | N | None; the manifest names no test. |
| M11-036 | CHECKED | N | None; the manifest names no test. |
| M11-037 | CHECKED | L | None. |
| M11-039 | CHECKED | N | None; the manifest names no test. |
| M11-040 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-041 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-043 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-044 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M11-049 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-003 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-004 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-005 | CHECKED | L | None. |
| M12-006 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-009 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-011 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-013 | CHECKED | L | None. |
| M12-015 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-016 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-017 | CHECKED | L | None. |
| M12-018 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-021 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-022 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-025 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-026 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-029 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-030 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-031 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-033 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-035 | CHECKED | L | None. |
| M12-036 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M12-037 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-001 | CHECKED | H/A | None; the manifest's exact class/method reference is stale and the located method is asset-silent. |
| M13-002 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-003 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-004 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-005 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-006 | CHECKED | L | None; `BeaconTests` is declared in `RestingFlatTests.cs`. |
| M13-007 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-008 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-009 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-010 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-011 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-012 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-013 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-014 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-015 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-016 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-017 | CHECKED | L | None; it enters the production behavior class on the standard manipulation rig. |
| M13-018 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-019 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-020 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-022 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M13-028 | CHECKED | H/S/A | None; the checked conclusion is a test-entry gap. |
| M14-001 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M14-002 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M14-003 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M14-004 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M14-005 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M14-008 | CHECKED | L | None. |
| M14-011 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M14-012 | CHECKED | H/S | None; the checked conclusion is a test-entry gap. |
| M15-004 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M15-007 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M15-008 | CHECKED | L | None. |
| M15-009 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M15-010 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M15-011 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M15-012 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M15-013 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M15-014 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M15-015 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| M15-017 | CHECKED | H/A | None; the checked conclusion is a test-entry gap. |
| TOOL-004 | CHECKED | L | None. |

No conclusion rests on unchecked work. `L/H/S/A/N` are test-entry conclusions, not
coverage labels; every scoped record's current test field, located method/class and
production owner were checked. The grouped sections below give the exact current test
families and proper live entries without repeating long multi-method manifest fields in
this 224-row coverage table.

Request: `requests/20261001-codex-queue-2.md`, Q18. Snapshot: `origin/main` / `415b9e0`, pulled before the audit.

Scope is every current settled record (`EXACT_SOURCE` or `EQUIVALENT_IMPLEMENTATION`, as
`fidelity.py` defines `SETTLED`) plus every record whose `unresolved` starts `built`:
**224 unique records** (171 exact, 2 equivalent, 54 built, with 3 exact/built overlaps).
I read each manifest `test`, located the named method/class, and classified its strongest test:

- **L** — at least one test enters through the live C# production entry and reaches the claimed behavior.
- **H** — helper/parser/model test only. It may prove the isolated body or bytes, but not live ownership.
- **S** — a test seam/parallel implementation replaces part of the production path.
- **A** — the only apparent production-shaped test can return successfully when required assets are absent.
- **N** — no test is named.

`H/S/A/N` are the requested gaps. “Proper entry” below names what a replacement must drive. Circular expected values are separately catalogued in `20260930-circular-tests.md`; this report is about entry and reachability.

## Result by subsystem

| subsystem | scope | L | H/S/A/N gap |
|---|---:|---:|---:|
| M1 | 28 | 28 | 0 |
| M2 | 16 | 16 | 0 |
| M3 | 23 | 16 | 7 |
| M4 | 15 | 13 | 2 |
| M5 | 21 | 15 | 6 |
| M6 | 3 | 0 | 3 |
| M7 | 5 | 2 | 3 |
| M9 | 5 | 3 | 2 |
| M10 | 7 | 0 | 7 |
| M11 | 37 | 3 | 34 |
| M12 | 22 | 4 | 18 |
| M13 | 22 | 2 | 20 |
| M14 | 8 | 1 | 7 |
| M15 | 11 | 1 | 10 |
| tools | 1 | 1 | 0 |
| **total** | **224** | **105** | **119** |

The lower wire/device layers generally have real entries. The dominant higher-layer pattern is a direct helper/action/offline-vision test cited for a claim whose actual production owner is the engine tick, mailbox, action list or freeplay chooser.

## Explicit no-test records

| record | named test | proper production entry |
|---|---|---|
| `M4-012` | **N** — empty | game/public calibration action → `CozmoMotion` → routed `MotorCalibration`, including carrying detach |
| `M11-034` | **N** — empty | engine Update → `VisionSystem` mode schedule → camera request/mode transitions |
| `M11-036` | **N** — empty | camera mailbox/result dispatch → `UpdateVisionMarkers` → live `BlockWorld` |
| `M11-039` | **N** — empty | connection calibration callback → `UpdateCameraCalibration` → `MarkerDetector.Init` |

## Asset-silent tests

The repository's `AssetPresenceTests` fails an ordinary run without assets, but many individual tests still contain a bare successful `return`; setting `COZMO_TESTS_WITHOUT_ASSETS=1` turns those into silent passes. Such a test cannot be the only evidence for a record.

| records | test path | early return | proper entry |
|---|---|---|---|
| `M5-001`, `M5-034` | `AnimationAssetTests`, portions of `M5AnimationTests` | `AnimationAssetTests.cs:68,90,131,175,192-193,227,247,278-279,302-303`; `M5AnimationTests.cs:251,739,744` | production `AnimationLibrary.Load` with required OBB and shipped animation map |
| `M7-001`, `M7-011`, `M7-019`, `M7-021` where their only end-to-end case is asset-backed | `TriggerTests`, `BehaviorTests`, `M7BehaviorTests`, `CorrectionTests` | `TriggerTests.cs:140,171,184`; `BehaviorTests.cs:188-234`; `M7BehaviorTests.cs:129-193,297`; `CorrectionTests.cs:45,83,138,189,...` | `FreeplayStack`/reaction-map load → real bound behavior → engine tick/action completion |
| M11/M12/M13 asset-dependent rows below | `M11RVisBuildTests`, `M12RVisBuildTests`, `M13RVisBuildTests`, `NavigationTests`, `ManipulationTests` | dozens of `if (Lib/obb/prims is null) return`; the R-VIS helpers intentionally return false under `COZMO_TESTS_WITHOUT_ASSETS=1` | load the OBB/marker library/motion primitives as production does; otherwise explicitly skip, never pass |
| M15 rows below | `FreeplayTests`, `NavigationTests`, `ManipulationTests` | `FreeplayTests.cs:136,711,741,936,981,1216,1252,1429,2307` plus navigation/manipulation returns | `FreeplayStack` created with shipped configs and animation library, then driven by engine ticks |

## Helper/seam gaps and required entries

### M3-device

| records | strongest current test | gap | proper entry |
|---|---|---|---|
| `M3-003` | direct `MiniJpeg` conversion | **H** | `ImageChunk` route → `CozmoCamera` completion → live decode/vision handoff |
| `M3-007`, `M3-009` | direct `FaceBitmapCodec` patterns | **H** | animation/face API → `AnimationScheduler` frame → `DisplayFaceImage` send |
| `M3-010`, `M3-011` | direct mu-law encoder/constants | **H** | live audio keyframe/playback → scheduler → encoded `AudioSample` |
| `M3-025` | direct tag/size/base-tag table calls | **H** | connection/component caller → `NVStorage.Read` → Update arm/reply validation |
| `M3-024` | firmware handler tests only stored selector | **S** | firmware message → selector → `CreateAudioAnimation` → distinct robot/device playback branch |

All other in-scope M3 records have at least one routed message, connection, scheduler or NV Update test: `M3-002 005 006 012 014 015 018 022 026 028 029 030 031 033 034 035`.

### M4-control

`M4-012` has no test. `M4-011` names
`EngineAppLayerTests.M4_011_BlockFilterInitRunsFromSetPhysicalRobotNotTheAppDefaults`,
which enters the firmware route and proves where `BlockFilter.Init` runs, but never
parses/writes `blockPool.txt` and never creates an equal-RSSI choice. It is therefore
**H/partial**, not a live test of the record's persistence grammar and tie-order claim.
Proper entry: two sessions through physical-robot firmware setup using the production
pool file, then equal-RSSI discovery through `CubeConnectionCoordinator`. The other
thirteen records have a `CozmoMotion`, routed state/cube event, firmware handler or
connection-init entry test.

### M5-animation

| records | current test | gap | proper entry |
|---|---|---|---|
| `M5-002`, `M5-015` | direct `ProceduralFace`/renderer calls | **H** | shipped clip/live-layer keyframe → scheduler renderer → face wire frame |
| `M5-003` | four direct `TrackLayerComponent`/`ProceduralFacePose` helper tests | **H** | loaded face track → `AnimationScheduler.BuildFrameLocked` → `ApplyLayersToAnim` → face wire frame |
| `M5-001`, `M5-034` | loader/map tests | **A/H** | production library load with mandatory shipped assets, then trigger lookup/play |
| `M5-028` | scheduler tests exist, but some claimed idle semantics are also tested through `IdleBehavior` | **S** for the parallel-idle half | `CozmoEngine.AnimationStreamerUpdate` → live `AnimationScheduler` keep-alive/layers only |

The remaining fifteen have at least one live scheduler/library entry: `M5-004 006 007 008 009 010 012 016 019 023 024 025 026 029 033`.

### M6-wwise-bank

| record | current test | gap | proper entry |
|---|---|---|---|
| `M6-003` | direct ADPCM block decoder | **H** | `WwisePlayback` media selection → live source/voice decoder → mixer/frame driver |
| `M6-019` | direct STMG parser and shipped-chunk parse | **H** | Init bank load through live Wwise runtime/library construction |
| `M6-026` | `WwisePlaybackLimiterTests` directly call extracted bridge/walker bodies | **H/S** | live event → PBI/voice lifecycle → playback-limit walker and resulting voice stop |

These tests are excellent body tests; none proves the recovered body is the one live audio runs.

### M7-behaviour

| record | gap | proper entry |
|---|---|---|
| `M7-004` | **S:** `IdleTests` constructs the tool-only `IdleBehavior`, not AnimationStreamer | engine Update → `AnimationScheduler` live-idle defaults/use |
| `M7-019` | **H/A:** direct reaction behavior/asset-conditional tests | routed cliff/pickup trigger → reaction map → real behavior/action list |
| `M7-021` | **H/A:** direct sensor flag and pickup behavior tests | RobotState/MotorCalibration route → behavior trigger → full live state machine |

`M7-001` is shipped enum data (its generated enum is the production consumer), and `M7-011` has an arbiter/config entry test; they count L for their narrow claims.

### M9-wwise-music

`M9-012` and `M9-019` are **H**: their tests parse/inspect bank nodes directly. A production test should load the banks through `WwiseAudioSource` and demonstrate that the MIDI/blend branches are unreachable. `M9-001`, `M9-018`, and `M9-021` have behavior/audio-source/scheduler entries.

### M10-derived

All seven are **H/S**:

| records | current entry | proper entry |
|---|---|---|
| `M10-002`, `M10-005`, `M10-006`, `M10-009`, `M10-010`, `M10-011`, `M10-012` | direct detector/strategy calls in `DerivedStateTests`; runnable behavior stand-ins for reaction predicates | routed RobotState/AnimationState/cube observation → `CozmoEngine` tick → real derived component → real reaction/Needs consumer |

The tests can pin isolated arithmetic. They do not establish tick position, all gates, or a real downstream behavior.

### M11-vision

Only `M11-001`, `M11-011`, and `M11-037` have an adequate production entry (camera/connection/firmware routing) among this scope. The remaining 34 are gaps:

- **N:** `M11-034`, `M11-036`, `M11-039`.
- **S (offline `ProcessImage` or direct mailbox/history):** `M11-002 003 004 005 006 007 008 009 010 013 014 015 018 019 020 022 023 024 025 026 027 028 029 030 031 032 040 041 043 044 049`.

Their proper entry is camera `FrameForVision` → production one-slot mailbox/vision thread → result deque → `CheckMailbox`/BlockWorld/FaceWorld. Numeric helper tests may remain, but each live record needs at least one test through that chain. `M11RVisBuildTests`' `NeedsLibrary()` is also asset-silent under the explicit no-assets mode.

### M12-manipulation

Four records have a routed dock/manipulation entry test: `M12-005`, `M12-013`, `M12-017`, `M12-035`. The other eighteen are **H/S/A**:

`M12-003 004 006 009 011 015 016 018 021 022 025 026 029 030 031 033 036 037`.

The named tests primarily instantiate docking/pre-action/placement helpers or action models directly, commonly on `ManipRig`'s offline `VisionSystem` and optional marker library. Proper entry: a game/freeplay behavior queues the actual manipulation action on the live action list; robot/vision messages complete it; assertions cover emitted wire and world/needs effects.

### M13-navigation

Only `M13-006` (live AIWhiteboard owner) and `M13-017` (behavior entry, though Q15 finds its implementation wrong) have a production-shaped entry. The other twenty are **H/S/A**:

`M13-001 002 003 004 005 007 008 009 010 011 012 013 014 015 016 018 019 020 022 028`.

Current tests call the primitive parser/planner/action/helper directly, use a virtual `NullPlanner`, use the straight-line/async action models, or return when assets are absent. Proper entry: `ManipulationSystem` successfully loads shipped primitives → a real `DriveTo*`/behavior queues an action → engine tick/action list/planner → `PathSender` → routed terminal event. Pan/tilt and flip tests must include the compound/action framework rather than direct child calls.

`M13-001` also has a stale exact test reference: the manifest names
`MotionPrimitiveTests.TheShippedMotionPrimitivesParseAsTheEngineReadsThem`, but that
method is in `NavigationTests.cs:84`; it returns when the OBB is absent. `M13-006` does
not have that problem: `BeaconTests` is a real class declared in
`RestingFlatTests.cs:71`, though its method is appropriately counted only for the
record's narrow live `AIWhiteboard` owner.

### M14-faces

`M14-008` has routed FaceWorld event tests. The other seven are **H/S**:

`M14-001 002 003 004 005 011 012`.

They call geometry/actions/recognizer/album helpers or fake OKAO directly. Proper entry: camera frame → production detector boundary → engine-side carrier/recognizer → FaceWorld → real face behavior/action; album tests should enter via the connection NV reads.

### M15-freeplay

`M15-008` has a behavior-to-Needs action completion entry. The other ten are **H/A**:

`M15-004 007 009 010 011 012 013 014 015 017`.

They construct Needs/activity/data-tracker/freeplay helpers or behaviors directly, and many can return without OBB/marker assets. Proper entry: `FreeplayStack` built with shipped configs/library → connection edges → engine/freeplay ticks → chooser/reaction/action callback → Needs/data persistence or wire effect.

## Complete per-record classification

Every scoped record appears below. An id not called out in the gap sections is `L`; the letters here make the per-record result explicit.

- **M1:** `L` — `001 002 003 004 005 006 007 008 009 010 011 012 016 017 018 019 020 021 022 023 024 026 027 028 030 032 035 041`.
- **M2:** `L` — `001 002 003 004 005 006 007 008 009 010 011 012 013 014 015 016` (for codec/layout records, Pack/Parse is itself the production entry).
- **M3:** `H/S` — `003 007 009 010 011 024 025`; `L` — `002 005 006 012 014 015 018 022 026 028 029 030 031 033 034 035`.
- **M4:** `H` — `011`; `N` — `012`; `L` — `001 002 003 004 005 007 014 015 016 020 022 023 025`.
- **M5:** `H/A/S` — `001 002 003 015 028 034`; `L` — `004 006 007 008 009 010 012 016 019 023 024 025 026 029 033`.
- **M6:** `H/S` — `003 019 026`.
- **M7:** `H/S/A` — `004 019 021`; `L` — `001 011`.
- **M9:** `H` — `012 019`; `L` — `001 018 021`.
- **M10:** `H/S` — `002 005 006 009 010 011 012`.
- **M11:** `L` — `001 011 037`; `N` — `034 036 039`; `H/S/A` — every other scoped M11 id.
- **M12:** `L` — `005 013 017 035`; `H/S/A` — `003 004 006 009 011 015 016 018 021 022 025 026 029 030 031 033 036 037`.
- **M13:** `L` — `006 017`; `H/S/A` — `001 002 003 004 005 007 008 009 010 011 012 013 014 015 016 018 019 020 022 028`.
- **M14:** `L` — `008`; `H/S` — `001 002 003 004 005 011 012`.
- **M15:** `L` — `008`; `H/A` — `004 007 009 010 011 012 013 014 015 017`.
- **tools:** `L` — `TOOL-004`.

## Recommended enforcement

Add a machine-readable `test_entry` field or convention (`LIVE`, `HELPER`, `SEAM`, `ASSET`) and reject settlement when no `LIVE` test exists. Also replace bare asset-missing `return` with an explicit skip/failure whose reason appears in test output; `COZMO_TESTS_WITHOUT_ASSETS=1` should mark affected fidelity tests skipped, never passed.
