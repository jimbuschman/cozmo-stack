# Cozmo native extraction — M15-071 follow-up (A) and M15-059 (B)

**Primary source:** uploaded `libcozmoEngine.so`, ARMv7/Thumb, address space as shipped. **Method:** direct Thumb disassembly (`llvm-objdump`), ELF jump-slot relocation resolution, ELF data/string decoding, native exception-table decoding. **Scope:** requested six questions only. **Status:** research report, no changes to code, manifest, inventory, or project status. `EXACT_SOURCE` below describes a verified **individual step**, not promotion of an entire fidelity record. Existing notes/decompiler output are not offered as evidence. `UNKNOWN` is explicit.

## A1. Static global at 0x0105C398

| Step | Original engine action | Exact native evidence | Record / step classification |
|---|---|---|---|
| A1.1 | Resolve the **static `std::__ndk1::string` object** at `0x0105C398` in the module initializer. | `0x004D90DE: ldr r6,[pc,#0xec]`; `0x004D90E2: add r6,pc` (the resolved PC-relative address is `0x0105C398`). | M15-071 / EXACT_SOURCE |
| A1.2 | Zero its three words before construction. | `0x004D90E4: strd r4,r4,[r6]`; `0x004D90E8: str r4,[r6,#8]` (`r4` is zero in this initializer). | M15-071 / EXACT_SOURCE |
| A1.3 | Allocate storage for its 26-byte string; copy the literal `needs.FreeplaySparksReward` from native read-only data at `0x00C4A360`. | `0x004D90EA: blx 0x004A42A0` (`operator new`); `0x004D90F2: movs r0,#0x1a`; `0x004D90F4: movs r2,#0x1a`; `0x004D90F6: add r1,pc` (resolves `0x00C4A360`); `0x004D9100: blx 0x004A42AC` (`memcpy`); rodata `0x00C4A360`: bytes decode to `needs.FreeplaySparksReward`. | M15-071 / EXACT_SOURCE |
| A1.4 | Populate the string's three words, null-terminate it and register destruction. | `0x004D90F8: strd r9,r0,[r6]`; `0x004D90FE: str r7,[r6,#8]`; `0x004D910A: strb r4,[r7,#0x1a]`; `0x004D910C: blx 0x004A4024` (`__cxa_atexit`). The 12-byte object is `[capacity/flags, length=26, heap pointer]` in this long-string representation. | M15-071 / EXACT_SOURCE |

**Answer A1:** This is a native static initializer, not a JSON key read. Exact global string value: **`needs.FreeplaySparksReward`**.

## A2. 0x004BDBA8, action-name conversion and all possible enum entries

| Step | Action | Native citation | Classification |
|---|---|---|---|
| A2.1 | ELF PLT `0x004BDBA8` resolves to `Anki::Cozmo::EnumToString(Anki::Cozmo::NeedsActionId)` at `0x00782DCC` (Thumb entry `0x00782DCD`). | ELF jump-slot relocation for PLT `0x004BDBA8` names `_ZN4Anki5Cozmo12EnumToStringENS0_13NeedsActionIdE`; direct function `0x00782DCC: cmp r0,#0x56`. | M15-071 / EXACT_SOURCE |
| A2.2 | Out-of-range unsigned action IDs (`>86`) return a null pointer. | `0x00782DCC: cmp r0,#0x56`; `0x00782DCE: itt hi`; `0x00782DD0: movhi r0,#0`; `0x00782DD2: bxhi lr`. | M15-071 / EXACT_SOURCE |
| A2.3 | Valid IDs `0..86` index a table of 87 four-byte C-string pointers; return the selected pointer. | `0x00782DD4: ldr r1,[pc,#8]`; `0x00782DD6: add r1,pc` (resolves table VA `0x01033D40`); `0x00782DD8: ldr.w r0,[r1,r0,lsl #2]`; `0x00782DDC: bx lr`. Each pointer/string was read from the ELF (`.data.rel.ro` VA, then `.rodata` C strings). | M15-071 / EXACT_SOURCE |
| A2.4 | Build pending display key by appending `.` and returned action name to the global prefix; then move the completed string to `this+0x3E0` (12-byte `std::string`). | `0x0069640E: add r1,pc` (global `0x0105C398`); `0x00696414: add r0,sp,#0xb0`; `0x00696416: adr r2,...` (literal `.`); `0x00696418: blx 0x004A7318` (`operator+(string,char const*)`); `0x0069641C: mov r0,r7`; `0x0069641E: blx 0x004BDBA8`; `0x00696424: mov r0,r4`; `0x00696426: blx 0x004A44E0` (`strlen`); `0x0069642C: add r0,sp,#0xb0`; `0x00696430: bl 0x004E8048` (append); `0x0069644A: add.w r0,r11,#0x3e0`; `0x00696470: bl 0x004E75EC`; `0x0069647A: strb.w r6,[r11,#0x3e0]`; `0x0069647E: blx 0x004A42AC`; `0x00696482: str.w r5,[r11,#0x3e8]`. | M15-071 / EXACT_SOURCE |

