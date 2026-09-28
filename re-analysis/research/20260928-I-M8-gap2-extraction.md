# M8-framework gap pass 2 — the three open questions settled

Agent: cozmo-extractor (read-only). Date: 2026-09-28.
Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF ARMv7/Thumb). Every citation is an address in that file with the instruction at it. `re-analysis/decomp/libcozmoEngine/` was used only to navigate. Raw addresses were disassembled with the existing scratch helper `.scratch/disasm_addr.py` (copied from `re-analysis/tools/disarm.py`).

Symbols used (dynamic symbol values carry no Thumb bit):
`IActionRunner::IActionRunner` = `0x0053fdb0`; `IActionRunner::NextIdTag` = `0x0053fd94`; `IActionRunner::SetTag` = `0x00540098`; `sTagCounter` = `0x01051020`; `sInUseTagSet` = `0x0105a8f4`; `RobotEventHandler::_gameActionTagCounter` = `0x01061018`; `IBehavior::Init` = `0x005bcb54`; `ActionList::QueueAction` = `0x0053d93c`; `ActionList::QueueActionNow` = `0x0053dca0`; `EnumToString(ReactionTrigger)` = `0x0077065c`; `ReactionTriggerFromString` = `0x00770674`.

---

## Question 1 — `IBehavior::Init`'s `action+0x60 > 0x2dc6c0` guard

**What the original does.** `+0x60` is the action's 32-bit tag. There are exactly two writers:

1. **`IActionRunner::IActionRunner` (0x0053fdb0)** assigns the tag from `sTagCounter`:
   - `0x0053fe26 add.w r7,r5,#0x5c` (r7 = &this+0x5c)
   - `0x0053fe54 ldr.w r0,[fp]` (fp = &sTagCounter; r0 = current counter)
   - `0x0053fe58 adds r1,r0,#1`
   - `0x0053fe5c movweq r1,#0xc6c1` / `0x0053fe60 movteq r1,#0x2d` (on wrap, counter = 0x2dc6c1)
   - `0x0053fe64 str.w r1,[fp]` (sTagCounter = old+1)
   - `0x0053fe68 str r0,[r7]` (this+0x5c = old counter)
   - `0x0053fe72 blx __tree<unsigned int>::__emplace_unique_key_args` into `sInUseTagSet`; `0x0053fe76 ldrb.w r0,[sp,#0x1c]` / `0x0053fe7a cbnz r0,#0x53fec4` retries while the tag is already in use
   - `0x0053fec6 ldr r0,[r5,#0x5c]` / `0x0053fec8 str r0,[r5,#0x60]` (this+0x60 = tag)
   - `sTagCounter` initial value read from `.data` `0x01051020` = `0x002dc6c1` (file offset 0x1050020, bytes `c1 c6 2d 00`).
   So every constructed `IActionRunner` has a tag `>= 0x2dc6c1`; the wrap case assigns `0xffffffff`, which is also `> 0x2dc6c0`.
   `NextIdTag` (`0x0053fd94`: `0x0053fd9a ldr r0,[r1]` / `0x0053fd9c adds r2,r0,#1` / `0x0053fda0 movweq r2,#0xc6c1` / `0x0053fda4 movteq r2,#0x2d` / `0x0053fda8 str r2,[r1]`) returns the current counter and increments the same way, but has **no callers** (Ghidra `// callers: (none)` for `0053fd94.c`); the constructor inlines it. It never writes `+0x60`.

