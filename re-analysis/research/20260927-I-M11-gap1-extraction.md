# I-M11 gap pass 1 — extraction (read-only)

Job: gap pass 1 for the M11-vision integration job (I-M11). Scope: close G1..G6 of
`re-analysis/research/20260927-X1-M11-extraction.md` section 5 and the X1 "too weak"
evidence rows.
Agent: opencode (DeepSeek), window 3. Date: 2026-09-27.
Scratch: `.scratch/I-M11-gap1/` (Thumb disassembler `vdis.py`, raw dumps).

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204).
The vision region is Thumb-2; disassembled with capstone `CS_MODE_THUMB` (base 0). All
addresses below are file VAs. The Ghidra decompilation was not used (this clone's
`re-analysis/decomp/libcozmoEngine/` has `index.tsv` but no per-function files).

Authority-6 leads checked against the `.so`:
- `re-analysis/evidence/m11/ecvcs-extractor.md` — checked instruction by instruction; see G1.15.
- `re-analysis/evidence/m11/marker-frontend.md` — checked for the rows this pass re-read; the
  front-end call graph and constants it names are real, but its A1/A5b/D3 items are superseded
  by the X1 pass and by G2 below. It is not used as evidence here.

Row form: `| # | what the original does | citation | record | class |`.
class = EXACT_SOURCE, RECOVERABLE_GAP (with exactly what to read) or UNKNOWN.

---

## G1. The live component extractor (M11-032 / M11-018 / M11-025)

The shipped selector byte is 1, so the live function is
`ExtractComponentsViaCharacteristicScale` at `0x0088F8BC` (the integral-image / box-filter
variant), not `..._binomial` at `0x00890448`. The evidence file
`re-analysis/evidence/m11/ecvcs-extractor.md` is **confirmed instruction for instruction**,
except for the divergences listed in G1.15. This pass additionally resolves the file's open
question Q1 (the vertical SII / ScrollDown bookkeeping).

### G1.1 Caller: window bank and arguments (`DetectFiducialMarkers`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.1a | Selector: `params[0]`; non-zero -> the live function. `Parameters::Initialize` writes `strh 0x0101` at `params+0`, so byte0 = 1. | `0x00898B4C: ldrb r0,[r7]`; `0x00898B4E: cmp r0,#0`; `0x00898B50: beq #0x898c18`; live call `0x00898BE0: blx` PLT `0x4D1360`; `0x008752FC: movw r2,#0x101`; `0x00875304: strh r2,[r0]` | M11-032, M11-024 | EXACT_SOURCE |
| G1.1b | Window bank: `FixedLengthList<int>(N=params+4+2, MS, Buffer)`, then `list[i] = (params+8) << i` for i = 0..(params+4+1). Shipped `params+4 = 1`, `params+8 = 4` -> `{4,8,16}`, size 3. | `0x00898B5A: ldr r6,[r7,#4]`; `0x00898B62: adds r1,r6,#2`; `0x00898B6A: blx` PLT `0x4D05D4`; loop `0x00898B8C: ldr r1,[r7,#8]` / `0x00898B90: lsls r1,r0` / `0x00898B92: str.w r1,[r2,r0,lsl#2]` / `0x00898B9A: cmp r0,r1` / `0x00898B9E: ble #0x898b8c` | M11-032 (numeric part) | EXACT_SOURCE |
| G1.1c | Live call args: `image` (r0=sb), `&list` (r1=sp+0x348), `int = params+0x0C` (r2=r5), `short = params+0x10` (r3), stack short = params+0x12, stack `ConnectedComponents& = sp+0x580` (r4), stack the three MemoryStacks. Shipped: `0xCCCC`, 0, 0. | `0x00898BAA: ldr r5,[r7,#0xc]`; `0x00898BA0: ldrsh r0,[r7,#0x10]`; `0x00898BAC: ldrsh.w r6,[r7,#0x12]`; `0x00898BDA: mov r2,r5`; `0x00898BCC: strd r6,r4,[sp]`; `0x00898BE0: blx` PLT `0x4D1360` | M11-032 | EXACT_SOURCE |

### G1.2 Extractor entry (`0x0088F8BC..0x0088FDF8`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.2a | Entry: r0=image, r1=scale list, r2=mult, r3=short1; after `push.w {r4-r11,lr}` + `sub sp,#0xfc`, stack [sp+0x120]=short2, [sp+0x124]=CC&, [sp+0x128/+0x12c/+0x130]=the three MS. | `0x0088F8BC: push.w {r4-r11,lr}`; `0x0088F8C0: sub sp,#0xfc`; `0x0088F8C2: mov r4,r0`; `0x0088F8CE: mov r7,r1`; `0x0088F8CA: mov sl,r2`; `0x0088F8C8: mov r8,r3`; `0x0088F8DE: ldrd r6,r5,[sp,#0x128]`; `0x0088F8E2: ldr.w fp,[sp,#0x130]` | NEW | EXACT_SOURCE |
| G1.2b | Validity: `AreValid<MemoryStack x3>`, `NotAliased<MemoryStack x3>`, `AreValid<Array<u8>,FixedLengthList<int>,ConnectedComponents>`, scale count in [1,0x40]. Failures log and return 0x4000000 / 0x1000002 / 0x1000003 / 0x3000000. | `0x0088F8F8: blx` PLT `0x4D0CA0`; `0x0088F908: blx` PLT `0x4D0CAC`; `0x0088F91E: blx` PLT `0x4D0CB8`; `0x0088F928: subs r0,r4,#1` / `0x0088F92A: cmp r0,#0x40` / `0x0088F92C: bhs #0x88fb0c` | NEW | EXACT_SOURCE |
| G1.2c | `maxScale = max(list[i]+1)`, starting from -1. Shipped 17. | `0x0088F93C: mov.w r8,#-1`; loop `0x0088F942: ldr r0,[r7,#0x30]` / `0x0088F944: ldr.w r2,[r0,r1,lsl#2]` / `0x0088F94A: adds r2,#1` / `0x0088F94C: cmp r8,r2` / `0x0088F950: movle r8,r2` | NEW | EXACT_SOURCE |
| G1.2d | `ScrollingIntegralImage_u8_s32(this=sp+0xbc, rows=image[+0], cols=image[+4], numBorderPixels=maxScale, MS, Buffer)`. | `0x0088F96A: mov r3,r8`; `0x0088F976: mov r1,r7` (=image rows); `0x0088F974: ldr r2,[sp,#0x20]` (=image cols); `0x0088F978: blx` PLT `0x4D0CC4` | NEW | EXACT_SOURCE |
| G1.2e | Initial `ScrollDown(image, image[+0], MS)`. | `0x0088F98C: mov r2,r7`; `0x0088F990: blx` PLT `0x4D0CD0` | NEW | EXACT_SOURCE |
| G1.2f | N row buffers: `FixedLengthList<Array<u8>>(N, MS1, Buffer)`, then N x `Array<u8>(1, cols)` copied in (element stride 0x14). | `0x0088F9A6: blx` PLT `0x4D0CDC`; alloc loop `0x0088F9E2..0x0088FA3C`; `0x0088F9F0: movs r1,#1`; `0x0088F9F4: mov r2,r6` (=cols); `0x0088F9FA: blx` PLT `0x4CFC8C`; `0x0088FA38: adds r4,#0x14` | NEW | EXACT_SOURCE |
| G1.2g | One mask row `Array<u8>(1, cols)` at sp+0x4c; its data pointer (sp+0x5c) is the `u8*` handed to the callback and to NextRow. | `0x0088FA46: blx` PLT `0x4CFBFC`; `0x0088FA56: blx` PLT `0x4CFC8C`; `0x0088FA5A: ldr r4,[sp,#0x5c]`; `0x0088FA80: mov fp,r4` | NEW | EXACT_SOURCE |
| G1.2h | `ConnectedComponents::Extract2dComponents_PerRow_Initialize(CC, MS1, MS2, MS3)`. | `0x0088FA70: ldrd r0,r1,[sp,#0x124]`; `0x0088FA78: blx` PLT `0x4D0CE8` | M11-032 | EXACT_SOURCE |

