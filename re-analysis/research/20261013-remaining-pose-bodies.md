# R-POSE-REST — remaining pose arithmetic and wrapper bodies

Answers `requests/20261013-remaining-pose-bodies.md`. Independent extraction of shipped ARMv7 `libcozmoEngine.so` 3.4.0-1204. Tables for all seven items precede detailed results. Addresses below are ELF virtual addresses. `f32(expr)` and `f64(expr)` mean a separate operation at that width, not one rounding after a whole expression. System libm calls are the existing unshipped-system-library boundary; surrounding arithmetic is shipped code.

## Coverage 1 — quaternion<double> × Point3d

| Step | Width and behavior | Address |
|---|---|---|
| Load and products | w,x,y,z and point are f64; squares and pair products separately computed. No normalization. | `0x00849F90..0x00849FC4` |
| Matrix terms | Products doubled by addition, diagonal terms evaluated with successive add/subtract. | `0x00849FC8..0x0084A008` |
| Point sums/store | f64 products and sums in the exact associations stated in §1; output three doubles. No logs. | `0x0084A00C..0x0084A060` |

## Coverage 2 — Euler/matrix angle functions

| Step | Width and behavior | Address |
|---|---|---|
| Euler constructor setup | Initializes identity, Renormalize(identity), then overwrites all nine entries from three Radians. No final Renormalize. | `0x0084AF98..0x0084AFD4`, `0x0084B078..0x0084B0A2` |
| Trig/order | cosf/sinf of second, first, third angles, in that order; all returned values/products/sums f32. | `0x0084AFD8..0x0084B074` |
| Euler singularity | Tests abs(abs(M20)−1)<1e-5f; singular branch sets z=0, y=±π/2 and x from atan2f. Returns true. | `0x0084B1B8..0x0084B23A`, `0x0084B314..0x0084B360` |
| Euler regular/second solution | y=−asinf(M20), reciprocal cosf(y), x and z by atan2f; optional alternate trio is gated only by alternate-x pointer. Returns false. | `0x0084B23C..0x0084B310` |
| X angle | Compare M11²+M21² against M12²+M22²; choose atan2f(M21,M11) or atan2f(−M12,M22). | `0x0084B370..0x0084B3C8` |
| Y angle | Compare M20²+M00² against M02²+M22²; choose atan2f(−M20,M00) or atan2f(M02,M22). | `0x0084B3CA..0x0084B422` |
| Radians wrapping | Float assignment/constructor rescales; copying preserves Radians value/flag. No direct DAS logs in these bodies. | Call sites above; Radians arithmetic `0x0084C9E8..0x0084CA16`; existing rescale extraction `0x0084C87C..0x0084C936` |

## Coverage 3 — IsValid, initializer_list and Abs

| Step | Exact operation | Address |
|---|---|---|
| IsValid | For columns 0,1,2: f32 sum (v0²+v1²)+v2², f32 sqrt, abs(norm−1)<tolerance. Stops on first failure. No orthogonality/determinant check. | `0x008497D0..0x00849868` |
| sqrt fallback | Hardware vsqrt first; only unordered result calls imported sqrtf. NaN comparison rejects. | `0x00849812..0x00849828` |
| GetColumn support | Valid column indexes return three elements; index≥3 throws CoreTechException. IsValid supplies only 0..2. | `0x0084986C..0x008498CE` |
| initializer_list | Zeroes destination, reads exactly nine floats from list pointer without consulting size, copies them, then Renormalize. | `0x00849900..0x00849936` |
| Abs | In-place row-major element loop, VABS.f32, returns this; no Renormalize and no logs. | `0x0084B51E..0x0084B55C` |

## Coverage 4 — Transform3d::ApplyTo<float>(vector)

| Step | Exact operation | Address |
|---|---|---|
| Select branch | Compare input/output point counts, not capacity. Equal: overwrite output; different: clear output then append. | `0x00656722..0x00656750`, `0x006567AC..0x006567D2` |
| Each point | Rotation3d×Point3f (quaternion narrowed first, no normalization), then translation[i]+rotated[i], each add f32 in x,y,z order. | `0x00656760..0x0065678A`, `0x006567DA..0x006567FE`; rotation dependency `0x0084ACA6..0x0084ACE2`, `0x00849C1C..0x00849CF8` |
| Equal count | Computes local point before writing all three words; supports input==output. | `0x0065678C..0x006567A8` |
| Different count | Retains allocation when clearing; walks input in 12-byte steps; append or emplace slow path. Empty input produces empty output. | `0x006567AC..0x00656828` |
| Exit/logs | Void; no success/failure log or validation of nonfinite coordinates. | `0x0065682A..0x0065682E` |