2. **`IActionRunner::SetTag` (0x00540098)** overwrites `+0x60` with a caller-supplied tag:
   - `0x005400a8 ldr r0,[r4,#0x18]` / `0x005400ae cmp.w r0,#0x1000000` / `0x005400b2 bne #0x540102` (a warning path if the state is exactly 0x1000000)
   - `0x00540102 mov r6,r4` / `0x00540104 ldr r0,[r6,#0x60]!` (r6 = &this+0x60)
   - `0x0054011e cbz r5,#0x54013e` — a tag argument of **0 is rejected** (error path, `+0x60` unchanged, returns 0)
   - `0x0054013a str r3,[r6]` (this+0x60 = new tag)
   SetTag is called from exactly three places, all in the game/external message path (Ghidra callee lists; `SetTag@004a8ce0` appears only in `00525a34.c`, `00527950.c`, `00527b74.c`):
   - **`RobotEventHandler::HandleActionEvents` (0x00525a34)** — assigns the tag from `RobotEventHandler::_gameActionTagCounter`:
     - `0x00525a9c ldr r1,[r0]` (r0 = &_gameActionTagCounter; r1 = counter)
     - `0x00525a9e adds r1,#1`
     - `0x00525aa0 cmp r1,r2` with `0x00525a8e movw r2,#0x8480` / `0x00525a96 movt r2,#0x1e` (r2 = 0x1e8480 = 2,000,000)
     - `0x00525aa4 movwhi r1,#0x4241` / `0x00525aa8 movthi r1,#0xf` (on overflow, counter = 0x000f4241 = 1,000,001)
     - `0x00525aac str r1,[r0]` / `0x00525ab0 blx SetTag` (tag = counter, 1..2,000,000)
     - `_gameActionTagCounter` is in `.bss` at `0x01061018` and is zero-initialised, so the first tag is 1.
   - **`RobotEventHandler::HandleMessage<QueueSingleAction>` (0x00527950)** — `0x00527a74 ldr r1,[r4]` / `0x00527a78 blx SetTag`; `r4` is the message (`0x00527956 mov r4,r1`), so the tag is the message's first uint32 field.
   - **`RobotEventHandler::HandleMessage<QueueCompoundAction>` (0x00527b74)** — `0x00527c12 ldr r1,[r4]` / `0x00527c16 blx SetTag`; `0x00527b7c mov r4,r1`; same, the message's first uint32 field.

`ICompoundAction::SetProxyTag` (`0x0054f20c`) writes `this+0x94` (`0x0054f218 str r1,[r5,#0x94]!`) and reads `[child+0x60]` (`0x0054f236 ldr r1,[r4,#0x60]`) only to compare; it does **not** write any action's `+0x60`.

**The guard.** `IBehavior::Init` sets its flag when an action tag is `> 0x2dc6c0`:
- current action: `0x005bcbe2 ldr r2,[r3,#0x14]` / `0x005bcbe8 ldr r2,[r2,#0x60]` / `0x005bcbec cmp r2,r6` (r6 = 0x2dc6c0, built by `0x005bcbd0 movw r6,#0xc6c0` / `0x005bcbda movt r6,#0x2d`) / `0x005bcbee it hi` / `0x005bcbf0 movhi r5,#1`
- queued actions: `0x005bcc06 ldr r5,[r7,#8]` / `0x005bcc08 ldr r5,[r5,#0x60]` / `0x005bcc0a cmp r5,r6` / `0x005bcc10 it hi` / `0x005bcc12 movhi r5,#1`
- the warning is taken when the flag is set: `0x005bcbde lsls r2,r5,#0x1f` / `0x005bcbe0 bne #0x5bcc36`.

**Answer.** Yes, there is a real case. Every engine-constructed `IActionRunner` has a tag `>= 0x2dc6c1`, but an action queued through the game/external path has its tag overwritten by `SetTag` with a value in `[1, 2,000,000]` (the `_gameActionTagCounter` path, `0x00525aac`/`0x00525ab0`) or with the app-supplied first field of a `QueueSingleAction`/`QueueCompoundAction` message (`0x00527a74`/`0x00527a78`, `0x00527c12`/`0x00527c16`). Those tags are `<= 0x2dc6c0`, so they do not set the flag. `+0x60` is never zero (the constructor always writes it; `SetTag` refuses 0 at `0x0054011e`). The guard therefore excludes exactly the externally-tagged actions; the `"Initializing %s: %zu actions already in queue"` warning fires only when at least one action carrying an engine (`sTagCounter`) tag is present in some queue.

