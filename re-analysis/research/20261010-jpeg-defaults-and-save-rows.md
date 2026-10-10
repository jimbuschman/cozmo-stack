# JPEG default Huffman tables (M3-001) and EncodedImage::Save encoder (M3-018): build rows

Extractor: read-only pass, 2026-10-10. Binaries: `resources/lib/armeabi-v7a/libopencv_imgcodecs.so` (IMG) and `libcozmoEngine.so` (ENG), ARM Thumb. Addresses are virtual addresses. IMG .text VA = file offset; IMG .data VA 0x175000 = file offset 0x174000; IMG .rodata VA = file offset.

## Coverage

| Item | Status | Note |
|---|---|---|
| 1a. Default-table condition 0x15BA4..0x15BE8 | CHECKED | Instructions read; slots and call arguments decoded. |
| 1b. Loader 0xE2B8 (walk, count<=256, selector<=3, alloc 0x1ED88, failure) | CHECKED | Whole function read; pc-relative literals resolved (static table VA 0x175018). |
| 1c. Static table byte dump | CHECKED | 420 bytes at VA 0x175018 dumped and parsed. The encoder's four standard tables were compared and are byte-identical. |
| 2a. ENG EncodedImage::Save (0x4F2EEC..0x4F3056) | CHECKED | Whole function read; every callee resolved to a symbol; every log string read. Callers: none found (U1). |
| 2b. ENG ImageBase<PixelRGB>::Save (0x86FCE8..0x86FFA8) | CHECKED | Whole function read; callees resolved; ImageRGB channel count read (0x6A8958). |
| 2c. ENG FileUtils::CreateDirectory / WriteFile | PARTIAL | CreateDirectory control flow read; WriteFile read up to its open/write loop; libc++ ofstream internals not transcribed. |
| 2d. IMG cv::imwrite and imwrite_ (0x10410, 0x10244) | CHECKED | |
| 2e. IMG JpegEncoder::write (0x15E26..0x1616E): params loop, defaults, call order | CHECKED | |
| 2f. libjpeg setup: jpeg_set_defaults 0x1F1BC, set_colorspace 0x1EF94, set_quality 0x1EF7A, quality_scaling 0x1EF54, default_qtables 0x1EEF4, add_quant_table 0x1EE5E, add_huff_table 0x1ED9E | CHECKED | Whole functions read. |
| 2g. Derived quant tables at quality 90 | CHECKED | Computed from the std tables read from the .so (VA 0xCE440 / 0xCE540) with the arithmetic read in 0x1EE5E. |
| 2h. Marker order (SOI, JFIF, DQT, SOF0, DHT, SOS) | PARTIAL | Control flow and constants of the file/frame/scan header writers read; emit_dqt (0x1D9B4), emit_dht (0x1DA60) and the SOS tail not read. |
| 2i. Pixel pipeline bodies (rgb_ycc_convert, prep controller, h2v2_downsample, forward_DCT, jpeg_fdct_islow, coef controller, jchuff) | PARTIAL | Selection logic CHECKED and addresses given; fdct_islow constants confirmed; arithmetic bodies not transcribed (G1). |
| Emulator run of imwrite to confirm output bytes | NOT DONE | Out of scope for this read-only pass. |

Library identification: the shipped encoder is the IJG libjpeg 9 series API, not libjpeg-turbo. Evidence: `jpeg_CreateCompress` is called with version 0x5A = 90 and struct size 0x1B8 (IMG 0x15E9A `movs r1,#0x5a`, 0x15EA4 `mov.w r2,#0x1b8`, `bl 0x1D368`); the compress struct has `q_scale_factor[4]` at +0x68 (0x1EEF4 `ldr r3,[r0,#0x68]`, `[r5,#0x6c]`) and a `jpeg_default_qtables` routine; forward-DCT start_pass dispatches on (DCT_h_scaled_size<<8 | DCT_v_scaled_size) (0x27288..0x2766A). No SIMD instructions occur in jpeg_fdct_islow (0x2C7D8..0x2CA24), h2v2_downsample or the Huffman encoder range (mnemonic scan).

---

## Part 1. Default Huffman tables in the decoder

