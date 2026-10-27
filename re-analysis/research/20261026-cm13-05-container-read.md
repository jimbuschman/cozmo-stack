# CM13-05: common native configuration-container producer

Actual selected model/reasoning telemetry is unobservable; requested configuration was gpt-6.1-sol medium. Phase1 extraction only, 2026-10-26. Contract: jobs/trial-m13/CM13-05.md. Read PROJECT_STATE, AGENTS, AUTONOMY and current REVIEW (R1 MERGED supersedes FIX). No code/test/status/manifest/git mutation or robot commands.

| Coverage | Result |
|---|---|
| Frame-tail gate, force/pending/root-cache, notify | Reuse B-M13-15/18 and latest row36/44 check; not reopened |
| Common Update, all3 types, query/order/processed IDs | Read native instructions; inventory1–8 |
| Old/current ownership, identity/equality, prune/membership/tallest | Read native instructions; inventory9–14,25–29 |
| Stack builder and carried ID | Read native instructions; inventory15–18 |
| Pyramid-base and pyramid producer joins | Read native instructions; inventory19–24; previously checked footprint internals reused only at their explicit boundary |
| Real C# binding, host event/time adapter | Compared; explicit boundary below |
| Remaining unsupported geometry/host inputs | MISSING, never an all-pairs or center/radius fallback |

## Current records, read before reliance

The following current manifest excerpts are literal title/status/evidence; M12-049 and M12-043 also include full unresolved. Statuses are unchanged. M12-042 is quoted only to establish that its action/helper lifecycle is separate, not a dependency silently rebuilt here.

```json
{
  "id": "M12-049",
  "title": "BehaviorBuildPyramid helper callbacks and the pyramid cache contents (Container::Update, CastNewEntryToBestSharedPointer, PruneFullPyramids, PyramidBase::operator==)",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-ov69-74-75-77-check.md: callbacks vtables 0x01029068 / 0x010290A8 (built 0x005DC16C..0x005DC17C, passed at 0x005DC298); cache UpdateAllBlockConfigs 0x00616F00 -> Container::Update 0x0061682C -> AddAllConfigsWithObjectToCache 0x0062C56E -> CastNewEntryToBestSharedPointer 0x0062C68C; PruneFullPyramids 0x0062C72E."
  ],
  "unresolved": "RECOVERABLE_GAP: read the two callbacks' operator() slots and the cache bodies (check M11-037/M12-042 ownership first). B-M12-6 boundary: BlockConfigurationManager.CopyNativePyramidBaseCache copies an explicitly supplied native strong-cache vector (manager+48), with visible ReportMissing when its native producer is absent; no copy from host all-pairs snapshot. BuildPyramid checked loop installs explicit ReportMissing callback recipients at vtables0x01029068/0x010290A8; no success/failure state transition is guessed. This record remains RECOVERABLE_GAP and those bodies/cache semantics are unbuilt."
}
```

```json
{
  "id": "M12-043",
  "title": "PyramidBase interior geometry: corners and midpoint for the pyramid top placement (GetBoundingQuad<float> corner order)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: corners/midpoint 0x006177F0..0x00617A08, 0x00617A8C..0x00617C32, multimap 0x00618A4C (equal keys keep insertion order, NaN right); unread GetBoundingQuad<float> 0x004E6490..0x004E6598 and Block::GetBoundingQuadXY 0x004E62A2.",
    "re-analysis/research/overnight/20261015-ov45-47-check.md: quad call 0x00617818..0x00617824; GetCanonicalCorners 0x004E60D8..0x004E6146 (8 corners at +-0.5: (-,-,+),(+,-,+),(-,-,-),(+,-,-),(-,+,+),(+,+,+),(-,+,-),(+,+,-)); pad/scale/rotate 0x004E62C0..0x004E6386; minAreaRect points opencv_core 0x00086188..0x0008627A; Quad 0x004E64FE..0x004E655A; centroid 0x004DF7BE..0x004DF84C; sort 0x004E7F24..0x004E7F96, 0x004E8508..0x004E8642; translation 0x004E639A..0x004E63B2; insertion 0x00617826..0x006179BC; midpoint 0x00617B98..0x00617BF6; container 0x006168EC..0x006169C4; builder 0x0061830A..0x006183B4; cache 0x0062C5A2..0x0062C616."
  ],
  "unresolved": "RECOVERABLE_GAP: read GetBoundingQuad<float> (it fixes the corner order and so the tie order); also whether two PyramidBase entries can both contain A and B (T7 replace path reachability). Read 2026-10-15 (re-analysis/research/overnight/20261015-ov45-47-check.md): IMPLEMENTATION_GAP for the pyramid-base geometry and container (duplicate AB not producible in a normal world). Helper stop semantics and BehaviorBuildPyramid's top-loop source are M12-047. Not re-verified: convexHull/minAreaRect internals (opencv), LSDA catch at 0x004E65C6. B-M12-5 (2026-10-16), built, awaiting strong verification: actual Footprint.TryGetBoundingQuadXY now feeds all eight canonical Block vertices through pad/scale/full f32 quaternion rotation, checked shared MinAreaRectF/points, centroid/sort physical layout and post-sort root translation. Removed yaw-only heuristic. Unported hull branches report stack MISSING. Native current-origin query/predicate, pose-distance/world-origin/robot-root joins, nearest-two/midpoint, processed-ID container and old-cache identity remain unbuilt; candidate all-pairs snapshot is visibly labelled. No claiming the candidate center average as native midpoint. M12-047 top-list boundary remains explicit."
}
```

