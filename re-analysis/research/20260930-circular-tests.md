# Q13 — circular tests, by subsystem

## Coverage audit (2026-10-01)

| subsystem test set | coverage | unchecked basis affecting conclusions |
| --- | --- | --- |
| M1-transport — M1-001..M1-045 except M1-013/033/036/043/044 (40 records; 155 distinct named references) | CHECKED | None. The five excluded records name no test. |
| M2-protocol — M2-001..M2-017 (17 records; 37 distinct references) | CHECKED | None. |
| M3-device — M3-001..M3-037 except M3-008/016 (35 records; 99 distinct references) | CHECKED | None. The two excluded records name no test. |
| M4-control — M4-001..M4-025 except M4-012/013/021/024 (21 records; 54 distinct references) | CHECKED | None. The four excluded records name no test. |
| M5-animation — M5-001..M5-035 (35 records; 121 distinct references) | CHECKED | None. M5-036 names no test. |
| M6-wwise-bank — M6-001..M6-026 except M6-018/020 (24 records; 111 distinct references) | CHECKED | None. The two excluded records name no test. |
| M7-behaviour — M7-001..M7-021 (21 records; 13 class/method references) | CHECKED | None. M7-022 names no test. |
| M8-framework — M8-001..M8-014 except M8-010 (13 records; 8 class/method references) | CHECKED | None. M8-010 names no test. |
| M9-wwise-music — M9-001..M9-028 except M9-023 (27 records; 25 distinct references) | CHECKED | None. M9-023 names no test. |
| M10-derived — M10-001..M10-012 (12 records; 19 distinct references) | CHECKED | None. M10-013 names no test. |
| M11-vision — M11-001..M11-053 except M11-033/034/035/036/038/039 (47 records; 20 class/pattern references) | CHECKED | None. The six excluded records name no test. |
| M12-manipulation — M12-001..M12-039 (39 records; 30 class/pattern references) | CHECKED | None. |
| M13-navigation — M13-001..M13-028 (28 records; 24 class/pattern references) | CHECKED | None. |
| M14-faces — M14-001..M14-012 except M14-006 (11 records; 10 class/method references) | CHECKED | None. M14-006 names no test. |
| M15-freeplay — M15-001..M15-018 (18 records; 9 class/method references) | CHECKED | None. |
| tools — TOOL-001/002/004/005 (4 records; 3 references) | CHECKED | None. TOOL-003 names no test. |

No conclusion below rests on an unchecked manifest-named test. “No circular finding”
means the named reference was inspected and its expected values came from literal
source rows, shipped assets/captures, or an independent native-emulator fixture. It
does not mean the test is complete. Records with an empty `test` field are explicitly
outside this request and are identified above rather than silently counted as checked.

Date: 2026-10-01
Baseline: `origin/main` / `415b9e0`
Scope: the current tests named by `re-analysis/fidelity_manifest.json`. I inspected the
named methods, wildcard families, and every method in a class when a record names only
the class, then cross-checked every suspected oracle against native or shipped-asset
rows. The re-audit also rejected three earlier false positives: M7's independently
transcribed reaction-lock table, M6's source-fixed Hijack 1024-float allocation, and a
nonexistent M13-027 direct-wrapper test.

“Circular” below has the CHECKLIST meaning: the expected value comes from the implementation itself, repeats its arithmetic/rounded literal, asserts its own string or state, or drives an isolated test seam in place of the claimed production entry. A test may remain useful as a regression while being non-probative for source fidelity. Round-trips are called circular only when neither side has an independent source-fixed byte/layout oracle.

## Summary

| subsystem | circular / implementation-shaped assertions found |
|---|---:|
| M1-transport | 0 |
| M2-protocol | 0 |
| M3-device | 0 in manifest-named methods |
| M4-control | 0 remaining after the B-CORE/B-CORE2 fixes |
| M5-animation | 2 test groups |
| M6-wwise-bank | 2 groups: bus-frame policy and gap-visibility throws |
| M7-behaviour | 8 groups, including class-wide framework references |
| M8-framework | 4 groups |
| M9-wwise-music | 0 confirmed in manifest-named methods |
| M10-derived | 1 group |
| M11-vision | 10 groups |
| M12-manipulation | 14 groups |
| M13-navigation | 7 groups |
| M14-faces | 9 groups |
| M15-freeplay | 12 groups |
| tools | 0 |

## M1-transport

No circular assertion found in the 155 manifest test references. The frame/layout expectations are literal bytes or independently transcribed engine tables. The clock tests now use `0.36f`/120.0f source values instead of reading the implementation clock as their expected value. M1-016's sole named test is incomplete for the unsent-seq-0/R43 boundary, but incompleteness is not circularity.

## M2-protocol

No circular assertion found in the 37 named references. Large codec/layout tests use independently transcribed native tag, size and field tables. Pack→parse round-trips are supplemental; the same records also have fixed native byte/layout oracles, so those round-trip assertions are not the sole evidence.

## M3-device

