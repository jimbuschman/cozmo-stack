# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **195 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 79 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 18 | The native behaviour is known from primary source; this stack reaches the same observable effect by a different mechanism, and the record names the difference. |
| RECOVERABLE_GAP | 68 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. Blocks source-completeness on a live path. |
| COMPATIBILITY_POLICY | 19 | A deliberate choice of this stack on a path that does not claim to be the engine's: offline tools, the test harness, PC-side plumbing, or a stand-in the operator has to ask for. |
| HARDWARE_ONLY | 3 | No shipped artifact can settle it; only a robot, or a recording of the stock app, can. |
| BLOCKED_EXTERNAL | 8 | The answer lies in third-party code or data that is not in the package (Omron OKAO, the Wwise runtime DSP, the Acapela text-to-speech engine). |

## Source-completeness by subsystem

A subsystem is source-complete when nothing on its normal live execution path is a RECOVERABLE_GAP.

| subsystem | records | live-path gaps | source-complete |
| --- | ---: | ---: | --- |
| M1-transport — UDP transport and reliability | 15 | 2 | no |
| M2-protocol — CLAD messages and protocol helpers | 6 | 2 | no |
| M3-device — Camera, display and audio device layer | 17 | 6 | no |
| M4-control — Motion, sensors, lights and cubes | 9 | 4 | no |
| M5-animation — Animation clips, scheduler and face | 20 | 4 | no |
| M6-wwise-bank — Wwise bank reading and codecs | 7 | 0 | yes |
| M7-behaviour — Idle, mood and reactions | 15 | 4 | no |
| M8-framework — Behaviour framework and scoring | 10 | 5 | no |
| M9-wwise-music — Wwise music, the MIDI sampler and singing | 27 | 0 | yes |
| M10-derived — Derived robot state and reaction strategies | 9 | 4 | no |
| M11-vision — Markers, camera geometry and BlockWorld | 16 | 9 | no |
| M12-manipulation — Docking, carrying and pre-action poses | 12 | 7 | no |
| M13-navigation — Planning, charger and block configurations | 10 | 7 | no |
| M14-faces — Face and pet pipeline | 7 | 5 | no |
| M15-freeplay — Needs, activities and freeplay | 12 | 9 | no |
| tools — Conformance CLI and offline tools | 3 | 0 | yes |

## Every RECOVERABLE_GAP

### M1-transport — UDP transport and reliability

**M1-011 — An echoed ping with isReply 0 is treated as a reply** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs`
* effect: round-trip time is measured, or is never measured at all
* rests on: robot behaviour in the fw2457 capture
* best authority: libcozmoEngine.so ping handling; the capture corroborates
* evidence: re-analysis/captures fw2457 frame logs
* unresolved: whether the engine also accepts isReply 0 as a reply, or ignores it and relies on its own timer; the ping handler was not disassembled

**M1-012 — MaxFramePayloadBytes 1037** (live path)

* where: `cozmo-stack/src/Cozmo.Transport/TransportConstants.cs`
* effect: a larger message is split, or refused, at a different size than the engine would
* rests on: PyCozmo robot-side measurement
* best authority: libcozmoEngine.so builds frames up to 1406 bytes; the robot own limit is the binding one
* evidence: dis_udptransport.txt 1420-byte maximum
* unresolved: the robot firmware real limit; the engine 1406 and PyCozmo 1037 disagree and neither was traced to the firmware

### M2-protocol — CLAD messages and protocol helpers

**M2-004 — Handshake order: GetManufacturingInfo, SyncTime, InitController** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs`
* effect: the robot refuses to come up, or comes up in a different state
* rests on: PyCozmo handshake, confirmed by a successful hardware connect
* best authority: libcozmoEngine.so connect sequence
* evidence: re-analysis/captures 2026-09-18_fw2457_probe.log
* unresolved: the engine own connect sequence was not disassembled end to end; the order works but is not read from the binary

**M2-005 — LightState RGB packed 5-5-5** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Lights.cs`
* effect: cube and backpack lights show the wrong colour
* rests on: PyCozmo packing; lights did light on hardware
* best authority: the CLAD definition of LightState and the engine light code
* unresolved: the bit order within the 16-bit field was never confirmed against the engine or a capture of a known colour

### M3-device — Camera, display and audio device layer

**M3-002 — Image chunk reassembly: the count arrives only on the last chunk** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Camera.cs`
* effect: frames are assembled short, or never completed
* rests on: the fw2457 capture
* best authority: the engine image-chunk handler
* evidence: re-analysis/captures
* unresolved: the engine own reassembly rule was not read; the capture shows what the robot sends, not what the engine accepts