```json
{
  "id": "M11-037",
  "title": "The BlockWorld frame sequence for observed markers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BlockWorld::UpdateObservedMarkers 0x00624EE8: non-empty path is ClearOccluders 0x00624F98, AddLiftOccluder 0x00624FA4, CreateObjectsFromMarkers 0x00624FC4, AddAndUpdateObjects 0x0062505A, then (on its success) CheckForUnobservedObjects 0x006250CA, UpdatePoseOfStackedObjects 0x006250D0, BlockConfigurationManager::Update 0x0062520C, UpdateMarkerlessObjects 0x0062521A; the empty-list branch 0x625020 does ClearOccluders, AddLiftOccluder, CheckForUnobservedObjects 0x0062504E and skips the stacked-pose update. AddLiftOccluder body 0x6564D8 (raw state at the timestamp, Robot::GetLiftTransformWrtCamera, Transform3d::ApplyTo, Camera::Project3dPoints, Camera::AddOccluder with the transform z scale); BlockConfigurationManager::Update body 0x616D7C (DidAnyObjectsMovePastThreshold gate, UpdateAllBlockConfigs, PruneFullPyramids, UpdateLastConfigCheckBlockPoses, NotifyBroadcasterOfConfigurationManagerUpdate). UpdatePoseOfStackedObjects 0x621794 and UpdateMarkerlessObjects 0x625704 are cross-layer entry points (M10/M12)",
    "re-analysis/research/20260929-R-VIS-verify-pre-items4-5.md (items 4a, 4b PASS)",
    "re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q3, Q5; re-analysis/research/20260929-R-VIS-verify-M11-gap1.md (objections 2 and 5 corrected)",
    "0x00507824..0x0050783A, 0x00621D54..0x00621D88, 0x0062206E..0x006220DA, 0x00622106..0x0062218E, 0x006211E6..0x00621214, 0x00625028, 0x0062521A, 0x00620BE2",
    "re-analysis/research/20260929-R-VIS-verify-M11-batchA.md objections 2, 5, 8, 9, 10"
  ]
}
```

```json
{
  "id": "M11-004",
  "title": "BlockWorld connected-object rule, moving and rotating gates, position-update thresholds",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "position-update thresholds: 45 deg 0x3F490FDB to [this+0x2c] (ctor 0x00612182), 80 mm 0x42A00000 to [this+0x38] (0x006121A2), 600000 ms 0x000927C0 to [this+0x34] (0x00612198); used at 0x006124C6..0x006124F2 and 0x00612592..0x006125A6",
    "connected-object rule: AddAndUpdateObjects 0x00620AD4 warns and records a 10 s cooldown when the connected counterpart is absent (0x00620E70..0x00620EDA) and continues to 0x00620EDE; it does not drop the object",
    "moving gate: CheckForUnobservedObjects 0x00621C6C skips when MovementComponent::WasMoving(timestamp) != 0 (0x00621C88..0x00621C94); WasMoving is HistRobotState+0x58 & 1 (IS_MOVING), lambda 0x00642672",
    "rotating gate: WasRotatingTooFast(timestamp, 0.174533, 0.174533, 0) 0x00621C9A..0x00621CAE; the ImuDataHistory is VisionComponent+0xb0 (0x656286/0x6562B4/0x6563AC/0x6563D8) filled by HandleImageImuData 0x535C20 -> ImuDataHistory::AddImuData 0x538B24 (ImuData: timestamp +0, rateX +4, rateY +8, rateZ +0xC, u8 +0x10); head uses rateY, body rateZ, |rate| > threshold, a missing IMU bracket returns 1",
    "object match: ObjectPoseConfirmer::FindObjectMatchForObservation 0x5063CC; tolerances from the observed object's virtuals vptr+0x30 = 0.8*extent (thunk 0x4E025C, 0.8 at 0x4E028C) and vptr+0x34 = pi/4 (thunk 0x4E0290); primary FindLocatedClosestMatchingObjectHelper 0x61FA68 predicate 0x6281DA (ObjectType match + Pose3d::IsSameAs, narrowing the captured tolerance to abs(delta), closest wins); fallback over the confirmer's list with ObservableObject::IsSameAs 0x8769A8 (last match). FindLocatedObjectHelper 0x61EB78's returned object is a RECOVERABLE_GAP",
    "0x0061EB94 filter copy; 0x0061EB98..0x0061EBCE onlyLatest; 0x0061EBD0..0x0061EF1C iteration, origin/family/type/ID tests, predicates, modify function and the last-passing-wins result; 0x008769A8..0x008769D8 IsSameAs",
    "re-analysis/research/20260929-R-VIS-pre-extraction.md Part 1 item 2 rows 2.1-2.9; re-analysis/research/20260929-R-VIS-verify-pre-items1-3.md (all PASS)"
  ]
}
```

```json
{
  "id": "M13-007",
  "title": "FindObjectOnTopOrUnderneathHelper: its found-object test and the tolerance at each of its eleven callers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BlockWorld::FindObjectOnTopOrUnderneathHelper(obj, float tolerance, filter, bool) 0x0062601C takes the tolerance as its second argument (r2 captured at 0x00626038 into the lambda at 0x006260CE/0x006260D8), reads the height through GetDimInParentFrame<(char)90> (0x00626070), adds +/-half to the parent Z (0x0062607E..0x006260A0) and calls FindLocatedObjectHelper (0x0062610E)",
    "StackOfCubes::BuildTallestStackForObject passes 30 (0x41F00000 at 0x0061928C and 0x00619344); BlockWorld::UpdatePoseOfStackedObjects 0x00621908, DockingComponent::CanInteractWithObjectHelper 0x0063C728 and CarryingComponent::SetObjectAsAttachedToLift 0x00632F0C pass 15 (0x41700000)",
    "0x0062601C..0x0062619A the helper; 0x0062B9FC..0x0062BB1E the predicate lambda (vtable 0x0102E194): 0x0062BA0E..0x0062BA46 wrt-parent, 0x0062BA50..0x0062BAA2 quad intersection, 0x0062BAC2..0x0062BB0C the height test",
    "the eleven call sites and their tolerance/onTop: 0x00507258, 0x00507664, 0x0055526E, 0x005BA136, 0x005C99C0, 0x0061929E, 0x00619358, 0x0062190C, 0x00625E1A, 0x00632F14, 0x0063C730",
    "the CORRECTION: 'everywhere else 15 mm' is wrong; RollBlockHelper::UnableToRollDelegate passes 30.0 (0x005BA12C movt r2,#0x41f0), and BuildTallestStackForObject's second call (0x00619358) is the underneath search",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md priority check 4",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q1; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q1 (PASS, with the float-direction omission recorded above)",
    "0x008F6220..0x008F6304, 0x008F6320..0x008F638C, 0x004E03B8..0x004E047E, 0x004DF764..0x004DF7BC",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q6; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q6"
  ]
}
```

