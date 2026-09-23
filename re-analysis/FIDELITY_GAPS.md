# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **220 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 166 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 19 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 2 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 1 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
| COMPATIBILITY_POLICY | 20 | A deliberate product or platform decision this stack intends to keep: offline tools, the test harness, PC-side plumbing, or a stand-in the operator has to ask for. Not a place to put fidelity work that is hard. |
| HARDWARE_ONLY | 4 | No shipped artifact can settle it; only a robot, or a recording of the stock app, can. |
| BLOCKED_EXTERNAL | 8 | The answer lies in third-party code or data that is not in the package (Omron OKAO, the Wwise runtime DSP, the Acapela text-to-speech engine). |

## Where each subsystem stands

Four separate things, because one word cannot carry them. **Source read** means nothing is left that
reading the original would settle. **Built** means nothing the original is known to do is knowingly
not done here. Neither says the behaviour is faithfully reproduced: the last two columns are what
remains after both, and they do not go away by working harder on this repository.

| subsystem | records | to read | to build | blocked externally | needs hardware | source read | built |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| M1-transport — UDP transport and reliability | 15 | 0 | 0 | 0 | 0 | yes | yes |
| M2-protocol — CLAD messages and protocol helpers | 7 | 0 | 0 | 0 | 0 | yes | yes |
| M3-device — Camera, display and audio device layer | 17 | 0 | 0 | 0 | 1 | yes | yes |
| M4-control — Motion, sensors, lights and cubes | 13 | 0 | 1 | 0 | 1 | yes | no |
| M5-animation — Animation clips, scheduler and face | 23 | 0 | 0 | 0 | 0 | yes | yes |
| M6-wwise-bank — Wwise bank reading and codecs | 7 | 0 | 0 | 0 | 0 | yes | yes |
| M7-behaviour — Idle, mood and reactions | 17 | 0 | 0 | 0 | 0 | yes | yes |
| M8-framework — Behaviour framework and scoring | 10 | 0 | 0 | 0 | 0 | yes | yes |
| M9-wwise-music — Wwise music, the MIDI sampler and singing | 27 | 0 | 0 | 6 | 1 | yes | yes |
| M10-derived — Derived robot state and reaction strategies | 9 | 0 | 0 | 0 | 0 | yes | yes |
| M11-vision — Markers, camera geometry and BlockWorld | 20 | 1 | 0 | 1 | 0 | no | yes |
| M12-manipulation — Docking, carrying and pre-action poses | 16 | 0 | 0 | 0 | 0 | yes | yes |
| M13-navigation — Planning, charger and block configurations | 15 | 1 | 0 | 0 | 0 | no | yes |
| M14-faces — Face and pet pipeline | 7 | 0 | 0 | 1 | 0 | yes | yes |
| M15-freeplay — Needs, activities and freeplay | 12 | 0 | 0 | 0 | 0 | yes | yes |
| tools — Conformance CLI and offline tools | 5 | 0 | 0 | 0 | 0 | yes | yes |

## Still to read: every RECOVERABLE_GAP

Each of these is a question the original can answer and nobody has asked it yet.

### M11-vision — Markers, camera geometry and BlockWorld

**M11-020 — Illumination normalisation of each marker's region before its corners are refined and it is decoded** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/MarkerDetector.cs`
* effect: the refinement and the decode read the raw image where the engine reads a locally normalised one, so contrast, the bright and dark levels and the refined corners can differ
* rests on: not reproduced: MarkerDetector refines and decodes on the camera image as it is
* best authority: DetectFiducialMarkers 0x00898760: when params+1 is set (Parameters::Initialize writes 0x101 at +0, so it is) each marker's bounding rectangle is cut out (Quadrilateral::ComputeBoundingRectangle 0x008990CE), box-filtered and subtracted and min-max normalised (cv::boxFilter 0x008993D2, cv::subtract 0x0089942C, cv::normalize 0x00899474; IlluminationNormalization in detectFiducialMarkers.cpp) before RefineCorners (0x00899528) and Extract (0x00899056) - not read in detail
* evidence: the per-marker loop 0x008990AA..0x008995A2; the IlluminationNormalization error string at 0x0089956A
* outstanding: read the normalisation: the filter size (computed with sqrtf and roundf at 0x00899304..0x0089937A), the subtraction and the normalise range, and which image RefineCorners and Extract are handed afterwards

