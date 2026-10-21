# R-M11-RETAINED-OWNER — carried-stack rejigger comparison

Request: `requests/20261021-m11-retained-pose-owner-comparison.md`. Examined HEADdc3004dd. Independent read-only comparison, pending manager checking. No status change.

## Coverage table

| Step | Evidence / current production | Disposition |
| --- | --- | --- |
| Corrected owner case | Adopted OV110 M1–M3/A1–A7; OV121 A8-11..15 | Reuse; no repeated owner census |
| Origin retention | NativeDelocalization.cs _nativeOrigins, GetNativeOrigin, DestroyPoseOriginOwners | Present; distinct from old object pose wrapper |
| Live carried stack | Docking.SetObjectAsAttachedToLift → NativePose.SetParent(LiftPose), top.NativePose.SetParent(obj.NativePose) | Present for initial attachment; parent-value mechanism adequate |
| Rejigger entry | BlockWorld.LocalizeToObject only SetLocalizedTo, emits MISSING | Full source path still absent; cannot claim production closure |
| Carried object transfer | MoveObjectToNewOrigin selects o.Pose, which flattens NativeCarryingTree objects | Parent/identity lost in this adapter; later builds do not close OV121 case |
| Embedded value copy | Native SetPose0x004EF590..0x004EF646; CopyWithNewPose0x00506EF8..0x00506F42 | Native assigns into owned node; ordinary C# SetPose replaces wrapper |
| Old-instance release | UpdateObjectOrigins removes map entries without disposing object pose; ClearConfirmer drops entries | Explicit last-wrapper transition absent |
| Diagnostic consequences | M11-077 P3; Geometry.Dispose, parent AssertParent | RemoveOwner/parent check primitives present; NotLastOwner and actual release edge missing |
| Whole record settlement | M11-084 has remaining independent sites/race questions | No settlement; bounded integration delta only |

## Result

**Do not close F7's carried-stack rejigger case as already preserved.** Managed node references can preserve the required parent lifetime, and current initial carrying attachment does use a live parent chain. However the actual origin-transfer adapter takes the flattened public pose, and the source localization/rejigger caller is still unbuilt. It also erases old objects without the native pose-owner release. These are concrete production joins, not a requirement to reproduce shared_ptr internals.

Retaining origin wrappers does not preserve the missing owner: the last wrapper that dies in OV121 is **old carried object C's pose**, while new top object T′ and its confirmation pose retain C's node as their parent. The origin is a different node higher in the chain. Keeping the origin alive neither prevents C's last-wrapper event nor reattaches T′ to new C′.

## Corrected source rows to use

The inventory at `re-analysis/inventory/M11-vision.md` adopts OV106, OV110 and OV121 in successive sections. Read with the later corrections; do not revive OV106's erase-order hypothesis.

- OV110 M1: “A pose copy allocates a new node whose parent is the source's parent shared_ptr” (0x008458F0..0x00845924; node copy0x00845FEE..0x00845FF6). Assignment changes the destination's existing node's parent/transform/name (0x008459D0..0x00845A2C); it does not alias the source wrapper.
- OV110 A1/A2: the top object T is parented to C.pose by SetObjectAsAttachedToLift0x00632FA2..0x00633020. AddObjectRelativeObservation0x00506D12..0x00506DEA stores T's value and a separate confirmation pose whose parent is also C.pose.
- OV110 A3: UnSetCarryingObjects “does not re-parent” (0x00633C00..0x00633D80). Unset is not an implicit lifetime fix.
- OV110 A5/A6 corrects OV106: DeleteLocatedObjects copies poses before erase; NotLastOwner can occur in either erase order. OV121 then closes the listed normal deletion callers more narrowly: SetCarriedObjectAsUnattached re-roots C/T before optional deletion, so that is not the positive witness.
- OV121 A8-11: LocalizeToObject obtains old origin, Rejiggers, updates objects/map/faces and Flattens (0x005158D8..0x005159B2).
- A8-12: UpdateObjectOrigins clears confirmer, runs lambda1, and conditionally erases the old-origin entry (0x00620534..0x006206F0).
- A8-13: carried object's transfer copies its own pose, retaining its parent (lambda1 0x00628E18..0x006292F6); noncarried path converts relative to new origin.
- A8-14: CopyWithNewPose assigns the pose into the destination and confirmation entry (0x00506EF8..0x00506F40).
- A8-15: erasing old C destroys its wrapper while T′/confirmation still retain old C's node. Node remains allocated and parent-linked, so T′ still follows the lift through old C until re-rooted; NotLastOwner is a defined VERIFY, not a dangling read to skip.
- OV121 reachability qualification remains: whether shipped pickup accepts a C with known T above is still an unread input boundary (IDockAction+0xC0/CanInteractWithObjectHelper), plus an origin change while carrying. The recovered conditional state case is not proof that every pickup/relocalization run reaches it.
- OV110 race correction also remains: ZeroOwners/ZeroOwnersWithID cannot be globally called unreachable; M11-095 owns the vision race. No race reproduced or excluded here.