```json
{
  "id": "M13-023",
  "title": "Footprint corner ordering: SortCornersClockwise (centroid, atan2f, __sort4/sort3 ties, Quadrilateral slots) and the cv::Rectangle fallback (empty input, bounds, center); corner writers read and unreachable/out of scope",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00502DD8, 0x004EF78A the corner getters; 0x0084AAE0, 0x008494E0, 0x008499B0 the rotation-matrix path; 0x004E7EFC SortCornersClockwise; 0x004E6778 InitFromPointContainer",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md open questions; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md 'What stays open'",
    "re-analysis/research/overnight/20261014-ov08-m13-023-check.md: S1-S6 0x004DF7BE..0x004DF84C, 0x004E7EFC..0x004E7F9C, 0x004E8260..0x004E8342, 0x004E847E..0x004E84E8, 0x004E8508..0x004E8642, 0x004E7E9C..0x004E7EFA; R1-R3 0x004E81B4..0x004E825E, 0x004E676A..0x004E67C4. Corner writers: MatPiece 0x00502DD8..0x00502ECE, FlatMat 0x004F6B58..0x004F70C0 (no constructor caller outside CloneType); CustomObject 0x004EF790..0x004F0340, clone 0x004F0470..0x004F04DC (callers only BlockWorld DefineCustom*/CreateFixedCustomObject handlers 0x0061E1EC..0x0061E6xx and DockingTestSimple 0x005CC1C0)."
  ]
}
```

```json
{
  "id": "M13-024",
  "title": "The exact footprint: cv::minAreaRect, convexHull, Sklansky_ and RotatedRect::points",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x004E6490..0x004E6570, 0x004E65C6..0x004E67C4, 0x0009C760..0x0009CEAC, 0x00039174..0x005395E0, 0x0003781A..0x00037968, 0x00086188..0x00086276",
    "re-analysis/evidence/m13-minarearect/port.py, emu.py, emu2.py, emu4.py, cmp2.py",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q1; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q1"
  ]
}
```

```json
{
  "id": "M12-012",
  "title": "What makes a cube a valid bottom to stack on: resting flat within ten degrees",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0063C5C4 CanStackOnTopOfObject; 0x0063C654 CanInteractWithObjectHelper",
    "0x0063C66C movw r1,#0xB8C2 + 0x0063C670 movt r1,#0x3E32 = 0.174533 rad = 10 deg; 0x0087751C IsRestingFlat",
    "the 15.0 is owned by M13-007",
    "IsPoseTooHigh 0x00877954: 0x00877978 D=GetDimInParentFrame<'Z'> (0x00557794); 0x00877990 s0=D*f1; 0x00877994 s16=D*f3; 0x00877998 s18=D*f1+f2; 0x008779A0 pose.z ([r0+0x28]); 0x008779AA D*f3+pose.z; 0x008779AE +1e-5 (0x3727C5AC); 0x008779B2 vcmpe; 0x008779BA movmi r0,#1",
    "0x0063C6C0..0x0063C730, 0x0063C78A..0x0063C792, 0x0063C5FE..0x0063C61C, 0x0063C840, 0x0063C8C2..0x0063C8D2",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q7"
  ]
}
```

```json
{
  "id": "M12-028",
  "title": "The behaviour-owned PlaceRelObjectParameters producers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055E2CA, 0x0055E3E0, 0x0055EB74, 0x0055EC2A, 0x005B58C2 the installers; 0x005C9588, 0x005DC266, 0x005DCE44 the parameter producers",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1, Q2 and open questions 2 and 6",
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: rows 1-5. Stack 0x005C9520..0x005C961A; top 0x005DC08C..0x005DC322 (+0x005DD134, 0x005DC78A, 0x005DC7F0..0x005DC9A4); base 0x005DCDA4..0x005DCF38; UpdateBlockPlacementOffsets 0x005DD140..0x005DD3B4; CheckBaseBlockPoseIsFree 0x005DD650..0x005DD8FC with 0x0061F8EC..0x0061F9E0 and 0x00627F92..0x00628064."
  ]
}
```

```json
{
  "id": "M12-033",
  "title": "The object-pose queries the placement path uses: GetZRotatedPointAboveObjectCenter, GetClosestMarkerPose and the Radians comparisons",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00877574, 0x00877774, 0x0084C87C, 0x0084CC0A, 0x0084CC90, 0x0084AA1C, 0x00557794",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q1-Q3; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q1-Q3 (PASS). Caveat: x, y are parent-frame values while z and yaw are root-relative, and the parent is set to root: consistent only when the object's parent is the root"
  ]
}
```

```json
{
  "id": "M11-065",
  "title": "Pose3d / Rotation3d / Transform3d arithmetic: double unit quaternion, float translation, float point rotation, renormalised composition (rows R1-R34, R38-R40)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: rows R1-R34 and R38-R40 (storage 0x0084A656.., Normalize 0x00849DB8..0x00849E52, product 0x00849EF4..0x00849F8E, Rotation3d*Point3f 0x0084ACA6 -> UnitQuaternion<float>* 0x00849C1C..0x00849CF8, GetAngleDiffFrom 0x0084A694..0x0084A746, GetAngleAroundZaxis 0x0084AA1C..0x0084AAB8, GetRotationMatrix 0x0084AAE0..0x0084ABEC, Renormalize 0x008494E0..0x0084969E, Transform3d ops 0x0084B92A..0x0084BB98, Pose3d ctors 0x0084683C..0x00846DEE, PoseBase ops 0x0084505C..0x008451EA, IsSameAs_WithAmbiguity 0x00846EA4..0x008471A6, RotationMatrix3d ops 0x0084B0E4..0x0084B1B4)."
  ]
}
```

```json
{
  "id": "M11-066",
  "title": "The pose tree: GetWithRespectTo (null/alias checks, depth walk, Tt^-1*Tf, 1000-step LoopBoundOverflow) and GetWithRespectToRoot (rows R35-R37)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: rows R35-R37: GetWithRespectTo 0x00845A34..0x00845DFE (FromEqualsTo warning, chain cases a-d, LoopBoundOverflow poseBase_impl.h lines 0x1A0/0x1B6/0x1D4), GetTreeDepth 0x00845E8C..0x00845F02, HasSameParentAs 0x00844D44..0x00844D7C, GetWithRespectToRoot 0x004DF628..0x004DF6DC."
  ]
}
```

