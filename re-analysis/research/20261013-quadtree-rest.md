| Item / step | Coverage and original behavior | Primary citation | Owner / research disposition |
| --- | --- | --- | --- |
| 1a | Four overlap results; vertex classification, two cell diagonals, edge intersection and final polygon containment, in that order | 0x00688290..0x0068836E; 0x00686890..0x0068691A | M11-048, recovered below |
| 1b | FastPolygon copies vertices, creates ordered edges/normals, computes centroid/bounds, then circle bounds; Contains uses bounds, circles, half-planes | 0x008416F0..0x00841AE8; 0x00841BA0..0x00841C70; 0x0050238A..0x005023D6 | M11-048, recovered below |
| 1c | LineSegment endpoint/orientation tests, including strict epsilon and inclusive unexpanded bounds | 0x00842740..0x008427A2; 0x0084292C..0x00842B64 | M11-048, recovered below |
| 2a | Upgrade limit, direction relative to root, quadrant selection, child construction and reparenting | 0x00685224..0x00685262; 0x006876D8..0x0068799C | M11-048 / M11-045, recovered below |
| 2b | Five executed ForceSets, before doubling root side; cached area changes depend on current child status | 0x00687956..0x006879CA; 0x00687518..0x006875E4; 0x00688E78..0x00688F0E | M11-045, recovered below |
| 3a | Shift direction flags, opposing-side refusal, all-inside error, recentering, leaf shortcut | 0x00686DF8..0x00687000 | M11-048 / M11-045, recovered below |
| 3b | All eight shift cases: **20 direct ForceSet sites**, 12 direct Swap sites; old-node destruction and success log | 0x00687004..0x00687400 | M11-048 / M11-045, recovered below |
| 3c | Area, cached types and border invalidation through ForceSet, Swap and DestroyNodes | 0x00687600..0x006876D2; 0x00688E50..0x006890A6; 0x0068928C..0x0068934C | M11-045, recovered below |
| 4 | Transform entry is **0x00688378**; 0x0068846E is its ForceSet call. Children precede merge and own-content callback; only a changed leaf is ForceSet | 0x00688378..0x00688474; 0x0068147C..0x00681526 | M11-046, recovered below |
| 5a | MemoryMap merge wrapper, no null-source guard; depth-first source leaves, skip Unknown, transform then insert each retained leaf | 0x006813A8..0x006813AC; 0x00685504..0x00685720; 0x00688250..0x0068828C | M11-048 / M13-032, recovered below |
| 5b | Merge's shift budget is signed conversion of ceilf(distance / 1280.0f); 2D projection uses quaternion-derived yaw, not an arbitrary planar rotation | 0x0068552A..0x006855F2; 0x00846594..0x00846610; 0x0084AA1C..0x0084AAB8 | Recovered calling contract; shared pose/math interfaces explicitly bounded below |
| 5c | Exact degenerate corner-sort tie behavior and Radians constructor behavior are not re-extracted here | 0x004E7F76 calls PLT 0x004A49E4; 0x0084AAB4 calls PLT 0x004A4294 | RECOVERABLE_GAP at these named interfaces; no claim of complete merge arithmetic for every malformed/degenerate pose |
| 6a | Eleven-pair mask fold, two-point FastPolygon and node ray recursion | 0x0068176E..0x006817A4; 0x00689D80..0x00689DBA; 0x00689F0A..0x00689F98 | M11-046 machinery; M14-007 and M11-017 caller inputs |
| 6b | HasContentType queries the processor's cached type-node set, not arbitrary polygons or a fresh tree traversal | 0x006817A8..0x006817AA; 0x0068A7E0..0x0068A800; cache mask 0x00688F12..0x00688F46 | M11-046 query; M11-045 cache producer |
| 6c | Face ray and overhead ray masks/calls checked; border walk is a distinct interface, not part of the ray test | 0x005C2420..0x005C24A4; 0x0067FC6E..0x0067FCAA; 0x0068039A..0x006803AA | M14-007 / M11-017; border algorithm remains M11-047 |

# P1 — quadtree remainder