P3's precise representation matters: adopted `overnight/20261014-ov09-m13-029-check.md` says NotLastOwner is checked after wrapper Owners reaches0 with unowned parents disallowed, and fires when control null or **control+4 !=0**. That internal shared-owner field stores use_count-1; the condition text is _node.use_count()==1 and printed count is control+4+1 (or0 if null). Do not translate OV110's abbreviated internal-counter prose into a test that fires with a sole shared owner.

## Narrow native boundary reopened

Only the ordinary SetPose/CopyWithNewPose copy join was reopened; the accepted lambda/census was reused.

At0x004EF598 SetPose selects embedded object+4.0x004EF5A2 calls PoseBase operator= into that node, then stores state+0x24, name, and nonnegative fromDistance; return0x004EF646. Therefore supported parent identity is carried through assignment, not by replacing the object wrapper or flattening its pose.

CopyWithNewPose0x00506EF8..0x00506F42:
-0x00506F0E loads destination virtual slot+0x28;0x00506F16 calls it with newPose and source distance/state.
-0x00506F2E obtains the confirmation entry.
-0x00506F36 selects entry+0x18;0x00506F38 calls PoseBase operator= again.
-returns0; the two embedded destinations are independent owned nodes sharing the newPose's parent.

These bodies were read through returns, resolving calls against the shipped .so. No decompiler text used as evidence.

## Current comparison

Paths are under `cozmo-stack/src/Cozmo.Robot/`.

1. **Generic pose primitives are substantially present.** `Vision/Geometry.cs` copy creates a separate node with source transform/parent; Assign keeps destination node identity and copies parent/transform/name. SetParent keeps a reference to the actual parent node. Parent checks and RemoveOwner counters/conditional diagnostics are implemented. A managed strong parent reference is sufficient to keep an unowned parent node and its transform chain alive. There is no need to clone C++ allocator/control-block machinery just to preserve those values.

2. **Retained origins are present.** `NativeDelocalization.cs` owns _nativeOrigins; GetNativeOrigin returns those wrappers. DestroyPoseOriginOwners logs its event, disposes in key order under the saved/restored AllowUnownedParents flag and process gate, then clears the map. MainPose → origin, LiftPivot → MainPose and LiftPose → LiftPivot are retained. This later build closes the old blanket “no retained origins” premise. It does not implement Rejigger or old-C object release.

3. **Initial carried-stack parents are present.** `Manipulation/Docking.cs` SetObjectAsAttachedToLift calls the actual carrying calculation, then under robot.PoseTreeGate makes C.NativePose parent LiftPose and T.NativePose parent C.NativePose, marking NativeCarryingTree. Its value inputs remain the existing M12-008 candidate; this comparison credits the explicit parent bindings, not the whole pickup path.

4. **Public snapshot and native owner are intentionally different.** `Vision/BlockWorld.cs` ObservableObject.Pose returns _pose for ordinary objects, but for NativeCarryingTree it calls GetWithRespectToRoot and returns FromFlatRotation — a new flattened snapshot. NativePose exposes the retained node. This facade is reasonable for public world coordinates; it is not interchangeable with native GetPose when copying parents for transfer.

5. **Origin transfer currently uses that wrong side of the boundary.** MoveObjectToNewOrigin has `Carrying(o.ObjectId) ? o.Pose : oldOriginWrtNew.Compose(o.Pose)`. On the carried path this selects the flat snapshot, so C′/T′ cannot acquire the old native parents required by A8-13. CopyWithNewPose then invokes ordinary obj.SetPose, whose body is `Pose = pose`; it replaces _pose with the provided wrapper. This is different from the native in-place assignment above. Confirmation copying is improved by AddInExistingPose's entry.Pose.Assign(obj.NativePose), but that copies the already selected/transferred parent; it cannot recover a discarded one.