```json
{
  "id": "M11-067",
  "title": "cv::Rodrigues (OpenCV 3.1.0, both directions, CV_32F in, double inside, Jacobi SVD) and the pose-tree copy semantics (deep copy ctor, in-place operator=, shared-node SetParent, \"_COPY\" names)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: \"Not read\" section: cv::Rodrigues via RenormalizeHelper 0x008499B0..0x00849A10, Rotation3d(RotationMatrix3d) 0x0084A3F4..0x0084A45C, RotationMatrix3d(angle, axis) 0x0084ADA4..0x0084AF34; PoseBase::operator= 0x00845940, SetParent 0x00844A14, PoseTreeNode ctors 0x00845F44/0x00845FE4; UnitQuaternion<double>*Point3d 0x00849F90..0x0084A062; Euler/matrix X/Y 0x0084AF98..0x0084B424; IsValid 0x008497D0..0x008498E4; Abs 0x0084B51E; ApplyTo(vector) 0x0065671C; the Pose2d/Transform2d family 0x0084B619..0x0084B92A.",
    "re-analysis/research/20261013-rodrigues-and-pose-tree.md: cvRodrigues2 calib3d 0xF28E..0xFB9A (vector->matrix in double, DBL_EPSILON identity, R = c1*rrt + c*I + s*[r]x; matrix->vector cvCheckArr [-100,100) -> zero, cvSVD -> JacobiSVDImpl_<double> core 0x595C0..0x59B56 with OpenCV hypot 0x56EB0, GEMM U*Vt, s<1e-5 and 180-degree branches, acos), wrapper 0x111F8..0x1133E; PoseBase copy ctor 0x008458F0..0x0084592A, node copy ctor 0x00845FE4..0x00846044, operator= 0x00845940..0x00845A2E, SetParent 0x00844A14..0x00844A7A, node SetParent 0x008449EA..0x00844A10, WrapExistingNode 0x0084529C..0x008452C0, GetWithRespectTo write order 0x00845A34..0x00845E04."
  ]
}
```

```json
{
  "id": "M12-042",
  "title": "PlaceRelObjectHelper lifecycle: ctor, Init (pre-action check, DriveTo delegation), StartPlaceRelObject (3-attempt counter), RespondToPlaceRelResult, MarkFailedToStackOrPlace",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: ctor 0x005B8F00..0x005B8F82; Init 0x005B8FD0..0x005B90FE; Start 0x005B9168..0x005B930E; Respond 0x005B94C0..0x005B952E; MarkFailed 0x005B93A8..0x005B9460; invokers 0x005B956A, 0x005B95D6."
  ]
}
```

## Evidence and method

Primary: local resources/lib/armeabi-v7a/libcozmoEngine.so, SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Thumb disassembly through LIEF/Capstone, ELF dynamic relocations resolved for virtual slots. Scratch `.scratch/cm13-05/` holds raw dumps, data.py/pred.py and known.txt; initial known preflights for61682C/616F00 were printed before tracing. Every subsequently traced function was preflighted with known.py. Decompiler files were unavailable at the reported navigation paths; no decompiler claim is evidence.

Reused checked sources: 20261022-r-m11-pose-config-check.md; 20261023-b-m13-15-claude-check.md and -18-claude-check.md; B-M12-5/6 contracts and preflight; overnight/20261016-ov69-74-75-77-check.md; overnight/20261015-ov45-47-check.md and its detailed OV46 report. Historical 20261021 'not released/root approval' wording is superseded by B-M13-15/18, not a new blocker. Geometry below re-reads the actual producer joins without claiming a new broad pose/OpenCV/libm recovery.

## Numbered behavior/address inventory

Read rows are source-supported extraction, not a manifest promotion. M12-049 owns common cache mechanics; M12-043 owns base geometry joins; M11-037/004 own frame/query; M13-007/023/024 and M12-012/033 own reused geometry. All unspecified libm and pose primitive internals retain their existing records/boundaries.

