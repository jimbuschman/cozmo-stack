# R-M15 gap-2 extraction: NeedsManager Init time, AttemptReadFromDevice, SendTimeSinceBackgroundedDasEvent, ReadFromDevice, WriteToDevice, LocalNotifications::Generate

Scope: R-M15 (M15-freeplay), gap pass 2, 2026-09-29. Read-only. All citations are Thumb VAs in `resources/lib/armeabi-v7a/libcozmoEngine.so`; instructions were read in the binary. The Ghidra decompilation under `re-analysis/decomp/libcozmoEngine/` was used only to locate entries. ABI: softfp AAPCS, so `float` arguments arrive in core registers (this build), not in `s0`.

## Function entries (verified in the `.so`; mangled names from `.dynsym`)

| function | entry (Thumb VA) | mangled name | body |
| --- | --- | --- | --- |
| NeedsManager::Init | 0x00692574 | `_ZN4Anki5Cozmo12NeedsManager4InitEfRKN4Json5ValueES5_S5_S5_S5_S5_` | 0x00692574..0x006926D9 |
| NeedsManager::InitInternal | 0x00693444 | `_ZN4Anki5Cozmo12NeedsManager12InitInternalEf` | 0x00693444..0x00693492 |
| NeedsManager::InitReset | 0x006934A8 | `_ZN4Anki5Cozmo12NeedsManager9InitResetEfjb` | 0x006934A8..0x006935E0 |
| NeedsManager::AttemptReadFromDevice | 0x00693690 | (thunk 0x004BD9C8) | 0x00693690..0x00693792 |
| NeedsManager::ReadFromDevice | 0x006998B4 | `_ZN4Anki5Cozmo12NeedsManager14ReadFromDeviceERKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEERb` | 0x006998B4..0x00699BB8 |
| NeedsManager::WriteToDevice | 0x00693BB0 | `_ZN4Anki5Cozmo12NeedsManager13WriteToDeviceEb` | 0x00693BB0..0x00693F66 |
| NeedsManager::SendTimeSinceBackgroundedDasEvent | 0x0069770C | `_ZN4Anki5Cozmo12NeedsManager33SendTimeSinceBackgroundedDasEventEv` | 0x0069770C..0x006977F1 |
| LocalNotifications::Init | 0x0068C618 | `_ZN4Anki5Cozmo18LocalNotifications4InitERKN4Json5ValueEPNS_4Util15RandomGeneratorEf` | 0x0068C618..0x0068C7A2 |
| LocalNotifications::Generate | 0x0068CA9C | `_ZN4Anki5Cozmo18LocalNotifications8GenerateEv` | 0x0068CA9C..0x0068CCE6 |
| FUN_00693384 | 0x00693384 | (none) | 0x00693384..0x0069340F |
| CozmoEngine::Init (call site) | 0x004EC9DA..0x004ECA0A | - | - |
| CozmoEngine::Update (call site) | 0x004ED632..0x004ED640 | - | - |
| static string initializer _INIT_48 | 0x004D8DDC | - | 0x004D8DDC..0x004D9142 |

---

## Q1. `NeedsManager::Init` float argument to `InitInternal` (`mov r1, r8` at 0x006926CE)

**Answer: it is `param_1`, the `float` argument of `NeedsManager::Init` (the "time" `CozmoEngine::Init` passes). It is NOT the return of `LocalNotifications::Init`, `DesiredFaceDistortionComponent::Init` or `FUN_00693384`.**

The mangled name `NeedsManager::Init(float, Json::Value const&, x6)` fixes the ABI: `this` = r0, `float param_1` = r1, then the Json refs in r2/r3/stack. `r8` is set exactly once in the function and is never reassigned; it is a callee-saved register carried across every intervening call.

| VA | instruction | meaning |
| --- | --- | --- |
| 0x0069257E | `mov r8, r1` | **r8 = param_1** (the Init float; no later write to r8 in 0x00692574..0x006926D9) |
| 0x00692640 | `mov r3, r8` | r3 = param_1, passed as `LocalNotifications::Init`'s float (4th arg) |
| 0x0069264A | `blx #0x4bd9a4` | `LocalNotifications::Init(Json const&, RandomGenerator*, float=r8)` |
| 0x0069264E | `ldr r0, [r5]` | **reloads r0**, discarding `LocalNotifications::Init`'s return in r0 |
| 0x006926C8 | `bl #0x693384` | `FUN_00693384(&local)`; its r0 is discarded |
| 0x006926CC | `mov r0, r5` | r0 = this |
| 0x006926CE | `mov r1, r8` | **r1 = param_1** (the `InitInternal` float argument) |
| 0x006926D0 | `blx #0x4bd9b0` | `NeedsManager::InitInternal(float=r8)` |

The Ghidra decompilation (`00692574.c` lines 63-93) chains `fVar3 = DesiredFaceDistortionComponent::Init(...)`, then `fVar3 = LocalNotifications::Init(...)`, then `fVar3 = FUN_00693384(...)`, then `InitInternal(fVar3)`. That chain is a decompiler artefact: the instructions show the float argument to `InitInternal` is the preserved r8, not any return value. `InitInternal` itself copies its incoming r1 straight into `InitReset` (`0x00693450 blx #0x4bd9bc`, r1 untouched) and `InitReset` consumes r1 at 0x006934B0/0x0069358A (see Q7).

**`CozmoEngine::Init` call site (0x004EC9DA..0x004ECA0A):**

