# Overnight research queue (2026-10-13/14)

One request at a time, in this order. **Each request starts only after the previous answer file is complete and
pushed.** The research push rule (QUEUE.md, operator 2026-10-11) applies: commit and push only that request's own answer file
(the answer column below).

## When every request here is answered (manager, 2026-10-15)

When no request in the table below is still unanswered, don't stop. Pre-flight the builder's queue instead.

1. **Pick the jobs.** Take the next three open jobs in `re-analysis/jobs/QUEUE.md`: those with status `open`, and no file
   at `jobs/status/<job>.md`. Skip any job that already has a pre-flight file.
2. **Check every item of each job** against the checked sources it names:
   - **The citations:** every address, range, log text, constant and record the item relies on must appear in a checked
     row, meaning a manager-adopted `*-check.md` or `*-pass.md`, or a record's evidence. Find the row that supports each
     one.
   - **The gaps:** list each step the builder would have to mark MISSING, because no checked row covers it. Give the
     native address and what is unknown.
   - **The conflicts:** list anything in the job text that contradicts the checked rows or the current manifest. Quote
     both.
3. **Write the result** to `research/preflight/<job>.md`. Put a table first (item, supported or MISSING or conflict,
   citation), then the details.
4. **Push it** under the research push rule: commit and push only that file.
5. **Don't edit** the job, the manifest or any code. Pre-flight is read-only research (authority 6). The manager checks
   it and fixes the job.
