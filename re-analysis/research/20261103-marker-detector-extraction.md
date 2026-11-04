# MARKER-DETECTOR-1 extraction (Claude extractor, 2026-11-03; filed by the manager)

The engine's cube and charger marker detector, from the grey image to the ObservedMarker list. Every citation was checked
against the `.so` instructions, not only against Ghidra. The manager re-read 0x00875386 (500), 0x00898E38..0x00898E52
(min(quads, 100)) and 0x00898E68..0x00898F12 (the 8.0 gate and validity 6).

**38 rows:** 16 MATCH, 6 DIFFERS, 5 NOT IN STACK, 11 UNKNOWN. Here UNKNOWN means not compared in this pass, not unread.

Row format: number. **step:** engine behaviour (citation; record; classification) → comparison with the stack.

1. **Mode gate, ApplyCLAHE and the image choice:**
   - the dark test sums every 3rd pixel of every 3rd row and compares it with 80·⌈rows/3⌉·⌈cols/3⌉;
   - when dark: a 4x4-tile CLAHE with clip 32, then a 3x3 box filter;
   - DetectMarkersWithCLAHE picks the image.

   (0x006B44E0..0x006B46B4, 0x006B47A8..0x006B47B4; M11-021; EXACT_SOURCE) → UNKNOWN. All five probe frames were "dark", so
   CLAHE is the live input.
2. **ResetBuffers:** scratch 4,000,000 / 600,000 / 200,000; marker list capacity params+0x6c (0x008753C2, 0x008753C6, 0x008753C8,
   body 0x00874E20; NEW; EXACT_SOURCE) → MATCH, except as rows 6 and 17 show.
3. **Pass list from params+0x7c** (shipped 0, so one pass) (0x00875400..0x00875470, 0x008753AE; M11-022) → MATCH
   (MarkerDetector.cs:41-47).
4. **Array<u8> wrap; per-slot 3x3 H arrays** (0x008753F0, 0x00875690..0x008756E2; M11-022) → MATCH.
5. **Size-filter bounds:** +0x14 = roundf((p74·dmin)² − (p70·p74·dmin)²), +0x18 the same with p78 and dmax, in binary32
   (0x008756E6..0x00875770; M11-026) → MATCH at 320x240 (19, 34685). The stack uses double and Math.Round to even.
6. **DetectFiducialMarkers input checks:** they run only if all three memory stacks are under 1,000,000. One is 4,000,000, so they
   are skipped (0x00898788..0x008987C4, 0x008987D0/0x008987D4; NEW) → MATCH (inert).
7. **ConnectedComponents capacity:** params+0x40 = 39000 (0x9858); ids are u16 (0x00898866, 0x0089886E, 0x0089887A ctor,
   0x00875392; NEW; RECOVERABLE_GAP) → NOT IN STACK. Read ctor 0x004D1354 and the overflow path. If the unit is segments, 320x240
   gives at most 38400 and the cap is inert.
8. **Extractor selector, window bank {4,8,16}** (0x00898B4C..0x00898B50, 0x00898B5A..0x00898B9E, 0x00898BE0; M11-024, M11-032) →
   MATCH (EcvcsExtractor).
9. **ecvcs binarise (0xCCCC), per-row DP** (0x0088F61A..0x0088F66C, 0x00893BAA, 0x00896FDC; M11-032) → UNKNOWN.
10. **Filter chain order:** Compress, SmallOrLarge, Compress, SolidOrSparse, Compress, Hollow, Compress, Sort
    (0x00898C90..0x00898DD0; M11-026) → DIFFERS in order: QuadDetector.cs:223-252 runs hollow, then compress, then per-component
    size and fill.
11. **Size test boundaries** (0x00898CAA..0x00898CBC; M11-026) → UNKNOWN.
12. **Solid/sparse test** (0x00898D0A..0x00898D1C; M11-026) → UNKNOWN.
13. **Hollow test** (0x00898D6A..0x00898D78, body 0x0089614C; M11-026) → UNKNOWN.
14. **Sort by id** (0x00898DD0; M11-026) → UNKNOWN.
15. **Quad list capacity:** params+0x44 = 500; appends beyond capacity are silently skipped (0x00898DEE, 0x00898DFC,
    0x00892F6A..0x00892F70; 500 = 0x00875386; NEW, M11-028 omits it) → DIFFERS: the stack's MaxQuads is 10000
    (QuadDetector.cs:25, 269).
