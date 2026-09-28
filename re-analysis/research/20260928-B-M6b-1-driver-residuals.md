# B-M6b-1 — M6-002 driver residuals: window combine, emit, work-buffer geometry, derived choices (read-only extraction)

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so` (ARM mode, Wwise 2016.2). All addresses are ELF VAs; `.text` VA == file offset. Instructions were read with capstone; the Ghidra tree and prior reports were used only for navigation. No file in the repo was modified by the extractor.

## Q1 — the window combine `0x00AB5A94`

### Q1.1 Arguments at entry

Prologue `0x00AB5A94: push {r4,r5,r6,r7,r8,sb,sl,fp,lr}` then `0x00AB5A9C: sub sp,sp,#0x1c`; the caller's first stack word is at `[sp+0x40]`.

| # | what the original does | citation | class |
|---|---|---|---|
| Q1.1a | `r0 = bs0` (`setup[0]`), `r1 = bs1` (`setup[4]`), from `ldm r6,{r0,r1}` at the top of the per-channel loop | `0x00AB3600: ldm r6,{r0,r1}`; `0x00AB3618..0x00AB3664` | EXACT_SOURCE |
| Q1.1b | `r2 = dsp+0x24` value (previous block flag), `r3 = dsp+0x28` value (current block flag) — values, not pointers | `0x00AB362C: ldr r2,[r4,#0x24]`; `0x00AB3640: ldr r3,[r4,#0x28]`; callee `0x00AB5A98: adds r4,r3,#0`; `0x00AB5AA4: adds lr,r2,#0` | EXACT_SOURCE |
| Q1.1c | arg5 `[sp+0] = dsp+0x14[ch]` — current block MDCT buffer | `0x00AB3620: ldr fp,[r2,r5,lsl#2]`; `0x00AB3630: str fp,[sp]` | EXACT_SOURCE |
| Q1.1d | arg6 `[sp+4] = dsp+0x18[ch]` — previous/overlap buffer | `0x00AB3634: ldr fp,[r3,r5,lsl#2]`; `0x00AB3658: str fp,[sp,#4]` | EXACT_SOURCE |
| Q1.1e | arg7 `[sp+8] = r8` — window selected by `bs0/2` | `0x00AB365C: str r8,[sp,#8]`; selection `0x00AB3564..0x00AB35E4`; default `0x00AB3728: mov r8,#0` | EXACT_SOURCE |
| Q1.1f | arg8 `[sp+0xc] = sb` — window selected by `bs1/2` | `0x00AB3660: str sb,[sp,#0xc]`; selection `0x00AB35B4..0x00AB35E4`; default `0x00AB3710: mov sb,#0` | EXACT_SOURCE |
| Q1.1g | arg9 `[sp+0x10] = out + param_3*chan_index*4`, chan_index forced to `channels-1` for the last channel | `0x00AB3624: mul ip,r3,ip`; `0x00AB363C: add ip,r3,ip,lsl#2`; `0x00AB3644: str ip,[sp,#0x10]`; force `0x00AB374C..0x00AB3754` | EXACT_SOURCE |
| Q1.1h | arg10 `[sp+0x14] = channels` | `0x00AB35F8: ldr sl,[ip,#0xc]`; `0x00AB364C: str sl,[sp,#0x14]` | EXACT_SOURCE |
| Q1.1i | arg11 `[sp+0x18] = dsp+0x1c` (start skip) | `0x00AB3650: str lr,[sp,#0x18]` | EXACT_SOURCE |
| Q1.1j | arg12 `[sp+0x1c] = r7 + dsp+0x1c` (end = skip + samples) | `0x00AB3648: add ip,r7,lr`; `0x00AB3654: str ip,[sp,#0x1c]` | EXACT_SOURCE |

Callee stack reads: arg5 `[sp+0x40]` (`0x00AB5B04`), arg6 `[sp+0x44]` (`0x00AB5AA8`), arg7 `[sp+0x48]` (`0x00AB5F5C`, `0x00AB5F80`), arg8 `[sp+0x4c]` (`0x00AB5ADC`), arg9 `[sp+0x50]` (`0x00AB5AB4`), arg11 `[sp+0x58]` (`0x00AB5AB8`), arg12 `[sp+0x5c]` (`0x00AB5ABC`). **arg10 (channels) is passed but never read** (no `[sp+0x54]` reference in `0x00AB5A94..0x00AB624C`).