Answers `re-analysis/research/requests/20261013-quadtree-rest.md`. Research only. No manifest, inventory, production code or tests changed. Read the local ARMv7 engine with Capstone detail and skipdata enabled; Ghidra was used only to find functions. Binary SHA-256: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Vtable entries below were resolved through ELF relocations, not interpreted as zero when the on-disk slot was zero. Thumb-to-ARM veneers were checked in ARM mode.

The coverage table is a research result, not a fidelity promotion. Every requested item has a disposition. The remaining shared-math boundaries in item 5 are explicit; this report does not turn a bounded read into a claim that the entire map subsystem is settled.

## Numeric notation

`F(expr)` means one binary32 instruction/result, with no reassociation or fused multiply-add. `D(expr)` means binary64. `e` is binary32 bits `0x3727C5AC`; `-e` is `0xB727C5AC`. Comparisons below describe ordered finite operands unless a NaN branch is stated. An ARM floating compare unordered result has N=0, Z=0, C=1, V=1: `MI`, `GT`, `GE`, `LS` are false; `HI`, `LT`, `PL` are true. This matters where otherwise similar source-looking comparisons compile differently.

Node fields: children vector +0/+4/+8; center XYZ +0xC/+0x10/+0x14; side +0x18; xmin/ymin/xmax/ymax +0x1C/+0x20/+0x24/+0x28; two 40-byte line segments +0x2C/+0x54; parent +0x7C; level byte +0x80; content shared pointer +0x84/+0x88.

## 1. Overlap, FastPolygon and segment intersection

### GetOverlapType

At 0x0068829A..0x00688324 walk vertices in stored order. For each, test xmin then xmax then ymin then ymax, using separately rounded bounds `F(xmin + -e)`, `F(xmax + e)`, `F(ymin + -e)`, `F(ymax + e)`. Equality is inside. NaN coordinates fail this node containment path. Track `allInside` (initial true) and `anyInside` (initial false). Return 1 immediately once both an inside and outside vertex have occurred.

After the loop:

- **3:** all vertices inside the node. An empty vertex vector also returns 3 from this function, without dereferencing vertices.
- **0:** no vertex inside and fewer than two vertices.
- Otherwise test each of the node's **two diagonals**, in storage order, against every polygon edge in storage order. The node segments are `(xmin,ymin)->(xmax,ymax)` and `(xmax,ymin)->(xmin,ymax)`, constructed at 0x006868CC..0x00686912. Any intersection returns **1** (0x00688334..0x0068835A).
- If none intersects, call FastPolygon.Contains on `(xmin,ymin)` (node+0x1C, 0x0068835C..0x00688362). True returns **2**, false **0**.

Thus 2 is the node-inside-polygon result; 3 is polygon-inside-node, and 1 is partial/intersecting. This is not four rectangle-edge tests. Empty GetOverlapType's return must not be confused with safe construction of an empty FastPolygon.

### FastPolygon construction

0x008416F4 copies the vertex vector. It clears center and the two derived vectors, then calls CreateEdgeVectors, ComputeCenter, ComputeCircles, exactly in that order (0x0084170A..0x00841718). It does **not** call SortEdgeVectors.

CreateEdgeVectors clears both derived vectors. For `n < 2` it returns. Otherwise for i=0..n-1, j=(i+1)%n:

1. `dx=F(p[j].x-p[i].x)`, `dy=F(p[j].y-p[i].y)` (0x008417D0..0x00841802).
2. `len=sqrt32(F(F(dx*dx)+F(dy*dy)))`. Hardware VSQRT is used; a NaN result calls imported sqrtf (0x00841804..0x0084182E).
3. `inv=F(1/len)`; append LineSegment(p[i],p[j]); append `(normal=(-F(inv*dy),F(inv*dx)), index=i)` (0x00841832..0x00841896). The negative multiply is VNMUL, not a reassociated division. There is no zero-length-edge guard.

ComputeCenter calls Polygon.ComputeCentroid (0x0050238A..0x005023D6): start each component at +0; for nonempty input compute `inv=F(1/F(uint32(n)))` once, then for every vertex in order do `center[k]=F(center[k]+F(inv*p[i][k]))`. It is not sum-then-divide. Empty input returns zero center. Four separate bounds scans follow: xmin and ymin start at largest finite f32 `0x7F7FFFFF`, xmax/ymax at `0xFF7FFFFF`; update min on MI and max on GT. NaN vertices do not update these bounds (0x008418BA..0x00841970).

