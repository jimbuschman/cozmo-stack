# M11 native evidence map

## Provenance baseline

- Repository baseline: `2ad71a340543d3be32857a8244e90d2d6d889296`.
- Native binary: `libcozmoEngine.so`, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
- Shipped `vision_config.json`: SHA-256 `d99a7a00ffbdc07f6cd9834159a65f6c67dd8ce51da598fce6eded9e9312cdde`.
- The machine-readable symbol/address snapshot is `evidence/native-evidence.json`. Addresses below are ARM Thumb code addresses with the low Thumb bit normalized away.

## End-to-end production path

1. `VisionComponent::SetNextImage` (`0x00652B04`) admits a frame, checks component/calibration state and ordering, and associates robot state by the frame timestamp.
2. `VisionSystem::Update(EncodedImage)` (`0x006B4B68`) decodes/selects color or grayscale and calls `VisionSystem::Update(ImageCache)` (`0x006B4D5C`).
3. `Update(ImageCache)` refreshes pose data, evaluates mode schedules through `ShouldProcessVisionMode` (`0x006B5AA4`), performs shared preprocessing, and invokes detectors in engine order.
4. Markers go through `DetectMarkersWithCLAHE` (`0x006B4780`) and `DetectFiducialMarkers` (`0x00898760`), then camera pose solving through `Camera::ComputeObjectPoseHelper` (`0x0085D750`).
5. `VisionComponent::UpdateAllResults` (`0x006542EC`) consumes the completed result; `UpdateVisionMarkers` (`0x00654D60`) passes marker observations to BlockWorld.
6. `BlockWorld::AddAndUpdateObjects` (`0x00620AD4`) associates observations, uses the pose confirmer, checks unobserved objects, and broadcasts changes to downstream systems.

That path is the unit of fidelity. Component evidence below must not be promoted into an end-to-end claim unless every behavior-changing join is settled.

## Function evidence

