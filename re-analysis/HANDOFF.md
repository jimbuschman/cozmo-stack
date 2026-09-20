# Handoff — 2026-09-20 (M15 complete offline, correction pass applied)

## Where things stand

| | |
| --- | --- |
| Latest commits | M11: `19d1417`, `39b27d6`; corrections: `981ce8c`; M12: `de08930`, `e35675d`; M13: `bdfc96a`, `f115117`; M14: `6551028`, `456e0ab`; M15: this series |
| M10 | Complete offline; hardware pending (items G–J). See [DERIVED_STATE.md](DERIVED_STATE.md) |
| M11 | Complete offline; hardware pending (items K–M). See [VISION.md](VISION.md) |
| M9/M10 corrections | Done 2026-09-19 (`981ce8c`): Wwise Stop ends the streaming song, songs prewarmed off the scheduler thread, reactions hold against scoring, latched strategies not consumed while unrunnable, one clock for the frustration cooldown, strategies disposed and stale resume state cleared, DizzyShakeLoop gap recorded |
| M12 | Complete offline; hardware pending (items N–R). See [MANIPULATION.md](MANIPULATION.md) |
| M13 | **COMPLETE OFFLINE.** The lattice planner over the shipped motion primitives, the flip action, the charger object with align / mount / drive-off, block configurations (stacks, pyramid bases, pyramids), the whiteboard's beacons, the workouts, and 16 behaviour classes (25 shipped configs). Hardware acceptance pending — items S–X. See [NAVIGATION.md](NAVIGATION.md) |
| M14 | **COMPLETE OFFLINE at the OKAO boundary.** `FaceWorld`, `TrackedFace` geometry, `SmartFaceID`, `PetWorld`, the turn / track / verify face actions and seven behaviour classes (14 configs) on an `IFaceDetector` seam; the stock detector (Omron OKAO, 177 exports) is recorded as unavailable, so the face behaviours are implemented but not counted as implementable. See [FACES.md](FACES.md) |
| M15 | **COMPLETE OFFLINE.** The engine's decision architecture over the systems built so far: `NeedsManager` (the three needs, brackets, decay, action deltas), the shipped activity tree (`activities_config.json` + `activities/**`: 24 freeplay sub-activities with priorities, strategies, scoring / strict-priority choosers, interludes), `FreeplaySystem` (`ActivityFreeplay`'s keep / end / pick and `BehaviorManager::Update`'s reactions-then-activity order), `FreeplayStack` (binds every implemented behaviour by id), six new behaviour classes (16 configs) plus `FindFaces` on the face pipeline, and the `freeplay` command (`--tree`, `--simulate`, live). Hardware acceptance pending — item Z. See [FREEPLAY.md](FREEPLAY.md) |
| Tests | **651 tests** (`dotnet test Cozmo.sln`, about 5 m; 630 after M15, 619 after M14, 606 after M13), all passing offline |
| Hardware | nothing new has been run since the two post-sweep retests; the consolidated plan is [HARDWARE_TEST_PLAN.md](HARDWARE_TEST_PLAN.md) (items A–Z) |
| Correction pass | **Done 2026-09-20** (this series), from an independent review of `8ccd694`: the reaction manager's two-phase contract for target-producing strategies, CubeMoved fed from real observations, the last two M7 reactions moved under the manager, recalibration readiness, a bounded shutdown flush, the Wwise scheduler path, disconnected cubes, the fw2457 observation timestamp, the path start/wait/cancel lifecycle, failed final turns, the native different-pose dock retry, the put-down image wait, the native lift presets (32 / 76 / 92), the animation wheel stop, planner-failure safety, the flip's carry lift, thread-safe block configurations, the freeplay activity-transition and repetition lifecycle, the activity duration and cooldown rules, the stack's own put-down wiring, fail-closed live freeplay, and the manipulation hardware commands. One review finding was disproved by the binary. See [SOURCE_FIDELITY_AUDIT.md](SOURCE_FIDELITY_AUDIT.md) §17 |
| Hardware runner | **`hardware-test <robot-ip> --obb <dir>`** is now the way to run the plan: one interactive command that briefs, runs, collects evidence and asks for a human verdict per check, saving after each so a run survives a disconnect (`--resume`, `--from`, `--only`). See [HARDWARE_TEST_PLAN.md](HARDWARE_TEST_PLAN.md) |
| Next | **The hardware / integration validation phase** (items A–Z, Y blocked) through `hardware-test`, then the architectural work listed under "Next task" |

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
| M15 freeplay, needs, autonomy | **Complete offline; hardware pending (item Z).** `Behavior/Needs.cs` (`NeedsConfig`, `DecayConfig`, `NeedsActionDelta`, `NeedsState`, `NeedsManager`), `Activities.cs` (`Graph2d`, `ScoredBehaviorEntry`, `ScoringChooser`, `StrictPriorityChooser`, `SelectionChooser`, `ActivityStrategy` incl. Needs / SevereNeedTransition / Pyramid / PlayWithHumans / Spark / NeedBasedCooldown, `FreeplayInputs`, `Activity`, `ActivityTreeLoader`), `FreeplaySystem.cs`, `FreeplayStack.cs`, `ExplorerBehaviors.cs` (ExploreLookAroundInPlace, FindFaces, DriveInDesperation, ExpressNeeds, PlayAnimOnNeedsChange, Wait, EarnedSparks; `PanAndTilt`), `PlayAnimBehavior.WantsToRunStrategy` (ObstacleDetected gate), `BehaviorContext.ObstacleDetected`, `VisionSystem.PanTiltOverride`; `freeplay --tree / --simulate / <ip>`. Inventory **123 of 178 implementable + 18 on the face pipeline**. |
| M14 faces around the OKAO boundary | **Complete offline; nothing runnable on hardware without a detector (item Y).** `Vision/Faces.cs` (`IFaceDetector`, `OkaoFaceDetector`, `DetectedFace`, `TrackedFace`, `FaceWorld`, `FaceEntry`, `SmartFaceID`, `IPetDetector`, `PetWorld`), `Vision/FaceActions.cs` (`TurnTowardsPoseAction`, `TurnTowardsFaceAction`, `TrackFaceAction`, `VisuallyVerifyFaceAction`), `VisionSystem.Faces/Pets/FaceDetector/PetDetector`; `Behavior/FaceBehaviors.cs` (`ActionBehavior` base shared with the manipulation behaviours, PlayAnimWithFace, AcknowledgeFace, InteractWithFaces, DriveToFace, SearchForFace, ReactToPet, PyramidThankYou); `ShippedBehaviors.Faces`. Inventory **107 of 178 implementable + 14 implemented on the face pipeline**. |
| M13 navigation, cube games, charger | **Complete offline; hardware pending (items S–X).** `Manipulation/`: `MotionPrimitiveSet`, `LatticeEnvironment`, `LatticePlanner` (+ `DriveToPoseAction.Goals` / `IgnoreObstacleIds`, `ManipulationSystem.Planner` / `LoadPlanner`), `FlipBlockAction`, `DriveAndFlipBlockAction`, `AlignWithObjectAction`, `MountChargerAction`, `DriveOffChargerContactsAction`, `BlockConfigurationManager` (+ `StackOfCubes`, `PyramidBase`, `Pyramid`), `AIWhiteboard` / `AIBeacon`, `WorkoutComponent` / `WorkoutConfig`; `Vision/ChargerGeometry.cs`, rectangular `KnownMarker`s, passive objects in `BlockWorld`; `Behavior/CubeGameBehaviors.cs` (KnockOverCubes, PopAWheelie, RamIntoBlock, CubeLiftWorkout, BuildPyramidBase/BuildPyramid, RespondPossiblyRoll, OnConfigSeen, CantHandleTallStack, CheckForStackAtInterval, ReactToConfiguration, ThinkAboutBeacons, BringCubeToBeacon), `ChargerBehaviors.cs` (DriveOffCharger, ReactToOnCharger, MountCharger), `ReactToFrustrationBehavior.Major`; `ShippedBehaviors.Navigation`; `manip --flip/--knockover/--wheelie/--mount/--driveoff`, `manip --plan --obb`. Inventory **107 of 178**. |
| M12 cube manipulation | **Complete offline; hardware pending (items N–R).** `Cozmo.Robot.Manipulation`: `PreActionPose` / `CubePreActionPoses`, `PathMotionProfile`, `PathSegment`, `PathSender`, `StraightLinePlanner`, `PathFollower`, `DriveToPoseAction`, `DriveToObjectAction`, `DriveStraightAction`, `DockingSystem`, `CarryingComponent`, `LiftPresets`, `DockActionBase` + `PickupObjectAction` / `PlaceRelObjectAction` / `RollObjectAction` / `PopAWheelieAction`, `PlaceObjectOnGroundAction`, `MoveLiftToHeightAction`, `ManipulationSystem`, `DockHelper`; `Behavior/ManipulationBehaviors.cs` (PickUpCube, PutDownBlock, RollBlock, StackBlocks, PickUpAndPutDownCube); `ShippedBehaviors.Manipulation`; `manip` command. Inventory **82 of 178**. |
| M11 vision and world state | Complete offline; hardware pending (items K–M; K is the only positive real-cube detection evidence). `Cozmo.Robot.Vision`: `MarkerLibrary` (Anki's data: extracted from the user's own binary by the build, not committed), `MarkerDecoder`, `QuadDetector`, `MarkerDetector`, `CameraCalibration` + `NvCalibrationReader`, `HeadGeometry`, `CameraModel`, `CubeGeometry`, `PoseEstimation`, `BlockWorld` / `ObservableObject`, `RobotStateHistory`, `VisionSystem`, `CubeLocator`, `TurnTowardsPose`; `Behavior/ObjectBehaviors.cs` (`ObjectPositionUpdatedStrategy`, `AcknowledgeObjectBehavior`); `vision` and `bodyangle` commands; `re-analysis/tools/extract_marker_library.py`. Inventory **69 of 178**. |

