DONE B-POSE-2 — 2026-10-14 — built, awaiting strong verification (uncommitted; the manager commits)

Built from the adopted check `research/20261014-b-pose-1-check.md` items 1-12, with `research/20261014-pose-rows-recheck.md`,
`research/20261013-pose3d-arithmetic.md` and `research/20261013-rodrigues-and-pose-tree.md`. No status changed. Manifest
`unresolved`/`test` appended only (M11-003, M11-065, M11-066, M11-067, M11-076, M12-001, M12-008, M12-021, M13-021, M14-001).

## Per item (file:line in the working tree)

| # | Change | Files and lines | Binary ranges followed |
| --- | --- | --- | --- |
| 1 | `PoseDiagnostics.Missing` always goes to `SteppedBehavior.ReportMissing` (stack channel, deduplicated per message); it no longer writes a Warning "MISSING" into the bound engine source sink. VERIFY/LoopBoundOverflow/FromEqualsTo/BadRowNorm etc. stay on the engine sink. | `cozmo-stack/src/Cozmo.Robot/Vision/PoseDiagnostics.cs:14-16` | native logs nothing at 0x008460C8 or destruction |
| 2 | `HeadGeometry.GetCameraPose(float)` / `GetCameraPose(Pose3d headCam, float)`: copy the head-cam pose, `RotationVector3d(new Radians(-a), Y)`, `Pose3d.RotateBy(RotationVector3d)` (R28 -> R22), name "Camera". `CameraPoseInRobot` flattens the identity neck with it. The second path (ComputeHeadAngleToSeePose) calls the same helper instead of `Mat3.AboutY(...).Compose`. | `Vision/CameraModel.cs:33-58`; `Vision/VisionSystem.cs:1270, 1277, 1294` | 0x00510FFC..0x00511064 (sign flip 0x00511016, 0x0051101C..0x0051102A, RotateBy 0x00511036, SetName 0x00511050) |
| 3 | `CubePreActionPoses.RotateBy` builds `RotationVector3d(new Radians((float)angle), axis)` and calls `pose.RotateBy(RotationVector3d)`; no Mat3.AxisAngle / FromMatrix round trip. | `Manipulation/PreActionPose.cs:306-320`; new overloads `Vision/NativePoseArithmetic.cs:153-154` (`Transform3d.RotateBy(RotationVector3d)`), `Vision/Geometry.cs:284-285` (`Pose3d.RotateBy(RotationVector3d)`) | 0x004E587E/0x004E58A0/0x004E58C2/0x004E58E4; 0x004E5A1C/0x004E5AC0/0x004E5B60/0x004E5C36; 0x0084BB3A |
| 4 | The two top-cube tests read `top.Pose.WithRespectTo(bottom.Pose)` after `UpdateCarriedObjectPose` / the pick-up and compare with `relBefore` (measured before the attach), not `Carrying.TopWrtCarried`. | `tests/Cozmo.Protocol.Tests/M12RVisBuildTests.cs:17-19 (OffsetTolMm), 695-699, 1341-1343` | Docking.cs:288-289 path |
| 5 | `Radians.IsNear(Radians a, ...)` rescales `a.Value` unconditionally. | `cozmo-stack/src/Cozmo.Robot/NativeRadians.cs:32-34` | 0x0084CC0E..0x0084CC1E |
| 6 | `operator -(float a, Radians b) => new(new Radians(a).Value - b.Value)`. | `NativeRadians.cs:28-30` | 0x0084CA66..0x0084CA96 |
| 7 | GetParent(root) skip names M11-076; `// fidelity: M11-076` tag on `TryGetParent`. | `Vision/Geometry.cs:263, 268` | 0x008452BA |
| 8 | Visible MISSING (stack channel) in `TrackedFace.UpdateTranslation`; the flattening is unchanged because the face consumers (FaceWorld, FaceActions, FlipBlockAction, FaceBehaviors) read `HeadPose.Translation` as a world pose, so no camera-parented tree is available there. | `Vision/Faces.cs:202-205` | 0x0087E042..0x0087E064 |
| 9 | Doc comment: `Rotation3d.Matrix` applies R17 then Renormalize R18. | `Behavior/CubeGameBehaviors.cs:1606` | 0x0084AAE0, 0x008494E0 |
| 10 | `IsSameAsWithAmbiguity` no longer calls `RequireNode()`; a null node skips the two parent cases and the legacy branch, so `GetWithRespectTo(other, this)` emits the NullInputPose VERIFY and returns false, then the PosesHaveDifferentOrigins warning and false. | `Vision/Geometry.cs:344-350` | 0x00846F7E cbz; R38 |
| 11 | New tests, below. | `tests/Cozmo.Protocol.Tests/NativePoseBuilderTestsBPose2.cs` (new) | rows cited per test |
| 12 | Same-object callers reported below; none changed. | — | — |

