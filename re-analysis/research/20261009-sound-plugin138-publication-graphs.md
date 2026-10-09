# Plug-in138 persistent publication dependency graphs

Primary: setup-design and live-design native companions. Engine SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Symbolic transcription, not production implementation, numeric execution or an equivalence test. All offsets hex.

Entry numerators N0,N1,N2,D2,D1 and denominator Den are raw F32 results of the separately retained type graph. No algebraic reassociation. Operation records preserve old destination in VMLA. Constants are raw bits; moves preserve raw identity. Stack LDM reads four words before following stack overwrites. Region0 denotes P+A0/P150. Four words at region-10..-4 repeat normalized N0/Den, not input gain.

This graph establishes common publication only. It does not assert the input numerators from the seven Init/live type graphs match.

## Init

| Native address | Result / destination | Dependency |
|---|---|---|
| 00AA3C8C | v00AA3C8C | vdiv.f32(N1, Den) |
| 00AA3C90 | v00AA3C90 | vdiv.f32(N2, Den) |
| 00AA3C94 | region+74 | publish(n1) |
| 00AA3C98 | v00AA3C98 | vdiv.f32(N0, Den) |
| 00AA3C9C | v00AA3C9C | vdiv.f32(D2, Den) |
| 00AA3CA4 | region+70 | publish(n2) |
| 00AA3CA8 | region-10 | publish(n2) |
| 00AA3CA8 | region-C | publish(n2) |
| 00AA3CAC | region-8 | publish(n2) |
| 00AA3CAC | region-4 | publish(n2) |
| 00AA3CB0 | v00AA3CB0 | vdiv.f32(D1, Den) |
| 00AA3CB4 | region+78 | publish(n3) |
| 00AA3CB8 | v00AA3CB8 | vneg.f32(n4) |
| 00AA3CC0 | region+7C | publish(n5) |
| 00AA3CC4 | v00AA3CC4 | vmla.f32(n1, n2, n5) |
| 00AA3CD0 | v00AA3CD0 | vmla.f32(n3, n5, n6) |
| 00AA3CE0 | region+0 | publish(F32(00000000)) |
| 00AA3CE4 | region+4 | publish(F32(00000000)) |
| 00AA3CE8 | region+8 | publish(F32(00000000)) |
| 00AA3CEC | region+C | publish(n6) |
| 00AA3CF4 | v00AA3CF4 | vmla.f32(n3, n1, n5) |
| 00AA3CF8 | v00AA3CF8 | vneg.f32(n7) |
| 00AA3CFC | v00AA3CFC | vmul.f32(n1, n8) |
| 00AA3D00 | region+80 | publish(n8) |
| 00AA3D04 | v00AA3D04 | vmul.f32(n5, n8) |
| 00AA3D08 | v00AA3D08 | vmla.f32(n9, n2, n8) |
| 00AA3D14 | v00AA3D14 | vmla.f32(n10, n5, n11) |
| 00AA3D28 | region+10 | publish(F32(00000000)) |
| 00AA3D2C | region+14 | publish(F32(00000000)) |
| 00AA3D30 | region+18 | publish(n6) |
| 00AA3D34 | region+1C | publish(n12) |
| 00AA3D38 | v00AA3D38 | vmul.f32(n2, n13) |
| 00AA3D3C | v00AA3D3C | vmul.f32(n3, n8) |
| 00AA3D40 | v00AA3D40 | vadd.f32(n3, n3) |
| 00AA3D44 | v00AA3D44 | vmla.f32(n14, n5, n12) |
| 00AA3D50 | v00AA3D50 | vmla.f32(n15, n5, n16) |
| 00AA3D54 | v00AA3D54 | vmul.f32(n3, n5) |
| 00AA3D58 | v00AA3D58 | vmul.f32(n8, n17) |
| 00AA3D5C | v00AA3D5C | vmul.f32(n8, F32(40400000)) |
| 00AA3D60 | v00AA3D60 | vmul.f32(n5, n18) |
| 00AA3D68 | v00AA3D68 | vmul.f32(n5, n19) |
| 00AA3D6C | v00AA3D6C | vmul.f32(n5, n5) |
| 00AA3D70 | v00AA3D70 | vmul.f32(n5, n20) |
| 00AA3D74 | v00AA3D74 | vadd.f32(n8, n8) |
| 00AA3D78 | v00AA3D78 | vmla.f32(n21, n5, n10) |
| 00AA3D7C | v00AA3D7C | vmla.f32(n22, n5, n23) |
| 00AA3D80 | v00AA3D80 | vmul.f32(n8, n24) |
| 00AA3D84 | v00AA3D84 | vmul.f32(n5, n25) |
| 00AA3D88 | v00AA3D88 | vmul.f32(n5, n26) |
| 00AA3D8C | v00AA3D8C | vadd.f32(n27, n10) |
| 00AA3D90 | v00AA3D90 | vmul.f32(n5, n28) |
| 00AA3DA8 | region+20 | publish(F32(00000000)) |
| 00AA3DAC | region+24 | publish(n6) |
| 00AA3DB0 | v00AA3DB0 | vmul.f32(n5, n13) |
| 00AA3DB4 | region+28 | publish(n12) |
| 00AA3DBC | region+2C | publish(n29) |
| 00AA3DCC | v00AA3DCC | vmla.f32(n30, n5, n31) |
| 00AA3DD0 | region+30 | publish(n1) |
| 00AA3DDC | region+34 | publish(n11) |
| 00AA3DE0 | region+38 | publish(n16) |
| 00AA3DE8 | region+3C | publish(n32) |
| 00AA3DEC | v00AA3DEC | vadd.f32(n15, n23) |
| 00AA3DF0 | v00AA3DF0 | vmul.f32(n8, n8) |
| 00AA3E00 | region+40 | publish(n3) |
| 00AA3E04 | region+44 | publish(n18) |
| 00AA3E08 | v00AA3E08 | vmla.f32(n31, n5, n24) |
| 00AA3E0C | region+48 | publish(n33) |
| 00AA3E10 | region+4C | publish(n34) |
| 00AA3E14 | v00AA3E14 | vmla.f32(n35, n5, n36) |
| 00AA3E1C | v00AA3E1C | vsub.f32(n25, n7) |
| 00AA3E20 | v00AA3E20 | vadd.f32(n37, n38) |
| 00AA3E28 | v00AA3E28 | vadd.f32(n38, n36) |
| 00AA3E3C | region+50 | publish(n5) |
| 00AA3E40 | region+54 | publish(n39) |
| 00AA3E48 | region+58 | publish(n40) |
| 00AA3E4C | region+5C | publish(n41) |
| 00AA3E58 | region+60 | publish(n8) |
| 00AA3E5C | region+64 | publish(n13) |
| 00AA3E60 | region+68 | publish(n42) |
| 00AA3E64 | region+6C | publish(n43) |

