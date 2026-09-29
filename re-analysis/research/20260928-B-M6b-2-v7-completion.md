# B-M6b-2 completion: the unread V7 continuation `0xA552F0`

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode).
Every behaviour below is read from the raw instruction stream (`re-analysis/tools/arm_disasm.py`,
capstone ARM mode); the Ghidra tree was used to navigate only. Addresses are ELF VAs.

Scope: item 1 / C12 voice-callees Q4 of `re-analysis/research/20260928-B-M6b-2-missing-bodies.md`
(the continuation reached from `0xA552C8` when `bus->vt+0x3C` returns 1, via `0xA555E8`).
This pass reads `0xA552F0` in full, the sibling `0xA55218`, the budget step `0xA55228`, and the
selection at `0xA552C8`/`0xA555E8`.

Register naming in `0xA54F1C` (from the prologue `0xA54F1C..0xA54F34` and the body):
- `r4` = voice; `r5` = `[voice+0xD4]` = source (reused as a scratch flag later);
- `r6` = `[source+0xC]` (called "bus" by the earlier reports/inventory; see UNKNOWN 3);
- `r8` = `[voice+0xF0]` (id); `sb` = `r6+0xC`; `r7` = `params`;
- `s16` = `round([params+0xC] * [r6+0x164])` computed at `0xA55090..0xA550C8` (`ldrh r3,[r7,#0xc]`;
  `vmov s15,r3`; `vcvt.f32.u32`; `vmul.f32 s15,s15,s13` with `s13=[r6+0x164]`; `+/-0.5`; `vcvt.s32.f32`).

Legend: EXACT_SOURCE = body/step read from instructions; RECOVERABLE_GAP = plausibly recoverable
but not read here; UNKNOWN = not established.

---

## 1. `0xA552F0` - the continuation

`0xA552F0` is a small block, not a function: it is entered only from `0xA555E8` (`mov r2,r0; b
0xA552F0`, when the `bus->vt+0x3C` call at `0xA552C8` returned 1) and by fall-through from
`0xA552EC` (`mov r2,r5`, when that call returned neither 1 nor 2). It calls the object at
`voice+0x1C0` and either continues into `0xA55218` or takes the stop path.

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | Entry sets the third argument: from the `ret==1` branch `r2 = r0 = 1`; from the fall-through `r2 = r5` (which is 0 on this path, because `A=[voice+0xCD]&1` was 0 at `0xA551F8` and never rewritten). | `0xA555E8 mov r2,r0`; `0xA552EC mov r2,r5`; `0xA551F8 ands r5,r2,#1` | EXACT_SOURCE |
| 2 | `r3 = [voice+0x1C0]` - the vtable pointer of the object embedded at `voice+0x1C0`. | `0xA552F0 ldr r3,[r4,#0x1c0]` | EXACT_SOURCE |
| 3 | `r0 = voice+0x1C0` - the `this` pointer. | `0xA552F4 add r0,r4,#0x1c0` | EXACT_SOURCE |
| 4 | `r1 = [voice+0xE0]` (`E0`, the "next-source index"). | `0xA552F8 ldr r1,[r4,#0xe0]` | EXACT_SOURCE |
| 5 | `r3 = [r3+0x18]` - vtable slot `+0x18`. | `0xA552FC ldr r3,[r3,#0x18]` | EXACT_SOURCE |
| 6 | Call `[voice+0x1C0]->vt+0x18(&voice[0x1C0], E0, r2)`. | `0xA55300 blx r3` | EXACT_SOURCE |
| 7 | If the return is 1 -> `0xA55218` (set `r5=1`, continue to the shared `0xA5521C`). | `0xA55304 cmp r0,#1`; `0xA55308 beq #0xa55218` | EXACT_SOURCE |
| 8 | Otherwise (0, 2, or anything else) -> `0xA5530C`: `voice->vt+0x48(voice)`, then `r0=0`, epilogue, **return 0**. | `0xA5530C ldr r3,[r4]`; `0xA55310 mov r0,r4`; `0xA55314 ldr r3,[r3,#0x48]`; `0xA55318 blx r3`; `0xA5531C mov r0,#0`; `0xA55320 add sp,sp,#0x5c`; `0xA55328 pop {r4,r5,r6,r7,r8,sb,sl,fp,pc}` | EXACT_SOURCE |

### The callee's identity (resolved)

`[voice+0x1C0]` is set by the voice constructor: `[0x104017C]` (GOT) `+ 8`. The GOT slot holds
`0x103C118`, so the vtable is **`0x103C120`**.

