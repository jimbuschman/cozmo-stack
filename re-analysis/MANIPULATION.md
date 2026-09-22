# M12 — cube manipulation: pre-action poses, paths, docking, carrying, and the behaviours on them

**Status: COMPLETE OFFLINE, hardware pending (HARDWARE_TEST_PLAN.md items N–R).** 2026-09-20.

M12 builds the manipulation foundation the 34 cube behaviours were blocked on, on top of M11's located
cubes: where the robot stands before acting on a cube (`PreActionPose`), how it gets there (`PathSender`,
a planner, `DriveToPoseAction`, `DriveToObjectAction`), how the firmware docks (`DockingSystem`:
`DockWithObject`, the per-frame `DockingErrorSignal`, `PickAndPlaceResult`), what it is carrying
(`CarryingComponent`), the dock actions (pick up, place relative, place on ground, roll, pop a wheelie, lift
presets) and five behaviour classes transcribed on them (13 shipped configs). Source of truth:
`libcozmoEngine.so` 3.4.0-1204; labels as in `SOURCE_FIDELITY_AUDIT.md`.

Code: `cozmo-stack/src/Cozmo.Robot/Manipulation/`, `Behavior/ManipulationBehaviors.cs`; tests
`ManipulationTests` (14); tool `manip`.

## 1. What was added, with provenance

