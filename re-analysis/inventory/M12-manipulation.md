# M12 manipulation inventory (docking, carrying and pre-action poses)

**State:** approved by the manager on 2026-09-28 under the operator's standing authorisation, and frozen with `python re-analysis/tools/fidelity.py --approve M12-manipulation`. The one forced choice is recorded as policy M12-014 (SD2).

## Where this comes from

- **Source:** `libcozmoEngine.so` 3.4.0-1204, sha256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Unity C# (authority 2) and the shipped assets (authority 3) were used only as supporting evidence.
- **Read-only extractor passes:**
  - **X1 pass** (Appendix A): M1..M18 (the production path), the existing-record review, and N1..N8 (new steps).
  - **Gap pass 1** (Appendix B): G1..G9 — the per-type distance mapping, the M12-003 scope, the real path-packing citations, the M12-004 entry, the report's citation fixes, the N8 caller correction, the M12-010 re-read, the mount path, and the 15.0 ownership.
  - **Gap pass 2** (Appendix C): F1..F3 — the PathMotionProfile consumer, the type-2 zero citation, and the `PreActionPose+0x18` value consumer.
  - **Gap pass 3** (Appendix D): H1..H2 — the block face-def records and their per-face masks, and the `ComputePreActionPoseDistThreshold` floor question.
