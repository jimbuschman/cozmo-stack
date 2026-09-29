# R-DEV M4-control gap pass 1: M4-009 DockingComponent+0xC, M4-018 robot+0x2C4, M4-016 lift eye-shift

- **Answers:** gap pass for job R-DEV (M4-control), questions 1..3.
- **Date:** 2026-09-29
- **Source:** `resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF, ARMv7; engine functions Thumb, PLT/OKAO/breakpad ARM). Read with capstone; Ghidra decomp used only to navigate.
- **Method:** all three answers were read directly from the `.so`. Addresses are virtual addresses in the manifest's load space. A `blx #0x4a....` is an ARM PLT stub unless stated otherwise.
- **Records in scope:** M4-009 (CD10a/CD10b), M4-018 (S7), M4-016 (lift rows L1..L6 / C6 and head H4..H7).

Status: complete. All three questions are answered from primary source; no UNKNOWN remains on the questions asked. Two small scan-limit caveats are recorded at the end.

---

## Question 1 (M4-009 CD10a/CD10b): `DockingComponent+0xC`

`[robot+0x280]` is a pointer to `Anki::Cozmo::DockingComponent` (identified in the R-DEV pre-extraction from `DockingComponent::CanPickUpObject`, 0x00570428..0x0057042C). `DockingComponent+0xC` is an `Anki::ObjectID` value, specifically the **engine ObjectID of the object the robot is currently docking with (the dock target)**. It is not the carried object: the carried object is `CarryingComponent+8` (robot+0x284), which is the *other* word loaded by the `ldrd` at each exclusion site.

An `Anki::ObjectID` is a class whose integer id sits at offset `+4` of the object: `ObjectID::Set()` stores the counter at `[r0+4]` (0x008412D8: `ldr r1,[pc,#0xc]`; 0x008412E4 `str r2,[r0,#4]`).

