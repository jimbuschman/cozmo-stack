# Q17 — parallel copies and stand-ins beside production paths

## Coverage audit (2026-10-01)

| subsystem | coverage | unchecked basis affecting conclusions |
| --- | --- | --- |
| M1 transport | CHECKED | None. Both socket/manual/offline construction and engine-facing consumers were traced. |
| M2 protocol | CHECKED | None. The generated/manual codecs and every production serializer/parser entry were searched; no second live codec was found. |
| M3 device | CHECKED | None. Offline engine/NV omissions and nominal calibration are reported. |
| M4 control | CHECKED | None. Test clocks/rigs inject the same motion/sensor/cube bodies; no second control implementation was found. |
| M5 animation | CHECKED | None. `IdleBehavior`, scheduler sink mode, trigger-map/library and cooldown-clock alternatives are reported. |
| M6 Wwise/audio | CHECKED | None. Live renderer fallbacks, loader compatibility, and standalone recovered components are reported. |
| M7 behavior | CHECKED | None. Parallel idle/chooser and cooldown consumers are reported. |
| M8 framework | CHECKED | None. Global chooser and local scope/helper alternatives are reported. |
| M9 music | CHECKED | None. The live legacy modulator path is reported. |
| M10 derived state | CHECKED | None. Runnable behavior test doubles are reported. |
| M11 vision | CHECKED | None. Offline entry, interpolation, pose and map/time alternatives are reported. |
| M12 manipulation | CHECKED | None. Geometry and default dock sub-action stand-ins are reported. |
| M13 navigation | CHECKED | None. Planner fallback, async actions, geometry and dock sub-actions are reported. |
| M14 faces | CHECKED | None. Detector/recognizer fakes are reported. |
| M15 freeplay | CHECKED | None. Direct helper entries, offline scheduling and asset-skipping tests are reported. |

No conclusion rests on unchecked work. I scanned all 198 production source files and
119 test files for alternate constructors, implementations of production interfaces,
offline/direct/helper entry points, fallbacks and explicit stand-in/substitution labels;
then traced each candidate from the four top-level production owners and from every
test/tool caller. `CHECKED` does not mean “no issue”: the findings below are the complete
set of behavior-changing parallel or substitute paths found at this snapshot.

Request: `requests/20261001-codex-queue-2.md`, Q17. Snapshot: `origin/main` / `415b9e0`, pulled before the scan.

I searched production, conformance and test code for duplicate owners, offline/tool paths, labelled stand-ins, fallback selection and direct helper entry points; then traced construction from `CozmoRobot`, `CozmoEngine`, `VisionSystem`, `ManipulationSystem` and `FreeplayStack`. This report lists behavior-changing alternatives, not benign dependency-injection hooks that still execute the same production body.

## Summary

| subsystem | confirmed parallel/stand-in paths | live stack can run it? |
|---|---:|---|
| M1 transport | fake peer plus the wider offline engine seam; transport body mostly shared | no |
| M2 protocol | none | — |
| M3 device | nominal calibration tool path | tools only |
| M4 control | none separate; offline motion rig shares live sender | — |
| M5 animation | `IdleBehavior`; no-budget scheduler sink; cooldown-clock fallback | no / no / yes respectively |
| M6 audio | song-output limiter and bank-loader fallbacks; disconnected exact DSP/frame components | yes / conditional / no respectively |
| M7 behavior | same `IdleBehavior`; generic global chooser | tool-only / yes |
| M8 framework | generic `ChooseAndSwitch`, direct Smart-helper scopes | yes / tests |
| M9 music | old `WwiseModulatorNode.ValueAt` renderer path beside recovered evaluator | yes |
| M10 derived | runnable-behavior test doubles | tests only |
| M11 vision | offline `ProcessImage`, interpolation, pose-estimation, map/geometry substitutes | some yes |
| M12 manipulation | planar footprint/intersection and default dock sub-action substitutes | yes |
| M13 navigation | lattice vs straight-line fallback; async action and dock sub-action models | yes |
| M14 faces | fake detector/recognizer boundary | tests only |
| M15 freeplay | direct manager/tracker helpers and asset-early-return tests | tests only |

## Findings

### P1 — M1: loopback fake robot beside the real transport

- **Parallel path:** `cozmo-stack/src/Cozmo.Conformance/Program.cs:52-91` implements `fakerobot`; `ReliableTransport.ConnectLoopback`/manual-pump seams are at `ReliableTransport.cs:547,731+`.
- **Live path shadowed:** socket → executor/scheduler → `ReliableConnection` used by `RobotLink`/`CozmoRobot`.
- **Who relies on it:** transport loopback and hardening tests, plus the conformance `fakerobot` command.
- **Live use:** no. Production does not set the receive/send hooks or explicit-port loopback. Most transport logic is shared, so this is not a second reliable-protocol implementation; the risk is treating the fake firmware's simplified accept/ack behavior as M1-033 hardware evidence. Comments already disclose that.