**All 87 table entries** (the number at left is the `NeedsActionId` passed in `r0`; text at right is the exact returned C string):

```
 0 NoAction                       1 Decay                         2 RepairHead
 3 RepairLift                     4 RepairTreads                  5 Feed
 6 KeepAwayWin                    7 KeepAwayLose                  8 MemoryMatchWin
 9 MemoryMatchLose               10 QuickTapWin                  11 QuickTapLose
12 CozmoPerformsComplete         13 CozmoSings                   14 DizzyHard
15 DizzyMedium                   16 DizzySoft                    17 Fall
18 FistBump                      19 FistBump_Sparked             20 GatherCubes
21 GuardDogLose                  22 GuardDogWin                  23 GuardDogNoInteraction
24 HiccupsEndBad                 25 HiccupsEndGood               26 IndividualHiccup
27 KnockDownCubes                28 KnockDownCubes_Sparked       29 PeekAboo
30 PeekAboo_Sparked              31 PickupCube                   32 PickupCube_Sparked
33 PopAWheelie                   34 PopAWheelie_Sparked          35 Pounce
36 Pounce_Sparked                37 PyramidCompleted             38 PyramidCompleted_Sparked
39 RollACube                     40 RollACube_Sparked            41 SayName
42 StackCube                     43 StackCube_Sparked            44 Workout
45 Workout_Sparked               46 SeeFace                      47 PlacedOnSide
48 BoredOnSide                   49 Laser_Sparked                50 CubeMixup
51 TreasureTrail                 52 Roboshambo                   53 TwistTapRoll
54 CubeTennis                    55 TidalTap                     56 StareOff
57 DadJokesBadJokes              58 CubeBlaster                  59 NumberTap
60 JackpotBot                    61 ColorShuffle                 62 ForYouMom
63 ComposeWithCozmo              64 PoetryJam                    65 CozmosGarden
66 Constellations                67 GuessTheNumber               68 WakeUpCozmo
69 PaddleBall                    70 DrivingInCircles             71 PotOGoldDetector
72 ComplimentGenerator           73 RemoteControlRacer           74 ShakeIt
75 PuttPutt                      76 ValentinesCozmogram          77 LaserSmile
78 MagicFortuneTeller            79 LightShow                    80 CubeWhack
81 TinyOrchestra                 82 HotPotato                    83 BlasterTag
84 FireTruckAlarm                85 CozmoSingsCompleted_Sparked  86 Count
```

For a valid ID *n*: display key = `needs.FreeplaySparksReward.` + table[n]. **UNKNOWN:** defined behavior for an invalid ID after returning null: the caller immediately passes the result to `strlen`, so an invalid out-of-range input does not have a source-established valid display-key value (`0x0069641E`, `0x00696426`).

## A3. Complete `FreeplaySparksAwarded` value/message and moved-from state

| Step | Action | Exact native citation | Classification |
|---|---|---|---|
| A3.1 | `SparksRewardCommunicatedToUser` clears pending flag *unconditionally*. | `0x00696E6A: movs r2,#0`; `0x00696E78: strb.w r2,[r0,#0x3d8]`. | M15-071 / EXACT_SOURCE |
| A3.2 | Passes address `this+0x3DC` as an rvalue payload to the message constructor. | `0x00696E82: add.w r1,r0,#0x3dc`; `0x00696E88: blx 0x004BDC80` (PLT resolves to `MessageEngineToGame(FreeplaySparksAwarded&&)` at `0x00726412`). | M15-071 / EXACT_SOURCE |
| A3.3 | **Field 1:** copies 32-bit integer `sparksAwarded` (payload offset 0) to outgoing union offset `+0x8`. Does not modify source integer. | `0x00726414: ldr r2,[r1]`; `0x0072641A: str r2,[r0,#8]`. | M15-071 / EXACT_SOURCE |
| A3.4 | **Field 2:** moves the *entire* 12-byte `std::string sparksAwardedDisplayKey` (payload offsets +4,+8,+0xC) into the outgoing union at offsets +0xC,+0x10,+0x14. | `0x00726416: add.w r12,r1,#4`; `0x0072641C: add.w r3,r0,#0xc`; `0x00726420: ldm.w r12,{r2,r4,lr}`; `0x00726424: stm.w r3,{r2,r4,lr}`. | M15-071 / EXACT_SOURCE |
| A3.5 | Zeroes all 12 source-string bytes (three words). The source string is now empty; heap ownership is transferred, not freed here. | `0x00726428: movs r2,#0`; `0x0072642A: strd r2,r2,[r1,#4]`; `0x0072642E: str r2,[r1,#0xc]`. | M15-071 / EXACT_SOURCE |
| A3.6 | Sets complete enclosing `MessageEngineToGame` tag to decimal **101** (`0x65`). | `0x00726430: movs r1,#0x65`; `0x00726432: strh r1,[r0]`. | M15-071 / EXACT_SOURCE |
| A3.7 | Sends the constructed message through the external interface and destroys/clears the temporary outgoing message afterwards. | `0x00696E90: blx r6` (virtual external-interface broadcast); `0x00696E94: blx 0x004A5158` (outgoing-message cleanup). | M15-071 / EXACT_SOURCE |
| A3.8 | Confirm **complete serialized payload field census**: exactly a 4-byte integer and one variable-length string, no third payload field. | `0x00716FAA: ldr r0,[r5],#4`; `0x00716FB4: movs r2,#4`; `0x00716FB6: blx 0x004BFC0C` (write integer); `0x00716FBC: mov r1,r5`; `0x00716FBE: bl 0x006C22DC` (pack string); `0x00717000: ldrb r1,[r0,#4]`; `0x0071700C: adds r0,#5` (serialized 4-byte int plus 1-byte string length plus bytes). | M15-071 / EXACT_SOURCE |

