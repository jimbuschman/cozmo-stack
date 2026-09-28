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