| # | Behavior established by native instructions | Instruction witness |
|---|---|---|
|1|UpdateAll dispatches type0 stack,1 base,2 pyramid in that order, Robot passed to each. Manager containers+28/+44/+60.|616F06..12 addresses;616F1C..36 selection/call;616F3A..42 increment/loop.|
|2|Each common Update makes default filter, allowed families{2,1}; latest-only=0, originmode=0. No PoseState Known test or carried exclusion in this outer filter.|616832..8A0 zero/default sets;6168A4 zero bytes;6168AE..C6 family insertion/assignment; literalC77784=0200000001000000;6168DE..E8 Robot+34 world query.|
|3|FindLocatedMatchingObjects is collect-all, not closest: copied filter, current Robot origin, family/type/ID trees in order, predicates before collector; no implicit Known-state admission.|6215DE..EE collector and returnFirst0;61EB94 copied filter,61EB98 latest optional;61EBF0..61EC26 current-origin test;61ED58..70 predicates;61ED72..8E collector/returnFirst;61EE12..36 tree successor. Reuse M11-004 for full tree order.|
|4|Only after collecting seeds, copy current strong vector to old, then clear current; keeps old entries alive during rebuilding.|6168EC..F4 vt+8,6168F6..FE vt+10. Address-point tables102E6DC/102E730/102E784: +8 copies62C7D4/62D0EC/62D8E4; +10 clears62C708/62D0BC/62D80C.|
|5|Iterate collected seed pointers in query order. Skip a seed if its numeric ID is already in local processed set; otherwise call concrete AddAllConfigsWithObjectToCache.|6169A4..B4 find(obj+14);616924..32 virtual+14 receives container,Robot,seed;6169B6..BC advance4.|
|6|For EVERY returned configuration, virtual GetAllBlockIDsOrdered and insert every returned ID uniquely into processed. Does not mark a seed that produced nothing; does not globally enumerate all pairs.|61693E..64 virtual+c and insert loop stride8;616990..98 next config stride8.|
|7|Returned config vector holds temporary strong references; destroy it after seed processing. Finally release old cache after all seeds, even for empty seed list, then destroy query/filter/processed temporaries.|61699A..9C vector dtor;616918 empty→6169BE;6169BE..C4 vt+c release-old;6169C6..616A40 cleanup.|
|8|No early survivor keep: current rebuilt from admitted results; removed/nonproduced entries lose manager ownership when old is cleared. External strong snapshots may still keep them alive; weak pointers alone do not.|4–7 plus release loops62C7DE..802/62D0F6..11A/62D8EE..912.|
|9|All three AddAll functions call their own builder, then append EACH resulting entry to current+4 and returned general-config vector. No current-cache uniqueness set.|Base62C580..586,62C5A2..616;Pyramid62CF34..3A,62CF56..CFCA;Stack62D6DE..E6,62D6EC..760.|
|10|Cast scans OLD vector+10 in order; first equal reuses old shared identity, deletes new candidate through virtual deleting dtor and nulls raw candidate. No equal: wraps raw candidate in strong shared_ptr. Reused or new is appended unconditionally.|62C690..6BC/62C6C2..6DA;62D044..D070/D076..8E;62D794..7C0/7C6..7DE. Equality calls62C6A0/62D054/62D7A4.|
|11|Current append and returned vector each retain strong ownership; temporary strong refs released. Old identity reuse persists only across adjacent successful rebuilds when equal remains; no ever-seen registry.|62C5B4..C6 add_shared;62C5D2..60A return retain/move/release;62C60E..612 release temporary; corresponding Pyramid/Stack row9. Old release row7.|
|12|PyramidBase canonical IDs signed-min at+14, signed-max at+c; equal inputs produce(-1,-1). Equality rejects self-invalid(equal fields), compares both numeric IDs. IDs alone, no geometry/time equality.|6174C8..EC signed lt selections;6174F8..7518.|
|13|Pyramid equality compares both embedded base IDs and topID, rejects equal base IDs. Stack equality compares all3 stored ID slots including invalid sentinel. Not list/reference equality.|61817C..1A6;618F58..7E.|
|14|Prune runs AFTER all3 updates, BEFORE last-pose cache and notify. Remove each base matching embedded base of ANY complete pyramid, release strong ref and keep remaining order, recheck shifted slot.|616DA2 update,616DA6..AE prune,616DB2..BE cache/notify (existing check);62C736..762 equality(base,pyramid+8);62C770..7C4 ordered erase/release/retest.|
|15|Stack builder first collects families{2,1} in current origin with predicate IsRestingFlat(Radians bits3E32B8C2). Predicate does not test Known state; flat means resting on ANY face, not only local Z upright.|6190A2 family-table binding,619112..12C allowed set;61913C..170 predicate/query;predicate vtable102DB34→operator6198AA..CA;87751C..55C root matrix,maxabs-parent-Z dot,acosf(abs(dot)),strict Radians<.|
|16|Stack neighbor searches use separate allowed{2,1} filter with no flat predicate. Search UP from seed first, tolerance30, onTop1; stop at null or found raw carriedID (unless sentinelFFFFFFFF). DOWN from original seed next, tolerance30,onTop0. Both append found objects, max10 successful links each; at10 log LoopBoundOverflow/error flag then continue to result selection. No visited-ID cycle rejection in these loops.|6191DA..278 filter;61927C..2DA UP,6192A8..B4 rawcarry compare Robot+284->8;6192DC..334 log/flag;619338..386 DOWN and619388..3E0 overflow;neighbor call61929E/619358→FindObjectOnTopOrUnderneathHelper M13-007.|
|17|Reject carried SEED. Also reject empty initial flat-family result. Admission requires seed in that initial flat list OR, when DOWN nonempty, its final bottom member in that list. No neighbors means no configuration. Thus a non-flat upper seed can still form a stack supported by flat bottom.|61943A..448 rawcarry/seed;61944A..460 initial/result emptiness;619464..472 seed pointer search;619474..4A4 bottom pointer search/combined admission.|
|18|Build ordered objects as reverse(DOWN), seed, UP. Only count>=2 yields a native StackOfCubes. Count2 stores bottom,invalidmiddle,top; count>2 stores FIRST THREE objects, truncating taller traversal. Max observable native height3, not8/10.|6194A8..4DE reverseDOWN;6194E0..4F4 seed;6194F8..524 UP;619526..530 count;619532..56A first3;619570..5A8 count2 fields;618F80..FA2 height0/1/2/3;618FD8..61905A ordered IDs bottom, middle onlyifnotFFFFFFFF, top.|
|19|Base builder independently queries current-origin allowed{2,1}, predicate IsRestingFlat(3E32B8C2), no state test. Needs at least2 results AND seed pointer present; removes seed occurrence once, then each remaining object in query order is tested. This is greedy seed expansion, not global pair enumeration.|618246..264 filter;618272..2B0 predicate/query;vtable102DA6C→operator618B56..76;61830A..35C count/find/remove;61836C..3B4 candidate loop.|
|20|BlocksFormPyramidBase: null argsfalse; root-express both input poses, reject |rootZB-rootZA|>10. Obtain nearest2 interior corners A→B AND B→A; either failsfalse. Obtain each object's GetZRotatedPointAboveObjectCenter(0.5). All four corner-to-opposite-center squared distances must succeed AND be STRICTLY<3600. No center separation lower bound or upright-Z-only requirement.|61752E..580 null/Z;617630..654 ordered corner calls;617656..66A centers;61766E..6A4 four distance calls;6176A6..6EA comparisons;literal6177EC=45610000;6176EE..730 false/cleanup.|
|21|Interior corners reuse actual virtual footprint slot+50 at original object pose,pad0. Insert physical Q3,Q2,Q1,Q0 in that order. Each candidate corner is zero-Z-yaw pose,XY=corner,Z=dimensionZ(A root rotation), parent ACTUAL Robot.GetWorldOrigin. Rank by full3D distance² to B native pose, equal finite keys go right preserving insertion; fail any distance returnsfalse. Flatten first2 ranked poses into outputs, release all locals.|617818..824 footprint;617826..890 reverse corners;617894..8C2 root dimension;6178E6..928 construction/worldorigin;61793A..94C distance/failure;61795E..962 tree;618A66..8C mi-left/else-right;61797C..9BC first2 flatten/assign;617A00 false. Reuse checked footprint hull boundary, not a new convexHull claim.|
|22|Midpoint requires successful corners; flatten both,XY=f32(f32(first+second)*0.5),Z=dimensionZ(A root rotation),yaw0; parent ROOT OF checked Robot.GetPose, not arbitrarily same as world-origin pointer. Assign output only on success.|617B00..B06 failure;617B0A..32 flatten;617B5E..84 dim;617B98..BB8 f32sum/multiply;617BBC..BC8 Robot pose/root;617BEE..BF6 construct/assign;617C18 failure.|
|23|Base candidate alloc24bytes and constructor stores ONLY canonical IDs (row12), no persistent midpoint. Complete-pyramid builder snapshots manager+48 (freshly rebuilt base vector) by strong copy BEFORE branching on seed.IsRestingAtHeight(0,20). If seed is NOT ground-resting, return first matching base with seed as top and stop; seed need not Known here.|61837A..38C base alloc/ctor;61850E..51A snapshot;61851E..52E ground predicate;618606..624 base scan;618626..672 firstmatchallocation then6187B8 cleanup.|
|24|Ground seed branch queries known non-ground tops in current-origin families{2,1}; predicate PoseState==1 AND !IsRestingAtHeight(0,20). For each base containing seed, scan ALL queried tops and append each match; no break/dedup. Ground predicate itself is STRICT |0-(rootZ-dimZ*0.5)|<20, no epsilon.|618530..5F4 filter/predicate;vtable102DAAC→618D0A..26;61867A..682 query;61870A..714 base.Contains;618716..784 all tops;87767C..6EA ground f32 arithmetic/strictmi.|
|25|ObjectIsOnTopOfBase looks up BOTH canonical base IDs with family=-1/current origin; missing top/basefalse. Require top root==first base root and second base root==first. Flatten firstbase/top, dimensionsX/Y/Z from firstbase root rotation, topZ dimension from top root rotation. STRICT abs((topRootZ-topDimZ*.5)-(baseRootZ+abs(baseDimZ)*.5))<15.|617CD8..D0C lookup/null;617D10..D44 same-root;617D48..DB6 root matrices/dimensions;617DBA..E06 f32 height and bpl rejection.|
|26|Then compute BOTH base interior midpoints A→B and B→A; failurefalse. Construct zero-yaw pose(parent=firstbase root,XY=top's LOCAL native pose XY,Z=0). Both IsSameAs(midpoint,tolerance) must pass: Point3f(baseDimX*.5-5,baseDimY*.5-5,100), anglepi bits40490FDB. No radius15 XY test.|617E5C..E7C midpoint calls;617E80..EA8 tolerance;617EAC..ED0 LOCALXY/Z0;617ED2..F04 root/pose;617F1E..46 first IsSameAs;617F4A..76 second;617F7E..FA6 false/release. Reuse checked Pose3d.IsSameAs, not an inserted distance comparison.|
|27|Base GetAllIDs returns signed-min then signed-max; Pyramid returns base IDs then top. Common processed-set membership is numeric IDs, independent of raw object pointer seed eligibility in row19.|618054..9E;618900..940;616950..964.|
|28|Container membership scans CURRENT entries, each ContainsBlock calls virtual ordered-ID list and numeric compare; returns on first true. Does not consult old, firstseen, pose or geometric proximity.|62C6E2..706/62D096..BA/62D7E6..80A;6167C8..7F6.|
|29|Tallest uses CURRENT stack order, first equal maximum retained(strict unsigned greater). Empty current returns an expired weak_ptr, hence existing notify uses height0. No persistence of old tallest after rebuild.|62D844..88C heights/strictbls;62D88E..8A2 weakretain/release;62D8A4..8C0 empty temporary strong→weak→strongrelease;existing notify checked B-M13-15/18.|
|30|Distance helper is checked reusable arithmetic: same-parent vector=f32(first.translation-second.translation); otherwise GetWithRespectTo(first,second), failure leaves output unchanged. Norm=f32(f32(x*x+y*y)+z*z). Failure propagation at producer callsites remains mandatory.|8476B0..6F8 same-parent;847726..756 relative/failure;8477F4..824 vector, x² then y²,z² each f32. Existing PlacementProducerGeometry.TryVector/TryDistanceSquared matches this shape.|

## Ownership and input binding in current C#

`ManipulationSystem` constructs Configurations from the actual Vision.World, supplies `NativeRobot=()=>robot.Engine.Robot`, binds `World.SetObjectPoseChanged` and frame-tail `BlockConfigurationManagerUpdate`. Existing lock order FrameGate→_updateGate, gate/root-cache/reset/notify remain the checked B-M13-15/18 path. No immediate ObjectObserved-triggered recomputation is reintroduced.

The native inputs are the actual current Robot/BlockWorld, ordered current-origin family1/2 query, objects' native poses/states/dimensions and raw CarryingOwner.CarriedObjectId (present-owner null or uintFFFFFFFF maps native sentinel). Existing EngineRobot.GetNativeOrigin(CurrentOriginId), GetCheckedMainPose(), CarryingOwner and World.NativeOrigin/GetNativeOrigin provide the real owners. Missing Robot/provider is NOT native sentinel/identity origin. Host fixture-only roots in Vision/NativeDelocalizationWorld are explicit fixtures, not production proof. Do not bind Docking's construction-time fallback owner instead of current EngineRobot.CarryingOwner (CM13-04 already identifies that distinction).

Native configuration objects own numeric IDs, not retained input object poses. Base has IDs only: C# PyramidBase.InteriorMidpoint is a HOST extra and cannot be used as source cache equality/state. The producer recomputes geometry when building; an equal prior entry can retain its identity even as poses changed, because equality compares IDs. Reuse immutable old configuration instances on equality; temporary pose wrappers/corner nodes must be disposed in scope without disposing shared actual origin/parent owners. Existing PlacementProducerGeometry vector/distance, IsRestingFlat, Footprint.TryGetBoundingQuadXY, pose root/assignment/IsSameAs and rotated-axis helpers are reuse candidates ONLY at the exact rows above; public candidate BlocksFormPyramidBase and ObjectIsOnTopOfBase are contradicted joins, not reusable source predicates.

Current `NativePyramidBaseStrongCache` is unbound and CopyNativePyramidBaseCache visibly stops. The native source for it is the POST-prune base current vector manager+48, with retained references/order/duplicates, not pre-prune bases or host all-pairs. Publishing this vector does not recover BuildPyramid callback slots; retain their MISSING. Source snapshot semantics are already checked in B-M12-6.

Current BuildTallestStackForObject uses down-first, bound8, visited-ID rejection and an arbitrary-length list. Native is UP-first, then DOWN,10-link diagnostic bounds, carried exclusion/admission and stored2/3 slots. Current helper FindObjectOnTopOrUnderneath queries supplied host cube lists; production row16 needs native current-origin family query and exact root/quad/height helper (M13-007). No new pose heuristic may stand in for this.

## Build boundary, remaining UNKNOWNs and useful checks

Smallest complete cache producer is rows1–30 for all3 containers, including actual query/predicate and geometry joins, old identity and post-prune publication. Merely replacing BuildLocked's outer loops while invoking current heuristic predicates would NOT close this unit. An independent cache-mechanics slice (rows4–14,27–29) can be built/tested against explicit source-shaped candidate fixtures, but must remain visibly unbound in production until real builders exist; that slice alone cannot clear the live producer MISSING.

The available primary source settles the finite normal producer logic above. Existing M13-023/024 footprint hull branches still return visible MISSING where unsupported; no new hull, libm or LSDA recovery is claimed here. Builders must propagate unavailable geometry distinctly from native false, without publishing a stronger partially fabricated cache or clearing the diagnostic. The source does not authorize a radius/axis/cube-only fallback. Exact policy for an unsupported admitted rebuild's host availability response remains a manager-visible implementation boundary; it cannot be presented as native false or successful full rebuild. This read finds no new approved-contract contradiction or need to invent an operator exclusion.

Host `ConfigurationSeen` and `_clockSec/FirstSeenSec` have no equivalent state or emitter in these native container bodies. Preserve/separate their existing adapter behavior without claiming native game-message semantics. Reordering configuration results, changing identity/canonical ID ordering, truncation or disappearance can affect that public adapter; its expected supported API contract is UNKNOWN here, not 'no effect'. No event exclusion or guessed payload is authorized. The checked native public-state notification remains driven by post-producer tallest and is distinct from these host first-seen events.

Focused independent observations for reviewer/build scope (no tests run):

- Actual runtime/frame-tail force rebuild, current-origin family1/2 objects, different insertion order; another origin and charger must not enter configs; outer Known-only filter would be wrong. Ground pyramid top predicate DOES require Known.
- A-B and A-C seed pairs mark B,C processed before later seeds: suppress a B-C edge that all-pairs would invent. Empty builder leaves seed unmarked. Do not add a current-cache dedup set.
- Consecutive equal IDs reuse exact object identity despite changed geometry; changed IDs/disappearance/reappearance use new identity; external strong snapshot stays alive. Removal affects membership and tallest/notify after old release/prune.
- Three versus four physical stack members must expose stored native3 ceiling. Native two-member representation omits middle sentinel from ordered IDs. Carried top stops UP; carried seed returns none. Exercise non-flat seed with flat bottom and tie-order tallest.
- Base geometry rotated/side-resting cubes, root-vs-local translations, strict3600 and height10 branches; old center-lower-bound predicate is a negative control. Corner equal-distance tie must retain Q3,Q2,Q1,Q0 insertion order.
- Pyramid top height exactly15 rejects; XY follows BOTH directional midpoint IsSameAs boxes, not radius15. Non-ground seed returns first match; ground seed loops all known non-ground tops. Prune removes base only after full pyramid production.
- Cached timestamp gate/force/pending reset and A-before-B audio notifications remain prior regressions, not new semantics; source container identity is not host first-seen time.

Obsolete/duplicate requests: full Container.Update/all-cache-body reading is now supplied by this bounded inventory, pending DIFFERENT primary checker PASS. Root-cache/gate/notify rework and historical release warnings are duplicates of B-M13-15/18. BuildPyramid callback bodies remain separate M12-049 work; broad helper lifecycle M12-042, generic retained pose audit, full OpenCV hull and action producers are not part of this read.

No manifest impact applied, no tests run, no commit. Report ready for independent primary-source PASS/FAIL before any code. Stop.

## Explicit unavailable-input/geometry boundary (manager clarification)

The exact present port guard is `MinAreaRectF.Compute`, BlockConfigurations.cs:686: after hull count>2, scan double convexities over f32 edge values and **if orient==0 throw NotSupportedException**. Footprint.TryGetBoundingQuadXY catches it at523..525, emits M12-043/M13-024 MISSING and returnsfalse. It also returnsfalse at517 for a non-cube type. **Neither false means native no-configuration.** Native row21 calls virtual footprint+50 and expects a quad; it has no managed 'unported geometry' false branch. Row20's actual sourcefalse occurs later on failed pose-distance/corner computation or failed numeric comparisons, and cannot be conflated with the port guard.

Current quoted **M13-024**, 'The exact footprint: cv::minAreaRect, convexHull, Sklansky_ and RotatedRect::points', **IMPLEMENTATION_GAP**, cites004E6490..6570/catch65C6..67C4, OpenCV9C760..CEAC,39174/Sklansky and evidence/m13-minarearect/port.py and emulator corpus. Its current unresolved explicitly retains degenerate hull/sort-tie/full-catch boundaries; B-M13-11 supersedes only n<=2. Thus zero/one/two-point helpers are already recovered/reusable; they do NOT remove this separate >2/zero-convexity guard. This read has not proved the >2/zero-convexity condition unreachable for every admitted finite/degenerate family1/2 input, nor established native result for that exact guard. Useful next recovery, if needed, is only the existing calipers orientation-zero path against the checked OpenCV9C8D8..CDC4/evidence corpus, not a full new geometry audit. Full catch/error integration and non-Block virtual footprints keep their own records; a managed port-gap exception is not evidence that native OpenCV throws.

If any required footprint, raw carrying owner, current world-origin owner or checked main-pose input is unavailable, propagate a distinct **UNKNOWN/unavailable** through the producer. Proposed host guard/staging boundary: do not publish newly staged native caches or emit a successful native-update claim from that attempt; preserve prior published caches with visible availability failure. This is **host guard: native N/A**, not a recovered engine branch, feature exclusion, or proof stale caches equal a source rebuild. Likewise a skipped candidate/empty cache with only a diagnostic would claim unsupported absence. Pending/force consumption, old-cache/publication and notify on an unavailable attempt need that availability boundary to remain visible rather than being advertised as a completed native rebuild. Do not invent an engine fallback, false hit/miss, sentinel, or identity root. Manager must keep the live-path acceptance limitation explicit until all admitted inputs can be evaluated faithfully.

## Independent primary clarification for rows12/16/25/27 (2026-10-26)

Re-ran known.py before personally re-reading61749C,617CC8,618054,62601C and62B9FC. Raw instruction extract is `.scratch/cm13-05/clarification.txt`. These clarify operand identity; they do not add geometric left/right names or broaden the job.

- **Row25 first base means signed-MAX ID.** Constructor6174C8..D6 selects signedMIN into value+14;6174D8..E4 selects signedMAX into value+C (equal inputs reset both toFFFFFFFF at6174E6..EC). The first embedded ObjectID is base+8, whose value is+C. ObjectIsOnTopOfBase617CDA passes **base+8** to lookup617CE4, saving result inr4 at617CE8. Its second lookup617CEC/617CF4 passes **base+10**, value+14=signedMIN, saving resultsl. Therefore the dimension/root/height/tolerance source object in rows25–26 is **signedMAX**, and second midpoint swaps to signedMIN first. Public LeftBlockId/RightBlockId names cannot establish this operand mapping.
- **Row27 ordered-ID enumeration is deliberately the opposite:**618062 passes base+10 first (MIN), then618084 appends value+C (MAX), or slow path618094 passes base+8. Thus GetAllBlockIDsOrdered is MIN,MAX while ObjectIsOnTop's first geometry operand is MAX. Keep both facts; do not equate enumeration with storage/lookup order.
- **Row16 neighbor query uses FIRST passing match.**62610C explicitly setsr3=1 before FindLocatedObjectHelper62610E. The common collect-all producer in row3 setsr3=0 at6215EC; these are different contracts. No last-match interpretation applies to the stack neighbor query.
- **Reference is flattened:**62603E gets reference pose,626048 calls checked004DF628 **GetWithRespectToRoot**, then626056/62605C passes that flattened reference pose to virtual footprint+50 withpad0. Its root rotation determines reference dimensionZ at626060..70; targetZ=f32(referenceRootZ+f32(dimZ*(onTop?+.5:-.5))) at62607E..A0. The older20260929 'copy/own parent' interpretation is stale; current M13-007 unresolved already records the OV100 root correction.
- The helper copies the supplied family filter at6260A4..A8 and inserts referenceID(obj+14) into its ignore-ID set at6260AC..BC. Candidate predicate62BA24..3A obtains its native pose expressed **with respect to flattened reference pose's parent**; transform failure returnsfalse62BA46/48. Save candidate XYZ62BA50..64; overwrite onlyZ with referenceRootZ at62BA66..78; compute candidate footprint+50 withpad0 at62BA7A..86 and Intersects at62BA88..8C. Restore actual candidateXYZ62BA92..9E BEFORE dimension/height comparison62BAA4..BB0E. Height test is inclusive abs(targetZ-(candidateZ+candidateDimZ*(onTop?-.5:+.5)))<=f32(tolerance+epsilon), epsilon checked bits3727C5AC. An unavailable footprint is distinct UNKNOWN, not this native predicatefalse and not PlanarStandIn. The existing C# helper's first-return alone does not certify its root/pose/filter/geometry joins.
- **Initial native-cache availability:** this read does not claim C# default-empty containers prove native constructor state. Either add a checked constructor-empty witness as separate bounded prerequisite, or keep native producer/cache availability unavailable until first successful source-backed rebuild. This clarification adopts neither an invented empty cache nor a broad lifecycle recovery.

This clarification is report-only. Independent checker must match this wording in the final build inventory. No production/tests/git/manifest changes.

## Constructor-empty cache witness and PyramidBase layout (2026-10-26)

Own known.py preflights and primary constructor reread are saved in `.scratch/cm13-05/known-constructors.txt`, `constructors.txt` and `manager-constructor.txt`. This resolves the earlier conditional initial-cache availability note **for a real constructed native manager/container owner**.

Manager ctor **00616B58** calls Stack container ctor at616B7E/616B82 withmanager+28, Base ctor at616B86/616B8A withmanager+44, and Pyramid ctor at616B8E/616B92 withmanager+60. Each concrete ctor first initializes the base vtable, installs its own address-point vtable, then clears exactly24bytes beginning atcontainer+4:

| Container | Primary zero-init witness |
|---|---|
| Stack0062D5FC |62D610 `str r1,[r0],#4` advancesr0 fromcontainer to+4;62D614 `movs r1,#0x18`;62D616 calls `__aeabi_memclr4`|
| Base0062C4D0 |62C4E4 advancesr0 to+4;62C4E8 length18hex;62C4EA memclr4|
| Pyramid0062CE84 |62CE98 advancesr0 to+4;62CE9C length18hex;62CE9E memclr4|

The six zero words are **current-vector begin/end/cap at+4/+8/+C and old-vector begin/end/cap at+10/+14/+18**. These are the vectors read by the already checked copy/clear/cast paths. Therefore all three native current/old strong vectors start genuinely empty; no first successful rebuild is needed to justify that initial native state. In manager coordinates the initial current vectors are+2C,+48,+64. Initial base strong-cache snapshot can safely be empty when bound to this constructed owner. Missing host Robot/container owner still cannot masquerade as that constructed source state.

Lifetime bound: empty vectors contain no elements/shared references to release. The checked update retains current entries into old before clearing current, takes/returns real strong references for created/reused entries, releases temporary returned vectors, then releases old after rebuilding (rows4–11). Prune releases only removed current base refs. These facts support the scoped empty→rebuild ownership lifecycle; this is not a new whole-Robot destruction audit or authorization to dispose retained external references.

**PyramidBase has no cached midpoint field in the native object.** Producer61837A allocates exactly0x18bytes and invokes61749C. The constructor writesvptr+0,type1+4,ObjectIDvptr+8/valueMAX+C,ObjectIDvptr+10/valueMIN+14, filling that24byte object;6174AE/BC initialize invalid values before canonical assignment6174C8..EC. No Pose3d, XYZ or midpoint storage exists in those slots. GetBaseInteriorMidpoint617A8C computes into a caller-supplied output, and ObjectIsOnTopOfBase617E5C..76 recomputes both directions from live looked-up objects. C# `PyramidBase.InteriorMidpoint` remains a host adapter extra, not native persistent cache state, identity or a substitute for these live geometry calls.

Report-only clarification; no source/tests/manifest/git changes. Independent checker must confirm these added constructor claims before adoption.
