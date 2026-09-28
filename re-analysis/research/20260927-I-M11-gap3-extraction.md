# I-M11 gap pass 3 - extraction (read-only)

Job: gap pass 3 (last allowed) for the M11-vision integration job (I-M11). Scope: close
H1..H6, the six targeted items left open by gap pass 2
(`re-analysis/research/20260927-I-M11-gap2-extraction.md`) and the X1 pass
(`re-analysis/research/20260927-X1-M11-extraction.md`). Agent: opencode (DeepSeek),
window 3. Date: 2026-09-27. Scratch: `.scratch/I-M11-gap3/`.

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204).
The vision region is Thumb-2; disassembled with capstone `CS_MODE_THUMB` (base 0). All
addresses below are file VAs. The Ghidra decompilation was not used. The callers were
found by scanning the executable segment for Thumb `bl`/`blx` to the PLT stubs (a
capstone pass with `skipdata=True`); PLT names come from `.rel.plt`.

Row form: `| # | what the original does | citation | record | class |`.

---

## H1. The connected-object drop (behaviour-changing)

**Result: the native path does not drop the unconnected observed active object; it warns
and applies a 10 s cooldown, then continues.** `BlockWorld::AddAndUpdateObjects`
(`0x00620AD4`) has exactly one caller and its return value is a status code, not an
object; there is no branch on the unconnected path that skips or discards the object.

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H1.1 | `BlockWorld::AddAndUpdateObjects(multimap<float,ObservableObject*> const&, unsigned int)` is called from exactly one place, inside `BlockWorld::UpdateObservedMarkers` (the V22 frame sequence): `0x0062505A: blx #0x4B8154` (PLT `AddAndUpdateObjects`). The return is a status: `0x0062505E: mov r6,r0`; `0x00625060: cmp r6,#0`; `0x00625062: beq.w #0x62523A` (0 = success, which runs `CheckForUnobservedObjects`); a non-zero return logs `"BlockWorld.UpdateObservedMarkers.AddAndUpdateFailed"` (`0x0062506C` -> `0xBF9269`) and skips that. No caller-side drop of an object. | `0x0062505A: blx #0x4B8154`; `0x0062505E`/`0x00625060`/`0x00625062`; `0x0062506C`; `0x006250CA` (`CheckForUnobservedObjects` on the 0 path) | M11-004 | EXACT_SOURCE |
| H1.2 | The connected check is gated on the observed object's vtable `+0xc` returning 1 (`0x00620DF0: blx r1`; `0x00620DF2: cmp r0,#1`; `0x00620DF4: bne #0x620EDE`). Only then does it look the object up **by ObjectID**: `0x00620E70: ldr r0,[sp,#0x118]`; `0x00620E72: add.w r1,r0,#0x14`; `0x00620E76: mov r0,fp`; `0x00620E78: blx #0x4A6FB8` (`GetConnectedActiveObjectByIdHelper`). | `0x00620DF0`..`0x00620DF4`; `0x00620E70`..`0x00620E78` | M11-004 | EXACT_SOURCE |
| H1.3 | If the lookup **finds** a counterpart (`0x00620E7C: cbnz r0,#0x620EDE`) it goes straight to `0x620EDE`. If it returns null, the code **warns** (`0x00620E9E: blx #0x4A4540`, `sWarningF`, string `0xBF854B` = `"Observed active object of type %s but it's not connected. Is the battery plugged in?"`) and records the 10 s cooldown (`0x00620ED2: blx #0x4B7F80` `unordered_map<int,float>::operator[]`; `0x00620ED6: vadd.f32 s0,s22,s18` with `s18 = 10.0` from `0x00620B1E`; `0x00620EDA: vstr s0,[r0]`). There is **no branch out**: the next instruction is `0x00620EDE`, which both the found and not-found paths reach. | `0x00620E7C`; `0x00620E9E`; `0x00620ED2`/`0x00620ED6`/`0x00620EDA`; `0x00620B1E`; string `0xBF854B` | M11-004 | EXACT_SOURCE |
| H1.4 | `0x00620EDE` continues into the located-object matching loop over the vector filled at `0x00620DE6` (`FindLocatedMatchingObjects`): `0x00620EDE: ldrd r5,sl,[sp,#0x160]`; `0x00620EE4: beq.w #0x621064` (empty -> skip); per located object it does `HasSameRootAs` (`0x00620EF2`), `IsCarryingObject` (`0x00620F06`), `FindRoot`/`GetID` (`0x00620F1A`/`0x00620F20`) and stores `map[id] = located` (`0x00620FBC: blx #0x4B7F8C`; `0x00620FC2: str r7,[r0,#0x14]`). Nothing here tests "connected" or drops the observation. | `0x00620EDE`..`0x00620FC2` | M11-004 (partial) | EXACT_SOURCE |
| H1.5 | `GetConnectedActiveObjectByIdHelper(ObjectID const&) const` (`0x0061F58C`) is a pure lookup: it builds a `BlockWorldFilter` with the ObjectID, calls `FindConnectedObjectHelper(filter, callback, true)` (`0x0061F61C: blx #0x4A70C0`) and returns its result. It has no side effect and no drop. | body `0x0061F58C`; `0x0061F61C: blx #0x4A70C0` | M11-004 | EXACT_SOURCE |
| H1.6 | `BlockWorld::AddConnectedActiveObject(int, unsigned int, ObjectType)` (`0x0062302C`) creates an `ActiveObject` (`0x00623296: blx #0x4A7B10` `CreateActiveObjectByType`) and inserts it into the connected map. Its only caller is `0x00533BD6`. Its only `RemoveConnectedActiveObject` call is on the `ConflictingActiveID` path (`0x00623140`..`0x0062319E`), where an existing entry with the same activeID is disconnected before re-adding — it is not a drop of an observed object. `RemoveConnectedActiveObject(int)` (`0x006243A0`) erases one entry from the connected map (`0x00624432: blx #0x4B80D0` `__tree<...>::erase`); its two callers are `0x00533CA0` and `0x0062319E`. Neither drops observed objects. | `0x0062302C`; `0x00623296`; `0x00623140`..`0x0062319E`; `0x006243A0`; `0x00624432`; callers `0x00533BD6`/`0x00533CA0`/`0x0062319E` | M11-004 | EXACT_SOURCE |

