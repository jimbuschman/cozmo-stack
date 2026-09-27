# M11 vision — front end (grayscale frame → decoded marker codes)

Scope: the marker-detection front end only, entry to decoded marker. Read-only extraction.
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (the 3.4.0-1204 build the project targets).

## 0. Method note the manager must act on: the vision region is Thumb-2, not ARM

The task brief says "the vision region is ARM mode; `re-analysis/tools/arm_disasm.py`". That is wrong.
`0x00898760` in ARM mode decodes to `svcmi #0xf0e92d` / `stc p0,c11,[sp,#-0x204]!` (nonsense); in Thumb mode it is
`push.w {r4-r11,lr}; sub sp,#4; vpush {d8-d13}; sub.w sp,sp,#0x810` — a real prologue. Every function in the
brief (`0x008A2344`, `0x008A5DB8`, `0x00892B18`, `0x008C6B18`, `0x0089ED1C`, …) is Thumb-2. `arm_disasm.py` in
ARM mode prints garbage for this region and must not be used. Use capstone `CS_MODE_THUMB` (as `disarm.py` does)
or the same tool retargeted. The task-supplied odd address `0x00890BB6` is consistent with Thumb and inconsistent
with an ARM branch target.

## 1. Production path overview

```
VisionSystem::Update(VisionPoseData const&, ImageCache&)      0x006B4D5C
  ShouldProcessVisionMode(DetectingMarkers)                   0x006B5AA4   (bl 0x006B5124 / 0x006B5164)
  Application::ApplyCLAHE(...)                                0x006B44E0   (bl 0x006B50F2)
  VisionSystem::DetectMarkersWithCLAHE(...)                   0x006B4780   (bl 0x006B5182)
    Anki::Vision::MarkerDetector::Detect(Image const&,        0x008753B6
        std::list<ObservedMarker>&)                                          (bl PLT 0x004BF120)
      Anki::Embedded::DetectFiducialMarkers(Array<u8> const&, 0x00898760   (bl PLT 0x004CFCEC)
          FixedLengthList<VisionMarker>&, FiducialDetectionParameters const&,
          MemoryStack x3)
        ExtractComponentsViaCharacteristicScale[_binomial]    0x0088F8BC / 0x00890448
        ConnectedComponents::CompressConnectedComponentSegmentIds
        ConnectedComponents::InvalidateSmallOrLargeComponents 0x00895714
        ConnectedComponents::InvalidateSolidOrSparseComponents 0x00895954
        ConnectedComponents::InvalidateFilledCenterComponents_hollowRows 0x00896110
        ConnectedComponents::SortConnectedComponentSegmentsById 0x008947BC
        ComputeQuadrilateralsFromConnectedComponents          0x00892D70
          TraceNextExteriorBoundary                           0x008C6B18
          ExtractLineFitsPeaks                                0x008A5DB8
            getGaussianKernel/filter2D/kmeans/solve
          Quadrilateral<float>::ComputeClockwiseCorners       0x008A1324
          IsQuadrilateralReasonable                           0x00892B18
        Transformations::ComputeHomographyFromQuad             0x00898E98
        VisionMarker::Extract                                 0x008A0078
          GetNearestNeighborLibrary                           0x0089ED1C
          NearestNeighborLibrary::GetNearestNeighbor          0x008C0938
            VisionMarker::GetProbeValues                       0x0089EF40
        VisionMarker::RefineCorners                           0x0089FD98
          VisionMarker::ComputeBrightDarkValues                0x0089F8E8
          RefineQuadrilateral                                  0x008C55E0
```

All names above are from the binary's symbol table (`so.symbols`, 37k entries). The `.so` is not stripped: the
vision functions are present as local symbols, so exact boundaries are available.

The shipped schedule (`re-analysis/obb/assets/cozmo_resources/config/engine/vision_config.json:5`) enables
`"DetectingMarkers": true` and does not list it under `InitialModeSchedules`, so markers run **every frame**.
There is **no per-frame time budget** in this path; the only timing is `Vision::Profiler::Tic/Toc` around each
mode (calls at `0x006B5102`/`0x006B511C` etc.). Marked UNKNOWN below.