ComputeCircles starts circumscribed squared radius C=0 and inscribed squared radius I=largest finite f32. For n>=3, in vertex order:

1. `dx=F(vertex.x-center.x)`, `dy=F(vertex.y-center.y)`; `dist2=F(D(D(dx)*D(dx))+D(D(dy)*D(dy)))` (the subtraction is f32, squares and addition f64, narrowing once). Replace C if C<dist2 by MI (0x008419B0..0x008419F8).
2. For that edge's stored index, `a=F(center.x-vertex[index].x)`, `b=F(center.y-vertex[index].y)`; `dot=F(F(normal.x*a)+F(normal.y*b))`; `edge2=F(dot*dot)`; replace I on ordered I>edge2 (0x008419FC..0x00841A60).
3. If the final BLE is not taken (ordered I>C), CoreTechPrint receives `ERROR: inscribed radius of %f is >= circumscribed radius of %f\n`, with I then C widened to double (0x00841A68..0x00841A84). Equality and unordered do **not** print despite the text.

For n=1 or 2, choose vertex 0 for n=1 and vertex 1 otherwise; subtract vertex 0; C is the same f32-subtraction/f64-square-and-sum/narrow result, and I=0. The n=2 C is the **full segment length squared**, not quarter length squared. For n=0 this same branch reads vertex 1 from the empty buffer (0x00841A8A..0x00841A9C): undefined read, covered by M1-034, smallest lost unit is that polygon operation. No empty-constructor semantics may be inferred from GetOverlapType's empty-vector return.

### Contains

0x00841BA0..0x00841C68 first checks exact xmin/xmax/ymin/ymax, without epsilon. The min tests branch GT and max tests MI. Then `dx=F(x-center.x)`, `dy=F(y-center.y)`, `r2=F(F(dy*dy)+F(dx*dx))`. If r2>C by GT, false; if r2<I by MI, true. Otherwise visit the stored normals in order and compute `F(F(nx*F(x-vx))+F(ny*F(y-vy)))`. Continue on LE (including equality **and unordered**, since N!=V); otherwise false. Exhaustion, including an empty normals vector, returns true. NaN dot products therefore do not reject the point; nor do unordered bounds/circle comparisons using GT/MI. A valid clockwise convex polygon has nonpositive interior dot products. This is the actual early-return algorithm, not a winding-number substitute, and circles can short-circuit before winding is tested.

### LineSegment::IntersectsWith

Constructor 0x00842740..0x008427A2 stores both endpoints; obtains xmin/xmax/ymin/ymax by imported fminf/fmaxf in that order; then stores `dx=F(bx-ax)`, `dy=F(by-ay)`.

Define, in this instruction order, `orient(segment,p) = F(F(segment.dy*F(px-ax))-F(segment.dx*F(py-ay)))`. The code computes original t, compares t with zero, conditionally negates on MI for abs(t), separately retains the comparison t versus -e, and classifies:

- 0 if abs(t)<e (strict MI);
- otherwise 1 if original t>=-e (GE);
- otherwise 2. NaN becomes 2 because both successful conditions fail.

Test B.a against A (0x0084292C..0x008429C6); if orientation=0 and B.a is within A's **unexpanded** inclusive bounds, true. Then B.b against A (0x008429CA..0x00842A48), same special return. If the two classifications equal, false (0x00842A4C..0x00842A52). Then A.a against B (0x00842A54..0x00842AD6), then A.b against B (0x00842AD8..0x00842B56), each with the same on-segment special return. Finally return whether these last two classifications differ (0x00842B58..0x00842B64). Bound tests reject unordered using HI/LT/GE. There is no generic line-distance tolerance or expanded endpoint box.

## 2. UpgradeRootLevel and area

0x006876E8..0x006876F6 returns false without mutation when level==255 or unsigned level>=max. Expand passes max=8 and **centroid minus current root XY**, independently rounded f32 per component (0x00685224..0x00685262); it recomputes this after each successful growth that still fails containment.

Let old side be S, old level L, old content data D, T=D.last(+0xC), old children O, and input direction `(dx,dy)`:

1. Move O out, leaving the root child vector empty (0x006876FA..0x0068771A).
2. Construct Unknown/type0 data with first=last=T. Let hp=F(S*0.5), hn=F(S*-0.5). New x=`F(oldx + (dx>=-e ? hp : hn))`; new y=`F((dy>=-e ? hp : hn)+oldy)`, with the exact GE decisions shown at 0x00687726..0x00687780. Unordered takes negative displacement.
3. Construct four Unknown children, side S, level L, parent root, z copied. Their XY offsets from the new center are Q0=(+hp,+hp), Q1=(+hp,-hp), Q2=(-hp,+hp), Q3=(-hp,-hp), in order (0x00687784..0x006878CE). Each is independently constructed from the Unknown data.
4. Select child q to receive the old root: dx>=-e/dy>=-e -> Q3; dx>=-e/dy<-e -> Q2; dx<-e/dy>=-e -> Q1; both below -> Q0 (0x006878D2..0x00687924). These are directional comparisons, not comparisons of the absolute centroid with zero. For NaN, LT is true and GE false; the instruction sequence selects the corresponding below-side cases.
5. Construct a separate shared Unknown NodeContent(first=last=T). ForceSet each child other than q in increasing index order (one static call site 0x00687962, three executions).
6. Reparent every old immediate child to q, transfer O into q (0x00687974..0x0068799C), ForceSet(q,D) at 0x006879A4, then ForceSet(root,Unknown) at 0x006879AE.
7. Only **after** those callbacks: side=`F(S+S)`, level=uint8(L+1), then ResetBoundingBox (0x006879B2..0x006879CA). Return true after the log and temporary cleanup.

Log: sChanneledInfoF channel `QuadTree`, event **`QuadTree.UpdgradeRootLevel`** (shipped spelling), format `Root expanded to level %u. Allowing %.2fm`; size argument is `double(F(newSide * f32(0x3A83126F)))` (0x006879CE..0x006879FA).

Area is the cached callback result, not a recomputed geometric union. Define `A(S)=double(F(F(S*f32(0x3A83126F))*F(S*f32(0x3A83126F))))`. ForceSet snapshots `oldExcluded=(oldType==0 || hasChildren)`, replaces the shared content, and calls OnNodeContentTypeChanged only if type changes (0x00687530..0x006875D6). The callback computes `newExcluded=(newType==0 || hasChildren)`; excluded->included adds A(S), included->excluded subtracts A(S), otherwise no explored-area change. Type9 departure subtracts A(S) from interesting-edge area, arrival adds A(S), independently of the explored gate (0x00688E78..0x00688F0E).

Consequently a nonzero **leaf** old root moved into q gains A(oldS) on q. The root already has children when ForceSet(root,Unknown) snapshots it, so its old explored credit is **not subtracted**. Growth can retain the root's previous credit in addition to q's new credit. Do not normalize this away. For type9, q adds then root subtracts the same old-side amount in that order. For an already subdivided Unknown root, assigning Unknown generally causes no callbacks; descendant credits remain where they were. The three Unknown->Unknown ForceSets do not independently add Unknown nodes to the cache.

## 3. ShiftRoot, all branches

Compute h=F(S*0.5). Scan vertices in order, accumulating four Boolean flags (short-circuit each already-true flag):

- px if `F(F(h+cx)+-e) <= x` (LS), nx if `x <= F(F(cx-h)+e)` (GE);
- py if `F(F(h+cy)+-e) <= y` (LS), ny if `y <= F(F(cy-h)+e)` (GE).

The boundaries count as needing a shift. NaN comparisons set none of the flags. Opposing x flags take precedence and warn `QuadTreeNode.ShiftRoot.CantShiftPMx`; otherwise opposing y flags warn `QuadTreeNode.ShiftRoot.CantShiftPMy`. Both use `Current root size can't accomodate given points` (shipped spelling), no arguments, and return false without changing the tree (0x00686ECA..0x00686F30). Call sites are 0x00686EE0 and 0x00686F08.

If no flags, including empty polygon, sErrorF at 0x00686F58 emits `QuadTreeNode.ShiftRoot.AllPointsIn`, `We don't need to shift`; sets `_errG=1`; calls sDebugBreakOnError only when `_errBreakOnError` is nonzero; returns false (0x00686F32..0x00686F9A).

