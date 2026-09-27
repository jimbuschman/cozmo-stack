# M11 - the live component extractor: `ExtractComponentsViaCharacteristicScale` (ecvcs integral-image variant)

Scope: the live (shipped) component extractor selected by `FiducialDetectionParameters` byte 0 = 1, and
everything it needs down to the `ConnectedComponents` segment list that the later filters and
`TraceNextExteriorBoundary` consume. Read-only extraction; nothing outside `.scratch/` was changed.

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204).
The region is Thumb-2; disassembled with capstone `CS_MODE_THUMB` (`.scratch/m11-frontend/fn.py`).
Do not use `arm_disasm.py` (ARM) on this region. Function boundaries are the binary's own symbols.

## 0. The six questions answered

1. **Structure.** `ExtractComponentsViaCharacteristicScale` (0x0088F8BC) is a *multi-window box-filter*
   extractor, not a pyramid. It builds N row buffers (N = params+0x04 + 2), constructs a
   `ScrollingIntegralImage_u8_s32` over the grayscale image with `numBorderPixels = max(scale)+1`, and for
   each image row: (a) filters that row through all N windows into the N row buffers, (b) binarises the row
   with one of the `ecvcs_computeBinaryImage_*` callbacks into a mask row, (c) folds the mask row into the
   `ConnectedComponents` DP (`Extract2dComponents_PerRow_NextRow`), scrolling the integral image as needed.
   `Extract2dComponents_PerRow_Initialize` is called once and `_Finalize` once. The only output is the
   `ConnectedComponents` object (arg 5); the return value is a Result code.
2. **Binary arithmetic.** Each filtered value is the *mean* of a (2s+1)x(2s+1) box, computed from the
   integral image and a fixed-point multiplier/shift pair from two tables. The binarisation picks, per pixel,
   the filtered value *after* the largest jump between adjacent windows, then
   `mask = ((v * mult) >> 16) > pixel`. `mult` = params+0x0C (`0xCCCC` = 0.8 in Q16). The
   `_thresholdMultiplier1` variant is the same selection with `mult == 0x10000`, so its test is `v > pixel`.
3. **Scrolling integral image.** `PadImageRow` replicates `numBorderPixels` pixels at both ends of each row;
   `ComputeIntegralImageRow` is a prefix sum; `ScrollDown` accumulates them vertically into a rolling buffer
   that is shifted up and extended as the rows advance. `FilterRow` reads the four integral corners of the
   box and `FilterRow_innerLoop` writes `sum * mult >> shift`.
4. **Down/up-scale.** There is **none** in this variant (no `DownsampleByTwo` / `Upsample...` call anywhere in
   0x0088F8BC..0x0088FDF8). "CharacteristicScale" here is the window-size bank plus the per-pixel
   largest-adjacent-jump selection. The pyramid/downsample machinery belongs to the *non-live* binomial
   extractor (`ExtractComponentsViaCharacteristicScale_binomial` 0x00890448), which is what the C# stack
   implements.
5. **The `FiducialDetectionParameters` fields used** are +0x04, +0x08, +0x0C, +0x10, +0x12 (shipped
   1, 4, 0x0000CCCC, 0, 0).
6. **Output format.** A `FixedLengthList<ConnectedComponentSegment<u16>>` (the raw row segments) plus a
   per-component id map; each segment is 8 bytes `{ s16 start, s16 end, u16 row, u16 componentId }`.
   The u16 instantiation is the live one (shipped `params+0x40 = 39000 < 0x10000` selects it).