**Conclusion for H1.** The C# `BlockWorld.AddAndUpdateObject` returning `null` for an
unconnected active object (BlockWorld.cs:458-464, guarded by `AllowUnconnectedObjects`,
which is an uninitialised `bool` property at line 306 and therefore defaults to false) is
**not** what the native `AddAndUpdateObjects` does. The native warns and rate-limits with
a 10 s cooldown and keeps going. M11-004's "connected-object rule" must be worded as
warn + cooldown, not drop, unless the manager deliberately classifies the C# drop as a
`LOCAL_POLICY`/`COMPATIBILITY_POLICY` divergence.

---

## H2. The `WasMoving` predicate

**Result: the predicate is not a wheel-velocity, pose-delta or threshold test. It reads a
single stored status bit: bit 0 (`IS_MOVING`) of the `HistRobotState` status word at
`+0x58` (a copy of `RobotState.status` at `+0x4c`).**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H2.1 | `MovementComponent::WasMoving(unsigned int)` (`0x006417DC`) builds a `std::function<bool(HistRobotState const&)>` whose vtable is `0x102F238` (`0x00641802`..`0x0064180C`; the `__func` type name at `0xC7F630` is `...MovementComponent::WasMoving...$_0...`) and calls the helper `0x00641898` with `(history, timestamp, &string, &function)`. | `0x006417DC`; `0x00641802`/`0x00641806`/`0x0064180C`; `0x00641898`; data `0xC7F630` | M11-004 | EXACT_SOURCE |
| H2.2 | The helper calls `RobotStateHistory::GetRawStateAt(timestamp, frameId, HistRobotState&, bool)` (`0x006418C0: blx #0x4A7D44`); on success it invokes the function through `std::function::operator()` (`0x00641956: blx #0x4B972C`); on failure it logs and returns 1. | `0x006418C0`; `0x00641956`; `0x006418C4`/`0x0064194E` (failure return 1) | M11-004 | EXACT_SOURCE |
| H2.3 | `std::function<bool(HistRobotState const&)>::operator()` (`0x00641DAC`) loads `__f_` at `this+0x10` (`0x00641DAC: ldr r0,[r0,#0x10]`), its vptr (`0x00641DB0: ldr r2,[r0]`) and the `__call` slot at vptr `+0x18` (`0x00641DB2: ldr r2,[r2,#0x18]`; `0x00641DB4: bx r2`). The `WasMoving` vtable is `0x102F238`, whose `+0x18` word is `0x00642673`. | `0x00641DAC`..`0x00641DB4`; vtable `0x102F238+0x18 = 0x00642673` | M11-004 | EXACT_SOURCE |
| H2.4 | The lambda body (`0x00642672`) is `ldr r0,[r1,#0x58]; and r0,r0,#1; bx lr`: it returns `HistRobotState[+0x58] & 1`. `HistRobotState` embeds `RobotState` at `+0xC` (memcpy of `0x5B` bytes at `0x005304F4`), so `+0x58` = `RobotState+0x4C`, the status word. | `0x00642672`/`0x00642674`/`0x00642678`; `0x005304F4: blx #0x4A4354` (memcpy 0x5B); `0x005304D8` | M11-004 | EXACT_SOURCE |
| H2.5 | Bit 0 of that status word is `IS_MOVING`: `MovementComponent::Update(RobotState)` does `ldr r1,[r8,#0x4C]`; `and r1,r1,#1`; `strb r1,[r6,#-0x7F]` (= `MovementComponent+9`). Confirms `+0x4C` is the status word and bit 0 is `IS_MOVING`. | `0x0063E30A`/`0x0063E314`/`0x0063E31A` | M2 (IS_MOVING mapping, M2-protocol.md:494) | EXACT_SOURCE |

