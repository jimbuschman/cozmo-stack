# Handoff — 2026-09-19 (M11 complete offline)

## Where things stand

| | |
| --- | --- |
| Latest commits | M9: `bfe2ed7`…`5fafcce`; M10: `4c0107d`, `0b11190`; M11: this series |
| M10 | Complete offline; hardware pending (items G–J). See [DERIVED_STATE.md](DERIVED_STATE.md) |
| M11 | **COMPLETE OFFLINE.** Marker detection over the engine's own nearest-neighbour library, camera and cube geometry, `BlockWorld` located/visible semantics, the real `ICubeLocator`, the cube-moved reaction live and `ObjectPositionUpdated → AcknowledgeObject`. Hardware acceptance pending — items K–M. See [VISION.md](VISION.md) |
| Tests | **554 tests** (`dotnet test Cozmo.sln`, about 3 m 40 s; 520 after M10), all passing offline |
| Hardware | nothing new has been run since the two post-sweep retests; the consolidated plan is [HARDWARE_TEST_PLAN.md](HARDWARE_TEST_PLAN.md) (items A–M) |
| Next | **M12 may begin now**: cube manipulation — `DriveToObjectAction`, docking, the lift — which the 34 manipulation behaviours need (see "Next task"). It does not depend on any pending hardware result |

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
| M10 derived robot state and reactions | Complete offline; hardware pending (items G–J). `OffTreadsClassifier`, `UnexpectedMovementDetector`, `ShippedReactionStrategies`, eight `ReactToX` classes + `ReactToFrustrationBehavior.Minor`, the cube-moved path behind `ICubeLocator`, `PlayAnimBehavior.LoadShipped`, `offtreads` and `reactions` commands. |
| M11 vision and world state | **Complete offline; hardware pending (items K–M; K is the only positive real-cube detection evidence).** `Cozmo.Robot.Vision`: `MarkerLibrary` (Anki's data: extracted from the user's own binary by the build, not committed), `MarkerDecoder`, `QuadDetector`, `MarkerDetector`, `CameraCalibration` + `NvCalibrationReader`, `HeadGeometry`, `CameraModel`, `CubeGeometry`, `PoseEstimation`, `BlockWorld` / `ObservableObject`, `RobotStateHistory`, `VisionSystem`, `CubeLocator`, `TurnTowardsPose`; `Behavior/ObjectBehaviors.cs` (`ObjectPositionUpdatedStrategy`, `AcknowledgeObjectBehavior`); `vision` and `bodyangle` commands; `re-analysis/tools/extract_marker_library.py`. Inventory **69 of 178**. |

## What M11 established

The cube can be seen. The engine's marker classifier is not an algorithm to approximate but a table to extract:
598 probe images, their labels and the label tables are linked into `libcozmoEngine.so`, and the decoder that
uses them (probe sampling, min–max normalisation, L1 nearest neighbour, the ambiguity test, the 50 threshold) was
read instruction by instruction. Rendered from that same library, every row decodes to itself and a cube placed
in a synthetic frame is localised to under a millimetre at 120–200 mm. The camera sits where `Robot::Robot` puts
it (neck at (−13, 0, 49), camera 17.52 mm out and up, the 4°-down rotation at 0xC4A854), the cube is
`Block::LookupBlockInfo`'s 44 mm with six 25 mm markers at `Block::AddFace`'s poses, and `BlockWorld`'s rules —
located means a pose not Unknown, active objects must be connected, an object that should be visible and is not
is marked unobserved and then forgotten, nothing is marked while moving or turning fast — are the engine's. The
front end (finding the black squares) follows the engine's pipeline and parameters but its pixel algorithms are
ours and say so. Faces and pets are Omron's OKAO library, not Anki's code, and are labelled out of scope rather
than invented.

What is **not** native is listed in `VISION.md` §5–§6 and every item is labelled: the dark-threshold multiplier
and the pixel algorithms of the front end, the miss count (2) and minimum marker size (10 px) for forgetting, the
pose-confirmer counting (first sight is Known), Dirty on `ObjectMoved`, the clustering tolerances, the flat-snap
angle, the absolute-angle reading of `SetBodyAngle` and the turn's speed, the nominal calibration stand-in, the NV
request framing, AcknowledgeObject's failure paths and verification timeout, the occluder list and the stacked-cube
search.

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
C falling → impact (M7), D lift readout (M4), E colour camera (M3), F animation timeline (M5), G off-treads
transitions live, H the M10 reactions under the manager, I StartMotorCalibration honoured, J unexpected
movement while driving (M10), **K camera calibration from NV and cube localisation live, L SetBodyAngle
semantics, M the cube reactions with a real cube (M11)**. Commit the acceptance JSON files when run.

Deferred and unchanged: enhanced backpack-light keyframes, pre-rendered `faceAnimations`, group cooldown
enforcement, lift 0 mm semantics, the seven stereo ADPCM files, the app's soundtrack, Code Lab, the world-model
side of unexpected movement (pose rewind and collision obstacle), BlockWorld's occluder list and
`IsAnythingBehind`, AcknowledgeObject's stacked-cube search, face/pet/motion detection (OKAO).

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
9. **`SetBodyAngle` semantics** (M11) — field order is read; that the angle is absolute is inferred from the
   name and `TurnInPlaceAction::Init`'s use of the current heading. `bodyangle` settles it (item L).
10. **BlockWorld's miss count and pose confirmer** (M11) — `cmp r3, #1` read as two misses; the confirmer's
    counting not transcribed.
11. **The 30.0 beside the position-update thresholds** (M11) — stored in the strategy, use not traced.
12. **The NV read framing** (M11) — `CommandNV` length/second byte and `MORE` chunking unverified; no capture
    holds an NV exchange.

## Next task

**M12: cube manipulation.** The inventory's largest remaining block is the 34 behaviours that pick up, put down,
roll, stack or knock over a cube (`requires cube manipulation`), every one of which now has a localisable target
and none of which has the actions: `DriveToObjectAction` (with the path planner it drives), `IDockAction` and
its subclasses (`PickupObjectAction`, `PlaceObjectOnGroundAction`, `RollObjectAction`), and the lift/head
choreography they run. Start from `DriveToObjectAction::Init` / `InitHelper` and `IDockAction::Init` (both
exported) and from `ObservableObject`'s pre-dock pose helpers; the docking messages on the wire (`DockWithObject`
and the robot's `DockingStatus`) are catalogued in `PROTOCOL_STATUS.md`. `VISION.md` §5 says what the world model
already answers (located pose, up axis, visibility) and §6 how the turn is sent.

Start from the engine, not from guesses: disassemble first (`re-analysis/tools/disarm.py`; the session scratch
disassembler was `engdis.py`, described in the memory notes), and read `VISION.md`, `DERIVED_STATE.md`,
`BEHAVIOR_LAYER.md` and `SOURCE_FIDELITY_AUDIT.md` §2 before writing code.
