# B-M6b-2 - the six MISSING bodies (voice/bus engine, modulator, bus meter, pool, completion tail, insert-FX slot)

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode).
Every behaviour below was read from the raw instruction stream (capstone, ARM mode). The Ghidra
tree was used to navigate only. Addresses are ELF VAs. Scope: the six items of job B-M6b-2.

Legend: EXACT_SOURCE = body/step read from instructions; RECOVERABLE_GAP = plausibly recoverable
from a shipped artifact but not read here; UNKNOWN = not established.

---

# Item 1 - the V7 per-voice state-machine callees

## 1.0 The caller and its registers

`0xA54F1C(voice, params)` prologue (`0xA54F1C..0xA54F34`): `r5 = [voice+0xD4]` (source);
`r6 = [source+0xC]` (**bus**); `r8 = [voice+0xF0]` (id); `sb = bus+0xC`. So in the branch
table, `r6` is the bus and `r4` is the voice.

Direct calls out of `0xA54F1C` (BL scan of `0xA54F1C..0xA55750`):

| call | site | when |
|---|---|---|
| `0xA4BC58(voice, bus+0xC, id, gain, ...)` | `0xA55058` | always, right after `source->vt+0x4C` (`0xA55008`) |
| `0xA022E8(bus,1)` | `0xA55398` | voice state path (`0xA55384`: `P2F=[sp+0x2F]!=0`, `E4==0`, `[voice+0xCD]&1`) |
| `0xA0228C(bus)` | `0xA55484` | voice state path (`0xA5547C`: `P2F==0`, `E4!=0`, `[voice+0xCD]&1==0`) |
| `0xA4C584(voice, r0)` | `0xA554E8` | after `voice->vt+0x58` returns 1 (`0xA554CC..0xA554E8`) |
| `0xA56650(source, [bus+0x1DC], [bus+0x1E0])` | `0xA554F8` | `E4==2`, `SRC10==0` (`0xA554F0`) |
| `0xA54A30(voice)` | `0xA555C0` | `r5!=0` and `[voice+0x1B4]==0` (`0xA555B8`) |
| `0xA4B4B0(voice)` | `0xA55644` | after `0xA54A30` returns 1 (`0xA555F0..0xA55644`) |
| `0xA4BC58` again | `0xA5572C` | same completion path, after the gain recompute |

`0xA4B93C` is **not** called from `0xA54F1C`. Its only caller is `0xA5587C`, inside
`0xA55750` (the duck/stop voice function). `0x9D4228` is called from `0xA4B93C`
(`0xA4BA90`), from `0xA4B74C`, and from `0xA55844` (inside `0xA55750`). `0xA01768` is
called from `0xA558AC` (`0xA559C4`) and `0xA55A84` (`0xA55B54`), plus `0xA37xxx`.
So the V7 "state machine" reaches all six bodies through `0xA54F1C` plus the
stop/duck/source-attach functions it shares the voice object with.

## 1.1 `0xA4C584(voice, bit)` - body `0xA4C584..0xA4C5AC` (40 B)

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `r3 = [voice+0x28]` (connection list head); if null return | `0xA4C584 ldr r3,[r0,#0x28]`; `0xA4C588 cmp`; `0xA4C58C bxeq lr` | EXACT_SOURCE |
| 2 | For every connection node: `r2 = [node+0x6C]`; `bfi r2, r1, #2, #1` (bit2 = low bit of `r1`); store; `r3 = [node+0x28]` | `0xA4C590 ldrb r2,[r3,#0x6c]`; `0xA4C594 bfi r2,r1,#2,#1`; `0xA4C598 strb`; `0xA4C59C ldr r3,[r3,#0x28]` | EXACT_SOURCE |

Contract: `r0=voice`, `r1=bit`. Effect: sets bit2 of `[conn+0x6C]` on every connection.
At the only call site (`0xA554E0 mov r0,r4`; `0xA554E8 bl`) `r1` is the return of
`voice->vt+0x58` (0/1). Bit2 of `conn+0x6C` is the same bit that `0xA4BC58` sets
(`0xA4BD54 bfi r3,r0,#2,#1`) and that the aux-send predicate tests as `&6`
(`0xA44890`). So the step propagates the voice `vt+0x58` result to all sends.

## 1.2 `0xA022E8(bus, unused)` - body `0xA022E8..0xA0232C`

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | If `[bus+0x1BE] & 0x20 == 0` return | `0xA022E8 ldrb r3,[r0,#0x1be]`; `0xA022EC tst r3,#0x20`; `0xA022F0 bxeq lr` | EXACT_SOURCE |
| 2 | Clear bit5 of `[bus+0x1BE]` | `0xA022F8 bfc r3,#5,#1`; `0xA02300 strb r3,[r0,#0x1be]` | EXACT_SOURCE |
| 3 | `ip = [bus+0x1F0]` (count), `r2 = [bus+0x1EC]` (array); for each entry `r1`: `[r1+0x22] -= 1` (u16) | `0xA022F4 ldr ip,[r0,#0x1f0]`; `0xA022FC ldr r2,[r0,#0x1ec]`; `0xA02318 ldrh r3,[r1,#0x22]`; `0xA0231C sub r3,r3,#1`; `0xA02320 strh r3,[r1,#0x22]` | EXACT_SOURCE |
| 4 | Tail: decrement the global dword at `0x108DE78` | `0xA02328 b 0xA370E4`; `0xA370E8 add r2,pc,#0x656D88` -> `0x108DE78`; `0xA370F0 sub r3,r3,#1`; `0xA370F4 str` | EXACT_SOURCE |

Contract: `r0=bus`; `r1` is ignored by the body. Effect: if the bus's bit5 latch is set,
clear it, decrement a per-target u16 at `[entry+0x22]` for each of `[bus+0x1F0]` entries
in the array `[bus+0x1EC]`, then decrement the global `[0x108DE78]`. This is the "release"
half of a counted acquire/release pair. Called at `0xA55398` with `(bus,1)`.

## 1.3 `0xA0228C(bus)` - body `0xA0228C..0xA022E0`

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | If `[bus+0x1BE] & 0x20 != 0` return | `0xA0228C ldrb r3,[r0,#0x1be]`; `0xA02290 tst r3,#0x20`; `0xA02294 bxne lr` | EXACT_SOURCE |
| 2 | Set bit5 of `[bus+0x1BE]` | `0xA0229C orr r3,r3,#0x20`; `0xA022A4 strb` | EXACT_SOURCE |
| 3 | For each of `[bus+0x1F0]` entries in `[bus+0x1EC]`: `[entry+0x22] += 1` | `0xA022BC ldrh r3,[r1,#0x22]`; `0xA022C0 add r3,r3,#1`; `0xA022C4 strh` | EXACT_SOURCE |
| 4 | Increment the global dword at `0x108DE78` | `0xA022CC/0xA022D0` GOT -> `0x108DE78`; `0xA022D4 ldr r2,[r3]`; `0xA022D8 add r2,r2,#1`; `0xA022DC str` | EXACT_SOURCE |

