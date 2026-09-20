# Handoff — 2026-09-20 (M12 complete offline)

## Where things stand

| | |
| --- | --- |
| Latest commits | M11: `19d1417`, `39b27d6`; corrections: `981ce8c`; M12: this series |
| M10 | Complete offline; hardware pending (items G–J). See [DERIVED_STATE.md](DERIVED_STATE.md) |
| M11 | Complete offline; hardware pending (items K–M). See [VISION.md](VISION.md) |
| M9/M10 corrections | Done 2026-09-19 (`981ce8c`): Wwise Stop ends the streaming song, songs prewarmed off the scheduler thread, reactions hold against scoring, latched strategies not consumed while unrunnable, one clock for the frustration cooldown, strategies disposed and stale resume state cleared, DizzyShakeLoop gap recorded |
| M12 | **COMPLETE OFFLINE.** Pre-action poses, path sender + planner, drive-to-pose / drive-to-object, the firmware docking exchange with the per-frame error signal, carrying, the dock actions and five behaviour classes (13 shipped configs). Hardware acceptance pending — items N–R. See [MANIPULATION.md](MANIPULATION.md) |
| Tests | **578 tests** (`dotnet test Cozmo.sln`, about 3 m 40 s; 554 after M11, 564 after the corrections), all passing offline |
| Hardware | nothing new has been run since the two post-sweep retests; the consolidated plan is [HARDWARE_TEST_PLAN.md](HARDWARE_TEST_PLAN.md) (items A–R) |
| Next | **M13 may begin now**: the remaining cube behaviours (pyramids, beacons, workouts), KnockOverCubes' flip action, the lattice planner, charger docking (see "Next task"). It does not depend on any pending hardware result |

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
| M12 cube manipulation | **Complete offline; hardware pending (items N–R).** `Cozmo.Robot.Manipulation`: `PreActionPose` / `CubePreActionPoses`, `PathMotionProfile`, `PathSegment`, `PathSender`, `StraightLinePlanner`, `PathFollower`, `DriveToPoseAction`, `DriveToObjectAction`, `DriveStraightAction`, `DockingSystem`, `CarryingComponent`, `LiftPresets`, `DockActionBase` + `PickupObjectAction` / `PlaceRelObjectAction` / `RollObjectAction` / `PopAWheelieAction`, `PlaceObjectOnGroundAction`, `MoveLiftToHeightAction`, `ManipulationSystem`, `DockHelper`; `Behavior/ManipulationBehaviors.cs` (PickUpCube, PutDownBlock, RollBlock, StackBlocks, PickUpAndPutDownCube); `ShippedBehaviors.Manipulation`; `manip` command. Inventory **82 of 178**. |
| M11 vision and world state | Complete offline; hardware pending (items K–M; K is the only positive real-cube detection evidence). `Cozmo.Robot.Vision`: `MarkerLibrary` (Anki's data: extracted from the user's own binary by the build, not committed), `MarkerDecoder`, `QuadDetector`, `MarkerDetector`, `CameraCalibration` + `NvCalibrationReader`, `HeadGeometry`, `CameraModel`, `CubeGeometry`, `PoseEstimation`, `BlockWorld` / `ObservableObject`, `RobotStateHistory`, `VisionSystem`, `CubeLocator`, `TurnTowardsPose`; `Behavior/ObjectBehaviors.cs` (`ObjectPositionUpdatedStrategy`, `AcknowledgeObjectBehavior`); `vision` and `bodyangle` commands; `re-analysis/tools/extract_marker_library.py`. Inventory **69 of 178**. |

## What M12 established

The robot can act on a cube it sees. The engine's manipulation is a division of labour the binary makes
plain: the engine chooses a pre-action pose (`Block::GeneratePreActionPoses`, 75 mm out from a side face for
docking and rolling), drives there (`DriveToObjectAction` → `DriveToPoseAction` → `PathComponent`, with the
head at −15° for path following and a 7.5° pre-action angle), turns to and visually verifies the cube
(`IDockAction::Init`, within 100 mm and 30°), and then hands the dock to the firmware: `DockWithObject` starts
it and every camera frame turns the marker's pose relative to the robot at that frame's time into a
`DockingErrorSignal` (clamp-flat 40°, yaw + π/2, offsets, skipped when turning faster than 22.9°/s) until the
robot's `PickAndPlaceResult` says picked up or placed, which is what sets the carrying state. The five
behaviour classes were read transition by transition with their animation triggers (0x215, 0x19B, 0x19A,
0x199, 0x1FB, 0x21A) and their helpers' retry structure.

What is **not** native is listed in `MANIPULATION.md` §4: the straight-line planner, timeouts, the unread
fields of three messages, the lift preset heights, the retry limit, target selection, the deferred
verification checks and face turns, and KnockOverCubes' flip action, which was not recovered and is not
claimed.

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
movement while driving (M10), K camera calibration from NV and cube localisation live, L SetBodyAngle
semantics, M the cube reactions with a real cube (M11), **N pick-up, O place on ground, P roll, Q drive to
the pre-dock pose, R stack (M12)**. Commit the acceptance JSON files when run.

Deferred and unchanged: enhanced backpack-light keyframes, pre-rendered `faceAnimations`, group cooldown
enforcement, lift 0 mm semantics, the seven stereo ADPCM files, the app's soundtrack, Code Lab, the world-model
side of unexpected movement (pose rewind and collision obstacle), BlockWorld's occluder list and
`IsAnythingBehind`, AcknowledgeObject's stacked-cube search, face/pet/motion detection (OKAO), the lattice
planner, the pick-up lift-load and accelerometer checks, KnockOverCubes' flip action.

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
13. **`DockWithObject`'s fourth float and trailing bytes, `PlaceObjectOnGround`'s field names,
    `DockingErrorSignal`'s trailing bytes** (M12) — packing order read, semantics of those fields not; sent as
    zero. Items N and O settle whether the firmware accepts them.
14. **Lift preset heights** (M12) — the engine fills its map from constants not exported; 32 / 92 / 72 used.
15. **`DockingResult` codes** (M12) — the result byte is passed through, its enum not decoded.

## Next task

**M13: the remaining cube behaviours, the flip action, the planner, and charger docking.** 19 behaviours
still need pyramids, beacons, workouts or games on top of the M12 actions; KnockOverCubes needs
`FlipBlockAction` (a dock action plus lift choreography not yet recovered); driving among obstacles needs the
engine's `xythetaPlanner` in place of the straight-line stand-in; and the 5 charger behaviours use the same
dock exchange through `MountChargerAction` / `AlignWithObjectAction`. Start from `FlipBlockAction::Init`,
`DriveAndFlipBlockAction`, `MountChargerAction::ConfigureAlignWithChargerAction` and `xythetaPlanner`'s
exports; `MANIPULATION.md` §2 has the dock exchange the charger will reuse.

Start from the engine, not from guesses: disassemble first (`re-analysis/tools/disarm.py`; the session scratch
disassembler was `engdis.py`, described in the memory notes), and read `MANIPULATION.md`, `VISION.md`,
`DERIVED_STATE.md` and `SOURCE_FIDELITY_AUDIT.md` §2 before writing code.
