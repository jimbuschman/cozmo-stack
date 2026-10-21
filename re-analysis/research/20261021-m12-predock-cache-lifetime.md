# R-M12-CACHE-LIFETIME — actionable cached-pose clearing

Request: `requests/20261021-m12-predock-cache-lifetime.md`. Started at HEAD97bdc60f; refreshed to65730364 before completing. Independent extraction, subject to manager checking. Research only; no fidelity status changed.

## Coverage table

Opened before tracing; every question now has a citation or UNKNOWN.

| Step | Native / checked source | Production reach | Disposition |
| --- | --- | --- | --- |
| SetPose clears entries | 0x004E01E4..0x004E0226; end decremented before destructor at0x004E0202/204 | Every actionable SetPose, before base pose write | Read; vector invalidation already built |
| Actual entry destruction | stride0x1C, embedded pose+8; PoseBase destructor0x008450F4..0x00845172, RemoveOwner0x008448B4..0x00844912 | Cached wrappers lose ownership; shared parent retained until final node release | Read; no scalar/marker deletion or explicit goal cancellation |
| Node and parent release | node destructor0x0084605C..0x008460C2; SetParent0x008449EA..0x00844A14 | Clears dying node's parent, releases shared control, frees its auxiliary tree/name | Read wrapper bodies; general reference-count/diagnostic integration remains M11-077 |
| Exported pre-action value | collection0x004DF88A..0x004DF8E8, insertion0x004DFE5C..0x004DFFA2; copy0x0050E138..0x0050E16C | Cache → local accumulator → independently composed world pose → copied output | Read/reused checked primitives; no returned cache-entry alias in this path |
| Output parent | derived ctor0x0050DF00..0x0050E0C0; SetParent0x0050DFFA..0x0050E002 | Parent comes from supplied object pose's parent, not cached entry | Read; retained world output does not depend on cached wrapper remaining owned |
| Supported retained consumer | DriveToObjectAction::GetPossiblePoses0x00558C80..0x00558F34 (remaining symbol tail is literals) | GetPreActionPoses output → copied Pose3d goals; temporary PreActionPose vector destroyed | Read; independent output nodes retain their own parents |
| Existing C# representation | PreActionPose.cs StoredElement/GenerateStored/Compose/ClearForSetPose/ComposeWorld; Geometry.cs copy/Compose/Dispose; DriveActions.cs GetPossiblePoses | Clears list; returns separate poses; flat adapter omits native parent integration | Existing parent-value machinery can retain nodes, but this adapter does not use the required parent relation |
| Universal no-effect / absent reader | No exhaustive direct/inlined cache-reader census in this report | Other consumers retaining a cache node as parent, and release-only error effects | UNKNOWN; not a license to claim all lifetime effects absent |

## Result

The destructor is not evidence that old pre-dock goals become invalid, move, or need cancellation. The bounded exported-pose/DriveToObject path does the opposite: it copies cache values, builds an independent world pose, then copies that pose into goals. Clearing stored wrappers does not rewrite those output transforms or their parents.

The current missing item mixes three different concerns: list invalidation (already built), native pre-action constructor/parent integration (still absent), and generic Pose3d ownership/release diagnostics (M11-077). A C# shared parent reference is an adequate mechanism for keeping an existing parent node reachable; reproducing a C++ control block is not itself a requirement. However, current ComposeWorld returns the parentless result of Compose and does not perform the native SetParent(P.GetParent()) join. Therefore existing general parent support does not establish this production path's fidelity. No blanket “cache destructor has no observable effect” conclusion is justified by this report.

## Source trace

1. ActionableObject::SetPose0x004E01E4 clears six vectors, beginning this+0x28, advancing0xC to exclusive this+0x70. At0x004E01FC it subtracts0x1C from end, at0x004E0200 selects old-end-0x14 (entry+8), stores the new end at0x004E0202, then calls PoseBase::~PoseBase at0x004E0204. Capacity/allocation is retained. Only after all six does0x004E0214..226 forward the base pose write. Reuse M12-045's checked SetPose path; no charger observation retrace.

