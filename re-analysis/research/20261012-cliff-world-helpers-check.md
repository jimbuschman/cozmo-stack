# Opus check of R-CLIFF-WORLD (2026-10-13)

- **Checked:** `research/20261012-cliff-world-helpers.md` (Codex), against libcozmoEngine.so. Read-only
  cozmo-extractor.
- **Adopted:** by the manager on 2026-10-13. This file is the checked source for job B-M3M4-16. Use it, not the Codex
  report it checks.
- **Result:** no reversed filter, wrong global or skipped caller. There are five corrections, and several omissions
  filled.
- **Both headline findings hold:**
  - the π/4 angle tolerance never applies, because IsSameAs passes π;
  - the 0x008CAD1C tail sends no game message for a cliff.

## 1. M11-062: the BlockWorld helpers reached by AddMarkerlessObject

### The overlap test

- **The search** (0x0062243E..0x006224A8; 0x0061FE0A..0x0061FE4A):
  - look up the current origin in BlockWorld+0x40 by lower_bound;
  - an absent origin skips the overlap test and goes straight to the filter build;
  - otherwise do operator[] on family 6, and FindOverlappingObjects iterates only the candidate's type map (key =
    candidate+0x4C).
- **A hit:** **every** matching existing object gets +0x1C = robot+0x2C (the loop at 0x006224B6..0x006224C4), then the
  candidate is dropped. *(Correction: all matches, not only the first.)*
- **CheckForOverlapHelper(cand, existing)** (0x0061FD98..0x0061FDF4):
  - the existing object's tolerance getters run first: vt+0x30 distance, then vt+0x34 angle;
  - then type (+0x4C) and family (+0x48) must be equal;
  - then `existing.IsSameAs(cand, …)`. Only a result of 1 pushes the existing object. Returns void.
- **The tolerances:**
  - **Distance:** size (+0x58) × 0.5 per component (0x00502B08, 0x00502B26). Type 15 has size (20,40,50)
    (0x0050271E..0x0050274A), so the tolerance is (10,20,25).
  - **Angle:** the getter returns π/4 (0x3F490FDB, 0x004E0292..0x004E029A), but that has no effect, because:
- **Vision::ObservableObject::IsSameAs** (0x008769B4..0x008769E4) ignores r3. It builds Radians(π) (0x40490FDB), gets
  the ambiguities from vt+0x38, and calls `Pose3d::IsSameAs_WithAmbiguity(existing.pose, cand.pose, amb, distTol, π,
  outPos, outAngle)`.

### IsSameAs_WithAmbiguity

1. The same pose object returns 1 (0x00846F4A..0x00846F4C, 0x0084700A).
2. **The frame conversion** has three cases. *(Correction: Codex said every failure logs, and called the third case a
   shared-parent fast path.)*
   - **`this` has a parent that is not `other`** (the cliff case: the parent is the world origin):
     GetWithRespectTo(other, parent, temp). A failure goes to 0x0084719A and returns 0 **silently**.
   - **`this` has no node or no parent:** GetWithRespectTo(other, this, temp). A failure logs sWarningF
     `Pose3d.IsSameAs.PosesHaveDifferentOrigins` (0x00847204) / "Could not get other pose w.r.t. this pose. Returning
     not same." (0x00847230), then returns 0.
   - **`this`'s parent node == `other`'s node:** temp = identity, with SetParent(other).

   The ranges are 0x00846F7A..0x00846FC2, 0x00846FC4..0x00846FDE and 0x0084700E.
3. **The position test:**
   - **Spherical,** used when |tol.x−tol.y| < 1e-5 **and** |tol.x−tol.z| < 1e-5 (0x3727C5AC, strict): passes when
     diff·diff ≤ tol.x². outPos is the untransformed diff (0x00847054..0x008470D4).
   - **Box** (the cliff case): d = R_existing⁻¹·diff, absolute per component. It rejects if any component is greater
     than its tolerance (`bgt`), so equality passes (0x008470D6..0x00847130).
4. **The angle test** (0x00847132..0x00847196):
   - angleTol ≥ π returns 1, which is always the case here;
   - otherwise GetAngleDiffFrom → outAngle, and ≤ tol returns 1;
   - otherwise empty ambiguities return 0;
   - otherwise IsRotationSame(R_existing⁻¹·R_other).

