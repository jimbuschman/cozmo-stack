# Plug-in138 persistent type dependency graphs

Primary: setup-design/live-design native companions; exact engine hash is in those files. Each address row transcribes one native dependency. Copies preserve raw identity; native integer mantissa/exponent operations are retained rather than replaced with pow. VMLA includes old destination before both multiplicands. Native SQRT and imported fallback are separate operations with an unordered-result selection. Gain threshold native MI means ordered less; unordered follows the polynomial branch.

These graphs bind raw F32 Rate, CappedFrequency, Gain and Q from P138527/P138533. CappedFrequency uses their exact LE/unordered selection. No PCM inputs. Entry type0 skips design; unknown nonzero six-zero fallback remains P138533C. Imports reuse primary-resolved cosf/sinf/tanf/sqrtf from the EQ graph companions; imported phone bodies remain outside shipped engine. Publication is P138534, independently transcribed.

This is symbolic research, not numerical execution, an implementation or an equivalence test.

## Init type 1

| Native address | Dependency |
|---|---|
| 00AA3B68 | vmul.f32(Gain, F32(3CCCCCCD)) |
| 00AA3B80 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| 00AA3B8C | vcvt.u32.f32(n2) |
| 00AA3B94 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| 00AA3B98 | lsr.raw32(n3, RawU32(00000017)) |
| 00AA3B9C | add.raw32(n4, RawU32(3F800000)) |
| 00AA3BA0 | lsl.raw32(n5, RawU32(00000017)) |
| 00AA3BA8 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| 00AA3BAC | vmla.f32(F32(3F272DDB), n6, n7) |
| 00AA3BB4 | vmul.f32(n8, n9) |
| 00AA3B74 | select(n10, F32(00000000), n11) |
| 00AA3BB8 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA3BBC | vdiv.f32(n12, Rate) |
| 00AA3BC4 | phone.sinf(n13) |
| 00AA3BCC | vdiv.f32(F32(3F800000), n14) |
| 00AA3BD8 | vadd.f32(n14, n15) |
| 00AA3BDC | vmla.f32(F32(40000000), n16, F32(00000000)) |
| 00AA3BE0 | vsqrt.f32(n17) |
| 00AA41DC | phone.sqrtf(n17) |
| 00AA3BEC | select(n19, n20, n18) |
| 00AA3BF4 | phone.cosf(n13) |
| 00AA3BF8 | vsqrt.f32(n14) |
| 00AA3C08 | vadd.f32(n14, F32(3F800000)) |
| 00AA3C10 | vsub.f32(n14, F32(3F800000)) |
| 00AA41CC | phone.sqrtf(n14) |
| 00AA3C14 | select(n22, n23, n21) |
| 00AA3C1C | vmul.f32(n24, F32(3F000000)) |
| 00AA3C20 | vmul.f32(n25, n26) |
| 00AA3C24 | vmul.f32(n27, n28) |
| 00AA3C28 | vadd.f32(n29, n29) |
| 00AA3C2C | vmul.f32(n27, n30) |
| 00AA3C30 | vmul.f32(n31, n32) |
| 00AA3C34 | vsub.f32(n30, n33) |
| 00AA3C38 | vadd.f32(n14, n14) |
| 00AA3C3C | vadd.f32(n30, n33) |
| 00AA3C40 | vsub.f32(n34, n35) |
| 00AA3C44 | vadd.f32(n35, n34) |
| 00AA3C48 | vsub.f32(n28, n36) |
| 00AA3C50 | vadd.f32(n28, n36) |
| 00AA3C54 | vmul.f32(n14, n37) |
| 00AA3C58 | vmul.f32(n38, n39) |
| 00AA3C5C | vmul.f32(n14, n40) |
| 00AA3C60 | vmul.f32(n41, F32(C0000000)) |
| 00AA3C64 | vadd.f32(n35, n42) |
| 00AA3C68 | vsub.f32(n42, n35) |

| Common input | Exact dependency |
|---|---|
| N0 | n43 |
| N1 | n44 |
| N2 | n45 |
| D2 | n46 |
| D1 | n47 |
| Den | n48 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(Gain, F32(3CCCCCCD)) |
| n2 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| n3 | vcvt.u32.f32(n2) |
| n4 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| n5 | lsr.raw32(n3, RawU32(00000017)) |
| n6 | add.raw32(n4, RawU32(3F800000)) |
| n7 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| n8 | vmla.f32(F32(3F272DDB), n6, n7) |
| n9 | lsl.raw32(n5, RawU32(00000017)) |
| n10 | nativeMI(n1, F32(C2140000)) |
| n11 | vmul.f32(n8, n9) |
| n12 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n13 | vdiv.f32(n12, Rate) |
| n14 | select(n10, F32(00000000), n11) |
| n15 | vdiv.f32(F32(3F800000), n14) |
| n16 | vadd.f32(n14, n15) |
| n17 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| n18 | vsqrt.f32(n17) |
| n19 | unordered(n18, n18) |
| n20 | phone.sqrtf(n17) |
| n21 | vsqrt.f32(n14) |
| n22 | unordered(n21, n21) |
| n23 | phone.sqrtf(n14) |
| n24 | phone.sinf(n13) |
| n25 | vmul.f32(n24, F32(3F000000)) |
| n26 | select(n19, n20, n18) |
| n27 | phone.cosf(n13) |
| n28 | vsub.f32(n14, F32(3F800000)) |
| n29 | select(n22, n23, n21) |
| n30 | vadd.f32(n14, F32(3F800000)) |
| n31 | vmul.f32(n25, n26) |
| n32 | vadd.f32(n29, n29) |
| n33 | vmul.f32(n27, n28) |
| n34 | vsub.f32(n30, n33) |
| n35 | vmul.f32(n31, n32) |
| n36 | vmul.f32(n27, n30) |
| n37 | vadd.f32(n35, n34) |
| n38 | vadd.f32(n14, n14) |
| n39 | vsub.f32(n28, n36) |
| n40 | vsub.f32(n34, n35) |
| n41 | vadd.f32(n28, n36) |
| n42 | vadd.f32(n30, n33) |
| n43 | vmul.f32(n14, n37) |
| n44 | vmul.f32(n38, n39) |
| n45 | vmul.f32(n41, F32(C0000000)) |
| n46 | vmul.f32(n14, n40) |
| n47 | vsub.f32(n42, n35) |
| n48 | vadd.f32(n35, n42) |

## Init type 2

| Native address | Dependency |
|---|---|
| 00AA3FEC | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA3FF0 | vdiv.f32(n1, Rate) |
| 00AA3FF8 | phone.cosf(n2) |
| 00AA3FFC | vmul.f32(Gain, F32(3CCCCCCD)) |
| 00AA4018 | vmla.f32(F32(4E7E0000), n3, F32(4BD49A78)) |
| 00AA4024 | vcvt.u32.f32(n4) |
| 00AA402C | ubfx.raw32(n5, RawU32(00000000), RawU32(00000017)) |
| 00AA4030 | lsr.raw32(n5, RawU32(00000017)) |
| 00AA4034 | add.raw32(n6, RawU32(3F800000)) |
| 00AA4038 | lsl.raw32(n7, RawU32(00000017)) |
| 00AA4040 | vmla.f32(F32(3CAA70DE), n8, F32(3EA67F46)) |
| 00AA4044 | vmla.f32(F32(3F272DDB), n8, n9) |
| 00AA404C | vmul.f32(n10, n11) |
| 00AA400C | select(n12, F32(00000000), n13) |
| 00AA4054 | phone.sinf(n2) |
| 00AA4058 | vadd.f32(Q, Q) |
| 00AA4060 | vmul.f32(n14, F32(C0000000)) |
| 00AA4068 | vdiv.f32(n15, n16) |
| 00AA406C | vdiv.f32(n17, n18) |
| 00AA4070 | vmul.f32(n17, n18) |
| 00AA407C | vadd.f32(n19, F32(3F800000)) |
| 00AA4080 | vadd.f32(n20, F32(3F800000)) |
| 00AA4084 | vsub.f32(F32(3F800000), n19) |
| 00AA4088 | vsub.f32(F32(3F800000), n20) |