### G1.3 Callback selection

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.3 | `N == 3` -> `numFilters3`; `N == 5 && mult == 0x10000` -> `numFilters5_thresholdMultiplier1`; `N == 5 && mult != 0x10000` -> `numFilters5`; otherwise the generic `ecvcs_computeBinaryImage`. Shipped N=3 -> `numFilters3`. | `0x0088FA8A: ldr r0,[sp,#0x1c]`; `0x0088FA8C: cmp r0,#5`; `0x0088FA8E: beq #0x88fb76`; `0x0088FA90: cmp r0,#3`; `0x0088FA92: bne #0x88fb90`; GOT slots `0x0103FF0C` (numFilters3), `0x0103FF08` (_5_thresholdMultiplier1), `0x0103FF04` (_5), `0x0103FF10` (generic) | M11-032 | EXACT_SOURCE |

### G1.4 Row loop

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.4 | For `row = 0 .. image[+0]-1`: `ecvcs_filterRows(sii, list, row, filteredRows)`; call the selected std::function with `(image, filteredRows, row, mult, maskPtr)`; `NextRow(maskPtr, cols, y=row, a=maxScale, b=params+0x12)`; then if `get_maxRow(maxScale-1) <= row` call `ScrollDown(image, image[+0] - 2*maxScale, MS)`. Finally `Finalize()`. | `0x0088FBCE: ldr r0,[sp,#0x18]` / `0x0088FBD0: cmp r6,r0` / `0x0088FBD2: bge #0x88fc6e`; `0x0088FBE4: blx` PLT `0x4D0D00`; `0x0088FBF8: strd r6,fp,[sp]` / `0x0088FC04: blx` PLT `0x4D0D0C`; `0x0088FC28: blx` PLT `0x4D0D18`; `0x0088FC3E: ldr r1,[sp,#0x1c]` / `0x0088FC42: blx` PLT `0x4D0D24` / `0x0088FC48: bgt #0x88fc62` / `0x0088FC5A: blx` PLT `0x4D0CD0`; `0x0088FC80: blx` PLT `0x4D0D30` | M11-032 | EXACT_SOURCE |
| G1.4n | `[sp+0x1c] = maxScale-1` (set at `0x0088FBBA: sub.w r0,r8,#1`), so `get_maxRow` is called with `maxScale-1`, not `maxScale`. `[sp+8] = image[+0] - 2*maxScale` (set at `0x0088F9D8: sub.w r0,r0,r8,lsl#1`). | as cited | NEW detail | EXACT_SOURCE |

### G1.5 `ecvcs_filterRows` (`0x0088F4D0`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.5 | For each window half-width `s` in the list: build `Rectangle<s16> { xmin=-s, xmax=+s, ymin=-s, ymax=+s }` at sp+0xc; look up `mult = T1[s]` at `0xC98958` and `shift = T2[s]` at `0xC98A5C`; call `FilterRow<u8>(sii, rect, row, mult, shift, out[i])`; advance out by 0x14. | `0x0088F4D6: ldr r7,[r1,#0xc]` (count); `0x0088F4F4: ldr r0,[r6],#4`; `0x0088F4FA: strh.w r0,[sp,#0xe]`; `0x0088F4FE: rsbs r1,r0,#0`; `0x0088F500: strh.w r1,[sp,#0xc]`; `0x0088F508: strh.w r1,[sp,#0x10]`; `0x0088F50C: strh.w r0,[sp,#0x12]`; `0x0088F504: ldr.w r3,[fp,r0,lsl#2]` (fp=0xC98958); `0x0088F512: ldr.w r0,[sl,r0,lsl#2]` (sl=0xC98A5C); `0x0088F51C: blx` PLT `0x4D0C7C`; `0x0088F522: add.w r4,r4,#0x14` | M11-032 | EXACT_SOURCE |

### G1.6 `ScrollingIntegralImage_u8_s32::FilterRow<u8>` (`0x0088F538`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.6a | Args: r0=sii, r1=rect, r2=row, r3=mult, [sp+0x40]=shift, [sp+0x44]=out&. `border = [sii+0x20]`, `rowOffset = [sii+0x1c]`, `stride = [sii+8]`, `data = [sii+0x10]`. | `0x0088F54A: ldr r1,[r4,#0x20]`; `0x0088F590: ldr r7,[r4,#0x1c]`; `0x0088F5A0: ldr r0,[r4,#8]`; `0x0088F5A8: ldr r2,[r4,#0x10]`; `0x0088F5F2: ldr r3,[sp,#0x40]`; `0x0088F5F6: str.w fp,[sp,#0x10]` | M11-032 | EXACT_SOURCE |
| G1.6b | Integral-buffer row indices: `top-1 = row - rowOffset + rect.ymin - 1`, `bottom = row - rowOffset + rect.ymax`. Columns: `left = border + rect.xmin - 1`, `right = border + rect.xmax`; the four corner pointers are `data + stride*rowIdx + 4*col`. | `0x0088F596: sub.w r7,r6,r7`; `0x0088F59E: add r3,r7`; `0x0088F5A2: add r5,r7`; `0x0088F5AA: sxtah r6,r1,lr`; `0x0088F5B2: sxtah r1,r1,ip`; `0x0088F5B6: ldr.w fp,[r8,#0x10]`; `0x0088F5C6`/`0x0088F5CA`/`0x0088F5CE`/`0x0088F5D2` | M11-032 | EXACT_SOURCE |
| G1.6c | Leading pad `(1+rect.xmin)-border` clamped to >=0 (0 at shipped scales); `memclr`; `FilterRow_innerLoop<u8>(start, end=W-1, mult, shift, p0,p1,p2,p3, out)`; trailing memclr when `W > end+1` (never at shipped). Returns 0. | `0x0088F558: subs.w sb,r3,r1`; `0x0088F55C: it le` / `0x0088F55E: movle.w sb,#0`; `0x0088F58A: str r0,[sp,#0x14]`; `0x0088F5DE: blx` PLT `0x4AE1E8`; `0x0088F5F6: str.w fp,[sp,#0x10]`; `0x0088F5FA: blx` PLT `0x4D0C94`; `0x0088F602: cmp r0,r1` / `0x0088F604: ble #0x88f612` | M11-032 | EXACT_SOURCE |

### G1.7 `FilterRow_innerLoop<u8>` (`0x0088A5206`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.7 | `out[i] = (p3[i] - p2[i] + p0[i] - p1[i]) * mult >> shift` (arithmetic shift, store low byte). If `mult==1 && shift==0`, writes the raw sum. | `0x008A5230: sub.w r4,r7,r4`; `0x008A5238: add r4,r5`; `0x008A523A: sub.w r4,r4,r6`; `0x008A523E: mul r4,r2,r4`; `0x008A5242: asr.w r4,r4,r3`; `0x008A5246: strb.w r4,[ip,r0]`; identity `0x008A5216: cmpeq r3,#0` / `0x008A521C: beq #0x8a5254` | NEW | EXACT_SOURCE |

### G1.8 `ecvcs_computeBinaryImage_numFilters3` (`0x0088F61A`, live callback)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.8 | Per pixel of the row: `a=f0`, `b=f1`, `c=f2` (filtered rows at data offsets +0x10/+0x24/+0x38); `v = (|b-a| > |c-b|) ? b : c` (strict `>`); `mask = (((v * mult) >> 16) > pixel) ? 1 : 0`, `mult` = the callback's 2nd register int (params+0x0C = 0xCCCC), 32-bit multiply and arithmetic `>>16`. | `0x0088F638: ldr.w lr,[r4,#0x10]`; `0x0088F63C: ldr.w r8,[r4,#0x24]`; `0x0088F640: ldr r4,[r4,#0x38]`; `0x0088F64E: subs r7,r5,r3`; `0x0088F650: subs r3,r6,r5`; `0x0088F652`/`0x0088F656` (abs); `0x0088F65C: cmp r7,r3`; `0x0088F65E: it gt`; `0x0088F660: movgt r6,r5`; `0x0088F662: mul r3,r6,r2`; `0x0088F66A: asrs r3,r3,#0x10`; `0x0088F66C: cmp r3,r5`; `0x0088F672: it gt`; `0x0088F674: movgt r3,#1`; `0x0088F67A: strb r3,[r1],#1` | M11-032 | EXACT_SOURCE |