### The intersection filter and quads

- **The wrapper** (0x00622908..0x0062297C):
  - the candidate quad = cand vt+0x50(cand.GetPose(), padding), with zero padding here;
  - it copies the filter (bl 0x005C6A88), appends a std::function (vtable 0x0102DC3C; operator() 0x0062750F), and calls
    FindLocatedMatchingObjects.
- **The callable** (0x0062750E..0x00627536): the located object's quad = vt+0x50(obj.GetPose(), [this+8] padding).
  Returns `locQuad.Intersects(*[this+4])`.
- **Intersects** (0x00514800..0x00514912):
  - this.Contains(other corner i) for i = 0..3, then other.Contains(this corner i);
  - then 4×4 kmSegment2 tests on the edges 0→2→3→1→0 of each quad.
- **Contains** (0x004DF764..0x004DF7BC): the triangle (c0,c1,c3), then (c0,c2,c3). It returns at the first true.
- **The point-in-triangle helper** (0x004E03B8..0x004E047E): the formulas and evaluation order are as in the Codex
  report. a, b and c must each be > −2⁻²³ (0xB4000000; `ble` rejects) and < 0x3F800001 (strict). NaN rejects.
- **kmRay2FillWithEndpoints** (0x008F61E4..0x008F621A): start = p0; dir = p1 − p0.
- **kmLine2WithLineIntersection** (0x008F6252..0x008F62A6): rejects when the double determinant is strictly inside
  (−1e-4, +1e-4) (the 8-byte literals 0xBF1A36E2EB1C432D and 0x3F1A36E2EB1C432D). A NaN determinant passes this gate.
- **kmSegment2 intersection** (0x008F6340..0x008F638A): t and s must each be in [0,1]. NaN gives false.
- **The canonical corners** (0x005025F0..0x00502652; **new**): a function-static vector of 8 corners with components
  ±0.5, in this order: (−,−,+) (+,−,+) (−,−,−) (+,−,−) (−,+,+) (+,+,+) (−,+,−) (+,+,−).
- **GetBoundingQuadXY** (0x00502C32..0x00502D72):
  - scale = size + 2·padding;
  - corner·scale, then R·p through GetTransform(), and push the xy;
  - GetBoundingQuad;
  - **then** add the translation (+0x20, +0x24) to each corner (0x004E68CC loop).
- **GetBoundingQuad<float>** (0x004E6490..0x004E6596; constructor 0x004E7E9C..0x004E7EF4):
  - cv::Point2f copies, with InputArray flags 0x8103000D;
  - cv::minAreaRect, then RotatedRect::points;
  - Quadrilateral(p0,p1,p2,p3), then SortCornersClockwise;
  - no validation.

  minAreaRect and RotatedRect::points are M13-024's port. Don't write a second one.
- **ComputeCentroid** (0x004DF7BE..0x004DF84C; **new**): (c0 + c2 + c1 + c3) × 0.25, summed in that order.
- **SortCornersClockwise** (0x004E7EFC..0x004E7F9E):
  - atan2f(dy, dx) per corner;
  - libc++ `__sort` with CompareFirst (0x004E8260; `__sort4` 0x004E85BE);
  - output slots [P0, P3, P1, P2].

  The tie order is M13-023's.

### SetID, InitPose and the pose broadcast

- **SetID** (0x004EF468..0x004EF528): vt+0x5C. Markerless returns 0 (0x004E02AE), so it tail-calls `ObjectID::Set` on
  obj+0x14; the unique-type cache branch isn't taken.
- **ObjectID::Set** (0x008412D8..0x008412E6): `old = C; C = old+1; id+4 = old`.
  - The counter is `UniqueIDCounter` (GOT 0x0103FE30, .bss 0x0105DFF0), 0 at load.
  - There is no atomic and no wrap guard.
  - `ResetObjectIDCounter` (0x008412C8) has **no caller**, so the counter is never reset (**new**).
  - Cubes share it: ActiveCube's SetID slot (0x0101CE10) relocates to ObservableObject::SetID (0x00623946..0x00623954).