Otherwise x displacement is +h for px, -h for nx, +0 for neither; y analogously. Write `cx=F(displacementX+cx)`, then `cy=F(displacementY+cy)`, ResetBoundingBox (0x00686FAA..0x00686FF6). Side and level do not change. **A leaf stops here**, with its existing content/area untouched, and emits the success log; it does not subdivide or clear content (0x00686FFA..0x00687000).

For a non-leaf, move out O and construct four Unknown children with side h, level uint8(L-1), z unchanged, centered at new center +/-q, q=F(h*0.5), same quadrant ordering as above (0x00687004..0x006871BA). First/last stamps are both the root's current last stamp. Construct the shared Unknown U for the ForceSets (0x006871BC..0x006871D4).

In this list `Fi@a` means ForceSet(new[i],U) at address a; `Si<-j@a` means SwapChildrenAndContent(new[i],old[j],processor) at a. Listed order is execution order, including interleaving:

- +x,+y: F0@6871F2, F1@6871FE, F2@68720A, S3<-0@687218.
- +x,-y: F0@6872D6, F1@6872E2, S2<-1@6872F0, F3@6872FC.
- -x,+y: F0@687266, S1<-2@687274, F2@687280, F3@68728C.
- -x,-y: S0<-3@68733A, F1@687346, F2@687352, F3@68735E.
- +x only: F0@687230, F1@68723C, S2<-0@68724A, S3<-1@687258.
- -x only: S0<-2@687308, S1<-3@687316, F2@687322, F3@68732E.
- +y only: F0@6872A4, S1<-0@6872B2, F2@6872BE, S3<-2@6872CC.
- -y only: S0<-1@68736A, F1@687376, S2<-3@687384, F3@687390.

Addresses above are hexadecimal native VAs with omitted leading `0x00`. There are **20**, not 19, distinct direct ForceSet sites (12 diagonal + 8 axial). Swap itself executes two further ForceSets at 0x00687660 and 0x0068766A; these are not in the direct-site count.

Swap first exchanges child vectors, reparents their immediate children, snapshots new[i]'s old content, ForceSets new[i] from old[j], then ForceSets old[j] from the snapshot (0x00687600..0x00687678). Coordinates and side are not swapped. Each retained subtree therefore stays at the physical location represented by the new child.

After the chosen case, DestroyNodes(O,processor) at 0x00687398 recursively destroys all entries in order. It recurses before OnNodeDestroyed, then nulls the owning pointer, releases content, frees the node/vector (0x0068768A..0x006876D2). A formerly nonempty vector still has its size when its parent is reported destroyed, so the parent is not treated as a leaf merely because descendant pointers have been nulled.

Area effects use exactly A(S) and the ForceSet rule above. Swapping a retained known leaf normally adds its new leaf credit then subtracts its old leaf credit; a retained Unknown internal subtree moves without charging its unchanged descendants. Destroyed nonzero leaves subtract their credits; a node of type9 subtracts its interesting-edge credit even if non-leaf (0x006892D2..0x00689340). Root content is never ForceSet in ShiftRoot. Root size/area is not recomputed. Floating additions and subtractions must retain this call order; a net geometric formula would lose rounding and stale-credit behavior.

Success log (0x006873AA..0x006873D8): channel `QuadTree`, event `QuadTree.ShiftRoot`, format `Root level is still %u, root shifted. Allowing %.2fm`, with uint level and double(F(S*0x3A83126F)). Return true at 0x006873FE. Exception landing pads clean up temporaries and resume unwinding; they are not an alternative successful shift.

## 4. Transform

The request's 0x0068846E identifies a call **inside** Transform. Entry is 0x00688378.

1. GetOverlapType; zero -> false without children or callback.
2. For each child in stored order, copy the function, recursively Transform(child,polygon,function,processor), destroy the copy; **ignore the child's Boolean result** (0x0068839A..0x006883D2).
3. If there were children, TryAutoMerge once (0x006883D4..0x006883D8).
4. Reload own content. Null -> false. Otherwise call the function with a shared copy of the content, wrap its returned shared pointer as NodeContent (0x006883DC..0x00688424).
5. Compare using NodeContent `!=`, not pointer inequality alone. If changed **and currently a leaf**, ForceSet and return true. Otherwise false (0x00688424..0x00688474). It is possible to change descendants and return false. The callback runs on non-leaf own content too, although that returned replacement is not assigned.