Contract: `r0=bus`. Effect: the exact inverse of 1.2; the pair is the acquire/release of
the bus's per-target reference count. Identity of `[bus+0x1EC]/[bus+0x1F0]` and of
`[0x108DE78]` (a manager-base dword, same base `0x108DE78` used by `0xA39564`) is UNKNOWN.

## 1.4 `0xA01768(bus, out)` - body `0xA01768..0xA017EC`

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `r3 = [bus+0x1BB]`; if bit7 set: `*out = r3 & 7`; return `(r3>>3)&0xF` | `0xA0176C ldrb r3,[r0,#0x1bb]`; `0xA01770 tst r3,#0x80`; `0xA01778 and r3,r3,#7`; `0xA0177C str r3,[r1]`; `0xA01784 ubfx r0,r0,#3,#4` | EXACT_SOURCE |
| 2 | Else set bit7; `r0 = [bus+0xE0]`; call `0x9EEDA4(r0)` | `0xA01790 orr r3,r3,#0x80`; `0xA01794 ldr r0,[r0,#0xe0]`; `0xA017A0 bl 0x9EEDA4` | EXACT_SOURCE |
| 3 | If result `== 3`: call `[[bus+0xE0]]->vt+0x120([bus+0x14C])`; code `= (r==0)?1:2` | `0xA017C8 ldr r0,[r4,#0xe0]`; `0xA017CC ldr r1,[r4,#0x14c]`; `0xA017D4 ldr r3,[r3,#0x120]`; `0xA017D8 blx`; `0xA017E0 moveq r2,#1`; `0xA017E4 movne r2,#2` | EXACT_SOURCE |
| 4 | Else code `= r & 0xF`; then `[bus+0x1BB]` bits0-2 = `*out`, bits3-6 = code; return code | `0xA017A8 andne r2,r0,#0xf`; `0xA017B8 bfi r3,r1,#0,#3`; `0xA017BC bfi r3,r2,#3,#4`; `0xA017C0 strb` | EXACT_SOURCE |

Contract: `r0=bus`, `r1=&out`. Returns a 4-bit "next source" code and writes a 3-bit
index to `*out`; the pair is cached in `[bus+0x1BB]` (bit7 = valid). In V7 it is called
as `0xA01768(bus, &[voice+0xE0])` at `0xA559C4`/`0xA55B54`, i.e. returns `E4` and writes
`E0`. The meaning of the codes and the callee `0x9EEDA4` are UNKNOWN.

## 1.5 `0xA4B93C(voice)` - body `0xA4B93C..0xA4BC30` (760 B)

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `r5 = [voice+8]` (the bus connection owner, set to `[source+0xC]+0xC` at `0xA55938`); `s14 = [r5+0x30]*0.05`; if `< -37.0` -> 0 else fast-pow; `*[r5+0x34]`; store `[voice+0x1C]` | `0xA4B944 ldr r5,[r0,#8]`; `0xA4B950 vldr s15,[r5,#0x30]`; `0xA4B958 vmul`; `0xA4B95C vcmpe`; `0xA4B9B4 vmul s14,s15,s14`; `0xA4B9B8 vstr s14,[r4,#0x1c]` | EXACT_SOURCE |
| 2 | Call `0x9BE28C(r5)`; if non-zero take the alternate path `0xA4BAB8` (`0x9BF8E4`/`0xA5E694`) | `0xA4B9BC bl 0x9BE28C`; `0xA4B9C4 bne 0xA4BAB8` | EXACT_SOURCE |
| 3 | `r6 = [voice+0x14]` (connection count); if 0, allocate a `0x4C`-byte array at `[voice+0x10]` (capacity `[voice+0x18]`), init each entry, then fall into 4 | `0xA4B9C8 ldr r6,[r4,#0x14]`; `0xA4BB54..0xA4BC30` (`0xA4BB68 bl 0xA7A7F4`; `0xA4BC20 str r3,[r4,#0x10]`) | EXACT_SOURCE |
| 4 | Copy `[[r5+8]+0x22]` byte to `[[voice+0x10]+0x44]`; `s14 = [r5+0x58]*0.05`; if `< -37` -> 0 else fast-pow; `*[[r5+8]+0x60]`; store `[[voice+0x10]+0x34]` | `0xA4B9D4 ldr r2,[r5,#8]`; `0xA4B9DC vldr s14`; `0xA4B9E0 ldrb r2,[r2,#0x22]`; `0xA4B9E8 strb r2,[r3,#0x44]`; `0xA4BA08..0xA4BA54` | EXACT_SOURCE |
| 5 | Call `0x9BDA88(r5)`; if 0 return | `0xA4BA5C bl 0x9BDA88`; `0xA4BA64 beq 0xA4BAA0` | EXACT_SOURCE |
| 6 | Call `0x9BD368(r5, sp+0x10)`; then `0x9D4228(sp+0x10, voice+0x2C, ([voice+0xCD]>>1)&1, voice+0xCC, voice)` | `0xA4BA74 bl 0x9BD368`; `0xA4BA84 str r4,[sp]`; `0xA4BA90 bl 0x9D4228` | EXACT_SOURCE |
| 7 | Set bit1 of `[voice+0xCD]` | `0xA4BA98 orr r3,r3,#2`; `0xA4BA9C strb` | EXACT_SOURCE |

Contract: `r0=voice`. Effect: (re)initialises the voice's send/connection table
(array at `[voice+0x10]`, count `[voice+0x14]`, capacity `[voice+0x18]`, 0x4C-byte
entries), copies the per-target byte and the bus send gain into the first entry, then
gathers and dispatches via `0x9D4228`; latches bit1 of `[voice+0xCD]`. The callees
`0x9BE28C`, `0x9BDA88`, `0x9BD368`, `0x9BF8E4`, `0xA5E694` and the object at `[voice+8]`
are not read here (RECOVERABLE_GAP).