- **The default ID** is −1 (Vision::ObservableObject constructor, 0x0087660A..0x0087660E).
- **InitPose** (0x004EF562..0x004EF57E): vt+0x28 SetPose(pose, −1.0f, state), then +0x50 = 1.
- **SetPose** (0x004EF590..0x004EF644):
  - pose = arg, and +0x24 = state;
  - name = "Object" + "_" + to_string(id) (0x004EF688, 0x004EF690). InitPose (0x00622430) runs before SetID
    (0x00622572), so a new cliff is named **"Object_-1"**, and SetID never renames it;
  - +0x10 = dist, only if dist ≥ 0.
- **BroadcastObjectPoseChanged** (0x00506F88..0x00506FB8): OnObjectPoseChanged(obj, oldPose, oldState) →
  MapComponent::UpdateObjectPose(obj, oldPose, oldState) → veneer 0x008CAD1C → BroadcastObjectPoseStateChanged
  (PLT 0x004A7024).
- **BroadcastObjectPoseStateChanged** (0x00506288..0x005062DA):
  - it returns if state == old;
  - if state ≠ 0: UpdateObjectPose(obj, &curPose, old);
  - if IsActive: CubeLightComponent::OnActiveObjectPoseStateChanged.

  A cliff takes neither branch, and **no message is sent**.
- **UpdateObjectPose for family 6** returns immediately (0x0067EA9A/0x0067EA9C). It is reached twice for a new cliff.
- **OnObjectPoseChanged** (0x00624808..0x006248B8; channel "BlockWorld" 0x00BF82BB). It is gated on byte BlockWorld+0x74.
  When that byte is set:
  - an ID already queued logs MultipleChanges (0x006248EC);
  - old == 0 logs FirstPoseForObject (0x00624924);
  - otherwise it pushes PoseChange(id, oldPose, old).

  On every path it then calls BlockConfigurationManager(+0x94)::SetObjectPoseChanged(id, cur), which is M11-037's.
  **The writer of +0x74 is UNKNOWN** (new RECOVERABLE_GAP).

### Deleting intersecting objects

- **AddLocatedObject → DeleteIntersectingObjects** (0x00622C68..0x00622C84): the filter's allowed-type set (+0x24)
  gets {14}, and +0x6C/+0x6D = 0, i.e. the current origin only. It is called unconditionally after the type==4 error
  gate.
- **The filter layout** (DeleteLocatedObjects 0x0061D0A6..0x0061D45C):
  - IDs: +0x00 ignored, +0x0C allowed;
  - types: +0x18 ignored, +0x24 allowed;
  - families: +0x30 ignored, +0x3C allowed;
  - origins: +0x48 ignored, +0x54 allowed;
  - +0x60: predicates, all of which must be true;
  - +0x6D, the origin mode: 0 = current only, 1 = non-current only, 2 = all, anything else = use the sets.
- **DeleteIntersectingObjects** (0x0062464C..0x00624732): the same callable vtable 0x0102DC3C, then
  DeleteLocatedObjects.
- **DeleteLocatedObjects:**
  - **A null entry:** the warning `BlockWorld.DeleteLocatedObjects.NullObject` (0x0061D6EC) / "Deleting null object in
    origin %d with Family:%s Type:%s ID:%d" (0x0061D718), then erase. This happens **before** any ID or predicate
    filtering (0x0061D402..0x0061D4C4).
  - **A passing entry** (0x0061D1B8..0x0061D3F8, 0x0061D4DC..0x0061D58E):
    - a non-current origin is a plain erase;
    - in the current origin: ClearLocatedObjectHelper → clone (vt+0) → clone.id = id → if the clone IsActive, copy
      +0x40/+0x44 with the NotActive warnings (0x00BF94CD, 0x00BF94FB, "ID: %d" 0x00BF94F4) → push (pose, state,
      clone) → erase;
    - empty type, family and origin maps are erased.
  - **The deferred notifications,** per entry, in order (0x0061D5A6..0x0061D602; 0x00507806):
    1. DeletedObjectInCurrentOrigin (erases the confirmer map +4, PLT 0x004A718C);
    2. BroadcastObjectPoseChanged(clone, &oldPose, oldState);
    3. RobotDeletedLocatedObject{objectID} → Robot::Broadcast (Size 4, 0x007143EC);
    4. the clone is deleted via vt+8.