## 2. Step table

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| F1 | Frame admission/mode gate: markers run only if `ShouldProcessVisionMode(DetectingMarkers)`; the shipped config enables markers with no schedule, i.e. every frame. | `0x006B5124: blx ... _ZN4Anki5Cozmo12VisionSystem23ShouldProcessVisionModeENS0_10VisionModeE`; file `re-analysis/obb/.../vision_config.json:5` (`"DetectingMarkers": true`) and `:19-29` (no marker entry) | NEW (archive proposed M11-022; no current record) | EXACT_SOURCE for the gate; the managed fixed subset stays a gap |
| F2 | Entry: `Anki::Vision::MarkerDetector::Detect(Image const&, list<ObservedMarker>&)` @`0x008753B6` resets detector buffers, wraps the Image as `Array<u8>`, and calls `DetectFiducialMarkers`. It then post-processes each marker (ROI/negative choice at `[camera+0x7C]`, `Rectangle::InitFromPointContainer<Quadrilateral<float>>`, `ObservedMarker` ctor). | `0x008753C8: blx ... _ZN4Anki6Vision14MarkerDetector6Memory12ResetBuffersEiii`; `0x008753F0: blx ... ArrayIhEC2...`; `0x004CFCEC: bl _ZN4Anki8Embedded21DetectFiducialMarkers...`; `0x004BF048: GetROI`, `0x004CFCC8: Image::GetNegative`, `0x004BEBBC: Rectangle::InitFromPointContainer` | NEW | EXACT_SOURCE (entry and call); post-processing beyond the front end = RECOVERABLE |
| F3 | `DetectFiducialMarkers` orchestration order: components → CompressSegmentIds → size filter → solid/sparse filter → hollow filter → sort by id → quads → homography → decode, then a second loop for illumination-normalise/refine/decode. | `0x00898C5C/0x00898BE0` (extract), `0x00898C90/0x00898CF0/0x00898D50` (compress), `0x00898CBC` (size), `0x00898D1C` (solid/sparse), `0x00898D78` (hollow), `0x00898DD0` (sort), `0x00898E2C` (quads), `0x00898E98` (homography), `0x00899056` (Extract), `0x00899528` (RefineCorners) | M11-026 (archive; no current record) | EXACT_SOURCE for the call list; RECOVERABLE for the per-marker loop details |
| A1 | Filter selector: the **first byte of the embedded `FiducialDetectionParameters`** chooses the implementation. Zero → `ExtractComponentsViaCharacteristicScale_binomial` @`0x00890448`; non-zero → `ExtractComponentsViaCharacteristicScale` @`0x0088F8BC`. | `0x00898B4C: ldrb r0,[r7]` (r7 = params); `0x00898B4E: cmp r0,#0`; `0x00898B50: beq #0x898c18`; call `0x00898C5C: blx ...ExtractComponentsViaCharacteristicScale_binomial...`; other call `0x00898BE0: blx ...ExtractComponentsViaCharacteristicScale...` | M11-018 (cites only 0x00898C18) | RECOVERABLE_GAP — read the params construction in `MarkerDetector::Detect` 0x008753B6 / `Parameters::Initialize` to learn the shipped byte. M11-018 does not record this selector |
| A2 | `ImageProcessing::BinomialFilter<u8,u8,u8>` @`0x008A2344`: separable five-tap `[1 4 6 4 1]`, each pass `>>4` (`lsrs r1,r1,#4`), border taps folded: first column weights 11,4,1 (`smulbb r1,r1,r7` with `r7=0xB` at `0x008A2426/0x008A243C`), second column 5,6,4,1 (`0x008A2458`–`0x008A246E`). | `0x008A24A2: lsrs r7,r7,#4` (row pass); `0x008A244A: lsrs r1,r1,#4`; `0x008A243C: smulbb r1,r1,r7` (11) | M11-018, M11-026 | EXACT_SOURCE |
| A3 | Binarize @`0x00890BB6`: `mask = ((scale * mult) >> 16) > pixel` with a 32-bit `mul` then arithmetic `asrs #0x10`; a second output stream is `-1` where dark and `0` otherwise. `mult = 0xCCCC` set in `Parameters::Initialize` at `+0xC`. | `0x00890BBE: mul r6,r6,ip`; `0x00890BC2: asrs r6,r6,#0x10`; `0x00890BC4: cmp r6,r5`; `0x00890BCC: movgt r6,#1`; `0x00890BD8: movgt.w r6,#-1`; `0x00875300: movw r1,#0xcccc` / `0x00875314: str r1,[r0,#0xc]` | M11-018 | EXACT_SOURCE |
| A4 | Characteristic-scale select @`0x00890B14`: per level, `BinomialFilter` (`bl 0x004D0E08`), response `|filtered−image|` via `Matrix::Elementwise::ApplyOperation<SumOfAbsDiff>` (`bl 0x004D0E2C`), keep the filtered value wherever the response is the largest so far (`ldrb r6,[r5]; cmp r6,r7; strbhi r6,[r3]; ldrbhi r7,[r2]; strbhi r7,[r1]`). `PyramidLevels` = 1 at parameters `+4`. | `0x00890B14: ldrb r6,[r5]`; `0x00890B1A: cmp r6,r7`; `0x00890B1E: strbhi r6,[r3]`; `0x00890B22: strbhi r7,[r1]`; `0x0087530E: strd r3,r2,[r0,#4]` (r3=1) | M11-018 | EXACT_SOURCE (level value and select loop). Level loop >1 uses `DownsampleByTwo`/`UpsampleByPowerOfTwoBilinear<1..5>`, not exercised at shipped settings |
| A5 | Component size filter: `ConnectedComponentsTemplate<u16/i32>::InvalidateSmallOrLargeComponents(min,max)` @`0x00895758` accumulates a per-component count and invalidates a component id when `count < min` or `count > max`. The two integers are `FiducialDetectionParameters+0x14/+0x18`. | `0x00898CAA: ldrd r5,r7,[r0,#0x14]`; `0x008957D6: cmp r2,fp` (min) `blt` invalidate; `0x008957DA: cmp r2,r8` (max) `strhgt r1,[r0]`; `0x008957E2: strh r1,[r0]` | none names these | EXACT_SOURCE for the test and its operands. **The effective min/max value is RECOVERABLE_GAP** |
| A5b | The min/max at `+0x14/+0x18` are written by `SetComputeComponentMinNumPixels(int,int)` @`0x00875BE8` and `SetComputeComponentMaxNumPixels(int,int)` @`0x00875C2C`: `s = min(a,b)` (min setter) / `max(a,b)` (max setter); `min = round((0.03·s)² − (0.024·s)²)` using `+0x74`=0.03 and `+0x70`=0.8; `max = round((0.97·s)² − (0.776·s)²)` using `+0x78`=0.97 and `+0x70`=0.8. | `0x00875BFA: vldr s4,[r4,#0x74]` (0.03); `0x00875C0A: vmul.f32 s0,s0,s0`; `0x00875C12: vsub.f32 s0,s0,s2`; `0x00875C1A: roundf`; `0x00875C26: vstr s0,[r4,#0x14]`; max setter `0x00875C3E/0x00875C6A` | NEW | RECOVERABLE_GAP — read the setters' callers (their two arguments) to get the shipped values. The local `QuadDetectorParameters.MinComponentPixels = 100` / `MaxComponentPixels = 39000` are **not established** by this pass; `39000` does appear at `Parameters+0x40` (`0x00875392` `movw sl,#0x9858`) but no link to `+0x18` was read |
| B1 | `InvalidateSolidOrSparseComponents(minFill,maxFill)` @`0x00895954` (wraps the u16/i32 template). Parameters `+0x74`=0.03 and `+0x70`=0.8. | call `0x00898D1C`; wrapper `0x00895954`; values `0x0087538A` `strd r7,r1,[r0,#0x74]` | M11-026 | RECOVERABLE_GAP — body of `ConnectedComponentsTemplate<u16>::InvalidateSolidOrSparseComponents` not read this pass (read at the template address reached from `0x00895954`) |
| B2 | `InvalidateFilledCenterComponents_hollowRows(float)` @`0x00896110` (fraction `+0x78`=0.97). | call `0x00898D78`; `0x0087538A` (`+0x78`=0.97) | M11-026 | RECOVERABLE_GAP — body not read this pass |
| B3 | `TraceNextExteriorBoundary` @`0x008C6B18` reduces a component to four extent arrays over its bounding box and stitches a staircase contour in four passes. The contour list capacity is **10000** (`ComputeQuadrilaterals…` `0x00892DA8: movw r1,#0x2710`). It requires the component's segments to be sorted by id (`get_isSortedInId` check `0x008C6B5C`). | `0x00892DA8: movw r1,#0x2710` (list ctor `0x004D08C8`); `0x008C6B5C: blx ...get_isSortedInIdEv`; `0x008C6B18` entry | M11-018, M11-026 | EXACT for capacity/sortedness; RECOVERABLE_GAP for the four-pass body (0x008C6FCA..0x008C72E8) |
| B4 | `ComputeQuadrilateralsFromConnectedComponents` @`0x00892D70`: for each component id, `TraceNextExteriorBoundary`; then a 4-way `CornerMethod` switch — 0 → `ExtractLaplacianPeaks` @`0x008A75C8`, 1 → `ExtractLineFitsPeaks` @`0x008A5DB8`. `Parameters+0x28 = 1`, so the shipped path uses `ExtractLineFitsPeaks`. It then requires exactly 4 corners, builds the `Quadrilateral<s16>` in the order 0,3,1,2, calls `IsQuadrilateralReasonable`, exchanges the middle pair if the swap flag is set, and appends only if the test returned valid. | `0x00892E24: ldr r0,[sp,#0x144]`; `0x00892E26: cmp r0,#1`; `0x00892E28: beq #0x892e44`; `0x00892E56: blx ...ExtractLineFitsPeaks...`; `0x00892E60: cmp r0,#4`; stores 0,3,1,2 at `0x00892E66`–`0x00892EC4`; `0x00892EF8: blx ...IsQuadrilateralReasonable...`; swap at `0x00892F04`; append `0x00892F62`; `0x00875332` `stm r1,{r3(1),r5(5),lr(25)}` → `+0x28`=1 | M11-018, M11-026 | EXACT_SOURCE |
| B5 | `IsQuadrilateralReasonable` @`0x00892B18`: (1) `|cross(c0,c1,c2)| ≥ minQuadArea` (`0x00892B42`–`0x00892B5C`); (2) convexity — each diagonal's two triangle areas must have equal signbit (`0x00892C4E`, `0x00892CC0`); (3) symmetry — `max(area)·256 < threshold·min(area)` for either diagonal (`0x00892CFC: muls r0,r3,r0`; `0x00892D02: lsl r1,r5,#8`; `0x00892D20: cmp r1,r0; bge reject`); (4) four corners each `≥ minDistanceFromEdge` from the image border and `< dim − margin − 1` (`0x00892D24`–`0x00892D5A`). Returns `1/0` and writes the swap flag (`strb r0,[ip]` at `0x00892BF4`). `minQuadArea=25` (`+0x30`), `symmetry=512` in 8.8 (`+0x34`), `minDistanceFromEdge=2` (`+0x38`). | `0x00892B42: mul r7,r4,r5`; `0x00892B5C: cmp r4,r1`; `0x00892C4E: blx __signbit`; `0x00892CFC/0x00892D02`; `0x00892D3E: ldrsh r5,[r6,#2]`; `0x00875338: stm r1,{r3(1),r5(5),lr(25)}` (=25); `0x00875340: strd r6,r7,[r0,#0x34]` (512,2) | M11-018 | EXACT_SOURCE |
| C1 | `ExtractLineFitsPeaks` @`0x008A5DB8` smoothing: `sigma = n/64` (`vmul.f32 s18,s0,s2` with `0x3C800000`=0.015625 at `0x008A5E90`); kernel size = `ceil(((sigma−0.8)/0.3+1)·2+1)`, forced odd (`0x008A5ED6`–`0x008A5F1A`); `cv::getGaussianKernel(ksize,sigma)` (`bl 0x004D18C4`); derivative kernel `[-0.5,0,+0.5]` (values `0xBF000000`/`0x3F000000` at `0x008A602C`–`0x008A6038`) applied with `cv::filter2D` (`bl 0x004BEBF8`); the boundary is convolved circularly, accumulated in double, and normalised to unit tangents (`0x008A613E`–`0x008A628A`). | `0x008A5E90: vmul.f32 s18,s0,s2`; `0x008A5F26: blx ...getGaussianKernel...`; `0x008A602C: mov.w r1,#-0x41000000`; `0x008A60B4` region (per local/M11-026) | M11-026 (archive), M11-018 does not cover | EXACT_SOURCE for sigma/kernel/derivative/filter2D; RECOVERABLE_GAP for the exact circular-convolution loop |
| C2 | Initial k-means labels @`0x008A638C`–`0x008A63EE`: four equal arcs of the boundary; quarter points taken with truncating (toward-zero) signed division. | `0x008A62D8: asr.w r0,sl,#0x1f`; `0x008A62E0: asrs r4,r0,#2`; `0x008A63A4: str r3,[r2],#4` (fill 1..3) | M11-026 | EXACT_SOURCE |
| C3 | `cv::kmeans` @`0x008A6490`: data = the transposed tangent matrix, K=4, criteria `COUNT|EPS` (type 3) with maxCount 15 and epsilon 0.1 (double `0x3fb999999999999a`), attempts=1, flags=1 (`KMEANS_USE_INITIAL_LABELS`). Nothing random. | `0x008A646A: strd r7,r3,[sp]` (type=3,count=15); `0x008A6476: movt r1,#0x3fb9` / `0x008A647E: strd r2,r1,[sp,#8]` (0.1); `0x008A6484: strd r0,r0,[sp,#0x10]` (attempts=1,flags=1); `0x008A6490: blx ...kmeans...` | M11-026 | EXACT_SOURCE |
| C4 | Per-cluster line fit: gather the cluster's points, pick the wider extent (`swapped`, `+8` of the line record; `0x008A6714`), solve least squares `[u 1]·[a b] = v`; then intersect every pair of the four lines (`0x008A6AA8`–`0x008A6B64`). | `0x008A6AA8: ldrb r1,[fp,#8]` (swapped); `bl 0x004BEA90 cv::solve`; `0x008A6A86` outer loop `cmp sl,#2`; `0x008A6AF8` etc. | M11-026 | EXACT_SOURCE for the intersection step; fit body partially read |
| C5 | Intersections are kept only if `0 ≤ y < imageHeight` and `0 ≤ x < imageWidth`, and **exactly four** must survive: `0x008A6B66`–`0x008A6BA0` then `0x008A6BE0: subs r2,r1,r0; 0x008A6BE2: cmp r2,#0x20; bne fail`. | as cited | M11-026 | EXACT_SOURCE |
| C6 | `Quadrilateral<float>::ComputeClockwiseCorners` @`0x008A1324`: sort the four points by `atan2f` about their centroid, ascending, with `Matrix::InsertionSort<float>`; a corner at the centroid gets angle 0. | `0x008A1324` entry; `bl 0x004A4510 atan2f`; `bl 0x004D1768 Matrix::InsertionSort<float>` | M11-026 | EXACT_SOURCE |
| C7 | Round the clockwise corners into `Quadrilateral<s16>`: clamp to `[-32768,32767]` (`0x008A6C1E`,`0x008A6C24`) then round half away from zero via `ceilf(v−0.5)` / `floorf(v+0.5)` (`0x008A6C4A`,`0x008A6C7C`). | as cited | M11-026 | EXACT_SOURCE |
| D1 | `VisionMarker::RefineCorners` @`0x0089FD98`: call `ComputeBrightDarkValues` @`0x0089F8E8` (`0x0089FE00`); if its bool is false set status 2 (low contrast) and return (`0x0089FECA`); set `threshold = (u8)(dark+bright)·0.5` (`0x0089FE52`–`0x0089FE66`); call `RefineQuadrilateral` @`0x008C55E0` (`0x0089FEA6`); on failure return; on success re-round into `s16`, re-run `IsQuadrilateralReasonable` (`0x0089FFCA`), and restore the original corners if invalid (`0x0089FFD0`–`0x0089FFEA`). | as cited | M11-005 (EQUIVALENT_IMPLEMENTATION) | EXACT_SOURCE for the orchestration; refinement numerics = EQUIVALENT |
| D2 | `RefineQuadrilateral` @`0x008C55E0`: 8 edge blocks sampled (100 samples ⇒ `ceil(100/8)` per block), template gradients along the edge normal, least-squares solved with `Matrix::SolveLeastSquaresWithCholesky<float>` (`bl 0x004D0B5C`), helper `0x008C66C4`. Constants: 255 (`0x008C5838`), 1/255 (`0x008C6116`), 1e-5 (`0x008C62C6`). | `0x008C55E0` entry; `0x008C66C4`; `0x004D0B5C`; floats as cited; M11-005 evidence quotes `0x008C570E..0x008C5C62` | M11-005 | RECOVERABLE_GAP for the full equations — M11-005's evidence is detailed but its status stays EQUIVALENT because the managed fit is a different solver |
| D3 | `ComputeBrightDarkValues` @`0x0089F8E8`: samples the 12 border probes and 12 interior probes (`ThresholdDark*`/`ThresholdBright*` tables) using the same homography; returns the two means; the bool result is `meanA > ratio · meanB` with `ratio` = the passed float (Parameters `+0x3C` = 1.01). Which mean is "dark" is not settled here. | `0x0089F98A` sampling loop; `0x0089FD18: vstr s0,[r0]` / `0x0089FD1C: vstr s2,[r1]`; `0x0089FD10: vldr s4,[r7,#-0x78]`; `0x0089FD14: vmul.f32 s4,s2,s4`; `0x0089FD26: vcmpe.f32 s0,s4`; `0x0089FD30: movgt r0,#1`; ratio `0x0087538E` (`sb`=0x3F8147AE=1.01) / `0x008753A2: stm r1,{sb,sl,ip,lr}` (`+0x3C`=1.01) | M11-005 authority names it | RECOVERABLE_GAP — read which out param is border vs interior and finish the comparison. The local decoder's `dark >= bright` (ratio 1.0) is **not source-backed**; see contradictions |
| E1 | `VisionMarker::Extract` @`0x008A0078`: `GetNearestNeighborLibrary` (`0x008A00B8`); `GetNearestNeighbor(image, probes, threshold, &label, &distance)` (`0x008A00D0`). Accept only if `label != −1`, `distance < threshold`, and `(label−149) unsigned > 1` (i.e. label not 149/150). Then `code = labelToCode[label]` (`0x008A0130`/`0x008A0134`), reorder the four corners by `cornerReorder[label][i]` (`0x008A0158`–`0x008A017E`), `orientation = orientationDeg[label]` (`0x008A0180`–`0x008A018A`). Rejected quads get `code = 0x27` (39, UNKNOWN), status 3 (`0x008A0190`–`0x008A0196`). The threshold `0x32` = 50 is passed from `DetectFiducialMarkers` (`0x00899054: movs r2,#0x32`). | as cited | M11-001, M11-002 | EXACT_SOURCE |
| E2 | Library layout: images `u8[598][1024]` @`0x00C9BCA8`; labels `u16[598]` @`0x00DC6CA8`; `labelToCode u32[150]` @`0x00DC7154`; `cornerReorder u32[150][4]` @`0x00DC73AC`; `orientationDeg f32[150]` @`0x00DC7D0C`. Constructor `NearestNeighborLibrary(u8*,u8*,u16*,int,int,s16*,s16*,s16*,s16*,int,int)` is called with `numImages=598` (`0x0089ED6E: movw r0,#0x256; 0x0089ED72: str r0,[sp]`). Verified directly from the binary: labels 0..149, label 149 twice; codes 0..38 only; reorder is the four permutations `(0,1,2,3),(1,3,0,2),(3,2,1,0),(2,0,3,1)`; orientation ∈ {0,90,180,270}. | `0x0089ED1C`; `0x0089ED6E`; data at the addresses above | M11-001 | EXACT_SOURCE |
| E3 | Probe geometry: centers `s16[1024]` @`0x00DC7FEC`/`0x00DC87EC`, values 7149..25619 (≈0.2181..0.7818 of the unit square at 1/2^15); 5 probe offsets @`0x00DC7F74`/`0x00DC7F7E` = `(0,205,0,−205,0)`/`(0,0,205,0,−205)`; 12 border probes `ThresholdDark*` @`0x00DC7F8C`/`0x00DC7FA4` at 0.0125 (410/32768); 12 interior `ThresholdBright*` @`0x00DC7FBC`/`0x00DC7FD4` at 0.15 (4915/32768). | data at the addresses above | M11-001 | EXACT_SOURCE |
| E4 | `GetProbeValues` @`0x0089EF40`: fixed-point scale `1/2^15` (`0x0089EF64`–`0x0089EFAA`, `vdiv.f32 s18,s16,s0`); for each probe, 5 samples at the projective image of `centre + offset` (`0x0089F09E`–`0x0089F0D6`); each coordinate is rounded **code-sharing**: `floor(c+0.5)` if `c>0`, else `ceil(c−0.5)` — for **both x and y** (`0x0089F0DA`–`0x0089F12E`); the probe value is the unsigned integer mean (`__aeabi_uidiv`, `0x0089F158`). | `0x0089F0E2: ble #0x89f0f2`; `0x0089F0EC: blx floorf`; `0x0089F0FA: blx ceilf`; `0x0089F158: blx __aeabi_uidiv` | M11-002 | EXACT_SOURCE. Local differs — see contradictions |
| E5 | `NearestNeighborLibrary::GetNearestNeighbor` @`0x008C0938` calls `GetProbeValues` (`0x008C0954`); `cv::normalize(query, query, 0, 255, NORM_MINMAX)` (`0x008C09D2`); per library row `cv::absdiff` (`0x008C0AD6`) then `cv::sum` (`0x008C0B50`); `distance = (int)(sum / numProbes)` (`0x008C0B58`–`0x008C0B68`); keep best and second-best rows. If their labels differ, recompute over probes where `|best−second| > threshold` (`0x008C0C4A`–`0x008C0C56`), average with integer division (`0x008C0C70: blx __aeabi_idiv`), and **reject when that average ≥ 1.25 × threshold** (`0x008C0C7E: vmov.f32 s2,#1.25`; `0x008C0C8A: vmul.f32 s2,s4,s2`; `0x008C0C96: bpl reject`). | as cited | M11-002 | EXACT_SOURCE |
| E6 | Decode output: a quad becomes `(code, corners TL,BL,TR,BR reordered by `cornerReorder[label]`, orientationDeg[label], integer distance)`. | `0x008A0130`, `0x008A0158`, `0x008A0186` | M11-001, M11-002 | EXACT_SOURCE |

