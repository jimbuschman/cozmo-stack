# Pose3d / Rotation3d / Transform3d arithmetic (Opus extractor, 2026-10-13)

- **Run:** read-only cozmo-extractor, after the B-M3M4-16 check (B5).
- **Adopted:** by the manager on 2026-10-13.
- **The finding:**
  - a rotation is a **double unit quaternion (w,x,y,z)**, and a translation is **float**;
  - points are rotated in **float**, after narrowing the quaternion with no renormalisation;
  - every rotation composition renormalises the double quaternion;
  - there is no fused multiply-add anywhere: every step is a separate vmul/vadd.
- **The stack's `Geometry.cs`** uses a double 3×3 matrix (`Mat3`) with double row sums. Its `WithRespectTo` is
  frame⁻¹·this, with no tree. Its `IsSameAs` is a world-frame box, with Δ's sign reversed.

## The rows

| # | What | Address |
|---|---|---|
| R1 | **Storage:** Rotation3d = UnitQuaternion<double> w,x,y,z at +0/+8/+0x10/+0x18. Transform3d = the rotation, then a float translation at +0x20..+0x2B. PoseBase: vptr, then `shared_ptr<PoseTreeNode>` at +4/+8. PoseTreeNode: the transform at +0, the parent at +0x30, the name at +0x38, the id at +0x44, the owner count at +0x48. Radians: a float, plus a rescale flag | 0x0084A656.., 0x0084BAA0.., 0x00845ACC, 0x00845110, 0x00846D88, 0x0084C832 |
| R2 | **Radians rescale** *(corrected 2026-10-13, the B-POSE-1 builder's block, confirmed by the manager in the disassembly)*: only when the flag is set (0x0084C884). **(−π, π] is unchanged**: `vcmpe v, −π (0xC0490FDB); ble` (0x0084C892..0x0084C89A) sends v ≤ −π, **including exactly −π**, to the rescale, and `vcmpe v, π; bls` returns for v ≤ π. With \|v\| < 10: v ≤ −π → add 2π (0x40C90FDB) until v > −π, store (0x0084C8BC..0x0084C8D8), so −π becomes +π (0x40490FDB); v > π → add −2π until v ≤ π (0x0084C8DC..0x0084C8FC). NaN: `ble` is taken (unordered), then `bpl` is taken, so the large branch runs. If \|v\| < 10, add or subtract 2π (0x40C90FDB) until it is in (−π, π]. Otherwise v − (float)(int)ceilf(v/2π − 0.5)·2π. `==` is IsNear 1e-5 | 0x0084C87C..0x0084C936, 0x0084CBDC..0x0084CC5C |
| R3 | **Rotation3d(angle, axis):** half = a·0.5f; cosf, sinf; x,y,z = s·axis in f32; widen to f64; Normalize. The axis isn't normalised first | 0x0084A506..0x0084A596 |
| R4 | **Normalize<double>:** n2 = ((w²+x²)+y²)+z². If \|1−n2\| < 2.107342e-8 (0x3E56A09E19D672B6), scale = 2/(n2+1). Otherwise divide by sqrt(n2). No zero guard; NaN takes the sqrt path | 0x00849DB8..0x00849E52 |
| R5 | **Normalize<float>:** the same in f32; the test and the fast scale are in f64 | 0x00849A50..0x00849AF2 |
| R6 | **Quaternion product:** w = ((w1w2−x1x2)−y1y2)−z1z2; x = ((w2x1+w1x2)+y1z2)−y2z1; y = x2z1+(w2y1+(w1y2−x1z2)); z = w2z1+((x1y2+w1z2)−x2y1). Then Normalize | double 0x00849EF4..0x00849F8E; float 0x00849B70..0x00849C1A |
| R7 | **Rotation3d·Point3f:** narrow each component to f32 with no Normalize, then R8 | 0x0084ACA6..0x0084ACE2 |
| R8 | **UnitQuaternion<float>·point**, all f32 (each "2ab" is ab+ab): m00 = ((ww+xx)−yy)−zz; m11 = ((ww−xx)+yy)−zz; m22 = ((ww−xx)−yy)+zz; m01 = 2xy−2wz; m10 = 2xy+2wz; m02 = 2wy+2xz; m20 = 2xz−2wy; m12 = 2yz−2wx; m21 = 2wx+2yz. x' = m02·pz+(m00·px+m01·py); y' = m12·pz+(m11·py+m10·px); z' = m22·pz+(m20·px+m21·py) | 0x00849C1C..0x00849CF8 |
| R9 | **Invert / GetInverse:** negate x, y, z; no renormalisation | 0x0084AD2C..0x0084AD78 |
| R10 | **`*=`** is q·o (R6). **`*`** copies, then `*=`. **PreMultiplyBy(o):** tmp = o; tmp *= this; this = tmp | 0x0084AC50..0x0084ACA4 |
| R11 | **`==`:** exact on the four doubles. **IsNearlyEqual:** strict \|Δ\| < tol per component; no q/−q equivalence | 0x0084AD7A..0x0084ADA2, 0x0084ACE4..0x0084AD2A |
| R12 | **GetAngle** = (float)(2·acos(w)), as Radians. **GetAxis** = (float)(x,y,z), then MakeUnitLength (≤0 → unchanged; otherwise multiply by 1/sqrt) | 0x0084A66C..0x0084A692, 0x0084AABA.., 0x0050E0C0..0x0050E130 |
| R13 | **GetAngleDiffFrom:** all four equal exactly → 0. Otherwise d = ((w1w2+x1x2)+y1y2)+z1z2; clamp to [−1, 1] (NaN → −1); acos(c·(c+c) + (−1)); narrow to f32 | 0x0084A694..0x0084A746 |
| R14 | **GetAngleAroundZaxis:** in f64, m01 = 2(xy−zw), m11 = 1−2(x²+z²), m10 = 2(xy+zw), m00 = 1−2(y²+z²); narrow to f32. If m10²+m00² > m01²+m11², atan2f(m10, m00); otherwise atan2f(−m01, m11) | 0x0084AA1C..0x0084AAB8 |
| R15 | **GetAngleAroundX/Y** (the same switch), and GetAngleAroundAxis' jump table (0 and out-of-range → −X) | 0x0084A8E0..0x0084AA1A, 0x0084A888..0x0084A8DE |
| R16 | **GetRmatEntry:** f64, then f32. Diagonal: ((a²+b²)·−2)+1; off-diagonal: (p±q) doubled as p+p | 0x0084A748..0x0084A884 |
| R17 | **GetRotationMatrix:** products in f64, each narrowed to f32 before the f32 sums; then Renormalize (R18) | 0x0084AAE0..0x0084ABEC |
| R18 | **Renormalize**, rows 0..2: n = sqrtf((r0²+r1²)+r2²). If \|n−1\| ≥ 0.01: warning `RotationMatrixBase.Renormalize.BadRowNorm` "Norm of row %d = %f! (Expecting near 1.0) Row = [%s]", then the helper. Else if n < 0.99999899 or n > 1.0: the helper, and stop at the first such row. Exactly 1.0, or NaN, passes | 0x008494E0..0x0084969E |
| R19 | **RenormalizeHelper:** cv::Rodrigues to a vector, then back (libopencv_calib3d.so): **being read** | 0x008499B0..0x00849A10 |
| R20 | **Rotation3d(RotationMatrix3d):** cv::Rodrigues → RotationVector3d(Point3f) → R22 | 0x0084A63C..0x0084A654, 0x0084A3F4..0x0084A45C |
| R21 | **RotationVector3d constructors:** (v): angle = \|v\|, axis = v/\|v\|; an angle of 0 (IsNear) → the X axis. (a, axis): MakeUnitLength; a zero norm with a zero angle → X; \|norm−1\| ≥ 1e-5 → warning `RotationVector3d.Constructor.AxisNotUnitLength` "(norm=%f)" | 0x0084A378..0x0084A3CC, 0x0084A224..0x0084A2E8 |
| R22 | **Rotation3d(RotationVector3d):** an angle of 0 (IsNear) → the Z axis; R3 with the **original** angle | 0x0084A598..0x0084A60C |
| R23 | **Rotation3d(UnitQuaternion<double>):** a plain copy | 0x0084A656..0x0084A66A |
| R24 | **Transform3d `*=`:** t = t + R·t_o (R7, then a float add); then R *= R_o | 0x0084B97A..0x0084B9D0 |
| R25 | **Transform3d `*`:** t = t_this + R_this·t_o; R = R_this·R_o | 0x0084B9D4..0x0084BA30 |
| R26 | **PreComposeWith(P):** R = R_P·R_this (PreMultiplyBy); t = t_P + R_P·t_this | 0x0084BA34..0x0084BA78 |
| R27 | **Transform3d Invert:** R = conj(R); t = R⁻¹·(−t) | 0x0084BA7A..0x0084BAD0 |
| R28 | **TranslateForward(d):** t = R·(d,0,0) + t. **RotateBy(r):** t = r·t, then R = r·R (and the angle, vector and matrix variants) | 0x0084B92A..0x0084B978, 0x0084BAD2..0x0084BB98 |
| R29 | **ApplyTo(quad):** R·p (R7), then t + p | 0x0050343C..0x005034B0 |
| R30 | **Pose3d constructors:** (name) gives q = (1,0,0,0); (angle, axis, t) uses R3; (Rotation3d) copies; (RotationVector3d) uses R22; (matrix) uses R20; (Transform3d) copies | 0x0084683C..0x00846AC8 |
| R31 | **Pose3d(PoseStruct3d):** the float q is widened, then Normalize; the parent is GetOriginByID | 0x00846C70..0x00846D16 |
| R32 | **ToPoseStruct3d:** the pose wrt its root; VERIFYs; the quaternion narrowed to f32 | 0x00846D38..0x00846DEE |
| R33 | **Pose3d(Pose2d):** the plane normal gives a = acosf(nz). If \|a\| ≥ 1e-5, PreComposeWith(Pose3d(a, (−ny, nx, 0), o)) | 0x00846AD0..0x00846C1E |
| R34 | **PoseBase `*`** (a new parentless pose, T·T_o); **`*=` / PreComposeWith** on the node; **Invert** (T.Invert, then ClearParent); **GetInverse** | 0x0084505C..0x008451EA |
| R35 | **GetWithRespectTo, the checks:** any null node → VERIFY NullInputPose, false. from == to → warning `PoseBase.GetWithRespectTo.FromEqualsTo` "Pose w.r.t. itself requested.", out = from, SetParent(from), true. Not the same root → false, silently | 0x00845A34..0x00845A68, 0x00845CB6..0x00845D3A |
| R36 | **GetWithRespectTo, the chain.** Tf = from.T and Tt = to.T; d = depth difference. (a) While from is deeper: Tf.PreComposeWith(parent.T), and a parent equal to `to` exits. (b) While `to` is deeper: the same on Tt, and an exit gives Tt⁻¹. (c) Then step both until they share a parent. (d) out = Tt⁻¹·Tf, with SetParent(to). Each loop is capped at 1000 steps with `LoopBoundOverflow` (poseBase_impl.h lines 0x1A0/0x1B6/0x1D4) | 0x00845A6C..0x00845DFE, 0x00845E8C..0x00845F02, 0x00844D44..0x00844D7C |
| R37 | **GetWithRespectToRoot** (inline): out = Pose3d(""); root → out = this; otherwise GetWithRespectTo(this, FindRoot()) | 0x004DF628..0x004DF6DC |
| R38 | **IsSameAs_WithAmbiguity** (as in `research/20261012-cliff-world-helpers-check.md`): Δ = P.t − this.t; spherical when the three thresholds are equal (each \|diff\| < 1e-5); otherwise the box in this pose's frame (R⁻¹·Δ); angle tol ≥ π → true | 0x00846EA4..0x008471A6 |
| R39 | **IsRotationSame:** an empty list → false. M = GetRotationMatrix (R17); Abs if +0xC; any GetAngleDiffFrom(M, A) < tol → true | 0x0084B4C6..0x0084B51C |
| R40 | **RotationMatrix3d:** GetAngleDiffFrom = acosf((((t00+t11)+t22) − 1)·0.5) of this·Aᵀ, renormalised, with no clamp. Multiply: ((0 + a_i0·b_0j) + a_i1·b_1j) + a_i2·b_2j in f32. Invert: transpose | 0x0084B0E4..0x0084B1B4, 0x00849700..0x0084975E |
| R41 | **RotationMatrix3d(angle, axis):** identity, Renormalize, RotationVector3d, then cv::Rodrigues(angle·axis) | 0x0084ADA4..0x0084AF34 |

## Not read

- **cv::Rodrigues**, and the pose-tree copy semantics: `PoseBase::operator=` (0x00845940), `SetParent` (0x00844A14),
  and the PoseTreeNode constructors (0x00845F44, 0x00845FE4). These are being read now.
- **Also unread:**
  - UnitQuaternion<double>·Point3d (0x00849F90..0x0084A062);
  - the Euler and matrix X/Y constructors (0x0084AF98..0x0084B424);
  - IsValid (0x008497D0..0x008498E4);
  - Abs (0x0084B51E);
  - ApplyTo(vector) (0x0065671C);
  - Rejigger (0x00847D7C);
  - the Pose2d/Transform2d family (0x0084B619..0x0084B92A).

## libm

cosf, sinf, acos, acosf, atan2f, sqrt and ceilf are bionic. They are system libraries that don't ship, so under AGENTS.md
("Exact, always") the stack's .NET equivalents are **EQUIVALENT_IMPLEMENTATION**. The operands, the f32/f64 widths and the
operation order around each call stay exact. This answers M13-023(e).