No circular assertion remains in the 99 manifest-named references. In particular, M3-010's named `M3DeviceTests` use native segment-table bytes and hand-derived endpoints. The superficially circular overload comparisons in `DeviceTests.cs:69-77` are **not named by M3-010** and therefore are outside this task's record-test set; they should not be substituted for the actual M3-010 tests.

## M4-control

No current circular assertion remains in the 54 manifest references. The former `M4ControlTests.cs:470-476` problem was replaced with native binary32-bit expectations and NaN/92-degree boundary cases. The former `EngineAppLayerTests.cs:370` expected `Timer.Seconds`; HEAD now asserts source-fixed `0.36f` and 120.0f around `EngineAppLayerTests.cs:373+`, matching `BaseStationTimer::UpdateTime` `0x0084BC38..0x0084BCA8`.

## M5-animation

| test file:line | what is copied / substituted | engine oracle |
|---|---|---|
| `ProceduralFaceRendererTests.cs:56-59,110-111,125,199` (`M5-021` names the whole class) | Expected canvas centre and eye centres are computed from `ProceduralFaceRenderer.CanvasWidth/CanvasHeight/EyeCenter*`, the production constants being exercised. The literal 64-pixel separation in the same test is independent; the listed assertions are not. | `ProceduralFaceDrawer::DrawEye 0x005850E0..0x005859DC`: canvas 128×64, eye centres `(32,32)` and `(96,32)`, nominal half extents 15 and 20. |
| `M5AnimationTests.cs:1341-1347` (`M5_028_K1_K3_TheDartKeyframe`) | Reads generated `x/y`, then recomputes `sY`, `towards`, `away` and eye-centre X with the same `LookAt` arithmetic and widths as production. | Native K1/K3 rows fix the binary32 constants and operation order; expected output should be fixed bit vectors for known RNG draws, not a second copy of the formula. |

The earlier draft incorrectly placed `KeepAliveTests` and `IdleFaceTests` here:
no M5 record names either class. Those findings belong to M7. Other M5 references
use shipped asset counts/bytes, disassembly words, native-emulator fixtures, or fixed
source input/output tables.

## M6-wwise-bank

| test file:line | circular expectation | engine/package answer |
|---|---|---|
| `WwiseBusLifetimeTests.cs:170,266,274` | Treats 1024 as the source frame quantum because the C# runtime constructed a 1024-frame bus. | Same live device-query path; expected frames must come from a captured/query result or an explicitly injected source fixture, not the production default. |
| Named `Assert.Throws<NotSupportedException>` groups in `WwiseBusFxTests.cs:437`, `WwisePlaybackLimiterTests.cs:2751`, and other M6 gap tests | Prove the code exposes an admitted gap, not the native failure behavior. | Native bodies are the record's RECOVERABLE/IMPLEMENTATION_GAP rows. A visible throw is valid gap visibility but cannot settle the record. |

The many M6 tests that compare against fixed disassembly words, shipped bank
properties, emulator output or native bit patterns are independent.
`WwiseRuntimeTests.cs:365-369` is outside the exact M6 test field; M6-019 names only
its two STMG methods.
`WwiseHijackPluginTests.cs:101,107` is not circular because 1024 is independently
fixed by the plugin's native allocation row. A decimal vector which merely repeats the
implementation equation is circular under this request; only independently fixed
native vectors are accepted.

## M7-behaviour

| test file:line | circular / seam substitution | engine oracle |
|---|---|---|
| `KeepAliveTests.cs:44-60,99-124,306-310,345-349` | Asserts the reconstructed 60-ms tick/modulo, an implementation-owned suppression string and constants, and the transient-layer reset. | Live gates are `0x0057D606..0x0057D6B8`; persistent eye shift is `0x0064F3C8..0x0064F472` and is removed only by the straight-shuffle path. |
| `KeepAliveTests.cs:150-158` | Computes the expected gap as 300+600 and widens it by `IdleBehavior.EngineTickMs`, repeating the implementation countdown/tick model. | Native decrements once per live Update at `0x0057D650`, `0x0057D68A`, `0x0057D6BA`. |
| `IdleFaceTests.cs:180-189,285-299` | Computes expected eye parameters through production `IdleBehavior.LookAt` and expects the same wall-clock ramp. | Native `SetFacePosition`/layer timing is `0x00583B20..0x00583BF8`, `0x0058CD9E..0x0058CE22`, `0x0058E70A..0x0058E724`. |
| `MoodTests.cs:143-155` | The interior `0.75` result repeats the linear interpolation arithmetic; the method has no `(0,1e-5]` node-gap case. | `GraphEvaluator2d::EvaluateY 0x00804C0C..0x00804C3C` returns left y when gap `<=1e-5f=0x3727C5AC`. |
| `MoodTests.cs:167` | Directly copies `MoodState.DecayResetThreshold` into a literal equality assertion. | Native threshold and comparison order are at `0x0067968A..0x006796AE`; use its binary32 word and boundary inputs. |
| `BehaviorFrameworkTests.cs:231-291,709-711` | M7-014 names the whole class, so the global `ChooseAndSwitch` ranking/self-reporting tests are in M7 scope too. | The engine has no global ranking of all code-built behaviors; selection belongs to activity choosers/reaction maps. |
| `BehaviorFrameworkTests.cs:1054-1056` | The first expected tag array uses the same `AIWhiteboard.Tag*` constants. The next literal `{68,69,53}` assertion is independent. | Native tags are 0x44, 0x45 and 0x35 at `0x0056A44C`, `0x0056A50C`, `0x0056A5CC`. |
| `BehaviorFrameworkTests.cs:1060-1072` | M7-014 names the whole class, so its beacon test is in scope: it subscribes to `BeaconRenderUpdated` and asserts the event count, not rendered geometry. | Beacon rendering `0x0056AA3C..0x0056ABFF` erases/draws three circles per beacon through VizManager. |

