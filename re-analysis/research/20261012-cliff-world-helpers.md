# Cliff world helpers — 2026-10-12

## 1. M11-062 — coverage first

| Step | Coverage / original behavior established | Native citation | Record |
| --- | --- | --- | --- |
| Overlap predicate | Read: compares object types, gets the existing object's tolerances, calls IsSameAs. | `0x0061FD8C..0x0061FDF8` | M11-062 |
| Distance tolerance | Read: componentwise half-size; for type 15, (10,20,25) mm. | `0x00502B06..0x00502B30`; type-size dispatch `0x005026EC..0x0050286E` | M11-062 |
| Angle tolerance | Read: getter returns π/4, but IsSameAs ignores that argument and passes π instead. | `0x004E0290..0x004E029E`; `0x008769A8..0x008769EA` | M11-062 |
| Pose comparison | Read enclosing body: frame conversion, spherical/axis-box distance branches, angle/ambiguity branches. Recursive frame conversion internals remain UNKNOWN here. | `0x00846F3C..0x008471A8` | M11-062 / boundary |
| Intersection predicate | Read: own-pose bounding quad with captured padding, then Intersects against candidate quad. | wrapper `0x00622908..0x0062297C`; callable `0x0062750E..0x00627536` | M11-062 |
| Intersection geometry | Read: two triangle containment tests per quad, then 16 edge-pair tests; barycentric and line/segment arithmetic inspected. | `0x00514800..0x00514914`; `0x004DF764..0x004DF7BC`; `0x004E03B8..0x004E047E`; `0x008F6220..0x008F638C` | M11-062 |
| Cliff bounding quad | Read engine wrapper, projected corners, OpenCV calls and sorting. Full shipped OpenCV calipers/hull path remains UNKNOWN. | `0x00502C32..0x00502D72`; `0x004E6490..0x004E6598`; `0x004E7E9C..0x004E7F9E` | M11-062 / boundary |
| SetID / allocator | Read both unique and nonunique branches. One shared counter, loader-zero initialization, postincrement assignment. Cube allocation call and vtable slot checked. Startup allocation ordering remains UNKNOWN. | `0x004EF468..0x004EF52C`; `0x008412D8..0x008412E6`; counter `0x0105DFF0`; cube dispatch `0x00623946..0x00623954`, slot `0x0101CE10` | M11-062 |
| InitPose / SetPose(1) | Read: virtual dispatch, pose/state/name assignment, nonnegative distance gate and initialized flag. | `0x004EF562..0x004EF644` | M11-062 |
| Tail `0x008CAD1C` | Resolved Thumb→ARM veneer to pose-state notifier. No game message on the cliff branch. | `0x008CAD1C..0x008CAD28` → PLT `0x004A7024` → `0x00506288..0x005062DA`; family-6 return `0x0067EA9A..0x0067EAA4` | M11-062 |
| DeleteIntersectingObjects | Read wrapper and DeleteLocatedObjects body, selected entries, cleanup, deferred callback/message order, direct diagnostics. Transitive cleanup/configuration callbacks remain partially UNKNOWN. | `0x0062464C..0x00624732`; `0x0061D038..0x0061D656`; `0x00625C78..0x00625F66`; `0x00507806..0x0050780C`; `0x00506F88..0x00506FB8` | M11-062 / boundary |

### Answer and source scope

This answers `requests/20261012-cliff-world-helpers.md`, using the checked `20261012-m4-cliff-and-names-check.md` as navigation, not as a substitute for native instructions. `git pull` fast-forwarded main from `f39ac70` to `74a7a11` before extraction. No branch, commit, implementation, manifest, inventory or state edit was made. Everything stayed local. No fidelity status is assigned by this report.