| step | what the original does | citation | record | classification |
| --- | --- | --- | --- | --- |
| D1 | **Default.** `DockingComponent`'s constructor writes `-1` (0xFFFFFFFF, the invalid ObjectID) to `+0xC`. | 0x0063BA1E `mov.w r3,#-1`; 0x0063BA2A `str r3,[r0,#0xc]` (ctor entry 0x0063BA1C) | NEW (M4-009 gap; the CD10a/CD10b rows name the exclusion but not the field) | EXACT_SOURCE |
| D2 | **Writer.** `DockingComponent::DockWithObject` copies the docked object's ObjectID id into `+0xC`. The argument is an `Anki::ObjectID` (passed indirectly); `[r6+4]` is its id, stored to `[this+0xC]`. This is the only non-zero writer. | 0x0063BA52 `mov r6,r1`; 0x0063BAAC `ldr r0,[r6,#4]`; 0x0063BAB6 `str.w r0,[sb,#0xc]` (fn entry 0x0063BA44) | NEW | EXACT_SOURCE |
| D3 | **No other writer.** The whole `DockingComponent` method range 0x0063BA1C..0x0063C900 contains only the two stores above to `+0xC` (the only other `#0xc]` hit is `strd r3,r2,[sp,#0xc]` at 0x0063BB84, a stack store). `AbortDocking` (0x0063BE10) sends AbortDocking but does **not** reset `+0xC`. A whole-`.text` Thumb scan for `ldr/ldrd ... [reg,#0x280]` followed by an access to `[reg,#0xC]` (300-instruction window, stop on clobber) finds six sites, all loads, none a store. | scans as cited; 0x0063BE10..0x0063BE80 (AbortDocking) has no `+0xC` access | NEW | EXACT_SOURCE for the two writers; "no other writer" is a lower bound (see limits) |
| D4 | **Reader: Moved.** `HandleActiveObjectMoved` loads `r7 = [DockingComponent+0xC]` and compares it with the object's id; when equal (or when `CarryingComponent::IsCarryingObject` returns true) the ObjectMoved broadcast is skipped (branch to 0x00534322). | 0x0053416E `ldrd r1,r0,[r5,#0x280]`; 0x00534174 `ldr r7,[r1,#0xc]`; 0x00534182 `cmp r4,r7`; 0x00534188 `orrs r0,r1`; 0x0053418C `bne.w #0x534322` (handler entry 0x00533E30) | M4-009 (CD10a) | EXACT_SOURCE |
| D5 | **Reader: Stopped.** `HandleActiveObjectStopped` loads `r4 = [DockingComponent+0xC]` and applies the same exclusion to ObjectStoppedMoving. | 0x0053497C `ldrd r1,r0,[fp,#0x280]`; 0x00534984 `ldr r4,[r1,#0xc]`; 0x00534992 `cmp r7,r4`; 0x00534998 `orrs r0,r1`; 0x0053499A `bne.w #0x534ae0` (handler entry 0x0053461C) | M4-009 (CD10b) | EXACT_SOURCE |
| D6 | **Reader: ObjectPoseConfirmer::UpdatePoseInInstance.** Loads `[DockingComponent+0xC]` and sets a flag if it equals the object's id. | 0x00505E9C `ldr.w r2,[r1,#0x280]`; 0x00505EA2 `ldr r2,[r2,#0xc]`; 0x00505EA4 `cmp r2,r1`; 0x00505EA8 `moveq r3,#1` (fn entry 0x00505DE1) | NEW | EXACT_SOURCE |
| D7 | **Reader: PotentialObjectsForLocalizingTo::CouldUseObjectForLocalization.** Excludes the dock target from localization candidates. | 0x0050D18C `ldr.w r2,[r0,#0x280]`; 0x0050D198 `ldr r6,[r2,#0xc]`; 0x0050D19E `cmp r7,r6` (fn entry 0x0050D170) | NEW | EXACT_SOURCE |
| D8 | **Reader: BlockWorld::CheckForUnobservedObjects.** Skips the object when it is the dock target. | 0x00621D7A `ldr.w r0,[r0,#0x280]`; 0x00621D7E `ldr r0,[r0,#0xc]`; 0x00621D80 `cmp r0,r1`; 0x00621D88 `beq #0x621e0c` (fn entry 0x00621C6D) | NEW | EXACT_SOURCE |
| D9 | **Reader: BlockWorld::CheckForCollisionWithRobot.** Returns "no collision" when the object is the dock target. | 0x0062598A `ldr.w r0,[r0,#0x280]`; 0x0062598E `ldr r0,[r0,#0xc]`; 0x00625990 `cmp r0,r1`; 0x00625992 `beq.w #0x625aec` (fn entry 0x0062593D) | NEW | EXACT_SOURCE |

**Answer.** `DockingComponent+0xC` is an `Anki::ObjectID`: the object currently being docked with. Default `-1`; written only by `DockWithObject`; read by the two cube broadcast handlers (M4-009) and by four BlockWorld/pose functions. It is neither the carried object (`CarryingComponent+8`) nor a connection slot.

---

## Question 2 (M4-018 S7): `robot+0x2C4`

`robot+0x2C4` is a **byte "localized" flag**: 1 while the robot has a valid localization, 0 after `Delocalize`. The `RobotDelocalized` handler sets `CubeLightComponent+0x21`; `CubeLightComponent::Update` then refreshes the default layer for every object only once `robot+0x2C4` is non-zero again, i.e. the refresh is deferred until the robot is re-localized.