### Q1.2 Top-level branch selection

| # | what the original does | citation | class |
|---|---|---|---|
| Q1.2a | `r4 = (arg4 != 0)`, `lr = (arg3 != 0)`, `sb = r4 & lr`; if **not both flags set** branch to `0x00AB5F44` | `0x00AB5A98: adds r4,r3,#0`; `0x00AB5AA0: movne r4,#1`; `0x00AB5AA4: adds lr,r2,#0`; `0x00AB5AAC: movne lr,#1`; `0x00AB5AB0: ands sb,r4,lr`; `0x00AB5AC0: beq #0xab5f44` | EXACT_SOURCE |
| Q1.2b | both flags set → setup at `0x00AB5AC4`; uses **W1 only** (arg8), sets `[sp+4]=0`, falls into the common code at `0x00AB5AFC` | `0x00AB5AC4: asr fp,r1,#2`; `0x00AB5ADC: ldr r1,[sp,#0x4c]`; `0x00AB5AF0: streq r2,[sp,#4]` | EXACT_SOURCE |
| Q1.2c | `prev!=0,curr==0` (long→short) → `0x00AB621C`: `fp=bs0/2`, `[sp+8]=W0+bs0/2`, `r6=OV+bs1`, `lr=bs1/4-bs0/4`, `fp=sl`, joins `0x00AB5F6C`, ends at `0x00AB6028` (`[sp+4]=0`, `[sp+0x4c]=W0`) → `0x00AB5F94` → negate path `0x00AB5F9C` | `0x00AB621C..0x00AB6248`; `0x00AB6028..0x00AB6034`; `0x00AB5F94: cmp lr,#0`; `0x00AB5F98: beq` not taken | EXACT_SOURCE |
| Q1.2d | `prev==0,curr==0` (short/short) → `0x00AB6028` (`[sp+4]=0`, `[sp+0x4c]=W0`) → `0x00AB5F94` → `lr==0` → common code `0x00AB5AFC` with the small window | `0x00AB5F54: asr r3,r0,#1`; `0x00AB5F5C: ldr r0,[sp,#0x48]`; `0x00AB5F64: add r3,r0,r3,lsl#2`; `0x00AB5F68: str r3,[sp,#8]`; `0x00AB5F7C: beq #0xab6028` | EXACT_SOURCE |
| Q1.2e | `prev==0,curr!=0` (short→long) → `[sp+0x4c]=W0`, `[sp+4]=bs1/4-bs0/4`, then `0x00AB5AFC` with the small window | `0x00AB5F80: ldr r2,[sp,#0x48]`; `0x00AB5F88: str r2,[sp,#0x4c]`; `0x00AB5F8C: rsb r3,r3,sl`; `0x00AB5F90: str r3,[sp,#4]`; `0x00AB5F98: beq #0xab5afc` | EXACT_SOURCE |

The window pointer passed as arg8 is overwritten with W0 whenever `prev==0`; the selected window length is carried in `[sp+8]` (W + h). The `[sp+4]` slot gates the `0x00AB5D88` negate region.

### Q1.3 The arithmetic forms (no FMA)

| form | instructions | per-lane result | citation | class |
|---|---|---|---|---|
| add `a*wA + b*wB` | `vmul.f32 q9,q10,q9`; `vmul.f32 q8,q8,q10`; `vadd.f32 q8,q8,q9` / scalar `vmul.f32 s15,s14,s15`; `vmla.f32 s15,s13,s14` | `out = input*window_fwd + overlap*window_rev` | `0x00AB5BA4`,`0x00AB5BAC`,`0x00AB5BB0`; `0x00AB5C28`,`0x00AB5C34` | EXACT_SOURCE |
| sub `a*wA - b*wB` | `vmul.f32 q9,q9,q10`; `vmul.f32 q8,q11,q8`; `vsub.f32 q8,q8,q9` / scalar `vmul.f32 s15,s14,s15`; `vnmls.f32 s15,s13,s14` | `out = overlap*window_rev - input*window_fwd` | `0x00AB5CF0`,`0x00AB5CF8`,`0x00AB5CFC`; `0x00AB5D58`,`0x00AB5D64` | EXACT_SOURCE |
| negate | `vld1.32 {d16,d17},[sb]`; `vneg.f32 q8,q8`; `vst1.32` / scalar `vldmia sb!,{s15}`; `vneg.f32 s15,s15`; `vstmia ip!,{s15}` | `out = -in` | `0x00AB5DD4`,`0x00AB5DDC`,`0x00AB5DE4`; `0x00AB6038`,`0x00AB603C`,`0x00AB6044`; `0x00AB5E68`,`0x00AB5ED4` | EXACT_SOURCE |
| mirror | `vrev64.32` + `vswp` reverses the 4 lanes; windows read with descending `vldmdb`/`sub` | reversed window indexing `w[h-1-i]` | `0x00AB5B8C`,`0x00AB5B9C`,`0x00AB5BB4`,`0x00AB5BB8`; `0x00AB5CDC`,`0x00AB5CF4`; `0x00AB5C1C: vldmdb r6!`; `0x00AB5C24: vldmdb r1!` | EXACT_SOURCE |

