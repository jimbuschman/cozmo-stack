# X5: M6 exact float NEON inverse MDCT

Scope: `M6-002`, specifically the Wwise Vorbis inverse transform called at
`0x00AB6F04`. Primary source is
`resources/lib/armeabi-v7a/libcozmoEngine.so` (ARM mode). The earlier C8 report
`re-analysis/evidence/m6-vorbis/vorbis-imdct.md` was used only as a lead; every
instruction and data range below was re-read from the `.so`.

## Result

The transform can be transliterated without choosing a different MDCT. The
normative implementation is the five instruction ranges below, in their native
order, with the 584 binary32 words at `0x01004A40..0x01005360` and the 512-entry
`u16` bit-reversal table at `0x01004640..0x01004A40`. The tables are data, not
values to regenerate with host `sin`/`cos`: regeneration changes their bits.

The 31 words immediately following the trig region, at
`0x01005360..0x010053DC`, are integer masks `0, 1, 3, 7, ...,
0x3fffffff`; they are not binary32 table values. None of the 13 table views
crosses the `0x01005360` boundary.

For transliteration, each `qN` is four binary32 lanes `{d(2N)[0], d(2N)[1],
d(2N+1)[0], d(2N+1)[1]}`. `vmul.f32`, `vadd.f32`, `vsub.f32`, `vneg.f32` round
each lane to binary32 after that instruction; there is no fused multiply-add in
these ranges. `vrev64.32` swaps lanes 0/1 and 2/3. `vswp dA,dB` swaps the two
64-bit halves. `vtrn.32 dA,dB` maps `{a0,a1},{b0,b1}` to
`{a0,b0},{a1,b1}`. `vld4.32` de-interleaves four consecutive four-float
structures. A source-faithful scalar C# transliteration therefore uses four
`float` lanes per q-register and performs the listed operations in exactly the
listed order; algebraic reassociation is not allowed.

## Production-path rows

| step | what the original does | citation | record id or NEW | classification |
|---|---|---|---|---|
| X5-I1 | The packet inverse path calls `mdct_backward(n, pcm[channel])` once for each channel after floor/residue/coupling and before overlap. `n` is the selected block size and the second argument is that channel's planar float buffer. | `0x00AB6EEC ldr r6,[fp,#-0x2c]`; `0x00AB6EF4 ldr r3,[r4,#0x14]`; `0x00AB6EF8 mov r0,r6`; `0x00AB6EFC ldr r1,[r3,r5,lsl#2]`; `0x00AB6F04 bl 0x00AB4E34` | M6-002 | EXACT_SOURCE |
| X5-I2 | Entry checks the pointer stored at BSS word `0x0108E648`; zero returns without transforming. For the live path this word is the work-buffer base used by the stage network and tail. Its writer is not directly referenced through its own GOT slot anywhere else in ARM `.text`; see open question Q1. | `0x00AB4E3C..0x00AB4E64`; work-buffer loads `0x00AB4FAC`, `0x00AB526C`, `0x00AB5A2C`; GOT `0x01040268 -> 0x0108E648` | M6-002 | EXACT_SOURCE for the check and uses; RECOVERABLE_GAP for the writer/alias |
| X5-I3 | For power-of-two Vorbis sizes, find the first set bit starting at bit 5 and set `shift = 13-bit`; set `n2=n/2`. The bit-4 alternate sets `shift=9` and skips the first three phases, but is unreachable for shipped block sizes 64..8192. | `0x00AB4E68..0x00AB4EA0`; alternate `0x00AB5A54..0x00AB5A90`; accepted sizes `0x00AB6380..0x00AB63D8` | M6-002 | EXACT_SOURCE |
| X5-I4 | Run pre-symmetry in place on `(in,n/2,shift)`. It uses five table views and a two-ended 16-float walk. The instruction sequence in Appendix A is normative. | entry call `0x00AB4EA4..0x00AB4EB8`; body `0x00AB3D28..0x00AB3FB0`; table loads `0x00AB3D58..0x00AB3DBC` | M6-002 | EXACT_SOURCE |
| X5-I5 | Run the large butterfly in place on `(in,n/2,shift)`. It uses nine table views, derived counts `points/128` and `points/16`, and two 64-float-chunk vector passes. The instruction sequence in Appendix B is normative. | entry call `0x00AB4EC0..0x00AB4ECC`; body `0x00AB3FCC..0x00AB4E30` | M6-002 | EXACT_SOURCE |
| X5-I6 | If `log2(n)>8`, run the recursive twiddle stages. Outer stage `s=0..log2(n)-9`; inner block count `1<<s`; work extent `(n/2)>>s`; table view offset advances by 16 bytes per stage. Each inner block executes the two vector loops `0x00AB500C..0x00AB5108` and `0x00AB510C..0x00AB5208` in that order. Appendix C is normative. | setup `0x00AB4ED0..0x00AB4FA8`; arithmetic `0x00AB4FAC..0x00AB5208`; inner/outer increments `0x00AB520C..0x00AB5244` | M6-002 | EXACT_SOURCE |
| X5-I7 | Run the fixed-size terminal butterfly over the work buffer in 128-float advances. Each iteration covers 128 floats and uses `cPI3_8=0x3EC3EF15`, `cPI1_8=0x3F6C835E`, and `cPI2_8=0x3F3504F3`; Appendix C preserves every load, lane operation and store. | `0x00AB5248..0x00AB5A1C`; constants at `0x00AB5004`, `0x00AB5008`, `0x00AB58B0`, `0x00AB58B4` | M6-002 | EXACT_SOURCE |
| X5-I8 | Tail-call the bit-reversed rotation with `(work,n,shift,in)`. It gathers float4s using `bitrev9[i]>>(shift-1)` and `bitrev9[n/16-i]>>(shift-1)`, rotates them with five table views, multiplies all four output q-registers by `0x33800000` (`2^-24`), and stores from both ends of `in`. Appendix D is normative. | call setup `0x00AB5A20..0x00AB5A44`; body `0x00AB39D8..0x00AB3CF0`; scale `0x00AB3AB0..0x00AB3AB4`, `0x00AB3CB4..0x00AB3CC4` | M6-002 | EXACT_SOURCE |
| X5-I9 | The transform's trig data is one 584-word master region with 13 overlapping GOT views at float offsets 0, 36, 72, 108, 144, 172, 200, 228, 256, 296, 368, 440 and 512. Copy the words in Appendix E byte-for-byte. The adjacent 31 words are integer masks, not floats. | trig data `0x01004A40..0x01005360`; masks `0x01005360..0x010053DC`; GOT `0x01040234..0x01040264` | M6-002 | EXACT_SOURCE; C8 contradicted on the table boundary |
| X5-I10 | The bit-reversal data is the complete 9-bit reversal table, 512 little-endian `u16`s. Smaller transforms select it by the right shift rather than selecting another table. | data `0x01004640..0x01004A40`; loads/indexing `0x00AB3AA8..0x00AB3B50` | M6-002 | EXACT_SOURCE |

## Table views and byte offsets

| GOT slot | target | master float offset | byte offset | consumers |
|---|---:|---:|---:|---|
| `0x01040264` | `0x01004A40` | 0 | 0 | butterfly |
| `0x01040260` | `0x01004AD0` | 36 | 144 | butterfly |
| `0x0104025C` | `0x01004B60` | 72 | 288 | butterfly |
| `0x01040258` | `0x01004BF0` | 108 | 432 | butterfly |
| `0x01040244` | `0x01004C80` | 144 | 576 | tail |
| `0x01040238` | `0x01004CF0` | 172 | 688 | tail |
| `0x01040240` | `0x01004D60` | 200 | 800 | tail |
| `0x0104023C` | `0x01004DD0` | 228 | 912 | tail |
| `0x01040234` | `0x01004E40` | 256 | 1024 | all phases |
| `0x01040254` | `0x01004EE0` | 296 | 1184 | pre-symmetry, butterfly, stages |
| `0x0104024C` | `0x01005000` | 368 | 1472 | pre-symmetry, butterfly, stages |
| `0x01040250` | `0x01005120` | 440 | 1760 | pre-symmetry, butterfly, stages |
| `0x01040248` | `0x01005240` | 512 | 2048 | pre-symmetry, butterfly, stages |

All view adjustments in the code are byte adjustments. Pre-symmetry applies
`(shift+1)*16` bytes (four floats per shift) to its five views. The butterfly
uses `(shift+2)*16` bytes where loaded at `0x00AB3FD8..0x00AB4088`. The tail
uses `(shift+1)*16`, expressed as `r7=(shift+1)<<4`, with some views addressed
at `r7-32` and others at `r7` exactly as Appendix D lists.

## Existing record judgement

`M6-002` remains `IMPLEMENTATION_GAP` for B1. C8's statement that the kernel
arithmetic and indexing were `RECOVERABLE_GAP` is now settled by X5-I4..I10
and the normative appendices. C8's correction from “13 tables” to one master
region with 13 views is confirmed, but its 615-float boundary is contradicted:
the region has 584 floats followed by 31 integer masks. C8's correction that
the tail is reachable and applies `2^-24` is confirmed.

One production-path detail remains separate from the arithmetic: the native
writer/ownership of BSS word `0x0108E648` (Q1). This does not justify replacing
the transform. B1 can build the exact transform with an explicit work-buffer
argument, but must not claim the native allocation/alias path unless Q1 is
closed by integration.

## Open questions

1. **Q1 — work-buffer writer/alias.** The only aligned ARM literal
   `0xFFFFFFDC` selecting GOT slot `0x01040268` is at `0x00AB4FEC`, inside this
   function. The slot resolves to BSS `0x0108E648`. No direct writer through
   that slot exists in the ARM text. The adjacent object at `0x0108E638` is a
   Vorbis-look cache, but its visible users do not write `+0x10`. A gap pass
   should inspect constructors/indirect initialisers for the adjacent object
   and determine whether the word aliases the channel buffer or a separate
   reusable work allocation. Until then, the exact facts are: helpers mutate
   `in`; the stage/tail source is `*0x0108E648`; the tail destination is `in`.

## Reproduction commands

```text
python re-analysis/tools/arm_disasm.py AB3D28 AB3FCC
python re-analysis/tools/arm_disasm.py AB3FCC AB4E34
python re-analysis/tools/arm_disasm.py AB4E34 AB5A94
python re-analysis/tools/arm_disasm.py AB39D8 AB3D28
python re-analysis/tools/arm_disasm.py AB6E80 AB6F20
```

## Appendix B: large butterfly, 0x00AB3FCC..0x00AB4E30