- **ClearLocatedObjectHelper** (0x00625C78..0x00625EF6). Strings: 0x00625F90, 0x00BF938C, 0x00BF9411, 0x00625FCC,
  0x00BF9444, 0x00BF9478.
  - A null object logs a warning.
  - **Robot+0x2B8 == id:** info LocalizeRobotToNothing (robotID, type, id), then SetLocalizedTo(null).
  - **CarryingComponent(+0x284)+8 == id:** info ClearingCarriedObject (type, id, robotID), then
    UnSetCarryingObjects(false).
  - **BlockWorld+0x60 == id:** info ClearingSelectedObject, then +0x60 = −1.
  - Then FindObjectOnTopOrUnderneathHelper(obj, 15.0f, the default filter, true) (M13-007). A non-null result is
    cleared recursively.
  - Then +0x54 = 1, and +0x58 = (u32)fmax(robot+0x2C, helper vt+0x38()).

## 2. M13-030: the cliff MemoryMap insert

**The cliff data:**
- **MemoryMapData_Cliff constructor** (0x00684984..0x006849A4): +4 = 8 (byte); +8 = +0xC = ts; +0x10/+0x14 = the
  normal's x/y.
- **Clone** (0x006849AC..0x006849D6) copies +4..+0x14.
- **Equals** (0x006849DC..0x00684A2C):
  - null or a differing type byte → 0;
  - both |Δnormal| < 0x35A00000 (strict) → 1;
  - timestamps are ignored.
- **NodeContent** (0x0068C450..0x0068C4E2): the constructor clones the data through vt+8.
  - ==: differing types → 0; the same pointer → 1; otherwise data vt+0xC (Equals).

**The insert:**
- **AddMarkerlessObject's insert** (**new**):
  - the quad is `obj vt+0x50(copy of the input pose argument, 0.0)`: the **input** pose r8 (0x0062238C, unchanged
    through 0x006225DE..0x006225EC), not the object pose;
  - the normal is R_robot·X from Robot::GetPose at insert time (0x006225A8..0x006225C6);
  - the timestamp is robot+0x2C (0x00622390);
  - type 14 goes to MemoryMapData_ProxObstacle instead (0x00622646..0x006226E6), and other types skip the map.
- **The dispatch** (0x006817C6..0x006817E0): MemoryMap::Insert (vtable 0x0103095C, slot 0x0103096C) builds a
  FastPolygon and calls QuadTree::Insert(this+8, poly, data, **1**). AddMarkerlessObject ignores the return
  (0x00622610..0x0062261A).

**The quad tree:** these rows are M11-045's, reused here, not a second implementation.
- **QuadTree::Insert** (0x00685008..0x00685130):
  - **NaN check:** isnanf over **all** points, with no early exit. Any NaN gives sErrorF `QuadTree.Insert.NaNPoly`
    (0x0068516C) / "Poly is not valid, at least one coordinate is NaN." (0x00685184), errG = 1, a conditional break,
    sDumpCallstack("QuadTree::Insert"), and return.
  - **A root that doesn't contain the polygon:**
    - the removal type (type == 5 only, 0x00684978) logs the RemovalPolyNotContained warning (0x006851D8 /
      0x00BFFB9F);
    - anything else calls Expand(poly, 1).
  - Then root(+0x50).Insert(poly, data, proc+8), and the dirty byte +0 |= the result.
- **Expand** (0x00685200..0x0068543A):
  1. Loop: dir = centroid − root center (+0x5C/+0x60); UpgradeRootLevel(dir, 8); recheck Contains; continue while
     upgraded and not contained.
  2. Then, if still not contained and limit > 0: ShiftRoot (return ignored), and recheck, at most `limit` times.
  3. If still not contained: warning `QuadTree.Expand.InsufficientExpansion` (0x006854DC / 0x00BFFBE6), with the
     centroid xy, the root xy and side +0x68 as doubles.
  4. dirty is always set to 1.