## 3. Existing records: contradictions and weak evidence

**Contradicted / not source-backed**

1. **Contrast gate ratio.** `MarkerDecoder.Extract` (`cozmo-stack/src/Cozmo.Robot/Vision/MarkerLibrary.cs:310`) rejects
   when `dark >= bright` — ratio 1.0. The native path refines/decodes through `ComputeBrightDarkValues`
   (`0x0089FD10`–`0x0089FD30`) whose bool is `meanA > ratio · meanB` with `ratio` = `MarkerDetector::Parameters+0x3C`
   = `1.01` (`0x0087538E` builds `0x3F8147AE`; `0x008753A2` stores the quad at `+0x3C`). The stack already carries
   `QuadDetectorParameters.MinContrastRatio = 1.01` but does not use it here. The 1.0 gate is not what the engine
   does; the exact direction still has a gap (D3), so the correct claim is narrower than either.
2. **Probe sampling rounding.** `MarkerLibrary.cs:180-181` documents and `MarkerDecoder.GetProbeValues`
   (`MarkerLibrary.cs:222`) implements `x = floor(x+0.5)`, **`y = ceil(y−0.5)` unconditionally**. Native
   `GetProbeValues` (`0x0089F0DA`–`0x0089F12E`) applies the same sign branch to both axes: `floor(c+0.5)` when
   `c>0`, `ceil(c−0.5)` when `c≤0`. The two differ at exact half-integer coordinates; the comment is wrong and
   the y rule is only aliased to native on y≤0. This is a behaviour-changing difference (small), not just a
   citation issue.
