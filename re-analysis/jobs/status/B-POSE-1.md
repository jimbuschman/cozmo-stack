DONE B-POSE-1 — 2026-10-14 — built, awaiting strong verification

Claim pushed: e7f5d05c. Implementation pushed: e5780c3ce7a00e2c65f2605996e62516195335cd (normal gate: fidelity PASS, 4710/4710 tests PASS, 0 skipped). No fidelity record is settled.

Checked authority: research/20261014-pose-rows-recheck.md overrides the adopted 20261013-pose3d-arithmetic.md R1–R41, rodrigues-and-pose-tree.md, remaining-pose-bodies-check.md and its report. OV12 HasSameRootAs is adopted for M11-066. Required call targets were reopened in the shipped ARMv7 engine/OpenCV libraries, including camera initializer 0x0084B0AC. No own unchecked extraction was used to invent a behavior.

## Per-record implementation / binary ranges

| Record | Changed production files (line inventory below) | Checked binary ranges / result |
| --- | --- | --- |
| M11-065 | NativeRadians.cs, Vision/NativePoseArithmetic.cs, Geometry.cs, PoseDiagnostics.cs; migrated callers below | R2 0x0084C828..0x0084CD1C; R3/R4/R5/R6 0x0084A506..A596, 0x00849DB8..9F8E, 0x00849A50..9C1A; R7/R8 0x0084ACA6..ACE2, 0x00849C1C..9CF8; R9–R17 0x0084A66C..ABEC / 0x0084AC50..ADA2; R18 0x008494E0..4969E; R21 0x0084A224..A3CC; R24–R30 0x0084B92A..BB98, 0x0084683C..46AC8; R33 0x00846AD0..46C1E; R34 0x0084505C..451EA; R38/R39 0x00846EA4..471A6, 0x0084B4C6..B51C; R40/R42 0x00849456..497B2, 0x0084B0E4..B1B4 / B424..B47E. Built arithmetic, widths, association, NaN/strict boundaries, raw quaternion equality, alias write order. |
| M11-066 | Geometry.cs, PoseDiagnostics.cs | 0x008448B4, 0x008449EA..44A7A; 0x00844BE4..44CE0 HasSameRootAs; 0x0084520C..4541E; 0x008458F0..45A2E; corrected R35/R36 0x00845A34..45E04 / 0x00845E8C..45F02 / 0x00844D44..44D7C; R37 0x004DF628..DF6DC. Built valid-node copying, in-place writes, owners++, parent sharing, walk order and all checked walk logs. Ownership diagnostics/lifetime remain MISSING below. |
| M11-067 | NativeRodrigues.cs, Geometry.cs, NativePoseArithmetic.cs, PoseDiagnostics.cs | Engine 0x008499B0..49A10 / 0x0084A3F4..A45C / 0x0084ADFC..AEBC; calib3d 0x000111F8..1133E / 0x0000F28E..F478 / 0x0000F696..FB9A; core 0x000595C0..59B56 / 0x00056EB0..56F16 / GEMM 0x00071EF0 onward. CV_32F double SVD, range check, sort, zero fill, GEMM order, 180-degree/sign/NaN and intermediate f32 narrowing built. |
| M11-068 | NativeRadians.cs, NativePose2d.cs, Geometry.cs, NativePoseArithmetic.cs | 0x0084AF98..B422 Euler/X/Y; 0x0084B51E..B55C Abs; 0x0065671C..5682E ApplyTo; 0x0084A19C..A1EE and 0x0084B618..B928 2d; 0x008463D8..4660C Pose2d; 0x0084520C..4541E GetParent/FindRoot/Wrap; 0x0084C846/84C884 assignment; 0x00849900..49936 and 0x0084B0AC..B0E4 initializer. Built assigned flags/alternate outputs, count/alias branch, summed-angle *= discrepancy, plane zero-normal constructor, valid-node sharing. |
| M11-083 | Geometry.cs, PoseDiagnostics.cs; NativePoseBuilderTests | Engine 0x008495CE / 495D8; libc++ to_string 0x000664F4..665A4 → 0x0007E270 → 0x00082528 → 0x000813B0. Only manager-authorized visible MISSING row-element placeholder; bundled %f not ported. |
| M11-003 | CameraModel.cs, Faces.cs / shared pose implementation | Camera static ctor 0x004D6DA4..D6DB6 → initializer 0x0084B0AC; R17–R20/R23/R30. Preserves exact q copying when changing only translation; camera initializer repair built. |
| M11-054 | PoseEstimation.cs, CameraModel.cs | Existing unresolved native camera translation 0x0050FF7C..FFBA/solvePnP boundary remains open. Private LOCAL double solver preserves pre-job candidate arithmetic; no engine solver port claimed. Output only crosses into native q/f32 pose; visible MISSING. |
| M10-001 | OffTreads.cs, NativeRadians.cs | R2 0x0084C87C..C936 / CC0A..CD1C; duplicate Radians removed in favor of shared flag-aware port. |

## Call sites migrated and purpose