**Answer A3 — state immediately after the broadcast routine:** `+0x3D8=0`; `+0x3DC` remains the unchanged 32-bit amount; `+0x3E0,+0x3E4,+0x3E8` are all zero (an empty moved-from C++ string). There are **two and only two fields** in `FreeplaySparksAwarded` itself; the external envelope has a separate message tag `101`.

## A4. Complete `UnlockLevel` JSON field layout, including +0x2C

`StarRewardsConfig::Init` gets `rewards` from its input JSON (`0x0068E66C: ldr r1,[pc,#0xa4]`; `0x0068E670: add r1,pc`; `0x0068E672: blx 0x004A5F80`), confirms the property is an array (`0x0068E678: blx 0x004A60E8`; `0x0068E67C: cmp r0,#1`), and loops over indexed entries (`0x0068E6A6: mov r0,r10`; `0x0068E6A8: mov r1,r8`; `0x0068E6AA: blx 0x004A6130`; `0x0068E6BA: blx 0x004BD614` to `UnlockLevel::SetFromJSON` at `0x007869D0`). Entries have stride 0x40 bytes (`0x0068E6E4: adds r0,#0x40`; `0x0068E6DA..0x0068E6DE: ldm/stm` copy all 64 bytes). Every scalar property below is conditional on `Json::Value::isMember(key)` (`0x004A5080`); if absent, the setter does not write that field. `0x004A5F2C` is `Json::Value::operator[](const char*)`, `0x004AA6E4` is `asInt()`, `0x004A7264` is `asFloat()`.

