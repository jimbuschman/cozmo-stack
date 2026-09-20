# Handoff — 2026-09-19 (M10 complete offline)

## Where things stand

| | |
| --- | --- |
| Latest commits | M9: `bfe2ed7`, `2e8e243`, `2ca5d45`, `5fafcce`; M10: this series |
| M10 | **COMPLETE OFFLINE.** The engine's derived robot state (off-treads classifier, shake and slope tests, unexpected-movement detector, auto-calibration report) and the nine reactions on it run in the M8 framework; the cube-moved path is transcribed behind a locator seam. Hardware acceptance pending — `HARDWARE_TEST_PLAN.md` items G–J. See [DERIVED_STATE.md](DERIVED_STATE.md) |
| Tests | **520 tests** (`dotnet test Cozmo.sln`, about 3 m 35 s; 480 after M9), all passing offline |
| Hardware | nothing new has been run since the two post-sweep retests; the consolidated plan is [HARDWARE_TEST_PLAN.md](HARDWARE_TEST_PLAN.md) (items A–J) |
| Next | **M11 may begin now**: vision's first slice, marker detection and cube localisation (see "Next task"). It does not depend on any pending hardware result |

## Milestone status

| Milestone | Status |
| --- | --- |
| M1 transport core | Frozen. |
| M2 protocol catalogue | Frozen. `RobotState` helpers read `liftAngle` as radians (D10). |
| M3 device layer | Frozen. Colour camera frames never exercised on hardware (plan item E). |
| M4 control layer | Frozen. Cubes: discovery hardware-observed; telemetry acceptance pending (plan item B). `Motion.RequestMotorCalibration` added in M10 (StartMotorCalibration 0x58; honoured-by-robot pending, item I). |
| M5 animation and expression | Frozen, hardware re-verified 2026-09-19. |
| M6 Wwise audio | Frozen; M9 errata recorded. |
| M7 reactive behaviour and idle | Frozen, hardware re-verified 2026-09-19. **M10 correction:** the pick-up reaction fires on the derived `InAir` state (factory lambda 0x0060DDCE), with the raw flag as a labelled fallback until the classifier runs. Falling → impact never exercised (item C). |
| M8 behaviour inventory and framework | Complete offline. `BehaviorManager` now runs reactions (`AddReaction`, `CheckReactions`, resume-last). `SteppedBehavior` is the transcription base for the engine's action-chain classes. Inventory regenerated: **67 of 178 implementable** (19 M1–M7, 39 Singing, 9 M10) plus `ReactToCubeMoved` implemented and waiting on localisation. **M10 correction:** `PlayAnimWithFace` needs a face (`TurnTowardsFaceAction` first) and is filed under vision. |
| M9 Wwise switch-state audio | Complete offline; hardware pending (items A, A2). |
| M10 derived robot state and reactions | **Complete offline; hardware pending (items G–J).** `OffTreadsClassifier`, `UnexpectedMovementDetector`, `ShippedReactionStrategies`, eight `ReactToX` classes + `ReactToFrustrationBehavior.Minor`, `CubeMotionTracker` / `CubeMovedReactionStrategy` / `AcknowledgeCubeMovedBehavior` behind `ICubeLocator`, `PlayAnimBehavior.LoadShipped`, `offtreads` and `reactions` commands. |

## What M10 established

Everything the robot's own state stream implies about how it is sitting is now derived the way the engine
derives it, from the binary: the 0.9/0.1 and 0.95/0.05 IMU filters, the on-side band (|accelY| within 3000 of
9800), the face limits (110°, −80°), the on-back band (74.5° ± 15° on a physical robot; 96.4° is the
simulator's entry), the level test (45°), the 250 ms debounce with the extra 750 ms on the back, the immediate
pick-up and put-down, and the head-calibration gate. The reaction factory's per-trigger rules were read case by
case, including that RobotPickedUp is the derived InAir state and that MotorCalibration reacts only to a
calibration the robot started itself. Seven behaviour classes were transcribed transition by transition with
their animation triggers, waits and head recalibrations; the shake behaviour's five-state machine and its
13000 / 16000 / 5 s / 2.5 s constants; the unexpected-movement detector's wheel-versus-gyro test with its 46 mm
wheel base, 20 mm/s floor, 10°/s gyro threshold, 0.2 rad/s tolerance and count of 10.

What is **not** native is short and labelled (`DERIVED_STATE.md` §5): the debounce clock source, the pick-up
fallback before calibration, the direct-drive gate on the detector, the 5 s calibration allowance, resume-last
mechanics, the per-play body-track lock, the needs/hiccup/DAS systems, and the cube path's "location no longer
valid" transition.

## Hardware

Consolidated in [HARDWARE_TEST_PLAN.md](HARDWARE_TEST_PLAN.md): A/A2 Cozmo sings (M9), B cube telemetry (M4),
C falling → impact (M7), D lift readout (M4), E colour camera (M3), F animation timeline (M5), **G off-treads
transitions live, H the M10 reactions under the manager, I StartMotorCalibration honoured, J unexpected
movement while driving (M10)**. Commit the acceptance JSON files when run.

Deferred and unchanged: enhanced backpack-light keyframes, pre-rendered `faceAnimations`, group cooldown
enforcement, lift 0 mm semantics, the seven stereo ADPCM files, the app's soundtrack, Code Lab, the world-model
side of unexpected movement (pose rewind and collision obstacle).

## Open unknowns carried forward

1. **Eye-dart lifecycle** (M7) — ramp-then-hold or snap-then-hold; a stock-app face capture settles it.
2. **Scanline parity** (M5) — transmitted; whether the firmware uses it is not established.
3. Renderer corner-radius assignment and fill rule (M5).
4. **Frames per engine update** (M5) — the engine streams to the audio budget; we stream one frame per tick.
5. **Sampler semantics** (M9) — sustain/release, the get-in branch, note tracking.
6. **Modulators** (M9) — LFO and envelope objects (types 21, 22): field semantics unread.
7. **`CalibrateMotorAction` timeout** (M10) — not read; 5 s stands in.
8. **The cube behaviour's unlocated branch** (M10) — what `TransitionToTurningToLastLocationOfBlock` does after
   logging that the location is no longer valid; treated as absence.

## Next task

**M11: vision, first slice — marker detection and cube localisation.** The inventory's largest blockers are
now vision (23 face/pet behaviours) and cubes (55, every one of which needs the cube's located pose; the
cube-moved reaction is built and tested against a fake `ICubeLocator` and only needs a real one). The engine's
`BlockWorld`, `VisionSystem` and marker detector are exported and readable the same way the behaviour classes
were; the camera pipeline (M3) already delivers frames. Start from the engine: `BlockWorld::GetLocatedObjectByIdHelper`,
`ObservableObject::IsVisibleFrom`, `TurnTowardsPoseAction`, and the marker code path from `VisionSystem::Update`.
Read `DERIVED_STATE.md` §6 for what the cube-moved reaction expects of the locator.

Start from the engine, not from guesses: disassemble first (`re-analysis/tools/disarm.py`; the session scratch
disassembler was `engdis.py`, described in the memory notes), and read `BEHAVIOR_LAYER.md`, `DERIVED_STATE.md`
and `SOURCE_FIDELITY_AUDIT.md` §2 before writing code.
