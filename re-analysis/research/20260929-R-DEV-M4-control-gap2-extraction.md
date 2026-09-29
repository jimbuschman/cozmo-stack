# R-DEV M4-control gap 2 extraction (2026-09-29)

Scope: `libcozmoEngine.so` 3.4.0-1204. Read-only extractor pass for job R-DEV, M4-control. Answers the three
questions on M4-018 S5 and M4-019 (S1). Ghidra decompilation was used only as a navigation aid; every claim
below is cited to instructions in the `.so`. Addresses are ELF virtual addresses; a Thumb function entry is
shown as its symbol value (odd) where relevant.

---

## Question 1 (M4-018 S5): the static `ObjectLights` used by `EnableGameLayerOnly(enable = 1)`

**Function.** `Anki::Cozmo::CubeLightComponent::EnableGameLayerOnly(ObjectID const&, bool)`, body
`0x00639904..0x00639B3B` (symbol value `0x00639905`). Callers reach it through the PLT stub `0x004B9360`
(GOT jump slot `0x010473AC` -> `0x00639905`).

**The enable=1 branch calls `SetObjectLights(id, <static>)`:**

- single-object branch (`*(int*)(id+4) != -1`): `0x006399EA: ldr r2,[pc,#0x204]`, `0x006399F0: add r2,pc`,
  `0x006399F2: blx #0x4B9264` (SetObjectLights). The literal at `0x00639BF0` is `0x00A21AAC`; with
  PC=`0x006399F4` the pointer is `0x0105B4A0`.
- all-objects branch (`*(int*)(id+4) == -1`): `0x00639A26: ldr r2,[pc,#0x1D0]`, `0x00639A2C: add r2,pc`,
  `0x00639A2E: blx #0x4B9264`. The literal at `0x00639BF8` is `0x00A21A70`; with PC=`0x00639A30` the pointer
  is `0x0105B4A0`.

**The static is at `0x0105B4A0`.** It lies in `.bss` (section vaddr `0x01059030`, size `0x36F84`), so it has no
file bytes; it is filled by a static constructor `_INIT_36` at `0x004D7EB4` (entry 36 of `.init_array` =
`0x004D7EB5`).

**`_INIT_36` (`0x004D7EB4..0x004D7EE5`):**

| address | instruction | effect |
|---|---|---|
| `0x004D7EB6` / `0x004D7EBA` | `ldr r0,[pc,#0x30]` / `ldr r1,[pc,#0x30]` | literals `0x00B66914` / `0x00B835DE` |
| `0x004D7EBC` / `0x004D7EBE` | `add r0,pc` / `add r1,pc` | r0 = `0x0103E7D4` (GOT slot), r1 = `0x0105B4A0` |
| `0x004D7EC0` | `ldr r0,[r0]` | r0 = `GOT[0x0103E7D4]`; relocation `0x0103E7D4` = `Anki::NamedColors::BLACK` @ `0x00C9744F` |
| `0x004D7EC2` | `ldr r0,[r0]` | r0 = the 4 bytes at `0x00C9744F` = `00 00 00 FF` = `0xFF000000` |
| `0x004D7EC8` | `rev r0,r0` | r0 = `0x000000FF` |
| `0x004D7ECA`, `0x004D7ECE`, `0x004D7ED2`, `0x004D7ED6` | `strd r0,r0,[r1,#0]`, `[r1,#8]`, `[r1,#0x10]`, `[r1,#0x18]` | +0x00..+0x1F = eight `u32` `0x000000FF` |
| `0x004D7EC4` | `strd r2,r2,[r1,#0x78]` (r2 = 0) | +0x78, +0x7C = 0 |
| `0x004D7EDA..0x004D7EE0` | `add.w r0,r1,#0x20`; `movs r1,#0x55`; `blx #0x4A48AC` (`__aeabi_memclr8`) | +0x20..+0x74 = 0 |

**Full content (layout from `ActiveObject::SetLEDs` `0x004E48B0` and `CubeLightComponent::SetObjectLights`
`0x00637E6C`):**