| Offset in 64-byte `UnlockLevel` | JSON key and type | Exact native proof (conditional check; conversion; store) | Classification |
|---|---|---|---|
| `+0x00,+0x04,+0x08` | `rewards`: vector begin / end / capacity, each nested `NeedsReward` is **20 bytes** | `0x007869E0: blx 0x004A5080`; `0x007869EE: blx 0x004A5F2C`; `0x007869F4: blx 0x004A6124` (array length); `0x007869FC: blx 0x004C6668` (reserve); `0x00786A10: ldr.w r5,[r8]`; `0x00786A1C: blx 0x004C6674` (`NeedsReward::SetFromJSON`); `0x00786A28: adds r4,#0x14` (20-byte stride). | M15-071 / EXACT_SOURCE |
| `+0x0C` | `numStarsToUnlock` int | `0x00786A34: blx 0x004A5080`; `0x00786A42: blx 0x004A5F2C`; `0x00786A46: blx 0x004AA6E4`; `0x00786A4A: str.w r0,[r8,#0xc]`. | M15-071 / EXACT_SOURCE |
| `+0x10` | `targetSparksTotal` int | `0x00786A54: blx 0x004A5080`; `0x00786A62: blx 0x004A5F2C`; `0x00786A66: blx 0x004AA6E4`; `0x00786A6A: str.w r0,[r8,#0x10]`. | M15-071 / EXACT_SOURCE |
| `+0x14` | `maxPriorLevelUnlocks` int | `0x00786A74: blx 0x004A5080`; `0x00786A82: blx 0x004A5F2C`; `0x00786A86: blx 0x004AA6E4`; `0x00786A8A: str.w r0,[r8,#0x14]`. | M15-071 / EXACT_SOURCE |
| `+0x18` | `minSparksPct` float | `0x00786A94: blx 0x004A5080`; `0x00786AA2: blx 0x004A5F2C`; `0x00786AA6: blx 0x004A7264`; `0x00786AAA: str.w r0,[r8,#0x18]`. | M15-071 / EXACT_SOURCE |
| `+0x1C` | `maxSparksPct` float | `0x00786AB4: blx 0x004A5080`; `0x00786AC2: blx 0x004A5F2C`; `0x00786AC6: blx 0x004A7264`; `0x00786ACA: str.w r0,[r8,#0x1c]`. | M15-071 / EXACT_SOURCE |
| `+0x20` | `minSparks` int | `0x00786AD4: blx 0x004A5080`; `0x00786AE2: blx 0x004A5F2C`; `0x00786AE6: blx 0x004AA6E4`; `0x00786AEA: str.w r0,[r8,#0x20]`. | M15-071 / EXACT_SOURCE |
| `+0x24` | `minMaxSparks` int | `0x00786AF4: blx 0x004A5080`; `0x00786B02: blx 0x004A5F2C`; `0x00786B06: blx 0x004AA6E4`; `0x00786B0A: str.w r0,[r8,#0x24]`. | M15-071 / EXACT_SOURCE |
| `+0x28` | **`freeplayTargetSparksTotal`**, int | `0x00786B14: blx 0x004A5080`; `0x00786B22: blx 0x004A5F2C`; `0x00786B26: blx 0x004AA6E4`; `0x00786B2A: str.w r0,[r8,#0x28]`. | M15-071 / EXACT_SOURCE |
| `+0x2C` | **`freeplayMinSparksRewardPct`**, float — *not padding* | `0x00786B34: blx 0x004A5080`; `0x00786B42: blx 0x004A5F2C`; `0x00786B46: blx 0x004A7264`; `0x00786B4A: str.w r0,[r8,#0x2c]`. | M15-071 / EXACT_SOURCE |
| `+0x30` | **`freeplayMinSparksPct`**, float | `0x00786B54: blx 0x004A5080`; `0x00786B62: blx 0x004A5F2C`; `0x00786B66: blx 0x004A7264`; `0x00786B6A: str.w r0,[r8,#0x30]`. | M15-071 / EXACT_SOURCE |
| `+0x34` | **`freeplayMaxSparksPct`**, float | `0x00786B74: blx 0x004A5080`; `0x00786B82: blx 0x004A5F2C`; `0x00786B86: blx 0x004A7264`; `0x00786B8A: str.w r0,[r8,#0x34]`. | M15-071 / EXACT_SOURCE |
| `+0x38` | **`freeplayMinSparks`**, int | `0x00786B94: blx 0x004A5080`; `0x00786BA2: blx 0x004A5F2C`; `0x00786BA6: blx 0x004AA6E4`; `0x00786BAA: str.w r0,[r8,#0x38]`. | M15-071 / EXACT_SOURCE |
| `+0x3C` | **`freeplayMinMaxSparks`**, int | `0x00786BB4: blx 0x004A5080`; `0x00786BC2: blx 0x004A5F2C`; `0x00786BC6: blx 0x004AA6E4`; `0x00786BCA: str.w r0,[r8,#0x3c]`. | M15-071 / EXACT_SOURCE |

The JSON property strings above were resolved from the PC-relative native literal addresses at `0x00786BD8..0x00786C38`; the store locations and call sequences are the actual behavioral evidence.

**Complete nested `NeedsReward` struct, each 0x14 bytes** (the caller indexes entries with `0x00786A28: adds r4,#0x14`):

| Offset | JSON member | Instruction-level evidence |
|---|---|---|
| `+0x0` | `rewardType` enum/int | `0x007845D6: blx 0x004A5080` (`isMember`); `0x007845E4: blx 0x004A5F2C` (`operator[]`); `0x007845F4: blx 0x004C656C` (name-to-enum conversion); `0x007845F8: str r0,[r4]` |
| `+0x4..+0xF` | `data` — 12-byte `std::string` | `0x0078460E: blx 0x004A5080`; `0x0078461C: blx 0x004A5F2C`; `0x00784626: blx 0x004A61CC` (JSON to string); `0x0078462C: ldrb r0,[r7,#4]!`; `0x0078464C: ldm.w r6,{r0,r1,r2}`; `0x00784650: stm r7!,{r0,r1,r2}` |
| `+0x10` | `inventoryIsFull` — bool | `0x00784658: blx 0x004A5080`; `0x00784666: blx 0x004A5F2C`; `0x0078466A: blx 0x004AE758` (JSON bool conversion); `0x0078466E: strb r0,[r4,#0x10]` |
| `+0x11..+0x13` | no additional JSON data field; alignment/storage padding | Next element stride `0x14` at `0x00786A28`. |

## B5. `ActivitySparked::ResetLightsAndAnimations` complete main path