There is no subdivision, containment requirement beyond nonzero overlap, or override-table check. An overlapping leaf is treated whole. Area changes occur through child transformations, TryAutoMerge, and the final ForceSet in that order. MemoryMap's polygon wrapper constructs FastPolygon and invokes the root at map+0x58 with processor map+0x10 (0x00681498..0x00681522); it discards the return value.

NodeContent equality first compares type, then pointer identity, then virtual Equals at data vt+0xC (0x0068C4BE..0x0068C4E2). It dereferences both data pointers to fetch type before later null checks. Thus a callback returning a null data pointer is not a supported 'delete' result; the equality path has an invalid read. Empty std::function throws bad_function_call (0x006884BC..0x006884EA). These are M1-034 terminating/undefined boundaries, not defined map transforms.

## 5. Merge and null source

The relocated slot at 0x01030970 (MemoryMap vtable address point 0x01030964 +0xC) is MemoryMap::Merge, entry 0x006813A8. It adds 8 to destination and source and tail-calls QuadTree::Merge through veneer 0x008CD74C -> ARM stub -> PLT 0x004BCA50. There is **no null-source branch, no cast check, no empty-map fallback**.

With source=null the wrapper passes 8. After constructing Pose2d, QuadTree::Merge invokes AddSmallestDescendantsDepthFirst on source+0x50 = address **0x58** (0x00685520..0x00685526). That function's first `ldrd` at **0x00688254** reads address 0x58. For a valid transform, this is the first identified null-source invalid read. It is covered by standing M1-034: skip the merge as the smallest lost operation, keep destination state, visibly name M1-034 and the owning M13-032/M11-048 record. This report does not assert that ordinary localization can produce null oldMap; the wrapper's behavior answers that separate input question.

Valid-source order:

1. Construct temporary Pose2d from the given Pose3d: quaternion GetAngleAroundZaxis plus XY translation, no parent copied (0x00846594..0x00846610).
2. Snapshot pointers to every source leaf by child-order depth-first traversal. Internal nodes are omitted; a leaf root is included (0x00688250..0x0068828C).
3. Transform source root XYZ center by the supplied full 3D rotation and translation. Subtract destination root XYZ center with f32 subtraction (0x0068552A..0x00685584). Accumulate x*x, then y*y, then z*z with separate f32 multiplies/adds; VSQRT, fallback sqrtf on NaN; `budget=VCVT.S32.F32(ceilf(F(distance/1280.0f)))` (0x00685586..0x006855F2). It is computed once, before inserting any leaf. Do not silently force it to one; coincident centers give zero.
4. For each snapshot leaf, read current content type. Skip type0, otherwise MakeQuadXY(0), ApplyTo with the temporary 2D transform, ImportQuad2d, FastPolygon construction, then destination QuadTree::Insert with this leaf's content data and the computed budget (0x006855F4..0x00685648). Insert's return is ignored. Free the per-leaf temporaries, continue, then free the leaf vector and temporary Pose2d. The source tree is not cleared by this routine.

MakeQuadXY computes `h=F(F(side*0.5)+padding)`; for padding=0 corners are indexed Q0=(cx+h,cy+h), Q1=(cx-h,cy+h), Q2=(cx+h,cy-h), Q3=(cx-h,cy-h), each addition/subtraction f32 (0x00686AFC..0x00686B5C). ApplyTo visits indices **0,2,1,3**, writing their same indices (0x00686560..0x006865BA). Each point uses a freshly constructed RotationMatrix2d and computes `x'=F(tx+F(F(x*c)+F(y*(-s))))`, `y'=F(ty+F(F(x*s)+F(y*c)))` (0x0084B8A6..0x0084B928). RotationMatrix2d calls cosf then sinf, stores [c,-s;s,c] (0x0084A1D0..0x0084A1E6); the constructor's preliminary identity Renormalize output is overwritten by these four stores.