| VA | instruction |
| --- | --- |
| 0x004EC9DA | `blx #0x4a4f6c` | `BaseStationTimer::getInstance()` |
| 0x004EC9DE | `blx #0x4a50b0` | `BaseStationTimer::GetCurrentTimeInSeconds() const` -> r0 |
| 0x004EC9E2 | `mov ip, r0` | ip = the BaseStationTimer time |
| 0x004EC9FE | `mov r1, ip` | r1 = that time |
| 0x004ECA0A | `blx #0x4a50bc` | `NeedsManager::Init(this, float=time, ...)` |

**What `LocalNotifications::Init` returns:** nothing meaningful. Its body 0x0068C618..0x0068C7A2 ends at 0x0068C7A0 `add sp, #0x74` / 0x0068C7A2 `pop.w {r4,r5,r6,r7,r8,sb,sl,fp,pc}`; there is no `mov r0, ...` return sequence, and the caller reloads r0 at 0x0069264E. (For the record, it consumes its float argument: 0x0068C61E `vldr s0, [pc, #0x1e0]` = 60.0 at 0x0068C800, 0x0068C622 `vmov s2, r3`, 0x0068C62A `vadd.f32 s0, s2, s0`, 0x0068C638 `vstr s0, [sb, #-4]` -> `LocalNotifications+0x18 = arg + 60.0`.)

**Whether `FUN_00693384` returns anything:** no. It is void. Its body 0x00693384..0x0069340F ends at 0x00693408 `addeq sp, #0x30` / 0x0069340A `popeq {r4,r5,r6,pc}`; no return value is placed in r0, and the call site discards r0 at 0x006926CC. It registers one handler with id 0xD2 (0x0069338C `movs r3, #0xd2`) by calling a virtual through `[*(param[0]) + 0x2c]` and pushing a `shared_ptr<ScopedHandleContainer>` into a vector.

---

## Q2. `NeedsManager::AttemptReadFromDevice` 0x00693690..0x00693792

Signature (from the call site 0x00693466 and the `DeviceHasNeedsState` body): `AttemptReadFromDevice(string const& filename, bool& out)`, this in r0, filename in r1, `bool&` in r2.

**Success sequence (all citations are instructions):**

| order | VA | instruction | meaning |
| ---: | --- | --- | --- |
| 1 | 0x0069369A | `blx #0x4bda04` | `DeviceHasNeedsState(this, filename)` |
| 2 | 0x0069369E | `cmp r0, #1` | |
| 3 | 0x006936A0 | `bne #0x693720` | false -> missing branch |
| 4 | 0x006936A8 | `blx #0x4bda10` | `ReadFromDevice(this, filename, &out)` |
| 5 | 0x006936AC | `cmp r0, #1` | |
| 6 | 0x006936AE | `bne #0x693746` | false -> read-failed branch |
| 7 | 0x006936B0 | `ldrd r0, r1, [r5, #8]` | r0=[this+8], r1=[this+0xC] |
| 8 | 0x006936B6 | `strd r0, r1, [r5, #0x1b8]` | **+0x1B8/+0x1BC = +8/+0xC** (saves the `_DateTime` timestamp) |
| 9 | 0x006936BA | `mov r0, r5` | |
| 10 | 0x006936BC | `movs r1, #0` | r1 = false |
| 11 | 0x006936BE | `blx #0x4bda1c` | `ApplyDecayForTimeSinceLastDeviceWrite(this, false)` |
| 12 | 0x006936C2 | `mov r0, r5` | |
| 13 | 0x006936C4 | `movs r1, #1` | **argument = 1** (`NeedsActionId` value 1) |
| 14 | 0x006936C6 | `blx #0x4bd9d4` | `SendNeedsStateToGame(this, 1)` |
| 15 | 0x006936CA | `ldr r0, [r5, #0x30]` | |
| 16 | 0x006936CC | `adds r0, #1` | |
| 17 | 0x006936CE | `str r0, [r5, #0x30]` | **+0x30 += 1** |
| 18 | 0x006936D0 | `mov r0, r5` | |
| 19 | 0x006936D2 | `blx #0x4bda28` | `SendTimeSinceBackgroundedDasEvent(this)` |
| 20 | 0x006936F2..0x006936F6 | `adr r1,#0xd0` / `add r2,sp,#4` / `blx #0x4a505c` | log "Successfully read file %s from device" |
| 21 | 0x0069371C | `movs r0, #1` | return value 1 |
| 22 | 0x0069371E | `b #0x69378e` | to the epilogue |

**`SendNeedsStateToGame` argument:** r1 = 1 at 0x006936C4. `SendNeedsStateToGame` is `_ZN4Anki5Cozmo12NeedsManager20SendNeedsStateToGameENS0_13NeedsActionIdE`; value 1 is `NeedsActionId` ordinal 1. In `unity/scripts/csharp/Anki.Cozmo/NeedsActionId.cs` ordinal 1 is `Decay` (0 = NoAction). The engine's own enum ordering was not independently decoded in this pass; the numeric value 1 is the source fact.

**What `+0x30` is, and every read/write found** (the whole NeedsManager method set was scanned at instruction level):