**Conclusion for H2.** `WasMoving(t)` is exactly "the `RobotStatusFlag::IS_MOVING` bit of
the robot state nearest `t`", i.e. `HistRobotState.status & 1`. The gap-2 question
"wheel velocities? pose delta? threshold?" is answered: none of those; it is a stored
firmware status flag. (`WasHeadMoving`/`WasLiftMoving`/`WereWheelsMoving`/`WasCameraMoving`
use other bits/bytes of the same word: `+0x59` bit 1, `+0x59` bit 0, `+0x59` bit 7, and
`(flags & 0x8200) != 0x200`, at `0x6426D2`/`0x64273A`/`0x6427A2`/`0x642802`.)

---

## H3. The object-match base extent (vtable slot `+0x2c`)

**Result: the virtual at `+0x2c` is the object's stored size/extent getter; it returns a
`Point<3,float>` at object `+0x88`, which `Block::Block` fills with the `BlockInfo`
dimensions `(44,44,44)` for every light-cube type. The `+0x30` thunk multiplies that by
`0.8`, so the distance threshold is `(35.2, 35.2, 35.2)` mm.**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H3.1 | The `+0x30` thunk (`0x004E025C`) calls vptr `+0x2c` (`0x004E0262: ldr r2,[r0,#0x2c]`; `0x004E0264: mov r0,r1`; `0x004E0266: blx r2`), copies the returned `Point<3,float>` to its out pointer (`0x004E026E: ldm.w r0,{r2,r3,r4}`; `0x004E0274: stm r1!,{r2,r3,r4}`) and multiplies all three components by the constant at `0x004E028C` (`0x004E0268: vldr s0,[pc,#0x20]` -> `0x004E028C` = `0x3F4CCCCD` = `0.8`; loop `0x004E0276`..`0x004E0288`). | `0x004E025C`..`0x004E0288`; data `0x004E028C = 0.8` | M11-004 (G10.4a) | EXACT_SOURCE |
| H3.2 | The `+0x2c` implementation (`0x004E3830`) is a size getter: `ldr r1,[r0]` (vptr); `ldr r1,[r1,#-0x34]` (secondary-base offset); `add r0,r1`; `adds r0,#0x88`; `bx lr`. It returns `&(this + baseoffset + 0x88)`. The matching primary getter is the adjacent `0x004E382C: adds r0,#0x88; bx lr`. The `+0x2c` getter appears at slot `+0x2c` in seven vtables whose `+0x30` is the 0.8 thunk (`0x101CDEC`, `0x101CFBC`, `0x101D574`, `0x101D69C`, `0x101D8A8`, `0x101D9CC`, `0x101DBC0`). | `0x004E3830`; `0x004E382C`; vtable words at the seven `+0x2c` slots | M11-004 (NEW detail) | EXACT_SOURCE |
| H3.3 | `Block::Block(ObjectFamily, ObjectType)` (`0x004E5F50`) fills object `+0x88`: `0x004E5F7C: blx #0x4A47C8` (`Block::LookupBlockInfo`); `0x004E5F80: adds r0,#0x10`; `0x004E5F82: add.w r1,r4,#0x88`; `0x004E5F88: ldm.w r0,{r2,r3,r7}`; `0x004E5F8C: stm r1!,{r2,r3,r7}`. The `BlockInfo` dimensions are `44.0` for keys 1..4 (G9.1: `0x004E4CD6: movt r1,#0x4230` etc.). So for a LightCube/Block the field is `(44.0, 44.0, 44.0)` and the scaled threshold is `(35.2, 35.2, 35.2)`. | `0x004E5F50`; `0x004E5F7C`..`0x004E5F8C`; `0x004E4CD6`/`0x004E4D8E`/`0x004E4E46`/`0x004E4EFA` (44.0) | M11-004 / M11-003 | EXACT_SOURCE |