| Common input | Exact dependency |
|---|---|
| N0 | n21 |
| N1 | n22 |
| N2 | n22 |
| D2 | n23 |
| D1 | n24 |
| Den | n25 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | vmul.f32(Gain, F32(3CCCCCCD)) |
| n4 | vmla.f32(F32(4E7E0000), n3, F32(4BD49A78)) |
| n5 | vcvt.u32.f32(n4) |
| n6 | ubfx.raw32(n5, RawU32(00000000), RawU32(00000017)) |
| n7 | lsr.raw32(n5, RawU32(00000017)) |
| n8 | add.raw32(n6, RawU32(3F800000)) |
| n9 | vmla.f32(F32(3CAA70DE), n8, F32(3EA67F46)) |
| n10 | vmla.f32(F32(3F272DDB), n8, n9) |
| n11 | lsl.raw32(n7, RawU32(00000017)) |
| n12 | nativeMI(n3, F32(C2140000)) |
| n13 | vmul.f32(n10, n11) |
| n14 | phone.cosf(n2) |
| n15 | phone.sinf(n2) |
| n16 | vadd.f32(Q, Q) |
| n17 | vdiv.f32(n15, n16) |
| n18 | select(n12, F32(00000000), n13) |
| n19 | vmul.f32(n17, n18) |
| n20 | vdiv.f32(n17, n18) |
| n21 | vadd.f32(n19, F32(3F800000)) |
| n22 | vmul.f32(n14, F32(C0000000)) |
| n23 | vsub.f32(F32(3F800000), n19) |
| n24 | vsub.f32(F32(3F800000), n20) |
| n25 | vadd.f32(n20, F32(3F800000)) |

## Init type 3

| Native address | Dependency |
|---|---|
| 00AA4090 | vmul.f32(Gain, F32(3CCCCCCD)) |
| 00AA40A8 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| 00AA40B4 | vcvt.u32.f32(n2) |
| 00AA40BC | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| 00AA40C0 | lsr.raw32(n3, RawU32(00000017)) |
| 00AA40C4 | add.raw32(n4, RawU32(3F800000)) |
| 00AA40C8 | lsl.raw32(n5, RawU32(00000017)) |
| 00AA40D0 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| 00AA40D4 | vmla.f32(F32(3F272DDB), n6, n7) |
| 00AA40DC | vmul.f32(n8, n9) |
| 00AA409C | select(n10, F32(00000000), n11) |
| 00AA40E0 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA40E8 | vdiv.f32(n12, Rate) |
| 00AA40EC | vdiv.f32(F32(3F800000), n13) |
| 00AA40F4 | phone.sinf(n14) |
| 00AA40FC | vadd.f32(n13, n15) |
| 00AA4104 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| 00AA4108 | vsqrt.f32(n17) |
| 00AA41BC | phone.sqrtf(n17) |
| 00AA4114 | select(n19, n20, n18) |
| 00AA411C | phone.cosf(n14) |
| 00AA4120 | vsqrt.f32(n13) |
| 00AA4130 | vadd.f32(n13, F32(3F800000)) |
| 00AA4138 | vsub.f32(n13, F32(3F800000)) |
| 00AA41AC | phone.sqrtf(n13) |
| 00AA413C | select(n22, n23, n21) |
| 00AA4144 | vmul.f32(n24, F32(3F000000)) |
| 00AA4148 | vmul.f32(n25, n26) |
| 00AA414C | vadd.f32(n27, n27) |
| 00AA4150 | vmul.f32(n28, n29) |
| 00AA4154 | vmul.f32(n30, n31) |
| 00AA4158 | vmul.f32(n28, n32) |
| 00AA4160 | vadd.f32(n32, n33) |
| 00AA4164 | vmul.f32(n13, F32(C0000000)) |
| 00AA4168 | vsub.f32(n32, n33) |
| 00AA416C | vadd.f32(n34, n35) |
| 00AA4170 | vadd.f32(n29, n36) |
| 00AA4174 | vsub.f32(n35, n34) |
| 00AA4178 | vsub.f32(n29, n36) |
| 00AA417C | vmul.f32(n13, n37) |
| 00AA4180 | vadd.f32(n34, n38) |
| 00AA4184 | vmul.f32(n13, n39) |
| 00AA4188 | vsub.f32(n38, n34) |
| 00AA418C | vadd.f32(n40, n40) |
| 00AA4190 | vmul.f32(n41, n42) |

| Common input | Exact dependency |
|---|---|
| N0 | n43 |
| N1 | n44 |
| N2 | n45 |
| D2 | n46 |
| D1 | n47 |
| Den | n48 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(Gain, F32(3CCCCCCD)) |
| n2 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| n3 | vcvt.u32.f32(n2) |
| n4 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| n5 | lsr.raw32(n3, RawU32(00000017)) |
| n6 | add.raw32(n4, RawU32(3F800000)) |
| n7 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| n8 | vmla.f32(F32(3F272DDB), n6, n7) |
| n9 | lsl.raw32(n5, RawU32(00000017)) |
| n10 | nativeMI(n1, F32(C2140000)) |
| n11 | vmul.f32(n8, n9) |
| n12 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n13 | select(n10, F32(00000000), n11) |
| n14 | vdiv.f32(n12, Rate) |
| n15 | vdiv.f32(F32(3F800000), n13) |
| n16 | vadd.f32(n13, n15) |
| n17 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| n18 | vsqrt.f32(n17) |
| n19 | unordered(n18, n18) |
| n20 | phone.sqrtf(n17) |
| n21 | vsqrt.f32(n13) |
| n22 | unordered(n21, n21) |
| n23 | phone.sqrtf(n13) |
| n24 | phone.sinf(n14) |
| n25 | vmul.f32(n24, F32(3F000000)) |
| n26 | select(n19, n20, n18) |
| n27 | select(n22, n23, n21) |
| n28 | phone.cosf(n14) |
| n29 | vsub.f32(n13, F32(3F800000)) |
| n30 | vmul.f32(n25, n26) |
| n31 | vadd.f32(n27, n27) |
| n32 | vadd.f32(n13, F32(3F800000)) |
| n33 | vmul.f32(n28, n29) |
| n34 | vmul.f32(n30, n31) |
| n35 | vadd.f32(n32, n33) |
| n36 | vmul.f32(n28, n32) |
| n37 | vadd.f32(n34, n35) |
| n38 | vsub.f32(n32, n33) |
| n39 | vsub.f32(n35, n34) |
| n40 | vsub.f32(n29, n36) |
| n41 | vmul.f32(n13, F32(C0000000)) |
| n42 | vadd.f32(n29, n36) |
| n43 | vmul.f32(n13, n37) |
| n44 | vmul.f32(n41, n42) |
| n45 | vadd.f32(n40, n40) |
| n46 | vmul.f32(n13, n39) |
| n47 | vsub.f32(n38, n34) |
| n48 | vadd.f32(n34, n38) |

## Init type 4

