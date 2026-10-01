# Q12 — R-VIS round-2 pre-extraction

Date: 2026-10-01  
Binary set: shipped ARMv7 `libcozmoEngine.so` and shipped OpenCV 3.1.0 libraries.  
Scope: R-VIS “Left for the next round”, excluding the records already answered row-by-row by Q4 (`20260930-recoverable-gaps.md`): M11-047, M11-048, M11-052, M11-042, M12-024/027/028/032/034/038/039 and M13-021/023. Those Q4 rows remain the extraction source and are not duplicated here.

## Result and dependency order

The buildable order is M11-045 → M11-046 → M14-007/M11-017, then M11-050/051/053; M13-024 → M13-025, while M13-026 and M13-027 can be built independently except for their explicitly named RECOVERABLE_GAP dependencies. M11-038 additionally needs M2 wire types and M11-040's result-deque producer.

Every item below remains an `IMPLEMENTATION_GAP` at HEAD. Tests which call the new helper directly can validate isolated arithmetic but cannot settle the record until the native production caller, failure result and ordering are live.

## M11-045 — exact memory-map quad tree

> Current manifest: `IMPLEMENTATION_GAP`, “build the quad tree exactly (Insert, Expand, override table, Subdivide/merge, timestamps, processor bookkeeping). ShiftRoot ... and LineSegment::IntersectsWith are M11-048.”

| step | address | source behavior: gates, order, failure and constants | C# entry |
|---|---|---|---|
| Entry/NaN | `0x006817d2..0x006817e0`; `0x00685008..0x006850ba` | `MemoryMap::Insert` makes `FastPolygon`, calls `QuadTree::Insert(..., maxShifts=1)`. Check every x/y with `isnanf` first; any NaN logs `QuadTree.Insert.NaNPoly`, optional debug break/dump, returns without mutation. Empty polygon passes. | `Vision/MemoryMap.cs`: replace polygon-list storage with the tree; retain visible error outcome. |
| Root/precision | `0x00684c28..0x00684c48`; `0x00685000` | Root centre `(0,0,1)`, side 160.0f=`0x43200000`, level 4; level 0 is 10.0f=`0x41200000`, maximum grown level 8/side 2560. | Exact binary32 fields, not doubles. |
| Containment/expand | `0x00686a84..0x00686af0`; `0x00685228..0x00685412` | AABB tolerance is ±1e-5f=`0x3727C5AC` (negative `0xB727C5AC`). Removal outside root warns and does not expand; other content grows toward centroid, then attempts at most one ShiftRoot; insufficient growth warns, but Node::Insert still runs. | ShiftRoot is Q4/M11-048; until built, outside-root production remains visibly incomplete. |
| Overlap | `0x00688290..0x00688376` | Return disjoint/partial/node-inside-poly/poly-inside-node in that order. Point tests precede edge-vs-diagonal tests; fewer than 2 points cannot edge-hit. Endpoint bounds are inclusive at 1e-5. | Depends on Q4's unread `LineSegment::IntersectsWith` for touching cases. |
| Recursive insert | `0x00686d02..0x00686dca` | Same content pointer returns unchanged. On any overlap, overwrite existing shared content stamp `+0xC` **before** override decision. Whole-node override may clear descendants; partial leaves subdivide to level 0; recurse Q0→Q3, then auto-merge. | Preserve shared-content/stamp aliasing and order. |
| Override table | `0x00687e78..0x00687eee` | Cliff(8) always overrides. Existing Cliff only yields to total ClearOfCliff(2). NotInteresting(10) only overrides Interesting(9). Interesting(9) overrides 0,1,2,5,6,9. ClearObstacle(1) overrides 0,1,5,6 partially and additionally 3,4,7,9,10 only on total overlap; never type 2. ChargerRemoved(5) only overrides Charger(4). Other new types override everything except Cliff. | Encode as a fixed 11×11/overlap rule, with source-fixed exhaustive tests. |
| Subdivide/merge | `0x00687c14..0x00687dae`; `0x00687f2a..0x0068809c` | Children centres are ±side/4, side/2, level-1. Copy parent content to children, reset parent Unknown. Merge only four equal leaf children; seed parent from child 0; stamp min/max loop covers children **0..2 only**, omitting child 3 (verified correction). | Tests must include unequal child-3 timestamps. |
| Processor bookkeeping | `0x00688e50..0x0068934c` | Update explored/interesting area as doubles from `(side*0.001)^2`, maintain cached node sets for types 3,6,7,8,9,10, invalidate every border cache on type change/destruction. Destroy subtracts only leaf contributions. | Whole production owner includes these counters/caches. |

