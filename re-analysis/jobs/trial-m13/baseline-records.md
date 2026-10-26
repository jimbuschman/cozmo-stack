# Frozen trial target records

Snapshot at 16173227; statuses and evidence are quoted verbatim. No official reclassification.

## M13-003
`json
{
  "id": "M13-003",
  "subsystem": "M13-navigation",
  "title": "Obstacle expansion, per-heading C-space polygons, and the primitive collision test",
  "location": "cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "ImportBlockworldObstaclesIfNeeded 0x004FD4B8: paddings 7.0/6.0 (0x004FD4EE/0x004FD4F2) or 2.0/1.0 (0x004FD4EA/0x004FD4EC) selected by the function's own first bool argument (r4=r1 0x004FD4D2; cmp 0x004FD4E8; itt ne 0x004FD4F6); callers pass 0 (0x004FD2AC, 0x004FF92A) or 1 (StartPlanning 0x004FEC38)",
    "import log \"robot padding %f, obstacle padding %f , didBlocksChange %d\" (0x004FD51A/0x004FD546); penalty 0.1 (0x3DCCCCCD at 0x004FE0CE); AddObstacleWithExpansion 0x00855528 stores the caller's penalty at pair+0x44 (0x00855612) and calls ExpandCSpace 0x008550E8; ConvexPolygon::RadialExpand 0x004FDF9E",
    "ConvexPolygon::RadialExpand body 0x00841580: for each vertex v, v' = v + d*(v-c)/|v-c| with c the polygon centroid (ComputeCentroid 0x008415D8; per-vertex 0x008415F2..0x00841654), no bisector/cos and no clamp; a negative d only warns (0x00841590/0x00841598)",
    "IsInCollision(State) 0x008515BC converts the grid shorts to mm and tail-calls IsInCollision(State_c) 0x008515F8, which buckets theta as round(theta*env+0x10) mod env+8 (0x00851614..0x0085167A) and returns 1 only for a containing polygon with penalty >= 1000.0 (0x008516D0/0x008516DC); IsInSoftCollision 0x00851708 returns 1 for any containing polygon; GetCollisionPenalty 0x008517B0 returns the first containing polygon's penalty or 0.0 (0x008517C4/0x0085184E)",
    "the per-primitive test is Anki::Planning::SuccessorIterator::Next 0x0085110C: broad phase is the primitive's cached bbox (MotionPrimitive+0x1C..+0x28) against env+0x50 (0x0085123A..0x00851286); turning vs non-turning is decided by end_pose.theta vs MotionPrimitive+1 (0x008512CC/0x008512D0)",
    "non-turning primitive: every intermediate pose is tested (loop 0x0085134A..0x008513A6, stride 0x14) at (start + IntermediatePosition.x_mm, +y_mm) in the end_pose.theta bucket (0x008512E0/0x008512E4); turning primitive: the intermediate poses are walked last to first (0x00851414/0x0085149E) each in its own bucket at IntermediatePosition+0xC (0x00851420/0x00851428)",
    "soft hit (polygon penalty < 1000.0) adds base + penalty*IntermediatePosition+0x10 to the successor cost (non-turning 0x0085137E..0x00851398; turning 0x0085146E..0x00851490); penalty >= 1000.0 is a hard collision and rejects the primitive (0x0085138A/0x0085147A; threshold 1000.0 at 0x00851162); base is 0.0 forward and 1000.0 for a reverse primitive (0x008515B0/0x008515B4)",
    "successor g = soft-collision cost + parent g + MotionPrimitive+4 (0x0085150A/0x00851516/0x0085151A/0x0085151E/0x00851522); the 0.1 penalty means the shipped obstacles are all soft",
    "the soft-collision multiplier at IntermediatePosition+0x10 is defined in MotionPrimitive::Create 0x00853DD0 (0x00853E96..0x00853F4E): for each intermediate pose, dist is the Euclidean distance to the previous intermediate pose already in the vector (0x00853E9E..0x00853EDE) and dtheta is Radians(cur.theta_rads) - Radians(prev.theta_rads) rescaled to [-pi,pi) (0x00853EBE/0x00853EFA/0x00853F0E), giving inverseDist = 1/(env+0x78 * env+0x60 * |dtheta| + dist) (0x00853F1E..0x00853F46, i.e. 1/(|dtheta|/60*24 + dist)); the first intermediate pose has inverseDist 0 (0x00853E96/0x00853E9A)"
  ],
  "effect": "the planner expands obstacles by the wrong padding or penalty and routes differently",
  "provenance": "the current stack code; its old 45 mm radius / 20 mm ring / penalty 100 design is not the engine's",
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): the planner's primitive import, polygon and C-space expansion, collision and cost are binary32 (0x00841580..0x00841654, 0x0085110C..0x008515B4; penalty 0x3DCCCCCD). Still open: the obstacle source is a labelled STAND-IN (ImportBlockWorldObstacles reads BlockWorld and includes the charger; the engine reads the memory map, predicate 0x00502410 keeps content type 3 only); the Pose3d round trip and ComputeOriginPose of the robot quad (0x004FE042..0x004FE08E) are not transliterated; previous-plan reuse (PlanIsSafe) is not built.",
  "hardware_required": false,
  "live_path": true,
  "test": "NavigationTests.ThePlannerRoutesAroundACubeInTheWay"
}
`

