# M4 MovementComponent leftover rows (extractor, 2026-10-10)

Source: `resources/lib/armeabi-v7a/libcozmoEngine.so`, Thumb disassembly read directly. Literal-pool strings resolved with
`ldr rX,[pc,#imm]` = `Align(addr+4,4)+imm`, then `add rX,pc` = `+(addr+4)`. Call-site lists come from a linear Thumb sweep of `.text`
(bl/blx/b.w to the PLT stub of the symbol); indirect calls through function pointers would not show, none was seen.

Conventions: `DirectDriveCheckSpeedAndLockTracks(this, float speed, bool& flag, u8 mask, const string& key /*stack+0*/, const string& debugName /*stack+4*/)`,
`LockTracks(this, u8 mask, const string& key, const string& debugName)`. Key-then-name order is proven at `0x0063F0AA..0x0063F0B8`
(`r2=r6` first stack string, `r3=r5` second, `b.w` to LockTracks) and in LockTracks itself (`0x00640098`: first string `r2` goes to tree node+0x10, second `r3` to node+0x1C).

## Row 1. The five MovementComponent constructor strings

Constructor `0x0063DA5C..0x0063DB38`. `r7 = this+0x8C`; `stm.w lr,{r1,r2,r3,r6}` at `0x0063DAEC` with `lr = r7+0x30 = this+0xBC` stores four `const char*`; `strd r5,ip,[r7,#0x40]` at `0x0063DAF4` stores two more at this+0xCC / this+0xD0. The pointers are inline literals in the code stream:

| field | pointer set at | string address | text |
|---|---|---|---|
| +0xBC | `0x0063DAE4 adr r1,#0x88` | `0x0063DB70` | `DirectDriveWheels` |
| +0xC0 | `0x0063DAE6 adr r2,#0x9C` | `0x0063DB84` | `DirectDriveHead` |
| +0xC4 | `0x0063DAC4 adr r3,#0xCC` | `0x0063DB94` | `DirectDriveLift` |
| +0xC8 | `0x0063DAC8 adr r6,#0xD8` | `0x0063DBA4` | `DirectDriveArc` |
| +0xCC | `0x0063DAF2 adr r5,#0xC0` | `0x0063DBB4` | `DirectDriveTurnInPlace` |
| +0xD0 (extra) | `0x0063DADC addw ip,pc,#0xEC` | `0x0063DBCC` | `OnChargerInSDK` |

Bytes +0xB8,+0xB9,+0xBA = 0 (`0x0063DB04..0x0063DB08`), +0xD4 = 0 (`0x0063DB0C`), +0xD5 = 7 (`0x0063DB12`).

StopAllMotors `0x0063FBD8..0x0063FE16`. Gate: any of +0xB8/+0xB9/+0xBA nonzero (`0x0063FBE2..0x0063FBF4`) AND +0xD4 == 0 (`0x0063FBF8..0x0063FBFE`); otherwise straight to the send. Five helper calls (`blx 0x4B9654`), each speed 0.0 (`movs r1,#0`), each string built from `strlen` of the char* (string ctor `0x4E02B2`):

| # | track | mask (r3) | bool& (r2) | key (stack+0) | debugName (stack+4) | site |
|---|---|---|---|---|---|---|
| 1 | HEAD | 1 (`0x0063FC46`) | this+0xB9 (`0x0063FC3E`) | +0xC0 `DirectDriveHead` | +0xC0 `DirectDriveHead` | `0x0063FC02..0x0063FC4C` |
| 2 | LIFT | 2 (`0x0063FCB0`) | this+0xBA (`0x0063FCA8`) | +0xC4 `DirectDriveLift` | +0xC4 `DirectDriveLift` | `0x0063FC6C..0x0063FCB6` |
| 3 | BODY | 4 (`0x0063FD18`) | this+0xB8 (`0x0063FD16`, r2=sb) | +0xBC `DirectDriveWheels` | +0xBC `DirectDriveWheels` | `0x0063FCD6..0x0063FD1E` |
| 4 | BODY | 4 (`0x0063FD80`) | this+0xB8 | +0xBC `DirectDriveWheels` | +0xC8 `DirectDriveArc` | `0x0063FD3E..0x0063FD86` |
| 5 | BODY | 4 (`0x0063FDE8`) | this+0xB8 | +0xBC `DirectDriveWheels` | +0xCC `DirectDriveTurnInPlace` | `0x0063FDA6..0x0063FDEE` |