No existing test owns this path: polygon-ray tests exercise the current exact-polygon stand-in and therefore cannot establish 10 mm quantisation, override, stamp or merge behavior.

## M11-046 — polygon Transform and collision-ray walk

> Current manifest: `IMPLEMENTATION_GAP`, “build Transform and the ray walk exactly. LineSegment::IntersectsWith ... is M11-048.”

| step | address | source behavior | C# entry |
|---|---|---|---|
| Transform entry | `0x0068147c..0x00681522` | Copy function, make FastPolygon, call root `QuadTreeNode::Transform`. | `MemoryMap.TransformContent`. |
| Transform walk | `0x00688378..0x00688476` | Disjoint returns false. With children: visit all four, ignore child return, auto-merge. Call function on node content; only a changed **leaf** is force-set. Any overlap transforms the whole leaf: no subdivision/containment gate. | ROI type-9→0 therefore clears the whole touched 10 mm leaf, including area outside ROI. |
| Ray mask/shape | `0x0068176e..0x006817a4`; `0x00689d80..0x00689db6` | OR flags for enabled entries in all eleven `(type,bool)` pairs; represent ray as a two-point FastPolygon. | Use M11-045 nodes, not source polygons. |
| Ray recursion | `0x00689f0a..0x00689f98` | If node type mask hits, return `overlap != 0` without consulting children. If it does not hit: leaf false; internal node with overlap recurses Q0→Q3 and short-circuits on first hit. | Touching geometry remains dependent on M11-048 intersection body. |

## M14-007 — behavior query over the rebuilt map

> Current manifest: `IMPLEMENTATION_GAP`; the previous EXACT_SOURCE claim was downgraded because C# answered the ray from polygons rather than the engine's quantised quad tree.

| step | address | source behavior | C# entry |
|---|---|---|---|
| Build ray | `0x005c2420..0x005c24a4` | Transform local `(40,0,0)`, 40.0f=`0x42200000`, by robot rotation and translation; call map virtual +0x30; return its negation. | `CanDriveIdealDistanceForward`. |
| Blocking mask | data `0x00c67962`; fold `0x0068176e` | Eleven booleans: only types 3,4,6,7,8,9,10 block; 0,1,2,5 do not. | Existing table holds; move query to M11-046. |
| Consequence | `0x005c2566..0x005c2572` | Clear ray → drive +40.0f=`0x42200000`; collision → drive -15.0f=`0xC1700000`; speed argument 40.0f. It changes distance, not whether an action starts. | Preserve the current behavior choice, but source it from the tree. |

Once M11-045/046 own the live query, source-fixed boundary tests must place obstacle polygons on 10 mm cell edges; the current polygon-based expectations are circular for this record.

## M11-017 — exact overhead-edge detector and map entry points

> Current manifest: `IMPLEMENTATION_GAP`, “build Detect, GroundPlaneROI, the ClampQuad clear quad and the four MapComponent entry points exactly, on ... M11-045, M11-046 and M11-047. ... OpenCV numerics are M11-048.”

| step | address / artifact | source behavior | C# entry |
|---|---|---|---|
| ROI/filter | statics `0x00c48f60`; `GetGroundQuad 0x004f7774`; detector `0x006abe34`; kernel `0x00c8e020` | Ground trapezoid 40..190 mm ahead, 40..150 mm wide. Project, bound, run shipped 7×5 kernel, mask outside corner order 0,2,3,1, transpose, scan each column bottom-up for first response >50. Columns without a hit become far-clear only between far-corner x values. | `OverheadEdges.cs` is polygon/double equivalent; replace with shipped integer/OpenCV path or emulation. |
| Lift gate/project/chain | `0x006ac4a2..0x006acdc8`; `0x006ae0a8`; `0x006ae1b0` | Abandon frame unless the two lift points straddle/outside the ROI rectangle as coded. Homography point rejected unless third component positive. Chain only same-kind points within 5.0f=`0x40A00000`. | Preserve rejection order. |
| World/map runs | `0x0067f7ac..0x006802ac` | Transform via robot pose at frame timestamp, split ray at near ROI edge, query two masks, keep direction within source constant 0.766f (bit pattern must be copied from the instruction pool during implementation), require run >6.00001f, and choose triangle versus midpoint line by squared length 225.0f=`0x43610000`; border chain adds type 9 line. | Exact float bit for 0.766 and 6.00001 must come from pool, not decimal retyping. |
| Entry points | `0x0067e6b0`, `0x0067e50c` | `FlagQuadAsNotInterestingEdges`: insert ClampQuad/type 10 with last-image timestamp. `FlagGroundPlaneROIInterestingEdgesAsUncertain`: Transform ROI with lambda `0x00680b54`, type 9→0 only. | Build on M11-045/046; Q4 M11-047/048 remain prerequisites. |