### Q1.4 Region selection and loop bounds

With `fp = n/4` (`n` the selected block size), `SKIP`/`END` = args 11/12:

| region | what the original does | citation | class |
|---|---|---|---|
| Region A — **add** | runs iff `min(END,fp) > min(SKIP,fp)`; output count `= min(END,fp) - min(SKIP,fp)`, written from `OUT[0]`; input and overlap read descending from `IN + fp - min(SKIP,fp)` / `OV + fp - min(SKIP,fp)`; windows forward `W + min(SKIP,fp)` and reverse `W + h - min(SKIP,fp)` | test `0x00AB5B60: cmp r2,r0`; `0x00AB5B64: bhs #0xab5c74`; setup `0x00AB5B68..0xAB5B78`; vector `0x00AB5B7C..0x00AB5BC4`; scalar `0x00AB5C1C..0x00AB5C3C` | EXACT_SOURCE |
| Region B — **sub** | runs iff `r4 > r0` where `r4 = IN + min(END - min(END,fp), fp)*4`, `r0 = OV + min(SKIP - min(SKIP,fp), fp)*4`; count `= min(END-min(END,fp),fp) - min(SKIP-min(SKIP,fp),fp)`, written after A | `0x00AB5C74..0xAB5C9C`; test `0x00AB5CB4: cmp r4,r0`; `0x00AB5CB8: bls #0xab5d88`; vector `0x00AB5CC8..0x00AB5D08`; scalar `0x00AB5D4C..0x00AB5D6C` | EXACT_SOURCE |
| Region C — **negate/copy** | runs only if `[sp+4] != 0`; count `= min(r8-r5,[sp+4]) - min(sb-fp,[sp+4])`, written after A+B | gate `0x00AB5D88: ldr r3,[sp,#4]`; `0x00AB5D8C: cmp r3,#0`; `0x00AB5D90: beq #0xab5d30`; vector `0x00AB5DD4..0x00AB5DEC`; scalar `0x00AB5DFC..0x00AB5F3C` | EXACT_SOURCE |
| prev==0 paths | share the common code with the small window (arg8 replaced by W0, `[sp+8] = W0 + bs0/2`); `prev!=0,curr==0` uses the separate negate path `0x00AB5F9C..0x00AB623C` | `0x00AB5F44..0x00AB5F98`; `0x00AB621C..0x00AB6248`; `0x00AB5F9C..0x00AB623C` | EXACT_SOURCE |

**UNKNOWN:** the semantic mapping of regions A/B/C to libvorbis's large/large, large/small, small/large, small/small cases, and why the fork uses a sub form where upstream `block.c` is add-only. The instruction-level gates are settled; the semantic label is not. **UNKNOWN:** whether region B reads past the overlap buffer for shipped block sizes (see Q3).

### Q1.5 Window tables

