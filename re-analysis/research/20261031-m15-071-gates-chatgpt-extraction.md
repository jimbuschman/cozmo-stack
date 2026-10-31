# M15-071 — NeedsManager reward gates and configuration selection

**Native source:** uploaded `libcozmoEngine.so` (ARMv7/Thumb). **Scope:** A1–A3 only. M12-049 B4/B5 intentionally not traced in this pass. **Method:** disassemble native `.text`, resolve native ELF PLT relocations, inspect native literals and shipped JSON. Native instructions below are the behavioral evidence; the existing manifest, prior research, symbol names and supplied config filenames are used only as navigation/identification aids. No C# code, inventory, or fidelity status changed.

`EXACT_SOURCE` on an individual row means *that row's cited native step*, not a promotion of the existing subsystem/record.

## A1. `NeedsManager::ShouldRewardSparksForFreeplay` (`0x00696D74`)

| Step | Native behavior / field | Native instructions (quoted) | Classification |
|---|---|---|---|
| A1.1 | Save manager pointer, pass inventory enum/type **1** to its inventory component (`manager+0x04` -> object `+0x444`) | `0x00696D7C: mov r4,r0`; `0x00696D7E: movs r1,#1`; `0x00696D80: ldr r0,[r4,#4]`; `0x00696D82: ldr.w r0,[r0,#0x444]`; `0x00696D86: blx 0x004BDC5C` (`InventoryComponent::GetInventoryAmount`) | EXACT_SOURCE |
| A1.2 | Preserve inventory amount as signed int and current star level (`manager+0x54`); read current `StarRewardsConfig*` (`manager+0x1A8`) | `0x00696D8A: ldr r6,[r4,#0x54]`; `0x00696D8C: mov r5,r0`; `0x00696D8E: ldr.w r0,[r4,#0x1a8]`; `0x00696D92: mov r1,r6`; `0x00696D94: blx 0x004BDC68` | EXACT_SOURCE |
| A1.3 | Level index is clamped to last element; element stride is **0x40** bytes | `0x0068E7A4: ldrd r2,r0,[r0]`; `0x0068E7AC: subs r0,r0,r2`; `0x0068E7AE: asrs r3,r0,#6`; `0x0068E7B0: cmp r3,r1`; `0x0068E7B4: addls.w r1,r12,r0,asr #6`; `0x0068E7B8: add.w r0,r2,r1,lsl #6`; `0x0068E7BC: bx lr`. Negative/out-of-range behavior beyond these instructions not assumed. | EXACT_SOURCE |
| A1.4 | Read **`UnlockLevel+0x28`** (`freeplayTargetSparksTotal`, int), cast inventory and target to f32, calculate `inventory / target` | `0x00696D98: vldr s0,[r0,#40]`; `0x00696D9C: vmov s2,r5`; `0x00696DA6: vcvt.f32.s32 s2,s2`; `0x00696DAA: vcvt.f32.s32 s0,s0`; `0x00696DB6: vdiv.f32 s0,s2,s0` | EXACT_SOURCE |
| A1.5 | Compute `1.0f - ratio` as initial chance | `0x00696DBA: vmov.f32 s2,#1.0`; `0x00696DBE: vsub.f32 s16,s2,s0` | EXACT_SOURCE |
| A1.6 | Re-fetch same level, read **`UnlockLevel+0x2C`** (`freeplayMinSparksRewardPct`, float), and choose larger of this and computed chance | `0x00696DC2: blx 0x004BDC68`; `0x00696DC6: vldr s0,[r0,#44]`; `0x00696DCE: vcmpe.f32 s16,s0`; `0x00696DD2: vmrs APSR_nzcv,fpscr`; `0x00696DD6: it mi`; `0x00696DD8: vmovmi.f32 s16,s0` | EXACT_SOURCE |
| A1.7 | Use manager context (`manager+0` then `+0x14`) as `RandomGenerator*`, draw `RandDbl(1.0)` | `0x00696DA0: vmov.f64 d2,#1.0`; `0x00696DB2: vmov r5,r7,d2`; `0x00696DCA: mov r2,r5`; `0x00696DCC: mov r3,r7`; `0x00696DDC: ldr r0,[r4]`; `0x00696DDE: vcvt.f64.f32 d8,s16`; `0x00696DE2: ldr r0,[r0,#0x14]`; `0x00696DE4: blx 0x004A60AC` | EXACT_SOURCE |
| A1.8 | Return **1 iff** resulting random double compares strictly less than threshold, otherwise **0**; there is no read of pending-reward `manager+0x3D8` in this function | `0x00696DE8: vmov d0,r0,r1`; `0x00696DEC: movs r0,#0`; `0x00696DEE: vcmpe.f64 d0,d8`; `0x00696DF2: vmrs APSR_nzcv,fpscr`; `0x00696DF6: it mi`; `0x00696DF8: movmi r0,#1`; `0x00696E00: pop {r4,r5,r6,r7,pc}` | EXACT_SOURCE |

