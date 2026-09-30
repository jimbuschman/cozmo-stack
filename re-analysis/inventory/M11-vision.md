# M11-vision inventory (markers, camera geometry and BlockWorld)

**State:** approved by the manager on 2026-09-27 under the operator's standing authorisation, and frozen with `python re-analysis/tools/fidelity.py --approve M11-vision`. Every record is IMPLEMENTATION_GAP (to compare or build) unless a row says otherwise. The records marked EXACT_SOURCE or EQUIVALENT_IMPLEMENTATION before this process were never checked against the source; they are re-cited here and set to IMPLEMENTATION_GAP.

## Where this comes from

- **Source:** `libcozmoEngine.so` 3.4.0-1204, sha256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Unity C# (authority 2) and the shipped `vision_config.json` (authority 3) were used as supporting evidence only.
- **Read-only extractor passes:**
  - **X1 pass** (Appendix A): the production path V1..V24, the four RECOVERABLE_GAP records (M11-021, M11-023, M11-027, M11-029) settled, and the existing-record judgements.
  - **Gap pass 1** (Appendix B), G1..G6: the live ecvcs extractor; the `MarkerDetector` post-processing; the sub-pixel refinement (partial); M11-003/M11-004 addresses; the shipped `vision_config.json`.
  - **Gap pass 2** (Appendix C), G7..G10: the two X1 rows that failed verification re-cited; the M11-005 numerics finished (design vector, bilinear read, `DotDivide`, Cholesky); M11-003 cube/marker size and face poses; M11-004 connected-object rule, motion gates and object-match thresholds.
  - **Gap pass 3** (Appendix D), H1..H6: the connected-object drop resolved (warn + 10 s cooldown, not drop); the `WasMoving` predicate (`IS_MOVING` status bit); the object-match base extent; `WasHeadRotatingTooFast`/`WasBodyRotatingTooFast`; the `Block` size getter; the M11-005 `converged` polarity.
- **Verification:** `@cozmo-verifier` opened every X1 row V1..V24 in the `.so`. Three citation defects were found and fixed by gap pass 2 G7, and none was kept: V14's call target (`0x00892EF8` is `blx 0x4D0F28`, not `0x4D0E60`) and the location of the quad-acceptance parameters (`Parameters+0x30/+0x34/+0x38`, not literals at `0x00892B18`); and V18's operand (`0x0089FEA6` is `blx 0x4D169C`, the `RefineQuadrilateral` PLT, body `0x008C55E0`).
- **Interfaces used and not redone:**
  - `M3-device.md`: the camera image hand-off producer, the calibration install, the NV CameraCalib read.
  - `M2-protocol.md`: the protocol/status bits (`IS_MOVING` etc.).
  - `M10-derived.md`: the position-update strategy base and its 80 mm / 45 deg / 600000 ms thresholds.
  - `M14`: faces, pets and the face album.