2. An entry is action type+0, raw KnownMarker pointer+4, embedded Pose3d+8, height tolerance+0x14 and distance+0x18. Copy0x0050E138 copies the two leading words, invokes PoseBase copy for +8, then copies the two final words. Clearing does not delete the KnownMarker or act on these scalar values. PoseBase destructor removes its wrapper owner and releases the node shared control. RemoveOwner decrements +0x48; it does not edit a transform or reparent children. The zero-owner/ID VERIFY cases are already recovered in `20261014-pose-tree-verifies.md` §4.

3. Final-node destruction0x0084605C..0x0084609C calls node SetParent with an empty shared pointer (0x00846064..6C), destroys its auxiliary pointer tree at+0x4C, releases heap name storage if used, then releases the parent-control field if nonzero. The unwind tail through0x008460C2 releases corresponding members. Node SetParent0x008449EA..0x00844A14 retains the new control, writes parent+0x30/control+0x34, releases the old control, then jumps through0x008D148C to the parent validity check. This mutates the dying node, not an independently copied output node. Full control-block use-count behavior and every auxiliary-tree reader are not newly extracted here: UNKNOWN as a global lifetime/no-effect argument, owned by M11-077 rather than invented as a docking side effect.

4. GetCurrentPreActionPoses collects per-type cached vectors into local vector sp+0xBC. The insertion call0x004DF8C8 resolves to0x004DFE5C. Its complete body through0x004DFFA2 was read: fresh elements use PreActionPose copy at0x004DFEEE/0x004DFF36; existing storage uses PoseBase assignment at0x004DFF76; no element reference is returned as the pose result. Ordinary PoseBase copy0x008458F0 allocates a fresh0x58-byte node (0x00845906..10), copies node contents via0x00845FE4, installs shared control and increments the new node's wrapper owner at0x00845924. Its parent is shared, not the original node itself. Checked copy/assignment primitives are in `20261013-rodrigues-and-pose-tree.md` and `20261021-m11-pose-history-settling.md` table067.

5. Derived PreActionPose constructor0x0050DF00 reads source local rotation/translation and constructs a separate result. It composes objectPose * temporary local pose at0x0050DFE4, assigns destination at0x0050DFEC, obtains the supplied object's parent at0x0050DFFA and installs it at0x0050E002. Thus it does not retain the cached pose as the world result's parent. The root-relative object-pose input and offset geometry are existing checked M12-001 evidence, reused here. At0x004DFC2C only virtual validity result1 survives;0x004DFC3E copies the local world PreActionPose into the output (capacity case; slow push at0x004DFC54..58). The source local pose is not the result's backing storage.

6. A concrete supported consumer is DriveToObjectAction::GetPossiblePoses. It calls IDockAction::GetPreActionPoses at0x00558CE8; failures destroy the temporary output vector at0x00558CF4 and return. Nonempty success copies the in-position byte and clears previous goal wrappers (0x00558D0E..32). The in-position branch creates a separate copy for logging at0x00558D4C, destroys it at0x00558E28, and still reaches the common all-pose loop. That loop copies each PreActionPose at0x00558E4A, then copies its embedded Pose3d into the supplied goal vector at0x00558E58 (slow path0x00558E6C), destroys the loop temporary at0x00558E72 and advances0x1C. Finally the temporary output vector is destroyed. Hence this consumer intentionally retains copied goal poses after intermediary destruction already during normal generation. Clearing the original object's caches later cannot make these nodes depend on those intermediary wrappers. This is a bounded reader argument, not a global consumer census.

## Current implementation reach

Paths below are under `cozmo-stack/src/Cozmo.Robot/`.