The binary's null-map dereference behavior is a policy item, not license to invent a managed fallback. Detector tests must use shipped-frame fixtures or independently encoded input/output bits; generating expected edges through the C# detector is circular.

## M13-024 — exact `minAreaRect` footprint

> Current manifest: `IMPLEMENTATION_GAP`, “build the port ... with strict float32 operations (no fused multiply-add) and double sqrt/atan2/sin/cos ... libm numerics are a policy question.”

| step | address | source behavior | C# entry |
|---|---|---|---|
| Hull/calipers | engine `0x004e6490..0x004e67c4`; OpenCV `0x0009c760..0x0009ceac`, hull/Sklansky `0x00039174..0x000395e0` | Convert projected points to `Point2f`, exact OpenCV convexHull/Sklansky ordering, rotating calipers, return `RotatedRect`. Degenerate 0/1/2-point branches must match. | Replace exact-rectangle and planar stand-ins in `BlockConfigurations.cs`. |
| Points | OpenCV core `0x00086188..0x00086276` | Compute four points from centre/size/angle with separately rounded float ops; no fused multiply-add. Opposite points use `2*center - p`. | Keep all intermediates binary32. |
| Numeric reference | validated extraction port | Axis-aligned 44 cube yields `(22,22),(-22,22),(-22,-22),(22,-22)` exactly. Yaw 0.3 has residual centre bits and nontrivial corners; tilted case produces a six-point hull and 53.677586×55.203194 rectangle. | Use fixed expected bit patterns from the validated port, not values returned by implementation. |

Double `atan2/sin/cos` are Android bionic external behavior; their possible final-float ULP difference remains Q4/M13-023 policy, not EXACT_SOURCE.

## M13-025 — per-class `GetBoundingQuadXY`

> Current manifest: `IMPLEMENTATION_GAP`, “build the class footprints for Markerless, Charger, Ramp, HumanHead; MatPiece/CustomObject ... and base numeric result stay open in M13-023.”

| step | address | source behavior | C# entry |
|---|---|---|---|
| Markerless | `0x00502c32..0x00502d42`; corners `0x005025f0` | Unit ±0.5 corners; size at +0x58/+0x5C/+0x60 plus twice padding; rotate each, take XY, M13-024 bounding quad, then add pose translation. | Add exact override. |
| Base variant | `0x0087713a..0x008772d2` | Fetch canonical-corner vector; add ±padding according to signbit (so -0 differs from +0); rotate by row-major matrix with non-fused float ops; M13-024; translate. | Shared by Charger/Ramp/HumanHead and the open vector-backed classes. |
| Charger | `0x004e9a50` | x `{0,96}`, y `{-40,40}`, z `{0,31}` in the extracted eight-corner order. | Exact fixed list. |
| Ramp | `0x0050e2d0` | x `{172=0x432C0000,222=0x435E0000}`, y `±37.25` (`0x42150000/0xC2150000`), z `{0,44=0x42300000}`. | Exact fixed list. |
| HumanHead | `0x004f8454` | Unit ±0.5 corners and **no size scaling** in the base variant. | Do not reuse Markerless scaling. |

MatPiece `+0x7C`, CustomObject `+0x64`, and `cv::Rodrigues` remain Q4/M13-023. Tests for those must remain explicitly incomplete rather than asserting a guessed corner list.

## M13-026 — head-angle calculations

> Current manifest: `IMPLEMENTATION_GAP`, “build both exactly ... including the 25/26 iteration quirk and result codes; GetCameraPose and robot members ... are M13-023.”

