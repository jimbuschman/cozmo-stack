# M6b bounded gap pass - voice-engine callee bodies

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode).
All addresses are file/VA in the `.so` (for `.text`/`.rodata` VA == file offset; `.data` is
VA-0x500 on disk). Instructions were read with `re-analysis/tools/arm_disasm.py`; every
behaviour below was checked against the raw instruction stream. The Ghidra tree was used only
to navigate. The manifest/inventory text is a claim, not evidence.

Scope: the ten named callee bodies. Classification: EXACT_SOURCE = body read; RECOVERABLE_GAP /
UNKNOWN = named exactly.

---

## Q1 - V3/N1: the output-device node's `vt+0x2c` and `vt+0x30` (render body pre-loop)

**Answer.** The render body `0xA44D4C` walks the global output-device list and, per list node
`r4`, loads the sub-object `r0 = [r4+0x70]`, calls `[r0]->vt+0x2c`, and if that returns non-zero
calls `[r0]->vt+0x30`.

The list head is `[0x108DB04]` (the output-device state struct base `0x108DAE8` + 0x1C, per G1);
next is `[node+4]`. `0xA44D5C: ldr r3,[r5,r3]` -> `0x108DAFC`; `0xA44D60: ldr r4,[r3,#8]` -> head;
`0xA44D70: ldr r4,[r4,#4]` -> next; `0xA44D7C: ldr r0,[r4,#0x70]`; `0xA44D80/0x84: ldr r3,[r0];
ldr r3,[r3,#0x2c]`; `0xA44D88: blx r3`; `0xA44D94..0xA44DA0` repeat with `[r3,#0x30]`.