- `Manipulation/PreActionPose.cs`: StoredElement contains a Pose3d and KnownMarker reference. GenerateStored flattens marker-local arithmetic through marker.PoseOnObject.Compose(rotated). It already visibly reports missing complete native marker-parent constructor binding. Caches is a ConditionalWeakTable of per-type lists. ClearForSetPose empties each list and emits the lifetime MISSING, without disposing the Stored Pose3d. Next generation therefore regenerates (existing B-M12-4 behavior).
- ComposeWorld creates a new local pose and returns objectPose.Compose(...). `Vision/Geometry.cs` Compose creates a fresh pose from composed transforms; it does not set the derived constructor's native parent. Separate output values survive list clearing, but their initial frame/parent fidelity is not established by that survival.
- Geometry.cs has explicit node identity, parent references, copy/assignment and wrapper Owners. Dispose removes an owner and clears Node while reporting unbuilt M11-077 shared-use-count/final-node bookkeeping. Managed reachability alone does not decrement this explicit Owners field. Simply adding Dispose calls would address a wrapper counter on stored values but would not supply missing parent bindings or prove no retained child needs that owner.
- `Manipulation/DriveActions.cs` GetPossiblePoses reaches DockPreActionPoses.Get and retains output.Poses.ToList(); later drive code uses those output world poses. This list copy shares C# PreActionPose records, unlike native goal-node copies, but does not share the cache's StoredElement poses. A claim of full native copy/lifetime parity would still require checking mutations of those shared output records; UNKNOWN here. It is not evidence that cache clearing mutates them.

The missing production owner is the complete pre-action stored/world constructor integration in M12-001 (also relevant to charger generation M13-009), plus generic M11-077 ownership accounting where it has observable effects. M12-045 owns reaching the clear before SetPose; it need not grow a second independent implementation of generic pose release.

## Smallest disposition / independent oracle

Keep list invalidation credited only to its existing bounded build. Check/adopt the copy-and-parent findings above before narrowing the lifetime warning. Do not settle M12-001/045 or dismiss M11-077 just because this drive consumer retains independent copies.

A smallest subsequent integration scope is the actual stored/world constructor joins, using the already supported Pose3d parent-value operations and their checked failure/continue behavior. It should establish marker → object → origin parents and retained output/goal ownership, rather than imitate vector capacity or reference-count instructions. Generic last-wrapper/child diagnostics remain their own owner. Whether a cache-only release needs additional behavior beyond those joins is UNKNOWN pending the bounded reader/ownership census.

Independent oracle (not run; no tests changed):
- Native/emulated sequence: generate cache and output, retain copied drive goal, snapshot each node's transform/parent/owner relationships, SetPose same object, query retained goal relative to origin, generate again.
- Assert old goal stays a distinct node with unchanged transform and the same retained parent; the next output uses the new object pose; regenerated flag/order match. Include parent mutation after generation to distinguish a real retained parent from a flattened transform.
- Include last stored-wrapper removal while an explicit child remains only if a real supported producer of that relationship is identified. Compare native conditional error flag/VERIFY behavior; do not invent such a production producer from a test fixture.
- A list-count-only test cannot establish these facts. There is no robot run, regression-test execution, or claim of complete docking fidelity here.

## Remaining questions — explicitly UNKNOWN

1. Exhaustive direct/inlined readers of cached entry nodes beyond the GetCurrentPreActionPoses export funnel: UNKNOWN; no universal absence claim.
2. Any supported producer that keeps a child parented to a cached node when SetPose removes its last wrapper: UNKNOWN. Read those producers before asserting a cache-clear UnownedParent effect.
3. Final shared-control and auxiliary-tree release effects across all pose users: UNKNOWN in this bounded task; M11-077 remains visible.
4. Complete charger cache adapter reach and native marker-parent/error paths: not recovered anew here; M12-001/M13-009 integration remains open.
5. Every output-record mutation after the C# shallow list copy: UNKNOWN; do not confuse it with an alias to cached stored poses.

No current record is contradicted solely by the destruction/copy findings. M12-001's historical “cache is never cleared” sentence is superseded by its later B-M12-4 paragraph and current code, not a new defect. No manifest or policy decision is made.

## Current manifest quotes (65730364)

### M12-001

Title: Pre-action pose types, their per-type ctor distances and the distance threshold

Status: IMPLEMENTATION_GAP

Evidence (verbatim):

> dispatch on actionType 0..5: 0x004E5958 cmp r5,#5 / 0x004E595E tbh / table 0x004E5962