Primary artifact: `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Addresses below are ELF virtual addresses, with Thumb bit cleared for code. ELF LOAD mappings and dynamic relocations were used for data and vtables. Ghidra pseudo-C was only a navigation aid; its data labels cannot safely be copied as ELF addresses.

Current manifest snapshot, read after the pull:

- M11-062: **RECOVERABLE_GAP**, title “BlockWorld helpers reached by AddMarkerlessObject (cliffs): IsSameAs, the intersecting predicate, GetBoundingQuad, SetID, InitPose, the 0x008CAD1C pose-state broadcast, DeleteIntersectingObjects”. Evidence names the checked report's M4-038 section and those unread helpers.
- M4-038: **IMPLEMENTATION_GAP**, title “CliffSensorComponent::ComputeCliffPose and BlockWorld::AddCliff (the detected-cliff path)”. Its current evidence includes the checked report; the world and map helper boundaries are separately named M11-062 and M13-030.

#### 1.1 IsSameAs: the π/4 argument is not applied

The helper at `0x0061FD8C` obtains the existing object's distance tolerance through virtual slot `+0x30`, obtains angular tolerance through `+0x34`, and initializes its output temporaries **before** checking that object type and family both match (`0x0061FDB8..0x0061FDC6`). A mismatch returns without adding a match. Otherwise it calls `Vision::ObservableObject::IsSameAs`; only a result of exactly 1 appends the existing-object pointer to the output vector, through its capacity or slow-growth branch (`0x0061FDDA..0x0061FDF4`). This helper returns void; the called IsSameAs returns the boolean. Markerless distance tolerance copies size and multiplies each component by float 0.5 (`0x00502B08`, `0x00502B26`). Type 15 has size (20,40,50), hence tolerance (10,20,25). The angular getter constructs `Radians(0x3F490FDB)` = float π/4 (`0x004E0292..0x004E029A`).

**The wrapper does not preserve or use incoming r3, the angular-tolerance reference.** It constructs its own `Radians(0x40490FDB)` = float π at `0x008769B4..0x008769C2`, obtains rotation ambiguities through slot `+0x38`, and passes that local π to `Pose3d::IsSameAs_WithAmbiguity` at `0x008769DA..0x008769E4`. This is an instruction-level finding, not an inference from a function name.

The callee's branches (`0x00846F3C..0x008471A8`) are:

1. Identical pose addresses return true immediately.
2. Construct a temporary pose in the existing pose's parent frame. There is a shared-parent fast path; otherwise call GetWithRespectTo. With no existing parent the comparison is relative to the existing pose itself. Failed conversion logs the warning below and returns false.
3. Write the positional difference to the output vector. If all tolerance components differ by less than `1e-5` (`0x3727C5AC`), compare squared Euclidean distance against the square of the first tolerance. Otherwise rotate the difference into the existing pose's local axes with the inverse existing rotation, and reject if an absolute component is greater than its tolerance. Equality at the component threshold passes. A cliff uses this second branch because 10,20,25 are unequal.
4. After position passes, construct π and compare supplied angular tolerance against it (`0x00847132..0x00847148`). Tolerance ≥ π returns true. Otherwise compute angle difference and return true if it passes; if it fails and the ambiguity set is nonempty, compare the relative rotation against the ambiguity set; otherwise return false.

For this cliff call the wrapper supplied π, so branch 4 returns true without computing the angle difference. The angular output is not written by that branch. A same-type cliff whose position is within the native local-axis tolerance matches regardless of angular difference. This corrects the premise that the caller's π/4 necessarily constrains the match.

There are no direct logs, errG writes or messages in the predicate or wrapper. The pose comparison's direct diagnostic is `sWarningF` at `0x00846FDE`, key `Pose3d.IsSameAs.PosesHaveDifferentOrigins`, format `Could not get other pose w.r.t. this pose. Returning not same.`, empty metadata, no explicit channel argument and no local errG write. Diagnostics inside recursive pose-conversion callees are not fully recovered here.

#### 1.2 Intersecting predicate and geometric edge cases

The wrapper at `0x00622908` obtains the candidate's quad through virtual slot `+0x50`, copies the supplied BlockWorldFilter, and appends a callable holding the quad reference and padding. The callable at `0x0062750E` obtains each located object's pose, invokes its `+0x50` quad method with captured padding, and returns `locatedQuad.Intersects(candidateQuad)` (`0x00627520..0x00627530`). Padding is float zero on the requested call. Neither callable nor wrapper has a direct log, errG or game/robot message.

The existing checked filter direction is preserved: on the AddMarkerlessObject collision query, robot localized-object ID != -1 makes families **{2,6} allowed**; otherwise every family counts. Current-origin restriction is also preserved. This pass does not reinstate the earlier report's backwards excluded-family interpretation.

`Intersects` (`0x00514800..0x00514914`) first checks the other quad's four corners against this quad, then this quad's four corners against the other quad. A successful containment returns true. If both passes fail it forms edge segments in corner order **0→2→3→1→0** and tests all 4×4 pairs. Any segment intersection returns true; exhaustion returns false.

`Contains` (`0x004DF764..0x004DF7BC`) tests triangles (corner 0,1,3) and (corner 0,2,3), stopping at the first success. `IsPointWithinTriangleHelper<float>` uses barycentric arithmetic with no degenerate-triangle guard (`0x004E03B8..0x004E047E`). For vertices A,B,C and point P:

```
inverse = 1 / ((Ax-Cx)*(By-Cy) + (Cx-Bx)*(Ay-Cy))
a = inverse * ((By-Cy)*(Px-Cx) + (Cx-Bx)*(Py-Cy))
b = inverse * ((Cy-Ay)*(Px-Cx) + (Ax-Cx)*(Py-Cy))
c = (1-a)-b
```

Each barycentric coordinate must be strictly greater than `-2^-23` (bits `0xB4000000`) and strictly less than the float successor of 1 (bits `0x3F800001`), with early returns after failed checks. Native single-precision operations and their order matter. For finite nondegenerate triangles this includes the edges within those limits. Do not replace it with an unspecified “point in polygon” routine or assume a special degenerate-triangle policy.

`kmLine2WithLineIntersection` (`0x008F6220..0x008F630A`) reconstructs each endpoint from ray start+direction, subtracts the start again, computes the float determinant, widens it to double and rejects determinants strictly between double -0.0001 and +0.0001. Outside that interval it calculates the line parameters and optional intersection point. There is no separate collinear-overlap branch. `kmSegment2WithSegmentIntersection` (`0x008F6320..0x008F638C`) returns false if the line helper fails or either parameter is outside the inclusive [0,1] interval; otherwise copies the intersection point and returns true. These geometry bodies have no logs, errG or messages. The native comparisons, rather than an added epsilon on segment parameters, determine touching behavior.

#### 1.3 GetBoundingQuad<float>

The cliff's `GetBoundingQuadXY` wrapper (`0x00502C32..0x00502D72`) obtains canonical corners through virtual `+0x54`, copies the pose, and constructs a scale vector **size+(2×padding)** componentwise (`0x00502C5C..0x00502C86`). For each canonical 3-D corner it multiplies by that scale, applies pose rotation, and appends x/y to the point vector. The scaling arithmetic still executes at padding 0; there is no padding-zero bypass. It calls GetBoundingQuad<float>, then adds the pose translation's x/y to each returned corner (`0x00502D2A..0x00502D42`, translation helper `0x004E68CC`), and releases its temporaries. Translation therefore occurs **after** the rotated local bounding quad is computed.

The engine's `GetBoundingQuad<float>` body (`0x004E6490..0x004E6598`) allocates a cv::Point2f vector of the same length, copies x/y values, constructs the OpenCV InputArray (flags `0x8103000D`), calls `cv::minAreaRect`, calls `cv::RotatedRect::points` for four points, constructs a Quadrilateral, calls SortCornersClockwise, copies that returned quad into the result, and releases the temporary point vector. The empty-input branch still proceeds to the OpenCV call; there is no engine-side empty/NaN validation or fallback rectangle. No engine-wrapper log, errG or message occurs.

SortCornersClockwise (`0x004E7EFC..0x004E7F9E`) computes a centroid, calls atan2f(y−centroidY,x−centroidX) for each corner, sorts four (angle,original-corner-index) pairs with CompareFirst, and constructs a new quad. If the ascending angle sequence is P0,P1,P2,P3, native storage slots are **[P0,P3,P1,P2]**. This comes from argument selection at `0x004E7F7A..0x004E7F96` and constructor stores at `0x004E7EB2..0x004E7EF2`; it is consistent with the Intersects perimeter walk above.

The OpenCV functions are imports from shipped libraries, not absent phone-system code. Local symbol inspection resolves `cv::minAreaRect` to `libopencv_imgproc.so` entry `0x0009C760` (1,944-byte symbol), and `RotatedRect::points` to `libopencv_core.so` entry `0x00086188` (264-byte symbol). Their full bodies and the hull/calipers callees were not recovered in this pass. Consequently the wrapper is answered, but the complete numerical/degenerate-case bounding-quad path remains **UNKNOWN**. No equivalent implementation is proposed.

#### 1.4 SetID and sharing with cubes

`ObservableObject::SetID` (`0x004EF468..0x004EF52C`) calls virtual `+0x5C`:

- If it does not return exactly 1, tail-call ObjectID::Set on the object's ObjectID member.
- If it returns 1, initialize a function-static object-type→ObjectID map through the C++ guard if needed, then look up type. A hit copies the cached ID. A miss allocates through ObjectID::Set and inserts that type/ID into the map. Map construction has its destructor registered through `__cxa_atexit`. No direct log or errG branch exists.

Markerless vtable `0x0101F318` slot `+0x5C` points to `0x004E02AE`, returning 0. Thus each accepted new cliff consumes a new ID. It does not use the unique-type cache.

`ObjectID::Set` consists of the counter load, increment and stores at `0x008412D8..0x008412E6`: load shared counter C; write C+1 to it; write old C to ObjectID+4. The symbol `ObjectID::UniqueIDCounter` is at `0x0105DFF0` in ELF `.bss`, so its loader-initialized value is **0**. There is no lock/atomic instruction, range check or wrap guard in this body. Allocation is 32-bit postincrement. This establishes the allocator's initial value, not that the first cliff detected after engine startup receives ID 0; previous allocations can consume it.

Cube IDs share this allocator. The no-existing-match branch in AddConnectedActiveObject adjusts to its ObservableObject virtual base, loads slot `+0x24`, and calls it (`0x00623946..0x00623954`). The installed ActiveCube ObservableObject vptr is `0x0101CDEC`; its `+0x24` slot `0x0101CE10` relocates to `ObservableObject::SetID`, `0x004EF469` with Thumb bit. Cubes may reuse a matching ID or the unique-type cache, but newly allocated cube and cliff object IDs do not have separate counters. This is unrelated to the robot's cube activeID slots or cube factory IDs. No game/robot message is emitted by either allocator body.

#### 1.5 InitPose / SetPose with state 1

`InitPose` (`0x004EF562..0x004EF57C`) supplies distance float -1 (`0xBF800000`) and the given state to the object's virtual SetPose at `+0x28`; after return it sets initialized byte `+0x50` to 1. The cliff slot resolves to `0x004EF581`, with body `0x004EF580` calling the shared SetPose body at `0x004EF590`, then also setting that initialized byte.

The shared body (`0x004EF590..0x004EF644`) assigns the pose, writes state to byte `+0x24`, constructs the pose's debug name from `Object_` plus the signed decimal object ID, assigns that name, and writes distance field `+0x10` only when the supplied float distance is nonnegative. With InitPose's -1 the previous distance is retained. State 1 is stored unchanged. Since cliff InitPose occurs before SetID, the initial pose name uses ID -1; SetID itself does not rename the pose. These bodies return void and contain no direct logging, errG or broadcast. Pose/string assignment internals are an explicitly separate boundary.

#### 1.6 The tail is an internal notifier; no cliff game payload

BroadcastObjectPoseChanged (`0x00506F88..0x00506FB8`) calls BlockWorld::OnObjectPoseChanged, then MapComponent::UpdateObjectPose, then tail-branches to `0x008CAD1C`. The veneer executes Thumb `bx pc`, switches to ARM at `0x008CAD20`, loads its displacement and adds it to PC, resolving to PLT `0x004A7024`, the imported-in-library entry for `ObjectPoseConfirmer::BroadcastObjectPoseStateChanged` (`0x00506288`). It is not a direct Robot::Broadcast call.

The notifier compares current state byte `object+0x24` with the previous-state argument. If equal it returns. Otherwise, if current state != 0, it calls MapComponent::UpdateObjectPose with the object's current pose and previous state. It next calls IsActive through virtual `+0x0C`; only the active branch invokes the cube-light component's state-change callback. A cliff returns 0 for IsActive and exits. MapComponent::UpdateObjectPose itself returns immediately for family 6 (`0x0067EA9A: ldr [object,#0x48]`, `0x0067EA9C: cmp #6`). Thus the new cliff transition old=0/current=1 produces **no engine-to-game message and no payload** through this tail. There is also no direct notifier log or errG.

The preceding BlockWorld callback (`0x00624808..0x006248B8`) checks its queue-enabled byte `+0x74`. With queueing enabled: an already-queued ID logs MultipleChanges; a first change with old state 0 logs FirstPoseForObject without queueing; otherwise it queues ID/old pose/old state. All paths call BlockConfigurationManager::SetObjectPoseChanged with ID and current state. That configuration manager's full transitive behavior remains UNKNOWN here; absence of a notifier message is not a claim that every caller or subsequent tick sends nothing.

#### 1.7 DeleteIntersectingObjects, allowed type {14}, padding 0

At `0x00622C68..0x00622C84`, AddLocatedObject sets allowed type {14} and invokes DeleteIntersectingObjects with padding zero. The wrapper (`0x0062464C..0x00624732`) computes the incoming object's quad, copies the filter, appends the same intersection callable `0x0062750E`, and calls DeleteLocatedObjects. It returns void. The query deletes **located type-14 entries satisfying the filter and geometric predicate**, not the incoming type-15 cliff, and does not erase connected-object records. It operates on located-map entries, not only a temporary list.

DeleteLocatedObjects (`0x0061D038..0x0061D656`) iterates origin, family, type and ID maps in their native ordered-map order, applying allowed/ignored origin/family/type/ID sets and appended object predicates. The supplied filter constrains the selected type to 14. A nonnull passing entry in the current origin is first passed to ClearLocatedObjectHelper, then cloned through virtual slot 0; its ID is copied into the clone. The active clone branch also preserves active/factory IDs, with the NotActive warnings if the nested IsActive checks fail. Old pose/state and clone are appended to a deferred vector. The located entry is erased, and empty inner type/family/origin maps are erased. Non-current-origin deletion skips this current-origin callback/deferred-notification path. A null stored pointer follows the NullObject warning branch and erasure, without a clone notification.

After map iteration, each deferred current-origin deletion performs, in order:

1. `DeletedObjectInCurrentOrigin(ObjectID)` (`0x0061D5B8`). Its small body at `0x00507806` erases the ID from the pose-confirmer map at `this+4`.
2. `BroadcastObjectPoseChanged(clone,&oldPose,oldState)` (`0x0061D5C8`), including the internal callbacks described above.
3. Obtain the clone's ObjectID value, construct MessageEngineToGame from `RobotDeletedLocatedObject`, call Robot::Broadcast (`0x0061D5CC..0x0061D5E4`), clear the message, and destroy the clone.

The direct game payload is **RobotDeletedLocatedObject.objectID**, the deleted object's ID; the native payload temporary is one 32-bit value at stack+0x68. The generated Size body at `0x007143EC` returns 4. No cliff pose, origin, geometry, sensor mask or newly assigned cliff ID is added to that payload. Transport serialization/delivery and listeners are outside this report.

ClearLocatedObjectHelper (`0x00625C78..0x00625F66`) warns and returns for a null argument. For a nonnull object, it separately checks whether the robot is localized to its ID, is carrying its ID, and whether it is the selected ID. Matching branches log and call SetLocalizedTo(nullptr), call UnSetCarryingObjects(false), and set selected ID=-1 respectively. It then finds an object above using FindObjectOnTopOrUnderneathHelper(object,15.0,filter,true), recursively clears that result if nonnull, sets BlockWorld dirty byte `+0x54=1`, and stores `(uint32)fmax(robotTimestamp,currentMapHelperTimestamp)` at `+0x58` (`0x00625E74..0x00625EB8`). Recursive clearing is not itself another located-map deletion. The top-object helper's outer body was inspected, but its complete candidate predicate and all transitive callbacks are not settled here.

#### 1.8 Direct logs, levels, channels and errG

All calls below construct empty metadata vectors. “No explicit channel” means the unchanneled `sWarningF` call signature, not an invented logger channel. The logging runtime's existing routing is a boundary owned by its records.

| Reachable body / branch | Level and explicit channel | Key | Format | Call / errG |
| --- | --- | --- | --- | --- |
| Pose conversion failed | warning; no explicit channel | `Pose3d.IsSameAs.PosesHaveDifferentOrigins` | `Could not get other pose w.r.t. this pose. Returning not same.` | `0x00846FDE`; no local errG |
| Delete active clone nested check fails | warning; no explicit channel | `ObservableObject.SetActiveID.NotActive` | `ID: %d` | `0x0061D216`; no local errG |
| Delete factory-ID nested check fails | warning; no explicit channel | `ObservableObject.SetFactoryID.NotActive` | `ID: %d` | `0x0061D268`; no local errG |
| Null located pointer | warning; no explicit channel | `BlockWorld.DeleteLocatedObjects.NullObject` | `Deleting null object in origin %d with Family:%s Type:%s ID:%d` | `0x0061D492`; no local errG |
| Null clear-helper argument | warning; no explicit channel | `BlockWorld.ClearObjectHelper.NullObjectPointer` | `BlockWorld asked to clear a null object pointer.` | `0x00625ED0`; no local errG |
| Clears localization | info; `BlockWorld` | `BlockWorld.ClearObjectHelper.LocalizeRobotToNothing` | `Setting robot %d as localized to no object, because it is currently localized to %s object with ID=%d, which is about to be cleared.` | `0x00625CBE`; no local errG |
| Clears carried object | info; `BlockWorld` | `BlockWorld.ClearObjectHelper.ClearingCarriedObject` | `Clearing %s object %d which robot %d thinks it is carrying.` | `0x00625D20`; no local errG |
| Clears selected object | info; `BlockWorld` | `BlockWorld.ClearObjectHelper.ClearingSelectedObject` | `Clearing %s object %d which is currently selected.` | `0x00625D7E`; no local errG |
| Queue enabled, first pose old=0 | info; `BlockWorld` | `BlockWorld.OnObjectPoseChanged.FirstPoseForObject` | `Object '%d' is setting its first pose. Not queueing change.` | body `0x00624808..0x006248B8`; no local errG |
| Queue enabled, ID already queued | info; `BlockWorld` | `BlockWorld.OnObjectPoseChanged.MultipleChanges` | `Object '%d' is changing its pose again this tick. Ignoring second change` | same body; no local errG |

The direct type-14 filter and IsActive branches must not be conflated: the clone warnings are guarded by active checks, not unconditional deletion logs. The named pure geometry, ID, InitPose/SetPose and cliff notifier bodies have no direct diagnostics/messages. This table does not claim all transitive dependencies are diagnostic-free.

### What stays UNKNOWN for M11-062

- Full shipped OpenCV minAreaRect→convex hull/rotating calipers and RotatedRect::points arithmetic, degenerate/NaN behavior and diagnostics. The local library exports were resolved, but their complete production path was not extracted.
- Sort comparator equality/NaN tie behavior and every downstream standard-library failure path; the engine's sort call, selected argument order and result stores are established.
- Recursive pose-parent conversion/assignment, string internals and their diagnostics beyond the enclosing bodies read here.
- Every startup consumer/reset writer of UniqueIDCounter. Loader-zero allocation origin and the common allocator are established; a particular first cliff ID after startup is not.
- Complete top/underneath candidate predicate and the transitive effects of SetLocalizedTo, UnSetCarryingObjects, BlockConfigurationManager::SetObjectPoseChanged and later pose-change queue consumers. The directly invoked calls, local side effects and direct deletion game message are established, but a blanket “every deletion effect/message recovered” claim is not supported.

The current M11-062 evidence names unread helpers rather than making contrary settled claims. The π/4-use assumption and the “pose-state broadcast to game” interpretation are corrected above; no manifest change or reclassification follows from this report.

## 2. M13-030 — coverage first

| Step | Coverage / original behavior established | Native citation | Record |
| --- | --- | --- | --- |
| Cliff data construction | Read: type 8, two uint32 timestamps, two float normal components; no object ID/pose payload. | `0x00684984..0x006849A4` | M13-030 |
| Clone / equality | Read: clone preserves fields; equality compares type and normal with strict epsilon, ignores timestamps. | `0x006849AC..0x006849DA`; `0x006849DC..0x00684A2C`; NodeContent `0x0068C450..0x0068C4EA` | M13-030 |
| Insert dispatch | Resolved installed vptr and +8 slot; FastPolygon conversion, QuadTree insert with shift limit 1. | slot `0x0103096C` → `0x006817C6`; `0x006817D6..0x006817E0` | M13-030 |
| QuadTree entry / validation | Read NaN, containment, removal and expansion branches and direct diagnostics. | `0x00685008..0x00685132`; expansion `0x00685200..0x0068544A` | M13-030 |
| Expansion / shifting | Read outer root-upgrade and shift bodies and logs. Constructor, swap/destroy and polygon-cache dependencies remain partially UNKNOWN. | `0x006876D8..0x00687A3A`; `0x00686DF8..0x0068743E` | M13-030 / boundary |
| Recursive overlap / insertion | Read incoming type-8 override, recursive splitting, leaf set, timestamp mutation and changed comparison. Full FastPolygon overlap dependencies remain UNKNOWN. | `0x00686CB4..0x00686DE2`; `0x00688290..`; `0x00687E78..0x00687F28`; `0x006880B0..` | M13-030 / boundary |
| Subdivision / force-set / merge | Read splitting, replacement, type-change callback and adjacent-leaf equality merge with min/max timestamps. | `0x00687BEC..0x00687DC6`; `0x00687518..`; `0x00687F2A..0x006880A0`; clear `0x00687E1C..0x00687E76` | M13-030 |
| Processor effects / broadcasts | Inspected type-change/destruction processor bodies: area/content-index updates and border invalidation. No direct broadcast in inspected insertion path; later scheduled publishing is outside scope. | `0x00688E50..0x006890AA`; `0x0068928C..0x0068934C`; separate broadcast slot `0x010309A8` → `0x006817BA` | M13-030 / boundary |
| Timestamp getter +0x38 | Fully resolved and read: returns root content's uint32 last-observed timestamp. | slot `0x0103099C` → `0x00681934..0x0068193A`; caller `0x006226EA..0x0062271E` | M13-030 |

### Answer

Current M13-030 is **RECOVERABLE_GAP**, title “The MemoryMap insert of cliff data (MemoryMapData_Cliff, QuadTree insert, helper +0x38 timestamp)”. Its evidence quotes checked M4-038 step 7: BlockWorld+0x54=1, cliff data constructor, quad getter, insert through helper+8 and fmax timestamp. This report expands those boundaries without changing the record.

#### 2.1 Data held and virtual targets

`MemoryMapData_Cliff` constructor (`0x00684984..0x006849A4`) installs its vptr, writes content type byte **8** at `+4`, writes incoming uint32 timestamp to **both +8 and +0xC**, and writes normal.x/normal.y float bits at **+0x10/+0x14**. +8 is the first-observed timestamp and +0xC the last-observed timestamp, as established by the merge min/max operations below. The AddMarkerlessObject caller supplies `(R·X).xy` and robot timestamp; the object pose and ID are not stored in this data subclass. The constructor has no log, errG or message.

Clone allocates the subclass and preserves timestamps/normal/type. Equals (`0x006849DC..0x00684A2C`) returns false for a null pointer or differing content type, then requires both absolute normal-component differences to be strictly less than float `0x35A00000` = 1.1920928955078125e-6. Timestamps are not compared. NodeContent equality checks content type, returns true for the same data pointer, otherwise invokes the data's Equals; this is the equality used by auto-merge, not timestamp equality.

The MemoryMap vtable symbol is `0x0103095C`, installed address `0x01030964`. Its +8 slot at `0x0103096C` resolves to `0x006817C7` (Thumb), MemoryMap::Insert. Insert constructs FastPolygon from the passed polygon, calls QuadTree::Insert on `this+8`, passing the data reference and **r3=1** (`0x006817DE`), then releases temporary polygon vectors. The 1 is the permitted root-shift count, not a content type.

#### 2.2 Insert, splitting, overwrite and merge

QuadTree::Insert (`0x00685008..0x00685132`) checks each polygon point's x/y with isnanf. Any NaN logs error, sets errG, conditionally debug-breaks, dumps the callstack and returns without inserting. With valid vertices, if the root does not contain the polygon: removal content logs a warning without causing expansion; other content calls Expand. Type 8 is not removal content. It then inserts at the root (`QuadTree+0x50`) and ORs the returned changed flag into QuadTree's dirty byte at offset 0.

Expand (`0x00685200..0x0068544A`) computes polygon-centroid direction from root center, repeatedly calls UpgradeRootLevel with maximum level **8**, and rechecks containment. If still not contained it attempts ShiftRoot up to the supplied limit; the requested MemoryMap path supplies one shift. If containment still fails it logs InsufficientExpansion. Expansion marks dirty even when it cannot fully contain the polygon; the subsequent insertion still uses the root overlap logic.

UpgradeRootLevel (`0x006876D8..0x00687A3A`) returns false if depth byte is 0xFF or already ≥ maximum. Otherwise it preserves the old subtree/content in the appropriate child, creates four children, initializes the other regions with Unknown content and the previous root timestamp, moves the root center toward the requested direction by half its old side length, increments level and doubles side length, resets bounds, logs the new size and returns true. The directional comparisons use -1e-5; the native quadrant selection must be retained rather than replaced by an assumed signed-zero convention.

ShiftRoot (`0x00686DF8..0x0068743E`) detects excursions beyond the root's ±half-side bounds with 1e-5 offsets. Opposing excursions in x or y produce CantShiftPMx/PMy and false. With no excursion it logs AllPointsIn, sets errG and returns false. Otherwise it translates root center by half-side in the selected axis directions, resets bounds, creates replacement quadrant layout when children exist, swaps preserved content/subtrees into overlapping quadrants, discards regions leaving the root, logs ShiftRoot and returns true. The detailed transitive swap/destruction/constructor effects remain a boundary; this report does not claim full closure of those helpers.

Node::Insert builds NodeContent, cloning the supplied data, and invokes Insert_Recursive (`0x00686CB4..0x00686DE2`). In the recursive body:

1. Identical incoming/existing data pointer returns unchanged.
2. Save the previous shared content; compute overlap. No overlap leaves it unchanged.
3. On **any nonzero overlap**, assign incoming last-observed timestamp to the old content's `+0xC` **before** override/splitting checks (`0x00686D30..0x00686D3A`). This mutates the referenced content, potentially shared by more than one node.
4. If children exist and CanOverrideSelfAndChildren permits full replacement, clear descendants and force-set incoming content.
5. Otherwise, a non-fully-covered leaf with nonzero remaining depth is subdivided. Recurse through children in vector order and OR their changed results, then TryAutoMerge. A leaf at terminal depth or wholly covered leaf calls TrySetDetectedContentType.
6. Return child-change OR NodeContent inequality against the saved content. Since equality ignores timestamps, timestamp-only changes can occur without setting the changed flag.

GetOverlapType (`0x00688290`) distinguishes 0 no overlap, 1 partial, 2 node fully inside polygon, 3 polygon fully inside node. It checks polygon vertices against node bounds with 1e-5, then edge intersections and polygon containment of the node center as needed. These enum values come from native returns and call-site comparisons. The complete FastPolygon edge/cache construction is not closed here.

**An incoming cliff (type 8) is always permitted to override a leaf's existing content**, irrespective of old type (`0x00687E78: cmp r1,#8`, immediate true at `0x00687E7C`). The full-subtree override helper additionally requires overlap value 2 and recursive permission for every child (`0x00687EF0..0x00687F28`); incoming cliff passes those permission checks. Thus full-node coverage collapses its descendants to cliff; partial coverage splits until a leaf can be set. The existing-content protection rules for incoming non-cliff types do not prevent this cliff insertion. One relevant later rule is that existing cliff can be overridden by incoming type 2 only on full overlap, as seen at `0x00687E86..0x00687E9C`; this is a different direction from inserting cliff.

Subdivide (`0x00687BEC..0x00687DC6`) creates four children centered at the parent center ±quarter-side, in ++,+−,−+,−− order; each side is half the parent's, each depth is one lower. Child contents inherit the parent content through force-set; parent content becomes Unknown. ForceSetDetectedContentType (`0x00687518`) replaces the shared NodeContent; its special removal-content branch concerns incoming type 5 and is not taken for cliff. It calls OnNodeContentTypeChanged only when old and new type bytes differ, not merely because normal/timestamp differs.

TryAutoMerge (`0x00687F2A..0x006880A0`) stops if any child still has children. It compares adjacent child NodeContents, accumulating **minimum first-observed** and **maximum last-observed** uint32 timestamps. If any content comparison fails it does not merge. If all compare equal, it uses the first child's shared content, updates those two timestamps, clears descendants and force-sets the parent. Consequently different cliff timestamps do not prevent merging; cliff normals must pass the two strict tolerance comparisons. It does not average normals: it retains the selected first child's content. ClearDescendants recursively notifies OnNodeDestroyed before freeing descendants (`0x00687E1C..0x00687E76`).

Processor bodies (`0x00688E50..0x006890AA`, `0x0068928C..0x0068934C`) update explored/interesting-edge areas, remove/add nodes in tracked-type sets, and invalidate cached borders. Area uses the square of side length converted from mm to m. The tracked-type bitmask is `0x7C8`, which includes cliff type 8. No direct game/robot broadcast occurs in these inspected insert/merge/processor bodies. MemoryMap's Broadcast is a separate virtual method (slot `0x010309A8` → `0x006817BA`) and is not called by Insert. Later publication timing and payloads are outside this extraction.

#### 2.3 Logs, error state and messages

All listed logs use empty metadata. Error/warning calls have no explicit channel argument; info calls explicitly use channel `QuadTree`.

| Branch | Level / channel | Key | Format | Native call / errG |
| --- | --- | --- | --- | --- |
| NaN polygon | error / no explicit channel | `QuadTree.Insert.NaNPoly` | `Poly is not valid, at least one coordinate is NaN.` | `0x00685052`; errG=1, conditional sDebugBreakOnError, then sDumpCallstack with `QuadTree::Insert` |
| Removal outside root; not type 8 | warning / no explicit channel | `QuadTree.Insert.RemovalPolyNotContained` | `Poly is not fully contained in root, removal does not cause expansion.` | `0x006850E4`; no local errG |
| Expansion inadequate | warning / no explicit channel | `QuadTree.Expand.InsufficientExpansion` | `Quad caused expansion, but expansion was not enough PolyCenter(%.2f, %.2f), Root(%.2f,%.2f) with sideLen(%.2f).` | body `0x00685200..0x0068544A`; no local errG |
| Root upgrade succeeds | info / `QuadTree` | `QuadTree.UpdgradeRootLevel` | `Root expanded to level %u. Allowing %.2fm` | `0x006879FA`; no local errG; key's spelling is shipped |
| Shift cannot fit both x sides | warning / no explicit channel | `QuadTreeNode.ShiftRoot.CantShiftPMx` | `Current root size can't accomodate given points` | `0x00686EE0`; no local errG; spelling is shipped |
| Shift cannot fit both y sides | warning / no explicit channel | `QuadTreeNode.ShiftRoot.CantShiftPMy` | same format | `0x00686F08`; no local errG |
| Shift requested though all points in | error / no explicit channel | `QuadTreeNode.ShiftRoot.AllPointsIn` | `We don't need to shift` | `0x00686F58`; errG=1 and conditional sDebugBreakOnError |
| Shift succeeds | info / `QuadTree` | `QuadTree.ShiftRoot` | `Root level is still %u, root shifted. Allowing %.2fm` | `0x006873D8`; no local errG |

The two successful-root info messages supply level and side-length×0.001 widened to double. Expansion warning supplies polygon center, root center and root side length widened to double. ELF symbols resolve `errG` to `0x0105DD34` and debug-break gating to `0x01051E78` (the decompiler's corresponding data labels are displaced by 0x10000). The constructors, Equals, timestamp getter, recursive insert, force-set and merge bodies contain no direct diagnostics or game messages. Transitive UNKNOWN boundaries below prevent a blanket claim of complete diagnostic coverage.

#### 2.4 +0x38 is a root timestamp, not a recursive maximum

Installed MemoryMap vtable `0x01030964` +0x38 gives slot `0x0103099C`, value `0x00681935`. The complete Thumb getter is:

```
0x00681934: ldr.w r0, [r0,#0xDC]
0x00681938: ldr   r0, [r0,#0x0C]
0x0068193A: bx    lr
```

MemoryMap contains QuadTree at +8; root is QuadTree+0x50; node content shared_ptr is +0x84. Therefore MemoryMap+0xDC (=8+0x50+0x84) is the **root content pointer**, and the getter returns its uint32 last-observed timestamp. It does not walk children or independently compute their maximum.

The AddMarkerlessObject caller loads this getter at `0x006226F6`, converts both timestamps from **uint32 to double** (`0x00622702`, `0x00622706`), calls fmax, converts back to uint32 and stores BlockWorld+0x58 (`0x00622712..0x0062271E`). These fields are not float timestamps despite VFP loads/stores. Root last-observed is updated by overlap timestamp mutation, root replacement and min/max merge; the getter itself performs no validation/logging/message.

### What stays UNKNOWN for M13-030

- Complete FastPolygon construction/cache, containment and edge-intersection dependency bodies, including malformed/degenerate/empty polygon behavior beyond the outer branches inspected.
- Full QuadTreeNode constructor, SwapChildrenAndContent, DestroyNodes and border-cache invalidation dependencies reached by expansion and shifting. Root-level branches, selected side effects and direct logs are established, but their complete transitive path is not claimed recovered.
- Full later MemoryMap publication path and timing. No broadcast is called directly in the inspected synchronous insert path; that is not a claim that no subsequent update publishes it.
- System allocator/container exceptional paths and logging runtime internals are separate boundaries. Their absence from this report cannot be used to close every transitive log/message possibility.

M13-030's current evidence is an open boundary, not a contradicted settled claim. The unsigned timestamp/root getter and overwrite/merge findings are new details for checking. No fidelity-manifest impact is applied; no regression test, hardware run or commit was performed.

## Local extraction aids

Native address annotations were saved beside this report in `20261012-cliff-world-helpers-native.txt`; focused reads in `-focus.txt`, `-map.txt`, `-details.txt`, `-extra.txt`, `-geometry.txt`, `-map-tail.txt`, `-delete.txt`, `-final-read.txt`, `-ids.txt`, `-poses.txt`, `-quad-tail.txt` and `-vtables.txt`. `20261012-cliff-world-helpers-disasm.py` maps ELF LOAD segments and annotates local Thumb instructions. Literal pools can interrupt linear decoding; focused entry/range reads and relocation inspection were used where needed. These files are local navigation/checking aids, not a substitute for checking the cited primary bytes.
