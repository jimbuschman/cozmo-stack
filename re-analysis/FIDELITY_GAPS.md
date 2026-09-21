# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **203 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 135 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 13 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 26 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 1 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
| COMPATIBILITY_POLICY | 17 | A deliberate product or platform decision this stack intends to keep: offline tools, the test harness, PC-side plumbing, or a stand-in the operator has to ask for. Not a place to put fidelity work that is hard. |
| HARDWARE_ONLY | 3 | No shipped artifact can settle it; only a robot, or a recording of the stock app, can. |
| BLOCKED_EXTERNAL | 8 | The answer lies in third-party code or data that is not in the package (Omron OKAO, the Wwise runtime DSP, the Acapela text-to-speech engine). |

## Where each subsystem stands

Four separate things, because one word cannot carry them. **Source read** means nothing is left that
reading the original would settle. **Built** means nothing the original is known to do is knowingly
not done here. Neither says the behaviour is faithfully reproduced: the last two columns are what
remains after both, and they do not go away by working harder on this repository.

| subsystem | records | to read | to build | blocked externally | needs hardware | source read | built |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| M1-transport — UDP transport and reliability | 15 | 0 | 0 | 0 | 0 | yes | yes |
| M2-protocol — CLAD messages and protocol helpers | 6 | 0 | 0 | 0 | 0 | yes | yes |
| M3-device — Camera, display and audio device layer | 17 | 0 | 0 | 0 | 1 | yes | yes |
| M4-control — Motion, sensors, lights and cubes | 9 | 0 | 0 | 0 | 0 | yes | yes |
| M5-animation — Animation clips, scheduler and face | 20 | 3 | 0 | 0 | 0 | no | yes |
| M6-wwise-bank — Wwise bank reading and codecs | 7 | 1 | 0 | 0 | 0 | no | yes |
| M7-behaviour — Idle, mood and reactions | 16 | 0 | 0 | 0 | 0 | yes | yes |
| M8-framework — Behaviour framework and scoring | 10 | 6 | 0 | 0 | 0 | no | yes |
| M9-wwise-music — Wwise music, the MIDI sampler and singing | 27 | 0 | 0 | 6 | 1 | yes | yes |
| M10-derived — Derived robot state and reaction strategies | 9 | 1 | 0 | 0 | 0 | no | yes |
| M11-vision — Markers, camera geometry and BlockWorld | 16 | 1 | 0 | 1 | 0 | no | yes |
| M12-manipulation — Docking, carrying and pre-action poses | 16 | 0 | 0 | 0 | 0 | yes | yes |
| M13-navigation — Planning, charger and block configurations | 12 | 0 | 0 | 0 | 0 | yes | yes |
| M14-faces — Face and pet pipeline | 7 | 5 | 0 | 1 | 0 | no | yes |
| M15-freeplay — Needs, activities and freeplay | 12 | 9 | 0 | 0 | 0 | no | yes |
| tools — Conformance CLI and offline tools | 4 | 0 | 0 | 0 | 0 | yes | yes |

## Still to read: every RECOVERABLE_GAP

Each of these is a question the original can answer and nobody has asked it yet.

### M5-animation — Animation clips, scheduler and face

**M5-012 — Keyframe volume applied as a linear PCM gain** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: animation audio plays at the wrong level
* rests on: a straight linear reading of the keyframe field
* best authority: the engine passes the keyframe volume to Wwise, which maps it through an RTPC curve
* evidence: RobotAudioKeyFrame::SetMembersFromFlatBuf 0x004F9E54
* outstanding: the RTPC or parameter the engine posts the keyframe volume to, and the curve on it; RobotAudioClient::SetCozmoEventParameter was not traced

**M5-015 — Procedural-face corner-radius parameter assignment and polygon fill rule** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`
* effect: eye corners are rounded on the wrong corners, or filled differently
* rests on: a reading of DrawEye that stopped short
* best authority: ProceduralFaceDrawer::DrawEye in libcozmoEngine.so
* evidence: re-analysis/PROCEDURAL_FACE.md
* outstanding: which radius parameter belongs to which corner, and the fill rule for the eye polygon

**M5-019 — The last face is held after the last face keyframe; an oversize face payload is dropped** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`
* effect: the screen holds a face the engine would have cleared, or drops a frame the engine would have sent another way
* rests on: a choice of this stack
* best authority: the engine display path, read only as far as CompressRLE; what it does with the screen at the end of a clip was not established
* outstanding: what the engine leaves on the screen after a clip ends. The oversize half of this is M3-007, which is known and unbuilt