6. **Release remains absent.** UpdateObjectOrigins erases _located keys with Remove. It never disposes the old object's pose. ClearConfirmer similarly clears its map. GC eventually dropping objects is not a deterministic decrement of Geometry's explicit Owners. Retaining a C# child reference keeps C's node alive, but leaving its old Owners positive suppresses the recovered unowned-parent checks. Geometry.Dispose itself still emits the M11-077 MISSING and omits NotLastOwner/final bookkeeping. Fixing only reachability does not fix these release/error-state transitions.

7. **Full production entry remains absent.** BlockWorld.LocalizeToObject emits a visible M11-044 missing Rejigger message, increments UnbuiltLocalizeToObjectCalls and performs only SetLocalizedTo. Direct calls to UpdateObjectOrigins in existing tests are explicitly labeled unit reach. They cannot demonstrate the supported robot localization → rejigger → transfer → release chain.

The concrete missing state is old-C node identity/parent retention in T′ and its confirmation, distinct C′/T′ embedded wrappers, and old-C last-wrapper release. That controls later lift-relative values and conditional UnownedParent error flags, independently of exact log wording. NotLastOwner is separately missing in M11-077. This is defined native behavior; M1-034 supplies no reason to skip it.

## Tests and smallest next scope

Existing `M11RVisBuildTests.M11_043_ObjectsAreClonedIntoTheNewOriginWithTheirStateAndTheOldOriginIsErased` checks flat coordinates, source state, independent confirmation node, and shared confirmation parent. Its carried fixture has no live carried parent chain. It does not compare old-C identity, later lift movement, owner loss or diagnostic gates. `PoseTreeVerifyBuilderTests` exercises retained unowned parents when parent.Dispose is called explicitly; that validates primitives, not the missing production erase/release edge. No tests were run here.

Smallest integration delta for this case, after inventory approval:
- retain native pose values and their actual parent nodes for the carried transfer; preserve separate destination/confirmation wrappers through native assignment semantics;
- map old-object/confirmation release to their actual source owner edges, with the existing parent-value representation keeping referenced nodes alive after Owners reaches0;
- account for conditional NotLastOwner and UnownedParent error-state effects using a sourced ownership relation model, rather than managed GC timing or a fabricated event;
- connect the actual localized-origin/rejigger entry before claiming complete production reach. M11-043/044 own those transfer/caller joins; M11-077 owns destruction diagnostics, M11-084 records the conditional lifetime case. Do not make a whole-record closure contingent on imitating unused allocator internals.

Independent regression scenario, not implemented:
1. Reach C-on-lift/T-on-C through the real checked pickup/attachment route after its known-top eligibility input is resolved; retain actual old C node identity.
2. Relocalize/rejigger with the native carried branch and successful old-entry erasure conditions, through the real producer.
3. Assert new T′ and its confirmation have separate nodes but parent exactly old-C node, not C′, origin or flattened copy. Assert C′ has its independently copied native parent.
4. After old C wrapper release, assert its node remains allocated/reachable with Owners0 and preserved transform/parent. Change lift pose and verify T′ root-relative pose follows through old C.
5. Capture native VERIFY/error-state behavior at release and subsequent parent walks; compare predicate/order and meaningful diagnostics. Do not make exact log spelling alone a blocker.
6. Run the native unattached operation and assert C/T are re-rooted before clearing carrying IDs; then later lift motion no longer carries them. Negative control: ordinary Delocalize moves map ownership and does not manufacture the rejigger last-wrapper case.

Remaining uncertainty: shipped known-top pickup eligibility, full production Rejigger integration, old-instance destruction/copy release accounting and the existing broader M11-084 site/thread questions. No census repeated, exclusion made, hardware run, implementation change, approval or settlement.

## Current manifest quotes (dc3004dd)

### M11-077

Title: Pose destruction path: PoseBase<Pose3d> destructor, RemoveOwner, the NotLastOwner verify, PoseTreeNode destructor

Status: IMPLEMENTATION_GAP

Evidence (verbatim):

> re-analysis/research/overnight/20261014-ov09-m13-029-check.md: P1 ~PoseBase 0x008450F4..0x00845166 (Pose3d vtable 0x0101CD00 slot 0 -> 0x008450F5); P2 RemoveOwner 0x008448B4..0x00844914; P3 verify call 0x00845156 (key PoseBase.Destructor.NotLastOwner, condition _node.use_count() == 1); P4 PoseTreeNode dtor 0x0084605C..0x0084609C, SetParent 0x008449EA..0x00844A10.

Current unresolved (verbatim):

