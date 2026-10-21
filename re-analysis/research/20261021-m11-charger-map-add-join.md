# R-M11-CHARGER-MAP — supported map-add prerequisites

Request: `requests/20261021-m11-charger-map-add-join.md`. Examined HEAD19b0a313; independent read-only production comparison, pending checking. Coverage opened before narrow boundary checks.

| Step | Checked source / native boundary | Production binding | Disposition |
| --- | --- | --- | --- |
| Observation to map add | B-M13-9; OV42 O1–O8; OV42 check double Add | BlockWorld2380..84→Maps.UpdateObjectPose | Real join present; valid/valid cache remains missing |
| Relative pose | 67ED94..A2; shared GetWithRespectTo845A34 | MapTiming.ObjectPoseRelativeToRobot unbound | Shared primitive recovered; needs actual Robot pose/tree owner |
| Height | 67EDC0;877954..C4;557794; checked OV05 axis | CubeGeometry.IsPoseTooHigh bound to column-based dimension helper | Wrapper arithmetic present; row/column mismatch, reusable row helper |
| Polygon / insert | 67EEC4..F024; checked M11-088 and R-QUADTREE | ObservableMapSlots dispatch built; SourceQuadTree recipients unbound | Recovered dependencies, no invented charger-only insertion |
| Cache false/true | 67EDC8..EE30 /67F024..F082 | No per-object/per-origin cache owner | Recovered distinct writes and root semantics |
| Generic tree/edge consequence | OV42 O8; P1/OV11 checks | Legacy regions/FillBorder are candidates | Binding gaps; “FastPolygon unread” messages stale |
| Exact remaining unread callee | Checked rows cover named admitted charger route | Full production closure not established | No new unread callee identified; residual alias/callback effects UNKNOWN, not invented blocker |

## Evidence and scope

Primary evidence: actual `resources/lib/armeabi-v7a/libcozmoEngine.so` instructions, with decomp for navigation only. Reused manager-adopted `overnight/20261015-ov42-44-check.md` O1–O8/caller correction, requested `overnight/20261016-batch-check.md` M11-088 slots/robot-relative input, `20261013-quadtree-rest-check.md`, and OV05/OV11 checks. No whole memory-map or pose arithmetic extraction.

Fresh narrow checks clarify producer/recipient identity and cache contents:67ED7E..EE32,67EEC4..EF20,67EF92..F0C0, IsPoseTooHigh877954..C4, GetDimInParentFrame557794..7D4 and its exact indexed row access. GetRotatedParentAxis5507A0..854 was checked against existing adopted OV05 B2, not a new independent algorithm recommendation.

## Actual live path

B-M13-9 already supplies the every-contact-true charger observation and calls normal ObjectPoseConfirmer/world notification; do not duplicate that observation in the map layer. Current `Vision/BlockWorld.cs`:2378..84 calls configuration callback, Maps.UpdateObjectPose(obj,oldPose,oldState), then BroadcastObjectPoseStateChanged. Map.SourceLog is supplied at2381. The native checked order506F98..FB8/506296..2D6 is world callback → UpdateObjectPose(old) → BroadcastObjectPoseStateChanged → UpdateObjectPose(current). **An Invalid→valid notification runs Add twice** under the checked caller correction. Do not deduplicate by object ID before reproducing the cache/second-call behavior.

Current `Vision/MapTiming.cs` handles family6 skip and three Invalid branches, but stops the valid/valid path at MISSING. Invalid→valid calls optional AddObservableObject67ECFC, otherwise AddObjectCheckedHeight. Both the robot-relative delegate and post-height continuation are unbound in production (assignment search found only test assignments). Thus a real charger observation reaches an actual, visibly incomplete recipient.

## Checked add contract and available owners