| step | what the original does | citation | existing record | classification |
|---|---|---|---|---|
| 1.1 Entry gate | Function 0x15B60 (OpenCV JpegDecoder::readData) fails (jumps to 0x15CDE) if `[r0+0x64]`, `[r0+4]` or `[r0+8]` is zero. Then `setjmp` (PLT 0xDC24 -> GOT 0x174E00 `setjmp`) on `cinfo+0x26C`; non-zero return branches to 0x15CDE. | IMG 0x15B7C `cmp r3,#0; beq.w 0x15cde`; 0x15B94 `add.w r0,r3,#0x26c`; 0x15B9A `blx #0xdc24`; 0x15B9E `bne.w 0x15cde` | M3-001 (J rows) | EXACT_SOURCE |
| 1.2 Default-table condition | Four loads from the decompress struct, in this order: `[cinfo+0xC4]` (ac_huff_tbl_ptrs[0]), `[cinfo+0xC8]` (ac[1]), `[cinfo+0xB4]` (dc_huff_tbl_ptrs[0]), `[cinfo+0xB8]` (dc[1]). Any non-null jumps to 0x15BAC and skips the load. Only when all four are null does it fall through to the call. Indexes 2 and 3 of either array are not tested. | IMG 0x15BA6 `ldr.w r3,[r3,#0xc4]; cbz r3,0x15bbc`; 0x15BBE `ldr.w r3,[r3,#0xc8]; cmp r3,#0; bne 0x15bac`; 0x15BC8 `ldr.w r3,[r3,#0xb4]; cmp; bne`; 0x15BD2 `ldr.w r3,[r3,#0xb8]; cmp; bne` | NEW | EXACT_SOURCE |
| 1.3 Loader call | `r0 = cinfo`, `r1 = cinfo+0xC4` (address of the AC pointer array), `r2 = cinfo+0xB4` (address of the DC pointer array), `bl 0xE2B8`. The return value is not read: execution continues unconditionally at 0x15BAC. | IMG 0x15BDA `ldr r0,[sp,#8]`; 0x15BDC `add.w r1,r0,#0xc4`; 0x15BE0 `add.w r2,r0,#0xb4`; 0x15BE4 `bl #0xe2b8`; 0x15BE8 `b #0x15bac` | NEW | EXACT_SOURCE |
| 1.4 Loader setup | Pushes 9 regs, 0x124 bytes of stack, stack-protector cookie. `r6 = cinfo`, `[sp] = AC array pointer`, `r7 = DC array pointer`, `r8 = 4` (offset of the first table class/index byte), `r5 = static table base`. The base resolves to VA 0x175018: literal at 0xE3C4 = 0x166D4C, plus pc 0xE2CC = 0x175018; the second literal at 0xE3CC = 0x166D3A plus pc 0xE2DE = 0x175018. | IMG 0xE2B8..0xE2D6; 0xE2D8 `ldr r3,[pc,#0xf0]; add r3,pc` | NEW | EXACT_SOURCE |
| 1.5 Total length | `r4 = ((table[2] << 8) + table[3]) - 2` (big-endian segment length minus its own 2 bytes). The static bytes are `ff c4 01 a2`, so r4 = 0x1A2 - 2 = 416. | IMG 0xE2DC `ldrb r4,[r3,#2]; ldrb r3,[r3,#3]; add.w r4,r3,r4,lsl #8; subs r4,#2` | NEW | EXACT_SOURCE |
| 1.6 Loop test | At the top of each iteration `cmp r4,#0x10; bls` exits to the epilogue: the walk stops when 16 or fewer bytes remain; it does not require exactly zero. | IMG 0xE2E6..0xE2E8, exit target 0xE3A8 | NEW | EXACT_SOURCE |
| 1.7 Per-table bits | One byte `tc_th = table[r8]` (kept in r3, copied to r0). A 17-byte `bits` array is built on the stack at sp+8: `bits[0] = 0`, `bits[1..16] = table[r8+1 .. r8+16]`; the sum `count` is kept in r1. | IMG 0xE2EE `strb.w r3,[sb]` (r3=0); 0xE2F0..0xE312 (`ldrb.w ip,[lr,r2]; add r1,ip; strb.w ip,[r2,sl]; adds r2,#1; cmp r2,#0x11`) | NEW | EXACT_SOURCE |
| 1.8 count <= 256 check | `cmp.w r1,#0x100; bls 0xE328`; otherwise `r0 = -1` and return (0xE322 `mov.w r0,#-1; b 0xE3B0`). Unsigned `bls`, so count == 256 passes. | IMG 0xE314, 0xE320, 0xE322 | NEW | EXACT_SOURCE |
| 1.9 Remaining-length check | `r4 -= 0x11` at 0xE31C, then `cmp r1,r4; bhi 0xE322`: count must not exceed (remaining - 17). Failure returns -1; tables loaded earlier in the walk stay loaded. | IMG 0xE31C `sub.w r4,r4,#0x11`; 0xE328 `cmp r1,r4`; 0xE32A `bhi #0xe322` | NEW | EXACT_SOURCE |
| 1.10 huffval read | `count` bytes are copied from `table[r8+17+i]` to a stack buffer at sp+0x1C; `r4 -= count` (0xE346 `subs r4,r4,r2`); `r8 += count + 17` (0xE356 `add r8,ip`, `ip = count+0x11`). | IMG 0xE32C..0xE346, 0xE356 | NEW | EXACT_SOURCE |
| 1.11 Slot select and selector<=3 | Bit 4 of `tc_th` is tested by `lsls r2,r3,#0x1b` (N flag). Set (AC): `r0 = tc_th - 0x10`, slot address = `[sp] + r0*4` (AC array). Clear (DC): `r0` stays `tc_th`, slot address = `r7 + tc_th*4` (DC array). Then `cmp r0,#3; bhi 0xE322` (unsigned): selector above 3 returns -1. No other bits of `tc_th` are masked; a DC byte with bits 5..7 set fails through this same test. | IMG 0xE348 `lsls r2,r3,#0x1b`; 0xE34A `itet mi`; 0xE34C `submi.w r0,r3,#0x10`; 0xE350 `addpl.w sl,r7,r3,lsl #2`; 0xE354 `ldrmi r3,[sp]`; 0xE35A `addmi.w sl,r3,r0,lsl #2`; 0xE35E `cmp r0,#3`; 0xE360 `bhi #0xe322` | NEW | EXACT_SOURCE |
| 1.12 Allocation | If `*slot` is null: `r0 = cinfo; bl 0x1ED88`; store the result at `*slot`. If `*slot` is still null the loader returns -1. | IMG 0xE362 `ldr.w r3,[sl]; cbnz r3,0xe372`; 0xE368 `mov r0,r6; bl #0x1ed88; str.w r0,[sl]`; 0xE372..0xE378 `beq #0xe322` | NEW | EXACT_SOURCE |
| 1.13 0x1ED88 body (jpeg_alloc_huff_table) | `r0 = cinfo->mem->alloc_small(cinfo, 0 /*JPOOL_PERMANENT*/, 0x118)` (function pointer `[[cinfo+4]+0]`), then `[r0+0x114] = 0` (sent_table = FALSE), returns r0. A libjpeg alloc_small failure is an error_exit/longjmp, not a null return, so the null test in 1.12 is not reached on out-of-memory. | IMG 0x1ED88..0x1ED9C: `ldr r3,[r0,#4]; ldr r3,[r3]; movs r1,#0; mov.w r2,#0x118; blx r3; movs r2,#0; str.w r2,[r0,#0x114]` | NEW | EXACT_SOURCE |
| 1.14 Table fill | `bits[0..16]` (17 bytes: two `ldm`/`str` iterations of 8 bytes, then one byte) are copied to the table first 17 bytes, then `memcpy(table+0x11, sp+0x1C, 0x100)` (PLT 0xDA44 -> GOT 0x174D60 `memcpy`). The copy is always 256 bytes, so bytes past `count` come from the uninitialised stack buffer. Then back to the loop test 0xE2E6. | IMG 0xE37A..0xE3A6 (`mov.w r2,#0x100; ldr.w r0,[sl]; adds r0,#0x11; blx #0xda44; b #0xe2e6`) | NEW | EXACT_SOURCE (copy length); see U2 |
| 1.15 Return | At loop exit `r0 = (r4 != 0) ? -1 : 0` (`adds r0,r4,#0; it ne; movne r0,#1; rsbs r0,r0,#0`), stack-protector check, pop. For the shipped table r4 reaches 0. | IMG 0xE3A8..0xE3C0 | NEW | EXACT_SOURCE |
| 1.16 What the shipped table produces | Four tables created and filled in this order: tc_th 0x00 (DC slot 0), 0x01 (DC slot 1), 0x10 (AC slot 0), 0x11 (AC slot 1); each left with `sent_table = 0`. Nothing can fail with these bytes: 17+12 + 17+12 + 17+162 + 17+162 = 416. | computed from the dump below | NEW | EXACT_SOURCE |
| 1.17 After the call | 0x15BAC: `r3 = [sp+0x1C]` (the output Mat flags word), `ubfx r5,r3,#3,#9` = channels-1, `r5 = (that != 0)`. Non-zero (colour) goes to 0x15BEA: if `[cinfo+0x24]` (num_components) == 4 it jumps to 0x15C06 with r3 = 4 (stored to `[cinfo+0x2C]` and `[cinfo+0x78]`), else `[cinfo+0x2C] = 2` (out_color_space RGB) and r3 = 3 (stored to `[cinfo+0x78]` at 0x15C0A). Gray goes to 0x15BFC: r3 = 1 unless num_components == 4 (then r3 stays 4), stored to the same two fields. This is the J14 colour-space row; named here only as the interface. | IMG 0x15BAC..0x15C0A | M3-001 J14 | EXACT_SOURCE (interface only) |

