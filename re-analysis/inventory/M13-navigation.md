# M13-navigation inventory (lattice planner, charger, block configurations)

**State:** approved by the manager on 2026-09-28 under the operator's standing authorisation (inventories and ordinary source-fidelity decisions need no operator checkpoint), and frozen with `python re-analysis/tools/fidelity.py --approve M13-navigation`. No forced policy was needed: every row settled EXACT_SOURCE, so no COMPATIBILITY_POLICY, RECOVERABLE_GAP, HARDWARE_ONLY or BLOCKED_EXTERNAL record was created.

## Where this comes from

- **Source:** `libcozmoEngine.so` (file VAs, Thumb) and the shipped OBB asset `re-analysis/obb/assets/cozmo_resources/config/engine/cozmo_mprim.json` (authority 3). The C# was never evidence.
- **Read-only extractor passes:**
  - **X2 pass** (Appendix A), rows N1..N27: the planner and its motion primitives, obstacle expansion, path dole, the charger (geometry, the mount, driving off the contacts), FlipBlock, AlignWithObject, pop-a-wheelie, the whiteboard beacons, the stack tolerance and the workouts.
  - **Gap pass 1** (Appendix B), sections G1..G10: the primitive loader and schema, the OBB asset itself, the stack-tolerance callers, the planner entry/worker and the Replan argument, the knock-over path, the AlignWithObject and FlipBlock constants, the drive-off contacts action and behaviour, the mount numbers and units, and the charger geometry.
  - **Gap pass 2** (Appendix C), items 1..5: the AlignWithObject alignment-type table (both earlier passes had it rotated), the BackupOntoChargerAction result codes, the knock-over maxTurn table address, the mount turn numbers/units and the Replan condition.
- **Verification:** `@cozmo-verifier` re-opened the instructions at every row that changed a record's status or contradicted one, plus a sample of more than twenty others. It FAILed five rows: the Replan condition (N10/G4/G10), the mount max speed (N16), the backup failure code (N17), the AlignWithObject table (N23/G6b) and the knock-over table address (G5a). All five went back to `@cozmo-extractor` and were corrected in gap pass 2 (see its "Contradictions" list); the corrected values are what the records below carry. Every other checked row was supported by the instructions.
- **The OBB was present in this clone** (unlike the X2 pass's clone), so M13-001 and M13-004's asset side is now checked directly: `cozmo_mprim.json`, size 1,272,734 bytes, sha256 `4C79666BDD5F3F5FE6C7458B0E7D59ECBCCDB2AB3C12F1844D68BCC57887431D`.
- **Interfaces used and not redone:** M4-control (TurnInPlaceAction's rad/s units and limits, 0x00545C08/0x00545D14), M11-vision (the face detector feeding `TurnTowardsLastFacePoseAction`), M12-manipulation (carrying/docking objects). Where a record needs an input from another layer, the input is named in the record's evidence; the engine-side behaviour is read here.

## Records

All records are **IMPLEMENTATION_GAP**: the engine behaviour is read, but the stack's implementation has not yet been compared against this inventory and repaired. This is deliberate (job I-M13 step 6): the earlier EXACT_SOURCE labels predate the evidence process and were never checked against the code.

| record | status | what | rows |
| --- | --- | --- | --- |
| M13-001 | IMPLEMENTATION_GAP | The lattice planner's motion primitives are loaded from the shipped `cozmo_mprim.json`. <br>- `xythetaEnvironment::ReadMotionPrimitives(path)` 0x008529C0 opens the file `"r"` (0x00852A44/0x00852A48), parses it with `Json::Reader::parse` (0x00852A62) and calls `ParseMotionPrims(root, oldFormat=false)` (0x00852B04/0x00852B06). <br>- `xythetaEnvironment::ParseMotionPrims(Json const&, bool)` body 0x00852014 reads `resolution_mm` → env+0 then `1/env[0]` → env+4 (0x0085203C, 0x008520AA), `num_angles` → env+8 (0x00852062), `actions` → a vector of 24-byte `ActionType` at env+0x30 (0x008520BA, each through `ActionType::Import` 0x00851A18), `angle_definitions` → floats at env+0x38 (0x008521C6), and `angles` → `vector<vector<MotionPrimitive>>` at env+0x14 resized to num_angles (0x00852248, 0x00852264); each angle's `prims` element goes to `MotionPrimitive::Create` (0x008522F4 for the old format, 0x0085230E otherwise). Error strings confirm the keys (0x00852092, 0x008521B4, 0x00852452). | N1, N2, G1a, G1d, G1e |
| M13-002 | IMPLEMENTATION_GAP | `FlipBlockAction`'s constants. The constructor 0x0055EC80 calls `IAction::IAction` with type **0xF** (0x0055ECA8/0x0055ECAA) and stores **+0x12C = 150.0** (0x43160000), **+0x130 = 20.0** (0x41A00000) (0x0055ECF6/0x0055ECFA), **+0x134 = 45.0** (0x42340000), **+0x138 = 40.0** (0x42200000), **+0x13C = -1** (0x0055ECFE..0x0055ED0E) and **+0x140 = 1** (0x0055ED10/0x0055ED12). | N21, G6a |
| M13-003 | IMPLEMENTATION_GAP | Obstacle expansion in `LatticePlannerImpl::ImportBlockworldObstaclesIfNeeded(bool, ColorRGBA const*)` 0x004FD4B8. <br>- The two paddings are **7.0/6.0** (0x004FD4EE/0x004FD4F2) or **2.0/1.0** (0x004FD4EA/0x004FD4EC) selected by the **function's own first `bool` argument** (r4 = r1 at 0x004FD4D2; `cmp r4,#0` at 0x004FD4E8; `itt ne` at 0x004FD4F6). Callers pass an immediate: `ComputePathHelper`/`PreloadObstacles` pass 0 (0x004FD2AC/0x004FD2AE, 0x004FF92A/0x004FF92C), `StartPlanning` passes 1 (0x004FEC38). The callers' bool is what selects the pair; `impl+0xA1` is not read by this function. <br>- The import log when the bool is set: "robot padding %f, obstacle padding %f , didBlocksChange %d" (0x004FD51A/0x004FD546). <br>- Each obstacle polygon is radially expanded by the obstacle padding (`ConvexPolygon::RadialExpand` 0x004FDF9E) and stored per heading with the constant penalty **0.1** (0x3DCCCCCD at 0x004FE0CE) through `xythetaEnvironment::AddObstacleWithExpansion(obstacle, robot, theta, 0.1f)` 0x00855528, which calls `ExpandCSpace` 0x008550E8. <br>- `IsInCollision(State)` 0x008515BC tail-calls `IsInCollision(State_c)` 0x008515F8; `IsInSoftCollision` 0x00851708 and `GetCollisionPenalty` 0x008517B0 read the same per-theta table (env+0x44). | N3..N7 |
| M13-004 | IMPLEMENTATION_GAP | The primitive file's schema and its arcs/turn costs. <br>- Per-action keys read by `ActionType::Import` 0x00851A18: `extra_cost_factor` → +0x00 (0x00851A96), `index` → +0x04 (0x00851AB8), `name` → +0x08 (0x00851ADC), `reverse_action` → +0x14 (0x00851B48); the ctor 0x008519EC initialises +0x04=0xFF and +0x08="<invalid>". <br>- Per-primitive keys read by `MotionPrimitive::Create` 0x00853DD0: `action_index` → +1 (0x00853DE8/0x00853E06), `end_pose` → +8 via `State::Import` (0x00853E20/0x00853E30), `intermediate_poses` → vector at +0x14 (0x00853E3E), `straight_length_mm` (0x00854088/0x00854094, then `asFloat` 0x008540D0 and `Path::AppendLine` 0x00854140), the `arc` block (`sweepRad` 0x00854160, `radius_mm` 0x0085417C, `centerPt_x_mm` 0x008541CE, `centerPt_y_mm` 0x008541E4, `startRad` 0x0085420C) → `Path::AppendArc` 0x0085425A, and `turn_in_place_direction` → `Path::AppendPointTurn` 0x0085431A. A per-primitive `extra_cost_factor` is rejected (0x00853FFC). <br>- The asset (sha256 above): `resolution_mm` 10.0, `num_angles` 16, `angle_definitions` the 16 listed angles, **9 actions** ("short straight" 1.0001, "long straight"/arcs 1.0, both in-place turns 2.0, "backwards short straight" 1.2 with `reverse_action` true) and 16 angles × 9 primitives = 144. Each arc is stored already rotated for its starting heading (angle 4 action 2 has centre (-94.72135954999578, 7.639320225002117), radius 94.72135954999578, startRad 0, sweepRad 0.46364760900080615). | N2, G1b, G1c, G2 |
| M13-005 | IMPLEMENTATION_GAP | A lattice-planner failure sends no path. `LatticePlannerImpl::DoPlanning` 0x00500090 has no substitute path: the non-empty-plan branch appends the engine's own `GetPlan()` result (0x00500540) and returns; nothing constructs a fallback. The failure condition is **Replan == 0** (0x00500106 saves the result; 0x00500136 selects "robot.lattice_planner_failure" when it is zero; 0x00500202 `cbz` returns 0). A failed plan therefore ends the action rather than driving a guessed line. | N10, G4d, G10 |
| M13-006 | IMPLEMENTATION_GAP | The whiteboard keeps a list of beacons and the active one is the first. `AIWhiteboard::AddBeacon` 0x0056C39C appends a 20-byte `AIBeacon` (a `Pose3d`, a float radius at +0xC, an int at +0x10) to the vector at whiteboard+0x60, growing it through `__emplace_back_slow_path` (0x0056C3A8/0x0056C3D2). `ClearAllBeacons` 0x0056AA08 walks it back (0x0056AA10 `subs r0,#0x14`). `GetActiveBeacon` 0x0056C404 compares begin/end and returns begin, or null when equal (0x0056C408/0x0056C40C) — the oldest still standing. | N25 |
| M13-007 | IMPLEMENTATION_GAP | The stack tolerance is 30 mm when building a stack and 15 mm everywhere else. `BlockWorld::FindObjectOnTopOrUnderneathHelper(obj, float tolerance, filter, bool)` 0x0062601C takes the tolerance as its second argument (r2 captured at 0x00626038 into the lambda at 0x006260CE/0x006260D8), reads the object's height through `GetDimInParentFrame<(char)90>` (0x00626070), adds ±half of it to the parent Z (0x0062607E..0x006260A0) and calls `FindLocatedObjectHelper` (0x0062610E). Callers: `StackOfCubes::BuildTallestStackForObject` passes **30** (0x41F00000 at 0x0061928C and 0x00619344); `BlockWorld::UpdatePoseOfStackedObjects` 0x00621908, `DockingComponent::CanInteractWithObjectHelper` 0x0063C728 and `CarryingComponent::SetObjectAsAttachedToLift` 0x00632F0C pass **15** (0x41700000). **Correction C-R1:** the helper has eleven callers, not four: RollBlockHelper (0x005BA136) and BuildTallestStackForObject (0x0061929E, and the underneath search 0x00619358) use 30.0; the other eight use 15.0. The helper's predicate lambda (0x0062B9FC) is part of this record. **Correction C-R5:** the found-object test is the reference and candidate footprints intersecting (Quadrilateral::Intersects), not a planar 22 mm test; only the Block override is read (M13-023). **Correction C-R9:** the edge test and the triangle test are single-precision with the operation order recorded in the record (denominator and directions in float, only the +-1e-4 compare in double). | N26, G3 |
| M13-008 | IMPLEMENTATION_GAP | The mount sequence and the reverse onto the charger. `MountChargerAction::ConfigureTurnAndMountAction` 0x0054E500: a `TurnInPlaceAction` at the atan2 of the vector to the marker (0x0054E52E/0x0054E536) with `SetMaxSpeed(0x3FDF66F3 = 1.745329 rad/s = 100 deg/s)` (0x0054E550/0x0054E55A) and `SetAccel(0x40A78D36 = 5.235988 rad/s^2)` (0x0054E55E/0x0054E568); if the lift is below 45.0, `MoveLiftToHeightAction(45, 5, 0)` (0x0054E58A..0x0054E5C6); then `DriveStraightAction(-120 mm, 30 mm/s, false)` whose vtable is overwritten with `BackupOntoChargerAction`'s (0x0054E5FC..0x0054E618). `BackupOntoChargerAction::CheckIfDone` 0x0054E7A8: on the contacts (robot+0x338) `Robot::SetPoseOnCharger()` and return 0 (0x0054E7B0..0x0054E7BE); not on contacts, `GetPitchAngle` compared with -0.261799 rad (0xBE860A92 at 0x0054E7CC), **pitch below returns 0x0400000A** (0x0054E7DE `adds r4,#4` after 0x04000006), fall-through returns **0x04000006** when `DriveStraightAction::CheckIfDone` returns 0 (0x0054E7E4..0x0054E7EC). `MountChargerAction::CheckIfDone` 0x0054E2D0 reaches the pi/2 comparison (0x3FC90FDB at 0x0054E374) only when the turn-and-mount sub-action FAILED (0x0054E31A skips success 0 and running 0x1000000); inside the window the failure stands, outside it `ConfigureDriveForRetryAction` 0x0054E72C runs `DriveStraightAction(120, 100, false)` returning **0x04000006** (0x0054E3E8/0x0054E742/0x0054E748). An align failure ends the action: the turn-and-mount is configured only after the align returns success (0x0054E2FA). | N16, N17, N18, G8 |
| M13-009 | IMPLEMENTATION_GAP | The charger's dimensions, its docked pose and its one pre-dock pose. `Charger::Charger` 0x004E9B6C stores **96.0/80.0/31.0** at +0xF0..+0xF8 (0x004E9B98/0x004E9BB4/0x004E9BB0/0x004E9BB8) and adds one marker: id **2** (0x004E9C16), pose angle **-pi/2** about Z (0xBFC90FDB, 0x004E9BBC) at **(86, 0, 22)** (0x004E9BD6/0x004E9BE2), size `Point2` with **x = 27.0 at sp+0x14** and **y = 20.0 at sp+0x18**, the pointer passed to `AddMarker` being sp+0x14 (0x004E9C28/0x004E9C30/0x004E9C36/0x004E9C38). `GetRobotDockedPose` 0x004EA1A0 = `Pose3d(Radians(pi), Z_AXIS, (30, 0, 0))` on the charger's pose (0x004EA1AC/0x004EA1C6/0x004EA202). `GeneratePreActionPoses` 0x004E9FB0 emits one pose for action types 0 and 1 only (`cmp r5,#1`/`bhi` at 0x004E9FD4/0x004E9FD6): `Pose3d(Radians(p.angle + pi/2), Z_AXIS, (p.x, -p.y, -15.5))` parented to the marker (0x004E9FE4/0x004E9FF2/0x004EA012/0x004EA018/0x004EA01E); `p` is the file-static Pose2d at 0x01059148 initialised `(Radians(0), 0.0, 250.0)` (0x004D6BC4, 0x004D6BD6). | N13, N14, N15, G9 |
| M13-010 | IMPLEMENTATION_GAP | The workouts run in file order and the last one repeats. `WorkoutComponent::GetCurrentWorkout` 0x00573DE8 returns the pointer at +0xC (0x00573DE8/0x00573DF0). `CompleteCurrentWorkout` 0x00573DEC triggers the workout's emotion event (`MoodManager::TriggerEmotionEvent` 0x00573E1C) and advances the pointer by one 0x40-byte entry unless it is the last (`subs r1,#0x40` 0x00573E24; `addne r0,#0x40` 0x00573E28/0x00573E2A). `ShouldPlayEightiesMusic` 0x00573E30 caches its answer at +0x11 and otherwise scores the current workout. **Correction C-R2:** ShouldPlayEightiesMusic has two callers outside this layer (0x00592416, 0x005D8014); GetHistoryValueTicksAgo needs the M7-mood ring buffer (capacity 0x80, initial {0,0} sample). | N27 |
| M13-011 | IMPLEMENTATION_GAP | The three path segment messages are a `Planning::PathSegment` copied field for field. `PathDolerOuter::Dole` 0x00507E4C switches on the segment type at `PathSegment+0` (1 line, 2 arc, 3 point turn; 0x00507F40/0x00507F44/0x00507F48) and copies words at +4, +8, +0xC, +0x10 (arc adds +0x14), then the speed profile at +0x1C, +0x20, +0x24; a point turn also copies the byte at +0x14 (line 0x00507FB6..0x00507FD2; arc 0x00508008..0x00508028; point turn 0x00507F5E..0x00507F7E). | N11, N12 |
| M13-012 | IMPLEMENTATION_GAP | The mount raises the lift to 45 mm when it is below it, not when it is above. `ConfigureTurnAndMountAction` reads `Robot::GetLiftHeight` (0x0054E58A) and branches past the lift move when the lift is at or above 45.0 (`vldr s2,[pc,#0x14c]` = 0x42340000; `bpl` at 0x0054E59E); otherwise `MoveLiftToHeightAction(45, 5, 0)` (0x0054E5B2..0x0054E5C6). | N16, G8 |
| M13-013 | IMPLEMENTATION_GAP | `DriveOffChargerContactsAction`. Constructor 0x00558228: `DriveStraightAction(10 mm, 20 mm/s, false)` (0x00558232/0x00558236/0x0055823E), +0x44 = 7, and **in SDK mode only** `SetTracksToLock(0)` (0x0055827C `IsInSdkMode`; 0x00558286/0x00558288) — the SDK clear is in the constructor, not `Init`. `Init` 0x005582D0 copies the robot's on-contacts flag robot+0x338 into action+0x8B (0x005582D2/0x005582D6) and returns 0 when not on contacts. `CheckIfDone` 0x005582E4 retries while the drive is still running and fails **0x04000009** if the robot is still on the contacts (0x00558344). | N19, G7a |
| M13-014 | IMPLEMENTATION_GAP | Knock over a stack: `BehaviorKnockOverCubes` (body 0x005C2EA0..0x005C3E2A). The behaviour-changing path is read and the record is raised from EQUIVALENT_IMPLEMENTATION to the engine's own flow (gap pass 2, G5). `IsRunnableInternal` 0x005C314C needs `StackOfCubes::GetStackHeight() >= +0x124` (minimumStackHeight, default 3). `InitInternal` 0x005C31A2 runs the reach unless +0xD9 (alwaysStreamline) or +0xD8 is set. `TransitionToReachingForBlock` 0x005C3254: `TurnTowardsObjectAction(max pi)`; if the block's x + 10 > 85.0 a `DriveStraightAction(x - 85, 60)`; a `TriggerLiftSafeAnimationAction(+0x150)`; then `TransitionToKnockingOverStack` regardless of result. `TransitionToKnockingOverStack` 0x005C34A8: `TurnTowardsObjectAction(max pi)`, `DriveAndFlipBlockAction(..., maxTurn = pi/2 on the first attempt, 0.0 once +0x140 > 0, at 0x005C36BC/0x005C36C0)`, `WaitAction(0.5)`; the callback 0x005C3DCE writes the block to `AIWhiteboard+0x70` on `NoPreActionPoses` (0x03000010), re-runs the knock-over while +0x140 <= 1 on a Retry result and otherwise blind-flips, and increments +0x140 either way; success goes to `TransitionToPlayingReaction` 0x005C3908, which selects the success trigger +0x15C or failure trigger +0x160 by the tipped-object set size at +0x14C. `IDriveToInteractWithObject` 0x0055B1F4 adds **two** actions when maxTurn > 0 (0x0055B37C..0x0055B392): a `TurnTowardsLastFacePoseAction` (vtable overwritten from `TurnTowardsFaceAction` at 0x0055B3C4..0x0055B3D8) **and** a `TurnTowardsObjectAction` (0x0055B42E/0x0055B43C), both with failure ignored. The trailing float 20.0 is not read by `DriveAndFlipBlockAction`'s ctor (0x0055E208). **Correction C-R3:** TurnTowardsLastFacePoseAction is a TurnTowardsFaceAction (face id 0) with a swapped vtable/destructor, not an M11 behaviour; the 'sayName' bool of G5d is the caller's argument. **Correction C-R6:** face id 0 is the invalid SmartFaceID (last observed face); flag set -> 0x0300000E, flag clear -> success; 'verified' = the SmartFaceID at +0x18C is valid. **Correction C-R13:** the derived constructor adds FlipBlockAction with AddAction(flip, false, false) (0x0055E2EE, 0x0055E2F0): the earlier 'flags not read' text is stale. | N22, G5a..G5g |
| M13-015 | IMPLEMENTATION_GAP | Pop a wheelie: one retry, the realign or retry animation, and what a failure marks. `TransitionToPerformingAction(Robot&, bool)` 0x005C7758 bumps the retry count at +0x12C when called as a retry (0x005C777E) and zeroes it otherwise (0x005C780C). The completion lambda 0x005C7CBC splits on `result >> 24`: success sets +0x128 = -1, plays `SuccessfulWheelie` 0x21C and reports objective 0x16 and the needs action; a Retry-category result calls `SetupRetryAction` only while the count is at most 0 (one retry), otherwise `AIWhiteboard::SetFailedToUse(obj, 3)`. `SetupRetryAction` 0x005C79D0 plays `PopAWheelieRealign` 0x18D for exactly `0x04000001` (0x005C79F8/0x005C79FE) and `PopAWheelieRetry` 0x18E otherwise (0x005C7A10/0x005C7A32). `ResetBehavior` 0x005C76B0 sends `EnableStopOnCliff(true)` (0x005C76E4). | N24 |
| M13-016 | IMPLEMENTATION_GAP | `AlignWithObjectAction`'s alignment-type table and pre-action type. `cmp r6,#3`/`bhi` 0x005533CC/0x005533E4; `tbb [pc,r6]` 0x005533E6 with table base 0x005533EA = `02 05 09 0c` (TBB scales by 2): **type 0 → 0x005533EE `vmov.f32 s16,#6.0` (distance 6.0)**; **type 1 → 0x005533F4 flag +0xBB = 2 (distance stays 0.0)**; **type 2 → 0x005533FC `vmov.f32 s16,#-15.0`**; **type 3 → 0x00553402 `vadd.f32 s16,s0,s2` (argument + -27.0)**. The distance is clamped to 0.0 when it is below **-16.000009536743164** (0xC1800005 at 0x00553480; `vcmpe`/`it mi`/`vmovmi` at 0x0055341C..0x00553426). `GetPreActionTypeFromAlignmentType` 0x005532B8: table at 0x00553360 maps type 0→1, 1→0, 2→1, 3→1; an invalid type logs and returns 1 (0x005532CC/0x00553320). | N23, G6b, G6c |
| M13-017 | IMPLEMENTATION_GAP | `BehaviorDriveOffCharger`. Constructor 0x005C0980 stores **96.0 + `<json extraDistanceToDrive_mm>`** at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6). `IsRunnableInternal` 0x005C0B10 returns robot+0x34A. `InitInternal` 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions unless robot+0x355 is set. `TransitionToDrivingForward` 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08). `UpdateInternal` 0x005C0DA8: while still on the contacts and robot+0x355 is set it calls `StopActing` (0x005C0DC4) and waits; once off the contacts (robot+0x34A == 0) it records the time in the whiteboard and returns 2 (0x005C0DF4..0x005C0E0A). | N20, G7b |
| M13-018 | IMPLEMENTATION_GAP | `LatticePlannerImpl`'s planning entry and worker. `StartPlanning` 0x004FEB44 stores its bool argument at impl+0xA1 (0x004FEB56/0x004FEB7E) and calls `ImportBlockworldObstaclesIfNeeded` with the bool **hard-coded 1** (0x004FEC38/0x004FEC3A). `DoPlanning` 0x00500090 sets status 1 (0x00500098), sleeps in chunks of **min(remaining, 10) ms** up to impl+0x108 ms, checking the abort flag each iteration (0x005000A2..0x005000EA; `cmp r0,#0xa`/`movge r4,#0xa` at 0x005000C8/0x005000CE), then calls `Replan(0x01C9C380, abortFlag)` (0x005000F2/0x005000FA/0x00500102). `0x01C9C380 = 30,000,000` is the **maximum number of state expansions**: `xythetaPlannerImpl::ComputePath` 0x008586A0 compares the count and on overflow warns "exceeded max expansions of %u, stopping" and returns 0 (0x008588C6/0x008588CA/0x00858A96). `Replan` returns 0 on failure, 1 on success. `DoPlanning` returns **0 when Replan == 0**, **3 when Replan != 0 and the plan's segment list is empty** (0x00500202/0x00500212) and **2 on success** (0x0050058E/0x00500590). | N8, N9, N10, G4a..G4c |
| M13-019 | IMPLEMENTATION_GAP | `xythetaEnvironment::Init` hard-codes 16 headings. After `ReadMotionPrimitives` succeeds (0x008528AE), it **overwrites env+8 with 0x10** (0x008528B6/0x008528BA), resizes the per-theta obstacle table at env+0x44 to 16 (0x008528C0) and sets env+0xC = 2π/16 and env+0x10 = 1/(2π/16) (0x008528C4..0x008528E6). The planner's heading count is therefore 16 regardless of the JSON `num_angles` (which the asset also sets to 16, so they agree). | G1f |
| M13-020 | IMPLEMENTATION_GAP | PanAndTiltAction, TurnTowardsPoseAction::CheckIfDone and WaitForImagesAction. libcozmoEngine.so 3.4.0-1204: PanAndTiltAction::Init 0x00549C48 clears the compound (this+0x78), sets [+0xCF]=[+0x57], adds a TurnInPlaceAction(robot, [this+0x114], [this+0x124]) (tolerance from this+0x140, [tia+0xD8]=[this+0x126], and if [this+0x160] a max speed [+0x148] and acceleration [+0x14C]) and a MoveHeadToAngleAction(robot, angle, tol [this+0x150], Radians(0)) where the angle is Radians([this+0x11C]) if [this+0x125] else robot[+0x2FC]+[this+0x11C], sets [this+0xCE]=1 and returns the compound's Update() mapped {SUCCESS, RUNNING} -> 0 (0x00549C48..0x00549D60); PanAndTiltAction::CheckIfDone 0x00549D73 returns the compound's IActionRunner::Update() unchanged; TurnTowardsPoseAction::CheckIfDone 0x0054B011 returns 0 if [this+0x179] is set (set by Init when |pan| exceeds the max turn) else the compound's Update(); WaitForImagesAction ctor 0x0054CA64 / Init 0x0054CB68 / CheckIfDone 0x0054CC1C / handler 0x0054DD88: it subscribes to tag 0x43 RobotProcessedImage and counts messages with timestamp > [this+0x7C] whose vision modes contain the wanted mode (or any if the mode is 0x10) until the count reaches [this+0x78] **Correction C-R7:** TurnTowardsPoseAction::Init and the PanAndTiltAction defaults are read in full. | R-VIS gap 1 |
| M13-021 | RECOVERABLE_GAP | TurnInPlaceAction and MoveHeadToAngleAction bodies, and the callback-setter bodies of TurnTowardsFaceAction. libcozmoEngine.so 3.4.0-1204: PanAndTiltAction::Init (M13-020) constructs TurnInPlaceAction (0xE8 bytes) and MoveHeadToAngleAction (0xB8 bytes) and sets fields on them; their bodies were not read. TurnTowardsFaceAction::SetSayNameTriggerCallback 0x0054BB8C, SetNoNameTriggerCallback 0x0054BC6C and SetNoNameAnimationTrigger 0x0054BA84 install a caller-supplied std::function; only SetSayNameAnimationTrigger 0x0054B978 was read in full **Correction C-R8:** narrowed to the unread MovementComponent wire bodies, the eye-shift arguments, the completion union and the Preset constructor; the two action bodies are M13-022. **Correction C-R10:** narrowed to the MovementComponent and TrackLayerComponent bodies; the head angle functions are M13-026, the wrapper M13-027. | R-VIS gap 1 |
| M13-022 | IMPLEMENTATION_GAP | TurnInPlaceAction and MoveHeadToAngleAction. libcozmoEngine.so 3.4.0-1204 (re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q5, verified in re-analysis/research/20260929-R-VIS-verify-M13-gap2.md): TurnInPlaceAction ctor 0x005459D4 (type 0x28, tracks 4; +0x78 = 5.235988, +0x7C = 10.0, +0x80 = 25.0, +0xB0 = Radians(2 deg) 0x3D0EFA35, +0xC4 = [+0x78], +0xC8 = [+0x7C], +0xD8 = 1, subscribes to tag 0xC4 motorActionAck); SetMaxSpeed 0x00545C08 writes +0xC4 and sets +0xCC; SetAccel 0x00545D14; SetTolerance 0x00545D50 (minimum 2 degrees); IsOffTreadsStateValid 0x00545EC4; Init 0x00545FA0 (invalid off-treads -> 0x0300000A; +0xD0 = [robot+0x2B0]; absolute/relative target math; relative |angle| > +0x80*2pi -> 0x03000000; in position -> 0; MovementComponent::TurnInPlace failing -> 0x03000016; eye-shift when +0xD8; +0xAC = 0.5*|+0xA4| at 0x0054620C); IsBodyInPosition 0x00546374; CheckIfDone 0x0054642C (RUNNING until the ack, origin-change counter, 0x04000004 when stopped without progress, 0x0300000A tail); MoveHeadToAngleAction ctor 0x00547E40 (type 0x12, tracks 1; +0x90 = 15.0, +0x94 = 20.0, +0x9C = 1, +0x9D = 0; angle clamped to [-0.4363323, 0.7766715]; tolerance floor 2 degrees), IsHeadInPosition 0x005484F4, Init 0x00548534 (in position -> 0; MoveHeadToAngle failing -> 0x03000016; the +0xA0 write also on failure), CheckIfDone 0x005485CC (0x04000004 when stopped with +0xAD set); ack handlers 0x0054D3F8 and 0x0054D624 **Correction C-R11:** GetCompletionUnion (relocalizedCnt) and the MoveHeadToAngleAction Preset constructor are recorded. | R-VIS gap 2 |
| M13-023 | RECOVERABLE_GAP | The other classes' GetBoundingQuadXY overrides and cv::minAreaRect. libcozmoEngine.so 3.4.0-1204: only Block::GetBoundingQuadXY 0x004E62A2 (and the derived cube / ActiveCube vtables through thunk 0x004E690C) is read; MarkerlessObject slot +0x50 -> 0x00502C32 and the Vision::ObservableObject / CustomObject base -> 0x0087713A are relocations only; Charger, Ramp, MatPiece and HumanHead are unresolved; cv::minAreaRect (libopencv_imgproc.so) is unread and matters only for tilted objects **Correction C-R12:** narrowed to the corner-list writers, cv::Rodrigues, SortCornersClockwise ties and the bionic libm policy; minAreaRect is M13-024 and the class footprints are M13-025. | R-VIS gap 2 |
| M13-024 | IMPLEMENTATION_GAP | The exact footprint: cv::minAreaRect, convexHull, Sklansky_ and RotatedRect::points. libcozmoEngine.so 3.4.0-1204 and the shipped OpenCV 3.1.0 (re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q1, verified in re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q1: the port reproduced the real code bit for bit on 270 fresh inputs, 0 mismatches, and RotatedRect::points on 200 fresh rects): GetBoundingQuad<float> 0x004E6490 copies the points, calls cv::minAreaRect (the InputArray flags 0x8103000D = CV_32FC2), then RotatedRect::points into Quadrilateral(p0,p1,p2,p3), then SortCornersClockwise; a catch-all landing pad (0x004E65C6, LSDA range 0x004E64F4..0x004E6502 = the minAreaRect call only) logs sErrorF('GetBoundingQuad.CvMinAreaRectFailed.COZMO-1916', 'cvPoints.size=%zu%s') and falls back to the axis-aligned box (Rectangle::InitFromPointContainer, angle 0.0). cv::minAreaRect 0x0009C760 (libopencv_imgproc.so, inlining rotatingCalipers): convexHull(points, hull, clockwise=1, returnPoints=1) 0x00039174 (CHullCmpPoints 0x00037990; first-strict min/max y; upper half Sklansky_(ptr,0,maxy,-1,1) and Sklansky_(ptr,total-1,maxy,-1,-1); lower half Sklansky_(0,miny,1,-1) and (total-1,miny,1,1); the stop/check_idx rule); Sklansky_<float> 0x0003781A in float non-fused with pprev = stack[ss_old-4] (NOT -3); n <= 2 paths at 0x0009CDFE (midpoint, size (dist, 0), atan2) and 0x0009CE82; the calipers: vect and inv[i] = (float)(1.0/sqrt((double)dy*dy + (double)dx*dx)), first-strict left/right/top/bottom, orientation from the first non-zero double convexity, seq = {bottom, right, top, left}, minarea FLT_MAX, dp0..dp3 and c_i, the main index (first strictly greater), the tbb table at 0x0009CBE2 (bytes 36 02 07 0C) assigning (base_a, base_b) = (lx,ly), (ly,-lx), (-lx,-ly), (-ly,lx), the area formulas, 'area <= minarea keeps the LAST minimal' (bhi skips), the result construction (C1, C2, idet, px, py, o1, o2, centre, sizes as float(sqrt(double))), angle = (float)((float)angle_rad * 180.0f / pi); RotatedRect::points 0x00086188 (libopencv_core.so): _a = (double)angle * pi / 180.0, b = (float)cos(_a)*0.5f, a = (float)sin(_a)*0.5f, pt0 = (cx - a*h - b*w, cy + b*h - a*w), pt1 = (cx + a*h - b*w, cy - b*h - a*w), pt2 = 2c - pt0, pt3 = 2c - pt1, each a separate float op. The validated float32 port is re-analysis/evidence/m13-minarearect/port.py. Yaw-only footprints give a RotatedRect with angle -90 and a w/h swap (e.g. a 44 cube: (0,0,44,44,-90), points (22,22),(-22,22),(-22,-22),(22,-22)) | R-VIS gap 3 |
| M13-025 | IMPLEMENTATION_GAP | GetBoundingQuadXY for every class: Markerless, the base ObservableObject version and the canonical corner lists. libcozmoEngine.so 3.4.0-1204 (re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q2, verified in re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q2): slot +0x50 resolves by relocation to the Block thunk 0x004E690C -> Block::GetBoundingQuadXY 0x004E62A2 (Block, Block_Cube1x1, Block_2x1, ActiveCube), MarkerlessObject::GetBoundingQuadXY 0x00502C32 (vptr 0x0101F318) and, for CustomObject (0x0101E9D8), HumanHead (0x0101EF18), Charger (0x0101E1C4), Ramp (0x0101FB1C), MatPiece (0x0101F43C), FlatMat (0x0101EC74) and the base sub-vtables, Vision::ObservableObject::GetBoundingQuadXY 0x0087713A; the slot +0x54 is each class's GetCanonicalCorners. MarkerlessObject: the 8 unit corners of +-0.5 (0x005025F0), size (this+0x58, +0x5C, +0x60) each + 2*padding, component-wise scale, Rotation3d::operator*, keep (x, y), GetBoundingQuad<float>, translate by the pose (x, y) via 0x004E68CC. Base version 0x0087713A: corners from vtable[+0x54] (12-byte Point3f elements), R = atPose.GetTransform().GetRotationMatrix(); each coordinate p (x, y, z in order) += (signbit(p) ? -padding : +padding) (__signbitf; -0.0 is negative) with NO size scaling; x' = (x*m0 + y*m1) + z*m2, y' = (x*m3 + y*m4) + z*m5 (float non-fused; z' discarded); GetBoundingQuad<float>; translate by the pose (x, y). Canonical corners: Charger 0x004E9A50: x in {0, 96}, y in {-40, 40}, z in {0, 31}, order (96,-40,0), (0,-40,0), (0,40,0), (96,40,0), (96,-40,31), (0,-40,31), (0,40,31), (96,40,31); Ramp 0x0050E2D0: x in {172, 222}, y in {-37.25, 37.25}, z in {0, 44}, order (222,-37.25,0), (172,-37.25,0), (172,37.25,0), (222,37.25,0), then the same four at z=44; HumanHead 0x004F8454: unit +-0.5 corners, unscaled. Rotation3d::operator*(Point3) 0x0084ACA6 -> UnitQuaternion_<float>::operator* 0x00849C1C: A=a*a, B=b*b, C=c*c, D=d*d; P00 = ((A+B)-C)-D; P11 = ((A-B)+C)-D; P22 = ((A-B)-C)+D; ab2, ac2, ad2, bc2, bd2, cd2 = each product rounded then doubled; R01 = bc2-ad2; R10 = bc2+ad2; R02 = ac2+bd2; R20 = bd2-ac2; R12 = cd2-ab2; R21 = ab2+cd2; out.x = (R02*vz) + ((P00*vx) + (R01*vy)); out.y = (R12*vz) + ((P11*vy) + (R10*vx)); out.z = (P22*vz) + ((R20*vx) + (R21*vy)) | R-VIS gap 3 |
| M13-026 | IMPLEMENTATION_GAP | GetAbsoluteHeadAngleToLookAtPose and Robot::ComputeHeadAngleToSeePose. libcozmoEngine.so 3.4.0-1204 (re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q3, verified in re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q3): TurnTowardsPoseAction::GetAbsoluteHeadAngleToLookAtPose 0x0054B428: h = z + (-49.0) (0xC2440000); d = sqrt(x*x + y*y) (float vsqrt, NaN falls back to sqrtf); D = d + 13.0; a = (300.0 - D)/150.0 (0x43960000, 0x43160000); b = (0.0 - h)/-10.0; t1 = clamp01(a) * 0.0872664601 (5 degrees) and t2 = clamp01(b) * 0.130899698 (7.5 degrees) (0 for <=0 or NaN, the value for 0<x<1, 1 for >=1); A = atan2f(h, D); result = ((t2 + (A + t1)) + 0.0698131695 (4 degrees, 0x3D8EFA35)) as Radians. Robot::ComputeHeadAngleToSeePose 0x00518344: GetWithRespectTo(pose, robot+0x2CC) failing -> warning and 0x06000000; p = (sqrt(tx*tx+ty*ty), 0, tz); the camera copied from [robot+0x258]+0x24, a null calibration -> error and 1; tol = (float)calibration.nrows * f + 9.99999975e-06; theta = 0, index 1; each iteration: camPose = Robot::GetCameraPose(theta), inv = camPose.GetInverse(), q = T.rotation * p + T.translation; if not q.z > 1e-5f -> 'BadProjectedZ' warning and 1; dv = calibration.focal_y * (q.y/q.z); converged iff tol >= |dv| (writes headAngle = theta via Radians::operator=(float), returns 0 unless the index is 25 -> 'MaxIterations' warning and 1); else theta += -0.8f * atan2f(dv, focal_y); after 25 non-converged iterations the code returns 0 (success) WITHOUT writing headAngle (r0 = 26 passes the 'cmp r0,#0x19; bne' test). Confirmed by reading (register trace) in the verifier; not emulated | R-VIS gap 3 |
| M13-027 | IMPLEMENTATION_GAP | TurnTowardsFaceWrapperAction. libcozmoEngine.so 3.4.0-1204 (re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q5, verified in re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q5): TurnTowardsFaceWrapperAction ctor 0x0054C7DC (Robot&, IActionRunner*, bool A, bool B, Radians, bool C): CompoundActionSequential base with the wrapper vtable (GOT 0x0103EB70); if A: a TurnTowardsLastFacePoseAction (TurnTowardsFaceAction ctor with face id 0, Radians copy and bool C, vtable overwritten with GOT 0x0103EA0C) added with AddAction(action, false, false); then the wrapped action added the same way; if B: a second TurnTowardsLastFacePoseAction added the same way; then SetProxyTag([action+0x60]); order [turn-before if A][wrapped][turn-after if B] (0x0054C7EA..0x0054C8CA). The flag names were read from register order (A = r3, B = [sp+0x60], Radians at [sp+0x64], C at [sp+0x68]); the caller was not read | R-VIS gap 3 |
| M13-028 | IMPLEMENTATION_GAP | FlipBlockAction::Init and DriveAndFlipBlockAction's constructor guards. libcozmoEngine.so 3.4.0-1204 (fix-round-2 verifier, re-analysis/research/20260929-R-VIS-verify-M12-fix2.md objections 2, 3 and 9): FlipBlockAction::Init 0x0055EDC8: the drive distance is the THREE-D norm of the object pose with respect to the robot pose (0x0055EED6..0x0055EEFA: [T+0x20]^2, then a loop over [T+0x24]^2 and [T+0x28]^2, vsqrt.f32) plus [this+0x130] (a 44 mm cube at z 22 and 200 mm gives 201.2 mm, the planar norm gives 200); it adds MoveLiftToHeightAction first (0x0055EF3A..0x0055EF58) and DriveStraightAction second (0x0055EF64..0x0055EF70) into the embedded CompoundActionSequential at this+0x80 (constructed at 0x0055ECE0), so the drive starts only after the lift; it calls DisableReactionsWithLock (0x0055EEC6) and IActionRunner::Update on the compound (0x0055EF7E); DriveAndFlipBlockAction's constructor (IDriveToInteractWithObject 0x0055B258..0x0055B264): when the robot is carrying the object id it warns and adds NO drive, wait or turn actions (branch to 0x0055B45E); maxTurn > 0 uses Radians::operator> with the 1e-5 epsilon (0x0055B38C) | R-VIS fix round 2 |

