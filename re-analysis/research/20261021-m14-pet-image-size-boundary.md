# R-M14-PET-SIZE — supported robot image versus pet pixels

Request: `requests/20261021-m14-pet-image-size-boundary.md`. HEADb19ce1bc. Independent read-only report; coverage opened before tracing. No approval/status/policy change.

## Coverage

| Join | Evidence | Result |
| --- | --- | --- |
| Supported robot image admission | Adopted M3 resolution4/decoded-size check | Accepted output320×240; half-size synthetic gray is not this path |
| Encoding8 decode | M3-001 A8/A11/JPEG accepted rows | MiniJPEG uses advertised320×240; no pet-specific half resize |
| ImageCache gray | Fresh ResetHelper0x008744E6..0x0087459C / GetGray0x0087465C..0x00874740 | Size0 selects decoded-size base entry, direct gray assignment |
| Vision update→pet | 0x006B4D8C..0x006B4E20,0x006B50E6,0x006B52B2..B8 | Same full gray image passed to DetectPets |
| Optional blackout | DetectPets0x006B3940..0x006B3ACC; BlackOutRects0x006B33AC..0x006B3548 | Copy/image ROI black fill, same dimensions and timestamp; no resize |
| Installed calibration | Adopted M3-022/M11-039; fresh callback0x0065ADA0..0x0065ADE0 | Values/dimensions from NV; no automatic “calibration must be320×240” proof |
| Actual robot NV dimensions | No current capture establishes them in this scoped check | UNKNOWN; cannot infer from image resolution |
| Differing-size conclusion | Both native helper and host image-turn use installed calibration | No native/C# image-turn discrepancy demonstrated on the accepted robot path; synthetic local-cal mismatch remains host input boundary |

## Result

The native supported robot camera route produces **320×240 pixels**, obtains ImageCache **size0** (the decoded/base size), and gives that image to PetTracker. No upstream half-size pet image or rectangle rescaling was found in this bounded chain. The optional blackout mask changes pixels, not coordinate units/dimensions. This closes the previous report's unknown about an intervening pet-specific resize.

The previous conditional160×120 example was a host synthetic/custom-image case. It is not a demonstrated ordinary robot defect. For an accepted320×240 robot frame, native and current host ReactToPet/image-turn both pass rectangle coordinates in that image's pixels and read the **installed calibration directly**. Current local scaled CameraModel is not passed to PetDetector or to TurnTowardsImagePoint. Its existence alone therefore does not prove a native-vs-host mismatch in this pet route.

Installed calibration dimensions are independent NV data. No blanket claim that every accepted robot's calibration says320×240 is supported here. A nonmatching NV calibration would need its actual source/values checked; neither a guessed resize nor an input exclusion follows.

## Reused primary evidence

Read M3/M11 inventories and adopted decoder/calibration rows before new tracing:
- M3 AddChunk accepts resolution4, sets columns320/rows240; other resolution chunks are dropped,0x004F1D4E and subsequent geometry stores.
- M3-001 A8: encoding8 MiniToJpegHelper uses advertised geometry and gray JPEG table, followed by imdecode. A11 validates decoded rows/cols against240×320 at0x004F2CDA..0x004F2D34. The accepted JPEG oracle/implementation is reused, not re-audited.
- M3 connection ImageRequest sends Stream/QVGA bytes01 04,0x005152F0..0x00515304; no encoding field or pet-specific resolution request.
- M11-033 Update(PoseData,EncodedImage) chooses gray/RGB decode, resets ImageCache then Update(PoseData,ImageCache)0x006B4C78.
- M3-022 NV calibration callback checks result/serialized size, unpacks fields, zeroes distortion on old hardware and invokes SetCameraCalibration0x0065ADE0; M11-039 owns installation into Camera.

`20261021-m14-image-coordinate-input.md` is a pending comparison lead, not adopted authority for new rows. Its integer detector-result→rectangle/center finding is not re-extracted here.

Color and other formats are not audited: known encoding9 half-width JPEG decode explicitly resizes back to advertised320×240 before downstream use. No claim that all firmware sends encoding8 is needed; M3-050 retains encoding5/6/7 hardware uncertainty and existing format scope decisions.