### Static table (IMG VA 0x175018, file offset 0x174018, section .data, 420 = 0x1A4 bytes)

OpenCV my_jpeg_odml_dht: the Annex K standard tables as a DHT segment, marker included. `ff c4` at 0x175018, `01 a2` at 0x17501A. Per table: class/index byte, then bits[1..16], then huffval[count]. The loader bits[0] is the zero it writes itself; it is not in the static data.

| Table | tc_th byte VA | tc_th | bits[1..16] | count | huffval VA |
|---|---|---|---|---|---|
| DC luminance (slot DC0) | 0x17501C | 00 | `00 01 05 01 01 01 01 01 01 00 00 00 00 00 00 00` | 12 | 0x17502D |
| DC chrominance (slot DC1) | 0x175039 | 01 | `00 03 01 01 01 01 01 01 01 01 01 00 00 00 00 00` | 12 | 0x17504A |
| AC luminance (slot AC0) | 0x175056 | 10 | `00 02 01 03 03 02 04 03 05 05 04 04 00 00 01 7d` | 162 | 0x175067 |
| AC chrominance (slot AC1) | 0x175109 | 11 | `00 02 01 02 04 04 03 04 07 05 04 04 00 01 02 77` | 162 | 0x17511A |

huffval bytes:

```
DC0 (12):  00 01 02 03 04 05 06 07 08 09 0a 0b
DC1 (12):  00 01 02 03 04 05 06 07 08 09 0a 0b
AC0 (162): 01 02 03 00 04 11 05 12 21 31 41 06 13 51 61 07 22 71 14 32 81 91 a1 08 23 42 b1 c1 15 52 d1 f0
           24 33 62 72 82 09 0a 16 17 18 19 1a 25 26 27 28 29 2a 34 35 36 37 38 39 3a 43 44 45 46 47 48 49
           4a 53 54 55 56 57 58 59 5a 63 64 65 66 67 68 69 6a 73 74 75 76 77 78 79 7a 83 84 85 86 87 88 89
           8a 92 93 94 95 96 97 98 99 9a a2 a3 a4 a5 a6 a7 a8 a9 aa b2 b3 b4 b5 b6 b7 b8 b9 ba c2 c3 c4 c5
           c6 c7 c8 c9 ca d2 d3 d4 d5 d6 d7 d8 d9 da e1 e2 e3 e4 e5 e6 e7 e8 e9 ea f1 f2 f3 f4 f5 f6 f7 f8
           f9 fa
AC1 (162): 00 01 02 03 11 04 05 21 31 06 12 41 51 07 61 71 13 22 32 81 08 14 42 91 a1 b1 c1 09 23 33 52 f0
           15 62 72 d1 0a 16 24 34 e1 25 f1 17 18 19 1a 26 27 28 29 2a 35 36 37 38 39 3a 43 44 45 46 47 48
           49 4a 53 54 55 56 57 58 59 5a 63 64 65 66 67 68 69 6a 73 74 75 76 77 78 79 7a 82 83 84 85 86 87
           88 89 8a 92 93 94 95 96 97 98 99 9a a2 a3 a4 a5 a6 a7 a8 a9 aa b2 b3 b4 b5 b6 b7 b8 b9 ba c2 c3
           c4 c5 c6 c7 c8 c9 ca d2 d3 d4 d5 d6 d7 d8 d9 da e2 e3 e4 e5 e6 e7 e8 e9 ea f2 f3 f4 f5 f6 f7 f8
           f9 fa
```

Contiguous hex of the whole segment (VA 0x175018..0x1751BB), written per table as `tc_th | bits | huffval`:

```
ffc401a2
00 00010501010101010100000000000000 000102030405060708090a0b
01 00030101010101010101010000000000 000102030405060708090a0b
10 0002010303020403050504040000017d 01020300041105122131410613516107227114328191a1082342b1c11552d1f02433627282090a161718191a25262728292a3435363738393a434445464748494a535455565758595a636465666768696a737475767778797a838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8f9fa
11 00020102040403040705040400010277 000102031104052131061241510761711322328108144291a1b1c109233352f0156272d10a162434e125f11718191a262728292a35363738393a434445464748494a535455565758595a636465666768696a737475767778797a82838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae2e3e4e5e6e7e8e9eaf2f3f4f5f6f7f8f9fa
```

The spaces are for reading; the file bytes are contiguous. Four zero bytes follow at VA 0x1751BC, outside the segment.

### Part 1 unresolved

- **U2.** Whether the uninitialised tail of the 256-byte copy (row 1.14) is ever read is not established here. Standard jpeg_make_d_derived_tbl reads only `count` entries, but the decoder Huffman table derivation was not read in this pass. RECOVERABLE_GAP: read it in IMG (reached from jpeg_start_decompress / start_pass_huff_decoder).
- The B-M3M4 statement "count <= 256, selector <= 3" is confirmed. Additional facts: rows 1.6 (exit at 16 or fewer bytes left), 1.9 (remaining-length test) and 1.15 (return value).

---

## Part 2. EncodedImage::Save and the JPEG encoder

### 2A. Engine EncodedImage::Save

Symbol `_ZNK4Anki5Cozmo12EncodedImage4SaveERKNSt6__ndk112basic_string...` value 0x4F2EED (Thumb); body 0x4F2EEC..0x4F3056. Arguments `r0 = this`, `r1 = const std::string& path`. It is in `.dynsym`. Fields used: encoding byte `this+0x20` (ldrb); `ldrh this+0x14` and `ldrh this+0x18` (inputs to the mini-JPEG helper); `this` itself is passed as a `const vector<uint8_t>&` (the data vector begins at offset 0; the helper signature `MiniToJpegHelper(const vector<uchar>&, ushort, ushort, vector<uchar>&, const uchar*, uint)` has the data vector as its first argument and receives `this`).

The existing evidence range 0x004F2EFC..0x004F2FA8 starts at the encoding load and ends at the call to ImageBase::Save; the function runs 0x4F2EEC..0x4F3056.

