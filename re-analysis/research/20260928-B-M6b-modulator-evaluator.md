# M6b bounded pass: the per-voice curve / modulator evaluator

Scope: `0x9E2BD0` (body `0x9E2BD0..0x9E52F3`, 9876 B) and `0x9E52F8`
(body `0x9E52F8..0x9E5E8B`, 2956 B), both called from Perform group member 4
`0x9E6D2C`. Read in ARM mode from `resources/lib/armeabi-v7a/libcozmoEngine.so`
3.4.0-1204. Ghidra used for navigation only; every claim below is cited to an
instruction. Addresses are VAs. The prior pass's "not transliterated" note is
replaced by the bodies below.

## Caller and arguments

- `0x9E6D2C ldr r3,[pc,#0xe0]` -> literal at `0x9E6E14` = `0x00659318`;
  `0x9E6D38 ldr r3,[pc,r3]` (pc `0x9E6D40`) -> `[0x9E6D40+0x659318] = [0x1040058]`,
  a GOT slot; `0x9E6D44 ldrh r1,[r3]` = the frame tick (u16).
- `0x9E6D40 ldr r0,[r7,#0x10]` with `r7` = manager `0x108D8DC` (V28), so
  `arg = manager+0x10`.
- `0x9E6D48 bl 0x9E2BD0`; the r0 return is a status and the caller does not test
  it.
- `0x9E2BD0` prologue: `0x9E2BD0 push {r4-r8,sb,sl,fp,lr}`; `0x9E2BD4 mov fp,r1`
  (tick); `0x9E2BD8 vpush {d8-d14}`; `0x9E2BDC ldr sl,[r0,#0x20]`.

---

# Question 1 - 0x9E2BD0(arg, tick)

## Answer

### 1.1 The two chunk lists and what the clear means

`[arg+0x20]` and `[arg+0x14]` are heads of singly-linked lists of *chunk nodes*
(next at node+0). Both lists share the same 0x28-byte node layout:

| node off | meaning |
|---|---|
| +0x00 | next node |
| +0x04 | buffer1 pointer - record slots |
| +0x08 | buffer1 used count |
| +0x0c | buffer1 capacity |
| +0x10 | buffer2 pointer - per-record state slots |
| +0x14 | buffer2 used count |
| +0x18 | buffer2 capacity |
| +0x1c | float-buffer *needed* length (in floats), recomputed each call |
| +0x20 | float-buffer *allocated* length (persists) |
| +0x24 | float-buffer pointer (persists; grown lazily) |

Clear loops:
- `0x9E2BDC ldr sl,[r0,#0x20]`; if non-null, `0x9E2C04 str r2,[r3,#0x1c]`,
  `0x9E2C08 str r2,[r3,#8]`, `0x9E2C0C str r2,[r3,#0x14]` (r2=0), `0x9E2C10 ldr r3,[r3]`
  loop.
- `0x9E2C20 ldr r4,[r3,#0x14]`; same three stores at `0x9E2C34/0x9E2C38/0x9E2C3C`.

So the clear resets the two used counts (+8, +0x14) and the per-call needed float
length (+0x1c). Capacities, buffers and the allocated float length survive. The
records themselves are overwritten as slots are handed out, so no record clear is
needed.

### 1.2 The voice walk and the +0x3C accumulator

`0x9E2C4C ldr r3,[arg+8]` (voice list head); `0x9E2D90 ldr r6,[r6,#4]` (next).
The loop test is `0x9E2D9C ldr r3,[r6,#0x48]` (`0x9E2DA0 cmp r3,#3`).

For each voice `v`:
- state `v+0x48 == 3`: if `v+0x30 == 0` (`0x9E2DA8..0x9E2DB0`) then
  `0x9E2DB4 ldr r3,[v+0x3c]; 0x9E2DB8 ldr r2,[sp,#4]` (tick);
  `0x9E2DBC add r3,r3,r2; 0x9E2DC0 str r3,[v+0x3c]`. Otherwise the source object
  path at `0x9E2CA4`.
- state `!= 3`: `0x9E2C88 ldr r1,[v+0x58]; 0x9E2C8C ldr r2,[v+0x50]`;
  `0x9E2C90 cmp r1,r2; bge` -> skip the voice. Otherwise if `v+0x30 == 0` the
  `+0x3C += tick` at `0x9E2CBC..0x9E2CC8`; else the same source path.

Source path (`v+0x30` non-null, `0x9E2CA4`):
- `0x9E2CA4 ldr r2,[source+4]`; `0x9E2CAC cmn r2,#1`; `0x9E2CB0 strne r2,[v+0x44]`.
- `0x9E2CB8 ldr r2,[source+8]; 0x9E2CC0 str r2,[v+0x4c]`.
- `+0x3C += tick` (above).

