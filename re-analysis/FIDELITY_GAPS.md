# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **210 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 161 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 15 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 2 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 1 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
| COMPATIBILITY_POLICY | 20 | A deliberate product or platform decision this stack intends to keep: offline tools, the test harness, PC-side plumbing, or a stand-in the operator has to ask for. Not a place to put fidelity work that is hard. |
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
| M2-protocol — CLAD messages and protocol helpers | 7 | 0 | 0 | 0 | 0 | yes | yes |
| M3-device — Camera, display and audio device layer | 17 | 0 | 0 | 0 | 1 | yes | yes |
| M4-control — Motion, sensors, lights and cubes | 11 | 1 | 1 | 0 | 0 | no | no |
| M5-animation — Animation clips, scheduler and face | 21 | 0 | 0 | 0 | 0 | yes | yes |
| M6-wwise-bank — Wwise bank reading and codecs | 7 | 0 | 0 | 0 | 0 | yes | yes |
| M7-behaviour — Idle, mood and reactions | 16 | 0 | 0 | 0 | 0 | yes | yes |
| M8-framework — Behaviour framework and scoring | 10 | 0 | 0 | 0 | 0 | yes | yes |
| M9-wwise-music — Wwise music, the MIDI sampler and singing | 27 | 0 | 0 | 6 | 1 | yes | yes |
| M10-derived — Derived robot state and reaction strategies | 9 | 0 | 0 | 0 | 0 | yes | yes |
| M11-vision — Markers, camera geometry and BlockWorld | 18 | 1 | 0 | 1 | 0 | no | yes |
| M12-manipulation — Docking, carrying and pre-action poses | 16 | 0 | 0 | 0 | 0 | yes | yes |
| M13-navigation — Planning, charger and block configurations | 12 | 0 | 0 | 0 | 0 | yes | yes |
| M14-faces — Face and pet pipeline | 7 | 0 | 0 | 1 | 0 | yes | yes |
| M15-freeplay — Needs, activities and freeplay | 12 | 0 | 0 | 0 | 0 | yes | yes |
| tools — Conformance CLI and offline tools | 5 | 0 | 0 | 0 | 0 | yes | yes |

## Still to read: every RECOVERABLE_GAP

Each of these is a question the original can answer and nobody has asked it yet.

### M4-control — Motion, sensors, lights and cubes