### P2 — M3: nominal camera calibration substitutes for the connection NV read

- **Parallel path:** `Vision/CameraCalibration.cs:108-123`; tool opt-ins in `FreeplayTool.cs:120-125`, `ManipTool.cs:54`, `VisionTool.cs:139`.
- **Live path shadowed:** connection NV calibration read → `VisionSystem.UpdateCameraCalibration`.
- **Who relies on it:** many vision/manipulation tests instantiate `CameraCalibration.Nominal()`; conformance tools can force `--nominal`.
- **Live use:** ordinary production does not choose it once the connection read succeeds, but tools do, and tests frequently use it while claiming downstream geometry. Such tests can prove control flow, not robot-calibrated projection values.

### P3 — M5/M7: `IdleBehavior` is a second implementation of the live AnimationStreamer idle path

- **Parallel path:** `Behavior/IdleBehavior.cs:1-400`; constructed by `Cozmo.Conformance/Behavior.cs:54`, `IdleTests`, `KeepAliveTests`, `IdleFaceTests`, `BehaviorHardeningTests`, and `CoreReviewTests`.
- **Live path shadowed:** `AnimationScheduler`/track-layer manager's `UpdateLiveAnimation`, keep-alive, eye shift, blink/dart and persistent-layer behavior (`0x0057D5F8`, `0x0057CF6A`, `0x0058CD80..0x0058E724`).
- **Records/tests relying on it:** the circular-test report identifies M7-005..010 and older M5 keep-alive assertions; `KeepAliveTests` and `IdleFaceTests` call the copy directly.
- **Live use:** no. `FreeplayStack`/`CozmoEngine.AnimationStreamerUpdate` uses `AnimationScheduler`, not `new IdleBehavior`. This is the audit's canonical parallel-copy defect.

### P4 — M6: output limiter/fixed-device fallback beside the recovered live bus

- **Parallel path:** `Animation/Wwise/WwiseSongRenderer.cs:74-91,157,288+` retains a fallback output-stage limiter; `WwiseRuntimeSettings` supplies fixed 48000/1024 when there is no phone device query.
- **Live path shadowed:** shipped Robot bus effect chain and Android output-device initialization.
- **Who relies on it:** `WwiseSongRenderer` is constructed live by `WwiseAudioSource.cs:39-53`; several runtime/bus tests assert the fallback's fixed frame size or output.
- **Live use:** yes when the bank/effect chain is unavailable. It is labelled, but any M6/M9 record or hardware result crossing the final output stage must expose the branch.

### P5 — M6: exact DSP/frame components exist beside, but are not wired into, the live player

- **Parallel path:** `WwiseFrameDriver.cs:88+`, `WwiseRobotAudioPath.cs:207+`, `WwiseHijackPlugin.cs:34+`, `WwiseVorbisNative.cs:142-185`, `WwiseVorbisSource.cs:53+`.
- **Live path shadowed:** `WwisePlayback` → `WwiseAudioSource` → `WwiseSongRenderer` → `AnimationScheduler`.
- **Who relies on it:** M6 emulator/unit tests and pre-extraction tests call these components directly.
- **Live use:** no where the comments say “not wired”. This is not evidence that the production audio path uses the recovered implementation; Q10 already recorded that wiring gap.

### P6 — M8/M7: `BehaviorManager.ChooseAndSwitch` is a generic global chooser the engine does not have

- **Parallel path:** `Behavior/BehaviorManager.cs:436+`; comments in `Behaviors.cs:101,405` and `SteppedBehavior.cs:74` acknowledge it.
- **Live path shadowed:** native activity-specific choosers/reaction dispatch, including `ScoringBSRunnableChooser 0x0060A44A`, activity stack and reaction-trigger map.
- **Who relies on it:** `BehaviorFrameworkTests.cs:231-291,709-711`, `CorrectionsTests.cs:155-173,280-301`, `DerivedStateTests.cs:1071,1117`; several behaviors describe their ordinary-play selection in terms of it.
- **Live use:** yes: until M8-013 is built it is the stack's behavior selection path. Tests that assert its ranking are circular and cannot settle native selection records.

### P7 — M8: direct `BehaviorScope`/Smart-helper emulation beside the missing helper component