The list node class is constructed by the function that starts before `0x9E9B20` (it sets the
node's vtable and initialises `+0x70 = 0`, `+0x74/+0x78 = 1.0`, `+0x84 = 0`). Its vtable pointer
is stored at `0x9E9B70: str r5,[ip]` where `r5 = pc + 0x00651924` at `0x9E9B64` (pc `0x9E9B6C`)
`+ 8` = **0x103B498**. This is the only constructor that stores that vtable and the class has no
RTTI or symbol, so its Wwise name stays UNKNOWN. The vtable slots are:

- **`vt+0x2c` = `0x9E935C`**: `0x9E935C: ldrb r0,[r0,#0x84]; 0x9E9360: bx lr`. Returns the byte at
  `[this+0x84]` (a per-node "data/ready" byte; the constructor writes it at `0x9E9BE0:
  strb r8,[ip,#0x84]`, and `0x9E9C10`/`0x9E9C74` maintain it).
- **`vt+0x30` = `0x9E9420`**: `push {r3,lr}; ldrb r3,[r0,#0x84]; cmp r3,#0; bne 0x9E9438;
  mov r0,#2; pop {r3,pc}` -- i.e. if `[this+0x84]==0` return **2**; else `0x9E9438..0x9E9448`
  loads the manager pointer and `bl 0xA40924` (the `sem_post` helper, `0x00A40924`) and
  `0x9E944C: mov r0,#1` -> return **1**.

So the pre-loop's `vt+0x2c` is a "is this device ready" predicate on `[obj+0x84]`, and `vt+0x30`
is the "kick the device / post the output-device semaphore" action returning 1 (posted) or 2
(nothing to do). This matches the ready/signal pair the audio thread consumes.

**Caveat / residual.** The pre-loop dereferences `[node+0x70]`, not the list node itself. The
node constructor sets `+0x70 = 0` and a separate vtable method (`0x9E9C0C`) sets `[node+0x70] =
r1`. No direct `bl` to `0x9E9C0C` exists (it is a virtual method), and the sub-object's own
vtable store was not located. If `[node+0x70]` is an instance of the same class, the two bodies
above are the slots; if it is a distinct sink class, its `+0x2c/+0x30` are unread. The class
identity (both) is UNKNOWN (no RTTI/symbols; `.dynsym` and `.rodata` have no Wwise class name).

**Citations.** `0xA44D4C..0xA44DA0` (pre-loop: `0xA44D5C/60/70/7C/80/84/88/94/98/9C/A0`);
`0x9E9B70` (vtable store), `0x103B498` (vtable), `0x103B498+0x2C -> 0x9E935C`,
`0x103B498+0x30 -> 0x9E9420`; `0x9E935C..0x9E9360`, `0x9E9420..0x9E944C`; `0x9E9C0C/10/18`
(the `+0x70` setter).

**Classification.** vtable address, slot targets and both bodies EXACT_SOURCE. The
`[node+0x70]` sub-object identity and its vtable: **UNKNOWN / RECOVERABLE_GAP** -- read the
caller that sets `[node+0x70]` and the object it passes (the node's virtual method at vtable
`0x103B498` that contains `0x9E9C0C`), then the sub-object's vtable store.

---

## Q2 - V5: `0xA43D24` ducking/volume pre-pass

**Answer.** Confirmed and extended. `0xA43D24` is called by the voice pass at `0xA4497C`
(`bl 0xA43D24`) right after `0x9D3CC0`. It has five ordered phases:

Manager base: `0xA43D24: ldr r3,[pc,#0x220]` (lit `0x0064A220`), `0xA43D28: push`, `0xA43D2C:
add r3,pc,r3` with pc `0xA43D34` -> **0x108DF50**. Fields used: `[0x108DF50+4]=0x108DF54`
(bus/voice count), `[+0x14]=0x108DF64` (voice list head), `[0x108DF4C]` (bus array base).
Voice nodes: next `+0xD0`, state `+0xDC`.

1. **Duck/stop voices** (`0xA43D40..0xA43D68`): walk head `[0x108DF64]` via `+0xD0`; for each
   node with `[node+0xDC]==1` (`cmp r3,#1`, `0xA43D4C/50`) call `0xA55750(node)`
   (`0xA43D5C: bl 0xA55750`).
2. **Per-bus ducking/volume** (`0xA43D6C..0xA43E28`): `r5 = [0x108DF54]` (count),
   `r1 = [0x108DF4C]` (array base), `ip = r1 + r5*4` (end). For each pointer `r3` in
   `[r1..ip)`:
   - `r2 = [r3+0x1C8]`; `s15 = (r2 ? [r2+0x88] : 0.0) + [r3+0x90]`;
   - `[r3+0x88] = s15` (`vstr s15,[r3,#0x88]`);
   - `s13 = s15 * 0.05` (`0x3D4CCCCD`);
   - if `s13 < -37.0` (`0xC2140000`, `vcmpe`/`bmi 0xA43E20`) then the linear value stays 0
     (`s14 = 0x00000000`), else it is linearised with the fast-pow bit trick
     (`0x4BD49A78`=27866352.0, `0x4E7E0000`=1065353216.0, `0x3EA67F46`, `0x3CAA70DE`,
     `0x3F272DDB`);
   - `[r3+0x8C] = linear` (`0xA43E24: vstr s14,[r3,#0x8c]`).
   Exact order: read `[r3+0x1C8]`, read `[r3+0x90]`, sum, store `+0x88`, scale, clamp/pow,
   store `+0x8C`.
3. **Apply ducking to voices** (`0xA43E2C..0xA43E60`): walk the voice list head `[0x108DF64]`
   via `+0xD0`; for `[node+0xDC]==1` call `0xA4AF50(node)` (`0xA43E54`).
4. **Retire flagged buses** (`0xA43E64..0xA43EB4`): `r5 = [0x108DF54]-1`, descending over the
   array `[0x108DF4C]`; for `obj = array[i]` with `[obj+0x1CC] & 2` (`0xA43EA0/EA4`) call
   `0xA437E0(obj)` (`0xA43EAC`).
5. **Per-voice post pass** (`0xA43EB8..0xA43EF8`): walk the voice list again; for
   `[node+0xDC]==1` call `0xA4B4B0(node)` (`0xA43EEC`).

So the B1-V5 summary ("walks the voice list and calls 0xA55750 per active voice, then computes
per-bus ducking/volume into +0x88/+0x8C with constants 0.05 and -37.0") is correct but partial:
the exact phases are `0xA55750` voices -> bus `+0x88/+0x8C` -> `0xA4AF50` voices -> `0xA437E0`
flagged buses (descending) -> `0xA4B4B0` voices. The B1 wording "voice list base 0x108DF54" is
off by 4: the container base is **0x108DF50**, the count is at `0x108DF54`, the head at
`0x108DF64` (this also contradicts V6's "count [base+0xC]" if base is read as 0x108DF54; with
base 0x108DF50 V6's count `+0xC`=0x108DF5C is the voice count set by the voice pass, while
`0x108DF54` is the bus-array count).

**Citations.** `0xA43D24..0xA43EFC`; key: `0xA43D2C` base -> 0x108DF50; `0xA43D5C bl 0xA55750`;
`0xA43D84` array base -> `[0x108DF4C]`; `0xA43DC0..0xA43E24` arithmetic (consts
`0x3D4CCCCD`, `0xC2140000`, `0x4BD49A78`, `0x4E7E0000`); `0xA43E54 bl 0xA4AF50`;
`0xA43EA0/EA4/AC` (`+0x1CC & 2` -> `0xA437E0`); `0xA43EEC bl 0xA4B4B0`.

**Classification.** EXACT_SOURCE.

---

## Q3 - V5: `0xA39564` node cleanup

**Answer.** Called by the voice pass at `0xA44980` (`bl 0xA39564`). Manager base computed at
`0xA39568` (lit `0x00654904`) `+ 0xA39574` = **0x108DE78**. Fields: `+0x50` = list head
`0x108DEC8` (nodes with next `+0x104`, flags `+0x1BE`), `+0x54` = array base `0x108DECC`,
`+0x58` = array count `0x108DED0`, `+0x60` = byte flag `0x108DED8`, `+0x61` = byte flag
`0x108DED9`.

Control flow:
1. `r2 = [mgr+0x60]`; if 0 -> `0xA39588`; else `r2 = [mgr+0x61]`; if 0 -> `0xA39600`; else
   `0xA39588`.
2. **`0xA39588` path**: `r3 = [mgr+0x50]`; loop `0xA395A8`: while `r3 != 0`, clear bit 2 of
   `[r3+0x1BE]` (`0xA39598: ldrb r2,[r3,#0x1be]; 0xA3959C: bfc r2,#2,#1; 0xA395A0: strb`),
   `r3 = [r3+0x104]`.
3. **`0xA39600` path** (only when `[mgr+0x60]!=0 && [mgr+0x61]==0`): `r4 = [mgr+0x50]`; loop
   `0xA3960C`: while `r4 != 0`, call `0xA00494(r4)` (`0xA39610`), `r4 = [r4+0x104]`. Then
   `[mgr+0x60] = 0` (`0xA39638: strb r1,[r2,#0x60]`), reload `r3 = [mgr+0x50]`, and fall into
   the `0xA395A8` bit-2-clear loop.
4. **Array pass** (`0xA395B0..0xA395FC`): `r3 = [mgr+0x58]` (count) `<< 2`; if 0 return.
   `r4 = [mgr+0x54]` (array base); loop over each pointer `r0 = [r4], r4 += 4`; if `r0 != 0`
   call `0x9F3BA4(r0)` (`0xA395E0`); until `r4 == r1 + count*4`.

So it (a) clears the bus `+0x1BE` bit 2 on every node of the list `0x108DEC8`, optionally
calling `0xA00494` on each and clearing the flag `0x108DED8`, then (b) calls `0x9F3BA4` on each
non-null element of the array `0x108DECC` (count `0x108DED0`).

**Citations.** `0xA39564..0xA395FC`; `0xA39568/6C` base -> 0x108DE78; `0xA39570/7C` flags
`+0x60/+0x61`; `0xA39590` head `+0x50`; `0xA3959C` `bfc #2,#1`; `0xA39610 bl 0xA00494`;
`0xA39638` clear flag; `0xA395B0/BC` array `+0x54/+0x58`; `0xA395E0 bl 0x9F3BA4`.

**Classification.** EXACT_SOURCE.

---

## Q4 - V7/C1: the per-connection state machine in `0xA54F1C`; plus `0xA4B4B0`, `0xA4BC58`

### `0xA54F1C(voice, params)`

**Prologue** (`0xA54F20..0xA54F4C`): `source=[voice+0xD4]`, `id=[voice+0xF0]`,
`bus=[source+0xC]`, `r2=[bus+0x1F8]`. If `r2 == -1` skip; else `[params+0x2C]=1`; and if
`r2 == 0` return **0** (`0xA5531C`).

**Gain** (`0xA54F50..0xA54FD0`): `s = ([bus+0x54]+[bus+0x11C])*0.05`; if `s < -37.0` the
linear gain is 0, else fast-pow. Then if `[source+8]`: `s *= [[source+8]+4]`; if
`[bus+0x58]&1`: `s *= [source+8]`. (`0x3D4CCCCD`, `0xC2140000`, pow constants as above.)

**Source call + connection update** (`0xA54FF0..0xA55058`): call `source->vt+0x4C`
(`0xA55008: ldr r3,[r3,#0x4c]`, `0xA5500C: blx`); then `0xA4BC58(voice, bus+0xC, id, s, ...)`
(`0xA55058`). After it, if `[sp+0x2e]!=0`, copy `[voice+0x344]`, `[voice+0x524]`,
`[voice+0x524]`, `[voice+0x354]` into `sp+0x30/+0x34/+0x38/+0x3C`.

**Sample count** (`0xA55090..0xA550C4`): `s16 = round([params+0xC] * [bus+0x164])`
(`vcvt.f32.u32`, `+0.5`/`-0.5` then `vcvt.s32.f32`). `r5 = [sp+0x2f]` (a byte from
`0xA4BC58`).

**Four parameter ramps** (`0xA550CC..0xA551F0`, only if `[voice+0x28]` (connection list) != 0):
targets `sp+0x30/34/38/3C` against current `[voice+0x344]`, `[voice+0x514?]`, `[voice+0x354]`,
`[voice+0x524]`; clamp to 100.0 (`0x42C80000`); ramp `cur += (target-cur)*0.125*(u16 at +8)`;
set `[voice+0x34b]/[voice+0x51b]/[voice+0x35b]/[voice+0x52b] = 1` (`0xA55450`, `0xA553E4`,
`0xA551CC`, `0xA5541C`).

**State machine** (`0xA551F0..0xA5574C`). State variables: `A = [voice+0xCD] bit0`,
`E4 = [voice+0xE4]`, `E0 = [voice+0xE0]`, `E8 = [voice+0xE8] bit0`,
`SRC10 = [source+0x10] bit0`, `P2F = [sp+0x2f]`, `N = [bus+0x1D8]`, `s = round(...)` above,
`C = [voice+0xCC]`. Branch table:

- `0xA551F0`: `A = [voice+0xCD] & 1`. If `A==0` -> `0xA552B0`; else -> `0xA55200`.
- `0xA55200`: if `SRC10!=0` -> `0xA55218`; else `E4=[voice+0xE4]`; if `E4==2` -> `0xA554F0`;
  else -> `0xA55218`.
- `0xA552B0`: if `E4!=2` -> `0xA55218`; else (E4==2) if `SRC10==0` -> `0xA554F0`; else -> `0xA552C8`.
- `0xA552C8` (E4==2, SRC10 set, A==0): `0xA552C8..0xA552D8` call **`bus->vt+0x3C(E0)`**
  (`0xA552D0: ldr r1,[r4,#0xe0]; 0xA552D4: ldr r3,[r3,#0x3c]; 0xA552D8: blx`).
  - returns 1 -> `0xA555E8` -> `0xA552F0`;
  - returns 2 -> `0xA5530C`: `voice->vt+0x48` (`0xA55314: ldr r3,[r3,#0x48]`) then return 0;
  - else `0xA552EC`: **`[voice+0x1C0]->vt+0x18(E0)`** (`0xA552FC: ldr r3,[r3,#0x18]`);
    returns 1 -> `0xA55218`; else `0xA5530C`.
- `0xA554F0` (E4==2, SRC10 clear): `0xA56650(source, [bus+0x1DC], [bus+0x1E0])` (`0xA554F8`);
  - returns `0x3F` -> `r5=0`, `[sp+0x2f]=0`, -> `0xA5521C`;
  - returns 1 -> `0xA55218`;
  - else `voice->vt+0x48`, `r5=0`, `[sp+0x2f]=0`, -> `0xA5521C`.
- `0xA55218`: `r5=1`; if `E8` (`0xA5521C: ldrb r3,[r4,#0xe8]; tst #1`) set -> `0xA553A4`:
  **`bus->vt+0x3C(E0)`** again (`0xA553AC/0xB0/0xB4`);
  - returns 1 -> `0xA554CC`: `voice->vt+0x58` (`0xA554D8`), then `0xA4C584(voice, r0)`
    (`0xA554E8`), then `0xA553C8`;
  - returns 2 -> `0xA555A0`: `voice->vt+0x48`, `r5=0`, -> `0xA553C8`;
  - else `0xA553C8`: clear `[voice+0xE8]` bit0; -> `0xA55228`.
- `0xA55228`: `s` (s16); `r3=[bus+0x1D8]`; if `r3 >= s` set `r5=0`; if `r3 >= 0` store
  `[bus+0x1D8] = r3 - s` (`0xA5523C..0xA55244`). Then `r3=[voice+0xE4]`, `r2=[sp+0x2f]`;
  if `r3==0` -> `0xA5526C`; else if `[voice+0xCD]&1==0` -> `0xA55384`; else if `r2==0` ->
  `0xA5547C`.
  - `0xA55384`: if `r2!=0` call `0xA022E8(bus, 1)` (`0xA55398`); -> `0xA5526C`.
  - `0xA5547C`: `0xA0228C(bus)` (`0xA55484`); -> `0xA5526C`.
- `0xA5526C`: `[voice+0xCD] bit0 = P2F` (`bfi r3,r2,#0,#1; strb`); if `r5==0` -> `0xA5528C`;
  else if `[voice+0x1B4]==0` -> `0xA555B8`.
- `0xA555B8`: `0xA54A30(voice)` (`0xA555C0`); if returns 1 -> `0xA555F0`; else
  `voice->vt+0x48`, -> `0xA5528C`.
- `0xA555F0`: if `[bus+0xE8]&0x20` and `[bus+0xE9]&1`: call `[[bus+0xC]]->vt+0x28` on `bus+0xC`
  (`0xA5560C..0xA55618`); then `[bus+0xC4] = 101.0` (`0x42CA0000`, `0xA55624/28`),
  `[params+4] = [voice+0xF0]`, clear `[voice+0xCD]` bit3 (`0xA5563C/40`), call
  `0xA4B4B0(voice)` (`0xA55644`), then recompute the gain and call `0xA4BC58` again
  (`0xA55648..0xA5572C`), -> `0xA5528C`.
  If `[bus+0xE8]&0x20 == 0` -> `0xA5573C`: `[[bus+0xC]]->vt+0x24` on `bus+0xC` (`0xA5573C..48`)
  -> `0xA5561C`.
- `0xA5528C`: `[voice+0xCD] |= 8`; return `r5`.
- `0xA5532C` (the `r5==0` path, from `0xA550C8`): `r3=[voice+0xE4]`, `r2=[voice+0xCD]`;
  - `r3==2` -> `0xA55490`: if `[voice+0xCD]&1` -> `0xA55534`; else if `[voice+0xE0]==1` ->
    `0xA5556C`; else `r5=0`, -> `0xA5521C`.
    - `0xA55534`: `[voice+0x1C0]->vt+0x14(E0)` (`0xA55544/48`); if `[voice+0xE0]==2` ->
      `0xA554A4` (`r5=0`); else `[voice+0x1C0]->vt+0xC` (`0xA55560/64`), -> `0xA55498`.
    - `0xA5556C`: if `[bus+0x1D8] < s` call `[voice+0x1C0]->vt+0x10(&s)` (`0xA5557C..0x94`),
      store result in `[params+0x28]`; `r5=0`; -> `0xA5521C`.
  - `r3==1` -> `0xA55344`: `voice->vt+0x48` (`0xA5534C`), -> `0xA5521C`.
  - else -> `0xA55218`.
- `0xA554A4`/`0xA5521C`: `r5=0`; `[voice+0xCD] |= 8`; return 0.

**Return semantics.** Returns `r5` (0 or 1): 1 = the voice has a live source this frame (the
voice pass then calls `0xA44630`); 0 = not. `params+0x2C` is set to 1 when `[bus+0x1F8]` is
present, and `params+0x28` is set to the DSP result code by `0xA53134`/`0xA52D4C`/`0xA548C0`.
The semantic names of `E4`/`E0` (0/1/2) are not in the binary; from `0xA558AC` `E4` is set from
`0xA01768(bus, &E0)` and `E0` is the index passed to `bus->vt+0x3C`/`voice+0x1C0->vt+0x18/14/10`,
and C10 writes `E4=2, E0=1` on duck/stop, so `E4` is a next-source request/result code and `E0`
the next-source index. The names are inference; the values and branches are EXACT_SOURCE.

### `0xA4B4B0(voice)` ducking apply

`r3=[voice+0xC]`; if 0 return. `s15=[r3+0x88]` (bus ducking sum), `s13=[r3+0x1E0]`,
`s14=[voice+0x20]` (voice output dB); `s13 += s15`; `s14 -= s13`; if `s14>0`: `s13 +=
s14*[r3+0x1E4]`. `s12=[r3+0x1D8]`; if `s12<s13`: `s12=s13`. `s15 -= s12`; `s14 = s15*0.05`;
if `s14 < -37.0`: `s14=0`, else fast-pow. `s12 = s14*[voice+0x1C]`. If `[voice+0x28]!=0`, walk
the connection list: for each `r3`, `s15 = s14*[r3+0x60]`; `[r3+0x60]=s15`; if `s15 <= g`
(`[0x108...]` via GOT, a threshold) `r1=1` else `r1=0`; set `[r3+0x6C]` bit1 = r1
(`bfi r2,r1,#1,#1`); `r3=[r3+0x28]`. Finally `[voice+0x1C]=s12`.

**Citations.** `0xA4B4B0..0xA4B5A8`; consts `0xC2140000`, `0x3D4CCCCD`, fast-pow constants;
`0xA4B550` connection walk; `0xA4B5A4: vstr s12,[r0,#0x1c]`.

### `0xA4BC58(voice, busChain, id, gain, ...)` per-connection gain/format update

Read in full (`0xA4BC58..0xA4C1xx`). Summary of the behaviour-changing steps:
- Initialise the four output floats (`[sp+0x5c]`, `[sp+0x60]`, `[sp+0x64]`, `[sp+0x68]` and
  `[sp+0x6c]`) to 0 (`0xA4BC90..0xA4BCAC`).
- Walk the connection list `[voice+0x28]` (next `+0x28`) to compute aggregate flags
  `sb`/`fp` from each `[conn+0x6C]` bits 1 and 2 (`0xA4BCC0..0xA4BCF0`).
- `0xA4BCF4: voice->vt+0x3C` (`0xA4BCFC: ldr r3,[r3,#0x3c]`) -- per-voice source request.
- If the result is 0 -> `0xA4C000`; else if `[voice+0xCD]&8` -> `0xA4C058`; else the
  per-connection loop `0xA4BD40..0xA4BFBC`: for each connection `fp`:
  - `0xA4BD54`: set `[conn+0x6C]` bit2 (`bfi r3,r0,#2,#1`).
  - `0xA4BE20`: if `[conn+0x64] != id` (`cmp r2,r6`) re-init (`0xA4BDDC`: `0xA67C58(conn+0x18)`,
    `[conn+0xc]=0`, `[conn+0x64]=0`, `0xA67B9C(conn+0x18, id, r5)`, `[conn+0x14]=0`).
  - `0xA4BE6C`: `[conn+0xc] = [voice+0x1C] * gain` (`vmul.f32 s15,s17,s16`, `s17=[voice+0x1c]`).
  - `0xA4BE80`: copy `[voice+0x3c]/[voice+0x40]` to `[conn+0x50]/[conn+0x58]` and zero
    `+0x54/+0x5c`; `0xA5975C(...)` (`0xA4BEC4`) is the per-connection conversion step.
  - `0xA4BEC8..0xA4BF34`: keep the minimum of `[conn+0x50/54/58/5C]` in the four output floats.
  - `0xA4BF38`: if `[sp+0x6c]!=0` call `0xA5D70C([sp+0x6c], conn)` (`0xA4BF4C`).
  - `0xA4BF6C`: memcpy the connection's conversion buffers (`[conn+0x20]`, `[conn+0x24]`,
    count `[conn+0x64]`, stride from `[conn+0x30]+0x64`) via `0x4D37F0`; then `[conn+8]=
    [conn+0xc]`, `[conn+0x10]=[conn+0x14]`.
- `0xA4BFC4`: copy `[voice+0xA8..0xB4]` to `[voice+0xB8..0xC4]` (the "propagate" step).
- `0xA4BFD4`: clear `[voice+0xCD]` bit2 and `[voice+0xDC]` bit4.

This confirms F1 (M6-011 evidence): `0xA4BC58` is the connection gain/format step, not the
filter. The filter body is `0xA4C60C -> 0xA766B8 -> 0xA766F0/0xA77480`.

**Citations.** `0xA4BC58..0xA4C000`; `0xA4BC68 ldr r0,[r0,#0x28]`; `0xA4BCFC` voice->vt+0x3C;
`0xA4BD54 bfi #2`; `0xA4BE6C vmul`; `0xA4BEC4 bl 0xA5975C`; `0xA4BEC8..0xA4BF34` min;
`0xA4BFC4` propagation.

**Classification.** EXACT_SOURCE for the code read; the class names of `source`/`bus` are
UNKNOWN (no RTTI); `E4`/`E0` value names are inference.

---

## Q5 - V8: the voice render/mix dispatcher `0xA44630`

**Answer.** `0xA44630(params)` with `r7 = [params+0x30]` (the voice), `r8 = voice+0x100`.
Order and effects:

1. **Insert-FX slots 4..1** (`0xA44650..0xA4472C`): `r4=4`; loop `0xA44650`: `r0 =
   [voice + 0x36C + r4*4]` (so `voice+0x370` for slot 4 down to `voice+0x37C` for slot 1). If
   non-null call **`slot->vt+0x38(params)`** (`0xA4466C: ldr r3,[r3,#0x38]; 0xA44670: blx`).
   Then if `[params+0x28]==0x2B` advance; if `[params+0x28]` is 0x2D or 0x11 and `r4!=4`, loop
   the *remaining* slots from `r4` upward calling **`slot->vt+0x3C(params)`**
   (`0xA446B4: ldr r3,[r3,#0x3c]; 0xA446B8: blx`); if that returns 0x2B -> advance, 0x11/0x2D
   -> continue, else return. (`0xA4471C`, `0xA44724`.)
2. **Filter A**: `0xA446E0: add r0,r7,#0x1c0; 0xA446E8: bl 0xA4C60C` (`0xA4C60C` thunks to
   `0xA766B8` -> LPF `0xA766F0`, HPF `0xA77480`).
3. **Gain/ramp**: `0xA446EC: add r0,r7,#0x380; 0xA446F4: bl 0xA56E00` (`0xA56E00` thunks to
   `0xA56A7C`, the per-connection gain/ramp application).
4. **Source execute**: `0xA446F8: mov r0,r7; 0xA44700: bl 0xA548C0`.
5. If `[params+0x28]` is 0x11 or 0x2D -> step 8 (aux walk); else return (`0xA44714`).
6. If state 0x2B -> `0xA44730`: **`0xA53134(voice+0x100, params)`** (`0xA44738`) = resampler
   pitch. If state 0x2B then `0xA4475C`: load the global at GOT `0x108DF98` (the audio-route
   byte), `strh` it to `[params+0xC]`; then **`source->vt+0x30(params)`** (`0xA4478C:
   ldr r0,[r7,#0xd4]; 0xA447A0: ldr r3,[r3,#0x30]; 0xA447A4: blx`). If `[params+0x28]==0x2E`
   call `0xA55C14(voice)` (`0xA447B8`). Then if state 0x11/0x2D call
   **`0xA52D4C(voice+0x100, params)`** (`0xA4477C`) = resampler execute; if 0x2B loop back to
   the source `vt+0x30` call; else handle.
7. **`0xA447C4` notify + aux-send walk** (reached when state 0x11/0x2D): `0xA03E8C(manager,
   params)` (`0xA447D4`), where manager = the GOT global at `0x108...`. Then `r6 =
   [voice+0x28]` (connection list). For each connection:
   - if `[conn+0x68]==0` or `[conn+0x18]==0` or `([conn+0x6C]&6)==6` -> skip to next
     (`0xA448AC`).
   - else accumulate per-aux gains: `sb=[conn+0x30]`, `r8=sb+0x4C`; zero `sp+8/sp+0xC`; if
     `[voice+0xCC]!=0` loop `r4 = voice+0x2C` stride 0x14, count `[voice+0xCC]`, matching
     `0xA68A2C(r8)` against `[r4+0xC]`, adding `[r4]` into `sp+0xC` and `[r4+4]` into `sp+8`;
   - call **`0xA4FBEC(bus=[conn+0x30], params, conn, &gains)`** (`0xA448A8`).
8. **Dry-mix walk** (`0xA448B8..0xA44938`): `r4=[voice+0x28]`, `r8=1.0`, `sb=sp+8`. For each
   connection with the *opposite* predicate (`[conn+0x68]==0` or `[conn+0x18]==0` or
   `([conn+0x6C]&6)==6`): if this is the first dry connection (`r6==0`) call
   **`0xA4C60C(voice+0x390, params)`** (filter B, `0xA4492C`); then call
   **`0xA4FBEC([conn+0x30], params, conn, &{1.0,1.0})`** (`0xA448EC`).

So the V8 order is confirmed: insert-FX slots (`vt+0x38` then `vt+0x3C` on state 0x2D/0x11),
filter A (`0xA4C60C` on `voice+0x1C0`), gain/ramp (`0xA56E00` on `voice+0x380`), source execute
(`0xA548C0`), then (on 0x11/0x2D) pitch (`0xA53134`), `source->vt+0x30`, resampler execute
(`0xA52D4C`); then `0xA03E8C` notify, the aux-send walk (`0xA4FBEC` per aux connection), filter
B (`0xA4C60C` on `voice+0x390`) before the first dry mix, and the dry-mix walk (`0xA4FBEC` with
gain 1.0).

**`[r7+0xD4] vt+0x30`.** The object is the voice's source (`voice+0xD4`, the same object as
C1/V7's `source`, set by `0xA549A0`/`0xA558AC`). The slot is +0x30 of the source class vtable.
The source is one of the Wwise source-node subclasses created by the factory `0xA562B8`; the
factory dispatches on `(mode, r1>>16)` and each constructor stores its vtable:

| constructor | vtable | `vt+0x30` | `vt+0x4c` |
|---|---|---|---|
| `0xA78D10` (mode 2, 0x70 B) | 0x103DA28 | `0xA78510` | `0xA566C8` |
| `0xA76140` (r1>>16==1, r3==1) | 0x103D950 | `0xA75E34` | `0xA72B14` |
| `0xA72D04` (r1>>16==1, r3==3) | 0x103D73C | `0xA73128` | `0xA566C0` |
| `0xA74244` (r1>>16==0/2, r3==1) | 0x103D838 | `0xA7538C` | `0xA566B8` |
| `0xA72A2C` (r1>>16==0/2, r3==3) | 0x103D6B8 | `0xA72674` | `0xA566B8` |
| base `0xA5627C` | 0x103C844 | 0 | `0xA566C0` |

For the shipped Cozmo robot audio the source type comes from the bank's source plug-in (mode
2 / `r1>>16==2` are the candidates); the `vt+0x30` body is per type. The class names are
UNKNOWN (no RTTI). The `vt+0x30` call is preceded by `strh [params+0xC] = 0x108DF98` (the
Android bluetooth audio-route byte, written by `0xA57D64`/`0xA58008`), so it passes the route
state to the source.