- CozmoEngine.TickAt and NativeLatticePlannerRuntime.DoPlanning: bind explicit SourceLogSink and engine DAS flush for the shared geometry calls.
- CubeGameBehaviors.BeaconFloorGeometry: quaternion operations delegate to the shared core; directionality uses quaternion yaw R14; flat candidate pose factory copies raw q instead of converting through a matrix.
- AIWhiteboard.IsSameAs / EngineRadians32: remove opposite-sign/no-sphere and Shepperd duplicates; shared R38/R20. FailureInfo insertion deep-copies native pose as the source emplace copy does.
- AIWhiteboardHandlers: raw RotatedParentZTiltMatrix helper separates a matrix predicate fixture from a Pose3d matrix constructor (R20 repairs invalid matrices).
- PreActionPose.RotateBy: R28 float point rotation then normalized q product; retained rotation constructors copy q (R23), not matrix round trips; yaw sites use R14 directly. EngineRadians matrix overload follows R42.
- BlockConfigurations, ChargerActions, DockActions, LatticePlanner: direct quaternion yaw; raised poses copy q rather than reconstruct it from R17 matrix.
- UnexpectedMovement, Faces: translation-only changes retain q exactly (R23/R30).
- CameraModel: direct angle-axis q constructors for neck/robot; static nine-word matrix initializer now Renormalizes before the R20 pose constructor.
- BlockWorld.IsRestingFlat: 0x0087751C..77562 GetRotationMatrix, row-Z dominant absolute term, acosf without clamp, Radians operator< at 0x0087755C; strict near-boundary result matches R2.
- PoseEstimation: existing LOCAL solver is isolated so its finite-difference epsilon does not invoke the native R21 near-zero axis substitution during numerical iterations. It remains visibly MISSING; old double candidate preserved, native pose conversion at output only.

## Every remaining MISSING / limits

1. M11-083 row element std::to_string(float) shipped %f conversion is unread. Key, row index, norm diagnostic and helper are built; Row=[MISSING: M11-083 ...] follows manager decision. No .NET row-element formatter.
2. M11-067 Dev_AssertIsValidParentPointer at 0x008460C8..46124 has no fully transcribed checked diagnostic body; every node SetParent reports it visibly. Native parent shared_ptr control-block release/ownership semantics remain unbuilt.
3. M11-067 RemoveOwner/ZeroOwners at 0x008448B4 and assignment-null NotLastOwner VERIFY at 0x0084594A..459B8 have untranscribed diagnostic text/native use_count; explicit wrapper owner count is not substituted for native shared_ptr use_count. AddOwner wrap increments are built; malformed count/control-block behavior remains open.
4. M11-077 native destruction/lifetime side effects remain assigned to the later owning job; Dispose visibly reports missing and only releases the C# wrapper owner count. Native allocation failure/string unwind/control-block teardown are not source-equivalence claims.
5. M11-066 flattened WithRespectTo and IsSameAs legacy caller inputs lack actual native root/parent bindings. Existing flat reductions remain candidates with visible MISSING; valid-node GetWithRespectTo uses the checked tree path. Unrelated native roots fail; source false-return handling for old flattened callers is not claimed recovered.
6. M11-073 GetParent(root) would fault at 0x008452BA: smallest operation skipped under M1-034 with visible owning-record MISSING, prior output preserved. Undefined null-node property/FindRoot operations remain guarded by the outer handler exception path; no invented native null result is claimed.
7. M11-065 PoseStruct3d ctor R31 origin-list binding (0x00846C70..46D16), ToPoseStruct3d R32 origin/parent VERIFY serialization (0x00846D38..46DEE) have no migrated complete stack path in this job. No settlement claim; SDK-only/unbuilt callers remain gaps.
8. M11-068 wider Pose2d shared-node tree/lifetime is not built; only checked transform/constructor fields needed here. Quaternion<double>*Point3d and IsValid had no engine caller in checked report and are not claimed implemented by generic carrier APIs. Plain Mat3 constructors remain raw helper/CV carriers; native initializer semantics use explicit FromInitializerRows.
9. M11-054 native solvePnP and known camera-z mismatch remain open. The LOCAL double solver is preserved and visibly MISSING, not classified exact/equivalent. No hardware acceptance run.
10. Native heap allocation/string/control-block teardown and OpenCV allocation/error handling outside the fixed 3x3/3-vector CV_32F, no-jacobian engine uses are not ported; unsupported shapes/types/jacobians are not reachable through this port's typed entry. Bundled cvSVD W tail is unused by these Rodrigues callers; no observable W caller claim.

## Calls / logs / side effects reviewed

All math call sites are represented by the shared operations above. Unshipped cosf/sinf/acos/acosf/atan2f/asinf/ceilf/sqrt fallback use Math/MathF only under M13-023(e); width/order stays checked. Rotation ctor/GetAxis call MakeUnitLength; q products call Normalize; matrix ctor/RenormalizeHelper call Rodrigues both ways. 2d product retains even the unused summed-angle trig calls. Vector slow append corresponds to List.Add in the different-count branch; equal count overwrites locally computed point.