For ordinary positive finite target values, this is a random gate with threshold `max(1 − inventoryAmount / freeplayTargetSparksTotal, freeplayMinSparksRewardPct)`; **no explicit clamp to [0,1]** occurs in this function. UNKNOWN: behavior guarantees for zero target, NaN and malformed/empty level arrays (not established here). The source fact is the actual VFP instruction order, not an assumption about input validity.

### Full 0x40-byte `UnlockLevel` layout (not just the two accessed fields)

The 0x40 stride is proven by `0x0068E7B8: add.w r0,r2,r1,lsl #6` and `0x0068E6E4: adds r0,#0x40`. The JSON setters use `isMember` (`blx 0x004A5080`) before `operator[]` (`blx 0x004A5F2C`); the cited conversion/storage instructions give the actual member layout:

| Offset | Field / type | Setter instruction evidence |
|---|---|---|
| `+0x00,+0x04,+0x08` | `rewards` vector begin/end/capacity; element `NeedsReward` is 0x14 bytes | `0x007869E0: blx 0x004A5080`; `0x007869EE: blx 0x004A5F2C`; `0x00786A1C: blx 0x004C6674`; `0x00786A28: adds r4,#0x14` |
| `+0x0C` | `numStarsToUnlock` int | `0x00786A46: blx 0x004AA6E4`; `0x00786A4A: str.w r0,[r8,#0xc]` |
| `+0x10` | `targetSparksTotal` int | `0x00786A66: blx 0x004AA6E4`; `0x00786A6A: str.w r0,[r8,#0x10]` |
| `+0x14` | `maxPriorLevelUnlocks` int | `0x00786A86: blx 0x004AA6E4`; `0x00786A8A: str.w r0,[r8,#0x14]` |
| `+0x18` | `minSparksPct` float | `0x00786AA6: blx 0x004A7264`; `0x00786AAA: str.w r0,[r8,#0x18]` |
| `+0x1C` | `maxSparksPct` float | `0x00786AC6: blx 0x004A7264`; `0x00786ACA: str.w r0,[r8,#0x1c]` |
| `+0x20` | `minSparks` int | `0x00786AE6: blx 0x004AA6E4`; `0x00786AEA: str.w r0,[r8,#0x20]` |
| `+0x24` | `minMaxSparks` int | `0x00786B06: blx 0x004AA6E4`; `0x00786B0A: str.w r0,[r8,#0x24]` |
| `+0x28` | `freeplayTargetSparksTotal` int | `0x00786B26: blx 0x004AA6E4`; `0x00786B2A: str.w r0,[r8,#0x28]` |
| `+0x2C` | `freeplayMinSparksRewardPct` float | `0x00786B46: blx 0x004A7264`; `0x00786B4A: str.w r0,[r8,#0x2c]` |
| `+0x30` | `freeplayMinSparksPct` float | `0x00786B66: blx 0x004A7264`; `0x00786B6A: str.w r0,[r8,#0x30]` |
| `+0x34` | `freeplayMaxSparksPct` float | `0x00786B86: blx 0x004A7264`; `0x00786B8A: str.w r0,[r8,#0x34]` |
| `+0x38` | `freeplayMinSparks` int | `0x00786BA6: blx 0x004AA6E4`; `0x00786BAA: str.w r0,[r8,#0x38]` |
| `+0x3C` | `freeplayMinMaxSparks` int | `0x00786BC6: blx 0x004AA6E4`; `0x00786BCA: str.w r0,[r8,#0x3c]` |