## M13-017
`json
{
  "id": "M13-017",
  "subsystem": "M13-navigation",
  "title": "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update",
  "location": "cozmo-stack/src/Cozmo.Robot/Behavior/ChargerBehaviors.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "ctor 0x005C0980 stores 96.0 + json extraDistanceToDrive_mm at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6); IsRunnableInternal 0x005C0B10 returns robot+0x34A (the on-charger-platform flag; contacts are +0x338)",
    "InitInternal 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions only when robot+0x355 == 0 (0x005C0B54); +0x355 is the robot's OffTreadsState (OnTreads = 0), written only by Robot::CheckAndUpdateTreadsState 0x00512088/0x0051208E and named by OffTreadsStateFromString 0x0078DF58 (OnTreads 0, InAir 1, OnBack 2, OnLeftSide 3, OnRightSide 4, OnFace 5, Falling 6)",
    "TransitionToDrivingForward 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08)",
    "UpdateInternal 0x005C0DA8: once off the platform (robot+0x34A == 0) it records the time at robot->[+0x264]->[+0x18]+0x44 and returns 2 (0x005C0DF4..0x005C0E0A); while still on the platform and robot+0x355 != 0 it calls StopActing(false,false) (0x005C0DB6/0x005C0DC4) and waits; on the platform with +0x355 == 0 it transitions to driving forward (0x005C0E18)"
  ],
  "effect": "the drive-off behaviour runs when it should not, drives the wrong distance or never stops",
  "provenance": "the current stack code; not yet compared against re-analysis/inventory/M13-navigation.md",
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): BehaviorDriveOffCharger per the binary: runnable on OnChargerPlatform (+0x34A, 0x005C0B10, 0x005C0DA8), redrives until the platform flag clears, no 5 s timeout, the whiteboard +0x44 stamp is float and written only by the behaviour (0x005C0E02..0x005C0E08; the local FreeplaySystem writer is removed), IsRunnableBase reads it in float (0x005BD826..0x005BD962); the Robot::Update platform step (0x00513CD8..0x00513E2A) clears the flag from the charger and robot quads (minAreaRect built from the validated float32 port). Contact-true located-charger observation is built by B-M13-9, corrected by B-M13-10 (M13-040); shared-owner gaps remain. Still open: PushDrivingAnimations and WaitForOnTreads action ownership; Markerless/Ramp/HumanHead footprints; GetBoundingQuad catch-all 0x004E65C6; shared SetObjectPoseChanged 0x00624892, M11-074 map continuations and M12-001/M12-045 cached-pose destructor ownership; a drive-off regression through admitted, frame-matched RobotStates. No hardware verification or settlement. B-M13-11 builds the shared row2 height selector and n<=2 minAreaRect results. The old double-rotation-cast description is stale: the current quaternion getter creates Mat3 with f32 storage; shared pose arithmetic retains its own verification boundary. Codex review; Claude spot-check pending. These units do not close drive-off ownership or live input gaps.",
  "hardware_required": false,
  "live_path": true,
  "test": "NavigationTests.DriveOffChargerDrivesTheChargerLengthPlusTheExtraAndFiresTheEvent"
}
`