> type 0 Docking 75.0 (0x004E5A36 movt r1,#0x4296); angle pi/2 (0x004E5998 vadd d10=pi/2); translation {0,-65,-22} (0x004E59B4/0x004E59C0/0x004E59D4)

> type 1 PlaceRelative 40.0 (0x004E5ADA movt r1,#0x4220); angle pi/2 (0x004E5A4E); translation {0,-100,-22} (0x004E5A70 movt r0,#0xc2c8 / 0x004E5A7E)

> type 2 PlaceOnGround 4th arg 0 (0x004E5B5A/0x004E5B80); angle pi/2 (0x004E5AF4); translation {0,-49,-22} (0x004E5B0C movt r0,#0xc244 / 0x004E5B16)

> type 3 Entry: 0x004E5DA2 (loop tail), no pose

> type 4 Rolling 75.0 (0x004E5C50 movt r1,#0x4296); angle pi/2 (0x004E5BA6/0x004E5BB4); translation {0,-65,-22} (0x004E5BE2/0x004E5BE8)

> type 5 Flipping 4th arg 0 (0x004E5D04/0x004E5D28); angle 3pi/4 (0x004E5C9C vadd d11); translation {dimX/2+56.5771,-56.5771,-22} (0x004E5CC0 vstr s18 / 0x004E5CB8 movt r0,#0xc262 (0xC2624EEF) / 0x004E5CC6 vstr s16)

> PreActionPose ctor 0x0050DCFC stores +0x18 at 0x0050DD46; second ctor 0x0050DB14

> SetHeightTolerance 0x0050DAA4 writes +0x14 at 0x0050DB06 (0.5773503 at 0x0050DB10)

> the +0x18 value consumer: ActionableObject::GetCurrentPreActionPoses 0x004DF850, 0x004DF9DE vldr s28,[sl,#0x18], stride 0x004DFDC4

> ComputePreActionPoseDistThreshold 0x00550098: 3-D distance (0x00550102 vldr s0,[r0,#0x20]; 0x00550122 vsqrt.f32); out[0]=2*dist*sin at 0x00550164/0x005501A4, out[1]=dist*sin at 0x005501A8

> the only tolerance branch is angleTolerance > Radians(0): 0x005500BC blx 0x4A4528 / 0x005500C0 cmp r0,#0 / 0x005500C2 beq 0x5501AE; operator> 0x0084CC90 with the ~1e-5 epsilon 0x3727C5AC; false writes -1.0f at 0x005501B6; a GetWithRespectTo failure writes -1.0f at 0x005501C0; no floor

> Block::LookupBlockInfo symbol 0x004E4C8C; 0x00503DC8 (75.0) is inside MinimalAnglePlanner::ComputeNewPathIfNeeded 0x00503C18 and unrelated

> the world-pose ctor PreActionPose 0x0050DF00 (PLT 0x4A4144): 0x0050DF54 vstr s16,[sl,#0x18] (a); 0x0050DF6C Point3::MakeUnitLength 0x0050E0C0 returns |t|; 0x0050DF7C vadd (|t|+b); 0x0050DFE4 operator*(Pose3d,Pose3d) 0x0084782C; SetHeightTolerance 0x0050E036

> GetCurrentPreActionPoses 0x004DF850: first ctor call 0x004DF99A; param_7 from 0x00550984 ([robot+0x10], named preDockPoseOffset_mm at 0x0055137A); param_8 = 0 on this path (0x00550A2A); second ctor call 0x004DFBE6 with b = min(dist2, d) (0x004DFB88 vldr s0,[sl,#0x18]; 0x004DFB94 bpl); the 0x004DFCFC..0x004DFD00 store is the param_8==1 viz block (0x004DFC5E)

> all three production threshold callers read both outputs with one ldrd (see M12-020)

> preDockPoseOffset_mm: 0x00550984 vldr s16,[r7,#0x10] (r7=r1=PreActionPoseInput); IDockAction+0x10 left 0 by IActionRunner 0x0053FDD0 strd r6,r6,[r5,#0x10]; input+0x10 from IDockAction+0xBC (0x0055161A, 0 at ctor 0x005503AA) and DriveToObjectAction+0x88 (ctor 0x005585A4; no setter; direct callers pass 0); offset-0 branch b=min(dist2,element+0x18)

> GetCurrentPreActionPoses param_2: 0x00550A00 bl 0x004EA398 -> &(Robot+0x298); FUN_004df628 0x004DF628 = pose.GetWithRespectTo(pose.FindRoot()) (0x004DF674/0x004DF67E); objectPose = ObservableObject::GetPose 0x00876840 = &(ObservableObject+4)

> sb rotation: 0x004E5870/0x004E5890/0x004E58B2/0x004E58D4 blx 0x4A47B0 (Y_AXIS_3D); base 0x010590AC (0x004E5876/0x004E587A); angles 0x3FC90FDB/0x40490FDB/0xBFC90FDB; applied with Transform3d::RotateBy 0x0084BB3A (0x004E5A1C/0x004E5AC0/0x004E5B60/0x004E5C36/0x004E5D0A)

> Pose3d(angle, Z_AXIS, translation) with the marker as 4th ctor arg (parent): 0x004E59EE strd r0,r6,[sp]; precompute 0x004E592C..0x004E5938 s16=-size[8]/2, s18=size[4]/2+56.5771; PreActionPose ctor re-roots 0x0050DDA2..0x0050DDB2 passedPose.GetWithRespectTo(marker.parent)

> 0x004DF88A..0x004DF8E8 collection; 0x004DF94A..0x004DF99A filters; 0x004DF9B8..0x004DFBE6 the b computation; 0x004DFC1A..0x004DFC58 the validity push; 0x004DFDD4 the return byte

> re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q2; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md (2.3 formula, 2.2b)

### M13-009

Title: The charger's dimensions, its docked pose and its one pre-dock pose

Status: IMPLEMENTATION_GAP

Evidence (verbatim):

> Charger::Charger 0x004E9B6C stores 96.0/80.0/31.0 at +0xF0..+0xF8 (0x004E9B98/0x004E9BB4/0x004E9BB0/0x004E9BB8) and adds one marker: id 2 (0x004E9C16), angle -pi/2 about Z (0xBFC90FDB at 0x004E9BBC) at (86,0,22) (0x004E9BD6/0x004E9BE2), size Point2 x=27.0 at sp+0x14 and y=20.0 at sp+0x18, the AddMarker pointer being sp+0x14 (0x004E9C28/0x004E9C30/0x004E9C36/0x004E9C38)

> Charger::GetRobotDockedPose 0x004EA1A0 = Pose3d(Radians(pi), Z_AXIS, (30,0,0)) on the charger pose (0x004EA1AC/0x004EA1C6/0x004EA202)

> Charger::GeneratePreActionPoses 0x004E9FB0 emits one pose for action types 0 and 1 only (0x004E9FD4/0x004E9FD6): Pose3d(Radians(p.angle + pi/2), Z_AXIS, (p.x, -p.y, -15.5)) parented to the marker (0x004E9FE4/0x004E9FF2/0x004EA012/0x004EA018/0x004EA01E); p is the file-static Pose2d at 0x01059148 initialised (Radians(0), 0.0, 250.0) at 0x004D6BC4/0x004D6BD6

### M12-045

Title: UnSetCarryingObjects: ascending-ID walk, NullObject error, StillAttached / StillAttachedToSomething warnings, SendSetCarryState(CARRY_NONE) tag 0x49 reliable, carry field resets; and AddRobotRelativeObservation (re-parent to robot, flatten to root, SetPoseHelper state 2 with the broadcast, confirmer entry)

Status: IMPLEMENTATION_GAP

Evidence (verbatim):

> re-analysis/research/20261014-m12-006-unattach.md: U1-U5 0x00633C0C..0x00633D80, SendSetCarryState 0x00632C44..0x00632C6E (tag 0x49); B1-B7 AddRobotRelativeObservation 0x00506B78..0x00506C48, SetPoseHelper 0x0050609C..0x005060FA, ActionableObject::SetPose 0x004E01E4..0x004E0226, Vision SetPose 0x004EF590..0x004EF646; callers C1-C9.