## 1.6 `0x9D4228(paramBlock, outArray, flag, &countByte, obj)` - body `0x9D4228..0x9D46EC` (1224 B)

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `r3=&countByte`; `r6 = *r3` (input count); if 0 -> `0x9D4680` (lr=0) | `0x9D4230 ldrb r6,[r3]`; `0x9D4238 cmp r6,#0`; `0x9D423C beq` | EXACT_SOURCE |
| 2 | Build a stack array of up to 8 entries `{float value, +0xc, +0x10}` from the input array `r1` (stride 0x14), taking only entries with `value > 0` | `0x9D4250 vldr s15,[ip]`; `0x9D4260 vcmpe s15,#0`; `0x9D426C vstr s15,[r4,#-0x5c]`; `0x9D4274/78/7C` | EXACT_SOURCE |
| 3 | Copy fields from `r0`'s sub-structures into the output entries (a 0xA0-byte per-entry layout, 8 entries, stride 0x14); the flag `r2` selects the variant | `0x9D4290 cmp r2,#0`; `0x9D429C..0x9D4404` (flag!=0) and `0x9D4408..0x9D4564` (flag==0) | EXACT_SOURCE |
| 4 | Write the resulting count to `*r3` (`strb r4,[r3]`) | `0x9D45F0 strb r4,[r3]`; also `0x9D46DC`, `0x9D4678`, `0x9D4694` | EXACT_SOURCE |
| 5 | Per gathered entry: `r8 = [[[obj+8]+8]+0x22]`; call `0x9D4108(obj, entry, r8)` | `0x9D45F8 ldr r3,[r5,#8]`; `0x9D45FC ldr r3,[r3,#8]`; `0x9D4600 ldrb r8,[r3,#0x22]`; `0x9D4620 bl 0x9D4108` | EXACT_SOURCE |

Contract: `r0=&paramBlock`, `r1=outArray`, `r2=flag`, `r3=&countByte`, `[sp+0x90]=obj`.
Effect: gathers the active entries of the block into the output array and dispatches a
per-entry update. Semantic identity of the block/entries UNKNOWN.

`0x9D4108(obj, entry, mask)` (`0x9D4108..0x9D4218`, read): looks up a registry node via
`0x9A7EB0([global],[entry+0xC],1)`; if found, walks `[[found]+8]` and calls
`0xA43434(found, entry, ...)` for nodes whose `[node+0x18] & mask`, then tail-calls
`found->vt+0xC` (`0x9D419C ldr r3,[r5]`; `0x9D41A4 ldr r3,[r3,#0xc]`; `0x9D41B0 bx r3`).
The `[found+0xCC]&0x40` alternate path (`0x9D41B4`) is also read. Identity of the
registry/objects UNKNOWN.

## 1.7 The insert-FX build inside `0xA54A30` - body `0xA54A30..0xA54F1C` (1232 B)

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `r2=[voice+0xD4]` (source); `r7=[source+0xC]` (bus); `sl=voice+0x100`; copy 3 words of `[bus+0x158]` to `sp+0x34`; call `0xA5321C(voice+0x100, sp+0x34, bus)`; if `!=1` return 2 | `0xA54A30 ldr r2,[r0,#0xd4]`; `0xA54A3C ldr r7,[r2,#0xc]`; `0xA54A40 add sl,r0,#0x100`; `0xA54A64 ldm r2,{r0,r1,r2}`; `0xA54A78 bl 0xA5321C`; `0xA54A7C cmp r0,#1`; `0xA54A88 mov r6,#2` | EXACT_SOURCE |
| 2 | For i=0..3: `0xA019B8(bus, i, &cand)`; if `cand!=0`: `0x9CC2AC([cand+0x10], sp+0x40, 1)` then `0x9CC4D8([cand+0x10], 3, sp+0x40)` | `0xA54AF0 bl 0xA019B8`; `0xA54B04 ldr r0,[r3,#0x10]`; `0xA54B2C bl 0x9CC2AC`; `0xA54B90..0xA54BA0` | EXACT_SOURCE |
| 3 | If no plugin (`r0==0` and `sp+0x4a==0` and `sp+0x48==0`): allocate 0x9C bytes (`0xA7A7F4`), memset, init fields `+0x10/+0x14/+0x18/+0x1c`, `+0x30..+0x68`, store vtable `[0x1040174]+8 = 0x103DC38`, call `vt+0x28` (`0xA79858`) as init; if it returns 1 store the slot at `[voice+0x370+i*4]` | `0xA54BE0 bl 0xA7A7F4`; `0xA54BF8 bl memset`; `0xA54C10..0xA54C98`; `0xA54C30 add r2,r2,#8`; `0xA54C50 str r2,[fp]`; `0xA54CB8 ldr ip,[ip,#0x28]`; `0xA54CBC blx ip`; `0xA54E98 str fp,[sb]` | EXACT_SOURCE |
| 4 | `[voice+0xF0] = sp+0x38`; `0xA764D4(voice+0x1D0, 0)` (filter A init); if 1 -> `0xA54D28`: `0xA764D4(voice+0x3A0,0)` (filter B init); `0xA5676C(voice+0x380, bus)` (gain init); for each collected slot object call `vt+0x24`; `voice->vt+0x6C`; return | `0xA54B70 str r1,[r5,#0xf0]`; `0xA54B74 bl 0xA764D4`; `0xA54D44 bl 0xA764D4`; `0xA54D5C bl 0xA5676C`; `0xA54DA8 ldr r3,[r3,#0x24]`; `0xA54DC8 ldr r3,[r3,#0x6c]` | EXACT_SOURCE |

Contract: `r0=voice`. Effect: starts/rebuilds the voice's source, resolves up to four
insert-FX slots from the bus, and initialises filter A/B and the gain object. The plugin
factory `0x9CC2AC` and validator `0x9CC4D8` are generic Wwise plug-in helpers:

- `0x9CC2AC(key, &obj, arg)` (`0x9CC2AC..0x9CC34C`, read): searches a registry list
  (`[base+0xC]` entries, `[base+0x10]` count, 12-byte entries `{key, fn, ...}`) at base
  `0x108D9DC` (`0x9CC2B8 add r3,pc,r3`, pc `0x9CC2C0` + lit `0x006C171C`); on a key match
  calls `entry[1]` and stores the result at `*r1`, then calls `result->vt+0x10(r2)`;
  returns 1. Returns 2 if not found. This is the plug-in create.
- `0x9CC4D8(desc, type, &out)` (`0x9CC4D8..0x9CC508`, read): checks `[desc+4]` version
  `== 0x7E000/0x7E001` (else 0x2E) and `[desc] == type` (else 0xA; 0 on match). This is
  the plug-in version/type validator.