- `0xA54858 ldr r3,[pc,r3]` with `r3=0x005EB91C` (`0xA547D0 ldr r3,[pc,#0xd8]` -> `0xA548B0`) and
  `pc=0xA54860` -> `0x104017C`; `0xA5485C add r8,r3,#8`; `0xA54860 str r8,[r4,#0x1c0]`.
- `0x104017C` on disk = `0x103C118`; `0x103C120+0x18 = 0x103C138` -> **`0xA4C620`**.

`0xA4C620` (the slot body) is a forwarder:

| step | what the original does | citation | classification |
|---|---|---|---|
| a | `r0 = [this+4]` = `[voice+0x1C4]` (the downstream node pointer). | `0xA4C620 ldr r0,[r0,#4]` | EXACT_SOURCE |
| b | If null -> return **1**. | `0xA4C624 cmp r0,#0`; `0xA4C628 beq #0xa4c638`; `0xA4C638 mov r0,#1`; `0xA4C63C bx lr` | EXACT_SOURCE |
| c | Else tail-call `[[voice+0x1C4]]->vt+0x18(inner, E0, r2)` (r1/r2 untouched). | `0xA4C62C ldr r3,[r0]`; `0xA4C630 ldr r3,[r3,#0x18]`; `0xA4C634 bx r3` | EXACT_SOURCE |

`[voice+0x1C4]` is initialised to 0 in the constructor (`0xA54854 str r5,[r4,#0x1c4]`, `r5=0`) and
written by the wrapper's own `vt+0x24` = `0xA52678` (`str r1,[r0,#4]; bx lr`). `0xA54A30` calls that
slot while building the source/insert-FX chain:

- The chain array is based at `sp+0x4C` with the source at index 0 (`0xA54A44 str r2,[sp,#0x4c]`);
  the `voice+0x1C0` wrapper is placed at index `local_88` (`0xA54B6C add r4,r5,#0x1c0`;
  `0xA54D40 str r4,[r3,#-0x24]`). The loop then calls each object's `vt+0x24` with the previous
  entry: `0xA54D98 ldr r0,[r3,#-0x24]` (current object), `0xA54DA0 ldr r1,[r3,#-0x24]` (previous
  object), `0xA54DA4 ldr r3,[r0]`, `0xA54DA8 ldr r3,[r3,#0x24]`, `0xA54DAC blx r3`
  (`0xA54D8C..0xA54DB8`).

