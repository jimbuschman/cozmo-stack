# Hardware acceptance execution-path audit

Static audit against `HardwareCatalog` after the bounded acceptance repair. `VALID` means the catalog command
reaches the named production entry point and its judge requires the listed outbound message, inbound telemetry,
or terminal observable. It does not claim that hardware has passed. `Y` remains `BLOCKED_EXTERNAL`; there are no
`BROKEN_TEST` or `BROKEN_PRODUCTION_INTEGRATION` rows.

The cleanup consistency pass also verifies that every runnable command is subject to the runner-wide
nonzero-exit failure invariant before its per-check judge runs. Each `AutoRule` below was compared with its
judge and terminal tool output; manipulation acceptance JSON now receives the same explicit success boolean
that controls the tool exit code and the catalog's terminal success marker.

| ID | Catalog command | Production entry point and implementation exercised | Wire / telemetry / terminal observable | Verdict |
|---|---|---|---|---|
| LINK | `connect --seconds 20` | `SmokeTest.Run` → `CozmoRobot.ConnectAsync` → `ReliableTransport` handshake/state routing | connection response, identity requests/replies, `RobotState` rate, transport backlog, smoke terminal verdict | VALID |
| D | `sensors --guide-lift` | `Control.GuidedLift` → `CozmoMotion.SetLiftHeightAsync` at the native 32/92 mm bounds | two `SetLiftHeight` actions, matching `MotorActionAck`, endpoint `RobotState` lift angle/height | VALID |
| F | `anim --name anim_bored_01` | `Anim.Play` → `CozmoAnimations.Play` → `AnimationScheduler` | start/end animation plus face/audio/head/lift/body keyframes; exact successful terminal verdict requires every keyframe and the expected timeline length | VALID |
| FD | `face --pattern eyes` | `Devices.Face` → `CozmoDisplay.Show/Hold` → face packer | outbound `FaceImage`, clean tool completion, required human screen observation | VALID |
| AUD | `tone --sound beeps` | `Devices.Tone` → `CozmoAudio` and native compander | outbound audio frames paced by inbound `AnimationState.NumAudioFramesPlayed`; tool completion and human sound observation | VALID |
| A | `sing --behavior Singing_AbaDaba` | `SingTool` → `SingingBehavior` → trigger map, Wwise renderer, animation scheduler | switch/event resolution, get-in/song/get-out, rendered notes, audio frames and terminal sing verdict | VALID |
| A2 | `sing --behavior Singing_Bingo` | same singing production path with the shipped Bingo config/tempo/stop event | terminal behavior completion and human tempo/stop observation | VALID |
| A3 | `sing --behavior Singing_Bingo --vibrato` | live Wwise music stream and mid-note parameter path | render/consume/send/play counters, posted vibrato, zero underruns, exact successful SingTool terminal verdict | VALID |
| MOV | `drive --allow-drive` | `Control.Drive` → normal `CozmoMotion` head/lift/wheel/stop APIs | every commanded action result plus the tool's complete terminal success marker | VALID |
| CR1 | `core --case CORE-001` | live `BodyKeyframe` → `CozmoAnimations.StreamLive` → scheduler deadline | outbound wheel drive then scheduler-issued stop; inbound wheel-speed settling | VALID |
| IDL | `behavior --no-react` | `BehaviorTool` → `IdleBehavior` → live animation/motion actions | real idle face/head/lift/body outputs and non-zero completed idle-action count | VALID |
| CR3 | `core --case CORE-003` | duration-derived active clip → `CozmoAnimations.Stop` emission gate | active immediately before stop, no later keyframes, ticker ends, wheel/head/lift telemetry settles | VALID |
| CR2 | `core --case CORE-002` | duration-derived active clip → real transport disconnect → animation ticker fault path | active immediately before link drop, `Faulted` delivered, playback task and ticker terminate; human verifies physical stop | VALID |
| E | `camera --color` | `Devices.Camera` → `CozmoRobot.StartCamera` → chunk reassembly/mini-color JPEG → `CameraFrame.Save` | `EnableColorImages`, `ImageRequest`, inbound `ImageChunk`; raw 160×240 JPEG retained and a COMPATIBILITY_POLICY column-duplicated 320×240 diagnostic image saved | VALID |
| B | `cubes --seconds 15` | connection-time auto block pool → advertisements → pool selection → `CubeConnections` | inbound `ObjectAvailable`, outbound `SetPropSlot`, inbound connected `ObjectConnectionState`, tap/move/up-axis/battery telemetry | VALID |
| K | `vision --seconds 90` | `VisionTool` → NV calibration reader → `VisionSystem` marker detector/world model | NV read messages, camera stream, connected cube marker observation, located `Known` world object | VALID |
| V | `manip --mount` | `ManipTool` → `MountChargerAction` → align/dock/path systems | path/docking messages and results; terminal `IS_ON_CHARGER` telemetry true | VALID |
| W | `manip --driveoff` | `DriveOffChargerBehavior` → `DriveOffChargerContactsAction` | 156 mm path/action result plus terminal on-treads and `IS_ON_CHARGER` false; explicit success marker | VALID |
| G | `offtreads` | `ReactionsTool.OffTreads` → production off-treads classifier | inbound `RobotState` IMU/status stream and multiple classified transitions including in-air/resting | VALID |
| C | `reactions --expect RobotFalling` | `BehaviorManager` shipped falling strategy → `ReactToImpactBehavior` | inbound fall/impact-derived state; expected reaction must fire in its own prompt window | VALID |
| H | `reactions --expect ...` | `BehaviorManager` with shipped derived-state strategies/behaviors | inbound state/IMU events; every named reaction must fire in its own window | VALID |
| I | `calibrate` | `Control.Calibrate` → production motor-calibration request | outbound `StartMotorCalibration`; post-request head calibration start and finish messages | VALID |
| J | `reactions --expect UnexpectedMovement --provoke-movement` | normal wheel motion plus unexpected-movement detector/strategy/behavior | outbound wheel motion and stop, inbound gyro/wheel state, expected reaction in its prompt window | VALID |
| M | `reactions --expect ObjectPositionUpdated,CubeMoved` | real `VisionSystem`/`CubeLocator` → shipped reaction registrations → `BehaviorManager` | camera/world object update and cube motion telemetry; both reactions in their own windows | VALID |
| CR8 | `core --case CORE-008` | `VisionSystem` origin handling → `BlockWorld.OnRobotDelocalized` | cube located before origin change; new `RobotState.PoseOriginId`; that same non-carried cube becomes `Unknown` | VALID |
| CR7 | `core --case CORE-007` | camera → `OverheadEdgesDetector` → `MemoryMap.AddVisionOverheadEdges` | processed frames, edge points and non-empty map regions | VALID |
| L | `bodyangle --deg 45` | `VisionTool.BodyAngle` → production body-angle motion API | outbound absolute body-angle command and inbound pose-angle change | VALID |
| Q | `manip --driveto` | `DriveToObjectAction` → `DriveToPoseAction` → planner → `PathSender` | real path segments/events and terminal drive success | VALID |
| CR4 | `core --case CORE-004` | two `ManipulationSystem.StartPath` calls from current reported poses → owned path abort | path B replaces A; late `Abort(A)` sends no clear; inbound B `Completed` event | VALID |
| X | `manip --driveto` with second physical cube | same Q production path plus `ImportBlockWorldObstacles`/`LatticePlanner` | live world obstacle imported, non-zero-obstacle lattice plan, positive count of actual outbound `AppendPathSegmentArc` messages, terminal drive success | VALID |
| N | `manip --pickup` | `DockHelper` → `PickupObjectAction` | dock/error-signal exchange, `PickAndPlaceResult.BlockPickedUp`, carrying state | VALID |
| O | `manip --putdown` | `PlaceObjectOnGroundAction` | place-on-ground messages/result and carrying state cleared | VALID |
| P | `manip --roll` | `DockHelper` → `RollObjectAction` | explicit terminal action success and an explicitly changed before/after cube up-axis | VALID |
| R | `manip --stack` | `StackBlocksBehavior` → pickup → `PlaceRelObjectAction` → success animation | terminal `StackedSuccessfully`, carrying cleared, top/bottom ids; phase entry alone cannot pass | VALID |
| S | `manip --flip` | `DriveAndFlipBlockAction` → `FlipBlockAction` | drive/lift messages, action terminal success and world pose result | VALID |
| T | `manip --knockover` | shipped `KnockOverCubesBehavior` over detected stack | real path/manipulation action and terminal knock-over success | VALID |
| U | `manip --wheelie` | shipped `PopAWheelieBehavior` | `PoppedWheelie`, at most one retry, and the terminal `EnableStopOnCliff(true)` cleanup | VALID |
| Z | `freeplay --seconds 300` | `FreeplayStack` → activities/choosers/`BehaviorManager`/needs/reactions/vision/manipulation | activities selected, at least two behaviors actually start, real downstream outputs, no fatal activity error, terminal freeplay verdict | VALID |
| Z2 | `freeplay --seconds 900 --require-mood-decay` | same full stack plus `MoodState.Advance` over the long run | behavior starts continue into second half; at least three mood samples and an observed decay; terminal verdict | VALID |
| Y | dormant `reactions` face path | `VisionSystem` is constructed and shipped face/pet reaction registrations are installed; stock `OkaoFaceDetector` remains unavailable | camera/vision wiring is present, but no legitimate detector output can be produced on this build | BLOCKED_EXTERNAL |