| what the original does | citation (address + instruction) | classification |
|---|---|---|
| `+0x60` is written only by the `IActionRunner` constructor (from `+0x5c`, which is the current `sTagCounter`) and by `SetTag`; the constructor's tags are always `>= 0x2dc6c1` | `0x0053fec6 ldr r0,[r5,#0x5c]`; `0x0053fec8 str r0,[r5,#0x60]`; `0x0053fe54 ldr.w r0,[fp]`; `0x0053fe5c movweq r1,#0xc6c1`; `0x0053fe60 movteq r1,#0x2d`; `sTagCounter` `.data 0x01051020 = 0x002dc6c1` | EXACT_SOURCE |
| `SetTag` writes a caller tag to `+0x60`, refuses 0 | `0x00540104 ldr r0,[r6,#0x60]!`; `0x0054011e cbz r5,#0x54013e`; `0x0054013a str r3,[r6]` | EXACT_SOURCE |
| `HandleActionEvents` tags game actions from `_gameActionTagCounter`, which starts at 0 and runs 1..2,000,000 | `0x00525a9c ldr r1,[r0]`; `0x00525a9e adds r1,#1`; `0x00525aac str r1,[r0]`; `0x00525ab0 blx SetTag`; `_gameActionTagCounter` `.bss 0x01061018` = 0 | EXACT_SOURCE |
| `QueueSingleAction` / `QueueCompoundAction` set the tag from the message's first uint32 | `0x00527a74 ldr r1,[r4]`; `0x00527a78 blx SetTag`; `0x00527c12 ldr r1,[r4]`; `0x00527c16 blx SetTag` | EXACT_SOURCE |
| the `Init` guard sets its flag only for tags `> 0x2dc6c0`, i.e. engine tags; externally-tagged actions are excluded | `0x005bcbec cmp r2,r6`; `0x005bcbf0 movhi r5,#1`; `0x005bcc0a cmp r5,r6`; `0x005bcc12 movhi r5,#1`; `0x005bcbde lsls r2,r5,#0x1f`; `0x005bcbe0 bne #0x5bcc36` | EXACT_SOURCE |

---

## Question 2 — `ReactionTrigger` numeric values for 0 and 0x14

**What the original does.** `EnumToString(ReactionTrigger)` (`0x0077065c`) rejects `r0 > 0x16` and otherwise indexes a string-pointer table at `0x10337c0`:
- `0x0077065c cmp r0,#0x16`
- `0x0077065e itt hi` / `0x00770660 movhi r0,#0` / `0x00770662 bxhi lr`
- `0x00770664 ldr r1,[pc,#8]` (literal at `0x00770670` = `0x8c3154`)
- `0x00770666 sxtb r0,r0`
- `0x00770668 add r1,pc` -> `r1 = 0x8c3154 + 0x0077066c = 0x10337c0`
- `0x0077066a ldr.w r0,[r1,r0,lsl #2]`

The table at `0x10337c0` (read from the file; entries are string pointers):