| step | what the original does | citation | existing record | classification |
|---|---|---|---|---|
| 2A.1 Prologue | `push {r4-r6,lr}; sub sp,#0x60`; an empty `std::vector<uint8_t>` (3 zero words) at sp+0x54..0x5C is the rebuilt-JPEG buffer. `r5 = this`, `r6 = path`. | ENG 0x4F2EEC..0x4F2EFA | NEW | EXACT_SOURCE |
| 2A.2 Dispatch on encoding | `ldrb r0,[this+0x20]`; `cmp r0,#9; beq 0x4F2F78` (encoding 9: decode and re-encode); `cmp r0,#8; bne 0x4F2F24` (every other value: raw write); encoding 8 first builds a JPEG. | ENG 0x4F2EFC `ldrb.w r0,[r5,#0x20]`; 0x4F2F00 `cmp r0,#9`; 0x4F2F04 `cmp r0,#8` | A25 | EXACT_SOURCE |
| 2A.3 Encoding 8 (gray mini-JPEG) | `EncodedImage::MiniToJpegHelper(data = this, height = ldrh [this+0x18], width = ldrh [this+0x14], out = vector at sp+0x54, header = VA 0xC48C40, headerLen = 0x144)`; then `r5 = &sp+0x54` so the raw write below writes the rebuilt JPEG. The header pointer comes from the literal at 0x4F2F08 plus pc; the bytes at VA 0xC48C40 start `ff d8 ff e0`. The helper return value is ignored. | ENG 0x4F2F08 `ldr r0,[pc,#0x268]; add r0,pc`; 0x4F2F12 `mov.w r3,#0x144`; 0x4F2F16 `strd r0,r3,[sp]`; 0x4F2F1E `blx #0x4A5998` (PLT -> MiniToJpegHelper, local symbol 0x4F31C5) | A25 (says written raw: confirmed) | EXACT_SOURCE |
| 2A.4 Raw write (every encoding except 9; for 8 the rebuilt vector) | `Util::FileUtils::WriteFile(path, vector, append = false)` (`r0 = path`, `r1 = vector`, `r2 = 0`; PLT 0x4A5A34; symbol 0x803801). Non-zero return means success: the function returns 0 (`r5 = 0`). Zero return: `sWarningF("EncodedImage.Save.WriteFail" (VA 0x4F3178), emptyKv, "Filename: %s" (VA 0x4F3194), path.c_str())`, free the log vector, return 1. | ENG 0x4F2F24..0x4F2F32 (`mov r0,r6; mov r1,r5; movs r2,#0; blx #0x4A5A34; movs r5,#0; cmp r0,#0; bne.w 0x4F3040`); 0x4F2F36..0x4F2F76 (`adr r0,#0x22c`, `adr r2,#0x244`, `blx #0x4A4540` = sWarningF, `movs r5,#1`) | NEW | EXACT_SOURCE |
| 2A.5 Encoding 9 decode | `ImageRGB::ImageRGB()` default-constructed at sp+0x14 (PLT 0x4A5A40), then `this->DecodeImageHelper<ImageRGB>(img)` (`r0 = this`, `r1 = &img`; PLT 0x4A595C). `r5` = its Result (0 = OK). This is the A9/A10 decode path (imdecode, cvtColor, resize to 320x240); only the call is cited here. | ENG 0x4F2F78 `add r0,sp,#0x14; blx #0x4A5A40`; 0x4F2F7E..0x4F2F8A | A9, A10 (callee) | EXACT_SOURCE (call); callee under A9/A10 |
| 2A.6 Encoding 9 decode failure | Non-zero Result: `sWarningF("EncodedImage.Save.DecodeColorFailed" (VA 0x4F3124), emptyKv, "" (format at VA 0xBE3F00 is the empty string))`. Nothing is written; the Result in r5 is returned. | ENG 0x4F2F8A `cbz r5,0x4F2FA2`; 0x4F2F8C..0x4F2FA0 (`adr r0,#0x188` = 0x4F3124, `blx #0x4A4540`) | NEW | EXACT_SOURCE |
| 2A.7 Encoding 9 save | `ImageBase<PixelRGB>::Save(&img, path, 0x5A = 90)` (`r0 = &img`, `r1 = path`, `r2 = 0x5A`; PLT 0x4A5A4C, symbol 0x86FCE9). Result in r5. Non-zero: `sWarningF("EncodedImage.Save.MiniJPEGSaveFailed" (VA 0x4F314C), emptyKv, "")`, Result returned. Zero: `r5 = 0`. | ENG 0x4F2FA2..0x4F2FBE (`movs r2,#0x5a` at 0x4F2FA6), 0x4F2FE8 `movs r5,#0` | A25 | EXACT_SOURCE |
| 2A.8 Epilogue | Releases the local ImageRGB (cv::Mat refcount at `[sp+0x3C]+0xC` by `ldrex/strex`, resets header and size array), frees the log temporaries, returns `r5`: 0 = OK, 1 = write or save failed, or the decode Result. | ENG 0x4F2FEA..0x4F3056 | NEW | EXACT_SOURCE |
| 2A.9 Callers | No `bl`, `blx` or `b.w` to 0x4F2EEC or 0x4F2EED exists in libcozmoEngine.so .text and .plt (instruction-pattern scan), no data pointer to 0x4F2EED in .data, .data.rel.ro, .rodata, .init_array or .got, and no PLT entry. The symbol is exported. No code in this library chooses the path, so the output extension is not known here. | scan over ENG .text; `.dynsym` entry 0x4F2EED | NEW | UNKNOWN (U1) |

### 2B. ImageBase<PixelRGB>::Save (ENG 0x86FCE8, symbol 0x86FCE9)

Arguments `r0 = image`, `r1 = path`, `r2 = quality`. Returns a Result: 0 success, 1 failure.