The earlier reaction-lock finding was false and out of scope. `M7BehaviorTests` is
named only for two M7-021 charger-contact methods; its Appendix-F table is independently
transcribed from native bytes. Its action-ended and emotion-wire methods are not named
by any M7 record.

## M8-framework

| test file:line | circular / seam substitution | engine oracle |
|---|---|---|
| `BehaviorFrameworkTests.cs:231-291,709-711` | Exercises `BehaviorManager.ChooseAndSwitch`, then asserts the ranking it just implemented. The native engine has no global ranking of every code-built behavior. | `IBehavior` score default is zero at `0x005BBD1C..0x005BBD28`; selection belongs to activity choosers/reaction map, including `ScoringBSRunnableChooser 0x0060A44A`. |
| `BehaviorFrameworkTests.cs:911-927` | Calls `SelectionChooser.RequestBehavior` directly, standing in for the missing wire handler. | Real subscriptions/tags and handler are `0x0060A87A..0x0060AC44`; payload is selector byte + signed int32 runs. |
| `BehaviorFrameworkTests.cs:1054-1056` | Expected tag list is made from `AIWhiteboard.TagRobotObservedObject/...`—the production constants being tested. | Native literals are 0x44,0x45,0x35 at `0x0056A44C`, `0x0056A50C`, `0x0056A5CC`. Expected array should contain those literals independently. |
| Any M8-014 test which only subscribes to `BeaconRenderUpdated` | Asserts a C# event seam, not the rendered geometry. | `UpdateBeaconRender 0x0056AA3C..0x0056ABFF` erases/draws three circles per beacon with VizManager and exact float constants. |

`TheHeadCalibrationWaitRunsToThirtySeconds` is source-fixed by `0x41F00000` and is
not circular. The Smart-helper tests at `BehaviorFrameworkTests.cs:946+` exercise the
specific native helper contracts cited in their comments; they are not substitutes for
the separate unbuilt `SmartDelegateToHelper` body. `CorrectionsTests` is not named by
an M8 record and is outside this subsystem's scope.

## M9-wwise-music

No confirmed circular assertion in the 25 manifest references. The important oracles come from shipped BNK/HIRC/MIDI assets, fixed IDs/byte layouts, or native-emulator vectors. Render→render determinism assertions (for example `WwiseSongTests.cs:222`) are weak by themselves but are supplemental to bank/note/window assertions and are not the named sole oracle for a recovered numeric rule.

## M10-derived

| test file:line | implementation-shaped assertion | engine oracle |
|---|---|---|
| `DerivedStateTests.cs:396-407` | Asserts only the default/null `BehaviorContext.ObstacleDetected` seam and calls that “nothing raises”; another test assigns the public seam true. | Native AIComponent+4 is zeroed at `0x00569A80`, read at `0x006143CA`, and has no shipped writer. A valid test must scan/own all production writers or prevent mutation, not assert the initial C# delegate. |

The earlier generic threshold finding was not a circular assertion: the named tests use
fixed input/output cases well away from the threshold. They do fail to cover the native
`0x3E32B8C2` boundary, but that is missing coverage. Missing NeedsManager consumer
coverage for M10-011 is likewise absent coverage, not circularity.

## M11-vision