```text
00ab3fcc  push     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
00ab3fd0  cmp      r1, #0
00ab3fd4  vpush    {d8, d9, d10, d11, d12, d13, d14, d15}
00ab3fd8  add      lr, r2, #2
00ab3fdc  ldr      ip, [pc, #0xe28] [pc->0xab4e0c]=0x58c298
00ab3fe0  add      r2, r1, #0x7f
00ab3fe4  ldr      r7, [pc, #0xe24] [pc->0xab4e10]=0xffffffcc
00ab3fe8  movge    r2, r1
00ab3fec  add      ip, pc, ip
00ab3ff0  lsl      lr, lr, #4
00ab3ff4  sub      sp, sp, #0x194
00ab3ff8  asr      r2, r2, #7
00ab3ffc  ldr      fp, [pc, #0xe10] [pc->0xab4e14]=0xffffffbc
00ab4000  add      r6, r1, #0xf
00ab4004  str      lr, [sp, #0x174]
00ab4008  movge    r6, r1
00ab400c  str      r2, [sp, #0x170]
00ab4010  asr      r6, r6, #4
00ab4014  ldr      r7, [ip, r7]
00ab4018  ldr      sl, [pc, #0xdf8] [pc->0xab4e18]=0xffffffc4
00ab401c  ldr      sb, [pc, #0xdf8] [pc->0xab4e1c]=0xffffffc0
00ab4020  ldr      r8, [pc, #0xdf8] [pc->0xab4e20]=0xffffffc8
00ab4024  ldr      r5, [pc, #0xdf8] [pc->0xab4e24]=0xffffffd0
00ab4028  ldr      fp, [ip, fp]
00ab402c  ldr      sl, [ip, sl]
00ab4030  ldr      sb, [ip, sb]
00ab4034  ldr      r8, [ip, r8]
00ab4038  ldr      r4, [pc, #0xde8] [pc->0xab4e28]=0xffffffd4
00ab403c  str      r7, [sp, #0x178]
00ab4040  str      r6, [sp, #0x18c]
00ab4044  ldr      r5, [ip, r5]
00ab4048  ldr      lr, [pc, #0xddc] [pc->0xab4e2c]=0xffffffd8
00ab404c  ldr      r2, [sp, #0x174]
00ab4050  str      r5, [sp, #0x17c]
00ab4054  ldr      r4, [ip, r4]
00ab4058  add      r2, r2, #0x80
00ab405c  ldr      r1, [sp, #0x170]
00ab4060  add      r7, r2, sl
00ab4064  str      r4, [sp, #0x180]
00ab4068  cmp      r1, #0
00ab406c  ldr      lr, [ip, lr]
00ab4070  add      r1, r2, fp
00ab4074  ldr      r4, [sp, #0x174]
00ab4078  vld1.32  {d16, d17}, [r1]
00ab407c  add      r1, r2, sb
00ab4080  str      lr, [sp, #0x184]
00ab4084  sub      r5, r4, #0x10
00ab4088  ldr      lr, [pc, #0xda0] [pc->0xab4e30]=0xffffffa8
00ab408c  add      r2, r2, r8
00ab4090  ldr      lr, [ip, lr]
00ab4094  add      ip, r4, #0x10
00ab4098  vstr     d16, [sp, #0x80]
00ab409c  vstr     d17, [sp, #0x88]
00ab40a0  add      ip, ip, lr
00ab40a4  add      lr, r4, lr
00ab40a8  str      ip, [sp, #0x188]
00ab40ac  add      ip, r4, #0x90
00ab40b0  ldr      r4, [sp, #0x17c]
00ab40b4  add      fp, ip, fp
00ab40b8  vld1.32  {d16, d17}, [r7]
00ab40bc  add      sl, ip, sl
00ab40c0  ldr      r7, [sp, #0x178]
00ab40c4  add      sb, ip, sb
00ab40c8  vstr     d16, [sp, #0xc0]
00ab40cc  vstr     d17, [sp, #0xc8]
00ab40d0  add      r8, ip, r8
00ab40d4  add      ip, r7, r5
00ab40d8  vld1.32  {d16, d17}, [r1]
00ab40dc  add      r1, r4, r5
00ab40e0  ldr      r4, [sp, #0x180]
00ab40e4  vstr     d16, [sp, #0x70]
00ab40e8  vstr     d17, [sp, #0x78]
00ab40ec  vld1.32  {d16, d17}, [r2]
00ab40f0  add      r2, r4, r5
00ab40f4  vstr     d16, [sp, #0xb0]
00ab40f8  vstr     d17, [sp, #0xb8]
00ab40fc  ldr      r4, [sp, #0x184]
00ab4100  vld1.32  {d16, d17}, [ip]
00ab4104  ldr      ip, [sp, #0x188]
00ab4108  add      r5, r4, r5
00ab410c  vstr     d16, [sp, #0x60]
00ab4110  vstr     d17, [sp, #0x68]
00ab4114  vld1.32  {d30, d31}, [r1]
00ab4118  vld1.32  {d16, d17}, [r2]
00ab411c  vld1.32  {d14, d15}, [r5]
00ab4120  vstr     d16, [sp, #0x50]
00ab4124  vstr     d17, [sp, #0x58]
00ab4128  vld1.32  {d16, d17}, [ip]
00ab412c  vstr     d16, [sp, #0x160]
00ab4130  vstr     d17, [sp, #0x168]
00ab4134  vld1.32  {d16, d17}, [fp]
00ab4138  vstr     d16, [sp, #0x90]
00ab413c  vstr     d17, [sp, #0x98]
00ab4140  vld1.32  {d16, d17}, [sl]
00ab4144  vstr     d16, [sp, #0x110]
00ab4148  vstr     d17, [sp, #0x118]
00ab414c  vld1.32  {d16, d17}, [sb]
00ab4150  vstr     d16, [sp, #0xa0]
00ab4154  vstr     d17, [sp, #0xa8]
00ab4158  vld1.32  {d16, d17}, [r8]
00ab415c  vstr     d16, [sp, #0x120]
00ab4160  vstr     d17, [sp, #0x128]
00ab4164  vld1.32  {d16, d17}, [lr]
00ab4168  vstr     d16, [sp, #0x20]
00ab416c  vstr     d17, [sp, #0x28]
00ab4170  ble      #0xab4d84
00ab4174  ldr      r1, [sp, #0x170]
00ab4178  add      r2, r6, r6, lsl #1
00ab417c  lsl      ip, r6, #5
00ab4180  lsl      fp, r6, #4
00ab4184  add      r2, r0, r2, lsl #4
00ab4188  vorr     q8, q7, q7
00ab418c  add      sl, r3, r1, lsl #8
00ab4190  vorr     q12, q15, q15
00ab4194  mov      r1, #0
00ab4198  str      sl, [sp, #0x150]
00ab419c  b        #0xab41ec ->0xab41ec
00ab41a0  vldr     d18, [sp, #0x10]
00ab41a4  vldr     d19, [sp, #0x18]
00ab41a8  vstr     d18, [sp, #0x50]
00ab41ac  vstr     d19, [sp, #0x58]
00ab41b0  vldr     d18, [sp, #0x30]
00ab41b4  vldr     d19, [sp, #0x38]
00ab41b8  vstr     d18, [sp, #0x60]
00ab41bc  vstr     d19, [sp, #0x68]
00ab41c0  vld1.64  {d18, d19}, [sp:0x40]
00ab41c4  vstr     d18, [sp, #0x70]
00ab41c8  vstr     d19, [sp, #0x78]
00ab41cc  vldr     d18, [sp, #0x40]
00ab41d0  vldr     d19, [sp, #0x48]
00ab41d4  vstr     d20, [sp, #0xa0]
00ab41d8  vstr     d21, [sp, #0xa8]
00ab41dc  vstr     d2, [sp, #0x90]
00ab41e0  vstr     d3, [sp, #0x98]
00ab41e4  vstr     d18, [sp, #0x80]
00ab41e8  vstr     d19, [sp, #0x88]
00ab41ec  add      lr, r0, r1
00ab41f0  add      r5, r1, ip
00ab41f4  vldr     d6, [sp, #0x20]
00ab41f8  vldr     d7, [sp, #0x28]
00ab41fc  add      r4, lr, fp
00ab4200  rsb      r6, fp, r5
00ab4204  add      r7, r4, #0x10
00ab4208  vldr     d20, [sp, #0x60]
00ab420c  vldr     d21, [sp, #0x68]
00ab4210  add      r6, r0, r6
00ab4214  vmul.f32 q1, q3, q10
00ab4218  add      sb, r4, #0x20
00ab421c  vld1.32  {d22, d23}, [r2]
00ab4220  add      r4, r4, #0x30
00ab4224  add      r8, r2, #0x20
00ab4228  add      r5, r0, r5
00ab422c  vorr     q15, q11, q11
00ab4230  vldr     d22, [sp, #0x50]
00ab4234  vldr     d23, [sp, #0x58]
00ab4238  vmul.f32 q14, q3, q11
00ab423c  add      sl, r3, #0x20
00ab4240  vld1.32  {d12, d13}, [r7]
00ab4244  add      r7, r2, #0x30
00ab4248  vsub.f32 q9, q1, q12
00ab424c  add      r1, r1, #0x40
00ab4250  vld1.32  {d22, d23}, [r6]
00ab4254  add      r6, r2, #0x10
00ab4258  vsub.f32 q2, q6, q11
00ab425c  add      r2, r2, #0x40
00ab4260  vld1.32  {d10, d11}, [sb]
00ab4264  add      sb, r3, #0x40
00ab4268  vadd.f32 q11, q6, q11
00ab426c  vld1.32  {d8, d9}, [r4]
00ab4270  add      r4, lr, ip
00ab4274  vorr     q13, q9, q9
00ab4278  vsub.f32 q9, q14, q8
00ab427c  vldr     d12, [sp, #0xb0]
00ab4280  vldr     d13, [sp, #0xb8]
00ab4284  vorr     q14, q3, q3
00ab4288  vldr     d6, [sp, #0x80]
00ab428c  vldr     d7, [sp, #0x88]
00ab4290  vsub.f32 q10, q4, q5
00ab4294  vstr     d26, [sp, #0x30]
00ab4298  vstr     d27, [sp, #0x38]
00ab429c  vmul.f32 q14, q14, q3
00ab42a0  vstr     d18, [sp, #0x10]
00ab42a4  vstr     d19, [sp, #0x18]
00ab42a8  vadd.f32 q4, q4, q5
00ab42ac  vld1.32  {d18, d19}, [r6]
00ab42b0  add      r6, r4, #0x20
00ab42b4  vsub.f32 q8, q15, q9
00ab42b8  vstr     d18, [sp, #0xe0]
00ab42bc  vstr     d19, [sp, #0xe8]
00ab42c0  vmul.f32 q9, q2, q13
00ab42c4  vldr     d26, [sp, #0x10]
00ab42c8  vldr     d27, [sp, #0x18]
00ab42cc  vmul.f32 q13, q2, q13
00ab42d0  vldr     d4, [sp, #0xc0]
00ab42d4  vldr     d5, [sp, #0xc8]
00ab42d8  vsub.f32 q14, q14, q2
00ab42dc  vstr     d8, [sp, #0x100]
00ab42e0  vstr     d9, [sp, #0x108]
00ab42e4  vstr     d18, [sp, #0x130]
00ab42e8  vstr     d19, [sp, #0x138]
00ab42ec  vldr     d18, [sp, #0x70]
00ab42f0  vldr     d19, [sp, #0x78]
00ab42f4  vstr     d28, [sp, #0x40]
00ab42f8  vstr     d29, [sp, #0x48]
00ab42fc  vldr     d28, [sp, #0x20]
00ab4300  vldr     d29, [sp, #0x28]
00ab4304  vmul.f32 q14, q14, q9
00ab4308  vld1.32  {d0, d1}, [r7]
00ab430c  add      r7, r4, #0x10
00ab4310  add      r4, r4, #0x30
00ab4314  vld1.32  {d2, d3}, [r8]
00ab4318  add      r8, lr, #0x10
00ab431c  vsub.f32 q12, q0, q1
00ab4320  vldr     d18, [sp, #0x10]
00ab4324  vldr     d19, [sp, #0x18]
00ab4328  vsub.f32 q4, q14, q6
00ab432c  vldr     d12, [sp, #0x30]
00ab4330  vldr     d13, [sp, #0x38]
00ab4334  vmul.f32 q5, q10, q9
00ab4338  vld1.32  {d14, d15}, [r7]
00ab433c  add      r7, lr, #0x20
00ab4340  vadd.f32 q1, q1, q0
00ab4344  vld1.32  {d6, d7}, [r4]
00ab4348  add      r4, r3, #0x90
00ab434c  vst1.64  {d8, d9}, [sp:0x40]
00ab4350  vmul.f32 q4, q10, q6
00ab4354  vorr     q10, q9, q9
00ab4358  vld1.32  {d4, d5}, [r5]
00ab435c  add      r5, r3, #0x50
00ab4360  vld1.32  {d28, d29}, [r8]
00ab4364  add      r8, r3, #0x80
00ab4368  vsub.f32 q13, q4, q13
00ab436c  vstr     d30, [sp, #0xd0]
00ab4370  vstr     d31, [sp, #0xd8]
00ab4374  vmul.f32 q4, q12, q6
00ab4378  vld1.32  {d30, d31}, [r6]
00ab437c  add      r6, lr, #0x30
00ab4380  vmul.f32 q10, q8, q10
00ab4384  vstr     d22, [sp, #0x140]
00ab4388  vstr     d23, [sp, #0x148]
00ab438c  vmul.f32 q8, q8, q6
00ab4390  vstr     d2, [sp, #0xf0]
00ab4394  vstr     d3, [sp, #0xf8]
00ab4398  vmul.f32 q12, q12, q9
00ab439c  vldr     d0, [sp, #0x130]
00ab43a0  vldr     d1, [sp, #0x138]
00ab43a4  vadd.f32 q4, q4, q10
00ab43a8  vldr     d22, [sp, #0xd0]
00ab43ac  vldr     d23, [sp, #0xd8]
00ab43b0  vadd.f32 q5, q0, q5
00ab43b4  vldr     d18, [sp, #0xe0]
00ab43b8  vldr     d19, [sp, #0xe8]
00ab43bc  vsub.f32 q12, q8, q12
00ab43c0  vldr     d20, [sp, #0x90]
00ab43c4  vldr     d21, [sp, #0x98]
00ab43c8  vadd.f32 q9, q11, q9
00ab43cc  vldr     d22, [sp, #0x160]
00ab43d0  vldr     d23, [sp, #0x168]
00ab43d4  vsub.f32 q8, q4, q5
00ab43d8  vld1.32  {d12, d13}, [lr]
00ab43dc  add      lr, r3, #0xd0
00ab43e0  vadd.f32 q5, q4, q5
00ab43e4  vld1.32  {d2, d3}, [r7]
00ab43e8  add      r7, r3, #0xc0
00ab43ec  vsub.f32 q4, q2, q7
00ab43f0  vld1.32  {d0, d1}, [r6]
00ab43f4  add      r6, r3, #0x10
00ab43f8  vadd.f32 q2, q2, q7
00ab43fc  vldr     d14, [sp, #0x40]
00ab4400  vldr     d15, [sp, #0x48]
00ab4404  vmul.f32 q10, q11, q10
00ab4408  vstr     d4, [sp, #0xb0]
00ab440c  vstr     d5, [sp, #0xb8]
00ab4410  vsub.f32 q2, q15, q3
00ab4414  vadd.f32 q15, q15, q3
00ab4418  vsub.f32 q3, q0, q1
00ab441c  vadd.f32 q0, q0, q1
00ab4420  vldr     d2, [sp, #0x110]
00ab4424  vldr     d3, [sp, #0x118]
00ab4428  vstr     d30, [sp, #0xc0]
00ab442c  vstr     d31, [sp, #0xc8]
00ab4430  vsub.f32 q15, q6, q14
00ab4434  vadd.f32 q14, q14, q6
00ab4438  vsub.f32 q1, q10, q1
00ab443c  vld1.64  {d20, d21}, [sp:0x40]
00ab4440  vmul.f32 q6, q4, q7
00ab4444  vstr     d0, [sp, #0xe0]
00ab4448  vstr     d1, [sp, #0xe8]
00ab444c  vmul.f32 q4, q4, q10
00ab4450  vstr     d28, [sp, #0xd0]
00ab4454  vstr     d29, [sp, #0xd8]
00ab4458  vmul.f32 q10, q2, q10
00ab445c  vsub.f32 q14, q12, q13
00ab4460  vadd.f32 q13, q12, q13
00ab4464  vorr     q12, q11, q11
00ab4468  vldr     d22, [sp, #0xa0]
00ab446c  vldr     d23, [sp, #0xa8]
00ab4470  vsub.f32 q10, q6, q10
00ab4474  vld1.64  {d12, d13}, [sp:0x40]
00ab4478  vmul.f32 q12, q12, q11
00ab447c  vldr     d22, [sp, #0x120]
00ab4480  vldr     d23, [sp, #0x128]
00ab4484  vmul.f32 q0, q8, q1
00ab4488  vstr     d20, [sp, #0x130]
00ab448c  vstr     d21, [sp, #0x138]
00ab4490  vmul.f32 q2, q2, q7
00ab4494  vsub.f32 q10, q12, q11
00ab4498  vldr     d22, [sp, #0x140]
00ab449c  vldr     d23, [sp, #0x148]
00ab44a0  vadd.f32 q2, q2, q4
00ab44a4  vmul.f32 q12, q14, q10
00ab44a8  vmul.f32 q4, q3, q7
00ab44ac  vmul.f32 q14, q14, q1
00ab44b0  vsub.f32 q0, q0, q12
00ab44b4  vsub.f32 q12, q9, q11
00ab44b8  vadd.f32 q9, q9, q11
00ab44bc  vldr     d22, [sp, #0xf0]
00ab44c0  vldr     d23, [sp, #0xf8]
00ab44c4  vmul.f32 q8, q8, q10
00ab44c8  vtrn.32  q0, q5
00ab44cc  vmul.f32 q3, q3, q6
00ab44d0  vstr     d18, [sp, #0x120]
00ab44d4  vstr     d19, [sp, #0x128]
00ab44d8  vadd.f32 q8, q14, q8
00ab44dc  vldr     d18, [sp, #0x100]
00ab44e0  vldr     d19, [sp, #0x108]
00ab44e4  vsub.f32 q9, q11, q9
00ab44e8  vldr     d28, [sp, #0xf0]
00ab44ec  vldr     d29, [sp, #0xf8]
00ab44f0  vmul.f32 q11, q15, q6
00ab44f4  vldr     d12, [sp, #0x90]
00ab44f8  vldr     d13, [sp, #0x98]
00ab44fc  vmul.f32 q15, q15, q7
00ab4500  vldr     d14, [sp, #0x120]
00ab4504  vldr     d15, [sp, #0x128]
00ab4508  vsub.f32 q11, q4, q11
00ab450c  vldr     d8, [sp, #0x100]
00ab4510  vldr     d9, [sp, #0x108]
00ab4514  vadd.f32 q3, q15, q3
00ab4518  vstr     d12, [sp, #0x110]
00ab451c  vstr     d13, [sp, #0x118]
00ab4520  vmul.f32 q15, q12, q1
00ab4524  vtrn.32  q8, q13
00ab4528  vadd.f32 q14, q14, q4
00ab452c  vmul.f32 q4, q9, q10
00ab4530  vmul.f32 q12, q12, q10
00ab4534  vmul.f32 q9, q9, q1
00ab4538  vsub.f32 q15, q15, q4
00ab453c  vldr     d8, [sp, #0x130]
00ab4540  vldr     d9, [sp, #0x138]
00ab4544  vsub.f32 q6, q3, q2
00ab4548  vadd.f32 q12, q9, q12
00ab454c  vtrn.32  q15, q7
00ab4550  vsub.f32 q9, q4, q11
00ab4554  vadd.f32 q11, q11, q4
00ab4558  vorr     d8, d0, d0
00ab455c  vtrn.32  q12, q14
00ab4560  vorr     d9, d30, d30
00ab4564  vadd.f32 q2, q3, q2
00ab4568  vorr     d6, d10, d10
00ab456c  vst1.32  {d8, d9}, [r3]
00ab4570  vorr     d7, d14, d14
00ab4574  vldr     d8, [sp, #0xd0]
00ab4578  vldr     d9, [sp, #0xd8]
00ab457c  vorr     d0, d1, d1
00ab4580  vorr     d1, d31, d31
00ab4584  vldr     d30, [sp, #0xb0]
00ab4588  vldr     d31, [sp, #0xb8]
00ab458c  vorr     d14, d16, d16
00ab4590  vsub.f32 q15, q15, q4
00ab4594  vst1.32  {d6, d7}, [sb]
00ab4598  add      sb, r3, #0x60
00ab459c  vorr     d8, d11, d11
00ab45a0  vldr     d10, [sp, #0xe0]
00ab45a4  vldr     d11, [sp, #0xe8]
00ab45a8  vorr     d16, d17, d17
00ab45ac  vorr     d17, d25, d25
00ab45b0  vldr     d6, [sp, #0xc0]
00ab45b4  vldr     d7, [sp, #0xc8]
00ab45b8  vorr     d9, d15, d15
00ab45bc  vsub.f32 q5, q5, q3
00ab45c0  vst1.32  {d0, d1}, [r8]
00ab45c4  add      r8, r3, #0xa0
00ab45c8  vmul.f32 q0, q6, q1
00ab45cc  vstr     d16, [sp, #0x90]
00ab45d0  vstr     d17, [sp, #0x98]
00ab45d4  vmul.f32 q8, q9, q10
00ab45d8  vst1.32  {d8, d9}, [r7]
00ab45dc  add      r7, r3, #0xe0
00ab45e0  vmul.f32 q9, q9, q1
00ab45e4  vldr     d8, [sp, #0xd0]
00ab45e8  vldr     d9, [sp, #0xd8]
00ab45ec  vorr     d15, d24, d24
00ab45f0  vmul.f32 q12, q6, q10
00ab45f4  vmul.f32 q3, q5, q1
00ab45f8  vmul.f32 q6, q15, q10
00ab45fc  vmul.f32 q5, q5, q10
00ab4600  vmul.f32 q15, q15, q1
00ab4604  vsub.f32 q8, q0, q8
00ab4608  vldr     d0, [sp, #0xb0]
00ab460c  vldr     d1, [sp, #0xb8]
00ab4610  vadd.f32 q12, q9, q12
00ab4614  vadd.f32 q0, q4, q0
00ab4618  vldr     d8, [sp, #0xe0]
00ab461c  vldr     d9, [sp, #0xe8]
00ab4620  vorr     d18, d26, d26
00ab4624  vtrn.32  q8, q11
00ab4628  vorr     d19, d28, d28
00ab462c  vorr     d28, d27, d27
00ab4630  vldr     d26, [sp, #0xc0]
00ab4634  vldr     d27, [sp, #0xc8]
00ab4638  vadd.f32 q13, q4, q13
00ab463c  vsub.f32 q3, q3, q6
00ab4640  vldr     d12, [sp, #0xa0]
00ab4644  vldr     d13, [sp, #0xa8]
00ab4648  vadd.f32 q15, q15, q5
00ab464c  vldr     d10, [sp, #0x80]
00ab4650  vldr     d11, [sp, #0x88]
00ab4654  vstr     d10, [sp, #0xc0]
00ab4658  vstr     d11, [sp, #0xc8]
00ab465c  vorr     q4, q12, q12
00ab4660  vtrn.32  q3, q0
00ab4664  vtrn.32  q15, q13
00ab4668  vldr     d10, [sp, #0x70]
00ab466c  vldr     d11, [sp, #0x78]
00ab4670  vstr     d12, [sp, #0x120]
00ab4674  vstr     d13, [sp, #0x128]
00ab4678  vorr     q6, q15, q15
00ab467c  vorr     d30, d17, d17
00ab4680  vstr     d10, [sp, #0xb0]
00ab4684  vstr     d11, [sp, #0xb8]
00ab4688  vorr     d31, d7, d7
00ab468c  vorr     d10, d16, d16
00ab4690  vorr     d11, d6, d6
00ab4694  vtrn.32  q4, q2
00ab4698  vorr     d6, d22, d22
00ab469c  vorr     d7, d0, d0
00ab46a0  vldr     d24, [sp, #0x60]
00ab46a4  vldr     d25, [sp, #0x68]
00ab46a8  vorr     d0, d23, d23
00ab46ac  vst1.32  {d10, d11}, [r6]
00ab46b0  add      r6, r3, #0x30
00ab46b4  vorr     d22, d8, d8
00ab46b8  vst1.32  {d6, d7}, [r5]
00ab46bc  add      r5, r3, #0x70
00ab46c0  vorr     d23, d12, d12
00ab46c4  vst1.32  {d30, d31}, [r4]
00ab46c8  add      r4, r3, #0xb0
00ab46cc  vorr     d8, d9, d9
00ab46d0  vst1.32  {d0, d1}, [lr]
00ab46d4  add      lr, r3, #0xf0
00ab46d8  str      lr, [sp, #0xa0]
00ab46dc  add      r3, r3, #0x100
00ab46e0  ldr      lr, [sp, #0x150]
00ab46e4  vorr     d9, d13, d13
00ab46e8  vorr     d12, d4, d4
00ab46ec  vst1.32  {d14, d15}, [sl]
00ab46f0  vorr     d13, d26, d26
00ab46f4  cmp      r3, lr
00ab46f8  vorr     d26, d5, d5
00ab46fc  ldr      lr, [sp, #0xa0]
00ab4700  vst1.32  {d18, d19}, [sb]
00ab4704  vldr     d18, [sp, #0x90]
00ab4708  vldr     d19, [sp, #0x98]
00ab470c  vldr     d16, [sp, #0x50]
00ab4710  vldr     d17, [sp, #0x58]
00ab4714  vst1.32  {d18, d19}, [r8]
00ab4718  vst1.32  {d28, d29}, [r7]
00ab471c  vst1.32  {d22, d23}, [r6]
00ab4720  vst1.32  {d12, d13}, [r5]
00ab4724  vst1.32  {d8, d9}, [r4]
00ab4728  vst1.32  {d26, d27}, [lr]
00ab472c  bne      #0xab41a0
00ab4730  ldr      r3, [sp, #0x170]
00ab4734  ldr      sl, [sp, #0x150]
00ab4738  lsl      r6, r3, #2
00ab473c  ldr      r3, [sp, #0x188]
00ab4740  vld1.32  {d16, d17}, [r3]
00ab4744  vstr     d16, [sp, #0x120]
00ab4748  vstr     d17, [sp, #0x128]
00ab474c  ldr      r2, [sp, #0x174]
00ab4750  ldr      r1, [sp, #0x178]
00ab4754  add      sb, r1, r2
00ab4758  ldr      r1, [sp, #0x17c]
00ab475c  add      r3, r1, r2
00ab4760  ldr      r1, [sp, #0x180]
00ab4764  vld1.32  {d16, d17}, [sb]
00ab4768  add      r5, r1, r2
00ab476c  ldr      r1, [sp, #0x184]
00ab4770  vstr     d16, [sp, #0x90]
00ab4774  vstr     d17, [sp, #0x98]
00ab4778  add      lr, r1, r2
00ab477c  ldr      r2, [sp, #0x170]
00ab4780  vld1.32  {d16, d17}, [r3]
00ab4784  cmp      r2, #0
00ab4788  vstr     d16, [sp, #0x110]
00ab478c  vstr     d17, [sp, #0x118]
00ab4790  vld1.32  {d16, d17}, [r5]
00ab4794  vstr     d16, [sp, #0xa0]
00ab4798  vstr     d17, [sp, #0xa8]
00ab479c  vld1.32  {d16, d17}, [lr]
00ab47a0  vstr     d16, [sp, #0x100]
00ab47a4  vstr     d17, [sp, #0x108]
00ab47a8  ble      #0xab4d78
00ab47ac  ldr      ip, [sp, #0x18c]
00ab47b0  add      r2, sl, r2, lsl #8
00ab47b4  str      r2, [sp, #0x160]
00ab47b8  add      r3, r0, r6, lsl #4
00ab47bc  add      r1, ip, r6
00ab47c0  vldr     d14, [sp, #0x80]
00ab47c4  vldr     d15, [sp, #0x88]
00ab47c8  add      r2, ip, r1
00ab47cc  add      sb, ip, r2
00ab47d0  add      r1, r0, r1, lsl #4
00ab47d4  add      r2, r0, r2, lsl #4
00ab47d8  add      sb, r0, sb, lsl #4
00ab47dc  b        #0xab482c ->0xab482c
00ab47e0  vldr     d16, [sp, #0xf0]
00ab47e4  vldr     d17, [sp, #0xf8]
00ab47e8  vstr     d16, [sp, #0x10]
00ab47ec  vstr     d17, [sp, #0x18]
00ab47f0  vldr     d16, [sp, #0x150]
00ab47f4  vldr     d17, [sp, #0x158]
00ab47f8  vstr     d16, [sp, #0x30]
00ab47fc  vstr     d17, [sp, #0x38]
00ab4800  vldr     d16, [sp, #0x80]
00ab4804  vldr     d17, [sp, #0x88]
00ab4808  vst1.64  {d16, d17}, [sp:0x40]
00ab480c  vldr     d16, [sp, #0xe0]
00ab4810  vldr     d17, [sp, #0xe8]
00ab4814  vstr     d10, [sp, #0xa0]
00ab4818  vstr     d11, [sp, #0xa8]
00ab481c  vstr     d4, [sp, #0x90]
00ab4820  vstr     d5, [sp, #0x98]
00ab4824  vstr     d16, [sp, #0x40]
00ab4828  vstr     d17, [sp, #0x48]
00ab482c  add      ip, r3, #0x10
00ab4830  add      r0, r1, #0x10
00ab4834  vldr     d28, [sp, #0x20]
00ab4838  vldr     d29, [sp, #0x28]
00ab483c  add      r5, sb, #0x20
00ab4840  add      r4, sb, #0x30
00ab4844  add      r7, r1, #0x20
00ab4848  vldr     d18, [sp, #0x30]
00ab484c  vldr     d19, [sp, #0x38]
00ab4850  add      r6, r1, #0x30
00ab4854  vmul.f32 q2, q14, q9
00ab4858  add      lr, sb, #0x10
00ab485c  vld1.32  {d30, d31}, [ip]
00ab4860  add      r8, r3, #0x30
00ab4864  add      ip, sl, #0xc0
00ab4868  add      fp, sl, #0x50
00ab486c  vstr     d30, [sp, #0xb0]
00ab4870  vstr     d31, [sp, #0xb8]
00ab4874  vldr     d30, [sp, #0x40]
00ab4878  vldr     d31, [sp, #0x48]
00ab487c  vmul.f32 q15, q14, q15
00ab4880  vld1.32  {d12, d13}, [r1]
00ab4884  add      r1, r1, #0x40
00ab4888  vld1.32  {d10, d11}, [r0]
00ab488c  add      r0, r3, #0x20
00ab4890  vsub.f32 q3, q5, q6
00ab4894  vldr     d22, [sp, #0x60]
00ab4898  vldr     d23, [sp, #0x68]
00ab489c  vsub.f32 q9, q2, q11
00ab48a0  vldr     d20, [sp, #0x10]
00ab48a4  vldr     d21, [sp, #0x18]
00ab48a8  vmul.f32 q8, q14, q10
00ab48ac  vldr     d22, [sp, #0x50]
00ab48b0  vldr     d23, [sp, #0x58]
00ab48b4  vsub.f32 q14, q15, q7
00ab48b8  vld1.32  {d2, d3}, [r5]
00ab48bc  add      r5, r2, #0x30
00ab48c0  vmul.f32 q12, q3, q9
00ab48c4  vstr     d18, [sp, #0x150]
00ab48c8  vstr     d19, [sp, #0x158]
00ab48cc  vsub.f32 q8, q8, q11
00ab48d0  vld1.64  {d14, d15}, [sp:0x40]
00ab48d4  vorr     q13, q9, q9
00ab48d8  vstr     d28, [sp, #0xe0]
00ab48dc  vstr     d29, [sp, #0xe8]
00ab48e0  vadd.f32 q5, q5, q6
00ab48e4  vld1.32  {d18, d19}, [r4]
00ab48e8  add      r4, sl, #0x40
00ab48ec  vsub.f32 q10, q9, q1
00ab48f0  vldr     d28, [sp, #0x20]
00ab48f4  vldr     d29, [sp, #0x28]
00ab48f8  vmul.f32 q7, q14, q7
00ab48fc  vstr     d24, [sp, #0x50]
00ab4900  vstr     d25, [sp, #0x58]
00ab4904  vorr     q12, q8, q8
00ab4908  vmul.f32 q8, q3, q8
00ab490c  vld1.32  {d8, d9}, [r7]
00ab4910  add      r7, r2, #0x10
00ab4914  vld1.32  {d0, d1}, [r6]
00ab4918  vadd.f32 q1, q1, q9
00ab491c  add      r6, r2, #0x20
00ab4920  vsub.f32 q11, q0, q4
00ab4924  vld1.32  {d4, d5}, [lr]
00ab4928  add      lr, sl, #0x80
00ab492c  vadd.f32 q0, q0, q4
00ab4930  vldr     d8, [sp, #0x70]
00ab4934  vldr     d9, [sp, #0x78]
00ab4938  vsub.f32 q4, q7, q4
00ab493c  vstr     d20, [sp, #0x60]
00ab4940  vstr     d21, [sp, #0x68]
00ab4944  vld1.32  {d20, d21}, [sb]
00ab4948  vorr     q7, q13, q13
00ab494c  add      sb, sb, #0x40
00ab4950  vstr     d16, [sp, #0xc0]
00ab4954  vstr     d17, [sp, #0xc8]
00ab4958  vsub.f32 q8, q10, q2
00ab495c  vld1.32  {d6, d7}, [r0]
00ab4960  vadd.f32 q10, q10, q2
00ab4964  add      r0, sl, #0x10
00ab4968  vld1.32  {d30, d31}, [r8]
00ab496c  add      r8, sl, #0x90
00ab4970  vldr     d28, [sp, #0x50]
00ab4974  vldr     d29, [sp, #0x58]
00ab4978  vstr     d24, [sp, #0xf0]
00ab497c  vstr     d25, [sp, #0xf8]
00ab4980  vmul.f32 q12, q11, q12
00ab4984  vstr     d0, [sp, #0xd0]
00ab4988  vstr     d1, [sp, #0xd8]
00ab498c  vmul.f32 q11, q11, q13
00ab4990  vld1.32  {d0, d1}, [r7]
00ab4994  add      r7, sl, #0xd0
00ab4998  vadd.f32 q12, q14, q12
00ab499c  vstr     d8, [sp, #0x80]
00ab49a0  vstr     d9, [sp, #0x88]
00ab49a4  vstr     d10, [sp, #0x130]
00ab49a8  vstr     d11, [sp, #0x138]
00ab49ac  vld1.32  {d8, d9}, [r6]
00ab49b0  add      r6, sl, #0x60
00ab49b4  vld1.32  {d10, d11}, [r5]
00ab49b8  add      r5, sl, #0x20
00ab49bc  str      r5, [sp, #0x140]
00ab49c0  add      r5, sl, #0xe0
00ab49c4  vldr     d18, [sp, #0x60]
00ab49c8  vldr     d19, [sp, #0x68]
00ab49cc  vldr     d12, [sp, #0xf0]
00ab49d0  vldr     d13, [sp, #0xf8]
00ab49d4  vstr     d2, [sp, #0x50]
00ab49d8  vstr     d3, [sp, #0x58]
00ab49dc  vmul.f32 q1, q9, q13
00ab49e0  vmul.f32 q13, q9, q6
00ab49e4  vldr     d18, [sp, #0x30]
00ab49e8  vldr     d19, [sp, #0x38]
00ab49ec  vmul.f32 q6, q8, q6
00ab49f0  vld1.32  {d4, d5}, [r2]
00ab49f4  add      r2, r2, #0x40
00ab49f8  vmul.f32 q8, q8, q7
00ab49fc  vstr     d18, [sp, #0x60]
00ab4a00  vstr     d19, [sp, #0x68]
00ab4a04  vldr     d18, [sp, #0xa0]
00ab4a08  vldr     d19, [sp, #0xa8]
00ab4a0c  vadd.f32 q6, q1, q6
00ab4a10  vldr     d2, [sp, #0x120]
00ab4a14  vldr     d3, [sp, #0x128]
00ab4a18  vsub.f32 q13, q8, q13
00ab4a1c  vldr     d28, [sp, #0xc0]
00ab4a20  vldr     d29, [sp, #0xc8]
00ab4a24  vmul.f32 q8, q1, q9
00ab4a28  vldr     d18, [sp, #0xe0]
00ab4a2c  vldr     d19, [sp, #0xe8]
00ab4a30  vsub.f32 q1, q2, q0
00ab4a34  vadd.f32 q2, q2, q0
00ab4a38  vadd.f32 q0, q4, q5
00ab4a3c  vsub.f32 q11, q11, q14
00ab4a40  vld1.32  {d28, d29}, [r3]
00ab4a44  add      r3, r3, #0x40
00ab4a48  vstr     d4, [sp, #0x30]
00ab4a4c  vstr     d5, [sp, #0x38]
00ab4a50  vsub.f32 q2, q4, q5
00ab4a54  vadd.f32 q5, q15, q3
00ab4a58  vstr     d0, [sp, #0x70]
00ab4a5c  vstr     d1, [sp, #0x78]
00ab4a60  vsub.f32 q4, q15, q3
00ab4a64  vldr     d6, [sp, #0xb0]
00ab4a68  vldr     d7, [sp, #0xb8]
00ab4a6c  vsub.f32 q15, q14, q3
00ab4a70  vadd.f32 q3, q3, q14
00ab4a74  vldr     d28, [sp, #0x100]
00ab4a78  vldr     d29, [sp, #0x108]
00ab4a7c  vstr     d10, [sp, #0xc0]
00ab4a80  vstr     d11, [sp, #0xc8]
00ab4a84  vsub.f32 q5, q8, q14
00ab4a88  vldr     d16, [sp, #0x80]
00ab4a8c  vldr     d17, [sp, #0x88]
00ab4a90  vsub.f32 q7, q6, q12
00ab4a94  vsub.f32 q14, q11, q13
00ab4a98  vstr     d6, [sp, #0xb0]
00ab4a9c  vstr     d7, [sp, #0xb8]
00ab4aa0  vmul.f32 q0, q2, q9
00ab4aa4  vadd.f32 q12, q6, q12
00ab4aa8  vadd.f32 q11, q13, q11
00ab4aac  vldr     d26, [sp, #0x120]
00ab4ab0  vldr     d27, [sp, #0x128]
00ab4ab4  vmul.f32 q6, q1, q9
00ab4ab8  vmul.f32 q1, q1, q8
00ab4abc  vmul.f32 q8, q2, q8
00ab4ac0  vldr     d4, [sp, #0x90]
00ab4ac4  vldr     d5, [sp, #0x98]
00ab4ac8  vmul.f32 q2, q13, q2
00ab4acc  vldr     d26, [sp, #0x110]
00ab4ad0  vldr     d27, [sp, #0x118]
00ab4ad4  vmul.f32 q3, q7, q5
00ab4ad8  vsub.f32 q8, q6, q8
00ab4adc  vldr     d12, [sp, #0x130]
00ab4ae0  vldr     d13, [sp, #0x138]
00ab4ae4  vsub.f32 q2, q2, q13
00ab4ae8  vmul.f32 q13, q4, q9
00ab4aec  vadd.f32 q0, q0, q1
00ab4af0  vstr     d16, [sp, #0x100]
00ab4af4  vstr     d17, [sp, #0x108]
00ab4af8  vmul.f32 q1, q14, q2
00ab4afc  vorr     q8, q9, q9
00ab4b00  vstr     d26, [sp, #0x110]
00ab4b04  vstr     d27, [sp, #0x118]
00ab4b08  vmul.f32 q14, q14, q5
00ab4b0c  vldr     d18, [sp, #0x80]
00ab4b10  vldr     d19, [sp, #0x88]
00ab4b14  vmul.f32 q13, q7, q2
00ab4b18  vldr     d14, [sp, #0x50]
00ab4b1c  vldr     d15, [sp, #0x58]
00ab4b20  vadd.f32 q1, q1, q3
00ab4b24  vsub.f32 q3, q10, q6
00ab4b28  vadd.f32 q6, q10, q6
00ab4b2c  vldr     d20, [sp, #0xd0]
00ab4b30  vldr     d21, [sp, #0xd8]
00ab4b34  vmul.f32 q4, q4, q9
00ab4b38  vtrn.32  q1, q12
00ab4b3c  vsub.f32 q7, q10, q7
00ab4b40  vmul.f32 q10, q15, q9
00ab4b44  vldr     d18, [sp, #0xd0]
00ab4b48  vldr     d19, [sp, #0xd8]
00ab4b4c  vmul.f32 q15, q15, q8
00ab4b50  vldr     d16, [sp, #0x50]
00ab4b54  vldr     d17, [sp, #0x58]
00ab4b58  vsub.f32 q14, q13, q14
00ab4b5c  vldr     d26, [sp, #0x110]
00ab4b60  vldr     d27, [sp, #0x118]
00ab4b64  vsub.f32 q10, q13, q10
00ab4b68  vadd.f32 q15, q15, q4
00ab4b6c  vmul.f32 q13, q7, q2
00ab4b70  vtrn.32  q14, q11
00ab4b74  vmul.f32 q4, q3, q5
00ab4b78  vmul.f32 q7, q7, q5
00ab4b7c  vmul.f32 q3, q3, q2
00ab4b80  vadd.f32 q4, q13, q4
00ab4b84  vadd.f32 q9, q8, q9
00ab4b88  vldr     d16, [sp, #0x10]
00ab4b8c  vldr     d17, [sp, #0x18]
00ab4b90  vsub.f32 q3, q3, q7
00ab4b94  vstr     d16, [sp, #0x50]
00ab4b98  vstr     d17, [sp, #0x58]
00ab4b9c  str      r6, [sp, #0xd0]
00ab4ba0  add      r6, sl, #0xa0
00ab4ba4  vtrn.32  q4, q6
00ab4ba8  vorr     d26, d2, d2
00ab4bac  vldr     d16, [sp, #0x100]
00ab4bb0  vldr     d17, [sp, #0x108]
00ab4bb4  vorr     d27, d8, d8
00ab4bb8  vsub.f32 q7, q10, q8
00ab4bbc  vtrn.32  q3, q9
00ab4bc0  vorr     d8, d3, d3
00ab4bc4  vldr     d2, [sp, #0x30]
00ab4bc8  vldr     d3, [sp, #0x38]
00ab4bcc  vadd.f32 q10, q10, q8
00ab4bd0  vst1.32  {d26, d27}, [sl]
00ab4bd4  vsub.f32 q8, q15, q0
00ab4bd8  vadd.f32 q0, q15, q0
00ab4bdc  vldr     d30, [sp, #0xb0]
00ab4be0  vldr     d31, [sp, #0xb8]
00ab4be4  vsub.f32 q15, q15, q1
00ab4be8  vldr     d2, [sp, #0xc0]
00ab4bec  vldr     d3, [sp, #0xc8]
00ab4bf0  vorr     d26, d24, d24
00ab4bf4  vorr     d27, d12, d12
00ab4bf8  vorr     d24, d25, d25
00ab4bfc  vorr     d25, d13, d13
00ab4c00  vldr     d12, [sp, #0x70]
00ab4c04  vldr     d13, [sp, #0x78]
00ab4c08  vsub.f32 q1, q1, q6
00ab4c0c  vst1.32  {d26, d27}, [r4]
00ab4c10  add      r4, sl, #0x30
00ab4c14  vorr     d27, d6, d6
00ab4c18  vorr     d6, d29, d29
00ab4c1c  vst1.32  {d8, d9}, [lr]
00ab4c20  vorr     d26, d28, d28
00ab4c24  add      lr, sl, #0x70
00ab4c28  vst1.32  {d24, d25}, [ip]
00ab4c2c  vmul.f32 q14, q7, q2
00ab4c30  add      ip, sl, #0xb0
00ab4c34  vmul.f32 q12, q8, q5
00ab4c38  vstr     d6, [sp, #0x10]
00ab4c3c  vstr     d7, [sp, #0x18]
00ab4c40  vmul.f32 q4, q15, q5
00ab4c44  vldr     d12, [sp, #0xb0]
00ab4c48  vldr     d13, [sp, #0xb8]
00ab4c4c  vmul.f32 q3, q7, q5
00ab4c50  vmul.f32 q7, q8, q2
00ab4c54  vmul.f32 q8, q15, q2
00ab4c58  vmul.f32 q15, q1, q5
00ab4c5c  vadd.f32 q12, q14, q12
00ab4c60  vldr     d28, [sp, #0x30]
00ab4c64  vldr     d29, [sp, #0x38]
00ab4c68  vadd.f32 q6, q6, q14
00ab4c6c  vmul.f32 q1, q1, q2
00ab4c70  vadd.f32 q8, q8, q15
00ab4c74  vtrn.32  q12, q10
00ab4c78  vld1.64  {d30, d31}, [sp:0x40]
00ab4c7c  vsub.f32 q3, q7, q3
00ab4c80  vsub.f32 q4, q1, q4
00ab4c84  vldr     d2, [sp, #0xa0]
00ab4c88  vldr     d3, [sp, #0xa8]
00ab4c8c  vtrn.32  q8, q6
00ab4c90  vorr     d28, d22, d22
00ab4c94  vorr     d29, d18, d18
00ab4c98  vstr     d2, [sp, #0x100]
00ab4c9c  vstr     d3, [sp, #0x108]
00ab4ca0  vorr     d22, d23, d23
00ab4ca4  vorr     d23, d19, d19
00ab4ca8  vldr     d2, [sp, #0x90]
00ab4cac  vldr     d3, [sp, #0x98]
00ab4cb0  vldr     d14, [sp, #0xc0]
00ab4cb4  vldr     d15, [sp, #0xc8]
00ab4cb8  vldr     d18, [sp, #0x70]
00ab4cbc  vldr     d19, [sp, #0x78]
00ab4cc0  vadd.f32 q9, q7, q9
00ab4cc4  vstr     d2, [sp, #0x110]
00ab4cc8  vstr     d3, [sp, #0x118]
00ab4ccc  vorr     d2, d21, d21
00ab4cd0  vstr     d30, [sp, #0x70]
00ab4cd4  vstr     d31, [sp, #0x78]
00ab4cd8  vorr     d3, d13, d13
00ab4cdc  vorr     d30, d24, d24
00ab4ce0  vorr     d31, d16, d16
00ab4ce4  vtrn.32  q3, q0
00ab4ce8  vorr     d16, d25, d25
00ab4cec  vorr     d24, d20, d20
00ab4cf0  vtrn.32  q4, q9
00ab4cf4  vorr     d25, d12, d12
00ab4cf8  vst1.32  {d30, d31}, [r0]
00ab4cfc  add      r0, sl, #0xf0
00ab4d00  str      r0, [sp]
00ab4d04  add      sl, sl, #0x100
00ab4d08  vst1.32  {d24, d25}, [fp]
00ab4d0c  vorr     d20, d6, d6
00ab4d10  ldr      r0, [sp, #0x160]
00ab4d14  vorr     d21, d8, d8
00ab4d18  vst1.32  {d16, d17}, [r8]
00ab4d1c  vorr     d6, d7, d7
00ab4d20  vorr     d8, d0, d0
00ab4d24  cmp      sl, r0
00ab4d28  vst1.32  {d2, d3}, [r7]
00ab4d2c  vorr     d7, d9, d9
00ab4d30  ldr      r7, [sp, #0x140]
00ab4d34  vorr     d9, d18, d18
00ab4d38  vldr     d16, [sp, #0x10]
00ab4d3c  vldr     d17, [sp, #0x18]
00ab4d40  vorr     d18, d1, d1
00ab4d44  vst1.32  {d26, d27}, [r7]
00ab4d48  ldr      r7, [sp, #0xd0]
00ab4d4c  vldr     d14, [sp, #0x40]
00ab4d50  vldr     d15, [sp, #0x48]
00ab4d54  vst1.32  {d28, d29}, [r7]
00ab4d58  vst1.32  {d16, d17}, [r6]
00ab4d5c  ldr      r0, [sp]
00ab4d60  vst1.32  {d22, d23}, [r5]
00ab4d64  vst1.32  {d20, d21}, [r4]
00ab4d68  vst1.32  {d8, d9}, [lr]
00ab4d6c  vst1.32  {d6, d7}, [ip]
00ab4d70  vst1.32  {d18, d19}, [r0]
00ab4d74  bne      #0xab47e0
00ab4d78  add      sp, sp, #0x194
00ab4d7c  vpop     {d8, d9, d10, d11, d12, d13, d14, d15}
00ab4d80  pop      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
00ab4d84  vldr     d16, [sp, #0x50]
00ab4d88  vldr     d17, [sp, #0x58]
00ab4d8c  mov      sl, r3
00ab4d90  mov      r6, #0
00ab4d94  vstr     d16, [sp, #0x10]
00ab4d98  vstr     d17, [sp, #0x18]
00ab4d9c  vldr     d16, [sp, #0x60]
00ab4da0  vldr     d17, [sp, #0x68]
00ab4da4  vstr     d16, [sp, #0x30]
00ab4da8  vstr     d17, [sp, #0x38]
00ab4dac  vldr     d16, [sp, #0x70]
00ab4db0  vldr     d17, [sp, #0x78]
00ab4db4  vst1.64  {d16, d17}, [sp:0x40]
00ab4db8  vldr     d16, [sp, #0x80]
00ab4dbc  vldr     d17, [sp, #0x88]
00ab4dc0  vstr     d16, [sp, #0x40]
00ab4dc4  vstr     d17, [sp, #0x48]
00ab4dc8  vldr     d16, [sp, #0x160]
00ab4dcc  vldr     d17, [sp, #0x168]
00ab4dd0  vstr     d16, [sp, #0x120]
00ab4dd4  vstr     d17, [sp, #0x128]
00ab4dd8  vldr     d16, [sp, #0xb0]
00ab4ddc  vldr     d17, [sp, #0xb8]
00ab4de0  vstr     d16, [sp, #0x70]
00ab4de4  vstr     d17, [sp, #0x78]
00ab4de8  vldr     d16, [sp, #0xc0]
00ab4dec  vldr     d17, [sp, #0xc8]
00ab4df0  vstr     d14, [sp, #0x50]
00ab4df4  vstr     d15, [sp, #0x58]
00ab4df8  vstr     d30, [sp, #0x60]
00ab4dfc  vstr     d31, [sp, #0x68]
00ab4e00  vstr     d16, [sp, #0x80]
00ab4e04  vstr     d17, [sp, #0x88]
00ab4e08  b        #0xab474c ->0xab474c
00ab4e0c  .word    0x0058c298
00ab4e10  .word    0xffffffcc
00ab4e14  .word    0xffffffbc
00ab4e18  .word    0xffffffc4
00ab4e1c  .word    0xffffffc0
00ab4e20  .word    0xffffffc8
00ab4e24  .word    0xffffffd0
00ab4e28  .word    0xffffffd4
00ab4e2c  .word    0xffffffd8
00ab4e30  .word    0xffffffa8

```