The record type is `[[v+8]+0x10]` (`0x9E2CE0 ldr r3,[r6,#8]; 0x9E2CE4 ldr r3,[r3,#0x10]`):
`0` -> a type-0 record in `[arg+0x20]` (`0x9E2DD4`), `1` -> a type-1 record in
`[arg+0x14]` (`0x9E2CF8`). Any other value skips the voice (`0x9E2CF0 cmp r3,#1;
bne 0x9E2D90`).

The function returns `1` (ok) or `0x34` (allocation failure):
`0x9E2F44 mov r3,#1; str r3,[sp,#0x20]`; `0x9E51A4 mov r3,#0x34`;
return `0x9E4CEC ldr r0,[sp,#0x20]`.

### 1.3 Type-1 record (0x30 bytes, list [arg+0x14])

Slot taken at `0x9E2D18 ldr r3,[r4,#4]` (data), `0x9E2D1C mov r1,r7`,
`0x9E2D20 add lr,r1,r1,lsl #1` (lr=3*r1), `0x9E2D34 add ip,r3,lr,lsl #4`
(stride 0x30 = 3*16). Fields written (all from the voice):

| rec off | voice field | note |
|---|---|---|
| +0x00 | +0x44 | |
| +0x04 | +0x34 | count |
| +0x08 | +0x38 | count |
| +0x0c | +0x3c | accumulated time (the +0x3C accumulator) |
| +0x10 | +0x4c | float |
| +0x14 | +0x54 | mode selector (0 -> inline transition, !=0 -> 0x9E52F8) |
| +0x18 | +0x74 | float |
| +0x1c | +0x78 | int (used >>1) |
| +0x20 | +0x7c | float |
| +0x24 | +0x80 | float |
| +0x28 | +0x84 | int |
| +0x2c | +0x88 | int |

Writes: `0x9E2D4C` +0x1c, `0x9E2D54` +0x18, `0x9E2D58` +0x28, `0x9E2D60` +0x2c,
`0x9E2D64` +0x20, `0x9E2D6C` +0x24, `0x9E2D70` +0x00, `0x9E2D7C` +0x10,
`0x9E2D80` +0x0c, `0x9E2D84` +0x08, `0x9E2D88` +0x04, `0x9E2D8C` +0x14.

### 1.4 Type-0 record (0x4C bytes, list [arg+0x20])

Slot: `0x9E2DF4 add r3,r5,r5,lsl #3` (9*r5), `0x9E2E04 add r3,r5,r3,lsl #1`
(19*r5), `0x9E2E08 adds r3,r2,r3,lsl #2` (stride 0x4C). The first six slots
+0x34/+0x48/+0x38/+0x3c/+0x40/+0x44 are zeroed at `0x9E2E20..0x9E2E34`. It then
copies the same twelve fields as the type-1 record at +0x00..+0x2c, plus the
modulator block:

| rec off | voice field | note |
|---|---|---|
| +0x30 | +0x8c | float |
| +0x34 | +0x90 | = source+0x10 (see 1.5) |
| +0x38 | +0x94 | = source+0x14 |
| +0x3c | +0x98 | = source+0x18 |
| +0x40 | +0x9c | = fmodf(source+0x1c, 2*pi or 1.0) |
| +0x44 | +0xa0 | = source+0x20 (or *2*pi) |
| +0x48 | +0xa4 | = source+0x24 (the shape selector 0..4) |

Writes: `0x9E2ED8` +0x24, `0x9E2EDC` +0x20, `0x9E2EE4` +0x1c, `0x9E2EF0` +0x18,
`0x9E2EF4` +0x2c, `0x9E2EF8` +0x30, `0x9E2F00` +0x28, `0x9E2F04` +0x34..0x40,
`0x9E2F18` +0x44, +0x48, `0x9E2F1C` +0x00, `0x9E2F20` +0x04/+0x08/+0x0c,
`0x9E2F24` +0x10, `0x9E2F34` +0x14.

### 1.5 The modulator/source block copy and phase wrap

At `0x9E2E38 ldr lr,[v+0x30]` (the source/modulator object). If non-null,
`0x9E2E44..0x9E2E94` copies `source+0x10..0x24` (6 words) into
`v+0x90..0xa4`:
`0x9E2E88 ldm sb!,{r0-r3}` / `0x9E2E8C stm lr!,{r0-r3}` (v+0x90..0x9c),
`0x9E2E90 ldm ip,{r0,r1}` / `0x9E2E94 stm lr,{r0,r1}` (v+0xa0, v+0xa4).
Then:
- `0x9E2E4C vldr s15,[source+0x1c]`; `0x9E2E64 cmp lr,#0` where
  `0x9E2E5C ldr lr,[source+0x24]`.