### G1.9 `numFilters5` / `numFilters5_thresholdMultiplier1` (`0x0088F684` / `0x0088F75E`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.9 | Same largest-adjacent-gap selection over five windows f0..f4 (data offsets +0x10/+0x24/+0x38/+0x4c/+0x60); selected value is `f_{j+1}` for the first index j maximising the gap with preference `g10 >= max(g43,g32,g21)`, then `g21`, then `g32`, else `f4`; binarise `((v*mult)>>16) > pixel`. The `_thresholdMultiplier1` variant (mult==0x10000) is the same selection with `pixel < v`. | gaps `0x0088F6CE..0x0088F70A`; select `0x0088F70C: cmp r2,r7` / `0x0088F712: bge #0x88f726` / `0x0088F71A: add r2,r1,r6` / `0x0088F722: it eq` / `0x0088F724: moveq r2,r7`; binarise `0x0088F73E: muls r1,r2,r1` / `0x0088F744: asrs r1,r1,#0x10` / `0x0088F746: cmp r1,r2` / `0x0088F74E: movgt r1,#1`; t-variant `0x0088F7FC: cmp r1,r0` / `0x0088F802: it lo` / `0x0088F804: movlo r0,#1` / `0x0088F80A: strb r0,[sb],#1`; selector `0x0088FB78: cmp.w r0,#0x10000` | M11-032 | EXACT_SOURCE |

### G1.10 `ScrollingIntegralImage_u8_s32` ctor (`0x0088A4D34`) and layout

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.10 | `Array<int>(rows, cols + 2*numBorderPixels, MS, Buffer)`. Fields: `+0x14 = cols` (get_imageWidth), `+0x18 = -1`, `+0x1c = -numBorderPixels` (get_rowOffset), `+0x20 = numBorderPixels` (get_numBorderPixels). Error path sets `+0/+4/+8 = -1`, `+0x10 = 0`. | `0x008A4D44: add.w r2,r7,r5,lsl#1`; `0x008A4D4E: blx` PLT `0x4D03F4`; `0x008A4D5C: strd r7,r8,[r4,#0x14]`; `0x008A4D60: strd r0,r5,[r4,#0x1c]`; guard `0x008A4D56..0x008A4D6C` | NEW | EXACT_SOURCE |

### G1.11 `ScrollDown` (`0x0088A4DB0..0x0088A4FD8`) — resolves the evidence file's open Q1

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.11a | Guard `1 <= arg2 <= [sii]` (integral row count). Builds a temporary 1 x `[sii+4]` `Array<u8>` (temp data at [sp+0x30]). `H = image[+0]` ([sp+0x18]), `W = [sii+4]`. | `0x008A4DBA: cmp.w sl,#1`; `0x008A4DC2: ldrge.w r6,[sb]`; `0x008A4DC6: cmpge r6,sl`; `0x008A4DF0: ldr.w r4,[sb,#4]`; `0x008A4DF6: str r0,[sp,#0x18]`; `0x008A4DF8: add r0,sp,#0x1c`; `0x008A4E0C: blx` PLT `0x4CFC8C`; `0x008A4E10: ldr.w r8,[sp,#0x30]` | NEW | EXACT_SOURCE |
| G1.11b | **Initial fill** (taken when `[sii] == arg2`, i.e. the first call with arg2=H): `PadImageRow(image, 0, temp)`; write the prefix sum of temp into integral row 0; then for `i=0..border-1` write `row(i+1) = row(i) + cumsum(temp)` (border replications); `arg2 -= border; arg2 -= 1`; then for `r7 = 1..` while `r7 < H` and `arg2 > 0`: `PadImageRow(image, r7, temp)`, `row(fp) = row(fp-1) + cumsum(temp)`, `fp++`, `r7++`, `arg2--`. Stores `[sii+0x18] = r7`. | `0x008A4E22: add r3,sp,#0x20`; `0x008A4E2A: blx` PLT `0x4D1894`; `0x008A4E3E: ldrb r7,[r2],#1` / `0x008A4E44: add r1,r7` / `0x008A4E46: str r1,[r0],#4`; replicate loop `0x008A4E5A..0x008A4E96`; `0x008A4E98: sub.w sl,sl,r1`; `0x008A4EA2: sub.w sl,sl,#1`; main fill `0x008A4F0A..0x008A4F5C`; `0x008A4F74: str r7,[sb,#0x18]` | NEW (resolves ecvcs open Q1) | EXACT_SOURCE |
| G1.11c | **Scroll fill** (taken when `[sii] != arg2`, i.e. later calls with arg2 = H-2*border): `rowOffset += arg2`; copy rows `arg2 .. [sii]-1` up to rows `0 .. [sii]-arg2-1` (`memcpy4` of one stride each); then fill rows `[sii]-arg2 .. [sii]-1` from source rows `r7+1..` while `r7 < H` and `arg2 > 0`; if source rows run out, keep using the last row (`r7 = H-1`). Stores `[sii+0x18] = r7`. | `0x008A4EB0: ldr.w r7,[sb,#0x18]`; `0x008A4EB4: sub.w fp,r6,sl`; `0x008A4EC0..0x008A4ED4: __aeabi_memcpy4`; `0x008A4EDE: ldr.w r0,[sb,#0x1c]` / `0x008A4EE2: add r0,sl` / `0x008A4EE4: str.w r0,[sb,#0x1c]`; fill `0x008A4F0A..0x008A4F5C`; edge reuse `0x008A4F60`/`0x008A4F64: subs r7,r0,#1`; `0x008A4F70: cmp.w sl,#1` / `0x008A4F74: str.w r7,[sb,#0x18]` | NEW (resolves ecvcs open Q1) | EXACT_SOURCE |

### G1.12 `get_maxRow` (`0x0088A51E0`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.12 | `get_maxRow(arg)`: let `H=[sii]`, `L=[sii+0x18]` (last source row filled), `O=[sii+0x1c]` (rowOffset). Return `L - arg`; but if `(H-1) > (L - O)` return `H-1 + O - arg`. With the main loop's `arg = maxScale-1` this is the condition that triggers the next `ScrollDown`. | `0x008A51E0: ldr.w ip,[r0]`; `0x008A51E4: ldrd r3,r2,[r0,#0x18]`; `0x008A51E8: subs r0,r3,r1`; `0x008A51EA: subs r2,r0,r2`; `0x008A51EC: add r1,r2`; `0x008A51EE: sub.w r2,ip,#1`; `0x008A51F2: subs r1,r2,r1`; `0x008A51F4: it gt`; `0x008A51F6: addgt r0,r0,r1` | NEW (resolves ecvcs open Q1) | EXACT_SOURCE |

### G1.13 `PadImageRow` / `ComputeIntegralImageRow` (`0x0088A5058` / `0x0088A50C8`, `0x0088A50E2`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.13 | `PadImageRow(image, row, out)`: `src = image.data + image.stride*row`; `out[0..border-1] = src[0]`; copy `floor(cols/4)` words to `out+border`; `out[border+cols .. border+cols+border-1] = src[cols-1]`. (The `cols % 4` tail is not copied by the word loop.) `ComputeIntegralImageRow(ptr, out, n)` is a running prefix sum; the 4-arg overload adds a previous row's sums. | `0x008A505C: ldrd ip,r6,[r1,#4]`; `0x008A5062: mla r1,r6,r2,r7`; `0x008A506C: asr.w r4,ip,#2`; `0x008A5078: ldrb lr,[r5,#-0x1]`; left fill `0x008A5084: strb r7,[r3,r2]`; word copy `0x008A509C: ldr r6,[r1],#4` / `0x008A50A2: str r6,[r5],#4`; right fill `0x008A50B6: strb.w lr,[r1,r2]`; prefix `0x008A50D2: ldrb r3,[r0],#1` / `0x008A50D8: add ip,r3` / `0x008A50DA: str ip,[r1],#4`; 4-arg `0x008A50EE..0x008A50FC` | NEW | EXACT_SOURCE (tail bytes only copied when cols%4==0; shipped camera width is a multiple of 4) |

