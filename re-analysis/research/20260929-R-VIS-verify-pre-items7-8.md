# Verification of R-VIS pre-extraction Part 2 items 7 and 8 (lines 508-579)

Method: own capstone Thumb disassembly (.scratch/v78/dd.py, plt.py) of libopencv_imgproc.so, libopencv_core.so, libcozmoEngine.so; PLT slots resolved via .pltgot_relocations. All functions checked are Thumb-2. Manifest quoted (current): M11-021 "The per-frame marker-mode gate" IMPLEMENTATION_GAP (unresolved: non-divisible branch 0x20EC0..0x20F58 not transcribed; ColumnSum<int,uchar> not read, "the 16S body is reused"); M11-033 "The image hand-off and the two VisionSystem::Update overloads" IMPLEMENTATION_GAP (unresolved: RGB2GRAY kernel not transcribed; colour branch inert). Report quotes match.

## Item 7A CLAHE_Impl::apply 0x20DAC
A1 PASS. 0x20DCC/0x20DD4/0x20DE0 (type 0 or 2), error line 0x162 at 0x20E0E, histSize 0x100/0x1000 at 0x20E46..0x20E50.
A2 PASS content; address slip: first size blx is 0x20E54 (not 0x20E58), idivmod blx 0x20E5C, cbnz 0x20E60. PLT: 0x172a8 _InputArray::size, 0x17230 __aeabi_idivmod, 0x17284 __aeabi_idiv.
A3 PASS (0x20E82..0x20EBE).
A4 PASS: literal 0x21198 = 0x02010000; 0x20ECA add.w sl,r4,#0x18; 0x20ED0..0x20ED4.
A5 PASS (0x20EDC..0x20EEE, bottom = tilesY - h%tilesY).
A6 PASS (0x20EF8..0x20F0A, right = tilesX - w%tilesX, unconditional).
A7 PASS: PLT 0x17434 = copyMakeBorder(InputArray,OutputArray,int x5,Scalar const&); top 0, bottom sb, left [sp]=0, right [sp+4], border 4 [sp+8], Scalar* [sp+0xc] (zero 32 bytes sp+0xd0..0xef).
A8 PASS: literal 0x2119C = 0x01010000; size.p at this+0x40 (Mat at +0x18, size at +0x28); tileW = p[1]/tilesX, tileH = p[0]/tilesY.
A9 PASS: 0x20F60 mul, 0x20F80 vdiv.f32 ((histSize-1)/total), 0x20F64/0x20F84 clip>0 test, 0x20F8E vmul.f64, 0x20F96 vdiv.f64, 0x20F9A vcvt.s32.f64 (truncation), 0x20FA2..A6 max(.,1), else 0.
A10 PASS for the LUT body (0x2104A..0x21086: new 0x88, srcForLut +4, lut_ (this+0x50) +0x3c, tileW +0x74, tileH +0x78, tilesX +0x7c, clip +0x80, lutScale +0x84). Interpolation body built at 0x21234.. (new 0x4f4): src Mat sp+0x60 at +4, dst sp+0x98 at +0x3c, lut_ +0x74, tileW +0xac, tileH +0xb0, tilesX +0xb4, tilesY +0xb8; uses the ORIGINAL src (claim right). CLASSIFICATION ISSUE: report calls the interpolation-body build a RECOVERABLE_GAP (0x21200..0x21400 unread); that range is the common tail of both branches and its per-column tables 0x2129E..0x21370 are already in M11-021 evidence/C3.5, so it is not a border-branch gap. Unmentioned, not output-affecting: lut_ reuse test 0x21006..0x21032 -> 0x21618.
A11 PASS (structure). borderInterpolate 0x44214: p<len unsigned returns p; types 2/4 delta = (type==4) via 0x4423E..0x44242; len==1 -> 0 (0x44246); loop 0x4424A..0x4425E; type 3 uses idiv/idivmod (0x1fa14/0x1fa20). copyMakeBorder 0x4514C: bics #0x10 at 0x452BA, beq 0x45512 constant path; left pad borderInterpolate(j-left, cols) at 0x45358, right (j+cols) at 0x45392; interior memcpy PLT 0x1f978; byte loops 0x4545E..0x45472 and 0x45474..0x4548C for elemSize 1; top rows 0x454BC, bottom rows 0x454E8.