| index | string |
|---|---|
| 0 (0x00) | `0xc1ae42 "CliffDetected"` |
| 1 (0x01) | `0xc1ae50 "CubeMoved"` |
| 2 (0x02) | `0xc1ae5a "FacePositionUpdated"` |
| 3 (0x03) | `0xc19f9d "FistBump"` |
| 4 (0x04) | `0xc1ae6e "Frustration"` |
| 5 (0x05) | `0xc1772d "Hiccup"` |
| 6 (0x06) | `0xc12361 "MotorCalibration"` |
| 7 (0x07) | `0xc1ae7a "NoPreDockPoses"` |
| 8 (0x08) | `0xc1ae89 "ObjectPositionUpdated"` |
| 9 (0x09) | `0xc18691 "PlacedOnCharger"` |
| 10 (0x0a) | `0xc1ae9f "PetInitialDetection"` |
| 11 (0x0b) | `0xc1aeb3 "RobotFalling"` |
| 12 (0x0c) | `0xc1aec0 "RobotPickedUp"` |
| 13 (0x0d) | `0xc1aece "RobotPlacedOnSlope"` |
| 14 (0x0e) | `0xc1aee1 "ReturnedToTreads"` |
| 15 (0x0f) | `0xc1aef2 "RobotOnBack"` |
| 16 (0x10) | `0xc1aefe "RobotOnFace"` |
| 17 (0x11) | `0xc1af0a "RobotOnSide"` |
| 18 (0x12) | `0xc1af16 "RobotShaken"` |
| 19 (0x13) | `0xc104a2 "Sparked"` |
| 20 (0x14) | `0xc1258b "UnexpectedMovement"` |
| 21 (0x15) | `0xc1966a "Count"` |
| 22 (0x16) | `0xc1af22 "NoneTrigger"` |

`ReactionTriggerFromString` (`0x00770674`) builds the same map independently: `0x007706a6 add r1,pc` -> `0xc1ae42 "CliffDetected"` with `0x007706c2 strb.w r6,[sp,#0xc]` (r6=0) gives `CliffDetected = 0`; `0x007708be add r1,pc` -> `0xc1258b "UnexpectedMovement"`; its value store is `0x007708e6 strb.w r0,[sp,#0x14c]` (r0=0x14), so `UnexpectedMovement = 0x14`. `Count` string load `0x007708da add r1,pc` -> `0xc1966a` with value store `0x00770902 strb.w r0,[sp,#0x15c]` (r0=0x15). `NoneTrigger` string load `0x007708f4 add r1,pc` -> `0xc1af22` with value store `0x00770914 strb.w r1,[sp,#0x16c]` (r1=0x16).

**Answer.** `0 = CliffDetected`, `0x14 = UnexpectedMovement`. `Count = 0x15` and `NoneTrigger = 0x16`. The gap-1 pass's labels (`0 = NoneTrigger`, `0x14 = Count`) are wrong; this also makes `Resume`'s `"TooManyResumesCliffOrMovement"` special path (`0x005bcf16 cmp r5,#0x14` / `0x005bcf1a cmpne r5,#0`, string at `0x5bd04c`) name `CliffDetected` and `UnexpectedMovement`, which is consistent.

**The table at `0x1031E20` is not the ReactionTrigger table.** It is the `MessageEngineToGame` tag-name table: entry 0 is `0xc12154 "UiDeviceConnected"`, 1 `0xc10569 "AudioCallback"`, 2 `0xc12166 "AdvertisementRegistrationMsg"`, ... 15 `0xc1226a "ObjectMoved"` ...; those names and ordinals match `re-analysis/protocol/MessageEngineToGame_tags.txt` exactly. The M10 report used it for `MessageEngineToGame` tag names in the reaction-strategy factory, which is correct for that purpose; the ReactionTrigger names come from `0x10337C0`.

| what the original does | citation (address + instruction) | classification |
|---|---|---|
| `EnumToString(ReactionTrigger)` indexes the name table at 0x10337c0, guarded to 0..0x16 | `0x0077065c cmp r0,#0x16`; `0x00770668 add r1,pc`; `0x0077066a ldr.w r0,[r1,r0,lsl #2]` | EXACT_SOURCE |
| entry 0 = "CliffDetected"; entry 0x14 = "UnexpectedMovement"; entry 0x15 = "Count"; entry 0x16 = "NoneTrigger" | table `0x10337c0`: `+0x00 -> 0xc1ae42`, `+0x50 -> 0xc1258b`, `+0x54 -> 0xc1966a`, `+0x58 -> 0xc1af22` | EXACT_SOURCE |
| `ReactionTriggerFromString` independently maps the same names to the same values | `0x007706a6`/`0x007706c2`; `0x007708be`/`0x007708e6`; `0x007708da`/`0x00770902`; `0x007708f4`/`0x00770914` | EXACT_SOURCE |
| `0x1031E20` is the `MessageEngineToGame` tag-name table, not ReactionTrigger | table `0x1031E20`: `+0x00 -> 0xc12154 "UiDeviceConnected"`, `+0x3c -> 0xc1226a "ObjectMoved"` | EXACT_SOURCE |

