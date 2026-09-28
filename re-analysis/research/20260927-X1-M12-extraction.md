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