Nested `NeedsReward` layout, complete: `+0x00` 32-bit rewardType (`0x007845F8: str r0,[r4]`), `+0x04..+0x0F` 12-byte std::string data (`0x0078464C: ldm.w r6,{r0,r1,r2}`; `0x00784650: stm r7!,{r0,r1,r2}`), `+0x10` inventoryIsFull bool (`0x0078466E: strb r0,[r4,#0x10]`), `+0x11..+0x13` alignment (element stride `0x00786A28: adds r4,#0x14`). This is the full 20-byte element; it will recur in the StarLevelCompleted message.

## A2. `NeedsManager::UpdateStarsState(bool)` (`0x00696AAC`)

| Step | Native behavior / fields read/written | Native instructions (quoted) | Classification |
|---|---|---|---|
| A2.1 | Preserve `this`, bool argument; get/create map entry for need key `2` in manager map `+0x84` | `0x00696AB2: mov r5,r0`; `0x00696AB4: movs r0,#2`; `0x00696ABC: mov r6,r1`; `0x00696AC8: add.w r1,r5,#0x84`; `0x00696ACE: blx 0x004BDB48`. May allocate/insert an entry if missing (std::map bracket operator). | EXACT_SOURCE |
| A2.2 | Read map entry value, node+0x14. If value zero, require bool exactly 1 or return0 | `0x00696AD2: ldr r0,[sp,#0x6c]`; `0x00696AD4: ldr r0,[r0,#0x14]`; `0x00696AD6: cbz r0,0x00696AEA`; `0x00696AEA: cmp r6,#1`; `0x00696AEC: bne 0x00696AE6`; `0x00696AE6: movs r6,#0` | EXACT_SOURCE |
| A2.3 | When stored bracket value nonzero, request `NeedsState::GetNeedBracketByIndex(2)` using manager+8; if returned value nonzero and bool is **false**, return0; else proceed | `0x00696AD8: add.w r0,r5,#8`; `0x00696ADC: movs r1,#2`; `0x00696ADE: blx 0x004BDA4C`; `0x00696AE2: cbz r0,0x00696AEE`; `0x00696AE4: cbnz r6,0x00696AEE`; `0x00696AE8: b 0x00696C7A` | EXACT_SOURCE |
| A2.4 | `GetNeedBracketByIndex` reads NeedsState+0x64 then calls another helper (side effects **UNKNOWN**), and looks up index in NeedsState map +0x70, returning node value +0x14 | `0x0069CB18: ldr r0,[r5,#0x64]`; `0x0069CB1A: add.w r1,r0,#0x18`; `0x0069CB20: blx 0x004BDA34`; `0x0069CB36: add.w r1,r5,#0x70`; `0x0069CB3C: blx 0x004BDB48`; `0x0069CB42: ldr r0,[r0,#0x14]` | Partial; helper effects UNKNOWN |
| A2.5 | Convert previous stored time point manager+0x60 and current `system_clock::now()` to localtime via `to_time_t` / `localtime_r` | `0x00696AEE: add.w r6,r5,#0x60`; `0x00696AF4: blx 0x004A7354`; `0x00696AFE: blx 0x004BD4AC`; `0x00696B06: blx 0x004A7348`; `0x00696B0C: blx 0x004A7354`; `0x00696B16: blx 0x004BD4AC` | EXACT_SOURCE |
| A2.6 | Compare old/new local calendar `tm_yday`, `tm_year`. Same day: no star-transition writes, set result0 | `0x00696B5A: ldr r1,[sp,#0x88]`; `0x00696B5C: ldr r2,[sp,#0x4c]`; `0x00696B5E: cmp r2,r1`; `0x00696B62: ldr r0,[sp,#0x80]`; `0x00696B64: ldr r3,[sp,#0x44]`; `0x00696B66: cmp r3,r0`; `0x00696B68: bne 0x00696B72`; `0x00696B6E: movs r6,#0` | EXACT_SOURCE |
| A2.7 | Date differs: overwrite **manager+0x60..+0x67** with 8-byte current clock time point, increment **manager+0x58** | `0x00696BBA: ldrd r0,r1,[sp,#96]`; `0x00696BBE: strd r0,r1,[r6]`; `0x00696BC2: ldr r0,[r5,#0x58]`; `0x00696BC4: adds r0,#1`; `0x00696BC6: str r0,[r5,#0x58]` | EXACT_SOURCE |
| A2.8 | Emit **StarUnlocked** to game through external interface (see full payload below) | `0x00696BC8: mov r0,r5`; `0x00696BCA: blx 0x004BDC44`; in callee `0x006986E8: ldm r3,{r1,r2,r3}`; `0x006986FC: blx 0x004BDD04`; `0x00698704: blx r6` | EXACT_SOURCE |
| A2.9 | If incremented manager+0x58 >= manager+0x5C, call `SendStarLevelCompletedToGame` (see full payload/effects) | `0x00696BCE: ldrd r0,r1,[r5,#88]`; `0x00696BD2: cmp r0,r1`; `0x00696BD4: itt ge`; `0x00696BD8: blxge 0x004BDC50` | EXACT_SOURCE |
| A2.10 | Request state write to robot with `true` | `0x00696BDC: mov r0,r5`; `0x00696BDE: movs r1,#1`; `0x00696BE2: blx 0x004BDBF0` (`PossiblyStartWriteToRobot(bool)`); internal message format/fields not traced here => UNKNOWN | Partial / UNKNOWN downstream |
| A2.11 | Invoke local notifications generator via manager+0x1B0 | `0x00696BE6: ldr.w r0,[r5,#0x1b0]`; `0x00696BEA: blx 0x004BD3D4` (`LocalNotifications::Generate`); messages/side effects downstream UNKNOWN | Partial / UNKNOWN downstream |
| A2.12 | Emit diagnostics/events on either same-day or transition path; result0 same-day, 1 date transition | `0x00696BEE: ldr r1,[r7]`; `0x00696C46: blx 0x004A50E0` (`sEvent`); `0x00696BE0: movs r6,#1`; `0x00696C7A: mov r0,r6`; `0x00696C7E: pop {r4,r5,r6,r7,pc}`. `sEvent` is diagnostics, **not** a game broadcast. | EXACT_SOURCE |

