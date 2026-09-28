# Bounded pass: `0xA4FEF8` bus-output tail (the region the prior pass called the "shared bus-mix kernel")

Scope: `resources/lib/armeabi-v7a/libcozmoEngine.so`, 3.4.0-1204, ARM mode. All addresses are ELF VAs.
Read with `re-analysis/tools/arm_disasm.py` and a linear-sweep capstone script (`sweep.py`). Ghidra tree used for navigation only; every claim below is checked against the instructions.

## Headline finding (read first)

The region `0xA50044..0xA50FD0` is **not sample mixing**. It is the bus node's **post-FX level-analysis / metering** stage. It writes **no audio samples**: every store in the region goes to the per-channel scalar arrays hanging off `[out+0x18]` (`+4`, `+8`, `+0xc`, `+0x1c`), to the filter state (`[out+0x18]+0x10`), or to the stack. The child/voice sample mixing is done by the connection vfuncs `+0x2c`/`+0x30` inside `0xA4FEF8`, and after it returns by `0xA4F9E0` -> `0xA45E9C` (inventory V14/V30/C7).

The prior pass's label "the actual sample mixing of the bus output" is contradicted by the instructions. The "per-child gain accumulation" it reported is really a **per-channel** loop over the bus's own output buffer; the gain it saw (`out+0x14`) is the **bus output gain** that `0xA4D994` just wrote (bus+0x84), not a child gain. There is no child list at `[out+0x18]`; that pointer is a meter/analysis object.

`0xA4FEF8` callees (from the actual BL list): `0xA4F754`, `0xA4FD84`, `0xA4D994`, `0xA52164`, `0x9C806C`, `memset` (`0x4D36DC`), `sqrtf` (`0x4A4078`), plus three indirect vfuncs on the connection object. It does **not** call `0xA4F9E0`.

---

## Q1 - `0xA4FEF8` bus output and the tail `0xA50044..0xA50FD0`

### Signature and what `out` is

`0xA4FEF8(r0=bus, r1=&out)` writes the resulting buffer pointer to `*r1` and returns void.

- `0xA4FF40..0xA4FF58`: branch on `[bus+0x1a8]` and `[[bus+0x1a8]+0xc]` (the input connection object).
- child branch (`0xA4FF5C`): `conn->vt+0x2c(conn, bus+0x60)` (`0xA4FF5C ldr r3,[r0]; 0xA4FF64 ldr r3,[r3,#0x2c]; 0xA4FF6C blx r3`).
- `out = 0xA4FD84(bus)` (`0xA4FF74 bl 0x00A4FD84`), stored to `*r1` (`0xA4FF88 str r0,[r2]`). `0xA4FD84` returns `bus+0x60` by default, or an FX out-of-place slot `bus+0x138 + 0x1c*i` (V19). So `out` is the bus's **output buffer struct**, not a child list.
- bus gain copied into the buffer: `0xA4FFB4 add r3,r5,#0x80; 0xA4FFB8 ldm r3,{r0,r1}; 0xA4FFBC stm r4,{r0,r1}` with `r4 = out+0x10` -> `out+0x10 = bus+0x80` (gain prev), `out+0x14 = bus+0x84` (gain).
- child branch then `conn->vt+0x30(conn, out)` (`0xA50014..0xA50028`) and sets `local_cc = 1` (`0xA50040 str r3,[fp,#-0xc8]`). The no-child branch enters at `0xA50120`, sets `local_cc = 0` and falls into the same tail at `0xA50044`.
- tail: `0xA5002C ldr r6,[r4]` (`r6 = out`), `0xA50030 ldr sl,[r6,#0x18]` (`sl = [out+0x18]`), `0xA50038 beq #0xa500f4` if null.
- `0xA50044 ldrh r3,[r6,#0xe]`, `0xA50048 vldr s15,[r6,#0x14]` (`local_c8 = out+0x14`, the bus gain), `0xA50050 ldrb r3,[sl,#0x20]` (`local_a8 = [out+0x18]+0x20`, the analysis flags).

### Structures (all offsets read from instructions)

`out` (bus output buffer, `bus+0x60` or an FX slot):

| off | meaning | citation |
|---|---|---|
| +0 | `float*` sample data (planar) | `0xA501D0 ldr ip,[r6]`; `0xA50D84 ldr ip,[r6]` |
| +4 | u32 flags. bits 8..11 = channel config, must == 1; bits 12..31 = channel mask, ANDed with `0x637` | `0xA52180 ubfx r2,r3,#8,#4; cmp r2,#1`; `0xA521A0 ubfx r3,r3,#0xc,#0x14`; `0xA521A4 movw r2,#0x637` |
| +0xc | u16 channel stride (frames) | `0xA501C8 ldrh lr,[r6,#0xc]`; `0xA50D64`; `0xA52270 ldrh sl,[sl,#0xc]` |
| +0xe | u16 valid frames | `0xA50044 ldrh r3,[r6,#0xe]`; `0xA5013C` |
| +0x10 | float gain (previous) | `0xA4FFBC stm r4,{r0,r1}` |
| +0x14 | float gain (current, = bus+0x84) | `0xA50048 vldr s15,[r6,#0x14]` |
| +0x18 | pointer to the analysis/meter object `sl` | `0xA50030 ldr sl,[r6,#0x18]` |

`sl = [out+0x18]` (analysis/meter object):

