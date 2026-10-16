# Overnight research answer OV85 (M11-013: ObservableObject::SetMarkerAsObserved)

- **Date:** 2026-10-16
- **Requested by:** manager (opencode), from `re-analysis/research/OVERNIGHT-QUEUE.md`
- **Kind:** independent extraction
- **Answers:** request `re-analysis/research/requests/overnight/20261015-ov85-setmarkerasobserved.md`
- **Answer file:** `re-analysis/research/overnight/20261015-ov85-setmarkerasobserved.md` (this file)
- **Binary:** libcozmoEngine.so 3.4.0-1204 (Thumb). Disassembled with capstone, Thumb mode, `skipdata`-style linear sweep over the body; pc-relative literals resolved at `(insn_addr+4)&~3 + imm`. The Ghidra decompilation (`re-analysis/decomp/libcozmoEngine/0087/00876da8.c`) was used only as a navigation aid; every claim below is cited to the instructions.
- **Scope:** `ObservableObject::SetMarkerAsObserved(ObservedMarker*, u32 timestamp, float)` body `0x00876DA8..0x008770CC`, its live caller `CreateObjectsFromMarkers` `0x0062559C` (float argument `0x3DCCCCCD` = 0.1f), and the plural `SetMarkersAsObserved` `0x00876CCC`. M1-034 crash paths noted where relevant.

## Coverage table (first)

| # | step (behaviour-changing) | address / citation | classification |
|---|---|---|---|
| 1 | Prologue: build empty `basic_string` local_b8 from `&DAT_00be3f00` (""), construct stack `Pose3d` aPStack_5c | `0x00876DA8..0x00876DE8` | EXACT_SOURCE |
| 2 | Code match: `find<short>(tree@this+0x34, (short*)param_1)` — look up observed marker's code in the code→vector<KnownMarker*> tree | `0x00876DFC..0x00876E0C` | EXACT_SOURCE |
| 3 | Code not found (iterator == end `this+0x38`) → Warning 1, then cleanup/return, no timestamp store | `0x00876E0C beq 0x00876f8b`; `0x00876FA8` | EXACT_SOURCE |
| 4 | Vector empty (begin==end) → Warning 2, then cleanup/return, no timestamp store | `0x00876E18 beq 0x00876faf`; `0x00876FC4` | EXACT_SOURCE |
| 5 | Per known marker: `GetWithRespectTo((PoseBase*)(node+4), (Pose3d*)(param_1+0x34), &aPStack_5c) == 1` gate | `0x00876E52..0x00876E64` | EXACT_SOURCE |
| 6 | Per known marker: project 3d corners, `Quadrilateral::Contains(testPoint) == 1` gate | `0x00876E68..0x00876E94` | EXACT_SOURCE |
| 7 | Per known marker: squared centroid distance `< current best` gate (best init FLT_MAX) | `0x00876E96..0x00876EE0` | EXACT_SOURCE |
| 8 | Per known marker: area metric `< param_3` (0.1f) gate; on pass update best distance + best node pointer | `0x00876EE2..0x00876F74` | EXACT_SOURCE |
| 9 | Timestamp store: `*(uint*)(best_node + 0x48) = param_2`, only when a best node was found | `0x00876F80..0x00876F88` (`str r0,[r1,#0x48]` at `0x00876F86`) | EXACT_SOURCE |
| 10 | Warning 1: key "ObservableObject.SetMarkerAsObserved", format "No markers found with code (%d:%s) of given 'nearestTo' observed marker", args (marker code, code name); after: cleanup+return | `0x00876F92..0x00876FAC` | EXACT_SOURCE |
| 11 | Warning 2: key "ObservableObject.SetMarkerAsObserved.NoMatchingMarkerFound", format "Marker:%s", arg (code name); after: string dealloc + Pose3d dtor + return | `0x00876FB6..0x00876FEC` | EXACT_SOURCE |
| 12 | Cleanup: dealloc local_b8 buffer if non-null, destroy stack Pose3d, return void | `0x00876FEA..0x00876FF8` | EXACT_SOURCE |
| 13 | Plural `SetMarkersAsObserved`: blanket `*(uint*)(node+0x48)=timestamp` on every marker of a code, no geometry | `0x00876CCC..0x00876D35` | EXACT_SOURCE |