### M6-wwise-bank — Wwise bank reading and codecs

**M6-003 — Stereo IMA ADPCM is refused, and eight robot sound effects are silent because of it** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAdpcm.cs`
* effect: eight shipped Robot_SFX events make no sound at all: the effort grunts, the spark launch and the four scan sounds
* rests on: mono ADPCM decodes; stereo is refused because the block layout was not established
* best authority: the seven shipped stereo ADPCM files themselves, which are the thing that would settle the layout: IMA ADPCM block packing is arithmetic that a file either fits or does not
* evidence: wwise --coverage: Play__Robot_SFX__Effort_Long, Effort_Medium, Effort_Fail, Spark_Launch, Scan_Loop_Play, Scan_Start, Scan_Stop, Scan_Single; wwise --validate: 7 files, "ADPCM with 2 channels is not decoded"
* outstanding: the stereo block layout. An earlier version of this record said no robot event reaches such a file; the coverage tool now lists the eight that do, which is what corrected it

### M8-framework — Behaviour framework and scoring

**M8-003 — Scored selection: score times repetition penalty, highest wins** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs`
* effect: a different behaviour is chosen at every decision point
* rests on: structure inferred from exported names
* best authority: IBehavior::ReadFromScoredJson 0x005BC489 and ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060B23E, of which the chooser has since been read
* evidence: ScoringBSRunnableChooser::GetDesiredActiveBehavior
* outstanding: ReadFromScoredJson has not been disassembled, so how a shipped config turns into a score is still not read

**M8-004 — Behaviour scores 1.0 and 5.0 where the shipped configs carry none** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: the scoring chooser ranks behaviours by numbers that are not the app numbers
* rests on: chosen because they looked right
* best authority: IBehavior::ReadFromScoredJson 0x005BC489 and the shipped behaviour configs
* outstanding: where the engine gets a score for a behaviour whose config carries none

**M8-005 — PlayAnim uses the first trigger that resolves** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs`
* effect: a different clip plays when several triggers are listed
* rests on: inferred
* best authority: the engine PlayAnim behaviour class
* outstanding: the engine selection rule among several triggers

**M8-006 — A strategy type this stack does not model reports not runnable** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs:126`
* effect: a behaviour whose config names an unmodelled strategy never runs
* rests on: a deliberate fail-closed choice, labelled
* best authority: WantsToRunStrategyFactory in libcozmoEngine.so, which names every strategy type
* evidence: IBehavior::ReadFromJson
* outstanding: the strategy types not yet modelled and what each one tests

**M8-007 — Per-play track lock is recorded and not applied** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/SteppedBehavior.cs:205`
* effect: a track the engine would mute for one play still moves
* rests on: deferred; the scheduler has no per-play track mask
* best authority: MovementComponent::LockTracks in libcozmoEngine.so
* outstanding: the mask the engine applies per play and where it is released

**M8-008 — A 5 s calibration allowance is borrowed from ReactToImpact for other behaviours** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/SteppedBehavior.cs:265`
* effect: behaviours wait 5 s for recalibration where the engine may wait a different time, or not wait
* rests on: a number borrowed from the one place the engine is known to use it
* best authority: BehaviorReactToImpact 0x00606348 uses 5 s. What the other behaviour classes do about recalibration was not read
* evidence: BehaviorReactToImpact::TransitionToPlayingAnim 0x00606348
* outstanding: what each behaviour class actually waits for, which is in the classes themselves

### M10-derived — Derived robot state and reaction strategies