**Naming note.** The `+0x2c`/`+0x18` functions have no symbol in the `.so` symbol table;
they are the class's stored-`Point<3,float>` size getter, overridden per class (e.g. the
vtable at `0x101EF18` overrides `+0x2c` with `0x004F8A2C: adds r0,#0x58; bx lr`). The
`ObservableObject` size getter is the correct name; the exact member name is not
recoverable from symbols, but the field offset (`+0x88`) and values are.

---

## H4. `WasHeadRotatingTooFast` / `WasBodyRotatingTooFast`

**Result: both compare the absolute value of one IMU gyro-rate component, from the
samples immediately before and after the timestamp, against the threshold. Head uses
`rateY` (`ImuData+0x8`); body uses `rateZ` (`ImuData+0xc`). If either exceeds the
threshold the predicate is true; if no IMU bracket exists it is also true (fail-safe).**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H4.1 | `WasHeadRotatingTooFast(timestamp, threshold, int)` (`0x00656260`): if `[this+0x2F0] != 0` return 0 (`0x0065626E`..`0x00656276`); `s16 = threshold` (`0x00656278: vmov s16,r2`). If the int arg `r3 >= 1` it takes the `ImuDataHistory::IsImuDataBeforeTimeGreaterThan(timestamp, int, threshold, 0, 0)` path (`0x0065627C`/`0x00656292: blx #0x4BA6E0`). Otherwise it zeroes two `ImuData` at `sp+0x30`/`sp+0x18` and calls `GetImuDataBeforeAndAfter(timestamp, before, after)` (`0x006562BE: blx #0x4BA6EC`). | `0x00656260`; `0x00656278`; `0x00656292`; `0x006562BE` | M11-004 | EXACT_SOURCE |
| H4.2 | No data -> log `"VisionComponent.VisionComponent.WasHeadRotatingTooFast.NoIMUData"` / `"Could not get next/previous imu data for timestamp %u"` and return 1 (`0x006562C2: cbz r0,#0x656306`; `0x00656306`..`0x00656342: movs r0,#1`). | `0x00656306`..`0x00656342` | M11-004 | EXACT_SOURCE |
| H4.3 | With data: `|before.rateY| > threshold` -> return 1 (`0x006562C4: vldr s0,[sp,#0x38]`; `0x006562DA: vcmpe.f32 s0,s16`; `0x006562E2: bgt #0x656342`), then `|after.rateY| > threshold` -> return 1 (`0x006562E4: vldr s0,[sp,#0x20]`; `0x006562FA: vcmpe`; `0x00656302: ble #0x656274` -> return 0, else `0x00656304: b #0x656342` -> return 1). `sp+0x38 = before+8`, `sp+0x20 = after+8`. | `0x006562C4`..`0x00656304` | M11-004 | EXACT_SOURCE |
| H4.4 | `WasBodyRotatingTooFast` (`0x00656384`) is identical except it reads `before+0xC` and `after+0xC`: `0x006563E8: vldr s0,[sp,#0x3C]`; `0x00656402: bgt`; `0x00656408: vldr s0,[sp,#0x24]`; `0x00656426: ble`; `0x00656428: b #0x656464` (return 1). | `0x00656384`; `0x006563E8`/`0x00656408` | M11-004 | EXACT_SOURCE |
| H4.5 | The `ImuData` layout: `ImuDataHistory::AddImuData(unsigned int, float, float, float, unsigned char)` (`0x00538B24`) stores `timestamp @0` (`0x00538B2E: str r1,[sp]`), `rateX @4`/`rateY @8` (`0x00538B3A: strd r2,r3,[sp,#4]`), `rateZ @0xC` (`0x00538B3E: vstr s0,[sp,#0xC]`), `u8 @0x10`. The caller `HandleImageImuData` (`0x00535C20`) loads `ImageImuData` `rateX/rateY/rateZ` (`0x00535C2E: ldm.w r0,{r1,r2,r3}`; `0x00535C36: vldr s0,[r0,#0xC]`) and passes them in that order. So `+8 = rateY`, `+0xC = rateZ`. | `0x00538B24`; `0x00538B2E`/`0x00538B3A`/`0x00538B3E`; `0x00535C2E`/`0x00535C36` | M11-004 (NEW detail) | EXACT_SOURCE |
| H4.6 | The live call passes `r3 = 0`: `VisionComponent::WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0)` (`0x00621C9A`/`0x00621C9E` -> `0x3E32B8C2`; `0x00621CA4: mov r3,r2`; `0x00621CAA: str r1,[sp]` with `r1 = 0`; `0x00621CAE: blx #0x4A5BB4`). `WasRotatingTooFast` (`0x0065359C`) forwards that 5th arg to both sub-calls (`0x006535A0: ldr r6,[sp,#0x18]`; `0x006535AC: blx #0x4BA404`; `0x006535C6` body `0x00656384`), so the live path is the `GetImuDataBeforeAndAfter` branch. | `0x00621C9A`..`0x00621CAE`; `0x0065359C`/`0x006535A0`/`0x006535AC` | M11-004 | EXACT_SOURCE |