| VA | instruction | function | R/W | value |
| --- | --- | --- | --- | --- |
| 0x0069772E | `ldr r1, [r4, #0x30]` | SendTimeSinceBackgroundedDasEvent | R | used as the `"$data"` value (int -> string) |
| 0x00693CC6 | `ldr.w r1, [sl, #0x30]` | WriteToDevice | R | serialised to JSON key `"OpenAppAfterDisconnect"` |
| 0x00699A1C | `str r0, [r4, #0x30]` | ReadFromDevice | W | loaded from JSON key `"OpenAppAfterDisconnect"` (`asInt`) |
| 0x006936CA/0x006936CE | `ldr r0,[r5,#0x30]` / `str r0,[r5,#0x30]` | AttemptReadFromDevice | R/W | +1 after a successful construction-time read |
| 0x006990EC/0x006990F2 | `ldr r0,[r4,#0x30]` / `str r0,[r4,#0x30]` | HandleMessage<SetGameBeingPaused> | R/W | +1 in the unpause branch (`movs r1,#1; adds r0,#1`) |
| 0x00695922 | `str r5, [r4, #0x30]` | OnRobotDisconnected | W | 0 (r5 = 0) |

So `+0x30` is the engine field serialised as `"OpenAppAfterDisconnect"` (see Q4/Q5). It is part of the NeedsState sub-object (NeedsState+0x28); `NeedsState::Init` sets it to 0 (0x0069C04E `stm.w r3,{r0,r1,sl}` with r3=NeedsState+0x20 stores sl=0 at +0x28), and the NeedsManager ctor does not touch offset 0x30 directly (its clears cover +0x1B8..+0x1E1 and +0x1E4..+0x39C). The exact semantic reason it is incremented on a successful read and on unpause is **UNKNOWN** (the key name is the only naming evidence).

**Failure / missing branches and return values:**

| branch | VA | log function | log format string | return |
| --- | --- | --- | --- | --- |
| `DeviceHasNeedsState` != 1 | 0x00693720..0x00693744 | `sChanneledInfoF` (0x00693740), function string "NeedsManager.AttemptReadFromDevice" (0x006937C4) | `"FAILED to FIND file %s on device"` (0x006937E8), arg = filename | 0 (0x0069378C `movs r0,#0`) |
| `ReadFromDevice` != 1 | 0x00693746..0x00693766 | `sChanneledInfoF` (0x00693766), function "NeedsManager.AttemptReadFromDevice" | `"FAILED to read file %s on device"` (0x00693810), arg = filename | 0 (0x0069378C) |
| success | 0x006936F2..0x006936F6 | `sChanneledInfoF`, function "NeedsManager.AttemptReadFromDevice" | `"Successfully read file %s from device"` (0x00C003F4), arg = filename | 1 (0x0069371C) |

The log channel argument in all three is the literal `"Unnamed"` at 0x00BE3FEC (`ldr r0,[pc,#0x15c]` + `add r0,pc` at 0x006936D6/0x006936DE, 0x00693728/0x0069372E and 0x0069374E/0x00693754). `sChanneledInfoF` is `_ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z`.

`DeviceHasNeedsState` 0x00699870 builds `this[0x3C4] + filename` (0x00699878 `add.w r1,r0,#0x3c4`, 0x0069987E `operator+`) and returns `FileUtils::FileExists(path)` (0x00699884).

---

## Q3. `NeedsManager::SendTimeSinceBackgroundedDasEvent`

Entry **0x0069770C** (`_ZN4Anki5Cozmo12NeedsManager33SendTimeSinceBackgroundedDasEventEv`), body to 0x006977F1. No arguments beyond `this`.

| VA | instruction | meaning |
| --- | --- | --- |
| 0x00697716 | `ldr r0, [r5, #0x20]!` (r5=this) | r0 = this+0x20 (TimeLastAppBackgrounded low) |
| 0x0069771A | `ldr r1, [r5, #4]` | r1 = this+0x24 (high) |
| 0x0069771C | `orrs r0, r1` | |
| 0x0069771E | `beq #0x6977ec` | **guard: emit nothing if this+0x20 == 0 && this+0x24 == 0** |
| 0x00697720/0x00697722 | `blx #0x4a7348` | `system_clock::now()` |
| 0x00697726 | `ldrd r6, r8, [r5]` | r6:r8 = the stored timestamp (this+0x20/+0x24) |
| 0x0069772A | `ldrd r7, r5, [sp, #0x30]` | r7:r5 = now |
| 0x0069772E | `ldr r1, [r4, #0x30]` | r1 = the `OpenAppAfterDisconnect` counter |
| 0x00697732/0x00697734 | `blx #0x4a4c18` | `to_string(int)` of the counter |
| 0x00697740 | `add r0, pc` -> `"$data"` (0x00BE44C1) | key |
| 0x00697758..0x0069776E | `operator new(8)` + `strd r1,r2,[r0]` | data vector = one pair `("$data", to_string(this+0x30))` |
| 0x00697772/0x00697776 | `movw r2,#0x4240; movt r2,#0xf` | divisor = 0xF4240 = 1,000,000 |
| 0x00697780..0x00697786 | `subs/sbc` + `__aeabi_ldivmod` | `(now - this+0x20) / 1e6` |
| 0x00697792 | `blx #0x4a6868` | `to_string(long long)` of the elapsed value |
| 0x006977A6 | `adr r0, #0x9c` | event name at 0x00697844 = **`"needs.app_backgrounded_time"`** |
| 0x006977A8 | `add r1, sp, #0x24` | the data vector |
| 0x006977AA | `blx #0x4a50e0` | `Util::sEvent(name, data, elapsed)` |

`Util::sEvent` is `_ZN4Anki4Util6sEventEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_` = `sEvent(char const*, vector<pair<char const*,char const*>> const&, char const*)`. So it emits:

- **event name:** `"needs.app_backgrounded_time"` (0x00697844);
- **data vector:** one pair, key `"$data"` (0x00BE44C1), value `to_string(this+0x30)` — the `OpenAppAfterDisconnect` counter as a decimal string;
- **third string argument:** `to_string((system_clock::now() - this+0x20) / 1000000)` — the elapsed time since `TimeLastAppBackgrounded`, in the timestamp's units divided by 1e6.

---

## Q4. `NeedsManager::ReadFromDevice` 0x006998B4..0x00699BB8

Signature `_ZN4Anki5Cozmo12NeedsManager14ReadFromDeviceERKNSt6__ndk112basic_string...ERb` = `ReadFromDevice(string const& filename, bool& out)`, this in r0, filename in r1, `bool&` in r2 (r4=this, r5=filename, sl=`bool&`).

**Out-parameter clear, read and version gate:**

| VA | instruction | meaning |
| --- | --- | --- |
| 0x006998C8 | `strb.w r0, [sl]` (r0=0 at 0x006998C4) | **`*out = false`** |
| 0x006998CC..0x006998D0 | `Json::Value::Value(Json::ValueType)` | builds the local Json |
| 0x006998D4..0x006998DC | `operator+` with `this+0x3C4` + filename | path = this[0x3C4] + "needsState.json" |
| 0x006998E4 | `blx #0x4a82f0` | `DataPlatform::readAsJson(string const&, Json::Value&)` |
| 0x006998E8 | `mov r5, r0` | |
| 0x006998F8 | `cbz r5, #0x69992a` | **readAsJson failure -> 0x0069992A** |
| 0x006998FE..0x00699904 | `operator[]` with key `"_StateFileVersion"` (0x0105C2B8) + `asInt` | version -> sb |
| 0x0069990A | `cmp.w sb, #6` | |
| 0x0069990E | `blt #0x69999c` | version < 6 -> proceed |
| 0x00699910..0x00699924 | `sVerifyFailedReturnFalse` with `"NeedsManager.ReadFromDevice.StateFileVersionIsFuture"` / `"VERIFY(%s): Needs state file version read was %d but app thinks latest version is %d"` (0x00C0044F), args 5 and sb | **version >= 6 rejected** |

**Field loading (key -> field).** The key globals are all built by the static initializer `_INIT_48` 0x004D8DDC..0x004D9142 (addresses are the std::string objects in `.data`):

| key global | key string | VA(s) read | destination | conversion |
| --- | --- | --- | --- | --- |
| 0x0105C2B8 | `_StateFileVersion` | 0x006998FE | sb (local) | `asInt` |
| 0x0105C2C8 | `_DateTime` | 0x0069999C..0x006999A6 | this+8/+0xC | `asLargestInt * 1e6` (`umull`/`mla` at 0x006999B8/0x006999BC, then 0x006999C0 `str r0,[r5,#8]!`, 0x006999C4 `str r1,[r5,#4]`) |
| 0x0105C2D8 | `_SerialNumber` | 0x00699A16..0x00699A24 | this+0x34 | `asUInt` |
| 0x0105C2E8 | `CurNeedLevel` | 0x00699A6E..0x00699A9C | per-need map at this+0x40, node+0x14 | `asInt`, `vcvt.f32.s32`, `vdiv.f32` by 100000.0 (s16 literal 0x47C35000 at 0x00699C88) |
| 0x0105C2F8 | `PartIsDamaged` | 0x00699AD8..0x00699AF6 | per-part map at this+0x4C, node+0x14 | `asBool`, `strb` |
| 0x0105C308 | `CurNeedsUnlockLevel` | 0x00699A2C..0x00699A38 | this+0x54 | `asInt` |
| 0x0105C318 | `NumStarsAwarded` | 0x00699A3C..0x00699A48 | this+0x58 | `asInt` |
| 0x0105C328 | `NumStarsForNextUnlock` | 0x00699A4C..0x00699A58 | this+0x5C | `asInt` |
| 0x0105C338 | `TimeCreated` | 0x00699B74..0x00699B8A | this+0x10/+0x14 | `asLargestInt * 1e6` |
| 0x0105C348 | `TimeLastStarAwarded` | 0x00699B1E..0x00699B40 | this+0x60/+0x64 | `asLargestInt * 1e6` |
| 0x0105C358 | `TimeLastDisconnect` | 0x006999C8..0x006999E2 | this+0x18/+0x1C | `asLargestInt * 1e6` |
| 0x0105C368 | `TimeLastAppBackgrounded` | 0x006999E6..0x006999FC | this+0x20/+0x24 | `asLargestInt * 1e6` |
| 0x0105C378 | `OpenAppAfterDisconnect` | 0x00699A00..0x00699A1C | this+0x30 | `asInt` |
| 0x0105C388 | `ForceNextSong` | 0x00699B46..0x00699B5E | this+0x68 | `asString` -> `UnlockIdFromString` |

**Version-dependent branches:**