| test file:line | circular / implementation-shaped oracle | engine oracle |
|---|---|---|
| `VisionTests.cs:1395-1396` | Repeats C# rounded decimals `0.0872665` and `0.349066`, with six-place tolerance. | Cluster angle is `0x3DB2B8C3` at `0x00625498..0x006254E0`; flat clamp is `0x3EB2B8C2` at `0x00505F10..0x00505F22`. |
| `M11RVisBuildTests.cs:496-507` | Asserts `InterpolationStandIns` increments. This proves the implementation advertises its missing blend, not native interpolation. | `GetRawStateAt 0x00531431..0x005315A6`, `HistRobotState::Interpolate 0x0053068C..0x00530848`. |
| `M11RVisBuildTests.cs:1333-1337,1440-1445,1586-1596` | Asserts the implementation's `NotSupportedException` gaps (non-cube creation, nonunique origin update, markerless update). | Native bodies/call sites are M11-042/M11-043/M11-037 recovery rows; none establishes a managed exception result. |
| `M11RVisBuildTests.cs:1399-1435` | Calls `UpdateObjectOrigins` directly and, at line 1421, asserts the implementation's `UnbuiltBroadcastLocatedObjectStatesCalls` counter. It bypasses Rejigger/LocalizeToObject and the missing broadcast. | Native `UpdateObjectOrigins 0x00620534..0x006206EA` is reached from localization/Rejigger and ends with `BroadcastLocatedObjectStates`; a helper-only call/counter cannot settle that path. |
| `M11RVisBuildTests.cs:1729-1755` | Directly instantiates/drives the C# mailbox; line 1731 copies `VisionSystem.ProcessorPollInterval`, and the expected log/drop counts come from that local thread. No result-deque producer or `CheckMailbox` lifecycle is entered. | `2 ms = 0x001E8480` ns in processor `0x00651F08`; `SetNextImage 0x00652B04`; result consumer `CheckMailbox 0x006B2AD4`. |
| `M11RVisBuildTests.cs:203-705,1800-2026` | Constructs and drives `PotentialObjectsForLocalizingTo`, history, and robot localization helpers directly. These local results stand in for the full interpolation→Rejigger→origin/map/face production chain. | Native chain begins `0x0050CE00`; history interpolation is `0x0053068C`; origin update `0x00620534`; side effects are M11-050/051/053. |
| `OverheadEdgeTests.cs:178-195` | Inserts the frame into the polygon-backed C# `MemoryMap` and asserts its local polygon regions. The production map stores the result in its 10-mm quad tree, so the test seam cannot detect cell-boundary/override differences. | `AddVisionOverheadEdges 0x0067F814`; QuadTree insert/ray/transform `0x00685008`, `0x00688378`, `0x00689D80`. |
| `VisionTests.cs:858-1327` (shared `WorldRig.Frame`, call at line 827) | World/block/behavior integration tests enter the offline `Vision.ProcessImage` helper and inspect its local world directly. They do not exercise `SetNextImage`, the processor, result deque, or result-handler ordering. | Live ownership is `SetNextImage 0x00652B04` → processor `0x00651F08` → deque → `CheckMailbox 0x006B2AD4`; offline results remain useful algorithm tests but cannot settle that production path. |
| `M11RVisBuildTests.cs:1701-1719` (`M11_049_NoImageIsProcessedUntil...`) | Line 1717 chooses the expected message from `vision.Calibration is null`, the implementation state being asserted, and then accepts whichever branch logs. | Native enabled byte has its only writer in the NV callback at `0x0065AE80`; disabled frames drop in `SetNextImage 0x00652B20..0x00652BEA`. Each callback outcome needs a fixed expected branch. |
| `M11RVisBuildTests.cs:1763-1774` | Asserts the implementation-owned warning text and local `FramesProcessed` counter after direct `ProcessCapture`; it does not enter the live handoff/result owner. | Native no-calibration exit/warning is `0x006B4D66..0x006B5022`; the production result/deque path remains separate. |

The OpenCV/native-emulation tests with fixed output vectors and `EcvcsExtractorTests` tables are independent. The missing SVD comparison for M11-029 is a coverage hole, not circularity.

## M12-manipulation