| Node | Exact dependency |
|---|---|
| n1 | vdiv.f32(N1, Den) |
| n2 | vdiv.f32(N0, Den) |
| n3 | vdiv.f32(D2, Den) |
| n4 | vdiv.f32(N2, Den) |
| n5 | vneg.f32(n4) |
| n6 | vmla.f32(n1, n2, n5) |
| n7 | vdiv.f32(D1, Den) |
| n8 | vneg.f32(n7) |
| n9 | vmla.f32(n3, n5, n6) |
| n10 | vmul.f32(n1, n8) |
| n11 | vmla.f32(n3, n1, n5) |
| n12 | vmla.f32(n9, n2, n8) |
| n13 | vmul.f32(n5, n8) |
| n14 | vmul.f32(n2, n13) |
| n15 | vmul.f32(n3, n8) |
| n16 | vmla.f32(n10, n5, n11) |
| n17 | vadd.f32(n3, n3) |
| n18 | vmul.f32(n3, n5) |
| n19 | vmul.f32(n8, n17) |
| n20 | vmul.f32(n8, F32(40400000)) |
| n21 | vmla.f32(n15, n5, n16) |
| n22 | vmul.f32(n5, n19) |
| n23 | vmul.f32(n5, n18) |
| n24 | vadd.f32(n8, n8) |
| n25 | vmul.f32(n5, n5) |
| n26 | vmul.f32(n5, n20) |
| n27 | vmla.f32(n14, n5, n12) |
| n28 | vmul.f32(n8, n24) |
| n29 | vadd.f32(n27, n10) |
| n30 | vmul.f32(n5, n26) |
| n31 | vmul.f32(n5, n25) |
| n32 | vmla.f32(n21, n5, n10) |
| n33 | vadd.f32(n15, n23) |
| n34 | vmla.f32(n22, n5, n23) |
| n35 | vmul.f32(n5, n28) |
| n36 | vmul.f32(n5, n13) |
| n37 | vmla.f32(n30, n5, n31) |
| n38 | vmul.f32(n8, n8) |
| n39 | vsub.f32(n25, n7) |
| n40 | vmla.f32(n31, n5, n24) |
| n41 | vadd.f32(n37, n38) |
| n42 | vadd.f32(n38, n36) |
| n43 | vmla.f32(n35, n5, n36) |