The following appendices are literal disassemblies from the `.so`. Literal-pool
words after function returns are not executable. They are retained where their
address is cited by a preceding load.

## Appendix A: pre-symmetry, 0x00AB3D28..0x00AB3FB0

```text
00ab3d28  push     {r4, r5, r6, r7, r8, sb, sl, lr}
00ab3d2c  add      r2, r2, #1
00ab3d30  vpush    {d8, d9, d10, d11, d12, d13, d14, d15}
00ab3d34  sub      r1, r1, #0xc0000010
00ab3d38  ldr      ip, [pc, #0x274] [pc->0xab3fb4]=0x58c53c
00ab3d3c  lsl      r3, r2, #4
00ab3d40  ldr      r6, [pc, #0x270] [pc->0xab3fb8]=0xffffffa8
00ab3d44  sub      lr, r3, #0x10
00ab3d48  add      ip, pc, ip
00ab3d4c  ldr      r4, [pc, #0x268] [pc->0xab3fbc]=0xffffffbc
00ab3d50  ldr      r5, [pc, #0x268] [pc->0xab3fc0]=0xffffffc0
00ab3d54  sub      sp, sp, #0x70
00ab3d58  ldr      r2, [ip, r6]
00ab3d5c  add      r1, r0, r1, lsl #2
00ab3d60  ldr      r4, [ip, r4]
00ab3d64  cmp      r0, r1
00ab3d68  add      r2, r3, r2
00ab3d6c  ldr      r5, [ip, r5]
00ab3d70  ldr      r3, [pc, #0x24c] [pc->0xab3fc4]=0xffffffc4
00ab3d74  add      r4, lr, r4
00ab3d78  vld1.32  {d16, d17}, [r2]
00ab3d7c  vorr     q10, q8, q8
00ab3d80  add      r5, lr, r5
00ab3d84  ldr      r6, [pc, #0x23c] [pc->0xab3fc8]=0xffffffc8
00ab3d88  vld1.32  {d22, d23}, [r4]
00ab3d8c  ldr      r4, [ip, r3]
00ab3d90  mov      r3, r1
00ab3d94  vstr     d16, [sp, #0x60]
00ab3d98  vstr     d17, [sp, #0x68]
00ab3d9c  vmul.f32 q8, q8, q11
00ab3da0  ldr      ip, [ip, r6]
00ab3da4  add      r4, lr, r4
00ab3da8  vld1.32  {d18, d19}, [r5]
00ab3dac  vmul.f32 q13, q10, q9
00ab3db0  add      ip, lr, ip
00ab3db4  vld1.32  {d28, d29}, [r4]
00ab3db8  vsub.f32 q15, q8, q14
00ab3dbc  vld1.32  {d28, d29}, [ip]
00ab3dc0  vsub.f32 q14, q13, q14
00ab3dc4  bhs      #0xab3fa8
00ab3dc8  vldr     d16, [sp, #0x60]
00ab3dcc  vldr     d17, [sp, #0x68]
00ab3dd0  add      ip, r0, #0x20
00ab3dd4  vmul.f32 q1, q8, q15
00ab3dd8  add      r2, r3, #0x20
00ab3ddc  vmov.32  sl, d30[1]
00ab3de0  add      r7, r0, #0x10
00ab3de4  vmul.f32 q8, q8, q14
00ab3de8  add      r6, r0, #0x30
00ab3dec  vmov.32  sb, d28[1]
00ab3df0  add      r5, r1, #0x10
00ab3df4  add      r4, r1, #0x20
00ab3df8  add      lr, r1, #0x30
00ab3dfc  vld4.32  {d6, d8, d10, d12}, [r0]
00ab3e00  vsub.f32 q10, q1, q11
00ab3e04  vsub.f32 q8, q8, q9
00ab3e08  vorr     q7, q6, q6
00ab3e0c  vorr     q6, q5, q5
00ab3e10  vorr     q5, q4, q4
00ab3e14  vorr     q4, q3, q3
00ab3e18  vld4.32  {d0, d2, d4, d6}, [r3]
00ab3e1c  sub      r3, r3, #0x40
00ab3e20  vorr     q12, q10, q10
00ab3e24  vorr     q13, q8, q8
00ab3e28  vorr     q9, q5, q5
00ab3e2c  vld4.32  {d1, d3, d5, d7}, [r2]
00ab3e30  vorr     q8, q4, q4
00ab3e34  vorr     q10, q6, q6
00ab3e38  vmov.32  r8, d24[0]
00ab3e3c  vorr     q11, q7, q7
00ab3e40  vmov.32  r2, d26[0]
00ab3e44  vld4.32  {d17, d19, d21, d23}, [ip]
00ab3e48  vstr     d24, [sp, #0x40]
00ab3e4c  vstr     d25, [sp, #0x48]
00ab3e50  vstmia   sp, {d16, d17, d18, d19, d20, d21, d22, d23}
00ab3e54  vorr     q8, q15, q15
00ab3e58  vorr     q9, q14, q14
00ab3e5c  vld1.64  {d24, d25}, [sp:0x40]
00ab3e60  vmov.32  d18[0], sb
00ab3e64  vmov.32  d16[0], sl
00ab3e68  vorr     q10, q9, q9
00ab3e6c  vmov.32  sb, d19[0]
00ab3e70  vmov.32  sl, d17[0]
00ab3e74  vldr     d14, [sp, #0x20]
00ab3e78  vldr     d15, [sp, #0x28]
00ab3e7c  vmul.f32 q6, q7, q14
00ab3e80  vstr     d26, [sp, #0x50]
00ab3e84  vstr     d27, [sp, #0x58]
00ab3e88  vmul.f32 q13, q12, q15
00ab3e8c  vrev64.32 q9, q3
00ab3e90  vrev64.32 q5, q1
00ab3e94  vmul.f32 q7, q7, q15
00ab3e98  vrev64.32 q4, q2
00ab3e9c  vmul.f32 q12, q12, q14
00ab3ea0  vrev64.32 q11, q0
00ab3ea4  vswp     d8, d9
00ab3ea8  vsub.f32 q3, q6, q13
00ab3eac  vswp     d22, d23
00ab3eb0  vldr     d0, [sp, #0x10]
00ab3eb4  vldr     d1, [sp, #0x18]
00ab3eb8  vorr     d4, d11, d11
00ab3ebc  vorr     d5, d10, d10
00ab3ec0  vldr     d2, [sp, #0x30]
00ab3ec4  vldr     d3, [sp, #0x38]
00ab3ec8  vadd.f32 q12, q12, q7
00ab3ecc  vswp     d18, d19
00ab3ed0  vrev64.32 q3, q3
00ab3ed4  vmul.f32 q13, q2, q15
00ab3ed8  vmov.32  d20[1], sb
00ab3edc  vmul.f32 q2, q2, q14
00ab3ee0  vmov.32  d16[1], sl
00ab3ee4  vmul.f32 q7, q9, q14
00ab3ee8  vmov.32  sl, d17[1]
00ab3eec  vmul.f32 q9, q9, q15
00ab3ef0  vmov.32  sb, d21[1]
00ab3ef4  vadd.f32 q13, q13, q7
00ab3ef8  vswp     d6, d7
00ab3efc  vsub.f32 q9, q9, q2
00ab3f00  vrev64.32 q12, q12
00ab3f04  vst1.64  {d6, d7}, [sp:0x40]
00ab3f08  vswp     d24, d25
00ab3f0c  vrev64.32 q13, q13
00ab3f10  vrev64.32 q2, q9
00ab3f14  vorr     q9, q14, q14
00ab3f18  vswp     d26, d27
00ab3f1c  vswp     d4, d5
00ab3f20  vldr     d28, [sp, #0x50]
00ab3f24  vldr     d29, [sp, #0x58]
00ab3f28  vmov.32  d17[0], sl
00ab3f2c  vmov.32  d21[0], sb
00ab3f30  vmov.32  d17[1], r8
00ab3f34  vmul.f32 q6, q4, q8
00ab3f38  vmov.32  d21[1], r2
00ab3f3c  vmul.f32 q3, q4, q10
00ab3f40  vmul.f32 q5, q0, q10
00ab3f44  vmul.f32 q4, q11, q10
00ab3f48  vmul.f32 q0, q0, q8
00ab3f4c  vmul.f32 q11, q11, q8
00ab3f50  vmul.f32 q8, q1, q8
00ab3f54  vmul.f32 q10, q1, q10
00ab3f58  vadd.f32 q3, q11, q3
00ab3f5c  vsub.f32 q4, q6, q4
00ab3f60  vadd.f32 q8, q5, q8
00ab3f64  vsub.f32 q0, q10, q0
00ab3f68  vst1.32  {d8, d9}, [r0]
00ab3f6c  add      r0, r0, #0x40
00ab3f70  vorr     q11, q15, q15
00ab3f74  vst1.32  {d16, d17}, [r7]
00ab3f78  vst1.32  {d6, d7}, [ip]
00ab3f7c  vld1.64  {d6, d7}, [sp:0x40]
00ab3f80  vst1.32  {d0, d1}, [r6]
00ab3f84  vst1.32  {d6, d7}, [r1]
00ab3f88  sub      r1, r1, #0x40
00ab3f8c  cmp      r0, r1
00ab3f90  vst1.32  {d26, d27}, [r5]
00ab3f94  vldr     d30, [sp, #0x40]
00ab3f98  vldr     d31, [sp, #0x48]
00ab3f9c  vst1.32  {d24, d25}, [r4]
00ab3fa0  vst1.32  {d4, d5}, [lr]
00ab3fa4  blo      #0xab3dc8
00ab3fa8  add      sp, sp, #0x70
00ab3fac  vpop     {d8, d9, d10, d11, d12, d13, d14, d15}
00ab3fb0  pop      {r4, r5, r6, r7, r8, sb, sl, pc}
00ab3fb4  subseq   ip, r8, ip, lsr r5
00ab3fb8  .word    0xffffffa8
00ab3fbc  .word    0xffffffbc
00ab3fc0  .word    0xffffffc0
00ab3fc4  .word    0xffffffc4
00ab3fc8  .word    0xffffffc8

```