- `0x9E2E98 movweq r1,#0xfdb; movteq r1,#0x40c9` (= 0x40C90FDB, 2*pi) when
  source+0x24 == 0, else `0x9E2EA0 movne r1,#0x3f800000` (1.0).
- `0x9E2EA4 vmov r0,s15`; `0x9E2EA8 bl 0x4BD524` (fmodf);
  `0x9E2EB4 str r0,[v+0x9c]`.
- `0x9E2EAC ldrb r3,[v+0xa8]`; if non-zero -> `0x9E2EB8 bne 0x9E35C0`
  (recompute coefficients), else continue at `0x9E2EBC`.

### 1.6 Coefficient recompute (0x9E35C0..0x9E36B4)

Reads the float trio `v+0x88` (damping), `v+0x84` (frequency), `v+0x80` (shape/mode):
- `0x9E35C0 vldr s15,[v+0x88]`; `0x9E35C4` if zero -> `0x9E5004`.
- `0x9E35D0 vldr s15,[v+0x84]`; `0x9E35D4 vmov.f64 d11,#2.0`;
  `0x9E35D8 vcvt.f64.f32 d16,s15`; `0x9E35DC vcmpe.f64 d16,d9` (d9=48000.0 from
  `0x9E2F90`); `0x9E35E4 vdivle.f64 d16,d8,d16` (d8=24000.0) else
  `0x9E35E8 vmovgt.f64 d16,#0.5`.
- `0x9E35F0 bl 0x4CFF50` (log); `0x9E35F4 vldr s15,[v+0x88]`;
  `0x9E3600 vnmul.f64 d16,d17,d16`; `0x9E3608 bl 0x4D0064` (exp);
  `0x9E3610 vmul d16,d16,d8`; `0x9E3614 vdiv d16,d16,d9`;
  `0x9E3618 vmul d16,d16,d10` (d10=2*pi); `0x9E3620 bl 0x4D0070` (cos).
- `0x9E362C vsub.f64 d11,d11,d16` (2-cos); `0x9E3630 vnmls.f64 d17,d11,d11`
  (1-(2-cos)^2); `0x9E3634 vsqrt.f64 d16,d17`; NaN -> `0x9E52E4`.
- `0x9E3644 vsub.f64 d16,d16,d11`; `0x9E364C vcvt.f32.f64 s15,d16`;
  `0x9E3650 vadd.f32 s12,s15,s12` (s12=1.0); `0x9E3660 vstr s12,[v+0x94]`;
  `0x9E3664 vstr s15,[v+0x98]`.
- `0x9E3668 vcmpe.f32 s14,s25` (s25=48000.0 from `0x9E2FA8`);
  `0x9E3670 vdivmi s13,s14,s25` else `0x9E3674 vmovpl.f32 s13,#1.0`;
  `0x9E367C vstr s13,[v+0xa0]`; if `v+0x80 == 0`
  `0x9E3680 vmuleq s13,s13,s24` (s24=2*pi) and store.
- `0x9E3688 cmp lr,r3` (v+0x80 vs v+0xa4); if changed, convert the phase between
  radians and cycles: `0x9E369C vldreq s15,[v+0x9c]; 0x9E36A0 vmuleq s15,s15,s24`
  (0 -> 2*pi), else `0x9E51B0` multiplies by 1/(2*pi) (0.15915494 from `0x9E2FB0`).
- `0x9E36AC str lr,[v+0xa4]`; `0x9E36B0 strb r3,[v+0xa8]` (clear dirty flag).

### 1.7 Type-0 curve evaluation (the five shapes)

Loop over the `[arg+0x20]` chunks: `0x9E380C ldr r3,[arg+0x14]`... actually the
type-0 loop is the block ending at `0x9E37F4 bne 0x9E337C`, whose records are
`0x4C` and whose per-record state pointer increments by `0x28`
(`0x9E37E8 add r4,r4,#0x28`, `0x9E37F0 add r5,r5,#0x4c`). State init at
`0x9E3450..0x9E3488`: `s24=[r4-0x14]`, `s21=[r4-0x10]`, `s18=[r4-0x18]`,
`s27=[r4-8]`, `r7=[r4-4]`; `s19` is the phase floor `9.9999999e-09` from
`0x9E36C8`; `s20=(s26-s16)/N` is the per-block gain ramp.

Switch on the record+0x48 shape (0..4):
`0x9E3488 cmp r7,#4`; `0x9E348C addls pc,pc,r7,lsl #2`. Targets:
`0x9E3494 -> 0x9E416C` (0), `0x9E3498 -> 0x9E4024` (1),
`0x9E349C -> 0x9E3ECC` (2), `0x9E34A0 -> 0x9E3E1C` (3),
`0x9E34A4 -> 0x9E36D4` (4); out-of-range falls to `0x9E3490 -> 0x9E3784`.