So on the live path, after a successful `0xA54A30` start-stream/insert-FX build, `[voice+0x1C4]`
points at the entry below the wrapper in that chain (a slot object, or whatever entry precedes it),
and
`vt+0x18` forwards the `(E0, r2)` request to it. The identity of that downstream node (its class
and what its own `vt+0x18` does) is **UNKNOWN** here (the slot class is the registry-assigned
Wwise plug-in from `0x9CC2AC`; the downstream node's own `vt+0x18` was not read).

### The sibling `0xA55218`

`0xA55218` is exactly `mov r5,#1` (`0xA55218 mov r5,#1`), falling into `0xA5521C`
(`0xA5521C ldrb r3,[r4,#0xe8]`). It writes `r5` only; it does not call anything.

**How the two differ.**
- `0xA55218` unconditionally sets the "continue" flag `r5=1` and proceeds to the `E8` gate at
  `0xA5521C`.
- `0xA552F0` first performs the `[voice+0x1C0]->vt+0x18(&voice[0x1C0], E0, r2)` request and reaches
  the same `0xA55218` (and `r5=1`) **only if that call returns 1**. On any other return it does not
  reach `0xA55218`: it calls `voice->vt+0x48(voice)` and returns 0 (`0xA5530C`).
- `0xA552F0` consumes the `r2` argument (1 from `0xA555E8`, 0 from `0xA552EC`); `0xA55218` ignores
  `r2` (it is not written on that path).

---

## 2. `0xA55228` - the budget step

Reached only from `0xA55224` (`tst r3,#1` at `0xA55220`, bit clear) or from `0xA553D4` (after the
`E8`-set path at `0xA553A4` clears bit0 at `0xA553CC`/`0xA553D0`).

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `r2 = s16` (the sample count `round([params+0xC]*[r6+0x164])`). | `0xA55228 vmov r2,s16` | EXACT_SOURCE |
| 2 | `r3 = [r6+0x1D8]` (the budget / remaining-offset field). | `0xA5522C ldr r3,[r6,#0x1d8]` | EXACT_SOURCE |
| 3 | If `budget >= s`: set `r5 = 0`; else leave `r5 = r5 & 1`. | `0xA55230 cmp r3,r2`; `0xA55234 movge r5,#0`; `0xA55238 andlt r5,r5,#1` | EXACT_SOURCE |
| 4 | If `budget >= 0`: `budget = budget - s` and store it back. If `budget < 0` nothing is written. | `0xA5523C cmp r3,#0`; `0xA55240 rsbge r3,r2,r3`; `0xA55244 strge r3,[r6,#0x1d8]` | EXACT_SOURCE |

Effect: it consumes `s` from the `[r6+0x1D8]` budget when that budget is non-negative, and clears
the "start this frame" flag `r5` when the budget was at least `s`. This is the same behaviour as
the existing M6 record `2.5` in `re-analysis/inventory/M6-wwise-bank.md:909` ("If +0x1D8 >= 0 it
subtracts s, and the source starts this frame only if the old value was < s").

**What field gates it.** The native gate is **`[voice+0xE8]` bit 0**, tested at `0xA5521C`:
`0xA5521C ldrb r3,[r4,#0xe8]`; `0xA55220 tst r3,#1`; `0xA55224 bne #0xa553a4`. If bit0 is set,
`0xA55228` is not the next instruction; the `0xA553A4` path runs first
(`bus->vt+0x3C(E0)` at `0xA553A4..0xA553B4`, then `0xA553C8 ldrb r3,[r4,#0xe8]`,
`0xA553CC bfc r3,#0,#1`, `0xA553D0 strb r3,[r4,#0xe8]`, `0xA553D4 b #0xa55228`).

**Does the native run it with the gate field zero?** Yes, always. `0xA55228` is only ever reached
with `[voice+0xE8]` bit0 clear: directly from `0xA55224` (bit already clear) or from `0xA553D4`
(the `E8`-set path clears bit0 at `0xA553CC`/`0xA553D0` first). There is no instruction that enters
`0xA55228` with bit0 set, so on every live path the budget step executes with the gate bit 0.

**There is no `[r6+0x164]` gate in the native.** The sample scale is read at `0xA55098`
(`vldr s13,[r6,#0x164]`) and multiplied unconditionally at `0xA550AC`; if it were 0, `s` would be
0 and `0xA55228` would still run: `cmp r3,r2` with `r2=0` sets `r5=0` whenever `budget >= 0`, and
`0xA55240` stores `budget - 0`. The native has no branch on `[r6+0x164]`. Whether `[r6+0x164]` is
ever 0 in the shipped media is not established (the writer of that field is the existing
RECOVERABLE_GAP noted on M6 line 909: "pbi+0x164 = 1.0 at ctor 0xA00228; other writers were not
traced"). The C# gate at `WwiseVoiceBusEngine.cs` ("when it is 0 (unset) the budget step is
skipped") is therefore not in the native at this point; the native step runs for every value of
`[r6+0x164]`.

The surrounding `0xA551E0..0xA5524C` is: the tail of the four parameter ramps
(`0xA551E0..0xA551EC`), the state-machine head (`0xA551F0..0xA55214`), the shared continue
(`0xA55218`), the `E8` gate (`0xA5521C..0xA55224`), then this budget step (`0xA55228..0xA55244`),
then the `E4`/`A`/`P2F` acquire/release dispatch (`0xA55248..0xA55268`).

---

## 3. `0xA552C8` / `0xA555E8` - the exact selection

`0xA552F0` is reached only through `0xA552C8`. The precondition to be at `0xA552C8` is:

- `A = [voice+0xCD] & 1 == 0` (`0xA551F8 ands r5,r2,#1`; `0xA551FC beq #0xa552b0`; the `A!=0`
  branch goes to `0xA55200` and never reaches `0xA552C8`), **and**
- `E4 = [voice+0xE4] == 2` (`0xA552B0 ldr r3,[r4,#0xe4]`; `0xA552B4 cmp r3,#2`; `0xA552B8 bne
  #0xa55218`), **and**
- `SRC10 = [source+0x10] & 1 != 0` (`0xA552BC ldrb r3,[r0,#0x10]` with `r0=[r4,#0xd4]`;
  `0xA552C0 tst r3,#1`; `0xA552C4 beq #0xa554f0`).

Then the call and the two exits:

| step | what the original does | citation | classification |
|---|---|---|---|
| 1 | `r3=[r6]` (bus vtable), `r0=r6`, `r1=[r4,#0xe0]` (`E0`), `r3=[r3,#0x3c]`, call `bus->vt+0x3C(bus, E0)`. | `0xA552C8 ldr r3,[r6]`; `0xA552CC mov r0,r6`; `0xA552D0 ldr r1,[r4,#0xe0]`; `0xA552D4 ldr r3,[r3,#0x3c]`; `0xA552D8 blx r3` | EXACT_SOURCE |
| 2 | Return `== 1` -> `0xA555E8` (`mov r2,r0` = 1) -> `0xA552F0`. | `0xA552DC cmp r0,#1`; `0xA552E0 beq #0xa555e8`; `0xA555E8 mov r2,r0`; `0xA555EC b #0xa552f0` | EXACT_SOURCE |
| 3 | Return `== 2` -> `0xA5530C`: `voice->vt+0x48(voice)`, return 0. (`0xA552F0` is **not** reached.) | `0xA552E4 cmp r0,#2`; `0xA552E8 beq #0xa5530c`; `0xA5530C..0xA55318` | EXACT_SOURCE |
| 4 | Any other return -> `0xA552EC mov r2,r5` (=0, since `A==0`) -> `0xA552F0`. | `0xA552EC mov r2,r5`; fall-through to `0xA552F0` | EXACT_SOURCE |

So the condition that puts control at `0xA552F0` rather than directly at `0xA55218` is:
`(A==0) && (E4==2) && (SRC10 bit0==1) && (bus->vt+0x3C(E0) != 2)`.

`0xA552F0` then reaches `0xA55218` only if the wrapper call returns 1; otherwise it goes to
`0xA5530C` and returns 0. A `bus->vt+0x3C` return of 2 short-circuits to `0xA5530C` and never
executes the `0xA552F0` wrapper call.

**Register/field values at `0xA552C8`:** `r4`=voice, `r6`=`[source+0xC]`, `r0`=r6, `r1`=`E0`,
`r5`=0, `A=0`, `E4=2`, `SRC10 bit0=1`, `s16`=s, `[sp+0x2f]`=`P2F` (the byte produced by `0xA4BC58`
at `0xA55058` and loaded at `0xA5509C`). At `0xA552F0` the wrapper receives `(this=&voice[0x1C0],
E0, r2)` with `r2=1` (return 1) or `r2=0` (other return).

---

## UNKNOWN / RECOVERABLE_GAP

1. **UNKNOWN - downstream node of the `voice+0x1C0` wrapper.** `[voice+0x1C4]` is set by
   `0xA52678` from `0xA54A30`; which node it points at depends on how many insert-FX slots were
   collected, and the slot class is the runtime plug-in registry (`0x9CC2AC`, base `0x108D9DC`,
   not statically resolvable). Its `vt+0x18` body is unread.
2. **UNKNOWN - the `r2` argument's meaning.** `r2` is 1 when `bus->vt+0x3C` returned 1 and 0 on the
   non-1/non-2 fall-through; it is passed unchanged into `[voice+0x1C4]->vt+0x18`. No symbol, RTTI
   or string names it.
3. **RECOVERABLE_GAP - the writer of `[r6+0x164]`.** Whether it is ever 0 (and so whether the
   budget step ever runs with `s==0`) is not established. Existing M6 record line 909 marks the
   writers of `pbi+0x164` untraced except the ctor `0xA00228` (=1.0). Note `r6=[source+0xC]`, so
   the offset may not be the same object the record calls `pbi`.
4. **UNKNOWN - semantic names of `E4`, `E0`, `r2`.** Values and branches are exact; the names are
   not in the binary (carried over from the earlier voice-callees report).
5. **UNKNOWN - whether the shipped Cozmo media exercises the `bus->vt+0x3C` returns 1 / other
   paths** that lead through `0xA552F0`. The `.so` alone cannot decide this (would need the
   bank/capture).

## Existing records affected

- **`20260928-B-M6b-voice-callees.md` Q4 lines 186-189 (and M6-022 / V7/C1) - partial.** They
  correctly identify `0xA552F0` and `0xA555E8`, but describe the call as
  `[voice+0x1C0]->vt+0x18(E0)` (no third argument), do not read the slot body `0xA4C620`, and do
  not state that the return-2 case at `0xA552E8` bypasses `0xA552F0` entirely. The added facts:
  `r2` is a real argument (1/0), the wrapper vtable is `0x103C120` (from `[0x104017C]+8`), the slot
  is `0xA4C620`, and `[voice+0x1C4]` is the forwarded pointer.
- **`20260928-B-M6b-2-missing-bodies.md` item 1 - now closed for `0xA552F0`.** Its statement that
  this continuation's body "is not read" is superseded by this report.
- **`WwiseVoiceBusEngine.cs` (`Path552F0` / budget step).** The C# gates the budget step on
  `bus.SampleScale164 > 0f`; the native has no such gate (the native gate is `[voice+0xE8]` bit0
  at `0xA5521C`). This is a behaviour difference to be checked by the verifier, not judged here.