| offset | field | value |
|---|---|---|
| `+0x00` | `onColors[0..3]` (4 x u32) | `0x000000FF` each (NamedColors::BLACK, `0x00C9744F` = `00 00 00 FF`) |
| `+0x10` | `offColors[0..3]` (4 x u32) | `0x000000FF` each |
| `+0x20` | `onPeriods[0..3]` (4 x i32) | 0 |
| `+0x30` | `offPeriods[0..3]` (4 x i32) | 0 |
| `+0x40` | `transitionOnPeriod[0..3]` (4 x i32) | 0 |
| `+0x50` | `transitionOffPeriod[0..3]` (4 x i32) | 0 |
| `+0x60` | `offset[0..3]` (4 x i32) | 0 |
| `+0x70` | `rotation` (u32) | 0 |
| `+0x74` | `makeRelative` (u8) | 0 |
| `+0x78` | `Point2f` used by `MakeStateRelativeToXY` | 0, 0 |

The struct that `HandleMessage<EnableCubeLightsStateTransitionMessages>` copies is `0x75` bytes
(`0x0063A340: movs r2,#0x75`, `0x0063A344: mov r1,r5` with r5 = `0x0105B4A0`, `0x0063A346: blx #0x4B150C`
= `__aeabi_memcpy8`); `SetObjectLights` also reads `+0x70` and `+0x78`, and `_INIT_36` writes `+0x78`, so the
global occupies `0x80` bytes (`0x0105B4A0..0x0105B51F`).

**Effect through `ActiveObject::SetLEDs` (`0x004E48B0..0x004E49AB`):** on each LED both periods are 0, so
`0x004E48C6..0x004E48CE` replaces both colours with 0 and sets the on period to `0x7FFFFFFF` (`0x004E48D8`
`movs`/literal `0x7FFFFFFF`); the LED is set solid off. `SetLEDs` also sets gamma `0x80` at `0x004E49A2`.

> Note on the decompilation: `re-analysis/decomp/libcozmoEngine/004d/004d7eb4.c` prints the eight colour words
> as `0xFFFFFFFF`. That is a Ghidra error. The instructions (`0x004D7EC0..0x004D7ED6`, relocation
> `0x0103E7D4` = `NamedColors::BLACK` @ `0x00C9744F` whose bytes are `00 00 00 FF`) give `0x000000FF`, which
> also matches the M4 report LB2/LB4h (`rev(BLACK) = 0x000000FF`).

**Other users of the same static:**
- `CubeLightComponent::Update` `0x00637904`: when a layer's anim list is empty and `ObjectInfo.gameLayerOnly`
> 0 it calls `SetObjectLights(id, &DAT_0105B4A0)` (P5 row, `0x00637B04..0x00637C06`).
- `HandleMessage<EnableCubeLightsStateTransitionMessages>` `0x0063A2DC`: `memcpy` of `0x75` bytes from
  `0x0105B4A0` at `0x0063A346` when the object has no anim on the layer.

### Callers that reach `EnableGameLayerOnly` with enable = 1

| caller | call site | enable argument |
|---|---|---|
| `HandleMessage<ExternalInterface::EnableLightStates>` body `0x0063A478..0x0063A49D` | `0x0063A48A: ldrb r1,[r1]` (msg byte0); `0x0063A48C: movs r2,#0`; `0x0063A48E: cmp r1,#0`; `0x0063A490: it eq`; `0x0063A492: moveq r2,#1`; `0x0063A496: blx #0x4B9360` | `(msg.byte0 == 0) ? 1 : 0` |
| the registered lambda for game tag `0xBB` (187), body `0x0063AFDC` (vtable `0x0102EDEC`, clone `0x0063AF7C`, invoke stub `0x0063AFB2: adds r0,#4; b.w #0x0063AFDC`) | `0x0063AFE6: blx #0x4B9450` = `Get_<187>`; `0x0063AFF8: ldrb r0,[r0]`; `0x0063B000: it eq`; `0x0063B002: moveq r2,#1`; `0x0063B006: blx #0x4B9360` | `(msg.byte0 == 0) ? 1 : 0` |
| `HandleMessage<ExternalInterface::EnableCubeSleep>` body `0x0063A4A4..0x0063A57D` | `0x0063A56E: ldr r1,[pc,#0x90]`; `0x0063A572: movs r2,#0`; `0x0063A576: blx #0x4B9360` | 0 (never takes enable=1) |

