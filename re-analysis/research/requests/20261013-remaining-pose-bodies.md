# Research request R-POSE-REST (M11-068)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-pose3d-arithmetic.md` and `research/20261013-rodrigues-and-pose-tree.md`, and the
  M11-068 record in `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-remaining-pose-bodies.md`.
- **Format:** one coverage table per item, first. For each item give the exact formula in evaluation order, the
  precision (f32/f64) at each step, the address, and any logs.

## Items

1. UnitQuaternion<double> · Point3d (0x00849F90..0x0084A062).
2. RotationMatrix3d(Radians, Radians, Radians), the Euler constructor; GetEulerAngles; and the matrix
   GetAngleAroundX/Yaxis (0x0084AF98..0x0084B424).
3. RotationMatrixBase IsValid (0x008497D0..0x008498E4), the initializer_list constructor (0x00849900), and SmallMatrix
   Abs (0x0084B51E).
4. Transform3d::ApplyTo<float>(vector) (0x0065671C).
5. The Pose2d / Transform2d family (0x0084B619..0x0084B92A).
6. PoseBase::GetParent (0x008453D4..0x00845448) and FindRoot (0x0084520C..0x0084529C): do they share the node, or
   copy it?
7. **Reachability of PoseBase::SetParent with a null parent node.** The log reads [null+0x38]/[null+0x44] at
   0x00844A46..0x00844A52. Find every SetParent caller that can pass a pose whose node is null.
