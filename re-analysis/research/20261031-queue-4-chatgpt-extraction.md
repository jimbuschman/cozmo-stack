# Cozmo Stack — Queue 4, combined native extraction

**Source of authority:** `libcozmoEngine.so` (ARMv7/Thumb 3.4.0-1204), original virtual addresses. This deliverable contains all eight requested bounded investigations. **Partial** means a requested whole-type, full-path, or alias-aware census is not fully settled. No program code or fidelity status was changed.

## Coverage

| Item | Status | Native address range | Main open point |
|---|---|---|---|
| 1. CalculateDesiredActivityFromObjects | **Partial** | `0x005ADF4C–0x005AE258` | Nested pose-helper semantics and all error continuations |
| 2. GetDesiredActiveBehaviorInternal | **Partial** | `0x005AE29C–0x005AE91C` | Some indirect downstream behavior and state ownership |
| 3. PickNewActivityForSpark / SetActivityIDFromSubActivity | **Partial** | `0x005ADC44`, `0x005AD8C8` and callees | Downstream callbacks and complete nested types |
| 4. IBehavior process ID +0x1C | **Partial** | `0x005BBBEE–0x005BBC08`, `0x005BD7D4`, `0x005E5FB2–0x005E5FB4` | No proof of absence of computed-pointer or alias writers |
| 5. StrategyGeneric | **Partial** | `0x00613748–0x00613ABC` | Fully traced predicate closures and event payloads |
| 6. Two needs strategies | **Partial** | `0x00613B00–0x00614710` | Referenced component full identity and inherited paths |
| 7. BehaviorLookForFaceAndCube | **Partial** | `0x005EF92C–0x005EFF10` | Downstream action vtable and callback details |
| 8. Idle removal and Activity_ global | **Done (requested scope)** | `0x0057BA60–0x0057BDD0`; `0x004D7590–0x004D75C0` | Secondary global logging symbols and downstream `SetStreamingAnimation` out of scope |

## Interpretation and safety of evidence

- Every claimed decision should be evaluated using the quoted *real* native instruction; descriptions are an extraction, **not** acceptance or status updates.
- The ELF's second LOAD maps a file offset to virtual address by adding `0x1000`. All displayed addresses in these sections are virtual addresses. BSS is initialized at runtime and cannot be read as if file-backed.
- A `cbz`/`cbnz` tests its register, whereas a `bne`/`beq` uses the flags set by the cited preceding `cmp`, `tst`, `subs`, etc.
- Any remaining UNKNOWN is visible in the respective section. Independent review is still required before promoting a fidelity claim.

---


---

## Full extraction section 1

# Queue 4 — item 1 — `ActivityFreeplay::CalculateDesiredActivityFromObjects`

| Item | Status | Native address range |
|---|---|---|
| 1. `CalculateDesiredActivityFromObjects` | **Partial** — decision and direct writes established; whole nested `Pose3d` field semantics, transient diagnostics/exceptions not fully classified | `0x005ADF4C–0x005AE258` |
| 2–8 | Not started in this report | See original queue |

**Authority:** ARMv7/Thumb `libcozmoEngine.so`, ELF executable text, dynamic PLT relocations. Entry is `0x005ADF4C` (Thumb symbol value `0x005ADF4D`). Raw literal addresses cited here are virtual addresses, not file offsets. The ELF's RW `LOAD` has file offset `0x1018200` and virtual address `0x1019200`; therefore, read a data virtual address in this segment at file offset `VA−0x1000`.

## 1. Inputs, type roots, and selection

| Step | Native instructions (exact instruction boundaries) and flag setter | Established behavior; explicit owning base |
|---|---|---|
| 1.01 | `0x005ADF52: mov r8,r0`; `0x005ADF8C: ldr.w r0,[r8,#0xa0]`; `0x005ADF90: ldr r0,[r0,#0x38]` | Entry `r0` is `ActivityFreeplay*`; saves it in `r8`. **ActivityFreeplay+0xA0** is the `Robot*`; **Robot+0x38** is the `FaceWorld*`. The latter is the target of the face lookup, not an `ActivityFreeplay+0x38` field. |
| 1.02 | `0x005ADF54: ldr r0,[pc,#0x304]` (literal at `0x005AE25C`, value `0x00A908EC`); `0x005ADF5C: add r0,pc`; `0x005ADF62: ldr r0,[r0]`; `0x005ADF64: ldr r0,[r0]`; `0x005ADF66: str r0,[sp,#0xa8]` | Loads a stack-canary pointer via GOT-derived virtual address `0x0103E84C` (`0x005ADF5C` PC=`0x005ADF60`; `+0x00A908EC`). Reads and saves the canary. No activity choice is made here. |
| 1.03 | `0x005ADF56: ldr r1,[pc,#0x308]` (literal `0x005AE260`, word `0x00635F9C`); `0x005ADF60: add r1,pc`; `0x005ADF70: mov r0,r9`; `0x005ADF72: bl 0x004E02B2`; `0x005ADF76: add r0,sp,#0x84`; `0x005ADF78: mov r1,r9`; `0x005ADF7A: blx 0x004A40C0` | Constructs a temporary name string (`r9=sp+0x10`) and stack `Pose3d` at `sp+0x84` from that string. Literal+PC yields original string pointer `0x00BE3F00`; this points to an empty NUL at that location, not to the following error message. The internal steps of `0x004E02B2` are **UNKNOWN** (direct native callee must be independently read). |
| 1.04 | `0x005ADF7E: ldrb.w r0,[sp,#0x10]`; `0x005ADF82: lsls r0,r0,#0x1f`; `0x005ADF84: itt ne`; `0x005ADF86: ldrne r0,[sp,#0x18]`; `0x005ADF88: blxne 0x004A40CC` | Releases the temporary string's allocation if its native long-string flag is set. `ne` derives from **`lsls`**, testing the least-significant bit of the string's first byte (shift into N flag/Z flag), *not* a prior face condition. |
| 1.05 | `0x005ADF92: add r1,sp,#0x84`; `0x005ADF94: movs r2,#1`; `0x005ADF96: blx 0x004AB380`; `0x005ADF9A: movs r7,#0`; `0x005ADF9C: cbz r0,0x005ADFAA` | Calls `FaceWorld::GetLastObservedFace(Pose3d&,true)` using the stack pose and literal boolean `1`. `cbz` tests the **returned `r0`**, not APSR flags. On zero, skips the world-origin check and establishes `faceAvailable=0`. |
| 1.06 | `0x005ADF9E: ldr.w r0,[r8,#0xa0]`; `0x005ADFA2: add r1,sp,#0x84`; `0x005ADFA4: blx 0x004A5C80`; `0x005ADFAA: movs r0,#0`; `0x005ADFAC: str r0,[sp,#4]` | If a face lookup returned nonzero, asks `Robot::IsPoseInWorldOrigin(facePose)` and stores its return. Otherwise stores zero. The value in local `sp+4` is thus `faceAvailable`. This routine does not write back to `FaceWorld` or the pose's source. |
| 1.07 | `0x005ADFAE: add.w r0,r9,#4`; `0x005ADFB2: strd r7,r7,[sp,#20]`; `0x005ADFC0–0x005AE01E` (multiple `strd` with `r7=0`); `0x005AE016: strh.w r7,[sp,#0x7c]` | Initializes a local `BlockWorldFilter` at `r9=sp+0x10` with eight initially empty tree sets, one empty predicate list, and zero filter flags at **filter+0x6C/+0x6D** (`sp+0x7C`). This is *stack-owned*, not a persistent `ActivityFreeplay` member. |
| 1.08 | `0x005ADFCC: ldr r4,[pc,#0x294]` (literal `0x005AE264`, value `0x006B3F6A`); `0x005ADFE2: add r4,pc`; `0x005AE020: adds r2,r4,r7`; `0x005AE028: blx 0x004A7DD4`; `0x005AE02C: adds r7,#4`; `0x005AE02E: cmp r7,#8`; `0x005AE030: bne 0x005AE020` | Builds a temporary `std::set<ObjectFamily>` using two 32-bit constants `1` and `2` from `0x00C61F50` and `0x00C61F54`. Here the high-register Thumb `add r4,pc` at `0x005ADFE2` uses PC `0x005ADFE6`, so `0x005ADFE6 + 0x006B3F6A = 0x00C61F50`. **`bne` consumes flags from `cmp r7,#8`**; loop increments offset by four and stops at eight. `0x004A7DD4` resolves to the set insert helper; this is not a filter callback. |
| 1.09 | `0x005AE032: ldr r1,[sp,#0x90]`; `0x005AE034: mov r0,r11`; `0x005AE036: mov r2,r5`; `0x005AE03A: blx 0x004A7DE0`; `0x005AE03E: ldr r1,[sp,#0x94]`; `0x005AE040: add r0,sp,#0x90`; `0x005AE042: blx 0x004A58A8` | Copies the temporary set of families into **local filter+0x3C** (`r11=sp+0x4C`), the *included ObjectFamily* set. Then destroys the temporary tree. Its comparator is `std::less<ObjectFamily>`. All seven other filter sets remain empty. |
| 1.10 | `0x005AE046: ldr.w r0,[r8,#0xa0]`; `0x005AE04C: ldr r0,[r0,#0x34]`; `0x005AE04A: movs r1,#0`; `0x005AE04E: str r1,[sp,#0xa0]`; `0x005AE056: movs r3,#1`; `0x005AE058: mov r1,r11`; `0x005AE05A: mov r2,r5`; `0x005AE05C: blx 0x004A709C`; `0x005AE060: mov r6,r0` | Retrieves **BlockWorld** via **ActivityFreeplay+0xA0 → Robot+0x34**. Calls `BlockWorld::FindLocatedObjectHelper(filter, empty std::function visitor, stopAfterFirst=true)`. Filter is local `sp+0x10`, visitor callable pointer zero at `sp+0xA0`, and the last argument is exactly `1`. Saves returned `r0` as `r6`. No found object is stored in this ActivityFreeplay function. |
| 1.11 | `0x005AE062: ldr r0,[sp,#0xa0]`; `0x005AE064: cmp r5,r0`; `0x005AE066: beq 0x005AE076`; `0x005AE068: cmp r0,#0`; `0x005AE06C: ittt ne`; `0x005AE06E: ldrne r1,[r0]`; `0x005AE070: ldrne r1,[r1,#0x14]`; `0x005AE072: blxne r1`; `0x005AE076: ldr r1,[r0]`; `0x005AE078: ldr r1,[r1,#0x10]`; `0x005AE07A: blx r1` | Destroys the local visitor if present. `beq` uses **`cmp r5,r0`** to distinguish an in-place callable from heap-allocated callable; `IT NE` uses **`cmp r0,#0`** to skip null. In-place callable uses virtual destructor slot `+0x10`; heap callable uses slot `+0x14`. The visitor was initialized empty at `sp+0xA0`, so none of these destructor calls should execute for the ordinary no-callback case unless the called helper changes the object (not established). |
| 1.12 | `0x005AE07E: mov r0,r6`; `0x005AE080: cmp r6,#0`; `0x005AE082: it ne`; `0x005AE084: movne r0,#1`; `0x005AE086: ands r0,r5`; `0x005AE088: cmp r0,#1`; `0x005AE08A: bne 0x005AE092` | Normalizes **the BlockWorld search result** to `objectAvailable` (`r6!=0`), computes `objectAvailable & faceAvailable` (`r5` loaded from stack `sp+4` at `0x005AE07C`), and tests whether both are `1`. `movne` uses **`cmp r6,#0`**; `bne` uses **`cmp r0,#1`**, not the `ands` flags. |
| 1.13 | `0x005AE08C: add.w r0,r8,#0x68`; `0x005AE090: b 0x005AE0A8`; `0x005AE092: cmp r5,#1`; `0x005AE094: bne 0x005AE09C`; `0x005AE096: add.w r0,r8,#0x69` | Both available → select **ActivityFreeplay+0x68**. Otherwise compare face-available to literal `1` using **`cmp r5,#1`**: if equal, face-only → **ActivityFreeplay+0x69**. `bne` derives from that `cmp`. |
| 1.14 | `0x005AE09C: cmp r6,#0`; `0x005AE09E: ite eq`; `0x005AE0A0: addeq.w r0,r8,#0x6b`; `0x005AE0A4: addne.w r0,r8,#0x6a` | If face not available, compares the **raw object search return** with `0` by **`cmp r6,#0`**. Search zero → **ActivityFreeplay+0x6B** (neither); nonzero → **ActivityFreeplay+0x6A** (object only). `addeq/addne` use exactly those flags. |
| 1.15 | `0x005AE0A8: ldrb r0,[r0]`; `0x005AE0AA: strb.w r0,[r8,#0x90]` | **Only direct persistent write in this function**: copies the one-byte configured ActivityID from the selected **ActivityFreeplay+0x68/0x69/0x6A/0x6B** to **ActivityFreeplay+0x90**. Does not set ActivityFreeplay+0x91. |
| 1.16 | `0x005AE0AE: blx 0x004AEFA4`; `0x005AE0B2: adr r1,#436`; `0x005AE0C0: movs r0,#8`; `0x005AE0C2: blx 0x004A42A0`; `0x005AE0E4: cmp r6,#0`; `0x005AE0E8: movne r6,#1`; `0x005AE0EC: str r6,[sp]`; `0x005AE0EE: blx 0x004A4F90` | Converts selected ActivityID to a string and emits a diagnostics event. The event string used by `sEventF` is at virtual `0x005AE270`: `robot.goal_from_face_and_cube`. The local `std::vector<pair<const char*,const char*>>` of one element is constructed at `sp+0x90`; event arguments include normalized object result and saved face result. The `movne` tests **`cmp r6,#0`**. No engine-to-game message is constructed here. |
| 1.17 | `0x005AE0F2: ldr r0,[sp,#0x90]`; `0x005AE0F4: cbz r0,0x005AE114`; `0x005AE0F6: ldr r1,[sp,#0x94]`; `0x005AE0F8: cmp r1,r0`; `0x005AE0FA: itttt ne`; `0x005AE110: blx 0x004A40CC` | Frees the diagnostic vector if allocated; `cbz` is a direct null test on the vector's begin pointer; the conditional IT operations take flags from **`cmp end,begin`**. |
| 1.18 | `0x005AE114: add.w r0,r11,#0x60`; `0x005AE118: blx 0x004A70CC`; `0x005AE11C–0x005AE168` (one `destroy` call for each of eight tree sets); `0x005AE16A: add r0,sp,#0x84`; `0x005AE16C: blx 0x004A406C` | Cleans the local filter predicate list and all eight sets; then destroys temporary `Pose3d` (`PoseBase` destructor). These calls do not undo the ActivityFreeplay+0x90 write. Tree destructor targets are identified in the native PLT table. |
| 1.19 | `0x005AE170: ldr r0,[pc,#0x124]`; `0x005AE174: add r0,pc`; `0x005AE176: ldr r0,[r0]`; `0x005AE178: ldr r0,[r0]`; `0x005AE17A: subs r0,r0,r1`; `0x005AE17C: itt eq`; `0x005AE17E: addeq sp,#0xac`; `0x005AE180: popeq.w {...,pc}`; `0x005AE184: blx 0x004A4FE4` | Verifies stack canary. EQ is set by **`subs r0,r0,r1`** comparing current canary to saved value. Match → returns void; mismatch → `__stack_chk_fail`. This failure does not normally rejoin selection. |
| 1.20 | `0x005AE188–0x005AE1D8`, `0x005AE1DA–0x005AE1F4`, `0x005AE1FE–0x005AE258` (entry instruction examples: `0x005AE1B6: cmp r5,r0`; `0x005AE1C8: blx r1`; `0x005AE1FE: mov r0,r10`; `0x005AE200: blx 0x004A70CC`; `0x005AE256: mov r0,r6`; `0x005AE258: blx 0x004A40A8`) | Exception landing pads tear down the visitor (virtual slots `+0x10/+0x14`), temporary family set, filter list/trees, temporary pose and other owned temporaries, then `_Unwind_Resume`. **UNKNOWN:** which precise throws enter each landing pad; the ARM unwind metadata has not been exhaustively mapped here. None of these paths writes a second persistent activity field in this function. |

