# M6-002 Vorbis float NEON IMDCT — extractor inventory (pass 4)

Read-only pass. Scope: the last M6-002 build gap, the float NEON inverse MDCT
`mdct_backward` (entry `0x00AB4E34`) and its helpers. Engine:
`resources/lib/armeabi-v7a/libcozmoEngine.so` (Wwise 2016.2 region is ARM mode). All addresses are VAs.
Builds on C7 (`re-analysis/evidence/m6-vorbis/vorbis-arithmetic-3.md`, rows 2a-2h) and C6 2a/2b.

Scratch: `.scratch/m6-vorbis4/` — `dis2.py`, `presym.txt`, `butterflies.txt`, `entry.txt`, `tail.txt`,
`alt.txt`, `caller.txt`, `gotdump.py`, `kfit.py`, `bits.py`, `xref2.py`. Nothing outside `.scratch/` was written.

## Headline

The engine's IMDCT is **not** the integer Tremor `mdct.c`, and it is **not** a straight float port of it either.
Its **control flow is Tremor-lowmem-shaped** — 2-arg in-place `mdct_backward(n,in)`, `shift=13-lowest_set_bit(n,>=4)`,
presymmetry then butterflies then a stage loop then a tail that applies `2^-24` — but:

- its `presymmetry` (0x00AB3D28) uses **five** trig tables and `vld4.32` deinterleave, where the reference uses **one**
  table and a scalar walk;
- its `butterflies` (0x00AB3FCC) uses **nine** trig tables and processes the array in 64-float chunks;
- the twiddle values are a **single 615-float master table** at `0x01004A40..0x010053DC`, not the reference's
  `sincos_lookup0`/`sincos_lookup1` (1026 + 1024 entries), and the 13 GOT pointers are 13 offsets into that one table;
- the stage after the butterflies (0x00AB4F0C..0x00AB5A1C) is a recursive twiddle-butterfly network, and the
  tail (0x00AB39D8) is a bit-reversed rotation, not the reference's `mdct_bitreverse`/`mdct_step7`/`mdct_step8` sequence
  in that order.

So the reference `tremor-lowmem-zephyr/mdct.c` (fetched this pass, branch `zephyr`) is a **structural guide only**.
The exact per-lane arithmetic and the per-stage table indexing are **not recovered**; they are RECOVERABLE_GAP with the
exact instruction ranges named below. The values of the master table, the unit constants, the stage order and the
`2^-24` tail scale are EXACT_SOURCE.