After the gate (taken or not) it sends the empty StopAllMotors message via `0x0064099C` (`0x0063FE0E ldr r0,[r4,#4]; bl 0x64099c`).
Key vs name: the lock key is the first string. For the three body calls the key is `DirectDriveWheels` every time and only the debug name changes (Wheels / Arc / TurnInPlace). Head and lift pass the same string as both.
Existing records: M4-026/027/028/031 A7 (calls confirmed; strings now filled). Classification: EXACT_SOURCE.

## Row 1b (NEW). DirectDriveCheckSpeedAndLockTracks body

`0x0063EFB0..0x0063F0BC`; tail `b.w 0x008CCDBC` is an ARM veneer (`ldr ip,[pc]; add pc,ip,pc` at `0x008CCDC0`) to LockTracks.
- `|speed| < 1e-5` (literal at `0x0063F0F8` = 0x3727C5AC; abs `0x0063EFC6..0x0063EFCC`, `vcmpe` `0x0063EFD8`): `*flag = 0` (`0x0063EFE6`). If `AreAllTracksLocked(mask)==1` (`0x0063EFEA`) and `UnlockTracks(mask,key)==1` (`0x0063EFF8`; returns 1 when any bit still has locks) it emits `sErrorF` (`0x0063F048`, PLT `0x4A4108`): event `MovementComponent.DirectDriveCheckSpeedAndLockTracks` (`0x0063F0FC`), format `Locks left on tracks %s [0x%x] after %s[%s] unlocked` (`0x0063F134`), args = AnimTrackFlagsToString(mask), mask, debugName.c_str(), key.c_str() (stack stores at `0x0063F032`, `0x0063F038`). Then it sets a global byte (GOT `0x0103E790`) to 1 and, if the byte at GOT `0x0103E78C` is nonzero, calls `sDebugBreakOnError` (`0x0063F07C..0x0063F090`).
- otherwise: `*flag = 1` (`0x0063F096..0x0063F09A`); if `AreAllTracksLocked(mask)==0` (`0x0063F09E`) tail-call `LockTracks(mask,key,debugName)` (`0x0063F0AA..0x0063F0B8`); if already all locked, return with no change.
Classification: EXACT_SOURCE for the lines read. The two GOT globals were not identified (UNKNOWN beyond their use here).

## Row 2. UnlockTracks "not currently locked" channel

`MovementComponent::UnlockTracks` `0x0063FE5C..0x00640098`. Per set bit of `mask` (loop `0x0063FE92..0x0063FF90`, bits 0..7) it builds a search `LockInfo(key,"")` (`0x0063FE9E..0x0063FED2`) and calls `tree.find` (PLT `0x4B969C`, `0x0063FEE0`). Not found (`0x0063FF08 beq 0x0063FF26`) -> `sChanneledInfoF` (`0x0063FF4A`, PLT `0x4A505C`):
- channel: `0x0063FF44 ldr r0,[pc,#0x14C]`, literal at `0x00640094` = 0x005A40A0, `add r0,pc` at `0x0063FF48` -> `0x00BE3FEC` = **`Unnamed`**.
- event `0x00640040` = `MovementComponent.UnlockTracks` (`adr r1,#0xFC` at `0x0063FF42`).
- format `0x00640060` = `Tracks 0x%x are not currently locked by %s` (`adr r3,#0x118` at `0x0063FF46`); args `[sp]=mask` (saved at `0x0063FE8E`, stored `0x0063FF3E`), `[sp+4]=key.c_str()` (`0x0063FF2E..0x0063FF38`); empty KV vector (`r2=sp+0x20`).
- afterwards `PrintLockState()` (PLT `0x4B96B4`, `0x0063FF76`) runs on this not-found path only. Found path: `erase` (PLT `0x4B96A8`, `0x0063FF0E`), OR `(count!=0)` into the return accumulator, `b 0x0063FF7E`, no PrintLockState.
- after either path: if the track remaining count is 0 (`0x0063FF7E..0x0063FF88`) its bit is added to a mask; after the loop a nonzero mask sends an EnableAnimTracks message (`0x0063FF94..0x0063FFB4`, `Robot::SendMessage(msg,1,0)` via `0x4A5368`). Return = accumulator & 1 (`0x0063FFCE`).
Classification: EXACT_SOURCE for channel/event/format/args.

