# R-M12-DETACH-POSE — detach(true) pose and state recipient

Request: `requests/20261021-m12-detach-pose-join.md`. HEADb7b32940. Independent read-only comparison pending checking. No classifier binding or retained-owner census repeated.

## Coverage

| Step | Adopted source / current implementation | Result |
| --- | --- | --- |
| Primary gate/lookup | A1/A2,0x006333E2..0x006333FC; Docking.SetCarriedObjectAsUnattached | Present; missing ID/object returns1, no UnSet/delete |
| Relative pose | A3/A4,0x00633400..0x00633442 and failures; NativePose→GetCheckedMainPose | Present; uses retained live pose, not Latest/flat facade |
| Root/Dirty conversion | B1–B4,0x00506B78..0x00506C48 / SetPoseHelper | Present bounded ARRO reparent/root/Dirty/in-place assign |
| Top pose | A7/A8 after primary | Present; top failure still proceeds to clear/delete |
| IDs / CarryNone order | A9–A11/U5, capture before0x00633882 | Present; reliable49 00 then resets, then optional deletion |
| deleteLocated=true | Existing DeleteLocatedObjects, adopted M11-062/OV110 | Real recipient, not a stub; complete deletion/ownership parity not established here |
| Ownership residue | M12-001/045 cache destruction, M11-077/084 | Separate explicit gaps; not “cube remains on lift” |
| Regression reach | M12PutDownCheckedRowsTests, real motor packet test | Core direct-helper tests and one real packet entry; not whole native pickup/detach production proof |

## Result

**Once the current real Docking recipient is invoked, the main pose calculation/root/Dirty/carried-ID sequence is built.** It no longer leaves the cube attached to the lift or computes its release from a latest flat pose. Reuse/adopt this bounded comparison to retire that historical description where superseded.

It is not a complete detach(true) settlement: the downstream DeleteLocatedObjects and actual wrapper/cached-pose destruction still need their owning records' comparison. A visible message says deletion body “remains unread”, but later checked evidence already recovers that body under M11-062. Correct the stale evidence boundary; do not turn it into a blanket no-effect or complete deletion claim.

## Source contract reused

`re-analysis/research/20261014-m12-006-unattach.md`, adopted in M12-006/045:
- A1/A2: primary unspecified/missing → warning/error, return1, no UnSet. A3's target is **Robot::GetPose(+0x298), not origin** (0x00633442); earlier B-M11-1 check wording was corrected.
- A4 relative conversion failure → error, return1, preserve carrying.
- A5 ARRO with state2 Dirty0x0063344C..0x00633458.
- B1 inside ARRO: copy relative pose, parent to Robot pose, GetWithRespectToRoot and assign. Origin conversion happens here.
- B3 Dirty clears localized-to object first when matching, copies old pose/state, SetPose, then BroadcastObjectPoseChanged.
- B4 actionable SetPose clears caches, assigns into existing object pose node, stores state/name/nonnegative distance; -1 leaves prior distance.
- B6 confirmer count1, root-relative pose and obj last-observed timestamp, then return0.
- A7/A8 top object follows primary; top lookup/conversion failure logs but continues to UnSet. It does not undo the primary observation.
- A9 captures carried IDs before clearing. A10 UnSet(false)0x0063387E..82.
- U5 sends reliable CarryState NONE tag49/state00 before marker3/primaryID=-1, always topID=-1. UnSet itself does not detach pose nodes.
- A11 true argument creates default origin-mode0 filter with captured allowed IDs, calls DeleteLocatedObjects0x00633930; A12 returns0.

Later adopted evidence: `20261012-cliff-world-helpers-check.md` (current M11-062) recovers DeleteLocatedObjects0x0061D0A6..0x0061D602 and ClearLocatedObjectHelper0x00625C78..0x00625EF6. OV110 A5 and OV121 clarify copy/erase/owner cases. The initial unattach report's “unread deletion” is historical, not the current source boundary. No new native algorithm extraction was needed for this comparison.

Pending `20261021-m11-retained-pose-owner-comparison.md` and classifier-owner report are comparison leads, not independent adoption of new source rows.

## Current real recipient

Paths under `cozmo-stack/src/Cozmo.Robot/`.

1. `Manipulation/Docking.cs` ReleaseCarriedObject(forget) calls SetCarriedObjectAsUnattached(deleteLocated). It obtains the current EngineRobot or visibly returns1 preserving state when absent. World mutation/Robot pose gate protect the calculation. This is an implemented recipient, not an unbound function placeholder.