| off | meaning | citation |
|---|---|---|
| +4 | `float*` output array (min/max peak, per channel) | `0xA50D6C ldr r5,[sl,#4]`; `0xA50F3C` memset |
| +8 | `float*` output array (RMS, per channel) | `0xA50CBC ldr r0,[sl,#8]`; `0xA50F50` memset |
| +0xc | `float*` output array (filtered peak, per channel) | `0xA50204 ldr r8,[sl,#0xc]`; `0xA50F7C` memset |
| +0x10 | filter state, 0x30 bytes (12 floats) per channel | `0xA5020C ldr sb,[sl,#0x10]`; `0xA50B58..0xA50B8C` copy-back |
| +0x14 | ptr A (filter coefficient/state object) | `0xA521F4 ldr r1,[r2,#0x14]` |
| +0x18 | ptr B | `0xA521F8 ldr r2,[r2,#0x18]` |
| +0x1c | float output of `0xA52164` | `0xA525AC vstr s15,[r3,#0x1c]` |
| +0x20 | u8 flags (bit0 peak, bit1 filtered peak, bit2 RMS, bit4 the `0xA52164` path) | `0xA50050 ldrb r3,[sl,#0x20]`; tests `0xA50060 tst r3,#1`, `0xA50090 tst r3,#4`, `0xA5009C tst r3,#2`, `0xA500A8 tst r3,#0x10` |

### Scalar control flow of the tail (0xA50044..0xA500F4)

```
if [out+0x18] == 0 -> tail-call conn->vt+0x34(conn,out,0)   ; 0xA50038 / 0xA500F4
local_cc = 1 (child) | 0 (no child)
if *(u16*)(out+0xe) == 0:              ; no valid frames: only clear arrays
    bit0 -> memset([sl+4],0, out+4*4)      ; 0xA50F08 -> 0xA50F28
    bit4 -> memset([sl+8],0, out+4*4)      ; 0xA50F30 -> 0xA50F50
    bit2 -> memset([sl+0xc],0,out+4*4)     ; 0xA50F58 -> 0xA50F7C
else:                                  ; valid frames: run the selected analysis
    bit0 -> 0xA50D48  (min/max peak, writes [sl+4]+ch*4)
    bit4 -> 0xA50C84  (RMS,          writes [sl+8]+ch*4)
    bit2 -> 0xA501B0  (filtered peak, writes [sl+0xc]+ch*4)
if local_a8 & 0x10 -> 0xA52164(sl, local_c8, out)          ; 0xA500B0..0xA500BC
if [bus+0xc1] & 0x1f -> 0x9C806C(reg, bus+0x48, [out+0x18], out+4)  ; 0xA500C0..0xA5019C
if local_cc == 0 -> return                                  ; 0xA500D0/0xA500DC
else conn->vt+0x34(conn, out, [out+0x18])                   ; 0xA500F4..0xA50110
```

The tests at `0xA500A4` (`ldr r3,[fp,#-0xa4]; tst r3,#0x10; beq #0xa500c0`) and `0xA500C0` (`ldr r3,[fp,#-0xa0]; ldrb r3,[r3,#0xc1]; tst r3,#0x1f; bne #0xa50178`) and `0xA500D0`/`0xA500E8` are exactly as the prior pass reported. The `0xA500A4` test is the meter flag, not a mix flag.

### The four analysis loops

**bit0 - min/max peak (`0xA50D48..0xA50E80`).** If gain <= 0: memset `[sl+4]` and goto `0xA500A4` (`0xA50D58 ble #0xa50f14`). Else `s5 = gain * 1.0009619` (`0xA50D5C vldr s5,[pc->0xA50FD4]=0x3F801F85`, `0xA50D7C vmul.f32 s5,s15,s5`). Per channel `sb = 0..out+4-1` (`0xA50D9C lsl r2,sb,#2`, `0xA50E70 cmp r3,sb`), source `= out+0 + ch*4*stride` (`0xA50DAC mla r2,r2,lr,ip`), destination `= [sl+4] + ch*4` (`0xA50DA4 add r7,r5,r2`). Inner loop `0xA50DBC..0xA50DD0`: `vld1.64 {d16,d17}` 4 floats, `vmin.f32 q10,q10,q8`, `vmax.f32 q9,q9,q8`, `add r2,r2,#0x10`, end `= source + (stride>>2)*16` (`0xA50D80 lsr r1,lr,#2; 0xA50D90 lsl r1,r1,#4`). Scalar reduce `0xA50DD4..0xA50E6C`, `vabs.f32 s13,s13` (`0xA50E50`), final `s13 = max(abs(min),max) * s5` (`0xA50E74 vmul.f32 s13,s5,s13`), store `0xA50E78 vstr s13,[r7]`. This is a per-channel **peak meter**.

**bit4 - RMS (`0xA50C84..0xA50D44`).** `r4 = out+4` channels (`0xA50C88 ldrb r4,[r6,#4]`), `s16 = gain * 1.0009619` (`0xA50C98/0xA50CA4/0xA50CA8/0xA50CAC`). Per channel `r7 = 0..r4-1` (`0xA50CB0`, `0xA50D2C add r7,r7,#1`, `0xA50D30 cmp r7,r4`), source `r3 = ip + ch*4*stride` (`0xA50CC8 mla r3,r1,lr,r3`), dest `r5 = [sl+8] + ch*4` (`0xA50CBC ldr r0,[sl,#8]; 0xA50CCC add r5,r0,r1`). Inner loop `0xA50CD4..0xA50CE8`: `vld1.64 {d16,d17}`, `vmul.f32 q8,q8,q8`, `vadd.f32 q9,q9,q8`, end `= source + (stride>>2)*16` (`0xA50CC0 lsr r2,lr,#2; 0xA50CD0 add r2,r3,r2,lsl #4`). Reduce `0xA50CEC..0xA50D08`, `vdiv.f32 s15,s15,s14` (`0xA50D14`, sum/stride), `vsqrt.f32 s14,s15` (`0xA50D18`), NaN guard -> `sqrtf` (`0xA50D24 bne #0xa50fb4`), `s14 *= s16` (`0xA50D28`), store `0xA50D34 vstr s14,[r5]`. Per-channel **RMS meter**.