### G1.14 Per-row DP: `Extract2dComponents_PerRow_*` and `Extract1dComponents`

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.14a | Dispatch thunk: object byte 0 -> i32 template at `+0x11c`, else u16 template at `+4`. `DetectFiducialMarkers` sets the byte from `params+0x40 < 0x10000` (shipped 39000 -> u16). | `0x00893BAA: ldrb.w ip,[r0]` / `0x00893BAE: cmp.w ip,#0` / `0x00893BB4: addeq.w r0,r0,#0x11c` / `0x00893BBC: adds r0,#4`; `0x0089886E: cmp.w r1,#0x10000` / `0x00898878: movlt r3,#1`; `0x0089351A: strb r7,[r8]` | NEW | EXACT_SOURCE |
| G1.14b | `Extract1dComponents(maskRow, width, a=maxScale, b=params+0x12=0, list&)`: scans runs of 1s; for each run `[start, end]` (end = last 1 pixel), if `(end-start+1) >= a` and the `b` test passes, append `{s16 start, s16 end, u16 0xffff, u16 0xffff}`. At shipped `b=0` the `b` test (`(s16)(r6+1) <= b`) never drops a run. | `0x00896FDC: strd r2,r3,[sp]`; run start `0x0089702E..0x00897038`; end test `0x00897040: add.w r8,r6,#1` / `0x00897046: sxth.w r6,r8` / `0x0089704A: cmp r6,r4` / `0x0089704C: ble #0x8970a0`; length test `0x00897054: sub.w r4,sl,sl,lsl#1` / `0x0089705C: add.w r4,r7,r4,lsl#16` / `0x00897060: asrs r4,r4,#0x10` / `0x00897062: cmp r4,r3` / `0x00897064: blt #0x8970a4`; append `0x00897086: str.w r4,[r2,lr,lsl#3]` / `0x00897096: str r4,[r2,#4]`; flush `0x008970BA..0x008970F4` | NEW | EXACT_SOURCE (b semantics = no-op at 0; see G1.15.3) |
| G1.14c | `NextRow` merges runs across rows: for each current run, walk the previous row's segments, union-find (set all to min) their component ids in the u16 parent array, write the merged segment into the current-row list and append to the output accumulator; a non-overlapping run gets a new id; finally swap the two row lists. | `0x00893F96: blx` PLT `0x4D0FF4`; overlap `0x00893FF0: ldrsh.w r5,[fp,r3,lsl#3]`; union `0x00894024: ldrh.w r0,[r7,ip,lsl#1]` / `0x00894042: strh.w r4,[r7,r5,lsl#1]` / `0x00894046`/`0x0089404A`; append `0x0089406A: str.w r4,[r6,r5,lsl#3]` / `0x00894078: str r0,[r6,#4]`; new id `0x008940AE: ldrh.w r0,[r5,#0x10c]`; `0x008940FC: blx` PLT `0x4D1000` (Swap) | NEW | EXACT_SOURCE |
| G1.14d | Segment format: 8 bytes `{ s16 start@+0, s16 end@+2, u16 row@+4, u16 componentId@+6 }`; the output accumulator is at CC+4 template base `{ size@+0xc, capacity@+0x1c, data@+0x30 }`. Finalize resolves ids to roots and sets max id. | `0x00894062: pkhbt r4,sl,lr,lsl#0x10`; `0x00894074: orr.w r0,r0,ip,lsl#0x10`; Finalize `0x00894426: ldrh r7,[r3]` / `0x0089442C: ldrh.w r7,[r2,r7,lsl#1]` / `0x00894430: strh r7,[r3],#8` | NEW | EXACT_SOURCE |

### G1.15 Does `re-analysis/evidence/m11/ecvcs-extractor.md` match the `.so`?

**Yes, instruction for instruction, with the following divergences (all minor).**

1. **L13 / open Q1.** The file writes "if `get_maxRow(maxScale) <= row` then `ScrollDown`". The actual argument is `maxScale-1` (`0x0088FBBA: sub.w r0,r8,#1; str r0,[sp,#0x1c]`; `0x0088FC3E: ldr r1,[sp,#0x1c]`). The condition and the scroll amount (`image[+0] - 2*maxScale`) are otherwise correct. This pass now also reads `ScrollDown` and `get_maxRow` (G1.11/G1.12), so the file's **open Q1 is closed**: the input `Array<u8>` is not pre-padded; the SII pads each row by `numBorderPixels` at both ends and the integral buffer row index of source row `r` is `r - rowOffset` (with `rowOffset` starting at `-border` and incremented by `H-2*border` per scroll); `get_maxRow(arg) = min(H-1+O-arg, L-arg)` in the sense of G1.12.
2. **L7.** The file gives `+0x14`, `+0x1c`, `+0x20`; it omits `+0x18 = -1` (set by the same ctor, `0x008A4D5C`). `+0x18` is the "last source row filled" that `ScrollDown` updates and `get_maxRow` reads.
3. **Open Q2 (the `Extract1dComponents` `b` test).** Confirmed: the test is `(s16)(r6+1) <= b` where `r6` is the run-length accumulator that is 0 at a run start and 1 at the end branch, so at shipped `b=0` it is a no-op; the parameter's intended meaning is still not recovered. The `a` test is `(end-start+1) >= a`.
4. **Tie-break.** Confirmed per function: `numFilters3` selects `f1` only on strict `>` (`0x0088F65E: it gt`), while the five-filter variants use `>=`. Behaviour-changing only on exact ties.
5. **D8 remap tail.** `CompressConnectedComponentSegmentIds` (u16 body `0x00894A7C`) was not re-read to its end this pass; the file itself says the remap tail `0x00894AF4..` is not read. Not needed for the live path (the id compression is read at `0x00898C90..0x00898D50` by X1 V11).
6. **Minor wording.** The file's L2 says "i = 0 .. params+0x04+1"; the loop stores indices 0..params+4 inclusive, i.e. size `params+4+2`. Correct, but easy to misread.

**Not contradicted.** The file's item 1 (0xCCCC is live, not binomial-only) is correct and agrees with X1. Its `+0x40` u16 selector, segment format and D1 thunk are all confirmed.

---

## G2. The `MarkerDetector` post-processing (M11-022)

