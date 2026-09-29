# R-M15 gap-1 extraction: NeedsManager construction-time device read/write, and the +0x1F0/+0x214 pause fields and the ConnectToRobot edge

Scope: M15-014 (construction-time device read/write) and M15-016 (the +0x1F0/+0x214 fields and the ConnectToRobot edge). Read-only. All citations are Thumb VAs in `resources/lib/armeabi-v7a/libcozmoEngine.so`; the Ghidra decompilation under `re-analysis/decomp/libcozmoEngine/` was used only to locate entries.

ABI note: this build uses the soft-float (softfp) AAPCS, so `float` arguments arrive in core registers, not in `s0`. Example: `NeedsManager::InitInternal` receives its float in r1 (NeedsManager::Init sets `mov r1, r8` at 0x006926CE) and `InitReset` reads its own float argument back with `vmov s0, sb` at 0x0069358A. This matters for the argument lists below.

Function entries (from `re-analysis/decomp/libcozmoEngine/index.tsv`, addresses verified in the `.so`):

| function | entry | size |
| --- | --- | --- |
| NeedsManager::InitInternal | 0x00693444 | 78 (ends 0x00693492, including the tail branch) |
| NeedsManager::InitReset | 0x006934A8 | 312 (0x006934A8..0x006935E0) |
| NeedsManager::AttemptReadFromDevice | 0x00693690 | 258 (0x00693690..0x00693792) |
| NeedsManager::DeviceHasNeedsState | 0x00699870 | 46 |
| NeedsManager::ReadFromDevice | 0x006998B4 | 772 |
| NeedsManager::SendNeedsLevelsDasEvent | 0x006940B8 | 472 (0x006940B8..0x00694290) |
| LocalNotifications::Generate | 0x0068CA9C | 586 (0x0068CA9C..0x0068CCE6) |
| NeedsState::Init | 0x0069C000 | 292 (0x0069C000..0x0069C124) |
| NeedsManager::InitAfterConnection | 0x00694384 | 26 (0x00694384..0x0069439E) |
| NeedsManager::DetectBracketChangeForDas | 0x00695958 | 632 (0x00695958..0x00695BD0) |
| NeedsManager::SetPaused | 0x00695E04 | 380 (0x00695E04..0x00695F80) |
| NeedsManager::HandleMessage<SetNeedsPauseStates> | 0x00698918 | 602 (0x00698918..0x00698B72) |
| CozmoEngine::HandleMessage<ConnectToRobot> | 0x004ED018 | 260 (0x004ED018..0x004ED11C) |
| _INIT_48 (global string initializer) | 0x004D8DDC | 872 |

---

## Group A

### A1. `NeedsManager::InitInternal` 0x00693444..0x00693492

Full instruction sequence. Every call and its arguments is listed.

| VA | instruction | note |
| --- | --- | --- |
| 0x00693444 | `push {r4, r5, r7, lr}` | |
| 0x00693446 | `mov.w r2, #-1` | r2 = 0xFFFFFFFF (InitReset param_2) |
| 0x0069344A | `movs r3, #0` | r3 = 0 (InitReset param_3 / "after onboarding skipped" gate) |
| 0x0069344C | `mov r4, r0` | r4 = this |
| 0x0069344E | `movs r5, #0` | r5 = 0 (used for the two flag clears) |
| 0x00693450 | `blx #0x4bd9bc` | **InitReset(this, float=r1, param_2=-1, param_3=0)** |
| 0x00693454 | `ldr r1, [pc, #0x40]` | r1 = 0x009c8e48 (base for the +pc add) |
| 0x00693456 | `addw r2, r4, #0x1cb` | r2 = this + 0x1CB (the bool& out-flag) |
| 0x0069345A | `mov r0, r4` | r0 = this |
| 0x0069345C | `strb.w r5, [r4, #0x1cb]` | **this[0x1CB] = 0** |
| 0x00693460 | `add r1, pc` | r1 = 0x0105C2AC (the global std::string "needsState.json") |
| 0x00693462 | `strb.w r5, [r4, #0x1c9]` | **this[0x1C9] = 0** |
| 0x00693466 | `blx #0x4bd9c8` | **AttemptReadFromDevice(this, r1=0x0105C2AC, r2=this+0x1CB)** |
| 0x0069346A | `cmp r0, #0` | test the returned 0/1 |
| 0x0069346C | `strb.w r0, [r4, #0x1c9]` | **this[0x1C9] = (byte)result** |
| 0x00693470 | `bne #0x69347a` | skip SendNeedsStateToGame unless result == 0 |
| 0x00693472 | `mov r0, r4` | |
| 0x00693474 | `movs r1, #0` | |
| 0x00693476 | `blx #0x4bd9d4` | **SendNeedsStateToGame(this, 0)** |
| 0x0069347A | `mov r0, r4` | |
| 0x0069347C | `movs r1, #1` | |
| 0x0069347E | `blx #0x4bd9e0` | **WriteToDevice(this, true)** |
| 0x00693482 | `adr r1, #0x18` | r1 = 0x0069349C, the literal `"app_start"` (bytes `61 70 70 5f 73 74 61 72 74 00` at 0x0069349C) |
| 0x00693484 | `mov r0, r4` | |
| 0x00693486 | `blx #0x4bd9ec` | **SendNeedsLevelsDasEvent(this, "app_start")** |
| 0x0069348A | `ldr.w r0, [r4, #0x1b0]` | r0 = the LocalNotifications* held at this+0x1B0 |
| 0x0069348E | `pop.w {r4, r5, r7, lr}` | restore, lr = InitInternal's return address |
| 0x00693492 | `b.w #0x8cd8ac` | **tail branch to the LocalNotifications::Generate thunk (0x008CD8AC -> body 0x0068CA9C), r0 = LocalNotifications*** |