**bit2 - filtered peak (`0xA501B0..0xA501E8` and the two NEON loops `0xA502BC..0xA50670`, `0xA50688..0xA50B54`).** `local_d4 = out+4` channels (`0xA501B4 str r3,[fp,#-0xd4]`); `lr = out+0xc` stride; `ip = out+0` (`0xA501C8/0xA501D0`); `local_d0 = gain * 1.0009619` (`0xA501D8..0xA501E4`). Per channel `local_b0 = 0..count-1` (increment `0xA50BC0 add r3,r3,#1`, compare `0xA50C58 cmp r3,r2`, reload `0xA50C6C..0xA50C80`), state offset `local_b4 += 0x30` per channel (`0xA50B98/0xA50BA8`). Per channel: peak out `= [sl+0xc] + ch*4` (`0xA50204`/`0xA50218`/`0xA5021C`), state `= [sl+0x10] + state_off` (`0xA5020C`/`0xA50220`); copies 12 floats of state to `local_b0` (`0xA5025C..0xA50280`) and 12 floats of the channel plane to `local_b0+0x30` (`0xA50284..0xA502B4`); runs the NEON filter block; stores `peak * local_d0` at `[sl+0xc]+ch*4` (`0xA50C5C..0xA50C64`); copies the 12-float state back (`0xA50B58..0xA50B8C`). The steady-state loop end is derived from the frame count at `0xA50674 lsr lr,lr,#2; 0xA50678 sub lr,lr,#0xf0000003; 0xA5067C add lr,ip,lr,lsl #4` and the loop backs at `0xA50670 blo #0xa502bc` and `0xA50B54 bhi #0xa50688`. This is a fixed-coefficient filtered peak (a weighting filter feeding a min/max envelope).

**bit4 - the `0xA52164` path (`0xA500B0..0xA500BC`).** See Q5.

### The NEON inner loops: operations, lanes, strides

The literal coefficient vectors sit in the pool `0xA506C0..0xA507CC`; values read directly:

```
0xA506C0 0x3AE00000 0.001708984  0xA506C4 0xBCEF0000 -0.029174805
0xA506C8 0xBC9B0000 -0.018920898 0xA506CC 0xBC080000 -0.008300781
0xA506D0 0x3C340000 0.010986328  0xA506D4 0x3CF00000 0.029296875
0xA506D8 0x3D078000 0.033081055  0xA506DC 0x3C740000 0.014892578
0xA506E0 0xBCA10000 -0.019653320 0xA506E4 0xBD540000 -0.051757813
0xA506E8 0xBD6E8000 -0.058227539 0xA506EC 0xBCDA0000 -0.026611328
0xA506F0 0x3D080000 0.033203125  0xA506F4 0x3DB68000 0.089111328
0xA506F8 0x3DD00000 0.101562500  0xA506FC 0x3D430000 0.047607422
0xA50700 0xBD738000 -0.059448242 0xA50704 0xBE2A8000 -0.166503906
0xA50708 0xBE4D2000 -0.200317383 0xA5070C 0xBDD18000 -0.102294922
0xA50710..0xA5074C repeat the 0x3AE00000..0xBDD18000 set in a different order
0xA50750 0x3F78E000 0.972167969  0xA50754 0x3F47A000 0.779785156
0xA50758 0x3EEE2000 0.465087891  0xA5075C 0x3E0CA000 0.137329102
0xA50760..0xA5077C = the 0xBDD18000..0xBCA10000 set reversed
0xA50780..0xA5079C = 0x3D430000,0x3DD00000,0x3DB68000,0x3D080000,0x3E0CA000,0x3EEE2000,0x3F47A000,0x3F78E000
0xA507A0..0xA507CC = 0x3C340000,0x3CF00000,0x3D078000,0x3C740000,0xBC080000,0xBC9B0000,0xBCEF0000,0x3AE00000,0xBCA10000,0xBD540000,0xBD6E8000,0xBCDA0000
0xA50FD4 0x3F801F85 1.0009619   0xA50FD8 0x00000000
```

Observed operations (all 4-lane, `float32`):
- load 4 samples: `0xA502BC vld1.64 {d16,d17},[r3:0x40]`, `0xA50688 vld1.64 {d16,d17},[ip:0x40]`, `0xA506A8 vld1.64 {d24,d25},[ip:0x40]`; step `add r3,r3,#0x10` / `add ip,ip,#0x10`.
- broadcast scalars to lanes: `vdup.32 q9,r0`, `vdup.32 q8,r2`, `vdup.32 q10,r8`, `vdup.32 q11,r1`, `vdup.32 q12,r0`, ... (the scalar lane extractors `vmov.32 r0,d16[0]`, `r2=d16[1]`, `r8=d17[0]`, `r1=d17[1]` at `0xA502CC..0xA502D8` show the 4 lanes are extracted individually).
- arithmetic: long sequences of `vmul.f32` / `vadd.f32` (the Ghidra `FloatVectorMult`/`FloatVectorAdd`), and `vmin.f32` / `vmax.f32` (`0xA5060C`, `0xA50618`, `0xA50640`, `0xA50648`, `0xA5064C`, `0xA50654`, `0xA50658`, `0xA50664`; and `0xA50AF0`..`0xA50B48`).
- stores: only to the stack (`vstr d16,[fp,#-0x7c]`, `[fp,#-0x74]`, `[fp,#-0x8c]`, `[fp,#-0x84]`) and the state copy (`0xA50B58..0xA50B8C`).