## 1. Step table

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| P1 | `Parameters::Initialize` writes the field bank: byte0 = 1 (variant selector, low half of `0x101`), +0x04 = 1, +0x08 = 4, +0x0C = `0x0000CCCC`, +0x10 = 0, +0x12 = 0. | `0x008752FC: movw r2,#0x101` / `0x00875304: strh r2,[r0]`; `0x0087530E: strd r3,r2,[r0,#4]` (r3=1,r2=4); `0x00875314: str r1,[r0,#0xc]` (r1=`0xCCCC` at `0x00875300`); `0x00875318: strh r1,[r0,#0x10]`; `0x0087531E: strh r1,[r0,#0x12]` (r1=0 at `0x00875316`) | M11-032 names the function; not these offsets | EXACT_SOURCE |
| L1 | **Selector.** `DetectFiducialMarkers` reads `params[0]`; non-zero goes to the live function, zero to the binomial one. Shipped byte0 = 1, so the live function runs. | `0x00898B4C: ldrb r0,[r7]`; `0x00898B4E: cmp r0,#0`; `0x00898B50: beq #0x898c18` (binomial); live call `0x00898BE0: blx ...ExtractComponentsViaCharacteristicScale...`; byte0 from P1 | M11-032 (implicitly), M11-018 evidence | EXACT_SOURCE |
| L2 | **Window bank.** The caller builds the `FixedLengthList<int>` the extractor takes: size = params+0x04 + 2; `list[i] = params+0x08 << i` for i = 0 .. params+0x04+1. Shipped: `{4, 8, 16}`, size 3. | `0x00898B5A: ldr r6,[r7,#4]`; `0x00898B62: adds r1,r6,#2`; `0x00898B68: add r0,sp,#0x348`; `0x00898B6A: blx FixedLengthList<int>::FixedLengthList(int,MemoryStack&,Flags::Buffer)`; loop `0x00898B8C: ldr r1,[r7,#8]` / `0x00898B90: lsls r1,r0` / `0x00898B92: str.w r1,[r2,r0,lsl#2]` / `0x00898B9A: cmp r0,r1` / `0x00898B9E: ble` | NEW (numeric part of M11-032) | EXACT_SOURCE |
| L3 | **Arguments.** live( image=&image, scales=&list, int = params+0x0C (0xCCCC), short = params+0x10 (0), short = params+0x12 (0), ConnectedComponents& = the object at sp+0x580, MemoryStack x3 ). | `0x00898BAA: ldr r5,[r7,#0xc]`; `0x00898BAC: ldrsh.w r6,[r7,#0x12]`; `0x00898BA0: ldrsh r0,[r7,#0x10]`; `0x00898BD8: ldr r3,[sp,#0x74]`; `0x00898BDA: mov r2,r5`; `0x00898BCC: strd r6,r4,[sp]` (r4 = sp+0x580 set at `0x00898B30`); demangled symbol `...RKNS0_5ArrayIhEERKNS0_15FixedLengthListIiEEissRNS0_19ConnectedComponentsENS0_11MemoryStackESB_SB_` | NEW | EXACT_SOURCE |
| L4 | **Entry.** Prologue/arg spill. Register args on entry: r0=image, r1=scale list, r2=multiplier, r3=short1; stack (after `push.w` 0x24 + `sub sp,#0xfc`): [sp+0x120]=short2, [sp+0x124]=CC&, [sp+0x128/+0x12c/+0x130]=the three MemoryStacks. | `0x0088F8BC: push.w {r4-r11,lr}`; `0x0088F8C0: sub sp,#0xfc`; `0x0088F8C2: mov r4,r0`; `0x0088F8CE: mov r7,r1`; `0x0088F8CA: mov sl,r2`; `0x0088F8C8: mov r8,r3`; `0x0088F8DE: ldrd r6,r5,[sp,#0x128]`; `0x0088F8E2: ldr.w fp,[sp,#0x130]`; `0x0088FA70: ldrd r0,r1,[sp,#0x124]` | NEW | EXACT_SOURCE |
| L5 | **Validity gates** (all fail into `Anki_Log`): three MemoryStacks valid and non-aliased; (image, list, CC) valid; scale count in [1,0x40]. | `0x0088F8F8: blx AreValid<MemoryStack,...>`; `0x0088F908: blx NotAliased<...>`; `0x0088F91E: blx AreValid<Array<u8>,FixedLengthList<int>,ConnectedComponents>`; `0x0088F928: subs r0,r4,#1` / `0x0088F92A: cmp r0,#0x40` / `0x0088F92C: bhs #0x88fb0c` | NEW | EXACT_SOURCE |
| L6 | **maxScale = max(list)+1.** Scans the list keeping the max of `list[i]+1`, starting from -1. Shipped: 16+1 = 17. | loop `0x0088F942: ldr r0,[r7,#0x30]` / `0x0088F944: ldr.w r2,[r0,r1,lsl#2]` / `0x0088F94A: adds r2,#1` / `0x0088F94C: cmp r8,r2` / `0x0088F950: movle r8,r2`; init `0x0088F93C: mov.w r8,#-1` | NEW | EXACT_SOURCE |
| L7 | **Integral image ctor.** `ScrollingIntegralImage_u8_s32( image[+0], image[+4], maxScale, MemoryStack, Buffer )`. The SII stores `+0x14 = image[+4]` (its `get_imageWidth`), `+0x1c = -maxScale`, `+0x20 = maxScale` (`get_numBorderPixels`), and constructs its `Array<int>` with `(image[+0], image[+4] + 2*maxScale)`. `Array<T>` is `{ +0 dim0, +4 dim1(=cols), +8 stride, +0xc Buffer, +0x10 data }` (L10), so the integral image is `(image rows) x (image cols + 2*maxScale)`. | `0x0088F96C: strd fp,r0,[sp]`; `0x0088F96A: mov r3,r8`; `0x0088F972: ldr r7,[sp,#0x18]`; `0x0088F976: mov r1,r7`; `0x0088F974: ldr r2,[sp,#0x20]`; `0x0088F978: blx ScrollingIntegralImage_u8_s32::ScrollingIntegralImage(int,int,int,MemoryStack&,Flags::Buffer)`; ctor `0x008A4D34` (`0x008A4D44: add.w r2,r7,r5,lsl#1`, `0x008A4D4E: blx Array<int>::Array`, `0x008A4D5C: strd r7,r8,[r4,#0x14]`, `0x008A4D60: strd r0,r5,[r4,#0x1c]`) | NEW | EXACT_SOURCE for the call/field writes; vertical row semantics are a RECOVERABLE_GAP (open Q1) |
| L8 | **Initial ScrollDown** with `arg2 = image[+0]`: fills the integral image (the SII's row field equals `arg2`, so the ctor path at `0x008A4E1C` runs: pad row 0, prefix-sum, replicate `numBorderPixels` rows). | `0x0088F988: ldr r1,[sp,#0x24]` (&image); `0x0088F98A: mov r0,r5`; `0x0088F98C: mov r2,r7` (=image[+0]); `0x0088F990: blx ScrollDown(Array<u8> const&,int,MemoryStack)`; `0x008A4E14: cmp r6,sl` / `0x008A4E1A: bne #0x8a4eaa` | NEW | RECOVERABLE_GAP - ScrollDown 0x008A4DB0..0x008A4FD8 not read line by line this pass |
| L9 | **N row buffers.** `FixedLengthList<Array<u8>>(count, MemoryStack1, Buffer(0,0,1))`; then `count` `Array<u8>(1, image[+4])` buffers are allocated and copied into the list (element stride 0x14). These receive the filtered rows. | `0x0088F9A6: ldr r3,[sp,#0x64]` / `0x0088F9AE: blx FixedLengthList<Array<u8>>::FixedLengthList(int,MS&,Buffer)`; alloc loop `0x0088F9E2`..`0x0088FA3C`, `0x0088F9F0: movs r1,#1`, `0x0088F9F4: mov r2,r6` (=image[+4]), `0x0088F9FA: blx Array<u8>::Array(int,int,MS&,Buffer)`, copy `0x0088FA00`..`0x0088FA14`, `0x0088FA38: adds r4,#0x14` | NEW | EXACT_SOURCE |
| L10 | **Mask row buffer**: one more `Array<u8>(1, image[+4])` at sp+0x4c; its data pointer is the `u8*` handed to the binarise callback and to `NextRow`. | `0x0088FA46 Flags::Buffer(1,0,0)`; `0x0088FA56: blx Array<u8>::Array`; `0x0088FA5A: ldr r4,[sp,#0x5c]` (data ptr); `0x0088FA80: mov fp,r4`; `0x0088FC20: mov r1,fp` | NEW | EXACT_SOURCE |
| L11 | **DP init**: `ConnectedComponents::Extract2dComponents_PerRow_Initialize(cc, MS1, MS2, MS3)`. (This is the call site at 0x88FA70; the function is the thunk 0x00893BAA -> u16 body 0x00893BC4.) | `0x0088FA70: ldrd r0,r1,[sp,#0x124]`; `0x0088FA74: mov r2,fp`; `0x0088FA76: ldr r3,[sp,#0x130]`; `0x0088FA78: blx ConnectedComponents::Extract2dComponents_PerRow_Initialize` | M11-032 cites only 0x0088FA70 | EXACT_SOURCE |
| L12 | **Binarise-callback selection.** count == 3 -> `numFilters3`; count == 5 and multiplier == 0x10000 -> `numFilters5_thresholdMultiplier1`; count == 5 and multiplier != 0x10000 -> `numFilters5`; any other count -> the generic `ecvcs_computeBinaryImage`. The four GOT slots resolve to exactly those four symbols (`.rel.dyn` ARM_GLOB_DAT entries at 0x0103FF0C/0x0103FF08/0x0103FF04/0x0103FF10). Shipped count=3, so `numFilters3` is live. | `0x0088FA8A: ldr r0,[sp,#0x1c]`; `0x0088FA8C: cmp r0,#5`; `0x0088FA8E: beq #0x88fb76`; `0x0088FA90: cmp r0,#3`; `0x0088FA92: bne #0x88fb90`; literal pair `0x0088FA94`+`0x0088FA96: add r0,pc` -> slot 0x0103FF0C; `0x0088FB78: cmp.w r0,#0x10000`; `0x0088FB7C: bne #0x88fba2`; 0x88FB7E -> 0x0103FF08; 0x88FBA2 -> 0x0103FF04; 0x88FB90 -> 0x0103FF10; `std::function<...>::operator=` at `0x0088FAA0`/`0x0088FB8A`/`0x0088FB9C`/`0x0088FBAE` | NEW; M11-032 names the functions but not the selector rule | EXACT_SOURCE |
| L13 | **Row loop** `for row = 0 .. image[+0]-1`: filterRows(row) -> callback -> NextRow -> if `get_maxRow(maxScale) <= row` then `ScrollDown(image[+0] - 2*maxScale)`. | `0x0088FBCE: ldr r0,[sp,#0x18]` / `0x0088FBD0: cmp r6,r0` / `0x0088FBD2: bge #0x88fc6e`; `0x0088FBE4: blx ecvcs_filterRows`; `0x0088FBF8: strd r6,fp,[sp]` / `0x0088FC04: blx std::function<...>::operator()`; `0x0088FC28: blx ...PerRow_NextRow`; `0x0088FC3E: ldr r1,[sp,#0x1c]` / `0x0088FC42: blx get_maxRow` / `0x0088FC48: bgt #0x88fc62` / `0x0088FC5A: blx ScrollDown`; `0x0088FC80: blx ...PerRow_Finalize`; `0x0088FCB2: mov r0,sb` (return) | NEW | EXACT_SOURCE for calls; RECOVERABLE_GAP for get_maxRow/ScrollDown semantics |
| F1 | **`ecvcs_filterRows`**: for every window `s` in the list, builds `Rectangle<s16> { -s, +s, -s, +s }` (col-min, col-max, row-min, row-max), looks up `mult = T1[s]`, `shift = T2[s]`, and calls `FilterRow<u8>(sii, rect, row, mult, shift, out[i])`. The out element is the `Array<u8>` at list[i] (advance 0x14 per window). | `0x0088F4D6: ldr r7,[r1,#0xc]`; `0x0088F4F0: ldr r4,[r3,#0x30]` (out data); rect `0x0088F4FA: strh r0,[sp,#0xe]` / `0x0088F500: strh r1,[sp,#0xc]` / `0x0088F508: strh r1,[sp,#0x10]` / `0x0088F50C: strh r0,[sp,#0x12]` (r1=-s from `0x0088F4FE: rsbs r1,r0,#0`); tables `0x0088F504: ldr.w r3,[fp,r0,lsl#2]` and `0x0088F512: ldr.w r0,[sl,r0,lsl#2]` with fp=0xC98958 (`0x0088F4E8` literal 0x00409462 + `0x0088F4F2: add fp,pc`) and sl=0xC98A5C (`0x0088F4E0` literal 0x0040956A + `0x0088F4EE: add sl,pc`); `0x0088F516: strd r0,r4,[sp]`; `0x0088F51C: blx FilterRow<u8>`; `0x0088F520: subs r7,#1` / `0x0088F522: add.w r4,r4,#0x14` | NEW | EXACT_SOURCE |
| F2 | **The two lookup tables** (0xC98958 = multiplier, 0xC98A5C = shift, indexed by the window half-width s): `T1[s] = round(2^T2[s] / (2s+1)^2)`, so `T1[s] >> T2[s]` is the fixed-point reciprocal of the box area. Verified for s=1..19 (s=4: 101>>13 vs 1/81; s=8: 227>>16 vs 1/289; s=16: 241>>18 vs 1/1089). | data at 0xC98958 (`01 00 00 00 39 00 ...`) and 0xC98A5C (`00 00 00 00 09 00 00 00 ...`); consumed at `0x0088F504`/`0x0088F512` | NEW | EXACT_SOURCE |
| F3 | **`ScrollingIntegralImage_u8_s32::FilterRow<u8>`** computes the four integral-image corner pointers of the (2s+1)x(2s+1) box centred on (row, x): top-1 row = `row + border + k2 - 1`, bottom row = `row + border + k3`, left col = `border + k0 - 1`, right col = `border + k1`; `border = [sii+0x20]`, `stride = [sii+8]`, data = `[sii+0x10]`. The horizontal window is relative to the output index (the inner loop adds i). Leading/trailing parts of the row are `__aeabi_memclr`'d. Returns Result 0. | `0x0088F54A: ldr r1,[r4,#0x20]`; `0x0088F590: ldr r7,[r4,#0x1c]`; `0x0088F596: sub.w r7,r6,r7` / `0x0088F59E: add r3,r7` / `0x0088F5A2: add r5,r7`; `0x0088F5A0: ldr r0,[r4,#8]`; `0x0088F5A8: ldr r2,[r4,#0x10]`; corners `0x0088F5C6`/`0x0088F5CA`/`0x0088F5CE`/`0x0088F5D2`; memclr `0x0088F5DE`/`0x0088F60E`; `0x0088F5FA: blx FilterRow_innerLoop<u8>` | M11-032 names the function only | EXACT_SOURCE for the box; the `+border` row offset interacts with open Q1 |
| F4 | **`FilterRow_innerLoop<u8>`**: `out[i] = (p3[i] - p2[i] + p0[i] - p1[i]) * mult >> shift` with the 4 corner pointers, i.e. the box sum times the table multiplier/shift = the box *mean*. The `mult==1 && shift==0` case writes the sum unchanged. | `0x008A5230: sub.w r4,r7,r4` / `0x008A5238: add r4,r5` / `0x008A523A: sub.w r4,r4,r6` / `0x008A523E: mul r4,r2,r4` / `0x008A5242: asr.w r4,r4,r3` / `0x008A5246: strb.w r4,[ip,r0]`; identity branch `0x008A5216: cmpeq r3,#0` / `0x008A521C: beq #0x8a5254` | NEW | EXACT_SOURCE |
| B1 | **`ecvcs_computeBinaryImage_numFilters3`** (the live callback at shipped settings). Reads the three filtered rows f0 (s=4), f1 (s=8), f2 (s=16) at element data offsets +0x10/+0x24/+0x38; per pixel `v = (|f1-f0| > |f2-f1|) ? f1 : f2`; `mask = (((v * mult) >> 16) > pixel) ? 1 : 0` with `mult` = the callback's first int (= params+0x0C = 0xCCCC). The shift is arithmetic and the multiply is 32-bit. | `0x0088F61E: ldr.w ip,[r0,#4]`; `0x0088F62C: ldr r7,[r0,#8]`; `0x0088F62E: ldr r0,[r0,#0x10]`; `0x0088F632: mla r0,r7,r3,r0`; `0x0088F630: ldr r4,[r1,#0x30]`; ptrs `0x0088F638: ldr.w lr,[r4,#0x10]` / `0x0088F63C: ldr.w r8,[r4,#0x24]` / `0x0088F640: ldr r4,[r4,#0x38]`; select `0x0088F64E: subs r7,r5,r3` / `0x0088F650: subs r3,r6,r5` / abs `0x0088F652`,`0x0088F656`-`0x0088F65A` / `0x0088F65C: cmp r7,r3` / `0x0088F65E: it gt` / `0x0088F660: movgt r6,r5`; binarise `0x0088F662: mul r3,r6,r2` / `0x0088F66A: asrs r3,r3,#0x10` / `0x0088F66C: cmp r3,r5` / `0x0088F672: it gt` / `0x0088F674: movgt r3,#1` / `0x0088F67A: strb r3,[r1],#1` | M11-032 names it only | EXACT_SOURCE |
| B2 | **`ecvcs_computeBinaryImage_numFilters5`**: same rule over five windows f0..f4 (half-widths params+0x08<<i, shipped {4,8,16,32,64}); the selected value is `f_{j+1}` where `j` is the first index maximising `|f_{j+1}-f_j|` (preference g10 >= max(g43,g32,g21), then g21, then g32, then f4); `mask = ((v*mult)>>16) > pixel`. | window ptrs `0x0088F6A0: ldr.w r8,[r1,#0x38]` / `0x0088F6A4: ldr.w lr,[r1,#0x10]` / `0x0088F6A8: ldr.w sl,[r1,#0x24]` / `0x0088F6AC: ldr r7,[r1,#0x4c]` / `0x0088F6AE: ldr.w sb,[r1,#0x60]`; gaps `0x0088F6CE`..`0x0088F70A`; selection `0x0088F70C: cmp r2,r7` / `0x0088F712: bge #0x88f726` / `0x0088F714: cmp fp,r1` / `0x0088F71A: add r2,r1,r6` / `0x0088F720: cmp ip,r1` / `0x0088F722: it eq` / `0x0088F724: moveq r2,r7`; binarise `0x0088F73E: muls r1,r2,r1` / `0x0088F744: asrs r1,r1,#0x10` / `0x0088F746: cmp r1,r2` / `0x0088F74C: it gt` / `0x0088F74E: movgt r1,#1` | M11-032 names it only | EXACT_SOURCE for structure/gaps; tie-break order as cited |
| B3 | **`ecvcs_computeBinaryImage_numFilters5_thresholdMultiplier1`**: identical largest-adjacent-gap selection over five windows, but the binarisation has no multiply: `mask = (pixel < v) ? 1 : 0`, i.e. `v > pixel`. Selected only when the multiplier int == 0x10000. | selection `0x0088F7A6`..`0x0088F72A`; binarise `0x0088F7FC: cmp r1,r0` / `0x0088F802: it lo` / `0x0088F804: movlo r0,#1` / `0x0088F80A: strb r0,[sb],#1`; selector `0x0088FB78: cmp.w r0,#0x10000` | M11-032 names it only | EXACT_SOURCE |
| B4 | **`ecvcs_computeBinaryImage`** (generic, used when count is neither 3 nor 5): copies the N filter data pointers out of the list (element stride 0x14, data at +0x10), then per pixel applies the same largest-adjacent-gap rule and `(v*mult)>>16 > pixel`. Not exercised at shipped settings. | `0x0088F828: ldr.w fp,[r1,#0xc]` (count); copy `0x0088F834: ldr r0,[r1,#0x30]` / `0x0088F83A: adds r0,#0x10` / `0x0088F83C: ldr r4,[r0],#0x14` / `0x0088F842: str r4,[r1],#4`; gaps `0x0088F876`..`0x0088F892`; binarise `0x0088F898: mul r0,r7,r2` / `0x0088F8A0: asrs r0,r0,#0x10` / `0x0088F8A2: cmp r0,r1` / `0x0088F8AC: movgt r0,#1` / `0x0088F8AE: strb r0,[r1,r6]` | NEW | EXACT_SOURCE for the arithmetic |
| D1 | **Per-row thunk** `ConnectedComponents::Extract2dComponents_PerRow_*`: a byte at object+0 picks the instantiation - 0 -> i32 template at +0x11c, non-zero -> u16 template at +4. | `0x00893BAA: ldrb.w ip,[r0]` / `0x00893BAE: cmp.w ip,#0` / `0x00893BB4: addeq.w r0,r0,#0x11c` / `0x00893BB8: beq` / `0x00893BBC: adds r0,#4`; same pattern at 0x00893F38-0x00893F52 (NextRow) and 0x008943A4-0x008943B2 (Finalize) | NEW | EXACT_SOURCE |
| D2 | **Which instantiation is live**: `DetectFiducialMarkers` builds the CC with `useU16 = 1` when `params+0x40 < 0x10000` (shipped 39000), which the CC ctor stores as object byte 0; the thunk therefore dispatches to the **u16** template. `params+0x40` doubles as the u16-template first arg (parent-array size). | `0x00898866: ldr r1,[r6,#0x40]`; `0x0089886E: cmp.w r1,#0x10000`; `0x00898876: movge r3,#0`; `0x00898878: movlt r3,#1`; `0x0089887A: blx ConnectedComponents::ConnectedComponents(int,short,bool,MemoryStack&)`; `0x0089351A: strb r7,[r8]`; `0x00893532: cmp r7,#1` / `0x00893534: bne #0x89362a` | NEW | EXACT_SOURCE |
| D3 | **`ConnectedComponentsTemplate<u16>::Extract2dComponents_PerRow_Initialize`**: requires state `[t+0x104]==1`, resets the component counter `[t+0x10c]=0`, builds the per-row segment lists (`+0x34`, `+0x68`, `+0x9c`) with capacity `[t+0x110]` and the u16 parent/id array (`+0xd0`) with capacity `[t+0x114]` (= params+0x40), zeroes the parent array, sets state 2. | `0x00893BD6: cmp r0,#1` / `0x00893BD8: bne #0x893cd8`; `0x00893BDA: ldr r1,[r4,#0x1c]`; `0x00893BDE: strh.w r0,[r4,#0x10c]`; list ctors `0x00893C06`/`0x00893C44`/`0x00893C82` (`FixedLengthList<ConnectedComponentSegment<u16>>`) and `0x00893D10` (`FixedLengthList<u16>`); sizes `0x00893BF2: ldr.w r7,[r4,#0x110]` / `0x00893CAC: ldr.w r0,[r4,#0x114]`; zero `0x00893D50: strh.w r2,[r0,r2,lsl#1]`; `0x00893D5A: movs r0,#2` / `0x00893D5C: str.w r0,[r4,#0x104]` | NEW | EXACT_SOURCE |
| D4 | **`Extract1dComponents`** (u16, 0x00896FD6): scans the mask row once and emits one `ConnectedComponentSegment<u16>` per run of 1s: `{ s16 start, s16 end, u16 row = 0xFFFF, u16 compId = 0xFFFF }`. `start` = first 1 pixel; `end` = last 1 pixel (`x - r8`, `r8 = r6+1`). A run shorter than arg `a` is dropped; a second test compares `r6+1` against arg `b` before recording. The final run is flushed after the loop. | `0x00896FF4-0x0089700A` init; run start `0x00897034: movne ip,r5`; record `0x0089704E: uxth.w sl,ip` / `0x0089705C: add.w r4,r7,r4,lsl#16` / `0x00897060: asrs r4,r4,#0x10` / `0x00897062: cmp r4,r3` (a) / `0x00897064: blt #0x8970a4` / `0x00897086: str.w r4,[r2,lr,lsl#3]` / `0x00897096: str r4,[r2,#4]` (0xffff row+id); `b` test `0x00897046: sxth.w r6,r8` / `0x0089704A: cmp r6,r4` / `0x0089704C: ble #0x8970a0`; flush `0x008970BA`..`0x008970F4` | NEW | EXACT_SOURCE for the record; the `b` test meaning is a RECOVERABLE_GAP (open Q2) |
| D5 | **Merge across rows** (`NextRow`, u16 0x00893F5C): requires state 2; calls `Extract1dComponents(maskRow, width, a, b, &list+0x34)`; then for each current run walks the previous row's segments (`[t+0x98]`) and union-finds (min) their component ids in the parent array (`[t+0x100]`), writes the merged segment into the current-row list (`[t+0xcc]`), and appends the segment to the output accumulator (data `[t+0x30]`, count `[t+0xc]`, capacity `[t+0x1c]`). A run with no overlap gets a new id from `[t+0x10c]`. Finally `Swap(list+0x9c, list+0x68)`. | state `0x00893F66`/`0x00893F6A`; Extract1dComponents `0x00893F96: blx`; overlap tests `0x00893FF0: ldrsh.w r5,[fp,r3,lsl#3]` / `0x00894000` / `0x00894008`; union `0x00894024: ldrh.w r0,[r7,ip,lsl#1]` / `0x00894042: strh.w r4,[r7,r5,lsl#1]`; append `0x00894058`/`0x0089405A`/`0x00894060`/`0x0089407C`; new id `0x008940AE: ldrh.w r0,[r5,#0x10c]` / `0x008940B4`; `0x008940FC: blx Swap<FixedLengthList<ConnectedComponentSegment<u16>>>` | NEW | EXACT_SOURCE for structure; union-find is a plain "set all three to min" (no path compression) |
| D6 | **`Finalize`** (u16 0x008943B8): requires state 2; resolves the parent array to fixed point (bounded at 0x3E6 passes), rewrites each output segment's id (`elem+6`) with its root, recomputes `[t+0x10c]` = max id, sets state 3. | `0x008943DE: ldrh.w r6,[r2,r1,lsl#1]` / `0x008943E2: ldrh.w r5,[r2,r6,lsl#1]` / `0x008943E6: cmp r5,r6` / `0x00894400: strh.w r5,[r2,r1,lsl#1]`; rewrite `0x00894426: ldrh r7,[r3]` / `0x0089442C: ldrh.w r7,[r2,r7,lsl#1]` / `0x00894430: strh r7,[r3],#8`; max `0x00894470`..`0x00894482`; `0x00894484: movs r1,#3` / `0x00894486: str.w r1,[r0,#0x104]` | NEW | EXACT_SOURCE |
| D7 | **Output format**: `ConnectedComponentSegment<u16>` = 8 bytes `{ s16 start @+0, s16 end @+2, u16 row @+4, u16 componentId @+6 }`; the accumulator is `{ size @+0xc, capacity @+0x1c, data @+0x30 }` relative to the u16 template base (CC+4). | packed `0x00894062: pkhbt r4,sl,lr,lsl#0x10` / `0x0089406A: str.w r4,[r6,r5,lsl#3]` / `0x00894074: orr.w r0,r0,ip,lsl#0x10` / `0x00894078: str r0,[r6,#4]`; read as extents `0x00893FF0`/`0x00894000` (elements at fp+r3*8); id at elem+6 read by Finalize `0x00894426` | NEW | EXACT_SOURCE |
| D8 | **`CompressConnectedComponentSegmentIds`**: wrapper at 0x00894A48 dispatches on the CC byte; the u16 body (0x00894A7C) allocates a `(maxId+1)`-byte used-bitmap and a `2+2*maxId` byte remap, marks used ids from `elem+6`, and renumbers surviving components to 1..N (id 0 stays 0). | `0x00894A4E: ldrb r0,[r4]` / `0x00894A50: cbz r0,#0x894a64`; body `0x00894A86: ldrh.w r0,[r4,#0x10c]` / `0x00894A8A: ldr.w r8,[r4,#0x30]` / `0x00894A92: blx MemoryStack::Allocate` (bitmap) / `0x00894AA4` (remap) / `0x00894AD4: ldrh r7,[r1],#8` / `0x00894ADA: strb r2,[r6,r7]` | NEW | EXACT_SOURCE for the format; remap tail 0x00894AF4-0x00894B6x not read to the end |
| D9 | **Consumer interface**: `TraceNextExteriorBoundary(ConnectedComponents const&, int id, FixedLengthList<Point<s16>>&, int&, MemoryStack)` reads the wrapper: `IsValid`, `get_size`, `get_isSortedInId`; the later filters consume the same segment list. | `0x008C6B26: blx ConnectedComponents::get_size`; `0x008C6B2E: blx ConnectedComponents::IsValid`; `0x008C6B5C: blx ConnectedComponents::get_isSortedInId`; `0x008C6B62`..`0x008C6B6A` bounds the id against `get_size()` | M11-018/M11-026 mention TraceNextExteriorBoundary; neither records its input format | EXACT_SOURCE |
| D10 | **`Array<T>` layout** (needed to place the integral image and the row buffers): `+0` = dim0, `+4` = dim1 (cols, used as the row width), `+8` = stride (`ComputeRequiredStride(cols)`), `+0xc` = `Flags::Buffer`, `+0x10` = data pointer. | `Array<u8>::Array` 0x008759CC: `0x008759D4` Buffer at +0xc, `0x00875A00: ComputeRequiredStride(r5=dim1)`, `0x00875A12: AllocateBufferFromMemoryStack`; `Allocate` `0x008760FC: str r7,[r6,#8]`; `InitializeBuffer` `0x0087624C: strd r7,r4,[r5]` / `0x00876256: strd r1,r6,[r5,#0xc]`; used in `PadImageRow` `0x008A505C: ldrd ip,r6,[r1,#4]` / `0x008A5060: ldr r7,[r1,#0x10]` / `0x008A5062: mla r1,r6,r2,r7` | NEW | EXACT_SOURCE for +4/+8/+0x10; exact `+0` semantics inferred (open Q1) |

## 2. Contrast with the non-live binomial path (what a reimplementation drops)

* **Entry point.** `ExtractComponentsViaCharacteristicScale_binomial` is a *different function*, called from
  the other arm of the same selector (`0x00898C5C`; branch taken at `0x00898B50`). Its symbol is
  `...RKNS0_5ArrayIhEEiiss...`: it takes `(Array<u8> const&, int, int, short, short, CC&, MS x3)`, i.e. no
  window list. The caller passes `params+0x04` as the first int (`0x00898C1C: ldr r5,[r7,#4]` /
  `0x00898C52: mov r1,r5`) and `params+0x0C` as the int (`0x00898C18`/`0x00898C58`, [sp+0x74]=0xCCCC). So in
  the binomial path `params+0x04` is a *pyramid level count* and `0xCCCC` feeds the `0x00890BB6` binarise; in
  the live path `params+0x04` is `(number of windows - 2)` and `0xCCCC` feeds `numFilters3`. **A
  reimplementation must not carry the binomial reading of `params+0x04`/`params+0x0C` across.**
* **Scaling.** The live variant has no `DownsampleByTwo`/`UpsampleByPowerOfTwoBilinear` call; the binomial
  path owns those (`0x00890EAD` etc.). Drop the pyramid entirely for the live path.
* **Filter.** The binomial path uses `ImageProcessing::BinomialFilter<u8,u8,u8>` (0x008A2344, 5-tap
  `[1 4 6 4 1]`, `>>4`) and `Matrix::Elementwise::ApplyOperation<SumOfAbsDiff>` in the
  characteristic-scale select (0x00890B14). The live path replaces both with a bank of box means over the
  integral image and a largest-adjacent-jump selection. Drop the binomial kernel and the |filtered-image|
  response.
* **Binarise.** The binomial path's `scale*0xCCCC>>16 > pixel` (0x00890BB6) uses a *single* filtered value;
  the live path applies `mult` to the *selected* window value, and `numFilters5_thresholdMultiplier1` shows
  the multiply is not inherent (`mult==0x10000` is special-cased to `v > pixel`).

## 3. Existing records: contradictions, weak evidence, gaps this pass found

**Contradicted / not supported by source**

1. **M11-032** (current title "The live component extractor is the ecvcs integral-image variant", status
   `IMPLEMENTATION_GAP`, live_path true). Its current `unresolved` field reads, verbatim:
   `"...the dark-mask multiplier 0xCCCC belongs to the non-live path"`. The second clause is
   **contradicted**: the live path reads the same `params+0x0C` and uses it as the binarise multiplier
   (`0x00898BAA: ldr r5,[r7,#0xc]` -> the callback's first int -> `0x0088F662: mul r3,r6,r2` in
   `numFilters3`). `0xCCCC` belongs to *both* paths, applied to a differently-derived value.
2. **M11-032's `evidence` is only a list of addresses** (`0x0088F8BC function; Extract2dComponents_PerRow_Initialize
   0x0088FA70; ecvcs_computeBinaryImage_numFilters3 0x0088F61A / _5 0x0088F684 / _5_thresholdMultiplier1
   0x0088F75E; ecvcs_filterRows 0x0088F4D0; ScrollingIntegralImage_u8_s32::FilterRow 0x0088F538`). Under the
   project rule "a bare symbol name is not a citation" this cannot carry the claim. It also cites
   `Extract2dComponents_PerRow_Initialize 0x0088FA70`, which is the **call site**, not the function (the
   function is the thunk `0x00893BAA` -> u16 body `0x00893BC4`), and it omits the selector rule that
   distinguishes the three `numFilters*` bodies, so it cannot vouch *which* callback is live.
3. **M11-018's** line "the dark mask 0x008A2344 / binarize 0x00890BB6 are inside the non-live binomial
   extractor (only caller 0x00890736)" is consistent for the binomial extractor, but must not be read as
   "0xCCCC is non-live only" (item 1). Its status stays `IMPLEMENTATION_GAP`.

**Records whose evidence is too weak for their status**

- **M11-032**: see item 2. Even the arithmetic it depends on (window bank, box mean, selection, `numFilters*`
  bodies, segment format) is not cited.
- **No record at all** covers the window-bank construction (L2), the callback selector (L12), the box-mean
  arithmetic and tables (F1-F4), the three `numFilters*` bodies (B1-B4), the per-row DP/segment format
  (D3-D8), or the SII. These are `NEW`; per the "a settled record owns its whole production path" rule they
  need records before M11-032 can be settled.

## 4. Open questions for the manager

1. **Vertical window / SII row bookkeeping (RECOVERABLE_GAP).** `FilterRow` indexes integral-image rows as
   `row + border + k` (`0x0088F590`-`0x0088F5A2`) with `border = maxScale = 17`, while the SII's
   `Array<int>` row count is `image[+0]` and the ExtractComponents row-loop bound is also `image[+0]`
   (`0x0088FBD0`). The rolling/offset mechanism (`ScrollDown` 0x008A4DB0..0x008A4FD8, `get_maxRow`
   0x008A51E0, `get_rowOffset`/`[sii+0x1c]` 0x008A5202) was not traced line by line, so I cannot state
   exactly how the top/bottom border rows are represented, nor whether the input `Array<u8>` is already
   padded. Exact reproduction of the image edges needs a follow-up extractor pass over those three
   functions and the caller's image construction. **UNKNOWN rather than guessed.**
2. **`Extract1dComponents` threshold `b` (RECOVERABLE_GAP).** The record path tests `r6 <= b` where
   `r6 = sxth(r6+1)` and the counter appears constant 1 at that point (`0x00897040`-`0x0089704C`; the same
   shape at `0x008971C0`-`0x008971C8` in the i32 twin). Read literally, `b=0` records all runs and any
   `b>=1` drops them, which is almost certainly not the intent. With shipped `b=0` the live behaviour is
   unaffected, but the parameter's meaning is not recovered. The `a` test *is* clear: a run shorter than `a`
   is dropped (`0x00897062`-`0x00897064`).
3. **Which record owns the output format.** The 8-byte `ConnectedComponentSegment<u16>` layout, the
   accumulator offsets (`+0xc`/`+0x1c`/`+0x30`) and the id compression are consumed by M11-018/M11-026's
   filters but recorded nowhere. Should this be a new M11 record that the later filters cite, or folded into
   M11-026?
4. **Tie-breaks.** `numFilters3` selects `f1` only on a *strict* `|f1-f0| > |f2-f1|` (`0x0088F65E: it gt`),
   while `numFilters5`/`_thresholdMultiplier1` use `>=` for the corresponding gap. Preserve per function, or
   normalise? Behaviour-changing only on exact ties.
5. **`Array<T>` `+0` field.** `InitializeBuffer` writes `[+0] = ctor arg0`, `[+4] = ctor arg1`, with
   `stride = ComputeRequiredStride(arg1)`; `PadImageRow`/`FilterRow` fix `[+4]` = cols and `[+8]` = stride.
   That only *infers* `[+0]` = rows. Confirm from the class (or the image construction in
   `DetectFiducialMarkers`) before the SII row count is treated as exact.

*Generated read-only; nothing outside `.scratch/` was changed.*