Logs are explicit keyed source calls: BadRowNorm warning; AxisNotUnitLength warning; FromEqualsTo warning; PosesHaveDifferentOrigins warning; GetWithRespectToRoot.Failed error; NullInputPose and UnownedParent VERIFY error/flag/ForceFlush; LoopBoundOverflow error/flag/debug gate at Tree.h decimal lines 263/270/325/393 and Base_impl.h 416/438/468. No level-prefixed engine text. Unbound source sink/flush is visible MISSING. There are no file/stream opens/closes in the checked pose/math ranges. Static flag UnownedParentsAllowed remains source default false. Wrapped node owners increment, deep-copy id=0/name _COPY, assignment preserves existing id/owners. Unchecked diagnostic/lifetime callees listed above stay MISSING rather than inferred.

CV Mat/view/create/convert/zero/identity/transposition are represented by fixed typed arrays and the wrapper's checked range/zero branches; cvSVD/Jacobi/hypot and cvGEMM have their checked arithmetic counterparts. Allocation/delete/unwind calls remain native-lifetime MISSING, not engine numerical behavior. Log vector allocation/string building behind warning calls likewise does not silently claim shipped %f conversion.

## Regression expectations changed from checked source

- NativePoseBuilderTests: 14 independent row/word tests covering q D/F fast/sqrt paths, zero/NaN, non-unit axis, cancellation association, exact equality/clamp, yaw branch/tie -0, row norm repair/NaN warning placeholder, Rodrigues exact half-turn/NaN, deep copy/in-place node alias, 3-deep WRT, same-object vs same-node, sphere/box and different roots, 2d and 3d alias order, Euler flags/alternate outputs, loop log/error flag, and ApplyTo count/in-place branches.
- M13X1 minimal backup: identity yaw -0 (R14 tie and sign flip), so diagnostic becomes -0.000000deg.
- M12RFix2 M12-019: assert unchanged exact q, not rounded matrix double cosine after R20 repair/reconstruction.
- M12RVis M12-008/M12-029: .6 yaw source float word 0x3F19999A; M12-026 32.6 -> 0x42026666; M12-036 NaN matrix ctor repairs to identity (R19/R20). Top-relative attachments assert retained stored relative transform, not bit-exact forward/inverse tree roundtrip (corrected R36). M12-037 Z offset uses one f32 addition (R1/R24).
- M8BatchThreeC: pathological raw matrix predicate fixture calls raw helper, avoiding unrelated R20 NaN-to-identity constructor; identity remains default result zero.
- RestingFlatTests: one float ULP below 10 degrees still fails native strict < because R2 IsNear excludes the near interval; inside/outside tests retained.
- M3M4X16: pi fixture uses native angle-axis constructor R3, avoiding a matrix->Rodrigues roundtrip at the half turn.
- M11RVis fallback match: first sphere candidate narrows Y tolerance to zero; subsequent box candidate's local Y is nonzero (R38/R8), so first near survives. Default identity uses q/T equality, not fresh wrapper object identity.
- RFixBatch1 cube faces: f32 q products R3/R4 and R17 product narrowing give exact zero diagonal terms for front/back/top/bottom; sin(pi_f) entry 0xB3BBBD2E.
- Navigation charger: R8 q components narrowed before squares produce scale 1-2^-24; width word 0x41D7FFFF, height 20-2^-20, predock X 0x42100004 and Z 0x40D00002, docked delta 194-2^-16 follow f32 sums R24/R29. CORE005 marker inputs/bounds retained unchanged.
- RFixBatch2 charger quad: uses R3 angle-axis input and independent checked-row generator Fixtures/native_pose_charger_reference.py (no C# output read). It follows q normalization and f32 R17, asserts row norms exactly one, and invokes the adopted independent minAreaRect evidence port. Four yaw corpus; three unchanged, -1.2 words C01FB1D0 C2CFF0AC C2152051 C167E8AC 429022C4 C295F680 42152055 4167E8AC.

## Validation

- Focused final migration: 155/155 carrying/M12/pose tests; final camera/planner/resting/native checks 166/166. LOCAL solver boundary check 17/17 includes unchanged CORE005 and pickup production entries.
- Release optimized JIT (.NET 9.0.3 x64 AVX512, TieredCompilation=0, FullOpts): 14/14 NativePoseBuilderTests pass; Rotation3d/Transform3d/Mat3/NativeRodrigues disassembly has zero fused multiply-add/sub instructions. Float point path retains cvtsd2ss, separate mulss/addss.
- Full suite: 4710/4710 passed, 0 skipped (Debug, 3 min 3 sec).
- fidelity.py regenerated and --check: PASS (606 records, statuses unchanged).
- No verification packet, hardware test, status promotion or hook bypass.

## Exact changed file/line inventory

All line numbers refer to this implementation revision. Each migration/test rationale is above; the following records every changed hunk and new file for review.

- `cozmo-stack/src/Cozmo.Robot/Behavior/AIWhiteboardHandlers.cs`: 70,3, 74 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs`: 1366, 1591, 1597, 1603, 1609, 1615, 1637 (start,count).
- `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs`: 2005 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Manipulation/AIWhiteboard.cs`: 286, 371, 413,7 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Manipulation/BlockConfigurations.cs`: 716 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Manipulation/ChargerActions.cs`: 500,2, 539 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Manipulation/DockActions.cs`: 419, 881 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs`: 1338 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Manipulation/NativeLatticePlannerRuntime.cs`: 119 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Manipulation/PreActionPose.cs`: 312, 364, 463,2, 497, 765, 858, 891 (start,count).
- `cozmo-stack/src/Cozmo.Robot/NativeRadians.cs`: 1,49 (start,count).
- `cozmo-stack/src/Cozmo.Robot/OffTreads.cs`: 317 (start,count).
- `cozmo-stack/src/Cozmo.Robot/UnexpectedMovement.cs`: 92 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs`: 336,5, 343 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Vision/CameraModel.cs`: 28, 31, 36, 42 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Vision/Faces.cs`: 202, 327 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs`: 1, 32,2, 36,8, 45,2, 48,8, 57,4, 62,8, 71,4, 76,4, 81,2, 84,5, 90,11, 102, 104, 106,6, 113,35, 150,2, 153,7, 161,2, 164, 166,9, 176,13, 190, 192,182 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Vision/NativePose2d.cs`: 1,71 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Vision/NativePoseArithmetic.cs`: 1,212 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Vision/NativeRodrigues.cs`: 1,171 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Vision/PoseDiagnostics.cs`: 1,34 (start,count).
- `cozmo-stack/src/Cozmo.Robot/Vision/PoseEstimation.cs`: 12,9, 30, 35,2, 40, 71, 73, 76, 79, 127, 134, 136,2, 140, 147, 154, 156,96 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/native_pose_charger_reference.py`: 1,32 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/M11RVisBuildTests.cs`: 1065,3, 1321 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/M12RFix2StreamATests.cs`: 42,3 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/M12RVisBuildTests.cs`: 614, 692,2, 935, 1195,2, 1229,2, 1336,2, 1423,2, 1884 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/M13X1BuilderTests.cs`: 36 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/M3M4X16BuilderTests.cs`: 65 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/M8BatchThreeCTests.cs`: 315,2, 322, 324, 326, 328, 330, 333 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/NativePoseBuilderTests.cs`: 1,138 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/NavigationTests.cs`: 247,4, 275,2, 278,3, 285 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/RFixBatch1Tests.cs`: 45,5 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/RFixBatch2Tests.cs`: 861, 872, 876 (start,count).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/RestingFlatTests.cs`: 49,2 (start,count).

## Per-call accounting for reopened instruction bodies

Calls below are deduplicated by library/address; the checked-row ranges above delimit the built parts. Untranscribed diagnostic/lifetime support remains MISSING.

| Binary | Call address | Target | C# counterpart / boundary |
| --- | --- | --- | --- |
| libcozmoEngine.so | 0x0084a522 | `__aeabi_memclr8` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084a538 | `cosf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084a540 | `sinf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084a582 | `_ZN4Anki15UnitQuaternion_IdE9NormalizeEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a5a2 | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a5ac | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a5b4 | `_ZN4AnkieqERKNS_7RadiansES2_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a5d0 | `__cxa_guard_acquire` | MISSING untranscribed native support call; no broader path settlement claimed |
| libcozmoEngine.so | 0x0084a5d6 | `_ZN4Anki9Z_AXIS_3DEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a5ee | `__cxa_guard_release` | MISSING untranscribed native support call; no broader path settlement claimed |
| libcozmoEngine.so | 0x0084a608 | `_ZN4Anki10Rotation3dC2ERKNS_7RadiansERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a616 | `__cxa_guard_abort` | MISSING untranscribed native support call; no broader path settlement claimed |
| libcozmoEngine.so | 0x0084a61c | `_Unwind_Resume` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084a646 | `_ZN4Anki16RotationVector3dC2ERKNS_16RotationMatrix3dE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a64e | `_ZN4Anki10Rotation3dC2ERKNS_16RotationVector3dE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a678 | `acos` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084a68e | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a6c0 | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a724 | `acos` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084a738 | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a740 | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00849ad2 | `sqrtf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x00849e32 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x00849e70 | `_ZN4Anki15UnitQuaternion_IdE9NormalizeEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00849eee | `_ZN4Anki15UnitQuaternion_IdEmLERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00849f84 | `_ZN4Anki15UnitQuaternion_IdE9NormalizeEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084aaac | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084aab4 | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084aada | `_ZN4Anki5PointILj3EfE14MakeUnitLengthEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084abba | `__aeabi_memclr4` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084abe0 | `_ZN4Anki18RotationMatrixBaseILj3EE11RenormalizeEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084c83e | `_ZN4Anki7Radians7rescaleEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084c876 | `_ZN4Anki7Radians7rescaleEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084c916 | `ceilf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084950c | `_ZNK4Anki11SmallMatrixILj3ELj3EfE6GetRowEj` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libcozmoEngine.so | 0x00849540 | `sqrtf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084959a | `_ZN4Anki17RenormalizeHelperERNS_18RotationMatrixBaseILj3EEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x008495c6 | `_ZN4Anki11SmallMatrixILj3ELj3EfEclEjj` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libcozmoEngine.so | 0x008495ce | `_ZNSt6__ndk19to_stringEf` | MISSING M11-083: visible Row placeholder, no substitute finite formatter |
| libcozmoEngine.so | 0x008495d8 | `0x4e8048` | MISSING untranscribed native support call; no broader path settlement claimed |
| libcozmoEngine.so | 0x00849604 | `0x4e8048` | MISSING untranscribed native support call; no broader path settlement claimed |
| libcozmoEngine.so | 0x0084965a | `_ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x0084967c | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084968a | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084afac | `__aeabi_memclr4` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084afd4 | `_ZN4Anki18RotationMatrixBaseILj3EE11RenormalizeEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084afdc | `cosf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084afe6 | `sinf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084aff4 | `cosf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b002 | `sinf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b018 | `cosf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b026 | `sinf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b206 | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b226 | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b240 | `asinf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b24a | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b260 | `cosf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b280 | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b288 | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b29c | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b2a4 | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b2bc | `_ZN4AnkimiEfRKNS_7RadiansE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b2c4 | `_ZN4Anki7RadiansaSERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b2cc | `cosf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b2e8 | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b2f0 | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b304 | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b30c | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b31e | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b32a | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b332 | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b35c | `_ZN4Anki7RadiansaSERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b3bc | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b3c4 | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b416 | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b41e | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b470 | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b478 | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b4da | `_ZNK4Anki10Rotation3d17GetRotationMatrixEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b4e4 | `_ZN4Anki11SmallMatrixILj3ELj3EfE3AbsEv` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libcozmoEngine.so | 0x0084b4fc | `_ZNK4Anki16RotationMatrix3d16GetAngleDiffFromERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b504 | `_ZN4AnkiltERKNS_7RadiansES2_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b530 | `_ZN4Anki11SmallMatrixILj3ELj3EfEclEjj` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libcozmoEngine.so | 0x0084b542 | `_ZN4Anki11SmallMatrixILj3ELj3EfEclEjj` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libcozmoEngine.so | 0x00844a6c | `_ZN4Anki4Util24sVerifyFailedReturnFalseEPKcS2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845908 | `_Znwj` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845910 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNodeC2ERKS4_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084591a | `_ZNSt6__ndk110shared_ptrIN4Anki8PoseBaseINS1_6Pose3dENS1_11Transform3dEE12PoseTreeNodeEEC2IS6_EEPT_NS_9enable_ifIXsr14is_convertibleISA_PS6_EE5valueENS7_5__natEE4typeE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845930 | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845936 | `_Unwind_Resume` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845950 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNodeaSERKS4_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845958 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode11RemoveOwnerEv` | MISSING parent-pointer/ownership diagnostic body, visible report |
| libcozmoEngine.so | 0x008459a8 | `_ZN4Anki4Util24sVerifyFailedReturnFalseEPKcS2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x008459b6 | `_ZNSt6__ndk119__shared_weak_count16__release_sharedEv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x008459da | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode9SetParentERKNSt6__ndk110shared_ptrIS4_EE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845a0c | `0x4e462a` | MISSING untranscribed native support call; no broader path settlement claimed |
| libcozmoEngine.so | 0x00845a28 | `0x4e8048` | MISSING untranscribed native support call; no broader path settlement claimed |
| libcozmoEngine.so | 0x00845a62 | `_ZNK4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode13HasSameRootAsERKS4_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845a7a | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845a96 | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845aa8 | `_ZNK4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode12GetTreeDepthEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845ab0 | `_ZNK4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode12GetTreeDepthEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845ac0 | `_ZN4Anki11Transform3d14PreComposeWithERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845ac8 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode30Dev_AssertIsValidParentPointerEPKS4_S6_` | MISSING parent-pointer/ownership diagnostic body, visible report |
| libcozmoEngine.so | 0x00845ad2 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode30Dev_AssertIsValidParentPointerEPKS4_S6_` | MISSING parent-pointer/ownership diagnostic body, visible report |
| libcozmoEngine.so | 0x00845b04 | `_ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845b26 | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845b3e | `_ZN4Anki4Util18sDebugBreakOnErrorEv` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845b54 | `_ZN4Anki11Transform3d14PreComposeWithERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845b5c | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode30Dev_AssertIsValidParentPointerEPKS4_S6_` | MISSING parent-pointer/ownership diagnostic body, visible report |
| libcozmoEngine.so | 0x00845b66 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode30Dev_AssertIsValidParentPointerEPKS4_S6_` | MISSING parent-pointer/ownership diagnostic body, visible report |
| libcozmoEngine.so | 0x00845b96 | `_ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845bb8 | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845bd0 | `_ZN4Anki4Util18sDebugBreakOnErrorEv` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845be4 | `_ZNK4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode15HasSameParentAsERKS4_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845bf0 | `_ZN4Anki11Transform3d14PreComposeWithERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845bf8 | `_ZN4Anki11Transform3d14PreComposeWithERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845c00 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode30Dev_AssertIsValidParentPointerEPKS4_S6_` | MISSING parent-pointer/ownership diagnostic body, visible report |
| libcozmoEngine.so | 0x00845c0a | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode30Dev_AssertIsValidParentPointerEPKS4_S6_` | MISSING parent-pointer/ownership diagnostic body, visible report |
| libcozmoEngine.so | 0x00845c34 | `_ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845c56 | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845c6e | `_ZN4Anki4Util18sDebugBreakOnErrorEv` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845c78 | `_ZNK4Anki11Transform3d10GetInverseEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845cb0 | `_ZN4Anki11Transform3dmLERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845cea | `_ZN4Anki4Util24sVerifyFailedReturnFalseEPKcS2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845d08 | `_ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845d2a | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845d32 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEaSERKS3_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845d44 | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845d9a | `_ZN4Anki11Transform3d6InvertEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845da6 | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00845dfe | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE9SetParentERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a8a4 | `_ZNK4Anki10Rotation3d19GetAngleAroundZaxisEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a8ae | `_ZNK4Anki10Rotation3d19GetAngleAroundYaxisEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a8b8 | `_ZNK4Anki10Rotation3d19GetAngleAroundXaxisEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a8c0 | `_ZNK4Anki7RadiansngEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a8c8 | `_ZNK4Anki10Rotation3d19GetAngleAroundXaxisEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a8d0 | `_ZNK4Anki10Rotation3d19GetAngleAroundYaxisEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a8d8 | `_ZNK4Anki10Rotation3d19GetAngleAroundZaxisEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a970 | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084a978 | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084aa0e | `atan2f` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084aa16 | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x008457fc | `_Znwj` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845814 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNodeC2ERKS2_RKNSt6__ndk110shared_ptrIS4_EERKNS7_12basic_stringIcNS7_11char_traitsIcEENS7_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845822 | `_ZNSt6__ndk110shared_ptrIN4Anki8PoseBaseINS1_6Pose3dENS1_11Transform3dEE12PoseTreeNodeEEC2IS6_EEPT_NS_9enable_ifIXsr14is_convertibleISA_PS6_EE5valueENS7_5__natEE4typeE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084582a | `_ZNSt6__ndk119__shared_weak_count16__release_sharedEv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845844 | `_ZNSt6__ndk119__shared_weak_count16__release_sharedEv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845856 | `_Unwind_Resume` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x008448fa | `_ZN4Anki4Util24sVerifyFailedReturnFalseEPKcS2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845ebc | `_ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00845ede | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00845ef6 | `_ZN4Anki4Util18sDebugBreakOnErrorEv` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x0084a22c | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a23e | `_ZN4Anki5PointILj3EfE14MakeUnitLengthEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a24a | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a252 | `_ZN4AnkieqERKNS_7RadiansES2_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a268 | `_ZN4Anki9X_AXIS_3DEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a2c0 | `_ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x0084a2e4 | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00844bf2 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode30Dev_AssertIsValidParentPointerEPKS4_S6_` | MISSING parent-pointer/ownership diagnostic body, visible report |
| libcozmoEngine.so | 0x00844c20 | `_ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00844c42 | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00844c5a | `_ZN4Anki4Util18sDebugBreakOnErrorEv` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00844c66 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEE12PoseTreeNode30Dev_AssertIsValidParentPointerEPKS4_S6_` | MISSING parent-pointer/ownership diagnostic body, visible report |
| libcozmoEngine.so | 0x00844c94 | `_ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x00844cb6 | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00844cce | `_ZN4Anki4Util18sDebugBreakOnErrorEv` | PoseDiagnostics keyed Warning/Error/Verify; error flag/debug or flush; untranscribed ownership/parent VERIFY is MISSING as listed |
| libcozmoEngine.so | 0x0084b626 | `cosf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b640 | `sinf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084b666 | `_ZN4Anki7RadianspLERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b670 | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b678 | `_ZN4Anki16RotationMatrix2dC1ENS_7RadiansE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b6f6 | `_ZN4AnkiplERKNS_7RadiansES2_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b700 | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b708 | `_ZN4Anki16RotationMatrix2dC1ENS_7RadiansE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b712 | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b71a | `_ZN4Anki16RotationMatrix2dC1ENS_7RadiansE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b77a | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b794 | `_ZN4Anki7RadianspLERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b79e | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b7a6 | `_ZN4Anki16RotationMatrix2dC1ENS_7RadiansE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b816 | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b822 | `_ZN4Anki11Transform2d6InvertEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b834 | `_ZN4Anki7RadiansmLEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b83e | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b846 | `_ZN4Anki16RotationMatrix2dC1ENS_7RadiansE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b8b4 | `_ZN4Anki7RadiansC1ERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b8bc | `_ZN4Anki16RotationMatrix2dC1ENS_7RadiansE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b940 | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b99e | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b9ca | `_ZN4Anki10Rotation3dmLERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084b9e8 | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084ba12 | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084ba18 | `_ZN4Anki10Rotation3dmLERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084ba20 | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084ba3c | `_ZN4Anki10Rotation3d13PreMultiplyByERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084ba4a | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084ba7e | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084ba90 | `_ZN4Anki11Transform3d6InvertEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084ba9c | `_ZN4Anki10Rotation3d6InvertEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bac2 | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bae4 | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084baf2 | `_ZN4Anki10Rotation3d13PreMultiplyByERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb08 | `_ZNK4Anki10Rotation3d7GetAxisEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb14 | `_ZN4Anki10Rotation3dC1ERKNS_7RadiansERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb24 | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb32 | `_ZN4Anki10Rotation3d13PreMultiplyByERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb44 | `_ZN4Anki10Rotation3dC1ERKNS_16RotationVector3dE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb54 | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb62 | `_ZN4Anki10Rotation3d13PreMultiplyByERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb74 | `_ZN4Anki10Rotation3dC1ERKNS_16RotationMatrix3dE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb84 | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084bb92 | `_ZN4Anki10Rotation3d13PreMultiplyByERKS0_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0050e100 | `sqrtf` | Math/MathF with checked width/order; system-library M13-023(e) |
| libcozmoEngine.so | 0x0084684c | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846850 | `_ZN4Anki9Z_AXIS_3DEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846864 | `_ZN4Anki6Pose3dC2ERKNS_7RadiansERKNS_5PointILj3EfEES7_RKNSt6__ndk112basic_stringIcNS8_11char_traitsIcEENS8_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00846878 | `_ZN4Anki10Rotation3dC1ERKNS_7RadiansERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846882 | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846896 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKNSt6__ndk112basic_stringIcNS6_11char_traitsIcEENS6_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x008468b2 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKNSt6__ndk112basic_stringIcNS6_11char_traitsIcEENS6_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x008468ca | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKS1_RKNSt6__ndk112basic_stringIcNS8_11char_traitsIcEENS8_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x008468ee | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846902 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKNSt6__ndk112basic_stringIcNS6_11char_traitsIcEENS6_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084692c | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846942 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKS1_RKNSt6__ndk112basic_stringIcNS8_11char_traitsIcEENS8_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084696c | `_ZN4Anki10Rotation3dC1ERKNS_16RotationVector3dE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846976 | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084698a | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKNSt6__ndk112basic_stringIcNS6_11char_traitsIcEENS6_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x008469b4 | `_ZN4Anki10Rotation3dC1ERKNS_16RotationVector3dE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x008469be | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x008469d4 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKS1_RKNSt6__ndk112basic_stringIcNS8_11char_traitsIcEENS8_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00846a00 | `_ZN4Anki10Rotation3dC1ERKNS_16RotationMatrix3dE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846a0a | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846a1e | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKNSt6__ndk112basic_stringIcNS6_11char_traitsIcEENS6_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00846a48 | `_ZN4Anki10Rotation3dC1ERKNS_16RotationMatrix3dE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846a52 | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846a68 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKS1_RKNSt6__ndk112basic_stringIcNS8_11char_traitsIcEENS8_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x00846a92 | `_ZN4Anki10Rotation3dC1ERKNS_7RadiansERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846a9e | `_ZN4Anki15UnitQuaternion_IdEC1ERKS1_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00846ab4 | `_ZN4Anki8PoseBaseINS_6Pose3dENS_11Transform3dEEC2ERKS2_RKS1_RKNSt6__ndk112basic_stringIcNS8_11char_traitsIcEENS8_9allocatorIcEEEE` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084a308 | `_ZdlPv` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084a30e | `_Unwind_Resume` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084a380 | `_ZN4Anki7RadiansC1Ev` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a392 | `_ZN4Anki5PointILj3EfE14MakeUnitLengthEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a39a | `_ZN4Anki7RadiansaSEf` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a3a4 | `_ZN4Anki7RadiansC1Ef` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a3ac | `_ZN4AnkieqERKNS_7RadiansES2_` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0084a3b4 | `_ZN4Anki9X_AXIS_3DEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x0065676c | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x006567e0 | `_ZNK4Anki10Rotation3dmlERKNS_5PointILj3EfEE` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libcozmoEngine.so | 0x00656820 | `_ZNSt6__ndk16vectorIN4Anki5PointILj3EfEENS_9allocatorIS3_EEE24__emplace_back_slow_pathIJS3_EEEvDpOT_` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084b0b6 | `__aeabi_memclr4` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libcozmoEngine.so | 0x0084b0da | `_ZN4Anki18RotationMatrixBaseILj3EE11RenormalizeEv` | NativeRadians / Rotation3d / Mat3 / Transform3d / Pose3d / NativePose2d checked helper; named unbuilt ownership/serialization boundaries stay MISSING |
| libopencv_calib3d.so | 0x00011218 | `_ZNK2cv11_InputArray6getMatEi` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0001124a | `_ZNK2cv12_OutputArray6createEiiiibi` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x00011258 | `_ZNK2cv11_InputArray6getMatEi` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x00011268 | `0xb2a8` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x00011270 | `0xb2a8` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x00011276 | `_ZNK2cv12_OutputArray6neededEv` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x000112a8 | `_ZNK2cv12_OutputArray6createENS_5Size_IiEEiibi` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x000112b8 | `_ZNK2cv11_InputArray6getMatEi` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x000112c0 | `0xb2a8` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x000112d8 | `_ZN2cv3MatD1Ev` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libopencv_calib3d.so | 0x000112de | `_ZNK2cv12_OutputArray6neededEv` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x000112ee | `cvRodrigues2` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0001130e | `_ZN2cv3MataSERKNS_7Scalar_IdEE` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x00011314 | `_ZN2cv3MatD1Ev` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libopencv_calib3d.so | 0x0001131a | `_ZN2cv3MatD1Ev` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libopencv_calib3d.so | 0x00011326 | `__stack_chk_fail` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x0001132c | `_ZN2cv3MatD1Ev` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libopencv_calib3d.so | 0x00011334 | `_ZN2cv3MatD1Ev` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libopencv_calib3d.so | 0x00011338 | `__cxa_end_cleanup` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x0000f2e2 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_calib3d.so | 0x0000f32a | `cvSetIdentity` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0000f33c | `memset` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x0000f370 | `memcpy` | typed storage/copy/list counterpart; MISSING native allocation, unwind and control-block teardown (no lifetime fidelity claim) |
| libopencv_calib3d.so | 0x0000f378 | `cos` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_calib3d.so | 0x0000f384 | `sin` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_calib3d.so | 0x0000f3fc | `memset` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x0000f6a6 | `cvConvertScale` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0000f6ba | `cvCheckArr` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0000f6c4 | `cvSetZero` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0000f6d0 | `cvSetZero` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0000f6e8 | `cvSVD` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0000f70a | `cvGEMM` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0000f74a | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_calib3d.so | 0x0000f79c | `acos` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_calib3d.so | 0x0000f7e2 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_calib3d.so | 0x0000f80c | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_calib3d.so | 0x0000f850 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_calib3d.so | 0x0000f8ce | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_calib3d.so | 0x0000f8f2 | `memset` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x0000f98a | `memset` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x0000f9d4 | `memset` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x0000fa02 | `memset` | NativeRodrigues checked fixed CV_32F path; MISSING generic CV wrapper allocation/error support |
| libopencv_calib3d.so | 0x0000faaa | `cvGEMM` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_calib3d.so | 0x0000fac6 | `cvGEMM` | NativeRodrigues fixed CV_32F arrays: checked conversion/range/zero/identity/SVD/GEMM; unsupported CV API and allocation/error paths MISSING |
| libopencv_core.so | 0x00059602 | `0x58fb4` | NativeRodrigues JacobiSvd / OpenCvSolveSvd.Hypot / fixed GEMM; MISSING native allocation/error support |
| libopencv_core.so | 0x00059710 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_core.so | 0x0005973c | `0x56eb0` | NativeRodrigues JacobiSvd / OpenCvSolveSvd.Hypot / fixed GEMM; MISSING native allocation/error support |
| libopencv_core.so | 0x00059762 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_core.so | 0x00059788 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_core.so | 0x00059866 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_core.so | 0x00059ae4 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_core.so | 0x00059b38 | `0x38b38` | NativeRodrigues JacobiSvd / OpenCvSolveSvd.Hypot / fixed GEMM; MISSING native allocation/error support |
| libopencv_core.so | 0x00059b48 | `__stack_chk_fail` | NativeRodrigues JacobiSvd / OpenCvSolveSvd.Hypot / fixed GEMM; MISSING native allocation/error support |
| libopencv_core.so | 0x00056ed8 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_core.so | 0x00056f00 | `sqrt` | Math/MathF with checked width/order; system-library M13-023(e) |
| libopencv_core.so | 0x00071f9a | `0x71bb0` | NativeRodrigues JacobiSvd / OpenCvSolveSvd.Hypot / fixed GEMM; MISSING native allocation/error support |
| libopencv_core.so | 0x00071fce | `0x71bb0` | NativeRodrigues JacobiSvd / OpenCvSolveSvd.Hypot / fixed GEMM; MISSING native allocation/error support |
| libopencv_core.so | 0x00072002 | `0x71bb0` | NativeRodrigues JacobiSvd / OpenCvSolveSvd.Hypot / fixed GEMM; MISSING native allocation/error support |
| libopencv_core.so | 0x000723b8 | `0x71bb0` | NativeRodrigues JacobiSvd / OpenCvSolveSvd.Hypot / fixed GEMM; MISSING native allocation/error support |