## Appendix C: entry and stage network, 0x00AB4E34..0x00AB5A50

```text
00ab4e34  push     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
00ab4e38  vpush    {d8, d9, d10, d11, d12, d13, d14, d15}
00ab4e3c  ldr      r3, [pc, #0x1a4] [pc->0xab4fe8]=0x58b444
00ab4e40  add      r3, pc, r3
00ab4e44  sub      sp, sp, #0x94
00ab4e48  mov      r2, r3
00ab4e4c  str      r3, [sp, #0x28]
00ab4e50  ldr      r3, [pc, #0x194] [pc->0xab4fec]=0xffffffdc
00ab4e54  mov      ip, r3
00ab4e58  ldr      r8, [r2, ip]
00ab4e5c  ldr      r3, [r8]
00ab4e60  cmp      r3, #0
00ab4e64  beq      #0xab5a48
00ab4e68  tst      r0, #0x10
00ab4e6c  str      r1, [sp, #0x88]
00ab4e70  mov      r7, r0
00ab4e74  bne      #0xab5a54
00ab4e78  mov      r5, #4
00ab4e7c  add      r5, r5, #1
00ab4e80  asr      r3, r7, r5
00ab4e84  tst      r3, #1
00ab4e88  beq      #0xab4e7c
00ab4e8c  add      r7, r7, r7, lsr #31
00ab4e90  ldr      sb, [sp, #0x88]
00ab4e94  rsb      r3, r5, #0xd
00ab4e98  str      r3, [sp, #0x8c]
00ab4e9c  asr      r2, r7, #1
00ab4ea0  str      r2, [sp, #0x64]
00ab4ea4  mov      r4, r3
00ab4ea8  mov      r0, sb
00ab4eac  mov      r6, r2
00ab4eb0  mov      r2, r3
00ab4eb4  mov      r1, r6
00ab4eb8  bl       #0xab3d28 ->0xab3d28
00ab4ebc  ldr      r3, [r8]
00ab4ec0  mov      r0, sb
00ab4ec4  mov      r1, r6
00ab4ec8  mov      r2, r4
00ab4ecc  bl       #0xab3fcc ->0xab3fcc
00ab4ed0  cmp      r5, #8
00ab4ed4  rsb      r3, r5, #0xf
00ab4ed8  ble      #0xab5248
00ab4edc  mov      r1, #0
00ab4ee0  ldr      r2, [pc, #0x108] [pc->0xab4ff0]=0xffffffa8
00ab4ee4  str      r1, [sp, #0x24]
00ab4ee8  lsl      r3, r3, #4
00ab4eec  str      r1, [sp, #0x20]
00ab4ef0  sub      r0, r5, #8
00ab4ef4  ldr      r1, [sp, #0x28]
00ab4ef8  str      r0, [sp, #0x2c]
00ab4efc  sub      r0, r3, #0x10
00ab4f00  str      r0, [sp, #0x30]
00ab4f04  ldr      r2, [r1, r2]
00ab4f08  add      r7, r3, r2
00ab4f0c  ldr      r3, [sp, #0x20]
00ab4f10  mov      r2, #1
00ab4f14  lsl      fp, r2, r3
00ab4f18  cmp      fp, #0
00ab4f1c  ble      #0xab5220
00ab4f20  ldr      r2, [sp, #0x64]
00ab4f24  ldr      sb, [sp, #0x28]
00ab4f28  ldr      ip, [pc, #0xc4] [pc->0xab4ff4]=0xffffffc8
00ab4f2c  asr      r3, r2, r3
00ab4f30  ldr      r2, [pc, #0xc0] [pc->0xab4ff8]=0xffffffc4
00ab4f34  add      r0, r3, #0xf
00ab4f38  ldr      r5, [pc, #0xbc] [pc->0xab4ffc]=0xffffffc0
00ab4f3c  add      r1, r3, r3, lsr #31
00ab4f40  cmp      r3, #0
00ab4f44  ldr      lr, [pc, #0xb4] [pc->0xab5000]=0xffffffbc
00ab4f48  sub      r6, r3, #0xc0000010
00ab4f4c  asr      r1, r1, #1
00ab4f50  ldr      sl, [sb, r2]
00ab4f54  sub      r4, r1, #0xc0000010
00ab4f58  mov      r1, sb
00ab4f5c  ldr      r2, [r1, ip]
00ab4f60  movlt    r3, r0
00ab4f64  ldr      r1, [sp, #0x30]
00ab4f68  bic      r3, r3, #0xf
00ab4f6c  ldr      r0, [sp, #0x24]
00ab4f70  mov      ip, #0
00ab4f74  ldr      lr, [sb, lr]
00ab4f78  lsl      r4, r4, #2
00ab4f7c  ldr      sb, [sb, r5]
00ab4f80  add      r5, r1, r0
00ab4f84  str      r3, [sp, #0x1c]
00ab4f88  add      sl, r5, sl
00ab4f8c  add      r3, r5, lr
00ab4f90  add      sb, r5, sb
00ab4f94  add      r5, r5, r2
00ab4f98  lsl      r6, r6, #2
00ab4f9c  mov      lr, ip
00ab4fa0  add      r1, r6, #0x40
00ab4fa4  str      r3, [sp, #0x18]
00ab4fa8  str      r1, [sp, #0x14]
00ab4fac  ldr      r1, [r8]
00ab4fb0  ldr      r0, [sp, #0x1c]
00ab4fb4  add      r1, r1, ip
00ab4fb8  vld1.64  {d28, d29}, [r7:0x40]
00ab4fbc  add      r0, r1, r0
00ab4fc0  str      r0, [sp]
00ab4fc4  ldr      r0, [sp, #0x18]
00ab4fc8  add      r2, r1, r6
00ab4fcc  add      r3, r1, r4
00ab4fd0  vld1.32  {d2[], d3[]}, [sl]
00ab4fd4  vld1.32  {d4[], d5[]}, [r0]
00ab4fd8  ldr      r0, [sp]
00ab4fdc  vld1.32  {d26[], d27[]}, [sb]
00ab4fe0  vld1.32  {d30[], d31[]}, [r5]
00ab4fe4  b        #0xab5014 ->0xab5014
00ab4fe8  subseq   fp, r8, r4, asr #8
00ab4fec  .word    0xffffffdc
00ab4ff0  .word    0xffffffa8
00ab4ff4  .word    0xffffffc8
00ab4ff8  .word    0xffffffc4
00ab4ffc  .word    0xffffffc0
00ab5000  .word    0xffffffbc
00ab5004  svclo    #0x6c835e
00ab5008  mcrlo    p15, #6, lr, c3, c5, #0
00ab500c  vorr     q13, q10, q10
00ab5010  vorr     q2, q12, q12
00ab5014  vldr     d6, [r2, #0x10]
00ab5018  vldr     d7, [r2, #0x18]
00ab501c  vmul.f32 q10, q14, q13
00ab5020  sub      r3, r3, #0x40
00ab5024  sub      r2, r2, #0x40
00ab5028  vldr     d18, [r2, #0x70]
00ab502c  vldr     d19, [r2, #0x78]
00ab5030  vmul.f32 q12, q14, q2
00ab5034  vldr     d16, [r2, #0x60]
00ab5038  vldr     d17, [r2, #0x68]
00ab503c  vadd.f32 q0, q8, q9
00ab5040  vldr     d22, [r2, #0x40]
00ab5044  vldr     d23, [r2, #0x48]
00ab5048  vadd.f32 q4, q11, q3
00ab504c  vsub.f32 q11, q11, q3
00ab5050  vstr     d0, [r2, #0x60]
00ab5054  vstr     d1, [r2, #0x68]
00ab5058  vsub.f32 q3, q9, q8
00ab505c  vstr     d8, [r2, #0x40]
00ab5060  vstr     d9, [r2, #0x48]
00ab5064  vsub.f32 q12, q12, q1
00ab5068  vldr     d16, [r3, #0x40]
00ab506c  vldr     d17, [r3, #0x48]
00ab5070  vsub.f32 q10, q10, q15
00ab5074  vldr     d18, [r3, #0x50]
00ab5078  vldr     d19, [r3, #0x58]
00ab507c  vorr     q1, q2, q2
00ab5080  vadd.f32 q0, q9, q8
00ab5084  vsub.f32 q9, q9, q8
00ab5088  vmul.f32 q15, q3, q10
00ab508c  vstr     d0, [r2, #0x50]
00ab5090  vstr     d1, [r2, #0x58]
00ab5094  vmul.f32 q4, q11, q10
00ab5098  vldr     d10, [r3, #0x70]
00ab509c  vldr     d11, [r3, #0x78]
00ab50a0  vmul.f32 q8, q9, q10
00ab50a4  vldr     d12, [r3, #0x60]
00ab50a8  vldr     d13, [r3, #0x68]
00ab50ac  vmul.f32 q0, q9, q12
00ab50b0  vsub.f32 q9, q5, q6
00ab50b4  vmul.f32 q3, q3, q12
00ab50b8  vmul.f32 q11, q11, q12
00ab50bc  vmul.f32 q7, q9, q10
00ab50c0  vmul.f32 q9, q9, q12
00ab50c4  vadd.f32 q5, q5, q6
00ab50c8  vadd.f32 q3, q3, q4
00ab50cc  vsub.f32 q9, q9, q8
00ab50d0  vadd.f32 q0, q0, q7
00ab50d4  vstr     d10, [r2, #0x70]
00ab50d8  vstr     d11, [r2, #0x78]
00ab50dc  vsub.f32 q8, q11, q15
00ab50e0  vstr     d6, [r3, #0x40]
00ab50e4  vstr     d7, [r3, #0x48]
00ab50e8  vstr     d18, [r3, #0x70]
00ab50ec  vstr     d19, [r3, #0x78]
00ab50f0  vorr     q15, q13, q13
00ab50f4  vstr     d0, [r3, #0x50]
00ab50f8  vstr     d1, [r3, #0x58]
00ab50fc  vstr     d16, [r3, #0x60]
00ab5100  vstr     d17, [r3, #0x68]
00ab5104  cmp      r0, r3
00ab5108  bls      #0xab500c
00ab510c  vldr     d30, [r2, #0x10]
00ab5110  vldr     d31, [r2, #0x18]
00ab5114  vmul.f32 q3, q14, q10
00ab5118  sub      r3, r3, #0x40
00ab511c  sub      r2, r2, #0x40
00ab5120  vldr     d18, [r2, #0x60]
00ab5124  vldr     d19, [r2, #0x68]
00ab5128  vmul.f32 q1, q14, q12
00ab512c  vldr     d22, [r2, #0x40]
00ab5130  vldr     d23, [r2, #0x48]
00ab5134  vadd.f32 q0, q11, q15
00ab5138  vldr     d16, [r2, #0x70]
00ab513c  vldr     d17, [r2, #0x78]
00ab5140  vsub.f32 q11, q11, q15
00ab5144  vadd.f32 q15, q9, q8
00ab5148  vstr     d0, [r2, #0x40]
00ab514c  vstr     d1, [r2, #0x48]
00ab5150  vsub.f32 q3, q3, q13
00ab5154  vsub.f32 q8, q9, q8
00ab5158  vstr     d30, [r2, #0x60]
00ab515c  vstr     d31, [r2, #0x68]
00ab5160  vorr     q13, q10, q10
00ab5164  vsub.f32 q15, q1, q2
00ab5168  vldr     d20, [r3, #0x50]
00ab516c  vldr     d21, [r3, #0x58]
00ab5170  vmul.f32 q1, q11, q3
00ab5174  vldr     d18, [r3, #0x40]
00ab5178  vldr     d19, [r3, #0x48]
00ab517c  vadd.f32 q2, q10, q9
00ab5180  vsub.f32 q9, q9, q10
00ab5184  vmul.f32 q5, q8, q15
00ab5188  vstr     d4, [r2, #0x50]
00ab518c  vstr     d5, [r2, #0x58]
00ab5190  vmul.f32 q11, q11, q15
00ab5194  vldr     d0, [r3, #0x60]
00ab5198  vldr     d1, [r3, #0x68]
00ab519c  vmul.f32 q10, q9, q3
00ab51a0  vldr     d4, [r3, #0x70]
00ab51a4  vldr     d5, [r3, #0x78]
00ab51a8  vmul.f32 q4, q9, q15
00ab51ac  vsub.f32 q9, q2, q0
00ab51b0  vmul.f32 q8, q8, q3
00ab51b4  vadd.f32 q2, q2, q0
00ab51b8  vmul.f32 q0, q9, q3
00ab51bc  vmul.f32 q9, q9, q15
00ab51c0  vsub.f32 q1, q1, q5
00ab51c4  vstr     d4, [r2, #0x70]
00ab51c8  vstr     d5, [r2, #0x78]
00ab51cc  vsub.f32 q4, q0, q4
00ab51d0  vadd.f32 q9, q10, q9
00ab51d4  vadd.f32 q11, q8, q11
00ab51d8  vstr     d2, [r3, #0x40]
00ab51dc  vstr     d3, [r3, #0x48]
00ab51e0  vstr     d8, [r3, #0x50]
00ab51e4  vstr     d9, [r3, #0x58]
00ab51e8  vorr     q2, q12, q12
00ab51ec  vorr     q10, q3, q3
00ab51f0  vstr     d18, [r3, #0x70]
00ab51f4  vstr     d19, [r3, #0x78]
00ab51f8  vorr     q12, q15, q15
00ab51fc  vstr     d22, [r3, #0x60]
00ab5200  vstr     d23, [r3, #0x68]
00ab5204  cmp      r1, r3
00ab5208  bls      #0xab510c
00ab520c  add      lr, lr, #1
00ab5210  ldr      r3, [sp, #0x14]
00ab5214  cmp      lr, fp
00ab5218  add      ip, ip, r3
00ab521c  bne      #0xab4fac
00ab5220  ldr      r3, [sp, #0x20]
00ab5224  add      r7, r7, #0x10
00ab5228  ldr      r2, [sp, #0x2c]
00ab522c  add      r3, r3, #1
00ab5230  str      r3, [sp, #0x20]
00ab5234  cmp      r3, r2
00ab5238  ldr      r3, [sp, #0x24]
00ab523c  add      r3, r3, #0x10
00ab5240  str      r3, [sp, #0x24]
00ab5244  bne      #0xab4f0c
00ab5248  ldr      r3, [sp, #0x64]
00ab524c  cmp      r3, #0
00ab5250  strgt    r8, [sp, #0x84]
00ab5254  movgt    r3, #0
00ab5258  strgt    r3, [sp, #0x50]
00ab525c  ble      #0xab5a28
00ab5260  ldr      r3, [sp, #0x84]
00ab5264  ldr      r2, [sp, #0x50]
00ab5268  vldr     s15, [pc, #-0x268] [pc->0xab5008]=0x3ec3ef15
00ab526c  ldr      r3, [r3]
00ab5270  add      r3, r3, r2, lsl #2
00ab5274  add      r2, r2, #0x80
00ab5278  add      r1, r3, #0x110
00ab527c  str      r2, [sp, #0x50]
00ab5280  add      r2, r3, #0x100
00ab5284  vldr     s7, [pc, #-0x288] [pc->0xab5004]=0x3f6c835e
00ab5288  mov      lr, r1
00ab528c  add      r1, r3, #0x120
00ab5290  mov      r7, r2
00ab5294  add      sl, r3, #0x80
00ab5298  mov      r4, r1
00ab529c  add      r1, r3, #0x130
00ab52a0  vld1.32  {d16, d17}, [r2]
00ab52a4  add      r2, r3, #0x40
00ab52a8  mov      r5, r1
00ab52ac  add      r1, r3, #0x10
00ab52b0  vld1.32  {d22, d23}, [lr]
00ab52b4  vsub.f32 q14, q8, q11
00ab52b8  mov      r6, r1
00ab52bc  add      r1, r3, #0x20
00ab52c0  vld1.32  {d0, d1}, [r5]
00ab52c4  vadd.f32 q11, q8, q11
00ab52c8  str      r2, [sp, #0x54]
00ab52cc  mov      ip, r1
00ab52d0  add      r1, r3, #0x30
00ab52d4  vld1.32  {d18, d19}, [r4]
00ab52d8  add      r2, r3, #0x50
00ab52dc  vsub.f32 q12, q9, q0
00ab52e0  mov      r0, r1
00ab52e4  vdup.32  q8, d7[1]
00ab52e8  add      r1, r3, #0x140
00ab52ec  vmul.f32 q3, q14, q8
00ab52f0  vdup.32  q8, d3[1]
00ab52f4  vld1.32  {d4, d5}, [ip]
00ab52f8  vmul.f32 q14, q14, q8
00ab52fc  str      r2, [sp]
00ab5300  vld1.32  {d10, d11}, [r3]
00ab5304  add      r2, r3, #0x60
00ab5308  vadd.f32 q9, q9, q0
00ab530c  str      r2, [sp, #0x14]
00ab5310  vld1.32  {d20, d21}, [r6]
00ab5314  add      r2, r3, #0x70
00ab5318  vsub.f32 q13, q10, q5
00ab531c  str      r2, [sp, #0x18]
00ab5320  vld1.32  {d16, d17}, [r0]
00ab5324  add      r2, r3, #0x1b0
00ab5328  vsub.f32 q6, q8, q2
00ab532c  str      r1, [sp, #0x58]
00ab5330  vst1.32  {d22, d23}, [r7]
00ab5334  add      r1, r3, #0x150
00ab5338  vadd.f32 q10, q10, q5
00ab533c  str      r1, [sp, #0x5c]
00ab5340  vdup.32  q11, d3[1]
00ab5344  str      r6, [sp, #0x2c]
00ab5348  vldr     s7, [pc, #-0x348] [pc->0xab5008]=0x3ec3ef15
00ab534c  add      r6, r3, #0xf0
00ab5350  str      sl, [sp, #0x1c]
00ab5354  add      sl, r3, #0x90
00ab5358  vmul.f32 q4, q12, q11
00ab535c  str      sl, [sp, #0x38]
00ab5360  vldr     s28, [pc, #-0x364] [pc->0xab5004]=0x3f6c835e
00ab5364  add      r1, r3, #0x160
00ab5368  str      ip, [sp, #0x30]
00ab536c  add      ip, r3, #0x1a0
00ab5370  vadd.f32 q8, q8, q2
00ab5374  str      r0, [sp, #0x34]
00ab5378  vdup.32  q11, d3[1]
00ab537c  str      r2, [sp, #0x78]
00ab5380  vldr     s7, [pc, #-0x384] [pc->0xab5004]=0x3f6c835e
00ab5384  add      r2, r3, #0xc0
00ab5388  str      r6, [sp, #0x28]
00ab538c  add      r6, r3, #0x1c0
00ab5390  vmul.f32 q12, q12, q11
00ab5394  str      r2, [sp, #0x7c]
00ab5398  str      r6, [sp, #0x20]
00ab539c  add      r2, r3, #0xd0
00ab53a0  vdup.32  q11, d3[1]
00ab53a4  add      r6, r3, #0x1d0
00ab53a8  vldr     s7, [pc, #-0x3a8] [pc->0xab5008]=0x3ec3ef15
00ab53ac  mov      r8, r1
00ab53b0  str      r2, [sp, #0x80]
00ab53b4  add      r1, r3, #0x170
00ab53b8  vmul.f32 q15, q13, q11
00ab53bc  str      r6, [sp, #0x24]
00ab53c0  str      r7, [sp, #0x68]
00ab53c4  add      r6, r3, #0x1e0
00ab53c8  vdup.32  q11, d3[1]
00ab53cc  vsub.f32 q3, q3, q4
00ab53d0  str      r6, [sp, #0x48]
00ab53d4  add      r6, r3, #0x1f0
00ab53d8  vmul.f32 q1, q6, q11
00ab53dc  str      r6, [sp, #0x4c]
00ab53e0  ldr      r7, [sp, #0x64]
00ab53e4  mov      sb, r1
00ab53e8  vst1.32  {d20, d21}, [lr]
00ab53ec  vmul.f32 q13, q13, q11
00ab53f0  ldr      r6, [sp, #0x50]
00ab53f4  add      r1, r3, #0xa0
00ab53f8  vst1.32  {d18, d19}, [r4]
00ab53fc  vadd.f32 q10, q12, q14
00ab5400  cmp      r7, r6
00ab5404  ldr      r7, [sp, #0x2c]
00ab5408  vadd.f32 q9, q15, q1
00ab540c  vdup.32  q11, d14[0]
00ab5410  vst1.32  {d16, d17}, [r5]
00ab5414  mov      sl, r1
00ab5418  vmul.f32 q11, q6, q11
00ab541c  str      lr, [sp, #0x6c]
00ab5420  vst1.32  {d6, d7}, [r3]
00ab5424  add      r1, r3, #0xb0
00ab5428  ldr      lr, [sp, #0x54]
00ab542c  add      r0, r3, #0x190
00ab5430  ldr      r6, [sp, #0x58]
00ab5434  mov      fp, r1
00ab5438  vst1.32  {d18, d19}, [r7]
00ab543c  add      r1, r3, #0x180
00ab5440  vsub.f32 q8, q11, q13
00ab5444  ldr      r7, [sp, #0x30]
00ab5448  str      r4, [sp, #0x70]
00ab544c  add      r2, r3, #0xe0
00ab5450  ldr      r4, [sp]
00ab5454  vst1.32  {d20, d21}, [r7]
00ab5458  ldr      r7, [sp, #0x34]
00ab545c  str      r5, [sp, #0x74]
00ab5460  ldr      r5, [sp, #0x14]
00ab5464  vst1.32  {d16, d17}, [r7]
00ab5468  ldr      r7, [sp, #0x5c]
00ab546c  vld1.32  {d4, d5}, [lr]
00ab5470  ldr      lr, [sp, #0x18]
00ab5474  vld1.32  {d16, d17}, [r6]
00ab5478  str      r7, [sp, #0x60]
00ab547c  vld1.32  {d24, d25}, [r7]
00ab5480  vsub.f32 q9, q8, q12
00ab5484  vld1.32  {d20, d21}, [r8]
00ab5488  vadd.f32 q12, q8, q12
00ab548c  vld1.32  {d28, d29}, [sb]
00ab5490  vsub.f32 q1, q10, q14
00ab5494  vld1.32  {d30, d31}, [r5]
00ab5498  vadd.f32 q14, q10, q14
00ab549c  vld1.32  {d22, d23}, [r4]
00ab54a0  vsub.f32 q3, q11, q2
00ab54a4  vld1.32  {d20, d21}, [lr]
00ab54a8  vsub.f32 q8, q10, q15
00ab54ac  vst1.32  {d24, d25}, [r6]
00ab54b0  vadd.f32 q11, q11, q2
00ab54b4  vldr     s3, [pc, #0x3f4] [pc->0xab58b0]=0x3ec3ef15
00ab54b8  vadd.f32 q10, q10, q15
00ab54bc  vadd.f32 q12, q8, q3
00ab54c0  vst1.32  {d22, d23}, [r7]
00ab54c4  vsub.f32 q13, q9, q1
00ab54c8  vst1.32  {d28, d29}, [r8]
00ab54cc  vsub.f32 q8, q8, q3
00ab54d0  vldr     s15, [pc, #0x3dc] [pc->0xab58b4]=0x3f3504f3
00ab54d4  vadd.f32 q9, q9, q1
00ab54d8  vst1.32  {d20, d21}, [sb]
00ab54dc  ldr      r7, [sp, #0x54]
00ab54e0  vdup.32  q11, d7[1]
00ab54e4  str      sl, [sp, #0x54]
00ab54e8  vdup.32  q10, d7[1]
00ab54ec  vmul.f32 q13, q13, q11
00ab54f0  vldr     s15, [pc, #0x3b8] [pc->0xab58b0]=0x3ec3ef15
00ab54f4  vmul.f32 q9, q9, q11
00ab54f8  vmul.f32 q8, q8, q10
00ab54fc  vst1.32  {d26, d27}, [r7]
00ab5500  vmul.f32 q11, q12, q11
00ab5504  vdup.32  q12, d7[1]
00ab5508  vst1.32  {d22, d23}, [r4]
00ab550c  ldr      r4, [sp, #0x38]
00ab5510  vst1.32  {d18, d19}, [r5]
00ab5514  ldr      r5, [sp, #0x1c]
00ab5518  vst1.32  {d16, d17}, [lr]
00ab551c  mov      lr, r1
00ab5520  str      lr, [sp, #0x5c]
00ab5524  vld1.32  {d18, d19}, [r0]
00ab5528  vld1.32  {d16, d17}, [r1]
00ab552c  vsub.f32 q15, q8, q9
00ab5530  vld1.32  {d12, d13}, [r5]
00ab5534  vadd.f32 q8, q8, q9
00ab5538  ldr      r5, [sp, #0x78]
00ab553c  vld1.32  {d20, d21}, [r4]
00ab5540  vsub.f32 q14, q10, q6
00ab5544  vdup.32  q9, d14[0]
00ab5548  vld1.32  {d8, d9}, [r5]
00ab554c  vmul.f32 q2, q15, q9
00ab5550  vld1.32  {d18, d19}, [ip]
00ab5554  vmul.f32 q15, q15, q12
00ab5558  vsub.f32 q12, q9, q4
00ab555c  vld1.32  {d2, d3}, [sl]
00ab5560  vld1.32  {d26, d27}, [fp]
00ab5564  vadd.f32 q10, q10, q6
00ab5568  vst1.32  {d16, d17}, [lr]
00ab556c  vsub.f32 q11, q13, q1
00ab5570  ldr      lr, [sp, #0x1c]
00ab5574  vdup.32  q8, d7[1]
00ab5578  vadd.f32 q9, q9, q4
00ab557c  vmul.f32 q3, q14, q8
00ab5580  vdup.32  q8, d14[0]
00ab5584  vst1.32  {d20, d21}, [r0]
00ab5588  vmul.f32 q14, q14, q8
00ab558c  vdup.32  q8, d1[1]
00ab5590  vmul.f32 q5, q12, q8
00ab5594  vdup.32  q8, d14[0]
00ab5598  vldr     s28, [pc, #0x310] [pc->0xab58b0]=0x3ec3ef15
00ab559c  vmul.f32 q0, q11, q8
00ab55a0  vst1.32  {d18, d19}, [ip]
00ab55a4  vmul.f32 q12, q12, q8
00ab55a8  vdup.32  q8, d14[0]
00ab55ac  vmul.f32 q11, q11, q8
00ab55b0  vadd.f32 q8, q13, q1
00ab55b4  vadd.f32 q10, q12, q15
00ab55b8  vsub.f32 q13, q2, q5
00ab55bc  vadd.f32 q9, q3, q0
00ab55c0  vst1.32  {d16, d17}, [r5]
00ab55c4  vsub.f32 q8, q11, q14
00ab55c8  vst1.32  {d26, d27}, [lr]
00ab55cc  ldr      lr, [sp, #0x48]
00ab55d0  vst1.32  {d18, d19}, [r4]
00ab55d4  ldr      r4, [sp, #0x24]
00ab55d8  vst1.32  {d20, d21}, [sl]
00ab55dc  ldr      sl, [sp, #0x20]
00ab55e0  vst1.32  {d16, d17}, [fp]
00ab55e4  vld1.32  {d16, d17}, [sl]
00ab55e8  vld1.32  {d22, d23}, [r4]
00ab55ec  vadd.f32 q3, q8, q11
00ab55f0  vld1.32  {d18, d19}, [lr]
00ab55f4  ldr      r1, [sp, #0x4c]
00ab55f8  vsub.f32 q11, q8, q11
00ab55fc  ldr      lr, [sp, #0x7c]
00ab5600  ldr      r4, [sp, #0x80]
00ab5604  vld1.32  {d20, d21}, [r1]
00ab5608  vadd.f32 q14, q9, q10
00ab560c  ldr      sl, [sp, #0x28]
00ab5610  vld1.32  {d16, d17}, [lr]
00ab5614  vsub.f32 q9, q9, q10
00ab5618  str      r2, [sp, #0x58]
00ab561c  vld1.32  {d20, d21}, [r4]
00ab5620  vadd.f32 q15, q10, q8
00ab5624  vld1.32  {d24, d25}, [r2]
00ab5628  ldr      r2, [sp, #0x20]
00ab562c  vsub.f32 q8, q8, q10
00ab5630  vld1.32  {d20, d21}, [sl]
00ab5634  vadd.f32 q13, q10, q12
00ab5638  vst1.32  {d6, d7}, [r2]
00ab563c  ldr      r2, [sp, #0x24]
00ab5640  vsub.f32 q10, q10, q12
00ab5644  vst1.32  {d30, d31}, [r2]
00ab5648  ldr      r2, [sp, #0x48]
00ab564c  vst1.32  {d28, d29}, [r2]
00ab5650  ldr      r2, [sp, #0x58]
00ab5654  vst1.32  {d26, d27}, [r1]
00ab5658  ldr      r1, [sp, #0x38]
00ab565c  vst1.32  {d22, d23}, [lr]
00ab5660  vst1.32  {d20, d21}, [r4]
00ab5664  vst1.32  {d18, d19}, [r2]
00ab5668  vst1.32  {d16, d17}, [sl]
00ab566c  ldr      sl, [sp, #0x2c]
00ab5670  vld1.32  {d16, d17}, [r3]
00ab5674  vld1.32  {d24, d25}, [sl]
00ab5678  ldr      sl, [sp, #0x1c]
00ab567c  vsub.f32 q9, q8, q12
00ab5680  vld1.32  {d0, d1}, [r1]
00ab5684  vadd.f32 q12, q8, q12
00ab5688  vld1.32  {d20, d21}, [sl]
00ab568c  vsub.f32 q8, q10, q0
00ab5690  ldr      sl, [sp, #0x54]
00ab5694  vadd.f32 q10, q10, q0
00ab5698  vld1.32  {d0, d1}, [fp]
00ab569c  vld1.32  {d28, d29}, [sl]
00ab56a0  vstr     d20, [sp, #0x38]
00ab56a4  vstr     d21, [sp, #0x40]
00ab56a8  vsub.f32 q10, q14, q0
00ab56ac  ldr      sl, [sp, #0x30]
00ab56b0  vadd.f32 q0, q14, q0
00ab56b4  vld1.32  {d6, d7}, [lr]
00ab56b8  vld1.32  {d22, d23}, [sl]
00ab56bc  ldr      sl, [sp, #0x34]
00ab56c0  vadd.f32 q14, q8, q10
00ab56c4  vld1.32  {d8, d9}, [r4]
00ab56c8  vsub.f32 q8, q8, q10
00ab56cc  vld1.32  {d14, d15}, [sl]
00ab56d0  vsub.f32 q15, q11, q7
00ab56d4  ldr      sl, [sp, #0x14]
00ab56d8  vld1.32  {d20, d21}, [r7]
00ab56dc  vadd.f32 q11, q11, q7
00ab56e0  vld1.32  {d4, d5}, [sl]
00ab56e4  vneg.f32 q5, q8
00ab56e8  ldr      sl, [sp, #0x18]
00ab56ec  vld1.32  {d14, d15}, [r2]
00ab56f0  vsub.f32 q6, q9, q15
00ab56f4  vld1.32  {d2, d3}, [sl]
00ab56f8  vadd.f32 q9, q9, q15
00ab56fc  ldr      sl, [sp]
00ab5700  vneg.f32 q15, q14
00ab5704  vld1.32  {d26, d27}, [sl]
00ab5708  vsub.f32 q14, q14, q6
00ab570c  ldr      sl, [sp, #0x28]
00ab5710  vsub.f32 q8, q9, q8
00ab5714  vsub.f32 q15, q15, q6
00ab5718  vsub.f32 q6, q2, q1
00ab571c  vadd.f32 q1, q2, q1
00ab5720  vsub.f32 q2, q3, q4
00ab5724  vadd.f32 q4, q3, q4
00ab5728  vsub.f32 q3, q5, q9
00ab572c  vld1.32  {d18, d19}, [sl]
00ab5730  ldr      sl, [sp, #0x1c]
00ab5734  vldr     s20, [pc, #0x178] [pc->0xab58b4]=0x3f3504f3
00ab5738  vdup.32  q5, d10[0]
00ab573c  vmul.f32 q14, q14, q5
00ab5740  vldr     s20, [pc, #0x16c] [pc->0xab58b4]=0x3f3504f3
00ab5744  vdup.32  q5, d10[0]
00ab5748  vmul.f32 q15, q15, q5
00ab574c  vadd.f32 q5, q2, q6
00ab5750  vsub.f32 q2, q2, q6
00ab5754  vldr     s24, [pc, #0x158] [pc->0xab58b4]=0x3f3504f3
00ab5758  vdup.32  q6, d12[0]
00ab575c  vmul.f32 q3, q3, q6
00ab5760  vldr     s24, [pc, #0x14c] [pc->0xab58b4]=0x3f3504f3
00ab5764  vdup.32  q6, d12[0]
00ab5768  vmul.f32 q8, q8, q6
00ab576c  vsub.f32 q6, q10, q13
00ab5770  vadd.f32 q10, q10, q13
00ab5774  vsub.f32 q13, q7, q9
00ab5778  vadd.f32 q9, q7, q9
00ab577c  vadd.f32 q7, q5, q14
00ab5780  vsub.f32 q5, q5, q14
00ab5784  vsub.f32 q14, q13, q6
00ab5788  vst1.32  {d14, d15}, [r3]
00ab578c  vadd.f32 q13, q13, q6
00ab5790  ldr      r3, [sp, #0x2c]
00ab5794  vldr     d14, [sp, #0x38]
00ab5798  vldr     d15, [sp, #0x40]
00ab579c  vsub.f32 q6, q4, q10
00ab57a0  vadd.f32 q10, q4, q10
00ab57a4  vsub.f32 q4, q0, q11
00ab57a8  vadd.f32 q11, q0, q11
00ab57ac  vsub.f32 q0, q7, q12
00ab57b0  vadd.f32 q12, q7, q12
00ab57b4  vsub.f32 q7, q9, q1
00ab57b8  vadd.f32 q9, q9, q1
00ab57bc  vadd.f32 q1, q14, q3
00ab57c0  vsub.f32 q3, q14, q3
00ab57c4  vadd.f32 q14, q2, q8
00ab57c8  vst1.32  {d2, d3}, [r3]
00ab57cc  vsub.f32 q8, q2, q8
00ab57d0  ldr      r3, [sp, #0x30]
00ab57d4  vadd.f32 q2, q13, q15
00ab57d8  vst1.32  {d10, d11}, [r3]
00ab57dc  vsub.f32 q13, q13, q15
00ab57e0  ldr      r3, [sp, #0x34]
00ab57e4  vst1.32  {d6, d7}, [r3]
00ab57e8  vadd.f32 q3, q6, q4
00ab57ec  ldr      r3, [sp]
00ab57f0  vst1.32  {d28, d29}, [r7]
00ab57f4  vsub.f32 q14, q7, q0
00ab57f8  vst1.32  {d4, d5}, [r3]
00ab57fc  vsub.f32 q4, q6, q4
00ab5800  ldr      r3, [sp, #0x14]
00ab5804  vadd.f32 q0, q7, q0
00ab5808  vst1.32  {d16, d17}, [r3]
00ab580c  vsub.f32 q8, q9, q11
00ab5810  ldr      r3, [sp, #0x18]
00ab5814  vadd.f32 q9, q9, q11
00ab5818  vst1.32  {d26, d27}, [r3]
00ab581c  vsub.f32 q13, q10, q12
00ab5820  vst1.32  {d6, d7}, [sl]
00ab5824  vadd.f32 q10, q10, q12
00ab5828  vst1.32  {d28, d29}, [r1]
00ab582c  ldr      sl, [sp, #0x54]
00ab5830  ldr      r3, [sp, #0x68]
00ab5834  ldr      r1, [sp, #0x5c]
00ab5838  vst1.32  {d8, d9}, [sl]
00ab583c  ldr      sl, [sp, #0x28]
00ab5840  vst1.32  {d0, d1}, [fp]
00ab5844  ldr      fp, [sp, #0x20]
00ab5848  vst1.32  {d26, d27}, [lr]
00ab584c  ldr      lr, [sp, #0x6c]
00ab5850  vst1.32  {d16, d17}, [r4]
00ab5854  ldr      r4, [sp, #0x70]
00ab5858  vst1.32  {d20, d21}, [r2]
00ab585c  ldr      r7, [sp, #0x60]
00ab5860  vst1.32  {d18, d19}, [sl]
00ab5864  mov      sl, r5
00ab5868  vld1.32  {d16, d17}, [r3]
00ab586c  vld1.32  {d20, d21}, [r1]
00ab5870  vld1.32  {d8, d9}, [r0]
00ab5874  vld1.32  {d24, d25}, [lr]
00ab5878  vsub.f32 q9, q8, q12
00ab587c  vld1.32  {d14, d15}, [r5]
00ab5880  vadd.f32 q12, q8, q12
00ab5884  ldr      r5, [sp, #0x74]
00ab5888  vld1.32  {d22, d23}, [ip]
00ab588c  vsub.f32 q8, q10, q4
00ab5890  vld1.32  {d28, d29}, [r4]
00ab5894  vadd.f32 q10, q10, q4
00ab5898  vld1.32  {d8, d9}, [r5]
00ab589c  vsub.f32 q15, q14, q4
00ab58a0  vld1.32  {d6, d7}, [fp]
00ab58a4  vadd.f32 q4, q14, q4
00ab58a8  ldr      fp, [sp, #0x24]
00ab58ac  b        #0xab58b8 ->0xab58b8
00ab58b0  mcrlo    p15, #6, lr, c3, c5, #0
00ab58b4  svclo    #0x3504f3
00ab58b8  vld1.32  {d4, d5}, [r8]
00ab58bc  vst1.64  {d20, d21}, [sp:0x40]
00ab58c0  vsub.f32 q10, q11, q7
00ab58c4  vsub.f32 q6, q9, q15
00ab58c8  vld1.32  {d0, d1}, [fp]
00ab58cc  ldr      fp, [sp, #0x48]
00ab58d0  vadd.f32 q9, q9, q15
00ab58d4  vld1.32  {d2, d3}, [sb]
00ab58d8  vadd.f32 q14, q8, q10
00ab58dc  vld1.32  {d26, d27}, [r7]
00ab58e0  vsub.f32 q8, q8, q10
00ab58e4  vld1.32  {d20, d21}, [r6]
00ab58e8  vadd.f32 q11, q11, q7
00ab58ec  vld1.32  {d14, d15}, [fp]
00ab58f0  ldr      r2, [sp, #0x4c]
00ab58f4  vneg.f32 q15, q14
00ab58f8  vneg.f32 q5, q8
00ab58fc  vsub.f32 q14, q14, q6
00ab5900  vsub.f32 q15, q15, q6
00ab5904  vsub.f32 q6, q2, q1
00ab5908  vadd.f32 q1, q2, q1
00ab590c  vsub.f32 q2, q3, q0
00ab5910  vadd.f32 q0, q3, q0
00ab5914  vsub.f32 q3, q5, q9
00ab5918  vldr     s20, [pc, #-0x6c] [pc->0xab58b4]=0x3f3504f3
00ab591c  vsub.f32 q8, q9, q8
00ab5920  vld1.32  {d18, d19}, [r2]
00ab5924  vdup.32  q5, d10[0]
00ab5928  vmul.f32 q14, q14, q5
00ab592c  vldr     s20, [pc, #-0x80] [pc->0xab58b4]=0x3f3504f3
00ab5930  vdup.32  q5, d10[0]
00ab5934  vmul.f32 q15, q15, q5
00ab5938  vadd.f32 q5, q2, q6
00ab593c  vsub.f32 q2, q2, q6
00ab5940  vldr     s24, [pc, #-0x94] [pc->0xab58b4]=0x3f3504f3
00ab5944  vdup.32  q6, d12[0]
00ab5948  vmul.f32 q3, q3, q6
00ab594c  vldr     s24, [pc, #-0xa0] [pc->0xab58b4]=0x3f3504f3
00ab5950  vdup.32  q6, d12[0]
00ab5954  vmul.f32 q8, q8, q6
00ab5958  vsub.f32 q6, q10, q13
00ab595c  vadd.f32 q10, q10, q13
00ab5960  vsub.f32 q13, q7, q9
00ab5964  vadd.f32 q9, q7, q9
00ab5968  vadd.f32 q7, q5, q14
00ab596c  vsub.f32 q5, q5, q14
00ab5970  vsub.f32 q14, q13, q6
00ab5974  vadd.f32 q13, q13, q6
00ab5978  vst1.32  {d14, d15}, [r3]
00ab597c  ldr      r3, [sp, #0x24]
00ab5980  vsub.f32 q6, q11, q4
00ab5984  vadd.f32 q11, q11, q4
00ab5988  vld1.64  {d8, d9}, [sp:0x40]
00ab598c  vsub.f32 q7, q0, q10
00ab5990  vadd.f32 q10, q0, q10
00ab5994  vsub.f32 q0, q4, q12
00ab5998  vadd.f32 q12, q4, q12
00ab599c  vsub.f32 q4, q9, q1
00ab59a0  vadd.f32 q9, q9, q1
00ab59a4  vadd.f32 q1, q14, q3
00ab59a8  vsub.f32 q3, q14, q3
00ab59ac  vadd.f32 q14, q2, q8
00ab59b0  vsub.f32 q8, q2, q8
00ab59b4  vst1.32  {d2, d3}, [lr]
00ab59b8  vadd.f32 q2, q13, q15
00ab59bc  vst1.32  {d10, d11}, [r4]
00ab59c0  vsub.f32 q13, q13, q15
00ab59c4  vst1.32  {d6, d7}, [r5]
00ab59c8  vadd.f32 q3, q7, q6
00ab59cc  vst1.32  {d28, d29}, [r6]
00ab59d0  vsub.f32 q6, q7, q6
00ab59d4  vst1.32  {d4, d5}, [r7]
00ab59d8  vsub.f32 q14, q4, q0
00ab59dc  vst1.32  {d16, d17}, [r8]
00ab59e0  vadd.f32 q0, q4, q0
00ab59e4  vst1.32  {d26, d27}, [sb]
00ab59e8  vsub.f32 q8, q9, q11
00ab59ec  vst1.32  {d6, d7}, [r1]
00ab59f0  ldr      r1, [sp, #0x20]
00ab59f4  vsub.f32 q13, q10, q12
00ab59f8  vst1.32  {d28, d29}, [r0]
00ab59fc  vadd.f32 q10, q10, q12
00ab5a00  vst1.32  {d12, d13}, [ip]
00ab5a04  vadd.f32 q9, q9, q11
00ab5a08  vst1.32  {d0, d1}, [sl]
00ab5a0c  vst1.32  {d26, d27}, [r1]
00ab5a10  vst1.32  {d16, d17}, [r3]
00ab5a14  vst1.32  {d20, d21}, [fp]
00ab5a18  vst1.32  {d18, d19}, [r2]
00ab5a1c  bgt      #0xab5260
00ab5a20  ldr      r8, [sp, #0x84]
00ab5a24  ldr      r3, [sp, #0x64]
00ab5a28  lsl      r1, r3, #1
00ab5a2c  ldr      r0, [r8]
00ab5a30  ldr      r2, [sp, #0x8c]
00ab5a34  ldr      r3, [sp, #0x88]
00ab5a38  add      sp, sp, #0x94
00ab5a3c  vpop     {d8, d9, d10, d11, d12, d13, d14, d15}
00ab5a40  pop      {r4, r5, r6, r7, r8, sb, sl, fp, lr}
00ab5a44  b        #0xab39d8 ->0xab39d8
00ab5a48  add      sp, sp, #0x94
00ab5a4c  vpop     {d8, d9, d10, d11, d12, d13, d14, d15}
00ab5a50  pop      {r4, r5, r6, r7, r8, sb, sl, fp, pc}

```