`MarkerDetector::Detect(Image const&, list<ObservedMarker>&)` at `0x008753B6..0x008759CC`.
`DetectFiducialMarkers` is the Thumb function at `0x00898760`; `0x004CFCEC` in the X1/lead
reports is its **PLT stub**, not the body. `GetROI`, `GetNegative` and `InitFromPointContainer`
in the X1 report are likewise PLT stubs; the bodies are named below.

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G2.1 | Entry: `ResetBuffers(width, height, [camera+0x6c])`; wrap the `Image` as `Array<u8>` with dims `[image+0xc] x [image+0x10]` and `MemoryStack = detectorMemory+0x40`. | `0x008753C6: ldr r3,[r1,#0x6c]`; `0x008753C8: blx` PLT `0x4CFC80`; `0x008753D4: ldrd r4,r5,[r8,#0xc]`; `0x008753E4: add.w r3,r6,#0x40`; `0x008753F0: blx` PLT `0x4CFC8C` (Array<u8> ctor) | M11-022 | EXACT_SOURCE |
| G2.2 | ROI / negative choice: build a `std::list<bool>` from `[camera+0x7c]` — 0 -> `{false}`, 1 -> `{true}`, 2 -> `{false, true}`, other -> `{}` (empty => Detect returns 0 without running the detector). | `0x00875402: ldrb.w r0,[r0,#0x7c]`; `0x00875406: cbz r0,#0x87543e`; `0x00875408: cmp r0,#1` / `0x0087540a: beq #0x87544c`; `0x0087540c: cmp r0,#2` / `0x0087540e: bne #0x875468`; `0x00875410..0x875462` (node construction); `0x0087546a..0x875470` (`cmp r6,r4`; empty list -> `0x87549c` cleanup/return) | M11-022 | EXACT_SOURCE |
| G2.3 | Per pass: construct a local `Image`; if the ROI vector is non-empty, `CopyTo` the source into it and for each `Rectangle<int>` `GetROI` (`ImageBase<u8>::GetROI<Image>` body `0x00658A92`/`0x006BA380`) and `FillWith(0xff)` the region; otherwise assign the source image. | `0x0087556C: ldrb r5,[r4,#8]`; `0x00875570: blx` PLT `0x4AC490` (Image::Image); `0x0087557E: blx` PLT `0x4A5A28` (CopyTo); `0x008755A2: blx` PLT `0x4BF048` (GetROI); `0x008755AA: blx` PLT `0x4A5E18` (FillWith 0xff); `0x0087560A: blx` PLT `0x4A5A1C` (operator=) | M11-022 | EXACT_SOURCE |
| G2.4 | Negative choice: if the pass bool is false use the image as-is; if true, `Image::GetNegative` (body `0x00871DCC`) into a temp and copy it into the `Array<u8>` via the local helper `0x00875A64`; if false, copy the image directly via `0x00875A64`. | `0x00875612: cbz r5,#0x87567c`; `0x0087561A: blx` PLT `0x4CFCC8` (GetNegative); `0x00875622: bl 0x875a64`; `0x0087567C: add r0,sp,#0x10c` / `0x00875680: bl 0x875a64`; helper `0x00875A64: ldrd r3,lr,[r0,#0xc]` / `0x00875A6C: ldrd r4,ip,[r1]` / `0x00875AD2: ldr r3,[r0,#0x14]` / `0x00875AE2: b.w 0x8cca1c` (bulk copy) | M11-022 | EXACT_SOURCE |
| G2.5 | Before the detector call, initialise each of `[memory+0x94]` VisionMarker slots' 3x3 float block at `marker+0x2c..0x3c` from a fresh `Array<float>(3,3)`, then call `DetectFiducialMarkers(array, markerList, params, MS x3)`. | `0x00875690: ldr.w r4,[r8,#0x94]`; `0x00875694: str.w r4,[r8,#0x84]`; `0x008756B8: movs r1,#3` / `0x008756BC: movs r2,#3` / `0x008756BE: blx` PLT `0x4CFCD4` (Array<float> 3x3); `0x008756D2: str r1,[r0,#0x2c]`..`0x008756E2: str r1,[r0,#0x3c]`; `0x008757B2: blx` PLT `0x4CFCEC` (DetectFiducialMarkers) | M11-022 | EXACT_SOURCE |
| G2.6 | On success, `count = [memory+0x84]`, marker array data = `[memory+0xa8]`, stride 0x40. For each marker: build `Quadrilateral<float>` from the four `Point<float>` at `marker+0..+0x1f`; if the bool-list size >= 2, zero a `Rectangle<int>` and `Rectangle<int>::InitFromPointContainer<Quadrilateral<float>>` (body `0x0087855C`) on the quad, then push it to the ROI vector. | `0x008754C0: ldr r8,[r0,#0x84]`; `0x008754E4: ldr r0,[r0,#0xa8]`; `0x008754EA..0x00875502` (four points); `0x00875516: blx` PLT `0x4A48D0` (Quadrilateral ctor); `0x0087551A: ldr r0,[sp,#0x160]` / `0x0087551C: cmp r0,#2` / `0x0087551E: blo #0x875546`; `0x00875532: blx` PLT `0x4BEBBC` (InitFromPointContainer) | M11-022 | EXACT_SOURCE |
| G2.7 | Append the `ObservedMarker`: `list<ObservedMarker>::emplace_back<unsigned int, MarkerType const&, Quad&, Camera const&>`; the first u32 is `[ImageBase+0x3c]` (the image id/timestamp field copied by `ImageBase<u8>::operator=` at `0x0086EF2C/0x0086EF30`), the `short` is the marker code's low 16 bits at `marker+0x20`, the quad is `sp+0x40`, the camera is `[detector]`. The `ObservedMarker` ctor (`0x0087E1FC`) stores code@+0, timestamp@+4, quad@+8..+0x27, camera@+0x28, -1@+0x4c. | `0x00875546: ldr r0,[sp,#0x20]` / `0x00875548: ldr r0,[r0,#0x3c]`; `0x0087554E: ldr r0,[r0]` ([detector]); `0x00875552: add r2,r5,#0x20`; `0x0087555C: blx` PLT `0x4CFCBC`; ctor `0x0087E200: ldrh r0,[r2]` / `0x0087E202: str r1,[r6,#4]` / `0x0087E204: strh r0,[r6]`; `0x0087E224: str r0,[r6,#0x4c]` | M11-022 | EXACT_SOURCE |

Notes / residual uncertainty:
- `IsQuadrilateralReasonable` is inside `DetectFiducialMarkers` (`0x00892B18`, X1 V14), not in `Detect`; `Detect` only consumes the already-accepted `VisionMarker` list.
- The `Rectangle<int>` ROI vector is initialised once before the bool loop (`0x0087546E/0x00875470`) and accumulates across passes; the first (non-negative) pass populates it, the second (negative) pass masks those regions with 0xff. This is the "ROI/negative choice" mechanism.
- `InitFromPointContainer` body `0x0087855C` is the float rectangle version; the calls use the `Quadrilateral<float>` overload (PLT `0x4BEBBC`). The exact min/max update is straightforward and cited; I did not transcribe every `vcmpe`.

---

## G3. Sub-pixel corner refinement: `RefineQuadrilateral` (`0x008C55E0..0x008C656C`)

This pass read the function body and the helper at `0x008C66C4`. The function is large
(~0x0F8C bytes) and NEON-heavy; I transcribe the behaviour-changing steps and constants, and
mark the sub-parts I could not decode exactly as RECOVERABLE_GAP with the exact address to read.