### A2.13. Full `StarUnlocked` game message

Its **entire payload** is three 32-bit values in this order, as shown by the nontrivial reordering in its native caller:

| Payload offset | Source | Exact instruction proof |
|---|---|---|
| `+0x00` | manager `+0x54` | `0x006986DA: add.w r3,r0,#0x54`; `0x006986E8: ldm r3,{r1,r2,r3}`; `0x006986EA: strd r1,r3,[sp,#4]` |
| `+0x04` | manager `+0x5C` | `0x006986E8: ldm r3,{r1,r2,r3}`; `0x006986EA: strd r1,r3,[sp,#4]` (second word is r3) |
| `+0x08` | manager `+0x58` | `0x006986E8: ldm r3,{r1,r2,r3}`; `0x006986F0: str r2,[sp,#0xc]` |

`0x00726624: ldm.w r1,{r3,r12,lr}`; `0x00726628: add.w r2,r0,#8`; `0x0072662C: movs r1,#0x67`; `0x0072662E: stm.w r2,{r3,r12,lr}`; `0x00726632: strh r1,[r0]`: outer `MessageEngineToGame` **tag 103**, payload exactly 12 bytes. `0x006986F6: ldr r0,[r4]`; `0x006986F8: ldr r6,[r0,#0x1c]`; `0x00698704: blx r6` sends it through external-interface vtable +0x1C. No nested fields.