| piece | file | provenance |
| --- | --- | --- |
| `PreActionType` (Docking 0, PlaceRelative 1, PlaceOnGround 2, Entry 3, Rolling 4, Flipping 5, None 6) | `PreActionPose.cs` | NATIVE values (`Block::GeneratePreActionPoses` jump table 0x004E5958, `DriveToObjectAction::InitHelper` `cmp #6`); names INFERRED |
| cube pre-action poses: 75 mm docking / rolling, 40 mm place-relative, 49 mm place-on-ground, 135° / 56.58 mm flipping corners; side faces only | `CubePreActionPoses` | NATIVE constants (0x004E5A36 0x4296, 0x004E5ADA 0x4220, 0x004E5B0C 0xC244, 0x004E5C9C 2.35619, 0xC2624EEF); ground-plane reduction LOCAL |
| "close enough" box = distance × sin(angle tolerance) | `DistanceThresholdMm` | NATIVE (`ComputePreActionPoseDistThreshold` 0x00550098: sqrt(x²+y²) · sinf); its floor value not read |
| `PathMotionProfile` defaults | `RobotPath.cs` | UNITY (decompiled class: 100 / 200 / 500 mm/s, 2 / 10 / 10 rad, dock 60 / 200 / 500, reverse 80) |
| path segment wire layouts and `ExecutePath {pathID, manualSpeed}`, `ClearPath {u16 = 0}` | `PathSender` | NATIVE packing (`PathDolerOuter::Dole` 0x00507F40..0x00508056, `PathComponent::ExecutePath` 0x0064A426); names from PyCozmo. `ClearPath`'s one field is always zero: `PathComponent::ClearPath` 0x00649220 is the engine's only builder of the message and writes a literal 0 (0x00649268, 0x0064926A), which the EngineToRobot constructor 0x007A891C and `Pack`'s tag-0x3C case 0x007AB84A copy verbatim. It cannot be a path identifier either - `_currentPathID` (+0x42) is zero-initialised (0x00648B2C) and pre-incremented before it is sent (0x0064A3C2), so no live path is id 0 - and the engine correlates ids against its own `_lastSentPathID` (+0x4A, set at 0x00649234) when it reads `PathFollowingEvent`, not against anything it puts in the clear (VERIFY strings 0x00BFCBAC..0x00BFCCC1). What the firmware does with the two bytes is unread: there is no firmware image, and PyCozmo names the field `unknown` |
| planner: point turn, line, point turn | `StraightLinePlanner` | LOCAL (the engine's lattice planner is DEFERRED); 2° point-turn tolerance NATIVE (`TurnInPlaceAction` 0x3D0EFA35) |
| `DriveToPoseAction`: head to −15°, path, success within 0.174533 rad and the distance threshold | `DriveActions.cs` | NATIVE structure and constants (0x0055A238..0x0055B1E8); planning timeout INFERRED (5 s), traversal timeout LOCAL |
| `DriveToObjectAction`: pre-action poses → closest or "use robot pose" → drive → turn towards the object unless carrying; 7.5° pre-action angle | `DriveActions.cs` | NATIVE (0x00558524..0x00559A00) |
| `DriveStraightAction` as a line path | `DriveActions.cs` | NATIVE role; the engine's action uses the path component likewise |
| `DockAction` enum, `BlockStatus`, `PickAndPlaceResult {timestamp, didSucceed, result, blockStatus}` | `Docking.cs` | UNITY enum; NATIVE layout from `HandlePickAndPlaceResult` 0x00533781 |
| `DockWithObject {speed, accel, decel, ?, dockAction, numRetries, doLiftLoadCheck, ?, ?}` | `DockingSystem.Message` | NATIVE field order (0x0063BD50); the fourth float and last two bytes INFERRED as zero |
| docking error signal: marker pose w.r.t. the robot at the frame time, clamp-flat 40°, x − offX, y + offY, z, yaw + π/2 + offAngle, timestamp; skipped when rotating > 22.9°/s | `DockingSystem.OnFrame` | NATIVE (`UpdateDockingErrorSignal` 0x0063BE80..0x0063C1F0); the two trailing bytes INFERRED zero |
| `CarryingComponent`: set on `BlockPickedUp`, cleared on `BlockPlaced` | `Docking.cs` | NATIVE (`HandlePickAndPlaceResult` → `SetDockObjectAsAttachedToLift` / `SetCarriedObjectAsUnattached`) |
| lift presets LowDock 32, HighDock 76, Carry 92, OutOfFOV | `LiftPresets` | NATIVE: names and order from `GetPresetName` 0x00548CD4 (0 LowDock, 1 HighDock, 2 HeightCarry, 3 OutOfFOV), heights from the preset table at 0x00C54688 (32, 76, 92, −1). **Corrected 2026-09-20**: M12 had 32 / 92 / 72 from the lift range limits and a later open-source constant. OutOfFOV is unresolved (−1 is not a height); the carry height stands in |
| `IDockAction` base: object located, within (100 mm, 30°) of a pre-action pose unless skipped, turn + visual verify, dock, subclass verify | `DockActions.cs` | NATIVE (0x005502D8..0x00552560: 0x42C80000, 0x3F060A92) |
| `PickupObjectAction`: high dock above 33.85 mm; verify carrying and not still seen in the original pose | `DockActions.cs` | NATIVE (0x00553648..0x00554540, 0x42076666); lift-load timeout and accelerometer checks DEFERRED |
| `PlaceRelObjectAction` (PlaceHigh / PlaceLow), `RollObjectAction` (RollLow / DeepRollLow), `PopAWheelieAction` | `DockActions.cs` | NATIVE selection; verification reduced to the dock result (+ up axis in the behaviour) |
| `PlaceObjectOnGroundAction` → `PlaceObjectOnGround` (0x44) | `DockActions.cs` | NATIVE role (0x00554638, `CarryingComponent::PlaceObjectOnGround` 0x00632BAE); field order INFERRED {speed, accel, decel, rel_x, rel_y, rel_angle, useApproachAngle} |
| `DockHelper`: drive, dock without re-checking the pose, retry up to 3 | `ManipulationSystem.cs` | NATIVE structure (`PickupBlockHelper::StartPickupAction` 0x005B7B48, `RespondToPickupResult`); attempt limit INFERRED; search fallback DEFERRED |
| `PickUpCubeBehavior`, `PutDownBlockBehavior`, `RollBlockBehavior`, `StackBlocksBehavior`, `PickUpAndPutDownCubeBehavior` | `Behavior/ManipulationBehaviors.cs` | §3 |
| `manip` tool | `Conformance/ManipTool.cs` | — |

## 2. The docking exchange, as the engine does it

The firmware docks; the engine steers it with vision. `IDockAction::CheckIfDone` calls
`DockingComponent::DockWithObject(objectID, speed, accel, decel, markerCode, …, dockAction, offsets, …)` which
marks the object Dirty (`ObjectPoseConfirmer::MarkObjectDirty`) and sends `DockWithObject`. From then on
`VisionComponent` → `DockingComponent::UpdateDockingErrorSignal(timestamp)` runs for every image: it finds the
dock object's marker with the dock code, takes the marker's pose with respect to the robot's pose at that
timestamp (`Robot::GetComputedStateAt`), `ClampPoseToFlat(0.698132)`, and sends
`DockingErrorSignal {x_dist = x − placementOffsetX, y_dist = y + placementOffsetY, z_dist = z, angle = yaw + π/2 +
placementOffsetAngle, timestamp}` unless `WasBodyRotatingTooFast(t, 22.9183°/s)`. The robot answers with
`DockingStatus` while it works and `PickAndPlaceResult` when done; `HandlePickAndPlaceResult` reads
`didSucceed`, `result` (a `DockingResult`) and `blockStatus` (0 none, 1 picked up, 2 placed), updates the
carrying component and re-enables marker detection. `MovingLiftPostDock` reports the lift moving afterwards.

`DockingSystem` does exactly that, with the marker pose solved from the frame's observed corners (M11's
`PoseEstimation`) rather than the engine's cached marker pose, and with `BlockWorld.ClampPoseToFlat`.