---

## H5. The `Block` size getter field (vtable slot `+0x18`)

**Result: the `+0x18` virtual is `0x004E382C` (`adds r0,#0x88; bx lr`); it reads the
object's `Point<3,float>` at `Block+0x88`, the same field `Block::Block` fills from the
`BlockInfo` dimensions (44,44,44).**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H5.1 | `Block::AddFace` (`0x004E53BC`) calls vptr `+0x18` three times and reads the returned `Point<3,float>`: `0x004E53FA: ldr r0,[r5]`; `0x004E53FC: ldr r1,[r0,#0x18]`; `0x004E53FE: mov r0,r5`; `0x004E5400: blx r1`; `0x004E5402: vldr s16,[r0,#4]`; then again for `[r0,#8]` (`0x004E540E`) and `[r0]` (`0x004E5424`). The three are halved at `0x004E542C`/`0x004E5430`/`0x004E5434`. | `0x004E53FA`..`0x004E5434` | M11-003 | EXACT_SOURCE |
| H5.2 | The `+0x18` implementation is `0x004E382C: adds r0,#0x88; bx lr` (the primary-base sibling of the `+0x2c` getter `0x004E3830`). The `+0x18` getter `0x004E382C` appears at slot `+0x18` in seven primary vtables (`0x101CD30`, `0x101CF2C`, `0x101D4DC`, `0x101D60C`, `0x101D810`, `0x101D93C`, `0x101DB30`); they are distinct vtable objects from the H3.2 secondary vtables, but both getters return the field at `+0x88`. | `0x004E382C`; vtable words `0x101CD48` etc. | M11-003 (NEW detail) | EXACT_SOURCE |
| H5.3 | `Block+0x88` is written from the `BlockInfo` entry by `Block::Block` (`0x004E5F80`..`0x004E5F8C`, H3.3), and the entry values are `44.0` for all four light-cube types (G9.1). So the field the getter reads is the block's size `Point<3,float>`. | `0x004E5F80`..`0x004E5F8C`; `0x004E4CD6` etc. | M11-003 | EXACT_SOURCE |