### A2.14. Full `StarLevelCompleted` game message and downstream state writes

| Step | Native behavior | Native instructions | Classification |
|---|---|---|---|
| A2.14a | Read current `UnlockLevel+0x14` `maxPriorLevelUnlocks` through manager+0x1A8, index manager+0x54. | `0x00698758: ldr r1,[r4,#0x54]`; `0x0069875A: ldr.w r0,[r4,#0x1a8]`; `0x0069875E: blx 0x004BDC68`; `0x00698762: ldr r0,[r0,#0x14]` | EXACT_SOURCE |
| A2.14b | For eligible prior levels, call `ProcessLevelRewards(index, vector&, true, int*)`, increment index, loop subject to level/max-prior budget. | `0x00698774: movs r5,#0`; `0x00698776: mov r0,r4`; `0x00698778: mov r1,r5`; `0x0069877A: mov r2,r6`; `0x0069877C: movs r3,#1`; `0x00698780: blx 0x004BDD10`; `0x00698786: adds r5,#1`; `0x00698788: cmp r5,r1`; `0x0069878C: ldr r0,[sp,#0x20]`; `0x00698790: bge 0x00698776` | EXACT_SOURCE; detailed reward-branch side effects UNKNOWN |
| A2.14c | Call `ProcessLevelRewards` again with current-level/default argument values. | `0x00698792: movs r0,#0`; `0x00698796: str r0,[sp]`; `0x0069879A: movs r3,#0`; `0x0069879C: blx 0x004BDD10` | EXACT_SOURCE; detailed reward-branch side effects UNKNOWN |
| A2.14d | Set NeedsState star level to manager+0x54 +1, writing NeedsState fields. | `0x006987A0: ldr r1,[r4,#0x54]`; `0x006987A2: add.w r0,r4,#8`; `0x006987A6: adds r1,#1`; `0x006987A8: blx 0x004BDD1C`; callee `0x0069CBBC: movs r2,#0`; `0x0069CBC0: strd r1,r2,[r4,#76]` (**NeedsState+0x4C** = new level, **NeedsState+0x50** = 0); `0x0069CBC4: blx 0x004BDDD0` (`GetMaxStarsForLevel`), `0x0069CBC8: str r0,[r4,#0x54]` (**NeedsState+0x54** max-stars). | EXACT_SOURCE |
| A2.14e | Build payload of 2 integers and vector of `NeedsReward` (3 pointer fields); dispatch. | `0x006987AC: ldr r0,[r4,#0x54]`; `0x006987AE: ldr r1,[r4,#0x5c]`; `0x006987B0: strd r0,r1,[sp,#12]`; `0x006987BA: blx 0x004BD620` (`std::vector<NeedsReward>` copy constructor; its internal element-copy details were not traced in this pass); `0x006987CC: blx 0x004BDD28`; `0x006987D4: blx r7` (external-interface virtual+0x1C). | EXACT_SOURCE |
| A2.14f | Outer message tag 102 and move vector ownership into payload | `0x0072653A: ldrd r12,r3,[r1]`; `0x00726540: strd r12,r3,[r0,#8]`; `0x0072654A: ldr r3,[r1,#8]`; `0x0072654C: str r3,[r0,#0x10]`; `0x0072654E: ldr r3,[r1,#0xc]`; `0x00726550: str r3,[r0,#0x14]`; `0x00726552: ldr r3,[r1,#0x10]`; `0x00726554: str r3,[r0,#0x18]`; `0x00726556: strd r2,r2,[r1,#8]`; `0x0072655A: str r2,[r1,#0x10]`; `0x0072655C: movs r1,#0x66`; `0x0072655E: strh r1,[r0]`. | EXACT_SOURCE |
| A2.14g | Serialized payload is `u32` at +0, `u32` at +4, 1-byte list count, each `NeedsReward` in full. | `0x00717164: ldr r0,[r5]`; `0x0071716C: movs r2,#4`; `0x0071716E: blx 0x004BFC0C`; `0x00717172: ldr r0,[r5,#4]`; `0x0071717A: movs r2,#4`; `0x0071717C: blx 0x004BFC0C`; `0x00717180: ldrd r0,r1,[r5,#8]`; `0x00717198: strb.w r0,[sp,#0xf]`; `0x0071719E: blx 0x004BFC0C` (count1); `0x007171AA: mov r0,r6`; `0x007171AE: blx 0x004C1F58` (`NeedsReward::Pack`); `0x007171B2: adds r6,#0x14` | EXACT_SOURCE |
| A2.14h | Each nested `NeedsReward` serializes **every** field: `rewardType` u32, `data` string, `inventoryIsFull` u8. | `0x0078441C: ldr r0,[r5]`; `0x00784424: movs r2,#4`; `0x00784426: blx 0x004BFC0C`; `0x0078442A: adds r1,r5,#4`; `0x0078442E: bl 0x006C22DC` (string pack); `0x00784432: ldrb r1,[r5,#0x10]`; `0x00784436: blx 0x004BFC18` (byte pack) | EXACT_SOURCE |