| Native address | Dependency |
|---|---|
| 00AA3EF4 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| 00AA3EF8 | vdiv.f32(n1, Rate) |
| 00AA3F00 | phone.tanf(n2) |
| 00AA3F08 | vdiv.f32(F32(3F800000), n3) |
| 00AA3F0C | vmul.f32(n4, n4) |
| 00AA3F10 | vmul.f32(n4, F32(3FB504F3)) |
| 00AA3F14 | vadd.f32(n5, F32(3F800000)) |
| 00AA3F18 | vadd.f32(n6, n7) |
| 00AA3F1C | vdiv.f32(F32(3F800000), n8) |
| 00AA3F20 | vsub.f32(F32(3F800000), n5) |
| 00AA3F24 | vsub.f32(n7, n6) |
| 00AA3F28 | vadd.f32(n9, n9) |
| 00AA3F34 | vmul.f32(n9, n10) |
| 00AA3F38 | vmul.f32(n11, n12) |

| Common input | Exact dependency |
|---|---|
| N0 | n9 |
| N1 | n11 |
| N2 | n13 |
| D2 | n9 |
| D1 | n14 |
| Den | F32(3F800000) |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.tanf(n2) |
| n4 | vdiv.f32(F32(3F800000), n3) |
| n5 | vmul.f32(n4, n4) |
| n6 | vmul.f32(n4, F32(3FB504F3)) |
| n7 | vadd.f32(n5, F32(3F800000)) |
| n8 | vadd.f32(n6, n7) |
| n9 | vdiv.f32(F32(3F800000), n8) |
| n10 | vsub.f32(n7, n6) |
| n11 | vadd.f32(n9, n9) |
| n12 | vsub.f32(F32(3F800000), n5) |
| n13 | vmul.f32(n11, n12) |
| n14 | vmul.f32(n9, n10) |

## Init type 5

| Native address | Dependency |
|---|---|
| 00AA3F48 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| 00AA3F4C | vdiv.f32(n1, Rate) |
| 00AA3F54 | phone.tanf(n2) |
| 00AA3F64 | vmul.f32(n3, n3) |
| 00AA3F68 | vmul.f32(n3, F32(3FB504F3)) |
| 00AA3F6C | vadd.f32(n4, F32(3F800000)) |
| 00AA3F70 | vadd.f32(n5, n6) |
| 00AA3F74 | vdiv.f32(F32(3F800000), n7) |
| 00AA3F78 | vmul.f32(n8, F32(C0000000)) |
| 00AA3F7C | vsub.f32(n4, F32(3F800000)) |
| 00AA3F80 | vsub.f32(n6, n5) |
| 00AA3F8C | vnmul.f32(n9, n10) |
| 00AA3F90 | vmul.f32(n8, n11) |

| Common input | Exact dependency |
|---|---|
| N0 | n8 |
| N1 | n9 |
| N2 | n12 |
| D2 | n8 |
| D1 | n13 |
| Den | F32(3F800000) |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.tanf(n2) |
| n4 | vmul.f32(n3, n3) |
| n5 | vmul.f32(n3, F32(3FB504F3)) |
| n6 | vadd.f32(n4, F32(3F800000)) |
| n7 | vadd.f32(n5, n6) |
| n8 | vdiv.f32(F32(3F800000), n7) |
| n9 | vmul.f32(n8, F32(C0000000)) |
| n10 | vsub.f32(n4, F32(3F800000)) |
| n11 | vsub.f32(n6, n5) |
| n12 | vnmul.f32(n9, n10) |
| n13 | vmul.f32(n8, n11) |

## Init type 6

| Native address | Dependency |
|---|---|
| 00AA3E6C | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA3E70 | vdiv.f32(n1, Rate) |
| 00AA3E78 | phone.cosf(n2) |
| 00AA3E84 | phone.sinf(n2) |
| 00AA3E88 | vadd.f32(Q, Q) |
| 00AA3E98 | vmul.f32(n3, F32(C0000000)) |
| 00AA3EA0 | vdiv.f32(n4, n5) |
| 00AA3EA4 | vadd.f32(n6, F32(3F800000)) |
| 00AA3EA8 | vneg.f32(n6) |
| 00AA3EAC | vsub.f32(F32(3F800000), n6) |

| Common input | Exact dependency |
|---|---|
| N0 | n6 |
| N1 | F32(00000000) |
| N2 | n7 |
| D2 | n8 |
| D1 | n9 |
| Den | n10 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.cosf(n2) |
| n4 | phone.sinf(n2) |
| n5 | vadd.f32(Q, Q) |
| n6 | vdiv.f32(n4, n5) |
| n7 | vmul.f32(n3, F32(C0000000)) |
| n8 | vneg.f32(n6) |
| n9 | vsub.f32(F32(3F800000), n6) |
| n10 | vadd.f32(n6, F32(3F800000)) |

## Init type 7

| Native address | Dependency |
|---|---|
| 00AA3F98 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA3F9C | vdiv.f32(n1, Rate) |
| 00AA3FA4 | phone.cosf(n2) |
| 00AA3FA8 | vadd.f32(Q, Q) |
| 00AA3FB4 | phone.sinf(n2) |
| 00AA3FBC | vmul.f32(n3, F32(C0000000)) |
| 00AA3FCC | vdiv.f32(n4, n5) |
| 00AA3FD4 | vadd.f32(n6, F32(3F800000)) |
| 00AA3FDC | vsub.f32(F32(3F800000), n6) |

| Common input | Exact dependency |
|---|---|
| N0 | F32(3F800000) |
| N1 | n7 |
| N2 | n7 |
| D2 | F32(3F800000) |
| D1 | n8 |
| Den | n9 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.cosf(n2) |
| n4 | phone.sinf(n2) |
| n5 | vadd.f32(Q, Q) |
| n6 | vdiv.f32(n4, n5) |
| n7 | vmul.f32(n3, F32(C0000000)) |
| n8 | vsub.f32(F32(3F800000), n6) |
| n9 | vadd.f32(n6, F32(3F800000)) |

## Live0 type 1

| Native address | Dependency |
|---|---|
| 00AA48A8 | vmul.f32(Gain, F32(3CCCCCCD)) |
| 00AA48C4 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| 00AA48D0 | vcvt.u32.f32(n2) |
| 00AA48D8 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| 00AA48DC | lsr.raw32(n3, RawU32(00000017)) |
| 00AA48E0 | add.raw32(n4, RawU32(3F800000)) |
| 00AA48E4 | lsl.raw32(n5, RawU32(00000017)) |
| 00AA48EC | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| 00AA48F0 | vmla.f32(F32(3F272DDB), n6, n7) |
| 00AA48F8 | vmul.f32(n8, n9) |
| 00AA48B4 | select(n10, F32(00000000), n11) |
| 00AA4900 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA4904 | vdiv.f32(n12, Rate) |
| 00AA490C | phone.sinf(n13) |
| 00AA4918 | vdiv.f32(F32(3F800000), n14) |
| 00AA4924 | vadd.f32(n14, n15) |
| 00AA4928 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| 00AA492C | vsqrt.f32(n17) |
| 00AA57EC | phone.sqrtf(n17) |
| 00AA4938 | select(n19, n20, n18) |
| 00AA4940 | phone.cosf(n13) |
| 00AA4944 | vsqrt.f32(n14) |
| 00AA4954 | vadd.f32(n14, F32(3F800000)) |
| 00AA495C | vsub.f32(n14, F32(3F800000)) |
| 00AA57DC | phone.sqrtf(n14) |
| 00AA4960 | select(n22, n23, n21) |
| 00AA4968 | vmul.f32(n24, F32(3F000000)) |
| 00AA496C | vmul.f32(n25, n26) |
| 00AA4970 | vadd.f32(n27, n27) |
| 00AA4974 | vmul.f32(n28, n29) |
| 00AA4978 | vmul.f32(n30, n31) |
| 00AA497C | vmul.f32(n28, n32) |
| 00AA4980 | vsub.f32(n32, n33) |
| 00AA4984 | vsub.f32(n29, n34) |
| 00AA4988 | vadd.f32(n35, n36) |
| 00AA498C | vadd.f32(n29, n34) |
| 00AA4990 | vadd.f32(n14, n14) |
| 00AA4998 | vadd.f32(n32, n33) |
| 00AA499C | vsub.f32(n36, n35) |
| 00AA49A0 | vmul.f32(n14, n37) |
| 00AA49A4 | vmul.f32(n38, n39) |
| 00AA49A8 | vmul.f32(n40, F32(C0000000)) |
| 00AA49AC | vmul.f32(n14, n41) |
| 00AA49B0 | vadd.f32(n35, n42) |
| 00AA49B4 | vsub.f32(n42, n35) |