### Is the enable=1 branch on the live path from a game handler?

**Yes.** The `CubeLightComponent` constructor (`0x006370E8`) subscribes ten handlers only when
`Robot::HasExternalInterface` returns 1 (`0x00637114..0x0063713C`). One of them, `FUN_00637784`, registers game
tag `0xBB` with vtable `0x0102EDEC` (`0x006377B2`-region; `local_3a = 0xBB`). Tag `0xBB` is
`ExternalInterface::EnableLightStates`:
- the engine's own builder `MessageGameToEngine::CreateEnableLightStates` sets the tag to `0xBB`
  (`0x0074F490`; also `Set_EnableLightStates` at `0x0074F4B0`/`0x0074F510`), and
- the decompiled app matches: `unity/scripts/csharp/Anki.Cozmo.ExternalInterface/MessageGameToEngine.cs:200`
  (`EnableLightStates = 187`).

The lambda invoked for tag `0xBB` is `0x0063AFDC` (vtable `0x0102EDEC`), and it calls `EnableGameLayerOnly`
with `enable = (msg.byte0 == 0)` at `0x0063B006`. The app sends this message from
`Robot.SetEnableFreeplayLightStates(bool enable, int objectID)` (`unity/scripts/csharp/Robot.cs:1902-1907`).
Calls with `enable: false` (which become engine enable=1) exist on the live app paths, e.g.
`BlockPoolPane.cs:43`, `SettingsCubeStatusPanel.cs:87`, `SettingsConnectToCube.cs:61`,
`PullCubeTabModal.cs:83`, `DroneModeDriveCozmoState.cs:181`, `CodeLabGame.cs:2873`, `NeedsHub.cs:281`,
`NeedsHub_2018.cs:345`, `TestTapPane.cs:41`, `RobotEngineManager.cs:604`.

So the enable=1 branch (static off lights + `StopAllAnimsOnLayer(1)` + `StopAllAnimsOnLayer(2)` +
`gameLayerOnly = 1`) runs when the app disables freeplay light states (`EnableLightStates(enable = false)`),
and the enable=0 branch (restore the default layer anim) runs when it enables them.

`HandleMessage<EnableCubeSleep>` never passes enable=1; it always passes false (`0x0063A572`).

---

## Question 2 (M4-019 S1): every store to `[robot+0x2C0]`

Search: all decompiled functions in the Robot address range (`0050`..`005F`, and `0060`..`006F`) for a store to
`+0x2C0`. The only Robot writers are `Robot::Robot` (`0x0050FBF0`) and `Robot::UpdateFullRobotState`
(`0x0051291C`). (The only other `+0x2C0` store found, `VisionComponent::~VisionComponent` at `0x00652554`, is a
different object.)

| address | value | context | citation |
|---|---|---|---|
| `0x0050FF08` | 0 | `Robot::Robot` ctor. `r1 = 0` set at `0x0050FEFA`; next to `+0x2B0` (frame id) = 0 at `0x0050FF02` and `+0x2B8` = -1 at `0x0050FEF6`. | `str.w r1,[r5,#0x2C0]` |
| `0x00512B9C` | 0 | UFRS. `sb = 0` set at `0x00512B94`; the treads / off-treads-change Delocalize path (Delocalize `blx` at `0x00512BA6`). | `str.w sb,[r4,#0x2C0]` |
| `0x00512EAE` | 0 | UFRS **frame-match path**. `r6 = 0` set at `0x00512EAC`; `0x00512D7A: cmp sl,r7` / `0x00512D7C: bne.w #0x512F14` falls through here after the send-400 block (`0x00512E98..0x00512EA6`). | `str.w r6,[r4,#0x2C0]` |
| `0x00512F1C` | old + 1 | UFRS **frame-mismatch path** (target of `0x00512D7C`). `0x00512F14: ldr.w r0,[r4,#0x2C0]`; `0x00512F1A: adds r0,#1`; store; `0x00512F20: cmp r0,#0x65`; `0x00512F22: blo #0x512FA0`. | `str.w r0,[r4,#0x2C0]` |
| `0x00512F88` | 0 | UFRS mismatch path after the count reaches >= `0x65`: `r1 = 0` set at `0x00512F86`; then Delocalize at `0x00512F96`. | `str.w r1,[r4,#0x2C0]` |