## Appendix D: bit-reversed rotation/tail, 0x00AB39D8..0x00AB3CF0

```text
00ab39d8  push     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
00ab39dc  asr      r1, r1, #1
00ab39e0  vpush    {d8, d9, d10, d11, d12, d13, d14, d15}
00ab39e4  add      r7, r2, #1
00ab39e8  ldr      ip, [pc, #0x31c] [pc->0xab3d0c]=0x58c88c
00ab39ec  add      sb, r1, #7
00ab39f0  ldr      r4, [pc, #0x318] [pc->0xab3d10]=0xffffffa8
00ab39f4  cmp      r1, #0
00ab39f8  add      ip, pc, ip
00ab39fc  ldr      r8, [pc, #0x310] [pc->0xab3d14]=0xffffffac
00ab3a00  ldr      lr, [pc, #0x310] [pc->0xab3d18]=0xffffffb0
00ab3a04  lsl      r7, r7, #4
00ab3a08  ldr      r5, [pc, #0x30c] [pc->0xab3d1c]=0xffffffb4
00ab3a0c  sub      sp, sp, #0x7c
00ab3a10  ldr      r4, [ip, r4]
00ab3a14  mov      fp, r3
00ab3a18  ldr      r6, [pc, #0x300] [pc->0xab3d20]=0xffffffb8
00ab3a1c  sub      r3, r7, #0x20
00ab3a20  ldr      lr, [ip, lr]
00ab3a24  movlt    r1, sb
00ab3a28  ldr      r5, [ip, r5]
00ab3a2c  asr      r1, r1, #3
00ab3a30  ldr      sb, [ip, r8]
00ab3a34  add      r8, r3, r4
00ab3a38  ldr      sl, [ip, r6]
00ab3a3c  sub      r2, r2, #1
00ab3a40  ldr      r6, [pc, #0x2dc] [pc->0xab3d24]=0x550bd4
00ab3a44  add      ip, r7, r5
00ab3a48  str      r0, [sp, #0x70]
00ab3a4c  add      r0, r3, lr
00ab3a50  str      r2, [sp, #0x4c]
00ab3a54  add      r2, r3, r5
00ab3a58  add      r5, r7, sb
00ab3a5c  vld1.32  {d16, d17}, [r8]
00ab3a60  add      r8, r3, sb
00ab3a64  add      r6, pc, r6
00ab3a68  add      r3, r3, sl
00ab3a6c  vld1.32  {d12, d13}, [r0]
00ab3a70  vmov.f32 q0, #5.000000e-01
00ab3a74  add      r0, fp, r1, lsl #5
00ab3a78  vld1.32  {d14, d15}, [r5]
00ab3a7c  vorr     q2, q7, q7
00ab3a80  add      r4, r7, r4
00ab3a84  add      lr, r7, lr
00ab3a88  vstr     d16, [sp, #0x60]
00ab3a8c  vstr     d17, [sp, #0x68]
00ab3a90  add      r7, r7, sl
00ab3a94  sub      r0, r0, #0x10
00ab3a98  str      r6, [sp, #0x74]
00ab3a9c  vld1.32  {d16, d17}, [r2]
00ab3aa0  sub      r2, r1, #1
00ab3aa4  vld1.32  {d10, d11}, [r8]
00ab3aa8  add      r8, r6, r1, lsl #1
00ab3aac  add      r1, fp, r1, lsl #4
00ab3ab0  vldr     d2, [pc, #0x240] [pc->0xab3cf8]=0x33800000
00ab3ab4  vldr     d3, [pc, #0x244] [pc->0xab3d00]=0x33800000
00ab3ab8  vstr     d16, [sp, #0x20]
00ab3abc  vstr     d17, [sp, #0x28]
00ab3ac0  vld1.32  {d16, d17}, [r3]
00ab3ac4  mov      r3, #0
00ab3ac8  vstr     d16, [sp, #0x10]
00ab3acc  vstr     d17, [sp, #0x18]
00ab3ad0  vld1.32  {d8, d9}, [lr]
00ab3ad4  vld1.32  {d16, d17}, [r4]
00ab3ad8  vstr     d16, [sp, #0x50]
00ab3adc  vstr     d17, [sp, #0x58]
00ab3ae0  vld1.32  {d16, d17}, [ip]
00ab3ae4  vst1.64  {d16, d17}, [sp:0x40]
00ab3ae8  vld1.32  {d16, d17}, [r7]
00ab3aec  vstr     d16, [sp, #0x30]
00ab3af0  vstr     d17, [sp, #0x38]
00ab3af4  ldr      r5, [sp, #0x74]
00ab3af8  lsl      lr, r3, #1
00ab3afc  ldrh     ip, [r8, #-2]!
00ab3b00  add      r4, fp, r3, lsl #4
00ab3b04  vldr     d16, [sp, #0x50]
00ab3b08  vldr     d17, [sp, #0x58]
00ab3b0c  add      r3, r3, #1
00ab3b10  ldrh     lr, [lr, r5]
00ab3b14  vmul.f32 q15, q8, q2
00ab3b18  str      r4, [sp, #0x44]
00ab3b1c  add      r5, fp, r2, lsl #4
00ab3b20  ldr      r4, [sp, #0x4c]
00ab3b24  vmul.f32 q9, q8, q4
00ab3b28  vldr     d16, [sp, #0x30]
00ab3b2c  vldr     d17, [sp, #0x38]
00ab3b30  sub      r2, r2, #1
00ab3b34  asr      ip, ip, r4
00ab3b38  asr      lr, lr, r4
00ab3b3c  ldr      r4, [sp, #0x70]
00ab3b40  vsub.f32 q15, q15, q8
00ab3b44  str      r5, [sp, #0x48]
00ab3b48  cmp      r3, r2
00ab3b4c  add      ip, r4, ip, lsl #4
00ab3b50  add      lr, r4, lr, lsl #4
00ab3b54  vld1.64  {d20, d21}, [sp:0x40]
00ab3b58  vsub.f32 q9, q9, q10
00ab3b5c  vldr     d16, [ip, #0x10]
00ab3b60  vldr     d17, [ip, #0x18]
00ab3b64  vldr     d24, [lr, #0x10]
00ab3b68  vldr     d25, [lr, #0x18]
00ab3b6c  vld1.64  {d26, d27}, [lr:0x40]
00ab3b70  vld1.64  {d22, d23}, [ip:0x40]
00ab3b74  vmov.32  sl, d24[1]
00ab3b78  vmov.32  sb, d17[1]
00ab3b7c  vmov.32  r7, d26[1]
00ab3b80  vmov.32  r6, d23[1]
00ab3b84  vmov.32  r5, d25[0]
00ab3b88  vmov.32  r4, d16[0]
00ab3b8c  vmov.32  lr, d27[0]
00ab3b90  vmov.32  ip, d22[0]
00ab3b94  vldr     d28, [sp, #0x60]
00ab3b98  vldr     d29, [sp, #0x68]
00ab3b9c  vmul.f32 q10, q14, q6
00ab3ba0  vldr     d6, [sp, #0x20]
00ab3ba4  vldr     d7, [sp, #0x28]
00ab3ba8  vmul.f32 q14, q14, q5
00ab3bac  vst1.64  {d8, d9}, [sp:0x40]
00ab3bb0  vorr     q4, q9, q9
00ab3bb4  vmov.32  d25[0], sl
00ab3bb8  vsub.f32 q10, q10, q3
00ab3bbc  vmov.32  d16[0], sb
00ab3bc0  vmov.32  d27[0], r7
00ab3bc4  vmov.32  d22[0], r6
00ab3bc8  vmov.32  d24[1], r5
00ab3bcc  ldr      r5, [sp, #0x48]
00ab3bd0  vldr     d6, [sp, #0x10]
00ab3bd4  vldr     d7, [sp, #0x18]
00ab3bd8  vsub.f32 q14, q14, q3
00ab3bdc  vmov.32  d17[1], r4
00ab3be0  ldr      r4, [sp, #0x44]
00ab3be4  vsub.f32 q3, q12, q8
00ab3be8  vmov.32  d23[1], ip
00ab3bec  vadd.f32 q8, q8, q12
00ab3bf0  vmov.32  d26[1], lr
00ab3bf4  vadd.f32 q12, q11, q13
00ab3bf8  vstr     d4, [sp, #0x30]
00ab3bfc  vstr     d5, [sp, #0x38]
00ab3c00  vmul.f32 q7, q3, q15
00ab3c04  vstr     d12, [sp, #0x20]
00ab3c08  vstr     d13, [sp, #0x28]
00ab3c0c  vmul.f32 q3, q3, q9
00ab3c10  vstr     d10, [sp, #0x10]
00ab3c14  vstr     d11, [sp, #0x18]
00ab3c18  vmul.f32 q9, q12, q9
00ab3c1c  vmul.f32 q12, q12, q15
00ab3c20  vsub.f32 q13, q11, q13
00ab3c24  vsub.f32 q9, q7, q9
00ab3c28  vadd.f32 q12, q12, q3
00ab3c2c  vldr     s15, [pc, #0xd4] [pc->0xab3d08]=0x3f3504f3
00ab3c30  vmul.f32 q7, q9, q0
00ab3c34  vmul.f32 q11, q13, q0
00ab3c38  vdup.32  q9, d7[1]
00ab3c3c  vdup.32  q3, d7[1]
00ab3c40  vmul.f32 q12, q12, q0
00ab3c44  vadd.f32 q13, q14, q10
00ab3c48  vorr     q2, q15, q15
00ab3c4c  vmul.f32 q15, q8, q0
00ab3c50  vmul.f32 q13, q13, q9
00ab3c54  vsub.f32 q8, q7, q11
00ab3c58  vsub.f32 q9, q15, q12
00ab3c5c  vadd.f32 q12, q15, q12
00ab3c60  vsub.f32 q15, q14, q10
00ab3c64  vadd.f32 q11, q11, q7
00ab3c68  vneg.f32 q8, q8
00ab3c6c  vmul.f32 q15, q15, q3
00ab3c70  vneg.f32 q11, q11
00ab3c74  vmul.f32 q7, q8, q13
00ab3c78  vmul.f32 q13, q9, q13
00ab3c7c  vmul.f32 q8, q8, q15
00ab3c80  vmul.f32 q9, q9, q15
00ab3c84  vorr     q6, q10, q10
00ab3c88  vsub.f32 q8, q8, q13
00ab3c8c  vmul.f32 q13, q11, q14
00ab3c90  vmul.f32 q11, q11, q10
00ab3c94  vmul.f32 q10, q12, q10
00ab3c98  vrev64.32 q8, q8
00ab3c9c  vmul.f32 q12, q12, q14
00ab3ca0  vswp     d16, d17
00ab3ca4  vadd.f32 q9, q9, q7
00ab3ca8  vadd.f32 q10, q10, q13
00ab3cac  vsub.f32 q12, q11, q12
00ab3cb0  vrev64.32 q9, q9
00ab3cb4  vmul.f32 q8, q8, q1
00ab3cb8  vswp     d18, d19
00ab3cbc  vmul.f32 q10, q10, q1
00ab3cc0  vmul.f32 q9, q9, q1
00ab3cc4  vmul.f32 q12, q12, q1
00ab3cc8  vst1.32  {d20, d21}, [r4]
00ab3ccc  vorr     q5, q14, q14
00ab3cd0  vst1.32  {d24, d25}, [r1]
00ab3cd4  add      r1, r1, #0x10
00ab3cd8  vst1.32  {d18, d19}, [r5]
00ab3cdc  vst1.32  {d16, d17}, [r0]
00ab3ce0  sub      r0, r0, #0x10
00ab3ce4  blt      #0xab3af4
00ab3ce8  add      sp, sp, #0x7c
00ab3cec  vpop     {d8, d9, d10, d11, d12, d13, d14, d15}
00ab3cf0  pop      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
00ab3cf4  nop      
00ab3cf8  orrlo    r0, r0, #0
00ab3cfc  orrlo    r0, r0, #0
00ab3d00  orrlo    r0, r0, #0
00ab3d04  orrlo    r0, r0, #0
00ab3d08  svclo    #0x3504f3
00ab3d0c  subseq   ip, r8, ip, lsl #17
00ab3d10  .word    0xffffffa8
00ab3d14  .word    0xffffffac
00ab3d18  .word    0xffffffb0
00ab3d1c  .word    0xffffffb4
00ab3d20  .word    0xffffffb8
00ab3d24  ldrsbeq  r0, [r5], #-0xb4

```
## Appendix E: exact trig table words, 0x01004A40..0x01005360