- **Verification.** Two read-only verifier passes opened every status-changing row and a sample of the rest. The first FAILed seven rows (M3, M7, M12-002's path-packing range, M13, M14, N8 and the M12-014 write range); gap pass 1 corrected them. The second FAILed one (G3.8, the PathMotionProfile consumer) plus two precision points; gap pass 2 corrected them. No failed row is kept.
- **Interfaces used and not redone:** `M11-vision.md` (M11-006, the 20° pose-confirm clamp), `M13-navigation.md` (M13-007 the 15.0 stack tolerance; M13-008 the charger mount), `M2-protocol.md`, `M4-control.md`.
- **The C# was never evidence.**

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M12-001 | IMPLEMENTATION_GAP | Pre-action pose types, their per-type ctor distances, the distance threshold, and what reads the distance. <br>- **Dispatch:** `Block::GeneratePreActionPoses` 0x004E5808 clears the output, reads the block size, and `tbh` dispatches on `actionType` 0..5 (0x004E595E; table 0x004E5962); >5 produces nothing. **Correction C-E1:** the per-face records are `{FaceName u32 @0, MarkerType code u32 @4, size 25.0f @8, maskForTypes0And5 u8 @0xC, maskForType4 u8 @0xD, pad @0xE}`, copied verbatim from rodata (LIGHTCUBE1 0x00C45C40, LIGHTCUBE2 0x00C45CA0, LIGHTCUBE3 0x00C45D00, GHOST 0x00C45D60); the vector order is `[Front, Back, Left, Right, Top, Bottom]` and the inner `sb = 0..3` loop (Z rotations 0, pi/2, pi, -pi/2; table 0x0105B0AC..0x0105B0E8, stride 0x14) tests `(1<<sb) & mask` — +0xC for types 0 and 5, +0xD for type 4, ungated for types 1 and 2, none for type 3. Masks: LIGHTCUBE1/2/3 +0xC = 0x05 for FaceNames 0..3, 0x00 for FaceName 4, 0x0F for FaceName 5; +0xD = 0x0F for all; GHOST 0x0F/0x0F. <br>- **Per type (the 4th ctor arg = the distance, `PreActionPose+0x18`):** corrected in C-E7 (the earlier "marker + angle" and "marker-relative" text was refuted by the source). Each branch builds `Pose3d(angle, Z_AXIS, translation)` with the **marker's pose as parent** and then rotates it about **Y** by `sb·pi/2` (`Transform3d::RotateBy`): 0 Docking 75.0 (0x004E5A36), angle pi/2, translation {0, -65, -size.z/2} (Pose2d(0,(0,65)) at 0x01059078, built 0x004D6AB4); 1 PlaceRelative 40.0 (0x004E5ADA), angle pi/2, {0, -100, -size.z/2} (0x004E5A70); 2 PlaceOnGround 0 (0x004E5B5A/0x004E5B80), angle pi/2, {0, -49, -size.z/2} (0x004E5B0C); 3 Entry none (0x004E5DA2); 4 Rolling 75.0 (0x004E5C50), angle pi/2, {0, -65, -size.z/2}; 5 Flipping 0 (0x004E5D04/0x004E5D28), angle 3pi/4, {dimX/2 + 56.5771, -56.5771, -size.z/2} (0x004E5CB8 = 0xC2624EEF). `-size.z/2` is -22 for a cube (`0x004E5938 vneg`; the precompute `s16 = -size[8]/2`, `s18 = size[4]/2 + 56.5771`). The `PreActionPose` ctor 0x0050DCFC re-roots the passed pose to the marker's parent (`0x0050DDB2 passedPose.GetWithRespectTo(marker.parent)`), so the stored pose is `marker.local ∘ builtPose`. <br>- **Ctor:** `PreActionPose` 0x0050DCFC stores the float at +0x18 (0x0050DD46); `SetHeightTolerance` 0x0050DAA4 writes +0x14 = max\|translation\|/sqrt(3) (0x0050DB06; 0x3F13CD3A at 0x0050DB10), so +0x14 and +0x18 differ. The second ctor 0x0050DB14. <br>- **The +0x18 value consumer:** `ActionableObject::GetCurrentPreActionPoses` 0x004DF850 reads element+0x18 as a value (0x004DF9DE `vldr s28,[sl,#0x18]`; arithmetic 0x004DFA26 / 0x004DFA78 / 0x004DFCD8 / 0x004DFCDC) with stride 0x1C (0x004DFDC4). **The composition (correction C-E1):** the world pose is built by `PreActionPose(PreActionPose const&, Pose3d const&, float, float)` 0x0050DF00 (PLT 0x4A4144): `dest.pose = objectPose * Pose3d(src.pose.rotation, unit(src.pose.translation) * (|src.pose.translation| + b))`, `dest+0x18 = a`; `GetCurrentPreActionPoses` calls it at 0x004DF99A with `a = element+0x18` and `b = preDockPoseOffset_mm` (0x00550984), and when that offset is 0 it computes `b = min(dist2, element+0x18)` at 0x004DF9B8..0x004DFC16 and calls the ctor again at 0x004DFBE6. The stores at 0x004DFCFC..0x004DFD00 are the `param_8==1` visualization path, not the returned pose. <br>- **`ComputePreActionPoseDistThreshold` 0x00550098 (382 bytes):** the distance is the **3-D** norm of the relative transform translation (+0x20/+0x24/+0x28; 0x00550102.., `vsqrt` 0x00550122) times `sin(tol)` (0x00550140 `sinf`, 0x0055014E `vmul`). Out[0] = 2·dist·sin (0x00550164/0x005501A4), out[1] = dist·sin (0x005501A8). The only tolerance branch is `angleTolerance > Radians(0)` (0x005500BC; `operator>` 0x0084CC90 with the ~1e-5 rad epsilon 0x3727C5AC); false, or a `GetWithRespectTo` failure, writes the -1.0f sentinel pair (0x005501AE, 0x005501C0). **There is no floor.** | X1 M2/M3, N7; gap1 G1; gap2 F2/F3; gap3 H2 |
| M12-002 | IMPLEMENTATION_GAP | Path packing, ExecutePath, ClearPath and the path-motion-profile defaults. <br>- **Packing:** `PathDolerOuter::Dole` 0x00507E4C switches on the `PathSegment` type (0x00507F40/44/48) and copies the fields straight in: line send 0x00507FE0 `blx 0x4A7234`; arc 0x00508036 `blx 0x4A7240`; point turn 0x00507F8E `blx 0x4A7228`. <br>- **ExecutePath:** `PathComponent::ExecutePath` 0x0064A340 builds `{pathID u16, manualSpeed bool}` (0x0064A426 `ldrh r0,[r5,#-0x12]` = `[this+0x42]`, 0x0064A430; 0x0064A42A) and sends it (0x0064A436 `blx 0x4B9D8C`, 0x0064A442 `blx 0x4A5368`). <br>- **ClearPath:** `PathComponent::ClearPath` 0x00649220 writes a literal zero into its one u16 (0x00649268 `movs r0,#0`, 0x0064926A `strh.w r0,[sp]`); path ids are correlated against `_lastSentPathID` (0x00649234/0x0064923A). <br>- **Profile:** `SpeedChooser::GetPathMotionProfile` 0x0053B798 copies the 11-word rodata table at 0x00C535E0 (100, 200, 500, 2, 10, 10, 60, 200, 500, 80, 0). <br>- **Wiring:** `DriveToPoseAction::Init` 0x0055A86C is the only caller of `PathComponent::StartDrivingToPose` (0x0055A998 `blx 0x4ABEE4`); that function is the only caller of `GetPathMotionProfile` (0x0064ADE0) and copies the profile into `[PathComponent+0x4C]` when no custom profile is set (0x0064ADD2/0x0064ADE4); `PathComponent::UpdatePlanning` 0x006495E0 tail-calls `HandlePlanComplete` on planner status 2 (0x006496DE veneer to 0x4B9D2C); `HandlePlanComplete` reads `[this+0x4C]` and passes it to `IPathPlanner::GetCompletePath` (0x00649F76/0x00649F82), whose `Path` reaches `ExecutePath` (0x0064A0A4). `DriveToObjectAction` never reaches `StartDrivingToPose`; its constants are its own ctor defaults (0x0055850C). | X1 M12; gap1 G3; gap2 F1 |
| M12-003 | IMPLEMENTATION_GAP | The dock-message order. `IDockAction::CheckIfDone` 0x005521AC is the only caller of `DockingComponent::DockWithObject` 0x0063BA44 (0x005522AE `blx 0x4AB9F8`), which is the only caller of the builder 0x0063BD50; the builder sends via `EngineToRobot(DockWithObject&&)` 0x0063BDB8 (`blx 0x4B94BC`) and `SendMessage` 0x0063BDC4 (`blx 0x4A5368`). The field layout it sends is M12-005. | X1 M5, N4; gap1 G2 |
| M12-004 | IMPLEMENTATION_GAP | The dock retry drops the pose it just failed from. `IBehavior::UseSecondClosestPreActionPose` **starts at 0x005BEE56**: it calls `DriveToObjectAction::GetPossiblePoses` (0x005BEE66), requires at least two poses (`(end-begin)/0xC`, 0x005BEE7C `asrs r0,r0,#2` / 0x005BEE80 `cmp r0,#2`), then gets the `IDockAction` from `this+0x2C` (0x005BEE88) and calls `IDockAction::RemoveMatchingPredockPose` (0x005BEE90; function 0x00551418), clearing the out-param bool when it returns 1 (0x005BEE98/0x005BEE9C). | X1 M13; gap1 G4 |
| M12-005 | IMPLEMENTATION_GAP | Every field of `DockWithObject`. The builder 0x0063BD50 is the only place the 21 bytes are constructed. <br>- word 0 = 0 (0x0063BB34 `movs r4,#0` / 0x0063BB7E `str r4,[sp,#0x1c]`); <br>- words 1..3 = speed/accel/decel from `IDockAction+0xAC/+0xB0/+0xB4`, defaults 60 (0x42700000), 200 (0x43480000), 500 (0x43FA0000) set in the ctor 0x005502D8; <br>- byte 4 = the DockAction at +0x80; byte 5 = +0x95; byte 6 = +0xBA (0); byte 7 = +0xBB (0; `PickupObjectAction` 0x005536EA writes 2, `RollObjectAction` 0x005565DC writes 5); byte 8 = +0xC1 (0; `PickupObjectAction` 0x005536F8 writes 1). The order it is sent through is M12-003. | X1 M5; gap1 G2 |
| M12-006 | IMPLEMENTATION_GAP | Letting go of a carried object leaves it Dirty where the lift left it. `CarryingComponent::SetCarriedObjectAsUnattached(bool)` 0x006333D4 takes the object pose with respect to the robot and hands it to `ObjectPoseConfirmer::AddRobotRelativeObservation(object, pose, PoseState 2)` (0x00633458); when the bool is set, `BlockWorld::DeleteLocatedObjects` also runs (0x00633930). `BehaviorPutDownBlock` passes false (0x005C84CE); `PickupObjectAction::Verify` passes true (0x00553CAA, 0x00553D2A). **Correction C-E2:** the production release is `HandlePickAndPlaceResult` 0x00533780, and it runs only on `blockStatus == 1` **and** `PickAndPlaceResult.success != 0`, with bool false (0x00533898/0x0053389A, 0x005338A2); the attach is `SetDockObjectAsAttachedToLift` 0x00533850, only on `blockStatus == 2` and `success != 0` (0x00533848/0x0053384A). | X1 M10 |
| M12-007 | IMPLEMENTATION_GAP | The two timed checks a pick-up is verified against. `PickupObjectAction::Verify` 0x00553BE0 stamps its first call at +0x10C (0x00553BF8) and runs two checks, both ending in `SetCarriedObjectAsUnattached(true)`: at 0x00553C98, if the object still reports itself moving (virtual `IsMoving`, 0x00553C8E) and more than +0x118 = 500 ms have passed, the pick-up failed; otherwise at 0x00553D0A the stamp must not be later than the object last-observed time plus a dock-action timeout at +0x80: +0x11C = 500 ms low, +0x120 = 2000 ms high (selected at 0x00553D0E/0x00553D12/0x00553D18; ctor sets them at 0x005536CC). | X1 M8 |
| M12-008 | IMPLEMENTATION_GAP | The pose chain that holds a carried object. `Robot::Robot` 0x0050FBF0 builds the lift pivot at robot+0x2E4 (child of the origin at (-41, 0, 45); 0xC2240000 at 0x0050FFE0, 0x42340000) and the lift pose at robot+0x2F0 (child of the pivot at (66, 0, 0); 0x42840000 at 0x00510038). `Robot::ComputeLiftPose` 0x005151AC swings the arm about Y and rotates the orientation back. `CarryingComponent::SetObjectAsAttachedToLift` 0x00632CC4 places the object with respect to the lift pose at (\|dockMarkerOffset\| + 4, 0, -12.5): norm at 0x00632E42..0x00632E74, 4.0 at 0x00632E7C, 0xC1480000 = -12.5 at 0x00632E82, sum at 0x00632E8C. The attach side is the counterpart of the release side (M12-006). The 15.0 stack tolerance this function passes to `FindObjectOnTopOrUnderneathHelper` is owned by M13-007. **Correction C-R1:** SetObjectAsAttachedToLift 0x00632CC4 is a nine-step flow (preconditions and returns, marker choice, the lift-relative transform with the **3-D** marker-translation length + 4.0 / 0 / -12.5, the 15.0 stacked-object search, `AddObjectRelativeObservation`, `SetCarryingObject`, the BlockConfigurationManager dirty flag, `AddLiftRelativeObservation` with PoseState Known); the record's own authority now lists each step with its address. The lift pose's per-tick update is M12-027. | X1 M9, N5; gap1 G9 |
| M12-009 | IMPLEMENTATION_GAP | The dock helper allows two attempts. `PickupBlockHelper::RespondToPickupResult` 0x005B8050 reads the attempt count at helper+0x108 (0x005B8192) and takes the retry branch only while it is 1 or less (`cmp r0,#1` 0x005B8196 / `bls` 0x005B8198); the log literal 2 is at 0x005B814A. The entry point `PickupBlockHelper::StartPickupAction` is 0x005B7B48; the count is zeroed in the constructor and Init. | X1 M16, N6 |
| M12-010 | IMPLEMENTATION_GAP | The search-for-block pattern, built. `SearchForNearbyObjectAction` ctor 0x005469C0 writes the defaults 0.8 (0x3F4CCCCD) / 1.2 s (0x3F99999A) and 0.261799 (0x3E860A92) / 0.349066 (0x3EB2B8C2) rad, storing distance/speed/head angle at +0x14C/+0x150/+0x154. `Init` 0x00546C14 draws three waits, two angles and a coin against 0.5 (0x00546CCE) and appends WaitAction; CompoundActionParallel(DriveStraightAction(distance, speed), MoveHeadToAngleAction(headAngle, tol 0.0349066)); WaitAction; TurnInPlaceAction(sign·a1, tol 0.0698132); WaitAction; TurnInPlaceAction(-sign·(a1+a2)); WaitAction (0x00546DD2..0x00546F4C). The speed 100.0 selects the default-speed ctor (0x00546DDE/0x00546E1E vs 0x00546E34). `SearchForBlockHelper::SearchForBlock` 0x005BAF24 dispatches on +0x10C: state 0 (0x005BAF9A) = SearchForNearbyObjectAction(target, -20, 100, -0.0872665) then TurnTowardsObjectAction(target, pi); state 1 (0x005BB1AA) = DriveStraightAction(-20, 20), two TurnInPlaceAction(+0.785398), the nearby search, then a second DriveStraightAction, TurnInPlaceAction(-3pi/4, 0xC016CBE4), TurnInPlaceAction(-0.785398), and a second nearby search (0x005BB2DA..0x005BB39A); state 2 (0x005BB066) is the mirror with -0.785398. **Correction C-E3:** state 0's execution order is `TurnTowardsObjectAction(target, pi)` **then** the nearby search (the turn is added at 0x005BB024 before the search at 0x005BB03E, `AddAction` 0x0054ED90 appends, and `UpdateInternal` 0x0054F70C runs front-to-back); the turn is added only when the target id is set (0x005BAFDA), the search unconditionally. State 2 is a **two-iteration loop** (r8=-1 at 0x005BB078, 0x005BB16E add r8,#1, 0x005BB176 blt) of DriveStraight(-20,20), TurnInPlace(-0.785398), TurnInPlace(-0.785398), nearby search — no state-1 tail, and it does not update `[this+0x10C]`. `SearchFinishedWithoutInterruption` 0x005BB478 requires object+0x24 == 1 (PoseState Known; 0x005BB49E/0x005BB4A2). The two production callers are `DriveToHelper::RespondToDriveResult` (call at 0x005B5D68) and `PickupBlockHelper::RespondToPickupResult` (call at 0x005B83B6). | X1 M18; gap1 G7 |
| M12-011 | IMPLEMENTATION_GAP | A drive that ends outside the pre-action pose tolerance reports `DidNotReachPreActionPose`. `DriveToObjectAction::CheckIfDone` 0x00559880 forms 0x04000001 (`movw r8,#1` 0x005598E2 + `movt r8,#0x400` 0x005598E6), compares the distance from the pre-action pose against the tolerance at +0x84, and on the failing side moves it into the result (0x005599FE); it returns 0x03000004 BadObject when the object is not located (`movs r4,#4` 0x005598B6 + `movt r4,#0x300` 0x005598BA). This function does **not** call `ComputePreActionPoseDistThreshold` (M12-020). **Correction C-R2:** `DriveToObjectAction::CheckIfDone` 0x00559880 reaches its tolerance compare only for ActionType 6 (the 4-arg ctor, reachable only from an external GotoObject). The production path calls the `std::function` at +0x150 with an in-position flag (flag 0 -> 0x04000001). The record described only the type-6 path. | X1 M14; gap1 G5/G6 |
| M12-012 | IMPLEMENTATION_GAP | What makes a cube a valid bottom to stack on: resting flat within ten degrees. `DockingComponent::CanStackOnTopOfObject` 0x0063C5C4 = `CanInteractWithObjectHelper` 0x0063C654 then `!IsPoseTooHigh(pose, 1.0, 15.0, 0.5)` (1.0 at 0x0063C614, 15.0 at 0x0063C60E, 0.5 at 0x0063C606, call 0x0063C618). The helper checks the object is located and then `IsRestingFlat(Radians(0.174533))` — ten degrees (0x3E32B8C2 at 0x0063C66C/0x0063C670). `ObservableObject::IsRestingFlat` 0x0087751C asks `GetRotatedParentAxis` for the parent axis the object's own Z lies closest to and returns `acos(\|dot\|) < tolerance`. The 15.0 is owned by M13-007. **Correction C-E4:** the callee is `ObservableObject::IsPoseTooHigh` 0x00877954 (PLT 0x4ACFF4): it returns `(D*f1 + f2 + 1e-5 < D*f3 + pose.translation.z)` with `D = GetDimInParentFrame<'Z'>` 0x00557794; with the caller's `f1=1.0, f2=15.0, f3=0.5` that is `pose.z > 0.5*D + 15.0 + 1e-5`. | X1 M15 |
| M12-013 | IMPLEMENTATION_GAP | `DockingErrorSignal` begins with the timestamp, not the geometry. `DockingComponent::UpdateDockingErrorSignal` 0x0063BE80 builds the struct at sp+0xa0: word 0 = the timestamp (0x0063C14A `str r6,[sp,#0xa0]`), then x (+0xa4, 0x0063C15E), y (+0xa8, 0x0063C174), z (+0xac, 0x0063C180) and the angle (+0xb0, 0x0063C1CE); `DockingErrorSignal::Pack` 0x007C0B26 reads five words then two bytes. The clamp uses 40 degrees (M12-019). | X1 M7 |
| M12-014 | COMPATIBILITY_POLICY | The docking error signal's last two bytes are whatever was on the engine's stack. `UpdateDockingErrorSignal` 0x0063BE80 is the only builder and never writes the struct bytes at +0x14/+0x15 (the stack bytes +0xb4/+0xb5); nothing clears the struct (no memclr, no ctor), and `Pack` 0x007C0B26 reads both bytes and sends them. The engine value is indeterminate, so this stack sends zero (SD2). The other twenty bytes are exact (M12-013). | X1 M7 |
| M12-015 | IMPLEMENTATION_GAP | `PlaceObjectOnGround` sends three zero offsets and a constant speed triple. The builder 0x00632B88, reached only from `CarryingComponent::PlaceObjectOnGround` 0x00632A88 (which zeroes the offsets at 0x00632AA6/0x00632AA8), reads the first three words as integer locals converted with `vcvt.f32.s32` (0x00632BAE/0x00632BB2/0x00632BB6) and the next three from the constant triple at 0x00C7CD90 — 100 (0x42C80000), 200 (0x43480000), 500 (0x43FA0000) — with the component bool as the last byte. | X1 M11 |
| M12-016 | IMPLEMENTATION_GAP | A roll gets three attempts. `RollBlockHelper::StartRollingAction` 0x005B9EF8 reads its own attempt count at helper+0x124 (0x005B9F0A), compares with the literal 3 (0x005B9F0E `cmp r0,#3`), and at or above it calls `MarkTargetAsFailedToRoll` (0x005B9F16) instead of starting another `DriveToRollObjectAction` (0x005B9F10 `blo`). The count is zeroed in the helper ctor 0x005B98D4. There is no charger helper in the build; the charger mount's one attempt is M13-008. | X1 M17; gap1 G8 |
| M12-017 | IMPLEMENTATION_GAP | `IDockAction::Init` setup and handler registration. `IDockAction::Init` 0x005514FC looks up the object (`GetLocatedObjectByIdHelper` 0x00551598), checks the pre-action pose (`GetPreActionPoses` 0x00551640), registers the completion handlers for message tags 0xc5 (0x005516EA) and 0xda (0x00551750), and sets the docking squint (`TrackLayerComponent::AddSquint` 0x00552394). The handler bodies are the action's own state machine. **Correction C-E5:** the `AddSquint` call at 0x00552394 is inside `IDockAction::CheckIfDone` 0x005521AC (body 0x005521AC..0x005523F6), not `Init` (body 0x005514FC..0x00551C8A, which contains no call to PLT 0x4ABA04). The call is `AddSquint(TrackLayerComponent at robot+0xC0, "DockSquint", 1.05, 0.35, -10.0)` (0x00552362, literal at 0x005524A0, 0x0055237A/88, 0x0055237E/8C, 0x00552378/82/86) and its bool is stored at `IDockAction+0xF4` (0x00552398). | X1 M4, N1 |
| M12-018 | IMPLEMENTATION_GAP | The dock abort path. `DockingComponent::AbortDocking` 0x0063BE10 builds an `AbortDocking` and sends it: `EngineToRobot(AbortDocking&&)` 0x0063BE2A (`blx 0x4B94C8`), `SendMessage` 0x0063BE36 (`blx 0x4A5368`). | X1 M6, N2 |
| M12-019 | IMPLEMENTATION_GAP | The docking-error-signal clamp is 40 degrees. `UpdateDockingErrorSignal` 0x0063BE80 clamps the pose to flat with 0x3F32B8C2 = 0.698132 rad = 40° (`movw r1,#0xB8C2` 0x0063C182 + `movt r1,#0x3F32` 0x0063C188) at `ObservableObject::ClampPoseToFlat` 0x0063C194. This is a different call site and value from the 20 degrees M11-006 records for the pose-confirmation path (0x3EB2B8C2). | X1 M7, N3 |
| M12-020 | IMPLEMENTATION_GAP | The production callers of `ComputePreActionPoseDistThreshold` (PLT 0x004AB938). A whole-`.text` scan finds exactly three: `DriveToPoseAction::CheckIfDone` 0x0055AB4C (call at 0x0055ACF0), `PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses` 0x005558F4 (call at 0x005561A0) and `IDockAction::GetPreActionPoses` 0x005508C8 (call at 0x00550FF8). `DriveToObjectAction::CheckIfDone` 0x00559880 is **not** a caller. The formula itself is M12-001. **Correction C-E6:** all three callers read **both** outputs with one `ldrd`: `DriveToPoseAction::CheckIfDone` 0x0055ACF4 -> `Point3 {out[0], out[1], GetHeight}` used as the x/y position tolerance of `Pose3d::IsSameAs` 0x4A7060; `PlaceRelObjectAction` 0x005561B0 -> `Point3 {out[0], out[1], 100.0}` for `IsSameAs` with `Radians(0.1308997)`; `IDockAction::GetPreActionPoses` 0x00550FFC -> stored at `sb+0x20`/`sb+0x24`, both must be > 0 and are compared against the robot's |dx|/|dy| to the closest pose, failing with 0x04000001. **Correction C-R3:** the threshold's pose order is (goal/pre-action pose, object pose): `r2.GetWithRespectTo(r1)` (0x005500EE..0x005500F4). The distance is the **3-D** norm (0x00550102..0x00550120), not planar. Caller 1 is M12-023, caller 2 is M12-026. | X1 N8; gap1 G6 |
| M12-021 | IMPLEMENTATION_GAP | The block face-def records and the per-face, per-rotation pre-action-pose gate. `Block::LookupBlockInfo` 0x004E4C8C builds a process-static map (guard 0x010590A8) with four entries, keys 1..4: LIGHTCUBE1/2/3 and LIGHTCUBE_GHOST. Each entry's face-def vector at info+0x20 is six 16-byte records copied verbatim from rodata: LIGHTCUBE1 0x00C45C40, LIGHTCUBE2 0x00C45CA0, LIGHTCUBE3 0x00C45D00, GHOST 0x00C45D60 (copy loops 0x004E4D12, 0x004E4DD2, 0x004E4E88, 0x004E4F3C). Layout `{FaceName u32 @0; MarkerType code u32 @4; size float 25.0 @8; maskForTypes0And5 u8 @0xC; maskForType4 u8 @0xD; pad @0xE}`. `GeneratePreActionPoses` calls `GetMarker` on record+0x0 (0x004E5942) and, in the inner sb=0..3 loop (Y rotations 0/pi/2/pi/-pi/2; the four `RotationVector3d` about `Y_AXIS_3D` (PLT 0x4A47B0) at 0x010590AC..0x010590E8, stride 0x14, built 0x004E5868..0x004E58E4, applied with `Transform3d::RotateBy` 0x0084BB3A; corrected in C-E7), tests `(1<<sb) & mask`: +0xC for types 0 and 5 (0x004E5976/0x004E5C80), +0xD for type 4 (0x004E5B94). Types 1 and 2 have no mask test; type 3 produces nothing. For the three real cubes the mask is +0xC = 0x05 for FaceNames 0..3, 0x00 for FaceName 4, 0x0F for FaceName 5, and +0xD = 0x0F for all; GHOST is 0x0F/0x0F everywhere. `Block::Block`/`AddFace` pass 0,0 and ignore the masks (0x004E5FF8/0x004E6010; 0x004E564C). | gap1 G1.3; gap3 H1 |
| M12-022 | IMPLEMENTATION_GAP | The `DriveToObjectAction::InitHelper` drive goal: a yaw-only `Pose3d` built from the object pose, the robot pose and `DriveToObjectAction+0x84`. `InitHelper` 0x00558FB4 builds it at 0x00559200..0x00559278: `objectInRobotParent = objectPose.GetWithRespectTo(robotPose.GetParent())` (0x005590AE); `delta = normalize(robot.xy - objectInRobotParent.xy) * DriveToObjectAction+0x84` (0x005590E6..0x005591E4; `vldr s2,[sb,#0x84]` 0x005591C2); the goal `Point3 = {objectInRobotParent.x + delta.x, objectInRobotParent.y + delta.y, robot.z}` (0x00559220..0x00559230); `yaw = atan2f(-delta.y, -delta.x)` (0x00559232/0x00559236/0x0055923A); `Pose3d(yaw, Z_AXIS_3D, goal, parent = objectInRobotParent.GetParent())` (0x00559278). `DriveToPoseAction::SetGoals` 0x0055A6D0 stores the vector as-is. **Unresolved:** the value/setter of `+0x84` (the 7-arg ctor sets -1.0) and whether the C# should use the engine's `object + delta` point; the C# currently drives to the chosen pre-action pose's position with the heading toward the object. **Correction C-R4:** no 7-arg ctor caller passes ActionType 6; the 4-arg ctor (the only source of 6) is reached from the RobotActionUnion GotoObject factory when `usePreDockPose` is 0 (0x0052A0EE) and the shipped app always sends it true, so only an external sender reaches the object+delta goal. `+0x84` is -1.0 on every 7-arg path. `DriveToPlaceCarriedObjectAction` passes ActionType 2 in production. The other +0x150 functions are M12-028. | B-M12 gap E9/Q15/Q17 |
| M12-023 | IMPLEMENTATION_GAP | DriveToPoseAction::CheckIfDone: the path-status state machine, tolerance and arrival test. libcozmoEngine.so 3.4.0-1204: DriveToPoseAction::CheckIfDone 0x0055AB4C: [[robot+0x24C]]==3 -> RUNNING (0x0055AB5C..0x0055AB6A); state=[[robot+0x5C]+0x38], >4 RUNNING; tbh 0x0055AB84 = {0x0055AB8E, 0x0055ABDC, 0x0055B0CE, 0x0055AC06, 0x0055ACA2}; initial result 0x03000008. State 0 Failed: 0x03000013, no end animation. State 1 ComputingPath: deadline [+0xB0] = now+[+0xA8], timeout aborts the path (PathComponent::Abort 0x0055AD5C) and gives 0x03000013. State 2 RUNNING. State 3 PlayStartAnim, [+0xB0]=-1, [+0xC4]++. State 4 Ready: tolerance Point3 {[+0x90],[+0x94],Robot::GetHeight}; if [+0xC0] the x,y come from ComputePreActionPoseDistThreshold(out, goals[idx], this+0xB4, this+0x9C) (0x0055ACF0); arrival is Pose3d::IsSameAs(robotPose, goals[idx], tolerance, Radians [+0x9C]); true -> 0; false -> if [[robot+0x5C]+0x42] != [[robot+0x5C]+0x44] keep 0x03000008, else 0x04000002. Tail: a result other than RUNNING and 0x03000013 calls PlayEndAnim; a nonzero return keeps RUNNING (0x0055B0AA..0x0055B0CC). +0xC0 is set 1 only by InitHelper (0x0055933E) and its inlined SetGoals overload, and 0 by the ctor (0x0055A31E) | R-VIS gap 1 |
| M12-024 | RECOVERABLE_GAP | DrivingAnimationHandler PlayStartAnim / PlayEndAnim. libcozmoEngine.so 3.4.0-1204: DrivingAnimationHandler ([robot+0x24C]; PLT 0x004AB134 and 0x004A5764) is called from DriveToPoseAction::CheckIfDone at state 3 (PlayStartAnim, 0x0055AC08) and at the tail (PlayEndAnim, 0x0055B0AA) | R-VIS gap 1 |
| M12-025 | IMPLEMENTATION_GAP | PlaceRelObjectAction: ctor, InitInternal and TransformPlacementOffsetsRelativeObject. libcozmoEngine.so 3.4.0-1204: PlaceRelObjectAction ctor 0x00554BE0: A -> +0x108, B -> +0x10C, r3 -> +0xA8, the ctor bool -> +0x110, +0xFC=-1, +0x100=0, +0xC0=0, +0xF0 = 0x208 or 0x209 if r3 != 0, IDockAction type 0x15; if |A| < 9.99999975e-06 (0x3727C5AC) and |B| < 9.99999975e-06 the +0xB9 store is skipped, otherwise +0xB9 = 0 with the log PlaceRelObjectAction.Constructor.WillNotCheckPreDockPoses (0x00554C86..0x00554CDE; the verifier corrected the extractor's inverted condition). InitInternal 0x00554DCC: if [+0x110]==0 call TransformPlacementOffsetsRelativeObject, then [+0x9C] = A' (0 if A' < -16.000009), [+0xA0] = B', [+0xBB]=3 if |B| >= eps. TransformPlacementOffsetsRelativeObject 0x00554E40: the object's z-rotated point above centre (0.5) relative to the robot gives psi (tolerance 0x3E860A92): psi~0 -> (A',B')=(-A,B); psi~+pi/2 -> (B,A); psi~-pi/2 -> (-B,-A); psi~+-pi -> (A,-B); none -> 0x04000001; A' < -16.000009 -> error, 0x03000000; object missing -> 0x03000004; else store +0x108/+0x10C, return 0 (independently re-decoded by the verifier). The wire PlaceRelObject message: usePreDockPose true builds DriveToPlaceRelObjectAction(robot, id, true, A=[msg+0x30], B=0.0, useApproachAngle=[msg+0x38], angle=[msg+0x34], useManualSpeed=[msg+0x3A], Radians(0), false, true) (0x0052A6C2..0x0052A700); false builds PlaceRelObjectAction(robot, id, true, A, 0.0, [msg+0x3A], true) then stores 1 at +0xF7 and 0 at +0xB9 (0x0052A72E..0x0052A754). DriveToPlaceRelObjectAction ctor 0x0055C7D0 installs the {vtbl,robot,A,B} pose-lambda only if its last bool is 0; the only caller passes 1, so the lambda is never installed on the wire path and +0x110 = 1, so the transform is skipped there (it runs for PlaceRelObjectHelper with a zero third byte) | R-VIS gap 1 |
| M12-026 | IMPLEMENTATION_GAP | PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses. libcozmoEngine.so 3.4.0-1204: ComputePlaceRelObjectOffsetPoses 0x005558F4 (r1=A, r2=B; callers: the DriveToPlaceRelObjectAction lambda invoker 0x0055E1C6 -> 0x0055E1DC, DriveToHelper 0x005B5918 with A,B = DriveToHelper+0x108/+0x10C, and IsAtPreActionPoseWithVisualVerification 0x005B7054, which calls it only when ActionType == 1): the GetPreActionPoses request/response, the per-element local pose, r8 = (|A|>1)&&(|x|>1), r7 = (|B|>1)&&(|y|>1), the sign-mismatch erase, the (r8=1,r7=0) and (0,1) erases (size>=3), the pi/2 and 4.712389 tolerances, the marker-count check and the FOV/tan arithmetic L = s4*t - marker[+0x10], mode 1: x += clamp(A,-L,L), y += B; mode 0: x += A, y += clamp(B,-L,L); the world-origin re-expression; the threshold call at 0x005561A0 with the SHIFTED element first and the object pose second, then Point3 {out0, out1, 100.0}, Radians 0x3E060A92, IsSameAs on the robot pose and *alreadyInPosition = 1. Rows 12.1-12.18 of the pre-extraction, each opened by the verifier (all PASS; 12.16's argument order agrees with the three callers). IsAtPreActionPoseWithVisualVerification 0x005B6F5C returns 0x03000004 for a missing object, 0x0300001D if [obj+0x1C]+1000.0 < now, and otherwise 0 if in position else 0x04000001 | R-VIS gap 1 |
| M12-027 | RECOVERABLE_GAP | The lift pose's per-tick update and the BroadcastObjectPoseChanged chain. libcozmoEngine.so 3.4.0-1204: a carried object's pose parent is the lift pose (M12-008), so it follows the lift only through the pose tree; Robot::ComputeLiftPose 0x005151AC and where the lift pose is updated per tick were not read. ObjectPoseConfirmer::BroadcastObjectPoseChanged 0x00506F88 is called by AddObjectRelativeObservation (0x00506D48) and AddLiftRelativeObservation (0x00506E9E); whether and how it reaches BlockWorld::OnObjectPoseChanged 0x00624808 was not read | R-VIS gap 1 |
| M12-028 | RECOVERABLE_GAP | The DriveToObjectAction +0x150 pose functions other than PlaceRel's and DriveToHelper's, and the pyramid PlaceRelObjectParameters. libcozmoEngine.so 3.4.0-1204: DriveToObjectAction installs a default std::function at +0x150 (0x005585F8, 0x0055889C) that callers replace at 0x0055C8A8 (never reached on the wire path, M12-025), 0x0055E2CA, 0x0055E3E0, 0x0055EB74, 0x0055EC2A and 0x005B58C2; only PlaceRel's (0x005586AC) and DriveToHelper's were read. The PlaceRelObjectParameters built by BehaviorStackBlocks::TransitionToStackingBlock 0x005C9588 (constant 0x00C69140 = {0,0,1}), BehaviorBuildPyramid::TransitionToPlacingTopBlock 0x005DC266 and BehaviorBuildPyramidBase::TransitionToPlacingBaseBlock 0x005DCE44 are behaviour-owned and unread | R-VIS gap 1 |

## Decisions (the manager's, recorded for audit)

- **MD1 (SD1).** Every engine-side behaviour above is reproduced exactly. No EQUIVALENT_IMPLEMENTATION is used in this subsystem.
- **MD2 (SD2, M12-014).** The two bytes the engine never writes are indeterminate (stack contents). This stack sends zero, the safest deterministic value, recorded as a forced COMPATIBILITY_POLICY. Precedent: M3-019/020, M6-021, M10-013.
- **MD3 (M12-003).** M12-003's title spanned three claims: the dock-message order (now its own, cited production path), the pick-and-place result (M12-007/M12-015) and the error signal (M12-013), and its evidence was the bare phrase "IDockAction and the firmware exchange". It is narrowed to the dock-message order; M12-005 owns the field layout it sends, so the two records do not duplicate each other.
- **MD4 (the action entry).** The action ctors (`PickupObjectAction` 0x00553648, `PlaceObjectOnGroundAction` 0x00554638, `DriveToPickupObjectAction` 0x0055C464) are an M8/M2 interface, not an M12 record.
- **MD5 (interfaces).** The 15.0 stack tolerance is owned by M13-007; M12-008 and M12-012 cite it and do not restate it as their own. The charger mount (one attempt, retryable result) is M13-008; M12's "roll and mount" is the roll (M12-016). The 20° pose-confirm clamp is M11-006.
- **MD6 (M12-021).** The block face-def records and their per-face masks are a distinct behaviour-changing data table on the pre-action-pose path, so they get their own record rather than being deferred into M12-001.
- **MD7 (the "floor").** The C# comment in `PreActionPose.cs:89-90` claimed the engine substitutes the angle tolerance "when it is above a floor". Gap pass 3 refutes it: the only tolerance branch is the positivity guard `angleTolerance > Radians(0)` (with `operator>`'s ~1e-5 rad epsilon), which returns the -1.0f sentinel. The threshold's distance is 3-D, and it writes two outputs (2·dist·sin and dist·sin). The comment and the code are corrected in the build.
- **MD8 (SD3).** Records whose consumers live in unbuilt layers stay IMPLEMENTATION_GAP; the build compares them when those layers exist. The named interfaces are M13-007/008, M11-006 and M8/M2.

## Corrections (B-M12 build gap pass, 2026-09-28)

A read-only extractor gap pass (report embedded as Appendix E) answered the MISSING items the build left open and corrected five rows. Each correction is marked inline as **C-E1..C-E6** in the row it changes, and the affected manifest records were updated before this inventory was re-approved.

- **C-E1 (M12-001, M12-021).** The world pose is composed by the copy-with-pose ctor `PreActionPose(PreActionPose const&, Pose3d const&, float, float)` 0x0050DF00, not by the arithmetic block; `GetCurrentPreActionPoses` passes `a = element+0x18` and `b = preDockPoseOffset_mm`, and when that offset is 0 uses `b = min(dist2, element+0x18)`. The stores at 0x004DFCFC..0x004DFD00 are the `param_8==1` visualization. The face-def records, their verbatim tables and the per-face gate are restated in the generation row.
- **C-E2 (M12-006).** The release is `HandlePickAndPlaceResult` 0x00533780, gated on `blockStatus == 1` **and** `success != 0`, bool false; the attach is gated the same way on `blockStatus == 2`.
- **C-E3 (M12-010).** State 0 executes the turn **then** the search (append + front-to-back); state 2 is a two-iteration loop with no state-1 tail.
- **C-E4 (M12-012).** `IsPoseTooHigh` 0x00877954: too-high iff `pose.z > 0.5*D + 15.0 + 1e-5`, `D = GetDimInParentFrame<'Z'>`.
- **C-E5 (M12-017).** The docking squint is called from `IDockAction::CheckIfDone`, not `Init`: `AddSquint(robot+0xC0, "DockSquint", 1.05, 0.35, -10.0)`.
- **C-E6 (M12-020).** All three threshold callers read both outputs (`ldrd`) as an x/y position tolerance.
- **C-E7 (M12-001, M12-021).** The four `sb` rotations are about **Y** (`Y_AXIS_3D`, table 0x010590AC), not Z, and the per-type stored poses are the `Pose3d(angle, Z_AXIS, translation)` with the marker as parent and the `-size.z/2` third component, as corrected in the generation row. `FlipBlockAction::Init` reaches the threshold through `IDockAction::GetPreActionPoses` (Q12), so the C# pre-action check must use the threshold pair, not a 100 mm box. `Pose3d::IsSameAs` compares the full rotation (the static `RotationAmbiguities` is empty), so the goal rotation must not be flattened to yaw. `Robot::GetHeight` (the Point3 z of caller 1) is `max(66·sin(lift) + 45 + 5, 67.7)`.
- **C-E8 (M12-022, new).** `DriveToObjectAction::InitHelper` builds the `DriveToPoseAction` goal as a **yaw-only** pose from the object pose, the robot pose and `DriveToObjectAction+0x84` (delta/point at 0x00559200..0x00559278), not the raw pre-action pose. The exact `+0x84` distance and point are unread, so M12-022 is a new gap and the C# uses the pre-action pose position with the heading toward the object as a labelled reduction.

## Corrections (R-VIS gap pass 1, 2026-09-29)

Source: R-VIS gap 1 (`20260929-R-VIS-M12-M13-gap1-extraction.md`, verified in `20260929-R-VIS-verify-M12-M13-gap1.md`). The extractor's report was checked row by row by a read-only verifier (FAIL on four rows, all four corrected below; the priority checks all passed). Where the two disagree the verifier's reading is used.

- **C-R1 (M12-008).** SetObjectAsAttachedToLift is a nine-step flow; the marker translation length is 3-D (verifier A2 corrected the extractor's planar reading).
- **C-R2 (M12-011).** `DriveToObjectAction::CheckIfDone` compares against +0x84 only for ActionType 6; the production path calls the +0x150 function (verifier A7: the extractor had attributed the state machine to M12-011).
- **C-R3 (M12-020).** Pose order (goal, object) and a 3-D norm (verifier A1).
- **C-R4 (M12-022).** No 7-arg caller passes 6; the type-6 goal is reachable only from an external GotoObject.
- **C-R5 (new M12-023, M12-024).** `DriveToPoseAction::CheckIfDone`'s state machine is its own record; the driving-animation handler is a RECOVERABLE_GAP.
- **C-R6 (new M12-025, M12-026).** `PlaceRelObjectAction` (ctor, InitInternal, offset transform) and `ComputePlaceRelObjectOffsetPoses` are records. Verifier A4 inverted the extractor's `+0xB9` condition; A3 corrected the non-predock constructor call; A5 records that the pose-lambda is never installed on the wire path.
- **C-R7 (new M12-027, M12-028).** The lift pose's per-tick update, the `BroadcastObjectPoseChanged` chain, the other +0x150 installers and the behaviour-owned PlaceRelObjectParameters are RECOVERABLE_GAP.

**Decisions (manager).** MD9: the ActionType-6 goal is built although the shipped app never triggers it, because the code ships in the engine (Exact, always); it is marked as reachable only from external senders. MD10: the null-map dereference the engine performs in M11-017 rows 48 and 49 is not a M12 matter.

**Open questions the verifier left:** the whole-.text scan for other `+0x84` accessors and for objects inlining the DriveToObjectAction base ctor was not rerun by the verifier (the extractor's scan is its evidence); `WaitForImagesAction`'s fine-tune constants are M13.

## Existing record evidence found contradicted or too weak

- **M12-001** — evidence was a bare phrase. The per-type distances are `movw`/`movt` immediates in `Block::GeneratePreActionPoses`, **not** a table and not the 75.0 at 0x00503DC8 (that literal is inside `MinimalAnglePlanner::ComputeNewPathIfNeeded` 0x00503C18 and unrelated). The record's "49 mm" conflated a ctor distance with a pose offset; the type-2/5 4th ctor arg is 0. Gap 1 also called the threshold distance "planar"; it is the **3-D** norm (gap 3 H2.9). The record's `+0x18` consumer is `ActionableObject::GetCurrentPreActionPoses`.
- **M12-002** — the path-packing citation `0x00632BAE..0x00632BEE` was inside the `PlaceObjectOnGround` builder, not path packing. Corrected to `PathDolerOuter::Dole` 0x00507E4C and `PathComponent::ExecutePath` 0x0064A340. The `ClearPath` citations were confirmed.
- **M12-003** — too weak: bare-phrase evidence. Narrowed to the dock-message order with the exact addresses (MD3); the field layout is M12-005.
- **M12-004** — `IBehavior::UseSecondClosestPreActionPose` starts at 0x005BEE56, not 0x005BEE40 (which is `asrs r4,r4,#1` mid-function). `RemoveMatchingPredockPose` 0x00551418 confirmed.
- **M12-010** — state 1 omitted its tail (the second drive, `TurnInPlaceAction(-3pi/4)`, `TurnInPlaceAction(-0.785398)` and the second nearby search); the caller site is 0x005B5D68, not 0x005B5D30.
- **M12-011** — the report's N8 had `DriveToObjectAction::CheckIfDone` as the threshold caller; it is not. The three real callers are M12-020.
- **M12-014** — the fill range is +0xa0..+0xb3 (five words), not +0xa0..+0xb1; the non-write of struct +0x14/+0x15 and the `Pack` layout are confirmed.
- **M12-013** — confirmed (the timestamp at word 0 and the five-word + two-byte layout).
- **M12-005, M12-006, M12-007, M12-008, M12-009, M12-012, M12-015, M12-016** — confirmed against the source; their citations are replaced with the exact instructions above.

## Appendix A: X1 pass, extractor report
# X1 — M12-manipulation extraction (read-only)

Job: re-analysis/jobs/X1.md. Subsystem: M12-manipulation (pre-action poses, path packing,
docking, pick-up and place verification, roll and mount, the carried-object pose chain).
Agent: opencode (DeepSeek), window 3. Date: 2026-09-27.

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204), Thumb-2.
Existing records: M12-001..M12-016.

## 0. Method

- The Ghidra decompilation and the OBB are absent in this clone; I read the `.so` directly with
  capstone. All addresses are file VAs and checkable.
- This is the first M12 pass in this process. It confirms the existing records where the source
  supports them, names the two whose evidence is too weak, and adds the NEW steps the records do
  not cover. The carried-object pose chain (M12-008), the dock message (M12-005), the error signal
  (M12-013/014), the pick-up verification (M12-007) and the place builder (M12-015) were
  re-read instruction by instruction and hold.

## 1. Production path

Entry: a game/behaviour action on a located object. Exit: engine-to-robot messages and the
action results that drive the behaviour retry logic.

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| M1 | Action entry (M8/M2 interface) | A `PickupObject`/`PlaceObjectOnGround`/`RollObject`/`DriveToObject` request reaches the action classes through the behaviour/action layer. Named as an interface only. | `0x00553648` (`PickupObjectAction` ctor); `0x00554638` (`PlaceObjectOnGroundAction` ctor); `0x0055c464` (`DriveToPickupObjectAction` ctor) | NEW (interface) | EXACT_SOURCE for the ctors |
| M2 | Pre-action pose generation | `IDockAction::GetPreActionPoses` asks the object for its current pre-action poses: `ActionableObject::GetCurrentPreActionPoses`, which dispatches to the object's `GeneratePreActionPoses` (Block 0x004e5808, Bridge 0x004e90c8, Charger 0x004e9fb0, Ramp 0x0050ef44). `Block::GeneratePreActionPoses` builds `PreActionPose` records for the four side markers (action types 0..5). | `0x005508c8`; `0x00550a3e: blx 0x4a4210` (`GetCurrentPreActionPoses`); `0x004df850`; `0x004e5808`; `0x0050dcfc` (`PreActionPose` ctor); `0x00550098` (`ComputePreActionPoseDistThreshold`) | M12-001 (partial) | EXACT_SOURCE for the call chain; the per-type distance derivation is RECOVERABLE (see M12-001 below) |
| M3 | Distance threshold | `ComputePreActionPoseDistThreshold(objectPose, preActionPose, angleTolerance)` computes the planar distance between the two poses and multiplies by `sin(angleTolerance)`. | `0x00550098`; `0x005500f0: blx 0x4a40fc` (`GetWithRespectTo`); `0x00550102: vldr s0,[r0,#0x20]`; `0x00550110/0x0055011c` (sum of squares); `0x00550134: blx sqrtf`; `0x00550140: blx sinf`; `0x0055014e: vmul.f32 s16,s20,s0`; `0x005501a4/0x005501a8` (out dist, dist*sin) | M12-001, M12-011 | EXACT_SOURCE |
| M4 | Dock init and handler registration | `IDockAction::Init` looks up the object, checks the pre-action pose, registers the completion handlers (message tags 0xc5 and 0xda), and sets the docking squint. | `0x005514fc`; `0x00551598: blx 0x4a7114` (`GetLocatedObjectByIdHelper`); `0x00551640: blx 0x4ab950` (`GetPreActionPoses`); `0x005516ea: movs r3,#0xc5`; `0x00551750: movs r3,#0xda`; `0x00552394: blx 0x4aba04` (`AddSquint`) | NEW | EXACT_SOURCE (entry and tags); the handler bodies are the action's own state machine |
| M5 | Dock message build | `IDockAction::CheckIfDone` is the only caller of `DockingComponent::DockWithObject`, which packs the 21-byte `DockWithObject` and sends it. The builder writes word 0 = 0, words 1..3 = the speed/accel/decel, then the dock action and flags. | `0x005521ac` (`CheckIfDone`); `0x005522ae: blx 0x4ab9f8` (`DockWithObject`); `0x0063ba44` (`DockingComponent::DockWithObject`); builder `0x0063bd50`; `0x0063bdb8: blx 0x4b94bc` (`EngineToRobot(DockWithObject&&)`); `0x0063bdc4: blx 0x4a5368` (`SendMessage`) | M12-003, M12-005 | EXACT_SOURCE |
| M6 | Abort path | `DockingComponent::AbortDocking` builds an `AbortDocking` and sends it. | `0x0063be10`; `0x0063be2a: blx 0x4b94c8` (`EngineToRobot(AbortDocking&&)`); `0x0063be36: blx 0x4a5368` (`SendMessage`) | NEW | EXACT_SOURCE |
| M7 | Docking error signal | `DockingComponent::UpdateDockingErrorSignal(timestamp)` writes word 0 = timestamp, then x, y, z and the yaw; it never writes the struct's bytes at +0x14/+0x15. It clamps the pose to flat with 40 degrees. | `0x0063be80`; `0x0063c14a: str r6,[sp,#0xa0]`; `0x0063c15e: vstr s16,[sp,#0xa4]`; `0x0063c174: vstr s18,[sp,#0xa8]`; `0x0063c180: str r6,[sp,#0xac]`; `0x0063c182: movw r1,#0xb8c2` / `0x0063c186: movt r1,#0x3f32` (0x3f32b8c2 = 0.698132 rad = 40 deg); `0x0063c194: blx 0x4a6fd0` (`ClampPoseToFlat`); `0x007c0b26` (`DockingErrorSignal::Pack`) | M12-013, M12-014 | EXACT_SOURCE |
| M8 | Dock result -> pick-up verify | The robot's `DockWithObject` completion reaches `PickupObjectAction::Verify`, which stamps the first call, tests the object's moving flag against 500 ms, and the last-observed time against a dock-action-dependent timeout (500 ms low, 2000 ms high), then calls `SetCarriedObjectAsUnattached(true)` on failure. | `0x00553be0`; `0x00553bf8: str.w r5,[r7,#0x10c]`; `0x00553c8e: blx r2` (virtual `IsMoving`); `0x00553c98: ldr.w r0,[r7,#0x118]`; `0x00553caa: blx 0x4a7b7c` (`SetCarriedObjectAsUnattached`); `0x00553d0a/0x00553d12/0x00553d18` (dock action selects +0x120 / +0x11c) | M12-007 | EXACT_SOURCE |
| M9 | Carried-object attach | `CarryingComponent::SetObjectAsAttachedToLift` places the object with respect to the lift pose at `(|dockMarkerOffset| + 4, 0, -12.5)`. | `0x00632cc4`; `0x00632e42/0x00632e4a/0x00632e5c/0x00632e62/0x00632e74` (norm of the dock marker translation); `0x00632e7c: vmov.f32 s0,#4.0`; `0x00632e82: movt r0,#0xc148` (0xC1480000 = -12.5); `0x00632e8c: vadd.f32 s0,s2,s0` | M12-008 | EXACT_SOURCE |
| M10 | Carried-object release | `CarryingComponent::SetCarriedObjectAsUnattached(bool)` takes the object pose with respect to the robot and hands it to `ObjectPoseConfirmer::AddRobotRelativeObservation(object, pose, PoseState 2)`; the bool additionally deletes the located objects. | `0x006333d4`; `0x00633458` (`AddRobotRelativeObservation`); `0x00633930` (delete branch) | M12-006 | EXACT_SOURCE |
| M11 | Place message | `CarryingComponent::PlaceObjectOnGround` builds `PlaceObjectOnGround` with three zero offsets and the constant speed triple 100/200/500. | `0x00632a88`; builder `0x00632b88`; `0x00632bae/0x00632bb2/0x00632bb6` (offsets); `0x00c7cd90` = 0x42c80000 (100), `0x00c7cd94` = 0x43480000 (200), `0x00c7cd98` = 0x43fa0000 (500) | M12-015 | EXACT_SOURCE |
| M12 | Path packing | Path segments and `ExecutePath` are packed; `PathComponent::ClearPath` writes a literal zero into its one u16. | `0x00632bae..0x00632bee`; `0x00649220` (`ClearPath`); `0x00649234/0x0064923a` (path-id correlation); `0x00649268: movs r0,#0`; `0x0064926a: strh.w r0,[sp]` | M12-002 | EXACT_SOURCE |
| M13 | Dock retry | The dock retry drops the pose it just failed from (`RemoveMatchingPredockPose`) and uses the second closest pose. | `0x005bee40` (`UseSecondClosestPreActionPose`); `0x00551418` (`RemoveMatchingPredockPose`) | M12-004 | EXACT_SOURCE |
| M14 | Drive result | `DriveToObjectAction::CheckIfDone` compares the distance from the pre-action pose against the tolerance at +0x84 and returns `DidNotReachPreActionPose` (0x04000001) on failure, `BadObject` (0x03000004) when the object is not located. | `0x00559880`; `0x005598e2: mov.w r8,#0x4000001`; `0x005599fe` (fail); `0x00559...` (BadObject) | M12-011 | EXACT_SOURCE |
| M15 | Stack validity | `DockingComponent::CanStackOnTopOfObject` = `CanInteractWithObjectHelper` then `!IsPoseTooHigh(pose,1.0,15.0,0.5)`; the helper tests `IsRestingFlat(Radians(0.174533))` (10 degrees). | `0x0063c5c4`; `0x0063c654`; `0x0063c670` (0x3e32b8c2 = 0.174533); `0x0087751c` (`IsRestingFlat`) | M12-012 | EXACT_SOURCE |
| M16 | Pick-up retry limit | `PickupBlockHelper::RespondToPickupResult` reads the attempt count at helper+0x108 and retries only while it is 1 or less. | `0x005b8050` (function); `0x005b8192: ldr.w r0,[sl,#0x108]`; `0x005b8196: cmp r0,#1`; `0x005b8198: bls 0x5b826c` (retry) | M12-009 | EXACT_SOURCE |
| M17 | Roll retry limit | `RollBlockHelper::StartRollingAction` reads its count at helper+0x124 and stops at 3. | `0x005b9ef8`; `0x005b9f0a: ldr.w r0,[r4,#0x124]`; `0x005b9f0e: cmp r0,#3`; `0x005b9f10: blo 0x5b9f20`; `0x005b9f12` (`MarkTargetAsFailedToRoll`) | M12-016 | EXACT_SOURCE |
| M18 | Search for block | `SearchForBlockHelper::SearchForBlock` dispatches on the counter at +0x10C and builds the search patterns; `SearchForNearbyObjectAction` draws the waits/angles. | `0x005baf24`; `0x005469c0`/`0x00546c14` (`SearchForNearbyObjectAction`); `0x005bb478` (`SearchFinishedWithoutInterruption`) | M12-010 | EXACT_SOURCE |

## 2. Existing records judged

**Confirmed against the source (keep status):**

- **M12-002** — the packing and the `ClearPath` zero are read; the path-id correlation
  (`0x00649234`/`0x0064923a`) is read. Keep EXACT_SOURCE.
- **M12-004** — both functions exist and are the retry path. Keep.
- **M12-005** — `CheckIfDone` -> `DockWithObject` -> builder -> `SendMessage` is read
  (`0x005522ae`, `0x0063ba44`, `0x0063bd50`, `0x0063bdb8`). Keep. The record's word/byte assignment
  was re-checked and holds.
- **M12-006** — `SetCarriedObjectAsUnattached` 0x006333d4 and the two callers (false from
  `BehaviorPutDownBlock`, true from `PickupObjectAction::Verify`) match. Keep.
- **M12-007** — the two timed checks and both `SetCarriedObjectAsUnattached(true)` sites are read
  (`0x00553c98`/`0x00553caa`, `0x00553d0a`). Keep.
- **M12-008** — the lift pivot/pose chain and the attach pose `(|offset|+4, 0, -12.5)` are read
  (`0x00632e42..0x00632e8c`). Keep.
- **M12-009** — the `cmp r0,#1 / bls` retry is read (`0x005b8192`). Keep.
- **M12-010** — the search pattern and its callers exist. Keep (the row was not re-read line by
  line this pass).
- **M12-011** — the `0x04000001` result is read. Keep.
- **M12-012** — `CanStackOnTopOfObject`, the ten-degree `IsRestingFlat`, and the 15 mm height check
  are read. Keep.
- **M12-013** — word 0 is the timestamp (`0x0063c14a`), x/y/z/angle follow. Keep.
- **M12-014** (EQUIVALENT_IMPLEMENTATION) — `UpdateDockingErrorSignal` writes only +0xa0..+0xb1;
  bytes +0xb4/+0xb5 (the struct's +0x14/+0x15) are never written. The engine value is
  indeterminate; EQUIVALENT stays honest. Keep.
- **M12-015** — the three zero offsets and the 100/200/500 triple at 0x00c7cd90 are read. Keep.
- **M12-016** — the `cmp r0,#3 / blo` is read (`0x005b9f0e`). Keep.

**Evidence too weak for the stated status (the checker cannot see this; the manager must):**

- **M12-001** (EXACT_SOURCE). `evidence` is the phrase "pre-action pose table and the actions that
  ask for each type" with no address. The chain is real
  (`0x005508c8` -> `0x004df850` -> `0x004e5808`), and `ComputePreActionPoseDistThreshold`
  (`0x00550098`) is read, but the per-type distances 75 / 40 / 49 mm were **not re-derived** in
  this pass: the 75.0 literal (0x42960000) exists at `0x00503dc8` and 40.0 (0x42200000) at several
  sites, but the exact mapping type->distance inside `Block::GeneratePreActionPoses` was not
  followed. Replace the evidence with the instructions, or mark the mapping RECOVERABLE_GAP and
  read `Block::GeneratePreActionPoses` 0x004e5808 (the per-type branches and the `PreActionPose`
  constructors 0x0050db14/0x0050dcfc) and `Block::LookupBlockInfo` 0x004e58fc.
- **M12-003** (EXACT_SOURCE). `evidence` is the phrase "IDockAction and the firmware exchange"
  with no address. The facts it claims are covered by M12-005 (the dock message), M12-007 (the
  verification) and M12-013 (the error signal), but this record as written carries no citation of
  its own. Replace the evidence with those addresses or narrow the record to the dock-message
  order (`0x005521ac` -> `0x005522ae` -> `0x0063bd50`).

**No record contradicted by this pass.** The two `Docking.cs` defects named in PROJECT_STATE's
cleanup queue (R-P1: releasing the carried object on `BlockPlaced` without the success gate;
R-P4: `MovingLiftPostDock != 0` where the engine compares for equality with `IDockAction+0x80`) are
C# issues, not manifest-record contradictions; the source behind them is M12-006 and M12-005.

**Partial evidence:**

- M12-001's evidence proves neither the table nor the distances; it is only a pointer. See above.
- M12-008's evidence proves the pose chain and the attach offsets; it does not prove the lift
  angle range or the `FindObjectOnTopOrUnderneathHelper` 15.0 (that is M13-007's).

## 3. NEW steps (no existing record)

- **N1 (M4):** `IDockAction::Init`'s setup and handler registration, tags 0xc5 and 0xda
  (`0x005514fc`, `0x005516ea`, `0x00551750`), and the docking squint (`0x00552394`).
- **N2 (M6):** the dock abort path, `DockingComponent::AbortDocking` -> `AbortDocking` message
  (`0x0063be10`, `0x0063be2a`).
- **N3 (M7):** the docking-error-signal clamp uses **40 degrees** (`0x3f32b8c2` at `0x0063c182`),
  which is a different call site and value from the 20 degrees M11-006 records for the
  pose-confirmation path (`0x3eb2b8c2`). Record the two separately.
- **N4 (M5):** the `DockWithObject` builder's send path (`0x0063bdb8` `EngineToRobot`, `0x0063bdc4`
  `SendMessage`), distinct from the field packing M12-005 records.
- **N5 (M9/M10):** `CarryingComponent::SetObjectAsAttachedToLift` (`0x00632cc4`) is the attach side
  of the carried-object chain; M12-006 records only the release side.
- **N6 (M16/M17):** the helper retry entry points `PickupBlockHelper::StartPickupAction`
  (`0x005b7b48`), `RespondToPickupResult` (`0x005b8050`) and `RollBlockHelper::StartRollingAction`
  (`0x005b9ef8`) — the records carry the limits but not the call sites.
- **N7 (M2):** the per-object pre-action-pose generators (`Block` 0x004e5808, `Bridge` 0x004e90c8,
  `Charger` 0x004e9fb0, `Ramp` 0x0050ef44) — M12-001 names only "the table".
- **N8 (M13):** `DriveToObjectAction::CheckIfDone` (`0x00559880`) is the production caller of the
  pre-action-pose distance threshold; M12-011 records the result, not the call.

## 4. Open questions for the manager / integrator

1. **M12-001's distances.** The 75 / 40 / 49 mm mapping is not settled by this pass. The manager
   should either send a follow-up extractor pass over `Block::GeneratePreActionPoses` 0x004e5808
   (per-type branches, `LookupBlockInfo`, the `PreActionPose` constructors), or mark M12-001's
   distance mapping RECOVERABLE_GAP. The record's current evidence cannot keep EXACT_SOURCE.
2. **M12-003's scope.** Its evidence is a phrase. Decide whether it is narrowed to the dock-message
   order (then cite `0x005521ac`/`0x005522ae`/`0x0063bd50`) or folded into M12-005.
3. **The mount/charger path.** M12's scope names "roll and mount". The charger mount is settled in
   M13-008 (one attempt, retryable result); the roll is M12-016. Confirm that no M12 mount record
   is missing, or add one naming the charger action (`ChargerActions.cs` / the engine's charger
   action) as the interface.
4. **The `FindObjectOnTopOrUnderneathHelper` 15.0 tolerance** appears in M12-008's evidence and in
   M13-007; the manager should keep one owner for it to avoid a duplicate claim.
5. **DockingErrorSignal Pack.** `0x007c0b26` reads five words then two bytes; M12-014 says the two
   bytes are indeterminate. This pass confirms they are never written before the send. No change.

*Read-only extraction. Nothing outside `.scratch/X1/` and this report file was changed.*


## Appendix B: gap pass 1, extractor report
# I-M12 gap pass 1 — M12-manipulation extraction (read-only)

Job: re-analysis/jobs/I-M12.md, gap pass 1. Subsystem: M12-manipulation.
Agent: opencode (extractor), window 3. Date: 2026-09-28.
Prior report: `re-analysis/research/20260927-X1-M12-extraction.md`.
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204), Thumb-2.
Method: every address below was opened in the `.so` with capstone (Thumb) and the symbol table;
the Ghidra decompilation was used only to navigate. A bare symbol is never the citation.

Answer to each numbered question, with exact instructions and a class. No guesses.

---

## G1. M12-001 per-type distance mapping

The distances are **not** a table and **not** the 75.0 at `0x00503DC8`. `Block::GeneratePreActionPoses`
contains them as `movw`/`movt` immediates (which is why the verifier's raw-literal search found none).
`0x00503DC8` is inside `MinimalAnglePlanner::ComputeNewPathIfNeeded` `0x00503C18` and is unrelated.

| step | what the original does | citation | class |
|---|---|---|---|
| G1.1 | `Block::GeneratePreActionPoses(actionType, poses)` clears the output vector, reads the block size, then dispatches on `actionType` 0..5 with a jump table; `>5` produces nothing. | `0x004E5808`; `cmp r5,#5 / bhi` `0x004E5958`; `tbh [pc,r5,lsl #1]` `0x004E595E`; table at `0x004E5962`: 0→`0x004E596E`, 1→`0x004E5A4C`, 2→`0x004E5AF0`, 3→`0x004E5DA2`, 4→`0x004E5B8C`, 5→`0x004E5C78` | EXACT_SOURCE |
| G1.2 | The block info is looked up. `Block::LookupBlockInfo` **symbol is `0x004E4C8C`**; the report's `0x004e58fc` is the call site, not the symbol. | call via PLT `0x004A47C8` at `0x004E58FC`; symbol `0x004E4C8C` | EXACT_SOURCE |
| G1.3 | `LookupBlockInfo` builds/returns a `BlockInfo` from a map keyed by `ObjectType` (LIGHTCUBE1/2/3; the face definitions are copied from rodata `0x00C45C40`, `0x00C45CA0`, `0x00C45D00`, six 16-byte entries each). The caller reads the face-def vector at `info+0x20..+0x24`, calls `Block::GetMarker` (`0x004A44C8`) on each entry's face name, and tests a per-entry mask byte at entry`+0xC` (types 0 and 5) or entry`+0xD` (type 4) against `1<<sb`, where `sb` is the inner 0..3 loop index that also selects the rotation vector (`fp`, advanced by 0x14 per step from the four `RotationVector3d` built at `0x004E5876..0x004E58E4`). | `0x004E4C8C`; `0x004E5900 ldrd r1,r0,[r0,#0x20]`; `0x004E5942 blx GetMarker`; `0x004E5976 ldrb r0,[r0,#0xc]`; `0x004E5B94 ldrb r0,[r0,#0xd]` | EXACT_SOURCE for the read; the six face-def records' full contents are RECOVERABLE_GAP (read `0x004E4C8C` and the rodata at `0x00C45C40..0x00C45DA0`) |
| G1.4 | type 0 (Docking): the 4th ctor arg is **75.0**; pose angle = marker angle + pi/2, pose built from the file-static `Pose2d` at `0x01059078`. | branch `0x004E596E`; `movt r1,#0x4296` `0x004E5A36`; `blx PreActionPose` `0x004E5A40` | EXACT_SOURCE |
| G1.5 | type 1 (PlaceRelative): the 4th arg is **40.0**; the pose is translated **-100.0** along the marker axis. | branch `0x004E5A4C`; `movt r0,#0xc2c8` `0x004E5A70`; `movt r1,#0x4220` `0x004E5ADA`; `blx` `0x004E5AE4` | EXACT_SOURCE |
| G1.6 | type 2 (PlaceOnGround): the 4th arg is **0**; the pose is translated **-49.0** (`0xC2440000`). | branch `0x004E5AF0`; `movt r0,#0xc244` `0x004E5B0C`; `mov.w r8,#0` `0x004E5ABA`, `str.w r8,[sp]` `0x004E5B80`; `blx` `0x004E5B86` | EXACT_SOURCE |
| G1.7 | type 3 (Entry): no pose is produced (jump target is the loop tail). | `0x004E5DA2` | EXACT_SOURCE |
| G1.8 | type 4 (Rolling): the 4th arg is **75.0**. | branch `0x004E5B8C`; `movt r1,#0x4296` `0x004E5C50`; `blx` `0x004E5C5A` | EXACT_SOURCE |
| G1.9 | type 5 (Flipping): the 4th arg is **0**; angle = marker + 3pi/4; pose at `(dimX*0.5 + 56.5771, -56.5771, ...)`. | branch `0x004E5C78`; `movt r0,#0xc262` `0x004E5CB8` (0xC2624EEF = -56.5771); `mov.w r8,#0` `0x004E5D04`, `str.w r8,[sp]` `0x004E5D28`; `blx` `0x004E5D2C` | EXACT_SOURCE |
| G1.10 | `PreActionPose(ActionType, KnownMarker const*, Pose3d const&, float)` `0x0050DCFC` stores the float 4th argument at **PreActionPose+0x18**; it also copies the `Pose3d` to +8 and sets the marker at +4. | `0x0050DD46 vstr s16,[r5,#0x18]`; ctor entry `0x0050DCFC` | EXACT_SOURCE |
| G1.11 | The same ctor calls `PreActionPose::SetHeightTolerance()` `0x0050DAA4`, which writes **+0x14 = max(abs(pose translation components)) / sqrt(3)** (0.5773503 at `0x0050DB10`). So +0x14 and +0x18 are two different floats. | `0x0050DAA4`; `0x0050DB06 vstr s0,[r6,#0x14]`; constant `0x0050DB10` = `0x3F13CD3A` | EXACT_SOURCE |
| G1.12 | `PreActionPose(ActionType, KnownMarker const*, Point3 const&, float)` `0x0050DB14` builds `Point3 = -Y_AXIS * d` and forwards the stack float to the Pose3d ctor. | `0x0050DB14`..`0x0050DB5C` (`0x0050DB5C blx 0x4A74C8`) | EXACT_SOURCE |
| G1.13 | The record's **"49 mm" is not a ctor distance**: it is the y translation of the PlaceOnGround pose. The ctor 4th arg is 0 for types 2 and 5. Likewise 56.5771 for Flipping is a pose translation, not the 4th arg. | `0x004E5B0C` (0xC2440000 = -49.0); `0x004E5B80` (arg 0); `0x004E5CB8` (0xC2624EEF = -56.5771); `0x004E5D28` (arg 0) | EXACT_SOURCE |
| G1.14 | What reads `PreActionPose+0x18` was **not found** in this pass. `IDockAction::GetPreActionPoses` `0x005508C8` and `DriveToObjectAction::GetPossiblePoses` `0x00558C80` both read only the Pose3d at +8. The C# `DistanceMm` field is the 4th ctor arg. | `0x005508C8`; `0x00558C80` | RECOVERABLE_GAP: read the remaining consumers (`CrossBridgeAction::GetDockMarker2` `0x0055722C`, `IBehavior::UseSecondClosestPreActionPose` `0x005BEE56`) to settle what +0x18 controls |

**Exact mapping (four side markers, one pose per enabled face):**

| actionType | name | 4th ctor arg | pose translation offset | angle |
|---|---|---|---|---|
| 0 | Docking | 75.0 (`0x42960000`) | marker-relative | marker + pi/2 |
| 1 | PlaceRelative | 40.0 (`0x42200000`) | -100.0 | marker |
| 2 | PlaceOnGround | 0.0 | -49.0 | marker |
| 3 | Entry | — | none | — |
| 4 | Rolling | 75.0 (`0x42960000`) | marker-relative | marker |
| 5 | Flipping | 0.0 | (dimX/2 + 56.5771, -56.5771, ...) | marker + 3pi/4 |

`dimX` is the block dimension read from the virtual call at `0x004E5836` (`*(float*)(obj+4)`).
The record title "75 / 40 / 49 mm" therefore conflates the ctor distance arg (75/40/0/75/0) with a
pose offset (49). The values 75 and 40 are correct as the 4th arg for types 0/1/4; 49 is not a 4th arg.

---

## G2. M12-003 scope

**Decision: fold M12-003 into M12-005.** M12-003's title spans three claims that already have owners:
the dock-message order (M12-005), the pick-up verification (M12-007) and the docking error signal
(M12-013). Its evidence is the bare phrase "IDockAction and the firmware exchange". Keeping it would
be a duplicate claim, which the process forbids ("one owner, no duplicate claim"). The order it names
is already in M12-005's authority. The exact addresses to put on M12-005 (and to keep in the merged
record) are:

| step | what the original does | citation | class |
|---|---|---|---|
| G2.1 | `IDockAction::CheckIfDone` is the only caller of `DockingComponent::DockWithObject`. | `IDockAction::CheckIfDone` `0x005521AC`; call via PLT `0x4AB9F8` at `0x005522AE` | EXACT_SOURCE |
| G2.2 | `DockingComponent::DockWithObject` resolves the object and is the only caller of the builder. | `0x0063BA44` (`0x0063BA88 blx GetLocatedObjectByIdHelper`); builder `0x0063BD50` | EXACT_SOURCE |
| G2.3 | The builder packs the 21 bytes into a stack `DockWithObject` and sends it. | `0x0063BD50`; `0x0063BDB8 blx 0x4B94BC EngineToRobot(DockWithObject&&)`; `0x0063BDC4 blx 0x4A5368 SendMessage` | EXACT_SOURCE |

The "docking error signal formula and gates" clause is M12-013; the "pick-and-place result layout"
clause is M12-007/M12-015. If the manager prefers not to fold, the alternative is to narrow M12-003
to G2.1..G2.3 with these citations. Folding is cleaner and is what I recommend.

---

## G3. M12-002 path-packing citation (verifier FAIL)

The report's `0x00632BAE..0x00632BEE` is **inside `CarryingComponent::PlaceObjectOnGround`'s builder**
`0x00632B88`: it packs the three zero offsets (`0x00632BAE vldr s0,[r2]`, `0x00632BB2 vldr s2,[r1]`,
`0x00632BB6 vldr s4,[r3]`, `vcvt` at `0x00632BC0/C8/D0`) and the speed triple. It is not path packing.

The real production path:

| step | what the original does | citation | class |
|---|---|---|---|
| G3.1 | `PathDolerOuter::Dole` switches on the `PathSegment` type at +0 and copies the fields straight into the message struct. | `PathDolerOuter::Dole` `0x00507E4C`; `cmp r1,#1` `0x00507F40`, `cmp r1,#2` `0x00507F44`, `cmp r1,#3` `0x00507F48` | EXACT_SOURCE |
| G3.2 | line: copies `PathSegment+4..+0x10`, then `EngineToRobot(AppendPathSegmentLine&&)`. | field loads `0x00507FB6..0x00507FC4`; `0x00507FE0 blx 0x4A7234` | EXACT_SOURCE |
| G3.3 | arc: copies `+4..+0x10` and `+0x1C..+0x24`, then `EngineToRobot(AppendPathSegmentArc&&)`. | field loads `0x00508008..0x00508032`; `0x00508036 blx 0x4A7240` | EXACT_SOURCE |
| G3.4 | point turn: copies `+4..+0x10` and the bool at `+0x14`, then `EngineToRobot(AppendPathSegmentPointTurn&&)`. | field loads `0x00507F5E..0x00507F7E`; `0x00507F8E blx 0x4A7228` | EXACT_SOURCE |
| G3.5 | `PathComponent::ExecutePath` builds `ExecutePath{pathID u16, manualSpeed bool}` and sends it. | `PathComponent::ExecutePath` `0x0064A340`; `ldrh r0,[r5,#-0x12]` `0x0064A426` (pathID = `[this+0x42]`), `strh.w r0,[sp,#8]` `0x0064A430`; `strb.w r6,[sp,#0xa]` `0x0064A42A`; `0x0064A436 blx 0x4B9D8C EngineToRobot(ExecutePath&&)`; `0x0064A442 blx 0x4A5368 SendMessage` | EXACT_SOURCE |
| G3.6 | ClearPath: keep the confirmed citations (`0x00649220`, `0x00649268 movs r0,#0`, `0x0064926A strh.w r0,[sp]`, `0x00649234`/`0x0064923A`). | as before | EXACT_SOURCE |
| G3.7 | The "DriveToPose and DriveToObject constants" are the shared `PathMotionProfile` defaults. `SpeedChooser::GetPathMotionProfile` copies the 11-word rodata table at `0x00C535E0` into the profile. | `0x0053B798`; `0x0053B7A4 ldr r0,[pc,#0x1d0]` / `0x0053B7AA add r0,pc` → `0x00C535E0`; `0x0053B7AE ldm r0!,{...}` and `0x0053B7B2 ldm.w r0,{...}`; table `0x00C535E0` = 100, 200, 500, 2, 10, 10, 60, 200, 500, 80, 0 | EXACT_SOURCE |
| G3.8 | `PathMotionProfile` is consumed by `PathComponent::UpdatePlanning`, which calls `SpeedChooser::GetPathMotionProfile`. | `0x0064ACC4` (call at decomp line 149; the PLT is `0x4B9DBC`) | EXACT_SOURCE |
| G3.9 | The action ctors carry their own defaults, which are a different thing from the PathMotionProfile: `DriveToPoseAction` sets `+0x90..+0x98 = 10.0`, `+0xA8 = 4.0`, `+0xAC = 1.0`, `+0xB0 = -1.0`; `DriveToObjectAction` sets `+0x84 = -1.0`, `+0x148 = 1`, `+0x14C = 0x3E060A92` (0.1308997 rad = 7.5 deg). | `0x0055A238` (base DriveToPoseAction ctor); `0x0055850C` (DriveToObjectAction ctor) | EXACT_SOURCE; the record's phrase is ambiguous, the RobotPath.cs-relevant reading is G3.7/G3.8 |

---

## G4. M12-004 evidence (verifier FAIL)

| step | what the original does | citation | class |
|---|---|---|---|
| G4.1 | `IBehavior::UseSecondClosestPreActionPose` **starts at `0x005BEE56`** (the symbol value); `0x005BEE40` is not the entry. | symbol `0x005BEE56`; body `0x005BEE56..0x005BEE9E` | EXACT_SOURCE |
| G4.2 | It calls `DriveToObjectAction::GetPossiblePoses`; a non-zero return is passed through. | `0x005BEE66 blx 0x4ABFBC` | EXACT_SOURCE |
| G4.3 | It requires at least two poses: `(end-begin)/0xC`; if `< 2` it returns 0. | `0x005BEE6E ldrd r0,r1,[r5]`; `0x005BEE7C asrs r0,r0,#2`; `0x005BEE80 cmp r0,#2`; `0x005BEE82 itt lo / movlo r0,#0` | EXACT_SOURCE |
| G4.4 | It gets the `IDockAction` from `this+0x2C` and calls `RemoveMatchingPredockPose(pose, poses)`; when that returns 1 it clears the bool out-param. | `0x005BEE88 ldr r0,[r6,#0x2c]`; `0x005BEE90 blx 0x4B20A0`; `0x005BEE98 cmp r1,#1`; `0x005BEE9C strbeq r0,[r4]` | EXACT_SOURCE |
| G4.5 | `IDockAction::RemoveMatchingPredockPose` confirmed. | `0x00551418` | EXACT_SOURCE |

So the record's claim "drops the failed pose it just failed from and uses the second closest" is
supported: the failed pose is removed from the possible-pose list and the caller then uses the
remaining (second-closest) pose. The only correction is the entry address, `0x005BEE56`.

---

## G5. Report citation fixes

| item | report said | correct citation | class |
|---|---|---|---|
| M3 `blx 0x4a40fc` | `0x005500F0` | `0x005500F4` (`GetWithRespectTo`) | EXACT_SOURCE |
| M7 `movt r1,#0x3f32` | `0x0063C186` | `0x0063C188` (0x3F32B8C2 = 0.698132 rad = 40 deg; `0x0063C186` is `add r0,sp,#4`) | EXACT_SOURCE |
| M14 `0x04000001` | `mov.w r8,#0x4000001` | `movw r8,#1` at `0x005598E2` + `movt r8,#0x400` at `0x005598E6` | EXACT_SOURCE |
| M14 `0x03000004` (BadObject) | one literal | `movs r4,#4` at `0x005598B6` + `movt r4,#0x300` at `0x005598BA` | EXACT_SOURCE |
| M12-014 fill range | `+0xa0..+0xb1` | `+0xa0..+0xb3`: five words, timestamp `0x0063C14A`, x `0x0063C15E`, y `0x0063C174`, z `0x0063C180`, angle `0x0063C1CE`; the struct's +0x14/+0x15 (bytes +0xb4/+0xb5) are still never written | EXACT_SOURCE |

---

## G6. N8 caller correction (verifier FAIL, contradicted claim)

Confirmed: `DriveToObjectAction::CheckIfDone` `0x00559880` is **not** a caller of
`ComputePreActionPoseDistThreshold`. The callers of PLT `0x004AB938` are three, not two:

| step | what the original does | citation | class |
|---|---|---|---|
| G6.1 | `DriveToPoseAction::CheckIfDone` calls it. | `DriveToPoseAction::CheckIfDone` `0x0055AB4C`; `0x0055ACF0 blx 0x4AB938` | EXACT_SOURCE |
| G6.2 | `PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses` calls it. | `0x005558F4`; `0x005561A0 blx 0x4AB938` | EXACT_SOURCE |
| G6.3 | **Third caller the verifier did not list:** `IDockAction::GetPreActionPoses` calls it. | `IDockAction::GetPreActionPoses` `0x005508C8`; `0x00550FF8 blx 0x4AB938` | EXACT_SOURCE (NEW) |
| G6.4 | `DriveToObjectAction::CheckIfDone` has no such call. | `0x00559880` (no `blx 0x4AB938` in its body) | EXACT_SOURCE (negative) |

**Ownership: a NEW record, not M12-011.** M12-011 owns only `DriveToObjectAction::CheckIfDone`'s
`DidNotReachPreActionPose` result and its tolerance at `+0x84`; the threshold's production callers are
DriveToPoseAction, PlaceRelObjectAction and IDockAction, a distinct production step. M12-001 owns the
formula itself. The new record should list G6.1..G6.3.

---

## G7. M12-010 pattern re-read

Re-read line by line. Most of the record holds; one part of state 1 is incomplete.

| step | what the original does | citation | class |
|---|---|---|---|
| G7.1 | `SearchForNearbyObjectAction` ctor writes wait defaults 0.8 (`0x3F4CCCCD`) and 1.2 (`0x3F99999A`), angle defaults 0.261799 (`0x3E860A92`) and 0.349066 (`0x3EB2B8C2`), and stores distance/speed/head-angle at `+0x14C`/`+0x150`/`+0x154`. | ctor `0x005469C0`; `0x00546A38 movt r6,#0x3f4c`; `0x00546A6C movt r1,#0x3f99`; `0x00546A52 movt r2,#0x3e86`; `0x00546A56 movt r3,#0x3eb2`; stores `0x00546A68/70/74` | EXACT_SOURCE |
| G7.2 | `Init` draws three waits from `(0.8, 1.2)` and two turn angles from `(0.261799, 0.349066)`, plus a coin `RandDbl` against 0.5. | `Init` `0x00546C14`; waits at `0x00546C3A/3E`, `0x00546C9C/A4`, `0x00546D16/2A`; turns at `0x00546C76/7A`, `0x00546CDA/DE`; coin `0x00546CCE vcmpe.f64 d10,d9` | EXACT_SOURCE |
| G7.3 | `Init` builds: WaitAction; CompoundActionParallel(DriveStraightAction(distance,speed), MoveHeadToAngleAction(headAngle, tol 0.0349066 = `0x3D0EFA35`)); WaitAction; TurnInPlaceAction(sign*a1, tol 0.0698132 = `0x3D8EFA35`); WaitAction; TurnInPlaceAction(-sign*(a1+a2)); WaitAction. | `0x00546DD2`; `0x00546E1E`/`0x00546E34`; `0x00546E56/5C` and `0x00546E7A`; `0x00546EB0`; `0x00546ECC`, `0x00546ED2/D6`, `0x00546EE4`; `0x00546EFE`; `0x00546F1A`, `0x00546F20/24`, `0x00546F32`; `0x00546F4C` | EXACT_SOURCE |
| G7.4 | The speed is compared with 100.0 and the default-speed ctor used when it matches. | `0x00546DDE vldr s0,[pc,#0x264]` = 100.0 at `0x00547044`; `0x00546E08 vldr s2,[pc,#0x23c]` = 1e-05 at `0x00547048`; `0x00546E1E blx DriveStraightAction(Robot&,float)` vs `0x00546E34 blx DriveStraightAction(Robot&,float,float,bool)` | EXACT_SOURCE |
| G7.5 | `SearchForBlock` dispatches on `this+0x10C`: state 0 `0x005BAF9A`, state 1 `0x005BB1AA`, state 2 `0x005BB066`; when the target is not in the world it clears the delegate at `this+0x1C`. | `SearchForBlock` `0x005BAF24`; `0x005BAF9A`, `0x005BB1AA`, `0x005BB066`; `0x005BB060 str.w r0,[fp,#0x1c]` clears `+0x1C` | EXACT_SOURCE |
| G7.6 | state 0: `SearchForNearbyObjectAction(target, -20, 100, -0.0872665)` then `TurnTowardsObjectAction(target, pi)`. | `0x005BAFC2 blx`; args `0x005BAFB4` (-20 = `0xC1A00000`), `0x005BAFB0` (100 = `0x42C80000`), `0x005BAFA4` (-0.0872665 = `0xBDB2B8C2`); `0x005BB016 blx`; `0x005BAFFA/0x005BB000` (pi = `0x40490FDB`) | EXACT_SOURCE |
| G7.7 | state 2: `DriveStraightAction(-20, 20)`, two `TurnInPlaceAction(-0.785398)`, `SearchForNearbyObjectAction(-20, 100, -0.0872665)`. | `0x005BB09E`; `0x005BB0D0`/`0x005BB102` with `0x005BB0C4/CA` and `0x005BB0F6/FC` (-0.785398 = `0xBF490FDB`); `0x005BB150` | EXACT_SOURCE |
| G7.8 | state 1: `DriveStraightAction(-20, 20)`, two `TurnInPlaceAction(+0.785398)`, `SearchForNearbyObjectAction`, **then a second `DriveStraightAction`, `TurnInPlaceAction(-3pi/4)`, `TurnInPlaceAction(-0.785398)` and a second `SearchForNearbyObjectAction`**. | `0x005BB1DC`; `0x005BB214`/`0x005BB24C` (+0.785398 = `0x3F490FDB`); `0x005BB29C`; `0x005BB2DA`; `0x005BB312` (`0xC016CBE4` = -3pi/4); `0x005BB34A` (`0xBF490FDB`); `0x005BB39A` | EXACT_SOURCE; the record's state-1 text stops after "then the drive again" and omits the last two turns and the second search |
| G7.9 | `SearchFinishedWithoutInterruption` tests the object's Known byte: `object+0x24 == 1`. | `0x005BB478`; `0x005BB49E ldrb.w r0,[r0,#0x24]`; `0x005BB4A2 cmp r0,#1` | EXACT_SOURCE |
| G7.10 | The two production callers of `IHelper::CreateSearchForBlockHelper` are `DriveToHelper::RespondToDriveResult` and `PickupBlockHelper::RespondToPickupResult`. | `0x005B5C94` (call at `0x005B5D68 blx 0x4B2064`); `0x005B8050` (call at `0x005B83B6 blx 0x4B2064`) | EXACT_SOURCE; the report's `0x005B5D30` is the target-in-world check at `0x005B5D3A`, not the call site |

The record's claims about 0.8/1.2 s, 0.261799/0.349066, three waits + two angles + coin, the -20/100/
-0.0872665 and +/-0.785398 values, the Known byte, and the two delegating callers all hold. The only
gap is G7.8's omitted tail.

---

## G8. The mount/charger path

**No new M12 mount record is needed.** The engine's charger mount is `MountChargerAction`
(`0x0054E018`), `ConfigureTurnAndMountAction` (`0x0054E458`), `BackupOntoChargerAction`
(`0x0054E7A8`) and `DriveOffChargerContactsAction` (`0x00558228`) — all M13 (M13-008, M13-012,
M13-013), with the charger's one pre-dock pose in M13-009. M12's "roll and mount" is the roll:
`RollBlockHelper::StartRollingAction` `0x005B9EF8` with the 3-attempt limit (`0x005B9F0A ldr r0,[r4,#0x124]`,
`0x005B9F0E cmp r0,#3`, `0x005B9F10 blo`), which is M12-016.

| step | what the original does | citation | class |
|---|---|---|---|
| G8.1 | No charger helper exists in the helper set (PickupBlock, PlaceBlock, PlaceRelObject, RollBlock only). | `M12-016`'s evidence; the helper constructors are the four named ones | EXACT_SOURCE (negative) |
| G8.2 | The mount's one attempt and retryable result are `MountChargerAction`. | M13-008: `0x0054E2D0`, `0x0054E31A`, `0x0054E72C` | EXACT_SOURCE (owned by M13-008) |
| G8.3 | The roll's three attempts are `RollBlockHelper`. | `0x005B9EF8`; `0x005B9F0E` | EXACT_SOURCE (owned by M12-016) |

M12-016's title clause "the mount gets one" duplicates M13-008's claim. To keep one owner it should
be a pointer to M13-008 rather than a second claim. There is no `ChargerActions.cs` behaviour that
belongs to M12; that file is M13.

---

## G9. `FindObjectOnTopOrUnderneathHelper` 15.0 tolerance ownership

**M13-007 owns it.** Its title is "The stack tolerance is 30 mm when building a stack and 15 mm
everywhere else" and its authority enumerates every caller, including
`CarryingComponent::SetObjectAsAttachedToLift 0x00632F0C` (one of M12-008's steps) passing 15.0
(`0x41700000`). M12-008's evidence restates the 15.0 and points at M13-007; that is a duplicate
claim. M12-008 should cite M13-007 and drop the restatement. One owner: M13-007.

---

## Corrections to the prior report

1. **M12-001 evidence.** The per-type distances are `movw`/`movt` immediates in
   `Block::GeneratePreActionPoses`, not a table and not the 75.0 at `0x00503DC8`. The mapping is
   75 / 40 / 0 (offset -49) / none / 75 / 0 (corner). The report's "75/40/49" conflates the ctor
   argument with a pose offset. `Block::LookupBlockInfo` symbol is `0x004E4C8C`; `0x004e58fc` is the
   call site. (G1)
2. **M12-002 citation.** `0x00632BAE..0x00632BEE` is `PlaceObjectOnGround`'s builder, not path
   packing. Real path packing is `PathDolerOuter::Dole` `0x00507E4C` (line `0x00507FE0`, arc
   `0x00508036`, point turn `0x00507F8E`); ExecutePath is `0x0064A340` (build `0x0064A426`/`0x0064A42A`,
   send `0x0064A436`). The PathMotionProfile defaults are `0x0053B798` from the table `0x00C535E0`.
   (G3)
3. **M12-003 scope.** Fold into M12-005; the dock order addresses are `0x005521AC`, `0x005522AE`,
   `0x0063BD50`, `0x0063BDB8`, `0x0063BDC4`. (G2)
4. **M12-004 entry.** `UseSecondClosestPreActionPose` starts at `0x005BEE56`, not `0x005BEE40`. (G4)
5. **M12-010 state 1.** The description stops after the second `DriveStraightAction`; it omits the
   following `TurnInPlaceAction(-3pi/4)` (`0x005BB312`), `TurnInPlaceAction(-0.785398)` (`0x005BB34A`)
   and the second `SearchForNearbyObjectAction` (`0x005BB39A`). The two delegating callers are
   `0x005B5D68` (not `0x005B5D30`) and `0x005B83B6`. (G7)
6. **M12-014 fill range.** `+0xa0..+0xb3` (five words), not `+0xa0..+0xb1`. (G5)
7. **M14 `0x04000001` and `0x03000004`.** Both are built from `movw`+`movt`, not single literals:
   `0x005598E2`/`0x005598E6` and `0x005598B6`/`0x005598BA`. (G5)
8. **M7 `movt r1,#0x3f32`.** At `0x0063C188`, not `0x0063C186`. (G5)
9. **M3 `blx 0x4a40fc`.** At `0x005500F4`, not `0x005500F0`. (G5)
10. **N8.** `DriveToObjectAction::CheckIfDone` `0x00559880` is not a caller of
    `ComputePreActionPoseDistThreshold`. The callers are `0x0055ACF0` (DriveToPoseAction),
    `0x005561A0` (PlaceRelObjectAction) and the newly found `0x00550FF8`
    (`IDockAction::GetPreActionPoses`). A NEW record should own them, not M12-011. (G6)
11. **M12-001's open question.** The consumer of `PreActionPose+0x18` was not found in this pass;
    keep it as a RECOVERABLE_GAP with the functions to read. (G1.14)

*Read-only extraction. Nothing outside `.scratch/I-M12-gap1/` and this report file was changed.*




## Appendix C: gap pass 2, extractor report
# I-M12 gap pass 2 — F1/F2/F3 correction (read-only)

Job: re-analysis/jobs/I-M12.md, gap pass 2. Subsystem: M12-manipulation.
Agent: opencode (extractor), window 3. Date: 2026-09-28.
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204), Thumb-2.
Prior report: `re-analysis/research/20260928-I-M12-gap1-extraction.md`.
Method: every address below was opened in the `.so` with capstone (Thumb) and the ELF symbol
table; the Ghidra decompilation was navigation only. Addresses are file VAs. Read-only; only
this file and `.scratch/I-M12-gap2/` were written.

---

## F1 (contradicted) — G3.8's PathMotionProfile consumer

**Confirmed exactly:**

| fact | evidence |
|---|---|
| `0x0064ACC4` is `PathComponent::StartDrivingToPose(vector<Pose3d> const&, shared_ptr<unsigned char>, bool)`, not `UpdatePlanning` | symbol table `0x0064ACC4`; entry `0x0064ACC4 push.w {r4,r5,r6,r7,r8,sb,sl,fp,lr}`; `index.tsv` line `0064acc4 624 OK ... StartDrivingToPose` |
| the only call to `SpeedChooser::GetPathMotionProfile` (PLT `0x4B9DBC`) is at `0x0064ADE0`, inside `StartDrivingToPose` | `0x0064ADE0 blx #0x4b9dbc ; PLT ...GetPathMotionProfile`; whole-`.text` scan for callers of `0x4B9DBC` returns exactly one hit, `0x0064ADE0` |
| `PathComponent::UpdatePlanning` is `0x006495E0` (main body to `0x0064972E pop {r4,pc}`; `0x00649730..0x0064975C` are its exception landing pads, then unwind tables/rodata to `0x0064980B`) and contains **no** call to `0x4B9DBC` or to StartDrivingToPose's PLT `0x4ABEE4` | entry `0x006495E0 push {r4,lr}`; `index.tsv` line `006495e0 338 OK ... UpdatePlanning`; full disassembly in `.scratch/I-M12-gap2/` |
| `UpdatePlanning` does **not** call `StartDrivingToPose` (directly or indirectly). Its only caller is `PathComponent::Update` at `0x006494DA`. Its callees are `ReplanWithFallbackPlanner` (`0x006495FE blx 0x4B9D20`), `SetDriveToPoseStatus` (`0x0064960A`, `0x006496EA`, `0x006496CA`, `0x006496AE`), `Abort` (`0x006496B0 blx 0x4A7AC8`), `VizManager::ErasePath` (`0x006496C2 blx 0x4A41A4`), `sChanneledInfoF`, and on planner status 2 a tail call `0x006496DE b.w #0x8CCF1C` (Thumb veneer `0x8CCF1C bx pc` / ARM `ldr ip,[pc]` at `0x8CCF20` with literal `0x8CCF28 = 0xFFBECE00` → target `0x4B9D2C`, the PLT stub for `PathComponent::HandlePlanComplete`). | `0x006494DA blx #0x4b9d08` is the only caller of PLT `0x4B9D08`; callers scan of `0x4ABEE4` returns exactly one hit, `0x0055A998` |

**Correct production path from `StartDrivingToPose` to the PathMotionProfile:**

| step | what the original does | citation | class |
|---|---|---|---|
| F1.1 | `DriveToPoseAction::Init` calls `PathComponent::StartDrivingToPose`; this is the only call site. | `DriveToPoseAction::Init` `0x0055A86C`; `0x0055A998 blx #0x4abee4 ; PLT StartDrivingToPose` | EXACT_SOURCE |
| F1.2 | `StartDrivingToPose` calls `SpeedChooser::GetPathMotionProfile`; this is the only call site of that function. | `0x0064ADE0 blx #0x4b9dbc`; `GetPathMotionProfile` `0x0053B798` | EXACT_SOURCE |
| F1.3 | If no custom profile is set (`[this+0x48]==0`) it copies the 41-byte returned profile into the path component's profile slot `[this+0x4C]`; a custom profile skips the copy. | `0x0064ADD2 ldrb.w r0,[r4,#0x48]`; `0x0064ADD6 cbnz r0,#0x64adf2`; `0x0064ADE4 ldr r0,[r4,#0x4c]`; copy `0x0064ADE6..0x0064ADF0` | EXACT_SOURCE |
| F1.4 | The slot is the same one `SetCustomMotionProfile`/`GetCustomMotionProfile` use. | `0x0064AB90 ldr r0,[r7,#0x4c]` (Set); `0x0064AC6A ldr r0,[r4,#0x4c]` (Get) | EXACT_SOURCE |
| F1.5 | When the planner reports status 2, `PathComponent::UpdatePlanning` tail-calls `PathComponent::HandlePlanComplete`. | `0x006496DE b.w #0x8CCF1C` (veneer to PLT `0x4B9D2C`); `UpdatePlanning` `0x006495E0` | EXACT_SOURCE |
| F1.6 | `HandlePlanComplete` reads `[this+0x4C]` and passes it as the `PathMotionProfile const*` argument to `IPathPlanner::GetCompletePath`; this is that function's only call site. | `0x00649F76 ldr r1,[r4,#0x4c]`; `0x00649F7E str r1,[sp]`; `0x00649F82 blx #0x4b9d68 ; PLT IPathPlanner::GetCompletePath(Pose3d const&, Planning::Path&, unsigned char&, PathMotionProfile const*)` | EXACT_SOURCE |
| F1.7 | The planner's returned `Path` is handed to `PathComponent::ExecutePath`, which packs and sends it; the segments are packed by `PathDolerOuter::Dole`. | `0x0064A0A4 blx #0x4b9d74 ; PLT ExecutePath`; `ExecutePath` `0x0064A340` (`0x0064A436 blx 0x4B9D8C`); `PathDolerOuter::Dole` `0x00507E4C` | EXACT_SOURCE |

So `UpdatePlanning` and `StartDrivingToPose` are two ends of one path that meet at `[PathComponent+0x4C]`:
`PathComponent::Update → UpdatePlanning → (status 2) HandlePlanComplete → GetCompletePath(profile) → ExecutePath → PathDolerOuter::Dole → wire`,
with the profile loaded into `[PathComponent+0x4C]` by `DriveToPoseAction::Init → StartDrivingToPose → GetPathMotionProfile`.

**Is M12-002's "path packing and constants" live-path wiring established by it?**
Partially, and honestly:
- The **DriveToPose constants** (the `PathMotionProfile` default table at `0x00C535E0`, G3.7) are wired: produced by `GetPathMotionProfile`, stored at `[PathComponent+0x4C]`, and consumed by the planner in `GetCompletePath`, whose `Path` reaches `ExecutePath`/`PathDolerOuter::Dole`. That is a live path to the wire.
- **Not established by this path:** the **DriveToObject constants**. `DriveToObjectAction` never reaches `StartDrivingToPose`; the only caller of `StartDrivingToPose` is `DriveToPoseAction::Init`. `DriveToObjectAction` has its own constructor defaults (G3.9) and reaches poses through `DriveToObjectAction::GetPossiblePoses`.
- **Unread step:** what a concrete `IPathPlanner::GetCompletePath` implementation does with the profile (it is a virtual; the lattice/fallback planner bodies are M13). The profile is passed, but its internal effect on the produced `PathSegment` fields is not read here.

`G3.8` as written ("consumed by `PathComponent::UpdatePlanning`") is contradicted: the consumer is `StartDrivingToPose` at `0x0064ADE0`, and `UpdatePlanning` is not a caller of either `StartDrivingToPose` or `GetPathMotionProfile`.

---

## F2 (minor) — G1.6's type-2 4th-arg zero

**Confirmed:** `0x004E5ABA` is inside the type-1 branch, not type 2.
- type-1 branch is `0x004E5A4C..0x004E5AEE` (`0x004E5A4C add r6,sp,#0x38`; `0x004E5AEE b #0x004E5D4A`). `0x004E5ABA mov.w r8,#0` sits there; type 1's 4th ctor arg is 40.0, written by `0x004E5ADA movt r1,#0x4220` then `0x004E5AE0 str r1,[sp]`, and the ctor is called at `0x004E5AE4`.
- type-2 branch is entered at `0x004E5AF0`. Its 4th-arg zero is set by `0x004E5B5A mov.w r8,#0` and stored by `0x004E5B80 str.w r8,[sp]`, immediately before `0x004E5B86 blx ...PreActionPose ctor`.

**Corrected citation for G1.6's 4th arg:** `mov.w r8,#0` at `0x004E5B5A` (not `0x004E5ABA`), `str.w r8,[sp]` at `0x004E5B80`. The pose translation -49.0 cited at `0x004E5B0C` (`movt r0,#0xc244`) is correct and unchanged.

---

## F3 (precision) — PreActionPose+0x18: compaction vs value consumer

**Verifier's compaction finding is confirmed.** In `IDockAction::GetPreActionPoses` (`0x005508C8`) the vector compaction moves the whole 0x1C-byte element:
`0x00550B3C mov r6,r5`; `0x00550B52 ldrd r0,r1,[r6,#0x30]`; `0x00550B56 strd r0,r1,[r6,#0x14]`; `0x00550B5A adds r6,#0x1c`.
`r6` is the current element, so `r6+0x30` is the next element's `+0x14` and `r6+0x14` is the current element's `+0x14`: the 8 bytes `+0x14..+0x1B`, which include `+0x18`, are moved as a unit. The second compaction path (`0x00550B82 ldrd r0,r1,[r6,#0x1c]` … `0x00550B96/0x00550B9A`) does the same. This is a struct move, not a value consumer.

**But there *is* a value consumer, and it is on the M12 live path.** `ActionableObject::GetCurrentPreActionPoses` (`0x004DF850`):
- builds a local `vector<PreActionPose>` at `sp+0xBC` (`0x004DF8C8 blx #0x4a4138 ; PLT vector<PreActionPose>::insert`);
- iterates it with `sl = [sp+0xBC]` (`0x004DF8EE ldrd sl,r6,[sp,#0xbc]`) and stride `0x004DFDC4 add.w sl,sl,#0x1c`;
- reads element`+0x18` and uses it as a value in arithmetic:
  - `0x004DF9DE vldr s28,[sl,#0x18]` then `0x004DFA26 vmul.f32 s0,s0,s28` (`s0` = cos of the angle) and `0x004DFA2A vadd.f32 s0,s24,s0`;
  - `0x004DF9F4 vldr s30,[sl,#0x18]` then `0x004DFA78 vmul.f32 s0,s0,s30`;
  - `0x004DFB88 vldr s0,[sl,#0x18]` then `0x004DFB8C vcmpe.f32 s2,s0` (compare against the computed distance);
  - `0x004DFCAC vldr s26,[sl,#0x18]` and `0x004DFCC2 vldr s30,[sl,#0x18]` then `0x004DFCD8 vmul.f32 s0,s0,s26` and `0x004DFCDC vmul.f32 s2,s2,s30`, followed by `0x004DFCE0/0x004DFCE4 vsub.f32` and the result stored into the pose transform at `0x004DFCFC..0x004DFD00`;
  - it also passes the field through the copy-with-pose ctor at `0x004DF99A ldr.w r3,[sl,#0x18]`, `0x004DFBD8 ldr.w r3,[sl,#0x18]`, `0x004DFC64 ldr.w r3,[sl,#0x18]` (these are field-preserving copies).
- Live path: `DriveToObjectAction::GetPossiblePoses` (`0x00558C80`) calls `IDockAction::GetPreActionPoses` (`0x00558CDE blx #0x4ab950`); `IDockAction::GetPreActionPoses` (`0x005508C8`) calls `GetCurrentPreActionPoses` (`0x00550A3E blx #0x4a4210`). So the consumer is reachable from the M12 drive-to-object path.

**Whole-`.text` scan result.** A linear Thumb sweep of `.text` (`0x4D6860..0xAE3682`, excluding the ARM-mode Wwise region `0x95E540..0xAE2E40`) found **5,599** instructions whose memory displacement is exactly `0x18`. Filtered to `PreActionPose` elements (base advancing by `0x1C`), the only `+0x18` loads are:
- `ActionableObject::GetCurrentPreActionPoses` — the value reads above (**NEW**);
- `IDockAction::GetPreActionPoses` — the struct-move compaction only (`0x550B52/0x550B56`, and `0x550B82/0x550B96`);
- `PreActionPose::PreActionPose(PreActionPose const&)` (`0x50E138`) — `0x0050E160 ldr r0,[r4,#0x18]` / `0x0050E162 str r0,[r5,#0x18]` (struct copy);
- `vector<PreActionPose>::insert` (`0x4DFE5C`) — `0x004DFF7A ldrd r0,r1,[r5,#0x14]` (struct copy covering `+0x18`).

The `vldr s0,[sb,#0x14]` / `vldr s2,[sb,#0x18]` loads in `IDockAction::GetPreActionPoses` at `0x550F42/0x550F46`, `0x5510D6/0x5510DA`, `0x55114E/0x551152` are fields of the `PreActionPoseOutput` struct `sb` (vector at `sb+4`, index at `sb+0x10`), not of a `PreActionPose` element. `PreActionPose::GetVisualizeColor` takes an `ActionType`, not a pose; its `+0x18` hits are stack. `Block/Charger/Ramp::GeneratePreActionPoses` `+0x18` hits are producer-object geometry. `DriveToHelper::DriveToPreActionPose` `0x005B5980 ldr r0,[r0,#0x18]` is `[object+0x18]` used as a pointer base (`0x005B5982 str r1,[r0,#0x70]`), not a float read.

So `G1.14`'s statement "What reads `PreActionPose+0x18` was **not found** in this pass" is contradicted: it is read as a value in `ActionableObject::GetCurrentPreActionPoses`. The correct class is EXACT_SOURCE for the consumer; this is a NEW step (or a rewrite of G1.14), not a RECOVERABLE_GAP.

---

## What remains unread about PreActionPose+0x18

- The **identity** of `+0x18` is read: it is the 4th `PreActionPose` ctor argument (the 75/40/0/75/0 values), written at `0x0050DD46 vstr s16,[r5,#0x18]`, distinct from the `+0x14` height tolerance written by `SetHeightTolerance` (`0x0050DB06`).
- The **consumer** is read: `ActionableObject::GetCurrentPreActionPoses` uses it as a distance/radius in computing the world-space pose. What is *not* read is its exact physical name/units and whether any non-M12 consumer (a planner or behaviour outside the functions above) also reads it; the whole-`.text` sweep found no other `PreActionPose`-element `+0x18` value read, but the sweep is a linear-sweep heuristic and the planner-internal use of the profile is M13 territory.
- No other unread step for F1/F2/F3.

*Read-only extraction. Nothing outside `.scratch/I-M12-gap2/` and this report file was changed.*



## Appendix D: gap pass 3, extractor report
# I-M12 gap pass 3 (final) - H1 face-def records / H2 angle-tolerance floor (read-only)

Job: re-analysis/jobs/I-M12.md, gap pass 3. Subsystem: M12-manipulation.
Agent: opencode (extractor), window 3. Date: 2026-09-28.
Prior reports: `re-analysis/research/20260928-I-M12-gap1-extraction.md` and `...-gap2-extraction.md`.
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204), Thumb-2.
Method: every address below was opened in the `.so` with capstone (Thumb) and the ELF symbol /
Ghidra index; the Ghidra decompilation was navigation only. Addresses are file VAs. The
rodata words were read from the file at their VAs. Read-only; only this file and
`.scratch/I-M12-gap3/` were written.

The two open details are both settled. H1 is EXACT_SOURCE. H2 is EXACT_SOURCE and the
C# comment's "floor" does **not** exist as a floor value (the only check is a positivity
guard that returns the -1 sentinel).

---

## H1. The six block face-def records and the per-face mask

**Result.** The records are **copied verbatim from rodata**, not built at runtime.
`Block::LookupBlockInfo` 0x004E4C8C builds a function-local static map on first call
(guard `DAT_010590a8`); each map entry holds a `vector<Block::BlockFaceDef_t>` at `info+0x20`
whose six 16-byte records are `ldm`/`stm`-copied from the rodata tables at 0x00C45C40
(LIGHTCUBE1), 0x00C45CA0 (LIGHTCUBE2), 0x00C45D00 (LIGHTCUBE3) and 0x00C45D60
(LIGHTCUBE_GHOST). Each record is
`{ u32 faceName @0; u32 markerType @4; float size @8; u8 maskForType0And5 @0xC; u8 maskForType4 @0xD; u16 pad @0xE }`.
**There is no face-name string and no string pointer in the record.** The "face name" is
the `Block::FaceName` enum integer at +0x0; the +0x4 word is the `Vision::MarkerType` code.

### H1 steps

| step | what the original does | citation | class |
|---|---|---|---|
| H1.1 | `LookupBlockInfo` builds a process-static map on first call, guarded by `DAT_010590a8`; on the guard-taken path it populates it, on every later call it only walks it. | entry `0x004E4C8C`; guard `0x004E4C94 ldr.w r0,[pc,#0x674]` (->0x00B7440C+pc), `0x004E4CA0 tst.w r0,#1`, `0x004E4CA8/0x004E4CAE __cxa_guard_acquire`, `0x004E4CB4 beq 0x4e50bc` | EXACT_SOURCE |
| H1.2 | Four entries, keys 1..4, named `LIGHTCUBE1`/`LIGHTCUBE2`/`LIGHTCUBE3`/`LIGHTCUBE_GHOST`. The record's three named tables omit GHOST. | name build `0x004E4CC4 addw r1,pc,#0x64c`/`0x004E4CC8 movs r2,#0xa`/`0x004E4CCC bl 0x4e02b2`; key stores `0x004E4D28 movs r0,#1`->`[sp,#0xc0]`, `0x004E4DEE movs r0,#2`->`[sp,#0xf0]`, `0x004E4EA4 movs r0,#3`->`[sp,#0x120]`, `0x004E4F58 movs r0,#4`->`[sp,#0x150]` | EXACT_SOURCE; **GHOST is NEW** relative to the record's three tables |
| H1.3 | Each entry's face-def vector is six 16-byte records copied verbatim from rodata. LIGHTCUBE1 base 0x00C45C40, LIGHTCUBE2 0x00C45CA0, LIGHTCUBE3 0x00C45D00, GHOST 0x00C45D60. | LIGHTCUBE1: `0x004E4CF8 ldr.w r2,[pc,#0x628]` (=0x00760F34) / `0x004E4D08 add r2,pc` (pc=0x004E4D0C) -> 0x00C45C40; copy loop `0x004E4D12 adds r3,r2,r1` / `0x004E4D18 ldm.w r3,{r4,r5,r6,r7}` / `0x004E4D1C stm r0!,{r4,r5,r6,r7}` / `0x004E4D14 adds r1,#0x10` / `0x004E4D16 cmp r1,#0x60` / `0x004E4D26 bne`. LIGHTCUBE2 `0x004E4DB2`+`0x004E4DC2`->0x00C45CA0, copy `0x004E4DD2/0x004E4DD6`. LIGHTCUBE3 `0x004E4E68`+`0x004E4E78`->0x00C45D00, copy `0x004E4E88/0x004E4E8C`. GHOST `0x004E4F1C`+`0x004E4F2C`->0x00C45D60, copy `0x004E4F3C/0x004E4F40`. | EXACT_SOURCE |
| H1.4 | The 16-byte record layout, from the rodata bytes and from `Block::Block`'s use: +0x0 FaceName, +0x4 MarkerType code, +0x8 float size 25.0, +0xC/+0xD the two mask bytes, +0xE/+0xF zero. `Block::Block` passes +0x0 as `FaceName`, `&record+0x4` as `MarkerType const&`, +0x8 as the size float; it does **not** pass +0xC/+0xD. | rodata bytes at 0x00C45C40..; `Block::Block` 0x004E5F50: `0x004E5FF8 ldrd r2,r5,[r0,#0x20]`, `0x004E6002 ldr r1,[r2]`, `0x004E6004 adds r7,r2,#4`, `0x004E6006 ldr r3,[r2,#8]`, `0x004E6010 blx AddFace`; `AddFace` 0x004E53BC `0x004E564C ldr.w r1,[r8]`/`0x004E5654 strh.w r1,[sp,#0x2c]` reads only the low short of +0x4 | EXACT_SOURCE |
| H1.5 | No string and no string pointer exist in the record. `Block::GetMarker` takes a `FaceName` and indexes `Block+0x70+faceName*4`; the enum has six values (0..5), clamped at 6. | `GetMarker` 0x004E5E98 (`this + param_2*4 + 0x70`); `operator++(FaceName&)` 0x004E699E `movs r1,#6`; `operator++(FaceName&,int)` 0x004E69AC `movs r1,#6` | EXACT_SOURCE (negative) |
| H1.6 | Caller `Block::GeneratePreActionPoses` reads the vector at `info+0x20`, calls `GetMarker(block, record+0x0)`, then, inside the inner `sb = 0..3` loop, tests `(1<<sb) & mask`: mask at +0xC for action types 0 and 5, mask at +0xD for type 4. A clear bit skips that face/rotation (jump to the loop tail). | vector `0x004E5900 ldrd r1,r0,[r0,#0x20]`; `GetMarker` `0x004E5942 blx 0x4a44c8`; `sb` init `0x004E594A mov.w sb,#0`; type-0 `0x004E5970 movs r1,#1`/`0x004E5972 lsl.w r1,r1,sb`/`0x004E5976 ldrb r0,[r0,#0xc]`/`0x004E5978 tst r1,r0`/`0x004E597A beq 0x4e5da2`; type-4 `0x004E5B90 lsl.w r1,r1,sb`/`0x004E5B94 ldrb r0,[r0,#0xd]`/`0x004E5B98 beq 0x4e5da2`; type-5 `0x004E5C7C lsl.w r1,r1,sb`/`0x004E5C80 ldrb r0,[r0,#0xc]`/`0x004E5C84 beq 0x4e5da2`; loop tail `0x004E5DA2 add.w sb,sb,#1`/`0x004E5DAA cmp.w sb,#4`/`0x004E5DAE blo 0x4e5958` | EXACT_SOURCE |
| H1.7 | Action types 1 (PlaceRelative) and 2 (PlaceOnGround) have **no mask test** at all; type 3 (Entry) produces nothing. So the mask gates only types 0, 4 and 5. | type-1 entry `0x004E5A4C` (first insn `add r6,sp,#0x38`, no `ldrb`); type-2 entry `0x004E5AF0` (first insn `add.w sl,sp,#0x2c`, no `ldrb`); type-3 entry `0x004E5DA2` (loop tail) | EXACT_SOURCE |
| H1.8 | The `sb` rotation table is four `RotationVector3d` at 0x0105B0AC (0 rad), 0x0105B0C0 (pi/2), 0x0105B0D4 (pi), 0x0105B0E8 (-pi/2), indexed by `fp` (advanced 0x14 per `sb`). | build `0x004E5868`/`0x004E587A`/`0x004E5882`/`0x004E589A`/`0x004E58A4`/`0x004E58B8`/`0x004E58C6`/`0x004E58DA`; `fp` init `0x004E5914 ldr.w fp,[pc,...]` / `0x004E5920 add fp,pc`; advance `0x004E5DA6 add.w fp,fp,#0x14` | EXACT_SOURCE |

### The six records (verbatim rodata)

Record order is the vector order; +0x0 is the `FaceName`, +0x4 the marker code, +0x8 = `0x41C80000` = 25.0f.

| cube / table | rec | +0x0 FaceName | +0x4 marker code | +0x8 | +0xC (types 0,5) | +0xD (type 4) |
|---|---|---|---|---|---|---|
| LIGHTCUBE1 @0x00C45C40 | 0 | 0 | 6 | 25.0 | 0x05 | 0x0F |
| | 1 | 2 | 4 | 25.0 | 0x05 | 0x0F |
| | 2 | 1 | 7 | 25.0 | 0x05 | 0x0F |
| | 3 | 3 | 8 | 25.0 | 0x05 | 0x0F |
| | 4 | 4 | 9 | 25.0 | 0x00 | 0x0F |
| | 5 | 5 | 5 | 25.0 | 0x0F | 0x0F |
| LIGHTCUBE2 @0x00C45CA0 | 0 | 0 | 12 (0xC) | 25.0 | 0x05 | 0x0F |
| | 1 | 2 | 10 (0xA) | 25.0 | 0x05 | 0x0F |
| | 2 | 1 | 13 (0xD) | 25.0 | 0x05 | 0x0F |
| | 3 | 3 | 14 (0xE) | 25.0 | 0x05 | 0x0F |
| | 4 | 4 | 15 (0xF) | 25.0 | 0x00 | 0x0F |
| | 5 | 5 | 11 (0xB) | 25.0 | 0x0F | 0x0F |
| LIGHTCUBE3 @0x00C45D00 | 0 | 0 | 18 (0x12) | 25.0 | 0x05 | 0x0F |
| | 1 | 2 | 16 (0x10) | 25.0 | 0x05 | 0x0F |
| | 2 | 1 | 19 (0x13) | 25.0 | 0x05 | 0x0F |
| | 3 | 3 | 20 (0x14) | 25.0 | 0x05 | 0x0F |
| | 4 | 4 | 21 (0x15) | 25.0 | 0x00 | 0x0F |
| | 5 | 5 | 17 (0x11) | 25.0 | 0x0F | 0x0F |
| LIGHTCUBE_GHOST @0x00C45D60 | 0 | 0 | 39 (0x27) | 25.0 | 0x0F | 0x0F |
| | 1 | 2 | 39 | 25.0 | 0x0F | 0x0F |
| | 2 | 1 | 39 | 25.0 | 0x0F | 0x0F |
| | 3 | 3 | 39 | 25.0 | 0x0F | 0x0F |
| | 4 | 4 | 39 | 25.0 | 0x0F | 0x0F |
| | 5 | 5 | 39 | 25.0 | 0x0F | 0x0F |

The raw 16 bytes are e.g. LIGHTCUBE1 rec0 `00 00 00 00 06 00 00 00 00 00 C8 41 05 0F 00 00`.

### Which faces are enabled

`1<<sb`, `sb` = 0..3 = Z rotations 0, pi/2, pi, -pi/2. Mask 0x05 = bits {0,2}; 0x0F = {0,1,2,3}; 0x00 = none.

| action type | mask source | enabled faces / rotations (LIGHTCUBE1/2/3) | enabled (GHOST) |
|---|---|---|---|
| 0 Docking | +0xC | FaceName 0,1,2,3: rotations {0,2}; FaceName 5: rotations {0,1,2,3}; FaceName 4: none | all six faces, all four rotations |
| 4 Rolling | +0xD | all six faces (0,1,2,3,4,5), all four rotations (0x0F everywhere) | all six faces, all four rotations |
| 5 Flipping | +0xC | FaceName 0,1,2,3: rotations {0,2}; FaceName 5: rotations {0,1,2,3}; FaceName 4: none | all six faces, all four rotations |

So for the three real cubes: type 0 and type 5 enable the same set; the `+0x0C` mask disables
FaceName 4 entirely and halves the rotations of FaceName 0..3, while FaceName 5 gets all four.
Type 4 (Rolling) is enabled on every face and every rotation. Types 1 and 2 are ungated.

**H1 class: EXACT_SOURCE.** Nothing left unread on H1.

---

## H2. `ComputePreActionPoseDistThreshold` 0x00550098 - is there an angle-tolerance floor?

**Result. No.** There is no compare, clamp, min, max or branch on the angle-tolerance *value*
before the `sinf` other than a single positivity guard: the function constructs `Radians(0)`
and calls `operator>(angleTolerance, Radians(0))`; when that is false it writes the `-1.0f`
sentinel pair and returns. There is no floor value substituted for the tolerance, and the
float fed to `sinf` is the caller's tolerance itself, unmodified.

### H2 steps

| step | what the original does | citation | class |
|---|---|---|---|
| H2.1 | Entry 0x00550098, body 0x00550098..0x00550215, **382 bytes**; epilogue `add sp,#0x30` / `vpop {d8,d9,d10}` / `add sp,#4` / `pop.w {r4,r5,r6,r7,r8,sb,pc}`; return at `0x00550212`. | `0x00550098 push.w {r4,r5,r6,r7,r8,sb,lr}`; `0x0055020A`/`0x0055020C`/`0x00550210`/`0x00550212`; `index.tsv` body `00550098..00550215` | EXACT_SOURCE |
| H2.2 | Arguments: `r0` = out `float[2]`, `r1` = pose1, `r2` = pose2, `r3` = `Radians const& angleTolerance`. No float is passed in `s0`. | `0x005500A8 mov r4,r0`, `0x005500A6 mov sb,r1`, `0x005500B0 mov r7,r2`, `0x005500AE mov r8,r3`; all three callers set only r0..r3 (PlaceRel `0x0055619A`..`0x005561A0`, IDock `0x00550FF4`..`0x00550FF8`, DriveToPose `0x0055ACEE`..`0x0055ACF0`) | EXACT_SOURCE |
| H2.3 | It builds `aRStack_44 = Radians(0.0f)` (r0=sp+0x24, r1=0 -> the float is in r1). | `0x005500A4 add r5,sp,#0x24`; `0x005500AA movs r1,#0`; `0x005500AC mov r0,r5`; `0x005500B4 blx 0x4a4294`; `Radians::Radians(this,float)` 0x0084C832 stores `in_r1` into `this` | EXACT_SOURCE |
| H2.4 | **The only tolerance branch:** `operator>(angleTolerance, Radians(0))`; if false, output = `(-1.0f, -1.0f)`. | `0x005500B8 mov r0,r8`; `0x005500BA mov r1,r5`; `0x005500BC blx 0x4a4528`; `0x005500C0 cmp r0,#0`; `0x005500C2 beq 0x5501ae`; `0x005501AE movs r1,#0`/`0x005501B0 movs r0,#0`/`0x005501B2 movt r1,#0xbf80`/`0x005501B6 str r1,[r4,r0]`/`0x005501B8 adds r0,#4`/`0x005501BA cmp r0,#8`/`0x005501BC bne 0x5501b6` | EXACT_SOURCE |
| H2.5 | The only other branch is the `GetWithRespectTo` failure path (also `-1.0f` pair); it does not depend on the tolerance value. | `0x005500F4 blx 0x4a40fc`; `0x005500F8 cmp r0,#0`; `0x005500FA beq 0x5501c0`; `0x005501C0`..`0x00550202` writes `0xBF800000` to `out[0]`/`out[1]` | EXACT_SOURCE |
| H2.6 | **No clamp / min / max / floor.** After the guard, the tolerance float is loaded and handed straight to `sinf`; the result is multiplied by the distance. | `0x0055013C ldr.w r0,[r8]`; `0x00550140 blx 0x4a4168` (`sinf`); `0x00550144 vmov s0,r0`; `0x0055014E vmul.f32 s16,s20,s0` | EXACT_SOURCE |
| H2.7 | `operator>` (0x0084CC90) itself: if `p1 - p2 <= 0` -> 0; else `!(IsNear(p1,p2,Radians(0x3727C5AC)))`. `0x3727C5AC` = `9.9999997e-06` rad, so the guard is effectively `angleTolerance > ~1e-5 rad`. This is the closest thing to a "floor". | `0x0084CC90`; `local_18 = 0x3727c5ac`, `local_14 = 1`; `Radians::rescale`; `IsNear`; `uVar1 = uVar1 ^ 1`; `operator>` thunk `0x004A4528` | EXACT_SOURCE |
| H2.8 | Out-parameter layout: `out+0` = `2 * dist * sin(tol)`; `out+4` = `dist * sin(tol)`. | `0x00550164 vadd.f32 s18,s16,s16`; `0x005501A4 vstr s18,[r4]`; `0x005501A8 vstr s16,[r4,#4]` | EXACT_SOURCE |
| H2.9 | The distance is the **3-D** norm of the relative transform's translation: sum of squares of the three floats at +0x20/+0x24/+0x28, then `sqrtf`. This corrects gap1's "planar distance" and the C# `DistanceThresholdMm`, which uses X and Y only. The translation triple at +0x20/+0x24/+0x28 is the `Transform3d` translation. | `0x00550102 vldr s0,[r0,#0x20]`; `0x00550106 adds r0,#0x24`; `0x0055010E adds r2,r0,r1`; `0x00550114 vldr s2,[r2]`; `0x0055011C vadd.f32 s0,s0,s2`; `0x00550122 vsqrt.f32 s20,s0`; translation layout confirmed by `Transform3d::TranslateForward` 0x0084B92A (`local_20[0..2] = [in_r0+0x20/+0x24/+0x28]`) and `PreActionPose::SetHeightTolerance` 0x0050DAB2 (`add.w r1,r0,#0x20`; `ldm.w r1,{r3,r4,r5}`) | EXACT_SOURCE |

### What the C# comment could have misread

`cozmo-stack/src/Cozmo.Robot/Manipulation/PreActionPose.cs:89-90` says
"The engine takes the angle tolerance itself when it is above a floor; the floor value was not
read (LOCAL: none)." The function has exactly one tolerance-dependent decision, H2.4/H2.7:
`angleTolerance > Radians(0)` (with `operator>`'s ~1e-5 rad epsilon). That is a
**positivity guard that returns the `-1.0f` sentinel**, not a floor that is substituted into
the formula. There is no floor constant anywhere between the prologue and `sinf`. The
plausible misreading is of the `Radians::Radians(aRStack_44, 0.0f)` + `operator>` prologue
(which looks like a floor test) and/or of `operator>`'s `0x3727C5AC` epsilon. The comment's
"the engine takes the angle tolerance itself" is correct; the implied non-zero floor is not.

**H2 class: EXACT_SOURCE.** Nothing left unread on H2.

---

## Record notes (not part of the two questions)

- **gap1 G1.3's "planar distance" is contradicted.** The binary sums three translation
  components (+0x20/+0x24/+0x28), so the distance is 3-D. The C# `DistanceThresholdMm`
  (`PreActionPose.cs:94-96`) uses X/Y only. This is a live-path fidelity discrepancy inside
  the function M12-001/M12-011 rely on; it needs an inventory correction before M12 is settled.
- **M12-001's evidence is thin** (`"pre-action pose table and the actions that ask for each type"`).
  The per-type distances are `movw`/`movt` immediates in `Block::GeneratePreActionPoses`
  (gap1 G1), and the face/rotation gate is H1 above. The record should cite these, not a
  bare "table".
- The face-def record's +0xC/+0xD mask is a per-face, per-rotation gate that only
  `GeneratePreActionPoses` reads; `Block::Block`/`AddFace` pass `0,0` and ignore it (H1.4/H1.7).
  No existing M12/M11 record owns it.

## Nothing left unread

H1 and H2 are both fully read; the two details the job named are settled. The only follow-on
item is the gap1/C# "planar" discrepancy in H2.9, which is a manifest/inventory correction
(the source fact is now established), not an unread gap.

*Read-only extraction. Nothing outside `.scratch/I-M12-gap3/` and this report file was changed.*


## Appendix E: B-M12 build gap pass, extractor report (2026-09-28)

The build left four MISSING items and one ambiguity. This pass answered them (report at
`.scratch/B-M12-gap/extraction.md`). Binary as above; every address opened with capstone and the ELF
symbol / `.rel.plt` tables; the Ghidra decompilation was navigation only.

### E1. The world-pose formula in `ActionableObject::GetCurrentPreActionPoses` 0x004DF850 (M12-001)

The element is 0x1C bytes: +0 ActionType, +4 `KnownMarker const*`, +8 `Pose3d`, +0x14 height
tolerance, +0x18 the 4th ctor arg. The world pose is built by the copy-with-pose ctor
`PreActionPose(PreActionPose const&, Pose3d const&, float a, float b)` 0x0050DF00 (PLT 0x4A4144):

- `0x0050DF54 vstr s16,[sl,#0x18]` — `dest+0x18 = a`
- `0x0050DF6C blx 0x4A74F8` — `Point3::MakeUnitLength` 0x0050E0C0, returns `|t|`
- `0x0050DF7C vadd.f32 s0,s2,s0` — `|t| + b`; the unit vector is scaled by it
- `0x0050DFE4 blx 0x4A7510` — `operator*(Pose3d const&, Pose3d const&)` 0x0084782C
- `0x0050E036 blx 0x4A74E0` — `SetHeightTolerance`

so `world = world(objectPose) * Pose3d(src.pose.rotation, unit(src.pose.translation) * (|src.pose.translation| + b))`.
Call site 0x004DF99A passes `a = element+0x18`, `b = param_7 = *(float*)(robot+0x10)` — named
`preDockPoseOffset_mm` by the function's own debug string at 0x0055137A; `param_8 = 0` there
(0x00550A2A). When `param_7 == 0` (0x004DF9AC vcmp / 0x004DF9B4 bne), the block
0x004DF9B8..0x004DFC16 computes `b = min(dist2, element+0x18)` (`dist2` is the perpendicular
projection of `out2` onto the line through `out1` with direction `(dx,dy)`, 0x004DFA32..0x004DFB46;
the cap at 0x004DFB88/0x004DFB94) and calls the ctor a second time at 0x004DFBE6. The stores at
0x004DFCFC..0x004DFD00 are the `param_8 == 1` visualization path (0x004DFC5E cmp r0,#1), which does
not run on the production path.

### E2. `IsPoseTooHigh` (M12-012)

`ObservableObject::IsPoseTooHigh(Pose3d const&, float f1, float f2, float f3)` 0x00877954 (PLT
0x4ACFF4), body 0x00877954..0x008779CC:

- `0x00877978` — `D = GetDimInParentFrame<'Z'>(this, pose.rotation)` (PLT 0x4AB9BC -> 0x00557794)
- `s0 = D*f1`; `s16 = D*f3`; `s18 = D*f1 + f2`
- `s0 = pose.translation.z` (`[r0+0x28]`); `0x008779AA vadd.f32 s0,s0,s16` = `D*f3 + pose.z`
- `0x008779AE` adds the `1e-5` constant 0x3727C5AC to `s18`; `0x008779B2 vcmpe.f32 s2,s0`;
  `0x008779BA movmi r0,#1`

Return 1 iff `D*f1 + f2 + 1e-5 < D*f3 + pose.z`. With the caller's `f1=1.0, f2=15.0, f3=0.5`:
`too_high == pose.z > 0.5*D + 15.0 + 1e-5`. `CanStackOnTopOfObject` returns
`CanInteractWithObjectHelper && !IsPoseTooHigh`.

### E3. The docking squint (M12-017) — attribution corrected

`IDockAction::Init` 0x005514FC (body to 0x00551C8A) contains no call to the `AddSquint` PLT
0x4ABA04. The call at 0x00552394 is inside `IDockAction::CheckIfDone` 0x005521AC (body
0x005521AC..0x005523F6). Arguments: `TrackLayerComponent` at `robot+0xC0` (0x00552362), name
`"DockSquint"` (inline literal at 0x005524A0, 10 bytes), `f1 = 0x3F866666 = 1.05`
(0x0055237A/0x00552388), `f2 = 0x3EB33333 = 0.35` (0x0055237E/0x0055238C), `f3 = 0xC1200000 = -10.0`
(0x00552378/0x00552382/0x00552386); the returned bool is stored at `IDockAction+0xF4` (0x00552398).
`TrackLayerComponent::AddSquint` 0x0064F370 -> `FaceLayerManager::GenerateSquint` 0x0058D738 (PLT
0x4BA164) then `AddPersistentLayer` 0x4AE9E0. `GenerateSquint` ignores its float arguments and
clips `EyeScaleY = 0.35`, `EyeScaleX = 1.05`, `UpperLidAngle = -10.0` on both eyes; the track's second keyframe is at 250 ms (`0xFA`) with `Reset` (0x0058D820/0x0058D82C).

### E4. The threshold outputs (M12-020)

`ComputePreActionPoseDistThreshold` writes `out[0] = 2*dist*sin` (0x00550164/0x005501A4) and
`out[1] = dist*sin` (0x005501A8). All three production callers load **both** with one `ldrd`:

- `DriveToPoseAction::CheckIfDone` 0x0055ACF4 `ldrd r0,r1,[sp,#0x80]` -> `Point3 {out[0], out[1],
  GetHeight}` at sp+0x8c (0x0055ACF8 `strd`), used as the x/y position tolerance of
  `Pose3d::IsSameAs` 0x4A7060.
- `PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses` 0x005561B0 `ldrd r0,r1,[sp,#0x54]` ->
  `Point3 {out[0], out[1], 100.0}` (0x005561B4/0x005561BC), `IsSameAs` with `Radians(0.1308997)`.
- `IDockAction::GetPreActionPoses` 0x00550FFC `ldrd r1,r2,[sp,#0x70]` -> `sb+0x20`/`sb+0x24`
  (0x00551002/0x00551008); both must be > 0 (0x0055101C/0x00551048) and are compared against the
  robot's `|dx|`/`|dy|` to the closest pose, failing with 0x04000001.

### E5. `SearchForBlockHelper::SearchForBlock` state 0 and state 2 (M12-010)

- `ICompoundAction::AddAction` 0x0054ED90 appends (`std::list::emplace_back` 0x0054EE28) to the list
  at `this+0x70`; `CompoundActionSequential::UpdateInternal` 0x0054F70C runs it front-to-back. State
  0 adds the `TurnTowardsObjectAction` first (0x005BB024) and the nearby search second (0x005BB03E),
  so execution is **turn then search**. The turn is added only when the target id is set
  (0x005BAFDA `beq`); the search unconditionally.
- State 1 tail signs: `TurnInPlace(-3pi/4)` 0x005BB312 (0xC016CBE4) and `TurnInPlace(-0.785398)`
  0x005BB34A (0xBF490FDB); state 1 sets `[this+0x10C] = 2` at 0x005BB3C4.
- State 2 (0x005BB066..0x005BB1A8) is a **two-iteration loop** (r8 = -1 at 0x005BB078;
  0x005BB16E `add r8,#1`; 0x005BB176 `blt 0x5bb084`) of `DriveStraight(-20,20)`;
  `TurnInPlace(-0.785398)`; `TurnInPlace(-0.785398)`; `SearchForNearbyObjectAction(-20,100,-0.0872665)`.
  It has no state-1 tail and does not update `[this+0x10C]`.

### E6. The release/attach gate (M12-006)

`RobotToEngineImplMessaging::HandlePickAndPlaceResult` 0x00533780 (body to 0x0053391C). Layout: +4
`success` (0x00533794), +5 `dockingResult` (0x005337F8), +6 `blockStatus` (0x0053379E).

- BlockPlaced (0x005337A8/0x005337AA): release only when `success != 0` (0x00533898/0x0053389A),
  `SetCarriedObjectAsUnattached(false)` (0x005338A0/0x005338A2 blx 0x4A7B7C). `EnableMode(1,true)`
  at 0x005338AE runs unconditionally in the branch.
- BlockPickedUp (0x005337A4/0x005337A6): attach only when `success != 0` (0x00533848/0x0053384A),
  `SetDockObjectAsAttachedToLift()` (0x00533850 blx 0x4AA06C).

*Read-only extraction. Nothing outside `.scratch/B-M12-gap/` and this report file was changed.*

### E9. Verification follow-up (M12-001, M12-020, M12-021; Q11..Q13)

A verifier pass contradicted the generation mapping; this pass confirms the verifier.

- **The four `sb` rotations are about `Y_AXIS_3D`** (PLT 0x4A47B0), table base 0x010590AC (0x004E5876/0x004E587A -> 0x010590AC), angles 0 / 0x3FC90FDB / 0x40490FDB / 0xBFC90FDB, applied with `Transform3d::RotateBy` 0x0084BB3A at 0x004E5A1C/0x004E5AC0/0x004E5B60/0x004E5C36/0x004E5D0A. The inventory's "Z rotations" and table 0x0105B0AC were wrong.
- **The per-type stored pose** is `Pose3d(angle, Z_AXIS, translation)` with the marker's pose as the 4th ctor argument (parent): Docking angle pi/2, {0,-65,-22}; PlaceRelative pi/2, {0,-100,-22}; PlaceOnGround pi/2, {0,-49,-22}; Rolling pi/2, {0,-65,-22}; Flipping 3pi/4, {dimX/2+56.5771, -56.5771, -22}; with -22 = -size[8]/2 (precompute 0x004E5938) and dimX = size[4]. The `PreActionPose` ctor 0x0050DCFC re-roots to the marker's parent (0x0050DDB2), so the stored pose is `marker.local ∘ builtPose`. The earlier "marker + pi/2" angles for types 1/2/4 and the "marker-relative / zero" translations for types 0/4 were wrong.
- **`FlipBlockAction::Init` 0x0055EDC8** calls `IDockAction::GetPreActionPoses` at 0x0055EE5E, so its pre-action check uses the threshold pair out[0]/out[1], not a fixed 100 mm box.
- **`Pose3d::IsSameAs` 0x00846EA4** uses a default-constructed empty `RotationAmbiguities` (0x0084B496: begin=end=0) and compares the full relative rotation (`GetAngleDiffFrom` 0x0084A694, `acos(2·dot²-1)`); no yaw flattening.
- **`Robot::GetHeight` 0x00516F0C** = `max(66·sin(liftAngle) + 45 + 5, 67.7)` (66 at 0x00516F54, 45 at 0x00516F58, 67.7 = 0x42876666).

### E8. The two poses `GetCurrentPreActionPoses` uses (M12-001, follow-up)

`param_2` (saved at 0x004DF8EA) is the **robot's world pose**: at the production call site
`IDockAction::GetPreActionPoses` 0x00550A3E it is `FUN_004EA398(robot)` (0x004EA398), which returns
`&(Robot+0x298)` (the robot pose built by `Robot::Robot` 0x0050FEA4, used by `Robot::SetPose`
0x00515042 and `Robot::GetWorldOrigin`), after checking its root is the world origin. `objectPose` is
`ObservableObject::GetPose` (`&(ObservableObject+4)`, 0x00876840) — the object's own pose, distinct
from `param_2`. `FUN_004DF628` returns a pose relative to its own root (`FindRoot` +
`GetWithRespectTo`), i.e. in the world frame for both. So the returned world pre-action pose is
`world = world(objectPose) * Pose3d(element.rotation, unit(element.translation) * (|element.translation| + b))`,
with `b` the `preDockPoseOffset_mm` argument when non-zero and otherwise
`min(‖projection of the robot pose onto the offset line‖, element+0x18)`, where the offset line runs
from the offset-0 pose in the direction `(cos(angle), sin(angle)) * element+0x18`.

### E7. The `preDockPoseOffset_mm` value (M12-001, follow-up)

The float read at 0x00550984 (`vldr s16,[r7,#0x10]`) is `PreActionPoseInput+0x10` (r7 = r1, the
input struct), not `IDockAction+0x10`; `IDockAction::GetPreActionPoses` takes the `Robot` in r0 and
the input in r1 (all eight call sites). The `IDockAction` ctor 0x005502D8 leaves +0x10 = 0
(`IActionRunner::IActionRunner` 0x0053FDD0 `strd r6,r6,[r5,#0x10]`), and no shipped function writes
`IDockAction+0x10` after construction. The input's +0x10 is filled inline: `IDockAction::Init`
0x0055161A uses `IDockAction+0xBC` (0 at construction), the two `DriveToObjectAction` paths use
`DriveToObjectAction+0x88`, and `PlaceRelObjectAction` 0x00555906, `DriveAndFlipBlockAction`
0x0055E490, `FlipBlockAction::Init` 0x0055EE2E and `IHelper` 0x005B7070 pass 0;
`BehaviorDriveInDesperation` 0x005D9918 passes `0x3E860A92` (0.2617994).

`DriveToObjectAction+0x88` is the ctor's 4th (first float) argument (0x005585A4 `vstr s16,[r4,#0x88]`;
the 5-arg ctor 0x005587B8 sets it at 0x00558850). There is no setter (`SetApproachAngle` 0x00558A90
writes +0x13C/+0x140), and every direct caller of the 7-arg ctor passes 0 (0x0054E82E, 0x005B5880,
0x005CBE74, 0x00559CF2, 0x0055EB20); only `IDriveToInteractWithObject` 0x0055B1F4 forwards its own
float argument. So `preDockPoseOffset_mm = 0` on every path this stack builds, and the offset-0
branch (`b = min(dist2, element+0x18)`) is the one that runs.