| step | what the original does | citation | record | classification |
| --- | --- | --- | --- | --- |
| L1 | **Default = 1.** `Robot::Robot` stores 1. | 0x0050FEF2 `movs r2,#1`; 0x0050FF10 `strb.w r2,[r5,#0x2c4]` | NEW (M4-018 S7 left the semantics UNKNOWN) | EXACT_SOURCE |
| L2 | **`Delocalize` = 0.** `Robot::Delocalize(bool)` stores 0 (r5=0). | 0x00510A30 `movs r5,#0`; 0x00510A4A `strb.w r5,[r4,#0x2c4]` | NEW | EXACT_SOURCE |
| L3 | **`SetLocalizedTo` = 1** (two branches: localize to odometry, and localize to an object). | 0x0051245C `movs r0,#1`; 0x0051245E `strb.w r0,[r4,#0x2c4]`; 0x005124EC `movs r0,#1`; 0x005124EE `strb.w r0,[r4,#0x2c4]` | NEW | EXACT_SOURCE |
| L4 | **`CheckAndUpdateTreadsState` = 1** (robot back on treads). | 0x0051217A `movs r0,#1`; 0x0051217C `strb.w r0,[sb,#0x2c4]` | NEW | EXACT_SOURCE |
| L5 | **No other writer.** A whole-`.text` Thumb scan for `#0x2c4` finds only the five byte stores above; every other `#0x2c4` hit is a stack/`adr`/pc-relative reference or a word access on a different object. | scan `#0x2c4` / `#0x2c4]` over `.text`; the only `strb`/`strb.w` to a non-stack base are L1..L4 | NEW | EXACT_SOURCE (lower bound; see limits) |
| L6 | **Reader: gate in `CubeLightComponent::Update`.** If `comp+0x21` is set and `[robot+0x2C4]` is non-zero, it clears `+0x21` and sets the "re-pick every object" flag; if `+0x2C4` is 0 it leaves `+0x21` set (defer). | 0x00637924 `ldrb.w r0,[fp,#0x21]`; 0x0063792A `ldr.w r0,[fp]`; 0x00637930 `ldrb.w r0,[r0,#0x2c4]`; 0x00637936 `strb.w r4,[fp,#0x21]`; 0x0063793A `movs r4,#1` | M4-018 (S7) | EXACT_SOURCE |
| L7 | **Reader: `Robot::LocalizeToObject`** gates part of its work on the flag. | 0x0051570A `ldrb.w r0,[r5,#0x2c4]`; 0x0051570E `cbz r0,#0x515762` (fn entry 0x005154B1) | NEW | EXACT_SOURCE |
| L8 | **Reader: `Robot::LocalizeToMat`** branches on the flag. | 0x00515F64 `ldrb.w r0,[r8,#0x2c4]`; 0x00515F68 `cmp r0,#0`; 0x00515F6A `bne.w #0x51609a` (fn entry 0x00515CBD) | NEW | EXACT_SOURCE |
| L9 | **Reader: `Robot::GetRobotState`** publishes the flag into the internal RobotState: byte at output `+0x6C` = 1 when `[robot+0x2C4] != 0` **and** `[robot+0x355]` (committed off-treads state) `== 0`; otherwise 0. | 0x00518210 `ldrb.w r0,[r5,#0x2c4]`; 0x00518214 `cbz r0,#0x518222`; 0x00518216 `ldrb.w r0,[r5,#0x355]`; 0x0051821C `movs r0,#1`; 0x0051821E `strb.w r0,[r7,#0x6c]` (fn entry 0x005180D9) | NEW | EXACT_SOURCE for the read and the store. The C++ name of RobotState+0x6C is not in the shipped symbols; the Unity side names the concept `GameStatusFlag.IsLocalized` (`unity/scripts/csharp/Anki.Cozmo/GameStatusFlag.cs:6`; `Robot.cs:456-458`), authority 2. |

**Answer.** `robot+0x2C4` is the engine's "robot is localized" byte. Default 1; set to 0 only by `Robot::Delocalize`; set to 1 by `SetLocalizedTo` and `CheckAndUpdateTreadsState`. It is read by the two localization entry points, by `GetRobotState` (as the `+0x6C` localized flag, additionally requiring the robot not be off-treads), and by the `CubeLightComponent::Update` S7 gate. For S7 it is the "wait until re-localized" condition: while the robot is delocalized, the `RobotDelocalized` refresh request (`+0x21`) is held and applied on the first Update after the flag returns to 1.