---

## H6. M11-005 `converged` semantics (Cholesky out-flag)

**Result: on `pivot < FLT_EPSILON` the function sets the `bool&` out-flag to `true` and
returns `Result 0`; on a completed factorisation the flag stays `false` and it also
returns `Result 0`. `RefineQuadrilateral` maps `flag != 0` to `[sp+0x60] = 1` and at
`0x008C6418: cmp r0,#1; bne #0x8C644A` the `1` case falls through to the branch that
copies the refined corners and sets the result to 0. So a near-singular normal matrix
takes the accept path, and a normal completed solve takes the error-threshold path.**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H6.1 | Signature confirmed: `Anki::Result Anki::Embedded::Matrix::SolveLeastSquaresWithCholesky<float>(Array<float>&, Array<float>&, bool, bool&)`. Entry clears the out-flag: `0x0088DE7A: mov sb,r3`; `0x0088DE86: strb.w r0,[sb]` with `r0 = 0` (`0x0088DE7C`). | `0x0088DE68`; `0x0088DE7A`/`0x0088DE7C`/`0x0088DE86`; signature string `0xC334FB` | M11-005 | EXACT_SOURCE |
| H6.2 | Failure condition: `0x0088DF50: vcmpe.f32 s0,s18` with `s18 = FLT_EPSILON` (`0x0088DEB8: vldr s18,[pc,#0x2F4]` -> `0x88E1B0 = 0x34000000 = 1.1920929e-07`); `0x0088DF58: bmi.w #0x88E132`. The target sets the out-flag **true** and returns 0: `0x0088E132: movs r0,#1`; `0x0088E134: strb.w r0,[sb]`; `0x0088E138: movs r0,#0`. | `0x0088DF50`/`0x0088DF58`; `0x0088DEB8`; data `0x88E1B0`; `0x0088E132`..`0x0088E138` | M11-005 | EXACT_SOURCE |
| H6.3 | On the normal path (`p >= FLT_EPSILON`) the out-flag is never written again. The epilogue `0x0088E090`..`0x0088E0DE` returns `r0 = 0` (`0x0088E094: movs r0,#0`; `0x0088E0D8: movs r0,#0`) without touching `[sb]`. So `flag == true` holds **only** for the near-singular pivot case. | `0x0088E090`..`0x0088E0DE` | M11-005 | EXACT_SOURCE |
| H6.4 | `RefineQuadrilateral` initialises its out-flag at `sp+0xD7`: `0x008C61E4: movs r0,#0`; `0x008C61EC: strb.w r0,[sp,#0xD7]`; calls the solver `0x008C61F4: blx #0x4D0B5C`; `0x008C61F8: mov sb,r0`; `0x008C61FE: bne.w #0x8C5BF4` (solver Result != 0 -> error return). | `0x008C61E4`..`0x008C61FE` | M11-005 / M11-031 | EXACT_SOURCE |
| H6.5 | It reads the flag: `0x008C6206: ldrb.w r6,[sp,#0xD7]`; `0x008C62AE: mov r2,r6`; `0x008C62CA: cmp r6,#0`; `0x008C62CC: it ne`; `0x008C62CE: movne r2,#1`; `0x008C62D4: str r2,[sp,#0x60]`. So `[sp+0x60] = (outflag != 0) ? 1 : 0`. | `0x008C6206`/`0x008C62AE`/`0x008C62CA`..`0x008C62D4` | M11-005 | EXACT_SOURCE |
| H6.6 | The branch: `0x008C6418: ldr r0,[sp,#0x60]`; `0x008C641A: cmp r0,#1`; `0x008C641C: bne #0x008C644A`. The `== 1` case falls through to `0x008C641E`..`0x008C6448`, which copies the four refined corners into `[sp+0x39C]`, calls `SetCast`, and sets `sb = 0`. The `!= 1` case (`0x008C644A`..`0x008C6464`) calls the error helper `0x008C66C4` and sets `sb = 1` when the error exceeds `[sp+0x394]`. `RefineQuadrilateral` returns `sb` (`0x008C5BF4: mov r0,sb`), so `0` is accept and `1` is failure. | `0x008C6418`..`0x008C6464`; `0x008C5BF4` | M11-005 / M11-031 | EXACT_SOURCE |