## M13-030
`json
{
  "id": "M13-030",
  "subsystem": "M13-navigation",
  "title": "The MemoryMap insert of cliff data (MemoryMapData_Cliff, QuadTree insert, helper +0x38 timestamp)",
  "location": "cozmo-stack/src/Cozmo.Robot/Sensors.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204.",
  "evidence": [
    "re-analysis/research/20261012-m4-cliff-and-names-check.md section M4-038 step 7: BlockWorld+0x54 = 1, MemoryMapData_Cliff (0x00684984), quad from GetBoundingQuadXY (0x00502612..0x00502650), insert through helper vtable+8, BlockWorld+0x58 = fmax(ts, helper+0x38()) (0x006226EA..).",
    "re-analysis/research/20261012-cliff-world-helpers-check.md section 2: MemoryMapData_Cliff ctor/Clone/Equals 0x00684984..0x00684A2C, NodeContent 0x0068C450..0x0068C4E2, AddMarkerlessObject insert (input pose r8 0x0062238C, normal 0x006225A8..0x006225C6, ts 0x00622390, type 14 branch 0x00622646..0x006226E6), MemoryMap::Insert 0x006817C6..0x006817E0 (QuadTree::Insert with 1), helper +0x38 0x00681934..0x0068193A, BlockWorld+0x58 fmax 0x006226EA..0x0062271E; quad-tree rows (Insert 0x00685008..0x00685130, Expand 0x00685200..0x0068543A, UpgradeRootLevel gate/log, Insert wrapper 0x00686CB4, Insert_Recursive 0x00686CF4..0x00686DDC, override rules, ForceSet, Subdivide parent timestamps, TryAutoMerge shared-object write) checked as M11-045 rows."
  ],
  "effect": "The navigation map after a cliff.",
  "provenance": "Opus check of R-M4-CLIFF (2026-10-12), manager record.",
  "unresolved": "Read 2026-10-13 (re-analysis/research/20261012-cliff-world-helpers-check.md): build the cliff data class and the insert from the rows (job B-M3M4-16), through M11-045's quad tree (shared rows, not a second tree). Unread internals are M11-048 (GetOverlapType/FastPolygon, UpgradeRootLevel body, ShiftRoot, processor bodies, ClearDescendants, Swap/DestroyNodes). Null helper: M13-031. Previous notes: RECOVERABLE_GAP: read the MemoryMap insert and helper+0x38 (split from M4-038).\nB-M3M4-16 (2026-10-13), built, awaiting strong verification: Cliff type-8 data constructor/clone/strict normal equality (timestamps ignored), NodeContent clone/equality and input-pose quad + current robot X-normal + robot timestamp dispatch built. Native insertion return ignored and BlockWorld fmax last-change write; FastPolygon/GetOverlapType stops insertion visibly as M11-048. ProxObstacle data branch absent as MISSING. Current flattened robot pose owner remains M11-053; no claim that an unknown ComputeStateAt succeeds.\nB-M3M4-16 final review: cliff data is constructed before the input quad; R_robot*X keeps float multiply/add order (including zero terms), rather than assuming a column shortcut.",
  "hardware_required": false,
  "live_path": true,
  "test": "M3M4X16BuilderTests"
}
`