**M4-CUBE-002 — Complete discovery-to-connection lifecycle is only partially recovered** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Cubes.cs`
* effect: A plausible auto-connect or retry rule could select the wrong cube/slot or turn discovery off at the wrong time.
* rests on: Native sender and substantial BlockFilter control flow recovered, but no complete source-backed lifecycle has been ported or claimed.
* best authority: Native BlockFilter 0x00619A2C..0x0061B628, Robot::Update 0x00514174..0x00514236; Unity BlockPoolTracker and ConnectionFlowController.CubeConnectFlow.
* evidence: re-analysis/CUBE_CONNECTION.md enumerates recovered steps and remaining boundaries; dis_cube_connection.txt records bounded primary disassembly and direct/PLT call sites.; BlockFilter::Update 0x0061A79A is gated and uses a two-second cadence; UpdateConnecting 0x0061AA8C checks alternatives after five seconds, not unconditional resends.; GetClosestDiscoveredObjectsOfType 0x00518314 compares unsigned RSSI bytes <=150; slot allocation is first empty of five, not cube type.; Named direct/PLT SetAccessoryDiscovery search found generated serialization calls but no production sender; this does not exclude inline/indirect construction.
* outstanding: Establish official radio-discovery enable/disable lifetime through inline/indirect startup/configuration paths; resolve native automatic type-list data, equal-RSSI container order and persistent-pool cold-start initialization/linker-thunk paths into a reproducible complete policy. The sender alone does not settle these choices.

### M11-vision — Markers, camera geometry and BlockWorld

**M11-005 — The sub-pixel corner refinement** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/QuadDetector.cs`
* effect: a refined corner lands in a slightly different place than the engine would put it, so the marker pose does too
* rests on: the corner extraction is now the engine's (QuadCorners); what is still local is the refinement this stack applies afterwards
* best authority: ComputeQuadrilateralsFromConnectedComponents 0x00892D70, TraceNextExteriorBoundary 0x008C6B18, ExtractLineFitsPeaks 0x008A5DB8, Quadrilateral<float>::ComputeClockwiseCorners 0x008A1324 and IsQuadrilateralReasonable 0x00892B18, all read and implemented; VisionMarker::RefineCorners 0x0089FD98 and RefineQuadrilateral 0x008C55E0 (3980 bytes), not read
* evidence: The corner extraction is transcribed. TraceNextExteriorBoundary 0x008C6B18 builds its staircase contour from four extent arrays over the component's bounding box in four passes, dropping points past the 10000 the caller allocates (0x00892DA8) and failing on an empty row or column; ExtractLineFitsPeaks 0x008A5DB8 differentiates a Gaussian of sigma = length / 64 (0x3C800000, with OpenCV's size relation solved backwards at 0x008A5ED6), convolves it circularly with the boundary in double and normalises, seeds four equal arcs and runs cv::kmeans with KMEANS_USE_INITIAL_LABELS, one attempt, 15 iterations and epsilon 0.1 (0x008A6490) - so nothing is random - fits each cluster across its wider extent (the swapped flag at 0x008A6714), intersects all six pairs keeping only the four inside the image (0x008A6BE2), orders them by atan2 about their centroid ascending (0x008A1324) and rounds half away from zero into an s16 quad.; So is the acceptance test and the order the quad is kept in: the clockwise corners permuted 0, 3, 1, 2 (0x00892E88), IsQuadrilateralReasonable's four rules on that order, and the middle pair exchanged when it reports the other winding (0x00892B7E and 0x00892F04).; What is left is the refinement. DetectFiducialMarkers calls VisionMarker::RefineCorners 0x0089FD98, which calls RefineQuadrilateral 0x008C55E0 - 3980 bytes, not read - and this stack instead walks each side of the quad looking for the strongest dark-to-light step along its normal and re-fits the four lines, up to 25 times. On rendered markers the corners land within 1 px of truth and PnP reprojection is 0.1 to 0.4 px, so the difference is small, but it is a difference and the source for it exists.; What it is, is read even though it is not transcribed: VisionMarker::RefineCorners 0x0089FD98 takes the marker's own bright and dark values (ComputeBrightDarkValues) and its homography, and RefineQuadrilateral 0x008C55E0 runs a Gauss-Newton refinement of that homography - samples laid along the four edges of the canonical square either side of each one, an offset scaled by (bright - dark) and the longer diagonal, Matrix::Multiply and MakeSymmetric into a normal-equation system, SolveLeastSquaresWithCholesky, and Invert3x3 to apply the update - under the parameters this stack already reads off Parameters::Initialize: 25 iterations, a 0.005 minimum change, a 5.0 maximum, and the 1.01 step growth.; The pose the refined corners feed is a separate matter and not a gap: the engine hands cv::solvePnP four corners inside Camera::ComputeObjectPoseHelper, and PoseEstimation.cs solves the same problem with its own numerics - a different mechanism for the same answer, and it recovers a cube pose to under a millimetre and half a degree in test.; How far it was traced, so the next pass does not start over: RefineQuadrilateral's arguments land at [sp+0x380] onwards - the second scale point, an int, bright, dark, the sample count, two more floats, the out quad, the out homography and the memory stack. It takes the longer of the two diagonals (0x008C5620), allocates five 1-by-N float arrays with N = 8 * ceil(count / 8), and fills them in four near-identical blocks, one per edge of the canonical square, with an offset of (dark - bright) / 255 * 0.5 * (diagonal / sqrt(2)) either side of the edge (0x008C583C..0x008C585E). Then the iteration: Matrix::Multiply and MakeSymmetric into a normal-equation system, SolveLeastSquaresWithCholesky, Invert3x3 and the homography update. What is not settled is the sampling and the Jacobian in detail, and transcribing those from the register plumbing alone would be guesswork rather than recovery.
* outstanding: RefineQuadrilateral 0x008C55E0: the sampling pattern along each edge and the Jacobian of the homography update. Its arguments, its offset formula and its solver are read

## Still to build: every IMPLEMENTATION_GAP