**M10-007 — What the engine does after unexpected movement: a history lookup, an obstacle and a new pose** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/UnexpectedMovement.cs`
* effect: the robot keeps the pose it drifted to, and nothing is left in the world where it was blocked
* rests on: nothing: this stack raises the event and stops there
* best authority: MovementComponent::CheckForUnexpectedMovement 0x0063E398 continues past the event, and the shape of what it does is read now
* evidence: on the event it checks BehaviorManager::IsReactionTriggerEnabled(0x14) (0x0063E60A) and does nothing further when that trigger is off; it asks RobotStateHistory::ComputeStateAt(startTimestamp, ...) 0x0063E63E for the robot's state at the tick the disagreement began - the timestamp it stored at +0x94 - and warns "Could not get robot pose at t=%u" when it cannot; it divides the accumulated wheel speeds at +0x98 and +0x9C by the count at +0xA0 to get mean left and right speeds (0x0063E6C0, 0x0063E6CE); it takes MarkerlessObject::GetSizeByType(0x10) and adds 5 mm to its first component (0x0063E6BC..0x0063E6D4), then picks one of four offsets from the robot's own footprint - +22.1 ahead, -55.9 behind, +/-27.1 to the sides, with a +/-pi/2 rotation about Z for the two turned-in-place cases - and a name for each; it ends with Robot::SetNewPose (0x0063E87E), having copied the robot's current rotation over the constructed pose's (the 32-byte loop at 0x0063E868, the transform's translation living at +0x20); the three UnexpectedMovementType values are read: TURNED_BUT_STOPPED, TURNED_IN_SAME_DIRECTION, TURNED_IN_OPPOSITE_DIRECTION
* outstanding: the exact composition of the four offset poses and which of them SetNewPose finally receives; the reading above is of the branches, not yet of the arithmetic that assembles them

### M11-vision — Markers, camera geometry and BlockWorld

**M11-005 — Front-end pixel algorithms, the 0.75 dark multiplier and PnP numerics** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/QuadDetector.cs`
* effect: markers are found at slightly different places, or not found
* rests on: local implementations of steps the engine parameterises
* best authority: MarkerDetector::Parameters::Initialize gives the parameters, which are read; the pixel loops themselves were not transcribed
* evidence: MarkerDetector::Parameters::Initialize
* outstanding: the engine own quad extraction and refinement, which is in the binary

### M14-faces — Face and pet pipeline

**M14-002 — Frame counts waited by VisuallyVerifyFaceAction and TurnTowardsFaceAction** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/FaceActions.cs:69`
* effect: a face is declared verified too early or too late
* rests on: inferred (5 frames)
* best authority: the engine face actions
* outstanding: the engine frame budget for each

**M14-003 — TrackFaceAction update period of 100 ms; eye shift and driving animation not implemented** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/FaceActions.cs:156`
* effect: tracking is jerkier and the face does not react while tracking
* rests on: chosen locally; the extra layers are deferred
* best authority: the engine TrackFaceAction
* outstanding: the engine update period and the layers it adds

**M14-004 — PetInitialDetection strategy shape** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FaceBehaviors.cs:524`
* effect: the pet reaction fires at the wrong times
* rests on: inferred shape: the first sighting of a pet id
* best authority: the engine strategy class for PetInitialDetection
* evidence: reactionTrigger_behavior_map.json
* outstanding: the strategy class was not disassembled

**M14-005 — TurnTowardsImagePoint aims at a ray 200 mm out** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FaceBehaviors.cs:573`
* effect: the robot turns to the wrong angle for a detected pet
* rests on: inferred range
* best authority: the engine TurnTowardsImagePointAction
* outstanding: the range the engine assumes

**M14-007 — The memory map is not modelled, so CanDriveIdealDistanceForward always allows the drive** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FaceBehaviors.cs:256`
* effect: the robot drives towards a face into an obstacle it should have known about
* rests on: deferred
* best authority: the engine memory map
* outstanding: the memory map structure and the query

### M15-freeplay — Needs, activities and freeplay

**M15-004 — Needs decay modifiers, damaged parts and persistence are not implemented** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs:45`
* effect: needs decay at the wrong rate and do not survive a restart
* rests on: deferred
* best authority: the shipped needs config carries DecayModifiers; the engine reads them
* evidence: needs config
* outstanding: how one need level scales another decay, the damaged-parts bookkeeping, and where the engine persists the state

**M15-005 — The flat 3 s recent-end cooldown case is not reproduced** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`
* effect: an activity that just ended can restart sooner than the app allows
* rests on: read in WantsToStart and left out because the second time argument was not identified
* best authority: IActivityStrategy::WantsToStart 0x005B529C
* evidence: WantsToStart 0x005B529C
* outstanding: what the second time argument is measured from

**M15-006 — Activity deselection hook and the null-pick path** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs:23`
* effect: state is cleared at the wrong moment when an activity ends or picks nothing
* rests on: inferred; IActivity::OnDeselected reads it but the exact hook was not traced
* best authority: ActivityFreeplay::GetDesiredActiveBehaviorInternal 0x005AE2xx and IActivity::OnDeselected
* evidence: ActivityFreeplay::GetDesiredActiveBehaviorInternal
* outstanding: the hook and the null-pick path, instruction by instruction