**M3-003 — Trailing 0xFF strip, byte re-stuffing and EOI append** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/MiniJpeg.cs`
* effect: decoded frames are corrupt or refused by a decoder
* rests on: PyCozmo transcription; every captured frame Huffman-decodes
* best authority: the engine JPEG assembly next to the header tables already read
* evidence: re-analysis/captures
* unresolved: the engine assembly code was not disassembled, only its constant tables

**M3-004 — 15 warm-up frames discarded** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Camera.cs`
* effect: the first usable frame arrives later or earlier than the engine would deliver it
* rests on: a choice of this stack; one capture measured 11
* best authority: the engine camera start-up handling
* unresolved: whether the engine discards anything at all, and how many

**M3-005 — Images older than the newest two are dropped** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Camera.cs`
* effect: vision runs on a stale frame, or never sees a frame under load
* rests on: a choice of this stack
* best authority: the engine image queue
* unresolved: the engine queue depth and drop rule

**M3-009 — Blank face encoded as two skip-64 commands** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Display.cs`
* effect: a blank face is sent as something the robot may not accept
* rests on: plausible and decodes to blank; not executed against the engine encoder
* best authority: CompressRLE 0x00581904 on an empty image
* evidence: CompressRLE 0x00581904
* unresolved: the encoder was read but not run on an empty image to confirm the two bytes

**M3-014 — Audio frames are sent reliably** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Audio.cs`
* effect: audio frames are retransmitted under loss, adding latency, or are dropped
* rests on: an assumption that the engine sends everything reliable
* best authority: AnimationStreamer::SendBufferedMessages, which was not read
* unresolved: whether the audio path is reliable or unreliable in the engine

### M4-control — Motion, sensors, lights and cubes

**M4-003 — Head and lift motor command default speed and acceleration** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Motion.cs`
* effect: the head and lift move faster or slower than the app moves them
* rests on: PyCozmo defaults (head 10/10, lift 3/20)
* best authority: MoveHeadToAngleAction and MoveLiftToHeightAction constructors in libcozmoEngine.so
* unresolved: the engine action constructor defaults were never disassembled

**M4-004 — Motion refused until both motors report calibrated** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Motion.cs`
* effect: commands are sent into a robot that is still calibrating and are lost
* rests on: observed robot behaviour on connect
* best authority: the engine calibration gate
* evidence: re-analysis/captures
* unresolved: whether the engine gates motion the same way, or simply queues

**M4-008 — Cliff sensors named by index; IMU and cube battery left in raw units** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Sensors.cs`
* effect: a caller reading a named sensor reads the wrong one
* rests on: uncertainty deliberately preserved rather than guessed
* best authority: the engine cliff sensor indexing and IMU scaling
* unresolved: which physical sensor each index is, and the IMU scale factors

**M4-009 — Cube tracking from ObjectAvailable and ObjectConnectionState** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Cubes.cs`
* effect: cubes appear or disappear from the world model at the wrong moment
* rests on: observed discovery of a real cube on hardware
* best authority: the engine cube connection handling
* evidence: operator report 2026-09-19
* unresolved: connection state, tap, movement, up-axis and battery telemetry have not been exercised or read from the engine

### M5-animation — Animation clips, scheduler and face

**M5-012 — Keyframe volume applied as a linear PCM gain** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: animation audio plays at the wrong level
* rests on: a straight linear reading of the keyframe field
* best authority: the engine passes the keyframe volume to Wwise, which maps it through an RTPC curve
* evidence: RobotAudioKeyFrame::SetMembersFromFlatBuf 0x004F9E54
* unresolved: the RTPC or parameter the engine posts the keyframe volume to, and the curve on it; RobotAudioClient::SetCozmoEventParameter was not traced

**M5-013 — faceAnimations track is not loaded; a lift value of 0 mm is passed through** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationClip.cs`
* effect: clips that carry a face animation play without it
* rests on: deferred
* best authority: the engine face-animation track loader
* unresolved: what the faceAnimations track carries and how the engine plays it

**M5-014 — Animation cooldown and head-angle gate are not enforced** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationLibrary.cs`
* effect: a clip the engine would refuse still plays
* rests on: deferred; the mechanism is recorded in AnimationLibrary.cs
* best authority: the engine cooldown and head-angle checks in the animation group loader
* unresolved: the cooldown clock and the head-angle window are read from the asset but not applied

**M5-015 — Procedural-face corner-radius parameter assignment and polygon fill rule** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`
* effect: eye corners are rounded on the wrong corners, or filled differently
* rests on: a reading of DrawEye that stopped short
* best authority: ProceduralFaceDrawer::DrawEye in libcozmoEngine.so
* evidence: re-analysis/PROCEDURAL_FACE.md
* unresolved: which radius parameter belongs to which corner, and the fill rule for the eye polygon