**Citations.** `0xA44630..0xA44938`; `0xA4466C/70` slot vt+0x38; `0xA446B4/B8` slot vt+0x3C;
`0xA446E8 bl 0xA4C60C`; `0xA446F4 bl 0xA56E00`; `0xA44700 bl 0xA548C0`; `0xA44738 bl 0xA53134`;
`0xA4478C/9C/A0/A4` source vt+0x30; `0xA4477C bl 0xA52D4C`; `0xA447D4 bl 0xA03E8C`;
`0xA448A8/0xA448EC bl 0xA4FBEC`; `0xA4492C bl 0xA4C60C` (filter B); `0xA562B8` factory;
`0xA5627C` base vtable 0x103C844; the five constructor vtable stores (`0xA78DB0`, `0xA7616C`,
`0xA72D20`, `0xA74270`, `0xA72A54`).

**Classification.** Control flow, calls and vtable addresses EXACT_SOURCE. The insert-FX slot
class identity and per-slot semantics of `vt+0x38`/`vt+0x3c`: RECOVERABLE_GAP (V12's slot
identity is unresolved; the slot objects are created by `0x9CC2AC`/`0x9CC4D8` inside `0xA54A30`).
The source class name is UNKNOWN.

---

## Q6 - V9: `vt+0x58` on the bus in `0xA548C0`

