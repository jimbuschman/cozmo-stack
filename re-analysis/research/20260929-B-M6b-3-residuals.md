# B-M6b-3 residuals: the Play play-params struct and the ADPCM type-1 `pbi+0x158` writer

**Scope:** the two residuals in `re-analysis/research/20260929-B-M6b-3-bridge-extraction.md` Item 7
(Q1 the full play-params struct written by `0xA62A1C`; Q2 the ADPCM type-1 `pbi+0x158` writer).
Read-only. Primary source `resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode in the
Wwise region). Every citation is an instruction in the `.so`; the Ghidra tree
`re-analysis/decomp/libcozmoEngine/` was used only to navigate and its boundaries are wrong in places.

---

## Call-shape correction (needed before the field table)

`0xA62A1C` is the Play execute helper. The Play execute vfunc `+0x24 = 0xA62D38` is called as
`action->vt+0x24(action, queuedAction)`, so in `0xA62A1C(param_1, param_2)`:

- **param_1 = the Play action** (target id `+0x1C`, isBus / fade curve `+0x22`, property and ranged
  bundles at `+0x14`/`+0x18`);
- **param_2 = the 0x38-byte queued action** allocated by `ExecuteEvent 0x9AA3DC`.

Citations:

- `0x9AA2A8 ldr r0,[r4,#4]`; `0x9AA2AC mov r1,r4`; `0x9AA2B4 ldr r3,[r3,#0x24]`; `0x9AA2B8 blx r3`
  (`EnqueueOrExecute 0x9AA0FC`; r4 = the queued action, `[r4+4]` = the action pointer).
- queued-action layout, `0x9AA3DC` (object-scope branch): `0x9AA440 bl 0xA7A7F4` (r1=#0x38 at
  `0x9AA434`); `0x9AA488 str r5,[r4,#4]` (+0x04 action), `0x9AA4D8 str sl,[r4,#0x14]` (+0x14
  custom-params object, from `param_5[0]`), `0x9AA4B0 str r2,[r4,#0x1c]` (+0x1C = `param_5[2]`),
  `0x9AA4B4 str r3,[r4,#0x20]` (+0x20 = `param_5[3]`), `0x9AA4AC str r1,[r4,#0x24]` (+0x24 =
  `param_5[4]`), `0x9AA490 str r8,[r4,#0x28]` (+0x28 playing id = `param_3`), `0x9AA48C str sb,[r4,#0x30]`
  (+0x30 = `param_4`), `0x9AA450 str r6,[r4,#0x34]` (+0x34 game object = `param_2`); `param_5` is the
  5th argument (`0x9AA3F0 ldr fp,[sp,#0x30]`). Branch test: `0x9AA4F8 ands sl,r3,#1`; `0x9AA4FC bne`.
- The queued action's `+0x0C` (sub-frame delay remainder) is written by `0x9AA164 str r2,[r4,#0xc]`
  and the playing id is the `0x9AA118 ldr r1,[r1,#0x28]` used by `0xA04EDC`.

This matters because `params+0x18/+0x1C/+0x20` are the queued action's **custom-param values**, and
`params+0x08`/`+0x24` are the queued action's **game object / playing id** — not action fields.
The existing M6-006 gapA 1.11 text does not state where the params values come from.

Frame and base: `0xA62A20 sub sp,sp,#0x148`; `0xA62BEC addne r5,sp,#0x1c` and `0xA62CB0 add r5,sp,#0x1c`
fix the params base at `sp+0x1c`; the node is stored at `sp+0x20` = params+4 (`0xA62B90`).

---

## Q1 — every store into the struct at `sp+0x1c`

### 1a. The transition / randomizer object at `sp+0x10` (pointed to by params+0xC)

| # | field | instruction | value / meaning |
|---|---|---|---|
| a1 | `sp+0x10` | `0xA62A48 str r4,[sp,#0x10]` (r4=0) | zero |
| a2 | `sp+0x14` | `0xA62A54 str r3,[sp,#0x14]` (r3=4) | default 4, then overwritten |
| a3 | `sp+0x18` | `0xA62A4C strb r4,[sp,#0x18]` | zero byte |
| a4 | `sp+0x14` | `0xA62A8C str ip,[sp,#0x14]` (`0xA62A6C ldrb ip,[r7,#0x22]`; `0xA62A84 and ip,ip,#0x1f`) | **fade curve = action+0x22 bits 0..4** |
| a5 | `sp+0x10` | `0xA62AF0 str r0,[sp,#0x10]` (r0 = `0xA62A58 bl 0xA61110`) | **fade-in time = prop 0x10 + ranged 0x10 (0xA61110)** |

`params+0xC` is set to `&sp+0x10` at `0xA62BFC`, so this object is the fade-in transition handed to
`0xA0067C` as arg2 (`0xA3808C mov r1,r8`; `0xA38094 bl 0xA0067C`).

### 1b. Stores into the params struct proper (base `sp+0x1c`), in address order

Offsets are `sp_offset - 0x1c`. r4=0, r3=0 (set at `0xA62A60`), r8=0x3F800000, lr=0xFFFFFFFF.

| # | offset | instruction | value / source |
|---|---|---|---|
| 1 | `+0x10` | `0xA62A80 str r4,[sp,#0x2c]` | 0 (later overwritten, #58) |
| 2 | `+0x6C` | `0xA62A88 str r4,[sp,#0x88]` | 0 |
| 3 | `+0x7C` | `0xA62A90 str r4,[sp,#0x98]` | 0 (later re-zeroed, #64) |
| 4 | `+0x80` | `0xA62A94 str r4,[sp,#0x9c]` | 0 |
| 5 | `+0x84` | `0xA62A98 strb r4,[sp,#0xa0]` | **0 byte (Sound special-branch byte)** |
| 6 | `+0x88` | `0xA62A9C str r4,[sp,#0xa4]` | 0 |
| 7 | `+0xE8` | `0xA62AA0 str r4,[sp,#0x104]` | 0 |
| 8 | `+0xEC` | `0xA62AA4 str r4,[sp,#0x108]` | 0 |
| 9 | `+0xF0` | `0xA62AA8 str r4,[sp,#0x10c]` | 0 |
| 10 | `+0x108` | `0xA62AAC str r4,[sp,#0x124]` | 0 |
| 11 | `+0x10C` | `0xA62AB0 str r4,[sp,#0x128]` | 0 |
| 12 | `+0x110` | `0xA62AB4 str r4,[sp,#0x12c]` | 0 |
| 13 | `+0xA8` | `0xA62AB8 strb r1,[sp,#0xc4]` | byte: `(uninit & 0xfe)`, bit1 forced 0 (`0xA62A5C/0xA62A70/0xA62A78`) |
| 14 | `+0xB0` | `0xA62ABC strb r2,[sp,#0xcc]` | byte: `(uninit & 0xfe)`, bit1 forced 0 (`0xA62A64/0xA62A74/0xA62A7C`) |
| 15 | `+0xF4` | `0xA62AC0 str r3,[sp,#0x110]` | 0 |
| 16 | `+0xF8` | `0xA62AC4 str r3,[sp,#0x114]` | 0 |
| 17 | `+0xFC` | `0xA62AC8 str r3,[sp,#0x118]` | 0 |
| 18 | `+0x100` | `0xA62ACC str r3,[sp,#0x11c]` | 0 |
| 19 | `+0x104` | `0xA62AD0 str r3,[sp,#0x120]` | 0 |
| 20 | `+0x8C` | `0xA62AD4 str r3,[sp,#0xa8]` | 0 |
| 21 | `+0x94` | `0xA62AD8 str r3,[sp,#0xb0]` | 0 |
| 22 | `+0x98` | `0xA62ADC str r3,[sp,#0xb4]` | 0 |
| 23 | `+0x9C` | `0xA62AE0 str r3,[sp,#0xb8]` | 0 |
| 24 | `+0xA0` | `0xA62AE4 str r3,[sp,#0xbc]` | 0 |
| 25 | `+0xA4` | `0xA62AE8 str r3,[sp,#0xc0]` | 0 |
| 26 | `+0xAC` | `0xA62AEC str r3,[sp,#0xc8]` | 0 |
| 27 | `+0x85` | `0xA62AF4 strb lr,[sp,#0xa1]` | **0xFF byte** |
| 28 | `+0x90` | `0xA62AF8 str r8,[sp,#0xac]` | **1.0f (0x3F800000)** |
| 29 | `+0xB4` | `0xA62AFC str r3,[sp,#0xd0]` | 0 |
| 30 | `+0xB8` | `0xA62B28 str r3,[sp,#0xd4]` | 0 |
| 31 | `+0x128` | `0xA62B2C strb r2,[sp,#0x144]` | flags byte: bit0=0, bits1,2=1, bit3=0, bit4=0 (`0xA62B00 ldrb` / `0xA62B0C..0xA62B24`) |
| 32 | `+0xBC` | `0xA62B34 str r3,[sp,#0xd8]` | 0 |
| 33 | `+0xC0` | `0xA62B38 str r4,[sp,#0xdc]` | 0 |
| 34 | `+0xC4` | `0xA62B3C str r4,[sp,#0xe0]` | 0 |
| 35 | `+0xC8` | `0xA62B40 str r4,[sp,#0xe4]` | 0 |
| 36 | `+0xCC` | `0xA62B44 str r4,[sp,#0xe8]` | 0 |
| 37 | `+0xD0` | `0xA62B48 str r4,[sp,#0xec]` | 0 |
| 38 | `+0xD4` | `0xA62B4C str r4,[sp,#0xf0]` | 0 |
| 39 | `+0xD8` | `0xA62B50 str r4,[sp,#0xf4]` | 0 |
| 40 | `+0xDC` | `0xA62B54 str r4,[sp,#0xf8]` | 0 |
| 41 | `+0xE0` | `0xA62B58 str r3,[sp,#0xfc]` | 0 |
| 42 | `+0xE4` | `0xA62B5C strb r4,[sp,#0x100]` | 0 byte |
| 43 | `+0xE5` | `0xA62B60 strb r4,[sp,#0x101]` | 0 byte |
| 44 | `+0xE6` | `0xA62B64 strb r4,[sp,#0x102]` | 0 byte |
| 45 | `+0xE7` | `0xA62B68 strb r4,[sp,#0x103]` | 0 byte |
| 46 | `+0x114` | `0xA62B6C str r4,[sp,#0x130]` | 0 |
| 47 | `+0x118` | `0xA62B70 str r3,[sp,#0x134]` | 0 |
| 48 | `+0x11C` | `0xA62B74 str r4,[sp,#0x138]` | 0 |
| 49 | `+0x120` | `0xA62B78 str r4,[sp,#0x13c]` | 0 |
| 50 | `+0x28` | `0xA62B7C str r4,[sp,#0x44]` | 0 (start of the 0x44-byte block copied to pbi+0x170) |
| 51 | `+0x00` | `0xA62B80 str r4,[sp,#0x1c]` | 0 |
| 52 | `+0x124` | `0xA62B84 strb r1,[sp,#0x140]` | byte: `(uninit & 0xfe)`, bit0 forced 0 (`0xA62B20/0xA62B30`) |
| 53 | `+0x18` | `0xA62B88 str lr,[sp,#0x34]` | **queued+0x1C** (`0xA62B04 ldr lr,[r5,#0x1c]`) |
| 54 | `+0x1C` | `0xA62B8C str ip,[sp,#0x38]` | **queued+0x20** (`0xA62B08 ldr ip,[r5,#0x20]`) |
| 55 | `+0x04` | `0xA62B90 str r6,[sp,#0x20]` | **target node** (r6 = `0xA62A2C bl 0xA6168C`) |
| 56 | `+0x24` | `0xA62B94 str r7,[sp,#0x40]` | **playing id = queued+0x28** (`0xA62B10 ldr r7,[r5,#0x28]`) |
| 57 | `+0x20` | `0xA62B98 str r0,[sp,#0x3c]` | **queued+0x24** (`0xA62B18 ldr r0,[r5,#0x24]`) |
| 58 | `+0x10` | `0xA62BC0 str r3,[sp,#0x2c]` | **queued+0x14 custom-params object** (`0xA62B9C ldr r3,[r5,#0x14]`; refcount++ at `0xA62BAC..0xA62BB4`) |
| 59 | `+0x74` | `0xA62BC8 str r1,[sp,#0x90]` | **queued+0x0C sub-frame remainder / initial delay** (`0xA62BB8 ldr r1,[r5,#0xc]`) |
| 60 | `+0x128` | `0xA62BCC strb r2,[sp,#0x144]` | flags byte with bit2 set (`0xA62BC4 orr r2,r2,#4`) |
| 61 | `+0x70` | `0xA62BD0 str r4,[sp,#0x8c]` | 0 |
| 62 | `+0x78` | `0xA62BE8 str r4,[sp,#0x94]` | 0 |
| 63 | `+0x08` | `0xA62BF0 str r3,[sp,#0x24]` | **game object = queued+0x34** (`0xA62BDC ldr r3,[r5,#0x34]`) |
| 64 | `+0x7C` | `0xA62BF8 str r4,[sp,#0x98]` | 0 (chain id; 0 => auto id at `0xA000E8` line 111) |
| 65 | `+0x0C` | `0xA62BFC str r3,[sp,#0x28]` | **transition pointer = &sp+0x10** (`0xA62BF4 add r3,sp,#0x10`) |
| 66 | `+0x128` | `0xA62C04 strb r2,[sp,#0x144]` | bit3 = action isBus: `0xA62BA0 ldr r0,[r5,#4]`; `0xA62BD4 bl 0xA616BC`; `0xA62C00 bfi r2,r0,#3,#1` |
| 67 | `+0x128` | `0xA62D0C strb r3,[sp,#0x144]` | **special branch only**: bit1 from `0xA62D04 ldrb r2,[sp,#0xf]` after `0x9EE454` |

`+0x14` (= `sp+0x30`) has **no store** in `0xA62A1C`; see "Contradictions" below.

After #66, `0x9F12E0(node, params)` runs at `0xA62C14` and mutates params in place:
It reads params+0x128 (`0x9F12E0 line 28`), params+0x124 (`line 32`), params+0x7C
(`line 93`) and writes params+0x74 (`line 106: *(int *)(param_2 + 0x74) = ...`) and params+0x124
(`line 112`). It does **not** write params+0x28..+0x6B.

### 1c. What each params field is (instruction-supported)

| offset | value | role | citation |
|---|---|---|---|
| `+0x04` | target node | Wwise node resolved from action+0x1C/isBus | `0xA62B90`; `0xA62A2C bl 0xA6168C`; `0xA62D14/20/24 node->vt+0x128` |
| `+0x08` | queued+0x34 | **game object** (ExecuteEvent arg) | `0xA62BF0`; `0x9AA450 str r6,[r4,#0x34]`; `0xA000E8 line 32` passes it to `0x9BC90C`, whose `local_30=param_2` is the RTPC-key game object (`0x9BC90C line 27`) |
| `+0x0C` | &sp+0x10 | **fade-in transition pointer** | `0xA62BFC`; arg2 of `0xA0067C` (`0xA3808C/0xA38094`) |
| `+0x10` | queued+0x14 | custom-params object (refcounted) -> `pbi+0x12C` | `0xA62BC0`; `0xA001E0 str r3,[r4,#0x12c]` |
| `+0x18` | queued+0x1C | custom param value -> `pbi+0x134` | `0xA62B88`; `0xA001CC str r1,[r4,#0x134]` |
| `+0x1C` | queued+0x20 | custom param value -> `pbi+0x138` | `0xA62B8C`; `0xA001C4 str r2,[r4,#0x138]` |
| `+0x20` | queued+0x24 | custom param value -> `pbi+0x13C` | `0xA62B98`; `0xA001BC str r0,[r4,#0x13c]` |
| `+0x24` | queued+0x28 | **playing id** -> `pbi+0x140` | `0xA62B94`; `0x9AA490 str r8,[r4,#0x28]`; `0xA0019C str r1,[r4,#0x140]` |
| `+0x28` | 0 | start of a 0x44-byte block; `memcpy(pbi+0x170, params+0x28, 0x44)` | `0xA62B7C`; `0xA00344 add r1,r5,#0x28`; `0xA0035C mov r2,#0x44`; `0xA00380 bl 0x4D37F0` |
| `+0x70` | 0 | read as `(params+0x70 == 1)` for `0xA0067C` arg3 | `0xA62BD0`; `0xA3806C ldr r6,[r4,#0x70]`; `0xA3807C sub r2,r6,#1`; `0xA38084 clz r2,r2`; `0xA38090 lsr r2,r2,#5` |
| `+0x74` | queued+0x0C | **initial delay / start offset** -> `pbi+0x1D8`; adjusted by `0x9F12E0` | `0xA62BC8`; `0xA002EC str r8,[r4,#0x1d8]`; `0x9F12E0 line 106` |
| `+0x7C` | 0 | **chain id**; 0 => `pbi+0x1C8 = global++` | `0xA62BF8`; `0xA000E8 lines 107..119` (`0xA00334`/`0xA00410`) |
| `+0x84` | 0 byte | **Sound special-branch byte** -> `pbi+0x1E4` | `0xA62A98`; `0xA002FC str r3,[r4,#0x1e4]`; `0xA1D450 ldrb r3,[r1,#0x84]` |
| `+0x85` | 0xFF byte | read by `0xA1D448` when +0x84==0x90 | `0xA62AF4`; `0xA1D... ldrb ...+0x85` (report 2a.3) |
| `+0x90` | 1.0f | unknown; not read in the paths read | `0xA62AF8` |
| `+0x128` | flags byte | bits read by `0xA000E8` (`&0xf>>3`, `&1`, `&0x1f>>4`, `&4`) and `0xA379D8` (`&4`) | `0xA62B2C/0xA62BCC/0xA62C04/0xA62D0C`; `0xA00128..0xA002D0`; `0xA379D8 line 106/291` |
| `+0x124` | byte | read by `0x9F12E0` (`bVar1 & 1`), updated by it | `0xA62B84`; `0x9F12E0 lines 32/112` |
| `+0x14` | **not written** | report lists it as a consumer field; no writer/reader found | see Contradictions |

Fields `+0x1DC`/`+0x1E0` in the task's list are **PBI** fields, not params: they are read at
`0xA01E5C` (`0xA01E24` item 5.2) and written by the source StartStream. They cannot be in this
struct (its frame ends at `sp+0x148` = params+0x12C).

### 1d. The 0x44-byte block at params+0x28 is not initialised here

The only store in `params+0x28..+0x6B` is `0xA62B7C` (params+0x28 = 0). A scan of the whole body
found no store to `sp+0x48..sp+0x87` (= params+0x2C..+0x6B). So the 0x44 bytes that `0xA000E8`
copies to `pbi+0x170` are, for this Play path, mostly whatever was on the stack; only the first word
is 0. The same is true of the initial byte reads at params+0xA8, +0xB0, +0x124 and the flags byte
(params+0x128), whose high bits are read before being written (only the low bits the consumers use
are defined). Likely the node's own `PlayInternal` fills the block for container nodes (Sound
`0xA1D448` does not); I did not read the container PlayInternals to confirm. **RECOVERABLE_GAP /
UNKNOWN** — see open questions.

---

## Q2 — the ADPCM type-1 `pbi+0x158` writer

**Settled: the format word is written at `0xA73B78 str lr,[r2,#0x158]`, inside the method at
`0xA73ABC`, which is the mode-1 (ADPCM stream 1 = type 1) vtable slot `+0x78`.** The report's
Item 5.10 missed it because it scanned only the StartStream body inline; the write happens in the
`vt+0x78` method that StartStream calls.

Chain, all instructions:

| step | instruction | what it shows |
|---|---|---|
| ctor | `0xA74504 push`; `0xA74538 str r2,[r4]` (r2 = `pc+0x005C9390`+8 = 0x103D8C8) | base ADPCM/source ctor sets vtable 0x103D8C8 |
| type-1 factory | `0xA7424C bl 0xA74504`; `0xA74270 str r3,[r4]` (r3 = `pc+0x005C95D0`+8 = 0x103D840) | the mode-1 class sets vtable 0x103D840 (`0xA74244`) |
| vtable slot | VA `0x103D840 + 0x78 = 0x103D8B8` holds `0xA73ABC` | the mode-1 `+0x78` method is 0xA73ABC |
| StartStream | `0xA753C4 bl #0xA74C80` | StartStream 0xA7538C calls 0xA74C80 |
| call site | `0xA74CC4 ldr r3,[r5]`; `0xA74CC8 ldr r1,[sp,#4]`; `0xA74CCC ldr r3,[r3,#0x78]`; `0xA74CD0 blx r3` | 0xA74C80 dispatches the source's `vt+0x78` |
| format read | `0xA73B10 bl #0x9CD340`; `0xA73B24 ldr r7,[sp,#0x1c]` | the format descriptor comes from `0x9CD340` |
| PBI | `0xA73B40 ldr r2,[r4,#0xc]` | r2 = `[this+0xC]` = the PBI |
| value | `0xA73B4C ldr r1,[r7,#0x14]`; `0xA73B58 ldr lr,[r7,#4]` | descriptor `+0x14` and `+4` |
| **write** | `0xA73B78 str lr,[r2,#0x158]` | **PBI+0x158 = descriptor+4** |
| siblings | `0xA73B88 strb r1,[r2,#0x15c]`; `0xA73B98/0xA73B9C/0xA73BA0 strb [r2,#0x15d/0x15e/0x15f]`; `0xA73BA4 strb r3,[r2,#0x161]`; `0xA73BA8 strb ip,[r2,#0x160]` | PBI+0x15C..+0x161 |

The render that consumes it is the mode-1 `+0x30` slot `0xA73D34`: `0xA73DB4 ldr fp,[r4,#0xc]`
(fp = PBI); `0xA73DC0 add r3,fp,#0x160`; `0xA73DC8 add r0,fp,#0x158`; `0xA73DE0 ldrb r5,[fp,#0x15c]`;
`0xA73F6C ldr r3,[fp,#0x158]`. The mode-1 vtable slot `+0x30` is confirmed by
VA `0x103D840+0x30 = 0x103D870` holding `0xA73D34`.

`0xA73B4C ldr r1,[r7,#0x14]` is the descriptor word whose bytes land in `+0x15C..+0x15F`; `+0x160`
is built from descriptor `+2` (`0xA73B48 ldrh r3,[r7,#2]`, `0xA73B54 lsl r3,r3,#1`, `0xA73B74 and r0,r3,#3`,
`0xA73B94 orr ip,ip,r0,lsl #6`, `0xA73BA8`). The exact semantic of each descriptor field is not named by
these instructions; only `+4 -> pbi+0x158` is fixed.

A second format-writer cluster exists at `0xA75C84/0xA75C8C/0xA75C90` (function entry near
`0xA75BC8`, also calling `0x9CD340` at `0xA75C18`). It is **not** the type-1 slot (`0xA75BC8` is not
referenced by the 0x103D840 vtable); it belongs to another source class (likely the type-3/PCM
sibling). Not needed for the shipped ADPCM stream-1 path.

**Q2 verdict: settled — not a RECOVERABLE_GAP.** Item 5.10 / the M6-025 residual can be closed.

---

## Existing records contradicted or too weak

- **M6-025 B15 / Item 5.10** ("ADPCM type-1 `0xA7538C` has no `pbi+0x158` write found ->
  RECOVERABLE_GAP"): contradicted. The write is `0xA73B78 str lr,[r2,#0x158]` in the `vt+0x78`
  method `0xA73ABC` (mode-1 vtable 0x103D840+0x78), reached from StartStream via `0xA753C4 bl
  0xA74C80` and `0xA74CCC/0xA74CD0`. The record's evidence proves only "no inline write in
  0xA7538C..0xA75680", not "no writer".
- **M6-025 residual "the full play-params struct layout (`0xA62A1C..0xA62D38`) RECOVERABLE_GAP"**:
  the stores are now enumerated (above). The residual should shrink to two sub-items: `params+0x14`
  (no writer found) and the uninitialised `params+0x28..+0x6B` block.
- **Bridge report Item 7.2 field list names `+0x14`** as read by `0xA000E8`/`0xA379D8`: too weak /
  unsupported. I read `0xA000E8` (reads +8, +0x128, +0x24, +0x10, +0x20, +0x1c, +0x18, +4, +0x74,
  +0x84, +0x7c, +0x28), `0xA379D8` (reads +4, +8, +0x10, +0xC, +0x24, +0x70, +0x128, +0x88, +0x8C,
  +0x108, +0x78, +0x28), `0xA1D448` (reads +8, +0x84..+0x88), `0x9F12E0` (reads +0x128, +0x124,
  +0x7C; writes +0x74, +0x124) and `0xA02494` (reads +0x84, +4) — none reads +0x14. `0xA62A1C`
  itself has no store at `sp+0x30`. The `+0x14` in the report may be a confusion with the local
  transition object at `sp+0x10` (whose `sp+0x14` = fade curve) or with `0xA62ED4`'s `param_2[5]`.
- **M6-006 gapA 1.11** ("`0xA62A1C` ... builds transition params ...") is confirmed as a call chain
  but does not say the params values come from the *queued action* (`param_2`) while the target and
  fade-in come from the *action* (`param_1`). Its "how the fade-in is applied" residual is now
  answered at the field level: the fade-in time/curve live in the `sp+0x10` object pointed to by
  `params+0xC` (`0xA62AF0`/`0xA62A8C`), consumed by `0xA0067C` (`0xA3808C/0xA38094`).

## Open questions for the manager

1. **`params+0x14`.** No store in `0xA62A1C`, no reader in the Play consumers. Decide whether to
   drop it from the M6-025 field list or send it back for a wider reader search (it could be a
   `0xA62ED4`-path field that leaked into the report).
2. **`params+0x28..+0x6B` uninitialised.** Confirm whether the container PlayInternals
   (`0xA0AFDC` RanSeq, `0xA2C730` Switch, `0xA667D0` ActorMixer, `0x9D0758` Layer) fill this block
   before `0xA379D8`. If they do, it is a per-node field and Sound leaves it unused; if not, the
   0x44-byte `memcpy` to `pbi+0x170` copies uninitialised stack on the Sound path. Addresses tried:
   `0xA62A1C..0xA62D38` (no store), `0x9F12E0`, `0x9EDEB8`, `0xA000E8` (source only).
3. **Semantics of `params+0x18/+0x1C/+0x20`** (= queued-action custom params [2]/[3]/[4] ->
   `pbi+0x134/+0x138/+0x13C`): the instructions show the source and destination but not the Wwise
   meaning (event id / action id / etc.). UNKNOWN until a reader of `pbi+0x134..+0x13C` is named.
4. **`params+0x70` and `params+0x7C` are hardcoded 0 in `0xA62A1C`.** Confirm whether any node
   PlayInternal sets them (the `0xA0067C` flag and the chain id depend on them). If none does, the
   `(params+0x70==1)` flag is always false and the chain id is always auto-allocated on this path.
5. **Descriptor-field naming for Q2.** `pbi+0x158 = descriptor+4`, `pbi+0x15C..+0x15F` =
   descriptor+0x14 bytes, `pbi+0x160` from descriptor+2. `0x9CD340`'s descriptor layout is the
   remaining RECOVERABLE_GAP (already listed in M6-025); only `+4 -> +0x158` is settled here.

## Settled in this pass

- Q1: all 67 stores into the params struct are enumerated with offsets, sources and instructions;
  the params base, the two-argument shape, the transition object, the game object, playing id,
  initial delay, chain id, flags byte, special-branch byte and custom-param fields are placed.
- Q2: the ADPCM type-1 `pbi+0x158` writer is `0xA73B78` in the mode-1 `vt+0x78` method `0xA73ABC`,
  reached from StartStream `0xA7538C` via `0xA74C80`; Item 5.10 / M6-025 B15 closes.
- Remaining UNKNOWN: `params+0x14`; `params+0x28..+0x6B` initialiser; the Wwise meaning of
  `params+0x18/+0x1C/+0x20`; the `0x9CD340` descriptor field names.