## Item 7B boxFilter and ColumnSum<int,uchar>
B1 PASS with mislabel: `bl 0x183ae` (0xAB798, 0xAB7D8) is a local getMat-type thunk (this, InputArray, -1), not _InputArray::type; type comes from Mat flags [sp+0x34]. 0xAB76D export, shortcut test 0xAB7DE/E0/E6, createBoxFilter 0xAB828, vtable+0x14 apply 0xAB836..0xAB852 all correct.
B2 PASS (0xAB66C, 0xAB68A/0xAB6AE, limits 0xAB696..0xAB6A6).
B3 PASS (0xAB6D2..0xAB6FE).
B4 PASS (0xA628C..0xA6292 anchor; 0xA629E new 0x28; fields +4,+8,+0x10,+0x18,+0x1c..0x24).
B5 PASS: literal 0xA66C4 = 0x55872; 0xA62B6+0x55872 = 0xFBB28, +8 = 0xFBB30; slots 0xA6C51, 0xA6C71, 0xAA501, 0xA259F. Typeinfo 0xFD738 -> name 0xF5030 "N2cv9ColumnSumIihEE" = ColumnSum<int,uchar>.
B6 PASS (0xAA500..0xAA53C; [sp+0xb0]=count, [sp+0xb4]=width).
B7 PASS. B8 PASS (error line 0x10c). B9 PASS.
B10 PASS (0xAA606, 0xAA61E, 0xAA620.., vqmovun.s32/vqmovn.u16, tail cmp 0xff).
B11 PASS: 0xAA630 vcvt.f32.f64, 0xAA63E vdup, bound width-7 (0xAA5F2), 0xAA680 vcvt.f32.s32, 0xAA684 vmul.f32, bl 0x265DC at 0xAA690 and 0xAA6B8, vqmovn.u32 0xAA6B4/0xAA6CC, vqmovn.u16 0xAA6D4, vst1.8 0xAA6D8, vsub.i32 0xAA6E8/0xAA6FA. Width 320 = 40 iterations, no tail.
B12 PASS: 0x265DC vmov.f32 q8,#0.5 (0x26602; guard PLT 0x17470/0x1747c = __cxa_guard_acquire/release), vadd.f32 0x2661A, vcvt.u32.f32 0x2661E (truncate, saturate, negative->0).
B13 PASS (0xAA70A..0xAA74E; vcvtr.s32.f64 at 0xAA728 uses FPSCR mode, assumed RN at runtime).
B14 PASS (0xAA7E2..0xAA7EC, exit 0xAA7F8).
B15 PASS but identity is recoverable: 0xAA820 is slot 2 (0xAA821) of vtable base 0xFBB58, typeinfo 0xFD750 -> name 0xF5060 "N2cv9ColumnSumIitEE" = ColumnSum<int,ushort> (selected for dst depth 2 = 16U). The 16S body (ColumnSum<int,short>) is 0xAAB44: vtable base 0xFBB40, slot2 0xAAB45, typeinfo 0xFD744 -> 0xF5048 "N2cv9ColumnSumIisEE"; it calls a different helper (bl 0x7a284 at 0xAACD8/0xAAD00) and vqmovn.s32 (0xAACFC), so it is not the uchar body.
B16 PASS as a stated gap (RowSum 0xA2EE8, FilterEngine 0xA6E74, apply unread).

Item 7 vs record: "the 16S body is reused" is contradicted (0xAA500 own body; 16S body is 0xAAB44 with a different rounding helper). Note for later comparison (not a citation error): cozmo-stack/src/Cozmo.Robot/Animation/OpenCv310.cs:909 uses RoundHalfAwayFromZero((float)s*(float)scale) for the vector part; native is float mul then float +0.5f then truncating vcvt.u32.f32 (0x2661A/0x2661E).

## Item 8 RGB2GRAY
FillGray 0x872998 (Thumb) PASS: 0x8729AA..AC flags 0x81010010; 0x8729B2..B4 flags 0x82010000; 0x8729C6 movs r2,#7; 0x8729C8 movs r3,#0; 0x8729CA blx 0x4A598C = cv::cvtColor (imgproc export 0x2bc79). Unreported: FillGray first copies timestamp [this+0x3c] to [dst+0x3c] (0x87299C..0x8729A0).
C1 PASS (0x2BCAC, 0x2BCB8, tst sb,#5 / cmp r6,#5, error 0x1eff at 0x2BD38).
C2 PASS: tbh 0x2BD5E, table 0x2BD62; codes 6,7,10,11 all = 0x2E3 -> 0x2C328 (checked); 8,9 -> 0x2C698.
C3 PASS (0x2C328..0x2C37E). C4 PASS (0x2C3A0..0x2C3B2). C5 PASS (0x2C3C0).
C6 PASS: literal 0x2C7C0 = 0xB655E; +0x2C3D2+0x180 = 0xE2AB0; words 4899, 9617, 1868, 0.
C7 PASS (loop 0x2C400..0x2C414; tab0[i]=i*coeffs[blueIdx^2], tab1[i]=i*coeffs[1], tab2[i]=0x2000+i*coeffs[blueIdx]).
C8 PASS with mislabel: literal 0x2C7C4 -> 0xF9FE0, +8 = 0xF9FE8; object {vtbl, src Mat, dst Mat, functor}. Mat::total (0x2C434, bl 0x2663c) is called on fp = the SOURCE Mat (0x2C41E mov r0,fp) and Range end is src.rows ([fp+8], 0x2C416); the report says dst.total(). Same values here, no effect.
C9 PASS: vtable slot2 0x24911; body 0x24910; tab[src0]+tab[256+src1]+tab[512+src2], asrs #14, strb; cols = [src+0xC].
C10 PASS: gray = (4899*src[0]+9617*src[1]+1868*src[2]+8192)>>14; src[0]=R only if the engine RGB buffer is R-first (open).
C11 PASS as gap.

## Stays open
- copyMakeBorder constant path 0x45512, dst.create front 0x4525C..0x452B4 (unused by CLAHE).
- boxFilter RowSum 0xA2EE8, FilterEngine ctor 0xA6E74 and apply.
- 16U CLAHE branch 0x210E0.., cvtColor 16U/32F 0x2C460...
- DecodeImageRGB channel order; the rest of M11-033 (SetNextImage, Processor, UpdateVisionSystem, both Update overloads) not covered by items 7/8.
- Runtime FPSCR rounding mode for vcvtr (B13).
- Neither record is raised; both remain IMPLEMENTATION_GAP until the C# is compared to the transcribed bodies.

Verdict: no row wrong in substance. Corrections: A2 blx address, B1 label of 0x183ae, C8 total/rows are on src, A10 gap misclassified, B15 identity resolvable.