| Step | Operation in exact execution order | Native instructions and arguments | Classification |
|---|---|---|---|
| B5.1 | Save activity (`r4`) and robot (`r5`); test activity flag `this+0xB5`. If zero, skip all work, return. | `0x005B17A8: mov r4,r0`; `0x005B17AA: mov r5,r1`; `0x005B17AC: ldrb.w r0,[r4,#0xb5]`; `0x005B17B0: cbz r0,0x005B1802`. | M15-059 / EXACT_SOURCE |
| B5.2 | Obtain robot's `DrivingAnimationHandler*` from `Robot+0x24C` and call activity virtual method vtable `+0x24` to get its activity-name C string. | `0x005B17B2: ldr r0,[r4]`; `0x005B17B4: ldr.w r8,[r5,#0x24c]`; `0x005B17B8: ldr r1,[r0,#0x24]`; `0x005B17BA: mov r0,r4`; `0x005B17BC: blx r1`; `0x005B17BE: mov r7,r0`. The `ActivitySparked` vtable path resolves through `0x005A7376: ldrb.w r0,[r0,#0x34]` / `0x005A737A: b.w 0x008CBD2C`, whose ARM veneer targets `EnumToString(ActivityID)` via PLT `0x004AEFA4`. | M15-059 / EXACT_SOURCE |
| B5.3 | Construct temporary 12-byte C++ string from returned activity name, of length `strlen(name)`. | `0x005B17C0: movs r0,#0`; `0x005B17C2: str r0,[sp,#8]`; `0x005B17C4: strd r0,r0,[sp]`; `0x005B17C8: mov r0,r7`; `0x005B17CA: blx 0x004A44E0` (`strlen`); `0x005B17CE: mov r6,sp`; `0x005B17D0: mov r2,r0` (length); `0x005B17D2: mov r0,r6` (temporary string address); `0x005B17D4: mov r1,r7` (char*); `0x005B17D6: bl 0x004E02B2` (construct/init string). At `0x004E02CA..0x004E02F8`, constructor selects small/long representation and copies from `r1` for `r2` bytes. | M15-059 / EXACT_SOURCE |
| B5.4 | Remove matching driving-animation entries by activity name. | `0x005B17DA: mov r0,r8` (`DrivingAnimationHandler*`); `0x005B17DC: mov r1,r6` (`std::string const&`); `0x005B17DE: blx 0x004A5818` (ELF relocation -> `DrivingAnimationHandler::RemoveDrivingAnimations`, at `0x004F0A94`). | M15-059 / EXACT_SOURCE |
| B5.5 | Free temporary activity-name string allocation only if long/heap representation. | `0x005B17E2: ldrb.w r0,[sp]`; `0x005B17E6: lsls r0,r0,#0x1f`; `0x005B17E8: itt ne`; `0x005B17EA: ldrne r0,[sp,#8]`; `0x005B17EC: blxne 0x004A40CC` (`operator delete`). | M15-059 / EXACT_SOURCE |
| B5.6 | Stop corresponding looping backpack lights by locator `activity+0xC8`, using robot `BodyLightComponent*` at `Robot+0x274`. Result ignored. | `0x005B17F0: ldr.w r0,[r5,#0x274]`; `0x005B17F4: add.w r1,r4,#0xc8`; `0x005B17F8: blx 0x004B1C50` (ELF relocation -> `BodyLightComponent::StopLoopingBackpackLights`, at `0x006324A0`). | M15-059 / EXACT_SOURCE |
| B5.7 | Clear flag `this+0xB5` **after** both removal calls. | `0x005B17FC: movs r0,#0`; `0x005B17FE: strb.w r0,[r4,#0xb5]`; `0x005B1802: add sp,#0x10`; `0x005B1804: pop.w {r4,r5,r6,r7,r8,pc}`. | M15-059 / EXACT_SOURCE |
| B5.8 | If an exception reaches local cleanup after temporary name construction, delete heap temporary and resume unwinding, rather than performing the remaining ordinary reset steps. | `0x005B1808: mov r4,r0`; `0x005B180A: ldrb.w r0,[sp]`; `0x005B180E: lsls r0,r0,#0x1f`; `0x005B1812: ldrne r0,[sp,#8]`; `0x005B1814: blxne 0x004A40CC`; `0x005B1818: mov r0,r4`; `0x005B181A: blx 0x004A40A8` (`_Unwind_Resume`). | M15-059 / EXACT_SOURCE |

**Complete 16-byte `BackpackLightDataLocator` layout** (followed into the native producer `BodyLightComponent::StartLoopingBackpackLightsInternal`, not guessed from its name):