- **The UpgradeRootLevel gate and log:**
  - depth (+0x80) == 0xFF → false; depth ≥ 8 → false (0x006876E8..0x006876F6);
  - on success: info, channel "QuadTree" (0x00BFFCD1), `QuadTree.UpdgradeRootLevel` (0x00687AC0) / "Root expanded to
    level %u. Allowing %.2fm" (0x00687ADC), with side × 0.001.
- **The node Insert wrapper** (0x00686CB4..0x00686CE2): NodeContent(data), then Insert_Recursive. *(Codex's label was
  wrong.)*
- **Insert_Recursive** (0x00686CF4..0x00686DDC):
  1. The same data pointer returns 0. Otherwise save the old content.
  2. Overlap = GetOverlapType; 0 means no change.
  3. Otherwise the old data's +0xC = the incoming +0xC (a shared object).
  4. With children and CanOverrideSelfAndChildren: ClearDescendants, then ForceSet.
  5. Otherwise, if the overlap ≠ 2, the node is a leaf and depth ≠ 0: Subdivide.
  6. With children: recurse in order, OR the results, then TryAutoMerge. Otherwise TrySetDetectedContentType, with its
     result **ignored**.
  7. Return childOR | (content != saved).
- **CanOverrideSelfWithContent** (0x00687E78..0x00687E9C): an incoming 8 → true. An existing 8 is overridden only by
  an incoming 2 with overlap 2.
- **CanOverrideSelfAndChildren** (0x00687EF0..0x00687F28): needs overlap 2, self permitted, and every child permitted
  recursively.
- **TrySetDetectedContentType** (0x006880B0..0x006880D2): CanOverrideSelf → ForceSet.
- **ForceSet** (0x00687518..0x006875E4): shares the incoming NodeContent pointer. Type 5 has its own branch. If the
  old type ≠ the new type: OnNodeContentTypeChanged(node, oldType, (old == 0) | hasChildren).
- **Subdivide** (0x00687BEC..0x00687DBA):
  - children (c±q) in the order ++, +−, −+, −−, with q = side/4, side/2 each, depth−1, and z kept;
  - each child is ForceSet to the parent's content;
  - the parent becomes Unknown data (type 0), with first = last = the old content's +0xC (**new**).
- **TryAutoMerge** (0x00687F2A..0x00688094):
  - any grandchild → return;
  - seed min/max from child[3] (+8/+0xC);
  - for i = 0..n−2: fmin/fmax as doubles, converted back to u32, and eq &= child[i] == child[i+1], with no early exit;
  - if all are equal: **write min/max into child[0]'s shared data object**, then ClearDescendants and ForceSet the
    parent with it (**new**).
- **Helper +0x38** (0x00681934..0x0068193A): the root content's last-observed timestamp ([+0xDC]+0xC).
- **BlockWorld+0x58** (0x006226EA..0x0062271E): (u32)fmax((double)robot+0x2C, (double)helper+0x38).

**Not re-checked here, so UNKNOWN** (M11-048):
- GetOverlapType, and FastPolygon's constructor and Contains (0x00688290 and its callees);
- the UpgradeRootLevel body (0x006876FA..0x006879C0);
- ShiftRoot (0x00686DF8..0x0068743E);
- the processor bodies (0x00688E50..0x006890AA, 0x0068928C..0x0068934C);
- ClearDescendants (0x00687E1C);
- SwapChildrenAndContent and DestroyNodes (0x00687600, 0x0068768A).

**The null map helper (new RECOVERABLE_GAP).** GetCurrentMemoryMapHelper (0x0067EA5C..0x0067EA88) returns null when
MapComponent+0x1C == UnknownOriginID (GOT 0x0103E978), or when no map exists. Its result is dereferenced without a
check by:
- AddMarkerlessObject (0x006225FE→0x00622610, 0x006226F4);
- ClearLocatedObjectHelper (0x00625E96).

Whether that state is reachable needs MapComponent's map-creation path. If it is reachable, it needs an operator
ruling.

## Corrections to M4-038's text

- **Step 3:** **every** overlapping object gets the timestamp.
- **Step 7:** the map quad uses the input pose, not obj.GetPose().
- **The angle tolerance:** π/4 is the getter's value, but IsSameAs uses π, so the angle never rejects.