Each of these is a question already answered. The original's behaviour is established and this stack knowingly does something else, so the work outstanding is writing it, not reading.

### M4-control — Motion, sensors, lights and cubes

**M4-CUBE-001 — Missing outbound cube slot request and incorrect SetPropSlot Boolean** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Cubes.cs`
* effect: Check B hears advertisements but never asks the robot to connect; the generated Boolean cannot encode all five native slots.
* rests on: Control.Cubes enables discovery and waits; CozmoCubes has no request sender. Protocol field connect was borrowed from PyCozmo.
* best authority: Native Robot::ConnectToRequestedObjects 0x00514A70 and SetPropSlot::Pack 0x007A11A4; caller Robot::Update 0x00514236.
* evidence: re-analysis/CUBE_CONNECTION.md and re-analysis/disassembly/dis_cube_connection.txt; 0x00514BB4..0x00514BCE: requested factory ID followed by slot index 0..4; reliable=true, hot=false. 0x00514C34..0x00514C58: factory ID zero disconnects the indexed slot.; 0x005173EA..0x00517482 queues changed slot requests; 0x005179C0 confirms connection from robot reports.
* outstanding: After the complete lifecycle gate in M4-CUBE-002 is settled, correct the canonical protocol field to a slot byte and implement the recovered pool/request/state path. Do not insert an advertisement-to-Connect=true shortcut.

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
| M4-009 | M4-control | EQUIVALENT_IMPLEMENTATION | Incoming cube advertisement, connection-report and telemetry tracking only | HandleActiveObjectAvailable 0x0053391C, HandleActiveObjectConnectionState 0x00533B3C, HandleActiveObjectMoved 0x00533E30 and HandleObjectPowerLevel 0x00537130, read |
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
| M11-012 | M11-vision | COMPATIBILITY_POLICY | The nominal camera calibration stand-in | not applicable: the live path reads the robot own calibration and fails closed without it |
| M11-013 | M11-vision | COMPATIBILITY_POLICY | AllowUnconnectedObjects switch | the engine connected-object rule, which is implemented |
| M11-017 | M11-vision | EQUIVALENT_IMPLEMENTATION | The memory map's vision-derived content: the overhead edges | OverheadEdgesDetector::Detect 0x006ABE34, its construction in VisionSystem::VisionSystem 0x006B0120, GroundPlaneROI 0x004F7774 and its statics at 0x00C48F60, MapComponent::ProcessVisionOverheadEdges 0x0067F7AC, AddVisionOverheadEdges 0x0067F814, FlagQuadAsNotInterestingEdges 0x0067E6B0, FlagGroundPlaneROIInterestingEdgesAsUncertain 0x0067E50C with its lambda at 0x00680B54, MemoryMap::FillBorderInternal and QuadTreeProcessor::FillBorder 0x00689FAC, all read |
| M12-014 | M12-manipulation | EQUIVALENT_IMPLEMENTATION | The docking error signal's last two bytes are whatever was on the engine's stack | UpdateDockingErrorSignal 0x0063BE80 is the only builder and it never writes the struct bytes at +0x14 and +0x15. Nothing clears the struct either: it is a stack local at sp+0xa0 filled field by field, with no memclr and no constructor call, and Pack 0x007C0B26 reads both bytes and sends them |
| TOOL-001 | tools | COMPATIBILITY_POLICY | Conformance CLI pass and fail criteria | not applicable: the harness is not part of the app |
| TOOL-002 | tools | COMPATIBILITY_POLICY | The fake robot side answers place docks without a marker signal | not applicable: this is the test double, not the robot |
| TOOL-003 | tools | COMPATIBILITY_POLICY | The --nominal calibration override in the vision, manipulation and freeplay tools | the live path fails closed without a real calibration |
| TOOL-005 | tools | COMPATIBILITY_POLICY | The hardware acceptance campaign: how a check is judged, and what a result may change | not applicable: the harness is not part of the app. The rule that matters runs the other way - a hardware result never changes a fidelity record's status. A check that passes says the behaviour was observed; a check that fails is an investigation item, not a licence to tune a source-backed constant. M11-005 stays open whatever the vision check reports, and the charger's 20 x 27 mm marker geometry is not adjusted to make the mount succeed |

