# Open fidelity gaps

Generated from `re-analysis/fidelity_manifest.json` by `re-analysis/tools/fidelity.py`.
Do not edit by hand: edit the manifest and regenerate, or the two will disagree.

Manifest of **207 records** over 16 subsystems.

| status | records | meaning |
| --- | ---: | --- |
| EXACT_SOURCE | 161 | Read from primary source and reproduced. The record names the address, asset or schema it was read from. |
| EQUIVALENT_IMPLEMENTATION | 14 | The native behaviour is known from primary source and this stack reaches the same observable effect by a different mechanism. The record names the difference, and the difference has to be one a listener, a viewer or the robot cannot tell apart. |
| RECOVERABLE_GAP | 2 | A behaviour-affecting decision whose answer plausibly exists in primary source that has not been read, or has been read too shallowly to settle it. The work outstanding is reverse engineering. |
| IMPLEMENTATION_GAP | 0 | The native behaviour is established from primary evidence, and the production implementation knowingly does something else. The work outstanding is building it. This is unfinished fidelity work, not a policy. |
| COMPATIBILITY_POLICY | 19 | A deliberate product or platform decision this stack intends to keep: offline tools, the test harness, PC-side plumbing, or a stand-in the operator has to ask for. Not a place to put fidelity work that is hard. |
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
| M4-control — Motion, sensors, lights and cubes | 9 | 0 | 0 | 0 | 0 | yes | yes |
| M5-animation — Animation clips, scheduler and face | 21 | 0 | 0 | 0 | 0 | yes | yes |
| M6-wwise-bank — Wwise bank reading and codecs | 7 | 0 | 0 | 0 | 0 | yes | yes |
| M7-behaviour — Idle, mood and reactions | 16 | 0 | 0 | 0 | 0 | yes | yes |
| M8-framework — Behaviour framework and scoring | 10 | 0 | 0 | 0 | 0 | yes | yes |
| M9-wwise-music — Wwise music, the MIDI sampler and singing | 27 | 0 | 0 | 6 | 1 | yes | yes |
| M10-derived — Derived robot state and reaction strategies | 9 | 0 | 0 | 0 | 0 | yes | yes |
| M11-vision — Markers, camera geometry and BlockWorld | 18 | 2 | 0 | 1 | 0 | no | yes |
| M12-manipulation — Docking, carrying and pre-action poses | 16 | 0 | 0 | 0 | 0 | yes | yes |
| M13-navigation — Planning, charger and block configurations | 12 | 0 | 0 | 0 | 0 | yes | yes |
| M14-faces — Face and pet pipeline | 7 | 0 | 0 | 1 | 0 | yes | yes |
| M15-freeplay — Needs, activities and freeplay | 12 | 0 | 0 | 0 | 0 | yes | yes |
| tools — Conformance CLI and offline tools | 4 | 0 | 0 | 0 | 0 | yes | yes |

## Still to read: every RECOVERABLE_GAP

Each of these is a question the original can answer and nobody has asked it yet.

### M11-vision — Markers, camera geometry and BlockWorld