The following are the 584 little-endian binary32 bit patterns in address order.

```
0x3f3310af 0x3f33587a 0x3f33a029 0x3f33e7bc 0x3f311722 0x3f31a81d 0x3f3238aa 0x3f32c8c9
0x3f2d146a 0x3f2e3bde 0x3f2f61a5 0x3f3085bb 0x3f24d225 0x3f273656 0x3f299415 0x3f2beb4a
0x3f13682a 0x3f187fbf 0x3f1d7fd1 0x3f226799 0x3edae87d 0x3ef15ae7 0x3f039c3c 0x3f0e39da
0x3dc8bd35 0x3e47c5c4 0x3e94a034 0x3ec3ef15 0xbf0e39d9 0xbec3ef18 0xbe47c5c2 0x33a22290
0xbf6c835e 0xbf800000 0xbf6c8360 0xbf3504f3 0x3f342f33 0x3f34768f 0x3f34bdcf 0x3f3504f3
0x3f33587a 0x3f33e7bc 0x3f347690 0x3f3504f4 0x3f31a81e 0x3f32c8c9 0x3f33e7bc 0x3f3504f4
0x3f2e3bde 0x3f3085ba 0x3f32c8c9 0x3f3504f3 0x3f273656 0x3f2beb49 0x3f3085bb 0x3f3504f4
0x3f187fc0 0x3f226799 0x3f2beb4a 0x3f3504f4 0x3ef15aeb 0x3f0e39da 0x3f22679a 0x3f3504f3
0x3e47c5c4 0x3ec3ef11 0x3f0e39d9 0x3f3504f4 0xbec3ef18 0x33a22290 0x3ec3ef0e 0x3f3504f2
0x3f36f3df 0x3f36ad7f 0x3f366703 0x3f36206b 0x3f38dd65 0x3f385216 0x3f37c655 0x3f373a23
0x3f3ca002 0x3f3b8f3b 0x3f3a7ca4 0x3f396841 0x3f43e200 0x3f41d870 0x3f3fc767 0x3f3daef9
0x3f514d3d 0x3f4d9f03 0x3f49d112 0x3f45e403 0x3f676bd8 0x3f61c598 0x3f5b941b 0x3f54db31
0x3f7ec46d 0x3f7b14be 0x3f74fa0a 0x3f6c835e 0x3f54db32 0x3f6c835e 0x3f7b14be 0x3f800000
0xbec3ef15 0x34222290 0x3ec3ef10 0x3f3504f3 0x3f35d9b8 0x3f3592e8 0x3f354bfc 0x3f3504f3
0x3f36ad7f 0x3f36206b 0x3f3592e7 0x3f3504f3 0x3f385215 0x3f373a23 0x3f36206b 0x3f3504f3
0x3f3b8f3b 0x3f396842 0x3f373a23 0x3f3504f3 0x3f41d870 0x3f3daefa 0x3f396841 0x3f3504f3
0x3f4d9f02 0x3f45e403 0x3f3daef9 0x3f3504f3 0x3f61c597 0x3f54db31 0x3f45e403 0x3f3504f3
0x3f7b14be 0x3f6c835f 0x3f54db32 0x3f3504f3 0x3f6c835e 0x3f800000 0x3f6c8360 0x3f3504f4
0x3f7ffeea 0x3f7fff30 0x3f7fff6b 0x3f7fff9c 0x3f7ffbaa 0x3f7ffcbe 0x3f7ffdab 0x3f7ffe70
0x3f7feea7 0x3f7ff2f8 0x3f7ff6ac 0x3f7ff9c1 0x3f7fba9e 0x3f7fcbe2 0x3f7fdaaf 0x3f7fe705
0x3f7eea9d 0x3f7f2f9d 0x3f7f6ac7 0x3f7f9c18 0x3f7baccd 0x3f7cbfc9 0x3f7dabcc 0x3f7e70b0
0x3f6ed89e 0x3f731447 0x3f76ba07 0x3f79c79d 0x3f7fffc4 0x3f7fffe1 0x3f7ffff5 0x3f7fffff
0x3f7fff0e 0x3f7fff85 0x3f7fffd4 0x3f7ffffb 0x3f7ffc39 0x3f7ffe13 0x3f7fff4e 0x3f7fffec
0x3f7ff0e3 0x3f7ff84a 0x3f7ffd39 0x3f7fffb1 0x3f7fc38f 0x3f7fe129 0x3f7ff4e6 0x3f7ffec4
0x3f7f0e58 0x3f7f84ab 0x3f7fd397 0x3f7ffb11 0x3f7c3b28 0x3f7e1323 0x3f7f4e6d 0x3f7fec43
0xbbbc7e98 0xbba35cb6 0xbb8a3acb 0xbb6231ba 0xbc3c7dcb 0xbc235c31 0xbc0a3a7b 0xbbe23161
0xbcbc7a9a 0xbca35a1c 0xbc8a3938 0xbc623000 0xbd3c6dd4 0xbd2351cc 0xbd0a342f 0xbce22a7b
0xbdbc3ac2 0xbda3308c 0xbd8a2009 0xbd62146a 0xbe3b6ece 0xbe22abb6 0xbe09cf86 0xbde1bc2f
0xbeb84429 0xbea09ae5 0xbe888e93 0xbe605c14 0xbb2fedd0 0xbafb53ca 0xba96cbe2 0xb9c90fe2
0xbbafeda7 0xbb7b53ac 0xbb16cbdb 0xba490fde 0xbc2fed00 0xbbfb5333 0xbb96cbc1 0xbac90fdb
0xbcafea68 0xbc7b514e 0xbc16cb58 0xbb490fcb 0xbd2fe005 0xbcfb49bc 0xbc96c9b6 0xbbc90f8d
0xbdafb67f 0xbd7b2b77 0xbd16c32c 0xbc490e95 0xbe2f10a1 0xbdfab275 0xbd96a905 0xbcc90ab5
0x3fffffb1 0x3fffffb1 0x3fffffb1 0x3fffffb1 0x3ffffec4 0x3ffffec4 0x3ffffec4 0x3ffffec4
0x3ffffb11 0x3ffffb11 0x3ffffb11 0x3ffffb11 0x3fffec43 0x3fffec43 0x3fffec43 0x3fffec43
0x3fffb10f 0x3fffb10f 0x3fffb10f 0x3fffb10f 0x3ffec46d 0x3ffec46d 0x3ffec46d 0x3ffec46d
0x3ffb14be 0x3ffb14be 0x3ffb14be 0x3ffb14be 0x3fec835e 0x3fec835e 0x3fec835e 0x3fec835e
0x3fb504f3 0x3fb504f3 0x3fb504f3 0x3fb504f3 0xb3bbb0a8 0xb3bbb0a8 0xb3bbb0a8 0xb3bbb0a8
0x3f7ffb11 0x3f7ffc39 0x3f7ffd39 0x3f7ffe13 0x3f7fec43 0x3f7ff0e3 0x3f7ff4e6 0x3f7ff84a
0x3f7fb10f 0x3f7fc38f 0x3f7fd397 0x3f7fe129 0x3f7ec46d 0x3f7f0e58 0x3f7f4e6d 0x3f7f84ab
0x3f7b14bf 0x3f7c3b28 0x3f7d3aac 0x3f7e1324 0x3f6c835f 0x3f710908 0x3f74fa0b 0x3f7853f8
0x3f3504f4 0x3f45e403 0x3f54db31 0x3f61c598 0x33a22290 0x3e47c5bc 0x3ec3ef15 0x3f0e39da
0xbf800000 0xbf6c8360 0xbf3504f3 0xbec3ef14 0x3f7ffc39 0x3f7ffd39 0x3f7ffe13 0x3f7ffec4
0x3f7ff0e3 0x3f7ff4e6 0x3f7ff84a 0x3f7ffb11 0x3f7fc38f 0x3f7fd397 0x3f7fe129 0x3f7fec43
0x3f7f0e58 0x3f7f4e6d 0x3f7f84ab 0x3f7fb10f 0x3f7c3b28 0x3f7d3aac 0x3f7e1324 0x3f7ec46d
0x3f710908 0x3f74fa0b 0x3f7853f8 0x3f7b14be 0x3f45e403 0x3f54db31 0x3f61c598 0x3f6c835e
0x3e47c5bc 0x3ec3ef15 0x3f0e39da 0x3f3504f3 0xbf6c8360 0xbf3504f3 0xbec3ef14 0xb33bb0a8
0x3f7ffec4 0x3f7fff4e 0x3f7fffb1 0x3f7fffec 0x3f7ffb11 0x3f7ffd39 0x3f7ffec4 0x3f7fffb1
0x3f7fec43 0x3f7ff4e6 0x3f7ffb11 0x3f7ffec4 0x3f7fb10f 0x3f7fd397 0x3f7fec43 0x3f7ffb11
0x3f7ec46d 0x3f7f4e6d 0x3f7fb10f 0x3f7fec43 0x3f7b14bf 0x3f7d3aac 0x3f7ec46d 0x3f7fb10f
0x3f6c835f 0x3f74fa0a 0x3f7b14be 0x3f7ec46d 0x3f3504f4 0x3f54db30 0x3f6c835e 0x3f7b14bf
0x33a22290 0x3ec3ef0e 0x3f3504f2 0x3f6c835f 0x3f7fff4e 0x3f7fffb1 0x3f7fffec 0x3f800000
0x3f7ffd39 0x3f7ffec4 0x3f7fffb1 0x3f800000 0x3f7ff4e6 0x3f7ffb11 0x3f7ffec4 0x3f800000
0x3f7fd397 0x3f7fec43 0x3f7ffb11 0x3f800000 0x3f7f4e6d 0x3f7fb10f 0x3f7fec43 0x3f800000
0x3f7d3aac 0x3f7ec46d 0x3f7fb10f 0x3f800000 0x3f74fa0a 0x3f7b14be 0x3f7ec46d 0x3f800000
0x3f54db30 0x3f6c835e 0x3f7b14bf 0x3f800000 0x3ec3ef0e 0x3f3504f2 0x3f6c835f 0x3f800000
0xbc490e8f 0xbc2fed02 0xbc16cb58 0xbbfb5330 0xbcc90aaf 0xbcafea6a 0xbc96c9b6 0xbc7b514b
0xbd48fb2f 0xbd2fe007 0xbd16c32c 0xbcfb49b9 0xbdc8bd35 0xbdafb681 0xbd96a905 0xbd7b2b74
0xbe47c5c1 0xbe2f10a3 0xbe164083 0xbdfab272 0xbec3ef15 0xbeac7cd4 0xbe94a031 0xbe78cfcc
0xbf3504f3 0xbf22679a 0xbf0e39da 0xbef15ae9 0xbf800000 0xbf7b14bf 0xbf6c835e 0xbf54db31
0xb4222290 0xbec3ef10 0xbf3504f3 0xbf6c835f 0xbc2fed02 0xbc16cb58 0xbbfb5330 0xbbc90f88
0xbcafea6a 0xbc96c9b6 0xbc7b514b 0xbc490e90 0xbd2fe007 0xbd16c32c 0xbcfb49b9 0xbcc90ab0
0xbdafb681 0xbd96a905 0xbd7b2b74 0xbd48fb30 0xbe2f10a3 0xbe164083 0xbdfab272 0xbdc8bd36
0xbeac7cd4 0xbe94a031 0xbe78cfcc 0xbe47c5c2 0xbf22679a 0xbf0e39da 0xbef15ae9 0xbec3ef16
0xbf7b14bf 0xbf6c835e 0xbf54db31 0xbf3504f3 0xbec3ef10 0xbf3504f3 0xbf6c835f 0xbf800000
0xbbc90f87 0xbb96cbc3 0xbb490fc7 0xbac90fd3 0xbc490e8f 0xbc16cb5a 0xbbc90f89 0xbb490fc3
0xbcc90aaf 0xbc96c9b8 0xbc490e91 0xbbc90f85 0xbd48fb2f 0xbd16c32e 0xbcc90ab1 0xbc490e8d
0xbdc8bd35 0xbd96a907 0xbd48fb31 0xbcc90aad 0xbe47c5c1 0xbe164085 0xbdc8bd37 0xbd48fb2d
0xbec3ef15 0xbe94a033 0xbe47c5c3 0xbdc8bd33 0xbf3504f3 0xbf0e39db 0xbec3ef16 0xbe47c5bf
0xbf800000 0xbf6c8360 0xbf3504f4 0xbec3ef13 0xbb96cbc3 0xbb490fc7 0xbac90fd3 0x00000000
0xbc16cb5a 0xbbc90f89 0xbb490fc3 0x00000000 0xbc96c9b8 0xbc490e91 0xbbc90f85 0x00000000
0xbd16c32e 0xbcc90ab1 0xbc490e8d 0x00000000 0xbd96a907 0xbd48fb31 0xbcc90aad 0x00000000
0xbe164085 0xbdc8bd37 0xbd48fb2d 0x00000000 0xbe94a033 0xbe47c5c3 0xbdc8bd33 0x00000000
0xbf0e39db 0xbec3ef16 0xbe47c5bf 0x00000000 0xbf6c8360 0xbf3504f4 0xbec3ef13 0x00000000
```