- `W0` from `setup+0` (bs0), `W1` from `setup+4` (bs1), keyed on `blocksize/2 ∈ {128,256,512,1024,2048}`, else 0: `0x00AB3564..0x00AB3738`; defaults `0x00AB3728: mov r8,#0`, `0x00AB3710: mov sb,#0`.
- Contiguous rising half-windows; pointers from the PC literals at `0xAB3758..0xAB377C`: `0x0105448C` (h=128), `0x0104690` (h=256), `0x01054A8C` (h=512), `0x010528C` (h=1024), `0x0105628C` (h=2048). First float of the h=256 table ≈ 0.
- Combine indexes forward `W + i` and reversed `W + h - 1 - i`, `h` carried in `[sp+8] = W + h` (`0x00AB5AEC`/`0x00AB5AF4`, `0x00AB5F64`/`0x00AB5F68`); 4-lane reversal is `vrev64.32` + `vswp`.
- bs0 selects W0, bs1 selects W1; both-long uses W1; W0 whenever the previous flag is 0.

### Q1.6 Overlap write-back

The combine does **not** write `dsp+0x18[ch]`; the write-back is in the caller `0x00AB3520`: `memcpy(dsp+0x18[ch], dsp+0x14[ch] + aligned(block_size), aligned(block_size))` (`0x00AB3668..0x00AB3698`). The first-window copy is `0x00AB3814..0x00AB3874`.

## Q2 — the shared emit `0xA73490`

Function range `0x00A73490..0x00A73564`; prologue `push {r3,r4,r5,r6,r7,lr}` (caller arg5 = `[sp+0x18]`, arg6 = `[sp+0x1c]`).

### Arguments

| # | what the original does | citation | class |
|---|---|---|---|
| Q2.1a | `r0 = src` (source object), kept in `r5` | `0x00A734AC: mov r5,r0` | EXACT_SOURCE |
| Q2.1b | `r1 = decoded PCM buffer pointer`; published to `params+0x00` | `0x00A734B4: str r1,[r4]` | EXACT_SOURCE |
| Q2.1c | `r2 = frame count`; `subs r6,r2,#0`; 0 → NoMoreData path | `0x00A73494: subs r6,r2,#0`; `0x00A7349C: beq #0xa7351c` | EXACT_SOURCE |
| Q2.1d | `r3 = arg4`; written to `params+0x24` (pitch/step) | `0x00A734A4: mov r7,r3`; `0x00A734F0: str r7,[r4,#0x24]` | EXACT_SOURCE |
| Q2.1e | `[sp+0x18] = arg5`; written to `params+0x04` (rate/format) | `0x00A734A8: ldr r3,[sp,#0x18]`; `0x00A734BC: str r3,[r4,#4]` | EXACT_SOURCE |
| Q2.1f | `[sp+0x1c] = arg6 = params` | `0x00A73498: ldr r4,[sp,#0x1c]` | EXACT_SOURCE |

Call sites: streamed `0xAB04E0: str r6,[sp]` (arg5 = `[src+0x84]`), `0xAB04D4: str r5,[sp,#4]`; in-memory `0xAB1964`; ADPCM `0xA73F64..0xA73F84`.

### Fields written to `params`

| field | value | source | citation |
|---|---|---|---|
| `+0x00` | arg2 | decoded buffer pointer | `0x00A734B4` |
| `+0x04` | arg5 | rate/format | `0x00A734BC` |
| `+0x0C` | `r6` u16 | frame count | `0x00A734B8` |
| `+0x0E` | `r6` u16 | frame count (0 on NoMoreData) | `0x00A734C0`; `0x00A73520` |
| `+0x18` | `[src+0x18]` before increment | start sample | `0x00A734C4`; `0x00A734F4` |
| `+0x20` | `[src+0x14]` | total/end sample | `0x00A734E8`; `0x00A734F8` |
| `+0x24` | arg4 | pitch/step | `0x00A734F0` |
| `+0x28` | `0x2D` or `0x2E` | result code | `0x00A73510`; `0x00A7351C` |
| `+0x10`/`+0x14` | written by `0x9D4C24` | marker count / array pointer | `0x009D4C50`; `0x009D4C54` |
| `+0x18` advance | `[src+0x18] = start + frames` | start advance | `0x00A734E4`; `0x00A734FC` |

`+0x08` (status) is **not** written by the emit; the render wrappers set it (`0x2B` etc.).

### `0x9D4C24(obj, pbi, params)`