| Common input | Exact dependency |
|---|---|
| N0 | n43 |
| N1 | n44 |
| N2 | n45 |
| D2 | n46 |
| D1 | n47 |
| Den | n48 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(Gain, F32(3CCCCCCD)) |
| n2 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| n3 | vcvt.u32.f32(n2) |
| n4 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| n5 | lsr.raw32(n3, RawU32(00000017)) |
| n6 | add.raw32(n4, RawU32(3F800000)) |
| n7 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| n8 | vmla.f32(F32(3F272DDB), n6, n7) |
| n9 | lsl.raw32(n5, RawU32(00000017)) |
| n10 | nativeMI(n1, F32(C2140000)) |
| n11 | vmul.f32(n8, n9) |
| n12 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n13 | vdiv.f32(n12, Rate) |
| n14 | select(n10, F32(00000000), n11) |
| n15 | vdiv.f32(F32(3F800000), n14) |
| n16 | vadd.f32(n14, n15) |
| n17 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| n18 | vsqrt.f32(n17) |
| n19 | unordered(n18, n18) |
| n20 | phone.sqrtf(n17) |
| n21 | vsqrt.f32(n14) |
| n22 | unordered(n21, n21) |
| n23 | phone.sqrtf(n14) |
| n24 | phone.sinf(n13) |
| n25 | vmul.f32(n24, F32(3F000000)) |
| n26 | select(n19, n20, n18) |
| n27 | select(n22, n23, n21) |
| n28 | phone.cosf(n13) |
| n29 | vsub.f32(n14, F32(3F800000)) |
| n30 | vmul.f32(n25, n26) |
| n31 | vadd.f32(n27, n27) |
| n32 | vadd.f32(n14, F32(3F800000)) |
| n33 | vmul.f32(n28, n29) |
| n34 | vmul.f32(n28, n32) |
| n35 | vmul.f32(n30, n31) |
| n36 | vsub.f32(n32, n33) |
| n37 | vadd.f32(n35, n36) |
| n38 | vadd.f32(n14, n14) |
| n39 | vsub.f32(n29, n34) |
| n40 | vadd.f32(n29, n34) |
| n41 | vsub.f32(n36, n35) |
| n42 | vadd.f32(n32, n33) |
| n43 | vmul.f32(n14, n37) |
| n44 | vmul.f32(n38, n39) |
| n45 | vmul.f32(n40, F32(C0000000)) |
| n46 | vmul.f32(n14, n41) |
| n47 | vsub.f32(n42, n35) |
| n48 | vadd.f32(n35, n42) |

## Live0 type 2

| Native address | Dependency |
|---|---|
| 00AA5390 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA5394 | vdiv.f32(n1, Rate) |
| 00AA539C | phone.cosf(n2) |
| 00AA53A8 | vmul.f32(Gain, F32(3CCCCCCD)) |
| 00AA53C8 | vmla.f32(F32(4E7E0000), n3, F32(4BD49A78)) |
| 00AA53D4 | vcvt.u32.f32(n4) |
| 00AA53DC | ubfx.raw32(n5, RawU32(00000000), RawU32(00000017)) |
| 00AA53E0 | lsr.raw32(n5, RawU32(00000017)) |
| 00AA53E4 | add.raw32(n6, RawU32(3F800000)) |
| 00AA53E8 | lsl.raw32(n7, RawU32(00000017)) |
| 00AA53F0 | vmla.f32(F32(3CAA70DE), n8, F32(3EA67F46)) |
| 00AA53F4 | vmla.f32(F32(3F272DDB), n8, n9) |
| 00AA53FC | vmul.f32(n10, n11) |
| 00AA53B8 | select(n12, F32(00000000), n13) |
| 00AA5404 | phone.sinf(n2) |
| 00AA5408 | vadd.f32(Q, Q) |
| 00AA5410 | vmul.f32(n14, F32(C0000000)) |
| 00AA5418 | vdiv.f32(n15, n16) |
| 00AA541C | vdiv.f32(n17, n18) |
| 00AA5420 | vmul.f32(n17, n18) |
| 00AA542C | vadd.f32(n19, F32(3F800000)) |
| 00AA5430 | vadd.f32(n20, F32(3F800000)) |
| 00AA5434 | vsub.f32(F32(3F800000), n19) |
| 00AA5438 | vsub.f32(F32(3F800000), n20) |

| Common input | Exact dependency |
|---|---|
| N0 | n21 |
| N1 | n22 |
| N2 | n22 |
| D2 | n23 |
| D1 | n24 |
| Den | n25 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | vmul.f32(Gain, F32(3CCCCCCD)) |
| n4 | vmla.f32(F32(4E7E0000), n3, F32(4BD49A78)) |
| n5 | vcvt.u32.f32(n4) |
| n6 | ubfx.raw32(n5, RawU32(00000000), RawU32(00000017)) |
| n7 | lsr.raw32(n5, RawU32(00000017)) |
| n8 | add.raw32(n6, RawU32(3F800000)) |
| n9 | vmla.f32(F32(3CAA70DE), n8, F32(3EA67F46)) |
| n10 | vmla.f32(F32(3F272DDB), n8, n9) |
| n11 | lsl.raw32(n7, RawU32(00000017)) |
| n12 | nativeMI(n3, F32(C2140000)) |
| n13 | vmul.f32(n10, n11) |
| n14 | phone.cosf(n2) |
| n15 | phone.sinf(n2) |
| n16 | vadd.f32(Q, Q) |
| n17 | vdiv.f32(n15, n16) |
| n18 | select(n12, F32(00000000), n13) |
| n19 | vmul.f32(n17, n18) |
| n20 | vdiv.f32(n17, n18) |
| n21 | vadd.f32(n19, F32(3F800000)) |
| n22 | vmul.f32(n14, F32(C0000000)) |
| n23 | vsub.f32(F32(3F800000), n19) |
| n24 | vsub.f32(F32(3F800000), n20) |
| n25 | vadd.f32(n20, F32(3F800000)) |

## Live0 type 3