---

## Question 3 (M4-016): lift `CheckIfDone` eye-shift, and head `+0xA8` writer

### 3a. The lift body has no eye-shift removal

`MoveLiftToHeightAction::CheckIfDone` is the symbol at 0x005493F1, entry 0x005493F0. The body asked about is 0x005493F6..0x00549508; the whole function runs to the epilogue at 0x0054956A.

| step | what the original does | citation | record | classification |
| --- | --- | --- | --- | --- |
| X1 | The complete call set in 0x005493F0..0x0054956A is: `MoveLiftToHeightAction::IsLiftInPosition` (PLT 0x4AB218, at 0x00549412), `Robot::GetLiftHeight` (PLT 0x4AB200, at 0x00549454), `sChanneledDebugF` (PLT 0x4A5B84, at 0x0054948A), `sWarningF` (PLT 0x4A4540, at 0x005494DC), and `operator delete` (PLT 0x4A40CC, at 0x005494AC and 0x005494FE). **No call to `TrackLayerComponent::RemoveEyeShift` (PLT 0x4AAFD8) and no call to `TrackLayerComponent::AddOrUpdateEyeShift` (PLT 0x4AB044).** The body has no `+0xA8` load and no eye-shift block. | disassembly 0x005493F0..0x0054956A; PLT resolution for 0x4AB218/0x4AB200/0x4A5B84/0x4A4540/0x4A40CC; the head's eye-shift PLTs are 0x4AAFD8/0x4AB044 | M4-016 (lift rows L1..L6 / C6) | EXACT_SOURCE |

**Answer (3a).** No. There is no eye-shift removal call in the lift's `CheckIfDone` range. The search was: (i) a full disassembly of the whole function 0x005493F0..0x0054956A; (ii) resolution of every `blx` target in it; (iii) a scan of all of `.text` for `bl/blx/b.w` to the two eye-shift PLT stubs, which returns exactly the 10 sites listed below and none in the lift function.

All eye-shift call sites in `.text`:
- `AddOrUpdateEyeShift` (0x4AB044): 0x00546286, 0x0056535A, 0x0057D7FC.
- `RemoveEyeShift` (0x4AAFD8): 0x00545BBC, 0x005465C2, 0x005484AA, 0x0054871E, 0x005647D6, 0x0057D8D2, 0x0063E37A.

### 3b. Head `+0xA8` has no non-zero writer, so H4..H6 are dead

| step | what the original does | citation | record | classification |
| --- | --- | --- | --- | --- |
| X2 | **All writers of the head action's `+0xA8` are zero stores, inside the head action's own methods.** Ctor (r6=0 at 0x00547F24): `str.w r6,[fp,#0xa8]`. Destructor: `movs r0,#0` then `strb.w r0,[r4,#0xa8]`. H6: `movs r0,#0` then `strb.w r0,[r4,#0xa8]`. | 0x00547F24 `movs r6,#0`; 0x00547F3C `str.w r6,[fp,#0xa8]`; 0x005484AE `movs r0,#0`; 0x005484B0 `strb.w r0,[r4,#0xa8]`; 0x00548722 `movs r0,#0`; 0x00548724 `strb.w r0,[r4,#0xa8]` | M4-016 (H7) | EXACT_SOURCE |
| X3 | **A full `.text` store scan finds no non-zero store to a head-action `+0xA8`.** The only `str/strb/strh ... [reg,#0xa8]` sites within the head action method range 0x00547E40..0x005488BE are X2's three, all zero. Every other `+0xA8` store in `.text` is a stack slot (`[sp,#0xa8]`) or belongs to a different class's field (TurnInPlaceAction, TrackFaceAction, AnimationStreamer and many others have their own `+0xA8`). | store scan over `.text` for `#0xa8]`; head range 0x00547E40..0x005488BE | M4-016 (H7) | EXACT_SOURCE (negative result) |
| X4 | **The only function that can set a tag non-zero, `AddOrUpdateEyeShift`, has exactly three callers, and none passes a MoveHeadToAngleAction tag.** Tag reference per call site: TurnInPlaceAction `this+0xD9` (0x0054626E `add.w r1,r4,#0xd9`), TrackFaceAction frame-local at `fp+0xA0` (0x00565350 `add.w r1,fp,#0xa0`), AnimationStreamer `this+0x1C4` (0x0057D7EE `add.w r1,r4,#0x1c4`). None is `this+0xA8` of a MoveHeadToAngleAction. | 0x0054626E/0x00546286; 0x00565350/0x0056535A; 0x0057D7EE/0x0057D7FC | M4-016 (H7) | EXACT_SOURCE |
| X5 | **No head-action construction site writes `+0xA8`.** The two head ctors are reached through the PLT stubs 0x4AB0B0, 0x4A94FC and 0x4B4E84; there are 22 such call sites. Scanning 100 instructions after each for a store to `+0xA8` finds only one hit, 0x005CCB00 `strd r0,r0,[sp,#0xa8]`, which is a stack store. No caller sets the field. | 22 call sites listed in the scan; only 0x005CCB00 (stack) | M4-016 (H7) | EXACT_SOURCE |

