# Recheck of the B-POSE-1 pose rows (Opus extractor, 2026-10-14)

- **Why:** B-POSE-1 was blocked twice by an adopted row that got the unordered (NaN) or boundary branch wrong: R2 (0290e08b)
  and R18 (07b96e18). Before the rerun, every row the job builds from was re-read against libcozmoEngine.so,
  libopencv_calib3d.so and libopencv_core.so.
- **Run:** read-only cozmo-extractor. The manager re-read R36(a) at 0x00845ABC..0x00845ADA and confirms it.
- **Adopted:** by the manager on 2026-10-14.
- **This file overrides** every row it marks CORRECTED, in:
  - `research/20261013-pose3d-arithmetic.md`;
  - `research/20261013-rodrigues-and-pose-tree.md`;
  - `research/20261013-remaining-pose-bodies-check.md` and its Codex report.

  Rows marked CONFIRMED stand as written. Where a row here has added notes, the notes apply too.
- **Branch notation:**
  - **taken on unordered (NaN):** `ble`, `bpl`;
  - **not taken on unordered:** `bgt`, `bmi`, `movmi`, `gt`;
  - **`bhi`'s complement** (`bls`) is taken on unordered.

## research/20261013-pose3d-arithmetic.md

**Confirmed as written:** R1, R3, R4, R6–R10, R16, R20, R22–R27, R29–R31, R34.

**Confirmed, with notes:**

- **R11:** `vcmp.f64` with `ne` gives false, so NaN never equals, and +0 == −0. IsNearlyEqual widens the f32 tolerance
  to f64 (0x0084ACEA). |Δ| ≥ tol, or NaN, gives false (`pl`).
- **R13:** the equality test is `vcmp.f64 ne` (0x0084A6A8): any NaN fails it, and ±0 compare equal. The clamp is
  `gt`/`mi`, so NaN becomes −1. The result passes through Radians(float) (0x0084A738), whose rescale is a no-op on
  [0, π_f].
- **R18 wording:** "a non-finite sqrt result" should read "a NaN vsqrt result". Only a NaN row sum calls sqrtf; +Inf
  gives Inf with no call, and Inf takes the warning branch. The bound constant is 0x3F7FFFEF. The row separator is " "
  (0x008496F0).
- **R24:** the old t is saved first; the result is old t + R·t_o; then R6. Aliasing `o` with `this` is safe.
- **R26:** if P aliases `this`, the rotation step uses the already-updated R_P, and t_P is the just-rotated t, which
  doubles it.
- **R32:** the VERIFYs are:
  - `Pose3d.ToPoseStruct3d.UnknownOrigin` "VERIFY(%s): ID:%d" (originList.ContainsOriginID);
  - `Pose3d.ToPoseStruct3d.BadParent` "VERIFY(%s): ParentID:%d OriginID:%d", checked only when the source pose has a
    parent (0x00846D6C..0x00846DAA).

### R2 (corrected: additions; the existing text stays)

- **Default and copy:**
  - Radians() = (0, flag set) (0x0084C828).
  - The copy ctor and operator=(Radians) copy the value and the flag, with no rescale (0x0084C84C, 0x0084C856).
  - operator=(float) stores, then rescales only if the flag is set (0x0084C846 → 0x0084C884).
- **Edges:**
  - |v| == 10 exactly takes the large branch (`bpl` 0x0084C8BA).
  - A NaN stays NaN: ceilf(NaN) is NaN, `vcvt.s32.f32` turns NaN into 0, and v − 0·2π is NaN.
  - ±Inf stays ±Inf.
  - The int conversion is ARM `vcvt.s32.f32` (0x0084C91E): it rounds toward zero, saturates to INT_MIN/INT_MAX, and
    turns NaN into 0.
  - The large-branch result is not re-checked.