## 3. The behaviours

| class (shipped configs) | transcription | provenance |
| --- | --- | --- |
| **PickUpCube** (`SparksPickupSingleCubeForPyramid`, `SparksPickupSingleCubeToStack`) | initial reaction `SparkPickupInitialCubeReaction` (0x215) → `PickupBlockHelper` → `ReactToBlockPickupSuccess` (0x19B) | NATIVE (0x005C64D4..0x005C6F70); target selection LOCAL (closest located cube); `ignoreCubesInBlockConfigTypes` DEFERRED |
| **PutDownBlock** (`PutDownBlock`, `PutDownBlockNothingToDo`, `PyramidPutDownBlock`, `SparksPutDownBlock`) | back up a random 45–75 mm at 100 mm/s (`RandDblInRange(−45, −75)`), `PutDownBlockPutDown` (0x19A), then head −20° with a 30 mm reverse, wait for images, `PutDownBlockKeepAlive` (0x199) | NATIVE (0x005C7F98..0x005C8330); the closing turn towards a face DEFERRED; release of the carried object on the animation INFERRED |
| **RollBlock** (`RollBlockOnSide`, `RollBlockOnSideLowScore`, `Hiking_RollCube`, `SparksRollBlock`) | `RollBlockHelper` → success when the up axis changes → "RollSucceeded" emotion event, `RollBlockSuccess` (0x1FB) | NATIVE (0x005C8598..0x005C8D00, 14400 mm² moved check); `isBlockRotationImportant` honoured as a target preference |
| **StackBlocks** (`StackBlocks`, `SparksStackBlock`) | not carrying → pick up the closest upright; carrying → `PlaceRelObjectHelper` onto the closest other upright (`GetClosestValidBottom`) → `StackBlocksSuccess` (0x21A); failure → back up, `PlaceObjectOnGroundAction` | NATIVE (0x005C93D0..0x005CA0C0); non-upright bottoms (progression unlock) DEFERRED |
| **PickUpAndPutDownCube** (`SparksPickUpCube`) | PickUpCube then PutDownBlock | INFERRED composition from the name and the two transcribed classes |
| KnockOverCubes (`KnockOverCubes`, `SparksKnockOverCubes`) | **not built**: `DriveAndFlipBlockAction` / `FlipBlockAction`'s dock action and lift choreography were not recovered | blocked, honestly |

The behaviours run on `SteppedBehavior` (M10) with the async actions posted back onto the behaviour's tick,
as the engine's `StartActing(action, callback)` does. `ShippedBehaviors.Manipulation(m)` builds the 13.

