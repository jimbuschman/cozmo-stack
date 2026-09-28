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
| M13-007 | IMPLEMENTATION_GAP | The stack tolerance is 30 mm when building a stack and 15 mm everywhere else. `BlockWorld::FindObjectOnTopOrUnderneathHelper(obj, float tolerance, filter, bool)` 0x0062601C takes the tolerance as its second argument (r2 captured at 0x00626038 into the lambda at 0x006260CE/0x006260D8), reads the object's height through `GetDimInParentFrame<(char)90>` (0x00626070), adds ±half of it to the parent Z (0x0062607E..0x006260A0) and calls `FindLocatedObjectHelper` (0x0062610E). Callers: `StackOfCubes::BuildTallestStackForObject` passes **30** (0x41F00000 at 0x0061928C and 0x00619344); `BlockWorld::UpdatePoseOfStackedObjects` 0x00621908, `DockingComponent::CanInteractWithObjectHelper` 0x0063C728 and `CarryingComponent::SetObjectAsAttachedToLift` 0x00632F0C pass **15** (0x41700000). | N26, G3 |
| M13-008 | IMPLEMENTATION_GAP | The mount sequence and the reverse onto the charger. `MountChargerAction::ConfigureTurnAndMountAction` 0x0054E500: a `TurnInPlaceAction` at the atan2 of the vector to the marker (0x0054E52E/0x0054E536) with `SetMaxSpeed(0x3FDF66F3 = 1.745329 rad/s = 100 deg/s)` (0x0054E550/0x0054E55A) and `SetAccel(0x40A78D36 = 5.235988 rad/s^2)` (0x0054E55E/0x0054E568); if the lift is below 45.0, `MoveLiftToHeightAction(45, 5, 0)` (0x0054E58A..0x0054E5C6); then `DriveStraightAction(-120 mm, 30 mm/s, false)` whose vtable is overwritten with `BackupOntoChargerAction`'s (0x0054E5FC..0x0054E618). `BackupOntoChargerAction::CheckIfDone` 0x0054E7A8: on the contacts (robot+0x338) `Robot::SetPoseOnCharger()` and return 0 (0x0054E7B0..0x0054E7BE); not on contacts, `GetPitchAngle` compared with -0.261799 rad (0xBE860A92 at 0x0054E7CC), **pitch below returns 0x0400000A** (0x0054E7DE `adds r4,#4` after 0x04000006), fall-through returns **0x04000006** when `DriveStraightAction::CheckIfDone` returns 0 (0x0054E7E4..0x0054E7EC). `MountChargerAction::CheckIfDone` 0x0054E2D0 reaches the pi/2 comparison (0x3FC90FDB at 0x0054E374) only when the turn-and-mount sub-action FAILED (0x0054E31A skips success 0 and running 0x1000000); inside the window the failure stands, outside it `ConfigureDriveForRetryAction` 0x0054E72C runs `DriveStraightAction(120, 100, false)` returning **0x04000006** (0x0054E3E8/0x0054E742/0x0054E748). An align failure ends the action: the turn-and-mount is configured only after the align returns success (0x0054E2FA). | N16, N17, N18, G8 |
| M13-009 | IMPLEMENTATION_GAP | The charger's dimensions, its docked pose and its one pre-dock pose. `Charger::Charger` 0x004E9B6C stores **96.0/80.0/31.0** at +0xF0..+0xF8 (0x004E9B98/0x004E9BB4/0x004E9BB0/0x004E9BB8) and adds one marker: id **2** (0x004E9C16), pose angle **-pi/2** about Z (0xBFC90FDB, 0x004E9BBC) at **(86, 0, 22)** (0x004E9BD6/0x004E9BE2), size `Point2` with **x = 27.0 at sp+0x14** and **y = 20.0 at sp+0x18**, the pointer passed to `AddMarker` being sp+0x14 (0x004E9C28/0x004E9C30/0x004E9C36/0x004E9C38). `GetRobotDockedPose` 0x004EA1A0 = `Pose3d(Radians(pi), Z_AXIS, (30, 0, 0))` on the charger's pose (0x004EA1AC/0x004EA1C6/0x004EA202). `GeneratePreActionPoses` 0x004E9FB0 emits one pose for action types 0 and 1 only (`cmp r5,#1`/`bhi` at 0x004E9FD4/0x004E9FD6): `Pose3d(Radians(p.angle + pi/2), Z_AXIS, (p.x, -p.y, -15.5))` parented to the marker (0x004E9FE4/0x004E9FF2/0x004EA012/0x004EA018/0x004EA01E); `p` is the file-static Pose2d at 0x01059148 initialised `(Radians(0), 0.0, 250.0)` (0x004D6BC4, 0x004D6BD6). | N13, N14, N15, G9 |
| M13-010 | IMPLEMENTATION_GAP | The workouts run in file order and the last one repeats. `WorkoutComponent::GetCurrentWorkout` 0x00573DE8 returns the pointer at +0xC (0x00573DE8/0x00573DF0). `CompleteCurrentWorkout` 0x00573DEC triggers the workout's emotion event (`MoodManager::TriggerEmotionEvent` 0x00573E1C) and advances the pointer by one 0x40-byte entry unless it is the last (`subs r1,#0x40` 0x00573E24; `addne r0,#0x40` 0x00573E28/0x00573E2A). `ShouldPlayEightiesMusic` 0x00573E30 caches its answer at +0x11 and otherwise scores the current workout. | N27 |
| M13-011 | IMPLEMENTATION_GAP | The three path segment messages are a `Planning::PathSegment` copied field for field. `PathDolerOuter::Dole` 0x00507E4C switches on the segment type at `PathSegment+0` (1 line, 2 arc, 3 point turn; 0x00507F40/0x00507F44/0x00507F48) and copies words at +4, +8, +0xC, +0x10 (arc adds +0x14), then the speed profile at +0x1C, +0x20, +0x24; a point turn also copies the byte at +0x14 (line 0x00507FB6..0x00507FD2; arc 0x00508008..0x00508028; point turn 0x00507F5E..0x00507F7E). | N11, N12 |
| M13-012 | IMPLEMENTATION_GAP | The mount raises the lift to 45 mm when it is below it, not when it is above. `ConfigureTurnAndMountAction` reads `Robot::GetLiftHeight` (0x0054E58A) and branches past the lift move when the lift is at or above 45.0 (`vldr s2,[pc,#0x14c]` = 0x42340000; `bpl` at 0x0054E59E); otherwise `MoveLiftToHeightAction(45, 5, 0)` (0x0054E5B2..0x0054E5C6). | N16, G8 |
| M13-013 | IMPLEMENTATION_GAP | `DriveOffChargerContactsAction`. Constructor 0x00558228: `DriveStraightAction(10 mm, 20 mm/s, false)` (0x00558232/0x00558236/0x0055823E), +0x44 = 7, and **in SDK mode only** `SetTracksToLock(0)` (0x0055827C `IsInSdkMode`; 0x00558286/0x00558288) — the SDK clear is in the constructor, not `Init`. `Init` 0x005582D0 copies the robot's on-contacts flag robot+0x338 into action+0x8B (0x005582D2/0x005582D6) and returns 0 when not on contacts. `CheckIfDone` 0x005582E4 retries while the drive is still running and fails **0x04000009** if the robot is still on the contacts (0x00558344). | N19, G7a |
| M13-014 | IMPLEMENTATION_GAP | Knock over a stack: `BehaviorKnockOverCubes` (body 0x005C2EA0..0x005C3E2A). The behaviour-changing path is read and the record is raised from EQUIVALENT_IMPLEMENTATION to the engine's own flow (gap pass 2, G5). `IsRunnableInternal` 0x005C314C needs `StackOfCubes::GetStackHeight() >= +0x124` (minimumStackHeight, default 3). `InitInternal` 0x005C31A2 runs the reach unless +0xD9 (alwaysStreamline) or +0xD8 is set. `TransitionToReachingForBlock` 0x005C3254: `TurnTowardsObjectAction(max pi)`; if the block's x + 10 > 85.0 a `DriveStraightAction(x - 85, 60)`; a `TriggerLiftSafeAnimationAction(+0x150)`; then `TransitionToKnockingOverStack` regardless of result. `TransitionToKnockingOverStack` 0x005C34A8: `TurnTowardsObjectAction(max pi)`, `DriveAndFlipBlockAction(..., maxTurn = pi/2 on the first attempt, 0.0 once +0x140 > 0, at 0x005C36BC/0x005C36C0)`, `WaitAction(0.5)`; the callback 0x005C3DCE writes the block to `AIWhiteboard+0x70` on `NoPreActionPoses` (0x03000010), re-runs the knock-over while +0x140 <= 1 on a Retry result and otherwise blind-flips, and increments +0x140 either way; success goes to `TransitionToPlayingReaction` 0x005C3908, which selects the success trigger +0x15C or failure trigger +0x160 by the tipped-object set size at +0x14C. `IDriveToInteractWithObject` 0x0055B1F4 adds **two** actions when maxTurn > 0 (0x0055B37C..0x0055B392): a `TurnTowardsLastFacePoseAction` (vtable overwritten from `TurnTowardsFaceAction` at 0x0055B3C4..0x0055B3D8) **and** a `TurnTowardsObjectAction` (0x0055B42E/0x0055B43C), both with failure ignored. The trailing float 20.0 is not read by `DriveAndFlipBlockAction`'s ctor (0x0055E208). | N22, G5a..G5g |
| M13-015 | IMPLEMENTATION_GAP | Pop a wheelie: one retry, the realign or retry animation, and what a failure marks. `TransitionToPerformingAction(Robot&, bool)` 0x005C7758 bumps the retry count at +0x12C when called as a retry (0x005C777E) and zeroes it otherwise (0x005C780C). The completion lambda 0x005C7CBC splits on `result >> 24`: success sets +0x128 = -1, plays `SuccessfulWheelie` 0x21C and reports objective 0x16 and the needs action; a Retry-category result calls `SetupRetryAction` only while the count is at most 0 (one retry), otherwise `AIWhiteboard::SetFailedToUse(obj, 3)`. `SetupRetryAction` 0x005C79D0 plays `PopAWheelieRealign` 0x18D for exactly `0x04000001` (0x005C79F8/0x005C79FE) and `PopAWheelieRetry` 0x18E otherwise (0x005C7A10/0x005C7A32). `ResetBehavior` 0x005C76B0 sends `EnableStopOnCliff(true)` (0x005C76E4). | N24 |
| M13-016 | IMPLEMENTATION_GAP | `AlignWithObjectAction`'s alignment-type table and pre-action type. `cmp r6,#3`/`bhi` 0x005533CC/0x005533E4; `tbb [pc,r6]` 0x005533E6 with table base 0x005533EA = `02 05 09 0c` (TBB scales by 2): **type 0 → 0x005533EE `vmov.f32 s16,#6.0` (distance 6.0)**; **type 1 → 0x005533F4 flag +0xBB = 2 (distance stays 0.0)**; **type 2 → 0x005533FC `vmov.f32 s16,#-15.0`**; **type 3 → 0x00553402 `vadd.f32 s16,s0,s2` (argument + -27.0)**. The distance is clamped to 0.0 when it is below **-16.000009536743164** (0xC1800005 at 0x00553480; `vcmpe`/`it mi`/`vmovmi` at 0x0055341C..0x00553426). `GetPreActionTypeFromAlignmentType` 0x005532B8: table at 0x00553360 maps type 0→1, 1→0, 2→1, 3→1; an invalid type logs and returns 1 (0x005532CC/0x00553320). | N23, G6b, G6c |
| M13-017 | IMPLEMENTATION_GAP | `BehaviorDriveOffCharger`. Constructor 0x005C0980 stores **96.0 + `<json extraDistanceToDrive_mm>`** at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6). `IsRunnableInternal` 0x005C0B10 returns robot+0x34A. `InitInternal` 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions unless robot+0x355 is set. `TransitionToDrivingForward` 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08). `UpdateInternal` 0x005C0DA8: while still on the contacts and robot+0x355 is set it calls `StopActing` (0x005C0DC4) and waits; once off the contacts (robot+0x34A == 0) it records the time in the whiteboard and returns 2 (0x005C0DF4..0x005C0E0A). | N20, G7b |
| M13-018 | IMPLEMENTATION_GAP | `LatticePlannerImpl`'s planning entry and worker. `StartPlanning` 0x004FEB44 stores its bool argument at impl+0xA1 (0x004FEB56/0x004FEB7E) and calls `ImportBlockworldObstaclesIfNeeded` with the bool **hard-coded 1** (0x004FEC38/0x004FEC3A). `DoPlanning` 0x00500090 sets status 1 (0x00500098), sleeps in chunks of **min(remaining, 10) ms** up to impl+0x108 ms, checking the abort flag each iteration (0x005000A2..0x005000EA; `cmp r0,#0xa`/`movge r4,#0xa` at 0x005000C8/0x005000CE), then calls `Replan(0x01C9C380, abortFlag)` (0x005000F2/0x005000FA/0x00500102). `0x01C9C380 = 30,000,000` is the **maximum number of state expansions**: `xythetaPlannerImpl::ComputePath` 0x008586A0 compares the count and on overflow warns "exceeded max expansions of %u, stopping" and returns 0 (0x008588C6/0x008588CA/0x00858A96). `Replan` returns 0 on failure, 1 on success. `DoPlanning` returns **0 when Replan == 0**, **3 when Replan != 0 and the plan's segment list is empty** (0x00500202/0x00500212) and **2 on success** (0x0050058E/0x00500590). | N8, N9, N10, G4a..G4c |
| M13-019 | IMPLEMENTATION_GAP | `xythetaEnvironment::Init` hard-codes 16 headings. After `ReadMotionPrimitives` succeeds (0x008528AE), it **overwrites env+8 with 0x10** (0x008528B6/0x008528BA), resizes the per-theta obstacle table at env+0x44 to 16 (0x008528C0) and sets env+0xC = 2π/16 and env+0x10 = 1/(2π/16) (0x008528C4..0x008528E6). The planner's heading count is therefore 16 regardless of the JSON `num_angles` (which the asset also sets to 16, so they agree). | G1f |

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