The table region is **615 floats, not ~1380**: the float data ends at `0x010053DC` (a NaN follows), and
`0x010053DC..0x01005FD0` is other .rodata (C7 2f's "~1380 floats" is an over-count).

## Row table

### A. Entry, shift, stage order (C7 2a-2c, re-read)

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| A1 | **Signature `mdct_backward(int n, float *in)`, in place, two args.** `r0 = n`, `r1 = in`. The only caller passes the per-channel spectrum buffer. | 0x00AB4E34 prologue; caller 0x00AB6EEC..0x00AB6F04 (`ldr r3,[r4,#0x14]`; `mov r0,r6`; `ldr r1,[r3,r5,lsl#2]`; `bl 0x00AB4E34`) | M6-002 / C7 2a | EXACT_SOURCE |
| A2 | **Global guard.** `r8 = [GOT 0x01040268] = 0x0108E648` (.bss). `if (*0x0108E648 == 0) return`. **But `*0x0108E648` is then used as a float-buffer base pointer, not as `n`:** the stage loop reads `*0x0108E648 + i*4` and the tail gathers `*0x0108E648 + bitrev*16`. C7 2a's parenthetical "its first word (`n`)" is therefore wrong; the object's first word is a pointer. **Nothing in the .text was found that writes `*0x0108E648`** (the only `-0x24` literal to a GOT base in the whole image is this function's at 0x00AB4FEC). Its writer and its aliasing with `in` are UNKNOWN (open Q1). | 0x00AB4E3C `ldr r3,[pc->0x00AB4FE8]=0x58b444` / 0x00AB4E40 `add r3,pc,r3`; 0x00AB4E50/0x00AB4E58 `ldr r8,[r2,ip]` (ip=-0x24); 0x00AB4E5C `ldr r3,[r8]` / 0x00AB4E60 `cmp r3,#0` / 0x00AB4E64 `beq 0x00AB5A48`; use as a buffer 0x00AB4FAC `ldr r1,[r8]`, 0x00AB5A2C `ldr r0,[r8]` | M6-002 / C7 2a (**partially contradicted**) | EXACT_SOURCE for the guard; the pointer role is NEW |
| A3 | **`shift` control.** `shift=4; while(!(n&(1<<shift)))shift++; shift=13-shift`. Bit 4 is peeled to the alternate entry 0x00AB5A54, which hard-sets `shift=9` and re-enters at 0x00AB5248. | 0x00AB4E68 `tst r0,#0x10` / 0x00AB4E74 `bne 0x00AB5A54`; 0x00AB4E78 `mov r5,#4` / 0x00AB4E7C `add r5,r5,#1` / 0x00AB4E80 `asr r3,r7,r5` / 0x00AB4E84 `tst r3,#1` / 0x00AB4E88 `beq 0x00AB4E7C` / 0x00AB4E94 `rsb r3,r5,#0xd`; 0x00AB5A54..0x00AB5A64 (`mov r2,#9`), 0x00AB5A90 `b 0x00AB5248` | C7 2b; alternate entry **NEW** | EXACT_SOURCE |
| A4 | **Stage order.** `presymmetry(in,n/2,shift)` (0x00AB3D28, r2 = shift), then `mdct_butterflies(in,n/2,shift)` (0x00AB3FCC), then the inlined stage loop 0x00AB4F0C..0x00AB5A1C, then the tail continuation 0x00AB39D8 (`b`, a tail call). | 0x00AB4EA4..0x00AB4EB8 `mov r2,r3`(shift) / `mov r1,r6`(n/2) / `bl 0x00AB3D28`; 0x00AB4EC0..0x00AB4ECC `bl 0x00AB3FCC`; 0x00AB4F0C..; 0x00AB5A44 `b 0x00AB39D8` | C7 2c | EXACT_SOURCE |

### B. `presymmetry` 0x00AB3D28..0x00AB3FCC

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| B1 | **Entry.** `r0=in`, `r1=n2`, `r2=shift`; the body first computes `r2 = shift+1` and `r3 = (shift+1)<<4`. | 0x00AB3D28 prologue; 0x00AB3D2C `add r2,r2,#1`; 0x00AB3D3C `lsl r3,r2,#4` | C7 2d | EXACT_SOURCE |
| B2 | **Five trig tables, offset `(shift+1)*16`.** GOT 0x01040234->0x01004E40, 0x01040248->0x01005240, 0x0104024C->0x01005000, 0x01040250->0x01005120, 0x01040254->0x01004EE0; each used at `table + (shift+1)*16`. | 0x00AB3D58 `ldr r2,[ip,r6]` (r6=-0x58); 0x00AB3D60 `ldr r4,[ip,r4]` (-0x44); 0x00AB3D6C `ldr r5,[ip,r5]` (-0x40); 0x00AB3D8C `ldr r4,[ip,r3]` (-0x3c); 0x00AB3DA0 `ldr ip,[ip,r6]` (-0x38); offsets applied 0x00AB3D68, 0x00AB3D74, 0x00AB3D80, 0x00AB3DA4, 0x00AB3DB0 | C7 2d (addresses NEW) | EXACT_SOURCE (addresses/offset); arithmetic RECOVERABLE_GAP |
| B3 | **Vectorised in-place walk.** `vld4.32` deinterleaves 4 consecutive floats into 4 lanes; the body processes 16 floats per iteration with two pointers (`r0` increasing, `r1` decreasing, `add/sub #0x40`, `cmp r0,r1`, `blo`). It uses `vmul.f32/vsub.f32/vadd.f32` on broadcast trig vectors. | 0x00AB3DFC `vld4.32 {d6,d8,d10,d12},[r0]`; 0x00AB3E18 `vld4.32 {d0,d2,d4,d6},[r3]`; 0x00AB3E2C `vld4.32 {d1,d3,d5,d7},[r2]`; 0x00AB3E44 `vld4.32 {d17,d19,d21,d23},[ip]`; 0x00AB3D30 vpush; 0x00AB3F68/0x00AB3F74/0x00AB3F78/0x00AB3F84/0x00AB3F90/0x00AB3F9C/0x00AB3FA0 stores; loop 0x00AB3F6C `add r0,r0,#0x40` / 0x00AB3F88 `sub r1,r1,#0x40` / 0x00AB3FA4 `blo 0x00AB3DC8` | C7 2d | **RECOVERABLE_GAP** — read 0x00AB3D28..0x00AB3FCC lane by lane |
| B4 | **The reference `presymmetry` is not this code.** Tremor-lowmem `presymmetry(in,n2,step)` uses one table (`sincos_lookup0`), `T+=step`/`T-=step`, and three scalar loops over `aX`/`bX`; the engine uses five tables at `(shift+1)*16` and a two-ended `vld4` walk. Whether the engine's output equals the reference's is UNKNOWN. | engine as B2/B3; reference `eulerdisk/tremor-lowmem-zephyr@zephyr:mdct.c` `presymmetry` | C7 2d (reference match) | **UNKNOWN** (mathematical equivalence) |

### C. `butterflies` 0x00AB3FCC..0x00AB4E34

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| C1 | **Entry and derived sizes.** `(x, points, shift)`; `lr=(shift+2)<<4`; `r2=(points+127)>>7` = points/128; `r6=(points+15)>>4` = points/16. | 0x00AB3FCC prologue; 0x00AB3FD8 `add lr,r2,#2`; 0x00AB3FF0 `lsl lr,lr,#4`; 0x00AB3FE0 `add r2,r1,#0x7f`; 0x00AB3FF8 `asr r2,r2,#7`; 0x00AB4000 `add r6,r1,#0xf`; 0x00AB4010 `asr r6,r6,#4`; stores 0x00AB4004/0x00AB400C | C7 2e (sizes); **NEW** (the (shift+2)<<4) | EXACT_SOURCE |
| C2 | **Nine trig tables.** GOT 0x01040258->0x01004BF0, 0x01040248->0x01005240, 0x01040250->0x01005120, 0x0104024C->0x01005000, 0x01040254->0x01004EE0, 0x0104025C->0x01004B60, 0x01040260->0x01004AD0, 0x01040264->0x01004A40, 0x01040234->0x01004E40. | 0x00AB3FE4 (-0x34), 0x00AB3FFC (-0x44), 0x00AB4018 (-0x3c), 0x00AB401C (-0x40), 0x00AB4020 (-0x38), 0x00AB4024 (-0x30), 0x00AB4038 (-0x2c), 0x00AB4048 (-0x28), 0x00AB4088 (-0x58) | C7 2e (addresses NEW) | EXACT_SOURCE (addresses); arithmetic RECOVERABLE_GAP |
| C3 | **Two passes over 64-float (0x100-byte) chunks.** Pass 1 0x00AB41A0..0x00AB472C (`cmp r3,lr`, lr = x + (points/128)*256 bytes); pass 2 0x00AB47E0..0x00AB4D74. Both do `vld1.32/vmul.f32/vadd.f32/vsub.f32/vtrn.32` and store back. | 0x00AB418C `add sl,r3,r1,lsl#8` (r1=points/128); 0x00AB41A0..; 0x00AB46F4 `cmp r3,lr` / 0x00AB472C `bne 0x00AB41A0`; 0x00AB47E0..; 0x00AB4D74 `bne 0x00AB47E0` | C7 2e | **RECOVERABLE_GAP** — read 0x00AB3FCC..0x00AB4E34 |
| C4 | **Relationship to the reference `mdct_butterflies`.** The reference = recursive `mdct_butterfly_generic` stages then `mdct_butterfly_32` in 32-float blocks; it uses no static table (only `sincos_lookup0` + `cPI1_8/2_8/3_8`). The engine's helper uses nine static tables and points/128 + points/16, so its stage decomposition differs. | engine C1-C3; reference `mdct.c` `mdct_butterflies`/`mdct_butterfly_generic` | C7 2e | **UNKNOWN** (decomposition) |

### D. The inlined stage loop 0x00AB4F0C..0x00AB5A1C

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| D1 | **Outer stage loop + inner block loop.** Outer index `i = [sp+0x20]` runs from 0 while `i != log2(n)-8` (bound stored at `[sp+0x2c]`); `fp = 1<<i` is the inner trip count. Data base is `*0x0108E648` (offset accumulator `ip`, advanced by `(n/2)>>i * 4` per stage); the trig pointers are the tables 0x01004E40, 0x01005120, 0x01004EE0, 0x01005240, 0x01005000 with per-stage offsets built from `(15-log2 n)<<4` and the `[sp+0x24]` accumulator. The exact per-lane formulas are in the cited instructions and were not transcribed. | 0x00AB4ED0 `cmp r5,#8` / 0x00AB4ED4 `rsb r3,r5,#0xf` / 0x00AB4ED8 `ble 0x00AB5248`; 0x00AB4EF0 `sub r0,r5,#8` / 0x00AB4EF8 `str r0,[sp,#0x2c]`; 0x00AB4F0C..0x00AB4F14 `lsl fp,r2,r3`; 0x00AB4F04 `ldr r2,[r1,r2]`; 0x00AB4F80 `add r5,r1,r0`; 0x00AB4FAC `ldr r1,[r8]`; 0x00AB4FD0..0x00AB4FE0 broadcasts; 0x00AB5218 `add ip,ip,r3`; 0x00AB5234..0x00AB5244 `cmp`/`bne 0x00AB4F0C` | C7 2c/2g (stage order); **NEW** (the index formulas) | **RECOVERABLE_GAP** — read 0x00AB4F0C..0x00AB5244 |
| D2 | **Second loop 0x00AB5260..0x00AB5A1C.** Single loop over `i` (0..n/2, step 0x80=128 floats) on `*0x0108E648`; uses the three butterfly-32 constants `0x3EC3EF15` (cPI3_8), `0x3F6C835E` (cPI1_8), `0x3F3504F3` (cPI2_8) as scalars, plus many `vtrn.32`. This is the shape of `mdct_butterfly_32`. | 0x00AB5248..0x00AB525C; 0x00AB5260..; 0x00AB5268 `vldr s15,[pc->0x00AB5008]=0x3ec3ef15`; 0x00AB5284 `vldr s7,[pc->0x00AB5004]=0x3f6c835e`; 0x00AB54D0 `vldr s15,[pc->0x00AB58B4]=0x3f3504f3`; 0x00AB5400 `cmp r7,r6`; 0x00AB5A1C `bgt 0x00AB5260` | C7 2g (constants); **NEW** (the loop/constant roles) | **RECOVERABLE_GAP** — read 0x00AB5260..0x00AB5A1C |

### E. Tail 0x00AB39D8..0x00AB3D28

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| E1 | **Tail call.** After the stage loop, `mdct_backward` tail-calls 0x00AB39D8 with `r0 = *0x0108E648`, `r1 = n`, `r2 = shift`, `r3 = in`. | 0x00AB5A20 `ldr r8,[sp,#0x84]`; 0x00AB5A24 `ldr r3,[sp,#0x64]`; 0x00AB5A28 `lsl r1,r3,#1`; 0x00AB5A2C `ldr r0,[r8]`; 0x00AB5A30 `ldr r2,[sp,#0x8c]`; 0x00AB5A34 `ldr r3,[sp,#0x88]`; 0x00AB5A44 `b 0x00AB39D8` | C7 2c/2h | EXACT_SOURCE |
| E2 | **`2^-24` output scale.** `q1 = [0x33800000, 0x33800000]` (2^-24) and `vmul.f32 q8,q8,q1` / `vmul.f32 q10,q10,q1` / `vmul.f32 q9,q9,q1` / `vmul.f32 q12,q12,q1`. | 0x00AB3AB0 `vldr d2,[pc->0x00AB3CF8]=0x33800000`; 0x00AB3AB4 `vldr d3,[pc->0x00AB3D00]=0x33800000`; 0x00AB3CB4 `vmul.f32 q8,q8,q1`; 0x00AB3CBC/0x00AB3CC0/0x00AB3CC4 | C6 2b / C7 2h | EXACT_SOURCE |
| E3 | **Unit constants.** `q0 = 0.5` (`vmov.f32 q0,#5.000000e-01`) and `s15 = 0x3F3504F3` (cPI2_8). | 0x00AB3A70 `vmov.f32 q0,#5.000000e-01`; 0x00AB3C2C `vldr s15,[pc->0x00AB3D08]=0x3f3504f3`; uses 0x00AB3C30/0x00AB3C34/0x00AB3C40/0x00AB3C4C/0x00AB3C58/0x00AB3C5C | C7 2g | EXACT_SOURCE |
| E4 | **Five trig tables and a bit-reverse table.** Tables 0x01004E40, 0x01004DD0, 0x01004D60, 0x01004CF0, 0x01004C80 at `(shift+1)*16`; the u16 bit-reverse table at 0x01004640, indexed `i` and `(n/16)-i`, shifted right by `shift-1`, then used as a float4 index into `*0x0108E648`; writes go to `in + i*16` and `in + (n/16-1-i)*16`. | 0x00AB3A10 (-0x58), 0x00AB3A20 (-0x50), 0x00AB3A28 (-0x4c), 0x00AB3A30 (-0x54), 0x00AB3A38 (-0x48); 0x00AB3A40/0x00AB3A64 `r6=0x01004640`; 0x00AB3AA8 `add r8,r6,r1,lsl#1`; 0x00AB3AFC `ldrh ip,[r8,#-2]!`; 0x00AB3B10 `ldrh lr,[lr,r5]` (r5=0x01004640); 0x00AB3B34/0x00AB3B38 `asr ip/lr,r4` (r4=shift-1); 0x00AB3B4C/0x00AB3B50 `add ip/lr,r4,ip,lsl#4`; 0x00AB3B00/0x00AB3B1C `add r4/r5,fp,r3/r2,lsl#4`; 0x00AB3CC8/0x00AB3CD8 stores | C7 2h (2^-24 only); **NEW** (tables/bitrev) | EXACT_SOURCE for the tables/bitrev; arithmetic **RECOVERABLE_GAP** — read 0x00AB39D8..0x00AB3D28 |
| E5 | **The bit-reverse table at 0x01004640 is `bitrev9`** (`[0]=0,[1]=256,[2]=128,[3]=384,...`), i.e. the 9-bit reversal used for the largest transform. Transcribed in full in Appendix 1. | 0x01004640..0x01004A40 (u16), values checked against `reverse9(i)` | C7 2h | EXACT_SOURCE (NEW) |

### F. The trig master table

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| F1 | **One contiguous float region, 615 floats**, `0x01004A40..0x010053DC`; a NaN (`0x7fc00000`-class) follows at 0x010053DC, then other .rodata. The 13 GOT pointers are 13 offsets into it: 0, 36, 72, 108, 144, 172, 200, 228, 256, 296, 368, 440, 512 (float offsets). | GOT 0x01040230..0x01040264 (dump in `.scratch/m6-vorbis4/gotdump.py`); end-of-float scan `.scratch/m6-vorbis4/endrun.py`; 0x010053DC = NaN | C7 2f (**length corrected**: 615, not ~1380) | EXACT_SOURCE (addresses/lengths NEW) |
| F2 | **Values are `±sin(k·pi/8192)` (equivalently `±cos(k·pi/8192)`) and `2·cos(k·pi/4096)`.** Examples: `0x01004CF0[0]=0x3f7fffc4=0.999996424 = cos(7·pi/8192)`; `0x01004E40[0..3]=0x3fffffb1=1.99999058 = 2·cos(8·pi/8192)`, `[4..7]=2·cos(16·pi/8192)`, `[8..11]=2·cos(32·pi/8192)`, `[12..15]=2·cos(64·pi/8192)` (each value repeated 4× = NEON lane broadcast); `0x01004A40[0..3]=sin(2020,2024,2028,2032 · pi/8192)`. | `.scratch/m6-vorbis4/kfit.py`, `bits.py`; hex of the first 16 floats in Appendix 2 | C7 2f (spot checks); **NEW** (formula + 4-lane layout) | EXACT_SOURCE |
| F3 | **Organisation.** Each sub-table is 4-float groups; within a group the angle index steps by a fixed amount, and the group base angle doubles (e.g. 0x01004CF0: `cos((2m+1)·2^g · pi/8192)`, m=3,2,1,0, g=0,1,2,...; 0x01004E40: `2·cos(2^(g+3)·pi/8192)` repeated 4×). This is a radix-2/radix-4 FFT twiddle layout, not Tremor's `sincos_lookup0`/`sincos_lookup1`. | `.scratch/m6-vorbis4/kfit.py` output; 0x01004E40 hex Appendix 2 | C7 2f (**reference match**); **NEW** (layout) | EXACT_SOURCE (layout observation) |
| F4 | **Per-stage selection and indexing.** Which sub-table each stage reads and the exact `k` formula per lane are encoded in the GOT offsets at 0x00AB3D58..0x00AB3DB0 (presymmetry), 0x00AB3FE4..0x00AB4088 (butterflies), 0x00AB4F04..0x00AB4F7C + 0x00AB4F80..0x00AB4FE0 (stage loop) and 0x00AB3A10..0x00AB3A38 (tail). Not transcribed. | as cited | C7 2f | **RECOVERABLE_GAP** — read those ranges |
| F5 | **Exact float bytes are authoritative.** Recomputing `sin(k·pi/8192)` in .NET does not reproduce every bit: of the 615 values, **375 equal `float(sin/cos(k·pi/8192))` exactly**, 131 differ by 1 ULP, 31 by 2 ULP and the rest by more (near-zero values and a few outliers; e.g. 0x01004A60 = 0.67609274 is 1 ULP from `float(sin(1936·pi/8192))`). The tables were produced with Wwise's own float32 `sinf/cosf` (or a recurrence), so a port must copy the 2460 bytes (or replicate that libm), not regenerate from the formula. | 0x01004A40..0x010053DC; `bits.py` | **NEW** | EXACT_SOURCE (observation) |

---

## Existing records contradicted or weakened by the source

1. **C7 2a: "its first word (`n`)" is wrong.** `*0x0108E648` is checked for zero, then used as a float-buffer base (`0x00AB4FAC ldr r1,[r8]`; `0x00AB5A2C ldr r0,[r8]`; `0x00AB3B4C add ip,r4,ip,lsl#4`). It is a pointer, not `n` (A2). The record's "in place" claim is not yet proven: the tail reads `*0x0108E648` and writes `in`, while presymmetry/butterflies write `in`; the two can only be consistent if `*0x0108E648 == in` at call time, and no writer of the global was found.
2. **C7 2f: "~1380 floats"** over-counts. The float region ends at 0x010053DC; it is 615 floats (F1).
3. **C7 2d/2e: the "reference lowmem `presymmetry`/`mdct_butterflies`" identification is too strong.** The engine's helpers have different table counts, different offsets and different vectorised walks (B4, C4). The reference is a guide; the match is not established.
4. **C7 2c: the stage order names.** "step7/step8" for 0x00AB4F0C..0x00AB5A1C is not supported: the loop is a recursive twiddle network (D1) followed by a `mdct_butterfly_32`-shaped loop (D2), not the reference's `mdct_step7`/`mdct_step8`. The tail (E) is a bit-reversed rotation + `2^-24`, not the reference's `mdct_bitreverse`.

## Existing records whose evidence is too weak to keep their status

- **M6-002 evidence string "IMDCT 0x00AB4E34 (the 2^-24 constants belong to 0x00AB39D8, which has no decode caller -> the normalisation is not established)".** 0x00AB39D8 **does** have a decode caller: `mdct_backward` tail-branches into it at 0x00AB5A44 (C6 already corrected this, but the M6-002 evidence list still carries the old wording in one entry). The `2^-24` is applied there; the remaining question is the **input scale** to `mdct_backward`, not the tail.
- **C7 2h ("tail ... already settled by C6 2a/2b; not re-derived").** C6 2a/2b read only the `2^-24`; the tail also reads five trig tables and the `bitrev9` table and does the rotation (E4). The record should say the tail's rotation is RECOVERABLE_GAP.
- **M6-002 "IMDCT ... the per-stage butterfly arithmetic and the trig-table indexing are RECOVERABLE_GAP"** remains correct, and this pass narrows it: the exact instruction ranges are now named (B3, C3, D1, D2, E4, F4) and the table layout is characterised (F1-F3).

## Open questions for the manager

1. **The writer of `*0x0108E648` is not located.** The global is in `.bss` (0x0108E648, section `.bss` 0x01059030+0x36f84), so it is zero at load, yet `mdct_backward` reads it as a data base and `mdct_backward` is its only reader (the only `-0x24`-from-GOT literal in the image is 0x00AB4FEC). Either a setup function writes it through a base+offset combination not found this pass, or `mdct_backward` is not in place and a copy of `in` into the global exists in a caller. This must be settled before any port: it decides whether the kernel is in place (C7 2a) or uses a second buffer. Recommended read: the Vorbis setup/allocator 0x00AB3780 and the window/overlap driver 0x00AB3520, plus a whole-image scan for stores to the GOT slot 0x01040268.
2. **Is the engine's transform mathematically Tremor's?** The control flow and the `shift` formula match; the twiddle tables, the `points/128`+`points/16` butterflies and the bit-reversed tail do not. Decide whether to (a) fund a full lane-level transliteration of the five ranges (B3, C3, D1, D2, E4) plus the table mapping (F4) to get a bit-exact port, or (b) accept an EQUIVALENT_IMPLEMENTATION (Tremor-lowmem `mdct.c` in float) and record the divergence as policy. The present code throws `NotSupportedException` for the kernel, which is the fail-closed choice.
3. **Float table transcription.** If (a), the 2460 bytes at 0x01004A40 must be vendored (F5). This is a new asset with a provenance/licence question (the values are Wwise-generated, not the BSD Tremor tables).
4. **The alternate entry 0x00AB5A54 (shift=9).** It is reachable for n with bit 4 set (n ≡ 16 mod 32) and re-enters at 0x00AB5248. Vorbis n is a power of two (shipped block sizes 64..8192, V2), so bit 4 is always clear and 0x00AB5A54 is **dead for shipped media**. Worth recording as a NEW dead path.
5. **C7's "13 precomputed trig tables"** should be restated as **one 615-float master table with 13 GOT views**, since that changes what a port copies.

## Notes on scope

- Read-only; nothing outside `.scratch/` was written. No repo generator, `fidelity.py`, or build was run.
- The C# was used only to see which behaviours the build needs; it is not evidence. The upstream Tremor-lowmem source
  (`https://github.com/eulerdisk/tremor-lowmem-zephyr`, branch `zephyr`, `mdct.c`/`mdct_lookup.h`) was fetched this pass and used
  only to interpret the binary; it is integer, and the engine is float, so it is a structural guide, not the source.

## Appendix 1: bit-reverse table 0x01004640 (first 48 u16)

`0, 256, 128, 384, 64, 320, 192, 448, 32, 288, 160, 416, 96, 352, 224, 480, 16, 272, 144, 400, 80, 336, 208, 464, 48, 304, 176, 432, 112, 368, 240, 496, 8, 264, 136, 392, 72, 328, 200, 456, 40, 296, 168, 424, 104, 360, 232, 488, ...`

(`bitrev9`; the table extends toward 0x01004A40.)

## Appendix 2: exact float bytes, 0x01004CF0 and 0x01004E40 (first 16 each)

0x01004CF0: `3f7fffc4 3f7fffe1 3f7ffff5 3f7fffff 3f7fff0e 3f7fff85 3f7fffd4 3f7ffffb 3f7ffc39 3f7ffe13 3f7fff4e 3f7fffec 3f7ff0e3 3f7ff84a 3f7ffd39 3f7fffb1`

0x01004E40: `3fffffb1 3fffffb1 3fffffb1 3fffffb1 3ffffec4 3ffffec4 3ffffec4 3ffffec4 3ffffb11 3ffffb11 3ffffb11 3ffffb11 3fffec43 3fffec43 3fffec43 3fffec43`

## Appendix 3: GOT map (0x01040230..0x0104026C)

| GOT | target | master offset (floats) | used by |
|---|---|---|---|
| 0x01040230 | 0x0108E638 | - | (not read this pass) |
| 0x01040234 | 0x01004E40 | 256 | presymmetry, butterflies, stage loop, tail |
| 0x01040238 | 0x01004CF0 | 172 | tail |
| 0x0104023C | 0x01004DD0 | 228 | tail |
| 0x01040240 | 0x01004D60 | 200 | tail |
| 0x01040244 | 0x01004C80 | 144 | tail |
| 0x01040248 | 0x01005240 | 512 | presymmetry, butterflies, stage loop |
| 0x0104024C | 0x01005000 | 368 | presymmetry, butterflies, stage loop |
| 0x01040250 | 0x01005120 | 440 | presymmetry, butterflies, stage loop |
| 0x01040254 | 0x01004EE0 | 296 | presymmetry, butterflies, stage loop |
| 0x01040258 | 0x01004BF0 | 108 | butterflies |
| 0x0104025C | 0x01004B60 | 72 | butterflies |
| 0x01040260 | 0x01004AD0 | 36 | butterflies |
| 0x01040264 | 0x01004A40 | 0 | butterflies |
| 0x01040268 | 0x0108E648 | - | mdct_backward global guard/buffer |
| 0x0104026C | 0x01058290 | - | codebook library |