6. **New requests come first.** If new requests have appeared in this file, answer those before any more pre-flight.

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
| OV44 | requests/overnight/20261014-ov44-m2-022.md (M2-022: local subscribers to tags 0x60/0x81; dispatch inside ActionList::Clear) | research/overnight/20261014-ov44-m2-022.md |
| OV45 | requests/overnight/20261014-ov45-m11-073.md (M11-073: can an unguarded GetParent caller see a root pose) | research/overnight/20261014-ov45-m11-073.md |
| OV46 | requests/overnight/20261014-ov46-m12-043.md (M12-043: GetBoundingQuad corner order; PyramidBase duplicates; StopHelperWithoutCallback) | research/overnight/20261014-ov46-m12-043.md |
| OV47 | requests/overnight/20261014-ov47-m12-044.md (M12-044: PreActionPose element escape analysis) | research/overnight/20261014-ov47-m12-044.md |
| OV48 | requests/overnight/20261014-ov48-m13-037.md (M13-037: TrackObjectAction selection and UpdateTracking tail) | research/overnight/20261014-ov48-m13-037.md |
| OV49 | requests/overnight/20261014-ov49-m11-042.md (M11-042: IsVisibleFrom, FindLocatedObjectHelper result, subscribers, null calibration) | research/overnight/20261014-ov49-m11-042.md |
| OV50 | requests/overnight/20261014-ov50-m11-078.md (M11-078: vision/world teardown leaves) | research/overnight/20261014-ov50-m11-078.md |
| OV51 | requests/overnight/20261014-ov51-m11-079.md (M11-079: quad-tree border consumer) | research/overnight/20261014-ov51-m11-079.md |
| OV52 | requests/overnight/20261014-ov52-m4-050.md (M4-050: libc++ __next_prime) | research/overnight/20261014-ov52-m4-050.md |
| OV53 | requests/overnight/20261014-ov53-m11-081.md (M11-081: ImagingPipeline exposure adjustment) | research/overnight/20261014-ov53-m11-081.md |
| OV54 | requests/overnight/20261014-ov54-m11-082.md (M11-082: getMemberNames order, LoadRobotConfigs failure) | research/overnight/20261014-ov54-m11-082.md |
| OV55 | requests/overnight/20261014-ov55-m8-023.md (M8-023: behaviour teardown recursive recipients) | research/overnight/20261014-ov55-m8-023.md |
| OV56 | requests/overnight/20261014-ov56-m8-024.md (M8-024: pending actions surviving to ~Robot) | research/overnight/20261014-ov56-m8-024.md |
| OV57 | requests/overnight/20261014-ov57-m11-083.md (M11-083: libc++_shared %f formatter) | research/overnight/20261014-ov57-m11-083.md |
| OV58 | requests/overnight/20261015-ov58-m15-049.md (M15-049: Lab experiment definitions: AnkiLab::Load (experiments.json) and the I) | research/overnight/20261015-ov58-m15-049.md |
| OV59 | requests/overnight/20261015-ov59-m15-050.md (M15-050: Lab assignments writeback: PossiblyWriteLabAssignmentsToRobot and Writ) | research/overnight/20261015-ov59-m15-050.md |
| OV60 | requests/overnight/20261015-ov60-m15-051.md (M15-051: ActivityBuildPyramid producers: UpdateChooserPhase and the Get*BlockID) | research/overnight/20261015-ov60-m15-051.md |
| OV61 | requests/overnight/20261015-ov61-m11-085.md (M11-085: HandleActiveObjectConnectionState disconnect tail: HandleDisconnectedF) | research/overnight/20261015-ov61-m11-085.md |
| OV62 | requests/overnight/20261015-ov62-m7-029.md (M7-029: BehaviorTrackLaser states, tracking/pounce and laser observation (ever) | research/overnight/20261015-ov62-m7-029.md |
| OV63 | requests/overnight/20261015-ov63-m11-087.md (M11-087: VisionSystem::IsExposureValid / IsGainValid bounds used by VisionCompo) | research/overnight/20261015-ov63-m11-087.md |
| OV64 | requests/overnight/20261015-ov64-m6-031.md (M6-031: Wwise Term dispatcher at shutdown: thread stop, bus teardown, memory-m) | research/overnight/20261015-ov64-m6-031.md |
| OV65 | requests/overnight/20261015-ov65-m6-032.md (M6-032: Hijack FX Term at Wwise shutdown reaching CozmoAudioController's destr) | research/overnight/20261015-ov65-m6-032.md |
| OV66 | requests/overnight/20261015-ov66-m6-033.md (M6-033: Audio stream hook and device teardown at shutdown) | research/overnight/20261015-ov66-m6-033.md |
| OV67 | requests/overnight/20261015-ov67-activatespark.md (M15-006: ActivateSpark handler, operator decision to expose spark requests) | research/overnight/20261015-ov67-activatespark.md |
| OV68 | requests/overnight/20261015-ov68-m11-088.md (M11-088: ObservableObject virtual +0x5C / +0x50 overrides per concrete class (m) | research/overnight/20261015-ov68-m11-088.md |
| OV69 | requests/overnight/20261015-ov69-m8-026.md (M8-026: AIWhiteboard::ProcessClearQuad: PossibleObjects erase predicate and Up) | research/overnight/20261015-ov69-m8-026.md |
| OV70 | requests/overnight/20261015-ov70-m9-037.md (M9-037: Wwise note-off remaining callees: holder look-ahead and pump order vs ) | research/overnight/20261015-ov70-m9-037.md |
| OV71 | requests/overnight/20261015-ov71-m9-036.md (M9-036: Wwise envelope remaining arithmetic: early-release (deadline before de) | research/overnight/20261015-ov71-m9-036.md |
| OV72 | requests/overnight/20261015-ov72-m9-035.md (M9-035: Wwise general parameter collector bodies (9EF258, 9FBE74, A11590)) | research/overnight/20261015-ov72-m9-035.md |
| OV73 | requests/overnight/20261015-ov73-m9-038.md (M9-038: Wwise live RTPC updates to modulator states: who calls state vt+0x08 () | research/overnight/20261015-ov73-m9-038.md |
| OV74 | requests/overnight/20261015-ov74-m12-047.md (M12-047: Behavior helper stop semantics (StopHelperWithoutCallback, ClearStackF) | research/overnight/20261015-ov74-m12-047.md |
| OV75 | requests/overnight/20261015-ov75-m12-048.md (M12-048: Candidate crash points in pre-action pose consumers: CrossBridge GetDo) | research/overnight/20261015-ov75-m12-048.md |
| OV76 | requests/overnight/20261015-ov76-m11-073.md (M11-073: Whether any unguarded GetParent caller can see a root pose in normal r) | research/overnight/20261015-ov76-m11-073.md |
| OV77 | requests/overnight/20261015-ov77-m13-038.md (M13-038: BehaviorAudioClient instances and lifetime: two clients (BehaviorManag) | research/overnight/20261015-ov77-m13-038.md |
| OV78 | requests/overnight/20261015-ov78-m11-004.md (M11-004: BlockWorld connected-object rule, moving and rotating gates, position-) | research/overnight/20261015-ov78-m11-004.md |
| OV79 | requests/overnight/20261015-ov79-m15-052.md (M15-052: Lab AutoActivate descendants and audience-tag qualification: ActivateE) | research/overnight/20261015-ov79-m15-052.md |
| OV80 | requests/overnight/20261015-ov80-m11-089.md (M11-089: InterestingEdge (type 9) map data class, MemoryMap::HasBorders callers) | research/overnight/20261015-ov80-m11-089.md |
| OV81 | requests/overnight/20261015-ov81-m11-090.md (M11-090: Vision/world teardown residue: std::function capture finalizers in the) | research/overnight/20261015-ov81-m11-090.md |
| OV82 | requests/overnight/20261015-ov82-m15-055.md (M15-055: Activity Update forwarding: ActivityFreeplay::Update to the current su) | research/overnight/20261015-ov82-m15-055.md |
| OV83 | requests/overnight/20261015-ov83-m15-056.md (M15-056: Cube disconnect consumers: ActivityFeeding and BehaviorGuardDog handle) | research/overnight/20261015-ov83-m15-056.md |
| OV84 | requests/overnight/20261015-ov84-m6-034.md (M6-034: Wwise Term recipients: the manager-list tail, head calls, allocator in) | research/overnight/20261015-ov84-m6-034.md |
| OV85 | requests/overnight/20261015-ov85-setmarkerasobserved.md (M11-013: SetMarkerAsObserved, unblocks B-M11-3) | research/overnight/20261015-ov85-setmarkerasobserved.md |
| OV86 | requests/overnight/20261016-ov86-m11-084.md (M11-084: Pose wrapper lifetimes in production: whether any flow leaves a child ) | research/overnight/20261016-ov86-m11-084.md |
| OV87 | requests/overnight/20261016-ov87-m8-027.md (M8-027: ProtoHandle subscriber-callable census: which callables the 8 ProtoHan) | research/overnight/20261016-ov87-m8-027.md |
| OV88 | requests/overnight/20261016-ov88-m9-034.md (M9-034: Wwise MIDI path boundary callees: A04384 prefix, owner vt+4, A70C84 (C) | research/overnight/20261016-ov88-m9-034.md |
| OV89 | requests/overnight/20261016-ov89-m6-035.md (M6-035: Wwise bus state +0x1BC transitions (what moves a bus from 1 back to 4)) | research/overnight/20261016-ov89-m6-035.md |
| OV90 | requests/overnight/20261016-ov90-m6-036.md (M6-036: Audio device storage cleanup at shutdown: 0x0096D624..0x0096FE6F, 0x00) | research/overnight/20261016-ov90-m6-036.md |
| OV91 | requests/overnight/20261016-ov91-m12-046.md (M12-046: Unattach path still unread: BlockWorld::DeleteLocatedObjects body, Che) | research/overnight/20261016-ov91-m12-046.md |
| OV92 | requests/overnight/20261016-ov92-m1-054.md (M1-054: CozmoEngine::Init before RobotManager::Init: the +0x10 Info, the three) | research/overnight/20261016-ov92-m1-054.md |
| OV93 | requests/overnight/20261016-ov93-m15-045.md (M15-045: AIComponent teardown recursive recipients: BehaviorHelper IHelper type) | research/overnight/20261016-ov93-m15-045.md |
| OV94 | requests/overnight/20261016-ov94-m9-042.md (M9-042: Wwise modulator-state residue: state insertion (V+0x34 store, 0x009DD1) | research/overnight/20261016-ov94-m9-042.md |
| OV95 | requests/overnight/20261016-ov95-m9-039.md (M9-039: Wwise MIDI residue: the 97D14C vt+0x70 / 981DBC branch, R8 init helper) | research/overnight/20261016-ov95-m9-039.md |
| OV96 | requests/overnight/20261016-ov96-m9-041.md (M9-041: Wwise parameter-collector residue: 9EF258 middle (SIS, factor, aux, bu) | research/overnight/20261016-ov96-m9-041.md |
| OV97 | requests/overnight/20261016-ov97-m6-037.md (M6-037: Wwise bus residue: the other bus-destroy contexts (0xA41A3C, 0xA423D8,) | research/overnight/20261016-ov97-m6-037.md |
| OV98 | requests/overnight/20261016-ov98-m6-038.md (M6-038: Wwise Term music-engine and variant: H3 music-engine Term (0x97D834 ->) | research/overnight/20261016-ov98-m6-038.md |
| OV99 | requests/overnight/20261016-ov99-m6-039.md (M6-039: Audio stream class behind the package-node dtor (node+0x34 vt+8 at 0x8) | research/overnight/20261016-ov99-m6-039.md |
| OV100 | requests/overnight/20261016-ov100-m11-091.md (M11-091: ObservableObject +0x50 (bounding quad) caller census, including the ma) | research/overnight/20261016-ov100-m11-091.md |
| OV101 | requests/overnight/20261016-ov101-m11-092.md (M11-092: BehaviorVisitInterestingEdge lifecycle and the libc++ sort helpers use) | research/overnight/20261016-ov101-m11-092.md |
| OV102 | requests/overnight/20261016-ov102-m15-060.md (M15-060: ActivityFreeplay put-down kick-out handler (RobotOffTreadsStateChanged) | research/overnight/20261016-ov102-m15-060.md |
| OV103 | requests/overnight/20261016-ov103-m15-061.md (M15-061: ActivityFeeding lifecycle beyond the cube-connection subscription, Set) | research/overnight/20261016-ov103-m15-061.md |
| OV104 | requests/overnight/20261016-ov104-m15-062.md (M15-062: Lab residue: NeedsState+8 at InitExperiments time, FindExperiment/Find) | research/overnight/20261016-ov104-m15-062.md |
| OV105 | requests/overnight/20261016-ov105-m15-063.md (M15-063: AIComponent teardown residue: concrete std::function and IHelper types) | research/overnight/20261016-ov105-m15-063.md |
| OV106 | requests/overnight/20261016-ov106-m11-084.md (M11-084: Pose wrapper lifetimes in production: whether any flow leaves a child ) | research/overnight/20261016-ov106-m11-084.md |
| OV107 | requests/overnight/20261016-ov107-m8-027.md (M8-027: ProtoHandle subscriber-callable census: which callables the 8 ProtoHan) | research/overnight/20261016-ov107-m8-027.md |
| OV108 | requests/overnight/20261016-ov108-m15-031.md (M15-031: ActivateHighLevelActivity, now in scope) | research/overnight/20261016-ov108-m15-031.md |
| OV109 | requests/overnight/20261016-ov109-m11-093.md (M11-093: Unowned FindLocatedIntersectingObjects callers: BehaviorLookAround::Tr) | research/overnight/20261016-ov109-m11-093.md |
| OV110 | requests/overnight/20261016-ov110-m11-084.md (M11-084: Pose wrapper lifetimes in production: whether any flow leaves a child ) | research/overnight/20261016-ov110-m11-084.md |
| OV111 | requests/overnight/20261016-ov111-m15-065.md (M15-065: BehaviorFeedingEat: the IFeedingListener callbacks (StartedEating, Eat) | research/overnight/20261016-ov111-m15-065.md |
| OV112 | requests/overnight/20261016-ov112-m15-066.md (M15-066: Order of the RobotOffTreadsStateChanged (E2G tag 53) subscribers: AIWh) | research/overnight/20261016-ov112-m15-066.md |
| OV113 | requests/overnight/20261016-ov113-m9-043.md (M9-043: Wwise modulator-state remainder: A1B3D8 bindings, 9DD950..9DE688 clean) | research/overnight/20261016-ov113-m9-043.md |
| OV114 | requests/overnight/20261016-ov114-m9-044.md (M9-044: Wwise MIDI remainder: A3FC4C (CC chase reader), A40238 (seek skip per ) | research/overnight/20261016-ov114-m9-044.md |
| OV115 | requests/overnight/20261016-ov115-m9-041.md (M9-041: Wwise parameter-collector residue: 9EF258 middle (SIS, factor, aux, bu) | research/overnight/20261016-ov115-m9-041.md |
| OV116 | requests/overnight/20261016-ov116-m6-042.md (M6-042: Wwise Term-time notifications and the engine destructor order: 0xA3599) | research/overnight/20261016-ov116-m6-042.md |
| OV117 | requests/overnight/20261016-ov117-m6-040.md (M6-040: Wwise bus remainder: 0xA55750 send gating, 0xA4B5D8/0xA4B93C send path) | research/overnight/20261016-ov117-m6-040.md |
| OV118 | requests/overnight/20261016-ov118-m6-041.md (M6-041: Wwise music stop tree at Term: 0x9854E0 beyond 0x985700, child vt+0x20) | research/overnight/20261016-ov118-m6-041.md |
| OV119 | requests/overnight/20261016-ov119-m6-043.md (M6-043: Wwise Term-path transitive callees and stream-device remainder) | research/overnight/20261016-ov119-m6-043.md |
| OV120 | requests/overnight/20261016-ov120-m15-067.md (M15-067: BehaviorGuardDog lifecycle: IsRunnable, Init, UpdateInternal pre-dispa) | research/overnight/20261016-ov120-m15-067.md |
| OV121 | requests/overnight/20261016-ov121-m11-084.md (M11-084: Pose wrapper lifetimes in production: whether any flow leaves a child ) | research/overnight/20261016-ov121-m11-084.md |
| OV122 | requests/overnight/20261016-ov122-m15-069.md (M15-069: R2E-phase synchronous action completions (census beyond 0x00512038 and) | research/overnight/20261016-ov122-m15-069.md |
| OV123 | requests/overnight/20261016-ov123-m9-046.md (M9-046: Wwise singing remainder; answered by manager-adopted Opus summary under the alternate filename) | research/overnight/20261016-ov123-answer.md |
| OV124 | requests/overnight/20261017-ov124-m1-055.md (M1-055: first broadcast population timing, procedural-name exclusion and EndOfMessage payload; current M1+M2 target) | research/overnight/20261017-ov124-m1-055.md |
| OV125 | requests/overnight/20261017-ov125-m1-056.md (M1-056: narrow: the state 1 -> 2 gate, SetEngineState effects, progress writers; trimmed 2026-10-17; current M1+M2 target) | research/overnight/20261017-ov125-m1-056.md |
| OV126 | requests/overnight/20261017-ov126-m1-startup-narrow.md (M1-054/M1-056/M2-020: narrow checks on UiMessageHandler::Init, InitInternal, the manager MessageHandler Init, SendSupportInfo, WifiFlashID; current M1+M2 target) | research/overnight/20261017-ov126-m1-startup-narrow.md |
| OV127 | requests/overnight/20261018-ov127-m1m2-settling-narrow.md (M1-061/M2-024/M1-054/M1-055/M1-056: LatencyMessage payload, missing Impl handlers, LoadRobotConfigs census, local E2G consumers; current M1+M2 target) | research/overnight/20261018-ov127-m1m2-settling-narrow.md |

**Skipped (SDK-only, out of scope):** M7-024, M8-018, M11-056, M15-032 (M15-031 is in scope since 2026-10-16).
