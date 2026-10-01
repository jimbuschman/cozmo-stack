# Q18 — settled/built records without a live production-entry test

Request: `requests/20261001-codex-queue-2.md`, Q18. Snapshot: `origin/main` / `954c092`, pulled before the audit.

Scope is every current `EXACT_SOURCE` record plus every record whose `unresolved` starts `built`: **222 unique records** (171 exact, 54 built, with 3 records in both selections). I read each manifest `test`, located the named method/class, and classified its strongest test:

- **L** — at least one test enters through the live C# production entry and reaches the claimed behavior.
- **H** — helper/parser/model test only. It may prove the isolated body or bytes, but not live ownership.
- **S** — a test seam/parallel implementation replaces part of the production path.
- **A** — the only apparent production-shaped test can return successfully when required assets are absent.
- **N** — no test is named.

`H/S/A/N` are the requested gaps. “Proper entry” below names what a replacement must drive. Circular expected values are separately catalogued in `20260930-circular-tests.md`; this report is about entry and reachability.

## Result by subsystem

| subsystem | scope | L | H/S/A/N gap |
|---|---:|---:|---:|
| M1 | 27 | 27 | 0 |
| M2 | 16 | 16 | 0 |
| M3 | 23 | 16 | 7 |
| M4 | 15 | 14 | 1 |
| M5 | 20 | 15 | 5 |
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
| **total** | **222** | **105** | **117** |

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

Only `M4-012` is missing as listed above. The other fourteen records have a `CozmoMotion`, routed state/cube event, firmware handler or connection-init entry test.

### M5-animation

| records | current test | gap | proper entry |
|---|---|---|---|
| `M5-002`, `M5-015` | direct `ProceduralFace`/renderer calls | **H** | shipped clip/live-layer keyframe → scheduler renderer → face wire frame |
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

- **M1:** `L` — `001 002 003 004 005 006 007 008 009 010 011 012 016 017 018 019 020 021 023 024 026 027 028 030 032 035 041`.
- **M2:** `L` — `001 002 003 004 005 006 007 008 009 010 011 012 013 014 015 016` (for codec/layout records, Pack/Parse is itself the production entry).
- **M3:** `H/S` — `003 007 009 010 011 024 025`; `L` — `002 005 006 012 014 015 018 022 026 028 029 030 031 033 034 035`.
- **M4:** `N` — `012`; `L` — `001 002 003 004 005 007 011 014 015 016 020 022 023 025`.
- **M5:** `H/A/S` — `001 002 015 028 034`; `L` — `004 006 007 008 009 010 012 016 019 023 024 025 026 029 033`.
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