| Native address | Dependency |
|---|---|
| 00AA5474 | vmul.f32(Gain, F32(3CCCCCCD)) |
| 00AA5490 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| 00AA549C | vcvt.u32.f32(n2) |
| 00AA54A4 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| 00AA54A8 | lsr.raw32(n3, RawU32(00000017)) |
| 00AA54AC | add.raw32(n4, RawU32(3F800000)) |
| 00AA54B0 | lsl.raw32(n5, RawU32(00000017)) |
| 00AA54B8 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| 00AA54BC | vmla.f32(F32(3F272DDB), n6, n7) |
| 00AA54C4 | vmul.f32(n8, n9) |
| 00AA5480 | select(n10, F32(00000000), n11) |
| 00AA54CC | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA54D0 | vdiv.f32(n12, Rate) |
| 00AA54D8 | phone.sinf(n13) |
| 00AA54E4 | vdiv.f32(F32(3F800000), n14) |
| 00AA54F0 | vadd.f32(n14, n15) |
| 00AA54F4 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| 00AA54F8 | vsqrt.f32(n17) |
| 00AA578C | phone.sqrtf(n17) |
| 00AA5504 | select(n19, n20, n18) |
| 00AA550C | phone.cosf(n13) |
| 00AA5510 | vsqrt.f32(n14) |
| 00AA5520 | vadd.f32(n14, F32(3F800000)) |
| 00AA5528 | vsub.f32(n14, F32(3F800000)) |
| 00AA57AC | phone.sqrtf(n14) |
| 00AA552C | select(n22, n23, n21) |
| 00AA5534 | vmul.f32(n24, F32(3F000000)) |
| 00AA5538 | vmul.f32(n25, n26) |
| 00AA553C | vmul.f32(n27, n28) |
| 00AA5540 | vadd.f32(n29, n29) |
| 00AA5544 | vmul.f32(n27, n30) |
| 00AA5548 | vmul.f32(n31, n32) |
| 00AA5550 | vadd.f32(n30, n33) |
| 00AA5554 | vmul.f32(n14, F32(C0000000)) |
| 00AA5558 | vadd.f32(n28, n34) |
| 00AA555C | vsub.f32(n30, n33) |
| 00AA5560 | vadd.f32(n35, n36) |
| 00AA5564 | vsub.f32(n28, n34) |
| 00AA5568 | vsub.f32(n36, n35) |
| 00AA556C | vadd.f32(n35, n37) |
| 00AA5570 | vmul.f32(n14, n38) |
| 00AA5574 | vmul.f32(n14, n39) |
| 00AA5578 | vsub.f32(n37, n35) |
| 00AA557C | vadd.f32(n40, n40) |
| 00AA5580 | vmul.f32(n41, n42) |

| Common input | Exact dependency |
|---|---|
| N0 | n43 |
| N1 | n44 |
| N2 | n45 |
| D2 | n46 |
| D1 | n47 |
| Den | n48 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(Gain, F32(3CCCCCCD)) |
| n2 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| n3 | vcvt.u32.f32(n2) |
| n4 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| n5 | lsr.raw32(n3, RawU32(00000017)) |
| n6 | add.raw32(n4, RawU32(3F800000)) |
| n7 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| n8 | vmla.f32(F32(3F272DDB), n6, n7) |
| n9 | lsl.raw32(n5, RawU32(00000017)) |
| n10 | nativeMI(n1, F32(C2140000)) |
| n11 | vmul.f32(n8, n9) |
| n12 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n13 | vdiv.f32(n12, Rate) |
| n14 | select(n10, F32(00000000), n11) |
| n15 | vdiv.f32(F32(3F800000), n14) |
| n16 | vadd.f32(n14, n15) |
| n17 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| n18 | vsqrt.f32(n17) |
| n19 | unordered(n18, n18) |
| n20 | phone.sqrtf(n17) |
| n21 | vsqrt.f32(n14) |
| n22 | unordered(n21, n21) |
| n23 | phone.sqrtf(n14) |
| n24 | phone.sinf(n13) |
| n25 | vmul.f32(n24, F32(3F000000)) |
| n26 | select(n19, n20, n18) |
| n27 | phone.cosf(n13) |
| n28 | vsub.f32(n14, F32(3F800000)) |
| n29 | select(n22, n23, n21) |
| n30 | vadd.f32(n14, F32(3F800000)) |
| n31 | vmul.f32(n25, n26) |
| n32 | vadd.f32(n29, n29) |
| n33 | vmul.f32(n27, n28) |
| n34 | vmul.f32(n27, n30) |
| n35 | vmul.f32(n31, n32) |
| n36 | vadd.f32(n30, n33) |
| n37 | vsub.f32(n30, n33) |
| n38 | vadd.f32(n35, n36) |
| n39 | vsub.f32(n36, n35) |
| n40 | vsub.f32(n28, n34) |
| n41 | vmul.f32(n14, F32(C0000000)) |
| n42 | vadd.f32(n28, n34) |
| n43 | vmul.f32(n14, n38) |
| n44 | vmul.f32(n41, n42) |
| n45 | vadd.f32(n40, n40) |
| n46 | vmul.f32(n14, n39) |
| n47 | vsub.f32(n37, n35) |
| n48 | vadd.f32(n35, n37) |

## Live0 type 4

| Native address | Dependency |
|---|---|
| 00AA50F4 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| 00AA50F8 | vdiv.f32(n1, Rate) |
| 00AA5100 | phone.tanf(n2) |
| 00AA5114 | vdiv.f32(F32(3F800000), n3) |
| 00AA5118 | vmul.f32(n4, n4) |
| 00AA511C | vmul.f32(n4, F32(3FB504F3)) |
| 00AA5120 | vadd.f32(n5, F32(3F800000)) |
| 00AA5124 | vadd.f32(n6, n7) |
| 00AA5128 | vdiv.f32(F32(3F800000), n8) |
| 00AA512C | vsub.f32(n7, n6) |
| 00AA5130 | vsub.f32(F32(3F800000), n5) |
| 00AA5134 | vadd.f32(n9, n9) |
| 00AA513C | vmul.f32(n9, n10) |
| 00AA5140 | vmul.f32(n11, n12) |

| Common input | Exact dependency |
|---|---|
| N0 | n9 |
| N1 | n11 |
| N2 | n13 |
| D2 | n9 |
| D1 | n14 |
| Den | F32(3F800000) |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.tanf(n2) |
| n4 | vdiv.f32(F32(3F800000), n3) |
| n5 | vmul.f32(n4, n4) |
| n6 | vmul.f32(n4, F32(3FB504F3)) |
| n7 | vadd.f32(n5, F32(3F800000)) |
| n8 | vadd.f32(n6, n7) |
| n9 | vdiv.f32(F32(3F800000), n8) |
| n10 | vsub.f32(n7, n6) |
| n11 | vadd.f32(n9, n9) |
| n12 | vsub.f32(F32(3F800000), n5) |
| n13 | vmul.f32(n11, n12) |
| n14 | vmul.f32(n9, n10) |

## Live0 type 5

| Native address | Dependency |
|---|---|
| 00AA5018 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| 00AA501C | vdiv.f32(n1, Rate) |
| 00AA5024 | phone.tanf(n2) |
| 00AA503C | vmul.f32(n3, n3) |
| 00AA5040 | vmul.f32(n3, F32(3FB504F3)) |
| 00AA5044 | vadd.f32(n4, F32(3F800000)) |
| 00AA5048 | vadd.f32(n5, n6) |
| 00AA504C | vdiv.f32(F32(3F800000), n7) |
| 00AA5050 | vmul.f32(n8, F32(C0000000)) |
| 00AA5054 | vsub.f32(n4, F32(3F800000)) |
| 00AA5058 | vsub.f32(n6, n5) |
| 00AA5060 | vnmul.f32(n9, n10) |
| 00AA5064 | vmul.f32(n8, n11) |

| Common input | Exact dependency |
|---|---|
| N0 | n8 |
| N1 | n9 |
| N2 | n12 |
| D2 | n8 |
| D1 | n13 |
| Den | F32(3F800000) |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.tanf(n2) |
| n4 | vmul.f32(n3, n3) |
| n5 | vmul.f32(n3, F32(3FB504F3)) |
| n6 | vadd.f32(n4, F32(3F800000)) |
| n7 | vadd.f32(n5, n6) |
| n8 | vdiv.f32(F32(3F800000), n7) |
| n9 | vmul.f32(n8, F32(C0000000)) |
| n10 | vsub.f32(n4, F32(3F800000)) |
| n11 | vsub.f32(n6, n5) |
| n12 | vnmul.f32(n9, n10) |
| n13 | vmul.f32(n8, n11) |

## Live0 type 6