## Live group0

| Native address | Result / destination | Dependency |
|---|---|---|
| 00AA49E0 | v00AA49E0 | vdiv.f32(N1, Den) |
| 00AA49E4 | v00AA49E4 | vdiv.f32(N2, Den) |
| 00AA49E8 | region+74 | publish(n1) |
| 00AA49EC | v00AA49EC | vdiv.f32(N0, Den) |
| 00AA49F0 | v00AA49F0 | vdiv.f32(D2, Den) |
| 00AA49F8 | region+70 | publish(n2) |
| 00AA49FC | region-10 | publish(n2) |
| 00AA49FC | region-C | publish(n2) |
| 00AA4A00 | region-8 | publish(n2) |
| 00AA4A00 | region-4 | publish(n2) |
| 00AA4A04 | v00AA4A04 | vneg.f32(n3) |
| 00AA4A08 | region+78 | publish(n4) |
| 00AA4A10 | region+7C | publish(n5) |
| 00AA4A14 | v00AA4A14 | vdiv.f32(D1, Den) |
| 00AA4A18 | v00AA4A18 | vmla.f32(n1, n2, n5) |
| 00AA4A24 | v00AA4A24 | vmla.f32(n4, n5, n6) |
| 00AA4A34 | region+0 | publish(F32(00000000)) |
| 00AA4A40 | v00AA4A40 | vmla.f32(n4, n1, n5) |
| 00AA4A44 | v00AA4A44 | vneg.f32(n7) |
| 00AA4A48 | v00AA4A48 | vmul.f32(n1, n8) |
| 00AA4A4C | region+80 | publish(n8) |
| 00AA4A50 | region+4 | publish(F32(00000000)) |
| 00AA4A54 | region+8 | publish(F32(00000000)) |
| 00AA4A58 | region+C | publish(n6) |
| 00AA4A5C | v00AA4A5C | vmla.f32(n9, n2, n8) |
| 00AA4A68 | v00AA4A68 | vmla.f32(n10, n5, n11) |
| 00AA4A7C | region+10 | publish(F32(00000000)) |
| 00AA4A84 | region+14 | publish(F32(00000000)) |
| 00AA4A8C | region+18 | publish(n6) |
| 00AA4A90 | region+1C | publish(n12) |
| 00AA4A98 | v00AA4A98 | vmul.f32(n5, n8) |
| 00AA4A9C | v00AA4A9C | vmul.f32(n4, n8) |
| 00AA4AA0 | v00AA4AA0 | vmul.f32(n8, F32(40400000)) |
| 00AA4AA4 | v00AA4AA4 | vadd.f32(n4, n4) |
| 00AA4AA8 | v00AA4AA8 | vmul.f32(n2, n13) |
| 00AA4AB0 | v00AA4AB0 | vmla.f32(n14, n5, n15) |
| 00AA4AB4 | v00AA4AB4 | vmul.f32(n5, n5) |
| 00AA4AB8 | v00AA4AB8 | vmul.f32(n5, n16) |
| 00AA4ABC | v00AA4ABC | vmul.f32(n4, n5) |
| 00AA4AC0 | v00AA4AC0 | vadd.f32(n8, n8) |
| 00AA4AC4 | v00AA4AC4 | vmul.f32(n8, n17) |
| 00AA4AC8 | v00AA4AC8 | vmla.f32(n18, n5, n12) |
| 00AA4ACC | v00AA4ACC | vmul.f32(n8, n19) |
| 00AA4AD0 | v00AA4AD0 | vmul.f32(n5, n20) |
| 00AA4AD4 | v00AA4AD4 | vmul.f32(n5, n21) |
| 00AA4AD8 | v00AA4AD8 | vmul.f32(n5, n22) |
| 00AA4ADC | v00AA4ADC | vmul.f32(n5, n23) |
| 00AA4AE0 | v00AA4AE0 | vmla.f32(n24, n5, n10) |
| 00AA4AE4 | v00AA4AE4 | vmul.f32(n5, n13) |
| 00AA4AE8 | v00AA4AE8 | vmul.f32(n5, n25) |
| 00AA4AEC | v00AA4AEC | vmla.f32(n26, n5, n27) |
| 00AA4AF0 | v00AA4AF0 | vadd.f32(n28, n10) |
| 00AA4AF4 | v00AA4AF4 | vmla.f32(n29, n5, n30) |
| 00AA4B08 | region+20 | publish(F32(00000000)) |
| 00AA4B0C | region+24 | publish(n6) |
| 00AA4B14 | region+28 | publish(n12) |
| 00AA4B18 | region+2C | publish(n31) |
| 00AA4B24 | v00AA4B24 | vmul.f32(n8, n8) |
| 00AA4B30 | region+30 | publish(n1) |
| 00AA4B34 | region+34 | publish(n11) |
| 00AA4B3C | region+38 | publish(n15) |
| 00AA4B40 | region+3C | publish(n32) |
| 00AA4B44 | v00AA4B44 | vmla.f32(n27, n5, n19) |
| 00AA4B48 | v00AA4B48 | vadd.f32(n14, n30) |
| 00AA4B4C | v00AA4B4C | vmla.f32(n33, n5, n34) |
| 00AA4B5C | region+40 | publish(n4) |
| 00AA4B60 | region+44 | publish(n22) |
| 00AA4B64 | region+48 | publish(n35) |
| 00AA4B68 | region+4C | publish(n36) |
| 00AA4B70 | v00AA4B70 | vadd.f32(n37, n38) |
| 00AA4B74 | v00AA4B74 | vsub.f32(n20, n7) |
| 00AA4B78 | v00AA4B78 | vadd.f32(n38, n34) |
| 00AA4B90 | region+50 | publish(n5) |
| 00AA4B94 | region+54 | publish(n39) |
| 00AA4B9C | region+58 | publish(n40) |
| 00AA4BA0 | region+5C | publish(n41) |
| 00AA4BAC | region+6C | publish(n42) |
| 00AA4BB4 | region+60 | publish(n8) |
| 00AA4BB8 | region+64 | publish(n13) |
| 00AA4BBC | region+68 | publish(n43) |