### Whole `BlockWorldFilter` type used at this call site

`r9=sp+0x10` is the filter base. All tree sets have the native 12-byte libc++ tree-wrapper storage. The layout is established by `FindLocatedObjectHelper`'s filter-copy at `0x0061EB86–0x0061EB94` and its tests at `0x0061EB98–0x0061ED66`:

| Filter offset | Type | Value supplied here |
|---|---|---|
| `+0x00` | Excluded ObjectIDs, `set<ObjectID>` | empty |
| `+0x0C` | Included ObjectIDs, `set<ObjectID>` | empty |
| `+0x18` | Excluded ObjectTypes, `set<ObjectType>` | empty |
| `+0x24` | Included ObjectTypes, `set<ObjectType>` | empty |
| `+0x30` | Excluded ObjectFamilies, `set<ObjectFamily>` | empty |
| `+0x3C` | Included ObjectFamilies, `set<ObjectFamily>` | `{1,2}` |
| `+0x48` | Excluded origin IDs, `set<uint32_t>` | empty |
| `+0x54` | Included origin IDs, `set<uint32_t>` | empty |
| `+0x60` | `list<function<bool(const ObservableObject*)>>` | empty |
| `+0x6C` | Optional world-match predicate flag | `0` |
| `+0x6D` | Origin selector byte | `0` |

**Important:** the filter's `+0x3C` is not ActivityFreeplay+0x3C, and an outer map/tree node's integer key at node `+0x10` is not ObservableObject+0x10.

### Exact selection rule

Let `F = (FaceWorld::GetLastObservedFace(outPose, true) != 0) && (Robot::IsPoseInWorldOrigin(outPose) != 0)`. Let `O = (BlockWorld::FindLocatedObjectHelper(filter: included ObjectFamily {1,2}, visitor: empty, stopAfterFirst: true) != 0)`.

- `F && O`: ActivityFreeplay `+0x90 ← +0x68`.
- `F && !O`: ActivityFreeplay `+0x90 ← +0x69`.
- `!F && O`: ActivityFreeplay `+0x90 ← +0x6A`.
- `!F && !O`: ActivityFreeplay `+0x90 ← +0x6B`.

**Every selected case executes the same final store** at `0x005AE0AA` and diagnostic event at `0x005AE0EE`. No per-case early return.

### OPEN / UNKNOWN (so status remains Partial)

1. The entire internal `Pose3d` member layout and `FaceWorld::GetLastObservedFace`'s own implementation beyond its interface call were not re-extracted here; the called code may have additional effects.
2. The local direct routine at `0x004E02B2` has not been isolated and proved to be a pure string operation.
3. The unwind metadata mapping from every throw site into each exception landing pad is not included; exact conditions leading to exceptions remain UNKNOWN.
4. The runtime semantic *name* for `ObjectFamily` enum values `1` and `2` is not established from this function alone, although the exact numeric values are verified.

No C# changes, manifest changes, or status promotions.



---

## Full extraction section 2

# Queue 4 — item 2 — `ActivityFreeplay::GetDesiredActiveBehaviorInternal`

| Item | Status | Native address range |
|---|---|---|
| 2. GetDesiredActiveBehaviorInternal | **Partial** — ordinary selection/retry/return paths traced; exception handlers and transitive side effects of child activities/strategies not fully closed | `0x005AE29C–0x005AE81A`; literals up to `0x005AE918` |

Native source is `libcozmoEngine.so`, ARMv7 Thumb. PLT targets below are identified by its `.rel.plt` entries. The native function uses a **hidden return pointer** in `r0`, `ActivityFreeplay*` in `r1`, `Robot&` in `r2`, and pointer to an incoming `shared_ptr<IBehavior>` in `r3`. The latter pointer is saved in `r6` and may designate a two-word pair (`IBehavior*`, shared control block). Do not confuse `r6+0` with an offset of `ActivityFreeplay`.

## Synchronous native steps

| Step | Native instructions; actual condition setter | Behavior and owning base |
|---|---|---|
| 2.01 | `0x005AE2A2: mov r10,r0`; `0x005AE2A4: movs r0,#0`; `0x005AE2A6: mov r4,r1`; `0x005AE2A8: strd r0,r0,[r10]`; `0x005AE2B0: mov r6,r3`; `0x005AE2B2: ldr.w r11,[r4,#0x80]`; `0x005AE2B6: mov r7,r2` | Clears both fields in the **output `shared_ptr<IBehavior>`** (base `r10`) to null. Saves **ActivityFreeplay* `r4`**, **Robot& `r7`**, **incoming shared_ptr pointer `r6`**, and **ActivityFreeplay+0x80** current child `IActivity*` (`r11`). |
| 2.02 | `0x005AE2AC: ldrb.w r0,[r4,#0x91]`; `0x005AE2B8: cbz r0,0x005AE2F4`; `0x005AE2BA: cmp.w r11,#0`; `0x005AE2BE: beq 0x005AE2CC`; `0x005AE2C0: ldrb.w r1,[r11,#0x34]`; `0x005AE2C6: cmp r1,r0`; `0x005AE2C8: beq.w 0x005AE65A` | **Requested activity** path: if `ActivityFreeplay+0x91 != 0` (the `cbz` tests loaded byte directly), compare **current child activity pointer** to null using `cmp r11,#0`. If present and **current IActivity+0x34** equals requested activity byte (`cmp r1,r0`), reuse the current child (branch to 2.19). Otherwise emit informational diagnostic and enter selection at 2.07. |
| 2.03 | `0x005AE2CC: movs r1,#0`; `0x005AE2D6: blx 0x004AEFA4`; `0x005AE2EC: mov r0,r6`; `0x005AE2EE: blx 0x004A505C`; `0x005AE2F2: b 0x005AE3E0` | Requested ID cannot be fulfilled by simply keeping the current child. Converts requested ID to string and calls `sChanneledInfoF`; then proceeds to spark selection. Temporary log vector is cleared at 2.07. The native log does not itself change requested ID. |
| 2.04 | `0x005AE2F4: ldrb.w r0,[r4,#0x90]`; `0x005AE2F8: cbz r0,0x005AE338`; `0x005AE2FA: cmp.w r11,#0`; `0x005AE2FE: beq 0x005AE30C`; `0x005AE300: ldrb.w r1,[r11,#0x34]`; `0x005AE306: cmp r1,r0`; `0x005AE308: beq.w 0x005AE65A` | If no requested `+0x91`, a nonzero **object-derived ActivityFreeplay+0x90** takes priority. Existing child with matching **IActivity+0x34** is reused; if absent or unequal, the function logs and proceeds to 2.07. `cbz` tests selected ID directly; `beq` tests `cmp r1,r0` on actual IDs. |
| 2.05 | `0x005AE30C: movs r1,#0`; `0x005AE316: blx 0x004AEFA4`; `0x005AE330: mov r0,r6`; `0x005AE332: blx 0x004A505C`; `0x005AE336: b 0x005AE3E0` | Logs the nonmatching object-derived ID and enters selection. Diagnostic uses temporary vector and event strings; no persistent activity-state store in this arm. |
| 2.06 | `0x005AE338: ldr r0,[r7,#0x44]`; `0x005AE33C: ldr r2,[r0,#0x58]`; `0x005AE33E: cmp r2,#0x55`; `0x005AE340: bne 0x005AE34A`; `0x005AE342: ldr r2,[r0,#0x60]`; `0x005AE344: cmp r2,#0x55`; `0x005AE346: it ne`; `0x005AE348: movne r1,#1`; `0x005AE34A: cbnz r1,0x005AE3B4` | With no requested/object-derived ID, checks two **BehaviorManager** (`Robot+0x44`) `UnlockId` fields: manager `+0x58` and `+0x60`, against integer sentinel `0x55`=85. `bne` uses the comparison of manager+0x58 to 85; `movne` uses the manager+0x60 comparison to 85. `cbnz r1` is a *direct test of the derived flag* which becomes 1 only if manager+0x58 is 85 and manager+0x60 is not 85. If this flag is 1, branch to selection. |
| 2.07 | `0x005AE34C: cmp.w r11,#0`; `0x005AE350: beq 0x005AE3B4`; `0x005AE352: ldr r0,[r6]`; `0x005AE356: cmp r0,#0`; `0x005AE358: bne.w 0x005AE65A`; `0x005AE35C: ldr r0,[r7]`; `0x005AE360: ldr r0,[r0,#0x34]`; `0x005AE362: ldrb.w r0,[r0,#0x3d8]`; `0x005AE366: cmp r0,#0`; `0x005AE368: bne.w 0x005AE65A` | With sentinel/current selection fallback, if **ActivityFreeplay+0x80** child exists (`cmp r11,#0`), retain it when supplied **incoming shared_ptr's IBehavior* `*r6`** is nonnull (`cmp r0,#0`), or when **Robot→NeedsManager+0x3D8** pending reward is nonzero (`cmp r0,#0`). These lead to 2.19. If no current child, go to new selection. |
| 2.08 | `0x005AE36C: ldr.w r0,[r11,#0x20]`; `0x005AE370: ldr.w r2,[r11,#0x54]`; `0x005AE374: mov r1,r7`; `0x005AE376: blx 0x004B19C8`; `0x005AE37A: cmp r0,#0`; `0x005AE37C: beq.w 0x005AE656` | Current **IActivity+0x20** strategy and **IActivity+0x54** floating argument feed `IActivityStrategy::WantsToEnd(Robot&, float)`. If result equals zero, reuse the current child at 2.19 (`cmp result,#0`, then `beq`). Nonzero result logs and enters selection. This is distinct from the later retry's stricter `==1` check. |
| 2.09 | `0x005AE380: movs r0,#0`; `0x005AE392: blx 0x004AEFA4`; `0x005AE3AE: blx 0x004A505C`; `0x005AE3B2: b 0x005AE3E0`; `0x005AE3B4: movs r1,#0`; `0x005AE3C0: blx 0x004AD258`; `0x005AE3DC: blx 0x004A505C` | If existing strategy wants to end, logs current `IActivity+0x34` ActivityID. Other fallback cases log `BehaviorManager+0x60` UnlockId (enum conversion). All rejoin at `0x005AE3E0` for temporary-vector destruction; none returns early. |
| 2.10 | `0x005AE3E0: ldr r0,[sp,#0x20]`; `0x005AE3E2: cbz r0,0x005AE402`; `0x005AE3E6: cmp r1,r0`; `0x005AE3E8: itttt ne`; `0x005AE3FE: blx 0x004A40CC`; `0x005AE402: mov.w r8,#1`; `0x005AE406: mov.w r9,#0`; `0x005AE40A: movs r3,#1` | Destroys log vector if nonnull. `cbz` directly tests vector begin. `IT NE` uses `cmp end,begin`. For first selection pass, initializes selection/retry variables: `r8=1`, `r9=0`, default boolean `r3=1`. |
| 2.11 | `0x005AE40C: ldr r0,[r7,#0x44]`; `0x005AE40E: ldr r6,[r0,#0x58]`; `0x005AE410: ldr r1,[r0,#0x60]`; `0x005AE412: cmp r6,r1`; `0x005AE414: bne 0x005AE466`; `0x005AE416: ldrb.w r0,[r0,#0x65]`; `0x005AE41A: cbz r0,0x005AE468` | At selection/retry, read **BehaviorManager+0x58** and **+0x60** UnlockIds. If unequal (`cmp r6,r1` / `bne`), select the second ID `+0x60` at `0x005AE466`. If equal, test **BehaviorManager+0x65** directly; zero goes to picker with `r3=1`; nonzero executes extra diagnostic and changes boolean. |
| 2.12 | `0x005AE41C: ldr.w r0,[pc,#0x4b0]`; `0x005AE420: movs r2,#0`; `0x005AE438: blx 0x004A505C`; `0x005AE45E: ldr r0,[r7,#0x44]`; `0x005AE460: movs r3,#0`; `0x005AE462: ldr r6,[r0,#0x60]`; `0x005AE466: mov r6,r1` | When the IDs match and `BehaviorManager+0x65` is nonzero, logs a message, then sets picker boolean `r3=0` and uses manager+0x60. Unequal IDs use second ID via `mov r6,r1` while retaining original default `r3=1`. Same-ID and zero flag go straight to picker with previous `r3`. |
| 2.13 | `0x005AE468: mov r0,r4`; `0x005AE46A: mov r1,r7`; `0x005AE46C: mov r2,r6`; `0x005AE46E: blx 0x004B19E0`; `0x005AE472: mov r5,r0`; `0x005AE474: orr.w r0,r8,r5`; `0x005AE478: cmp r0,#1`; `0x005AE47A: bne.w 0x005AE5DE` | Calls `ActivityFreeplay::PickNewActivityForSpark(Robot&,UnlockId,bool)` (picker is item 3). `r5` is picker return, `r8` previous pass/retry variable. Only if **bitwise `r8 OR r5` equals exactly `1`** (not merely nonzero) proceeds; else advances to clearing selected ID and return. `bne` tests `cmp (r8|r5),#1`. |
| 2.14 | `0x005AE47E: cmp r5,#1`; `0x005AE480: bne 0x005AE4F6`; `0x005AE482: ldr r0,[r7,#0x44]`; `0x005AE484: blx 0x004B19EC` | If picker result **exactly `1`** (`cmp r5,#1`), calls `BehaviorManager::SwitchToRequestedSpark()`. If it is not `1` but OR check passed, does not call this manager method. |
| 2.15 | `0x005AE488: ldr.w r0,[r4,#0x80]`; `0x005AE48C: cbz r0,0x005AE4E2`; `0x005AE48E: ldr r0,[r0,#0x4c]`; `0x005AE490: cmp r0,r6`; `0x005AE492: beq 0x005AE4E2`; `0x005AE49C: blx 0x004AD258`; `0x005AE4A4: blx 0x004AD258`; `0x005AE4BC: blx 0x004A4540` | On picker result 1, if **current child IActivity** exists and **IActivity+0x4C** UnlockId differs from the selected ID `r6` (flags from `cmp r0,r6`), issues warning with both enum names. The `cbz` is direct child-pointer test. Does not abort selection; goes on after cleanup. |
| 2.16 | `0x005AE4E2: ldr.w r0,[r7,#0x264]`; `0x005AE4E8: cmp r6,#0x55`; `0x005AE4EA: ldr r0,[r0,#0x2c]`; `0x005AE4EC: it ne`; `0x005AE4EE: movne r1,#1`; `0x005AE4F0: movs r2,#1`; `0x005AE4F2: blx 0x004A7B4C` | Calls `FreeplayDataTracker::SetFreeplayPauseFlag(bool,FreeplayPauseFlag)` on `Robot+0x264 → pointee+0x2C`. Passes bool `(selected UnlockId != 0x55)` and pause-flag enum value **1**; `movne` consumes `cmp r6,#0x55`, and r1 defaults zero at `0x005AE4E6`. It is a transitive state write in another object, not an `ActivityFreeplay` field. |
| 2.17 | `0x005AE4F6: ldr.w r6,[r4,#0x80]`; `0x005AE4FA: cmp r6,#0`; `0x005AE4FC: beq 0x005AE5A4`; `0x005AE4FE: ldr r0,[sp,#0xc]`; `0x005AE500: ldrd r1,r0,[r0]`; `0x005AE504: cmp r0,#0`; `0x005AE50A: beq 0x005AE510`; `0x005AE50C: blx 0x004A5014` | If there is a current **ActivityFreeplay+0x80 child**, copy incoming behavior shared-pointer to a local pair and conditionally increment control-block shared count (nonzero test `cmp r0,#0`). Absent child logs a separate path and skips the child call. The shared pair owns `IBehavior*` and control block (two words). |
| 2.18 | `0x005AE510: add r0,sp,#0x20`; `0x005AE512: add r3,sp,#0x10`; `0x005AE514: mov r1,r6`; `0x005AE516: mov r2,r7`; `0x005AE518: blx 0x004B0FA8`; `0x005AE51C: ldrd r7,r6,[sp,#32]`; `0x005AE520: movs r0,#0`; `0x005AE522: cmp.w r9,#0`; `0x005AE526: strd r0,r0,[sp,#32]`; `0x005AE52A: strd r7,r6,[r10]` | Invokes `IActivity::GetDesiredActiveBehavior(Robot&,incomingSharedPtr)` on selected child to obtain an output shared_ptr in local `sp+0x20`. Moves that pair into the caller's **output shared_ptr** at `r10`, clearing local ownership first. Any release of prior controls is handled in following cleanup; the `cmp r9,#0` flags are used at 2.19. |
| 2.19 | `0x005AE52E: beq 0x005AE53E`; `0x005AE530: mov r0,r9`; `0x005AE532: blx 0x004A4EF4`; `0x005AE536: ldr r0,[sp,#0x24]`; `0x005AE538: cbz r0,0x005AE53E`; `0x005AE53A: blx 0x004A4EF4`; `0x005AE53E: ldr r0,[sp,#0x14]`; `0x005AE540: cbz r0,0x005AE546`; `0x005AE542: blx 0x004A4EF4` | Releases shared ownership for prior local controls; `beq` at `0x005AE52E` specifically uses **`cmp r9,#0`** at `0x005AE522`; `cbz` checks each local control pointer directly. This cleanup occurs before checking output behavior. |
| 2.20 | `0x005AE546: cmp r7,#0`; `0x005AE548: beq 0x005AE5E0`; `0x005AE54A: eors r0,r5,#1`; `0x005AE54E: bne 0x005AE5E0`; `0x005AE560: blx 0x004AEFA4`; `0x005AE57C: blx 0x004A505C` | If **returned IBehavior* `r7`** is nonnull and **picker result `r5` equals 1**, logs the new activity. `beq` tests `cmp r7,#0`; `bne` tests flags from **`eors r0,r5,#1`**, where zero means `r5==1`. Then proceeds to 2.23. |
| 2.21 | `0x005AE5A4: ldr.w r0,[pc,#0x348]`; `0x005AE5B8: blx 0x004A505C`; `0x005AE5DE: mov r6,r9` | If selected `ActivityFreeplay+0x80` child is null, emits informational diagnostic, then enters the common selected-ID clearing and exit logic. No child behavior method called. |
| 2.22 | `0x005AE656: ldr.w r5,[r4,#0x80]`; `0x005AE65A: ldrd r1,r0,[r6]`; `0x005AE65E: cmp r0,#0`; `0x005AE660: strd r1,r0,[sp,#24]`; `0x005AE664: beq 0x005AE66A`; `0x005AE666: blx 0x004A5014`; `0x005AE66A: add r0,sp,#0x20`; `0x005AE66E: mov r1,r5`; `0x005AE670: mov r2,r7`; `0x005AE672: blx 0x004B0FA8`; `0x005AE680: strd r5,r9,[r10]` | **Keep-current-child path** from a matched ActivityID, nonnull incoming behavior, pending reward, or `WantsToEnd==0`: clones incoming shared ptr, gets desired behavior from the existing child and moves resulting `(IBehavior*,control)` into output. `beq` tests `cmp r0,#0` on incoming shared control block. This path does not switch spark or run picker before the child call. |
| 2.23 | `0x005AE5E0: ldrb.w r0,[r4,#0x90]`; `0x005AE5E4: cbz r0,0x005AE5EC`; `0x005AE5E6: movs r0,#0`; `0x005AE5E8: strb.w r0,[r4,#0x90]` | **Persistent ActivityFreeplay write:** clears `ActivityFreeplay+0x90` to `0` if it was nonzero, after attempting an activity selection. Does not clear requested byte `ActivityFreeplay+0x91`. `cbz` tests loaded byte directly. |
| 2.24 | `0x005AE5EE: cmp.w r11,#0`; `0x005AE5F2: itt eq`; `0x005AE5F4: ldreq.w r0,[r4,#0x80]`; `0x005AE5F8: cmpeq r0,#0`; `0x005AE5FA: beq 0x005AE602`; `0x005AE5FC: add sp,#0x2c`; `0x005AE5FE: pop.w {...,pc}` | Returns void after producing output, *unless both* captured initial child (`r11`) and current `ActivityFreeplay+0x80` are null. The second `cmpeq` is **IT-predicated** by the first comparison; `beq` observes the final condition. If both null, enters error diagnostic at 2.25. |
| 2.25 | `0x005AE602: ldr r0,[pc,#0x308]`; `0x005AE614: blx 0x004A4108`; `0x005AE63A: ldr r0,[pc,#0x2d8]`; `0x005AE63C: movs r2,#1`; `0x005AE646: ldr r1,[r1]`; `0x005AE648: ldrb r0,[r0]`; `0x005AE64A: strb r2,[r1]`; `0x005AE64C: cmp r0,#0`; `0x005AE64E: beq 0x005AE5FC`; `0x005AE650: blx 0x004A4114` | The no-initial/no-current-child error path **logs**, sets a **global error byte** to `1` via a GOT-loaded pointer (not an ActivityFreeplay field), and optionally calls `sDebugBreakOnError` if separate global debug flag was nonzero. `beq` uses **`cmp r0,#0`** testing that debug flag, then re-enters the same ordinary function return. Error path is *not* a return before its global write. Exact global identities and data-VA derivation need further inspection. |
| 2.26 | `0x005AE68C: cbz r5,0x005AE70A`; `0x005AE68E: ldrb.w r0,[r5,#0xa1]`; `0x005AE692: cmp r0,#0`; `0x005AE694: bne 0x005AE5EE`; `0x005AE696: ldr r0,[r7]`; `0x005AE698: ldr r0,[r0,#0x34]`; `0x005AE69A: ldrb.w r0,[r0,#0x3d8]`; `0x005AE69E: cmp r0,#0`; `0x005AE6A0: bne 0x005AE5EE` | After the **keep-current-child** call, output `IBehavior* r5` null triggers retry; otherwise return immediately if **IBehavior+0xA1** byte nonzero or **Robot→NeedsManager+0x3D8** pending reward nonzero. `bne`s use the corresponding `cmp value,#0`, not another condition. No ActivityFreeplay+0x90 clear on those direct returns. |
| 2.27 | `0x005AE6A2: ldr.w r1,[r4,#0x80]`; `0x005AE6A6: ldr r0,[r1,#0x20]`; `0x005AE6A8: ldr r2,[r1,#0x54]`; `0x005AE6AA: mov r1,r7`; `0x005AE6AC: blx 0x004B19C8`; `0x005AE6B0: cmp r0,#1`; `0x005AE6B2: bne 0x005AE5EE` | If returned `IBehavior+0xA1 == 0` and no pending reward, query current child `IActivityStrategy::WantsToEnd(Robot&,float)` again. **Only exact result 1** (`cmp r0,#1`) enables retry; other result returns. This differs from initial gate `WantsToEnd!=0` at 2.08. |
| 2.28 | `0x005AE6B4: movs r0,#0`; `0x005AE6C6: blx 0x004AEFA4`; `0x005AE6DC: blx 0x004A505C`; `0x005AE702: movs r3,#1`; `0x005AE704: mov.w r8,#0`; `0x005AE708: b 0x005AE40C` | On exact strategy result 1, logs, then **retries selection** from `0x005AE40C` with `r8=0`, picker boolean initially `r3=1`. Existing selected output pair is not overwritten here until a later child call; exact control-block lifetime effects of the retry are UNKNOWN. |
| 2.29 | `0x005AE70A: movs r0,#0`; `0x005AE71C: blx 0x004AEFA4`; `0x005AE736: blx 0x004A505C`; `0x005AE75C: mov.w r8,#0`; `0x005AE760: movs r3,#0`; `0x005AE762: b 0x005AE40C` | If the child returned a null IBehavior pointer, logs and re-enters selection with `r8=0` **and picker boolean `r3=0`**. The target is the same loop at `0x005AE40C`. |
| 2.30 | `0x005AE75E–0x005AE818` (e.g. `0x005AE7D8: ldr r0,[sp,#0x1c]`; `0x005AE7DA: cmp r0,#0`; `0x005AE7DE: blxne 0x004A4EF4`; `0x005AE816: mov r0,r4`; `0x005AE818: blx 0x004A40A8`) | Exception landing pads release vectors and shared_ptr controls and rethrow via `_Unwind_Resume`. `blxne` consumes `cmp r0,#0`. The complete unwind-table coverage mapping and every exceptional entry path are **UNKNOWN**, rather than being called ordinary diagnostics. |