2. Place(primary) looks up actual located object and calls Pose3d.GetWithRespectTo(**obj.NativePose**, robot.GetCheckedMainPose(), relative). NativePose retains the lift-parent chain updated by the real Robot pose/lift primitives. No Vision.History.Latest, object.Pose flattened snapshot or stale saved lift transform is used in this detach calculation. Primary failure returns before UnSet/delete exactly as A2/A4.

3. World.ApplyRobotRelativeObservation copies relative, SetParent(robotPose), flattens with GetWithRespectToRoot and Assign, then WritePoseHelper Dirty with carryingObservation=true. WritePoseHelper clears matching localization, snapshots old NativePose, uses SetCarryingObservationPose, broadcasts and disposes old snapshot, then ARRO updates confirmation.

4. ObservableObject.SetCarryingObservationPose clears cube pre-action cache, calls _pose.Assign rather than replacing its node, stores Dirty and name and initializes cube pose flag. Old FromDistance is retained through -1. The assigned root parent severs the lift relation while retaining node identity, so later lift movement no longer changes the released cube. NativeCarryingTree may remain set as a public flattening-adapter choice; its actual node is now root-parented. That flag alone is not evidence the native pose is still on the lift.

5. Top Place runs after primary's re-root. If T was parented to old C's node, C's in-place assignment preserves its world transform while replacing the parent; T's relative calculation still has a meaningful chain. Top is then independently re-rooted/Dirty. A top error does not roll back primary and does not prevent UnSet. This comparison does not certify pickup's known-top eligibility or rejigger owner cases.

6. IDs are captured before Carrying.UnsetCarrying(false). The real Docking-owned NativeUnset callback walks IDs, emits source error/warning conditions, sends CarryStateUpdate NONE and clears fields. Carrying.ClearNativeCarryingFields does not reparent nodes. Source-consistent detach is supplied by preceding observations, not by treating unset as a generic tree detach.

7. For deleteLocated=true, current code creates default BlockWorldFilter, inserts saved IDs and calls actual World.DeleteLocatedObjects. Thus the transient Dirty/root observations occur before deletion and before the object-deleted API reports; true is not permission to skip the pose calculation/broadcast. Current implementation removes map entries and broadcasts through real world paths, not a counted stub. Its full helper/map/clone/pose-release semantics are downstream scope, not established by this call.

## Remaining narrow gaps

- M12-001/045 still explicitly marks cached pose destructor/retained ownership; list clear is present, complete constructor/lifetime joins remain separate.
- M11-077 generic Dispose/NotLastOwner/final-node effects remain unbuilt. Current DeleteLocatedObjects map removal does not itself prove deterministic native object-pose release; reusing a surviving host reference is not equivalent to its last native wrapper.
- DeleteLocatedObjects current notifications capture o.Pose, a flat snapshot for NativeCarryingTree, and clones/confirmation clearing/map effects have their existing M11 owners. Do not reopen all those algorithms here or claim the captured Dirty pose proves complete API parity.
- M12-046/M12-006 call-site MISSING saying DeleteLocatedObjects “body remains unread in put-down rows” is too broad against the current adopted body and manifest. It should distinguish unverified implementation/ownership from recovered source. Exact diagnostic text alone is not a blocker; misidentifying the missing work can mislead subsequent builds.
- Actual detach invocation outside this recipient (classifier missing hook, facade lifetime/replacement) is the separate owner report. This answer starts once invoked; it does not silently certify that hook.
- Hardware calibration/lift state errors and unreviewed source algorithms remain their own provenance. No plausible pose fallback is added.

## Tests and bounded next step

`M12PutDownCheckedRowsTests` already has meaningful independent expected invariants:
- missing primary and origin mismatch preserve state/no CarryNone;
- released cube preserves NativePose node identity, has actual origin parent, Dirty, prior distance, count1/last-observed confirmation and49 00;
- subsequent ComputeLiftPose does not move released cube;
- top failure still clears both IDs; both objects re-root before UnSet;
- delete IDs captured before unset and actual world delete invoked;
- MotorAutoDisable test feeds a real MotorAutoEnabled packet via Data/Tick and invokes this recipient for lift only.

These fixtures directly place objects/attach state, so they do not prove native pickup geometry or classifier binding. The delete test expressly does not certify remaining deletion semantics. Tests were read, not run.

Smallest next integration scope: keep the working NativePose→Robot-relative→root/Dirty sequence; independently check/adopt its existing build before narrowing stale warnings. Reconcile the true-delete call with adopted M11-062 deletion/owner semantics and close only the concrete release/copy/notification defects it reveals. No reason to replace the supported transform calculation or repeat the retained-owner census.