**M15-007 — boredomMultiplier, feature gates and the pyramid strategy random factor are not applied** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`
* effect: activity scores differ from the app
* rests on: deferred
* best authority: the shipped activity configs carry them
* evidence: activities/**
* outstanding: how the engine applies each

**M15-008 — The needsActionID hook and desired-from-objects ordering** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs:139`
* effect: a different behaviour is picked when several objects are desirable
* rests on: inferred ordering
* best authority: the engine activity strategy
* outstanding: the ordering rule and what needsActionID does

**M15-009 — Emotion event names fired when a cube is placed in a beacon** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs:825`
* effect: mood does not move where the app moves it
* rests on: deferred: the names were not read
* best authority: FireEmotionEvents call sites in libcozmoEngine.so and mood_config.json
* outstanding: the event names

**M15-010 — A block whose location is no longer valid goes straight to the upset reaction** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeReactions.cs:259`
* effect: the robot reacts where the app might search first
* rests on: inferred: the engine log string was found, the branch after it was not read
* best authority: the engine behaviour that logs the location is no longer valid
* outstanding: what the engine does after that log line

**M15-011 — The beacon is centred on the robot** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs:793`
* effect: cubes are gathered to the wrong place
* rests on: inferred; the engine selection logic was not read further
* best authority: the engine beacon selection
* outstanding: how the engine chooses the beacon centre

**M15-012 — Images waited for after a put-down, and the CantHandleTallStack trigger name** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs:636`
* effect: a stack check runs on too few frames, or the wrong animation plays
* rests on: inferred from acknowledgeObject.json and from the trigger name
* best authority: the engine behaviour classes
* evidence: acknowledgeObject.json
* outstanding: the engine image count and trigger

## Still to build: every IMPLEMENTATION_GAP

Each of these is a question already answered. The original's behaviour is established and this stack knowingly does something else, so the work outstanding is writing it, not reading.

### M9-wwise-music — Wwise music, the MIDI sampler and singing

**M9-021 — A music event with several Play actions renders only the first** (not on the live path)