**Answer.** `0xA548C0(voice, params)`:
- `r3=[voice+0xD4]` (source), `r4=[source+0xC]` (**the bus**), `r5=params`.
- `r2=[bus+4]`; if `bit 0x100000` set and `[params+0x18] != -1`: `0xA05574(map,
  [bus+0x140], &[params+0x18], ...)` (`0xA548E0..0xA54900`). The map pointer comes from the
  GOT global at `0x108...`; `r2 = params+0x18` is the value, `r1 = [bus+0x140]` the key.
- `0xA54904`: `r3=[bus]` (bus vtable); `r0=bus`; `r3=[r3+0x58]`; `blx` -> **`bus->vt+0x58`**.
- If the return `r0 != -1` (`cmn r0,#1`, `0xA54914`): if `r0 < [params+0xE]`
  (`0xA5491C/20`) store `[params+0xE]=r0` (`strhlo`) and `[params+0x2C]=1` (`0xA5492C`).
- `0xA54930`: `r0=[voice+0xD8]` (pending); if 0 return. Else `r3=[r0+0xC]`,
  `0xA56650(pending, [r3+0x1DC], [r3+0x1E0])` (`0xA54948`); if the result is 2,
  `[params+0x28]=2` (`0xA54950/54`). Return.

**Which vtable.** `r4` is the Wwise **bus** class. Its factory is the HIRC type-8 handler
`0x9B2CE8` (called from the dispatch at `0x9B3ADC`); the object is created by `0x9C3620`,
which stores the primary vtable at `0x9C3688: str ip,[r4]` with `ip = pc + 0x0067765C + 8`
(pc `0x9C367C`) = **0x103ACE0**, and a second interface at `[r4+0x10] = 0x103AE24`
(`0x9C3690`). Slot `0x103ACE0+0x58` = **`0x9C07C4`**. Class name UNKNOWN (no RTTI/symbols).