| Area | Native function | Address | Establishes | Does not establish by itself |
|---|---|---:|---|---|
| Vision lifecycle | `VisionSystem::VisionSystem` | `0x006AFFD0` | Native component layout/default construction. | Shipped runtime settings after config load. |
| Vision lifecycle | `VisionSystem::Init` | `0x006B0658` | Initialization/calibration gates. | Frame scheduling. |
| Modes | `VisionSystem::EnableMode` | `0x006B19C0` | Enable/disable state. | Per-frame schedule. |
| Pose data | `VisionSystem::UpdatePoseData` | `0x006B24D0` | Camera pose data is derived for each admitted image. | Exact state-history interpolation. |
| Pipeline | `VisionSystem::Update(EncodedImage)` | `0x006B4B68` | Encoded-image entry and image-cache handoff. | Device-side chunk assembly (M3). |
| Pipeline | `VisionSystem::Update(ImageCache)` | `0x006B4D5C` | Detector order, shared preprocess, schedules, result production. | External OKAO internals. |
| Scheduling | `VisionSystem::ShouldProcessVisionMode` | `0x006B5AA4` | Frame-schedule decision is a first-class native behavior. | Values loaded from assets. |
| Marker preprocess | `VisionSystem::DetectMarkersWithCLAHE` | `0x006B4780` | CLAHE marker path exists. | Whether a particular runtime config enables it. |
| Frame admission | `VisionComponent::SetNextImage` | `0x00652B04` | Admission gates, timestamp checks, robot-state lookup. | Image transport decoding (M3). |
| Result delivery | `VisionComponent::UpdateVisionSystem` | `0x00653D28` | Worker/system update boundary. | Managed task equivalence. |
| Result delivery | `VisionComponent::UpdateAllResults` | `0x006542EC` | Complete-result consumption order. | Downstream implementation correctness. |
| Result delivery | `VisionComponent::UpdateVisionMarkers` | `0x00654D60` | Marker-to-world join. | BlockWorld internals. |
| Motion gate | `VisionComponent::WasRotatingTooFast` | `0x0065359C` | Rotation suppresses or qualifies vision work. | History interpolation. |
| Occlusion | `VisionComponent::AddLiftOccluder` | `0x006564D8` | Lift is explicitly inserted as a camera occluder. | General visibility logic. |
| State history | `RobotStateHistory::GetRawStateAt` | `0x00531430` | Time-indexed raw-state lookup. | Interpolated state calculation. |
| State history | `RobotStateHistory::ComputeStateAt` | `0x00531784` | Native state interpolation path. | Frame admission rules. |
| State history | `RobotStateHistory::CullToWindowSize` | `0x005309D0` | Native history retention behavior. | Local 2 s/66 ms constants. |
| Marker front end | `DetectFiducialMarkers` | `0x00898760` | Native detector orchestration. | Equivalence of a synthetic managed detector. |
| Marker front end | characteristic-scale routine | `0x00890448` | Native scale-selection stage. | Remaining image stages. |
| Marker front end | `BinomialFilter` | `0x008A2344` | Native smoothing stage. | Border/component traversal. |
| Marker front end | `IsQuadrilateralReasonable` | `0x00892B18` | Quad acceptance tests. | Candidate creation/deduplication. |
| Marker front end | `ComputeQuadrilaterals...` | `0x00892D70` | Quad construction path. | Decoder and pose solve. |
| Marker front end | boundary trace | `0x008C6B18` | Native component-boundary traversal. | Local capacity behavior. |
| Marker front end | `ExtractLineFitsPeaks` | `0x008A5DB8` | Line/peak extraction stage. | Complete quad equivalence. |
| Decoder | `VisionMarker::GetNearestNeighborLibrary` | `0x0089ED1C` | Compiled library/table construction call path. | Pixel detection. |
| Decoder | `GetProbeValues` | `0x0089EF40` | Probe sampling. | Code association. |
| Decoder | `ComputeBrightDarkValues` | `0x0089F8E8` | Bright/dark decision inputs. | Full threshold behavior without callers. |
| Refinement | `RefineCorners` | `0x0089FD98` | Native subpixel iteration. | Managed floating-point equivalence. |
| Refinement | `RefineQuadrilateral` | `0x008C55E0` | Quad refinement orchestration. | Object pose. |
| Pose | `Camera::ComputeObjectPoseHelper` | `0x0085D750` | Object/image point assembly and native PnP call. | Managed homography/LM equivalence. |
| Pose | `cv::solvePnP` call | `0x0085D8A2` | OpenCV PnP is the production solver. | Exact OpenCV result without golden output. |
| Visibility | `Camera::AddOccluder(KnownMarker)` | `0x0085E76C` | Projected marker occluder construction. | Lift insertion and unobserved policy. |
| Visibility | `KnownMarker::IsVisibleFrom` | `0x0087E4A8` | Projection/orientation/size visibility rules. | World state transition. |
| World | `BlockWorld::AddAndUpdateObjects` | `0x00620AD4` | Main observed-object update path. | Correctness of local IDs/association. |
| World | `BlockWorld::CheckForUnobservedObjects` | `0x00621C6C` | Expected-visible miss processing. | Exact occluder list construction. |
| World | `BlockWorld::OnRobotDelocalized` | `0x006249C4` | Delocalization side effects. | Local origin-change approximation. |
| World | `BlockWorld::UpdateObservedMarkers` | `0x00624EE8` | Per-frame marker list handling. | Object creation details. |
| World | `BlockWorld::CreateObjectsFromMarkers` | `0x0062539C` | Marker grouping/object candidates. | Connected-object lifecycle outside callers. |
| Confirmation | `ObjectPoseConfirmer::UpdatePoseInInstance` | `0x00505DE0` | Confirmed pose application. | How the first candidate was formed. |
| Confirmation | `ObjectPoseConfirmer::AddVisualObservation` | `0x0050684C` | Multi-observation confirmation state. | Visibility miss handling. |
| Confirmation | `MarkObjectUnobserved` | `0x00506FBC` | Miss transition. | Dirty transition from radio motion. |
| Confirmation | `MarkObjectDirty` | `0x005075A4` | Dirty transition. | Carrying-component early return without caller. |
| Overhead | `OverheadEdgesDetector::Detect` | `0x006ABE34` | Native ground-plane edge detector. | Map insertion. |
| Overhead | `MapComponent::ProcessVisionOverheadEdges` | `0x0067F7AC` | Frame-to-map processing boundary. | Detector equivalence. |
| Overhead | `MapComponent::AddVisionOverheadEdges` | `0x0067F814` | Clear/border map writes. | Map consumers. |
| Motion | `MotionDetector::Detect` | `0x006AAAF0` | Motion is Anki native code and recoverable. | External face/pet behavior. |
| External boundary | `FaceTracker::Impl::Update` | `0x0086D740` | Call boundary into OKAO. | OKAO internals. |
| External boundary | `PetTracker::Update` | `0x0087C980` | Pet tracker boundary. | Proprietary classifier semantics. |

## Shipped configuration evidence

`assets/cozmo_resources/config/engine/vision_config.json` enables marker, face, pet, motion, overhead-edge, image-quality, laser-point, and statistics modes at startup. It supplies a two-frame alternating schedule for pet and overhead-edge work, image-quality work every 15 frames, laser work every two frames, and detector settings for pet and motion paths. Unspecified marker/face/motion schedules therefore cannot be replaced by a blanket managed “run everything implemented” loop.

The asset establishes configured values, not implementation semantics. The native loader/call site still has to connect each key to a field before a value can support an `EXACT_SOURCE` claim.

## Direct contradictions found

- The production native pose path calls OpenCV `solvePnP`; the managed path uses planar homography plus a locally authored optimizer.
- Native state-at-image processing interpolates through `RobotStateHistory::ComputeStateAt`; the managed history selects a nearest sample.
- Native visibility explicitly calls `AddLiftOccluder`; the managed production path does not.
- Native mode scheduling is asset-driven; the managed path has a fixed set and leaves overhead edges disabled unless another subsystem installs it.
- Native motion detection is recoverable Anki code; classifying it with OKAO as `BLOCKED_EXTERNAL` is unsupported.
- Native pose confirmation has a multi-observation path; managed BlockWorld sets a newly observed object to `Known` immediately.

## Reproduction

Run `scripts/dump-native-evidence.ps1` with the paths to the original binary and shipped OBB tree. It hashes inputs and records the selected symbols without changing either source. The checked-in JSON is a snapshot, not a substitute for reviewing the disassembly at each cited address.