## 4. What is not native, and is labelled

| item | class |
| --- | --- |
| the planner (straight line with point turns; no obstacles) | LOCAL (engine's lattice planner DEFERRED) |
| planning and traversal timeouts | INFERRED / LOCAL |
| `DockWithObject`'s fourth float and trailing bytes; `DockingErrorSignal`'s trailing bytes; `PlaceObjectOnGround`'s field names | INFERRED (hardware items N, O) |
| OutOfFOV lift preset height | unresolved (the native table holds −1); the carry height stands in |
| dock helper attempt limit (3) | INFERRED; search-for-block fallback DEFERRED. The retry's **different** pre-dock pose is NATIVE (`IBehavior::UseSecondClosestPreActionPose` 0x005BEE40 re-reads the possible poses and calls `IDockAction::RemoveMatchingPredockPose` 0x00551418, which drops the pose matching within 100 mm per axis and 0.523599 rad, only while more than one remains) |
| target selection (closest located cube; up-axis preference for rolls) | LOCAL for `ObjectInteractionInfoCache` |
| pick-up verification's lift-load timeout and accelerometer checks; place verification beyond the dock result | DEFERRED |
| carried object released on the put-down animation | INFERRED |
| turn-towards-face steps in PutDownBlock and the helpers' pre-dock name animations | DEFERRED (faces are OKAO, out of scope) |
| KnockOverCubes' flip action | not recovered |

## 5. Offline evidence

`ManipulationTests` (14) run every flow against a fake robot side that answers the offline transport the way
the firmware does (path events, pick-and-place results, head commands): pre-action poses (four docking poses
75 mm out facing the sides, closest selection, the sin-threshold); planner segments and their wire messages
(ClearPath, point turn 2 rad/s with shortest-direction, line 100/200/500, ExecutePath id); DriveToPose success
with the −15° head; the `DockWithObject` bytes; docking error signals streamed per frame with the marker 128 mm
ahead and the frame timestamp, and carrying set on `BlockPickedUp`; a failed dock leaves nothing carried;
DriveToObject to the closest pre-dock pose then a low pick-up; refusal away from a pre-action pose; high dock
for a cube on a cube; place on ground needs and releases a carried object; PickUpCube, PutDownBlock and
RollBlock end to end; the 13-behaviour shipped set. `manip --plan` prints a plan's messages offline.

What this does **not** show: that the real firmware accepts these field layouts and docks on these signals.
That is items N–R.

## 6. Hardware pending

| item | check |
| --- | --- |
| N | `manip <ip> --pickup`: drive to the pre-dock pose, dock, `PickAndPlaceResult` succeeded / picked up, cube on the lift |
| O | `manip <ip> --putdown`: `PlaceObjectOnGround` accepted, `BlockPlaced` reported |
| P | `manip <ip> --roll`: the cube's up axis changes |
| Q | `manip <ip> --driveto`: `PathFollowingEvent` completed and the robot at the pre-dock pose (path layouts, `ExecutePath`) |
| R | `manip <ip> --stack` with two cubes |

## 7. Next

> **Done (2026-09-20):** M13 built these — see `NAVIGATION.md`. The lattice planner replaces the straight-line
> stand-in when `ManipulationSystem.Planner` is loaded; `FlipBlockAction` is recovered (0x0055EC80); the
> charger docks through `AlignWithObjectAction` / `MountChargerAction`. One correction to §4 of this document:
> the place actions verify the placement pose is clear rather than seeing the target (audit §14).

**M13: the remaining cube behaviours and the planner.** 19 behaviours need pyramids, beacons, workouts or
games on top of these actions (`BuildPyramid*`, `BringCubeToBeacon`, `CubeLiftWorkout`, `RamIntoBlock`, …),
KnockOverCubes needs the flip action, and driving among obstacles needs the engine's lattice planner
(`xythetaPlanner`) in place of the straight-line stand-in. Charger docking (`MountChargerAction`, 5
behaviours) uses the same dock exchange with `Align` and is the natural next dock action.