Signature (mangled): `RefineQuadrilateral(Quadrilateral<float> const& quad, Array<float> const&,
Array<u8> const& image, Point<float> const&, Point<float> const&, int numSamples, float, float,
int maxIter, float stopThresh, float, Quadrilateral<float>& outQuad, Array<float>&
outHomography, MemoryStack)`.
The stack-argument map is at the prologue: `[sp+0x390]=numSamples`, `[sp+0x384]=maxIter`,
`[sp+0x380]=image`, `[sp+0x388]=?`, `[sp+0x38c]=?`, `[sp+0x394]=?`, `[sp+0x398]=stopThresh`,
`[sp+0x39c]=outQuad`, `[sp+0x3a0]=outHomography`, `[sp+0x3a4]=MS` (read at
`0x008C55FE`/`0x008C56B8`/`0x008C56BC`/`0x008C56BE`/`0x008C6358`).

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G3.1 | Guards: `AreEqualSize(3,3, initialHomography, refinedHomography)` and `NotAliased`. | `0x008C5600: movs r0,#3` / `0x008C5602: movs r1,#3` / `0x008C5608: blx` PLT `0x4D229C`; `0x008C5616: blx` PLT `0x4D1570` | M11-005 | EXACT_SOURCE |
| G3.2 | Input quad geometry: edge vectors `d0=(x0-x3, y0-y3)`, `d1=(x1-x2, y1-y2)`; the longer edge (`s16 = sqrt(d0.d0)`, `s0 = sqrt(d1.d1)`) selects the sample direction; `scale = s20 / 1.4142135` (1/sqrt2) and a spacing factor. | `0x008C5620..0x008C56B6`; `0x008C5838: vldr s4,[pc,#0x3d8]` (=255.0 at `0x8C5C14`); `0x008C5840: vldr s6,[pc,#0x3d4]` (=1.4142135 at `0x8C5C18`); `0x008C584E: vdiv.f32 s0,s0,s4`; `0x008C5852: vdiv.f32 s4,s20,s6`; `0x008C585A/0x008C585E: vmul` | M11-005 | EXACT_SOURCE for the constants; the geometric derivation is partially read |
| G3.3 | Sample count per block: `r6 = ceil(numSamples * 0.125)` (`0x008C5712: vmov.f32 s0,#0.125`; `0x008C571A: vmul`; `0x008C5722: blx ceilf`); eight blocks per pass, total `r5 = r6*8`. Allocates five `Array<float>(1, r5)`: xSquare, ySquare, Tx, Ty, and one more. | `0x008C5712/0x008C571A/0x008C5722`; `0x008C572A: ldr r1,[sp,#0x2dc]`; `0x008C573C: lsls r5,r6,#3`; `0x008C5740: blx` PLT `0x4CFCD4`; four more at `0x008C575C`/`0x008C5778`/`0x008C5794` | M11-005 | EXACT_SOURCE |
| G3.4 | **Sample generation** (`0x008C5E80..0x008C61E0`): for each of the `r5` samples, two parameter arrays (`[sp+0x5c]`, `[sp+0x50]`) give `(u,v)`; the sample is projected through the 3x3 initial homography (`[sp+0x3a0]`) into image coordinates: `X = (h0*u + h1*v + h2)/(h6*u + h7*v + h8)`, `Y = (h3*u + h4*v + h5)/(h6*u + h7*v + h8)`; `floorf`/`ceilf` bound the pixel and a bilinear value is read. | load homography `0x008C5EDA..0x008C5F00`; project `0x008C5F46: vmul.f32 s4,s19,s0` .. `0x008C5F7E: vadd.f32 s0,s23,s0`; `0x008C5F76: vdiv.f32 s4,s18,s4`; `0x008C5F82/0x008C5F86: vmul`; `0x008C5F90: blx floorf` / `0x008C5F9C: blx floorf` / `0x008C5FA4: blx ceilf` / `0x008C5FBE: blx ceilf`; bounds `0x008C5FB4`..`0x008C5FF2`; fractional parts `0x008C6022: vsub.f32 s17,s17,s30` / `0x008C6026: vsub.f32 s22,s22,s28`; bilinear pixels `0x008C60B6..0x008C60DC`; bilinear value `0x008C60E6..0x008C610A`; residual vs `[sp+0x40]` `0x008C6112: vsub.f32 s0,s0,s2`; `0x008C6116: vmul.f32 s0,s0,s2` (=1/255 at `0x8C6474` = 0.003921569) | M11-005 | RECOVERABLE_GAP: the pixel indexing `mla r0,r1,r5,r2` / `mla r1,r1,r7,r2` and the exact bilinear weight pairing at `0x008C60B6..0x008C610A` are read but not fully decoded; read `0x008C606A..0x008C610A` against the image `Array<u8>` layout (`+0x44` base, `+0x48` stride, from `0x008C609A: ldrd r2,r1,[sp,#0x44]`) |
| G3.5 | **Normal equations** (`0x008C611E..0x008C6188`): for each sample, an 8-element design vector at `[sp+0x25c]` (indexed by `r1 = 0..7`; element pointers are `[sp+0x25c][j]` offset by `sl*4`) is accumulated into an 8x9 matrix `A` at `[sp+0xf8]` (row stride 0x24 = 9 floats) and an 8-vector `b` at `[sp+0xd8]`: `A[j][k] += d[j]*d[k]`, `b[j] += residual*d[j]`. | `0x008C611E: ldr.w r2,[r4,r1,lsl#2]`; `0x008C6130: add.w r2,ip,r1,lsl#5`; `0x008C6134: add.w r2,r2,r1,lsl#2`; `0x008C6138: vmul.f32 s4,s2,s2`; `0x008C6144: vstr s4,[r2]`; inner `0x008C614C..0x008C616C`; rhs `0x008C616E: vmul.f32 s2,s0,s2` / `0x008C6182: vstr s2,[r1]` | M11-005 | RECOVERABLE_GAP: the contents of the 8-element design vector (`[sp+0x25c]`) are not decoded; read `0x008C5C3E..0x008C5E80` (where `sp+0x25c` is filled) |
| G3.6 | `Matrix::MakeSymmetric(A, false)` (`0x008C61E0`) then `Matrix::SolveLeastSquaresWithCholesky(A, b, false, converged)` (`0x008C61F4`); if the result is non-zero, return it. | `0x008C61E0: blx` PLT `0x4D0B50`; `0x008C61F4: blx` PLT `0x4D0B5C`; `0x008C61FA: cmp.w sb,#0` / `0x008C61FE: bne.w #0x8c5bf4` | M11-005 | EXACT_SOURCE (call sites); the Cholesky body `0x0088DE68` and `MakeSymmetric` `0x0088DDC0` were not re-read this pass |
| G3.7 | **Update composition `H = H * inv(U)`** (`0x008C620A..0x008C629A`): build the 3x3 `U` from the 8-vector solution `s[0..7]`: `U = [[s0+1, s1, s2],[s3, s4+1, s5],[s6, s7, 1]]`; `Invert3x3(U)` in place; `Matrix::Multiply(initialHomography, U, newHomography)`. | `0x008C620A: vldr s0,[r0]` / `0x008C620E: vadd.f32 s0,s0,s18` (=+1.0) / `0x008C6212: vstr s0,[r1]`; `0x008C6218`/`0x008C621E`/`0x008C6222`/`0x008C622A: vadd.f32 s0,s0,s18`/`0x008C623E`/`0x008C6246`/`0x008C6252`/`0x008C625E..0x008C6268: mov.w r1,#0x3f800000; str r1,[r0,#8]`; `0x008C6290: blx` PLT `0x4D22A8` (Invert3x3); `0x008C629A: blx` PLT `0x4D2200` (Multiply) | M11-005 | EXACT_SOURCE |
| G3.8 | **H22 renormalisation** (`0x008C62A0..0x008C634A`): if `|H22 - 1| >= 1e-5` (literal at `0x8C6504`), divide the 3x3 by H22 via `Matrix::Elementwise::ApplyOperation<DotDivide>` (`0x008C634A`). `[sp+0x60]` records whether the solve reported converged (`0x008C62CA..0x008C62D4`). | `0x008C62AA: vldr s0,[r2,#8]`; `0x008C62B0: vadd.f32 s0,s0,s2` (s2=-1); `0x008C62BC: vneg.f32 s2,s0`; `0x008C62C0: it mi` / `0x008C62C2: vmovmi.f32 s0,s2`; `0x008C62C6: vldr s2,[pc,#0x23c]`; `0x008C62D0: vcmpe.f32 s0,s2` / `0x008C62DA: bmi #0x8c634e`; `0x008C634A: blx` PLT `0x4D22B4` | M11-005 | EXACT_SOURCE (threshold); RECOVERABLE_GAP for the exact DotDivide operand setup `0x008C62DC..0x008C6348` (ArraySlice plumbing) |
| G3.9 | Write `newHomography` back into `refinedHomography` (`Array<float>::SetCast`, `0x008C6354`); call the helper `0x008C66C4` on `(refinedHomography, quad)` to get the residual `s16`. | `0x008C6354: blx` PLT `0x4D0AE4`; `0x008C6358: ldr r1,[sp,#0x39c]`; `0x008C635C: bl 0x8c66c4`; `0x008C6362: vmov.f32 s16,s0` | M11-005 | EXACT_SOURCE |
| G3.10 | **Stopping test** (`0x008C636C..0x008C6388`): stop if `residual < [sp+0x398]` (`0x008C6370: vcmpe.f32 s16,s0` / `0x008C6378: bmi #0x8c63e4`); else increment the iteration count and stop if it reaches `[sp+0x384]` (`0x008C637E..0x008C6388`). If `r6` (the solve-converged flag) is zero, loop back to `0x008C5EC0`; otherwise finish. | as cited | M11-005 | EXACT_SOURCE |
| G3.11 | Finalise (`0x008C63E4..0x008C6470`): copy the original quad into a temp; if `[sp+0x60] == 1`, write the temp to `[sp+0x39c]` (out quad) and `SetCast` the refined homography into `fp`; else call the helper again and set the success flag from `residual > [sp+0x394]`. Returns `sb`. | `0x008C63F2..0x008C6416`; `0x008C6418: ldr r0,[sp,#0x60]` / `0x008C641C: bne #0x8c644a`; `0x008C643C: blx` PLT `0x4D0AE4`; `0x008C644A`/`0x008C644E: bl 0x8c66c4`; `0x008C6452: vldr s2,[sp,#0x394]` / `0x008C645A: vcmpe.f32 s0,s2` / `0x008C6464: movgt.w sb,#1` | M11-005 | EXACT_SOURCE |
| G3.12 | Helper `0x008C66C4(Array<float> const& homography, Quadrilateral<float> const& quad)`: clear a 32-byte temp; copy the 4 quad points; transform each corner by the 3x3 homography (`X=(h0*x+h1*y+h2)/(h6*x+h7*y+h8)`, `Y=(h3*x+h4*y+h5)/(...)`); compute `sqrt((X-xt)^2+(Y-yt)^2)` for each corner and return the maximum (floor 0). | `0x008C66C4` prologue; `0x008C66D2..0x008C6720`; division `0x008C6742: vdiv.f32 s8,s2,s30`; `0x008C6786/0x008C678A: vmul`; distances `0x008C67AE: vsub.f32 s0,s8,s0` / `0x008C67B6: vmul.f32 s0,s0,s0` / `0x008C67C2: vsqrt.f32 s0,s2`; max `0x008C6834: vcmpe.f32 s0,s16` / `0x008C6840: vmovgt.f32 s16,s0`; return `0x008C6848: vmov.f32 s0,s16` | M11-005 | EXACT_SOURCE |
| G3.13 | `ComputeBrightDarkValues` contrast gate (`0x0089F8E8`; tail `0x0089FD00..0x0089FD3A`): the two returned means `s0` (out at `[r0]`) and `s2` (out at `[r1]`) are normalised (`s4 = s29/s4`, `s2 *= s4`, `s0 *= s4`), then the bool result is `s0 > ratio * s2` with `ratio = [r7-0x78]` (Parameters+0x3C = 1.01). | `0x0089FD04: vdiv.f32 s4,s29,s4`; `0x0089FD08: vmul.f32 s2,s4,s2`; `0x0089FD0C: vmul.f32 s0,s4,s0`; `0x0089FD10: vldr s4,[r7,#-0x78]`; `0x0089FD14: vmul.f32 s4,s2,s4`; `0x0089FD18/0x0089FD1C: vstr`; `0x0089FD26: vcmpe.f32 s0,s4`; `0x0089FD2E: it gt` / `0x0089FD30: movgt r0,#1`; `0x0089FD3A: strb.w r0,[r8]` | M11-005 | EXACT_SOURCE |
| G3.14 | Constants read: `255.0` (`0x8C5C14`), `1.4142135` (`0x8C5C18`), `0.125` (sample block divisor), `1/255 = 0.003921569` (`0x8C6474`), `1e-5 = 9.9999997e-6` (`0x8C6504`), `0.0` (`0x8C6854`). | `0x008C5838`/`0x008C5840`/`0x008C5712`/`0x008C6116`/`0x008C62C6`/`0x008C67E0`; data at the cited addresses | M11-005 | EXACT_SOURCE |