| Native address | Dependency |
|---|---|
| 00AA5248 | vadd.f32(Q, Q) |
| 00AA524C | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA5250 | vdiv.f32(n1, Rate) |
| 00AA5258 | phone.cosf(n2) |
| 00AA5264 | phone.sinf(n2) |
| 00AA5274 | vmul.f32(n3, F32(C0000000)) |
| 00AA527C | vdiv.f32(n4, n5) |
| 00AA5280 | vneg.f32(n6) |
| 00AA5284 | vadd.f32(n6, F32(3F800000)) |
| 00AA5288 | vsub.f32(F32(3F800000), n6) |

| Common input | Exact dependency |
|---|---|
| N0 | n6 |
| N1 | F32(00000000) |
| N2 | n7 |
| D2 | n8 |
| D1 | n9 |
| Den | n10 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.cosf(n2) |
| n4 | phone.sinf(n2) |
| n5 | vadd.f32(Q, Q) |
| n6 | vdiv.f32(n4, n5) |
| n7 | vmul.f32(n3, F32(C0000000)) |
| n8 | vneg.f32(n6) |
| n9 | vsub.f32(F32(3F800000), n6) |
| n10 | vadd.f32(n6, F32(3F800000)) |

## Live0 type 7

| Native address | Dependency |
|---|---|
| 00AA52E0 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA52E4 | vdiv.f32(n1, Rate) |
| 00AA52EC | phone.cosf(n2) |
| 00AA52F8 | phone.sinf(n2) |
| 00AA52FC | vadd.f32(Q, Q) |
| 00AA5304 | vmul.f32(n3, F32(C0000000)) |
| 00AA5310 | vdiv.f32(n4, n5) |
| 00AA531C | vadd.f32(n6, F32(3F800000)) |
| 00AA5324 | vsub.f32(F32(3F800000), n6) |

| Common input | Exact dependency |
|---|---|
| N0 | F32(3F800000) |
| N1 | n7 |
| N2 | n7 |
| D2 | F32(3F800000) |
| D1 | n8 |
| Den | n9 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.cosf(n2) |
| n4 | phone.sinf(n2) |
| n5 | vadd.f32(Q, Q) |
| n6 | vdiv.f32(n4, n5) |
| n7 | vmul.f32(n3, F32(C0000000)) |
| n8 | vsub.f32(F32(3F800000), n6) |
| n9 | vadd.f32(n6, F32(3F800000)) |

## Live1 type 1

| Native address | Dependency |
|---|---|
| 00AA4C7C | vmul.f32(Gain, F32(3CCCCCCD)) |
| 00AA4C98 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| 00AA4CA4 | vcvt.u32.f32(n2) |
| 00AA4CAC | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| 00AA4CB0 | lsr.raw32(n3, RawU32(00000017)) |
| 00AA4CB4 | add.raw32(n4, RawU32(3F800000)) |
| 00AA4CB8 | lsl.raw32(n5, RawU32(00000017)) |
| 00AA4CC0 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| 00AA4CC4 | vmla.f32(F32(3F272DDB), n6, n7) |
| 00AA4CCC | vmul.f32(n8, n9) |
| 00AA4C88 | select(n10, F32(00000000), n11) |
| 00AA4CD4 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA4CD8 | vdiv.f32(n12, Rate) |
| 00AA4CE0 | phone.sinf(n13) |
| 00AA4CEC | vdiv.f32(F32(3F800000), n14) |
| 00AA4CF8 | vadd.f32(n14, n15) |
| 00AA4CFC | vmla.f32(F32(40000000), n16, F32(00000000)) |
| 00AA4D00 | vsqrt.f32(n17) |
| 00AA57CC | phone.sqrtf(n17) |
| 00AA4D0C | select(n19, n20, n18) |
| 00AA4D14 | phone.cosf(n13) |
| 00AA4D18 | vsqrt.f32(n14) |
| 00AA4D28 | vadd.f32(n14, F32(3F800000)) |
| 00AA4D30 | vsub.f32(n14, F32(3F800000)) |
| 00AA57BC | phone.sqrtf(n14) |
| 00AA4D34 | select(n22, n23, n21) |
| 00AA4D3C | vmul.f32(n24, F32(3F000000)) |
| 00AA4D40 | vmul.f32(n25, n26) |
| 00AA4D44 | vmul.f32(n27, n28) |
| 00AA4D48 | vadd.f32(n29, n29) |
| 00AA4D4C | vmul.f32(n27, n30) |
| 00AA4D50 | vmul.f32(n31, n32) |
| 00AA4D54 | vsub.f32(n30, n33) |
| 00AA4D58 | vadd.f32(n14, n14) |
| 00AA4D5C | vadd.f32(n30, n33) |
| 00AA4D60 | vadd.f32(n34, n35) |
| 00AA4D64 | vsub.f32(n35, n34) |
| 00AA4D68 | vsub.f32(n28, n36) |
| 00AA4D6C | vadd.f32(n28, n36) |
| 00AA4D74 | vmul.f32(n14, n37) |
| 00AA4D78 | vmul.f32(n38, n39) |
| 00AA4D7C | vmul.f32(n14, n40) |
| 00AA4D80 | vmul.f32(n41, F32(C0000000)) |
| 00AA4D84 | vadd.f32(n34, n42) |
| 00AA4D88 | vsub.f32(n42, n34) |

| Common input | Exact dependency |
|---|---|
| N0 | n43 |
| N1 | n44 |
| N2 | n45 |
| D2 | n46 |
| D1 | n47 |
| Den | n48 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(Gain, F32(3CCCCCCD)) |
| n2 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| n3 | vcvt.u32.f32(n2) |
| n4 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| n5 | lsr.raw32(n3, RawU32(00000017)) |
| n6 | add.raw32(n4, RawU32(3F800000)) |
| n7 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| n8 | vmla.f32(F32(3F272DDB), n6, n7) |
| n9 | lsl.raw32(n5, RawU32(00000017)) |
| n10 | nativeMI(n1, F32(C2140000)) |
| n11 | vmul.f32(n8, n9) |
| n12 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n13 | vdiv.f32(n12, Rate) |
| n14 | select(n10, F32(00000000), n11) |
| n15 | vdiv.f32(F32(3F800000), n14) |
| n16 | vadd.f32(n14, n15) |
| n17 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| n18 | vsqrt.f32(n17) |
| n19 | unordered(n18, n18) |
| n20 | phone.sqrtf(n17) |
| n21 | vsqrt.f32(n14) |
| n22 | unordered(n21, n21) |
| n23 | phone.sqrtf(n14) |
| n24 | phone.sinf(n13) |
| n25 | vmul.f32(n24, F32(3F000000)) |
| n26 | select(n19, n20, n18) |
| n27 | phone.cosf(n13) |
| n28 | vsub.f32(n14, F32(3F800000)) |
| n29 | select(n22, n23, n21) |
| n30 | vadd.f32(n14, F32(3F800000)) |
| n31 | vmul.f32(n25, n26) |
| n32 | vadd.f32(n29, n29) |
| n33 | vmul.f32(n27, n28) |
| n34 | vmul.f32(n31, n32) |
| n35 | vsub.f32(n30, n33) |
| n36 | vmul.f32(n27, n30) |
| n37 | vadd.f32(n34, n35) |
| n38 | vadd.f32(n14, n14) |
| n39 | vsub.f32(n28, n36) |
| n40 | vsub.f32(n35, n34) |
| n41 | vadd.f32(n28, n36) |
| n42 | vadd.f32(n30, n33) |
| n43 | vmul.f32(n14, n37) |
| n44 | vmul.f32(n38, n39) |
| n45 | vmul.f32(n41, F32(C0000000)) |
| n46 | vmul.f32(n14, n40) |
| n47 | vsub.f32(n42, n34) |
| n48 | vadd.f32(n34, n42) |

## Live1 type 2