| step | what the original does | citation | existing record | classification |
|---|---|---|---|---|
| 2B.1 Params vector | A `std::vector<int>` at sp+0x60 receives `push_back(1)` and then `push_back(quality)`. The parameter list handed to imwrite is exactly `{ 1, 90 }`: 1 is `cv::IMWRITE_JPEG_QUALITY`, 90 the value from EncodedImage::Save. No other flag (no progressive, optimize, restart interval, luma/chroma quality). | ENG 0x86FCFC `movs r0,#1; str r0,[sp,#0x28]`; 0x86FD08 `blx #0x4BAFB0`; second push at 0x86FD14..0x86FD24 (`ldr r1,[sp,#0x6c]; str r1,[r0]`, slow path `blx #0x4A6EBC` = `vector<int>::__push_back_slow_path`) | NEW | EXACT_SOURCE |
| 2B.2 Channel-count dispatch | `r0 = image->vtable[0]()` (virtual GetNumChannels). For ImageRGB the vtable (symbol 0x1031848, slot 0 at 0x1031850) holds 0x6A8959, whose code is `movs r0,#3; bx lr`, so the value is 3. Cases: 1 -> copy the Mat header unconverted; 3 -> cvtColor code 4; 4 -> cvtColor code 5; anything else -> log and return 1 (2B.7). | ENG 0x86FD4E..0x86FD6E; 0x6A8958 `movs r0,#3` | NEW | EXACT_SOURCE |
| 2B.3 3-channel conversion | `cv::cvtColor(src = InputArray{flags 0x81010010 = fixed type CV_8UC3 (0x10), kind Mat} over the image Mat at this+4, dst = OutputArray over a local Mat at sp+0x28, code 4, dcn 0)`. Code 4 is the R/B channel swap. | ENG 0x86FD92..0x86FDB0 (`movs r1,#0x10; movt r1,#0x8101`; `adds r2,r5,#4`; `movs r2,#4`; `movs r3,#0`; `blx #0x4A598C` = cv::cvtColor(const _InputArray&, const _OutputArray&, int, int)) | NEW | EXACT_SOURCE |
| 2B.4 Create directory | `Util::FileUtils::CreateDirectory(path, true, true)` (`r0 = path`, `r1 = 1`, `r2 = 1`; PLT 0x4A6508; body 0x802874..0x8029C8). The return value is not tested. With the first flag set the path is cut after its last slash (file name removed) and the rest is created recursively: prefixes are found with `string::find` of the slash character, each is tested with `FileUtils::DirectoryExists` and, if absent, created with `mkdir(prefix, 0x1C0)` (0x1C0 = octal 0700); a failed mkdir ends the walk with false; the walk is bounded at 200 iterations (`cmp r4,#0xc8`). | ENG 0x86FE8A..0x86FE90; body: `cmp r1,#0x2f` scan at 0x8028A0..0x8028A8; `mov.w r1,#0x1c0; blx #0x4CB378` (PLT -> mkdir) at 0x80293C..0x802946; `cmp r4,#0xc7; bhi` at 0x802972 | NEW | EXACT_SOURCE (call, mode, bound); PARTIAL on the post-loop result flags, which do not affect the output bytes |
| 2B.5 imwrite | The path is copied into a `cv::String` (`cv::String::allocate`, `memcpy`), then `cv::imwrite(filename, InputArray{flags 0x01010000: read access, kind Mat} over the swapped Mat, params = vector at sp+0x60)` (PLT 0x4CFA04 -> `_ZN2cv7imwrite...`, defined in libopencv_imgcodecs.so at 0x10411). The cv::String is then deallocated. | ENG 0x86FE9A..0x86FED6 (`add r0,sb,#-0x1000000`; `blx #0x4CFA04`); `blx #0x4BA194` (cv::String::allocate), `blx #0x4BA1AC` (cv::String::deallocate) | NEW | EXACT_SOURCE |
| 2B.6 Success | imwrite true (`r7 != 0`): returns `r6 = 0`. | ENG 0x86FEE2 `movs r6,#0`; 0x86FEE4 `cbnz r7,0x86FF32` | NEW | EXACT_SOURCE |
| 2B.7 Failure logs | imwrite false: `sWarningF("ImageBase.Save.ImwriteFailed" (VA 0xC2D2E1), emptyKv, "Failed writing %dx%d image to %s" (VA 0xC2D2FE), [this+0x10], [this+0xC], path.c_str())`, return 1. Channel count outside {1,3,4}: `sWarningF("ImageBase.Save.UnexpectedNumChannels" (VA 0xC2D294), emptyKv, "Don't know how to save %d-channel image" (VA 0xC2D2B9), channels)`, return 1. | ENG 0x86FE28..0x86FE48 (channels); 0x86FEE6..0x86FF0E and 0x86FF30 | NEW | EXACT_SOURCE |
| 2B.8 Net pixel bytes handed to libjpeg | The engine swaps RGB to BGR (code 4) and the JPEG encoder swaps BGR back to RGB (2C.8), so libjpeg receives the original ImageRGB bytes in R,G,B order, declared JCS_RGB with 3 components. | 2B.3 and 2C.8 | NEW | EXACT_SOURCE |

### 2C. libopencv_imgcodecs.so: imwrite and JpegEncoder::write