## Decisions (the manager's, recorded for audit)

- **MD1. Standing decisions applied.** **SD1 (exact, always)** governs every row: all facts are the engine's instructions or the shipped asset, transliterated, with the exact literals (e.g. `0x3FDF66F3 = 1.745329`, `0x40A78D36 = 5.235988`, `0xC1800005 = -16.00000954`, the sha256 of `cozmo_mprim.json`). **No EQUIVALENT_IMPLEMENTATION was chosen**: M13-014, which was EQUIVALENT before this process, is now read from the engine (gap pass 2) and carries the engine's own flow. **SD2 (engine-undefined behaviour)** was not needed: no row found uninitialised memory, an out-of-bounds read or time-seeded randomness in this subsystem. **SD3 (work that depends on an unbuilt layer)** was not needed: the engine-side behaviour of every row is readable here; the only cross-layer inputs are named interfaces (MD3). **SD4 (housekeeping)** produced nothing to move.
- **MD2. M13-014's status.** It was EQUIVALENT_IMPLEMENTATION because the flow had not been read. Gap pass 2 read the whole behaviour-changing path (`BehaviorKnockOverCubes` and its callback, `DriveAndFlipBlockAction`/`IDriveToInteractWithObject`). The record is now an IMPLEMENTATION_GAP like the rest: the engine's flow is established, and the build job compares the stack against it. It is no longer an equivalence claim.
- **MD3. Interfaces.** The engine-side behaviour is recorded here; these inputs belong to other layers and are named in the records:
  - the face detector feeding `TurnTowardsLastFacePoseAction` (M11-vision) — `BehaviorKnockOverCubes` passes pi/2 on its first attempt, so without a face the turn is a no-op (M13-014);
  - `TurnInPlaceAction`'s rad/s units and 300 deg/s limit (M4-control, 0x00545C08/0x00545D14), used by M13-008;
  - carrying/docking objects (M12-manipulation) for `DockingComponent::CanInteractWithObjectHelper` and `CarryingComponent::SetObjectAsAttachedToLift` in M13-007.
- **MD4. New record boundaries.** The job's rows that had no record are split by behaviour: M13-016 (`AlignWithObjectAction`'s alignment-type table, from M13-002's bundle), M13-017 (`BehaviorDriveOffCharger`), M13-018 (`LatticePlannerImpl`'s entry/worker, including the `0x01C9C380` meaning and the result codes) and M13-019 (`xythetaEnvironment::Init`'s 16-heading override). M13-002 is narrowed to `FlipBlockAction`'s constants, since `AlignWithObjectAction` is now M13-016 and the charger geometry is M13-009.
- **MD5. Non-behavioural helper.** `FUN_005c0ca8` (a channel logger used by the behaviours) was not read and needs no record; it formats a log message only.
- **MD6. Unit ownership.** The turn-in-place max speed and accel are recorded in M13-008's evidence but the units and the 300 deg/s limit are `TurnInPlaceAction`'s (control layer); M13-008 only passes the literals.

## Corrections (R-VIS gap pass 1, 2026-09-29)

Source: R-VIS gap 1 (`20260929-R-VIS-M12-M13-gap1-extraction.md`, verified in `20260929-R-VIS-verify-M12-M13-gap1.md`; the earlier pre-extraction item 13 was verified in `20260929-R-VIS-verify-pre-items11-13.md`).

- **C-R1 (M13-007).** Eleven callers, two tolerances, one underneath search, and the predicate lambda are now the record's evidence.
- **C-R2 (M13-010).** Two callers found; GetHistoryValueTicksAgo is M7's ring buffer.
- **C-R3 (M13-014).** TurnTowardsLastFacePoseAction is the TurnTowardsFaceAction subclass; +0x193 and the callback slots have their writers.
- **C-R4 (new M13-020, M13-021).** PanAndTiltAction, TurnTowardsPoseAction::CheckIfDone and WaitForImagesAction are a record; the TurnInPlaceAction / MoveHeadToAngleAction bodies and the three unread callback-setter bodies are a RECOVERABLE_GAP.

**Open (not M13):** Emotion::Update's caller chain (M7), the NeedsManager internals (needs subsystem), FaceWorld and the RobotObservedFace content (M14).

## Corrections (R-VIS gap pass 2, 2026-09-29)

Sources: `20260929-R-VIS-M13-gap2-extraction.md`, verified in `20260929-R-VIS-verify-M13-gap2.md` (behavioural claims supported; omissions listed there and folded in here).

- **C-R5 (M13-007), C-R6 (M13-014), C-R7 (M13-020), C-R8 (M13-021)** as marked in the rows.
- **New M13-022** (TurnInPlaceAction and MoveHeadToAngleAction, IMPLEMENTATION_GAP) and **M13-023** (the other GetBoundingQuadXY overrides and cv::minAreaRect, RECOVERABLE_GAP).
- The M13-021 status move the extractor proposed (RECOVERABLE_GAP to IMPLEMENTATION_GAP) was refused: the MovementComponent wire bodies, the eye-shift arguments, the completion union and the Preset constructor are unread, so the record stays RECOVERABLE_GAP with those items named.

## Corrections (R-VIS gap pass 3, 2026-09-29)

Sources: `20260929-R-VIS-M13-gap3-extraction.md`, verified in `20260929-R-VIS-verify-M13-gap3.md` (PASS on the source facts; five wording objections folded in). The extractor's float32 port of `cv::minAreaRect` and its harness are in `re-analysis/evidence/m13-minarearect/`; a fresh run against the real OpenCV code reproduced it bit for bit on 270 inputs.

- **C-R9 (M13-007), C-R10 (M13-021), C-R11 (M13-022), C-R12 (M13-023)** as marked in the rows.
- **New M13-024** (the exact footprint: minAreaRect, convexHull, Sklansky_, RotatedRect::points), **M13-025** (GetBoundingQuadXY for every class and the canonical corner lists), **M13-026** (GetAbsoluteHeadAngleToLookAtPose and ComputeHeadAngleToSeePose, including the 25/26 iteration quirk) and **M13-027** (TurnTowardsFaceWrapperAction).
- **Decision needed (policy review, operator):** the footprint and head-angle numerics call the bionic libm `sin`, `cos`, `atan2` and `atan2f` in double before a float cast. bionic does not ship in the APK, so a host `Math` may differ by one ulp; this is EQUIVALENT_IMPLEMENTATION territory and is not claimed as EXACT_SOURCE.
- **Not built in this round:** M13-024..M13-027. The stack keeps the labelled planar stand-in (M13-007) until they are built.

## Corrections (R-VIS fix-round-2 verification, 2026-09-29)

Source: `20260929-R-VIS-verify-M12-fix2.md`. **New M13-028**: `FlipBlockAction::Init` drives the 3-D distance (not planar), adds the lift then the drive to the embedded compound, locks reactions, and `DriveAndFlipBlockAction`'s constructor adds nothing when the robot carries the object. **C-R13 (M13-014):** stale 'flags not read' text corrected.

## Existing record evidence found contradicted or too weak

- **M13-003** — contradicted on one clause: the padding pair is selected by `ImportBlockworldObstaclesIfNeeded`'s **own first `bool` argument**, not by "the planner's flag at impl+0xA1". The values 7/6 and 2/1 are right; the citation was wrong. Corrected in M13-003's evidence.
- **M13-009** — the marker size was written "20 x 27". The source stores **x = 27.0** at sp+0x14 (the pointer passed to `AddMarker`) and **y = 20.0** at sp+0x18. Corrected to x=27, y=20.
- **M13-002** — evidence too weak: two bare addresses (`FlipBlockAction 0x0055EC80`, `charger 0x004E9B6C`) with no instructions, and the title named four things. Re-cited with the instructions; `AlignWithObjectAction` and the charger moved to M13-016 and M13-009.
- **M13-005** — evidence too weak: three prose sentences with no address, and the prose stated the Replan condition backwards ("returns 0 when Replan is non-zero"). The source returns 0 when Replan **is zero**. Re-cited to 0x00500106/0x00500136/0x00500202 and the failure/no-fallback claim kept.
- **M13-014** — was EQUIVALENT_IMPLEMENTATION; the path is now read (MD2).
- **M13-012** — evidence carried the lift branch but not the turn-in-place numbers, which are now in M13-008's evidence (the two records share `ConfigureTurnAndMountAction`).
- **Corrections from the citation check and gap pass 2** (the X2/gap1 rows, now reflected above):
  - X2 N16: the mount's `TurnInPlaceAction` max speed is `0x3FDF66F3 = 1.745329 rad/s` (100 deg/s), not 1.7459.
  - X2 N17: `BackupOntoChargerAction::CheckIfDone`'s pitch-below return is **0x0400000A**, and the fall-through returns **0x04000006**; 0x04000004 appears nowhere in the cited ranges.
  - X2 N23 and gap1 G6b: the `AlignWithObjectAction` alignment-type table was rotated; the correct mapping is type 0→6.0, type 1→flag, type 2→-15.0, type 3→arg+(-27.0), and the clamp literal is -16.00000954, not -16.000001.
  - gap1 G5a: the knock-over maxTurn 0.0 entry is at 0x005C36C0, not 0x005C36C4 (which holds the pointer 0x00A7B382).
  - X2 N10 / gap1 G4/G10: the Replan condition was backwards (see M13-005).
  - X2 N19: the SDK `SetTracksToLock(0)` clear is in the constructor, not `Init` (corrected in M13-013).
  - X2 N20 / gap1 G7b: `BehaviorDriveOffCharger::UpdateInternal` records the time and returns 2 once off the contacts; `StopActing` is called while still on the contacts (corrected in M13-017).
  - X2 N22 / gap1 G5d: `IDriveToInteractWithObject` adds **two** actions when maxTurn > 0 (corrected in M13-014).

## Appendix A: X2 extraction report (2026-09-27)

﻿# X2 — M13-navigation extraction (read-only)

Job: re-analysis/jobs/X2.md, scope 1. Subsystem: M13-navigation — the lattice planner and
its motion primitives, obstacle expansion, path following, the charger (geometry, the mount,
driving off the contacts), FlipBlock, AlignWithObject, and pop-a-wheelie.
Agent: opencode (DeepSeek), window 3. Date: 2026-09-27.

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`. Addresses below are file VAs of the
ELF, disassembled with capstone in Thumb mode. Every branch target is annotated with the
dynamic symbol or PLT entry it lands on.

## 0. Method, and what this clone could not check

- The Ghidra decompilation `re-analysis/decomp/libcozmoEngine/` is absent in this clone
  (`index.tsv` missing). I worked from the `.so` directly.
- **`re-analysis/obb/` is empty in this clone.** The shipped `cozmo_mprim.json` — the whole
  subject of M13-001 and M13-004 — could not be opened. Those two records are therefore only
  checked on the engine side (the loader and parser), not on the asset contents. This is
  called out on the rows and in section 3.
- Earlier records are treated as claims to check, not evidence. Where a row reuses a record's
  citation I re-read the instruction in the `.so`.

## 1. Production path

Entry: a game message or a behaviour constructs an action; the planner runs on the engine's
own thread; `PathDolerOuter::Dole` sends the path segments to the robot.

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| N1 | Motion-primitive load | `LatticePlannerImpl` reads the primitive file through `xythetaEnvironment::ReadMotionPrimitives(path)` (0x008529C0): it parses JSON and calls `xythetaEnvironment::ParseMotionPrims(Json::Value const&, bool)` (0x004CE840), which calls `MotionPrimitive::Create` (0x00853DD0) per action. | 0x008529C0 (`fopen` 0x00852A48; `Json::Reader::parse` 0x00852A62; `ParseMotionPrims` 0x00852B06) | M13-001, M13-004 | EXACT_SOURCE for the loader; the file contents are **not re-checkable here** (OBB absent) |
| N2 | Primitive schema | `ParseMotionPrims` / `MotionPrimitive::Create` read the per-action fields; the record's keys (`extra_cost_factor`, `straight_length_mm`, `arc.centerPt_x_mm`, `radius_mm`, `startRad`, `sweepRad`) are the schema. | not transcribed in this pass; 0x004CE840 and 0x00853DD0 are the functions to read | M13-004 | RECOVERABLE_GAP (read the two functions) |
| N3 | Obstacle import | `LatticePlannerImpl::ImportBlockworldObstaclesIfNeeded(bool, ColorRGBA const*)` (0x004FD4B8) takes a recursive-mutex try-lock; on the miss it warns and returns. | 0x004FD4DC (`blx 0x4A658C recursive_mutex::try_lock`), 0x004FD4E0/0x004FD4E2 branch, 0x004FD574 warning | M13-003 | EXACT_SOURCE |
| N4 | The two paddings | The robot padding is 7.0 and the obstacle padding 6.0, or 2.0 and 1.0 when the **function's first bool argument** is non-zero. | 0x004FD4EE `vmov.f32 s16,#7.0`; 0x004FD4F2 `vmov.f32 s18,#6.0`; 0x004FD4E8 `cmp r4,#0`; 0x004FD4F6 `itt ne`; 0x004FD4F8 `vmovne.f32 s16,s2` (s2=2.0 from 0x004FD4EA); 0x004FD4FC `vmovne.f32 s18,s0` (s0=1.0) | M13-003 (wording wrong; see §2) | EXACT_SOURCE for the values, **contradicted** for the flag origin |
| N5 | Import log | When the caller's bool is set, it logs "robot padding %f, obstacle padding %f , didBlocksChange %d" with those two floats. | 0x004FD51A `cmp r4,#0` / `beq 0x4FD5CE`; 0x004FD546 `blx 0x4A5B84 sChanneledDebugF`; format string at the literal resolved from 0x004FD524/0x004FD528 | M13-003 | EXACT_SOURCE |
| N6 | Obstacle expansion | Each obstacle polygon is radially expanded by the obstacle padding and stored per heading with a constant penalty; `AddObstacleWithExpansion` and `ExpandCSpace`. | 0x00855528 (`AddObstacleWithExpansion`); 0x008550E8 (`ExpandCSpace`); penalty 0.1 passed by the caller | M13-003 | EXACT_SOURCE (addresses exist; body not re-transcribed here) |
| N7 | Collision query | `IsInCollision(State)` tail-calls `IsInCollision(State_c)`; the per-theta table and penalty are read by `IsInSoftCollision` / `GetCollisionPenalty`. | 0x008515BC (`IsInCollision(State)`), 0x008515F8 (`IsInCollision(State_c)`), 0x00851708 (`IsInSoftCollision`), 0x008517B0 (`GetCollisionPenalty`) | M13-003 | EXACT_SOURCE |
| N8 | Planning entry | `LatticePlannerImpl::StartPlanning` writes `impl+0xA1`, then calls `ImportBlockworldObstaclesIfNeeded` with the bool **hard-coded 1**, then plans. | 0x004FEB7E `strb.w r6,[sl,#0xA1]`; 0x004FEC38 `movs r1,#1`; 0x004FEC3A `blx 0x4A65B0` | NEW | EXACT_SOURCE |
| N9 | Planning worker | `DoPlanning` (0x00500090) first sleeps in 1 ms chunks up to `impl+0x108` ms, then calls `xythetaPlanner::Replan(arg=0x01C9C380, abortFlag)`. | 0x005000A2 `ldr r0,[fp,#0x108]`; 0x005000D4 `smull r0,r1,r4,r8` (r8=0x000F4240 = 1e6 ns); 0x005000DE `sleep_for`; 0x005000F2 `movw r1,#0xC380`; 0x005000FA `movt r1,#0x1C9`; 0x00500102 `blx 0x4A6850 Replan` | NEW | EXACT_SOURCE for the call; the meaning of 0x01C9C380 is UNKNOWN (not read) |
| N10 | Failure path | If `Replan` returns non-zero, `DoPlanning` returns 0; if it succeeds but `GetPlan()`'s segment list is empty, it returns 3. There is no substitute path in `DoPlanning`. | 0x00500126 `cmp r4,#0`; 0x00500202 `cbz r0,0x500216` (return 0); 0x00500206 `GetPlan`; 0x0050020E `cmp r0,r1`; 0x00500212 `movs r0,#3` | M13-005 | EXACT_SOURCE for the codes; the record's prose is uncited (see §2) |
| N11 | Path dole | `PathDolerOuter::Dole` (0x00507E4C) indexes the segment list, logs the segment, then switches on the segment type at `PathSegment+0` (1 line, 2 arc, 3 point turn) and copies the fields into the matching `AppendPathSegment*` message. | 0x00507F3A `ldr r0,[r4]`; 0x00507F3C `ldr.w r1,[r0,r6,lsl #3]`; 0x00507F40/0x00507F44/0x00507F48 (`cmp #1/#2/#3`); line ctor 0x00507FE0; arc ctor 0x00508036; point-turn ctor 0x00507F8E | M13-011 | EXACT_SOURCE |
| N12 | Segment fields | Line/arc copy words at +4,+8,+0xC,+0x10 (arc adds +0x14), then the speed profile at +0x1C,+0x20,+0x24; point turn also copies the byte at +0x14. | line 0x00507FB6..0x00507FD2; arc 0x00508008..0x00508028; point turn 0x00507F5E..0x00507F7E | M13-011 | EXACT_SOURCE |
| N13 | Charger geometry | `Charger::Charger` stores 96, 80, 31 at +0xF0..+0xF8, and adds one marker: id 2, pose angle -pi/2 about Z at (86,0,22), size `Point2{27.0, 20.0}`. | 0x004E9B98 `movt r2,#0x42C0` (96) / `strd r2,r1,[r4,#0xF0]`; 0x004E9BB8 `movt r0,#0x41F8` (31); 0x004E9BBC `movw r1,#0xFDB; movt r1,#0xBFC9` (-pi/2); 0x004E9BD6 (86), 0x004E9BE0 (22); 0x004E9C1C id 2; 0x004E9C28 `str r1,[sp,#0x18]` (20.0), 0x004E9C30 `str r1,[sp,#0x14]` (27.0); 0x004E9C38 `AddMarker` | M13-002, M13-009 | EXACT_SOURCE (size order differs from the record; see §2) |
| N14 | Docked pose | `Charger::GetRobotDockedPose` = `Pose3d(Radians(pi), Z_AXIS, (30,0,0))` on the charger's pose. | 0x004EA1AC `movw r1,#0xFDB; movt r1,#0x4049` (pi); 0x004EA1C6 `movt r0,#0x41F0` (30.0) stored at sp+0x28; 0x004EA202 `Pose3d` | M13-009 | EXACT_SOURCE |
| N15 | Pre-dock pose | `Charger::GeneratePreActionPoses` returns nothing for action type > 1; for types 0 and 1 it emits one pose `(p.angle + pi/2, (p.x, -p.y, -15.5))` parented to the charger marker. `p` is the file-static Pose2d at 0x01059148, initialised to `(Radians(0), 0.0, 250.0)` by the initialiser at 0x004D6BC4. | 0x004E9FD4 `cmp r5,#1`; 0x004E9FD6 `bhi 0x4EA120`; 0x004E9FE4 `vldr s0,[pc,#0x19C]` (=0x3FC90FDB pi/2); 0x004E9FF2 `vadd.f32 s0,s2,s0`; 0x004EA012/0x004EA01E (`-p.y`); 0x004EA018 `movt r0,#0xC178` (-15.5); static init 0x004D6BD6 `movt r3,#0x437A` (250) | M13-009 | EXACT_SOURCE |
| N16 | Mount sequence | `MountChargerAction::ConfigureTurnAndMountAction`: a `TurnInPlaceAction` at the atan2 of the vector to the marker, max speed 1.7459, accel 5.236; if the lift is below 45 mm, `MoveLiftToHeightAction(45, 5, 0)`; then a `DriveStraightAction(-120, 30, false)` whose vtable is overwritten with `BackupOntoChargerAction`'s. | 0x0054E52E `ComputeVectorBetween`; 0x0054E536 `atan2f`; 0x0054E550 `movw r1,#0x66F3; movt r1,#0x3FDF`; 0x0054E55E `movw r1,#0x8D36; movt r1,#0x40A7`; 0x0054E58A `GetLiftHeight`; 0x0054E592 `vldr s2,[pc,#0x14C]` (=0x42340000 45.0); 0x0054E59E `bpl`; 0x0054E5C6 `MoveLiftToHeightAction`; 0x0054E5FC `movt r2,#0xC2F0` (-120.0); 0x0054E600 `movt r3,#0x41F0` (30.0); 0x0054E608 `DriveStraightAction`; 0x0054E612 `add r0,pc`; 0x0054E618 `str r0,[r6]` (vtable) | M13-008, M13-012 | EXACT_SOURCE; the -120/30 reverse and the turn speeds are NEW |
| N17 | Backup end | `BackupOntoChargerAction::CheckIfDone`: on the charger contacts (`robot+0x338`) it calls `Robot::SetPoseOnCharger()` and succeeds; if the pitch is below -0.261799 rad it fails (0x04000004); otherwise it defers to `DriveStraightAction::CheckIfDone`. | 0x0054E7B0 `ldrb.w r0,[r1,#0x338]`; 0x0054E7B8 `SetPoseOnCharger`; 0x0054E7C2 `GetPitchAngle`; 0x0054E7CC `vldr s2,[pc,#0x24]` (=0xBE860A92); 0x0054E7DE `adds r4,#4`; 0x0054E7E4 `DriveStraightAction::CheckIfDone` | M13-008 | EXACT_SOURCE |
| N18 | Mount retry | `MountChargerAction::CheckIfDone` runs the align sub-action; on success it configures turn-and-mount; the turn-and-mount result reaches the pi/2 test only when it failed. Inside the window the failure stands; outside it warns and runs `ConfigureDriveForRetryAction` (`DriveStraightAction(120, 100, false)`), which returns 0x04000006. | 0x0054E2FA `ConfigureTurnAndMountAction`; 0x0054E324 `GetLocatedObjectByIdHelper`; 0x0054E344/0x0054E358 `GetAngleAroundZaxis`; 0x0054E374 `vldr s2,[pc,#0xA8]` (=0x3FC90FDB); 0x0054E380 `ble`; 0x0054E3D4 `ConfigureDriveForRetryAction`; 0x0054E742 `movt r2,#0x42F0` (120.0); 0x0054E748 `movt r3,#0x42C8` (100.0); 0x0054E3E8 `movs r5,#6; movt r5,#0x400` | M13-008 | EXACT_SOURCE |
| N19 | Drive off contacts | `DriveOffChargerContactsAction` is a `DriveStraightAction(10, 20, false)`; its `Init` copies the robot's on-contacts flag (`robot+0x338`) into the action and, **in SDK mode only, calls `SetTracksToLock(0)`**; `CheckIfDone` retries while the drive is still running and fails 0x04000009 if the robot is still on the contacts. | 0x00558232 `movt r2,#0x4120` (10.0); 0x00558236 `movt r3,#0x41A0` (20.0); 0x0055823E `DriveStraightAction`; 0x0055827C `IsInSdkMode`; 0x00558286 `movs r1,#0`; 0x00558288 `SetTracksToLock`; 0x005582D2 `ldrb.w r1,[r1,#0x338]`; 0x005582E4 `CheckIfDone`; 0x00558344 `movs r0,#9; movt r0,#0x400` | M13-013 | EXACT_SOURCE; the SDK track-lock clear is NEW |
| N20 | Drive-off behaviour | `BehaviorDriveOffCharger`'s constructor stores `96.0 + <json extraDistanceToDrive_mm>` at +0x11C; `IsRunnableInternal` returns `robot+0x34A`; `InitInternal` locks reactions and pushes driving animations; `TransitionToDrivingForward` drives that distance; `UpdateInternal` stops acting when the robot leaves the contacts. | 0x005C09D4 `vldr s0,[pc,#0xD4]` (=0x42C00000 96.0); 0x005C09E2 `vadd.f32 s0,s16,s0`; 0x005C09E6 `vstr s0,[r4,#0x11C]`; 0x005C0B10 `ldrb.w r0,[r1,#0x34A]`; 0x005C0B2A `SmartDisableReactionsWithLock`; 0x005C0B4A `PushDrivingAnimations`; 0x005C0C02 `ldr.w r2,[r4,#0x11C]`; 0x005C0C08 `DriveStraightAction`; 0x005C0DC4 `StopActing` | M13-013 | EXACT_SOURCE |
| N21 | FlipBlockAction | Constructor: `IAction` type 0xF; +0x12C=150.0, +0x130=20.0, +0x134=45.0, +0x138=40.0, +0x13C=-1, +0x140=1. | 0x0055ECAA `IAction`; 0x0055ECF2 `movt r0,#0x41A0` (20.0); 0x0055ECF6 `movt r1,#0x4316` (150.0); 0x0055ECFA `strd r1,r0,[r4,#0x12C]`; 0x0055ECFE `movt r2,#0x4234` (45.0); 0x0055ED02 `movt r3,#0x4220` (40.0); 0x0055ED06 `mov.w r7,#-1`; 0x0055ED0E `stm r0!,{r2,r3,r7}`; 0x0055ED12 `strb.w r0,[r4,#0x140]` | M13-002 | EXACT_SOURCE (the record does not state these; see §2) |
| N22 | Drive-and-flip | `DriveAndFlipBlockAction(Robot&, ObjectID, bool, float, bool, Radians maxTurn, bool, float)`; `IDriveToInteractWithObject` adds a `TurnTowardsLastFacePoseAction` only when `maxTurn > Radians(0)` and ignores its failure. | 0x0055E208 (ctor); 0x0055B1F4 (`IDriveToInteractWithObject`); 0x0055B37C/0x0055B392 (angle test); 0x0055B3D4 (`AddAction(action,true)`) | M13-014 | EXACT_SOURCE (entry points) |
| N23 | AlignWithObject | `AlignWithObjectAction` maps the alignment type (0..3) to a distance and a pre-action type: type 0 = `arg + (-27.0)`, type 1 = 6.0, type 2 sets a flag, type 3 = -15.0; the distance is clamped to 0 when below -16.000001. | 0x005533E6 `tbb [pc,r6]`; 0x005533EE `vmov.f32 s16,#6.0`; 0x005533FC `vmov.f32 s16,#-15.0`; 0x00553402 `vmov.f32 s2,#-27.0`; 0x00553406 `vadd.f32 s16,s0,s2`; 0x00553414 `vldr s0,[pc,#0x68]` (=0xC1800005); 0x00553424 `it mi`; 0x00553426 `vmovmi.f32 s16,s2` (0.0); 0x00553410 `GetPreActionTypeFromAlignmentType` | M13-002 | EXACT_SOURCE; the record's evidence does not carry these |
| N24 | Pop-a-wheelie | `BehaviorPopAWheelie` retries at most once: the completion lambda splits on `result >> 24`; a Retry-category result calls `SetupRetryAction` only while the count is at most 0, otherwise marks `SetFailedToUse(obj, 3)`; success sets +0x128 = -1, plays SuccessfulWheelie 0x21C and reports objective 0x16 and the needs action. `SetupRetryAction` uses animation 0x18D for exactly `0x04000001`, else 0x18E, then re-runs with retry=true. `ResetBehavior` sends `EnableStopOnCliff(true)`. | 0x005C7758 (`TransitionToPerformingAction`); 0x005C777E `adds r1,#1`; 0x005C780C `str.w r0,[r4,#0x12C]`; 0x005C76E4 `EnableStopOnCliff`; 0x005C79D0 (`SetupRetryAction`); 0x005C79F8/0x005C79FE (compare 0x04000001); 0x005C7A10 `movw r2,#0x18D`; 0x005C7A32 `mov.w r2,#0x18E` | M13-015 | EXACT_SOURCE |
| N25 | Whiteboard beacons | `AIWhiteboard::AddBeacon` appends a 20-byte `AIBeacon` (Pose3d, float radius at +0xC, int at +0x10) to the vector at whiteboard+0x60; `ClearAllBeacons` walks it back; `GetActiveBeacon` returns the begin pointer, or null when empty. | 0x0056C39C (`AddBeacon`); 0x0056C3A8 `ldrd r0,r3,[r4,#0x64]`; 0x0056C3D2 `__emplace_back_slow_path`; 0x0056C404 (`GetActiveBeacon`); 0x0056C408 `cmp r1,r0`; 0x0056C40C `moveq r1,#0`; 0x0056AA08 (`ClearAllBeacons`) | M13-006 | EXACT_SOURCE |
| N26 | Stack tolerance | `BlockWorld::FindObjectOnTopOrUnderneathHelper(obj, float tolerance, filter, bool)` takes the tolerance as its second argument; it uses `GetDimInParentFrame<90>` and the parent Z at +0x28, then `FindLocatedObjectHelper`. | 0x0062601C; 0x00626070 `GetDimInParentFrame<(char)90>`; 0x0062607E `vmov.f32 s4,#0.5`; 0x00626086 `vmov.f32 s2,#-0.5`; 0x00626098 `vldr s2,[r0,#0x28]`; 0x0062610E `FindLocatedObjectHelper` | M13-007 | EXACT_SOURCE for the helper; the per-caller 30/15 constants were not re-read |
| N27 | Workouts | `WorkoutComponent::GetCurrentWorkout` returns the pointer at +0xC; `CompleteCurrentWorkout` triggers the workout's emotion event and advances the pointer by one 0x40-byte entry unless it is the last; `ShouldPlayEightiesMusic` caches in +0x11 and scores the current workout. | 0x00573DE8 `ldr r0,[r0,#0xC]`; 0x00573DF0/0x00573DF4; 0x00573E1C `TriggerEmotionEvent`; 0x00573E24 `subs r1,#0x40`; 0x00573E28/0x00573E2A `addne r0,#0x40`; 0x00573E30 (`ShouldPlayEightiesMusic`) | M13-010 | EXACT_SOURCE |

## 2. Existing records judged

### Confirmed against the source (keep status)

- **M13-003** (EXACT_SOURCE): the two paddings (7/6 or 2/1), the log string, the per-heading
  expansion, the 0.1 penalty and the collision queries all exist as claimed. One clause is
  wrong (below).
- **M13-006** (EXACT_SOURCE): confirmed in full.
- **M13-007** (EXACT_SOURCE): the helper and its second-argument tolerance are confirmed; the
  individual caller constants were not re-read in this pass, so the record's strongest claim
  (the 30-vs-15 split) is not independently re-verified here.
- **M13-008** (EXACT_SOURCE): confirmed in full; one NEW number (the reverse) is missing.
- **M13-009** (EXACT_SOURCE): confirmed, except the marker size order (below).
- **M13-010** (EXACT_SOURCE): confirmed in full.
- **M13-011** (EXACT_SOURCE): confirmed in full.
- **M13-012** (EXACT_SOURCE): confirmed; the comparison and the `MoveLiftToHeightAction(45,5,0)`
  are exactly as cited.
- **M13-013** (EXACT_SOURCE): confirmed; one NEW step (the SDK track-lock clear) is missing.
- **M13-015** (EXACT_SOURCE): the retry count, the two animations, the success path and
  `SetFailedToUse(…,3)` are all present as cited.

### Contradicted by the source

- **M13-003**, the clause "or 2.0 and 1.0 when the planner's flag at impl+0xA1 is set
  (0x004FD4F6)". 0x004FD4F6 tests the function's own first `bool` argument (`r4 = r1` at
  0x004FD4D2; `cmp r4,#0` at 0x004FD4E8; `itt ne` at 0x004FD4F6). The callers pass an
  immediate: `ComputePathHelper` and `PreloadObstacles` pass 0 (0x004FD2AC/0x004FD2AE,
  0x004FF92A/0x004FF92C), `StartPlanning` passes 1 (0x004FEC38). `impl+0xA1` is written by
  `StartPlanning` (0x004FEB7E) but is not what selects the padding pair. The values are right;
  the citation attributes them to the wrong source.
- **M13-009**, "sized 20 x 27". `Charger::Charger` passes `Point2{27.0, 20.0}` to `AddMarker`:
  x is stored at `sp+0x14` = 0x41D80000 (27.0) at 0x004E9C30 and y at `sp+0x18` =
  0x41A00000 (20.0) at 0x004E9C28, then `r3 = sp+0x14` at 0x004E9C36. If the record means
  (height x width) it is a naming difference, not a wrong number; as written it reads x=20,
  y=27, which the source does not say.

### Evidence too weak for the stated status

- **M13-002** (EXACT_SOURCE). `evidence` is two bare addresses (`FlipBlockAction 0x0055EC80`,
  `charger 0x004E9B6C`) with no instructions, and the title names four things, one of which
  (`AlignWithObjectAction`) has no citation at all. The behaviour is real and readable
  (N21/N23), but the record as written does not carry its own evidence. Replace the evidence
  with the instructions, or downgrade until re-cited.
- **M13-005** (EXACT_SOURCE). `evidence` is three prose sentences and no address. The
  behaviour is confirmed (N10: `DoPlanning` returns 0 on `Replan` failure and 3 on an empty
  plan; no fallback exists), but the record cites nothing. Re-cite to 0x00500126 / 0x00500202 /
  0x00500212.
- **M13-014** (EQUIVALENT_IMPLEMENTATION). Its evidence is extensive and address-level, but it
  is self-labelled EQUIVALENT and I did not re-read the whole `BehaviorKnockOverCubes` body in
  this pass. Keep as is; a later pass should either finish it or leave it EQUIVALENT.

### Partial evidence

- **M13-001** (EXACT_SOURCE) and **M13-004** (EXACT_SOURCE). Both rest on
  `config/engine/cozmo_mprim.json`. This clone has no OBB, so the asset's contents could not
  be re-opened. The engine-side loader/parser entry is confirmed (N1). The records are not
  contradicted; they are simply not re-verifiable here. The integrator should re-check them
  where the OBB is present, or keep them on the strength of the earlier extraction.

## 3. NEW steps (no existing record)

- **N8/N9**: `LatticePlannerImpl::StartPlanning`'s hard-coded `true` import, the
  `impl+0xA1` write, and `DoPlanning`'s artificial-delay sleep and `Replan` call
  (0x004FEB7E, 0x004FEC38, 0x005000A2..0x00500102). The `0x01C9C380` argument's meaning is
  UNKNOWN.
- **N10**: the exact result codes 0 and 3 from `DoPlanning`.
- **N16**: the mount's turn-in-place max speed 1.7459 and accel 5.236, and the reverse
  `DriveStraightAction(-120 mm, 30 mm/s, false)` whose vtable becomes
  `BackupOntoChargerAction`. None of these numbers is in M13-008/M13-012.
- **N19**: `DriveOffChargerContactsAction::Init` clears the SDK track lock
  (`SetTracksToLock(0)`, 0x00558288).
- **N20**: the drive-off behaviour's `IsRunnableInternal` (`robot+0x34A`), `InitInternal`
  (reaction lock, driving animations) and `UpdateInternal` (StopActing).
- **N21/N23**: the `FlipBlockAction` constants (150/20/45/40/-1, flag 1) and the
  `AlignWithObjectAction` alignment-type table.
- **N25/N26/N27**: the beacon list semantics, the stack-tolerance helper signature, and the
  workout pointer advance are already partly covered by their records, but the individual
  instructions above are new detail.

## 4. Open questions for the manager / integrator

1. **The OBB.** M13-001 and M13-004 cannot be closed in a clone without the OBB. Decide
   whether to re-extract them where the OBB exists, or to accept the earlier asset read.
2. **M13-003's flag wording** should be corrected to "the function's first bool argument"
   (with the call sites 0x004FD2AC/0x004FD2AE, 0x004FF92A, 0x004FEC38), or the record should
   state what `impl+0xA1` actually gates. It is a citation defect, not a behaviour change.
3. **M13-009's marker size** should be stated in the source's order (x=27, y=20) or labelled.
4. **M13-002/M13-005 evidence** is too weak to keep EXACT_SOURCE as written; re-cite or
   downgrade.
5. **`0x01C9C380`** (the `Replan` argument) and the `TurnInPlaceAction` speed/accel units are
   unread; they may be behaviour-changing.

*Read-only extraction. Nothing outside `.scratch/X2/` and this report file was changed.*


## Appendix B: gap pass 1 extraction report (2026-09-28)

﻿# I-M13 gap pass 1 (G1..G10) — read-only extraction