| test file:line | circular / implementation-derived expected | engine oracle |
|---|---|---|
| `ManipulationTests.cs:82` | Copies implementation decimal `56.5771` into the expected flipping pose. | Native positive corner is binary32 `0x42624EEF` (56.5770835876) at `0x004E5CB2..0x004E5CBC`; implementation decimal narrows to `0x42624EF3`. |
| `ManipulationTests.cs:139-142` | Recomputes expected threshold using the same binary64 `Math.Sin` and double multiplication as C#. | Native uses `vmul/vadd/vsqrt.f32`, `sinf`, then float multiply/add at `0x00550102..0x00550164`. |
| `RestingFlatTests.cs:34-36` | Converts the implementation's rounded `0.174533` back to degrees and accepts two decimals. | Native tolerance is binary32 `0x3E32B8C2` at `0x0063C66C..0x0063C67E`; C# decimal narrows to `0x3E32B8C7`. |
| `M12RVisBuildTests.cs:341-347` (`M12_024_TheDefaultDrivingAnimationHandlerIsAVisibleStub`) | Expects the implementation's three `NotSupportedException` exits. It proves the handler is visibly missing, not what the engine does. | M12-024's native driving-animation callback bodies remain the source oracle; none returns this C# exception. |
| `M12RVisBuildTests.cs:406-416` (`M12_025_TheWireRoutes`) | Expects the C# wire factory's `NotSupportedException` for the unresolved offset-bearing route. | Native `PlaceRelObjectAction` wire construction/offset path is the M12-025 source row; the engine has no managed exception result. |
| `M12RVisBuildTests.cs:499-568,1151-1214` (`M12_026_*`) | Calls the extracted pre-action calculations directly and asserts their local list/flag result. The absent engine callers and pose-tree failure exits are not entered. | Native `GetCurrentPreActionPoses` and its callers in the M12-026 inventory rows; a live caller must own the returned poses and failure results. |
| `M12RVisBuildTests.cs:864-931` (`M12_029_*`) | Installs/calls the removal functors directly. The tests do not enter the setter guard, native empty-list result, or `DriveToHelper` owner. | Native installers/functors at `0x005585F8..0x0055EC2A`; the production caller, rather than the public helper, is the required entry. |
| `M12RVisBuildTests.cs:1258-1267` (`M12_022_ANanDistanceBuildsANanGoalAndIsAVisibleStub`) | Expects the implementation's NaN-path `NotSupportedException`. | The native type-six drive path produces its own action/result behavior; it does not establish this exception. |
| `M12RVisBuildTests.cs:1462-1467` (`M12_036_ATiltedObstacleIsTheLabelledPlanarStandIn`) | Explicitly expects `IsStandIn=true` from the C# planar fallback. | Native collision uses the exact object footprint/min-area rectangle path; M13-023 still leaves the non-planar/Rodrigues portion open. |
| `M12RVisBuildTests.cs:1882-1890` (`M12_037_TheDefaultVerifyNoObjectStubIsCountedAndTraced`) | Expects the implementation-owned stand-in call counter and trace. | Native VerifyNoObject sub-action and its result/order are the M12-037 source path; a counted no-op is only gap visibility. |
| `M12RVisBuildTests.cs:1902-1922,2005-2020` (`M12_030_TheCloneKeepsNothing*`, `M12_030_TheApproachAngleHelperPicksTheAxisArm`) | The first test expects the unresolved approach action to throw; the second expects the X/Y helper gaps to throw. | `ComputePlacementApproachAngle 0x005504A0`, X/Y helpers at `0x00550858` and their rotation getters; native results are not managed exceptions. |
| `M12RVisBuildTests.cs:1966-1993` (`M12_030_IsPlacementGoalFreeIntersectsThePaddedCandidateQuadWithoutAZTest`) | Asserts the implementation-owned `"M13-023 planar stand-in"` trace while using that fallback as the collision oracle. | Native footprint/collision path is `0x004E6490..0x004E67C4`, `0x0087713A..0x008772D2`; the exact non-planar path remains open. |
| `M12RVisBuildTests.cs:2103-2130` (`M12_035_TheInstalledFunctionLeavesTheFlipFlagAtOneAndSelectsClosestOnlyForL2`) | One branch explicitly expects the unrecovered `PoseNearerThan` helper to throw. | The native `DriveToFlipBlockPose` L2 installer/callee defines the selection and failure result; it does not throw this managed exception. |
| `M12RVisBuildTests.cs:2216-2233` (`M12_028_TheLegacyPlaceRelKeepsTheEarlierStandInAndNeverWaitsForAVisibleMarker`) | The expected outcome is the deliberately retained marker-facing/placement-clear stand-in. | Native PlaceRel parameter producers/callers at `0x005C9588`, `0x005DC266`, `0x005DCE44`; the retained legacy behavior is not the engine oracle. |

`M12_036_TheSweepStepCountIsFloorNotCeil` at `M12RVisBuildTests.cs:2203-2213`
is no longer circular: it uses source-fixed independent cases (`19` and `5`) for the
native `floorf((L+55.9)/10)` rule. The earlier draft's generic M12 wire-round-trip row
also had no manifest-named M12 test behind it and has been removed.

## M13-navigation