## M11-074
`json
{
  "id": "M11-074",
  "subsystem": "M11-vision",
  "title": "Memory-map producers and readers not yet read: MapComponent::UpdateRobotPose (explored area), UpdateObjectPose, RemoveObservableObject, FlagInterestingEdgesAsUseless, ReviewInterestingEdges, ClearRobotToMarkers, FindContentIf/FindIf",
  "location": "cozmo-stack/src/Cozmo.Robot/Vision/MemoryMap.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204.",
  "evidence": [
    "re-analysis/research/20261013-quadtree-rest-check.md: UpdateRobotPose 0x0067E224..0x0067E50B; UpdateObjectPose 0x0067EA90..0x0067ECFB; RemoveObservableObject 0x0067F2C4..0x0067F58B; FlagInterestingEdgesAsUseless 0x0067E778..0x0067E85B; ReviewInterestingEdges/ClearRobotToMarkers 0x0067F58C..0x0067F7AB; FindContentIf 0x00681674..0x006816E7 and QuadTreeNode::FindIf 0x006884FC..",
    "re-analysis/research/overnight/20261015-ov42-44-check.md: R1-R6 0x0067E23A..0x0067E486 (cliff byte writer 0x005356C6..0x005356D4); O1-O8 0x0067EA9A..0x0067F0BC; caller order 0x00506F98..0x00506FB8, 0x00506296..0x005062D6 (double Add); D1-D3 0x0067F4C4..0x0067F544, 0x0067F2DE..0x0067F368, 0x006810E0..0x00681172; C1-C5 0x0067F5B8..0x0067F714, caller 0x00506A86..0x00506A98; Q1-Q6 0x00681674..0x006816DC, 0x006884FC..0x006886F4."
  ],
  "effect": "What goes into the memory map as the robot drives and sees objects, and what the planner reads from it.",
  "provenance": "Opus check of R-QUADTREE (2026-10-13), manager record.",
  "unresolved": "RECOVERABLE_GAP: read the listed bodies completely. Needed before all map content moves into the quad tree (the stack's MemoryMap._regions writers are stand-ins for these). Read 2026-10-15 (re-analysis/research/overnight/20261015-ov42-44-check.md): IMPLEMENTATION_GAP for UpdateRobotPose, object add/remove (with the double Add), ClearRobotToMarkers. Unreachable (no reference in the binary): FlagInterestingEdgesAsUseless 0x0067E778, ReviewInterestingEdges 0x0067F58C; not built. New: M11-088 (+0x5C/+0x50 per class), M8-026 (ProcessClearQuad). FindIf result order follows heap addresses: not built until the operator decides whether the M11-047 departure extends to it and M13-003's consumer 0x004FDF1C..0x004FDF8C is read. M1-034 points: 0x0067E38C, 0x0067E3E4, 0x0067E444, 0x0067EF14, 0x0067EFF6, 0x0067F6D0, 0x0067EC3C, 0x0068878C. Operator decision 2026-10-15: the M11-047 departure extends to FindIf's result order (AGENTS.md): the stack returns the matches in insertion order (the order the post-order walk first finds them); the rest of FindContentIf/FindIf stays exact. The memory-map stream (M11-075) stays unexposed, as in the shipped app. B-M11-9 (2026-10-16), built, awaiting strong verification: actual ObjectPoseConfirmer.BroadcastObjectPoseChanged now calls UpdateObjectPose after BlockWorld and again for changed nonzero current state before recipients; Invalid-to-valid requests Add twice. Markerless early return and BothStatesAreInvalid Error/errG are built. Add/remove/cache recipients stop with precise MISSING; no per-class +0x50/+0x5C fallback or polygon-list insertion/removal was substituted. Remaining: R1-R6 native robot writer, object cache/height/add/remove lambda/border, marker-clear join, FindIf and its insertion-order departure. Legacy Freeplay SyncFromWorld/UpdateRobotPose still exist as unclaimed candidates; this batch does not promote them or call its route observers real inserts.",
  "hardware_required": false,
  "live_path": true,
  "test": "MapTimingCheckedRowsTests"
}
`