### M7-behaviour — Idle, mood and reactions

**M7-007 — Eye-dart lifecycle: ramp-then-hold or snap-then-hold** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: the eyes jump to the dart or slide to it, and may or may not hold there
* rests on: snap to the shifted pose and hold for the drawn duration
* best authority: GenerateEyeShift 0x0058D100 with AddToPersistentLayer 0x0058EAA0 and ITrackLayerManager::ApplyLayersToFrame 0x0058E644, which were read and contradict each other when read statically
* evidence: AddToPersistentLayer 0x0058EAA0; ApplyLayersToFrame 0x0058E644
* unresolved: the two readings were not separated; a deeper trace of ApplyLayersToFrame trimming a finished persistent layer would settle it

**M7-008 — Idle timers are counted in wall-clock milliseconds** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: blinks and darts come at a different rate than the app
* rests on: wall-clock ms
* best authority: the engine decrements each keep-alive countdown by 60 per Update call (UpdateLiveAnimation 0x0057D5F8, KeepFaceAlive 0x0058D374), so the unit is engine ticks
* evidence: UpdateLiveAnimation 0x0057D5F8; KeepFaceAlive 0x0058D374
* unresolved: the engine tick period, readable from CozmoEngine::Update 0x004ED4D4

**M7-010 — Idle body shuffle is recorded but not driven** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IdleBehavior.cs`
* effect: the robot sits still where the app would shuffle
* rests on: a deliberate omission; the engine numbers are recorded in the file
* best authority: UpdateLiveAnimation 0x0057D6CC onward: speed uniform in plus or minus 10 mm per s, duration 250..1500 ms, straight with probability BodyMovementStraightFraction
* evidence: UpdateLiveAnimation 0x0057D6CC
* unresolved: nothing in the source; it is read and simply not implemented

**M7-013 — Mood clamp to plus or minus 1 and flat extrapolation outside decay-graph nodes** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Mood.cs`
* effect: emotion values saturate differently and decay differently at the edges
* rests on: a plausible reading
* best authority: MoodManager::UpdateEmotions in libcozmoEngine.so
* unresolved: the clamp and the edge behaviour were never disassembled

### M8-framework — Behaviour framework and scoring

**M8-003 — Scored selection: score times repetition penalty, highest wins** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs`
* effect: a different behaviour is chosen at every decision point
* rests on: structure inferred from exported names
* best authority: IBehavior::ReadFromScoredJson 0x005BC489 and ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060B23E, of which the chooser has since been read
* evidence: ScoringBSRunnableChooser::GetDesiredActiveBehavior
* unresolved: ReadFromScoredJson has not been disassembled, so how a shipped config turns into a score is still not read

**M8-004 — Behaviour scores 1.0 and 5.0 where the shipped configs carry none** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: the scoring chooser ranks behaviours by numbers that are not the app numbers
* rests on: chosen because they looked right
* best authority: IBehavior::ReadFromScoredJson 0x005BC489 and the shipped behaviour configs
* unresolved: where the engine gets a score for a behaviour whose config carries none

**M8-005 — PlayAnim uses the first trigger that resolves** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: a different clip plays when several triggers are listed
* rests on: inferred
* best authority: the engine PlayAnim behaviour class
* unresolved: the engine selection rule among several triggers

**M8-006 — A strategy type this stack does not model reports not runnable** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs:126`
* effect: a behaviour whose config names an unmodelled strategy never runs
* rests on: a deliberate fail-closed choice, labelled
* best authority: WantsToRunStrategyFactory in libcozmoEngine.so, which names every strategy type
* evidence: IBehavior::ReadFromJson
* unresolved: the strategy types not yet modelled and what each one tests

**M8-007 — Per-play track lock is recorded and not applied** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/SteppedBehavior.cs:205`
* effect: a track the engine would mute for one play still moves
* rests on: deferred; the scheduler has no per-play track mask
* best authority: MovementComponent::LockTracks in libcozmoEngine.so
* unresolved: the mask the engine applies per play and where it is released

### M10-derived — Derived robot state and reaction strategies

**M10-006 — The unexpected-movement detector is gated on direct drive** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/UnexpectedMovement.cs:47`
* effect: the detector fires, or fails to fire, during commanded motion
* rests on: inferred as to which locks the engine tests; the skip itself is native
* best authority: MovementComponent::CheckForUnexpectedMovement 0x0063E398 and the lock it reads
* evidence: MovementComponent::CheckForUnexpectedMovement 0x0063E398
* unresolved: which track locks the engine tests before skipping the check