| test file:line | circular / weak oracle | engine oracle |
|---|---|---|
| `M13RVisBuildTests.cs:112-115` (`M13_007_TheCallerToleranceConstants`) | Compares the two production properties to literals. That catches mutation, but the expected properties are the implementation constants themselves and the test does not enter any of the eleven native callers. | `30.0f = 0x41F00000` at callers `0x005BA136`, `0x0061929E`, `0x00619358`; the other eight callers pass `15.0f = 0x41700000` into `0x0062601C`. |
| `M13RVisBuildTests.cs:281-330` (`M13_010_*`) and `NavigationTests.cs:579-602` | Calls the scorer/workout object directly; the two gap methods expect the implementation's `NotSupportedException`, and the navigation test compares `GetCurrentWorkout()` with entries from that same parsed object. None enters the two shipped behavior callers or the mood history ring. | `ShouldPlayEightiesMusic 0x00573E30`; production callers `0x00592416`, `0x005D8014`; `Emotion::GetHistoryValueTicksAgo 0x004BC804` via the 0x80-entry ring. |
| `M13RVisBuildTests.cs:615-846` (`M13_020_*`, `M13_021_*`) | Directly drives `PanAndTiltAction`, `TurnTowardsPoseCompound`, and `WaitForImagesAction`; lines 624/647 copy `TurnTowardsPose.AccelRadPerSec2`, lines 682-683 copy the action/tag properties, line 819 copies `SeePoseToleranceRad`, and lines 831/833 expect the missing head-angle seam to throw. | Constructor defaults include `0x40A78D36`, `0x41200000`, `0x41700000`, `0x41A00000`, and 5-degree `0x3DB2B8C2`; image action/tag are `0x32/0x43`; see-pose tolerance is `0x3C23D70A`. Bodies are `0x00549C48..0x0054DE38`; the MovementComponent/processed-image production owner remains M13-021. |
| `M13RVisBuildTests.cs:871-1009` (`M13_022_*`) | Direct action-model tests copy `DefaultMaxSpeed`, `DefaultAccel`, `MaxRevolutions`, `MinToleranceRad`, action/tag/track properties (lines 874-893, 911, 965), and repeat the same rescale/half-difference arithmetic (lines 915-920, 992). They never exercise the absent MovementComponent wire/completion union/preset/RNG path; line 973 expects that missing seam to throw. | TurnInPlace: action `0x28`, tracks 4, tag `0xC4`, `0x40A78D36`, `0x41200000`, `0x41C80000`, `0x3D0EFA35`, body `0x005459D4..0x005469BE`. MoveHead: action `0x12`, track 1, clamp `0xBEDF66F3..0x3F46D3F2`, tolerance `0x3D0EFA35`, body `0x00547E40..0x005488B8`. |
| `M13RVisBuildTests.cs:193-265` (`M13_023_*`) and `M12RVisBuildTests.cs:1966-1993` | Expects either the implementation's `NotSupportedException` or its labelled planar fallback/trace for tilted/non-cube footprints. | Exact block min-area-rectangle/bounding-quad path `0x004E6490..0x004E67C4`, `0x0087713A..0x008772D2`; the corner writers, `cv::Rodrigues`, tie order and bionic libm are still RECOVERABLE_GAP and do not establish the fallback. |
| `M12RVisBuildTests.cs:2236-2580` (`M13_028_*`) | Several expectations come from the implementation under test: line 2248 repeats its 3-D norm arithmetic; lines 2372/2406/2488 copy `LiftPresets.CarryMm`; lines 2256-2257/2568 assert the C# “not modelled” trace/counter. The track-lock test at 2392-2418 also feeds `asin(1)` while labelling it 92 mm (the native lift relation makes that 111 mm), so it does not independently prove the claimed completion point. | Native Init/Check/destructor are `0x0055EED6..0x0055EF70`, `0x0055F074..0x0055F208`, `0x0055ED54..0x0055EDA0`; carry preset is 92 mm, tolerance `5.0f = 0x40A00000`, strict distance is `40.0f = 0x42200000`; +0x56 skips lock/unlock at `0x00540434..0x00540592` and `0x005408EC`. A 92-mm lift fixture requires `asin(47/66)`. |
| `NavigationTests.cs:422-445` and `M12RVisBuildTests.cs:2241-2292` | These full-action tests enter the live C# action but rely on a fake rig that immediately supplies motion results and on implementation trace/state. They therefore do not cover the native robot ActionList owner, queued-lift cancel, reaction-lock lifecycle, or the 10-ms poll stand-in. | The production path includes Robot ActionList queue/cancel at `0x0055F160..0x0055F16A`, `0x0055ED6C..0x0055ED7A` and reaction lock calls `0x0055EEC6`, `0x0055ED88`; those remain explicitly unmodelled in M13-028. |

The round-4 verifier's old `flip.LiftRaised ? ... : ...` tautology and the unused
`LiftToleranceMm` self-test are no longer present at HEAD. There is also no direct
M13-027 wrapper test: its four current production callers, not a test constructor,
establish A=1/B=0/C=0. Those three stale findings have therefore been removed.

## M14-faces