## What M15 established

The stack now decides for itself. `FreeplaySystem.Tick` runs the engine's order (`BehaviorManager::Update`
0x005A2F60): reactions first, then the current freeplay sub-activity's chooser, then the behaviour's update.
Activities come from the shipped tree with their priorities (sparks 0, the needs activities 1–3, PutDownDispatch
10 … NothingToDo 17); an activity is kept until its strategy wants to end and its behaviour has finished, a
spark or a requested change kicks it out, or the robot is put down (then `desiredActivityNames` decides
Socialize / PlayAlone / Hiking from the faces and cubes known). The scoring chooser is
`ScoringBSRunnableChooser`'s: flat score and emotion scorers, repetition and running penalties, the running
behaviour's bonus, and the "is interrupting" log line. Needs decay per minute by level band and refill by action
deltas from the shipped configs; the brackets gate the needs activities, `ExpressNeeds` and the get-ins. Six
behaviour classes were read transition by transition (the S1–S7 look-around, the desperation drive's random
points and request, the needs expressions, the sparks reward). `freeplay --simulate --obb <obb>` prints every
decision offline; the whole stack on the fake robot drives off the charger and rolls the cube it sees
(`TheWholeStackDrivesOffTheChargerAndPlaysWithTheCubeItSees`). 630 tests in the full suite at that point.