**Answer (3b).** The head action's `+0xA8` tag has no writer that sets it non-zero anywhere in `.text`: the only writers are the ctor, destructor and H6, all zero; `AddOrUpdateEyeShift`'s three callers pass other classes' tags; and no head-ctor caller touches `+0xA8`. Therefore H4..H6 (and the destructor's `RemoveEyeShift`) never run in this build.

---

## Contradictions, weak evidence, open questions

**Existing records contradicted by the source:** none for the three questions.

**Existing records whose evidence is too weak (partial evidence):**

- **M4-009 (CD10a/CD10b).** The rows cite the exclusion `[robot+0x280]+0xC` and the two handler addresses, but they do not name the field. This pass establishes it (Q1 D1..D9). The M4-009 record's evidence should carry the ctor/DockWithObject writers and at least the two handler readers; the other four readers are new behaviour that no M4 record owns.
- **M4-018 (S7).** The row correctly reads the gate at 0x00637924..0x00637940 but records `robot+0x2C4` semantics as UNKNOWN. This pass settles it (Q2 L1..L9). S7 can be promoted from "read only" once the record names the flag and its writers.
- **M4-016 (H7).** The pre-extraction's negative result is confirmed here by two independent searches (full store scan and ctor-caller scan). H4..H6 and the destructor eye-shift branch are dead. The lift has no equivalent block at all (X1).

**Open questions / limits the manager should note:**

1. **Scan limits for Q1 and Q2.** The whole-`.text` writer/reader scans decode `.text` as Thumb with `skipdata`; the ARM regions of the binary are the third-party OKAO face code and breakpad (symbols with even addresses, mostly 0x8F0000..0x95xxxx) and do not reference `DockingComponent` or `robot+0x2C4`. The scans also cannot see an access made through a pointer cached in a callee-saved register more than 300 instructions after the `[robot+0x280]` load, or a base computed by `add` rather than the raw field offset. The writer lists for `DockingComponent+0xC` and `robot+0x2C4` are therefore lower bounds; no contradicting site was found.
2. **`RobotState+0x6C` name (Q2 L9).** The engine's internal `RobotState` layout is not in the shipped symbols, so the exact C++ field name is not recovered. The value and its derivation are read; the Unity concept is `GameStatusFlag.IsLocalized` (authority 2). This is a naming gap only, not a behaviour gap.
3. **`DockingComponent+0xC` is not reset on abort.** `AbortDocking` (0x0063BE10) sends AbortDocking but leaves `+0xC` at the last dock target. Whether a later `DockWithObject` always overwrites it before the handlers read it was not traced; the handlers only compare, so a stale value would keep suppressing that one object's Moved/Stopped broadcasts. This is a NEW behaviour question for M4-009, not a contradiction.