## Row 3. Second (debug-name) string passed to LockTracks, per caller

Complete call-site list of LockTracks (PLT `0x4A57B8`): `0x004F0F68`, `0x005BE624`, `0x00640290`, `0x006404EC`, plus the tail `b.w` at `0x0063F0B8`. No call site in the animation streamer.
`0x004F0F4C` is a local helper `(movement, mask, int tag, const string& name)`: it builds `std::to_string(tag)` (PLT `0x4A4C18`, `0x004F0F5C`) as the key and passes `name` through as the debug name (`0x004F0F68`). Its callers: `0x004F0E00`, `0x0054058E`, `0x0054BE24`, `0x0054EC08`.

| caller | key | debugName | mask | site |
|---|---|---|---|---|
| IActionRunner::Update, else-branch taken when `AreAnyTracksLocked(mask)!=1` (`0x00540440..0x00540446`) | to_string(int at action+0x60) | the action own name string at action+0x48 (`add.w r3,r4,#0x48`), the `std::string name` argument of the IActionRunner ctor | byte action+0x54 (`0x00540438`) | `0x00540584..0x0054058E` |
| TurnTowardsFaceAction::Init | to_string(action+0x60) | action name at +0x48 | 5 (`0x0054BE22`) | `0x0054BE24` |
| ICompoundAction (inner fn `0x0054EB78`, nearest export DeleteActions) | to_string(child+0x60) | child name at +0x48 | child+0x54 | `0x0054EC08` |
| DrivingAnimationHandler (inner fn containing `0x004F0E00`; nearest export HandleActionCompleted `0x004F0D78`) | to_string([this+0x44]) | literal `DrivingAnimations` (17 chars, `adr r1,#0x64` at `0x004F0DDC` -> `0x004F0E44`) | byte [this+0x48] | `0x004F0E00` |
| IBehavior::SmartLockTracks `0x005BE5BC` | its arg 2 | its arg 3 (passed straight through at `0x005BE624`; only when the insert into the behaviour tree at this+0xB4 succeeded, else a log and return 0) | its arg 1 | |
| BehaviorPounceOnMotion::TransitionToWaitForMotion `0x005F8F46` and TransitionToCreepForward `0x005F9662` (the only two SmartLockTracks call sites) | `behaviorPounceOnMotionWaitLock` (30 chars, `0x00BF5F10`) | same string (built twice: `0x005F8F0C..0x005F8F3A`, `0x005F9634..0x005F965C`) | 1 (`0x005F8F44`, `0x005F9660`) | |
| MovementComponent::HandleMessage<ChargerEvent> (SDK-mode gated: `blx 0x4A88E4` at `0x00640200`, `cmp r0,#1` `0x00640204`; unreachable if SDK mode is unsupported) | [this+0xD0] `OnChargerInSDK` | same | byte [this+0xD5] | `0x00640290` |
| MovementComponent::HandleMessage<EnterSdkMode> (SDK mode) | `OnChargerInSDK` | same | byte [this+0xD5] | `0x006404EC` |
| DirectDriveCheckSpeedAndLockTracks | Row 1 | Row 1 | | `0x0063F0B8` |

UnlockTracks call sites (for pairing): `0x004F0ECA`, `0x005BD174` (IBehavior::Stop), `0x005BE706` (SmartUnLockTracks), `0x0063EFF8`, `0x006402C8`, `0x00640570` (ExitSdkMode).
The IActionRunner debug name is a runtime value (each action ctor name argument); there is no single literal. UNKNOWN: IActionRunner::UnlockTracks (`0x005408ED`) and the other UnlockTracks callers key derivation were not read.
Classification: EXACT_SOURCE for the table.