> IMPLEMENTATION_GAP: port P1-P4 with the B-POSE-1 pose tree. Unread: the PoseTreeNode bookkeeping tail 0x008D148C and the Pose3d node shared_ptr on_zero_shared. OV13 check (re-analysis/research/overnight/20261014-ov13-m11-055-check.md): confirmed again; the verify sink is sVerifyFailedReturnFalse 0x0080D9A4..0x0080DA34 (sErrorV, _errG, sDumpCallstack "VERIFY", provider slot +0x14, returns 0). Pose-tree extraction (re-analysis/research/20261014-pose-tree-verifies.md): RemoveOwner 0x008448B4..0x00844912: count 0 logs ZeroOwners with no decrement; reaching 0 logs ZeroOwnersWithID only with the flag clear and a non-zero ID (only origins have IDs). The VERIFY sink is sVerifyFailedReturnFalse (Error, error flag, call-stack dump, provider slot +0x14). To build: job B-POSE-3.
> built, awaiting strong verification: B-POSE-3 (2026-10-15) ports the adopted conditional parent-pointer VERIFY at existing IsRoot/HasParent/GetParent/FindRoot/HasSameRootAs/HasSameParentAs/GetWithRespectTo/IsSameAs call sites, exact RemoveOwner zero-count and zero-with-ID branches, constructor/copy/wrap owner counts and non-null assignment retention; binds origin-owner destruction at Robot lifetime +0x294 with ordered owner release under the saved/restored AllowUnownedParents flag. VERIFY uses the existing Error/error-flag/provider-flush sink; dump target is bx lr. MISSING: absent GetRootID/GetParentString/GetRawParentPtr/IsChildOf/IsParentOf/ToPoseStruct3d APIs, native shared_ptr/use_count/NotLastOwner/bookkeeping, and M11-084 production ownership/cross-thread census. No settlement or status change. See jobs/status/B-POSE-3.md for per-record files/lines, native ranges and call accounting.
> B-POSE-4 (2026-10-15), built, awaiting strong verification: origin-owner teardown now includes its missing first Event under M11-055; ordered disposal/flag restore retained. Existing native shared-control-block/NotLastOwner and other destructor gaps remain unclaimed.
> B-POSE-5 (2026-10-15), built, awaiting strong verification: origin-list event/count and ordered owner resets now share one gated body. Existing RemoveOwner suppression/flag restoration retained; native control-block/NotLastOwner/destructor gaps still unclaimed.
>
> B-M13-4 (2026-10-15), partial build, awaiting strong verification; BLOCKED: No new native shared_ptr use_count model inferred. Existing RemoveOwner is retained; the visible MISSING now accurately names NotLastOwner0x0084510E..0x0084515E and final-node/bookkeeping0x0084605C..0x0084609C ->0x008D148C. Path Abort uses actual Pose3d wrapper release instead of List<Action> callbacks. Full native control ownership and final-node destruction remain unbuilt, so P1-P4 are not claimed complete.
> B-M11-6 (2026-10-15): full-suite concurrency exposed overlapping host robot teardowns saving/restoring the process-global AllowUnownedParents flag under different per-robot gates, leaving it true after both completed. All temporary writers now also share Pose3d.UnownedParentsGate around the existing checked flag/save/owner-release/restore sequence (native 0x00847944..0x008479DE). Single native-thread ordering and event-first behavior unchanged; this host exclusion does not settle the remaining native ownership census. Regression blocks real origin-owner release while the process gate is held, then verifies release/restoration.

### M11-084

Title: Pose wrapper lifetimes in production: whether any flow leaves a child under an unowned parent or drops an origin's last wrapper outside ~PoseOriginList (UnownedParent, NotLastOwner, ZeroOwners, ZeroOwnersWithID VERIFYs on a running robot)

Status: RECOVERABLE_GAP

Evidence (verbatim):

> re-analysis/research/20261014-pose-tree-verifies.md: "Still unread": parent-pose lifetimes (robot +0x298, origins, object poses, temporaries), PoseOriginList erase/rejigger paths, inline wrapper attaches writing PoseBase+4 without +0x48, cross-thread pose-tree mutation.