* where: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseMusic.cs`
* effect: Play__Codelab__Music_Tiny_Orchestra_Init plays one of its nine layers
* rests on: a reduction of this stack on the music path. Ordinary events no longer reduce this way: WwisePlayback plays every Play target together
* best authority: the shipped bank lists all nine Play actions, and the ordinary-event walk already shows the shape the music path needs. Singing is unaffected, which is why this is off the live path: each of the three tempo events holds exactly one Play action
* evidence: Play__Codelab__Music_Tiny_Orchestra_Init: 9 Play actions and 9 of type 0x1901; Play__Robot_VO__Cozmo_Singing_80bpm, _100bpm and _120bpm: one Play action each; nothing in cozmo-stack posts a Codelab music event
* outstanding: the music resolver has to carry every Play action rather than the first; nothing is left to read

## What remains after both: blocked externally, or needing hardware

| id | subsystem | status | what | why it cannot be settled here |
| --- | --- | --- | --- | --- |
| M3-008 | M3-device | HARDWARE_ONLY | Scanline parity: dd written as 01 | whether the firmware selects a physical row from the bit position; only the panel can show this |
| M3-016 | M3-device | HARDWARE_ONLY | Colour camera frames are half-width three-component JPEG | no colour capture exists; the robot has only ever been asked for grayscale |
| M9-013 | M9-wwise-music | BLOCKED_EXTERNAL | Whether Wwise routes MIDI notes into the get-in branch of the singing sampler | the dispatch rule itself. Only a recording of the stock app singing, or a Wwise runtime of this bank version, can settle it. This is the largest remaining doubt about how a rendered song sounds |
| M9-014 | M9-wwise-music | BLOCKED_EXTERNAL | Whether a note's velocity changes anything, when nothing in the bank binds it | whether Wwise applies a velocity-to-level mapping of its own to a MIDI voice. The shipped songs vary velocity over about 20 units, so if it does, some notes are a few dB quieter than this renders them. Only a Wwise runtime of this bank version, or a recording of the stock app, can settle it. Until the M9 re-audit this was recorded as an equivalent implementation, which the varying velocities do not support |
| M9-022 | M9-wwise-music | BLOCKED_EXTERNAL | Container semantics: blend and actor-mixer play all children, random picks one by weight avoiding the last, sequence steps its playlist | the runtime dispatch rule, in particular whether a container pre-filters its playlist to children that accept a MIDI note or picks first and then filters |
| M9-023 | M9-wwise-music | HARDWARE_ONLY | How the stock app sounded when it sang | sustain, release tails, level and whether the get-in branch is audible during a song |
| M9-024 | M9-wwise-music | BLOCKED_EXTERNAL | Whether cozmo_singing_note_off also stops the voice it is attached to | the name of modulator property 15 and the enumeration of its values, which live in the Wwise SDK; no Wwise runtime or header ships in the APK. What is left open is only whether the stop is this modulator or the break-on-note-off bit, not whether the note stops |
| M9-025 | M9-wwise-music | BLOCKED_EXTERNAL | The shape a Wwise LFO produces between its extremes | whether the output is unipolar or bipolar, and the exact waveform. This is on the live path: the cube shake is measured and the vibrato reaches a playing song, so any shake exercises this shape |
| M9-026 | M9-wwise-music | BLOCKED_EXTERNAL | The filter and limiter arithmetic between the shipped settings | the exact coefficient formulas and detector behaviour. The settings they act on are exact, so the shape is right and the detail is not |
| M11-016 | M11-vision | BLOCKED_EXTERNAL | Face, pet and motion detection | nothing recoverable: the detector is third-party binary code |
| M14-006 | M14-faces | BLOCKED_EXTERNAL | Text to speech is not implemented | the voice model and the plug-in are third-party binaries |

## Settled differences: equivalent implementations and kept policies

| id | subsystem | status | what | why it is settled |
| --- | --- | --- | --- | --- |
| M1-013 | M1-transport | COMPATIBILITY_POLICY | Windows high-resolution timer and the tick loop | none needed: the engine runs on Android and this is the local equivalent |
| M1-014 | M1-transport | COMPATIBILITY_POLICY | Three-thread dispatch, handler isolation, socket error handling | the engine threading is its own; nothing on the wire depends on it |
| M1-015 | M1-transport | COMPATIBILITY_POLICY | 5 s connect timeout | the engine connect timeout was not read; nothing the robot does depends on it |
| M3-004 | M3-device | COMPATIBILITY_POLICY | Warm-up frames are flagged, and delivered like any other | RobotToEngineImplMessaging::HandleImageChunk and VisionComponent::SetNextImage, read |
| M3-013 | M3-device | EQUIVALENT_IMPLEMENTATION | TargetInFlight 10, counter-paced feed, Busy window 200 ms, priming | the engine feeds to its own budget every update (UpdateStream 0x0057C84C) |
| M3-017 | M3-device | COMPATIBILITY_POLICY | Test tones, beeps and sweeps | not applicable |
| M4-004 | M4-control | COMPATIBILITY_POLICY | Motion is gated on calibration here; the engine reacts to it instead | HandleMotorCalibration 0x00536A68, BehaviorReactToMotorCalibration 0x006065F0, and the callers of IsHeadCalibrated / IsLiftCalibrated, all read |
| M4-005 | M4-control | COMPATIBILITY_POLICY | Action ids cycle 1..255 | the engine action id allocation |
| M4-006 | M4-control | COMPATIBILITY_POLICY | Wheel confirmation tolerance 35 percent / 5 mm per s | not applicable: the engine does not confirm wheel speeds this way |
| M4-007 | M4-control | COMPATIBILITY_POLICY | StopAll sends StopAllMotors and a zero DriveWheels | the engine stop path |
| M4-009 | M4-control | EQUIVALENT_IMPLEMENTATION | Cube tracking from ObjectAvailable and ObjectConnectionState | HandleActiveObjectAvailable 0x0053391C, HandleActiveObjectConnectionState 0x00533B3C, HandleActiveObjectMoved 0x00533E30 and HandleObjectPowerLevel 0x00537130, read |
| M5-018 | M5-animation | EQUIVALENT_IMPLEMENTATION | How many frames one wall-clock tick streams | the engine streams to the audio budget on every update regardless of the clock (UpdateStream 0x0057C84C) |
| M5-020 | M5-animation | COMPATIBILITY_POLICY | Expressions helper faces | not applicable |
| M6-002 | M6-wwise-bank | EQUIVALENT_IMPLEMENTATION | Vorbis rebuild with external codebooks and granule computation | the Wwise Vorbis packing; no runtime in the package to check against |
| M6-004 | M6-wwise-bank | EQUIVALENT_IMPLEMENTATION | Resampling to the robot rate is a band-limited windowed sinc, not Audiokinetic resampler | the Wwise runtime resampler, which does not ship in the APK. What was fixed here is a defect of this stack, not a reproduction of theirs: nearest-sample decimation aliases, and no competent resampler does |
| M7-015 | M7-behaviour | EQUIVALENT_IMPLEMENTATION | Pick-up falls back to the raw status flag until the off-treads classifier is enabled | Robot::CheckAndUpdateTreadsState 0x00511E00, which is implemented and used once calibration is reported |
| M8-009 | M8-framework | COMPATIBILITY_POLICY | Behaviour scope undo order | the engine Smart* destructor order |
| M8-010 | M8-framework | COMPATIBILITY_POLICY | Behaviour inventory classifier rules | not applicable: this is bookkeeping, not robot behaviour |
| M9-011 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | The render goes through the effect chain the robot bus carries, not a local peak normalisation | libcozmoEngine.so for the routing and Init.bnk for the chain and its parameters |
| M9-016 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A song is rendered as it plays, a block at a time, about 66 ms ahead of the clock | BehaviorSinging::UpdateInternal 0x005EF0C8 posts Cozmo_Singing_Vibrato every tick, and the bank binds that parameter to the depth of the LFO on the sampler pitch. Continuous is what the engine does, and this reproduces it. The 66 ms is this stack's choice, and it is small against the engine's own: UpdateAmountToSend 0x0057C6F0 lets the engine run up to 14 audio frames ahead of what the robot has played, which at 744 samples and 22320 Hz is 467 ms of audio already committed before it is heard. A parameter cannot reach audio either stack has already sent |
| M9-018 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A Stop action ends the streaming song when its target is the Play target or an ancestor | the shipped bank. The three tempo events play targets 914766641, 139286641 and 602865028, and Stop__Robot_VO__Cozmo_Singing_Stop holds three action-type-1 actions targeting exactly those three, so every stop the product can post is a direct hit on the container that is playing |
| M9-020 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A clip plays its source from BeginTrim for its length; a note still held at the clip end is released there | the shipped clip fields, which are read exactly. Every clip in every bank has PlayAt 0 and BeginTrim 0, so the start of the window is never moved; the end of it is what makes a twelve-second song out of a MIDI source minutes long, and is far from decorative. The one part of the rule that is a runtime judgement - what becomes of a note still held at the end - reaches two notes in the whole product, and the segment ends at the same instant, so what they would have sounded past it is outside the rendered song under either reading |
| M9-027 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | Robot_Bus_Eq_HiLowPass low-pass at 14298 Hz is above Nyquist for the robot's rate | Init.bnk gives 14298 Hz; AnimConstants::AUDIO_SAMPLE_RATE gives 22320 Hz, so Nyquist is 11160 Hz in the engine too |
| M10-005 | M10-derived | EQUIVALENT_IMPLEMENTATION | The off-treads debounce runs on the local clock | the engine debounces against its own base-station clock in milliseconds; the same quantity, a different source |
| M11-012 | M11-vision | COMPATIBILITY_POLICY | The nominal camera calibration stand-in | not applicable: the live path reads the robot own calibration and fails closed without it |
| M11-013 | M11-vision | COMPATIBILITY_POLICY | AllowUnconnectedObjects switch | the engine connected-object rule, which is implemented |
| M12-014 | M12-manipulation | EQUIVALENT_IMPLEMENTATION | The docking error signal's last two bytes are whatever was on the engine's stack | UpdateDockingErrorSignal 0x0063BE80 is the only builder and it never writes the struct bytes at +0x14 and +0x15. Nothing clears the struct either: it is a stack local at sp+0xa0 filled field by field, with no memclr and no constructor call, and Pack 0x007C0B26 reads both bytes and sends them |
| TOOL-001 | tools | COMPATIBILITY_POLICY | Conformance CLI pass and fail criteria | not applicable: the harness is not part of the app |
| TOOL-002 | tools | COMPATIBILITY_POLICY | The fake robot side answers place docks without a marker signal | not applicable: this is the test double, not the robot |
| TOOL-003 | tools | COMPATIBILITY_POLICY | The --nominal calibration override in the vision, manipulation and freeplay tools | the live path fails closed without a real calibration |