Both EXACT_SOURCE for the body; the registry contents are runtime-populated (UNKNOWN).

## 1.8 Call order (relative to the named functions)

In `0xA54F1C`: `source->vt+0x4C` (`0xA55008`) -> **`0xA4BC58`** (`0xA55058`) -> state
machine; within it, in the order the code can reach them: **`0xA56650`** (`0xA554F8`,
`E4==2`/`SRC10==0`), **`0xA4C584`** (`0xA554E8`, after `voice->vt+0x58`), `0xA022E8`
(`0xA55398`) / `0xA0228C` (`0xA55484`), **`0xA54A30`** (`0xA555C0`, `r5!=0` and no
`[voice+0x1B4]`), then **`0xA4B4B0`** (`0xA55644`) and **`0xA4BC58`** again (`0xA5572C`).
`0xA4B93C` (`0xA5587C`), `0x9D4228` (`0xA4BA90`, `0xA4B74C`, `0xA55844`) and `0xA01768`
(`0xA559C4`, `0xA55B54`) are reached from the stop/duck and source-attach paths, not
from the `0xA54F1C` state machine.

**Reachability:** `0xA022E8`/`0xA0228C` are gated only on `[bus+0x1BE]` bit5;
`0xA4C584` only on the `voice->vt+0x58` return; `0xA01768` on the source attach. Whether
the shipped Cozmo media exercises each state cannot be decided from the `.so` alone
(would need the bank/capture) -> UNKNOWN. No callee is provably dead.

---

# Item 2 - the modulator coefficient recompute `0x9E35C0..0x9E3664` (+ handlers)

