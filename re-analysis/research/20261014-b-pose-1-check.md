# Opus check of B-POSE-1 (commit e5780c3c, 2026-10-14)

- **Run:** two read-only cozmo-verifiers, against libcozmoEngine.so and the shipped OpenCV:
  - **A:** the arithmetic (Radians, quaternions, matrices, Rodrigues/SVD);
  - **B:** the pose tree, the callers and the camera.
- **Verdict:** FAIL.
  - **The arithmetic** matches the rows and the binary everywhere except two latent Radians operators.
  - **The tree core** matches too.
  - **Blocking:** one logging defect, two caller migrations that contradict their native call sites, and one circular test.
- **Commands:**
  - `fidelity.py --check` passed (606 records);
  - NativePoseBuilderTests passed 14/14;
  - the tree/caller filters passed 42/42.
- **Adopted:** by the manager on 2026-10-14. The fixes are job B-POSE-2.

## Blocking

1. **MISSING lines go into the engine log stream** (verifier B).
   - **The code:** `PoseDiagnostics.Missing` (PoseDiagnostics.cs:15, bound at CozmoEngine.cs:2005) sends every MISSING as a
     Warning "MISSING" into the engine source sink, so they appear as engine log entries for subscribers and the DAS
     display.
   - **How often:** on every Pose3d ctor (through SetParent), Dispose, legacy WithRespectTo/IsSameAs and
     PoseEstimation.Solve, with no dedup.
   - **The native** logs nothing at these points: `Dev_AssertIsValidParentPointer` 0x008460C8 returns on a null parent, on
     the global flag, or when parent+0x48 ≠ 0; destruction logs nothing.
   - **Fix:** route these MISSINGs to the stack's own channel (StackLogLine / ReportMissing, with dedup), as every other
     stack MISSING does. They must never enter the engine log stream.
2. **GetCameraPose** (CameraModel.cs:36).
   - **The native** (0x00510FFC..0x00511064):
     1. copy the head-cam pose (robot+0x2D8, parent the neck);
     2. build `RotationVector3d(Radians(−a), Y_AXIS_3D)` (0x0051101C..0x0051102A, R21);
     3. `Transform3d::RotateBy(RotationVector3d)` on the head-cam transform (0x00511036, R28 → R22). This puts the Z axis
        in when |a| < 1e-5, rotates the translation by R7, and pre-multiplies normalised;
     4. SetName("Camera").
   - **The port** composes Pose3d(−a, Y, Neck), which is R3.
   - **Fix:** follow the native.
   - **The same for the second GetCameraPose path** at VisionSystem.cs:1294, which still uses `Mat3.AboutY`.
3. **The block pre-action poses** (PreActionPose.cs:309-312).
   - **The native** builds `RotationVector3d(Radians, axis)` (0x004E587E, 0x004E58A0, 0x004E58C2, 0x004E58E4), then
     `Transform3d::RotateBy(RotationVector3d)` (0x004E5A1C, 0x004E5AC0, 0x004E5B60, 0x004E5C36). That is R28 → R22
     directly.
   - **The port** goes through Mat3.AxisAngle (Rodrigues V→M), then Rotation3d.FromMatrix (M→V). That is an extra
     Rodrigues round trip with f32 narrowing, and the 180° branch, which the native never runs here.
   - **Fix:** RotateBy(RotationVector3d) directly.
4. **A circular test** (M12RVisBuildTests.cs:693, :1337).
   - **The problem:** the check that the top cube keeps its offset was changed to read `Carrying.TopWrtCarried`. That value
     is computed by the same call as `relBefore` (LiftGeometry.cs:128-131), so the test compares a value with itself.
   - **Fix:** restore an independent check: the top cube's pose relative to the carried cube **after** the update
     (`UpdateCarriedObjectPose`, Docking.cs:288-289) equals the offset before the attach.

## Latent (verifier A): fix now, they are one-liners

5. **NativeRadians.cs:30, `IsNear(Radians a, …)`:** it rescales `a` only when a's flag is set. The native **always**
   rescales a copy of a with the flag forced to 1 (0x0084CC0E..0x0084CC1E). Fix: rescale `a.Value` unconditionally.
6. **NativeRadians.cs:28, `operator -(float a, Radians b)`:**
   - **The native** (0x0084CA66..0x0084CA96) first builds Radians(a) with the flag set and rescales it, then computes
     rescaled(a) − b.raw, sets the flag, and rescales again.
   - **Fix:** `new(new Radians(a).Value - b.Value)`.

## Queued (fix in B-POSE-2 too)

7. **Geometry.cs:267:** the GetParent(root) skip should name **M11-076**, not M11-073.
8. **Faces.cs:202:**
   - **The native** (0x0087E042..0x0087E064) writes the camera-frame translation into the head pose and calls
     SetParent(camera), keeping the head pose's own rotation.
   - **The port** copies the camera rotation and flattens.
   - **Fix:** follow the native, or mark it a visible MISSING with that citation if the tree parenting isn't available
     there.
9. **CubeGameBehaviors.cs:1606:** the doc comment says Renormalize isn't applied, but `Matrix()` now applies R17+R18.
   Correct the comment.
10. **`RequireNode()` (Geometry.cs:341):** it throws on a null node. The native has a defined path (0x00846F7E: `cbz` →
    GetWithRespectTo → NullInputPose VERIFY → PosesHaveDifferentOrigins → false). Port that path.
11. **Tests:**
    - walk (a)'s exit-after-step case (from.parent == to → Tt⁻¹·(Tt·Tf));
    - walks (b) and (c);
    - each LoopBoundOverflow file/line;
    - the NullInputPose VERIFY;
    - the GetWithRespectToRoot failure;
    - a full V→M→V Rodrigues round trip at 180°.

    Expected values come from the rows.
12. **Pose3d is now a reference type,** so passing the same object as from and to takes the FromEqualsTo branch. Check the
    callers that may pass the same object twice (e.g. `History.Latest.RobotPose`). A same-object call is native behaviour
    only where the native passes the same object.

## For the extractor (not the builder)

- **`Dev_AssertIsValidParentPointer`** (0x008460C8..0x00846124, key `PoseBase.Dev_AssertIsValidParentPointer.UnownedParent`)
  and **RemoveOwner's `ZeroOwnersWithID` / `ZeroOwners`** (0x008448B4..0x00844912).
  - The native calls them twice per walk step in GetWithRespectTo, and also in HasSameRootAs, FindRoot, HasSameParentAs,
    IsRoot and GetParent.
  - Whether they ever log on a valid tree is unread. Owner: M11-066.
- **The %f in the BadRowNorm norm** uses .NET "F6", which rounds half away from zero. The native's formatter is
  unread: M11-083 covers to_string; the DAS `%f` path needs the same read.

## Confirmed

**Arithmetic (verifier A):**
- R2 (except 5 and 6);
- R3–R29;
- R33;
- R38–R42;
- IsSameAs;
- Euler/2D;
- Rodrigues/SVD;
- no FMA;
- libm widths;
- no circular tests in NativePoseBuilderTests.

**Tree (verifier B):**
- GetWithRespectTo and its walks;
- GetTreeDepth, HasSameRootAs, HasSameParentAs, FindRoot and GetParent;
- the copy chain and operator=;
- the SetParent VERIFY;
- R37;
- the IsSameAs head;
- the camera initializer (nine words, then Renormalize);
- IsRestingFlat;
- ToStateC;
- AIWhiteboard.IsSameAs;
- M11-054's LOCAL solver, which is labelled;
- the row-driven test changes.

No races. The manifest is append-only.