---

## Question 3 — what the `"Initializing %s: %zu actions already in queue"` number counts

**What the original does.** The warning is emitted once, from `0x005bcc36`, after the outer scan set the flag. Its strings and call:
- `0x005bcc7e adr r0,#0x164` -> `0x5bcde4 "IBehavior.Init.ActionsInQueue"`
- `0x005bcc82 adr r2,#0x180` -> `0x5bce04 "Initializing %s: %zu actions already in queue"`
- `0x005bcc84 blx sWarningF`; the behaviour-name string is in `r3` (`0x005bcc46 addeq.w r3,r8,#1` / `0x005bcc4a ldrne r3,[r4,#0x48]`), and the count is the first vararg on the stack (`0x005bcc7c str r0,[sp]`).

The count is produced by a fresh `find(0)` on the action map, not by the queue that triggered the warning:
- `0x005bcc4c ldr r2,[ip,#4]!` (ip = `[robot+0x250]` = `ActionList*`; now ip = &tree+4, r2 = root)
- `0x005bcc58 cmp r2,#0` / `0x005bcc5a bge #0x5bcc64` (search left for the first key >= 0)
- `0x005bcc74 ldr r1,[r0,#0x10]` / `0x005bcc76 cmp r1,#0` / `0x005bcc78 ble #0x5bcd64` (found only when the key == 0)
- if not found, `0x005bcc7a movs r0,#0` (count 0).