Not a lock and does not copy the decoded buffer. Gate `[obj+4] != 0` (`0x009D4C24`, `0x009D4C2C`), then `(pbi+4 & 4) != 0` (`0x009D4C34..0x009D4C3C`). Initialises `params+0x14=0`, `params+0x10=0` (`0x009D4C50`, `0x009D4C54`); counts `[obj+4]` entries (count `[obj]`, 0x0C bytes) whose `[entry+4]` lies in `[r3, r3+maxFrames)` (`0x009D4C5C..0x009D4C8C`); allocates `20*count` via `0xA7A7F4` and stores the pointer at `params+0x14` (`0x009D4CA0..0x009D4CC8`); fills `{buffer, offset, entry[0..2]}` 0x14-byte records (`0x009D4CE0..0x009D4D24`). The writer of `[obj]`/`[obj+4]` (inited by `0x9D4ACC`) is not located, so the entry semantics are UNKNOWN; it does not change the sample values.

### Result codes

| code | condition | citation |
|---|---|---|
| `0x2E` NoMoreData | `frames == 0` at entry | `0x00A7349C`; `0x00A7351C`; `0x00A73524` |
| `0x2D` DataReady | mono (`[src+0x38]==1`) and `total > start+frames`; or stereo and `start+frames <= [src+0x28]`; also after the `vt+0x74` call in the overrun/at-total paths | `0x00A734EC`; `0x00A73500`; `0x00A73504..0x00A73514`; `0x00A7352C..0x00A73564` |
| `0x11` / `2` | **not set by the emit** (set by the framing `0xAB7E40` and the mode-2 wrapper `0xA78510`) | negative read of `0x00A73490..0x00A73564` |

The emit publishes the pointer and does not copy or resample.

## Q3 — decoder work-buffer geometry

| # | what the original does | citation | class |
|---|---|---|---|
| Q3.1 | `dsp+0x14[c]` is a **float\*** array: stride = `round_up((bs1/2)*4*channels,16)/channels` bytes = `(bs1/2)*4` bytes = `bs1/2` floats per channel; `dsp+0x14[c] = work_base + c*stride`, `work_base = [0x0108E650]` | `0x00AB3798`; `0x00AB37A4: asr r0,r0,#1`; `0x00AB37AC: lsl r0,r0,#2`; `0x00AB37B0: mul r0,r0,r6`; `0x00AB37B4: add r0,r0,#0xf`; `0x00AB37B8: bic r0,r0,#0xf`; `0x00AB37BC: bl #0x4b3e70` (`__aeabi_idiv`, GOT `0x104575C`); `0x00AB37D0: str r5,[r2,r3,lsl#2]` | EXACT_SOURCE |
| Q3.2 | the packet inverse zeroes `n*2` **bytes** (`n/2` floats) of `dsp+0x14[ch]` before floor1 inverse1 | `0x00AB6B8C: lsl r3,r3,#2`; `0x00AB6B90: lsr r3,r3,#1`; `0x00AB6B94: str r3,[fp,#-0x28]`; `0x00AB6C08`; `0x00AB6C0C: bl #0x4d36dc` | EXACT_SOURCE |
| Q3.3 | `mdct_backward` is called with `r0 = setup[blockflag]` (block size), `r1 = dsp+0x14[ch]`; inside, `0x00AB4E9C: asr r2,r7,#1` computes `n/2`, and the tail `0x00AB39D8` halves again (`0x00AB39DC: asr r1,r1,#1`). **The transform operates in place on `n/2` floats, not `n`.** | `0x00AB6EF8`; `0x00AB6EFC`; `0x00AB6F04`; `0x00AB4E9C`; `0x00AB4EB8`; `0x00AB4ECC`; `0x00AB5A28: lsl r1,r3,#1`; `0x00AB5A44: b #0xab39d8`; `0x00AB39DC` | EXACT_SOURCE |
| Q3.4 | overlap save reads `dsp+0x14[ch] + aligned(block_size)` and copies `aligned(block_size)` **bytes** | `0x00AB3674`; `0x00AB3690: bic r2,r2,#3`; `0x00AB3694`; `0x00AB3698` | EXACT_SOURCE |
| Q3.5 | first-window copy is the same length: `aligned(old_block_size)` bytes from `dsp+0x14[ch] + aligned(old_block_size)` | `0x00AB3820`; `0x00AB3834..0x00AB3840`; `0x00AB384C..0x00AB3860` | EXACT_SOURCE |
| Q3.6 | `dsp+0x18[ch]` holds `block_size/4` floats (both copies are `aligned(block_size)` bytes); the combine reads the overlap at `OV + bs/4` (`r7 = fp<<2 = bs1` bytes) | `0x00AB5AE0: lsl r7,fp,#2`; `0x00AB5AE8`; `0x00AB5B14: rsb r8,r2,r8`; `0x00AB5B18: sub r2,r6,r2,lsl#2` | EXACT_SOURCE |