## Coverage 5 — Pose2d/Transform2d family

| Operation | Exact behavior, all numeric arithmetic f32 | Address |
|---|---|---|
| RotationMatrix2d helper | Identity + Renormalize first; then cosf(angle), sinf(angle), matrix [c,−s;s,c]. | `0x0084A19C..0x0084A1EE` |
| TranslateForward | x←x+cosf(a)*d, then y←y+sinf(a)*d. | **Even entry** `0x0084B618..0x0084B65C` (request's 0x84B619 is Thumb symbol bit) |
| *= | Add/rescale angle first; rotates other translation using **new summed angle**, then adds own translation. | `0x0084B65E..0x0084B6E4` |
| Transform product | Compute summed angle; rotate other's translation using original left angle; add left translation; construct result. | `0x0084B6E6..0x0084B788` |
| PreComposeWith | Add/rescale angle; rotate own translation using argument's angle; store rotated vector then add argument translation. | `0x0084B78C..0x0084B810` |
| GetInverse | Copies angle and translation then calls Invert. | `0x0084B812..0x0084B826` |
| Invert | angle*=−1 through Radians; negate both translation coordinates; rotate them by inverted angle. | `0x0084B828..0x0084B8A4` |
| Point product | Rotate point by stored angle, then translation+rotated coordinate. | `0x0084B8A6..0x0084B928` |
| Pose2d constructors | Default, angle/point, Transform2d, angle/x/y, Pose3d variants checked; all allocate their PoseBase node through constructor. | `0x008463D8..0x0084660C` |
| Direct logs | None in transform operations or these constructors. Renormalize helper has its separate existing log behavior. | Bodies above |

## Coverage 6 — GetParent / FindRoot ownership

| Step | Behavior | Address |
|---|---|---|
| GetParent result | Constructs identity Pose3d(""); if source node nonnull, validates parent then wraps parent shared_ptr. No deep copy. | `0x008453D4..0x0084541E` |
| GetParent null-source case | Leaves newly constructed identity node; does not wrap null source. | `0x00845408..0x0084540A` → `0x0084541E` |
| GetParent root case | Attempts to wrap null parent, then WrapExistingNode dereferences null owner count. **Does not successfully return an empty pose.** | `0x0084540E..0x0084541A`; fatal `0x008452BA` |
| FindRoot | Constructs identity result, then wraps own shared_ptr if source null/root; else wraps node-level FindRoot shared_ptr. | `0x0084520C..0x0084526E` |
| FindRoot null-source case | Also reaches null owner-count read in WrapExistingNode. | `0x00845240..0x00845242`, `0x0084526A..0x0084526E`, fatal `0x008452BA` |
| Node-level traversal | Starts from parent, shares each ancestor; after 1000 transitions logs LoopBoundOverflow and returns current ancestor. | `0x008452C4..0x0084537C` |
| Wrap mechanics | Add_shared new control block, replace node/control pointers, release old block, **unconditionally node.owners++**. | `0x0084529C..0x008452C0` |

## Coverage 7 — SetParent null-node reachability

| Question/step | Result | Address or UNKNOWN |
|---|---|---|
| Direct-call inventory | 34 Thumb call instructions to the SetParent PLT found in all executable sections; listed individually in §7. No direct branch to exported empty PoseBase constructor found. | `20261013-remaining-pose-bodies-branch-scan.txt`, caller excerpts in `...-callers.txt` |
| Null-parent fatal site | With unowned-parent flag false and parent.node==null, reads parent name at null+0x38 before VERIFY, then id at null+0x44. | `0x00844A24..0x00844A52` |
| Null producer | Exported bare PoseBase default constructor creates null node/control block. Assignment from null source propagates null to existing destination. | `0x00845724..0x00845734`, `0x0084594A..0x008459B8` |
| Misleading producer | GetParent(root) and FindRoot(null) do not return successfully with null node: they fault first in WrapExistingNode. | `0x008452BA`, paths in coverage 6 |
| Input-node gates | GetWithRespectTo rejects null inputs; IsSameAs reads pose node transform before its SetParent; input-copy paths fault earlier if copied source null. | `0x00845A34..0x00845A68`; `0x008458F0..0x0084592A`; caller table §7 |
| Positive production witness | **UNKNOWN: no complete, defined execution producing a null Pose3d node and delivering it to a listed live SetParent call was established.** No “unreachable” claim follows from that. | Remaining owner-field/argument lifetime boundary, §7 |
| Policy | Every original null/undefined read is already covered by standing M1-034; no new approval required. | Current AGENTS.md standing rule; original addresses above |

## 1. Double quaternion point formula and order

Storage: q=(w,x,y,z), each f64; p=(px,py,pz), each f64. First compute separate products in instruction order:

`xx, ww, yz, wx, wy, wz, xz, xy, yy`, then `ww−xx`, `ww+xx`, then `zz` (`0x00849F98..0x00849FC4`). Double yz,wx,wy,xz; form `(ww−xx)+yy`; double wz and xy; compute `(ww+xx)−yy`, `(ww−xx)−yy`; form doubled wx+yz, doubled xz−wy, doubled xy+wz; then subtract zz from first two diagonal partials. There is no replacement of `ww` with `1−xx−yy−zz`.

For readability define each `two(ab)` as `f64(ab+ab)` after its single multiplication:

```
m00 = f64(f64(ww+xx)-yy)-zz
m11 = f64(f64(ww-xx)+yy)-zz
m22 = f64(f64(ww-xx)-yy)+zz
m01 = two(xy)-two(wz); m10 = two(xy)+two(wz)
m02 = two(wy)+two(xz); m20 = two(xz)-two(wy)
m12 = two(yz)-two(wx); m21 = two(wx)+two(yz)
xout = f64(m02*pz) + f64(f64(m00*px)+f64(m01*py))
yout = f64(m12*pz) + f64(f64(m11*py)+f64(m10*px))
zout = f64(m22*pz) + f64(f64(m20*px)+f64(m21*py))
```

All unmarked binary operations in that block are also separate f64 operations. The output x/y/z stores occur at `0x0084A050/54/58`. This is the double-point overload; the live float-point overload narrows quaternion components and uses a separate f32 body. No normalization, clamping, matrix Renormalize, or log is inserted here.

## 2. Euler constructor and extraction

Let first input be x-angle A, second y-angle B, third z-angle C. All Radians payloads are floats. Calls occur `cosf(B), sinf(B), cosf(A), sinf(A), cosf(C), sinf(C)` at `0x0084AFDC/AFE6/AFF4/B002/B018/B026`. Denote their results cB,sB,cA,sA,cC,sC. Compute `u=f32(sB*cA)` at B00E and `v=f32(sB*sA)` at B012. Products at B030..B064 are separately rounded. Stored row-major matrix:

```
M00 = cB*cC                  M01 = (v*cC)-(cA*sC)       M02 = (u*cC)+(sA*sC)
M10 = cB*sC                  M11 = (cA*cC)+(v*sC)       M12 = (u*sC)-(sA*cC)
M20 = -sB                    M21 = cB*sA                M22 = cB*cA
```

Every product/add/subtract above is f32 and un-fused. Identity Renormalize happens **before** the trig stores. No subsequent row correction occurs in this constructor.

GetEulerAngles(matrix,x,y,z,x2*,y2*,z2*) uses the f32 singular test `abs(f32(abs(M20)-1)) < float(0x3727C5AC)` (=1e-5f), using compare/conditional negate rather than replacing intermediate arithmetic. On singular branch, z=0; M20>0 sets y=−float(π/2) (`0xBFC90FDB`) and x=atan2f(−M01,−M02); otherwise y=+float(π/2), x=atan2f(M01,M02). Each Radians float assignment wraps its result. Each alternate pointer is independently checked before copying the corresponding primary Radians. Return true marks the singular case.

Regular branch: y←Radians(−asinf(M20)); load its wrapped float; k←f32(1/cosf(y)); x←Radians(atan2f(f32(M21*k),f32(M22*k))); z←Radians(atan2f(f32(M10*k),f32(M00*k))). Return false. No asin-domain clamp: an invalid matrix can produce NaN. If x2 is nonnull, y2←float(π)−y through the float-minus-Radians helper; then k2←f32(1/cosf(wrapped y2)); x2 and z2 use the same pairs multiplied by k2. **This branch dereferences y2 and z2 without independent null checks**; supplying x2 alone is an original crash, covered by M1-034. If x2 is null, nonnull y2/z2 are not written in the regular branch. Output aliasing is not protected by a snapshot: assignment and subsequent loads occur in the stated order.

GetAngleAroundX/Y choose the pair with larger squared projected length, all squares and additions f32; a tie selects the second pair. With unordered comparison, `ble` is not taken, so the first pair is selected (VFP unordered NZCV=0011). Both call atan2f and then construct a wrapped Radians. They do not call Euler extraction or validate matrix. There are no direct logs in these four bodies.

## 3. Validity, list reads and absolute values

IsValid(tolerance) checks **column lengths alone**. For each column in increasing order, `sum=f32(f32(v0*v0)+f32(v1*v1)); sum=f32(sum+f32(v2*v2)); n=vsqrt.f32(sum)`. Only a NaN sqrt result invokes system sqrtf(sum). Compute `delta=f32(n−1)`, conditional-negate when negative, then require delta<tolerance, strictly. Equality fails, tolerance zero rejects even unit columns, NaN fails. It stops on the first failure and returns false; three passing columns return true. A matrix with three identical unit columns can therefore pass despite zero determinant. No warning or VERIFY is emitted by IsValid itself.

The request range `0x008497D0..0x008498E4` also contains GetColumn and its exception path. IsValid's fixed indexes cannot reach that support error. An index≥3 constructs CoreTechException with condition `j >= NCOLS`, source `matrix_impl.h`, line 0x18F, then throws (`0x0084989C..0x008498C0`). This is an exception, not a DAS log.

The initializer_list constructor ignores supplied count, reads nine words into a temporary and then copies nine words to the destination, before Renormalize. Longer lists have their tail ignored; shorter lists read beyond their backing object. Zeroing the destination first does not zero the missing temporary elements: those are out-of-bounds source reads. Current M1-034 applies to malformed short lists; no safe partial-fill policy is recovered from this body. Renormalize's warning/helper semantics are the already-adopted pose3d-arithmetic and Rodrigues reports, reached exactly at `0x0084992E`.

Abs walks rows 0..2, columns 0..2. For each entry it gets mutable element address, loads f32, executes VABS.f32 and stores it through a second address lookup. Negative zero becomes positive zero; no norm repair occurs. Preserve hardware floating-point handling of nonfinite operands rather than introducing a blanket nonfinite rejection. Indices in this body are valid and no DAS calls occur.

## 4. Vector transform ownership and arithmetic

The counts are `(end−begin)/12`. Equal-sized vectors are transformed by index into a local three-float temporary and then overwritten, which permits in-place transformation. Input begin is reloaded between iterations; output begin is reloaded before each store. Count is captured once. Different-sized output is cleared by resetting end to begin (trivial element destruction), without shrinking capacity. Input begin/end are then refreshed, and transformed points appended in order. No upfront reserve or output-size resize occurs. Allocation failures can propagate from the slow append; this body has no recovery catch.

Both branches call Rotation3d×Point3f; the adopted float quaternion point formula applies, including component narrowing without Normalize. Add translation to that rotated result **after rotation**, x then y then z, each as `f32(translation[i]+rotated[i])`. Empty input returns immediately after any required output clear. Input==output necessarily selects equal-count branch. No additional matrix conversion, deduplication or floating-point validation occurs.

## 5. Two-dimensional operations, including the *= discrepancy

Transform2d stores Radians at +0/+4 and translation x/y at +8/+C. Define Rot(a,p) with c=cosf(a), s=sinf(a), then matrix [c,−s;s,c]; each product rounded f32 and each row is one separate add:

`rx=f32(f32(px*c)+f32(py*(-s))); ry=f32(f32(px*s)+f32(py*c))`.

TranslateForward updates x first using x+cosf(a)*d, then calls sinf and updates y. Transform `A*B` computes wrapped sum angle first, constructs a rotation of that sum (unused for translation), then constructs rotation of original A angle and uses `A.t+Rot(A.a,B.t)`. The unused constructor/trig calls are actually present (`0x0084B6FA..0x0084B71A`), not a formula simplification to omit silently.

`A*=B` first mutates A.a to wrapped A.a+B.a (`0x0084B666`), then copies **A's updated Radians** for matrix construction (`0x0084B66C..0x0084B678`). Thus its translation is `A.t+Rot(new A.a,B.t)`, not the product overload's formula. This discrepancy is defined original behavior. A*=A uses doubled/wrapped angle and old translation, locally rotates both coordinates before stores, then adds original coordinates.

PreComposeWith(B): first A.a+=B.a; rotate A.t using then-current B.a; copy both rotated coordinates into A.t; then add B.t coordinate by coordinate. For A==B, B.a is the just-updated angle and B.t aliases the already-stored rotated vector: the final translation doubles that vector. Preserve these alias effects. Invert scales angle by −1 through Radians (multiplication plus rescale), negates translation in place, and then applies Rot(new angle,negated translation). GetInverse copies Radians/translation, then runs Invert. Point multiplication creates Rot(original angle,p), stores both rotated values, then adds original translation x/y in that order. No f64 widening appears in these arithmetic bodies.

Pose2d constructors are outside the supplied transform range but were also inspected. Default delegates to angle zero and point (0,0). Angle/point and angle/x/y constructors use an unnamed PoseBase<2d> node, plane origin (0,0,0), and plane normal Z_AXIS_3D. Pose2d(Pose3d) extracts stored node rotation's GetAngleAroundZaxis and stored x/y translation, then supplies the same plane origin/Z normal; it does not first transform into a world frame. **Pose2d(Transform2d) instead zeroes all six plane-origin/normal floats** (`0x008464D2..0x008464D8`), so its plane normal is zero, not Z. These constructor paths have no direct log. PoseBase<2d>'s wider tree API is outside this bounded request's transform family.

## 6. Shared-node results and additional null crashes

GetParent and FindRoot are wrappers, not ordinary Pose3d copy constructors. Both initially build a new unnamed identity pose, then WrapExistingNode replaces its shared_ptr with the selected existing shared_ptr. Successful results share the parent's/root's node; edits through them affect the original tree node. They do not append `_COPY`, reset that shared node's ID, or clone its transform. The temporary initial node is released. WrapExistingNode adds shared ownership before releasing old storage and increments the selected node's logical owner count. It does **not** call RemoveOwner for the replaced initial node; that is the observed instruction sequence.

Critical correction to an intuitive “empty parent wrapper” interpretation: WrapExistingNode has no null guard at `0x008452BA`. GetParent(root) loads its null parent shared_ptr, calls WrapExistingNode, installs null pointers into the result, then reads address 0x48. FindRoot(null-source) takes the own-node wrap branch and faults at the same address. GetParent(null-source) skips wrapping and successfully leaves a newly allocated identity pose. FindRoot(valid root) shares that root itself; FindRoot(valid nonroot) traverses ancestors, shares the root, and releases its temporary shared_ptr.

Node-level FindRoot starts from the known-nonnull parent; it validates parent pointers and iteratively installs the next ancestor's shared_ptr. It caps **transitions** at 1000, logs Error `LoopBoundOverflow` with `%s:%d`, `poseTreeNode.h` line 0x145, sets errG and optionally debug-breaks, then returns the current shared ancestor. That is a defined bounded return, not an invented throw or empty result. Dev_AssertIsValidParentPointer's existing VERIFY behavior remains a separate helper.

For the root/null wrapper crashes, the current M1-034 policy says skip the smallest failed operation, preserve prior state and log M1-034/M11-068; it does not authorize fabricating a shared null wrapper as source behavior. The manager must record both fatal site `0x008452BA` and triggering wrapper.

## 7. Every recovered direct SetParent call and its parent source

The native branch scan checks every halfword position in executable sections for Thumb BL/BLX/B.W encodings targeting PLT `0x004A5B78` or body `0x00844A14`. It found these **34** call sites (the decompiled function index has 29 containing functions); native excerpts resolve the parent arguments below. No SetParent function-pointer relocation/callback was found beyond its ordinary PLT/GOT linkage. This is a static direct-call inventory, not proof against unknown indirect callers or arbitrary externally constructed objects.

| Call instruction | Caller and supplied parent | Null-node assessment |
|---|---|---|
| `0x004F42F2` | FaceWorld::AddOrUpdateFace → GetOriginByID | Bad ID returns constructed fallback origin, not null. Stored origin lifetime follows origin list. |
| `0x004F5B32` | FaceWorld::GetLastObservedFace → GetWorldOrigin | Current origin pointer; no null-node construction here. |
| `0x00506B9A` | ObjectPoseConfirmer::AddRobotRelativeObservation → Robot::GetPose | Robot-owned pose; getter reads its tree/root before return. |
| `0x0050E002` | PreActionPose ctor → GetParent(input pose) | Null input leaves identity; valid root faults in GetParent **before this call**; successful GetParent has nonnull node. |
| `0x00510B0A` | Robot::Delocalize → new origin | New PoseOriginList origin, not an empty wrapper. |
| `0x00510B44` | Robot::Delocalize → same new origin | Same as prior row. |
| `0x00513B16` | Robot::GetHistoricalCameraPose → HistRobotState pose | r2 entry argument saved in r7; not timestamp. Null-state-node feasibility **UNKNOWN**; no guard at call. |
| `0x005156CA` | Robot::LocalizeToObject → current origin | Origin-list +0x14 reference. |
| `0x005156FE` | Robot::LocalizeToObject → observed object's pose | GetWithRespectTo succeeded on related input first; ordinary path has valid input node. |
| `0x00515E1A` | Robot::LocalizeToMat → current origin | Origin-list +0x14 reference. |
| `0x00515E76` | Robot::LocalizeToMat → stored mat object's pose | Following successful GetWithRespectTo; no null construction here. |
| `0x00517144` | Robot::GetComputedStateAt → current origin | Only history success result zero; origin-list +0x14. |
| `0x00531AA8` | RobotStateHistory::ComputeStateAt → GetParent(computed historical pose) | GetParent failure precedes SetParent; successful wrapper nonnull. |
| `0x0054AA58` | TurnTowardsPoseAction::Init → GetWorldOrigin | Robot-owned current origin. |
| `0x0054E51E` | MountChargerAction::ConfigureTurnAndMountAction → GetWorldOrigin | Robot-owned current origin. |
| `0x00553EC8` | PickupObjectAction::Verify → GetParent(Robot::GetPose) | Successful GetParent nonnull; a root faults earlier. |
| `0x005CDCAA` | BehaviorDockingTestSimple::HandleWhileRunning → GetWorldOrigin | Robot-owned current origin. |
| `0x005F4152` | BehaviorGuardDog::ComputeStartingPose → GetWorldOrigin | Robot-owned current origin. |
| `0x0061E284` | BlockWorld::CreateFixedCustomObject → GetParent(Robot::GetPose) | Successful GetParent nonnull; root faults earlier. |
| `0x006219A8` | BlockWorld::UpdatePoseOfStackedObjects → GetParent(object pose) | Successful GetParent nonnull; root faults earlier. |
| `0x0062241E` | BlockWorld::AddMarkerlessObject → GetParent(Robot::GetPose) | Successful GetParent nonnull; root faults earlier. |
| `0x00633010` | CarryingComponent::SetObjectAsAttachedToLift → object pose | Object transform/GetPose used upstream; no null-producing write here. Whole object lifetime **UNKNOWN** for malformed state. |
| `0x0063E8C4` | MovementComponent::CheckForUnexpectedMovement → Robot::GetPose | Getter validates root before return; no null construction here. |
| `0x0065995C` | VisionPoseData swap overload 1 → first data's robot-state pose | Earlier deep-copy/assignment sequence; null input faults earlier or leaves unusable destination before call. |
| `0x00659964` | Same swap → second data's robot-state pose | Same input validity boundary. |
| `0x00659DBC` | VisionPoseData swap overload 2 → first data's robot-state pose | Same sequence as overload 1. |
| `0x00659DC4` | Same swap → second data's robot-state pose | Same input validity boundary. |
| `0x006AF36A` | VisionPoseData::Set → its stored robot-state pose | Assignment from null input can reset parent node; whether a null input reaches this production API is **UNKNOWN**. See below. |
| `0x006B37C8` | VisionSystem::DetectFaces → stored camera pose | Object member/reference; complete lifetime proof **UNKNOWN**, no node-null guard here. |
| `0x00845DFE` | GetWithRespectTo → target pose | Both input nodes checked before arithmetic; null target rejected before call. |
| `0x00847012` | Pose3d::IsSameAs_WithAmbiguity → compared pose | Compared node transform already read; a null node faults earlier. |
| `0x00847E48` | PoseOriginList::Rejigger → found origin | Map-owned origin; current direct lookup success required. |
| `0x00876862` | ObservableObject::AddMarker → this object's own pose | No node guard at call; general malformed object's node lifetime **UNKNOWN**. |
| `0x0087E064` | TrackedFace::UpdateTranslation → camera pose at camera+0xC | Camera pose is supplied reference; malformed camera-node feasibility **UNKNOWN**. |

Actual null production must be distinguished from null **parent pointers within valid nodes**. Roots have valid nodes with parent shared_ptr null; passing that root to SetParent is valid. GetOriginByID misses log BadID and lazily construct `OriginListDefaultOrigin` at `0x00847C82..0x00847C9A`; it does not return an empty Pose3d. GetWorldOrigin just returns origin-list+0x14 (`0x005128FC..0x00512902`), so its global lifetime still relies on the owner.

The bare exported PoseBase default constructor initializes node/control pointers zero. No direct caller to this constructor was found by the whole-executable scan. Pose3d named/transform/copy constructors inspected allocate nodes; copying from a null node would fault in the node copy constructor rather than safely produce null. Assignment **can** propagate null: source.node==null takes RemoveOwner on destination then sets destination node/control to zero (`0x0084594A..0x008459B8`). This means absence of a visible empty Pose3d constructor is not enough to declare null nodes impossible.

The clearest conditional SetParent witness is VisionPoseData::Set: it assigns the input HistRobotState pose into stored robot pose at data+4, copies remaining state, assigns the separate supplied camera pose into data+0x6C, and calls `SetParent(storedCamera,storedRobot)` at `0x006AF36A`. With a null-node HistRobotState input and valid existing destination/camera nodes, first assignment resets stored robot to null, second assignment succeeds, and SetParent reaches the null-name read at `0x00844A48` when the global unowned-parent flag is false. This is a recovered **conditional** route; the upstream existence of that null-state input in a defined shipped run remains UNKNOWN, not a demonstrated live failure.

GetParent(root) therefore cannot by itself demonstrate the requested SetParent crash: it crashes first at `0x008452BA`. Calls immediately consuming a successful GetParent result are protected by that earlier fatal boundary, not by a checked-null return. No complete production witness was established for the remaining argument/member lifetime boundaries. This report inventories all recovered direct consumers and identifies the specific producer-to-input gap; it does not mark the SetParent fatal path either reachable or unreachable in ordinary shipped execution.

If a null-node SetParent invocation occurs, its log cannot complete: `[parent.node+0x38]` is read **before** the VERIFY call, and then `[parent.node+0x44]`. When `_areUnownedParentsAllowed` is true, that VERIFY branch is skipped; the lower PoseTreeNode::SetParent can install a null parent. A valid nonnull but owner-count-zero parent merely causes VERIFY `PoseBase.SetParent.UnownedParent`, `VERIFY(%s): Setting parent of %d(%s) to unowned pose %d(%s)`, condition `otherPose.IsOwned()`, and continues. Defined behavior stays exact. Original null-node crashes use existing M1-034 with M11-068 and the owning caller record; the current manifest's suggestion that another operator ruling is needed is stale relative to the standing approval.

## Evidence and handoff

Binary SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Local native extracts/scripts are `20261013-remaining-pose-bodies-*` beside this answer. Instruction reading, whole-executable direct-branch inventory and source navigation only; no shipped code execution, upload, hardware run or production edit.

Current M11-068 title: **“Remaining unread pose bodies: UnitQuaternion<double>*Point3d, Euler/matrix X/Y ctors, IsValid, Abs, ApplyTo(vector), Pose2d/Transform2d, GetParent/FindRoot wrappers; reachability of SetParent with a null parent node”**, status **RECOVERABLE_GAP**; evidence references the two context reports and ranges from this request. All requested bodies are supplied for manager verification; SetParent's upstream null-input lifetime remains explicitly UNKNOWN. No fidelity record changed. Suggested later regressions: double association; Euler singular/tie/optional outputs; unit-length but nonorthogonal columns; strict tolerance and NaN; in-place/different-size vectors; the 2d *= discrepancy and aliases; shared root/parent identity; each fatal null/list case under M1-034. No test run, branch, commit or push was made.
