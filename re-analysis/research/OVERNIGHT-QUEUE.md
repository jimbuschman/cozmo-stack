# Overnight research queue (2026-10-13/14)

One request at a time, in this order. **Each request starts only after the previous answer file is complete and
pushed.** The research push rule (QUEUE.md, operator 2026-10-11) applies: commit and push only that request's own answer file
(the answer column below).

The first three are the requests already written:

| # | request | answer |
| --- | --- | --- |
| P1 | requests/20261013-quadtree-rest.md (M11-048) | research/20261013-quadtree-rest.md |
| P2 | requests/20261013-small-reads-1.md (M11-072, M11-073) | research/20261013-small-reads-1.md |
| P3 | requests/20261013-small-reads-2.md (M15-044, M2-022) | research/20261013-small-reads-2.md |
| OV01 | requests/overnight/20261014-ov01-m12-027.md (M12-027: The lift pose's per-tick update and the BroadcastObjectPoseChanged chain) | research/overnight/20261014-ov01-m12-027.md |
| OV02 | requests/overnight/20261014-ov02-m12-028.md (M12-028: The behaviour-owned PlaceRelObjectParameters producers) | research/overnight/20261014-ov02-m12-028.md |
| OV03 | requests/overnight/20261014-ov03-m12-034.md (M12-034: Unread dock-action handler bodies and the ramp/charger validity overrides) | research/overnight/20261014-ov03-m12-034.md |
| OV04 | requests/overnight/20261014-ov04-m12-038.md (M12-038: The PreActionPose+0x14 height tolerance reader) | research/overnight/20261014-ov04-m12-038.md |
| OV05 | requests/overnight/20261014-ov05-m12-039.md (M12-039: ComputePlacementApproachAngle 0x005504A0 and the X/Y angle functions) | research/overnight/20261014-ov05-m12-039.md |
| OV06 | requests/overnight/20261014-ov06-m12-040.md (M12-040: Carrying/docking lifetime after preceding control abort) | research/overnight/20261014-ov06-m12-040.md |
| OV07 | requests/overnight/20261014-ov07-m13-021.md (M13-021: Unread MovementComponent and TrackLayerComponent bodies next to the turn and hea) | research/overnight/20261014-ov07-m13-021.md |
| OV08 | requests/overnight/20261014-ov08-m13-023.md (M13-023: The remaining unread pieces of the footprint path (corner-list writers, cv::Rodr) | research/overnight/20261014-ov08-m13-023.md |
| OV09 | requests/overnight/20261014-ov09-m13-029.md (M13-029: Path/planner abort and destruction) | research/overnight/20261014-ov09-m13-029.md |
| OV10 | requests/overnight/20261014-ov10-m11-042.md (M11-042: Unread bodies next to the BlockWorld observation path) | research/overnight/20261014-ov10-m11-042.md |
| OV11 | requests/overnight/20261014-ov11-m11-047.md (M11-047: The quad tree's border walk (FindBorders, AddBorderWaypoint, FillBorder)) | research/overnight/20261014-ov11-m11-047.md |
| OV12 | requests/overnight/20261014-ov12-m11-052.md (M11-052: RobotStateHistory::GetRawStateAt interpolation and HistRobotState::Interpolate) | research/overnight/20261014-ov12-m11-052.md |
| OV13 | requests/overnight/20261014-ov13-m11-055.md (M11-055: Vision/history/world/pose/map lifetime recipients) | research/overnight/20261014-ov13-m11-055.md |
| OV14 | requests/overnight/20261014-ov14-m11-057.md (M11-057: LimitedExposure: the vision system's reader of the exposure limit (M4-017 E9)) | research/overnight/20261014-ov14-m11-057.md |
| OV15 | requests/overnight/20261014-ov15-m11-058.md (M11-058: cv::Exception from the image decode: where it is caught and what happens to the ) | research/overnight/20261014-ov15-m11-058.md |
| OV16 | requests/overnight/20261014-ov16-m11-059.md (M11-059: VisionComponent::Init and VisionSystem::Init: the config reads, IsInitialized an) | research/overnight/20261014-ov16-m11-059.md |
| OV17 | requests/overnight/20261014-ov17-m11-060.md (M11-060: VisionSystem::Update's application of the pending SetNextCameraParams to the cur) | research/overnight/20261014-ov17-m11-060.md |
| OV18 | requests/overnight/20261014-ov18-m8-015.md (M8-015: ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown) | research/overnight/20261014-ov18-m8-015.md |
| OV19 | requests/overnight/20261014-ov19-m8-016.md (M8-016: Behavior-container destruction and virtual children) | research/overnight/20261014-ov19-m8-016.md |
| OV20 | requests/overnight/20261014-ov20-m8-017.md (M8-017: Go-to-sleep compound execution ordering and failure propagation) | research/overnight/20261014-ov20-m8-017.md |
| OV21 | requests/overnight/20261014-ov21-m8-019.md (M8-019: BehaviorManager::RequestCurrentBehaviorEndImmediately, as HandleRobotStopped cal) | research/overnight/20261014-ov21-m8-019.md |
| OV22 | requests/overnight/20261014-ov22-m8-022.md (M8-022: LoadDasBlacklistedAnimationTriggers and its configuration source) | research/overnight/20261014-ov22-m8-022.md |
| OV23 | requests/overnight/20261014-ov23-m10-014.md (M10-014: BlockFilter and gyro-drift lifetime effects) | research/overnight/20261014-ov23-m10-014.md |
| OV24 | requests/overnight/20261014-ov24-m10-015.md (M10-015: RobotGyroDriftDetector::DetectGyroDrift and DetectBias on each RobotState) | research/overnight/20261014-ov24-m10-015.md |
| OV25 | requests/overnight/20261014-ov25-m15-027.md (M15-027: Disconnect persistence, ForceUpdate and AI/progression/inventory destruction) | research/overnight/20261014-ov25-m15-027.md |
| OV26 | requests/overnight/20261014-ov26-m15-028.md (M15-028: App telemetry/context/member lifetime recipients) | research/overnight/20261014-ov26-m15-028.md |
| OV27 | requests/overnight/20261014-ov27-m15-029.md (M15-029: Repair need mutation and random part-damage contract) | research/overnight/20261014-ov27-m15-029.md |
| OV28 | requests/overnight/20261014-ov28-m15-030.md (M15-030: Needs binary32 exceptional-value propagation) | research/overnight/20261014-ov28-m15-030.md |
| OV29 | requests/overnight/20261014-ov29-m15-034.md (M15-034: ProgressionUnlockComponent's NV read callback (tag 0x182000) and its sink) | research/overnight/20261014-ov29-m15-034.md |
| OV30 | requests/overnight/20261014-ov30-m15-035.md (M15-035: InventoryComponent's NV read callback (tag 0x195000) and its sink) | research/overnight/20261014-ov30-m15-035.md |
| OV31 | requests/overnight/20261014-ov31-m15-036.md (M15-036: The Lab read: ReadLabAssignmentsFromRobot (tag 0x196000) and its callback into C) | research/overnight/20261014-ov31-m15-036.md |
| OV32 | requests/overnight/20261014-ov32-m15-037.md (M15-037: ActivityBuildPyramid cube-light modifiers and tracker lifecycle (the live modifi) | research/overnight/20261014-ov32-m15-037.md |
| OV33 | requests/overnight/20261014-ov33-m14-013.md (M14-013: Pet/face-world and speech lifetime recipients) | research/overnight/20261014-ov33-m14-013.md |
| OV34 | requests/overnight/20261014-ov34-m7-022.md (M7-022: DockingTestSimple developer-test state machine is not exhaustively recovered) | research/overnight/20261014-ov34-m7-022.md |
| OV35 | requests/overnight/20261014-ov35-m7-023.md (M7-023: Mood destruction with already-null ActionList dependency) | research/overnight/20261014-ov35-m7-023.md |
| OV36 | requests/overnight/20261014-ov36-m7-025.md (M7-025: BehaviorTrackLaser's read of VisionComponent+0x32A (the EnableColorImages flag) ) | research/overnight/20261014-ov36-m7-025.md |
| OV37 | requests/overnight/20261014-ov37-m6-029.md (M6-029: Context +0xC AudioMultiplexer teardown (CozmoAudioController, AudioEngineControl) | research/overnight/20261014-ov37-m6-029.md |
| OV38 | requests/overnight/20261014-ov38-m9-013.md (M9-013: Whether Wwise routes MIDI notes into the singing sampler get-in branch) | research/overnight/20261014-ov38-m9-013.md |
| OV39 | requests/overnight/20261014-ov39-m9-014.md (M9-014: Whether MIDI note velocity implicitly changes voice level) | research/overnight/20261014-ov39-m9-014.md |
| OV40 | requests/overnight/20261014-ov40-m9-024.md (M9-024: Whether the note-off envelope stops the voice it is attached to) | research/overnight/20261014-ov40-m9-024.md |
| OV41 | requests/overnight/20261014-ov41-m9-025.md (M9-025: The exact waveform produced by the Wwise LFO between its extrema) | research/overnight/20261014-ov41-m9-025.md |
| OV42 | requests/overnight/20261014-ov42-m11-074.md (M11-074: memory-map producers and readers) | research/overnight/20261014-ov42-map-producers.md |
| OV43 | requests/overnight/20261014-ov43-m11-075.md (M11-075: memory-map broadcast to the game) | research/overnight/20261014-ov43-map-broadcast.md |

**Skipped (SDK-only, out of scope):** M7-024, M8-018, M11-056, M15-031, M15-032.