## Rechecked shared paths

- M11-020 remains `MarkerDetector → NormalizeIllumination → RefineCorners → RestoreRegion → decode`.
- M13-014 remains `FreeplayStack → bound RamIntoBlock → ShippedBehaviors.Reactions → NoPreDockPosesStrategy → AIWhiteboard target → RamIntoBlock`.
- Q and X still intentionally share `ManipTool --driveto → DriveToObjectAction → DriveToPoseAction → ManipulationSystem.Planner → ImportBlockWorldObstacles → LatticePlanner.PlanTo → PathSender`; X differs only by the second live physical obstacle.

## Source evidence used by this repair

- Cube connection: `Robot::Update` 0x00514190, `BlockFilter::Update` 0x0061A79A,
  `BlockFilter::UpdateDiscovering` 0x0061A7EC, `Robot::ConnectToObjects` 0x00517150,
  `Robot::ConnectToRequestedObjects` 0x00514A70, and app
  `ConnectionFlowController.CubeConnectFlow → BlockPoolTracker.EnableAutoBlockPool(0)`.
- Lift endpoints/conversion: `Robot::SetLiftAngle` 0x00513485, `Robot::GetLiftHeight` 0x00516F64,
  `ConvertLiftHeightToLiftAngleRad` 0x005170B0, and `MoveLiftToHeightAction::Init` 0x0054903C.
- Camera encoded geometry: `EncodedImage::AddChunk` 0x004F1CE0 and `MiniColorToJpeg` table
  0x00C48D84 establish nominal QVGA with a half-width three-component encoded JPEG. Raw encoded and
  presentation representations are kept distinct. Column duplication is this stack's diagnostic
  `COMPATIBILITY_POLICY` (M3-018), not recovered app interpolation; M3-016 remains `HARDWARE_ONLY`.
- Delocalization: `Robot::Delocalize` 0x00510A24/0x00510CF0 establishes transfer of carried objects and
  invalidation of non-carried old-origin objects.
- Stack and charger success are the existing recovered terminal action/behavior states; the repair exposes
  those states to the conformance runner rather than treating phase-name log text as success.