| test | VA | effect |
| --- | --- | --- |
| `cmp sb,#6; blt` | 0x0069990A/0x0069990E | v >= 6 rejected (return 0) |
| `cmp sb,#3; blt #0x699a0c` | 0x006999B4/0x006999C6 | v < 3: zero this+0x18/+0x1C and this+0x20/+0x24 (0x00699A0C `strd r0,r0,[r4,#0x18]`, 0x00699A12 `strd r0,r0,[r4,#0x20]`), and this+0x30 = 0 via r0=0 at 0x00699A1C |
| v >= 3 | 0x006999C8..0x00699A1C | load TimeLastDisconnect, TimeLastAppBackgrounded, OpenAppAfterDisconnect |
| `cmp sb,#1; ble #0x699b90` | 0x00699B18/0x00699B1C | v <= 1: zero this+0x60/+0x64 (0x00699B90), this+0x68 (0x00699B96), this+0x10/+0x14 (0x00699B9A) |
| v >= 2 | 0x00699B1E..0x00699B40 | load TimeLastStarAwarded -> this+0x60/+0x64 |
| `cmp sb,#4` at 0x00699B30; `blt #0x699b96` at 0x00699B44 | 0x00699B30/0x00699B44 | v 2 or 3: this+0x68 = 0 (0x00699B96) then this+0x10 = 0 |
| v >= 4 | 0x00699B46..0x00699B5E | load ForceNextSong -> this+0x68 |
| `cmp sb,#5; blt #0x699b9a` | 0x00699B6E/0x00699B72 | v 4: this+0x10/+0x14 = 0 (0x00699B9A) |
| v >= 5 | 0x00699B74..0x00699B8E | load TimeCreated -> this+0x10/+0x14 |

**Out Boolean set, dirty flag / bracket update, return:**

| VA | instruction | meaning |
| --- | --- | --- |
| 0x00699BA0 | `movs r0, #1` / `strb.w r0, [sl]` | **`*out = true`** (reached by every non-error version) |
| 0x00699BA6 | `movs r5, #1` | return value 1 |
| 0x00699BA8 | `strb.w r5, [r4, #0x90]` | dirty flag `NeedsManager+0x90` (= NeedsState+0x88) = 1 |
| 0x00699BAC | `ldr r0, [sp, #8]` | r0 = NeedsManager+8 (NeedsState) |
| 0x00699BAE | `add.w r1, r4, #0x140` | r1 = NeedsManager+0x140 (NeedsConfig+0x18) |
| 0x00699BB2 | `blx #0x4bda34` | `NeedsState::UpdateCurNeedsBrackets(this+8, this+0x140)` |
| 0x0069998E | `mov r0, r5` | return (1) |

**Error returns (both 0):**
- `readAsJson` failure: 0x006998F8 `cbz r5,#0x69992a`; at 0x0069992A..0x00699948 logs via `sErrorF` function `"NeedsManager.ReadFromDevice.ReadStateFailed"` (0x00699C18) and format `"Failed to read %s"` (0x00699C44) with the filename; 0x00699986 `movs r5,#0` -> 0x0069998E returns 0.
- version >= 6: `sVerifyFailedReturnFalse` at 0x00699924, then 0x00699986 `movs r5,#0` -> 0.

---

## Q5. `NeedsManager::WriteToDevice` 0x00693BB0

Signature `_ZN4Anki5Cozmo12NeedsManager13WriteToDeviceEb` = `WriteToDevice(bool refreshDateTime)`.

| VA | instruction | meaning |
| --- | --- | --- |
| 0x00693BC0 | `blx #0x4a7348` | `system_clock::now()` |
| 0x00693BC4..0x00693BCC | `cmp r5,#1` / `ldrdeq` / `strdeq r0,r1,[sl,#8]` | if `refreshDateTime`: this+8/+0xC = now |
| 0x00693BD4 | `Json::Value::Value(Json::ValueType)` | builds the root object |
| 0x00693BD8..0x00693BEE | key `_StateFileVersion` (0x0105C2B8) = `Value(int 5)` | **version 5** |
| 0x00693BF8..0x00693C22 | key `_DateTime` (0x0105C2C8) = `Value(long long)` this+8/+0xC divided by 1e6 | |
| 0x00693C2C..0x00693C56 | key `TimeCreated` (0x0105C338) = this+0x10/+0x14 / 1e6 | |
| 0x00693C60..0x00693C8A | key `TimeLastDisconnect` (0x0105C358) = this+0x18/+0x1C / 1e6 | |
| 0x00693C94..0x00693CBC | key `TimeLastAppBackgrounded` (0x0105C368) = this+0x20/+0x24 / 1e6 | |
| 0x00693CC6..0x00693CDC | key `OpenAppAfterDisconnect` (0x0105C378) = `Value(int)` this+0x30 | |
| 0x00693CE6..0x00693CFC | key `_SerialNumber` (0x0105C2D8) = `Value(unsigned int)` this+0x34 | |
| 0x00693D06..0x00693D1C | key `CurNeedsUnlockLevel` (0x0105C308) = `Value(int)` this+0x54 | |
| 0x00693D26..0x00693D3C | key `NumStarsAwarded` (0x0105C318) = `Value(int)` this+0x58 | |
| 0x00693D46..0x00693D5C | key `NumStarsForNextUnlock` (0x0105C328) = `Value(int)` this+0x5C | |
| 0x00693D66..0x00693DC0 | key `CurNeedLevel` (0x0105C2E8), object: per node of the map at this+0x40, key `EnumToString(NeedId)`, value `int(node+0x14 * 100000.0 + 0.5)` (0x00693D82 `vmul`, 0x00693D8E `vadd 0.5`, 0x00693D92 `vcvt.s32`) | |
| 0x00693DE4..0x00693E24 | key `PartIsDamaged` (0x0105C2F8), object: per node of the map at this+0x4C, key `EnumToString(RepairablePartId)`, value `Value(bool)` node+0x14 | |
| 0x00693E48..0x00693E76 | key `TimeLastStarAwarded` (0x0105C348) = this+0x60/+0x64 / 1e6 | |
| 0x00693E7A..0x00693E9C | key `ForceNextSong` (0x0105C388) = `Value(char const*)` `EnumToString(UnlockId)` of this+0x68 | |
| 0x00693EA0..0x00693EDA | builds the literal `"nurture/"` on the stack (0x00693EBC/0x00693EC8, size byte 0x10 at 0x00693ECE) and `operator+`s it with the global filename `"needsState.json"` (0x0105C2AC, loaded at 0x00693EAC/0x00693EB4) | path string = `"nurture/needsState.json"` |
| 0x00693EDE..0x00693EE6 | `r1=sp+0x1c` (Scope, zeroed at 0x00693EB0/0x00693EB2), `r2=sp+0x10` (path), `r3=sp+0x170` (Json), `r0=CozmoContext[8]` (DataPlatform), `blx #0x4a6514` | **`DataPlatform::writeAsJson(Scope const&, string const&, Json::Value const&)`** |
| 0x00693F08..0x00693F18 | on r4==0 logs `sErrorF` and sets `_errG` | write-failure path |
| 0x00693F56 | `Json::Value::~Value()` | |