## Tests (expected values from the rows, worked by hand in each test's summary)

`NativePoseBuilderTestsBPose2` (12 tests, collection "M5 process statics"):
- `M11_066_WalkA_FromParentEqualsToGoesThroughCombineNotAnEarlyExit`: R36 (a) exit-after-step; result = Tt^-1*(Tt*Tf) by the
  row's sequence of R26/R27/R24 on copies, asserted not bit-equal to Tf; parent = to.
- `M11_066_WalkB_ExitInvertsTheWalkedToTransform`, `M11_066_WalkC_BothStepUntilTheyShareAParent`: R36 (b) and (c).
- `M11_066_TreeLoopBoundOverflowsLogTheirFileAndLine`: poseTreeNode.h 325 (0x145), 263 (0x107), 270 (0x10E), 393 (0x189), Error level, full path, error flag.
- `M11_066_NullInputPoseVerifiesAndReturnsFalse`: key, Error, "FromNull:Y ToNull:N WrtOtherNull:N" suffix, error flag, one ForceFlush, false.
- `M11_066_IsSameAsOnANullNodeTakesTheDefinedFalsePath`: item 10; VERIFY then the PosesHaveDifferentOrigins warning text, in that order.
- `M11_066_GetWithRespectToRootFailureLogsTheError`: 2-cycle; Error `Pose3d.GetWithRespectToRoot.Failed` with the row text, error flag, fresh identity "" pose.
- `M11_067_RodriguesRoundTripAtAHalfTurnTakesThe180Branch`: V->M entries (R5 0x33BBBD2E, R7 0xB3BBBD2E, diagonal 1/-1/-1), then M->V x = 0x40490FDA and y = z in (1.3e-7, 1.5e-7).
- `M11_065_IsNearRescalesAUnflaggedLeftOperand`: a = 1e6 (flag off), b = 0.3: |d| = 0.675 (native) vs 0.6875 (old); tol 0.68 true, 0.67 false.
- `M11_065_FloatMinusRadiansRescalesTheFloatFirst`: 1e6 - Radians(0.3) = (-0.375f - 0.3f) bits, flag set.
- `M11_003_GetCameraPoseNearZeroUsesTheZAxisSubstitution`: a = 5e-6: x = z = 17.52f, y = (2*(1*-2.5e-6f))*17.52f (a Y-axis rotation would give 0), name "Camera", no AxisNotUnitLength; through `CameraPoseInRobot`, the f32 sums with the neck.
- `PoseMissingNeverEntersTheEngineSourceLog`: no "MISSING" key/text in the source sink; each pose MISSING arrives once on `SteppedBehavior.MissingReported`.

M12RVisBuildTests `M12_008_ACubeOnTopIsRecordedDirtyAndFollowsTheCarriedOne` and `M12_008_TheCubeOnTopFollowsAtOnceWhenThePickupResultArrives` (item 4).

## Results