## Newly read missing joins

### Base-size ImageCache and vision handoff

At0x006B4D8C VisionSystem::Update passes r1=0/r2=0 to ImageCache.GetGray0x006B4D94. The returned Image pointer remains r0 while result containers initialize, is saved at stack+0x14 at0x006B4E1C, and its timestamp+0x3C is copied0x006B4E1E/20. Later0x006B50E6 reloads r4 from that exact slot;0x006B52B6 passes r4 to DetectPets0x006B52B8. No scaled image replaces this input pointer on that edge.

ImageCache.Reset(Image)0x008744E2 enters reset helper0x008744E6. It invalidates cache entries; finds/inserts key0; existing key0 directly assigns source Image into entry+0x14 at0x00874540 and sets gray-valid0x00874546. It records input rows/cols0x00874572..78. Fresh GetGray/GetImageHelper read0x0087465C..0x00874740 confirms key0 returns its valid gray image; RGB-only branch calls FillGray without changing dimensions. Other requested-size cache entries can be derived from base0, but this caller requests0, not a smaller entry.

The adopted cache entry layout is gray+0x14, RGB+0x54, validity+0x94/+0x95. Source comments that attach Reset gray/RGB addresses inconsistently do not change the directly read key0 assignments. No generic arbitrary-size cache resize algorithm is certified.

### DetectPets optional mask

DetectPets0x006B3940 tests supplied blackout-rectangle vector:
- empty: passes incoming Image directly to PetTracker.Update at0x006B39D6;
- nonempty: BlackOutRects at0x006B3960 produces an Image copy, passes that copy to PetTracker.Update0x006B396E.

BlackOutRects0x006B33AC..0x006B3548 constructs Image then input.CopyTo(output)0x006B33C0. For each rectangle it obtains an ROI on the copy and fills it0 via Array2d.FillWith0x006B3422. It never calls Resize or changes output geometry; cleanup/unwind frees ROI/copy storage only. ImageBase.CopyTo0x0086EFFA..0x0086F028 invokes matrix copy and explicitly copies timestamp+0x3C0x0086F01C/1E.

DetectPets's remaining normal body0x006B3A14..0x006B3ACC adds integer rectangles derived from detected bounds to the blackout vector for other detectors; it does not scale/rewrite the TrackedPet rectangle list. No operation here maps pet pixels to a different-size calibration frame. Relevant normal input/output and mask body ranges were read fully; unrelated profiler/allocator internals are reused interfaces.

Thus both routes feed PetTracker the same decoded-size pixel frame and camera timestamp, even when some areas are black.

### Calibration is separately installed

Native calibration arrives from NV CameraCalib0x80000001. Fresh callback0x0065ADA0..0x0065ADC6 passes serialized row/column fields (pointers to CLAD offsets+0x14/+0x16) and fx/fy/cx/cy/skew/distortion fields into make_shared<CameraCalibration>, then invokes SetCameraCalibration0x0065ADE0. Old-hardware distortion-zeroing is unrelated to dimension scaling.

This selected successful callback has no decoded-image row/column comparison or320/240 forced substitution. Adopted Camera.SetCalibration stores the shared calibration pointer. The image-turn helper reads its optical center/focal fields directly. Full constructor conversion/default validation for arbitrary calibration values is outside this task; no actual robot NV payload showing differing dimensions was established. Image geometry alone cannot prove NV dimensions.

## Current host comparison

Under `cozmo-stack/src/Cozmo.Robot/`:
- Camera decoder accepts the same resolution4 geometry and checks output dimensions; Vision.ProcessCapture(ImageCache,...) calls GetGray without requesting a half-size image. Gray input goes straight through; RGB FillGray preserves width/height.
- Vision.ProcessImage supplies this gray and camera timestamp to IPetDetector.Detect, then stores rectangles and that timestamp in PetWorld. The real OkaoPetDetector remains unavailable, so a detector boundary substitution is still required to exercise pets.
- For differing decoded versus installed calibration dimensions, ProcessImage creates a **local** scaled CameraModel for other computations. IPetDetector receives no CameraModel, and TurnTowardsImagePoint later reads Vision.Calibration directly. Therefore there is no evidence pet rectangle units were defined using that local scaled calibration.
- With ordinary accepted320×240 image input and corresponding detector rectangles, both native and host image-turn use those pixels and the same installed calibration. No extra half-image compensation belongs in this path based on the prior synthetic example.
- Public ProcessImage(GrayImage,...)/custom IPetDetector can accept synthetic dimensions. Such input is not proven equivalent to the robot decoder's accepted geometry; its contract and relation to calibration remains explicit UNKNOWN. This is not an exclusion decision.