### M13-navigation — Planning, charger and block configurations

**M13-014 — Knock over a stack: BehaviorKnockOverCubes has no verified fidelity record** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs`
* effect: the approach, the flip of the bottom block, the blind-flip fallback or the success and failure reactions differ from the engine
* rests on: the behaviour's flow is now the engine's and verified: the reach, the knock-over sequence, the retry and blind-flip rule, the NoPreActionPoses exit and the reaction. What is not reproduced is what DriveAndFlipBlockAction is told beyond the object: the local action takes only the object
* best authority: BehaviorKnockOverCubes 0x005C2EA0..0x005C3C00, its knock-over callback 0x005C3DCE, IBehavior::StartActing(action, function<void(Robot&)>) 0x005BE0E4 and its lambda 0x005BF8F4, IBehavior::Init 0x005BCB54 and ReadFromJson 0x005BBFB4, read; DriveAndFlipBlockAction 0x0055E208 not read past its arguments
* evidence: InitInternal 0x005C31A2 goes straight to the knock-over when IBehavior+0xD9 (alwaysStreamline, ReadFromJson 0x005BC216) or +0xD8 (set in IBehavior::Init 0x005BCCC2 from the spark state) is set; neither shipped config sets alwaysStreamline and freeplay is not sparked, so the reach runs; Reach 0x005C3254: TurnTowardsObjectAction(max pi) at the bottom block; then, only when the block's x in the robot frame plus 10 exceeds 85 (0x005C3354..0x005C336C), DriveStraightAction(x - 85) at 60 mm/s; then the reachForBlockTrigger lift-safe animation. The continuation TransitionToKnockingOverStack runs whatever the result (StartActing's lambda 0x005BF8F4 ignores it); Knock-over 0x005C34A8: a sequence of TurnTowardsObjectAction(max pi), DriveAndFlipBlockAction and WaitAction(0.5). The callback 0x005C3DCE: NoPreActionPoses (0x03000010) writes the block to AIWhiteboard+0x70 and ends; success goes to TransitionToPlayingReaction; a Retry-category result re-runs the knock-over while the attempt count at +0x140 is at most 1 and otherwise goes to the blind flip, counting up either way; anything else ends. The count is zeroed in InitializeMemberVars (0x005C3218); Blind flip 0x005C3840: FlipBlockAction with the pre-action check off and WaitAction(0.5), then the reaction; DriveAndFlipBlockAction is constructed with useApproachAngle false, approach angle 0, sayName false, a maximum turn towards a face of pi/2 on the first attempt and 0 on later ones or when streamlined (the table at 0x005C36BC), false, and a trailing float of 20.0 (0x005C352E..0x005C3548), with the say-name triggers 0xFD and 0xFE
* outstanding: what DriveAndFlipBlockAction 0x0055E208 does with the maximum face-turn angle and the trailing 20.0 it is given, and so what passing them would change; the local DriveAndFlipBlockAction takes neither. The readers of AIWhiteboard+0x70 were not traced

## Still to build: every IMPLEMENTATION_GAP

Each of these is a question already answered. The original's behaviour is established and this stack knowingly does something else, so the work outstanding is writing it, not reading.

### M4-control — Motion, sensors, lights and cubes

**M4-011 — The block pool is not persisted, and an RSSI tie is not broken in the engine's order** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/CubeConnections.cs`
* effect: after a restart the engine reconnects the cubes it pooled last session; this stack rediscovers from scratch. With two same-type cubes at equal RSSI a different cube may be chosen
* rests on: a fresh pool every session; a tie is broken by this stack's iteration order
* best authority: BlockFilter::Load / BlockFilter::Save (named, not transcribed); the libc++ unordered_map visit order behind GetClosestDiscoveredObjectsOfType 0x00518314
* evidence: documented as not reproduced in the CubeConnections class summary
* outstanding: not buildable from the evidence recorded: BlockFilter::Load 0x0061A2DC and BlockFilter::Save 0x0061B014 have never been transcribed, so the file the pool is kept in, its format and when it is read and written are not known; and the tie between two same-type cubes at one RSSI byte is decided by the libc++ unordered_map's bucket traversal order at Robot+0x47C, which depends on its bucket count and insertion and rehash history, none of which is recorded

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
| M4-013 | M4-control | HARDWARE_ONLY | Whether the robot honours StartMotorCalibration after its connection-time calibration | only a robot can show whether the request is honoured |

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
| M5-021 | M5-animation | EQUIVALENT_IMPLEMENTATION | The eye and its lids are filled by scanline, not by cv::fillConvexPoly | ProceduralFaceDrawer::DrawEye 0x005850E0, read |
| M6-002 | M6-wwise-bank | EQUIVALENT_IMPLEMENTATION | Vorbis rebuild with external codebooks and granule computation | the Wwise Vorbis packing; no runtime in the package to check against |
| M6-004 | M6-wwise-bank | EQUIVALENT_IMPLEMENTATION | Resampling to the robot rate is a band-limited windowed sinc, not Audiokinetic resampler | the Wwise runtime resampler, which does not ship in the APK. What was fixed here is a defect of this stack, not a reproduction of theirs: nearest-sample decimation aliases, and no competent resampler does |
| M7-015 | M7-behaviour | EQUIVALENT_IMPLEMENTATION | Pick-up falls back to the raw status flag until the off-treads classifier is enabled | Robot::CheckAndUpdateTreadsState 0x00511E00, which is implemented and used once calibration is reported |
| M8-004 | M8-framework | COMPATIBILITY_POLICY | Behaviours built in code carry a score of their own; the engine's default is zero | IBehavior::IBehavior 0x005BBB74 and IBehavior::EvaluateScoreInternal 0x005BEEC2, read |
| M8-008 | M8-framework | COMPATIBILITY_POLICY | The head recalibration wait: the engine has no timeout, this stack keeps a backstop | CalibrateMotorAction::CheckIfDone 0x00547D38 and IAction::IAction 0x00540C44, read |
| M8-009 | M8-framework | COMPATIBILITY_POLICY | Behaviour scope undo order | the engine Smart* destructor order |
| M8-010 | M8-framework | COMPATIBILITY_POLICY | Behaviour inventory classifier rules | not applicable: this is bookkeeping, not robot behaviour |
| M9-011 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | The render goes through the effect chain the robot bus carries, not a local peak normalisation | libcozmoEngine.so for the routing and Init.bnk for the chain and its parameters |
| M9-016 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A song is rendered as it plays, a block at a time, about 66 ms ahead of the clock | BehaviorSinging::UpdateInternal 0x005EF0C8 posts Cozmo_Singing_Vibrato every tick, and the bank binds that parameter to the depth of the LFO on the sampler pitch. Continuous is what the engine does, and this reproduces it. The 66 ms is this stack's choice, and it is small against the engine's own: UpdateAmountToSend 0x0057C6F0 lets the engine run up to 14 audio frames ahead of what the robot has played, which at 744 samples and 22320 Hz is 467 ms of audio already committed before it is heard. A parameter cannot reach audio either stack has already sent |
| M9-018 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A Stop action ends the streaming song when its target is the Play target or an ancestor | the shipped bank. The three tempo events play targets 914766641, 139286641 and 602865028, and Stop__Robot_VO__Cozmo_Singing_Stop holds three action-type-1 actions targeting exactly those three, so every stop the product can post is a direct hit on the container that is playing |
| M9-020 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | A clip plays its source from BeginTrim for its length; a note still held at the clip end is released there | the shipped clip fields, which are read exactly. Every clip in every bank has PlayAt 0 and BeginTrim 0, so the start of the window is never moved; the end of it is what makes a twelve-second song out of a MIDI source minutes long, and is far from decorative. The one part of the rule that is a runtime judgement - what becomes of a note still held at the end - reaches two notes in the whole product, and the segment ends at the same instant, so what they would have sounded past it is outside the rendered song under either reading |
| M9-027 | M9-wwise-music | EQUIVALENT_IMPLEMENTATION | Robot_Bus_Eq_HiLowPass low-pass at 14298 Hz is above Nyquist for the robot's rate | Init.bnk gives 14298 Hz; AnimConstants::AUDIO_SAMPLE_RATE gives 22320 Hz, so Nyquist is 11160 Hz in the engine too |
| M10-005 | M10-derived | EQUIVALENT_IMPLEMENTATION | The off-treads debounce runs on the local clock | the engine debounces against its own base-station clock in milliseconds; the same quantity, a different source |
| M11-005 | M11-vision | EQUIVALENT_IMPLEMENTATION | The sub-pixel corner refinement | VisionMarker::RefineCorners 0x0089FD98, VisionMarker::ComputeBrightDarkValues 0x0089F8E8, RefineQuadrilateral 0x008C55E0 and its corner helper 0x008C66C4, MarkerDetector::Parameters::Initialize 0x008752F8, DetectFiducialMarkers 0x00898760, read |
| M11-012 | M11-vision | COMPATIBILITY_POLICY | The nominal camera calibration stand-in | not applicable: the live path reads the robot own calibration and fails closed without it |
| M11-013 | M11-vision | COMPATIBILITY_POLICY | AllowUnconnectedObjects switch | the engine connected-object rule, which is implemented |
| M11-017 | M11-vision | EQUIVALENT_IMPLEMENTATION | The memory map's vision-derived content: the overhead edges | OverheadEdgesDetector::Detect 0x006ABE34, its construction in VisionSystem::VisionSystem 0x006B0120, GroundPlaneROI 0x004F7774 and its statics at 0x00C48F60, MapComponent::ProcessVisionOverheadEdges 0x0067F7AC, AddVisionOverheadEdges 0x0067F814, FlagQuadAsNotInterestingEdges 0x0067E6B0, FlagGroundPlaneROIInterestingEdgesAsUncertain 0x0067E50C with its lambda at 0x00680B54, MemoryMap::FillBorderInternal and QuadTreeProcessor::FillBorder 0x00689FAC, all read |
| M12-014 | M12-manipulation | EQUIVALENT_IMPLEMENTATION | The docking error signal's last two bytes are whatever was on the engine's stack | UpdateDockingErrorSignal 0x0063BE80 is the only builder and it never writes the struct bytes at +0x14 and +0x15. Nothing clears the struct either: it is a stack local at sp+0xa0 filled field by field, with no memclr and no constructor call, and Pack 0x007C0B26 reads both bytes and sends them |
| TOOL-001 | tools | COMPATIBILITY_POLICY | Conformance CLI pass and fail criteria | not applicable: the harness is not part of the app |
| TOOL-002 | tools | COMPATIBILITY_POLICY | The fake robot side answers place docks without a marker signal | not applicable: this is the test double, not the robot |
| TOOL-003 | tools | COMPATIBILITY_POLICY | The --nominal calibration override in the vision, manipulation and freeplay tools | the live path fails closed without a real calibration |
| TOOL-005 | tools | COMPATIBILITY_POLICY | The hardware acceptance campaign: how a check is judged, and what a result may change | not applicable: the harness is not part of the app. The rule that matters runs the other way - a hardware result never changes a fidelity record's status. A check that passes says the behaviour was observed; a check that fails is an investigation item, not a licence to tune a source-backed constant. M11-005 stays open whatever the vision check reports, and the charger's 20 x 27 mm marker geometry is not adjusted to make the mount succeed |
| M7-017 | M7-behaviour | EQUIVALENT_IMPLEMENTATION | The complete live-animation wire lifecycle | AnimationStreamer::UpdateLiveAnimation 0x0057D5F8, AnimationStreamer::Update 0x0057CE5C, InitStream 0x0057B674, UpdateStream 0x0057C84C, SendStartOfAnimation 0x0057C400, SendBufferedMessages 0x0057BF60, read |
| M5-022 | M5-animation | EQUIVALENT_IMPLEMENTATION | What the engine does when the robot disconnects during a streaming animation | Robot::SendMessage 0x0051349C, AnimationStreamer::SendBufferedMessages 0x0057BF60, UpdateStream 0x0057C84C, Update 0x0057CE5C, RobotManager::RemoveRobot 0x0052F1A4, Robot::~Robot 0x005110D4, AnimationStreamer::~AnimationStreamer 0x0057AF48, read |
| M5-023 | M5-animation | EQUIVALENT_IMPLEMENTATION | Native cancellation and emission sequencing of a cancelled streaming animation | AnimationStreamer::Abort 0x0057B3E0, SetStreamingAnimation 0x0057B174, InitStream 0x0057B674, Update 0x0057CE5C, read |