**The prior report's reading is wrong.** `0x9E3630` is `vnmls.f64 d17,d11,d11`; VNMLS is
`Dd = Dn*Dm - Dd`, so with `d17=1.0` it computes `d11*d11 - 1.0`, **not** `1 - d11*d11`.
This is confirmed by Ghidra's own expression at `009e2bd0.c:337` (`SQRT(dVar49 * dVar49 -
1.0)`) and by the other `vnmls.f32` uses in this file (`0x9FDB0C`-style random values:
`s12 = s11*s14 - 1.0`). The argument is therefore `>= 0` for every real angle, and the
"sqrt of a negative for every real angle" claim is contradicted.

Inputs: `r6` = the per-voice source/modulator object. `damping = [r6+0x88]` (f32);
`freq = [r6+0x84]` (f32); `shape = [r6+0x80]`; `prev_shape = [r6+0xa4]`;
`phase = [r6+0x9c]`. Constants (read from the literal pool): `d9 = 48000.0` (`0x9E2F90`),
`d8 = 24000.0` (`0x9E2F98`), `d10 = 2*pi = 6.283185307179586` (`0x9E2FA0`),
`s25 = 48000.0f` (`0x9E2FA8 = 0x473B8000`), `s24 = 2*pi f` (`0x9E2FAC = 0x40C90FDB`).

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `d = [r6+0x88]`; if `d == 0` -> `0x9E5004`: `c1=1.0`, `c2=0.0`, jump to the store at `0x9E3654` | `0x9E35C0 vldr s15,[r6,#0x88]`; `0x9E35C4 vcmp s15,#0`; `0x9E35CC beq 0x9E5004`; `0x9E5004 vmov.f32 s12,#1.0`; `0x9E5008 vldr s15,[pc->0x9E5018]=0x0`; `0x9E500C b 0x9E3654` | EXACT_SOURCE |
| 2 | else `t = (f <= 48000) ? 24000/f : 0.5` (f64) | `0x9E35D0 vldr s15,[r6,#0x84]`; `0x9E35D8 vcvt.f64.f32 d16,s15`; `0x9E35DC vcmpe.f64 d16,d9`; `0x9E35E4 vdivle.f64 d16,d8,d16`; `0x9E35E8 vmovgt.f64 d16,#0.5` | EXACT_SOURCE |
| 3 | `t = exp(-damping * log(t))` = `t^(-damping)` | `0x9E35F0 bl 0x4CFF50 (log)`; `0x9E35F4 vldr s15,[r6,#0x88]`; `0x9E3600 vnmul.f64 d16,d17,d16`; `0x9E3608 bl 0x4D0064 (exp)` | EXACT_SOURCE |
| 4 | `w = t * 24000 / 48000 * 2*pi`; `c = cos(w)` | `0x9E3610 vmul d16,d16,d8`; `0x9E3614 vdiv d16,d16,d9`; `0x9E3618 vmul d16,d16,d10`; `0x9E3620 bl 0x4D0070 (cos)` | EXACT_SOURCE |
| 5 | `a = 2.0 - c` | `0x9E35D4 vmov.f64 d11,#2.0`; `0x9E362C vsub.f64 d11,d11,d16` | EXACT_SOURCE |
| 6 | `arg = a*a - 1.0` | `0x9E3624 vmov.f64 d17,#1.0`; `0x9E3630 vnmls.f64 d17,d11,d11` | EXACT_SOURCE |
| 7 | `s = sqrt(arg)`; if the result is NaN (`d16 != d16`) -> `0x9E52E4`: `s = sqrt(arg)` (PLT `sqrt` @ `0x4A6D3C`) and jump to 0x9E3644 | `0x9E3634 vsqrt.f64 d16,d17`; `0x9E3638 vcmp.f64 d16,d16`; `0x9E3640 bne 0x9E52E4`; `0x9E52E4 vmov r0,r1,d17`; `0x9E52E8 bl 0x4A6D3C` | EXACT_SOURCE |
| 8 | `s = s - a`; `c2 = (float)s`; `c1 = c2 + 1.0`; store `[r6+0x94]=c1`, `[r6+0x98]=c2` | `0x9E3644 vsub.f64 d16,d16,d11`; `0x9E364C vcvt.f32.f64 s15,d16`; `0x9E3650 vadd.f32 s12,s15,s12` (s12=1.0); `0x9E3660 vstr s12,[r6,#0x94]`; `0x9E3664 vstr s15,[r6,#0x98]` | EXACT_SOURCE |
| 9 | Phase increment: `[r6+0xa0] = (f<48000) ? f/48000 : 1.0`, times `2*pi` if `shape==0`; convert `[r6+0x9c]` between radians/cycles if `shape` changed; `[r6+0xa4]=shape`; `[r6+0xa8]=0` | `0x9E3668 vcmpe s14,s25`; `0x9E3670 vdivmi s13,s14,s25`; `0x9E3674 vmovpl s13,#1.0`; `0x9E367C vstr s13,[r6,#0xa0]`; `0x9E3680 vmuleq s13,s13,s24`; `0x9E3688 cmp lr,r3`; `0x9E369C/0x9E36A0`; `0x9E36AC/0x9E36B0` | EXACT_SOURCE |

**Result.** `c2 = sqrt((2-cos w)^2 - 1) - (2-cos w)`, `c1 = c2 + 1`, where
`w = pi * (f/24000)^damping` for `f <= 48000`, and `w = pi * 0.5^(1-damping)` for
`f > 48000`. The recurrence consuming these is `out[n] = gain*shape*c1 - out[n-1]*c2`
(see item 4). The `damping==0` branch is the pass-through `c1=1, c2=0`. The NaN branch
is defensive: `arg = (2-cos w)^2 - 1 >= 0` for all real `w`, so the branch is not taken
for real input; if it were, it would compute `sqrt` of the same (negative) argument and
leave NaN in `c1/c2`. The Wwise identity of the source object at `voice+0x30` and of the
`+0x80/+0x84/+0x88` fields remains UNKNOWN.

---

# Item 3 - the bus metering DSP `0xA50044..0xA50FD0`

Confirmed: this region is the bus node's post-FX **level-analysis/metering** stage. It
writes no audio samples. Every store in the region goes to stack, to the meter object's
per-channel scalar arrays (`[sl+4]`, `[sl+8]`, `[sl+0xc]`), to the filter state
(`[sl+0x10]`), to the 0x30-byte state copy, or to `[sl+0x1c]`. The only stores outside
those are the output-pointer/flags bookkeeping at `0xA50EC0` (`strb r3,[r5,#0x1b8]`) and
`0xA50EC8` (`str [r1,#-4]!`), both in the `[bus+0x1BC]==1` branch. There is no store to
`out+0` (the sample data).

`out = [bus+0x60]` (or an FX slot `bus+0x138+i*0x1C`), `sl = [out+0x18]` = meter object.

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `valid = [out+0xE]` (u16); `gain = [out+0x14]`; `flags = [sl+0x20]`; if `valid==0` clear the selected arrays via `memset` | `0xA50044 ldrh r3,[r6,#0xe]`; `0xA50048 vldr s15,[r6,#0x14]`; `0xA50050 ldrb r3,[sl,#0x20]`; `0xA5005C bne 0xA50084`; `0xA50060..0xA50074`; `0xA50F08/0xA50F30/0xA50F58` | EXACT_SOURCE |
| 2 | If `flags&1`: per-channel min/max **peak** -> `[sl+4]+ch*4`; `gain*1.0009619` applied | `0xA50084 tst r3,#1`; `0xA50088 bne 0xA50D48`; `0xA50D5C vldr s5,[0xA50FD4]=0x3F801F85`; `0xA50E78 vstr s13,[r7]` | EXACT_SOURCE |
| 3 | If `flags&4`: per-channel **RMS** -> `[sl+8]+ch*4` | `0xA50090 tst r3,#4`; `0xA50094 bne 0xA50C84`; `0xA50D14 vdiv.f32`; `0xA50D18 vsqrt.f32`; `0xA50D34 vstr s14,[r5]` | EXACT_SOURCE |
| 4 | If `flags&2`: per-channel **fixed-coefficient filtered peak** -> `[sl+0xc]+ch*4`; state at `[sl+0x10]`, 0x30 bytes/channel | `0xA5009C tst r3,#2`; `0xA500A0 bne 0xA501B0`; `0xA50204 ldr r8,[sl,#0xc]`; `0xA5020C ldr sb,[sl,#0x10]`; `0xA50C64 vstr s13,[r3]`; `0xA50B58..0xA50B8C` state copy-back | EXACT_SOURCE |
| 5 | If `flags&0x10`: `0xA52164(sl, gain, out)` -> `[sl+0x1c]` = `gain^2*(E1 + 1.41253746*E2)/nCh` from a two-section biquad cascade | `0xA500A4 tst r3,#0x10`; `0xA500BC bl 0xA52164`; `0xA5257C..0xA525AC` (`0x3FB4CE07`, `0x3F801F85`) | EXACT_SOURCE |
| 6 | If `[bus+0xC1] & 0x1F`: `0x9C806C(reg, bus+0x48, sl, out+4)` (registered per-bus callback) | `0xA500C0..0xA5019C`; `0xA5019C bl 0x9C806C` | EXACT_SOURCE |
| 7 | Return, or if the child flag is set call `conn->vt+0x34(conn, out, sl)` | `0xA500D0 ldr r3,[fp,#-0xc8]`; `0xA500DC return`; `0xA500F4..0xA50110` | EXACT_SOURCE |

Per-channel mapping (read): channel count `= [out+4]` (byte); stride `= [out+0xC]` (u16);
channel mask `= (out+4 >> 12) & 0x637`; config `= (out+4 >> 8) & 0xF` must be 1 for
`0xA52164` (`0xA52180 ubfx r2,r3,#8,#4; cmp r2,#1`). Each analysis loops `ch = 0..count-1`
and indexes the channel plane at `out+0 + ch*4*stride`.

**Filter identity.** The bit-2 stage is a per-channel fixed-coefficient filter feeding a
min/max envelope. Its coefficient literals are exactly the 4-lane pool at
`0xA506C0..0xA507CC` (e.g. `0x3F78E000`=0.97216797, `0x3F47A000`=0.77978516,
`0x3EEE2000`=0.46508789, `0x3E0CA000`=0.13732910, and the antisymmetric negative set
`0xBDD18000`=-0.10229492, `0xBE4D2000`=-0.20031738, `0xBE2A8000`=-0.16650391,
`0xBD738000`=-0.05944824). The stage uses `vld1.64 {d16,d17}`/`vdup.32` and processes
four independent lanes; the per-lane tap-to-state mapping is not confidently
transliterable from the compiler-scheduled NEON (lane extractors at
`0xA502CC..0xA502D8`). No shipped artifact names the filter: there is no
meter/peak/RMS/loudness string and no Wwise SDK header. So the **DSP identity and the
per-lane mapping are UNKNOWN / RECOVERABLE_GAP**; settling them needs the Wwise 2016.2
SDK bus-meter source or a dynamic trace of `0xA4FEF8`. The scalar control flow, the
field offsets, the loop bounds, the gains and the store targets are EXACT_SOURCE.

---

# Item 4 - `0x9E2BD0` / `0x9E52F8`: pool/record layer and "NEON lane order"

## 4.1 Record and pool layout (verified from instructions)

- Two chunk lists: `[arg+0x20]` holds **type-0** records (0x4C B) and `[arg+0x14]`
  holds **type-1** records (0x30 B) (`0x9E2BDC ldr sl,[r0,#0x20]`; `0x9E2C20 ldr
  r4,[r3,#0x14]`).
- Chunk node (0x28 B) at `[arg+0x20]`/`[arg+0x14]` nodes: `+0` next, `+4` buffer1,
  `+8` buffer1 used, `+0xC` buffer1 cap, `+0x10` buffer2, `+0x14` buffer2 used,
  `+0x18` buffer2 cap, `+0x1C` float-buffer needed, `+0x20` float-buffer allocated,
  `+0x24` float-buffer pointer. This is written by the allocators
  (`0x9E501C..0x9E50D8` and `0x9E50DC..0x9E5198`).
- Type-0 node allocation: `0x9E5020 mov r1,#0x28`; `0x9E5050 mov r1,#0x4C0` (16*0x4C);
  `0x9E5090 mov r1,#0x280` (16*0x28); cap 0x10 (`0x9E5080 mov r8,#0x10`).
- Type-1 node allocation: `0x9E50E0 mov r1,#0x28`; `0x9E5110 mov r1,#0x300` (16*0x30);
  `0x9E5150 mov r1,#0x100` (16*0x10); cap 0x10.
- The two `vst1.32 {d16,d17}` at `0x9E5068` and `0x9E5128` are the only NEON in
  `0x9E2BD0`; they zero four words of the node header.

## 4.2 Compaction (verified)

`0x9E2F4C` (list `[arg+0x20]`) and `0x9E3074` (list `[arg+0x14]`): for each node, if
`used(+8) < capacity(+0xC)/2` (`0x9E2F5C cmp r3,r2,lsr #1`; `0x9E2F60 bhs`), unlink it,
free the float buffer `[node+0x24]` via `0xA7A914` (`0x9E3018`), free buffer1 `[node+4]`
(`0x9E3038`), free buffer2 `[node+0x10]` (`0x9E3058`), and free the node via `0xA7A988`
(`0x9E3064`); zero `+8/+0x14/+0x20/+0x24`. Same in the second list at `0x9E30B4..0x9E3150`.
The float buffer is grown lazily when `node+0x20 < node+0x1C` (free `0xA7A914`, alloc
`node+0x1C<<2` via `0xA7A894`). **EXACT_SOURCE.**

## 4.3 "NEON lane order" - there is none

The per-sample shape/transition loops are **scalar VFP**, not NEON. The full
`0x9E2BD0..0x9E52F4` disassembly contains only two `vst1.32` (the node zero-init above)
and no `vld1`/`vmla`/`vdup` NEON data-processing; every shape case is a scalar loop, e.g.
case 4 saw-down at `0x9E3734..0x9E374C`:
`vadd.f32 s17,s17,s19` (phase += inc); `vadd.f32 s16,s16,s20` (gain += step);
`vsub.f32 s15,s25,s17`; `vmul.f32 s15,s16,s15`; `vmul.f32 s15,s15,s24` (c1);
`vmls.f32 s15,s18,s21` (out = ... - prev*c2); `vstmia r3!,{s15}`; `vmov.f32 s18,s15`.
So the prior report's "NEON lane order" gap does not exist for `0x9E2BD0`; the
recurrence is exact and scalar. `0x9E52F8` likewise contains no NEON (0 vld1/vmla in the
whole body); its stages are scalar VFP loops. **EXACT_SOURCE** (the arithmetic),
UNKNOWN/not applicable (there is no lane order to determine).

Note: the type-0 record layout, the type-1 record layout and the five shapes are
confirmed as in the prior report; the report's 0x28-byte "second buffer" wording is the
pool *node* size, while the records are 0x4C/0x30 as stated there.

---

# Item 5 - `0x9FD910`, the V26 completion tail

Called by `0x9FDD90` as a tail call when `tick >= [obj+0x34]`
(`0x9FDE20 ldr r3,[obj+0x34]`; `0x9FDE28 blt`; `0x9FDE30 b 0x9FD910`). `r0 = obj`;
`r1 = tick` is not read by the body.

`obj` fields used (read from instructions): `+8` pointer to a "used-byte" descriptor
`{+0 buffer, +4 len}`; `+0xC` pointer to a row-table descriptor `{+0 base, +4 count,
+8 slope0, +0xC slope1, +0x10 slope2}`; `+0x10` u16 index; `+0x12` u16 index limit;
`+0x14` u16 row index; `+0x18` flags (bit0 randomize, bit1 continue); `+0x1C/+0x1D`
bytes; `+0x30` previous threshold; `+0x34` threshold; `+0x38` segment length;
`+0x3C` rate; `+0x40` t-start; `+0x48/+0x4C/+0x50` base (3 f32); `+0x54/+0x58/+0x5C`
slopes (3 f32).

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `r7=[obj+0xC]` (table); `sl=[r7+4]` (count); `idx=[obj+0x14]`; if `idx < count` go to the "advance row" path | `0x9FD918 ldr r7,[r0,#0xc]`; `0x9FD920 ldrh r2,[r0,#0x14]`; `0x9FD924 ldr sl,[r7,#4]`; `0x9FD92C cmp r2,sl`; `0x9FD938 blt 0x9FDA34` | EXACT_SOURCE |
| 2 | Global LCG state pointer at `[0x1040090] -> 0x108D868`; multiplier `0x5851F42D4C957F2D`, `+1`; output = `high32 >> 1`; the two halves `0x5851F42D`/`0x4C957F2D` are used at `0x9FD94C/50/54/58` and `0x9FDA40..5C` | `0x9FD928 ldr r8,[pc->0x9FDD60]=0x642950`; `0x9FD934 add r8,pc,r8` -> `0x104028C`; `0x9FD948/0x9FD960` `[r8-0x1FC]` -> `0x1040090`; `0x9FD96C mul`; `0x9FD974 umull`; `0x9FD984 strd`; `0x9FD988 lsr r0,r5,#1` | EXACT_SOURCE |
| 3 | "Advance row": element `= [r7] + idx*0x10`; read `{e0,e1,e2,e3}`; three LCG outputs `u` give random `u*2^-30 - 1` in `[-1,1)`; `base_i = e_i + rand_i*slope_i`; store base to `[obj+0x48/+0x4C/+0x50]`; `[obj+0x14] = idx+1` | `0x9FDA34 ldr r3,[pc->0x9FDD64]=0xFFFFFE04`; `0x9FDA38 add r1,r2,#1`; `0x9FDA3C ldr fp,[r7]`; `0x9FDA60 add r2,fp,r2,lsl #4`; `0x9FDA7C/84/8C` loads; `0x9FDAF0..0x9FDB18` `vnmls.f32` (s = rand*0x30800000 - 1); `0x9FDB20/24/2C vmla`; `0x9FDB28/30/34 vstr`; `0x9FDA4C strh r1,[r6,#0x14]` | EXACT_SOURCE |
| 4 | Segment length: `G = [[0x10400EC]]`; `r0 = max(1, (G + e3 - 1)/G)`; `[obj+0x38] = r0` | `0x9FDAF0 ldr r3,[pc->0x9FDD68]=0xFFFFFE60`; `0x9FDB38 ldr r3,[r8,r3]` -> `0x10400EC`; `0x9FDB3C ldr r1,[r3]`; `0x9FDB44 add r0,r1,r3`; `0x9FDB48 sub r0,r0,#1`; `0x9FDB4C bl __aeabi_idiv`; `0x9FDB58 moveq r0,#1`; `0x9FDB60 str r0,[r6,#0x38]` | EXACT_SOURCE |
| 5 | If `count > idx+1`: interpolate to row `idx+1` (`0x9FDB70`); else go to the "random/end" path `0x9FD93C` | `0x9FDB5C cmp sl,r1`; `0x9FDB6C ble 0x9FD93C`; `0x9FDB64 movgt r3,r1`; `0x9FDB68 lslgt r1,r3,#4` | EXACT_SOURCE |
| 6 | Interpolate: `target = [r1]` (row); `r7=[obj+0x34]`; `[obj+0x34]=r7+r0`; `[obj+0x30]=r7`; `[obj+0x3C]=1/r0`; `[obj+0x40]=-r7/r0`; `[obj+0x54/+0x58/+0x5C] = target - base` | `0x9FDB98/9C/A0/B4` loads; `0x9FDBA0..0x9FDC10` LCG; `0x9FDC14..0x9FDC3C`; `0x9FDC4C/50/54 vmla`; `0x9FDC60/68/70 vsub`; `0x9FDC6C/74/78 vstr` | EXACT_SOURCE |
| 7 | "Random/end" path (`idx >= count`): if flags bit0 clear -> `0x9FDC88` increment `[obj+0x10]`, and if `[obj+0x12] <= [obj+0x10]` either reset to `[obj+4]` (flags bit1) or return with `[obj]=0` | `0x9FD93C ldr sl,[r6,#0x18]`; `0x9FD940 ands r2,sl,#1`; `0x9FD944 beq 0x9FDC88`; `0x9FDC88..0x9FDCD0` | EXACT_SOURCE |
| 8 | If flags bit0 set: LCG -> `[obj+0x10] = out % [obj+0x12]` | `0x9FD948..0x9FD990`; `0x9FD98C bl __aeabi_idivmod`; `0x9FD994 strh r1,[r6,#0x10]` | EXACT_SOURCE |
| 9 | If flags bit1 set: use the byte buffer at `[obj+8]` (`{+0 buffer, +4 len}`) as a shuffle bag: scan for a zero; if none, `memset(buffer,0,len)` then set `buffer[idx]=1`; else set `buffer[idx]=1`; if the buffer is all-nonzero and `[obj+0x1D]!=0` continue | `0x9FD9B8..0x9FD9FC`; `0x9FDCE4..0x9FDD0C`; `0x9FDD24..0x9FDD48` (`0x9FDD30 bl memset`) | EXACT_SOURCE |
| 10 | Then interpolate from the **current base** `[obj+0x48/+0x4C/+0x50]` to row 0 (`0x9FDA00` -> `0x9FDB70`), reusing `[obj+0x38]` as the segment length | `0x9FDA00 ldr r3,[r6,#0xc]`; `0x9FDA0C strh r2,[r6,#0x14]`; `0x9FDA10/18/1C/20/24/28/2C`; `0x9FDA30 b 0x9FDB70` | EXACT_SOURCE |

**Result.** On completion, `0x9FD910` picks the next 3-component target (either the next
table row or a random row from the shuffle bag), sets the base to the current row plus a
per-component random jitter in `[-1,1)` times the per-table slopes, sets the three
slopes to `target - base`, sets the segment length `r0`, rate `1/r0`, t-start `-old/r0`,
and advances the threshold `[obj+0x34]`. The Wwise feature name, the meaning of the
table descriptor / `[obj+8]` / `[0x1052444]` and of the flags is **UNKNOWN** (no RTTI,
no symbol, no bank reference read).

---

# Item 6 - insert-FX slot class identity (V12 / V18b)

## 6.1 Bus insert-FX slot (V18b) - `0xA4E974`

`0xA4E974(bus, i, local)` (body `0xA4E974..0xA4ECD4`) is the per-slot create/init/reset
flow. The slot record is at `bus + i*0x1C` (`i=0..3`; `sb = bus + i*0x1C`, e.g.
`0xA4E9B8..0xA4E9CC`). Field layout (all read):

| field | meaning | citation |
|---|---|---|
| `+0xC8` | format/reset target (passed to `0x9CF644`, `0x9CF820`) | `0xA4E9F0 add lr,r6,#0xc8`; `0xA4EA20 bl 0x9CF644`; `0xA4E884 bl 0x9CF820` |
| `+0xCC` | Init arg (`effect->vt+0x1C` 4th arg) | `0xA4EB24 ldr r3,[sb,#0xcc]` |
| `+0xD4` | plugin descriptor pointer (`= [ip+0x10]`; `-1` after drop) | `0xA4E9D4 str r3,[sb,#0xd4]`; `0xA4E880 str r3,[fp,#0xd4]` |
| `+0xD8` | effect object (created by `0x9CC2AC`); `vt+0x1C`=Init, `vt+0xC`=Reset, `vt+0x20`=Execute/GetBuffer | `0xA4EA88 bl 0x9CC2AC`; `0xA4EB30 ldr ip,[ip,#0x1c]`; `0xA4EC08 ldr r3,[r3,#0xc]`; `0xA4FE7C/0xA4FEE8 ldr ip,[ip,#0x20]` |
| `+0xDC` | 0x24-byte helper object (alloc `0xA7A7F4(0x24)`, ctor `0xA6C25C`, vtable `0x103D538`) | `0xA4EA4C bl 0xA7A7F4`; `0xA4EA64 bl 0xA6C25C`; `0xA4EA74 str fp,[sb,#0xdc]` |
| `+0xE0` | flags: bit0 in-place (from `sp+0x1C`), bit1 set from the per-slot object | `0xA4EA3C/44 bfi/strb`; `0xA4FE80..0xA4FE98` |
| `+0x138` | out-buffer pointer (allocated when not in-place) | `0xA4ECC4 str sb,[r3,#0x138]`; freed `0xA4E8F4 bl 0xA7A914` |
| `+0x13C` | format word | `0xA4ECD0 str r1,[r3,#0x13c]` |
| `+0x140` | state (`0x11` not-in-place, `0x2B` after drop) | `0xA4EC84 str lr,[r1,#0x140]`; `0xA4E910 str r1,[r4,#0x140]` |
| `+0x144`/`+0x146` | u16 count / u16 0 | `0xA4ECC8 strh r6,[r2,#4]`; `0xA4ECCC strh r0,[r2,#6]` |
| `+0x150` | another object; destroyed `vt+0` then freed | `0xA4E888 ldr r5,[fp,#0x150]`; `0xA4E89C..0xA4E8B4` |

The effect object's vtable is **assigned by the runtime-populated plug-in registry**
(`0x9CC2AC`, registry base `0x108D9DC`; `0x9CC2B8 add r3,pc,r3` with lit `0x006C171C`).
It is not statically visible. The helper object at `+0xDC` has the static vtable
`0x103D538` (`0xA6C280 add lr,pc,lr` with lit `0x005D1218`, `0xA6C288 +0x98`), whose
`vt+0` destructor is `0xA6B790`. **No RTTI, no symbol and no effect-class name string
exists** in the `.so` (searched `CAk`, `Insert`, `Reverb`, `Delay`, `Effect`,
`Compressor`, `Matrix`, `Meter`: only Anki/breakpad hits). So the class name is
**UNKNOWN**; what is established is the behaviour and the slot table above.

## 6.2 Voice insert-FX slot (V12) - `0xA54A30`

The 0x9C-byte voice slot object created in `0xA54A30` (`0xA54BE0 bl 0xA7A7F4`) stores its
vtable from the GOT:

- plugin present: `r2 = [0x1040174]` = `0x103DC30`; `0xA54C30 add r2,r2,#8` -> **vtable
  `0x103DC38`** (`0xA54C50 str r2,[fp]`). Slots: `+0x00`=0xA79E18 (dtor),
  `+0x24`=0xA52678, `+0x28`=0xA79858 (init, called `0xA54CB8 ldr ip,[ip,#0x28]`),
  `+0x2C`=0xA79DAC (teardown `0xA55D78`), `+0x38`=0xA79A2C, `+0x3C`=0xA79A78
  (used by `0xA44630`).
- no plugin: `[0x1040170]` = `0x103DB90`; `0xA54E70 add ip,ip,#8` -> **vtable
  `0x103DB98`** (`0xA54E78 str ip,[fp]`). Slots: `+0x00`=0xA7936C, `+0x10`=0xA527E4,
  `+0x14`=0xA7915C, ..., `+0x38`=0xA79088, `+0x3C`=0xA7935C.

Both are EXACT_SOURCE for the vtable address and slot targets. The class names are
**UNKNOWN** (the vtables have no adjacent name string and no RTTI).

---

# UNKNOWN / RECOVERABLE_GAP list

1. **UNKNOWN** - Wwise class names of: the bus (`0x103ACE0`), the source subclasses
   (`0x103C844` etc.), the voice insert-FX slot (`0x103DC38`/`0x103DB98`), the bus
   insert-FX effect (registry-assigned), the 0x24-byte helper (`0x103D538`), the V26
   group object and the V28 modulator manager. No RTTI/symbols/name strings.
2. **RECOVERABLE_GAP** - the bus insert-FX effect class: read the plug-in registry
   (base `0x108D9DC`, populated at init) and its create functions. Not statically
   resolvable in this pass.
3. **RECOVERABLE_GAP** - bus-metering bit-2 filter DSP identity and per-lane mapping
   (`0xA501B0..0xA50B54`, coefficients `0xA506C0..0xA507CC`). Needs the Wwise 2016.2
   SDK bus-meter source or a dynamic trace of `0xA4FEF8`. The scalar flow and store
   targets are exact.
4. **UNKNOWN** - identity of `0x9EEDA4` (called by `0xA01768`) and of the 4-bit
   next-source codes; identity of `[bus+0x1EC]/[bus+0x1F0]` and of the global
   `[0x108DE78]`; identity of `[0x108DA00]`/`[0x1052444]` (the `G` divisor in `0x9FD910`).
5. **UNKNOWN** - semantic names of the V7 `E4`/`E0` values and of the V26 fields
   (`+0x80..+0x88`, table descriptor, `+8` byte buffer, `+0x18` flags). Values and
   branches are exact; the names are not in the binary.
6. **UNKNOWN** - whether the shipped Cozmo media exercises the `0xA022E8`/`0xA0228C`
   states and the `0xA01768` attach path. Determined only by bank/capture analysis.

# Existing records contradicted / too weak

- **Contradicted: the modulator-evaluator report's coefficient recompute.** Its claim
  that `0x9E3630` yields `1-(2-cos)^2` and therefore a NaN sqrt for every real angle is
  wrong. The instruction is `vnmls.f64 d17,d11,d11` = `d11*d11 - 1.0`; the argument is
  `(2-cos w)^2 - 1 >= 0`, and the NaN handler `0x9E52E4` is defensive. Cite
  `0x9E3630` and `re-analysis/decomp/libcozmoEngine/009e/009e2bd0.c:337`.
- **Contradicted: the modulator-evaluator report's "NEON lane order" gap for
  `0x9E2BD0`/`0x9E52F8`.** Both bodies are scalar VFP; the only NEON is the two
  `vst1.32` node zero-inits at `0x9E5068`/`0x9E5128`. There is no lane order to recover.
- **Too weak: M6-022 / V26.** Its citation `0x9FDD90..0x9FDE30` does not cover
  `0x9FD910`; the completion handler is now read and is a table/random segment selector,
  not a simple "3-component interpolation" tail.
- **Too weak: M6-022 / V18b (bus insert-FX).** The row names `0xA4E974` but not the
  effect object's class/vtable, which is registry-assigned; the class name remains
  UNKNOWN and the record cannot be EXACT_SOURCE for the identity.
- **Partial: the voice-callees report Q5/V12.** It says the slot class is at `0xA54C50`;
  the actual stored vtable is `0x103DC38` (plugin present) / `0x103DB98` (no plugin),
  from GOT slots `0x1040174` / `0x1040170`. The per-slot methods are now tabulated.