## Bounded disposition / oracle

No source-backed resize correction is established for ordinary robot pet image input. Keep the preceding report's differing-size example labeled synthetic/custom-host boundary; do not queue a factor2 pixel/calibration adjustment for the robot path.

A minimal source-based test after adoption should use an accepted resolution4 decoded320×240 frame, actual installed calibration values and a substituted raw OMCV detector result with known rectangle/stamp. Verify base cache image dimensions/time survive the optional blackout copy and that ReactToPet constructs the image-turn with unchanged pixel center/stamp. This isolates Anki plumbing while honestly naming the external OMCV boundary.

Separately inspect a real robot NV calibration bundle to establish actual rows/cols; do not infer them from nominal test calibration. If it differs from320×240, compare the actual native helper output using those same values before proposing a host conversion. Any public custom-image dimension rule requires its own supported-input decision, not this robot-route inference.

Remaining UNKNOWNs: actual robot NV dimension corpus, arbitrary calibration-value handling, public synthetic/custom detector frame contract and external recognition. No JPEG/OMCV internals, history/PanAndTilt re-audit, production edits, tests run, hardware or settlement.

## Current owning records

M3-001 — EXACT_SOURCE — Camera frame reconstruction and gray decode: mini-JPEG headers, BE size fields, the DecodeImageHelper<Image> dispatch, the libjpeg 9 / OpenCV 3.1 imdecode port, the 240x320 check, DecodeImageHelper's logs

> A13 header tables 0x00C48C40 (gray, 0x144 B) and 0x00C48D84 (colour, 0x14E B): SOF0 at 0x59, 1 or 3 components (Y 0x21, Cb/Cr 0x11)

> A12 MiniToJpegHelper writes height BE at 0x5E/0x5F and width BE at 0x60/0x61 (0x004F31C4..0x004F32CC)

> A8 gray dispatch tbh 0x004F2898; A11 rows==240, cols==320 else BadDecode (0x004F2CDA..0x004F2D34); ts = EncodedImage+0xC

> JPEG port rows: re-analysis/research/20261010-jpeg-lse-logs-oom-rows.md and re-analysis/jobs/B-M3M4.md "JPEG unblocked" (J/L/D/O rows)

> Handoffs: a cv::Exception leaves DecodeImageHelper at the imdecode call 0x004F28C6, whose landing pad 0x004F2EA0 only cleans up and ends in _Unwind_Resume: M11-058; the decoded Image, with the J38 +0x3C timestamp copy, goes to VisionSystem::Update(PoseData, ImageCache) 0x006B4C78: M11-033

> O8 closed (Opus final settling pass (2026-10-11, HEAD 8f035387; jobs/B-M3M4.md "Final settling pass")): libopencv_java3.so imports cv::redirectError only as JUMP_SLOT 0x7A9E4 with no BL/BLX to its stub 0x231A0; libopencv_calib3d.so calls it only inside cv::findCirclesGrid (0xBEF2/0xBF08, scoped set/restore), which the engine does not import (only calibrateCamera and solvePnP).


M14-004 — IMPLEMENTATION_GAP — PetInitialDetection: a pet not reacted to, and a minute between reactions

> The strategy keeps the pet ids it has reacted to in a tree at +0x34: UpdateReactedTo 0x00611E1C walks the PetWorld (robot+0x3c) and inserts each pet id (node+0x10), and is called from ShouldTriggerBehaviorInternal 0x00611AE4 and BehaviorDidReact 0x00611E80. InitReactedTo 0x00611FA4 clears and refills the set, but no instruction in the shipped .so calls it (whole-.text BL/BLX, relocation and pointer scan): it is not part of the reachable path.