**What is not confidently transliterable.** The scalar control flow, the loop bounds, the pointer steps and the load/store widths are readable. The exact per-lane tap-to-state mapping of the two bit-2 NEON blocks is **not** confidently transliterable: the compiler interleaves four lanes (the lane extractors at `0xA502CC..0xA502D8` and the `vdup.32` broadcasts show four independent scalar channels being processed in one vector unit), it schedules the 24-float state buffer (`local_b0`, 12 state + 12 source floats) across the block, and Ghidra's `FloatVector*` reconstruction is not reliable here. The DSP identity of the literal coefficient set is also not established from any shipped artifact (no "meter"/"peak"/"RMS"/"loudness" string exists in the `.so`; the only hit is an unrelated `rms %f` at file offset `0xc01dca`). What to read to settle it: the Wwise 2016.2 SDK source for the bus meter/mixer, or a dynamic trace of `0xA4FEF8` on the original. Until then: RECOVERABLE_GAP.

### Citations
- entry/branch: `0xA4FEF8`..`0xA4FF58`; `0xA4FF74 bl 0x00A4FD84`; `0xA4FFB4..0xA4FFC0`; `0xA50014..0xA50040`.
- tail head: `0xA5002C`..`0xA5005C`.
- flag dispatch: `0xA50060`, `0xA50068`, `0xA50070`, `0xA50084`, `0xA5008C`, `0xA50094`, `0xA500A4`, `0xA500C0`, `0xA500D0`, `0xA500E8`.
- bit0 loop: `0xA50D48`..`0xA50E80`; memset `0xA50F3C`/`0xA50F50`/`0xA50F7C`.
- bit4 loop: `0xA50C84`..`0xA50D44`; sqrtf `0xA50FB4`.
- bit2 loop: `0xA501B0`..`0xA502BC`, `0xA502BC`..`0xA50670`, `0xA50674..0xA50688`, `0xA50688`..`0xA50B54`, `0xA50B58`..`0xA50C80`.
- coefficients: `0xA506C0..0xA507CC`, `0xA50FD4`.

### Classification
- scalar control flow, field offsets, loop bounds, pointer strides, gain arithmetic, the memset paths, the `0xA52164`/`0x9C806C` gates, the return paths: **EXACT_SOURCE**.
- the exact per-lane mapping of the bit-2 NEON filter and the DSP identity of the fixed coefficient set: **RECOVERABLE_GAP** (read the Wwise 2016.2 SDK bus-meter/mixer source or trace `0xA4FEF8`; no shipped artifact settles it).

---

## Q2 - `0xA4D994(bus, &local)`

Read in full (`0xA4D994..0xA4DD43`). It is the bus's **output gain + parameter update**, called on both bus-output paths immediately before the tail.

