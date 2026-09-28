# X2 — M13-navigation extraction (read-only)

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