Job: re-analysis/jobs/I-M13.md, gap pass 1. Subsystem: M13-navigation.
Agent: opencode (DeepSeek), window 3. Date: 2026-09-28.
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`. Addresses are file VAs of the ELF,
disassembled with capstone in Thumb mode. The Ghidra decompilation `re-analysis/decomp/libcozmoEngine/`
was used as a navigation aid only; every claim below was re-read from the instructions.

Note on the addresses the job gave: several are import veneers, not function bodies. In this
ELF the import veneers live in an ARM-mode region near 0x004Cxxxx and the bodies live near
0x0085xxxx. The job's `ParseMotionPrims 0x004CE840` is the ARM veneer; the body is at
**0x00852014**. `MotionPrimitive::Create 0x00853DD0`, `ReadMotionPrimitives 0x008529C0`,
`Replan` PLT 0x004A6850 (body `xythetaPlanner::Replan 0x00858598`) are as given.

The OBB is present in this clone at
`re-analysis/obb/assets/cozmo_resources/config/engine/cozmo_mprim.json`.

---

## G1 — M13-004, M13-001 (X2 row N2). Class: EXACT_SOURCE

### G1a. `xythetaEnvironment::ParseMotionPrims(Json::Value const&, bool)` — body 0x00852014

Signature from the prologue: r0 = this (env), r1 = Json root, r2 = bool `oldFormat`.
`mov r4,r1` (root), `mov sl,r0` (env), `mov r6,r2` (bool) at 0x0085201A..0x00852026.

| JSON key | read at | how used |
|---|---|---|
| `resolution_mm` | 0x0085203C `GetValueOptional<float>(root, "resolution_mm", env+0)`; key ptr built at 0x00852024 (`add r1,pc` -> 0x00C29ADC "resolution_mm") | stored env+0; then 0x008520AA..0x008520B4 `vdiv 1.0 / env[0]` -> env+4 |
| `num_angles` | 0x00852062 `GetValueOptional<unsigned int>(root, "num_angles", env+8)`; key at 0x0085204C | stored env+8 |
| `actions` | 0x008520BA `Json::Value::operator[]("actions")`; size checked non-zero (0x008520BE..0x008520C4); iterated 0x008520D8..0x008521AE | each element -> `ActionType::Import` at 0x00852144, pushed into vector at env+0x30 |
| `angle_definitions` | 0x008521C6 `operator[]("angle_definitions")` (key at 0x008521C0); iterated, each `asFloat` pushed to vector env+0x38 (0x008521FA..0x00852222) | count compared with env+8 at 0x0085222E..0x0085223C |
| `angles` | 0x00852248 `operator[]("angles")` (key at 0x00852244); `size()` compared with env+8 at 0x0085224E..0x0085225A | then `resize` env+0x14 (vector<vector<MotionPrimitive>>) to env+8 at 0x00852264 |
| `angles[i].prims` | 0x008522B0 `operator[](unsigned i)`, 0x008522BC `operator[]("prims")`; each prim 0x008522EC / 0x00852300 | if bool `oldFormat`==1 -> `MotionPrimitive::Import` (0x008522F4); else `MotionPrimitive::Create(prim, (unsigned char)i, env)` (0x0085230E) |

Error strings (puts) confirm the keys: "error: could not find key 'resolution_mm' or 'num_angles' in motion primitives" (0x00852092), "empty or non-existant actions section! (old format, perhaps?)" (0x008521B4), "error: could not find key 'angles' in motion primitives" (0x00852452). On a bad primitive: "Failed to import motion primitive" (0x0085237C). At the end it calls `PopulateReverseMotionPrims` (0x008524AC).

The second bool argument is the old-format switch; `ReadMotionPrimitives` passes **0** (see G1d).

### G1b. Per-action keys — `Anki::Planning::ActionType::Import` 0x00851A18

The per-action fields are read here, not in `MotionPrimitive::Create`:

| key | read at | destination (ActionType layout, 24 bytes) |
|---|---|---|
| `extra_cost_factor` | 0x00851A82 (key), 0x00851A96 `GetValueOptional<float>` | ActionType+0x00 |
| `index` | 0x00851AA4 (key), 0x00851AB8 `GetValueOptional<unsigned char>` | ActionType+0x04 |
| `name` | 0x00851AC6 (key), 0x00851ADC `GetValueOptional<string>` | ActionType+0x08 (std::string, 12 bytes) |
| `reverse_action` | 0x00851B32 (key), 0x00851B48 `GetValueOptional<bool>` | ActionType+0x14 (bool) |

The `ActionType` constructor 0x008519EC initialises +0x00=0, +0x04=0xFF, +0x08="<invalid>", +0x14=0.

### G1c. Per-primitive keys — `Anki::Planning::MotionPrimitive::Create` 0x00853DD0

Signature: r0 = this (MotionPrimitive), r1 = Json prim, r2 = unsigned char action_index, r3 = env.
Prologue `strb.w r6,[sb,#1]` at 0x00853DEC stores action_index at MotionPrimitive+1.

| key | read at | how used |
|---|---|---|
| `action_index` | 0x00853DE8 (key), 0x00853E06 `GetValueOptional<unsigned char>(..., sb+0)` | presence required; error "error: missing key 'action_index'" (0x0085400E) |
| `end_pose` | 0x00853E20 (key -> "end_pose"), 0x00853E30 `State::Import` | MotionPrimitive+8; error "error: could not read 'end_pose'" (0x0085401C) |
| `intermediate_poses` | 0x00853E3E (key), iterated; each `State_c::Import` (0x00853E8C) pushed to vector at sb+0x14 | |
| `extra_cost_factor` | 0x00853FFC `isMember("extra_cost_factor")`; if present -> error "ERROR: individual primitives shouldn't have cost factors. Old file format?" (0x00854006) | **rejected** per primitive |
| (per-action lookup) | 0x00854036..0x00854044: `ldrb [sb]` action_index, `ldr r1,[env+0x2C]` (actions vector begin), stride 24, `ldrb r7,[action+0x14]` | reads `reverse_action` of the action; flips a scale sign (0x0085404C..0x00854054, 0x008540F8..0x00854106) |
| `straight_length_mm` | 0x00854088 (key), 0x00854094 `asDouble` | abs*scale added to sb+4; then 0x008540CC `operator[]("straight_length_mm")`, 0x008540D0 `asFloat`; if non-zero -> `Path::AppendLine` (0x00854140) |
| `arc` | 0x00854144 `isMember("arc")` (key -> "arc") | if present, reads the arc block (below) and calls `Path::AppendArc` 0x0085425A |
| `sweepRad` | 0x00854160 (key), 0x00854166 `asDouble` | arc |
| `radius_mm` | 0x0085417C (key), 0x00854184 `asDouble` | arc |
| `centerPt_x_mm` | 0x008541CE (key), 0x008541D4 `asFloat` | arc |
| `centerPt_y_mm` | 0x008541E4 (key), 0x008541EA `asFloat` | arc |
| `startRad` | 0x0085420C `adr r1,#0x28c` -> 0x0085449C "startRad", 0x00854216 `asFloat` | arc |
| `sweepRad` (again) | 0x00854226 (key), 0x0085423A `asFloat` | arc |
| `turn_in_place_direction` | 0x00854264 `isMember("turn_in_place_direction")` (key -> "turn_in_place_direction"), 0x00854278 `asDouble` | `Path::AppendPointTurn` 0x0085431A |

The arc key strings were read from the literal pool at 0x00854480 ("arc"), 0x0085449C ("startRad"),
and the annotated key pointers (0x00C29CD3 "sweepRad", 0x00C29CDC "radius_mm", 0x00C29CE6 "centerPt_x_mm",
0x00C29CF4 "centerPt_y_mm").

### G1d. `ReadMotionPrimitives` 0x008529C0 — file open

- 0x00852A44 `adr r1,#0x1b8` -> mode string at 0x00852C00 = **"r"**.
- 0x00852A48 `blx fopen`.
- 0x00852A62 `Json::Reader::parse(stream, root, true)`.
- 0x00852B06 `blx ParseMotionPrims` with `movs r2,#0` -> **oldFormat = false**, i.e. the shipped file goes through `MotionPrimitive::Create`.
- Returns `ParseMotionPrims`'s result (0x00852B0A `mov r7,r0`).

### G1e. "9 actions expected"

There is **no hard-coded 9** in `ParseMotionPrims`, `ReadMotionPrimitives`, `MotionPrimitive::Create`
or `xythetaEnvironment::Init`. The only count assertions are `angle_definitions.size() == num_angles`
and `angles.size() == num_angles` (0x0085223C, 0x0085225A). `MotionPrimitive::Create` indexes
`env->actions[action_index]` with a 24-byte stride and no bounds check (0x00854036..0x00854044).
The number 9 is the **shipped asset's** action count (G2); it is a fact about the OBB, not an engine constant.

### G1f. NEW: `xythetaEnvironment::Init` 0x008528A8 hard-codes 16 angles

`Init(char const*)` calls `ReadMotionPrimitives` (0x008528AE); on success it **overwrites** env+8
with `0x10` (0x008528B6..0x008528BA `movs r0,#0x10; str r0,[r4,#8]`), resizes the per-theta obstacle
table at env+0x44 to 16 (0x008528C0), and sets env+0xC = 2*pi/16 and env+0x10 = 1/(2*pi/16)
(0x008528C4..0x008528E6). So the planner's heading count is hard-coded 16 even though
`ParseMotionPrims` validates against the JSON `num_angles`. The asset's `num_angles` is 16, so they agree.

**G1 class: EXACT_SOURCE** for the loader and both parsers, and for the 16-heading override.
Record: M13-004 (schema) and M13-001 (loader). X2 row N2 is closed.

---

## G2 — M13-001, M13-004 asset side. Class: EXACT_SOURCE

File: `re-analysis/obb/assets/cozmo_resources/config/engine/cozmo_mprim.json`
Size 1,272,734 bytes. **sha256 = 4C79666BDD5F3F5FE6C7458B0E7D59ECBCCDB2AB3C12F1844D68BCC57887431D**

Top-level keys: `actions`, `angle_definitions`, `angles`, `num_angles`, `resolution_mm`.
`resolution_mm` = 10.0. `num_angles` = 16. `angle_definitions` = 16 values:
0.0, 0.4636476090008061, 0.7853981633974483, 1.1071487177940904, 1.5707963267948966,
2.0344439357957027, 2.356194490192345, 2.677945044588987, 3.141592653589793,
-2.677945044588987, -2.356194490192345, -2.0344439357957027, -1.5707963267948966,
-1.1071487177940904, -0.7853981633974483, -0.4636476090008061.

`actions` has exactly **9** entries:

| index | name | extra_cost_factor | reverse_action |
|---|---|---|---|
| 0 | "short straight" | 1.0001 | (absent -> false) |
| 1 | "long straight" | 1.0 | (absent) |
| 2 | "slight left" | 1.0 | (absent) |
| 3 | "slight right" | 1.0 | (absent) |
| 4 | "hard left" | 1.0 | (absent) |
| 5 | "hard right" | 1.0 | (absent) |
| 6 | "inplace left" | 2.0 | (absent) |
| 7 | "inplace right" | 2.0 | (absent) |
| 8 | "backwards short straight" | 1.2 | **true** |

`angles` has 16 entries; each has 9 primitives (action_index 0..8 in order) -> **144 primitives total**.
Every primitive has `action_index`, `end_pose`, `intermediate_poses`, `straight_length_mm`, and either
an `arc` block (actions 2..5) or `turn_in_place_direction` (actions 6..7) or neither (actions 0,1,8).

Angle 0 (heading 0.0) block, verbatim:

| action | straight_length_mm | arc / turn_in_place_direction |
|---|---|---|
| 0 | 10.0 | - |
| 1 | 50.0 | - |
| 2 | 7.639320225002111 | arc { centerPt_x_mm 7.639320225002111, centerPt_y_mm 94.72135954999578, radius_mm 94.72135954999578, startRad -1.5707963267948966, sweepRad 0.4636476090008061 } |
| 3 | 7.639320225002111 | arc { centerPt_x_mm 7.639320225002111, centerPt_y_mm -94.72135954999578, radius_mm 94.72135954999578, startRad 1.5707963267948966, sweepRad -0.4636476090008061 } |
| 4 | 5.857864376269046 | arc { centerPt_x_mm 5.857864376269046, centerPt_y_mm 34.14213562373096, radius_mm 34.14213562373096, startRad -1.5707963267948966, sweepRad 0.7853981633974483 } |
| 5 | 5.857864376269046 | arc { centerPt_x_mm 5.857864376269046, centerPt_y_mm -34.14213562373096, radius_mm 34.14213562373096, startRad 1.5707963267948966, sweepRad -0.7853981633974483 } |
| 6 | 0.0 | turn_in_place_direction 1.0 |
| 7 | 0.0 | turn_in_place_direction -1.0 |
| 8 | -10.0 | - |

The arc blocks are stored already rotated for the starting heading. Example (heading index 4,
angle pi/2, action 2 "slight left"): centerPt_x_mm = -94.72135954999578, centerPt_y_mm =
7.639320225002117, radius_mm = 94.72135954999578, startRad = 0.0, sweepRad = 0.46364760900080615.
This matches M13-004's evidence "the same slight left at heading 90 has its centre at
(-94.721, 7.639) with startRad 0". The values are JSON doubles; some straight_length_mm are
tiny floating-point residue (e.g. -8.88e-15 at angle 1 action 4), which the engine reads as double
(0x00854094) before the abs/scale step.

**G2 class: EXACT_SOURCE.** The X2 "OBB absent" limitation is closed. Records M13-001 and M13-004.

---

## G3 — M13-007 (X2 row N26). Class: EXACT_SOURCE

All five call sites re-read:

- `BlockConfigurations::StackOfCubes::BuildTallestStackForObject`
  - 0x0061928C `movt r7,#0x41f0` -> r7 = 0x41F00000 = **30.0**; passed as r2 at 0x0061929E
    `blx BlockWorld::FindObjectOnTopOrUnderneathHelper`. (5th stack arg = 1 at 0x0061929A.)
  - 0x00619344 `movt r5,#0x41f0` -> r5 = 0x41F00000 = **30.0**; passed as r2 at 0x00619358.
    (5th stack arg = 0 at 0x00619348/0x00619356.)
- `BlockWorld::UpdatePoseOfStackedObjects` 0x00621908 `movt r2,#0x4170` -> 0x41700000 = **15.0**;
  call at 0x0062190C.
- `DockingComponent::CanInteractWithObjectHelper` 0x0063C728 `movt r2,#0x4170` -> **15.0**;
  call at 0x0063C730.
- `CarryingComponent::SetObjectAsAttachedToLift` 0x00632F0C `movt r2,#0x4170` -> **15.0**;
  call at 0x00632F14.

Helper `BlockWorld::FindObjectOnTopOrUnderneathHelper` 0x0062601C:
- 0x00626026 `mov r4,r1` -> r1 = the object (first explicit argument);
- 0x00626038 `str r2,[sp,#0xa4]` -> r2 = the tolerance (second explicit argument), captured into
  the lambda at 0x006260CE/0x006260D8;
- 0x00626070 `blx GetDimInParentFrame<(char)90>(RotationMatrix3d)` -> r6 = the object's height;
- 0x0062607E `vmov.f32 s4,#0.5`; 0x00626086 `vmov.f32 s2,#-0.5`; 0x00626082 `cmp.w sl,#0`;
  0x0062608E `it ne`; 0x00626090 `vmovne.f32 s2,s4`; 0x00626094 `vmul.f32 s0,s0,s2`;
  0x00626098 `vldr s2,[r0,#0x28]` (parent Z); 0x0062609C `vadd.f32 s0,s2,s0`;
  0x006260A0 `vstr s0,[sp,#0x70]`. So the neighbour's centre is the parent Z plus +/-half the
  object's Z dimension; the tolerance is used inside the lambda passed to `FindLocatedObjectHelper`
  (0x0062610E).

**G3 class: EXACT_SOURCE.** Record M13-007. X2 row N26's deferred per-caller constants are now read.

---

## G4 — M13-005, NEW N8/N9/N10. Class: EXACT_SOURCE; the meaning of 0x01C9C380 is settled

### G4a. `LatticePlannerImpl::StartPlanning` 0x004FEB44

- 0x004FEB56 `mov r6,r2` -> r2 = the function's bool argument.
- 0x004FEB7E `strb.w r6,[sl,#0xA1]` -> **impl+0xA1 = the bool argument** (not a constant).
- 0x004FEC38 `movs r1,#1`; 0x004FEC3A `blx ImportBlockworldObstaclesIfNeeded(bool, ColorRGBA const*)`
  -> the import bool is **hard-coded 1**; r2 = `Anki::NamedColors::REPLAN_BLOCK_BOUNDING_QUAD`
  (0x004FEC32 `add r0,pc` -> GOT, 0x004FEC34 `ldr r2,[r0]`).

### G4b. `LatticePlannerImpl::DoPlanning` 0x00500090

- 0x00500098 `movs r0,#1; str.w r0,[fp,#0xF8]` -> status = 1.
- Sleep loop, 0x005000A2..0x005000EA:
  - 0x005000A2 `ldr.w r0,[fp,#0x108]` -> total wait in **milliseconds** (integer).
  - 0x005000B2 `movw r8,#0x4240; movt r8,#0xF` -> r8 = 0x000F4240 = 1,000,000 ns = 1 ms.
  - 0x005000C2 `ldr r0,[fp,#0x108]`; 0x005000C6 `subs r0,r0,r7` (remaining); 0x005000C8
    `cmp r0,#0xa`; 0x005000CC `it ge`; 0x005000CE `movge r4,#0xa` -> **r4 = min(remaining, 10)**.
  - 0x005000D4 `smull r0,r1,r4,r8` -> r4 * 1 ms; 0x005000DE `sleep_for`.
  - 0x005000BE `ldrb r0,[r6]` with r6 = fp+0xF2 -> the abort flag is checked every iteration.
  - So the wait is in **chunks of at most 10 ms**, not 1 ms. (The job's "1 ms chunks" is wrong.)
- 0x005000F2 `movw r1,#0xC380`; 0x005000FA `movt r1,#0x1C9` -> r1 = **0x01C9C380 = 30,000,000**;
  0x005000FE `mov r2,r6` (fp+0xF2, the volatile abort flag); 0x00500102 `blx Replan`.
- 0x00500106 `mov r4,r0` (save Replan result); 0x00500126 `cmp r4,#0`;
  0x00500132 `str r4,[sp,#8]`; 0x00500134 `it eq`; 0x00500136 `moveq r1,r0`
  with r0 = "robot.lattice_planner_failure" (0x00500112) and r1 = "robot.lattice_planner_success"
  (0x0050011C). So **r4 == 0 means failure**.
- 0x00500200 `ldr r0,[sp,#8]`; 0x00500202 `cbz r0,#0x00500216`; 0x00500216 `movs r0,#0`
  -> **return 0 when Replan == 0 (failure)**.
- 0x00500206 `GetPlan`; 0x0050020A `ldrd r1,r0,[r0,#8]`; 0x0050020E `cmp r0,r1`;
  0x00500210 `bne #0x0050021A`; 0x00500212 `movs r0,#3` -> **return 3 when Replan != 0 and the
  segment list is empty**.
- The normal path (Replan != 0, non-empty plan) ends at 0x0050058E `movs r0,#2` and
  0x00500590 `str.w r0,[fp,#0xF8]` -> **return 2**.
- There is no substitute path: every non-empty-plan branch builds the plan from `GetPlan()` and
  `xythetaPlan::Append` (0x00500540), never a locally constructed fallback.

### G4c. `xythetaPlanner::Replan` 0x00858598 and the argument

- 0x00858598 prologue; 0x008585AC `blx xythetaPlannerImpl::ComputePath(unsigned int, bool volatile*)`;
  0x00858642 `mov r0,r4` -> Replan returns ComputePath's value unchanged.
- `xythetaPlannerImpl::ComputePath` 0x008586A0: `mov sl,r1` (0x008586AE) is the argument;
  `str.w r2,[r8,#0x90]` (0x008586B4) stores the abort flag; `str.w sl,[sp,#0x28]` (0x0085883E);
  at 0x008588C6 `ldr r0,[sp,#0x28]`; 0x008588C8 `cmp sl,r0`; 0x008588CA `bhi #0x00858A96`;
  0x00858A96 warns "exceeded max expansions of %u, stopping" (string at 0x00858A9A)
  and returns 0. So **0x01C9C380 is the maximum number of state expansions (30,000,000), not a
  timeout and not a mode**, and the units are expansions.
- Return values: 0 on failure (CheckContextGoals 0x008586D4, CheckContextStart 0x00858798,
  InitializeHeuristic 0x0085882A, NoPlanFound 0x008589DE, exceeded max 0x00858B34); 1 on
  "No replan needed" (0x00858806) and after `BuildPlan` (0x00858A4C). So Replan's return is
  0 = failure, 1 = success, consistent with DoPlanning's string selection.

### G4d. Contradiction with X2 row N10

X2 row N10 says "If `Replan` returns non-zero, `DoPlanning` returns 0". The instructions say the
opposite: 0x00500202 is `cbz` (branch if zero) to the return-0 block, and 0x00500136 selects the
failure string when the saved Replan result is zero. **DoPlanning returns 0 when Replan returns 0
(failure), 3 when Replan returns non-zero with an empty plan, and 2 on success.** The job's G4/G10
premise repeats the X2 error. This is a contradiction of the X2 N10 row, not of M13-005's title
(M13-005's title "A lattice-planner failure sends no path" is still true).

**G4 class: EXACT_SOURCE.** Records M13-005 and the NEW N8/N9/N10 rows. The 0x01C9C380 meaning
is settled as max expansions.

---

## G5 — M13-014 (X2 row N22). Class: EXACT_SOURCE for the behaviour-changing path

Read from the instructions (the body is `0x005C2EA0..0x005C3E2A`; the callback is 0x005C3DCE).

### G5a. `BehaviorKnockOverCubes` body

- ctor 0x005C2EA0: calls `IBehavior::IBehavior` 0x005C2EA8; sets +0x11C/+0x120 = 0 (0x005C2EB8),
  +0x12C = -1 (0x005C2EBE), +0x134 = -1 (0x005C2EC8), +0x13C = -1, +0x140 = 0 (0x005C2ECC),
  +0x144 = &+0x148 (0x005C2EE2); +0x150/+0x154/+0x158/+0x15C/+0x160 = 0x23F (0x005C2EEE..0x005C2EFA);
  calls `LoadConfig` 0x005C2F02; subscribes to the message tag set at 0x005C2F16/0x005C2F1E.
- `LoadConfig` 0x005C2F70: reads AnimationTrigger `reachForBlockTrigger` -> +0x150 (key resolved
  at 0x005C30C0), `knockOverEyesTrigger` -> +0x158 (0x005C30DC), `knockOverSuccessTrigger` -> +0x15C
  (0x005C30F0), `knockOverFailureTrigger` -> +0x160 (0x005C3108), `knockOverPutDownTrigger` -> +0x154
  (0x005C3120), and int `minimumStackHeight` (default 3) -> +0x124 (0x005C306A/0x005C307A).
- `IsRunnableInternal` 0x005C314C: `StackOfCubes::GetStackHeight()` >= +0x124 (0x005C316E/0x005C317A).
- `InitInternal` 0x005C31A2: `InitializeMemberVars`; if +0xD9 (alwaysStreamline) or +0xD8 is set ->
  `TransitionToKnockingOverStack`, else `TransitionToReachingForBlock`.
- `InitializeMemberVars` 0x005C31D8: reaction lock, clear the object set at +0x144, +0x140 = 0
  (0x005C3218), copies +0x12C/+0x134/+0x13C from the stack configuration (0x005C321C..0x005C322A).
- `TransitionToReachingForBlock` 0x005C3254: object id from `BlockWorld::GetLocatedObjectByIdHelper`
  (0x005C3298); if null, calls vtable+0x90 (0x005C3414..0x005C341E); else builds a
  `CompoundActionSequential` and adds
  1. `TurnTowardsObjectAction(robot, objid, Radians(pi), false, false)` (0x005C32EE, pi = 0x40490FDB);
  2. if block x + 10 > 85.0 (0x005C3354..0x005C336C, 85.0 = 0x42AA0000) a
     `DriveStraightAction(robot, x - 85, 60.0, true)` (0x005C33A0, 60.0 = 0x42700000);
  3. `TriggerLiftSafeAnimationAction(robot, +0x150, 1, true, 0, 60.0, false)` (0x005C33E4);
  then `StartActing<BehaviorKnockOverCubes>(compound, &TransitionToKnockingOverStack)` (0x005C3408).
- `TransitionToKnockingOverStack` 0x005C34A8: if +0xD9/+0xD8 unset and +0x140 > 0 use the table
  entry at 0x005C36C4 (0.0), else 0x005C36BC (**pi/2 = 0x3FC90FDB**). Builds
  `DriveAndFlipBlockAction(robot, objid, false, 0.0, false, Radians(pi/2 or 0), false, 20.0)`
  (0x005C3548; 20.0 = 0x41A00000), sets say-name trigger 0xFD and no-name trigger 0xFE
  (0x005C3550/0x005C3558), then a `CompoundActionSequential` with
  1. `TurnTowardsObjectAction(robot, objid, Radians(pi), false, false)` (0x005C35A6),
  2. the `DriveAndFlipBlockAction` (0x005C35CC),
  3. `WaitAction(robot, 0.5)` (0x005C35FA, 0.5 = 0x3F000000);
  calls `PrepareForKnockOverAttempt` (0x005C3606) then `StartActing(compound, lambda 0x005C3DCE)`
  (0x005C3628).
- `PrepareForKnockOverAttempt` 0x005C3780: destroys and reinitialises the tipped-object set at +0x144
  (0x005C3786..0x005C37A4; +0x144=&+0x148, +0x148=0, +0x14C=0), `IncreaseScoreWhileActing(10.0)`,
  `SmartRemoveDisableReactionsLock("preparingToKnockOverDisable")`, then
  `SmartDisableReactionsWithLock` with the same string.
- `TransitionToBlindlyFlipping` 0x005C3840: `CompoundActionSequential` with
  1. `FlipBlockAction(robot, objid)` then `SetShouldCheckPreActionPose(false)` (0x005C387C/0x005C3884),
  2. `WaitAction(0.5)`;
  `PrepareForKnockOverAttempt`; `StartActing(compound, &TransitionToPlayingReaction)` (0x005C38E0).
- `TransitionToPlayingReaction` 0x005C3908: sets `robot->[+0x264]->[+0x18]->[+0xC] = 1`
  (0x005C3946..0x005C394E); if the tipped-object set size at +0x14C != 0 -> `BehaviorObjectiveAchieved(0xD, true)`,
  `NeedActionCompleted(0)`, trigger +0x15C (knockOverSuccessTrigger); else trigger +0x160
  (knockOverFailureTrigger) (0x005C3950..0x005C3972); if not streamlined, plays
  `TriggerLiftSafeAnimationAction(robot, trigger, 1, true, 0, 60.0, false)` (0x005C39A4).
- `ClearStack` 0x005C3A30 resets +0x11C/+0x120 and the three ids; `UpdateTargetStack` 0x005C3A56
  stores the tallest stack; `HandleObjectUpAxisChanged` 0x005C3A98 inserts the tipped object into
  the set at +0x144 (this is what makes the set non-empty); `HandleWhileRunning` 0x005C3AE4 and
  `AlwaysHandle` 0x005C3BC8 dispatch on message tag 0x11 (ObjectUpAxisChanged).

### G5b. Knock-over callback 0x005C3DCE

- 0x005C3DD0 `ldr r1,[r1]` -> the ActionResult's first word; 0x005C3DD4 `ldr r4,[r0,#4]` -> `this`.
- if result == **0x03000010** (NoPreActionPoses): 0x005C3DDE `ldr r0,[r0,#8]` (robot);
  0x005C3DE0 `ldr r1,[r4,#0x12C]` (object id); 0x005C3DE4 `ldr r0,[r0,#0x264]`;
  0x005C3DE8 `ldr r0,[r0,#0x18]`; 0x005C3DEA `str r1,[r0,#0x70]` -> writes the block to
  `AIWhiteboard+0x70`; return.
- if `result >> 24 == 4` (Retry): 0x005C3E08 `ldr r0,[r4,#0x140]` (attempt count); 0x005C3E0E
  `cmp r0,#1`; 0x005C3E10 `bgt` -> `TransitionToBlindlyFlipping` (0x005C3E1C); else
  `TransitionToKnockingOverStack` (0x005C3E14). Then 0x005C3E20 `ldr r0,[r4,#0x140]`;
  0x005C3E24 `adds r0,#1`; 0x005C3E26 `str.w r0,[r4,#0x140]` -> **the count is incremented after
  either branch**.
- if `result >> 24 == 0` (success): 0x005C3DFC `ldr r1,[r0,#8]` (robot); 0x005C3DFE `mov r0,r4`;
  0x005C3E04 `b.w #0x008CC16C` (ARM veneer -> `BehaviorKnockOverCubes::TransitionToPlayingReaction`
  via the thunk at 0x004B30B4, confirmed by the decompilation `005c3dce.c` callee list).
- otherwise return.

### G5c. `DriveAndFlipBlockAction` ctor 0x0055E208

Read past its arguments. It calls `IDriveToInteractWithObject` at 0x0055E24C with a
`PreActionPose::ActionType` value 5 (0x0055E222), then creates a `FlipBlockAction(robot, objid)`
(0x0055E27C), calls a helper at 0x0055C9BC and conditionally a lambda at 0x005586AC, adds the
FlipBlockAction to the compound (0x0055E2F2), and sets the proxy tag from `FlipBlockAction+0x60`
(0x0055E312/0x0055E316). The trailing float (the 8th argument, 20.0 in KnockOverCubes) is **not
read** in the ctor.

### G5d. `IDriveToInteractWithObject` 0x0055B1F4

- 0x0055B2C8 constructs `DriveToObjectAction`; 0x0055B2EC adds it to a compound; 0x0055B358 adds
  that compound to the outer compound; 0x0055B372 adds the outer compound to the behaviour.
- 0x0055B37C `Radians(0)`; 0x0055B38C `operator>(maxTurnTowardsFaceAngle, Radians(0))`;
  0x0055B392 `bne #0x0055B45E` -> the whole block is skipped when maxTurn <= 0.
- Inside (maxTurn > 0):
  1. 0x0055B398 allocate 0x1D8; 0x0055B3C0 `TurnTowardsFaceAction(robot, 0, angle, false)`;
     0x0055B3C4..0x0055B3CC overwrite the vtable with `TurnTowardsLastFacePoseAction`'s;
     0x0055B3D8 add with r3=1 (ignore failure).
  2. 0x0055B3FA allocate 0x198; 0x0055B42E `TurnTowardsObjectAction(robot, objid, angle, false, false)`;
     0x0055B43C add with r3=1.
  So **both** a TurnTowardsLastFacePoseAction and a TurnTowardsObjectAction are added when
  maxTurn > 0. X2 row N22 mentions only the first; the second is a missing behaviour-changing step.

### G5e. `IBehavior::StartActing` 0x005BE0E4 and the lambda 0x005BF8F4

- 0x005BE0E4 wraps the `function<void(Robot&)>` in a `function<void(ActionResult)>` and calls the
  base `StartActing(IActionRunner*, function<void(ActionResult)>)` (0x005BE132).
- The lambda's `operator()` 0x005BF8F4: `ldr r1,[r0,#8]`; `adds r0,#0x10`; `ldr r1,[r1,#0x2C]`;
  `b.w #0x008CC08C`. It invokes the stored `function<void(Robot&)>` and **ignores the
  ActionResult** (the wrapper's bound target takes no result). This matches X2 row N22's
  "StartActing's lambda ignores it".

### G5f. The C# difference

The engine constructs a `TurnTowardsFaceAction` and then overwrites its vtable with
`TurnTowardsLastFacePoseAction`'s (0x0055B3C4..0x0055B3CC), i.e. the action that runs is
`TurnTowardsLastFacePoseAction`. The existing C# uses `TurnTowardsFaceAction` for the last face.
That is a real difference; the engine's last-face action is `TurnTowardsLastFacePoseAction`
(plus the extra `TurnTowardsObjectAction`).

### G5g. What remains

The behaviour-changing path is read. The only unread item is the logging helper `FUN_005c0ca8`
(0x005C0CA8), which is non-behavioural (it formats a channel message). The `+0x14C` field read by
`TransitionToPlayingReaction` is the size/count of the tipped-object `std::set` at +0x144, written
by the tree insert from `HandleObjectUpAxisChanged` and zeroed by `PrepareForKnockOverAttempt`;
it is the success/failure trigger selector. No other behaviour-changing step is unread.

**G5 class: EXACT_SOURCE** for the production path. Record M13-014 can move from
EQUIVALENT_IMPLEMENTATION to EXACT_SOURCE once its evidence is updated with the transitions above
and the two missing steps (the second TurnTowardsObjectAction; the +0x14C success/failure trigger
selector).

---

## G6 — M13-002 / NEW M13-016 (X2 rows N21/N23). Class: EXACT_SOURCE

### G6a. `FlipBlockAction` ctor 0x0055EC80 (constants 0x0055ECAA..0x0055ED12)

- 0x0055ECAA `blx IAction::IAction` with `r3 = 0xF` (0x0055ECA8) -> IAction type **0xF**.
- 0x0055ECF2 `movt r0,#0x41A0` -> r0 = 0x41A00000 = **20.0**.
- 0x0055ECF6 `movt r1,#0x4316` -> r1 = 0x43160000 = **150.0**.
- 0x0055ECFA `strd r1,r0,[r4,#0x12C]` -> **+0x12C = 150.0, +0x130 = 20.0**.
- 0x0055ECFE `movt r2,#0x4234` -> r2 = 0x42340000 = **45.0**.
- 0x0055ED02 `movt r3,#0x4220` -> r3 = 0x42200000 = **40.0**.
- 0x0055ED06 `mov.w r7,#-1`.
- 0x0055ED0A `add.w r0,r4,#0x134`; 0x0055ED0E `stm r0!,{r2,r3,r7}` ->
  **+0x134 = 45.0, +0x138 = 40.0, +0x13C = -1**.
- 0x0055ED10 `movs r0,#1`; 0x0055ED12 `strb.w r0,[r4,#0x140]` -> **+0x140 = 1**.

All confirmed exactly as the job stated.

### G6b. `AlignWithObjectAction` alignment-type table 0x005533E4..0x00553426

- 0x005533CC `cmp r6,#3`; 0x005533E4 `bhi #0x0055340E` (types > 3 skip the table).
- 0x005533E6 `tbb [pc,r6]` with the byte table at 0x005533EA:
  type 0 -> 0x00553402, type 1 -> 0x005533EE, type 2 -> 0x005533F4, type 3 -> 0x005533FC.
- type 0: 0x00553402 `vmov.f32 s2,#-27.0`; 0x00553406 `vmov s0,r8` (the argument);
  0x0055340A `vadd.f32 s16,s0,s2` -> **arg + (-27.0)**.
- type 1: 0x005533EE `vmov.f32 s16,#6.0` -> **6.0**.
- type 2: 0x005533F4 `movs r0,#2`; 0x005533F6 `strb.w r0,[r4,#0xBB]` -> **sets a flag at +0xBB = 2**
  (distance stays 0, s16 was initialised to 0 at 0x005533C8).
- type 3: 0x005533FC `vmov.f32 s16,#-15.0` -> **-15.0**.
- 0x0055340E `mov r0,r6`; 0x00553410 `blx GetPreActionTypeFromAlignmentType`;
  0x00553414 `vldr s0,[pc,#0x68]` -> 0x00553480 = 0xC1800005 = **-16.000001**;
  0x0055341C `vcmpe.f32 s16,s0`; 0x00553424 `it mi`; 0x00553426 `vmovmi.f32 s16,s2` (s2 = 0.0)
  -> **clamp to 0.0 when s16 < -16.000001**.
- 0x0055342A `str.w r0,[r4,#0xFC]` stores the pre-action type; 0x00553436 `vstr s16,[r4,#0x9C]`
  stores the distance.

### G6c. `GetPreActionTypeFromAlignmentType` 0x005532B8

- if r0 < 4: 0x005532C0 `adr r1,#0x9c` -> table at 0x00553360, `ldr.w r0,[r1,r0,lsl#2]`.
  Table: type 0 -> 1, type 1 -> 0, type 2 -> 1, type 3 -> 1.
- else: logs "AlignWithObjectAction.GetPreActionTypeByAlignmentType.InvalidAlignmentType"
  (0x005532DC) and returns **1** (0x00553320).

**G6 class: EXACT_SOURCE.** Records M13-002 (FlipBlock constants) and the NEW M13-016
(AlignWithObject alignment-type table). X2 rows N21/N23 closed.

---

## G7 — M13-013 / NEW M13-017 (X2 rows N19/N20). Class: EXACT_SOURCE

### G7a. `DriveOffChargerContactsAction`

- Constructor 0x00558228: 0x00558232 `movt r2,#0x4120` -> r2 = 0x41200000 = **10.0**;
  0x00558236 `movt r3,#0x41A0` -> r3 = 0x41A00000 = **20.0**; 0x0055823C `str r5,[sp]` with
  r5 = 0; 0x0055823E `blx DriveStraightAction(robot, 10.0, 20.0, false)`. Then +0x44 = 7
  (0x00558276/0x00558278). **In the constructor** (not Init): 0x0055827C `blx CozmoContext::IsInSdkMode`;
  0x00558280 `cmp r0,#1`; 0x00558282 `bne`; 0x00558286 `movs r1,#0`; 0x00558288
  `blx IActionRunner::SetTracksToLock(0)` -> only in SDK mode.
- `Init` 0x005582D0: 0x005582D2 `ldrb.w r1,[robot+0x338]`; 0x005582D6 `strb.w r1,[action+0x8B]`;
  0x005582DA `cbz r1` -> return 0 if not on contacts; else tail-call at 0x005582DC
  `b.w #0x008CB5EC` (the DriveStraightAction Init veneer).
- `CheckIfDone` 0x005582E4: 0x005582EA `ldrb [action+0x8B]`; if 0 -> return 0 (0x00558302).
  Else `DriveStraightAction::CheckIfDone` (0x005582F2); if it returns 0x1000000 -> return 0x1000000
  (still running, 0x005582FC). Else 0x00558306 `ldr r0,[robot]`; 0x00558308 `ldrb.w r1,[r0,#0x338]`;
  0x0055830C `movs r0,#0`; 0x0055830E `cbz r1,#0x0055834A` -> if off contacts return 0; if still on
  contacts, warn and 0x00558344 `movs r0,#9; movt r0,#0x400` -> **0x04000009**.

### G7b. `BehaviorDriveOffCharger`

- ctor 0x005C0980: 0x005C09B2 reads JSON key at 0x005C0A94 = **"extraDistanceToDrive_mm"**
  (`Json::Value::get`, default a double 0.0 at 0x005C09AC); 0x005C09C0 `asFloat` -> s16;
  0x005C09D4 `vldr s0,[pc,#0xd4]` -> 0x005C0AB0 = 0x42C00000 = **96.0**;
  0x005C09E2 `vadd.f32 s0,s16,s0`; 0x005C09E6 `vstr s0,[r4,#0x11C]` -> **+0x11C = 96.0 + json**.
- `IsRunnableInternal` 0x005C0B10: `ldrb.w r0,[r1,#0x34A]` -> **robot+0x34A**.
- `InitInternal` 0x005C0B18: `SmartDisableReactionsWithLock` (0x005C0B2A); +0x120 = 0
  (0x005C0B2E/0x005C0B30); if `robot->[+0x264]->[+0x30]->[+0x14] == 3` then
  `DrivingAnimationHandler::PushDrivingAnimations` (0x005C0B4A) and +0x120 = 1 (0x005C0B4E/0x005C0B50);
  if `robot+0x355` != 0 it logs "WaitForOnTreads" (0x005C0B5A..0x005C0B74) and does not transition;
  else `TransitionToDrivingForward` (0x005C0B8C).
- `TransitionToDrivingForward` 0x005C0BB8: if `robot+0x34A` != 0 (0x005C0BF4/0x005C0BF8), creates
  `DriveStraightAction(robot, +0x11C)` (0x005C0C02/0x005C0C08) and `StartActing(action, lambda)`
  (0x005C0C26). Drives the 96.0+extra distance.
- `StopInternal` 0x005C0D90: if +0x120 != 0, calls the DrivingAnimationHandler pop at veneer
  0x008CABCC (0x005C0D96..0x005C0DA0).
- `UpdateInternal` 0x005C0DA8: 0x005C0DB0 `ldrb [robot+0x34A]`;
  - if **not** on contacts (r0 == 0): 0x005C0DF4 if `[this+0x84]==0` -> `BaseStationTimer::GetCurrentTimeInSeconds()`
    stored at `robot->[+0x264]->[+0x18]+0x44` (0x005C0DFA..0x005C0E08), return **2**.
  - if on contacts: 0x005C0DB6 `ldrb [robot+0x355]`; if != 0 -> `StopActing(false,false)`
    (0x005C0DC4) and logs "WaitForOnTreads"; else if `[this+0x84]==0` -> `TransitionToDrivingForward`
    (0x005C0E18); return **1**.

Note: the job's "UpdateInternal StopActing on leaving the contacts" is not what the instructions
say. StopActing is called when the robot is **still** on the contacts (`robot+0x34A != 0`) and
`robot+0x355 != 0`; when the robot has left the contacts (`robot+0x34A == 0`) UpdateInternal
records the time and returns 2.

**G7 class: EXACT_SOURCE.** Records M13-013 and the NEW M13-017 (BehaviorDriveOffCharger).
X2 rows N19/N20 closed.

---

## G8 — M13-008 (X2 row N16). Class: EXACT_SOURCE

`MountChargerAction::ConfigureTurnAndMountAction`:

- 0x0054E52E `ComputeVectorBetween`; 0x0054E536 `atan2f`; 0x0054E54C
  `TurnInPlaceAction(robot, atan2, true)`.
- 0x0054E550/0x0054E556 `movw r1,#0x66f3; movt r1,#0x3fdf` -> r1 = **0x3FDF66F3 = 1.7453293**;
  0x0054E55A `SetMaxSpeed(1.7453293)`.
- 0x0054E55E/0x0054E564 `movw r1,#0x8d36; movt r1,#0x40a7` -> r1 = **0x40A78D36 = 5.2359877**;
  0x0054E568 `SetAccel(5.2359877)`.
- 0x0054E58A `Robot::GetLiftHeight`; 0x0054E592 `vldr s2,[pc,#0x14c]` = 0x42340000 = **45.0**;
  0x0054E59E `bpl` -> if lift >= 45 skip; else 0x0054E5B2 `movt r2,#0x4234` (45.0),
  0x0054E5BE `movt r3,#0x40A0` (5.0), `str r7,[sp]` (0.0), 0x0054E5C6
  `MoveLiftToHeightAction(robot, 45.0, 5.0, 0.0)`.
- 0x0054E5FC `movt r2,#0xc2f0` -> 0xC2F00000 = **-120.0**; 0x0054E600 `movt r3,#0x41f0`
  -> 0x41F00000 = **30.0**; `str r4,[sp]` (0.0); 0x0054E608
  `DriveStraightAction(robot, -120.0, 30.0, false)`.
- 0x0054E60E `strb.w r7,[r6,#0x8B]` (copies +0x80); 0x0054E60C/0x0054E612
  `GOT->{vtable(BackupOntoChargerAction)}`; 0x0054E618 `str r0,[r6]` -> **vtable overwritten**.

The job's "max speed 1.7459" is slightly off: 0x3FDF66F3 = **1.7453293 rad/s = 100 deg/s**.

### Units

`TurnInPlaceAction::SetMaxSpeed` 0x00545C08:
- 0x00545C0E `vldr s4,[pc,#0xc8]` -> 0x00545CD8 = 0x40A78D36 = 5.2359877 (300 deg/s);
  0x00545C18 `vabs s2,s0`; 0x00545C1C `vcmpe s2,s4`; 0x00545C24 `ble` -> if |maxSpeed| <= 5.236 keep.
- if larger: 0x00545C26 `vldr s2,[pc,#0xb4]` -> 0x00545CE0 = 0x42652EE1 = **57.29578 (180/pi)**;
  0x00545C2E `vmul.f32 s0,s0,s2` (rad -> deg); the warning at 0x00545C36 is
  **"Speed of %f deg/s exceeds limit of %f deg/s. Clamping."** with the limit 0x4072C000 = 300.0;
  then clamps to 0x40A78D36.
- So **SetMaxSpeed takes rad/s** (the warning converts to deg/s only to display), and the limit is
  300 deg/s = 5.236 rad/s.
- `SetAccel` 0x00545D14 stores r1 (rad/s^2) at +0xC8, defaulting from +0x7C when r1 == 0
  (0x00545D22/0x00545D24). The constructor 0x00545A26..0x00545A44 sets +0x78 = 0x40A78D36
  (default max speed 5.236 rad/s), +0x7C = 0x41200000 (10.0 rad/s^2), +0x80 = 0x41C80000 (25.0).
- The units are owned by `TurnInPlaceAction` (the control layer), not by the mount.

**G8 class: EXACT_SOURCE.** Record M13-008; the units question is settled as rad/s and rad/s^2.

---

## G9 — M13-009 (X2 row N13). Class: EXACT_SOURCE

`Charger::Charger` 0x004E9B6C:

- 0x004E9B98 `movt r2,#0x42c0` -> 0x42C00000 = **96.0**; 0x004E9BAC `movt r1,#0x42a0`
  -> 0x42A00000 = **80.0**; 0x004E9BB4 `strd r2,r1,[r4,#0xF0]` -> +0xF0 = 96.0, +0xF4 = 80.0.
- 0x004E9BB0 `movt r0,#0x41f8` -> 0x41F80000 = **31.0**; 0x004E9BB8 `str.w r0,[r4,#0xF8]`.
- marker pose: 0x004E9BBC/0x004E9BC2 `movw r1,#0xfdb; movt r1,#0xbfc9` -> 0xBFC90FDB = **-pi/2**;
  `Radians::Radians` 0x004E9BC6; `Z_AXIS_3D` 0x004E9BCA; translation at 0x004E9BD6 `movt r2,#0x42ac`
  -> 0x42AC0000 = **86.0** (sp+0x14), 0 at sp+0x18, 0x004E9BE2 `movt r2,#0x41b0` -> 0x41B00000 = **22.0**
  (sp+0x1c); `Pose3d` 0x004E9C02.
- marker id: 0x004E9C16 `movs r1,#2`; 0x004E9C1C `strh.w r1,[sp,#8]`.
- **size**: 0x004E9C28 `str r1,[sp,#0x18]` with r1 = 0x41A00000 = **20.0** (y);
  0x004E9C30 `str r1,[sp,#0x14]` with r1 = 0x41D80000 = **27.0** (x). 0x004E9C36 `add r3,sp,#0x14`
  -> the pointer passed to `AddMarker` (0x004E9C38) is `sp+0x14`, whose first float is **27.0 (x)**
  and whose second is **20.0 (y)**.
- 0x004E9C38 `blx ObservableObject::AddMarker(short const&, Pose3d const&, Point<2,float> const&)`;
  result stored at +0xFC (0x004E9C3C).

So the size is **x = 27.0, y = 20.0**, and the pointer to `AddMarker` is `sp+0x14` (the 27.0 word).
M13-009's "20 x 27" reads x=20,y=27, which the source does not say; the source's order is x=27,y=20.
(X2's section 2 already flagged this.)

**G9 class: EXACT_SOURCE.** Record M13-009.

---

## G10 — M13-005 (X2 row N10). Class: EXACT_SOURCE; the job's premise is contradicted

`LatticePlannerImpl::DoPlanning` 0x00500090:

- 0x00500102 `Replan(0x01C9C380, abortFlag)`; 0x00500106 `mov r4,r0`; 0x00500132 `str r4,[sp,#8]`.
- 0x00500200 `ldr r0,[sp,#8]`; 0x00500202 `cbz r0,#0x00500216`; 0x00500216 `movs r0,#0` ->
  **return 0 when Replan returns 0**.
- 0x00500206 `GetPlan`; 0x0050020E `cmp r0,r1` (segment list begin vs end); 0x00500210 `bne #0x0050021A`;
  0x00500212 `movs r0,#3` -> **return 3 when Replan is non-zero and the plan's segment list is empty**.
- Normal success returns **2** (0x0050058E/0x00500590).
- There is no substitute path in `DoPlanning`: the non-empty-plan branch appends the engine's own
  `GetPlan()` result (0x00500540) and returns; nothing constructs a fallback plan.

**Contradiction:** X2 row N10 and the job's G4/G10 say "returns 0 when Replan is non-zero". The
source says 0 is returned when Replan returns **zero** (failure); the non-zero/empty case returns 3.
0x00500136 `moveq r1,r0` (failure string) also confirms Replan==0 is the failure. This is a
citation/semantics defect in the X2 row, not a change to M13-005's title ("A lattice-planner failure
sends no path" remains correct).

**G10 class: EXACT_SOURCE.** Record M13-005; X2 row N10 must be corrected.

---

## Contradictions of existing records / prior rows

1. **X2 row N10** (and the job's G4/G10 premise): "returns 0 when Replan is non-zero" is backwards.
   0x00500202 is `cbz` -> return 0 when Replan == 0 (failure); return 3 when Replan != 0 and the
   plan is empty; return 2 on success. Citation: 0x00500106, 0x00500136, 0x00500202, 0x00500212,
   0x0050058E.
2. **M13-009 / X2 row N13**: "sized 20 x 27" reads x=20, y=27. The source stores 27.0 at sp+0x14
   (x, the pointer passed to AddMarker) and 20.0 at sp+0x18 (y). Citation: 0x004E9C28 (20.0),
   0x004E9C30 (27.0), 0x004E9C36 (`add r3,sp,#0x14`), 0x004E9C38 (`AddMarker`). (Already noted by X2.)
3. **X2 row N16**: the mount's turn-in-place max speed is 0x3FDF66F3 = 1.7453293 rad/s (100 deg/s),
   not 1.7459. Citation: 0x0054E550/0x0054E556, 0x0054E55A.
4. **X2 row N22**: `IDriveToInteractWithObject` adds **two** actions when maxTurn > 0, not one:
   a `TurnTowardsLastFacePoseAction` (0x0055B3C4..0x0055B3D8) **and** a `TurnTowardsObjectAction`
   (0x0055B42E/0x0055B43C). The X2 row mentions only the first. This is a missing
   behaviour-changing step.
5. **X2 row N19**: the `SetTracksToLock(0)` SDK-mode clear is in the **constructor** (0x0055827C/
   0x00558288), not in `Init`; `Init` 0x005582D0 copies `robot+0x338` to `action+0x8B`. X2 attributed
   the copy and the clear to `Init` together.
6. **Job G7**: "UpdateInternal StopActing on leaving the contacts" is not what the source says.
   `StopActing` is called when still on the contacts (`robot+0x34A != 0`) and `robot+0x355 != 0`
   (0x005C0DB0..0x005C0DC4); when the robot has left (`robot+0x34A == 0`) it records the time and
   returns 2 (0x005C0DF4..0x005C0E0C).
7. **Job G4**: "the sleep loop (impl+0x108 ms in 1 ms chunks)" is wrong: the loop sleeps
   `min(remaining, 10)` ms per iteration (0x005000C8..0x005000CE), with r8 = 1,000,000 ns/ms.
8. **Job G1**: `ParseMotionPrims` is not at 0x004CE840; that is the ARM import veneer. The body is
   0x00852014. Likewise `MotionPrimitive::Create` is 0x00853DD0 and `ReadMotionPrimitives`
   0x008529C0 (those two are bodies).

## Records whose evidence is too weak to keep their status

- **M13-002** (EXACT_SOURCE): evidence is two bare addresses (`FlipBlockAction 0x0055EC80`,
  `charger 0x004E9B6C`); it does not carry the instructions. The behaviour is real and now fully
  read (G6, G9), but the record should cite the instructions.
- **M13-005** (EXACT_SOURCE): evidence is three prose sentences with no address. The behaviour is
  confirmed (G4/G10) but the record cites nothing; and its prose is contradicted on the Replan
  condition.
- **M13-014** (EQUIVALENT_IMPLEMENTATION): can be raised to EXACT_SOURCE now that the path is read
  (G5), but its evidence must add the second `TurnTowardsObjectAction`, the `+0x14C` success/failure
  trigger selector, and the corrected `TransitionToPlayingReaction` (0xD objective / NeedActionCompleted).

## Open questions for the manager

1. **M13-005 prose.** The record and X2 row N10 state the Replan condition backwards. Decide
   whether to rewrite M13-005's evidence and keep EXACT_SOURCE, or to re-cite it. The title still
   holds; the failure/no-substitute-path claim is correct.
2. **M13-014 status.** With G5 read, it can be EXACT_SOURCE. Confirm whether the two newly found
   steps (second `TurnTowardsObjectAction`; `+0x14C` trigger selector) get their own records or are
   folded into M13-014's evidence.
3. **M13-016 / M13-017 numbering.** G6's AlignWithObject table and G7's BehaviorDriveOffCharger are
   new records; confirm the numbers.
4. **`num_angles` override (G1f).** `xythetaEnvironment::Init` hard-codes 16 headings and overrides
   the JSON value. This is a behaviour-changing step not in M13-001/M13-004; decide whether it
   becomes a record.
5. **`FUN_005c0ca8`** (the channel logger used by the behaviours) was not read; it is non-behavioural.
   Confirm it does not need a record.
6. **M13-008 / M13-012** both cite the mount; the unit question is now settled inside
   `TurnInPlaceAction`, which is the control layer. Confirm which record owns the rad/s vs rad/s^2
   statement.

*Read-only extraction. Nothing outside this report file was changed.*


## Appendix C: gap pass 2 extraction report (2026-09-28)

# I-M13 gap pass 2 — verifier findings resolved (read-only extraction)

Job: I-M13 gap pass 2 (subsystem M13-navigation). Agent: opencode (DeepSeek), window 3. Date: 2026-09-28.
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, file VAs, Thumb. Every claim below was re-read
from the instructions with capstone (detail on, PC-relative literals resolved). The Ghidra
decompilation `re-analysis/decomp/libcozmoEngine/` was used only to name the containing functions;
it is not evidence. Earlier rows referenced: X2 `20260927-X2-M13-navigation-extraction.md` (N1..N27)
and gap1 `20260928-I-M13-gap1-extraction.md` (G1..G10).

---

## 1. AlignWithObjectAction alignment-type table (X2 N23, gap1 G6b). Class: EXACT_SOURCE

Function `Anki::Cozmo::AlignWithObjectAction::AlignWithObjectAction` entry **0x00553370**
(`index.tsv`; body 0x00553370..0x00553443). The switch is 0x005533C8..0x0055343A.

Setup:
- 0x005533C8 `vldr s16,[pc,#0xb0]` -> word at 0x0055347C = 0x00000000 = **0.0** (the initial distance).
- 0x005533CC `cmp r6,#3`; 0x005533E4 `bhi #0x0055340E` (alignment types > 3 skip the table; r6 = the type).
- 0x005533E6 `tbb [pc,r6]`; table base PC = 0x005533E6 + 4 = **0x005533EA**; raw bytes at 0x005533EA =
  **`02 05 09 0c`**; TBB target = 0x005533EA + **2 x byte**.

| alignment type | table byte | target | instruction(s) | effect |
|---|---|---|---|---|
| 0 | 0x02 | 0x005533EE | `vmov.f32 s16,#6.0` | distance = **6.0** |
| 1 | 0x05 | 0x005533F4 | `movs r0,#2`; `strb.w r0,[r4,#0xbb]` | flag at **+0xBB = 2**; distance stays 0.0 |
| 2 | 0x09 | 0x005533FC | `vmov.f32 s16,#-15.0` | distance = **-15.0** |
| 3 | 0x0c | 0x00553402 | `vmov.f32 s2,#-27.0`; `vmov s0,r8`; `vadd.f32 s16,s0,s2` | distance = **(float argument) + (-27.0)** |

This is the **inverse** of both earlier passes. X2 N23 and gap1 G6b assigned type 0 to `arg+(-27.0)`
and type 3 to `-15.0`; the instructions put `arg+(-27.0)` at type **3** and `6.0` at type **0**.
(The decomp 00553370.c shows the same switch, case 0 fVar2=6.0, case 1 this[0xbb]=2, case 2 fVar2=-15.0,
case 3 fVar2=param_4-27.0; that file is a navigation aid only.)

Clamp:
- 0x00553414 `vldr s0,[pc,#0x68]` -> word at **0x00553480 = 0xC1800005 = -16.000009536743164**
  (i.e. **-16.00000954**), **not** -16.000001.
- 0x00553418 `vldr s2,[pc,#0x60]` -> word at **0x0055347C = 0x00000000 = 0.0**.
- 0x0055341C `vcmpe.f32 s16,s0`; 0x00553420 `vmrs apsr_nzcv,fpscr`; 0x00553424 `it mi`;
  0x00553426 `vmovmi.f32 s16,s2`.
- Exact clamp: s16 is replaced by 0.0 exactly when **`s16 < -16.000009536743164`** (mi = N set = less than).
  gap1 G6b's threshold -16.000001 is corrected to -16.00000954.

Store:
- 0x0055342A `str.w r0,[r4,#0xfc]` stores the pre-action type; 0x00553436 `vstr s16,[r4,#0x9c]`
  stores the distance; 0x0055342E `movs r0,#0` / 0x00553430 `strd r0,r0,[r4,#0xa0]` clears +0xA0/+0xA4.

`GetPreActionTypeFromAlignmentType` **0x005532B8**:
- 0x005532BC `cmp r0,#4`; 0x005532BE `bhs #0x005532CC` (invalid).
- 0x005532C0 `adr r1,#0x9c` -> base align(0x005532C0+4,4)=0x005532C4 + 0x9c = **0x00553360**;
  0x005532C2 `sxtb r0,r0`; 0x005532C4 `ldr.w r0,[r1,r0,lsl#2]`.
- Table at 0x00553360, 32-bit words:
  **[0x00553360]=1, [0x00553364]=0, [0x00553368]=1, [0x0055336C]=1** -> type 0->1, type 1->0,
  type 2->1, type 3->1.
- Invalid (r0 >= 4): 0x005532CC..0x00553320 logs
  `"AlignWithObjectAction.GetPreActionTypeByAlignmentType.InvalidAlignmentType"` and
  0x00553320 `movs r0,#1` -> **default 1**.

---

## 2. BackupOntoChargerAction::CheckIfDone result code (X2 N17). Class: EXACT_SOURCE

Function **0x0054E7A8** (body 0x0054E7A8..0x0054E7F2).

- 0x0054E7AE `ldr r1,[r5,#4]` (robot); 0x0054E7B0 `ldrb.w r0,[r1,#0x338]` (on-contacts flag);
  0x0054E7B4 `cbz r0,#0x0054E7C0`.
- **On-contacts success path** (flag != 0): 0x0054E7B6 `mov r0,r1`; 0x0054E7B8 `blx 0x004AB728`
  `Robot::SetPoseOnCharger`; 0x0054E7BC `movs r4,#0`; 0x0054E7BE `b #0x0054E7EE` -> **return 0**.
- **Not on contacts** (flag == 0): 0x0054E7C0 `mov r0,sp`; 0x0054E7C2 `blx 0x004AB734`
  `GetPitchAngle`; 0x0054E7C6 `vldr s0,[sp]`; 0x0054E7CA `movs r4,#6`;
  0x0054E7CC `vldr s2,[pc,#0x24]` -> word at **0x0054E7F4 = 0xBE860A92 = -0.2617993950843811**;
  0x0054E7D0 `movt r4,#0x400` -> r4 = **0x04000006**;
  0x0054E7D4 `vcmpe.f32 s0,s2`; 0x0054E7D8 `vmrs apsr_nzcv,fpscr`;
  0x0054E7DC `bpl #0x0054E7E2` (branch when pitch >= -0.2617994).
- **Pitch-below path** (pitch < -0.2617994): 0x0054E7DE `adds r4,#4` -> **0x0400000A**;
  0x0054E7E0 `b #0x0054E7EE` -> **return 0x0400000A**.
- **Fall-through** (pitch >= -0.2617994): 0x0054E7E2 `mov r0,r5`; 0x0054E7E4 `blx 0x004AB590`
  `DriveStraightAction::CheckIfDone`; 0x0054E7E8 `cmp r0,#0`; 0x0054E7EA `it ne`;
  0x0054E7EC `movne r4,r0`. If the drive returns non-zero, return that value; if it returns 0
  (drive done), r4 keeps **0x04000006**. 0x0054E7EE `mov r0,r4`; 0x0054E7F2 `pop {r4,r5,r7,pc}`.

Where each constant is:
- **0x04000006**: 0x0054E7CA `movs r4,#6` + 0x0054E7D0 `movt r4,#0x400`; returned on the fall-through
  when `DriveStraightAction::CheckIfDone` returns 0. Also 0x0054E3E8 `movs r5,#6` + 0x0054E3EA
  `movt r5,#0x400` in `MountChargerAction::CheckIfDone` (0x0054E2D0) is the same 0x04000006.
- **0x0400000A**: 0x0054E7DE `adds r4,#4`; returned on the pitch-below path.
- **0x04000004**: does **not** appear.

Re-check of 0x04000004 in the cited ranges. The cited addresses are M13-008:
0x0054E2D0, 0x0054E31A, 0x0054E612, 0x0054E72C, 0x0054E7A8; M13-012: 0x0054E58A, 0x0054E59E,
0x0054E5C6. I read every function containing them: 0x0054E2D0..0x0054E3F4 (MountChargerAction::CheckIfDone),
0x0054E500..0x0054E620 (ConfigureTurnAndMountAction), 0x0054E72C..0x0054E772
(ConfigureDriveForRetryAction), 0x0054E7A8..0x0054E7F2 (BackupOntoChargerAction::CheckIfDone).
The only 0x040000xx constants are 0x04000006 (0x0054E3E8/0x0054E3EA and 0x0054E7CA/0x0054E7D0) and
0x0400000A (0x0054E7DE). **0x04000004 is absent from every cited range.** X2 N17's
"fails (0x04000004)" is wrong; the pitch-below return is **0x0400000A**.

---

## 3. TransitionToKnockingOverStack maxTurn table (gap1 G5a). Class: EXACT_SOURCE

Raw bytes at **0x005C36B0**:

| address | bytes | value |
|---|---|---|
| 0x005C36B0 | `53 74 61 63 6b 00 00 00` | "Stack\0\0\0" |
| 0x005C36B8 | `00 00 00 00` | **0.0** |
| 0x005C36BC | `db 0f c9 3f` | **0x3FC90FDB = pi/2 = 1.5707963705062866** |
| 0x005C36C0 | `00 00 00 00` | **0.0** |
| 0x005C36C4 | `82 b3 a7 00` | pointer 0x00A7B382 (not a float) |

Selection in `TransitionToKnockingOverStack` **0x005C34A8**:
- 0x005C34EA `ldrb.w r0,[sl,#0xd9]`; 0x005C34EE `vldr s16,[pc,#0x1c8]` -> base align(0x005C34EE+4,4)
  = 0x005C34F0 + 0x1c8 = **0x005C36B8 = 0.0**; 0x005C34F2 `cbnz r0,#0x005C350A`.
- 0x005C34F4 `ldrb.w r0,[sl,#0xd8]`; 0x005C34F8 `cbnz r0,#0x005C350A`.
- 0x005C34FA `ldr.w r0,[sl,#0x140]` (attempt count); 0x005C34FE `adr r1,#0x1bc` -> base
  align(0x005C34FE+4,4)=0x005C3500 + 0x1bc = **0x005C36BC (pi/2)**; 0x005C3500 `cmp r0,#0`;
  0x005C3502 `it gt`; 0x005C3504 `addgt r1,#4` -> **0x005C36C0 (0.0)**; 0x005C3506 `vldr s16,[r1]`.

So the maxTurn selection is: if +0xD9 != 0 or +0xD8 != 0 -> 0.0; else if +0x140 (attempt count) > 0
-> **0.0 at 0x005C36C0**; else **pi/2 at 0x005C36BC**. gap1 G5a's address 0x005C36C4 is wrong; the
`addgt` target is 0x005C36C0, and 0x005C36C4 holds the pointer 0x00A7B382.

---

## 4. Mount turn numbers and units (X2 N16, gap1 G8). Class: EXACT_SOURCE

`ConfigureTurnAndMountAction` (function containing 0x0054E52E..0x0054E618):
- 0x0054E550 `movw r1,#0x66f3`; 0x0054E556 `movt r1,#0x3fdf` -> r1 = **0x3FDF66F3 =
  1.7453292608261108** (7 significant figures: **1.745329**). 0x0054E55A `blx 0x004A9598`
  `TurnInPlaceAction::SetMaxSpeed`. X2 N16's "1.7459" is wrong; the value is 100 deg/s.
- 0x0054E55E `movw r1,#0x8d36`; 0x0054E564 `movt r1,#0x40a7` -> r1 = **0x40A78D36 =
  5.235987663269043** (7 significant figures: **5.235988**). 0x0054E568 `blx 0x004A95A4`
  `TurnInPlaceAction::SetAccel`.

Units, `TurnInPlaceAction::SetMaxSpeed` **0x00545C08**:
- 0x00545C0E `vldr s4,[pc,#0xc8]` -> 0x40A78D36 = 5.235987663 (the limit); 0x00545C18 `vabs.f32 s2,s0`;
  0x00545C1C `vcmpe.f32 s2,s4`; 0x00545C24 `ble #0x00545C90`.
- Over the limit: 0x00545C26 `vldr s2,[pc,#0xb4]` -> 0x42652EE1 = **57.29578 (180/pi)**;
  0x00545C2E `vmul.f32 s0,s0,s2` (rad -> deg for the warning); 0x00545C3E `movw r1,#0xc000`;
  0x00545C42 `movt r1,#0x4072` -> 0x4072C000 = **300.0**; 0x00545C7E `movw r0,#0x8d36`;
  0x00545C82 `movt r0,#0x40a7`; 0x00545C86 `bfi r5,r0,#0,#0x1f` clamps to 0x40A78D36.
- So SetMaxSpeed takes **rad/s**; the warning converts to deg/s and the limit is 300 deg/s =
  5.235988 rad/s.

Units, `TurnInPlaceAction::SetAccel` **0x00545D14**:
- 0x00545D14 `vmov s0,r1`; 0x00545D18 `vcmp.f32 s0,#0`; 0x00545D20 `itt eq`;
  0x00545D22 `ldreq r1,[r0,#0x7c]`; 0x00545D24 `streq.w r1,[r0,#0xc8]`; 0x00545D28 `bxeq lr`;
  else 0x00545D2A `movs r1,#1`; 0x00545D2C `strb.w r1,[r0,#0xcc]`; 0x00545D30 `vstr s0,[r0,#0xc8]`.
- The stored value is r1 unchanged (the constructor default at +0x7C is 10.0 = 0x41200000), so accel
  is **rad/s^2**.

---

## 5. DoPlanning / Replan condition (X2 N10, gap1 G4/G10). Class: EXACT_SOURCE

`LatticePlannerImpl::DoPlanning` **0x00500090**:
- 0x00500102 `blx 0x004A6850` `Replan` (r1 = 0x01C9C380, r2 = fp+0xF2 abort flag);
  0x00500106 `mov r4,r0` saves the result; 0x00500132 `str r4,[sp,#8]`.
- 0x00500112 `addw r0,pc,#0x5cc` -> base align(0x00500112+4,4)=0x00500114 + 0x5cc = **0x005006E0 =
  "robot.lattice_planner_failure"**; 0x0050011C `addw r1,pc,#0x5e0` -> base 0x00500120 + 0x5e0 =
  **0x00500700 = "robot.lattice_planner_success"**; 0x00500126 `cmp r4,#0`; 0x00500134 `it eq`;
  0x00500136 `moveq r1,r0`. So Replan == 0 selects the failure string.
- 0x00500200 `ldr r0,[sp,#8]`; **0x00500202 `cbz r0,#0x00500216`**; 0x00500216 `movs r0,#0`;
  0x00500218 `b #0x00500590` -> **return 0 when Replan == 0**.
- 0x00500204 `mov r0,r4`; 0x00500206 `blx 0x004A688C` `GetPlan`; 0x0050020A `ldrd r1,r0,[r0,#8]`;
  **0x0050020E `cmp r0,r1`**; 0x00500210 `bne #0x0050021A`; **0x00500212 `movs r0,#3`**;
  0x00500214 `b #0x00500590` -> **return 3 when Replan != 0 and the segment list is empty**.
- **0x0050058E `movs r0,#2`**; 0x00500590 `str.w r0,[fp,#0xf8]` -> **return 2 on success**.

So DoPlanning returns **0** when Replan returns 0, **3** when Replan is non-zero with an empty plan,
**2** on success. X2 N10's "If `Replan` returns non-zero, `DoPlanning` returns 0" is backwards.

---

## Contradictions

1. **X2 row N23 and gap1 row G6b** — AlignWithObjectAction alignment-type mapping was inverted.
   Corrected by 0x005533E6 `tbb [pc,r6]` with table at 0x005533EA = `02 05 09 0c` and TBB scaling
   by 2: type 0 -> 0x005533EE `vmov.f32 s16,#6.0`; type 1 -> 0x005533F4 `strb.w r0,[r4,#0xbb]`
   (flag = 2, distance 0.0); type 2 -> 0x005533FC `vmov.f32 s16,#-15.0`; type 3 -> 0x00553402
   `vadd.f32 s16,s0,s2` (arg + -27.0).
2. **gap1 row G6b** — clamp threshold was -16.000001; corrected to **-16.000009536743164** by
   0x00553414 `vldr s0,[pc,#0x68]` -> 0x00553480 = 0xC1800005, compared at 0x0055341C
   `vcmpe.f32 s16,s0` / 0x00553424 `it mi` / 0x00553426 `vmovmi.f32 s16,s2`.
3. **X2 row N17** — BackupOntoChargerAction::CheckIfDone's pitch-below return was 0x04000004;
   corrected to **0x0400000A** by 0x0054E7DE `adds r4,#4` after 0x0054E7CA `movs r4,#6` /
   0x0054E7D0 `movt r4,#0x400` set 0x04000006. The fall-through returns 0x04000006 when
   `DriveStraightAction::CheckIfDone` returns 0 (0x0054E7E4..0x0054E7EC). 0x04000004 appears nowhere
   in the M13-008/M13-012 cited ranges.
4. **gap1 row G5a** — the 0.0 maxTurn entry is **0x005C36C0**, not 0x005C36C4 (0x005C36C4 holds the
   pointer 0x00A7B382). pi/2 is 0x005C36BC. Corrected by 0x005C34FE `adr r1,#0x1bc` -> 0x005C36BC and
   0x005C3504 `addgt r1,#4` -> 0x005C36C0.
5. **X2 row N16 and gap1 row G8** — the mount's turn-in-place numbers: 0x3FDF66F3 =
   **1.7453292608261108** rad/s (7 s.f. 1.745329), not 1.7459 (X2) / 1.7453293 (gap1);
   0x40A78D36 = **5.235987663269043** (7 s.f. 5.235988), not 5.2359877 (gap1). Cited at
   0x0054E550/0x0054E556 and 0x0054E55E/0x0054E564; units owned by `SetMaxSpeed` 0x00545C08 and
   `SetAccel` 0x00545D14.
6. **X2 row N10 and gap1 rows G4/G10** — the Replan condition was stated backwards. Corrected by
   0x00500106 `mov r4,r0`, 0x00500136 `moveq r1,r0` (failure string selected when Replan == 0),
   0x00500202 `cbz r0,#0x00500216` (return 0), 0x00500212 `movs r0,#3` (return 3), 0x0050058E
   `movs r0,#2` (return 2 on success).

---

## Records whose evidence is too weak or partial (unchanged from gap1, re-stated)

- **M13-002** (EXACT_SOURCE) — title names `AlignWithObjectAction`, but its evidence list is only the
  two bare addresses `FlipBlockAction 0x0055EC80` and `charger 0x004E9B6C`. Item 1 above supplies the
  missing AlignWithObjectAction instructions (0x005533E6..0x00553436, 0x005532B8) if the record is
  re-cited.
- **M13-008** (EXACT_SOURCE) — its evidence states the pitch threshold correctly (-0.261799 rad,
  0xBE860A92 at 0x0054E7CC) but never states the pitch-below result code, so the record itself is not
  contradicted; only X2 N17 carried the wrong 0x04000004. No record change needed for item 2 beyond
  correcting X2 N17.
- **M13-012** (EXACT_SOURCE) — does not carry the turn-in-place numbers; item 4 only corrects X2 N16
  and gap1 G8, not the record.

*Read-only extraction. Nothing outside this report file was changed.*


## Correction C-BM13: gap pass 3 (2026-09-28)
The build job B-M13 stopped on 19 MISSING points. Two read-only extractor passes settled them from the instructions (reports in Appendices D and E below). This section is the corrected, authoritative statement of every M13 record's evidence; where it differs from the records table above, this section governs. The statuses remain IMPLEMENTATION_GAP until the build is verified.
The corrections, per record:

### M13-001 — Lattice planner motion primitives loaded from the shipped cozmo_mprim.json
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs`; test `MotionPrimitiveTests.TheShippedMotionPrimitivesParseAsTheEngineReadsThem`*
- xythetaEnvironment::ReadMotionPrimitives 0x008529C0 opens the file "r" (0x00852A44/0x00852A48), Json::Reader::parse (0x00852A62) and ParseMotionPrims with oldFormat=false (0x00852B04/0x00852B06)
- xythetaEnvironment::ParseMotionPrims body 0x00852014 reads resolution_mm -> env+0 then 1/env[0] -> env+4 (0x0085203C/0x008520AA), num_angles -> env+8 (0x00852062), actions -> 24-byte ActionType vector at env+0x30 (0x008520BA), angle_definitions -> env+0x38 (0x008521C6), angles -> env+0x14 resized to num_angles (0x00852248/0x00852264), each prim -> MotionPrimitive::Create (0x0085230E)
- count mismatch aborts and returns 0: angle_definitions.size() != num_angles (0x0085223C bne -> 0x00852442 printf "ERROR: numAngles is %u, but we read %lu angle definitions" at 0x008527D8 -> 0x0085244C -> 0x0085274A movs r0,#0) and angles.size() != num_angles (0x0085225A bne -> 0x0085244E puts "error: could not find key 'angles' in motion primitives" 0x00C97D20 -> 0x0085244C); success calls PopulateReverseMotionPrims (0x008524AC) and returns 1 (0x008524B0)
- config/engine/cozmo_mprim.json sha256 4C79666BDD5F3F5FE6C7458B0E7D59ECBCCDB2AB3C12F1844D68BCC57887431D
- unresolved: compare LatticePlanner.cs's loader, schema and count validation against the cited instructions and the asset, then settle

### M13-002 — FlipBlockAction's constants, their roles and IAction type
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/FlipBlockAction.cs`; test `NavigationTests.FlipDrivesThroughTheCubeRaisingTheLiftAndForgetsItsPose`*
- FlipBlockAction ctor 0x0055EC80: IAction type 0xF (0x0055ECA8/0x0055ECAA); +0x12C=150.0 (0x43160000), +0x130=20.0 (0x41A00000) at 0x0055ECF6/0x0055ECFA; +0x134=45.0 (0x42340000), +0x138=40.0 (0x42200000), +0x13C=-1 at 0x0055ECFE..0x0055ED0E; +0x140=1 at 0x0055ED10/0x0055ED12
- +0x12C is the drive speed: Init 0x0055EF18 loads it into r3 and passes it to DriveStraightAction 0x0055EF2C; the r3 argument is stored at DriveStraightAction+0x7C (0x005472A4)
- +0x130 is the drive-past distance: Init computes the robot-to-object distance (vsqrt.f32 0x0055EEFA), adds +0x130 (0x0055EF14/0x0055EF1C) and passes the sum as DriveStraightAction's distance argument 0x0055EF20 (stored at DriveStraightAction+0x78)
- +0x134 is the approach lift height: Init 0x0055EF3A loads it into r2 for MoveLiftToHeightAction(45.0, 5.0, 0) 0x0055EF4A (added before the drive)
- +0x138 is the lift trigger distance: CheckIfDone 0x0055F124 vldr s0,[r4,#0x138] / vcmpe / bpl 0x0055F130; when the object is closer than it and +0x13C is still -1 it queues MoveLiftToHeightAction(preset 2, speed 5.0) 0x0055F14E and stores that action's id in +0x13C (0x0055F15C) through ActionList::QueueAction position 5 (0x0055F16A)
- +0x13C is the queued lift action's id, -1 when none; the destructor cancels it on robot+0x250 ActionList (0x0055ED6C/0x0055ED70/0x0055ED7A)
- +0x140 is shouldCheckPreActionPose: Init reads it (0x0055EE10) into the PreActionPoseInput byte at sp+0x64 (0x0055EE1C) passed to IDockAction::GetPreActionPoses with ActionType 5 (0x0055EE14/0x0055EE5E); SetShouldCheckPreActionPose writes it (0x0055EDC2)
- unresolved: compare FlipBlockAction.cs's constants and their roles against the cited instructions and settle

### M13-003 — Obstacle expansion, per-heading C-space polygons, and the primitive collision test
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs`; test `NavigationTests.ThePlannerRoutesAroundACubeInTheWay`*
- ImportBlockworldObstaclesIfNeeded 0x004FD4B8: paddings 7.0/6.0 (0x004FD4EE/0x004FD4F2) or 2.0/1.0 (0x004FD4EA/0x004FD4EC) selected by the function's own first bool argument (r4=r1 0x004FD4D2; cmp 0x004FD4E8; itt ne 0x004FD4F6); callers pass 0 (0x004FD2AC, 0x004FF92A) or 1 (StartPlanning 0x004FEC38)
- import log "robot padding %f, obstacle padding %f , didBlocksChange %d" (0x004FD51A/0x004FD546); penalty 0.1 (0x3DCCCCCD at 0x004FE0CE); AddObstacleWithExpansion 0x00855528 stores the caller's penalty at pair+0x44 (0x00855612) and calls ExpandCSpace 0x008550E8; ConvexPolygon::RadialExpand 0x004FDF9E
- ConvexPolygon::RadialExpand body 0x00841580: for each vertex v, v' = v + d*(v-c)/|v-c| with c the polygon centroid (ComputeCentroid 0x008415D8; per-vertex 0x008415F2..0x00841654), no bisector/cos and no clamp; a negative d only warns (0x00841590/0x00841598)
- IsInCollision(State) 0x008515BC converts the grid shorts to mm and tail-calls IsInCollision(State_c) 0x008515F8, which buckets theta as round(theta*env+0x10) mod env+8 (0x00851614..0x0085167A) and returns 1 only for a containing polygon with penalty >= 1000.0 (0x008516D0/0x008516DC); IsInSoftCollision 0x00851708 returns 1 for any containing polygon; GetCollisionPenalty 0x008517B0 returns the first containing polygon's penalty or 0.0 (0x008517C4/0x0085184E)
- the per-primitive test is Anki::Planning::SuccessorIterator::Next 0x0085110C: broad phase is the primitive's cached bbox (MotionPrimitive+0x1C..+0x28) against env+0x50 (0x0085123A..0x00851286); turning vs non-turning is decided by end_pose.theta vs MotionPrimitive+1 (0x008512CC/0x008512D0)
- non-turning primitive: every intermediate pose is tested (loop 0x0085134A..0x008513A6, stride 0x14) at (start + IntermediatePosition.x_mm, +y_mm) in the end_pose.theta bucket (0x008512E0/0x008512E4); turning primitive: the intermediate poses are walked last to first (0x00851414/0x0085149E) each in its own bucket at IntermediatePosition+0xC (0x00851420/0x00851428)
- soft hit (polygon penalty < 1000.0) adds base + penalty*IntermediatePosition+0x10 to the successor cost (non-turning 0x0085137E..0x00851398; turning 0x0085146E..0x00851490); penalty >= 1000.0 is a hard collision and rejects the primitive (0x0085138A/0x0085147A; threshold 1000.0 at 0x00851162); base is 0.0 forward and 1000.0 for a reverse primitive (0x008515B0/0x008515B4)
- successor g = soft-collision cost + parent g + MotionPrimitive+4 (0x0085150A/0x00851516/0x0085151A/0x0085151E/0x00851522); the 0.1 penalty means the shipped obstacles are all soft
- unresolved: compare the paddings, the 0.1 penalty, the per-heading C-space polygon, RadialExpand and the primitive collision test against the cited instructions and settle

### M13-004 — The primitive file's schema, the end/intermediate pose import and the traversal cost
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs`; test `MotionPrimitiveTests.ATurningPrimitiveArcEndsOnItsEndCell`*
- ActionType::Import 0x00851A18: extra_cost_factor -> +0 (0x00851A96), index -> +4 (0x00851AB8), name -> +8 (0x00851ADC), reverse_action -> +0x14 (0x00851B48); ctor 0x008519EC
- MotionPrimitive::Create 0x00853DD0: action_index -> MotionPrimitive+0 (GetValueOptional<unsigned char> output sb at 0x00853E04/0x00853E06; read at 0x00854036), the heading/angle index -> +1 (Create's 2nd argument, 0x00853DEC; read at 0x00854280), end_pose -> +8 via State::Import (0x00853E30), intermediate_poses -> vector at +0x14 (0x00853E3E, each State_c::Import 0x00853E8C), straight_length_mm (0x00854094/0x008540D0/0x00854140), arc keys sweepRad/radius_mm/centerPt_x_mm/centerPt_y_mm/startRad (0x00854160/0x0085417C/0x008541CE/0x008541E4/0x0085420C) -> AppendArc 0x0085425A, turn_in_place_direction -> AppendPointTurn 0x0085431A; a per-primitive extra_cost_factor is rejected (0x00853FFC)
- State::Import body 0x0084F8A4 reads "x" as a short -> State+0 (0x0084F920, key 0x0084FB08), "y" as a short -> +2 (0x0084F940, key 0x0084FB0C) and "theta" as an unsigned char -> +4 (0x0084F962, key 0x00C299A3); x/y are grid cells and theta a heading index
- State_c::Import body 0x0084FD70 reads "x_mm"/"y_mm"/"theta_rads" as floats -> +0/+4/+8 (0x0084FDEE/0x0084FE10/0x0084FE34)
- traversal cost = extra_cost_factor * d8 * (|straight_length_mm| + |sweepRad|*(|radius_mm| + halfWheelBase_mm) + halfWheelBase_mm*|dtheta|), stored as a float at MotionPrimitive+4: initialised 0 at 0x0085408A, straight 0x008540AA..0x008540C8, arc 0x00854188..0x008541C4, turn 0x008542D6..0x008542F2, then multiplied by extra_cost_factor 0x00854364/0x00854368/0x00854378
- d8 = 1/maxVelocity_mmps (env+0x78) for a forward action (0x00854046/0x00854078) and 1/maxReverseVelocity_mmps (env+0x70) for a reverse one (0x0085404C/0x00854050); env+0x60 is a RobotActionParams constructed at 0x00851EDE with the defaults halfWheelBase 24.0, maxVelocity 60.0, maxReverseVelocity 25.0 (ctor 0x0084EDE6; Import 0x0084EF28 has no callers and the asset has no such keys)
- a base or final cost below 1e-6 (0x008543E0) logs and makes Create return 0 (0x00854334/0x00854370/0x0085437C); on success it calls CacheBoundingBox (0x008543B2) and returns 1 (0x008543B8)
- asset: resolution_mm 10.0, num_angles 16, 9 actions with cost factors 1.0001/1.0/1.0/1.0/1.0/1.0/2.0/2.0/1.2, 144 primitives; sha256 4C79666BDD5F3F5FE6C7458B0E7D59ECBCCDB2AB3C12F1844D68BCC57887431D
- unresolved: compare the schema, the pose import, the arcs and the traversal cost formula against the cited instructions and settle

### M13-005 — A lattice-planner failure sends no path
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/DriveActions.cs`; test `CorrectionTests.APlannerFailureSendsNoPathEvenWhenTheStraightLineIsClear`*
- DoPlanning 0x00500090 has no substitute path: the non-empty-plan branch appends GetPlan() (0x00500540) and returns
- the failure condition is Replan == 0: 0x00500106 saves the result, 0x00500136 selects "robot.lattice_planner_failure" when it is zero, 0x00500202 cbz returns 0
- unresolved: compare DriveActions.cs's failure handling against the cited instructions and settle

### M13-006 — The whiteboard keeps a list of beacons and the active one is the first
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/AIWhiteboard.cs`; test `BeaconTests.TheActiveBeaconIsTheFirstOneAdded`*
- AIWhiteboard::AddBeacon 0x0056C39C appends a 20-byte AIBeacon (Pose3d, a float radius at +0xC, an int at +0x10) to the vector at whiteboard+0x60, growing it through __emplace_back_slow_path (0x0056C3A8/0x0056C3D2)
- ClearAllBeacons 0x0056AA08 walks it back (0x0056AA10 subs r0,#0x14)
- GetActiveBeacon 0x0056C404 compares begin/end and returns begin, or null when equal (0x0056C408/0x0056C40C); the active beacon is the oldest still standing
- FailedToFindLocationInBeacon 0x0056C3F0 forwards to AIBeacon::FailedToFindLocation
- unresolved: compare AIWhiteboard.cs against the cited instructions and settle

### M13-007 — The stack tolerance is 30 mm when building a stack and 15 mm everywhere else
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/BlockConfigurations.cs`; test `ManipulationTests`*
- BlockWorld::FindObjectOnTopOrUnderneathHelper(obj, float tolerance, filter, bool) 0x0062601C takes the tolerance as its second argument (r2 captured at 0x00626038 into the lambda at 0x006260CE/0x006260D8), reads the height through GetDimInParentFrame<(char)90> (0x00626070), adds +/-half to the parent Z (0x0062607E..0x006260A0) and calls FindLocatedObjectHelper (0x0062610E)
- StackOfCubes::BuildTallestStackForObject passes 30 (0x41F00000 at 0x0061928C and 0x00619344); BlockWorld::UpdatePoseOfStackedObjects 0x00621908, DockingComponent::CanInteractWithObjectHelper 0x0063C728 and CarryingComponent::SetObjectAsAttachedToLift 0x00632F0C pass 15 (0x41700000)
- unresolved: compare BlockConfigurations.cs against the 30/15 split and settle

### M13-008 — The mount sequence and the reverse onto the charger, with the retry result codes
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/ChargerActions.cs`; test `NavigationTests.MountChargerAlignsTurnsAndBacksOntoTheContacts`*
- ConfigureTurnAndMountAction 0x0054E500: TurnInPlaceAction at atan2 of the vector to the marker (0x0054E52E/0x0054E536), SetMaxSpeed(0x3FDF66F3 = 1.745329 rad/s) at 0x0054E550/0x0054E55A and SetAccel(0x40A78D36 = 5.235988 rad/s^2) at 0x0054E55E/0x0054E568; then DriveStraightAction(-120 mm, 30 mm/s, false) whose vtable becomes BackupOntoChargerAction's (0x0054E5FC..0x0054E618)
- BackupOntoChargerAction::CheckIfDone 0x0054E7A8: on contacts (robot+0x338) SetPoseOnCharger and return 0 (0x0054E7B0..0x0054E7BE); pitch below -0.261799 rad (0xBE860A92 at 0x0054E7CC) returns 0x0400000A (0x0054E7DE adds r4,#4); fall-through returns 0x04000006 when DriveStraightAction::CheckIfDone returns 0 (0x0054E7E4..0x0054E7EC)
- MountChargerAction::CheckIfDone 0x0054E2D0 reaches the pi/2 comparison (0x3FC90FDB at 0x0054E374) only when the turn-and-mount sub-action FAILED (0x0054E31A skips 0 and 0x1000000); outside the window ConfigureDriveForRetryAction 0x0054E72C runs DriveStraightAction(120, 100) returning 0x04000006 (0x0054E3E8/0x0054E742/0x0054E748); an align failure ends the action (0x0054E2FA)
- unresolved: compare ChargerActions.cs against the turn numbers, the reverse and the result codes, and settle

### M13-009 — The charger's dimensions, its docked pose and its one pre-dock pose
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Vision/ChargerGeometry.cs`; test `NavigationTests.TheChargerGeneratesOnePreDockPoseOnItsAxisTwoHundredAndFiftyMillimetresOut`*
- Charger::Charger 0x004E9B6C stores 96.0/80.0/31.0 at +0xF0..+0xF8 (0x004E9B98/0x004E9BB4/0x004E9BB0/0x004E9BB8) and adds one marker: id 2 (0x004E9C16), angle -pi/2 about Z (0xBFC90FDB at 0x004E9BBC) at (86,0,22) (0x004E9BD6/0x004E9BE2), size Point2 x=27.0 at sp+0x14 and y=20.0 at sp+0x18, the AddMarker pointer being sp+0x14 (0x004E9C28/0x004E9C30/0x004E9C36/0x004E9C38)
- Charger::GetRobotDockedPose 0x004EA1A0 = Pose3d(Radians(pi), Z_AXIS, (30,0,0)) on the charger pose (0x004EA1AC/0x004EA1C6/0x004EA202)
- Charger::GeneratePreActionPoses 0x004E9FB0 emits one pose for action types 0 and 1 only (0x004E9FD4/0x004E9FD6): Pose3d(Radians(p.angle + pi/2), Z_AXIS, (p.x, -p.y, -15.5)) parented to the marker (0x004E9FE4/0x004E9FF2/0x004EA012/0x004EA018/0x004EA01E); p is the file-static Pose2d at 0x01059148 initialised (Radians(0), 0.0, 250.0) at 0x004D6BC4/0x004D6BD6
- unresolved: compare ChargerGeometry.cs against the cited dimensions, the marker (x=27,y=20) and the poses, and settle

### M13-010 — The workouts run in file order, the last one repeats, and ShouldPlayEightiesMusic rolls against the mood score
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/Workouts.cs`; test `NavigationTests.TheWorkoutConfigParsesAndScoresLiftsFromConfidence`*
- WorkoutComponent::GetCurrentWorkout 0x00573DE8 returns the pointer at +0xC (0x00573DE8/0x00573DF0)
- CompleteCurrentWorkout 0x00573DEC triggers the workout emotion event (MoodManager::TriggerEmotionEvent 0x00573E1C) and advances by one 0x40-byte entry unless it is the last (0x00573E24/0x00573E28/0x00573E2A)
- ShouldPlayEightiesMusic 0x00573E30 returns the cached answer at +0x10 once +0x11 is set (0x00573E34/0x00573E36/0x00573E38); otherwise it scores the current workout's MoodScorer at workout+0x18 through WorkoutConfig::MoodScoreHelper 0x00573B70 -> MoodScorer::EvaluateEmotionScore 0x0067C9B8 and roundf (0x00573E42/0x00573B86/0x00573B8A), skips the roll when the score is 0 (0x00573E48), and returns true iff RandomGenerator::RandDbl(1.0) < 0.1 (0x00573E58; threshold double 0.1 at 0x00573E80) ; it then sets +0x11 = 1 and stores the answer at +0x10 (0x00573E70/0x00573E72/0x00573E74)
- unresolved: compare Workouts.cs against the file-order advance and ShouldPlayEightiesMusic's score/roll/cache, and settle

### M13-011 — The three path segment messages are a Planning::PathSegment copied field for field
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/RobotPath.cs`; test `NavigationTests`*
- PathDolerOuter::Dole 0x00507E4C switches on the segment type at PathSegment+0 (1 line, 2 arc, 3 point turn; 0x00507F40/0x00507F44/0x00507F48) and copies words at +4, +8, +0xC, +0x10 (arc adds +0x14), then the speed profile at +0x1C, +0x20, +0x24; a point turn also copies the byte at +0x14 (line 0x00507FB6..0x00507FD2; arc 0x00508008..0x00508028; point turn 0x00507F5E..0x00507F7E)
- the field definers: PathSegment::DefineLine 0x0085BC00, DefineArc 0x0085BC98, DefinePointTurn 0x0085BD28, SetSpeedProfile 0x0085BC90
- unresolved: compare RobotPath.cs against the field layout and settle

### M13-012 — The mount raises the lift to 45 mm when it is below it, not when it is above
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/ChargerActions.cs`; test `NavigationTests.TheMountCarriesTheEnginesOwnNumbers`*
- ConfigureTurnAndMountAction reads Robot::GetLiftHeight (0x0054E58A) and branches past the lift move when the lift is at or above 45.0 (0x42340000 at 0x0054E592; bpl at 0x0054E59E); otherwise MoveLiftToHeightAction(45, 5, 0) (0x0054E5B2..0x0054E5C6)
- unresolved: compare the lift branch against the cited instructions and settle

### M13-013 — DriveOffChargerContactsAction: a drive straight that retries while still on the charger
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/ChargerActions.cs`; test `NavigationTests.DriveOffChargerContactsActionFailsWhileStillOnTheContacts`*
- DriveOffChargerContactsAction ctor 0x00558228: DriveStraightAction(10 mm, 20 mm/s, false) (0x00558232/0x00558236/0x0055823E); then +0x44 = 7 (0x00558276/0x00558278), which is IActionRunner's RobotActionType (stored at +0x44 by the IActionRunner ctor 0x0053FDCC) and 7 = DRIVE_OFF_CHARGER_CONTACTS (RobotActionTypeFromString 0x0075A448 maps the string at 0x00C1604E to 7)
- in SDK mode only, the constructor clears the required-track lock: CozmoContext::IsInSdkMode 0x0055827C, cmp #1 0x00558280, then IActionRunner::SetTracksToLock(0) 0x00558286/0x00558288; SetTracksToLock 0x00540918 writes the argument to IActionRunner+0x54 only when the action state (+0x18) is 0x2000001 (not started), else warns; it is a local pre-run flag consumed by IActionRunner::Update 0x00540438 (MovementComponent::AreAnyTracksLocked), not a robot message
- Init 0x005582D0 copies robot+0x338 into action+0x8B (0x005582D2/0x005582D6) and returns 0 when not on contacts
- CheckIfDone 0x005582E4 retries while the drive is still running and fails 0x04000009 if still on the contacts (0x00558344)
- unresolved: compare ChargerActions.cs against the cited constructor/Init/CheckIfDone and settle

### M13-014 — Knock over a stack: BehaviorKnockOverCubes flow, DriveAndFlipBlockAction and IDriveToInteractWithObject
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs`; test `NavigationTests.DriveAndFlipBlockAddsBothTurnsWhenMaxTurnIsPositive`*
- BehaviorKnockOverCubes body 0x005C2EA0..0x005C3E2A; IsRunnableInternal 0x005C314C needs StackOfCubes::GetStackHeight() >= +0x124 (minimumStackHeight default 3); InitInternal 0x005C31A2 runs the reach unless +0xD9 (alwaysStreamline) or +0xD8 (the soft/hard switch flag) is set (0x005C31B4/0x005C31BA)
- TransitionToReachingForBlock 0x005C3254: TurnTowardsObjectAction(max pi), then DriveStraightAction(x-85, 60) when the block x+10 > 85.0, then TriggerLiftSafeAnimationAction(+0x150), then TransitionToKnockingOverStack regardless of result
- TransitionToKnockingOverStack 0x005C34A8: TurnTowardsObjectAction(max pi), DriveAndFlipBlockAction with maxTurn = pi/2 on the first attempt (0x005C36BC) and 0.0 once +0x140 > 0 (0x005C36C0); +0xD9 or +0xD8 also forces 0.0 (0x005C34EA..0x005C34F8); WaitAction(0.5); the callback 0x005C3DCE writes AIWhiteboard+0x70 on NoPreActionPoses (0x03000010), re-runs while +0x140 <= 1 on a Retry result and otherwise blind-flips, incrementing +0x140 either way
- success goes to TransitionToPlayingReaction 0x005C3908: it sets robot->[+0x34]->[+0x94]->[+0xC] = 1 (0x005C3946..0x005C394E), i.e. the BlockWorld's BlockConfigurationManager dirty flag (BlockWorld+0x94 is the manager; BlockConfigurationManager+0xC is read by its Update 0x00616D84) forcing all block configurations to recompute; the tipped-object set size at +0x14C selects the success trigger +0x15C with BehaviorObjectiveAchieved(0xD, true) and NeedActionCompleted(0) (0x005C3950..0x005C3964) or the failure trigger +0x160 (0x005C396E); when +0xD9 or +0xD8 is set the reaction animation is skipped (0x005C3972/0x005C3978)
- IDriveToInteractWithObject 0x0055B1F4 adds TWO actions when maxTurn > 0 (0x0055B37C..0x0055B392): a TurnTowardsLastFacePoseAction (vtable overwritten from TurnTowardsFaceAction at 0x0055B3C4..0x0055B3D8) and a TurnTowardsObjectAction (0x0055B42E/0x0055B43C), both with failure ignored; the trailing float 20.0 is not read by DriveAndFlipBlockAction ctor 0x0055E208
- +0xD9 is the JSON config key alwaysStreamline (IBehavior::ReadFromJson 0x005BC208/0x005BC216/0x005BC21E); +0xD8 is computed by IBehavior::Init 0x005BCCAA..0x005BCCC2 as 1 when the behaviour was entered by a soft spark switch and 0 for a hard switch or no current behaviour
- unresolved: compare CubeGameBehaviors.cs against the cited flow, including the second TurnTowardsObjectAction, the BlockConfigurationManager dirty flag and the +0xD8/+0xD9 streamline gate, and settle

### M13-015 — Pop a wheelie: one retry, the realign or retry animation, and what a failure marks
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs`; test `NavigationTests.PopAWheelieRetriesWithTheRetryAnimationWhenTheDockFails`*
- TransitionToPerformingAction(Robot&, bool) 0x005C7758 bumps +0x12C on a retry (0x005C777E) and zeroes it otherwise (0x005C780C); the completion lambda 0x005C7CBC splits on result >> 24: success sets +0x128=-1, plays SuccessfulWheelie 0x21C and reports objective 0x16 and the needs action; a Retry result calls SetupRetryAction only while the count is at most 0, otherwise AIWhiteboard::SetFailedToUse(obj, 3)
- SetupRetryAction 0x005C79D0 plays PopAWheelieRealign 0x18D for exactly 0x04000001 (0x005C79F8/0x005C79FE) and PopAWheelieRetry 0x18E otherwise (0x005C7A10/0x005C7A32); ResetBehavior 0x005C76B0 sends EnableStopOnCliff(true) (0x005C76E4)
- unresolved: compare CubeGameBehaviors.cs against the retry count, the animations and the failure mark, and settle

### M13-016 — AlignWithObjectAction's alignment-type table and pre-action type
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/ChargerActions.cs`; test `NavigationTests.AlignWithObjectUsesTheEnginesAlignmentTypeTable`*
- AlignWithObjectAction ctor body 0x00553370: cmp r6,#3 / bhi 0x005533CC/0x005533E4; tbb [pc,r6] 0x005533E6 with table base 0x005533EA = 02 05 09 0c (TBB scales by 2): type 0 -> 0x005533EE vmov.f32 s16,#6.0; type 1 -> 0x005533F4 flag +0xBB=2; type 2 -> 0x005533FC vmov.f32 s16,#-15.0; type 3 -> 0x00553402 vadd.f32 s16,s0,s2 (argument + -27.0)
- clamp: 0x00553414 vldr s0,[pc,#0x68] -> 0x00553480 = 0xC1800005 = -16.000009536743164; vcmpe/it mi/vmovmi at 0x0055341C..0x00553426 set 0.0 when below it
- GetPreActionTypeFromAlignmentType 0x005532B8: table at 0x00553360 maps 0->1, 1->0, 2->1, 3->1; invalid type logs and returns 1 (0x005532CC/0x00553320); the result is stored at AlignWithObjectAction+0xFC (0x0055342A) and indexes Anki::Cozmo::PreActionPose::ActionType (passed as that type by DriveToAlignWithObjectAction 0x0055C3A0 to IDriveToInteractWithObject); the values 0..6 are native (Block::GeneratePreActionPoses 0x004E5808 cmp r5,#5 / tbh 0x004E595E), the names have no shipped table (UNKNOWN)
- action+0xBB is IDockAction's DockingMethod field: the IDockAction ctor leaves it 0 (0x005503AA), DriveToPickupObjectAction::SetDockingMethod writes it (0x0055C584/0x0055C5A2) and IDockAction::CheckIfDone passes it to DockingComponent::DockWithObject (0x00552288/0x005522AE); DockingMethodFromString 0x007C013C gives BLIND_DOCKING 0, TRACKER_DOCKING 1, HYBRID_DOCKING 2, EVEN_BLINDER_DOCKING 3, so alignment type 1 runs the dock with HYBRID_DOCKING and every other type with BLIND_DOCKING
- unresolved: compare the alignment-type table, the clamp, the pre-action type and the DockingMethod flag against the cited instructions and settle

### M13-017 — BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs`; test `NavigationTests.DriveOffChargerDrivesTheChargerLengthPlusTheExtraAndFiresTheEvent`*
- ctor 0x005C0980 stores 96.0 + json extraDistanceToDrive_mm at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6); IsRunnableInternal 0x005C0B10 returns robot+0x34A (the on-contacts flag)
- InitInternal 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions only when robot+0x355 == 0 (0x005C0B54); +0x355 is the robot's OffTreadsState (OnTreads = 0), written only by Robot::CheckAndUpdateTreadsState 0x00512088/0x0051208E and named by OffTreadsStateFromString 0x0078DF58 (OnTreads 0, InAir 1, OnBack 2, OnLeftSide 3, OnRightSide 4, OnFace 5, Falling 6)
- TransitionToDrivingForward 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08)
- UpdateInternal 0x005C0DA8: once off the contacts (robot+0x34A == 0) it records the time at robot->[+0x264]->[+0x18]+0x44 and returns 2 (0x005C0DF4..0x005C0E0A); while still on the contacts and robot+0x355 != 0 it calls StopActing(false,false) (0x005C0DB6/0x005C0DC4) and waits; on the contacts with +0x355 == 0 it transitions to driving forward (0x005C0E18)
- unresolved: compare the drive-off behaviour against the cited constructor/IsRunnable/Init/Transition/Update and settle

### M13-018 — LatticePlannerImpl's planning entry and worker, the Replan argument, the heuristic and the result codes
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs`; test `NavigationTests.DoPlanningReturnsTheEngineResultCodes`*
- StartPlanning 0x004FEB44 stores its bool argument at impl+0xA1 (0x004FEB56/0x004FEB7E); true forces an unconditional replan and imports with the 7.0/6.0 padding (ImportBlockworldObstaclesIfNeeded with bool 0 at 0x004FEEFE), false first imports with the 2.0/1.0 padding (bool 1 at 0x004FEC38/0x004FEC3A) and reuses a safe old plan (FindClosestPlanSegmentToPose 0x004FEC50; PlanIsSafe 0x004FF424 -> return 2 at 0x004FF42E); ComputePathHelper passes true through the vtable slot +0xC (0x004FD326..0x004FD330)
- DoPlanning 0x00500090 sets status 1 at impl+0xF8 (0x00500098) and sleeps in chunks of min(remaining,10) ms up to impl+0x108 ms, checking the run flag each iteration (0x005000A2..0x005000EA; 0x005000C8/0x005000CE); impl+0x108 defaults 0 (ctor 0x004FCCF6) and is set by LatticePlanner::SetArtificialPlannerDelay_ms 0x004FFFEC; the flag is impl+0xF2, a run/continue flag (1 = keep planning): ctor 1 (0x004FCCCA), StartPlanning 1 (0x004FF310), StopPlanning 0 (0x004FD1C8); DoPlanning reads it at 0x005000BE/0x005000C0 and passes it to Replan at 0x005000FE
- then calls Replan(0x01C9C380, runFlag) (0x005000F2/0x005000FA/0x00500102); 0x01C9C380 = 30,000,000 is the maximum number of state expansions: xythetaPlannerImpl::ComputePath 0x008586A0 warns "exceeded max expansions of %u, stopping" and returns 0 on overflow (0x008588C6/0x008588CA/0x00858A96); Replan returns 0 on failure, 1 on success
- DoPlanning returns 0 when Replan == 0 (0x00500202), 3 when Replan != 0 with an empty plan (0x00500212) and 2 on success (0x0050058E/0x00500590); the code is stored at impl+0xF8 (0x00500218/0x00500590); both direct callers discard r0 (worker 0x00500838; the synchronous StartPlanning branch 0x004FF35E), and the only in-engine consumer of the status, GetCompletePath 0x004FFB64, treats 2 and 3 alike (append the plan when the robot is within 20.0 mm) and has no special case for 3
- the A* heuristic is not a precomputed grid: heur_internal 0x0085A7B8 returns min_i(heurMap[i] + GetDistanceBetween(goal_i, state)/maxVelocity_mmps) (0x0085A7FA/0x0085A808/0x0085A816), memoized in the hash map at planner+0xAC by heur 0x0085A780; GetDistanceBetween 0x00850DCC is the Euclidean distance between (resolution_mm*state.x, resolution_mm*state.y) and the goal's x_mm/y_mm (0x00850DDC/0x00850DF0/0x00850E18)
- heurMap comes from InitializeHeuristic 0x008598DC: it resizes planner+0xA0 to the goal count (0x0085991E) and for each goal runs ExpandCollisionStatesFromGoal 0x0085998E, a soft-collision Dijkstra (0x00859BF0..0x00859FBD), dropping goals whose cost exceeds 1000.0 (0x008599E0) and storing the rest at heurMap+4 (0x008599CA)
- the open list is a min-f std::multimap<float,StateID> (0x0084E4E8; top/pop 0x0084E500/0x0084E508/0x0084E50E); ExpandState 0x0085A34C inserts with f = g + heur (0x0085A4AC) and ComputePath 0x008586A0 pops the smallest, checks the abort flag (0x00858886..0x00858890), looks up goals (0x008588A4), expands (0x008588B6) and compares the expansion count with the cap (0x008588C8)
- unresolved: compare the planner entry/worker, the 30,000,000 cap, the heuristic and the result codes against the cited instructions and settle

### M13-019 — The production xythetaEnvironment uses the JSON num_angles; the hard-coded 16 is in the uncalled Init(char const*) overload
*status IMPLEMENTATION_GAP; location `cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs`; test `NavigationTests.TheShippedMotionPrimitivesParseAsTheEngineReadsThem`*
- the production planner constructs its environment with the JSON overload xythetaEnvironment::Init(Json const&) 0x00851F9E (called from the LatticePlannerImpl ctor 0x004FCC44; PLT 0x004A64F0 resolves to 0x00851F9E), which calls ClearObstacles and ParseMotionPrims(env, json, false) and does not overwrite env+8, so the heading count is the JSON num_angles
- the hard-coded-16 override is in the other, uncalled overload xythetaEnvironment::Init(char const*) 0x008528A8 (no callers in the shipped code): it calls ReadMotionPrimitives (0x008528AE) then overwrites env+8 with 0x10 (0x008528B6/0x008528BA), resizes the per-theta obstacle table at env+0x44 to 16 (0x008528C0) and sets env+0xC = 2pi/16 and env+0x10 = 1/(2pi/16) (0x008528C4..0x008528E6)
- the shipped asset's num_angles is 16, so the production path and the uncalled overload agree on 16
- unresolved: compare LatticePlanner.cs's heading count against the JSON num_angles (production) and settle

**Records whose wording was contradicted by the instructions (corrected above):**
- **M13-019** — the 16-heading override is on the uncalled `Init(char const*)` 0x008528A8; the production `Init(Json const&)` 0x00851F9E uses the JSON `num_angles` (asset 16). The old title "hard-codes 16 headings and overrides the JSON num_angles" was wrong for the production path.
- **M13-004** — `action_index` is `MotionPrimitive+0`, not +1; +1 is the heading/angle index. The traversal-cost formula (and the `d8` speed reciprocals) was missing and is now stated.
- **M13-014** — the store in `TransitionToPlayingReaction` is `robot+0x34 -> +0x94 -> +0xC` (BlockWorld -> BlockConfigurationManager dirty flag), not `robot+0x264 -> +0x18 -> +0xC`; the `+0xD8`/`+0xD9` streamline gate was missing and is now stated.
- **M13-016** — the pre-action-type numeric indexes `PreActionPose::ActionType` (values native, names not in the binary); the +0xBB flag is IDockAction's `DockingMethod` (type 1 -> HYBRID_DOCKING).
- **M13-002** — the constructor offsets' roles are now read (`Init` 0x0055EDC8, `CheckIfDone` 0x0055F074): +0x12C speed, +0x130 drive-past, +0x134 approach lift, +0x138 lift trigger, +0x13C queued-lift id, +0x140 shouldCheckPreActionPose.
- **M13-003** — the per-primitive collision sampling (SuccessorIterator::Next) and the `RadialExpand` vertex math (`v + d*(v-c)/|v-c|`) are now stated; the stack's bisector/0.3-clamp guess was not the source.
- **M13-013** — +0x44 is the IActionRunner `RobotActionType` 7 (`DRIVE_OFF_CHARGER_CONTACTS`); `SetTracksToLock(0)` is a local IActionRunner flag write, not a robot message.
- **M13-017** — `robot+0x355` is the robot's `OffTreadsState` (OnTreads 0), not an opaque wait.
- **M13-010** — `ShouldPlayEightiesMusic`'s score (`round(MoodScoreHelper)`), roll (`RandDbl(1.0) < 0.1`) and cache (+0x10 answer, +0x11 done) are now stated.


## Appendix D: gap pass 3 extraction report (2026-09-28)

﻿# B-M13 extractor — lattice planner internals (questions 1..7)

Read-only extraction. Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, file VAs, Thumb.
Ghidra decomp in `re-analysis/decomp/libcozmoEngine/` used only to navigate; every claim below was
re-read from the instructions with capstone (PC-relative literals resolved). Asset:
`re-analysis/obb/assets/cozmo_resources/config/engine/cozmo_mprim.json`.

Answer summary: all seven questions are settled from the shipped code. One prior-record error is
contradicted (M13-004's `action_index` offset), and one record rests on the wrong overload
(M13-019). Details and citations follow.

---

## Production-path steps (one row per behaviour-changing step)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| P1 | `MotionPrimitive::Create` zeroes the primitive cost field at `MotionPrimitive+4`, then accumulates `d8*abs(straight_length_mm)` | 0x0085408A `str.w r0,[sb,#4]` (r0=0); 0x008540AA `vabs.f64 d0,d0`; 0x008540B4 `vmul.f64 d0,d8,d0`; 0x008540C8 `vstr s0,[sb,#4]` | M13-004 (partial) | EXACT_SOURCE |
| P2 | `d8` is chosen per action: forward = env+0x78 (`1/maxVelocity_mmps`), reverse = `1/env+0x70` (`1/maxReverseVelocity_mmps`) | 0x00854044 `ldrb r7,[r0,#0x14]`; 0x00854046 `cbz r7,#0x854078`; 0x0085404C `vldr d9,[r4,#0x70]` / 0x00854050 `vdiv.f64 d8,d0,d9`; 0x00854078 `vldr d9,[r4,#0x68]` / 0x0085407C `vldr d8,[r4,#0x78]` | NEW | EXACT_SOURCE |
| P3 | Arc adds `d8*abs(sweepRad)*(abs(radius_mm)+halfWheelBase_mm)` (halfWheelBase = env+0x60) | 0x00854188 `bfc r1,#0x1f,#1`; 0x008541A4 `vadd.f64 d0,d1,d0`; 0x008541AC `vmul.f64 d0,d8,d0`; 0x008541C4 `vstr s0,[sb,#4]` | NEW | EXACT_SOURCE |
| P4 | Point turn adds `d8*halfWheelBase_mm*abs(dtheta)` | 0x008542D6 `vmul.f64 d1,d2,d1`; 0x008542DE `vmul.f64 d1,d8,d1`; 0x008542F2 `vstr s2,[sb,#4]` | NEW | EXACT_SOURCE |
| P5 | The whole base cost is multiplied by `ActionType+0x00` (`extra_cost_factor`) and stored back at `MotionPrimitive+4` | 0x0085432E `ldr r2,[r4,#0x2c]`; 0x00854360 `add.w r2,r2,r0,lsl#3`; 0x00854364 `vldr s0,[r2]`; 0x00854368 `vmul.f32 s4,s4,s0`; 0x00854378 `vstr s4,[sb,#4]` | NEW | EXACT_SOURCE |
| P6 | If base cost < 1e-6 or final cost < 1e-6 it logs "base/final action cost" and `Create` returns 0; else it calls `CacheBoundingBox` and returns 1 | 0x00854322 `vldr d1,[pc,#0xbc]` -> 0x008543E0 = 1e-6; 0x00854334/0x0085433C; 0x00854370/0x0085437C; 0x008543B2 `blx CacheBoundingBox`; 0x008543B8 `movs r0,#1` | NEW | EXACT_SOURCE |
| P7 | The successor g is `parentG + MotionPrimitive+4 + softCollisionCost` | 0x0085150A `vldr s2,[r5,#0x14]`; 0x00851516 `vldr s4,[r1,#4]`; 0x0085151A `vadd.f32 s2,s2,s4`; 0x0085151E `vadd.f32 s0,s0,s2`; 0x00851522 `vstr s0,[r5,#0x24]` | NEW | EXACT_SOURCE |
| P8 | Heuristic: `heur_internal(state) = min_i ( heurMap[i] + GetDistanceBetween(goal_i.State_c, state) / maxVelocity_mmps )`; `heur` memoizes in a hash map at planner+0xAC | 0x0085A7DE..0x0085A83A; 0x0085A780 (cache find); 0x00850DDC `vldr s2,[r0]` (resolution) | M13-018 (partial) | EXACT_SOURCE |
| P9 | `InitializeHeuristic` resizes heurMap (planner+0xA0) to the goal count, and for each goal stores `ExpandCollisionStatesFromGoal(goal)` (a Dijkstra expansion through soft-collision states), dropping goals with cost > 1000 | 0x0085991E `resize`; 0x00859980 `IsInSoftCollision`; 0x0085998E `ExpandCollisionStatesFromGoal`; 0x00859942 s16=1000.0; 0x008599E0 `vcmpe`/0x008599E8 `ble`; 0x008599CA `vstr s20,[r0,#4]` | NEW | EXACT_SOURCE |
| P10 | Open list is a `std::multimap<float,StateID>` ordered by `std::less<float>`; pop/top take the smallest key | 0x0084E4E8 (`__tree<...less<float>...>`); 0x0084E500 `ldr r1,[r1]` / `ldr r1,[r1,#0x14]` (top value); 0x0084E50E pop erases root; 0x0084E530 `__emplace_multi` | NEW | EXACT_SOURCE |
| P11 | `ComputePath` loop: abort-flag check, `OpenList::pop`, goal-hash lookup, `ExpandState`, expansion counter vs max | 0x00858886..0x00858890 (abort); 0x00858898 `pop`; 0x008588A4 `find`; 0x008588B6 `ExpandState`; 0x008588C8 `cmp` max | M13-018 (partial) | EXACT_SOURCE |
| P12 | `ExpandState`: f = g + heur; on a better g it removes the old open entry and reinserts; otherwise emplaces a new node | 0x0085A486/0x0085A48E `heur`; 0x0085A49E `OpenList::remove`; 0x0085A4AC `vadd.f32 s0,s18,s0`; 0x0085A4BC `insert`; 0x0085A522 `StateTable::emplace` | NEW | EXACT_SOURCE |
| P13 | Per-primitive collision test in `SuccessorIterator::Next`: broad-phase primitive bbox vs `env+0x50` bounds; if overlap, sample every intermediate pose with `FastPolygon::Contains` | 0x0085123A..0x00851286 (bbox); 0x0085134A..0x008513A6 (non-turning); 0x00851414..0x008514A0 (turning) | M13-003 (partial) | EXACT_SOURCE |
| P14 | Turning primitive: each sampled pose uses its own heading bucket (`IntermediatePosition+0xC`); non-turning: all poses use `end_pose.theta` | 0x008512D0 `ldrb r1,[r4,#1]` / `cmp`; 0x00851428 `ldrb r1,[r1,#0xc]`; 0x008512E0 bucket = end_pose.theta | NEW | EXACT_SOURCE |
| P15 | Soft hit (polygon penalty < 1000): add `base + penalty*IntermediatePosition+0x10` to the successor g; penalty >= 1000 is a hard collision (reject) | 0x0085137E `vldr s0,[r7,#0x44]` / 0x0085138A `bge`; 0x0085138C..0x00851398; threshold 0x00851162 = 1000.0; base 0x008515B0=0.0 / 0x008515B4=1000.0 | NEW | EXACT_SOURCE |
| P16 | `ConvexPolygon::RadialExpand` moves each vertex to `v + d*(v-c)/|v-c|` (c = centroid, d = padding); negative d only warns | 0x008415D8 `ComputeCentroid`; 0x008415F2..0x0084160A; 0x00841610 `hypotf`; 0x00841622 `vdiv.f32 s0,s16,s0`; 0x0084163C..0x00841654 | M13-003 (partial) | EXACT_SOURCE |
| P17 | `ParseMotionPrims` count mismatch aborts and returns 0 | 0x0085223C `bne` -> 0x00852442 `printf` (0x8527D8) -> 0x0085244C `b 0x85274A`; 0x0085225A `bne` -> 0x0085244E `puts` -> 0x0085244C; 0x0085274A `movs r0,#0` | M13-001 (partial) | EXACT_SOURCE |
| P18 | `State::Import` reads "x" (short)->+0, "y" (short)->+2, "theta" (uchar)->+4; `State_c::Import` reads "x_mm"/"y_mm"/"theta_rads" (float)->+0/+4/+8 | 0x0084F920/0x0084F940/0x0084F962; 0x0084FDEE/0x0084FE10/0x0084FE34 | NEW | EXACT_SOURCE |
| P19 | `impl+0x108` pre-plan wait defaults to 0 (ctor) and is set by `LatticePlanner::SetArtificialPlannerDelay_ms` | 0x004FCCF6 `str.w r1,[r4,#0x108]`; 0x004FFFEC `str.w r1,[r2,#0x108]` | M13-018 (partial) | EXACT_SOURCE |
| P20 | `impl+0xF2` is a run/abort flag: ctor=1, StartPlanning=1, StopPlanning=0; DoPlanning/Replan abort when it reads 0 | 0x004FCCCA `strb.w r3,[r4,#0xf2]`; 0x004FF310 `strb.w r0,[sl,#0xf2]`; 0x004FD1C8 `strb.w r1,[r0,#0xf2]`; 0x005000BE `ldrb r0,[r6]` / 0x005000C0 `cbz`; 0x0085888C/0x00858890 | NEW | EXACT_SOURCE |
| P21 | DoPlanning's code is stored at `impl+0xF8`; direct callers ignore r0; `GetCompletePath` reads the status (0/1 -> no path, 2/3 -> append plan) | 0x0050009A, 0x00500218, 0x00500212, 0x00500590; 0x00500838 (worker); 0x004FF35E (StartPlanning); 0x004FFB64 status test | M13-018 (partial) | EXACT_SOURCE |
| P22 | StartPlanning's bool is stored at `impl+0xA1`; true forces a replan, false first checks the old plan (import with the 2/1 padding) | 0x004FEB7E `strb.w r6,[sl,#0xa1]`; 0x004FEBAC `ldrb` / 0x004FEBB2 `cbz`; 0x004FEC38 `movs r1,#1`; 0x004FEEFE `movs r1,#0` | M13-018 (partial) | EXACT_SOURCE |

---

## Q1 — primitive traversal cost (M13-004)

**Answer.** `MotionPrimitive::Create` computes

```
MotionPrimitive+4 (float) =
    extra_cost_factor  *  d8  *  ( |straight_length_mm|
                                   + |sweepRad| * (|radius_mm| + halfWheelBase_mm)
                                   + halfWheelBase_mm * |dtheta| )
```

* `extra_cost_factor` = `ActionType+0x00`; it multiplies the whole sum once, at the end
  (0x0085432E `ldr r2,[r4,#0x2c]` = actions begin; 0x00854330 `add.w r0,r1,r1,lsl#1`;
  0x00854360 `add.w r2,r2,r0,lsl#3` = &action[action_index]; 0x00854364 `vldr s0,[r2]`;
  0x00854368 `vmul.f32 s4,s4,s0`; 0x00854378 `vstr s4,[sb,#4]`).
* `d8` is the reciprocal of the action's speed:
  * forward action (`reverse_action == 0`): `d8 = env+0x78` = `invMaxVelocity` = `1/maxVelocity_mmps`
    (0x00854046 `cbz r7,#0x854078`; 0x00854078 `vldr d9,[r4,#0x68]`; 0x0085407C `vldr d8,[r4,#0x78]`).
  * reverse action: `d8 = 1.0 / env+0x70` = `1/maxReverseVelocity_mmps`
    (0x0085404C `vldr d9,[r4,#0x70]`; 0x00854050 `vdiv.f64 d8,d0,d9`).
* `env+0x60..+0x7F` is an embedded `Anki::Planning::RobotActionParams` (constructed at
  0x00851EDE `add.w r0,r4,#0x60`; 0x00851EE2 `blx RobotActionParams::RobotActionParams`).
  Its fields are doubles: `+0x00 halfWheelBase_mm`, `+0x08 maxVelocity_mmps`,
  `+0x10 maxReverseVelocity_mmps`, `+0x18 1/maxVelocity_mmps`.
  The constructor 0x0084EDE6 sets defaults **24.0** (`0x4038000000000000`, 0x0084EDF2),
  **60.0** (`0x404E000000000000`, 0x0084EDFC/0x0084EE10), **25.0** (`0x4039000000000000`, 0x0084EE08),
  and `1/60` (`0x3F91111111111111`, 0x0084EE04/0x0084EE18).
  `RobotActionParams::Import` (0x0084EF28) has **no callers** in the binary, and the shipped
  `cozmo_mprim.json` has no `halfWheelBase_mm`/`maxVelocity_mmps`/`maxReverseVelocity_mmps` keys,
  so the defaults stand: halfWheelBase = 24.0, maxVelocity = 60.0, maxReverseVelocity = 25.0.
* Straight term: 0x008540AA `vabs.f64 d0,d0`; 0x008540B4 `vmul.f64 d0,d8,d0`;
  0x008540B8 `vldr s2,[sb,#4]`; 0x008540C0 `vadd.f64 d0,d0,d1`; 0x008540C8 `vstr s0,[sb,#4]`.
* Arc term: 0x00854188 `bfc r1,#0x1f,#1` (clear sign of `radius_mm`'s high word);
  0x00854190 `bfc r7,#0x1f,#1` (clear sign of `sweepRad`);
  0x008541A4 `vadd.f64 d0,d1,d0` = radius + halfWheelBase;
  0x008541A8 `vmul.f64 d0,d2,d0` = sweep * that; 0x008541AC `vmul.f64 d0,d8,d0`;
  0x008541C4 `vstr s0,[sb,#4]`.
  (0x008541C0 recomputes `d8` afterwards but that value is not used again — the arc branch jumps
  to 0x0085431E, the extra-factor step; the recompute is dead in this path.)
* Turn term: `dtheta` is the signed `Radians::angularDistance(angle_definitions[+1], angle_definitions[end_pose.theta], dir)`
  (0x00854280 `ldrb r0,[sb,#1]`, 0x00854294 `ldrb r0,[sb,#0xc]`, 0x008542BC `blx angularDistance`);
  0x008542CE `vabs.f32 s2,s0`; 0x008542D6 `vmul.f64 d1,d2,d1` (d2 = halfWheelBase);
  0x008542DE `vmul.f64 d1,d8,d1`; 0x008542F2 `vstr s2,[sb,#4]`.
* The result is stored at **`MotionPrimitive+4`** (float). The initial zero is 0x0085408A.
* Guards: threshold `1e-6` at 0x008543E0 (`vldr d1,[pc,#0xbc]`); base < 1e-6 logs
  "ERROR: base action cost is %f for action %d '%s'" (format 0x00C29D1A) and the final < 1e-6 logs
  "ERROR: final action cost is %f (%f x) for action %d '%s'" (format 0x00C29D4C); either returns 0.
  On success 0x008543B2 `blx MotionPrimitive::CacheBoundingBox` and 0x008543B8 returns 1.
* The cost field is consumed as the successor edge cost: 0x00851516 `vldr s4,[r1,#4]` (r1 = the
  primitive), 0x0085151A `vadd.f32 s2,s2,s4` (parent g), 0x00851522 `vstr s0,[r5,#0x24]`.

**Primitive layout** (for the record): `+0` action_index (byte; `GetValueOptional<unsigned char>` output
= `sb` at 0x00853E04/0x00853E06; read at 0x00854036), `+1` heading/angle index (byte; `strb.w r6,[sb,#1]`
at 0x00853DEC from Create's 2nd argument; read at 0x00854280), `+4` cost (float), `+8` end_pose State
(short x +0x8, short y +0xA, uchar theta +0xC; see Q6), `+0x10/+0x14/+0x18` vector<IntermediatePosition>
begin/end/cap (emplace `this` = `sb+0x10` at 0x00853E68), `+0x1C..+0x28` cached bbox (used at
0x0085121E..0x0085123A), `+0x2C` `Path`, total stride 0x124 (0x00851154 `mov.w lr,#0x124`).

**UNKNOWN / caveats.** None for the formula. The intermediate-pose field at `IntermediatePosition+0x10`
is a reciprocal, `1.0 / (halfWheelBase*|dtheta|/maxVelocity + dist)` (0x00853F1E `vldr d1,[r0,#0x60]`,
0x00853F28 `vldr d2,[r0,#0x78]`, 0x00853F46 `vdiv.f32 s0,s20,s0`); it is not added to `MotionPrimitive+4`
and is only used by the soft-collision penalty (Q3).

## Q2 — A* heuristic and priority ordering (M13-018)

**Heuristic is computed on demand, not a precomputed grid.**
* `heur_internal(StateID)` 0x0085A7B8: for each goal entry in the vector at planner+0x24 (stride 0x10;
  the State_c starts at +4), take the per-goal cost from the vector at planner+0xA0 (0x0085A7FA
  `ldr.w r2,[r4,#0xa0]` + 0x0085A800 `add r2,r7`, `vldr s18,[r2]`), compute
  `GetDistanceBetween(goal.State_c, state)` (0x0085A808), multiply by `env+0x78` (= `1/maxVelocity_mmps`,
  0x0085A816 `vldr d1,[r0,#0x78]`), add the per-goal cost (0x0085A822 `vadd.f64 d0,d0,d1`) and keep the
  minimum (0x0085A82A..0x0085A834). Empty goal list -> `FLT_MAX` (0x0085A83E `vldr s16,[pc,#0x10]`
  = 0x7F7FFFFF).
* `heur(StateID)` 0x0085A780: memoizes in the hash map at planner+0xAC (0x0085A78E `find`,
  0x0085A798 `vldr s0,[r0,#0xc]`; miss -> `heur_internal`).
* `GetDistanceBetween(State_c const&, State const&)` 0x00850DCC = Euclidean distance between
  `(resolution_mm*state.x, resolution_mm*state.y)` and `(State_c.x_mm, State_c.y_mm)`:
  0x00850DDC/0x00850DE0 convert the shorts, 0x00850DF0/0x00850DF4 multiply by `env[0]` (resolution_mm),
  0x00850E18 `vsqrt.f32`.
* The per-goal cost `heurMap[i]` comes from `InitializeHeuristic` 0x008598DC: it resizes planner+0xA0
  to the goal count (0x0085991E), and for each goal calls `IsInSoftCollision` (0x00859980) and
  `ExpandCollisionStatesFromGoal` (0x0085998E) — a Dijkstra over `OpenList` through states that are in
  soft collision, returning the accumulated cost when free space is reached (0x00859BF0..0x00859FBD).
  Goals whose cost > 1000.0 are removed with "very high cost of _costOutsideHeurMap = %f, so removing
  goal %d" (0x008599E0 compare with 1000.0; 0x008599F2..0x00859A06 warning + swap-remove);
  the surviving cost is stored at `[heurMap + 4]` (0x008599CA `vstr s20,[r0,#4]`).
  `InitializeHeuristic` returns 1 iff any goal remains (0x008599E0..0x008599E6).

**Priority/queue ordering.** `OpenList` is a `std::multimap<float, StateID>` ordered by
`std::less<float>` (0x0084E4E8 references `__tree<... __map_value_compare<float,...,std::less<float>,true> ...>`);
`topF` returns the root key `[root+0x10]`, `pop` erases the root and returns its StateID `[root+0x14]`
(0x0084E500 `ldr r1,[r1]` / `ldr r1,[r1,#0x14]`? — key at +0x10: 0x0084E508 `ldr r0,[r0]` / `ldr r0,[r0,#0x10]`;
value at +0x14: 0x0084E50E `pop` reads `[r2,#0x14]` and erases the root). So the search always expands
the smallest `f`.

**Cost accumulation (`ExpandState` 0x0085A34C).**
* `s16 = [StateTable node + 0x14]` = the state's g (0x0085A3C6).
* `GetSuccessors(env, stateID, g, false)` (0x0085A3DE) fills a `SuccessorIterator`.
* For each successor: `s18 = [sp+0x5c]` = the successor's g (from `Next`).
* `StateTable::find` (0x0085A45C): if found and not closed and `s18 < [node+0x20]` (0x0085A47C `vldr s0,[fp,#0x20]`),
  remove the old open entry (0x0085A49E), compute `heur(successor)` (0x0085A48E), insert with
  `f = s18 + heur` (0x0085A4AC `vadd.f32 s0,s18,s0`; 0x0085A4BC `insert`) and rewrite the node
  (parent = current id, g = s18, closed = -1, 0x0085A4C0..0x0085A4DC).
* If not found, `heur(successor)` (0x0085A4E6), insert `f = s18 + heur` (0x0085A4FE) and
  `StateTable::emplace` (0x0085A522).
* At the end the expanded state's closed flag is set to the current closed id (0x0085A53E..0x0085A544).
So `f = g + h`; the stack's "Euclidean distance to nearest goal" is only the distance part — the
original divides by `maxVelocity_mmps` and adds the per-goal `ExpandCollisionStatesFromGoal` cost.

**UNKNOWN.** The exact edge weights used inside `ExpandCollisionStatesFromGoal` (0x00859BF0..0x00859FBD)
are not fully transcribed here; the function returns the accumulated cost at which the Dijkstra leaves
soft collision (0x00859FA0 return). If the manager needs the per-edge weight, that is a follow-up read.

## Q3 — per-primitive collision sampling (M13-003)

Collision testing for a primitive is in `Anki::Planning::SuccessorIterator::Next` 0x0085110C, not in
`ExpandState`. `ExpandState` only consumes the resulting g.

1. **Broad phase**: the primitive's cached bbox (`MotionPrimitive+0x1C/+0x20/+0x24/+0x28`, added to the
   start pose `s20/s22`) is tested against every AABB in the vector at `env+0x50` (16-byte entries,
   minX/maxX/minY/maxY): 0x0085123A `ldrd r1,r2,[fp,#0x50]`; 0x00851246..0x0085127C; no overlap ->
   no collision and the primitive cost is added with zero collision penalty (0x00851286 -> 0x008514DC).
2. **Turning vs non-turning** is decided by `end_pose.theta` (0x008512D0 `ldrb r1,[r4,#1]`, the
   primitive's `+1` angle index) vs the byte at `MotionPrimitive+0xC` (0x008512CC/0x008512D0):
   * **Non-turning** (`end_pose.theta == +1`): the polygons for the **end_pose.theta bucket** are used
     (0x008512E0 `add.w r0,r0,r0,lsl#1`; 0x008512E4 `ldr.w r7,[r1,r0,lsl#2]` where r1 = `env+0x44`),
     and **every intermediate pose** is tested (loop 0x0085134A `ldr.w r6,[lr]` (end),
     0x00851356 `add.w r4,r0,#0x10`, stride 0x14, 0x008513A4 `cmp r0,r6`), each at
     `(start + IntermediatePosition.x_mm, start + IntermediatePosition.y_mm)` (0x0085135C..0x00851376
     `FastPolygon::Contains`). There is no stride; all poses are sampled.
   * **Turning** (`end_pose.theta != +1`): the loop walks the intermediate poses from last to first
     (0x00851414 `subs r4,r6,#1`, 0x0085149E `cmp r6,#2`, 0x008514A0 `bge`), and each pose uses
     **its own bucket** at `IntermediatePosition+0xC` (0x00851428 `ldrb r1,[r1,#0xc]`), selecting
     polygons from `env+0x44 + bucket*0xC` (0x00851420/0x0085142E).
   The intermediate list is the sampled set; the end pose is its last element (e.g. asset angle 0
   action 0 intermediate_poses end at x_mm = 1.0 = end_pose x).
3. **Soft vs hard** for each `FastPolygon::Contains == 1`:
   * polygon penalty (`FastPolygon+0x44`) `< 1000.0` -> **soft**: add
     `base + penalty * IntermediatePosition+0x10` to the accumulated collision cost
     (non-turning 0x0085137E `vldr s0,[r7,#0x44]`; 0x0085138A `bge`; 0x0085138C `vldr s2,[r4]`;
     0x00851390 `vmul.f32 s0,s0,s2`; 0x00851394 `vadd.f32 s0,s19,s0`; 0x00851398 `vadd.f32 s24,s24,s0`;
     turning 0x0085146E..0x00851490).
   * penalty `>= 1000.0` -> **hard collision**: the primitive is rejected
     (non-turning 0x0085138A `bge #0x8513BE`; turning 0x0085147A `bge #0x85149A`).
   * `base` is 0.0 for a forward primitive and 1000.0 for a reverse primitive
     (`reverse_action` at `MotionPrimitive+0`; non-turning 0x008512FA..0x0085130A reads
     0x008515B0 = 0.0 / 0x008515B4 = 1000.0; turning 0x00851402..0x0085140C). The hard threshold
     1000.0 is loaded at 0x00851162 (`vldr s18,[pc,#0x374]`).
4. **Successor g** = `softCollisionCost + parentG + MotionPrimitive+4`:
   0x0085150A `vldr s2,[r5,#0x14]`; 0x00851516 `vldr s4,[r1,#4]`; 0x0085151A `vadd.f32 s2,s2,s4`;
   0x0085151E `vadd.f32 s0,s0,s2`; 0x00851522 `vstr s0,[r5,#0x24]`.

**The separate collision queries** (used by `InitializeHeuristic` and elsewhere, not by `Next`):
* `IsInCollision(State)` 0x008515BC converts the grid shorts to mm (`sxth`, `asrs`, multiply by
  `env[0]` at 0x008515D2/0x008515DC) and tail-calls `IsInCollision(State_c)` (0x008515EE `b.w 0x8D15CC`).
* `IsInCollision(State_c)` 0x008515F8 normalizes theta to `[0,2pi)` (0x00851614..0x00851658),
  buckets it as `round(theta * env+0x10) mod env+8` (0x0085165A..0x0085167A), iterates the polygons
  for that bucket and returns 1 only when a polygon contains the point **and** its penalty is
  `>= 1000.0` (0x008516D0 `vldr s0,[r0,#0x44]`; 0x008516D8 `vcmpe`; 0x008516DC `bge #0x8516E6`).
* `IsInSoftCollision` 0x00851708 returns 1 if **any** polygon in the bucket contains the point
  (0x0085175A `blx FastPolygon::Contains`; 0x0085175E `cbnz r0,#0x8517A2`).
* `GetCollisionPenalty` 0x008517B0 returns the first containing polygon's penalty
  (0x0085184E..0x00851860) or 0.0 when none (0x008517C4 `vldr s16,[pc,#0xa4]` = 0.0).

**Note.** `AddObstacleWithExpansion` 0x00855528 stores the caller's penalty at `pair+0x44`
(0x0085557E `vldr s0,[sp,#0xb0]`; 0x00855612 `str r2,[r0,#0x44]`); the `ImportBlockworldObstaclesIfNeeded`
caller passes **0.1**, so the shipped obstacles are soft. No shipped obstacle polygon with penalty
`>= 1000.0` was found on this path.

**UNKNOWN.** None for the sampling/penalty mechanism. The origin of any `>= 1000.0` polygon (if one
exists elsewhere) was not traced.

---

## Q4 — `ConvexPolygon::RadialExpand` 0x004FDF9E (body 0x00841580)

**Answer.** For each vertex `v` and padding `d >= 0`, the vertex becomes

```
v' = v + d * (v - c) / |v - c|
```

where `c` is the polygon's centroid, computed once before the loop. It is **not** a bisector or
`cos(half-angle)` expansion and there is **no** `max(0.3, ...)` clamp.

Instructions:
* 0x00841590 `vcmpe.f32 s16,#0`; 0x00841598 `bge #0x8415D2` — only a negative distance is rejected,
  with the warning "called expand with a negative distance." (0x008415A2..0x008415A8; strings at
  0x00841690/0x008416AC).
* 0x008415D8 `blx Polygon<2,float>::ComputeCentroid` -> centroid at `sp+4`.
* Per vertex (0x008415DC `ldrd r6,r8,[r5]`, loop 0x008415E8..0x0084165A):
  * 0x008415E8 `ldrd r0,r1,[r6]` copy vertex to `sp+0x18`;
  * 0x008415F2..0x0084160A subtract the centroid component-wise;
  * 0x00841610 `blx hypotf` = `|v-c|`;
  * 0x00841622 `vdiv.f32 s0,s16,s0` = `d / |v-c|`;
  * 0x00841626..0x00841638 multiply `(v-c)` by that;
  * 0x0084163C..0x00841654 add it back to the vertex in place.
* No clamp, no early-out other than the negative-distance guard; the vertex array is the polygon's
  own (`r6 = [r5]`).

The stack's guess (bisector, `cos(half-angle)`, `Math.Max(0.3, ...)`) is not what the source does.

## Q5 — `ParseMotionPrims` count validation (M13-001)

`xythetaEnvironment::ParseMotionPrims(Json const&, bool)` body **0x00852014** (the 0x004CE840 address is
the ARM import veneer).

* `angle_definitions.size() != num_angles`: 0x0085222E `ldr.w r1,[sl,#8]` (num_angles);
  0x00852232 `ldrd r0,r2,[sl,#0x38]`; 0x00852238 `cmp.w r1,r0,asr #2`; **0x0085223C `bne.w #0x852442`**.
  At 0x00852442 `asrs r2,r0,#2` (the count) and 0x00852448 `blx printf` with the format at
  **0x008527D8** = `"ERROR: numAngles is %u, but we read %lu angle definitions\n"`, then
  0x0085244C `b #0x85274A` -> **returns 0**.
* `angles.size() != num_angles`: 0x0085224E `blx Json::Value::size()`; 0x00852254 `ldr.w r0,[sl,#8]`;
  **0x0085225A `bne.w #0x85244E`**. At 0x0085244E `ldr r0,[pc,#0x3c8]` -> 0x00C97D20
  `"error: could not find key 'angles' in motion primitives"`, 0x00852454 `puts`, 0x00852458
  `PrintJsonCout`, 0x00852460 `b #0x85274A` -> **returns 0**. (The same string is used for a count
  mismatch, not only a missing key.)
* Both paths abort parsing (they jump to the shared epilogue 0x0085274A: `movs r0,#0`; `add sp,#0x19C`;
  `pop`). Success is 0x008524AC `blx PopulateReverseMotionPrims`; 0x008524B0 `movs r0,#1` -> **returns 1**.
* Note the index.tsv size for ParseMotionPrims (1192 bytes) does not cover the out-of-line
  error/cleanup block that starts around 0x008524B4 and runs to 0x0085274E; the `b 0x85274A` targets
  are inside that block.

## Q6 — `State::Import` / `State_c::Import` (M13-004)

* `Anki::Planning::State::Import(Json const&)` body **0x0084F8A4**:
  * key `"x"` (0x0084FB08) -> `GetValueOptional<short>` (0x0084F920) writes **`State+0`** (short).
  * key `"y"` (0x0084FB0C) -> `GetValueOptional<short>` (0x0084F940) writes **`State+2`** (short).
  * key `"theta"` (0x00C299A3) -> `GetValueOptional<unsigned char>` (0x0084F962) writes **`State+4`**
    (byte).
  * `GetValue<short>` = `Json::Value::asInt` truncated to short (0x008400BA `blx asInt`), so the JSON
    numbers are read as integers.
  * Units: `x`/`y` are **grid cells**; `GetDistanceBetween` multiplies them by `resolution_mm`
    (0x00850DDC `vldr s2,[r0]` = env[0]; 0x00850DF0/0x00850DF4), and the asset's `resolution_mm` is 10.0.
    `theta` is the **heading bucket index** (0..num_angles-1), used at 0x00854294 to index
    `angle_definitions`.
  * Error paths: null -> "State.Import.Null"; any key missing -> "State.Import.Invalid" +
    "could not parse state, dump follows" and return 0 (0x0084F99C..0x0084F9F6). Success returns 1.
* `Anki::Planning::State_c::Import(Json const&)` body **0x0084FD70**:
  * key `"x_mm"` (0x00C299A9) -> `GetValueOptional<float>` (0x0084FDEE) writes **`State_c+0`**.
  * key `"y_mm"` (0x00C299AE) -> `GetValueOptional<float>` (0x0084FE10) writes **`State_c+4`**.
  * key `"theta_rads"` (0x00C299B3) -> `GetValueOptional<float>` (0x0084FE34) writes **`State_c+8`**.
  * Units: mm and radians (float). Error path "State_c.Import.Invalid" + "could not parse State_c,
    dump follows"; success returns 1.
* In `MotionPrimitive::Create`: `end_pose` is read via `State::Import` into `MotionPrimitive+8`
  (0x00853E20 key, 0x00853E2C `add.w r0,sb,#8`, 0x00853E30 `blx State::Import`); each
  `intermediate_poses[i]` via `State_c::Import` into a 0x14-byte `IntermediatePosition`
  (0x00853E8A `mov r0,r6` (sp+0x40), 0x00853E8C `blx State_c::Import`; the struct stores x,y at
  +0/+4, the step distance at +8, the bucket at +0xC, the reciprocal at +0x10).

## Q7 — `StartPlanning` / `DoPlanning` callers and fields (M13-018)

**(a) `impl+0x108` (pre-plan wait in ms).**
* Constructor `FUN_004FCC44` (the LatticePlannerImpl ctor, called from `LatticePlanner::LatticePlanner`
  0x004FCBB0; object size 0x118 allocated at 0x004FCBF0) sets it to 0:
  **0x004FCCF6 `str.w r1,[r4,#0x108]`** with `r1 = 0`.
* Setter `Anki::Cozmo::LatticePlanner::SetArtificialPlannerDelay_ms(int)` **0x004FFFE0**:
  **0x004FFFEC `str.w r1,[r2,#0x108]`** where `r2 = [r0+0x130]` (the impl). It also logs
  "Adding %dms of artificial delay" (0x00500000).
* **UNKNOWN who calls the setter**: `SetArtificialPlannerDelay_ms` has no callers in the decompiled
  call graph and no direct `blx` found; it is presumably called through a game message or the public
  API. DoPlanning reads `impl+0x108` at 0x005000A2/0x005000C2.

**(b) abort flag `impl+0xF2`.**
* It is a **run/continue flag**, not an abort flag: 1 = keep planning, 0 = stop.
* Constructor 0x004FCC44 sets 1: **0x004FCCCA `strb.w r3,[r4,#0xf2]`** (`r3 = 1`).
* `StartPlanning` sets 1 before scheduling/planning: **0x004FF310 `strb.w r0,[sl,#0xf2]`** (`r0 = 1`).
* `LatticePlannerImpl::StopPlanning` 0x004FD1BA clears it when `impl+0xDC` (the worker thread ptr) is
  non-null and `impl+0xF3` (planning-in-progress) is non-zero: **0x004FD1C8 `strb.w r1,[r0,#0xf2]`**
  (`r1 = 0`; guards 0x004FD1BE/0x004FD1C4).
* `DoPlanning` 0x00500090 checks it during the pre-plan sleep: `r6 = fp+0xF2` (0x0050009E),
  **0x005000BE `ldrb r0,[r6]`** / **0x005000C0 `cbz r0,#0x5000EC`** (leave the sleep when 0), and passes
  it as the `bool volatile*` to `Replan` at **0x005000FE `mov r2,r6`** / 0x00500102 `blx Replan`.
* `ComputePath` 0x008586A0 stores the pointer at `planner+0x90` (**0x008586B4 `str.w r2,[r8,#0x90]`**)
  and aborts the A* loop when it reads 0: **0x00858886 `ldr r0,[r8,#0x90]`**; 0x0085888C `ldrb r0,[r0]`;
  0x00858890 `beq.w #0x858B34` (return 0).
* So `impl+0xF2` is the same flag object in both places: the planner context at impl+0xF2 and the
  pointer `planner+0x90`.

**(c) Who calls `DoPlanning` and what happens to 0 / 3 / 2.**
* Direct callers (both discard the return in r0):
  * `LatticePlannerImpl::worker` 0x005007E0 at **0x00500838 `blx DoPlanning`** (after setting
    `impl+0xF3 = 1` and `impl+0xF0 = 0`; then `impl+0xF3 = 0`).
  * `StartPlanning`'s synchronous branch at **0x004FF35E `blx DoPlanning`** (when `impl+0xF5`
    (`SetIsSynchronous`) is non-zero; else it signals the worker and returns).
* `DoPlanning` writes the code to `impl+0xF8`: status 1 at 0x0050009A, and at the end
  **0x00500216/0x00500218** (0), **0x00500212** (3), **0x0050058E/0x00500590** (2); the same value is
  in r0.
* `LatticePlannerImpl::CheckPlanningStatus` 0x004FD19C returns `impl+0xF8`; the public
  `LatticePlanner::CheckPlanningStatus` 0x004FD192 returns `[impl+0xF8]`.
* The only in-engine consumer of the status is `LatticePlannerImpl::GetCompletePath` 0x004FFB64:
  `status == 0` -> return 0; `status == 1` -> return 0 (still planning); **anything else (2 or 3)** ->
  `*param_3 = impl+0x100` (chosen goal id), `FindClosestPlanSegmentToPose`, and if the robot is within
  20.0 mm of the plan, append the plan (`AppendToPath`) and return 1; otherwise clear the plan, set
  `impl+0xF8 = 0` and return 0 (0x004FFB64..0x004FFC15; the tests are at 0x004FFB64 `ldr [this+0xf8]`
  / `cbz`, and 0x004FFB6A/0x004FFB6C `cmp`/`bne`).
* **There is no special handling of code 3** in `GetCompletePath`; 3 and 2 take the same branch.
  `LatticePlanner::GetCompletePath_Internal` (0x004FFB50, 0x004FFF14) only forwards and stores the
  goal id (`this+0x2A`); `IPathPlanner::GetCompletePath` 0x00508AAC returns 1 iff the internal call
  returns 1. No engine code distinguishes 3 from 2.
* For completeness, `StartPlanning` itself returns (in r4) 0 when it only signalled the worker
  (0x004FF3C8 `movs r4,#0`), 1 when it planned synchronously (0x004FF49E `movs r4,#1`), and 2 when the
  existing plan was found safe (0x004FF42E `movs r4,#2`); the epilogue returns r4 at
  0x004FF400/0x004FF402 (`moveq r0,r4`). `ComputeNewPathIfNeeded` 0x004FEB00 forwards that value (or 2
  if the mutex try-lock fails, 0x004FEB30 `movs r4,#2`).

**(d) `StartPlanning`'s bool argument (`impl+0xA1`).**
* Stored at **0x004FEB7E `strb.w r6,[sl,#0xa1]`** (`r6 = r2`, the bool).
* Passers:
  * `LatticePlanner::ComputeNewPathIfNeeded(Pose3d const&, bool)` **0x004FEB00**: saves its own bool in
    r4 (0x004FEB04 `mov r4,r2`) and calls StartPlanning at **0x004FEB22 `blx StartPlanning`** with
    `r2 = r4`. So the bool is the caller's.
  * `LatticePlanner::ComputePathHelper` 0x004FD1D0 calls the `LatticePlanner` vtable slot +0xC with a
    hard-coded `true`: **0x004FD326 `ldr r0,[r6]`**; **0x004FD328 `ldr r3,[r0,#0xc]`**;
    **0x004FD32E `movs r2,#1`**; **0x004FD330 `blx r3`**. The vtable relocation at 0x0101F194
    (`_ZTVN4Anki5Cozmo14LatticePlannerE` + 0xC) is `Anki::Cozmo::LatticePlanner::ComputeNewPathIfNeeded(Pose3d const&, bool)`,
    so the bool is **1**.
* Meaning (from StartPlanning's own branches):
  * `impl+0xA1 != 0` (true): 0x004FEBAC `ldrb r0,[sl,#0xa1]` / 0x004FEBB2 `cbz r0,#0x4FEC2E`; true
    skips the old-plan check and goes to the replan path. In that path
    `ImportBlockworldObstaclesIfNeeded(impl, false, BLOCK_BOUNDING_QUAD)` is called
    (**0x004FEEFE `movs r1,#0`**; 0x004FEF02), i.e. the 7.0/6.0 padding pair, then StartIsValid /
    GoalsAreValid / PrepareForPlanning and DoPlanning.
  * `impl+0xA1 == 0` (false): first calls `ImportBlockworldObstaclesIfNeeded(impl, true,
    REPLAN_BLOCK_BOUNDING_QUAD)` (**0x004FEC38 `movs r1,#1`**; 0x004FEC3A), i.e. the 2.0/1.0 padding
    pair, then `FindClosestPlanSegmentToPose` (0x004FEC50) and only replans when the robot is >= 20.0
    from the plan or `PlanIsSafe` fails (0x004FF424 `blx PlanIsSafe`; 0x004FF42A `beq` to the replan
    path; 0x004FF42E `movs r4,#2` when safe).
  So the bool means **force replan unconditionally** (true) vs **reuse the old plan if it is still safe**
  (false). The two import calls also select the padding pair (see M13-003).

---

## Contradictions of existing records

1. **M13-004, `action_index` offset.** The record says "`action_index` → +1 (0x00853DE8/0x00853E06)".
   The instructions say `action_index` is read by `GetValueOptional<unsigned char>(fp, "action_index", sb)`
   with the output pointer `r2 = sb` (**0x00853E04 `mov r2,sb`**; 0x00853E06), i.e. **`MotionPrimitive+0`**;
   and `MotionPrimitive+1` is the **heading/angle index** passed as `Create`'s second argument
   (**0x00853DEC `strb.w r6,[sb,#1]`**, `r6 = r2` from 0x00853DE4). This matters because the two bytes
   index different tables (actions vs angle_definitions) at 0x00854036 and 0x00854280.
2. **M13-019 ("`xythetaEnvironment::Init` hard-codes 16 headings")**. The evidence cites
   `ReadMotionPrimitives` and `0x008528AE`, which are in the `Init(char const*)` overload at
   **0x008528A8**. That overload has **no callers** (decomp header; and PLT 0x004A64F0 resolves to the
   other overload 0x00851F9E, per 004a/004a64f0.c "thunk to ... @ 00851f9e"). The LatticePlannerImpl
   constructor calls `xythetaEnvironment::Init(Json const&)` 0x00851F9E (0x004FCC44), which calls
   `ClearObstacles` + `ParseMotionPrims(env, json, false)` and does **not** overwrite `env+8`.
   Therefore the production planner uses the JSON `num_angles` (the asset also sets 16), not a
   hard-coded 16. The record's mechanism is wrong for the production path; the value 16 happens to be
   the same.

## Records whose evidence is too weak / partial for what they claim

* **M13-004** — the record is titled "schema and its arcs/turn costs" but its evidence has the schema
  only; the actual cost formula (P1..P7 above), the `d8` speed reciprocals, the `extra_cost_factor`
  multiply, the `1e-6` guards and the `+4` field are absent, and the `action_index` offset is wrong.
* **M13-003** — the record's obstacle-expansion half is supported, but the per-primitive collision
  sampling (SuccessorIterator::Next, the turning/non-turning bucket split, the intermediate-pose walk,
  the soft/hard 1000.0 threshold and the `base + penalty*reciprocal` penalty) is not in the record and
  is where the search actually spends its cost. The record's claim that `AddObstacleWithExpansion`
  stores a constant 0.1 penalty is correct; the consequence (all shipped obstacles are soft) is not
  stated.
* **M13-001** — the loader/parser entry is supported; the count-mismatch abort/return-0 path and the
  exact error strings (0x008527D8, 0x00C97D20) are not in the record.
* **M13-018** — the entry/worker and the 0/3/2 status are supported, but the record does not say that
  both direct callers discard r0, that `impl+0x108` is the ctor-defaulted / setter field
  (`SetArtificialPlannerDelay_ms`), that `impl+0xF2` is a run flag (not an abort flag), or that the
  engine's only status consumer (`GetCompletePath`) does not distinguish 3 from 2.

## Open questions for the manager

1. **Code 3.** No engine consumer distinguishes `DoPlanning`'s 3 (empty plan) from 2. If a hardware
   test or the stack expects code 3 to change behaviour, the consumer is outside this binary (game /
   C#) or was not found. Decide whether to keep the record's "3 = empty plan" as an engine-side fact
   only, or to trace the game-side consumer.
2. **M13-019.** The 16-heading override is on an uncalled overload. Decide whether to rewrite the
   record to say "the JSON `num_angles` is used (asset 16)" and drop the hard-coded-16 claim, or to
   keep the uncalled-overload fact as a separate NEW record.
3. **M13-004 offset.** The `action_index`/heading-index offsets should be corrected and the cost
   formula added; this is a behaviour-changing part of the path that the record currently omits.
4. **`SetArtificialPlannerDelay_ms` caller.** Not found in the engine; if the manager needs the
   artificial-delay source, it is a game-side call.
5. **`ExpandCollisionStatesFromGoal` edge weights.** The heuristic's per-goal offset is produced there;
   only its return (the cost when leaving soft collision) was read. If the exact weight matters, that
   is a follow-up read (0x00859BF0..0x00859FBD).

*Read-only extraction. Nothing outside `.scratch/B-M13/` was changed.*


## Appendix E: gap pass 3 extraction report (2026-09-28)

# B-M13 extractor actions  -  answers to the build pass MISSING points

Scope: M13-navigation (records M13-002, M13-010, M13-013, M13-014, M13-016, M13-017).
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, file VAs, Thumb (capstone CS_MODE_THUMB).
The Ghidra decompilation was used as a navigation aid only; every instruction below was re-read
from the `.so`. Read-only pass: nothing outside `.scratch/B-M13/` was written.

Legend: `+0xNN` = byte/word offset on the object named in the sentence. All addresses are file VAs.

---

## Q1. `FlipBlockAction` (M13-002)  -  the role of each constructor offset

`FlipBlockAction::FlipBlockAction` 0x0055EC80 stores the constants (0x0055ECF2..0x0055ED12).
Two members read them: `Init` 0x0055EDC8 and `CheckIfDone` 0x0055F074; the destructor 0x0055ED54
reads +0x13C only.

| offset | value | role (what reads it and how) | citation |
|---|---|---|---|
| +0x12C | 150.0 (0x43160000) | **drive speed (mm/s)**. `Init` loads it into `r3` and passes it as the 3rd arg (speed) of `DriveStraightAction`. In `DriveStraightAction::DriveStraightAction(Robot&,float,float,bool)` 0x00547278 the `r3` arg is stored at `DriveStraightAction+0x7C`, the speed field (0x005472A4; the negative-speed warning at 0x005472BA/0x005472BE names it "Speed"). | ctor 0x0055ECF6/0x0055ECFA; Init 0x0055EF18 `ldr.w r3,[r6,#0x12c]`, 0x0055EF2C `blx DriveStraightAction`; speed field 0x005472A4 |
| +0x130 | 20.0 (0x41A00000) | **drive-past distance (mm)** added to the distance from the robot to the object. `Init` computes the norm of the object's robot-relative translation (`vsqrt.f32` at 0x0055EEFA), loads +0x130 at 0x0055EF14, adds (`vadd.f32` 0x0055EF1C) and passes the sum as the `r2` arg (distance) of `DriveStraightAction`. The `r2` arg is stored at `DriveStraightAction+0x78` (0x005470F0+0x? -> `*(float*)(this+0x78)=in_r2`), the distance field. | ctor 0x0055ECF2/0x0055ECFA; Init 0x0055EF14 `vldr s0,[r6,#0x130]`, 0x0055EF1C `vadd.f32 s0,s2,s0`, 0x0055EF20 `vmov r2,s0` |
| +0x134 | 45.0 (0x42340000) | **approach lift height (mm)**. `Init` loads it into `r2` and passes it as the height of `MoveLiftToHeightAction(robot, 45.0, 5.0, 0)` (added to the compound **before** the drive). | ctor 0x0055ECFE/0x0055ED0E; Init 0x0055EF3A `ldr.w r2,[r6,#0x134]`, 0x0055EF3E/0x0055EF42 `r3=5.0`, 0x0055EF48 `str r6,[sp]` (0), 0x0055EF4A `blx MoveLiftToHeightAction` |
| +0x138 | 40.0 (0x42200000) | **lift trigger distance (mm)**. `CheckIfDone` computes the object distance and compares it with +0x138 (`vcmpe`/`bpl`); when the object is **closer than 40.0** and +0x13C is still -1, it queues a `MoveLiftToHeightAction(robot, Preset=2, 5.0)` (ctor 0x4A9A48), sets `action+0x56 = 1`, stores the new action's id (+0x60) into +0x13C and `ActionList::QueueAction(position 5, ...)`. | ctor 0x0055ED02/0x0055ED0E; CheckIfDone 0x0055F124 `vldr s0,[r4,#0x138]`, 0x0055F128 `vcmpe.f32 s2,s0`, 0x0055F130 `bpl`, 0x0055F14E `blx MoveLiftToHeightAction`, 0x0055F15C `str.w r1,[r4,#0x13c]`, 0x0055F16A `blx ActionList::QueueAction` |
| +0x13C | -1 (0xFFFFFFFF) | **queued-lift-action id / state**. -1 = no lift queued. Written by `CheckIfDone` with the queued `MoveLiftToHeightAction`'s id (+0x60). The destructor 0x0055ED54 cancels that id on the robot's `ActionList` (`robot+0x250`) when it is not -1. | ctor 0x0055ED06/0x0055ED0E; CheckIfDone 0x0055F132/0x0055F138 (`adds r0,#1` / `bne`), 0x0055F15C; dtor 0x0055ED6C `ldr.w r1,[r4,#0x13c]`, 0x0055ED70 `adds r0,r1,#1`, 0x0055ED72 `beq`, 0x0055ED7A `blx ActionList::Cancel` |
| +0x140 | 1 (byte) | **`shouldCheckPreActionPose` flag**. `Init` reads it (`ldrb.w r0,[r6,#0x140]`) and puts it into the `PreActionPoseInput` byte at `sp+0x64` passed to `IDockAction::GetPreActionPoses` (with ActionType 5 at `sp+0x60`). `SetShouldCheckPreActionPose(bool)` 0x0055EDC2 writes it (`strb.w r1,[r0,#0x140]`). | ctor 0x0055ED10/0x0055ED12; Init 0x0055EE10 `ldrb.w r0,[r6,#0x140]`, 0x0055EE14 `movs r1,#5`, 0x0055EE16 `strd r7,r1,[sp,#0x5c]`, 0x0055EE1C `strb.w r0,[sp,#0x64]`, 0x0055EE5E `blx IDockAction::GetPreActionPoses`; setter 0x0055EDC2 |

**No offset is unread.** Every one of +0x12C/+0x130/+0x134/+0x138/+0x13C/+0x140 is read in the
shipped code (sources: 0x0055EDC8, 0x0055F074, 0x0055ED54, 0x0055EDC2).
The job's lead (45 = approach lift, 40 = trigger, 150/20 = speed/past) is confirmed.

---

## Q2. `AlignWithObjectAction` (M13-016)  -  the pre-action-type enum and +0xBB

### Q2a. The numeric returned by `GetPreActionTypeFromAlignmentType` 0x005532B8

- Table 0x00553360: [0x00553360]=1, [0x00553364]=0, [0x00553368]=1, [0x0055336C]=1
  (`0x005532C0 adr r1,#0x9c` -> 0x00553360; 0x005532C4 `ldr.w r0,[r1,r0,lsl#2]`). So
  alignment type 0 -> 1, 1 -> 0, 2 -> 1, 3 -> 1; invalid (r0 >= 4, 0x005532BC/0x005532BE) -> 1
  (0x00553320 `movs r0,#1`).
- The result is stored at `AlignWithObjectAction+0xFC` (ctor 0x0055342A `str.w r0,[r4,#0xfc]`).
- **The enum it indexes is `Anki::Cozmo::PreActionPose::ActionType`**, not a distinct
  "PreActionType". Evidence: `DriveToAlignWithObjectAction::DriveToAlignWithObjectAction`
  0x0055C3A0 takes the result of `GetPreActionTypeFromAlignmentType` (0x0055C3A0+0x? line 28)
  and passes it to `IDriveToInteractWithObject::IDriveToInteractWithObject`, whose mangled symbol
  is `_ZN4Anki5Cozmo26IDriveToInteractWithObjectC1ERNS0_5RobotERKNS_8ObjectIDERKNS0_13PreActionPose10ActionTypeEfbfbNS_7RadiansEb`
  (4th arg = `PreActionPose::ActionType const&`).
- **Native enum values** (`PreActionPose::ActionType`): `Block::GeneratePreActionPoses`
  0x004E5808 dispatches on it with `cmp r5,#5` / `bhi` (0x004E5958/0x004E595A) and
  `tbh [pc,r5,lsl#1]` (0x004E595E, table 0x004E5962 = halfwords `06 00 75 00 c7 00 20 02 15 01 8b 01`):
  0 -> 0x004E596E, 1 -> 0x004E5A4C, 2 -> 0x004E5AF0, 3 -> 0x004E5DA2, 4 -> 0x004E5B8C, 5 -> 0x004E5C78.
  `PreActionPose::GetVisualizeColor` 0x0050D7D0 also maps values 0,1,2,3,5 to colors and
  accepts up to 6 (`cmp r1,#6` / `movhi r0,#0`), so the enum has entries 0..6.
- **Enum names: UNKNOWN from the shipped binary.** There is no `EnumToString(PreActionPose::ActionType)`
  symbol and no name table; `PreActionPose::GetVisualizeColor` maps to colors only. The names in
  `re-analysis/MANIPULATION.md` (Docking 0, PlaceRelative 1, PlaceOnGround 2, Entry 3, Rolling 4,
  Flipping 5, None 6) are there explicitly marked **INFERRED**, and the only nearby string
  "PlaceOnGround" (file 0x00E0704) is part of `IDockAction::SetPlaceOnGround(bool)`, not an enum name.
  Treat the names as authority-6/inferred; the values are native.
- Consequence for M13-016: alignment type **1 -> pre-action type 0**, alignment types 0, 2, 3 and
  invalid -> pre-action type **1**.

### Q2b. The flag at `action+0xBB = 2`

- `+0xBB` is `IDockAction`'s **`DockingMethod`** field. It is set by
  `DriveToPickupObjectAction::SetDockingMethod(DockingMethod)` 0x0055C584:
  0x0055C59E `ldr.w r1,[r5,#0xf8]`; 0x0055C5A2 `strb.w r4,[r1,#0xbb]` (writes the argument to
  `this->[+0xF8]+0xBB`). The IDockAction ctor 0x005502D8 leaves it at its default 0 (the
  `stm r0!,{r2,r3,r6}` at 0x005503AA stores `r3=0x100` at +0xB8, i.e. +0xBB = 0x00).
- **Values** (`Anki::Cozmo::DockingMethod`, `DockingMethodFromString` 0x007C013C):
  - `BLIND_DOCKING` = 0 (0x007C0160 `movs r6,#0`; string 0x00C204FF)
  - `TRACKER_DOCKING` = 1 (0x007C0190 `movs r0,#1`; string 0x00C2050D)
  - `HYBRID_DOCKING` = 2 (0x007C01AA `movs r0,#2`; string 0x00C2051D)
  - `EVEN_BLINDER_DOCKING` = 3 (0x007C01C4 `movs r1,#3`; string 0x00C2052C)
- **Use**: `IDockAction::CheckIfDone` 0x005521AC passes it as the 14th argument
  (`DockingMethod`) of `DockingComponent::DockWithObject(...)`: 0x00552288
  `ldrb.w r5,[r4,#0xbb]`; 0x005522AE `blx DockingComponent::DockWithObject`.
  So for `AlignWithObjectAction` alignment type 1, +0xBB = 2 means the dock runs with
  **HYBRID_DOCKING**; every other alignment type leaves it at the ctor default 0 (BLIND_DOCKING).

---

## Q3. `WorkoutComponent::ShouldPlayEightiesMusic` 0x00573E30 (M13-010)

Whole body 0x00573E30..0x00573E7F, disassembled:

- 0x00573E34 `ldrb r0,[r4,#0x11]`; 0x00573E36 `cbz r0,#0x573e3c` -> if **+0x11 == 0** (not yet
  evaluated) it computes; else 0x00573E38 `ldrb r5,[r4,#0x10]` and returns that cached value.
- Compute:
  - 0x00573E3C `ldr r1,[r4,#0xc]` (the current workout pointer), 0x00573E3E `ldr r0,[r4,#0x14]`
    (the robot), 0x00573E40 `adds r1,#0x18` -> `MoodScorer` is at **workout+0x18**.
  - 0x00573E42 `blx WorkoutConfig::MoodScoreHelper(robot, workout+0x18)`.
    `MoodScoreHelper` 0x00573B70: if the scorer's entry vector is non-empty (`ldrd r2,r3,[r1]`;
    `cmp r2,r3`; `itt eq; moveq r0,#0; bxeq lr`), it calls `MoodScorer::EvaluateEmotionScore(scorer,
    robot->moodManager at robot+0x440)` (0x00573B86), `roundf`s the float (0x00573B8A) and returns
    it as an **unsigned int** (`vcvt.u32.f32` 0x00573B92, so a negative/zero score becomes 0);
    an empty scorer returns 0.
    `EvaluateEmotionScore` 0x0067C9B8 sums, for each scorer entry with a flag at entry+0x10, the
    `GraphEvaluator2d::EvaluateY` of the entry's graph over `Emotion::GetHistoryValueTicksAgo(..., 0x3c)`
    (60 ticks ago), and returns 0 if any |value| < 1e-5, else the mean of the entries.
    So the "score" is **round(mean emotion-graph value of the current workout's MoodScorer)**, used
    only as a non-zero gate.
  - 0x00573E48 `cbz r0,#0x573e70` -> if the score is 0, skip the roll (result stays 0).
  - 0x00573E4A `ldr r0,[r4,#0x14]`; 0x00573E4C `blx Robot::GetRNG`; 0x00573E50
    `vmov.f64 d0,#1.0` (r2/r3); 0x00573E58 `blx RandomGenerator::RandDbl(1.0)`.
  - 0x00573E5C `vldr d0,[pc,#0x20]` -> 0x00573E80 = bytes `9a 99 99 99 99 99 b9 3f` =
    **double 0.1**; 0x00573E64 `vcmpe.f64 d1,d0`; 0x00573E6C `it mi`; 0x00573E6E `movmi r5,#1`.
    So the result is true iff `RandDbl(1.0) < 0.1` **and** the mood score was non-zero.
- Cache: 0x00573E70 `movs r0,#1`; 0x00573E72 `strb r0,[r4,#0x11]` -> **+0x11 = 1 (the
  "already evaluated" flag)**; 0x00573E74 `strb r5,[r4,#0x10]` -> **+0x10 = the cached boolean
  answer**. The `else` branch reads +0x10, so the answer is cached at +0x10 and +0x11 only marks it done.

Summary: score = `round(EvaluateEmotionScore(currentWorkout->MoodScorer at workout+0x18, robot's
MoodManager))`, roll = `RandDbl(1.0)`, threshold = `0.1`, cache = flag at +0x11 and answer at +0x10.

---

## Q4. `DriveOffChargerContactsAction` +0x44 = 7 (M13-013)

- The field is the **`RobotActionType`** of the action runner, defined on **`IActionRunner`**.
  `IActionRunner::IActionRunner(Robot&, string, RobotActionType, unsigned char)` 0x0053FDB0
  stores its 3rd explicit argument (r3) at +0x44: 0x0053FDCC `str r3,[r5,#0x44]`. The same
  argument drives the completed-union `switch(param_4)` (0x0053FE?..) in that ctor.
- The class chain is `DriveOffChargerContactsAction` -> `DriveStraightAction` -> `IAction` ->
  `IActionRunner`. `DriveStraightAction`'s own `IAction::IAction` call passes RobotActionType **8**
  (0x00547120 `movs r3,#8`); `DriveOffChargerContactsAction`'s ctor then overwrites +0x44 with 7
  (0x00558276 `movs r1,#7`; 0x00558278 `str r1,[r4,#0x44]`).
- **7 = `RobotActionType::DRIVE_OFF_CHARGER_CONTACTS`**. `RobotActionTypeFromString` 0x0075A448
  builds the string->value map; the string "DRIVE_OFF_CHARGER_CONTACTS" (0x00C1604E) is paired with
  value 7 (0x0075A59E `movs r0,#7`, stored at the pair's value slot 0x005? -> see 0x0075A5A6
  `strd r0,r7,[sp,#0x9c]`), and the neighbouring known entries confirm the layout: "DRIVE_STRAIGHT"
  = 8 (matches DriveStraightAction's `movs r3,#8`), "FLIP_BLOCK" = 15, "MOUNT_CHARGER" = 17.

---

## Q5. `DriveOffChargerContactsAction` SDK `SetTracksToLock(0)` (M13-013)

- Constructor 0x00558228: 0x0055827A `ldr r0,[r0]` (context); 0x0055827C `blx
  CozmoContext::IsInSdkMode()`; 0x00558280 `cmp r0,#1`; 0x00558282 `bne 0x55828C`; 0x00558284
  `mov r0,r4`; 0x00558286 `movs r1,#0`; 0x00558288 `blx IActionRunner::SetTracksToLock(unsigned char)`.
  **The argument is 0**, and the call is in the **constructor**, only when `IsInSdkMode() == 1`.
- `IActionRunner::SetTracksToLock` 0x00540918:
  - 0x0054091C `ldr r2,[r0,#0x18]`; 0x0054091E/0x00540920 `r3 = 0x2000001`; 0x00540924 `cmp`;
    0x00540926 `bne 0x54092E`; 0x00540928 `strb.w r1,[r0,#0x54]` -> if the action state (+0x18) is
    **0x2000001 (not started)** it writes the argument to **`IActionRunner+0x54` (the
    `tracksToLock` byte)**; 0x0054092E..0x0054093C logs `Util::sWarningF("IActionRunner.SetTracksToLock",
    "Trying to set tracks to lock while running")` and does nothing.
- It is **not a robot-message send.** `+0x54` is a pre-run configuration byte, consumed by
  `IActionRunner::Update` 0x00540370: 0x00540438 `ldrb.w sb,[r4,#0x54]`; 0x00540440
  `blx MovementComponent::AreAnyTracksLocked`; if the required tracks are locked it warns
  "not running because required tracks are locked". The actual lock/unlock is applied to the
  in-process `MovementComponent` (`robot+0x254`) through `MovementComponent::UnlockTracks`
  (FUN_004F0EB2) on interrupt/destruction. So `SetTracksToLock(0)` = **clear the required-track
  lock mask to 0 (lock nothing)** for this action; no robot message.

---

## Q6. `BehaviorDriveOffCharger` `robot+0x355` (M13-017)

- `robot+0x355` is the robot's **`OffTreadsState`** (current state), not "wait-for-on-treads".
  Evidence: `Robot::CheckAndUpdateTreadsState(RobotState const&)` 0x00511E00 sets it and emits the
  state message; the enum is `Anki::Cozmo::OffTreadsState`
  (`EnumToString(OffTreadsState)` 0x0078DF40, `OffTreadsStateFromString` 0x0078DF58).
- Enum values (`OffTreadsStateFromString` 0x0078DF58, pairs string -> value):
  **OnTreads = 0, InAir = 1, OnBack = 2, OnLeftSide = 3, OnRightSide = 4, OnFace = 5, Falling = 6**
  (0x0078DF58 builds "OnTreads".."Falling" with value bytes 0..6 at `local_74`, `local_64`, `local_54`,
  `local_44`, `local_34`, `local_24`, `local_14`).
- **Writer**: only `Robot::CheckAndUpdateTreadsState` 0x00511E00. It copies the pending state
  `robot+0x356` into `robot+0x355` at 0x00512088 `ldrb.w r0,[sb,#0x356]` / 0x0051208E
  `strb.w r0,[sb,#0x355]`, then constructs and sends `RobotOffTreadsStateChanged` (0x00512098
  `blx ExternalInterface::MessageEngineToGame::MessageEngineToGame(RobotOffTreadsStateChanged&&)`).
  A whole-decompilation scan finds no other store to `+0x355`.
- Behaviour reads:
  - `BehaviorDriveOffCharger::InitInternal` 0x005C0B18: 0x005C0B54 `ldrb.w r0,[r5,#0x355]`;
    `if (== 0)` (OnTreads) -> `TransitionToDrivingForward`, else logs "WaitForOnTreads" (0x005C0B5A..)
    and does not transition.
  - `BehaviorDriveOffCharger::UpdateInternal` 0x005C0DA8: 0x005C0DB0 `ldrb.w r0,[r5,#0x34a]`
    (on-contacts); if off-contacts -> record time, return 2; else 0x005C0DB6 `ldrb.w r0,[r5,#0x355]`;
    `if (== 0)` -> `TransitionToDrivingForward`, else `StopActing(false,false)` and log
    "WaitForOnTreads".
  So the wait condition is `OffTreadsState == 0 (OnTreads)`; the stack's `OffTreadsState` field is the
  right target, but the value compared is **0 = OnTreads**, and it is a separate field from
  `robot+0x34A` (on-contacts).

---

## Q7. `BehaviorKnockOverCubes::TransitionToPlayingReaction` 0x005C3908 (M13-014)

### Q7a. The store into the object chain  -  **the record's chain is wrong**

The job (and the approved inventory M13-014) states the store is
`robot->[+0x264]->[+0x18]->[+0xC] = 1`. The instructions at the cited addresses are:

```
0x005C3946  ldr      r0, [r5, #0x34]      ; r5 = robot (param_1); r0 = robot->[+0x34]
0x005C3948  movs     r1, #1
0x005C394A  ldr.w    r0, [r0, #0x94]      ; r0 = robot->[+0x34]->[+0x94]
0x005C394E  strb     r1, [r0, #0xc]       ; [robot->[+0x34]->[+0x94]]+0xC = 1
```

The decompilation agrees (`*(undefined1 *)(*(int *)(*(int *)(param_1 + 0x34) + 0x94) + 0xc) = 1;`).
The chain is **`robot+0x34` -> `+0x94` -> `+0xC`**, not `robot+0x264` -> `+0x18` -> `+0xC`.

Identity:
- `robot+0x34` = the robot's `BlockWorld*` (same base used by `BlockWorld::GetLocatedObjectByIdHelper`
  throughout, e.g. FlipBlockAction 0x0055EDE4 `ldr r0,[r0,#0x34]`).
- `BlockWorld+0x94` = the `BlockConfigurationManager*` (`BlockWorld::OnObjectPoseChanged` 0x00624808
  line 76 calls `(*(BlockConfigurationManager **)(this + 0x94), ...)`).
- `BlockConfigurationManager+0xC` = a byte flag initialised 0 by the ctor 0x00616B58 (0x00616B76
  `strb r0,[r4,#0xc]`) and read by `BlockConfigurationManager::Update` 0x00616D7C (0x00616D84
  `ldrb r0,[r4,#0xc]`; 0x00616D86..): when it is 0 and no object moved past threshold, Update
  returns early; when it is 1, Update runs `UpdateAllBlockConfigs` unconditionally, then clears it
  to 0 (decomp line 33). So setting it to 1 is **"force the block-configuration manager to recompute
  all block configurations (stacks/pyramids) on the next Update"**  -  the dirty/force-update flag.
  The same store appears in `BlockWorld::UpdateObjectOrigins` 0x00620534 (0x00620534 line 186) and
  `CarryingComponent::SetObjectAsAttachedToLift` 0x00632CC4 (line 341), consistent with
  "configuration changed, recompute".

This is a real contradiction of M13-014's evidence wording (and of the implementer's MISSING item 10),
not a behaviour change to the rest of the path.

### Q7b. Success/failure trigger selection by the tipped-object count at +0x14C  -  **confirmed**

```
0x005C3950  ldr.w    r0, [r4, #0x14c]     ; r4 = this; r0 = size/count of the tipped-object set
0x005C3954  cbz      r0, #0x5C396E        ; 0 -> failure path
0x005C3956  mov      r0, r4               ; success path:
0x005C3958  movs     r1, #0xd
0x005C395A  movs     r2, #1
0x005C395C  blx      IBehavior::BehaviorObjectiveAchieved(0xD, true)
0x005C3960  mov      r0, r4
0x005C3962  movs     r1, #0
0x005C3964  blx      IBehavior::NeedActionCompleted(0)
0x005C3968  add.w    r0, r4, #0x15c       ; success trigger
0x005C396C  b        #0x5C3972
0x005C396E  add.w    r0, r4, #0x160       ; failure trigger
0x005C3972  ldrb.w   r1, [r4, #0xd9]      ; then the streamline gate (Q8)
```

So the tipped-object set size at `+0x14C` (the `std::set` at +0x144, zeroed by
`PrepareForKnockOverAttempt` 0x005C3780 and filled by `HandleObjectUpAxisChanged` 0x005C3A98)
selects the success trigger `+0x15C` (plus objective 0xD and `NeedActionCompleted(0)`) when
non-zero, and the failure trigger `+0x160` when zero. Confirmed exactly as the inventory says.

---

## Q8. `BehaviorKnockOverCubes` streamline gate +0xD8/+0xD9 (M13-014)

Both are bytes on the behaviour base class `IBehavior` (not on Robot).

- **+0xD9 = `alwaysStreamline`**, the JSON config key. `IBehavior::ReadFromJson` 0x005BBFB4:
  0x005BC208 `add r1,pc` -> 0x00BF2AF9 `"alwaysStreamline"`; 0x005BC216 `add.w r2,r5,#0xd9`
  (destination); 0x005BC21E `blx JsonTools::GetValueOptional<bool>`.
- **+0xD8 = a runtime streamline/resume flag**, computed by `IBehavior::Init` 0x005BCB54 from the
  robot's `BehaviorManager` (robot+0x44):
  ```
  0x005BCCAA  ldr      r0, [r4, #0x2c]     ; robot
  0x005BCCAC  ldr      r0, [r0, #0x44]     ; BehaviorManager
  0x005BCCAE  ldr      r1, [r0, #0x58]     ; current behavior class (0x55 = none/default)
  0x005BCCB0  cmp      r1, #0x55
  0x005BCCB2  itte     ne
  0x005BCCB4  ldrbne.w r0, [r0, #0x5c]     ; switch mode: 0="soft", 1="hard"
  0x005BCCB8  eorne    r0, r0, #1          ; +0xD8 = NOT hard  (soft -> 1)
  0x005BCCBC  moveq    r0, #0             ; no current class -> 0
  0x005BCCC2  strb.w   r0, [r4, #0xd8]
  ```
  `BehaviorManager::BehaviorManager` 0x005A0864 initialises +0x58 = 0x55 (0x005A0864 line 62) and
  +0x5C = 0 (line 65). `BehaviorManager::SwitchToRequestedSpark` 0x005A4198 sets
  `+0x58 = +0x60` (0x005A4198 line 49) and `+0x5C = +0x64` (line 50), and logs the two modes as
  the strings at 0x00BF0B78 `"soft"` / 0x00BF0B7D `"hard"`. So +0xD8 = 1 when the behavior was
  entered by a **soft** spark switch (resume), 0 for a hard switch or no current behavior.
  It is not a JSON key; it is derived at Init.
- **Branches**:
  - `InitInternal` 0x005C31A2: 0x005C31B4 `ldrb.w r0,[r5,#0xd9]`; 0x005C31B8 `cbnz 0x5C31C0`;
    0x005C31BA `ldrb.w r0,[r5,#0xd8]`; 0x005C31BE `cbz 0x5C31CC`. If **+0xD9 != 0 or +0xD8 != 0**
    -> `TransitionToKnockingOverStack` (0x005C31C4); else -> `TransitionToReachingForBlock`
    (0x005C31D0). Streamline **skips the reach-for-block phase** and goes straight to knocking over.
  - `TransitionToKnockingOverStack` 0x005C34A8: 0x005C34EA `ldrb.w r0,[sl,#0xd9]`;
    0x005C34EE `vldr s16,[pc,#0x1c8]` -> 0x005C36B8 = **0.0**; 0x005C34F2 `cbnz 0x5C350A`;
    0x005C34F4 `ldrb.w r0,[sl,#0xd8]`; 0x005C34F8 `cbnz 0x5C350A`. If either is set, `maxTurn`
    stays **0.0**; else 0x005C34FA `ldr.w r0,[sl,#0x140]` (attempt count), 0x005C34FE
    `adr r1,#0x1bc` -> 0x005C36BC = **pi/2**, 0x005C3500 `cmp r0,#0`, 0x005C3504 `addgt r1,#4`
    -> 0x005C36C0 = **0.0**. So streamline (or a retry, +0x140 > 0) sets `maxTurn = 0` (no turn
    towards the last face pose); the first non-streamline attempt uses pi/2.
  - `TransitionToPlayingReaction` 0x005C3908: 0x005C3972 `ldrb.w r1,[r4,#0xd9]`; 0x005C3976
    `cbnz 0x5C39CA`; 0x005C3978 `ldrb.w r1,[r4,#0xd8]`; 0x005C397C `cbnz 0x5C39CA`. If either is
    set, it skips playing the `TriggerLiftSafeAnimationAction(trigger +0x15C/+0x160, 1, 1, 0,
    60.0, 0)` (0x005C39A4); else it plays it. Streamline **suppresses the reaction animation**.

---

## Contradictions / records affected

1. **M13-014 (and MISSING item 10)**  -  the store at 0x005C3946..0x005C394E is
   `robot->[+0x34]->[+0x94]->[+0xC] = 1`, i.e. `BlockWorld->BlockConfigurationManager->dirty flag`,
   **not** `robot->[+0x264]->[+0x18]->[+0xC]`. The `+0x264->+0x18` chain is the *callback's*
   whiteboard write (0x005C3DDE..0x005C3DEA, `str r1,[r0,#0x70]`), a different path. The record's
   `TransitionToPlayingReaction` clause must be re-cited/reworded.
2. **M13-002 (and MISSING item 1)**  -  the role of each constructor offset is now settled (Q1); the
   record should state the roles, not just the values. +0x12C = speed, +0x130 = extra drive distance,
   +0x134 = lift height, +0x138 = lift trigger distance, +0x13C = queued-lift id, +0x140 =
   shouldCheckPreActionPose.
3. **M13-016 (and MISSING item 6)**  -  the numeric returned by `GetPreActionTypeFromAlignmentType`
   indexes `PreActionPose::ActionType`; values 0..6 are native, **names are not in the shipped
   binary** (the repo's names are INFERRED). Alignment type 1 -> 0; 0/2/3/invalid -> 1.
4. **M13-013 (MISSING items 7/8)**  -  +0x44 = `RobotActionType` (7 =
   `DRIVE_OFF_CHARGER_CONTACTS`); `SetTracksToLock(0)` is a local flag write to `IActionRunner+0x54`,
   not a robot message.
5. **M13-017 (MISSING item 16)**  -  `robot+0x355` = `OffTreadsState` (0 = OnTreads); it is not the
   on-contacts flag (`robot+0x34A`). The stack should model the enum, not `OffTreadsState == OnTreads`
   as an opaque "wait".
6. **M13-010 (MISSING item 12)**  -  `ShouldPlayEightiesMusic` score = round of the workout's
   `MoodScorer` mean graph value (via `MoodScoreHelper`), roll `RandDbl(1.0) < 0.1`, cache flag at
   +0x11 and answer at +0x10.
7. **M13-014 (MISSING item 11)**  -  +0xD9 = JSON `alwaysStreamline`; +0xD8 = runtime soft/hard
   switch flag computed in `IBehavior::Init`; both bypass the reach and the reaction animation and
   zero the maxTurn.

## Open questions for the manager

- Whether the `PreActionPose::ActionType` **names** should be recorded as authority-6/inferred or
  left UNKNOWN (values are EXACT_SOURCE; names have no shipped table).
- Whether the `BlockConfigurationManager+0xC` dirty flag deserves its own NEW record, or is folded
  into M13-014's evidence (it is reached from M13-014 but is a BlockWorld/BlockConfigurations
  behaviour).
- `MoveLiftToHeightAction` `Preset=2` (used by `FlipBlockAction::CheckIfDone`) is a M12/manipulation
  preset; its name is not read here (the enum only has the string `UnknownPreset` at file 0x00548F14
  nearby). M13-002 only needs the numeric.

*Read-only extraction. Nothing outside `.scratch/B-M13/` was changed.*

**M13-018, gap pass 3 follow-up (2026-09-28):** the heuristic's per-goal Dijkstra and the reflected primitive set it walks are now read and folded into M13-018's evidence (Appendix F).


## Appendix F: gap pass 3 heuristic extraction report (2026-09-28)

﻿# B-M13 extractor — `ExpandCollisionStatesFromGoal` edge weights (resolves M13-018's last UNKNOWN)

Scope: M13-navigation, the only UNKNOWN blocking record **M13-018** — the exact edge weights used by
`xythetaPlannerImpl::ExpandCollisionStatesFromGoal` (entry 0x0085998E, body 0x00859BF0..0x00859FBD)
and the meaning of its return, its loop bounds, and the 1000.0 comparison in `InitializeHeuristic`.

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, file VAs, Thumb (`CS_MODE_THUMB`).
The Ghidra decompilation under `re-analysis/decomp/libcozmoEngine/` was used only to navigate; every
instruction below was re-read from the `.so`. Read-only: nothing outside `.scratch/B-M13/` was written.

Legend: `+0xNN` = byte/word offset on the object named in the sentence. All addresses are file VAs.

---

## Direct answers

### 1. What it starts from, and what "leaving soft collision" means for the return

It starts from **the goal StateID itself**, not its neighbours.

* It reads the goal StateID (`0x00859C04 ldr r0,[r5]`), seeds the per-state cost map at `planner+0xAC`
  to 0 for that goal (`0x00859C0A mov r0,r8` with `r8=this+0xAC`; `0x00859C10 movs r1,#0`;
  `0x00859C12 str r1,[r0]`), and pushes the goal onto the OpenList with cost 0
  (`0x00859C28 ldr r0,[r5]`; `0x00859C2A str r0,[sp,#0x80]`; `0x00859C2C..0x00859C32 OpenList::insert`,
  `r2=0`).

Each iteration pops the smallest-cost state (`topF` 0x00859C6E, `pop` 0x00859C78), builds a `State`
from the popped StateID (`0x00859C84..0x00859C88`), and calls
`xythetaEnvironment::IsInSoftCollision` (`0x00859CA2 blx`). `IsInSoftCollision` 0x00851708 returns 1
when a polygon in the heading bucket contains the point, 0 when none does (free space).

* **"Leaving soft collision" = the first popped state for which `IsInSoftCollision` returns 0.**
  When that happens the function stops (`0x00859CA8 cmp r0,#0`; `0x00859CAE beq.w #0x859F50`), logs
  `"expanded %u states for heuristic, reached free space %d times with cost of %f and have %lu in
  heurMap"` (string 0xC29F9F; `0x00859F50..0x00859F7A`), and returns the **popped accumulated cost**
  (`0x00859F7E..0x00859F9C` cleanup, `0x00859FAE vmov r0,s18`).
* So the returned value is the accumulated traversal cost from the goal, through states that are all
  in soft collision, out to the first free state reached.

Note: `InitializeHeuristic` only calls this function when the goal is itself in soft collision
(see step 2); if the goal is already free it uses cost 0.0 and never calls it.

### 2. The edge weight, the successor helper, and how the accumulated cost is stored/compared

**Edge weight = parent accumulated cost + `MotionPrimitive+4` (primitive traversal cost) + the
soft-collision penalty term. It is both, not one or the other.**

* `SuccessorIterator::Next` 0x0085110C writes the successor cost to `iterator+0x24`:
  * `0x0085150A vldr s2,[r5,#0x14]` = parent accumulated cost (stored at `iterator+0x14` by the ctor
    from the 4th argument, 0x0085041E `str r3,[r0,#0x14]`);
  * `0x00851516 vldr s4,[r1,#4]` = the primitive's traversal cost `MotionPrimitive+4`;
  * `0x0085151A vadd.f32 s2,s2,s4` = parent + primitive;
  * `0x0085151E vadd.f32 s0,s0,s2` = + the soft-collision sum `s0`;
  * `0x00851522 vstr s0,[r5,#0x24]`.
* The soft-collision term is the same one the A* successor uses: per containing polygon with penalty
  `< 1000.0` (hard threshold loaded at `0x00851162`, literal 0x008514D8 = 0x447A0000 = 1000.0f),
  `base + penalty * IntermediatePosition+0x10`, summed over the sampled intermediate poses
  (non-turning `0x0085137E vldr s0,[r7,#0x44]`; `0x0085138A bge`; `0x0085138C..0x00851398`;
  turning `0x0085146E..0x00851490`; `base` 0.0/1000.0 chosen at `0x008512FA..0x0085130A`).

**Which helper produces the successors.** Both, layered:
* `xythetaEnvironment::GetSuccessors` 0x00855D78 is a 24-byte wrapper that dereferences the StateID and
  tail-calls the `SuccessorIterator` constructor (thunk 0x004CEA08 -> 0x008503D8).
* The caller then drives the iterator with `SuccessorIterator::Done` 0x0085043A and
  `SuccessorIterator::Next` 0x0085110C, reading the successor StateID at `iterator+0x1C` and the
  successor cost at `iterator+0x24`.
* **Important, and not in the record:** `GetSuccessors` is called here with its 5th argument = **1**
  (`0x00859E62 movs r0,#1`; `0x00859E66 str r0,[sp]`; `0x00859E6A blx #0x4CED98`). The ctor stores
  that byte at `iterator+0x2C` (`0x00850428 strb.w lr,[r0,#0x2C]`, `lr = [sp,#8]`). `Next`/`Done` read
  it and select the primitive set: `0x00851130 cmp r2,#0` / `0x00851132 it eq` / `0x00851134 moveq r3,r0`
  with `r0 = env+0x14`, else `r3 = env+0x20`; `Done` does the same at `0x00850448..0x0085044C`.
  `env+0x14` is the forward/normal primitive set (`ParseMotionPrims`, 0x00852014, resizes and fills
  `this+0x14`); `env+0x20` is the reflected set built by `PopulateReverseMotionPrims` 0x008544C0
  (reads `this+0x14`, resizes and writes `this+0x20`, negating the end-pose x/y and rewriting the
  theta byte: `0x008544C0` body, `local_140 = CONCAT22(-...,-(short)local_140)`).
  The A* `ExpandState` 0x0085A34C passes **0** (`0x0085A3D8 movs r0,#0`; `0x0085A3DA str r0,[sp]`;
  `0x0085A3DE blx`), i.e. the forward set. So the heuristic expansion walks the **reflected/reverse**
  primitive set, which is why its costs mirror a forward move toward the goal.

**How the accumulated cost is stored/compared.** No A* `StateTable`. Three structures:
* **OpenList** (min-`f`, `std::multimap<float,StateID>` ordered by `std::less<float>`, 0x0084E4E8) on the
  stack at `sp+0x84`; `topF` returns the smallest key, `pop` removes the root and returns its StateID.
  Goal inserted with 0 (`0x00859C32`); successors inserted with their cost
  (`0x00859EC0 mov r0,r7` (OpenList); `0x00859EB8 vmov r2,s20`; `0x00859EC2 blx OpenList::insert`).
* A **visited set** `std::unordered_set<unsigned int>` at `sp+0x90` (the `__hash_table<unsigned_int,...>`
  at `local_58`). Popped states are inserted (`0x00859D06..0x00859E58`); a successor already present is
  skipped (`0x00859EAC mov r0,r5` (set at sp+0x90); `0x00859EAE str r6,[sp,#0x2C]`;
  `0x00859EB0 blx hash_table<uint>::find`; `0x00859EB6 bne` back to the loop).
* The **per-state best-cost map** at `planner+0xAC` — the same `std::unordered_map<unsigned int,float>`
  that `heur` 0x0085A780 memoizes in. The goal is seeded to 0; each expanded state is inserted with its
  cost if absent (`0x00859CDA operator_new(0x10)`; `0x00859CE6 vstr s18,[r6,#0xC]`;
  `0x00859CF0..0x00859CF6 __node_insert_unique` into `this+0xAC`) or lowered if the new cost is smaller
  (`0x00859CC6 vldr s0,[r0,#0xC]`; `0x00859CCA vcmpe.f32 s18,s0`; `0x00859CD4 vstrmi s18,[r0,#0xC]`).
  (A consequence for `heur`: a state expanded here is later returned from the memo map directly rather
  than via `heur_internal`.)

### 3. The exact return

* **Popped state no longer in soft collision** (`IsInSoftCollision` returns 0): returns the accumulated
  cost of that popped state — the OpenList key read by `topF` into `[sp,#0x24]`
  (`0x00859C6E..0x00859C72`) and moved to `s18` at `0x00859CAA vmov s18,r1`; `0x00859FAE vmov r0,s18`.
* **OpenList empties before a free state is found:** returns **0.0**. `0x00859C58 cmp r0,#0`;
  `0x00859C5A bne.w #0x859F0C`; log `"ran out of open list entries during ExpandStatesForHeur after %u
  exps!"` (string 0xC2A083); `0x00859F4A vmov.f32 s18,s16`; `b #0x859FA0`. `s16` = literal at 0x00859FC0
  = 0x00000000.
* **Run/continue flag is 0 (abort):** returns **0.0**. `0x00859C5E ldr.w r0,[r4,#0x90]` (flag pointer);
  `0x00859C64 ldrb r0,[r0]`; `0x00859C66 cmp r0,#0`; `0x00859C68 beq.w #0x859F4A` -> `s18 = s16 = 0.0`.
* **Expansion cap exceeded:** returns the **last popped cost** (`s18`, already set for the current
  iteration at `0x00859CAA`). `0x00859EC8 ldr r6,[sp,#0x1C]`; `0x00859ECA adds r6,#1`;
  `0x00859ECC cmp.w r6,#0x3E8`; `0x00859ED0 bls.w #0x859C52`; fall-through warns
  `"exceeded max allowed expansions of %d"` (string 0x85A080) with `r3 = 0x3E8` (1000)
  (`0x00859EE4 mov.w r3,#0x3E8`), then `0x00859F9C..0x00859FA0` and returns `s18`.

### 4. Loop bounds and the 1000.0 comparison in `InitializeHeuristic`

* **Expansion cap:** counter initialised 0 at `0x00859C4C movs r6,#0`, stored each iteration at
  `0x00859C7C str r6,[sp,#0x1C]`, incremented at the end of an expansion (`0x00859ECA adds r6,#1`),
  and the loop continues while the counter is `<= 1000` (`0x00859ECC cmp.w r6,#0x3E8`;
  `0x00859ED0 bls.w #0x859C52`). It therefore permits up to 1001 popped soft-collision expansions
  before giving up, while the warning prints **1000** (`0x00859EE4 mov.w r3,#0x3E8`, string 0x85A080).
* **`InitializeHeuristic` 0x008598DC, per goal:** if the goal is not in soft collision the cost is 0.0
  (`0x00859980 blx IsInSoftCollision`; `0x00859984 cmp r0,#0`; `0x00859986 beq.w #0x859AB8`;
  `0x00859AB8 vmov.f32 s20,s18`, `s18 = 0.0` from `0x00859946`). Otherwise it calls
  `ExpandCollisionStatesFromGoal(goal)` and takes the return (`0x0085998A mov r0,r4`;
  `0x0085998C mov r1,sb`; `0x0085998E blx`; `0x00859992 vmov s20,r0`).
* **Comparison with 1000.0:** `0x008599E0 vcmpe.f32 s20,s16` with `s16 = 0x447A0000 = 1000.0f`
  (literal at 0x00859B90, loaded at `0x00859942 vldr s16,[pc,#0x24C]`); `0x008599E8 ble #0x859ABC`.
  So a goal with cost **<= 1000.0 survives**; a goal with cost **> 1000.0 is removed** with the warning
  `"very high cost of _costOutsideHeurMap = %f, so removing goal %d"` (string 0xC29F5F;
  `0x008599F2..0x00859A06`) and a swap-remove (`0x00859A28..0x00859AB6`).
* **Stored value:** the surviving goal's id byte and cost go to the `heurMap` entry
  (`0x00859AC4 strb r1,[r0,r6]`; `0x00859ACA vstr s20,[r0,#4]`) — `heurMap` is
  `vector<pair<unsigned char,float>>` (resize thunk 0x00859BB8), cost at entry+4, consistent with
  `heur_internal` reading `vldr s18,[r2]` with `r2 = heurMap_begin + 4` (`0x0085A800 add r2,r7`,
  `r7` starts 4 and advances 8; `0x0085A802 vldr s18,[r2]`).
* **Return:** 1 iff any goal remains, else 0 (`0x00859ADE ldr r1,[r4,#8]`;
  `0x00859AE0 movs r0,#0`; `0x00859AE2 cmp r5,r1`; `0x00859AE4 it ne`; `0x00859AE6 movne r0,#1`).

---

## Production-path steps (one row per behaviour-changing step)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| P1 | `InitializeHeuristic` clears `heurMap` (planner+0xA0) and the `heur` memo map (planner+0xAC), then resizes `heurMap` to the goal count | 0x008598EA `ldr r1,[r0,#0xA0]!`; 0x008598F2..0x00859908 (clear); 0x0085990C `blx` (map clear); 0x00859914..0x0085991E `resize` | M13-018 (partial) | EXACT_SOURCE |
| P2 | per goal: if `IsInSoftCollision(goal) == 0` cost = 0.0, else cost = `ExpandCollisionStatesFromGoal(goal)` | 0x00859980 `blx`; 0x00859986 `beq.w #0x859AB8`; 0x00859AB8 `vmov.f32 s20,s18`; 0x0085998E `blx`; 0x00859992 `vmov s20,r0` | NEW | EXACT_SOURCE |
| P3 | drop a goal whose cost `> 1000.0` (keep `<= 1000.0`) and warn | 0x00859942 `vldr s16,[pc,#0x24C]` -> 0x00859B90 = 0x447A0000; 0x008599E0 `vcmpe.f32 s20,s16`; 0x008599E8 `ble #0x859ABC`; warning 0xC29F5F | M13-018 (partial) | EXACT_SOURCE |
| P4 | store the surviving goal's id + cost at `heurMap` (+0 / +4) | 0x00859AC4 `strb r1,[r0,r6]`; 0x00859ACA `vstr s20,[r0,#4]` | M13-018 (partial) | EXACT_SOURCE |
| P5 | return 1 iff any goal remains | 0x00859ADE..0x00859AE6 | M13-018 (partial) | EXACT_SOURCE |
| P6 | `ExpandCollisionStatesFromGoal` starts from the goal StateID: seeds its own map entry to 0 and pushes the goal on the OpenList with cost 0 | 0x00859C04 `ldr r0,[r5]`; 0x00859C0A..0x00859C12; 0x00859C28..0x00859C32 | NEW | EXACT_SOURCE |
| P7 | pop the smallest-cost state; if it is not in soft collision, log and return its accumulated cost | 0x00859C6C..0x00859C78 (topF/pop); 0x00859CA2 `blx`; 0x00859CAE `beq.w #0x859F50`; 0x00859F50..0x00859FAE | NEW | EXACT_SOURCE |
| P8 | record each expanded state's best cost in the map at planner+0xAC (insert, or lower if smaller) | 0x00859CB6..0x00859CD4; 0x00859CDA..0x00859D02 | NEW | EXACT_SOURCE |
| P9 | mark each expanded state in a `std::unordered_set<unsigned int>` at sp+0x90; skip successors already there | 0x00859D06..0x00859E58; 0x00859EAC..0x00859EB6 | NEW | EXACT_SOURCE |
| P10 | generate successors with `GetSuccessors` and its 5th argument = 1, which makes `SuccessorIterator` iterate the reflected set env+0x20 (the A* path uses 0 -> env+0x14) | 0x00859E5A..0x00859E6A (`0x00859E62 movs r0,#1`; `0x00859E66 str r0,[sp]`); 0x00855D78; ctor 0x008503D8 store 0x00850428 `strb.w lr,[r0,#0x2C]`; 0x00851130..0x00851136; 0x00850448..0x0085044C; `PopulateReverseMotionPrims` 0x008544C0 | NEW | EXACT_SOURCE |
| P11 | successor cost = parent accumulated cost + `MotionPrimitive+4` + soft-collision penalty (both terms) | 0x0085150A `vldr s2,[r5,#0x14]`; 0x00851516 `vldr s4,[r1,#4]`; 0x0085151A `vadd.f32`; 0x0085151E `vadd.f32`; 0x00851522 `vstr s0,[r5,#0x24]`; penalty 0x0085137E..0x00851398 and 0x0085146E..0x00851490; base 0x008512FA..0x0085130A | NEW | EXACT_SOURCE |
| P12 | push each non-visited successor into the OpenList with that cost | 0x00859E9C `ldr r6,[sp,#0x5C]`; 0x00859EA0 `vldr s20,[sp,#0x64]`; 0x00859EB8..0x00859EC2 | NEW | EXACT_SOURCE |
| P13 | expansion cap: increment the counter, continue while `<= 1000`, else warn with 1000 and return the last popped cost | 0x00859EC8..0x00859ED0; 0x00859EE4 `mov.w r3,#0x3E8`; string 0x85A080 | NEW | EXACT_SOURCE |
| P14 | empty OpenList returns 0.0 after logging | 0x00859C58/0x00859C5A; 0x00859F0C..0x00859F4A; s16 at 0x00859FC0 = 0x0 | NEW | EXACT_SOURCE |
| P15 | run/continue flag 0 (planner+0x90 -> byte) aborts and returns 0.0 | 0x00859C5E..0x00859C68; 0x00859F4A `vmov.f32 s18,s16` | M13-018 (partial) | EXACT_SOURCE |
| P16 | `Next` applies the hard-collision threshold 1000.0 to decide soft vs hard while sampling the reflected primitive | 0x00851162 `vldr s18,[pc,#0x374]` -> 0x008514D8 = 0x447A0000; 0x0085138A `bge`; 0x0085147A `bge` | NEW | EXACT_SOURCE |

---

## Contradictions of existing records

None. Every M13-018 evidence clause about this function is confirmed:

* "a soft-collision Dijkstra (0x00859BF0..0x00859FBD)" — confirmed.
* "dropping goals whose cost exceeds 1000.0 (0x008599E0)" — confirmed (`vcmpe`/`ble`; `> 1000.0`
  removed, `<= 1000.0` kept).
* "storing the rest at heurMap+4 (0x008599CA)" — confirmed (`0x00859ACA vstr s20,[r0,#4]`).
* "heurMap comes from InitializeHeuristic ... resizes planner+0xA0 to the goal count (0x0085991E)" —
  confirmed.

## Records whose evidence is too weak / partial for what they claim

* **M13-018** — its evidence for this function is accurate but incomplete. It does not state:
  (a) that `ExpandCollisionStatesFromGoal` is only called when the goal is in soft collision
  (`IsInSoftCollision` gate at 0x00859980/0x00859986), so "for each goal runs
  ExpandCollisionStatesFromGoal" is not unconditional;
  (b) that its successors come from the **reflected/reverse** primitive set env+0x20 (GetSuccessors 5th
  arg = 1), not the A* set;
  (c) the edge weight (`parentG + MotionPrimitive+4 + soft-collision penalty`);
  (d) the frontier/visited/best-cost structures (OpenList at sp+0x84, unordered_set at sp+0x90, memo map
  at planner+0xAC) and the 1000-expansion cap with its return behaviour;
  (e) that it writes its per-state costs into the same map `heur` memoizes in.
  These are the behaviours the record's `unresolved` asks to settle; they are now read.

## Open questions for the manager

1. The `ExpandCollisionStatesFromGoal` return when the goal itself is free is not exercised through
   `InitializeHeuristic` (that path short-circuits to 0.0), but the function would return 0.0. Decide
   whether M13-018's effect text should mention the IsInSoftCollision gate explicitly.
2. The function walks the **reflected** primitive set (env+0x20, `PopulateReverseMotionPrims`). The
   manager may want a separate NEW record for that set's construction (the reflection of end-pose x/y
   and theta, and the copied `+4` cost) rather than folding it into M13-018, since it is a
   behaviour-changing input to the heuristic.
3. The 1000.0 constant appears in three places on this path: the soft/hard collision threshold
   (0x00851162), the expansion cap/print (0x00859EE4, 0x85A080) and the goal-drop comparison
   (0x00859942/0x008599E0). The cap counter continues while `<= 1000` but the warning prints 1000
   (an off-by-one in the source); decide whether that belongs in the record.

*Read-only extraction. Nothing outside `.scratch/B-M13/` was changed.*

**M13-003/010/018, gap pass 3 follow-up 2 (2026-09-28):** the per-intermediate reciprocal, the workout mood-scorer schema and the reflected primitive set / StartPlanning(false) branch are now read and folded into those records (Appendix G).


## Appendix G: gap pass 3 follow-up extraction report (2026-09-28)

﻿# B-M13 extractor follow-up: four UNKNOWN sub-parts

Read-only follow-up for **M13-navigation**, repo `cozmo-stack-w3`.
Engine: `resources/lib/armeabi-v7a/libcozmoEngine.so`, ELF VAs, Thumb.
Ghidra decomp was used only to find functions; every fact below is cited to an instruction in the `.so`.

Call sites the task named are **call sites inside `LatticePlannerImpl::StartPlanning` (0x004FEB44)**, not the callee entries:
- `0x004FEC50` is the call to `xythetaEnvironment::FindClosestPlanSegmentToPose` (real body `0x00856310`, thunk `0x004A66F4`).
- `0x004FF424` is the call to `xythetaEnvironment::PlanIsSafe` (real body `0x00850B78`, thunk `0x004A6778`).

---

## 1. `xythetaEnvironment::PopulateReverseMotionPrims` 0x008544C0 (called from `ParseMotionPrims` at 0x008524AC)

Entry `0x008544C0`, body `0x008544C0..0x0085464F`. Called at `0x008524AC: blx #0x4CE8A0` from `ParseMotionPrims`.

**Sets involved.** `this+8` = `num_angles` (0x008544E4/E8 load it, `resize` the destination). `this+0x14` = forward `vector<vector<MotionPrimitive>>` (source, written by `ParseMotionPrims`). `this+0x20` = reverse `vector<vector<MotionPrimitive>>` (destination). Outer element = 0xC bytes; `MotionPrimitive` stride = 0x124 (`0x00854612: add.w sb, sb, #0x124`).

**Loop.** Outer index `sl` (starts 0 at `0x00854508: mov.w sl, #0`, increments at `0x00854640: add.w sl, sl, #1`) runs 0..num_angles-1 over the forward buckets. For every forward primitive:

| step | what it does | citation |
|---|---|---|
| read forward end-pose theta | `r5 = *(u8*)(prim+0xC)`; this is the primitive's `end_state_offset.theta` | `0x00854540: ldrb r5, [r7, #0xc]` |
| copy header | copies `prim+0`, `+4`, `+8` and the halfword at `+0xC` into the local struct | `0x00854542: ldm r1!, {r2,r3,r6}` / `0x00854544: stm r0!, {r2,r3,r6}` / `0x00854546: ldrh r1, [r1]` / `0x00854548: strh r1, [r0]` |
| copy intermediate vector | `vector<IntermediatePosition>` at `prim+0x10` deep-copied to local | `0x0085454A: add.w r1, r7, #0x10` / `0x00854550: blx #0x4CE888` |
| copy bbox | words at `prim+0x1C..0x28` copied verbatim | `0x00854554..0x0085455E: ldm.w r0,{r2,r3,r4,r6}; stm r1!,{r2,r3,r4,r6}` |
| copy path | `Path` at `prim+0x2C` deep-copied to local (`Path::Path` is a copy ctor, `0x0085C74E`) | `0x00854560: add.w r1, r7, #0x2c` / `0x00854566: blx #0x4ABDD0` |
| **negate end-pose x and y** | low half (x at `+8`) and high half (y at `+0xA`) each `0 - v`, 16-bit | `0x0085456A: ldrh r0,[sp,#0x22]` / `0x0085456E: ldrh r1,[sp,#0x20]` / `0x00854572: rsbs r0,r0,#0` / `0x00854578: rsbs r1,r1,#0` / `0x0085457A: strh.w r1,[sp,#0x20]` / `0x0085457E: strh.w r0,[sp,#0x22]` |
| **set end-pose theta** | `*(u8*)(local+0xC) = sl` (the **outer loop index**, i.e. the forward primitive's start heading); the byte at `+0xD` is left as copied | `0x00854574: strb.w sl, [sp, #0x24]` |
| push into reverse bucket | bucket index = the forward **end** theta `r5`; `r0 = reverse_begin + (3*r5)<<2`; append the local struct | `0x00854582: add.w r1, r5, r5, lsl #1` / `0x00854588: ldr r0, [r0]` / `0x0085458A: add.w r0, r0, r1, lsl #2` / `0x0085459A..0x008545CE` |

**Exact answer to the theta question.** The theta byte is **not** `theta + num_angles/2 mod num_angles` and **not** a negation. It is **overwritten with the forward primitive's start-theta index** (the outer loop index `sl`). The reverse primitive is stored in bucket `forward_end_theta`, so its implicit start heading is the forward end heading and its explicit end heading is the forward start heading — a swap of the two headings, not a modular offset of the forward end theta. There is no arithmetic on the forward end theta at all (`r5` is used only as the bucket index).

**Fields that do NOT change** (contrary to the "arc block / straight_length / turn" framing):
- `MotionPrimitive+4` (cost): copied from `prim+4`, never touched (`0x00854544: stm r0!,{r2,r3,r6}` copies `+4`; only `sp+0x20`/`sp+0x22`/`sp+0x24` are rewritten afterwards).
- `straight_length_mm`, the `arc` block (`sweepRad`/`radius_mm`/`centerPt_x_mm`/`centerPt_y_mm`/`startRad`) and `turn_in_place_direction` are **not stored as fields** in `MotionPrimitive`; `MotionPrimitive::Create` bakes them into the `Path` at `+0x2C` (M13-004 evidence). That `Path` is deep-copied verbatim (`0x00854560`/`0x008545C4`), so none of those quantities is recomputed or reflected.
- The `IntermediatePosition` vector at `+0x10` and the cached bbox at `+0x1C..0x28` are copied verbatim.

**NEW observation for M13-018.** M13-018's evidence says `PopulateReverseMotionPrims` "negates the end-pose x/y and rewrites the theta byte". That is correct as far as it goes, but it omits that the primitive's `Path`, intermediate poses and bbox are shared unchanged with the forward primitive. That is behaviour-changing for the goal-side expansion that consumes `env+0x20` (M13-018 evidence: `ExpandCollisionStatesFromGoal` calls `GetSuccessors(...,1)` and `SuccessorIterator::Next` selects `env+0x20` when its flag is 1, `0x00851130..0x00851136`; the collision test walks the primitive's intermediate poses, M13-003). I do **not** claim the reflection is geometrically wrong — only that the record must state the copied path/poses/bbox, because a downstream reader would otherwise assume they were mirrored.

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| R1-1 | reverse vector resized to num_angles, cleared | `0x008544E4: ldr.w r1,[r8,#8]`; `0x008544EA: blx #0x4CE864` | M13-001/M13-018 partial | EXACT_SOURCE |
| R1-2 | end-pose x and y each negated (16-bit) | `0x0085456A..0x0085457E` | M13-018 partial | EXACT_SOURCE |
| R1-3 | end-pose theta set to the forward start index, not theta±k | `0x00854540`; `0x00854574` | M13-018 partial / NEW | EXACT_SOURCE |
| R1-4 | cost `+4`, Path `+0x2C`, intermediate vector `+0x10`, bbox `+0x1C..0x28` copied unchanged | `0x00854542..0x00854566`; `0x0085459A..0x008545C4` | NEW | EXACT_SOURCE |

---

## 2. `IntermediatePosition+0x10` in `MotionPrimitive::Create` 0x00853DD0 (range 0x00853E96..0x00853F4E)

`IntermediatePosition` layout (from `IntermediatePosition::Import` 0x00850130 and the `emplace_back` at 0x00857FA4): `+0` float x_mm, `+4` float y_mm, `+8` float theta_rads, `+0xC` u8 theta index, `+0x10` float inverseDist. Stride 0x14 (`0x00857FA4`: `local_30[4] = fVar9` at +0x10, then `+5` words).

For each JSON `intermediate_poses` entry, `State_c::Import` writes the current pose at `sp+0x40`/`sp+0x44`/`sp+0x48` (`0x00853E8A: mov r0, r6` where `r6 = sp+0x40`; `0x00853E8C: blx #0x4CE720`). Then:

| step | what it does | citation |
|---|---|---|
| previous = last stored intermediate pose, or none | `begin`/`end` of the vector at `this+0x10`; if empty skip | `0x00853E9E: ldrd r1, r0, [sb, #0x10]`; `0x00853EA2: cmp r1, r0`; `0x00853EA4: beq #0x853F52` |
| `dist` = Euclidean step distance to the previous pose | `dx = cur.x - prev.x` (`0x00853EA6/0x00853EAC/0x00853EBA`), `dy = cur.y - prev.y` (`0x00853EB2/0x00853EB6/0x00853EC2`), `sqrt(dx^2+dy^2)` -> `s22` | `0x00853EC6..0x00853EDE: vcvt/vmul/vadd/vsqrt s22` |
| `dtheta` = rescaled heading change of that step | `cur.theta_rads` at `[sp+0x48]` (`0x00853EFA`) -> `Radians::Radians` (`0x00853EFE`); `prev.theta_rads` at `[prev+0x08]` (`0x00853EBE: vldr s24, [r0, #-0xc]`) -> `Radians::Radians` (`0x00853F0A`); `Anki::operator-` gives `cur - prev` (`0x00853F0E: blx #0x4A454C`, result at `sp+0x34`) | `0x00853EBE`; `0x00853EFA`; `0x00853F0E` |
| formula | `inverseDist = 1.0 / (env[0x78] * env[0x60] * |dtheta| + dist)` | `0x00853F18: vldr s0,[sp,#0x34]`; `0x00853F1E: vldr d1,[r0,#0x60]`; `0x00853F24: vabs.f32 s0,s0`; `0x00853F28: vldr d2,[r0,#0x78]`; `0x00853F32/0x00853F36: vmul.f64`; `0x00853F3A: vcvt.f64.f32 d1,s22`; `0x00853F3E: vadd.f64 d0,d0,d1`; `0x00853F46: vdiv.f32 s0,s20,s0` (s20 = 1.0) |
| first pose | no previous -> `inverseDist` stays 0.0 (r8 cleared at `0x00853E96/0x00853E9A`, stored at `0x00853F4E` only on the non-empty path) | `0x00853E96`; `0x00853E9A`; `0x00853E9E` |

**Exact answer.** `dist` is the Euclidean distance **to the previous intermediate pose already in the vector**, not the distance from the start. `dtheta` is the **heading change of that step**: `Radians(cur.theta_rads) - Radians(prev.theta_rads)`, where `Radians::Radians` rescales into `[-pi, pi)` (`Radians::rescale`, called from `0x0084C832`). The first intermediate pose gets `inverseDist = 0`. This is the multiplier at `IntermediatePosition+0x10` that M13-003 uses for the soft-collision penalty (`successor cost += penalty * IntermediatePosition+0x10`).

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| R2-1 | `dist` = distance to previous intermediate pose | `0x00853E9E..0x00853EDE` | M13-003 / NEW detail | EXACT_SOURCE |
| R2-2 | `dtheta` = cur.theta_rads - prev.theta_rads, rescaled | `0x00853EBE`; `0x00853EFA`; `0x00853F0E` | M13-003 / NEW detail | EXACT_SOURCE |
| R2-3 | `inverseDist = 1/(env78*env60*|dtheta|+dist)`; first pose 0 | `0x00853F1E..0x00853F4E` | M13-003 / NEW detail | EXACT_SOURCE |

---

## 3. `WorkoutConfig` mood scorer and `ShouldPlayEightiesMusic`

There is **no `WorkoutConfig::Import`** symbol in the binary. The JSON reader is `WorkoutConfig::InitConfiguration` 0x005736E8, called per `workouts` array entry by `WorkoutComponent::InitConfiguration` 0x00573BC0.

**Keys read for the mood scorer** (`WorkoutConfig::InitConfiguration`):

| JSON key | destination | citation |
|---|---|---|
| `numStrongLifts` | `MoodScorer` at `WorkoutConfig+0x18` | key pointer computed at `0x0057380E: adr r1,#0x30c` -> string `numStrongLifts` at `0x00573B1C`; `0x005738E4: add.w r0,r4,#0x18`; `0x005738EA: blx #0x4AD3A8` |
| `numWeakLifts` | `MoodScorer` at `WorkoutConfig+0x24` | key at `0x005738EE: adr r1,#0x23c` -> `numWeakLifts` at `0x00573B2C`; `0x00573956: add.w r0,r4,#0x24`; `0x0057395C: blx #0x4AD3A8` |

**`MoodScorer` schema.** `MoodScorer::ReadFromJson` 0x0067C67C requires an **array**; each element is an `EmotionScorer` (`0x0067C6C2: isArray`; `0x0067C67C`). `EmotionScorer::ReadFromJson` 0x0067AABC reads:

| key | destination | citation |
|---|---|---|
| `emotionType` (string) | `EmotionScorer+0` u8 (`EmotionTypeFromString`; 9 = invalid -> warning) | `0x0067AAC4: ldr r1,[pc,#0x140]` -> `emotionType` at `0xBFF1E0`; `0x0067AACC: blx #0x4A5F2C`; `0x0067AB1E: blx #0x4BC438` |
| `scoreGraph` | `GraphEvaluator2d` at `EmotionScorer+4` | `0x0067AAD0` -> `scoreGraph` at `0xBFF37A`; `0x0067AB5A: blx #0x4B1ED8` |
| `trackDelta` (bool) | `EmotionScorer+0x10` | `0x0067AADC` -> `trackDelta` at `0xBFF385`; `0x0067ABC4: blx #0x4AE758`; `0x0067ABC8: strb r0,[r4,#0x10]` |

**Graph schema.** `GraphEvaluator2d::ReadFromJson` 0x00804DAC reads key `nodes` (array); each node reads `x` and `y` as floats and calls `AddNode`:

| key | citation |
|---|---|
| `nodes` | `0x00804DB2` -> string `nodes` at `0xC23E8A`; `0x00804DBC: blx #0x4A5F2C` |
| `x` | `0x00804E60: adr r1,#0x14c` -> `x` at `0x00804FB0`; `0x00804E64: blx #0x4A5F2C`; `0x00804E8A: blx #0x4A7264` (asFloat) |
| `y` | `0x00804E6A: adr r1,#0x148` -> `y` at `0x00804FB4`; `0x00804E6E: blx #0x4A5F2C`; `0x00804E92: blx #0x4A7264` |

So the full schema is `[ { "emotionType": <string>, "scoreGraph": { "nodes": [ {"x": <float>, "y": <float>}, ... ] }, "trackDelta": <bool> }, ... ]`.

**`MoodScorer::EvaluateEmotionScore` 0x0067C9B8 (206 bytes).** The scorer vector is `this..this+4`, element stride 0x14 (`0x0067C9C4: ldrd r6, r5, [r0]`; `0x0067CA5E: adds r6, #0x10`; `0x0067CA62: cmp r6, r5`).

For each entry:
- emotion pointer = `MoodManager + emotionType*0x20` (`0x0067C9E4: ldrb r0,[r6]`; `0x0067C9E8: add.w r0, r4, r0, lsl #5`). `r4` is the `MoodManager` argument (`this` of the function is the `MoodScorer`).
- flag `trackDelta` = `entry+0x10` (`0x0067C9E6: ldrb r1, [r6, #0x10]`).
  - if `trackDelta != 0`: `x = emotion+0x18 - Emotion::GetHistoryValueTicksAgo(emotion, 0x3C)`. `0x0067C9EC: cbz r1, #0x67CA02`; `0x0067C9EE: movs r1, #0x3c`; `0x0067C9F0: vldr s22, [r0, #0x18]`; `0x0067C9F4: blx #0x4BC804`; `0x0067C9FC: vsub.f32 s22, s22, s0`.
  - if `trackDelta == 0`: `x = emotion+0x18` (the current value) — `0x0067CA02: vldr s22, [r0, #0x18]`.
- `y = GraphEvaluator2d::EvaluateY(entry+4 graph, x)` — the graph's node vector is copied at `0x0067CA08/0x0067CA0C` (`blx #0x4BC810`), then `0x0067CA10: vmov r1, s22`; `0x0067CA14: blx #0x4B1EE4`. `EvaluateY` 0x00804BD0 linearly interpolates `y` between the bracketing nodes and returns the first/last node's `y` outside the range.
- **zero rule:** if `|y| < 1e-5` (`s20` = `0x3727C5AC` at `0x0067C9DA`) the function returns 0.0 immediately — `0x0067CA3A..0x0067CA4C` (`vneg`, `it mi`, `vmovmi`), `0x0067CA50: vcmpe.f32 s2, s20`, `0x0067CA58: bmi #0x67CA76`, and `s16` is still 0.0 at `0x0067CA76: vmov r0, s16`.
- otherwise it accumulates `s18 += y` (`0x0067CA5A`) and decrements a counter (`0x0067CA60: subs r7, #1`).

After the loop:
- if no entries (`r7 == 0`) return 0.0 (`0x0067CA66: cbz r7, #0x67CA76`).
- else `mean = sum / count`: `0x0067CA68: rsbs r0, r7, #0`; `0x0067CA6E: vcvt.f32.u32 s0, s0`; `0x0067CA72: vdiv.f32 s16, s18, s0`. So the result is the **arithmetic mean** of the per-entry `EvaluateY` values, with an early 0.0 if any single entry's absolute value is below 1e-5.

**`WorkoutConfig::MoodScoreHelper` 0x00573B70.** If the scorer vector is empty (`r2 == r3`) return 0 (`0x00573B74/0x00573B76`); else call `EvaluateEmotionScore(this, robot+0x440)` (`0x00573B7E: ldr.w r2, [r0, #0x440]`), `roundf` (`0x00573B8A: blx #0x4A6688`), then `vcvt.u32.f32` (`0x00573B92`) — i.e. `max(0, round(score))` (a negative score saturates to 0).

**`WorkoutComponent::ShouldPlayEightiesMusic` 0x00573E30 input.** It is `*(u32*)(this+0xC) + 0x18` — the current workout config pointer (`this+0xC`, set by `WorkoutComponent::InitConfiguration` 0x00573BC0 line 137) plus `0x18`, i.e. the **`numStrongLifts` MoodScorer**:
- `0x00573E3C: ldr r1, [r4, #0xc]`; `0x00573E3E: ldr r0, [r4, #0x14]` (robot); `0x00573E40: adds r1, #0x18`; `0x00573E42: blx #0x4AD3C0` (MoodScoreHelper).
- if the helper returns non-zero, roll `RandomGenerator::RandDbl(GetRNG(robot), 1.0)` (`0x00573E4A/0x00573E4C/0x00573E58`) and set true iff the roll `< 0.1` (`0x00573E5C: vldr d0,[pc,#0x20]` -> double `0x3FB999999999999A` at `0x00573E80/0x00573E84`; `0x00573E64: vcmpe.f64`; `0x00573E6E: movmi r5,#1`).
- cache: `this+0x11 = 1`, `this+0x10 = result` (`0x00573E70/0x00573E72/0x00573E74`).

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| R3-1 | `numStrongLifts` -> MoodScorer `workout+0x18`; `numWeakLifts` -> `workout+0x24` | `0x0057380E`/`0x005738EA`; `0x005738EE`/`0x0057395C` | M13-010 partial | EXACT_SOURCE |
| R3-2 | EmotionScorer keys `emotionType`/`scoreGraph`/`trackDelta`; graph `nodes` of `{x,y}` | `0x0067AAC4..0x0067ABC8`; `0x00804DB2..0x00804E92` | M13-010 partial / NEW | EXACT_SOURCE |
| R3-3 | x = delta-over-60-ticks if `trackDelta` else current; y = `EvaluateY` | `0x0067C9E4..0x0067CA14` | M13-010 partial | EXACT_SOURCE |
| R3-4 | any `|y| < 1e-5` -> 0.0; else mean | `0x0067CA3A..0x0067CA72` | M13-010 partial / NEW | EXACT_SOURCE |
| R3-5 | `MoodScoreHelper` = `max(0, round(score))`; empty scorer -> 0 | `0x00573B74..0x00573B96` | M13-010 partial | EXACT_SOURCE |
| R3-6 | input to helper is `workout+0x18`; roll `< 0.1` | `0x00573E3C..0x00573E42`; `0x00573E5C..0x00573E6E` | M13-010 covered | EXACT_SOURCE |

---

## 4. `LatticePlanner::ComputePathHelper` 0x004FD1D0 and the `StartPlanning(false)` branch

### 4a. `ComputePathHelper`'s reuse call
`ComputePathHelper` calls the object's virtual slot `+0xC` with `true`:
- `0x004FD326: ldr r0, [r6]` (vptr); `0x004FD328: ldr r3, [r0, #0xc]`; `0x004FD32A: mov r0, r6`; `0x004FD32C: mov r1, fp`; `0x004FD32E: movs r2, #1`; `0x004FD330: blx r3`.
- The `LatticePlanner` vtable's address point is `0x101F188`; `+0xC` = `0x101F194` = `LatticePlanner::ComputeNewPathIfNeeded` (relocation at `0x101F194` for `_ZN4Anki5Cozmo14LatticePlanner22ComputeNewPathIfNeeded...`). `ComputeNewPathIfNeeded` 0x004FEB00 forwards its `bool` to `StartPlanning` (`0x004FEB20: mov r2, r4`; `0x004FEB22: blx #0x4A66E8`). So the live path passes **true**.

### 4b. `StartPlanning(false)` — the false branch
`StartPlanning` stores its bool at `impl+0xA1` (`0x004FEB7E: strb.w r6, [sl, #0xa1]`; `r6` = arg2). At `0x004FEBB2: ldrb.w r0, [sl, #0xa1]` / `0x004FEBB2: cbz r0, #0x4FEC2E`, false goes to `0x004FEC2E`:

1. `ImportBlockworldObstaclesIfNeeded(this, true, ...)` — `0x004FEC36: mov r0, sl`; `0x004FEC38: movs r1, #1`; `0x004FEC3A: blx #0x4A65B0`.
2. `FindClosestPlanSegmentToPose(env, plan, currentState, &dist, false)` — `0x004FEC50: blx #0x4A66F4`. Result index in `r4` (`0x004FEC54`), distance at `[sp+0x150]` (`0x004FEC40: str r0,[sp,#0x150]`).
3. Force-replan test: `0x004FEC56: vmov.f32 s2, #2.000000e+01`; `0x004FEC5A: vldr s0,[sp,#0x150]`; `0x004FEC5E: vcmpe.f32 s0,s2`; `0x004FEC66: blt #0x4FECD0`. If `dist >= 20.0` it logs `LatticePlanner.GetPlan.ForcePlan` and clears the old plan (`0x004FECB2..0x004FECCC`).
4. If still false, `0x004FECD0: ldrb.w r0,[sl,#0xa1]`; `0x004FECD6: beq.w #0x4FF410`.
5. `PlanIsSafe(env, plan, 40.0, index, &state, &outPlan)` — `0x004FF410: add r0, sp, #0x1f0`; `0x004FF416: movt r2, #0x4220` (= 40.0f); `0x004FF41E: mov r0, r5`; `0x004FF420: mov r1, r6`; `0x004FF422: mov r3, r4`; `0x004FF424: blx #0x4A6778`.
6. **Branch:**
   - `0x004FF428: cmp r0, #0`; `0x004FF42A: beq.w #0x4FECDE` -> **replan** (store the plan and run `DoPlanning`; `0x004FECDE..`).
   - else `0x004FF42E: movs r4, #2`; `0x004FF430: b #0x4FF3CA` -> cleanup/unlock and **return 2** (old plan reused, no replan). Return is `r4` (`0x004FF402: moveq r0, r4`).

The true branch (`impl+0xA1 != 0`) clears the old plan (`0x004FEBB4..0x004FEBD0`), sets `r4 = 0`, and falls into the same store+`DoPlanning` path (`0x004FECDA`).

### 4c. What `FindClosestPlanSegmentToPose` and `PlanIsSafe` test
**`FindClosestPlanSegmentToPose` 0x00856310** (`(env, plan, State_c const&, float& outDist, bool verbose)`): walks the plan's action list and, for each action, the action's `MotionPrimitive` intermediate poses; computes the Euclidean distance (in mm, `env+4` scale) from the given state to each plan state (`0x008563F2..0x008564A6` first loop; `0x008566xx` second loop over intermediate poses `0x008567xx`); returns the action index of the minimum and writes the minimum distance to `outDist` (`0x008569BA: *param_3 = fVar24`; `return local_a4`). An exact state match short-circuits with distance 0 (`0x0085639A..0x008563A0`). It tests **distance only**, no collision.

**`PlanIsSafe` 0x00850B78** (`(env, plan, float tolerance, int startIndex, State_c&, xythetaPlan& outPlan)`): if the plan is empty returns 0 (`0x00850B88..0x00850B92`). It reconstructs the state at `startIndex` by applying actions `0..startIndex-1` (`0x00850BF2..0x00850C18`), writes that state and the start pose into the out plan (`0x00850C1E..0x00850C5C`), then for each remaining action calls `ApplyAction` (`0x00850C90`) and compares the new collision penalty `fVar12` with the plan's stored per-action penalty `fVar13` at the action record:
- `0x00850CA2: vldr s0,[r2]` (stored penalty); `0x00850CAA: vadd.f64 d3,d1,d11` where `d11 = 0.5` (`0x00850C66`); `0x00850CAE: vcmpe.f64 d3,d2`; `0x00850CB6: bmi #0x850D76` -> on `stored + 0.5 < new` it prints `"Collision along plan action %lu (starting from %d) Penalty increased from %f to %f"` (`0x00850D76..0x00850D84`) and returns 0 (`0x00850D88: movs r0, #0`).
- otherwise it keeps updating the out plan and the start-distance bookkeeping (`0x00850CBE..0x00850D5E`), and returns 1 at the end (`0x00850D72: movs r0, #1`).
It tests **collision penalty along the remaining plan** (plus the `tolerance` distance bookkeeping), and returns 1 = safe.

### 4d. Reachability of `StartPlanning(false)`
- `StartPlanning` 0x004FEB44 has exactly one caller: the thunk `0x004A66E8` (index and `0x004FEB00` callee list; `0x004FEB22: blx #0x4A66E8`).
- The thunk's only caller is `LatticePlanner::ComputeNewPathIfNeeded` 0x004FEB00, which passes its own bool through unchanged.
- `LatticePlanner::ComputeNewPathIfNeeded` is a virtual (`_ZN4Anki5Cozmo14LatticePlanner22ComputeNewPathIfNeeded...` relocation at the vtable slot `0x101F194` = vptr+0xC). The only call sites of that interface slot in the decompiled binary are the three `ComputePath` methods, and all pass `1` (true):
  - `LatticePlanner::ComputePathHelper` `0x004FD330` (`movs r2, #1`);
  - `FaceAndApproachPlanner::ComputePath` `0x004F355C` (`(**(code**)(*(int*)this + 0xc))(this,param_1,1)`);
  - `MinimalAnglePlanner::ComputePath` `0x00503BC2` (`(**(code**)(*(int*)this + 0xc))(this,param_1,1)`).
- No direct call to `LatticePlannerImpl::StartPlanning` with `false` exists (no relocations or direct `bl` to it other than the thunk), and `ComputeNewPathIfNeeded` has no other caller in the binary.

**Conclusion.** The `StartPlanning(false)` branch (2.0/1.0 import padding, `FindClosestPlanSegmentToPose`, `PlanIsSafe`, return 2 on a safe old plan) is **not reachable from any caller recovered from the shipped engine**. The live path is `ComputePathHelper -> ComputeNewPathIfNeeded(pose, true) -> StartPlanning(pose, true)`. The residual uncertainty is only an indirect/virtual caller not present in the decompiled call graph; none was found.

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| R4-1 | `ComputePathHelper` calls vptr+0xC with `true` | `0x004FD326..0x004FD330` | M13-018 partial | EXACT_SOURCE |
| R4-2 | false: import with padding 1, find closest segment, force replan if dist >= 20 | `0x004FEC36..0x004FEC66` | M13-018 partial | EXACT_SOURCE |
| R4-3 | false: `PlanIsSafe` with 40.0; 0 -> replan, non-zero -> return 2 | `0x004FF410..0x004FF430`; `0x004FF42A`; `0x004FF42E` | M13-018 partial | EXACT_SOURCE |
| R4-4 | `FindClosestPlanSegmentToPose` returns closest index + distance | `0x00856310` (return `0x008569BA`) | NEW detail | EXACT_SOURCE |
| R4-5 | `PlanIsSafe` returns 0 on a collision penalty rise > 0.5, else 1 | `0x00850CA2..0x00850CB6`; `0x00850D88`; `0x00850D72` | NEW detail | EXACT_SOURCE |
| R4-6 | false branch unreachable from any recovered caller; all three `ComputePath` callers pass true | `0x004FEB00`; `0x004FD330`; `0x004F355C`; `0x00503BC2` | M13-018 partial | EXACT_SOURCE (residual UNKNOWN below) |

---

## Records contradicted or too weak

- **M13-018** (current title "LatticePlannerImpl's planning entry and worker, the Replan argument, the heuristic and the result codes", status IMPLEMENTATION_GAP). Its evidence already states the false branch "reuses a safe old plan (FindClosestPlanSegmentToPose 0x004FEC50; PlanIsSafe 0x004FF424 -> return 2 at 0x004FF42E)" and that `PopulateReverseMotionPrims` "negates the end-pose x/y and rewrites the theta byte". It is **not contradicted**, but its `PopulateReverseMotionPrims` clause is **partial**: it does not say what the theta byte is rewritten to (the forward start index, not a modular offset), and it does not record that the `Path`, intermediate poses and bbox are copied unchanged. Because M13-018's consumer is the goal-side expansion, that omission can mislead. Recommend adding R1-2/R1-3/R1-4 to M13-018 or a new sibling record before M13-018 is settled.
- **M13-003** (status IMPLEMENTATION_GAP) cites `IntermediatePosition+0x10` as the soft-collision penalty multiplier but does not define it. R2-1..R2-3 supply the definition. Not contradicted.
- **M13-010** (status IMPLEMENTATION_GAP) covers `ShouldPlayEightiesMusic`, `MoodScoreHelper` and `EvaluateEmotionScore` at the "scores ... through ..." level but does not record the mood-scorer JSON keys, the `EmotionScorer`/graph schema, the `trackDelta` branch or the mean/zero rule. R3-1..R3-6 supply them. Not contradicted.
- **M13-001** (status IMPLEMENTATION_GAP) already cites the `PopulateReverseMotionPrims` call at `0x008524AC`; that call is confirmed (`0x008524AC: blx #0x4CE8A0`). Not contradicted.

## Open questions for the manager

1. Should the `PopulateReverseMotionPrims` theta-byte fact and the shared-path/poses/bbox fact be added to M13-018 or split into a NEW record? The current M13-018 wording is not wrong but is incomplete for the goal-side expansion it feeds.
2. The three M13 records (`M13-003`, `M13-010`, `M13-018`) are all IMPLEMENTATION_GAP; the new facts are extracted but not yet in the manifest. The manager should fold R1..R4 in before any M13 approval.
3. Residual UNKNOWN (low confidence): whether an indirect caller outside the decompiled call graph passes `false` to `LatticePlanner::ComputeNewPathIfNeeded`. No such caller was found; a full scan of the vtable slot's indirect call sites was not performed.

**Correction C-BM13b (verifier findings, 2026-09-28):** M13-004's cost formula is branched, not a sum: an arc primitive adds the arc term and not the turn term, a point turn adds the turn term. M13-015 marks SetFailedToUse(obj,3) for a category-3 result as well as a category-4 result with the retry used. Both are corrected in the records above.

## Correction A6 (manager, 2026-10-02)

The 2026-09-29 audit found defects in 13 of this subsystem's settled records and handed them to R-VIS, which never demoted them. Codex's re-audit (`re-analysis/research/20261001-reaudit-M7-M8-M9-M13.md`) reproduced them, and the manager re-checked the +0x34A flag in the binary. M13-002, 003, 004, 005, 008, 009, 011, 012, 013, 015, 016, 017 and 018 go back to IMPLEMENTATION_GAP, each with its defect in `unresolved`. M13-001's test reference is corrected to the method's current name.


## Cited B-M1M2 lifetime/sleep split (2026-10-05)

Authorized correction A2 in inventory/M1-transport.md; manager-adopted research/20261005-B-M1M2-blockers-extraction.md T1–T9/ownership table. New higher-layer obligations only, not builds or settlements.

| Record | Boundary and citation | Remaining work |
| --- | --- | --- |
| M13-029 — Path/planner abort and destruction | 0x00649100..0x006491BA; 0x00649220..0x0064929E; 0x00649002; 0x0051127E..0x00511288; Path destructor calls Abort again, including another ClearPath send attempt. Pose vector destruction does not invoke callbacks. Planner virtual cancellation descendants UNKNOWN. | MISSING: Path destructor calls Abort again, including another ClearPath send attempt. Pose vector destruction does not invoke callbacks. Planner virtual cancellation descendants UNKNOWN. Bind the native owner to its actual higher-layer production component and recover/check the complete recipient path before implementation or settlement. |