## M13-040
`json
{
  "id": "M13-040",
  "subsystem": "M13-navigation",
  "title": "Contact-true charger observation: live ingress, located identity, Known pose and ordered world/contact effects",
  "location": "cozmo-stack/src/Cozmo.Robot/Sensors.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "RobotState sync gate 0x0051293C..942, contact bit12 0x00512AAC/AAE and SetOnCharger call 0x00512AB4; SetOnCharger 0x00511990..0x00511C2D observes on every true input before old-contact gate 0x00511BA8/BB0. Contacts +0x338; platform +0x34A.",
    "ChargerDockPose 0x004EA304..0x004EA35A: Z rotation pi bits 0x40490FDB, translation (30,0,0), robot pose parent and ChargerDockPose name. Located family4/type13 first/current-origin query 0x00511A1C..A52, 0x0061EBF2..0x0061EC26; factory and connected identity/unique SetID 0x00511AD6..0x00511B40, 0x004EF468..0x004EF528.",
    "Known observation 0x00511B4A -> 0x00506B70..0x00506C48: reparent/root flatten, existing/new branch, SetPoseHelper 0x0050609C, distance -1, confirmer count1/reference/existing timestamp. Actionable cache invalidation 0x004E01E4..226; common SetPose 0x004EF590..646. AddLocatedObject 0x00622AC0..EB0 includes obstacle deletion, VizManager pointer store obj+0x54 (0x00622D24..0x00622D30; CozmoContext+0x24, ctor 0x004EA734; read by EraseVisualization 0x004EA5E8..0x004EA5F2): viz, no effect on 1-4 and notification.",
    "Notifications 0x00506F98/FA8/FB8 world/map/state-change; unchanged return 0x00506296..29A; changed nonzero new state map 0x005062B4 and active light recipient. 0x00639840 compares NEW state r3 with1: Known trigger0x25/layer2, other changed trigger1/layer2; unchanged no request.",
    "Contact edge/store ordering 0x00511A66..ACC, 0x00511B50..0x00511C14; platform setter 0x00511D44..DCA; existing footprint tick 0x00513CD8..0x00513E2A and M4-019 threshold send 0x0063427E, 0x006342C8..CE, 0x00634364..368.",
    "re-analysis/research/20261021-m13-017-located-charger-check.md: independent check of A1-A11/P1-P4 with corrected A9; supplementary mount and exhaustive caller census excluded."
  ],
  "effect": "Missing located charger clears platform protection too early after contacts drop; pose, identity and API/world effects must follow source.",
  "provenance": "Independent native-source review 2026-10-21; operator authorized integrating the checked contract and queueing builder B-M13-9.",
  "unresolved": "built, awaiting strong verification: B-M13-9 wires synchronized contact-true ingress to the shared BlockWorld charger factory/identity owner, native ChargerDockPose parent/flatten, Known existing/new observation, actionable cache clearing and ordered existing world/map/light recipients before contact edges. Received-state regressions in M13ContactChargerTests cover identity, current origin, nontrivial pose, repeated true, distance/timestamp/localization, recipient order and footprint exit. MISSING shared BlockConfigurationManager::SetObjectPoseChanged 0x00624892, M11-074 map height/cache continuations and M12-001/M12-045 cached-pose destructor ownership retain their existing boundaries; no bypass or invented map insertion. Prior scope note (before this build): Unbuilt live contact-observation join. B-M13-9 builds checked A1-A11/P1-P4 with A9 corrected to NEW state. Shared pose/map/obstacle/light implementation gaps remain visible; existence of helpers is not proof of whole-path fidelity. No mount implementation or exhaustive caller census is claimed. No hardware verification or M13-017 settlement. B-M13-10 removes the false insertion timestamp claim; obj+0x54 is VizManager, per the Claude check 2026-10-21.",
  "hardware_required": false,
  "live_path": true,
  "test": "M13ContactChargerTests (received RobotState/real world recipients; isolated actionable-cache fixture explicitly labelled)"
}
`