- `python -I re-analysis/tools/fidelity.py`: regenerated FIDELITY_GAPS.md; 606 records, statuses unchanged.
- `python -I re-analysis/tools/fidelity.py --check`: exit 0.
- Focused: `dotnet test tests/Cozmo.Protocol.Tests --filter "FullyQualifiedName~NativePoseBuilderTests|FullyQualifiedName~M12RVisBuildTests|FullyQualifiedName~M11RVis|FullyQualifiedName~RestingFlat|FullyQualifiedName~M13X1BuilderTests|FullyQualifiedName~Camera|FullyQualifiedName~PreAction"`: 316/316 passed.
- Full: `dotnet test tests/Cozmo.Protocol.Tests`: 4722/4722 passed, 0 skipped (4 min 34 s).

## MISSING

1. **M12-001/M12-021, the sb Radians bits.** The table 0x010590AC (built 0x004E5868..0x004E58E4) holds 0/pi/2/pi/-pi/2, but no
   row gives the bit patterns or how each Radians is built. The stack passes (float)(sb*pi/2) through Radians(float), which
   for sb = 3 is (float)(3pi/2) - 2pi_f. By hand that is 0xBFC90FDC, one ulp from a float -pi/2 (0xBFC90FDB). The code
   reports this through ReportMissing in `CubePreActionPoses.RotateBy`. It is needed at PreActionPose.cs:317.
2. **M11-003, the neck pose's own rotation.** The neck is Robot::Robot robot+0x2CC, built at 0x0050FF36..0x0050FF40. Only
   its translation is in the rows. `CameraPoseInRobot` keeps the identity rotation the stack already used, which the old
   neck(-a, Y) composition implied, and reports a MISSING. It is needed at CameraModel.cs:55-57.
3. **M14-001/M11-066, item 8.** The camera-parented head pose is not built, because the face consumers read flattened world
   translations. The code reports it with a visible MISSING at Faces.cs:202-205.
4. **M11-066, item 11: the GetWithRespectTo walk caps (a) 0x1A0, (b) 0x1B6 and (c) 0x1D4 have no test.**
   - **Why no test:** with HasSameRootAs's 1000-step walk and GetTreeDepth's 1001 cap as built, no tree passes the
     same-root gate and then needs 1000 walk steps.
     - (a) needs d >= 1000, so `to` is a root. `from` then reaches it within 1000 steps, so the exit fires by step 999.
     - (b) is the mirror case.
     - (c): HasSameRootAs ending both walks on one node X at step 1000 means f_999 and t_999 share X as parent, so (c)
       exits by iteration 1000.
   - **Not checked:** I did not open the binary to confirm that the native bounds make these unreachable too.
   - **If one of (a) or (b) did overflow:** (c) would read a null parent, a native fault for which no M1-034 skip is built.
   - **Needed:** an extractor or verifier judgement on reachability.

## Choices made (for the manager)

- **Item 4 tolerance.** `OffsetTolMm = 8/16384` mm is 8 f32 ulps at translations under 512 mm. It is a test bound, not a
  source value. The measured difference is 1.8e-7 mm, and with a tolerance of 0 both tests fail, which shows they run.
- **Item 8.** The check allowed implementing it or marking it MISSING. I marked it MISSING, because implementing it
  would change every HeadPose consumer, outside the item.
- **Item 2.**
  - The negation is a float sign flip, then `new Radians(float)` (flag set, rescale).
  - `CameraPoseInRobot` casts its double argument to float first.
  - Legacy-flattened flags are kept by building the head-cam and neck through the existing `Pose3d(Mat3, Vec3)`
    constructor, so downstream WithRespectTo behaviour is unchanged.
  - I removed "GetCameraPose's pre-multiply" from the ReportMissing text and the doc in ComputeHeadAngleToSeePose
    (VisionSystem.cs:1270, 1277). After this change it is no longer the stack's composition.
- **Item 10.** The legacy flattened branch, which is stack-only, is skipped for a null node, so the native null path runs.
- **Expected values in item 11's tree-walk tests** use the Transform3d primitives (R24/R26/R27, confirmed by verifier A)
  in the row's order. The unit under test is the walk order, not the arithmetic.

