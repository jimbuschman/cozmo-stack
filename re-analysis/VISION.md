# M11 — vision and world state: markers, cube localisation, BlockWorld

**Status: COMPLETE OFFLINE, hardware pending (HARDWARE_TEST_PLAN.md items K–M).** 2026-09-19.

M11 makes cube localisation real. The engine's fiducial pipeline, its compiled-in marker library, its camera
and cube geometry and its `BlockWorld` located/visible semantics are transcribed into `Cozmo.Robot.Vision`;
the `ICubeLocator` seam M10 left open is filled by `CubeLocator`, which activates the cube-moved reaction, and
the shipped `ObjectPositionUpdated → AcknowledgeObject` reaction runs on the same world model. Faces, pets and
motion are out of scope for a stated reason (§8). Source of truth throughout: `libcozmoEngine.so` 3.4.0-1204.

Provenance labels as in `SOURCE_FIDELITY_AUDIT.md`: NATIVE (read from the binary), UNITY (decompiled C# enums),
ASSET (OBB file), INFERRED (a reading not confirmed in code), LOCAL / LOCAL_POLICY (ours, stated), DEFERRED.

## 1. What was added

| piece | file | provenance |
| --- | --- | --- |
| marker codes `MarkerType` (0..39) | `Vision/MarkerLibrary.cs` | NATIVE: name table indexed by `Marker::GetNameForCode` 0x0087E0D4 (GOT 0x1038220) |
| the nearest-neighbour library: 598 probe images × 1024, labels, label→code, corner reorder, orientation, probe geometry | `re-analysis/tools/extract_marker_library.py` → `Vision/Data/marker_nn_library.bin` (git-ignored; **not in the repository**, see §2) | NATIVE: `VisionMarker::GetNearestNeighborLibrary` 0x0089ED1C and the tables it passes (addresses in the script) |
| decoder: probe sampling, min-max normalisation, L1 nearest neighbour, ambiguity test, label tables | `MarkerDecoder` | NATIVE algorithm (§2) |
| quad front end | `QuadDetector`, `QuadCorners` | pipeline, parameters, dark mask, corner extraction and quad acceptance NATIVE (`Parameters::Initialize` 0x008752F8, `ExtractComponentsViaCharacteristicScale_binomial` 0x00890448, `BinomialFilter` 0x008A2344, `TraceNextExteriorBoundary` 0x008C6B18, `ExtractLineFitsPeaks` 0x008A5DB8, `IsQuadrilateralReasonable` 0x00892B18); the sub-pixel refinement LOCAL (§3, M11-005) |
| camera calibration struct, NV read | `CameraCalibration`, `NvCalibrationReader` | UNITY struct, NATIVE tag 0x80000001; request framing INFERRED |
| camera pose on the robot | `HeadGeometry` | NATIVE: `Robot::Robot` neck (−13, 0, 49), head cam (17.52, 0, 17.52), `_kDefaultHeadCamRotation` at 0xC4A854, `GetCameraPose` |
| projection and distortion | `CameraModel` | OpenCV model the engine's `Vision::Camera` uses; numerics LOCAL |
| cube geometry: 44 mm, 25 mm markers, six face poses, code assignment | `CubeGeometry`, `KnownMarker` | NATIVE: `Block::LookupBlockInfo` 0x004E4C8C, `Block::AddFace`, `KnownMarker::_canonicalCorners3d` 0x004DD7D8 |
| pose from corners | `PoseEstimation` | stands in for `cv::solvePnP` in `Camera::ComputeObjectPoseHelper`; numerics LOCAL |
| world model: located objects, Known/Dirty/Unknown, matching to connected cubes, unobserved marking, `IsVisibleFrom` | `BlockWorld`, `ObservableObject` | NATIVE rules (§5) with three INFERRED constants |
| robot state at frame time | `RobotStateHistory`, `VisionPoseData` | engine's `RobotStateHistory`; windows LOCAL |
| the system: frame → gray → markers → world | `VisionSystem` | engine's `VisionSystem::Update` / `VisionComponent` order |
| the locator and the turn | `CubeLocator`, `TurnTowardsPose` | `TurnTowardsPoseAction::Init` 0x0054A8FC NATIVE; `SetBodyAngle` packing NATIVE (0x00640898); absolute-angle semantics INFERRED |
| the reaction `ObjectPositionUpdated → AcknowledgeObject` | `Behavior/ObjectBehaviors.cs` | NATIVE constants (§6) |
| synthetic renderer for tests and the self-test | `MarkerRenderer` | LOCAL (border width) over NATIVE probe images |
| tools | `vision --synthetic / --replay / --images / <ip>`, `bodyangle <ip>` | — |

## 2. The marker decoder — NATIVE

`Anki::Embedded::VisionMarker::Extract` classifies a quad by nearest neighbour over a library linked into the
binary. Everything below was read from the constructor at 0x0089ED1C and the functions it calls.

**Library.** `NearestNeighborLibrary(images @0xC9BCA8 [598][1024] u8, images2 @0xD314A8 (passed, never copied into
the database), labels @0xDC6CA8 u16[598], numImages 598, numProbes 1024, ProbeCenters_X/Y, ProbePoints_X/Y, 5, 15)`.
Tables: `labelToCode` u32[150] @0xDC7154, `cornerReorder` u32[150][4] @0xDC73AC (the four permutations
(0,1,2,3), (1,3,0,2), (3,2,1,0), (2,0,3,1)), `orientationDeg` f32[150] @0xDC7D0C (0/90/180/270). 149 labels have
four images each and label 149 (INVALID) has two; every code 0..38 is covered by four labels, one per quarter
turn (code 39 UNKNOWN has none).

**Distribution.** The extracted blob is Anki's data (a copy of tables linked into the engine), and this repository
excludes proprietary Cozmo assets, so the blob is **not committed**: `re-analysis/tools/extract_marker_library.py`
(the deterministic extractor, with every address, the layout and the expected SHA-256 `1b7166ac…` / 621384 bytes)
is what ships. The build runs it against the user's own `libcozmoEngine.so` when the `.so` and python are present
(`Cozmo.Robot.csproj`, target `ExtractMarkerLibrary`), embeds the result, and `MarkerLibrary` verifies the hash on
load; `--check` re-extracts and compares byte for byte. Without the `.so` the library is absent,
`MarkerLibrary.IsAvailable` is false, the vision tests that need it skip (as the OBB-dependent tests do) and the
tools say so. The marker fidelity is unchanged; only how the derived artifact reaches a machine changed.

**Probe geometry** (exported data symbols): `ProbeCenters_X/Y` s16[1024], a 32×32 grid from 7149 to 25619 in units
of 1/2¹⁵ (0.218..0.782 of the unit square), column-major (x constant over 32 consecutive entries);
`ProbePoints_X/Y` = (0, 205, 0, −205, 0) / (0, 0, 205, 0, −205), the five offsets averaged per probe;
`ThresholdDarkProbe_X/Y` twelve points at 0.0125 in from the quad's edges, `ThresholdBrightProbe_X/Y` twelve at
0.15 in.

**Algorithm.** `GetProbeValues` (0x0089EF41): for each probe, the mean of the 5 nearest-pixel samples at the
homography image of centre + offset, with x rounded as floor(x + 0.5) and y as ceil(y − 0.5).
`GetNearestNeighbor`: min-max normalise the query to 0..255; distance to each row = Σ|q − db| / 1024 (integer);
keep best and second best; if their labels differ, average |q − db_best| over the probes where |db_best −
db_second| > 50 and reject when that average ≥ 1.25 × 50. `Extract`: reject distance ≥ 50 (the `movs r2, #0x32`
passed from `DetectFiducialMarkers`) or label ∈ {149, 150}; otherwise code = labelToCode[label], corners
reordered by cornerReorder[label], orientation = orientationDeg[label]. Corner order is TL, BL, TR, BR.

Offline evidence: every one of the 598 library rows, rendered and decoded, returns its own label; a marker
handed to the decoder with its quad rotated a quarter turn decodes to the same code with the same corner
assignment (the rotation labels and the reorder table doing what they are for); a blank quad is rejected by the
dark/bright gate.

## 3. The quad front end — structure NATIVE, pixels LOCAL

The engine's `DetectFiducialMarkers` runs `ExtractComponentsViaCharacteristicScale_binomial →
InvalidateSmallOrLargeComponents → InvalidateSolidOrSparseComponents → InvalidateFilledCenterComponents_hollowRows →
ComputeQuadrilateralsFromConnectedComponents → (corner refinement) → ComputeHomographyFromQuad` (profiler labels
in the binary). Its parameters (`MarkerDetector::Parameters::Initialize` 0x008752F8), re-read on 2026-09-21
against the call sites that consume them: **1** pyramid level (+4), threshold multiplier **0xCCCC** in Q16
(+0xC), component pixel bounds 100 and 39000, 32000 segments, minimum quad area 25 (+0x30), quad symmetry
threshold 512 in 8.8 - two - (+0x34), image-edge distance 2 (+0x38), 500 markers, fill ratio bounds 0.03 and
0.8, rounded-corner fraction 0.15, side-length fractions 0.1, hollow-row fill 0.97, 25 refinement iterations,
corner change bounds 0.005 and 5.0. (The 512 had been recorded as a maximum number of quads, and the dark
multiplier as a local 0.75.)

`QuadDetector` follows that structure with those numbers, and two of its stages are now the engine's rather
than local (M11-018): the dark mask is `BinomialFilter` - the separable five-tap [1 4 6 4 1] with `>> 4` and
the edge pixel standing in at the borders - followed by `(filtered * 0xCCCC) >> 16 > pixel`, and the quad
acceptance test is `IsQuadrilateralReasonable`'s four rules (minimum area, convexity, one diagonal's
triangles within a factor of two, every corner two pixels clear of the image edge).

**The corners are now the engine's too** (`QuadCorners`, the whole of `ExtractLineFitsPeaks` 0x008A5DB8 and
the boundary trace it works on):

1. `TraceNextExteriorBoundary` 0x008C6B18 never walks the component pixel by pixel. It reduces it to four
   extent arrays over its bounding box - the least and greatest x in each row, the least and greatest y in
   each column - and stitches a staircase contour out of them in four passes: down the right side, left along
   the bottom, up the left, back along the top, starting and ending at the rightmost pixel of the top row.
   An empty row or column in the box makes the trace fail. The list it fills holds 10000 points (0x00892DA8).
2. The contour is smoothed with the *derivative of a Gaussian*: `cv::getGaussianKernel` with
   sigma = length / 64 and OpenCV's size-to-sigma relation solved backwards,
   `ceil(((sigma - 0.8) / 0.3 + 1) * 2 + 1)` forced odd, then `cv::filter2D` of that kernel with
   [-0.5, 0, +0.5]. That single kernel is convolved circularly with the boundary, in double, and normalised:
   a unit tangent per point.
3. `cv::kmeans` splits the tangents into four - the four sides - seeded with four equal arcs of the boundary
   and run with `KMEANS_USE_INITIAL_LABELS`, one attempt, fifteen iterations, epsilon 0.1. Nothing about it
   is random, which is why it reproduces.
4. Each cluster is fitted by least squares across whichever of its extents is wider: `y = a x + b` for a
   flattish side and `x = a y + b` for a steep one (the flag at 0x008A6714), so no side is ever fitted
   against a vertical.
5. Every pair of the four lines is intersected, each intersection is kept only if it lands inside the image,
   and *exactly four* must survive. `Quadrilateral<float>::ComputeClockwiseCorners` 0x008A1324 sorts them by
   the angle they make with their centroid, ascending, which with y down is clockwise on screen; they are
   then rounded half away from zero into the s16 quad.
6. The quad the engine stores is that clockwise order permuted 0, 3, 1, 2 - upper left, lower left, upper
   right, lower right, which is the decoder's order - and `IsQuadrilateralReasonable` both accepts it and
   says whether its middle pair needs exchanging, which is the winding flag it returns through its `bool&`.

What is still local is the sub-pixel refinement (M11-005): the engine refines later, in
`DetectFiducialMarkers` through `VisionMarker::RefineCorners` 0x0089FD98 and
`RefineQuadrilateral` 0x008C55E0, and this stack refines each side to the dark-to-light edge with its own
line fit instead. On rendered markers the corners land within 1 px (test) and PnP reprojection is 0.1-0.4 px
(§7). The engine's own `component_minimumNumPixels` of 100 puts a floor under how small a marker can be:
rendered squares are found down to twenty pixels a side and lost at sixteen, which is roughly a cube at
350 mm in this camera.

## 4. Camera and cube geometry — NATIVE

**Camera on the robot.** `Robot::Robot` creates "RobotNeck" at (−13.0, 0, 49.0) mm in the robot frame (X forward,
Y left, Z up) and "RobotHeadCam" at (17.52, 0, 17.52) from the neck with `_kDefaultHeadCamRotation` = the nine
floats at 0xC4A854, `[0, −0.0698, 0.9976; −1, 0, 0; 0, −0.9976, −0.0698]`: camera X (image right) → robot −Y,
camera Y (image down) → robot −Z, optical axis → robot X tilted 4° down. `Robot::GetCameraPose(headAngle)`
rotates that about the neck's Y by −headAngle (head up = positive). The robot pose is `RobotState.Pose` X, Y, Z
and yaw; its pitch is not applied (LOCAL, small on level ground).

**Calibration.** The engine reads `NVEntry_CameraCalib` (0x80000001) on `RobotConnectionResponse` (0x006583EE)
through `NVStorageComponent::Read`; the CLAD struct is `CameraCalibration {fx, fy, cx, cy, skew, nrows u16,
ncols u16, distCoeffs f32[8]}` (UNITY). `VisionSystem::Update` refuses to run without it ("Must be initialized and
have calibrated camera to Update"); so does ours. No default numbers exist in the engine. `CameraCalibration.Nominal`
(f = 290 px, 320×240, no distortion) is a LOCAL_POLICY stand-in for offline tests and the `--nominal` tool switch
and is never used silently.

**Cube.** `Block::LookupBlockInfo`: size 44.0 mm, marker 25.0 mm; face definitions {name, code, 25.0, …} give
LIGHTCUBE1 FRONT→6, LEFT→7, BACK→4, RIGHT→8, TOP→9, BOTTOM→5 (cube 2 +6, cube 3 +12; GHOST all 39). `Block::AddFace`
builds each marker pose as `Pose3d(angle, axis, translation)`:

| face | rotation | translation (× cube size) |
| --- | --- | --- |
| Front | −90° about Z | (−½, 0, 0) |
| Left | 180° about Z | (0, +½, 0) |
| Back | +90° about Z | (+½, 0, 0) |
| Right | 0 | (0, −½, 0) |
| Top | 120° about (−0.577, 0.577, −0.577) | (0, 0, +½) |
| Bottom | 120° about (0.577, −0.577, −0.577) | (0, 0, −½) |

`KnownMarker::_canonicalCorners3d` (initialiser 0x004DD7D8) puts the unit marker in the X–Z plane: TL(−½, 0, +½),
BL(−½, 0, −½), TR(+½, 0, +½), BR(+½, 0, −½), scaled by the marker size in `Get3dCorners`; the marker's outward
normal is its −Y. A test checks that every face's normal points out of its face and its corners lie 22 mm out.

## 5. BlockWorld — located, visible, unobserved

Transcribed from `BlockWorld::UpdateObservedMarkers` → `CreateObjectsFromMarkers` → `AddAndUpdateObjects` →
`CheckForUnobservedObjects` and `ObservableObject::IsVisibleFrom`:

* **Located** = pose state not Unknown (`GetLocatedObjectByIdHelper`). States print "Known" and "Dirty".
* **Active objects must be connected.** An observed cube type with no connected cube of that type is dropped:
  "Observed active object of type %s but it's not connected". (`AllowUnconnectedObjects` is a LOCAL_POLICY switch
  for offline tools; off by default.)
* **Visibility** (`KnownMarker::IsVisibleFrom`): a marker is visible when the angle between its normal and the
  line to the camera is under the maximum (0.785398 rad everywhere the behaviours ask), its projected size (√area)
  is at least the minimum, and all four corners are inside the padded field of view; an object is visible when
  any marker is. The engine's occluder list (lift, other objects) and `IsAnythingBehind(0.25)` are DEFERRED.
* **Unobserved.** After each frame, every located object not seen that `IsVisibleFrom` the frame's camera gets
  `MarkObjectUnobserved`; the check is skipped while the robot `WasMoving` or `WasRotatingTooFast` (0.174533 rad/s).
  After enough misses `MarkObjectUnknown`. INFERRED: the miss count is read as `cmp r3, #1` and implemented as
  2 misses; the minimum projected marker size is 10 px.
* **Observation makes the pose Known at once.** INFERRED: the `ObjectPoseConfirmer`'s confirmation counting was
  not transcribed; the AcknowledgeObject behaviour's own two-image verification covers the visible effect.
* **Dirty.** INFERRED: an `ObjectMoved` report on a located cube sets Dirty until it is seen again; Dirty is still
  located, which is what the cube-moved strategy relies on.
* **Pose from several markers.** Per-marker PnP; markers of one type whose implied poses agree within 20 mm and
  10° (LOCAL) are one object and are refined jointly. `ClampPoseToFlat` snaps a cube within 8° (LOCAL) of resting
  flat onto its up axis.

## 6. The behaviours that run on it

**ReactToCubeMoved** (M10, transcribed then; now live). `CubeLocator` answers its four questions from BlockWorld:
located, distance to the robot, `IsVisibleFrom(camera now, 0.785398)`, and `TurnTowardsPoseAction(pose, max π)`.
The M10 test suite runs unchanged against a fake locator; `VisionTests` runs the strategy against the real one
and shows it firing only when the located cube is out of the camera's view.

**AcknowledgeObject** (`ObjectPositionUpdated`, the shipped map). `ReactionTriggerStrategyPositionUpdate`'s
constructor (0x0061216E) stores 0.785398 rad, 80.0 mm (0x42A00000) and 600000 ms (0x927C0): an observed object is a
target when its observed pose is not `IsSameAs` the pose last reacted to within 80 mm / 45°, and the observation
is within 600000 ms of the last image. A 30.0 (0x41F00000) stored beside them was not traced to a use.
`BehaviorAcknowledgeObject` (0x00602FA4): max turn 45°, pan and tilt tolerance 5°, `ReactionAnimGroup`
AcknowledgeObject, `NumImagesToWaitFor` 2 (`acknowledgeObject.json`); `BeginIteration` → `TurnTowardsObjectAction`
→ `VisuallyVerifyObjectAction(id, 2)` → `TriggerLiftSafeAnimationAction` → `FinishIteration`. Its
`LookForStackedCubes` ghost-object search is DEFERRED; a failed turn or verification ends the iteration without
the animation (INFERRED); the verification timeout is LOCAL (2 s).

**The turn.** `TurnTowardsPoseAction::Init`: turn = atan2(y, x) of the target with respect to the robot; fail if
|turn| > max; head angle to look at the pose clamped to −25°..44.5° (0xBEDF66F3, 0x3F46D3F2). The body turn is
`SetBodyAngle` (0x39), packed by `MovementComponent::TurnInPlace` (0x00640898) as {angle f32, maxSpeed f32,
accel f32, tolerance f32, numHalfRevolutions u16, useShortestDirection u8, actionId u8} — NATIVE field order.
INFERRED and hardware-pending (item L): the angle is the absolute body angle. LOCAL_POLICY: 100°/s and 10 rad/s²;
the 2° tolerance (0x3D0EFA35) is the `TurnInPlaceAction` constructor's.

Inventory after M11: **69 of 178** implementable (19 M1–M7, 39 M9, 9 M10, 2 M11). The 34 cube behaviours that
pick up, put down, roll, stack or knock over are now filed as *requires cube manipulation (docking/lift/path,
M12)*: the cube is localisable, the drive-and-dock actions are not built. Face-named classes moved to vision (29),
the ten `RequestGameSimple` configs to *requires the app*.

## 7. Offline evidence

**What this evidence is and is not.** The synthetic frames are rendered from the same recovered library the
decoder consumes. That validates the decoder, the tables, the geometry, PnP and the world model's rules end to
end, and the LOCAL front end against ideal renderings. It does **not** validate the front end against real cube
imagery: the 28 fw2457 frames show a room with no cube and establish only that nothing is hallucinated.
**Positive detection and localisation of a real cube through Cozmo's optics is hardware-pending (item K)** and
is not upgraded by anything below.

* `VisionTests`: 34 tests. Library shape; every row decodes to its label; rotated quads; blank rejection; quad
  corners within 1 px; markers decoded at three sizes and places and two in one frame; no markers in a synthetic
  room; cube face normals; camera pose at head angles 0 and 20°; PnP recovers a cube pose to < 1 mm / 0.5°;
  distortion round trip; calibration struct and chunked NV results; `SetBodyAngle` byte layout; world model:
  located on sight, unconnected dropped, forgotten after two misses, not marked when out of view, checks skipped
  while moving or rotating, Dirty on `ObjectMoved`, two faces → one object; the real locator under the
  cube-moved strategy; `ObjectPositionUpdated` firing rules; AcknowledgeObject's turn → two images → reaction
  and its unlocated-target path; the 28 fw2457 room frames decode with no cube marker found.
* `vision --synthetic`: five cubes at 90–300 mm and up to 46° yaw, rendered from the library into 320×240 frames:
  pose errors 0.2–0.7 mm and ≤ 0.5° up to 200 mm; 3.4 mm / 3.9° at 300 mm from a single 24 px marker.
  20–95 ms per frame in a Debug build.
* `vision --replay hw_fw2457_probe.log`: 28 real frames, 0 markers (the room has none). Note those frames carry
  `FrameTimestamp` 0; such frames are paired with the latest robot state (LOCAL fallback).

## 8. Out of scope, and why

**Faces and pets.** `VisionSystem::Update` moves `TrackedFace`, `TrackedPet` and `UpdatedFaceID` lists beside the
marker list, but the detection behind them is Omron's OKAO library statically linked into the engine
(194 `OKAO_*` symbols in the exports; `Vision::FaceRecognizer` wraps them). There is no Anki algorithm to transcribe and inventing a
detector would not be Cozmo's. The 29 face/pet behaviours stay *requires vision/person detection* with that
reason recorded in the inventory. Motion detection, overhead edges, laser points and tool codes share the
update loop and are likewise not built.

## 9. Hardware pending

`HARDWARE_TEST_PLAN.md` items **K** (camera calibration read from NV and cube localisation live), **L**
(`SetBodyAngle` semantics), **M** (the two reactions under the manager with a real cube). None of them gates
further development.

## 10. Next

**M12: cube manipulation** — `DriveToObjectAction`, the dock actions and the lift, which is what the 34 manipulation
behaviours need now that the cube has a pose. Start from `DriveToObjectAction::Init` / `InitHelper` and
`IDockAction::Init` in the binary (both exported), and from the path planner they hand poses to.