All five share the same filter recurrence:
`out[n] = gain[n] * shape(phase) * c1 - out[n-1] * c2`, where `gain` ramps from
`s16` by `s20`, `c1 = s24`, `c2 = s21`, `out[n-1] = s18`, and the phase `s17`
increments by `s19` and wraps. The block is written to the float buffer; the
remaining float-buffer slots are zero-filled and the state is left with the
carry.

Constants (`0x9E3310 vldr s23,[pc,#-0x35c]` = `0xB9408E8F` = -0.00018363654;
`0x9E3324 vldr s22,[pc,#-0x36c]` = `0x3C081741` = 0.008306325;
`0x9E4010` = `0x3E2AA5D9` = 0.16664828; `0x9E4014` = `0x3F7FFFC7` = 0.9999966;
`0x9E400C` = `0x3FC90FDB` = pi/2; `0x9E401C` = `0x40490FDB` = 3*pi/2).

- **case 0 - sine (0x9E416C)**: folds the phase into [0,pi/2] using pi/2
  (`0x9E4174`), pi (`0x9E4234`), 3*pi/2 (`0x9E42F0`) and the polynomial
  `P(x) = 0.5*(1 + x*(0.9999966 - 0.16664828*x^2 + 0.008306325*x^4 - 0.00018363654*x^6))`.
  Evaluated at `0x9E41E0..0x9E4210`: `p=x*x`; `a=s22+p*s23`;
  `0x9E41F0 vnmls s11,s15,s14` (`= -0.16664828 + p*a`);
  `0x9E41FC vmla s11,s15,s14`; `0x9E4204 vmla s14,s17,s11`;
  `0x9E4208 vmul s15,s14,s13` (s13=0.5); `0x9E4210 vmul s15,s16,s15`.
  At x=pi/2 this is exactly 1.0; at x=0 it is 0.5, so it is 0.5*(1+sin).
- **case 1 - triangle (0x9E4024)**: `0x9E4090 vadd s15,s17,s17` (2*phase) then
  `0x9E4098 vmul s15,s15,s24; 0x9E409C vmls s15,s18,s21`; second half
  `0x9E4118 vsub s15,s25,s17; 0x9E4120 vadd s15,s15,s15` (2*(1-phase)).
- **case 2 - square (0x9E3ECC)**: first half `0x9E3F34 vmul s15,s16,s24;
  0x9E3F38 vmls s15,s18,s21` (input = 1); second half `0x9E3FB0 vmul s15,s24,s29`
  with `0x9E3EDC vldr s29,[pc,#0x124]` = 0.0 (input = 0).
- **case 3 - saw up (0x9E3E1C)**: `0x9E3E84 vmul s15,s16,s17;
  0x9E3E88 vmul s15,s15,s24; 0x9E3E8C vmls s15,s18,s21` (input = phase).
- **case 4 - saw down (0x9E36D4)**: `0x9E373C vsub s15,s25,s17;
  0x9E3740 vmul s15,s16,s15; 0x9E3744 vmul s15,s15,s24; 0x9E3748 vmls s15,s18,s21`
  (input = 1-phase).

Each case re-derives the next chunk boundary with `0x4A83F8` (ceilf) so the
per-sample loop is unrolled/NEON-vectorised; the four `0x9E36E4/0x9E3E2C/
0x9E3F30/...` blocks are the vector bodies. All cases wrap phase with
`vcmpe s17,s25` / `vsubge s17,s17,s25` (s25=1.0), except case 0 which wraps at
2*pi.

### 1.8 Type-1 curve evaluation (transition)

Loop over `[arg+0x14]` chunks: `0x9E380C ldr r3,[sp,#0x1c]` (arg), `0x9E3810 ldr r3,[r3,#0x14]`; `0x9E3828` reads
used (+0x1c) and capacity (+0x20); `0x9E3848 ldr r0,[r2,#8]` (used1),
`0x9E384C ldr r3,[r2,#4]` (data); `0x9E386C add r4,r3,#0x30` (stride 0x30);
`0x9E3DE0 add r4,r4,#0x30`. Per record:
- `0x9E3DF0 ldr ip,[r4,#-0x1c]` (record+0x14, the mode selector).
- If `!= 0`: `0x9E3E00 mov r0,sl; 0x9E3E04 mov r1,r8;
  0x9E3E08 sub r2,fp,#0x10; 0x9E3E0C mov r3,r5; 0x9E3E10 bl 0x9E52F8`.
- If `== 0`: `0x9E387C` inline four-segment ramp.