16. **Quad corners and acceptance:** boundary trace, line fits, clockwise order, s16 round, IsQuadrilateralReasonable, order 0,3,1,2
    (0x00892D70..0x00892F62, 0x008C6B18, 0x008A5DB8..0x008A6C7C; M11-018, M11-027..030) → UNKNOWN.
17. **Marker count:** min(max(quads, 0), capacity 100) (0x00898E38..0x00898E52; NEW) → DIFFERS: the stack decodes every quad and
    stops at 500 decoded (MarkerDetector.cs:116).
18. **Homography loop:** it runs over all quads (up to 500) and writes slot i·0x40. Above 100 quads it writes past the 100-slot
    list, which is undefined (0x00898E62..0x00898E66, loop 0x00898E78..0x00898F1A; NEW; M1-034 candidate) → NOT IN STACK.
19. **ComputeHomographyFromQuad:** s16 corners to f32; the source is the unit square (0,0),(0,1),(1,0),(1,1); IsConvex on both
    quads, otherwise identity H with the failure flag (0x008C9036..0x008C9098, 0x008C909A, 0x008C90D4; NEW) → DIFFERS: no
    convexity identity in Homography.FromUnitSquare.
20. **EstimateHomography<float>:** an 8x8 normal-equation system in binary32, SolveLeastSquaresWithCholesky<float> 0x0088DE68,
    h22 = 1 (0x008C9340..0x008C97A5; NEW; the rows of A need an instruction-level read) → DIFFERS: the stack uses double with
    Gaussian elimination.
21. **Reprojection check:** with 4 points, an error over 0.1 px sets the flag and gives identity H (in 0x008C9340; NEW;
    RECOVERABLE_GAP, range to pin) → NOT IN STACK.
22. **Validity 6:** set when the flag is set, or H[2][0] > 8.0, or H[2][1] > 8.0 (signed). A quad that isn't validity 4 is later
    skipped (0x00898E68, 0x00898EE2..0x00898F12, 0x008994D4; NEW) → NOT IN STACK.
23. **Corners into the slot as f32, validity 4** (0x00898EA8, 0x00898EB8..0x00898EE0; M11-023) → MATCH.
24. **Illumination normalisation (params+1):** runs before the validity-4 test (0x008990BE..0x00899474; M11-020; IMPLEMENTATION_GAP)
    → UNKNOWN.
25. **Empty ROI:** logs "Got empty ROI for given corners"; the slot stays at 4 and is erased later (0x0089955A..0x0089957C; M11-020)
    → MATCH.
26. **RefineCorners call and arguments** (0x008994D8..0x00899528; M11-005) → UNKNOWN (numerics; the position matches).
27. **Restore after refine** (0x00899530..0x0089954E; M11-023) → MATCH.
28. **Refine failure:** gives validity 7; validity 2 gives type 0x27 (0x00899552..0x00899556, 0x0089959E..0x008995AA; M11-023,
    M11-031) → MATCH.
29. **Decode gate params+0x69 = 1** (0x00899032..0x00899036, 0x00899074..0x0089907C; M11-023) → MATCH (inert).
30. **Extract:** threshold 0x32; nearest neighbour on the slot's refined H. Accept when label ≠ −1, distance < 50 and label not
    149/150; then type = labelToCode, corners reordered, orientation at +0x24, validity 0. Reject gives type 0x27 and validity 3
    (0x00899054, 0x00899056, body 0x008A0078; M11-001, M11-002) → MATCH (structure).
31. **GetProbeValues:** binary32, 1/w then multiply, sign-branch rounding, no clamp (0x0089EF40..0x0089F158; M11-002) → DIFFERS:
    double, divide, Math.Clamp (MarkerLibrary.cs:214-233).
32. **GetNearestNeighbor** (0x008C0938; M11-002) → UNKNOWN.
33. **Type-3 check:** inert, because label 149 is rejected first (0x0089905A..0x00899070, labelToCode 0x00DC7154; NEW) → MATCH.
34. **Erase validity ≥ 2,** order kept (0x00899668..0x0089968E; M11-022) → MATCH.
35. **Error return:** a non-zero DetectFiducialMarkers result adds no markers for the pass (0x00898EA0 → 0x00898830; NEW) → NOT IN
    STACK, probably unreachable.