**M11-005 — Corner extraction by line fits, the refinement and the PnP numerics** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/QuadDetector.cs`
* effect: a marker's corners land in slightly different places, so its pose does too
* rests on: the binarization, the component rules and the quad acceptance test are now the engine's (M11-018); the corner extraction and refinement are still local
* best authority: ComputeQuadrilateralsFromConnectedComponents 0x00892D70 and the corner-method switch at 0x00892E24, read; ExtractLineFitsPeaks 0x008A5DB8 and TraceNextExteriorBoundary 0x008C6B18 read to their structure and constants but not transcribed; ExtractLaplacianPeaks 0x008A75C8 not read
* evidence: The corner method is settled: the parameters' +0x28 is 1 (0x00875338) and the switch at 0x00892E24 sends 1 to ExtractLineFitsPeaks 0x008A5DB8 and 0 to ExtractLaplacianPeaks; a quad is kept only when that returns exactly four corners (0x00892E60). The boundary it works on comes from TraceNextExteriorBoundary 0x008C6B18.; This stack takes the four extreme points of the component's boundary instead, then refines each side to the sub-pixel edge with its own line fit. The engine's ExtractLineFitsPeaks is 6160 bytes of fixed-point code and has not been transcribed.; Its shape is read even though its arithmetic is not. ExtractLineFitsPeaks copies the boundary into an n-by-2 float matrix (0x008A5E96), smooths it with an OpenCV Gaussian whose sigma is the boundary length over 64 (0x3C800000 at 0x008A616C) and whose size is OpenCV's own inverse of that relation, ceil(((sigma - 0.8) / 0.3 + 1) * 2 + 1) forced odd (0x008A5EDE..0x008A5F1A), transposes the kernel and runs cv::filter2D at CV_32F with border type 4 (0x008A60B4); then cv::kmeans splits the boundary into four clusters - the four sides - (0x008A6490), cv::solve fits a line to each (0x008A68E0), and Quadrilateral<float>::ComputeClockwiseCorners orders the four intersections (0x008A6C0E). It also refuses to start unless the four initial corners are already there (the count test at 0x008A5E18).; The refinement parameters are already the engine's - 25 iterations (+0x48), 1.01 step growth (+0x3c), 0.005 minimum change (+0x54), 5.0 maximum (+0x50) - but the loop they drive is this stack's own.; The pose solve from the four corners (the PnP numerics) is likewise local.; Its clustering is deterministic, which decides whether this can be reproduced exactly at all: the cv::kmeans call at 0x008A6490 passes K = 4, criteria {COUNT|EPS, 15, 0.1}, attempts 1 and flags 1 - KMEANS_USE_INITIAL_LABELS - and the labels are seeded just above it by splitting the boundary into four equal arcs by index: [0, n/4) = 0, [n/4, n/2) = 1, [n/2, 3n/4) = 2, [3n/4, n) = 3 (0x008A62D8..0x008A63EE, with n < 4 falling through to the halves). So nothing in it is drawn at random.; The boundary it works on is a staircase contour of the component, not its pixel edge. TraceNextExteriorBoundary 0x008C6B18 takes the component's bounding box, builds four short arrays - the least and greatest y in each column and the least and greatest x in each row, filled from the runs at 0x008C6E68..0x008C6E8E - refuses the component if any column or row in the box is empty (0x008C6F70..0x008C6FC6), and then walks the four sides in turn, emitting a point per column or row and filling the vertical or horizontal gap between neighbours (0x008C6FF0 onwards) - four passes in turn: the columns left to right on the least y (0x008C6FF0), the rows on the greatest x (0x008C70A6), the columns right to left on the greatest y (0x008C7158) and the rows back up on the least x (0x008C7208).; What the Gaussian smooths is the boundary's derivative. The matrix handed to cv::filter2D is 1 by 3 and holds -0.5, 0, +0.5 (0xBF000000, 0, 0x3F000000 written at 0x008A602C..0x008A603E) - the central difference - so the filter2D turns the n-by-2 boundary into (dx, dy) per point. The manual convolution at 0x008A61C4 then smooths that pair with the Gaussian, wrapping round the ends, and normalises it: a unit tangent per boundary point, which is what the four clusters are clusters of.; The line fit is an ordinary least squares through cv::solve with DECOMP_SVD (flags 1 at 0x008A68DE): each cluster builds an A of rows [x, 1] - the 1.0 written at +4 of every row, 0x008A6846 and 0x008A6878 - and a B of the matching y, so the solution is the slope and intercept of that side.; Its output is integer corners. The four line intersections are copied into a Quadrilateral<float>, ordered by ComputeClockwiseCorners 0x008A6C0E, then rounded with a +/- 0.5 and clamped to the short range (0x008A6C12..0x008A6C24) - the list it fills is FixedLengthList<Quadrilateral<short>>. So the sub-pixel work is the separate refinement stage the parameters at +0x48..+0x54 drive, which is where this stack's own line fits sit.
* outstanding: the index bookkeeping of TraceNextExteriorBoundary's four passes, and how near-vertical clusters are handled in the least-squares fit; everything else in the chain is read - the staircase boundary's shape, the [-0.5, 0, 0.5] derivative, the Gaussian of sigma = n / 64, the four-arc initial labels, the kmeans settings, the [x 1] least squares and the integer rounding of the result

**M11-017 — The memory map's vision-derived content: the overhead edges** (live path)

* where: `cozmo-stack/src/Cozmo.Robot/Vision/MemoryMap.cs`
* effect: the map has no InterestingEdge or NotInterestingEdge regions, so a ray that should be blocked by the boundary of what has been explored is answered clear, and the two behaviours built on those edges have nothing to visit
* rests on: the obstacles (M14-007) and the robot's own passage are implemented; the overhead edges are not
* best authority: MapComponent::UpdateRobotPose 0x0067E224, read; OverheadEdgesDetector::Detect 0x006ABE34 (7620 bytes), VisionComponent::UpdateOverheadEdges 0x006553FC, MapComponent::ProcessVisionOverheadEdges 0x0067F7AC and AddVisionOverheadEdges 0x0067F814 (3724 bytes), FlagGroundPlaneROIInterestingEdgesAsUncertain 0x0067E50C, FlagQuadAsNotInterestingEdges 0x0067E6B0 and QuadTreeProcessor::FillBorder 0x00689FAC, not read
* evidence: The robot's own passage is now read and implemented. UpdateRobotPose 0x0067E224 does nothing while the robot is within 8 mm (0x41000000) and 0.349066 rad of where it last wrote itself (Pose3d::IsSameAs at 0x0067E27C); otherwise it halves the ProxObstacle markerless size, (10, 10, 50) from GetSizeByType, builds that square at the robot's pose and inserts it - ClearOfCliff (the type byte 2 at 0x0067E3C2) when nothing reports a cliff, Cliff with the robot's X axis as the data's direction (0x0067E342..0x0067E358) when something does.; The edges are what is left. InterestingEdge (9) and NotInterestingEdge (10) both block a drive in the mask at 0x00C67962, and BehaviorVisitInterestingEdge and BehaviorLookInPlaceMemoryMap are built on them, but what produces them is MapComponent::AddVisionOverheadEdges 0x0067F814 - which takes an OverheadEdgeFrame from the vision system's ground-plane processing, and this stack's vision front end does not produce one (M11-005).; The detector's four tuning constants are read even though its code is not: kOverheadEdgeCloseMaxLenForTriangle_mm 15 (0x00C8764C), kOverheadEdgeFarMaxLenForLine_mm 15, kOverheadEdgeFarMinLenForClearReport_mm 3 and kOverheadEdgeSegmentNoiseLen_mm 6. The work is OverheadEdgesDetector::Detect (7620 bytes) followed by MapComponent::AddVisionOverheadEdges (3724 bytes), so it is a subsystem rather than a correction.
* outstanding: the overhead-edge frames the vision system produces and the four MapComponent routines that turn them into edge regions

## Still to build: every IMPLEMENTATION_GAP

Each of these is a question already answered. The original's behaviour is established and this stack knowingly does something else, so the work outstanding is writing it, not reading.

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
| M12-014 | M12-manipulation | EQUIVALENT_IMPLEMENTATION | The docking error signal's last two bytes are whatever was on the engine's stack | UpdateDockingErrorSignal 0x0063BE80 is the only builder and it never writes the struct bytes at +0x14 and +0x15. Nothing clears the struct either: it is a stack local at sp+0xa0 filled field by field, with no memclr and no constructor call, and Pack 0x007C0B26 reads both bytes and sends them |
| TOOL-001 | tools | COMPATIBILITY_POLICY | Conformance CLI pass and fail criteria | not applicable: the harness is not part of the app |
| TOOL-002 | tools | COMPATIBILITY_POLICY | The fake robot side answers place docks without a marker signal | not applicable: this is the test double, not the robot |
| TOOL-003 | tools | COMPATIBILITY_POLICY | The --nominal calibration override in the vision, manipulation and freeplay tools | the live path fails closed without a real calibration |