Corrections found on the way: a shipped PlayAnim can carry a `wantsToRunStrategyConfig` (ReactToObstacle:
ObstacleDetected), which `PlayAnimBehavior` had ignored and which made it always runnable; the shipped strategy
type strings are `PlayWithHumans` and `NeedBasedCooldown` (Singing: cooldown graphs over the Energy level); a
severe-needs activity does not end from inside freeplay when the need refills, because feeding is the app's
high-level Feeding activity (the requested-activity path, byte +0x90 of `ActivityFreeplay`, is what brings a
new pick).

## What M14 established

The engine's face pipeline is Omron OKAO from the image to the identity; this stack reproduces the world
model, geometry, actions and behaviours around it and refuses to fake the detector. `TrackedFace` places a
head at 62 mm × f / intra-eye pixels along the camera ray (0x0087DE24); `FaceWorld` matches by id or by pose
within 220 mm, skips fast turns and faces below the robot, and forgets unnamed faces after 15 s;
`TurnTowardsFaceAction` turns, waits a few frames for a fresh observation, fine-tunes within 45°, fires
"LookAtFaceVerified" and greets by name or with the no-name trigger; `TrackFaceAction` pans to atan2 and
tilts to atan((z − 49) / d). Seven behaviour classes (14 configs) were read transition by transition and
run offline against a fake detector (619 tests in the full suite). On the robot nothing in M14 acts until an `IFaceDetector` is attached;
that is the recorded boundary, and the inventory keeps these 14 out of the implementable total.