ImportQuad2d calls SortCornersClockwise, then appends its corners in order **0,1,3,2** (0x0050096C..0x00500A02). Sort computes four centroid-relative f32 atan2f angles, sorts angle/index pairs, and rearranges corners (0x004E7EFC..0x004E7F9C). Its library sort's comparator/tie behavior and the Quadrilateral centroid's exact accumulation have not been independently completed in this pass: those are recoverable shared geometry interfaces, not permission to use an arbitrary corner sort for degenerate quads.

Additional numeric boundaries checked: Rotation3d's point multiplication narrows each of the four stored double quaternion components to float before UnitQuaternion<float>::operator* (0x0084ACA6..0x0084ACE2); the latter's products and ordered sums are all f32 (0x00849C1C..0x00849CF8). Yaw takes w,x,y,z as f64; computes a=F(2*(x*y-z*w)), b=F(1-2*(x*x+z*z)), c=F(2*(x*y+z*w)), d=F(1-2*(y*y+z*z)), with the inner operations f64 as at 0x0084AA24..0x0084AA70; compare F(F(c*c)+F(d*d)) to F(F(a*a)+F(b*b)). If first is greater, atan2f(c,d); otherwise atan2f(-a,b). Unordered takes the second branch because BLE is true (N!=V). The returned angle passes through Radians(float) (0x0084AA74..0x0084AAB4). Its normalization is a named shared-pose dependency here, not newly recovered by this report. Imported phone libm rounding is external; no shipped map decision is excused as equivalent DSP.

## 6. Current queries and ownership

MemoryMap.HasCollisionRayWithTypes iterates exactly 11 two-byte (enum,bool) pairs, includes an enum whenever its bool byte is nonzero, ORs EContentTypeToFlag(enum), then tail-calls processor map+0x10 (0x0068176E..0x006817A4). EContentTypeToFlag is `1 << enum` at 0x00684970, using ARM register shift semantics; valid enum range is 0..10.

Processor constructs a two-vertex polygon `[from,to]`, constructs FastPolygon, retrieves its root at +0x24, and recurses (0x00689D80..0x00689DBA). Recursion:

1. `hitType=mask & flag(node.content.type)`.
2. If hitType==0 and no children, false **without testing overlap**.
3. Else compute overlap. If hitType!=0, return overlap!=0 immediately, even for an internal node.
4. If overlap==0, false. Otherwise recurse children in order, short-circuit on true; exhaustion false (0x00689F30..0x00689F98).

In particular, Unknown/type0 on a subdivided parent blocks any intersecting ray when bit0 is included; it does not require an Unknown leaf. Two equal ray endpoints are not rejected by the wrapper: the FastPolygon zero-length normal arithmetic and segment predicate described above still execute.

HasContentType adjusts this to processor at +0x10 (0x006817A8), finds the enum key, returns false for missing key and otherwise whether its node set size is nonzero (+0x18 in the hash node, 0x0068A7E0..0x0068A800). Cache maintenance only tracks mask **0x7C8 = types {3,6,7,8,9,10}** (0x00688F12..0x00688F46). Therefore this is not a general 'does any leaf have this type' query: types 0,1,2,4,5 do not populate this cache through the checked callback. Type changes remove old cached membership, insert new membership, then invalidate borders; destruction removes membership and invalidates too. Same-type ForceSet calls do not trigger this machinery.

Caller inputs checked directly:

- **M14-007:** CanDriveIdealDistanceForward gets the current map, forms robot translation -> robot pose applied to (40,0,0), invokes map vt+0x30 at 0x005C24A2, and XORs result with 1. Table 0x00C67962 includes types **3,4,6,7,8,9,10**. TransitionToDrivingForward selects -15.0f unless the check is true, then 40.0f, with 40.0f speed (0x005C254E..0x005C2590).
- **M11-017:** overhead calls at 0x0067FC80 and 0x0067FCAA both reach the same vt+0x30. The first table 0x00C8768B includes **3,4,5,6,7,8,9,10**; the second 0x00C876A1 includes **3,4,5,6,7**. Preserve the caller's ray splitting and short circuit around these calls; map query semantics are the recursion above. The later vt+0x4C call at 0x006803AA is FillBorderInternal(type9, table0x00C87675, type10), whose table includes **3,4,5,6,7,10**. It is the separate M11-047 border algorithm, not an alternative ray query.
- **M11-046:** owns ray recursion and polygon Transform; HasContentType's query contract fits this query owner, with its producer/cache bookkeeping in M11-045. M11-017 owns overhead producer inputs and their read order, and M14-007 owns the face behavior's geometry/mask/action decision. None can rely on the C# polygon list as evidence of native query behavior.