The **origin-miss** path (`ContainsOriginID` returns 0 -> `0x00512EC4..0x00512F12`) and the **history** paths
(`AddRobotStateToHistory` failure and `GetLastStateWithFrameID` failure -> `0x00512C38`) contain **no** store to
`+0x2C0`.

**Conclusion: the counter is per-mismatch-run, not cumulative.** It is set to 0 at construction
(`0x0050FF08`), on every frame-matching state (`0x00512EAE`), on the treads-change Delocalize path
(`0x00512B9C`), and after the `>= 0x65` mismatch Delocalize (`0x00512F88`). It accumulates only across
consecutive frame-mismatched states; any frame-match state breaks the run. An origin-miss or a history-failure
state leaves it untouched, so it neither continues nor resets a run.

The increment is unconditional on the mismatch path; the `cmp r0,#0x65` / `blo` at `0x00512F20..0x00512F22`
only decides whether the `>= 0x65` branch logs, resets to 0 and delocalizes.

---

## Question 3 (M4-019): the `r6 = 1` at `0x00512C38` and the failing call

**Confirmed.** `0x00512C38: movs r6,#1`; `0x00512C3A: mov fp,sb`; `0x00512C3C: b #0x512FAA`.

**The failing call whose return is tested is `Anki::Cozmo::Robot::AddRobotStateToHistory` (the "history add"):**

- `0x00512BEE: blx #0x4A7C48` (PLT for `Robot::AddRobotStateToHistory`).
- `0x00512BF2: mov sb,r0` (save the return).
- `0x00512BF4: cmp.w sb,#0`; `0x00512BF8: beq.w #0x512D1C` (success when the return is 0).
- Return **nonzero** falls through to the `"AddRawOdomStateToHistory failed for timestamp=%d"` warning
  (`0x00512BFC..0x00512C34`), then to `0x00512C38` (`r6 = 1`).

**The same label is also reached from `RobotStateHistory::GetLastStateWithFrameID`:**

- `0x00513088: blx #0x4A7CB4`; `0x0051308C: mov sb,r0`; `0x0051308E: cmp.w sb,#0`;
  `0x00513092: beq #0x5130EE` (success when 0).
- Return nonzero -> `0x005130EA: movs r6,#0`; `0x00513108..0x00513110: cmp r6,#0` / `beq.w #0x512C38`
  -> `r6 = 1`.

**The stats gate must see this.** `0x00512FB0: cmp r6,#0`; `0x00512FB2: bne #0x513054` skips the block that
starts at `0x00512FB4`: `DetectGyroDrift` (`0x00512FBA`), `DetectBias` (`0x00512FC4`),
`UpdateCliffRunningStats` (`0x00512FCE`), `UpdateCliffDetectThreshold` (`0x00512FD6`) and `SendRobotState`.
So yes: when `AddRobotStateToHistory` returns nonzero (or `GetLastStateWithFrameID` fails), `r6 = 1` and the
stats/threshold block is skipped. The same is true of the origin-miss path, which sets `r6 = 1` at
`0x00512F0C` and branches to `0x512FAA`.

---

## Existing records / evidence

**Contradicted by the source.** None of the three findings contradicts a manifest record. (The only wrong
statement found is in the Ghidra decompilation, not in the manifest: `004d/004d7eb4.c` prints the static's
colour words as `0xFFFFFFFF`, but `_INIT_36` writes `rev(NamedColors::BLACK) = 0x000000FF`.)

**Evidence too weak for its status.**
- C10.3 S5 (`0x006399C0..0x00639B36`) records the `EnableGameLayerOnly` branches but not the static's address
  or content; M4-018's `unresolved` already names this gap. This report fills it.
- C10.6 S1 ("r6=0 while `+0x2C0 < 0x65`") is true for frame-mismatched states, but it is not the only input to
  the `r6` gate: `r6 = 1` also on the `AddRobotStateToHistory` failure, the `GetLastStateWithFrameID` failure
  and the origin miss. The stack's stats gate must mirror all of them.

**Open questions for the manager.**
- None blocking on these three items; the static, its init, the counter stores and the `r6` gate are fully read.