**M10-007 — The pose rewind after unexpected movement is not reproduced** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/UnexpectedMovement.cs:66`
* effect: the robot pose is not corrected back to the start timestamp, so later odometry is off
* rests on: deferred
* best authority: MovementComponent::CheckForUnexpectedMovement 0x0063E398 continues into the rewind
* evidence: MovementComponent::CheckForUnexpectedMovement 0x0063E398
* unresolved: the rewind itself, which is in the same function that was read for the detector

**M10-008 — Resume-last mechanics after a reaction** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs`
* effect: the behaviour that was interrupted does or does not come back
* rests on: inferred structure
* best authority: the engine behaviour manager resume path
* unresolved: whether the engine resumes the interrupted behaviour, and under what conditions

**M10-009 — The obstacle-detected flag has a local source** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/IBehavior.cs:45`
* effect: ReactToObstacle runs at the wrong times
* rests on: a local hook: true while an obstacle stop is pending
* best authority: StrategyObstacleDetected in libcozmoEngine.so and whatever writes the flag it reads
* unresolved: what sets the engine obstacle-detected flag

### M11-vision — Markers, camera geometry and BlockWorld

**M11-005 — Front-end pixel algorithms, the 0.75 dark multiplier and PnP numerics** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/QuadDetector.cs`
* effect: markers are found at slightly different places, or not found
* rests on: local implementations of steps the engine parameterises
* best authority: MarkerDetector::Parameters::Initialize gives the parameters, which are read; the pixel loops themselves were not transcribed
* evidence: MarkerDetector::Parameters::Initialize
* unresolved: the engine own quad extraction and refinement, which is in the binary

**M11-006 — Clustering tolerances, the flat-snap angle, history windows, verification timeout** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs`
* effect: observations merge into the wrong object, or fail to merge
* rests on: chosen locally (20 mm / 10 degrees, 8 degrees)
* best authority: the engine BlockWorld clustering
* unresolved: the engine own tolerances

**M11-007 — Two misses before an object pose is forgotten** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs:127`
* effect: a cube is forgotten too early or too late
* rests on: read as cmp r3, #1 and taken as 2 misses
* best authority: MarkObjectUnknown in libcozmoEngine.so
* evidence: MarkObjectUnknown
* unresolved: the comparison was read but the counter it compares was not traced

**M11-008 — Minimum projected marker size for the visibility test, 10 pixels** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs:140`
* effect: a distant cube is or is not counted as visible
* rests on: inferred
* best authority: the engine visibility test
* unresolved: the engine threshold

**M11-009 — A located cube that reports movement over the radio is marked Dirty** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs:319`
* effect: the pose of a moved cube is trusted or distrusted differently
* rests on: inferred
* best authority: the engine ObjectMoved handling
* unresolved: what the engine does to the pose on ObjectMoved

**M11-010 — Occlusion is not modelled; a marker that passes the geometric tests counts as visible** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs:65`
* effect: a hidden cube is reported visible
* rests on: deferred
* best authority: the engine occluder list and IsAnythingBehind
* unresolved: the occluder list and the IsAnythingBehind test

**M11-011 — NV storage request framing and MORE chunking for the camera calibration** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/CameraCalibration.cs:112`
* effect: the calibration read fails, so live vision falls back or refuses
* rests on: inferred framing
* best authority: the engine NV storage client and the CLAD definition
* unresolved: the request Length field and second byte, and the chunking rule

**M11-014 — SetBodyAngle is read as an absolute body angle in the robot pose frame** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/VisionSystem.cs:264`
* effect: the robot turns to the wrong heading
* rests on: the packing is native; the frame of reference is inferred
* best authority: the engine SetBodyAngle caller
* evidence: packing read from libcozmoEngine.so
* unresolved: whether the angle is absolute or relative; hardware item L would also show it

**M11-015 — Turn speed 100 deg per s and acceleration 10 rad per s squared** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/VisionSystem.cs:265`
* effect: turns are faster or slower than the app
* rests on: chosen locally
* best authority: the engine turn action constants
* unresolved: the engine own speed and acceleration for this turn

### M12-manipulation — Docking, carrying and pre-action poses

**M12-005 — Unread DockWithObject fields are sent as zero** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/Docking.cs:75`
* effect: the firmware receives a zero where the app sends something else, and may dock differently
* rests on: the fourth float and the last two bytes are sent as zero because they were not traced
* best authority: the engine DockWithObject packing, which was read for the other fields
* evidence: packing at 0x00632BAE..0x00632BEE
* unresolved: what the engine puts in the fourth float and the two trailing bytes

**M12-006 — A carried object is released in the world model when the put-down animation completes** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/ManipulationBehaviors.cs:92`
* effect: the world model believes the robot is carrying a cube it has put down, or the reverse
* rests on: inferred; the engine learns it from the robot carry state
* best authority: the robot carry state in RobotState and the engine handling of it
* unresolved: which field of RobotState carries the carry state and how the engine reacts to it