The `StarLevelCompleted` **complete C++ payload struct** is `+0x00` four-byte current level (manager+0x54); `+0x04` four-byte max/threshold (manager+0x5C); `+0x08,+0x0C,+0x10` vector<NeedsReward> begin/end/capacity (12 bytes, elements 0x14 bytes each). The enclosing `MessageEngineToGame` is tag **102**. The level field is read *after* `NeedsState::SetStarLevel`; whether manager+0x54 has been synchronized by that time is not established by these instructions alone, so source origin rather than a guessed semantic updated-level value is stated.

**Important boundary:** `ProcessLevelRewards` at `0x006993F8` demonstrably branches by reward type (`0x00699448: ldr.w r0,[r6,r11]`; `0x00699454: blo 0x006994F2`; `0x00699458: beq 0x00699552`) and has inventory/award operations (`0x006994D0: blx 0x004BDC74` -> `AwardSparks`, `0x00699506: blx 0x004ACED4`); a complete inventory of its indirect messages and every effect is **UNKNOWN** here. Accordingly this report does **not** claim `UpdateStarsState` emits *only* the two directly identified game-message types in every downstream path.

## A3. Default and test-variation needs-level configs

Five files are actually present in the uploaded resources ZIP, each under `cozmo_resources/config/engine/`:

- `needs_level_config.json`
- `needs_level_config_alloff.json`
- `needs_level_config_testAoff.json`
- `needs_level_config_testBoff.json`
- `needs_level_config_testCoff.json`

All five contain a top-level `unlockLevels` array with **28 entries**. As a concrete asset observation, the first entry of unsuffixed default has `numStarsToUnlock = 1` while the first entry of the four suffixed files has `numStarsToUnlock = 3`; these are **data observations**, not runtime selection evidence.