## Item 12: callers that may pass one Pose3d object as both operands (reported, not changed; no native citation for these callers' object identity)

- `Vision/MemoryMap.cs:152-153`: `_lastRobotPose = robotPose` stores the reference, and the next `last.IsSameAs(robotPose)`
  takes the same-object `true` if the caller passes the same object again. If that object were mutated in place, the
  stored "last" would follow it, which a native member copy would not.
- `Manipulation/DockActions.cs:652, 702`: `_originalPose = target.Pose` (a reference), later `target.Pose.IsSameAs(_originalPose, ...)`.
  If the object's Pose3d instance is not replaced between them, this takes the same-object `true`. The native IDockAction
  presumably holds a copy (unread).
- `Vision/BlockWorld.cs:1472`: `remaining.Where(c => c.World.IsSameAs(seed.World, ...))` includes `seed` itself (same object, true).
- `Manipulation/PreActionPose.cs:777` (RemoveMatchingPredockPose) and `Manipulation/DriveActions.cs:716` (IsSamePredockPose):
  they take the same object if `match` is an element of the list.
- `Vision/BlockWorld.cs:2979`: `pb.IsSameAs(pa)` if `ComputedRobotPoseAt` returns the same instance for both timestamps.

## Follow-up (2026-10-14, after the verifier's read)

1. **Blocking, fixed: the sb table.** `cozmo-stack/src/Cozmo.Robot/Manipulation/PreActionPose.cs:312-322` adds
   `SbRadiansBits` and `SbRotations` and `RotateBy(Pose3d, int sb)`.
   - The table is the four `RotationVector3d(Radians(BitConverter.Int32BitsToSingle(word)), Y)` entries from the guarded
     static at 0x004E5868..0x004E58E4. The words are sb0 0x00000000 (0x004E586A), sb1 0x3FC90FDB (0x004E5882/0x004E5888),
     sb2 0x40490FDB (0x004E58A4/0x004E58AA) and sb3 0xBFC90FDB (0x004E58C6/0x004E58CC).
   - The caller at line 223 now passes `sb`.
   - The sb-bits ReportMissing and the old `(axis, angle)` overload are removed, so MISSING 1 above is withdrawn.
   - **Test:** `NativePoseBuilderTestsBPose2.M12_001_SbTableUsesTheEngineRadiansWordsAndRotatesThroughRotationVector3d` (line 212).
     It checks the four words, that sb3 is 0xBFC90FDB, the unit Y axis, and that the quaternion for sb 1..3 is bit-equal to
     R3 then R4 on cosf/sinf(word·0.5f), followed by R6 with the identity and R4. That is the direct RotationVector3d path,
     with no Rodrigues round trip.
2. **Fixed: the neck citation** (`Vision/CameraModel.cs:53-55`). The neck ReportMissing is removed, and a comment cites the
   neck's construction: Pose3d(Radians(0.0f) [0x0050FF26..0x0050FF28], Y_AXIS_3D [0x0050FF2C], (-13 = 0xC1500000, 0,
   49 = 0x42440000), parent robot, name) at 0x0050FF6A, which R30/R3 makes the identity. MISSING 2 above is withdrawn.
3. **Fixed: the OffsetTolMm comment** (`tests/Cozmo.Protocol.Tests/M12RVisBuildTests.cs:17-18`). Below 512 mm one f32 ulp
   is at most 2^-15 mm. The bound 2^-11 mm is unchanged.
4. **Manifest:** `unresolved` is appended for M12-001, M12-021 and M11-003, and `test` for M12-001 and M12-021.

**Results:**
- `fidelity.py`: regenerated.
- `fidelity.py --check`: exit 0.
- Focused filter: 317/317.
- Full suite: 4723/4723 passed, 0 skipped (4 min 55 s).

**Still open:** MISSING 3 (item 8, Faces) and MISSING 4 (the reachability of the walk caps 0x1A0/0x1B6/0x1D4).
