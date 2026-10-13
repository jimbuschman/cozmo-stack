# Job B-POSE-1: the exact Pose3d / Rotation3d / Transform3d port

- **Agent:** Codex, as builder. Follow `jobs/CODEX-BUILDER.md` rules 1–10.
- **Status file:** `status/B-POSE-1.md`.
- **Runs after B-M3M4-19.**
- **Checked rows:**
  - `research/20261013-pose3d-arithmetic.md` (R1–R41);
  - `research/20261013-rodrigues-and-pose-tree.md`.

  Expected test values come from those rows. Compute them by hand or with an independent script that follows the
  rows. Never take them from the port.

## Why

Every pose in the original is a **double unit quaternion with a float translation**. Points are rotated in float, and
compositions renormalise. The stack's `Vision/Geometry.cs` uses a double 3×3 matrix (`Mat3`). That makes every
rotation, angle and tolerance decision differ in the last bits. The B-M3M4-16 check found this in the cliff
intersection, the cliff normal and the IsSameAs box test.

## Items

1. **The types** (M11-065):
   - Rotation3d = UnitQuaternion<double> (w,x,y,z), with R3–R6, R9–R16 and R22–R23 exactly;
   - Transform3d = a Rotation3d plus a float translation, with R24–R29;
   - Radians, with R2;
   - Point3f, rotated through R7/R8: narrow to float, all f32, in the given order;
   - **no fused multiply-add:** use separate multiplies and adds, and check that the JIT doesn't fuse them (use
     explicit `float` locals, and no Math.FusedMultiplyAdd).
2. **The matrix side** (M11-065 R17–R18, R39–R41; M11-067):
   - GetRotationMatrix (f64 products narrowed to f32 before the sums), then Renormalize, with its warning;
   - RenormalizeHelper through a port of **cv::Rodrigues**, both directions, as read: double inside; the [−100, 100)
     check → identity; the Jacobi SVD with OpenCV's hypot, the sort and the zero-value fill; the GEMM order; the
     branches; the float narrowing;
   - RotationMatrix3d's multiply, invert, GetAngleDiffFrom and angle-axis constructor.
3. **The pose tree** (M11-066, M11-067):
   - PoseTreeNode (transform, parent, name, ID, owners);
   - Pose3d: the deep-copy constructor (the name + "_COPY"); the **in-place** operator=; SetParent on the shared node,
     with its VERIFYs; WrapExistingNode sharing;
   - GetWithRespectTo with the null/alias checks, the FromEqualsTo warning, the depth walk, Tt⁻¹·Tf, the 1000-step
     LoopBoundOverflow, and the write order (inputs copied first, then out written in place);
   - GetWithRespectToRoot;
   - IsSameAs_WithAmbiguity (R38) and IsRotationSame (R39).
4. **libm:** cosf, sinf, acos, acosf, atan2f, sqrt (where not IEEE-exact) and ceilf use .NET's Math/MathF as
   EQUIVALENT_IMPLEMENTATION (M13-023(e)). The operands, widths and order around each call stay exact. Comment each
   call site with that.
5. **Migrate:** replace `Geometry.cs`'s Mat3/Pose3d internals with the port, keeping the public API where its meaning
   is the same.
   - Where a call site's meaning differs, port the call site:
     - `WithRespectTo` was frame⁻¹·this with no tree;
     - `IsSameAs` had the opposite Δ sign and no spherical branch;
     - `AngleAroundZ` had no magnitude switch.
   - Remove the partial duplicates (CubeGameBehaviors.cs BeaconFloorGeometry, AIWhiteboard.cs EngineRadians32 with its
     Shepperd's conversion) in favour of the port.
   - In the status file, list every call site you changed and why.
6. **Unread pieces** (M11-068): UnitQuaternion<double>·Point3d, the Euler and matrix X/Y constructors, IsValid, Abs,
   ApplyTo(vector), Pose2d/Transform2d, and the GetParent/FindRoot wrappers. Where the stack needs one, give a visible
   MISSING naming M11-068 rather than a guess.
7. **The fixed points and the tests:**
   - unit tests for R3, R4 (both branches), R6, R8, R13 (including the equality and clamp edges), R14 (both branches),
     R17/R18 (a row norm just over 1.0 goes to the helper), Rodrigues round trips including the 180° and NaN cases,
     and GetWithRespectTo over a 3-deep tree;
   - every existing test whose expected value changes: say in the status file which tests, and give the binary row
     that justifies each new value. Never update a test just to make it pass.

## Out of scope

Status changes. Reading M11-068.

**Manifest:** append to test lists and `unresolved`; never replace.

**Report:**
- per item: fixed, or MISSING with the reason;
- the focused tests;
- the full suite;
- `fidelity.py --check`.

Then stop.