The current C# `MemoryMap.HasContentType` and `HasCollisionRayWithTypes` still inspect `_regions` (MemoryMap.cs lines 122 and 182), and FaceBehaviors.cs line 275 calls the latter. These reads establish why the query contract is needed; they are not primary evidence. LatticePlanner's FindContentIf stand-in is another explicitly marked interface; no claim that this report recovers every planner/map query or the border walker.

## Current records, contradictions and limits

Current manifest was read during this request:

- **M11-048**, `Unread quad-tree, OpenCV-numeric and vision-handler bodies`, **RECOVERABLE_GAP**. Evidence currently cites the 20260929 gap2 extraction's “not done” and verify “Open”/“Still open”. This report supplies primary reads for the six requested quadtree items; it does not cover all OpenCV/vision-handler obligations in that broad record.
- **M11-045**, `The memory map's quad tree: Insert, Expand, override rules, subdivision and merging`, **IMPLEMENTATION_GAP**. Evidence cites `0x00685008 QuadTree::Insert; 0x00684C28..0x00684C48 root; 0x00687E78..0x00687EEE CanOverrideSelfWithContent; 0x00687F56..0x0068805C TryAutoMerge ...; 0x006878D2..0x0068791E quadrant map`. Its authority's shorthand quadrant comparisons need the actual **-e** threshold and relative direction explained above. Its current unresolved bookkeeping/integration warning remains justified.
- **M11-046**, `The quad tree's polygon Transform and the collision ray walk`, **IMPLEMENTATION_GAP**. Evidence `0x0068147C..0x006814F4, 0x00688378..0x00688476 Transform; 0x00689D80..0x00689F5C the ray walk`. Clarify that callback output is assigned only on a leaf, and the return does not OR descendant changes. The core whole-leaf and Unknown-parent claims hold in the checked instructions.
- **M11-017**, `The memory map's vision-derived content: the overhead edges`, **IMPLEMENTATION_GAP**. Evidence still says the polygon-vs-tree difference is “neither observable in what the map answers.” The checked ray predicate, quantized cell overlap and cached HasContentType contradict that sentence. Its status is already a gap; no new downgrade is claimed.
- **M14-007**, `The memory map, and the question the face behaviour asks it before driving in`, **IMPLEMENTATION_GAP**. Evidence still contains “answers the same ray exactly rather than to the tree's precision”, while unresolved explicitly repudiates that claim. The new read supports the existing unresolved correction, not a claim against an older EXACT_SOURCE revision.
- **M13-032**, `MapComponent memory-map lifecycle: constructor (+0x1C Unknown, no map), CreateLocalizedMemoryMap on delocalize (zombie/keep-alive events, create, +0x1C), UpdateMapOrigins on re-localize (NullMapFound, merge, +0x1C, erase old)`, **IMPLEMENTATION_GAP**. Evidence names merge vt+0xC at 0x0067DF80 and the blockworld-flag-and-map-helper report. Null source handling is now established as an invalid read, not a missing benign case. The current Robot destructor and caller/lifecycle residuals are not resolved here.

**Remaining uncertainty:** corner-sort ties/degenerate quad centroid and Radians normalization at the named merge math interfaces remain recoverable, not BLOCKED_EXTERNAL; full nonfinite/degenerate merge behavior is consequently not claimed closed. Native out-of-memory/unwind and arbitrary aliasing of source and destination were not exercised. No hardware run, emulator oracle or regression test was run, and no manifest status is raised. Proposed evidence-derived regression cases for the manager are the four overlap codes, boundary +/-e, all eight shift lists and 20-site count, leaf-shift content retention, old-leaf growth area-credit retention, Transform returning false after changing children, coincident-center merge budget zero, null merge's 0x58 read, cached type4 absence versus type3 presence, and a bit0 ray intersecting an internal Unknown parent. These are test specifications from instructions, not tests inferred from candidate C#.