## Appendix F: exact bit-reversal table, 0x01004640..0x01004A40

The following are the 512 little-endian `u16` values in address order.

```
0x0000 0x0100 0x0080 0x0180 0x0040 0x0140 0x00c0 0x01c0 0x0020 0x0120 0x00a0 0x01a0 0x0060 0x0160 0x00e0 0x01e0
0x0010 0x0110 0x0090 0x0190 0x0050 0x0150 0x00d0 0x01d0 0x0030 0x0130 0x00b0 0x01b0 0x0070 0x0170 0x00f0 0x01f0
0x0008 0x0108 0x0088 0x0188 0x0048 0x0148 0x00c8 0x01c8 0x0028 0x0128 0x00a8 0x01a8 0x0068 0x0168 0x00e8 0x01e8
0x0018 0x0118 0x0098 0x0198 0x0058 0x0158 0x00d8 0x01d8 0x0038 0x0138 0x00b8 0x01b8 0x0078 0x0178 0x00f8 0x01f8
0x0004 0x0104 0x0084 0x0184 0x0044 0x0144 0x00c4 0x01c4 0x0024 0x0124 0x00a4 0x01a4 0x0064 0x0164 0x00e4 0x01e4
0x0014 0x0114 0x0094 0x0194 0x0054 0x0154 0x00d4 0x01d4 0x0034 0x0134 0x00b4 0x01b4 0x0074 0x0174 0x00f4 0x01f4
0x000c 0x010c 0x008c 0x018c 0x004c 0x014c 0x00cc 0x01cc 0x002c 0x012c 0x00ac 0x01ac 0x006c 0x016c 0x00ec 0x01ec
0x001c 0x011c 0x009c 0x019c 0x005c 0x015c 0x00dc 0x01dc 0x003c 0x013c 0x00bc 0x01bc 0x007c 0x017c 0x00fc 0x01fc
0x0002 0x0102 0x0082 0x0182 0x0042 0x0142 0x00c2 0x01c2 0x0022 0x0122 0x00a2 0x01a2 0x0062 0x0162 0x00e2 0x01e2
0x0012 0x0112 0x0092 0x0192 0x0052 0x0152 0x00d2 0x01d2 0x0032 0x0132 0x00b2 0x01b2 0x0072 0x0172 0x00f2 0x01f2
0x000a 0x010a 0x008a 0x018a 0x004a 0x014a 0x00ca 0x01ca 0x002a 0x012a 0x00aa 0x01aa 0x006a 0x016a 0x00ea 0x01ea
0x001a 0x011a 0x009a 0x019a 0x005a 0x015a 0x00da 0x01da 0x003a 0x013a 0x00ba 0x01ba 0x007a 0x017a 0x00fa 0x01fa
0x0006 0x0106 0x0086 0x0186 0x0046 0x0146 0x00c6 0x01c6 0x0026 0x0126 0x00a6 0x01a6 0x0066 0x0166 0x00e6 0x01e6
0x0016 0x0116 0x0096 0x0196 0x0056 0x0156 0x00d6 0x01d6 0x0036 0x0136 0x00b6 0x01b6 0x0076 0x0176 0x00f6 0x01f6
0x000e 0x010e 0x008e 0x018e 0x004e 0x014e 0x00ce 0x01ce 0x002e 0x012e 0x00ae 0x01ae 0x006e 0x016e 0x00ee 0x01ee
0x001e 0x011e 0x009e 0x019e 0x005e 0x015e 0x00de 0x01de 0x003e 0x013e 0x00be 0x01be 0x007e 0x017e 0x00fe 0x01fe
0x0001 0x0101 0x0081 0x0181 0x0041 0x0141 0x00c1 0x01c1 0x0021 0x0121 0x00a1 0x01a1 0x0061 0x0161 0x00e1 0x01e1
0x0011 0x0111 0x0091 0x0191 0x0051 0x0151 0x00d1 0x01d1 0x0031 0x0131 0x00b1 0x01b1 0x0071 0x0171 0x00f1 0x01f1
0x0009 0x0109 0x0089 0x0189 0x0049 0x0149 0x00c9 0x01c9 0x0029 0x0129 0x00a9 0x01a9 0x0069 0x0169 0x00e9 0x01e9
0x0019 0x0119 0x0099 0x0199 0x0059 0x0159 0x00d9 0x01d9 0x0039 0x0139 0x00b9 0x01b9 0x0079 0x0179 0x00f9 0x01f9
0x0005 0x0105 0x0085 0x0185 0x0045 0x0145 0x00c5 0x01c5 0x0025 0x0125 0x00a5 0x01a5 0x0065 0x0165 0x00e5 0x01e5
0x0015 0x0115 0x0095 0x0195 0x0055 0x0155 0x00d5 0x01d5 0x0035 0x0135 0x00b5 0x01b5 0x0075 0x0175 0x00f5 0x01f5
0x000d 0x010d 0x008d 0x018d 0x004d 0x014d 0x00cd 0x01cd 0x002d 0x012d 0x00ad 0x01ad 0x006d 0x016d 0x00ed 0x01ed
0x001d 0x011d 0x009d 0x019d 0x005d 0x015d 0x00dd 0x01dd 0x003d 0x013d 0x00bd 0x01bd 0x007d 0x017d 0x00fd 0x01fd
0x0003 0x0103 0x0083 0x0183 0x0043 0x0143 0x00c3 0x01c3 0x0023 0x0123 0x00a3 0x01a3 0x0063 0x0163 0x00e3 0x01e3
0x0013 0x0113 0x0093 0x0193 0x0053 0x0153 0x00d3 0x01d3 0x0033 0x0133 0x00b3 0x01b3 0x0073 0x0173 0x00f3 0x01f3
0x000b 0x010b 0x008b 0x018b 0x004b 0x014b 0x00cb 0x01cb 0x002b 0x012b 0x00ab 0x01ab 0x006b 0x016b 0x00eb 0x01eb
0x001b 0x011b 0x009b 0x019b 0x005b 0x015b 0x00db 0x01db 0x003b 0x013b 0x00bb 0x01bb 0x007b 0x017b 0x00fb 0x01fb
0x0007 0x0107 0x0087 0x0187 0x0047 0x0147 0x00c7 0x01c7 0x0027 0x0127 0x00a7 0x01a7 0x0067 0x0167 0x00e7 0x01e7
0x0017 0x0117 0x0097 0x0197 0x0057 0x0157 0x00d7 0x01d7 0x0037 0x0137 0x00b7 0x01b7 0x0077 0x0177 0x00f7 0x01f7
0x000f 0x010f 0x008f 0x018f 0x004f 0x014f 0x00cf 0x01cf 0x002f 0x012f 0x00af 0x01af 0x006f 0x016f 0x00ef 0x01ef
0x001f 0x011f 0x009f 0x019f 0x005f 0x015f 0x00df 0x01df 0x003f 0x013f 0x00bf 0x01bf 0x007f 0x017f 0x00ff 0x01ff
```