**Gaps:** none. The whole body `0x00876DA8..0x008770CC` was read instruction-by-instruction; every behaviour-changing step above has an address. Nothing is filled with plausible behaviour.

## The target: `ObservableObject::SetMarkerAsObserved(ObservedMarker*, u32, float)`

Register convention (ARM EABI, thiscall): `r0=this`, `r1=param_1` (ObservedMarker*)→`r4`, `r2=param_2` (u32 timestamp)→`sl`, `r3=param_3` (float)→`r8` initially. The caller `CreateObjectsFromMarkers` at `0x0062559C` passes `param_3 = 0x3DCCCCCD` = 0.1f (see M11-006 / B-M11-3 context).

### Prologue (0x00876DA8..0x00876DE8)
- `push.w {r4,r5,r6,r7,r8,sb,sl,fp,lr}`; `sub sp,#4`; `vpush {d8..d12}`; `sub sp,#0x88`.
- `FUN_004e02b2((basic_string *)&local_b8, &DAT_00be3f00, 0)` at `0x00876DCE`: constructs `local_b8` as an empty basic_string. I verified `&DAT_00be3f00` at `0x00BE3F00` is an empty string `""`, so `local_b8 == ""`.
- `Pose3d::Pose3d(aPStack_5c, &local_b8)` at `0x00876DD4..0x00876DD6`; the `Pose3d` is destroyed on every exit path (`0x00876FEA..0x00876FEC`, `0x00877040..0x00877044`).

### How a known marker is selected (code match + geometry + thresholds)

**Code match.** `0x00876DFC add.w r0,r7,#0x34` (`this+0x34`, the tree); `0x00876E00 mov r1,r4` (`param_1`, the observed marker; its `short` code is `*(short*)param_1`); `0x00876E02 blx __tree::find<short> (0x004cfe00)`. If the iterator == `this+0x38` (end), branch `0x00876E0C beq.w 0x00876f8b` → **Warning 1** (see below).

**Selection loop** over `vector<KnownMarker*>`: `begin = *(int**)(pOVar3+0x14)`, `end = *(int**)(pOVar3+0x18)` (`0x00876E12 ldrd sb,r6,[r0,#0x14]`). Empty → `0x00876E18 beq.w 0x00876faf` → **Warning 2**.

For each known marker `iVar11 = *piVar12` (`0x00876E52 ldr.w r8,[sb]`), in order, with unordered early-exit branches to `0x00876f77` (next marker) on any failure:

1. **Frame gate** — `0x00876E56 add.w r0,r8,#4`; `0x00876E5E blx PoseBase::GetWithRespectTo (0x004a40fc)` with `(PoseBase*)(node+4)`, `(Pose3d*)(param_1+0x34)`, output `&aPStack_5c`. `0x00876E62 cmp r0,#1`; `0x00876E64 bne.w 0x00876f77` — skip unless it returns 1.
2. **Projection/containment gate** — `memset(&local_88,0,0x20)` (`0x00876E6E`, `__aeabi_memclr8 0x004a48ac`); `KnownMarker::Get3dCorners` (`0x004ba9a4`, `0x00876E7A`); `Camera::Project3dPoints(param_1+0x28, &local_b8 corners)` (`0x004bf27c`, `0x00876E84`); `Quadrilateral::Contains(&local_88)` (`0x004a409c`, `0x00876E8E`). `0x00876E92 cmp r0,#1`; `0x00876E94 bne 0x00876f77` — skip unless the test point is inside the projected quad.
3. **Distance gate** — centroid computed (`KnownMarker::Get3dCorners`/`ComputeCentroid`), `fVar13 = cx^2 + cy^2` (`0x00876EC4..0x00876ED4`, `vmul`+`vadd`). `fVar15` (best) initialized to FLT_MAX `0x624F7F7F` (`0x00876E36 vldr s20,[pc->0x00877056]=0x624F7F7F`). `0x00876ED8 vcmpe.f32 s0,s20`; `0x00876EE0 bpl 0x00876f77` — skip unless `fVar13 < fVar15` (strictly nearer than current best).
4. **Area gate + update** — `fVar2 = Quadrilateral::Area(param_1+8)` (observed marker's screen area, `0x00876DF6 blx 0x004cfe0c`, result in `r5`→`s0`). `s18 = 1.0 / fVar2` (`0x00876E4C vdiv.f32 s18,s2,s0`, `s2=1.0`). `fVar14 = s18 * ( |cross1|*0.5 + |cross2|*0.5 ) - 1.0`, abs'd (`0x00876EE2..0x00876F4A`, using `s22=0.5`, `s24=-1.0`, `vabs.f32`, `vmovmi` for the negate). This is the normalized area difference of the projected known-marker quad relative to the observed quad. `0x00876F60 vcmpe.f32 s2,s16` where `s16 = in_r3 = param_3 = 0.1f`; `0x00876F68 it mi` / `0x00876F6A vmovmi.f32 s20,s0` — update `fVar15 = fVar13` only if `fVar14 < 0.1f`; `0x00876F70 it mi` / `0x00876F72 movmi r0,r8` / `0x00876F74 str r0,[sp,#0xc]` — update `local_cc = node` (best pointer) only under the same condition.

**Branch order (unordered):** `GetWithRespectTo==1` → `Contains==1` → `dist < best` → `area < 0.1f`. Each failure branches to the next marker (`0x00876f77`); the area test is only reached when the distance test passes. No f64 is used anywhere — all arithmetic is f32 (`s`-registers).

**Result:** the selected known marker is the *nearest* (by squared centroid distance) among those whose projected quad contains the test point, whose frame transform succeeds, and whose normalized area difference is within 0.1f of the observed marker's area. Ties: the first nearest in iteration order wins (strict `<` on distance keeps the earlier one on equality).

### When the timestamp store at 0x00876F86 runs, and on which node field

After the loop: `0x00876F80 ldr r1,[sp,#0xc]` (`r1 = local_cc` = best node pointer, or 0 if none); `0x00876F82 ldr r0,[sp,#8]` (`r0 = sl` = `param_2`, the timestamp); `0x00876F84 cbz r1, 0x00876faf` — if no best node was found, skip the store and go to Warning 2.

**`0x00876F86: str r0, [r1, #0x48]`** → `*(uint*)(best_node + 0x48) = param_2` (the timestamp). Then `0x00876F88 b 0x00876feb` → cleanup/return.

So the store runs **only when a matching known marker was found** (`local_cc != 0`), immediately after the selection loop, before cleanup. It writes the caller's timestamp (`param_2`, from `CreateObjectsFromMarkers`' "first prototype timestamp") to the selected node.

**Offset:** the instruction encodes `#0x48`. Both the singular `SetMarkerAsObserved` (`str r0,[r1,#0x48]` at `0x00876F86`) and the plural `SetMarkersAsObserved` (`*(uint *)(*piVar3 + 0x48) = param_2` per the decompilation of `0x00876CCC`) use **+0x48**, not +0x50. The request's "+0x50" does not match the instructions; I report +0x48 as directly observed. `local_cc`/`r1` is the selected `KnownMarker*`/node base (the element of the `vector<KnownMarker*>`, i.e. `iVar11 = *piVar12`, whose `PoseBase` subobject is at `node+4` per the `GetWithRespectTo` call). So the timestamp field is at **node + 0x48** (a u32).

### The two Warning calls (both `Util::sWarningF`, `0x004a4540`)