**M12-007 — Lift-load timeout and the accelerometer object-did-not-move check are not implemented** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/DockActions.cs:135`
* effect: a failed pick-up is reported as a success
* rests on: deferred
* best authority: the engine pick-up verification in IDockAction
* unresolved: both checks, which are in the dock action already partly read

**M12-009 — Retry limits: 3 attempts for manipulation, 2 for the charger, 3 for the wheelie** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/ManipulationSystem.cs:125`
* effect: the robot gives up sooner or later than the app
* rests on: inferred numbers
* best authority: the engine behaviour classes, which carry their own retry counts
* unresolved: the retry count each engine behaviour uses

**M12-010 — The search-for-block fallback is not implemented** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/ManipulationSystem.cs:125`
* effect: the robot stops where the app would look around for the cube
* rests on: deferred
* best authority: the engine search behaviour
* unresolved: the search pattern

**M12-011 — A failed final turn fails DriveToObjectAction as DidNotReachPreActionPose** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/DriveActions.cs`
* effect: the caller sees the nearest shipped result rather than the engine own
* rests on: a labelled reduction to the nearest shipped ActionResult
* best authority: the engine DriveToObjectAction result for this case
* unresolved: which ActionResult the engine reports

**M12-012 — Non-upright stacking needs a progression unlock and is not implemented** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/ManipulationBehaviors.cs:229`
* effect: the robot refuses placements the app would attempt
* rests on: deferred, uprights only
* best authority: the engine placement rules
* unresolved: the unlock condition and the non-upright placement geometry

### M13-navigation — Planning, charger and block configurations

**M13-003 — Obstacle padding and the soft-ring penalty** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs:110`
* effect: paths hug or avoid obstacles differently, and some plans fail that would succeed
* rests on: inferred from the robot 56 by 70 mm body
* best authority: xythetaEnvironment in libcozmoEngine.so, whose interface was read but whose padding and penalty were not
* evidence: xythetaEnvironment
* unresolved: the engine padding radius and penalty weight

**M13-004 — In-place turn cost and the arc reconstruction from the primitive file** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs:71`
* effect: the planner prefers different primitives, so the robot takes a different route
* rests on: inferred reconstruction
* best authority: the engine planner cost function and its precomputed heuristic table
* unresolved: the cost of an in-place turn and the exact arc the engine builds from each primitive

**M13-006 — One active beacon at a time in AIWhiteboard** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/AIWhiteboard.cs:31`
* effect: the robot returns to the wrong place
* rests on: inferred from GetActiveBeacon being singular
* best authority: the engine AIWhiteboard, whose interface is exported
* evidence: AIWhiteboard exports
* unresolved: whether the engine keeps more than one beacon, and how failures are recorded

**M13-007 — Stack detection tolerance of 15 mm above a cube height of 44** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/BlockConfigurations.cs:41`
* effect: a stack is or is not recognised
* rests on: inferred tolerance
* best authority: FindObjectOnTopOrUnderneathHelper in libcozmoEngine.so
* evidence: FindObjectOnTopOrUnderneathHelper
* unresolved: the engine tolerance

**M13-008 — The charger drive-off is allowed to end short when the contacts report** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/ChargerActions.cs:54`
* effect: the robot stops at a different distance from the charger
* rests on: inferred
* best authority: DriveOffChargerContactsAction CheckIfDone in libcozmoEngine.so
* evidence: Robot::IsOnChargerContacts
* unresolved: whether the engine ends the drive early or always drives the full distance

**M13-009 — Charger length and the pre-dock pose derived from the drive-off distance** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/ChargerGeometry.cs:8`
* effect: the robot aims at the wrong point when mounting
* rests on: inferred from the drive-off distance
* best authority: the charger object definition at 0x004E9B6C, partly read
* evidence: charger 0x004E9B6C
* unresolved: the charger real dimensions and the pose the mount action aligns to

**M13-010 — Workout selection by index, medium by default** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Manipulation/Workouts.cs:43`
* effect: the robot does a different workout than the app would pick
* rests on: chosen locally; the selection input is inferred to be the energy need
* best authority: WorkoutComponent in libcozmoEngine.so and the shipped workout config
* evidence: WorkoutComponent
* unresolved: what the engine selects on

### M14-faces — Face and pet pipeline

**M14-002 — Frame counts waited by VisuallyVerifyFaceAction and TurnTowardsFaceAction** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/FaceActions.cs:69`
* effect: a face is declared verified too early or too late
* rests on: inferred (5 frames)
* best authority: the engine face actions
* unresolved: the engine frame budget for each

**M14-003 — TrackFaceAction update period of 100 ms; eye shift and driving animation not implemented** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/FaceActions.cs:156`
* effect: tracking is jerkier and the face does not react while tracking
* rests on: chosen locally; the extra layers are deferred
* best authority: the engine TrackFaceAction
* unresolved: the engine update period and the layers it adds