The inline path (0x9E387C..0x9E3DD4) reads record+0x04 (count), +0x08 (count),
+0x0c (time), +0x10 (float), +0x18 (float start), +0x1c (int >>1), +0x20
(float breakpoint), +0x24 (float end), +0x28 (count), +0x2c (count). It walks up
to four segments with slopes `record+0x10/N`, `(1-record+0x10)/N`,
`record+0x20/N`, `-prev/record+0x2c` (`0x9E39A4`, `0x9E3A3C`, `0x9E3AE0`,
`0x9E3B7C`), clamping each segment to the running min/max and storing the result
into the state at `fp-4`. The state struct is 0x10 bytes:
+0x00 = output float pointer (set by the caller), +0x04 = status (-1 then 3),
+0x08 = last value, +0x0c = running max.

### 1.9 Pool allocation and reuse

Allocator identity (read):
- `0xA7A7F4` - allocate a pool *object* (the 0x28-byte chunk node). It takes a
  pool handle in r0 and a size in r1, locks a mutex at object+0x20 via
  `0x4D3064`, and returns the object.
- `0xA7A894` - allocate a block from the pool: `0xA7A894 cmp r1,#0`; locks
  `0x4D3064`; `0xA7A8D4 ldr r0,[r5,#0x24]; 0xA7A8D8 bl 0xA7BB58`.
- `0xA7A914` - free a block: `0xA7A94C bl 0xA7B6C4`.
- `0xA7A988` - free/return a pool object.

Pool handle: `0x9E2BE0 ldr r3,[pc,#0x3cc]` = 0x0065D698 at `0x9E2FB4`;
`0x9E2BEC add r3,pc,r3` -> GOT base `0x104028C` (stored at `[sp,#0x18]`);
`0x9E501C ldr r3,[pc,#-0x10]` = 0xFFFFFDBC at `0x9E5014`;
`0x9E5028 ldr r5,[r2,r3]` -> `[0x1040248]`; `0x9E502C ldr r0,[r5]` = pool handle.

List `[arg+0x20]` node (alloc `0x9E501C..0x9E50D8`):
`0x9E5020 mov r1,#0x28` (node size); `0x9E5050 mov r1,#0x4c0` (buffer1 =
16*0x4C); `0x9E5058 mov r2,#0x10`; `0x9E5080 mov r8,#0x10`;
`0x9E5090 mov r1,#0x280` (buffer2 = 16*0x28). Fields: +4=buffer1, +0xc=cap1(16),
+0x10=buffer2, +0x18=cap2(16); the block is linked at `0x9E50B0..0x9E50D4`.

List `[arg+0x14]` node (alloc `0x9E50DC..0x9E5198`):
`0x9E50E0 mov r1,#0x28`; `0x9E5110 mov r1,#0x300` (buffer1 = 16*0x30);
`0x9E5150 mov r1,#0x100` (buffer2 = 16*0x10). Same field pattern.

Reuse: when a chunk's used count reaches its capacity the code advances to
`[node]` (`0x9E2D10`/`0x9E2DD4` etc.) and allocates a new node if null. The
float output buffer is grown lazily in the compaction at decomp 676-690 /
1474-1495: if `node+0x20 < node+0x1c`, free `node+0x24` via `0xA7A914`, allocate
`node+0x1c << 2` via `0xA7A894`, and set `node+0x20 = node+0x1c`. The tail
compaction frees whole nodes whose used count is below half capacity
(`0x9E2F4C..0x9E315C` and `0x9E3074..0x9E3158`), returning them with `0xA7A988`.

## Citations

- Caller/args: `0x9E6D2C`, `0x9E6D38`, `0x9E6D40`, `0x9E6D44`, `0x9E6D48`.
- Clear: `0x9E2BDC`, `0x9E2C04`, `0x9E2C08`, `0x9E2C0C`, `0x9E2C10`, `0x9E2C20`,
  `0x9E2C34`, `0x9E2C38`, `0x9E2C3C`.
- Voice walk / accumulator: `0x9E2C4C`, `0x9E2D90`, `0x9E2D9C`, `0x9E2DA0`,
  `0x9E2DA8`, `0x9E2DB4`, `0x9E2DC0`, `0x9E2C88`, `0x9E2CBC`, `0x9E2CC8`,
  `0x9E2CA4`, `0x9E2CB0`, `0x9E2CC0`, `0x9E2CE0`, `0x9E2CE4`.
- Type-1 record: `0x9E2CF8`, `0x9E2D18`, `0x9E2D20`, `0x9E2D34`, `0x9E2D4C`,
  `0x9E2D54`, `0x9E2D58`, `0x9E2D60`, `0x9E2D64`, `0x9E2D6C`, `0x9E2D70`,
  `0x9E2D7C`, `0x9E2D80`, `0x9E2D84`, `0x9E2D88`, `0x9E2D8C`.
