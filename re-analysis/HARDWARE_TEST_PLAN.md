# Consolidated hardware test plan — pending checks as of M10 (2026-09-19)

Everything below is offline-verified in the repository and waits only for a robot. Run in the order given
(each item is independent; the order puts the cheapest and most informative first). Every command writes
an acceptance JSON when `--acceptance` is given: **commit those files** (`re-analysis/acceptance/`), which
is the process change the fidelity audit asked for.

Setup for all items: robot on its own access point at 172.31.1.1, the OBB unpacked at `<obb>` (the sound
directory needs only `cozmo_resources/sound/AudioAssets.zip`), commands run from `cozmo-stack/`.

| # | check | milestone | command | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- |
| A | **Cozmo sings** | M9 | `dotnet run --project src/Cozmo.Conformance -- sing 172.31.1.1 --obb <obb> --behavior Singing_AbaDaba --acceptance` | switch posted; get-in, song and get-out animations completed; the song rendered with N notes | he sang a tune for about 12 s between a get-in and a get-out, in his own voice, one note per note, at a sensible level; no bursts of get-in phrases during the song |
| A2 | one song from each tempo group | M9 | as A with `--behavior Singing_Bingo` (100 bpm, cut at 9.8 s by the clip's Stop event) and `--behavior Singing_TwinkleTwinkle` (120 bpm) | as A | as A; the three tempos audibly differ |
| B | **cube telemetry** | M4 | `dotnet run --project src/Cozmo.Conformance -- cubes 172.31.1.1 --acceptance` (tap, roll and lift a cube during the 15 s) | connection state, tap, movement, up-axis and battery reported for the cube | the events match what you did to the cube. Discovery is already hardware-observed (2026-09-19); this completes it |
| C | falling → impact | M7 (D9) | `dotnet run --project src/Cozmo.Conformance -- behavior 172.31.1.1 --obb <obb> --seconds 60`, then drop the robot a few centimetres onto a soft surface | an `Unresolved … waits for the landing` line on the fall and a `ReactToImpact` decision on landing when the impact exceeds 1000 | he reacts on landing, not while falling |
| D | lift readout in radians | M4 (D10) | `dotnet run --project src/Cozmo.Conformance -- sensors 172.31.1.1 --acceptance` with the lift down, then raised by hand | `lift=` prints about −0.198 rad / 32 mm with the lift down and about 0.712 rad / 92 mm raised | the printed millimetres match where the lift is |
| E | colour camera frames | M3 | `dotnet run --project src/Cozmo.Conformance -- camera 172.31.1.1 --color --acceptance` | frames decode | the saved images are colour photographs of the room |
| F | animation timeline under a stall | M5 (D11, D12) | `dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <obb>/assets/cozmo_resources/assets --name anim_bored_01 --wwise <obb>/assets/cozmo_resources/sound` | keyframes fired = keyframes in clip; no stalls reported | the clip plays as before. There is no way to force a stall from the tool; this confirms nothing regressed in the normal case |
| G | **off-treads transitions** | M10 | `dotnet run --project src/Cozmo.Conformance -- offtreads 172.31.1.1 --seconds 90 --acceptance` (pick him up, put him down, lay him on his back, each side, his face; hold him still tilted 20–40°) | classifier enabled after the calibration report; a transition printed for each handling; on-back arrives about 1 s after laying him down, sides and face after 0.25 s, pick-up and put-down at once | every printed transition matches what was done, in that order, and nothing prints while he sits still |
| H | **the derived-state reactions** | M10 | `dotnet run --project src/Cozmo.Conformance -- reactions 172.31.1.1 --obb <obb> --seconds 120 --acceptance` | `REACTION RobotOnBack -> ReactToRobotOnBack` (and OnFace, OnSide, RobotPlacedOnSlope, RobotShaken, ReturnedToTreads) print as he is handled, each with its animation steps | on his back he flips down; on his face he rolls; on a side he asks to be righted and waits; put down on a slope he reacts then checks his pitch; shaken then set down he acts dizzy (soft under 2.5 s, medium under 5 s, hard beyond); nothing fires for a state he is not in |
| I | StartMotorCalibration honoured | M4/M10 | during H, lay him on his back with a finger over cliff sensor 0, or watch `ReactToReturnedToTreads` after setting him down tilted: the trace prints `calibrate head (StartMotorCalibration head=1 lift=0)` | a `MotorCalibration` report with `CalibStarted=true` for the head follows within the 5 s allowance, then one with `CalibStarted=false` | the head visibly recalibrates (nods to its stop) |
| K | **camera calibration and cube localisation** (the only positive-detection evidence for the LOCAL front end: the offline tests render markers from the recovered library and the real captures hold no cube) | M11 | `dotnet run --project src/Cozmo.Conformance -- vision 172.31.1.1 --seconds 90 --acceptance --out frames` (a cube connected — run `cubes` first or leave discovery on — then show him the cube at 10–30 cm, turn it, move it 10 cm, hide it) | `camera calibration from robot: fx=… ` printed (the NV read worked); markers print with the face codes; `new object N Block_LIGHTCUBE1 Known at (x, y, 22)` with a pose in millimetres in his frame; hiding the cube in view prints `miss 1`, `miss 2 -> Unknown`; a cube moved out of view stays located | the codes match the faces shown (front/left/right/top), the printed distance matches a ruler within about 5 %, the yaw matches how the cube is turned. If the NV read fails, rerun with `--nominal` and note the calibration is a stand-in |
| L | `SetBodyAngle` semantics | M11 | `dotnet run --project src/Cozmo.Conformance -- bodyangle 172.31.1.1 --deg 45 --acceptance` | `turned 45.0 deg for a 45.0 deg request: ABSOLUTE body angle confirmed` (or the RELATIVE / did-not-turn verdicts, which mean `TurnTowardsPose` needs its angle or fields changed) | he turns 45° left in place, smoothly, and stops |
| M | the cube reactions with a real cube | M11 | `dotnet run --project src/Cozmo.Conformance -- reactions 172.31.1.1 --obb <obb> --seconds 180 --acceptance` after K has passed, with the cube in view; then slide the cube 10 cm while he looks (AcknowledgeObject), then turn him away and roll the cube (ReactToCubeMoved) | `REACTION ObjectPositionUpdated -> AcknowledgeObject` with `turning towards`, `VisuallyVerifyObjectAction`, the animation; `REACTION CubeMoved -> ReactToCubeMoved` with `CubeMovedSense`, the turn, then `AcknowledgeObject` if he finds it or `CubeMovedUpset` if not | he looks at the cube's new place and nods to it; turned away and hearing the cube move, he turns back to where it was and reacts to finding or not finding it |
| N | **pick up a cube** | M12 | `dotnet run --project src/Cozmo.Conformance -- manip 172.31.1.1 --pickup --acceptance` (a connected cube 15–30 cm ahead, face towards him) | `path N: Completed`, `Docking with marker ... using action PickupLow`, docking error signals sent, `PickAndPlaceResult: succeeded ... status=BlockPickedUp`, `Pickup -> Success ... carrying=7` | he drives to about 75 mm in front of the face, docks smoothly, lifts the cube and holds it. If the robot ignores `DockWithObject` or reports failure at once, the unread fields of that message are the first suspect (MANIPULATION.md §4) |
| O | place a carried cube on the ground | M12 | `dotnet run --project src/Cozmo.Conformance -- manip 172.31.1.1 --putdown --acceptance` right after N (or with the cube set on the lift by hand) | `PickAndPlaceResult ... status=BlockPlaced`, `PlaceObjectOnGround -> Success; carrying=False` | he lowers the lift and backs off the cube |
| P | roll a cube | M12 | `dotnet run --project src/Cozmo.Conformance -- manip 172.31.1.1 --roll --acceptance` (cube on its side or upright) | `Roll -> Success; up axis X -> Y` (any change) | he docks and the cube rolls onto another face |
| Q | drive to the pre-dock pose | M12 | `dotnet run --project src/Cozmo.Conformance -- manip 172.31.1.1 --driveto --acceptance` (also `manip --plan 150 80 90` offline shows the messages) | `path 1: Started`, `Completed`, `DriveToPoseAction.CheckIfDone.Success`, `DriveToObject -> Success` | he turns, drives straight, turns to face the cube and stops about 75 mm from its face; no jerks between segments |
| R | stack two cubes | M12 | `dotnet run --project src/Cozmo.Conformance -- manip 172.31.1.1 --stack --acceptance --obb <obb>` with two connected upright cubes in view | phases `PickingUpBlock` → `StackingBlock` → `PlayingFinalAnim` | he picks one up, carries it to the other and places it on top |
| J | unexpected movement while driving | M10 | `dotnet run --project src/Cozmo.Conformance -- drive 172.31.1.1 ...` in one window is not enough because the detector suspends during direct drive; instead run H and, while a behaviour's animation drives the body, hold him so he cannot turn, or twist him against the turn | `unexpected movement TurnedButStopped from <side>` or `TurnedInOppositeDirection` printed, then `REACTION UnexpectedMovement -> ReactToUnexpectedMovement` | he plays the startled reaction once, on the side the push came from; no report while he drives freely |

Items A and A2 are the M9 acceptance; G–J are the M10 acceptance; K–M are the M11 acceptance (`VISION.md` §9);
N–R are the M12 acceptance (`MANIPULATION.md` §6). Run K before N–R: docking needs a located cube. Items B–F are carried over from the fidelity
sweep and its reconciliation (`SOURCE_FIDELITY_AUDIT.md` §8, §10) and were never gated on M9 or M10.

## What G and H cannot tell you, and what would

The classifier's thresholds are the engine's; what a run settles is that this robot's IMU units and mounting put
its resting states where the engine expects them (gravity read about 10500, not 9800, in the committed captures,
which the 3000-wide side band still accommodates). If a state never appears, or appears for the wrong handling,
the first place to look is `offtreads` output's pitch and filtered-accel columns against `DERIVED_STATE.md` §2.3,
not the constants.

## What K cannot tell you, and what would

The decoder and the tables are the engine's; what K settles is the LOCAL front end against real optics — whether
the black squares are found at Cozmo's exposure and blur. If markers are missed, `vision --images <saved jpeg>`
prints every quad and why it was rejected, and `--out` writes the dark mask; the first knob is
`QuadDetectorParameters.DarkThresholdMultiplier` (0.75, ours), not the decoder. Pose accuracy against a ruler
tests the calibration read from NV and the head-camera geometry together; a constant offset in X with a good yaw
points at the calibration, a distance error growing with head angle at the camera pose.

## What N–R cannot tell you, and what would

The exchange is the engine's, but three messages carry fields whose meaning was not read and are sent as zero
(`MANIPULATION.md` §4). A dock that never starts, or a `PickAndPlaceResult` with `result` ≠ 0 at once, points at
`DockWithObject`'s fourth float or trailing bytes; the `result` byte printed is the firmware's `DockingResult`,
whose enum this build does not decode, so record it. A path the robot drives but ends off-pose points at the
planner's segment endpoints (the layouts are the engine's; the planner is ours).

## What A cannot tell you, and what would

The sampler's rules are Wwise runtime semantics taken from public documentation (`WWISE_MUSIC.md` §3):
whether notes sustain and release the way the stock app made them, whether the level is right, and
whether the get-in branch is silent during a song. A recording of the stock app singing the same song
would settle those; if one can be made (the original app still runs on some phones), put it beside the
`--render` WAV of the same song and compare by ear.

## Results

Fill in as run. Until then every row above is "offline-verified, hardware pending", and no milestone's
freeze depends on it: M9 is complete offline, and the next milestone does not use these results.

| # | date | firmware | automated | human | acceptance file |
| --- | --- | --- | --- | --- | --- |
| A | | | | | |
| A2 | | | | | |
| B | | | | | |
| C | | | | | |
| D | | | | | |
| E | | | | | |
| F | | | | | |
| G | | | | | |
| H | | | | | |
| I | | | | | |
| J | | | | | |
| K | | | | | |
| L | | | | | |
| M | | | | | |
| N | | | | | |
| O | | | | | |
| P | | | | | |
| Q | | | | | |
| R | | | | | |