**M14-004 — PetInitialDetection strategy shape** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FaceBehaviors.cs:524`
* effect: the pet reaction fires at the wrong times
* rests on: inferred shape: the first sighting of a pet id
* best authority: the engine strategy class for PetInitialDetection
* evidence: reactionTrigger_behavior_map.json
* unresolved: the strategy class was not disassembled

**M14-005 — TurnTowardsImagePoint aims at a ray 200 mm out** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FaceBehaviors.cs:573`
* effect: the robot turns to the wrong angle for a detected pet
* rests on: inferred range
* best authority: the engine TurnTowardsImagePointAction
* unresolved: the range the engine assumes

**M14-007 — The memory map is not modelled, so CanDriveIdealDistanceForward always allows the drive** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FaceBehaviors.cs:256`
* effect: the robot drives towards a face into an obstacle it should have known about
* rests on: deferred
* best authority: the engine memory map
* unresolved: the memory map structure and the query

### M15-freeplay — Needs, activities and freeplay

**M15-004 — Needs decay modifiers, damaged parts and persistence are not implemented** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs:45`
* effect: needs decay at the wrong rate and do not survive a restart
* rests on: deferred
* best authority: the shipped needs config carries DecayModifiers; the engine reads them
* evidence: needs config
* unresolved: how one need level scales another decay, the damaged-parts bookkeeping, and where the engine persists the state

**M15-005 — The flat 3 s recent-end cooldown case is not reproduced** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`
* effect: an activity that just ended can restart sooner than the app allows
* rests on: read in WantsToStart and left out because the second time argument was not identified
* best authority: IActivityStrategy::WantsToStart 0x005B529C
* evidence: WantsToStart 0x005B529C
* unresolved: what the second time argument is measured from

**M15-006 — Activity deselection hook and the null-pick path** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs:23`
* effect: state is cleared at the wrong moment when an activity ends or picks nothing
* rests on: inferred; IActivity::OnDeselected reads it but the exact hook was not traced
* best authority: ActivityFreeplay::GetDesiredActiveBehaviorInternal 0x005AE2xx and IActivity::OnDeselected
* evidence: ActivityFreeplay::GetDesiredActiveBehaviorInternal
* unresolved: the hook and the null-pick path, instruction by instruction

**M15-007 — boredomMultiplier, feature gates and the pyramid strategy random factor are not applied** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`
* effect: activity scores differ from the app
* rests on: deferred
* best authority: the shipped activity configs carry them
* evidence: activities/**
* unresolved: how the engine applies each

**M15-008 — The needsActionID hook and desired-from-objects ordering** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs:139`
* effect: a different behaviour is picked when several objects are desirable
* rests on: inferred ordering
* best authority: the engine activity strategy
* unresolved: the ordering rule and what needsActionID does

**M15-009 — Emotion event names fired when a cube is placed in a beacon** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs:825`
* effect: mood does not move where the app moves it
* rests on: deferred: the names were not read
* best authority: FireEmotionEvents call sites in libcozmoEngine.so and mood_config.json
* unresolved: the event names

**M15-010 — A block whose location is no longer valid goes straight to the upset reaction** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeReactions.cs:259`
* effect: the robot reacts where the app might search first
* rests on: inferred: the engine log string was found, the branch after it was not read
* best authority: the engine behaviour that logs the location is no longer valid
* unresolved: what the engine does after that log line

**M15-011 — The beacon is centred on the robot** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs:793`
* effect: cubes are gathered to the wrong place
* rests on: inferred; the engine selection logic was not read further
* best authority: the engine beacon selection
* unresolved: how the engine chooses the beacon centre

**M15-012 — Images waited for after a put-down, and the CantHandleTallStack trigger name** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs:636`
* effect: a stack check runs on too few frames, or the wrong animation plays
* rests on: inferred from acknowledgeObject.json and from the trigger name
* best authority: the engine behaviour classes
* evidence: acknowledgeObject.json
* unresolved: the engine image count and trigger

## Items that are not gaps but are not the engine either