| Locator offset | Value recorded by producer | Native source |
|---|---|---|
| `+0x00` | tree/map node or owner iterator (`r6`) | `0x00632320: strd r6,r4,[r8]`; `0x00632306..0x00632312` select the tree node |
| `+0x04` | light-list node/iterator (`r4`) | `0x00632312: ldr r4,[r6,#0x18]`; `0x00632320: strd r6,r4,[r8]` |
| `+0x08` | shared light-data pointer (`r7`) | `0x00632314: ldrd r7,r5,[r4,#8]`; `0x00632324: str.w r7,[r8,#8]` |
| `+0x0C` | shared ownership/control-block pointer (`r5`) | `0x00632328: ldr.w r0,[r8,#0xc]`; `0x0063232C: str.w r5,[r8,#0xc]` |

**Followed effect of the B5.6 callee:** `StopLoopingBackpackLights` checks control block viability (`0x006324A8: ldr r0,[r5,#0xc]`; `0x006324AA: cbz ...`; `0x006324AC: ldr r0,[r0,#4]`; `0x006324AE: adds r0,#1`), then the owner's list presence (`0x006324B2: ldr r0,[r5]`; `0x006324B4: ldr r1,[r0,#0x1c]`; `0x006324B6: cbz r1,0x00632510`). If present, it erases the located list entry (`0x006324B8: ldr r1,[r5,#4]`; `0x006324BA: adds r0,#0x14`; `0x006324BC: blx 0x004B8E50` -> list erase). If list becomes empty it erases the owner tree node (`0x006324C2: ldr r0,[r1,#0x1c]`; `0x006324C4: cbnz r0,0x006324CE`; `0x006324C6: add.w r0,r4,#0x10`; `0x006324CA: blx 0x004B8E5C` -> tree erase). Returns 1 at `0x006324CE`, or logs and returns 0 at `0x0063250A`; the caller ignores the return. This follows all locator **fields**, including the producer's `+0x08` pointer, which the stop routine does not itself read. No colors or other light-setting payloads are sent directly by `ResetLightsAndAnimations`.

## B6. `ActivitySparked::ReloadFromConfig` exception/unwind tail: reachable ranges and actions

**First distinguish normal execution:** the ordinary success path finishes at `0x005B11A2: movs r0,#0`; `0x005B11A4: add sp,#0x54`; `0x005B11A6: pop.w {r4,r5,r6,r7,r8,r9,pc}`. The block beginning `0x005B11AA` is **not a normal fall-through extension**: it contains landing pads described by the engine's own `.ARM.extab` C++ exception table (native binary bytes at `0x00B0D80C`, corresponding to function entry `0x005B0EAC`; personality symbol `__gxx_personality_v0`). The table's `udata4` call-site entries enumerate protected code intervals and the landing-pad offsets below. This metadata establishes where **exceptions while unwinding** may land, rather than proving that a particular input actually throws. Every named landing pad is confirmed against the quoted Thumb branch or cleanup instruction at that address.