> re-analysis/research/overnight/20261016-ov121-answer.md: AddRobotRelativeObservation 0x00506B70..0x00506C48; SetPoseHelper 0x0050609C..0x00506116; SetCarriedObjectAsUnattached 0x006333D4..0x00633930; MarkObjectUnobserved/Unknown 0x00506FBC..0x005073FC; CheckForUnobservedObjects filter 0x00621C6C..0x00621D88; LocalizeToObject rejigger 0x005158D8..0x005159B2; UpdateObjectOrigins 0x00620534..0x006206F0 and lambda1 0x00628E18..0x006292F6; CopyWithNewPose 0x00506EF8..0x00506F40; ~ObservableObject 0x004E0D78..0x004E0DA0; DriveToPoseAction::Init 0x0055A8A6..0x0055A91E.

Current unresolved (verbatim):

> RECOVERABLE_GAP: census the parent-pose lifetimes and the origin erase paths. Batch check 2026-10-16 (re-analysis/research/overnight/20261016-batch-check.md): OV86 is not evidence. New rows: PoseBase.SetParent.UnownedParent VERIFY (0x00844A26..0x00844A7A, attaches anyway); PoseOriginList::Flatten re-parenting 0x00847F34..0x00847FBE (from LocalizeToObject 0x005159B2); Rejigger caller 0x0051590C. Node layout: quaternion +0, translation +0x20, parent +0x30/+0x34, name +0x38, ID +0x44, owner count +0x48. Copy and assignment allocate or deep-copy into own node; NotLastOwner = a child/shared_ptr outlives the last wrapper. Next: the 34 SetParent sites and local-parent lifetimes. Opus answer OV106 (re-analysis/research/overnight/20261016-ov100-101-106-answer.md): narrowed. Unreachable: RemoveOwner.ZeroOwners; ZeroOwnersWithID outside ~PoseOriginList; AssignmentOperator.NotLastOwner (no null-node wrapper is ever created: PoseBase() 0x00845724 has no callers); SetParent.UnownedParent for live wrappers; SanityCheckOwnership (no callers). Destructor.NotLastOwner and the Dev_Assert UnownedParent checks fire exactly when a node's last wrapper dies while a child or shared_ptr still refers to it. 35 SetParent sites classified (owned or safe, or unreachable). Open: (a) SetObjectAsAttachedToLift 0x00633010 parents the top object to the carried object's pose: the DeleteLocatedObjects erase order and UnSetCarryingObjects 0x00633C00 decide whether NotLastOwner fires; (b) GetWithRespectTo callers passing a temporary to; (c) copies sharing a parent that outlive it; (d) vision-thread non-atomic owner counts. Opus answer OV110 (re-analysis/research/overnight/20261016-ov109-110-answer.md): CORRECTIONS: ZeroOwners / ZeroOwnersWithID are not unreachable but HARDWARE_ONLY via the vision-thread race (M11-095); UnSetCarryingObjects never re-parents; the DeleteLocatedObjects erase order does not matter (each pose is copied into a local list first, 0x0061D2A6/0x0061D372); T's PoseConfirmation record (0x00506DD0) is a third holder of C.pose; copies carry the parent shared_ptr and operator= skips the UnownedParent VERIFY. Read rows M1-M3, A1-A7, B1, B2, D1 (ready to build once the remainder closes). Open: A8 (who deletes a carried C while T hangs: 13 DeleteLocatedObjects callers, AddRobotRelativeObservation 0x00506B70, MarkObjectUnobserved 0x00506FBC); B3 parameter/memory to sites; (c) census of outliving copies (ObservableObject dtor order; pre-action/goal copies before 0x0055A8C2/0x0055A8DC); (d) LatticePlannerImpl 0x004A6520 and FaceRecognizer 0x004CF164 thread wraps. OV121 answer (re-analysis/research/overnight/20261016-ov121-answer.md): A8 settled for the listed callers (SetCarriedObjectAsUnattached re-roots C and T first; no DeleteLocatedObjects caller deletes a carried C with T parented, except unread AddConnectedActiveObject 0x006238D4). NEW reachable destroyer: re-jiggering origins while carrying a stack (A8-11..A8-15): lambda1 copies the carried T pose keeping old C as parent, then the old-origin erase destroys old C -> NotLastOwner (defined). Open: whether shipped pick-up docks to a C with a known T on top (IDockAction+0xC0 consumer, CanInteractWithObjectHelper); SeeingCarriedObject routing (0x00620AD4..0x006215CC, 0x0050684C..0x00506B70); lambda2 0x00629862; B3 per-site list; concrete ActiveCube/Block dtors; goal-copy window before DriveToPoseAction::Init (0x00558FB4); indirect calls on planner/face threads. (d) direct calls: only the vision thread wraps poses.