| Native address | Dependency |
|---|---|
| 00AA558C | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA5590 | vdiv.f32(n1, Rate) |
| 00AA5598 | phone.cosf(n2) |
| 00AA55A4 | vmul.f32(Gain, F32(3CCCCCCD)) |
| 00AA55C4 | vmla.f32(F32(4E7E0000), n3, F32(4BD49A78)) |
| 00AA55D0 | vcvt.u32.f32(n4) |
| 00AA55D8 | ubfx.raw32(n5, RawU32(00000000), RawU32(00000017)) |
| 00AA55DC | lsr.raw32(n5, RawU32(00000017)) |
| 00AA55E0 | add.raw32(n6, RawU32(3F800000)) |
| 00AA55E4 | lsl.raw32(n7, RawU32(00000017)) |
| 00AA55EC | vmla.f32(F32(3CAA70DE), n8, F32(3EA67F46)) |
| 00AA55F0 | vmla.f32(F32(3F272DDB), n8, n9) |
| 00AA55F8 | vmul.f32(n10, n11) |
| 00AA55B4 | select(n12, F32(00000000), n13) |
| 00AA5600 | phone.sinf(n2) |
| 00AA5604 | vadd.f32(Q, Q) |
| 00AA560C | vmul.f32(n14, F32(C0000000)) |
| 00AA5614 | vdiv.f32(n15, n16) |
| 00AA5618 | vdiv.f32(n17, n18) |
| 00AA561C | vmul.f32(n17, n18) |
| 00AA5628 | vadd.f32(n19, F32(3F800000)) |
| 00AA562C | vsub.f32(F32(3F800000), n19) |
| 00AA5630 | vadd.f32(n20, F32(3F800000)) |
| 00AA5634 | vsub.f32(F32(3F800000), n20) |

| Common input | Exact dependency |
|---|---|
| N0 | n21 |
| N1 | n22 |
| N2 | n22 |
| D2 | n23 |
| D1 | n24 |
| Den | n25 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | vmul.f32(Gain, F32(3CCCCCCD)) |
| n4 | vmla.f32(F32(4E7E0000), n3, F32(4BD49A78)) |
| n5 | vcvt.u32.f32(n4) |
| n6 | ubfx.raw32(n5, RawU32(00000000), RawU32(00000017)) |
| n7 | lsr.raw32(n5, RawU32(00000017)) |
| n8 | add.raw32(n6, RawU32(3F800000)) |
| n9 | vmla.f32(F32(3CAA70DE), n8, F32(3EA67F46)) |
| n10 | vmla.f32(F32(3F272DDB), n8, n9) |
| n11 | lsl.raw32(n7, RawU32(00000017)) |
| n12 | nativeMI(n3, F32(C2140000)) |
| n13 | vmul.f32(n10, n11) |
| n14 | phone.cosf(n2) |
| n15 | phone.sinf(n2) |
| n16 | vadd.f32(Q, Q) |
| n17 | vdiv.f32(n15, n16) |
| n18 | select(n12, F32(00000000), n13) |
| n19 | vmul.f32(n17, n18) |
| n20 | vdiv.f32(n17, n18) |
| n21 | vadd.f32(n19, F32(3F800000)) |
| n22 | vmul.f32(n14, F32(C0000000)) |
| n23 | vsub.f32(F32(3F800000), n19) |
| n24 | vsub.f32(F32(3F800000), n20) |
| n25 | vadd.f32(n20, F32(3F800000)) |

## Live1 type 3

| Native address | Dependency |
|---|---|
| 00AA5644 | vmul.f32(Gain, F32(3CCCCCCD)) |
| 00AA5660 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| 00AA566C | vcvt.u32.f32(n2) |
| 00AA5674 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| 00AA5678 | lsr.raw32(n3, RawU32(00000017)) |
| 00AA567C | add.raw32(n4, RawU32(3F800000)) |
| 00AA5680 | lsl.raw32(n5, RawU32(00000017)) |
| 00AA5688 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| 00AA568C | vmla.f32(F32(3F272DDB), n6, n7) |
| 00AA5694 | vmul.f32(n8, n9) |
| 00AA5650 | select(n10, F32(00000000), n11) |
| 00AA569C | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA56A0 | vdiv.f32(n12, Rate) |
| 00AA56A8 | phone.sinf(n13) |
| 00AA56B4 | vdiv.f32(F32(3F800000), n14) |
| 00AA56C0 | vadd.f32(n14, n15) |
| 00AA56C4 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| 00AA56C8 | vsqrt.f32(n17) |
| 00AA577C | phone.sqrtf(n17) |
| 00AA56D4 | select(n19, n20, n18) |
| 00AA56DC | phone.cosf(n13) |
| 00AA56E0 | vsqrt.f32(n14) |
| 00AA56F0 | vadd.f32(n14, F32(3F800000)) |
| 00AA56F8 | vsub.f32(n14, F32(3F800000)) |
| 00AA579C | phone.sqrtf(n14) |
| 00AA56FC | select(n22, n23, n21) |
| 00AA5704 | vmul.f32(n24, F32(3F000000)) |
| 00AA5708 | vmul.f32(n25, n26) |
| 00AA570C | vmul.f32(n27, n28) |
| 00AA5710 | vadd.f32(n29, n29) |
| 00AA5714 | vmul.f32(n27, n30) |
| 00AA5718 | vmul.f32(n31, n32) |
| 00AA5720 | vadd.f32(n30, n33) |
| 00AA5724 | vmul.f32(n14, F32(C0000000)) |
| 00AA5728 | vsub.f32(n30, n33) |
| 00AA572C | vadd.f32(n34, n35) |
| 00AA5730 | vsub.f32(n35, n34) |
| 00AA5734 | vadd.f32(n28, n36) |
| 00AA5738 | vsub.f32(n28, n36) |
| 00AA573C | vmul.f32(n14, n37) |
| 00AA5740 | vadd.f32(n34, n38) |
| 00AA5744 | vmul.f32(n14, n39) |
| 00AA5748 | vsub.f32(n38, n34) |
| 00AA574C | vadd.f32(n40, n40) |
| 00AA5750 | vmul.f32(n41, n42) |

| Common input | Exact dependency |
|---|---|
| N0 | n43 |
| N1 | n44 |
| N2 | n45 |
| D2 | n46 |
| D1 | n47 |
| Den | n48 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(Gain, F32(3CCCCCCD)) |
| n2 | vmla.f32(F32(4E7E0000), n1, F32(4BD49A78)) |
| n3 | vcvt.u32.f32(n2) |
| n4 | ubfx.raw32(n3, RawU32(00000000), RawU32(00000017)) |
| n5 | lsr.raw32(n3, RawU32(00000017)) |
| n6 | add.raw32(n4, RawU32(3F800000)) |
| n7 | vmla.f32(F32(3CAA70DE), n6, F32(3EA67F46)) |
| n8 | vmla.f32(F32(3F272DDB), n6, n7) |
| n9 | lsl.raw32(n5, RawU32(00000017)) |
| n10 | nativeMI(n1, F32(C2140000)) |
| n11 | vmul.f32(n8, n9) |
| n12 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n13 | vdiv.f32(n12, Rate) |
| n14 | select(n10, F32(00000000), n11) |
| n15 | vdiv.f32(F32(3F800000), n14) |
| n16 | vadd.f32(n14, n15) |
| n17 | vmla.f32(F32(40000000), n16, F32(00000000)) |
| n18 | vsqrt.f32(n17) |
| n19 | unordered(n18, n18) |
| n20 | phone.sqrtf(n17) |
| n21 | vsqrt.f32(n14) |
| n22 | unordered(n21, n21) |
| n23 | phone.sqrtf(n14) |
| n24 | phone.sinf(n13) |
| n25 | vmul.f32(n24, F32(3F000000)) |
| n26 | select(n19, n20, n18) |
| n27 | phone.cosf(n13) |
| n28 | vsub.f32(n14, F32(3F800000)) |
| n29 | select(n22, n23, n21) |
| n30 | vadd.f32(n14, F32(3F800000)) |
| n31 | vmul.f32(n25, n26) |
| n32 | vadd.f32(n29, n29) |
| n33 | vmul.f32(n27, n28) |
| n34 | vmul.f32(n31, n32) |
| n35 | vadd.f32(n30, n33) |
| n36 | vmul.f32(n27, n30) |
| n37 | vadd.f32(n34, n35) |
| n38 | vsub.f32(n30, n33) |
| n39 | vsub.f32(n35, n34) |
| n40 | vsub.f32(n28, n36) |
| n41 | vmul.f32(n14, F32(C0000000)) |
| n42 | vadd.f32(n28, n36) |
| n43 | vmul.f32(n14, n37) |
| n44 | vmul.f32(n41, n42) |
| n45 | vadd.f32(n40, n40) |
| n46 | vmul.f32(n14, n39) |
| n47 | vsub.f32(n38, n34) |
| n48 | vadd.f32(n34, n38) |