| step | what the original does | citation | existing record | classification |
|---|---|---|---|---|
| 2C.1 cv::imwrite (0x10410..0x1045C, symbol 0x10411) | Builds an empty `cv::Mat` temporary (0xF3D6), calls `imwrite_(filename, image, params, flipv = 0)` (0x10244) and destroys the temporary. | IMG 0x10410..0x10436 (`movs r3,#0; bl #0x10244`) | NEW | EXACT_SOURCE |
| 2C.2 imwrite_ checks (0x10244) | (a) `channels-1` (`ubfx r3,[image],#3,#9`) must be 0, 2 or 3 (1, 3 or 4 channels), else `cv::error(-215)` at source line 455 (0x1C7). (b) `findEncoder(filename)` (0xED74); an empty result raises `cv::error(-2)` at line 459 (0x1CB). (c) `encoder->isFormatSupported(depth = flags and 7)` (vtable +8). If it is false the code checks the support again (error at line 462, 0x1CE, if that also fails) and converts the image (call at 0x1034A, `blx #0xDB34`). For a CV_8U engine image the first test passes and no conversion occurs. (d) `flipv = 0`: no flip. | IMG 0x10264..0x102A0; 0x102AE..0x102DE; 0x102E4..0x10350; 0x10354 `cmp.w sb,#0; beq 0x10378` | NEW | EXACT_SOURCE on the passing path; assertion message texts not read (U3) |
| 2C.3 Encoder dispatch | `encoder->setDestination(filename)` (vtable +0xC), then `encoder->write(image, params)` (vtable +0x14); the returned bool is what imwrite returns. The encoder is chosen by `findEncoder` (0xED74) from the filename extension (the text after the last dot via `strrchr`, PLT 0xD990), compared case-insensitively (ASCII class table) with each registered encoder description in the static codec list. JpegEncoder::write is the routine at 0x15E26. The extension of the engine path is not known (U1). | IMG 0xED74..0xEED0; 0x1037A..0x1038E | NEW | EXACT_SOURCE for the dispatch; the extension match table for other formats not read |
| 2C.4 write(): setup and destination (0x15E26..0x15EE6) | `jpeg_CreateCompress(cinfo = sp+0x1F0, version 90, size 0x1B8)` (0x1D368), then the std error manager (0x23A3C = jpeg_std_error) with error_exit replaced (pointer at `[sp+0x6C]`). Destination: when the encoder memory-buffer member `[this+0x14]` is null (the Save case: a filename was set) it calls `fopen(filename, "wb")` (PLT 0xDA08; mode string at VA 0xC21DC; a null filename is replaced by the empty string at VA 0xC2150) and `jpeg_stdio_dest(cinfo, FILE*)` (0x206C8: allocates a 0x1C-byte destination, outfile at +0x14); fopen returning null branches to the failure exit 0x160F8. When the member is set (imencode) a custom memory destination is installed instead. | IMG 0x15E9A..0x15EE6 (`movs r1,#0x5a`, `bl #0x1d368`, `blx #0xda08`, `bl #0x206c8`) | NEW | EXACT_SOURCE |
| 2C.5 write(): setjmp and params | After `setjmp` (0x15F10) the params are read as (key, value) pairs. Defaults: `quality = 95` (`movs r5,#0x5f`), luma = chroma = -1, `restart_interval = 0`, progressive = 0, optimize = 0. Key 1: `quality = clamp(v, 0..100)`; key 2: progressive = v; key 3: optimize = v; key 4: `restart_interval = clamp(v, 0..0xFFFF)` (`movw lr,#0xffff`); key 5: luma quality = clamp(v, 0..100) if v >= 0; key 6: chroma quality likewise. For `{1, 90}`: quality = 90, everything else stays at its default. | IMG 0x15F1A..0x15FD4 | NEW | EXACT_SOURCE |
| 2C.6 write(): image fields | `image_width = Mat cols ([image+0xC])`, `image_height = Mat rows ([image+8])`, `input_components = (cn == 1) ? 1 : 3`, `in_color_space = (cn == 1) ? 1 (JCS_GRAYSCALE) : 2 (JCS_RGB)`. A 4-channel Mat is also declared 3 components / RGB and its rows are converted BGRA to BGR. | IMG 0x15F26..0x15F4C (stores at sp+0x20C, 0x210, 0x214, 0x218) | NEW | EXACT_SOURCE |
| 2C.7 write(): libjpeg init sequence | In order: `jpeg_set_defaults(cinfo)` (0x15FD8, `bl 0x1F1BC`); `cinfo->restart_interval = restart` (0; `str.w r8,[r0,#0xec]` at 0x15FE2); `jpeg_set_quality(cinfo, quality, force_baseline = TRUE)` (0x15FE6, `bl 0x1EF7A`, `r2 = 1`); if progressive != 0: `jpeg_simple_progression` (0x16002, `bl 0x1F2C8`); if optimize != 0: `cinfo->optimize_coding = 1` (`[cinfo+0xD8]`); if luma and chroma were both given: `q_scale_factor[0] = jpeg_quality_scaling(luma)`, `q_scale_factor[1] = jpeg_quality_scaling(chroma)`, then `jpeg_default_qtables(cinfo, TRUE)` (0x1EEF4); then `jpeg_start_compress(cinfo, TRUE)` (0x1D5E4). For `{1, 90}` neither the progressive, the optimize nor the luma/chroma branch is taken. | IMG 0x15FD6..0x1604C | NEW | EXACT_SOURCE |
| 2C.8 write(): row loop | A row buffer of `input_components * width` bytes (`[sp+0x3AC]`) is allocated when cn != 1. For each row i in 0..rows-1 the row pointer is `data + i*step`. cn == 1: the row pointer goes straight to `jpeg_write_scanlines` (0x1D640, one line per call, `r2 = 1`). cn == 3: icvCvt_BGR2RGB_8u_C3R (0x11C7E: per pixel `dst[0]=src[2]; dst[1]=src[1]; dst[2]=src[0]`) into the row buffer first. cn == 4: 0x11AA6 (BGRA to BGR) first. Then `jpeg_finish_compress` (0x1D466). | IMG 0x16050..0x160F2; 0x11C7E..0x11CD6 (`ldrb sb,[r5,#-3]; ldrb r8,[r5,#-2]; ldrb ip,[r5,#-1]; strb sb,[r3,#-1]; strb r8,[r3,#-2]; strb ip,[r3,#-3]`) | NEW | EXACT_SOURCE |
| 2C.9 write(): exit | `jpeg_destroy_compress` (0x1D428), buffer frees, `fclose` (PLT 0xDA38) when the FILE was opened here (0x16146 `blx #0xda38`); the function returns the success byte at `[sp+0x2F]` (1 after finish_compress, 0 on error). A libjpeg error goes through the replaced error_exit and longjmp to the setjmp at 0x15F10, which lands on 0x15F14 `cmp r0,#0; bne.w 0x160F8` with `[sp+0x2F] = 0`. | IMG 0x160F8..0x1616E | NEW | EXACT_SOURCE |

### 2D. libjpeg 9 settings used by this path (read from IMG)