1. `0xA4D994 ldr ip,[r0,#0x34]; 0xA4D998 ldr r3,[r0,#0x84]; 0xA4D9B0 str r3,[r0,#0x80]` - saves the current gain `bus+0x84` to `bus+0x80` (prev/next pair used downstream at `out+0x10/+0x14`).
2. If `bus+0x34 != 0`, it (re)allocates `((bus+0x44 + 3) >> 2) * (*(u8*)r1 * 4)` bytes via `0x4D37F0` (`0xA4D9B8..0xA4DA10`). `r1` is the caller's local; `0xA4FEF8` passes a pointer whose first word is `*(u32*)(out+4)` (the channel/flags word), so the byte is the channel count (`0xA4FF90 ldr r3,[r0,#4]; ... 0xA4FFB0 bl 0xA4D994`).
3. **Bus gain.** `s15 = bus+0x90 * 0.05` (`0xA4D9E0/0xA4D9E4 vldr s13,[pc->0xA4DD44]=0x3D4CCCCD`); if `s15 < -37.0` (`0xA4D9E8 vldr s14,[pc->0xA4DD48]=0xC2140000`) then `bus+0x84 = 0` (`0xA4D9FC/0xA4DA00`); else the fast 10^x: `0xA4DA30 vldr s13,[pc->0xA4DD4C]=0x4BD49A78 (27866352.0)`, `0xA4DA38 s14=[pc->0xA4DD50]=0x4E7E0000 (1065353216.0)`, `0xA4DA40 vmla.f32 s14,s15,s13`, `0xA4DA4C vcvt.u32.f32`, `0xA4DA54..0xA4DA64` exponent/mantissa split, `0xA4DA68 vmla.f32 s13,s14,s12` with `s12=[0xA4DD54]=0x3EA67F46 (0.32518977)`, `0xA4DA6C vmla.f32 s15,s14,s13` with `0xA4DA48 s15=[0xA4DD5C]=0x3F272DDB (0.65304345)`, `s13=[0xA4DD58]=0x3CAA70DE (0.02080577)`, `0xA4DA74 vmul.f32 s15,s15,s14`, `0xA4DA78 vstr s15,[r4,#0x84]`. This is exactly the `dBToLin` of M6-010 (y = 0.05*dB; y < -37 -> 0; bits = 1065353216 + 27866352*y; polynomial 0.6530434 + m(0.0208058 + 0.3251898m)). So `bus+0x90` is the bus Volume in dB and `bus+0x84` becomes its linear gain.
4. **Other params.** `p1 = clamp((bus+0x94 + 100.0) * 0.005, 0, 1)` (`0xA4DAA4..0xA4DAD0`, `s11=[0xA4DD60]=0x42C80000 (100.0)`, `s12=[0xA4DD64]=0x3BA3D70A (0.005)`), `p2 = clamp((bus+0x98 + 100.0) * 0.005, 0, 1)` (`0xA4DAD8..0xA4DAF8`). `s13 = bus+0x9c`; if `bus+0xc0 & 2` then `s13 = s13 / 100.0` (`0xA4DB00 tst ip,#2; 0xA4DB08 vldrne s12,[0xA4DD60]; 0xA4DB1C vdivne.f32 s13,s13,s12`), else `s13 = 1.0` (`0xA4DB14 vmoveq.f32 s13,#1.0`).
5. **Registry lookup.** `0xA4DB04 ldrd r6,r7,[r4,#0x28]`; `0xA4DB0C ldr r2,[pc,r2]` (GOT) and `0xA4DB18 ldr r2,[r2,#8]`; walks a list at `[reg+8]`, matching `[r2+0x10]/[r2+0x14]` against `bus+0x28`/`bus+0x2c` (`0xA4DB2C..0xA4DB44`). It passes the matching node as a stack argument.
6. **Call `0xA25FF8`** (`0xA4DB68 bl 0x00A25FF8`) with `r0=p1`, `r1=p2`, `r2=s13`, and stack args `{*(u32*)local, bus+0x44, ip=bus+0xc0, matched node}` plus `[bus+0x3c]`/`[bus+0x40]` (`0xA4DB48..0xA4DB64`). `0xA25FF8` is a 12496-byte routine with `cosf`/`sinf`/`sqrtf` and the constants `0.8660254`, `0.7905694` (its decomp signature is `(float,float,float,float,dword*,uint,uint,float*,float)`); its semantics are **RECOVERABLE_GAP** (it is the only unread callee of `0xA4D994`).
7. If `bus+0xc0 & 4`, it calls `0x9C7FE4(reg, ...)` and multiplies `bus+0x84` by the returned float (`0xA4DC8C..0xA4DD20`). `0x9C7FE4` is the 8-byte `{key,fn}` registry dispatch (Q4).
8. `0xA4DB78..0xA4DB84` copies `bus+0x94..0xa0` to `bus+0xa4..0xb0` (prev param set), and `0xA4DBD4/0xA4DBDC` sets/clears `bus+0xc0` bit3.

**Answer:** `0xA4D994` computes the bus output gain `bus+0x84 = dBToLin(bus+0x90)` with `bus+0x80` as the previous value, normalizes `bus+0x94`/`bus+0x98`/`bus+0x9c`, looks up a per-bus registry node keyed by `bus+0x28/bus+0x2c`, and calls `0xA25FF8` to apply the parameter set to the buffer; if `bus+0xc0 & 4` it additionally scales `bus+0x84` by the result of the `0x9C7FE4` callback. It does not touch audio samples directly.

### Citations
`0xA4D994..0xA4D9B4`; gain `0xA4D9E0..0xA4DA78`; params `0xA4DA80..0xA4DB1C`; registry `0xA4DB04..0xA4DB44`; call `0xA4DB48..0xA4DB68`; `0xA4DC8C..0xA4DD20`; copy-back `0xA4DB78..0xA4DB84`. Constants at `0xA4DD44..0xA4DD68`.