## Row 4. PathComponent::Abort status names

`PathComponent::Abort` `0x00649100..0x006491BA`: `ldr r2,[r4,#0x38]; ldr r1,[r1,r2,lsl #2]` (`0x00649114..0x00649118`) indexes a static array of `const char*` with **no bounds check**. Array base = literal `0x006491E4` + `0x00649114` = `0x0102F610` (`.data.rel.ro`). Channel = literal `0x006491E8` + `0x0064911A` = `0x00BE5510` = `Planner`; event `PathComponent.Abort` (`0x006491EC`); format `Aborting from status '%s'` (`0x00649200`); `sChanneledInfoF` at `0x00649124`.
No function: the table is read in place.

| value | pointer | name |
|---|---|---|
| 0 | `0x00BFC9C6` | `Failed` |
| 1 | `0x00BFC9CD` | `ComputingPath` |
| 2 | `0x00BFC9DB` | `WaitingToBeginPath` |
| 3 | `0x00BFC9EE` | `FollowingPath` |
| 4 | `0x00BFC9FC` | `Ready` |
| 5 | `0x00BFCA02` | `WaitingToCancelPath` |
| 6 | `0x00BFCA16` | `WaitingToCancelPathAndSetFailure` |
| 7+ | slot `0x0102F62C` holds 0 in the file image | no name; UNKNOWN what a value >= 7 does (null pointer to the formatter); nothing read produces one |

After the log (`0x00649170..0x00649188`): `cmp r0,#4; bhi skip; (1<<status)&0x13` -> status 0,1,4 request 4 (`Ready`), status 2,3 request 5 (`WaitingToCancelPath`), via PLT `0x4B9CD8`; status 5/6 unchanged.
Existing record: A3. Classification: EXACT_SOURCE.

## Row 5. Lock-tree comparator

`std::less<LockInfo>` is inlined. Node: left +0, right +4, parent +8, color +0xC, key string +0x10 (12 bytes libc++ SSO), name string +0x1C; size 0x28 (`0x006423E0 movs r0,#0x28`; key copy `0x006423EA..0x006423FE`, name copy `0x00642400..0x0064240E`).
Order uses **only the first string (key, node+0x10)**; the name at +0x1C is never read by any of `0x0064245F`, `0x0064257A`, `0x00642509`.
- compare = `memcmp` (PLT `0x4A5728`) over min(len_a,len_b) bytes (SSO decode: bit0 of byte0 clear -> len=byte0>>1, data=obj+1; else len=[+4], data=[+8]); nonzero result decides; on equal prefix (or min length 0) the shorter string is less (`cmp r7,r5; bhs`). So memcmp byte order, then length.
- `__find_leaf_high` (`0x0064245F..0x006424DA`, used by `__emplace_multi` `0x006423D8` through PLT `0x4B981C` at `0x0064241C`, then `__insert_node_at` PLT `0x4B9828` at `0x00642428`): goes left only when new < node (`ble` at `0x006424B8`, `bhs` at `0x0064247E`), otherwise right. Equal keys therefore insert **after** existing equal keys (insertion order preserved).
- `__lower_bound` (`0x0064257A..0x006425E0`): node < search -> right (`blt` at `0x006425C4`), else record node and go left (`0x006425D4`).
- `find` (`0x00642509..0x00642576`): `lower_bound` (PLT `0x4B9834`, `0x00642516`); returns end if search < found (memcmp<0 at `0x00642564`, or equal prefix with search shorter at `0x0064256E`).
So UnlockTracks `find(LockInfo(key,""))` returns the first-inserted node with that key; erase removes that single node.
Existing records: none covers this (NEW detail for the lock tree). Classification: EXACT_SOURCE.

## Row 6. MotorActionAcked info lines