3. **`component_minimumNumPixels`.** `QuadDetectorParameters.MinComponentPixels = 100` / `MaxComponentPixels =
   39000` (`QuadDetector.cs:20-23`) are asserted as `Parameters::Initialize` immediates. The native filter
   operands are `FiducialDetectionParameters+0x14/+0x18` (`0x00898CAA`) written by `SetComputeComponentMin/MaxNumPixels`
   (`0x00875BE8`/`0x00875C2C`), whose formula is a function of two arguments and `+0x70/+0x74/+0x78`. The value
   100 is **not established** by primary source yet (RECOVERABLE_GAP, A5b). `39000` exists at `Parameters+0x40`
   but the linkage to the filter operand was not read.
4. **M11-002's evidence is bare symbol names** (`"GetProbeValues"`, `"GetNearestNeighbor"`, `"Extract"` — no
   addresses). Per the project's own rule a symbol name is not a citation, so M11-002 is too weak to keep
   EXACT_SOURCE even though the underlying algorithm is largely confirmed. It also does not name the
   normalization/rounding/integer-division details that differ from the stack.
5. **M11-018 citation imprecision.** Its evidence says the dark-mask call is "in DetectFiducialMarkers at
   `0x00898C18`". `0x00898C18` is the branch target/label where the binomial setup block begins; the actual
   `blx` to `ExtractComponentsViaCharacteristicScale_binomial` is at **`0x00898C5C`**. More importantly, the
   record does not record the **selector** (`0x00898B4C: ldrb r0,[params]; beq 0x898c18`) that chooses between
   the binomial and non-binomial functions, so it cannot by itself vouch that the binomial variant is the live
   one. The dark-mask and quad-test decisions it names are confirmed (A2/A3/B5); its scope must not be read as
   the whole front end.