**`0x9C07C4` body** (`0x9C07C4..0x9C0860`): it iterates two child arrays on the bus --
`[this+0x58]` with count `[this+0x5c]`, then `[this+0x48]` with count `[this+0x4c]` -- and for
each child pointer calls **`child->vt+0x58(r1, r2, r3)`** recursively
(`0x9C080C: ldr r0,[r4],#4; 0x9C0810: ldr ip,[r0]; 0x9C0814: ldr ip,[ip,#0x58]; 0x9C0818:
blx ip`). It returns the last child's `r0`. So the bus's `vt+0x58` is a recursive
"collect/execute over children" that propagates the same arguments; `0xA548C0` uses its return
as a valid-frame count (`-1` = none).

Other bus vtable slots used in the same path (for cross-reference): `vt+0x3C = 0x9C6D78`
(`0xA54F1C`), `vt+0x24 = 0x9F1C48`, `vt+0x28 = 0x9C181C`, `vt+0x48 = 0x980EE0`,
`vt+0x4C = 0x9C10E0`. The source's `vt+0x4C` (`0xA54F1C` step) is on the source class
(`0xA55008`), not the bus.

**Citations.** `0xA548C0..0xA54954`; `0xA548CC ldr r4,[r3,#0xc]`; `0xA548D8 tst #0x100000`;
`0xA54900 bl 0xA05574`; `0xA5490C ldr r3,[r3,#0x58]`; `0xA54910 blx r3`; `0xA5491C..2C`;
`0xA54948 bl 0xA56650`; `0x9C3688` vtable store, `0x103ACE0` vtable, `0x103ACE0+0x58 ->
0x9C07C4`; `0x9C07C4..0x9C0860`.

**Classification.** `0xA548C0` and `0x9C07C4` EXACT_SOURCE; bus vtable address and slot
EXACT_SOURCE; class name UNKNOWN.

---

## Q7 - V11: `0xA52D4C` (resampler execute), `0xA5268C` (append), `0xA4721C`/`0xA47224`

### `0xA5268C` -- buffer append/carry, not a filter