| Node | Exact dependency |
|---|---|
| n1 | vdiv.f32(N1, Den) |
| n2 | vdiv.f32(N0, Den) |
| n3 | vdiv.f32(N2, Den) |
| n4 | vdiv.f32(D2, Den) |
| n5 | vneg.f32(n3) |
| n6 | vmla.f32(n1, n2, n5) |
| n7 | vdiv.f32(D1, Den) |
| n8 | vneg.f32(n7) |
| n9 | vmla.f32(n4, n5, n6) |
| n10 | vmul.f32(n1, n8) |
| n11 | vmla.f32(n4, n1, n5) |
| n12 | vmla.f32(n9, n2, n8) |
| n13 | vmul.f32(n5, n8) |
| n14 | vmul.f32(n4, n8) |
| n15 | vmla.f32(n10, n5, n11) |
| n16 | vmul.f32(n8, F32(40400000)) |
| n17 | vadd.f32(n4, n4) |
| n18 | vmul.f32(n2, n13) |
| n19 | vadd.f32(n8, n8) |
| n20 | vmul.f32(n5, n5) |
| n21 | vmul.f32(n5, n16) |
| n22 | vmul.f32(n4, n5) |
| n23 | vmul.f32(n8, n17) |
| n24 | vmla.f32(n14, n5, n15) |
| n25 | vmul.f32(n8, n19) |
| n26 | vmul.f32(n5, n21) |
| n27 | vmul.f32(n5, n20) |
| n28 | vmla.f32(n18, n5, n12) |
| n29 | vmul.f32(n5, n23) |
| n30 | vmul.f32(n5, n22) |
| n31 | vadd.f32(n28, n10) |
| n32 | vmla.f32(n24, n5, n10) |
| n33 | vmul.f32(n5, n25) |
| n34 | vmul.f32(n5, n13) |
| n35 | vadd.f32(n14, n30) |
| n36 | vmla.f32(n29, n5, n30) |
| n37 | vmla.f32(n26, n5, n27) |
| n38 | vmul.f32(n8, n8) |
| n39 | vsub.f32(n20, n7) |
| n40 | vmla.f32(n27, n5, n19) |
| n41 | vadd.f32(n37, n38) |
| n42 | vmla.f32(n33, n5, n34) |
| n43 | vadd.f32(n38, n34) |