> RecentlyReacted 0x00611DD0 is the cooldown: the last reaction time sits at +0x40 as a float, -1 meaning never, and the answer is true while lastReacted + 60 is still ahead of BaseStationTimer::GetCurrentTimeInSeconds - the 60 is the 0x42700000 loaded at 0x00611DE8.

> This stack latched on the first sighting of any pet id and had no cooldown; it now keeps the reacted-to set and the sixty seconds.


M3-022 — EXACT_SOURCE — The connection-time CameraCalib read and its callback: the Failed/SizeMismatch/Recvd logs, the distortion zeroing for body hardware <= 6, the +0x48 store

> 1h NVStorageComponent::Read(0x80000001, cb) queued in the connection handler (0x006583E2..0x006583FA); M1 CD21

> 1j callback 0x0065AB68: the Failed, SizeMismatch and Recvd paths all end strb.w #1 at +0x48 (0x0065AE7E/0x0065AE80); distortion zeroed when robot+0x24 <= 6; SetCameraCalibration starts processing (0x0065175E..0x00651766)

> 2a..2f +0x48 is written only by that callback; +0x4B never; +0x49 stays 0

> Handoffs: VisionComponent::SetCameraCalibration blx 0x4B3C90 at 0x0065ADE0, before the +0x48 store strb.w r0,[r5,#0x48] at 0x0065AE80: the install is M11-039's; the readers of +0x48 (the image drop) are M11-049's (overlap ruling 2026-10-10)


M11-033 — IMPLEMENTATION_GAP — The image hand-off and the two VisionSystem::Update overloads

> VisionComponent::SetNextImage 0x00652B04; Processor 0x00651F08; UpdateVisionSystem(pose, image) 0x00653D30

> VisionSystem::Update(PoseData, EncodedImage) 0x006B4B68: IsColor 0x006B4B7C, DecodeImageRGB 0x006B4B90 or DecodeImageGray 0x006B4C02, then Update(PoseData, ImageCache) 0x006B4C78

> Update(PoseData, ImageCache) 0x006B4D5C: the pose and image guards 0x006B4D66/0x006B4D70, UpdatePoseData 0x006B4D88, GetGray 0x006B4D94. ImageCache layout: grey Image at entry+0x14, RGB ImageRGB at entry+0x54, grey-valid +0x94, RGB-valid +0x95 (ResetHelper<Image> 0x008744F0, ResetHelper<ImageRGB> 0x0087459E, GetImageHelper 0x0087465C). An RGB entry becomes grey via ImageRGB::FillGray 0x00872998 -> cv::cvtColor(rgb,gray,COLOR_RGB2GRAY=7,0) 0x008729CA (libopencv_imgproc.so 0x2BC78). EncodedImage::IsColor 0x4F2100 (encoding byte at +0x20, tbb table 0x4F2110). For the shipped 320x240 grey camera the colour branch is inert: only the grey member is stored, +0x95 stays 0 and FillGray is never called

> re-analysis/research/20260929-R-VIS-pre-extraction.md Part 2 item 8; re-analysis/research/20260929-R-VIS-verify-pre-items7-8.md (rows C1-C11 PASS)

> re-analysis/research/20260929-R-VIS-verify-pre-items9-10.md (Processor and the Update overloads)


M11-039 — IMPLEMENTATION_GAP — The calibration-install path into MarkerDetector::Init

> VisionSystem::UpdateCameraCalibration 0x006B1E3E; Camera::SetCalibration 0x006B1E5E; MarkerDetector::Init 0x006B1E78; Parameters::Initialize 0x008752E4/0x008752E6


M3-050 — HARDWARE_ONLY — Whether the robot ever sends an ImageChunk in encodings 5/6/7

> Encodings 5/6/7 reach imdecode from DecodeImageHelper (gray tbh 0x004F2898, RGB tbh 0x004F21AE); the encoding byte comes only from the robot's ImageChunk; ImageRequest cannot ask for 5/6/7 (research/20261010-m3045-m4033-rows.md A.3).