| Step | Exact contract / citation | Existing reusable owner / unresolved binding |
| --- | --- | --- |
| Admission family | OV42 O5/D1,67ECFC onward; family4 maps to **content type4**, not type3 | Charger family identity already source-backed; generic family mapping still required for all supported add inputs |
| Origin/map selection | Resolve supplied pose root ID, use existing map for that origin; missing map returns before relative pose/height/cache | SourceMapComponent.Maps exists; shared Pose.FindRoot/GetRootID semantics must use existing M11-065/066/068 recipients, no current-origin fallback |
| Robot pose | 67ED94 loads MapComponent's Robot, calls4EA398, then source pose r6.GetWithRespectTo(robotPose,out) at67EDA2 | Bind actual engine Robot pose (MainPose/current pose owner), not History.Latest planar host facade |
| Relative-pose failure | result!=1 at67EDA6..A8→67EE86 warns InvalidPose, returns without inserting/committing pose cache | Pose3d.GetWithRespectTo in Vision/Geometry.cs:346 and TryWithRespectTo388 already preserve native tree/failed-query behavior; delegate should return failure, not pose-at-root fallback |
| Height | obj.IsPoseTooHigh(relativePose,1.0f,15.0f,0.0f) at67EDAA..C0; exactly1 takes high path | CubeGeometry.IsPoseTooHigh arithmetic is f32 and takes passed relative pose, but its dimension selector needs correction below |
| High branch |67EDC8 copies **original supplied pose r6**, added=false;67EDFA/EE18 create/find per-ID/origin entry;67EE24 assigns pose,67EE2C writes flag | No C# native PoseInMapInfo cache counterpart; need generic shared cache, not charger acceptance bit |
| Admitted geometry |67EEC4..C8 GetWithRespectToRoot(supplied pose);67EECC..DA calls object's actual virtual+50 with **root-relative pose** and raw padding0 | ObservableMapSlots.TryObservableBoundingQuad50 already routes Charger to base physical-corner footprint and Cube to distinct Block thunk; share existing M13-024 minAreaRect/quad units |
| Content | Type3 gets MemoryMapData_ObservableObject/ObjectID/polygon; type4 charger takes **base MemoryMapData**, both timestamps fresh Robot.GetLastImageTimeStamp | No fabricated ObjectID/polygon subtype on charger base data. Non-type3 branch warns AddedNonObservableType but continues67EF92..F002 |
| Polygon import/insert |67EFF2 ImportQuad2d50096C then existing map virtual+8 at67F002 (type3 analogous67EF20) | Existing PolygonF.ImportQuad2d and checked FastPolygon/tree source; do not call legacy MemoryMap.Insert region append as exact tree insertion |
| Added cache |67F024 copies **root-relative pose sp+7C**, sets true67F036, creates/finds ID/origin entry, assign67F07A then flag67F082 | Different stored pose from high branch; preserve distinction even when coordinates happen to match |
| Post-insert edges |67F09A scales a temporary quad but actual67F0BC map+4C receives type9/mask{3,4,5,6,7,10}/type10/last image timestamp, no quad argument | Existing checked OV11 FillBorder owner; global tree/cache invalidation path, not legacy polygon-touching approximation |

4EA398 is the existing Robot::GetPose entry/wrapper, not a timestamp-history query. The supported requested source pose is the already owned charger.NativePose. C# ManipulationSystem.RobotPose155 instead returns Vision.History.Latest.RobotPose; that convenience facade is not the native input merely because both describe a robot pose.

Admission is generic even for a real charger. No map for the pose's origin is a defined rejection; it is not permission to create a new map automatically. Robot-relative query failure is a defined failure. Height rejection **records a false cache entry**; it is not the same as either early rejection.

## Height dependency: recovered helper, implementation mismatch

The called877954..C4 reads the **passed pose** transform/matrix, calls GetDimInParentFrame<'Z'>557794 at87797A, then compares in f32:
`F(F(F(D*1)+15)+epsilon) < F(F(D*0)+relativeZ)`,
epsilon raw3727C5AC; ordered MI branch, NaN does not returntrue.

