# R-M14-IMAGE-INPUT — pet pixels, image timestamp and calibration

Request: `requests/20261021-m14-image-coordinate-input.md`. HEAD3f5a1337. Independent read-only report; coverage opened before tracing. No policy/status change.

## Coverage

| Join | Evidence / current source | Result |
| --- | --- | --- |
| Native selected pet→center/time | 0x00607010..0x0060703E,0x006070DA..0x006070E0 | Raw rectangle pixel center and TrackedPet+0xC timestamp |
| Detector input→rectangle | PetTracker.Update0x0087C980..0x0087CC68, selected conversion | Image dimensions/data passed to OMCV; integer result coordinates become float rectangle, no normalization |
| Pet world handoff | Adopted UpdatePets0x00655360..66; fresh PetWorld copy window0x0050B7F4..0x0050B95E | TrackedPet rectangle/stamp copied, not pose-projected |
| Optical center | Existing helper0x005187A6..EA and0x00518864..8E; adopted Camera.SetCalibration | Native helper uses currently installed camera calibration directly |
| Host producer | IPetDetector/OkaoPetDetector; ProcessCapture/ProcessImage/Pets.Update | Default detector unavailable; injected results in gray-image coordinates |
| Host dimensional join | ProcessImage builds local scaled cal; later image turn reads Vision.Calibration | No pet rectangle rescale/calibration association installed; mismatch possible with size differing from stored calibration |
| Fixtures | FaceTests direct Pets.Update + nominal calibration | Bypass real detector/decoder/frame provenance |
| Supported decoder size↔native calibration identity | Existing M3/M11 inputs, no full native pet-image dimension producer traced here | UNKNOWN for all encodings/custom frames; do not assert a live robot mismatch from a synthetic size |

## Result

Native ReactToPet supplies **pixel rectangle center and the detected image's timestamp**, not normalized coordinates, a3D point, a fixed-distance ray or the latest robot timestamp. Current C# passes the same kinds of values from PetEntry. However **the real pet detector is unavailable**, so existing pet fixtures bypass the producer that would establish those rectangles' scale and provenance.

Current ProcessImage may scale calibration to the decoded gray dimensions for its local CameraModel, but TurnTowardsImagePoint later reads the installed, unscaled Vision.Calibration. No code converts pet rectangles to that installed calibration's coordinate frame or retains which calibration size produced them. This is a concrete host input-contract gap for differing-size images, not proof that ordinary robot frames actually reach that mismatch. Establishing native supported decoder resolution/calibration relation remains UNKNOWN here; no guessed resize is recommended.

## Source reuse and new narrow reads

Reuse `20261021-m14-image-turn-history-join.md` (pending comparison), M14 inventory/extraction/check and M3/M11 camera calibration/ImageCache records. Do not treat the early M14 EXACT_SOURCE inventory rows as current settlement. The fresh native reads below address only previously open pet input joins, not history arithmetic, PanAndTilt or all detector internals.

**Behavior input.** BehaviorReactToPet.BeginIteration0x00606FA8 reads its target-ID set and resolves the first existing pet through PetWorld.GetPetByID0x00606FD8. Selection differs from current latest-pet selection, but no broader behavior audit is performed here. For the resolved TrackedPet:
-0x00607014 loads height+0x20;0x00607018 loads width+0x1C;
-0x0060701E/22 load x+0x14/y+0x18;
-0x00607028/2E multiply sizes by0.5f;
-0x00607032/36 add to y/x in binary32 and store the2D point;
-0x006070DA loads timestamp+0xC;
-0x006070E0 constructs TurnTowardsImagePointAction with that point/time.
No coordinate normalization, undistortion, world pose or millimeter conversion appears in this input block. The requested point is a rectangle midpoint, not a pose estimated from apparent pet size.

**PetTracker boundary.** Read normal/error/unwind instructions0x0087C980..0x0087CC68 (following literals are data):
- Detect calls OMCV_PD_Detect0x0087C9AE with Image data/dimension fields read at+0xC/+0x10/+0x14 (0x0087C9A2..AC).
- GetResultCount and GetResultInfo are the third-party boundary; no OKAO internals recovered.
- Successful known pet type constructs a TrackedPet at stack+0x20.
- Integer size fields become float width/height (0x0087CB10..2A), integer center minus signed truncating half-size becomes rectangle x/y (0x0087CB18/20/2E/3A, conversion0x0087CB42/46), stored at0x0087CB4C/50/5A/5E.
-0x0087CB62 loads input Image+0x3C timestamp;0x0087CB6C writes it at TrackedPet+0xC.
-0x0087CB72 appends TrackedPet to result list.
No factor2, camera center subtraction or normalized-coordinate transform is applied in this result conversion. These are pixel units of the actual Image passed to detector. OMCV result production remains the external seam.

An odd positive width matters: integer center100,width61 yields x70 via truncating half30, then ReactToPet center100.5. Do not silently replace the recovered conversion with “rectangle center equals detector integer center” or an all-double divide in the wrapper. This is an independent boundary example, not a measured shipped pet detection.