## Observed type/offset census

| Owning base | Offset | Access/type | Evidence |
|---|---|---|---|
| `ActivityFreeplay` | `+0x80` | Current child `IActivity*` | `0x005AE2B2`, `0x005AE4F6`, `0x005AE656` |
| `ActivityFreeplay` | `+0x90` | Object-derived desired `ActivityID` byte, consumed/cleared | `0x005AE2F4`, `0x005AE5E0`, `0x005AE5E8` |
| `ActivityFreeplay` | `+0x91` | Explicit requested/override ActivityID byte | `0x005AE2AC` |
| `IActivity` child | `+0x20` | `IActivityStrategy*` | `0x005AE36C`, `0x005AE6A6` |
| `IActivity` child | `+0x34` | `ActivityID` byte | `0x005AE2C0`, `0x005AE300` |
| `IActivity` child | `+0x4C` | `UnlockId` field | `0x005AE48E` |
| `IActivity` child | `+0x54` | float argument to `WantsToEnd` | `0x005AE370`, `0x005AE6A8` |
| `BehaviorManager` (`Robot+0x44`) | `+0x58` | `UnlockId`, compared with 85 and +0x60 | `0x005AE33C`, `0x005AE40E` |
| `BehaviorManager` (`Robot+0x44`) | `+0x60` | `UnlockId`, compared with 85 and +0x58 | `0x005AE342`, `0x005AE410` |
| `BehaviorManager` (`Robot+0x44`) | `+0x65` | one-byte guard in special same-ID selection | `0x005AE416` |
| `Robot` | `+0x34` | `NeedsManager*` for pending-reward check | `0x005AE360`, `0x005AE698` |
| `NeedsManager` | `+0x3D8` | pending spark reward flag | `0x005AE362`, `0x005AE69A` |
| `Robot` | `+0x264` | component pointer containing tracker at +0x2C | `0x005AE4E2`, `0x005AE4EA` |
| `FreeplayDataTracker` | its own `+0x2C` | target of SetFreeplayPauseFlag | `0x005AE4EA–0x005AE4F2` |
| returned `IBehavior` | `+0xA1` | readiness/restriction byte tested for nonzero | `0x005AE68E` |
| output `shared_ptr<IBehavior>` | `+0x00,+0x04` | IBehavior pointer and control block | `0x005AE2A8`, `0x005AE52A`, `0x005AE680` |
| incoming `shared_ptr<IBehavior>` | `+0x00,+0x04` | IBehavior pointer and control block | `0x005AE352`, `0x005AE500`, `0x005AE65A` |

## Exact observed branch outcomes

- Requested `ActivityFreeplay+0x91 != 0`: reuse current child if current `IActivity+0x34 == requested`; otherwise select a new child.
- Else object-derived `ActivityFreeplay+0x90 != 0`: reuse current child if current `IActivity+0x34 == desired`; otherwise select a new child.
- Else, if the current manager unlock-state combination indicates a pending change, or no child exists: select a new child. With a child, retain it if incoming IBehavior* is nonnull OR NeedsManager has a pending reward OR `IActivityStrategy::WantsToEnd(...) == 0`; otherwise select.
- Selection invokes `PickNewActivityForSpark`, and only continues to child retrieval when `(firstPassFlag | pickerResult)==1`. On `pickerResult==1`, it also calls `SwitchToRequestedSpark` and updates the Freeplay pause flag to `(selectedUnlockId != 85, flag 1)`.
- After child selection, `ActivityFreeplay+0x90` is cleared if nonzero. `+0x91` is not written.
- When a returned behavior exists on the reuse path and `IBehavior+0xA1==0` and pending reward is zero, exactly `WantsToEnd(...) == 1` causes another picker pass; no behavior result also triggers a retry.
- If both initial and current child are null at return, the error path sets its global error byte before returning.

## UNKNOWN / incompleteness

1. Downstream implementation and every state write of `PickNewActivityForSpark`, `SwitchToRequestedSpark`, `IActivity::GetDesiredActiveBehavior`, `WantsToEnd`, and `SetFreeplayPauseFlag` must be independently read to establish their *transitive* effects (some are covered by later queue items).
2. Full names of error globals in the no-child path are not resolved to virtual addresses in this file.
3. Exact exception landing-pad entries and their potential effects on shared-pointer lifetime are not completely mapped.
4. The native loop has two retry edges. An upper bound or proof of termination under every possible callback outcome has **not** been established.

No project code, manifest entry, or review status was changed.



---

## Full extraction section 3

# Queue 4 — item 3 — `PickNewActivityForSpark` and `SetActivityIDFromSubActivity`

| Item | Status | Native address range |
|---|---|---|
| 3. Selection and SetActivityIDFromSubActivity | **Partial** — branch selection, direct state writes and calls traced; non-PLT string helper details and exceptional flows not exhausted | `0x005ADC44–0x005ADE9A`; `0x005AD8C8–0x005AD9C8` |

All quoted instructions are from the ARMv7 Thumb `libcozmoEngine.so`, not decompiler pseudo-C. PLT labels use `.rel.plt`. All offsets in the tables below have a named owner. `ActivityFreeplay+0x6C` is a native `unordered_map<UnlockId, vector<unique_ptr<IActivity>>>`, not a sorted list of UnlockIDs. Only **candidate vector order within a particular map entry** is used here; no map-wide iteration occurs.

## 3A. `PickNewActivityForSpark`