## Live1 type 4

| Native address | Dependency |
|---|---|
| 00AA5178 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| 00AA517C | vdiv.f32(n1, Rate) |
| 00AA5184 | phone.tanf(n2) |
| 00AA5198 | vdiv.f32(F32(3F800000), n3) |
| 00AA519C | vmul.f32(n4, n4) |
| 00AA51A0 | vmul.f32(n4, F32(3FB504F3)) |
| 00AA51A4 | vadd.f32(n5, F32(3F800000)) |
| 00AA51A8 | vadd.f32(n6, n7) |
| 00AA51AC | vdiv.f32(F32(3F800000), n8) |
| 00AA51B0 | vsub.f32(n7, n6) |
| 00AA51B4 | vsub.f32(F32(3F800000), n5) |
| 00AA51B8 | vadd.f32(n9, n9) |
| 00AA51C0 | vmul.f32(n9, n10) |
| 00AA51C4 | vmul.f32(n11, n12) |

| Common input | Exact dependency |
|---|---|
| N0 | n9 |
| N1 | n11 |
| N2 | n13 |
| D2 | n9 |
| D1 | n14 |
| Den | F32(3F800000) |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.tanf(n2) |
| n4 | vdiv.f32(F32(3F800000), n3) |
| n5 | vmul.f32(n4, n4) |
| n6 | vmul.f32(n4, F32(3FB504F3)) |
| n7 | vadd.f32(n5, F32(3F800000)) |
| n8 | vadd.f32(n6, n7) |
| n9 | vdiv.f32(F32(3F800000), n8) |
| n10 | vsub.f32(n7, n6) |
| n11 | vadd.f32(n9, n9) |
| n12 | vsub.f32(F32(3F800000), n5) |
| n13 | vmul.f32(n11, n12) |
| n14 | vmul.f32(n9, n10) |

## Live1 type 5

| Native address | Dependency |
|---|---|
| 00AA5070 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| 00AA5074 | vdiv.f32(n1, Rate) |
| 00AA507C | phone.tanf(n2) |
| 00AA5094 | vmul.f32(n3, n3) |
| 00AA5098 | vmul.f32(n3, F32(3FB504F3)) |
| 00AA509C | vadd.f32(n4, F32(3F800000)) |
| 00AA50A0 | vadd.f32(n5, n6) |
| 00AA50A4 | vdiv.f32(F32(3F800000), n7) |
| 00AA50A8 | vmul.f32(n8, F32(C0000000)) |
| 00AA50AC | vsub.f32(n4, F32(3F800000)) |
| 00AA50B0 | vsub.f32(n6, n5) |
| 00AA50B8 | vnmul.f32(n9, n10) |
| 00AA50BC | vmul.f32(n8, n11) |

| Common input | Exact dependency |
|---|---|
| N0 | n8 |
| N1 | n9 |
| N2 | n12 |
| D2 | n8 |
| D1 | n13 |
| Den | F32(3F800000) |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40490FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.tanf(n2) |
| n4 | vmul.f32(n3, n3) |
| n5 | vmul.f32(n3, F32(3FB504F3)) |
| n6 | vadd.f32(n4, F32(3F800000)) |
| n7 | vadd.f32(n5, n6) |
| n8 | vdiv.f32(F32(3F800000), n7) |
| n9 | vmul.f32(n8, F32(C0000000)) |
| n10 | vsub.f32(n4, F32(3F800000)) |
| n11 | vsub.f32(n6, n5) |
| n12 | vnmul.f32(n9, n10) |
| n13 | vmul.f32(n8, n11) |

## Live1 type 6

| Native address | Dependency |
|---|---|
| 00AA5294 | vadd.f32(Q, Q) |
| 00AA5298 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA529C | vdiv.f32(n1, Rate) |
| 00AA52A4 | phone.cosf(n2) |
| 00AA52B0 | phone.sinf(n2) |
| 00AA52C0 | vmul.f32(n3, F32(C0000000)) |
| 00AA52C8 | vdiv.f32(n4, n5) |
| 00AA52CC | vneg.f32(n6) |
| 00AA52D0 | vadd.f32(n6, F32(3F800000)) |
| 00AA52D4 | vsub.f32(F32(3F800000), n6) |

| Common input | Exact dependency |
|---|---|
| N0 | n6 |
| N1 | F32(00000000) |
| N2 | n7 |
| D2 | n8 |
| D1 | n9 |
| Den | n10 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.cosf(n2) |
| n4 | phone.sinf(n2) |
| n5 | vadd.f32(Q, Q) |
| n6 | vdiv.f32(n4, n5) |
| n7 | vmul.f32(n3, F32(C0000000)) |
| n8 | vneg.f32(n6) |
| n9 | vsub.f32(F32(3F800000), n6) |
| n10 | vadd.f32(n6, F32(3F800000)) |

## Live1 type 7

| Native address | Dependency |
|---|---|
| 00AA5330 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| 00AA5334 | vdiv.f32(n1, Rate) |
| 00AA533C | phone.cosf(n2) |
| 00AA534C | phone.sinf(n2) |
| 00AA5350 | vadd.f32(Q, Q) |
| 00AA5358 | vmul.f32(n3, F32(C0000000)) |
| 00AA5364 | vdiv.f32(n4, n5) |
| 00AA536C | vadd.f32(n6, F32(3F800000)) |
| 00AA5374 | vsub.f32(F32(3F800000), n6) |

| Common input | Exact dependency |
|---|---|
| N0 | F32(3F800000) |
| N1 | n7 |
| N2 | n7 |
| D2 | F32(3F800000) |
| D1 | n8 |
| Den | n9 |

| Node | Exact dependency |
|---|---|
| n1 | vmul.f32(CappedFrequency, F32(40C90FDB)) |
| n2 | vdiv.f32(n1, Rate) |
| n3 | phone.cosf(n2) |
| n4 | phone.sinf(n2) |
| n5 | vadd.f32(Q, Q) |
| n6 | vdiv.f32(n4, n5) |
| n7 | vmul.f32(n3, F32(C0000000)) |
| n8 | vsub.f32(F32(3F800000), n6) |
| n9 | vadd.f32(n6, F32(3F800000)) |

Type1: exact symbolic input equality across Init/live0/live1 = True.

Type2: exact symbolic input equality across Init/live0/live1 = True.

Type3: exact symbolic input equality across Init/live0/live1 = True.

Type4: exact symbolic input equality across Init/live0/live1 = True.

Type5: exact symbolic input equality across Init/live0/live1 = True.

Type6: exact symbolic input equality across Init/live0/live1 = True.

Type7: exact symbolic input equality across Init/live0/live1 = True.