36. **ObservedMarker:** timestamp = Image+0x3C, s16 type, quad in slot order, Camera copy, −1 (0x00875548, 0x0087555C, ctor
    0x0087E1FC; M11-022) → MATCH. The stack also carries Distance and Orientation.
37. **ROI vector only with two or more passes** (0x0087551C; M11-022) → MATCH.
38. **DetectMarkersWithCLAHE tail:** quality rectangles from the raw quads. When [VisionSystem+0x50] is set, rolling-shutter
    correction and a −1e-5 out-of-bounds test ("RemovingMarkerOOB") follow (0x006B4854, 0x006B4868, literal 0x006B4B20 =
    0xB727C5AC; M11-021/M11-033) → UNKNOWN (NativeRollingShutter not compared).

## Records the source contradicts or that are too weak

- **No record is contradicted outright.** Two stack comments are wrong:
  - `QuadDetectorParameters.MaxMarkers` says "500 at +0x44". In fact +0x44 is the quad capacity and the marker capacity is +0x6c = 100.
  - The `MaxQuads` comment says the bound is 10000. It is 500.
- **M11-028** (EXACT_SOURCE, "The quad construction and acceptance"): its append at 0x00892F62 is conditional on capacity
  (0x00892F6A..0x00892F70), and no record owns that.
- **M11-023** (IMPLEMENTATION_GAP, the front-end orchestration order; unresolved "Still open: none known beyond M11-020"): it leaves
  out the validity-6 homography gate (rows 19-22) and the 100-marker truncation (row 17).
- **M11-002** (IMPLEMENTATION_GAP): its text is right, but the stack's probe arithmetic differs from it (row 31).

## Emulator

- **Entry:** `MarkerDetector::Detect(const Image&, std::list<ObservedMarker>&)` 0x008753B6 (call 0x008753B7).
- **Detector object:** the engine's own constructor `MarkerDetector(const Camera&)` 0x0087509C, with `Camera::Camera()` 0x0085DD14
  (it loads the nearest-neighbour library itself), then `Init(int,int)` 0x008752E0.
- **Image:** `Image::Image(240,320)` 0x00871CAC (as in emu_overhead_edges.py). The list is a 12-byte sentinel.
- **Output node** (value at node+8): s16 type at +0; u32 timestamp at +4; 4×(f32 x, y) at +8..+0x27; Camera at +0x28; int at
  +0x4C.
- **CLAHE front end:** `VisionSystem::ApplyCLAHE(img, 4, out)` 0x006B44E0 on a zeroed 0x800-byte VisionSystem stand-in, with
  `cv::createCLAHE(40.0, Size{8,8})` at +0x34C and +0x350/+0x354 = 0. The dark flag lands at +0x358.
- **Stand-ins:**
  - cv::parallel_for_ hooked inside libopencv_core;
  - `__signbit` / `__signbitf`, floorf/ceilf/roundf/expf;
  - logging, benchmark and Tic/Toc.
- **libm boundary:** host exp, sqrt (IEEE-exact) and atan2f, about 50 / 25 / 12 calls per frame.
- **Speed:** about 3 s per frame. The output does not depend on uninitialised memory (poison 0x00 and 0xFF give identical bits).

## Probe (PIL-decoded JPEGs, so the input is approximate; CLAHE path)

| Frame | Markers |
|---|---|
| 20260925-165815-CONTROL/camera-02942.jpg | MARKER_LIGHTCUBEJ_LEFT (13): (79.829, 26.866), (84.800, 145.872), (206.087, 30.666), (194.388, 144.310) |
| 20260925-173447-CONTROL/camera-35659.jpg | MARKER_LIGHTCUBEJ_FRONT (12): (177.047, 32.068), (168.835, 180.394), (248.364, 34.263), (233.873, 143.010). Only one of the two visible faces. |
| 20260925-180557-CONTROL/camera-63864.jpg | MARKER_LIGHTCUBEJ_FRONT (12): (46.846, 58.569), (65.067, 179.859), (191.037, 59.444), (185.650, 183.115) |
| P0-baseline-02287.jpg, camera-02460.jpg | none |

Without CLAHE, camera-35659 found nothing (a RefineQuadrilateral failure).

Probe scripts: scratchpad `marker\emu_marker_probe.py` and `marker\rdis.py` (session scratch, not in git).