## Live group1

| Native address | Result / destination | Dependency |
|---|---|---|
| 00AA4DB4 | v00AA4DB4 | vdiv.f32(N1, Den) |
| 00AA4DB8 | v00AA4DB8 | vdiv.f32(N2, Den) |
| 00AA4DBC | region+74 | publish(n1) |
| 00AA4DC0 | v00AA4DC0 | vdiv.f32(N0, Den) |
| 00AA4DC4 | v00AA4DC4 | vdiv.f32(D2, Den) |
| 00AA4DCC | region+70 | publish(n2) |
| 00AA4DD0 | region-10 | publish(n2) |
| 00AA4DD0 | region-C | publish(n2) |
| 00AA4DD4 | region-8 | publish(n2) |
| 00AA4DD4 | region-4 | publish(n2) |
| 00AA4DD8 | v00AA4DD8 | vneg.f32(n3) |
| 00AA4DDC | region+78 | publish(n4) |
| 00AA4DE4 | region+7C | publish(n5) |
| 00AA4DE8 | v00AA4DE8 | vdiv.f32(D1, Den) |
| 00AA4DEC | v00AA4DEC | vmla.f32(n1, n2, n5) |
| 00AA4DF8 | v00AA4DF8 | vmla.f32(n4, n5, n6) |
| 00AA4E08 | region+0 | publish(F32(00000000)) |
| 00AA4E14 | v00AA4E14 | vmla.f32(n4, n1, n5) |
| 00AA4E18 | v00AA4E18 | vneg.f32(n7) |
| 00AA4E1C | v00AA4E1C | vmul.f32(n1, n8) |
| 00AA4E20 | region+80 | publish(n8) |
| 00AA4E24 | region+4 | publish(F32(00000000)) |
| 00AA4E28 | region+8 | publish(F32(00000000)) |
| 00AA4E2C | region+C | publish(n6) |
| 00AA4E30 | v00AA4E30 | vmla.f32(n9, n2, n8) |
| 00AA4E3C | v00AA4E3C | vmla.f32(n10, n5, n11) |
| 00AA4E50 | region+10 | publish(F32(00000000)) |
| 00AA4E58 | region+14 | publish(F32(00000000)) |
| 00AA4E60 | region+18 | publish(n6) |
| 00AA4E64 | region+1C | publish(n12) |
| 00AA4E6C | v00AA4E6C | vmul.f32(n5, n8) |
| 00AA4E70 | v00AA4E70 | vmul.f32(n4, n8) |
| 00AA4E74 | v00AA4E74 | vmul.f32(n8, F32(40400000)) |
| 00AA4E78 | v00AA4E78 | vadd.f32(n4, n4) |
| 00AA4E7C | v00AA4E7C | vmul.f32(n2, n13) |
| 00AA4E84 | v00AA4E84 | vmla.f32(n14, n5, n15) |
| 00AA4E88 | v00AA4E88 | vmul.f32(n5, n5) |
| 00AA4E8C | v00AA4E8C | vmul.f32(n5, n16) |
| 00AA4E90 | v00AA4E90 | vmul.f32(n4, n5) |
| 00AA4E94 | v00AA4E94 | vadd.f32(n8, n8) |
| 00AA4E98 | v00AA4E98 | vmul.f32(n8, n17) |
| 00AA4E9C | v00AA4E9C | vmla.f32(n18, n5, n12) |
| 00AA4EA0 | v00AA4EA0 | vmul.f32(n8, n19) |
| 00AA4EA4 | v00AA4EA4 | vmul.f32(n5, n20) |
| 00AA4EA8 | v00AA4EA8 | vmul.f32(n5, n21) |
| 00AA4EAC | v00AA4EAC | vmul.f32(n5, n22) |
| 00AA4EB0 | v00AA4EB0 | vmul.f32(n5, n23) |
| 00AA4EB4 | v00AA4EB4 | vmla.f32(n24, n5, n10) |
| 00AA4EB8 | v00AA4EB8 | vmul.f32(n5, n13) |
| 00AA4EBC | v00AA4EBC | vmul.f32(n5, n25) |
| 00AA4EC0 | v00AA4EC0 | vmla.f32(n26, n5, n27) |
| 00AA4EC4 | v00AA4EC4 | vadd.f32(n28, n10) |
| 00AA4EC8 | v00AA4EC8 | vmla.f32(n29, n5, n30) |
| 00AA4EDC | region+20 | publish(F32(00000000)) |
| 00AA4EE0 | region+24 | publish(n6) |
| 00AA4EE8 | region+28 | publish(n12) |
| 00AA4EEC | region+2C | publish(n31) |
| 00AA4EF8 | v00AA4EF8 | vmul.f32(n8, n8) |
| 00AA4F04 | region+30 | publish(n1) |
| 00AA4F08 | region+34 | publish(n11) |
| 00AA4F10 | region+38 | publish(n15) |
| 00AA4F14 | region+3C | publish(n32) |
| 00AA4F18 | v00AA4F18 | vmla.f32(n27, n5, n19) |
| 00AA4F1C | v00AA4F1C | vadd.f32(n14, n30) |
| 00AA4F20 | v00AA4F20 | vmla.f32(n33, n5, n34) |
| 00AA4F30 | region+40 | publish(n4) |
| 00AA4F34 | region+44 | publish(n22) |
| 00AA4F38 | region+48 | publish(n35) |
| 00AA4F3C | region+4C | publish(n36) |
| 00AA4F44 | v00AA4F44 | vadd.f32(n37, n38) |
| 00AA4F48 | v00AA4F48 | vsub.f32(n20, n7) |
| 00AA4F4C | v00AA4F4C | vadd.f32(n38, n34) |
| 00AA4F64 | region+50 | publish(n5) |
| 00AA4F68 | region+54 | publish(n39) |
| 00AA4F70 | region+58 | publish(n40) |
| 00AA4F74 | region+5C | publish(n41) |
| 00AA4F80 | region+6C | publish(n42) |
| 00AA4F84 | region+60 | publish(n8) |
| 00AA4F88 | region+64 | publish(n13) |
| 00AA4F8C | region+68 | publish(n43) |