Answers to the A1 questions:

- **After `WriteToDevice(this,true)`** the function does call `SendNeedsLevelsDasEvent` with a reason string, and it does call `LocalNotifications::Generate`. The reason is the inline literal at 0x0069349C = `"app_start"` (set by `adr r1, #0x18` at 0x00693482); `Generate` is the tail branch `b.w` at 0x00693492, not a `blx`. The record's evidence range (`InitInternal 0x00693444..0x0069348E`) stops one instruction before that tail branch (see "weak evidence" below).
- **The out-flags clear and the result store are confirmed**: `strb.w r5, [r4, #0x1cb]` at 0x0069345C (r5 = 0), `strb.w r5, [r4, #0x1c9]` at 0x00693462, and `strb.w r0, [r4, #0x1c9]` at 0x0069346C with r0 = AttemptReadFromDevice's return.
- **+0x1CA is not touched**: no instruction in 0x00693444..0x00693492 references `#0x1ca` (nor does the decompilation of the function). (+0x1CA is set elsewhere, in FinishReadFromRobot, per M15-018's evidence; that is not this function.)

### A2. `NeedsManager::InitReset` 0x006934A8..0x006935E0

**"After onboarding skipped" branch condition.** The branch is taken when the 4th integer argument (r3, the decompiler's `in_r3`) is 1 **and** this[0x1C8] != 0:

| VA | instruction |
| --- | --- |
| 0x006934B4 | `cmp r3, #1` |
| 0x006934B6 | `bne #0x693558` (else) |
| 0x006934B8 | `ldrb.w r0, [r4, #0x1c8]` |
| 0x006934BC | `cmp r0, #0` |
| 0x006934BE | `beq #0x693558` (else) |

From `InitInternal` the gate is 0 (`movs r3, #0` at 0x0069344A), so `InitInternal` always takes the else branch. The "bool argument" is r3: the decompiler declares it separately as `in_r3` and the declared `param_3` is r2 (the value later handed to `NeedsState::Init`).

**Onboarding-skipped branch (0x006934C0..0x00693556).** Saves this[0x18..0x24], calls `FUN_00695870(this+8, this+0x98)` (0x006934D2), restores this[0x18..0x24], then reads `NeedsState::GetNeedLevel(this+8, 0/1/2)` (0x006934F4, 0x006934FE, 0x00693508), converts the three to double and calls `Util::sChanneledInfoF` at 0x00693530 with tag `"NeedsManager.InitReset"` (0x00693618) and the format string at 0x00693630.

**Log string confirmed:** the format string at 0x00693630 is exactly `After onboarding skipped, needs levels at %f, %f, %f` (bytes `41 66 74 65 72 ...`). It is loaded by `adr r3, #0x104` at 0x00693528 (r3 = 0x00693630).

**Else branch (0x00693558..0x00693588) — `NeedsState::Init` call and arguments:**

| VA | instruction | note |
| --- | --- | --- |
| 0x00693558 | `add.w r7, r4, #0x128` | r7 = this + 0x128 (NeedsConfig) |
| 0x0069355C | `add.w r5, r4, #8` | r5 = this + 8 (the NeedsState) |
| 0x00693560 | `ldrd r1, r0, [r4, #0x1a8]` | shared_ptr<StarRewardsConfig> from this[0x1A8]/[0x1AC] |
| 0x00693564 | `cmp r0, #0` | |
| 0x00693566 | `strd r1, r0, [sp, #0x1c]` | copy to a local shared_ptr |
| 0x0069356A | `beq #0x693570` | |
| 0x0069356C | `blx #0x4a5014` | `__shared_weak_count::__add_shared` |
| 0x00693570 | `ldr r0, [r4]` | r0 = *(this) = CozmoContext* |
| 0x00693572 | `ldr r0, [r0, #0x14]` | r0 = the RandomGenerator* |
| 0x00693574 | `add r3, sp, #0x1c` | r3 = &shared_ptr |
| 0x00693576 | `str r0, [sp]` | 5th (stack) argument = RandomGenerator* |
| 0x00693578 | `mov r0, r5` | r0 = this + 8 |
| 0x0069357A | `mov r1, r7` | r1 = this + 0x128 |
| 0x0069357C | `mov r2, r6` | r2 = incoming r2 (from InitInternal: -1/0xFFFFFFFF) |
| 0x0069357E | `blx #0x4bd9f8` | **NeedsState::Init(this+8, this+0x128, r2, &shared_ptr<StarRewardsConfig>, RNG)** |
| 0x00693582 | `ldr r0, [sp, #0x20]` | |
| 0x00693584 | `cbz r0, #0x69358a` | |
| 0x00693586 | `blx #0x4a4ef4` | `__release_shared` |

**The store `+0x3B0 = +0x130 + <float arg>`** (note: the second term is the passed float argument in r1, not the clock +0x3AC):

| VA | instruction |
| --- | --- |
| 0x0069358A | `vmov s0, sb` (sb was set from r1 at 0x006934B0) |
| 0x0069358E | `vldr s2, [r4, #0x130]` |
| 0x00693598 | `vadd.f32 s0, s2, s0` |
| 0x0069359E | `vstr s0, [r4, #0x3b0]` |

**Per-need loop (0x006935A2..0x006935CC).** Registers: r2 (the float-array byte offset) = 0,4,8, looping until r2 == 0xC; r1 (the byte-flag offset) = 0x1DC,0x1DD,0x1DE.

| VA | instruction | written field (for r2 = 0 / 4 / 8) |
| --- | --- | --- |
| 0x006935A2 | `adds r3, r4, r2` | |
| 0x006935A4 | `ldr.w r7, [r4, #0x3ac]` | r7 = clock |
| 0x006935A8 | `adds r2, #4` | |
| 0x006935AA | `str.w r0, [r3, #0x1fc]` | +0x1FC / +0x200 / +0x204 = 0 |
| 0x006935AE | `cmp r2, #0xc` | loop bound |
| 0x006935B0 | `str.w r7, [r3, #0x1e4]` | +0x1E4 / +0x1E8 / +0x1EC = clock (+0x3AC) |
| 0x006935B4 | `str.w r0, [r3, #0x208]` | +0x208 / +0x20C / +0x210 = 0 |
| 0x006935B8 | `ldr.w r7, [r4, #0x3ac]` | r7 = clock |
| 0x006935BC | `str.w r7, [r3, #0x214]` | +0x214 / +0x218 / +0x21C = clock (+0x3AC) |
| 0x006935C0 | `add.w r3, r4, r1` | |
| 0x006935C4 | `strb r0, [r4, r1]` | +0x1DC / +0x1DD / +0x1DE = 0 |
| 0x006935C6 | `add.w r1, r1, #1` | |
| 0x006935CA | `strb r0, [r3, #3]` | +0x1DF / +0x1E0 / +0x1E1 = 0 |
| 0x006935CC | `bne #0x6935a2` | |

Notable: the loop writes +0x1E4, +0x1FC, +0x208, +0x214 (three floats each) and the six pause-flag bytes +0x1DC..+0x1E1, but **does not write +0x1F0**.

**`__aeabi_memclr4` range:** `add.w r0, r4, #0x244` at 0x006935CE, `mov.w r1, #0x158` at 0x006935D2, `blx #0x4a403c` at 0x006935D6. So `__aeabi_memclr4(this + 0x244, 0x158)` = bytes +0x244..+0x39B inclusive.

### A3. Where the fixed device file path comes from

**File-existence test** — `NeedsManager::DeviceHasNeedsState` 0x00699870 (called from AttemptReadFromDevice at 0x0069369A):

| VA | instruction | note |
| --- | --- | --- |
| 0x00699876 | `mov r2, r1` | r2 = the filename argument |
| 0x00699878 | `add.w r1, r0, #0x3c4` | r1 = this + 0x3C4 (the host directory string) |
| 0x0069987C | `mov r0, r4` | r0 = the output string (sp+4) |
| 0x0069987E | `blx #0x4a835c` | `std::__ndk1::operator+` — result = this[0x3C4] + filename |
| 0x00699882 | `mov r0, r4` | |
| 0x00699884 | `blx #0x4a82e4` | `Anki::Util::FileUtils::FileExists(path)` |
| 0x00699888 | `mov r4, r0` | return the existence result |

The read path uses the same concatenation: `ReadFromDevice` 0x006998B4 constructs `operator+(&local, this+0x3C4, filename)` and calls `Util::Data::DataPlatform::readAsJson` (decomp 006998b4.c lines 34-35).

**Filename argument** — the global `std::string` at 0x0105C2AC, passed by `add r1, pc` at 0x00693460 (after `ldr r1, [pc, #0x40]` = 0x009C8E48 at 0x00693454). It is constructed by the static initializer `_INIT_48` at 0x004D8DDC:

| VA | instruction | note |
| --- | --- | --- |
| 0x004D8DE2 | `ldr r6, [pc, #0x360]` | = 0x00B834B2 |
| 0x004D8DE4 | `movs r0, #0x14` | |
| 0x004D8DE6 | `ldr r1, [pc, #0x360]` | = 0x00771448 |
| 0x004D8DE8 | `movs r2, #0xa` | 10 = strlen("needsState") |
| 0x004D8DEA | `add r6, pc` | r6 = 0x0105C2A0 |
| 0x004D8DEC | `add r1, pc` | r1 = 0x00C4A238 = `"needsState"` |
| 0x004D8DEE | `strb r0, [r6]` | std::string size field = 0x14 (10<<1) |
| 0x004D8DF0 | `adds r0, r6, #1` | |
| 0x004D8DF2 | `blx #0x4a42ac` | memcpy(dest=r6+1, src="needsState", 10) |
| 0x004D8E02 | `strb r4, [r6, #0xb]` | NUL terminator |
| 0x004D8E16 | `add r7, pc` | r7 = 0x0105C2AC |
| 0x004D8E18 | `add r2, pc` | r2 = 0x00BE5869 = `".json"` |
| 0x004D8E1C | `blx #0x4a7318` | `std::__ndk1::operator+` — 0x0105C2AC = "needsState" + ".json" = **"needsState.json"** |
| 0x004D8E26 | `blx #0x4a4024` | `__cxa_atexit` registration |

**Directory** — this+0x3C4 is set in `NeedsManager::NeedsManager` 0x0069210C. At 0x006921A6..0x006921CE the constructor builds the literal `"nurture/"` on the stack (`movw/movt` r2 = 0x2F657275 (`"ure/"`) stored at sp+5, r2 = 0x7472756E (`"nurt"`) stored at sp+1, size byte 0x10 = 8<<1 at sp, NUL at sp+9), then calls `Util::Data::DataPlatform::pathToResource` at 0x006921D8 with r0 = this+0x3C4, r1 = `*(CozmoContext+8)`, r2 = sp+0x10, r3 = sp. The alternate branch at 0x006921EC stores the empty/default path (when the context has no DataPlatform, `FUN_004e02b2(this+0x3C4, DAT_00be3f00, 0)`).

**Plain statement: the file location is host business.** The engine does not hard-code a directory; it resolves `<DataPlatform resource root>/nurture/` through `DataPlatform::pathToResource` and then appends the fixed global filename `"needsState.json"`. Both the directory root and the resulting full path are provided by the host platform (the phone app's DataPlatform), and the read itself is `DataPlatform::readAsJson` / `FileUtils::FileExists`. The only engine constants are the filename string `"needsState.json"` and the resource subpath `"nurture/"`.

### A4. `NeedsState::Init` 0x0069C000..0x0069C124

| VA | instruction | what it resets |
| --- | --- | --- |
| 0x0069C00E | `blx #0x4be070` | `NeedsState::Reset` (body 0x0069BF9C) — destroys and re-initialises the level map (this+0x34), the current-bracket map (this+0x70), the previous-bracket map (this+0x7C) and the repairable-part map (this+0x40); sets this[0x88] = 1 |
| 0x0069C018 | `strd sl, sl, [r5]` | this+0x0, this+0x4 = 0 |
| 0x0069C01C | `blx #0x4a7348` | `system_clock::now` |
| 0x0069C028 | `stm.w r2, {r0, r1, sl}` | this+0x8 timestamp, this+0x10 = 0 |
| 0x0069C02E | `strd sl, sl, [r5, #0x14]` | this+0x14, this+0x18 = 0 |
| 0x0069C032 | `str.w sl, [r5, #0x1c]` | this+0x1C = 0 |
| 0x0069C036 | `blx #0x4a7348` | `system_clock::now` (second) |
| 0x0069C03C-0x0069C04E | `add.w r3, r5, #0x20` / `stm.w r3, {r0, r1, sl}` | this+0x20 timestamp, this+0x28 = 0 |
| 0x0069C048 | `str r7, [r5, #0x64]` | this+0x64 = the NeedsConfig* argument (r1) |
| 0x0069C056 | `strd r4, r2, [r5, #0x2c]` | this+0x2C = arg r2; this+0x30 = stack arg (the RNG) |
| 0x0069C06A..0x0069C0A8 | loop (3x) `blx #0x4bdc8c` + `str.w fp, [r0, #0x14]` | the level map at this+0x34 gets 3 NeedId->float entries, each set to the first inserted value |
| 0x0069C0AE | `strb.w r0, [r4, #0x88]` | this+0x88 = 1 (brackets-dirty flag) |
| 0x0069C0BA | `blx #0x4bda34` | `NeedsState::UpdateCurNeedsBrackets(this, NeedsConfig+0x18)` — recomputes the current-bracket cache and clears this+0x88 |
| 0x0069C0CE..0x0069C0EC | loop (3x) `blx #0x4bdbfc` + `strb.w sl, [r0, #0x14]` | the repairable-part map at this+0x40 gets 3 entries = false |
| 0x0069C0FE-0x0069C102 | `str r4, [r6, #0x68]` / `str r5, [r6, #0x6c]` | StarRewardsConfig shared_ptr at this+0x68/this+0x6C |
| 0x0069C112 | `strd r5, r5, [r6, #0x4c]` | this+0x4C, this+0x50 = 0 |
| 0x0069C116 | `blx #0x4bddd0` | `StarRewardsConfig::GetMaxStarsForLevel(rewards, 0)` |
| 0x0069C11A | `str r0, [r6, #0x54]` | this+0x54 = max stars for level 0 |
| 0x0069C11C | `str r5, [r6, #0x60]` | this+0x60 = 0 |

`NeedsState::Reset` 0x0069BF9C (called at 0x0069C00E) destroys the three maps at this+0x34 (NeedId->float level), this+0x70 (NeedId->NeedBracketId current), this+0x7C (NeedId->NeedBracketId previous), and this+0x40 (RepairablePartId->bool), re-arms each tree sentinel, and sets this[0x88] = 1 (decomp 0069bf9c.c lines 16-48).

### A5. `SendNeedsLevelsDasEvent` and `LocalNotifications::Generate`

**`NeedsManager::SendNeedsLevelsDasEvent` 0x006940B8..0x00694290.** Signature (decomp 006940b8.c): `void SendNeedsLevelsDasEvent(char const* reason)` — this in r0, reason in r1. It formats the three current need levels into a colon-separated string (the `":"` separator is at 0x00695C78, written by the loop at 0x00694196 `adr r1, #0x1b4` + `blx __put_character_sequence`), then emits a DAS event:
- 0x00694212 `adr r0, #0x144` -> r0 = 0x00694358
- 0x00694216 `blx #0x4a50e0` (`Util::sEvent`) with the event name at 0x00694358 = `"needs.needs_levels"` and `"$data"` (0x006941CC) = the reason pointer r1 (param_1).

At the InitInternal site the reason is `"app_start"` (r1 = 0x0069349C, set at 0x00693482). `LocalNotifications::Generate` is not called by SendNeedsLevelsDasEvent; it is the separate tail branch at 0x00693492.

**`LocalNotifications::Generate` 0x0068CA9C..0x0068CCE6.** At the InitInternal site it is entered by the tail branch at 0x00693492 with r0 = `*(this+0x1B0)` (the LocalNotifications object). Its body (decomp 0068ca9c.c): checks `CozmoFeatureGate::IsFeatureEnabled(..., 0xb)` at 0x0068CAB8; if enabled it calls `GetCurNeedsState`/`GetDecayMultipliers` (0x0068CAE4-0x0068CAF4), computes the elapsed time, iterates the notification item list calling `ShouldBeRegistered` (0x0068CB1C) and `DetermineTimeToNotify`, builds and pushes `MessageEngineToGame` notifications, then sets `LocalNotifications+0x18 = NeedsManager[0x3AC] + 60.0` (0x0068CC9C-0x0068CCA2) as the next generate time.

---

## Group B

### B1. The per-need field at `NeedsManager+0x1F0` (three floats, stride 4)

**Semantics.** `+0x1F0[need]` is the engine clock (the value at NeedsManager+0x3AC) at which that need's per-need pause began. It is written when a per-need pause starts (`HandleMessage<SetNeedsPauseStates>`, 0x00698A48) and read when that pause is unwound to compute the need's decay debt (0x00698A18). `SetPaused` shifts it forward by the whole-manager pause duration so the per-need pause debt survives an overall pause (0x00695F38/0x00695F40). It is zeroed at construction and **not** reset by `InitReset` (the InitReset loop skips it); it is overwritten on each pause start.

**Every read/write:**

| VA | instruction | function | R/W | value |
| --- | --- | --- | --- | --- |
| 0x00692264 | `str.w r0, [r7, #0x1f0]` | NeedsManager::NeedsManager (ctor) | W | 0 |
| 0x00698A18 | `vldr s6, [r1, #0x1f0]` | HandleMessage<SetNeedsPauseStates> | R | old pause-start clock |
| 0x00698A48 | `strne.w r1, [r2, #0x1f0]` | HandleMessage<SetNeedsPauseStates> | W | +0x3AC (r1 loaded at 0x00698A40), only when the need was not paused and the message sets its pause bit |
| 0x00695F2C | `vldr s6, [r1, #0x1f0]` | SetPaused | R | |
| 0x00695F38 | `vadd.f32 s6, s0, s6` | SetPaused | R+compute | + elapsed pause |
| 0x00695F40 | `vstr s6, [r1, #0x1f0]` | SetPaused | W | shifted clock |

The ctor also bulk-clears the range with `__aeabi_memclr4(this+0x1e4, 0x1b9)` at 0x00692188, which covers +0x1F0. `InitReset` does not write it (A2). The `HandleMessage<SetNeedsPauseStates>` write/read instructions are inside the `this[0x1D5] == 0` (whole-manager not paused) path.

**Consumers:** `NeedsManager::HandleMessage<SetNeedsPauseStates>` 0x00698918 (read at 0x00698A18, write at 0x00698A48) and `NeedsManager::SetPaused` 0x00695E04 (read at 0x00695F2C, write at 0x00695F40). No other NeedsManager method touches it.

**Lifetime:** initialised to 0 in the constructor; not cleared by `InitReset`/`WipeRobotGameData`; set to the current clock each time a per-need pause begins; only meaningful while that need's pause flag (this[0x1DC+need]) is set. The pause flags themselves are cleared by `InitReset` (A2).

### B2. The per-need field at `NeedsManager+0x214` (three floats, stride 4)

**Semantics.** `+0x214[need]` is the engine clock (+0x3AC) at which that need's bracket last changed. `DetectBracketChangeForDas` computes the DAS event's elapsed value as `now(+0x3AC) - +0x214[need]` (converted to a signed int) and updates `+0x214[need] = now` only when its `force` argument is false. `InitReset` seeds it to +0x3AC, and `SetPaused` shifts it by the whole-manager pause duration.

**Every read/write:**

| VA | instruction | function | R/W | value |
| --- | --- | --- | --- | --- |
| 0x00692274 | `str.w r0, [r7, #0x214]` | NeedsManager::NeedsManager (ctor) | W | 0 |
| 0x006935BC | `str.w r7, [r3, #0x214]` | InitReset | W | +0x3AC (r7 loaded at 0x006935B8) |
| 0x006959D6 | `vldr s18, [r0]` | DetectBracketChangeForDas | R | r0 = (&this[0x214]) + idx*4, computed at 0x006959D2 |
| 0x00695BA2 | `str.w r0, [r1, sl, lsl #2]` | DetectBracketChangeForDas | W | +0x3AC (r0 loaded at 0x00695B9E), only when `force == 0` |
| 0x00695F5E | `vldr s2, [r1, #0x214]` | SetPaused | R | |
| 0x00695F62 | `vadd.f32 s2, s0, s2` | SetPaused | R+compute | + elapsed pause |
| 0x00695F66 | `vstr s2, [r1, #0x214]` | SetPaused | W | shifted clock |

The ctor's `__aeabi_memclr4(this+0x1e4, 0x1b9)` at 0x00692188 also covers +0x214. No other NeedsManager method touches it.

**`DetectBracketChangeForDas` confirmation (0x00695958):**

| VA | instruction | note |
| --- | --- | --- |
| 0x00695968 | `add.w r0, r2, #0x214` | r0 = &this[0x214], saved at [sp+0x1C] |
| 0x006959CC | `vldr s16, [r0, #0x3ac]` | r0 = this; s16 = now |
| 0x006959D2 | `add.w r0, r0, sl, lsl #2` | r0 = &this[0x214] + need*4 |
| 0x006959D6 | `vldr s18, [r0]` | s18 = old +0x214[need] |
| 0x006959FA | `vsub.f32 s0, s16, s18` | elapsed = now - +0x214[need] |
| 0x006959FE | `vcvt.s32.f32 s16, s0` | signed int elapsed (used in the DAS event) |
| 0x00695B96 | `ldr r0, [sp, #0xc]` | r0 = the `force` argument (stored at 0x0069598A) |
| 0x00695B98 | `cbnz r0, #0x695ba6` | skip the update when force != 0 |
| 0x00695B9A | `ldr r0, [sp, #0x18]` | r0 = this |
| 0x00695B9C | `ldr r1, [sp, #0x1c]` | r1 = &this[0x214] |
| 0x00695B9E | `ldr.w r0, [r0, #0x3ac]` | r0 = now |
| 0x00695BA2 | `str.w r0, [r1, sl, lsl #2]` | +0x214[need] = now |

The event name is `"needs.bracket_changed"` (bytes at 0x00695C7C, loaded at 0x00695B6C `adr r0, #0x10c`), emitted by `Util::sEvent` at 0x00695B70 with `"$data"` = `EnumToString(needId)`.

**Consumers:** `NeedsManager::DetectBracketChangeForDas` 0x00695958 (read 0x006959D6, write 0x00695BA2) and `NeedsManager::SetPaused` 0x00695E04 (read 0x00695F5E, write 0x00695F66); `InitReset` seeds it.

### B3. `NeedsManager::SetPaused` 0x00695E04..0x00695F80

**Redundant guard / common store.** 0x00695E0C `ldrb.w r0, [r5, #0x1d5]`; 0x00695E10 `cmp r4, r0`; 0x00695E12 `bne #0x695e66`. If equal, the function logs `"NeedsManager.SetPaused.Redundant"` (0x00695E20) with `"Setting paused to %s but already in that state"` (0x00695E26) and returns at 0x00695F7C. Otherwise 0x00695E6A `strb.w r4, [r5, #0x1d5]` stores the new paused flag, then 0x00695E6E `cmp r4, #1`; `bne #0x695ed0` takes the **unpause** branch when the new state is not 1.

**Pause branch (new state == 1, 0x00695E78..0x00695ECE).**

| VA | instruction | note |
| --- | --- | --- |
| 0x00695EAA | `vldr s0, [r5, #0x3ac]` | now |
| 0x00695EB0 | `vldr s2, [r5, #0x3b0]` | next-decay stamp |
| 0x00695EB6 | `vstr s0, [r5, #0x1d8]` | **+0x1D8 = now (pause start)** |
| 0x00695EBA | `vsub.f32 s2, s2, s0` | |
| 0x00695EBE | `vstr s2, [r5, #0x3b4]` | **+0x3B4 = +0x3B0 - now (remaining)** |
| 0x00695EC2 | `blx #0x4bd9d4` | `SendNeedsStateToGame(this, 0)` (r1 = 0 set at 0x00695EB4) |
| 0x00695ECA | `blx #0x4bd9e0` | `WriteToDevice(this, true)` (r1 = 1 set at 0x00695EC8) |

**Unpause branch (new state != 1, 0x00695ED0..0x00695F6A).** After the log,

| VA | instruction | note |
| --- | --- | --- |
| 0x00695F02 | `vldr s0, [r5, #0x1d8]` | pause start |
| 0x00695F06 | `movs r0, #0` | loop offset = 0 |
| 0x00695F08 | `vldr s2, [r5, #0x3ac]` | now |
| 0x00695F0C | `vldr s4, [r5, #0x3b4]` | remaining |
| 0x00695F10 | `vsub.f32 s0, s2, s0` | **s0 = now - +0x1D8 = elapsed pause** |
| 0x00695F14 | `vadd.f32 s4, s2, s4` | |
| 0x00695F18 | `vstr s4, [r5, #0x3b0]` | **+0x3B0 = now + +0x3B4** |

The per-need loop (r0 = 0, 4, 8; `cmp r0, #0xc` at 0x00695F5C, `bne #0x695f1c` at 0x00695F6A):

| VA | instruction | note |
| --- | --- | --- |
| 0x00695F1C | `adds r1, r5, r0` | r1 = this + need*4 |
| 0x00695F1E | `adds r0, #4` | |
| 0x00695F20 | `vldr s2, [r1, #0x208]` | cooldown end |
| 0x00695F24 | `vldr s4, [r1, #0x1e4]` | last-decay clock |
| 0x00695F28 | `vcmp.f32 s2, #0` | sets flags for the conditional below |
| 0x00695F2C | `vldr s6, [r1, #0x1f0]` | per-need pause start |
| 0x00695F30 | `vmrs apsr_nzcv, fpscr` | |
| 0x00695F34 | `vadd.f32 s4, s0, s4` | |
| 0x00695F38 | `vadd.f32 s6, s0, s6` | |
| 0x00695F3C | `vstr s4, [r1, #0x1e4]` | **+0x1E4 += elapsed (always)** |
| 0x00695F40 | `vstr s6, [r1, #0x1f0]` | **+0x1F0 += elapsed (always)** |
| 0x00695F44-0x00695F4A | `itttt ne` / `vaddne.f32 s2, s0, s2` / `vstrne s2, [r1, #0x208]` | **+0x208 += elapsed only if +0x208 != 0** |
| 0x00695F4E-0x00695F58 | `vldrne s2, [r1, #0x1fc]` / `vaddne.f32 s2, s0, s2` / `vstrne s2, [r1, #0x1fc]` | **+0x1FC += elapsed only if +0x208 != 0** |
| 0x00695F5E | `vldr s2, [r1, #0x214]` | bracket-change clock |
| 0x00695F62 | `vadd.f32 s2, s0, s2` | |
| 0x00695F66 | `vstr s2, [r1, #0x214]` | **+0x214 += elapsed (always)** |

Then 0x00695F6C `ldr.w r0, [r5, #0x1b0]`; 0x00695F70 `mov r1, r4`; 0x00695F72 `blx #0x4bdb84` (`LocalNotifications::SetPaused(this, newState)`); 0x00695F76 `mov r0, r5`; 0x00695F78 `blx #0x4bdb90` (`NeedsManager::SendNeedsPauseStateToGame(this)`).

So the unpause loop is exactly `+0x1E4`, `+0x1F0`, `+0x214` unconditionally, and `+0x208` + `+0x1FC` together only when `+0x208 != 0`; the loop runs three times (need offsets 0/4/8). Confirmed.

### B4. `NeedsManager::InitAfterConnection` 0x00694384..0x0069439E

| VA | instruction | note |
| --- | --- | --- |
| 0x00694384 | `push {r4, lr}` | |
| 0x00694386 | `mov r4, r0` | r4 = this |
| 0x00694388 | `ldr r0, [r4]` | r0 = *(this) = CozmoContext* |
| 0x0069438A | `ldr r0, [r0, #0x20]` | r0 = RobotManager* |
| 0x0069438C | `blx #0x4a52c0` | `RobotManager::GetFirstRobot()` |
| 0x00694390 | `str r0, [r4, #4]` | **+0x4 = the robot pointer** |
| 0x00694392 | `movs r0, #1` | |
| 0x00694394 | `strb.w r0, [r4, #0x3d0]` | **+0x3D0 = 1** |
| 0x00694398 | `strb.w r0, [r4, #0x1d4]` | **+0x1D4 = 1** |
| 0x0069439C | `pop {r4, pc}` | |

Three stores confirmed.

**What the stack flags model.** `NeedsManager::Update` 0x00695C9C passes `*(int *)(this+4) != 0` as the boolean to `ApplyDecayAllNeeds` (decomp 00695c9c.c line 21). So `_robotConnected` models "the robot pointer at +0x4 is non-null", which `InitAfterConnection` sets at 0x00694390. `+0x3D0` is set to 1 here (0x00694394), cleared in `StartReadFromRobot` 0x0069449C at 0x0069453A (`strb.w r0, [r4, #0x3d0]`), and read in `StartWriteToRobot` 0x00695494 at 0x006954AE (`ldrb.w r0, [r4, #0x3d0]`), so `_awaitingRobotData` models +0x3D0. Both are exactly what the two flags model.

**What reads +0x1D4.** `LocalNotifications::ShouldBeRegistered` 0x0068D00C: 0x0068D01A `ldr r0, [r4, #4]` (r4 = LocalNotifications, +4 = the NeedsManager*), 0x0068D01E `ldrb.w r1, [r0, #0x1d4]`, then 0x0068D022 `cbnz r1, #0x68d028` / 0x0068D024 `cmp r2, #1` / 0x0068D026 `beq #0x68d02e` / 0x0068D028 `cmp r2, #2` / 0x0068D02A `bne #0x68d03c` / 0x0068D02C `cbz r1, #0x68d03c`. It gates notification conditions 1 and 2: condition 1 is suppressed when +0x1D4 == 0 and condition 2 is suppressed when +0x1D4 != 0. It is set to 1 by InitAfterConnection (0x00694398) and cleared to 0 by `NeedsManager::Init` 0x00692574 at 0x00692652 (`strb.w r1, [r5, #0x1d4]`). Its exact name/semantics beyond this gate is UNKNOWN (see below).

### B5. `CozmoEngine::HandleMessage<ConnectToRobot>` 0x004ED018..0x004ED11C

| VA | instruction | note |
| --- | --- | --- |
| 0x004ED018 | `push {r4, r5, r7, lr}` | |
| 0x004ED01A | `sub sp, #0x10` | |
| 0x004ED01C | `mov r4, r0` | r4 = engine |
| 0x004ED01E | `mov r5, r1` | r5 = the ConnectToRobot message |
| 0x004ED020 | `ldr r0, [r4, #0x34]` | |
| 0x004ED022 | `movs r1, #1` | |
| 0x004ED024 | `ldr r0, [r0, #0x20]` | r0 = RobotManager* |
| 0x004ED026 | `blx #0x4a5170` | `RobotManager::DoesRobotExist(rm, 1)` |
| 0x004ED02A | `cmp r0, #1` | |
| 0x004ED02C | `bne #0x4ed06c` | not already connected -> 0x004ED06C |
| 0x004ED02E-0x004ED06A | log `"CozmoEngine.HandleMessage.ConnectToRobot.AlreadyConnected"` / `"Robot already connected"`, `b #0x4ed118` | **already-connected returns without InitAfterConnection** |
| 0x004ED06C | `ldr r0, [r4, #0x34]` | |
| 0x004ED06E | `mov r1, r5` | |
| 0x004ED070 | `ldr r0, [r0, #0x20]` | RobotManager* |
| 0x004ED072 | `ldr r0, [r0, #0x60]` | |
| 0x004ED074 | `blx #0x4a517c` | **MessageHandler::AddRobotConnection(mh, message)** |
| 0x004ED078 | `mov r0, r4` | |
| 0x004ED07A | `movs r1, #1` | |
| 0x004ED07C | `blx #0x4a5188` | **CozmoEngine::AddRobot(engine, 1)** |
| 0x004ED080 | `cbz r0, #0x4ed0d0` | r0 == 0 -> success path (0x004ED0D0) |
| 0x004ED082-0x004ED0CE | log `"CozmoEngine.HandleMessage.ConnectToRobot.Fail"` / `"Failed to connect to robot!"`, `uRam0106dd34 = 1`, optional `sDebugBreakOnError` | failure path |
| 0x004ED0D0-0x004ED108 | log `"CozmoEngine.HandleMessage.ConnectToRobot.Success"` / `"Connected to robot!"` | success path |
| 0x004ED10A | `ldr r0, [r4, #0x34]` | |
| 0x004ED10C | `ldr r0, [r0, #0x34]` | r0 = NeedsManager* |
| 0x004ED10E | `blx #0x4a5194` | **NeedsManager::InitAfterConnection(nm)** — both paths reach here |
| 0x004ED112 | `movs r0, #1` | |
| 0x004ED114 | `blx #0x4a51a0` | **DASPauseUploadingToServer(1)** |
| 0x004ED118 | `add sp, #0x10` / `pop {r4, r5, r7, pc}` | |

Confirmed: the already-connected branch returns at 0x004ED118 without calling InitAfterConnection; otherwise the order is `AddRobotConnection` (0x004ED074), `AddRobot` (0x004ED07C), then `InitAfterConnection` at 0x004ED10E unconditionally (the AddRobot-failed path at 0x004ED082 and the success path at 0x004ED0D0 both jump to 0x004ED10A), then `DASPauseUploadingToServer(1)` at 0x004ED114.

**Serial:** no instruction in 0x004ED018..0x004ED11C loads or stores a serial number; the only use of the message is `AddRobotConnection(mh, param_1)` at 0x004ED072. This handler neither sets nor reads the serial. (Background, authority 6, from `status/G-M15.md`: the serial edge is the later `mfgId` tag-0xED callback; I did not re-trace that in this pass.)

---

## Existing records contradicted by the source

None found. No instruction contradicts a current M15 record's claim. The points below are partial-evidence / wording issues, not contradictions.

## Existing records whose evidence is too weak for their status

1. **M15-014, evidence `NeedsManager::InitInternal 0x00693444..0x0069348E`.** The range stops before the tail branch at 0x00693492 that calls `LocalNotifications::Generate` (thunk 0x008CD8AC, body 0x0068CA9C). The record's `unresolved` correctly says the construction-time read/write is unbuilt, so this is not an overclaim of the whole path, but the cited range does not cover the function's last behaviour-changing step. Extend to 0x00693492 and cite the `adr`/`blx` at 0x00693482/0x00693486 and the `b.w` at 0x00693492.

2. **M15-016, evidence `['NeedsManager::SetPaused 0x00695E04', 'NeedsManager::OnRobotDisconnected 0x00695908']`.** The +0x1F0 and +0x214 fields are consumed by `HandleMessage<SetNeedsPauseStates>` 0x00698918 (0x00698A18 read, 0x00698A48 write) and `DetectBracketChangeForDas` 0x00695958 (0x006959D6 read, 0x00695BA2 write), neither of which is in the record's evidence. The record's `unresolved` also says "the unpause shift omits +0x1F0/+0x214 (both consumers unbuilt)": the source at SetPaused does shift both (0x00695F38/0x00695F40 and 0x00695F62/0x00695F66); the stack omits them. This is a stack gap, not a source claim, but the record should name the two source consumers before it can settle.

3. **Framing correction (for the manager, not a record):** the question's A2 phrase "+0x3B0 = +0x130 + now" is not what the source does. At 0x00693598/0x0069359E the addend is the function's float argument in r1 (`vmov s0, sb` at 0x0069358A, `sb` set from r1 at 0x006934B0), not the clock +0x3AC. The clock is +0x3AC; `InitInternal` passes NeedsManager::Init's float through to InitReset. Do not record "+0x130 + now".

## Open questions the manager must decide

1. **+0x1D4's exact semantics.** Established: set to 1 by `InitAfterConnection`, cleared by `NeedsManager::Init`, read by `LocalNotifications::ShouldBeRegistered` to suppress notification condition 1 when 0 and condition 2 when non-zero. Its domain name (e.g. "has the robot been connected since app start") is not in the binary; it is UNKNOWN. M15-016 or M15-001 must decide whether to record it by offset or leave it in `unresolved`.
2. **The construction-time device read/write as a host seam.** The source resolves the directory through `DataPlatform::pathToResource` and reads/writes `"needsState.json"` through `DataPlatform::readAsJson` / `FileUtils::FileExists`; the file location is host business (A3). Whether the stack's `Save`/`Load` seam is an acceptable host substitution (and whether it should be a COMPATIBILITY_POLICY or EQUIVALENT_IMPLEMENTATION record) is a manager/policy decision, not a source fact. M15-014's `unresolved` currently calls it a gap.
3. **The per-serial alternate file** (`AlternateDeviceFilePath`) is out of this pass's scope; A3 covers only the fixed `"needsState.json"` file.
4. **The serial edge** (mfgId tag 0xED) was not re-traced here; B5 only establishes that the ConnectToRobot handler neither sets nor reads a serial.

## UNKNOWN list

- **UNKNOWN:** the exact semantic name of NeedsManager+0x1D4. Only its read site (0x0068D01E in `LocalNotifications::ShouldBeRegistered`), its set site (0x00694398) and its clear site (0x00692652) are established; what it represents is not named in the image.
- **UNKNOWN:** whether any function outside the `0x0069xxxx` NeedsManager methods reads/writes +0x1F0 or +0x214. The search was: all `0x0069xxxx` entries of `index.tsv` (the NeedsManager methods) plus a grep of the whole decompiled corpus for `0x1f0`/`0x214` filtered to NeedsManager files. No other access was found; a fully exhaustive instruction-level sweep of every function in the image was not performed. The fields are private to NeedsManager, and the class's methods are all in the 0x0069xxxx range, so the result is expected to be complete.
- **UNKNOWN:** whether DataPlatform applies any further transformation to the resource subpath string read as `"nurture/"` from the stack bytes at 0x006921A6..0x006921CE (out of scope). The filename itself is certain: `"needsState.json"`.
- **UNKNOWN:** what `InitInternal`'s float argument (r1) means at its call site. It comes from `NeedsManager::Init`'s `fVar3` (the return of `FUN_00693384`, or 0 when there is no RNG; decomp 00692574.c lines 91-93) and is stored into +0x3B0 as `+0x130 + arg`. Its units/meaning were not established in this pass.
- **UNKNOWN:** the exact type/meaning of +0x1CB beyond "the bool& out-flag that AttemptReadFromDevice/ReadFromDevice set true when a device read succeeded" (ReadFromDevice decomp line 32 `*param_2 = false`, line 157 `*param_2 = true`).