`0xA5268C(r0=listA, r1=listB, r2=lo, r3=len)`:
- `lr=[r0+0x14]` (array), `sl=[r0+0x10]` (u16 count); if either is 0 return.
- `r7 = r2 + r3` (end). Count how many of A's entries (stride 0x14, field at `+4`) have
  `offset >= r2 && offset < r7` -> `r5` (u16). If 0 return.
- Allocate `20 * ([r1+0x10] + r5)` bytes (`0xA7A7F4`); if null return.
- If `[r1+0x14]` (B's old array) non-null, memcpy `20*[r1+0x10]` bytes (`0x4D37F0`).
- For each entry of A (`[sb+0x14]`, count `sl`) whose `+4` offset is in `[r2, r7)`, copy its
  5 words to the new array (advancing `lr` by 0x14).
- `0xA69A38(r1)` (free the old B array); `[r1+0x14]=newArray`; `[r1+0x10]=newCount`.

So it merges/carries entries from list A into list B by offset window -- no filtering, no
result code (returns nothing; `r0` is not set). This confirms the B1 report.

**Citations.** `0xA5268C..0xA527D0`; `0xA52694/98` A array/count; `0xA526B8..0xA526DC` count;
`0xA5271C bl 0xA7A7F4`; `0xA52740 bl 0x4D37F0`; `0xA52774..0xA527A8` merge; `0xA527B4
bl 0xA69A38`; `0xA527BC str r8,[r7,#0x14]`.

### `0xA52D4C` -- voice-stage resampler execute

`0xA52D4C(this=voice+0x100, params)`:
- `r6=params`. `r3=[params+0x28]`. If `r3==0x11` -> `0xA52EBC`: `[this+0xB8]=1`; if
  `[this+0x6E]!=0` -> `0xA52DA8`, else copy 40 bytes `params` -> `this+0x60` (`0xA52D88`).
- If `[this+0x6E]==0` (`0xA52D64/68`): if `[params+0xE]==0 && r3==0x2D` set `r3=0x2B`,
  `[params+0x28]=r3`, return (`0xA52D70..0xA52D84`).
- `0xA52DA8`: `r4=this`; `r5=[this+0x88]` (source object); if 0 -> `0xA53030`.
- `r1=[this+0xB4]` (bus); `r0=[r1+0x1BD]`; `r5 = r0 & 0x80`; if non-zero -> `0xA52E10`; else
  if `[r1+0x1B4]==0` -> `0xA52E10`; else if `[r1+0x1B4] >= [this+0x6E]` -> `0xA52F9C`; else
  `[this+0x2C]=[r1+0x1B4]`, `[this+0x6E] -= [r1+0x1B4]`, clear `[r1+0x1BE]` bit1 and
  `[r1+0x1BD]` bit7, `[r1+0x1B4]=0` (the source-start latch).
- `0xA52E10`: `sl=this+0x60`, `r5=this+0x88`, `sb=this+8`, `fp=[this+0x2C]`,
  `r8=[this+0x6E]`; call **`0xA47178(this+8, this+0x88, this+0x60)`** (`0xA52E30`) =
  CAkResampler::Execute (kernel dispatch); `r7 = r0`.
- `r3 = r8 - [this+0x6E]`; call **`0xA5268C(this+0x60, this+0x88, fp, r3)`** (`0xA52E50`).
- `r2=[this+0x78]`; if `r2==-1` -> `0xA52E6C`; else if `[this+0xB9]==0` -> `0xA52E98`
  (copy `[this+0x78..]` to `[this+0xA0..]`, `fp += r2`, `[this+0xA0]=fp`, `[this+0xB9]=1`),
  then `0xA52E6C`.
- `0xA52E6C`: `0xA46E30(this+8)` (`0xA52E70`); `r8=[this+0x6E]`; `[this+0xA4]=r0`; if `r8==0`
  -> `0xA52F0C`; if `r7==0x2D` or `r7==0x11` -> `0xA52ED8`; else `[params+0x28]=r7`, return.
- `0xA52ED8`: `0xA4721C(this+8)`; if result != 0 -> `0xA52F8C` (call `0xA47224(this+8,
  this+0x88)`), then copy 40 bytes `this+0x88` -> `params`, `[params+0x28]=r7`, return.
- `0xA52F0C` (`r8==0`): `[this+4]->vt+0xC`; `0xA69A38(this+0x60)`; reset fields
  (`[this+0x60]=0`, `[this+0x6C]=0`, `[this+0x6E]=0`, `[this+0x78]=-1`, `[this+0x80]=-1`,
  `[this+0x68]=0x2B`, `[this+0x84]=1`, `[this+0x7C]=1.0`); `[params]=0`, `[params+0x10]=0`,
  `[params+0x14]=0`; then if `[[this+0xB0]+0xD8]!=0` call `0xA52B90(this)`, `r7=result`; ->
  `0xA52E84`.
- `0xA52F9C` (`[r1+0x1B4] >= [this+0x6E]`): `[r1+0x1B4] -= [this+0x6E]`; clear bits;
  `[this+0x6E]=r5`; `[params+0xE]=r5`; `[this+4]->vt+0xC`; reset; `[params+0x28] = ([this+0xB8]
  ? 0x11 : 0x2B)`; return.
- `0xA52E84`: if `r7==0x2D` or `r7==0x11` -> `0xA52ED8`; else `[params+0x28]=r7`, return.
- `0xA53030` (`[this+0x88]==0`): `0xA69A70(this+0x88, [this+0x48], [this+0x64])`; if result==1
  and `[this+0xBA]!=0` do the first-buffer leading zero-fill (`0xA5305C..0xA5312C`):
  `n = round(([this+0x1D8] + [this+0xB4?+0x164]*F)/[this+0xB4?+0x164])` where F is a global u16;
  if `n>0` zero-fill `n` frames per channel into `[this+0x88]` and `[this+0x30]=n`; clear
  `[this+0xBA]`; continue. If result != 1 -> `[params+0x28]=2`, return.

**Result codes** written to `[params+0x28]`: **0x11 (17)** = source exhausted/need next,
**0x2B (43)** = keep filling the same output buffer, **0x2D (45)** = output buffer full,
**2** = format mismatch/failure. `0xA47178` returns 0x11 when the input has no frames
(`0xA47180`), else the kernel's 0x2B/0x2D/0x11.

**Citations.** `0xA52D4C..0xA53134`; `0xA52D60 beq 0xA52EBC`; `0xA52D88` 40-byte copy;
`0xA52E30 bl 0xA47178`; `0xA52E50 bl 0xA5268C`; `0xA52E70 bl 0xA46E30`; `0xA52EDC bl 0xA4721C`;
`0xA52F90 bl 0xA47224`; `0xA52F80 bl 0xA52B90`; `0xA5303C bl 0xA69A70`;
`0xA5305C..0xA5312C` zero-fill.

### `0xA4721C` / `0xA47224`

- **`0xA4721C`**: `0xA4721C: mov r0,#0; 0xA47220: bx lr` -- a stub that always returns **0**.
  (The inventory's "DataReady/NoMoreData 0xA4721C/0xA47224" labels are inverted in effect:
  0xA4721C is the zero-return check, 0xA47224 is the update.)
- **`0xA47224(r4=dest buffer, r1=source buffer)`**: copy 40 bytes `[r1]` to the stack; call
  `0xA69A70(stack, [r1+0xC], [r1+4])` -> `r5`; if `r5 != 1` return `r5`; else
  `[stack+0xE]=[r4+0xE]`; `0xA48B04(r4, stack)`; `0xA69AC8(r4)`; copy 40 bytes stack -> `r4`;
  return **1**.

**Citations.** `0xA4721C..0xA47220`; `0xA47224..0xA472A4`; `0xA4725C bl 0xA69A70`;
`0xA47288 bl 0xA48B04`; `0xA47290 bl 0xA69AC8`.

**Classification.** EXACT_SOURCE. The "DataReady/NoMoreData" naming in V11 is not supported by
the bodies; the codes that matter are the `[params+0x28]` values above.

---

## Q8 - V15: `0xA55D04`, `0xA55A84`, `0xA54A30`, and the state-0x11 tail

### `0xA55D04(voice, arg)` -- voice DSP teardown

- If `[voice+0xD4]!=0`: `0xA56414(source, arg)` (`0xA55D18`); destroy source (`source->vt[0]`,
  `0xA55D40`); free (`0xA7A988`, `0xA55D4C`); `[voice+0xD4]=0`.
- For each of the four insert-FX slots `voice+0x370..0x37C` (`r4=voice+0x370; r7=voice+0x380`):
  if slot non-null: `slot->vt+0x2C` (`0xA55D78`), destroy `vt[0]`, free, `slot=0`.
- `0xA53244(voice+0x100)` (`0xA55DC8`) -- resampler/pitch node cleanup.
- `0xA76608(voice+0x1D0)` (`0xA55DD0`) -- filter A cleanup.
- `0xA76608(voice+0x3A0)` (`0xA55DD8`) -- filter B cleanup.
- `[voice+0xCD] |= 1` (`0xA55DE0/4`). Return.

**Citations.** `0xA55D04..0xA55DE8`; `0xA55D18 bl 0xA56414`; `0xA55D40 blx vt[0]`;
`0xA55D4C bl 0xA7A988`; `0xA55D78 ldr r3,[r3,#0x2c]`; `0xA55DC8/DD0/DD8`; `0xA55DE0`.

### `0xA55A84(voice, source, r2, r3)` -- attach an existing source

- `sb = r2 & r3`; `r6=[source+0xC]` (bus); `r4=voice`; `r5=source`; `r7=r2`;
  `[bus+0x154]=voice` (`0xA55AA4`).
- If `sb==0`: `r0=[voice+0xE4]`; if `r0!=0` -> `0xA55B98`; else `0xA56650(source,
  [bus+0x1DC], [bus+0x1E0])` (`0xA55AC4`).
  - if result is 1 or 0x3F: `sb=result`; if `r7==0` `[voice+0xD8]=source` else
    `[voice+0xD4]=source`; `[voice+8]=[source+0xC]+0xC`; clear `[bus+0x1BE]` bit3; return `sb`.
  - else `0xA55B10`: `0xA56414(source,1)`, destroy/free, return `sb`.
- If `sb!=0` -> `0xA55B4C`: `0xA01768(bus, &[voice+0xE0])` -> `[voice+0xE4]`, then more
  (the rest of `0xA55A84` mirrors the `0xA558AC` body).

`0xA558AC` is the same logic but creates the source first via `0xA562B8` (`0xA558D8`); it is the
"create source" entry, `0xA55A84` the "attach existing" entry.

**Citations.** `0xA55A84..0xA55B48`; `0xA55AA4 str r0,[r6,#0x154]`; `0xA55AC4 bl 0xA56650`;
`0xA55ADC/E4` pending/current; `0xA55AF8` clear bit3; `0xA55B18 bl 0xA56414`.

### `0xA54A30(voice)` -- start stream / build the insert-FX chain

- `r2=[voice+0xD4]` (source), `r7=[source+0xC]` (bus), `sl=voice+0x100`.
- Copy 3 words `[bus+0x158]` to `sp+0x34`; call **`0xA5321C(voice+0x100, sp+0x34, bus)`**
  (`0xA54A78`). If `!=1` return **2**.
- `0xA54A98..0xA54B5C`: loop `r8=0..3` over the insert-FX slots (`sb=voice+0x370`, `+4` each):
  - `0xA019B8(bus, 0, voice+0x100)` -> candidate `[sp+0x2c]`; if present
    `0x9CC2AC(cand+0x10, sp+0x40, 1)` (`0xA54B2C`) and `0x9CC4D8(cand+0x10, 3, sp+0x40)`
    (`0xA54BA0`).
  - If `r0==0 && [sp+0x4a]==0 && [sp+0x48]==0`: allocate 0x9c bytes (`0xA7A7F4`), memcpy
    from `sp+0x24`, initialise the slot object (fields `+0x10`/`+0x14`/`+0x18`/`+0x1c`,
    `+0x30..`, `+0x34/+0x35`, `+0x5c/+0x5d`, `+0x38=0x2B`, `+0x60=0x2B`, `+0x64..+0x68`),
    store its vtable (`0xA54C50: str r2,[fp]`), call **`vtable+0x28`** on it (`0xA54CB8:
    ldr ip,[ip,#0x28]; 0xA54CBC: blx ip`) -- the slot constructor/init. If it returns 1 store
    the slot in `[sb]` (`0xA54E98: str fp,[sb]`).
- `0xA54B60`: `[voice+0xF0] = [sp+0x38]`; call **`0xA764D4(voice+0x1D0, 0)`** (`0xA54B74`) =
  filter A init; if `==1` -> `0xA54D28`.
- `0xA54D28`: `0xA764D4(voice+0x3A0, 0)` (filter B init); `0xA5676C(voice+0x380, bus)`
  (`0xA54D5C`) = gain/ramp init; for each collected slot object call `slot->vt+0x24`
  (`0xA54DA8`); then `voice->vt+0x6C` (`0xA54DC8`); return.

So `0xA54A30` is the per-voice "start/resolve source" that (re)builds the four insert-FX slots
(via `0x9CC2AC`/`0x9CC4D8` and the slot class at `0xA54C50`), initialises filter A/B and the
gain/ramp object, and calls the voice start hook `vt+0x6C`.

**Citations.** `0xA54A30..0xA54E84`; `0xA54A78 bl 0xA5321C`; `0xA54B2C bl 0x9CC2AC`;
`0xA54BA0 bl 0x9CC4D8`; `0xA54BE0 bl 0xA7A7F4`; `0xA54CB8` slot vtable+0x28;
`0xA54B74 bl 0xA764D4`; `0xA54D5C bl 0xA5676C`; `0xA54DC8` voice vt+0x6C.

### The state-0x11 tail (exact)

Reached in the voice pass when `[params+0x28]==0x11` (`0xA44A94: cmp r1,#0x11`), the duck flag
`sl==0` (`0xA44A9C`), and `[voice+0xD8] != 0` (`0xA44AA4/AA8`). Exact order
(`0xA44AB0..0xA44BFC`):

```
[voice+0xD8] = 0                          ; 0xA44AB8
0xA55D04(voice, 0)                        ; 0xA44ABC
r = 0xA55A84(voice, pending, 1, 0)        ; 0xA44AD0
if (r != 1) goto 0xA44ADC                 ; 0xA44AD4
r = 0xA54A30(voice)                       ; 0xA44BE8
if (r != 1) goto 0xA44ADC                 ; 0xA44BEC
0xA56478(pending)                         ; 0xA44BF8
goto 0xA449E0
```

`0xA44ADC` is the destroy path: `voice->vt+0x48` (`0xA44AE4/48`), then if `[voice+0xDC]==2`
unlink from `[0x108DF64]` and destroy with `0x9D40C4` (`0xA44AF8..0xA44B30`). `0xA56478` is
the per-source start notification (M6-008 row 2.4; reached only here in this tail).

**Citations.** `0xA44A94..0xA44BFC`; `0xA44AB8`; `0xA44ABC bl 0xA55D04`; `0xA44AD0 bl 0xA55A84`;
`0xA44BE8 bl 0xA54A30`; `0xA44BF8 bl 0xA56478`; `0xA44ADC..0xA44B30` destroy.

**Classification.** EXACT_SOURCE for all three bodies and the tail.

---

## Q9 - C10: `0xA01BD8`

**Answer.** Called from `0xA55750` at `0xA557EC` with `r0 = bus`. It dispatches on
`[bus+0xE8]`:
- `r3 = [bus+0xE8]`; if `(r3 & 3) == 0` -> `0xA01C48`: `[bus+0xC4] = 101.0` (`0x42CA0000`) and
  return (`0xA01C48..0xA01C54`).
- else `r2 = r3 & 0xC`; if `r2 == 4` -> `0xA01C58`:
  - `r2=[bus+0xDC]`; `r2=[r2+0x3C]`; if `r2 & 8` set, or `r3 & 0x80` set: `0xA01C70`:
    `0xA36D34([bus+0x14]+0x1C)` -> `r0`; `s15 = r0 / [ [bus+0x14]+0x64 ]`; tail-call
    `bus->vt+0x54(bus, s15)` (`0xA01CA0: bx r3`). Otherwise return.
- else (`r2 != 4`) -> `0xA01BF8`:
  - `r0=[bus+0xAC]`; if 0 return.
  - `r5 = ([ [bus+0xDC]+0x3C ] >> 5) & 1`; `0x9FE794([bus+0xAC], r5)` (`0xA01C14`).
  - if `r5==0` return; `r0=[bus+0xAC]`; `r3=[r0+0x18]`; if `(r3 & 2)==0` return.
  - `0x9FDD80(r0)` (`0xA01C30`); if 0 return; `0x9BCED4(bus+0xC)` (tail, `0xA01C44`).

**Citations.** `0xA01BD8..0xA01CA0`; `0xA01BDC tst #3`; `0xA01C48..0xA01C54` (101.0);
`0xA01C58..0xA01CA0`; `0xA01C14 bl 0x9FE794`; `0xA01C30 bl 0x9FDD80`; `0xA01C44 b 0x9BCED4`.

**Classification.** EXACT_SOURCE.

---

## Q10 - C11: `0x9E84C8` device volume

**Answer.** `0x9E84C8(node, key)` computes a product of per-node gains over a linked list:
- `ip = [node]`; if `ip == 0` return **1.0** (`0x9E8594..0x9E859C`).
- `s14 = 1.0`. Outer loop over `ip`:
  - `r2=[ip+0xC]` (count), `r6=[ip+8]` (array); `r2 = count*12`; if 0 skip.
  - `r3 = r6+0xC`; `r4,r5 = [key]` (a 64-bit value).
  - The count is scrambled (`0x9E8500..0x9E8520`) to compute the array end `r6`.
  - Inner loop over 12-byte entries `r3`:
    - `r2 = [r3-4]`; `bit = ( (r4 >> r2) | (r5 << (32-r2)) | (r5 >> (r2-32)) ) & 1`
      (`0x9E8528..0x9E8544`); if 0 skip.
    - `r1=[ip+4]`; `s13=[r3-8]`; `r2=[r1+0x30]`; `s15=[r3-0xC]`;
      `s12 = (r2 ? [r2+0xC] : [r1+0x4C])`; `s15 += s12*s13`; `s14 *= s15`.
    - `r3 += 0xC`; until `r6`.
  - `ip = [ip]`; while non-zero.
- Return `s14` (`0x9E858C: vmov r0,s14`).

So it multiplies `(value + slope * (sub?[sub+0xC]:node+0x4C))` for each entry whose bit in the
64-bit key is set, over the whole linked list; returns 1.0 if the list is empty. This is the
device volume/gain evaluation reached from `0xA4AF50` (C11).

**Citations.** `0x9E84C8..0x9E859C`; `0x9E84CC/94` empty -> 1.0; `0x9E84E0..0x9E8524` list
walk/end; `0x9E8528..0x9E8544` bit test; `0x9E8550..0x9E8570` gain multiply;
`0x9E8580..0x9E8590` next/return.

**Classification.** EXACT_SOURCE.

---

## Records contradicted / too weak / to update

- **B1-V5** (`0xA43D24`): confirmed but incomplete; the record must add the `0xA4AF50` voice
  pass, the `0xA437E0` flagged-bus retirement and the `0xA4B4B0` voice pass, and the exact
  array/count addresses (`[0x108DF4C]`, `[0x108DF54]`). The phrase "voice list base 0x108DF54"
  is off by 4: the container base is `0x108DF50` (count `0x108DF54` is the bus array's;
  voice count is `0x108DF5C`, head `0x108DF64`).
- **V6**: "count [base+0xC] decremented on unlink" is right only for base `0x108DF50`
  (count `0x108DF5C`). The `0x108DF54` count is the bus array's.
- **V7/C1**: the branch inventory is right but the "state machine over `[voice+0xE0]`,
  `[voice+0xE4]`, `+0xCD` bits" is not enough for transliteration. The full branch table above
  is needed; the value names (0/1/2) are not in the binary.
- **V9**: correct; add that `r4=[source+0xC]` is the bus class whose vtable is `0x103ACE0`
  and `vt+0x58 = 0x9C07C4` (recursive child collect).
- **V11**: `0xA5268C` is confirmed as append/carry (void, no result code); the "DataReady/
  NoMoreData 0xA4721C/0xA47224" labels are inverted in effect (0xA4721C always returns 0,
  0xA47224 returns 1 or the `0xA69A70` result). The result codes that matter are
  `[params+0x28]` = 0x11/0x2B/0x2D/2.