| Node | Exact dependency |
|---|---|
| n1 | vdiv.f32(N1, Den) |
| n2 | vdiv.f32(N0, Den) |
| n3 | vdiv.f32(N2, Den) |
| n4 | vdiv.f32(D2, Den) |
| n5 | vneg.f32(n3) |
| n6 | vmla.f32(n1, n2, n5) |
| n7 | vdiv.f32(D1, Den) |
| n8 | vneg.f32(n7) |
| n9 | vmla.f32(n4, n5, n6) |
| n10 | vmul.f32(n1, n8) |
| n11 | vmla.f32(n4, n1, n5) |
| n12 | vmla.f32(n9, n2, n8) |
| n13 | vmul.f32(n5, n8) |
| n14 | vmul.f32(n4, n8) |
| n15 | vmla.f32(n10, n5, n11) |
| n16 | vmul.f32(n8, F32(40400000)) |
| n17 | vadd.f32(n4, n4) |
| n18 | vmul.f32(n2, n13) |
| n19 | vadd.f32(n8, n8) |
| n20 | vmul.f32(n5, n5) |
| n21 | vmul.f32(n5, n16) |
| n22 | vmul.f32(n4, n5) |
| n23 | vmul.f32(n8, n17) |
| n24 | vmla.f32(n14, n5, n15) |
| n25 | vmul.f32(n8, n19) |
| n26 | vmul.f32(n5, n21) |
| n27 | vmul.f32(n5, n20) |
| n28 | vmla.f32(n18, n5, n12) |
| n29 | vmul.f32(n5, n23) |
| n30 | vmul.f32(n5, n22) |
| n31 | vadd.f32(n28, n10) |
| n32 | vmla.f32(n24, n5, n10) |
| n33 | vmul.f32(n5, n25) |
| n34 | vmul.f32(n5, n13) |
| n35 | vadd.f32(n14, n30) |
| n36 | vmla.f32(n29, n5, n30) |
| n37 | vmla.f32(n26, n5, n27) |
| n38 | vmul.f32(n8, n8) |
| n39 | vsub.f32(n20, n7) |
| n40 | vmla.f32(n27, n5, n19) |
| n41 | vadd.f32(n37, n38) |
| n42 | vmla.f32(n33, n5, n34) |
| n43 | vadd.f32(n38, n34) |

Validated 37 word publications per path, all 37 exact symbolic destination dependencies identical across Init/live0/live1. Publication schedules are listed separately and need not be reordered. Seven input type graphs remain separate retained work.