- Type-0 record: `0x9E2DD4`, `0x9E2DF4`, `0x9E2E04`, `0x9E2E08`, `0x9E2E20`,
  `0x9E2ED8`, `0x9E2EDC`, `0x9E2EE4`, `0x9E2EF0`, `0x9E2EF4`, `0x9E2EF8`,
  `0x9E2F00`, `0x9E2F04`, `0x9E2F18`, `0x9E2F1C`, `0x9E2F20`, `0x9E2F24`,
  `0x9E2F34`.
- Modulator copy / fmodf: `0x9E2E38`, `0x9E2E44`, `0x9E2E4C`, `0x9E2E5C`,
  `0x9E2E64`, `0x9E2E88`, `0x9E2E8C`, `0x9E2E90`, `0x9E2E94`, `0x9E2E98`,
  `0x9E2EA0`, `0x9E2EA4`, `0x9E2EA8`, `0x9E2EB4`, `0x9E2EB8`.
- Coefficient recompute: `0x9E35C0`..`0x9E36B4`.
- Shape switch/cases: `0x9E3488`, `0x9E348C`, `0x9E3494`, `0x9E3498`,
  `0x9E349C`, `0x9E34A0`, `0x9E34A4`; case bodies `0x9E416C`, `0x9E4024`,
  `0x9E3ECC`, `0x9E3E1C`, `0x9E36D4`.
- Type-1 loop / 0x9E52F8 call: `0x9E380C`, `0x9E3828`, `0x9E3848`, `0x9E386C`,
  `0x9E3DE0`, `0x9E3DF0`, `0x9E3E10`, `0x9E387C`.
- Pools: `0x9E501C`..`0x9E50D8`, `0x9E50DC`..`0x9E5198`; allocators
  `0xA7A7F4`, `0xA7A894`, `0xA7A914`, `0xA7A988`.

## Classification

**EXACT_SOURCE** for the control flow, the two record layouts, the pool node
layout and allocation/reuse, the five shape arithmetic, and the coefficient
recompute: each behaviour-changing step above is read from the instructions.
Residual sub-items that I read structurally but did not instruction-by-instruction
verify: the exact NEON lane ordering of the unrolled vector bodies
(`0x9E36E4..0x9E3780`, `0x9E3E2C..0x9E3EC8`, etc.) and the whole compaction
free-list bookkeeping. Those are **RECOVERABLE_GAP**; what to read is the exact
ranges above. The identity of the `source` object at `voice+0x30` and of the
`voice+0x74..0x88` fields is **UNKNOWN** (no RTTI, no symbols, no bank/CLAD
reference found in this pass).

---

# Question 2 - 0x9E52F8(record, n, state, out)

## Answer

`r0` = record (0x30-byte type-1 record), `r1` = n (samples available this call),
`r2` = state struct (0x10 bytes), `r3` = pointer to the output float pointer
(`*r3` is the buffer). No callees.

Prologue:
- `0x9E5304 vldr s15,[r0,#0x18]` - start value `V0`.
- `0x9E530C ldr r1,[r0,#0x1c]`; `0x9E5320 lsr lr,r1,#1`; `0x9E532C add r7,lr,#2`;
  `0x9E5334 bic lr,r7,#3` -> `L1 = ((record[0x1c]>>1)+2) & ~3`.
- `0x9E5314 ldr r8,[r0,#8]`; `0x9E533C add r8,r8,#2`; `0x9E5344 bic r8,r8,#3`
  -> `L2 = (record[8]+2)&~3`.
- `0x9E5328 ldr r5,[r0,#4]`; `0x9E5348 bic r5,r5,#3` -> `L0 = record[4]&~3`.
- `0x9E5330 ldr sb,[r0,#0x2c]`; `0x9E534C add sb,sb,#2`; `0x9E5378 bic ip,sb,#3`
  -> `L4 = (record[0x2c]+2)&~3`.
- `0x9E531C ldr ip,[r0,#0x28]`; `0x9E5340 add sl,ip,#2`; `0x9E5368 bic ip,sl,#3`
  -> `L3 = (record[0x28]+2)&~3`.
- `0x9E5338 ldr r1,[r0,#0xc]`; `0x9E535C rsb r1,r6,r1` -> `t = record[0xc] - n`.
- `0x9E5350 mvn ip,#0`; `0x9E5360 str ip,[r2,#4]` -> `state[4] = -1`.
- `0x9E537C vstr s15,[r2,#0xc]` -> `state[0xc] = V0`.
- If `s15 > 0` (`0x9E5324 vcmpe.f32 s15,#0`; `0x9E5388 ble 0x9E53EC`):
  `0x9E538C vldr s14,[r0,#0x20]` (`B = record[0x20]`);
  `0x9E53B8 vdiv.f32 s13,s15,s14` (`V0/B`); `0x9E53BC vmov s14,r7` (L1);
  `0x9E53C4 vmul s14,s14,s13`; `0x9E53C8 vcvt.u32.f32`; `0x9E53D4 add r1,r1,#2`;
  `0x9E53D8 bic r1,r1,#3` -> `d = (L1*(V0/B)+2)&~3`; `0x9E53DC add t,t,d`;
  `0x9E53E0 add L2,L2,d`.