**World handoff.** Adopted M14 F29/check forwards the tracked list through VisionComponent.UpdatePets0x00655360..66 to PetWorld.Update0x0050B7F4. Fresh copy windows through0x0050B95E show stored TrackedPet fields copied, including rectangle words, and no camera reprojection in those assignments. The whole PetWorld detection/removal/notification algorithm is not re-extracted. Its first-observation rotation gate and later bookkeeping remain M14-004; no universal no-transform claim outside this bounded input copy is made.

**Calibration.** The already read image-turn helper reads the camera's shared calibration pointer (Robot+0x258→+0x28), subtracts calibration center at+0xC/+0x10, and uses focal lengths+4/+8. It does not call GetScaled for this point. The native Camera.SetCalibration/UpdateCameraCalibration contract is existing M11-039 evidence; native memory fields are not the CLAD serialized field order.

## Current C# route

Paths under `cozmo-stack/src/Cozmo.Robot/`.

- `Vision/Faces.cs` DetectedPet carries Id, Type, FaceRect and score. FaceRect is documented pixel coordinates; Center uses double x+w/2,y+h/2. PetEntry stores Rect and LastObservedTimestamp.
- Default OkaoPetDetector.IsAvailable=false; Detect returns no detections. IPetDetector accepts GrayImage and timestamp, with no calibration/coordinate-frame carrier. It does not enforce the native integer center/size conversion, so an injected implementation must honor that separately.
- `Vision/VisionSystem.cs` live ProcessCapture(ImageCache,...) obtains cache.GetGray, preserves cameraTimestamp after historical-pose selection, then calls ProcessImage. PetDetector.Detect(gray,timestamp) sees those decoded pixels; detectedPets reaches Pets.Update(pets,timestamp,pd.RotatingTooFast). The selected history timestamp is not substituted for observation time; image timestamp remains the observation producer.
- PetWorld.Update copies d.Rect, writes the supplied timestamp, increments observations. It does not resize or retain the gray image dimensions. Its current blanket rotatingTooFast return differs in scope from native new-pet gate, an existing M14-004 candidate; not a new complete algorithm finding here.
- `Behavior/FaceBehaviors.cs` ReactToPet takes current selected pet.Rect.Center and pet.LastObservedTimestamp, then TurnTowardsImagePoint.RunAsync. It does not use current image receipt time or invent range. Float/double center rounding differs for arbitrary noninteger/high-magnitude inputs; actual detector-domain conversion is missing, so do not claim full parity for all injected rectangles.
- `Vision/CameraCalibration.cs` Unpack/Parse use serialized five floats, two u16 and eight distortion floats (56 bytes). Production install uses robot camera settings/NV calibration through CalibrationInstalled and Vision.UpdateCameraCalibration; constructor calibration is also an explicit host input. Nominal() is documented unsourced offline stand-in, not real robot evidence.
- UpdateCameraCalibration preserves nonnull pointer identity behavior and assigns Calibration, then initializes marker detector. RunAsync later reads this installed property; it does not read the local CameraModel used for the detection image.
- ProcessImage line1016 computes local cal = calibration if dimensions match, otherwise calibration.Scaled(gray.Width,gray.Height), then local CameraModel. Scaled scales focal lengths/center by dimension ratio. PetDetector is still passed only gray/timestamp, and no conversion is applied to its returned rectangles.
- Consequently, if installed calibration is320×240 but an admitted injected160×120 gray frame yields pet center(80,60), image-turn receives(80,60) while subtracting the320-frame center(160,120). A centered half-size image would be treated as off-center. This is a concrete conditional host mismatch, not evidence native decoders actually feed that combination.

The real decoder/input dimension relation is M3-045/M3-050/M11-033 plus the calibration producer. This report does not repeat their entire format pipeline. Need to verify actual supported encodings' output dimensions and native PetTracker input construction before deciding whether a resize, frame conversion or host input restriction is correct. Current unknown cannot be replaced by assuming every frame is320×240.

## Fixtures and bounded next step

`FaceTests.ReactToPetTurnsToThePetAndPlaysItsTrigger` directly calls Pets.Update with a rectangle(100,80,60,60) and timestamp1000, then runs a hand-built behavior. It bypasses robot decoder, OMCV, native integer result conversion and image/calibration dimension association. Its trace/target assertions are not a calibrated input-source oracle. Other tests explicitly assert PetDetector unavailable or supply DetectedPet values. The prior scalar Angles tests take nominal calibration directly. No tests run here.

Smallest next recovery/comparison:
- finish the exact supported gray-image dimension producer passed to native PetTracker and compare its dimension to installed camera calibration;
- preserve detector pixel rectangle/timestamp in that proven frame, including native integer half-size behavior when implementing the wrapper;
- establish whether supported host custom detectors/ProcessImage permit different dimensions; if so, retain the actual frame/calibration relation explicitly rather than silently rescale based on a plausible assumption.
- real OMCV detector availability remains its existing external/input dependency; do not substitute fixture rectangles and call the supported pet pipeline complete.

