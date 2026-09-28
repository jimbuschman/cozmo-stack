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