**Confirmation:** the write is `Anki::Util::Data::DataPlatform::writeAsJson` (`_ZNK4Anki4Util4Data12DataPlatform11writeAsJsonERKNS1_5ScopeERKNSt6__ndk112basic_string...ERKN4Json5ValueE`) via PLT 0x004A6514 at 0x00693EE6. The only engine constants in the path are the subpath `"nurture/"` and the filename `"needsState.json"`; the write resolves the path through `DataPlatform::pathToResource` inside `writeAsJson` (0x0084C328 calls pathToResource and then `FileUtils::CreateDirectory` + a file open). **Confirmed: the file location is host business, as pass 1 A3 says.** Note the read path uses the 2-arg `DataPlatform::readAsJson(string const&, Json&)` with `this[0x3C4] + filename`, where `this[0x3C4]` was produced in the ctor by `pathToResource(scope=0, "nurture/")` at 0x006921D8 (r1=DataPlatform, r2=scope at sp+0x10, r3="nurture/" at sp); the write path passes `writeAsJson(scope=0, "nurture/needsState.json", value)`, which resolves the same subpath through pathToResource. The engine constant is the filename; the directory resolution is the host DataPlatform.

---

## Q6. `LocalNotifications::Generate` 0x0068CA9C at the `InitInternal` tail

`InitInternal` reaches it by tail branch: 0x0069348A `ldr.w r0, [r4, #0x1b0]` (the `LocalNotifications*`), 0x00693492 `b.w #0x8cd8ac` -> thunk 0x008CD8AC -> body 0x0068CA9C. Signature `_ZN4Anki5Cozmo18LocalNotifications8GenerateEv`.

**Feature gate:** 0x0068CAB6 `ldr.w r0, [r8]` (r8 = LocalNotifications), 0x0068CABA `ldr r0, [r0, #0x10]` (the `CozmoFeatureGate*`), 0x0068CABC `movs r1, #0xb`, 0x0068CABC `blx #0x4b1fb0` `CozmoFeatureGate::IsFeatureEnabled(FeatureType)`; 0x0068CAC0 `cmp r0,#1`; 0x0068CAC2 `bne.w #0x68ccc8` (return without doing anything). **The gate is FeatureType 0xb = `LocalNotifications`** (unity `Anki.Cozmo.FeatureType.cs`: Invalid=0, AndroidConnectionFlow=1, SpeedTapMultiPlayer=2, CodeLabGame=3, PeekABoo=4, SparksGatherCubes=5, Hiccups=6, GuardDog=7, Bouncer=8, Laser=9, Singing=10, LocalNotifications=11).

**Side effects (whole body read; no store to NeedsManager and no DataPlatform call):**

| VA | instruction | meaning |
| --- | --- | --- |
| 0x0068CAD4/0x0068CAD6 | `MessageEngineToGame(ClearNotificationCache&&)` ctor | builds the message |
| 0x0068CADA/0x0068CADE | `mov r0,r5; mov r1,r6; blx r7` | dispatches it through the ExternalInterface (`r7 = [*r5 + 0x1c]`) |
| 0x0068CAE6/0x0068CAEA | `NeedsManager::GetCurNeedsState()` | read-only |
| 0x0068CAFC | `NeedsState::GetDecayMultipliers(...)` | read-only |
| 0x0068CB00..0x0068CB24 | `system_clock::now()`, `(now - NeedsState+0x20)/1e6` | elapsed for notification timing |
| 0x0068CB4C..0x0068CCAE | loop over the notification item list (LocalNotifications+0x1c..+0x20): `ShouldBeRegistered`, `DetermineTimeToNotify`, then builds `MessageEngineToGame(CacheNotificationToSchedule&&)` (0x0068CC34) and dispatches it (0x0068CC3C) | outgoing game messages |
| 0x0068CCB8 | `ldr.w r0, [r8, #4]` | NeedsManager* |
| 0x0068CCBC | `vldr s0, [r0, #0x3ac]` | the NeedsManager clock |
| 0x0068CCC0 | `vadd.f32 s0, s0, s16` | s16 = 60.0 (literal at 0x0068CD84 = 0x42700000) |
| 0x0068CCC4 | `vstr s0, [r8, #0x18]` | **LocalNotifications+0x18 = NeedsManager[0x3AC] + 60.0** |