**Conclusion for H6.** A near-singular normal matrix (`pivot < FLT_EPSILON`) sets the
`bool&` out-flag true and `RefineQuadrilateral` takes the **accept** path. The record's
name `converged` is misleading: the flag is not set on a successful factorisation; it is
set exactly on the degenerate-pivot early-out, and the caller treats that early-out as
accept. The manager should either rename the flag in M11-005's wording (e.g.
`degeneratePivot`) or state the polarity explicitly, and should not describe the
`pivot < FLT_EPSILON` path as the failure path.

---

## Existing records contradicted or too weak (from this pass)

- **M11-004** (`EXACT_SOURCE`). Its "connected-object rule" (the C# drop) is contradicted
  by H1: the native `AddAndUpdateObjects` warns and applies a 10 s cooldown, then
  continues; it does not drop the observation. The record must be re-worded, or the C#
  drop recorded as a deliberate policy divergence. Its `WasMoving` wording ("the
  robot-motion gate is `WasMoving(timestamp)`") is correct but the predicate body is now
  known to be the `IS_MOVING` status bit (H2.4/H2.5).
- **M11-005** (`EQUIVALENT_IMPLEMENTATION`). The gap-2 note stands and is now confirmed
  instruction-for-instruction (H6): the `converged` out-flag is set true only on
  `pivot < FLT_EPSILON`, and the caller accepts on true. The manifest wording must be
  tightened before the failure path is called exact.
- **M11-003** (`EXACT_SOURCE`). Its evidence is still bare symbol names. This pass pins
  the `Block` size getter at vtable `+0x18` = `0x004E382C` reading `Block+0x88`, and the
  `+0x2c` getter at `0x004E3830` (H3/H5). Re-cite from those addresses.

## Open questions the manager must decide or send back

1. **M11-004 connected-object rule (H1).** Decide whether the C# `return null` is a
   deliberate `LOCAL_POLICY`/`COMPATIBILITY_POLICY` or a defect. The native behaviour is
   warn + 10 s cooldown + continue; the C# behaviour is drop. The two are not the same.
2. **M11-005 `converged` polarity (H6).** Rename/word the flag. The current record's
   naming implies the opposite of the mechanical behaviour; a reader would call the
   near-singular path a failure when the code accepts it.
3. **H3 class/member naming.** The `+0x2c`/`+0x18` getters have no symbol; the manager
   may accept the descriptive "size getter / `Point<3,float>` at `+0x88`" wording, or
   ask for the class name from the `vtt`/typeinfo (not read here).

## What is still unread

`BlockWorld::CreateObjectsFromMarkers`'s insertion of the observed object (to show the
unconnected observation persists after the warning) is still unread; the H1 answer rests
on the `AddAndUpdateObjects` branch structure alone. The exact class/typeinfo name of the
seven `+0x18`/`+0x2c` vtables is unread.

*Read-only extraction. Nothing outside `.scratch/I-M11-gap3/` and this report file was changed.*