**Reconciliation.** There is **no factor-of-two mismatch**: the buffer is `bs1/2` floats, the MDCT is passed the block size but internally uses `n/2`, and the overlap copies are `block_size/4` floats. P9's stride and P24's copy describe different buffers.

**UNKNOWN:** why this fork's IMDCT consumes/produces `n/2` elements where standard Vorbis consumes `n/2` spectral values and produces `n` time samples; and whether region B of the combine reads past the `block_size/4`-float overlap buffer for shipped block sizes. The instructions are clear; the semantic reason is not settled.

## Q4 — four derived choices checked

| # | choice | verdict | citation |
|---|---|---|---|
| 4.1 | packed codebook: codeword length = `read(lengthBits)+1` (sparse and non-sparse); ordered initial length = `read(5)+1` | **confirmed, with a correction:** `lengthBits` is `read(3)` (`0x00ABA1FC`, `0x00ABA204`), not `read(5)`. Non-sparse `0x00ABA40C..0x00ABA430`, store `read+1` `0x00ABA420`; sparse `0x00ABA258..0x00ABA284`, store `read+1` `0x00ABA274`; ordered `0x00ABA288`, `0x00ABA29C: add r8,r0,#1` | EXACT_SOURCE |
| 4.2 | `0x00ABA840` returns the raw leaf entry, no dequantisation; only `0x00AB9BB0` dequantises | **confirmed.** `0x00ABA8D8: cmp r0,#0`; `0x00ABA8DC: bge`; `0x00ABA8E0: bic r0,r0,#0x80000000`; 16-bit `0x00ABA978: ubfx r0,r0,#0,#0xf`. `0x00AB9BB0` loads `q_bits` `0x00AB9C50`, unpacks integers `0x00AB9C94..0x00AB9C9C`, dequantises `0x00AB9CA8..0x00AB9D14` | EXACT_SOURCE |
| 4.3 | residue `stages` byte (gapG 6.7) is computed as the highest set cascade bit + 1, not read | **confirmed; P8's "+0x1a stages read(8)" is contradicted.** `0x00AB6F34` never reads into `[r4+0x1a]`; the class loop sets it from the cascade bits: `0x00AB7180`, `0x00AB718C`, `0x00AB71C4`, `0x00AB720C`, `0x00AB7254`, `0x00AB729C`, `0x00AB72E4`, `0x00AB732C`, `0x00AB7374`; cascade byte `read(3) | (read(5)<<3)` at `0x00AB700C..0x00AB703C` | EXACT_SOURCE |
| 4.4 | residue type-2 uses `perChannel = grouping / channels` and offset `subpart*perChannel + begin/channels` | **confirmed.** `0x00AB7518`; `0x00AB751C`; `0x00AB7520` (`__aeabi_idiv`); `0x00AB752C`; `0x00AB7648: mul r5,r4,r7`; `0x00AB769C`; `0x00AB76B4`; `0x00AB76CC: add r2,r5,r0` | EXACT_SOURCE |

## Existing records contradicted / too weak

1. **P8 / gapG 6.7 residue setup:** `+0x1a stages read(8)` is contradicted (Q4.3). It is computed.
2. **source-classes Q4 `params` table:** `+0x04` is the **5th** argument, not the 3rd (`0x00A734A8`, `0x00A734BC`).
3. **source-classes Q4 / task result codes:** `0xA73490` sets only `0x2D`/`0x2E`; `0x11` and `2` are set by the framing and the mode-2 wrapper.
4. **P24:** the memcpy length is bytes (`block_size/4` floats) and `mdct_backward` halves `n` internally; P24 + P9/P22 should carry the Q3 citations.
5. **C5 8b / C6 5:** the forms are cited but not the region selection; the region gates are now read (Q1.4), but the semantic mapping stays UNKNOWN.
6. **P27 body range** and **P2's gate** are corrected by the stream-reset report (`20260928-B-M6b-1-stream-reset.md`).