- **Parallel path:** scope undo closures in `BehaviorManager` and helper tests in `BehaviorFrameworkTests.cs:946+`.
- **Live path shadowed:** native `BehaviorHelperComponent` stack/callback path `0x0056DAD8..0x0056E15C` and action-list lifecycle.
- **Who relies on it:** M8 Smart* tests and behavior code using `Scope.DisableReactions`, idle pushes, locks.
- **Live use:** yes for the local scope system, while `SmartDelegateToHelper` remains unbuilt/throws. It is an adapter, not source proof of the native helper semantics.

### P8 — M9: old per-node modulator renderer beside the recovered evaluator

- **Parallel path:** `WwiseModulator.cs:98-129` explicitly labels `WwiseModulatorNode.ValueAt` a stand-in (sine LFO, ignores waveform and pulse width); the recovered native-shaped evaluator is in `WwiseModulatorEvaluator.cs:224+`.
- **Live path shadowed:** native Wwise modulator records/evaluation used by singing vibrato/envelopes.
- **Who relies on it:** `WwiseSongRenderer.cs:350+` still resolves a `WwiseModulatorNode`; legacy modulator/song tests exercise `ValueAt`, while newer voice-bus tests exercise the evaluator.
- **Live use:** yes through `WwiseAudioSource` → `WwiseSongRenderer`. This is a true parallel live copy; the recovered evaluator does not prove the renderer uses it.

### P9 — M10: runnable behavior stand-ins in derived-state reaction tests

- **Parallel path:** `tests/M10TestSupport.cs:10-24`, used at `DerivedStateTests.cs:1207+`, `VisionTests.cs:1203,1261` and `CorrectionTests.cs:95-97`.
- **Live path shadowed:** actual reaction behavior `IsRunnable`/running state and animation-library dependencies.
- **Who relies on it:** M10 predicate tests for ObjectPositionUpdated/CubeMoved.
- **Live use:** no. The derived-state producer can still be tested, but the stand-in cannot prove the real bound behavior is runnable or that the end-to-end reaction fires.

### P10 — M11: offline `VisionSystem.ProcessImage` bypasses the production mailbox/gates

- **Parallel path:** public offline overload `VisionSystem.cs:809-824`; comments at `:243` say it bypasses the schedule gate. Tools/tests call it (`VisionTool.cs:59-60`, `ManipRig.cs:203`, `VisionTests`, `M11RVisBuildTests`).
- **Live path shadowed:** camera `FrameForVision` → one-slot mailbox → vision processor thread → result deque → `CheckMailbox`, with per-frame mode/gates.
- **Who relies on it:** many M11 marker/object/face/map tests and downstream M12/M13 fixtures.
- **Live use:** no from `CozmoRobot`; it is tool/test entry only. Direct results do not prove mailbox replacement, scheduling, drop order, or result dispatch.

### P11 — M11: nearest-state interpolation stand-in

- **Parallel path:** `Vision/RobotStateHistory.cs:131-163` chooses before/after by `fraction<0.5` and increments `InterpolationStandIns`.
- **Live path shadowed:** `HistRobotState::Interpolate 0x0053068C..0x00530848` reached by `GetRawStateAt 0x00531431..0x005315A6`.
- **Who relies on it:** production vision pose lookup and `M11RVisBuildTests.cs:505-507`, which explicitly assert the stand-in counter.
- **Live use:** yes whenever an image timestamp lies between states. The tests prove visibility of the gap, not source behavior.

### P12 — M11/M12: custom pose estimator beside OpenCV `solvePnP`

- **Parallel path:** `Vision/PoseEstimation.cs:1+` says it stands in for `cv::solvePnP`; used by `BlockWorld.cs:1141,1163` and `Docking.cs:427`.
- **Live path shadowed:** engine OpenCV solvePnP and its failure/numeric behavior.
- **Who relies on it:** `VisionTests.PoseEstimationRecoversAKnownCubePose`, object localization, docking pose confirmation.
- **Live use:** yes. This is a production replacement, not merely a test helper; records must remain no stronger than the OpenCV-equivalence evidence.

### P13 — M11: other live map/time fallbacks

- **Parallel paths:** `VisionSystem.cs:78` unsourced last-image timestamp, `:576` origin-change Delocalize trigger, `:789` latest-state fallback; `RobotStateHistory.cs:40` extra origin purge; `BlockWorld.cs:642` cross-origin success where engine fails, `:1708` revisit guard.
- **Live path shadowed:** native camera timestamp getter, Delocalize callers, timestamp rejection/history and BlockWorld traversal.
- **Who relies on it:** live vision and map updates plus M11/M12/M13 tests.
- **Live use:** yes. Each is labelled, but they are behavior-changing adapters beside native paths and must not be hidden under neighboring settled records.