6. **Ambiguity average arithmetic.** `GetNearestNeighbor` computes the disambiguation average with
   `__aeabi_idiv` (integer division) before converting to float (`0x008C0C70`–`0x008C0C78`); the local
   `MarkerDecoder.GetNearestNeighbor` (`MarkerLibrary.cs:287`) uses `double` division. Divergence at the 1.25×
   boundary only, but it is a behaviour-changing difference in the claimed-exact decoder.
7. **Min-max normalisation rounding.** Native uses `cv::normalize ... NORM_MINMAX` (OpenCV `saturate_cast`),
   local uses `Math.Round` (banker's rounding) at `MarkerLibrary.cs:264`. Divergence at half-integers.

**Records whose status is too strong for the evidence read this pass**

- **M11-002 (EXACT_SOURCE)** — evidence is symbol names only (above), and two of its sub-behaviours differ
  from the stack. Should be narrowed or its evidence replaced with instruction citations.
- **M11-018 (EXACT_SOURCE)** — valid for "the dark mask uses `BinomialFilter` + `(scale·0xCCCC)>>16`" and "the
  quad test is `IsQuadrilateralReasonable`'s four rules", but it cites a label instead of the call and omits the
  filter selector; it must not be read as covering the connected-component filters, the boundary trace, or the
  corner extraction. The archive's own note ("M11-018 cannot vouch for the whole quad detector") stands.
- **M11-005 (EQUIVALENT_IMPLEMENTATION)** — the orchestration in `RefineCorners` is confirmed; `RefineQuadrilateral`
  uses a Cholesky least-squares solver, not the local line-fit edge search. The EQUIVALENT status is honest, but
  its `unresolved` is empty while the numerical equations were not re-derived this pass.

**No current record covers** (mark NEW): the on-image entry `Anki::Vision::MarkerDetector::Detect` (F2); the
filter selector (A1); the component size/solid-sparse/hollow filter bodies (A5/B1/B2); the boundary-trace
pass structure (B3); the derivative-of-Gaussian/k-means/line-fit chain (C1–C4); the contrast-gate ratio and
direction (D3); the real sub-pixel equations (D2). The archive's proposed M11-021..M11-035 map to some of these
but were never reconciled into `fidelity_manifest.json`.

## 4. Open questions for the manager

1. **Mode:** accept the correction that the vision region is Thumb-2 and that `arm_disasm.py` (ARM) is unusable
   for it. A retargeted tool at `.scratch/m11-frontend/fn.py` exists for this pass; it is scratch, not a repo
   change. Does the manager want a committed Thumb disassembler before the M11 inventory is written?
2. **A1 selector:** which of `ExtractComponentsViaCharacteristicScale[_binomial]` is on the shipped path? Needs
   `MarkerDetector::Detect` `0x008753B6` (and `Parameters::Initialize`) read to the point where the embedded
   `FiducialDetectionParameters` byte 0 is set.
3. **A5b values:** what are the two arguments the setters receive (hence the effective min/max component counts)?
   This decides whether the stack's `100/39000` should be repaired.
4. **D3 direction:** in `ComputeBrightDarkValues`, which returned mean is the border and which the interior, and
   is the accept condition exactly `interior > 1.01·border`? This decides the decoder gate.
5. **M11-002 vs the rounding/arithmetic differences:** are these to be recorded as a corrected record
   (EXACT_SOURCE for the algorithm, with the stack's two divergences fixed) or as EQUIVALENT_IMPLEMENTATION?
   The task says not to judge the C#, so this is a classification question for the manager.
6. **Per-frame budget:** none found in the front end. Is a budget expected at the `VisionComponent` queue level
   (M3/M11-032 worker/mailbox) rather than here? If so this row is UNKNOWN at M11 front-end scope.
7. **Boundary trace and filter bodies** were not read instruction-by-instruction this pass; they are the
   specific RECOVERABLE_GAPs (B1, B2, B3, C1-convolution, D2). Confirm they should be the next extractor task
   rather than folded into M11-026.

## 5. What is certain (short)

- The graph and entry are certain: `MarkerDetector::Detect` → `DetectFiducialMarkers` → the listed stages.
- The parameters `1`, `0xCCCC`, `25`, `512`, `2`, `0.015625`, `0.1`, `15`, `K=4`, `flags=1`, `0x32`, `1.01`,
  `1.25`, the 10000-entry boundary list and the 0,3,1,2 reorder are all read from instructions.
- The library layout (598 × 1024, 150 labels, the three label tables, the probe geometry) is read from the
  binary's data, independent of the extractor script.
- Four behaviours in the existing C# are not source-faithful: the probe-y rounding rule, the 1.0 contrast gate,
  the double average in the ambiguity test, and the `Math.Round` normalisation; `component_minimumNumPixels=100`
  is unverified.

*Generated read-only; no repository files were changed outside `.scratch/`.*

