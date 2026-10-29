# AcknowledgeObject, one whole behaviour: read (extractor, 2026-10-29)

Read-only. Binary resources/lib/armeabi-v7a/libcozmoEngine.so 3.4.0-1204. Every address below was disassembled from
the .so (capstone; PLT stubs resolved through their GOT relocation, ARM veneers 0x8CCxxx resolved by their literal).
Records quoted from re-analysis/fidelity_manifest.json as read 2026-10-29. AO = BehaviorAcknowledgeObject,
ST = ReactionTriggerStrategyObjectPositionUpdated, PU = ReactionTriggerStrategyPositionUpdate (base).

## Path table

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| C1 | factory | class 0x3B: new(0x168), ctor PLT 0x4AFF7C (-> 0x602FA4), shared_ptr | 0x0059D0D2 mov.w r0,#0x168; 0x0059D0E2 blx 0x4aff7c; 0x0059D0EC blx 0x4aff88 | M7-018 | EXACT_SOURCE |
| C2 | ctor fields | IBehavior C2(robot, json) (0x00602FB0); +0x11C = 0x23F (AnimationTrigger Count, sentinel); +0x120 Radians 0x3F490FDB (pi/4, MaxTurnAngle); +0x128 and +0x130 Radians 0x3DB2B8C2 (5 deg, Pan/Tilt tolerance); +0x13C ObjectID vptr, +0x140 = -1 (current target); +0x138 = 2 (NumImagesToWaitFor); +0x148 = 0 (begin flag), +0x149 = 1, +0x14A = 0; set of int targets at +0x14C (size +0x154); set of IReactToObjectListener pointers at +0x158 (size +0x160) | 0x00602FB6..0x00602FBA movw r1,#0x23f / str [r4,#0x11c]; 0x00602FC6..0x00602FF2; 0x00602FF8..0x0060300C; 0x00603022..0x00603046 (strh 0x100 at +0x148, strb 0 at +0x14A, strd at +0x14C/+0x154/+0x15C); unity AnimationTrigger.cs:580 Count = 575 | NEW | EXACT_SOURCE |
| C3 | ghost object | new(0xFC) Block_Cube1x1 with ObjectType 4 = Block_LIGHTCUBE_GHOST (FUN_004E772C); +0x144 = its ObservableObject base; ghost+0x54 = [[robot]+0x24] (VizManager, debug viz); ghost.InitPose(empty-named Pose3d, PoseState 2). Never added to BlockWorld by this class | 0x00603010..0x0060304A; 0x004E772C..0x004E778A (str r6,[r4,#0xf0]; literal 0x004E7740 -> GOT reloc _ZTTN4Anki5Cozmo13Block_Cube1x1E, 0x004E7776 -> _ZTVN4Anki5Cozmo13Block_Cube1x1E); 0x00603056..0x00603060; 0x00603062..0x00603086 (InitPose, movs r2,#2); ObjectType.cs:10; obj+0x54 = VizManager per inventory/M13-navigation.md:2663 | NEW | EXACT_SOURCE (the +0x54 no-effect rests on the M13 note) |
| C4 | LoadConfig | ReactionAnimGroup -> +0x11C (GetValueOptional, AnimationTrigger overload); MaxTurnAngle_deg -> +0x120, PanTolerance_deg -> +0x128, TiltTolerance_deg -> +0x130 (GetAngleOptional, degrees=true; debug IBehaviorPoseBasedAcknowledgement.LoadConfig.Set* when present); NumImagesToWaitFor -> +0x138 (GetValueOptional int; debug). Missing keys keep C2 defaults | 0x00603118..0x0060334A; key strings 0x00603390, 0x006033A4, 0x00603408, 0x00BF72CC, 0x00BF731C; PLT 0x4AD39C, 0x4B6264, 0x4A61F0 (GOT relocs named); movs r3,#1 at 0x0060316C/0x006031EC/0x00603270 | M7-018; NEW (keys, defaults) | EXACT_SOURCE |
| C5 | shipped config | ReactionAnimGroup AcknowledgeObject (= AnimationTrigger 3), NumImagesToWaitFor 2; angles absent -> 45, 5, 5 deg | behaviors/reactions/acknowledgeObject.json:5-6; AnimationTrigger.cs:8 | M7-018 | EXACT_SOURCE |
| C6 | anim group | AcknowledgeObject -> ag_reacttoblock_react_short (head-angle windows) | animationGroupMaps/AnimationTriggerMap.json:16-17; animationGroups/ReactToBlock/ag_reacttoblock_react_short.json | M8-021 / M5 group choice | cited, not re-traced |
| T1 | map | ObjectPositionUpdated (8) -> AcknowledgeObject | reactionTrigger_behavior_map.json:82-83 | M7-018 / M10-003 | EXACT_SOURCE |
| T2 | strategy ctor | PU C2 with trigger 8; +0x54 = 0x39; +0x50 = listener subobject (vptr = ST vtable + 0x48); subscribes E2G tags | 0x00611054..0x006110CC (movs r0,#8 0x00611072; strb [r4,#0x54] 0x00611094; str r1,[r4,#0x50] 0x006110A4) | M10-003 (M10-derived.md App. C 4a, 4i) | EXACT_SOURCE |
| T3 | recording | tag 0x44 only with a current behaviour whose class != ST+0x54 (so nothing is recorded while AO runs); families LightCube, Block; carried/docking id records as reacted; 80 mm / 45 deg / 600000 ms | 0x0061153C..0x00611582; 0x006114A0..0x0061151E; 0x00612168..0x00612B22 | M10-003 (App. C 4a-4l) | per M10 inventory |
| T4 | AddListener | BehaviorThatStrategyWillTriggerInternal: behaviour vslot +0x38 (AO::AddListener, vtable reloc at 0x0102B9B0) with ST+0x50; ST+0x54 = behaviour class (+0x64). AddListener inserts into +0x158 | 0x00611674..0x00611690; 0x006042D4..0x006042EA (blx 0x4b62d0, tree emplace_unique of IReactToObjectListener pointers) | NEW | EXACT_SOURCE |
| T5 | listener slots | ST secondary vtable (0x0102D2A0+0x48): slot0 _ZThn80_ ReactedToID(Robot, int) -> PU::RobotReactedToId; slot1 _ZThn80_ ClearDesiredTargets(Robot) -> ST::ClearDesiredTargets (RobotReactedToId for every current desired target) | relocs 0x0102D2E8/0x0102D2EC; 0x00611696 subs r0,#0x50 -> veneer 0x8CC9AC -> PLT 0x4B6EDC; 0x00611702 -> 0x8CC9BC -> 0x4B738C; body 0x0061169C..0x006116EE | M10-003 (4h, 4l) bodies; NEW wiring | EXACT_SOURCE |
| T6 | STBI | false if carrying ([robot+0x284]+8 != -1), [[robot+0x280]+4] != 0, or robot+0x34A; false unless HasDesiredReactionTargets(robot, false); else GetDesiredReactionTargets -> FindBehaviorByIDAndDowncast AO (0x3B) -> AO+0x14C = targets (assign_multi into an int set: ascending id) -> IBehavior::IsRunnable | 0x006113E8..0x00611482 | M10-003 | EXACT_SOURCE |
| T7 | forced trigger | SetupForceTriggerBehavior: HasDesiredReactionTargets(robot, false) ? copy targets into AO+0x14C : warning | 0x00611148..0x006111E8 | NEW | EXACT_SOURCE body; reachability RECOVERABLE_GAP |
| T8 | trigger loop | CheckReactionTriggerStrategies; flags (1,1,0); locks that list ObjectPositionUpdated (placeOnGroundAction 0x00C55A21, SparkBehaviorDisables 0x00C65F90, behaviour lock tables) | M10-derived.md row ObjectPositionUpdated (1,1,0) | M10-004, M10-008, M8-001 | shared |
| R1 | IsRunnableBase | +0x20 slot (robot+0x355) base 0x005BF04C = 0; +0x24 slot (robot+0x34A) 0x0059EC12 = 0; +0x28 slot (carrying) class override 0x00604380 = 1: runnable while carrying (the AcknowledgeFace override 0x00602D20 returns 0) | 0x005BD864..0x005BD89C; vtable 0x0102B970 relative relocs 0x5BF04D, 0x59EC13, 0x604381; 0x00604380 movs r0,#1 | M8-001; NEW (override) | EXACT_SOURCE |
| R2 | IsRunnableInternal | +0x154 != 0 | 0x006042C8..0x006042D2 | M7-019 (M7 inventory row) | EXACT_SOURCE |
| I1 | InitInternal | +0x148 = 1; if [ghost+0x18] == -1: ghost vslot +0x24 = ObservableObject::SetID, debug BehaviorAcknowledgeObject.GhostObjectCreated; return 0 | 0x00603434..0x00603494; Block_Cube1x1 vtable 0x0101D4D0+0xC8 reloc ObservableObject5SetIDEv | M7-019 | EXACT_SOURCE |
| I2 | ghost id | SetID: ghost vslot +0x5C = 0x004E02AE (movs r0,#0: not unique) -> ObjectID::Set() at ghost+0x14: next value of the process-wide counter (0x008412D8..0x008412E6). Once, at the first Init of AO. Every later counter-allocated ObjectID is one higher than without it | 0x004EF468..0x004EF478; 0x004EF4EA..0x004EF4F4 (b.w 0x8CAB5C -> 0x4A55F0) | NEW | EXACT_SOURCE; that observed objects share this counter is the claim of M11-013 |
| U1 | UpdateInternal | if +0x148: clear, BeginIteration; tail IBehavior::UpdateInternal (veneer 0x8CC12C -> 0x4B2A90) | 0x00603524..0x00603546 | M7-019; M8-001 | EXACT_SOURCE |
| B1 | target | +0x140 = -1; set empty -> return, no action; else +0x140 = first set node (smallest id) | 0x00603560..0x00603580 | NEW | EXACT_SOURCE |
| B2 | null object | GetLocatedObjectByIdHelper(robot+0x34, +0x13C, family -1) null -> warning BehaviorAcknowledgeObject.BeginIteration.NullObject; erase; +0x140 = -1; non-empty -> BeginIteration. No listener call, no objective | 0x00603584..0x00603590; 0x00603742..0x006037A2; strings 0x00603858, 0x0060388C | NEW | EXACT_SOURCE |
| B3 | high flag | obj pose GetWithRespectTo(Robot::GetPose = FUN_004EA398, robot+0x298), result ignored; +0x149 = (transform z (+0x28) > 33.85 mm) | 0x006035AA..0x006035F4; float at 0x006035E4 | NEW | EXACT_SOURCE |
| B4 | turn | TurnTowardsObjectAction(robot, ObjectID(+0x140), Radians(+0x120), false, false); SetTiltTolerance(+0x130); SetPanTolerance(+0x128) | 0x006035F8..0x0060363E; PLT 0x4A9658, 0x4A961C, 0x4A95F8 (GOT relocs named) | NEW call; body M15-022 (Init 0x0054A1F0, CheckIfDone 0x0054ADD4), M13-020 (tolerances) | EXACT_SOURCE call; TTO ctor 0x00549D78/0x00549DC0 no record: RECOVERABLE_GAP |
| B5 | compound | CompoundActionSequential(robot); AddAction (vslot +0x20) turn | 0x00603642..0x0060366A | M8-017 | EXACT_SOURCE call |
| B6 | verify | if +0x138 >= 1: VisuallyVerifyObjectAction(robot, ObjectID(+0x140), Marker::ANY_CODE); action+0x8C = +0x138; AddAction | 0x0060366C..0x006036C0; literal 0x0060367E -> GOT reloc _ZN4Anki6Vision6Marker8ANY_CODEE | NEW call; VVO 0x00568A7C/0x00568B80/0x00568C24 and IVisuallyVerifyAction Init 0x005687D6 / CheckIfDone 0x00568968: no record (M14-002 owns only the +0x8C default 10) | EXACT_SOURCE call; VVO body RECOVERABLE_GAP |
| B7 | animation | if !+0xD9 (alwaysStreamline) and !+0xD8 (hard-spark flag): TriggerLiftSafeAnimationAction(robot, +0x11C, 1, true, 0, 60.0f, false); AddAction | 0x006036C2..0x00603710; M8-framework.md:224; M7-behaviour.md:1267 | NEW call; body M8-005/M8-020/M8-021 | EXACT_SOURCE call |
| B8 | StartActing | function of ActionResult {vptr 0x0102BA1C, AO, robot}; operator() 0x0060440E: SUCCESS -> +0x14A = 0, LookForStackedCubes; else FinishIteration | 0x00603714..0x00603730; vtable slot 0x0060440F; 0x0060440E..0x00604424 (veneers 0x8CC86C -> FinishIteration, 0x8CC87C -> LookForStackedCubes); ActionResult.cs:5 | NEW; M8-001 | EXACT_SOURCE |
| L1 | stacked: target | GetLocatedObjectByIdHelper(+0x13C, -1) null -> warning BehaviorAcknowledgeObject.StackedCube.NullTargetObject; FinishIteration | 0x006038DA..0x006038EE; 0x00603984..0x006039BC; 0x00603A1C | NEW | EXACT_SOURCE |
| L2 | stacked: height | h = obj.GetDimInParentFrame Z (rotation of obj pose wrt root, FUN_004DF628) | 0x006038F0..0x00603924; 0x00557794 | NEW; M12-012/M12-033 cite the helper | EXACT_SOURCE call |
| L3 | below check | if +0x149: +0x149 = !CheckIfGhostBlockVisible(robot, obj, -h, tmp) | 0x00603928..0x00603948 | NEW | EXACT_SOURCE |
| L4 | above check | r = CheckIfGhostBlockVisible(robot, obj, +h, outsideFov); !r and outsideFov -> L6; else +0x149 -> L5; else FinishIteration | 0x0060394C..0x0060396A; 0x006039BE..0x006039C2; 0x00603A1C | NEW | EXACT_SOURCE |
| L5 | look down | debug LookingDownToSeeCubeBelow; SetGhostBlockPoseRelObject(-h); LookAtGhostBlock(robot, false, FinishIteration) | 0x006039C4..0x00603A1A; 0x00603B4C..0x00603B50; GOT 0x0103F598 reloc FinishIteration | NEW | EXACT_SOURCE |
| L6 | look up | SetGhostBlockPoseRelObject(+h); if +0x14A: backup = 1; else markerPose = ghost.GetClosestMarkerPose(Robot::GetPose, true) (nonzero Result -> warning ClosestMarkerPoseFailed, use ghost pose); r = ComputeHeadAngleToSeePose(markerPose, angle, 0.1f); +0x14A = 1; backup = r != 0 or angle > 0.6021487 rad; debug LookingUpToSeeCubeAbove BackingUpFirst=Y/N; LookAtGhostBlock(robot, backup, LookForStackedCubes) | 0x0060396C..0x00603982; 0x00603A26..0x00603B50; float at 0x00603ADE; 0x3DCCCCCD 0x00603ABA..0x00603AC2; GOT 0x0103F594 reloc LookForStackedCubes; Result convention 0x008778A2..0x008778CC | NEW; M13-026; M12-033 | EXACT_SOURCE; callees per their records |
| G1 | CheckIfGhostBlockVisible | save ghost pose; SetGhostBlockPoseRelObject(offset); reason = ghost.IsVisibleFromWithReason(camera [robot+0x258]+0x24, 0.785398, 0.0, false, 0, 0); SetGhostObjectPose(ghost, saved, 2); out = reason == 6 OUTSIDE_FOV; return reason in {0 IS_VISIBLE, 7 OCCLUDED, 8 NOTHING_BEHIND} (debug StackedCube.AlreadyVisible / StackedCube.NotVisible) | 0x00603DA8..0x00603F4C; table 0x00C73120 = 0,7,8; names via 0x010382C0 | NEW; IsVisibleFromWithReason 0x008767D4 in M11-010 range 0x00876774..0x0087683E | EXACT_SOURCE; visibility per M11-010 |
| G2 | SetGhostBlockPoseRelObject | p = obj pose wrt root; translation z += offset (+0x20/+0x24/+0x28); ObjectPoseConfirmer(robot+0x26C)::SetGhostObjectPose(ghost, p, 2) = ghost vslot +0x28 SetPose(p, -1.0f, 2), returns 0 | 0x00604054..0x006040C2; 0x00506E40..0x00506E5A | NEW | EXACT_SOURCE |
| G3 | LookAtGhostBlock | Compound; if backup: DriveStraightAction(robot, -25.0, 100.0, false); TurnTowardsObjectAction(robot, ObjectID(-1), Radians(0), false, false) + UseCustomObject(ghost); WaitForImagesAction(robot, +0x138, VisionMode 1, 0); StartActing(compound, Robot function = bind(member, AO, _1)) | 0x006040D8..0x0060420A; movt 0xc1c8/0x42c8 at 0x00604114..0x00604118; PLT 0x4A9340, 0x4B41C4, 0x4A96A0; bind func vtable GOT 0x0103F59C; operator() 0x0060449A..0x006044AE | NEW; M13-020 (WaitForImages); M13-002 (DriveStraight, partial) | EXACT_SOURCE call; UseCustomObject 0x0054A050 and TTO with maxTurn 0: RECOVERABLE_GAP |
| G4 | Robot callback | StartActing (Robot overload) runs the member on any result | 0x005BE0E4; 0x005BF8F4 (AcknowledgeFace read B10) | M8-001 | EXACT_SOURCE (cited) |
| F1 | FinishIteration | each listener (set +0x158, pointer order) slot0 ReactedToID(robot, +0x140); erase +0x140; targets non-empty: BehaviorObjectiveAchieved(0x1B ReactedAcknowledgedObject, true), BeginIteration; empty: StartActing(TurnTowardsObjectAction(robot, ObjectID(+0x140), Radians(+0x120), false, false), void function 0x0060465A = BehaviorObjectiveAchieved(0x1B, true); BeginIteration) | 0x00603C74..0x00603D6C; 0x0060465A..0x00604672 (veneer 0x8CC89C -> BeginIteration); BehaviorObjective.cs:32 | NEW | EXACT_SOURCE |
| F2 | turn-back result | the void-function StartActing wrapper ignores the ActionResult | 0x005BDFE0..0x005BE02A; vtable 0x010267A0 slot 0x005BF791; 0x005BF790..0x005BF792 | M8-001 | EXACT_SOURCE |
| F3 | strategy record | RobotReactedToId: reacted pose = observed, reacted time = last image timestamp | 0x00612A94..0x00612B22 | M10-003 (4h) | EXACT_SOURCE |
| F4 | objective | E2G BehaviorObjectiveAchieved, info log, DAS | 0x005BE228..0x005BE308 | as AcknowledgeFace F3 | order EXACT_SOURCE; packer RECOVERABLE_GAP |
| S1 | StopInternal | each listener slot1 ClearDesiredTargets(robot); +0x140 = -1; targets destroyed and reset; +0x148 = 0. +0x149, +0x14A, ghost pose and listeners kept | 0x0060425E..0x006042C6 (ldr r2,[r1,#4] 0x00604278; blx r2 0x0060427C) | M7-019 (wording contradicted) | EXACT_SOURCE |
| A1 | shared children | TTO body, VVO, TriggerLiftSafeAnimationAction, DriveStraightAction, WaitForImagesAction, compounds, IsVisibleFrom, ComputeHeadAngleToSeePose, GetClosestMarkerPose | above | M15-022, M13-020, M8-005/020/021, M13-002, M8-017, M11-010, M13-026, M12-033 | not re-traced |

## Observable result
- Robot: turn (body within 45 deg, pan/tilt tolerance 5 deg) toward the lowest-id target; wait for 2 marker frames;
  play ag_reacttoblock_react_short unless streamlined or hard spark. On success, maybe look below and/or above the
  cube at a ghost pose (turn to it, a 25 mm reverse first when needed, 2-frame wait); after the last target, turn back
  toward it.
- C# API: one E2G BehaviorObjectiveAchieved(ReactedAcknowledgedObject) per target that reaches FinishIteration; none
  for a NullObject target.
- State: strategy reacted pose and time per target (FinishIteration, Stop); one process-wide ObjectID consumed by the
  ghost at the first Init.

## C# stack now (facts only)
- cozmo-stack/src/Cozmo.Robot/Behavior/ObjectBehaviors.cs:31-164 ObjectPositionUpdatedStrategy; STBI :143-152 calls
  ResetTargets(DesiredTargets()) then IsRunnable; ClearDesiredTargets() :155 has no caller in src.
- ObjectBehaviors.cs:179-311 AcknowledgeObjectBehavior: FIFO queue of targets; OnStart calls Scope.DisableReactions()
  (:238); NullObject path raises ReactedTo (:255); turn via _locator.TurnTowardsAsync(id, MaxTurnAngleRad) (:269);
  verification counts ObjectObserved sightings with a local 2.0 s timeout marked INFERRED (:185, :284); animation
  PlayTrigger(AnimationTrigger.AcknowledgeObject) hard-coded (:288); FinishIteration raises ReactedTo only: no
  objective, no turn-back (:292-301); stacked-cube ghost search DEFERRED (:175); OnStop raises ReactedTo for the
  current id unless Completed (:303-310). Config values are constants, not read from acknowledgeObject.json.
- Wiring Behaviors.cs:733-741; factory row BehaviorContainer.cs:261. BehaviorObjective ReactedAcknowledgedObject is
  not referenced in src.

## Existing records contradicted
- M7-019 (IMPLEMENTATION_GAP, "M7 concrete reaction-behaviour state machines"), via its evidence "reaction-class rows
  in M7 inventory Appendices E and F": the row at inventory/M7-behaviour.md:789 says StopInternal makes "virtual dtors
  on +0x158..+0x15C". The source calls listener vslot +4 with r1 = robot (0x00604274..0x0060427C), which for the only
  listener is ST ClearDesiredTargets (reloc 0x0102D2EC). The rest of that row holds.
- No other contradiction found. M10-derived.md App. C 4i-4l and round-1 row 8 agree with 0x006113E8..0x00611482 and
  0x00611674..0x006116EE.

## Evidence too weak for status
- None of the AO lifecycle is owned by a settled record. M7-019 covers only Init/Update/Stop/IsRunnableInternal rows
  (no ctor, LoadConfig, BeginIteration, LookForStackedCubes, ghost, FinishIteration).
- M14-002 (EXACT_SOURCE) owns the IVisuallyVerifyAction +0x8C default only; the VVO body that AO relies on has no record.

## UNKNOWN / RECOVERABLE_GAP
1. VisuallyVerifyObjectAction body (0x00568A7C, 0x00568B80, 0x00568C24; base Init 0x005687D6, CheckIfDone 0x00568968,
   SetupEventHandler 0x00568890): what it waits on, its timeout and failure result. RECOVERABLE_GAP.
2. TurnTowardsObjectAction ctor (0x00549D78 / 0x00549DC0) and UseCustomObject (0x0054A050..0x0054A13E), and Init
   0x0054A1F0 with maxTurn 0 on an invalid ObjectID plus custom object (the ghost look). RECOVERABLE_GAP.
3. ActionableObject::SetPose side effects when the ghost pose is set (vslot +0x28 virtual thunk): whether anything
   besides the ghost changes. The ghost is not in BlockWorld, so a broadcast is not expected, but it was not read.
4. BehaviorObjectiveAchieved packer (as in AcknowledgeFace F3).
5. Reachability of SetupForceTriggerBehavior (0x00611148).
6. Whether the process-wide ObjectID counter (0x008412D8) is the one that numbers API-delivered objects (M11-013).

## Follow-up read (coordinator request, 2026-10-29): gaps 1, 2, 6, 3

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| V1 | VVO ctor | name = VisuallyVerifyObject + id; IVisuallyVerifyAction C2(robot, name, RobotActionType 0x2E VISUALLY_VERIFY_OBJECT, VisionMode 1 DetectingMarkers, lift Preset 3); +0x90/+0x94 ObjectID, +0x98 = marker code, +0x9A = 0 | 0x00568A7C..0x00568B1E (movs r3,#0x2e 0x00568AC6; movs r1,#1 / movs r0,#3 / strd r1,r0,[sp] 0x00568ABA..0x00568ABE; strh r8,[r4,#0x98] 0x00568B0A); prefix string 0x00568B4C; RobotActionType.cs:53 | NEW | EXACT_SOURCE |
| V2 | base ctor | IAction C2(robot, name, type, tracksToLock 1); +0x78 = VisionMode, +0x7C = lift preset, +0x80..+0x88 = 0, +0x8C = 10 (frames; AO overwrites with NumImagesToWaitFor) | 0x005686D8..0x0056874C (str r0,[sp] with r0 = 1 at 0x00568706..0x0056870A; str r6,[r4,#0x78]; strb r5,[r4,#0x7C]; str 0xa at [r4,#0x8c] 0x0056873E..0x00568742) | M14-002 (only the +0x8C default); NEW rest | EXACT_SOURCE; track bit 1 = HEAD_TRACK rests on the AnimTrackFlag order at 0x007BC250 cited in cozmo-stack Motion.cs:357 (not rechecked) |
| V3 | base Init | CompoundActionParallel(robot, list of MoveLiftToHeightAction(robot, Preset +0x7C, 5.0f) and WaitForImagesAction(robot, vslot +0x30 = +0x8C frames, VisionMode +0x78, 0)) stored at +0x80; compound byte +0x56 = 1; then vslot +0x34 InitInternal | 0x005687D6..0x00568866; vslot +0x30 = 0x005698EC ldr r0,[r0,#0x8c]; VVO vtable 0x0103EC64 relocs | NEW; preset 3 OutOfFOV = M4-002 EXACT_SOURCE; WaitForImages = M13-020 EXACT_SOURCE | EXACT_SOURCE; meaning of compound +0x56 UNKNOWN (not read) |
| V4 | VVO InitInternal | +0x9A = 0; SetupEventHandler(E2G tag 0x44 RobotObservedObject, lambda) through Robot::GetExternalInterface vslot +0x28, handle at +0x84/+0x88; +0x9B = (marker code == Marker::ANY_CODE); returns 0 | 0x00568B80..0x00568BF0; 0x00568890..0x00568914 | NEW | EXACT_SOURCE |
| V5 | observed lambda | on RobotObservedObject: if !+0x9A and msg.objectID (msg +0xC) == +0x94: +0x9A = 1 | 0x0056992E..0x00569952 (vtable 0x010232B4 slot 0x0056992F); RobotObservedObject.cs:9-15 field order | NEW | EXACT_SOURCE |
| V6 | HaveSeenObject | !+0x9A -> false; +0x9B (ANY_CODE) -> true; else looks the object up and returns true (setting +0x9B) if an observed marker has code +0x98, else info log of the seen codes and false; object missing -> warning, false | 0x00568C24..0x00568DB8 | NEW | EXACT_SOURCE (AO never takes the non-ANY branch) |
| V7 | CheckIfDone | vslot +0x38 HaveSeenObject true -> SUCCESS (0), checked before the compound on every tick; else compound Update: RUNNING -> RUNNING; any other result (lift done and the frames counted, whatever the compound result) -> info log and VISUAL_OBSERVATION_FAILED 0x0300001D | 0x00568968..0x005689EE (movs r0,#0x1d / movt r0,#0x300 at 0x005689E6..0x005689E8); ActionResult.cs:38 | NEW | EXACT_SOURCE |
| V8 | VVO timeout | no own timeout: IAction default slot +0x2C = 0x0052B0C2 returns 30.0f (0x41F00000); the timeout handling is IAction::Update | 0x0052B0C2..0x0052B0C4; VVO vtable slot +0x2C relative reloc 0x52B0C3 | M8-015; M15-022 cites the same slot | EXACT_SOURCE value |
| V9 | VVO messages | VVO sends nothing itself; robot traffic comes from its children, the lift move (M4-016) and the WaitForImages vision-mode request (M13-020); head track locked through IAction tracks 1 (M8-015) | V3, V7 | M4-016, M13-020, M8-015 | EXACT_SOURCE (absence in 0x005686D8..0x00568DB8 and 0x0056992E) |
| W1 | TTO C1 | forwards to C2(robot, ObjectID, Marker::ANY_CODE, Radians maxTurn, bool verify, bool headTrack) | 0x00549D78..0x00549DB4 (ldrsh r3,[r1] from the ANY_CODE GOT at 0x00549DA6..0x00549DA8; blx 0x4ab23c) | NEW | EXACT_SOURCE |
| W2 | TTO C2 | TurnTowardsPoseAction C2(robot, maxTurn); +0x17A = 0, +0x17C = 0 (verify child), +0x180 = verify, +0x181 = 0, +0x184/+0x188 ObjectID, +0x18C = 0 (object ptr), +0x190 = marker code, +0x192 = headTrack, +0x193 = 1 (refine pass pending), +0x194 = Radians 5 deg; name TurnTowardsObject + id; +0x44 = 0x2C TURN_TOWARDS_OBJECT | 0x00549DC0..0x00549E92 | NEW | EXACT_SOURCE |
| W3 | UseCustomObject | if +0x188 != -1: warning TurnTowardsObjectAction.UseCustomObject.CustomObjectOverwriteId, +0x188 = -1; +0x18C = object; name TurnTowardsCustomObject + id; object id == -1 -> info log. AO passes ObjectID -1 and a ghost that has an id, so neither log fires | 0x0054A050..0x0054A13A; strings 0x0054A18C, 0x0054A1CC | NEW | EXACT_SOURCE |
| W4 | Init, custom path | id == -1: +0x18C null -> error, BAD_OBJECT 0x03000004; object pose not in the world origin -> warning, BAD_POSE 0x03000005; verify flag set -> warning, cleared; then the shared marker step on the custom object: ANY_CODE -> GetClosestMarkerPose(Robot::GetPose, true), failure -> error, BAD_MARKER 0x03000002; success -> +0x164 = pose, +0x178 = 1, TurnTowardsPoseAction::Init; OK -> +0x17A = 0 | 0x0054A1FC..0x0054A20E (sl = 0x03000002); 0x0054A300..0x0054A36A; 0x0054A490..0x0054A4F0; 0x0054A4F2..0x0054A580; 0x0054A26E..0x0054A2FE; 0x0054A6D8..0x0054A704 | NEW (custom branch); M15-022 (id branch) | EXACT_SOURCE |
| W5 | maxTurn 0 | TurnTowardsPoseAction stores abs(maxTurn) at +0x170; Init computes and tests the pan only if +0x170 > 0, so with Radians(0) the body pan stays 0 and only the head angle (ComputeHeadAngleToSeePose, clamped) is commanded: the ghost look is head only | M13-020 evidence: ctors 0x00549F10/0x0054B344; TurnTowardsPoseAction::Init 0x0054A8FC | M13-020 EXACT_SOURCE | EXACT_SOURCE (per M13-020, not re-traced) |
| W6 | CheckIfDone (AO flags) | !+0x17A: if +0x179 (pan beyond max turn) skip the compound, else compound Update and a non-SUCCESS result is returned. Then +0x17A = 1; if +0x193: IAction::Reset, +0x193 = 0, SetPanTolerance(+0x194, 5 deg), info log, RUNNING (a second, refining pass through Init). Second pass: verify flag clear, no child, +0x192 clear -> SUCCESS | 0x0054ADD4..0x0054AECC; 0x0054AF60 | M15-022 (names the RUNNING tick 0x0054AE40..0x0054AEB8 as not modelled) | EXACT_SOURCE |
| W7 | beyond 45 deg | when the pan exceeds +0x170, TurnTowardsPoseAction::Init sets +0x179 and returns 0 before the head angle (M13-020), and W6 skips the compound: the turn ends SUCCESS with no motion, so AO goes on to verify and animate | W5, W6 | M13-020; NEW for the AO consequence | EXACT_SOURCE |
| N1 | ObjectID writers | the only BL/BLX/B.W callers of ObjectID::Set (PLT 0x004A55F0, veneer 0x008CAB5C, body 0x008412D8) are Cozmo::ObservableObject::SetID 0x004EF468 (0x004EF4F4 non-unique, 0x004EF50E unique first time) and a Vision::ObservableObject SetID body at 0x008779CE (adds r0,#0x14; b.w 0x8CAB5C). SetID has no direct caller (PLT 0x004A4378 unreferenced): it is reached through vtable +0x24 | full .text scan of Thumb BL/BLX/B.W targets 0x004A4378, 0x004A55F0, 0x008412D8, 0x004EF468 and their veneers | M11-013 | EXACT_SOURCE |
| N2 | shared counter | observed and connected objects (cubes, charger, non-unique objects) get their ObjectID from this one counter (M11-013 evidence 0x00620C40..0x00620C5A, 0x004EF468..0x004EF526); unique types (vslot +0x5C true) take one value per type, the first time. The ghost (not unique, 0x004E02AE) takes one value at the first AO Init. Every ObjectID first assigned after that point is one higher than without AO; IDs already assigned do not move | N1; 0x004EF4BC..0x004EF526; 0x00603444..0x0060344E | M11-013; NEW (the AO consumer) | EXACT_SOURCE for the mechanism; how often a first assignment follows the first AO run is a runtime question |
| N3 | faces | face IDs do not come from this counter: Set has no other caller (N1) | N1 | M14 | EXACT_SOURCE (absence) |
| N4 | stack allocator | cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs:77-121 ObjectIdSpace: one static counter plus unique-type map (Set, SetIdFor), used by SetID (:356) from :1258, :1786, :2875. The stack has no AO ghost and no Set call for it | file lines | M11-013 | fact |
| P1 | ghost SetPose | SetGhostObjectPose -> ghost vslot +0x28 = virtual thunk 0x004E022A -> ActionableObject::SetPose 0x004E01E4: clears the cached pre-action pose vectors (+0x28..+0x70), then ObservableObject::SetPose 0x004EF580 -> 0x004EF590: pose copy, PoseState byte +0x24, pose name, distance stored only if >= 0 (-1 is not), +0x50 = 1. No BlockWorld call, no message, no broadcast; the callees are pose and string helpers only | 0x00506E40..0x00506E5A; 0x004E022A..0x004E0234; 0x004E01E4..0x004E0226; 0x004EF580..0x004EF58E; 0x004EF590..0x004EF646 | NEW | EXACT_SOURCE: no effect on 1-4 beyond the ghost |

### M15-022 coverage of TurnTowardsObjectAction
M15-022, IMPLEMENTATION_GAP, PlaceObjectOnGroundAtPoseAction composition and PlaceObjectOnGroundAction Init/CheckIfDone.
Evidence: PlaceObjectOnGroundAction Init sends 0x44, builds a TurnTowardsObject verify action and calls StopAllMotors;
CheckIfDone returns the verify action result; report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md.
Its unresolved: the verify child is TurnTowardsObjectAction from Init 0x0054A1F0 and CheckIfDone 0x0054ADD4 with the
visual-verify byte clear; still open: the TrackObjectAction branch (+0x192, 0x0054AE08), the VVO branch (0x0054AED8),
the single RUNNING tick (0x0054AE40..0x0054AEB8), the with-respect-to failure 0x03000005.
- Covered by M15-022: the id path of Init (object lookup 0x0054A210..0x0054A226, marker choice, head-angle fallback).
- Not covered: ctors W1/W2, UseCustomObject W3, the custom-object Init branch W4, the refine pass W6 (open in
  M15-022 itself), and the beyond-max SUCCESS consequence W7.

## Updated: can it be built now
Yes for everything AcknowledgeObject itself does: VVO (V1-V9), TurnTowardsObjectAction (W1-W7), the ghost pose (P1)
and the ObjectID use (N1-N4) are now read. What remains belongs to shared records: M11-010 visibility reasons
(IMPLEMENTATION_GAP) for the stacked-cube decision; M13-026 ComputeHeadAngleToSeePose (EQUIVALENT_IMPLEMENTATION) for
the backup choice; M12-033 GetClosestMarkerPose; the objective packer (F4); the IAction 30 s timeout and track
locking (M8-015); the compound byte +0x56 (V3, UNKNOWN). For the manager: whether the ghost ObjectID use (N2) is
reproduced, and whether the forced trigger (T7) is reachable.