| Step | Native instructions and flag provenance | Established behavior; owning base |
|---|---|---|
| 3.01 | `0x005ADC4A: mov r4,r0`; `0x005ADC4C: mov r8,r1`; `0x005ADC4E: add.w r0,r4,#0x6c`; `0x005ADC52: add r1,sp,#0x18`; `0x005ADC54: mov r6,r3`; `0x005ADC56: str r2,[sp,#0x18]`; `0x005ADC58: blx 0x004B19B0` | Arguments are `ActivityFreeplay*`, `Robot&`, `UnlockId`, `bool`. Looks up **UnlockId argument** in **ActivityFreeplay+0x6C** `unordered_map` using native `__hash_table::find`. Stores `bool` in `r6`, Robot in `r8`; `UnlockId` in `sp+0x18`. |
| 3.02 | `0x005ADC5C: cbz r0,0x005ADCD8`; `0x005ADC5E: ldrd r5,r7,[r0,#12]`; `0x005ADC62: add.w r9,r4,#0x80`; `0x005ADC66: cmp r5,r7`; `0x005ADC68: beq 0x005ADCB6` | Missing **hash-node pointer** (`cbz` directly on `r0`) goes to warning/return 0 (3.14). Existing node: reads begin/end of its value `vector<unique_ptr<IActivity>>` from **hash node+0x0C/+0x10**, each vector element a four-byte `IActivity*`; `cmp begin,end` and `beq` test empty vector. `r9` points to the **ActivityFreeplay+0x80** current-child pointer field. |
| 3.03 | `0x005ADC6A: ldrb.w r0,[r4,#0x91]`; `0x005ADC6E: ldr r1,[r5]`; `0x005ADC70: cbnz r0,0x005ADC78`; `0x005ADC72: ldrb.w r0,[r4,#0x90]`; `0x005ADC76: cbz r0,0x005ADC88` | For each vector candidate `IActivity*` at `*r5`, first use **ActivityFreeplay+0x91** override if nonzero. Otherwise use **ActivityFreeplay+0x90** desired ID if nonzero. `cbnz` and `cbz` directly test those loaded bytes; neither uses prior APSR flags. If both zero, use strategy gates at 3.05–3.06. |
| 3.04 | `0x005ADC78: ldrb.w r1,[r1,#0x34]`; `0x005ADC7C: cmp r1,r0`; `0x005ADC7E: beq 0x005ADCB2` | With desired/override activity ID, accept the first candidate whose **IActivity+0x34** ActivityID is *equal* to that selected ID. `beq` consumes **`cmp r1,r0`** on the actual two bytes, not another guard. It does not ask WantsToStart/End in this branch. Nonmatch continues to next vector element at 3.07. |
| 3.05 | `0x005ADC88: ldr.w r2,[r9]`; `0x005ADC8C: ldr r0,[r1,#0x20]`; `0x005ADC8E: cmp r1,r2`; `0x005ADC90: beq 0x005ADCA2`; `0x005ADC92: ldrd r3,r2,[r1,#84]`; `0x005ADC96: mov r1,r8`; `0x005ADC98: blx 0x004B19BC`; `0x005ADC9C: cmp r0,#1`; `0x005ADC9E: bne 0x005ADC80` | When no explicit desired ID, for a **candidate `IActivity*` different from the current ActivityFreeplay+0x80 child** (`cmp r1,r2`, then `beq`), call **candidate IActivity+0x20** `IActivityStrategy::WantsToStart(Robot&,float,float)`, with native `r2 = IActivity+0x58`, `r3 = IActivity+0x54`. Only exact return **1** (`cmp r0,#1`; `bne` rejects otherwise) selects the candidate; anything else goes to next vector entry. |
| 3.06 | `0x005ADCA2: cmp r6,#1`; `0x005ADCA4: bne 0x005ADC80`; `0x005ADCA6: ldr r2,[r1,#0x54]`; `0x005ADCA8: mov r1,r8`; `0x005ADCAA: blx 0x004B19C8`; `0x005ADCAE: cmp r0,#0`; `0x005ADCB0: bne 0x005ADC80` | For a candidate equal to the current child, `bool` input must equal exactly **1** (`cmp r6,#1`) and its **IActivityStrategy+method WantsToEnd(Robot&, IActivity+0x54)** must return **exactly 0** (`cmp r0,#0` then `bne` rejects). Otherwise skip that candidate. |
| 3.07 | `0x005ADC80: adds r5,#4`; `0x005ADC82: cmp r7,r5`; `0x005ADC84: bne 0x005ADC6A`; `0x005ADC86: b 0x005ADCB6`; `0x005ADCB2: ldr r5,[r5]`; `0x005ADCB6: movs r5,#0` | A rejected candidate increments by one 4-byte element. `bne` tests **`cmp end,cursor`**, retaining candidate-vector order. Match loads the selected `IActivity*`. Exhaustion or empty vector selects null. |
| 3.08 | `0x005ADCB8: ldr.w r0,[r9]`; `0x005ADCBC: cmp r0,r5`; `0x005ADCBE: beq 0x005ADCF8` | Compare **existing ActivityFreeplay+0x80 child** with selected `IActivity*`, including null. If identical, **do not switch**, branch to 3.12. `beq` tests `cmp oldPtr,newPtr`. |
| 3.09 | `0x005ADCC0: movs r1,#0`; `0x005ADCC2: cmp r0,#0`; `0x005ADCCA: beq 0x005ADD4E`; `0x005ADCCC: ldrb.w r0,[r0,#0x34]`; `0x005ADCD0: blx 0x004AEFA4` | If selected pointer differs, optionally convert old child's **IActivity+0x34** ActivityID to its native string for logging. The `beq` tests **`cmp oldPtr,#0`**. Null old-child chooses a literal instead. All branches proceed through diagnostics to the switch at 3.11. |
| 3.10 | `0x005ADD4E: adr r6,#336`; `0x005ADD50: cbz r5,0x005ADD5E`; `0x005ADD52: ldrb.w r0,[r5,#0x34]`; `0x005ADD56: blx 0x004AEFA4`; `0x005ADD62: blx 0x004AD258`; `0x005ADD72: blx 0x004A4F90`; `0x005ADDA0: ldr.w r0,[r9]`; `0x005ADDA4: cbz r0,0x005ADDB2`; `0x005ADDAA: blx 0x004AEFA4`; `0x005ADDB4: cbz r5,0x005ADDC0`; `0x005ADDBA: blx 0x004AEFA4`; `0x005ADDD4: blx 0x004A505C` | Emits an activity-change event then an informational diagnostic naming previous, next and UnlockId values. `cbz` tests new/old child pointer directly; a null gets a literal instead of enum name. Temporary diagnostic vectors are destroyed at `0x005ADD76–0x005ADD98` and `0x005ADDD8–0x005ADDFA`. No return before switch. |
| 3.11 | `0x005ADDFA: ldr.w r0,[r9]`; `0x005ADDFE: cbz r0,0x005ADE06`; `0x005ADE00: mov r1,r8`; `0x005ADE02: blx 0x004B0EAC`; `0x005ADE06: cmp r5,#0`; `0x005ADE08: str.w r5,[r9]`; `0x005ADE0C: beq 0x005ADE22`; `0x005ADE0E: ldrb.w r1,[r5,#0x34]`; `0x005ADE12: mov r0,r4`; `0x005ADE14: blx 0x004B19D4`; `0x005ADE18: ldr.w r0,[r9]`; `0x005ADE1C: mov r1,r8`; `0x005ADE1E: blx 0x004B0EC4` | **Actual transition:** if old current child nonnull, call **old IActivity::OnDeselected(Robot&)**. Store selected `IActivity*` (possibly null) into **ActivityFreeplay+0x80**. If selected nonnull (`cmp r5,#0` / `beq`), call `SetActivityIDFromSubActivity(selected IActivity+0x34)` and **new IActivity::OnSelected(Robot&)** in that order. A transition *to null* skips both calls but still returns 1; it leaves `+0x92` / display name unchanged, absent another callee write. |
| 3.12 | `0x005ADCF8: cmp r5,#0`; `0x005ADCFA: beq.w 0x005ADE26`; `0x005ADD06: ldrb.w r0,[r5,#0x34]`; `0x005ADD0A: blx 0x004AEFA4`; `0x005ADD12: blx 0x004AD258`; `0x005ADD38: cmp r4,r7`; `0x005ADD3C: moveq r5,r2`; `0x005ADD48: blx 0x004A505C`; `0x005ADD4C: b 0x005ADE44` | If selected == existing and nonnull, logs retaining that child, with ActivityID and UnlockId; **returns 0** at 3.15. When both selected and current are null, logs a warning at 3.13 and returns 0. The logging selection compares the **override ActivityFreeplay+0x91** against selected child's ID: `cmp r4,r7`; `moveq` reads this comparison's flags. |
| 3.13 | `0x005ADE26: ldr r0,[sp,#0x18]`; `0x005ADE30: blx 0x004AD258`; `0x005ADE40: blx 0x004A4540`; `0x005ADE44: ldr r0,[sp,#0x0c]`; `0x005ADE46: cbz r0,0x005ADE66` | With no candidate/current child, logs no activity for that UnlockId (warning), then cleans temporary diagnostics. `cbz` tests vector begin pointer directly. It does **not** call `OnDeselected` because current child is already null. |
| 3.14 | `0x005ADCD8: ldr r0,[sp,#0x18]`; `0x005ADCE2: blx 0x004AD258`; `0x005ADCF2: blx 0x004A4540`; `0x005ADCF6: b 0x005ADE44` | If the UnlockId is missing from **ActivityFreeplay+0x6C hash map**, log warning and return 0. Crucially, does **not** clear or replace current ActivityFreeplay+0x80 child. |
| 3.15 | `0x005ADE22: movs r0,#1`; `0x005ADE24: b 0x005ADE68`; `0x005ADE66: movs r0,#0`; `0x005ADE68: add sp,#0x1c`; `0x005ADE6A: pop.w {r4,r5,r6,r7,r8,r9,pc}` | Returns `1` only on an actual **pointer change** (including change to null); returns `0` on unchanged pointer, missing hash entry, or no candidate/current. The checks selecting those outcomes are proved above, not inferred from enum names. |
| 3.16 | `0x005ADE76: mov r4,r0`; `0x005ADE7A: cbz r0,0x005ADE98`; `0x005ADE7E: cmp r1,r0`; `0x005ADE80: beq 0x005ADE94`; `0x005ADE94: blx 0x004A40CC`; `0x005ADE98: mov r0,r4`; `0x005ADE9A: blx 0x004A40A8` | Exception cleanup landing pads free diagnostics and resume unwinding. `cbz` is direct pointer test, `beq` uses `cmp end,begin`. Exact throwing sites mapped to these pads are **UNKNOWN**. |

## 3B. `SetActivityIDFromSubActivity`

| Step | Native instructions and flag provenance | Established behavior; owning base |
|---|---|---|
| 3.17 | `0x005AD8CC: mov r4,r0`; `0x005AD8CE: strb.w r1,[r4,#0x92]` | Directly stores the **argument ActivityID byte** at **ActivityFreeplay+0x92**. This is distinct from `+0x90` and `+0x91`. No gate. |
| 3.18 | `0x005AD8D2: ldrb.w r0,[r4,#0x34]`; `0x005AD8D6: blx 0x004AEFA4`; `0x005AD8E4: blx 0x004A44E0`; `0x005AD8EE: mov r1,r5`; `0x005AD8F0: bl 0x004E02B2` | Converts **ActivityFreeplay inherited IActivity+0x34** to string, gets its `strlen`, and builds local native string at `sp+0`. Its role is a name prefix; no ActivityFreeplay field is changed by this step. |
| 3.19 | `0x005AD8F4: adr r1,#212`; `0x005AD8F8: movs r2,#1`; `0x005AD8FA: bl 0x004E8048` | Appends a single `_` character from **virtual address `0x005AD9CC`** to the local name. `adr` uses the aligned Thumb PC and resolves to that exact byte (`0x5F`). The direct callee's internal memory-management branches are not traced in this item. |
| 3.20 | `0x005AD90E: ldrb.w r0,[r4,#0x92]`; `0x005AD912: blx 0x004AEFA4`; `0x005AD91A: blx 0x004A44E0`; `0x005AD920: add r0,sp,#0x10`; `0x005AD924: bl 0x004E8048` | Converts the newly stored `ActivityFreeplay+0x92` ActivityID to its enum name, obtains string length and appends to local name. Thus the new stored string has content `EnumToString(ActivityFreeplay+0x34) + "_" + EnumToString(newID)` under normal nonnull enum-name inputs. |
| 3.21 | `0x005AD928: mov r5,r0`; `0x005AD92A: adds r1,r5,#1`; `0x005AD92C: add r0,sp,#0x1c`; `0x005AD92E: movs r2,#7`; `0x005AD930: ldrb r7,[r5]`; `0x005AD932: blx 0x004A42AC`; `0x005AD936: movs r1,#0`; `0x005AD938: ldr r6,[r5,#8]`; `0x005AD93A: strd r1,r1,[r5]`; `0x005AD940: str r1,[r5,#8]` | Creates a 12-byte *native libc++ string representation* in registers/local: first byte saved to `r7`, next seven bytes copied to `sp+0x1C`, last word saved in `r6`. Clears the temporary source string's three words, indicating ownership is being transferred. The plain string object's fields are **bytes 0..11**: short representation stores inline bytes and length tag; long representation uses the libc++ length/capacity/pointer encoding. |
| 3.22 | `0x005AD93E: mov r0,r4`; `0x005AD942: ldrb r2,[r0,#148]!`; `0x005AD946: tst.w r2,#1`; `0x005AD94A: bne 0x005AD950`; `0x005AD94C: strh r1,[r0]`; `0x005AD950: ldr.w r2,[r4,#0x9c]`; `0x005AD954: strb r1,[r2]`; `0x005AD956: str.w r1,[r4,#0x98]` | Checks old native string at **ActivityFreeplay+0x94** (`148` decimal is `0x94`): `tst.w firstByte,#1` is **the flag-setting instruction** for `bne` at `0x005AD94A`. Short case writes zero 16-bit prefix to the old string; long case zeroes first byte through the existing pointer at **ActivityFreeplay+0x9C** and clears `+0x98`. This does **not** yet free the old string; follow the next direct callee. |
| 3.23 | `0x005AD95A: movs r1,#0`; `0x005AD95E: bl 0x004E75EC`; `0x005AD962: add.w r0,r4,#0x95`; `0x005AD966: add r1,sp,#0x1c`; `0x005AD968: movs r2,#7`; `0x005AD96A: strb.w r7,[r4,#0x94]`; `0x005AD96E: blx 0x004A42AC`; `0x005AD972: str.w r6,[r4,#0x9c]` | Calls an unexported direct helper on old ActivityFreeplay string (target `0x004E75EC`, still **UNKNOWN** internally), then replaces the **12-byte native string ActivityFreeplay+0x94..0x9F** by writing first byte `+0x94`, next seven bytes `+0x95..+0x9B`, and final pointer/word `+0x9C..+0x9F`. This is a full type move, not simply a renamed scalar. |
| 3.24 | `0x005AD976: ldrb.w r0,[sp,#0x10]`; `0x005AD980: lsls r0,r0,#0x1f`; `0x005AD986: itt ne`; `0x005AD988: ldrne r0,[sp,#0x18]`; `0x005AD98A: blxne 0x004A40CC`; `0x005AD98E: ldrb.w r0,[sp]`; `0x005AD992: lsls r0,r0,#0x1f`; `0x005AD994: itt ne`; `0x005AD996: ldrne r0,[sp,#8]`; `0x005AD998: blxne 0x004A40CC` | Cleans both temporary `std::string` allocations if long-string flag was set. Each `IT NE` reads its respective **immediately preceding `lsls`**, not the other string's flag. The destination object's string remains owned by ActivityFreeplay. |
| 3.25 | `0x005AD99C: add sp,#0x24`; `0x005AD99E: pop {r4,r5,r6,r7,pc}`; `0x005AD9A4: mov r4,r0`; `0x005AD9C6: mov r0,r4`; `0x005AD9C8: blx 0x004A40A8` | Ordinary return is void. Exception landing pads at `0x005AD9A0–0x005AD9C8` clean temporary strings then `_Unwind_Resume`; exact throw-site coverage remains **UNKNOWN**. |

## Complete directly accessed layout relevant to item 3

| Owning object | Offset | Value/type | Native evidence |
|---|---|---|---|
| `ActivityFreeplay` | `+0x6C` | `unordered_map<UnlockId, vector<unique_ptr<IActivity>>>` | `0x005ADC4E`, `0x005ADC58` |
| Hash node mapped vector | `+0x0C/+0x10` | vector begin/end (third capacity pointer not read by this method; **UNKNOWN** here) | `0x005ADC5E` |
| Vector element | `+0` | `IActivity*` owned by unique_ptr | `0x005ADC6E`, `0x005ADCB2` |
| `ActivityFreeplay` | `+0x80` | current `IActivity*` | `0x005ADC62`, `0x005ADCB8`, `0x005ADE08` |
| `ActivityFreeplay` | `+0x90` | desired ActivityID from object availability | `0x005ADC72` |
| `ActivityFreeplay` | `+0x91` | requested ActivityID override | `0x005ADC6A` |
| `ActivityFreeplay` | `+0x92` | new subactivity's ActivityID | `0x005AD8CE` |
| `ActivityFreeplay` | `+0x94..+0x9F` | full 12-byte libc++ name string | `0x005AD942–0x005AD972` |
| `IActivity` candidate | `+0x20` | `IActivityStrategy*` | `0x005ADC8C` |
| `IActivity` candidate | `+0x34` | ActivityID | `0x005ADC78`, `0x005ADE0E` |
| `IActivity` candidate | `+0x54/+0x58` | two scalar/float arguments for strategy | `0x005ADC92` |
| `std::vector<unique_ptr<IActivity>>` | each `+0` | 32-bit `unique_ptr` raw pointer, four-byte stride | `0x005ADC80` |

### Selection rule, no omitted defaults

For requested `UnlockId U`: if `ActivityFreeplay+0x6C.find(U)` is missing, return 0, current child unchanged. Otherwise iterate the mapped vector **in its stored order**, selecting the *first* candidate satisfying:

