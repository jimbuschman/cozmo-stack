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