**Answer:** `Generate` does not write anything back to the NeedsManager and does not touch the device. Its only side effects are (a) `MessageEngineToGame` notifications pushed to the ExternalInterface (`ClearNotificationCache`, then one `CacheNotificationToSchedule` per registered item), and (b) `LocalNotifications+0x18 = NeedsManager+0x3AC + 60.0` (the next generate time). It reads `NeedsManager+0x3AC` and the NeedsState only.

---

## Q7. `InitReset`'s float argument and the `+0x3B0 = +0x130 + <arg>` clock

**`InitReset` consumes its own float parameter in r1, not the clock and not r2.** `InitReset` is `_ZN4Anki5Cozmo12NeedsManager9InitResetEfjb` = `(float, unsigned int, bool)`: this=r0, float=r1, uint=r2, bool=r3.

| VA | instruction | meaning |
| --- | --- | --- |
| 0x006934AE | `mov r6, r2` | r6 = the uint argument (-1 from InitInternal) |
| 0x006934B0 | `mov sb, r1` | **sb = the float argument** |
| 0x0069358A | `vmov s0, sb` | s0 = the float argument |
| 0x0069358E | `vldr s2, [r4, #0x130]` | s2 = this+0x130 (the decay period config value) |
| 0x00693598 | `vadd.f32 s0, s2, s0` | s0 = this+0x130 + float arg |
| 0x0069359E | `vstr s0, [r4, #0x3b0]` | **this+0x3B0 = this+0x130 + float arg** |

So the `+0x3B0` seed is `+0x130 +` the same Init time that `InitInternal` receives (Q1). The clock at `+0x3AC` is not used for that store.

**Same clock as `+0x3AC`? Yes.** `+0x3AC` is written only by `NeedsManager::Update`:

| VA | instruction | meaning |
| --- | --- | --- |
| 0x00695CA4 | `ldrb.w r0, [r4, #0x1d5]` | whole-manager paused flag |
| 0x00695CA8 | `str.w r1, [r4, #0x3ac]` | **this+0x3AC = Update's float argument** |
| 0x00695CBA | `vldr s0, [r4, #0x3b0]` | compares the next-decay stamp against the clock |
| 0x00695CCE | `vldr s2, [r4, #0x130]` | |
| 0x00695CD6 | `vadd.f32 s0, s0, s2` | +0x3B0 += +0x130 when the clock passes it |
| 0x00695CDC | `vstr s0, [r4, #0x3b0]` | |

`NeedsManager::Update`'s float comes from `CozmoEngine::Update`, which passes the same `BaseStationTimer::GetCurrentTimeInSeconds()`:

| VA | instruction |
| --- | --- |
| 0x004ED632 | `blx #0x4a4f6c` `BaseStationTimer::getInstance()` |
| 0x004ED636 | `blx #0x4a50b0` `BaseStationTimer::GetCurrentTimeInSeconds() const` |
| 0x004ED63A | `mov r1, r0` |
| 0x004ED63E | `ldr r0, [r0, #0x34]` (this+0x34 = NeedsManager) |
| 0x004ED640 | `blx #0x4a5260` `NeedsManager::Update(float)` |

`NeedsManager::Init` receives the same `BaseStationTimer::GetCurrentTimeInSeconds()` (Q1: 0x004EC9DA..0x004EC9FE). Therefore `+0x3B0` (next decay) is seeded as `+0x130 + now` and `+0x3AC` is `now` on each tick, from the same time base. **The stack's `nextDecay = now + period` is faithful to the source.** (The scan of every NeedsManager method found no other writer of +0x3AC; the only `str` to it is 0x00695CA8.)

---

## Existing records contradicted by the source

None found in this pass. No instruction read contradicts a current M15 record's claim.

## Existing records whose evidence is too weak to keep their status

1. **M15-014 (IMPLEMENTATION_GAP), evidence `NeedsManager::InitInternal 0x00693444..0x0069348E`.** The range stops one instruction before the tail branch `b.w #0x8cd8ac` at 0x00693492 that reaches `LocalNotifications::Generate` (body 0x0068CA9C). The record's `unresolved` correctly calls the construction-time read/write unbuilt, so this is not an overclaim of the whole path, but the cited range does not cover the function's last behaviour-changing step. Extend to 0x00693492.
2. **M15-014, evidence `NeedsManager::AttemptReadFromDevice 0x00693690..0x00693790` and `NeedsManager::ReadFromDevice 0x006998B4..0x00699BB8` and `NeedsManager::WriteToDevice 0x00693BB0`.** These are whole-function range citations only; the record does not enumerate the `+0x30`/`OpenAppAfterDisconnect` increment, the `SendNeedsStateToGame(...,1)` call, the `+0x1B8/+0x1BC` copy, the `SendTimeSinceBackgroundedDasEvent` call, the JSON key set, or the ReadFromDevice version/field matrix. The record's `unresolved` says the construction-time read/write is only a host seam, so it does not claim these are built; but the evidence does not yet pin the field/version contract this pass recovered (Q4/Q5). The new rows below are the specific evidence.
3. **M15-016 (IMPLEMENTATION_GAP), evidence only `NeedsManager::SetPaused 0x00695E04` and `NeedsManager::OnRobotDisconnected 0x00695908`.** Its `unresolved` names `HandleMessage<SetGameBeingPaused> 0x00698F44` for `ApplyDecayForTimeSinceLastDeviceWrite` but the record's evidence list does not include it; that handler is also where `+0x20` (`TimeLastAppBackgrounded`) is written (0x00698FA6) and where `+0x30` is incremented (0x006990EC/0x006990F2) and where `SendTimeSinceBackgroundedDasEvent` is called (0x00699104). The record should name the handler in its evidence before it can settle.
4. **M15-001 (IMPLEMENTATION_GAP), evidence `NeedsManager::Init 0x00692574` and `CozmoEngine::Init 0x004EC9E6..0x004ECA0A`.** It does not state what the Init float argument is or that it seeds `+0x3B0`. Not wrong, but incomplete for the `nextDecay` contract (Q7). The new row below supplies it.

