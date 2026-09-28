# I-M11 gap pass 2 - extraction (read-only)

Job: gap pass 2 for the M11-vision integration job (I-M11). Scope: close G7..G10 of
`re-analysis/research/20260927-I-M11-gap1-extraction.md` (the "what is still unread" list).
Agent: opencode (DeepSeek), window 3. Date: 2026-09-27.
Scratch: `.scratch/I-M11-gap2/` (Thumb disassembler `vdis.py`, raw dumps).

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204).
The vision region is Thumb-2; disassembled with capstone `CS_MODE_THUMB` (base 0). All
addresses below are file VAs. The Ghidra decompilation was not used. Where a step is fully
read it is EXACT_SOURCE; where it is not, the exact range to read is named.

Row form: `| # | what the original does | citation | record | class |`.

---

## G7. Two X1 rows re-cited (required)

The X1 pass wrote two branch targets that do not match the bytes. Both are confirmed
against the instruction encoding and the `.plt` relocation map.

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G7.1 | **X1 V14 correction.** At `0x00892EF8` the bytes are `3e f4 16 e8`, which capstone decodes as `blx #0x4d0f28` (the PLT stub for `IsQuadrilateralReasonable`), not `0x4d0e60`. `0x4d0e60` is not a PLT entry (the neighbouring stubs are `0x4d0e5c` = `UpsampleByPowerOfTwoBilinear<4>` and `0x4d0e68` = `UpsampleByPowerOfTwoBilinear<5>`). The function body is `0x00892B18` (`IsQuadrilateralReasonable(Quadrilateral<short> const&, int, int, int, int, int, bool&)`). Its three named parameters are **not** literals at `0x00892B18`; they are `MarkerDetector::Parameters` fields: `minQuadArea = 25` at `Parameters+0x30`, `symmetry = 512` (8.8 fixed) at `Parameters+0x34`, `minDistanceFromEdge = 2` at `Parameters+0x38`. Written in `Parameters::Initialize`: `0x00875338: stm.w r1,{r3,r5,lr}` with `r1 = r0+0x28` (`0x0087532E: add.w r1,r0,#0x28`), `r5 = 5` (`0x00875328`), `lr = 0x19` (`0x0087532A`) -> `+0x30 = 25`; `0x00875340: strd r6,r7,[r0,#0x34]` with `r6 = 0x200` (`0x00875332`), `r7 = 2` (`0x00875336`) -> `+0x34 = 512`, `+0x38 = 2`. Read on the live path in `DetectFiducialMarkers`: `0x00898E00: ldrd r6,r4,[r7,#0x28]`; `0x00898E06: ldrd r5,r0,[r7,#0x30]` (r5 = 25, r0 = 512); `0x00898E0A: str r0,[sp,#0x74]`; `0x00898E0E: ldr r7,[r7,#0x38]` (2); `0x00898E1E: mov r3,r7`; `0x00898E28: ldr r2,[sp,#0x74]`; `0x00898E2A: mov r1,r5`; `0x00898E2C: blx #0x4d13c0` (`ComputeQuadrilateralsFromConnectedComponents`). (The task's "mov r3,r7" is at `0x00898E1E`, not at `0x00898E2A`.) The corrected V14 citation is: `0x00892EF8: blx #0x4d0f28` (PLT `IsQuadrilateralReasonable`); params `0x00875338`/`0x00875340` -> `0x00898E00`/`0x00898E0E`/`0x00898E1E`/`0x00898E2A` -> `0x00898E2C: blx #0x4d13c0`. | `0x00892EF8` bytes `3e f4 16 e8`; `0x00892B18` (`IsQuadrilateralReasonable` body, `push.w {r4,r5,r6,r7,r8,sb,sl,fp,lr}`); `0x0087532E`/`0x00875338`/`0x00875340`; `0x00898E00`/`0x00898E06`/`0x00898E0E`/`0x00898E1E`/`0x00898E28`/`0x00898E2A`/`0x00898E2C`; PLT `0x4d0f28` = `IsQuadrilateralReasonable` | M11-018, M11-028 | EXACT_SOURCE |
| G7.2 | **X1 V18 correction.** At `0x0089FEA6` the bytes are `31 f4 fa eb`, which capstone decodes as `blx #0x4d169c` (the PLT stub for `RefineQuadrilateral`), not `0x008c55e0` (which is the body). Confirmed against the `.rel.plt` map: `0x4d169c = RefineQuadrilateral(...)`. The call site passes `r0 = sb` (`0x0089FEA4: mov r0,sb`) with the stack arguments set at `0x0089FE90..0x0089FEA0`. | `0x0089FEA6` bytes `31 f4 fa eb`; `0x0089FEA4: mov r0,sb`; PLT `0x4d169c`; body `0x008c55e0` | M11-005, M11-031 | EXACT_SOURCE |

---

## G8. The M11-005 numerics, finished

All three ranges are now read. `RefineQuadrilateral` body is `0x008C55E0..0x008C656C`;
the design-vector fill is at `0x008C5CE6..0x008C5D86`; the bilinear read is at
`0x008C609A..0x008C610A`; the H22 `DotDivide` is at `0x008C62A0..0x008C634A`.

### G8.1 Bilinear pixel read (`0x008C609A..0x008C610A`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.1a | Image `Array<u8>` layout used here: `0x008C609A: ldrd r2,r1,[sp,#0x44]` loads `r2 = base = [sp+0x44]` and `r1 = stride = [sp+0x48]`; `0x008C5F30: str r1,[sp,#0x48]` and `0x008C5F32: ldr r0,[r0,#0x10]` / `0x008C5F34: str r0,[sp,#0x44]` take them from the image (`+8` = stride, `+0x10` = data). | `0x008C5F30`/`0x008C5F32`/`0x008C5F34`; `0x008C609A` | M11-005 | EXACT_SOURCE |
| G8.1b | The projected sample `(X, Y)` (s22 = X, s17 = Y) is bounded by `floorX = floor(X)`, `ceilX = ceil(X)`, `floorY = floor(Y)`, `ceilY = ceil(Y)`, and the four guard branches reject out-of-image samples: `floor(X) < 0` (`0x008C5FB4: bmi`), `ceil(Y) > rows-1` (`0x008C5FCE: bgt`, bound `[sp+0x68] = s30-1 = image rows-1`, set at `0x008C5EB4`), `floor(Y) < 0` (`0x008C5FDE: bmi`), `ceil(X) > cols-1` (`0x008C5FF2: bgt`, bound `[sp+0x64] = s17-1 = image cols-1`, set at `0x008C5EBC`). On any violation the sample is skipped (`0x008C618A`). | `0x008C5FB4`/`0x008C5FCE`/`0x008C5FDE`/`0x008C5FF2`; bounds `0x008C5EB4`/`0x008C5EBC` | M11-005 | EXACT_SOURCE |
| G8.1c | The four pixels read are `img[floorY, floorX]` (r3/s6), `img[floorY, floorX+1]` (r0/s4), `img[ceilY, floorX]` (r7/s2), `img[ceilY, floorX+1]` (r1/s0), via `mla r0,r1,r5,r2` (r5 = floorY) and `mla r1,r1,r7,r2` (r7 = ceilY), then `ldrb` at `+r2` (r2 = floorX) and `+1`. | `0x008C609E`/`0x008C60A8`; `0x008C60B6: ldrb r7,[r1,r2]`; `0x008C60BA: ldrb r3,[r0,r2]`; `0x008C60BC: ldrb r1,[r1,#1]`; `0x008C60D2: ldrb r0,[r0,#1]` | M11-005 | EXACT_SOURCE |
| G8.1d | Weights: `fracX = s22 = X - floor(X)` (`0x008C6026`), `1-fracX = s30` (`0x008C6066: vsub.f32 s30,s18,s22`, s18 = 1.0), `fracY = s17 = Y - floor(Y)` (`0x008C6022`), `1-fracY = s24` (`0x008C6062: vsub.f32 s24,s18,s17`). The value is `(1-fracY)*[(1-fracX)*img[floorY,floorX] + fracX*img[floorY,floorX+1]] + fracY*[(1-fracX)*img[ceilY,floorX] + fracX*img[ceilY,floorX+1]]`. Pairing in the instructions: `0x008C60E6: vmul s0,s22,s0` (ceilY,col+1), `0x008C60EE: vmul s2,s30,s2` (ceilY,col), `0x008C60F2: vmul s4,s22,s4` (floorY,col+1), `0x008C60F6: vmul s6,s30,s6` (floorY,col), `0x008C60FA: vadd s0,s2,s0`, `0x008C60FE: vadd s2,s6,s4`, `0x008C6102: vmul s0,s17,s0`, `0x008C6106: vmul s2,s24,s2`, `0x008C610A: vadd s0,s2,s0`. | `0x008C6062`/`0x008C6066`/`0x008C60E6..0x008C610A` | M11-005 | EXACT_SOURCE |
| G8.1e | The residual is `value - [sp+0x40]` then scaled by `1/255` (`0x008C6112: vsub.f32 s0,s0,s2`; `0x008C6116: vmul.f32 s0,s0,s2`; constant at `0x8C6474 = 0x3b808081 = 0.003921569`). `[sp+0x40]` is `(bright+dark)/2` (`0x008C5EAC: vstr s0,[sp,#0x40]`). | `0x008C6112`/`0x008C6116`; data `0x8C6474` | M11-005 | EXACT_SOURCE |

### G8.2 The 8-element design vector (`[sp+0x25c]`, filled `0x008C5CE6..0x008C5D86`)

The eight rows of `Array<float>(8, N)` are pointed to by `[sp+0x25C..0x27B]`
(`0x008C5CB2: str.w r0,[r3,r2,lsl#2]`, `r3 = sp+0x25c`, advanced by the array stride
`[sp+0x288]`). Per sample, the loop loads `x = xSquare` (array at `[sp+0x2F0]`, the
"xSquare Array." of the allocation error at `0x8C5BD0`), `y = ySquare` (array at
`[sp+0x2D8]`, "ySquare Array." at `0x8C5C04`), `Tx` (array at `[sp+0x2C0]`, "Tx Array." at
`0x8C5C1C`) and `Ty` (array at `[sp+0x2A8]`, "Ty Array." at `0x8C5C2C`).

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.2a | Inputs: `0x008C5CEC: vldr s0,[lr]` (s0 = ySquare sample, lr = `[sp+0x5c]` = ySquare data), `0x008C5CF4: vldr s2,[r2]` (s2 = xSquare sample, r2 = `[sp+0x50]` = xSquare data), `0x008C5CFE: vldr s14,[r3]` (s14 = Tx sample, r3 = old `sl` = Tx data), `0x008C5D06: vldr s12,[r6]` (s12 = Ty sample, r6 = old `sb` = Ty data). | `0x008C5CEC`/`0x008C5CF4`/`0x008C5CFE`/`0x008C5D06`; data pointers `0x008C5C66`/`0x008C5C72`/`0x008C5C76`/`0x008C5C7A` | M11-005 | EXACT_SOURCE |
| G8.2b | Entry 0 (`[sp+0x25C]`) = `Tx * x`: `0x008C5D16: vmul.f32 s1,s2,s14`, stored `0x008C5D32: vstr s1,[r4]`. | `0x008C5D16`/`0x008C5D32` | M11-005 | EXACT_SOURCE |
| G8.2c | Entry 1 (`[sp+0x260]`) = `Tx * y`: `0x008C5D1A: vmul.f32 s3,s0,s14`, stored `0x008C5D3C: vstr s3,[r7]`. | `0x008C5D1A`/`0x008C5D3C` | M11-005 | EXACT_SOURCE |
| G8.2d | Entry 2 (`[sp+0x264]`) = `Tx`: `0x008C5D4C: vstr s14,[r4]`. | `0x008C5D4C` | M11-005 | EXACT_SOURCE |
| G8.2e | Entry 3 (`[sp+0x268]`) = `Ty * x`: `0x008C5D1E: vmul.f32 s2,s2,s12`, stored `0x008C5D52: vstr s2,[sl]`. | `0x008C5D1E`/`0x008C5D52` | M11-005 | EXACT_SOURCE |
| G8.2f | Entry 4 (`[sp+0x26C]`) = `Ty * y`: `0x008C5D36: vmul.f32 s0,s0,s12`, stored `0x008C5D5A: vstr s0,[sb]`. | `0x008C5D36`/`0x008C5D5A` | M11-005 | EXACT_SOURCE |
| G8.2g | Entry 5 (`[sp+0x270]`) = `Ty`: `0x008C5D62: vstr s12,[r1]`. | `0x008C5D62` | M11-005 | EXACT_SOURCE |
| G8.2h | Entry 6 (`[sp+0x274]`) = `-(Tx*x*x + Ty*x*y)`: intermediates `0x008C5CFA: vmul s8,s0,s0` (y^2), `0x008C5D02: vnmul s6,s2,s2` (-x^2), `0x008C5D0A: vmul s4,s2,s0` (x*y), `0x008C5D22: vmul s8,s8,s12` (y^2*Ty), `0x008C5D26: vmul s6,s6,s14` (-x^2*Tx), `0x008C5D2A: vmul s4,s4,s12` (x*y*Ty), then `0x008C5D42: vsub.f32 s4,s6,s4` = `-(x^2*Tx + x*y*Ty)`, stored `0x008C5D68: vstr s4,[r0]`. | `0x008C5D02`/`0x008C5D0A`/`0x008C5D22`/`0x008C5D26`/`0x008C5D2A`/`0x008C5D42`/`0x008C5D68` | M11-005 | EXACT_SOURCE |
| G8.2i | Entry 7 (`[sp+0x278]`) = `-(Tx*x*y + Ty*y*y)`: `0x008C5D10: vnmul s10,s2,s0` (-x*y), `0x008C5D2E: vmul s10,s10,s14` (-x*y*Tx), `0x008C5D48: vsub.f32 s6,s10,s8` = `-(x*y*Tx + y^2*Ty)`, stored `0x008C5D6E: vstmia r5!,{s6}`. | `0x008C5D10`/`0x008C5D2E`/`0x008C5D48`/`0x008C5D6E` | M11-005 | EXACT_SOURCE |

So, in order: `Tx*x, Tx*y, Tx, Ty*x, Ty*y, Ty, -(Tx*x*x + Ty*x*y), -(Tx*x*y + Ty*y*y)`,
matching the manifest's M11-005 evidence exactly.

### G8.3 `DotDivide` operand plumbing (`0x008C62A0..0x008C634A`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.3a | Gate: `s0 = |H22 - 1|`; skip if `< 1e-5`. `0x008C62AA: vldr s0,[r2,#8]`; `0x008C62B0: vadd.f32 s0,s0,s2` (s2 = -1.0); `0x008C62BC: vneg.f32 s2,s0`; `0x008C62C0: it mi` / `0x008C62C2: vmovmi.f32 s0,s2`; `0x008C62C6: vldr s2,[pc,#0x23c]` -> `0x8C6504 = 0x3727C5AC = 1e-5`; `0x008C62D0: vcmpe.f32 s0,s2`; `0x008C62DA: bmi #0x8c634e` (skip the divide). | `0x008C62AA..0x008C62DA`; data `0x8C6504` | M11-005 | EXACT_SOURCE |
| G8.3b | The numerator expression is a `ConstArraySlice<float>` over the refined homography `Array<float>` at `[sp+0x200]` (`dim0/dim1/stride/flags/data` = `[sp+0x200]`/`[sp+0x204]`/`[sp+0x208]`/`[sp+0x20C]`/`[sp+0x210]`). A copy of that descriptor is built at `[sp+0x304..0x314]` (`0x008C62DC: ldrd r2,r3,[sp,#0x200]`; `0x008C62E4: strd r2,r3,[sp,#0x304]`; `0x008C62E8: strd r1,r5,[sp,#0x30c]`; `0x008C62EE: str r0,[sp,#0x314]`) and the `ConstArraySlice<float>` ctor (`0x008C62F2: blx #0x4d1c48`) builds the expression at `[sp+0xa0]`. | `0x008C62DC..0x008C62F2` | M11-005 | EXACT_SOURCE |
| G8.3c | The divisor is `H22 = *(float*)(data + 2*stride + 8)`, i.e. element `[2][2]`: `0x008C6304: add.w r7,r5,r3,lsl#1` (r5 = data `[sp+0x210]`, r3 = stride `[sp+0x208]`); `0x008C6308: ldr r7,[r7,#8]`; `0x008C630A: str r7,[sp,#0x48]`. It is passed as the `float` operand in `r1` at `0x008C6344: ldrd r2,r1,[sp,#0x44]`. | `0x008C6304`/`0x008C6308`/`0x008C630A`/`0x008C6344` | M11-005 | EXACT_SOURCE |
| G8.3d | The output is an `ArraySlice<float>` over the same homography: `0x008C6310: add r0,sp,#0x6c`; `0x008C6312: blx #0x4d1c54` (`ArraySlice<float>::ArraySlice(Array<float>)`); its fields are forwarded to `r2`/`r3` at `0x008C6316..0x008C6348`. | `0x008C6310..0x008C6348` | M11-005 | EXACT_SOURCE |
| G8.3e | The call is `0x008C634A: blx #0x4d22b4` = `Matrix::Elementwise::ApplyOperation<float, DotDivide<float,float,float>, float>(ConstArraySliceExpression<float> const&, float, ArraySlice<float>)`; i.e. the whole 3x3 is divided elementwise by `H22`, in place. | `0x008C634A`; PLT `0x4d22b4` | M11-005 | EXACT_SOURCE |

### G8.4 `Matrix::MakeSymmetric` (`0x0088DDC0..0x0088DE30`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.4a | Requires a square array: `0x0088DDC4: ldrd ip,r2,[r0]` (dim0, dim1); `0x0088DDC8: cmp ip,r2`; `0x0088DDCA: bne #0x88de10` -> log `"Invalid objects"` (`0x88de1c`) and return `0x5000000`. If `dim0 < 1` return 0. | `0x0088DDC4..0x0088DE0E` | M11-005 | EXACT_SOURCE |
| G8.4b | Mirror direction depends on the bool arg: if `bool == false` (`0x0088DDE0: cmp r1,#0` not taken) the inner loop runs `r4 = 0..r3-1` and writes `A[r3][r4] = A[r4][r3]` (copies the upper triangle into the lower); if `bool != 0` it runs `r4 = r3+1..dim0-1` and writes the same (copies the lower triangle into the upper). Body: `0x0088DDF2: mla r7,r5,r4,r6` (row r4), `0x0088DDF6: mla r5,r3,r5,r6` (row r3), `0x0088DDFA: ldr r6,[r7,r3,lsl#2]` (A[r4][r3]), `0x0088DDFE: str r6,[r5,r4,lsl#2]` (A[r3][r4]). | `0x0088DDE0..0x0088DE08` | M11-005 | EXACT_SOURCE |

`RefineQuadrilateral` calls it with `false` (`0x008C61DC: movs r1,#0`; `0x008C61E0: blx #0x4d0b50`),
so it mirrors the upper triangle into the lower.

### G8.5 `Matrix::SolveLeastSquaresWithCholesky` (`0x0088DE68..0x0088E142`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.5a | Guards: `AreValid(A, b)` (`0x0088DE8C`); if false -> log `"Invalid objects"` and return `0x4000000`; require `A.dim0 == A.dim1` (`0x0088DE96: ldr r0,[fp,#4]` / `0x0088DE9C: cmp r4,r0` / `bne 0x88e102` -> `0x5000000`) and `b.dim1 == A.dim0` (`0x0088DEA2: ldr r0,[sl,#4]` / `0x0088DEA8: cmp r0,ip` / `bne 0x88e112` -> `0x5000000`); require `dim0 >= 1`. `*converged = false` at `0x0088DE86`. | `0x0088DE86..0x0088DEB0`; error returns `0x0088E0FC`/`0x0088E12C` | M11-005 | EXACT_SOURCE |
| G8.5b | Factorisation, **pivot order natural** `i = 0..n-1` (outer loop `0x0088DEC2`..`0x0088DF86`). For each `j < i`: `L[i][j] = (A[i][j] - sum_{k<j} L[i][k]*L[j][k]) / L[j][j]`, written in place to `A[i][j]` (`0x0088DEDE..0x0088DF22`; the inner sum `0x0088DEF4: vldr s2,[r3]` / `0x0088DEFA: vldr s4,[r0]` / `0x0088DF04: vmul` / `0x0088DF08: vsub`; the divide-by-pivot `0x0088DF1A: vmul s0,s0,s2` with `s2 = A[j][j]`). | `0x0088DEDE..0x0088DF22` | M11-005 | EXACT_SOURCE |
| G8.5c | Pivot: `p = A[i][i] - sum_{k<i} L[i][k]^2` (`0x0088DF24..0x0088DF46`; `0x0088DF2A: vldr s0,[r5]` = A[i][i]; `0x0088DF3C: vmul.f32 s2,s2,s2`; `0x0088DF40: vsub.f32 s0,s0,s2`). | `0x0088DF24..0x0088DF46` | M11-005 | EXACT_SOURCE |
| G8.5d | **Failure condition:** if `p < FLT_EPSILON` the factorisation stops: `0x0088DF50: vcmpe.f32 s0,s18` with `s18 = [pc,#0x2f4]` -> `0x0088E1B0 = 0x34000000 = 1.1920929e-07` (`0x0088DEB8`); `0x0088DF58: bmi.w #0x88e132` -> `0x0088E132: movs r0,#1` / `0x0088E134: strb.w r0,[sb]` (sets the `converged` out-flag true) / `0x0088E138: movs r0,#0` (returns Result 0). Otherwise `A[i][i] = 1/sqrt(p)` (`0x0088DF5C: vsqrt.f32 s2,s0`; `0x0088DF7A: vdiv.f32 s0,s16,s2` with s16 = 1.0; `0x0088DF82: vstr s0,[r5]`). | `0x0088DEB8`; `0x0088DF50..0x0088DF82`; `0x0088E132..0x0088E138`; data `0x88E1B0` | M11-005 | EXACT_SOURCE |
| G8.5e | Forward substitution (`0x0088DF88..0x0088DFF6`): for each `i`, for each `b` row `r`: `b[r][i] = (b[r][i] - sum_{k<i} A[i][k]*b[r][k]) * A[i][i]` (the last factor is the stored `1/L[i][i]`). | `0x0088DFC6..0x0088DFEC` | M11-005 | EXACT_SOURCE |
| G8.5f | Backward substitution (`0x0088DFF8..0x0088E090`): for `i = n-1..0`, for each `b` row: `b[r][i] = (b[r][i] - sum_{k>i} A[i][k]*b[r][k]) * A[i][i]`. | `0x0088E028..0x0088E080` | M11-005 | EXACT_SOURCE |
| G8.5g | Optional normalisation (`0x0088E092..0x0088E0DE`) runs only if the bool argument (`[sp+0xc]`) is 1: invert the diagonal (`0x0088E0C2: vdiv.f32 s0,s16,s0` / `0x0088E0C6: vstr s0,[r0,#-4]`) and `memclr4` the strict triangle. `RefineQuadrilateral` passes `0` (`0x008C61F2: movs r2,#0`), so this block is **not** run on the live path. | `0x0088E092..0x0088E0DE`; `0x008C61F2` | M11-005 | EXACT_SOURCE |

Note (behaviour to check, not settled here): the `converged` out-flag is set true only on
`p < FLT_EPSILON`, and `RefineQuadrilateral` treats the flag being true as the accept path
(`0x008C62CA..0x008C62D4` stores `1` at `[sp+0x60]`; `0x008C6418: cmp r0,#1` / `0x008C641C: bne 0x8c644a`
takes the accept branch). So a near-singular normal matrix takes the same branch as a
completed solve. This is reported mechanically; the manager may want to confirm the intended
semantics before M11-005's failure path is called EXACT_SOURCE.

---

## G9. M11-003 remainder - cube size, marker size, face poses and codes

### G9.1 `Block::LookupBlockInfo` (`0x004E4C8C..0x004E5104`)

The function builds a `std::map<ObjectType, BlockInfoTableEntry_t>` of **four** entries and
returns `map.find(ObjectType)+0x14` (the value), or the map's end+0x14 if absent
(`0x004E50C2..0x004E50FE`). Each entry is keyed by `ObjectType` (1,2,3,4), carries a name,
a colour, three equal `44.0f` dimensions and a `std::vector<BlockFaceDef_t>` of six 0x10-byte
records.

| # | ObjectType key | name string | colour | dimensions | entry build cite |
|---|---|---|---|---|---|
| G9.1a | 1 | `"LIGHTCUBE1"` (`0x4E5314`) | `NamedColors::ORANGE` (`GOT 0x103E7D8`) | `(44.0, 44.0, 44.0)` | `0x004E4D2E: str r0,[sp,#0xc0]` (r0=1); `0x004E4CC4` name; `0x004E4CDA` colour; `0x004E4CD6: movt r1,#0x4230` -> `0x42300000 = 44.0`, `0x004E4CDC: strd r1,r1,[sp,#0xa0]` / `0x004E4CE0: str r1,[sp,#0xa8]`; copied to entry `+0x14/+0x18/+0x1c` at `0x004E4D58..0x004E4D60` |
| G9.1b | 2 | `"LIGHTCUBE2"` (`0x4E5328`) | `NamedColors::YELLOW` (`GOT 0x103E7DC`) | `(44.0, 44.0, 44.0)` | `0x004E4D8E: movt r1,#0x4230`; entry key 2 at `0x004E4DF0: str r0,[sp,#0xf0]` |
| G9.1c | 3 | `"LIGHTCUBE3"` (`0x4E533C`) | `NamedColors::RED` (`GOT 0x103E7E0`) | `(44.0, 44.0, 44.0)` | `0x004E4E46: movt r1,#0x4230`; entry key 3 at `0x004E4EA4: str r0,[sp,#0x120]` |
| G9.1d | 4 | `"LIGHTCUBE_GHOST"` (`0x4E5350`) | `NamedColors::WHITE` (`GOT 0x103E7E4`) | `(44.0, 44.0, 44.0)` | `0x004E4EFA: movt r1,#0x4230`; entry key 4 at `0x004E4F58: str r0,[sp,#0x150]` |

The `44.0f` value is the block edge: `Block::Block` halves the size getter's three components
in `AddFace` (`0x004E5402: vldr s16,[r0,#4]`; `0x004E540E: vldr s18,[r0,#8]`; `0x004E5424: vldr s2,[r0]`;
`0x004E5420: vmov.f32 s0,#0.5`; `0x004E542C/0x004E5430/0x004E5434: vmul`), and the face
translations are all at `+-22` (see G9.3), so the three components are `44.0` each. The
getter is a `Block` virtual at vtable slot `+0x18` (`0x004E53FC: ldr r1,[r0,#0x18]` /
`0x004E5400: blx r1`); the exact `this` offset of the returned `Point<3,float>` was not
pinned (the `BlockInfo` entry stores the three values at `+0x14/+0x18/+0x1c`; the block copy
at `0x004E5F80` moves `BlockInfo+0x10/+0x14/+0x18` to `Block+0x88/+0x8c/+0x90`). The value is
settled; only the field name is a minor open point.

### G9.2 Marker size

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G9.2a | Each `BlockFaceDef_t` (0x10 bytes) is `{ u32 faceName @0; u32 markerType @4; float size @8; u32 packed @0xc }`; the six records per cube all carry `size = 25.0` (`0x41C80000`). | `0x004E4C8C` table: `+0x08 = 00 00 c8 41` in every record; `0x004E4C40..0x004E4D60` data at `0xC45C40` (LIGHTCUBE1), `0xC45CA0` (2), `0xC45D00` (3), `0xC45D60` (GHOST) | M11-003 | EXACT_SOURCE |
| G9.2b | `Block::Block` passes that `float` as `AddFace`'s third argument (`0x004E6006: ldr r3,[r2,#8]` / `0x004E6010: blx #0x4a4828`), and `AddFace` builds a `Point<2,float>` from it (both components equal) and hands it to `ObservableObject::AddMarker` (`0x004E5648: vmov s0,sb`; `0x004E565A..0x004E5664: vstr s0,[r2]` twice; `0x004E566E: blx #0x4a4798`). | `0x004E6006`/`0x004E6010`; `0x004E5648..0x004E566E` | M11-003 | EXACT_SOURCE |
| G9.2c | `KnownMarker::KnownMarker(short const&, Pose3d const&, Point<2,float> const&)` (`0x0087E22C`) stores that `Point<2,float>` at `KnownMarker+0x10` (`0x0087E26E: ldrd r0,r1,[r7]` / `0x0087E272: strd r0,r1,[r5,#0x10]`). `KnownMarker::Get3dCorners` scales the canonical `+-0.5` corners by `size.x`/`size.y` (`0x0087E312: vldr s4,[r1,#0x10]`; `0x0087E322: vldr s0,[r1,#0x14]`; `0x0087E31A`/`0x0087E326: vmul`), so the marker's physical extent is `25 mm` square. | `0x0087E22C`; `0x0087E26E`/`0x0087E272`; `0x0087E308..0x0087E332` | M11-003 | EXACT_SOURCE |

So the marker's physical size on the live path is **25.0** (both in-plane dimensions), taken
from the `BlockFaceDef_t` size field; there is no separate global marker-size constant.

### G9.3 `Block::AddFace` per-face bodies (`0x004E53BC..0x004E5688`)

Prologue: `r4 = FaceName`, `r8 = &MarkerType`, `sb = size`; the switch is
`0x004E5438: tbb [pc,r4]` with table at `0x4E543C` (`03 a0 57 7c 2a cd`), i.e.
`target = 0x4E543C + 2*byte`. Half-sizes: `s18 = size[0]/2`, `s16 = size[1]/2`,
`s20 = size[2]/2` (all `22.0` for the 44 mm cube). Each case builds a `Pose3d` with
`Pose3d::Pose3d(Radians, Point3 axis, Point3 translation, string)` (`0x4A478C`).

| FaceName | case VA | rotation | axis | translation | cite |
|---|---|---|---|---|---|
| 0 | `0x004E5442` | `-pi/2` (`r1 = 0xBFC90FDB`, `0x004E5448: movt r1,#0xbfc9`) | `Z_AXIS_3D()` (`0x004E5450`) | `(-22, 0, 0)` (`0x004E5456: vneg.f32 s0,s18`; `0x004E5466: vstr s0,[sp,#0x2c]`) | `0x004E5442..0x004E5482` |
| 1 | `0x004E557C` | `+pi` (`r1 = 0x40490FDB`, `0x004E5582: movt r1,#0x4049`) | `Z_AXIS_3D()` (`0x004E558A`) | `(0, 22, 0)` (`0x004E5590: vstr s16,[sp,#0x30]`) | `0x004E557C..0x004E55B8` |
| 2 | `0x004E54EA` | `+pi/2` (`r1 = 0x3FC90FDB`, `0x004E54F0: movt r1,#0x3fc9`) | `Z_AXIS_3D()` (`0x004E54F8`) | `(22, 0, 0)` (`0x004E5506: vstr s18,[sp,#0x2c]`) | `0x004E54EA..0x004E5526` |
| 3 | `0x004E5534` | `0` (`0x004E5536: movs r1,#0`) | `Z_AXIS_3D()` (`0x004E553C`) | `(0, -22, 0)` (`0x004E5542: vneg.f32 s0,s16`; `0x004E5552: vstr s0,[sp,#0x30]`) | `0x004E5534..0x004E556E` |
| 4 | `0x004E5490` | `2*pi/3` (`r1 = 0x40060A92`, `0x004E5496: movt r1,#0x4006`) | `(-0.57735, +0.57735, -0.57735)` (`0x004E54AC`/`0x004E54B6`/`0x004E54B8`; const `0x3F13CD3A`/`0xBF13CD3A`) | `(0, 0, 22)` (`0x004E54BE: vstr s20,[sp,#0x28]`) | `0x004E5490..0x004E54DC` |
| 5 | `0x004E55D6` | `2*pi/3` (`r1 = 0x40060A92`) | `(+0.57735, -0.57735, -0.57735)` (`0x004E55F2`/`0x004E55F8`/`0x004E55FC`) | `(0, 0, -22)` (`0x004E55E4: vneg.f32 s0,s20`; `0x004E5608: vstr s0,[sp,#0x28]`) | `0x004E55D6..0x004E5626` |

At the end of `AddFace` the marker is added: `0x004E564C: ldr.w r1,[r8]` (the `MarkerType`'s
low `short`), `0x004E5654: strh.w r1,[sp,#0x2c]`, and
`0x004E566E: blx ObservableObject::AddMarker(short const&, Pose3d const&, Point<2,float> const&)`
(PLT `0x4A4798`); the returned index is stored at `block + 0x70 + faceName*4`
(`0x004E5672: add.w r1,r5,r4,lsl#2` / `0x004E5676: str r0,[r1,#0x70]`).

### G9.4 Face-to-marker-code map

`Block::Block(ObjectFamily, ObjectType)` (`0x004E5F50`) iterates the `BlockInfo`'s face vector:
`0x004E5FF8: ldrd r2,r5,[r0,#0x20]` (begin/end), `0x004E6002: ldr r1,[r2]` = faceName,
`0x004E6004: adds r7,r2,#4` = `&MarkerType`, `0x004E6006: ldr r3,[r2,#8]` = size,
`0x004E6010: blx AddFace`. The `MarkerType`'s code is its first `short`. The records give:

| cube (key) | face 0 | face 1 | face 2 | face 3 | face 4 | face 5 |
|---|---|---|---|---|---|---|
| LIGHTCUBE1 (1) | 6 | 7 | 4 | 8 | 9 | 5 |
| LIGHTCUBE2 (2) | 12 | 13 | 10 | 14 | 15 | 11 |
| LIGHTCUBE3 (3) | 18 | 19 | 16 | 20 | 21 | 17 |
| LIGHTCUBE_GHOST (4) | 39 | 39 | 39 | 39 | 39 | 39 |

The `u32` at record `+0xc` (e.g. `0x00000F05`, `0x00000F00`, `0x00000F0F`) is **not** passed to
`AddFace` on this path: the constructor supplies `0,0` for the two `u8` arguments
(`0x004E600C: strd r6,r6,[sp]` with `r6 = 0` at `0x004E6000`), and `AddFace` does not read
them in the body read here. So the packed word is unused by this constructor.

### G9.5 KnownMarker canonical corners and code

`KnownMarker::_canonicalCorners3d` is the 48-byte `Quadrilateral<3,float>` at `0x0105E0F0`
(already in G4: `(-0.5,0,-0.5)`, `(-0.5,0,0.5)`, `(0.5,0,0.5)`, `(0.5,0,-0.5)`), and
`KnownMarker::Get3dCorners` (`0x0087E2E8`) copies it and scales x by `size.x` (`+0x10`) and z
by `size.y` (`+0x14`), then applies the marker pose (`0x0087E336: GetTransform`,
`0x0087E33E: Transform3d::ApplyTo<float>`). The `KnownMarker` code is the `short` stored at
`KnownMarker+0` (`0x0087E238: ldrh r0,[r1]` / `0x0087E23E: strh r0,[r4],#4`).

---

## G10. M11-004 remainder - connected-object rule, moving and rotating gates

### G10.1 The connected-object rule (`BlockWorld::AddAndUpdateObjects`, `0x00620AD4`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G10.1a | For an observed active object, the engine asks whether a **connected** active object with the same `ObjectID` exists: `0x00620E70: ldr r0,[sp,#0x118]` (the observed object); `0x00620E72: add.w r1,r0,#0x14` (`&ObjectID`); `0x00620E76: mov r0,fp` (`BlockWorld`); `0x00620E78: blx #0x4a6fb8` (`BlockWorld::GetConnectedActiveObjectByIdHelper(ObjectID const&) const`, body `0x0061F58C`). | `0x00620E70..0x00620E78`; body `0x0061F58C` | M11-004 | EXACT_SOURCE |
| G10.1b | If the lookup returns null the engine warns and rate-limits; it does **not** drop the object in this path. `0x00620E7C: cbnz r0,#0x620ede` (found -> continue); otherwise `0x00620E88: ldr r0,[r0,#0x4c]` / `0x00620E8A: blx EnumToString(ObjectType)` and `0x00620E9E: blx Util::sWarningF` with the string `0x00620E9C -> 0xBF854B "Observed active object of type %s but it's not connected. Is the battery plugged in?"`. Then `0x00620EC4..0x00620EDA` stores `map[ObjectID] = now + 10.0` into the `unordered_map<int,float>` (`0x00620ED2: blx unordered_map<int,float>::operator[]`; `0x00620ED6: vadd.f32 s0,s22,s18`; `0x00620EDA: vstr s0,[r0]`). | `0x00620E7C..0x00620EDA`; string `0xBF854B`; map type `0x00620E5A` | M11-004 | EXACT_SOURCE |
| G10.1c | The warning is gated by that map: `0x00620E42: blx BaseStationTimer::getInstance` / `0x00620E46: blx GetCurrentTimeInSeconds` (s22); `0x00620E4C..0x00620E5A` read `map[ObjectID]`; `0x00620E66: vcmpe.f32 s22,s0` / `0x00620E6E: blt #0x620ede` (skip the check while `now < map[id]`). The cooldown is `10.0` (`s18 = 10.0` at `0x00620B1E: vmov.f32 s18,#1.000000e+01`), matching the global `kUnconnectedObservationCooldownDuration_sec` at `0x00C781EC = 0x41200000 = 10.0`. | `0x00620B1E`; `0x00620E42..0x00620E6E`; data `0xC781EC` | M11-004 | EXACT_SOURCE |

**Divergence to flag:** the stack's `BlockWorld.AddAndUpdateObject` (line 463) *returns null*
(``drops'') an unconnected active object; the native path read here only warns and records the
10 s cooldown, then continues to `0x00620EDE`. The manager should check whether a drop lives
elsewhere (e.g. in the caller or in `AddConnectedActiveObject`/`FindConnectedActiveMatchingObjects`)
before the record is settled on the C# wording.

### G10.2 Moving gate (`BlockWorld::CheckForUnobservedObjects`, `0x00621C6C`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G10.2a | At the top of `CheckForUnobservedObjects`, if `MovementComponent::WasMoving(timestamp)` is non-zero the whole unobserved pass is skipped: `0x00621C88: ldr.w r0,[r0,#0x254]` (the `MovementComponent`); `0x00621C8C: mov r1,r5` (timestamp); `0x00621C8E: blx #0x4b8010` (`WasMoving(unsigned int)`, body `0x006417DC`); `0x00621C92: cmp r0,#0` / `0x00621C94: bne.w #0x62220c` (skip). | `0x00621C88..0x00621C94`; body `0x006417DC` | M11-004 | EXACT_SOURCE |
| G10.2b | `MovementComponent::WasMoving` consults the robot state history: `0x006417F2: ldr r6,[r0,#4]` (the `RobotStateHistory`), builds a predicate, and calls the helper `0x00641898` (`GetRawStateAt(timestamp)` + the predicate `std::function<bool(HistRobotState const&)>` at `0x00641956`). The predicate's exact "moving" test was not decoded (it is an unnamed lambda invoked through the `std::function`); the gate itself is `WasMoving(timestamp) != 0`. | `0x006417F2`/`0x00641898`/`0x00641956` | M11-004 | EXACT_SOURCE for the gate; RECOVERABLE_GAP for the predicate body (read the lambda invoked at `0x00641956`) |

### G10.3 Rotating gate (`VisionComponent::WasRotatingTooFast`, `0x0065359C`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G10.3a | The threshold passed by `CheckForUnobservedObjects` is `0.174533` rad/s (`= 10 deg/s`): `0x00621C9A: movw r2,#0xb8c2` / `0x00621C9E: movt r2,#0x3e32` -> `0x3E32B8C2 = 0.174533`; `0x00621CA4: mov r3,r2`; `0x00621CAA: str r1,[sp]` (`r1 = 0`); `0x00621CAE: blx #0x4a5bb4` (`WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0)`); `0x00621CB2: cmp r0,#0` / `0x00621CB4: bne.w #0x62220c` (skip). | `0x00621C98..0x00621CB4` | M11-004 | EXACT_SOURCE |
| G10.3b | `WasRotatingTooFast` = head OR body: `0x006535AC: blx #0x4ba404` (`WasHeadRotatingTooFast(timestamp, thresh, int)`, body `0x00656260`); if true return 1 (`0x006535B2`); else tail-call `WasBodyRotatingTooFast` (`0x006535C6 -> 0x8cd09c`, body `0x00656384`). | `0x006535AC`/`0x006535C6`; bodies `0x00656260`/`0x00656384` | M11-004 | EXACT_SOURCE (the criterion inside the two sub-functions was not transcribed) |

### G10.4 ObjectPoseConfirmer / ObservableObject match thresholds

`ObjectPoseConfirmer::FindObjectMatchForObservation` (`0x005063CC`) matches an observation to a
located object through `BlockWorld::FindLocatedClosestMatchingObjectHelper(object, Point3, Radians, filter)`
(`0x0050658E`) and `Vision::ObservableObject::IsSameAs(object, Point3, Radians, ...)`
(`0x0050660A`). The two thresholds are **per-object virtuals**, not literals in this function:

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G10.4a | The distance threshold is the object's base `Point<3,float>` scaled by `0.8`: `0x0050656C: ldr r0,[r7]` (vptr); `0x0050656E: ldr r2,[r0,#0x30]`; `0x00506570..0x00506574` (virtual call, out at `sp+0xc0`); the thunk body is `0x004E025C` (`0x004E0262: ldr r2,[r0,#0x2c]` calls the base virtual; `0x004E0268: vldr s0,[pc,#0x20]` -> `0x004E028C = 0x3F4CCCCD = 0.8`; `0x004E0276..0x004E0288` multiplies the three components by 0.8). | `0x0050656C..0x0050658E`; thunk `0x004E025C`; data `0x004E028C` | M11-004 | EXACT_SOURCE (value); RECOVERABLE_GAP for the base virtual at vptr `+0x2c` (its name/meaning) |
| G10.4b | The rotation threshold is `Radians(45 deg)`: `0x0050657A: ldr r2,[r0,#0x34]`; the thunk body is `0x004E0290` (`0x004E0292: movw r1,#0xfdb` / `0x004E0296: movt r1,#0x3f49` -> `0x3F490FDB = 0.785398 = pi/4`; `0x004E029A: blx Radians::Radians`). | `0x0050657A`; thunk `0x004E0290` | M11-004 | EXACT_SOURCE |

So the object-matching gate is `IsSameAs` with translation tolerance `0.8 * (object base
extent)` and rotation tolerance `45 deg`; the robot-motion gate is `WasMoving(timestamp)`
plus `WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0)`.

---

## Existing records contradicted or too weak (from this pass)

- **M11-018** (`IMPLEMENTATION_GAP`). Its evidence line
  `"IsQuadrilateralReasonable 0x00892B18 (minQuadArea 25, symmetry 512 8.8, minDistanceFromEdge 2) -- live"`
  locates the parameters at the function entry. They are not literals there; they are
  `MarkerDetector::Parameters+0x30/+0x34/+0x38`, written at `0x00875338`/`0x00875340` and read
  at `0x00898E00`/`0x00898E0E` (G7.1). Replace the citation; the values themselves are right.
- **M11-003** (`EXACT_SOURCE`). The evidence is still bare symbol names (`Block::LookupBlockInfo`,
  `Block::AddFace`, `KnownMarker::_canonicalCorners3d`). This pass supplies the addresses and
  values (G9.1-G9.5): the four-entry table at `0x004E4C8C` (`44.0` cubes; keys 1..4; the six
  face records at `0xC45C40`/`0xC45CA0`/`0xC45D00`/`0xC45D60`; marker size `25.0`; the six
  `AddFace` cases; the face->code map). The record still needs re-citing from the addresses.
- **M11-004** (`EXACT_SOURCE`). The evidence is still the prose
  `"80 mm, 45 degrees, 600000 ms thresholds in the position-update strategy"`. G5 gave the
  three thresholds; this pass adds the connected check (G10.1), the robot motion gates
  (G10.2/G10.3) and the object-match thresholds (G10.4). The record needs re-citing.
- **M11-005** (`EQUIVALENT_IMPLEMENTATION`). The manifest's evidence now includes the design
  vector and the bilinear read; this pass confirms both instruction for instruction (G8.2,
  G8.1) and reads the `DotDivide` plumbing (G8.3). The Cholesky body's failure condition is
  `pivot < FLT_EPSILON` (not `pivot <= 0`), and it sets the `converged` out-flag rather than
  returning an error (G8.5d); the caller treats that flag as the accept path (G8 note). The
  manifest's provenance wording ("reports a non-positive pivot as the numerical failure")
  should be tightened to the epsilon condition before the failure path is called exact.
- **M11-032** (`IMPLEMENTATION_GAP`). Its `unresolved` still says
  `"the dark-mask multiplier 0xCCCC belongs to the non-live path"`, which the X1 and gap-1
  passes already contradicted (`0x00898BAA: ldr r5,[r7,#0xc]` -> `0x0088F662: mul r3,r6,r2`).
  Still unfixed; the record cannot be settled until the clause is removed and its evidence
  replaced with instructions.

## Open questions the manager must decide or send back

1. **M11-004 connected-object drop.** The native path read (G10.1) warns and cooldowns but does
   not drop the object; the C# `AddAndUpdateObject` returns null. Is the drop in the native
   caller of `AddAndUpdateObjects`, or is the C# behaviour a `LOCAL_POLICY`? Needs a read of the
   callers of `AddAndUpdateObjects` (`0x00620AD4`) and/or `AddConnectedActiveObject` (`0x62302C`).
2. **M11-004 `WasMoving` predicate.** The gate is `WasMoving(timestamp) != 0`; the lambda's
   predicate body was not decoded (G10.2b). Read the lambda invoked at `0x00641956` if the
   exact "moving" criterion is wanted.
3. **M11-004 base matching extent.** The `0.8` factor is read (G10.4a), but the base virtual at
   `vptr+0x2c` (the un-scaled `Point<3,float>`) was not identified. Read that vtable slot.
4. **M11-005 `converged` semantics.** The flag is set true when `pivot < FLT_EPSILON` and the
   caller accepts on that flag (G8 note). Confirm the intended meaning before calling the
   failure path exact.
5. **M11-003 block-size getter offset.** The dimensions are `(44,44,44)`; the exact `Block`
   field the `+0x18` virtual returns was not pinned (G9.1). A short read of the `Block` vtable
   slot `+0x18` settles the field name.

## What is still unread

1. `BlockWorld::AddAndUpdateObjects` callers and `AddConnectedActiveObject`/`RemoveConnectedActiveObject`
   (`0x62302C`/`0x6243A0`) - only if the connected-object *drop* is required (open question 1).
2. The `WasMoving` predicate lambda invoked at `0x00641956`, and the bodies of
   `WasHeadRotatingTooFast` (`0x00656260`) / `WasBodyRotatingTooFast` (`0x00656384`).
3. The `ObservableObject` vtable slot `+0x2c` (base matching extent) and `Block` vtable slot
   `+0x18` (size getter field).
4. `Block::Block`'s exact copy of the `BlockInfo` fields (the `+0x88..0x9c` block layout) if the
   field names are wanted.
5. `CompressConnectedComponentSegmentIds` remap tail `0x00894AF4..` and the non-live binomial
   bodies remain unread, but neither is on the live path (carried over from gap 1).

*Read-only extraction. Nothing outside `.scratch/I-M11-gap2/` and this report file was changed.*