**Warning 1 at `0x00876FA8`** — reached when the code is not in the tree (`iterator == end`, `0x00876E0C beq 0x00876f8b`).
- **Key:** `"ObservableObject.SetMarkerAsObserved"` (string at `0x008770A4`, loaded via `adr r0,#0x100` at `0x00876FA2`).
- **Context arg (r1):** `&local_b8` = `""` (empty basic_string from `&DAT_00be3f00` at `0x00BE3F00`).
- **Format (r2):** `"No markers found with code (%d:%s) of given 'nearestTo' observed marker"` (string at `0x00C2DDE0`, loaded via `ldr r2,[pc,#0x100]` at `0x00876F9C` then `add r2,pc`).
- **Arguments:** `%d` = `sVar1 = *(short*)param_1` (the observed marker's code, `0x00876F92 ldrsh.w r5,[r4]` → `r3` at `0x00876FA6`); `%s` = code name = `Marker::GetCodeName((Marker*)param_1)` result (`0x004a44d4`, `0x00876F98`), stored at `[sp]` and passed as the 5th vararg (`0x00876FA0 str r0,[sp]`).
- **What happens after:** `0x00876FAC b 0x00876fc9` → jumps straight to cleanup (`0x00876fea..0x00876fec`: destroy stack `Pose3d`) and return. **No timestamp store is performed.**

**Warning 2 at `0x00876FC4`** — reached when the loop found no marker passing all gates (`local_cc == 0`, `0x00876F84 cbz` → `0x00876faf`).
- **Key:** `"ObservableObject.SetMarkerAsObserved.NoMatchingMarkerFound"` (string at `0x00877058`, loaded via `adr r0,#0x98` at `0x00876FBE`).
- **Context arg (r1):** `&local_b8` = `""`.
- **Format (r2):** `"Marker:%s"` (string at `0x00877094`, loaded via `adr r2,#0xd0` at `0x00876FC2`).
- **Argument:** `%s` = `pcVar6 = Marker::GetCodeName((Marker*)param_1)` (`0x004a44d4`, `0x00876FB8`), in `r3` (`0x00876FBC mov r3,r0`).
- **What happens after:** falls through to `0x00876FC8`: `ldr r0,[sp,#0x24]`; `cmp r1,r0`; if the string buffer is non-null, adjust the pointer (`0x00876FD2..0x00876FE4`, `bic`/`add`) and `operator_delete (0x004a40cc)` at `0x00876FE6`; then destroy the stack `Pose3d` (`0x004a406c`, `0x00876FEA..0x00876FEC`) and return. **No timestamp store is performed.**

### Return value

**`void`.** The function has no return value; every exit path returns via `pop.w {r4,r5,r6,r7,r8,sb,sl,fp,pc}` at `0x00876FF8` (equivalent to `bx lr`). No result is communicated through registers.

### Every other state write

The **only** persistent (non-stack) state write in the entire body is:

- `0x00876F86: str r0, [r1, #0x48]` → `*(uint*)(best_node + 0x48) = param_2` (the timestamp), conditional on a best node being found.

All other stores are to stack slots (`[sp, #0xc]` for `local_cc`, `[sp,#8]` for the timestamp spill, `[sp,#0x18]`/`[sp,#0x20]` for centroid, `[sp,#0x50..]` for locals, etc.). The function does **not** write PoseState, ObjectID, confirmation counters, the observed marker, or any other member. It constructs/destroys only stack objects (`local_b8` basic_string, `aPStack_5c` Pose3d) and frees `local_b8`'s buffer on the warning paths.

### Contrast with the plural `SetMarkersAsObserved` at `0x00876CCC`

`SetMarkersAsObserved(short *code, u32 timestamp)` (`0x00876CCC..0x00876D35`, 106 bytes):
- **No `ObservedMarker*`, no float, no geometry.** It takes a marker `code` (short) and a timestamp only.
- `find<short>(tree@this+0x34, code)`; if not found → warning key `"ObservableObject.SetMarkersAsObserved"`, format `"No markers found with code %d"` (string at `0x00876D88`), arg = code. Return.
- If found: iterate **every** `KnownMarker*` in that code's vector and do `*(uint *)(*piVar3 + 0x48) = param_2` (timestamp) for each — a **blanket store**, no nearest, no `Contains`, no `GetWithRespectTo`, no area gate, no `0.1f` comparison.
- Also uses **+0x48** (not +0x50).
- Returns void.

So the singular `SetMarkerAsObserved` selects **one nearest marker by geometry/area** and timestamps only it; the plural `SetMarkersAsObserved` timestamps **all markers of a code** unconditionally. They are distinct code paths (the plural has no callers per the decompilation, but it is a separate function).

## Build rows

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| prologue + empty string + Pose3d ctor/dtor | build `local_band` from `&DAT_00be3f00` (""), stack Pose3d, destroyed on all exits | `0x00876DA8..0x00876DE8`, `0x00876FEA..0x00876FEC` | M11-013 | EXACT_SOURCE |
| code match via find<short> | look up observed marker's code in `this+0x34` tree | `0x00876DFC..0x00876E0C` | M11-013 | EXACT_SOURCE |
| not-found branch → Warning 1 | `beq 0x00876f8b` when iterator == end | `0x00876E0C` | M11-013 | EXACT_SOURCE |
| empty-vector branch → Warning 2 | `beq 0x00876faf` when begin==end | `0x00876E18` | M11-013 | EXACT_SOURCE |
| GetWithRespectTo gate | `node+4` vs `param_1+0x34`, must return 1 | `0x00876E52..0x00876E64` | M11-013 | EXACT_SOURCE |
| project+Contains gate | project corners, `Contains(testPoint)==1` | `0x00876E68..0x00876E94` | M11-013 | EXACT_SOURCE |
| squared-distance `< best` gate | `cx^2+cy^2` vs FLT_MAX best | `0x00876E96..0x00876EE0` | M11-013 | EXACT_SOURCE |
| area metric `< param_3(0.1f)` gate + update best | `(1/area)*(|cross1|*0.5+|cross2|*0.5)-1.0` abs'd, `< 0.1f` | `0x00876EE2..0x00876F74` | M11-013 | EXACT_SOURCE |
| timestamp store | `*(uint*)(best_node+0x48) = param_2`, only if best found | `0x00876F80..0x00876F88` (`0x00876F86 str [r1,#0x48]`) | M11-013 | EXACT_SOURCE |
| Warning 1 | key/format/args as above; cleanup+return, no store | `0x00876FA8` | M11-013 | EXACT_SOURCE |
| Warning 2 | key/format/arg as above; string free + Pose3d dtor + return, no store | `0x00876FC4` | M11-013 | EXACT_SOURCE |
| return value | void, `pop.w ...pc` | `0x00876FF8` | M11-013 | EXACT_SOURCE |
| no other state writes | only persistent write is the timestamp at node+0x48 | whole body `0x00876DA8..0x008770CC` | M11-013 | EXACT_SOURCE |
| plural `SetMarkersAsObserved` contrast | blanket `*(uint*)(node+0x48)=ts` on all markers of a code, no geometry | `0x00876CCC..0x00876D35` | M11-013 (NEW contrast row) | EXACT_SOURCE |

**Note on the request's "+0x50":** the instructions show `#0x48` at `0x00876F86`, and the plural `SetMarkersAsObserved` also uses `+0x48`. The request's "+0x50" does not match the instructions; I report +0x48 as observed and do not substitute a plausible +0x50.

## Verification notes

- All addresses are VAs in `libcozmoEngine.so` 3.4.0-1204, Thumb mode. Literal pool: `0x00877056` = FLT_MAX `0x624F7F7F`; strings at `0x008770A4` (warning-1 key), `0x00C2DDE0` (warning-1 format), `0x00877058` (warning-2 key), `0x00877094` (warning-2 format), `0x00876D88` (plural warning format), `0x00BE3F00` = `""`.
- `param_3 = 0x3DCCCCCD` = 0.1f (area tolerance), per the caller at `0x0062559C` and M11-006.
- `sWarningF` = `0x004a4540`; `Marker::GetCodeName` = `0x004a44d4`; `GetWithRespectTo` = `0x004a40fc`; `KnownMarker::Get3dCorners` = `0x004ba9a4`; `Camera::Project3dPoints` = `0x004bf27c`; `Quadrilateral::Contains` = `0x004a409c`; `Quadrilateral::Area` = `0x004cfe0c`; `__aeabi_memclr8` = `0x004a48ac`; `operator_delete` = `0x004a40cc`; `Pose3d::~PoseBase` = `0x004a406c`.
- No crash path is reached by any shipped file here (M1-034 not triggered): the function is total (every path returns via cleanup). The two warnings are logged, not fatal.
