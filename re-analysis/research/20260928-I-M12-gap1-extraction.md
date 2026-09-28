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