- **V15**: the three bodies and the tail are read; the record should cite `0xA56478` only as
  reached from the tail (M6-008 row 2.4 already owns it).
- **C10**: `0xA01BD8` body read; add the `[bus+0xE8]&3` zero case (`[bus+0xC4]=101.0`).
- **C11**: `0x9E84C8` body read.
- **M6-011 evidence (F1)**: confirmed -- `0xA4BC58` is the per-connection gain/format step,
  not the filter; the filter body is `0xA4C60C -> 0xA766B8 -> 0xA766F0/0xA77480`.
- **Q1/V3-N1**: the node class vtable is `0x103B498`; `vt+0x2c=0x9E935C` (returns
  `[this+0x84]`), `vt+0x30=0x9E9420` (sem_post, returns 1/2). The `[node+0x70]` sub-object
  identity is UNKNOWN.

## Open questions / what to read next

1. **`[node+0x70]` identity (Q1):** read the caller of the node vtable method that contains
   `0x9E9C0C` and the object it passes; then the sub-object's vtable store. Without it the
   `vt+0x2c/+0x30` functions may be different from the node class's.
2. **Source class name / which type the shipped robot audio uses (Q5):** the factory
   `0xA562B8` dispatches on `(mode, r1>>16)`; the bank's source plug-in for the robot-audio
   events selects it. Needed only for naming; the `vt+0x30` targets are tabulated.
3. **Insert-FX slot class (V12/Q5):** the slot objects are built in `0xA54A30` via
   `0x9CC2AC`/`0x9CC4D8` and the class at `0xA54C50`; the per-slot semantics of `vt+0x38`/
   `vt+0x3c` are not named.
4. **Bus class name (Q6):** vtable `0x103ACE0`, factory `0x9C3620`; no RTTI/symbol.
5. **`E4`/`E0` value names (Q4):** the code is exact; the Wwise names of states 0/1/2 are not
   in the binary. `0xA01768(bus, &E0)` and `0xA558AC`/`0xA55A84` set them.