Source-based regression extension: begin with real parented C/T, nonidentity Robot pose/root and moving lift; invoke true through a supported motor/verification recipient. Observe primary then top Dirty/root broadcasts, saved ID set, CarryNone before field resets, then actual deletion reports. Retain distinct native parent/node identities to distinguish root coordinates from a coincidentally equal flat transform; test primary conversion failure and top failure separately. Validate old-pose API payloads and owner release against adopted native rows; eventual map emptiness alone is insufficient. No new owner policy, hardware or settlement.

## Current record quotes

M12-006 — IMPLEMENTATION_GAP — Letting go of a carried object leaves it Dirty where the lift left it

> 0x006333D4 SetCarriedObjectAsUnattached; 0x00633458 AddRobotRelativeObservation; 0x00633930 DeleteLocatedObjects

> BehaviorPutDownBlock passes false: 0x005C84CE

> PickupObjectAction::Verify passes true: 0x00553CAA, 0x00553D2A

> HandlePickAndPlaceResult 0x00533780: release only when blockStatus==1 (0x005337A8 cmp r0,#1 / 0x005337AA beq) and success!=0 (0x00533898 ldrb r0,[r5,#4] / 0x0053389A cbz 0x5338a6), with bool false (0x005338A0 movs r1,#0 / 0x005338A2 blx 0x4A7B7C SetCarriedObjectAsUnattached)

> attach only when blockStatus==2 (0x005337A4/0x005337A6) and success!=0 (0x00533848 ldrb / 0x0053384A cbz), via SetDockObjectAsAttachedToLift (0x00533850 blx 0x4AA06C)

> PickAndPlaceResult layout: +4 success (0x00533794 ldrb r1,[r5,#4]), +5 dockingResult (0x005337F8 ldrsb), +6 blockStatus (0x0053379E ldrb r0,[r5,#6])

> VisionComponent::EnableMode(1,true) runs unconditionally in the BlockPlaced branch (0x005338AE blx 0x4A91CC)

> re-analysis/research/20261014-m12-006-unattach.md: A1-A12 SetCarriedObjectAsUnattached 0x006333D4..0x006339A2 (warnings/errors CarryingObjectNotSpecified, CarryingObjectDoesNotExist, OriginMisMatch, Top*, Info ObjectPlaced; the robot-relative pose w.r.t. Robot::GetPose 0x004EA398; AddRobotRelativeObservation Dirty; GetCarryingObjects before UnSet; deleteLocated filter); callers C1-C9.


M12-045 — IMPLEMENTATION_GAP — UnSetCarryingObjects: ascending-ID walk, NullObject error, StillAttached / StillAttachedToSomething warnings, SendSetCarryState(CARRY_NONE) tag 0x49 reliable, carry field resets; and AddRobotRelativeObservation (re-parent to robot, flatten to root, SetPoseHelper state 2 with the broadcast, confirmer entry)

> re-analysis/research/20261014-m12-006-unattach.md: U1-U5 0x00633C0C..0x00633D80, SendSetCarryState 0x00632C44..0x00632C6E (tag 0x49); B1-B7 AddRobotRelativeObservation 0x00506B78..0x00506C48, SetPoseHelper 0x0050609C..0x005060FA, ActionableObject::SetPose 0x004E01E4..0x004E0226, Vision SetPose 0x004EF590..0x004EF646; callers C1-C9.


M12-046 — IMPLEMENTATION_GAP — Unattach path still unread: BlockWorld::DeleteLocatedObjects body, CheckAndUpdateTreadsState order against Delocalize (past 0x00512188), the object-name helpers 0x004E80B8/0x004E8048, Robot::SetLocalizedTo body, SetCarryingObject 0x00633E78 (the attach-side CarryState send)

> re-analysis/research/20261014-m12-006-unattach.md: "Still unread" list: DeleteLocatedObjects PLT 0x004A715C; CheckAndUpdateTreadsState 0x00511E00..; 0x004E80B8, 0x004E8048; Robot::SetLocalizedTo; SetCarryingObject 0x00633E78.

> re-analysis/research/overnight/20261016-batch-check.md: SetCarryingObject 0x00633E78..0x00633F0E; SendSetCarryState 0x00632C44..0x00632C6E (tag 0x49); callers AttachObjectToLift 0x006331AE, G2E SetRobotCarryingObject 0x005295F2.