| step | address | source behavior | C# entry |
|---|---|---|---|
| Absolute angle | `0x0054b428..0x0054b55a` | `h=z-49` (`0xC2440000`), `d=sqrtf(x²+y²)`, `D=d+13`, `a=(300-D)/150`, `b=(0-h)/-10`; clamp01(NaN→0); `atan2f(h,D)` plus clamp bonuses 5°=`0x3DB2B8C2`, 7.5°=`0x3E060A92`, and 4°=`0x3D8EFA35`, in the extracted order. | `PanAndTiltActions.cs`; use `MathF`, with rounded stages. |
| Pose/frame gates | `0x00518344..0x005183f0` | `GetWithRespectTo(pose, robot+0x2CC)` failure warns and returns `0x06000000`; missing camera calibration errors/debug-breaks and returns 1. | Preserve distinct results. |
| Iteration | `0x00518410..0x00518600` | tolerance=`uint16 nrows*f + 1e-5f` (`0x3727C5AC`); each pass gets camera pose, inverses, projects; q.z not >1e-5 returns 1. Converge when `tol >= abs(focalY*q.y/q.z)`; otherwise `theta += -0.8f(0xBF4CCCCD)*atan2f(dv,focalY)`. Quirk: convergence exactly iteration 25 warns/returns 1; no convergence through pass 25 reaches index 26, returns success 0 **without writing output angle**. | Test 24/25/26 boundaries with an independent fake camera path. |

GetCameraPose and the fields at +0x2CC/+0x258 remain Q4/M13-023, so live production cannot settle until those are supplied.

## M13-027 — `TurnTowardsFaceWrapperAction`

> Current manifest: `IMPLEMENTATION_GAP`, “build the wrapper; read its callers to confirm the flag names.”

| step | address | source behavior | C# entry |
|---|---|---|---|
| Construct/order | `0x0054c7dc..0x0054c8bc` | Sequential compound. If bool A: create `TurnTowardsLastFacePoseAction(faceId=0,maxAngle,bool C)` and add `(false,false)`; add wrapped runner `(false,false)`; if bool B add the same turn-after action. Strict order is before → wrapped → after. | `FaceActions.cs`; flag semantic names remain UNKNOWN until callers are read. |
| Proxy | `0x0054c8c6..0x0054c8ca` | Set compound proxy tag from wrapped action `+0x60`. | Preserve completion/wire identity. |

Tests should assert child types, order, flags and proxy tag from literal source rows. Naming A/B as “turnBefore/turnAfter” is a local description, not recovered caller semantics.

## M11-038 — BlockWorld broadcasts and mailbox dispatch

> Current manifest: `IMPLEMENTATION_GAP`, “no EngineToGame message types or consumers for LocatedObjectStates, ConnectedObjectStates, RobotObservedObject ... or RobotMarkedObjectPoseUnknown ... CheckMailbox's result deque has no producer.”

| step | address | source behavior | C# entry |
|---|---|---|---|
| Observed object | `0x0061fed8..0x0061ffe8` | Project object; construct rectangle. Active LightCube must dynamic-cast to ActiveCube or error/return 1/no message. Wire fields in order: timestamp, family, type, ObjectID, rect x/y/w/h, pose, top-face radians, active; size 69. Broadcast, clear current, return 0. | Current `ObjectObserved` event is local and its rectangle source must be the native projection path. |
| Located/connected | `0x0061e6c0`, `0x0061e91c`; pack bodies `0x00714a10`, `0x00715370`, `0x007153a4` | Build and broadcast tags `0x53` LocatedObjectStates and `0x54` ConnectedObjectStates in native iteration order. Exact pack bodies remain unread in the earlier report and must be opened before claiming wire fidelity. | `BlockWorld.cs` counted stubs at current ~812-815. |
| Mailbox | `0x006b2ad4` | Drain vision-result deque and dispatch result handlers in engine order; producer belongs to M11-040. | No production producer at HEAD; counter is not a substitute. |

Protocol tests must assert fixed packed bytes from CLAD/native packers and observe the real broadcast consumer. Event-only tests are circular for the absent wire.

## M11-050 — complete `UpdateVisionMarkers`

> Current manifest: `IMPLEMENTATION_GAP`; all gates between history lookup and `UpdateObservedMarkers` are not built.