| Step | Behavior established | Exact native instructions | Classification |
|---|---|---|---|
| A3.1 | Default `RobotDataLoader::LoadRobotConfigs` selects base **`config/engine/needs_level_config`**, appends literal `.json`, yielding unsuffixed filename, and reads that JSON through DataPlatform | `0x0052282C: ldr.w r1,[pc,#0xb54]`; `0x00522836: add r1,pc` (resolved native string); `0x0052283C: mov r0,r5`; `0x0052283E: movs r2,#0x20`; `0x00522840: blx 0x004A42AC` (copy base); `0x00522844: ldr.w r0,[pc,#0xb40]`; `0x00522856: blx 0x004A7318` (string concatenation); `0x0052289C: blx 0x004A64E4` (`DataPlatform::readAsJson`). The shipped literal for suffix is native `.json` at `0x00BE5869`. | EXACT_SOURCE |
| A3.2 | NeedsManager initialization hands star-level JSON to `StarRewardsConfig::Init`, stored manager+0x1A8 | `0x0069260A: ldr.w r0,[r4,#0x1a8]`; `0x00692612: blx 0x004BD980` (`StarRewardsConfig::Init`) | EXACT_SOURCE |
| A3.3 | `StarRewardsConfig::Init` reads root JSON level array, iterates and constructs 0x40-byte `UnlockLevel` records | `0x0068E66C: ldr r1,[pc,#0xa4]`; `0x0068E672: blx 0x004A5F80`; `0x0068E678: blx 0x004A60E8` (array test); `0x0068E67C: cmp r0,#1`; `0x0068E6AA: blx 0x004A6130` (indexed element); `0x0068E6BA: blx 0x004BD614` (`UnlockLevel::SetFromJSON`); `0x0068E6E4: adds r0,#0x40` | EXACT_SOURCE. Native initializer's global-key literal not independently decoded here; observed shipped root key is `unlockLevels`. |
| A3.4 | Tuning entrypoint arguments are `this` (r0), variation **string** (r1→r8), and `AssignmentStatus` (r2→r6); status is converted to text | `0x00697192: mov r6,r2`; `0x00697194: mov r9,r0`; `0x0069719C: mov r8,r1`; `0x006971A2: mov r0,r6`; `0x006971A4: blx 0x004BDC98` (`EnumToString(AssignmentStatus)`) | EXACT_SOURCE |
| A3.5 | Build/store human-readable status string in manager **+0x3EC..+0x3F7**. This is distinct from variation suffix | `0x006971EA: add.w r0,r9,#0x3ec`; `0x006971F4: ldrb.w r1,[r9,#0x3ec]`; `0x0069721E: strb.w r5,[r9,#0x3ec]`; `0x00697226: str.w r7,[r9,#0x3f4]` | EXACT_SOURCE |
| A3.6 | Compare incoming variation string to stored manager+0x3F8/+0x3FC/+0x400 (C++ string representation) while constructing filenames; if equal, suffix append is skipped | `0x00697272: add.w r7,r9,#0x3f8`; `0x00697278: ldrb.w r6,[r8]`; `0x0069727C: ldrb.w r1,[r9,#0x3f8]`; `0x00697280: ldr.w r2,[r8,#4]`; `0x00697288: ldr.w r0,[r9,#0x3fc]`; `0x00697298: cmp r2,r0`; `0x0069729A: bne 0x006972DA`; `0x006972BC: ldrb r2,[r1]`; `0x006972C0: cmp r6,r2`; `0x006972DA: add r6,sp,#0x58`; `0x006972E4: blx 0x004A7330` (append input variation). | EXACT_SOURCE; where `+0x3F8` is last written UNKNOWN |
| A3.7 | Build and read `config/engine/needs_config` + incoming variation + `.json`; success initializes NeedsConfig at manager+0x128, failure logs | `0x00697256: ldr.w r1,[pc,#0x440]`; `0x0069726C: blx 0x004A42AC` (copy base); `0x006972E4: blx 0x004A7330` (optional variation append); `0x0069731A: bl 0x004E8048` (append `.json`); `0x00697338: blx 0x004A64E4`; `0x0069733C: cmp r0,#0`; `0x00697340: add.w r0,r9,#0x128`; `0x00697346: blx 0x004BD968`; failure branch `0x0069733E: beq 0x006973D0` | EXACT_SOURCE |
| A3.8 | Build and read **`config/engine/needs_level_config` + incoming variation + `.json`**. On success reinitialize `StarRewardsConfig` at manager+0x1A8; on failure log rather than initialize | `0x0069735A: ldr.w r1,[pc,#0x3a0]`; `0x0069736E: blx 0x004A42AC` (copy base); `0x006973F4: add r6,sp,#0x30`; `0x006973F8: mov r2,r8`; `0x006973FC: blx 0x004A7330` (append variation); `0x00697428: ldr r1,[pc,#0x2d4]` (append `.json`); `0x0069744E: blx 0x004A64E4`; `0x00697452: cmp r0,#0`; `0x00697454: beq 0x006974E4`; `0x00697456: ldr.w r0,[r9,#0x1a8]`; `0x0069745C: blx 0x004BD980` | EXACT_SOURCE |
| A3.9 | Build and read `config/engine/needs_action_config` + incoming variation + `.json`; success reinitializes ActionsConfig at manager+0x19C | `0x00697470: ldr r1,[pc,#0x290]`; `0x00697482: blx 0x004A42AC`; `0x00697510: blx 0x004A7330`; `0x0069753C: ldr r1,[pc,#0x1c8]` (append `.json`); `0x00697562: blx 0x004A64E4`; `0x00697566: cbz r0,0x00697574`; `0x00697568: add.w r0,r9,#0x19c`; `0x0069756E: blx 0x004BD98C` | EXACT_SOURCE |
| A3.10 | Destroy temporary JSON/string values before return; failure to read requested variant is logged (the function's return value does not identify selected variant) | `0x0069758E: add r0,sp,#8`; `0x00697590: blx 0x004A4FF0`; `0x006975A4: blx 0x004A4FF0`; `0x006975B8: blx 0x004A4FF0`; `0x006975CA: add sp,#0x7c`; `0x006975CC: pop.w {r4,r5,r6,r7,r8,r9,pc}` | EXACT_SOURCE |

**Selection conclusion:** The default loader uses the **unsuffixed** `needs_level_config.json`. `SetTuningTestVariation` selects a suffixed file **only if supplied that suffix as its string argument** (e.g. `_alloff`, `_testAoff`, `_testBoff`, `_testCoff`). The native function does **not** choose one of A/B/C based on `AssignmentStatus` itself: that status is converted for display; the filename comes from the separate string parameter. **UNKNOWN:** which variation, if any, is active on a particular robot/session, because the caller and its variation-string construction were not traced here; no inference from shipped file existence.

## B. M12-049 — not extracted in this pass

By the user's explicit split instruction, this pass **stops after A** rather than presenting unverified pyramid-cache claims. **UNKNOWN / NOT TRACED:** identities and complete `operator()` steps of the two BehaviorBuildPyramid helper callbacks (B4); `BlockConfigurationContainer::Update`, `CastNewEntryToBestSharedPointer<PyramidBase>`, `PruneFullPyramids`, and the full cache entry ownership/content semantics (B5). Symbol/address *leads only*, **not evidence**: `0x005DC16C`, `0x005DC298`, `0x0061682C`, `0x0062C68C`, `0x0062C72E`. No B behavior is classified from leads.

## Unresolved / audit boundaries

1. **A1:** malformed/empty arrays, zero level denominator, NaN random/threshold behavior; detailed internal `InventoryComponent::GetInventoryAmount(1)` implementation not traced. No fabricated valid-input behavior.
2. **A2:** `NeedsState::GetNeedBracketByIndex` helper at `0x0069CB20` may modify downstream state; helper not traced. `PossiblyStartWriteToRobot(true)` payload/writer and `LocalNotifications::Generate` full downstream effects are **UNKNOWN**. `ProcessLevelRewards` has multiple paths with observable inventory effects; exhaustive downstream event/message census **UNKNOWN**; hence no claim the two direct game broadcasts exhaust all effects. Log event's dynamically constructed field strings not fully decoded.
3. **A3:** caller selection of test-variation string and runtime-assigned `manager+0x3F8` value **UNKNOWN**. The global JSON-key string that `StarRewardsConfig::Init` references at `0x0068E66C` is not independently decoded from the BSS initializer in this pass; shipped file key is `unlockLevels`.
4. **B4–B5:** intentionally **NOT STARTED** by the requested A-before-B split.

**No repo/project code edits, status changes, or manifests changed.**