- **The comparison operators:**

  | Operator | Address | Definition |
  | --- | --- | --- |
  | `==` | 0x0084CBDC | IsNear(a, b, Radians(1e-5f = 0x3727C5AC)) |
  | IsNear | 0x0084CC0A..0x0084CC5C | d = rescale(rescale(copy of a) − b's raw value); then \|d\| < \|tol\| strictly (`movmi`). NaN gives false. |
  | `>` | 0x0084CC90 | (a − b) > 0 in f32 (`ble` → false, so NaN is false), and not IsNear(a, b, 1e-5) |
  | `>=` | 0x0084CCDC | (a > b) or IsNear(a, b) |
  | `<` | 0x0084CD12 | b > a |
  | `<=` | 0x0084CD1C | b >= a |

  The veneers 0x008D152C and 0x008D153C resolve to operator> and operator>=.

### R5 (corrected)

**Normalize\<float\>** (0x00849A50..0x00849AF2):
1. n2 = ((w²+x²)+y²)+z² in f32, widened to f64.
2. **If |1 − n2| < 2.107342e-8** (f64, strict; equal or NaN takes the sqrt path, `bpl` 0x00849A8E):
   - k = (float)(2.0/(n2+1.0)), computed in f64 and then narrowed;
   - each component = k·c in f32.
3. **Otherwise:**
   - s = vsqrt.f32(n2); the system sqrtf is called only on a NaN result;
   - each component = c/s in f32.

### R12 (corrected)

- **GetAngle** (0x0084A66C..0x0084A692) = Radians(float)((float)(acos(w) + acos(w))):
  - acos is computed in f64 (libm) and doubled by an add in f64, then narrowed;
  - Radians(float) sets the flag and rescales (R2), so a narrowed angle above π_f (0x40490FDB) wraps negative (w < 0);
  - |w| > 1 gives NaN.
- **GetAxis** (0x0084AABA..0x0084AADE) narrows x, y and z to f32, then runs MakeUnitLength (0x0050E0C0..0x0050E130):
  1. s = ((x²+y²)+z²) in f32.
  2. **If s ≤ 0 or NaN** (`ble` 0x0050E0EC): the axis is unchanged, and it returns 0.
  3. **Otherwise:** n = vsqrt (system sqrtf only on a NaN result); k = 1/n; each component = k·c; it returns n.

### R14 (corrected)

**GetAngleAroundZaxis** (0x0084AA1C..0x0084AAB8):
1. In f64, each narrowed to f32:
   - m01 = (xy − zw) + (xy − zw);
   - m11 = 1 − ((x²+z²) + (x²+z²));
   - m10 = (xy + zw) + (xy + zw);
   - m00 = 1 − ((y²+z²) + (y²+z²)).
2. In f32: A = m01² + m11², B = m10² + m00².
3. **If B > A** (`ble` 0x0084AA94 not taken): atan2f(m10, m00).
   **Otherwise, including a tie and NaN:** atan2f(−m01, m11), where −m01 is a sign-bit flip (+0 becomes −0).
4. The result is Radians(float): the flag is set and R2 rescales, so atan2f's −π_f becomes +π_f. NaN stays NaN.

### R15 (corrected)

Each 2(·) below is computed as a+a, in f64, then narrowed. Both functions return Radians(float) with the R2 rescale.

| Function | Matrix terms | A, B (f32) | If B > A | Otherwise (tie or NaN) |
| --- | --- | --- | --- | --- |
| **X** (0x0084A8E0..0x0084A97C) | m12 = 2(yz − xw); m22 = 1 − 2(y²+x²); m21 = 2(yz + xw); m11 = 1 − 2(z²+x²) | A = m12² + m22²; B = m21² + m11² | atan2f(m21, m11) | atan2f(−m12, m22) (`ble` 0x0084A958) |
| **Y** (0x0084A97E..0x0084AA1A) | m02 = 2(xz + yw); m22 = 1 − 2(x²+y²); m20 = 2(xz − yw); m00 = 1 − 2(z²+y²) | A = m02² + m22²; B = m20² + m00² | atan2f(−m20, m00) | atan2f(m02, m22) (`ble` 0x0084A9F6) |

**GetAngleAroundAxis** (0x0084A888..0x0084A8DE): AxisName+3 indexes the `tbb` table at 0x0084A898 (bytes 04 09 0E 0E 17 1B 1F).

| AxisName | −3 | −2 | −1 | 0 | 1 | 2 | 3 | out of range |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Result | −Z | −Y | −X | −X | X | Y | Z | −X |

Negation is Radians::operator-() (0x0084CB04): negate, keep the flag, and rescale if it's set (−π → +π).

### R17 (corrected)

**GetRotationMatrix** (0x0084AAE0..0x0084ABEC):
1. Form each product in f64 and narrow it: xx, yy, zz, wx, wy, wz, xy, xz, yz.
2. Then, in f32:

   | Entry | Formula |
   | --- | --- |
   | m00 | ((yy + zz)·(−2)) + 1 |
   | m11 | 1 − ((xx + zz) + (xx + zz)) |
   | m22 | 1 − ((xx + yy) + (xx + yy)) |
   | m01 | (xy − wz) + (xy − wz) |
   | m02 | (xz + wy) + (xz + wy) |
   | m10 | (xy + wz) + (xy + wz) |
   | m12 | (yz − wx) + (yz − wx) |
   | m20 | (xz − wy) + (xz − wy) |
   | m21 | (yz + wx) + (yz + wx) |

3. Write the nine values row-major into the result, then run Renormalize (R18) on it.

### R19 (corrected)

**RenormalizeHelper** (0x008499B0..0x00849A10):
1. cv::Rodrigues: M as a CV_32F 3×3 → a zero-initialised CV_32F 3-vector.
2. cv::Rodrigues: that vector → M, written in place.

- Both directions follow `research/20261013-rodrigues-and-pose-tree.md`, with the corrections below.
- The intermediate vector is f32, so the round trip narrows between the two calls.
- A matrix that fails the [−100, 100) check comes back as the identity.
- The old "being read" is stale.

### R21 (corrected)

- **RotationVector3d(v)** (0x0084A378..0x0084A3CC):
  1. angle = Radians() (0, flag set); axis = v.
  2. n = MakeUnitLength(axis) (see R12).
  3. angle = n, through Radians::operator=(float), **which rescales (R2) because the flag is set**:
     - a float-rounded |v| just above π_f becomes ≈ −π;
     - ≈ 2π becomes ≈ 0.
  4. If angle == Radians(0) (IsNear 1e-5), axis = X (1,0,0).
  - So a zero or NaN v gives angle 0 and axis X.
- **RotationVector3d(a, axis)** (0x0084A224..0x0084A2E8):
  1. angle = a copy of `a` (value and flag, no rescale).
  2. n = MakeUnitLength(axis).
  3. **If n == 0** (`vcmp eq`) **and** angle == Radians(0) (IsNear): axis = X, with no warning.
  4. **Else if |n − 1| < 1e-5** (strict, `bmi` 0x0084A2A8): done.
  5. **Otherwise,** including n = 0 with a non-zero angle: the warning `RotationVector3d.Constructor.AxisNotUnitLength`
     "Provided axis was not unit length (norm=%f)." with (double)n. The axis is left as MakeUnitLength left it.

### R28 (corrected)

| Function | Address | Behaviour |
| --- | --- | --- |
| TranslateForward(d) | 0x0084B92A..0x0084B978 | p = R·(d,0,0) (R7/R8); then t = p + t per component, in f32 |
| RotateBy(Rotation3d r) | 0x0084BAD2 | t = r·t (R7); then R = r·R (PreMultiplyBy, R10, normalised) |
| RotateBy(Radians a) | 0x0084BAFA | r = Rotation3d(a, R.GetAxis()), then as RotateBy(r) |
| RotateBy(RotationVector3d) | 0x0084BB3A | r from R22 |
| RotateBy(RotationMatrix3d) | 0x0084BB6A | r from R20 |

**RotateBy(Radians a) uses the axis of this transform's own current rotation** (R12: narrowed, then MakeUnitLength). An
identity rotation gives the zero axis, so r is R3/R4 applied to (cos(a/2), 0, 0, 0).

### R33 (corrected)

**Pose3d(Pose2d)** (0x00846AD0..0x00846C1E):
1. this = Pose3d(Radians of the Pose2d node's angle, Z, (x, y, 0), ""). The result is **parentless**: the Pose2d's parent
   isn't used.
2. Let n be the plane normal (Pose2d +0x18..+0x20) and o the plane origin (+0xC..+0x14).
3. d = ((n.x·0 + n.y·0) + n.z·1) in f32. This equals n.z for finite n.x and n.y, and is NaN if either is ±Inf or NaN.
4. a = Radians(acosf(d)) (flag set, R2).
5. **If |a| < 1e-5** (strict, `bmi` 0x00846BA0): stop.
6. **Otherwise, including a NaN a:**
   - axis = (n.z·0 − n.y, n.x − n.z·0, n.y·0 − n.x·0) in f32. That is (−n.y, n.x, 0) for a finite n; it isn't normalised;
   - P = Pose3d(a, axis, o, "");
   - this.node.T.PreComposeWith(P.T) (R26).

### R35 (corrected)

**GetWithRespectTo, entry checks** (0x00845A34..0x00845A68, 0x00845CB6..0x00845D3A):
1. **If from.node, to.node or out.node is null:**
   - VERIFY `PoseBase.GetWithRespectTo.NullInputPose` "VERIFY(%s): FromNull:%s ToNull:%s WrtOtherNull:%s" (each Y or N);
   - return false.
2. **If &from == &to** (the same Pose3d object; two wrappers of one node don't qualify):
   - warning `PoseBase.GetWithRespectTo.FromEqualsTo` "Pose w.r.t. itself requested.";
   - out = from, through PoseBase::operator= in place: out's node takes from's parent, from's transform, and from's name
     with "_COPY" appended if it isn't empty;
   - SetParent(out, from);
   - return true.

   So out keeps from's own transform under the parent `from`. It isn't the identity.
3. **If HasSameRootAs(from.node, to.node) is false:** return false, with no log.

### R36 (corrected; the manager re-read (a) at 0x00845ABC..0x00845ADA)

**Setup:**
- Tf = a copy of from.node.T; Tt = a copy of to.node.T.
- f = from.node; t = to.node.
- d = depth(f) − depth(t), where depth is 1 + the number of ancestors (0x00845E8C..0x00845F02). At 1000 it logs
  LoopBoundOverflow "%s:%d" (**poseTreeNode.h** line 0x189; corrected 2026-10-14 from the B-POSE-1 block 60295b23, filename pointer 0x00845EB0 → 0x00C294C9) and returns 1001.

**(a) Walk f up** (0x00845AB8..0x00845AE6). While d ≥ 1:
1. Tf.PreComposeWith(f.parent.T).
2. f = f.parent.
3. **Then:** if f.parent == t, out.T = Tf, and go to (e).
4. Otherwise i++ and d−−.
5. At i = 1000: log LoopBoundOverflow (line 0x1A0), set the error flag, and fall into (b).

The exit test runs only **after** a step, on the new f's parent. So when from.parent == to (d = 1), Tt is composed into
Tf without exiting, and the result comes from (d) as Tt⁻¹·(Tt·Tf). **It isn't bit-equal to Tf.**

**(b) Walk t up** (0x00845B42..0x00845B78). i = 0; while d + i ≤ −1:
1. Tt.PreComposeWith(t.parent.T).
2. t = t.parent.
3. If t.parent == f: Tt.Invert() (R27), out.T = Tt, and go to (e).
4. Otherwise i++.
5. At i = 1000: log line 0x1B6 and fall into (c).

**(c) Walk both up** (0x00845BD4..0x00845C16). Loop:
1. If f == t, or HasSameParentAs(t, f), go to (d). HasSameParentAs (0x00844D44): equal nodes → true; either parent null →
   false; otherwise, are the parents equal.
2. Otherwise:
   - Tf.PreComposeWith(f.parent.T);
   - Tt.PreComposeWith(t.parent.T);
   - t = t.parent; f = f.parent.
3. At 1000: log line 0x1D4 and go to (d).

**(d) Combine** (0x00845C72..0x00845CB4): out.node.T = Tt.GetInverse(), then out.node.T *= Tf (R24).

**(e) Finish** (0x00845DFA): PoseBase::SetParent(out, to), with its unowned VERIFY; return true.

### R37 (corrected)

**GetWithRespectToRoot** (0x004DF628..0x004DF6DC):
1. out = Pose3d("").
2. **If this.IsRoot()** (0x00844AA2; also true for a null node): out = this, through PoseBase::operator=.
3. **Otherwise:** root = FindRoot() (it shares the node), then GetWithRespectTo(this, root, out).
4. **A false result** logs the error `Pose3d.GetWithRespectToRoot.Failed` "Could not get pose w.r.t. its own root. This
   should never happen." and sets the error flag. This can't happen in a valid tree.

### R38 (corrected)

**IsSameAs** (0x00846F3C..0x008471A6):

1. **Same object:** if &P == &this, return true, with the outputs unwritten.
2. **Express P in this pose's frame:** P_wrt = Pose3d(""). Then the first case that applies:
   - **both nodes non-null and this.parent node == P.node:** P_wrt.SetParent(P) (the identity);
   - **this has a node and a parent:** GetWithRespectTo(P, this.GetParent(), P_wrt). If that's false, return false
     silently;
   - **otherwise:** GetWithRespectTo(P, this, P_wrt). If that's false:
     - warning `Pose3d.IsSameAs.PosesHaveDifferentOrigins` "Could not get other pose w.r.t. this pose. Returning not
       same.";
     - return false.
3. **The difference:** Δ = P_wrt.t − this.t per component, in f32; outDiff = Δ.
4. **The distance test:**
   - **Spherical,** when |tol.x − tol.y| < 1e-5 and |tol.x − tol.z| < 1e-5 (strict; NaN goes to the box).
     - It fails only if ((Δx² + Δy²) + Δz²) > tol.x².
     - **A NaN sum passes** (`ble` at 0x008470D2 is taken on unordered).
   - **Box:**
     - d = |R_this⁻¹·Δ| per component (R9, R7/R8).
     - It fails if any d_i > tol_i (`bgt` 0x00847126).
     - Equality or NaN passes.
5. **The angle test:**
   1. **If angleTol >= Radians(π_f)** (Radians operator>=, R2): return true, with outAngle unwritten.
   2. **Otherwise:** outAngle = R_this.GetAngleDiffFrom(R_Pwrt) (R13).
   3. **If outAngle <= angleTol** (Radians operator<=, R2, where IsNear 1e-5 counts as equal): return true.
   4. **Otherwise, with an empty ambiguity list:** return false.
   5. **Otherwise:** IsRotationSame(R_this⁻¹·R_Pwrt, angleTol) (R39), with the product from R9 and R6 including
      Normalize.

### R39 (corrected)

**IsRotationSame** (0x0084B4C6..0x0084B51C):
1. **An empty list** returns false, before anything is computed.
2. M = R.GetRotationMatrix() (R17 with R18).
3. **If the +0xC flag is set:** M.Abs() in place (VABS per entry, no Renormalize).
4. **For each A, in list order:**
   - d = M.GetAngleDiffFrom(A) (R40);
   - if d < tol by **Radians operator<**, return true. That means (tol − d) > 0 in f32 **and** not IsNear(tol, d, 1e-5)
     (0x0084CC90). So a d within 1e-5 of tol fails, and a NaN d fails.
5. **If none passes:** return false.

### R40 (corrected)

RotationMatrix3d is f32, row-major.

- **The product helper** (0x004F83F8): P[i][j] = ((0 + A[i][0]·B[0][j]) + A[i][1]·B[1][j]) + A[i][2]·B[2][j], with
  separate multiplies and adds.
- **operator\*** (0x00849456, this·o), **operator\*=** (0x0084949A, this = this·o) and **PreMultiplyBy** (0x00849700,
  this = o·this) each Renormalize the product (R18), so the helper may run.
- **Invert and GetInverse** are a transpose (0x0084975E, 0x0084976A, 0x008497B2), with no Renormalize.
- **GetAngleDiffFrom(A)** (0x0084B0E4):
  1. T = identity (its Renormalize is a no-op);
  2. T = Aᵀ;
  3. T.PreMultiplyBy(this), giving this·Aᵀ renormalised;
  4. then GetAngle.
- **GetAngle** (0x0084B168): acosf((((T00+T11)+T22) + (−1))·0.5) in f32, with no clamp (|arg| > 1 gives NaN), as
  Radians(float).

### R41 (corrected)

**RotationMatrix3d(angle, axis)** (0x0084ADA4..0x0084ADF2) and **RotationMatrix3d(RotationVector3d)**
(0x0084AEF0..0x0084AF34):
1. Zero the matrix, set the identity, and Renormalize (a no-op).
2. rv = RotationVector3d(angle, axis) (R21: MakeUnitLength, the zero/X rule, the warning).
3. **Anki::Rodrigues(rv, M)** (0x0084ADFC..0x0084AEBC):
   - **If rv.angle == Radians(0)** (IsNear 1e-5), **the axis used is Z (0,0,1)**; otherwise rv.axis;
   - r = angle·axis per component in f32, using the raw Radians value;
   - cv::Rodrigues (vector → matrix, CV_32F) writes M directly.
4. **There is no Renormalize afterwards.**

### New rows: read, not previously in any row

Use these if the port needs them.

- **R42, RotationMatrix3d::GetAngleAroundZaxis** (0x0084B424..0x0084B47E):
  - B = M10² + M00² and A = M01² + M11²;
  - **B > A:** atan2f(M10, M00);
  - **otherwise** (tie or NaN): atan2f(−M01, M11).
  - **The X and Y versions** (0x0084B370, 0x0084B3CA) take the second pair on NaN (`ble` 0x0084B3A4/0x0084B3FE).
- **R43, Rotation3d::GetRotationVector** (0x0084ABEE) and **RotationVector3d::operator==** (0x0084A462): read but not
  transcribed. If needed, stop with MISSING and ask for a row; don't derive one.

## research/20261013-rodrigues-and-pose-tree.md

**Confirmed as written:**
- **the wrapper, flags and callers:** a result ≤ 0 sets dst to the scalar 0 (calib3d 0x000112EE..0x0001130E);
- **V→M steps 1–6** (0x0000F2CE..0x0000F478):
  - theta² = (ry² + rx²) + rz², with non-fused `vmla`;
  - NaN takes the general path (`bpl` 0x0000F2FA);
  - `itheta` uses `vcmp eq`, so NaN gives 1/NaN;
- **M→V steps 1–2:** cvCheckArr(flags 3, −100.0, 100.0) (0xC0590000/0x40590000). A failure runs cvSetZero and returns 0;
- **the sort and W:** a strict `vcmp mi`, so NaN doesn't move (0x000598C2);
- **GEMM** (GEMMSingleMul\<double\>, core 0x00071EF0..): for a length of 3, it reduces to ((0 + Σ in k order)·1.0), with
  the C term skipped;
- **step 5:** the clamp is `bgt` then `vcmp mi`, so NaN passes through (0x0000F70E..0x0000F79C);
- **step 7:** the narrowing (`vcvt.f32.f64` at 0x0000FB78..0x0000FB8E);
- **the tree:** the copy ctor and node ctor (0x008458F0, 0x00845FE4, 0x00845F44); SetParent, node SetParent, Wrap,
  owners and the write order.

### Step 3, per pair: γ (corrected)

γ = hypot(p, β), using OpenCV's hypot (core 0x00056EB0..0x00056F16), **on absolute values**. Let A = |p| and B = |β|.
- **If A > B:** A·sqrt(1 + (B/A)²).
- **Else if B > 0:** B·sqrt(1 + (A/B)²).
- **Otherwise:** 0.

The 1 + q² is a `vmla` (a rounded product, then an add). Unordered compares fall through to the next case, so a NaN B
gives 0.

### Step 3, the skip test (corrected: addition)

The pair is skipped if |p| ≤ eps·sqrt(a·b) (`bls` 0x0005972A). NaN does not skip.

### Step 3, the rotation (corrected)

The rotation is at 0x0005979C..0x00059828. **a and b restart from 0.** For each k in order:
1. t0 = (s·Aj[k]) + (c·Ai[k]); write it to Ai[k].
2. t1 = (c·Aj[k]) + ((−s)·Ai[k]); write it to Aj[k].
3. Accumulate a = Σ t0² and b = Σ t1² from 0 with `vmla`.

Then W[i] = a and W[j] = b.
- The Vt rows get the same t0/t1, with no accumulation.
- The β < 0 test is `bpl` 0x0005974C: β ≥ 0, or NaN, takes the c-first branch.

### Step 3, the zero fill (corrected)

The fill is at 0x00059988..0x00059B34. For each i:
1. sd = W[i], or 0 if i ≥ n.
2. **Up to 100 attempts** (sl = 0x64 at 0x000599B2, `bne` 0x00059AF0). Each attempt runs only while sd ≤ minval (`bhi`
   0x000599BE; sd > minval, or NaN, skips it). Each attempt:
   1. **Fill the row with ±1/m:**
      - state = lo·0xF83F630A + hi, seeded 0x12345678:0;
      - the new low word's bit 0x100 set means +.
   2. **Two passes.** In each pass, for each earlier row j:
      - dot from 0 (`vmla`);
      - row −= dot·row_j (`vmls`, not fused), accumulating L1 = Σ|row|;
      - row *= (L1 > eps·100 ? 1/L1 : 0) (`ble` → 0; NaN → 0).
   3. sd = sqrt(Σ row²).
3. **After the loop:** row *= (sd > minval ? 1/sd : 0).

This final scaling is also how the ordinary rows of U are normalised by 1/W[i].

### Step 6, the branches (corrected)

- **s < 1e-5** is strict. A NaN s takes the general branch (`bpl` 0x0000F7B0).
- **Within the general branch, c > 0** is strict (`bgt` 0x0000F7BC). A NaN c takes the 180° branch.
- **In the 180° branch:**
  - max(x, 0) means "x if x ≥ 0 or NaN, otherwise 0" (`ite pl` at 0x0000F7D8, 0x0000F802, 0x0000F846);
  - **"sign(R1)" is (R1 < 0 ? −1 : +1)**, and likewise for R2, so +0, −0 and NaN all give +1 (0x0000F820, 0x0000F868);
  - the negation test is `vcmpe` with `gt` (strict), on R5 and on ry·rz.
- **The general branch:** r *= ((1/(s+s))·theta) (0x0000F94C, 0x0000FB5C).

### Tree: PoseBase::operator= (corrected)

PoseBase::operator= is at 0x00845940..0x008459BE.
- **If &this == &other:** return.
- **If other.node is non-null:** node::operator= runs (0x008459D0..0x00845A2E):
  1. node SetParent(other.parent), with no unowned VERIFY;
  2. copy the quaternion and the translation;
  3. name = other.name, **then append "_COPY" only if that name isn't empty**.

  Two wrappers of the same node assign the node to itself, so the name grows by "_COPY".
- **If other.node is null:**
  1. RemoveOwner.
  2. If the owners reach 0 and unowned parents aren't allowed: VERIFY `PoseBase.AssignmentOperator.NotLastOwner`
     ("_node.use_count() == 1").
  3. The node is reset to null.

## research/20261013-remaining-pose-bodies-check.md and the Codex report

**Confirmed as written:**
- **every bullet in the check file:**
  - NaN picks the second pair in the matrix X/Y angles;
  - Radians operator=(float);
  - Pose2d(Transform2d) memclr4 at 0x008464D2;
  - the FindRoot cap at line 0x145;
- **Codex §1, §3, §4:**
  - the Euler ctor 0x0084AFD8..0x0084B098;
  - IsValid, strict, with NaN failing (0x0084984A);
  - Abs;
  - ApplyTo(vector) 0x0065671C..0x0065682E;
- **Codex §6–7:** 0x008452BA has no null guard.

**Corrected: Codex §2** ("each Radians float assignment wraps its result"; also coverage row 2). In GetEulerAngles
(0x0084B1B8..0x0084B368):
- **Every x/y/z/x2/z2 store** is Radians::operator=(float), **which rescales only if the destination's flag is already
  set**.
- **y2 is the exception:** it comes from operator-(float, Radians), whose result always has the flag set and is rescaled,
  then is copied with its flag.
- **The singular test** is `bpl` 0x0084B200 (strict), so a NaN M20 takes the regular branch.
- **M20 > 0** is `ble` 0x0084B21A, so M20 ≤ 0 gives +π/2.

**Confirmed, with an addition: Codex §5.**
- Pose2d(Pose3d) (0x00846594) is also **parentless**, and uses R14 on the node's rotation.
- Every Transform2d body was re-read: angle updates go through Radians += and *= (flag set, then rescale).

## Manager decisions

1. **The BadRowNorm text (corrected 2026-10-14, from the B-POSE-1 block 74970f31).**
   - **The source:** each row element goes through `std::to_string(float)` (0x008495CE), followed by a space
     (0x008495D8). In the shipped `libc++_shared.so`, that is `to_string` 0x000664F4..0x000665A4: it widens the float
     (0x00066530), then a `%f` vsnprintf **bundled in libc++_shared.so** (0x0006656C → 0x0007E270..0x0007E2B2 →
     0x00082528..0x000825A8 → core 0x000813B0).
   - **It isn't bionic,** and it isn't the stack's LibcxxPrintf (which ports only %g and the integer conversions).
   - **That formatter is unread.** It is the new record named in M11-065.
   - **For B-POSE-1:**
     - build every arithmetic row and the warning itself: key, format, the `%d` row index, the `%f` norm, and the
       helper call;
     - emit the `Row = [%s]` element text as a **visible MISSING placeholder**, naming that record;
     - do **not** substitute .NET formatting.
2. **Three items not re-read first-hand this pass,** taken from the earlier `.scratch/rod` dumps:
   - the cvSVD wrapper's eps = 10·DBL_EPSILON and minval = DBL_MIN;
   - cvConvertScale's rounding;
   - cvCheckArr's [min, max) semantics.

   They stand as adopted. If the builder finds a contradiction, it stops with BLOCKED as before.

## Every LoopBoundOverflow site in the pose tree (manager, 2026-10-14)

Each is sErrorF, key `LoopBoundOverflow`, format "%s:%d" with (file, line), then the error flag and break-on-error.

| Site | Address of the line constant | File | Line |
| --- | --- | --- | --- |
| GetTreeDepth (R36 setup) | 0x00845EB4 | poseTreeNode.h (0x00C294C9) | 0x189 |
| GetWithRespectTo (a) | 0x00845AFC | poseBase_impl.h (0x00C29472) | 0x1A0 |
| GetWithRespectTo (b) | 0x00845B8E | poseBase_impl.h (0x00C29472) | 0x1B6 |
| GetWithRespectTo (c) | 0x00845C2C | poseBase_impl.h (0x00C29472) | 0x1D4 |
| PoseTreeNode::FindRoot | 0x00845334 | poseTreeNode.h | 0x145 |
| PoseTreeNode::HasSameRootAs (OV12 check) | — | poseTreeNode.h | 0x107, 0x10E |

The file string is the full path `../../../../coretech/common/include/anki/common/basestation/math/<file>`.