| step | what the original does | citation | existing record | classification |
|---|---|---|---|---|
| 2D.1 jpeg_set_defaults (0x1F1BC) | Requires `global_state == 100`, else error 0x15. Allocates the component array (0x370 bytes) if null. `data_precision = 8`, `scale_num = scale_denom = 1`, then `jpeg_set_quality(cinfo, 75, TRUE)`, then the four standard Huffman tables through `jpeg_add_huff_table` (0x1ED9E): DC0 into `cinfo+0x78`, AC0 into `+0x88`, DC1 into `+0x7C`, AC1 into `+0x8C`. Arithmetic tables: `arith_dc_L = 0`, `arith_dc_U = 1`, `arith_ac_K = 5` for 16 entries. Then `num_scans = 0`, `scan_info = NULL`, `raw_data_in = 0`, `arith_code = 0`, `optimize_coding = 0` (1 only if data_precision > 8), `CCIR601_sampling = 0`, `do_fancy_downsampling = 1`, `smoothing_factor = 0`, `dct_method = 0` (JDCT_ISLOW), `restart_interval = 0`, `restart_in_rows = 0`, `write_JFIF_header = 0`, `JFIF_major_version = 1`, `JFIF_minor_version = 1`, `density_unit = 0`, `X_density = 1`, `Y_density = 1`, `write_Adobe_marker = 0`, `color_transform = 0`; then jpeg_default_colorspace (0x1F18C: in_color_space GRAYSCALE -> jpeg_set_colorspace(1), RGB -> jpeg_set_colorspace(3 = YCbCr)). | IMG 0x1F1BC..0x1F2C0 (`movs r1,#0x4b; bl #0x1ef7a`; `str.w r2,[r4,#0xe0]`; `strh.w r2,[r4,#0xfc]`; `strh.w r2,[r4,#0xfe]`; `strb.w r2,[r4,#0xf8]` and `[0xf9]`; `str.w r3,[r4,#0xe8]`) | NEW | EXACT_SOURCE |
| 2D.2 jpeg_set_colorspace (0x1EF94), YCbCr case at 0x1F02C | For RGB input the file colour space is JCS_YCbCr (3): `write_JFIF_header = 1` (`[cinfo+0xF4]`), `write_Adobe_marker = 0`, `num_components = 3`. Components (struct stride 0x58): comp 0 id 1, h_samp 2, v_samp 2, quant_tbl_no 0, dc_tbl_no 0, ac_tbl_no 0; comp 1 id 2, h 1, v 1, quant 1, dc 1, ac 1; comp 2 id 3, h 1, v 1, quant 1, dc 1, ac 1. Sampling is 4:2:0. Grayscale: one component, id 1, 1x1, quant 0, dc 0, ac 0, `write_JFIF_header = 1`. | IMG 0x1F02C..0x1F060 (`movs r1,#2; str r1,[r3,#8]; str r1,[r3,#0xc]` for comp 0; `str r1,[r3,#0x58]` id 2); gray case 0x1EFCC..0x1EFE4 | NEW | EXACT_SOURCE |
| 2D.3 jpeg_set_quality (0x1EF7A) and jpeg_quality_scaling (0x1EF54) | `quality <= 0` gives 1; `quality > 100` gives 100; `quality <= 49` gives `5000 / quality`; otherwise `200 - 2*quality`. For 90 the scale is 20. `q_scale_factor[0] = q_scale_factor[1] = scale`, then `jpeg_default_qtables(cinfo, force_baseline)`. | IMG 0x1EF54..0x1EF78 (`cmp r1,#0x31; ble`; `movw r0,#0x1388`; `rsb.w r0,r1,#0x64; lsls r0,r0,#1`); 0x1EF7A..0x1EF90 | NEW | EXACT_SOURCE |
| 2D.4 jpeg_add_quant_table (0x1EE5E) | For i in 0..63 (natural order): `t = (basic[i] * scale + 50) / 100` (signed divide); `t <= 0` gives 1; `t >= 0x8000` gives 255 if force_baseline else 32767; otherwise if force_baseline and t > 255 then 255. The table is allocated if null (jpeg_alloc_quant_table 0x1ED74); `sent_table = 0` (`str.w r3,[r5,#0x80]`). The standard tables are 32-bit ints at IMG VA 0xCE440 (luminance, 64 entries) and VA 0xCE540 (chrominance, 64 entries). Table 0 uses `q_scale_factor[0]`, table 1 uses `q_scale_factor[1]`. | IMG 0x1EE5E..0x1EEE8 (`adds r0,#0x32; movs r1,#0x64; blx #0xdb1c` = signed divide); 0x1EEF4..0x1EF1C | NEW | EXACT_SOURCE |
| 2D.5 Stage selection | jinit_compress_master (0x1D770), in order: master control (0x1E968); with raw_data_in == 0: colour converter (0x26F3C), downsampler (0x1FE60), prep controller (0x1F7C8); forward DCT (0x27898); entropy encoder: arith_code ? 0x26574 : 0x28AB0 (Huffman); coef controller (0x26AE8, full-buffer only if num_scans > 1 or optimize_coding, else single pass); main controller (0x1D8B0); marker writer (0x1E088). Colour conversion RGB to YCbCr: start routine 0x26B7C (rgb_ycc_start; constants 19595 `0x4C8B`, 38470 `0x9646`, 7471 `0x1D2F` at 0x26B8C..0x26BA0) and convert routine 0x26C18. Downsampling: comp 0 full size (fullsize_downsample, smoothing_factor 0); comps 1 and 2 use `h2v2_downsample` at 0x1FAA6 (chosen when both ratios are 2 and smoothing is 0; the smoothing variant 0x1FAAC is not chosen). Forward DCT: dct_method 0 with 8x8 blocks selects do_dct = 0x2C7D8 (jpeg_fdct_islow: constants 4433 `0x1151`, 6270 `0x187E`, 9633 `0x25A1`, 12299 `0x300B`, 25172 `0x6254`, 16819 `0x41B3`, 2446 `0x98E`), with integer divisors `quantval << 3` (loop 0x276E2..0x276F6 `ldrh r3,[r2,#2]!; lsls r3,r3,#3`); the forward_DCT wrapper is at 0x270F8. The float and ifast variants exist but are not selected. | IMG 0x1D770..0x1D7EA; 0x1FE60..0x1FFB2; 0x2761A..0x27650; 0x276C8..0x276FC; 0x2C7D8..0x2CA24; 0x2702A..0x27034 | NEW | EXACT_SOURCE for selection; bodies see G1 |
| 2D.6 Marker order and constants | write_file_header (0x1DF76): `FF D8`; if write_JFIF_header: `FF E0`, length `00 10`, `4A 46 49 46 00` (JFIF NUL), `[0xF8]` = 1, `[0xF9]` = 1, `[0xFA]` = 0 (units), X_density 2 bytes (`00 01`), Y_density 2 bytes (`00 01`), thumbnail `00 00`; the Adobe marker is skipped (write_Adobe_marker = 0). write_frame_header (0x1DDE4..0x1DF2C): DQT per distinct quant_tbl_no (comp 0 uses table 0; comps 1 and 2 table 1), then SOF0 (`C0`) because the stream is Huffman, sequential, 8-bit and every component has dc_tbl_no <= 1 and ac_tbl_no <= 1 (else SOF1 `C1`; progressive would be `C2`, arithmetic `C9` or `CA`). write_scan_header (region 0x1DCDA..0x1DDB2): for each scan component emit_dht(dc_tbl_no, 0) then emit_dht(ac_tbl_no, 1), each only if `sent_table == 0`, so the DHT segments come as DC0, AC0, DC1, AC1; DRI (`FF DD`) only if restart_interval differs from the last one written (0 and 0: not emitted); then SOS `FF DA`, length `2*ncomp+6 = 12`, ncomp = 3, per component its id and `(dc_tbl_no << 4) | ac_tbl_no` (`01 00`, `02 11`, `03 11`). Stream ends with EOI `FF D9` (constant `movs r1,#0xd9` at 0x1D966 and 0x1DB1A). | IMG 0x1DF76..0x1E080; 0x1DDE4..0x1DF40 (SOF choice 0x1DE56..0x1DE7A); 0x1DD16..0x1DDB2 | NEW | EXACT_SOURCE for order and constants; PARTIAL for the emit_dqt, emit_dht and SOS tail internals (G1) |

### Derived tables at quality 90

`quality = 90` gives `jpeg_quality_scaling(90) = 200 - 180 = 20`. With `force_baseline = TRUE`, `t = (std * 20 + 50) / 100`; no value reaches 0 or exceeds 255.

Standard luminance table read from IMG VA 0xCE440 (natural order, 64 x uint32):

```
16 11 10 16 24 40 51 61 | 12 12 14 19 26 58 60 55 | 14 13 16 24 40 57 69 56 | 14 17 22 29 51 87 80 62
18 22 37 56 68 109 103 77 | 24 35 55 64 81 104 113 92 | 49 64 78 87 103 121 120 101 | 72 92 95 98 112 100 103 99
```