### Classification
- gain/prev/param arithmetic, the registry walk, the copy-back, the call: **EXACT_SOURCE** (matches inventory 3.2 for the gain part).
- `0xA25FF8` semantics: **RECOVERABLE_GAP** (read `0xA25FF8`'s mode dispatch; it is the only unread callee).

---

## Q3 - relation to `0xA4F9E0` (C7)

**The kernel does not call `0xA4F9E0`.** `0xA4FEF8`'s complete BL list is `0xA4F754`, `0xA4FD84`, `0xA4D994`, `0xA52164`, `0x9C806C`, `memset`, `sqrtf` (grep of the disassembly; no `0xA4F9E0`). `0xA4F9E0` is called by the **callers** of `0xA4FEF8`, on the bus-output path, *after* the kernel returns:

- `0xA57FF8` (the render body) at `0xA44C18`: `FUN_00a4fef8(iVar5, local_2c)` then, if `[bus+0x1c8] != 0`, `FUN_00a4f9e0([bus+0x1c8], local_2c[0], bus)`; else the `0x9E9E78` device path (V17).
- `0xA40CA4` does the same.

So `0xA4F9E0` is the **child-bus / output-bus mix outside the kernel**, exactly as inventory C7 describes ("mixes a child bus/voice into the bus": `[bus+0x1BC]==4 -> 1`, `[bus+0x68]=0x2D`, zero-pad, then `[bus+0x1A8]+0xC -> vt+0x28`, else the `0xA45E9C` mixer). The relation is: `0xA4FEF8` produces the bus's post-FX output buffer and its meter values; the caller then hands that buffer to `0xA4F9E0` (Hijack/output-bus) or `0x9E9E78` (device). No call/return relation exists between the kernel and `0xA4F9E0`.

### Citations
- `0xA4FEF8` BL list: `.scratch/m6b-extract/a4fef8_full.dis`.
- `0xA57FF8`: `unity` decomp `00a57ff8.c:38,51`; `0xA44C18` V17; `0xA40CA4` decomp `00a40ca4.c:15,33`.
- C7: `re-analysis/inventory/M6-wwise-bank.md:1280,1473`.

### Classification
**EXACT_SOURCE** for the non-call and the caller relation. Note: M6-022 lists `0xA4F9E0` inside "the per-voice DSP chain"; that grouping is imprecise (it is the generic connection mix, used on the bus output here), but C7 itself is correct.

---

## Q4 - `0x9C806C` (called at `0xA500C0`)

Read in full (`0x9C806C..0x9C8107`, 156 bytes). It is a **mutex-guarded registered-callback dispatch**.

Body:
```
0x9C806C push; r6 = r0+0x18; sb = r0; r5 = r1 (key); r7 = r2 (arg0); r8 = r3 (arg1)
0x9C8088 bl 0x4D3064            ; lock(r0+0x18)
0x9C808C ldr r4,[sb,#0x10]      ; count
0x9C8090 ldr lr,[sb,#0xc]       ; base
0x9C8094 add r4,r4,r4,lsl #1    ; r4 = count*3
0x9C8098 add r4,lr,r4,lsl #2    ; end = base + count*12
loop 0x9C80A4..0x9C80CC: if [entry] == key goto 0x9C80E4
0x9C80D0 mov r4,#0; unlock; return 0
0x9C80E4 adds r4,lr,#4          ; entry+4
0x9C80E8 beq 0x9C80D4           ; if [entry+4]==0 return 0
0x9C80EC mov r0,r7              ; arg0
0x9C80F0 mov r1,r8              ; arg1
0x9C80F4 ldr r3,[lr,#4]         ; fn = entry[1]
0x9C80FC ldr r2,[lr,#8]         ; ctx = entry[2]
0x9C8100 blx r3                 ; fn(arg0, arg1, ctx)
0x9C8104 b 0x9C80D4             ; unlock; return 1
```
So `0x9C806C(registry, key, arg0, arg1)` searches a 12-byte-entry list `{[key,fn,ctx]}` at `[registry+0xc]` (count `[registry+0x10]`), and on a match calls `fn(arg0, arg1, ctx)` under `registry+0x18`; returns 1 if a matching entry with a non-null `fn` exists, else 0.

At the call site `0xA50178..0xA5019C`: `key = bus+0x48`, `arg0 = [[out]+0x18]` (the meter object `sl`), `arg1 = [[out]+4]` (the channel/flags word). The registry pointer comes from `0xA5017C ldr r0,[pc,#0x24]` (literal `0x005EFFB8`) then `0xA50188 ldr r0,[pc,r0]` (GOT slot at `0x1040148`) then `0xA50194 ldr r0,[r0]`. The GOT slot `0x1040148` contains `0x0108D95C` (a `.bss` global); the decompiler labels it `uRam0109d95c` (off by `0x10000`). The same global is used by `0x9C7FE4` (8-byte `{key,fn}` list at `[reg]`/`[reg+4]`), `0x9C8108` (`{key,fn}` presence test -> `bus+0xc0` bit2) and `0x9C817C` (12-byte list, returns `entry[2]` if `entry[1] != 0` -> `bus+0xc1` low 5 bits). It is populated at engine init: `0xA02F80` (called from `SoundEngine::Init 0x99E3EC`) sets the global and builds the list.

So `0x9C806C` takes the bus's registered callback (keyed by the bus ID `bus+0x48`, set in the bus ctor `0xA4F0EC` from `0xA68A2C`) and calls it with the meter object and the channel/flags word. It does **not** read `[bus+0x48]` itself; it receives it as the key. The gate is `[bus+0xc1] & 0x1f`, and `bus+0xc1` is exactly `0x9C817C(registry, bus+0x48) & 0x1f` written by `0xA4F0EC`.

**What the registered callbacks are** is not established from the shipped artifact: the strings contain no meter/peak/RMS name, and no writer of the list is reachable except through `0xA02F80`. The registry is a generic engine-init callback table; its semantic owner is RECOVERABLE_GAP.

### Citations
- dispatch: `0x9C806C..0x9C8104`; list stride `0x9C8094 add r4,r4,r4,lsl #1; 0x9C8098 add r4,lr,r4,lsl #2`.
- call site: `0xA50178..0xA5019C`; GOT literal `0xA501A8`; GOT slot `0x1040148 -> 0x0108D95C`.
- sibling APIs: `0x9C7FE4`, `0x9C8108`, `0x9C817C`; init `0xA02F80` from `0x99E3EC`; bus ctor `0xA4F0EC` (`0xA4F0EC` decomp lines 35,38,45).

### Classification
**EXACT_SOURCE** for the dispatch body and the argument wiring. **RECOVERABLE_GAP** for the semantic identity/owner of the registered callbacks (read the writers of the `0x0108D95C` list, i.e. the callers of `0x9C8108`/`0x9C817C`'s add counterpart; not present in the code I read).

---

## Q5 - `0xA52164` (called when `[fp-0xa4] & 0x10`)

Read in full (`0xA52164..0xA5266B`, 1288 bytes). Call site `0xA500B0..0xA500BC`: `r0 = sl = [out+0x18]` (meter), `r1 = local_c8 = *(float*)(out+0x14)` (bus gain), `r2 = out`.

Body:
1. `0xA52178 ldr r3,[r2,#4]`; `0xA52180 ubfx r2,r3,#8,#4; cmp r2,#1; beq 0xA521A0`. If the channel config (bits 8..11 of `out+4`) is not 1, `0xA5218C mov r3,#0; 0xA52190 str r3,[r0,#0x1c]` (meter+0x1c = 0) and return.
2. `0xA521A0 ubfx r3,r3,#0xc,#0x14` (channel mask); `0xA521A4 movw r2,#0x637`; `0xA521AC and r3,r3,r2`; if zero -> `0xA52630`. `0xA521B4 vmov s7,r1` (gain).
3. Popcount of the mask into `r6` (`0xA521C4..0xA521D8`), then `sub sp,sp,r6*16+8` twice (`0xA521E0..0xA52204`) for two stack arrays.
4. `r1 = [meter+0x14]` (A), `r2 = [meter+0x18]` (B) (`0xA521F4/0xA521F8`). For each active channel: copies 4 floats from `A+0xa0` and 4 from `B+0xa0` (stride 0xb0) into the stack arrays (`0xA5222C..0xA52260`).
5. `r3 = (mask >> 0) & 7` (`0xA52270 and r3,r3,#7`) - the second-stage selection; popcount into `r7` (`0xA52288..0xA5229C`). If `r7 == 0` -> `0xA52660`.
6. Per active channel, a **cascade of two biquads** on the samples, e.g. `0xA5233C vmul.f32 s14,s3,s20`, `0xA52344 vmla.f32 s14,s9,s21`, `0xA52348 vmla.f32 s14,s10,s19`, `0xA5234C vmla.f32 s14,s4,s18`, `0xA52350 vmla.f32 s14,s11,s17` (section 1: 5 taps), then `0xA52354 vmul.f32 s15,s5,s16`, `0xA52358 vmla.f32 s15,s14,s22`, `0xA5235C vmla.f32 s15,s13,s0`, `0xA52360 vmla.f32 s15,s6,s1`, `0xA52364 vmla.f32 s15,s12,s2` (section 2: 5 taps). The feedback states are rolled (`0xA52328..0xA52334`, `0xA52368..0xA52374`), and the second-stage sum of squares is accumulated `0xA52378 vmla.f32 s8,s15,s15` (and `0xA5250C vmla.f32 s2,s15,s15` for the second array). The source pointer steps `0xA52338 vldmia r3!,{s9}` and the destination is stored back `0xA523C4 vst1.64 {d18,d19},[r4:0x40]`, `0xA523C8 vst1.64 {d16,d17},[r0:0x40]` (the coefficient/state arrays), not the audio.
7. A second pass over the remaining channels at `0xA52424..0xA52578` (same biquad), then the scalar reduction:
   - `0xA5257C s15=[0xA52670]=0x3FB4CE07 (1.41253746)`; `0xA52580 vmul.f32 s2,s2,s15`.
   - `0xA52584 s15=[0xA52674]=0x3F801F85 (1.0009619)`; `0xA52590 vmul.f32 s7,s7,s15`; `0xA52598 vmul.f32 s7,s7,s7` (gain squared).
   - `0xA5259C vadd.f32 s8,s8,s2`; `0xA525A0 vmul.f32 s8,s7,s8`; `0xA525A4 vcvt.f32.u32 s15,s15` (channel count); `0xA525A8 vdiv.f32 s15,s8,s15`; `0xA525AC vstr s15,[r3,#0x1c]` -> **meter+0x1c = gain^2 * (sumSq_stage2 + 1.41253746 * sumSq_other) / numChannels**.
8. If channels remain, copies the coefficient/state arrays back (`0xA525C8..0xA525FC`).

**Answer:** `0xA52164` is a per-channel **two-section biquad cascade + weighted mean-square** stage selected by meter flag bit 0x10. It requires the channel config to be 1 (else it zeroes meter+0x1c), uses the mask `(out+4 >> 12) & 0x637` to choose the active channels, filters each channel's samples with the coefficients/state at `meter+0x14`/`meter+0x18` (state at `+0xa0`, stride 0xb0), and writes `gain^2 * (E1 + 1.41253746*E2) / numChannels` to `meter+0x1c`. It does not modify audio samples.

### Citations
`0xA52164..0xA521A8`; mask `0xA521A0..0xA521AC`; popcount `0xA521C4..0xA521D8`; arrays `0xA521E0..0xA52204`; state copy `0xA5222C..0xA52260`; biquad `0xA5233C..0xA52378` and `0xA524D0..0xA5250C`; reduction `0xA5257C..0xA525AC`; constants `0xA52670`, `0xA52674`; return `0xA52600..0xA52608`.

### Classification
- control flow, mask/flag arithmetic, per-channel loop, the biquad tap count and state strides, the reduction formula: **EXACT_SOURCE**.
- the DSP identity of the two-section filter and the `1.41253746` (= 10^0.15) weighting: **RECOVERABLE_GAP** (same reason as Q1: no SDK header/strings; read the Wwise bus-meter source or trace).

---

## NEW steps the prior pass / M6-022 did not cover

| step | what the original does | citation | classification |
|---|---|---|---|
| N1 | The `0xA4FEF8` tail `0xA4FF40..0xA50FD0` is the bus **level-analysis/metering** stage, not the sample mix. Four flag-selected per-channel analyses: min/max peak -> `meter+4`; RMS -> `meter+8`; fixed-coefficient filtered peak -> `meter+0xc`; `0xA52164` biquad+loudness -> `meter+0x1c`. | `0xA50044`..`0xA50FD0`; `0xA50D48`; `0xA50C84`; `0xA501B0`; `0xA500B0` | NEW, EXACT_SOURCE for structure; filter identity RECOVERABLE_GAP |
| N2 | The meter object is `[out+0x18]` (= `bus+0x78`); its flag byte `+0x20` selects the analyses, and it is cleared by `memset` per flag when there are no valid frames. | `0xA50030`; `0xA50050`; `0xA50F08/0xA50F30/0xA50F58` | NEW, EXACT_SOURCE |
| N3 | The bus output buffer fields are `+0` pData, `+4` u32 {config bits 8..11 == 1, mask bits 12..31 & 0x637}, `+0xc` u16 stride, `+0xe` u16 valid, `+0x10/+0x14` prev/next bus gain (copied from `bus+0x80/+0x84`). | `0xA4FFB4..0xA4FFC0`; `0xA50044`; `0xA52180/0xA521A0` | NEW, EXACT_SOURCE |
| N4 | `[bus+0xc1] & 0x1f` is the 5-bit gate for the registered per-bus callback `0x9C806C(reg, bus+0x48, meter, out+4)`; `bus+0xc1` is written by the bus ctor `0xA4F0EC` from `0x9C817C(reg, bus+0x48)`, and `bus+0x48` is the bus ID from `0xA68A2C`. | `0xA500C0`..`0xA5019C`; `0xA4F0EC` decomp lines 35,38,45; `0x9C817C` | NEW, EXACT_SOURCE for wiring; callback owner RECOVERABLE_GAP |
| N5 | `0xA4D994` also handles `bus+0x94/+0x98/+0x9c` (normalized params), a per-bus registry node keyed by `bus+0x28/bus+0x2c`, and calls `0xA25FF8`; `bus+0xc0 & 4` scales `bus+0x84` by the `0x9C7FE4` callback. | `0xA4DAA4..0xA4DB68`; `0xA4DC8C..0xA4DD20` | NEW, EXACT_SOURCE for wiring; `0xA25FF8` RECOVERABLE_GAP |

---

## Existing records contradicted by the source

1. **The prior pass's report that `0xA50044..0xA50FD0` is "the actual sample mixing of the bus output"** is contradicted. There is no store to `out+0` (the audio) anywhere in the region: the only stores are `vstr` to stack, the 0x30-byte state copy `0xA50B58..0xA50B8C`, and the per-channel scalar arrays `meter+4/+8/+0xc/+0x1c`. The mixing is the connection `vt+0x2c`/`vt+0x30` inside `0xA4FEF8` and `0xA4F9E0`/`0xA45E9C` after it.
2. **The prior pass's "child-list structure (buffer+0x18)" and "per-child gain accumulation"** are not supported. `[out+0x18]` is the analysis/meter object; the loop is over **channels** (`out+4`), and `out+0x14` is the bus gain written by `0xA4D994`, not a per-child gain.
3. **M6-022's grouping of `0xA4F9E0` into "the per-voice DSP chain"** is imprecise: on this path `0xA4F9E0` is the bus->output-bus mix called by the `0xA4FEF8` callers (`0xA57FF8:51`, `0xA40CA4:33`), not by `0xA4FEF8`.

## Existing records whose evidence is too weak for their status

- **V18 (`M6-022`, EXACT_SOURCE)**: its citation stops at `0xA4FF3C` and says only "else `0xA50120`". It does not describe the `0xA4FEF8` tail `0xA4FF40..0xA50FD0`, which is the bulk of the bus output path. V18 is correct for what it claims but M6-022 does not own the whole bus-output production path.
- **3.2 (`M6-022`, EXACT_SOURCE)**: correct for the `0xA4D9E0..0xA4DA78` gain arithmetic, but the row's claim that `0xA4D994` "copies {prev,next} into the output buffer's +0x10" is done by `0xA4FEF8` at `0xA4FFB4..0xA4FFC0`, not inside `0xA4D994`; and `0xA4D994` also calls `0xA25FF8` (unread), which the row does not mention.
- **C7 (`M6-022`, EXACT_SOURCE)**: its citation `0xA4F9E0; 0xA4F9E4; 0xA4F9EC; 0xA4FA0C; 0xA4FB44` covers the mix, but its title says "mixes a child bus/voice"; on the bus path it is the bus's **output**-connection mix. Behaviour read, label narrow.
- **M6-022's `0xA52164`/`0x9C806C` absence**: neither appears in M6-022's inventory rows; they are only implicit in the "unbuilt bodies" list. They need their own records (N1/N4).

## Open questions the manager must decide

1. Does the stack model the bus meter at all? The kernel is source-backed but the semantic owner of the callback registry (`0x0108D95C`) and the filter identity are not. If the robot-audio path never reads a meter, this tail may be dead for the Hijack path — but the `0x9C806C` callback is gated only by `bus+0xc1`, which the bus ctor sets from the registry, so whether Robot_Bus_1 has a registered callback is the deciding fact. Read the add-side of `0x0108D95C` (the function that calls `0x9C8108`/`0x9C817C`'s counterpart) to settle it.
2. `0xA25FF8` (12 KB, called by `0xA4D994`) is unread and is the only unread callee of the bus gain update. Does the inventory want it as its own record, or folded into M6-022?
3. The fixed coefficient set at `0xA506C0..0xA507CC` has no shipped name. If exact reproduction is required, the SDK source or a trace is needed; otherwise the manager must classify the meter's DSP as RECOVERABLE_GAP and say so in M6-022.
4. V18 should be extended (or a new record added) to cover `0xA4FF40..0xA50FD0`, and the "bus-mix kernel" label should be corrected to "bus level analysis".