**G3 verdict.** The orchestration, update composition, H22 gate, stopping test, helper and
contrast gate are now transcribed. The two pieces still not fully decoded are (a) the exact
bilinear pixel weighting in `0x008C60B6..0x008C610A` and (b) the contents of the 8-element
design vector filled at `0x008C5C3E..0x008C5E80` (the `[sp+0x25c]` array) and the exact
`DotDivide` operand plumbing at `0x008C62DC..0x008C6348`. These are RECOVERABLE_GAP; read those
three ranges. Until then M11-005 cannot be more than EXACT_SOURCE for orchestration and
RECOVERABLE_GAP for the numerics.

---

## G4. M11-003 evidence — native addresses and instructions

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G4.1 | `Robot::_kDefaultHeadCamRotation` is a 36-byte `RotationMatrix3d` in `.bss` at `0x01059CF8` (GOT slot `0x103E958`, `ARM_GLOB_DAT`). It is initialised by the static ctor at `0x004D6DA4..0x004D6DB6` from the 9-float initializer list at `0x00C4A854` = `[0, -0.0698, 0.9976, -1, 0, 0, 0, -0.9976, -0.0698]`. It is read in `Robot::Robot` at `0x0050FFAE/0x0050FFB0` and passed as the rotation to `Pose3d::Pose3d` at `0x0050FFBA`. | data `0x01059CF8`; init `0x004D6DAC: add r0,pc` (GOT `0x103E958`) / `0x004D6DB2: blx` PLT `0x4A4930` (`RotationMatrix3d::RotationMatrix3d(std::initializer_list<float>)`); list `0x00C4A854`; read `0x0050FFAE: add r0,pc` / `0x0050FFB0: ldr r1,[r0]` / `0x0050FFBA: blx` PLT `0x4A6C40` | M11-003 | EXACT_SOURCE (address + value) |
| G4.2 | `Robot::GetCameraPose(float) const` at `0x00510FFC`: copy the head-cam pose from `[this+0x2d8]`, negate the angle (`0x00511016: eor r1,r5,#0x80000000`), build a `RotationVector3d(Radians, Y_AXIS_3D())`, apply `Transform3d::RotateBy`, set the pose name (6 chars at `0x00511042`). | `0x00510FFC` entry; `0x00511000: add.w r1,r1,#0x2d8`; `0x00511016: eor r1,r5,#0x80000000`; `0x00511020: blx` PLT `0x4A47B0` (`Y_AXIS_3D`); `0x0051102A: blx` PLT `0x4A47BC`; `0x00511036: blx` PLT `0x4A47EC` (`RotateBy`); `0x00511050: blx` PLT `0x4A4C24` (`SetName`) | M11-003 | EXACT_SOURCE |
| G4.3 | `Block::LookupBlockInfo(ObjectType)` at `0x004E4C8C`: a guarded function-local static table of block infos; builds entries (e.g. `"LIGHTCUBE1"` at `0x004E4CC4`, `NamedColors::ORANGE` at `0x004E4CDA`) and returns the lookup. | `0x004E4C8C` entry; `0x004E4CAE: blx` PLT `0x4A472C` (`__cxa_guard_acquire`); `0x004E4CC4: addw r1,pc,#0x64c` (`"LIGHTCUBE1"`); `0x004E4CDA: add r0,pc` (GOT `0x103E7D8` `NamedColors::ORANGE`) | M11-003 | EXACT_SOURCE (entry + table construction); the full table body was not transcribed |
| G4.4 | `Block::AddFace(FaceName, MarkerType const&, float, u8, u8)` at `0x004E53BC`: build a `Pose3d` from a string, query the block's size via the vtable slot `[vptr+0x18]`, scale by 0.5 (`0x004E5420: vmov.f32 s0,#0.5`), then a `tbb` switch on `FaceName` (0..5) computes the face placement. | `0x004E53BC` entry; `0x004E53E8: blx` PLT `0x4A40C0` (`Pose3d::Pose3d(string)`); `0x004E53FC: ldr r1,[r0,#0x18]` / `0x004E5400: blx r1` (vtable size getter); `0x004E5420: vmov.f32 s0,#5.000000e-01`; `0x004E5438: tbb [pc,r4]` | M11-003 | EXACT_SOURCE (entry + dispatch); the per-face bodies were not transcribed |
| G4.5 | `KnownMarker::_canonicalCorners3d` is a 48-byte `Quadrilateral<3,float>` in `.bss` at `0x0105E0F0` (GOT slot `0x103FEF8`). It is initialised by the static ctor at `0x004DD7D8..0x004DD81A` with the four points `(-0.5,0,-0.5)`, `(-0.5,0,0.5)`, `(0.5,0,0.5)`, `(0.5,0,-0.5)` (constants `0xBF000000` = -0.5, `0x3F000000` = +0.5, 0). Read by `KnownMarker` at `0x0087E2F2`. | data `0x0105E0F0`; init `0x004DD7E8: add r0,pc` (GOT `0x103FEF8`) / `0x004DD814: blx` PLT `0x4A4AC8` (`Quadrilateral<3,float>` ctor); point constants `0x004DD7E0: mov.w ip,#-0x41000000` / `0x004DD7E4: mov.w r3,#0x3f000000`; read `0x0087E2F2: add r0,pc` / `0x0087E2F4: ldr.w ip,[r0]` | M11-003 | EXACT_SOURCE (address + value) |