Three callbacks of identical shape. Channel `0x00BE99E4` = `Actions`; format `0x00BE9DE8` = `[%d] ActionID: %d`; `sChanneledInfoF`; empty KV vector; args `[sp]` = int action tag `[action+0x60]`, `[sp+4]` = u8 motor action id.
- MoveLiftToHeightAction `0x0054D748..0x0054D7B2`: guard `[r4+0x95]!=0` and `[r4+0x94] == id from message` (`0x0054D74E..0x0054D764`); `ldr r1,[r4,#0x60]; ldrb r2,[r4,#0x94]; strd r1,r2,[sp]` (`0x0054D774..0x0054D77C`); event `MoveLiftToHeightAction.MotorActionAcked` (`0x0054D7E4`); `blx` at `0x0054D784`; then `[r4+0x96]=1` (`0x0054D7AC`). So args = (action tag, lift motor action id).
- MoveHeadToAngleAction counterpart **exists**: `0x0054D624..0x0054D68E`: guard `[r4+0xAA]` and `[r4+0xA9]==id`; args `[r4+0x60]`, `[r4+0xA9]` (`0x0054D650..0x0054D658`); event `MoveHeadToAngleAction.MotorActionAcked` (string `0x0054D6C0`); `blx` `0x0054D660`; then `[r4+0xAB]=1` (`0x0054D688`). Literals resolve to the same `Actions` / format.
- TurnInPlaceAction counterpart `0x0054D3F8..0x0054D462`: guard `[r4+0xDB]`, id `[r4+0xDA]`, event `TurnInPlaceAction.MotorActionAcked` (string `0x0054D49A`), ack flag `[r4+0xDC]=1` (`0x0054D45C`).
Existing record: L13 (covers only the lift one). Classification: EXACT_SOURCE.

## Row 7. AnimTrackFlagsToString

`AnimTrackFlagsToString(u8)` `0x006305E8..0x00630786`. Per-flag helper `EnumToString(AnimTrackFlag)` (PLT `0x4B8CAC`) = `0x007BC250..0x007BC2B2` (`cmp #0xF`, `cmp #8`, `tbb` table at `0x007BC262` for 0..8, compare chain for 0x10/0x20/0x40/0xFF):

| value | returned | string address |
|---|---|---|
| 0 | `NO_TRACKS` | `0x00C20049` |
| 1 | `HEAD_TRACK` | `0x00C20053` |
| 2 | `LIFT_TRACK` | `0x00C2005E` |
| 4 | `BODY_TRACK` | `0x00C20069` |
| 8 | `FACE_IMAGE_TRACK` | `0x00C20074` |
| 0x10 | `EVENT_TRACK` | `0x00C20085` |
| 0x20 | `BACKPACK_LIGHTS_TRACK` | `0x00C20091` |
| 0x40 | `AUDIO_TRACK` | `0x00C200A7` |
| 0xFF | `ALL_TRACKS` | `0x00C200B3` |
| 0x80 and every other value | **NULL** (`0x007BC2B0 movs r0,#0`) | |

AnimTrackFlagsToString: arg == 0xFF (`0x006305F2`) -> EnumToString(0xFF) = `ALL_TRACKS` (`0x0063060A..0x0063062C`); arg == 0 (`0x006305F6`) -> `NO_TRACKS` (`0x006305F8..0x0063062C`); both then `strlen` + std::string construct (`0x0063061C`, `0x00630626`). Otherwise an ostringstream loop over bits 0..7 (8 iterations, `cmp.w sb,#7; blt` at `0x0063072A`): for each set bit, if not the first, write `+` (`mov.w sl,#0x2B` at `0x006306EC`, write `0x00630702..0x0063070C`), then `EnumToString(u8(1<<bit))` -> `strlen` -> `__put_character_sequence` (`0x00630712..0x00630724`); result from `stringbuf::str()` (`0x00630734`). Mask 3 -> `HEAD_TRACK+LIFT_TRACK`.
**Bit 0x80 set (mask not 0xFF), including the single-bit call:** `EnumToString(0x80)` is NULL and the next instruction is `strlen(NULL)` at `0x0063071C`, no null check. The original dereferences null (crash).
PrintLockState `0x006410D8..`: loops tracks 0..7 (r8), skips a track whose lock count at track-record +0x30 is 0 (`0x006411B2..0x006411B8`), calls `AnimTrackFlagsToString(u8(1<<i))` (`0x006411BC..0x006411C4`) with no null handling; the function returns a std::string by value, never null, so a null only exists as the `strlen(NULL)` inside it. A lock on track 7 would crash there; the masks seen in the engine (1, 2, 4, 5, `[+0xD5]`=7) never include bit 7.
The rest of PrintLockState output (`0x006411C8..0x006412F4`: name, a literal, `0x20` char, lock key/name strings) was not decoded: UNKNOWN exact text/channel.
Existing record: D2/D3 (confirmed; the old row omitted 0 and 0xFF). Classification: EXACT_SOURCE for AnimTrackFlagsToString; RECOVERABLE_GAP for PrintLockState own output (read `0x006410D8..0x00641500` and its literals).