Native557794..7D4 calls GetRotatedParentAxis<'Z'>5507A0 and selects size.z for±3, size.y for±2, size.x otherwise. Adopted OV05 B2 explicitly says the helper reads **M20,M21,M22**, strict ordered greater comparisons, ties/NaN keep earlier axis. Fresh5507B2..B8 passes row2 and columns0..2 to SmallMatrix::operator()557890;55789A..A0 computes (row*3+column)*4. This is not projection by summing extents, nor selecting the matrix column.

Current `Vision/CubeGeometry.cs`:173..181 DimInParentFrameZ reads **r[0,2],r[1,2],r[2,2]**, with >= choosing Z first on ties.197 calls that helper from IsPoseTooHigh. This differs from the native row/tie rule. Equal-sized44mm cubes hide the difference; charger dimensions differ. `Manipulation/DockActions.cs`:206 RotatedParentAxis.Get already implements the checked row/strict-greater ordering and size selection. Its convenience DimInParentFrame overload uses **o.Pose**, so blindly calling that overload would lose the passed **robot-relative pose**. Reuse row selection with the passed matrix and actual object's SizeOf/vtable-size recipient.

Current CubeGeometry.SizeOf routes charger to ChargerGeometry.Size rather than44mm cube size; preserve that supported input. This is a small source-backed shared-helper correction, not new pose arithmetic or permission to use a Z>15-only gate. Exact f32 matrix conversion/pose owner still belongs to existing pose records; no independent “charger always low enough” constant is justified.

## Cache outcomes / later valid→valid

Reuse checked OV42 O1–O4 and O8; no new extraction of that state machine:
- Outer cache keyed by ObjectID, inner by pose root/origin. Family6 is ignored before state transitions.
- Both states valid: missing origin cache entry adds; existing added=true compares current pose with cached pose using three5mm thresholds and Radians3E32B8C2 (~10degrees). Nonzero IsSameAs skips changes.
- added=false skips that comparison and retries: concrete object's slot+5C==1 invokes Remove before Add; other results skip Remove then Add. Checked ActiveCube/Charger +5C returns1.
- Height false cache can therefore be retried even with unchanged current pose. A cached added=true can suppress re-add until native pose comparison changes. Do not replace false with absent entry or treat “same timestamp” as an automatic skip.
- Generic removal transforms only existing type3 cells with matching ObjectID. A charger's **type4 base cells do not match** even though family mapping computes removal type5. This checked quirk must not be “fixed” by clearing all charger footprints.
- Invalid→valid Add twice can create or replace a cache entry on each add. Keep that checked duplicate admission sequence distinct from later notifications whose previous state is already valid; those take the valid/valid cache branch. All admission outcomes need this actual caller join.

The admitted insert call is followed unconditionally by added=true cache update; caller does not inspect a tree “changed” or acceptance result. A native Insert that returns without changing contents (for example defined invalid-polygon rejection) does **not** mean height-added=false or absent cache. Conversely an unbuilt C# tree recipient cannot be labelled successful: retain visible MISSING until the real checked recipient is bound, rather than synthesizing a successful side effect.

## Which callee is still unknown?

**No new unread behavior-changing callee was identified in the requested charger admission/polygon path.** The remaining production deficiencies above are chiefly implementation and owner bindings. In particular:
- Current SourceQuadTree.Insert187..208 reports FastPolygon ctor/Contains/GetOverlapType “UNKNOWN”. That is a stale source-coverage claim. Adopted `20261013-quadtree-rest-check.md` says items1–6 confirmed and the quad-tree subset of M11-048 fully read: FastPolygon8416F0..841C70/50238A..23D6; overlap688290..8836E; expansion, ForceSet, ShiftRoot, callbacks and cached types are listed under M11-045/046/M13-032.
- Native “6817D2” in a Missing message is a call-site boundary, not evidence of an unread separate FastPolygon algorithm. The named constructor symbol is8416F0. Reuse checked source and existing FastPolygonF where validated.
- GetRotatedParentAxis and the concrete charger slots were also already recovered; “unknown helper” would misclassify the height mismatch.
- The map producer parent including cache and fill was checked in OV42; no need to reread the whole parent or whole tree.