## What M13 established

The robot can plan around what it knows, flip, build, and go home. The engine's planner is a lattice over
the shipped `cozmo_mprim.json` (10 mm cells, 16 lattice headings, nine costed primitives) searched by
`xythetaPlanner` with the world's objects imported as expanded convex obstacles; this stack reproduces that
structure (`LatticePlanner`), reconstructs the primitives' segments as line + arc, and keeps the straight-line
planner only as a labelled fallback. `FlipBlockAction` is not a dock: it drives through the cube at 150 mm/s
with the lift at 40 mm and raises it to carry height within 45 mm, then forgets the cube's pose. The charger
is a passive 96 × 80 × 31 object with one 20 × 27 mm marker on its back wall; `MountChargerAction` aligns to
120 mm (CUSTOM = −27), turns to the docked pose, backs 120 mm at 30 mm/s and retries forward. Stacks, pyramid
bases (≤ 60 mm apart, same height) and pyramids (one cube up over the interior midpoint) are recognised from
poses by `BlockConfigurationManager`, which the pyramid, tall-stack and configuration-seen behaviours read.
Beacons and object-failure memory live on `AIWhiteboard`; the workouts on `WorkoutComponent`. Sixteen
behaviour classes (25 configs) were transcribed transition by transition; `NAVIGATION.md` lists each one's
evidence and what is labelled.

One source-backed correction (audit §14): the place actions verify the placement pose is clear
(`VisuallyVerifyNoObjectAtPoseAction`), not that the target is visible; the M12 transcription would have
refused every stack from the 40 mm place pose.

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
the pre-dock pose, R stack (M12)**, S–X navigation / cube games / charger (M13), Y the face pipeline (M14,
blocked without a detector), **Z freeplay on the robot (M15)**. Commit the acceptance JSON files when run.

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
20. **Freeplay ordering details** (M15) — the interleaving of the desired-from-objects activity with the
    priority order, the `needsActionID` hook, the null-pick switch and the `boredomMultiplier` are INFERRED /
    DEFERRED (`FREEPLAY.md` §3); `StrategyObstacleDetected`'s source flag was not traced (a hook stands in).
19. **Face-detector availability** (M14) — the whole face path waits on an `IFaceDetector`; OKAO is not
    reproducible. `FaceWorld`'s below-robot test and the fine-tune frame budget are INFERRED (`FACES.md` §2).
16. **Planner padding and heuristic** (M13) — "robot padding %f, obstacle padding %f" logged, values not read;
    45 / 20 mm and the Euclidean heuristic stand in. Item X compares the real drive with `manip --plan`.