## Row 8. StopHead and StopBody

Gate (both): `(b[+0xB8] || b[+0xB9] || b[+0xBA]) && b[+0xD4]==0` gates the direct-drive helper calls; the message send happens regardless.

StopHead `0x00640A08..0x00640AA0`:
- gate `0x00640A10..0x00640A26` (to the send at `0x00640A92` otherwise).
- one helper call (`blx 0x4B9654` at `0x00640A72`): speed 0.0, `bool& = this+0xB9` (`add.w r2,r4,#0xB9`), mask 1, key = debugName = +0xC0 `DirectDriveHead` (`0x00640A28..0x00640A6E`).
- always: `MoveHead{speed = 0}` (one zero 32-bit word, `str r1,[sp,#0x18]` at `0x00640A96`), `EngineToRobot(MoveHead&&)` (PLT `0x4B9684`, `0x00640AEA`), `Robot::SendMessage(msg, reliable=1, hot=0)` (`0x00640AF2..0x00640AF6`); sender `0x00640ACC..0x00640B16`; result ignored by StopHead.

StopBody `0x00640C70..0x00640DE8`:
- gate `0x00640C7A..0x00640C94` (to `0x00640DD2` otherwise).
- three helper calls (`blx 0x4B9654` at `0x00640CE2`, `0x00640D4A`, `0x00640DB2`), all speed 0.0, `bool& = this+0xB8` (`r2=sb`), mask 4: (key +0xBC `DirectDriveWheels`, name +0xBC), (+0xBC, +0xC8 `DirectDriveArc`), (+0xBC, +0xCC `DirectDriveTurnInPlace`). No head or lift call.
- always: `DriveWheels` from four zero 32-bit words (`strd r1,r1,[sp,#0x10]; strd r1,r1,[sp,#8]` at `0x00640DD6..0x00640DDA`, `ldm.w r1,{r3,r4,r5,r6}` at `0x00640E36`), `EngineToRobot(DriveWheels&&)` (PLT `0x4B966C`, `0x00640E42`), `Robot::SendMessage(msg,1,0)` (`0x00640E4A..0x00640E4E`); sender `0x00640E1C..0x00640E72`. DriveWheels field names not re-derived: UNKNOWN beyond "all four 32-bit fields are zero".
Existing record: MA4 gate claim confirmed. Classification: EXACT_SOURCE.

## Contradicted / weak existing rows

- No contradiction of A3, A7, D2/D3, L13 found. A7 and A3 are confirmed and now filled; L13 covered only the lift callback, while MoveHeadToAngleAction and TurnInPlaceAction have the same line.
- D2/D3 listed bits 0..6 only: values 0 (`NO_TRACKS`) and 0xFF (`ALL_TRACKS`) are handled before the bit loop.

## Open questions

1. Per-action debug names are runtime values (each action ctor name argument), outside this request.
2. IActionRunner::UnlockTracks (`0x005408ED`) not read; the DrivingAnimationHandler unlock helper (`0x004F0EB2..0x004F0ECA`) uses `to_string(tag)` as key.
3. PrintLockState output format not decoded (Row 7).
4. GOT globals `0x0103E78C` / `0x0103E790` (error-flag / break-on-error) not identified.
5. DriveWheels field names not re-derived.