**UNKNOWN:** whether every relevant SourceQuadTree cached-type/border callback has its concrete storage/consumer binding in the current production object graph; exact full-production subscriber consequences of those callbacks; complete residual pose/footprint numerical equivalence outside these checked input contracts. Those are limits on claiming full closure, not a newly discovered unread callee at67EDA2/EDC0/EDC8/EEC4. SourceQuadTree.OverlapForTest/CentroidForTest are currently unbound production seams; tests supplying them do not establish native production ownership.

A narrower next task can integrate checked tree geometry/callback/cache owners (M11-045/046/047), then bind the shared object cache/add path (M11-074). If a specific checked row proves insufficient during that work, report the exact next callee then; do not invent a fresh repository-wide recovery dependency.

## Production comparison / bounded recommendation

- `Vision/MapTiming.cs`:65..75 AddObjectCheckedHeight currently starts at relative input and lacks family/root/map pre-admission in that fallback. Bind the **whole generic Add entry contract**, not just a relative transform delegate that bypasses missing-origin rejection.
- `Vision/SourceQuadTree.cs`:245 SourceMapComponent has Maps/current origin and localization-map owner. Existing origin/missing-map behavior can be reused; no charger singleton map.
- `Vision/ObservableMapSlots.cs`:TryObservableBoundingQuad50 uses actual explicit pose and existing Charger/Block footprint paths. Its visible NotSupportedException/MISSING remains a numerical/recipient limitation, not an acceptance shortcut.
- `Vision/MemoryMap.cs`:122 public Insert appends to _regions; FillBorder405 uses polygon touching. Neither is the checked tree writer/fill. InsertCliff111 is the only shown direct SourceQuadTree.Insert join, and that stops at unbound OverlapForTest/centroid recipients. Existing FastPolygonF/PolygonF helpers live under Manipulation/LatticePlanner.cs; duplicate geometry is unnecessary.
- `Vision/MapTiming.cs`:valid/valid path and AddObjectAfterHeight currently report missing cache/map continuation. No production assignments were found for ObjectPoseRelativeToRobot, AddObjectAfterHeight or AddObservableObject67ECFC; tests alone set them.

After manager checking: bind actual robot pose/shared GetWithRespectTo, correct height dimension using existing row helper, reuse actual supplied/root poses for false/true cache, then typed map insertion/fill through checked tree owners. Supply real per-ID/per-origin cache to both first add and subsequent valid/valid path. No charger-specific acceptance, removal repair or legacy-region success substitute.

Independent regression expectations, not tests added or run:
1. Actual B-M13-9 observation reaches map recipient. Missing-origin and relative-query failure leave cache untouched; high pose writes false; admitted insert writes true/root pose then fill.
2. Non-cubic charger matrix with distinct row/column dominant axes exercises shared height rule. Include ties and native f32 boundaries; equal cube sizes are insufficient.
3. Height unit currently `ObservableWalkAndInterestingEdgeTests.cs`:94..100 substitutes relative pose and continuation, cube44 atZ59/60. It checks only height forwarding/threshold, not source input, dimension axis, cache or insertion.
4. Real cached false entry retries unchanged valid pose; true entry suppresses within tolerance. Reproduce native double Add callback ordering and no special timestamp deduplication.
5. Charger content is base type4 with both image stamps, no ObjectID-carrying subtype; Remove does not turn its type4 cells into type5 merely by geometry overlap.
6. Import/Insert reference verifies checked quad order, actual root-origin map, content override/callbacks, and global9→10 fill; do not assert a region-list footprint as a tree oracle.
7. Cache true after a defined native Insert no-change return differs from admission rejection and false height cache; record each separately.

Research-only, no production/tests/inventory/manifest/approval/hardware changes. No whole M11-074/088 settlement; current source status and remaining uncertainty quoted below.

## Current manifest snapshots