### P14 — M12/M13: planar footprint/intersection implementations beside native 3-D/min-area geometry

- **Parallel path:** `Manipulation/BlockConfigurations.cs:210-307`, `PreActionPose.cs:512-629`, `DriveActions.cs:728-745`.
- **Live path shadowed:** native min-area rectangle/footprint and object-intersection paths (`0x004E6490..0x004E67C4`, `0x0087713A..0x008772D2`).
- **Who relies on it:** placement, stack/on-top search, pre-action validity; `M13RVisBuildTests.M13_023_*` and `M12RVisBuildTests.M12_036_*` explicitly accept the stand-in.
- **Live use:** yes for tilted/non-cube pairs. These tests are gap visibility, not fidelity tests.

### P15 — M13: straight-line fallback beside the lattice planner

- **Parallel path:** `Manipulation/RobotPath.cs:171+` explicitly calls `StraightLinePlanner` a local stand-in; selection at `DriveActions.cs:240-261`; `ManipulationSystem.cs:67-78` loads lattice assets but permits `Planner=null`; `ManipTool.cs:259-267` exposes both.
- **Live path shadowed:** native `LatticePlannerImpl`/PathDolerOuter.
- **Who relies on it:** drive actions when motion primitives are unavailable, manipulation tests and conformance tool default/flag behavior.
- **Live use:** yes if asset loading failed or planner is absent. A successful direct lattice test cannot prove the live drive took it; tests must assert `ManipulationSystem.Planner` and the production `DriveToPoseAction` entry.

### P16 — M13: async action models beside native IAction/ActionList state machines

- **Parallel path:** `ChargerActions.cs:181-305,343-352`, `FlipBlockAction.cs:85-200`, and similar direct `RunAsync` classes.
- **Live path shadowed:** engine-tick `IActionRunner`, action queue, compound actions, lock gates and native results.
- **Who relies on it:** Navigation/Manipulation tests instantiate these models directly; behaviors call them live.
- **Live use:** yes. Host timeouts, task completion order and missing compound/lock pieces make this more than an adapter; Q15 identifies the affected exact records.

### P17 — M14: fake detector/recognizer paths

- **Parallel path:** injected `IFaceDetector`/recognizer fakes in `ManipRig.cs:393+` and `FaceRecognizerTests.cs:775+`.
- **Live path shadowed:** packaged OKAO boundary and native carrier/callback path.
- **Who relies on it:** face recognizer/album tests and manipulation rigs.
- **Live use:** no unless explicitly injected. Appropriate for engine-side contract tests, but cannot settle anything inside OKAO or production detector scheduling.

### P18 — M15: direct subsystem helpers substitute for Freeplay production scheduling

- **Parallel path:** tests construct/update NeedsManager, activity strategies, data tracker and behaviors directly; `FreeplayTests` also returns early without OBB in asset-dependent cases.
- **Live path shadowed:** `FreeplayStack` tick/order, chooser/reaction gate, connection edges and behavior callbacks.
- **Who relies on it:** M15 helper tests identified in Q13 (needs-action mapping, short-run cooldown, beacon, data tracker).
- **Live use:** helper objects are live components, but the test entry is parallel; it omits production scheduling and assets. These are not separate production implementations, but they are test-only paths cited as if end-to-end.

### P19 — M1/M3/M4 and all higher layers: the offline robot bypasses production engine gates

- **Parallel path:** `CozmoRobot.CreateOffline` at `CozmoRobot.cs:345-352` constructs an offline peer and calls `CozmoEngine.InitOfflineLink`; that entry adds a robot with `withRic:false`, accepts any pose origin, skips the production engine thread and has no normal connection-owned NV component (`CozmoEngine.cs:825,1047-1052,1500-1508,1765-1792`).
- **Live path shadowed:** socket/RCD/RIC connection setup → the 60 ms engine thread → origin filtering, connection NV queue, readiness and live counters.
- **Who relies on it:** it is the base fixture for transport, device, control, animation, behavior, vision, manipulation, navigation and freeplay tests, plus several conformance tools. The search found calls in more than twenty test classes.
- **Live use:** no. Most downstream production component bodies are shared, so this is a useful integration harness, but it changes behavior-changing gates. A test through `CreateOffline` cannot by itself prove RIC filtering, startup NV/readiness order, real thread timing, origin rejection or robot feedback/budget behavior unless it explicitly reinstates and asserts those edges.

### P20 — M12/M13: the default dock compound runs unread sub-actions as a live stand-in