| test file:line | circular oracle | engine oracle |
|---|---|---|
| `FaceTests.cs:163-193,573-579` | Geometry expectations repeat the C# formula with `TrackedFace.InterPupilDistanceMm`, `MinCosOrDistance`, and other production properties; line 193 expects the no-roll seam's `NotSupportedException`. The later constant test even asserts the dead-branch 220²/0.5 match constants. | Live geometry is binary32 at `0x0087DC68..0x0087E068`: epsilon `0x3727C5AC`, minimum distance `6.0f=0x40C00000`, inter-pupil `62.0f=0x42780000`. The pose/overlap match branch containing 220² and 0.5 is unreachable because `IsRecognitionSupported` always returns one at `0x0086B244`; no-roll detector population is an M11-016/M14-010 gap. |
| `FaceTests.cs:300-308` (`TheFaceActionConstantsAreTheEnginesOwn`) | Every expected value is compared with a public production constant, including rounded decimal radian values and `UpdateIntervalMs`. This is a constant self-test, not the live tick path. | Both wait counts are 10. TrackFace bits: tolerance `0x3D0EFA35`, max head `0x3F46D3F2`, sound angle `0x3E32B8C2`, tilt `0x3E19999A`, pan `0x3ECCCCCD`, target time `0x3F000000`; CheckIfDone is called on the 60-ms basestation tick at `0x005646BC..0x00565104`. |
| `FaceTests.cs:352-383` (`ThePetStrategyRemembersWhatItReactedToAndWaitsAMinute`) | Line 352 copies `RecentlyReactedSec`; the test drives `BehaviorDidReact` directly at whole seconds 10/71 and never exercises the production behavior callback or the binary32 boundary. | `RecentlyReacted 0x00611DD0..0x00611E14` stores/compares float seconds with `60.0f=0x42700000`; `BehaviorDidReact 0x00611E80` and `UpdateReactedTo 0x00611E1C` own the live update. |
| `FaceTests.cs:323-336`, plus face-turn checks at `278` and `427` | Expected pan/head values use the same binary64 `Math.Atan2` arithmetic as the C# helper and the tests call the helper/action seam directly. | Native subtracts the calibration centre, calls `atan2f`, then adds the historical state's binary32 angles at `0x005187CC..0x0051888E`; fixed expected float words are required. |
| `FaceTests.cs:27-98` (`TheMemoryMapAnswersWhetherTheRobotCanDriveInToAFace` is within M14-010's class scope) | Exercises the polygon-backed C# map, and lines 66-67 copy its public drive constants. That seam stands in for the 10-mm quad-tree answer used by the production behavior. | Ray/mask path `0x005C2420..0x005C2572`, `0x0068176E`, `0x00689D80`; distances are `-15.0f=0xC1700000` or `40.0f=0x42200000`, speed `40.0f`. The exact map result remains M11-045/M11-046. |
| `FaceTests.cs:133-144` | Asserts that the installed `OkaoFaceDetector` reports unavailable/empty and that behaviors therefore do not run. This verifies the C# third-party boundary stub, not the shipped engine's FaceTracker call order/output handling. | Native detect/enumerate/parts/expression/smile/gaze sequence is `0x0086D776..0x0086DB20`; enrollment gates are `0x0086E074..0x0086E1AC`. The OKAO outputs are still unavailable at the seam. |
| `FaceRecognizerTests.cs:23-33` | Compares eleven public recognizer/storage constants directly with literals; a wrong use-site can pass as long as the property retains the expected value. | Native thresholds/capacities are 750, 550, 675, 4, 5, 999999 μs, 1000, 10 and 1000 in the M14-011 C3/C4/C5 rows; storage prefix is `0x0002FACE`, header 8 bytes. Production bodies are `0x008640E4..0x00868C5E`. |
| `FaceTests.cs:690-753` (`M14_012_TheFaceAlbumReadsAlbumThenEnrollmentAndReplaysEraseBeforeNames`) | Drives the persistence component and fake NV replies directly, then asserts local event/order state. It does not enter the connection-time M3-033 queue or EngineToGame broadcasts. | Native reads `0x184000` then `0x183000` at `0x006512EA/0x00651330`, installs under the vision mutex `0x0065A876..0x0065A896`, replays erase before names `0x00651594..0x00651612`, and writes album then enrollment with four-byte padding `0x00657180..0x006573EA`. |
| `FaceTests.cs:646-679` (`M14_008_*`, `M14_009_*`) | Calls FaceWorld lifecycle/enrollment methods directly and asserts their local observable events/tuples. The per-frame recognizer feature carrier and EngineToGame broadcasts named as missing by both records are not entered. | Face-result handoff is `0x006551E8..0x00655274`; observed/deleted broadcasts are `0x004F4B7A/0x004F4B82`, `0x004F3C2C..0x004F3C64`; eligibility/enrollment gates are `0x004F55B8..0x004F55E2`, `0x004F5C9E..0x004F5CB2`. |

## M15-freeplay

| test file:line | circular / implementation-shaped oracle | engine oracle |
|---|---|---|
| `FreeplayTests.cs:54-85,349-405,419-430,571-656,766-789,800-843` | Directly drives `NeedsState`/`NeedsManager`, repeats the decay arithmetic in binary64, and at lines 377/773 copies `cfg.MinimumNeedLevel`/`NeedsManager.WriteThrottleSec`. The Save→Load portion validates the stack's own JSON shape. | Native need comparisons/multipliers are binary32 at `0x0069C12C`, `0x0069C214..0x0069C2A4`, `0x0069C3C0`; damaged parts `0x0069CCAC..0x0069CCD2`. Device persistence uses the app JSON contract at `0x006998C8..0x00699BB2`, not the stack's array-shaped round trip; device throttle is 61 s (`0x03A2C940` μs). |
| `FreeplayTests.cs:865-880` | `Assert.Equal(3.0, ActivityStrategy.ShortRunCooldownSec)` and `OnEnded(10 + ActivityStrategy.TickSec)` use the production constants to create and judge the short run. | Native short-run gate compares duration to **two × last real tick duration** from `GetTimeSinceLastTickInSeconds 0x0084BCBC`; then uses 3.0f at `0x005B5312`. It is not `2×1/30 s`. |
| `FreeplayTests.cs:195-323,850-927` | Chooser/strategy objects are invoked directly; diagnostic assertions copy implementation-owned notes/reasons such as `"not runnable"`, `"not built"`, `"running"`, `"cooldown"`, and `"left"`. The Selection inbound handler is never entered. | Chooser and strategy bodies are `0x00609C88..0x0060B23E`, `0x005B4EC8..0x005B54E4`; the missing production entry is ExecuteBehaviorByExecutableType/ID tags `0x93/0x94`, handler `0x0060A848..0x0060AA58`. Only native strings found in those bodies are valid string oracles. |
| `FreeplayTests.cs:129-184` needs-action helper tests | Prove isolated name→delta mapping but not the native behavior call-site set; missing GatherCubes/stack ordering passes. | Native stack branch precedes PickupCube at `0x005DF4EA..0x005DF5A0`; GatherCubes call `0x005AF2EC..0x005AF2F8`. |
| `FreeplayTests.cs:1180-1209,1293-1375` | Calls `RequestNewActivity` and `SetRequestedSpark` directly and asserts the C# latch/log strings. The record explicitly says those public seams have no inbound production caller. | Engine writers are ActivateSpark at `0x005A3C92..0x005A3C9A` and the requested-activity message into +0x90; selection/reselect is `0x005ADC44..0x005AE46E`. |
| `NavigationTests.cs:554-575` | Identity/Z=0 fixture makes the flattened C# pose equal to source and therefore masks the discarded pitch/roll/parent/Z. | Native copies complete Robot Pose3d at `0x005E5F1A..0x005E5F30`. |
| `ManipulationTests.cs:529-546` (`PutDownBlockBacksUpPlaysThePutDownAndLooksDown`) | Asserts implementation trace strings, only a broad backup range, and rounded head angle `-0.349066f` with `1e-4`; it does not independently prove the two-image wait, optional wrapper, or parallel ordering named by M15-012. | Native head angle is binary32 `0xBEB2B8C2`, tolerance `0x3D0EFA35`; `CreateLookAfterPlaceAction 0x005C8174..0x005C82CC` waits for 2 images and owns the parallel head/drive plus optional wrapper. |
| `FreeplayTests.cs:1530-1648,1731-1847,2034-2089` | Several persistence tests call `StartReadFromRobot`, `FinishReadFromRobot`, `StartWriteToRobot`, or NV callbacks directly and compare tags/sizes through `NeedsManager.NeedsNvKey` and `NeedsStateOnRobot.Size`. The filename test calls the production formatter itself. These test the C# seams, not the mfgId→connection queue owner. | Native owner is mfgId tag `0xED` at `0x0052E2F8..0x0052E3B2` → `0x006943A0..0x0069453E`; key `0x194000`, blob size `0x74`, version 5; filename body `0x00695224..0x006952BA`. The stack deliberately moves ownership to an engine-side buffered read, so direct-manager success cannot establish the whole path. |
| `FreeplayTests.cs:1652-1688` | The fixed-offset byte assertions are independent, but the trailing `Assert.Equal(s.field, back.field)` assertions are a Pack→Unpack round trip and can share a defect. | Native fixed layout/size is independently available at `0x007846B4..0x007848C8`; only the byte-offset assertions and hand-built version blobs are fidelity oracles. |
| Freeplay data-tracker tests `FreeplayTests.cs:439-505` | Drive the same C# double clock at whole seconds and construct tracker directly; expected accumulation follows binary64 model and bypasses live initial pause state. | Native accumulation/resume is integer nanoseconds at `0x0056EC50..0x0056EF14`, with only next-send deadline in float seconds; live component already knows off-treads/charger state. |
| `FreeplayTests.cs:517-560,666-760,1385-1405` | Calls `SetPaused`, disconnect/connect, and the forced bracket check directly, then asserts implementation event seams and its own log text (`NeedsManager.SetPaused.Redundant`). The game-message callers and DAS wire are absent. | Native bodies are `0x00695E04..0x00695F6A`, `0x00695908`, `0x00695958`; production callers include SetGameBeingPaused tag 85, SetNeedsPauseState 201, onboarding 200 and SDK tags 241/242, none of which is entered here. |
| `FreeplayTests.cs:939-1094,1120-1281` | Class-wide M15 references include many assertions against the stack's own behavior IDs, traces, log messages and local fake-rig decisions. These are useful regressions, but the expected strings/state come from the C# behavior graph and do not independently check native callbacks/wire ordering. | Source oracles are the shipped `activities_config.json` for tree data and the individual native behavior bodies; any asserted trace string not present in a cited native body or shipped asset is implementation-owned. |

M15-009's named `ThinkAboutBeaconsThenBringCubeToBeaconPlacesTheCubeInside` test
does **not** assert either emotion-event name; `TheBeaconAndStackConstantsAreTheEnginesOwn`
is not named by an M15 record. M15-010 similarly names the whole `BehaviorTests` class,
but that class has no `AcknowledgeCubeMoved` last-location test. Those are missing
coverage, not circular assertions. M15-017's hand-built versioned blobs and fixed byte
offsets are independent; only its trailing Pack→Unpack comparisons are circular.

## tools

No circular assertion in the three manifest references. Fidelity-manifest/literal-lint tests inspect repository structure and fixed baseline data; they do not claim native behavioral equality.

## Required replacements

1. For constants and arithmetic, store the native integer/float bits and independently compute the expected output with the extracted operation width/order.
2. For wire records, compare to fixed CLAD/native bytes, not Pack→Parse of the same implementation.
3. For production ownership, enter through the real engine-facing C# callback/message/tick path; helper-only and public-setter tests may remain, but must be labelled seam tests and cannot settle the record.
4. For explicit gaps, keep `Throws`/counter tests only as gap visibility. They are evidence that the record is still open, never evidence for `EXACT_SOURCE`.