Exact current title/status/evidence fields; unchanged statuses.

### M12-012

```json
{
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

### M11-045

```json
{
  "title": "The memory map's quad tree: Insert, Expand, override rules, subdivision and merging",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00685008 QuadTree::Insert; 0x00684C28..0x00684C48 root; 0x00687E78..0x00687EEE CanOverrideSelfWithContent; 0x00687F56..0x0068805C TryAutoMerge (child 3 seed; the extractor's 'three children' corrected by the verifier); 0x006878D2..0x0068791E quadrant map",
    "re-analysis/research/20260929-R-VIS-M11-gap2-extraction.md; re-analysis/research/20260929-R-VIS-verify-M11-gap2.md (rows 1.2-1.24 PASS, row 1.22 corrected)"
  ]
}
```

### M11-048

```json
{
  "title": "Unread quad-tree, OpenCV-numeric and vision-handler bodies",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "re-analysis/research/20260929-R-VIS-M11-gap2-extraction.md 'not done'; re-analysis/research/20260929-R-VIS-verify-M11-gap2.md 'Open'; re-analysis/research/20260929-R-VIS-verify-M11-gap1.md 'Still open'"
  ]
}
```

### M11-074

```json
{
  "title": "Memory-map producers and readers not yet read: MapComponent::UpdateRobotPose (explored area), UpdateObjectPose, RemoveObservableObject, FlagInterestingEdgesAsUseless, ReviewInterestingEdges, ClearRobotToMarkers, FindContentIf/FindIf",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-quadtree-rest-check.md: UpdateRobotPose 0x0067E224..0x0067E50B; UpdateObjectPose 0x0067EA90..0x0067ECFB; RemoveObservableObject 0x0067F2C4..0x0067F58B; FlagInterestingEdgesAsUseless 0x0067E778..0x0067E85B; ReviewInterestingEdges/ClearRobotToMarkers 0x0067F58C..0x0067F7AB; FindContentIf 0x00681674..0x006816E7 and QuadTreeNode::FindIf 0x006884FC..",
    "re-analysis/research/overnight/20261015-ov42-44-check.md: R1-R6 0x0067E23A..0x0067E486 (cliff byte writer 0x005356C6..0x005356D4); O1-O8 0x0067EA9A..0x0067F0BC; caller order 0x00506F98..0x00506FB8, 0x00506296..0x005062D6 (double Add); D1-D3 0x0067F4C4..0x0067F544, 0x0067F2DE..0x0067F368, 0x006810E0..0x00681172; C1-C5 0x0067F5B8..0x0067F714, caller 0x00506A86..0x00506A98; Q1-Q6 0x00681674..0x006816DC, 0x006884FC..0x006886F4."
  ]
}
```

### M11-088

```json
{
  "title": "ObservableObject virtual +0x5C / +0x50 overrides per concrete class (map Add/Remove and quad)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov42-44-check.md: base slot 0x004E02AE returns 0; ActiveObject-family slot 0x004E3882 returns 1; CustomObject 0x004F04FA returns byte +0x9C; callers 0x0067EB38..0x0067EC2E (+0x5C), 0x0067ED06..0x0067F0BC (+0x50).",
    "re-analysis/research/overnight/20261016-batch-check.md: slots by relocation: Block quad thunk 0x004E690D, base quad 0x0087713B, markerless 0x00502C33; +0x5C 0x004E3883 (1), 0x004E02AF (0), 0x004F04FB (+0x9C); Block quad body 0x004E62A2..0x004E6418; base quad 0x0087713A..0x008772D2; UpdateObjectPose 0x0067EA9A..0x0067EC2E (invalid->invalid error 0x0067EAF6, invalid->valid Add 0x0067EACA, valid->invalid Remove 0x0067EC3C..0x0067EC4C); AddObservableObject family table 0x0067F4CC, IsPoseTooHigh(1.0, 15.0, 0.0) on the robot-relative pose 0x0067EDA2..0x0067EDC0."
  ]
}
```
