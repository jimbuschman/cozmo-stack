# POSE-SOLVEPNP-1: Claude check (manager, 2026-11-02)

**Verdict: PASS after one correction. M11-054 settled (EXACT_SOURCE), M11-099 added and settled (EQUIVALENT_IMPLEMENTATION, libm
only), M11-100 added (IMPLEMENTATION_GAP). No robot has run this.**

Built by a Claude implementer from Codex's extraction `research/20261004-solvepnp-extraction.md`; checked by a separate verifier who
opened the engine and calib3d.

## What changed

- `Vision/NativeSolvePnP.cs`: the shipped iterative `cv::solvePnP` (method 0, no guess): undistortion, planarity test, homography and
  DLT initial estimates, Levenberg-Marquardt with the shipped projection, Jacobians, Rodrigues, Jacobi eigen and SVD, back
  substitution, GEMM and norm reduction orders. `PoseEstimation.Solve` narrows inputs to binary32 and the result to binary32 as the
  engine does (0x0085D77E..0x0085D8E2).
- `CameraModel.HeadCamPositionMm` is the engine's (17.52, 0, -8.0) (0x0050FF8A). First visible floor at VisitInterestingEdge's head is
  about 43.5 mm, against about 78 mm before. The 49 and 89 mm figures in `20261102-visit-edge-claude-check.md` were wrong; the
  implementer, the verifier and I each computed 43.5..43.7 and 78.
- `MarkerRenderer` (test harness only): pixel centres on integers as in `CameraModel.Project` and OpenCV, and 8x8 sampling. This, not
  the solver, is why the three marker tests named in M11-054 missed at z -8. The 8x8 grid is a harness choice.

## Proof

`re-analysis/tools/emu/emu_solvepnp.py` runs the shipped calib3d, core, imgproc, flann, features2d and libc++ under Unicorn, called
with the engine's own wrapper types and flags (verifier disassembled 0x0085D77E..0x0085D8A2 and compared). Python stands in only for
libm and IEEE-exact `__aeabi` double ops. Fixture inputs come from a Python pinhole model; expected values only from the emulator.
254 of 254 cases bit-exact on all six output doubles, 0 ulp, stable under heap poison 0x00/0xFF and NEON on/off. The verifier re-ran
seven cases independently: bit-exact. 34 mutants, 24 caught; the 10 survivors are equivalent or unobservable on this path.

The 0 ulp holds on this host because the oracle and .NET share the Windows libm; bionic's last bit is not established.

## Correction made at check

The implementer changed `CloseEdgeFlagsTheVisitedConeAndObserves` to use real frames at a 1/15 s gather period. It passed for the
implementer and for me but failed twice for the verifier (state 4: the reading consumed was NaN, though a 53.78 mm reading was logged).
A result that varies between runs proves nothing, and the 15 fps period has no source (M3-device says the camera rate is hardware). The
test is restored to its supplied-chain form and its header updated. **Whether the close branch is reached from real frames is not
established**, and the nondeterminism in that harness is a queued item.

## Expectations changed

- Camera height tests: from 0xC1000000 (VisionTests, BPose2).
- `OverheadEdgeTests.TheArmInTheWayAbandonsTheFrame`: at minimum head with the lift down the arm crosses the ROI; the verifier's hand
  check of the engine's gate (Detect 0x006ABE34, GetImageQuad clamp 0x004F78E0) gives the same abandon. At VisitInterestingEdge's head
  frames are kept with about a 14-row margin.
- CORE007 pair, overhead map writer, M11 timestamp pair: inputs moved to the behaviour's own head/lift, because the old configuration is
  now abandoned by the engine's own rule.
- NavigationTests: the 10 mm lattice (RoundSafe 0x00855AF8): stricter, not looser.

No tolerance was widened.

## Records

- **M11-054 -> EXACT_SOURCE.** The constant is applied; the solver behind it is M11-099. Its old text blaming the solver was wrong.
- **M11-099 new, EQUIVALENT_IMPLEMENTATION.** Libm only. Not built and unreachable: planar sets above four points (H03) and tilt
  coefficients (P06; the engine zeroes the distortion). Inputs inherit M11-003's double-built corners.
- **M11-100 new, IMPLEMENTATION_GAP.** BlockWorld's 3 px and 6 px RMS gates have no engine counterpart (ComputePossiblePoses 0x008769F0,
  PoseCluster::RecomputePose 0x0062A490). The cluster re-solve itself is real.
- **M11-017 note.** Lift gate shape differs when A does not project; not reached by the tests' heads.

## Queued

- Close edge from real frames, and why the canonical run varies between runs.
- M11-100: remove the RMS gates after comparing clustering with PoseCluster.
- `Docking.cs:706` solves a marker-frame pose each frame for the legacy DockAsync path; the engine has no such solve.
- `CameraModel.Undistort` (10 iterations, skew) differs from U02/U05; `Ray` has no callers.
- `emu_solvepnp.py` enforces hashes for core, imgproc and libc++ only.
- A fixture case with a bundle's exact calibration bits.

## Progress

19 records settled across 42 start-path jobs, 1 of 178 finished.