- `0x9E53EC cmp r1,#0`; if `t != 0` `0x9E53F4 vldr s15,[r0,#0x10]`
  (`V0 = record[0x10]`).
- Stage A (zeros): `0x9E53F8 cmp r1,r5`; if `t < L0`, count = min(L0-t, n), fill
  `out[0..count)` with 0; `t += count`, `n -= count`; clamp `state[0xc]` to >= 0;
  advance out.
- Stage B (attack): `0x9E540C add r4,L1,L0`; `0x9E5410 cmp L2,r4`;
  `0x9E5418 movhs ip,r4`; if `t < min(L2, L1+L0)`: slope `= B/L1`
  (`0x9E543C vcvt.f32.u32 s13,L1`; `0x9E544C vdivhi s14,s14,s13`), ramp from
  `V0` with the vector add at `0x9E5B0C...`; update `state[0xc]` = max.
- Stage C (decay): slope `= (1-B)/L1` (`0x9E5C00` region); same pattern.
- Stage D (sustain): constant `record[0x24]` (`0x9E5D20` region); `state[0xc]`
  clamped to >= `record[0x24]`.
- Stage E (release): slope `= -prev/L4`; writes the tail.
- Epilogue: `0x9E584C`/`0x9E5878` set `state[0xc]`; `0x9E57E8 str r3,[r2,#4]`
  (=3) and `0x9E57F4 vstr s15,[r2,#8]` (last value) when the curve ends with
  `n == 0` or `t >= L4`; otherwise `state[8]` keeps the last written value and
  `state[4]` stays -1. Only constant literal is 0.0 at `0x9E5E8C`.

The segment boundaries are the four counts `L0` (base), `L1` (fade length),
`L2` (L0+L1 extended by the `V0` correction), `L3`, `L4`. The floats
`record[0x18]` (start), `record[0x10]`, `record[0x20]` (breakpoint fraction),
`record[0x24]` (sustain), and `record[0x04]/[0x08]/[0x0c]/[0x1c]/[0x28]/[0x2c]`
define the shape. `state[0xc]` is a running maximum used by the caller.

## Citations

`0x9E52F8`, `0x9E5304`, `0x9E530C`, `0x9E5314`, `0x9E531C`, `0x9E5320`,
`0x9E5328`, `0x9E5330`, `0x9E5338`, `0x9E5350`, `0x9E535C`, `0x9E5360`,
`0x9E537C`, `0x9E5388`, `0x9E538C`, `0x9E53B8`, `0x9E53C8`, `0x9E53DC`,
`0x9E53E0`, `0x9E53EC`, `0x9E53F4`, `0x9E540C`, `0x9E543C`, `0x9E544C`,
`0x9E57E8`, `0x9E57F4`, `0x9E5E8C`.

## Classification

**EXACT_SOURCE** for the argument/field map, the segment boundaries and the
write-back to the state struct. The per-stage vector bodies (the `FloatVectorAdd`
NEON sequences between `0x9E5B0C` and `0x9E5E54`) are read for the boundary
arithmetic but not lane-by-lane; the exact per-sample order is a **RECOVERABLE_GAP**
(read `0x9E5A00..0x9E5E54`). No Wwise public name is asserted.

---

# Question 3 - record / field layout

## Type-1 record (0x30 B, list [arg+0x14], written at 0x9E2D18..0x9E2D8C)

| off | source | role |
|---|---|---|
| 0x00 | voice+0x44 | id/handle |
| 0x04 | voice+0x34 | count (base length) |
| 0x08 | voice+0x38 | count |
| 0x0c | voice+0x3c | accumulated time; `record[0xc]-tick` |
| 0x10 | voice+0x4c | float |
| 0x14 | voice+0x54 | mode selector (0 = inline transition, !=0 = 0x9E52F8) |
| 0x18 | voice+0x74 | float start |
| 0x1c | voice+0x78 | int, used `>>1` |
| 0x20 | voice+0x7c | float breakpoint |
| 0x24 | voice+0x80 | float sustain |
| 0x28 | voice+0x84 | count |
| 0x2c | voice+0x88 | count |

Consumed by `0x9E52F8` and by the inline transition at `0x9E387C`.

## Type-0 record (0x4C B, list [arg+0x20], written at 0x9E2EBC..0x9E2F34)

Same +0x00..+0x2c as the type-1 record, plus:

| off | source | role |
|---|---|---|
| 0x30 | voice+0x8c | float |
| 0x34 | voice+0x90 = source+0x10 | filter coefficient c1 (stored from state) |
| 0x38 | voice+0x94 = source+0x14 | filter coefficient c2 |
| 0x3c | voice+0x98 = source+0x18 | |
| 0x40 | voice+0x9c = fmodf(source+0x1c, 2*pi or 1.0) | phase |
| 0x44 | voice+0xa0 = source+0x20 (*2*pi if mode 0) | phase increment |
| 0x48 | voice+0xa4 = source+0x24 | shape selector 0..4 |

Consumed by the type-0 oscillator loop (switch at `0x9E3488`).

## Per-record state (0x28 B, list [arg+0x20] buffer2; 0x10 B for list [arg+0x14])

0x28 state: +0x00 out ptr, +0x04 status, +0x08 last value, +0x0c current,
+0x10 phase, +0x14 c1, +0x18 c2, +0x1c gain ramp, +0x20 gain step, +0x24 shape.
0x10 state (list A): +0x00 out ptr, +0x04 status (-1 then 3), +0x08 last value,
+0x0c running max. Both are written by the loop and by `0x9E52F8`.

## Classification

**EXACT_SOURCE** for the offsets and sources (citations above). The semantic
*names* of the fields (id, count, breakpoint, sustain) are labels inferred from
use, not symbols; they are not evidence. The meaning of `voice+0x54` (mode) and
`voice+0x74..0x88` is **UNKNOWN**.

---

# Question 4 - which Wwise concept

## Answer

The recovered code computes, per voice per frame, a block of `tick` float samples
from a phase accumulator. It is a **per-voice low-frequency oscillator with five
shapes plus a per-voice multi-segment transition/fade ramp**:

- the phase increments by `source+0x20` (scaled by 2*pi when the mode at
  `source+0x24` is 0) and wraps at 2*pi or 1.0 (`0x9E2E98`, `0x9E3680`,
  `0x9E369C`);
- `source+0x24` selects one of five shapes: sine (0), triangle (1), square (2),
  saw-up (3), saw-down (4), each filtered by the one-pole recurrence
  `y[n] = gain*shape*c1 - y[n-1]*c2` (`0x9E3488`, `0x9E3490..0x9E34A4`);
- the coefficients are derived from `voice+0x84` (frequency) and `voice+0x88`
  (damping) with log/exp/cos/sqrt doubles, using 48000.0 as the mix rate
  (`0x9E35C0..0x9E36B4`);
- the type-1 records instead write a transition/fade ramp (attack, decay,
  sustain, release) either inline (`0x9E387C`) or through `0x9E52F8`.

The Wwise class name of the manager (`0x108D8DC`) and of the `source` object at
`voice+0x30` is **UNKNOWN**: no RTTI, no symbol, and no bank/CLAD id is read by
this code. The behaviour is a parameter modulator/envelope, but the specific
Wwise feature name cannot be established from the binary, and I do not import
public Wwise documentation as evidence. What is established is only that this is
**not** the RTPC curve evaluator (M6-009): no RTPC id, curve id, point list or
accumulate flag is touched here.

## Citations

`0x9E3488`, `0x9E3490`..`0x9E34A4`, `0x9E3680`, `0x9E369C`, `0x9E35C0`..`0x9E36B4`,
`0x9E2E98`, `0x9E3E10`, `0x9E387C`.

## Classification

Behaviour **EXACT_SOURCE**. Wwise identity **UNKNOWN** (no RTTI/symbols/bank id;
attempted: symbol scan, `.rodata` strings, this code's field reads). The manager
and source class names stay UNKNOWN.

---

# Existing records touched

- **M6-022 (IMPLEMENTATION_GAP)**: its V28 row says `0x9E2BD0` and `0x9E52F8`
  are not transliterated. This pass supplies the bodies. V28's own behaviour
  (call `0x9E2BD0(manager+0x10, tick)`, lock, hash walk, tail `0x9E2AE4`) is
  confirmed; no correction.
- **M6-009 (RTPC)**: not affected - no RTPC data is read here.
- The prior pass's description of `0x9E2BD0` as a "per-voice
  modulator/transition curve evaluator plus pool allocator" is confirmed, with
  one correction: the two record sizes are **0x4C** (list `[arg+0x20]`, with the
  five LFO shapes) and **0x30** (list `[arg+0x14]`, transition only), not the
  0x28 that the Ghidra decompilation shows for the second buffer.

# Open questions for the manager

1. Do the `voice+0x30` source object and `voice+0x74..0x88` fields need a
   separate extractor pass, or are they settled by an existing voice/source
   record? They are the only inputs not identified.
2. Should the five LFO shapes and the transition ramps get their own manifest
   rows under M6-022, or be folded into it? The current M6-022 row names the
   functions but not the arithmetic.
3. The exact NEON lane order of the unrolled vector bodies and of `0x9E52F8`'s
   stages remains a RECOVERABLE_GAP; it matters only for bit-identical output.