17. **The charger's pre-dock pose** (M13) — a static `Pose2d` plus −15.5 mm; on-axis at the align distance
    stands in. Item V settles whether the align reaches the marker from there.
18. **`FlipBlockAction` member roles, `MountChargerAction` retries, the wheelie retry limit, workout
    selection** (M13) — values NATIVE, roles / limits INFERRED (`NAVIGATION.md` §3).
13. **`DockWithObject`'s fourth float and trailing bytes, `PlaceObjectOnGround`'s field names,
    `DockingErrorSignal`'s trailing bytes** (M12) — packing order read, semantics of those fields not; sent as
    zero. Items N and O settle whether the firmware accepts them.
14. **Lift preset heights** (M12) — the engine fills its map from constants not exported; 32 / 92 / 72 used.
15. **`DockingResult` codes** (M12) — the result byte is passed through, its enum not decoded.

## Next task

**The dedicated hardware / integration validation phase**: items A–Z of `HARDWARE_TEST_PLAN.md` in order, with the
acceptance JSON files committed. Architectural work that remains before or beside it, in the order the
inventory suggests: the memory map and possible-object exploration (`VisitInterestingEdge`,
`LookInPlaceMemoryMap`, `ExploreVisitPossibleMarker`, `ExploreBumpObject`: the engine's `MemoryMap` quad tree
and `INavMap` were not transcribed), motion and laser detection (PounceOnMotion, TrackLaser: OKAO-adjacent
image processing, not started), GuardDog and the app-driven sparks / games / Selection flow (the app's
`RequestGame` and unlock messages), face recognition and enrolment (OKAO, blocked), text-to-speech, and the
persistence the engine keeps across sessions (needs levels, stars, face albums).

**The original M15 brief, kept for the record: the freeplay / explorer / autonomy layer.** The engine's decision architecture is
`BehaviorManager::Update` → the current `IActivity` (`ActivityFreeplay` with prioritised sub-activities from
`activities_config.json`: sparks, needs, PutDownDispatch, Socialize, Singing, PlayWithHumans, BuildPyramid,
PlayAlone, Hiking, NothingToDo) → its `IActivityStrategy` (`WantsToStart` / `WantsToEnd`: cooldowns, durations,
mood scorers, needs brackets) and its chooser (`ScoringBSRunnableChooser`: flatScore × repetition penalty ×
running penalty + the running behaviour's bonus; `StrictPriorityBSRunnableChooser`; interludes) →
`IBehavior::EvaluateScore` → `SwitchToBehaviorBase`. `CalculateDesiredActivityFromObjects` picks
Socialize / PlayAlone / Hiking on put-down from faces and cubes. The needs system (`NeedsManager`,
`NeedsState`: levels, brackets Full / Normal / Warning / Critical from `needs_config.json`, decay from
`needs_decay_config.json`, action deltas from `needs_action_config.json`) gates the needs activities and
`ExpressNeeds` / `PlayAnimOnNeedsChange`. Behaviours to add: `ExploreLookAroundInPlace` (the s1–s7 scan from
its params; unlocks `FindFaces` on the face pipeline), `DriveInDesperation`, `ExpressNeeds`,
`PlayAnimOnNeedsChange`, `Wait`, `EarnedSparks`. Build a `FreeplaySystem` that loads the shipped activity tree,
binds the behaviour ids to the implemented set and drives the `BehaviorManager`, with an offline simulation
that shows the stack choosing and running behaviours on its own.

Start from the engine, not from guesses: disassemble first (`re-analysis/tools/disarm.py`; the session scratch
disassembler was `engdis.py`, described in the memory notes), and read `FACES.md`, `NAVIGATION.md`,
`MANIPULATION.md`, `VISION.md`, `DERIVED_STATE.md` and `SOURCE_FIDELITY_AUDIT.md` §2 before writing code.