Standard chrominance table read from IMG VA 0xCE540 (natural order):

```
17 18 24 47 99 99 99 99 | 18 21 26 66 99 99 99 99 | 24 26 56 99 99 99 99 99 | 47 66 99 99 99 99 99 99
then 32 entries of 99
```

Derived luminance table (table 0), natural order:

```
 3  2  2  3  5  8 10 12
 2  2  3  4  5 12 12 11
 3  3  3  5  8 11 14 11
 3  3  4  6 10 17 16 12
 4  4  7 11 14 22 21 15
 5  7 11 13 16 21 23 18
10 13 16 17 21 24 24 20
14 18 19 20 22 20 21 20
```

Derived chrominance table (table 1), natural order:

```
 3  4  5  9 20 20 20 20
 4  4  5 13 20 20 20 20
 5  5 11 20 20 20 20 20
 9 13 20 20 20 20 20 20
20 20 20 20 20 20 20 20   (the last four rows are all 20)
```

In the file the DQT payload is in zigzag order (emit_dqt emits through jpeg_natural_order; that routine was not read, see G1). If the standard zigzag is used the 64 payload bytes are:

```
table 0: 03 02 02 03 02 02 03 03 03 03 04 03 03 04 05 08 05 05 04 04 05 0a 07 07 06 08 0c 0a 0c 0c 0b 0a 0b 0b 0d 0e 12 10 0d 0e 11 0e 0b 0b 10 16 10 11 13 14 15 15 15 0c 0f 17 18 16 14 18 12 14 15 14
table 1: 03 04 04 05 04 05 09 05 05 09 14 0d 0b 0d 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14 14
```

Huffman tables written (standard, optimize_coding = 0): the encoder static copies are at IMG VA 0xCE640 (DC0 bits[17], vals at 0xCE658), 0xCE668 (AC0 bits[17], vals at 0xCE680), 0xCE728 (DC1 bits[17], vals at 0xCE740) and 0xCE750 (AC1 bits[17], vals at 0xCE768). They match the decoder static table of Part 1 byte for byte (bits[0] = 0, then the same 16 count bytes, same huffval):

```
DC0 bits[17]: 00 00 01 05 01 01 01 01 01 01 00 00 00 00 00 00 00
DC1 bits[17]: 00 00 03 01 01 01 01 01 01 01 01 01 00 00 00 00 00
AC0 bits[17]: 00 00 02 01 03 03 02 04 03 05 05 04 04 00 00 01 7d
AC1 bits[17]: 00 00 02 01 02 04 04 03 04 07 05 04 04 00 01 02 77
```

Encoder choices established above for `{1, 90}`: baseline sequential (SOF0); 4:2:0 (comp 0 2x2, comps 1 and 2 1x1); standard Annex K Huffman tables; no optimisation; no restart interval (no DRI, no RST); JFIF 1.01 APP0 with density 1:1, unit 0, no thumbnail; no Adobe marker; quality 90 applied to both tables; jpeg_fdct_islow; integer RGB to YCbCr; h2v2_downsample without smoothing; full-size luminance.

### Part 2 unresolved

- **U1. Caller and output extension of Save.** EncodedImage::Save has no caller inside libcozmoEngine.so and is exported; its callers and the extension of the path they pass are not established here. The extension decides which encoder findEncoder picks; the JPEG path above applies only to the jpg and jpeg extensions. Read next: grep `unity/`, `smali/` and `sources/` for EncodedImage and Save, search the other `.so` files for the mangled name, and read the findEncoder extension table (IMG 0xED74 and the registered-encoder vector) for other extensions.
- **G1. libjpeg 9 pipeline bodies not transcribed** (RECOVERABLE_GAP; integer code, no SIMD seen, but not read line by line): rgb_ycc_start 0x26B7C and rgb_ycc_convert 0x26C18 (offset and rounding terms), prep controller 0x1F7C8 (edge replication, context rows), h2v2_downsample 0x1FAA6 (bias alternation), forward_DCT 0x270F8 (level shift and quantisation rounding), jpeg_fdct_islow 0x2C7D8 (body beyond the constants), coef controller 0x26AE8, Huffman encoder 0x28AB0 with its start_pass, encode_mcu and flush (bit padding), emit_dqt 0x1D9B4 (zigzag, precision), emit_dht 0x1DA60, the SOS tail (Ss, Se, AhAl bytes), jpeg_stdio_dest callbacks (buffer size, empty_output_buffer, term; entry 0x206C8 only read to its setup). The B-M3M4 method (shipped imwrite run under `re-analysis/tools/emu/` with byte-identical output) settles these without reading every line.
- **U3.** The assertion message texts raised at imwrite_ lines 455, 459 and 462 were not read; only their call sites are cited.

### Existing records contradicted by the source

None found.

### Existing records with evidence too weak for their claim

- **M3-018 / A25.** Current manifest title: "Colour decode: half-width JPEG, BGR to RGB, cv::resize INTER_LINEAR to 320x240; IsColor; Save at quality 90"; status IMPLEMENTATION_GAP; evidence line "A25 EncodedImage::Save quality 90 (movs r2,#0x5a; 0x004F2EFC..0x004F2FA8)"; inventory row A25 is marked EXACT_SOURCE in `re-analysis/inventory/M3-device.md` line 131. The cited range covers only the encoding dispatch and the call into ImageBase::Save. It does not contain: ImageBase::Save (cvtColor code 4, params `{1, 90}`, CreateDirectory, imwrite and its two log lines), the three EncodedImage log calls and the return values (0, 1, or the decode Result), the raw WriteFile path, or any of the JPEG encoder. The Save at encoding 9 re-encodes the decoded and resized 320x240 RGB image, not the half-width input JPEG. A25 as EXACT_SOURCE supports one function inside a path that has more behaviour-changing steps than the record names (rows 2A.3 to 2A.8, 2B.1 to 2B.7, 2C.1 to 2D.6 are NEW).
- **M3-001 J rows.** The default-table branch had no row (Part 1 rows 1.2 to 1.16 are NEW). The note "count <= 256, selector <= 3" is correct but incomplete (rows 1.6, 1.9, 1.15).

### Open questions for the manager

1. U1: which code calls EncodedImage::Save and with what extension? Until that is known only the jpg and jpeg path is described.
2. Save for encoding 9 is now established as: decode (A9/A10) into a 320x240 RGB image, then ImageRGB save at quality 90 as a re-encoded JPEG. Confirm that the build treats the decode as owned by A9/A10 and the Save rows here as the encoder.
3. Should the emulator run of imwrite on a test image (to confirm the table, marker order and the G1 bodies byte for byte) be part of the oracle job, or be sent back to this extractor as a follow-up? It was not done in this pass.