- **The C# was never evidence.**

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M11-001 | IMPLEMENTATION_GAP | The marker type table and the nearest-neighbour library. **Table:** u8[598][1024] at `0x00C9BCA8`; labels at `0x00DC6CA8`; labelToCode `0x00DC7154`; cornerReorder `0x00DC73AC`; orientationDeg `0x00DC7D0C`. **Extract:** `VisionMarker::Extract` `0x008A0078`. Compare `MarkerLibrary.cs` against these and build what is missing. | X1 V17, §3 |
| M11-002 | IMPLEMENTATION_GAP | The decoder. **GetProbeValues** `0x0089EF40`: 5 samples at centre+offset, the sign-branch round on both axes `0x0089F0DA..0x0089F12E`, the integer mean `0x0089F158` (`__aeabi_uidiv`). **GetNearestNeighbor** `0x008C0938`: `normalize NORM_MINMAX` `0x008C09D2`, absdiff+sum `0x008C0AD6`/`0x008C0B50`, distance `0x008C0B58`, the 1.25x ambiguity reject `0x008C0C7E`, integer average `0x008C0C70` (`__aeabi_idiv`). **Extract** `0x008A0078`: label!=-1, distance<threshold, label not 149/150, then labelToCode `0x008A0130` / cornerReorder `0x008A0158` / orientationDeg `0x008A0180`. | X1 V17 |
| M11-003 | IMPLEMENTATION_GAP | Camera pose, cube size, marker size, face poses and codes. `_kDefaultHeadCamRotation` is a `RotationMatrix3d` in `.bss` at `0x01059CF8`, initialised by the static ctor `0x004D6DA4..0x004D6DB6` from the 9 floats at `0x00C4A854` = `[0,-0.0698,0.9976,-1,0,0,0,-0.9976,-0.0698]`, read in `Robot::Robot` at `0x0050FFAE/0x0050FFB0`. `GetCameraPose` `0x00510FFC` copies `[this+0x2d8]`, negates the angle, builds `RotationVector3d(Radians, Y_AXIS_3D())` and applies `RotateBy`. `Block::LookupBlockInfo` `0x004E4C8C` is a four-entry map (keys 1..4 LIGHTCUBE1/2/3/GHOST, ORANGE/YELLOW/RED/WHITE), each `(44,44,44)`. Marker size `25.0` in every `BlockFaceDef_t` at `+8` (tables `0xC45C40`/`0xC45CA0`/`0xC45D00`/`0xC45D60`); `KnownMarker` stores it at `+0x10` and `Get3dCorners` scales the canonical corners by it. `KnownMarker::_canonicalCorners3d` is a `Quadrilateral<3,float>` in `.bss` at `0x0105E0F0`, initialised by the static ctor `0x004DD7D8..0x004DD81A` with `(-0.5,0,-0.5)`, `(-0.5,0,0.5)`, `(0.5,0,0.5)`, `(0.5,0,-0.5)`. `Block::AddFace` `0x004E53BC` is a `tbb` on FaceName with six cases (face0 `0x004E5442` -pi/2, face1 `0x004E557C` +pi, face2 `0x004E54EA` +pi/2, face3 `0x004E5534` 0, face4 `0x004E5490` 2pi/3, face5 `0x004E55D6` 2pi/3; translations +-22); the size getter is `Block+0x88` (vtable `+0x18` = `0x004E382C`). Face-to-code map: LIGHTCUBE1 6,7,4,8,9,5; LIGHTCUBE2 12,13,10,14,15,11; LIGHTCUBE3 18,19,16,20,21,17; GHOST all 39. | X1 §3, gap1 G4, gap2 G9, gap3 H3/H5 |
| M11-004 | IMPLEMENTATION_GAP | The BlockWorld connected-object rule, the moving and rotating gates, and the position-update thresholds. **Thresholds:** 45 deg (`0x3F490FDB` to `[this+0x2c]`, ctor `0x00612182`), 80 mm (`0x42A00000` to `[this+0x38]`, `0x006121A2`), 600000 ms (`0x000927C0` to `[this+0x34]`, `0x00612198`); used as `IsSameAs` tolerances at `0x006124C6..0x006124F2` and the stale check at `0x00612592..0x006125A6`. **Connected-object rule:** `AddAndUpdateObjects` `0x00620AD4`, when the observed object's connected counterpart is absent, warns ("Observed active object of type %s but it's not connected...") and records a 10 s cooldown in an `unordered_map<int,float>` (`0x00620E9E..0x00620EDA`), then continues to `0x00620EDE`; it does **not** drop the object. The stack returns null. **Moving gate:** `CheckForUnobservedObjects` `0x00621C6C` skips the pass when `MovementComponent::WasMoving(timestamp) != 0` (`0x00621C88..0x00621C94`); `WasMoving` is the `IS_MOVING` bit (`HistRobotState+0x58 & 1`, lambda `0x00642672`). **Rotating gate:** `WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0)` (`0x00621C9A..0x00621CAE`); head uses `rateY`, body uses `rateZ`, each `|rate| > threshold`, no IMU bracket => true. **Object match:** `FindLocatedClosestMatchingObjectHelper`/`IsSameAs` with translation tolerance `0.8 * object extent` (`Block+0x88`, `(44,44,44)` => 35.2 mm; thunk `0x004E025C`, factor `0.8` at `0x004E028C`) and rotation tolerance 45 deg (thunk `0x004E0290`). **Correction C-R1:** FindLocatedObjectHelper's result is the last passing object (or the first with returnFirstOnly); IsSameAs ignores its angle tolerance (passes pi). | X1 §3, gap1 G5, gap2 G10, gap3 H1..H4 |
| M11-005 | IMPLEMENTATION_GAP | The sub-pixel corner refinement. **Orchestration (`RefineCorners` `0x0089FD98`):** `ComputeBrightDarkValues` first (validity 2 on failure); `threshold = (u8)((Bright+Dark)*0.5)`; `RefineQuadrilateral` (`0x0089FEA6 blx 0x4D169C`, body `0x008C55E0`); on success re-round to s16 and re-run `IsQuadrilateralReasonable`, restoring the original corners if invalid. **Numerics:** sigma = n/64 (0.015625); a Gaussian kernel and the derivative `[-0.5,0,0.5]` via `filter2D`; circular convolution over the boundary normalised to unit tangents; kmeans K=4, `TermCriteria(COUNT|EPS,15,0.1)`, attempts 1; per cluster a least-squares line (`solve`, `DECOMP_NORMAL`); the four intersections; `ComputeClockwiseCorners`; s16 round half away from zero. **RefineQuadrilateral:** `ceil(n*0.125)` samples per block x 8; each sample projected through the current homography and read bilinearly at `(floor,ceil)` with weights `(1-fracY)*[(1-fracX)*p00 + fracX*p01] + fracY*[(1-fracX)*p10 + fracX*p11]`; the residual is `(value - (bright+dark)/2)/255`; the 8-vector is `Tx*x, Tx*y, Tx, Ty*x, Ty*y, Ty, -(Tx*x*x+Ty*x*y), -(Tx*x*y+Ty*y*y)`; `A = J'J` upper triangle, `MakeSymmetric(false)` mirrors it; `SolveLeastSquaresWithCholesky` `0x0088DE68` in natural pivot order, `pivot < FLT_EPSILON` (1.1920929e-07) sets the bool& out-flag TRUE and returns 0 (the caller treats flag!=0 as the accept path: `0x008C6418 cmp r0,#1`), otherwise `1/sqrt(pivot)`; the update `U = [[s0+1,s1,s2],[s3,s4+1,s5],[s6,s7,1]]`, `Invert3x3`, `H = H*inv(U)`; renormalise by H22 when `|H22-1| >= 1e-5`; the stopping test uses the max corner move from `0x008C66C4`. The stack uses a different solver; SD1 requires the engine's exactly. | X1 V18/V19, gap1 G3, gap2 G8, gap3 H6 |
| M11-006 | IMPLEMENTATION_GAP | The clustering tolerances 5.0 mm / 0.0872665 rad (5 deg) and the flat-snap angle 0.349066 (20 deg). `CreateObjectsFromMarkers` `0x0062539C`; `ClampPoseToFlat` `0x00877330`. | X1 §3 (confirmed) |
| M11-007 | IMPLEMENTATION_GAP | Two misses before a pose is forgotten and two sightings before it is confirmed (`ObjectPoseConfirmer::MarkObjectUnobserved` `0x00506FBC`, confirming side `0x00506A04`). **Correction C-R2:** the caller writes no PoseState; Known/Dirty is decided by UpdatePoseInInstance at confirmation; two misses delete the object and its stack (MarkObjectUnknown writes no state). **Correction C-R12:** MarkObjectUnknown walks UPWARD only (never underneath); CanBeUsedForLocalization is readable; a Dirty cube stays Dirty without M11-044. | X1 §3 (confirmed) |
| M11-008 | IMPLEMENTATION_GAP | The minimum projected marker size for "should have been seen" is 40 px (`0x42200000` at `0x006220F0`) in `CheckForUnobservedObjects`. | X1 §3 (confirmed) |
| M11-009 | IMPLEMENTATION_GAP | A cube that reports movement is marked Dirty unless the robot is carrying it (`HandleActiveObjectMoved` `0x00533E30`, guard `0x00534116`). | X1 §3 (confirmed) |
| M11-010 | IMPLEMENTATION_GAP | Occlusion, the nine visibility reasons and the two ways an object is forgotten. The occluder list is cleared at the top of the frame (`0x00624F98`) and filled by `AddAndUpdateObjects`; the frame sequence itself is M11-037. | X1 §3, X1 V22 |
| M11-011 | IMPLEMENTATION_GAP | The NV CameraCalib read request: tag `0x80000001`, length 1, READ, zero second byte; replies assemble by index. This is the M3 NV interface; the engine-side NV records are M3's. | X1 V24 |
| M11-012 | COMPATIBILITY_POLICY | The nominal camera calibration stand-in; only with `--nominal`, never on the live path. **Correction C-R3:** provenance and evidence corrected: the engine's calibration gate is VisionSystem +0x58/+0x64 (0x006B4D66..0x006B4FFC); several tool paths use Nominal() unconditionally. | existing |
| M11-013 | IMPLEMENTATION_GAP | `AllowUnconnectedObjects`, an offline-tools switch, off on the live path. **Correction C-R4:** the record is no policy: the engine has no AllowUnconnectedObjects switch, keeps an unconnected cube after two matching sightings with the 10 s warning, and takes its ObjectID from the match else SetID() (a process-wide counter from 0, once per unique type). **Correction C-R14:** the warning clock is BaseStationTimer::GetCurrentTimeInSeconds; the non-unique branches' tolerance and filter details are read. | existing |
| M11-014 | IMPLEMENTATION_GAP | `SetBodyAngle` carries an absolute body angle, and the fields that follow say how it was asked for (`TurnInPlaceAction::Init` `0x00545FA0`; `+0x9C`, `+0xA4`, `+0xC0`; `numHalfRevolutions`). | X1 §3 (confirmed) |
| M11-015 | IMPLEMENTATION_GAP | The body turn speed 5.23599 rad/s, acceleration 10.0, tolerance 2 deg and revolution bound 25 (ctor `0x005459D4`). | X1 §3 (confirmed) |
| M11-016 | BLOCKED_EXTERNAL | Face, pet and motion detection: Omron OKAO, 177 proprietary exports; no Anki algorithm exists to transcribe. | X1 §3 |
| M11-017 | IMPLEMENTATION_GAP | The memory map's overhead edges: `OverheadEdgesDetector::Detect` `0x006ABE34`, `GroundPlaneROI` `0x004F7774`, and the four `MapComponent` entry points (`0x0067F7AC`, `0x0067F814`, `0x0067E6B0`, `0x0067E50C`). The stack's detector is an equivalent (polygon map, double-precision filter); SD1 requires the engine's exactly. **Correction C-R5:** the clear region is the ClampQuad quad, the 6.00001 compare is on a squared length, and the final run of a chain is dropped when it breaks the run (row 38); the map effect rests on the new quad-tree records M11-045..M11-048. | X1 §3 (consistent); SD1 |
| M11-018 | IMPLEMENTATION_GAP | The dark mask and the quad acceptance test. `IsQuadrilateralReasonable` `0x00892B18` is called at `0x00892EF8` (`blx 0x4D0F28`); its parameters are `MarkerDetector::Parameters+0x30` (25), `+0x34` (512, 8.8), `+0x38` (2), written at `0x00875338`/`0x00875340` and read on the live path at `0x00898E00..0x00898E2C`. The stack's dark mask is the binomial path; the live extractor is the ecvcs variant (M11-032). | X1 V11/V14, verifier, gap2 G7 |
| M11-019 | IMPLEMENTATION_GAP | A new pose origin in RobotState delocalizes: located objects are forgotten, carried objects move across (`Robot::Delocalize` `0x00510A24`; carried objects `0x00510CF0`). | X1 §3 (confirmed) |
| M11-020 | IMPLEMENTATION_GAP | Illumination normalisation of each marker's region before refinement and decode: the region grown by 5, `boxFilter` CV_16S, `subtract`, `normalize NORM_MINMAX 0..255`, put back after (`0x008990CE..0x0089954E`). The stack writes out OpenCV's arithmetic; SD1 requires the shipped OpenCV routines exactly. | X1 V16; SD1 |
| M11-021 | IMPLEMENTATION_GAP | The per-frame marker-mode gate. `ShouldProcessVisionMode(mode)` = `(1<<mode) & [VisionSystem+0xac]` AND the front schedule's `CheckTimeToProcessAndAdvance(mode)` (`0x006B5AA4`, `0x006AF1F8`); `InitDefaultSchedules` fills all 16 modes with `{true}` and counter 0 (`0x006AEFDE`); `ApplyCLAHE(image, 4)` before the marker mode (`0x006B50EE`); the call `0x006B5162..0x006B5182`. `vision_config.json` enables DetectingMarkers with no schedule. **Correction C-R6:** the 16S body is not reused; ColumnSum<int,short> is 0x000AAB44 with another rounding helper; the non-divisible CLAHE branch and ColumnSum<int,uchar> are transcribed. | X1 V4..V7, gap1 G6 |
| M11-022 | IMPLEMENTATION_GAP | The marker-detector entry and post-processing. `Detect` `0x008753B6` (`ResetBuffers` `0x008753C8`, `Array<u8>` wrap `0x008753F0`); the ROI/negative choice from `[camera+0x7c]` (`0x00875402..0x00875470`); `CopyTo`/`GetROI`/`FillWith` `0x0087556E..0x008755AA`; `GetNegative` `0x0087561A`; the `VisionMarker` slot init `0x00875690..0x008756E2`; `DetectFiducialMarkers` `0x008757B2` (body `0x00898760`); the `ObservedMarker` build `0x00875546..0x0087555C` (ctor `0x0087E1FC`). | X1 V8, gap1 G2 |
| M11-023 | IMPLEMENTATION_GAP | The front-end orchestration order per quad: homography (`0x00898E98`); `params+1` gates illumination normalisation (`0x008990BE`, region `0x008990CE`, `boxFilter` `0x008993D2`, `subtract` `0x0089942C`, `normalize` `0x00899474`); the exactly-4-corner check (`0x008994CE`); `RefineCorners` (`0x00899528`) and the copy-back (`0x0089954E`); decode (`params+0x69` `0x00899032`, ratio `0x0089903A`, threshold `0x32` `0x00899054`, `Extract` `0x00899056`). | X1 V16, §2 |
| M11-024 | IMPLEMENTATION_GAP | The component-extraction filter selector: `params[0]` byte0=1 (`0x008752FC`/`0x00875304`) -> the live ecvcs variant `0x0088F8BC` (call `0x00898BE0`); zero -> the binomial `0x00890448` (`0x00898C18`). | X1 V9, §3 |
| M11-025 | IMPLEMENTATION_GAP | The characteristic-scale select. The select loop `0x00890B14` is inside the non-live binomial extractor; the live path uses the ecvcs window bank `{4,8,16}` (M11-032). | X1 §3 |
| M11-026 | IMPLEMENTATION_GAP | The connected-component filters: compress segment ids (`0x00898C90..0x00898D50`), size filter (`0x00898CAA`, operands `+0x14`/`+0x18`), solid/sparse (`0x00898D0A`, `+0x1c`=32000/`+0x20`=64), hollow (`0x00898D6A`, `+0x24`=1.0), sort by id (`0x00898DD0`). | X1 V11 |
| M11-027 | IMPLEMENTATION_GAP | The exterior boundary trace `TraceNextExteriorBoundary` `0x008C6B18`: a 10000-point `FixedLengthList` (`0x00892DA8 movw 0x2710`); requires segments sorted by id (`0x008C6B5C`); four extent arrays and four staircase passes (`0x008C6FF0..0x008C72CC`); sentinels `0x7fff`/`0x8000` (`0x008C6FA6`/`0x008C6FB2`); translate by `(ip,r4)` (`0x008C72D6`/`0x008C72E4`). It is on the live path: called at `0x00892E0A` from `ComputeQuadrilateralsFromConnectedComponents`, called at `0x00898E2C` in `DetectFiducialMarkers`. | X1 V12, §2; verifier |
| M11-028 | IMPLEMENTATION_GAP | The quad construction and acceptance: `ComputeQuadrilateralsFromConnectedComponents` `0x00892D70`; method switch `0x00892E24`; the 0,3,1,2 order `0x00892E66..`; `IsQuadrilateralReasonable` at `0x00892EF8` (`blx 0x4D0F28`); swap `0x00892F04`; append `0x00892F62`. | X1 V14, gap2 G7 |
| M11-029 | IMPLEMENTATION_GAP | `ExtractLineFitsPeaks` and the corner fit: sigma = n/64 (`0x008A5E90`; 0.015625); `getGaussianKernel` `0x008A5F26`; derivative `0x008A602C`/`0x008A6038`; the circular convolution `0x008A613E..0x008A628A` with the wrap `0x008A6166` and the unit-tangent normalisation `0x008A6248`/`0x008A6266`; `kmeans` `0x008A6490` (K=4, type 3, 15, 0.1, attempts 1); the cos-25-deg reject (literal `0x3F6803C9` at `0x8A61C0`, `0x008A64B4..0x008A65E2`); the least-squares fit `0x008A6714` (swapped flag, `(coord,1)` rows, `solve` `0x008A68E0`); exactly four intersections `0x008A6BE0`; `ComputeClockwiseCorners` `0x008A1324`; the s16 round `0x008A6C1E/4A/7C`. | X1 V13, §2 |
| M11-030 | IMPLEMENTATION_GAP | `ComputeClockwiseCorners` `0x008A1324` and the s16 round half away from zero (`0x008A6C1E`/`0x008A6C4A`/`0x008A6C7C`). | X1 V13 |
| M11-031 | IMPLEMENTATION_GAP | The contrast gate `ComputeBrightDarkValues` `0x0089F8E8`: the Bright/interior mean and the Dark/border mean normalised, the bool `Bright > ratio*Dark` with ratio 1.01 (`0x3F8147AE` at `0x0087538E`, read at `params+0x3c`); compare `0x0089FD10..0x0089FD30`. | X1 V19, gap1 G3 |
| M11-032 | IMPLEMENTATION_GAP | The live component extractor is the ecvcs integral-image variant. Selector byte 1 (`0x008752FC`/`0x00875304`) -> `ExtractComponentsViaCharacteristicScale` `0x0088F8BC`; window bank `{4,8,16}` (`0x00898B5A..0x00898B9E`); `ScrollingIntegralImage_u8_s32` (ctor `0x008A4D34`, `FilterRow` `0x0088F538`); the callback `numFilters3` `0x0088F61A` (`a = |f1-f0| > |f2-f1| ? f1 : f2`; `mask = ((a * params+0x0c) >> 16) > pixel`, and the multiplier `0xCCCC` **is live** at `0x00898BAA` -> `0x0088F662 mul` / `0x0088F66A asrs #0x10`); `ecvcs_filterRows` `0x0088F4D0`; `ScrollDown` `0x008A4DB0`; `get_maxRow` `0x008A51E0`; the per-row DP `Extract2dComponents_PerRow_*` (`0x00893BAA` thunk, `Extract1dComponents` `0x00896FDC`, `NextRow` `0x00893F96`). | X1 V9/V10, gap1 G1 |
| M11-033 | IMPLEMENTATION_GAP | The image hand-off and the two `VisionSystem::Update` overloads. `VisionComponent::SetNextImage` `0x00652B04` puts an `EncodedImage` in the component; the `Processor` thread `0x00651F08` calls `UpdateVisionSystem(pose, image)` `0x00653D30`; `VisionSystem::Update(PoseData, EncodedImage)` `0x006B4B68` checks `IsColor` (`0x006B4B7C`), `DecodeImageRGB` `0x006B4B90` or `DecodeImageGray` `0x006B4C02`, resets the `ImageCache` and calls `Update(PoseData, ImageCache)` `0x006B4C78`; that requires pose and image (`0x006B4D66`/`0x006B4D70`), calls `UpdatePoseData` `0x006B4D88` and `GetGray` `0x006B4D94`. The M3 producer side is the M3 interface. **Correction C-R7:** the RGB2GRAY kernel and FillGray's timestamp copy are read; the NotReady gate tests VisionSystem +0x58/+0x64, not pose or image. | X1 V1..V3 |
| M11-034 | IMPLEMENTATION_GAP | The vision-mode schedule mechanism. `ShouldProcessVisionMode` `0x006B5AA4` (the mode enable bitmask at `VisionSystem+0xac` AND the front schedule's `CheckTimeToProcessAndAdvance`); `CheckTimeToProcessAndAdvance` `0x006AF1F8` (a bool vector at `schedule+0` and a wrapping counter at `schedule+0xc`); `InitDefaultSchedules` `0x006AEFDE` (all 16 modes `{true}`, counter 0); `sDefaultSchedules` `0x0105C540`. | X1 V6/V7 |
| M11-035 | IMPLEMENTATION_GAP | `VisionComponent::UpdateAllResults` `0x006542EC` runs the per-mode handlers in order (markers `0x006544A2`, faces `0x00654510`, pets `0x0065457E`, motion `0x006545E8`, overhead edges `0x00654652`, tool code `0x006546BC`, computed calibration `0x00654726`, image quality `0x00654790`, laser points `0x006547FA`), then `CheckMailbox` `0x00654A74`, then broadcasts a `RobotProcessedImage` (`0x00654A14`/`0x00654A1C`). **Correction C-R8:** the dispatch is gated on the VisionMode bits; five handler bodies remain unread (M11-048). | X1 V20 |
| M11-036 | IMPLEMENTATION_GAP | `VisionComponent::UpdateVisionMarkers` `0x00654D60` calls `BlockWorld::UpdateObservedMarkers(list<ObservedMarker>)` `0x00654DF4`. | X1 V21 |
| M11-037 | IMPLEMENTATION_GAP | `BlockWorld::UpdateObservedMarkers` `0x00624EE8`: clears the occluders (`0x00624F98`), adds the lift occluder (`0x00624FA4`), creates objects from markers (`0x00624FC4`), runs `CheckForUnobservedObjects` (`0x0062504E`), `AddAndUpdateObjects` (`0x0062505A`), `UpdatePoseOfStackedObjects` (`0x006250D0`), `BlockConfigurationManager::Update` (`0x0062520C`) and `UpdateMarkerlessObjects` (`0x0062521A`). **Correction C-R9:** the occluder float is a translation length; the empty-list branch, the r7 gate, CheckForUnobservedObjects, UpdatePoseOfStackedObjects (M11, not M10/M12) and UpdateMarkerlessObjects are read. **Correction C-R13:** GetLastVisuallyMatchedTime is [entry+0x30]; the pads, the +0x58 store, GetLastImageTimeStamp and Robot::GetPose() are read. | X1 V22 |
| M11-038 | IMPLEMENTATION_GAP | The BlockWorld game-broadcast entry points: `BroadcastObjectObservation` `0x0061FED8`, `BroadcastLocatedObjectStates` `0x0061E6C0`, `BroadcastConnectedObjects` `0x0061E91C`; `VisionSystem::CheckMailbox` `0x006B2AD4`. The individual message layouts belong to M2/M10. **Correction C-R10:** BroadcastLocatedObjectStates, BroadcastConnectedObjects, CheckMailbox and BroadcastObjectObservation are read. | X1 V23 |
| M11-039 | IMPLEMENTATION_GAP | The calibration-install path. `VisionSystem::UpdateCameraCalibration` `0x006B1E3E` installs the calibration (`Camera::SetCalibration` `0x006B1E5E`) and on success calls `MarkerDetector::Init` (`0x006B1E78`), which calls `Parameters::Initialize` `0x008752E4`/`0x008752E6`. This is the M3 calibration interface. | X1 V24 |
| M11-040 | IMPLEMENTATION_GAP | The `VisionComponent` mailbox/thread interface: `SetNextImage` `0x00652B04`, `Processor` `0x00651F08`. The per-frame bound is M3's. **Correction C-R11:** the engine's mailbox is one pending slot, latest wins, discard on completion; the enabled flag is M11-049. | X1 N9 |
| M11-041 | IMPLEMENTATION_GAP | BlockWorld::AddConnectedActiveObject and its caller HandleActiveObjectConnectionState. libcozmoEngine.so 3.4.0-1204 (re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q4a, verified in re-analysis/research/20260929-R-VIS-verify-M11-gap1.md): AddConnectedActiveObject 0x0062302C: activeID >= 5 (signed compare, 0x00623040) warns and returns -1; the same slot occupied with the same factoryID and type returns the existing ID, else a ConflictingActiveID error and RemoveConnectedActiveObject; a connected object with the same factoryID only logs; CreateActiveObjectByType null returns -1; then it searches located objects of that type in all origins: with the same activeID it takes the first (same factoryID: new.ID = that ID; factoryID 0: adopt and update activeID/factoryID on every located object of the type; a different nonzero factoryID: MismatchedFactoryID error, DeleteLocatedObjects for that ID, fresh SetID); with none of that activeID: none of the type -> fresh SetID, otherwise every such object is marked Dirty (MarkObjectDirty(o,false)) and updated (activeID -1: set both; factoryID equal: set activeID; else set both if factoryID != 0) and new.ID = the first one's ID; the new object is registered in m_connectedObjects with no pose or PoseState. The caller HandleActiveObjectConnectionState 0x00533B3C (range test unsigned, cmp r7,#4 bhi at 0x00533B58) logs Connected (0x00533BF4) and calls Robot::HandleConnectedToObject(activeID, factoryID, type) at 0x00533C34 after an ID other than -1 | R-VIS gap 1/2 |
| M11-042 | RECOVERABLE_GAP | Unread bodies next to the BlockWorld observation path. libcozmoEngine.so 3.4.0-1204: CreateActiveObjectByType (0x00623296 call); the Clone bodies of Charger and the non-active blocks; the virtual SetID users CreateFixedCustomObject and AddMarkerlessObject (vtable +0x24 by pattern only); ObservableObject vtable +0x58; HandleActiveObjectConnectionState error branch 0x00533CB8; RemoveConnectedActiveObject; Robot::HandleConnectedToObject; the full MarkObjectUnknown body 0x00507128 (stack-walk order, message fields); IsVisibleFrom (camera at VisionComponent+0x24, 0.7854 rad, 40.0); the names of the MovementComponent bytes +0xA/+0xC and Robot+0x280 byte +4; the SetCameraParams contents sent at 0x00658414; the BlockConfigurationManager container slot-0 functions (Stack, PyramidBase, Pyramid), PruneFullPyramids and NotifyBroadcaster; the RobotDelocalized emitter (0x00510D88) | R-VIS gap 1/2 |
| M11-043 | IMPLEMENTATION_GAP | BlockWorld::UpdateObjectOrigins: re-keying located objects between origins. libcozmoEngine.so 3.4.0-1204 (re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q1, verified in re-analysis/research/20260929-R-VIS-verify-M11-gap1.md): Robot::LocalizeToObject 0x005154B0 calls UpdateObjectOrigins 0x00620534 (its only call site 0x00515992) when the object's origin differs from the robot's world origin, after Rejigger. UpdateObjectOrigins clears the ObjectPoseConfirmer (0x00620594), runs a modify function (0x00628E18) on every object in the old origin that looks up a counterpart in the new origin (same ID for unique types, closest for others), sets the counterpart's ID to the moved object's ID (re-keying the shared_ptr if the IDs differ) and calls CopyWithNewPose 0x00506EF8 (copies the pose and the source's PoseState); with no counterpart it clones the object, sets the clone's ID and adds it with AddLocatedObject; the second modify function (0x00629862) is AddInExistingPose (0x00506F48, writes only a confirmer entry); the old origin's map entry is erased only if the last object's result was 0; then BlockConfigurationManager [+0xC] = 1 (0x006206EA) and BroadcastLocatedObjectStates (0x006206E6). LocalizeRobot 0x0050D1CC and UseDiscardedObservation 0x0050CD90 write no object state | R-VIS gap 1/2 |
| M11-044 | IMPLEMENTATION_GAP | PotentialObjectsForLocalizingTo::Insert. libcozmoEngine.so 3.4.0-1204: PotentialObjectsForLocalizingTo::Insert 0x0050CE00 decides what reaches UseDiscardedObservation: a distance gate, the this+0x10 gate and a CouldUseObjectForLocalization gate (0x0050CE64..0x0050CE8E), motion gates (Robot+0x254 bytes +0xA/+0xC, WasCameraMoving) returning 0 (0x0050CE92..0x0050CEB0), a stationary-robot pose comparison and a one-entry-per-root keep-nearest rule (0x0050CEB4..0x0050D120). Extracted in re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q1 but not row-checked by the verifier **Correction C-R15:** the missing path is a live REGRESSION (poses freeze, Dirty stays Dirty); it must be built. **Correction C-R16:** read row by row in `20260929-R-VIS-M11-gap3-insert.md` (P0..P11, A1..A8): the Insert discard/motion/stationary gates, the per-root pair map, UseDiscardedObservation and LocalizeRobot are recorded in the record's authority; status IMPLEMENTATION_GAP (the missing path freezes confirmed poses). | R-VIS gap 1/2 |
| M11-045 | IMPLEMENTATION_GAP | The memory map's quad tree: Insert, Expand, override rules, subdivision and merging. libcozmoEngine.so 3.4.0-1204 (re-analysis/research/20260929-R-VIS-M11-gap2-extraction.md rows 1.x, verified in re-analysis/research/20260929-R-VIS-verify-M11-gap2.md): MemoryMap::Insert 0x006817DE calls QuadTree::Insert(FastPolygon, MemoryMapData, int) 0x00685008 with the int argument 1 = the maximum ShiftRoot attempts after root growth (0x00685110, 0x00685220, 0x006853B2); the root starts at centre (0,0,1.0), side 160.0, level 4 (0x00684C28..0x00684C48), grows up to level 8 (UpgradeRootLevel; quadrant map x>=,y>= Q3; x>=,y< Q2; x<,y< Q0; x<,y>= Q1, 0x006878D2..0x0068791E), and level 0 is 10.0 mm precision (0x00685000, no subdivision below 0x00686D78). CanOverrideSelfWithContent 0x00687E78: newType 8: always; current type 8: only when newType is 2 and overlap is 2; newType 10: only over 9; newType 9: over 0, 1, 2, 5, 6, 9; newType 1 (ClearOfObstacle): mask 0x698 = {3,4,7,9,10} needs full overlap (2), current type 2 is never overridden, everything else yes; newType 5: only over 4; all others yes. Insert_Recursive subdivides partly covered cells above level 0 to 10 mm, TrySet/ForceSet write (ForceSet rewrites a removal type to ClearOfObstacle with both stamps = the new +0xC), and TryAutoMerge folds all FOUR children's stamps (min into +8, max into +0xC, seeded from child 3, 0x00687F56..0x0068805C) into child 0's content. Polygons are clockwise (ImportQuad2d) and FastPolygon::Contains accepts only clockwise polygons. Processor bookkeeping (areas, per-type node sets, InvalidateBorders) is read. An existing cell's timestamp (+0xC) is refreshed even when its type is not overridden | R-VIS gap 1/2 |
| M11-046 | IMPLEMENTATION_GAP | The quad tree's polygon Transform and the collision ray walk. libcozmoEngine.so 3.4.0-1204 (re-analysis/research/20260929-R-VIS-M11-gap2-extraction.md, verified in re-analysis/research/20260929-R-VIS-verify-M11-gap2.md): the polygon MemoryMap::TransformContent 0x0068147C (via 0x006814F4) calls QuadTreeNode::Transform 0x00688378 (PLT 0x4BCA8C), not QuadTreeProcessor::Transform 0x0068A414 (the whole-map overload): overlap 0 returns false; every child is transformed then TryAutoMerge runs; the function is called on the node's own content (skipped when the content pointer is null, 0x006883E2); a changed content goes through ForceSet (0x0068846E); every leaf that overlaps the polygon in any way is converted whole (no subdivision, no containment test, no override table); the ground-plane lambda (0x00680B54, identical to 0x00680C2C) turns type 9 into a type-0 object carrying the stamps. HasCollisionRayWithTypes 0x00689D80/0x00689F0A: a two-point polygon walks the tree; a zero flag with no children is false with no overlap test; a non-zero flag returns overlap != 0 without consulting children; a zero flag with children descends children 0..3 in order; an endpoint within 1e-5 of a cell counts as inside; a subdivided parent holds fresh Unknown (type 0) content, so a mask that includes type 0 returns true on any overlap without descending (0x00689F5C) | R-VIS gap 1/2 |
| M11-047 | RECOVERABLE_GAP | The quad tree's border walk (FindBorders, AddBorderWaypoint, FillBorder). libcozmoEngine.so 3.4.0-1204: the border cache key is type<<32|flags, a new entry is dirty (flag byte 1); any node type change or destruction marks every entry dirty; the waypoint is {from, to, dir}, from a type-9 node with a flagged neighbour, walked by a wall-follower; FillBorder inserts a one-point polygon at each de-duplicated from-node centre straight into QuadTreeNode::Insert (0x0068A206; no NaN gate, no Expand), so a level-0 seed becomes one type-10 cell and a merged block four type-10 cells around its centre. FindBorders iterates an unordered_set of node pointers, so the exact waypoint order depends on heap addresses and is not read; kDebugFindBorders (0x00C88160) is unreferenced (the pc-relative pairs at 0x00689AB8, 0x0068ABAC, 0x0068B1EE resolve to 0x00C88161, the empty piecewise_construct_t) | R-VIS gap 1/2 |
| M11-048 | RECOVERABLE_GAP | Unread quad-tree, OpenCV-numeric and vision-handler bodies. libcozmoEngine.so 3.4.0-1204 and the shipped OpenCV 3.1.0: QuadTree::ShiftRoot branch table, SwapChildrenAndContent 0x00687601, DestroyNodes; LineSegment::IntersectsWith (PLT 0x004BD080); AddSmallestDescendants 0x00688105 and ...DepthFirst 0x00688251 and the CheckedInfo bit indexing; the 3-vector behind AIWhiteboard +0x50's running maximum (0x0067FDE8..0x0067FE38); the uses of the constants 3.0 and 6.0 next to the 6.00001 literal; callers of MemoryMap::TransformContent(fn); OpenCV 3.1.0 filter2D 8U->16S (libopencv_imgproc.so 0x0005705D) and fillConvexPoly (0x00042F59, 0x00042C99), Mat::convertTo 0x000407E9, Mat::setTo 0x0004486D, transpose 0x0008ED6D (libopencv_core.so); kmLine2WithLineIntersection (PLT 0x004D3220); the UpdateAllResults loop head 0x006542EC..0x00654486 and the handler bodies UpdateToolCode 0x00655421, UpdateComputedCalibration 0x006554C5, UpdateImageQuality 0x00655579, UpdateLaserPoints 0x00655795, UpdateOverheadMap 0x00655909; the shipped PerformanceLogging.DropStatsWindowLength_sec value | R-VIS gap 1/2 |
| M11-049 | IMPLEMENTATION_GAP | The enabled flag: images are dropped until the NV calibration read callback has run. libcozmoEngine.so 3.4.0-1204 (re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q7, verified in re-analysis/research/20260929-R-VIS-verify-M11-gap1.md): VisionComponent+0x48 (enabled) has one writer, movs r0,#1; strb.w r0,[r5,#0x48] at 0x0065AE80, inside the NV read callback at 0x0065AB68 (tag NVEntry_CameraCalib 0x80000001), reached on every path: NV failure, size mismatch and success. The callback is registered by HandleMessage<RobotConnectionResponse> 0x006583A4 only when the response result byte is 0 (Success) (0x006583BA..0x006583FA); that handler also sends SetCameraParams with only the struct byte at +6 set (0x00658414; the other bytes are uninitialised, contents UNKNOWN). SetNextImage 0x00652B04 drops images ('not enabled') until the callback has run once. VisionComponent+0x4B (paused) has no writer (the constructor zeroes both bytes at 0x006500EE); it is only read (0x006517C8, 0x00651874, 0x00652218, 0x0065300E, 0x0065302C) | R-VIS gap 1/2 |
| M11-050 | IMPLEMENTATION_GAP | UpdateVisionMarkers steps between ComputeAndInsertStateAt and UpdateObservedMarkers (0x00654D92..0x006550A0): the 0x06000000 silent branch, IsPoseInWorldOrigin drop, the WasRotatingTooFast(0.5236, 0.1745) gate, GetHistoricalCamera and the per-marker drops and camera replacement. Cited in re-analysis/research/20260929-R-VIS-verify-M11-batchA4.md (verifier round 3; new from the M11 batch A round-3 verification). | X1 |
| M11-051 | IMPLEMENTATION_GAP | AddRawOdomState gates (0x00530CF8..0x00530EC4): stale drop, consecutive-gap reset, parent check, duplicate key; CullToWindowSize guards. Cited in re-analysis/research/20260929-R-VIS-verify-M11-batchA4.md. | X1 |
| M11-052 | RECOVERABLE_GAP | GetRawStateAt interpolation and HistRobotState::Interpolate (0x00531431..0x005315A6, 0x0053068C..0x00530848); the frame of tmp needs PoseBase::GetWithRespectTo. Cited in re-analysis/research/20260929-R-VIS-verify-M11-batchA4.md. | X1 |
| M11-053 | IMPLEMENTATION_GAP | Robot::Delocalize callees and the frame-id-mismatch Delocalize trigger (Robot+0x2C0 counter, 0x00512D7A..0x00512F96; Delocalize 0x00510A24..0x00510D98). Cited in re-analysis/research/20260929-R-VIS-verify-M11-batchA5.md. | X1 |

## Decisions (the manager's, recorded for audit)

- **SD1 (exact, always).** Three records that were EQUIVALENT_IMPLEMENTATION are set to IMPLEMENTATION_GAP because the engine's code ships and must be reproduced exactly, not approximated:
  - **M11-005** — the Cholesky sub-pixel refinement; the numerics are now fully read (gap pass 2 G8, gap pass 3 H6) and must be transcribed.
  - **M11-017** — the overhead-edge detector (polygon map, double-precision filter in the stack).
  - **M11-020** — the illumination normalisation (`boxFilter`/`subtract`/`normalize` written out rather than transcribed from the shipped OpenCV).
- **SD2.** No behaviour the engine leaves undefined was found on the M11 live path, so no forced policy was added. M11-012 and M11-013 remain COMPATIBILITY_POLICY (offline stand-ins), unchanged.
- **SD3.** No cross-layer work is parked as a gap. The dependencies are recorded as interfaces: the image hand-off producer and the calibration install and the NV read are M3's; the face/pet/motion detector is M14/M11-016 (BLOCKED_EXTERNAL); the position-update thresholds belong to M10 and are cited from there.
- **SD4.** No housekeeping change was needed.
- **Interfaces, not gaps.**
  - M11-011 (the NV CameraCalib read) and M11-039 (the calibration install) and M11-033 (the image hand-off) name M3's side; the engine-side production path is in this inventory.
  - M11-016 is BLOCKED_EXTERNAL: the Omron OKAO detector is third-party binary code.
- **M11-027 `live_path` corrected** from false to true: the exterior boundary trace is called from `ComputeQuadrilateralsFromConnectedComponents` `0x00892E0A`, which is called at `0x00898E2C` inside `DetectFiducialMarkers` (verifier finding, gap pass 2 G7.1).
- **M11-005 `converged` wording.** The Cholesky out-flag is set TRUE only on the `pivot < FLT_EPSILON` early-out, and `RefineQuadrilateral` treats flag!=0 as the accept path (`0x008C6418 cmp r0,#1`). The record must not describe that path as the failure path; the stack must reproduce the polarity exactly.

## Corrections (R-VIS gap pass 1 and 2, 2026-09-29)

Sources: the pre-extraction (`20260929-R-VIS-pre-extraction.md`, verified in six passes `20260929-R-VIS-verify-pre-*.md`), gap pass 1 (`20260929-R-VIS-M11-gap1-extraction.md`, verified in `20260929-R-VIS-verify-M11-gap1.md`) and gap pass 2, the quad tree (`20260929-R-VIS-M11-gap2-extraction.md`, verified in `20260929-R-VIS-verify-M11-gap2.md`). Where an extractor and a verifier disagree the verifier's reading is used.

- **C-R1..C-R11** are marked in the rows they change (M11-004, 007, 012, 013, 017, 021, 033, 035, 037, 038, 040).
- **M11-013 is no policy.** It contradicted the source (no switch exists) and is IMPLEMENTATION_GAP, live path.
- **New records M11-041..M11-049:** AddConnectedActiveObject (M11-041); unread bodies (M11-042); UpdateObjectOrigins (M11-043); PotentialObjectsForLocalizingTo::Insert, extracted but not verified (M11-044); the quad tree's Insert/Expand/override (M11-045), Transform and ray walk (M11-046), border walk (M11-047, RECOVERABLE_GAP), and unread numerics and handlers (M11-048, RECOVERABLE_GAP); the enabled flag / NV callback gate (M11-049).
- **Three extractor statements the verifier corrected:** TryAutoMerge folds all four children (not three); a NaN z does not fail Project3dPoint; kDebugFindBorders is unreferenced (the control was invalid). The extractor's `MarkObjectUnknown -> Invalid` was contradicted: it deletes.
- **M14-007** (approved in M14-faces) carried the claim that the ray answers 'exactly'; it is corrected in the manifest here and re-approved with M14-faces.

**Decisions (manager).** The engine's null-map dereference (M11-017 rows 48-49) is not reproduced; it is left for the policy review. Whether the border walk's address-dependent order must be reproduced is left to the operator (M11-047).

## Corrections (R-VIS M11 batch-A verification, 2026-09-29)

Source: `20260929-R-VIS-verify-M11-batchA.md` (verdict FAIL; the core ObjectID, confirmation, UpdatePoseInInstance, mailbox and enable-gate rows were reproduced from the binary).

- **C-R12 (M11-007):** MarkObjectUnknown loops upward only with the 15.0 on-top search; CanBeUsedForLocalization 0x004E49AC is readable.
- **C-R13 (M11-037):** GetLastVisuallyMatchedTime, the IsVisibleFrom pads, the +0x58 store, Robot::GetLastImageTimeStamp and Robot::GetPose() are read.
- **C-R14 (M11-013):** the warning uses BaseStationTimer::GetCurrentTimeInSeconds.
- **C-R15 (M11-044):** building PotentialObjectsForLocalizingTo::Insert / UseDiscardedObservation is required to remove a live-path regression (a confirmed cube's pose refresh).

- **C-R16 (M11-044):** the rules are read (`20260929-R-VIS-M11-gap3-insert.md`); M11-044 is IMPLEMENTATION_GAP and must be built to remove a live-path regression.

## Existing record evidence found contradicted or too weak

- **M11-032:** its `unresolved` clause "the dark-mask multiplier 0xCCCC belongs to the non-live path" is **contradicted**. The live path reads `params+0x0c` (`0x00898BAA` -> `0x00898BDA` -> `0x0088F662 mul` / `0x0088F66A asrs #0x10`), so 0xCCCC is applied by both paths.
- **M11-018:** its evidence located the quad-acceptance parameters at `0x00892B18`; they are `MarkerDetector::Parameters+0x30/+0x34/+0x38`, written at `0x00875338`/`0x00875340` and read at `0x00898E00..0x00898E2C`. The values are right; the location was wrong.
- **M11-001 / M11-002 / M11-003 / M11-004:** their evidence was a tool path, bare symbol names, or prose. Re-cited here with addresses (M11-001/002 from X1 §3 and V17; M11-003 from gap 1 G4 and gap 2 G9 and gap 3 H3/H5; M11-004 from gap 1 G5 and gap 2 G10 and gap 3 H1..H4).
- **M11-004:** its connected-object rule is **contradicted**: the native `AddAndUpdateObjects` warns and applies a 10 s cooldown and continues; the stack's `AddAndUpdateObject` returns null. Its `WasMoving` gate is now known to be the `IS_MOVING` status bit, not a motion computation.
- **M11-005:** its `converged` polarity was inverted from the mechanical behaviour (see Decisions).
- **M11-010:** its evidence proved the visibility reasons, the occluder list and the two forget conditions, but not the `UpdateObservedMarkers` frame sequence; that is now M11-037.
- **M11-027:** `live_path` was false; the trace is live.
## Appendix A: X1 pass extraction report (20260927-X1-M11-extraction.md)

# X1 — M11-vision extraction (read-only)

Job: re-analysis/jobs/X1.md. Subsystem: M11-vision, the whole production vision path.
Agent: opencode (DeepSeek), window 3. Date: 2026-09-27.

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204).
The vision region is Thumb-2; disassembled with capstone `CS_MODE_THUMB`.

## 0. Method, and what this clone could not check

- The Ghidra decompilation `re-analysis/decomp/libcozmoEngine/` is not present in this clone
  (`index.tsv` absent, `decomp/libcozmoEngine` empty). It is gitignored. I worked from the `.so`
  with capstone directly (scratch `vdis.py`). Addresses below are file VAs and are checkable.
- **`re-analysis/obb/` is empty in this clone.** The shipped `vision_config.json` (the
  `DetectingMarkers` flag and the absence of an initial marker schedule) could not be re-opened.
  The config-dependent part of M11-021 therefore rests on the earlier evidence file
  `re-analysis/evidence/m11/marker-frontend-2.md` (authority 6) plus the engine's own default
  schedule, which I did read (authority 1). This is called out on the row.
- Earlier M11 evidence (`re-analysis/evidence/m11/ecvcs-extractor.md`,
  `marker-frontend.md`, `marker-frontend-2.md`) is treated as a lead and spot-checked against the
  `.so`, never copied. Where a row reuses an earlier citation I re-read the instruction.

## 1. Production path

Entry: the M3/M4 camera image hand-off. Consumer: BlockWorld and the game broadcasts.

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| V1 | Image hand-off (M3 interface) | A decoded camera frame (an `EncodedImage`) is put into `VisionComponent` by `SetNextImage`; the component's `Processor` thread pops it and calls `UpdateVisionSystem(poseData, encodedImage)`. | `0x00652b04` (`VisionComponent::SetNextImage`); `0x00651f08` (`Processor`); `0x00653d30: blx 0x4ba464` (`VisionSystem::Update(PoseData, EncodedImage)`) | NEW | EXACT_SOURCE for the call; the M3 producer side is the M3 interface |
| V2 | Encode/decode to `ImageCache` | `VisionSystem::Update(PoseData, EncodedImage)` checks `EncodedImage::IsColor`; colour decodes to `ImageRGB` and resets the cache with it, otherwise it decodes grey to `Image` and resets with that; then calls `VisionSystem::Update(PoseData, ImageCache)`. | `0x006b4b7c: blx 0x4bf150` (`IsColor`); `0x006b4b90: blx 0x4bf15c` (`DecodeImageRGB`); `0x006b4c02: blx 0x4ba3d4` (`DecodeImageGray`); `0x006b4c78: blx 0x4bf180` (`Update(PoseData, ImageCache)`) | NEW | EXACT_SOURCE |
| V3 | Frame setup | `VisionSystem::Update(PoseData, ImageCache)` requires the pose data and image present, calls `UpdatePoseData`, fetches the grey image from the cache, then dispatches the enabled vision modes. | `0x006b4d66`/`0x006b4d70` guards; `0x006b4d88: blx 0x4bf18c` (`UpdatePoseData`); `0x006b4d94: blx 0x4be9f4` (`ImageCache::GetGray`) | NEW | EXACT_SOURCE |
| V4 | CLAHE | Before the marker mode, the grey image is CLAHE-equalised once: `ApplyCLAHE(image, MarkerDetectionCLAHE 4, out)`. | `0x006b50ee: movs r2,#4`; `0x006b50f2: blx 0x4bf1c8` (`ApplyCLAHE`) | M11-021 (partial) | EXACT_SOURCE |
| V5 | Per-frame marker-mode gate | `ShouldProcessVisionMode(1 /*DetectingMarkers*/)`; only if it returns 1 does `DetectMarkersWithCLAHE(image, claheImage, rects, 4)` run. | `0x006b5162: movs r1,#1`; `0x006b5164: blx 0x4bf1e0`; `0x006b5168: cmp r0,#1`; `0x006b516a: bne 0x6b51fe`; `0x006b5182: blx 0x4bf1f8` (`DetectMarkersWithCLAHE`) | M11-021 | EXACT_SOURCE |
| V6 | The gate itself | `ShouldProcessVisionMode(mode)` = the mode's enable bit in the VisionSystem's mode bitmask AND the current schedule's bit for that mode. The tail call is `AllVisionModesSchedule::CheckTimeToProcessAndAdvance(mode)` on the front schedule. | `0x006b5aa8: ldr.w r2,[r0,#0xac]`; `0x006b5aac: movs r3,#1`; `0x006b5aae: lsls r3,r1`; `0x006b5ab0: tst r2,r3`; `0x006b5ab2: beq 0x6b5b1e`; `0x006b5ab4: ldr.w r2,[r0,#0xd0]`; `0x006b5ab8: cbz r2,0x6b5aca`; `0x006b5ac6: b.w 0x8cdc9c` -> ARM veneer `0x8cdca0: ldr ip,[pc]; add pc,ip,pc` -> PLT `0x4bf264` (`AllVisionModesSchedule::CheckTimeToProcessAndAdvance`); body `0x006af1f8` (counter at schedule+0xc, bit vector at schedule+0, wrap when counter == count) | M11-021 (NEW detail) | EXACT_SOURCE |
| V7 | Default schedule | `AllVisionModesSchedule::InitDefaultSchedules` builds, for all 16 modes, a one-element bool vector `{true}` and a zero counter. A mode with the default schedule is therefore processed every frame. | `0x006aefde` function; `0x006aefec: movs r5,#0`; `0x006aeff2: strb.w r7,[sp,#0x13]` (r7=1); `0x006aeffe..0x006af014` (store per mode); counter zeroed `0x006af036`/`0x006af044`; `0x00c90770`-adjacent data `0x0105c540` (`sDefaultSchedules`) | NEW | EXACT_SOURCE |
| V8 | Detector entry | `DetectMarkersWithCLAHE` calls `MarkerDetector::Detect(Image, list<ObservedMarker>&)`; the detector resets its buffers, wraps the image as `Array<u8>` and calls `DetectFiducialMarkers`. | `0x006b4780` (`DetectMarkersWithCLAHE`); `0x008753b6` (`MarkerDetector::Detect`); `0x008753c8: blx` (`ResetBuffers`); `0x008753f0: blx` (`Array<u8>` ctor); `0x004cfcec` (`DetectFiducialMarkers`) | M11-022 | EXACT_SOURCE (entry/call); post-processing RECOVERABLE |
| V9 | Extractor selector | `DetectFiducialMarkers` reads `params[0]`; non-zero -> `ExtractComponentsViaCharacteristicScale` (ecvcs), zero -> `..._binomial`. `Parameters::Initialize` writes `strh 0x0101` at `params+0`, so byte0 = 1 and the ecvcs variant is live. | `0x00898b4c: ldrb r0,[r7]`; `0x00898b4e: cmp r0,#0`; `0x00898b50: beq 0x898c18`; live call `0x00898be0: blx 0x4d1360`; `0x008752fc: movw r2,#0x101`; `0x00875304: strh r2,[r0]` | M11-024, M11-032 | EXACT_SOURCE |
| V10 | Live extractor | The ecvcs integral-image extractor builds a window bank `{4,8,16}` (size = params+4+2), a `ScrollingIntegralImage_u8_s32` with `numBorderPixels = max(scale)+1`, N filtered row buffers, binarises each row with `ecvcs_computeBinaryImage_numFilters3` (multiplier params+0xc = 0xCCCC), and folds it through `ConnectedComponents::Extract2dComponents_PerRow_*`. | `0x00898b5a..0x00898b9e` (window bank); `0x00898baa: ldr r5,[r7,#0xc]`; `0x00898bda: mov r2,r5`; `0x00898be0`; `0x0088f8bc` (ecvcs); `0x0088f61a` (`numFilters3`); `0x0088f662: mul r3,r6,r2` / `0x0088f66a: asrs r3,r3,#0x10` / `0x0088f66c: cmp r3,r5` (the binarise) | M11-032 | EXACT_SOURCE (selector + multiplier); full ecvcs transcription is RECOVERABLE (evidence file) |
| V11 | Connected-component filters | Compress segment ids; size filter (`InvalidateSmallOrLargeComponents` with the inline-computed `params+0x14/+0x18`); solid/sparse (`params+0x1c`=32000, `+0x20`=64); hollow (`params+0x24`=1.0); sort by id. | `0x00898c90/0x00898cf0/0x00898d50` (compress); `0x00898caa: ldrd r5,r7,[r0,#0x14]`; `0x00898cbc: blx 0x4d1384`; `0x00898d0a: ldrd r5,r7,[r0,#0x1c]`; `0x00898d1c: blx 0x4d1390`; `0x00898d6a: ldr r5,[r0,#0x24]`; `0x00898d78: blx 0x4d139c`; `0x00898dd0` (sort) | M11-018, M11-026 | EXACT_SOURCE |
| V12 | Boundary trace | `ComputeQuadrilateralsFromConnectedComponents` calls `TraceNextExteriorBoundary` per component; it builds four s16 extent arrays over the component bbox and emits the staircase contour in four passes, then translates every point. It refuses a component whose extent arrays still hold the `0x7fff`/`0x8000` sentinels. | `0x00892d70`; `0x00892da8: movw r1,#0x2710` (10000-point list); `0x008c6b18`; `0x008c6b5c: blx 0x4d0eec` (`get_isSortedInId`); sentinels `0x008c6fa6`/`0x008c6fb2`; passes `0x008c6ff0..0x008c709a`, `0x008c709c..0x008c7158`, `0x008c7158..0x008c7208`, `0x008c7208..0x008c72cc`; translate `0x008c72d6`/`0x008c72e4` | M11-027 | EXACT_SOURCE |
| V13 | Corner extraction | `CornerMethod` params+0x28 = 1, so `ExtractLineFitsPeaks` runs: `sigma = n/64` (0.015625), Gaussian kernel from `getGaussianKernel`, derivative `[-0.5,0,0.5]` via `filter2D`, circular convolution over the boundary normalised to unit tangents; k-means K=4 with `KMEANS_USE_INITIAL_LABELS`, `TermCriteria(COUNT|EPS,15,0.1)`, attempts 1; per cluster a least-squares line (`cv::solve`, `DECOMP_NORMAL`); the four line intersections, kept only if inside the image and exactly four survive; `ComputeClockwiseCorners` sorts by `atan2f`; the s16 round clamps then rounds half away from zero. | `0x008a5e90: vmul.f32 s18,s0,s2`; `0x008a5f26: blx` (`getGaussianKernel`); `0x008a602c`/`0x008a6038` (derivative); `0x008a613e..0x008a628a` (circular convolution; `0x008a6166: addlt r3,sl` wrap; `0x008a6266: vdiv.f32 s0,s16,s0`); `0x008a6490: blx 0x4d18d0` (`kmeans`); `0x008a64b4: vldr s18,[pc,#-0x2f8]` -> `0x8a61c0 = 0x3f6803c9` (0.906308 = cos 25 deg), reject `0x008a65e2`; `0x008a6714` (`swapped`), `0x008a68e0: blx 0x4bea90` (`solve`); `0x008a6be0` (exactly four); `0x008a1324` (`ComputeClockwiseCorners`); `0x008a6c1e/0x008a6c4a/0x008a6c7c` (s16 round) | M11-029, M11-030 | EXACT_SOURCE |
| V14 | Quad acceptance | `IsQuadrilateralReasonable` (minQuadArea 25, symmetry 512 in 8.8, minDistanceFromEdge 2) accepts the quad; the 0,3,1,2 order and the swap flag. | `0x00892b18`; `0x00892e66..0x00892ec4` (order); `0x00892ef8: blx 0x4d0e60`; `0x00892f04` (swap); `0x00892f62` (append) | M11-018, M11-028 | EXACT_SOURCE |
| V15 | Homography | `Transformations::ComputeHomographyFromQuad`. | `0x00898e98` | M11-023 | EXACT_SOURCE (call site) |
| V16 | Per-marker loop: normalise -> refine -> decode | For each quad, if `params+1` (1) the bounding rectangle is grown by 5 px, cropped, `boxFilter` (CV_16S), `subtract` the original, `cv::normalize(...,NORM_MINMAX,0..255)`; then the 4-corner check, `VisionMarker::RefineCorners` (illumination-normalised image), the saved region copied back, and `VisionMarker::Extract(image, threshold 0x32, ratio 1.01)`. Order is normalise, refine, decode. | `0x008990be: ldrb r0,[r0,#1]`; `0x008990c4: beq 0x8994ce`; `0x008990ce: blx 0x4d08ec` (`ComputeBoundingRectangle`); `0x00899132: blx 0x4ba89c` (`cv::Mat(Mat,Rect)`); `0x008993d2: blx 0x4beaf0` (`boxFilter`); `0x0089942c: blx 0x4d13f0` (`subtract`); `0x00899474: blx 0x4d13fc` (`normalize`); `0x008994ce: ldr r6,[sp,#0x74]` / `0x008994d0: ldr r0,[r6,#0x28]!` / `0x008994d4: cmp r0,#4` / `0x008994d6: bne 0x89957e`; `0x00899528: blx 0x4d1408` (`RefineCorners`); `0x0089954e: blx 0x4bf2f4` (`copyTo`); `0x0089959e`/`0x008995a2` (back to decode); `0x00899032: ldrb.w r0,[r0,#0x69]`; `0x0089903a: vldr s22,[r0,#0x3c]`; `0x00899054: movs r2,#0x32`; `0x00899056: blx 0x4d13e4` (`Extract`) | M11-023, M11-020 | EXACT_SOURCE |
| V17 | Decode | `VisionMarker::Extract` builds/reads the nearest-neighbour library (598 images x 1024), `GetProbeValues` (5 samples per probe, sign-branch rounding both axes, integer mean), `GetNearestNeighbor` (`normalize NORM_MINMAX`, absdiff+sum, distance, the 1.25x ambiguity reject with integer average), then `labelToCode`, `cornerReorder`, `orientationDeg`; rejects labels 149/150 and distance >= threshold. | `0x008a0078` (`Extract`); `0x0089ed1c` (`GetNearestNeighborLibrary`); `0x0089ef40` (`GetProbeValues`); `0x0089f0da..0x0089f12e` (rounding); `0x0089f158: blx __aeabi_uidiv`; `0x008c0938` (`GetNearestNeighbor`); `0x008c09d2` (`normalize`); `0x008c0c70: blx __aeabi_idiv`; `0x008c0c7e: vmov.f32 s2,#1.25`; `0x008a0130` (`labelToCode`); `0x008a0158` (cornerReorder); `0x008a0180` (orientationDeg) | M11-001, M11-002 | EXACT_SOURCE |
| V18 | Refinement orchestration | `RefineCorners` calls `ComputeBrightDarkValues`; a false bool is status 2 (low contrast) and return; `threshold = (u8)((Bright+Dark)*0.5)`; `RefineQuadrilateral` (Cholesky least-squares on the sub-pixel edge samples); on success re-round to s16 and re-run `IsQuadrilateralReasonable`, restoring the original corners if invalid. | `0x0089fd98`; `0x0089fe00: blx` (`ComputeBrightDarkValues`); `0x0089feca` (status 2); `0x0089fe56/0x0089fe5a/0x0089fe5e/0x0089fe66` (threshold); `0x0089fea6: blx 0x008c55e0`; `0x0089ffca` (re-check); `0x0089ffd0..0x0089ffea` (restore) | M11-005, M11-031 | EXACT_SOURCE (orchestration); refinement numerics EQUIVALENT |
| V19 | Contrast gate direction | `ComputeBrightDarkValues` returns `Bright/interior_mean > 1.01 * Dark/border_mean` (ratio params+0x3c). | `0x0089fd10: vldr s4,[r7,#-0x78]`; `0x0089fd14: vmul.f32 s4,s2,s4`; `0x0089fd26: vcmpe.f32 s0,s4`; `0x0089fd2e/0x0089fd30: it gt; movgt r0,#1`; ratio `0x0087538e` (0x3F8147AE) / `0x008753a2` | M11-031 | EXACT_SOURCE |
| V20 | Consumer: VisionComponent | `VisionComponent::UpdateAllResults` runs the per-mode result handlers in a fixed order (markers, faces, pets, motion, overhead edges, tool code, computed calibration, image quality, laser points), then `CheckMailbox`, then broadcasts a `RobotProcessedImage`. | `0x006542ec`; `0x006544a2: blx 0x4ba47c` (`UpdateVisionMarkers`); `0x00654510` (`UpdateFaces`); `0x0065457e` (`PetWorld::Update`); `0x006545e8` (`UpdateMotionCentroid`); `0x00654652` (`UpdateOverheadEdges`); `0x006546bc` (`UpdateToolCode`); `0x00654726` (`UpdateComputedCalibration`); `0x00654790` (`UpdateImageQuality`); `0x006547fa` (`UpdateLaserPoints`); `0x00654a74: blx 0x4ba518` (`CheckMailbox`); `0x00654a14`/`0x00654a1c` (RobotProcessedImage broadcast) | NEW | EXACT_SOURCE |
| V21 | Markers into BlockWorld | `VisionComponent::UpdateVisionMarkers` calls `BlockWorld::UpdateObservedMarkers(list<ObservedMarker>)`. | `0x00654d60`; `0x00654df4: blx 0x4ba5a8` (`BlockWorld::UpdateObservedMarkers`) | NEW | EXACT_SOURCE |
| V22 | BlockWorld frame update | `BlockWorld::UpdateObservedMarkers` clears the occluders, adds the lift occluder, builds objects from markers, runs `CheckForUnobservedObjects`, `AddAndUpdateObjects`, `UpdatePoseOfStackedObjects`, the block-configuration manager, and `UpdateMarkerlessObjects`. | `0x00624ee8`; `0x00624f98: blx 0x4b8124` (`Camera::ClearOccluders`); `0x00624fa4` (`AddLiftOccluder`); `0x00624fc4: blx 0x4b813c` (`CreateObjectsFromMarkers`); `0x0062504e: blx 0x4b8148` (`CheckForUnobservedObjects`); `0x0062505a: blx 0x4b8154` (`AddAndUpdateObjects`); `0x006250d0: blx 0x4b816c` (`UpdatePoseOfStackedObjects`); `0x0062520c` (`BlockConfigurationManager::Update`); `0x0062521a: blx 0x4b8184` (`UpdateMarkerlessObjects`) | M11-010 (partial) | EXACT_SOURCE |
| V23 | Game broadcasts | `BlockWorld::BroadcastObjectObservation`, `BroadcastLocatedObjectStates`, `BroadcastConnectedObjects`, and the vision mailbox produce the engine-to-game messages. | `0x0061fed8` (`BroadcastObjectObservation`); `0x0061e6c0` (`BroadcastLocatedObjectStates`); `0x0061e91c` (`BroadcastConnectedObjects`); `0x006b2ad4` (`VisionSystem::CheckMailbox`) | NEW | EXACT_SOURCE (entry points); the individual message layouts belong to M2/M10 |
| V24 | Calibration install (M3 interface) | `VisionSystem::UpdateCameraCalibration` installs the calibration (`Camera::SetCalibration`) and, on success, calls `MarkerDetector::Init(width, height)`, which calls `Parameters::Initialize`. | `0x006b1e3e`; `0x006b1e5e: blx` (`SetCalibration`); `0x006b1e78: blx 0x4bef58` (`MarkerDetector::Init`); `0x008752e4/0x008752e6` (`Parameters::Initialize`) | M11-011 (interface) | EXACT_SOURCE |

## 2. The four RECOVERABLE_GAP records, settled

These four were the job's explicit requirement. Each is now read; the integrator should set them
EXACT_SOURCE (the manifest is outside this job's write scope, so they are settled here).

### M11-021 — the per-frame marker-mode gate (RECOVERABLE_GAP -> EXACT_SOURCE)

- The gate is `VisionSystem::ShouldProcessVisionMode(mode)` (0x006b5aa4). It tests
  `(1 << mode) & [VisionSystem+0xac]`; on a miss it returns 0 (`0x006b5ab2: beq 0x6b5b1e`, and
  0x006b5b1e returns 0). On a hit, if the front schedule list is empty (`0x006b5ab8: cbz r2,0x6b5aca`)
  it returns 0, otherwise it tail-calls `AllVisionModesSchedule::CheckTimeToProcessAndAdvance(mode)`
  on the front schedule (`0x006b5ac6: b.w 0x8cdc9c`, ARM veneer to PLT `0x4bf264`).
- `CheckTimeToProcessAndAdvance` (0x006af1f8) reads the schedule's bool-vector word and advances a
  wrapping counter (schedule+0xc); it returns the current bit.
- `AllVisionModesSchedule::InitDefaultSchedules` (0x006aefde) fills all 16 mode schedules with a
  single `true` and counter 0, so an enabled mode with the default schedule runs every frame.
- The marker call: `0x006b5162: movs r1,#1; 0x006b5164: blx 0x4bf1e0; 0x006b5168: cmp r0,#1;
  0x006b516a: bne 0x6b51fe; ... 0x006b5182: blx 0x4bf1f8 (DetectMarkersWithCLAHE)`.
- The shipped `vision_config.json` enables `DetectingMarkers` with no initial schedule
  (`re-analysis/evidence/m11/marker-frontend-2.md`, S17, from the earlier OBB extraction). This
  clone's `re-analysis/obb/` is empty, so that one input is authority 6, not re-checked here; the
  engine-side gate and default schedule are authority 1.
- The record's `unresolved` ("the managed fixed subset of marker modes, whether any mode is
  scheduled") is answered for the engine: no marker schedule is installed, and the default is
  always-true. The managed (`VisionComponent`) throttle is an M3/M11 interface; see open question 1.

### M11-023 — the front-end orchestration order (RECOVERABLE_GAP -> EXACT_SOURCE)

`DetectFiducialMarkers` second loop, per quad candidate:
1. homography (`0x00898e98`);
2. `params[1]` (byte at +1, shipped 1) gates illumination normalisation:
   bounding rectangle grown by 5 (`0x008990ce ComputeBoundingRectangle`, `0x008990da/0x008990e8` and
   `0x008990f8/0x00899102`), `cv::Mat(Mat,Rect)` view (`0x00899132`), `boxFilter` to CV_16S
   (`0x008993d2`), `subtract` the original (`0x0089942c`), `cv::normalize NORM_MINMAX 0..255`
   (`0x00899474`);
3. exactly-4-corner check (`0x008994ce`/`0x008994d0`/`0x008994d4`);
4. `RefineCorners` (`0x00899528`) and the copy-back of the saved region (`0x0089954e`);
5. decode: `params+0x69` gate (`0x00899032`), ratio `params+0x3c` (`0x0089903a`), threshold 0x32
   (`0x00899054`), `VisionMarker::Extract` (`0x00899056`).
The record's `unresolved` ("the per-marker second loop details are not read") is answered.

### M11-027 — the exterior boundary trace (RECOVERABLE_GAP -> EXACT_SOURCE)

`TraceNextExteriorBoundary` (0x008c6b18): the contour list is a 10000-point `FixedLengthList<Point<s16>>`
(`0x00892da8: movw r1,#0x2710`); it requires the component's segments sorted by id
(`0x008c6b5c: blx 0x4d0eec`). It builds four s16 extent arrays over the bbox, then emits the contour
in four passes, each walking consecutive extents and appending a staircase run via
`strh [data+count*4]` / `strh [data+count*4+2]` while bumping `count = [out+0xc]`:
pass 1 `0x008c6ff0..0x008c709a`, pass 2 `0x008c709c..0x008c7158`, pass 3 `0x008c7158..0x008c7208`,
pass 4 `0x008c7208..0x008c72cc`. It fails (returns 1) if any extent sentinel `0x7fff`/`0x8000`
remains (`0x008c6fa6`/`0x008c6fb2`, then `0x008c6fc4`). After the passes it translates every emitted
point by `(ip, r4)` (`0x008c72d6`/`0x008c72e4`).
The record's `unresolved` ("the four-pass body 0x008C6FCA..0x008C72E8 is not transcribed") is
answered for the body structure. The pass-to-side (top/right/bottom/left) naming remains
interpretive; it is not needed to reproduce the body.

### M11-029 — ExtractLineFitsPeaks and the corner fit (RECOVERABLE_GAP -> EXACT_SOURCE)

- Circular convolution `0x008a613e..0x008a628a`: outer index i = 0..n-1 (`sb = -n/2`), inner k
  accumulates the kernel array `[sp+0x98]` against the two boundary-component arrays; a negative
  index wraps by `+n` (`0x008a615c: cmp r7,sl` / `0x008a6162: cmp r7,#0` / `0x008a6166: addlt r3,sl`);
  the two double sums are `sqrt`-normalised to a unit tangent (`0x008a6248: vsqrt.f32`;
  `0x008a6266: vdiv.f32 s0,s16,s0`; `0x008a626e`/`0x008a627a: vmul`; stored back).
- `kmeans` call `0x008a6490` with K=4, `TermCriteria` type 3 (COUNT|EPS), maxCount 15, eps 0.1,
  attempts 1, flags 1 (`0x008a646a`, `0x008a6476`/`0x008a647e`, `0x008a6484`).
- Cluster scatter reject: `0x008a64b4: vldr s18,[pc,#-0x2f8]` -> word at `0x8a61c0 = 0x3f6803c9`
  (0.906308 = cos 25 deg); `0x008a651e: blx 0x4d18e8` (`Mat::dot`); `0x008a65e2: vcmpe.f32 s0,s18`;
  reject label = -1 (`0x008a65ea`/`0x008a65ec`/`0x008a65f2`).
- Least-squares fit `0x008a6714` onward: the `swapped` flag picks the independent coordinate
  (`0x008a6714`/`0x008a671a`), the design matrix rows are `(coord, 1.0)`, `cv::solve(A,b,x,1)`
  (`0x008a68e0`), solution `[a b]` to the line record.
- Exactly four intersections must survive (`0x008a6be0`), then `ComputeClockwiseCorners`
  (0x008a1324) and the s16 round (0x008a6c1e/0x008a6c4a/0x008a6c7c).
The record's `unresolved` (the circular-convolution loop and the fit body) is answered.

## 3. Existing records judged

**Confirmed against the source (keep status):**

- M11-005 (EQUIVALENT_IMPLEMENTATION) — orchestration confirmed; the numerical refinement is a
  different solver, so EQUIVALENT stays honest. Its `unresolved` is empty but the Cholesky
  sub-pixel equations are still not transcribed; see open question 3.
- M11-006, M11-007, M11-008, M11-009, M11-010, M11-011, M11-014, M11-015, M11-019, M11-020,
  M11-024, M11-026, M11-028, M11-030, M11-031 — the cited instructions exist and support the
  claims. M11-024 and M11-026 in particular are correct in the current revision (the earlier
  second-pass warning about `+0x74/+0x70` was against an older text; the manifest's M11-026 now
  cites `+0x1c/+0x20/+0x24`, which is right).
- M11-016 (BLOCKED_EXTERNAL) — the OKAO detector is third-party; nothing to recover.
- M11-017 (EQUIVALENT_IMPLEMENTATION) — the overhead-edge detector; consistent.

**Evidence too weak for the stated status (the checker cannot see this; the manager must):**

- **M11-001** (EXACT_SOURCE). `evidence` is a tool path
  (`re-analysis/tools/extract_marker_library.py records the addresses and the check`), not a
  citation. The library layout is real and checkable (`0x00C9BCA8` u8[598][1024], `0x00DC6CA8`
  labels, `0x00DC7154` labelToCode, `0x00DC73AC` cornerReorder, `0x00DC7D0C` orientationDeg), but
  the record as written does not carry its own evidence. Replace the evidence with those addresses.
- **M11-002** (EXACT_SOURCE). `evidence` is three bare symbol names (`GetProbeValues 0x0089EF40`,
  `GetNearestNeighbor 0x008C0938`, `VisionMarker::Extract 0x008A0078`) plus data addresses; the
  instruction-level details that the record claims (sign-branch rounding, the 1.25x reject with
  integer average) are not cited. The algorithm is confirmed by this pass, but the evidence must be
  replaced with the instructions (`0x0089f0da..0x0089f12e`, `0x008c0c7e`, `0x008c0c70`).
- **M11-003** (EXACT_SOURCE). `evidence` is bare symbols (`Robot::Robot _kDefaultHeadCamRotation`,
  `GetCameraPose`, `Block::LookupBlockInfo`, `Block::AddFace`, `KnownMarker::_canonicalCorners3d`).
  No address. Needs the address of each constant/function.
- **M11-004** (EXACT_SOURCE). `evidence` is prose ("80 mm, 45 degrees, 600000 ms thresholds in the
  position-update strategy") with no address. Needs the position-update-strategy instructions.
- **M11-022** (IMPLEMENTATION_GAP). `evidence` is bare addresses with no instruction detail
  (`Detect 0x008753B8`, `DetectFiducialMarkers 0x004CFCEC`, the post-processing addresses). It is a
  gap record, so nothing is overclaimed, but the post-processing is still only located, not read.
- **M11-025** (IMPLEMENTATION_GAP). Consistent with the source: the cited select loop `0x00890B14`
  is inside the non-live binomial function; the live path is the ecvcs variant. No change needed.

**Contradicted by the source:**

- **M11-032** (IMPLEMENTATION_GAP). Its `unresolved` reads, verbatim, "...the dark-mask multiplier
  0xCCCC belongs to the non-live path". The second clause is contradicted: the live path reads the
  same `params+0x0c` and uses it as the binarise multiplier. Citation:
  `0x00898baa: ldr r5,[r7,#0xc]` -> `0x00898bda: mov r2,r5` -> the callback's first int ->
  `0x0088f662: mul r3,r6,r2` / `0x0088f66a: asrs r3,r3,#0x10` in
  `ecvcs_computeBinaryImage_numFilters3`. `0xCCCC` belongs to both paths, applied to a
  differently-derived value. (M11-018's line that the binomial dark mask is `BinomialFilter` +
  `(scale*0xCCCC)>>16` is fine for the binomial path and must not be read as "0xCCCC is non-live".)

**Partial evidence (an existing record whose evidence proves only part of its claim):**

- M11-010's evidence proves the nine visibility reasons, the occluder list and the two forget
  conditions, but not the `UpdateObservedMarkers` frame sequence; the sequence is now V22 (NEW).
- M11-018's evidence proves the dark mask and the quad test for the binomial variant, but the live
  extractor is ecvcs (M11-032); M11-018 must not be read as covering the live connected components.

## 4. NEW steps (no existing record)

- N1 (V1/V2/V3): the image hand-off and the two `VisionSystem::Update` overloads, including the
  colour/grey decode into the `ImageCache`. `0x00653d30`, `0x006b4b68`, `0x006b4c78`, `0x006b4d5c`.
- N2 (V6/V7): the mode-schedule mechanism — `ShouldProcessVisionMode` 0x006b5aa4,
  `CheckTimeToProcessAndAdvance` 0x006af1f8, `InitDefaultSchedules` 0x006aefde, `sDefaultSchedules`
  0x0105c540.
- N3 (V8): `MarkerDetector::Detect`'s buffer reset and the `Array<u8>` wrap before
  `DetectFiducialMarkers`. 0x008753b6.
- N4 (V20): the `VisionComponent::UpdateAllResults` handler order and the `CheckMailbox` and
  `RobotProcessedImage` broadcast. 0x006542ec.
- N5 (V21): `VisionComponent::UpdateVisionMarkers` -> `BlockWorld::UpdateObservedMarkers`.
  0x00654d60 / 0x00654df4.
- N6 (V22): the `BlockWorld::UpdateObservedMarkers` frame sequence (occluder clear, lift occluder,
  object creation, unobserved check, add/update, stacked poses, block configs, markerless).
  0x00624ee8.
- N7 (V23): the BlockWorld game-broadcast entry points. 0x0061fed8, 0x0061e6c0, 0x0061e91c.
- N8 (V24): the calibration-install path into `MarkerDetector::Init`. 0x006b1e3e.
- N9: the `VisionComponent` mailbox/thread interface (`SetNextImage` 0x00652b04, `Processor`
  0x00651f08) — named as an interface only; the per-frame bound is M3's.

## 5. Open questions for the manager / integrator

1. **VisionComponent per-frame bound (M3/M11 interface).** The engine's `VisionComponent` is a
   thread with a mailbox; whether it throttles markers per frame is outside M11. It must be settled
   when M3 is re-inventoried, or recorded as an interface. Not read here.
2. **OBB config not present.** `re-analysis/obb/` is empty in this clone, so the shipped
   `vision_config.json` (`DetectingMarkers: true`, no initial schedule) is authority 6 from the
   earlier pass. If the integrator needs it as primary evidence, extract the OBB in the clone that
   approves M11.
3. **RefineQuadrilateral numerics.** M11-005 stays EQUIVALENT; the full sub-pixel equations
   (`0x008c55e0`, sampling loops `0x008c5e80..0x008c61e0`, helpers `0x008c635c`/`0x008c644e` to
   `0x008c66c4`) are still not transcribed. This is the one remaining numerical EQUIVALENT in M11.
4. **The ecvcs transcription.** The live extractor's full body (integral image `ScrollDown`
   `0x008a4db0..0x008a4fd8`, `get_maxRow` 0x008a51e0, the per-row DP) is transcribed in
   `re-analysis/evidence/m11/ecvcs-extractor.md` but not re-derived in this pass. M11-032's
   `unresolved` must be corrected (0xCCCC is live) and its evidence replaced with instructions.
5. **M11-001/002/003/004 evidence** is too weak to keep EXACT_SOURCE as written; the manager should
   re-cite them from the addresses in section 3, or downgrade them until re-cited.

*Read-only extraction. Nothing outside `.scratch/X1/` and this report file was changed.*


## Appendix B: gap pass 1 extraction report (20260927-I-M11-gap1-extraction.md)

# I-M11 gap pass 1 — extraction (read-only)

Job: gap pass 1 for the M11-vision integration job (I-M11). Scope: close G1..G6 of
`re-analysis/research/20260927-X1-M11-extraction.md` section 5 and the X1 "too weak"
evidence rows.
Agent: opencode (DeepSeek), window 3. Date: 2026-09-27.
Scratch: `.scratch/I-M11-gap1/` (Thumb disassembler `vdis.py`, raw dumps).

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204).
The vision region is Thumb-2; disassembled with capstone `CS_MODE_THUMB` (base 0). All
addresses below are file VAs. The Ghidra decompilation was not used (this clone's
`re-analysis/decomp/libcozmoEngine/` has `index.tsv` but no per-function files).

Authority-6 leads checked against the `.so`:
- `re-analysis/evidence/m11/ecvcs-extractor.md` — checked instruction by instruction; see G1.15.
- `re-analysis/evidence/m11/marker-frontend.md` — checked for the rows this pass re-read; the
  front-end call graph and constants it names are real, but its A1/A5b/D3 items are superseded
  by the X1 pass and by G2 below. It is not used as evidence here.

Row form: `| # | what the original does | citation | record | class |`.
class = EXACT_SOURCE, RECOVERABLE_GAP (with exactly what to read) or UNKNOWN.

---

## G1. The live component extractor (M11-032 / M11-018 / M11-025)

The shipped selector byte is 1, so the live function is
`ExtractComponentsViaCharacteristicScale` at `0x0088F8BC` (the integral-image / box-filter
variant), not `..._binomial` at `0x00890448`. The evidence file
`re-analysis/evidence/m11/ecvcs-extractor.md` is **confirmed instruction for instruction**,
except for the divergences listed in G1.15. This pass additionally resolves the file's open
question Q1 (the vertical SII / ScrollDown bookkeeping).

### G1.1 Caller: window bank and arguments (`DetectFiducialMarkers`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.1a | Selector: `params[0]`; non-zero -> the live function. `Parameters::Initialize` writes `strh 0x0101` at `params+0`, so byte0 = 1. | `0x00898B4C: ldrb r0,[r7]`; `0x00898B4E: cmp r0,#0`; `0x00898B50: beq #0x898c18`; live call `0x00898BE0: blx` PLT `0x4D1360`; `0x008752FC: movw r2,#0x101`; `0x00875304: strh r2,[r0]` | M11-032, M11-024 | EXACT_SOURCE |
| G1.1b | Window bank: `FixedLengthList<int>(N=params+4+2, MS, Buffer)`, then `list[i] = (params+8) << i` for i = 0..(params+4+1). Shipped `params+4 = 1`, `params+8 = 4` -> `{4,8,16}`, size 3. | `0x00898B5A: ldr r6,[r7,#4]`; `0x00898B62: adds r1,r6,#2`; `0x00898B6A: blx` PLT `0x4D05D4`; loop `0x00898B8C: ldr r1,[r7,#8]` / `0x00898B90: lsls r1,r0` / `0x00898B92: str.w r1,[r2,r0,lsl#2]` / `0x00898B9A: cmp r0,r1` / `0x00898B9E: ble #0x898b8c` | M11-032 (numeric part) | EXACT_SOURCE |
| G1.1c | Live call args: `image` (r0=sb), `&list` (r1=sp+0x348), `int = params+0x0C` (r2=r5), `short = params+0x10` (r3), stack short = params+0x12, stack `ConnectedComponents& = sp+0x580` (r4), stack the three MemoryStacks. Shipped: `0xCCCC`, 0, 0. | `0x00898BAA: ldr r5,[r7,#0xc]`; `0x00898BA0: ldrsh r0,[r7,#0x10]`; `0x00898BAC: ldrsh.w r6,[r7,#0x12]`; `0x00898BDA: mov r2,r5`; `0x00898BCC: strd r6,r4,[sp]`; `0x00898BE0: blx` PLT `0x4D1360` | M11-032 | EXACT_SOURCE |

### G1.2 Extractor entry (`0x0088F8BC..0x0088FDF8`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.2a | Entry: r0=image, r1=scale list, r2=mult, r3=short1; after `push.w {r4-r11,lr}` + `sub sp,#0xfc`, stack [sp+0x120]=short2, [sp+0x124]=CC&, [sp+0x128/+0x12c/+0x130]=the three MS. | `0x0088F8BC: push.w {r4-r11,lr}`; `0x0088F8C0: sub sp,#0xfc`; `0x0088F8C2: mov r4,r0`; `0x0088F8CE: mov r7,r1`; `0x0088F8CA: mov sl,r2`; `0x0088F8C8: mov r8,r3`; `0x0088F8DE: ldrd r6,r5,[sp,#0x128]`; `0x0088F8E2: ldr.w fp,[sp,#0x130]` | NEW | EXACT_SOURCE |
| G1.2b | Validity: `AreValid<MemoryStack x3>`, `NotAliased<MemoryStack x3>`, `AreValid<Array<u8>,FixedLengthList<int>,ConnectedComponents>`, scale count in [1,0x40]. Failures log and return 0x4000000 / 0x1000002 / 0x1000003 / 0x3000000. | `0x0088F8F8: blx` PLT `0x4D0CA0`; `0x0088F908: blx` PLT `0x4D0CAC`; `0x0088F91E: blx` PLT `0x4D0CB8`; `0x0088F928: subs r0,r4,#1` / `0x0088F92A: cmp r0,#0x40` / `0x0088F92C: bhs #0x88fb0c` | NEW | EXACT_SOURCE |
| G1.2c | `maxScale = max(list[i]+1)`, starting from -1. Shipped 17. | `0x0088F93C: mov.w r8,#-1`; loop `0x0088F942: ldr r0,[r7,#0x30]` / `0x0088F944: ldr.w r2,[r0,r1,lsl#2]` / `0x0088F94A: adds r2,#1` / `0x0088F94C: cmp r8,r2` / `0x0088F950: movle r8,r2` | NEW | EXACT_SOURCE |
| G1.2d | `ScrollingIntegralImage_u8_s32(this=sp+0xbc, rows=image[+0], cols=image[+4], numBorderPixels=maxScale, MS, Buffer)`. | `0x0088F96A: mov r3,r8`; `0x0088F976: mov r1,r7` (=image rows); `0x0088F974: ldr r2,[sp,#0x20]` (=image cols); `0x0088F978: blx` PLT `0x4D0CC4` | NEW | EXACT_SOURCE |
| G1.2e | Initial `ScrollDown(image, image[+0], MS)`. | `0x0088F98C: mov r2,r7`; `0x0088F990: blx` PLT `0x4D0CD0` | NEW | EXACT_SOURCE |
| G1.2f | N row buffers: `FixedLengthList<Array<u8>>(N, MS1, Buffer)`, then N x `Array<u8>(1, cols)` copied in (element stride 0x14). | `0x0088F9A6: blx` PLT `0x4D0CDC`; alloc loop `0x0088F9E2..0x0088FA3C`; `0x0088F9F0: movs r1,#1`; `0x0088F9F4: mov r2,r6` (=cols); `0x0088F9FA: blx` PLT `0x4CFC8C`; `0x0088FA38: adds r4,#0x14` | NEW | EXACT_SOURCE |
| G1.2g | One mask row `Array<u8>(1, cols)` at sp+0x4c; its data pointer (sp+0x5c) is the `u8*` handed to the callback and to NextRow. | `0x0088FA46: blx` PLT `0x4CFBFC`; `0x0088FA56: blx` PLT `0x4CFC8C`; `0x0088FA5A: ldr r4,[sp,#0x5c]`; `0x0088FA80: mov fp,r4` | NEW | EXACT_SOURCE |
| G1.2h | `ConnectedComponents::Extract2dComponents_PerRow_Initialize(CC, MS1, MS2, MS3)`. | `0x0088FA70: ldrd r0,r1,[sp,#0x124]`; `0x0088FA78: blx` PLT `0x4D0CE8` | M11-032 | EXACT_SOURCE |

### G1.3 Callback selection

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.3 | `N == 3` -> `numFilters3`; `N == 5 && mult == 0x10000` -> `numFilters5_thresholdMultiplier1`; `N == 5 && mult != 0x10000` -> `numFilters5`; otherwise the generic `ecvcs_computeBinaryImage`. Shipped N=3 -> `numFilters3`. | `0x0088FA8A: ldr r0,[sp,#0x1c]`; `0x0088FA8C: cmp r0,#5`; `0x0088FA8E: beq #0x88fb76`; `0x0088FA90: cmp r0,#3`; `0x0088FA92: bne #0x88fb90`; GOT slots `0x0103FF0C` (numFilters3), `0x0103FF08` (_5_thresholdMultiplier1), `0x0103FF04` (_5), `0x0103FF10` (generic) | M11-032 | EXACT_SOURCE |

### G1.4 Row loop

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.4 | For `row = 0 .. image[+0]-1`: `ecvcs_filterRows(sii, list, row, filteredRows)`; call the selected std::function with `(image, filteredRows, row, mult, maskPtr)`; `NextRow(maskPtr, cols, y=row, a=maxScale, b=params+0x12)`; then if `get_maxRow(maxScale-1) <= row` call `ScrollDown(image, image[+0] - 2*maxScale, MS)`. Finally `Finalize()`. | `0x0088FBCE: ldr r0,[sp,#0x18]` / `0x0088FBD0: cmp r6,r0` / `0x0088FBD2: bge #0x88fc6e`; `0x0088FBE4: blx` PLT `0x4D0D00`; `0x0088FBF8: strd r6,fp,[sp]` / `0x0088FC04: blx` PLT `0x4D0D0C`; `0x0088FC28: blx` PLT `0x4D0D18`; `0x0088FC3E: ldr r1,[sp,#0x1c]` / `0x0088FC42: blx` PLT `0x4D0D24` / `0x0088FC48: bgt #0x88fc62` / `0x0088FC5A: blx` PLT `0x4D0CD0`; `0x0088FC80: blx` PLT `0x4D0D30` | M11-032 | EXACT_SOURCE |
| G1.4n | `[sp+0x1c] = maxScale-1` (set at `0x0088FBBA: sub.w r0,r8,#1`), so `get_maxRow` is called with `maxScale-1`, not `maxScale`. `[sp+8] = image[+0] - 2*maxScale` (set at `0x0088F9D8: sub.w r0,r0,r8,lsl#1`). | as cited | NEW detail | EXACT_SOURCE |

### G1.5 `ecvcs_filterRows` (`0x0088F4D0`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.5 | For each window half-width `s` in the list: build `Rectangle<s16> { xmin=-s, xmax=+s, ymin=-s, ymax=+s }` at sp+0xc; look up `mult = T1[s]` at `0xC98958` and `shift = T2[s]` at `0xC98A5C`; call `FilterRow<u8>(sii, rect, row, mult, shift, out[i])`; advance out by 0x14. | `0x0088F4D6: ldr r7,[r1,#0xc]` (count); `0x0088F4F4: ldr r0,[r6],#4`; `0x0088F4FA: strh.w r0,[sp,#0xe]`; `0x0088F4FE: rsbs r1,r0,#0`; `0x0088F500: strh.w r1,[sp,#0xc]`; `0x0088F508: strh.w r1,[sp,#0x10]`; `0x0088F50C: strh.w r0,[sp,#0x12]`; `0x0088F504: ldr.w r3,[fp,r0,lsl#2]` (fp=0xC98958); `0x0088F512: ldr.w r0,[sl,r0,lsl#2]` (sl=0xC98A5C); `0x0088F51C: blx` PLT `0x4D0C7C`; `0x0088F522: add.w r4,r4,#0x14` | M11-032 | EXACT_SOURCE |

### G1.6 `ScrollingIntegralImage_u8_s32::FilterRow<u8>` (`0x0088F538`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.6a | Args: r0=sii, r1=rect, r2=row, r3=mult, [sp+0x40]=shift, [sp+0x44]=out&. `border = [sii+0x20]`, `rowOffset = [sii+0x1c]`, `stride = [sii+8]`, `data = [sii+0x10]`. | `0x0088F54A: ldr r1,[r4,#0x20]`; `0x0088F590: ldr r7,[r4,#0x1c]`; `0x0088F5A0: ldr r0,[r4,#8]`; `0x0088F5A8: ldr r2,[r4,#0x10]`; `0x0088F5F2: ldr r3,[sp,#0x40]`; `0x0088F5F6: str.w fp,[sp,#0x10]` | M11-032 | EXACT_SOURCE |
| G1.6b | Integral-buffer row indices: `top-1 = row - rowOffset + rect.ymin - 1`, `bottom = row - rowOffset + rect.ymax`. Columns: `left = border + rect.xmin - 1`, `right = border + rect.xmax`; the four corner pointers are `data + stride*rowIdx + 4*col`. | `0x0088F596: sub.w r7,r6,r7`; `0x0088F59E: add r3,r7`; `0x0088F5A2: add r5,r7`; `0x0088F5AA: sxtah r6,r1,lr`; `0x0088F5B2: sxtah r1,r1,ip`; `0x0088F5B6: ldr.w fp,[r8,#0x10]`; `0x0088F5C6`/`0x0088F5CA`/`0x0088F5CE`/`0x0088F5D2` | M11-032 | EXACT_SOURCE |
| G1.6c | Leading pad `(1+rect.xmin)-border` clamped to >=0 (0 at shipped scales); `memclr`; `FilterRow_innerLoop<u8>(start, end=W-1, mult, shift, p0,p1,p2,p3, out)`; trailing memclr when `W > end+1` (never at shipped). Returns 0. | `0x0088F558: subs.w sb,r3,r1`; `0x0088F55C: it le` / `0x0088F55E: movle.w sb,#0`; `0x0088F58A: str r0,[sp,#0x14]`; `0x0088F5DE: blx` PLT `0x4AE1E8`; `0x0088F5F6: str.w fp,[sp,#0x10]`; `0x0088F5FA: blx` PLT `0x4D0C94`; `0x0088F602: cmp r0,r1` / `0x0088F604: ble #0x88f612` | M11-032 | EXACT_SOURCE |

### G1.7 `FilterRow_innerLoop<u8>` (`0x0088A5206`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.7 | `out[i] = (p3[i] - p2[i] + p0[i] - p1[i]) * mult >> shift` (arithmetic shift, store low byte). If `mult==1 && shift==0`, writes the raw sum. | `0x008A5230: sub.w r4,r7,r4`; `0x008A5238: add r4,r5`; `0x008A523A: sub.w r4,r4,r6`; `0x008A523E: mul r4,r2,r4`; `0x008A5242: asr.w r4,r4,r3`; `0x008A5246: strb.w r4,[ip,r0]`; identity `0x008A5216: cmpeq r3,#0` / `0x008A521C: beq #0x8a5254` | NEW | EXACT_SOURCE |

### G1.8 `ecvcs_computeBinaryImage_numFilters3` (`0x0088F61A`, live callback)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.8 | Per pixel of the row: `a=f0`, `b=f1`, `c=f2` (filtered rows at data offsets +0x10/+0x24/+0x38); `v = (|b-a| > |c-b|) ? b : c` (strict `>`); `mask = (((v * mult) >> 16) > pixel) ? 1 : 0`, `mult` = the callback's 2nd register int (params+0x0C = 0xCCCC), 32-bit multiply and arithmetic `>>16`. | `0x0088F638: ldr.w lr,[r4,#0x10]`; `0x0088F63C: ldr.w r8,[r4,#0x24]`; `0x0088F640: ldr r4,[r4,#0x38]`; `0x0088F64E: subs r7,r5,r3`; `0x0088F650: subs r3,r6,r5`; `0x0088F652`/`0x0088F656` (abs); `0x0088F65C: cmp r7,r3`; `0x0088F65E: it gt`; `0x0088F660: movgt r6,r5`; `0x0088F662: mul r3,r6,r2`; `0x0088F66A: asrs r3,r3,#0x10`; `0x0088F66C: cmp r3,r5`; `0x0088F672: it gt`; `0x0088F674: movgt r3,#1`; `0x0088F67A: strb r3,[r1],#1` | M11-032 | EXACT_SOURCE |

### G1.9 `numFilters5` / `numFilters5_thresholdMultiplier1` (`0x0088F684` / `0x0088F75E`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.9 | Same largest-adjacent-gap selection over five windows f0..f4 (data offsets +0x10/+0x24/+0x38/+0x4c/+0x60); selected value is `f_{j+1}` for the first index j maximising the gap with preference `g10 >= max(g43,g32,g21)`, then `g21`, then `g32`, else `f4`; binarise `((v*mult)>>16) > pixel`. The `_thresholdMultiplier1` variant (mult==0x10000) is the same selection with `pixel < v`. | gaps `0x0088F6CE..0x0088F70A`; select `0x0088F70C: cmp r2,r7` / `0x0088F712: bge #0x88f726` / `0x0088F71A: add r2,r1,r6` / `0x0088F722: it eq` / `0x0088F724: moveq r2,r7`; binarise `0x0088F73E: muls r1,r2,r1` / `0x0088F744: asrs r1,r1,#0x10` / `0x0088F746: cmp r1,r2` / `0x0088F74E: movgt r1,#1`; t-variant `0x0088F7FC: cmp r1,r0` / `0x0088F802: it lo` / `0x0088F804: movlo r0,#1` / `0x0088F80A: strb r0,[sb],#1`; selector `0x0088FB78: cmp.w r0,#0x10000` | M11-032 | EXACT_SOURCE |

### G1.10 `ScrollingIntegralImage_u8_s32` ctor (`0x0088A4D34`) and layout

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.10 | `Array<int>(rows, cols + 2*numBorderPixels, MS, Buffer)`. Fields: `+0x14 = cols` (get_imageWidth), `+0x18 = -1`, `+0x1c = -numBorderPixels` (get_rowOffset), `+0x20 = numBorderPixels` (get_numBorderPixels). Error path sets `+0/+4/+8 = -1`, `+0x10 = 0`. | `0x008A4D44: add.w r2,r7,r5,lsl#1`; `0x008A4D4E: blx` PLT `0x4D03F4`; `0x008A4D5C: strd r7,r8,[r4,#0x14]`; `0x008A4D60: strd r0,r5,[r4,#0x1c]`; guard `0x008A4D56..0x008A4D6C` | NEW | EXACT_SOURCE |

### G1.11 `ScrollDown` (`0x0088A4DB0..0x0088A4FD8`) — resolves the evidence file's open Q1

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.11a | Guard `1 <= arg2 <= [sii]` (integral row count). Builds a temporary 1 x `[sii+4]` `Array<u8>` (temp data at [sp+0x30]). `H = image[+0]` ([sp+0x18]), `W = [sii+4]`. | `0x008A4DBA: cmp.w sl,#1`; `0x008A4DC2: ldrge.w r6,[sb]`; `0x008A4DC6: cmpge r6,sl`; `0x008A4DF0: ldr.w r4,[sb,#4]`; `0x008A4DF6: str r0,[sp,#0x18]`; `0x008A4DF8: add r0,sp,#0x1c`; `0x008A4E0C: blx` PLT `0x4CFC8C`; `0x008A4E10: ldr.w r8,[sp,#0x30]` | NEW | EXACT_SOURCE |
| G1.11b | **Initial fill** (taken when `[sii] == arg2`, i.e. the first call with arg2=H): `PadImageRow(image, 0, temp)`; write the prefix sum of temp into integral row 0; then for `i=0..border-1` write `row(i+1) = row(i) + cumsum(temp)` (border replications); `arg2 -= border; arg2 -= 1`; then for `r7 = 1..` while `r7 < H` and `arg2 > 0`: `PadImageRow(image, r7, temp)`, `row(fp) = row(fp-1) + cumsum(temp)`, `fp++`, `r7++`, `arg2--`. Stores `[sii+0x18] = r7`. | `0x008A4E22: add r3,sp,#0x20`; `0x008A4E2A: blx` PLT `0x4D1894`; `0x008A4E3E: ldrb r7,[r2],#1` / `0x008A4E44: add r1,r7` / `0x008A4E46: str r1,[r0],#4`; replicate loop `0x008A4E5A..0x008A4E96`; `0x008A4E98: sub.w sl,sl,r1`; `0x008A4EA2: sub.w sl,sl,#1`; main fill `0x008A4F0A..0x008A4F5C`; `0x008A4F74: str r7,[sb,#0x18]` | NEW (resolves ecvcs open Q1) | EXACT_SOURCE |
| G1.11c | **Scroll fill** (taken when `[sii] != arg2`, i.e. later calls with arg2 = H-2*border): `rowOffset += arg2`; copy rows `arg2 .. [sii]-1` up to rows `0 .. [sii]-arg2-1` (`memcpy4` of one stride each); then fill rows `[sii]-arg2 .. [sii]-1` from source rows `r7+1..` while `r7 < H` and `arg2 > 0`; if source rows run out, keep using the last row (`r7 = H-1`). Stores `[sii+0x18] = r7`. | `0x008A4EB0: ldr.w r7,[sb,#0x18]`; `0x008A4EB4: sub.w fp,r6,sl`; `0x008A4EC0..0x008A4ED4: __aeabi_memcpy4`; `0x008A4EDE: ldr.w r0,[sb,#0x1c]` / `0x008A4EE2: add r0,sl` / `0x008A4EE4: str.w r0,[sb,#0x1c]`; fill `0x008A4F0A..0x008A4F5C`; edge reuse `0x008A4F60`/`0x008A4F64: subs r7,r0,#1`; `0x008A4F70: cmp.w sl,#1` / `0x008A4F74: str.w r7,[sb,#0x18]` | NEW (resolves ecvcs open Q1) | EXACT_SOURCE |

### G1.12 `get_maxRow` (`0x0088A51E0`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.12 | `get_maxRow(arg)`: let `H=[sii]`, `L=[sii+0x18]` (last source row filled), `O=[sii+0x1c]` (rowOffset). Return `L - arg`; but if `(H-1) > (L - O)` return `H-1 + O - arg`. With the main loop's `arg = maxScale-1` this is the condition that triggers the next `ScrollDown`. | `0x008A51E0: ldr.w ip,[r0]`; `0x008A51E4: ldrd r3,r2,[r0,#0x18]`; `0x008A51E8: subs r0,r3,r1`; `0x008A51EA: subs r2,r0,r2`; `0x008A51EC: add r1,r2`; `0x008A51EE: sub.w r2,ip,#1`; `0x008A51F2: subs r1,r2,r1`; `0x008A51F4: it gt`; `0x008A51F6: addgt r0,r0,r1` | NEW (resolves ecvcs open Q1) | EXACT_SOURCE |

### G1.13 `PadImageRow` / `ComputeIntegralImageRow` (`0x0088A5058` / `0x0088A50C8`, `0x0088A50E2`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.13 | `PadImageRow(image, row, out)`: `src = image.data + image.stride*row`; `out[0..border-1] = src[0]`; copy `floor(cols/4)` words to `out+border`; `out[border+cols .. border+cols+border-1] = src[cols-1]`. (The `cols % 4` tail is not copied by the word loop.) `ComputeIntegralImageRow(ptr, out, n)` is a running prefix sum; the 4-arg overload adds a previous row's sums. | `0x008A505C: ldrd ip,r6,[r1,#4]`; `0x008A5062: mla r1,r6,r2,r7`; `0x008A506C: asr.w r4,ip,#2`; `0x008A5078: ldrb lr,[r5,#-0x1]`; left fill `0x008A5084: strb r7,[r3,r2]`; word copy `0x008A509C: ldr r6,[r1],#4` / `0x008A50A2: str r6,[r5],#4`; right fill `0x008A50B6: strb.w lr,[r1,r2]`; prefix `0x008A50D2: ldrb r3,[r0],#1` / `0x008A50D8: add ip,r3` / `0x008A50DA: str ip,[r1],#4`; 4-arg `0x008A50EE..0x008A50FC` | NEW | EXACT_SOURCE (tail bytes only copied when cols%4==0; shipped camera width is a multiple of 4) |

### G1.14 Per-row DP: `Extract2dComponents_PerRow_*` and `Extract1dComponents`

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G1.14a | Dispatch thunk: object byte 0 -> i32 template at `+0x11c`, else u16 template at `+4`. `DetectFiducialMarkers` sets the byte from `params+0x40 < 0x10000` (shipped 39000 -> u16). | `0x00893BAA: ldrb.w ip,[r0]` / `0x00893BAE: cmp.w ip,#0` / `0x00893BB4: addeq.w r0,r0,#0x11c` / `0x00893BBC: adds r0,#4`; `0x0089886E: cmp.w r1,#0x10000` / `0x00898878: movlt r3,#1`; `0x0089351A: strb r7,[r8]` | NEW | EXACT_SOURCE |
| G1.14b | `Extract1dComponents(maskRow, width, a=maxScale, b=params+0x12=0, list&)`: scans runs of 1s; for each run `[start, end]` (end = last 1 pixel), if `(end-start+1) >= a` and the `b` test passes, append `{s16 start, s16 end, u16 0xffff, u16 0xffff}`. At shipped `b=0` the `b` test (`(s16)(r6+1) <= b`) never drops a run. | `0x00896FDC: strd r2,r3,[sp]`; run start `0x0089702E..0x00897038`; end test `0x00897040: add.w r8,r6,#1` / `0x00897046: sxth.w r6,r8` / `0x0089704A: cmp r6,r4` / `0x0089704C: ble #0x8970a0`; length test `0x00897054: sub.w r4,sl,sl,lsl#1` / `0x0089705C: add.w r4,r7,r4,lsl#16` / `0x00897060: asrs r4,r4,#0x10` / `0x00897062: cmp r4,r3` / `0x00897064: blt #0x8970a4`; append `0x00897086: str.w r4,[r2,lr,lsl#3]` / `0x00897096: str r4,[r2,#4]`; flush `0x008970BA..0x008970F4` | NEW | EXACT_SOURCE (b semantics = no-op at 0; see G1.15.3) |
| G1.14c | `NextRow` merges runs across rows: for each current run, walk the previous row's segments, union-find (set all to min) their component ids in the u16 parent array, write the merged segment into the current-row list and append to the output accumulator; a non-overlapping run gets a new id; finally swap the two row lists. | `0x00893F96: blx` PLT `0x4D0FF4`; overlap `0x00893FF0: ldrsh.w r5,[fp,r3,lsl#3]`; union `0x00894024: ldrh.w r0,[r7,ip,lsl#1]` / `0x00894042: strh.w r4,[r7,r5,lsl#1]` / `0x00894046`/`0x0089404A`; append `0x0089406A: str.w r4,[r6,r5,lsl#3]` / `0x00894078: str r0,[r6,#4]`; new id `0x008940AE: ldrh.w r0,[r5,#0x10c]`; `0x008940FC: blx` PLT `0x4D1000` (Swap) | NEW | EXACT_SOURCE |
| G1.14d | Segment format: 8 bytes `{ s16 start@+0, s16 end@+2, u16 row@+4, u16 componentId@+6 }`; the output accumulator is at CC+4 template base `{ size@+0xc, capacity@+0x1c, data@+0x30 }`. Finalize resolves ids to roots and sets max id. | `0x00894062: pkhbt r4,sl,lr,lsl#0x10`; `0x00894074: orr.w r0,r0,ip,lsl#0x10`; Finalize `0x00894426: ldrh r7,[r3]` / `0x0089442C: ldrh.w r7,[r2,r7,lsl#1]` / `0x00894430: strh r7,[r3],#8` | NEW | EXACT_SOURCE |

### G1.15 Does `re-analysis/evidence/m11/ecvcs-extractor.md` match the `.so`?

**Yes, instruction for instruction, with the following divergences (all minor).**

1. **L13 / open Q1.** The file writes "if `get_maxRow(maxScale) <= row` then `ScrollDown`". The actual argument is `maxScale-1` (`0x0088FBBA: sub.w r0,r8,#1; str r0,[sp,#0x1c]`; `0x0088FC3E: ldr r1,[sp,#0x1c]`). The condition and the scroll amount (`image[+0] - 2*maxScale`) are otherwise correct. This pass now also reads `ScrollDown` and `get_maxRow` (G1.11/G1.12), so the file's **open Q1 is closed**: the input `Array<u8>` is not pre-padded; the SII pads each row by `numBorderPixels` at both ends and the integral buffer row index of source row `r` is `r - rowOffset` (with `rowOffset` starting at `-border` and incremented by `H-2*border` per scroll); `get_maxRow(arg) = min(H-1+O-arg, L-arg)` in the sense of G1.12.
2. **L7.** The file gives `+0x14`, `+0x1c`, `+0x20`; it omits `+0x18 = -1` (set by the same ctor, `0x008A4D5C`). `+0x18` is the "last source row filled" that `ScrollDown` updates and `get_maxRow` reads.
3. **Open Q2 (the `Extract1dComponents` `b` test).** Confirmed: the test is `(s16)(r6+1) <= b` where `r6` is the run-length accumulator that is 0 at a run start and 1 at the end branch, so at shipped `b=0` it is a no-op; the parameter's intended meaning is still not recovered. The `a` test is `(end-start+1) >= a`.
4. **Tie-break.** Confirmed per function: `numFilters3` selects `f1` only on strict `>` (`0x0088F65E: it gt`), while the five-filter variants use `>=`. Behaviour-changing only on exact ties.
5. **D8 remap tail.** `CompressConnectedComponentSegmentIds` (u16 body `0x00894A7C`) was not re-read to its end this pass; the file itself says the remap tail `0x00894AF4..` is not read. Not needed for the live path (the id compression is read at `0x00898C90..0x00898D50` by X1 V11).
6. **Minor wording.** The file's L2 says "i = 0 .. params+0x04+1"; the loop stores indices 0..params+4 inclusive, i.e. size `params+4+2`. Correct, but easy to misread.

**Not contradicted.** The file's item 1 (0xCCCC is live, not binomial-only) is correct and agrees with X1. Its `+0x40` u16 selector, segment format and D1 thunk are all confirmed.

---

## G2. The `MarkerDetector` post-processing (M11-022)

`MarkerDetector::Detect(Image const&, list<ObservedMarker>&)` at `0x008753B6..0x008759CC`.
`DetectFiducialMarkers` is the Thumb function at `0x00898760`; `0x004CFCEC` in the X1/lead
reports is its **PLT stub**, not the body. `GetROI`, `GetNegative` and `InitFromPointContainer`
in the X1 report are likewise PLT stubs; the bodies are named below.

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G2.1 | Entry: `ResetBuffers(width, height, [camera+0x6c])`; wrap the `Image` as `Array<u8>` with dims `[image+0xc] x [image+0x10]` and `MemoryStack = detectorMemory+0x40`. | `0x008753C6: ldr r3,[r1,#0x6c]`; `0x008753C8: blx` PLT `0x4CFC80`; `0x008753D4: ldrd r4,r5,[r8,#0xc]`; `0x008753E4: add.w r3,r6,#0x40`; `0x008753F0: blx` PLT `0x4CFC8C` (Array<u8> ctor) | M11-022 | EXACT_SOURCE |
| G2.2 | ROI / negative choice: build a `std::list<bool>` from `[camera+0x7c]` — 0 -> `{false}`, 1 -> `{true}`, 2 -> `{false, true}`, other -> `{}` (empty => Detect returns 0 without running the detector). | `0x00875402: ldrb.w r0,[r0,#0x7c]`; `0x00875406: cbz r0,#0x87543e`; `0x00875408: cmp r0,#1` / `0x0087540a: beq #0x87544c`; `0x0087540c: cmp r0,#2` / `0x0087540e: bne #0x875468`; `0x00875410..0x875462` (node construction); `0x0087546a..0x875470` (`cmp r6,r4`; empty list -> `0x87549c` cleanup/return) | M11-022 | EXACT_SOURCE |
| G2.3 | Per pass: construct a local `Image`; if the ROI vector is non-empty, `CopyTo` the source into it and for each `Rectangle<int>` `GetROI` (`ImageBase<u8>::GetROI<Image>` body `0x00658A92`/`0x006BA380`) and `FillWith(0xff)` the region; otherwise assign the source image. | `0x0087556C: ldrb r5,[r4,#8]`; `0x00875570: blx` PLT `0x4AC490` (Image::Image); `0x0087557E: blx` PLT `0x4A5A28` (CopyTo); `0x008755A2: blx` PLT `0x4BF048` (GetROI); `0x008755AA: blx` PLT `0x4A5E18` (FillWith 0xff); `0x0087560A: blx` PLT `0x4A5A1C` (operator=) | M11-022 | EXACT_SOURCE |
| G2.4 | Negative choice: if the pass bool is false use the image as-is; if true, `Image::GetNegative` (body `0x00871DCC`) into a temp and copy it into the `Array<u8>` via the local helper `0x00875A64`; if false, copy the image directly via `0x00875A64`. | `0x00875612: cbz r5,#0x87567c`; `0x0087561A: blx` PLT `0x4CFCC8` (GetNegative); `0x00875622: bl 0x875a64`; `0x0087567C: add r0,sp,#0x10c` / `0x00875680: bl 0x875a64`; helper `0x00875A64: ldrd r3,lr,[r0,#0xc]` / `0x00875A6C: ldrd r4,ip,[r1]` / `0x00875AD2: ldr r3,[r0,#0x14]` / `0x00875AE2: b.w 0x8cca1c` (bulk copy) | M11-022 | EXACT_SOURCE |
| G2.5 | Before the detector call, initialise each of `[memory+0x94]` VisionMarker slots' 3x3 float block at `marker+0x2c..0x3c` from a fresh `Array<float>(3,3)`, then call `DetectFiducialMarkers(array, markerList, params, MS x3)`. | `0x00875690: ldr.w r4,[r8,#0x94]`; `0x00875694: str.w r4,[r8,#0x84]`; `0x008756B8: movs r1,#3` / `0x008756BC: movs r2,#3` / `0x008756BE: blx` PLT `0x4CFCD4` (Array<float> 3x3); `0x008756D2: str r1,[r0,#0x2c]`..`0x008756E2: str r1,[r0,#0x3c]`; `0x008757B2: blx` PLT `0x4CFCEC` (DetectFiducialMarkers) | M11-022 | EXACT_SOURCE |
| G2.6 | On success, `count = [memory+0x84]`, marker array data = `[memory+0xa8]`, stride 0x40. For each marker: build `Quadrilateral<float>` from the four `Point<float>` at `marker+0..+0x1f`; if the bool-list size >= 2, zero a `Rectangle<int>` and `Rectangle<int>::InitFromPointContainer<Quadrilateral<float>>` (body `0x0087855C`) on the quad, then push it to the ROI vector. | `0x008754C0: ldr r8,[r0,#0x84]`; `0x008754E4: ldr r0,[r0,#0xa8]`; `0x008754EA..0x00875502` (four points); `0x00875516: blx` PLT `0x4A48D0` (Quadrilateral ctor); `0x0087551A: ldr r0,[sp,#0x160]` / `0x0087551C: cmp r0,#2` / `0x0087551E: blo #0x875546`; `0x00875532: blx` PLT `0x4BEBBC` (InitFromPointContainer) | M11-022 | EXACT_SOURCE |
| G2.7 | Append the `ObservedMarker`: `list<ObservedMarker>::emplace_back<unsigned int, MarkerType const&, Quad&, Camera const&>`; the first u32 is `[ImageBase+0x3c]` (the image id/timestamp field copied by `ImageBase<u8>::operator=` at `0x0086EF2C/0x0086EF30`), the `short` is the marker code's low 16 bits at `marker+0x20`, the quad is `sp+0x40`, the camera is `[detector]`. The `ObservedMarker` ctor (`0x0087E1FC`) stores code@+0, timestamp@+4, quad@+8..+0x27, camera@+0x28, -1@+0x4c. | `0x00875546: ldr r0,[sp,#0x20]` / `0x00875548: ldr r0,[r0,#0x3c]`; `0x0087554E: ldr r0,[r0]` ([detector]); `0x00875552: add r2,r5,#0x20`; `0x0087555C: blx` PLT `0x4CFCBC`; ctor `0x0087E200: ldrh r0,[r2]` / `0x0087E202: str r1,[r6,#4]` / `0x0087E204: strh r0,[r6]`; `0x0087E224: str r0,[r6,#0x4c]` | M11-022 | EXACT_SOURCE |

Notes / residual uncertainty:
- `IsQuadrilateralReasonable` is inside `DetectFiducialMarkers` (`0x00892B18`, X1 V14), not in `Detect`; `Detect` only consumes the already-accepted `VisionMarker` list.
- The `Rectangle<int>` ROI vector is initialised once before the bool loop (`0x0087546E/0x00875470`) and accumulates across passes; the first (non-negative) pass populates it, the second (negative) pass masks those regions with 0xff. This is the "ROI/negative choice" mechanism.
- `InitFromPointContainer` body `0x0087855C` is the float rectangle version; the calls use the `Quadrilateral<float>` overload (PLT `0x4BEBBC`). The exact min/max update is straightforward and cited; I did not transcribe every `vcmpe`.

---

## G3. Sub-pixel corner refinement: `RefineQuadrilateral` (`0x008C55E0..0x008C656C`)

This pass read the function body and the helper at `0x008C66C4`. The function is large
(~0x0F8C bytes) and NEON-heavy; I transcribe the behaviour-changing steps and constants, and
mark the sub-parts I could not decode exactly as RECOVERABLE_GAP with the exact address to read.

Signature (mangled): `RefineQuadrilateral(Quadrilateral<float> const& quad, Array<float> const&,
Array<u8> const& image, Point<float> const&, Point<float> const&, int numSamples, float, float,
int maxIter, float stopThresh, float, Quadrilateral<float>& outQuad, Array<float>&
outHomography, MemoryStack)`.
The stack-argument map is at the prologue: `[sp+0x390]=numSamples`, `[sp+0x384]=maxIter`,
`[sp+0x380]=image`, `[sp+0x388]=?`, `[sp+0x38c]=?`, `[sp+0x394]=?`, `[sp+0x398]=stopThresh`,
`[sp+0x39c]=outQuad`, `[sp+0x3a0]=outHomography`, `[sp+0x3a4]=MS` (read at
`0x008C55FE`/`0x008C56B8`/`0x008C56BC`/`0x008C56BE`/`0x008C6358`).

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G3.1 | Guards: `AreEqualSize(3,3, initialHomography, refinedHomography)` and `NotAliased`. | `0x008C5600: movs r0,#3` / `0x008C5602: movs r1,#3` / `0x008C5608: blx` PLT `0x4D229C`; `0x008C5616: blx` PLT `0x4D1570` | M11-005 | EXACT_SOURCE |
| G3.2 | Input quad geometry: edge vectors `d0=(x0-x3, y0-y3)`, `d1=(x1-x2, y1-y2)`; the longer edge (`s16 = sqrt(d0.d0)`, `s0 = sqrt(d1.d1)`) selects the sample direction; `scale = s20 / 1.4142135` (1/sqrt2) and a spacing factor. | `0x008C5620..0x008C56B6`; `0x008C5838: vldr s4,[pc,#0x3d8]` (=255.0 at `0x8C5C14`); `0x008C5840: vldr s6,[pc,#0x3d4]` (=1.4142135 at `0x8C5C18`); `0x008C584E: vdiv.f32 s0,s0,s4`; `0x008C5852: vdiv.f32 s4,s20,s6`; `0x008C585A/0x008C585E: vmul` | M11-005 | EXACT_SOURCE for the constants; the geometric derivation is partially read |
| G3.3 | Sample count per block: `r6 = ceil(numSamples * 0.125)` (`0x008C5712: vmov.f32 s0,#0.125`; `0x008C571A: vmul`; `0x008C5722: blx ceilf`); eight blocks per pass, total `r5 = r6*8`. Allocates five `Array<float>(1, r5)`: xSquare, ySquare, Tx, Ty, and one more. | `0x008C5712/0x008C571A/0x008C5722`; `0x008C572A: ldr r1,[sp,#0x2dc]`; `0x008C573C: lsls r5,r6,#3`; `0x008C5740: blx` PLT `0x4CFCD4`; four more at `0x008C575C`/`0x008C5778`/`0x008C5794` | M11-005 | EXACT_SOURCE |
| G3.4 | **Sample generation** (`0x008C5E80..0x008C61E0`): for each of the `r5` samples, two parameter arrays (`[sp+0x5c]`, `[sp+0x50]`) give `(u,v)`; the sample is projected through the 3x3 initial homography (`[sp+0x3a0]`) into image coordinates: `X = (h0*u + h1*v + h2)/(h6*u + h7*v + h8)`, `Y = (h3*u + h4*v + h5)/(h6*u + h7*v + h8)`; `floorf`/`ceilf` bound the pixel and a bilinear value is read. | load homography `0x008C5EDA..0x008C5F00`; project `0x008C5F46: vmul.f32 s4,s19,s0` .. `0x008C5F7E: vadd.f32 s0,s23,s0`; `0x008C5F76: vdiv.f32 s4,s18,s4`; `0x008C5F82/0x008C5F86: vmul`; `0x008C5F90: blx floorf` / `0x008C5F9C: blx floorf` / `0x008C5FA4: blx ceilf` / `0x008C5FBE: blx ceilf`; bounds `0x008C5FB4`..`0x008C5FF2`; fractional parts `0x008C6022: vsub.f32 s17,s17,s30` / `0x008C6026: vsub.f32 s22,s22,s28`; bilinear pixels `0x008C60B6..0x008C60DC`; bilinear value `0x008C60E6..0x008C610A`; residual vs `[sp+0x40]` `0x008C6112: vsub.f32 s0,s0,s2`; `0x008C6116: vmul.f32 s0,s0,s2` (=1/255 at `0x8C6474` = 0.003921569) | M11-005 | RECOVERABLE_GAP: the pixel indexing `mla r0,r1,r5,r2` / `mla r1,r1,r7,r2` and the exact bilinear weight pairing at `0x008C60B6..0x008C610A` are read but not fully decoded; read `0x008C606A..0x008C610A` against the image `Array<u8>` layout (`+0x44` base, `+0x48` stride, from `0x008C609A: ldrd r2,r1,[sp,#0x44]`) |
| G3.5 | **Normal equations** (`0x008C611E..0x008C6188`): for each sample, an 8-element design vector at `[sp+0x25c]` (indexed by `r1 = 0..7`; element pointers are `[sp+0x25c][j]` offset by `sl*4`) is accumulated into an 8x9 matrix `A` at `[sp+0xf8]` (row stride 0x24 = 9 floats) and an 8-vector `b` at `[sp+0xd8]`: `A[j][k] += d[j]*d[k]`, `b[j] += residual*d[j]`. | `0x008C611E: ldr.w r2,[r4,r1,lsl#2]`; `0x008C6130: add.w r2,ip,r1,lsl#5`; `0x008C6134: add.w r2,r2,r1,lsl#2`; `0x008C6138: vmul.f32 s4,s2,s2`; `0x008C6144: vstr s4,[r2]`; inner `0x008C614C..0x008C616C`; rhs `0x008C616E: vmul.f32 s2,s0,s2` / `0x008C6182: vstr s2,[r1]` | M11-005 | RECOVERABLE_GAP: the contents of the 8-element design vector (`[sp+0x25c]`) are not decoded; read `0x008C5C3E..0x008C5E80` (where `sp+0x25c` is filled) |
| G3.6 | `Matrix::MakeSymmetric(A, false)` (`0x008C61E0`) then `Matrix::SolveLeastSquaresWithCholesky(A, b, false, converged)` (`0x008C61F4`); if the result is non-zero, return it. | `0x008C61E0: blx` PLT `0x4D0B50`; `0x008C61F4: blx` PLT `0x4D0B5C`; `0x008C61FA: cmp.w sb,#0` / `0x008C61FE: bne.w #0x8c5bf4` | M11-005 | EXACT_SOURCE (call sites); the Cholesky body `0x0088DE68` and `MakeSymmetric` `0x0088DDC0` were not re-read this pass |
| G3.7 | **Update composition `H = H * inv(U)`** (`0x008C620A..0x008C629A`): build the 3x3 `U` from the 8-vector solution `s[0..7]`: `U = [[s0+1, s1, s2],[s3, s4+1, s5],[s6, s7, 1]]`; `Invert3x3(U)` in place; `Matrix::Multiply(initialHomography, U, newHomography)`. | `0x008C620A: vldr s0,[r0]` / `0x008C620E: vadd.f32 s0,s0,s18` (=+1.0) / `0x008C6212: vstr s0,[r1]`; `0x008C6218`/`0x008C621E`/`0x008C6222`/`0x008C622A: vadd.f32 s0,s0,s18`/`0x008C623E`/`0x008C6246`/`0x008C6252`/`0x008C625E..0x008C6268: mov.w r1,#0x3f800000; str r1,[r0,#8]`; `0x008C6290: blx` PLT `0x4D22A8` (Invert3x3); `0x008C629A: blx` PLT `0x4D2200` (Multiply) | M11-005 | EXACT_SOURCE |
| G3.8 | **H22 renormalisation** (`0x008C62A0..0x008C634A`): if `|H22 - 1| >= 1e-5` (literal at `0x8C6504`), divide the 3x3 by H22 via `Matrix::Elementwise::ApplyOperation<DotDivide>` (`0x008C634A`). `[sp+0x60]` records whether the solve reported converged (`0x008C62CA..0x008C62D4`). | `0x008C62AA: vldr s0,[r2,#8]`; `0x008C62B0: vadd.f32 s0,s0,s2` (s2=-1); `0x008C62BC: vneg.f32 s2,s0`; `0x008C62C0: it mi` / `0x008C62C2: vmovmi.f32 s0,s2`; `0x008C62C6: vldr s2,[pc,#0x23c]`; `0x008C62D0: vcmpe.f32 s0,s2` / `0x008C62DA: bmi #0x8c634e`; `0x008C634A: blx` PLT `0x4D22B4` | M11-005 | EXACT_SOURCE (threshold); RECOVERABLE_GAP for the exact DotDivide operand setup `0x008C62DC..0x008C6348` (ArraySlice plumbing) |
| G3.9 | Write `newHomography` back into `refinedHomography` (`Array<float>::SetCast`, `0x008C6354`); call the helper `0x008C66C4` on `(refinedHomography, quad)` to get the residual `s16`. | `0x008C6354: blx` PLT `0x4D0AE4`; `0x008C6358: ldr r1,[sp,#0x39c]`; `0x008C635C: bl 0x8c66c4`; `0x008C6362: vmov.f32 s16,s0` | M11-005 | EXACT_SOURCE |
| G3.10 | **Stopping test** (`0x008C636C..0x008C6388`): stop if `residual < [sp+0x398]` (`0x008C6370: vcmpe.f32 s16,s0` / `0x008C6378: bmi #0x8c63e4`); else increment the iteration count and stop if it reaches `[sp+0x384]` (`0x008C637E..0x008C6388`). If `r6` (the solve-converged flag) is zero, loop back to `0x008C5EC0`; otherwise finish. | as cited | M11-005 | EXACT_SOURCE |
| G3.11 | Finalise (`0x008C63E4..0x008C6470`): copy the original quad into a temp; if `[sp+0x60] == 1`, write the temp to `[sp+0x39c]` (out quad) and `SetCast` the refined homography into `fp`; else call the helper again and set the success flag from `residual > [sp+0x394]`. Returns `sb`. | `0x008C63F2..0x008C6416`; `0x008C6418: ldr r0,[sp,#0x60]` / `0x008C641C: bne #0x8c644a`; `0x008C643C: blx` PLT `0x4D0AE4`; `0x008C644A`/`0x008C644E: bl 0x8c66c4`; `0x008C6452: vldr s2,[sp,#0x394]` / `0x008C645A: vcmpe.f32 s0,s2` / `0x008C6464: movgt.w sb,#1` | M11-005 | EXACT_SOURCE |
| G3.12 | Helper `0x008C66C4(Array<float> const& homography, Quadrilateral<float> const& quad)`: clear a 32-byte temp; copy the 4 quad points; transform each corner by the 3x3 homography (`X=(h0*x+h1*y+h2)/(h6*x+h7*y+h8)`, `Y=(h3*x+h4*y+h5)/(...)`); compute `sqrt((X-xt)^2+(Y-yt)^2)` for each corner and return the maximum (floor 0). | `0x008C66C4` prologue; `0x008C66D2..0x008C6720`; division `0x008C6742: vdiv.f32 s8,s2,s30`; `0x008C6786/0x008C678A: vmul`; distances `0x008C67AE: vsub.f32 s0,s8,s0` / `0x008C67B6: vmul.f32 s0,s0,s0` / `0x008C67C2: vsqrt.f32 s0,s2`; max `0x008C6834: vcmpe.f32 s0,s16` / `0x008C6840: vmovgt.f32 s16,s0`; return `0x008C6848: vmov.f32 s0,s16` | M11-005 | EXACT_SOURCE |
| G3.13 | `ComputeBrightDarkValues` contrast gate (`0x0089F8E8`; tail `0x0089FD00..0x0089FD3A`): the two returned means `s0` (out at `[r0]`) and `s2` (out at `[r1]`) are normalised (`s4 = s29/s4`, `s2 *= s4`, `s0 *= s4`), then the bool result is `s0 > ratio * s2` with `ratio = [r7-0x78]` (Parameters+0x3C = 1.01). | `0x0089FD04: vdiv.f32 s4,s29,s4`; `0x0089FD08: vmul.f32 s2,s4,s2`; `0x0089FD0C: vmul.f32 s0,s4,s0`; `0x0089FD10: vldr s4,[r7,#-0x78]`; `0x0089FD14: vmul.f32 s4,s2,s4`; `0x0089FD18/0x0089FD1C: vstr`; `0x0089FD26: vcmpe.f32 s0,s4`; `0x0089FD2E: it gt` / `0x0089FD30: movgt r0,#1`; `0x0089FD3A: strb.w r0,[r8]` | M11-005 | EXACT_SOURCE |
| G3.14 | Constants read: `255.0` (`0x8C5C14`), `1.4142135` (`0x8C5C18`), `0.125` (sample block divisor), `1/255 = 0.003921569` (`0x8C6474`), `1e-5 = 9.9999997e-6` (`0x8C6504`), `0.0` (`0x8C6854`). | `0x008C5838`/`0x008C5840`/`0x008C5712`/`0x008C6116`/`0x008C62C6`/`0x008C67E0`; data at the cited addresses | M11-005 | EXACT_SOURCE |

**G3 verdict.** The orchestration, update composition, H22 gate, stopping test, helper and
contrast gate are now transcribed. The two pieces still not fully decoded are (a) the exact
bilinear pixel weighting in `0x008C60B6..0x008C610A` and (b) the contents of the 8-element
design vector filled at `0x008C5C3E..0x008C5E80` (the `[sp+0x25c]` array) and the exact
`DotDivide` operand plumbing at `0x008C62DC..0x008C6348`. These are RECOVERABLE_GAP; read those
three ranges. Until then M11-005 cannot be more than EXACT_SOURCE for orchestration and
RECOVERABLE_GAP for the numerics.

---

## G4. M11-003 evidence — native addresses and instructions

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G4.1 | `Robot::_kDefaultHeadCamRotation` is a 36-byte `RotationMatrix3d` in `.bss` at `0x01059CF8` (GOT slot `0x103E958`, `ARM_GLOB_DAT`). It is initialised by the static ctor at `0x004D6DA4..0x004D6DB6` from the 9-float initializer list at `0x00C4A854` = `[0, -0.0698, 0.9976, -1, 0, 0, 0, -0.9976, -0.0698]`. It is read in `Robot::Robot` at `0x0050FFAE/0x0050FFB0` and passed as the rotation to `Pose3d::Pose3d` at `0x0050FFBA`. | data `0x01059CF8`; init `0x004D6DAC: add r0,pc` (GOT `0x103E958`) / `0x004D6DB2: blx` PLT `0x4A4930` (`RotationMatrix3d::RotationMatrix3d(std::initializer_list<float>)`); list `0x00C4A854`; read `0x0050FFAE: add r0,pc` / `0x0050FFB0: ldr r1,[r0]` / `0x0050FFBA: blx` PLT `0x4A6C40` | M11-003 | EXACT_SOURCE (address + value) |
| G4.2 | `Robot::GetCameraPose(float) const` at `0x00510FFC`: copy the head-cam pose from `[this+0x2d8]`, negate the angle (`0x00511016: eor r1,r5,#0x80000000`), build a `RotationVector3d(Radians, Y_AXIS_3D())`, apply `Transform3d::RotateBy`, set the pose name (6 chars at `0x00511042`). | `0x00510FFC` entry; `0x00511000: add.w r1,r1,#0x2d8`; `0x00511016: eor r1,r5,#0x80000000`; `0x00511020: blx` PLT `0x4A47B0` (`Y_AXIS_3D`); `0x0051102A: blx` PLT `0x4A47BC`; `0x00511036: blx` PLT `0x4A47EC` (`RotateBy`); `0x00511050: blx` PLT `0x4A4C24` (`SetName`) | M11-003 | EXACT_SOURCE |
| G4.3 | `Block::LookupBlockInfo(ObjectType)` at `0x004E4C8C`: a guarded function-local static table of block infos; builds entries (e.g. `"LIGHTCUBE1"` at `0x004E4CC4`, `NamedColors::ORANGE` at `0x004E4CDA`) and returns the lookup. | `0x004E4C8C` entry; `0x004E4CAE: blx` PLT `0x4A472C` (`__cxa_guard_acquire`); `0x004E4CC4: addw r1,pc,#0x64c` (`"LIGHTCUBE1"`); `0x004E4CDA: add r0,pc` (GOT `0x103E7D8` `NamedColors::ORANGE`) | M11-003 | EXACT_SOURCE (entry + table construction); the full table body was not transcribed |
| G4.4 | `Block::AddFace(FaceName, MarkerType const&, float, u8, u8)` at `0x004E53BC`: build a `Pose3d` from a string, query the block's size via the vtable slot `[vptr+0x18]`, scale by 0.5 (`0x004E5420: vmov.f32 s0,#0.5`), then a `tbb` switch on `FaceName` (0..5) computes the face placement. | `0x004E53BC` entry; `0x004E53E8: blx` PLT `0x4A40C0` (`Pose3d::Pose3d(string)`); `0x004E53FC: ldr r1,[r0,#0x18]` / `0x004E5400: blx r1` (vtable size getter); `0x004E5420: vmov.f32 s0,#5.000000e-01`; `0x004E5438: tbb [pc,r4]` | M11-003 | EXACT_SOURCE (entry + dispatch); the per-face bodies were not transcribed |
| G4.5 | `KnownMarker::_canonicalCorners3d` is a 48-byte `Quadrilateral<3,float>` in `.bss` at `0x0105E0F0` (GOT slot `0x103FEF8`). It is initialised by the static ctor at `0x004DD7D8..0x004DD81A` with the four points `(-0.5,0,-0.5)`, `(-0.5,0,0.5)`, `(0.5,0,0.5)`, `(0.5,0,-0.5)` (constants `0xBF000000` = -0.5, `0x3F000000` = +0.5, 0). Read by `KnownMarker` at `0x0087E2F2`. | data `0x0105E0F0`; init `0x004DD7E8: add r0,pc` (GOT `0x103FEF8`) / `0x004DD814: blx` PLT `0x4A4AC8` (`Quadrilateral<3,float>` ctor); point constants `0x004DD7E0: mov.w ip,#-0x41000000` / `0x004DD7E4: mov.w r3,#0x3f000000`; read `0x0087E2F2: add r0,pc` / `0x0087E2F4: ldr.w ip,[r0]` | M11-003 | EXACT_SOURCE (address + value) |

Note: both `.bss` constants are actually initialised by `init_array` static constructors inside the
shipped `.so` (`init_array` entries `0x004D6DA5` and `0x004DD7D9`); the earlier X1 note that the
bare symbol names prove nothing was correct, and the values above are the missing evidence.

---

## G5. M11-004 evidence — position-update thresholds

The strategy is `ReactionTriggerStrategyPositionUpdate`; its constructor
`0x00612168` writes the three fields, and its two predicate functions read them.

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G5.1 | **45 degrees.** Ctor writes `0x3F490FDB` = 0.785398 rad to `[this+0x2c]` via `Radians::Radians`. | `0x00612182: movw r1,#0xfdb`; `0x00612186: movt r1,#0x3f49`; `0x0061218A: blx` PLT `0x4A4294` (`Radians::Radians`) with r0 = `this+0x2c` (`0x0061217E: str r1,[r0],#0x2c`) | M11-004 | EXACT_SOURCE |
| G5.1u | Used as the rotation tolerance of `Pose3d::IsSameAs`: `r3 = [this+0x2c]`; the return is inverted (`eor r4,r0,#1`) so `ShouldReactToTarget_poseHelper` is true when the target has moved more than 45 deg (or more than 80 mm). | `0x006124E4: add.w r3,r5,#0x2c`; `0x006124F2: blx` PLT `0x4A7060` (`Pose3d::IsSameAs`); `0x006124F6: eor r4,r0,#1` | M11-004 | EXACT_SOURCE |
| G5.2 | **80 mm.** Ctor writes `0x42A00000` = 80.0f to `[this+0x38]`. | `0x006121A2: movt r3,#0x42a0`; `0x006121B4: strd r5,r3,[r6,#-0x10]` (r6 = `this+0x44`, so `[this+0x38]`) | M11-004 | EXACT_SOURCE |
| G5.2u | Used as the translation tolerance of `Pose3d::IsSameAs`: `r0 = [this+0x38]` is replicated into `(80,80,80)` and passed as `r2`. | `0x006124C6: ldr r0,[r5,#0x38]`; `0x006124CC: str.w r0,[r2,r1,lsl#2]` (3 times); `0x006124F2: blx` PLT `0x4A7060` | M11-004 | EXACT_SOURCE |
| G5.3 | **600000 ms.** Ctor writes `0x000927C0` = 600000 to `[this+0x34]`. | `0x00612198: movw r5,#0x27c0`; `0x006121A6: movt r5,#9`; `0x006121B4: strd r5,r3,[r6,#-0x10]` (r6-0x10 = `this+0x34`) | M11-004 | EXACT_SOURCE |
| G5.3u | Used in `ShouldReactToTarget`: `r8 = [this+0x34]`; `dt = Robot::GetLastImageTimeStamp() - storedTimestamp`; if `dt > 600000` the target is treated as stale (`orrs r0,r2`). | `0x00612592: ldr.w r8,[r4,#0x34]`; `0x00612584: blx` PLT `0x4AC6F4` (`GetLastImageTimeStamp`); `0x0061259C: subs r1,r6,r7`; `0x006125A0: cmp r1,r8`; `0x006125A2: it hi` / `0x006125A4: movhi r2,#1`; `0x006125A6: orrs r0,r2` | M11-004 | EXACT_SOURCE |

The record's title also mentions "BlockWorld connected-object rule, moving and rotating gates".
Those are separate from these three thresholds; the three threshold addresses above are what the
record's evidence prose ("80 mm, 45 degrees, 600000 ms thresholds in the position-update
strategy") lacked.

---

## G6. Shipped `vision_config.json`

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G6.1 | `InitialVisionModes.DetectingMarkers = true`. | `re-analysis/obb/assets/cozmo_resources/config/engine/vision_config.json:5` (`"DetectingMarkers" : true`) | M11-021 | EXACT_SOURCE |
| G6.2 | `InitialModeSchedules` has no `DetectingMarkers` entry, so markers use the default schedule and run every frame. | `.../vision_config.json:19-29` (entries: DetectingPets, DetectingOverheadEdges, CheckingQuality, DetectingLaserPoints, ComputingStatistics) | M11-021 | EXACT_SOURCE |

The OBB file is present in this clone, so the earlier X1 authority-6 caveat is now closed.

---

## What is still unread, and exactly what to read next

1. **G1** is complete; no follow-up needed for the live extractor. (The file's `CompressConnectedComponentSegmentIds` remap tail `0x00894AF4..` and the non-live binomial bodies remain unread but are not on the live path.)
2. **G2** is complete for the `Detect` post-processing. If the exact `Rectangle<int>` min/max update is wanted, read `0x0087855C` to its end; and if the `ImageBase+0x3c` field's name is wanted, it is an `ImageBase<u8>` scalar copied by `operator=` at `0x0086EF2C` (semantics: image id/timestamp; the `ObservedMarker` ctor stores it at `+4`).
3. **G3** remains partially unread:
   - the bilinear pixel weighting, `0x008C60B6..0x008C610A` (against `Array<u8>` base `[sp+0x44]`, stride `[sp+0x48]`);
   - the 8-element design vector filled at `0x008C5C3E..0x008C5E80` (the `[sp+0x25c]` array) and the `DotDivide` operand plumbing at `0x008C62DC..0x008C6348`.
   These are the only two ranges needed before M11-005's numerics can be called EXACT_SOURCE.
4. **G4**: `Block::LookupBlockInfo` (`0x004E4C8C`) and `Block::AddFace` (`0x004E53BC`) were read at entry/dispatch only; read them fully if the record needs the per-face and per-type values.
5. **G5** is complete for the three thresholds; the "moving/rotating gates" part of M11-004 is a separate read (X1 section 3) and was not part of G5.

## Existing records contradicted or too weak (from this pass)

- **M11-003**: the record's evidence is bare symbol names. The symbols are real and their
  values are now recovered (G4.1, G4.5), but the record's citation must be replaced with the
  addresses; `_kDefaultHeadCamRotation` and `_canonicalCorners3d` live in `.bss` and are set by
  the static ctors, not by a literal in `.data` (a reviewer looking only at the symbol's section
  would wrongly conclude they are zero).
- **M11-004**: the evidence is prose; the three thresholds are now cited (G5.1/2/3).
- **M11-005**: the orchestration is EXACT_SOURCE; the two G3 numerics ranges are still
  RECOVERABLE_GAP, so the record should not be upgraded to EXACT_SOURCE for the refinement
  numerics until they are read.
- **M11-032**: its `unresolved` clause "the dark-mask multiplier 0xCCCC belongs to the non-live
  path" remains contradicted (G1.8; X1 section 3), and its evidence is still a bare address list.
- **M11-022**: now read (G2); the record can be settled once the manifest evidence is replaced.

*Read-only; nothing outside `.scratch/I-M11-gap1/` and this report file was changed.*


## Appendix C: gap pass 2 extraction report (20260927-I-M11-gap2-extraction.md)

# I-M11 gap pass 2 - extraction (read-only)

Job: gap pass 2 for the M11-vision integration job (I-M11). Scope: close G7..G10 of
`re-analysis/research/20260927-I-M11-gap1-extraction.md` (the "what is still unread" list).
Agent: opencode (DeepSeek), window 3. Date: 2026-09-27.
Scratch: `.scratch/I-M11-gap2/` (Thumb disassembler `vdis.py`, raw dumps).

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204).
The vision region is Thumb-2; disassembled with capstone `CS_MODE_THUMB` (base 0). All
addresses below are file VAs. The Ghidra decompilation was not used. Where a step is fully
read it is EXACT_SOURCE; where it is not, the exact range to read is named.

Row form: `| # | what the original does | citation | record | class |`.

---

## G7. Two X1 rows re-cited (required)

The X1 pass wrote two branch targets that do not match the bytes. Both are confirmed
against the instruction encoding and the `.plt` relocation map.

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G7.1 | **X1 V14 correction.** At `0x00892EF8` the bytes are `3e f4 16 e8`, which capstone decodes as `blx #0x4d0f28` (the PLT stub for `IsQuadrilateralReasonable`), not `0x4d0e60`. `0x4d0e60` is not a PLT entry (the neighbouring stubs are `0x4d0e5c` = `UpsampleByPowerOfTwoBilinear<4>` and `0x4d0e68` = `UpsampleByPowerOfTwoBilinear<5>`). The function body is `0x00892B18` (`IsQuadrilateralReasonable(Quadrilateral<short> const&, int, int, int, int, int, bool&)`). Its three named parameters are **not** literals at `0x00892B18`; they are `MarkerDetector::Parameters` fields: `minQuadArea = 25` at `Parameters+0x30`, `symmetry = 512` (8.8 fixed) at `Parameters+0x34`, `minDistanceFromEdge = 2` at `Parameters+0x38`. Written in `Parameters::Initialize`: `0x00875338: stm.w r1,{r3,r5,lr}` with `r1 = r0+0x28` (`0x0087532E: add.w r1,r0,#0x28`), `r5 = 5` (`0x00875328`), `lr = 0x19` (`0x0087532A`) -> `+0x30 = 25`; `0x00875340: strd r6,r7,[r0,#0x34]` with `r6 = 0x200` (`0x00875332`), `r7 = 2` (`0x00875336`) -> `+0x34 = 512`, `+0x38 = 2`. Read on the live path in `DetectFiducialMarkers`: `0x00898E00: ldrd r6,r4,[r7,#0x28]`; `0x00898E06: ldrd r5,r0,[r7,#0x30]` (r5 = 25, r0 = 512); `0x00898E0A: str r0,[sp,#0x74]`; `0x00898E0E: ldr r7,[r7,#0x38]` (2); `0x00898E1E: mov r3,r7`; `0x00898E28: ldr r2,[sp,#0x74]`; `0x00898E2A: mov r1,r5`; `0x00898E2C: blx #0x4d13c0` (`ComputeQuadrilateralsFromConnectedComponents`). (The task's "mov r3,r7" is at `0x00898E1E`, not at `0x00898E2A`.) The corrected V14 citation is: `0x00892EF8: blx #0x4d0f28` (PLT `IsQuadrilateralReasonable`); params `0x00875338`/`0x00875340` -> `0x00898E00`/`0x00898E0E`/`0x00898E1E`/`0x00898E2A` -> `0x00898E2C: blx #0x4d13c0`. | `0x00892EF8` bytes `3e f4 16 e8`; `0x00892B18` (`IsQuadrilateralReasonable` body, `push.w {r4,r5,r6,r7,r8,sb,sl,fp,lr}`); `0x0087532E`/`0x00875338`/`0x00875340`; `0x00898E00`/`0x00898E06`/`0x00898E0E`/`0x00898E1E`/`0x00898E28`/`0x00898E2A`/`0x00898E2C`; PLT `0x4d0f28` = `IsQuadrilateralReasonable` | M11-018, M11-028 | EXACT_SOURCE |
| G7.2 | **X1 V18 correction.** At `0x0089FEA6` the bytes are `31 f4 fa eb`, which capstone decodes as `blx #0x4d169c` (the PLT stub for `RefineQuadrilateral`), not `0x008c55e0` (which is the body). Confirmed against the `.rel.plt` map: `0x4d169c = RefineQuadrilateral(...)`. The call site passes `r0 = sb` (`0x0089FEA4: mov r0,sb`) with the stack arguments set at `0x0089FE90..0x0089FEA0`. | `0x0089FEA6` bytes `31 f4 fa eb`; `0x0089FEA4: mov r0,sb`; PLT `0x4d169c`; body `0x008c55e0` | M11-005, M11-031 | EXACT_SOURCE |

---

## G8. The M11-005 numerics, finished

All three ranges are now read. `RefineQuadrilateral` body is `0x008C55E0..0x008C656C`;
the design-vector fill is at `0x008C5CE6..0x008C5D86`; the bilinear read is at
`0x008C609A..0x008C610A`; the H22 `DotDivide` is at `0x008C62A0..0x008C634A`.

### G8.1 Bilinear pixel read (`0x008C609A..0x008C610A`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.1a | Image `Array<u8>` layout used here: `0x008C609A: ldrd r2,r1,[sp,#0x44]` loads `r2 = base = [sp+0x44]` and `r1 = stride = [sp+0x48]`; `0x008C5F30: str r1,[sp,#0x48]` and `0x008C5F32: ldr r0,[r0,#0x10]` / `0x008C5F34: str r0,[sp,#0x44]` take them from the image (`+8` = stride, `+0x10` = data). | `0x008C5F30`/`0x008C5F32`/`0x008C5F34`; `0x008C609A` | M11-005 | EXACT_SOURCE |
| G8.1b | The projected sample `(X, Y)` (s22 = X, s17 = Y) is bounded by `floorX = floor(X)`, `ceilX = ceil(X)`, `floorY = floor(Y)`, `ceilY = ceil(Y)`, and the four guard branches reject out-of-image samples: `floor(X) < 0` (`0x008C5FB4: bmi`), `ceil(Y) > rows-1` (`0x008C5FCE: bgt`, bound `[sp+0x68] = s30-1 = image rows-1`, set at `0x008C5EB4`), `floor(Y) < 0` (`0x008C5FDE: bmi`), `ceil(X) > cols-1` (`0x008C5FF2: bgt`, bound `[sp+0x64] = s17-1 = image cols-1`, set at `0x008C5EBC`). On any violation the sample is skipped (`0x008C618A`). | `0x008C5FB4`/`0x008C5FCE`/`0x008C5FDE`/`0x008C5FF2`; bounds `0x008C5EB4`/`0x008C5EBC` | M11-005 | EXACT_SOURCE |
| G8.1c | The four pixels read are `img[floorY, floorX]` (r3/s6), `img[floorY, floorX+1]` (r0/s4), `img[ceilY, floorX]` (r7/s2), `img[ceilY, floorX+1]` (r1/s0), via `mla r0,r1,r5,r2` (r5 = floorY) and `mla r1,r1,r7,r2` (r7 = ceilY), then `ldrb` at `+r2` (r2 = floorX) and `+1`. | `0x008C609E`/`0x008C60A8`; `0x008C60B6: ldrb r7,[r1,r2]`; `0x008C60BA: ldrb r3,[r0,r2]`; `0x008C60BC: ldrb r1,[r1,#1]`; `0x008C60D2: ldrb r0,[r0,#1]` | M11-005 | EXACT_SOURCE |
| G8.1d | Weights: `fracX = s22 = X - floor(X)` (`0x008C6026`), `1-fracX = s30` (`0x008C6066: vsub.f32 s30,s18,s22`, s18 = 1.0), `fracY = s17 = Y - floor(Y)` (`0x008C6022`), `1-fracY = s24` (`0x008C6062: vsub.f32 s24,s18,s17`). The value is `(1-fracY)*[(1-fracX)*img[floorY,floorX] + fracX*img[floorY,floorX+1]] + fracY*[(1-fracX)*img[ceilY,floorX] + fracX*img[ceilY,floorX+1]]`. Pairing in the instructions: `0x008C60E6: vmul s0,s22,s0` (ceilY,col+1), `0x008C60EE: vmul s2,s30,s2` (ceilY,col), `0x008C60F2: vmul s4,s22,s4` (floorY,col+1), `0x008C60F6: vmul s6,s30,s6` (floorY,col), `0x008C60FA: vadd s0,s2,s0`, `0x008C60FE: vadd s2,s6,s4`, `0x008C6102: vmul s0,s17,s0`, `0x008C6106: vmul s2,s24,s2`, `0x008C610A: vadd s0,s2,s0`. | `0x008C6062`/`0x008C6066`/`0x008C60E6..0x008C610A` | M11-005 | EXACT_SOURCE |
| G8.1e | The residual is `value - [sp+0x40]` then scaled by `1/255` (`0x008C6112: vsub.f32 s0,s0,s2`; `0x008C6116: vmul.f32 s0,s0,s2`; constant at `0x8C6474 = 0x3b808081 = 0.003921569`). `[sp+0x40]` is `(bright+dark)/2` (`0x008C5EAC: vstr s0,[sp,#0x40]`). | `0x008C6112`/`0x008C6116`; data `0x8C6474` | M11-005 | EXACT_SOURCE |

### G8.2 The 8-element design vector (`[sp+0x25c]`, filled `0x008C5CE6..0x008C5D86`)

The eight rows of `Array<float>(8, N)` are pointed to by `[sp+0x25C..0x27B]`
(`0x008C5CB2: str.w r0,[r3,r2,lsl#2]`, `r3 = sp+0x25c`, advanced by the array stride
`[sp+0x288]`). Per sample, the loop loads `x = xSquare` (array at `[sp+0x2F0]`, the
"xSquare Array." of the allocation error at `0x8C5BD0`), `y = ySquare` (array at
`[sp+0x2D8]`, "ySquare Array." at `0x8C5C04`), `Tx` (array at `[sp+0x2C0]`, "Tx Array." at
`0x8C5C1C`) and `Ty` (array at `[sp+0x2A8]`, "Ty Array." at `0x8C5C2C`).

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.2a | Inputs: `0x008C5CEC: vldr s0,[lr]` (s0 = ySquare sample, lr = `[sp+0x5c]` = ySquare data), `0x008C5CF4: vldr s2,[r2]` (s2 = xSquare sample, r2 = `[sp+0x50]` = xSquare data), `0x008C5CFE: vldr s14,[r3]` (s14 = Tx sample, r3 = old `sl` = Tx data), `0x008C5D06: vldr s12,[r6]` (s12 = Ty sample, r6 = old `sb` = Ty data). | `0x008C5CEC`/`0x008C5CF4`/`0x008C5CFE`/`0x008C5D06`; data pointers `0x008C5C66`/`0x008C5C72`/`0x008C5C76`/`0x008C5C7A` | M11-005 | EXACT_SOURCE |
| G8.2b | Entry 0 (`[sp+0x25C]`) = `Tx * x`: `0x008C5D16: vmul.f32 s1,s2,s14`, stored `0x008C5D32: vstr s1,[r4]`. | `0x008C5D16`/`0x008C5D32` | M11-005 | EXACT_SOURCE |
| G8.2c | Entry 1 (`[sp+0x260]`) = `Tx * y`: `0x008C5D1A: vmul.f32 s3,s0,s14`, stored `0x008C5D3C: vstr s3,[r7]`. | `0x008C5D1A`/`0x008C5D3C` | M11-005 | EXACT_SOURCE |
| G8.2d | Entry 2 (`[sp+0x264]`) = `Tx`: `0x008C5D4C: vstr s14,[r4]`. | `0x008C5D4C` | M11-005 | EXACT_SOURCE |
| G8.2e | Entry 3 (`[sp+0x268]`) = `Ty * x`: `0x008C5D1E: vmul.f32 s2,s2,s12`, stored `0x008C5D52: vstr s2,[sl]`. | `0x008C5D1E`/`0x008C5D52` | M11-005 | EXACT_SOURCE |
| G8.2f | Entry 4 (`[sp+0x26C]`) = `Ty * y`: `0x008C5D36: vmul.f32 s0,s0,s12`, stored `0x008C5D5A: vstr s0,[sb]`. | `0x008C5D36`/`0x008C5D5A` | M11-005 | EXACT_SOURCE |
| G8.2g | Entry 5 (`[sp+0x270]`) = `Ty`: `0x008C5D62: vstr s12,[r1]`. | `0x008C5D62` | M11-005 | EXACT_SOURCE |
| G8.2h | Entry 6 (`[sp+0x274]`) = `-(Tx*x*x + Ty*x*y)`: intermediates `0x008C5CFA: vmul s8,s0,s0` (y^2), `0x008C5D02: vnmul s6,s2,s2` (-x^2), `0x008C5D0A: vmul s4,s2,s0` (x*y), `0x008C5D22: vmul s8,s8,s12` (y^2*Ty), `0x008C5D26: vmul s6,s6,s14` (-x^2*Tx), `0x008C5D2A: vmul s4,s4,s12` (x*y*Ty), then `0x008C5D42: vsub.f32 s4,s6,s4` = `-(x^2*Tx + x*y*Ty)`, stored `0x008C5D68: vstr s4,[r0]`. | `0x008C5D02`/`0x008C5D0A`/`0x008C5D22`/`0x008C5D26`/`0x008C5D2A`/`0x008C5D42`/`0x008C5D68` | M11-005 | EXACT_SOURCE |
| G8.2i | Entry 7 (`[sp+0x278]`) = `-(Tx*x*y + Ty*y*y)`: `0x008C5D10: vnmul s10,s2,s0` (-x*y), `0x008C5D2E: vmul s10,s10,s14` (-x*y*Tx), `0x008C5D48: vsub.f32 s6,s10,s8` = `-(x*y*Tx + y^2*Ty)`, stored `0x008C5D6E: vstmia r5!,{s6}`. | `0x008C5D10`/`0x008C5D2E`/`0x008C5D48`/`0x008C5D6E` | M11-005 | EXACT_SOURCE |

So, in order: `Tx*x, Tx*y, Tx, Ty*x, Ty*y, Ty, -(Tx*x*x + Ty*x*y), -(Tx*x*y + Ty*y*y)`,
matching the manifest's M11-005 evidence exactly.

### G8.3 `DotDivide` operand plumbing (`0x008C62A0..0x008C634A`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.3a | Gate: `s0 = |H22 - 1|`; skip if `< 1e-5`. `0x008C62AA: vldr s0,[r2,#8]`; `0x008C62B0: vadd.f32 s0,s0,s2` (s2 = -1.0); `0x008C62BC: vneg.f32 s2,s0`; `0x008C62C0: it mi` / `0x008C62C2: vmovmi.f32 s0,s2`; `0x008C62C6: vldr s2,[pc,#0x23c]` -> `0x8C6504 = 0x3727C5AC = 1e-5`; `0x008C62D0: vcmpe.f32 s0,s2`; `0x008C62DA: bmi #0x8c634e` (skip the divide). | `0x008C62AA..0x008C62DA`; data `0x8C6504` | M11-005 | EXACT_SOURCE |
| G8.3b | The numerator expression is a `ConstArraySlice<float>` over the refined homography `Array<float>` at `[sp+0x200]` (`dim0/dim1/stride/flags/data` = `[sp+0x200]`/`[sp+0x204]`/`[sp+0x208]`/`[sp+0x20C]`/`[sp+0x210]`). A copy of that descriptor is built at `[sp+0x304..0x314]` (`0x008C62DC: ldrd r2,r3,[sp,#0x200]`; `0x008C62E4: strd r2,r3,[sp,#0x304]`; `0x008C62E8: strd r1,r5,[sp,#0x30c]`; `0x008C62EE: str r0,[sp,#0x314]`) and the `ConstArraySlice<float>` ctor (`0x008C62F2: blx #0x4d1c48`) builds the expression at `[sp+0xa0]`. | `0x008C62DC..0x008C62F2` | M11-005 | EXACT_SOURCE |
| G8.3c | The divisor is `H22 = *(float*)(data + 2*stride + 8)`, i.e. element `[2][2]`: `0x008C6304: add.w r7,r5,r3,lsl#1` (r5 = data `[sp+0x210]`, r3 = stride `[sp+0x208]`); `0x008C6308: ldr r7,[r7,#8]`; `0x008C630A: str r7,[sp,#0x48]`. It is passed as the `float` operand in `r1` at `0x008C6344: ldrd r2,r1,[sp,#0x44]`. | `0x008C6304`/`0x008C6308`/`0x008C630A`/`0x008C6344` | M11-005 | EXACT_SOURCE |
| G8.3d | The output is an `ArraySlice<float>` over the same homography: `0x008C6310: add r0,sp,#0x6c`; `0x008C6312: blx #0x4d1c54` (`ArraySlice<float>::ArraySlice(Array<float>)`); its fields are forwarded to `r2`/`r3` at `0x008C6316..0x008C6348`. | `0x008C6310..0x008C6348` | M11-005 | EXACT_SOURCE |
| G8.3e | The call is `0x008C634A: blx #0x4d22b4` = `Matrix::Elementwise::ApplyOperation<float, DotDivide<float,float,float>, float>(ConstArraySliceExpression<float> const&, float, ArraySlice<float>)`; i.e. the whole 3x3 is divided elementwise by `H22`, in place. | `0x008C634A`; PLT `0x4d22b4` | M11-005 | EXACT_SOURCE |

### G8.4 `Matrix::MakeSymmetric` (`0x0088DDC0..0x0088DE30`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.4a | Requires a square array: `0x0088DDC4: ldrd ip,r2,[r0]` (dim0, dim1); `0x0088DDC8: cmp ip,r2`; `0x0088DDCA: bne #0x88de10` -> log `"Invalid objects"` (`0x88de1c`) and return `0x5000000`. If `dim0 < 1` return 0. | `0x0088DDC4..0x0088DE0E` | M11-005 | EXACT_SOURCE |
| G8.4b | Mirror direction depends on the bool arg: if `bool == false` (`0x0088DDE0: cmp r1,#0` not taken) the inner loop runs `r4 = 0..r3-1` and writes `A[r3][r4] = A[r4][r3]` (copies the upper triangle into the lower); if `bool != 0` it runs `r4 = r3+1..dim0-1` and writes the same (copies the lower triangle into the upper). Body: `0x0088DDF2: mla r7,r5,r4,r6` (row r4), `0x0088DDF6: mla r5,r3,r5,r6` (row r3), `0x0088DDFA: ldr r6,[r7,r3,lsl#2]` (A[r4][r3]), `0x0088DDFE: str r6,[r5,r4,lsl#2]` (A[r3][r4]). | `0x0088DDE0..0x0088DE08` | M11-005 | EXACT_SOURCE |

`RefineQuadrilateral` calls it with `false` (`0x008C61DC: movs r1,#0`; `0x008C61E0: blx #0x4d0b50`),
so it mirrors the upper triangle into the lower.

### G8.5 `Matrix::SolveLeastSquaresWithCholesky` (`0x0088DE68..0x0088E142`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G8.5a | Guards: `AreValid(A, b)` (`0x0088DE8C`); if false -> log `"Invalid objects"` and return `0x4000000`; require `A.dim0 == A.dim1` (`0x0088DE96: ldr r0,[fp,#4]` / `0x0088DE9C: cmp r4,r0` / `bne 0x88e102` -> `0x5000000`) and `b.dim1 == A.dim0` (`0x0088DEA2: ldr r0,[sl,#4]` / `0x0088DEA8: cmp r0,ip` / `bne 0x88e112` -> `0x5000000`); require `dim0 >= 1`. `*converged = false` at `0x0088DE86`. | `0x0088DE86..0x0088DEB0`; error returns `0x0088E0FC`/`0x0088E12C` | M11-005 | EXACT_SOURCE |
| G8.5b | Factorisation, **pivot order natural** `i = 0..n-1` (outer loop `0x0088DEC2`..`0x0088DF86`). For each `j < i`: `L[i][j] = (A[i][j] - sum_{k<j} L[i][k]*L[j][k]) / L[j][j]`, written in place to `A[i][j]` (`0x0088DEDE..0x0088DF22`; the inner sum `0x0088DEF4: vldr s2,[r3]` / `0x0088DEFA: vldr s4,[r0]` / `0x0088DF04: vmul` / `0x0088DF08: vsub`; the divide-by-pivot `0x0088DF1A: vmul s0,s0,s2` with `s2 = A[j][j]`). | `0x0088DEDE..0x0088DF22` | M11-005 | EXACT_SOURCE |
| G8.5c | Pivot: `p = A[i][i] - sum_{k<i} L[i][k]^2` (`0x0088DF24..0x0088DF46`; `0x0088DF2A: vldr s0,[r5]` = A[i][i]; `0x0088DF3C: vmul.f32 s2,s2,s2`; `0x0088DF40: vsub.f32 s0,s0,s2`). | `0x0088DF24..0x0088DF46` | M11-005 | EXACT_SOURCE |
| G8.5d | **Failure condition:** if `p < FLT_EPSILON` the factorisation stops: `0x0088DF50: vcmpe.f32 s0,s18` with `s18 = [pc,#0x2f4]` -> `0x0088E1B0 = 0x34000000 = 1.1920929e-07` (`0x0088DEB8`); `0x0088DF58: bmi.w #0x88e132` -> `0x0088E132: movs r0,#1` / `0x0088E134: strb.w r0,[sb]` (sets the `converged` out-flag true) / `0x0088E138: movs r0,#0` (returns Result 0). Otherwise `A[i][i] = 1/sqrt(p)` (`0x0088DF5C: vsqrt.f32 s2,s0`; `0x0088DF7A: vdiv.f32 s0,s16,s2` with s16 = 1.0; `0x0088DF82: vstr s0,[r5]`). | `0x0088DEB8`; `0x0088DF50..0x0088DF82`; `0x0088E132..0x0088E138`; data `0x88E1B0` | M11-005 | EXACT_SOURCE |
| G8.5e | Forward substitution (`0x0088DF88..0x0088DFF6`): for each `i`, for each `b` row `r`: `b[r][i] = (b[r][i] - sum_{k<i} A[i][k]*b[r][k]) * A[i][i]` (the last factor is the stored `1/L[i][i]`). | `0x0088DFC6..0x0088DFEC` | M11-005 | EXACT_SOURCE |
| G8.5f | Backward substitution (`0x0088DFF8..0x0088E090`): for `i = n-1..0`, for each `b` row: `b[r][i] = (b[r][i] - sum_{k>i} A[i][k]*b[r][k]) * A[i][i]`. | `0x0088E028..0x0088E080` | M11-005 | EXACT_SOURCE |
| G8.5g | Optional normalisation (`0x0088E092..0x0088E0DE`) runs only if the bool argument (`[sp+0xc]`) is 1: invert the diagonal (`0x0088E0C2: vdiv.f32 s0,s16,s0` / `0x0088E0C6: vstr s0,[r0,#-4]`) and `memclr4` the strict triangle. `RefineQuadrilateral` passes `0` (`0x008C61F2: movs r2,#0`), so this block is **not** run on the live path. | `0x0088E092..0x0088E0DE`; `0x008C61F2` | M11-005 | EXACT_SOURCE |

Note (behaviour to check, not settled here): the `converged` out-flag is set true only on
`p < FLT_EPSILON`, and `RefineQuadrilateral` treats the flag being true as the accept path
(`0x008C62CA..0x008C62D4` stores `1` at `[sp+0x60]`; `0x008C6418: cmp r0,#1` / `0x008C641C: bne 0x8c644a`
takes the accept branch). So a near-singular normal matrix takes the same branch as a
completed solve. This is reported mechanically; the manager may want to confirm the intended
semantics before M11-005's failure path is called EXACT_SOURCE.

---

## G9. M11-003 remainder - cube size, marker size, face poses and codes

### G9.1 `Block::LookupBlockInfo` (`0x004E4C8C..0x004E5104`)

The function builds a `std::map<ObjectType, BlockInfoTableEntry_t>` of **four** entries and
returns `map.find(ObjectType)+0x14` (the value), or the map's end+0x14 if absent
(`0x004E50C2..0x004E50FE`). Each entry is keyed by `ObjectType` (1,2,3,4), carries a name,
a colour, three equal `44.0f` dimensions and a `std::vector<BlockFaceDef_t>` of six 0x10-byte
records.

| # | ObjectType key | name string | colour | dimensions | entry build cite |
|---|---|---|---|---|---|
| G9.1a | 1 | `"LIGHTCUBE1"` (`0x4E5314`) | `NamedColors::ORANGE` (`GOT 0x103E7D8`) | `(44.0, 44.0, 44.0)` | `0x004E4D2E: str r0,[sp,#0xc0]` (r0=1); `0x004E4CC4` name; `0x004E4CDA` colour; `0x004E4CD6: movt r1,#0x4230` -> `0x42300000 = 44.0`, `0x004E4CDC: strd r1,r1,[sp,#0xa0]` / `0x004E4CE0: str r1,[sp,#0xa8]`; copied to entry `+0x14/+0x18/+0x1c` at `0x004E4D58..0x004E4D60` |
| G9.1b | 2 | `"LIGHTCUBE2"` (`0x4E5328`) | `NamedColors::YELLOW` (`GOT 0x103E7DC`) | `(44.0, 44.0, 44.0)` | `0x004E4D8E: movt r1,#0x4230`; entry key 2 at `0x004E4DF0: str r0,[sp,#0xf0]` |
| G9.1c | 3 | `"LIGHTCUBE3"` (`0x4E533C`) | `NamedColors::RED` (`GOT 0x103E7E0`) | `(44.0, 44.0, 44.0)` | `0x004E4E46: movt r1,#0x4230`; entry key 3 at `0x004E4EA4: str r0,[sp,#0x120]` |
| G9.1d | 4 | `"LIGHTCUBE_GHOST"` (`0x4E5350`) | `NamedColors::WHITE` (`GOT 0x103E7E4`) | `(44.0, 44.0, 44.0)` | `0x004E4EFA: movt r1,#0x4230`; entry key 4 at `0x004E4F58: str r0,[sp,#0x150]` |

The `44.0f` value is the block edge: `Block::Block` halves the size getter's three components
in `AddFace` (`0x004E5402: vldr s16,[r0,#4]`; `0x004E540E: vldr s18,[r0,#8]`; `0x004E5424: vldr s2,[r0]`;
`0x004E5420: vmov.f32 s0,#0.5`; `0x004E542C/0x004E5430/0x004E5434: vmul`), and the face
translations are all at `+-22` (see G9.3), so the three components are `44.0` each. The
getter is a `Block` virtual at vtable slot `+0x18` (`0x004E53FC: ldr r1,[r0,#0x18]` /
`0x004E5400: blx r1`); the exact `this` offset of the returned `Point<3,float>` was not
pinned (the `BlockInfo` entry stores the three values at `+0x14/+0x18/+0x1c`; the block copy
at `0x004E5F80` moves `BlockInfo+0x10/+0x14/+0x18` to `Block+0x88/+0x8c/+0x90`). The value is
settled; only the field name is a minor open point.

### G9.2 Marker size

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G9.2a | Each `BlockFaceDef_t` (0x10 bytes) is `{ u32 faceName @0; u32 markerType @4; float size @8; u32 packed @0xc }`; the six records per cube all carry `size = 25.0` (`0x41C80000`). | `0x004E4C8C` table: `+0x08 = 00 00 c8 41` in every record; `0x004E4C40..0x004E4D60` data at `0xC45C40` (LIGHTCUBE1), `0xC45CA0` (2), `0xC45D00` (3), `0xC45D60` (GHOST) | M11-003 | EXACT_SOURCE |
| G9.2b | `Block::Block` passes that `float` as `AddFace`'s third argument (`0x004E6006: ldr r3,[r2,#8]` / `0x004E6010: blx #0x4a4828`), and `AddFace` builds a `Point<2,float>` from it (both components equal) and hands it to `ObservableObject::AddMarker` (`0x004E5648: vmov s0,sb`; `0x004E565A..0x004E5664: vstr s0,[r2]` twice; `0x004E566E: blx #0x4a4798`). | `0x004E6006`/`0x004E6010`; `0x004E5648..0x004E566E` | M11-003 | EXACT_SOURCE |
| G9.2c | `KnownMarker::KnownMarker(short const&, Pose3d const&, Point<2,float> const&)` (`0x0087E22C`) stores that `Point<2,float>` at `KnownMarker+0x10` (`0x0087E26E: ldrd r0,r1,[r7]` / `0x0087E272: strd r0,r1,[r5,#0x10]`). `KnownMarker::Get3dCorners` scales the canonical `+-0.5` corners by `size.x`/`size.y` (`0x0087E312: vldr s4,[r1,#0x10]`; `0x0087E322: vldr s0,[r1,#0x14]`; `0x0087E31A`/`0x0087E326: vmul`), so the marker's physical extent is `25 mm` square. | `0x0087E22C`; `0x0087E26E`/`0x0087E272`; `0x0087E308..0x0087E332` | M11-003 | EXACT_SOURCE |

So the marker's physical size on the live path is **25.0** (both in-plane dimensions), taken
from the `BlockFaceDef_t` size field; there is no separate global marker-size constant.

### G9.3 `Block::AddFace` per-face bodies (`0x004E53BC..0x004E5688`)

Prologue: `r4 = FaceName`, `r8 = &MarkerType`, `sb = size`; the switch is
`0x004E5438: tbb [pc,r4]` with table at `0x4E543C` (`03 a0 57 7c 2a cd`), i.e.
`target = 0x4E543C + 2*byte`. Half-sizes: `s18 = size[0]/2`, `s16 = size[1]/2`,
`s20 = size[2]/2` (all `22.0` for the 44 mm cube). Each case builds a `Pose3d` with
`Pose3d::Pose3d(Radians, Point3 axis, Point3 translation, string)` (`0x4A478C`).

| FaceName | case VA | rotation | axis | translation | cite |
|---|---|---|---|---|---|
| 0 | `0x004E5442` | `-pi/2` (`r1 = 0xBFC90FDB`, `0x004E5448: movt r1,#0xbfc9`) | `Z_AXIS_3D()` (`0x004E5450`) | `(-22, 0, 0)` (`0x004E5456: vneg.f32 s0,s18`; `0x004E5466: vstr s0,[sp,#0x2c]`) | `0x004E5442..0x004E5482` |
| 1 | `0x004E557C` | `+pi` (`r1 = 0x40490FDB`, `0x004E5582: movt r1,#0x4049`) | `Z_AXIS_3D()` (`0x004E558A`) | `(0, 22, 0)` (`0x004E5590: vstr s16,[sp,#0x30]`) | `0x004E557C..0x004E55B8` |
| 2 | `0x004E54EA` | `+pi/2` (`r1 = 0x3FC90FDB`, `0x004E54F0: movt r1,#0x3fc9`) | `Z_AXIS_3D()` (`0x004E54F8`) | `(22, 0, 0)` (`0x004E5506: vstr s18,[sp,#0x2c]`) | `0x004E54EA..0x004E5526` |
| 3 | `0x004E5534` | `0` (`0x004E5536: movs r1,#0`) | `Z_AXIS_3D()` (`0x004E553C`) | `(0, -22, 0)` (`0x004E5542: vneg.f32 s0,s16`; `0x004E5552: vstr s0,[sp,#0x30]`) | `0x004E5534..0x004E556E` |
| 4 | `0x004E5490` | `2*pi/3` (`r1 = 0x40060A92`, `0x004E5496: movt r1,#0x4006`) | `(-0.57735, +0.57735, -0.57735)` (`0x004E54AC`/`0x004E54B6`/`0x004E54B8`; const `0x3F13CD3A`/`0xBF13CD3A`) | `(0, 0, 22)` (`0x004E54BE: vstr s20,[sp,#0x28]`) | `0x004E5490..0x004E54DC` |
| 5 | `0x004E55D6` | `2*pi/3` (`r1 = 0x40060A92`) | `(+0.57735, -0.57735, -0.57735)` (`0x004E55F2`/`0x004E55F8`/`0x004E55FC`) | `(0, 0, -22)` (`0x004E55E4: vneg.f32 s0,s20`; `0x004E5608: vstr s0,[sp,#0x28]`) | `0x004E55D6..0x004E5626` |

At the end of `AddFace` the marker is added: `0x004E564C: ldr.w r1,[r8]` (the `MarkerType`'s
low `short`), `0x004E5654: strh.w r1,[sp,#0x2c]`, and
`0x004E566E: blx ObservableObject::AddMarker(short const&, Pose3d const&, Point<2,float> const&)`
(PLT `0x4A4798`); the returned index is stored at `block + 0x70 + faceName*4`
(`0x004E5672: add.w r1,r5,r4,lsl#2` / `0x004E5676: str r0,[r1,#0x70]`).

### G9.4 Face-to-marker-code map

`Block::Block(ObjectFamily, ObjectType)` (`0x004E5F50`) iterates the `BlockInfo`'s face vector:
`0x004E5FF8: ldrd r2,r5,[r0,#0x20]` (begin/end), `0x004E6002: ldr r1,[r2]` = faceName,
`0x004E6004: adds r7,r2,#4` = `&MarkerType`, `0x004E6006: ldr r3,[r2,#8]` = size,
`0x004E6010: blx AddFace`. The `MarkerType`'s code is its first `short`. The records give:

| cube (key) | face 0 | face 1 | face 2 | face 3 | face 4 | face 5 |
|---|---|---|---|---|---|---|
| LIGHTCUBE1 (1) | 6 | 7 | 4 | 8 | 9 | 5 |
| LIGHTCUBE2 (2) | 12 | 13 | 10 | 14 | 15 | 11 |
| LIGHTCUBE3 (3) | 18 | 19 | 16 | 20 | 21 | 17 |
| LIGHTCUBE_GHOST (4) | 39 | 39 | 39 | 39 | 39 | 39 |

The `u32` at record `+0xc` (e.g. `0x00000F05`, `0x00000F00`, `0x00000F0F`) is **not** passed to
`AddFace` on this path: the constructor supplies `0,0` for the two `u8` arguments
(`0x004E600C: strd r6,r6,[sp]` with `r6 = 0` at `0x004E6000`), and `AddFace` does not read
them in the body read here. So the packed word is unused by this constructor.

### G9.5 KnownMarker canonical corners and code

`KnownMarker::_canonicalCorners3d` is the 48-byte `Quadrilateral<3,float>` at `0x0105E0F0`
(already in G4: `(-0.5,0,-0.5)`, `(-0.5,0,0.5)`, `(0.5,0,0.5)`, `(0.5,0,-0.5)`), and
`KnownMarker::Get3dCorners` (`0x0087E2E8`) copies it and scales x by `size.x` (`+0x10`) and z
by `size.y` (`+0x14`), then applies the marker pose (`0x0087E336: GetTransform`,
`0x0087E33E: Transform3d::ApplyTo<float>`). The `KnownMarker` code is the `short` stored at
`KnownMarker+0` (`0x0087E238: ldrh r0,[r1]` / `0x0087E23E: strh r0,[r4],#4`).

---

## G10. M11-004 remainder - connected-object rule, moving and rotating gates

### G10.1 The connected-object rule (`BlockWorld::AddAndUpdateObjects`, `0x00620AD4`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G10.1a | For an observed active object, the engine asks whether a **connected** active object with the same `ObjectID` exists: `0x00620E70: ldr r0,[sp,#0x118]` (the observed object); `0x00620E72: add.w r1,r0,#0x14` (`&ObjectID`); `0x00620E76: mov r0,fp` (`BlockWorld`); `0x00620E78: blx #0x4a6fb8` (`BlockWorld::GetConnectedActiveObjectByIdHelper(ObjectID const&) const`, body `0x0061F58C`). | `0x00620E70..0x00620E78`; body `0x0061F58C` | M11-004 | EXACT_SOURCE |
| G10.1b | If the lookup returns null the engine warns and rate-limits; it does **not** drop the object in this path. `0x00620E7C: cbnz r0,#0x620ede` (found -> continue); otherwise `0x00620E88: ldr r0,[r0,#0x4c]` / `0x00620E8A: blx EnumToString(ObjectType)` and `0x00620E9E: blx Util::sWarningF` with the string `0x00620E9C -> 0xBF854B "Observed active object of type %s but it's not connected. Is the battery plugged in?"`. Then `0x00620EC4..0x00620EDA` stores `map[ObjectID] = now + 10.0` into the `unordered_map<int,float>` (`0x00620ED2: blx unordered_map<int,float>::operator[]`; `0x00620ED6: vadd.f32 s0,s22,s18`; `0x00620EDA: vstr s0,[r0]`). | `0x00620E7C..0x00620EDA`; string `0xBF854B`; map type `0x00620E5A` | M11-004 | EXACT_SOURCE |
| G10.1c | The warning is gated by that map: `0x00620E42: blx BaseStationTimer::getInstance` / `0x00620E46: blx GetCurrentTimeInSeconds` (s22); `0x00620E4C..0x00620E5A` read `map[ObjectID]`; `0x00620E66: vcmpe.f32 s22,s0` / `0x00620E6E: blt #0x620ede` (skip the check while `now < map[id]`). The cooldown is `10.0` (`s18 = 10.0` at `0x00620B1E: vmov.f32 s18,#1.000000e+01`), matching the global `kUnconnectedObservationCooldownDuration_sec` at `0x00C781EC = 0x41200000 = 10.0`. | `0x00620B1E`; `0x00620E42..0x00620E6E`; data `0xC781EC` | M11-004 | EXACT_SOURCE |

**Divergence to flag:** the stack's `BlockWorld.AddAndUpdateObject` (line 463) *returns null*
(``drops'') an unconnected active object; the native path read here only warns and records the
10 s cooldown, then continues to `0x00620EDE`. The manager should check whether a drop lives
elsewhere (e.g. in the caller or in `AddConnectedActiveObject`/`FindConnectedActiveMatchingObjects`)
before the record is settled on the C# wording.

### G10.2 Moving gate (`BlockWorld::CheckForUnobservedObjects`, `0x00621C6C`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G10.2a | At the top of `CheckForUnobservedObjects`, if `MovementComponent::WasMoving(timestamp)` is non-zero the whole unobserved pass is skipped: `0x00621C88: ldr.w r0,[r0,#0x254]` (the `MovementComponent`); `0x00621C8C: mov r1,r5` (timestamp); `0x00621C8E: blx #0x4b8010` (`WasMoving(unsigned int)`, body `0x006417DC`); `0x00621C92: cmp r0,#0` / `0x00621C94: bne.w #0x62220c` (skip). | `0x00621C88..0x00621C94`; body `0x006417DC` | M11-004 | EXACT_SOURCE |
| G10.2b | `MovementComponent::WasMoving` consults the robot state history: `0x006417F2: ldr r6,[r0,#4]` (the `RobotStateHistory`), builds a predicate, and calls the helper `0x00641898` (`GetRawStateAt(timestamp)` + the predicate `std::function<bool(HistRobotState const&)>` at `0x00641956`). The predicate's exact "moving" test was not decoded (it is an unnamed lambda invoked through the `std::function`); the gate itself is `WasMoving(timestamp) != 0`. | `0x006417F2`/`0x00641898`/`0x00641956` | M11-004 | EXACT_SOURCE for the gate; RECOVERABLE_GAP for the predicate body (read the lambda invoked at `0x00641956`) |

### G10.3 Rotating gate (`VisionComponent::WasRotatingTooFast`, `0x0065359C`)

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G10.3a | The threshold passed by `CheckForUnobservedObjects` is `0.174533` rad/s (`= 10 deg/s`): `0x00621C9A: movw r2,#0xb8c2` / `0x00621C9E: movt r2,#0x3e32` -> `0x3E32B8C2 = 0.174533`; `0x00621CA4: mov r3,r2`; `0x00621CAA: str r1,[sp]` (`r1 = 0`); `0x00621CAE: blx #0x4a5bb4` (`WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0)`); `0x00621CB2: cmp r0,#0` / `0x00621CB4: bne.w #0x62220c` (skip). | `0x00621C98..0x00621CB4` | M11-004 | EXACT_SOURCE |
| G10.3b | `WasRotatingTooFast` = head OR body: `0x006535AC: blx #0x4ba404` (`WasHeadRotatingTooFast(timestamp, thresh, int)`, body `0x00656260`); if true return 1 (`0x006535B2`); else tail-call `WasBodyRotatingTooFast` (`0x006535C6 -> 0x8cd09c`, body `0x00656384`). | `0x006535AC`/`0x006535C6`; bodies `0x00656260`/`0x00656384` | M11-004 | EXACT_SOURCE (the criterion inside the two sub-functions was not transcribed) |

### G10.4 ObjectPoseConfirmer / ObservableObject match thresholds

`ObjectPoseConfirmer::FindObjectMatchForObservation` (`0x005063CC`) matches an observation to a
located object through `BlockWorld::FindLocatedClosestMatchingObjectHelper(object, Point3, Radians, filter)`
(`0x0050658E`) and `Vision::ObservableObject::IsSameAs(object, Point3, Radians, ...)`
(`0x0050660A`). The two thresholds are **per-object virtuals**, not literals in this function:

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| G10.4a | The distance threshold is the object's base `Point<3,float>` scaled by `0.8`: `0x0050656C: ldr r0,[r7]` (vptr); `0x0050656E: ldr r2,[r0,#0x30]`; `0x00506570..0x00506574` (virtual call, out at `sp+0xc0`); the thunk body is `0x004E025C` (`0x004E0262: ldr r2,[r0,#0x2c]` calls the base virtual; `0x004E0268: vldr s0,[pc,#0x20]` -> `0x004E028C = 0x3F4CCCCD = 0.8`; `0x004E0276..0x004E0288` multiplies the three components by 0.8). | `0x0050656C..0x0050658E`; thunk `0x004E025C`; data `0x004E028C` | M11-004 | EXACT_SOURCE (value); RECOVERABLE_GAP for the base virtual at vptr `+0x2c` (its name/meaning) |
| G10.4b | The rotation threshold is `Radians(45 deg)`: `0x0050657A: ldr r2,[r0,#0x34]`; the thunk body is `0x004E0290` (`0x004E0292: movw r1,#0xfdb` / `0x004E0296: movt r1,#0x3f49` -> `0x3F490FDB = 0.785398 = pi/4`; `0x004E029A: blx Radians::Radians`). | `0x0050657A`; thunk `0x004E0290` | M11-004 | EXACT_SOURCE |

So the object-matching gate is `IsSameAs` with translation tolerance `0.8 * (object base
extent)` and rotation tolerance `45 deg`; the robot-motion gate is `WasMoving(timestamp)`
plus `WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0)`.

---

## Existing records contradicted or too weak (from this pass)

- **M11-018** (`IMPLEMENTATION_GAP`). Its evidence line
  `"IsQuadrilateralReasonable 0x00892B18 (minQuadArea 25, symmetry 512 8.8, minDistanceFromEdge 2) -- live"`
  locates the parameters at the function entry. They are not literals there; they are
  `MarkerDetector::Parameters+0x30/+0x34/+0x38`, written at `0x00875338`/`0x00875340` and read
  at `0x00898E00`/`0x00898E0E` (G7.1). Replace the citation; the values themselves are right.
- **M11-003** (`EXACT_SOURCE`). The evidence is still bare symbol names (`Block::LookupBlockInfo`,
  `Block::AddFace`, `KnownMarker::_canonicalCorners3d`). This pass supplies the addresses and
  values (G9.1-G9.5): the four-entry table at `0x004E4C8C` (`44.0` cubes; keys 1..4; the six
  face records at `0xC45C40`/`0xC45CA0`/`0xC45D00`/`0xC45D60`; marker size `25.0`; the six
  `AddFace` cases; the face->code map). The record still needs re-citing from the addresses.
- **M11-004** (`EXACT_SOURCE`). The evidence is still the prose
  `"80 mm, 45 degrees, 600000 ms thresholds in the position-update strategy"`. G5 gave the
  three thresholds; this pass adds the connected check (G10.1), the robot motion gates
  (G10.2/G10.3) and the object-match thresholds (G10.4). The record needs re-citing.
- **M11-005** (`EQUIVALENT_IMPLEMENTATION`). The manifest's evidence now includes the design
  vector and the bilinear read; this pass confirms both instruction for instruction (G8.2,
  G8.1) and reads the `DotDivide` plumbing (G8.3). The Cholesky body's failure condition is
  `pivot < FLT_EPSILON` (not `pivot <= 0`), and it sets the `converged` out-flag rather than
  returning an error (G8.5d); the caller treats that flag as the accept path (G8 note). The
  manifest's provenance wording ("reports a non-positive pivot as the numerical failure")
  should be tightened to the epsilon condition before the failure path is called exact.
- **M11-032** (`IMPLEMENTATION_GAP`). Its `unresolved` still says
  `"the dark-mask multiplier 0xCCCC belongs to the non-live path"`, which the X1 and gap-1
  passes already contradicted (`0x00898BAA: ldr r5,[r7,#0xc]` -> `0x0088F662: mul r3,r6,r2`).
  Still unfixed; the record cannot be settled until the clause is removed and its evidence
  replaced with instructions.

## Correction C1 (B-M11 build job, 2026-09-28)

A B-M11 implementer stopped with `MISSING:` on five points; `@cozmo-extractor` settled them from the
`.so` (read-only report: `.scratch/B-M11/missing-report.md`). The rows below are corrected and the
inventory was re-approved with `python re-analysis/tools/fidelity.py --approve M11-vision`.

### C1.1 M11-032: the `Extract1dComponents` floor `a` is `Parameters+0x10`, not `maxScale`

**Rows corrected:** Appendix B G1.4 (`NextRow(maskPtr, cols, y=row, a=maxScale, b=params+0x12)`) and
G1.14b (`Extract1dComponents(maskRow, width, a=maxScale, b=params+0x12=0, list&)`). The `a=maxScale`
clause in both is **contradicted**.

The live chain passes `Parameters+0x10` as `a` and `Parameters+0x12` as `b`, and the shipped
`Parameters::Initialize` sets both to 0:

- `0x00898BA0: ldrsh.w r0,[r7,#0x10]` (short1 = `Parameters+0x10`), `0x00898BAC: ldrsh.w r6,[r7,#0x12]`
  (short2 = `Parameters+0x12`); `0x00898BE0: blx 0x4D1360` (ecvcs), with short1 in `r3`.
- ecvcs `0x0088F8C8: mov r8,r3`; `0x0088F938: strd r8,sl,[sp,#0xc]` stores `a` at `[sp+0xc]`
  before `r8` is reused for `maxScale` (`0x0088F93C: mov.w r8,#-1`).
- main loop `0x0088FBC0: ldrd sl,r5,[sp,#0xc]` (sl = a), `0x0088FC1C: str.w sl,[sp]`,
  `0x0088FC28: blx 0x4D0D18` (`..._NextRow`); the u16 body `0x00893F94: mov r2,ip` passes a to
  `Extract1dComponents` (`0x00893F96: blx 0x4D0FF4`).
- `0x00896FDC: strd r2,r3,[sp]`; the length test `0x00897052..0x00897064` appends only when
  `length >= a`.
- `Parameters::Initialize` `0x00875316/0x00875318: strh r1,[r0,#0x10]` and
  `0x0087531e: strh r1,[r0,#0x12]` with `r1 = 0`.

So `a = 0` (the floor never drops a run) and `b = 0` (the `b` test is a no-op). `maxScale` (17) is
used only for the integral-image border and the `get_maxRow`/`ScrollDown` bookkeeping. The
implementer's `a = params+0x10` is confirmed and kept.

### C1.2 M11-022: the ROI/negative mode is `MarkerDetector::Parameters+0x7c`, not `[camera+0x7c]`

**Row corrected:** M11-022's evidence ("ROI/negative choice from [camera+0x7c]") and Appendix B
G2.2. `MarkerDetector::MarkerDetector(Camera const&)` `0x0087509C` stores `[this+0] = Camera` and
`[this+4] = Parameters` (`0x008750A4`, `0x008750B8`); `Detect` reads `[r7+4]` then `[r0,#0x7c]`
(`0x00875400/0x00875402`). The only setter is `Parameters::Initialize`
(`0x008753AE: strh.w r3,[r0,#0x7C]` with `r3 = 0x100`, i.e. `+0x7c = 0`, `+0x7d = 1`); the ctor
`0x00875D48` does not write `+0x7c`. The live byte is 0, so the shipped mechanism is a single
non-negative pass and the `size >= 2` guard (`0x0087551C..0x0087551E`) means the `Rectangle<int>`
ROI vector is never populated. Mode semantics: 0 -> `{false}`, 1 -> `{true}`, 2 -> `{false,true}`,
other -> empty list (Detect returns 0).

**Also corrected:** Appendix B G2.6's `InitFromPointContainer` body is `0x006ABB38`
(`Rectangle<int>`; truncating `vcvt.s32.f32`, fields `{xmin, ymin, width, height}`), not
`0x0087855C` (the `Rectangle<float>` overload).

### C1.3 M11-020: the OpenCV 3.1.0 routines are still RECOVERABLE_GAP

The call-site arguments are exact and already in the manifest evidence (`boxFilter` CV_16S, anchor
(-1,-1), normalize true, `borderType = 4` = BORDER_REFLECT_101; `subtract` dtype -1; `normalize`
alpha 255.0, beta 0.0, NORM_MINMAX, dtype -1). The shipped routine bodies are **not yet read** and
SD1 requires them: `libopencv_core.so` `arith_op` `0x0002B7DC` (the `dtype=-1` mixed CV_8U/CV_16S
resolution and saturation), `libopencv_core.so` normalize's per-type applier `0x000407E8..0x00040A46`
(the `saturate_cast` rounding), and `libopencv_imgproc.so` the separable box worker `0x000A6E74`
and its `borderType=4` border helper. `cozmo-stack/src/Cozmo.Robot/Animation/OpenCv310.cs` has none
of the three today. M11-020 cannot be settled until these are transcribed.

### C1.4 M11-029: the cos-25-deg cluster reject operands

The reject at `0x008A64B4..0x008A65F2` is `cv::Mat::dot(point, clusterCenter) < 0.90630776`, where
operand 1 is `data.row(sb)` (the point's normalised tangent, 1x2 CV_32F; `0x008A64DC..0x008A64EC`)
and operand 2 is `centers.row(labels[sb])` (that point's kmeans center, 1x2 CV_32F;
`0x008A64FE..0x008A6506`). The float-converted dot (`0x008A65D2..0x008A65DE`) is compared at
`0x008A65E2`; on `mi` (less than) the point's label is set to -1 (`0x008A65EC/0x008A65F2`).

### C1.5 M11-026: the hollow test and the id compression, read in full

Hollow (`InvalidateFilledCenterComponents_hollowRows`, u16 body `0x0089614C`, threshold
`Parameters+0x24 = 1.0`): `ratio = merged[id] / size[id]`, where `merged[id]` is the per-component sum
over rows of the largest inter-run gap on each row (`0x00896232..0x00896260`, tail
`0x0089626E..0x00896296`) and `size[id]` is the component's pixel count from `ComputeComponentSizes`
(`0x008962E8`); if `ratio < 1.0` (`0x0089631E..0x00896328`) the component's validity flag is cleared
and all its segments get `componentId = 0` (`0x00896338..0x00896352`). Id compression
(`CompressConnectedComponentSegmentIds`, `0x00894A7C..0x00894B82`): `used[id] = 1` for every id
present, `lookup[0] = 0`, `lookup[id] = k++` for used ids in ascending order, each segment's id is
replaced by `lookup[id]`, `maxId` recomputed. The resulting ascending-id order is observable to the
boundary trace (which requires segments sorted by id) and the sort `0x00898DD0`.

## Correction C2 (B-M11 build job, 2026-09-28)

A second `MISSING:` round (`.scratch/B-M11/vision-missing-report.md`) filled the VisionSystem
records' evidence and pinned the M11-002 distance. Re-approved with `--approve M11-vision`.

### C2.1 M11-021: the CLAHE path is live and must be built

`ApplyCLAHE(image, 4, out)` is `0x006B44EC` (dispatched by `tbb [pc,r2]`, table `0x006B44F4`;
the only live value is 4). For enum 4 it (a) sets `[VisionSystem+0x358]=1`, then sums every 3rd
byte of every 3rd row of the grey image and clears the flag when
`sum >= 80 * ((cols+2)/3) * ((rows+2)/3)` (`0x006B451A..0x006B457E`, threshold constant 80 at
`0xC8E14C`); (b) lazily `CLAHE::setTilesGridSize(4,4)` (`0x006B4580..0x006B45D8`, `0xC8E148`) and
`setClipLimit(32.0)` (`0x006B45DC..0x006B4636`, `0xC8E144`, double at `0x6B4700`); (c)
`CLAHE::apply` through the vtable `[vptr+0x20]` (`0x006B4656..0x006B4672`); (d) a post-CLAHE
`cv::boxFilter(out,out,-1,ksize=(3,3),anchor=(-1,-1),normalize=true,borderType=4)`
(`0x006B4686..0x006B46A8`); (e) copies `[image+0x3c]` to `[out+0x3c]` (`0x006B46B4`).
`DetectMarkersWithCLAHE` (`0x006B4796`, `tbh [pc,r0,lsl#1]` table `0x006B479E`) for enum 4 picks
the original grey image when `[VisionSystem+0x358]==0` and the CLAHE image otherwise
(`0x006B47A8..0x006B47B4`); whichever it picks is what `MarkerDetector::Detect` copies into its
`Array<u8>` and passes to `DetectFiducialMarkers` (`0x008757AC..0x008757B2`). The shipped
`cv::CLAHE` is in `libopencv_imgproc.so` (apply body `0x20DAC`, worker `CLAHE_Apply_8u::operator()`
`0x20834`; decisive ranges: clip redistribution `0x2094E..0x209B8`, tile LUT `0x209BA..0x209F2`).
The record's evidence must gain these before it can own the path.

### C2.2 M11-033: the colour branch, and it is inert for the shipped grey camera

`EncodedImage::IsColor` (`0x4F2100`) is the encoding byte at `[this+0x20]`; the `tbb` table
`0x4F2110` makes encodings 2,3,4,6,7 colour and 0,1,5,8 grey. Colour -> `DecodeImageRGB` +
`ImageCache::Reset(ImageRGB const&)` (`0x0087459E`, RGB at entry `+0x54`, RGB-valid `+0x95`);
grey -> `DecodeImageGray` + `Reset(Image const&)`. `ImageCache::GetGray` (`0x0087465C`) turns an
RGB entry into grey via `ImageRGB::FillGray` (`0x00872998`), which calls
`cv::cvtColor(rgb, gray, COLOR_RGB2GRAY=7, 0)` (`0x008729CA`; `libopencv_imgproc.so` `0x2BC78`).
For the shipped camera (M3: 320x240 grey) the branch is inert: `IsColor` is false, only the grey
member is populated, `+0x95` stays 0, and `FillGray` is never called. The record must gain the
`ImageCache` layout (`+0x14` grey, `+0x54` RGB, `+0x94`/`+0x95` valid flags) and `FillGray`.

### C2.3 M11-034: the vision-mode numbers

`Anki::Cozmo::VisionModeFromString` (`0x796BD4`) fixes the ids: Idle 0, DetectingMarkers 1,
DetectingFaces 2, DetectingMotion 3, DetectingOverheadEdges 4, ReadingToolCode 5,
ComputingCalibration 6, CheckingQuality 7, ComputingStatistics 8, DetectingPets 9,
EstimatingFacialExpression 10, DetectingSmileAmount 11, DetectingGaze 12, DetectingBlinkAmount 13,
LimitedExposure 14, DetectingLaserPoints 15, Count 16. The `VisionSystem::Update` dispatcher calls
`ShouldProcessVisionMode` with 8, 1, 2, 9, 3, 4, 5, 6, 0xF, 7 in that order (`0x006B5122..0x006B55E4`).
Shipped `vision_config.json` enables markers 1, faces 2, motion 3, overhead 4, quality 7,
statistics 8, pets 9, laser 15 (bits 0x0002/0x0004/0x0008/0x0010/0x0080/0x0100/0x0200/0x8000) and
disables 10..13. The default of the unlisted modes 0, 5, 6, 14 is UNKNOWN.

### C2.4 M11-035: the non-marker handlers are other layers' production paths

The dispatch order and the `VisionProcessingResult` field offsets are M11's. The handler bodies
belong elsewhere: faces and pets -> M14 (`FaceWorld::ChangeFaceID` `0x6551FA`, `FaceWorld::Update`
`0x655274`, `PetWorld::Update` `0x4BA494`); motion -> M10 (`0x65536C`, `RobotObservedMotion`
`0x655382`); tool code -> M11 `VisionSystem::ReadToolCode` `0x6B5B98` with the `RobotReadToolCode`
layout in M2/M10; computed calibration -> M11-039/M3 with the `CameraCalibration` layout in M2/M10;
image quality -> M11 auto-exposure with M3 `SetCameraSettings` and the `EngineErrorCodeMessage`
layout in M2/M10; laser points -> M11 `LaserPointDetector` `0x6A7A10`/`0x6A7F46` with the
`RobotObservedLaserPoint` layout in M2/M10; `CheckMailbox` `0x6B2AD4`; the `RobotProcessedImage`
broadcast layout in M2/M10. M11-035 is a cross-layer IMPLEMENTATION_GAP.

### C2.5 M11-002: the nearest-neighbour distance is the absdiff sum divided by 1024

`0x008C0B58`: `s0 = [this+0x74]` (NUM_PROBES), `d0 = (double)NUM_PROBES`,
`d1 = cv::sum(diff)`, `d0 = d1/d0`, `(int)d0` truncated; `NUM_PROBES = 0x400 = 1024`
(`0xDC7F68`). The stack's integer divide by 1024 is correct. The record's evidence must state the
divisor.

## Correction C3 (B-M11 build job, 2026-09-28)

A third read (`.scratch/B-M11/pass2-report.md`) corrected two rows and filled four live-path gaps.
Re-approved with `--approve M11-vision`.

### C3.1 M11-003: the canonical corner order is `(-0.5,0,+0.5)`, `(-0.5,0,-0.5)`, `(+0.5,0,+0.5)`, `(+0.5,0,-0.5)`

**Row corrected:** G4.5 (and the M11-003 row) states the order `(-0.5,0,-0.5)`, `(-0.5,0,0.5)`,
`(0.5,0,0.5)`, `(0.5,0,-0.5)`. The static ctor `_INIT_69` `0x004DD7D8..0x004DD81A` calls
`Quadrilateral<3,float>` with the arguments in the order `(-0.5,0,+0.5)`, `(-0.5,0,-0.5)`,
`(+0.5,0,+0.5)`, `(+0.5,0,-0.5)` (`0x4DD7F4/0x4DD7EC/0x4DD7FC/0x4DD810` with the point addresses
`0x4DD802/0x4DD7F0/0x4DD80E/0x4DD80C`), and the ctor `0x4E9636` stores argument *i* at corner *i*
(`0x4E964C..0x4E968E`). So memory corner 0 is `(-0.5,0,+0.5)` and corner 1 is `(-0.5,0,-0.5)`.
`KnownMarker::Get3dCorners` `0x87E2E8` copies them in memory order and scales x by `size.x` and z by
`size.y` (`0x87E312/0x87E326`). `VisionMarker::Extract` `0x8A0130..0x8A018A` produces
`marker_corner[i] = detected[cornerReorder[label][i]]` (table `0xDC73AC`, 16 bytes/label;
orientationDeg `0xDC7D0C`), so the stack must pair canonical corner `i` with detected
`cornerReorder[label][i]`.

### C3.2 M11-037: on the normal path `AddAndUpdateObjects` runs before `CheckForUnobservedObjects`

**Row corrected:** V22 and the M11-037 row list `CheckForUnobservedObjects` (`0x0062504E`) before
`AddAndUpdateObjects` (`0x0062505A`). `0x0062504E` is in the **empty-observed-list** branch
(`0x624F86: beq 0x625020`); the normal path is `CreateObjectsFromMarkers` -> `AddAndUpdateObjects`
(`0x62505A`) -> if it returns 0, `CheckForUnobservedObjects` (`0x6250CA`) -> `UpdatePoseOfStackedObjects`
(`0x6250D0`) -> block config (`0x62520C`) -> `UpdateMarkerlessObjects` (`0x62521A`). Empty-list:
`ClearOccluders`, `AddLiftOccluder`, `CheckForUnobservedObjects`, skip the stacked-pose update. The
helper bodies: `AddLiftOccluder` `0x6564D8` (raw state at the timestamp -> lift transform ->
`ApplyTo` the occluder points -> `Camera::Project3dPoints` -> `Camera::AddOccluder` with the
transform's z scale); `BlockConfigurationManager::Update` `0x616D7C` (the `DidAnyObjectsMovePastThreshold`
gate, `UpdateAllBlockConfigs`, `PruneFullPyramids`, `UpdateLastConfigCheckBlockPoses`,
`NotifyBroadcasterOfConfigurationManagerUpdate`). `UpdatePoseOfStackedObjects` `0x621794` and
`UpdateMarkerlessObjects` `0x625704` are cross-layer (M10/M12) entry points only.

### C3.3 M11-004: the rotating gate's ImuData and the object match

The `ImuDataHistory` is `VisionComponent+0xb0` (`0x656286/0x6562B4/0x6563AC/0x6563D8`); it is filled
by `HandleImageImuData` `0x535C20` -> `ImuDataHistory::AddImuData(timestamp, rateX, rateY, rateZ, u8)`
`0x538B24` (`ImuData` layout: timestamp +0, rateX +4, rateY +8, rateZ +0xC, u8 +0x10). Head uses
`rateY`, body `rateZ`; `abs(rate) > threshold` is true, and a missing IMU bracket returns 1.
Object match: `ObjectPoseConfirmer::FindObjectMatchForObservation` `0x5063CC`; tolerances from the
observed object's virtuals (`vptr+0x30` = 0.8*extent, thunk `0x4E025C` with `0.8` at `0x4E028C`;
`vptr+0x34` = `pi/4`, thunk `0x4E0290`); primary `BlockWorld::FindLocatedClosestMatchingObjectHelper`
`0x61FA68` whose predicate `0x6281DA` requires the ObjectType and `Pose3d::IsSameAs` and narrows the
captured tolerance to `abs(delta)` (closest wins); fallback iterates the confirmer's list with
`ObservableObject::IsSameAs` `0x8769A8` (last match). The exact object returned by
`FindLocatedObjectHelper` `0x61EB78` is RECOVERABLE_GAP.

### C3.4 M11-007: the confirming side needs two sightings

`ObjectPoseConfirmer::AddVisualObservation` `0x50684C`: a new entry starts at count 1; a matching
second sighting increments to 2 and calls `UpdatePoseInInstance` (`0x506A04..0x506A26`); a
mismatching sighting resets the count to 1 (`0x506A46..0x506A4E`). `IsReferencePoseConfirmed` is
`count > 1` (`0x506340`); `IsObjectConfirmedAtObservedPose` requires count `>= 2` and the pose to
match (`0x50634C..0x5063B8`). So the first sighting does not confirm; the second does.

### C3.5 M11-021: the shipped CLAHE bodies

`CLAHE_Impl::apply` `0x20DAC`; the tile LUT body is `CLAHE_CalcLut_Body<uchar,256,0>::operator()`
`0x20834` and the bilinear body is `CLAHE_Interpolation_Body<uchar>::operator()` `0x203FE` (the
inventory's "worker" wording must name both). Divisible branch (the shipped 320x240): tileW 80,
tileH 60, tileSizeTotal 4800, histSize 256, `lutScale = 255/4800 = 0.053125`,
`clipLimit = (int)(32.0*4800/256) = 600`; the LUT and the interpolation both use `vcvtr.s32.f32`
(round to nearest, ties to even) then saturate; the interpolation weights `xa`/`ya` are floats.
Ranges: clip `0x2094E..0x209B8`, LUT `0x209BA..0x209F2`, per-column tables `0x2129E..0x21370`,
interpolation `0x204B8..0x20542`.

## Open questions the manager must decide or send back

1. **M11-004 connected-object drop.** The native path read (G10.1) warns and cooldowns but does
   not drop the object; the C# `AddAndUpdateObject` returns null. Is the drop in the native
   caller of `AddAndUpdateObjects`, or is the C# behaviour a `LOCAL_POLICY`? Needs a read of the
   callers of `AddAndUpdateObjects` (`0x00620AD4`) and/or `AddConnectedActiveObject` (`0x62302C`).
2. **M11-004 `WasMoving` predicate.** The gate is `WasMoving(timestamp) != 0`; the lambda's
   predicate body was not decoded (G10.2b). Read the lambda invoked at `0x00641956` if the
   exact "moving" criterion is wanted.
3. **M11-004 base matching extent.** The `0.8` factor is read (G10.4a), but the base virtual at
   `vptr+0x2c` (the un-scaled `Point<3,float>`) was not identified. Read that vtable slot.
4. **M11-005 `converged` semantics.** The flag is set true when `pivot < FLT_EPSILON` and the
   caller accepts on that flag (G8 note). Confirm the intended meaning before calling the
   failure path exact.
5. **M11-003 block-size getter offset.** The dimensions are `(44,44,44)`; the exact `Block`
   field the `+0x18` virtual returns was not pinned (G9.1). A short read of the `Block` vtable
   slot `+0x18` settles the field name.

## What is still unread

1. `BlockWorld::AddAndUpdateObjects` callers and `AddConnectedActiveObject`/`RemoveConnectedActiveObject`
   (`0x62302C`/`0x6243A0`) - only if the connected-object *drop* is required (open question 1).
2. The `WasMoving` predicate lambda invoked at `0x00641956`, and the bodies of
   `WasHeadRotatingTooFast` (`0x00656260`) / `WasBodyRotatingTooFast` (`0x00656384`).
3. The `ObservableObject` vtable slot `+0x2c` (base matching extent) and `Block` vtable slot
   `+0x18` (size getter field).
4. `Block::Block`'s exact copy of the `BlockInfo` fields (the `+0x88..0x9c` block layout) if the
   field names are wanted.
5. `CompressConnectedComponentSegmentIds` remap tail `0x00894AF4..` and the non-live binomial
   bodies remain unread, but neither is on the live path (carried over from gap 1).

*Read-only extraction. Nothing outside `.scratch/I-M11-gap2/` and this report file was changed.*


## Appendix D: gap pass 3 extraction report (20260927-I-M11-gap3-extraction.md)

# I-M11 gap pass 3 - extraction (read-only)

Job: gap pass 3 (last allowed) for the M11-vision integration job (I-M11). Scope: close
H1..H6, the six targeted items left open by gap pass 2
(`re-analysis/research/20260927-I-M11-gap2-extraction.md`) and the X1 pass
(`re-analysis/research/20260927-X1-M11-extraction.md`). Agent: opencode (DeepSeek),
window 3. Date: 2026-09-27. Scratch: `.scratch/I-M11-gap3/`.

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204).
The vision region is Thumb-2; disassembled with capstone `CS_MODE_THUMB` (base 0). All
addresses below are file VAs. The Ghidra decompilation was not used. The callers were
found by scanning the executable segment for Thumb `bl`/`blx` to the PLT stubs (a
capstone pass with `skipdata=True`); PLT names come from `.rel.plt`.

Row form: `| # | what the original does | citation | record | class |`.

---

## H1. The connected-object drop (behaviour-changing)

**Result: the native path does not drop the unconnected observed active object; it warns
and applies a 10 s cooldown, then continues.** `BlockWorld::AddAndUpdateObjects`
(`0x00620AD4`) has exactly one caller and its return value is a status code, not an
object; there is no branch on the unconnected path that skips or discards the object.

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H1.1 | `BlockWorld::AddAndUpdateObjects(multimap<float,ObservableObject*> const&, unsigned int)` is called from exactly one place, inside `BlockWorld::UpdateObservedMarkers` (the V22 frame sequence): `0x0062505A: blx #0x4B8154` (PLT `AddAndUpdateObjects`). The return is a status: `0x0062505E: mov r6,r0`; `0x00625060: cmp r6,#0`; `0x00625062: beq.w #0x62523A` (0 = success, which runs `CheckForUnobservedObjects`); a non-zero return logs `"BlockWorld.UpdateObservedMarkers.AddAndUpdateFailed"` (`0x0062506C` -> `0xBF9269`) and skips that. No caller-side drop of an object. | `0x0062505A: blx #0x4B8154`; `0x0062505E`/`0x00625060`/`0x00625062`; `0x0062506C`; `0x006250CA` (`CheckForUnobservedObjects` on the 0 path) | M11-004 | EXACT_SOURCE |
| H1.2 | The connected check is gated on the observed object's vtable `+0xc` returning 1 (`0x00620DF0: blx r1`; `0x00620DF2: cmp r0,#1`; `0x00620DF4: bne #0x620EDE`). Only then does it look the object up **by ObjectID**: `0x00620E70: ldr r0,[sp,#0x118]`; `0x00620E72: add.w r1,r0,#0x14`; `0x00620E76: mov r0,fp`; `0x00620E78: blx #0x4A6FB8` (`GetConnectedActiveObjectByIdHelper`). | `0x00620DF0`..`0x00620DF4`; `0x00620E70`..`0x00620E78` | M11-004 | EXACT_SOURCE |
| H1.3 | If the lookup **finds** a counterpart (`0x00620E7C: cbnz r0,#0x620EDE`) it goes straight to `0x620EDE`. If it returns null, the code **warns** (`0x00620E9E: blx #0x4A4540`, `sWarningF`, string `0xBF854B` = `"Observed active object of type %s but it's not connected. Is the battery plugged in?"`) and records the 10 s cooldown (`0x00620ED2: blx #0x4B7F80` `unordered_map<int,float>::operator[]`; `0x00620ED6: vadd.f32 s0,s22,s18` with `s18 = 10.0` from `0x00620B1E`; `0x00620EDA: vstr s0,[r0]`). There is **no branch out**: the next instruction is `0x00620EDE`, which both the found and not-found paths reach. | `0x00620E7C`; `0x00620E9E`; `0x00620ED2`/`0x00620ED6`/`0x00620EDA`; `0x00620B1E`; string `0xBF854B` | M11-004 | EXACT_SOURCE |
| H1.4 | `0x00620EDE` continues into the located-object matching loop over the vector filled at `0x00620DE6` (`FindLocatedMatchingObjects`): `0x00620EDE: ldrd r5,sl,[sp,#0x160]`; `0x00620EE4: beq.w #0x621064` (empty -> skip); per located object it does `HasSameRootAs` (`0x00620EF2`), `IsCarryingObject` (`0x00620F06`), `FindRoot`/`GetID` (`0x00620F1A`/`0x00620F20`) and stores `map[id] = located` (`0x00620FBC: blx #0x4B7F8C`; `0x00620FC2: str r7,[r0,#0x14]`). Nothing here tests "connected" or drops the observation. | `0x00620EDE`..`0x00620FC2` | M11-004 (partial) | EXACT_SOURCE |
| H1.5 | `GetConnectedActiveObjectByIdHelper(ObjectID const&) const` (`0x0061F58C`) is a pure lookup: it builds a `BlockWorldFilter` with the ObjectID, calls `FindConnectedObjectHelper(filter, callback, true)` (`0x0061F61C: blx #0x4A70C0`) and returns its result. It has no side effect and no drop. | body `0x0061F58C`; `0x0061F61C: blx #0x4A70C0` | M11-004 | EXACT_SOURCE |
| H1.6 | `BlockWorld::AddConnectedActiveObject(int, unsigned int, ObjectType)` (`0x0062302C`) creates an `ActiveObject` (`0x00623296: blx #0x4A7B10` `CreateActiveObjectByType`) and inserts it into the connected map. Its only caller is `0x00533BD6`. Its only `RemoveConnectedActiveObject` call is on the `ConflictingActiveID` path (`0x00623140`..`0x0062319E`), where an existing entry with the same activeID is disconnected before re-adding — it is not a drop of an observed object. `RemoveConnectedActiveObject(int)` (`0x006243A0`) erases one entry from the connected map (`0x00624432: blx #0x4B80D0` `__tree<...>::erase`); its two callers are `0x00533CA0` and `0x0062319E`. Neither drops observed objects. | `0x0062302C`; `0x00623296`; `0x00623140`..`0x0062319E`; `0x006243A0`; `0x00624432`; callers `0x00533BD6`/`0x00533CA0`/`0x0062319E` | M11-004 | EXACT_SOURCE |

**Conclusion for H1.** The C# `BlockWorld.AddAndUpdateObject` returning `null` for an
unconnected active object (BlockWorld.cs:458-464, guarded by `AllowUnconnectedObjects`,
which is an uninitialised `bool` property at line 306 and therefore defaults to false) is
**not** what the native `AddAndUpdateObjects` does. The native warns and rate-limits with
a 10 s cooldown and keeps going. M11-004's "connected-object rule" must be worded as
warn + cooldown, not drop, unless the manager deliberately classifies the C# drop as a
`LOCAL_POLICY`/`COMPATIBILITY_POLICY` divergence.

---

## H2. The `WasMoving` predicate

**Result: the predicate is not a wheel-velocity, pose-delta or threshold test. It reads a
single stored status bit: bit 0 (`IS_MOVING`) of the `HistRobotState` status word at
`+0x58` (a copy of `RobotState.status` at `+0x4c`).**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H2.1 | `MovementComponent::WasMoving(unsigned int)` (`0x006417DC`) builds a `std::function<bool(HistRobotState const&)>` whose vtable is `0x102F238` (`0x00641802`..`0x0064180C`; the `__func` type name at `0xC7F630` is `...MovementComponent::WasMoving...$_0...`) and calls the helper `0x00641898` with `(history, timestamp, &string, &function)`. | `0x006417DC`; `0x00641802`/`0x00641806`/`0x0064180C`; `0x00641898`; data `0xC7F630` | M11-004 | EXACT_SOURCE |
| H2.2 | The helper calls `RobotStateHistory::GetRawStateAt(timestamp, frameId, HistRobotState&, bool)` (`0x006418C0: blx #0x4A7D44`); on success it invokes the function through `std::function::operator()` (`0x00641956: blx #0x4B972C`); on failure it logs and returns 1. | `0x006418C0`; `0x00641956`; `0x006418C4`/`0x0064194E` (failure return 1) | M11-004 | EXACT_SOURCE |
| H2.3 | `std::function<bool(HistRobotState const&)>::operator()` (`0x00641DAC`) loads `__f_` at `this+0x10` (`0x00641DAC: ldr r0,[r0,#0x10]`), its vptr (`0x00641DB0: ldr r2,[r0]`) and the `__call` slot at vptr `+0x18` (`0x00641DB2: ldr r2,[r2,#0x18]`; `0x00641DB4: bx r2`). The `WasMoving` vtable is `0x102F238`, whose `+0x18` word is `0x00642673`. | `0x00641DAC`..`0x00641DB4`; vtable `0x102F238+0x18 = 0x00642673` | M11-004 | EXACT_SOURCE |
| H2.4 | The lambda body (`0x00642672`) is `ldr r0,[r1,#0x58]; and r0,r0,#1; bx lr`: it returns `HistRobotState[+0x58] & 1`. `HistRobotState` embeds `RobotState` at `+0xC` (memcpy of `0x5B` bytes at `0x005304F4`), so `+0x58` = `RobotState+0x4C`, the status word. | `0x00642672`/`0x00642674`/`0x00642678`; `0x005304F4: blx #0x4A4354` (memcpy 0x5B); `0x005304D8` | M11-004 | EXACT_SOURCE |
| H2.5 | Bit 0 of that status word is `IS_MOVING`: `MovementComponent::Update(RobotState)` does `ldr r1,[r8,#0x4C]`; `and r1,r1,#1`; `strb r1,[r6,#-0x7F]` (= `MovementComponent+9`). Confirms `+0x4C` is the status word and bit 0 is `IS_MOVING`. | `0x0063E30A`/`0x0063E314`/`0x0063E31A` | M2 (IS_MOVING mapping, M2-protocol.md:494) | EXACT_SOURCE |

**Conclusion for H2.** `WasMoving(t)` is exactly "the `RobotStatusFlag::IS_MOVING` bit of
the robot state nearest `t`", i.e. `HistRobotState.status & 1`. The gap-2 question
"wheel velocities? pose delta? threshold?" is answered: none of those; it is a stored
firmware status flag. (`WasHeadMoving`/`WasLiftMoving`/`WereWheelsMoving`/`WasCameraMoving`
use other bits/bytes of the same word: `+0x59` bit 1, `+0x59` bit 0, `+0x59` bit 7, and
`(flags & 0x8200) != 0x200`, at `0x6426D2`/`0x64273A`/`0x6427A2`/`0x642802`.)

---

## H3. The object-match base extent (vtable slot `+0x2c`)

**Result: the virtual at `+0x2c` is the object's stored size/extent getter; it returns a
`Point<3,float>` at object `+0x88`, which `Block::Block` fills with the `BlockInfo`
dimensions `(44,44,44)` for every light-cube type. The `+0x30` thunk multiplies that by
`0.8`, so the distance threshold is `(35.2, 35.2, 35.2)` mm.**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H3.1 | The `+0x30` thunk (`0x004E025C`) calls vptr `+0x2c` (`0x004E0262: ldr r2,[r0,#0x2c]`; `0x004E0264: mov r0,r1`; `0x004E0266: blx r2`), copies the returned `Point<3,float>` to its out pointer (`0x004E026E: ldm.w r0,{r2,r3,r4}`; `0x004E0274: stm r1!,{r2,r3,r4}`) and multiplies all three components by the constant at `0x004E028C` (`0x004E0268: vldr s0,[pc,#0x20]` -> `0x004E028C` = `0x3F4CCCCD` = `0.8`; loop `0x004E0276`..`0x004E0288`). | `0x004E025C`..`0x004E0288`; data `0x004E028C = 0.8` | M11-004 (G10.4a) | EXACT_SOURCE |
| H3.2 | The `+0x2c` implementation (`0x004E3830`) is a size getter: `ldr r1,[r0]` (vptr); `ldr r1,[r1,#-0x34]` (secondary-base offset); `add r0,r1`; `adds r0,#0x88`; `bx lr`. It returns `&(this + baseoffset + 0x88)`. The matching primary getter is the adjacent `0x004E382C: adds r0,#0x88; bx lr`. The `+0x2c` getter appears at slot `+0x2c` in seven vtables whose `+0x30` is the 0.8 thunk (`0x101CDEC`, `0x101CFBC`, `0x101D574`, `0x101D69C`, `0x101D8A8`, `0x101D9CC`, `0x101DBC0`). | `0x004E3830`; `0x004E382C`; vtable words at the seven `+0x2c` slots | M11-004 (NEW detail) | EXACT_SOURCE |
| H3.3 | `Block::Block(ObjectFamily, ObjectType)` (`0x004E5F50`) fills object `+0x88`: `0x004E5F7C: blx #0x4A47C8` (`Block::LookupBlockInfo`); `0x004E5F80: adds r0,#0x10`; `0x004E5F82: add.w r1,r4,#0x88`; `0x004E5F88: ldm.w r0,{r2,r3,r7}`; `0x004E5F8C: stm r1!,{r2,r3,r7}`. The `BlockInfo` dimensions are `44.0` for keys 1..4 (G9.1: `0x004E4CD6: movt r1,#0x4230` etc.). So for a LightCube/Block the field is `(44.0, 44.0, 44.0)` and the scaled threshold is `(35.2, 35.2, 35.2)`. | `0x004E5F50`; `0x004E5F7C`..`0x004E5F8C`; `0x004E4CD6`/`0x004E4D8E`/`0x004E4E46`/`0x004E4EFA` (44.0) | M11-004 / M11-003 | EXACT_SOURCE |

**Naming note.** The `+0x2c`/`+0x18` functions have no symbol in the `.so` symbol table;
they are the class's stored-`Point<3,float>` size getter, overridden per class (e.g. the
vtable at `0x101EF18` overrides `+0x2c` with `0x004F8A2C: adds r0,#0x58; bx lr`). The
`ObservableObject` size getter is the correct name; the exact member name is not
recoverable from symbols, but the field offset (`+0x88`) and values are.

---

## H4. `WasHeadRotatingTooFast` / `WasBodyRotatingTooFast`

**Result: both compare the absolute value of one IMU gyro-rate component, from the
samples immediately before and after the timestamp, against the threshold. Head uses
`rateY` (`ImuData+0x8`); body uses `rateZ` (`ImuData+0xc`). If either exceeds the
threshold the predicate is true; if no IMU bracket exists it is also true (fail-safe).**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H4.1 | `WasHeadRotatingTooFast(timestamp, threshold, int)` (`0x00656260`): if `[this+0x2F0] != 0` return 0 (`0x0065626E`..`0x00656276`); `s16 = threshold` (`0x00656278: vmov s16,r2`). If the int arg `r3 >= 1` it takes the `ImuDataHistory::IsImuDataBeforeTimeGreaterThan(timestamp, int, threshold, 0, 0)` path (`0x0065627C`/`0x00656292: blx #0x4BA6E0`). Otherwise it zeroes two `ImuData` at `sp+0x30`/`sp+0x18` and calls `GetImuDataBeforeAndAfter(timestamp, before, after)` (`0x006562BE: blx #0x4BA6EC`). | `0x00656260`; `0x00656278`; `0x00656292`; `0x006562BE` | M11-004 | EXACT_SOURCE |
| H4.2 | No data -> log `"VisionComponent.VisionComponent.WasHeadRotatingTooFast.NoIMUData"` / `"Could not get next/previous imu data for timestamp %u"` and return 1 (`0x006562C2: cbz r0,#0x656306`; `0x00656306`..`0x00656342: movs r0,#1`). | `0x00656306`..`0x00656342` | M11-004 | EXACT_SOURCE |
| H4.3 | With data: `|before.rateY| > threshold` -> return 1 (`0x006562C4: vldr s0,[sp,#0x38]`; `0x006562DA: vcmpe.f32 s0,s16`; `0x006562E2: bgt #0x656342`), then `|after.rateY| > threshold` -> return 1 (`0x006562E4: vldr s0,[sp,#0x20]`; `0x006562FA: vcmpe`; `0x00656302: ble #0x656274` -> return 0, else `0x00656304: b #0x656342` -> return 1). `sp+0x38 = before+8`, `sp+0x20 = after+8`. | `0x006562C4`..`0x00656304` | M11-004 | EXACT_SOURCE |
| H4.4 | `WasBodyRotatingTooFast` (`0x00656384`) is identical except it reads `before+0xC` and `after+0xC`: `0x006563E8: vldr s0,[sp,#0x3C]`; `0x00656402: bgt`; `0x00656408: vldr s0,[sp,#0x24]`; `0x00656426: ble`; `0x00656428: b #0x656464` (return 1). | `0x00656384`; `0x006563E8`/`0x00656408` | M11-004 | EXACT_SOURCE |
| H4.5 | The `ImuData` layout: `ImuDataHistory::AddImuData(unsigned int, float, float, float, unsigned char)` (`0x00538B24`) stores `timestamp @0` (`0x00538B2E: str r1,[sp]`), `rateX @4`/`rateY @8` (`0x00538B3A: strd r2,r3,[sp,#4]`), `rateZ @0xC` (`0x00538B3E: vstr s0,[sp,#0xC]`), `u8 @0x10`. The caller `HandleImageImuData` (`0x00535C20`) loads `ImageImuData` `rateX/rateY/rateZ` (`0x00535C2E: ldm.w r0,{r1,r2,r3}`; `0x00535C36: vldr s0,[r0,#0xC]`) and passes them in that order. So `+8 = rateY`, `+0xC = rateZ`. | `0x00538B24`; `0x00538B2E`/`0x00538B3A`/`0x00538B3E`; `0x00535C2E`/`0x00535C36` | M11-004 (NEW detail) | EXACT_SOURCE |
| H4.6 | The live call passes `r3 = 0`: `VisionComponent::WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0)` (`0x00621C9A`/`0x00621C9E` -> `0x3E32B8C2`; `0x00621CA4: mov r3,r2`; `0x00621CAA: str r1,[sp]` with `r1 = 0`; `0x00621CAE: blx #0x4A5BB4`). `WasRotatingTooFast` (`0x0065359C`) forwards that 5th arg to both sub-calls (`0x006535A0: ldr r6,[sp,#0x18]`; `0x006535AC: blx #0x4BA404`; `0x006535C6` body `0x00656384`), so the live path is the `GetImuDataBeforeAndAfter` branch. | `0x00621C9A`..`0x00621CAE`; `0x0065359C`/`0x006535A0`/`0x006535AC` | M11-004 | EXACT_SOURCE |

---

## H5. The `Block` size getter field (vtable slot `+0x18`)

**Result: the `+0x18` virtual is `0x004E382C` (`adds r0,#0x88; bx lr`); it reads the
object's `Point<3,float>` at `Block+0x88`, the same field `Block::Block` fills from the
`BlockInfo` dimensions (44,44,44).**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H5.1 | `Block::AddFace` (`0x004E53BC`) calls vptr `+0x18` three times and reads the returned `Point<3,float>`: `0x004E53FA: ldr r0,[r5]`; `0x004E53FC: ldr r1,[r0,#0x18]`; `0x004E53FE: mov r0,r5`; `0x004E5400: blx r1`; `0x004E5402: vldr s16,[r0,#4]`; then again for `[r0,#8]` (`0x004E540E`) and `[r0]` (`0x004E5424`). The three are halved at `0x004E542C`/`0x004E5430`/`0x004E5434`. | `0x004E53FA`..`0x004E5434` | M11-003 | EXACT_SOURCE |
| H5.2 | The `+0x18` implementation is `0x004E382C: adds r0,#0x88; bx lr` (the primary-base sibling of the `+0x2c` getter `0x004E3830`). The `+0x18` getter `0x004E382C` appears at slot `+0x18` in seven primary vtables (`0x101CD30`, `0x101CF2C`, `0x101D4DC`, `0x101D60C`, `0x101D810`, `0x101D93C`, `0x101DB30`); they are distinct vtable objects from the H3.2 secondary vtables, but both getters return the field at `+0x88`. | `0x004E382C`; vtable words `0x101CD48` etc. | M11-003 (NEW detail) | EXACT_SOURCE |
| H5.3 | `Block+0x88` is written from the `BlockInfo` entry by `Block::Block` (`0x004E5F80`..`0x004E5F8C`, H3.3), and the entry values are `44.0` for all four light-cube types (G9.1). So the field the getter reads is the block's size `Point<3,float>`. | `0x004E5F80`..`0x004E5F8C`; `0x004E4CD6` etc. | M11-003 | EXACT_SOURCE |

---

## H6. M11-005 `converged` semantics (Cholesky out-flag)

**Result: on `pivot < FLT_EPSILON` the function sets the `bool&` out-flag to `true` and
returns `Result 0`; on a completed factorisation the flag stays `false` and it also
returns `Result 0`. `RefineQuadrilateral` maps `flag != 0` to `[sp+0x60] = 1` and at
`0x008C6418: cmp r0,#1; bne #0x8C644A` the `1` case falls through to the branch that
copies the refined corners and sets the result to 0. So a near-singular normal matrix
takes the accept path, and a normal completed solve takes the error-threshold path.**

| # | what the original does | citation | record | class |
|---|---|---|---|---|
| H6.1 | Signature confirmed: `Anki::Result Anki::Embedded::Matrix::SolveLeastSquaresWithCholesky<float>(Array<float>&, Array<float>&, bool, bool&)`. Entry clears the out-flag: `0x0088DE7A: mov sb,r3`; `0x0088DE86: strb.w r0,[sb]` with `r0 = 0` (`0x0088DE7C`). | `0x0088DE68`; `0x0088DE7A`/`0x0088DE7C`/`0x0088DE86`; signature string `0xC334FB` | M11-005 | EXACT_SOURCE |
| H6.2 | Failure condition: `0x0088DF50: vcmpe.f32 s0,s18` with `s18 = FLT_EPSILON` (`0x0088DEB8: vldr s18,[pc,#0x2F4]` -> `0x88E1B0 = 0x34000000 = 1.1920929e-07`); `0x0088DF58: bmi.w #0x88E132`. The target sets the out-flag **true** and returns 0: `0x0088E132: movs r0,#1`; `0x0088E134: strb.w r0,[sb]`; `0x0088E138: movs r0,#0`. | `0x0088DF50`/`0x0088DF58`; `0x0088DEB8`; data `0x88E1B0`; `0x0088E132`..`0x0088E138` | M11-005 | EXACT_SOURCE |
| H6.3 | On the normal path (`p >= FLT_EPSILON`) the out-flag is never written again. The epilogue `0x0088E090`..`0x0088E0DE` returns `r0 = 0` (`0x0088E094: movs r0,#0`; `0x0088E0D8: movs r0,#0`) without touching `[sb]`. So `flag == true` holds **only** for the near-singular pivot case. | `0x0088E090`..`0x0088E0DE` | M11-005 | EXACT_SOURCE |
| H6.4 | `RefineQuadrilateral` initialises its out-flag at `sp+0xD7`: `0x008C61E4: movs r0,#0`; `0x008C61EC: strb.w r0,[sp,#0xD7]`; calls the solver `0x008C61F4: blx #0x4D0B5C`; `0x008C61F8: mov sb,r0`; `0x008C61FE: bne.w #0x8C5BF4` (solver Result != 0 -> error return). | `0x008C61E4`..`0x008C61FE` | M11-005 / M11-031 | EXACT_SOURCE |
| H6.5 | It reads the flag: `0x008C6206: ldrb.w r6,[sp,#0xD7]`; `0x008C62AE: mov r2,r6`; `0x008C62CA: cmp r6,#0`; `0x008C62CC: it ne`; `0x008C62CE: movne r2,#1`; `0x008C62D4: str r2,[sp,#0x60]`. So `[sp+0x60] = (outflag != 0) ? 1 : 0`. | `0x008C6206`/`0x008C62AE`/`0x008C62CA`..`0x008C62D4` | M11-005 | EXACT_SOURCE |
| H6.6 | The branch: `0x008C6418: ldr r0,[sp,#0x60]`; `0x008C641A: cmp r0,#1`; `0x008C641C: bne #0x008C644A`. The `== 1` case falls through to `0x008C641E`..`0x008C6448`, which copies the four refined corners into `[sp+0x39C]`, calls `SetCast`, and sets `sb = 0`. The `!= 1` case (`0x008C644A`..`0x008C6464`) calls the error helper `0x008C66C4` and sets `sb = 1` when the error exceeds `[sp+0x394]`. `RefineQuadrilateral` returns `sb` (`0x008C5BF4: mov r0,sb`), so `0` is accept and `1` is failure. | `0x008C6418`..`0x008C6464`; `0x008C5BF4` | M11-005 / M11-031 | EXACT_SOURCE |

**Conclusion for H6.** A near-singular normal matrix (`pivot < FLT_EPSILON`) sets the
`bool&` out-flag true and `RefineQuadrilateral` takes the **accept** path. The record's
name `converged` is misleading: the flag is not set on a successful factorisation; it is
set exactly on the degenerate-pivot early-out, and the caller treats that early-out as
accept. The manager should either rename the flag in M11-005's wording (e.g.
`degeneratePivot`) or state the polarity explicitly, and should not describe the
`pivot < FLT_EPSILON` path as the failure path.

---

## Existing records contradicted or too weak (from this pass)

- **M11-004** (`EXACT_SOURCE`). Its "connected-object rule" (the C# drop) is contradicted
  by H1: the native `AddAndUpdateObjects` warns and applies a 10 s cooldown, then
  continues; it does not drop the observation. The record must be re-worded, or the C#
  drop recorded as a deliberate policy divergence. Its `WasMoving` wording ("the
  robot-motion gate is `WasMoving(timestamp)`") is correct but the predicate body is now
  known to be the `IS_MOVING` status bit (H2.4/H2.5).
- **M11-005** (`EQUIVALENT_IMPLEMENTATION`). The gap-2 note stands and is now confirmed
  instruction-for-instruction (H6): the `converged` out-flag is set true only on
  `pivot < FLT_EPSILON`, and the caller accepts on true. The manifest wording must be
  tightened before the failure path is called exact.
- **M11-003** (`EXACT_SOURCE`). Its evidence is still bare symbol names. This pass pins
  the `Block` size getter at vtable `+0x18` = `0x004E382C` reading `Block+0x88`, and the
  `+0x2c` getter at `0x004E3830` (H3/H5). Re-cite from those addresses.

## Open questions the manager must decide or send back

1. **M11-004 connected-object rule (H1).** Decide whether the C# `return null` is a
   deliberate `LOCAL_POLICY`/`COMPATIBILITY_POLICY` or a defect. The native behaviour is
   warn + 10 s cooldown + continue; the C# behaviour is drop. The two are not the same.
2. **M11-005 `converged` polarity (H6).** Rename/word the flag. The current record's
   naming implies the opposite of the mechanical behaviour; a reader would call the
   near-singular path a failure when the code accepts it.
3. **H3 class/member naming.** The `+0x2c`/`+0x18` getters have no symbol; the manager
   may accept the descriptive "size getter / `Point<3,float>` at `+0x88`" wording, or
   ask for the class name from the `vtt`/typeinfo (not read here).

## What is still unread

`BlockWorld::CreateObjectsFromMarkers`'s insertion of the observed object (to show the
unconnected observation persists after the warning) is still unread; the H1 answer rests
on the `AddAndUpdateObjects` branch structure alone. The exact class/typeinfo name of the
seven `+0x18`/`+0x2c` vtables is unread.

*Read-only extraction. Nothing outside `.scratch/I-M11-gap3/` and this report file was changed.*