The count itself (`0x005bcd64`):
- `0x005bcd64 ldr r1,[r0,#0x14]` (queue's current action)
- `0x005bcd66 ldr r0,[r0,#0x20]` (queue's `std::list<IActionRunner*>` size; the list sentinel is at node+0x18, `__next_` at +0x1c, size at +0x20 — see `AddConcurrentAction` `0x0053dfb2 add.w r1,r5,#0x18` / `0x0053dfbe ldr r1,[r5,#0x20]` / `0x0053dfc2 adds r0,r1,#1` / `0x0053dfc4 str r0,[r5,#0x20]`)
- `0x005bcd68 cmp r1,#0` / `0x005bcd6a it ne` / `0x005bcd6c addne r0,#1`
- `0x005bcd6e b #0x5bcc7c` (pass the count)

Map key 0 is the main queue: `ActionList::QueueActionNow` (`0x0053dca0`) uses `0x0053dcb4 movs r0,#0` / `0x0053dcb8 str r0,[sp,#0xc]` as the emplace key (`0x0053dccc`), as do `QueueAction` (`0x0053daa4 str r5,[sp,#8]`, r5=0), `QueueActionNext`, `QueueActionAtEnd` and `QueueActionAtFront`. `ActionList::AddConcurrentAction` (`0x0053df2c`) uses keys `>= 1` (`0x0053df4a movs r2,#1` / `0x0053df50 str r2,[sp,#8]`, incremented per occupied slot), so the map can hold more than one queue.

**Answer.** The number is one queue's count: **the current action plus the queued actions of the main queue (map key 0)** — `list.size + (current != 0 ? 1 : 0)`. It is not the sum over all queues. Note the count is looked up for key 0 even when the flag was set by a concurrent queue (key >= 1), and it is 0 when key 0 does not exist.

| what the original does | citation (address + instruction) | classification |
|---|---|---|
| warning strings and call | `0x005bcc7e adr r0,#0x164`; `0x005bcc82 adr r2,#0x180`; `0x005bcc84 blx sWarningF`; `0x5bcde4 "IBehavior.Init.ActionsInQueue"`; `0x5bce04 "Initializing %s: %zu actions already in queue"` | EXACT_SOURCE |
| the count source is a fresh `find(0)` on the action map | `0x005bcc4c ldr r2,[ip,#4]!`; `0x005bcc58 cmp r2,#0`; `0x005bcc74 ldr r1,[r0,#0x10]`; `0x005bcc76 cmp r1,#0`; `0x005bcc78 ble #0x5bcd64` | EXACT_SOURCE |
| count = queue list size + 1 when a current action is set | `0x005bcd64 ldr r1,[r0,#0x14]`; `0x005bcd66 ldr r0,[r0,#0x20]`; `0x005bcd68 cmp r1,#0`; `0x005bcd6c addne r0,#1`; `0x005bcc7c str r0,[sp]` | EXACT_SOURCE |
| map key 0 is the main queue | `0x0053dcb4 movs r0,#0`; `0x0053dcb8 str r0,[sp,#0xc]`; `0x0053dccc blx __emplace_unique_key_args<int,...>` | EXACT_SOURCE |
| concurrent queues use keys >= 1, so "all queues" is a real alternative the code does not take | `0x0053df4a movs r2,#1`; `0x0053df50 str r2,[sp,#8]`; `0x0053df80 cmp r3,r0` / `0x0053df84 addne r2,#1` / `0x0053df86 strne r2,[sp,#8]` | EXACT_SOURCE |

---

## Existing records contradicted or weakened

- **`20260928-I-M8-gap1-extraction.md`, B5** labels `0x14 = Count` and `0 = NoneTrigger`. Contradicted: `0 = CliffDetected`, `0x14 = UnexpectedMovement`, `Count = 0x15`, `NoneTrigger = 0x16` (table `0x10337c0`; `ReactionTriggerFromString` `0x00770674`). The numeric control flow in B5 is unaffected, but the names in the record text must be corrected.
- **M8-001** (`EXACT_SOURCE`, "Behaviour lifecycle and the Smart* scope helpers"): its evidence still names only `IBehavior::IsRunnableBase 0x005BD778`, which does not cover `IBehavior::Init` (`0x005bcb54`) or the rest of the lifecycle. The gap-1 report already flagged this; the tag/guard facts above are additional uncovered behaviour. Status is too weak for the record as written (split or re-evidence).
- **M8-003** (`EXACT_SOURCE`, scored selection): its evidence omits the fifth JSON key `runningPenalty` and the `+0x104`/`+0x108`/`+0x110`/`+0x111` semantics (gap-1 finding). Not touched by this pass.
- **M10-derived** already carries the correct ReactionTrigger ordinals ("0 CliffDetected ... 20 UnexpectedMovement, 21 Count, 22 NoneTrigger (0x16)", `re-analysis/inventory/M10-derived.md` line 320) and correctly cites `EnumToString 0x77065C -> table 0x10337C0`; the M10 factory row's "tag names checked against the engine table 0x1031E20" is correct for `MessageEngineToGame` tags, not ReactionTrigger. No contradiction, but the two tables must not be conflated.

## Open questions for the manager

1. **Q1's guard is now exact but its intent is a design question.** The guard distinguishes engine-tagged actions from externally-tagged ones; whether the warning is *meant* to ignore externally-queued actions cannot be read from the instructions. If the record needs an intent statement, that is the manager's call, not the source's.
2. **Q3's concurrent-queue edge.** The warning can be triggered by a key >= 1 concurrent queue while the count is read from key 0 (possibly 0). Whether any shipped path queues a concurrent action before the main queue exists is not settled here; the counting rule itself is exact.
3. **M8-001's split.** The pass establishes `Init`'s tag/guard and the count, but M8-001's evidence is still only `IsRunnableBase`. Recommend splitting the record or adding the addresses listed in the gap-1 "too weak" section plus `0x005bcb54`, `0x005bcbe8`, `0x005bcc4c`/`0x005bcd64`.