| id | subsystem | status | what | why it is not a gap |
| --- | --- | --- | --- | --- |
| M1-013 | M1-transport | COMPATIBILITY_POLICY | Windows high-resolution timer and the tick loop | none needed: the engine runs on Android and this is the local equivalent |
| M1-014 | M1-transport | COMPATIBILITY_POLICY | Three-thread dispatch, handler isolation, socket error handling | the engine threading is its own; nothing on the wire depends on it |
| M1-015 | M1-transport | COMPATIBILITY_POLICY | 5 s connect timeout | the engine connect timeout was not read; nothing the robot does depends on it |
| M3-007 | M3-device | EQUIVALENT_IMPLEMENTATION | RLE encoder emits runs only; the engine also uses skip and repeat and falls back to a raw 1024-byte frame | CompressRLE 0x00581904, which was read; the raw fallback path above MAX_FACE_FRAME_SIZE was read but not built |
| M3-008 | M3-device | HARDWARE_ONLY | Scanline parity: dd written as 01 | CompressRLE 0x00581904 shows dd carries the bits of a pair of canvas rows; the firmware use of them is not in the engine |
| M3-013 | M3-device | EQUIVALENT_IMPLEMENTATION | TargetInFlight 10, counter-paced feed, Busy window 200 ms, priming | the engine feeds to its own budget every update (UpdateStream 0x0057C84C) |
| M3-016 | M3-device | HARDWARE_ONLY | Colour camera frames are half-width three-component JPEG | MiniColorToJpeg table 0x00C48D84 |
| M3-017 | M3-device | COMPATIBILITY_POLICY | Test tones, beeps and sweeps | not applicable |
| M4-005 | M4-control | COMPATIBILITY_POLICY | Action ids cycle 1..255 | the engine action id allocation |
| M4-006 | M4-control | COMPATIBILITY_POLICY | Wheel confirmation tolerance 35 percent / 5 mm per s | not applicable: the engine does not confirm wheel speeds this way |
| M4-007 | M4-control | COMPATIBILITY_POLICY | StopAll sends StopAllMotors and a zero DriveWheels | the engine stop path |
| M5-017 | M5-animation | EQUIVALENT_IMPLEMENTATION | Fallback to another audio alternative when the chosen one cannot be decoded | the engine does not fall back; it plays what Wwise resolves |
| M5-018 | M5-animation | EQUIVALENT_IMPLEMENTATION | How many frames one wall-clock tick streams | the engine streams to the audio budget on every update regardless of the clock (UpdateStream 0x0057C84C) |
| M5-019 | M5-animation | COMPATIBILITY_POLICY | The last face is held after the last face keyframe; an oversize face payload is dropped | the engine display path, partly read (see M3-007) |
| M5-020 | M5-animation | COMPATIBILITY_POLICY | Expressions helper faces | not applicable |
| M6-002 | M6-wwise-bank | EQUIVALENT_IMPLEMENTATION | Vorbis rebuild with external codebooks and granule computation | the Wwise Vorbis packing; no runtime in the package to check against |
| M6-003 | M6-wwise-bank | COMPATIBILITY_POLICY | IMA ADPCM mono decode; stereo refused | no stereo ADPCM clip is reachable from any robot event in the shipped banks |
| M6-004 | M6-wwise-bank | BLOCKED_EXTERNAL | Nearest-sample resampling and channel averaging | the Wwise runtime resampler, which is not in the package |
| M7-009 | M7-behaviour | EQUIVALENT_IMPLEMENTATION | Idle head and lift move through the motion API instead of the live animation | the engine adds HeadAngleKeyFrame(currentDeg, variability 6, duration) and LiftHeightKeyFrame(35, 8, duration) to its live animation and streams them as 0x93 / 0x94 |
| M7-011 | M7-behaviour | COMPATIBILITY_POLICY | Arbitration order caller over reaction over idle; autonomy off by default; 5 s per-reaction cooldown | the engine per-trigger cooldowns exist only where the shipped map gives one |
| M7-015 | M7-behaviour | EQUIVALENT_IMPLEMENTATION | Pick-up falls back to the raw status flag until the off-treads classifier is enabled | Robot::CheckAndUpdateTreadsState 0x00511E00, which is implemented and used once calibration is reported |
| M8-008 | M8-framework | EQUIVALENT_IMPLEMENTATION | The 5 s calibration allowance borrowed from ReactToImpact | BehaviorReactToImpact 0x00606348 |
| M8-009 | M8-framework | COMPATIBILITY_POLICY | Behaviour scope undo order | the engine Smart* destructor order |
| M8-010 | M8-framework | COMPATIBILITY_POLICY | Behaviour inventory classifier rules | not applicable: this is bookkeeping, not robot behaviour |
| M9-011 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | The render goes through the effect chain the robot bus carries, not a local peak normalisation | libcozmoEngine.so for the routing and Init.bnk for the chain and its parameters |
| M9-013 | M9-wwise-music | BLOCKED_EXTERNAL | Whether Wwise routes MIDI notes into the get-in branch of the singing sampler | the Wwise MIDI dispatch rule, which is runtime behaviour. No Wwise runtime ships in the APK: no AkSoundEngine, CAk*, AkModulator or Wwise string occurs in libcozmoEngine.so, libunity.so or libmain.so, and there is no separate Wwise library. The shipped data was searched to exhaustion: the branch parent and the target child list, the key and velocity ranges on all 18 containers under it, the play-on-note property, the channel mask, the node bit vectors, the blend container layers (there are none), and the Wwise object paths in Cozmo.txt |
| M9-014 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | Velocity is ignored | the shipped bank: no node under the MIDI target carries a velocity RTPC, and only one velocity layer of recordings ships |
| M9-015 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | The final-PCM cache freezes the renderer random choices for the life of a source | the container avoid-repeat and weight fields are read and honoured within a render |
| M9-016 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | The song is rendered whole, ahead of playback, on a worker | not applicable: this is a local architecture decision, and the observable output is the same once the render completes |
| M9-018 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A Stop action ends the streaming song when its target is the Play target or an ancestor | the shipped bank: Stop__Robot_VO__Cozmo_Singing_Stop holds three action-type-1 actions targeting the three music switch containers |
| M9-020 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A clip plays its source from BeginTrim for its length; a note still held at the clip end is released there | the shipped clip fields, which are read exactly |
| M9-021 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A Play event with several Play actions renders only the first | the shipped bank lists all nine actions |
| M9-022 | M9-wwise-music | BLOCKED_EXTERNAL | Container semantics: blend and actor-mixer play all children, random picks one by weight avoiding the last, sequence steps its playlist | the Wwise runtime, which is not in the package; the container fields themselves are read exactly |
| M9-023 | M9-wwise-music | HARDWARE_ONLY | How the stock app sounded when it sang | a recording of a stock Cozmo singing, or the app running against a robot |
| M9-024 | M9-wwise-music | BLOCKED_EXTERNAL | Whether cozmo_singing_note_off also stops the voice it is attached to | the shipped bank: this modulator alone of the eleven sets property 15, to the integer 2. The two readings that fit the numbering argued from the data agree on what a listener hears - under either one the voice ends when the note is released, which is also what the note layer's break-on-note-off bit asks for |
| M9-025 | M9-wwise-music | BLOCKED_EXTERNAL | The shape a Wwise LFO produces between its extremes | the Wwise runtime, which does not ship in the APK (no AkSoundEngine, CAk* or Wwise strings occur in any .so). The bank settles the frequency, the attack, the pulse width and the range the curve maps the output onto; it cannot settle the waveform Wwise draws |
| M9-026 | M9-wwise-music | BLOCKED_EXTERNAL | The filter and limiter arithmetic between the shipped settings | Audiokinetic's own ParametricEQ and AkPeakLimiter, which are in the Wwise runtime; no Wwise runtime ships in the APK |
| M9-027 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | Robot_Bus_Eq_HiLowPass low-pass at 14298 Hz is above Nyquist for the robot's rate | Init.bnk gives 14298 Hz; AnimConstants::AUDIO_SAMPLE_RATE gives 22320 Hz, so Nyquist is 11160 Hz in the engine too |
| M10-005 | M10-derived | EQUIVALENT_IMPLEMENTATION | The off-treads debounce runs on the local clock | the engine debounces against its own base-station clock in milliseconds; the same quantity, a different source |
| M11-012 | M11-vision | COMPATIBILITY_POLICY | The nominal camera calibration stand-in | not applicable: the live path reads the robot own calibration and fails closed without it |
| M11-013 | M11-vision | COMPATIBILITY_POLICY | AllowUnconnectedObjects switch | the engine connected-object rule, which is implemented |
| M11-016 | M11-vision | BLOCKED_EXTERNAL | Face, pet and motion detection | Omron OKAO, 177 OKAO_* exports of proprietary code; no Anki algorithm exists to transcribe |
| M12-008 | M12-manipulation | EQUIVALENT_IMPLEMENTATION | The pose of a carried object is parented to the lift by a reduction | the engine pose tree |
| M13-005 | M13-navigation | COMPATIBILITY_POLICY | A lattice-planner failure falls back to the straight line when the same environment reports it clear | the engine returns a planning failure |
| M14-006 | M14-faces | BLOCKED_EXTERNAL | Text to speech is not implemented | Acapela (libacattsandroid.so) plus the Anki Wave Portal Wwise source plug-in; both third-party binaries |
| TOOL-001 | tools | COMPATIBILITY_POLICY | Conformance CLI pass and fail criteria | not applicable: the harness is not part of the app |
| TOOL-002 | tools | COMPATIBILITY_POLICY | The fake robot side answers place docks without a marker signal | not applicable: this is the test double, not the robot |
| TOOL-003 | tools | COMPATIBILITY_POLICY | The --nominal calibration override in the vision, manipulation and freeplay tools | the live path fails closed without a real calibration |