Independent future regression: use decoded image dimensions and robot calibration from a real supported source, a native/emulated detector result with odd sizes and known center/stamp, pass through PetWorld/ReactToPet and inspect the exact point/timestamp at image-action construction. Include matching and differing image/calibration dimensions only when those combinations are source-backed/admitted. Tests may supply raw OMCV boundary outputs to isolate Anki conversion, but must label that substituted boundary.

Remaining UNKNOWNs: OMCV internal recognition; actual supported native pet-image resolution versus calibration and any upstream resize; arbitrary host custom-detector coordinate contract; complete PetWorld/ReactToPet behavior selection and production availability. No policy, inventory approval, hardware or settlement.

## Current records/evidence

M14-004 — IMPLEMENTATION_GAP — PetInitialDetection: a pet not reacted to, and a minute between reactions

> The strategy keeps the pet ids it has reacted to in a tree at +0x34: UpdateReactedTo 0x00611E1C walks the PetWorld (robot+0x3c) and inserts each pet id (node+0x10), and is called from ShouldTriggerBehaviorInternal 0x00611AE4 and BehaviorDidReact 0x00611E80. InitReactedTo 0x00611FA4 clears and refills the set, but no instruction in the shipped .so calls it (whole-.text BL/BLX, relocation and pointer scan): it is not part of the reachable path.

> RecentlyReacted 0x00611DD0 is the cooldown: the last reaction time sits at +0x40 as a float, -1 meaning never, and the answer is true while lastReacted + 60 is still ahead of BaseStationTimer::GetCurrentTimeInSeconds - the 60 is the 0x42700000 loaded at 0x00611DE8.

> This stack latched on the first sighting of any pet id and had no cooldown; it now keeps the reacted-to set and the sixty seconds.


M14-005 — IMPLEMENTATION_GAP — TurnTowardsImagePoint turns by the angle the pixel subtends

> There is no distance in it. ComputeTurnTowardsImagePointAngles 0x0051879C subtracts the calibration's centre from the point (the two-float loop at 0x005187CC), takes the historical state at the image's timestamp through RobotStateHistory::ComputeStateAt, and computes atan2(-(v - cy), fy) for the head (0x0051886C with the focal length at calibration+8) and atan2(-(u - cx), fx) for the body (0x0051888E, focal length at +4). The head angle is added to the head angle in that state and the body angle to its heading, so both come out absolute.

> TurnTowardsImagePointAction 0x0054B59C is a PanAndTiltAction: Init 0x0054B664 asks for those two angles, writes them into the pan and tilt fields at +0x114 and +0x11C, and runs PanAndTiltAction::Init. When the history cannot answer it warns "TurnTowardsImagePointAction.Init.ComputeTurnTowardsImagePointAnglesFailed" and turns nowhere.

> This stack aimed at a point 200 mm along the ray through the pixel, a range it invented, and then turned towards that pose. It now computes the same two angles and pans and tilts to them.


M11-033 — IMPLEMENTATION_GAP — The image hand-off and the two VisionSystem::Update overloads

> VisionComponent::SetNextImage 0x00652B04; Processor 0x00651F08; UpdateVisionSystem(pose, image) 0x00653D30

> VisionSystem::Update(PoseData, EncodedImage) 0x006B4B68: IsColor 0x006B4B7C, DecodeImageRGB 0x006B4B90 or DecodeImageGray 0x006B4C02, then Update(PoseData, ImageCache) 0x006B4C78

> Update(PoseData, ImageCache) 0x006B4D5C: the pose and image guards 0x006B4D66/0x006B4D70, UpdatePoseData 0x006B4D88, GetGray 0x006B4D94. ImageCache layout: grey Image at entry+0x14, RGB ImageRGB at entry+0x54, grey-valid +0x94, RGB-valid +0x95 (ResetHelper<Image> 0x008744F0, ResetHelper<ImageRGB> 0x0087459E, GetImageHelper 0x0087465C). An RGB entry becomes grey via ImageRGB::FillGray 0x00872998 -> cv::cvtColor(rgb,gray,COLOR_RGB2GRAY=7,0) 0x008729CA (libopencv_imgproc.so 0x2BC78). EncodedImage::IsColor 0x4F2100 (encoding byte at +0x20, tbb table 0x4F2110). For the shipped 320x240 grey camera the colour branch is inert: only the grey member is stored, +0x95 stays 0 and FillGray is never called

> re-analysis/research/20260929-R-VIS-pre-extraction.md Part 2 item 8; re-analysis/research/20260929-R-VIS-verify-pre-items7-8.md (rows C1-C11 PASS)

> re-analysis/research/20260929-R-VIS-verify-pre-items9-10.md (Processor and the Update overloads)


M11-039 — IMPLEMENTATION_GAP — The calibration-install path into MarkerDetector::Init

> VisionSystem::UpdateCameraCalibration 0x006B1E3E; Camera::SetCalibration 0x006B1E5E; MarkerDetector::Init 0x006B1E78; Parameters::Initialize 0x008752E4/0x008752E6