## NEW steps found (no existing record covers them)

| step | what the original does | citation | classification |
| --- | --- | --- | --- |
| Init time threading | `CozmoEngine::Init` passes `BaseStationTimer::GetCurrentTimeInSeconds()` to `NeedsManager::Init`; `Init`'s `param_1` (r8) is passed unchanged to `InitInternal` and `InitReset`, so `+0x3B0 = +0x130 + now` and `+0x3AC = now` are the same clock | 0x004EC9DA..0x004ECA0A; 0x0069257E; 0x006926CE; 0x006934B0; 0x0069358A..0x0069359E; 0x00695CA8 | EXACT_SOURCE (new row) |
| `+0x30` / `OpenAppAfterDisconnect` | counter field: JSON key `OpenAppAfterDisconnect`; +1 on a successful construction-time read and on unpause; reset 0 on disconnect; read as the `"$data"` of the backgrounded-time event; written to JSON | 0x006936CA/0x006936CE; 0x006990EC/0x006990F2; 0x00695922; 0x0069772E; 0x00693CC6; 0x00699A1C | EXACT_SOURCE (new row) |
| `SendTimeSinceBackgroundedDasEvent` | emits `"needs.app_backgrounded_time"` with data `("$data", to_string(+0x30))` and elapsed `to_string((now - +0x20)/1e6)`, guarded on `+0x20|+0x24 != 0` | 0x0069770C..0x006977AA; event string 0x00697844; `"$data"` 0x00BE44C1 | EXACT_SOURCE (new row) |
| `ReadFromDevice` field/version contract | key set and version branches as in Q4; out bool; dirty flag; `UpdateCurNeedsBrackets`; return 1/0 | Q4 table | EXACT_SOURCE (new row) |
| `WriteToDevice` key set and host call | version 5; key set as in Q5; `DataPlatform::writeAsJson(Scope, path, Json)` | Q5 table; PLT 0x004A6514 at 0x00693EE6 | EXACT_SOURCE (new row) |
| `LocalNotifications::Generate` | feature gate 0xb; sends `ClearNotificationCache` and `CacheNotificationToSchedule`; sets `LocalNotifications+0x18 = NeedsManager+0x3AC + 60.0`; no device/NeedsManager write | 0x0068CAB6..0x0068CAC2; 0x0068CAD4..0x0068CC3C; 0x0068CCB8..0x0068CCC4 | EXACT_SOURCE (new row) |

## Open questions the manager must decide

1. Whether the `+0x30`/`OpenAppAfterDisconnect` counter and `SendTimeSinceBackgroundedDasEvent` get their own records (they are live path: `Generate` runs from `InitInternal`, and the event fires on unpause) or are folded into M15-014/M15-016.
2. Whether the construction-time read/write host seam (M15-014's `unresolved`) now needs the exact Q4/Q5 contract as the stack's `Load`/`Save` spec, and whether that is recorded as the same M15-014 or split.
3. The `NeedsActionId` ordinal-1 name (`Decay` per the Unity enum) was not independently confirmed from the engine's own enum table; decide whether to cite the Unity enum (authority 2) or decode the engine's `EnumToString` table.

## UNKNOWN list

- **UNKNOWN:** the semantic reason `+0x30`/`OpenAppAfterDisconnect` is incremented both on a successful construction-time device read and on unpause, and reset on disconnect. Only the key name, the arithmetic sites and the DAS/JSON uses are established.
- **UNKNOWN:** the exact resolved on-disk path of `"needsState.json"`. The engine constants are `"nurture/"` and `"needsState.json"` and both the ctor and `WriteToDevice` use Scope = 0; `DataPlatform::pathToResource` (host) performs the resolution. What would settle it: the host `DataPlatform` implementation (not shipped in `libcozmoEngine.so`).
- **UNKNOWN:** the engine's own `NeedsActionId` ordinal-1 name. The numeric value 1 at 0x006936C4 is source; the name `Decay` is from the Unity enum (authority 2), not decoded from the engine's `EnumToString` table in this pass.
- **UNKNOWN:** the engine's own `FeatureType` ordinal-0xb name. The immediate 0xb is source; the name `LocalNotifications` is from the Unity enum (authority 2).
- **UNKNOWN:** the exact meaning/units of the third `sEvent` string argument in `SendTimeSinceBackgroundedDasEvent` beyond its computation `to_string((now - this+0x20)/1000000)`. `sEvent`'s third parameter is typed `char const*`; its consumer is the DAS sink (not in this binary).
- **UNKNOWN:** whether any function outside the `NeedsManager::*` method set reads/writes `+0x30` or `+0x20`. The scan covered every `NeedsManager::*` body listed in `index.tsv` with size > 12 at instruction level. A whole-image sweep was not performed; the fields are private to NeedsManager.