- **Parallel path:** `StandInDockSubActions` at `Manipulation/DockActions.cs:74-113`; every `DockActionBase` installs it by default at `:170`.
- **Live path shadowed:** `SetupTurnAndVerifyAction`'s native `VisuallyVerifyNoObjectAtPoseAction` and `TurnTowardsObjectAction` bodies in the IAction/ActionList path.
- **Who relies on it:** all live dock actions unless a caller replaces `SubActions`, and the docking/manipulation tests. The visual-clear action is a counted success no-op; the turn uses `TurnTowardsObjectAsync(..., Math.PI)` plus an invented two-second marker wait.
- **Live use:** yes. This is more specific than P14/P16: it is the default production dependency, not only a directly tested action model. A test that reaches a dock compound still traverses this substitute.

### P21 — M5/M7: animation-group cooldown uses the host clock because MoodManager time is never wired

- **Parallel path:** `AnimationLibrary.CooldownTimeSec` is optional at `AnimationLibrary.cs:258-264`; `GetAnimationNameFromGroup` falls back to `Environment.TickCount64 / 1000.0` at `:426`. No production assignment to `CooldownTimeSec` exists at HEAD.
- **Live path shadowed:** the native group chooser reads `MoodManager+0x130`, its last-update seconds.
- **Who relies on it:** live `CozmoAnimations` trigger/group selection and direct group/cooldown tests.
- **Live use:** yes. The fallback usually advances monotonically, but it is a distinct time source and can change cooldown boundary decisions. Tests that inject an explicit `nowSec` or call a group directly do not prove this production edge.

### P22 — M6: the general bank loader keeps arbitrary banks when the six scene banks are incomplete

- **Parallel path:** `WwiseSoundLibrary.Load` calls `ApplySceneBankOrder(sceneOnly:false)`; `WwiseSoundLibrary.cs:211-239` returns without applying the native six-bank list unless all six are present. `LoadAudioScene` forces native selection, but `WwiseAudioSource.Load` uses the general loader at `WwiseAudioSource.cs:292`.
- **Live path shadowed:** the native audio-scene constructor unconditionally builds the recovered six-bank order at `0x00592BB0`.
- **Who relies on it:** synthetic/plain-directory Wwise tests and any production caller pointed at an incomplete directory.
- **Live use:** conditional. The shipped full asset tree takes the native branch; partial deployments and test directories take the compatibility branch. Such tests establish parser/runtime behavior, not native scene membership or duplicate precedence.

### P23 — M5/M3: direct scheduler sinks remove robot budgets and use a different pacing seam

- **Parallel path:** `IAnimationSink`'s default played counters are null, which makes `AnimationScheduler` apply no robot backlog budget and use its local due-frame seam (`AnimationScheduler.cs:19-29,155,381-389,1142+`). Dozens of animation tests construct `new AnimationScheduler(testSink)` directly.
- **Live path shadowed:** `CozmoRobot` constructs `RobotAnimationSink`; live `AnimationState` feedback supplies bytes/frames played, and `CozmoEngine.AnimationStreamerUpdate` runs the scheduler once per engine tick.
- **Who relies on it:** `AnimationTests`, `M5AnimationTests`, `AnimationGapTests`, `AnimationStreamLifecycleTests`, `KeepAliveTests`, parts of `M3DeviceTests` and others.
- **Live use:** no. These tests can establish within-frame order and local state transitions from source literals. They cannot establish backlog gates, number of frames built per real engine update, send failure/retry behavior or the complete wire lifecycle unless they use `RobotAnimationSink` through the offline/live robot route.

## No separate copy found

- **M2:** serializers/parsers share one generated/manual catalog; round-trip tests may be weak but there is no second live codec.
- **M4:** offline rigs inject messages/clocks into the same `CozmoMotion`/sensor/cube code used live.
- Ordinary M1 manual-pump mode similarly changes scheduling but executes the same reliable-transport state machine; only the fake firmware side is separate.

## Highest-risk fixes

1. Remove `IdleBehavior` as fidelity evidence and route its useful tool UI through `AnimationScheduler`.
2. Wire recovered Wwise modulator/DSP/frame components into `WwiseAudioSource` instead of leaving old renderer fallbacks live.
3. Make vision tests enter through mailbox/result dispatch when they support live records.
4. Fail closed when lattice assets are required; never silently treat `StraightLinePlanner` as the engine planner.
5. Keep stand-in acceptance tests, but label them gap-visibility and remove them from settlement evidence.
6. Replace the default dock sub-action stand-in before using any dock-compound test as source fidelity evidence.
7. Separate offline-harness coverage from live RIC/NV/thread/origin/budget coverage in test names and manifest evidence.