| Step | Protected native code interval [start,end); landing pad | Evidence at target / classification |
|---|---|---|
| B6.01 | `0x005B0ED8..0x005B0EE2` -> `0x005B1216` | `0x005B1216: mov r4,r0`; cleanup string at `0x005B1218..0x005B1222` / EXACT_SOURCE |
| B6.02 | `0x005B0F02..0x005B0F0E` -> `0x005B120C` | `0x005B120C: mov r4,r0`; `0x005B120E: add r0,sp,#0x18` / EXACT_SOURCE |
| B6.03 | `0x005B0F0E..0x005B0F18` -> `0x005B1202` | `0x005B1202: mov r4,r0`; `0x005B1204: add r0,sp,#0x30` / EXACT_SOURCE |
| B6.04 | `0x005B0F24..0x005B0F34` -> `0x005B125A` | `0x005B125A: mov r4,r0`; `0x005B125C: b 0x005B1246` / EXACT_SOURCE |
| B6.05 | `0x005B0F34..0x005B0F40` -> `0x005B11F8` | `0x005B11F8: mov r4,r0`; `0x005B11FA: add r0,sp,#0x18` / EXACT_SOURCE |
| B6.06 | `0x005B0F40..0x005B0F4C` -> `0x005B11EE` | `0x005B11EE: mov r4,r0`; `0x005B11F0: add r0,sp,#0x30` / EXACT_SOURCE |
| B6.07 | `0x005B0F82..0x005B0F8A` -> `0x005B11B2` | `0x005B11B2: b 0x005B1236` / EXACT_SOURCE |
| B6.08 | `0x005B0F8A..0x005B0F90` -> `0x005B11B0` | `0x005B11B0: b 0x005B11D8` / EXACT_SOURCE |
| B6.09 | `0x005B0FD6..0x005B0FDE` -> `0x005B11AE` | `0x005B11AE: b 0x005B1236` / EXACT_SOURCE |
| B6.10 | `0x005B0FDE..0x005B0FE4` -> `0x005B11AC` | `0x005B11AC: b 0x005B11D8` / EXACT_SOURCE |
| B6.11 | `0x005B1004..0x005B100C` -> `0x005B11EC` | `0x005B11EC: b 0x005B1236` / EXACT_SOURCE |
| B6.12 | `0x005B1034..0x005B103E` -> `0x005B11EA` | `0x005B11EA: b 0x005B1236` / EXACT_SOURCE |
| B6.13 | `0x005B103E..0x005B1048` -> `0x005B11D8` | `0x005B11D8: mov r4,r0`; `0x005B11DA: ldrb.w r0,[sp,#0x30]` / EXACT_SOURCE |
| B6.14 | `0x005B1066..0x005B1070` -> `0x005B11D6` | `0x005B11D6: b 0x005B1236` / EXACT_SOURCE |
| B6.15 | `0x005B1070..0x005B107A` -> `0x005B11D4` | `0x005B11D4: b 0x005B11D8` / EXACT_SOURCE |
| B6.16 | `0x005B109C..0x005B10A4` -> `0x005B11D2` | `0x005B11D2: b 0x005B1236` / EXACT_SOURCE |
| B6.17 | `0x005B10A8..0x005B10B0` -> `0x005B11D0` | `0x005B11D0: b 0x005B11D8` / EXACT_SOURCE |
| B6.18 | `0x005B10CE..0x005B10D6` -> `0x005B11CE` | `0x005B11CE: b 0x005B1236` / EXACT_SOURCE |
| B6.19 | `0x005B10D6..0x005B10E2` -> `0x005B11CC` | `0x005B11CC: b 0x005B11D8` / EXACT_SOURCE |
| B6.20 | `0x005B10F0..0x005B10FE` -> `0x005B1236` | `0x005B1236: mov r4,r0`; `0x005B1238: ldrb.w r0,[sp,#0xc]` / EXACT_SOURCE |
| B6.21 | `0x005B1102..0x005B110C` -> `0x005B11C8` | `0x005B11C8: mov r4,r0`; `0x005B11CA: b 0x005B122E` / EXACT_SOURCE |
| B6.22 | `0x005B110C..0x005B112C` -> `0x005B1226` | `0x005B1226: mov r4,r0`; `0x005B1228: add r0,sp,#0x30` / EXACT_SOURCE |
| B6.23 | `0x005B112C..0x005B1132` -> `0x005B11B6` | `0x005B11B6: mov r4,r0`; `0x005B11B8: ldrb.w r0,[sp]` / EXACT_SOURCE |
| B6.24 | `0x005B1154..0x005B115C` -> `0x005B11B4` | `0x005B11B4: b 0x005B1236` / EXACT_SOURCE |
| B6.25 | `0x005B115C..0x005B116C` -> `0x005B1224` | `0x005B1224: b 0x005B1236` / EXACT_SOURCE |
| B6.26 | `0x005B116C..0x005B1176` -> `0x005B11AA` | `0x005B11AA: b 0x005B1236` / EXACT_SOURCE |
| B6.27 | `0x005B0EAC..0x005B0ED8`, `0x005B0EE2..0x005B0F02`, and `0x005B1176..0x005B1304` have no landing pads specified for those native ranges in this local call-site table; other uncovered holes in the function likewise lack a local pad | **UNKNOWN** local cleanup for an exception in such a range; the table specifies landing offset zero. Do not imply these ranges are unthrowable. |

**Distinct cleanup steps / order inside the tail:**