Note: both `.bss` constants are actually initialised by `init_array` static constructors inside the
shipped `.so` (`init_array` entries `0x004D6DA5` and `0x004DD7D9`); the earlier X1 note that the
bare symbol names prove nothing was correct, and the values above are the missing evidence.

---

## G5. M11-004 evidence — position-update thresholds

The strategy is `ReactionTriggerStrategyPositionUpdate`; its constructor
`0x00612168` writes the three fields, and its two predicate functions read them.

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G5.1 | **45 degrees.** Ctor writes `0x3F490FDB` = 0.785398 rad to `[this+0x2c]` via `Radians::Radians`. | `0x00612182: movw r1,#0xfdb`; `0x00612186: movt r1,#0x3f49`; `0x0061218A: blx` PLT `0x4A4294` (`Radians::Radians`) with r0 = `this+0x2c` (`0x0061217E: str r1,[r0],#0x2c`) | M11-004 | EXACT_SOURCE |
| G5.1u | Used as the rotation tolerance of `Pose3d::IsSameAs`: `r3 = [this+0x2c]`; the return is inverted (`eor r4,r0,#1`) so `ShouldReactToTarget_poseHelper` is true when the target has moved more than 45 deg (or more than 80 mm). | `0x006124E4: add.w r3,r5,#0x2c`; `0x006124F2: blx` PLT `0x4A7060` (`Pose3d::IsSameAs`); `0x006124F6: eor r4,r0,#1` | M11-004 | EXACT_SOURCE |
| G5.2 | **80 mm.** Ctor writes `0x42A00000` = 80.0f to `[this+0x38]`. | `0x006121A2: movt r3,#0x42a0`; `0x006121B4: strd r5,r3,[r6,#-0x10]` (r6 = `this+0x44`, so `[this+0x38]`) | M11-004 | EXACT_SOURCE |
| G5.2u | Used as the translation tolerance of `Pose3d::IsSameAs`: `r0 = [this+0x38]` is replicated into `(80,80,80)` and passed as `r2`. | `0x006124C6: ldr r0,[r5,#0x38]`; `0x006124CC: str.w r0,[r2,r1,lsl#2]` (3 times); `0x006124F2: blx` PLT `0x4A7060` | M11-004 | EXACT_SOURCE |
| G5.3 | **600000 ms.** Ctor writes `0x000927C0` = 600000 to `[this+0x34]`. | `0x00612198: movw r5,#0x27c0`; `0x006121A6: movt r5,#9`; `0x006121B4: strd r5,r3,[r6,#-0x10]` (r6-0x10 = `this+0x34`) | M11-004 | EXACT_SOURCE |
| G5.3u | Used in `ShouldReactToTarget`: `r8 = [this+0x34]`; `dt = Robot::GetLastImageTimeStamp() - storedTimestamp`; if `dt > 600000` the target is treated as stale (`orrs r0,r2`). | `0x00612592: ldr.w r8,[r4,#0x34]`; `0x00612584: blx` PLT `0x4AC6F4` (`GetLastImageTimeStamp`); `0x0061259C: subs r1,r6,r7`; `0x006125A0: cmp r1,r8`; `0x006125A2: it hi` / `0x006125A4: movhi r2,#1`; `0x006125A6: orrs r0,r2` | M11-004 | EXACT_SOURCE |

The record's title also mentions "BlockWorld connected-object rule, moving and rotating gates".
Those are separate from these three thresholds; the three threshold addresses above are what the
record's evidence prose ("80 mm, 45 degrees, 600000 ms thresholds in the position-update
strategy") lacked.

---

## G6. Shipped `vision_config.json`

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G6.1 | `InitialVisionModes.DetectingMarkers = true`. | `re-analysis/obb/assets/cozmo_resources/config/engine/vision_config.json:5` (`"DetectingMarkers" : true`) | M11-021 | EXACT_SOURCE |
| G6.2 | `InitialModeSchedules` has no `DetectingMarkers` entry, so markers use the default schedule and run every frame. | `.../vision_config.json:19-29` (entries: DetectingPets, DetectingOverheadEdges, CheckingQuality, DetectingLaserPoints, ComputingStatistics) | M11-021 | EXACT_SOURCE |

The OBB file is present in this clone, so the earlier X1 authority-6 caveat is now closed.

---

## What is still unread, and exactly what to read next

1. **G1** is complete; no follow-up needed for the live extractor. (The file's `CompressConnectedComponentSegmentIds` remap tail `0x00894AF4..` and the non-live binomial bodies remain unread but are not on the live path.)
2. **G2** is complete for the `Detect` post-processing. If the exact `Rectangle<int>` min/max update is wanted, read `0x0087855C` to its end; and if the `ImageBase+0x3c` field's name is wanted, it is an `ImageBase<u8>` scalar copied by `operator=` at `0x0086EF2C` (semantics: image id/timestamp; the `ObservedMarker` ctor stores it at `+4`).
3. **G3** remains partially unread:
   - the bilinear pixel weighting, `0x008C60B6..0x008C610A` (against `Array<u8>` base `[sp+0x44]`, stride `[sp+0x48]`);
   - the 8-element design vector filled at `0x008C5C3E..0x008C5E80` (the `[sp+0x25c]` array) and the `DotDivide` operand plumbing at `0x008C62DC..0x008C6348`.
   These are the only two ranges needed before M11-005's numerics can be called EXACT_SOURCE.
4. **G4**: `Block::LookupBlockInfo` (`0x004E4C8C`) and `Block::AddFace` (`0x004E53BC`) were read at entry/dispatch only; read them fully if the record needs the per-face and per-type values.
5. **G5** is complete for the three thresholds; the "moving/rotating gates" part of M11-004 is a separate read (X1 section 3) and was not part of G5.

## Existing records contradicted or too weak (from this pass)

- **M11-003**: the record's evidence is bare symbol names. The symbols are real and their
  values are now recovered (G4.1, G4.5), but the record's citation must be replaced with the
  addresses; `_kDefaultHeadCamRotation` and `_canonicalCorners3d` live in `.bss` and are set by
  the static ctors, not by a literal in `.data` (a reviewer looking only at the symbol's section
  would wrongly conclude they are zero).
- **M11-004**: the evidence is prose; the three thresholds are now cited (G5.1/2/3).
- **M11-005**: the orchestration is EXACT_SOURCE; the two G3 numerics ranges are still
  RECOVERABLE_GAP, so the record should not be upgraded to EXACT_SOURCE for the refinement
  numerics until they are read.
- **M11-032**: its `unresolved` clause "the dark-mask multiplier 0xCCCC belongs to the non-live
  path" remains contradicted (G1.8; X1 section 3), and its evidence is still a bare address list.
- **M11-022**: now read (G2); the record can be settled once the manifest evidence is replaced.

*Read-only; nothing outside `.scratch/I-M11-gap1/` and this report file was changed.*