- If `(ActivityFreeplay+0x91 != 0)`, candidate `IActivity+0x34 == ActivityFreeplay+0x91`.
- Else if `(ActivityFreeplay+0x90 != 0)`, candidate `IActivity+0x34 == ActivityFreeplay+0x90`.
- Else if `(candidate == ActivityFreeplay+0x80)`, require input bool exactly `1` **and** `WantsToEnd(Robot&,IActivity+0x54) == 0`.
- Else require `WantsToStart(Robot&,IActivity+0x58,IActivity+0x54) == 1`.

If no match, candidate is null. If selected pointer equals current pointer, return `0`. If different, call old child's `OnDeselected` if old nonnull, assign `ActivityFreeplay+0x80 = newPointer`, and if new nonnull write its ActivityID/name and call its `OnSelected`; return `1` even for a transition from nonnull to null.

### OPEN / UNKNOWN

- Exact body and allocator/exception behavior of direct string helpers `0x004E02B2`, `0x004E8048`, and `0x004E75EC`. These have been resolved as **direct call destinations**, not assumed to be pure operations.
- Full internal operations and messages of `IActivity::OnDeselected` and `OnSelected` are outside this function; the direct call sites and order are established.
- Exception table mapping for individual landing pads is incomplete.
- The precise field names for the candidate strategy's two float arguments `+0x54/+0x58` are not provable from this caller alone.

No implementation or status changes.



---

## Full extraction section 4

# Queue 4, item 4 — `IBehavior+0x1C` native writer/read audit

**Status: PARTIAL.** Direct base-constructor initialization and one direct derived-constructor override are demonstrated. A complete alias-aware writer census has **not** been proved, so no claim of exhaustive class coverage is made. No statuses or source files changed.

## Native evidence — one row per established step