| Step | Action | Exact native citation | Classification |
|---|---|---|---|
| B6.28 | Entry shortcuts jump to a matching cleanup depth rather than directly returning, e.g. `0x005B11AA`, `0x005B11AE`, `0x005B11B2`, `0x005B11B4` all branch to shared `0x005B1236`; `0x005B11AC`, `0x005B11B0` branch to `0x005B11D8`. | `0x005B11AA: b 0x005B1236`; `0x005B11AC: b 0x005B11D8`; `0x005B11B2: b 0x005B1236`. | M15-059 / EXACT_SOURCE |
| B6.29 | Some landing pads release a heap-backed short-term C++ string (`[sp+0x0]` string, pointer `[sp+0x8]`) before two JSON destructors. | `0x005B11B6: mov r4,r0`; `0x005B11B8: ldrb.w r0,[sp]`; `0x005B11C0: ldrne r0,[sp,#8]`; `0x005B11C2: blxne 0x004A40CC`; `0x005B11C6: b 0x005B1228`. | M15-059 / EXACT_SOURCE |
| B6.30 | Some pads release temporary string at `[sp+0x30]`, data pointer `[sp+0x38]`, *then* go to common string cleanup. | `0x005B11D8: mov r4,r0`; `0x005B11DA: ldrb.w r0,[sp,#0x30]`; `0x005B11DE: lsls r0,r0,#0x1f`; `0x005B11E2: ldr r0,[sp,#0x38]`; `0x005B11E4: blx 0x004A40CC`; `0x005B11E8: b 0x005B1238`. | M15-059 / EXACT_SOURCE |
| B6.31 | Other pads destroy JSON temporaries `Json::Value` at stack `+0x30` then `+0x18`, or only `+0x18`, as dictated by which objects were constructed when the throw occurred. | `0x005B11F0: add r0,sp,#0x30`; `0x005B11F2: blx 0x004A4FF0` (`Json::Value::~Value`); `0x005B11FA: add r0,sp,#0x18`; `0x005B11FC: blx 0x004A4FF0`; alternately `0x005B1204: add r0,sp,#0x30`; `0x005B1206: blx 0x004A4FF0`; `0x005B120E: add r0,sp,#0x18`; `0x005B1210: blx 0x004A4FF0`; `0x005B1228: add r0,sp,#0x30`; `0x005B122A: blx 0x004A4FF0`; `0x005B122E: add r0,sp,#0x18`; `0x005B1230: blx 0x004A4FF0`. | M15-059 / EXACT_SOURCE |
| B6.32 | Shared final cleanup checks and (if heap-backed) frees stack string at `+0x0C/+0x14`, then separately at `+0x48/+0x50`. | `0x005B1238: ldrb.w r0,[sp,#0xc]`; `0x005B123C: lsls r0,r0,#0x1f`; `0x005B1240: ldrne r0,[sp,#0x14]`; `0x005B1242: blxne 0x004A40CC`; `0x005B1246: ldrb.w r0,[sp,#0x48]`; `0x005B124A: lsls r0,r0,#0x1f`; `0x005B124E: ldr r0,[sp,#0x50]`; `0x005B1250: blx 0x004A40CC`. | M15-059 / EXACT_SOURCE |
| B6.33 | Pass exception object through to the unwinder; no conversion to a `ReloadFromConfig` normal error result in the tail. | `0x005B1254: mov r0,r4`; `0x005B1256: blx 0x004A40A8` (`_Unwind_Resume`); if unwind itself lands on its local secondary handler: `0x005B125A: mov r4,r0`; `0x005B125C: b 0x005B1246`. | M15-059 / EXACT_SOURCE |

**What reaches the exception tail:** the 26 specific protected execution intervals listed above, **only if an exception is actually raised in the interval**. Actual invoking operations include `Json::Value` operations (`0x005B0F0A: blx 0x004A7258`; `0x005B0F3C: blx 0x004A7258`; `0x005B1108: blx 0x004A7258`), string construction (`0x005B0ED4`, `0x005B0F86`, `0x005B10D2`, `0x005B1128`: each `bl 0x004E02B2`), property parsing (`0x005B1044: blx 0x004B1BF0`; `0x005B1076: blx 0x004B1BF0`; `0x005B10DE: blx 0x004A60DC`), and an objective parsing/conversion path (`0x005B112E: blx 0x004AD3B4`). The EHABI metadata maps the exact ranges; **which bad input or runtime failure causes a specific library call to throw is UNKNOWN without tracing that library call's throw paths**. The cleanup routines themselves do not change the activity's normal output state to a success or failure value.

## Explicit remaining UNKNOWNs / limitations

1. **A2 invalid enum:** `EnumToString(NeedsActionId)` returns null for IDs over 86; the next `strlen` receives null. Undefined original behavior is not replaced with an invented display string; no assertion that a normal run supplies invalid IDs.
2. **A3 concurrency/aliasing:** This trace proves what the constructor and broadcast path write sequentially, not absence of concurrent or indirect writes from code outside the requested path.
3. **A4 defaults:** Each JSON key is conditional. This report establishes *where* fields are filled, not constructor/default values if keys are absent, nor every caller of every field. Full `NeedsReward` nested field layout and all `UnlockLevel` 64 bytes are included; source of `rewards` JSON at an even higher configuration loader is outside this bounded request.
4. **B5 names and side effects:** The activity virtual name conversion is traced, but the actual `ActivityID` instance value on any particular run is runtime data. Full downstream timing/state effects of `RemoveDrivingAnimations` beyond identifying its native callee and direct call arguments were not exhaustively decompiled here. `BackpackLightDataLocator` is fully enumerated and the light-list erase path is followed.
5. **B6 triggering exceptions:** The native C++ exception table establishes protected ranges and landing pads, but does not establish which specific JSON value, memory condition, or input necessarily throws there. These are labeled UNKNOWN rather than guessed. Exception pad reachability assumes a thrown exception is present.
6. No project repository, C# implementation, manifest record or fidelity status was modified. No shipped sound bank was opened.