| step | address | source behavior | C# entry |
|---|---|---|---|
| Empty result | `0x00654d74..0x00654e4e` | Empty marker list still calls `UpdateObservedMarkers(empty)`; nonzero result warns; no docking update; return that result. | `VisionSystem.UpdateVisionMarkers`. |
| Historical state failure | `0x00654d92..0x00654de6`, `0x0065507e..0x006550a0` | `ComputeAndInsertStateAt(...,interpolate=1)`: result `0x06000000` logs mismatch and returns 0 silently; any other nonzero logs `HistoricalPoseNotFound`; neither calls world update. | Current nearest-state fallback must be removed; M11-052 supplies interpolation. |
| Origin/rotation gates | `0x00654e50..0x00654e7e` | Drop old-origin state; then `WasRotatingTooFast(tOut, 0.5236f=0x3F060A92, 0.174533f=0x3E32B8C2, false)` true drops silently. | Exact binary32 constants. |
| Per-marker | `0x00654e82..0x00655020` | Build historical camera once. Timestamp mismatch errors/drops. If marker camera first word equals live camera's and history key invalid, warn/drop. Otherwise replace id/calibration/pose/occluders with historical camera, draw quad, append. First-word semantic name UNKNOWN. | Do not pass original marker camera through. |
| Dispatch | `0x00654dec..0x00654e4e` | World update; warn on nonzero. Only nonempty accepted list updates DockingErrorSignal with result timestamp. | Preserve call/result order. |

## M11-051 — `RobotStateHistory::AddRawOdomState`

> Current manifest: `IMPLEMENTATION_GAP`; stale-drop, gap counter/reset, parent and duplicate-key rules are absent, while C# additionally purges another origin.

| step | address | source behavior | C# entry |
|---|---|---|---|
| Validate/insert | `0x00530cf8..0x00530ec4` | Apply stale timestamp drop before insertion; track consecutive time gaps and reset after the native threshold; require the expected pose-parent relation; reject/handle duplicate map key through the native branch rather than overwrite. Then insert and call `CullToWindowSize`. | `RobotStateHistory.Add`, current `VisionSystem.cs` / `RobotStateHistory.cs:37-66`. |
| Cull | `0x005309d1..0x00530bbe` | If raw size <2 or newest < window, return. Default window 3000 ms (`0xBB8`). Cutoff is newest **raw** timestamp minus window; lower_bound/erase old entries from all four maps. | Remove the unsourced clear/purge of another origin; use 3000 ms and common-map cull. |

The global double used by the consecutive-gap reset remains unread in the cited row; mark that threshold UNKNOWN until its pool/GOT source is opened. Boundary tests must fix literal timestamps and origin/parent IDs from the extracted branches.

## M11-053 — mismatch-triggered delocalization and all callees

> Current manifest: `IMPLEMENTATION_GAP`; the mismatch counter and most `Robot::Delocalize` callees are not built, and current RobotDelocalized carries the old origin.

| step | address | source behavior | C# entry |
|---|---|---|---|
| Counter | ctor `0x0050ff08`; compare `0x00512d7a`; reset `0x00512eae`; increment/threshold `0x00512f14..0x00512f22` | `Robot+0x2C0` starts 0. Matching frame resets it. Each consecutive mismatch increments; values <101 continue the rest of state processing. At 101, reset counter, log/error, delocalize. Origin-miss/history-failure paths do not change this counter. | Add to full-state production handler, before the same downstream gates as native. |
| Delocalize fields | `0x00510a24..0x00510d98`; trigger `0x00512f88..0x00512f96` | Allocate/add new origin, clear localized object (`+0x2B8=-1`), localization gate/score/state fields, and propagate the **new** origin through the call chain. | Current event uses old origin; fix allocation before notification. |
| Callee order | same body | Notify/reset BlockWorld, FaceWorld, AI whiteboard/components, BehaviorManager, MovementComponent, visualization, and send absolute localization update in the binary's order. Cliff-running-stat reset is among Delocalize effects; the frame-mismatch trigger state skips the later cliff schedule because failure flag is set. | `Faces.OnRobotDelocalized` presently has no caller; counted omissions are not production ownership. |

Tests must deliver 100 then 101 consecutive mismatched full states through the real handler, verify a matching state resets the run, and assert all consumer side effects/new-origin ordering. Directly invoking `Delocalize` does not test the trigger.

## Remaining UNKNOWN / Q4 dependencies

- M11-047: address-dependent unordered-set border order and `AddBorderWaypoint` neighbor/wall-following details.
- M11-048: ShiftRoot branch table, line intersection, several OpenCV bodies and vision result handlers.
- M11-052: frame of `GetWithRespectTo` temporary; its field blend is otherwise extracted by Q4.
- M13-023: bionic libm policy, `cv::Rodrigues`, canonical-corner writers, GetCameraPose and robot camera/origin fields.
- M11-042 and the M12/M13 RECOVERABLE_GAP list: use Q4's per-record rows; none may be silently filled while integrating this round.