| Step | Behavior and owning base | Exact `.so` instruction(s) | Outcome and conditional flags |
|---|---|---|---|
| 4.01 | `IBehavior` constructor receives `this` in `r0`, saves to `r4`. | `0x005BBB7A: mov r4, r0` | `r4` is the base `IBehavior*`. |
| 4.02 | `IBehavior` constructor establishes zero source register. | `0x005BBBEE: movs r2, #0x0` | `r2=0`; no intervening write to `r2` before 4.03 on the straight-line path. |
| 4.03 | Base-constructor initialization of **IBehavior+0x1C**. | `0x005BBC08: str r2, [r4, #0x1c]` | Process ID initialized to integer `0`. No branch at this store. |
| 4.04 | `BehaviorVisitInterestingEdge` constructor preserves the **same inherited `IBehavior` object** in `r4` and invokes base constructor. | `0x005E5F68: mov r4, r0`; `0x005E5F6A: blx 0x004AFF4C` (`IBehavior::IBehavior`). | `r4` continues to hold derived object's pointer, whose first subobject is `IBehavior`. |
| 4.05 | This derived constructor sets **inherited IBehavior+0x1C**. | `0x005E5FB2: movs r0, #0x1`; `0x005E5FB4: str r0, [r4, #0x1c]` | Writes `1` (overriding base constructor's `0`). No branch immediately governing the store on the normal constructor path. |
| 4.06 | Enum `StringFromEProcess` tests ID `1`. | `0x00574184: ldr r1, [pc, #0x14]` (literal at `0x0057419C` is `0x0067A68E`); `0x00574188: cmp r0, #0x1`; `0x0057418A: add r1, pc`; `0x0057418C: it eq`; `0x0057418E: moveq r2, r1` | `cmp r0,#1` sets Z=1 iff ID `1`; IT eq uses these flags. Thumb PC for `add` is `0x0057418E`, so resolved string address is `0x0057418E+0x0067A68E=0x00BEE81C`; native bytes decode to `CalculateInterestingRegions`. |
| 4.07 | Enum `StringFromEProcess` chooses returned name. | `0x00574192: cmp r0, #0x0`; `0x00574194: it ne`; `0x00574196: movne r1, r2`; `0x00574198: mov r0, r1`; `0x0057419A: bx lr` | For input `1`, returns `CalculateInterestingRegions`. `movne` uses flags of `cmp r0,#0`; for `0`, a separate initial `Invalid` literal is retained. |
| 4.08 | `IBehavior::IsRunnableBase` reads **IBehavior+0x1C**. | `0x005BD7D4: ldr r1, [r4, #0x1c]`; `0x005BD7D6: cbz r1, 0x005BD7E6` | `cbz` tests `r1` directly: if ID `0`, no process requirement is checked; no APSR flag producer is involved in `cbz`. |
| 4.09 | For nonzero process ID, fetches analyzer from the **Robot** (not from `IBehavior`). | `0x005BD7D8: ldr.w r0, [r5, #0x264]`; `0x005BD7DC: ldr r0, [r0, #0x8]`; `0x005BD7DE: blx 0x004B2940` (`AIInformationAnalyzer::IsProcessRunning`) | `Robot+0x264` is the component owner and owner `+0x08` the analyzer pointer; `r1` is the process ID obtained from `IBehavior+0x1C`. |
| 4.10 | Process-running gate after the call. | `0x005BD7E2: cmp r0, #0x0`; `0x005BD7E4: beq 0x005BD8CC` | Branches to report/diagnostic path when `IsProcessRunning(id)==0`, with APSR flags set by `cmp r0,#0`. **UNKNOWN** downstream full reporting effects; this is not assumed to return immediately. |
| 4.11 | Diagnostic path re-reads **IBehavior+0x1C** to get the process label. | `0x005BD8CC: movs r0, #0x0`; `0x005BD8D4: ldr r0, [r4, #0x1c]`; `0x005BD8D6: blx 0x004AD45C` (`StringFromEProcess`) | Confirms same base object and same process ID; direct call into native naming function. |
| 4.12 | Formats missing-process error, using the IBehavior name string and process label. | `0x005BD8DE: ldrb r1,[r0,#64]!`; `0x005BD8E2: tst.w r1,#0x1`; `0x005BD8E8: addeq r1,r0,#0x1`; `0x005BD8EA: ldrne r1,[r4,#0x48]`; `0x005BD8EC: ldr r0,[pc,#0xf0]`; `0x005BD8EE: add r0,pc`; `0x005BD8F6: blx 0x004A4108` (`sErrorF`) | `tst.w` sets Z based on first-byte bit0 in the short-string layout; `ITE eq` chooses inline payload vs pointer for **IBehavior+0x40** string. Literal `0x005BD9E0=0x00634EF2` and Thumb `PC=0x005BD8F2` resolve to `0x00BF27E4`, whose native bytes contain `IBehavior.IsRunnable.RequiredProcessNotFound`. |
| 4.13 | Releases temporary diagnostics array if allocated. | `0x005BD8FA: ldr r0,[sp,#4]`; `0x005BD8FC: cbz r0,0x005BD91C`; `0x005BD900: cmp r1,r0`; `0x005BD918: blx 0x004A40CC` (`operator delete`) | `cbz` tests allocation pointer directly; `cmp` determines `NE` cleanup length adjustment. |
| 4.14 | Sets diagnostic error flag and optionally triggers error break. | `0x005BD91C: ldr r0,[pc,#0xf4]`; `0x005BD91E: movs r2,#1`; `0x005BD920: ldr r1,[pc,#0xf4]`; `0x005BD922: add r0,pc`; `0x005BD924: add r1,pc`; `0x005BD926: ldr r0,[r0]`; `0x005BD928: ldr r1,[r1]`; `0x005BD92A: strb r2,[r0]`; `0x005BD92C: ldrb r0,[r1]`; `0x005BD92E: cbz r0,0x005BD962`; `0x005BD930: blx 0x004A4114` (`sDebugBreakOnError`) | `0x005BD91C` literal `0x00A80E6A` + PC at `0x005BD926` = `0x0103E790`; second literal `0x00A80E64` + PC at `0x005BD928` = `0x0103E78C`; both are GOT cells containing global pointers. First points to byte set `1`; second to the debug-break condition byte. `cbz` tests this byte directly. |
| 4.15 | Missing-process path joins common failure return. | `0x005BD934: b 0x005BD962`; `0x005BD962: movs r2,#0`; `0x005BD964: mov r0,r2`; `0x005BD968: pop {r4,r5,r6,pc}` | After reporting, returns `false` (0), whether debug-break was skipped or control returned from it. |

## Exact observed rule

In `IsRunnableBase`, this one gate permits normal continuation if `(IBehavior.processId == 0) || AIInformationAnalyzer.IsProcessRunning(IBehavior.processId)`. If process ID is nonzero and the analyzer reports zero, it branches to `0x005BD8CC`, logs `IBehavior.IsRunnable.RequiredProcessNotFound`, sets the error global, conditionally calls the debug-break helper, rejoins at `0x005BD962`, and returns `0`. This is **one gate only**, not a claim about the overall `IsRunnableBase` result.

The identified derived constructor sets process ID `1 = CalculateInterestingRegions`. The base constructor sets `0`.

## Writer search method, scope, exclusions

1. Disassembled the entire `.text` executable section at virtual `0x004D6860` onward using LLVM's Thumbv7 target, approximately **2.62 million disassembly lines**, and matched addresses to the ELF's exported symbols. A potential-store prefilter for literal `#0x1C` or `#28` yielded **3,441 candidate locations** across all types; this count includes stack spills, unrelated structures and non-`IBehavior` objects and is **not** a writer count.
2. Filtered `IBehavior` and `Behavior*` symbols, examined direct `str`/`str.w`/`strd` cases whose effective addresses could touch `+0x1C`, and searched `strd` at `+0x18` (would write second word at `+0x1C`), plus `add.w`/`sub.w` using `+0x1C` to detect a pointer later used by `str` or `stm`. These are candidate searches, not a complete interprocedural data-flow proof.
3. Excluded stores to `[sp,#0x1c]` (stack), fields of `BehaviorManager`, `IHelper`, `CarryingComponent`, `Robot`, independent allocated subobjects, and local structures. The same offset does not imply the same owning object. Examples: `0x005E2292: str r0,[r7,#0x1c]` writes a configuration structure built at `BehaviorExploreLookAroundInPlace::LoadConfig`, **not** inherited `IBehavior+0x1C`; `0x005A08F6` writes `BehaviorManager+0x1C`, also unrelated.
4. The directly established `IBehavior`-relative stores in this audit are `0x005BBC08` and `0x005E5FB4`. **UNKNOWN:** other writes mediated by opaque pointer aliases, inlined routines not named with `Behavior`, bulk copying, or dynamic addressing. No proof of their absence.

## Outstanding work required to mark Done

- Whole-program alias/points-to checking of every potential writer including `stm` and calculated-pointer stores, plus constructors that write through an inherited `IBehavior` subobject via aliased registers. The candidate scan alone cannot prove the exact global writer set.
- Confirm whether the parent `IBehavior` can be mutated through a function pointer, generic container or external interface after construction.

No C# changes, no status changes, no invented callers or classifications.



---

## Full extraction section 5

# Queue 4, item 5 — StrategyGeneric

**Status: PARTIAL.** Native gate logic, tag-handler paths, and direct writers of `StrategyGeneric+0x80` are extracted below. The concrete configurations that supply function closures, every nested event payload field, and any indirect/alias writers are not established.

## Address-backed steps

| Step | Native evidence (instructions at instruction boundaries) | Exact effect / condition with owning object |
|---|---|---|
| 5.01 | `0x00613748: push {r7,lr}`; `0x0061374A: blx 0x004B7548` | Constructor calls `IWantsToRunStrategy::IWantsToRunStrategy(Robot&, Json::Value const&)` first. `this` pointer remains in return `r0`. |
| 5.02 | `0x00613252: str r1,[r4,#4]`; `0x00613254: movs r1,#0`; `0x0061325A: strd r1,r1,[r4,#12]`; `0x00613264: str r1,[r5,#8]!`; `0x0061326A: blx 0x004B753C`; `0x0061326E: strb r0,[r4,#0x14]` | Parent `IWantsToRunStrategy`: stores `Robot*` at `+0x04`, initializes handle-vector storage `+0x08..+0x13`, parses and saves `WantsToRunStrategyType` at `+0x14`. `r4` is parent base, not a tag-node pointer. |
| 5.03 | `0x006131AC: adr r2,#140`; `0x006131B2: blx 0x004ACEA4`; `0x006131C6: blx 0x004B6F9C` | `IWantsToRunStrategy::ExtractStrategyType` uses **JSON key** at code literal `0x0061323C`, native ASCII `strategyType`, as **`ParseString` key argument `r2`**, then translates the parsed string to enum. The other `adr` at `0x0061319A` constructs a **default** string, not the JSON key. |
| 5.04 | `0x00613750: movs r2,#0`; `0x00613754: str r2,[r0,#0x20]`; `0x00613758: str r2,[r0,#0x38]`; `0x0061375A: str r2,[r3,#28]!`; `0x00613760: str r3,[r0,#0x18]` | Derived `StrategyGeneric` initializes first `std::set<MessageEngineToGameTag>` tree storage at strategy `+0x18..+0x24`, with null root at `+0x20`. The stored `r3` after `str ...!` is a pointer into its own set. |
| 5.05 | `0x00613764: str r2,[r0,#0x48]`; `0x00613768: str r2,[r3,#68]!`; `0x0061376C: str r3,[r0,#0x40]`; `0x0061376E: str r2,[r0,#0x60]` | Initializes second `std::set<MessageGameToEngineTag>` at `StrategyGeneric+0x40..+0x4C` (null root `+0x48`), and its associated `std::function` control at `+0x60` to null. |
| 5.06 | `0x00613770: str r2,[r0,#0x78]`; `0x00613772: strb.w r2,[r0,#0x80]` | Initializes Robot-only predicate function control pointer `+0x78` to null and one-byte event-trigger flag `StrategyGeneric+0x80` to `0`. |
| 5.07 | `0x00613786: ldr r0,[r5,#0x20]`; `0x0061378A: cbz r0,0x0061379C` | `ConfigureRelevantEvents(MessageEngineToGameTag set,predicate)` checks existing strategy-owned outgoing tag set root. `cbz` uses `r0` directly; if nonzero it executes a verify-failure path and returns (`0x0061378C–0x0061379A`). |
| 5.08 | `0x0061379C: ldr r0,[r6,#8]`; `0x0061379E: cbz r0,0x006137C6`; `0x006137A0: add.w r0,r5,#0x18`; `0x006137A4: cmp r0,r6`; `0x006137AE: blxne 0x004B7194` | For nonempty **input set**, compares its address against strategy-owned set. `cmp` sets NE for conditional assignment from input (tree node `+0x04` is not `StrategyGeneric+0x04`); otherwise empty-set input goes to verify-failure at `0x006137C6`. |
| 5.09 | `0x006137B2: mov r0,r5`; `0x006137B4: mov r1,r6`; `0x006137B6: blx 0x004B7560`; `0x006137BA: add.w r0,r5,#0x28`; `0x006137C0: blx 0x004B756C` | Subscribes strategy to **MessageEngineToGame** tags and copies associated `std::function<bool(event,Robot)>` into `StrategyGeneric+0x28..+0x3F` (control present at `+0x38`). |
| 5.10 | `0x0061386A: ldr r0,[r5,#0x48]`; `0x0061386E: cbz r0,0x00613880`; `0x00613880: ldr r0,[r6,#8]`; `0x00613882: cbz r0,0x006138AA` | Second `ConfigureRelevantEvents` overload checks strategy's **MessageGameToEngine** tag-set root, then input set's root. Nonempty prior configuration or empty input takes verification path. `cbz` directly tests loaded pointers, no APSR producer. |
| 5.11 | `0x00613888: cmp r0,r6`; `0x00613892: blxne 0x004B7584`; `0x0061389A: blx 0x004B7590`; `0x0061389E: add.w r0,r5,#0x50`; `0x006138A4: blx 0x004B759C` | Conditionally copies incoming set, subscribes to **MessageGameToEngine** tags, and copies associated `std::function` into `StrategyGeneric+0x50..+0x67` (control pointer `+0x60`). `blxne` uses flags from `cmp r0,r6` at `0x00613888`. |
| 5.12 | `0x0061399E: ldrh r0,[r1,#0x10]`; `0x006139A2: ldr r6,[r3,#28]!`; `0x006139A6: cbz r6,0x006139EE` | Outgoing `AlwaysHandleInternal` takes **AnkiEvent<MessageEngineToGame>+0x10** tag as 16-bit key. Looks at strategy's first tag-tree root. Empty tree goes to error-report tail; `cbz` tests root pointer, not flags. |
| 5.13 | `0x006139AC: ldrh r6,[r4,#0x0e]`; `0x006139AE: cmp r6,r0`; `0x006139B0: bhs 0x006139BA`; `0x006139B2: ldr r4,[r4,#4]`; `0x006139B4: cmp r4,#0` | Tree search compares **set-node tag at node+0x0E** with event tag `r0` and follows child pointers. `bhs` uses unsigned `cmp nodeTag,eventTag`; `bne` at `0x006139B6` uses `cmp child,#0`. This is not a `StrategyGeneric+0x0E` field. |
| 5.14 | `0x006139C6: cmp r4,r3`; `0x006139C8: beq 0x006139EE`; `0x006139CA: ldrh r6,[r4,#0x0e]`; `0x006139CC: cmp r0,r6`; `0x006139CE: blo 0x006139EE` | Validates candidate tree-node match or enters no-tag report path. `beq` uses `cmp candidate,sentinel`; `blo` uses unsigned `cmp eventTag,nodeTag`. |
| 5.15 | `0x006139D4: ldr r0,[r7,#0x38]`; `0x006139D6: cmp r0,#0`; `0x006139D8: beq 0x006139E6`; `0x006139DA: add.w r0,r7,#0x28`; `0x006139DE: blx 0x004B75C0`; `0x006139E2: cmp r0,#1`; `0x006139E4: bne 0x00613A44` | If outgoing tag found, absent predicate accepts automatically; otherwise event predicate must return **exactly `1`**. Each `beq/bne` uses its immediately cited `cmp`. |
| 5.16 | `0x006139E6: movs r0,#1`; `0x006139E8: strb.w r0,[r7,#0x80]` | Outgoing matching event (and passing predicate, if supplied) sets **StrategyGeneric+0x80 = 1**. |
| 5.17 | `0x006139EE: movs r1,#0`; `0x006139F6: blx 0x004A6CD0`; `0x00613A06: blx 0x004A4108`; `0x00613A2C: ldr r0,[pc,#0x48]`; `0x00613A3C: strb r2,[r1]`; `0x00613A3E: cbz r0,0x00613A44`; `0x00613A40: blx 0x004A4114` | Outgoing unregistered tag: converts tag to text, calls `sErrorF`, releases temporary logging data if allocated, sets diagnostic error global, may invoke debug-break helper, **then rejoins** at `0x00613A44` without setting the trigger flag. `cbz` tests the loaded debug-break flag directly. |
| 5.18 | `0x00613AC2: ldrh r0,[r1,#0x10]`; `0x00613AC6: ldr r6,[r3,#68]!`; `0x00613ACA: cbz r6,0x00613B12` | Incoming `AlwaysHandleInternal` reads **AnkiEvent<MessageGameToEngine>+0x10** tag and second tree root `StrategyGeneric+0x44`; empty tree routes to error path. |
| 5.19 | `0x00613AD0: ldrh r6,[r4,#0x0e]`; `0x00613AD2: cmp r6,r0`; `0x00613AD4: bhs 0x00613ADE`; `0x00613AEE: ldrh r6,[r4,#0x0e]`; `0x00613AF0: cmp r0,r6`; `0x00613AF2: blo 0x00613B12` | Incoming-tag tree search uses same unsigned node-key comparator and sentinels. Conditions explicitly derive from the shown `cmp`s. |
| 5.20 | `0x00613AF8: ldr r0,[r7,#0x60]`; `0x00613AFA: cmp r0,#0`; `0x00613AFC: beq 0x00613B0A`; `0x00613AFE: add.w r0,r7,#0x50`; `0x00613B02: blx 0x004B75CC`; `0x00613B06: cmp r0,#1`; `0x00613B08: bne 0x00613B68` | Incoming event's optional callback gate: absent callback passes, else only return **`1`** passes. |
| 5.21 | `0x00613B0A: movs r0,#1`; `0x00613B0C: strb.w r0,[r7,#0x80]` | Incoming matching event sets **StrategyGeneric+0x80 = 1**. |
| 5.22 | `0x00613B12: movs r1,#0`; `0x00613B1A: blx 0x004A8CEC`; `0x00613B2A: blx 0x004A4108`; `0x00613B60: strb r2,[r1]`; `0x00613B62: cbz r0,0x00613B68`; `0x00613B64: blx 0x004A4114`; `0x00613B68: add sp,#0xC` | Incoming unregistered tag logs error, clears diagnostic temporary, sets error global, optionally debug-breaks, then exits. Does not set event flag. |
| 5.23 | `0x0061394E: ldr r0,[r4,#0x20]`; `0x00613950: ldrb.w r5,[r4,#0x80]`; `0x00613954: cmp r0,#0`; `0x0061395A: bne 0x00613972` | `WantsToRunInternal` reads first configured tag-tree root and the pending flag. If outgoing tags configured, skips generic Robot predicate. `bne` uses `cmp r0,#0`. |
| 5.24 | `0x0061395C: ldr r0,[r4,#0x48]`; `0x0061395E: cbz r0,0x00613964`; `0x00613964: ldr r0,[r4,#0x78]`; `0x00613966: cmp r0,#0`; `0x00613968: beq 0x00613960`; `0x0061396A: add.w r0,r4,#0x68`; `0x0061396E: blx 0x004B75B4` | Only when **both** tag sets are empty and Robot-only predicate function is configured, calls `std::function<bool(Robot const&)>` at `StrategyGeneric+0x68`. `cbz` tests second root directly; `beq` uses `cmp functionPointer,#0`. |
| 5.25 | `0x0061394C: movs r6,#0`; `0x00613972: strb.w r6,[r4,#0x80]`; `0x00613976: cmp r5,#0`; `0x0061397A: movne r5,#1`; `0x0061397C: orrs r0,r5`; `0x0061397E: pop {r4,r5,r6,pc}` | Consumes event-trigger flag **on every call** by setting `+0x80=0`, normalizes previous event flag to boolean via `cmp r5,#0`, ORs with Robot-only predicate result (or zero), returns resulting integer. |
| 5.26 | `0x00613980: ldr r2,[r0,#0x20]`; `0x00613982: cbnz r2,0x00613988`; `0x00613984: ldr r2,[r0,#0x48]`; `0x00613986: cbz r2,0x0061398C`; `0x00613988: movs r0,#0` | `ShouldTrigger` rejects if either tag set nonempty; `cbnz`/`cbz` test the corresponding tree roots directly. |
| 5.27 | `0x0061398C: ldr r2,[r0,#0x78]`; `0x0061398E: cmp r2,#0`; `0x00613990: beq 0x00613988`; `0x00613992: adds r0,#0x68`; `0x00613994: b.w 0x008CC9FC` | With both sets empty and Robot predicate configured, tail-calls its `std::function::operator()`; the Thumb-to-ARM veneer at `0x008CC9FC` (`bx pc`) and ARM `0x008CCA00: ldr r12,[pc]`; `0x008CCA04: add pc,r12,pc`; literal at `0x008CCA08=0xFFBEABA8` resolves target **`0x004B75B4`**. No unexplained indirect destination remains for this call. |

## Complete directly observed `StrategyGeneric` storage zones

| Owner and range | Native object/type, evidence | Notes |
|---|---|---|
| `IWantsToRunStrategy+0x00` | Inherited vptr; base constructor `0x00613262` | Derived strategy overwrites at `0x00613776`. |
| `IWantsToRunStrategy+0x04` | `Robot*`; `0x00613252` | Not a strategy event node. |
| `IWantsToRunStrategy+0x08..+0x13` | Vector of subscription-handle shared pointers; constructor `0x0061325A–0x00613264` | Underlying elements' complete layout not extracted. |
| `IWantsToRunStrategy+0x14` | `WantsToRunStrategyType` byte; `0x0061326E` | Derived from JSON key `strategyType`. |
| `StrategyGeneric+0x18..+0x27` | Outgoing event-tag `std::set` storage; ctor `0x0061375A–0x00613760` | Root loaded at `+0x20`; nodes have tag at **node+0x0E**, child pointers at node offsets `+0x00,+0x04`. |
| `StrategyGeneric+0x28..+0x3F` | Outgoing predicate `std::function<bool(AnkiEvent<MessageEngineToGame> const&,Robot const&)>`; `0x006137BA–0x006137C0` | Function-control pointer read at `+0x38`. |
| `StrategyGeneric+0x40..+0x4F` | Incoming event-tag `std::set` storage; ctor `0x00613768–0x0061376C` | Root at `+0x48`. |
| `StrategyGeneric+0x50..+0x67` | Incoming predicate `std::function<bool(AnkiEvent<MessageGameToEngine> const&,Robot const&)>`; `0x0061389E–0x006138A4` | Function-control pointer at `+0x60`. |
| `StrategyGeneric+0x68..+0x7F` | Robot-only predicate `std::function<bool(Robot const&)>`; `0x0061396A`, `0x0061398C–0x00613994` | Function-control pointer at `+0x78`; this extraction has not identified **who installs it**. |
| `StrategyGeneric+0x80` | Pending one-byte event trigger; ctor `0x00613772` | Direct writers: ctor `0x00613772=0`; outgoing `0x006139E8=1`; incoming `0x00613B0C=1`; consumer `0x00613972=0`. |

## Plain rules

- `AlwaysHandleInternal` for either message direction: **set pending flag to 1 if and only if event's 16-bit tag matches a configured tag and `(predicate absent || predicate(event,robot)==1)`**. Unconfigured tags enter the error path; present tag but false predicate does not set it.
- `WantsToRunInternal`: **return `(pendingFlagBeforeCall != 0) || (outgoingTagsEmpty && incomingTagsEmpty && robotPredicateConfigured && robotPredicate(robot))`; clear pending flag to `0` before returning.**
- `ShouldTrigger`: **return `robotPredicate(robot)` only when both message-tag sets are empty and Robot predicate is configured; otherwise return `0`.**
- `ConfigureRelevantEvents`: a nonempty supplied set is required and the corresponding strategy set must not be configured already. Successful call subscribes to the given tags and copies that direction's predicate. Where a verify-failure helper is used, its internal side effects remain untraced.

## Explicit UNKNOWNs preventing Done

- All fields (beyond the 16-bit tag at `AnkiEvent+0x10`) of both concrete event templates and their nested message bodies. Their type names are established; their complete layouts are not.
- The installers, captures, event field reads and behavior of each concrete configured outgoing/incoming predicate, and the Robot-only predicate at `+0x68`.
- Any additional JSON keys used by callers to assemble these predicates (the native `StrategyGeneric` constructor itself parses none beyond inherited `strategyType`).
- Indirect/alias writers of `+0x80` beyond the four direct native stores found by the full-text `strb` address scan.
- The complete tree-node allocator ownership/destructor paths and exception cleanup semantics of `ConfigureRelevantEvents`.

No project code, status or fidelity-manifest changes.



---

## Full extraction section 6

# Queue 4, item 6 — StrategyExpressNeedsTransition / StrategyInNeedsBracket

**Status: PARTIAL** under the requested complete-object, complete-caller standard. The native configuration keys and direct runnable gates are established; nested needs-state objects and any enum-invalid behavior outside these functions remain untraced.

## Address-backed steps

| Step | Native instruction quotation | Behavior, condition, and owning base object |
|---|---|---|
| 6.01 | `0x00613604: mov r5,r2`; `0x00613606: mov r4,r0`; `0x00613608: blx 0x004B7548` | `StrategyExpressNeedsTransition` preserves its own `this` and incoming JSON then initializes the inherited `IWantsToRunStrategy` first. |
| 6.02 | `0x00613628: add r0,sp,#0xc`; `0x0061362A: adr r2,#160`; `0x0061362E: mov r1,r5`; `0x00613630: blx 0x004ACEA4` | `ParseString` receives JSON at `r1`, **key pointer at `r2`**. Thumb ADR at `0x0061362A` aligns PC to `0x0061362C` and adds `160`, yielding literal `0x006136CC` = ASCII `need`. The default string built at `0x0061361E` is not the key. |
| 6.03 | `0x00613642: add r0,sp,#0xc`; `0x00613644: blx 0x004B1EB4`; `0x00613648: str r0,[r4,#0x18]` | Converts configured `need` string with `NeedIdFromString` and stores **StrategyExpressNeedsTransition+0x18** (32-bit NeedId). |
| 6.04 | `0x006136DC: ldr.w r0,[r1,#0x264]`; `0x006136E0: ldr r1,[r1]`; `0x006136E2: ldr r5,[r0,#0x30]`; `0x006136E4: ldr r0,[r1,#0x34]`; `0x006136E6: blx 0x004A5740` | `StrategyExpressNeedsTransition::WantsToRunInternal` obtains NeedsManager from `RobotImpl+0x34` and a **separate owner** through `Robot+0x264` then that component `+0x30`; its pointer is saved `r5`. No cross-object offset alias is assumed. |
| 6.05 | `0x006136EA: ldr r1,[r4,#0x18]`; `0x006136EC: movs r2,#3`; `0x006136EE: blx 0x004AD2C4`; `0x006136F2: mov r1,r0`; `0x006136F4: movs r0,#0`; `0x006136F6: cmp r1,#1`; `0x006136FA: popne {r4,r5,r7,pc}` | Tests `NeedsState::IsNeedAtBracket(StrategyExpressNeedsTransition.need, **3**)`. `popne` uses flags set by `cmp result,#1`: returns **0** unless the method returns exactly `1`. |
| 6.06 | `0x006136FC: ldr r1,[r4,#0x18]`; `0x006136FE: ldr r2,[r5,#0x14]`; `0x00613700: cmp r2,r1`; `0x00613704: movne r0,#1`; `0x00613706: pop {r4,r5,r7,pc}` | On bracket match, compares `+0x14` of the separate object at `Robot+0x264→+0x30` against configured NeedId. `movne` uses `cmp`; returns **1** iff different, otherwise **0**. Exact semantic label of that object's `+0x14`: **UNKNOWN**. |
| 6.07 | `0x0061370C: ldr r0,[r1]`; `0x0061370E: ldr r0,[r0,#0x34]`; `0x00613710: blx 0x004A5740`; `0x00613714: ldr r1,[r4,#0x18]`; `0x00613716: movs r2,#3`; `0x0061371C: b.w 0x008CC9EC` | `StrategyExpressNeedsTransition::InRequiredNeedBracket` calls same `NeedsState::IsNeedAtBracket(NeedId,3)`. Thumb-to-ARM veneer at `0x008CC9EC: bx pc`; ARM `0x008CC9F0: ldr r12,[pc]`; `0x008CC9F4: add pc,r12,pc`; literal `0x008CC9F8=0xFFBE08C8`; target `0x004AD2C4` (verified). |
| 6.08 | `0x00614052: mov r4,r0`; `0x00614054: blx 0x004B7548`; `0x00614074: add r0,sp,#0x10`; `0x00614076: adr r2,#224`; `0x0061407C: blx 0x004ACEA4` | `StrategyInNeedsBracket` initializes same inherited strategy then parses JSON **key** `need`. ADR `0x00614076` uses aligned PC `0x00614078` +224 = `0x00614158`, bytes `need`. |
| 6.09 | `0x0061408E: add r0,sp,#0x10`; `0x00614090: blx 0x004B1EB4`; `0x00614094: str r0,[r4,#0x18]` | Stores `NeedIdFromString` result in **StrategyInNeedsBracket+0x18**. Same numeric offset as ExpressNeedsTransition belongs to a *different subclass instance*. |
| 6.10 | `0x006140B6: adr r2,#216`; `0x006140BE: blx 0x004ACEA4`; `0x006140D0: mov r0,sp`; `0x006140D2: blx 0x004B5328`; `0x006140D6: str r0,[r4,#0x1c]` | Parses second JSON **key** `needBracket` (ADR `0x006140B6`, aligned PC `0x006140B8`+216=`0x00614190`, ASCII `needBracket`). Converts via `NeedBracketIdFromString`; stores **StrategyInNeedsBracket+0x1C**. **This `+0x1C` is not `IBehavior+0x1C`** from item 4. |
| 6.11 | `0x006141A4: ldr r0,[r1]`; `0x006141A6: ldr r0,[r0,#0x34]`; `0x006141A8: blx 0x004A5740`; `0x006141AC: ldrd r1,r2,[r4,#24]`; `0x006141B4: b.w 0x008CC9EC` | `StrategyInNeedsBracket::WantsToRunInternal` obtains NeedsState, loads its own configured **NeedId and NeedBracketId** from `+0x18/+0x1C`, and tail-calls `NeedsState::IsNeedAtBracket`. Verified veneer target `0x004AD2C4`. |
| 6.12 | `0x006141BC: ldr r0,[r1]`; `0x006141BE: ldr r0,[r0,#0x34]`; `0x006141C0: blx 0x004A5740`; `0x006141C4: ldrd r1,r2,[r4,#24]`; `0x006141CC: b.w 0x008CC9EC` | `StrategyInNeedsBracket::InRequiredNeedBracket` uses the **same** configured pair and tail-call. No additional local guard. |

## Precise rules

- `StrategyExpressNeedsTransition::WantsToRunInternal` returns **1 iff** `NeedsState.IsNeedAtBracket(configuredNeed, 3) == 1 && (otherComponentAtRobotPlus0x264_plus0x30.field0x14 != configuredNeed)`. It returns **0** otherwise. This uses the comparison `0x006136F6: cmp r1,#1` and `0x00613700: cmp r2,r1`, not speculative labels for the second object.
- `StrategyExpressNeedsTransition::InRequiredNeedBracket` directly returns `NeedsState.IsNeedAtBracket(configuredNeed,3)`.
- `StrategyInNeedsBracket::WantsToRunInternal` and `::InRequiredNeedBracket` both directly return `NeedsState.IsNeedAtBracket(configuredNeed,configuredBracket)`.
- JSON keys established by actual `ParseString` key argument are `strategyType` (inherited), `need` (both), `needBracket` (InNeedsBracket only). The unqualified strings built for default arguments do **not** count as keys.

## Explicit UNKNOWNs

- Full nested type and semantics of the pointer from `Robot+0x264`, offset `+0x30`, whose own `+0x14` is compared for ExpressNeedsTransition.
- `NeedsState::IsNeedAtBracket` internal implementation and any side effects, beyond this call and its return: not extracted in this item.
- Failure/exception behavior of `ParseString`, enum-conversion invalid strings, and heap cleanup through the two constructors' landing pads, except the shown normal cleanup.
- Other callers/derived uses of these strategy fields and complete `IWantsToRunStrategy` subscription-handle element type.

No source, code, or fidelity-status changes.



---

## Full extraction section 7

# Queue 4, item 7 — BehaviorLookForFaceAndCube::LoadConfig and InitInternal

**Status: PARTIAL.** All 16 directly parsed config keys and the four nominated field writes have verified native addresses; opaque constructors and action execution downstream remain untraced. Every table offset below is from **BehaviorLookForFaceAndCube**, never the JSON parser, local stack, or its action objects.

## LoadConfig: all 16 JSON keys

Each literal is the **key argument** to the named `Anki::JsonTools::Parse*`, not a default or logger string. The first six and four cube angle fields use `0x005EF958: vldr s16,[pc,#792]` (literal `0x005EFC74=0x3C8EFA35`, `0.01745329238f`) and `vmul.f32 s0,s0,s16` before `Radians::operator=`; this is true even where the JSON key ends in `_rad`.

| Step | Owning field and parse type | Key | Exact instruction citations / literal computation |
|---|---|---|---|
| 7.01 | `BehaviorLookForFaceAndCube+0x11C` `float→Radians` | `bodyTurnSpeed_degPerSec` | `0x005EF94A: adr r1, #784`; Thumb aligned PC `0x005EF94C` + `784` = key `0x005EFC5C`; `0x005EF950: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EF964: add.w	r0, r4, #0x11c`. |
| 7.02 | `BehaviorLookForFaceAndCube+0x124` `float→Radians` | `headTurnSpeed_degPerSec` | `0x005EF96C: adr r1, #776`; Thumb aligned PC `0x005EF970` + `776` = key `0x005EFC78`; `0x005EF972: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EF982: add.w	r0, r4, #0x124`. |
| 7.03 | `BehaviorLookForFaceAndCube+0x12C` `float→Radians` | `face_headAngleAbsRangeMin_deg` | `0x005EF98A: adr r1, #772`; Thumb aligned PC `0x005EF98C` + `772` = key `0x005EFC90`; `0x005EF990: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EF9A0: add.w	r0, r4, #0x12c`. |
| 7.04 | `BehaviorLookForFaceAndCube+0x134` `float→Radians` | `face_headAngleAbsRangeMax_deg` | `0x005EF9AA: add r1, pc`; 32-bit literal at `0x005EFCB0` plus Thumb PC `0x005EF9AE` = key `0x00BF55F9`; `0x005EF9B0: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EF9C0: add.w	r0, r4, #0x134`. |
| 7.05 | `BehaviorLookForFaceAndCube+0x13C` `float→Radians` | `face_bodyAngleRelRangeMin_deg` | `0x005EF9CA: add r1, pc`; 32-bit literal at `0x005EFCB4` plus Thumb PC `0x005EF9CE` = key `0x00BF5617`; `0x005EF9D0: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EF9E0: add.w	r0, r4, #0x13c`. |
| 7.06 | `BehaviorLookForFaceAndCube+0x144` `float→Radians` | `face_bodyAngleRelRangeMax_deg` | `0x005EF9EA: add r1, pc`; 32-bit literal at `0x005EFCB8` plus Thumb PC `0x005EF9EE` = key `0x00BF5635`; `0x005EF9F0: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EFA00: add.w	r0, r4, #0x144`. |
| 7.07 | `BehaviorLookForFaceAndCube+0x14C` `uint8` | `face_sidePicks` | `0x005EFA08: adr r1, #688`; Thumb aligned PC `0x005EFA0C` + `688` = key `0x005EFCBC`; `0x005EFA0E: blx	0x4acebc <.plt+0x8eac>`; `0x005EFA14: strb.w	r0, [r4, #0x14c]`. |
| 7.08 | `BehaviorLookForFaceAndCube+0x150` `float→Radians` | `cube_headAngleAbsRangeMin_rad` | `0x005EFA18: add r1, pc`; 32-bit literal at `0x005EFCCC` plus Thumb PC `0x005EFA1C` = key `0x00BF5653`; `0x005EFA1E: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EFA2E: add.w	r0, r4, #0x150`. |
| 7.09 | `BehaviorLookForFaceAndCube+0x158` `float→Radians` | `cube_headAngleAbsRangeMax_rad` | `0x005EFA38: add r1, pc`; 32-bit literal at `0x005EFCD0` plus Thumb PC `0x005EFA3C` = key `0x00BF5671`; `0x005EFA3E: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EFA4E: add.w	r0, r4, #0x158`. |
| 7.10 | `BehaviorLookForFaceAndCube+0x160` `float→Radians` | `cube_bodyAngleRelRangeMin_rad` | `0x005EFA58: add r1, pc`; 32-bit literal at `0x005EFCD4` plus Thumb PC `0x005EFA5C` = key `0x00BF568F`; `0x005EFA5E: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EFA6E: add.w	r0, r4, #0x160`. |
| 7.11 | `BehaviorLookForFaceAndCube+0x168` `float→Radians` | `cube_bodyAngleRelRangeMax_rad` | `0x005EFA78: add r1, pc`; 32-bit literal at `0x005EFCD8` plus Thumb PC `0x005EFA7C` = key `0x00BF56AD`; `0x005EFA7E: blx	0x4b1bf0 <.plt+0xdbe0>`; `0x005EFA8E: add.w	r0, r4, #0x168`. |
| 7.12 | `BehaviorLookForFaceAndCube+0x170` `uint8` | `cube_sidePicks` | `0x005EFA98: add r1, pc`; 32-bit literal at `0x005EFCDC` plus Thumb PC `0x005EFA9C` = key `0x00BF56CB`; `0x005EFA9E: blx	0x4acebc <.plt+0x8eac>`; `0x005EFAA4: strb.w	r0, [r4, #0x170]`. |
| 7.13 | `BehaviorLookForFaceAndCube+0x14D` `bool` | `verifySeenFaces` | `0x005EFAA8: add r1, pc`; 32-bit literal at `0x005EFCE0` plus Thumb PC `0x005EFAAC` = key `0x00BF56DA`; `0x005EFAAE: blx	0x4b2ee0 <.plt+0xeed0>`; `0x005EFAB4: strb.w	r0, [r4, #0x14d]`. |
| 7.14 | `BehaviorLookForFaceAndCube+0x178` `bool` | `stopBehaviorOnAnyFace` | `0x005EFAB8: add r1, pc`; 32-bit literal at `0x005EFCE4` plus Thumb PC `0x005EFABC` = key `0x00BF56EA`; `0x005EFABE: blx	0x4b2ee0 <.plt+0xeed0>`; `0x005EFAC4: strb.w	r0, [r4, #0x178]`. |
| 7.15 | `BehaviorLookForFaceAndCube+0x179` `bool` | `stopBehaviorOnNamedFace` | `0x005EFAC8: add r1, pc`; 32-bit literal at `0x005EFCE8` plus Thumb PC `0x005EFACC` = key `0x00BF5700`; `0x005EFACE: blx	0x4b2ee0 <.plt+0xeed0>`; `0x005EFAD4: strb.w	r0, [r4, #0x179]`. |
| 7.16 | `BehaviorLookForFaceAndCube+0x174` `string→AnimationTrigger` | `lookInPlaceAnimTrigger` | `0x005EFAD8: add r2, pc`; 32-bit literal at `0x005EFCEC` plus Thumb PC `0x005EFADC` = key `0x00BF4CC4`; `0x005EFAE2: blx	0x4acea4 <.plt+0x8e94>`; `0x005EFB1E: str.w	r0, [r4, #0x174]`. |

The `+0x174` string parse has separate outcomes: `0x005EFAEC: ands r0,r2,#1`, `0x005EFAF2: lsreq r1,r2,#1` (short-string length); `0x005EFAF4: cbz r1,0x005EFB3E` branches directly on length. Empty string: `0x005EFB3E: movw r0,#0x23f`; `0x005EFB42: str.w r0,[r4,#0x174]`. Nonempty string: `0x005EFB1A: blx 0x004A9BF8` (`AnimationTriggerFromString`), `0x005EFB1E: str.w r0,[r4,#0x174]`. At `0x005EFB34: movw r1,#0x23f`; `0x005EFB38: cmp r0,r1`; `0x005EFB3A: beq 0x005EFB46` tests that same `0x23F` value: invalid value goes through an error report at `0x005EFB74: blx 0x004A4108`, sets an error flag at `0x005EFBAA: strb r2,[r1]`, and rejoins at `0x005EFBB2`. The error/cleanup path is not a normal result change. All temporary strings are released and the function returns at `0x005EFBD6`.

## InitInternal: ordered native operations

| Step | Instructions | Effect and owning base |
|---|---|---|
| 7.17 | `0x005EFD60: mov r4,r0`; `0x005EFD62: mov r8,r1`; `0x005EFD68: adr r2,#424`; `0x005EFD6C: blx 0x004A7318` | Preserves **BehaviorLookForFaceAndCube*** and `Robot&`, constructs a temporary behavior-name string for info logging. |
| 7.18 | `0x005EFD80: tst.w r2,#1`; `0x005EFD88: orreq r1,r5,#1`; `0x005EFD90: blx 0x004A505C` | Chooses inline/heap string representation for a logged name. `orreq` flags set by `tst.w r2,#1`. No behavior gate. |
| 7.19 | `0x005EFDC4: mov r0,r8`; `0x005EFDC6: bl 0x004EA398`; `0x005EFDD0: bl 0x004DF628`; `0x005EFDD6: blx 0x004A4048`; `0x005EFDDE: blx 0x004A4150`; `0x005EFDEA: blx 0x004A4564` | Derives a rotation around Z from a Robot pose and writes a `Radians` object at **BehaviorLookForFaceAndCube+0x17C**. The first two local helper targets are unexported; full pose production chain **UNKNOWN**. |
| 7.20 | `0x005EFDF4: ldr.w r1,[r4,#0x190]`; `0x005EFDF8: add.w r0,r4,#0x18c`; `0x005EFDFE: str.w r5,[r4,#0x188]`; `0x005EFE02: strb.w r5,[r4,#0x184]`; `0x005EFE06: blx 0x004A5CE0` | Clears two tracking fields (`+0x188` word, `+0x184` byte) and releases prior `std::tree<int>` nodes using the **tree root** previously stored at behavior `+0x190`. `r5` was set to `0` at `0x005EFDFC: movs r5,#0`. |
| 7.21 | `0x005EFE0A: add.w r0,r4,#0x190`; `0x005EFE0E: strd r0,r5,[r4,#396]`; `0x005EFE14: str.w r5,[r4,#0x194]`; `0x005EFE18: strb.w r5,[r4,#0x198]` | Resets the **BehaviorLookForFaceAndCube** tree at `+0x18C` (root pointer `+0x190`, count `+0x194`) and sets **BehaviorLookForFaceAndCube+0x198 = 0**. The store at `+0x198` is unequivocally part of `InitInternal`, **not** a parsed config field. |
| 7.22 | `0x005EFE12: movs r0,#0x9c`; `0x005EFE1C: blx 0x004A42A0`; `0x005EFE24: blx 0x004A9178` | Allocates `0x9C` bytes and constructs `CompoundActionParallel(Robot&)`. |
| 7.23 | `0x005EFE28: movs r0,#0xa4`; `0x005EFE2A: blx 0x004A42A0`; `0x005EFE30: movs r3,#0`; `0x005EFE34: movt r3,#0x40a0`; `0x005EFE38: movs r2,#0`; `0x005EFE3E: blx 0x004A9A48` | Allocates `0xA4` bytes and constructs `MoveLiftToHeightAction` with **Robot**, preset `0`, float `5.0` (`r3=0x40A00000`). |
| 7.24 | `0x005EFE42: ldr r0,[r6]`; `0x005EFE4A: ldr r7,[r0,#0x20]`; `0x005EFE52: blx r7` | Adds lift action to parallel compound using its virtual slot `+0x20` with flags zero; exact concrete vtable target remains **UNKNOWN**. |
| 7.25 | `0x005EFE5E: movs r1,#0`; `0x005EFE66: blx 0x004A4294`; `0x005EFE6C: movs r1,#0`; `0x005EFE70: blx 0x004A4294` | Initializes two temporary zero `Radians` values. |
| 7.26 | `0x005EFE74: add.w r2,r4,#0x12c`; `0x005EFE78: add.w r0,r4,#0x11c`; `0x005EFE7C: add.w r1,r4,#0x134`; `0x005EFE82: strd r9,r2,[sp]`; `0x005EFE88: strd r1,r0,[sp,#8]`; `0x005EFE92: blx 0x004B549C` | Builds body/head turn action using the constructor's own `Radians` fields, the newly computed `+0x17C`, and the two zero-angle temporaries. All address arguments are from **BehaviorLookForFaceAndCube**, not `Robot`. |
| 7.27 | `0x005EFE98: ldr r0,[r6]`; `0x005EFE9E: ldr r7,[r0,#0x20]`; `0x005EFEA6: blx r7` | Adds the turn action to same compound via virtual `+0x20`; owning object `CompoundActionParallel`. |
| 7.28 | `0x005EFEB0: ldr r0,[pc,#0x98]`; `0x005EFEB8: ldr r2,[r0]`; `0x005EFEBA: mov r0,r4`; `0x005EFEBC: blx 0x004B54A8` | Calls `IBehavior::StartActing<BehaviorLookForFaceAndCube>` with the compound action and a callback pointer obtained from a GOT-derived global. The exact callback body remains **UNKNOWN**. |
| 7.29 | `0x005EFEC0: movs r0,#0`; `0x005EFEC4: pop.w {r4,r5,r6,r7,r8,r9,r10,pc}` | Normal return value `0`. Exception landing pads at `0x005EFEC8–0x005EFF10` destroy temporaries, then `_Unwind_Resume`, not regular success branches. |

## Direct answer for four nominated offsets

- `BehaviorLookForFaceAndCube+0x14D` is assigned the `ParseBool` result for actual JSON key **`verifySeenFaces`** (`0x005EFAAE` → `0x005EFAB4`).
- `+0x178` is assigned the result of key **`stopBehaviorOnAnyFace`** (`0x005EFABE` → `0x005EFAC4`).
- `+0x179` is assigned the result of key **`stopBehaviorOnNamedFace`** (`0x005EFACE` → `0x005EFAD4`).
- `+0x198` is cleared to **0** in `InitInternal`, at `0x005EFE18`, sourced from `0x005EFDFC: movs r5,#0`.

## Explicit UNKNOWNs

- Full semantics/side effects of the two unexported Robot-pose helpers, concrete compound-action virtual methods, and the callback pointer passed to `StartActing`.
- Errors and exact fallback values inside external `Parse*` implementations; this report only establishes the keys and stored return values.
- Full implicit `Radians`, action, and tree-node field layouts beyond the directly accessed words.
- The downstream effects after `StartActing` scheduling; no completion callback was traced here.

No implementation or status changes.



---

## Full extraction section 8

# Queue 4, item 8 — AnimationStreamer::RemoveIdleAnimation and static global `0x0105AE60`

**Status: DONE for the two requested questions.** The routine's error-handling side effects and every path's return are included. Native source: ARMv7/Thumb `libcozmoEngine.so`, disassembled at actual instruction boundaries. The global data location is a **virtual** BSS address (not a file offset); the source literal is in the file-backed `.rodata`.

## Call boundary, parameter, and object layouts

`0x005B3406: add.w r0,r4,#0x60`; `0x005B340A: mov r1,r7`; `0x005B340C: blx 0x004A92A4` calls `AnimationStreamer::RemoveIdleAnimation(std::string const&)`, with **Robot+0x60** the embedded `AnimationStreamer` object and `r7` a temporary string built by `0x005B33F4–0x005B3402` from the activity's virtual name. After return, `0x005B3410: ldrb.w r0,[sp,#0x28]`, `0x005B3414: lsls r0,r0,#0x1f`, and the conditional `0x005B341A: blxne 0x004A40CC` release heap-backed temporary storage; the return value of `RemoveIdleAnimation` is not inspected by this caller.

**AnimationStreamer object fields directly accessed:** `+0x48` idle-stack vector begin, `+0x4C` vector end; `+0x34` streaming-animation-related pointer/flag, `+0x38` state, `+0x40` animation parameter, `+0x64` byte status. The exact complete semantic types of `+0x34/+0x38/+0x40/+0x64` are **UNKNOWN**. **Each 16-byte idle entry** has a 32-bit `AnimationTrigger` at `+0x00` and a 12-byte libc++ `std::string` at `+0x04` (short string length/flag and inline bytes or pointer/size/capacity). These per-entry offsets are not offsets from `AnimationStreamer`.

## Native steps and condition provenance

| Step | Native instruction quotation, owning object, condition and effect |
|---|---|
| 8.01 | `0x0057BA66: mov r9,r0`; `0x0057BA68: mov r4,r1`; `0x0057BA6A: ldrd r7,r5,[r9,#72]`. `r9` is the **AnimationStreamer**; `r7`/`r5` are its idle-vector begin/end, `r4` is requested `std::string const&`. |
| 8.02 | `0x0057BA6E: subs r0,r5,r7`; `0x0057BA70: cmp r0,#0x10`; `0x0057BA72: bne 0x0057BACE`. If the vector contains **exactly one 16-byte entry**, immediately takes special warning path (8.03), regardless of whether the name matches; otherwise proceeds to empty/multi-entry path. `bne` flags come from `cmp` against 16. |
| 8.03 | `0x0057BA7C: ldr r0,[r5,#-16]`; `0x0057BA80: blx 0x004AAE4C` (`EnumToString(AnimationTrigger)`); `0x0057BA94: tst.w r7,#1`; `0x0057BA9A: addeq r1,r4,#1`; `0x0057BAA2: blx 0x004A4540` (`sWarningF`), then temporary-vector cleanup (`0x0057BAA6: ldr r0,[sp,#0x18]`; `0x0057BAA8: cmp r0,#0`; `0x0057BACC: b 0x0057BC14`). At `0x0057BC14: movs r2,#1`; `0x0057BC16: mov r0,r2`; `0x0057BC18: add sp,#0x24`: **returns 1, leaves idle vector unchanged**. `addeq` is controlled by the `tst.w` on the requested string flag, not by any name match. |
| 8.04 | `0x0057BACE: cmp r7,r5`; `0x0057BAD0: beq 0x0057BBC4` when vector begin equals end, i.e. empty. This gate's flags come from the equality `cmp`. |
| 8.05 | Empty-vector path: `0x0057BBC4: ldr r0,[pc,#0x2ac]`; `0x0057BBC8: ldr r2,[pc,#0x2ac]`; `0x0057BBD6: blx 0x004A4108` (`sErrorF`), temporary cleanup, then `0x0057BC0A: ldrb r0,[r0]`; `0x0057BC0C: strb r2,[r1]` (sets **global logging-error flag** to 1; `r2` set to 1 at `0x0057BBFE`); `0x0057BC0E: cbz r0,0x0057BC16` checks **another global debug-break setting**, otherwise `0x0057BC10: blx 0x004A4114` (`sDebugBreakOnError`). `0x0057BC14: movs r2,#1`; `0x0057BC16: mov r0,r2`: **returns 1, no idle entry removed**. Global names behind those two indirect pointers are not established in this scope. |
| 8.06 | Multi-entry scan initializes `0x0057BADA: mov r10,r5` (**candidate = idle-vector end**, i.e. no match), `0x0057BAEA: movs r0,#1`; `0x0057BAEC: mov r8,r0` (iteration counter 1). `0x0057BAEE: cmp r5,r7`; `0x0057BAF0: beq 0x0057BB9E` tests current iterator against end. The `beq` flags are from `cmp r5,r7`. |
| 8.07 | For each 16-byte **entry**, `0x0057BAF4: ldrb r2,[r1,#4]!`, `0x0057BAF8: ldr r0,[r1,#4]`, `0x0057BAFA: ands r3,r2,#1`, `0x0057BB00: lsreq r0,r2,#1`, `0x0057BB02: cmp r0,r6`, `0x0057BB04: bne 0x0057BB3E`: compare entry string length against request string length. `r6` was loaded from caller string's `+0x04`/short-string length at `0x0057BAD2–0x0057BAE8`. `bne` flags from `cmp r0,r6`. |
| 8.08 | Equal lengths compare contents. `0x0057BB0A: cmp r3,#0`; `0x0057BB10: bne 0x0057BB30` chooses heap vs inline by the **entry string flag** from `ands r3,r2,#1` at `0x0057BAFA`. Inline path `0x0057BB12: cbz r6,0x0057BB3C`; otherwise byte loop `0x0057BB1E: cmp r4,r3`; `0x0057BB20: bne 0x0057BB3E`, with iteration `0x0057BB22: adds r0,#1` and `0x0057BB2C: bne 0x0057BB1A`. Heap path `0x0057BB30: cbz r6,0x0057BB3C`; `0x0057BB36: blx 0x004A5728` (`memcmp`); `0x0057BB3A: cbnz r0,0x0057BB3E`. The `cmp`/`adds` set the specified flags; `cbz`/`cbnz` directly test their registers. |
| 8.09 | Matching entry: `0x0057BB3C: mov r10,r7` sets candidate to **current entry**. Scan continues: `0x0057BB3E: add.w r0,r8,#1`; `0x0057BB42: adds r7,#0x10`; `0x0057BB44: cmp.w r8,#0x3e8`; `0x0057BB48: blo 0x0057BAEC` (loops while the previous counter is below **1000**, flags from `cmp`; the next counter is installed in `r8` by `0x0057BAEC` via the loop setup). Since scan does not break at a match, the **last encountered matching entry** is retained. Beyond 1000 iterations, it enters `sErrorF` path at `0x0057BB60`; no longer scans. |
| 8.10 | After scanning or hitting iteration limit, `0x0057BB9E: ldr.w r0,[r9,#0x4c]`; `0x0057BBA2: cmp r0,r10`; `0x0057BBA4: beq 0x0057BC1E`. When end equals candidate, **requested lock/name was not found**; `beq` flags from this `cmp`. |
| 8.11 | **No match (multi-entry):** `0x0057BC1E: movs r0,#0`; `0x0057BC28: ldrb r1,[r0]` (from saved requested string pointer); `0x0057BC2E: tst.w r1,#1`; `0x0057BC38: moveq r3,r1` selects inline requested-string pointer when its flag bit is zero; `0x0057BC3E: blx 0x004A4540` (`sWarningF`, including requested name). `0x0057BC42: b 0x0057BAA6` follows the normal temporary cleanup, then `0x0057BC14: movs r2,#1`; `0x0057BC16: mov r0,r2`: **returns 1; idle vector and streamer state unchanged**. |
| 8.12 | **Match found:** `0x0057BBAE: mov r1,r10` selects matching **entry**; `0x0057BBB4: tst.w r2,#1`; `0x0057BBB8: bne 0x0057BC44` handles the entry string's allocation representation; when inline `0x0057BBBC: ldm.w r1,{r3,r6,r7}`; `0x0057BBC0: stm r2!,{r3,r6,r7}` copies 12-byte string representation. `0x0057BC44: ldrd r2,r1,[r10,#8]`; `0x0057BC4A: bl 0x004E02B2` for heap representation, before joining common removal. |
| 8.13 | Compares removal candidate to last vector entry: `0x0057BC52: add.w r1,r10,#0x10`; `0x0057BC56: cmp r1,r0`; `0x0057BC58: beq 0x0057BCA4`. For a non-last candidate, shifts following 16-byte entries left (`0x0057BC64: ldr r1,[r5,#0x10]`; `0x0057BC6A: str r1,[r5]`; `0x0057BC88: ldm.w r0,{r1,r2,r3}`; `0x0057BC8C: stm r6!,{r1,r2,r3}`), zeroing the moved-from source string, and comparing shifted entry pointers at `0x0057BC96: cmp r7,r5`; `0x0057BC98: bne 0x0057BC62`. |
| 8.14 | Shrinks the **AnimationStreamer idle vector** `0x0057BCAA: str.w r1,[r9,#0x4c]`; destroys former last entry, frees heap string when `0x0057BCB2: lsls r2,r2,#0x1f` results nonzero (`0x0057BCB4: beq ...`; `0x0057BCBA: blx 0x004A40CC`), loops cleanup until `0x0057BCC2: cmp r1,r5`; `0x0057BCC6: bne 0x0057BCA6`. Branch flags come from named `lsls` and `cmp`. |
| 8.15 | Tests if removed stack's top animation has `AnimationTrigger==0x23F`: `0x0057BD36: ldr.w r0,[r9,#0x4c]`; `0x0057BD3A: movw r1,#0x23f`; `0x0057BD3E: ldr r0,[r0,#-16]`; `0x0057BD42: cmp r0,r1`; `0x0057BD44: bne 0x0057BD70`. If equal, checks `AnimationStreamer+0x34` non-null (`0x0057BD46: ldr.w r0,[r9,#0x34]`; `0x0057BD4A: cbz r0,...`) and `AnimationStreamer+0x38==0` (`0x0057BD4C: ldr.w r0,[r9,#0x38]`; `0x0057BD50: cbnz r0,...`). If both, `0x0057BD60: blx 0x004AADE0` calls `SetStreamingAnimation` with `r1=AnimationStreamer+0x40`, `r2=1,r3=1,[sp]=0`. Always clears `AnimationStreamer+0x64` byte and `+0x34` word at `0x0057BD66: strb.w r2,[r9,#0x64]` and `0x0057BD6A: str.w r2,[r9,#0x34]` (r2=0 at `0x0057BD64`). `0x0057BD70: movs r2,#0` marks normal successful removal; `0x0057BC16: mov r0,r2`: **returns 0**. |

**Plain outcomes:** A one-entry idle stack refuses removal, warns, returns **1**; an empty stack errors, may set an error indicator/debug break, returns **1**; a multi-entry stack with no matching name warns, keeps stack unchanged, returns **1**; a multi-entry match removes the last matching entry among those scanned (bounded by 1000), may change current-streaming state as in 8.15, returns **0**. When the bound is reached it additionally reports an error before the match/no-match handling; the binary gives no guarantee all entries were scanned. The no-match case does **not** delete a different entry or silently clear the idle stack.

## Global string `0x0105AE60`: exact initializer and value

`0x0105AE60` is a **virtual BSS address** in the second LOAD segment. Directly reading file bytes there is incorrect. Its initializing function is the Thumb routine at **`0x004D7590`**, referenced by `.init_array` pointer **`0x0103E400`** (file offset `0x0103D400`, containing `0x004D7591` Thumb-bit pointer).

| Step | Exact evidence |
|---|---|
| 8.16 | `0x004D7592: ldr r4,[pc,#0x30]` reads word at `0x004D75C4` = `0x00B838C2`; `0x004D759A: add r4,pc` uses Thumb PC `0x004D759E`, obtaining `0x0105AE60`. This is the BSS `std::string` base **not** a file offset. |
| 8.17 | `0x004D7594: movs r5,#0`; `0x004D7598: movs r0,#0x12`; `0x004D75A0: strd r5,r5,[r4]`; `0x004D75A4: str r5,[r4,#8]`; `0x004D75A6: strb r0,[r4]` builds a 12-byte short `std::string` object (short length 9 represented as `0x12`). |
| 8.18 | `0x004D7596: ldr r1,[pc,#0x30]` reads literal at `0x004D75C8` = `0x0077272E`; `0x004D759E: add r1,pc` uses Thumb PC `0x004D75A2`, computing `0x00C49CD0`, whose first nine bytes are ASCII `Activity_`. `0x004D759C: movs r2,#9`; `0x004D75A8: adds r0,r4,#1`; `0x004D75AA: blx 0x004A42AC` (`__aeabi_memcpy`) copies those nine bytes into short-string inline storage. |
| 8.19 | `0x004D75B6: strb r5,[r4,#0xA]` writes a NUL terminator. `0x004D75AE: ldr r0,[pc,#0x1c]`; `0x004D75B4: add r0,pc`; `0x004D75B8: add r2,pc`; `0x004D75C0: b.w 0x008CADDC` transfers to an ARM veneer for the static destructor registration; destructor details beyond this scope **UNKNOWN**. |
| 8.20 | This global is read elsewhere: `0x005B32EE: ldr r1,[pc,#0xc4]` (literal `0x005B33B4`); `0x005B32F6: add r1,pc` computes `0x0105AE60`, as does `0x005B37D6: ldr r1,[pc,...]` followed by `0x005B37DC: add r1,pc`. These are in `IActivity::SmartPushIdleAnimation` and `SmartRemoveIdleAnimation`, respectively. |

**Exact global value: `Activity_`** (9 ASCII characters). This prefix is a **global short libc++ string**, not a JSON key or logging-only literal.

## Explicit limitations

- Some internal error-level globals are dereferenced through relocations; this report establishes their stores but does not assign a stronger symbolic name than the native instructions support.
- The complete downstream behavior of `SetStreamingAnimation` is outside the two requested questions. The relevant branch and call arguments are extracted.
- The native routine assumes valid vector layout and name-string invariants. This report does not infer behavior for malformed or undefined inputs.

No C# code, fidelity manifest, or project status was changed.

